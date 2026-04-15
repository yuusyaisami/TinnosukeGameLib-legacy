#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.Search;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class WithTargetChannelExecutor : ICommandExecutor
    {
        readonly struct TargetEntry
        {
            public readonly IScopeNode Scope;
            public readonly float DistanceSq;
            public readonly int StableOrder;

            public TargetEntry(IScopeNode scope, float distanceSq, int stableOrder)
            {
                Scope = scope;
                DistanceSq = distanceSq;
                StableOrder = stableOrder;
            }
        }

        public int CommandId => CommandIds.WithTargetChannel;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not WithTargetChannelCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "WithTargetChannelCommandData is required.");

            if (typed.Body == null || typed.Body.Count == 0)
                return;

            var (resolvedScope, error) = await ActorScopeResolver.ResolveAsync(typed.ChannelOwnerSource, ctx, ct);
            var channelOwnerScope = resolvedScope ?? ctx.Scope;
            if (channelOwnerScope == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, string.IsNullOrEmpty(error) ? "Channel owner scope not found." : error);

            if (!TargetChannelTargetPositionSourceHelper.TryResolveRuntimeFromScopeChain(channelOwnerScope, typed.NormalizedChannelTag, out var runtime) || runtime == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"TargetChannel runtime '{typed.NormalizedChannelTag}' not found from owner scope chain.");

            var targets = BuildExecutionTargets(runtime.Hits, typed.Order, typed.MaxTargets);
            if (targets.Count == 0)
                return;

            if (typed.AwaitMode == FlowRunAwaitMode.RunInBackground)
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    var targetScope = targets[i];
                    if (targetScope == null)
                        continue;

                    EnsureScopeBuiltIfNeeded(targetScope);
                    if (!TryResolveRunner(targetScope, out var runner) || runner == null)
                        continue;

                    var vars = ResolveVars(typed.VarsPolicy, ctx, targetScope);
                    var targetCtx = new CommandContext(
                        targetScope,
                        vars,
                        runner,
                        actor: targetScope,
                        options: ctx.Options,
                        commandRootScope: ctx.CommandRootScope,
                        rootActor: ctx.RootActor,
                        callerActor: ctx.Actor,
                        sourceContext: ctx);

                    var task = runner.ExecuteListAsync(typed.Body, targetCtx, ct, ctx.Options);
                    RunInBackground(task);
                }

                return;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                var targetScope = targets[i];
                if (targetScope == null)
                    continue;

                EnsureScopeBuiltIfNeeded(targetScope);
                if (!TryResolveRunner(targetScope, out var runner) || runner == null)
                    continue;

                var vars = ResolveVars(typed.VarsPolicy, ctx, targetScope);
                var targetCtx = new CommandContext(
                    targetScope,
                    vars,
                    runner,
                    actor: targetScope,
                    options: ctx.Options,
                    commandRootScope: ctx.CommandRootScope,
                    rootActor: ctx.RootActor,
                    callerActor: ctx.Actor,
                    sourceContext: ctx);

                var result = await runner.ExecuteListAsync(typed.Body, targetCtx, ct, ctx.Options);
                if (result.Status == CommandRunStatus.Break)
                    break;
                if (result.Status == CommandRunStatus.Canceled)
                    throw new OperationCanceledException();

                if (result.Status == CommandRunStatus.Error || result.FailureCount > 0)
                    throw new CommandExecutionException(result.FailureKind, result.Message ?? "WithTargetChannel body failed.");
            }
        }

        static List<IScopeNode> BuildExecutionTargets(IReadOnlyList<DynamicSearchHit>? hits, WithTargetChannelOrder order, int maxTargets)
        {
            if (hits == null || hits.Count == 0)
                return new List<IScopeNode>(0);

            var entries = new List<TargetEntry>(hits.Count);
            for (int i = 0; i < hits.Count; i++)
            {
                var hit = hits[i];
                if (!TargetChannelTargetPositionSourceHelper.IsHitAlive(hit))
                    continue;

                var scope = hit.Scope;
                if (scope == null)
                    continue;

                var existingIndex = FindEntryIndex(entries, scope);
                if (existingIndex >= 0)
                {
                    var existing = entries[existingIndex];
                    if (hit.DistanceSq < existing.DistanceSq)
                        entries[existingIndex] = new TargetEntry(scope, hit.DistanceSq, existing.StableOrder);
                    continue;
                }

                entries.Add(new TargetEntry(scope, hit.DistanceSq, i));
            }

            entries.Sort((a, b) =>
            {
                var compare = a.DistanceSq.CompareTo(b.DistanceSq);
                if (order == WithTargetChannelOrder.FarFirst)
                    compare = -compare;

                if (compare != 0)
                    return compare;

                return a.StableOrder.CompareTo(b.StableOrder);
            });

            var limit = maxTargets <= 0 ? entries.Count : Math.Min(maxTargets, entries.Count);
            var result = new List<IScopeNode>(limit);
            for (int i = 0; i < limit; i++)
                result.Add(entries[i].Scope);

            return result;
        }

        static int FindEntryIndex(List<TargetEntry> entries, IScopeNode scope)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (ReferenceEquals(entries[i].Scope, scope))
                    return i;
            }

            return -1;
        }

        static void RunInBackground(UniTask<CommandRunResult> task)
        {
            UniTask.Void(async () =>
            {
                try { await task; }
                catch (OperationCanceledException) { }
                catch (ObjectDisposedException) { }
                catch (Exception ex)
                {
                    Debug.LogError($"[WithTargetChannelExecutor] Background execution failed: {ex.Message}");
                }
            });
        }

        static bool TryResolveRunner(IScopeNode scope, out ICommandRunner? runner)
        {
            runner = null;
            var resolver = scope?.Resolver;
            if (resolver == null)
                return false;

            return resolver.TryResolve(out runner) && runner != null;
        }

        static void EnsureScopeBuiltIfNeeded(IScopeNode scope)
        {
            if (scope is BaseLifetimeScope baseScope)
            {
                baseScope.EnsureScopeBuilt();
                return;
            }

            if (scope is RuntimeLifetimeScope runtimeScope)
                runtimeScope.EnsureScopeBuilt();
        }

        static IVarStore ResolveVars(VarsPolicy policy, CommandContext ctx, IScopeNode actorScope)
        {
            if (policy == VarsPolicy.UseActorScopeVars)
            {
                var resolver = actorScope?.Resolver;
                if (resolver != null && resolver.TryResolve<IVarStore>(out var vars) && vars != null)
                    return vars;
            }

            return ctx.Vars;
        }
    }
}

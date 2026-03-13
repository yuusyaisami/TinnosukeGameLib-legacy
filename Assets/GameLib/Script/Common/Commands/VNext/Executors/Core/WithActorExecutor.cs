#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using UnityEngine;
using Game;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class WithActorExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.WithActor;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not WithActorCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "WithActorCommandData is required.");

            if (typed.Body == null || typed.Body.Count == 0)
                return;

            // ActorSourceがCurrentで、CheckSelfIdentityFilterが有効な場合、自分の条件をチェック
            if (typed.ActorSource.Kind == ActorSourceKind.Current && typed.CheckSelfIdentityFilter)
            {
                if (ctx.Actor == null || !MatchesIdentity(ctx.Actor, typed.SelfIdentityFilter))
                {
                    // 自分が条件に合致しないため、実行しない
                    Debug.Log($"[WithActorExecutor] Skipping actor(Kind={(ctx.Actor == null ? "null" : ctx.Actor.Kind.ToString())} id={(ctx.Actor == null ? "null" : ctx.Actor.Identity?.Id)} Category={(ctx.Actor == null ? "null" : ctx.Actor.Identity?.Category)}), \nfilterSetting(kind={typed.SelfIdentityFilter.kind}, id={typed.SelfIdentityFilter.id}, category={typed.SelfIdentityFilter.category}) does not match.");
                    return;
                }
            }

            var (actorScope, error) = await ActorScopeResolver.ResolveAsync(typed.ActorSource, ctx, ct);
            if (actorScope == null)
            {
                if (AllowFallback(ctx.Options))
                {
                    Debug.LogError($"[WithActorExecutor] Actor resolve failed: {error} Falling back to current scope.");
                    actorScope = ctx.Scope;
                }
                else
                {
                    throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, error);
                }
            }

            if (actorScope == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Actor scope could not be resolved (null).");

            var allowFallback = AllowFallback(ctx.Options);
            foreach (var targetScope in GetExecutionTargets(actorScope, typed.ExecutionScope))
            {
                if (targetScope == null)
                    continue;

                await ExecuteBodyOnScope(typed, ctx, targetScope, actorScope, ct, allowFallback);
            }
        }

        static IEnumerable<IScopeNode> GetExecutionTargets(IScopeNode actorScope, WithActorExecutionScope executionScope)
        {
            return executionScope switch
            {
                WithActorExecutionScope.ActorAndDescendants => ScopeNodeHierarchy.EnumerateSubtree(actorScope, includeSelf: true),
                WithActorExecutionScope.DescendantsOnly => ScopeNodeHierarchy.EnumerateSubtree(actorScope, includeSelf: false),
                _ => new[] { actorScope }
            };
        }

        static async UniTask ExecuteBodyOnScope(
            WithActorCommandData typed,
            CommandContext ctx,
            IScopeNode targetScope,
                IScopeNode actorScope,
            CancellationToken ct,
            bool allowFallback)
        {
            var executionScope = targetScope;
            EnsureScopeBuiltIfNeeded(executionScope);

            if (typed.UseDescendantFilter && !DoesScopeMatchFilter(typed.DescendantFilter, executionScope, ctx, actorScope))
            {
                return;
            }

            if (!TryResolveRunner(executionScope, out var runner) || runner == null)
            {
                if (allowFallback)
                {
                    Debug.LogError($"[WithActorExecutor] Actor scope {DescribeScope(executionScope)} has no runner; falling back to current scope.");
                    executionScope = ctx.Scope ?? executionScope;
                    runner = ctx.Runner;
                }
                else
                {
                    throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, $"Actor scope {DescribeScope(executionScope)} has no ICommandRunner.");
                }
            }

            if (runner == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "No ICommandRunner available in actor or fallback scope.");

            var vars = ResolveVars(typed.VarsPolicy, ctx, executionScope);
            var actorCtx = new CommandContext(
                executionScope,
                vars,
                runner,
                executionScope,
                ctx.Options,
                commandRootScope: ctx.CommandRootScope,
                rootActor: ctx.RootActor,
                callerActor: ctx.Actor);

            try
            {
                var result = await runner.ExecuteListAsync(typed.Body, actorCtx, ct, ctx.Options);
                if (result.Status == CommandRunStatus.Canceled)
                    throw new OperationCanceledException();

                if (result.Status == CommandRunStatus.Error || result.FailureCount > 0)
                {
                    var msg = $"Actor command list failed for scope {DescribeScope(executionScope)}. FailureCount={result.FailureCount}, ErrorIndex={result.ErrorIndex}, Message={result.Message}";
                    throw new CommandExecutionException(result.FailureKind, msg);
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"[WithActorExecutor] Actor command list execution was canceled for scope {DescribeScope(executionScope)}.");
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogException(new Exception($"[WithActorExecutor] Exception executing actor commands. ActorScope={DescribeScope(executionScope)}; Runner={(runner == null ? "null" : runner.GetType().Name)}", ex));
                throw;
            }
        }

        static void EnsureScopeBuiltIfNeeded(IScopeNode scope)
        {
            if (scope is BaseLifetimeScope baseScope)
            {
                baseScope.EnsureScopeBuilt();
                return;
            }

            if (scope is RuntimeLifetimeScope runtimeScope)
            {
                runtimeScope.EnsureScopeBuilt();
                return;
            }
        }

        static bool TryResolveRunner(IScopeNode scope, out ICommandRunner? runner)
        {
            runner = null;
            var resolver = scope?.Resolver;
            if (resolver == null)
                return false;

            return resolver.TryResolve(out runner) && runner != null;
        }

        static bool DoesScopeMatchFilter(ActorSource filter, IScopeNode scope, CommandContext ctx, IScopeNode actorScope)
        {
            switch (filter.Kind)
            {
                case ActorSourceKind.Current:
                    return ReferenceEquals(scope, actorScope);
                case ActorSourceKind.GameLogicRoot:
                    var logicRoot = ScopeNodeHierarchy.FindNearestGameLogicRoot(actorScope, includeSelf: true);
                    return logicRoot != null && ReferenceEquals(scope, logicRoot);
                case ActorSourceKind.Player:
                    {
                        var playerScope = ActorSourceFastResolver.Resolve(actorScope, filter);
                        return playerScope != null && ReferenceEquals(scope, playerScope);
                    }
                case ActorSourceKind.Global:
                    {
                        var globalScope = ActorSourceFastResolver.Resolve(actorScope, filter);
                        return globalScope != null && ReferenceEquals(scope, globalScope);
                    }
                case ActorSourceKind.ByIdentity:
                    return MatchesIdentity(scope, filter.Identity);
                case ActorSourceKind.FromUnityObject:
                    if (filter.UnityObject == null)
                        return false;
                    if (TryResolveFromUnityObject(filter.UnityObject, out var target))
                        return ReferenceEquals(scope, target);
                    return false;
                default:
                    return false;
            }
        }

        static bool MatchesIdentity(IScopeNode scope, CommandTargetIdentityFilter filter)
        {
            var identity = scope.Identity;
            if (identity == null)
                return false;
            if (filter.requireActive && !identity.IsActive)
                return false;
            if (filter.kind != LifetimeScopeKind.None && filter.kind != identity.Kind)
                return false;
            if (!string.IsNullOrEmpty(filter.id))
            {
                if (string.IsNullOrEmpty(identity.Id))
                    return false;
                if (!string.Equals(filter.id, identity.Id, StringComparison.Ordinal))
                    return false;
            }
            if (!string.IsNullOrEmpty(filter.category))
            {
                if (string.IsNullOrEmpty(identity.Category))
                    return false;
                if (!string.Equals(filter.category, identity.Category, StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        static IVarStore ResolveVars(VarsPolicy policy, CommandContext ctx, IScopeNode actorScope)
        {
            if (policy == VarsPolicy.UseActorScopeVars)
            {
                var resolver = actorScope?.Resolver;
                if (resolver != null && resolver.TryResolve<IVarStore>(out var vars) && vars != null)
                    return vars;
                return NullVarStore.Instance;
            }

            return ctx.Vars ?? NullVarStore.Instance;
        }

        static bool AllowFallback(CommandRunOptions options)
        {
            if (!options.AllowActorFallback)
                return false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return true;
#else
            return Debug.isDebugBuild;
#endif
        }

        static string DescribeScope(IScopeNode scope)
        {
            if (scope == null)
                return "null";
            try
            {
                var id = scope.Identity?.Id ?? "(no id)";
                return $"{scope.Kind}:{id}";
            }
            catch
            {
                return scope.Kind.ToString();
            }
        }

        static bool TryResolveFromUnityObject(UnityEngine.Object obj, out IScopeNode? scope)
        {
            scope = null;
            // TODO: Implement resolution logic for UnityObject to IScopeNode
            return false;
        }
    }
}

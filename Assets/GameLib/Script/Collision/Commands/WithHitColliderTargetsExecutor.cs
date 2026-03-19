#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Collision;
using Game.Common;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class WithHitColliderTargetsExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.WithHitColliderTargets;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not WithHitColliderTargetsCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "WithHitColliderTargetsCommandData is required.");

            if (typed.Body == null || typed.Body.Count == 0)
                return;

            var (resolvedScope, error) = await ActorScopeResolver.ResolveAsync(typed.ControllerSource, ctx, ct);
            var controllerScope = resolvedScope ?? ctx.Scope;
            if (controllerScope == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, string.IsNullOrEmpty(error) ? "Controller scope not found." : error);

            if (!TryResolveControllerService(controllerScope, out var controllerService) || controllerService == null)
                return;

            var targetScopes = new List<IScopeNode>(16);
            if (!controllerService.TryGetCurrentHitTargetScopes(typed.RuleName, targetScopes))
                return;

            if (typed.AwaitMode == FlowRunAwaitMode.RunInBackground)
            {
                for (int i = 0; i < targetScopes.Count; i++)
                {
                    var targetScope = targetScopes[i];
                    if (targetScope == null)
                        continue;

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

            for (int i = 0; i < targetScopes.Count; i++)
            {
                var targetScope = targetScopes[i];
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
                if (result.Status == CommandRunStatus.Canceled)
                    throw new OperationCanceledException();

                if (result.Status == CommandRunStatus.Error || result.FailureCount > 0)
                    throw new CommandExecutionException(result.FailureKind, result.Message ?? "WithHitColliderTargets body failed.");
            }
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
                    Debug.LogError($"[WithHitColliderTargetsExecutor] Background execution failed: {ex.Message}");
                }
            });
        }

        static bool TryResolveControllerService(IScopeNode scope, out HitColliderControllerService? service)
        {
            service = null;
            if (scope == null)
                return false;

            var resolver = scope.Resolver;
            if (resolver != null && resolver.TryResolve<HitColliderControllerService>(out var direct) && direct != null)
            {
                service = direct;
                return true;
            }

            foreach (var node in ScopeNodeHierarchy.EnumerateSubtree(scope, includeSelf: true))
            {
                var nodeResolver = node?.Resolver;
                if (nodeResolver == null)
                    continue;

                if (nodeResolver.TryResolve<HitColliderControllerService>(out var found) && found != null)
                {
                    service = found;
                    return true;
                }
            }

            return false;
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

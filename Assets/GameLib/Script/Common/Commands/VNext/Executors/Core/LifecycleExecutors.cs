#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using VContainer;
using Game.Times;
using UnityEngine;

namespace Game.Commands.VNext
{
    public sealed class SelfDespawnExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.SelfDespawn;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not SelfDespawnCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "SelfDespawnCommandData is required.");

            var scope = ctx.Scope;
            if (scope == null)
                return;
            if (IsDestroyed(scope))
                return;

            // Use scope.Resolver safely (may be null during teardown). Build a safe dynamic context
            var resolver = scope.Resolver;
            IVarStore varStore = NullVarStore.Instance;
            if (resolver != null)
            {
                resolver.TryResolve<IVarStore>(out var resolvedVars);
                varStore = resolvedVars ?? NullVarStore.Instance;
            }
            var dynCtx = new Game.Common.SimpleDynamicContext(varStore, scope);

            float delay = 0f;
            try
            {
                delay = typed.DelaySeconds.GetOrDefault(dynCtx, 0f);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SelfDespawnExecutor] Failed to evaluate DelaySeconds: {ex.Message}");
                delay = 0f;
            }

            if (delay > 0f)
            {
                if (resolver != null && resolver.TryResolve<ILTSIdentityService>(out var identity) && identity != null)
                    await identity.DelayAsync(delay, ct);
                else
                    await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: ct);
            }

            if (IsDestroyed(scope))
                return;

            if (typed.BeforeDespawnCommands != null && typed.BeforeDespawnCommands.Count > 0)
            {
                var commandResult = await ctx.Runner.ExecuteListAsync(typed.BeforeDespawnCommands, ctx, ct, ctx.Options);
                if (commandResult.Status == CommandRunStatus.Canceled)
                    throw new OperationCanceledException(ct);
                if (commandResult.Status == CommandRunStatus.Error)
                {
                    var msg = string.IsNullOrEmpty(commandResult.Message)
                        ? "BeforeDespawn commands failed."
                        : commandResult.Message;
                    throw new CommandExecutionException(commandResult.FailureKind, msg);
                }
            }

            if (IsDestroyed(scope))
                return;

            if (scope is RuntimeLifetimeScope runtimeScopeForReacquire &&
                typed.OnReacquireCommands != null &&
                typed.OnReacquireCommands.Count > 0 &&
                resolver != null &&
                resolver.TryResolve<IRuntimeLifetimeScopePool>(out var poolForReacquire) &&
                poolForReacquire != null)
            {
                poolForReacquire.TryEnqueueOnNextAcquire(runtimeScopeForReacquire, typed.OnReacquireCommands, ctx.Options);
            }

            ILTSIdentityService? idService = null;
            if (resolver != null && resolver.TryResolve<ILTSIdentityService>(out var resolved))
                idService = resolved;

            var isRuntime = idService?.Kind == LifetimeScopeKind.Runtime || scope is RuntimeLifetimeScope;
            if (isRuntime)
            {
                if (resolver != null && resolver.TryResolve<IRuntimeLifetimeScopePool>(out var pool) && pool != null)
                {
                    if (scope is RuntimeLifetimeScope runtimeScope)
                    {
                        if (IsDestroyed(runtimeScope))
                            return;
                        pool.Release(runtimeScope);
                    }
                    return;
                }
            }

            if (scope is BaseLifetimeScope baseScope)
            {
                await baseScope.DespawnAsync(ct);
                return;
            }

            if (scope is Component comp)
            {
                if (!comp)
                    return;

                var go = comp.gameObject;
                if (!go)
                    return;

                UnityEngine.Object.Destroy(go);
            }
        }

        static bool IsDestroyed(object obj)
        {
            if (obj is not UnityEngine.Object unityObj)
                return false;

            return !unityObj;
        }
    }
}

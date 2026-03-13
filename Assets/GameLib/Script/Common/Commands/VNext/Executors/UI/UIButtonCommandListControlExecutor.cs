#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.UI;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class UIButtonCommandListControlExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.UIButtonCommandListControl;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not UIButtonCommandListControlCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "UIButtonCommandListControlCommandData is required.");

            ct.ThrowIfCancellationRequested();

            var (targetScope, error) = await ActorScopeResolver.ResolveAsync(typed.Target, ctx, ct);
            if (targetScope == null)
            {
                if (AllowFallback(ctx.Options))
                {
                    Debug.LogWarning($"[UIButtonCommandListControlExecutor] Target resolve failed: {error} Falling back to current scope.");
                    targetScope = ctx.Scope;
                }
                else
                {
                    throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, error);
                }
            }

            if (targetScope == null)
                return;

            EnsureScopeBuiltIfNeeded(targetScope);

            if (!TryResolve(targetScope, out IUIButtonCommandListController? controller) || controller == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "IUIButtonCommandListController is missing on target scope.");

            var ok = controller.ApplyCommandList(typed.TargetList, typed.Operation, typed.Commands, typed.SwapTarget);
            if (!ok)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "UIButton command list operation failed.");
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
            }
        }

        static bool TryResolve<T>(IScopeNode scope, out T? value) where T : class
        {
            value = null;
            var resolver = scope?.Resolver;
            if (resolver == null)
                return false;

            return resolver.TryResolve(out value) && value != null;
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
    }
}

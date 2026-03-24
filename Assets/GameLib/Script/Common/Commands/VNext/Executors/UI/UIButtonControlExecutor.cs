#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.UI;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class UIButtonControlExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.UIButtonControl;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not UIButtonControlCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "UIButtonControlCommandData is required.");

            ct.ThrowIfCancellationRequested();

            var (targetScope, error) = await ActorScopeResolver.ResolveAsync(typed.Target, ctx, ct);
            if (targetScope == null)
            {
                if (AllowFallback(ctx.Options))
                {
                    Debug.LogWarning($"[UIButtonControlExecutor] Target resolve failed: {error} Falling back to current scope.");
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

            if (!TryResolve(targetScope, out IUIButtonService? button) || button == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "IUIButtonService is missing on target scope.");

            TryResolve(targetScope, out ICommandListRuntimeMutationService? mutationService);

            ApplyControlState(button, typed);
            ApplyCommandLists(button, typed, mutationService);
            button.RefreshTelemetry();
        }

        static void ApplyControlState(IUIButtonService button, UIButtonControlCommandData typed)
        {
            if (typed.ApplyState)
            {
                button.Kind = typed.Kind;
                button.CanSubmit = typed.CanSubmit;
                button.InputControlCondition = typed.InputControlCondition;
                button.TriggerAction = typed.TriggerAction;
            }

            if (typed.ApplyHold)
            {
                button.HoldTime = typed.HoldTime;
                button.HoldInterval = typed.HoldInterval;
                button.GuardSelectionWhileHolding = typed.GuardSelectionWhileHolding;
            }

            if (typed.ApplyExecutionGuards)
            {
                button.GuardDuringCommandExecution = typed.GuardDuringCommandExecution;
                button.DisableSelectionDuringCommandExecution = typed.DisableSelectionDuringCommandExecution;
            }
        }

        static void ApplyCommandLists(
            IUIButtonService button,
            UIButtonControlCommandData typed,
            ICommandListRuntimeMutationService? mutationService)
        {
            if (typed.ApplySubmitDownCommands)
                button.OnSubmitDownCommands.ApplyRuntimeMutation(typed.SubmitDownCommands, mutationService);

            if (typed.ApplySubmitUpCommands)
                button.OnSubmitUpCommands.ApplyRuntimeMutation(typed.SubmitUpCommands, mutationService);

            if (typed.ApplyHoldDecisionCommands)
                button.OnHoldDecisionCommands.ApplyRuntimeMutation(typed.HoldDecisionCommands, mutationService);

            if (typed.ApplyHoldIntervalCommands)
                button.OnHoldIntervalCommands.ApplyRuntimeMutation(typed.HoldIntervalCommands, mutationService);

            if (typed.ApplyHoldCancelCommands)
                button.OnHoldCancelCommands.ApplyRuntimeMutation(typed.HoldCancelCommands, mutationService);
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

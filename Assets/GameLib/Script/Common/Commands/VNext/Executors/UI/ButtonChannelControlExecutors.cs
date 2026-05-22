#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.UI;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class ButtonChannelHubControlExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.ButtonChannelHubControl;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not ButtonChannelHubControlCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "ButtonChannelHubControlCommandData is required.");

            var targetScope = await ResolveTargetScopeAsync(typed.Target, ctx, ct);
            EnsureScopeBuiltIfNeeded(targetScope);

            if (!TryResolve(targetScope, out IButtonChannelHubService? hub) || hub == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "IButtonChannelHubService is missing on target scope.");

            switch (typed.Operation)
            {
                case ButtonChannelHubControlOperation.RegisterOrReplace:
                    ExecuteRegisterOrReplace(hub, typed, targetScope, ctx);
                    return;

                case ButtonChannelHubControlOperation.Unregister:
                    if (!hub.Unregister(typed.NormalizedChannelTag))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"ButtonChannel '{typed.NormalizedChannelTag}' was not found.");
                    return;

                default:
                    throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Unsupported ButtonChannel hub operation: {typed.Operation}");
            }
        }

        static void ExecuteRegisterOrReplace(
            IButtonChannelHubService hub,
            ButtonChannelHubControlCommandData typed,
            IScopeNode targetScope,
            CommandContext ctx)
        {
            var dynamicContext = new SimpleDynamicContext(ctx.Vars ?? NullVarStore.Instance, targetScope);
            if (!typed.Preset.TryGet(dynamicContext, out ButtonChannelPreset? preset) || preset == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "ButtonChannel preset could not be resolved.");

            if (!hub.RegisterOrReplace(typed.NormalizedChannelTag, preset))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"ButtonChannel '{typed.NormalizedChannelTag}' could not be registered.");
        }

        internal static async UniTask<IScopeNode> ResolveTargetScopeAsync(ActorSource target, CommandContext ctx, CancellationToken ct)
        {
            var (targetScope, error) = await ActorScopeResolver.ResolveAsync(target, ctx, ct);
            if (targetScope != null)
                return targetScope;

            if (AllowFallback(ctx.Options) && ctx.Scope != null)
            {
                Debug.LogWarning($"[ButtonChannelHubControlExecutor] Target resolve failed: {error} Falling back to current scope.");
                return ctx.Scope;
            }

            throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, error);
        }

        internal static void EnsureScopeBuiltIfNeeded(IScopeNode scope)
        {
            ScopeFeatureInstallerUtility.EnsureScopeBuiltIfNeeded(scope);
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

    public sealed class ButtonChannelPlayerControlExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.ButtonChannelPlayerControl;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not ButtonChannelPlayerControlCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "ButtonChannelPlayerControlCommandData is required.");

            var targetScope = await ButtonChannelHubControlExecutor.ResolveTargetScopeAsync(typed.Target, ctx, ct);
            ButtonChannelHubControlExecutor.EnsureScopeBuiltIfNeeded(targetScope);

            if (!TryResolve(targetScope, out IButtonChannelHubService? hub) || hub == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "IButtonChannelHubService is missing on target scope.");

            if (!hub.TryGetControl(typed.NormalizedChannelTag, out var control) || control == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"ButtonChannel '{typed.NormalizedChannelTag}' was not found.");

            TryResolve(targetScope, out ICommandListRuntimeMutationService? mutationService);

            switch (typed.Operation)
            {
                case ButtonChannelPlayerControlOperation.SwapInputPreset:
                    ExecuteSwapInputPreset(control, typed, targetScope, ctx);
                    return;

                case ButtonChannelPlayerControlOperation.SwapPlayerPreset:
                    ExecuteSwapPlayerPreset(control, typed, targetScope, ctx);
                    return;

                case ButtonChannelPlayerControlOperation.MutateInputSettings:
                    if (typed.InputMutation == null || !typed.InputMutation.HasAnyMutation())
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "ButtonChannel input mutation is empty.");
                    if (!control.MutateInputSettings(typed.InputMutation, mutationService))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "ButtonChannel input mutation did not change any runtime settings.");
                    return;

                case ButtonChannelPlayerControlOperation.MutatePlayerSettings:
                    if (typed.PlayerMutation == null || !typed.PlayerMutation.HasAnyMutation())
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "ButtonChannel player mutation is empty.");
                    if (!control.MutatePlayerSettings(typed.PlayerMutation, mutationService))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "ButtonChannel player mutation did not change any runtime settings.");
                    return;

                case ButtonChannelPlayerControlOperation.ResetRuntimeOverrides:
                    if (!control.ResetRuntimeOverrides(typed.ResetInput, typed.ResetPlayer))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "ButtonChannel reset operation requires at least one reset target.");
                    return;

                default:
                    throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Unsupported ButtonChannel player operation: {typed.Operation}");
            }
        }

        static void ExecuteSwapInputPreset(
            IButtonChannelControlService control,
            ButtonChannelPlayerControlCommandData typed,
            IScopeNode targetScope,
            CommandContext ctx)
        {
            var dynamicContext = new SimpleDynamicContext(ctx.Vars ?? NullVarStore.Instance, targetScope);
            if (!typed.InputPreset.TryGet(dynamicContext, out ButtonInputPresetBase? preset) || preset == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "ButtonChannel input preset could not be resolved.");

            if (!control.SwapInputPreset(preset))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "ButtonChannel input preset swap failed.");
        }

        static void ExecuteSwapPlayerPreset(
            IButtonChannelControlService control,
            ButtonChannelPlayerControlCommandData typed,
            IScopeNode targetScope,
            CommandContext ctx)
        {
            var dynamicContext = new SimpleDynamicContext(ctx.Vars ?? NullVarStore.Instance, targetScope);
            if (!typed.PlayerPreset.TryGet(dynamicContext, out ButtonPlayerPresetBase? preset) || preset == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "ButtonChannel player preset could not be resolved.");

            if (!control.SwapPlayerPreset(preset))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "ButtonChannel player preset swap failed.");
        }

        static bool TryResolve<T>(IScopeNode scope, out T? value) where T : class
        {
            value = null;
            var resolver = scope?.Resolver;
            if (resolver == null)
                return false;

            return resolver.TryResolve(out value) && value != null;
        }
    }
}

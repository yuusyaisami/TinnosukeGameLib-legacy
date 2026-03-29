#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.UI;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class TooltipChannelHubControlExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.TooltipChannelHubControl;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not TooltipChannelHubControlCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "TooltipChannelHubControlCommandData is required.");

            var targetScope = await CommandListChannelHubControlExecutor.ResolveTargetScopeAsync(typed.Target, ctx, ct);
            CommandListChannelHubControlExecutor.EnsureScopeBuiltIfNeeded(targetScope);

            if (!TryResolve(targetScope, out ITooltipChannelHubService? hub) || hub == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "ITooltipChannelHubService is missing on target scope.");

            switch (typed.Operation)
            {
                case TooltipChannelHubControlOperation.RegisterOrReplace:
                    ExecuteRegisterOrReplace(hub, typed, targetScope, ctx);
                    return;

                case TooltipChannelHubControlOperation.Unregister:
                    if (!hub.Unregister(typed.NormalizedChannelTag))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"TooltipChannel '{typed.NormalizedChannelTag}' was not found.");
                    return;

                case TooltipChannelHubControlOperation.ClearAll:
                    hub.ClearAll();
                    return;

                case TooltipChannelHubControlOperation.SwapHubPreset:
                    ExecuteSwapHubPreset(hub, typed, targetScope, ctx);
                    return;

                default:
                    throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Unsupported TooltipChannel hub operation: {typed.Operation}");
            }
        }

        static void ExecuteRegisterOrReplace(
            ITooltipChannelHubService hub,
            TooltipChannelHubControlCommandData typed,
            IScopeNode targetScope,
            CommandContext ctx)
        {
            var dynamicContext = new SimpleDynamicContext(ctx.Vars ?? NullVarStore.Instance, targetScope);
            if (!typed.PlayerPreset.TryGet(dynamicContext, out TooltipPlayerPreset? preset) || preset == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Tooltip player preset could not be resolved.");

            if (!hub.RegisterOrReplace(typed.NormalizedChannelTag, preset))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"TooltipChannel '{typed.NormalizedChannelTag}' could not be registered.");
        }

        static void ExecuteSwapHubPreset(
            ITooltipChannelHubService hub,
            TooltipChannelHubControlCommandData typed,
            IScopeNode targetScope,
            CommandContext ctx)
        {
            var dynamicContext = new SimpleDynamicContext(ctx.Vars ?? NullVarStore.Instance, targetScope);
            if (!typed.HubPreset.TryGet(dynamicContext, out TooltipHubPreset? preset) || preset == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Tooltip hub preset could not be resolved.");

            if (!hub.SwapHubPreset(preset))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Tooltip hub preset swap failed.");
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

    public sealed class TooltipChannelExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.TooltipChannel;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not TooltipChannelCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "TooltipChannelCommandData is required.");

            var targetScope = await CommandListChannelHubControlExecutor.ResolveTargetScopeAsync(typed.Target, ctx, ct);
            CommandListChannelHubControlExecutor.EnsureScopeBuiltIfNeeded(targetScope);

            if (!TryResolve(targetScope, out ITooltipChannelHubService? hub) || hub == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "ITooltipChannelHubService is missing on target scope.");

            if (!hub.TryGetCommand(typed.NormalizedChannelTag, out var command) || command == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"TooltipChannel '{typed.NormalizedChannelTag}' was not found.");

            switch (typed.Operation)
            {
                case TooltipChannelOperation.ForceShow:
                    if (!command.ForceShow())
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "TooltipChannel force show failed.");
                    return;

                case TooltipChannelOperation.ForceHide:
                    if (!command.ForceHide())
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "TooltipChannel force hide failed.");
                    return;

                case TooltipChannelOperation.ClearForceOverride:
                    if (!command.ClearForceOverride())
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "TooltipChannel had no force override.");
                    return;

                default:
                    throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Unsupported TooltipChannel operation: {typed.Operation}");
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
    }

    public sealed class TooltipChannelPlayerControlExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.TooltipChannelPlayerControl;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not TooltipChannelPlayerControlCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "TooltipChannelPlayerControlCommandData is required.");

            var targetScope = await CommandListChannelHubControlExecutor.ResolveTargetScopeAsync(typed.Target, ctx, ct);
            CommandListChannelHubControlExecutor.EnsureScopeBuiltIfNeeded(targetScope);

            if (!TryResolve(targetScope, out ITooltipChannelHubService? hub) || hub == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "ITooltipChannelHubService is missing on target scope.");

            if (!hub.TryGetControl(typed.NormalizedChannelTag, out var control) || control == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"TooltipChannel '{typed.NormalizedChannelTag}' was not found.");

            switch (typed.Operation)
            {
                case TooltipChannelPlayerControlOperation.SwapPlayerPreset:
                    ExecuteSwapPlayerPreset(control, typed, targetScope, ctx);
                    return;

                case TooltipChannelPlayerControlOperation.SwapCommandsPreset:
                    ExecuteSwapCommandsPreset(control, typed, targetScope, ctx);
                    return;

                case TooltipChannelPlayerControlOperation.ResetRuntimeOverrides:
                    if (!control.ResetRuntimeOverrides(typed.ResetPlayer, typed.ResetCommands, typed.ResetForceOverride))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "TooltipChannel reset operation requires at least one reset target.");
                    return;

                default:
                    throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Unsupported TooltipChannel player operation: {typed.Operation}");
            }
        }

        static void ExecuteSwapPlayerPreset(
            ITooltipChannelControlService control,
            TooltipChannelPlayerControlCommandData typed,
            IScopeNode targetScope,
            CommandContext ctx)
        {
            var dynamicContext = new SimpleDynamicContext(ctx.Vars ?? NullVarStore.Instance, targetScope);
            if (!typed.PlayerPreset.TryGet(dynamicContext, out TooltipPlayerPreset? preset) || preset == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Tooltip player preset could not be resolved.");

            if (!control.SwapPlayerPreset(preset))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Tooltip player preset swap failed.");
        }

        static void ExecuteSwapCommandsPreset(
            ITooltipChannelControlService control,
            TooltipChannelPlayerControlCommandData typed,
            IScopeNode targetScope,
            CommandContext ctx)
        {
            var dynamicContext = new SimpleDynamicContext(ctx.Vars ?? NullVarStore.Instance, targetScope);
            if (!typed.CommandsPreset.TryGet(dynamicContext, out TooltipCommandsPreset? preset) || preset == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Tooltip commands preset could not be resolved.");

            if (!control.SwapCommandsPreset(preset))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Tooltip commands preset swap failed.");
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

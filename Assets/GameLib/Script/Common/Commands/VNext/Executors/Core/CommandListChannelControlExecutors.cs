#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Commands;
using Game.Common;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class CommandListChannelHubControlExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.CommandListChannelHubControl;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not CommandListChannelHubControlCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "CommandListChannelHubControlCommandData is required.");

            var targetScope = await ResolveTargetScopeAsync(typed.Target, ctx, ct);
            EnsureScopeBuiltIfNeeded(targetScope);

            if (!TryResolve(targetScope, out ICommandListChannelHubService? hub) || hub == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "ICommandListChannelHubService is missing on target scope.");

            switch (typed.Operation)
            {
                case CommandListChannelHubControlOperation.RegisterOrReplace:
                    ExecuteRegisterOrReplace(hub, typed, targetScope, ctx);
                    return;

                case CommandListChannelHubControlOperation.Unregister:
                    if (!hub.Unregister(typed.NormalizedChannelTag))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"CommandListChannel '{typed.NormalizedChannelTag}' was not found.");
                    return;

                case CommandListChannelHubControlOperation.ClearAll:
                    hub.Clear();
                    return;

                default:
                    throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Unsupported CommandListChannel hub operation: {typed.Operation}");
            }
        }

        static void ExecuteRegisterOrReplace(
            ICommandListChannelHubService hub,
            CommandListChannelHubControlCommandData typed,
            IScopeNode targetScope,
            CommandContext ctx)
        {
            var dynamicContext = new SimpleDynamicContext(ctx.Vars ?? NullVarStore.Instance, targetScope);
            if (!typed.Preset.TryGet(dynamicContext, out CommandListChannelPreset? preset) || preset == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "CommandListChannel preset could not be resolved.");

            if (!hub.RegisterOrReplace(typed.NormalizedChannelTag, preset))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"CommandListChannel '{typed.NormalizedChannelTag}' could not be registered.");
        }

        internal static async UniTask<IScopeNode> ResolveTargetScopeAsync(ActorSource target, CommandContext ctx, CancellationToken ct)
        {
            var (targetScope, error) = await ActorScopeResolver.ResolveAsync(target, ctx, ct);
            if (targetScope != null)
                return targetScope;

            if (AllowFallback(ctx.Options) && ctx.Scope != null)
            {
                Debug.LogWarning($"[CommandListChannelHubControlExecutor] Target resolve failed: {error} Falling back to current scope.");
                return ctx.Scope;
            }

            throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, error);
        }

        internal static void EnsureScopeBuiltIfNeeded(IScopeNode scope)
        {
            if (scope is BaseLifetimeScope baseScope)
            {
                baseScope.EnsureScopeBuilt();
                return;
            }

            if (scope is RuntimeLifetimeScope runtimeScope)
                runtimeScope.EnsureScopeBuilt();
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

    public sealed class CommandListChannelExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.CommandListChannel;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not CommandListChannelCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "CommandListChannelCommandData is required.");

            var targetScope = await CommandListChannelHubControlExecutor.ResolveTargetScopeAsync(typed.Target, ctx, ct);
            CommandListChannelHubControlExecutor.EnsureScopeBuiltIfNeeded(targetScope);

            if (!TryResolve(targetScope, out ICommandListChannelHubService? hub) || hub == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "ICommandListChannelHubService is missing on target scope.");

            if (!hub.TryGetCommand(typed.NormalizedChannelTag, out var command) || command == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"CommandListChannel '{typed.NormalizedChannelTag}' was not found.");

            switch (typed.Operation)
            {
                case CommandListChannelOperation.Play:
                    if (!command.Play(ctx.Vars))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"CommandListChannel '{typed.NormalizedChannelTag}' is already active.");

                    await AwaitIfNeededAsync(command, typed.AwaitMode, ct);
                    return;

                case CommandListChannelOperation.Pause:
                    if (!command.Pause())
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"CommandListChannel '{typed.NormalizedChannelTag}' is not playing.");
                    return;

                case CommandListChannelOperation.Resume:
                    if (!command.Resume())
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"CommandListChannel '{typed.NormalizedChannelTag}' is not paused.");

                    await AwaitIfNeededAsync(command, typed.AwaitMode, ct);
                    return;

                case CommandListChannelOperation.Stop:
                    if (!command.Stop())
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"CommandListChannel '{typed.NormalizedChannelTag}' could not stop.");
                    return;

                case CommandListChannelOperation.ExecuteNow:
                    if (!command.ExecuteNow(ctx.Vars))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"CommandListChannel '{typed.NormalizedChannelTag}' is busy.");

                    await AwaitIfNeededAsync(command, typed.AwaitMode, ct);
                    return;

                default:
                    throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Unsupported CommandListChannel operation: {typed.Operation}");
            }
        }

        static UniTask AwaitIfNeededAsync(
            ICommandListChannelCommandService command,
            FlowRunAwaitMode awaitMode,
            CancellationToken ct)
        {
            if (awaitMode != FlowRunAwaitMode.WaitForCompletion)
                return UniTask.CompletedTask;

            return command.WaitForCurrentExecutionAsync(ct);
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

    public sealed class CommandListChannelPlayerControlExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.CommandListChannelPlayerControl;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not CommandListChannelPlayerControlCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "CommandListChannelPlayerControlCommandData is required.");

            var targetScope = await CommandListChannelHubControlExecutor.ResolveTargetScopeAsync(typed.Target, ctx, ct);
            CommandListChannelHubControlExecutor.EnsureScopeBuiltIfNeeded(targetScope);

            if (!TryResolve(targetScope, out ICommandListChannelHubService? hub) || hub == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "ICommandListChannelHubService is missing on target scope.");

            if (!hub.TryGetControl(typed.NormalizedChannelTag, out var control) || control == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"CommandListChannel '{typed.NormalizedChannelTag}' was not found.");

            TryResolve(targetScope, out ICommandListRuntimeMutationService? mutationService);

            switch (typed.Operation)
            {
                case CommandListChannelPlayerControlOperation.SwapCommandListPreset:
                    ExecuteSwapCommandListPreset(control, typed, targetScope, ctx);
                    return;

                case CommandListChannelPlayerControlOperation.SwapPlayerPreset:
                    ExecuteSwapPlayerPreset(control, typed, targetScope, ctx);
                    return;

                case CommandListChannelPlayerControlOperation.MutateCommands:
                    ExecuteMutateCommands(control, typed, targetScope, ctx, mutationService);
                    return;

                case CommandListChannelPlayerControlOperation.SetRuntimeVars:
                    if (!control.SetRuntimeVars(typed.Payload, ctx.Vars, typed.OverwriteExistingVars))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "CommandListChannel runtime vars could not be updated.");
                    return;

                case CommandListChannelPlayerControlOperation.ClearRuntimeVars:
                    if (!control.ClearRuntimeVars())
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "CommandListChannel runtime vars could not be cleared.");
                    return;

                case CommandListChannelPlayerControlOperation.ResetRuntimeOverrides:
                    if (!control.ResetRuntimeOverrides(typed.ResetCommands, typed.ResetPlayer, typed.ResetRuntimeVars, typed.ResetPlaybackState))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "CommandListChannel reset operation requires at least one reset target.");
                    return;

                default:
                    throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Unsupported CommandListChannel player operation: {typed.Operation}");
            }
        }

        static void ExecuteSwapCommandListPreset(
            ICommandListChannelControlService control,
            CommandListChannelPlayerControlCommandData typed,
            IScopeNode targetScope,
            CommandContext ctx)
        {
            var dynamicContext = new SimpleDynamicContext(ctx.Vars ?? NullVarStore.Instance, targetScope);
            if (!typed.CommandListPreset.TryGet(dynamicContext, out CommandListPreset? preset) || preset == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "CommandList preset could not be resolved.");

            if (!control.SwapCommandListPreset(preset))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "CommandList preset swap failed.");
        }

        static void ExecuteSwapPlayerPreset(
            ICommandListChannelControlService control,
            CommandListChannelPlayerControlCommandData typed,
            IScopeNode targetScope,
            CommandContext ctx)
        {
            var dynamicContext = new SimpleDynamicContext(ctx.Vars ?? NullVarStore.Instance, targetScope);
            if (!typed.PlayerPreset.TryGet(dynamicContext, out CommandListPlayerPreset? preset) || preset == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "CommandList player preset could not be resolved.");

            if (!control.SwapPlayerPreset(preset))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "CommandList player preset swap failed.");
        }

        static void ExecuteMutateCommands(
            ICommandListChannelControlService control,
            CommandListChannelPlayerControlCommandData typed,
            IScopeNode targetScope,
            CommandContext ctx,
            ICommandListRuntimeMutationService? mutationService)
        {
            var step = new CommandListMutationStep
            {
                Operation = typed.Mutation.Operation,
                Commands = typed.Mutation.Commands,
            };

            if (step.RequiresCommands())
            {
                var dynamicContext = new SimpleDynamicContext(ctx.Vars ?? NullVarStore.Instance, targetScope);
                if (!typed.MutationCommands.TryGet(dynamicContext, out CommandListData? commands) || commands == null)
                    throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "CommandListChannel mutation command list could not be resolved.");

                step.Commands = commands;
            }

            if (!control.MutateCommands(step, mutationService))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "CommandListChannel commands could not be mutated.");
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

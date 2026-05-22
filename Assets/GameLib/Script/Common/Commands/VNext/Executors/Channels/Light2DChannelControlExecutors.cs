#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Channel;
using Game.Common;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class Light2DChannelHubControlExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.Light2DChannelHubControl;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not Light2DChannelHubControlCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Light2DChannelHubControlCommandData is required.");

            var targetScope = await ResolveTargetScopeAsync(typed.Target, ctx, ct);
            EnsureScopeBuiltIfNeeded(targetScope);

            if (!TryResolve(targetScope, out ILight2DChannelHubService? hub) || hub == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "ILight2DChannelHubService is missing on target scope.");

            switch (typed.Operation)
            {
                case Light2DChannelHubControlOperation.SwapSourcePreset:
                    ExecuteSwapSourcePreset(hub, typed, targetScope, ctx);
                    return;

                default:
                    throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Unsupported Light2D hub operation: {typed.Operation}");
            }
        }

        static void ExecuteSwapSourcePreset(
            ILight2DChannelHubService hub,
            Light2DChannelHubControlCommandData typed,
            IScopeNode targetScope,
            CommandContext ctx)
        {
            var dynamicContext = new SimpleDynamicContext(ctx.Vars ?? NullVarStore.Instance, targetScope);
            if (!typed.SourcePreset.TryGet(dynamicContext, out Light2DPreset? preset) || preset == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Light2D source preset could not be resolved.");

            if (!hub.SwapSourcePreset(typed.NormalizedChannelTag, preset))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Light2D channel '{typed.NormalizedChannelTag}' could not swap source preset.");
        }

        internal static async UniTask<IScopeNode> ResolveTargetScopeAsync(ActorSource target, CommandContext ctx, CancellationToken ct)
        {
            var (targetScope, error) = await ActorScopeResolver.ResolveAsync(target, ctx, ct);
            if (targetScope != null)
                return targetScope;

            if (AllowFallback(ctx.Options) && ctx.Scope != null)
            {
                Debug.LogWarning($"[Light2DChannelHubControlExecutor] Target resolve failed: {error} Falling back to current scope.");
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

    public sealed class Light2DChannelPlayerControlExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.Light2DChannelPlayerControl;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not Light2DChannelPlayerControlCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Light2DChannelPlayerControlCommandData is required.");

            var targetScope = await Light2DChannelHubControlExecutor.ResolveTargetScopeAsync(typed.Target, ctx, ct);
            Light2DChannelHubControlExecutor.EnsureScopeBuiltIfNeeded(targetScope);

            if (!TryResolve(targetScope, out ILight2DChannelHubService? hub) || hub == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "ILight2DChannelHubService is missing on target scope.");

            if (!hub.TryGetControl(typed.NormalizedChannelTag, out var control) || control == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"Light2D channel '{typed.NormalizedChannelTag}' was not found.");

            switch (typed.Operation)
            {
                case Light2DChannelPlayerControlOperation.SwapPlayerPreset:
                    ExecuteSwapPlayerPreset(control, typed, targetScope, ctx);
                    return;

                case Light2DChannelPlayerControlOperation.MutatePlayerPreset:
                    if (typed.PlayerMutation == null || !typed.PlayerMutation.HasAnyMutation())
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Light2D player mutation is empty.");
                    if (!control.MutatePlayerPreset(typed.PlayerMutation))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Light2D player mutation did not change any runtime settings.");
                    return;

                case Light2DChannelPlayerControlOperation.SetGlobalIntensity:
                    ExecuteSetGlobalIntensity(control, typed, targetScope, ctx);
                    return;

                case Light2DChannelPlayerControlOperation.ResetGlobalIntensity:
                    if (!control.ResetGlobalIntensity())
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Light2D global intensity override was not active.");
                    return;

                case Light2DChannelPlayerControlOperation.ReplaceEffect:
                    ExecuteReplaceEffect(control, typed, targetScope, ctx);
                    return;

                case Light2DChannelPlayerControlOperation.MutateEffect:
                    if (typed.EffectMutation == null || !typed.EffectMutation.HasAnyMutation())
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Light2D effect mutation is empty.");
                    if (!control.MutateEffect(typed.NormalizedEffectId, typed.EffectMutation))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Light2D effect '{typed.NormalizedEffectId}' could not be mutated.");
                    return;

                case Light2DChannelPlayerControlOperation.SetEffectEnabled:
                    if (!control.SetEffectEnabled(typed.NormalizedEffectId, typed.SetEffectEnabled))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Light2D effect '{typed.NormalizedEffectId}' was not found.");
                    return;

                case Light2DChannelPlayerControlOperation.RemoveEffect:
                    if (!control.RemoveEffect(typed.NormalizedEffectId))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Light2D effect '{typed.NormalizedEffectId}' was not found.");
                    return;

                case Light2DChannelPlayerControlOperation.ResetRuntimeOverrides:
                    if (!control.ResetRuntimeOverrides(typed.ResetPlayerPreset, typed.ResetEffects, typed.ResetGlobalIntensity))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Light2D reset operation requires at least one active runtime override.");
                    return;

                case Light2DChannelPlayerControlOperation.RestoreBaseline:
                    control.RestoreBaseline();
                    return;

                default:
                    throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Unsupported Light2D player operation: {typed.Operation}");
            }
        }

        static void ExecuteSwapPlayerPreset(
            ILight2DChannelControlService control,
            Light2DChannelPlayerControlCommandData typed,
            IScopeNode targetScope,
            CommandContext ctx)
        {
            var dynamicContext = new SimpleDynamicContext(ctx.Vars ?? NullVarStore.Instance, targetScope);
            if (!typed.PlayerPreset.TryGet(dynamicContext, out Light2DPlayerPreset? preset) || preset == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Light2D player preset could not be resolved.");

            if (!control.SwapPlayerPreset(preset))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Light2D player preset swap failed.");
        }

        static void ExecuteSetGlobalIntensity(
            ILight2DChannelControlService control,
            Light2DChannelPlayerControlCommandData typed,
            IScopeNode targetScope,
            CommandContext ctx)
        {
            var dynamicContext = new SimpleDynamicContext(ctx.Vars ?? NullVarStore.Instance, targetScope);
            if (!typed.GlobalIntensity.TryGet(dynamicContext, out var intensity))
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Light2D global intensity could not be resolved.");

            if (!control.SetGlobalIntensity(intensity))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Light2D global intensity did not change.");
        }

        static void ExecuteReplaceEffect(
            ILight2DChannelControlService control,
            Light2DChannelPlayerControlCommandData typed,
            IScopeNode targetScope,
            CommandContext ctx)
        {
            var dynamicContext = new SimpleDynamicContext(ctx.Vars ?? NullVarStore.Instance, targetScope);
            if (!typed.EffectPreset.TryGet(dynamicContext, out Light2DEffectPresetBase? preset) || preset == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Light2D effect preset could not be resolved.");

            if (!control.ReplaceEffect(
                    typed.NormalizedEffectId,
                    preset,
                    typed.EffectPriority,
                    typed.EffectBlendMode,
                    typed.EffectEnabled))
            {
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Light2D effect '{typed.NormalizedEffectId}' could not be replaced.");
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
}

#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.UI;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class SliderControlExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.SliderControl;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not SliderControlCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "SliderControlCommandData is required.");

            ct.ThrowIfCancellationRequested();

            var (targetScope, error) = await ActorScopeResolver.ResolveAsync(typed.Target, ctx, ct);
            if (targetScope == null)
            {
                if (AllowFallback(ctx.Options))
                {
                    Debug.LogWarning($"[SliderControlExecutor] Target resolve failed: {error} Falling back to current scope.");
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

            if (!TryResolve(targetScope, out ISliderChannelHubService? channelHub) || channelHub == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "ISliderChannelHubService is missing on target scope.");

            if (!channelHub.TryGetControl(typed.NormalizedChannelTag, out var controlService) || controlService == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"Slider channel '{typed.NormalizedChannelTag}' was not found.");

            TryResolve(targetScope, out ICommandListRuntimeMutationService? mutationService);

            switch (typed.Operation)
            {
                case SliderControlOperation.SwapPreset:
                    ExecuteSwapPreset(controlService, typed, targetScope, ctx);
                    return;

                case SliderControlOperation.MutateSettings:
                    if (!controlService.MutateSettings(
                            typed.ApplyVisualizerMutation ? typed.VisualizerMutation : null,
                            typed.ApplyPlayerMutation ? typed.PlayerMutation : null,
                            mutationService))
                    {
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "SliderControl mutate operation did not change any runtime settings.");
                    }
                    return;

                case SliderControlOperation.ResetRuntimeOverrides:
                    if (!controlService.ResetRuntimeOverrides(typed.ResetVisualizer, typed.ResetPlayer))
                    {
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "SliderControl reset operation requires at least one reset target.");
                    }
                    return;

                default:
                    throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Unsupported SliderControl operation: {typed.Operation}");
            }
        }

        static void ExecuteSwapPreset(
            ISliderControlService controlService,
            SliderControlCommandData typed,
            IScopeNode targetScope,
            CommandContext ctx)
        {
            var dynamicContext = new SimpleDynamicContext(ctx.Vars ?? NullVarStore.Instance, targetScope);

            SliderVisualizerPreset? visualizerPreset = null;
            if (typed.ApplyVisualizerPreset)
            {
                if (!typed.VisualizerPreset.TryGet(dynamicContext, out visualizerPreset) || visualizerPreset == null)
                    throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Failed to resolve visualizer preset.");
            }

            SliderPlayerPreset? playerPreset = null;
            if (typed.ApplyPlayerPreset)
            {
                if (!typed.PlayerPreset.TryGet(dynamicContext, out playerPreset) || playerPreset == null)
                    throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Failed to resolve player preset.");
            }

            if (!controlService.SwapPreset(typed.ApplyVisualizerPreset, visualizerPreset, typed.ApplyPlayerPreset, playerPreset))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "SliderControl swap operation requires at least one resolved preset.");
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

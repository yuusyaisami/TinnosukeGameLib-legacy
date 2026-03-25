#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.UI;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class WorldSliderControlExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.WorldSliderControl;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not WorldSliderControlCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "WorldSliderControlCommandData is required.");

            ct.ThrowIfCancellationRequested();

            var (targetScope, error) = await ActorScopeResolver.ResolveAsync(typed.Target, ctx, ct);
            if (targetScope == null)
            {
                if (AllowFallback(ctx.Options))
                {
                    Debug.LogWarning($"[WorldSliderControlExecutor] Target resolve failed: {error} Falling back to current scope.");
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

            if (!TryResolve(targetScope, out IWorldSliderControlService? controlService) || controlService == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "IWorldSliderControlService is missing on target scope.");

            TryResolve(targetScope, out ICommandListRuntimeMutationService? mutationService);

            switch (typed.Operation)
            {
                case WorldSliderControlOperation.SwapPreset:
                    ExecuteSwapPreset(controlService, typed, targetScope, ctx);
                    return;

                case WorldSliderControlOperation.MutateSettings:
                    if (!controlService.MutateSettings(
                            typed.ApplyVisualizerMutation ? typed.VisualizerMutation : null,
                            typed.ApplyPlayerMutation ? typed.PlayerMutation : null,
                            mutationService))
                    {
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "WorldSliderControl mutate operation did not change any runtime settings.");
                    }
                    return;

                case WorldSliderControlOperation.ResetRuntimeOverrides:
                    if (!controlService.ResetRuntimeOverrides(typed.ResetVisualizer, typed.ResetPlayer))
                    {
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "WorldSliderControl reset operation requires at least one reset target.");
                    }
                    return;

                default:
                    throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Unsupported WorldSliderControl operation: {typed.Operation}");
            }
        }

        static void ExecuteSwapPreset(
            IWorldSliderControlService controlService,
            WorldSliderControlCommandData typed,
            IScopeNode targetScope,
            CommandContext ctx)
        {
            var dynamicContext = new SimpleDynamicContext(ctx.Vars ?? NullVarStore.Instance, targetScope);

            WorldSliderVisualizerPreset? visualizerPreset = null;
            if (typed.ApplyVisualizerPreset)
            {
                if (!typed.VisualizerPreset.TryGet(dynamicContext, out visualizerPreset) || visualizerPreset == null)
                    throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Failed to resolve visualizer preset.");
            }

            WorldSliderPlayerPreset? playerPreset = null;
            if (typed.ApplyPlayerPreset)
            {
                if (!typed.PlayerPreset.TryGet(dynamicContext, out playerPreset) || playerPreset == null)
                    throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Failed to resolve player preset.");
            }

            if (!controlService.SwapPreset(typed.ApplyVisualizerPreset, visualizerPreset, typed.ApplyPlayerPreset, playerPreset))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "WorldSliderControl swap operation requires at least one resolved preset.");
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

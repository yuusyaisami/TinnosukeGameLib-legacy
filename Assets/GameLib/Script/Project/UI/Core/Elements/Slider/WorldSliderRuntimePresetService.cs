#nullable enable
using System;
using Game.Commands.VNext;
using Game.Common;
using VContainer;

namespace Game.UI
{
    public sealed class WorldSliderRuntimePresetService :
        IWorldSliderRuntimePresetProvider,
        IWorldSliderControlService,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        readonly IWorldSliderOptions _options;

        WorldSliderVisualizerPreset _baseVisualizerPreset = new();
        WorldSliderPlayerPreset _basePlayerPreset = new();
        WorldSliderVisualizerPreset _currentVisualizerPreset = new();
        WorldSliderPlayerPreset _currentPlayerPreset = new();

        public WorldSliderVisualizerPreset CurrentVisualizerPreset => _currentVisualizerPreset;
        public WorldSliderPlayerPreset CurrentPlayerPreset => _currentPlayerPreset;

        public event Action? OnVisualizerPresetChanged;
        public event Action? OnPlayerPresetChanged;

        public WorldSliderRuntimePresetService(IWorldSliderOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = isReset;

            var vars = ResolveVars(scope);
            var dynamicContext = new SimpleDynamicContext(vars, scope);
            _baseVisualizerPreset = WorldSliderRuntimeHelpers.ResolveVisualizerPreset(_options.VisualizerPresetValue, dynamicContext).CreateRuntimeCopy();
            _basePlayerPreset = WorldSliderRuntimeHelpers.ResolvePlayerPreset(_options.PlayerPresetValue, dynamicContext).CreateRuntimeCopy();
            _currentVisualizerPreset = _baseVisualizerPreset.CreateRuntimeCopy();
            _currentPlayerPreset = _basePlayerPreset.CreateRuntimeCopy();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;

            _baseVisualizerPreset = new WorldSliderVisualizerPreset();
            _basePlayerPreset = new WorldSliderPlayerPreset();
            _currentVisualizerPreset = new WorldSliderVisualizerPreset();
            _currentPlayerPreset = new WorldSliderPlayerPreset();
        }

        public bool SwapPreset(
            bool applyVisualizer,
            WorldSliderVisualizerPreset? visualizerPreset,
            bool applyPlayer,
            WorldSliderPlayerPreset? playerPreset)
        {
            var changed = false;

            if (applyVisualizer)
            {
                if (visualizerPreset == null)
                    return false;

                _baseVisualizerPreset = visualizerPreset.CreateRuntimeCopy();
                _currentVisualizerPreset = _baseVisualizerPreset.CreateRuntimeCopy();
                changed = true;
                OnVisualizerPresetChanged?.Invoke();
            }

            if (applyPlayer)
            {
                if (playerPreset == null)
                    return false;

                _basePlayerPreset = playerPreset.CreateRuntimeCopy();
                _currentPlayerPreset = _basePlayerPreset.CreateRuntimeCopy();
                changed = true;
                OnPlayerPresetChanged?.Invoke();
            }

            return changed;
        }

        public bool MutateSettings(
            WorldSliderVisualizerRuntimeMutation? visualizerMutation,
            WorldSliderPlayerRuntimeMutation? playerMutation,
            ICommandListRuntimeMutationService? mutationService)
        {
            var changed = false;

            if (visualizerMutation != null && visualizerMutation.HasAnyMutation())
            {
                _currentVisualizerPreset.ApplyMutation(visualizerMutation, mutationService);
                changed = true;
                OnVisualizerPresetChanged?.Invoke();
            }

            if (playerMutation != null && playerMutation.HasAnyMutation())
            {
                _currentPlayerPreset.ApplyMutation(playerMutation, mutationService);
                changed = true;
                OnPlayerPresetChanged?.Invoke();
            }

            return changed;
        }

        public bool ResetRuntimeOverrides(bool resetVisualizer, bool resetPlayer)
        {
            var changed = false;

            if (resetVisualizer)
            {
                _currentVisualizerPreset = _baseVisualizerPreset.CreateRuntimeCopy();
                changed = true;
                OnVisualizerPresetChanged?.Invoke();
            }

            if (resetPlayer)
            {
                _currentPlayerPreset = _basePlayerPreset.CreateRuntimeCopy();
                changed = true;
                OnPlayerPresetChanged?.Invoke();
            }

            return changed;
        }

        static IVarStore ResolveVars(IScopeNode scope)
        {
            if (scope?.Resolver != null &&
                scope.Resolver.TryResolve<IVarStore>(out var vars) &&
                vars != null)
            {
                return vars;
            }

            return NullVarStore.Instance;
        }
    }
}

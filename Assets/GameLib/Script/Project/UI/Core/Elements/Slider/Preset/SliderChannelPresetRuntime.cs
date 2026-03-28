#nullable enable
using System;
using Game.Commands.VNext;
using Game.Common;
using VContainer;

namespace Game.UI
{
    public sealed class SliderChannelPresetRuntime :
        ISliderRuntimePresetProvider,
        ISliderControlService,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        readonly ISliderOptions _options;

        SliderVisualizerPreset _baseVisualizerPreset = new();
        SliderPlayerPreset _basePlayerPreset = new();
        SliderVisualizerPreset _currentVisualizerPreset = new();
        SliderPlayerPreset _currentPlayerPreset = new();

        public SliderVisualizerPreset CurrentVisualizerPreset => _currentVisualizerPreset;
        public SliderPlayerPreset CurrentPlayerPreset => _currentPlayerPreset;

        public event Action? OnVisualizerPresetChanged;
        public event Action? OnPlayerPresetChanged;

        public SliderChannelPresetRuntime(ISliderOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = isReset;

            var vars = ResolveVars(scope);
            var dynamicContext = new SimpleDynamicContext(vars, scope);
            _baseVisualizerPreset = _options.VisualizerPresetValue.GetOrDefault(dynamicContext, new SliderVisualizerPreset()).CreateRuntimeCopy();
            _basePlayerPreset = _options.PlayerPresetValue.GetOrDefault(dynamicContext, new SliderPlayerPreset()).CreateRuntimeCopy();
            _currentVisualizerPreset = _baseVisualizerPreset.CreateRuntimeCopy();
            _currentPlayerPreset = _basePlayerPreset.CreateRuntimeCopy();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;

            _baseVisualizerPreset = new SliderVisualizerPreset();
            _basePlayerPreset = new SliderPlayerPreset();
            _currentVisualizerPreset = new SliderVisualizerPreset();
            _currentPlayerPreset = new SliderPlayerPreset();
        }

        public bool SwapPreset(
            bool applyVisualizer,
            SliderVisualizerPreset? visualizerPreset,
            bool applyPlayer,
            SliderPlayerPreset? playerPreset)
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
            SliderVisualizerRuntimeMutation? visualizerMutation,
            SliderPlayerRuntimeMutation? playerMutation,
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

#nullable enable
using UnityEngine;

namespace Game.UI
{
    internal sealed class SliderChannelVisualizerRuntime
    {
        readonly ISliderVisualBackend _backend;

        public SliderChannelVisualizerRuntime(
            IScopeNode owner,
            ISliderOptions options,
            ISliderPlayerRuntime output,
            ISliderRuntimePresetProvider presetProvider,
            SliderEnvironmentKind environmentKind,
            Canvas? canvas)
        {
            _backend = environmentKind == SliderEnvironmentKind.ScreenUI && canvas != null
                ? new ScreenSliderVisualBackend(owner, options, output, presetProvider, canvas)
                : new WorldSpaceSliderVisualBackend(owner, options, output, presetProvider);
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _backend.OnAcquire(scope, isReset);
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _backend.OnRelease(scope, isReset);
        }

        public void Tick()
        {
            _backend.Tick();
        }
    }
}

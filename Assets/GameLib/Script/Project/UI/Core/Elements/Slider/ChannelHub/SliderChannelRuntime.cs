#nullable enable

namespace Game.UI
{
    internal sealed class SliderChannelRuntime
    {
        public string Tag = "default";
        public SliderChannelOptions Options = null!;
        public SliderChannelPresetRuntime Preset = null!;
        public SliderChannelPlayerRuntime Player = null!;
        public SliderChannelVisualizerRuntime Visualizer = null!;
        public ISliderInteractionRuntime? Interaction;
    }
}

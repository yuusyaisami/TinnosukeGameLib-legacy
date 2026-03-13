#nullable enable
using Game.Input;
namespace Game.UI
{
    public interface IUINavigationTelemetry
    {
        IUIInputConsumer? CurrentTarget { get; }
        NavigateDirection LastNavigateDirection { get; }
        float NavigateRepeatTimer { get; }
        float NavigateThreshold { get; }
        float RepeatDelay { get; }
        float RepeatRate { get; }
        InputUsageMode InputUsageMode { get; }
    }
}

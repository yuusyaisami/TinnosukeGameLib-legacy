#nullable enable
using System;
using Sirenix.OdinInspector;
using Game.Input;

namespace Game.UI
{
    [Serializable]
    public sealed class UINavigationDebugView : IDisposable
    {
        [ShowInInspector, ReadOnly]
        public string CurrentTarget = "(none)";

        [ShowInInspector, ReadOnly]
        public NavigateDirection LastDirection = NavigateDirection.None;

        [ShowInInspector, ReadOnly]
        public float RepeatTimer = 0f;

        [ShowInInspector, ReadOnly]
        public float Threshold = 0f;

        [ShowInInspector, ReadOnly]
        public float RepeatDelay = 0f;

        [ShowInInspector, ReadOnly]
        public float RepeatRate = 0f;

        [ShowInInspector, ReadOnly]
        public InputUsageMode InputUsageMode = InputUsageMode.Pointer;

        IUINavigationTelemetry? _telemetry;

        public void Bind(IUINavigationTelemetry telemetry)
        {
            Unbind();
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
            Refresh();
        }

        void Refresh()
        {
            if (_telemetry == null) return;
            CurrentTarget = _telemetry.CurrentTarget?.ToString() ?? "(none)";
            LastDirection = _telemetry.LastNavigateDirection;
            RepeatTimer = _telemetry.NavigateRepeatTimer;
            Threshold = _telemetry.NavigateThreshold;
            RepeatDelay = _telemetry.RepeatDelay;
            RepeatRate = _telemetry.RepeatRate;
            InputUsageMode = _telemetry.InputUsageMode;
        }

        public void Unbind() { _telemetry = null; }
        public void Dispose() => Unbind();
    }
}

#nullable enable
using UnityEngine;
using Sirenix.OdinInspector;
using Game.Input;

namespace Game.UI
{
    [DisallowMultipleComponent]
    public sealed class UINavigationDebugViewerMB : MonoBehaviour
    {
        IUINavigationTelemetry? _telemetry;

        public void AttachTelemetry(IUINavigationTelemetry tel) => _telemetry = tel;

        [ShowInInspector, ReadOnly]
        public string CurrentTarget => _telemetry?.CurrentTarget?.ToString() ?? "(null)";

        [ShowInInspector, ReadOnly]
        public NavigateDirection LastDirection => _telemetry?.LastNavigateDirection ?? NavigateDirection.None;

        [ShowInInspector, ReadOnly]
        public float RepeatTimer => _telemetry?.NavigateRepeatTimer ?? 0f;

        [ShowInInspector, ReadOnly]
        public float Threshold => _telemetry?.NavigateThreshold ?? 0f;

    }
}

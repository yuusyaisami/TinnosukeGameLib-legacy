#nullable enable
using System;
using Game.Common;
using VContainer;

namespace Game.UI
{
    public sealed class UIButtonTelemetryInspectorBridge : IScopeAcquireHandler, IScopeReleaseHandler
    {
        readonly UIButtonMB _owner;
        IUIButtonTelemetry? _telemetry;

        public UIButtonTelemetryInspectorBridge(UIButtonMB owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            var resolver = scope?.Resolver;
            if (resolver == null)
                return;

            if (resolver.TryResolve<IUIButtonTelemetry>(out var t) && t != null)
            {
                _telemetry = t;
                _telemetry.OnTelemetryUpdated += HandleTelemetry;
            }
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            if (_telemetry != null)
            {
                _telemetry.OnTelemetryUpdated -= HandleTelemetry;
                _telemetry = null;
            }
        }

        void HandleTelemetry(UIButtonTelemetrySnapshot snapshot)
        {
            _owner.SetInspectorTelemetry(snapshot);
        }
    }
}

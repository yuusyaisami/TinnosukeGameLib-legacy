#nullable enable
using System;
using Game.Common;
using VContainer;
using UnityEngine;

namespace Game.UI
{
    // Bridge that subscribes to IUISliderTelemetry and writes into the parent UISliderMB's serialized state
    public sealed class UISliderTelemetryInspectorBridge : IScopeAcquireHandler, IScopeReleaseHandler
    {
        readonly UISliderMB _owner;
        IUISliderTelemetry? _telemetry;

        public UISliderTelemetryInspectorBridge(UISliderMB owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            var resolver = scope?.Resolver;
            if (resolver == null)
                return;

            if (resolver.TryResolve<IUISliderTelemetry>(out var t) && t != null)
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

        void HandleTelemetry(UISliderTelemetrySnapshot snapshot)
        {
            // Update the serializable inspector state on the owner (runs on Unity main thread since telemetry runs on main thread)
            _owner.SetInspectorTelemetry(snapshot);
        }
    }
}

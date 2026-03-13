#nullable enable
using System;
using UnityEngine;
using VContainer.Unity;

namespace Game.AI
{
    public sealed class AIAgentDebugService : ITickable, IDisposable
    {
        readonly AIAgentMB _mb;
        readonly AIStateDebugViewer _viewer;
        readonly IAIStateTelemetry _telemetry;
        readonly IAIStateService _state;

        int _lastVersion = -1;
        bool _disposed;

        public AIAgentDebugService(
            AIAgentMB mb,
            AIStateDebugViewer viewer,
            IAIStateService state,
            IAIStateTelemetry telemetry)
        {
            _mb = mb ? mb : throw new ArgumentNullException(nameof(mb));
            _viewer = viewer ?? throw new ArgumentNullException(nameof(viewer));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));

            _viewer.Bind(_telemetry);
            _lastVersion = _telemetry.TelemetryVersion;
        }

        public void Tick()
        {
            if (_disposed)
                return;

            if (!_mb)
                return;

            _mb.SetDebugState(_state.ActiveClipKey, _state.StackDepth);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var version = _telemetry.TelemetryVersion;
            if (version != _lastVersion)
            {
                _lastVersion = version;
                _viewer.Refresh();
            }
#endif
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _viewer.Bind(null);
        }
    }
}


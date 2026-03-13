#nullable enable
using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Movement
{
    public interface IInputDirectionTelemetry
    {
        bool HasDirection { get; }
        Vector2 RawDirection { get; }
        Vector2 ProcessedDirection { get; }
        InputMovementInput LastMovementInput { get; }
        string LastSourceName { get; }
    }

    [Serializable]
    public sealed class InputDirectionDebugView : IDisposable
    {
        [ShowInInspector, ReadOnly]
        public bool HasDirection
        {
            get { Refresh(); return _hasDirection; }
        }

        [ShowInInspector, ReadOnly]
        public Vector2 RawDirection
        {
            get { Refresh(); return _rawDirection; }
        }

        [ShowInInspector, ReadOnly]
        public Vector2 ProcessedDirection
        {
            get { Refresh(); return _processedDirection; }
        }

        [ShowInInspector, ReadOnly]
        public InputMovementInput LastMovementInput
        {
            get { Refresh(); return _lastMovementInput; }
        }

        [ShowInInspector, ReadOnly]
        public string LastSourceName
        {
            get { Refresh(); return _lastSourceName; }
        }

        IInputDirectionTelemetry? _telemetry;
        bool _hasDirection;
        Vector2 _rawDirection;
        Vector2 _processedDirection;
        InputMovementInput _lastMovementInput;
        string _lastSourceName = "(none)";

        public void Bind(IInputDirectionTelemetry telemetry)
        {
            if (telemetry == null)
                return;
            _telemetry = telemetry;
            Refresh();
        }

        public void Unbind()
        {
            _telemetry = null;
        }

        public void Dispose() => Unbind();

        void Refresh()
        {
            if (_telemetry == null)
                return;
            _hasDirection = _telemetry.HasDirection;
            _rawDirection = _telemetry.RawDirection;
            _processedDirection = _telemetry.ProcessedDirection;
            _lastMovementInput = _telemetry.LastMovementInput;
            _lastSourceName = _telemetry.LastSourceName;
        }
    }
}

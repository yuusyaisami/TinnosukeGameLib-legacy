#nullable enable
using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Movement
{
    public interface IInputMovementTelemetry
    {
        bool HasInput { get; }
        Vector2 RawDirection { get; }
        Vector2 ProcessedDirection { get; }
        InputMovementInput LastInput { get; }
        float SpeedMultiplier { get; }
        bool AccelerationActive { get; }
        float ActiveAccel { get; }
        float ActiveDecel { get; }
        Vector2 TargetVelocity { get; }
        Vector2 CurrentVelocity { get; }
        Vector2 BaseDirection { get; }
        Vector2 GuidanceDirection { get; }
        float SpeedBase { get; }
        float FinalSpeedMul { get; }
        Vector2 FinalDirection { get; }
        Vector2 AdditiveVelocity { get; }
    }

    [Serializable]
    public sealed class InputMovementDebugView : IDisposable
    {
        [ShowInInspector, ReadOnly]
        public bool HasInput { get { Refresh(); return _hasInput; } }

        [ShowInInspector, ReadOnly]
        public Vector2 RawDirection { get { Refresh(); return _rawDirection; } }

        [ShowInInspector, ReadOnly]
        public Vector2 ProcessedDirection { get { Refresh(); return _processedDirection; } }

        [ShowInInspector, ReadOnly]
        public InputMovementInput LastInput { get { Refresh(); return _lastInput; } }

        [ShowInInspector, ReadOnly]
        public float SpeedMultiplier { get { Refresh(); return _speedMultiplier; } }

        [ShowInInspector, ReadOnly]
        public bool AccelerationActive { get { Refresh(); return _accelerationActive; } }

        [ShowInInspector, ReadOnly]
        public float ActiveAccel { get { Refresh(); return _activeAccel; } }

        [ShowInInspector, ReadOnly]
        public float ActiveDecel { get { Refresh(); return _activeDecel; } }

        [ShowInInspector, ReadOnly]
        public Vector2 TargetVelocity { get { Refresh(); return _targetVelocity; } }

        [ShowInInspector, ReadOnly]
        public Vector2 CurrentVelocity { get { Refresh(); return _currentVelocity; } }

        [ShowInInspector, ReadOnly]
        public Vector2 BaseDirection { get { Refresh(); return _baseDirection; } }

        [ShowInInspector, ReadOnly]
        public Vector2 GuidanceDirection { get { Refresh(); return _guidanceDirection; } }

        [ShowInInspector, ReadOnly]
        public float SpeedBase { get { Refresh(); return _speedBase; } }

        [ShowInInspector, ReadOnly]
        public float FinalSpeedMul { get { Refresh(); return _finalSpeedMul; } }

        [ShowInInspector, ReadOnly]
        public Vector2 FinalDirection { get { Refresh(); return _finalDirection; } }

        [ShowInInspector, ReadOnly]
        public Vector2 AdditiveVelocity { get { Refresh(); return _additiveVelocity; } }

        IInputMovementTelemetry? _telemetry;
        bool _hasInput;
        Vector2 _rawDirection;
        Vector2 _processedDirection;
        InputMovementInput _lastInput;
        float _speedMultiplier;
        bool _accelerationActive;
        float _activeAccel;
        float _activeDecel;
        Vector2 _targetVelocity;
        Vector2 _currentVelocity;
        Vector2 _baseDirection;
        Vector2 _guidanceDirection;
        float _speedBase;
        float _finalSpeedMul;
        Vector2 _finalDirection;
        Vector2 _additiveVelocity;

        public void Bind(IInputMovementTelemetry telemetry)
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

            _hasInput = _telemetry.HasInput;
            _rawDirection = _telemetry.RawDirection;
            _processedDirection = _telemetry.ProcessedDirection;
            _lastInput = _telemetry.LastInput;
            _speedMultiplier = _telemetry.SpeedMultiplier;
            _accelerationActive = _telemetry.AccelerationActive;
            _activeAccel = _telemetry.ActiveAccel;
            _activeDecel = _telemetry.ActiveDecel;
            _targetVelocity = _telemetry.TargetVelocity;
            _currentVelocity = _telemetry.CurrentVelocity;
            _baseDirection = _telemetry.BaseDirection;
            _guidanceDirection = _telemetry.GuidanceDirection;
            _speedBase = _telemetry.SpeedBase;
            _finalSpeedMul = _telemetry.FinalSpeedMul;
            _finalDirection = _telemetry.FinalDirection;
            _additiveVelocity = _telemetry.AdditiveVelocity;
        }
    }
}

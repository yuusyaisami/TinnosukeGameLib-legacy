#nullable enable
using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.TransformSystem
{
    public interface ITransformControllerTelemetry
    {
        Vector2 CurrentVelocity { get; }
        float CurrentAngularVelocity { get; }
        string TargetName { get; }
        TransformOutputTarget OutputTarget { get; }
        bool MovementEnabled { get; }
        bool RotationEnabled { get; }
        bool HasRotationAdapter { get; }
        bool Rigidbody2DFreezeRotation { get; }
        float Rigidbody2DAngularVelocity { get; }
        float Rigidbody2DRotation { get; }
    }

    [Serializable]
    public sealed class TransformControllerDebugView : IDisposable
    {
        [ShowInInspector, ReadOnly]
        public Vector2 CurrentVelocity { get { Refresh(); return _currentVelocity; } }

        [ShowInInspector, ReadOnly]
        public float CurrentAngularVelocity { get { Refresh(); return _currentAngularVelocity; } }

        [ShowInInspector, ReadOnly]
        public string TargetName { get { Refresh(); return _targetName; } }

        [ShowInInspector, ReadOnly]
        public TransformOutputTarget OutputTarget { get { Refresh(); return _outputTarget; } }

        [ShowInInspector, ReadOnly]
        public bool MovementEnabled { get { Refresh(); return _movementEnabled; } }

        [ShowInInspector, ReadOnly]
        public bool RotationEnabled { get { Refresh(); return _rotationEnabled; } }

        [ShowInInspector, ReadOnly]
        public bool HasRotationAdapter { get { Refresh(); return _hasRotationAdapter; } }

        [ShowInInspector, ReadOnly]
        public bool Rigidbody2DFreezeRotation { get { Refresh(); return _rigidbody2DFreezeRotation; } }

        [ShowInInspector, ReadOnly]
        public float Rigidbody2DAngularVelocity { get { Refresh(); return _rigidbody2DAngularVelocity; } }

        [ShowInInspector, ReadOnly]
        public float Rigidbody2DRotation { get { Refresh(); return _rigidbody2DRotation; } }

        ITransformControllerTelemetry? _telemetry;
        Vector2 _currentVelocity;
        float _currentAngularVelocity;
        string _targetName = "(none)";
        TransformOutputTarget _outputTarget;
        bool _movementEnabled;
        bool _rotationEnabled;
        bool _hasRotationAdapter;
        bool _rigidbody2DFreezeRotation;
        float _rigidbody2DAngularVelocity;
        float _rigidbody2DRotation;

        public void Bind(ITransformControllerTelemetry telemetry)
        {
            if (telemetry == null)
                return;
            _telemetry = telemetry;
            Refresh();
        }

        public void Unbind() => _telemetry = null;

        public void Dispose() => Unbind();

        void Refresh()
        {
            if (_telemetry == null)
                return;

            _currentVelocity = _telemetry.CurrentVelocity;
            _currentAngularVelocity = _telemetry.CurrentAngularVelocity;
            _targetName = _telemetry.TargetName;
            _outputTarget = _telemetry.OutputTarget;
            _movementEnabled = _telemetry.MovementEnabled;
            _rotationEnabled = _telemetry.RotationEnabled;
            _hasRotationAdapter = _telemetry.HasRotationAdapter;
            _rigidbody2DFreezeRotation = _telemetry.Rigidbody2DFreezeRotation;
            _rigidbody2DAngularVelocity = _telemetry.Rigidbody2DAngularVelocity;
            _rigidbody2DRotation = _telemetry.Rigidbody2DRotation;
        }
    }
}

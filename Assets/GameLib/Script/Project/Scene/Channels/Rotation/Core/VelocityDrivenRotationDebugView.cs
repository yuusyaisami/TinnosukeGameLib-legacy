#nullable enable
using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Rotation
{
    public interface IVelocityDrivenRotationTelemetry
    {
        bool Enabled { get; }
        VelocityRotationMode Mode { get; }
        VelocityRotationSourceKind SourceKind { get; }
        string RotateChannelKey { get; }
        bool HasChannel { get; }
        bool HasSourceTransformChannel { get; }
        bool HasSourceRigidbody2D { get; }
        Vector2 RawVelocity { get; }
        Vector2 ScaledVelocity { get; }
        float SpeedScale { get; }
        float DirectionAngle { get; }
        float TargetAngle { get; }
        float DeltaAngle { get; }
        float TargetAngularVelocity { get; }
        float AppliedAngularVelocity { get; }
    }

    [Serializable]
    public sealed class VelocityDrivenRotationDebugView : IDisposable
    {
        [ShowInInspector, ReadOnly] public bool Enabled { get { Refresh(); return _enabled; } }
        [ShowInInspector, ReadOnly] public VelocityRotationMode Mode { get { Refresh(); return _mode; } }
        [ShowInInspector, ReadOnly] public VelocityRotationSourceKind SourceKind { get { Refresh(); return _sourceKind; } }
        [ShowInInspector, ReadOnly] public string RotateChannelKey { get { Refresh(); return _rotateChannelKey; } }
        [ShowInInspector, ReadOnly] public bool HasChannel { get { Refresh(); return _hasChannel; } }
        [ShowInInspector, ReadOnly] public bool HasSourceTransformChannel { get { Refresh(); return _hasSourceTransformChannel; } }
        [ShowInInspector, ReadOnly] public bool HasSourceRigidbody2D { get { Refresh(); return _hasSourceRigidbody2D; } }
        [ShowInInspector, ReadOnly] public Vector2 RawVelocity { get { Refresh(); return _rawVelocity; } }
        [ShowInInspector, ReadOnly] public Vector2 ScaledVelocity { get { Refresh(); return _scaledVelocity; } }
        [ShowInInspector, ReadOnly] public float SpeedScale { get { Refresh(); return _speedScale; } }
        [ShowInInspector, ReadOnly] public float DirectionAngle { get { Refresh(); return _directionAngle; } }
        [ShowInInspector, ReadOnly] public float TargetAngle { get { Refresh(); return _targetAngle; } }
        [ShowInInspector, ReadOnly] public float DeltaAngle { get { Refresh(); return _deltaAngle; } }
        [ShowInInspector, ReadOnly] public float TargetAngularVelocity { get { Refresh(); return _targetAngularVelocity; } }
        [ShowInInspector, ReadOnly] public float AppliedAngularVelocity { get { Refresh(); return _appliedAngularVelocity; } }

        IVelocityDrivenRotationTelemetry? _telemetry;
        bool _enabled;
        VelocityRotationMode _mode;
        VelocityRotationSourceKind _sourceKind;
        string _rotateChannelKey = "(none)";
        bool _hasChannel;
        bool _hasSourceTransformChannel;
        bool _hasSourceRigidbody2D;
        Vector2 _rawVelocity;
        Vector2 _scaledVelocity;
        float _speedScale = 1f;
        float _directionAngle;
        float _targetAngle;
        float _deltaAngle;
        float _targetAngularVelocity;
        float _appliedAngularVelocity;

        public void Bind(IVelocityDrivenRotationTelemetry telemetry)
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

            _enabled = _telemetry.Enabled;
            _mode = _telemetry.Mode;
            _sourceKind = _telemetry.SourceKind;
            _rotateChannelKey = _telemetry.RotateChannelKey;
            _hasChannel = _telemetry.HasChannel;
            _hasSourceTransformChannel = _telemetry.HasSourceTransformChannel;
            _hasSourceRigidbody2D = _telemetry.HasSourceRigidbody2D;
            _rawVelocity = _telemetry.RawVelocity;
            _scaledVelocity = _telemetry.ScaledVelocity;
            _speedScale = _telemetry.SpeedScale;
            _directionAngle = _telemetry.DirectionAngle;
            _targetAngle = _telemetry.TargetAngle;
            _deltaAngle = _telemetry.DeltaAngle;
            _targetAngularVelocity = _telemetry.TargetAngularVelocity;
            _appliedAngularVelocity = _telemetry.AppliedAngularVelocity;
        }
    }
}

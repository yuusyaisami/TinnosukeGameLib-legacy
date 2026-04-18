#nullable enable
using System;
using System.Runtime.CompilerServices;
using DG.Tweening;
using Game;
using Game.Channel;
using Game.MaterialFx;
using Game.Scalar;
using Game.TransformSystem;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace Game.Rotation
{
    public sealed class VelocityDrivenRotationService :
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        ITickable,
        IDisposable,
        IVelocityRotationSettingsAdapter,
        IVelocityDrivenRotationTelemetry
    {
        readonly IRotateChannelHub _rotateHub;
        readonly IObjectResolver _resolver;
        readonly IBaseScalarService? _scalarService;
        IMaterialFxServiceFactory? _materialFxFactory;

        VelocityRotationSettings _settings;
        IRotateChannelHandle? _channel;

        Transform? _sourceTransform;
        Rigidbody2D? _sourceRigidbody;
        ITransformChannelPoseReader? _sourceTransformChannel;
        Transform? _scopeTransform;

        float _currentAngularVelocity;
        bool _disposed;
        Vector2 _lastRawVelocity;
        Vector2 _lastScaledVelocity;
        float _lastSpeedScale = 1f;
        float _lastDirectionAngle;
        float _lastTargetAngle;
        float _lastDeltaAngle;
        float _lastTargetAngularVelocity;
        float _lastAppliedAngularVelocity;

        IMaterialFxService? _flipMaterialFx;
        FlipController? _flipController;
        string _flipContext = string.Empty;
        int _flipTargetInstanceId;
        bool _flipActive;
        bool _lastFlipX;
        const int FlipLayerPriority = 1000;

        public VelocityRotationSettings CurrentSettings => _settings;
        public bool Enabled => _settings.Enabled;
        public VelocityRotationMode Mode => _settings.Mode;
        public VelocityRotationSourceKind SourceKind => _settings.Source;
        public string RotateChannelKey => string.IsNullOrEmpty(_settings.RotateChannelKey) ? "velocity" : _settings.RotateChannelKey;
        public bool HasChannel => _channel != null;
        public bool HasSourceTransformChannel => _sourceTransformChannel != null;
        public bool HasSourceRigidbody2D => _sourceRigidbody != null;
        public Vector2 RawVelocity => _lastRawVelocity;
        public Vector2 ScaledVelocity => _lastScaledVelocity;
        public float SpeedScale => _lastSpeedScale;
        public float DirectionAngle => _lastDirectionAngle;
        public float TargetAngle => _lastTargetAngle;
        public float DeltaAngle => _lastDeltaAngle;
        public float TargetAngularVelocity => _lastTargetAngularVelocity;
        public float AppliedAngularVelocity => _lastAppliedAngularVelocity;

        public VelocityDrivenRotationService(
            IRotateChannelHub rotateHub,
            IObjectResolver resolver,
            VelocityRotationSettings settings)
        {
            _rotateHub = rotateHub;
            _resolver = resolver;
            _settings = settings;

            _resolver.TryResolve(out IBaseScalarService? scalar);
            _scalarService = scalar;
            _resolver.TryResolve(out IMaterialFxServiceFactory? materialFxFactory);
            _materialFxFactory = materialFxFactory;
        }

        public void ApplySettings(in VelocityRotationSettings settings)
        {
            _settings = settings;
            ResolveSources();
            RefreshFlipTarget();
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (_disposed)
                return;

            _scopeTransform = scope?.Identity?.SelfTransform;
            EnsureChannel();
            ResolveSources();
            RefreshFlipTarget();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            if (_disposed)
                return;

            _currentAngularVelocity = 0f;
            if (_channel != null)
                _channel.AngularVelocity = 0f;
            DisableFlip();
            DisposeFlip();
            ResetRuntimeDebugValues();
        }

        public void Tick()
        {
            if (_disposed)
                return;

            EnsureChannel();
            if (_channel == null)
            {
                ResetRuntimeDebugValues();
                return;
            }

            if (!_settings.Enabled)
            {
                _channel.AngularVelocity = 0f;
                _currentAngularVelocity = 0f;
                DisableFlip();
                ResetRuntimeDebugValues();
                return;
            }

            var rawVelocity = ResolveVelocity();
            _lastRawVelocity = rawVelocity;
            var velocity = rawVelocity;
            if (velocity.sqrMagnitude <= 0.0001f)
            {
                _channel.AngularVelocity = 0f;
                _currentAngularVelocity = 0f;
                _lastScaledVelocity = Vector2.zero;
                _lastSpeedScale = ResolveSpeedScale();
                _lastTargetAngularVelocity = 0f;
                _lastAppliedAngularVelocity = 0f;
                _lastDirectionAngle = 0f;
                _lastTargetAngle = 0f;
                _lastDeltaAngle = 0f;
                return;
            }

            var speedScale = ResolveSpeedScale();
            _lastSpeedScale = speedScale;
            velocity *= speedScale;
            _lastScaledVelocity = velocity;
            _lastDirectionAngle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;

            if (_settings.Mode != VelocityRotationMode.Facing || !_settings.Facing.FlipWhenNegativeX)
            {
                DisableFlip();
            }

            switch (_settings.Mode)
            {
                case VelocityRotationMode.Spin:
                    ApplySpin(velocity, Time.deltaTime);
                    break;

                case VelocityRotationMode.Facing:
                    ApplyFacing(velocity, Time.deltaTime);
                    break;

                case VelocityRotationMode.Tilt:
                default:
                    ApplyTilt(velocity, Time.deltaTime);
                    break;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _channel = null;
            DisableFlip();
            DisposeFlip();
            _sourceTransform = null;
            _sourceRigidbody = null;
            _sourceTransformChannel = null;
        }

        void EnsureChannel()
        {
            if (_channel != null)
                return;

            var key = string.IsNullOrEmpty(_settings.RotateChannelKey) ? "velocity" : _settings.RotateChannelKey;
            if (_rotateHub.TryGetChannel(key, out var handle))
            {
                _channel = handle;
                return;
            }

            var def = new RotateChannelDef
            {
                Tag = key,
                Priority = 0,
                BlendOp = RotateBlendOp.Add,
                Influence = 1f,
                EnabledByDefault = true,
            };
            _channel = _rotateHub.RegisterChannel(key, def);
        }

        void ResolveSources()
        {
            _sourceTransform = _settings.SourceTransform;
            if (_sourceTransform == null)
                _sourceTransform = _scopeTransform;

            if (_settings.Source == VelocityRotationSourceKind.TransformChannel)
            {
                _resolver.TryResolve<ITransformChannelPoseReader>(out var poseReader);
                _sourceTransformChannel = poseReader;
                if (_sourceTransformChannel == null && _sourceTransform != null)
                    _sourceTransformChannel = TryResolveTransformChannelPoseReader(_sourceTransform);
            }
            else
            {
                _sourceTransformChannel = null;
            }

            if (_settings.Source == VelocityRotationSourceKind.Rigidbody2D)
            {
                _sourceRigidbody = _settings.SourceRigidbody2D;
                if (_sourceRigidbody == null && _sourceTransform != null)
                    _sourceRigidbody = _sourceTransform.GetComponent<Rigidbody2D>();
                if (_sourceRigidbody == null)
                {
                    if (_scopeTransform != null)
                        _sourceRigidbody = _scopeTransform.GetComponent<Rigidbody2D>();
                }
            }
            else
            {
                _sourceRigidbody = null;
            }
        }

        Vector2 ResolveVelocity()
        {
            switch (_settings.Source)
            {
                case VelocityRotationSourceKind.Rigidbody2D:
                    return _sourceRigidbody != null ? _sourceRigidbody.linearVelocity : Vector2.zero;
                case VelocityRotationSourceKind.TransformChannel:
                default:
                    return _sourceTransformChannel != null ? _sourceTransformChannel.CurrentVelocity : Vector2.zero;
            }
        }

        float ResolveSpeedScale()
        {
            var scale = Mathf.Max(0f, _settings.SpeedScale);
            if (_settings.UseScalarSpeedScale && _scalarService != null)
            {
                var key = _settings.SpeedScaleScalar;
                if (!string.IsNullOrEmpty(key.Name) && _scalarService.GlobalTryGet(key, out var scalar))
                    scale = Mathf.Max(0f, scalar);
            }
            return scale;
        }

        void ApplySpin(Vector2 velocity, float deltaTime)
        {
            var settings = _settings.Spin;
            var axis = settings.DirectionAxis.sqrMagnitude > 0.0001f ? settings.DirectionAxis.normalized : Vector2.right;

            float signedSpeed = settings.UseSignedDirection
                ? Vector2.Dot(velocity, axis)
                : velocity.magnitude;

            var targetAngular = signedSpeed * settings.SpeedToAngular;
            if (settings.MaxAngularVelocity > 0f)
                targetAngular = Mathf.Clamp(targetAngular, -settings.MaxAngularVelocity, settings.MaxAngularVelocity);

            if (settings.LerpSpeed > 0f && deltaTime > 0f)
                _currentAngularVelocity = Mathf.MoveTowards(_currentAngularVelocity, targetAngular, settings.LerpSpeed * deltaTime);
            else
                _currentAngularVelocity = targetAngular;

            _lastTargetAngle = 0f;
            _lastDeltaAngle = 0f;
            _lastTargetAngularVelocity = targetAngular;
            _lastAppliedAngularVelocity = _currentAngularVelocity;
            _channel!.AngularVelocity = _currentAngularVelocity;
        }

        void ApplyFacing(Vector2 velocity, float deltaTime)
        {
            var settings = _settings.Facing;
            var direction = velocity;
            bool flipX = false;

            if (settings.FlipWhenNegativeX && direction.x < 0f)
            {
                flipX = true;
                direction.x = -direction.x;
            }

            UpdateFlipForFacing(settings, flipX);

            var directionAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            var targetAngle = settings.BaseAngle + directionAngle;

            var currentAngle = ResolveCurrentAngle();
            var delta = Mathf.DeltaAngle(currentAngle, targetAngle);

            float angularVelocity;
            if (!settings.DisableLerp)
            {
                angularVelocity = delta * settings.LerpSpeed;
                if (settings.MaxAngularVelocity > 0f)
                    angularVelocity = Mathf.Clamp(angularVelocity, -settings.MaxAngularVelocity, settings.MaxAngularVelocity);
            }
            else
            {
                // No Lerp mode: drive one-frame snap to target angle.
                angularVelocity = deltaTime > 0f ? (delta / deltaTime) : 0f;
            }

            _currentAngularVelocity = angularVelocity;
            _lastDirectionAngle = directionAngle;
            _lastTargetAngle = targetAngle;
            _lastDeltaAngle = delta;
            _lastTargetAngularVelocity = !settings.DisableLerp
                ? delta * settings.LerpSpeed
                : angularVelocity;
            _lastAppliedAngularVelocity = angularVelocity;
            _channel!.AngularVelocity = angularVelocity;
        }

        void ApplyTilt(Vector2 velocity, float deltaTime)
        {
            var settings = _settings.Tilt;
            var weighted = new Vector2(
                velocity.x * (velocity.x >= 0f ? settings.AxisWeightPos.x : settings.AxisWeightNeg.x),
                velocity.y * (velocity.y >= 0f ? settings.AxisWeightPos.y : settings.AxisWeightNeg.y));

            if (weighted.sqrMagnitude <= 0.0001f)
            {
                _channel!.AngularVelocity = 0f;
                _currentAngularVelocity = 0f;
                _lastDirectionAngle = 0f;
                _lastTargetAngle = 0f;
                _lastDeltaAngle = 0f;
                _lastTargetAngularVelocity = 0f;
                _lastAppliedAngularVelocity = 0f;
                return;
            }

            var directionAngle = Mathf.Atan2(weighted.y, weighted.x) * Mathf.Rad2Deg;
            if (directionAngle > 90f)
                directionAngle -= 180f;
            else if (directionAngle < -90f)
                directionAngle += 180f;

            var maxTiltAbs = Mathf.Abs(settings.MaxTiltAngle);
            if (settings.MaxTiltAngle < 0f)
                directionAngle = -directionAngle;

            if (settings.SpeedToTilt > 0f && maxTiltAbs > 0f)
            {
                var tiltAmount = Mathf.Clamp(weighted.magnitude * settings.SpeedToTilt, 0f, maxTiltAbs);
                directionAngle = Mathf.Clamp(directionAngle, -tiltAmount, tiltAmount);
            }

            float targetAngle = settings.ReferenceMode == TiltReferenceMode.Relative
                ? settings.BaseEuler.z + directionAngle
                : directionAngle;

            var currentAngle = ResolveCurrentAngle();
            var delta = Mathf.DeltaAngle(currentAngle, targetAngle);

            var speed = settings.LerpSpeed;
            var boost = 1f;
            if (settings.ErrorBoost > 0f)
            {
                var capped = settings.ErrorBoostMax > 0f ? Mathf.Min(settings.ErrorBoostMax, Mathf.Abs(delta)) : Mathf.Abs(delta);
                boost += capped * settings.ErrorBoost;
            }

            var angularVelocity = delta * speed * boost;
            if (settings.MaxAngularVelocity > 0f)
                angularVelocity = Mathf.Clamp(angularVelocity, -settings.MaxAngularVelocity, settings.MaxAngularVelocity);

            _currentAngularVelocity = angularVelocity;
            _lastDirectionAngle = directionAngle;
            _lastTargetAngle = targetAngle;
            _lastDeltaAngle = delta;
            _lastTargetAngularVelocity = delta * speed * boost;
            _lastAppliedAngularVelocity = angularVelocity;
            _channel!.AngularVelocity = angularVelocity;
        }

        float ResolveCurrentAngle()
        {
            var t = _sourceTransform;
            if (t == null)
            {
                t = _scopeTransform;
            }
            return t != null ? t.eulerAngles.z : 0f;
        }

        void UpdateFlipForFacing(in FacingSettings settings, bool flipX)
        {
            if (!settings.FlipWhenNegativeX)
            {
                DisableFlip();
                return;
            }

            if (!EnsureFlipController(settings))
                return;

            if (!_flipActive || _lastFlipX != flipX)
            {
                var duration = settings.FlipDuration > 0f ? settings.FlipDuration : 0f;
                _flipController!.SetTarget(flipX, duration, Ease.OutQuad);
                _flipActive = true;
                _lastFlipX = flipX;
            }
        }

        bool EnsureFlipController(in FacingSettings settings)
        {
            if (_materialFxFactory == null)
            {
                _resolver.TryResolve(out _materialFxFactory);
            }

            if (_materialFxFactory == null)
                return false;

            var target = settings.FlipTarget != null
                ? settings.FlipTarget
                : (_sourceTransform != null ? _sourceTransform : _scopeTransform);

            if (target == null)
                return false;

            var instanceId = target.GetInstanceID();
            if (_flipController != null && _flipMaterialFx != null && _flipTargetInstanceId == instanceId)
                return true;

            DisposeFlip();

            var spriteRenderer = target.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                _flipMaterialFx = _materialFxFactory.CreateForSpriteRenderer(spriteRenderer);
            }
            else
            {
                var graphic = target.GetComponent<Graphic>();
                if (graphic != null)
                {
                    _flipMaterialFx = _materialFxFactory.CreateForGraphic(graphic);
                }
            }

            if (_flipMaterialFx == null)
                return false;

            _flipTargetInstanceId = instanceId;
            _flipContext = "VelocityDrivenRotation.FlipX." + RuntimeHelpers.GetHashCode(this);
            _flipController = new FlipController(_flipMaterialFx, _flipContext, FlipLayerPriority);
            _flipActive = false;
            _lastFlipX = false;
            return true;
        }

        void RefreshFlipTarget()
        {
            if (_settings.Mode != VelocityRotationMode.Facing || !_settings.Facing.FlipWhenNegativeX)
            {
                DisableFlip();
                DisposeFlip();
                return;
            }

            EnsureFlipController(_settings.Facing);
        }

        void DisableFlip()
        {
            if (_flipController != null && _flipActive)
            {
                _flipController.StopAndSnap(disableLayer: true);
            }
            _flipActive = false;
            _lastFlipX = false;
        }

        void DisposeFlip()
        {
            if (_flipController != null)
            {
                _flipController.StopAndSnap(disableLayer: true);
                _flipController = null;
            }

            if (_flipMaterialFx != null)
            {
                _flipMaterialFx.Dispose();
                _flipMaterialFx = null;
            }

            _flipTargetInstanceId = 0;
            _flipContext = string.Empty;
            _flipActive = false;
            _lastFlipX = false;
        }

        static ITransformChannelPoseReader? TryResolveTransformChannelPoseReader(Transform transform)
        {
            for (var current = transform; current != null; current = current.parent)
            {
                var scope = current.GetComponent<BaseLifetimeScope>();
                if (scope?.Resolver == null)
                    continue;

                if (scope.Resolver.TryResolve<ITransformChannelPoseReader>(out var poseReader) && poseReader != null)
                    return poseReader;
            }

            return null;
        }

        void ResetRuntimeDebugValues()
        {
            _lastRawVelocity = Vector2.zero;
            _lastScaledVelocity = Vector2.zero;
            _lastSpeedScale = Mathf.Max(0f, _settings.SpeedScale);
            _lastDirectionAngle = 0f;
            _lastTargetAngle = 0f;
            _lastDeltaAngle = 0f;
            _lastTargetAngularVelocity = 0f;
            _lastAppliedAngularVelocity = 0f;
        }
    }
}

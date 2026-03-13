#nullable enable
using Game.Commands.VNext;
using Game.TransformSystem;
using UnityEngine;
using VContainer;

namespace Game.Channel
{
    public readonly struct ParallaxChannelTelemetrySnapshot
    {
        public readonly string Tag;
        public readonly bool Enabled;
        public readonly ParallaxDriverMode DriverMode;
        public readonly ParallaxCameraBindMode CameraBindMode;
        public readonly ParallaxWriteMode WriteMode;
        public readonly string TargetName;
        public readonly string CameraName;
        public readonly Vector3 LastOffset;
        public readonly Vector3 BaseWorldPosition;
        public readonly Vector3 Factor;
        public readonly Vector3 ExtraOffset;
        public readonly bool UseSmoothing;
        public readonly float SmoothTime;
        public readonly int UpdateEveryNFrames;
        public readonly bool AllowUnsafeRigidbody2DWrite;
        public readonly int LastTickFrame;
        public readonly bool LastWriteApplied;
        public readonly Vector3 LastAppliedWorldPosition;

        public ParallaxChannelTelemetrySnapshot(
            string tag,
            bool enabled,
            ParallaxDriverMode driverMode,
            ParallaxCameraBindMode cameraBindMode,
            ParallaxWriteMode writeMode,
            string targetName,
            string cameraName,
            Vector3 lastOffset,
            Vector3 baseWorldPosition,
            Vector3 factor,
            Vector3 extraOffset,
            bool useSmoothing,
            float smoothTime,
            int updateEveryNFrames,
            bool allowUnsafeRigidbody2DWrite,
            int lastTickFrame,
            bool lastWriteApplied,
            Vector3 lastAppliedWorldPosition)
        {
            Tag = tag;
            Enabled = enabled;
            DriverMode = driverMode;
            CameraBindMode = cameraBindMode;
            WriteMode = writeMode;
            TargetName = targetName;
            CameraName = cameraName;
            LastOffset = lastOffset;
            BaseWorldPosition = baseWorldPosition;
            Factor = factor;
            ExtraOffset = extraOffset;
            UseSmoothing = useSmoothing;
            SmoothTime = smoothTime;
            UpdateEveryNFrames = updateEveryNFrames;
            AllowUnsafeRigidbody2DWrite = allowUnsafeRigidbody2DWrite;
            LastTickFrame = lastTickFrame;
            LastWriteApplied = lastWriteApplied;
            LastAppliedWorldPosition = lastAppliedWorldPosition;
        }
    }

    public interface IParallaxChannelTelemetry
    {
        ParallaxChannelTelemetrySnapshot GetTelemetrySnapshot();
    }

    public interface IParallaxChannelPlayer
    {
        string Tag { get; }
        bool Enabled { get; }
        void SetEnabled(bool enabled);
        void ToggleEnabled();
        void SetWriteMode(ParallaxWriteMode mode);
        void SetFactor(Vector3 factor);
        void SetExtraOffset(Vector3 offset);
        void SetAffectAxes(bool affectX, bool affectY, bool affectZ);
        void SetSmoothing(bool enabled, float smoothTime);
        void SetMaxOffsetMagnitude(float maxMagnitude);
        void SetUpdateEveryNFrames(int value);
        void SetAllowUnsafeRigidbody2DWrite(bool allow);
        void SetDriverMode(ParallaxDriverMode mode);
        void SetCameraBindMode(ParallaxCameraBindMode mode);
        void SetDirectTarget(Transform? target);
        void SetAnimationChannelTag(string tag);
        void ResetCameraOrigin();
        void ResetRuntimeOverrides();
        void OnAcquire();
        void OnRelease();
        void Tick(float deltaTime, int frameCount);
    }

    public sealed class ParallaxChannelPlayer : IParallaxChannelPlayer, IParallaxChannelTelemetry
    {
        const float ExternalMoveRebaseEpsilon = 0.0001f;
        const float UnlimitedOffsetSafetyMaxMagnitude = 30f;
        const float UnlimitedCameraDeltaRecenteringDistance = 60f;

        readonly ParallaxChannelDef _def;
        readonly IScopeNode _ownerScope;

        ActorSourceResolveCache _animationActorCache;
        ActorSourceResolveCache _controllerActorCache;
        ActorSourceResolveCache _cameraActorCache;

        bool _enabled;
        ParallaxDriverMode _driverMode;
        Transform? _directTarget;
        ActorSource _animationHubActorSource;
        string _transformAnimationChannelTag = "default";
        ActorSource _controllerActorSource;
        ParallaxCameraBindMode _cameraBindMode;
        Transform? _specificCameraTransform;
        ActorSource _cameraActorSource;
        ParallaxWriteMode _writeMode;
        bool _allowUnsafeRigidbody2DWrite;
        ParallaxParams _parameters;
        int _updateEveryNFrames;

        Transform? _currentTarget;
        Transform? _currentCamera;
        Vector3 _baseWorldPosition;
        Vector3 _baseLocalPosition;
        Vector3 _currentOffset;
        Vector3 _offsetVelocity;
        Vector3 _cameraOrigin;
        bool _hasCameraOrigin;
        bool _loggedMissingTarget;
        bool _loggedMissingCamera;
        int _lastTickFrame;
        bool _lastWriteApplied;
        Vector3 _lastAppliedWorldPosition;
        bool _hasLastAppliedWorldPosition;

        public string Tag => _def.Tag;
        public bool Enabled => _enabled;

        public ParallaxChannelPlayer(ParallaxChannelDef def, IScopeNode ownerScope)
        {
            _def = def;
            _ownerScope = ownerScope;
            _enabled = def != null && def.EnabledOnAcquire;
            ApplyDefinitionDefaults();
        }

        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            if (!enabled)
            {
                _offsetVelocity = Vector3.zero;
                _currentOffset = Vector3.zero;
            }
        }

        public void ToggleEnabled() => SetEnabled(!_enabled);
        public void SetWriteMode(ParallaxWriteMode mode) => _writeMode = mode;
        public void SetFactor(Vector3 factor) => _parameters.Factor = factor;
        public void SetExtraOffset(Vector3 offset) => _parameters.ExtraOffset = offset;
        public void SetAffectAxes(bool affectX, bool affectY, bool affectZ)
        {
            _parameters.AffectX = affectX;
            _parameters.AffectY = affectY;
            _parameters.AffectZ = affectZ;
        }
        public void SetSmoothing(bool enabled, float smoothTime)
        {
            _parameters.UseSmoothing = enabled;
            _parameters.SmoothTime = Mathf.Max(0f, smoothTime);
        }
        public void SetMaxOffsetMagnitude(float maxMagnitude) => _parameters.MaxOffsetMagnitude = Mathf.Max(0f, maxMagnitude);
        public void SetUpdateEveryNFrames(int value) => _updateEveryNFrames = Mathf.Max(1, value);
        public void SetAllowUnsafeRigidbody2DWrite(bool allow) => _allowUnsafeRigidbody2DWrite = allow;
        public void SetDriverMode(ParallaxDriverMode mode)
        {
            _driverMode = mode;
            _currentTarget = null;
        }
        public void SetCameraBindMode(ParallaxCameraBindMode mode)
        {
            _cameraBindMode = mode;
            _currentCamera = null;
            _hasCameraOrigin = false;
        }
        public void SetDirectTarget(Transform? target)
        {
            _directTarget = target;
            if (_driverMode == ParallaxDriverMode.DirectObject)
                _currentTarget = null;
        }
        public void SetAnimationChannelTag(string tag)
        {
            _transformAnimationChannelTag = string.IsNullOrWhiteSpace(tag) ? "default" : tag.Trim();
            if (_driverMode == ParallaxDriverMode.TransformAnimationChannel)
                _currentTarget = null;
        }
        public void ResetCameraOrigin()
        {
            if (_currentCamera != null)
            {
                _cameraOrigin = _currentCamera.position;
                _hasCameraOrigin = true;
            }
            else
            {
                _hasCameraOrigin = false;
            }
        }

        public void ResetRuntimeOverrides()
        {
            ApplyDefinitionDefaults();
            _currentTarget = null;
            _currentCamera = null;
            _hasCameraOrigin = false;
            _currentOffset = Vector3.zero;
            _offsetVelocity = Vector3.zero;
        }

        public ParallaxChannelTelemetrySnapshot GetTelemetrySnapshot()
        {
            var targetName = _currentTarget != null ? _currentTarget.name : BuildTargetHintName();
            var cameraName = _currentCamera != null ? _currentCamera.name : BuildCameraHintName();

            return new ParallaxChannelTelemetrySnapshot(
                tag: Tag,
                enabled: _enabled,
                driverMode: _driverMode,
                cameraBindMode: _cameraBindMode,
                writeMode: _writeMode,
                targetName: targetName,
                cameraName: cameraName,
                lastOffset: _currentOffset,
                baseWorldPosition: _baseWorldPosition,
                factor: _parameters.Factor,
                extraOffset: _parameters.ExtraOffset,
                useSmoothing: _parameters.UseSmoothing,
                smoothTime: _parameters.SmoothTime,
                updateEveryNFrames: _updateEveryNFrames,
                allowUnsafeRigidbody2DWrite: _allowUnsafeRigidbody2DWrite,
                lastTickFrame: _lastTickFrame,
                lastWriteApplied: _lastWriteApplied,
                lastAppliedWorldPosition: _lastAppliedWorldPosition);
        }

        public void OnAcquire()
        {
            ApplyDefinitionDefaults();
            _currentTarget = null;
            _currentCamera = null;
            _baseWorldPosition = Vector3.zero;
            _baseLocalPosition = Vector3.zero;
            _currentOffset = Vector3.zero;
            _offsetVelocity = Vector3.zero;
            _cameraOrigin = Vector3.zero;
            _hasCameraOrigin = false;
            _loggedMissingTarget = false;
            _loggedMissingCamera = false;
            _animationActorCache = default;
            _controllerActorCache = default;
            _cameraActorCache = default;
            _lastTickFrame = -1;
            _lastWriteApplied = false;
            _lastAppliedWorldPosition = Vector3.zero;
            _hasLastAppliedWorldPosition = false;
        }

        public void OnRelease()
        {
            _enabled = false;
            _currentTarget = null;
            _currentCamera = null;
            _offsetVelocity = Vector3.zero;
            _currentOffset = Vector3.zero;
            _hasCameraOrigin = false;
            _lastTickFrame = -1;
            _lastWriteApplied = false;
            _lastAppliedWorldPosition = Vector3.zero;
            _hasLastAppliedWorldPosition = false;
        }

        public void Tick(float deltaTime, int frameCount)
        {
            _lastTickFrame = frameCount;
            _lastWriteApplied = false;

            if (!_enabled)
                return;

            if (_updateEveryNFrames > 1 && frameCount % _updateEveryNFrames != 0)
                return;

            if (!TryResolveTarget(out var target) || target == null)
            {
                if (!_loggedMissingTarget)
                {
                    Debug.LogError($"[ParallaxChannel] Target resolve failed. Tag={Tag}, Driver={_driverMode}");
                    _loggedMissingTarget = true;
                }
                return;
            }

            var previousTarget = _currentTarget;
            if (!ReferenceEquals(_currentTarget, target))
                _currentTarget = target;

            if (!TryResolveCamera(out var cameraTransform) || cameraTransform == null)
            {
                if (!_loggedMissingCamera)
                {
                    Debug.LogError($"[ParallaxChannel] Camera resolve failed. Tag={Tag}, CameraBind={_cameraBindMode}");
                    _loggedMissingCamera = true;
                }
                return;
            }

            _loggedMissingTarget = false;
            _loggedMissingCamera = false;

            var targetChanged = !ReferenceEquals(previousTarget, target);
            var cameraChanged = !ReferenceEquals(_currentCamera, cameraTransform);

            if (targetChanged)
            {
                _currentTarget = target;
                _baseWorldPosition = target.position;
                _baseLocalPosition = target.localPosition;
                _offsetVelocity = Vector3.zero;
                _currentOffset = Vector3.zero;
            }

            if (cameraChanged || !_hasCameraOrigin)
            {
                _currentCamera = cameraTransform;
                _cameraOrigin = cameraTransform.position;
                _hasCameraOrigin = true;
            }

            TryRebaseBasePositionIfExternalMoved(target);

            var cameraDelta = ResolveCameraDelta(cameraTransform.position);

            var rawOffset = BuildRawOffset(cameraDelta);
            if (_parameters.UseSmoothing && _parameters.SmoothTime > 0f)
            {
                _currentOffset = Vector3.SmoothDamp(_currentOffset, rawOffset, ref _offsetVelocity, _parameters.SmoothTime, Mathf.Infinity, Mathf.Max(0.0001f, deltaTime));
            }
            else
            {
                _currentOffset = rawOffset;
                _offsetVelocity = Vector3.zero;
            }

            ApplyOffset(target, _currentOffset);
            _lastWriteApplied = true;
            _lastAppliedWorldPosition = target.position;
            _hasLastAppliedWorldPosition = true;
        }

        Vector3 BuildRawOffset(Vector3 cameraDelta)
        {
            var factor = _parameters.Factor;
            var offset = new Vector3(
                cameraDelta.x * factor.x,
                cameraDelta.y * factor.y,
                cameraDelta.z * factor.z);

            offset += _parameters.ExtraOffset;

            if (!_parameters.AffectX)
                offset.x = 0f;
            if (!_parameters.AffectY)
                offset.y = 0f;
            if (!_parameters.AffectZ)
                offset.z = 0f;

            var maxMag = _parameters.MaxOffsetMagnitude;
            if (maxMag > 0f && offset.sqrMagnitude > maxMag * maxMag)
                offset = offset.normalized * maxMag;

            if (maxMag <= 0f && offset.sqrMagnitude > UnlimitedOffsetSafetyMaxMagnitude * UnlimitedOffsetSafetyMaxMagnitude)
                offset = offset.normalized * UnlimitedOffsetSafetyMaxMagnitude;

            return offset;
        }

        void TryRebaseBasePositionIfExternalMoved(Transform target)
        {
            if (!_hasLastAppliedWorldPosition)
                return;

            var currentWorld = target.position;
            var deltaWorld = currentWorld - _lastAppliedWorldPosition;
            if (deltaWorld.sqrMagnitude <= ExternalMoveRebaseEpsilon * ExternalMoveRebaseEpsilon)
                return;

            _baseWorldPosition += deltaWorld;

            var parent = target.parent;
            if (parent != null)
            {
                var deltaLocal = parent.InverseTransformVector(deltaWorld);
                _baseLocalPosition += deltaLocal;
            }
            else
            {
                _baseLocalPosition += deltaWorld;
            }
        }

        bool ShouldRecenterCameraOrigin(Vector3 cameraDelta)
        {
            if (_parameters.MaxOffsetMagnitude > 0f)
                return false;

            var threshold = UnlimitedCameraDeltaRecenteringDistance;
            return cameraDelta.sqrMagnitude > threshold * threshold;
        }

        Vector3 ResolveCameraDelta(Vector3 cameraPosition)
        {
            if (_writeMode == ParallaxWriteMode.AdditiveLocal || _writeMode == ParallaxWriteMode.AdditiveWorld)
            {
                return cameraPosition - _baseWorldPosition;
            }

            var delta = cameraPosition - _cameraOrigin;
            if (!ShouldRecenterCameraOrigin(delta))
                return delta;

            _cameraOrigin = cameraPosition;
            return Vector3.zero;
        }

        void ApplyOffset(Transform target, Vector3 rawOffset)
        {
            var localOffset = ResolveLocalOffset(target, rawOffset);
            var worldOffset = ResolveWorldOffset(target, rawOffset);

            if (TryApplyOffsetViaRigidbody2D(target, localOffset, worldOffset))
                return;

            switch (_writeMode)
            {
                case ParallaxWriteMode.AdditiveLocal:
                    target.localPosition = _baseLocalPosition + localOffset;
                    break;
                case ParallaxWriteMode.AdditiveWorld:
                    target.position = _baseWorldPosition + worldOffset;
                    break;
                case ParallaxWriteMode.OverrideLocal:
                    target.localPosition = localOffset;
                    break;
                case ParallaxWriteMode.OverrideWorld:
                    target.position = worldOffset;
                    break;
            }
        }

        bool TryApplyOffsetViaRigidbody2D(Transform target, Vector3 localOffset, Vector3 worldOffset)
        {
            var rb = target.GetComponent<Rigidbody2D>();
            if (rb == null)
                return false;

            if (_allowUnsafeRigidbody2DWrite)
                return false;

            var worldTarget = ComputeWorldTargetPositionForRigidbody(target, localOffset, worldOffset);
            rb.MovePosition(new Vector2(worldTarget.x, worldTarget.y));
            return true;
        }

        Vector3 ComputeWorldTargetPositionForRigidbody(Transform target, Vector3 localOffset, Vector3 worldOffset)
        {
            switch (_writeMode)
            {
                case ParallaxWriteMode.AdditiveLocal:
                    {
                        var localTarget = _baseLocalPosition + localOffset;
                        var parent = target.parent;
                        return parent != null ? parent.TransformPoint(localTarget) : localTarget;
                    }

                case ParallaxWriteMode.AdditiveWorld:
                    return _baseWorldPosition + worldOffset;

                case ParallaxWriteMode.OverrideLocal:
                    {
                        var parent = target.parent;
                        return parent != null ? parent.TransformPoint(localOffset) : localOffset;
                    }

                case ParallaxWriteMode.OverrideWorld:
                    return worldOffset;
            }

            return target.position;
        }

        Vector3 ResolveLocalOffset(Transform target, Vector3 rawOffset)
        {
            if (_parameters.UseLocalSpace)
                return rawOffset;

            var parent = target.parent;
            return parent != null ? parent.InverseTransformVector(rawOffset) : rawOffset;
        }

        Vector3 ResolveWorldOffset(Transform target, Vector3 rawOffset)
        {
            if (!_parameters.UseLocalSpace)
                return rawOffset;

            var parent = target.parent;
            return parent != null ? parent.TransformVector(rawOffset) : rawOffset;
        }

        bool TryResolveTarget(out Transform? target)
        {
            target = null;

            switch (_driverMode)
            {
                case ParallaxDriverMode.DirectObject:
                    target = _directTarget;
                    return target != null;

                case ParallaxDriverMode.TransformAnimationChannel:
                    return TryResolveTargetFromAnimationHub(out target);

                case ParallaxDriverMode.TransformController:
                    return TryResolveTargetFromTransformController(out target);
            }

            return false;
        }

        bool TryResolveTargetFromAnimationHub(out Transform? target)
        {
            target = null;
            var scope = ActorSourceFastResolver.ResolveCached(_ownerScope, _animationHubActorSource, ref _animationActorCache);
            if (scope?.Resolver == null)
                return false;

            if (!scope.Resolver.TryResolve<ITransformAnimationHubService>(out var hub) || hub == null)
                return false;

            if (!hub.TryGetPlayer(_transformAnimationChannelTag, out var player) || player == null)
                return false;

            target = player.TargetTransform;
            return target != null;
        }

        bool TryResolveTargetFromTransformController(out Transform? target)
        {
            target = null;

            var scope = ActorSourceFastResolver.ResolveCached(_ownerScope, _controllerActorSource, ref _controllerActorCache);
            if (scope?.Resolver == null)
                return false;

            if (!scope.Resolver.TryResolve<ITransformControllerPoseReader>(out var reader) || reader == null)
                return false;

            target = reader.TargetTransform;
            return target != null;
        }

        bool TryResolveCamera(out Transform? cameraTransform)
        {
            cameraTransform = null;
            switch (_cameraBindMode)
            {
                case ParallaxCameraBindMode.MainCamera:
                    var main = Camera.main;
                    if (main != null)
                    {
                        cameraTransform = main.transform;
                        return true;
                    }

                    var all = Camera.allCameras;
                    for (int i = 0; i < all.Length; i++)
                    {
                        var camera = all[i];
                        if (camera == null)
                            continue;

                        cameraTransform = camera.transform;
                        break;
                    }
                    return cameraTransform != null;

                case ParallaxCameraBindMode.SpecificTransform:
                    cameraTransform = _specificCameraTransform;
                    return cameraTransform != null;

                case ParallaxCameraBindMode.ActorSource:
                    var scope = ActorSourceFastResolver.ResolveCached(_ownerScope, _cameraActorSource, ref _cameraActorCache);
                    cameraTransform = scope?.Identity?.SelfTransform;
                    return cameraTransform != null;
            }

            return false;
        }

        string BuildTargetHintName()
        {
            switch (_driverMode)
            {
                case ParallaxDriverMode.DirectObject:
                    return _directTarget != null ? _directTarget.name : "(Direct Target: null)";

                case ParallaxDriverMode.TransformAnimationChannel:
                    return $"(Anim:{_transformAnimationChannelTag})";

                case ParallaxDriverMode.TransformController:
                    return "(TransformController)";
            }

            return "(null)";
        }

        string BuildCameraHintName()
        {
            switch (_cameraBindMode)
            {
                case ParallaxCameraBindMode.MainCamera:
                    var main = Camera.main;
                    if (main != null)
                        return main.name;

                    var all = Camera.allCameras;
                    for (int i = 0; i < all.Length; i++)
                    {
                        var camera = all[i];
                        if (camera != null)
                            return camera.name;
                    }

                    return "(MainCamera unresolved)";

                case ParallaxCameraBindMode.SpecificTransform:
                    return _specificCameraTransform != null ? _specificCameraTransform.name : "(Specific Camera: null)";

                case ParallaxCameraBindMode.ActorSource:
                    return "(ActorSource Camera)";
            }

            return "(null)";
        }

        void ApplyDefinitionDefaults()
        {
            _enabled = _def.EnabledOnAcquire;
            _driverMode = _def.DriverMode;
            _directTarget = _def.DirectTarget;
            _animationHubActorSource = _def.AnimationHubActorSource;
            _transformAnimationChannelTag = string.IsNullOrWhiteSpace(_def.TransformAnimationChannelTag)
                ? "default"
                : _def.TransformAnimationChannelTag.Trim();
            _controllerActorSource = _def.ControllerActorSource;
            _cameraBindMode = _def.CameraBindMode;
            _specificCameraTransform = _def.SpecificCameraTransform;
            _cameraActorSource = _def.CameraActorSource;
            _writeMode = _def.WriteMode;
            _allowUnsafeRigidbody2DWrite = _def.AllowUnsafeRigidbody2DWrite;
            _parameters = _def.Parameters;
            EnsureRuntimeParameterIntegrity();
            _updateEveryNFrames = Mathf.Max(1, _def.UpdateEveryNFrames);
        }

        void EnsureRuntimeParameterIntegrity()
        {
            if (_parameters.AffectX || _parameters.AffectY || _parameters.AffectZ)
                return;

            _parameters.AffectX = true;
            _parameters.AffectY = true;
            _parameters.AffectZ = false;
        }
    }
}

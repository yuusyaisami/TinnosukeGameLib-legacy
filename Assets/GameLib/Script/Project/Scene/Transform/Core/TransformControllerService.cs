#nullable enable
using System;
using Game.ActionBlock;
using Game.ActionBlock.Keys;
using Game;
using Game.BuildConsole;
using Game.Common;
using Game.Movement;
using Game.Rotation;
using Game.Times;
using Game.Vars.Generated;
using Unity.Mathematics;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Game.Input;

namespace Game.TransformSystem
{
    public sealed class TransformControllerService : ITickable, IScopeAcquireHandler, IScopeReleaseHandler, IDisposable, ITransformTeleportService, ITransformAnimationOutputSink, ITransformControllerTelemetry, ITransformControllerPoseReader
    {
        readonly TransformControllerConfig _config;
        readonly Transform _ownerTransform;
        readonly IObjectResolver _resolver;

        IMovementChannelHub? _movementHub;
        IRotateChannelHub? _rotateHub;
        IBulkTransformManager? _bulkManager;

        IMovementAdapter? _movementAdapter;
        IRotationAdapter? _rotationAdapter;

        TransformHandle _bulkHandle = TransformHandle.Invalid;
        bool _useBulkTransform;
        bool _acquired;
        bool _disposed;
        TimeScaleBehavior _timeScaleBehavior = TimeScaleBehavior.Scaled;
        bool _positionOverriddenInUpdate;
        bool _rotationOverriddenInUpdate;
        bool _loggedMovementBlocked;
        bool _loggedMissingMovementAdapter;
        bool _loggedMissingRigidbody;
        bool _loggedRigidbodySuppressed;

        Transform? _currentTarget;
        Transform? _registeredTarget;
        RectTransform? _cachedRectTransform;
        Rigidbody2D? _cachedRigidbody2D;
        CharacterController? _cachedCharacterController;

        readonly TransformAnimationOutput _animationOutput = new();
        ITransformAnimationOutputRegistry? _animationOutputRegistry;
        IBlackboardService? _blackboard;
        IActionBlockService? _actionBlockService;
        IScopeNode? _scopeNode;
        bool _forceZeroVelocityWhenMovementBlocked = true;

        public Transform TargetTransform => _registeredTarget != null ? _registeredTarget : _ownerTransform;
        public TransformAnimationOutput AnimationOutput => _animationOutput;
        public Vector2 CurrentVelocity => _movementHub?.Output?.Value ?? Vector2.zero;
        public float CurrentAngularVelocity => _rotateHub?.Output?.Value ?? 0f;
        public string TargetName => TargetTransform != null ? TargetTransform.name : "(none)";
        public TransformOutputTarget OutputTarget => _config.OutputTarget;
        public bool MovementEnabled => _config.EnableMovement;
        public bool RotationEnabled => _config.EnableRotation;
        public bool HasRotationAdapter => _rotationAdapter != null;
        public bool Rigidbody2DFreezeRotation
        {
            get
            {
                var rb = ResolveTelemetryRigidbody2D();
                return rb != null && (rb.constraints & RigidbodyConstraints2D.FreezeRotation) != 0;
            }
        }
        public float Rigidbody2DAngularVelocity
        {
            get
            {
                var rb = ResolveTelemetryRigidbody2D();
                return rb != null ? rb.angularVelocity : 0f;
            }
        }
        public float Rigidbody2DRotation
        {
            get
            {
                var rb = ResolveTelemetryRigidbody2D();
                return rb != null ? rb.rotation : 0f;
            }
        }

        public TransformControllerService(
            TransformControllerConfig config,
            Transform ownerTransform,
            IObjectResolver resolver)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _ownerTransform = ownerTransform != null ? ownerTransform : throw new ArgumentNullException(nameof(ownerTransform));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

            resolver.TryResolve(out _movementHub);
            resolver.TryResolve(out _rotateHub);
            resolver.TryResolve(out _bulkManager);
            resolver.TryResolve(out _animationOutputRegistry);
            resolver.TryResolve(out _blackboard);
            resolver.TryResolve(out _actionBlockService);
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (_disposed || _acquired)
                return;

            _acquired = true;
            _scopeNode = scope;
            _timeScaleBehavior = scope?.Identity?.TimeScaleBehavior ?? TimeScaleBehavior.Scaled;
            _loggedMovementBlocked = false;
            _loggedMissingMovementAdapter = false;
            _loggedMissingRigidbody = false;
            _loggedRigidbodySuppressed = false;

            // Re-resolve optional dependencies on acquire.
            // In pooled/runtime scopes, construction can happen before some parent services are ready.
            _resolver.TryResolve(out _movementHub);
            _resolver.TryResolve(out _rotateHub);
            _resolver.TryResolve(out _bulkManager);
            _resolver.TryResolve(out _animationOutputRegistry);
            _resolver.TryResolve(out _blackboard);
            _resolver.TryResolve(out _actionBlockService);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
#endif

            SetupAdapters();
            LogAcquireState();
            WriteBlackboardSnapshot(active: true);

            if (_config.EnableRotation && _config.OutputTarget == TransformOutputTarget.Rigidbody2D)
            {
                var rb = _cachedRigidbody2D ?? _config.TargetRigidbody2D;
                if (rb != null && (rb.constraints & RigidbodyConstraints2D.FreezeRotation) != 0)
                {
                    Debug.LogWarning("[TransformControllerService] Rigidbody2D FreezeRotation is enabled. Rotation output may not be visible.", rb);
                }
            }
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            if (_disposed || !_acquired)
                return;

            _acquired = false;
            _scopeNode = null;
            WriteBlackboardSnapshot(active: false);
            TeardownAdapters();
        }

        public void Tick()
        {
            if (_disposed || !_acquired)
                return;

            float deltaTime;
            if (_timeScaleBehavior == TimeScaleBehavior.Unscaled)
            {
                deltaTime = _config.OutputTarget == TransformOutputTarget.Rigidbody2D
                    ? Time.fixedUnscaledDeltaTime
                    : Time.unscaledDeltaTime;
            }
            else
            {
                deltaTime = _config.OutputTarget == TransformOutputTarget.Rigidbody2D
                    ? Time.fixedDeltaTime
                    : Time.deltaTime;
            }

            var target = _currentTarget != null
                ? _currentTarget
                : (_config.TargetTransform != null ? _config.TargetTransform : _ownerTransform);

            bool positionOverridden = false;
            bool rotationOverridden = false;
            if (target != null)
            {
                positionOverridden = ApplyAnimationPosition(target);
                rotationOverridden = ApplyAnimationRotation(target);
                ApplyAnimationScale(target);
                ApplyAnimationRectExtras(target);
            }

            _positionOverriddenInUpdate = positionOverridden;
            _rotationOverriddenInUpdate = rotationOverridden;

            if (_config.EnableMovement)
            {
                if (IsMovementForceBlockedByActionBlock())
                {
                    if (!_loggedMovementBlocked)
                    {
                        _loggedMovementBlocked = true;
                        BuildConsoleLog.Scope(
                            _scopeNode,
                            $"TransformController movement blocked | Key={ActionBlockKeys.Entity.TransformControllerMovement} OutputTarget={_config.OutputTarget}",
                            LogType.Warning);
                    }
                    ForceStopMovementNow();
                }
                else
                {
                    _loggedMovementBlocked = false;
                    _movementHub?.Tick(deltaTime);

                    if (!positionOverridden)
                    {
                        if (!_useBulkTransform)
                        {
                            _movementAdapter?.Tick(deltaTime);
                        }
                        else
                        {
                            UpdateBulkTransformVelocity();
                        }
                    }

                    LogMovementSuppressionIfNeeded();
                }
            }

            if (_config.EnableRotation)
            {
                _rotateHub?.Tick(deltaTime);

                if (!rotationOverridden)
                {
                    if (!_useBulkTransform)
                    {
                        _rotationAdapter?.Tick(deltaTime);
                    }
                    else
                    {
                        UpdateBulkTransformAngularVelocity();
                    }
                }
            }

            WriteBlackboardSnapshot(active: true);
        }

        bool IsMovementForceBlockedByActionBlock()
        {
            if (!_forceZeroVelocityWhenMovementBlocked)
                return false;

            return _actionBlockService?.IsBlocked(ActionBlockKeys.Entity.TransformControllerMovement) ?? false;
        }


        void SetupAdapters()
        {
            TeardownAdapters();

            var target = _config.TargetTransform != null ? _config.TargetTransform : _ownerTransform;
            _currentTarget = target;
            CacheOutputTargets(target);

            if (_config.OutputTarget == TransformOutputTarget.BulkTransform)
            {
                SetupBulkTransform(target);
                RegisterAnimationOutput(target);
                return;
            }

            if (_config.EnableMovement && _movementHub != null)
            {
                SetupMovementAdapter(target);
            }

            if (_config.EnableRotation && _rotateHub != null)
            {
                SetupRotationAdapter(target);
            }

            RegisterAnimationOutput(target);
        }

        void TeardownAdapters()
        {
            UnregisterAnimationOutput();

            if (_useBulkTransform && _bulkManager != null && _bulkHandle.IsValid)
            {
                try
                {
                    _bulkManager.Unregister(_bulkHandle);
                }
                catch (Exception)
                {
                }
            }

            _useBulkTransform = false;
            _bulkHandle = TransformHandle.Invalid;

            _movementAdapter?.Dispose();
            _movementAdapter = null;

            _rotationAdapter?.Dispose();
            _rotationAdapter = null;

            _currentTarget = null;
            _cachedRectTransform = null;
            _cachedRigidbody2D = null;
            _cachedCharacterController = null;
        }

        void CacheOutputTargets(Transform target)
        {
            _cachedRectTransform = null;
            _cachedRigidbody2D = null;
            _cachedCharacterController = null;

            switch (_config.OutputTarget)
            {
                case TransformOutputTarget.RectTransform:
                    _cachedRectTransform = _config.TargetRectTransform != null
                        ? _config.TargetRectTransform
                        : target.GetComponent<RectTransform>();
                    break;

                case TransformOutputTarget.Rigidbody2D:
                    _cachedRigidbody2D = _config.TargetRigidbody2D != null
                        ? _config.TargetRigidbody2D
                        : target.GetComponent<Rigidbody2D>();
                    break;

                case TransformOutputTarget.CharacterController:
                    _cachedCharacterController = _config.TargetCharacterController != null
                        ? _config.TargetCharacterController
                        : target.GetComponent<CharacterController>();
                    break;
            }
        }

        void RegisterAnimationOutput(Transform target)
        {
            if (_animationOutputRegistry == null || target == null)
                return;

            if (_registeredTarget != null && _registeredTarget != target)
                _animationOutputRegistry.Unregister(this);

            _registeredTarget = target;
            _animationOutputRegistry.Register(this);
        }

        void UnregisterAnimationOutput()
        {
            if (_animationOutputRegistry == null || _registeredTarget == null)
                return;

            _animationOutputRegistry.Unregister(this);
            _registeredTarget = null;
            _animationOutput.Clear();
        }

        bool ApplyAnimationPosition(Transform target)
        {
            if (_animationOutput.IsActive(TransformAnimationProperty.WorldPosition))
                return ApplyWorldPosition(target, _animationOutput.WorldPosition);

            if (_animationOutput.IsActive(TransformAnimationProperty.LocalPosition))
            {
                if (_animationOutput.IsLocalPositionAdditiveOnly)
                {
                    // additive only: current transform position + offset
                    var current = target.localPosition;
                    return ApplyLocalPosition(target, current + _animationOutput.LocalPosition);
                }
                return ApplyLocalPosition(target, _animationOutput.LocalPosition);
            }

            if (_animationOutput.IsActive(TransformAnimationProperty.AnchoredPosition))
                return ApplyAnchoredPosition(target, _animationOutput.AnchoredPosition);

            return false;
        }

        bool ApplyAnimationRotation(Transform target)
        {
            if (_animationOutput.IsActive(TransformAnimationProperty.LocalRotation))
            {
                if (_animationOutput.IsLocalRotationAdditiveOnly)
                {
                    var current = target.localEulerAngles;
                    return ApplyLocalRotation(target, current + _animationOutput.LocalEulerAngles);
                }
                return ApplyLocalRotation(target, _animationOutput.LocalEulerAngles);
            }

            return false;
        }

        void ApplyAnimationScale(Transform target)
        {
            if (_animationOutput.IsActive(TransformAnimationProperty.LocalScale))
                ApplyLocalScale(target, _animationOutput.LocalScale);
        }

        void ApplyAnimationRectExtras(Transform target)
        {
            var rect = ResolveRectTransform(target);
            if (rect == null)
                return;

            if (_animationOutput.IsActive(TransformAnimationProperty.SizeDelta))
                rect.sizeDelta = _animationOutput.SizeDelta;

            if (_animationOutput.IsActive(TransformAnimationProperty.Pivot))
            {
                if (_animationOutput.IsActive(TransformAnimationProperty.AnchoredPosition))
                    rect.pivot = _animationOutput.Pivot;
                else
                    SetPivotWithPositionPreserved(rect, _animationOutput.Pivot);
            }
        }

        bool ApplyWorldPosition(Transform target, Vector3 worldPosition)
        {
            if (_config.OutputTarget == TransformOutputTarget.BulkTransform &&
                _useBulkTransform &&
                _bulkManager != null &&
                _bulkHandle.IsValid)
            {
                _bulkManager.Teleport(_bulkHandle, new float3(worldPosition.x, worldPosition.y, worldPosition.z));
                target.position = worldPosition;
                return true;
            }

            switch (_config.OutputTarget)
            {
                case TransformOutputTarget.Rigidbody2D:
                    if (_cachedRigidbody2D != null)
                    {
                        _cachedRigidbody2D.position = new Vector2(worldPosition.x, worldPosition.y);
                        return true;
                    }
                    break;

                case TransformOutputTarget.CharacterController:
                    if (_cachedCharacterController != null)
                    {
                        var wasEnabled = _cachedCharacterController.enabled;
                        if (wasEnabled)
                            _cachedCharacterController.enabled = false;
                        target.position = worldPosition;
                        if (wasEnabled)
                            _cachedCharacterController.enabled = true;
                        return true;
                    }
                    break;
            }

            target.position = worldPosition;
            return true;
        }

        bool ApplyLocalPosition(Transform target, Vector3 localPosition)
        {
            if (_config.OutputTarget == TransformOutputTarget.BulkTransform)
            {
                var worldPosition = ResolveWorldPosition(target, localPosition);
                return ApplyWorldPosition(target, worldPosition);
            }

            if (_config.OutputTarget == TransformOutputTarget.Rigidbody2D && _cachedRigidbody2D != null)
            {
                var worldPosition = ResolveWorldPosition(target, localPosition);
                _cachedRigidbody2D.position = new Vector2(worldPosition.x, worldPosition.y);
                return true;
            }

            if (_config.OutputTarget == TransformOutputTarget.CharacterController)
            {
                var worldPosition = ResolveWorldPosition(target, localPosition);
                return ApplyWorldPosition(target, worldPosition);
            }

            target.localPosition = localPosition;
            return true;
        }

        bool ApplyAnchoredPosition(Transform target, Vector2 anchoredPosition)
        {
            var rect = ResolveRectTransform(target);
            if (rect == null)
                return false;

            rect.anchoredPosition = anchoredPosition;
            return true;
        }

        bool ApplyLocalRotation(Transform target, Vector3 localEulerAngles)
        {
            if (_config.OutputTarget == TransformOutputTarget.BulkTransform &&
                _useBulkTransform &&
                _bulkManager != null &&
                _bulkHandle.IsValid)
            {
                _bulkManager.SetRotation(_bulkHandle, ResolveWorldRotationZ(target, localEulerAngles));
                target.localEulerAngles = localEulerAngles;
                return true;
            }

            if (_config.OutputTarget == TransformOutputTarget.Rigidbody2D && _cachedRigidbody2D != null)
            {
                _cachedRigidbody2D.rotation = ResolveWorldRotationZ(target, localEulerAngles);
                return true;
            }

            target.localEulerAngles = localEulerAngles;
            return true;
        }

        void ApplyLocalScale(Transform target, Vector3 localScale)
        {
            target.localScale = localScale;
        }

        RectTransform? ResolveRectTransform(Transform target)
        {
            if (_cachedRectTransform != null)
                return _cachedRectTransform;

            return target as RectTransform;
        }

        static Vector3 ResolveWorldPosition(Transform target, Vector3 localPosition)
        {
            if (target.parent == null)
                return localPosition;

            return target.parent.TransformPoint(localPosition);
        }

        static float ResolveWorldRotationZ(Transform target, Vector3 localEulerAngles)
        {
            if (target.parent == null)
                return localEulerAngles.z;

            var worldRotation = target.parent.rotation * Quaternion.Euler(localEulerAngles);
            return worldRotation.eulerAngles.z;
        }

        static void SetPivotWithPositionPreserved(RectTransform rect, Vector2 newPivot)
        {
            var oldPivot = rect.pivot;
            var size = rect.rect.size;

            var deltaPos = new Vector2(
                (newPivot.x - oldPivot.x) * size.x,
                (newPivot.y - oldPivot.y) * size.y
            );

            rect.pivot = newPivot;
            rect.anchoredPosition += deltaPos;
        }

        public bool TryTeleportWorld(Vector3 worldPosition, bool resetVelocity = true)
        {
            if (_disposed)
            {
                return false;
            }

            if (!_acquired)
            {
                return false;
            }

            var target = _currentTarget != null ? _currentTarget : (_config.TargetTransform != null ? _config.TargetTransform : _ownerTransform);
            if (target == null)
            {
                return false;
            }

            // BulkTransform is authoritative: update its internal position buffer.
            if (_useBulkTransform && _bulkManager != null && _bulkHandle.IsValid)
            {
                _bulkManager.Teleport(_bulkHandle, new float3(worldPosition.x, worldPosition.y, worldPosition.z));
                if (resetVelocity)
                    _bulkManager.SetVelocity(_bulkHandle, float3.zero);

                // Also write Transform for immediate visual update this frame.
                target.position = worldPosition;
                return true;
            }

            switch (_config.OutputTarget)
            {
                case TransformOutputTarget.Rigidbody2D:
                    {
                        var rb = _config.TargetRigidbody2D != null ? _config.TargetRigidbody2D : target.GetComponent<Rigidbody2D>();
                        if (rb == null)
                            break;

                        rb.position = new Vector2(worldPosition.x, worldPosition.y);
                        if (resetVelocity)
                            rb.linearVelocity = Vector2.zero;
                        return true;
                    }

                case TransformOutputTarget.CharacterController:
                    {
                        var cc = _config.TargetCharacterController != null ? _config.TargetCharacterController : target.GetComponent<CharacterController>();
                        if (cc == null)
                            break;

                        var wasEnabled = cc.enabled;
                        if (wasEnabled)
                            cc.enabled = false;
                        target.position = worldPosition;
                        if (wasEnabled)
                            cc.enabled = true;
                        return true;
                    }

                case TransformOutputTarget.Transform:
                case TransformOutputTarget.RectTransform:
                case TransformOutputTarget.None:
                default:
                    target.position = worldPosition;
                    return true;
            }

            target.position = worldPosition;
            return true;
        }

        void SetupBulkTransform(Transform target)
        {
            if (_bulkManager == null)
            {
                SetupFallbackAdapters(target);
                return;
            }

            float3 initVel = float3.zero;
            float initAngVel = 0f;

            if (_movementHub?.Output != null)
            {
                var v = _movementHub.Output.Value;
                initVel = new float3(v.x, v.y, 0f);
            }

            if (_rotateHub?.Output != null)
            {
                initAngVel = _rotateHub.Output.Value;
            }

            _bulkHandle = _bulkManager.Register(target, initVel, initAngVel);
            _useBulkTransform = true;
        }

        void SetupFallbackAdapters(Transform target)
        {
            if (_config.EnableMovement && _movementHub != null)
            {
                _movementAdapter = new TransformMovementAdapter(target, _movementHub.Output);
            }

            if (_config.EnableRotation && _rotateHub != null)
            {
                _rotationAdapter = new TransformRotationAdapter(target, _rotateHub.Output);
            }
        }

        void SetupMovementAdapter(Transform target)
        {
            if (_movementHub == null)
            {
                if (!_loggedMissingMovementAdapter)
                {
                    _loggedMissingMovementAdapter = true;
                    BuildConsoleLog.Scope(
                        _scopeNode,
                        $"TransformController missing movement hub | OutputTarget={_config.OutputTarget}",
                        LogType.Warning);
                }
                return;
            }

            switch (_config.OutputTarget)
            {
                case TransformOutputTarget.Transform:
                    _movementAdapter = new TransformMovementAdapter(target, _movementHub.Output);
                    break;

                case TransformOutputTarget.RectTransform:
                    {
                        var rt = _config.TargetRectTransform != null
                            ? _config.TargetRectTransform
                            : target.GetComponent<RectTransform>();

                        if (rt != null)
                            _movementAdapter = new Game.Movement.RectTransformMovementAdapter(rt, _movementHub.Output);
                        break;
                    }

                case TransformOutputTarget.Rigidbody2D:
                    {
                        var rb = _config.TargetRigidbody2D != null
                            ? _config.TargetRigidbody2D
                            : target.GetComponent<Rigidbody2D>();

                        if (rb != null)
                        {
                            _movementAdapter = new Rigidbody2DMovementAdapter(
                                rb,
                                _movementHub.Output,
                                _config.Rigidbody2DVelocityMode,
                                _config.Rigidbody2DAdditiveControl,
                                _config.Rigidbody2DGravityClamp);
                        }
                        else if (!_loggedMissingRigidbody)
                        {
                            _loggedMissingRigidbody = true;
                            BuildConsoleLog.Scope(
                                _scopeNode,
                                $"TransformController missing Rigidbody2D | Target={target.name} OutputTarget={_config.OutputTarget}",
                                LogType.Warning);
                        }
                        break;
                    }

                case TransformOutputTarget.CharacterController:
                    {
                        var cc = _config.TargetCharacterController != null
                            ? _config.TargetCharacterController
                            : target.GetComponent<CharacterController>();

                        if (cc != null)
                            _movementAdapter = new CharacterControllerMovementAdapter(cc, _movementHub.Output);
                        break;
                    }
            }

            if (_movementAdapter == null && !_loggedMissingMovementAdapter)
            {
                _loggedMissingMovementAdapter = true;
                BuildConsoleLog.Scope(
                    _scopeNode,
                    $"TransformController movement adapter unavailable | OutputTarget={_config.OutputTarget} Target={target.name}",
                    LogType.Warning);
            }
        }

        void SetupRotationAdapter(Transform target)
        {
            if (_rotateHub == null)
                return;

            switch (_config.OutputTarget)
            {
                case TransformOutputTarget.Transform:
                    _rotationAdapter = new TransformRotationAdapter(target, _rotateHub.Output);
                    break;

                case TransformOutputTarget.Rigidbody2D:
                    {
                        var rb = _config.TargetRigidbody2D != null
                            ? _config.TargetRigidbody2D
                            : target.GetComponent<Rigidbody2D>();

                        if (rb != null)
                            _rotationAdapter = new Rigidbody2DRotationAdapter(rb, _rotateHub.Output);
                        break;
                    }

                case TransformOutputTarget.CharacterController:
                    _rotationAdapter = new TransformRotationAdapter(target, _rotateHub.Output);
                    break;
            }
        }

        void UpdateBulkTransformVelocity()
        {
            if (_bulkManager == null || !_bulkHandle.IsValid || _movementHub?.Output == null)
                return;

            var v = _movementHub.Output.Value;
            _bulkManager.SetVelocity(_bulkHandle, new float3(v.x, v.y, 0f));
        }

        void UpdateBulkTransformAngularVelocity()
        {
            if (_bulkManager == null || !_bulkHandle.IsValid || _rotateHub?.Output == null)
                return;

            _bulkManager.SetAngularVelocity(_bulkHandle, _rotateHub.Output.Value);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _acquired = false;
            WriteBlackboardSnapshot(active: false);
            TeardownAdapters();
        }

        public void SetMovementEnabled(bool enabled)
        {
            _config.EnableMovement = enabled;
            if (!enabled)
                ForceStopMovementNow();
        }

        public void SetRotationEnabled(bool enabled)
        {
            _config.EnableRotation = enabled;
            if (_acquired && !_disposed)
                SetupAdapters();
        }

        public void SetForceZeroVelocityWhenMovementBlocked(bool enabled)
        {
            _forceZeroVelocityWhenMovementBlocked = enabled;
            if (enabled && (_actionBlockService?.IsBlocked(ActionBlockKeys.Entity.TransformControllerMovement) ?? false))
                ForceStopMovementNow();
        }

        public bool SetTransformControllerMovementBlocked(bool blocked, string? reason = null)
        {
            if (_actionBlockService == null)
                return false;

            _actionBlockService.SetBlockFlag(
                ActionBlockKeys.Entity.TransformControllerMovement,
                blocked,
                string.IsNullOrWhiteSpace(reason) ? null : reason);

            if (blocked && _forceZeroVelocityWhenMovementBlocked)
                ForceStopMovementNow();

            return true;
        }

        public bool TryApplyRigidbody2DSettings(
            bool applySimulated, bool simulated,
            bool applyGravityScale, float gravityScale,
            bool applyFreezeRotation, bool freezeRotation,
            bool applyLinearVelocity, Vector2 linearVelocity,
            bool applyAngularVelocity, float angularVelocity)
        {
            if (_config.OutputTarget != TransformOutputTarget.Rigidbody2D)
                return false;

            var rb = ResolveTelemetryRigidbody2D();
            if (rb == null)
                return false;

            if (applySimulated)
                rb.simulated = simulated;

            if (applyGravityScale)
                rb.gravityScale = gravityScale;

            if (applyFreezeRotation)
            {
                var constraints = rb.constraints;
                if (freezeRotation)
                    constraints |= RigidbodyConstraints2D.FreezeRotation;
                else
                    constraints &= ~RigidbodyConstraints2D.FreezeRotation;
                rb.constraints = constraints;
            }

            if (applyLinearVelocity)
                rb.linearVelocity = linearVelocity;

            if (applyAngularVelocity)
                rb.angularVelocity = angularVelocity;

            return true;
        }

        public bool TryAddForceToRigidbody2D(Vector2 force, ForceMode2D mode = ForceMode2D.Force)
        {
            if (_config.OutputTarget != TransformOutputTarget.Rigidbody2D)
                return false;

            var rb = ResolveTelemetryRigidbody2D();
            if (rb == null)
                return false;

            rb.AddForce(force, mode);
            return true;
        }

        public void ForceStopMovementNow()
        {
            if (_movementHub is MovementChannelHubService movementHub)
                movementHub.ResetAllVelocities();

            if (_useBulkTransform && _bulkManager != null && _bulkHandle.IsValid)
                _bulkManager.SetVelocity(_bulkHandle, float3.zero);

            if (_config.OutputTarget == TransformOutputTarget.Rigidbody2D)
            {
                var rb = ResolveTelemetryRigidbody2D();
                if (rb != null)
                    rb.linearVelocity = Vector2.zero;
            }
        }

        Rigidbody2D? ResolveTelemetryRigidbody2D()
        {
            if (_cachedRigidbody2D != null)
                return _cachedRigidbody2D;

            if (_config.TargetRigidbody2D != null)
                return _config.TargetRigidbody2D;

            if (_config.OutputTarget != TransformOutputTarget.Rigidbody2D)
                return null;

            var target = _currentTarget != null
                ? _currentTarget
                : (_config.TargetTransform != null ? _config.TargetTransform : _ownerTransform);

            if (target == null)
                return null;

            return target.GetComponent<Rigidbody2D>();
        }

        void LogAcquireState()
        {
            if (_scopeNode == null)
                return;

            var rb = ResolveTelemetryRigidbody2D();
            var target = _currentTarget != null
                ? _currentTarget
                : (_config.TargetTransform != null ? _config.TargetTransform : _ownerTransform);
            var targetName = target != null ? target.name : "(none)";
            var rbState = rb != null
                ? $" HasRigidbody2D=True Simulated={rb.simulated} BodyType={rb.bodyType} Gravity={rb.gravityScale:F2}"
                : " HasRigidbody2D=False";

            BuildConsoleLog.Scope(
                _scopeNode,
                $"TransformController acquire | Target={targetName} OutputTarget={_config.OutputTarget} MovementEnabled={_config.EnableMovement} RotationEnabled={_config.EnableRotation} MovementHub={(_movementHub != null)} MovementAdapter={(_movementAdapter != null)}{rbState}");
        }

        void LogMovementSuppressionIfNeeded()
        {
            if (_scopeNode == null || _movementHub?.Output == null)
                return;

            var requestedVelocity = _movementHub.Output.Value;
            if (requestedVelocity.sqrMagnitude <= 0.0001f)
            {
                _loggedRigidbodySuppressed = false;
                return;
            }

            if (_config.OutputTarget != TransformOutputTarget.Rigidbody2D)
            {
                _loggedRigidbodySuppressed = false;
                return;
            }

            var rb = ResolveTelemetryRigidbody2D();
            if (rb == null)
            {
                if (!_loggedMissingRigidbody)
                {
                    _loggedMissingRigidbody = true;
                    BuildConsoleLog.Scope(
                        _scopeNode,
                        $"TransformController movement output without Rigidbody2D | Requested=({requestedVelocity.x:F2},{requestedVelocity.y:F2})",
                        LogType.Warning);
                }
                return;
            }

            var currentVelocity = rb.linearVelocity;
            var suppressed = !rb.simulated || currentVelocity.sqrMagnitude <= 0.0001f;
            if (!suppressed)
            {
                _loggedRigidbodySuppressed = false;
                return;
            }

            if (_loggedRigidbodySuppressed)
                return;

            _loggedRigidbodySuppressed = true;
            BuildConsoleLog.Scope(
                _scopeNode,
                $"TransformController suppressed movement | Requested=({requestedVelocity.x:F2},{requestedVelocity.y:F2}) Actual=({currentVelocity.x:F2},{currentVelocity.y:F2}) Simulated={rb.simulated} BodyType={rb.bodyType} Gravity={rb.gravityScale:F2} MovementBlocked={(_actionBlockService?.IsBlocked(ActionBlockKeys.Entity.TransformControllerMovement) ?? false)}",
                LogType.Warning);
        }

        // Ensure initial rotation is applied to either the bulk manager entry or the transform.
        public void SetInitialRotation(Quaternion rotation)
        {
            if (_disposed) return;

            if (_useBulkTransform && _bulkManager != null && _bulkHandle.IsValid)
            {
                // Set rotation degrees on bulk manager so subsequent job applies correct rotation.
                _bulkManager.SetRotation(_bulkHandle, rotation.eulerAngles.z);
            }
            else
            {
                _ownerTransform.rotation = rotation;
            }
        }

        void WriteBlackboardSnapshot(bool active)
        {
            var blackboard = _blackboard;
            if (blackboard == null)
                return;

            var vars = blackboard.LocalVars;
            if (vars == null)
                return;

            var target = _currentTarget != null
                ? _currentTarget
                : (_config.TargetTransform != null ? _config.TargetTransform : _ownerTransform);

            TrySetVar(vars, VarIds.GameLib.Movement.TransformController.active, DynamicVariant.FromInt(active ? 1 : 0));
            TrySetVar(vars, VarIds.GameLib.Movement.TransformController.outputTarget, DynamicVariant.FromInt((int)_config.OutputTarget));
            TrySetVar(vars, VarIds.GameLib.Movement.TransformController.movementEnabled, DynamicVariant.FromInt(_config.EnableMovement ? 1 : 0));
            TrySetVar(vars, VarIds.GameLib.Movement.TransformController.rotationEnabled, DynamicVariant.FromInt(_config.EnableRotation ? 1 : 0));

            if (target != null)
            {
                var worldPos = target.position;
                var localEuler = target.localEulerAngles;
                TrySetVar(vars, VarIds.GameLib.Movement.TransformController.Target.name, DynamicVariant.FromString(target.name));
                TrySetVar(vars, VarIds.GameLib.Movement.TransformController.Target.instanceId, DynamicVariant.FromInt(target.GetInstanceID()));
                TrySetVar(vars, VarIds.GameLib.Movement.TransformController.Target.WorldPos.x, DynamicVariant.FromFloat(worldPos.x));
                TrySetVar(vars, VarIds.GameLib.Movement.TransformController.Target.WorldPos.y, DynamicVariant.FromFloat(worldPos.y));
                TrySetVar(vars, VarIds.GameLib.Movement.TransformController.Target.LocalEuler.z, DynamicVariant.FromFloat(localEuler.z));
            }
            else
            {
                TrySetVar(vars, VarIds.GameLib.Movement.TransformController.Target.name, DynamicVariant.FromString(string.Empty));
                TrySetVar(vars, VarIds.GameLib.Movement.TransformController.Target.instanceId, DynamicVariant.FromInt(0));
            }

            var velocity = CurrentVelocity;
            TrySetVar(vars, VarIds.GameLib.Movement.TransformController.Channel.Velocity.x, DynamicVariant.FromFloat(velocity.x));
            TrySetVar(vars, VarIds.GameLib.Movement.TransformController.Channel.Velocity.y, DynamicVariant.FromFloat(velocity.y));
            TrySetVar(vars, VarIds.GameLib.Movement.TransformController.Channel.angularVelocity, DynamicVariant.FromFloat(CurrentAngularVelocity));

            if (_config.OutputTarget != TransformOutputTarget.Rigidbody2D)
            {
                TrySetVar(vars, VarIds.GameLib.Movement.TransformController.Rigidbody2D.has, DynamicVariant.FromInt(0));
                return;
            }

            var rb = ResolveTelemetryRigidbody2D();
            TrySetVar(vars, VarIds.GameLib.Movement.TransformController.Rigidbody2D.has, DynamicVariant.FromInt(rb != null ? 1 : 0));
            TrySetVar(vars, VarIds.GameLib.Movement.TransformController.Rigidbody2D.velocityMode, DynamicVariant.FromInt((int)_config.Rigidbody2DVelocityMode));
            if (rb == null)
                return;

            var rbVelocity = rb.linearVelocity;
            TrySetVar(vars, VarIds.GameLib.Movement.TransformController.Rigidbody2D.freezeRotation, DynamicVariant.FromInt((rb.constraints & RigidbodyConstraints2D.FreezeRotation) != 0 ? 1 : 0));
            TrySetVar(vars, VarIds.GameLib.Movement.TransformController.Rigidbody2D.gravityScale, DynamicVariant.FromFloat(rb.gravityScale));
            TrySetVar(vars, VarIds.GameLib.Movement.TransformController.Rigidbody2D.LinearVelocity.x, DynamicVariant.FromFloat(rbVelocity.x));
            TrySetVar(vars, VarIds.GameLib.Movement.TransformController.Rigidbody2D.LinearVelocity.y, DynamicVariant.FromFloat(rbVelocity.y));
            TrySetVar(vars, VarIds.GameLib.Movement.TransformController.Rigidbody2D.angularVelocity, DynamicVariant.FromFloat(rb.angularVelocity));
            TrySetVar(vars, VarIds.GameLib.Movement.TransformController.Rigidbody2D.rotation, DynamicVariant.FromFloat(rb.rotation));
        }

        static void TrySetVar(IVarStore vars, int varId, DynamicVariant value)
        {
            if (varId <= 0 || vars == null)
                return;

            vars.TrySetVariant(varId, value);
        }
    }
}

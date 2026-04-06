#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.TransformSystem
{
    internal sealed class TransformChannelRuntime : ITransformChannelRuntime
    {
        const float Epsilon = 0.00001f;

        readonly string _tag;
        readonly TransformChannelOptions _options;
        readonly IScopeNode _owner;

        TransformControllerService? _service;
        TransformChannelOutputTarget _outputTarget;
        TransformChannelFeaturePreset _featurePreset = new();
        TransformChannelEffectPreset _effectPreset = new();

        readonly List<TransformManagerChannelApplyRequest> _globalApplyRequests = new();
        readonly List<TransformManagerMovementEntry> _movementContinuousEntries = new();
        readonly List<TransformManagerMovementEntry> _movementOneShotEntries = new();
        readonly List<TransformManagerRotateEntry> _rotateContinuousEntries = new();
        readonly List<TransformManagerRotateEntry> _rotateOneShotEntries = new();
        readonly List<TransformManagerScaleEntry> _scaleContinuousEntries = new();
        readonly List<TransformManagerScaleEntry> _scaleOneShotEntries = new();

        float _appliedRotationOffset;
        bool _scaleStateInitialized;
        bool _lastScaleHadOverride;
        Vector3 _lastScaleOverrideBase = Vector3.one;
        Vector3 _lastScaleAffineAdd = Vector3.zero;
        Vector3 _lastScaleAffineMultiply = Vector3.one;

        public TransformChannelRuntime(string tag, TransformChannelOptions options, IScopeNode owner)
        {
            _tag = TransformChannelTagUtility.Normalize(tag);
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public string Tag => _tag;
        public Transform TargetTransform => _service?.TargetTransform ?? _options.OwnerTransform;
        public TransformChannelOutputTarget OutputTarget => _outputTarget;
        public Vector2 CurrentVelocity => _service?.CurrentVelocity ?? Vector2.zero;
        public TransformChannelFeaturePreset FeaturePreset => _featurePreset;
        public TransformChannelEffectPreset EffectPreset => _effectPreset;

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (!ReferenceEquals(_owner, scope))
                return;

            _ = isReset;
            RebuildService(scope);
            _service?.OnAcquire(scope, isReset);
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            if (!ReferenceEquals(_owner, scope))
                return;

            ResetGlobalRuntimeState();

            if (_service != null)
            {
                _service.OnRelease(scope, isReset);
                _service.Dispose();
            }

            _service = null;
        }

        public void Tick()
        {
            _service?.Tick();
            ApplyTransformManagerGlobals();
        }

        public bool TryTeleportWorld(Vector3 worldPosition, bool resetVelocity = true)
        {
            return _service != null && _service.TryTeleportWorld(worldPosition, resetVelocity);
        }

        public void SetMovementEnabled(bool enabled)
        {
            _service?.SetMovementEnabled(enabled);
        }

        public void SetRotationEnabled(bool enabled)
        {
            _service?.SetRotationEnabled(enabled);
        }

        public void SetForceZeroVelocityWhenMovementBlocked(bool enabled)
        {
            _service?.SetForceZeroVelocityWhenMovementBlocked(enabled);
        }

        public bool SetTransformChannelMovementBlocked(bool blocked, string? reason = null)
        {
            return _service != null && _service.SetTransformControllerMovementBlocked(blocked, reason);
        }

        public bool TryApplyRigidbody2DSettings(
            bool applySimulated,
            bool simulated,
            bool applyGravityScale,
            float gravityScale,
            bool applyFreezeRotation,
            bool freezeRotation,
            bool applyLinearVelocity,
            Vector2 linearVelocity,
            bool applyAngularVelocity,
            float angularVelocity)
        {
            return _service != null && _service.TryApplyRigidbody2DSettings(
                applySimulated,
                simulated,
                applyGravityScale,
                gravityScale,
                applyFreezeRotation,
                freezeRotation,
                applyLinearVelocity,
                linearVelocity,
                applyAngularVelocity,
                angularVelocity);
        }

        public bool TryAddForceToRigidbody2D(Vector2 force, ForceMode2D mode = ForceMode2D.Force)
        {
            return _service != null && _service.TryAddForceToRigidbody2D(force, mode);
        }

        public void ForceStopMovementNow()
        {
            _service?.ForceStopMovementNow();
        }

        public void SetInitialRotation(Quaternion rotation)
        {
            _service?.SetInitialRotation(rotation);
        }

        void RebuildService(IScopeNode scope)
        {
            if (_service != null)
            {
                _service.Dispose();
                _service = null;
            }

            var resolver = scope.Resolver;
            if (resolver == null)
                return;

            var dynamicContext = CreateDynamicContext(scope);
            var outputPreset = _options.OutputPresetValue.GetOrDefault(dynamicContext, new TransformChannelTransformOutputPreset()).CreateRuntimeCopy();
            _featurePreset = _options.FeaturePresetValue.GetOrDefault(dynamicContext, new TransformChannelFeaturePreset()).CreateRuntimeCopy();
            _effectPreset = _options.EffectPresetValue.GetOrDefault(dynamicContext, new TransformChannelEffectPreset()).CreateRuntimeCopy();
            _effectPreset.BuildGlobalApplyRequests(_globalApplyRequests);
            _outputTarget = outputPreset.OutputTarget;
            ResetGlobalRuntimeState();

            var config = new TransformControllerConfig
            {
                EnableMovement = _featurePreset.EnableMovement,
                EnableRotation = _featurePreset.EnableRotation,
            };

            outputPreset.ApplyToConfig(config, _options.OwnerTransform);

            _service = new TransformControllerService(config, _options.OwnerTransform, resolver);
        }

        void ApplyTransformManagerGlobals()
        {
            if (_service == null)
                return;

            if (_globalApplyRequests.Count == 0)
                return;

            var target = _service.TargetTransform;
            if (target == null)
                return;

            if (!TryResolveTransformManagerService(out var manager) || manager == null)
            {
                RestoreRotationOffsetIfNeeded(target);
                RestoreScaleIfNeeded(target);
                return;
            }

            var deltaTime = ResolveDeltaTime(_service.OutputTarget);

            if (_featurePreset.EnableMovement)
                ApplyMovementGlobals(manager, target, deltaTime);

            if (_featurePreset.EnableRotation)
                ApplyRotateGlobals(manager, target, deltaTime);
            else
                RestoreRotationOffsetIfNeeded(target);

            if (_featurePreset.EnableScale)
                ApplyScaleGlobals(manager, target);
            else
                RestoreScaleIfNeeded(target);
        }

        void ApplyMovementGlobals(ITransformManagerService manager, Transform target, float deltaTime)
        {
            var baseVelocity = _service?.CurrentVelocity ?? Vector2.zero;
            var composedVelocity = baseVelocity;

            for (var i = 0; i < _globalApplyRequests.Count; i++)
            {
                manager.CollectMovementEntries(_globalApplyRequests[i], _movementContinuousEntries, _movementOneShotEntries);
                ApplyMovementEntries(_movementContinuousEntries, ref composedVelocity);
                ApplyMovementEntries(_movementOneShotEntries, ref composedVelocity);
            }

            var velocityDelta = composedVelocity - baseVelocity;
            if (velocityDelta.sqrMagnitude <= Epsilon)
                return;

            if (_service != null && _service.OutputTarget == TransformOutputTarget.Rigidbody2D)
            {
                _service.TryApplyRigidbody2DSettings(
                    applySimulated: false,
                    simulated: false,
                    applyGravityScale: false,
                    gravityScale: 0f,
                    applyFreezeRotation: false,
                    freezeRotation: false,
                    applyLinearVelocity: true,
                    linearVelocity: composedVelocity,
                    applyAngularVelocity: false,
                    angularVelocity: 0f);
                return;
            }

            if (deltaTime <= 0f)
                return;

            target.position += new Vector3(velocityDelta.x, velocityDelta.y, 0f) * deltaTime;
        }

        void ApplyRotateGlobals(ITransformManagerService manager, Transform target, float deltaTime)
        {
            var continuousOffset = 0f;
            var continuousAngularVelocity = _service?.CurrentAngularVelocity ?? 0f;
            var oneShotOffset = 0f;
            var oneShotAngularVelocity = 0f;

            for (var i = 0; i < _globalApplyRequests.Count; i++)
            {
                manager.CollectRotateEntries(_globalApplyRequests[i], _rotateContinuousEntries, _rotateOneShotEntries);
                ApplyRotateEntries(_rotateContinuousEntries, ref continuousOffset, ref continuousAngularVelocity);

                var oneShotOffsetWork = continuousOffset;
                var oneShotAngularWork = continuousAngularVelocity;
                ApplyRotateEntries(_rotateOneShotEntries, ref oneShotOffsetWork, ref oneShotAngularWork);

                oneShotOffset += oneShotOffsetWork - continuousOffset;
                oneShotAngularVelocity += oneShotAngularWork - continuousAngularVelocity;
            }

            var euler = target.localEulerAngles;
            var baseZ = euler.z - _appliedRotationOffset;
            var nextZ = baseZ + continuousOffset + oneShotOffset + (continuousAngularVelocity + oneShotAngularVelocity) * deltaTime;
            target.localEulerAngles = new Vector3(euler.x, euler.y, nextZ);
            _appliedRotationOffset = continuousOffset;
        }

        void ApplyScaleGlobals(ITransformManagerService manager, Transform target)
        {
            var hasAnyScaleEntry = false;
            var hasOverride = false;
            var affineTrackable = true;
            var affineAdd = Vector3.zero;
            var affineMultiply = Vector3.one;

            var baseScale = RecoverScaleBase(target.localScale);
            var composedScale = baseScale;

            for (var i = 0; i < _globalApplyRequests.Count; i++)
            {
                manager.CollectScaleEntries(_globalApplyRequests[i], _scaleContinuousEntries, _scaleOneShotEntries);

                if (_scaleContinuousEntries.Count > 0 || _scaleOneShotEntries.Count > 0)
                    hasAnyScaleEntry = true;

                ApplyScaleEntries(_scaleContinuousEntries, ref composedScale, ref hasOverride, ref affineTrackable, ref affineMultiply, ref affineAdd);
                ApplyScaleEntries(_scaleOneShotEntries, ref composedScale, ref hasOverride, ref affineTrackable, ref affineMultiply, ref affineAdd);
            }

            if (!hasAnyScaleEntry)
            {
                if (_scaleStateInitialized)
                {
                    target.localScale = baseScale;
                    ResetScaleState();
                }

                return;
            }

            target.localScale = composedScale;
            _scaleStateInitialized = true;
            _lastScaleHadOverride = hasOverride;

            if (hasOverride)
            {
                _lastScaleOverrideBase = baseScale;
                _lastScaleAffineAdd = Vector3.zero;
                _lastScaleAffineMultiply = Vector3.one;
                return;
            }

            _lastScaleAffineAdd = affineTrackable ? affineAdd : Vector3.zero;
            _lastScaleAffineMultiply = affineTrackable ? affineMultiply : Vector3.one;
        }

        void RestoreRotationOffsetIfNeeded(Transform target)
        {
            if (Mathf.Abs(_appliedRotationOffset) <= Epsilon)
                return;

            var euler = target.localEulerAngles;
            euler.z -= _appliedRotationOffset;
            target.localEulerAngles = euler;
            _appliedRotationOffset = 0f;
        }

        void RestoreScaleIfNeeded(Transform target)
        {
            if (!_scaleStateInitialized)
                return;

            target.localScale = RecoverScaleBase(target.localScale);
            ResetScaleState();
        }

        Vector3 RecoverScaleBase(Vector3 currentScale)
        {
            if (!_scaleStateInitialized)
                return currentScale;

            if (_lastScaleHadOverride)
                return _lastScaleOverrideBase;

            var baseScale = currentScale - _lastScaleAffineAdd;
            return DivideScaleSafe(baseScale, _lastScaleAffineMultiply);
        }

        static Vector3 DivideScaleSafe(Vector3 value, Vector3 divisor)
        {
            return new Vector3(
                Mathf.Abs(divisor.x) > Epsilon ? value.x / divisor.x : value.x,
                Mathf.Abs(divisor.y) > Epsilon ? value.y / divisor.y : value.y,
                Mathf.Abs(divisor.z) > Epsilon ? value.z / divisor.z : value.z);
        }

        void ResetGlobalRuntimeState()
        {
            _appliedRotationOffset = 0f;
            ResetScaleState();

            _movementContinuousEntries.Clear();
            _movementOneShotEntries.Clear();
            _rotateContinuousEntries.Clear();
            _rotateOneShotEntries.Clear();
            _scaleContinuousEntries.Clear();
            _scaleOneShotEntries.Clear();
        }

        void ResetScaleState()
        {
            _scaleStateInitialized = false;
            _lastScaleHadOverride = false;
            _lastScaleOverrideBase = Vector3.one;
            _lastScaleAffineAdd = Vector3.zero;
            _lastScaleAffineMultiply = Vector3.one;
        }

        bool TryResolveTransformManagerService(out ITransformManagerService? manager)
        {
            manager = null;

            for (var current = _owner; current != null; current = current.Parent)
            {
                var resolver = current.Resolver;
                if (resolver == null)
                    continue;

                if (resolver.TryResolve<ITransformManagerService>(out var resolved) && resolved != null)
                {
                    manager = resolved;
                    return true;
                }
            }

            return false;
        }

        static float ResolveDeltaTime(TransformOutputTarget outputTarget)
        {
            return outputTarget == TransformOutputTarget.Rigidbody2D
                ? Time.fixedDeltaTime
                : Time.deltaTime;
        }

        static void ApplyMovementEntries(List<TransformManagerMovementEntry> entries, ref Vector2 currentVelocity)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var settings = entry.Settings;
                var weightedValue = entry.Velocity * Mathf.Max(0f, settings.Weight);

                switch (settings.BlendMode)
                {
                    case TransformChannelGlobalBlendMode.Override:
                        currentVelocity = weightedValue;
                        break;
                    case TransformChannelGlobalBlendMode.Additive:
                        currentVelocity += weightedValue;
                        break;
                    case TransformChannelGlobalBlendMode.Multiply:
                        currentVelocity = Vector2.Scale(currentVelocity, weightedValue);
                        break;
                }
            }
        }

        static void ApplyRotateEntries(List<TransformManagerRotateEntry> entries, ref float currentOffset, ref float currentAngularVelocity)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var settings = entry.Settings;
                var weightedOffset = entry.OffsetDegrees * Mathf.Max(0f, settings.Weight);
                var weightedAngular = entry.AngularVelocity * Mathf.Max(0f, settings.Weight);

                switch (settings.BlendMode)
                {
                    case TransformChannelGlobalBlendMode.Override:
                        currentOffset = weightedOffset;
                        currentAngularVelocity = weightedAngular;
                        break;
                    case TransformChannelGlobalBlendMode.Additive:
                        currentOffset += weightedOffset;
                        currentAngularVelocity += weightedAngular;
                        break;
                    case TransformChannelGlobalBlendMode.Multiply:
                        currentOffset *= weightedOffset;
                        currentAngularVelocity *= weightedAngular;
                        break;
                }
            }
        }

        static void ApplyScaleEntries(
            List<TransformManagerScaleEntry> entries,
            ref Vector3 currentScale,
            ref bool hasOverride,
            ref bool affineTrackable,
            ref Vector3 affineMultiply,
            ref Vector3 affineAdd)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var settings = entry.Settings;

                switch (settings.BlendMode)
                {
                    case TransformChannelGlobalBlendMode.Override:
                    {
                        var t = Mathf.Clamp01(settings.Weight);
                        currentScale = Vector3.Lerp(currentScale, entry.LocalScale, t);
                        hasOverride = true;
                        affineTrackable = false;
                        break;
                    }

                    case TransformChannelGlobalBlendMode.Additive:
                    {
                        var add = entry.LocalScale * Mathf.Max(0f, settings.Weight);
                        currentScale += add;
                        if (affineTrackable)
                            affineAdd += add;
                        break;
                    }

                    case TransformChannelGlobalBlendMode.Multiply:
                    {
                        var t = Mathf.Clamp01(settings.Weight);
                        var multiply = Vector3.Lerp(Vector3.one, entry.LocalScale, t);
                        currentScale = Vector3.Scale(currentScale, multiply);
                        if (affineTrackable)
                        {
                            affineMultiply = Vector3.Scale(affineMultiply, multiply);
                            affineAdd = Vector3.Scale(affineAdd, multiply);
                        }
                        break;
                    }
                }
            }
        }

        static IDynamicContext CreateDynamicContext(IScopeNode scope)
        {
            var vars = ResolveVars(scope);
            return new SimpleDynamicContext(vars, scope);
        }

        static IVarStore ResolveVars(IScopeNode scope)
        {
            var resolver = scope.Resolver;
            if (resolver != null && resolver.TryResolve<IBlackboardService>(out var blackboard) && blackboard?.LocalVars != null)
                return blackboard.LocalVars;

            return NullVarStore.Instance;
        }

    }

    public sealed class TransformChannelHubService :
        ITransformChannelHubService,
        ITransformTeleportService,
        ITransformControllerPoseReader,
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        ITickable
    {
        readonly IScopeNode _owner;
        readonly TransformChannelHub _mb;
        readonly Dictionary<string, TransformChannelRuntime> _runtimeByTag = new(StringComparer.Ordinal);
        readonly List<TransformChannelRuntime> _orderedRuntimes = new();

        bool _isAcquired;

        public TransformChannelHubService(IScopeNode owner, TransformChannelHub mb)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _mb = mb ?? throw new ArgumentNullException(nameof(mb));
        }

        public int ChannelCount => _orderedRuntimes.Count;

        public Transform TargetTransform
        {
            get
            {
                if (TryGetDefaultRuntime(out var runtime) && runtime != null)
                    return runtime.TargetTransform;

                return _owner.Identity?.SelfTransform ?? _mb.transform;
            }
        }

        public TransformOutputTarget OutputTarget
        {
            get
            {
                if (TryGetDefaultRuntime(out var runtime) && runtime != null)
                {
                    switch (runtime.OutputTarget)
                    {
                        case TransformChannelOutputTarget.Transform:
                            return TransformOutputTarget.Transform;
                        case TransformChannelOutputTarget.RectTransform:
                            return TransformOutputTarget.RectTransform;
                        case TransformChannelOutputTarget.BulkTransform:
                            return TransformOutputTarget.BulkTransform;
                        case TransformChannelOutputTarget.Rigidbody2D:
                            return TransformOutputTarget.Rigidbody2D;
                        case TransformChannelOutputTarget.CharacterController:
                            return TransformOutputTarget.CharacterController;
                        default:
                            return TransformOutputTarget.None;
                    }
                }

                return TransformOutputTarget.None;
            }
        }

        public Vector2 CurrentVelocity
        {
            get
            {
                if (TryGetDefaultRuntime(out var runtime) && runtime != null)
                    return runtime.CurrentVelocity;

                return Vector2.zero;
            }
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (!ReferenceEquals(_owner, scope))
                return;

            _isAcquired = true;
            RebuildRuntimes(scope, isReset);
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            if (!ReferenceEquals(_owner, scope))
                return;

            ReleaseRuntimes(scope, isReset);
            _isAcquired = false;
        }

        public void Tick()
        {
            if (!_isAcquired)
                return;

            for (var i = 0; i < _orderedRuntimes.Count; i++)
                _orderedRuntimes[i].Tick();
        }

        public bool Contains(string tag)
        {
            return _runtimeByTag.ContainsKey(TransformChannelTagUtility.Normalize(tag));
        }

        public bool TryGetRuntime(string tag, out ITransformChannelRuntime? runtime)
        {
            runtime = null;
            var normalized = TransformChannelTagUtility.Normalize(tag);
            if (!_runtimeByTag.TryGetValue(normalized, out var resolved) || resolved == null)
                return false;

            runtime = resolved;
            return true;
        }

        public void GetTags(List<string> output)
        {
            if (output == null)
                return;

            output.Clear();
            for (var i = 0; i < _orderedRuntimes.Count; i++)
                output.Add(_orderedRuntimes[i].Tag);
        }

        public bool TryTeleportWorld(Vector3 worldPosition, bool resetVelocity = true)
        {
            if (!TryGetDefaultRuntime(out var runtime) || runtime == null)
                return false;

            return runtime.TryTeleportWorld(worldPosition, resetVelocity);
        }

        void RebuildRuntimes(IScopeNode scope, bool isReset)
        {
            ReleaseRuntimes(scope, isReset);

            var definitions = _mb.Channels;
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null)
                    continue;

                var tag = TransformChannelTagUtility.Normalize(definition.ChannelTag);
                if (_runtimeByTag.ContainsKey(tag))
                {
                    Debug.LogWarning($"[TransformChannelHub] Duplicate channel tag '{tag}' was skipped.");
                    continue;
                }

                var options = definition.CreateOptions(_mb.transform);
                var runtime = new TransformChannelRuntime(tag, options, _owner);
                runtime.OnAcquire(scope, isReset);
                _runtimeByTag.Add(tag, runtime);
                _orderedRuntimes.Add(runtime);
            }
        }

        void ReleaseRuntimes(IScopeNode scope, bool isReset)
        {
            for (var i = _orderedRuntimes.Count - 1; i >= 0; i--)
                _orderedRuntimes[i].OnRelease(scope, isReset);

            _orderedRuntimes.Clear();
            _runtimeByTag.Clear();
        }

        bool TryGetDefaultRuntime(out TransformChannelRuntime? runtime)
        {
            runtime = null;
            if (_runtimeByTag.TryGetValue(TransformChannelTagUtility.DefaultTag, out var byDefault) && byDefault != null)
            {
                runtime = byDefault;
                return true;
            }

            if (_orderedRuntimes.Count > 0)
            {
                runtime = _orderedRuntimes[0];
                return true;
            }

            return false;
        }
    }
}

#nullable enable
using System;
using System.Collections.Generic;
using Game;
using Game.Common;
using Game.Movement;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.TransformSystem
{
    internal sealed class TransformChannelRuntime : ITransformChannelRuntime, ITransformChannelRuntimeDebugTelemetry
    {
        const float Epsilon = 0.00001f;

        readonly string _tag;
        readonly TransformChannelOptions _options;
        readonly IScopeNode _owner;

        TransformControllerService? _service;
        TransformChannelOutputTarget _outputTarget;
        TransformChannelFeaturePreset _featurePreset = new();
        TransformChannelEffectPreset _effectPreset = new();

        Vector2 _lastAppliedGlobalBaseVelocity;
        Vector2 _lastAppliedGlobalVelocityDelta;
        Vector2 _lastAppliedGlobalVelocity;
        int _lastAppliedGlobalFrame = -1;

        readonly List<TransformManagerChannelApplyRequest> _globalApplyRequests = new();
        readonly List<TransformManagerMovementEntry> _movementContinuousEntries = new();
        readonly List<TransformManagerMovementEntry> _movementOneShotEntries = new();
        readonly List<TransformManagerRotateEntry> _rotateContinuousEntries = new();
        readonly List<TransformManagerRotateEntry> _rotateOneShotEntries = new();
        readonly List<TransformManagerScaleEntry> _scaleContinuousEntries = new();
        readonly List<TransformManagerScaleEntry> _scaleOneShotEntries = new();

        readonly Dictionary<string, ConditionEntryState> _movementConditionStates = new(StringComparer.Ordinal);
        readonly Dictionary<string, ConditionEntryState> _rotateConditionStates = new(StringComparer.Ordinal);
        readonly Dictionary<string, ConditionEntryState> _scaleConditionStates = new(StringComparer.Ordinal);

        readonly HashSet<string> _movementAppliedEntryIds = new(StringComparer.Ordinal);
        readonly HashSet<string> _rotateAppliedEntryIds = new(StringComparer.Ordinal);
        readonly HashSet<string> _scaleAppliedEntryIds = new(StringComparer.Ordinal);

        IDynamicContext? _conditionContext;
        IVarStore? _conditionVars;
        Action<int>? _conditionVarChangedHandler;

        float _appliedRotationOffset;
        bool _scaleStateInitialized;
        bool _lastScaleHadOverride;
        Vector3 _lastScaleOverrideBase = Vector3.one;
        Vector3 _lastScaleAffineAdd = Vector3.zero;
        Vector3 _lastScaleAffineMultiply = Vector3.one;

        sealed class ConditionEntryState
        {
            public int Version = int.MinValue;
            public bool AlwaysEvaluate;
            public bool IsDirty = true;
            public bool HasCachedValue;
            public bool CachedValue;
            public int[]? DependentVarIds;
        }

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
        public Quaternion CurrentWorldRotation => _service?.CurrentWorldRotation ?? Quaternion.identity;
        public TransformChannelFeaturePreset FeaturePreset => _featurePreset;
        public TransformChannelEffectPreset EffectPreset => _effectPreset;

        public bool EnableMovement => _featurePreset.EnableMovement;
        public bool EnableRotation => _featurePreset.EnableRotation;
        public bool EnableScale => _featurePreset.EnableScale;
        public IReadOnlyList<TransformManagerChannelApplyRequest> GlobalApplyRequests => _globalApplyRequests;
        public Vector2 RigidbodyLinearVelocity => _service?.Rigidbody2DLinearVelocity ?? Vector2.zero;
        public Vector2 RigidbodyOverlayVelocity => _service?.Rigidbody2DLinearVelocityOverlay ?? Vector2.zero;
        public Vector2 LastAppliedGlobalBaseVelocity => _lastAppliedGlobalBaseVelocity;
        public Vector2 LastAppliedGlobalVelocityDelta => _lastAppliedGlobalVelocityDelta;
        public Vector2 LastAppliedGlobalVelocity => _lastAppliedGlobalVelocity;
        public int LastAppliedGlobalFrame => _lastAppliedGlobalFrame;
        bool DebugGlobalApplyLogs => _options.DebugGlobalApplyLogs;

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (!ReferenceEquals(_owner, scope))
                return;

            _ = isReset;
            RebuildService(scope);
            SubscribeConditionVarChanges();
            _service?.OnAcquire(scope, isReset);

            if (DebugGlobalApplyLogs)
            {
                Debug.Log(
                    $"[TransformChannelHub][Init] frame={Time.frameCount} tag={_tag} owner={DescribeScope(_owner)} target={DescribeTransform(TargetTransform)} " +
                    $"conditionScope={DescribeScope(_conditionContext?.Scope)} output={_outputTarget} requests={_globalApplyRequests.Count} " +
                    $"movement={_featurePreset.EnableMovement} rotation={_featurePreset.EnableRotation} scale={_featurePreset.EnableScale}");
            }
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            if (!ReferenceEquals(_owner, scope))
                return;

            UnsubscribeConditionVarChanges();
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
            float angularVelocity,
            Rigidbody2DVelocityApplyMode linearVelocityMode = Rigidbody2DVelocityApplyMode.Override)
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
                angularVelocity,
                linearVelocityMode);
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

            _conditionContext = null;

            var resolver = scope.Resolver;
            if (resolver == null)
                return;

            var dynamicContext = CreateDynamicContext(scope);
            _conditionContext = dynamicContext;
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
            _conditionContext = CreateDynamicContext(ResolveConditionScope(config.TargetTransform, _owner));
        }

        void SubscribeConditionVarChanges()
        {
            UnsubscribeConditionVarChanges();

            var context = _conditionContext;
            if (context == null)
                return;

            var vars = context.Vars;
            _conditionVars = vars;
            if (vars == null)
                return;

            _conditionVarChangedHandler = HandleConditionVarChanged;
            vars.OnVarChanged += _conditionVarChangedHandler;
        }

        void UnsubscribeConditionVarChanges()
        {
            if (_conditionVars != null && _conditionVarChangedHandler != null)
                _conditionVars.OnVarChanged -= _conditionVarChangedHandler;

            _conditionVarChangedHandler = null;
            _conditionVars = null;
        }

        void HandleConditionVarChanged(int varId)
        {
            if (varId == 0)
                return;

            MarkConditionStatesDirty(_movementConditionStates, varId);
            MarkConditionStatesDirty(_rotateConditionStates, varId);
            MarkConditionStatesDirty(_scaleConditionStates, varId);
        }

        static void MarkConditionStatesDirty(Dictionary<string, ConditionEntryState> states, int varId)
        {
            foreach (var state in states.Values)
            {
                var dependencyVarIds = state.DependentVarIds;
                if (dependencyVarIds == null || dependencyVarIds.Length == 0)
                    continue;

                for (var i = 0; i < dependencyVarIds.Length; i++)
                {
                    if (dependencyVarIds[i] != varId)
                        continue;

                    state.IsDirty = true;
                    break;
                }
            }
        }

        void ApplyTransformManagerGlobals()
        {
            if (_service == null)
            {
                if (DebugGlobalApplyLogs)
                    Debug.LogWarning($"[TransformChannelHub][Global] frame={Time.frameCount} tag={_tag} skipped: service missing");

                return;
            }

            var target = _service.TargetTransform;
            if (target == null)
            {
                if (DebugGlobalApplyLogs)
                    Debug.LogWarning($"[TransformChannelHub][Global] frame={Time.frameCount} tag={_tag} skipped: target missing owner={DescribeScope(_owner)} output={_service.OutputTarget}");

                return;
            }

            if (!TryResolveTransformManagerService(out var manager) || manager == null)
            {
                if (DebugGlobalApplyLogs)
                {
                    Debug.LogWarning(
                        $"[TransformChannelHub][Global] frame={Time.frameCount} tag={_tag} manager=missing owner={DescribeScope(_owner)} target={DescribeTransform(target)} " +
                        $"conditionScope={DescribeScope(_conditionContext?.Scope)} output={_service.OutputTarget} requests={_globalApplyRequests.Count}");
                }

                RecordMovementTelemetry(_service.CurrentVelocity, _service.CurrentVelocity);
                ClearRigidbody2DMovementOverlay();
                RestoreRotationOffsetIfNeeded(target);
                RestoreScaleIfNeeded(target);
                return;
            }

            if (DebugGlobalApplyLogs)
            {
                Debug.Log(
                    $"[TransformChannelHub][Global] frame={Time.frameCount} tag={_tag} managerVersion={manager.RuntimeVersion} owner={DescribeScope(_owner)} " +
                    $"target={DescribeTransform(target)} conditionScope={DescribeScope(_conditionContext?.Scope)} output={_service.OutputTarget} requests={_globalApplyRequests.Count}");
            }

            var deltaTime = ResolveDeltaTime(_service.OutputTarget);

            if (_featurePreset.EnableMovement)
            {
                ApplyMovementGlobals(manager, target, deltaTime);
            }
            else
            {
                RecordMovementTelemetry(_service.CurrentVelocity, _service.CurrentVelocity);
                ClearRigidbody2DMovementOverlay();
            }

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
            var conditionContext = _conditionContext ?? CreateDynamicContext(_owner);
            var debugLogs = DebugGlobalApplyLogs;
            var debugHeader = debugLogs
                ? $"frame={Time.frameCount} tag={_tag} owner={DescribeScope(_owner)} target={DescribeTransform(target)} conditionScope={DescribeScope(conditionContext.Scope)} managerVersion={manager.RuntimeVersion} output={_service?.OutputTarget}"
                : string.Empty;
            var baseVelocity = _service?.CurrentVelocity ?? Vector2.zero;
            var composedVelocity = baseVelocity;
            var hasAnyCollectedEntries = false;

            _movementAppliedEntryIds.Clear();

            for (var i = 0; i < _globalApplyRequests.Count; i++)
            {
                var request = _globalApplyRequests[i];
                manager.CollectMovementEntries(request, _movementContinuousEntries, _movementOneShotEntries);

                if (_movementContinuousEntries.Count > 0 || _movementOneShotEntries.Count > 0)
                    hasAnyCollectedEntries = true;

                if (debugLogs)
                {
                    Debug.Log(
                        $"[TransformChannelHub][Movement] {debugHeader} request={DescribeRequest(request)} continuous={_movementContinuousEntries.Count} oneShot={_movementOneShotEntries.Count}");

                    if (_movementContinuousEntries.Count == 0 && _movementOneShotEntries.Count == 0)
                    {
                        Debug.LogWarning(
                            $"[TransformChannelHub][Movement] {debugHeader} request={DescribeRequest(request)} collected no movement entries.");
                    }
                }

                ApplyMovementEntries(manager, _movementContinuousEntries, ref composedVelocity, conditionContext, _movementConditionStates, _movementAppliedEntryIds, debugLogs, debugHeader, phase: "continuous", removeAfterApply: false);
                ApplyMovementEntries(manager, _movementOneShotEntries, ref composedVelocity, conditionContext, _movementConditionStates, _movementAppliedEntryIds, debugLogs, debugHeader, phase: "one-shot", removeAfterApply: true);
            }

            var velocityDelta = composedVelocity - baseVelocity;
            RecordMovementTelemetry(baseVelocity, composedVelocity);

            if (debugLogs)
            {
                Debug.Log(
                    $"[TransformChannelHub][Movement] {debugHeader} base={FormatVector2(baseVelocity)} composed={FormatVector2(composedVelocity)} delta={FormatVector2(velocityDelta)} " +
                    $"overlayBefore={FormatVector2(_service?.Rigidbody2DLinearVelocityOverlay ?? Vector2.zero)}");

                if (hasAnyCollectedEntries && velocityDelta.sqrMagnitude <= Epsilon)
                {
                    Debug.LogWarning(
                        $"[TransformChannelHub][Movement] {debugHeader} collected entries but final delta stayed zero.");
                }
            }

            if (_service != null && _service.OutputTarget == TransformOutputTarget.Rigidbody2D)
            {
                ApplyRigidbody2DMovementOverlay(velocityDelta);
                return;
            }

            if (velocityDelta.sqrMagnitude <= Epsilon)
                return;

            if (deltaTime <= 0f)
                return;

            target.position += new Vector3(velocityDelta.x, velocityDelta.y, 0f) * deltaTime;
        }

        void ClearRigidbody2DMovementOverlay()
        {
            ApplyRigidbody2DMovementOverlay(Vector2.zero);
        }

        void ApplyRigidbody2DMovementOverlay(Vector2 linearVelocity)
        {
            if (_service == null || _service.OutputTarget != TransformOutputTarget.Rigidbody2D)
                return;

            _service.TryApplyRigidbody2DSettings(
                applySimulated: false,
                simulated: false,
                applyGravityScale: false,
                gravityScale: 0f,
                applyFreezeRotation: false,
                freezeRotation: false,
                applyLinearVelocity: true,
                linearVelocity: linearVelocity,
                applyAngularVelocity: false,
                angularVelocity: 0f,
                linearVelocityMode: Rigidbody2DVelocityApplyMode.Overlay);
        }

        void ApplyRotateGlobals(ITransformManagerService manager, Transform target, float deltaTime)
        {
            var conditionContext = _conditionContext ?? CreateDynamicContext(_owner);
            var continuousOffset = 0f;
            var continuousAngularVelocity = _service?.CurrentAngularVelocity ?? 0f;
            var oneShotOffset = 0f;
            var oneShotAngularVelocity = 0f;

            _rotateAppliedEntryIds.Clear();

            for (var i = 0; i < _globalApplyRequests.Count; i++)
            {
                manager.CollectRotateEntries(_globalApplyRequests[i], _rotateContinuousEntries, _rotateOneShotEntries);
                ApplyRotateEntries(manager, _rotateContinuousEntries, ref continuousOffset, ref continuousAngularVelocity, conditionContext, _rotateConditionStates, _rotateAppliedEntryIds, removeAfterApply: false);

                var oneShotOffsetWork = continuousOffset;
                var oneShotAngularWork = continuousAngularVelocity;
                ApplyRotateEntries(manager, _rotateOneShotEntries, ref oneShotOffsetWork, ref oneShotAngularWork, conditionContext, _rotateConditionStates, _rotateAppliedEntryIds, removeAfterApply: true);

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
            var conditionContext = _conditionContext ?? CreateDynamicContext(_owner);
            var hasAnyScaleEntry = false;
            var hasOverride = false;
            var affineTrackable = true;
            var affineAdd = Vector3.zero;
            var affineMultiply = Vector3.one;

            var baseScale = RecoverScaleBase(target.localScale);
            var composedScale = baseScale;

            _scaleAppliedEntryIds.Clear();

            for (var i = 0; i < _globalApplyRequests.Count; i++)
            {
                manager.CollectScaleEntries(_globalApplyRequests[i], _scaleContinuousEntries, _scaleOneShotEntries);

                if (_scaleContinuousEntries.Count > 0 || _scaleOneShotEntries.Count > 0)
                    hasAnyScaleEntry = true;

                ApplyScaleEntries(manager, _scaleContinuousEntries, ref composedScale, ref hasOverride, ref affineTrackable, ref affineMultiply, ref affineAdd, conditionContext, _scaleConditionStates, _scaleAppliedEntryIds, removeAfterApply: false);
                ApplyScaleEntries(manager, _scaleOneShotEntries, ref composedScale, ref hasOverride, ref affineTrackable, ref affineMultiply, ref affineAdd, conditionContext, _scaleConditionStates, _scaleAppliedEntryIds, removeAfterApply: true);
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

            _lastAppliedGlobalBaseVelocity = Vector2.zero;
            _lastAppliedGlobalVelocityDelta = Vector2.zero;
            _lastAppliedGlobalVelocity = Vector2.zero;
            _lastAppliedGlobalFrame = -1;

            _movementAppliedEntryIds.Clear();
            _rotateAppliedEntryIds.Clear();
            _scaleAppliedEntryIds.Clear();

            _movementConditionStates.Clear();
            _rotateConditionStates.Clear();
            _scaleConditionStates.Clear();

            _movementContinuousEntries.Clear();
            _movementOneShotEntries.Clear();
            _rotateContinuousEntries.Clear();
            _rotateOneShotEntries.Clear();
            _scaleContinuousEntries.Clear();
            _scaleOneShotEntries.Clear();
        }

        void RecordMovementTelemetry(Vector2 baseVelocity, Vector2 composedVelocity)
        {
            _lastAppliedGlobalBaseVelocity = baseVelocity;
            _lastAppliedGlobalVelocity = composedVelocity;
            _lastAppliedGlobalVelocityDelta = composedVelocity - baseVelocity;
            _lastAppliedGlobalFrame = Time.frameCount;
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

        static void ApplyMovementEntries(
            ITransformManagerService manager,
            List<TransformManagerMovementEntry> entries,
            ref Vector2 currentVelocity,
            IDynamicContext context,
            Dictionary<string, ConditionEntryState> conditionStates,
            HashSet<string> appliedEntryIds,
            bool debugLogs,
            string debugHeader,
            string phase,
            bool removeAfterApply)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (!TryEvaluateCondition(entry.Settings, context, conditionStates, out var enabled))
                {
                    if (debugLogs)
                    {
                        Debug.LogWarning($"[TransformChannelHub][Movement] {debugHeader} phase={phase} entry={DescribeMovementEntry(entry)} condition=no-source scope={DescribeScope(context.Scope)}");
                    }

                    continue;
                }

                if (!enabled)
                {
                    if (debugLogs)
                    {
                        Debug.Log($"[TransformChannelHub][Movement] {debugHeader} phase={phase} entry={DescribeMovementEntry(entry)} condition=false scope={DescribeScope(context.Scope)}{DescribeConditionVariables(entry.Settings.Condition, context)}");
                    }

                    continue;
                }

                var settings = entry.Settings;
                if (!appliedEntryIds.Add(settings.EntryId))
                    continue;

                var beforeVelocity = currentVelocity;
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

                if (removeAfterApply)
                {
                    manager.RemoveMovement(settings.EntryId);
                    conditionStates.Remove(settings.EntryId);
                }

                if (debugLogs)
                {
                    Debug.Log(
                        $"[TransformChannelHub][Movement] {debugHeader} phase={phase} entry={DescribeMovementEntry(entry)} applied before={FormatVector2(beforeVelocity)} after={FormatVector2(currentVelocity)}");
                }
            }
        }

        static void ApplyRotateEntries(
            ITransformManagerService manager,
            List<TransformManagerRotateEntry> entries,
            ref float currentOffset,
            ref float currentAngularVelocity,
            IDynamicContext context,
            Dictionary<string, ConditionEntryState> conditionStates,
            HashSet<string> appliedEntryIds,
            bool removeAfterApply)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (!TryEvaluateCondition(entry.Settings, context, conditionStates, out var enabled) || !enabled)
                    continue;

                var settings = entry.Settings;
                if (!appliedEntryIds.Add(settings.EntryId))
                    continue;

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

                if (removeAfterApply)
                {
                    manager.RemoveRotate(settings.EntryId);
                    conditionStates.Remove(settings.EntryId);
                }
            }
        }

        static void ApplyScaleEntries(
            ITransformManagerService manager,
            List<TransformManagerScaleEntry> entries,
            ref Vector3 currentScale,
            ref bool hasOverride,
            ref bool affineTrackable,
            ref Vector3 affineMultiply,
            ref Vector3 affineAdd,
            IDynamicContext context,
            Dictionary<string, ConditionEntryState> conditionStates,
            HashSet<string> appliedEntryIds,
            bool removeAfterApply)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (!TryEvaluateCondition(entry.Settings, context, conditionStates, out var enabled) || !enabled)
                    continue;

                var settings = entry.Settings;
                if (!appliedEntryIds.Add(settings.EntryId))
                    continue;

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

                if (removeAfterApply)
                {
                    manager.RemoveScale(settings.EntryId);
                    conditionStates.Remove(settings.EntryId);
                }
            }
        }

        static bool TryEvaluateCondition(
            in TransformManagerEntrySettings settings,
            IDynamicContext context,
            Dictionary<string, ConditionEntryState> conditionStates,
            out bool enabled)
        {
            enabled = false;

            if (!settings.Condition.HasSource)
                return false;

            if (!conditionStates.TryGetValue(settings.EntryId, out var state))
            {
                state = new ConditionEntryState();
                conditionStates.Add(settings.EntryId, state);
            }

            if (state.Version != settings.Version)
            {
                state.Version = settings.Version;
                state.AlwaysEvaluate = false;
                state.IsDirty = true;
                state.HasCachedValue = false;
                state.CachedValue = false;
                state.DependentVarIds = ResolveConditionDependentVarIds(settings.Condition);

                if (state.DependentVarIds == null)
                    state.AlwaysEvaluate = true;
            }

            if (state.AlwaysEvaluate)
            {
                enabled = settings.Condition.EvaluateBool(context);
                return true;
            }

            if (state.HasCachedValue && !state.IsDirty)
            {
                enabled = state.CachedValue;
                return true;
            }

            enabled = settings.Condition.EvaluateBool(context);
            state.CachedValue = enabled;
            state.HasCachedValue = true;
            state.IsDirty = false;
            return true;
        }

        static int[]? ResolveConditionDependentVarIds(DynamicValue<bool> condition)
        {
            if (!condition.HasSource)
                return null;

            if (condition.TryGetSource<LiteralBoolSource>(out _))
                return Array.Empty<int>();

            if (condition.TryGetSource<SelfBlackboardSource>(out var selfBlackboard))
            {
                if (selfBlackboard.BlackboardVarId > 0)
                    return new[] { selfBlackboard.BlackboardVarId };

                return Array.Empty<int>();
            }

            if (condition.TryGetSource<BoolExpressionSource>(out var expressionSource) && expressionSource != null)
            {
                var collectedIds = new HashSet<int>();
                var sawVariable = false;

                if (!TryCollectConditionVariableDependencies(expressionSource.DebugExternalVariables, collectedIds, ref sawVariable))
                    return null;

                if (!TryCollectConditionVariableDependencies(expressionSource.DebugVariables, collectedIds, ref sawVariable))
                    return null;

                if (!sawVariable)
                    return Array.Empty<int>();

                if (collectedIds.Count == 0)
                    return Array.Empty<int>();

                var ids = new int[collectedIds.Count];
                collectedIds.CopyTo(ids);
                return ids;
            }

            var dependentKeys = condition.GetDependentKeys();
            if (dependentKeys != null)
                return ResolveDependentVarIds(dependentKeys);

            return null;
        }

        static bool TryCollectConditionVariableDependencies(IReadOnlyList<ExpressionVariable>? variables, HashSet<int> collectedIds, ref bool sawVariable)
        {
            if (variables == null)
                return true;

            for (var i = 0; i < variables.Count; i++)
            {
                var variable = variables[i];
                if (variable == null || !variable.HasSource)
                    continue;

                sawVariable = true;

                if (variable.Value.TryGetSource<LiteralSource>(out _))
                    continue;

                if (variable.Value.TryGetSource<SelfBlackboardSource>(out var selfBlackboard) && selfBlackboard != null)
                {
                    if (selfBlackboard.BlackboardVarId <= 0)
                        return false;

                    collectedIds.Add(selfBlackboard.BlackboardVarId);
                    continue;
                }

                return false;
            }

            return true;
        }

        static int[]? ResolveDependentVarIds(IReadOnlyList<string>? dependentKeys)
        {
            if (dependentKeys == null || dependentKeys.Count == 0)
                return Array.Empty<int>();

            var ids = new List<int>(dependentKeys.Count);
            for (var i = 0; i < dependentKeys.Count; i++)
            {
                var key = dependentKeys[i];
                if (string.IsNullOrEmpty(key))
                    continue;

                if (!VarIdResolver.TryResolve(key, out var varId) || varId == 0)
                    return null;

                ids.Add(varId);
            }

            return ids.Count > 0 ? ids.ToArray() : Array.Empty<int>();
        }

        internal static IDynamicContext CreateDynamicContext(IScopeNode scope)
        {
            var vars = ResolveVars(scope);
            return new SimpleDynamicContext(vars, scope);
        }

        static IScopeNode ResolveConditionScope(Transform? targetTransform, IScopeNode fallbackScope)
        {
            if (targetTransform != null)
            {
                for (var current = targetTransform; current != null; current = current.parent)
                {
                    if (current.TryGetComponent<RuntimeLifetimeScope>(out var runtimeScope) && runtimeScope != null)
                        return runtimeScope;

                    if (current.TryGetComponent<BaseLifetimeScope>(out var baseScope) && baseScope != null)
                        return baseScope;

                    var components = current.GetComponents<Component>();
                    for (var i = 0; i < components.Length; i++)
                    {
                        if (components[i] is IScopeNode node)
                            return node;
                    }
                }
            }

            return fallbackScope;
        }

        static IVarStore ResolveVars(IScopeNode scope)
        {
            var resolver = scope.Resolver;
            if (resolver != null && resolver.TryResolve<IBlackboardService>(out var blackboard) && blackboard?.LocalVars != null)
                return blackboard.LocalVars;

            return NullVarStore.Instance;
        }

        static string DescribeScope(IScopeNode? scope)
        {
            if (scope == null)
                return "(null)";

            var identity = scope.Identity?.SelfTransform;
            var scopeName = identity != null ? identity.name : "(no transform)";
            return $"{scope.Kind}:{scopeName}";
        }

        static string DescribeTransform(Transform? transform)
        {
            return transform != null ? transform.name : "(null)";
        }

        static string DescribeRequest(TransformManagerChannelApplyRequest request)
        {
            return request.HasChannelTagFilter ? $"Tag={request.ChannelTagFilter}" : "All";
        }

        static string DescribeMovementEntry(in TransformManagerMovementEntry entry)
        {
            var settings = entry.Settings;
            var channelTag = settings.ApplyAllChannels
                ? "All"
                : string.IsNullOrWhiteSpace(settings.ChannelTag)
                    ? "(empty)"
                    : settings.ChannelTag;

            return
                $"id={settings.EntryId} v={settings.Version} blend={settings.BlendMode} weight={settings.Weight:0.###} " +
                $"applyAll={settings.ApplyAllChannels} tag={channelTag} oneShot={settings.OneShot} duration={settings.DurationSeconds:0.###} " +
                $"condition={settings.Condition.SourceDebugData} velocity={FormatVector2(entry.Velocity)}";
        }

        static string DescribeConditionVariables(DynamicValue<bool> condition, IDynamicContext context)
        {
            if (!condition.TryGetSource<BoolExpressionSource>(out var expressionSource) || expressionSource == null)
                return string.Empty;

            var variables = expressionSource.DebugExternalVariables ?? expressionSource.DebugVariables;
            if (variables == null || variables.Count == 0)
                return " vars=(none)";

            var segments = new List<string>(variables.Count);
            for (var i = 0; i < variables.Count; i++)
            {
                var variable = variables[i];
                if (variable == null)
                    continue;

                var key = string.IsNullOrWhiteSpace(variable.ExpressionKey) ? "(empty)" : variable.ExpressionKey;
                var sourceType = variable.Value.SourceTypeName;
                var sourceDebug = string.IsNullOrWhiteSpace(variable.Value.SourceDebugData) ? "(empty)" : variable.Value.SourceDebugData;

                string evaluatedText;
                try
                {
                    evaluatedText = variable.Value.Evaluate(context).ToString();
                }
                catch (Exception ex)
                {
                    evaluatedText = $"<error:{ex.Message}>";
                }

                segments.Add($"{key}={evaluatedText}[{sourceType}:{sourceDebug}]");
            }

            return segments.Count > 0 ? " vars=" + string.Join("; ", segments) : " vars=(none)";
        }

        static string FormatVector2(Vector2 value)
        {
            return $"({value.x:0.###}, {value.y:0.###})";
        }

    }

    public sealed class TransformChannelHubService :
        ITransformChannelHubService,
        ITransformTeleportService,
        ITransformChannelPoseReader,
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        IScopeTickHandler
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

        internal bool DebugGlobalApplyLogs => _mb.DebugGlobalApplyLogs;

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

        public Quaternion CurrentWorldRotation
        {
            get
            {
                var target = TargetTransform;
                return target != null ? target.rotation : Quaternion.identity;
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
                options.DebugGlobalApplyLogs = _mb.DebugGlobalApplyLogs;
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

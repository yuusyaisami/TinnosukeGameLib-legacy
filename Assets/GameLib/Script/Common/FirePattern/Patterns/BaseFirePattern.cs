#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.DI;
using Game.Search;
using Game.Spawn;
using Game.Vars.Generated;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Fire
{
    [Serializable]
    public abstract class BaseFirePattern : IFirePattern
    {
        static readonly bool EnableDebugLogs = false;

        [Header("Identity")]
        [SerializeField] string _patternId = "";

        [Header("Fire Definition")]
        [SerializeReference, InlineProperty]
        [SerializeField] FireDefinition? _fireDefinition;

        [Header("Spawner Settings")]
        [SerializeField] SpawnerKind _spawnerKind = SpawnerKind.RuntimeEntity;
        [SerializeField] string _spawnerTag = "";

        [Header("Spawn Target")]
        [HideIf("@_spawnerKind == Game.Spawn.SpawnerKind.RuntimeEntity || _spawnerKind == Game.Spawn.SpawnerKind.RuntimeUIElement")]
        [SerializeField] GameObject? _defaultPrefab;

        [ShowIf("@_spawnerKind == Game.Spawn.SpawnerKind.RuntimeEntity || _spawnerKind == Game.Spawn.SpawnerKind.RuntimeUIElement")]
        [SerializeField, Tooltip("Template used for runtime entity or UI element spawning.")]
        DynamicValue<ParticleRuntimeTemplatePreset> _defaultTemplate;

        [ShowIf("@_spawnerKind == Game.Spawn.SpawnerKind.RuntimeEntity || _spawnerKind == Game.Spawn.SpawnerKind.RuntimeUIElement")]
        [SerializeField] RuntimeIdentityData? _identityData;

        [Header("Dynamic Data")]
        [SerializeField] DynamicValue<float> _speedMultiplier = DynamicValueExtensions.FromLiteral(1f);
        [SerializeField] DynamicValue<float> _rotationSpeed = DynamicValueExtensions.FromLiteral(0f);
        [SerializeField] DynamicValue<float> _delayTime = DynamicValueExtensions.FromLiteral(0f);
        [SerializeField] DynamicValue<float> _angleOffset = DynamicValueExtensions.FromLiteral(0f);
        [SerializeField] DynamicValue<float> _distanceOffset = DynamicValueExtensions.FromLiteral(0f);

        [Header("Direction Blend")]
        [SerializeField] DynamicValue<float> _targetBlendWeight = DynamicValueExtensions.FromLiteral(0f);

        [Header("Debug")]
        [SerializeField, ReadOnly, TextArea(6, 20)]
        string _variableBagKeys = "";

        [Header("Fire Emitter Repeat")]
        [MinValue(1)]
        [SerializeField]
        int _fireEmitRepeatCount = 1;

        [SerializeField]
        [Tooltip("true の場合、このパターンで生成されたユニットを全スポーン完了後に SelfDespawn 相当で自動破棄します。")]
        bool _autoDespawnSpawnedUnitsAfterComplete = true;

        public string PatternId => _patternId;
        public string DebugName => string.IsNullOrEmpty(_patternId) ? GetType().Name : _patternId;
        public SpawnerKind SpawnerKind => _spawnerKind;
        public string SpawnerTag => _spawnerTag ?? "";
        public FireDefinition? FireDefinition => _fireDefinition;

        public int EmitRepeatCount => Mathf.Max(1, _fireEmitRepeatCount);
        public bool AutoDespawnSpawnedUnitsAfterComplete => _autoDespawnSpawnedUnitsAfterComplete;

        protected bool IsRuntimeKind =>
            _spawnerKind == SpawnerKind.RuntimeEntity ||
            _spawnerKind == SpawnerKind.RuntimeUIElement;

        public virtual UniTask<FireContext[]> EvaluateAsync(
            IFirePatternService service,
            UnitSpawnContext inputContext,
            System.Collections.Generic.IReadOnlyList<DynamicSearchHit> targetHits,
            CancellationToken ct = default)
        {
            if (_fireDefinition == null)
                return UniTask.FromResult(Array.Empty<FireContext>());

            var ctx = CreateDynamicContext(inputContext);

            var origin = inputContext.Base.Position;
            var baseDirection = inputContext.Base.Data.Direction;
            if (baseDirection.sqrMagnitude <= 0.000001f)
                baseDirection = Vector3.up;
            else
                baseDirection = baseDirection.normalized;

            var points = _fireDefinition.Build(origin, baseDirection, EmitRepeatCount, targetHits, ctx);
            if (points == null || points.Length == 0)
                return UniTask.FromResult(Array.Empty<FireContext>());

            var results = new FireContext[points.Length];

            for (int i = 0; i < points.Length; i++)
            {
                ct.ThrowIfCancellationRequested();

                var point = points[i];
                if (ctx is FireDynamicContext fireCtx)
                    fireCtx.SetFromFirePoint(in point);

                var data = EvaluateFireData(in point, ctx);

                // If a burstInterval variable was provided by the FireDefinition (e.g. BurstFireDefinition),
                // apply it as an additional per-shot delay: Delay += burstInterval * index
                if (ctx is FireDynamicContext fd)
                {
                    var varId = VarIds.GameLib.SpawnPattern.FirePattern.BurstFire.burstInterval;
                    if (fd.Vars.TryGetVariant(varId, out var biVariant) &&
                        biVariant.TryGet(out float bi))
                    {
                        data.DelayTime += bi * point.Index;
                        if (EnableDebugLogs)
                            Debug.Log($"[BaseFirePattern] Applying burstInterval: index={point.Index} bi={bi} newDelay={data.DelayTime}");
                    }
                    else
                    {
                        if (EnableDebugLogs)
                            Debug.Log($"[BaseFirePattern] No burstInterval found for index={point.Index} (existing Delay={data.DelayTime})");
                    }
                }

                // Debug: log per-point base direction/angle for multi-shot patterns
                if (points.Length > 1)
                {
                    var a = Mathf.Atan2(point.BaseDirection.y, point.BaseDirection.x) * Mathf.Rad2Deg;
                    if (EnableDebugLogs)
                        Debug.Log($"[BaseFirePattern] point={point.Index} baseDir={point.BaseDirection} angle={a} delay={data.DelayTime}");
                }
                var finalDir = CalculateFinalDirection(in point, in data, ctx);
                var finalPos = CalculateFinalPosition(in point, in data, finalDir);
                var velocity = finalDir * data.SpeedMultiplier;

                results[i] = new FireContext(point, data, finalPos, finalDir, velocity, inputContext);
            }

            return UniTask.FromResult(results);
        }

        protected virtual IDynamicContext CreateDynamicContext(UnitSpawnContext inputContext)
        {
            return new FireDynamicContext(in inputContext);
        }

        protected virtual FireData EvaluateFireData(in FirePoint point, IDynamicContext ctx)
        {
            return new FireData
            {
                SpeedMultiplier = _speedMultiplier.Resolve(ctx),
                RotationSpeed = _rotationSpeed.Resolve(ctx),
                DelayTime = _delayTime.Resolve(ctx),
                AngleOffset = _angleOffset.Resolve(ctx),
                DistanceOffset = _distanceOffset.Resolve(ctx),
            };
        }

        protected virtual Vector3 CalculateFinalPosition(in FirePoint point, in FireData data, in Vector3 finalDirection)
        {
            return point.Position + finalDirection * data.DistanceOffset;
        }

        protected virtual Vector3 CalculateFinalDirection(in FirePoint point, in FireData data, IDynamicContext ctx)
        {
            float blendWeight = _targetBlendWeight.Resolve(ctx);

            var blended = (point.HasTarget && blendWeight > 0f)
                ? Vector3.Slerp(point.BaseDirection, point.TargetDirection, blendWeight)
                : point.BaseDirection;

            if (Mathf.Abs(data.AngleOffset) > 0.001f)
                blended = Quaternion.Euler(0, 0, data.AngleOffset) * blended;

            if (blended.sqrMagnitude <= 0.000001f)
                return Vector3.up;

            return blended.normalized;
        }

        public SpawnParams BuildSpawnParams(in FireContext context)
        {
            var rot = Quaternion.LookRotation(Vector3.forward, context.FinalDirection);

            if (IsRuntimeKind)
            {
                var dynCtx = CreateDynamicContext(context.InputContext);
                if (dynCtx is FireDynamicContext fireCtx)
                    fireCtx.SetFromFirePoint(in context.Point);

                if (!_defaultTemplate.TryGet(dynCtx, out var templatePreset) || templatePreset == null)
                    return SpawnParams.Default;

                var runtimeTemplate = RuntimeTemplatePresetResolver.ResolveTemplateSO(templatePreset);
                if (runtimeTemplate == null)
                    return SpawnParams.Default;

                return SpawnParams.ForRuntime(
                    runtimeTemplate,
                    context.FinalPosition,
                    rot,
                    Vector3.one,
                    _identityData);
            }

            if (_defaultPrefab == null)
                return SpawnParams.Default;



            return new SpawnParams
            {
                Prefab = _defaultPrefab,
                Position = context.FinalPosition,
                Rotation = rot,
                Scale = Vector3.one,
                TransformParent = null,
                LifetimeScopeParent = null,
                WorldSpace = true,
                AllowPooling = true,
                Template = null,
                Identity = null
            };
        }

        [OnInspectorInit]
        void SyncContextKeys()
        {
            _variableBagKeys = FireDynamicContext.ContextKeysText;
        }
    }
}

#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.DI;
using Game.Fire;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Spawn
{
    /// <summary>
    /// 逕滓・繝代ち繝ｼ繝ｳ縺ｮ蝓ｺ蠎輔け繝ｩ繧ｹ縲・
    /// </summary>
    [Serializable]
    public abstract class BaseSpawnPattern : ISpawnPattern
    {
        // ================================================================================
        // Identity
        // ================================================================================

        [Header("Identity")]
        [SerializeField] string _patternId = "";

        // ================================================================================
        // Spawner Settings
        // ================================================================================

        [Header("Spawner Settings")]
        [SerializeField] SpawnerKind _spawnerKind = SpawnerKind.RuntimeEntity;

        [SerializeField] string _spawnerTag = "";

        // ================================================================================
        // Spawn Target
        // ================================================================================

        [Header("Spawn Target")]
        [HideIf("@_spawnerKind == Game.Spawn.SpawnerKind.RuntimeEntity || _spawnerKind == Game.Spawn.SpawnerKind.RuntimeUIElement")]
        [SerializeField] GameObject? _prefab;

        [ShowIf("@_spawnerKind == Game.Spawn.SpawnerKind.RuntimeEntity || _spawnerKind == Game.Spawn.SpawnerKind.RuntimeUIElement")]
        [SerializeField] DynamicValue<FirePatternRuntimeTemplatePreset> _template;

        [ShowIf("@_spawnerKind == Game.Spawn.SpawnerKind.RuntimeEntity || _spawnerKind == Game.Spawn.SpawnerKind.RuntimeUIElement")]
        [SerializeField] RuntimeIdentityData? _identityData;

        // ================================================================================
        // Line Definition
        // ================================================================================

        [Header("Line Definition")]
        [SerializeReference, InlineProperty]
        [SerializeField] SpawnLineDefinition? _lineDefinition;

        // ================================================================================
        // Emitter Repeat (NEW)
        // ================================================================================

        [Header("Emitter Repeat")]
        [MinValue(1)]
        [SerializeField] int _emitRepeatCount = 1;

        [SerializeField]
        [Tooltip("Inspector setting.")]
        bool _autoDespawnSpawnedUnitsAfterComplete = true;

        // ================================================================================
        // Spawn Count (Layer 1)
        // ================================================================================

        [Header("Spawn Count")]
        [MinValue(1)]
        [SerializeField] int _spawnCount = 1;

        // ================================================================================
        // Dynamic Data (Layer 2)
        // ================================================================================

        [Header("Dynamic Data")]
        [SerializeField] DynamicValue<float> _direction = DynamicValueExtensions.FromLiteral(0f);
        [SerializeField] DynamicValue<float> _speed = DynamicValueExtensions.FromLiteral(0f);
        [SerializeField] DynamicValue<float> _delayTime = DynamicValueExtensions.FromLiteral(0f);

        [Header("Random Offset (TangentDirection Base)")]
        [SerializeField] DynamicValue<float> _randomVertical = DynamicValueExtensions.FromLiteral(0f);
        [SerializeField] DynamicValue<float> _randomHorizontal = DynamicValueExtensions.FromLiteral(0f);

        [Header("Debug")]
        [SerializeField, ReadOnly, TextArea(6, 20)]
        string _variableBagKeys = "";

        public string PatternId => _patternId;
        public SpawnerKind SpawnerKind => _spawnerKind;
        public string SpawnerTag => _spawnerTag ?? "";

        public int EmitRepeatCount => Mathf.Max(1, _emitRepeatCount);
        public int SpawnCount => Mathf.Max(1, _spawnCount);
        public bool AutoDespawnSpawnedUnitsAfterComplete => _autoDespawnSpawnedUnitsAfterComplete;

        protected bool IsRuntimeKind => _spawnerKind == SpawnerKind.RuntimeEntity || _spawnerKind == SpawnerKind.RuntimeUIElement;

        protected GameObject? Prefab => IsRuntimeKind ? null : _prefab;
        protected DynamicValue<FirePatternRuntimeTemplatePreset> Template => _template;
        protected RuntimeIdentityData? IdentityData => IsRuntimeKind ? _identityData : null;
        protected SpawnLineDefinition? LineDefinition => _lineDefinition;


        // ================================================================================
        // ISpawnPattern
        // ================================================================================

        public virtual UniTask<SpawnContext[]> EvaluateAsync(IEmitterService emitter, CancellationToken ct = default)
        {
            var emitCount = EmitRepeatCount;

            if (_lineDefinition == null)
                return UniTask.FromResult(Array.Empty<SpawnContext>());

            // 莠句燕縺ｫ繝昴う繝ｳ繝域焚繧呈耳貂ｬ縺ｧ縺阪↑縺・・縺ｧ List 縺ｧ縺ｾ縺ｨ繧√ｋ
            var all = new System.Collections.Generic.List<SpawnContext>(64);

            for (int emitIndex = 0; emitIndex < emitCount; emitIndex++)
            {
                ct.ThrowIfCancellationRequested();
                EvaluateOnce(emitter, emitIndex, emitCount, all);
            }

            return UniTask.FromResult(all.ToArray());
        }

        // ================================================================================
        // Core
        // ================================================================================

        void EvaluateOnce(IEmitterService emitter, int emitIndex, int emitCount, System.Collections.Generic.List<SpawnContext> output)
        {
            var ctx = new SpawnDynamicContext(emitter.OwnerScope);
            ctx.SetEmitterInfo(emitter.Origin, emitter.Rotation, emitIndex, emitCount);

            var line = _lineDefinition!.Build(ctx);
            if (line.Points == null || line.Points.Length == 0)
                return;

            var worldPoints = new Vector3[line.Points.Length];
            for (int i = 0; i < worldPoints.Length; i++)
                worldPoints[i] = emitter.Origin + (emitter.Rotation * line.Points[i]);

            EnsureNormalized(ref line);

            for (int i = 0; i < worldPoints.Length; i++)
            {
                var pos = worldPoints[i];
                var delta = pos - emitter.Origin;
                var dist = delta.magnitude;
                var dir = dist > 0f ? (delta / dist) : Vector3.right;

                var tangent = CalculateTangent(worldPoints, i);

                var point = new SpawnPoint(
                    index: i,
                    position: pos,
                    directionFromOrigin: dir,
                    distanceFromOrigin: dist,
                    tangentDirection: tangent,
                    normalizedPosition: line.NormalizedPositions[i],
                    spawnCount: SpawnCount,
                    emitCount: emitCount);

                ctx.SetFromSpawnPoint(in point);
                var data = EvaluateSpawnData(in point, ctx);

                var spawnParams = BuildSpawnParams(pos, emitter.Rotation, ctx);

                output.Add(new SpawnContext(
                    index: point.Index,
                    position: point.Position,
                    emitterPosition: emitter.Origin,
                    directionFromEmitter: point.DirectionFromOrigin,
                    distanceFromEmitter: point.DistanceFromOrigin,
                    tangentDirection: point.TangentDirection,
                    spawnCount: point.SpawnCount,

                    data: data,
                    spawnParams: spawnParams,
                    emitIndex: emitIndex,
                    emitCount: emitCount));
            }
        }

        static void EnsureNormalized(ref SpawnLine line)
        {
            if (line.NormalizedPositions == null || line.NormalizedPositions.Length != line.Points.Length)
            {
                // 繝輔か繝ｼ繝ｫ繝舌ャ繧ｯ: 0..1 繧堤ｭ蛾俣髫・
                int n = line.Points.Length;
                var norm = new float[n];
                for (int i = 0; i < n; i++)
                    norm[i] = n <= 1 ? 0f : (i / (n - 1f));
                line.NormalizedPositions = norm;
            }
        }

        static Vector3 CalculateTangent(Vector3[] pts, int i)
        {
            if (pts.Length <= 1)
                return Vector3.right;

            Vector3 a;
            Vector3 b;

            if (i <= 0)
            {
                a = pts[0];
                b = pts[1];
            }
            else if (i >= pts.Length - 1)
            {
                a = pts[pts.Length - 2];
                b = pts[pts.Length - 1];
            }
            else
            {
                a = pts[i - 1];
                b = pts[i + 1];
            }

            var d = b - a;
            var mag = d.magnitude;
            return mag > 0f ? (d / mag) : Vector3.right;
        }

        protected SpawnData EvaluateSpawnData(in SpawnPoint point, IDynamicContext ctx)
        {
            return new SpawnData
            {
                Direction = CalculateDirection(in point, ctx),
                Speed = _speed.Resolve(ctx),
                DelayTime = _delayTime.Resolve(ctx),
                RandomVerticalOffset = _randomVertical.Resolve(ctx),
                RandomHorizontalOffset = _randomHorizontal.Resolve(ctx),
            };
        }

        protected virtual Vector3 CalculateDirection(in SpawnPoint point, IDynamicContext ctx)
        {
            float offsetDegrees = _direction.Resolve(ctx);
            return Quaternion.Euler(0, 0, offsetDegrees) * point.DirectionFromOrigin;
        }

        [OnInspectorInit]
        void SyncContextKeys()
        {
            _variableBagKeys = SpawnDynamicContext.ContextKeysText;
        }

        protected SpawnParams BuildSpawnParams(Vector3 position, Quaternion rotation, IDynamicContext ctx, Vector3 scale = default)
        {
            if (scale == default) scale = Vector3.one;

            if (IsRuntimeKind)
            {
                if (!_template.TryGet(ctx, out var templatePreset) || templatePreset == null)
                    return SpawnParams.Default;

                var runtimeTemplate = RuntimeTemplatePresetResolver.ResolveTemplateSO(templatePreset);
                if (runtimeTemplate == null)
                    return SpawnParams.Default;

                return SpawnParams.ForRuntime(
                    runtimeTemplate,
                    position,
                    rotation,
                    scale,
                    _identityData);
            }

            if (_prefab == null)
                return SpawnParams.Default;

            return new SpawnParams
            {
                Prefab = _prefab,
                Position = position,
                Rotation = rotation,
                Scale = scale,
                TransformParent = null,
                LifetimeScopeParent = null,
                WorldSpace = true,
                AllowPooling = true,
                Template = null,
                Identity = null
            };
        }
    }

}

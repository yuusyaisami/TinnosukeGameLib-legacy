#nullable enable
using System;
using Game.Common;
using Game.Spawn;
using Game.DI;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class SpawnParticleCommandData : ICommandData
    {
        public enum EmitterParentMode
        {
            SpawnerDefault = 0,
            CommandRunner = 1,
            Specify = 2,
        }

        public int CommandId => CommandIds.SpawnParticle;
        public string DebugData
        {
            get
            {
                var emitterName = CommandDebugDataHelper.GetDynamicDebugData(EmitterTemplate, "null");
                var patterns = Patterns?.Length ?? 0;
                var mode = patterns > 0 ? $"Patterns={patterns}" : $"Direct={DirectSpawnCount}";
                return $"Emitter={emitterName} {mode}";
            }
        }

        bool ShowDirectSpawnFields => Patterns == null || Patterns.Length == 0;
        bool ShowEmitterSpawnOptions => SpawnEmitterIfMissing;

        [BoxGroup("Emitter")]
        [SerializeField]
        [Tooltip("Inspector setting.")]
        public DynamicValue<SpawnPatternRuntimeTemplatePreset> EmitterTemplate;

        [SerializeField]
        [Tooltip("Inspector setting.")]
        [ShowIf(nameof(ShowDirectSpawnFields))]
        public DynamicValue<FirePatternRuntimeTemplatePreset> FireTemplate;

        [BoxGroup("Emitter")]
        [SerializeField]
        [Tooltip("Inspector setting.")]
        public bool UseExistingEmitterIfPresent;

        [BoxGroup("Emitter")]
        [SerializeField]
        [Tooltip("Inspector setting.")]
        public bool SpawnEmitterIfMissing = true;

        [BoxGroup("Emitter")]
        [SerializeField]
        [ShowIf(nameof(ShowEmitterSpawnOptions))]
        [LabelText("Emitter Parent")]
        public EmitterParentMode EmitterParent = EmitterParentMode.CommandRunner;

        [BoxGroup("Emitter")]
        [SerializeField]
        [ShowIf(nameof(ShowEmitterParentPicker))]
        [LabelText("Parent Transform")]
        public DynamicValue<Transform> EmitterParentTransform;

        [BoxGroup("Emitter")]
        [SerializeField]
        [ShowIf(nameof(ShowEmitterSpawnOptions))]
        [Tooltip("Inspector setting.")]
        public bool ReleaseSpawnedEmitterAfter = true;

        [BoxGroup("Emitter")]
        [SerializeField]
        [ShowIf(nameof(ShowEmitterSpawnOptions))]
        [Tooltip("Inspector setting.")]
        public DynamicValue<Vector3> EmitterSpawnPosition;

        [BoxGroup("Emitter")]
        [SerializeField]
        [ShowIf(nameof(ShowEmitterSpawnOptions))]
        [Tooltip("Inspector setting.")]
        public DynamicValue<Vector3> EmitterSpawnOffset;

        [BoxGroup("Emitter")]
        [SerializeField]
        [ShowIf(nameof(ShowEmitterSpawnOptions))]
        [Tooltip("Inspector setting.")]
        public DynamicValue<Vector3> EmitterSpawnRotationEuler = DynamicValueExtensions.FromLiteral(Vector3.zero);

        [SerializeField, HideInInspector, FormerlySerializedAs("EmitterSpawnLocalOffset")]
        Vector3 _legacyEmitterSpawnOffset = Vector3.zero;

        [SerializeField, HideInInspector, FormerlySerializedAs("EmitterSpawnRotationEulerOffset")]
        Vector3 _legacyEmitterSpawnRotationEulerOffset = Vector3.zero;

        [BoxGroup("Mode")]
        [LabelText("Await Mode")]
        [Tooltip("Inspector setting.")]
        public FlowRunAwaitMode AwaitMode = FlowRunAwaitMode.RunInBackground;

        [BoxGroup("Mode")]
        [SerializeReference, ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        [Tooltip("Inspector setting.")]
        public BaseSpawnPattern[] Patterns = Array.Empty<BaseSpawnPattern>();

        [BoxGroup("Mode")]
        [SerializeField]
        [ShowIf(nameof(ShowDirectSpawnFields))]
        [Tooltip("Inspector setting.")]
        public int DirectSpawnCount = 1;

        [BoxGroup("Mode")]
        [SerializeField]
        [ShowIf(nameof(ShowDirectSpawnFields))]
        [Tooltip("Inspector setting.")]
        public Vector3 DirectSpawnLocalOffset = Vector3.zero;

        [BoxGroup("Mode")]
        [SerializeField]
        [ShowIf(nameof(ShowDirectSpawnFields))]
        [Tooltip("Inspector setting.")]
        public bool DirectSpawnUseEmitterRotation = true;

        [BoxGroup("Mode")]
        [SerializeField]
        [ShowIf(nameof(ShowDirectSpawnFields))]
        [Tooltip("Inspector setting.")]
        public Vector3 DirectSpawnRotationEulerOffset = Vector3.zero;

        bool ShowEmitterParentPicker => ShowEmitterSpawnOptions && EmitterParent == EmitterParentMode.Specify;

        public SpawnParticleCommandData()
        {
            // 蠕捺擂縺ｮ縲後さ繝槭Φ繝牙ｮ溯｡・actor 縺ｮ菴咲ｽｮ蝓ｺ貅悶阪↓霑代＞譌｢螳壼､繧堤ｶｭ謖・
            // ・亥ｿ・ｦ√↑繧・Inspector 縺ｧ莉ｻ諢上・ DynamicSource 縺ｫ蟾ｮ縺玲崛縺亥庄閭ｽ・・
            EmitterSpawnPosition = DynamicValue<Vector3>.FromSource(new ActorWorldPosition3Source());
            EmitterSpawnOffset = DynamicValueExtensions.FromLiteral(Vector3.zero);
        }

        public Vector3 LegacyEmitterSpawnOffset => _legacyEmitterSpawnOffset;
        public Vector3 LegacyEmitterSpawnRotationEulerOffset => _legacyEmitterSpawnRotationEulerOffset;

    }
}

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
        [Tooltip("パーティクルのスポーンに使用されるエミッターテンプレート。エミッターのスポーンまたは検索に使用されます。")]
        public DynamicValue<SpawnPatternRuntimeTemplatePreset> EmitterTemplate;

        [SerializeField]
        [Tooltip("直接スポーン時に使用されるファイアテンプレート（Patternsが空の場合に使用）。")]
        [ShowIf(nameof(ShowDirectSpawnFields))]
        public DynamicValue<FirePatternRuntimeTemplatePreset> FireTemplate;

        [BoxGroup("Emitter")]
        [SerializeField]
        [Tooltip("trueの場合、新しいエミッターサービスを生成する前に、スコープ内で既存のエミッターサービスを再利用しようと試みます。")]
        public bool UseExistingEmitterIfPresent;

        [BoxGroup("Emitter")]
        [SerializeField]
        [Tooltip("trueの場合、エミッターが見つからない場合に、設定されたテンプレートからエミッターを生成します。")]
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
        [Tooltip("trueの場合、コマンドの実行完了後に、このコマンドで生成されたエミッターを解放します。")]
        public bool ReleaseSpawnedEmitterAfter = true;

        [BoxGroup("Emitter")]
        [SerializeField]
        [ShowIf(nameof(ShowEmitterSpawnOptions))]
        [Tooltip("エミッター生成の基準ワールド座標。SpawnRuntimeTemplateCommand と同様に DynamicValue で評価されます。")]
        public DynamicValue<Vector3> EmitterSpawnPosition;

        [BoxGroup("Emitter")]
        [SerializeField]
        [ShowIf(nameof(ShowEmitterSpawnOptions))]
        [Tooltip("基準座標に加算されるワールド位置オフセット。SpawnRuntimeTemplateCommand の Offset と同じ扱いです。")]
        public DynamicValue<Vector3> EmitterSpawnOffset;

        [BoxGroup("Emitter")]
        [SerializeField]
        [ShowIf(nameof(ShowEmitterSpawnOptions))]
        [Tooltip("エミッター生成に使用するワールド回転（Euler, degree）。SpawnRuntimeTemplateCommand の RotationEuler と同じ扱いです。")]
        public DynamicValue<Vector3> EmitterSpawnRotationEuler = DynamicValueExtensions.FromLiteral(Vector3.zero);

        [SerializeField, HideInInspector, FormerlySerializedAs("EmitterSpawnLocalOffset")]
        Vector3 _legacyEmitterSpawnOffset = Vector3.zero;

        [SerializeField, HideInInspector, FormerlySerializedAs("EmitterSpawnRotationEulerOffset")]
        Vector3 _legacyEmitterSpawnRotationEulerOffset = Vector3.zero;

        [BoxGroup("Mode")]
        [LabelText("Await Mode")]
        [Tooltip("コマンド完了を待機するかを指定します。RunInBackground で待機しません。")]
        public FlowRunAwaitMode AwaitMode = FlowRunAwaitMode.RunInBackground;

        [BoxGroup("Mode")]
        [SerializeReference, ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        [Tooltip("空でない場合、コマンドはエミッター上でこれらのスポーンパターンを実行します。")]
        public BaseSpawnPattern[] Patterns = Array.Empty<BaseSpawnPattern>();

        [BoxGroup("Mode")]
        [SerializeField]
        [ShowIf(nameof(ShowDirectSpawnFields))]
        [Tooltip("Patternsが空の場合に使用される直接スポーン数。")]
        public int DirectSpawnCount = 1;

        [BoxGroup("Mode")]
        [SerializeField]
        [ShowIf(nameof(ShowDirectSpawnFields))]
        [Tooltip("エミッターのローカル空間での直接スポーンオフセット。")]
        public Vector3 DirectSpawnLocalOffset = Vector3.zero;

        [BoxGroup("Mode")]
        [SerializeField]
        [ShowIf(nameof(ShowDirectSpawnFields))]
        [Tooltip("trueの場合、直接スポーンはエミッターの回転を基準回転として使用します。")]
        public bool DirectSpawnUseEmitterRotation = true;

        [BoxGroup("Mode")]
        [SerializeField]
        [ShowIf(nameof(ShowDirectSpawnFields))]
        [Tooltip("直接スポーンの回転オフセット（オイラー角、度単位）。")]
        public Vector3 DirectSpawnRotationEulerOffset = Vector3.zero;

        bool ShowEmitterParentPicker => ShowEmitterSpawnOptions && EmitterParent == EmitterParentMode.Specify;

        public SpawnParticleCommandData()
        {
            // 従来の「コマンド実行 actor の位置基準」に近い既定値を維持
            // （必要なら Inspector で任意の DynamicSource に差し替え可能）
            EmitterSpawnPosition = DynamicValue<Vector3>.FromSource(new ActorWorldPosition3Source());
            EmitterSpawnOffset = DynamicValueExtensions.FromLiteral(Vector3.zero);
        }

        public Vector3 LegacyEmitterSpawnOffset => _legacyEmitterSpawnOffset;
        public Vector3 LegacyEmitterSpawnRotationEulerOffset => _legacyEmitterSpawnRotationEulerOffset;

    }
}

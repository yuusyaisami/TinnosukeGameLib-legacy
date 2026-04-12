#nullable enable

using System;
using Game.DI;
using Game.Common;
using Game.Spawn;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum SpawnTransformParentPolicy
    {
        SpawnerRoot = 0,
        UseTransform = 1,
        ActorSource = 2,
    }

    [Serializable]
    public sealed class SpawnRuntimeTemplateCommandData : ICommandData
    {
        public int CommandId => CommandIds.SpawnRuntimeTemplate;
        public string DebugData
        {
            get
            {
                var templateName = CommandDebugDataHelper.GetDynamicDebugData(Template, "null");
                var tag = string.IsNullOrEmpty(SpawnerTag) ? "<none>" : SpawnerTag;
                var contextLabel = WriteSpawnedScopeToContext ? $" Ctx={SpawnedScopeSlot}" : string.Empty;
                var sourceLabel = WriteSpawnerToContext ? $" Src={SpawnerContextSlot}" : string.Empty;
                var hiddenLabel = UseHiddenPreSpawn
                    ? $" HiddenPreSpawn=on Offset={CommandDebugDataHelper.GetDynamicDebugData(HiddenSpawnOffset, "(far)")} Delay={CommandDebugDataHelper.GetDynamicDebugData(RevealDelayFrames, "1")}" 
                    : string.Empty;
                return $"Template={templateName} Spawner={SpawnerKind} Tag={tag}{contextLabel}{sourceLabel}{hiddenLabel}";
            }
        }

        [Header("Template")]
        [SerializeField, Required]
        [LabelText("Template")]
        [PropertyTooltip("生成する RuntimeTemplate preset です。実行時に RuntimeTemplateSO へ解決されます。")]
        public DynamicValue<BaseRuntimeTemplatePreset> Template;

        [Header("Spawner")]
        [SerializeField]
        [EnumToggleButtons]
        [LabelText("Spawner Kind")]
        [PropertyTooltip("どの種類の spawner を使って生成するかを指定します。通常 UI は RuntimeUIElement、ワールドは RuntimeEntity です。")]
        public SpawnerKind SpawnerKind = SpawnerKind.RuntimeEntity;

        [SerializeField]
        [LabelText("Spawner Tag")]
        [PropertyTooltip("spawner 解決に使う tag です。空なら tag fallback を許可します。")]
        public string SpawnerTag = "";

        [Header("Transform")]
        [SerializeField]
        [LabelText("World Space")]
        [PropertyTooltip("true のとき worldSpace spawn を行います。false のとき parent ローカル基準で生成します。")]
        public bool WorldSpace = true;

        [SerializeField]
        [LabelText("Position")]
        [PropertyTooltip("生成基準座標です。")]
        public DynamicValue<Vector3> Position;

        [SerializeField]
        [LabelText("Offset")]
        [PropertyTooltip("Position に加算する追加オフセットです。")]
        public DynamicValue<Vector3> Offset;

        [SerializeField]
        [LabelText("Rotation Euler")]
        [PropertyTooltip("生成時の回転です。Euler degree で指定します。")]
        public DynamicValue<Vector3> RotationEuler = DynamicValueExtensions.FromLiteral(Vector3.zero);

        [SerializeField]
        [LabelText("Scale")]
        [PropertyTooltip("生成時のローカルスケールです。")]
        public DynamicValue<Vector3> Scale = DynamicValueExtensions.FromLiteral(Vector3.one);

        [Header("Pre Spawn")]
        [SerializeField]
        [LabelText("Use Hidden Pre Spawn")]
        [PropertyTooltip("true のとき、まず十分に遠い場所へ spawn してから指定フレーム後に Position へ戻します。RuntimeLTS の初期描画崩れ対策です。")]
        public bool UseHiddenPreSpawn = false;

        [SerializeField, ShowIf(nameof(UseHiddenPreSpawn))]
        [LabelText("Hidden Spawn Offset")]
        [PropertyTooltip("Hidden Pre Spawn 時に final Position へ足すオフセットです。十分に遠い値を指定してください。")]
        public DynamicValue<Vector3> HiddenSpawnOffset = DynamicValueExtensions.FromLiteral(new Vector3(100000f, 100000f, 100000f));

        [SerializeField, ShowIf(nameof(UseHiddenPreSpawn))]
        [MinValue(0)]
        [LabelText("Reveal Delay Frames")]
        [PropertyTooltip("Hidden で spawn してから final Position に戻すまでのフレーム数です。1 か 2 を推奨します。")]
        public DynamicValue<int> RevealDelayFrames = DynamicValueExtensions.FromLiteral(1);

        [Header("Count")]
        [SerializeField]
        [MinValue(1)]
        [LabelText("Count")]
        [PropertyTooltip("何体生成するかを指定します。")]
        public DynamicValue<int> Count = DynamicValueExtensions.FromLiteral(1);

        [Header("Delay Between Spawns")]
        [SerializeField]
        [LabelText("Delay Seconds")]
        [PropertyTooltip("複数生成時、各 spawn の間に待つ秒数です。0 で連続生成します。")]
        public DynamicValue<float> DelayBetweenSpawns = DynamicValueExtensions.FromLiteral(0f);

        [Header("Parent")]
        [SerializeField]
        [EnumToggleButtons]
        [LabelText("Transform Parent Policy")]
        [PropertyTooltip("生成物の transform parent をどの方法で決めるかを指定します。")]
        public SpawnTransformParentPolicy TransformParentPolicy = SpawnTransformParentPolicy.SpawnerRoot;

        [SerializeField, ShowIf(nameof(ShowTransformParent))]
        [LabelText("Transform Parent")]
        [PropertyTooltip("TransformParentPolicy=UseTransform のときに使う親 Transform です。")]
        public Transform? TransformParent;

        [SerializeField, ShowIf(nameof(ShowTransformParentActorSource))]
        [InlineProperty]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Transform Parent\", TransformParentActorSource)")]
        [PropertyTooltip("TransformParentPolicy=ActorSource のときに使う親 ActorSource です。")]
        public ActorSource TransformParentActorSource;

        [Header("DI Parent (optional)")]
        [SerializeField]
        [LabelText("Override LifetimeScope Parent")]
        [PropertyTooltip("true のとき、生成 Runtime の LifetimeScope 親を ActorSource から上書きします。")]
        public bool OverrideLifetimeScopeParent = false;

        [SerializeField, ShowIf(nameof(OverrideLifetimeScopeParent))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"DI Parent\", LifetimeScopeParent)")]
        [PropertyTooltip("OverrideLifetimeScopeParent=true のときに使う DI 親です。")]
        public ActorSource LifetimeScopeParent;

        [Header("Pooling")]
        [SerializeField]
        [LabelText("Allow Pooling")]
        [PropertyTooltip("true のとき template 側の pool 設定を使って再利用を許可します。")]
        public bool AllowPooling = true;

        [Header("Context")]
        [SerializeField]
        [LabelText("Write Spawned Scope To Context")]
        [PropertyTooltip("生成された spawned scope を ContextA-D slot に書き込みます。")]
        public bool WriteSpawnedScopeToContext = false;

        [SerializeField, ShowIf(nameof(WriteSpawnedScopeToContext))]
        [LabelText("Spawned Scope Slot")]
        [PropertyTooltip("spawned scope を書き込む ContextA-D slot です。")]
        public CommandLtsSlot SpawnedScopeSlot = CommandLtsSlot.ContextA;

        [SerializeField]
        [LabelText("Write Spawner To Context")]
        [PropertyTooltip("spawn を実行した側の scope を ContextA-D slot に書き込みます。")]
        public bool WriteSpawnerToContext = false;

        [SerializeField, ShowIf(nameof(WriteSpawnerToContext))]
        [LabelText("Spawner Slot")]
        [PropertyTooltip("spawner scope を書き込む ContextA-D slot です。")]
        public CommandLtsSlot SpawnerContextSlot = CommandLtsSlot.ContextB;

        [Header("After Spawn")]
        [SerializeField]
        [LabelText("Run Commands On Spawned")]
        [PropertyTooltip("生成後、spawned scope 上で OnSpawnedCommands を実行します。")]
        public bool RunCommandsOnSpawned = false;

        [SerializeField, ShowIf(nameof(RunCommandsOnSpawned))]
        [LabelText("Vars Policy")]
        [PropertyTooltip("OnSpawnedCommands 実行時に使う vars の参照元です。")]
        public VarsPolicy VarsPolicy = VarsPolicy.Inherit;

        [SerializeField, ShowIf(nameof(RunCommandsOnSpawned))]
        [LabelText("Await OnSpawned Commands")]
        [PropertyTooltip("true のとき OnSpawnedCommands 完了まで待機します。false のときバックグラウンド実行します。")]
        public bool AwaitOnSpawnedCommands = true;

        [SerializeField, ShowIf(nameof(RunCommandsOnSpawned))]
        [LabelText("On Spawned Commands")]
        [PropertyTooltip("生成完了後に spawned scope 上で実行する command list です。")]
        public CommandListData OnSpawnedCommands = new();

        bool ShowTransformParent => TransformParentPolicy == SpawnTransformParentPolicy.UseTransform;
        bool ShowTransformParentActorSource => TransformParentPolicy == SpawnTransformParentPolicy.ActorSource;

        public SpawnRuntimeTemplateCommandData()
        {
            Position = DynamicValueExtensions.FromLiteral(Vector3.zero);
            Offset = DynamicValueExtensions.FromLiteral(Vector3.zero);
        }
    }
}

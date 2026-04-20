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
        [PropertyTooltip("Inspector setting.")]
        public DynamicValue<BaseRuntimeTemplatePreset> Template;

        [Header("Spawner")]
        [SerializeField]
        [EnumToggleButtons]
        [LabelText("Spawner Kind")]
        [PropertyTooltip("Inspector setting.")]
        public SpawnerKind SpawnerKind = SpawnerKind.RuntimeEntity;

        [SerializeField]
        [LabelText("Spawner Tag")]
        [PropertyTooltip("Inspector setting.")]
        public string SpawnerTag = "";

        [Header("Transform")]
        [SerializeField]
        [LabelText("World Space")]
        [PropertyTooltip("Inspector setting.")]
        public bool WorldSpace = true;

        [SerializeField]
        [LabelText("Position")]
        [PropertyTooltip("Inspector setting.")]
        public DynamicValue<Vector3> Position;

        [SerializeField]
        [LabelText("Offset")]
        [PropertyTooltip("Inspector setting.")]
        public DynamicValue<Vector3> Offset;

        [SerializeField]
        [LabelText("Rotation Euler")]
        [PropertyTooltip("Inspector setting.")]
        public DynamicValue<Vector3> RotationEuler = DynamicValueExtensions.FromLiteral(Vector3.zero);

        [SerializeField]
        [LabelText("Scale")]
        [PropertyTooltip("Inspector setting.")]
        public DynamicValue<Vector3> Scale = DynamicValueExtensions.FromLiteral(Vector3.one);

        [Header("Pre Spawn")]
        [SerializeField]
        [LabelText("Use Hidden Pre Spawn")]
        [PropertyTooltip("Inspector setting.")]
        public bool UseHiddenPreSpawn = false;

        [SerializeField, ShowIf(nameof(UseHiddenPreSpawn))]
        [LabelText("Hidden Spawn Offset")]
        [PropertyTooltip("Inspector setting.")]
        public DynamicValue<Vector3> HiddenSpawnOffset = DynamicValueExtensions.FromLiteral(new Vector3(100000f, 100000f, 100000f));

        [SerializeField, ShowIf(nameof(UseHiddenPreSpawn))]
        [MinValue(0)]
        [LabelText("Reveal Delay Frames")]
        [PropertyTooltip("Inspector setting.")]
        public DynamicValue<int> RevealDelayFrames = DynamicValueExtensions.FromLiteral(1);

        [Header("Count")]
        [SerializeField]
        [MinValue(1)]
        [LabelText("Count")]
        [PropertyTooltip("Inspector setting.")]
        public DynamicValue<int> Count = DynamicValueExtensions.FromLiteral(1);

        [Header("Delay Between Spawns")]
        [SerializeField]
        [LabelText("Delay Seconds")]
        [PropertyTooltip("Inspector setting.")]
        public DynamicValue<float> DelayBetweenSpawns = DynamicValueExtensions.FromLiteral(0f);

        [Header("Parent")]
        [SerializeField]
        [EnumToggleButtons]
        [LabelText("Transform Parent Policy")]
        [PropertyTooltip("Inspector setting.")]
        public SpawnTransformParentPolicy TransformParentPolicy = SpawnTransformParentPolicy.SpawnerRoot;

        [SerializeField, ShowIf(nameof(ShowTransformParent))]
        [LabelText("Transform Parent")]
        [PropertyTooltip("Inspector setting.")]
        public Transform? TransformParent;

        [SerializeField, ShowIf(nameof(ShowTransformParentActorSource))]
        [InlineProperty]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Transform Parent\", TransformParentActorSource)")]
        [PropertyTooltip("Inspector setting.")]
        public ActorSource TransformParentActorSource;

        [Header("DI Parent (optional)")]
        [SerializeField]
        [LabelText("Override LifetimeScope Parent")]
        [PropertyTooltip("Inspector setting.")]
        public bool OverrideLifetimeScopeParent = false;

        [SerializeField, ShowIf(nameof(OverrideLifetimeScopeParent))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"DI Parent\", LifetimeScopeParent)")]
        [PropertyTooltip("Inspector setting.")]
        public ActorSource LifetimeScopeParent;

        [Header("Pooling")]
        [SerializeField]
        [LabelText("Allow Pooling")]
        [PropertyTooltip("Inspector setting.")]
        public bool AllowPooling = true;

        [Header("Context")]
        [SerializeField]
        [LabelText("Write Spawned Scope To Context")]
        [PropertyTooltip("Inspector setting.")]
        public bool WriteSpawnedScopeToContext = false;

        [SerializeField, ShowIf(nameof(WriteSpawnedScopeToContext))]
        [LabelText("Spawned Scope Slot")]
        [PropertyTooltip("Inspector setting.")]
        public CommandLtsSlot SpawnedScopeSlot = CommandLtsSlot.ContextA;

        [SerializeField]
        [LabelText("Write Spawner To Context")]
        [PropertyTooltip("Inspector setting.")]
        public bool WriteSpawnerToContext = false;

        [SerializeField, ShowIf(nameof(WriteSpawnerToContext))]
        [LabelText("Spawner Slot")]
        [PropertyTooltip("Inspector setting.")]
        public CommandLtsSlot SpawnerContextSlot = CommandLtsSlot.ContextB;

        [Header("After Spawn")]
        [SerializeField]
        [LabelText("Run Commands On Spawned")]
        [PropertyTooltip("Inspector setting.")]
        public bool RunCommandsOnSpawned = false;

        [SerializeField, ShowIf(nameof(RunCommandsOnSpawned))]
        [LabelText("Vars Policy")]
        [PropertyTooltip("Inspector setting.")]
        public VarsPolicy VarsPolicy = VarsPolicy.Inherit;

        [SerializeField, ShowIf(nameof(RunCommandsOnSpawned))]
        [LabelText("Await OnSpawned Commands")]
        [PropertyTooltip("Inspector setting.")]
        public bool AwaitOnSpawnedCommands = true;

        [SerializeField, ShowIf(nameof(RunCommandsOnSpawned))]
        [LabelText("On Spawned Commands")]
        [PropertyTooltip("Inspector setting.")]
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

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
                return $"Template={templateName} Spawner={SpawnerKind} Tag={tag}{contextLabel}{sourceLabel}";
            }
        }

        [Header("Template")]
        [SerializeField, Required]
        public DynamicValue<BaseRuntimeTemplatePreset> Template;

        [Header("Spawner")]
        [SerializeField]
        [EnumToggleButtons]
        public SpawnerKind SpawnerKind = SpawnerKind.RuntimeEntity;

        [SerializeField]
        public string SpawnerTag = "";

        [Header("Transform")]
        [SerializeField]
        public bool WorldSpace = true;

        [SerializeField]
        public DynamicValue<Vector3> Position;

        [SerializeField]
        public DynamicValue<Vector3> Offset;

        [SerializeField]
        public DynamicValue<Vector3> RotationEuler = DynamicValueExtensions.FromLiteral(Vector3.zero);

        [SerializeField]
        public DynamicValue<Vector3> Scale = DynamicValueExtensions.FromLiteral(Vector3.one);

        [Header("Count")]
        [SerializeField]
        [MinValue(1)]
        public DynamicValue<int> Count = DynamicValueExtensions.FromLiteral(1);
        [Header("Delay Between Spawns")]
        [SerializeField]
        public DynamicValue<float> DelayBetweenSpawns = DynamicValueExtensions.FromLiteral(0f);

        [Header("Parent")]
        [SerializeField]
        [EnumToggleButtons]
        public SpawnTransformParentPolicy TransformParentPolicy = SpawnTransformParentPolicy.SpawnerRoot;

        [SerializeField, ShowIf(nameof(ShowTransformParent))]
        public Transform? TransformParent;

        [SerializeField, ShowIf(nameof(ShowTransformParentActorSource))]
        [InlineProperty]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Transform Parent\", TransformParentActorSource)")]
        public ActorSource TransformParentActorSource;

        [Header("DI Parent (optional)")]
        [SerializeField]
        public bool OverrideLifetimeScopeParent = false;

        [SerializeField, ShowIf(nameof(OverrideLifetimeScopeParent))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"DI Parent\", LifetimeScopeParent)")]
        public ActorSource LifetimeScopeParent;

        [Header("Pooling")]
        [SerializeField]
        public bool AllowPooling = true;

        [Header("Context")]
        [SerializeField]
        public bool WriteSpawnedScopeToContext = false;

        [SerializeField, ShowIf(nameof(WriteSpawnedScopeToContext))]
        [LabelText("Spawned Scope Slot")]
        public CommandLtsSlot SpawnedScopeSlot = CommandLtsSlot.ContextA;

        [SerializeField]
        [LabelText("Write Spawner To Context")]
        public bool WriteSpawnerToContext = false;

        [SerializeField, ShowIf(nameof(WriteSpawnerToContext))]
        [LabelText("Spawner Slot")]
        public CommandLtsSlot SpawnerContextSlot = CommandLtsSlot.ContextB;

        [Header("After Spawn")]
        [SerializeField]
        public bool RunCommandsOnSpawned = false;

        [SerializeField, ShowIf(nameof(RunCommandsOnSpawned))]
        public VarsPolicy VarsPolicy = VarsPolicy.Inherit;

        [SerializeField, ShowIf(nameof(RunCommandsOnSpawned))]
        [LabelText("Await OnSpawned Commands")]
        public bool AwaitOnSpawnedCommands = true;

        [SerializeField, ShowIf(nameof(RunCommandsOnSpawned))]
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

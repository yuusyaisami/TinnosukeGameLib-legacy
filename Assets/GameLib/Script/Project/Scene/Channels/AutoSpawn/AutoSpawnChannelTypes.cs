#nullable enable
using System;
using System.Collections.Generic;
using Game.Commands.VNext;
using Game.Common;
using Game.DI;
using Game.Spawn;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Channel
{
    public enum AutoSpawnChannelConditionMode
    {
        All = 0,
        Any = 1,
    }

    public enum AutoSpawnInitialSpawnConditionPolicy
    {
        IgnoreConditions = 0,
        RequireTrueOnAcquire = 1,
        WaitUntilFirstTrue = 2,
    }

    [Serializable]
    public sealed class AutoSpawnChannelFrequency
    {
        [LabelText("Interval (Sec) (-1 = Initial Only)")]
        public float SpawnIntervalSeconds = 1f;

        [LabelText("Count Per Interval"), MinValue(1)]
        public int SpawnCountPerInterval = 1;

        [LabelText("Initial Spawn Count"), MinValue(0)]
        public int InitialSpawnCount = 0;

        [LabelText("Initial Delay (Sec)"), MinValue(0f)]
        public float InitialDelaySeconds = 0f;

        [LabelText("Interval Jitter (Sec)"), MinValue(0f)]
        public float IntervalJitterSeconds = 0f;

        public void EnsureIntegrity()
        {
            if (SpawnIntervalSeconds < -1f)
                SpawnIntervalSeconds = -1f;
            else if (SpawnIntervalSeconds < 0f && SpawnIntervalSeconds > -1f)
                SpawnIntervalSeconds = 0f;
            if (SpawnCountPerInterval < 1)
                SpawnCountPerInterval = 1;
            if (InitialSpawnCount < 0)
                InitialSpawnCount = 0;
            if (InitialDelaySeconds < 0f)
                InitialDelaySeconds = 0f;
            if (IntervalJitterSeconds < 0f)
                IntervalJitterSeconds = 0f;
        }
    }

    [Serializable]
    public sealed class AutoSpawnChannelMappingEntry
    {
        [HorizontalGroup("Row"), HideLabel]
        [LabelText("Template Preset")]
        public DynamicValue<BaseRuntimeTemplatePreset> RuntimeTemplatePreset;

        [LabelText("On Spawned")]
        [CommandListFunctionName("AutoSpawnChannel.OnSpawned")]
        public CommandListData OnSpawnedCommands = new();

        [LabelText("Vars Policy")]
        public VarsPolicy VarsPolicy = VarsPolicy.Inherit;

        public bool TryResolveRuntimeTemplate(IDynamicContext context, out BaseRuntimeTemplateSO? runtimeTemplate)
        {
            runtimeTemplate = null;
            if (!RuntimeTemplatePreset.TryGet(context, out var preset) || preset == null)
                return false;

            runtimeTemplate = RuntimeTemplatePresetResolver.ResolveTemplateSO(preset);
            return runtimeTemplate != null;
        }
    }

    [Serializable]
    public sealed class AutoSpawnChannelDefinition : ChannelDefBase
    {
        [LabelText("Enabled")]
        public bool Enabled = true;

        [LabelText("Area Tags")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, DraggableItems = true, ShowIndexLabels = true)]
        public List<string> AreaTags = new();

        [LabelText("Area Selection")]
        public AreaTagSelectionMode AreaSelectionMode = AreaTagSelectionMode.RandomOne;

        [LabelText("Use Initial Area Tags")]
        public bool UseInitialAreaTags = false;

        [LabelText("Initial Area Tags")]
        [ShowIf(nameof(UseInitialAreaTags))]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, DraggableItems = true, ShowIndexLabels = true)]
        public List<string> InitialAreaTags = new();

        [LabelText("Frequency")]
        [InlineProperty]
        public AutoSpawnChannelFrequency Frequency = new();

        [LabelText("Spawn Offset")]
        public DynamicValue<Vector3> SpawnOffset = DynamicValueExtensions.FromLiteral(Vector3.zero);

        [LabelText("Condition Mode")]
        [EnumToggleButtons]
        public AutoSpawnChannelConditionMode ConditionMode = AutoSpawnChannelConditionMode.All;

        [LabelText("Initial Spawn Condition Policy")]
        [EnumToggleButtons]
        public AutoSpawnInitialSpawnConditionPolicy InitialSpawnConditionPolicy = AutoSpawnInitialSpawnConditionPolicy.IgnoreConditions;

        [LabelText("Conditions")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, DraggableItems = true, ShowIndexLabels = true)]
        public List<DynamicValue<bool>> Conditions = new();

        [LabelText("Mappings")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, DraggableItems = true, ShowIndexLabels = true)]
        public List<AutoSpawnChannelMappingEntry> Mappings = new();

        [LabelText("Spawner Kind")]
        [EnumToggleButtons]
        public SpawnerKind SpawnerKind = SpawnerKind.RuntimeEntity;

        [LabelText("Spawner Tag")]
        public string SpawnerTag = string.Empty;

        [LabelText("Allow Pooling")]
        public bool AllowPooling = true;

        [LabelText("Transform Parent")]
        public Transform? TransformParent;

        public override void EnsureIntegrity(Component owner)
        {
            base.EnsureIntegrity(owner);

            Frequency ??= new AutoSpawnChannelFrequency();
            Frequency.EnsureIntegrity();

            Conditions ??= new List<DynamicValue<bool>>();
            Mappings ??= new List<AutoSpawnChannelMappingEntry>();
            AreaTags ??= new List<string>();
            InitialAreaTags ??= new List<string>();
        }
    }

    public interface IAutoSpawnChannelHubService : IChannelHubService
    {
    }
}

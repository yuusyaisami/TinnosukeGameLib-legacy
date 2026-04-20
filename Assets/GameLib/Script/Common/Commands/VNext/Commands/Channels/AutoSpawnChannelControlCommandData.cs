#nullable enable
using System;
using Game.Channel;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum AutoSpawnChannelControlOperation
    {
        MutatePlayerSettings = 10,
        SpawnExternal = 20,
        ClearSpawned = 30,
    }

    [Serializable]
    public sealed class AutoSpawnChannelControlCommandData : ICommandData
    {
        public int CommandId => CommandIds.AutoSpawnChannelControl;

        public string DebugData => $"Hub={HubSource.Kind} Op={Operation} Tag={NormalizedTag}";

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Hub Source\", HubSource)")]
        public ActorSource HubSource = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Target")]
        [LabelText("Operation")]
        [EnumToggleButtons]
        public AutoSpawnChannelControlOperation Operation = AutoSpawnChannelControlOperation.SpawnExternal;

        [BoxGroup("Target")]
        [LabelText("Tag")]
        [Tooltip("Inspector setting.")]
        public string Tag = "default";

        [BoxGroup("Mutation")]
        [ShowIf(nameof(UsesMutation))]
        [InlineProperty]
        [HideLabel]
        public AutoSpawnChannelRuntimeMutation Mutation = new();

        [BoxGroup("Spawn")]
        [ShowIf(nameof(UsesSpawn))]
        [LabelText("Count")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<int> Count = DynamicValueExtensions.FromLiteral(1);

        [BoxGroup("Spawn")]
        [ShowIf(nameof(UsesSpawn))]
        [LabelText("Interval Sec")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<float> IntervalSeconds = DynamicValueExtensions.FromLiteral(0f);

        [BoxGroup("Spawn")]
        [ShowIf(nameof(UsesSpawn))]
        [LabelText("Await Mode")]
        [PropertyTooltip("Inspector setting.")]
        public FlowRunAwaitMode AwaitMode = FlowRunAwaitMode.WaitForCompletion;

        public string NormalizedTag => string.IsNullOrWhiteSpace(Tag) ? "default" : Tag.Trim();

        bool UsesMutation => Operation == AutoSpawnChannelControlOperation.MutatePlayerSettings;
        bool UsesSpawn => Operation == AutoSpawnChannelControlOperation.SpawnExternal;
    }
}

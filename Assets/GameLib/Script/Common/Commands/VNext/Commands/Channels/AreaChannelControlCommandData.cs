#nullable enable
using System;
using System.Collections.Generic;
using Game.Channel;
using Game.Common;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    public enum AreaChannelControlOperation
    {
        RegisterOrReplace = 10,
        Unregister = 20,
        MutateSettings = 30,
        MutateSettingsByTags = 40,
    }

    [Serializable]
    public sealed class AreaChannelControlCommandData : ICommandData
    {
        public int CommandId => CommandIds.AreaChannelControl;

        public string DebugData => $"Hub={HubSource.Kind} Op={Operation} Tag={NormalizedTag}";

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Hub Source\", HubSource)")]
        public ActorSource HubSource = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Target")]
        [LabelText("Operation")]
        [EnumToggleButtons]
        public AreaChannelControlOperation Operation = AreaChannelControlOperation.MutateSettingsByTags;

        [BoxGroup("Target")]
        [ShowIf(nameof(UsesTag))]
        [LabelText("Tag")]
        public string Tag = "default";

        [BoxGroup("Target")]
        [ShowIf(nameof(UsesTags))]
        [LabelText("Tags")]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = true, DefaultExpandedState = true)]
        public List<string> Tags = new();

        [BoxGroup("Register")]
        [ShowIf(nameof(UsesDefinition))]
        [LabelText("Definition")]
        [InlineProperty]
        [HideLabel]
        public DynamicValue<AreaChannelDefinition> Definition =
            DynamicValue<AreaChannelDefinition>.FromSource(
                new ManagedRefLiteralSource<AreaChannelDefinition>(new AreaChannelDefinition()));

        [BoxGroup("Register")]
        [ShowIf(nameof(UsesDefinition))]
        [LabelText("Overwrite")]
        public bool Overwrite = true;

        [BoxGroup("Mutation")]
        [ShowIf(nameof(UsesMutation))]
        [InlineProperty]
        [HideLabel]
        public AreaChannelRuntimeMutation Mutation = new();

        public string NormalizedTag => string.IsNullOrWhiteSpace(Tag) ? "default" : Tag.Trim();

        bool UsesTag =>
            Operation == AreaChannelControlOperation.Unregister ||
            Operation == AreaChannelControlOperation.MutateSettings;

        bool UsesTags => Operation == AreaChannelControlOperation.MutateSettingsByTags;
        bool UsesDefinition => Operation == AreaChannelControlOperation.RegisterOrReplace;
        bool UsesMutation =>
            Operation == AreaChannelControlOperation.MutateSettings ||
            Operation == AreaChannelControlOperation.MutateSettingsByTags;
    }
}

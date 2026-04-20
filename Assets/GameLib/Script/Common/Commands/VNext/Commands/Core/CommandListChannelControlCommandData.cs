#nullable enable
using System;
using Game.Commands;
using Game.Common;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class CommandListChannelHubControlCommandData : ICommandData
    {
        public int CommandId => CommandIds.CommandListChannelHubControl;
        public string DebugData => $"Target={Target.Kind} Tag={NormalizedChannelTag} Op={Operation}";

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Target\", Target)")]
        [PropertyTooltip("Inspector setting.")]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Target")]
        [LabelText("Channel Tag")]
        [PropertyTooltip("Inspector setting.")]
        public string ChannelTag = "default";

        [BoxGroup("Target")]
        [LabelText("Operation")]
        [EnumToggleButtons]
        public CommandListChannelHubControlOperation Operation = CommandListChannelHubControlOperation.RegisterOrReplace;

        [BoxGroup("Preset")]
        [ShowIf(nameof(UsesPreset))]
        [LabelText("Preset")]
        [PropertyTooltip("Inspector setting.")]
        public DynamicValue<CommandListChannelPreset> Preset =
            DynamicValue<CommandListChannelPreset>.FromSource(
                new ManagedRefLiteralSource<CommandListChannelPreset>());

        public string NormalizedChannelTag => string.IsNullOrWhiteSpace(ChannelTag) ? "default" : ChannelTag.Trim();

        bool UsesPreset => Operation == CommandListChannelHubControlOperation.RegisterOrReplace;
    }

    [Serializable]
    public sealed class CommandListChannelCommandData : ICommandData
    {
        public int CommandId => CommandIds.CommandListChannel;
        public string DebugData => $"Target={Target.Kind} Tag={NormalizedChannelTag} Op={Operation}";

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Target\", Target)")]
        [PropertyTooltip("Inspector setting.")]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Target")]
        [LabelText("Channel Tag")]
        [PropertyTooltip("Inspector setting.")]
        public string ChannelTag = "default";

        [BoxGroup("Target")]
        [LabelText("Operation")]
        [EnumToggleButtons]
        public CommandListChannelOperation Operation = CommandListChannelOperation.Play;

        [BoxGroup("Execution")]
        [ShowIf(nameof(UsesAwaitMode))]
        [LabelText("Await Mode")]
        [PropertyTooltip("Inspector setting.")]
        public FlowRunAwaitMode AwaitMode = FlowRunAwaitMode.WaitForCompletion;

        public string NormalizedChannelTag => string.IsNullOrWhiteSpace(ChannelTag) ? "default" : ChannelTag.Trim();

        bool UsesAwaitMode =>
            Operation == CommandListChannelOperation.Play ||
            Operation == CommandListChannelOperation.Resume ||
            Operation == CommandListChannelOperation.ExecuteNow;
    }

    [Serializable]
    public sealed class CommandListChannelPlayerControlCommandData : ICommandData
    {
        public int CommandId => CommandIds.CommandListChannelPlayerControl;
        public string DebugData => $"Target={Target.Kind} Tag={NormalizedChannelTag} Op={Operation}";

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Target\", Target)")]
        [PropertyTooltip("Inspector setting.")]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Target")]
        [LabelText("Channel Tag")]
        [PropertyTooltip("Inspector setting.")]
        public string ChannelTag = "default";

        [BoxGroup("Target")]
        [LabelText("Operation")]
        [EnumToggleButtons]
        public CommandListChannelPlayerControlOperation Operation = CommandListChannelPlayerControlOperation.SwapPlayerPreset;

        [BoxGroup("CommandListPreset")]
        [ShowIf(nameof(UsesCommandListPreset))]
        [LabelText("Command List Preset")]
        [PropertyTooltip("Inspector setting.")]
        public DynamicValue<CommandListPreset> CommandListPreset =
            DynamicValue<CommandListPreset>.FromSource(
                new ManagedRefLiteralSource<CommandListPreset>());

        [BoxGroup("PlayerPreset")]
        [ShowIf(nameof(UsesPlayerPreset))]
        [LabelText("Player Preset")]
        [PropertyTooltip("Inspector setting.")]
        public DynamicValue<CommandListPlayerPreset> PlayerPreset =
            DynamicValue<CommandListPlayerPreset>.FromSource(
                new ManagedRefLiteralSource<CommandListPlayerPreset>());

        [BoxGroup("Mutate")]
        [ShowIf(nameof(UsesMutation))]
        [LabelText("Mutation")]
        [InlineProperty]
        [HideLabel]
        public CommandListMutationStep Mutation = new()
        {
            Operation = CommandListMutationOperation.Append,
        };

        [BoxGroup("Mutate")]
        [ShowIf(nameof(UsesMutationCommandsSource))]
        [LabelText("Commands Source")]
        [InlineProperty]
        [HideLabel]
        public DynamicValue<CommandListData> MutationCommands =
            DynamicValue<CommandListData>.FromSource(
                new ManagedRefLiteralSource<CommandListData>());

        [BoxGroup("Vars")]
        [ShowIf(nameof(UsesRuntimeVars))]
        [LabelText("Overwrite Existing Vars")]
        public bool OverwriteExistingVars = true;

        [BoxGroup("Vars")]
        [ShowIf(nameof(UsesRuntimeVars))]
        [LabelText("Payload")]
        [InlineProperty]
        [HideLabel]
        public VarStorePayload Payload = new();

        [BoxGroup("Reset")]
        [ShowIf(nameof(UsesReset))]
        [LabelText("Reset Commands")]
        public bool ResetCommands = true;

        [BoxGroup("Reset")]
        [ShowIf(nameof(UsesReset))]
        [LabelText("Reset Player")]
        public bool ResetPlayer = true;

        [BoxGroup("Reset")]
        [ShowIf(nameof(UsesReset))]
        [LabelText("Reset Runtime Vars")]
        public bool ResetRuntimeVars = true;

        [BoxGroup("Reset")]
        [ShowIf(nameof(UsesReset))]
        [LabelText("Reset Playback State")]
        public bool ResetPlaybackState = true;

        public string NormalizedChannelTag => string.IsNullOrWhiteSpace(ChannelTag) ? "default" : ChannelTag.Trim();

        bool UsesCommandListPreset => Operation == CommandListChannelPlayerControlOperation.SwapCommandListPreset;
        bool UsesPlayerPreset => Operation == CommandListChannelPlayerControlOperation.SwapPlayerPreset;
        bool UsesMutation => Operation == CommandListChannelPlayerControlOperation.MutateCommands;
        bool UsesMutationCommandsSource => UsesMutation && Mutation.RequiresCommands();
        bool UsesRuntimeVars => Operation == CommandListChannelPlayerControlOperation.SetRuntimeVars;
        bool UsesReset => Operation == CommandListChannelPlayerControlOperation.ResetRuntimeOverrides;
    }
}

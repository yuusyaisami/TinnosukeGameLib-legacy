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
        [PropertyTooltip("CommandListChannelHub を持つ target scope です。空なら current scope から解決します。")]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Target")]
        [LabelText("Channel Tag")]
        [PropertyTooltip("操作対象の channel tag です。空白の場合は default を使用します。")]
        public string ChannelTag = "default";

        [BoxGroup("Target")]
        [LabelText("Operation")]
        [EnumToggleButtons]
        public CommandListChannelHubControlOperation Operation = CommandListChannelHubControlOperation.RegisterOrReplace;

        [BoxGroup("Preset")]
        [ShowIf(nameof(UsesPreset))]
        [LabelText("Preset")]
        [PropertyTooltip("register/replace 時に channel へ設定する source preset です。")]
        public DynamicValue<CommandListChannelPreset> Preset =
            DynamicValue<CommandListChannelPreset>.FromSource(
                new ManagedRefLiteralSource<CommandListChannelPreset>(new CommandListChannelPreset()));

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
        [PropertyTooltip("CommandListChannelHub を持つ target scope です。空なら current scope から解決します。")]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Target")]
        [LabelText("Channel Tag")]
        [PropertyTooltip("操作対象の channel tag です。空白の場合は default を使用します。")]
        public string ChannelTag = "default";

        [BoxGroup("Target")]
        [LabelText("Operation")]
        [EnumToggleButtons]
        public CommandListChannelOperation Operation = CommandListChannelOperation.Play;

        [BoxGroup("Execution")]
        [ShowIf(nameof(UsesAwaitMode))]
        [LabelText("Await Mode")]
        [PropertyTooltip("WaitForCompletion はこの command で直後に開始された execution の完了だけ待機します。Loop/PingPong の以後の周期実行までは待ちません。")]
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
        [PropertyTooltip("CommandListChannelHub を持つ target scope です。空なら current scope から解決します。")]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Target")]
        [LabelText("Channel Tag")]
        [PropertyTooltip("操作対象の channel tag です。空白の場合は default を使用します。")]
        public string ChannelTag = "default";

        [BoxGroup("Target")]
        [LabelText("Operation")]
        [EnumToggleButtons]
        public CommandListChannelPlayerControlOperation Operation = CommandListChannelPlayerControlOperation.SwapPlayerPreset;

        [BoxGroup("CommandListPreset")]
        [ShowIf(nameof(UsesCommandListPreset))]
        [LabelText("Command List Preset")]
        [PropertyTooltip("current command list preset をこの preset に完全差し替えします。")]
        public DynamicValue<CommandListPreset> CommandListPreset =
            DynamicValue<CommandListPreset>.FromSource(
                new ManagedRefLiteralSource<CommandListPreset>(new CommandListPreset()));

        [BoxGroup("PlayerPreset")]
        [ShowIf(nameof(UsesPlayerPreset))]
        [LabelText("Player Preset")]
        [PropertyTooltip("current player preset をこの preset に完全差し替えします。")]
        public DynamicValue<CommandListPlayerPreset> PlayerPreset =
            DynamicValue<CommandListPlayerPreset>.FromSource(
                new ManagedRefLiteralSource<CommandListPlayerPreset>(new CommandListPlayerPreset()));

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
                new ManagedRefLiteralSource<CommandListData>(new CommandListData()));

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

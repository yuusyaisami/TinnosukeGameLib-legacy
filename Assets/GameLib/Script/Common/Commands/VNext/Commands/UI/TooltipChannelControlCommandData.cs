#nullable enable
using System;
using Game.Common;
using Game.UI;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class TooltipChannelHubControlCommandData : ICommandData
    {
        public int CommandId => CommandIds.TooltipChannelHubControl;
        public string DebugData => $"Target={Target.Kind} Tag={NormalizedChannelTag} Op={Operation}";

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Target\", Target)")]
        [PropertyTooltip("TooltipChannelHub を持つ target scope です。空なら current scope から解決します。")]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Target")]
        [LabelText("Channel Tag")]
        [PropertyTooltip("register/replace/unregister の対象 channel tag です。空白の場合は default を使用します。")]
        public string ChannelTag = "default";

        [BoxGroup("Target")]
        [LabelText("Operation")]
        [EnumToggleButtons]
        public TooltipChannelHubControlOperation Operation = TooltipChannelHubControlOperation.RegisterOrReplace;

        [BoxGroup("Preset")]
        [ShowIf(nameof(UsesPlayerPreset))]
        [LabelText("Player Preset")]
        [PropertyTooltip("register/replace 時に channel へ設定する player preset です。")]
        public DynamicValue<TooltipPlayerPreset> PlayerPreset =
            DynamicValue<TooltipPlayerPreset>.FromSource(
                new ManagedRefLiteralSource<TooltipPlayerPreset>(new TooltipPlayerPreset()));

        [BoxGroup("Preset")]
        [ShowIf(nameof(UsesHubPreset))]
        [LabelText("Hub Preset")]
        [PropertyTooltip("hub 全体の current hub preset をこの preset に差し替えます。")]
        public DynamicValue<TooltipHubPreset> HubPreset =
            DynamicValue<TooltipHubPreset>.FromSource(
                new ManagedRefLiteralSource<TooltipHubPreset>(new TooltipHubPreset()));

        public string NormalizedChannelTag => string.IsNullOrWhiteSpace(ChannelTag) ? "default" : ChannelTag.Trim();

        bool UsesPlayerPreset => Operation == TooltipChannelHubControlOperation.RegisterOrReplace;
        bool UsesHubPreset => Operation == TooltipChannelHubControlOperation.SwapHubPreset;
    }

    [Serializable]
    public sealed class TooltipChannelCommandData : ICommandData
    {
        public int CommandId => CommandIds.TooltipChannel;
        public string DebugData => $"Target={Target.Kind} Tag={NormalizedChannelTag} Op={Operation}";

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Target\", Target)")]
        [PropertyTooltip("TooltipChannelHub を持つ target scope です。空なら current scope から解決します。")]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Target")]
        [LabelText("Channel Tag")]
        [PropertyTooltip("操作対象の channel tag です。空白の場合は default を使用します。")]
        public string ChannelTag = "default";

        [BoxGroup("Target")]
        [LabelText("Operation")]
        [EnumToggleButtons]
        public TooltipChannelOperation Operation = TooltipChannelOperation.ForceShow;

        public string NormalizedChannelTag => string.IsNullOrWhiteSpace(ChannelTag) ? "default" : ChannelTag.Trim();
    }

    [Serializable]
    public sealed class TooltipChannelPlayerControlCommandData : ICommandData
    {
        public int CommandId => CommandIds.TooltipChannelPlayerControl;
        public string DebugData => $"Target={Target.Kind} Tag={NormalizedChannelTag} Op={Operation}";

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Target\", Target)")]
        [PropertyTooltip("TooltipChannelHub を持つ target scope です。空なら current scope から解決します。")]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Target")]
        [LabelText("Channel Tag")]
        [PropertyTooltip("操作対象の channel tag です。空白の場合は default を使用します。")]
        public string ChannelTag = "default";

        [BoxGroup("Target")]
        [LabelText("Operation")]
        [EnumToggleButtons]
        public TooltipChannelPlayerControlOperation Operation = TooltipChannelPlayerControlOperation.SwapPlayerPreset;

        [BoxGroup("PlayerPreset")]
        [ShowIf(nameof(UsesPlayerPreset))]
        [LabelText("Player Preset")]
        [PropertyTooltip("current player preset をこの preset に完全差し替えします。")]
        public DynamicValue<TooltipPlayerPreset> PlayerPreset =
            DynamicValue<TooltipPlayerPreset>.FromSource(
                new ManagedRefLiteralSource<TooltipPlayerPreset>(new TooltipPlayerPreset()));

        [BoxGroup("CommandsPreset")]
        [ShowIf(nameof(UsesCommandsPreset))]
        [LabelText("Commands Preset")]
        [PropertyTooltip("current commands preset をこの preset に完全差し替えします。")]
        public DynamicValue<TooltipCommandsPreset> CommandsPreset =
            DynamicValue<TooltipCommandsPreset>.FromSource(
                new ManagedRefLiteralSource<TooltipCommandsPreset>(new TooltipCommandsPreset()));

        [BoxGroup("Reset")]
        [ShowIf(nameof(UsesReset))]
        [LabelText("Reset Player")]
        public bool ResetPlayer = true;

        [BoxGroup("Reset")]
        [ShowIf(nameof(UsesReset))]
        [LabelText("Reset Commands")]
        public bool ResetCommands = true;

        [BoxGroup("Reset")]
        [ShowIf(nameof(UsesReset))]
        [LabelText("Reset Force Override")]
        public bool ResetForceOverride = true;

        public string NormalizedChannelTag => string.IsNullOrWhiteSpace(ChannelTag) ? "default" : ChannelTag.Trim();

        bool UsesPlayerPreset => Operation == TooltipChannelPlayerControlOperation.SwapPlayerPreset;
        bool UsesCommandsPreset => Operation == TooltipChannelPlayerControlOperation.SwapCommandsPreset;
        bool UsesReset => Operation == TooltipChannelPlayerControlOperation.ResetRuntimeOverrides;
    }
}

#nullable enable
using System;
using Game.Common;
using Game.UI;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class ButtonChannelHubControlCommandData : ICommandData
    {
        public int CommandId => CommandIds.ButtonChannelHubControl;

        public string DebugData => $"Target={Target.Kind} Tag={NormalizedChannelTag} Op={Operation}";

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Target\", Target)")]
        [Tooltip("ButtonChannelHub を持つ target scope です。空なら current scope から解決します。")]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Target")]
        [LabelText("Channel Tag")]
        [Tooltip("操作対象の channel tag です。空白の場合は default を使用します。")]
        public string ChannelTag = "default";

        [BoxGroup("Target")]
        [LabelText("Operation")]
        [EnumToggleButtons]
        public ButtonChannelHubControlOperation Operation = ButtonChannelHubControlOperation.RegisterOrReplace;

        [BoxGroup("Preset")]
        [ShowIf(nameof(UsesPreset))]
        [LabelText("Preset")]
        [Tooltip("register/replace 時に channel へ設定する source preset です。")]
        public DynamicValue<ButtonChannelPreset> Preset =
            DynamicValue<ButtonChannelPreset>.FromSource(
                new ManagedRefLiteralSource<ButtonChannelPreset>(new ButtonChannelPreset()));

        public string NormalizedChannelTag => string.IsNullOrWhiteSpace(ChannelTag) ? "default" : ChannelTag.Trim();

        bool UsesPreset => Operation == ButtonChannelHubControlOperation.RegisterOrReplace;
    }

    [Serializable]
    public sealed class ButtonChannelPlayerControlCommandData : ICommandData
    {
        public int CommandId => CommandIds.ButtonChannelPlayerControl;

        public string DebugData => $"Target={Target.Kind} Tag={NormalizedChannelTag} Op={Operation}";

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Target\", Target)")]
        [Tooltip("ButtonChannelHub を持つ target scope です。空なら current scope から解決します。")]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Target")]
        [LabelText("Channel Tag")]
        [Tooltip("操作対象の channel tag です。空白の場合は default を使用します。")]
        public string ChannelTag = "default";

        [BoxGroup("Target")]
        [LabelText("Operation")]
        [EnumToggleButtons]
        public ButtonChannelPlayerControlOperation Operation = ButtonChannelPlayerControlOperation.MutatePlayerSettings;

        [BoxGroup("Swap Input")]
        [ShowIf(nameof(UsesSwapInputPreset))]
        [LabelText("Input Preset")]
        [Tooltip("current input preset をこの preset に完全差し替えします。")]
        public DynamicValue<ButtonInputPresetBase> InputPreset =
            DynamicValue<ButtonInputPresetBase>.FromSource(
                new ManagedRefLiteralSource<ButtonInputPresetBase>(new InstantButtonInputPreset()));

        [BoxGroup("Swap Player")]
        [ShowIf(nameof(UsesSwapPlayerPreset))]
        [LabelText("Player Preset")]
        [Tooltip("current player preset をこの preset に完全差し替えします。")]
        public DynamicValue<ButtonPlayerPreset> PlayerPreset =
            DynamicValue<ButtonPlayerPreset>.FromSource(
                new ManagedRefLiteralSource<ButtonPlayerPreset>(new ButtonPlayerPreset()));

        [BoxGroup("Mutate Input")]
        [ShowIf(nameof(UsesInputMutation))]
        [SerializeReference]
        [InlineProperty]
        [HideLabel]
        [Tooltip("current input preset に対する runtime mutation です。現在の preset kind と一致する mutation を指定します。")]
        public ButtonInputRuntimeMutationBase InputMutation = new InstantButtonInputRuntimeMutation();

        [BoxGroup("Mutate Player")]
        [ShowIf(nameof(UsesPlayerMutation))]
        [InlineProperty]
        [HideLabel]
        [Tooltip("current player preset に対する runtime mutation です。")]
        public ButtonPlayerRuntimeMutation PlayerMutation = new();

        [BoxGroup("Reset")]
        [ShowIf(nameof(UsesReset))]
        [LabelText("Reset Input")]
        public bool ResetInput = true;

        [BoxGroup("Reset")]
        [ShowIf(nameof(UsesReset))]
        [LabelText("Reset Player")]
        public bool ResetPlayer = true;

        public string NormalizedChannelTag => string.IsNullOrWhiteSpace(ChannelTag) ? "default" : ChannelTag.Trim();

        bool UsesSwapInputPreset => Operation == ButtonChannelPlayerControlOperation.SwapInputPreset;
        bool UsesSwapPlayerPreset => Operation == ButtonChannelPlayerControlOperation.SwapPlayerPreset;
        bool UsesInputMutation => Operation == ButtonChannelPlayerControlOperation.MutateInputSettings;
        bool UsesPlayerMutation => Operation == ButtonChannelPlayerControlOperation.MutatePlayerSettings;
        bool UsesReset => Operation == ButtonChannelPlayerControlOperation.ResetRuntimeOverrides;
    }
}

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
        [Tooltip("Inspector setting.")]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Target")]
        [LabelText("Channel Tag")]
        [Tooltip("Inspector setting.")]
        public string ChannelTag = "default";

        [BoxGroup("Target")]
        [LabelText("Operation")]
        [EnumToggleButtons]
        public ButtonChannelHubControlOperation Operation = ButtonChannelHubControlOperation.RegisterOrReplace;

        [BoxGroup("Preset")]
        [ShowIf(nameof(UsesPreset))]
        [LabelText("Preset")]
        [Tooltip("Inspector setting.")]
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
        [Tooltip("Inspector setting.")]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Target")]
        [LabelText("Channel Tag")]
        [Tooltip("Inspector setting.")]
        public string ChannelTag = "default";

        [BoxGroup("Target")]
        [LabelText("Operation")]
        [EnumToggleButtons]
        public ButtonChannelPlayerControlOperation Operation = ButtonChannelPlayerControlOperation.MutatePlayerSettings;

        [BoxGroup("Swap Input")]
        [ShowIf(nameof(UsesSwapInputPreset))]
        [LabelText("Input Preset")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<ButtonInputPresetBase> InputPreset =
            DynamicValue<ButtonInputPresetBase>.FromSource(
                new ManagedRefLiteralSource<ButtonInputPresetBase>(new InstantButtonInputPreset()));

        [BoxGroup("Swap Player")]
        [ShowIf(nameof(UsesSwapPlayerPreset))]
        [LabelText("Player Preset")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<ButtonPlayerPresetBase> PlayerPreset =
            DynamicValue<ButtonPlayerPresetBase>.FromSource(
                new ManagedRefLiteralSource<ButtonPlayerPresetBase>(new ButtonPlayerPreset()));

        [BoxGroup("Mutate Input")]
        [ShowIf(nameof(UsesInputMutation))]
        [SerializeReference]
        [InlineProperty]
        [HideLabel]
        [Tooltip("Inspector setting.")]
        public ButtonInputRuntimeMutationBase InputMutation = new InstantButtonInputRuntimeMutation();

        [BoxGroup("Mutate Player")]
        [ShowIf(nameof(UsesPlayerMutation))]
        [InlineProperty]
        [HideLabel]
        [Tooltip("Inspector setting.")]
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

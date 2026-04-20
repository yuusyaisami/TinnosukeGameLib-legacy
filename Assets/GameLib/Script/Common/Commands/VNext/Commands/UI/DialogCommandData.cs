#nullable enable
using System;
using Game.Common;
using Game.Dialogue;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum DialogueChannelOperation
    {
        Setup = 10,
        ShowMessage = 20,
        ShowChoiceAndWait = 30,
        ApplyCharacters = 40,
        RefreshLayout = 50,
        End = 60,
        SetVisible = 70,
        SetActive = 80,
        SetInputEnabled = 90,
        RequestAdvance = 100,
        CancelChoice = 110,
        RegisterOrReplaceChannel = 120,
        UnregisterChannel = 130,
    }

    [Serializable]
    public sealed class DialogueChannelCommandData : ICommandData
    {
        public int CommandId => CommandIds.DialogueChannel;

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
        public DialogueChannelOperation Operation = DialogueChannelOperation.Setup;

        [BoxGroup("Policy")]
        [LabelText("Strict")]
        [Tooltip("Inspector setting.")]
        public bool Strict = true;

        [BoxGroup("Setup")]
        [ShowIf(nameof(UsesSetup))]
        [InlineProperty]
        [HideLabel]
        public DialogueSetupRequest SetupRequest = new();

        [BoxGroup("Message")]
        [ShowIf(nameof(UsesShowMessage))]
        [InlineProperty]
        [HideLabel]
        public DialogueMessageRequest MessageRequest = new();

        [BoxGroup("Choice")]
        [ShowIf(nameof(UsesShowChoice))]
        [InlineProperty]
        [HideLabel]
        public DialogueChoiceRequest ChoiceRequest = new();

        [BoxGroup("Choice")]
        [ShowIf(nameof(UsesShowChoice))]
        [LabelText("Fail When Not Selected")]
        [Tooltip("Inspector setting.")]
        public bool FailWhenChoiceNotSelected;

        [BoxGroup("Choice")]
        [ShowIf(nameof(UsesShowChoice))]
        [LabelText("On Choice Canceled")]
        [CommandListFunctionName("DialogueChannel.Choice.OnCanceled")]
        public CommandListData OnChoiceCanceledCommands = new();

        [BoxGroup("Choice")]
        [ShowIf(nameof(UsesShowChoice))]
        [LabelText("On Choice Timeout")]
        [CommandListFunctionName("DialogueChannel.Choice.OnTimeout")]
        public CommandListData OnChoiceTimeoutCommands = new();

        [BoxGroup("Choice")]
        [ShowIf(nameof(UsesShowChoice))]
        [LabelText("On Choice Replaced")]
        [CommandListFunctionName("DialogueChannel.Choice.OnReplaced")]
        public CommandListData OnChoiceReplacedCommands = new();

        [BoxGroup("Characters")]
        [ShowIf(nameof(UsesApplyCharacters))]
        [InlineProperty]
        [HideLabel]
        public DialogueCharacterFrameRequest CharacterFrameRequest = new();

        [BoxGroup("Layout")]
        [ShowIf(nameof(UsesRefreshLayout))]
        [InlineProperty]
        [HideLabel]
        public DialogueLayoutRefreshRequest LayoutRequest = new();

        [BoxGroup("End")]
        [ShowIf(nameof(UsesEnd))]
        [InlineProperty]
        [HideLabel]
        public DialogueEndRequest EndRequest = new();

        [BoxGroup("State")]
        [ShowIf(nameof(UsesSetVisible))]
        [LabelText("Visible")]
        public bool Visible = true;

        [BoxGroup("State")]
        [ShowIf(nameof(UsesSetActive))]
        [LabelText("Active")]
        public bool Active = true;

        [BoxGroup("State")]
        [ShowIf(nameof(UsesSetInputEnabled))]
        [LabelText("Input Enabled")]
        public bool InputEnabled = true;

        [BoxGroup("Cancel")]
        [ShowIf(nameof(UsesCancelChoice))]
        [LabelText("Reason")]
        public string CancelChoiceReason = string.Empty;

        [BoxGroup("Registry")]
        [ShowIf(nameof(UsesRegisterOrReplaceChannel))]
        [LabelText("Preset")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<DialogueChannelPreset> RegisterPreset =
            DynamicValue<DialogueChannelPreset>.FromSource(
                new ManagedRefLiteralSource<DialogueChannelPreset>(new DialogueChannelPreset()));

        public string NormalizedChannelTag => string.IsNullOrWhiteSpace(ChannelTag) ? "default" : ChannelTag.Trim();

        bool UsesSetup => Operation == DialogueChannelOperation.Setup;
        bool UsesShowMessage => Operation == DialogueChannelOperation.ShowMessage;
        bool UsesShowChoice => Operation == DialogueChannelOperation.ShowChoiceAndWait;
        bool UsesApplyCharacters => Operation == DialogueChannelOperation.ApplyCharacters;
        bool UsesRefreshLayout => Operation == DialogueChannelOperation.RefreshLayout;
        bool UsesEnd => Operation == DialogueChannelOperation.End;
        bool UsesSetVisible => Operation == DialogueChannelOperation.SetVisible;
        bool UsesSetActive => Operation == DialogueChannelOperation.SetActive;
        bool UsesSetInputEnabled => Operation == DialogueChannelOperation.SetInputEnabled;
        bool UsesCancelChoice => Operation == DialogueChannelOperation.CancelChoice;
        bool UsesRegisterOrReplaceChannel => Operation == DialogueChannelOperation.RegisterOrReplaceChannel;
    }
}

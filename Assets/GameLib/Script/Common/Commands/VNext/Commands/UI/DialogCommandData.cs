#nullable enable
using System;
using Game.Common;
using Game.UI;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    public enum UIDialogInvokeMode
    {
        ShowOnly = 0,
        ShowAndWait = 1,
    }

    [Serializable]
    public sealed class UIDialogEventCommandMapping
    {
        [LabelText("Event Key")]
        public string EventKey = string.Empty;

        [LabelText("Commands")]
        public CommandListData Commands = new();

        [LabelText("Close After Invoke")]
        public bool CloseAfterInvoke = true;
    }

    [Serializable]
    public sealed class UIDialogChannelCommandData : ICommandData
    {
        public int CommandId => CommandIds.UIDialogChannel;
        public string DebugData
        {
            get
            {
                var key = string.IsNullOrEmpty(ChannelKey) ? "<none>" : ChannelKey;
                var awaitCount = AwaitEventKeys?.Length ?? 0;
                return $"Channel={key} Mode={Mode} Await={awaitCount}";
            }
        }

        [BoxGroup("Target")]
        [LabelText("Channel Key")]
        public string ChannelKey = "default";

        [BoxGroup("Mode")]
        [LabelText("Invoke Mode")]
        public UIDialogInvokeMode Mode = UIDialogInvokeMode.ShowAndWait;

        [BoxGroup("Vars")]
        [LabelText("Use Context Vars As Initial")]
        public bool UseContextVarsAsInitialVariables = true;

        [BoxGroup("Vars")]
        [LabelText("Initial Vars Payload")]
        public VarStorePayload InitialVariables = new();

        [BoxGroup("Vars")]
        [LabelText("Overwrite Initial Vars")]
        public bool OverwriteInitialVariables = true;

        [BoxGroup("Wait")]
        [ShowIf(nameof(ShowWait))]
        [LabelText("Await Event Keys")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        public string[] AwaitEventKeys = Array.Empty<string>();

        [BoxGroup("Wait")]
        [ShowIf(nameof(ShowWait))]
        [LabelText("Close After Event")]
        public bool CloseAfterEvent = true;

        [BoxGroup("Result")]
        [ShowIf(nameof(ShowWait))]
        [LabelText("Write Result To Vars")]
        public bool WriteResultToVars = true;

        [BoxGroup("Result")]
        [ShowIf(nameof(ShowWait))]
        [LabelText("Result EventKey Var (Shared)")]
        [VariableKeyPicker]
        public string ResultEventKeyStableKey = string.Empty;

        [BoxGroup("Result")]
        [ShowIf(nameof(ShowWait))]
        [LabelText("Result SelectedIndex Var (Shared)")]
        [VariableKeyPicker]
        public string ResultSelectedIndexStableKey = string.Empty;

        [BoxGroup("Result")]
        [ShowIf(nameof(ShowWait))]
        [LabelText("Result WasCancelled Var (Shared)")]
        [VariableKeyPicker]
        public string ResultWasCancelledStableKey = string.Empty;

        [BoxGroup("Result")]
        [ShowIf(nameof(ShowWait))]
        [LabelText("Merge Event Payload To Vars")]
        public bool MergeEventPayloadToVars = true;

        [BoxGroup("Result")]
        [ShowIf(nameof(ShowWait))]
        [LabelText("Overwrite Existing Vars")]
        public bool OverwriteExistingVars = true;

        [BoxGroup("Mapping")]
        [LabelText("Use Event→Commands Mapping")]
        public bool UseEventCommandMapping;

        [BoxGroup("Mapping")]
        [ShowIf(nameof(UseEventCommandMapping))]
        [LabelText("Mappings")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        public UIDialogEventCommandMapping[] Mappings = Array.Empty<UIDialogEventCommandMapping>();

        bool ShowWait() => Mode == UIDialogInvokeMode.ShowAndWait;
    }
}

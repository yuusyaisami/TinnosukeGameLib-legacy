#nullable enable
using System;
using Game.Common;
using Game.Conversation;
using Game.Dialogue;
using Game.Vars.Generated;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum ConversationFlowOperation
    {
        Run = 10,
        Start = 20,
        Continue = 30,
        End = 40,
    }

    public enum ConversationInFlowOperation
    {
        ShowMessage = 10,
        ShowChoiceAndWait = 20,
        JumpToNode = 30,
        WriteSnapshotToVars = 40,
    }

    [Serializable]
    public sealed class ConversationFlowCommandData : ICommandData
    {
        public int CommandId => CommandIds.ConversationFlow;

        public string DebugData => $"Target={Target.Kind} Tag={NormalizedConversationTag} Op={Operation}";

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Target\", Target)")]
        [Tooltip("ConversationChannelHub を持つ target scope です。空なら current scope から解決します。")]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Target")]
        [LabelText("Conversation Tag")]
        [Tooltip("操作対象の conversation tag です。空白の場合は default を使用します。")]
        public string ConversationTag = "default";

        [BoxGroup("Target")]
        [LabelText("Operation")]
        [EnumToggleButtons]
        public ConversationFlowOperation Operation = ConversationFlowOperation.Run;

        [BoxGroup("Policy")]
        [LabelText("Strict")]
        [Tooltip("true のとき失敗を command error として扱います。false のとき失敗時も処理を継続します。")]
        public bool Strict = true;

        [BoxGroup("Policy")]
        [ShowIf(nameof(UsesPreset))]
        [LabelText("Use Registered Preset Fallback")]
        [Tooltip("FlowPreset が resolve できないとき、hub 登録済み preset を fallback 使用します。")]
        public bool UseRegisteredPresetFallback = true;

        [BoxGroup("Flow")]
        [ShowIf(nameof(UsesPreset))]
        [LabelText("Flow Preset")]
        [Tooltip("Run/Start 時に使用する flow preset です。")]
        public DynamicValue<ConversationFlowPreset> FlowPreset =
            DynamicValue<ConversationFlowPreset>.FromSource(
                new ManagedRefLiteralSource<ConversationFlowPreset>(new ConversationFlowPreset()));

        [BoxGroup("Flow")]
        [ShowIf(nameof(UsesRunLike))]
        [LabelText("Max Node Steps Override")]
        [Tooltip("0 より大きいとき、preset の Max Node Steps を上書きします。")]
        [MinValue(0)]
        public int MaxNodeStepsOverride;

        [BoxGroup("End")]
        [ShowIf(nameof(UsesEnd))]
        [LabelText("End Message")]
        [Tooltip("End 操作時に snapshot へ記録するメッセージです。")]
        public string EndMessage = string.Empty;

        public string NormalizedConversationTag => string.IsNullOrWhiteSpace(ConversationTag) ? "default" : ConversationTag.Trim();

        bool UsesPreset => Operation == ConversationFlowOperation.Run || Operation == ConversationFlowOperation.Start;
        bool UsesRunLike => Operation == ConversationFlowOperation.Run || Operation == ConversationFlowOperation.Continue;
        bool UsesEnd => Operation == ConversationFlowOperation.End;
    }

    [Serializable]
    public sealed class ConversationInFlowCommandData : ICommandData
    {
        public int CommandId => CommandIds.ConversationInFlow;

        public string DebugData => $"Target={Target.Kind} Tag={NormalizedConversationTag} Op={Operation}";

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Target\", Target)")]
        [Tooltip("ConversationChannelHub を持つ target scope です。空なら current scope から解決します。")]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Target")]
        [LabelText("Conversation Tag")]
        [Tooltip("操作対象の conversation tag です。空白の場合は default を使用します。")]
        public string ConversationTag = "default";

        [BoxGroup("Target")]
        [LabelText("Operation")]
        [EnumToggleButtons]
        public ConversationInFlowOperation Operation = ConversationInFlowOperation.ShowMessage;

        [BoxGroup("Policy")]
        [LabelText("Strict")]
        [Tooltip("true のとき失敗を command error として扱います。false のとき失敗時も処理を継続します。")]
        public bool Strict = true;

        [BoxGroup("Dialogue")]
        [ShowIf(nameof(UsesDialogueOps))]
        [LabelText("Speaker Slot")]
        [Tooltip("Conversation slot -> Dialogue tag マップ解決で使うスロットです。")]
        public ConversationCharacterSlot Slot = ConversationCharacterSlot.Center;

        [BoxGroup("Dialogue")]
        [ShowIf(nameof(UsesDialogueOps))]
        [LabelText("Dialogue Tag Override")]
        [Tooltip("空でない場合、slot マップ解決を上書きしてこの tag を使用します。")]
        public string DialogueTagOverride = string.Empty;

        [BoxGroup("Dialogue")]
        [ShowIf(nameof(UsesDialogueOps))]
        [LabelText("Use Current Node Request")]
        [Tooltip("true のとき current node に定義された request を使います。")]
        public bool UseCurrentNodeRequest = true;

        [BoxGroup("Message")]
        [ShowIf("@UsesShowMessage && !UseCurrentNodeRequest")]
        [InlineProperty]
        [HideLabel]
        public DialogueMessageRequest MessageRequest = new();

        [BoxGroup("Choice")]
        [ShowIf("@UsesShowChoice && !UseCurrentNodeRequest")]
        [InlineProperty]
        [HideLabel]
        public DialogueChoiceRequest ChoiceRequest = new();

        [BoxGroup("Choice")]
        [ShowIf(nameof(UsesShowChoice))]
        [LabelText("Fail When Not Selected")]
        [Tooltip("true のとき choice completion が Selected 以外なら command error にします。")]
        public bool FailWhenChoiceNotSelected = true;

        [BoxGroup("Choice")]
        [ShowIf(nameof(UsesShowChoice))]
        [LabelText("Write SelectedIndex To Vars")]
        [Tooltip("true のとき selected index を SelectedIndexVar へ書き込みます。")]
        public bool WriteSelectedIndexToVars = true;

        [BoxGroup("Choice")]
        [ShowIf("@UsesShowChoice && WriteSelectedIndexToVars")]
        [LabelText("SelectedIndex Var")]
        [Tooltip("selected index の書き込み先 var です。")]
        public VarKeyRef SelectedIndexVar = new(VarIds.GameLib.Base.CommandVar.i, "i");

        [BoxGroup("Jump")]
        [ShowIf(nameof(UsesJump))]
        [LabelText("Node Id")]
        [MinValue(1)]
        public int JumpNodeId = 1;

        [BoxGroup("Snapshot")]
        [ShowIf(nameof(UsesSnapshotWrite))]
        [LabelText("Current Node Id Var")]
        public VarKeyRef CurrentNodeIdVar = new(VarIds.GameLib.Base.LocalVar.A, "A");

        [BoxGroup("Snapshot")]
        [ShowIf(nameof(UsesSnapshotWrite))]
        [LabelText("Turn Count Var")]
        public VarKeyRef TurnCountVar = new(VarIds.GameLib.Base.LocalVar.B, "B");

        [BoxGroup("Snapshot")]
        [ShowIf(nameof(UsesSnapshotWrite))]
        [LabelText("Last Selected Index Var")]
        public VarKeyRef LastSelectedIndexVar = new(VarIds.GameLib.Base.CommandVar.i, "i");

        public string NormalizedConversationTag => string.IsNullOrWhiteSpace(ConversationTag) ? "default" : ConversationTag.Trim();

        bool UsesShowMessage => Operation == ConversationInFlowOperation.ShowMessage;
        bool UsesShowChoice => Operation == ConversationInFlowOperation.ShowChoiceAndWait;
        bool UsesJump => Operation == ConversationInFlowOperation.JumpToNode;
        bool UsesSnapshotWrite => Operation == ConversationInFlowOperation.WriteSnapshotToVars;
        bool UsesDialogueOps => UsesShowMessage || UsesShowChoice;
    }
}

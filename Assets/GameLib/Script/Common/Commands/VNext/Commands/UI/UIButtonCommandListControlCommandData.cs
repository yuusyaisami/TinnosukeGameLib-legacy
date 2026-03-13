#nullable enable
using System;
using Game.Commands;
using Game.UI;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class UIButtonCommandListControlCommandData : ICommandData
    {
        public int CommandId => CommandIds.UIButtonCommandListControl;

        public string DebugData
        {
            get
            {
                var targetLabel = ActorSourceOdinLabelHelper.GetLabel("Target", Target);
                return $"{targetLabel} List={TargetList} Op={Operation}";
            }
        }

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        public ActorSource Target;

        [LabelText("Target List")]
        public UIButtonCommandListKind TargetList = UIButtonCommandListKind.SubmitDown;

        [LabelText("Operation")]
        public UIButtonCommandListOperation Operation = UIButtonCommandListOperation.Set;

        [ShowIf("@Operation == UIButtonCommandListOperation.Swap")]
        [LabelText("Swap Target")]
        public UIButtonCommandListKind SwapTarget = UIButtonCommandListKind.SubmitUp;

        [ShowIf("@Operation != UIButtonCommandListOperation.Swap")]
        [LabelText("Commands")]
        public CommandListData Commands = new();
    }
}

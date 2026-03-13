#nullable enable
using System;
using Game.Commands;
using Game.Commands.VNext;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    public enum MonitorChannelRuleOperation
    {
        AddRule = 0,
        RemoveRule = 1,
        ClearRules = 2,
    }

    [Serializable]
    public sealed class MonitorChannelRuleControlCommandData : ICommandData
    {
        public int CommandId => CommandIds.MonitorChannelRuleControl;

        public string DebugData
        {
            get
            {
                var targetLabel = ActorSourceOdinLabelHelper.GetLabel("Target", Target);
                return $"{targetLabel} Op={Operation} Rule={RuleName}";
            }
        }

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        public ActorSource Target;

        [LabelText("Operation")]
        public MonitorChannelRuleOperation Operation = MonitorChannelRuleOperation.AddRule;

        [ShowIf("@Operation == MonitorChannelRuleOperation.AddRule")]
        [LabelText("Rule")]
        public MonitorRule Rule;

        [ShowIf("@Operation == MonitorChannelRuleOperation.RemoveRule")]
        [LabelText("Rule Name")]
        public string RuleName = string.Empty;
    }
}

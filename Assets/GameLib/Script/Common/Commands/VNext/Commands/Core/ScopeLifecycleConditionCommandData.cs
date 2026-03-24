#nullable enable

using System;
using Game.Common;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    public enum ScopeLifecycleConditionOperation
    {
        SetOverride = 0,
        ClearOverride = 1,
    }

    [Serializable]
    public sealed class ScopeLifecycleConditionCommandData : ICommandData
    {
        public int CommandId => CommandIds.ScopeLifecycleCondition;

        public string DebugData
        {
            get
            {
                var actorLabel = ActorSourceOdinLabelHelper.GetLabel("Target", Target);
                if (Operation == ScopeLifecycleConditionOperation.ClearOverride)
                    return $"{actorLabel} Op=ClearOverride";

                var condition = CommandDebugDataHelper.GetDynamicDebugData(Condition, "true");
                return $"{actorLabel} Op=SetOverride Cond={condition}";
            }
        }

        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        public ActorSource Target;

        [LabelText("Operation")]
        [EnumToggleButtons]
        public ScopeLifecycleConditionOperation Operation = ScopeLifecycleConditionOperation.SetOverride;

        [ShowIf(nameof(ShowCondition))]
        [LabelText("Condition")]
        public DynamicValue<bool> Condition = DynamicValueExtensions.FromLiteral(true);

        bool ShowCondition => Operation == ScopeLifecycleConditionOperation.SetOverride;
    }
}

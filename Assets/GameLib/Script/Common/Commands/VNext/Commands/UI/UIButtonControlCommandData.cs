#nullable enable
using System;
using Game.Common;
using Game.UI;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class UIButtonControlCommandData : ICommandData
    {
        public int CommandId => CommandIds.UIButtonControl;

        public string DebugData
        {
            get
            {
                var targetLabel = ActorSourceOdinLabelHelper.GetLabel("Target", Target);
                return $"{targetLabel} State={ApplyState} Hold={ApplyHold} Guards={ApplyExecutionGuards} " +
                       $"Down={Count(SubmitDownCommands)} Up={Count(SubmitUpCommands)} " +
                       $"Decision={Count(HoldDecisionCommands)} Interval={Count(HoldIntervalCommands)} Cancel={Count(HoldCancelCommands)}";
            }
        }

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        public ActorSource Target;

        [BoxGroup("State")]
        [LabelText("Apply State")]
        [ToggleLeft]
        public bool ApplyState = false;

        [BoxGroup("State")]
        [ShowIf(nameof(ApplyState))]
        [LabelText("Kind")]
        public UIButtonKind Kind = UIButtonKind.Instant;

        [BoxGroup("State")]
        [ShowIf(nameof(ApplyState))]
        [LabelText("Can Submit")]
        public bool CanSubmit = false;

        [BoxGroup("State")]
        [ShowIf(nameof(ApplyState))]
        [LabelText("Input Control Condition")]
        [DynamicValueDefaultLiteral(true)]
        public DynamicValue<bool> InputControlCondition = DynamicValueExtensions.FromLiteral(true);

        [BoxGroup("State")]
        [ShowIf(nameof(ApplyState))]
        [LabelText("Trigger Action")]
        public UIInputAction TriggerAction = UIInputAction.Submit;

        [BoxGroup("Hold")]
        [LabelText("Apply Hold")]
        [ToggleLeft]
        public bool ApplyHold = false;

        [BoxGroup("Hold")]
        [ShowIf(nameof(ApplyHold))]
        [LabelText("Hold Time")]
        [MinValue(0.01f)]
        public float HoldTime = 1.0f;

        [BoxGroup("Hold")]
        [ShowIf(nameof(ApplyHold))]
        [LabelText("Hold Interval")]
        [MinValue(0.01f)]
        public float HoldInterval = 0.1f;

        [BoxGroup("Hold")]
        [ShowIf(nameof(ApplyHold))]
        [LabelText("Guard Selection While Holding")]
        public bool GuardSelectionWhileHolding = true;

        [BoxGroup("Guards")]
        [LabelText("Apply Execution Guards")]
        [ToggleLeft]
        public bool ApplyExecutionGuards = false;

        [BoxGroup("Guards")]
        [ShowIf(nameof(ApplyExecutionGuards))]
        [LabelText("Guard During Command Execution")]
        public bool GuardDuringCommandExecution = true;

        [BoxGroup("Guards")]
        [ShowIf(nameof(ApplyExecutionGuards))]
        [LabelText("Disable Selection During Command Execution")]
        public bool DisableSelectionDuringCommandExecution;

        [BoxGroup("Submit Down")]
        [LabelText("Apply Submit Down")]
        [ToggleLeft]
        public bool ApplySubmitDownCommands = false;

        [BoxGroup("Submit Down")]
        [ShowIf(nameof(ApplySubmitDownCommands))]
        [InlineProperty]
        [HideLabel]
        public CommandListMutationStep SubmitDownCommands = new()
        {
            Operation = CommandListMutationOperation.Override,
        };

        [BoxGroup("Submit Up")]
        [LabelText("Apply Submit Up")]
        [ToggleLeft]
        public bool ApplySubmitUpCommands = false;

        [BoxGroup("Submit Up")]
        [ShowIf(nameof(ApplySubmitUpCommands))]
        [InlineProperty]
        [HideLabel]
        public CommandListMutationStep SubmitUpCommands = new()
        {
            Operation = CommandListMutationOperation.Override,
        };

        [BoxGroup("Hold Decision")]
        [LabelText("Apply Hold Decision")]
        [ToggleLeft]
        public bool ApplyHoldDecisionCommands = false;

        [BoxGroup("Hold Decision")]
        [ShowIf(nameof(ApplyHoldDecisionCommands))]
        [InlineProperty]
        [HideLabel]
        public CommandListMutationStep HoldDecisionCommands = new()
        {
            Operation = CommandListMutationOperation.Override,
        };

        [BoxGroup("Hold Interval")]
        [LabelText("Apply Hold Interval")]
        [ToggleLeft]
        public bool ApplyHoldIntervalCommands = false;

        [BoxGroup("Hold Interval")]
        [ShowIf(nameof(ApplyHoldIntervalCommands))]
        [InlineProperty]
        [HideLabel]
        public CommandListMutationStep HoldIntervalCommands = new()
        {
            Operation = CommandListMutationOperation.Override,
        };

        [BoxGroup("Hold Cancel")]
        [LabelText("Apply Hold Cancel")]
        [ToggleLeft]
        public bool ApplyHoldCancelCommands = false;

        [BoxGroup("Hold Cancel")]
        [ShowIf(nameof(ApplyHoldCancelCommands))]
        [InlineProperty]
        [HideLabel]
        public CommandListMutationStep HoldCancelCommands = new()
        {
            Operation = CommandListMutationOperation.Override,
        };

        static int Count(CommandListMutationStep? step) => step?.Commands?.Count ?? 0;
    }
}

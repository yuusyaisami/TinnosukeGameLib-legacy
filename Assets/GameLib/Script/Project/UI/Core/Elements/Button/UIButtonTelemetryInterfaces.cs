#nullable enable
using System;

namespace Game.UI
{
    public enum UIButtonInputRejectReason
    {
        None,
        GuardDuringCommandExecution,
        CanSubmitFalse,
        ElementNotActive,
        ElementNotVisible,
        SelectionStateMissing,
        NotSelected,
        InputControlConditionFalse
    }

    public readonly struct UIButtonTelemetrySnapshot
    {
        public readonly string OwnerName;
        public readonly UIButtonKind Kind;
        public readonly UIInputAction TriggerAction;
        public readonly bool CanSubmit;

        public readonly bool IsSelected;
        public readonly bool IsVisible;
        public readonly bool IsEffectivelyActive;

        public readonly bool GuardSelectionWhileHolding;
        public readonly bool GuardDuringCommandExecution;
        public readonly bool DisableSelectionDuringCommandExecution;

        public readonly bool IsHolding;
        public readonly float HoldProgress;
        public readonly bool IsHoldDecisionExecuting;
        public readonly bool IsSubmitUpExecuting;

        public readonly UIInputEventType LastInputEventType;
        public readonly UIInputPhase LastInputPhase;
        public readonly bool LastInputMatched;
        public readonly bool LastInputAccepted;
        public readonly UIButtonInputRejectReason LastRejectReason;

        public readonly bool InputConditionHasSource;
        public readonly bool InputConditionValue;

        public readonly double TimestampUtc;

        public UIButtonTelemetrySnapshot(
            string ownerName,
            UIButtonKind kind,
            UIInputAction triggerAction,
            bool canSubmit,
            bool isSelected,
            bool isVisible,
            bool isEffectivelyActive,
            bool guardSelectionWhileHolding,
            bool guardDuringCommandExecution,
            bool disableSelectionDuringCommandExecution,
            bool isHolding,
            float holdProgress,
            bool isHoldDecisionExecuting,
            bool isSubmitUpExecuting,
            UIInputEventType lastInputEventType,
            UIInputPhase lastInputPhase,
            bool lastInputMatched,
            bool lastInputAccepted,
            UIButtonInputRejectReason lastRejectReason,
            bool inputConditionHasSource,
            bool inputConditionValue,
            double timestampUtc)
        {
            OwnerName = ownerName;
            Kind = kind;
            TriggerAction = triggerAction;
            CanSubmit = canSubmit;
            IsSelected = isSelected;
            IsVisible = isVisible;
            IsEffectivelyActive = isEffectivelyActive;
            GuardSelectionWhileHolding = guardSelectionWhileHolding;
            GuardDuringCommandExecution = guardDuringCommandExecution;
            DisableSelectionDuringCommandExecution = disableSelectionDuringCommandExecution;
            IsHolding = isHolding;
            HoldProgress = holdProgress;
            IsHoldDecisionExecuting = isHoldDecisionExecuting;
            IsSubmitUpExecuting = isSubmitUpExecuting;
            LastInputEventType = lastInputEventType;
            LastInputPhase = lastInputPhase;
            LastInputMatched = lastInputMatched;
            LastInputAccepted = lastInputAccepted;
            LastRejectReason = lastRejectReason;
            InputConditionHasSource = inputConditionHasSource;
            InputConditionValue = inputConditionValue;
            TimestampUtc = timestampUtc;
        }
    }

    public interface IUIButtonTelemetry
    {
        event Action<UIButtonTelemetrySnapshot>? OnTelemetryUpdated;
        UIButtonTelemetrySnapshot LastSnapshot { get; }
    }
}

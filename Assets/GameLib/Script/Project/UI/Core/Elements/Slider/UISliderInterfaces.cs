#nullable enable
using System;
using UnityEngine;
using Game.Common;
using Game.Scalar;

namespace Game.UI
{
    public enum UISliderAxis
    {
        Horizontal,
        Vertical
    }

    public enum UISliderDirection
    {
        LeftToRight,
        RightToLeft,
        BottomToTop,
        TopToBottom
    }

    public enum UISliderInputMode
    {
        SubmitToggle,
        PointerCapture
    }

    public enum UISliderStepMode
    {
        Raw,
        Normalized
    }

    public enum UISliderCancelBehavior
    {
        KeepValue,
        RevertToStart
    }

    public enum UISliderExternalBindingPriority
    {
        Scalar,
        Blackboard
    }

    public enum UISliderChangeSource
    {
        UserPointer,
        UserNavigate,
        ExternalBinding,
        Initialization
    }

    public enum UISliderEditMode
    {
        None,
        PointerCapture,
        SubmitToggle
    }

    public enum UISliderEndEditReason
    {
        PointerUp,
        SubmitToggle,
        SelectionLost,
        Cancel
    }

    public interface IUISliderController
    {
        void RequestBeginEdit(UISliderEditMode mode);
        void RequestEndEdit(UISliderEndEditReason reason);
        void RequestSetNormalized(float normalized, UISliderChangeSource source);
        void RequestStep(int step, UISliderChangeSource source);
    }

    public readonly struct UISliderOutputSnapshot
    {
        public readonly float NormalizedValue;
        public readonly float RawValue;
        public readonly bool IsEditing;

        public UISliderOutputSnapshot(float normalized, float raw, bool isEditing)
        {
            NormalizedValue = normalized;
            RawValue = raw;
            IsEditing = isEditing;
        }
    }

    public interface IUISliderOutput
    {
        float NormalizedValue { get; }
        float RawValue { get; }
        bool IsEditing { get; }
        event Action<UISliderOutputSnapshot>? OnUpdated;
    }

    public interface IUISliderValueOptions
    {
        float MinValue { get; }
        float MaxValue { get; }
        float InitialValue { get; }
        float Step { get; }
        UISliderStepMode StepMode { get; }
        float UpdateEpsilon { get; }
        bool IsEditable { get; }
        UISliderCancelBehavior CancelBehavior { get; }
        bool UseScalarBinding { get; }
        ScalarKey ScalarKey { get; }
        bool UseBlackboardBinding { get; }
        VarKeyRef BlackboardKey { get; }
        UISliderExternalBindingPriority BindingPriority { get; }
        bool WriteToBothBindings { get; }
    }

    public interface IUISliderInputOptions
    {
        RectTransform TrackRect { get; }
        RectTransform? HitTestRect { get; }
        UISliderAxis Axis { get; }
        UISliderDirection Direction { get; }
        float PaddingStart { get; }
        float PaddingEnd { get; }
        UISliderInputMode InputMode { get; }
        Camera? UICamera { get; }
        float NavigateRepeatDelay { get; }
        float NavigateRepeatInterval { get; }
        float ScrollRepeatDelay { get; }
        float ScrollRepeatInterval { get; }
    }
}

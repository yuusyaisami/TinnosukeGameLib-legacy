#nullable enable
using System;
using Game.Commands.VNext;
using UnityEngine;
using Game.Common;
using Game.Scalar;

namespace Game.UI
{
    public enum UISliderAxis
    {
        Horizontal = 0,
        Vertical = 1
    }

    public enum UISliderDirection
    {
        LeftToRight = 0,
        RightToLeft = 1,
        BottomToTop = 2,
        TopToBottom = 3
    }

    public enum UISliderInputMode
    {
        SubmitToggle = 0,
        PointerCapture = 1,
        None = 2
    }

    public enum UISliderStepMode
    {
        Raw = 0,
        Normalized = 1
    }

    public enum UISliderCancelBehavior
    {
        KeepValue = 0,
        RevertToStart = 1
    }

    public enum UISliderExternalBindingPriority
    {
        Scalar = 0,
        Blackboard = 1
    }

    public enum UISliderChangeSource
    {
        UserPointer = 0,
        UserNavigate = 1,
        ExternalBinding = 2,
        Initialization = 3
    }

    public enum UISliderEditMode
    {
        None = 0,
        PointerCapture = 1,
        SubmitToggle = 2
    }

    public enum UISliderEndEditReason
    {
        PointerUp = 0,
        SubmitToggle = 1,
        SelectionLost = 2,
        Cancel = 3
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
        DynamicValue<float> MinValue { get; }
        DynamicValue<float> MaxValue { get; }
        DynamicValue<float> InitialValue { get; }
        DynamicValue<float> Step { get; }
        UISliderStepMode StepMode { get; }
        float UpdateEpsilon { get; }
        bool IsEditable { get; }
        UISliderCancelBehavior CancelBehavior { get; }
        bool UseScalarBinding { get; }
        ActorSource ScalarBindingSource { get; }
        ScalarKey ScalarKey { get; }
        bool UseBlackboardBinding { get; }
        ActorSource BlackboardBindingSource { get; }
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

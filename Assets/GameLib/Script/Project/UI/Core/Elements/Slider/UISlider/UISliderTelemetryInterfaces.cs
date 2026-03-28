#nullable enable
using System;
using UnityEngine;

namespace Game.UI
{
    public enum UISliderInteractionEventKind
    {
        None,
        PointerDown,
        PointerUp,
        PointerMove,
        LongPressStart,
        LongPressEnd
    }

    public readonly struct UISliderTelemetrySnapshot
    {
        public readonly UISliderInteractionEventKind LastEvent;
        public readonly Vector2 LastPointerPosition;
        public readonly float NormalizedValue;
        public readonly float RawValue;
        public readonly bool IsEditing;
        public readonly bool IsPointerDown;
        public readonly bool IsLongPressed;
        public readonly double TimestampUtc;

        public UISliderTelemetrySnapshot(
            UISliderInteractionEventKind lastEvent,
            Vector2 lastPointerPosition,
            float normalizedValue,
            float rawValue,
            bool isEditing,
            bool isPointerDown,
            bool isLongPressed,
            double timestampUtc)
        {
            LastEvent = lastEvent;
            LastPointerPosition = lastPointerPosition;
            NormalizedValue = normalizedValue;
            RawValue = rawValue;
            IsEditing = isEditing;
            IsPointerDown = isPointerDown;
            IsLongPressed = isLongPressed;
            TimestampUtc = timestampUtc;
        }
    }

    public interface IUISliderTelemetry
    {
        /// <summary>
        /// Snapshot that listeners receive whenever telemetry state changes.
        /// </summary>
        event Action<UISliderTelemetrySnapshot>? OnTelemetryUpdated;

        /// <summary>
        /// Notify telemetry that pointer went down on the slider (screen position).
        /// </summary>
        void NotifyPointerDown(Vector2 screenPosition);

        /// <summary>
        /// Notify telemetry that pointer moved while down.
        /// </summary>
        void NotifyPointerMove(Vector2 screenPosition);

        /// <summary>
        /// Notify telemetry that pointer was released.
        /// </summary>
        void NotifyPointerUp();
    }
}

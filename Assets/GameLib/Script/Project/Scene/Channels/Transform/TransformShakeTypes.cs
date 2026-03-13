#nullable enable
using System;
using Sirenix.OdinInspector;

namespace Game.Channel
{
    [Serializable]
    public struct TransformShakeSettings
    {
        [LabelText("Amplitude X"), MinValue(0f)]
        public float AmplitudeX;

        [LabelText("Amplitude Y"), MinValue(0f)]
        public float AmplitudeY;

        [LabelText("Frequency"), MinValue(0.01f)]
        public float Frequency;

        [LabelText("Enable Rotation")]
        public bool EnableRotation;

        [ShowIf(nameof(EnableRotation))]
        [LabelText("Rotation Amplitude"), MinValue(0f)]
        public float RotationAmplitudeDeg;

        [LabelText("Duration (sec)"), MinValue(0f)]
        public float DurationSeconds;

        public static TransformShakeSettings Default => new TransformShakeSettings
        {
            AmplitudeX = 0.1f,
            AmplitudeY = 0.1f,
            Frequency = 12f,
            EnableRotation = false,
            RotationAmplitudeDeg = 2f,
            DurationSeconds = 0f,
        };
    }
}

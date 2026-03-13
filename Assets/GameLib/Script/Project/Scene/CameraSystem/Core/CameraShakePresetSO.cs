#nullable enable
using UnityEngine;
using Sirenix.OdinInspector;
using Game.Times;

namespace Game.CameraSystem
{
    [CreateAssetMenu(menuName = "Game/Camera/CameraShakePreset", fileName = "CameraShakePreset")]
    public sealed class CameraShakePresetSO : ScriptableObject
    {
        [BoxGroup("Amplitude")]
        [LabelText("Position")]
        [MinValue(0f)]
        public float amplitudePosition = 0.5f;

        [BoxGroup("Amplitude")]
        [LabelText("Rotation (deg)")]
        [MinValue(0f)]
        public float amplitudeRotationDeg = 1f;

        [BoxGroup("Timing")]
        [LabelText("Frequency")]
        [MinValue(0.01f)]
        public float frequency = 8f;

        [BoxGroup("Timing")]
        [LabelText("Duration")]
        [MinValue(0f)]
        public float duration = 0.4f;

        [BoxGroup("Decay")]
        [LabelText("Mode")]
        public CameraShakeDecayMode decayMode = CameraShakeDecayMode.Exponential;

        [BoxGroup("Decay")]
        [LabelText("Lambda Or Decay")]
        [MinValue(0f)]
        public float lambdaOrDecay = 8f;

        [BoxGroup("Behavior")]
        [LabelText("Seed")]
        public uint seed = 1;

        [BoxGroup("Behavior")]
        [LabelText("Time Scale")]
        public TimeScaleBehavior timeScaleBehavior = TimeScaleBehavior.Unscaled;

        public CameraShakePreset ToPreset()
        {
            return new CameraShakePreset(
                amplitudePosition,
                amplitudeRotationDeg,
                frequency,
                duration,
                decayMode,
                lambdaOrDecay,
                seed,
                timeScaleBehavior);
        }
    }
}

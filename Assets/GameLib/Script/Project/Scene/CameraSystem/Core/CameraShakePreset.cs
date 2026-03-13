#nullable enable
using Game.Times;

namespace Game.CameraSystem
{
    public readonly struct CameraShakePreset
    {
        public readonly float AmplitudePosition;
        public readonly float AmplitudeRotationDeg;
        public readonly float Frequency;
        public readonly float Duration;
        public readonly CameraShakeDecayMode DecayMode;
        public readonly float LambdaOrDecay;
        public readonly uint Seed;
        public readonly TimeScaleBehavior TimeScaleBehavior;

        public CameraShakePreset(
            float amplitudePosition,
            float amplitudeRotationDeg,
            float frequency,
            float duration,
            CameraShakeDecayMode decayMode,
            float lambdaOrDecay,
            uint seed,
            TimeScaleBehavior timeScaleBehavior)
        {
            AmplitudePosition = amplitudePosition;
            AmplitudeRotationDeg = amplitudeRotationDeg;
            Frequency = frequency;
            Duration = duration;
            DecayMode = decayMode;
            LambdaOrDecay = lambdaOrDecay;
            Seed = seed;
            TimeScaleBehavior = timeScaleBehavior;
        }
    }
}

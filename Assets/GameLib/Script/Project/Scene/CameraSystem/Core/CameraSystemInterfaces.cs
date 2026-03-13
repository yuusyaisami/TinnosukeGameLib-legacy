#nullable enable
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using Game.Times;

namespace Game.CameraSystem
{
    public interface ICameraSystemService
    {
        ICameraZoomService Zoom { get; }
        ICameraPostProcessService PostProcess { get; }
        ICameraFxService Fx { get; }
        string MoveChannelTag { get; }
        void ResetZoom();
        void ResetPostProcess();
        void ResetFx();
        void ResetAll();
    }

    public interface ICameraZoomService
    {
        void SetLayer(string layerTag, float value);
        void SetLayer(string layerTag, float value, float duration, Ease ease);
        void ClearLayer(string layerTag);
        void ClearAllLayers();
        float Current { get; }
        float Target { get; }
        float BaseZoom { get; }
        float MinSize { get; }
        float MaxSize { get; }
        IReadOnlyList<CameraZoomLayer> Layers { get; }
    }

    public interface ICameraPostProcessService
    {
        bool IsActive(string tag);
        void SetLayer(string layerTag, CameraPostProcessFloatParam param, float value);
        void SetLayer(string layerTag, CameraPostProcessFloatParam param, float value, float duration, Ease ease);
        void SetLayer(string layerTag, CameraPostProcessColorParam param, Color value);
        void SetLayer(string layerTag, CameraPostProcessColorParam param, Color value, float duration, Ease ease);
        void SetLayer(string layerTag, CameraPostProcessVector2Param param, Vector2 value);
        void SetLayer(string layerTag, CameraPostProcessVector2Param param, Vector2 value, float duration, Ease ease);
        void SetLayer(string layerTag, CameraPostProcessBoolParam param, bool value);
        void ClearLayer(string layerTag, CameraPostProcessFloatParam param);
        void ClearLayer(string layerTag, CameraPostProcessColorParam param);
        void ClearLayer(string layerTag, CameraPostProcessVector2Param param);
        void ClearLayer(string layerTag, CameraPostProcessBoolParam param);
        void ClearLayer(string layerTag);
        void ClearAllLayers();
        IReadOnlyList<CameraPostProcessLayer> Layers { get; }
    }

    public interface ICameraFxService
    {
        int PlayShake(CameraShakePreset preset, int priority = 0);
        void StopShake(int handle, float fadeOutSeconds = 0.1f);
        void StopAll(float fadeOutSeconds = 0.1f);
        void SetGlobalIntensity(float scale);
        Vector3 CurrentOffset { get; }
        float CurrentRotationZ { get; }
        IReadOnlyList<CameraShakeInstance> ActiveShakes { get; }
    }

    public enum CameraPostProcessFloatParam
    {
        BloomThreshold,
        BloomIntensity,
        BloomScatter,
        BloomClamp,
        VignetteIntensity,
        VignetteSmoothness,
        ChromaticAberrationIntensity,
        ColorAdjustmentsPostExposure,
        ColorAdjustmentsContrast,
        ColorAdjustmentsHueShift,
        ColorAdjustmentsSaturation,
        SplitToningBalance
    }

    public enum CameraPostProcessColorParam
    {
        BloomTint,
        VignetteColor,
        ColorAdjustmentsColorFilter,
        SplitToningShadows,
        SplitToningHighlights
    }

    public enum CameraPostProcessVector2Param
    {
        VignetteCenter
    }

    public enum CameraPostProcessBoolParam
    {
        VignetteRounded
    }

    public enum CameraShakeDecayMode
    {
        Exponential,
        Linear
    }

    public readonly struct CameraZoomLayer
    {
        public readonly string Tag;
        public readonly float Value;
        public readonly float Target;
        public readonly float Duration;
        public readonly Ease Ease;
        public readonly bool IsAnimating;

        public CameraZoomLayer(string tag, float value, float target, float duration, Ease ease, bool isAnimating)
        {
            Tag = tag;
            Value = value;
            Target = target;
            Duration = duration;
            Ease = ease;
            IsAnimating = isAnimating;
        }
    }

    public readonly struct CameraPostProcessLayer
    {
        public readonly string Tag;

        public CameraPostProcessLayer(string tag)
        {
            Tag = tag;
        }
    }

    public readonly struct CameraShakeInstance
    {
        public readonly int Handle;
        public readonly float AmplitudePos;
        public readonly float AmplitudeRotDeg;
        public readonly float Frequency;
        public readonly float Duration;
        public readonly CameraShakeDecayMode DecayMode;
        public readonly float LambdaOrDecay;
        public readonly uint Seed;
        public readonly TimeScaleBehavior TimeScaleBehavior;
        public readonly int Priority;

        public CameraShakeInstance(
            int handle,
            float amplitudePos,
            float amplitudeRotDeg,
            float frequency,
            float duration,
            CameraShakeDecayMode decayMode,
            float lambdaOrDecay,
            uint seed,
            TimeScaleBehavior timeScaleBehavior,
            int priority)
        {
            Handle = handle;
            AmplitudePos = amplitudePos;
            AmplitudeRotDeg = amplitudeRotDeg;
            Frequency = frequency;
            Duration = duration;
            DecayMode = decayMode;
            LambdaOrDecay = lambdaOrDecay;
            Seed = seed;
            TimeScaleBehavior = timeScaleBehavior;
            Priority = priority;
        }
    }
}

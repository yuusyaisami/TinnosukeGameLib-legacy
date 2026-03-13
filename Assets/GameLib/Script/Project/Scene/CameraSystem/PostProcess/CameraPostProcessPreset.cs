#nullable enable
using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.CameraSystem
{
    public interface ICameraPostProcessPreset
    {
        void Apply(string layerTag, ICameraPostProcessService service);
    }

    [Serializable]
    public sealed class CameraPostProcessPreset : ICameraPostProcessPreset
    {
        [BoxGroup("Bloom")]
        [ToggleLeft]
        public bool bloomEnabled = false;

        [BoxGroup("Bloom")]
        [ShowIf(nameof(bloomEnabled))]
        [LabelText("Threshold")]
        [InlineProperty]
        public CameraPostProcessFloatSetting bloomThreshold;

        [BoxGroup("Bloom")]
        [ShowIf(nameof(bloomEnabled))]
        [LabelText("Intensity")]
        [InlineProperty]
        public CameraPostProcessFloatSetting bloomIntensity;

        [BoxGroup("Bloom")]
        [ShowIf(nameof(bloomEnabled))]
        [LabelText("Scatter")]
        [InlineProperty]
        public CameraPostProcessFloatSetting bloomScatter;

        [BoxGroup("Bloom")]
        [ShowIf(nameof(bloomEnabled))]
        [LabelText("Clamp")]
        [InlineProperty]
        public CameraPostProcessFloatSetting bloomClamp;

        [BoxGroup("Bloom")]
        [ShowIf(nameof(bloomEnabled))]
        [LabelText("Tint")]
        [InlineProperty]
        public CameraPostProcessColorSetting bloomTint;

        [BoxGroup("Vignette")]
        [ToggleLeft]
        public bool vignetteEnabled = false;

        [BoxGroup("Vignette")]
        [ShowIf(nameof(vignetteEnabled))]
        [LabelText("Color")]
        [InlineProperty]
        public CameraPostProcessColorSetting vignetteColor;

        [BoxGroup("Vignette")]
        [ShowIf(nameof(vignetteEnabled))]
        [LabelText("Center")]
        [InlineProperty]
        public CameraPostProcessVector2Setting vignetteCenter;

        [BoxGroup("Vignette")]
        [ShowIf(nameof(vignetteEnabled))]
        [LabelText("Intensity")]
        [InlineProperty]
        public CameraPostProcessFloatSetting vignetteIntensity;

        [BoxGroup("Vignette")]
        [ShowIf(nameof(vignetteEnabled))]
        [LabelText("Smoothness")]
        [InlineProperty]
        public CameraPostProcessFloatSetting vignetteSmoothness;

        [BoxGroup("Vignette")]
        [ShowIf(nameof(vignetteEnabled))]
        [LabelText("Rounded")]
        [InlineProperty]
        public CameraPostProcessBoolSetting vignetteRounded;

        [BoxGroup("Color Adjustments")]
        [ToggleLeft]
        public bool colorAdjustmentsEnabled = false;

        [BoxGroup("Color Adjustments")]
        [ShowIf(nameof(colorAdjustmentsEnabled))]
        [LabelText("Post Exposure")]
        [InlineProperty]
        public CameraPostProcessFloatSetting postExposure;

        [BoxGroup("Color Adjustments")]
        [ShowIf(nameof(colorAdjustmentsEnabled))]
        [LabelText("Contrast")]
        [InlineProperty]
        public CameraPostProcessFloatSetting contrast;

        [BoxGroup("Color Adjustments")]
        [ShowIf(nameof(colorAdjustmentsEnabled))]
        [LabelText("Color Filter")]
        [InlineProperty]
        public CameraPostProcessColorSetting colorFilter;

        [BoxGroup("Color Adjustments")]
        [ShowIf(nameof(colorAdjustmentsEnabled))]
        [LabelText("Hue Shift")]
        [InlineProperty]
        public CameraPostProcessFloatSetting hueShift;

        [BoxGroup("Color Adjustments")]
        [ShowIf(nameof(colorAdjustmentsEnabled))]
        [LabelText("Saturation")]
        [InlineProperty]
        public CameraPostProcessFloatSetting saturation;

        [BoxGroup("Split Toning")]
        [ToggleLeft]
        public bool splitToningEnabled = false;

        [BoxGroup("Split Toning")]
        [ShowIf(nameof(splitToningEnabled))]
        [LabelText("Shadows")]
        [InlineProperty]
        public CameraPostProcessColorSetting splitShadows;

        [BoxGroup("Split Toning")]
        [ShowIf(nameof(splitToningEnabled))]
        [LabelText("Highlights")]
        [InlineProperty]
        public CameraPostProcessColorSetting splitHighlights;

        [BoxGroup("Split Toning")]
        [ShowIf(nameof(splitToningEnabled))]
        [LabelText("Balance")]
        [InlineProperty]
        public CameraPostProcessFloatSetting splitBalance;

        [BoxGroup("Chromatic Aberration")]
        [ToggleLeft]
        public bool chromaticAberrationEnabled = false;

        [BoxGroup("Chromatic Aberration")]
        [ShowIf(nameof(chromaticAberrationEnabled))]
        [LabelText("Intensity")]
        [InlineProperty]
        public CameraPostProcessFloatSetting chromaticIntensity;

        public void Apply(string layerTag, ICameraPostProcessService service)
        {
            if (service == null || string.IsNullOrEmpty(layerTag))
                return;

            if (bloomEnabled)
            {
                bloomThreshold.Apply(layerTag, service, CameraPostProcessFloatParam.BloomThreshold);
                bloomIntensity.Apply(layerTag, service, CameraPostProcessFloatParam.BloomIntensity);
                bloomScatter.Apply(layerTag, service, CameraPostProcessFloatParam.BloomScatter);
                bloomClamp.Apply(layerTag, service, CameraPostProcessFloatParam.BloomClamp);
                bloomTint.Apply(layerTag, service, CameraPostProcessColorParam.BloomTint);
            }
            else
            {
                service.ClearLayer(layerTag, CameraPostProcessFloatParam.BloomThreshold);
                service.ClearLayer(layerTag, CameraPostProcessFloatParam.BloomIntensity);
                service.ClearLayer(layerTag, CameraPostProcessFloatParam.BloomScatter);
                service.ClearLayer(layerTag, CameraPostProcessFloatParam.BloomClamp);
                service.ClearLayer(layerTag, CameraPostProcessColorParam.BloomTint);
            }

            if (vignetteEnabled)
            {
                vignetteColor.Apply(layerTag, service, CameraPostProcessColorParam.VignetteColor);
                vignetteCenter.Apply(layerTag, service, CameraPostProcessVector2Param.VignetteCenter);
                vignetteIntensity.Apply(layerTag, service, CameraPostProcessFloatParam.VignetteIntensity);
                vignetteSmoothness.Apply(layerTag, service, CameraPostProcessFloatParam.VignetteSmoothness);
                vignetteRounded.Apply(layerTag, service, CameraPostProcessBoolParam.VignetteRounded);
            }
            else
            {
                service.ClearLayer(layerTag, CameraPostProcessColorParam.VignetteColor);
                service.ClearLayer(layerTag, CameraPostProcessVector2Param.VignetteCenter);
                service.ClearLayer(layerTag, CameraPostProcessFloatParam.VignetteIntensity);
                service.ClearLayer(layerTag, CameraPostProcessFloatParam.VignetteSmoothness);
                service.ClearLayer(layerTag, CameraPostProcessBoolParam.VignetteRounded);
            }

            if (colorAdjustmentsEnabled)
            {
                postExposure.Apply(layerTag, service, CameraPostProcessFloatParam.ColorAdjustmentsPostExposure);
                contrast.Apply(layerTag, service, CameraPostProcessFloatParam.ColorAdjustmentsContrast);
                colorFilter.Apply(layerTag, service, CameraPostProcessColorParam.ColorAdjustmentsColorFilter);
                hueShift.Apply(layerTag, service, CameraPostProcessFloatParam.ColorAdjustmentsHueShift);
                saturation.Apply(layerTag, service, CameraPostProcessFloatParam.ColorAdjustmentsSaturation);
            }
            else
            {
                service.ClearLayer(layerTag, CameraPostProcessFloatParam.ColorAdjustmentsPostExposure);
                service.ClearLayer(layerTag, CameraPostProcessFloatParam.ColorAdjustmentsContrast);
                service.ClearLayer(layerTag, CameraPostProcessColorParam.ColorAdjustmentsColorFilter);
                service.ClearLayer(layerTag, CameraPostProcessFloatParam.ColorAdjustmentsHueShift);
                service.ClearLayer(layerTag, CameraPostProcessFloatParam.ColorAdjustmentsSaturation);
            }

            if (splitToningEnabled)
            {
                splitShadows.Apply(layerTag, service, CameraPostProcessColorParam.SplitToningShadows);
                splitHighlights.Apply(layerTag, service, CameraPostProcessColorParam.SplitToningHighlights);
                splitBalance.Apply(layerTag, service, CameraPostProcessFloatParam.SplitToningBalance);
            }
            else
            {
                service.ClearLayer(layerTag, CameraPostProcessColorParam.SplitToningShadows);
                service.ClearLayer(layerTag, CameraPostProcessColorParam.SplitToningHighlights);
                service.ClearLayer(layerTag, CameraPostProcessFloatParam.SplitToningBalance);
            }

            if (chromaticAberrationEnabled)
            {
                chromaticIntensity.Apply(layerTag, service, CameraPostProcessFloatParam.ChromaticAberrationIntensity);
            }
            else
            {
                service.ClearLayer(layerTag, CameraPostProcessFloatParam.ChromaticAberrationIntensity);
            }
        }
    }

    [Serializable]
    public struct CameraPostProcessFloatSetting
    {
        [ToggleLeft]
        [LabelText("Send")]
        public bool Enabled;

        [ShowIf(nameof(Enabled))]
        public float Value;

        [ToggleLeft]
        [LabelText("Animate")]
        [ShowIf(nameof(Enabled))]
        public bool Animate;

        [ShowIf(nameof(ShowAnimationFields))]
        [MinValue(0f)]
        public float Duration;

        [ShowIf(nameof(ShowAnimationFields))]
        public Ease Ease;

        bool ShowAnimationFields => Enabled && Animate;

        public void Apply(string layerTag, ICameraPostProcessService service, CameraPostProcessFloatParam param)
        {
            if (service == null || string.IsNullOrEmpty(layerTag))
                return;

            if (!Enabled)
            {
                service.ClearLayer(layerTag, param);
                return;
            }

            if (Animate)
            {
                var easing = Ease == Ease.Unset ? Ease.Linear : Ease;
                service.SetLayer(layerTag, param, Value, Duration, easing);
                return;
            }

            service.SetLayer(layerTag, param, Value);
        }
    }

    [Serializable]
    public struct CameraPostProcessColorSetting
    {
        [ToggleLeft]
        [LabelText("Send")]
        public bool Enabled;

        [ShowIf(nameof(Enabled))]
        public Color Value;

        [ToggleLeft]
        [LabelText("Animate")]
        [ShowIf(nameof(Enabled))]
        public bool Animate;

        [ShowIf(nameof(ShowAnimationFields))]
        [MinValue(0f)]
        public float Duration;

        [ShowIf(nameof(ShowAnimationFields))]
        public Ease Ease;

        bool ShowAnimationFields => Enabled && Animate;

        public void Apply(string layerTag, ICameraPostProcessService service, CameraPostProcessColorParam param)
        {
            if (service == null || string.IsNullOrEmpty(layerTag))
                return;

            if (!Enabled)
            {
                service.ClearLayer(layerTag, param);
                return;
            }

            if (Animate)
            {
                var easing = Ease == Ease.Unset ? Ease.Linear : Ease;
                service.SetLayer(layerTag, param, Value, Duration, easing);
                return;
            }

            service.SetLayer(layerTag, param, Value);
        }
    }

    [Serializable]
    public struct CameraPostProcessVector2Setting
    {
        [ToggleLeft]
        [LabelText("Send")]
        public bool Enabled;

        [ShowIf(nameof(Enabled))]
        public Vector2 Value;

        [ToggleLeft]
        [LabelText("Animate")]
        [ShowIf(nameof(Enabled))]
        public bool Animate;

        [ShowIf(nameof(ShowAnimationFields))]
        [MinValue(0f)]
        public float Duration;

        [ShowIf(nameof(ShowAnimationFields))]
        public Ease Ease;

        bool ShowAnimationFields => Enabled && Animate;

        public void Apply(string layerTag, ICameraPostProcessService service, CameraPostProcessVector2Param param)
        {
            if (service == null || string.IsNullOrEmpty(layerTag))
                return;

            if (!Enabled)
            {
                service.ClearLayer(layerTag, param);
                return;
            }

            if (Animate)
            {
                var easing = Ease == Ease.Unset ? Ease.Linear : Ease;
                service.SetLayer(layerTag, param, Value, Duration, easing);
                return;
            }

            service.SetLayer(layerTag, param, Value);
        }
    }

    [Serializable]
    public struct CameraPostProcessBoolSetting
    {
        [ToggleLeft]
        [LabelText("Send")]
        public bool Enabled;

        [ShowIf(nameof(Enabled))]
        public bool Value;

        public void Apply(string layerTag, ICameraPostProcessService service, CameraPostProcessBoolParam param)
        {
            if (service == null || string.IsNullOrEmpty(layerTag))
                return;

            if (!Enabled)
            {
                service.ClearLayer(layerTag, param);
                return;
            }

            service.SetLayer(layerTag, param, Value);
        }
    }
}

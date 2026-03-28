#nullable enable
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Game.Channel
{
    public enum Light2DChannelHubControlOperation
    {
        SwapSourcePreset = 10,
    }

    public enum Light2DChannelPlayerControlOperation
    {
        SwapPlayerPreset = 10,
        MutatePlayerPreset = 20,
        SetGlobalIntensity = 30,
        ResetGlobalIntensity = 40,
        ReplaceEffect = 50,
        MutateEffect = 60,
        SetEffectEnabled = 70,
        RemoveEffect = 80,
        ResetRuntimeOverrides = 90,
        RestoreBaseline = 100,
    }

    public enum Light2DEffectBlendMode
    {
        Override = 10,
        Add = 20,
        Multiply = 30,
    }

    public interface ILight2DChannelPlayer
    {
        string Tag { get; }
        string GlobalLinkKey { get; }
        Light2D Target { get; }
        float SelfGlobalIntensity { get; }
        float InheritedGlobalIntensity { get; }
        float EffectiveGlobalIntensity { get; }
    }

    public interface ILight2DChannelControlService
    {
        bool SwapPlayerPreset(Light2DPlayerPreset? preset);
        bool MutatePlayerPreset(Light2DPlayerRuntimeMutation? mutation);
        bool SetGlobalIntensity(float intensity);
        bool ResetGlobalIntensity();
        bool ReplaceEffect(
            string effectId,
            Light2DEffectPresetBase? preset,
            int priority,
            Light2DEffectBlendMode blendMode,
            bool enabled);
        bool MutateEffect(string effectId, Light2DEffectRuntimeMutationBase? mutation);
        bool SetEffectEnabled(string effectId, bool enabled);
        bool RemoveEffect(string effectId);
        bool ResetRuntimeOverrides(bool resetPlayerPreset, bool resetEffects, bool resetGlobalIntensity);
        void RestoreBaseline();
    }

    public interface ILight2DChannelHubService : IChannelHubService
    {
        IReadOnlyList<ILight2DChannelPlayer> Players { get; }
        bool TryGetPlayer(string tag, out ILight2DChannelPlayer? player);
        bool TryGetControl(string tag, out ILight2DChannelControlService? control);
        bool SwapSourcePreset(string tag, Light2DPreset? preset);
    }

    [Serializable]
    public sealed class Light2DLocalState
    {
        [BoxGroup("State")]
        [LabelText("Enabled")]
        public bool Enabled = true;

        [BoxGroup("State")]
        [LabelText("Light Type")]
        public Light2D.LightType LightType = Light2D.LightType.Point;

        [BoxGroup("Common")]
        [LabelText("Color")]
        public UnityEngine.Color Color = UnityEngine.Color.white;

        [BoxGroup("Common")]
        [LabelText("Intensity")]
        [MinValue(0f)]
        public float Intensity = 1f;

        [BoxGroup("Common")]
        [LabelText("Blend Style Index")]
        public int BlendStyleIndex;

        [BoxGroup("Common")]
        [LabelText("Falloff Intensity")]
        [MinValue(0f)]
        public float FalloffIntensity = 0.5f;

        [BoxGroup("Common")]
        [LabelText("Overlap Operation")]
        public Light2D.OverlapOperation OverlapOperation;

        [BoxGroup("Common")]
        [LabelText("Light Order")]
        public int LightOrder;

        [BoxGroup("Volume")]
        [LabelText("Volume Intensity")]
        [MinValue(0f)]
        public float VolumeIntensity;

        [BoxGroup("Volume")]
        [LabelText("Volumetric Enabled")]
        public bool VolumetricEnabled;

        [BoxGroup("Shadow")]
        [LabelText("Shadows Enabled")]
        public bool ShadowsEnabled;

        [BoxGroup("Shadow")]
        [LabelText("Shadow Intensity")]
        [MinValue(0f)]
        public float ShadowIntensity = 1f;

        [BoxGroup("Shadow")]
        [LabelText("Shadow Softness")]
        [MinValue(0f)]
        public float ShadowSoftness;

        [BoxGroup("Shadow")]
        [LabelText("Shadow Softness Falloff")]
        [MinValue(0f)]
        public float ShadowSoftnessFalloffIntensity = 1f;

        [BoxGroup("Shadow")]
        [LabelText("Shadow Volume Intensity")]
        [MinValue(0f)]
        public float ShadowVolumeIntensity;

        [BoxGroup("Shadow")]
        [LabelText("Volumetric Shadows Enabled")]
        public bool VolumetricShadowsEnabled;

        [BoxGroup("Sorting")]
        [LabelText("Target Sorting Layers")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
        public int[] TargetSortingLayers = Array.Empty<int>();

        [BoxGroup("Cookie")]
        [ShowIf(nameof(UsesCookieSpriteField))]
        [LabelText("Cookie Sprite")]
        public UnityEngine.Sprite? CookieSprite;

        [BoxGroup("Point")]
        [ShowIf(nameof(UsesPointLightFields))]
        [LabelText("Inner Angle")]
        [MinValue(0f)]
        public float PointLightInnerAngle;

        [BoxGroup("Point")]
        [ShowIf(nameof(UsesPointLightFields))]
        [LabelText("Outer Angle")]
        [MinValue(0f)]
        public float PointLightOuterAngle = 360f;

        [BoxGroup("Point")]
        [ShowIf(nameof(UsesPointLightFields))]
        [LabelText("Inner Radius")]
        [MinValue(0f)]
        public float PointLightInnerRadius;

        [BoxGroup("Point")]
        [ShowIf(nameof(UsesPointLightFields))]
        [LabelText("Outer Radius")]
        [MinValue(0f)]
        public float PointLightOuterRadius = 1f;

        [BoxGroup("Shape")]
        [ShowIf(nameof(UsesShapeFalloffField))]
        [LabelText("Shape Falloff Size")]
        [MinValue(0f)]
        public float ShapeLightFalloffSize = 0.5f;

        [BoxGroup("Shape")]
        [ShowIf(nameof(UsesShapePathField))]
        [LabelText("Shape Path")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
        public UnityEngine.Vector3[] ShapePath = Array.Empty<UnityEngine.Vector3>();

        bool UsesCookieSpriteField =>
            LightType == Light2D.LightType.Point || LightType == Light2D.LightType.Sprite;

        bool UsesPointLightFields => LightType == Light2D.LightType.Point;

        bool UsesShapeFalloffField =>
            LightType == Light2D.LightType.Freeform || LightType == Light2D.LightType.Parametric;

        bool UsesShapePathField => LightType == Light2D.LightType.Freeform;

        public Light2DLocalState CreateRuntimeCopy()
        {
            return new Light2DLocalState
            {
                Enabled = Enabled,
                LightType = LightType,
                Color = Color,
                Intensity = Intensity,
                BlendStyleIndex = BlendStyleIndex,
                FalloffIntensity = FalloffIntensity,
                OverlapOperation = OverlapOperation,
                LightOrder = LightOrder,
                VolumeIntensity = VolumeIntensity,
                VolumetricEnabled = VolumetricEnabled,
                ShadowsEnabled = ShadowsEnabled,
                ShadowIntensity = ShadowIntensity,
                ShadowSoftness = ShadowSoftness,
                ShadowSoftnessFalloffIntensity = ShadowSoftnessFalloffIntensity,
                ShadowVolumeIntensity = ShadowVolumeIntensity,
                VolumetricShadowsEnabled = VolumetricShadowsEnabled,
                TargetSortingLayers = CloneSortingLayers(TargetSortingLayers),
                CookieSprite = CookieSprite,
                PointLightInnerAngle = PointLightInnerAngle,
                PointLightOuterAngle = PointLightOuterAngle,
                PointLightInnerRadius = PointLightInnerRadius,
                PointLightOuterRadius = PointLightOuterRadius,
                ShapeLightFalloffSize = ShapeLightFalloffSize,
                ShapePath = CloneShapePath(ShapePath),
            };
        }

        public static Light2DLocalState CaptureFrom(Light2D light)
        {
            return new Light2DLocalState
            {
                Enabled = light != null && light.enabled,
                LightType = light != null ? light.lightType : Light2D.LightType.Point,
                Color = light != null ? light.color : UnityEngine.Color.white,
                Intensity = light != null ? light.intensity : 1f,
                BlendStyleIndex = light != null ? light.blendStyleIndex : 0,
                FalloffIntensity = light != null ? light.falloffIntensity : 0.5f,
                OverlapOperation = light != null ? light.overlapOperation : default,
                LightOrder = light != null ? light.lightOrder : 0,
                VolumeIntensity = light != null ? light.volumeIntensity : 0f,
                VolumetricEnabled = light != null && light.volumetricEnabled,
                ShadowsEnabled = light != null && light.shadowsEnabled,
                ShadowIntensity = light != null ? light.shadowIntensity : 1f,
                ShadowSoftness = light != null ? light.shadowSoftness : 0f,
                ShadowSoftnessFalloffIntensity = light != null ? light.shadowSoftnessFalloffIntensity : 1f,
                ShadowVolumeIntensity = light != null ? light.shadowVolumeIntensity : 0f,
                VolumetricShadowsEnabled = light != null && light.volumetricShadowsEnabled,
                TargetSortingLayers = light != null ? CloneSortingLayers(light.targetSortingLayers) : Array.Empty<int>(),
                CookieSprite = light != null ? light.lightCookieSprite : null,
                PointLightInnerAngle = light != null ? light.pointLightInnerAngle : 0f,
                PointLightOuterAngle = light != null ? light.pointLightOuterAngle : 360f,
                PointLightInnerRadius = light != null ? light.pointLightInnerRadius : 0f,
                PointLightOuterRadius = light != null ? light.pointLightOuterRadius : 1f,
                ShapeLightFalloffSize = light != null ? light.shapeLightFalloffSize : 0.5f,
                ShapePath = light != null ? CloneShapePath(light.shapePath) : Array.Empty<UnityEngine.Vector3>(),
            };
        }

        public void ApplyTo(Light2D light, bool allowRuntimeLightTypeChange)
        {
            if (light == null)
                return;

            light.enabled = Enabled;
            if (allowRuntimeLightTypeChange)
                light.lightType = LightType;

            light.color = Color;
            light.intensity = Mathf.Max(0f, Intensity);
            light.blendStyleIndex = BlendStyleIndex;
            light.falloffIntensity = Mathf.Max(0f, FalloffIntensity);
            light.overlapOperation = OverlapOperation;
            light.lightOrder = LightOrder;
            light.volumeIntensity = Mathf.Max(0f, VolumeIntensity);
            light.volumetricEnabled = VolumetricEnabled;
            light.shadowsEnabled = ShadowsEnabled;
            light.shadowIntensity = Mathf.Max(0f, ShadowIntensity);
            light.shadowSoftness = Mathf.Max(0f, ShadowSoftness);
            light.shadowSoftnessFalloffIntensity = Mathf.Max(0f, ShadowSoftnessFalloffIntensity);
            light.shadowVolumeIntensity = Mathf.Max(0f, ShadowVolumeIntensity);
            light.volumetricShadowsEnabled = VolumetricShadowsEnabled;
            light.targetSortingLayers = CloneSortingLayers(TargetSortingLayers);
            light.lightCookieSprite = CookieSprite;
            light.pointLightInnerAngle = Mathf.Max(0f, PointLightInnerAngle);
            light.pointLightOuterAngle = Mathf.Max(0f, PointLightOuterAngle);
            light.pointLightInnerRadius = Mathf.Max(0f, PointLightInnerRadius);
            light.pointLightOuterRadius = Mathf.Max(0f, PointLightOuterRadius);
            light.shapeLightFalloffSize = Mathf.Max(0f, ShapeLightFalloffSize);

            if (ShapePath != null && ShapePath.Length > 0)
                light.SetShapePath(CloneShapePath(ShapePath));
        }

        public static int[] CloneSortingLayers(int[]? source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<int>();

            var copy = new int[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }

        public static UnityEngine.Vector3[] CloneShapePath(UnityEngine.Vector3[]? source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<UnityEngine.Vector3>();

            var copy = new UnityEngine.Vector3[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }
    }

    [Serializable]
    public sealed class Light2DContributionState
    {
        [BoxGroup("Common")]
        public bool HasEnabled;

        [BoxGroup("Common")]
        public bool Enabled;

        [BoxGroup("State")]
        public bool HasLightType;

        [BoxGroup("State")]
        public Light2D.LightType LightType = Light2D.LightType.Point;

        [BoxGroup("Common")]
        public bool HasColor;

        [BoxGroup("Common")]
        public UnityEngine.Color Color = UnityEngine.Color.white;

        [BoxGroup("Common")]
        public bool HasIntensity;

        [BoxGroup("Common")]
        public float Intensity = 1f;

        [BoxGroup("Common")]
        public bool HasBlendStyleIndex;

        [BoxGroup("Common")]
        public int BlendStyleIndex;

        [BoxGroup("Common")]
        public bool HasFalloffIntensity;

        [BoxGroup("Common")]
        public float FalloffIntensity = 0.5f;

        [BoxGroup("Common")]
        public bool HasOverlapOperation;

        [BoxGroup("Common")]
        public Light2D.OverlapOperation OverlapOperation;

        [BoxGroup("Common")]
        public bool HasLightOrder;

        [BoxGroup("Common")]
        public int LightOrder;

        [BoxGroup("Volume")]
        public bool HasVolumeIntensity;

        [BoxGroup("Volume")]
        public float VolumeIntensity;

        [BoxGroup("Volume")]
        public bool HasVolumetricEnabled;

        [BoxGroup("Volume")]
        public bool VolumetricEnabled;

        [BoxGroup("Shadow")]
        public bool HasShadowsEnabled;

        [BoxGroup("Shadow")]
        public bool ShadowsEnabled;

        [BoxGroup("Shadow")]
        public bool HasShadowIntensity;

        [BoxGroup("Shadow")]
        public float ShadowIntensity = 1f;

        [BoxGroup("Shadow")]
        public bool HasShadowSoftness;

        [BoxGroup("Shadow")]
        public float ShadowSoftness;

        [BoxGroup("Shadow")]
        public bool HasShadowSoftnessFalloffIntensity;

        [BoxGroup("Shadow")]
        public float ShadowSoftnessFalloffIntensity = 1f;

        [BoxGroup("Shadow")]
        public bool HasShadowVolumeIntensity;

        [BoxGroup("Shadow")]
        public float ShadowVolumeIntensity;

        [BoxGroup("Shadow")]
        public bool HasVolumetricShadowsEnabled;

        [BoxGroup("Shadow")]
        public bool VolumetricShadowsEnabled;

        [BoxGroup("Sorting")]
        public bool HasTargetSortingLayers;

        [BoxGroup("Sorting")]
        public int[] TargetSortingLayers = Array.Empty<int>();

        [BoxGroup("Cookie")]
        [ShowIf(nameof(UsesCookieSpriteField))]
        public bool HasCookieSprite;

        [BoxGroup("Cookie")]
        [ShowIf(nameof(UsesCookieSpriteField))]
        public UnityEngine.Sprite? CookieSprite;

        [BoxGroup("Point")]
        [ShowIf(nameof(UsesPointLightFields))]
        public bool HasPointLightInnerAngle;

        [BoxGroup("Point")]
        [ShowIf(nameof(UsesPointLightFields))]
        public float PointLightInnerAngle;

        [BoxGroup("Point")]
        [ShowIf(nameof(UsesPointLightFields))]
        public bool HasPointLightOuterAngle;

        [BoxGroup("Point")]
        [ShowIf(nameof(UsesPointLightFields))]
        public float PointLightOuterAngle = 360f;

        [BoxGroup("Point")]
        [ShowIf(nameof(UsesPointLightFields))]
        public bool HasPointLightInnerRadius;

        [BoxGroup("Point")]
        [ShowIf(nameof(UsesPointLightFields))]
        public float PointLightInnerRadius;

        [BoxGroup("Point")]
        [ShowIf(nameof(UsesPointLightFields))]
        public bool HasPointLightOuterRadius;

        [BoxGroup("Point")]
        [ShowIf(nameof(UsesPointLightFields))]
        public float PointLightOuterRadius = 1f;

        [BoxGroup("Shape")]
        [ShowIf(nameof(UsesShapeFalloffField))]
        public bool HasShapeLightFalloffSize;

        [BoxGroup("Shape")]
        [ShowIf(nameof(UsesShapeFalloffField))]
        public float ShapeLightFalloffSize = 0.5f;

        [BoxGroup("Shape")]
        [ShowIf(nameof(UsesShapePathField))]
        public bool HasShapePath;

        [BoxGroup("Shape")]
        [ShowIf(nameof(UsesShapePathField))]
        public UnityEngine.Vector3[] ShapePath = Array.Empty<UnityEngine.Vector3>();

        bool UsesCookieSpriteField =>
            LightType == Light2D.LightType.Point || LightType == Light2D.LightType.Sprite;

        bool UsesPointLightFields => LightType == Light2D.LightType.Point;

        bool UsesShapeFalloffField =>
            LightType == Light2D.LightType.Freeform || LightType == Light2D.LightType.Parametric;

        bool UsesShapePathField => LightType == Light2D.LightType.Freeform;

        public bool HasAnyValue()
        {
            return HasEnabled ||
                   HasLightType ||
                   HasColor ||
                   HasIntensity ||
                   HasBlendStyleIndex ||
                   HasFalloffIntensity ||
                   HasOverlapOperation ||
                   HasLightOrder ||
                   HasVolumeIntensity ||
                   HasVolumetricEnabled ||
                   HasShadowsEnabled ||
                   HasShadowIntensity ||
                   HasShadowSoftness ||
                   HasShadowSoftnessFalloffIntensity ||
                   HasShadowVolumeIntensity ||
                   HasVolumetricShadowsEnabled ||
                   HasTargetSortingLayers ||
                   HasCookieSprite ||
                   HasPointLightInnerAngle ||
                   HasPointLightOuterAngle ||
                   HasPointLightInnerRadius ||
                   HasPointLightOuterRadius ||
                   HasShapeLightFalloffSize ||
                   HasShapePath;
        }

        public Light2DContributionState CreateRuntimeCopy()
        {
            return new Light2DContributionState
            {
                HasEnabled = HasEnabled,
                Enabled = Enabled,
                HasLightType = HasLightType,
                LightType = LightType,
                HasColor = HasColor,
                Color = Color,
                HasIntensity = HasIntensity,
                Intensity = Intensity,
                HasBlendStyleIndex = HasBlendStyleIndex,
                BlendStyleIndex = BlendStyleIndex,
                HasFalloffIntensity = HasFalloffIntensity,
                FalloffIntensity = FalloffIntensity,
                HasOverlapOperation = HasOverlapOperation,
                OverlapOperation = OverlapOperation,
                HasLightOrder = HasLightOrder,
                LightOrder = LightOrder,
                HasVolumeIntensity = HasVolumeIntensity,
                VolumeIntensity = VolumeIntensity,
                HasVolumetricEnabled = HasVolumetricEnabled,
                VolumetricEnabled = VolumetricEnabled,
                HasShadowsEnabled = HasShadowsEnabled,
                ShadowsEnabled = ShadowsEnabled,
                HasShadowIntensity = HasShadowIntensity,
                ShadowIntensity = ShadowIntensity,
                HasShadowSoftness = HasShadowSoftness,
                ShadowSoftness = ShadowSoftness,
                HasShadowSoftnessFalloffIntensity = HasShadowSoftnessFalloffIntensity,
                ShadowSoftnessFalloffIntensity = ShadowSoftnessFalloffIntensity,
                HasShadowVolumeIntensity = HasShadowVolumeIntensity,
                ShadowVolumeIntensity = ShadowVolumeIntensity,
                HasVolumetricShadowsEnabled = HasVolumetricShadowsEnabled,
                VolumetricShadowsEnabled = VolumetricShadowsEnabled,
                HasTargetSortingLayers = HasTargetSortingLayers,
                TargetSortingLayers = Light2DLocalState.CloneSortingLayers(TargetSortingLayers),
                HasCookieSprite = HasCookieSprite,
                CookieSprite = CookieSprite,
                HasPointLightInnerAngle = HasPointLightInnerAngle,
                PointLightInnerAngle = PointLightInnerAngle,
                HasPointLightOuterAngle = HasPointLightOuterAngle,
                PointLightOuterAngle = PointLightOuterAngle,
                HasPointLightInnerRadius = HasPointLightInnerRadius,
                PointLightInnerRadius = PointLightInnerRadius,
                HasPointLightOuterRadius = HasPointLightOuterRadius,
                PointLightOuterRadius = PointLightOuterRadius,
                HasShapeLightFalloffSize = HasShapeLightFalloffSize,
                ShapeLightFalloffSize = ShapeLightFalloffSize,
                HasShapePath = HasShapePath,
                ShapePath = Light2DLocalState.CloneShapePath(ShapePath),
            };
        }
    }

    internal readonly struct Light2DEffectEvaluationContext
    {
        public readonly float ElapsedTime;
        public readonly Light2DLocalState CurrentState;

        public Light2DEffectEvaluationContext(float elapsedTime, Light2DLocalState currentState)
        {
            ElapsedTime = elapsedTime;
            CurrentState = currentState;
        }
    }
}

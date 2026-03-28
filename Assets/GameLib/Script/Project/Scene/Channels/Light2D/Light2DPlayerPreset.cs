#nullable enable
using System;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Game.Channel
{
    [Serializable]
    public sealed class Light2DPlayerPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Local")]
        [InlineProperty]
        [HideLabel]
        [SerializeField]
        Light2DLocalState _localState = new();

        [BoxGroup("Global")]
        [LabelText("Global Intensity")]
        [MinValue(0f)]
        [Tooltip("この channel 自身が子孫へ伝播させる intensity 乗算値です。")]
        [SerializeField]
        float _globalIntensity = 1f;

        public Light2DLocalState LocalState => _localState;
        public float GlobalIntensity => Mathf.Max(0f, _globalIntensity);

        public Light2DPlayerPreset CreateRuntimeCopy()
        {
            return new Light2DPlayerPreset
            {
                _localState = _localState?.CreateRuntimeCopy() ?? new Light2DLocalState(),
                _globalIntensity = GlobalIntensity,
            };
        }

        internal void ApplyMutation(Light2DPlayerRuntimeMutation mutation)
        {
            if (mutation == null)
                return;

            ApplyContribution(_localState, mutation.LocalState);

            if (mutation.ApplyGlobalIntensity)
                _globalIntensity = Mathf.Max(0f, mutation.GlobalIntensity);
        }

        internal static void ApplyContribution(Light2DLocalState localState, Light2DContributionState? contribution)
        {
            if (localState == null || contribution == null || !contribution.HasAnyValue())
                return;

            if (contribution.HasEnabled)
                localState.Enabled = contribution.Enabled;
            if (contribution.HasLightType)
                localState.LightType = contribution.LightType;
            if (contribution.HasColor)
                localState.Color = contribution.Color;
            if (contribution.HasIntensity)
                localState.Intensity = Mathf.Max(0f, contribution.Intensity);
            if (contribution.HasBlendStyleIndex)
                localState.BlendStyleIndex = contribution.BlendStyleIndex;
            if (contribution.HasFalloffIntensity)
                localState.FalloffIntensity = Mathf.Max(0f, contribution.FalloffIntensity);
            if (contribution.HasOverlapOperation)
                localState.OverlapOperation = contribution.OverlapOperation;
            if (contribution.HasLightOrder)
                localState.LightOrder = contribution.LightOrder;
            if (contribution.HasVolumeIntensity)
                localState.VolumeIntensity = Mathf.Max(0f, contribution.VolumeIntensity);
            if (contribution.HasVolumetricEnabled)
                localState.VolumetricEnabled = contribution.VolumetricEnabled;
            if (contribution.HasShadowsEnabled)
                localState.ShadowsEnabled = contribution.ShadowsEnabled;
            if (contribution.HasShadowIntensity)
                localState.ShadowIntensity = Mathf.Max(0f, contribution.ShadowIntensity);
            if (contribution.HasShadowSoftness)
                localState.ShadowSoftness = Mathf.Max(0f, contribution.ShadowSoftness);
            if (contribution.HasShadowSoftnessFalloffIntensity)
                localState.ShadowSoftnessFalloffIntensity = Mathf.Max(0f, contribution.ShadowSoftnessFalloffIntensity);
            if (contribution.HasShadowVolumeIntensity)
                localState.ShadowVolumeIntensity = Mathf.Max(0f, contribution.ShadowVolumeIntensity);
            if (contribution.HasVolumetricShadowsEnabled)
                localState.VolumetricShadowsEnabled = contribution.VolumetricShadowsEnabled;
            if (contribution.HasTargetSortingLayers)
                localState.TargetSortingLayers = Light2DLocalState.CloneSortingLayers(contribution.TargetSortingLayers);
            if (contribution.HasCookieSprite)
                localState.CookieSprite = contribution.CookieSprite;
            if (contribution.HasPointLightInnerAngle)
                localState.PointLightInnerAngle = Mathf.Max(0f, contribution.PointLightInnerAngle);
            if (contribution.HasPointLightOuterAngle)
                localState.PointLightOuterAngle = Mathf.Max(0f, contribution.PointLightOuterAngle);
            if (contribution.HasPointLightInnerRadius)
                localState.PointLightInnerRadius = Mathf.Max(0f, contribution.PointLightInnerRadius);
            if (contribution.HasPointLightOuterRadius)
                localState.PointLightOuterRadius = Mathf.Max(0f, contribution.PointLightOuterRadius);
            if (contribution.HasShapeLightFalloffSize)
                localState.ShapeLightFalloffSize = Mathf.Max(0f, contribution.ShapeLightFalloffSize);
            if (contribution.HasShapePath)
                localState.ShapePath = Light2DLocalState.CloneShapePath(contribution.ShapePath);
        }
    }

    [Serializable]
    public sealed class Light2DPlayerRuntimeMutation : IDynamicManagedRefValue
    {
        [BoxGroup("Local")]
        [InlineProperty]
        [HideLabel]
        [SerializeField]
        Light2DContributionState _localState = new();

        [BoxGroup("Global")]
        [ToggleLeft]
        [LabelText("Apply Global Intensity")]
        [SerializeField]
        bool _applyGlobalIntensity;

        [BoxGroup("Global")]
        [ShowIf(nameof(_applyGlobalIntensity))]
        [LabelText("Global Intensity")]
        [MinValue(0f)]
        [SerializeField]
        float _globalIntensity = 1f;

        public Light2DContributionState LocalState => _localState;
        public bool ApplyGlobalIntensity => _applyGlobalIntensity;
        public float GlobalIntensity => Mathf.Max(0f, _globalIntensity);

        public bool HasAnyMutation()
        {
            return _applyGlobalIntensity || (_localState != null && _localState.HasAnyValue());
        }
    }
}

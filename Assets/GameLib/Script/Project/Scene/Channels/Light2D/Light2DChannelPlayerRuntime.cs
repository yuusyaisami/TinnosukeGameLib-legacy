#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Game.Channel
{
    public sealed class Light2DChannelPlayerRuntime :
        ILight2DChannelPlayer,
        ILight2DChannelControlService
    {
        readonly IScopeNode _ownerScope;
        readonly Light2DChannelHubService _hub;
        readonly Light2DChannelDef _definition;
        readonly Light2DChannelPresetRuntime _presetRuntime;
        readonly List<Light2DEffectEntry> _effectScratch = new();

        Light2DLocalState _baseline = new();
        bool _hasBaseline;
        bool _isAcquired;
        bool _runtimeStateActive;
        bool _hasGlobalIntensityOverride;
        float _globalIntensityOverride = 1f;
        float _inheritedGlobalIntensity = 1f;
        bool _globalDirty = true;

        public string Tag => _definition.Tag;
        public string GlobalLinkKey => _definition.GlobalLinkKey;
        public Light2D Target => _definition.TargetLight!;
        public float SelfGlobalIntensity => _hasGlobalIntensityOverride
            ? Mathf.Max(0f, _globalIntensityOverride)
            : _presetRuntime.CurrentPlayerPreset.GlobalIntensity;
        public float InheritedGlobalIntensity => _inheritedGlobalIntensity;
        public float EffectiveGlobalIntensity => SelfGlobalIntensity * InheritedGlobalIntensity;
        internal Light2DChannelPresetRuntime PresetRuntime => _presetRuntime;

        public Light2DChannelPlayerRuntime(
            IScopeNode ownerScope,
            Light2DChannelHubService hub,
            Light2DChannelDef definition,
            Light2DChannelPresetRuntime presetRuntime)
        {
            _ownerScope = ownerScope ?? throw new ArgumentNullException(nameof(ownerScope));
            _hub = hub ?? throw new ArgumentNullException(nameof(hub));
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _presetRuntime = presetRuntime ?? throw new ArgumentNullException(nameof(presetRuntime));

            _presetRuntime.OnPlayerPresetChanged += HandlePlayerPresetChanged;
            _presetRuntime.OnEffectsChanged += HandleEffectsChanged;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;

            _isAcquired = true;
            _runtimeStateActive = _definition.ApplyOnAcquire;
            _hasGlobalIntensityOverride = false;
            _globalIntensityOverride = 1f;
            _baseline = _definition.TargetLight != null
                ? Light2DLocalState.CaptureFrom(_definition.TargetLight)
                : new Light2DLocalState();
            _hasBaseline = _definition.TargetLight != null;
            _globalDirty = true;

            if (_runtimeStateActive)
                ApplyResolvedState();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;

            if (_definition.RestoreOnRelease)
                RestoreBaseline();

            _isAcquired = false;
            _runtimeStateActive = false;
            _hasGlobalIntensityOverride = false;
            _globalIntensityOverride = 1f;
            _inheritedGlobalIntensity = 1f;
            _globalDirty = true;
        }

        public void Tick(float deltaTime)
        {
            if (!_isAcquired || !_runtimeStateActive || _definition.TargetLight == null)
                return;

            _presetRuntime.Tick(deltaTime);
            ApplyResolvedState();
        }

        public bool SwapPlayerPreset(Light2DPlayerPreset? preset)
        {
            return _presetRuntime.SwapPlayerPreset(preset);
        }

        public bool MutatePlayerPreset(Light2DPlayerRuntimeMutation? mutation)
        {
            return _presetRuntime.MutatePlayerPreset(mutation);
        }

        public bool SetGlobalIntensity(float intensity)
        {
            var normalized = Mathf.Max(0f, intensity);
            if (_hasGlobalIntensityOverride && Mathf.Approximately(_globalIntensityOverride, normalized))
                return false;

            _hasGlobalIntensityOverride = true;
            _globalIntensityOverride = normalized;
            ActivateAndApply(notifyDescendants: true);
            return true;
        }

        public bool ResetGlobalIntensity()
        {
            if (!_hasGlobalIntensityOverride)
                return false;

            _hasGlobalIntensityOverride = false;
            _globalIntensityOverride = 1f;
            ActivateAndApply(notifyDescendants: true);
            return true;
        }

        public bool ReplaceEffect(
            string effectId,
            Light2DEffectPresetBase? preset,
            int priority,
            Light2DEffectBlendMode blendMode,
            bool enabled)
        {
            return _presetRuntime.ReplaceEffect(effectId, preset, priority, blendMode, enabled);
        }

        public bool MutateEffect(string effectId, Light2DEffectRuntimeMutationBase? mutation)
        {
            return _presetRuntime.MutateEffect(effectId, mutation);
        }

        public bool SetEffectEnabled(string effectId, bool enabled)
        {
            return _presetRuntime.SetEffectEnabled(effectId, enabled);
        }

        public bool RemoveEffect(string effectId)
        {
            return _presetRuntime.RemoveEffect(effectId);
        }

        public bool ResetRuntimeOverrides(bool resetPlayerPreset, bool resetEffects, bool resetGlobalIntensity)
        {
            var changed = _presetRuntime.ResetRuntimeOverrides(resetPlayerPreset, resetEffects);
            var globalChanged = false;
            if (resetGlobalIntensity && _hasGlobalIntensityOverride)
            {
                _hasGlobalIntensityOverride = false;
                _globalIntensityOverride = 1f;
                changed = true;
                globalChanged = true;
            }

            if (!changed)
                return false;

            if (globalChanged)
            {
                _globalDirty = true;
                if (!resetPlayerPreset && !resetEffects)
                    ActivateAndApply(notifyDescendants: true);
                else
                    _hub.NotifyGlobalIntensityChanged(GlobalLinkKey);
            }

            return true;
        }

        public void RestoreBaseline()
        {
            if (!_hasBaseline || _definition.TargetLight == null)
                return;

            _baseline.ApplyTo(_definition.TargetLight, allowRuntimeLightTypeChange: true);
            _runtimeStateActive = false;
        }

        internal void MarkInheritedGlobalDirty()
        {
            _globalDirty = true;
            if (_runtimeStateActive)
                ApplyResolvedState();
        }

        void HandlePlayerPresetChanged()
        {
            if (!_isAcquired)
                return;

            ActivateAndApply(notifyDescendants: true);
        }

        void HandleEffectsChanged()
        {
            if (!_isAcquired)
                return;

            ActivateAndApply(notifyDescendants: false);
        }

        void ActivateAndApply(bool notifyDescendants)
        {
            _runtimeStateActive = true;
            ApplyResolvedState();

            if (notifyDescendants)
                _hub.NotifyGlobalIntensityChanged(GlobalLinkKey);
        }

        void ApplyResolvedState()
        {
            if (_definition.TargetLight == null)
                return;

            if (_globalDirty)
                RefreshInheritedGlobalIntensity();

            var resolved = _presetRuntime.CurrentPlayerPreset.LocalState.CreateRuntimeCopy();
            ApplyEffects(resolved);
            resolved.Intensity = Mathf.Max(0f, resolved.Intensity * EffectiveGlobalIntensity);
            resolved.ApplyTo(_definition.TargetLight, _definition.AllowRuntimeLightTypeChange);
        }

        void ApplyEffects(Light2DLocalState resolved)
        {
            _effectScratch.Clear();

            var currentEffects = _presetRuntime.CurrentEffects;
            for (var i = 0; i < currentEffects.Count; i++)
            {
                var effect = currentEffects[i];
                if (effect == null || !effect.Enabled || effect.Preset == null)
                    continue;

                _effectScratch.Add(effect);
            }

            if (_effectScratch.Count == 0)
                return;

            _effectScratch.Sort(CompareEffects);

            for (var i = 0; i < _effectScratch.Count; i++)
            {
                var effect = _effectScratch[i];
                var contribution = effect.Preset!.Evaluate(new Light2DEffectEvaluationContext(effect.ElapsedTime, resolved.CreateRuntimeCopy()));
                ApplyContribution(resolved, contribution, effect.BlendMode);
            }
        }

        void RefreshInheritedGlobalIntensity()
        {
            var inherited = 1f;
            if (!string.IsNullOrWhiteSpace(GlobalLinkKey))
            {
                var current = _ownerScope.Parent;
                while (current != null)
                {
                    if (Light2DChannelHubService.TryResolveOwnedHub(current, out var hub) &&
                        hub != null &&
                        hub.TryGetPrimaryGlobalProvider(GlobalLinkKey, out var player) &&
                        player != null)
                    {
                        inherited *= player.SelfGlobalIntensity;
                    }

                    current = current.Parent;
                }
            }

            _inheritedGlobalIntensity = Mathf.Max(0f, inherited);
            _globalDirty = false;
        }

        static int CompareEffects(Light2DEffectEntry x, Light2DEffectEntry y)
        {
            var priorityCompare = x.Priority.CompareTo(y.Priority);
            if (priorityCompare != 0)
                return priorityCompare;

            return x.Order.CompareTo(y.Order);
        }

        static void ApplyContribution(
            Light2DLocalState resolved,
            Light2DContributionState? contribution,
            Light2DEffectBlendMode blendMode)
        {
            if (resolved == null || contribution == null || !contribution.HasAnyValue())
                return;

            if (contribution.HasEnabled)
                resolved.Enabled = contribution.Enabled;
            if (contribution.HasLightType)
                resolved.LightType = contribution.LightType;
            if (contribution.HasColor)
                resolved.Color = ApplyColorBlend(resolved.Color, contribution.Color, blendMode);
            if (contribution.HasIntensity)
                resolved.Intensity = ApplyFloatBlend(resolved.Intensity, contribution.Intensity, blendMode);
            if (contribution.HasBlendStyleIndex)
                resolved.BlendStyleIndex = contribution.BlendStyleIndex;
            if (contribution.HasFalloffIntensity)
                resolved.FalloffIntensity = ApplyFloatBlend(resolved.FalloffIntensity, contribution.FalloffIntensity, blendMode);
            if (contribution.HasOverlapOperation)
                resolved.OverlapOperation = contribution.OverlapOperation;
            if (contribution.HasLightOrder)
                resolved.LightOrder = contribution.LightOrder;
            if (contribution.HasVolumeIntensity)
                resolved.VolumeIntensity = ApplyFloatBlend(resolved.VolumeIntensity, contribution.VolumeIntensity, blendMode);
            if (contribution.HasVolumetricEnabled)
                resolved.VolumetricEnabled = contribution.VolumetricEnabled;
            if (contribution.HasShadowsEnabled)
                resolved.ShadowsEnabled = contribution.ShadowsEnabled;
            if (contribution.HasShadowIntensity)
                resolved.ShadowIntensity = ApplyFloatBlend(resolved.ShadowIntensity, contribution.ShadowIntensity, blendMode);
            if (contribution.HasShadowSoftness)
                resolved.ShadowSoftness = ApplyFloatBlend(resolved.ShadowSoftness, contribution.ShadowSoftness, blendMode);
            if (contribution.HasShadowSoftnessFalloffIntensity)
                resolved.ShadowSoftnessFalloffIntensity = ApplyFloatBlend(
                    resolved.ShadowSoftnessFalloffIntensity,
                    contribution.ShadowSoftnessFalloffIntensity,
                    blendMode);
            if (contribution.HasShadowVolumeIntensity)
                resolved.ShadowVolumeIntensity = ApplyFloatBlend(resolved.ShadowVolumeIntensity, contribution.ShadowVolumeIntensity, blendMode);
            if (contribution.HasVolumetricShadowsEnabled)
                resolved.VolumetricShadowsEnabled = contribution.VolumetricShadowsEnabled;
            if (contribution.HasTargetSortingLayers)
                resolved.TargetSortingLayers = Light2DLocalState.CloneSortingLayers(contribution.TargetSortingLayers);
            if (contribution.HasCookieSprite)
                resolved.CookieSprite = contribution.CookieSprite;
            if (contribution.HasPointLightInnerAngle)
                resolved.PointLightInnerAngle = ApplyFloatBlend(resolved.PointLightInnerAngle, contribution.PointLightInnerAngle, blendMode);
            if (contribution.HasPointLightOuterAngle)
                resolved.PointLightOuterAngle = ApplyFloatBlend(resolved.PointLightOuterAngle, contribution.PointLightOuterAngle, blendMode);
            if (contribution.HasPointLightInnerRadius)
                resolved.PointLightInnerRadius = ApplyFloatBlend(resolved.PointLightInnerRadius, contribution.PointLightInnerRadius, blendMode);
            if (contribution.HasPointLightOuterRadius)
                resolved.PointLightOuterRadius = ApplyFloatBlend(resolved.PointLightOuterRadius, contribution.PointLightOuterRadius, blendMode);
            if (contribution.HasShapeLightFalloffSize)
                resolved.ShapeLightFalloffSize = ApplyFloatBlend(resolved.ShapeLightFalloffSize, contribution.ShapeLightFalloffSize, blendMode);
            if (contribution.HasShapePath)
                resolved.ShapePath = Light2DLocalState.CloneShapePath(contribution.ShapePath);

            resolved.Intensity = Mathf.Max(0f, resolved.Intensity);
            resolved.FalloffIntensity = Mathf.Max(0f, resolved.FalloffIntensity);
            resolved.VolumeIntensity = Mathf.Max(0f, resolved.VolumeIntensity);
            resolved.ShadowIntensity = Mathf.Max(0f, resolved.ShadowIntensity);
            resolved.ShadowSoftness = Mathf.Max(0f, resolved.ShadowSoftness);
            resolved.ShadowSoftnessFalloffIntensity = Mathf.Max(0f, resolved.ShadowSoftnessFalloffIntensity);
            resolved.ShadowVolumeIntensity = Mathf.Max(0f, resolved.ShadowVolumeIntensity);
            resolved.PointLightInnerAngle = Mathf.Max(0f, resolved.PointLightInnerAngle);
            resolved.PointLightOuterAngle = Mathf.Max(0f, resolved.PointLightOuterAngle);
            resolved.PointLightInnerRadius = Mathf.Max(0f, resolved.PointLightInnerRadius);
            resolved.PointLightOuterRadius = Mathf.Max(0f, resolved.PointLightOuterRadius);
            resolved.ShapeLightFalloffSize = Mathf.Max(0f, resolved.ShapeLightFalloffSize);
        }

        static float ApplyFloatBlend(float current, float value, Light2DEffectBlendMode blendMode)
        {
            return blendMode switch
            {
                Light2DEffectBlendMode.Add => current + value,
                Light2DEffectBlendMode.Multiply => current * value,
                _ => value,
            };
        }

        static Color ApplyColorBlend(Color current, Color value, Light2DEffectBlendMode blendMode)
        {
            return blendMode switch
            {
                Light2DEffectBlendMode.Add => current + value,
                Light2DEffectBlendMode.Multiply => current * value,
                _ => value,
            };
        }
    }
}

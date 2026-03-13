// Game.StatusEffect.SpeedModEffectProfileSO
// スピード変更エフェクト用 薄い asset wrapper。実データは SpeedModEffectPreset に保持。

using System;
using System.Collections.Generic;
using Game.Common;
using Game.Profile;
using Game.Scalar;
using Game.Scalar.Generated;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.StatusEffect
{
    [CreateAssetMenu(menuName = "Game/StatusEffect/SpeedModEffectProfile", fileName = "SpeedModEffectProfile")]
    public sealed class SpeedModEffectProfileSO : ScriptableObject, IProfileDefinition, IDynamicValueAsset<SpeedModEffectPreset>
    {
        [SerializeReference, InlineProperty, HideLabel]
        SpeedModEffectPreset _preset;

        // Legacy fields — kept for migration
        [HideInInspector, SerializeField] ProfileFloatValue _boostDefaultDuration = new() { Value = 5f, ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.StatusEffect.SpeedMod.DefaultDuration), ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime, UseClampMod = true, Clamp = new ScalarClamp { UseMin = true, Min = 0f } };
        [HideInInspector, SerializeField] ProfileFloatValue _boostDefaultIntensity = new() { Value = 0.5f, ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.StatusEffect.SpeedMod.DefaultIntensity), ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime, UseClampMod = true, Clamp = new ScalarClamp { UseMin = true, Min = 0f } };
        [HideInInspector, SerializeField] ProfileFloatValue _boostBaseMultiplier = new() { Value = 1.5f, ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.StatusEffect.SpeedMod.Multiplier), ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime, UseEffectMod = true, UseClampMod = true, Clamp = new ScalarClamp { UseMin = true, Min = 1f } };
        [HideInInspector, SerializeField] ProfileFloatValue _slowDefaultDuration = new() { Value = 3f, ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.StatusEffect.Slow.DefaultDuration), ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime, UseClampMod = true, Clamp = new ScalarClamp { UseMin = true, Min = 0f } };
        [HideInInspector, SerializeField] ProfileFloatValue _slowDefaultIntensity = new() { Value = 0.3f, ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.StatusEffect.Slow.DefaultIntensity), ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime, UseClampMod = true, Clamp = new ScalarClamp { UseMin = true, Min = 0f, UseMax = true, Max = 0.9f } };
        [HideInInspector, SerializeField, FormerlySerializedAs("_boostVisualData")] EffectVisualData _boostVisualData_legacy = new() { IconAnimation = null, DisplayName = "加速", Description = "移動速度が上昇している" };
        [HideInInspector, SerializeField, FormerlySerializedAs("_slowVisualData")] EffectVisualData _slowVisualData_legacy = new() { IconAnimation = null, DisplayName = "減速", Description = "移動速度が低下している" };

        public SpeedModEffectPreset Preset
        {
            get { EnsurePresetMigrated(); return _preset; }
        }

        public float BoostDefaultDuration => Preset?.BoostDefaultDuration ?? 5f;
        public float BoostDefaultIntensity => Preset?.BoostDefaultIntensity ?? 0.5f;
        public float BoostBaseMultiplier => Preset?.BoostBaseMultiplier ?? 1.5f;
        public EffectVisualData BoostVisualData => Preset?.BoostVisualData;
        public float SlowDefaultDuration => Preset?.SlowDefaultDuration ?? 3f;
        public float SlowDefaultIntensity => Preset?.SlowDefaultIntensity ?? 0.3f;
        public EffectVisualData SlowVisualData => Preset?.SlowVisualData;

        public EffectConfig CreateSpeedBoostConfig(
            object source = null, float? overrideDuration = null, float? overrideIntensity = null)
            => Preset?.CreateSpeedBoostConfig(source, overrideDuration, overrideIntensity)
                ?? new EffectConfig { Duration = overrideDuration ?? 5f, Intensity = overrideIntensity ?? 0.5f, StackMode = EffectStackMode.Refresh, Source = source, Tag = "SpeedBoost" };

        public EffectConfig CreateSlowConfig(
            object source = null, float? overrideDuration = null, float? overrideIntensity = null)
            => Preset?.CreateSlowConfig(source, overrideDuration, overrideIntensity)
                ?? new EffectConfig { Duration = overrideDuration ?? 3f, Intensity = overrideIntensity ?? 0.3f, StackMode = EffectStackMode.Refresh, Source = source, Tag = "Slow" };

        public Type ProfileType => typeof(SpeedModEffectProfileSO);

        public IEnumerable<IProfileValueBinding> EnumerateBindings()
        {
            var p = Preset;
            if (p == null) yield break;
            foreach (var b in p.EnumerateBindings())
                yield return b;
        }

        public void CollectBindings(List<IProfileValueBinding> output) => Preset?.CollectBindings(output);
        public int GetBindingCount() => Preset?.GetBindingCount() ?? 0;

        void OnEnable() => EnsurePresetMigrated();
        void OnValidate() => EnsurePresetMigrated();

        void EnsurePresetMigrated()
        {
            if (_preset != null) return;
            _preset = SpeedModEffectPreset.CreateFromLegacyFields(
                _boostDefaultDuration, _boostDefaultIntensity, _boostBaseMultiplier,
                _slowDefaultDuration, _slowDefaultIntensity,
                _boostVisualData_legacy, _slowVisualData_legacy);
        }
    }
}

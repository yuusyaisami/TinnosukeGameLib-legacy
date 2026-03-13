// Game.StatusEffect.PoisonEffectProfileSO
// 毒エフェクト用 薄い asset wrapper。実データは PoisonEffectPreset に保持。

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
    [CreateAssetMenu(menuName = "Game/StatusEffect/PoisonEffectProfile", fileName = "PoisonEffectProfile")]
    public sealed class PoisonEffectProfileSO : ScriptableObject, IProfileDefinition, IDynamicValueAsset<PoisonEffectPreset>
    {
        [SerializeReference, InlineProperty, HideLabel]
        PoisonEffectPreset _preset;

        // Legacy fields — kept for migration
        [HideInInspector, SerializeField] ProfileFloatValue _defaultDuration = new() { Value = 5f, ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.StatusEffect.Poison.DefaultDuration), ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime, UseClampMod = true, Clamp = new ScalarClamp { UseMin = true, Min = 0f } };
        [HideInInspector, SerializeField] ProfileFloatValue _defaultIntensity = new() { Value = 1f, ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.StatusEffect.Poison.DefaultIntensity), ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime, UseClampMod = true, Clamp = new ScalarClamp { UseMin = true, Min = 0f } };
        [HideInInspector, SerializeField] ProfileFloatValue _damagePerSecond = new() { Value = 5f, ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.Health.Modifier.Poison.DamagePerSecond), ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime, UseEffectMod = true, UseClampMod = true, Clamp = new ScalarClamp { UseMin = true, Min = 0f } };
        [HideInInspector, SerializeField] ProfileFloatValue _tickInterval = new() { Value = 0.5f, ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.Health.Modifier.Poison.TickInterval), ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime, UseClampMod = true, Clamp = new ScalarClamp { UseMin = true, Min = 0.1f } };
        [HideInInspector, SerializeField, FormerlySerializedAs("_visualData")] EffectVisualData _visualData_legacy = new() { IconAnimation = null, DisplayName = "毒", Description = "時間経過でダメージを受ける" };

        public PoisonEffectPreset Preset
        {
            get { EnsurePresetMigrated(); return _preset; }
        }

        public float DefaultDuration => Preset?.DefaultDuration ?? 5f;
        public float DefaultIntensity => Preset?.DefaultIntensity ?? 1f;
        public float DamagePerSecond => Preset?.DamagePerSecond ?? 5f;
        public float TickInterval => Preset?.TickInterval ?? 0.5f;
        public EffectVisualData VisualData => Preset?.VisualData;

        public EffectConfig CreateConfig(
            object source = null, float? overrideDuration = null, float? overrideIntensity = null)
            => Preset?.CreateConfig(source, overrideDuration, overrideIntensity)
                ?? new EffectConfig { Duration = overrideDuration ?? 5f, Intensity = overrideIntensity ?? 1f, StackMode = EffectStackMode.Refresh, Source = source, Tag = "Poison" };

        public Type ProfileType => typeof(PoisonEffectProfileSO);

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
            _preset = PoisonEffectPreset.CreateFromLegacyFields(
                _defaultDuration, _defaultIntensity,
                _damagePerSecond, _tickInterval,
                _visualData_legacy);
        }
    }
}

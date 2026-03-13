// Game.Health.HealthModifierProfileSO
// Health Modifier 用 薄い asset wrapper。実データは HealthModifierPreset に保持。

using System;
using System.Collections.Generic;
using Game.Common;
using Game.Profile;
using Game.Scalar;
using Game.Scalar.Generated;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Health
{
    [CreateAssetMenu(menuName = "Game/Health/HealthModifierProfile", fileName = "HealthModifierProfile")]
    public sealed class HealthModifierProfileSO : ScriptableObject, IProfileDefinition, IDynamicValueAsset<HealthModifierPreset>
    {
        [SerializeReference, InlineProperty, HideLabel]
        HealthModifierPreset _preset;

        // Legacy fields — kept for migration
        [HideInInspector, SerializeField] ProfileFloatValue _poisonDamagePerSecond = new() { Value = 5f, ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.Health.Modifier.Poison.DamagePerSecond), ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime, UseEffectMod = true, UseClampMod = true, Clamp = new ScalarClamp { UseMin = true, Min = 0f } };
        [HideInInspector, SerializeField] ProfileFloatValue _poisonTickInterval = new() { Value = 0.5f, ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.Health.Modifier.Poison.TickInterval), ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime, UseClampMod = true, Clamp = new ScalarClamp { UseMin = true, Min = 0.1f } };
        [HideInInspector, SerializeField] ProfileFloatValue _damageReductionRate = new() { Value = 0.3f, ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.Health.Modifier.DamageReduction.Rate), ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime, UseEffectMod = true, UseClampMod = true, Clamp = new ScalarClamp { UseMin = true, Min = 0f, UseMax = true, Max = 1f } };
        [HideInInspector, SerializeField] ProfileFloatValue _healBoostRate = new() { Value = 0.5f, ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.Health.Modifier.HealBoost.Rate), ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime, UseEffectMod = true, UseClampMod = true, Clamp = new ScalarClamp { UseMin = true, Min = 0f } };
        [HideInInspector, SerializeField] ProfileFloatValue _criticalIncomingMultiplier = new() { Value = 2f, ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.Health.Modifier.Critical.IncomingMultiplier), ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime, UseEffectMod = true, UseClampMod = true, Clamp = new ScalarClamp { UseMin = true, Min = 1f } };
        [HideInInspector, SerializeField] ProfileFloatValue _criticalIncomingChance = new() { Value = 0.05f, ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.Health.Modifier.Critical.IncomingChance), ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime, UseEffectMod = true, UseClampMod = true, Clamp = new ScalarClamp { UseMin = true, Min = 0f, UseMax = true, Max = 1f } };
        [HideInInspector, SerializeField] ProfileFloatValue _criticalOutgoingMultiplier = new() { Value = 2f, ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.Health.Modifier.Critical.OutgoingMultiplier), ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime, UseEffectMod = true, UseClampMod = true, Clamp = new ScalarClamp { UseMin = true, Min = 1f } };
        [HideInInspector, SerializeField] ProfileFloatValue _criticalOutgoingChance = new() { Value = 0.1f, ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.Health.Modifier.Critical.OutgoingChance), ScalarPolicyValue = ScalarBindPolicy.ReplaceRuntime, UseEffectMod = true, UseClampMod = true, Clamp = new ScalarClamp { UseMin = true, Min = 0f, UseMax = true, Max = 1f } };

        public HealthModifierPreset Preset
        {
            get { EnsurePresetMigrated(); return _preset; }
        }

        public Type ProfileType => typeof(HealthModifierProfileSO);

        public IEnumerable<IProfileValueBinding> EnumerateBindings()
        {
            var p = Preset;
            if (p == null) yield break;
            foreach (var b in p.EnumerateBindings())
                yield return b;
        }

        public void CollectBindings(List<IProfileValueBinding> output)
        {
            Preset?.CollectBindings(output);
        }

        public int GetBindingCount() => Preset?.GetBindingCount() ?? 0;

        void OnEnable() => EnsurePresetMigrated();
        void OnValidate() => EnsurePresetMigrated();

        void EnsurePresetMigrated()
        {
            if (_preset != null) return;
            _preset = HealthModifierPreset.CreateFromLegacyFields(
                _poisonDamagePerSecond, _poisonTickInterval,
                _damageReductionRate, _healBoostRate,
                _criticalIncomingMultiplier, _criticalIncomingChance,
                _criticalOutgoingMultiplier, _criticalOutgoingChance);
        }
    }
}

// Game.StatusEffect.PoisonEffectProfileSO
// 毒エフェクト用 薄い asset wrapper。実データは PoisonEffectPreset に保持。

using System;
using System.Collections.Generic;
using Game.Common;
using Game.Profile;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.StatusEffect
{
    [CreateAssetMenu(menuName = "Game/StatusEffect/PoisonEffectProfile", fileName = "PoisonEffectProfile")]
    public sealed class PoisonEffectProfileSO : ScriptableObject, IProfileDefinition, IDynamicValueAsset<PoisonEffectPreset>
    {
        [SerializeReference, InlineProperty, HideLabel]
        PoisonEffectPreset _preset = new();

        public PoisonEffectPreset Preset => _preset;

        public float DefaultDuration => _preset?.DefaultDuration ?? 5f;
        public float DefaultIntensity => _preset?.DefaultIntensity ?? 1f;
        public float DamagePerSecond => _preset?.DamagePerSecond ?? 5f;
        public float TickInterval => _preset?.TickInterval ?? 0.5f;
        public EffectVisualData VisualData => _preset?.VisualData;

        public EffectConfig CreateConfig(
            object source = null, float? overrideDuration = null, float? overrideIntensity = null)
            => _preset?.CreateConfig(source, overrideDuration, overrideIntensity)
                ?? new EffectConfig { Duration = overrideDuration ?? 5f, Intensity = overrideIntensity ?? 1f, StackMode = EffectStackMode.Refresh, Source = source, Tag = "Poison" };

        public Type ProfileType => typeof(PoisonEffectPreset);

        public IEnumerable<IProfileValueBinding> EnumerateBindings()
        {
            if (_preset == null) yield break;
            foreach (var b in _preset.EnumerateBindings())
                yield return b;
        }

        public void CollectBindings(List<IProfileValueBinding> output) => _preset?.CollectBindings(output);
        public int GetBindingCount() => _preset?.GetBindingCount() ?? 0;
    }
}

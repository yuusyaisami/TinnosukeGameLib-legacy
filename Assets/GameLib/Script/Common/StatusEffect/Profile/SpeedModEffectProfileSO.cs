// Game.StatusEffect.SpeedModEffectProfileSO
// スピード変更エフェクト用 薄い asset wrapper。実データは SpeedModEffectPreset に保持。

using System;
using System.Collections.Generic;
using Game.Common;
using Game.Profile;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.StatusEffect
{
    [CreateAssetMenu(menuName = "Game/StatusEffect/SpeedModEffectProfile", fileName = "SpeedModEffectProfile")]
    public sealed class SpeedModEffectProfileSO : ScriptableObject, IProfileDefinition, IDynamicValueAsset<SpeedModEffectPreset>
    {
        [SerializeReference, InlineProperty, HideLabel]
        SpeedModEffectPreset _preset = new();

        public SpeedModEffectPreset Preset => _preset;

        public float BoostDefaultDuration => _preset?.BoostDefaultDuration ?? 5f;
        public float BoostDefaultIntensity => _preset?.BoostDefaultIntensity ?? 0.5f;
        public float BoostBaseMultiplier => _preset?.BoostBaseMultiplier ?? 1.5f;
        public EffectVisualData BoostVisualData => _preset?.BoostVisualData;
        public float SlowDefaultDuration => _preset?.SlowDefaultDuration ?? 3f;
        public float SlowDefaultIntensity => _preset?.SlowDefaultIntensity ?? 0.3f;
        public EffectVisualData SlowVisualData => _preset?.SlowVisualData;

        public EffectConfig CreateSpeedBoostConfig(
            object source = null, float? overrideDuration = null, float? overrideIntensity = null)
            => _preset?.CreateSpeedBoostConfig(source, overrideDuration, overrideIntensity)
                ?? new EffectConfig { Duration = overrideDuration ?? 5f, Intensity = overrideIntensity ?? 0.5f, StackMode = EffectStackMode.Refresh, Source = source, Tag = "SpeedBoost" };

        public EffectConfig CreateSlowConfig(
            object source = null, float? overrideDuration = null, float? overrideIntensity = null)
            => _preset?.CreateSlowConfig(source, overrideDuration, overrideIntensity)
                ?? new EffectConfig { Duration = overrideDuration ?? 3f, Intensity = overrideIntensity ?? 0.3f, StackMode = EffectStackMode.Refresh, Source = source, Tag = "Slow" };

        public Type ProfileType => typeof(SpeedModEffectPreset);

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

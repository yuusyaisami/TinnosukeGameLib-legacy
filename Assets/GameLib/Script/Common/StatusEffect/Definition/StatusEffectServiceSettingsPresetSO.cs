#nullable enable

using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.StatusEffect
{
    [CreateAssetMenu(menuName = "Game/StatusEffect/Global Lifetime Preset", fileName = "StatusEffectGlobalLifetimePreset")]
    public sealed class StatusEffectGlobalLifetimeSettingsSO : ScriptableObject, IDynamicValueAsset<StatusEffectGlobalLifetimeSettings>
    {
        [SerializeReference]
        [InlineProperty]
        [HideLabel]
        StatusEffectGlobalLifetimeSettings? preset = new();

        public StatusEffectGlobalLifetimeSettings? Preset => preset;
    }

    [CreateAssetMenu(menuName = "Game/StatusEffect/Global UseCooldown Preset", fileName = "StatusEffectGlobalUseCooldownPreset")]
    public sealed class StatusEffectGlobalUseCooldownSettingsSO : ScriptableObject, IDynamicValueAsset<StatusEffectGlobalUseCooldownSettings>
    {
        [SerializeReference]
        [InlineProperty]
        [HideLabel]
        StatusEffectGlobalUseCooldownSettings? preset = new();

        public StatusEffectGlobalUseCooldownSettings? Preset => preset;
    }

    [CreateAssetMenu(menuName = "Game/StatusEffect/Global Count Preset", fileName = "StatusEffectGlobalCountPreset")]
    public sealed class StatusEffectGlobalCountSettingsSO : ScriptableObject, IDynamicValueAsset<StatusEffectGlobalCountSettings>
    {
        [SerializeReference]
        [InlineProperty]
        [HideLabel]
        StatusEffectGlobalCountSettings? preset = new();

        public StatusEffectGlobalCountSettings? Preset => preset;
    }
}

#nullable enable

using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.StatusEffect
{
    [CreateAssetMenu(menuName = "Game/StatusEffect/Stack Preset", fileName = "StatusEffectStackPreset")]
    public sealed class StatusEffectStackPresetSO : ScriptableObject, IDynamicValueAsset<StatusEffectStackPreset>
    {
        [SerializeReference]
        [InlineProperty]
        [HideLabel]
        StatusEffectStackPreset? preset = StatusEffectStackPreset.CreateDurationRefreshPreset();

        public StatusEffectStackPreset? Preset => preset;
    }
}
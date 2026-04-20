#nullable enable

using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.StatusEffect
{
    [CreateAssetMenu(menuName = "Game/StatusEffect/Definition", fileName = "StatusEffectDefinition")]
    public sealed class StatusEffectDefinitionSO :
        ScriptableObject,
        IDynamicValueAsset<BaseStatusEffectDefinitionData>
    {
        [SerializeReference]
        [HideLabel]
        [InlineProperty]
        [Tooltip("Inspector setting.")]
        BaseStatusEffectDefinitionData? preset;

        public BaseStatusEffectDefinitionData? Preset => preset;
    }
}

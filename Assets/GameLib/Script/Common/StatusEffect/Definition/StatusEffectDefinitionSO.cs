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
        [Tooltip("ScriptableObject が保持する StatusEffect 定義本体です。")]
        BaseStatusEffectDefinitionData? preset;

        public BaseStatusEffectDefinitionData? Preset => preset;
    }
}

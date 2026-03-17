#nullable enable
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.NoiseProducer
{
    [CreateAssetMenu(menuName = "GameLib/Noise/ChannelDefinition", order = 100)]
    public sealed class NoiseChannelDefinitionSO : ScriptableObject
    {
        [SerializeField, InlineProperty, HideLabel]
        NoiseChannelDefinition _definition = new();

        public NoiseChannelDefinition Definition => _definition;
    }
}

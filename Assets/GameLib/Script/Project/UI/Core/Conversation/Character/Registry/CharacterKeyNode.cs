#nullable enable

using System;
using Game.Registry;
using UnityEngine;

namespace Game.Conversation
{
    [Serializable]
    public sealed class CharacterKeyNode : HierarchyNodeBase
    {
        [SerializeField] int characterId;
        [SerializeField] string stableKey = string.Empty;

        public int CharacterId
        {
            get => characterId;
            set => characterId = value;
        }

        public string StableKey
        {
            get => stableKey;
            set => stableKey = value ?? string.Empty;
        }
    }
}

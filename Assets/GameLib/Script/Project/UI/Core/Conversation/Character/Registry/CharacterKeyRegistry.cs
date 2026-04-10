#nullable enable

using System;
using System.Collections.Generic;
using Game.Registry;
using UnityEngine;

namespace Game.Conversation
{
    [CreateAssetMenu(menuName = "Game/Conversation/Registry/Character Key Registry")]
    public sealed class CharacterKeyRegistry : HierarchyRegistryBase<CharacterKeyNode>
    {
        [SerializeField] int nextCharacterId = 1;

        readonly Dictionary<string, int> _keyToId = new(StringComparer.Ordinal);
        readonly Dictionary<int, string> _idToKey = new();

        bool _built;

        public override string GetKeyString(CharacterKeyNode node)
        {
            if (node == null || node.IsFolder)
                return string.Empty;

            return !string.IsNullOrEmpty(node.StableKey)
                ? node.StableKey
                : (node.Name ?? string.Empty);
        }

        protected override void InitializeLeafNode(CharacterKeyNode node)
        {
            base.InitializeLeafNode(node);

            if (node == null)
                return;

            if (node.CharacterId <= 0)
                node.CharacterId = AllocateNewCharacterId();

            if (string.IsNullOrEmpty(node.StableKey))
                node.StableKey = node.Name ?? string.Empty;

            _built = false;
        }

        public bool TryResolve(string stableKey, out int characterId)
        {
            EnsureLookup();
            if (string.IsNullOrWhiteSpace(stableKey))
            {
                characterId = 0;
                return false;
            }

            return _keyToId.TryGetValue(stableKey.Trim(), out characterId) && characterId > 0;
        }

        public bool TryGetStableKey(int characterId, out string stableKey)
        {
            EnsureLookup();
            if (characterId <= 0 || !_idToKey.TryGetValue(characterId, out stableKey))
            {
                stableKey = string.Empty;
                return false;
            }

            return !string.IsNullOrEmpty(stableKey);
        }

        public void EnsureLookupRebuild()
        {
            _built = false;
        }

        int AllocateNewCharacterId()
        {
            var maxId = 0;
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null || node.IsFolder)
                    continue;

                if (node.CharacterId > maxId)
                    maxId = node.CharacterId;
            }

            if (nextCharacterId <= maxId)
                nextCharacterId = maxId + 1;

            return nextCharacterId++;
        }

        void EnsureLookup()
        {
            if (_built)
                return;

            _keyToId.Clear();
            _idToKey.Clear();

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null || node.IsFolder)
                    continue;

                if (node.CharacterId <= 0)
                    continue;

                var key = node.StableKey;
                if (string.IsNullOrWhiteSpace(key))
                    key = node.Name;

                if (string.IsNullOrWhiteSpace(key))
                    continue;

                key = key.Trim();
                if (_keyToId.TryGetValue(key, out var existing) && existing != node.CharacterId)
                {
                    Debug.LogError($"[CharacterKeyRegistry] Duplicate stableKey: '{key}' ({existing} vs {node.CharacterId})");
                    continue;
                }

                _keyToId[key] = node.CharacterId;
                if (!_idToKey.ContainsKey(node.CharacterId))
                    _idToKey.Add(node.CharacterId, key);
            }

            _built = true;
        }
    }
}

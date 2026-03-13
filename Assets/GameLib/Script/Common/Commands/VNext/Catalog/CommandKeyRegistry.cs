#nullable enable
using System;
using System.Collections.Generic;
using Game.Registry;
using UnityEngine;

namespace Game.Commands.VNext
{
    [CreateAssetMenu(menuName = "Game/Registry/Commands/Command Key Registry")]
    public sealed class CommandKeyRegistry : HierarchyRegistryBase<CommandKeyNode>, ICommandKeyRegistry
    {
        [Serializable]
        sealed class Tombstone
        {
            [SerializeField] int keyId;
            [SerializeField] string stableKey = string.Empty;
            [SerializeField] List<string> aliases = new();

            public int KeyId => keyId;
            public string StableKey => stableKey;
            public IReadOnlyList<string> Aliases => aliases;

            public Tombstone(int keyId, string stableKey, List<string> aliases)
            {
                this.keyId = keyId;
                this.stableKey = stableKey ?? string.Empty;
                this.aliases = aliases != null ? new List<string>(aliases) : new List<string>();
            }
        }

        [SerializeField] int nextKeyId = 1;
        [SerializeField] List<Tombstone> tombstones = new();

        readonly Dictionary<string, int> _keyToId = new(StringComparer.Ordinal);
        readonly Dictionary<int, string> _idToKey = new();
        bool _built;

        public override string GetKeyString(CommandKeyNode node)
        {
            if (node == null || node.IsFolder)
                return string.Empty;

            return !string.IsNullOrEmpty(node.StableKey) ? node.StableKey : (node.Name ?? string.Empty);
        }

        public bool TryResolve(string stableKeyOrAlias, out CommandKeyId keyId)
        {
            keyId = default;
            EnsureLookup();
            if (string.IsNullOrEmpty(stableKeyOrAlias))
                return false;

            if (!_keyToId.TryGetValue(stableKeyOrAlias, out var id) || id <= 0)
                return false;

            keyId = new CommandKeyId(id);
            return true;
        }

        public bool IsReservedKey(string stableKeyOrAlias)
        {
            EnsureLookup();
            if (string.IsNullOrEmpty(stableKeyOrAlias))
                return false;

            return _keyToId.TryGetValue(stableKeyOrAlias, out var id) && id == 0;
        }

        public bool TryGetStableKey(CommandKeyId keyId, out string stableKey)
        {
            stableKey = string.Empty;
            EnsureLookup();
            if (!keyId.IsValid || !_idToKey.TryGetValue(keyId.Value, out stableKey))
            {
                stableKey = string.Empty;
                return false;
            }
            return !string.IsNullOrEmpty(stableKey);
        }

        protected override void InitializeLeafNode(CommandKeyNode node)
        {
            base.InitializeLeafNode(node);
            if (node == null)
                return;

            if (node.KeyId <= 0)
            {
                node.KeyId = AllocateNewKeyId();
            }

            if (string.IsNullOrEmpty(node.StableKey))
            {
                node.StableKey = node.Name ?? string.Empty;
            }

            _built = false;
        }

        public void EnsureLookupRebuild() => _built = false;

        protected override bool OnDeleteNode(CommandKeyNode node)
        {
            if (node == null)
                return true;

            if (node.IsFolder)
                return true;

            if (node.KeyId > 0 && !string.IsNullOrEmpty(node.StableKey))
            {
                tombstones ??= new List<Tombstone>();
                tombstones.Add(new Tombstone(node.KeyId, node.StableKey, node.Aliases));
            }

            _built = false;
            return true;
        }

        int AllocateNewKeyId()
        {
            int maxId = 0;
            foreach (var n in nodes)
            {
                if (n != null && !n.IsFolder && n.KeyId > maxId)
                    maxId = n.KeyId;
            }

            if (tombstones != null)
            {
                for (int i = 0; i < tombstones.Count; i++)
                {
                    var t = tombstones[i];
                    if (t != null && t.KeyId > maxId)
                        maxId = t.KeyId;
                }
            }

            if (nextKeyId <= maxId)
                nextKeyId = maxId + 1;

            return nextKeyId++;
        }

        void EnsureLookup()
        {
            if (_built)
                return;

            _keyToId.Clear();
            _idToKey.Clear();
            _built = true;

            BuildFromNodes();
            BuildFromTombstones();
        }

        void BuildFromNodes()
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null || node.IsFolder)
                    continue;

                var stableKey = node.StableKey;
                if (string.IsNullOrEmpty(stableKey))
                    stableKey = node.Name;

                if (node.KeyId <= 0 || string.IsNullOrEmpty(stableKey))
                    continue;

                AddKey(stableKey, node.KeyId, isReserved: false);

                var aliases = node.Aliases;
                if (aliases == null)
                    continue;

                for (int a = 0; a < aliases.Count; a++)
                {
                    var alias = aliases[a];
                    if (string.IsNullOrEmpty(alias))
                        continue;
                    AddKey(alias, node.KeyId, isReserved: false);
                }

                if (!_idToKey.ContainsKey(node.KeyId))
                    _idToKey.Add(node.KeyId, stableKey);
            }
        }

        void BuildFromTombstones()
        {
            if (tombstones == null)
                return;

            for (int i = 0; i < tombstones.Count; i++)
            {
                var t = tombstones[i];
                if (t == null)
                    continue;

                if (t.KeyId <= 0 || string.IsNullOrEmpty(t.StableKey))
                    continue;

                AddKey(t.StableKey, t.KeyId, isReserved: true);

                var aliases = t.Aliases;
                if (aliases == null)
                    continue;
                for (int a = 0; a < aliases.Count; a++)
                {
                    var alias = aliases[a];
                    if (string.IsNullOrEmpty(alias))
                        continue;
                    AddKey(alias, t.KeyId, isReserved: true);
                }
            }
        }

        void AddKey(string key, int keyId, bool isReserved)
        {
            if (_keyToId.ContainsKey(key))
            {
                if (!isReserved)
                    Debug.LogError($"[CommandKeyRegistry] Duplicate stableKey/alias: '{key}'");
                return;
            }
            _keyToId.Add(key, isReserved ? 0 : keyId);
        }
    }
}

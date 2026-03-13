#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Game.Commands.VNext
{
    [CreateAssetMenu(menuName = "Game/Commands/VNext/Command Catalog")]
    public sealed class CommandCatalogSO : ScriptableObject, ICommandCatalog
    {
        [SerializeField] List<CommandCatalogEntry> entries = new();

        readonly Dictionary<int, int> _keyIdToIndex = new();
        bool _built;

        public IReadOnlyList<CommandCatalogEntry> Entries => entries;

        public bool TryResolve(CommandKeyId keyId, out ICommandData data)
        {
            data = null!;
            EnsureLookup();
            if (keyId.Value == 0)
                return false;

            if (!_keyIdToIndex.TryGetValue(keyId.Value, out var index))
                return false;

            if (index < 0 || index >= entries.Count)
                return false;

            var entry = entries[index];
            data = entry?.Data!;
            return data != null;
        }

        public bool TryResolve(CommandKeyRef key, out ICommandData data)
        {
            data = null!;
            if (string.IsNullOrEmpty(key.StableKey))
                return false;

            var registry = CommandKeyRegistryLocator.GetOrCreate();
            if (registry != null)
            {
                if (registry.IsReservedKey(key.StableKey))
                    return false;

                if (registry.TryResolve(key.StableKey, out var keyId))
                    return TryResolve(keyId, out data);
            }

            return TryResolveByStableKey(key.StableKey, out data);
        }

        public bool TryGetMeta(CommandKeyRef key, out CommandCatalogMeta meta)
        {
            meta = null!;
            if (string.IsNullOrEmpty(key.StableKey))
                return false;

            var registry = CommandKeyRegistryLocator.GetOrCreate();
            if (registry != null)
            {
                if (registry.IsReservedKey(key.StableKey))
                    return false;

                if (registry.TryResolve(key.StableKey, out var keyId))
                {
                    EnsureLookup();
                    if (!_keyIdToIndex.TryGetValue(keyId.Value, out var index))
                        return false;

                    if (index < 0 || index >= entries.Count)
                        return false;

                    meta = entries[index]?.Meta!;
                    return meta != null;
                }
            }

            return TryGetMetaByStableKey(key.StableKey, out meta);
        }

        void EnsureLookup()
        {
            if (_built)
                return;

            _keyIdToIndex.Clear();
            _built = true;

            var registry = CommandKeyRegistryLocator.GetOrCreate();
            if (registry == null)
                return;

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.Data == null)
                    continue;

                var stableKey = entry.Key.StableKey;
                if (string.IsNullOrEmpty(stableKey))
                {
                    Debug.LogError("[CommandCatalogSO] Entry has empty stableKey.");
                    continue;
                }

                if (!registry.TryResolve(stableKey, out var keyId) || keyId.Value == 0)
                {
                    if (registry.IsReservedKey(stableKey))
                        Debug.LogError($"[CommandCatalogSO] Tombstoned key: '{stableKey}'");
                    else
                        Debug.LogError($"[CommandCatalogSO] Key not registered: '{stableKey}'");
                    continue;
                }

                if (_keyIdToIndex.ContainsKey(keyId.Value))
                {
                    Debug.LogError($"[CommandCatalogSO] Duplicate keyId: {keyId.Value} for '{stableKey}'");
                    continue;
                }

                _keyIdToIndex.Add(keyId.Value, i);
            }
        }

        bool TryResolveByStableKey(string stableKey, out ICommandData data)
        {
            data = null!;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.Data == null)
                    continue;

                if (string.Equals(entry.Key.StableKey, stableKey, System.StringComparison.Ordinal))
                {
                    data = entry.Data;
                    return true;
                }
            }

            return false;
        }

        bool TryGetMetaByStableKey(string stableKey, out CommandCatalogMeta meta)
        {
            meta = null!;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.Meta == null)
                    continue;

                if (string.Equals(entry.Key.StableKey, stableKey, System.StringComparison.Ordinal))
                {
                    meta = entry.Meta;
                    return true;
                }
            }

            return false;
        }

        void OnEnable()
        {
            _built = false;
        }

        void OnValidate()
        {
            _built = false;
        }
    }
}

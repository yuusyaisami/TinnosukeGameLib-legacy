#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using Game;

namespace Game.Commands.VNext
{
    public sealed class CommandKeyResolver : ICommandKeyResolver, IScopeAcquireHandler, IScopeReleaseHandler
    {
        readonly Dictionary<string, CommandKeyId> _cacheKeyToId = new(StringComparer.Ordinal);
        readonly Dictionary<int, string> _cacheIdToKey = new();

        public bool TryResolve(string stableKey, out CommandKeyId keyId)
        {
            keyId = default;
            if (string.IsNullOrEmpty(stableKey))
                return false;

            if (_cacheKeyToId.TryGetValue(stableKey, out keyId) && keyId.Value != 0)
                return true;

            var registry = CommandKeyRegistryLocator.GetOrCreate();
            if (registry != null)
            {
                if (registry.TryResolve(stableKey, out keyId) && keyId.IsValid)
                {
                    _cacheKeyToId[stableKey] = keyId;
                    if (!_cacheIdToKey.ContainsKey(keyId.Value) && registry.TryGetStableKey(keyId, out var canonical))
                        _cacheIdToKey[keyId.Value] = canonical;

                    if (registry.TryGetStableKey(keyId, out var stable) && !string.Equals(stable, stableKey, StringComparison.Ordinal))
                        Debug.Log($"[CommandKeyResolver] Alias '{stableKey}' resolved to '{stable}'.");

                    return true;
                }

                if (registry.IsReservedKey(stableKey))
                {
                    Debug.LogError($"[CommandKeyResolver] Tombstoned key: '{stableKey}'");
                    return false;
                }
            }

            return false;
        }

        public bool TryGetStableKey(CommandKeyId keyId, out string stableKey)
        {
            stableKey = string.Empty;
            if (!keyId.IsValid)
                return false;

            if (_cacheIdToKey.TryGetValue(keyId.Value, out stableKey) && !string.IsNullOrEmpty(stableKey))
                return true;

            var registry = CommandKeyRegistryLocator.GetOrCreate();
            if (registry != null && registry.TryGetStableKey(keyId, out stableKey) && !string.IsNullOrEmpty(stableKey))
            {
                _cacheIdToKey[keyId.Value] = stableKey;
                return true;
            }

            stableKey = string.Empty;
            return false;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (isReset)
                ClearCaches();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            ClearCaches();
        }

        void ClearCaches()
        {
            _cacheKeyToId.Clear();
            _cacheIdToKey.Clear();
        }
    }
}

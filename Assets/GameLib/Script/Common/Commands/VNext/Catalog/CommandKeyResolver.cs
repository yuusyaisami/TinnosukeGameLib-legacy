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

        readonly Dictionary<string, CommandKeyId> _runtimeKeyToId = new(StringComparer.Ordinal);
        readonly Dictionary<int, string> _runtimeIdToKey = new();
        int _nextRuntimeId = -1;

        public bool TryResolve(string stableKey, out CommandKeyId keyId)
        {
            return TryResolve(stableKey, allowRuntimeFallback: false, out keyId);
        }

        public bool TryResolve(string stableKey, bool allowRuntimeFallback, out CommandKeyId keyId)
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

            if (!AllowRuntimeFallback(allowRuntimeFallback))
                return false;

            if (_runtimeKeyToId.TryGetValue(stableKey, out keyId) && keyId.Value != 0)
                return true;

            keyId = new CommandKeyId(_nextRuntimeId--);
            _runtimeKeyToId[stableKey] = keyId;
            _runtimeIdToKey[keyId.Value] = stableKey;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[CommandKeyResolver] StableKey '{stableKey}' is not registered. Using runtime-only keyId={keyId.Value} (do not serialize).");
#endif
            return true;
        }

        public bool TryGetStableKey(CommandKeyId keyId, out string stableKey)
        {
            stableKey = string.Empty;
            if (!keyId.IsValid && keyId.Value >= 0)
                return false;

            if (_cacheIdToKey.TryGetValue(keyId.Value, out stableKey) && !string.IsNullOrEmpty(stableKey))
                return true;

            var registry = CommandKeyRegistryLocator.GetOrCreate();
            if (registry != null && registry.TryGetStableKey(keyId, out stableKey) && !string.IsNullOrEmpty(stableKey))
            {
                _cacheIdToKey[keyId.Value] = stableKey;
                return true;
            }

            if (_runtimeIdToKey.TryGetValue(keyId.Value, out stableKey) && !string.IsNullOrEmpty(stableKey))
                return true;

            stableKey = string.Empty;
            return false;
        }

        static bool AllowRuntimeFallback(bool allowRuntimeFallback)
        {
            if (!allowRuntimeFallback)
                return false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return true;
#else
            return Debug.isDebugBuild;
#endif
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
            _runtimeKeyToId.Clear();
            _runtimeIdToKey.Clear();
            _nextRuntimeId = -1;
        }
    }
}

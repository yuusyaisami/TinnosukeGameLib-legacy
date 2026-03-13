#nullable enable
using System.Collections.Generic;
using Game.VarStoreKeys;
using UnityEngine;
using System;
/// <summary>
/// stableKey(string) → varId(int) の解決を提供する。
/// </summary>
/// <remarks>
/// 重要:
/// - 本来、資産化されたデータは varId を直接保持し、Runtime で stableKey を解決しないのが理想。
/// - ただし移行期間中は「旧システムが string key を前提としている」箇所が残るため、
///   registry 未登録の stableKey は runtime-only の負 ID（varId &lt; 0）を割り当てて継続動作させる。
/// - runtime-only varId は保存/資産化へ出さないこと。
/// </remarks>
public static class VarIdResolver
{
    static readonly object Gate = new();
    static readonly Dictionary<string, int> RuntimeKeyToVarId = new(StringComparer.Ordinal);
    static readonly Dictionary<int, string> RuntimeVarIdToKey = new();
    static int _nextRuntimeVarId = -1;

    // Positive cache for resolved stableKey -> positive varId to avoid repeated registry lookups
    static readonly Dictionary<string, int> s_positiveCache = new(StringComparer.Ordinal);

#if UNITY_EDITOR
    const bool EnableRegistryDiagnostics = false;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        lock (Gate)
        {
            RuntimeKeyToVarId.Clear();
            RuntimeVarIdToKey.Clear();
            s_positiveCache.Clear();
            _nextRuntimeVarId = -1;
        }
    }

    public static bool TryResolve(string stableKeyOrAlias, out int varId)
    {
        varId = 0;
        if (string.IsNullOrEmpty(stableKeyOrAlias))
            return false;

        // Fast path: return cached positive resolution to avoid repeated registry lookups.
        if (s_positiveCache.TryGetValue(stableKeyOrAlias, out var cached) && cached > 0)
        {
            varId = cached;
            return true;
        }

        // Registry がある場合は最優先（正の安定ID）
        var registry = VarKeyRegistryLocator.GetOrCreate();
        if (registry != null && registry.TryResolve(stableKeyOrAlias, out varId) && varId != 0)
        {
            // cache positive result
            lock (Gate)
            {
                s_positiveCache[stableKeyOrAlias] = varId;
            }
            return true;
        }

#if UNITY_EDITOR
        if (EnableRegistryDiagnostics && registry != null && registry.RegisteredKeyCount == 0)
        { }
#endif

        lock (Gate)
        {
            if (RuntimeKeyToVarId.TryGetValue(stableKeyOrAlias, out varId))
                return true;
        }

        // registry 未登録は runtime-only の負IDへフォールバック（移行ブリッジ）
        lock (Gate)
        {
            if (RuntimeKeyToVarId.TryGetValue(stableKeyOrAlias, out varId))
                return true;

            varId = _nextRuntimeVarId--;
            RuntimeKeyToVarId.Add(stableKeyOrAlias, varId);
            RuntimeVarIdToKey.Add(varId, stableKeyOrAlias);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
#endif
            return true;
        }
    }

    public static bool TryGetStableKey(int varId, out string stableKey)
    {
        stableKey = string.Empty;
        if (varId == 0)
            return false;

        var registry = VarKeyRegistryLocator.GetOrCreate();
        if (registry != null && registry.TryGetStableKey(varId, out stableKey) && !string.IsNullOrEmpty(stableKey))
            return true;

        lock (Gate)
        {
            return RuntimeVarIdToKey.TryGetValue(varId, out stableKey!) && !string.IsNullOrEmpty(stableKey);
        }
    }

    public static string? TryGetIdToStable(int varId)
    {
        if (varId == 0)
            return null;

        var registry = VarKeyRegistryLocator.GetOrCreate();
        if (registry != null && registry.TryGetStableKey(varId, out var stableKey) && !string.IsNullOrEmpty(stableKey))
            return stableKey;

        lock (Gate)
        {
            if (RuntimeVarIdToKey.TryGetValue(varId, out var key) && !string.IsNullOrEmpty(key))
                return key;
        }

        return null;
    }
}

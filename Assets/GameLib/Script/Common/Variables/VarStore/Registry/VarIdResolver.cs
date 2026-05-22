#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Game.VarStoreKeys;
using UnityEngine;

/// <summary>
/// stableKey(string) → varId(int) の解決を提供する。
/// </summary>
/// <remarks>
/// 重要:
/// - 登録済み stableKey だけを正の varId に解決する。
/// - 未登録 stableKey を runtime-only の負 ID に補修しない。
/// - registry が見つからない場合は fail-closed に失敗する。
/// </remarks>
public static class VarIdResolver
{
    static readonly object Gate = new();

    // Positive cache for resolved stableKey -> positive varId to avoid repeated registry lookups.
    static readonly Dictionary<string, int> s_positiveCache = new(StringComparer.Ordinal);

    public enum VarIdResolutionFailureReason
    {
        None = 0,
        EmptyStableKey = 10,
        RegistryUnavailable = 20,
        StableKeyNotFound = 30,
    }

#if UNITY_EDITOR
    const bool EnableRegistryDiagnostics = false;
#endif

    internal static void ClearCachedResolutions()
    {
        lock (Gate)
        {
            s_positiveCache.Clear();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        ClearCachedResolutions();
    }

    public static bool TryResolve(string stableKeyOrAlias, out int varId)
    {
        return TryResolve(stableKeyOrAlias, out varId, out _);
    }

    public static bool TryResolve(string stableKeyOrAlias, out int varId, out VarIdResolutionFailureReason failureReason)
    {
        varId = 0;
        failureReason = VarIdResolutionFailureReason.None;

        if (string.IsNullOrEmpty(stableKeyOrAlias))
        {
            failureReason = VarIdResolutionFailureReason.EmptyStableKey;
            return false;
        }

        if (VerifiedValueRuntimeBridge.TryGetSession(out IVerifiedValueRuntimeSession? verifiedSession) && verifiedSession != null)
        {
            if (verifiedSession.TryResolveValueKey(stableKeyOrAlias, out varId) && varId > 0)
            {
                lock (Gate)
                {
                    s_positiveCache[stableKeyOrAlias] = varId;
                }

                return true;
            }

            varId = 0;
            failureReason = VarIdResolutionFailureReason.StableKeyNotFound;
            return false;
        }

        lock (Gate)
        {
            if (s_positiveCache.TryGetValue(stableKeyOrAlias, out int cached) && cached > 0)
            {
                varId = cached;
                return true;
            }
        }

        bool hasRegistry = VarKeyRegistryLocator.TryGetExplicitRegistry(out var registry) && registry != null;
        if (hasRegistry && registry.TryResolve(stableKeyOrAlias, out varId) && varId > 0)
        {
            lock (Gate)
            {
                s_positiveCache[stableKeyOrAlias] = varId;
            }
            return true;
        }

        varId = 0;
        failureReason = hasRegistry
            ? VarIdResolutionFailureReason.StableKeyNotFound
            : VarIdResolutionFailureReason.RegistryUnavailable;
        return false;
    }

    public static bool TryGetStableKey(int varId, out string stableKey)
    {
        stableKey = string.Empty;
        if (varId == 0)
            return false;

        if (VerifiedValueRuntimeBridge.TryGetSession(out IVerifiedValueRuntimeSession? verifiedSession) && verifiedSession != null)
            return verifiedSession.TryGetStableKey(varId, out stableKey);

        if (VarKeyRegistryLocator.TryGetExplicitRegistry(out var registry) && registry != null && registry.TryGetStableKey(varId, out stableKey) && !string.IsNullOrEmpty(stableKey))
            return true;

        stableKey = string.Empty;
        return false;
    }

    public static string? TryGetIdToStable(int varId)
    {
        if (varId == 0)
            return null;

        if (VerifiedValueRuntimeBridge.TryGetSession(out IVerifiedValueRuntimeSession? verifiedSession) && verifiedSession != null)
            return verifiedSession.TryGetStableKey(varId, out string verifiedStableKey) ? verifiedStableKey : null;

        if (VarKeyRegistryLocator.TryGetExplicitRegistry(out var registry) && registry != null && registry.TryGetStableKey(varId, out var registryStableKey) && !string.IsNullOrEmpty(registryStableKey))
            return registryStableKey;

        return null;
    }
}

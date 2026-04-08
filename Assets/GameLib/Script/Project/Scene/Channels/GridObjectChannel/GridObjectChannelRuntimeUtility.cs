#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.Commands.VNext;
using Game.DI;
using UnityEngine;
using VContainer;

namespace Game.Channel
{
    internal static class GridObjectChannelRuntimeUtility
    {
        public static bool TryResolveFromScopeOrAncestors<T>(IScopeNode? scope, out T? value) where T : class
        {
            value = null;
            for (var current = scope; current != null; current = current.Parent)
            {
                var resolver = current.Resolver;
                if (resolver == null)
                    continue;

                if (resolver.TryResolve<T>(out var resolved) && resolved != null)
                {
                    value = resolved;
                    return true;
                }
            }

            return false;
        }

        public static IVarStore ResolveVars(IScopeNode? scope)
        {
            if (scope?.Resolver != null &&
                scope.Resolver.TryResolve<IVarStore>(out var vars) &&
                vars != null)
            {
                return vars;
            }

            return NullVarStore.Instance;
        }

        public static void EnsureScopeBuiltIfNeeded(IScopeNode? scope)
        {
            if (scope is BaseLifetimeScope baseScope)
            {
                baseScope.EnsureScopeBuilt();
                return;
            }

            if (scope is RuntimeLifetimeScope runtimeScope)
                runtimeScope.EnsureScopeBuilt();
        }

        public static int ResolveVarId(VarKeyRef key, int fallback)
        {
            if (!string.IsNullOrEmpty(key.StableKey) && VarIdResolver.TryResolve(key.StableKey, out var resolved) && resolved > 0)
                return resolved;

            return key.VarId > 0 ? key.VarId : fallback;
        }

        public static CommandLtsSlot ResolveContextSlotOrDefault(CommandLtsSlot slot)
        {
            return CommandLtsSlotUtility.IsContextSlot(slot)
                ? slot
                : CommandLtsSlot.ContextA;
        }

        public static CancellationTokenSource? CreateLinkedTokenSource(CancellationTokenSource? lifecycleCts, CancellationToken ct)
        {
            if (lifecycleCts == null)
                return null;

            return CancellationTokenSource.CreateLinkedTokenSource(ct, lifecycleCts.Token);
        }

        public static async UniTask ReleaseSpawnedInstanceAsync(
            Transform? root,
            IScopeNode? scope,
            IObjectResolver? resolver)
        {
            if (resolver == null)
                return;

            await UniTask.SwitchToMainThread();

            try
            {
                if (resolver.TryResolve<RuntimeLifetimeScope>(out var runtimeScope) && runtimeScope != null)
                {
                    if (runtimeScope.Resolver != null &&
                        runtimeScope.Resolver.TryResolve<IRuntimeLifetimeScopePool>(out var pool) &&
                        pool != null)
                    {
                        pool.Release(runtimeScope);
                        return;
                    }

                    if (root != null)
                        Object.Destroy(root.gameObject);
                    else
                        Object.Destroy(runtimeScope.gameObject);
                    return;
                }

                if (scope is BaseLifetimeScope baseScope)
                {
                    await baseScope.DespawnAsync(CancellationToken.None);
                    return;
                }

                if (root != null)
                    Object.Destroy(root.gameObject);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GridObjectChannel] Release failed: {ex.Message}");
            }
        }

        public static void ExtractSpawnedInfo(IObjectResolver? resolver, out Transform? root, out IScopeNode? scopeNode)
        {
            root = null;
            scopeNode = null;
            if (resolver == null)
                return;

            if (resolver.TryResolve<RuntimeLifetimeScope>(out var runtimeScope) && runtimeScope != null)
            {
                root = runtimeScope.transform;
                scopeNode = runtimeScope;
                return;
            }

            if (resolver.TryResolve<BaseLifetimeScope>(out var baseScope) && baseScope != null)
            {
                root = baseScope.transform;
                scopeNode = baseScope;
            }
        }

        public static void WriteVariant(IVarStore vars, int varId, DynamicVariant value)
        {
            if (vars == null || varId == 0)
                return;

            vars.TrySetVariant(varId, value);
        }
    }
}

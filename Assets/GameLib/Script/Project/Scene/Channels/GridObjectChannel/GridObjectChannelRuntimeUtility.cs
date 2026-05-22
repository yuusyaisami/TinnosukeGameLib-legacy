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
        public static bool TryResolveFromScope<T>(IScopeNode? scope, out T? value) where T : class
        {
            value = null;
            var resolver = scope?.Resolver;
            if (resolver == null)
                return false;

            return resolver.TryResolve<T>(out var resolved) && (value = resolved) != null;
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
            IRuntimeResolver? resolver)
        {
            if (resolver == null)
                return;

            try
            {
                await ScopeFeatureInstallerUtility.ReleaseSpawnedLifetimeAsync(resolver, CancellationToken.None);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GridObjectChannel] Release failed: {ex.Message}");
            }
        }

        public static void ExtractSpawnedInfo(IRuntimeResolver? resolver, out Transform? root, out IScopeNode? scopeNode)
        {
            var lifetime = ScopeFeatureInstallerUtility.CaptureSpawnedLifetime(resolver);
            root = lifetime.Root != null ? lifetime.Root.transform : null;
            scopeNode = lifetime.ScopeNode;
        }

        public static void WriteVariant(IVarStore vars, int varId, DynamicVariant value)
        {
            if (vars == null || varId == 0)
                return;

            vars.TrySetVariant(varId, value);
        }
    }
}

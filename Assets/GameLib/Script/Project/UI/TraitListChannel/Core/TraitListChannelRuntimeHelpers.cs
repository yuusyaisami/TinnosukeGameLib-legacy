#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Channel;
using Game.Commands.VNext;
using Game.Common;
using Game.Spawn;
using UnityEngine;
using VContainer;

namespace Game.UI
{
    internal static class TraitListChannelRuntimeHelpers
    {
        public static TraitListChannelEnvironmentKind ResolveEnvironment(Transform ownerTransform, out Canvas? canvas)
        {
            var environment = TransformGridSharedUtility.ResolveEnvironment(ownerTransform, out canvas);
            return environment == TransformGridEnvironmentKind.ScreenUI
                ? TraitListChannelEnvironmentKind.ScreenUI
                : TraitListChannelEnvironmentKind.World;
        }

        public static string NormalizeTag(string? tag)
        {
            return string.IsNullOrWhiteSpace(tag) ? "default" : tag.Trim();
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

        public static bool TryResolveVisualBounds(TraitListChannelVisualInstance instance, out VisualBoundsOutput output)
        {
            return TransformGridSharedUtility.TryResolveVisualBounds(
                instance.Resolver,
                instance.Root,
                instance.RootRect,
                out output);
        }

        public static Vector3 ResolvePlacementLocalPosition(
            TraitListChannelVisualInstance instance,
            Vector3 targetLocalPosition,
            TraitListChannelHorizontalAlignment horizontalAlignment,
            TraitListChannelVerticalAlignment verticalAlignment)
        {
            return TransformGridSharedUtility.ResolvePlacementLocalPosition(
                instance.Resolver,
                instance.Root,
                instance.RootRect,
                targetLocalPosition,
                (int)horizontalAlignment,
                (int)verticalAlignment);
        }

        public static void SetLocalPosition(
            TraitListChannelVisualInstance instance,
            Vector3 localPosition,
            TraitListChannelEnvironmentKind environmentKind)
        {
            TransformGridSharedUtility.SetLocalPosition(
                instance.Root,
                instance.RootRect,
                localPosition,
                environmentKind == TraitListChannelEnvironmentKind.ScreenUI
                    ? TransformGridEnvironmentKind.ScreenUI
                    : TransformGridEnvironmentKind.World);
        }

        public static async UniTask ReleaseSpawnedInstanceAsync(
            Transform? root,
            IScopeNode? scope,
            IRuntimeResolver? resolver)
        {
            if (resolver == null)
                return;

            await UniTask.SwitchToMainThread();
            //Debug.Log(
            //    $"[TraitListChannel][ReleaseSpawnedInstance] root='{DescribeTransform(root)}' scope='{scope?.GetType().Name ?? "<null>"}' " +
            //    $"resolver='{resolver.GetType().Name}'");

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
                Debug.LogError($"[TraitListChannel] Release failed: {ex.Message}");
            }
        }

        public static void ExtractSpawnedInfo(
            IRuntimeResolver? resolver,
            out Transform? root,
            out IScopeNode? scopeNode)
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
                return;
            }
        }

        public static bool TryResolveTransformAnimationPlayer(
            TraitListChannelVisualInstance instance,
            string channelTag,
            out ITransformAnimationChannelPlayer? player)
        {
            return TransformGridSharedUtility.TryResolveTransformAnimationPlayer(instance.Resolver, channelTag, out player);
        }

        static string DescribeTransform(Transform? target)
        {
            if (target == null)
                return "<null>";

            var current = target;
            var path = current.name;
            while (current.parent != null)
            {
                current = current.parent;
                path = $"{current.name}/{path}";
            }

            return $"{target.name} path='{path}'";
        }
    }
}

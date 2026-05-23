#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game
{
    public static class ScopeFeatureInstallerUtility
    {
        public static void EnsureScopeBuiltIfNeeded(IScopeNode? scope)
        {
            if (scope == null)
                return;

            if (scope is ICoordinatedBuildScope coordinated)
            {
                if (coordinated.Resolver == null && !coordinated.IsBuildCompleted)
                    coordinated.ExecuteBuildForCoordinator();
                return;
            }

            if (scope is KernelScopeHost runtimeScope)
                runtimeScope.EnsureScopeBuilt();
        }

        public static SpawnedLifetimeHandle CaptureSpawnedLifetime(IRuntimeResolver? resolver)
            => SpawnedLifetimeHandle.FromResolver(resolver);

        public static UniTask ReleaseSpawnedLifetimeAsync(
            IRuntimeResolver? resolver,
            CancellationToken ct = default,
            Action<Exception>? onPoolReleaseError = null)
        {
            if (resolver == null)
                return UniTask.CompletedTask;

            return SpawnedLifetimeHandle.FromResolver(resolver).ReleaseAsync(ct, onPoolReleaseError);
        }

        public static bool TryGetScopeNode(Component? component, bool includeInactive, out IScopeNode? node)
        {
            if (component == null)
            {
                node = null;
                return false;
            }

            return TryGetScopeNode(component.transform, includeInactive, out node);
        }

        public static bool TryGetScopeNode(GameObject? gameObject, bool includeInactive, out IScopeNode? node)
        {
            if (gameObject == null)
            {
                node = null;
                return false;
            }

            return TryGetScopeNode(gameObject.transform, includeInactive, out node);
        }

        public static bool TryGetScopeNode(Transform? transform, bool includeInactive, out IScopeNode? node)
        {
            node = null;
            if (transform == null)
                return false;

            for (var current = transform; current != null; current = current.parent)
            {
                if (!includeInactive && !current.gameObject.activeInHierarchy)
                    continue;

                if (current.TryGetComponent<KernelScopeHost>(out var runtimeScope) && runtimeScope != null)
                {
                    node = runtimeScope;
                    return true;
                }

                var components = current.GetComponents<Component>();
                for (var i = 0; i < components.Length; i++)
                {
                    if (components[i] is IScopeNode scopeNode)
                    {
                        node = scopeNode;
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool TryGetNearestScopeNode(Component component, bool includeInactive, out IScopeNode? node)
        {
            return TryGetScopeNode(component, includeInactive, out node);
        }

        public static UniTask WaitForResolverBuiltAsync(IScopeNode? node, CancellationToken ct = default)
        {
            if (node == null || node.Resolver != null)
                return UniTask.CompletedTask;

            if (node is ICoordinatedBuildScope coordinated)
                return ScopeBuildCoordinator.WaitUntilBuiltAsync(coordinated, ct);

            return UniTask.CompletedTask;
        }
    }
}


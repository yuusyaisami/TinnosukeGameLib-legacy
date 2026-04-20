#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;

namespace Game
{
    public static class ScopeFeatureInstallerUtility
    {
        public static void InstallOwnedFeatureInstallers(
            Component scopeBehaviour,
            bool includeInactive,
            IRuntimeContainerBuilder builder,
            IScopeNode scope)
        {
            if (scopeBehaviour == null)
                throw new ArgumentNullException(nameof(scopeBehaviour));
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));
            if (scope == null)
                throw new ArgumentNullException(nameof(scope));

            var installers = ListPool<IFeatureInstaller>.Get();
            try
            {
                scopeBehaviour.GetComponentsInChildren(includeInactive, installers);
                for (int i = 0; i < installers.Count; i++)
                {
                    var installer = installers[i];
                    if (installer is not Component component)
                        continue;

                    if (!IsOwnedByScope(component, includeInactive, scope))
                        continue;

                    installer.InstallFeature(builder, scope);
                }
            }
            finally
            {
                ListPool<IFeatureInstaller>.Release(installers);
            }
        }

        static bool IsOwnedByScope(Component component, bool includeInactive, IScopeNode owner)
        {
            if (!component)
                return false;

            if (!TryGetNearestScopeNode(component, includeInactive, out var node))
                return false;

            return ReferenceEquals(node, owner);
        }

        public static bool TryGetNearestScopeNode(Component component, bool includeInactive, out IScopeNode? node)
        {
            node = null;
            if (!component)
                return false;

            var current = component.transform;
            while (current != null)
            {
                if (current.TryGetComponent<RuntimeLifetimeScopeBase>(out var runtimeScope) && runtimeScope != null)
                {
                    node = runtimeScope;
                    return true;
                }

                current = current.parent;
            }

            return false;
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

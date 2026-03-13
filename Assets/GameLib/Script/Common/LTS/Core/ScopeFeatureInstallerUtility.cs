#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;
using VContainer.Unity;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game
{
    public static class ScopeFeatureInstallerUtility
    {
        public static void InstallOwnedFeatureInstallers(
            LifetimeScope scopeBehaviour,
            bool includeInactive,
            IContainerBuilder builder,
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

            // IMPORTANT:
            // Do not rely on GetComponentsInParent ordering (it has changed in the past and differs from
            // developer expectations). We must always pick the *closest* scope in the hierarchy
            // (including the component's own GameObject), and we must stop at RuntimeLifetimeScope too.
            var current = component.transform;
            while (current != null)
            {
                if (current.TryGetComponent<RuntimeLifetimeScope>(out var runtimeScope) && runtimeScope != null)
                {
                    node = runtimeScope;
                    return true;
                }
                if (current.TryGetComponent<BaseLifetimeScope>(out var baseScope) && baseScope != null)
                {
                    node = baseScope;
                    return true;
                }
                if (current.TryGetComponent<LifetimeScope>(out var lifetimeScope) && lifetimeScope is IScopeNode scopeNode)
                {
                    node = scopeNode;
                    return true;
                }
                current = current.parent;
            }

            return false;
        }

        /// <summary>
        /// Waits until the specified <paramref name="node"/> has a built <see cref="IObjectResolver"/>,
        /// if possible. For <see cref="ICoordinatedBuildScope"/> this waits until the coordinator completes
        /// its build; otherwise returns immediately.
        /// </summary>
        public static UniTask WaitForResolverBuiltAsync(IScopeNode? node, CancellationToken ct = default)
        {
            if (node == null)
                return UniTask.CompletedTask;

            if (node.Resolver != null)
                return UniTask.CompletedTask;

            if (node is ICoordinatedBuildScope coordinated)
            {
                // Wait until the coordinator completes building this scope (this will return immediately
                // if the scope is already built).
                return ScopeBuildCoordinator.WaitUntilBuiltAsync(coordinated, ct);
            }

            // Node isn't a coordinated build scope and resolver isn't available — nothing we can wait for.
            return UniTask.CompletedTask;
        }
    }
}

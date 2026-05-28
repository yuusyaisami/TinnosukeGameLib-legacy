#nullable enable
using System;
using System.Collections.Generic;
using Game.Kernel.Boot;
using Game.Kernel.Generation;

namespace Game.Kernel.Layers.Composition
{
    public sealed class SceneKernelComposition : ISceneKernelComposition
    {
        IKernelBootRuntimeSurface? runtimeSurface;
        KernelRuntimeServiceGraph? serviceGraph;
        KernelRuntimeScopeGraph? scopeGraph;
        ISceneKernelSpawnBoundary? spawnBoundary;
        ISceneKernelValueStoreBoundary? valueStoreBoundary;
        KernelLifecycleDispatcher? lifecycleDispatcher;
        ILifecyclePlanResolver? lifecyclePlanResolver;
        EntityRegistrationPlan? entityRegistrationPlan;
        ServiceRegistrationPlan? serviceRegistrationPlan;
        EntityServiceRoutePlan? entityServiceRoutePlan;

        SceneKernelComposition()
        {
        }

        public SceneKernelComposition(
            IKernelBootRuntimeSurface runtimeSurface,
            KernelRuntimeServiceGraph serviceGraph,
            KernelRuntimeScopeGraph scopeGraph,
            ILifecyclePlanResolver lifecyclePlanResolver,
            KernelLifecycleDispatcher? lifecycleDispatcher,
            EntityRegistrationPlan? entityRegistrationPlan,
            ServiceRegistrationPlan? serviceRegistrationPlan,
            EntityServiceRoutePlan? entityServiceRoutePlan)
        {
            BindRuntimeSurface(runtimeSurface, serviceGraph, scopeGraph, lifecyclePlanResolver, lifecycleDispatcher, entityRegistrationPlan, serviceRegistrationPlan, entityServiceRoutePlan);
        }

        public IReadOnlyList<KernelComponentPlacementDescriptor> Placements => KernelComponentPlacementCatalog.Scene;

        public ISceneKernelSpawnBoundary? SpawnBoundary => spawnBoundary;

        public ISceneKernelValueStoreBoundary? ValueStoreBoundary => valueStoreBoundary;

        public IKernelBootRuntimeSurface RuntimeSurface => runtimeSurface ?? throw new InvalidOperationException("SceneKernelComposition runtime surface has not been bound.");

        public KernelRuntimeServiceGraph ServiceGraph => serviceGraph ?? throw new InvalidOperationException("SceneKernelComposition runtime service graph has not been bound.");

        public KernelRuntimeScopeGraph ScopeGraph => scopeGraph ?? throw new InvalidOperationException("SceneKernelComposition runtime scope graph has not been bound.");

        public KernelLifecycleDispatcher? LifecycleDispatcher => lifecycleDispatcher;

        public ILifecyclePlanResolver LifecyclePlanResolver => lifecyclePlanResolver ?? throw new InvalidOperationException("SceneKernelComposition lifecycle plan resolver has not been bound.");

        public EntityRegistrationPlan? EntityRegistrationPlan => entityRegistrationPlan;

        public ServiceRegistrationPlan? ServiceRegistrationPlan => serviceRegistrationPlan;

        public EntityServiceRoutePlan? EntityServiceRoutePlan => entityServiceRoutePlan;

        public bool HasRuntimeBinding => runtimeSurface != null;

        public static SceneKernelComposition CreatePending()
        {
            return new SceneKernelComposition();
        }

        public static SceneKernelComposition FromRuntimeSurface(IKernelBootRuntimeSurface runtimeSurface)
        {
            if (runtimeSurface == null)
                throw new ArgumentNullException(nameof(runtimeSurface));

            if (runtimeSurface is not KernelBootRuntimeSurface concreteSurface)
            {
                throw new ArgumentException(
                    "SceneKernelComposition requires the concrete KernelBootRuntimeSurface so scene-local runtime parts can be extracted without rebuilding them.",
                    nameof(runtimeSurface));
            }

            KernelRuntime runtime = concreteSurface.Runtime;
            return new SceneKernelComposition(
                runtimeSurface,
                runtime.ServiceGraph,
                runtime.RootScopeGraph,
                concreteSurface.LifecyclePlanResolver,
                concreteSurface.LifecycleDispatcher,
                concreteSurface.EntityRegistrationPlan,
                concreteSurface.ServiceRegistrationPlan,
                concreteSurface.EntityServiceRoutePlan);
        }

        public void BindRuntimeSurface(IKernelBootRuntimeSurface runtimeSurface)
        {
            if (runtimeSurface == null)
                throw new ArgumentNullException(nameof(runtimeSurface));

            if (runtimeSurface is not KernelBootRuntimeSurface concreteSurface)
            {
                throw new ArgumentException(
                    "SceneKernelComposition requires the concrete KernelBootRuntimeSurface so scene-local runtime parts can be extracted without rebuilding them.",
                    nameof(runtimeSurface));
            }

            KernelRuntime runtime = concreteSurface.Runtime;
            BindRuntimeSurface(
                runtimeSurface,
                runtime.ServiceGraph,
                runtime.RootScopeGraph,
                concreteSurface.LifecyclePlanResolver,
                concreteSurface.LifecycleDispatcher,
                concreteSurface.EntityRegistrationPlan,
                concreteSurface.ServiceRegistrationPlan,
                concreteSurface.EntityServiceRoutePlan);
        }

        public void BindSpawnBoundary(ISceneKernelSpawnBoundary spawnBoundary)
        {
            this.spawnBoundary = spawnBoundary ?? throw new ArgumentNullException(nameof(spawnBoundary));
        }

        public void BindValueStoreBoundary(ISceneKernelValueStoreBoundary valueStoreBoundary)
        {
            this.valueStoreBoundary = valueStoreBoundary ?? throw new ArgumentNullException(nameof(valueStoreBoundary));
        }

        public void ClearSpawnBoundary()
        {
            spawnBoundary = null;
        }

        public void ClearValueStoreBoundary()
        {
            valueStoreBoundary = null;
        }

        public void ClearRuntimeBinding()
        {
            runtimeSurface = null;
            serviceGraph = null;
            scopeGraph = null;
            lifecycleDispatcher = null;
            lifecyclePlanResolver = null;
            entityRegistrationPlan = null;
            serviceRegistrationPlan = null;
            entityServiceRoutePlan = null;
        }

        public bool TryGetBoundary(SceneKernelBoundaryKind boundaryKind, out object? boundary)
        {
            switch (boundaryKind)
            {
                case SceneKernelBoundaryKind.RuntimeSurface:
                    boundary = runtimeSurface;
                    return runtimeSurface != null;
                case SceneKernelBoundaryKind.RuntimeServiceGraph:
                    boundary = serviceGraph;
                    return serviceGraph != null;
                case SceneKernelBoundaryKind.RuntimeScopeGraph:
                    boundary = scopeGraph;
                    return scopeGraph != null;
                case SceneKernelBoundaryKind.SpawnBoundary:
                    boundary = spawnBoundary;
                    return spawnBoundary != null;
                case SceneKernelBoundaryKind.LifecycleDispatcher:
                    boundary = lifecycleDispatcher;
                    return lifecycleDispatcher != null;
                case SceneKernelBoundaryKind.LifecyclePlanResolver:
                    boundary = lifecyclePlanResolver;
                    return lifecyclePlanResolver != null;
                case SceneKernelBoundaryKind.EntityRegistrationPlan:
                    boundary = entityRegistrationPlan;
                    return entityRegistrationPlan != null;
                case SceneKernelBoundaryKind.ServiceRegistrationPlan:
                    boundary = serviceRegistrationPlan;
                    return serviceRegistrationPlan != null;
                case SceneKernelBoundaryKind.EntityServiceRoutePlan:
                    boundary = entityServiceRoutePlan;
                    return entityServiceRoutePlan != null;
                case SceneKernelBoundaryKind.ValueStore:
                    boundary = valueStoreBoundary;
                    return valueStoreBoundary != null;
                case SceneKernelBoundaryKind.Unknown:
                default:
                    boundary = null;
                    return false;
            }
        }

        void BindRuntimeSurface(
            IKernelBootRuntimeSurface runtimeSurface,
            KernelRuntimeServiceGraph serviceGraph,
            KernelRuntimeScopeGraph scopeGraph,
            ILifecyclePlanResolver lifecyclePlanResolver,
            KernelLifecycleDispatcher? lifecycleDispatcher,
            EntityRegistrationPlan? entityRegistrationPlan,
            ServiceRegistrationPlan? serviceRegistrationPlan,
            EntityServiceRoutePlan? entityServiceRoutePlan)
        {
            this.runtimeSurface = runtimeSurface ?? throw new ArgumentNullException(nameof(runtimeSurface));
            this.serviceGraph = serviceGraph ?? throw new ArgumentNullException(nameof(serviceGraph));
            this.scopeGraph = scopeGraph ?? throw new ArgumentNullException(nameof(scopeGraph));
            this.lifecyclePlanResolver = lifecyclePlanResolver ?? throw new ArgumentNullException(nameof(lifecyclePlanResolver));
            this.lifecycleDispatcher = lifecycleDispatcher;
            this.entityRegistrationPlan = entityRegistrationPlan;
            this.serviceRegistrationPlan = serviceRegistrationPlan;
            this.entityServiceRoutePlan = entityServiceRoutePlan;
        }
    }
}

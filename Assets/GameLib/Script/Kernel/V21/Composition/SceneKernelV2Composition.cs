#nullable enable
using System;
using System.Collections.Generic;
using Game.Kernel.Boot;

namespace Game.Kernel.V21.Composition
{
    public sealed class SceneKernelV2Composition : ISceneKernelComposition
    {
        IKernelBootRuntimeSurface? runtimeSurface;
        KernelRuntimeServiceGraph? serviceGraph;
        KernelRuntimeScopeGraph? scopeGraph;
        KernelLifecycleDispatcher? lifecycleDispatcher;
        ILifecyclePlanResolver? lifecyclePlanResolver;

        SceneKernelV2Composition()
        {
        }

        public SceneKernelV2Composition(
            IKernelBootRuntimeSurface runtimeSurface,
            KernelRuntimeServiceGraph serviceGraph,
            KernelRuntimeScopeGraph scopeGraph,
            ILifecyclePlanResolver lifecyclePlanResolver,
            KernelLifecycleDispatcher? lifecycleDispatcher)
        {
            BindRuntimeSurface(runtimeSurface, serviceGraph, scopeGraph, lifecyclePlanResolver, lifecycleDispatcher);
        }

        public IReadOnlyList<KernelComponentPlacementDescriptor> Placements => KernelV2ComponentPlacementCatalog.Scene;

        public IKernelBootRuntimeSurface RuntimeSurface => runtimeSurface ?? throw new InvalidOperationException("SceneKernelV2Composition runtime surface has not been bound.");

        public KernelRuntimeServiceGraph ServiceGraph => serviceGraph ?? throw new InvalidOperationException("SceneKernelV2Composition runtime service graph has not been bound.");

        public KernelRuntimeScopeGraph ScopeGraph => scopeGraph ?? throw new InvalidOperationException("SceneKernelV2Composition runtime scope graph has not been bound.");

        public KernelLifecycleDispatcher? LifecycleDispatcher => lifecycleDispatcher;

        public ILifecyclePlanResolver LifecyclePlanResolver => lifecyclePlanResolver ?? throw new InvalidOperationException("SceneKernelV2Composition lifecycle plan resolver has not been bound.");

        public bool HasRuntimeBinding => runtimeSurface != null;

        public static SceneKernelV2Composition CreatePending()
        {
            return new SceneKernelV2Composition();
        }

        public static SceneKernelV2Composition FromRuntimeSurface(IKernelBootRuntimeSurface runtimeSurface)
        {
            if (runtimeSurface == null)
                throw new ArgumentNullException(nameof(runtimeSurface));

            if (runtimeSurface is not KernelBootRuntimeSurface concreteSurface)
            {
                throw new ArgumentException(
                    "SceneKernelV2Composition requires the concrete KernelBootRuntimeSurface so scene-local V2 runtime parts can be extracted without rebuilding them.",
                    nameof(runtimeSurface));
            }

            KernelRuntime runtime = concreteSurface.Runtime;
            return new SceneKernelV2Composition(
                runtimeSurface,
                runtime.ServiceGraph,
                runtime.RootScopeGraph,
                concreteSurface.LifecyclePlanResolver,
                concreteSurface.LifecycleDispatcher);
        }

        public void BindRuntimeSurface(IKernelBootRuntimeSurface runtimeSurface)
        {
            if (runtimeSurface == null)
                throw new ArgumentNullException(nameof(runtimeSurface));

            if (runtimeSurface is not KernelBootRuntimeSurface concreteSurface)
            {
                throw new ArgumentException(
                    "SceneKernelV2Composition requires the concrete KernelBootRuntimeSurface so scene-local V2 runtime parts can be extracted without rebuilding them.",
                    nameof(runtimeSurface));
            }

            KernelRuntime runtime = concreteSurface.Runtime;
            BindRuntimeSurface(
                runtimeSurface,
                runtime.ServiceGraph,
                runtime.RootScopeGraph,
                concreteSurface.LifecyclePlanResolver,
                concreteSurface.LifecycleDispatcher);
        }

        public void ClearRuntimeBinding()
        {
            runtimeSurface = null;
            serviceGraph = null;
            scopeGraph = null;
            lifecycleDispatcher = null;
            lifecyclePlanResolver = null;
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
                case SceneKernelBoundaryKind.LifecycleDispatcher:
                    boundary = lifecycleDispatcher;
                    return lifecycleDispatcher != null;
                case SceneKernelBoundaryKind.LifecyclePlanResolver:
                    boundary = lifecyclePlanResolver;
                    return lifecyclePlanResolver != null;
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
            KernelLifecycleDispatcher? lifecycleDispatcher)
        {
            this.runtimeSurface = runtimeSurface ?? throw new ArgumentNullException(nameof(runtimeSurface));
            this.serviceGraph = serviceGraph ?? throw new ArgumentNullException(nameof(serviceGraph));
            this.scopeGraph = scopeGraph ?? throw new ArgumentNullException(nameof(scopeGraph));
            this.lifecyclePlanResolver = lifecyclePlanResolver ?? throw new ArgumentNullException(nameof(lifecyclePlanResolver));
            this.lifecycleDispatcher = lifecycleDispatcher;
        }
    }
}

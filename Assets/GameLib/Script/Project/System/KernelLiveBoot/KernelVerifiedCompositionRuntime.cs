#nullable enable
using System;
using System.Collections.Generic;
using Game.DI;
using Game.Kernel.Boot;
using Game.Kernel.IR;
using UnityEngine;

namespace Game.Project.Bootstrap
{
    public static class KernelVerifiedCompositionRuntime
    {
        sealed class ScopeBindingSink : VerifiedCompositionRuntime.IVerifiedScopeBindingSink
        {
            public bool TryBindRuntimeScope(BaseRuntimeTemplateSO template, IScopeGraphHost scope, IScopeNode? explicitParent)
            {
                if (template == null)
                    throw new ArgumentNullException(nameof(template));
                if (scope == null)
                    throw new ArgumentNullException(nameof(scope));
                if (s_runtimeSurface == null)
                    return false;

                int planIdValue = template.VerifiedScopePlanId;
                if (planIdValue <= 0)
                    return false;

                ScopeHandle parentHandle = default;
                if (explicitParent is IScopeGraphHost runtimeParent)
                {
                    if (!TryGetBoundScopeHandle(runtimeParent, out parentHandle))
                        return false;
                }

                ScopePlanId planId = new ScopePlanId(planIdValue);
                ScopeCreateMode mode = parentHandle.IsDefault ? ScopeCreateMode.Root : ScopeCreateMode.Child;
                ScopeHandle handle = s_runtimeSurface.Runtime.RootScopeGraph.CreateScope(
                    new ScopeCreateRequest(planId, parentHandle, mode, default, new SourceLocationId(planIdValue)));

                BindScope(scope, handle, explicitParent);
                return true;
            }

            public void ReleaseRuntimeScope(IScopeGraphHost scope)
            {
                if (scope == null)
                    throw new ArgumentNullException(nameof(scope));

                if (!s_boundScopes.TryGetValue(scope, out ScopeHandle handle))
                    return;

                s_boundScopes.Remove(scope);

                if (s_runtimeSurface != null)
                    s_runtimeSurface.Runtime.RootScopeGraph.TryDestroyScope(handle);
            }

            public bool TryUpdateRuntimeScopeState(IScopeGraphHost scope, int nextState)
            {
                if (scope == null)
                    throw new ArgumentNullException(nameof(scope));

                if (!Enum.IsDefined(typeof(ScopeRuntimeState), nextState))
                    return false;

                return s_runtimeSurface != null
                    && s_boundScopes.TryGetValue(scope, out ScopeHandle handle)
                    && s_runtimeSurface.Runtime.RootScopeGraph.TrySetState(handle, (ScopeRuntimeState)nextState);
            }

            public bool TryRefreshRuntimeScopeUnityLink(IScopeGraphHost scope)
            {
                if (scope == null)
                    throw new ArgumentNullException(nameof(scope));

                return s_runtimeSurface != null
                    && s_boundScopes.TryGetValue(scope, out ScopeHandle handle)
                    && s_runtimeSurface.Runtime.RootScopeGraph.TrySetUnityLink(handle, CreateUnityLink(scope));
            }
        }

        static KernelBootRuntimeSurface? s_runtimeSurface;
        static readonly ScopeBindingSink s_scopeBindingSink = new();
        static readonly Dictionary<IScopeGraphHost, ScopeHandle> s_boundScopes =
            new(ReferenceEqualityComparer<IScopeGraphHost>.Instance);

        public static bool IsActive => s_runtimeSurface != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_runtimeSurface = null;
            s_boundScopes.Clear();
            VerifiedCompositionRuntime.Deactivate();
        }

        public static void Activate(IKernelBootRuntimeSurface runtimeSurface)
        {
            if (runtimeSurface is not KernelBootRuntimeSurface kernelRuntimeSurface)
                throw new InvalidOperationException("Wave B verified composition requires a KernelBootRuntimeSurface.");

            s_runtimeSurface = kernelRuntimeSurface;
            s_boundScopes.Clear();
            VerifiedCompositionRuntime.Activate(s_scopeBindingSink);
        }

        public static void Deactivate()
        {
            s_boundScopes.Clear();
            s_runtimeSurface = null;
            VerifiedCompositionRuntime.Deactivate();
        }

        public static bool TryGetRuntimeSurface(out KernelBootRuntimeSurface? runtimeSurface)
        {
            runtimeSurface = s_runtimeSurface;
            return runtimeSurface != null;
        }

        public static bool TryResolveRootScopeHandle(ScopePlanId planId, out ScopeHandle handle)
        {
            handle = default;
            if (s_runtimeSurface == null)
                return false;

            IReadOnlyList<ScopeHandle> rootHandles = s_runtimeSurface.Runtime.RootScopeGraph.RootScopeHandles;
            for (int index = 0; index < rootHandles.Count; index++)
            {
                ScopeHandle candidate = rootHandles[index];
                if (!s_runtimeSurface.Runtime.RootScopeGraph.TryGetScopeBoundary(candidate, out ScopeBoundarySnapshot boundary))
                    continue;

                if (boundary.PlanId != planId)
                    continue;

                handle = candidate;
                return true;
            }

            return false;
        }

        public static bool TryBindRootScope(IScopeGraphHost scope, ScopePlanId planId, IScopeNode? explicitParent = null)
        {
            if (scope == null)
                throw new ArgumentNullException(nameof(scope));

            if (!TryResolveRootScopeHandle(planId, out ScopeHandle handle))
                return false;

            BindScope(scope, handle, explicitParent);
            return true;
        }

        public static void BindScope(IScopeGraphHost scope, ScopeHandle handle, IScopeNode? explicitParent = null)
        {
            if (scope == null)
                throw new ArgumentNullException(nameof(scope));

            if (explicitParent != null)
                scope.SetExplicitBuildParent(explicitParent);

            s_boundScopes[scope] = handle;

            if (!s_scopeBindingSink.TryRefreshRuntimeScopeUnityLink(scope))
                throw new InvalidOperationException("Kernel verified composition could not synchronize the Unity link for a bound runtime scope.");
        }

        public static bool TryGetBoundScopeHandle(IScopeGraphHost scope, out ScopeHandle handle)
        {
            if (scope == null)
                throw new ArgumentNullException(nameof(scope));

            return s_boundScopes.TryGetValue(scope, out handle);
        }

        public static bool TryGetBoundScopeHandle(IScopeNode scope, out ScopeHandle handle)
        {
            if (scope == null)
                throw new ArgumentNullException(nameof(scope));

            if (scope is IScopeGraphHost scopeHost)
                return TryGetBoundScopeHandle(scopeHost, out handle);

            handle = default;
            return false;
        }

        static UnityObjectLink CreateUnityLink(IScopeGraphHost scope)
        {
            GameObject hostGameObject = scope.HostGameObject;
            string debugName = hostGameObject != null ? hostGameObject.name : scope.HostComponent.name;
            int runtimeInstanceId = hostGameObject != null ? Math.Max(hostGameObject.GetInstanceID(), 0) : 0;
            return new UnityObjectLink(UnityObjectLinkKind.Runtime, sourceGuid: null, localFileId: 0, runtimeInstanceId, debugName);
        }
    }
}
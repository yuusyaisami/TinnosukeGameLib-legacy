#nullable enable

using System;
using System.Collections.Generic;
using Game.Kernel.Layers;
using UnityEngine;

namespace Game.Kernel.Authoring
{
    [DisallowMultipleComponent]
    public sealed class SceneKernelSpawnDeclarationMB : MonoBehaviour
    {
        [SerializeField]
        Transform? defaultParkingRoot;

        [SerializeField]
        bool enableDebugView = true;

        [SerializeField]
        List<SceneKernelSpawnRouteDeclaration> routes = new List<SceneKernelSpawnRouteDeclaration>();

        [SerializeField]
        List<SceneKernelSpawnWarmupDeclaration> warmupEntries = new List<SceneKernelSpawnWarmupDeclaration>();

        public Transform? DefaultParkingRoot => defaultParkingRoot;

        public bool EnableDebugView => enableDebugView;

        public IReadOnlyList<SceneKernelSpawnRouteDeclaration> Routes => routes;

        public IReadOnlyList<SceneKernelSpawnWarmupDeclaration> Warmups => warmupEntries;

        public int RouteCount => routes.Count;

        public int WarmupCount => warmupEntries.Count;

        public bool TryGetRoute(SceneKernelSpawnRouteId routeId, out SceneKernelSpawnRouteDeclaration route)
        {
            EnsureCollections();

            for (int index = 0; index < routes.Count; index++)
            {
                SceneKernelSpawnRouteDeclaration? candidate = routes[index];
                if (candidate == null)
                    continue;

                if (candidate.KernelRouteId == routeId)
                {
                    route = candidate;
                    return true;
                }
            }

            route = null!;
            return false;
        }

        public bool TryValidateDeclarations(out string failureReason)
        {
            EnsureCollections();

            HashSet<string> seenRouteKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < routes.Count; index++)
            {
                SceneKernelSpawnRouteDeclaration? route = routes[index];
                if (route == null)
                {
                    failureReason = "SceneKernel spawn declarations cannot contain null route entries.";
                    return false;
                }

                route.Normalize();
                if (!route.TryValidate(out failureReason))
                    return false;

                if (!seenRouteKeys.Add(route.KernelRouteId.Value))
                {
                    failureReason = "SceneKernel spawn declarations require unique RuntimeEntity/RuntimeUIElement route keys per scene.";
                    return false;
                }
            }

            for (int index = 0; index < warmupEntries.Count; index++)
            {
                SceneKernelSpawnWarmupDeclaration? warmup = warmupEntries[index];
                if (warmup == null)
                {
                    failureReason = "SceneKernel spawn declarations cannot contain null warmup entries.";
                    return false;
                }

                warmup.Normalize();
                if (!warmup.TryValidate(out failureReason))
                    return false;

                if (warmup.Count > 0 && !seenRouteKeys.Contains(warmup.KernelRouteId.Value))
                {
                    failureReason = "SceneKernel warmup entries must reference a declared RuntimeEntity/RuntimeUIElement route.";
                    return false;
                }
            }

            failureReason = string.Empty;
            return true;
        }

        public bool TryBindDeclaredRoutes(ISceneKernelSpawnBoundary boundary, out string failureReason)
        {
            if (boundary == null)
                throw new ArgumentNullException(nameof(boundary));

            if (!TryValidateDeclarations(out failureReason))
                return false;

            for (int index = 0; index < routes.Count; index++)
            {
                SceneKernelSpawnRouteDeclaration route = routes[index];
                if (!boundary.TryBindSpawnRoute(route.KernelRouteId, route.PoolId))
                {
                    failureReason = "SceneKernel spawn declaration could not bind a declared route into the current scene boundary. Route=" + route.KernelRouteId.Value;
                    return false;
                }
            }

            failureReason = string.Empty;
            return true;
        }

        void OnValidate()
        {
            EnsureCollections();

            for (int index = 0; index < routes.Count; index++)
                routes[index]?.Normalize();

            for (int index = 0; index < warmupEntries.Count; index++)
                warmupEntries[index]?.Normalize();
        }

        void EnsureCollections()
        {
            routes ??= new List<SceneKernelSpawnRouteDeclaration>();
            warmupEntries ??= new List<SceneKernelSpawnWarmupDeclaration>();
        }

#if UNITY_EDITOR
        public void SetRoutesForEditor(params SceneKernelSpawnRouteDeclaration[] declarations)
        {
            EnsureCollections();
            routes.Clear();

            if (declarations == null)
                return;

            for (int index = 0; index < declarations.Length; index++)
            {
                if (declarations[index] != null)
                    routes.Add(declarations[index]);
            }
        }

        public void SetWarmupsForEditor(params SceneKernelSpawnWarmupDeclaration[] declarations)
        {
            EnsureCollections();
            warmupEntries.Clear();

            if (declarations == null)
                return;

            for (int index = 0; index < declarations.Length; index++)
            {
                if (declarations[index] != null)
                    warmupEntries.Add(declarations[index]);
            }
        }
#endif
    }
}
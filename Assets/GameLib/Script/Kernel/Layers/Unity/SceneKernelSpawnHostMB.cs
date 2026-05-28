#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.DI;
using Game.Kernel.Authoring;
using UnityEngine;

namespace Game.Kernel.Layers.Unity
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SceneKernelHostMB), typeof(SceneKernelSpawnDeclarationMB))]
    [DefaultExecutionOrder(-31980)]
    public sealed class SceneKernelSpawnHostMB : MonoBehaviour
    {
        SceneKernelHostMB? sceneKernelHost;
        SceneKernelSpawnDeclarationMB? spawnDeclaration;
        ISceneKernelSpawnBoundary? spawnBoundary;

        public SceneKernelHostMB SceneKernelHost => sceneKernelHost ?? throw new InvalidOperationException("SceneKernelSpawnHostMB has not been initialized.");

        public SceneKernelSpawnDeclarationMB SpawnDeclaration => spawnDeclaration ?? throw new InvalidOperationException("SceneKernelSpawnHostMB has not been initialized.");

        public SceneKernel RuntimeKernel => SceneKernelHost.RuntimeKernel;

        public ISceneKernelSpawnBoundary SpawnBoundary => spawnBoundary ?? throw new InvalidOperationException("SceneKernelSpawnHostMB has not been initialized.");

        void Awake()
        {
            if (transform.parent != null)
                throw new InvalidOperationException("SceneKernelSpawnHostMB must be placed at the scene root.");

            sceneKernelHost ??= GetComponent<SceneKernelHostMB>() ?? throw new InvalidOperationException("SceneKernelSpawnHostMB requires SceneKernelHostMB on the same scene root.");
            spawnDeclaration ??= GetComponent<SceneKernelSpawnDeclarationMB>() ?? throw new InvalidOperationException("SceneKernelSpawnHostMB requires SceneKernelSpawnDeclarationMB on the same scene root.");

            if (!spawnDeclaration.TryValidateDeclarations(out string failureReason))
                throw new InvalidOperationException("SceneKernelSpawnDeclarationMB is invalid: " + failureReason);

            if (!sceneKernelHost.RuntimeKernel.TryGetSpawnBoundary(out ISceneKernelSpawnBoundary resolvedBoundary) || resolvedBoundary == null)
            {
                throw new InvalidOperationException(
                    "SceneKernelSpawnHostMB requires an attached SceneKernel composition with an operational spawn boundary.");
            }

            if (!spawnDeclaration.TryBindDeclaredRoutes(resolvedBoundary, out failureReason))
                throw new InvalidOperationException("SceneKernelSpawnHostMB could not bind declared routes: " + failureReason);

            spawnBoundary = resolvedBoundary;
            WarmupDeclaredEntriesAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        void OnDestroy()
        {
            spawnBoundary = null;
            spawnDeclaration = null;
            sceneKernelHost = null;
        }

        public bool TryGetSpawnBoundary(out ISceneKernelSpawnBoundary boundary)
        {
            boundary = spawnBoundary!;
            return boundary != null;
        }

        public bool TryGetRouteDeclaration(SceneKernelSpawnRouteId routeId, out SceneKernelSpawnRouteDeclaration route)
        {
            if (spawnDeclaration != null)
                return spawnDeclaration.TryGetRoute(routeId, out route);

            route = null!;
            return false;
        }

        public async UniTask WarmupDeclaredEntriesAsync(CancellationToken cancellationToken = default)
        {
            if (spawnBoundary == null)
                throw new InvalidOperationException("SceneKernelSpawnHostMB has not been initialized.");

            if (spawnDeclaration == null)
                throw new InvalidOperationException("SceneKernelSpawnHostMB has not been initialized.");

            for (int index = 0; index < spawnDeclaration.WarmupCount; index++)
            {
                SceneKernelSpawnWarmupDeclaration warmup = spawnDeclaration.Warmups[index];
                if (warmup == null || warmup.Count <= 0)
                    continue;

                if (!spawnDeclaration.TryGetRoute(warmup.KernelRouteId, out SceneKernelSpawnRouteDeclaration route))
                    throw new InvalidOperationException("SceneKernelSpawnHostMB could not resolve a warmup route: " + warmup.KernelRouteId.Value);

                if (!warmup.TryResolveTemplate(out BaseRuntimeTemplateSO runtimeTemplate, out string failureReason))
                    throw new InvalidOperationException("SceneKernelSpawnHostMB could not resolve a warmup template: " + failureReason);

                SceneKernelWarmupResult result = await spawnBoundary.WarmupAsync(
                    new SceneKernelWarmupRequest(route.KernelRouteId, runtimeTemplate, warmup.Count, route.ParkingRoot ?? spawnDeclaration.DefaultParkingRoot),
                    cancellationToken);

                if (!result.Succeeded)
                    throw new InvalidOperationException("SceneKernelSpawnHostMB could not warm up declared routes: " + (result.Diagnostic?.Message ?? "Unknown failure."));
            }
        }
    }
}
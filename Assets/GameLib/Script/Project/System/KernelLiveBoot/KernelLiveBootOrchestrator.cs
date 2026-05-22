#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Flow;
using Game.Kernel.Boot;
using Game.Kernel.IR;
using UnityEngine;

namespace Game.Project.Bootstrap
{
    public sealed class KernelLiveBootResult
    {
        KernelLiveBootResult(
            bool isSuccessful,
            string message,
            Exception? exception,
            KernelBootBoundaryResult? boundaryResult,
            KernelLiveBootPersistentRootInstance? projectRoot,
            KernelLiveBootPersistentRootInstance? globalRoot)
        {
            IsSuccessful = isSuccessful;
            Message = message;
            Exception = exception;
            BoundaryResult = boundaryResult;
            ProjectRoot = projectRoot;
            GlobalRoot = globalRoot;
        }

        public bool IsSuccessful { get; }

        public string Message { get; }

        public Exception? Exception { get; }

        public KernelBootBoundaryResult? BoundaryResult { get; }

        public KernelLiveBootPersistentRootInstance? ProjectRoot { get; }

        public KernelLiveBootPersistentRootInstance? GlobalRoot { get; }

        public static KernelLiveBootResult Success(
            KernelBootBoundaryResult boundaryResult,
            KernelLiveBootPersistentRootInstance projectRoot,
            KernelLiveBootPersistentRootInstance globalRoot)
        {
            return new KernelLiveBootResult(true, string.Empty, null, boundaryResult, projectRoot, globalRoot);
        }

        public static KernelLiveBootResult Failure(string message, Exception? exception = null, KernelBootBoundaryResult? boundaryResult = null)
        {
            return new KernelLiveBootResult(false, message, exception, boundaryResult, null, null);
        }
    }

    public static class KernelLiveBootOrchestrator
    {
        public static async UniTask<KernelLiveBootResult> ExecuteAsync(KernelLiveBootBundleAsset bundle, CancellationToken cancellationToken = default)
        {
            if (bundle == null)
                throw new ArgumentNullException(nameof(bundle));

            KernelLiveBootRuntime.BeginVerifiedBoot(bundle.LoadingParentKind);

            try
            {
                KernelBootPublishedArtifactBundle publishedBundle = bundle.CreatePublishedArtifactBundle();
                BootValidationInput bootInput = publishedBundle.CreateValidationInput(KernelLiveBootRuntime.CreateFallbackStateSnapshot());
                KernelBootBoundaryResult boundaryResult = KernelBootBoundary.Execute(bootInput);
                if (!boundaryResult.IsReady)
                {
                    KernelVerifiedValueRuntime.Deactivate();
                    KernelVerifiedCommandRuntime.Deactivate();
                    KernelVerifiedCompositionRuntime.Deactivate();
                    KernelLiveBootRuntime.AbortVerifiedBoot();
                    return KernelLiveBootResult.Failure("Verified live boot was blocked by boot validation.", boundaryResult: boundaryResult);
                }

                if (boundaryResult is not KernelBootBoundaryResult.Success success
                    || success.RuntimeSurface is not KernelBootRuntimeSurface)
                {
                    KernelVerifiedValueRuntime.Deactivate();
                    KernelVerifiedCommandRuntime.Deactivate();
                    KernelVerifiedCompositionRuntime.Deactivate();
                    KernelLiveBootRuntime.AbortVerifiedBoot();
                    return KernelLiveBootResult.Failure("Verified live boot requires a KernelBootRuntimeSurface for Wave B composition authority.", boundaryResult: boundaryResult);
                }

                KernelVerifiedValueRuntime.Activate(success.RuntimeSurface);
                KernelVerifiedCommandRuntime.Activate(success.RuntimeSurface);
                KernelVerifiedCompositionRuntime.Activate(success.RuntimeSurface);

                EnsureNoPreexistingPersistentRoots();

                KernelLiveBootPersistentRootInstance projectRoot = bundle.InstantiateProjectRootHost();
                try
                {
                    global::Game.IScopeGraphHost platformRoot = ResolvePlatformRootHost(projectRoot.RootScope);
                    BindConfiguredPersistentRootScopes(bundle, projectRoot.RootScope, platformRoot, globalRoot: null);

                    projectRoot.RootScope.EnsureScopeBuilt();
                    await projectRoot.RootScope.WhenBuiltAsync(cancellationToken);

                    KernelLiveBootPersistentRootInstance globalRoot = bundle.InstantiateGlobalRootHost(platformRoot.HostTransform);
                    try
                    {
                        BindConfiguredPersistentRootScopes(bundle, projectRoot.RootScope, platformRoot, globalRoot.RootScope);

                        globalRoot.RootScope.EnsureScopeBuilt();
                        await globalRoot.RootScope.WhenBuiltAsync(cancellationToken);

                        Transform loadingParent = ResolveLoadingParent(bundle.LoadingParentKind, projectRoot.RootTransform, platformRoot.HostTransform, globalRoot.RootTransform);
                        KernelLiveBootRuntime.CompleteVerifiedBoot(loadingParent);

                        if (bundle.AutoLoadInitialScene)
                            await LoadInitialSceneAsync(projectRoot.RootScope, bundle.ResolveInitialSceneName(), cancellationToken);

                        return KernelLiveBootResult.Success(boundaryResult, projectRoot, globalRoot);
                    }
                    catch
                    {
                        UnityEngine.Object.Destroy(globalRoot.RootGameObject);

                        throw;
                    }
                }
                catch
                {
                    UnityEngine.Object.Destroy(projectRoot.RootGameObject);

                    throw;
                }
            }
            catch (Exception exception)
            {
                KernelVerifiedValueRuntime.Deactivate();
                KernelVerifiedCommandRuntime.Deactivate();
                KernelVerifiedCompositionRuntime.Deactivate();
                KernelLiveBootRuntime.AbortVerifiedBoot();
                return KernelLiveBootResult.Failure(exception.Message, exception);
            }
        }

        static global::Game.IScopeGraphHost ResolvePlatformRootHost(global::Game.IScopeGraphHost projectRoot)
        {
            MonoBehaviour[] components = projectRoot.HostGameObject.GetComponentsInChildren<MonoBehaviour>(true);
            global::Game.IScopeGraphHost? platformRoot = null;
            for (int index = 0; index < components.Length; index++)
            {
                if (components[index] is not global::Game.IScopeGraphHost scope)
                    continue;

                if (ReferenceEquals(scope.HostComponent, projectRoot.HostComponent))
                    continue;

                global::Game.LifetimeScopeKind resolvedKind = global::Game.ScopeIdentityMB.PredictKindFromComponent(scope.HostComponent, scope.Kind);
                if (resolvedKind != global::Game.LifetimeScopeKind.Platform)
                    continue;

                if (platformRoot != null)
                {
                    throw new InvalidOperationException(
                        "Kernel live boot requires exactly one Platform-kind runtime scope beneath the explicit project root host prefab.");
                }

                platformRoot = scope;
            }

            if (platformRoot == null)
            {
                throw new InvalidOperationException(
                    "Kernel live boot requires the explicit project root host prefab to contain a Platform-kind scope-graph host child.");
            }

            return platformRoot;
        }

        static void BindConfiguredPersistentRootScopes(
            KernelLiveBootBundleAsset bundle,
            global::Game.IScopeGraphHost projectRoot,
            global::Game.IScopeGraphHost platformRoot,
            global::Game.IScopeGraphHost? globalRoot)
        {
            if (!bundle.TryGetProjectRootScopePlanId(out ScopePlanId projectPlanId))
                throw new InvalidOperationException("Kernel live boot requires an explicit Project root scope plan id.");

            if (!KernelVerifiedCompositionRuntime.TryBindRootScope(projectRoot, projectPlanId))
            {
                throw new InvalidOperationException("Kernel live boot could not bind the configured Project root scope plan to a verified root handle.");
            }

            if (!bundle.TryGetPlatformRootScopePlanId(out ScopePlanId platformPlanId))
                throw new InvalidOperationException("Kernel live boot requires an explicit Platform root scope plan id.");

            if (!KernelVerifiedCompositionRuntime.TryBindRootScope(platformRoot, platformPlanId, projectRoot))
            {
                throw new InvalidOperationException("Kernel live boot could not bind the configured Platform root scope plan to a verified root handle.");
            }

            if (globalRoot == null)
                return;

            if (!bundle.TryGetGlobalRootScopePlanId(out ScopePlanId globalPlanId))
                throw new InvalidOperationException("Kernel live boot requires an explicit Global root scope plan id.");

            if (!KernelVerifiedCompositionRuntime.TryBindRootScope(globalRoot, globalPlanId, platformRoot))
            {
                throw new InvalidOperationException("Kernel live boot could not bind the configured Global root scope plan to a verified root handle.");
            }
        }

        static void EnsureNoPreexistingPersistentRoots()
        {
            MonoBehaviour[] runtimeComponents = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < runtimeComponents.Length; index++)
            {
                if (runtimeComponents[index] is not global::Game.IScopeGraphHost scope)
                    continue;

                global::Game.LifetimeScopeKind resolvedKind = global::Game.ScopeIdentityMB.PredictKindFromComponent(scope.HostComponent, scope.Kind);
                switch (resolvedKind)
                {
                    case global::Game.LifetimeScopeKind.Project:
                        throw new InvalidOperationException(
                            $"Verified live boot detected a pre-existing project root host '{scope.HostComponent.GetType().Name}'. Mixed persistent-root authority is not allowed.");
                    case global::Game.LifetimeScopeKind.Global:
                        throw new InvalidOperationException(
                            $"Verified live boot detected a pre-existing global root host '{scope.HostComponent.GetType().Name}'. Mixed persistent-root authority is not allowed.");
                }
            }
        }

        static Transform ResolveLoadingParent(
            KernelLiveBootLoadingParentKind loadingParentKind,
            Transform projectRoot,
            Transform platformRoot,
            Transform globalRoot)
        {
            return loadingParentKind switch
            {
                KernelLiveBootLoadingParentKind.ProjectRoot => projectRoot,
                KernelLiveBootLoadingParentKind.PlatformRoot => platformRoot,
                KernelLiveBootLoadingParentKind.GlobalRoot => globalRoot,
                _ => throw new InvalidOperationException("Kernel live boot requires a concrete loading parent kind."),
            };
        }

        static async UniTask LoadInitialSceneAsync(global::Game.IScopeGraphHost projectRoot, string sceneName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                throw new InvalidOperationException("Kernel live boot initial scene is not configured.");

            if (projectRoot.Resolver == null)
                projectRoot.EnsureScopeBuilt();

            if (projectRoot.Resolver == null || !projectRoot.Resolver.TryResolve<ISceneService>(out ISceneService sceneService) || sceneService == null)
                throw new InvalidOperationException("Kernel live boot could not resolve the preserved ISceneService from the explicit project root host.");

            cancellationToken.ThrowIfCancellationRequested();
            await sceneService.LoadSingle(sceneName);
        }
    }

}

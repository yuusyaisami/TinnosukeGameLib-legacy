#nullable enable
using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Game.Commands.VNext;
using Game.Common;
using Game.Flow;
using Game.Kernel.Abstractions;
using Game.Kernel.Boot;
using Game.Kernel.Diagnostics;
using Game.Kernel.Generation;
using Game.Kernel.IR;
using Game.Kernel.Validation;
using Game.Loading;
using Game.Project;
using Game.Project.Bootstrap;
using Game.Scene;
using NUnit.Framework;
using UnityEngine;
using Game.Vars.Generated;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class KernelV21LiveBootTests
    {
        [SetUp]
        public void SetUp()
        {
            ResetLiveBootRuntime();
        }

        [TearDown]
        public void TearDown()
        {
            ResetLiveBootRuntime();
        }

        [Test]
        public void MinimalPublishedBundle_ProducesReadyBootBoundaryResult()
        {
            KernelBootPublishedArtifactBundle bundle = KernelBootPublishedArtifactBundleFactory.CreateMinimal(
                new KernelProfile(new KernelProfileId(21001), KernelProfileKind.Development),
                new ManifestId(21001),
                new BootPolicyId(21001),
                new PlanId(21001),
                new ArtifactSetId(21001),
                formatVersion: 1,
                generatorVersion: "V21-M1");

            BootValidationInput input = bundle.CreateValidationInput(new BootFallbackValidationState(false, false, false, false, false, false));

            BootValidationReport report = BootValidator.Validate(input);
            KernelBootBoundaryResult result = KernelBootBoundary.Execute(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Passed));
            Assert.That(result.Status, Is.EqualTo(KernelBootBoundaryStatus.Ready));
            Assert.That(result.IsReady, Is.True);
            Assert.That(result, Is.InstanceOf<KernelBootBoundaryResult.Success>());

            KernelBootBoundaryResult.Success success = (KernelBootBoundaryResult.Success)result;
            Assert.That(success.RuntimeSurface, Is.InstanceOf<KernelBootRuntimeSurface>());

            KernelBootRuntimeSurface runtimeSurface = (KernelBootRuntimeSurface)success.RuntimeSurface;
            Assert.That(runtimeSurface.Runtime.ServiceGraph.RootServiceCount, Is.EqualTo(0));
            Assert.That(runtimeSurface.Runtime.RootScopeGraph.RootScopeCount, Is.EqualTo(0));
            Assert.That(runtimeSurface.Runtime.CommandCatalogPlan, Is.Not.Null);
            Assert.That(runtimeSurface.Runtime.CommandCatalogPlan!.Entries.Length, Is.EqualTo(0));
            Assert.That(runtimeSurface.Runtime.ValueSchemaPlan, Is.Not.Null);
            Assert.That(runtimeSurface.Runtime.ValueSchemaPlan!.ValueKeys.Length, Is.EqualTo(0));
            Assert.That(runtimeSurface.Runtime.RuntimeQueryPlan, Is.Not.Null);
            Assert.That(runtimeSurface.Runtime.RuntimeQueryPlan!.RuntimeQueries.Length, Is.EqualTo(0));
            Assert.That(runtimeSurface.Runtime.DebugMap.Entries.Length, Is.EqualTo(0));
            Assert.That(runtimeSurface.Runtime.Diagnostics.ValidationReport.Status, Is.EqualTo(ValidationResultStatus.Passed));
        }

        [Test]
        public void LiveBootRuntime_SuppressesLegacyBootstrapOnlyInsideVerifiedBootSession()
        {
            Assert.That(KernelLiveBootRuntime.IsLegacyAutoBootstrapSuppressed, Is.False);

            KernelLiveBootRuntime.BeginVerifiedBoot(KernelLiveBootLoadingParentKind.PlatformRoot);
            Assert.That(KernelLiveBootRuntime.IsLegacyAutoBootstrapSuppressed, Is.True);
            Assert.That(KernelLiveBootRuntime.IsVerifiedBootInProgress, Is.True);
            Assert.That(KernelLiveBootRuntime.IsVerifiedLiveBootReady, Is.False);

            InvokeLegacyEnsureInScene("Game.ProjectLifetimeScope");
            InvokeLegacyEnsureInScene("Game.GlobalLifetimeScope");

            Assert.That(KernelLiveBootRuntime.CreateFallbackStateSnapshot().LegacyFallbackAttempted, Is.False);

            KernelLiveBootRuntime.AbortVerifiedBoot();

            Assert.That(KernelLiveBootRuntime.IsLegacyAutoBootstrapSuppressed, Is.False);
            Assert.That(KernelLiveBootRuntime.IsVerifiedBootInProgress, Is.False);
            Assert.That(KernelLiveBootRuntime.IsVerifiedLiveBootReady, Is.False);
        }

        [Test]
        public void LegacyAutoBootstrap_ThrowsExplicitFailureOutsideVerifiedBoot()
        {
            Assert.That(KernelLiveBootRuntime.IsVerifiedLiveBootActive, Is.False);
            Assert.That(KernelLiveBootRuntime.IsLegacyAutoBootstrapSuppressed, Is.False);

            int projectBefore = CountRuntimeComponentInstances("Game.ProjectLifetimeScope");
            int globalBefore = CountRuntimeComponentInstances("Game.GlobalLifetimeScope");

            InvalidOperationException projectException = Assert.Throws<InvalidOperationException>(() => InvokeLegacyEnsureInScene("Game.ProjectLifetimeScope"));
            InvalidOperationException globalException = Assert.Throws<InvalidOperationException>(() => InvokeLegacyEnsureInScene("Game.GlobalLifetimeScope"));

            Assert.That(projectException.Message, Does.Contain("ProjectLifetimeScope"));
            Assert.That(globalException.Message, Does.Contain("GlobalLifetimeScope"));
            Assert.That(KernelLiveBootRuntime.CreateFallbackStateSnapshot().LegacyFallbackAttempted, Is.True);
            Assert.That(CountRuntimeComponentInstances("Game.ProjectLifetimeScope"), Is.EqualTo(projectBefore));
            Assert.That(CountRuntimeComponentInstances("Game.GlobalLifetimeScope"), Is.EqualTo(globalBefore));
        }

        [Test]
        public void LoadingScreenService_RequiresVerifiedBootAuthorityBeforeAcquisition()
        {
            GameObject loadingPrefabObject = new GameObject("LoadingScreenPrefab");
            try
            {
                loadingPrefabObject.AddComponent<LoadingScreenMB>();
                LoadingScreenService service = new LoadingScreenService(new TestLoadingScreenConfig(loadingPrefabObject.GetComponent<LoadingScreenMB>()));

                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => service.OnAcquire(null!, false));
                Assert.That(exception!.Message, Does.Contain("verified live boot authority"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(loadingPrefabObject);
            }
        }

        [Test]
        public void LoadingScreenService_RequiresVerifiedSceneHandoffBeforeAcquisition()
        {
            GameObject loadingPrefabObject = new GameObject("LoadingScreenPrefab");
            GameObject parentObject = new GameObject("LoadingParent");
            try
            {
                LoadingScreenMB loadingPrefab = loadingPrefabObject.AddComponent<LoadingScreenMB>();
                KernelLiveBootRuntime.BeginVerifiedBoot(KernelLiveBootLoadingParentKind.GlobalRoot);
                KernelLiveBootRuntime.CompleteVerifiedBoot(parentObject.transform);

                LoadingScreenService service = new LoadingScreenService(new TestLoadingScreenConfig(loadingPrefab));

                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => service.OnAcquire(null!, false));
                Assert.That(exception!.Message, Does.Contain("scene handoff"));
                Assert.That(CountSingletonLoadingSceneInstances(), Is.EqualTo(0));

                KernelLiveBootRuntime.BeginSceneHandoff();

                Assert.That(() => service.OnAcquire(null!, false), Throws.Nothing);
                Assert.That(CountSingletonLoadingSceneInstances(), Is.EqualTo(1));

                service.Dispose();
                Assert.That(CountSingletonLoadingSceneInstances(), Is.EqualTo(0));
            }
            finally
            {
                KernelLiveBootRuntime.AbortVerifiedBoot();
                UnityEngine.Object.DestroyImmediate(loadingPrefabObject);
                UnityEngine.Object.DestroyImmediate(parentObject);
            }
        }

        [Test]
        public void LoadingScreenService_RejectsParentDriftInsteadOfRepairingIt()
        {
            GameObject loadingPrefabObject = new GameObject("LoadingScreenPrefab");
            GameObject parentObject = new GameObject("LoadingParent");
            GameObject rogueParentObject = new GameObject("RogueLoadingParent");
            try
            {
                LoadingScreenMB loadingPrefab = loadingPrefabObject.AddComponent<LoadingScreenMB>();
                KernelLiveBootRuntime.BeginVerifiedBoot(KernelLiveBootLoadingParentKind.GlobalRoot);
                KernelLiveBootRuntime.CompleteVerifiedBoot(parentObject.transform);
                KernelLiveBootRuntime.BeginSceneHandoff();

                LoadingScreenService service = new LoadingScreenService(new TestLoadingScreenConfig(loadingPrefab));
                service.OnAcquire(null!, false);

                Assert.That(CountSingletonLoadingSceneInstances(), Is.EqualTo(1));

                LoadingScreenMB? loadingScene = FindSingletonLoadingScene();
                Assert.That(loadingScene, Is.Not.Null);
                loadingScene!.transform.SetParent(rogueParentObject.transform, worldPositionStays: false);

                LoadingScreenService repairingService = new LoadingScreenService(new TestLoadingScreenConfig(loadingPrefab));

                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => repairingService.OnAcquire(null!, false));
                Assert.That(exception!.Message, Does.Contain("parent drift"));
                Assert.That(loadingScene.transform.parent, Is.SameAs(rogueParentObject.transform));

                repairingService.Dispose();
                Assert.That(CountSingletonLoadingSceneInstances(), Is.EqualTo(1));

                service.Dispose();
                Assert.That(CountSingletonLoadingSceneInstances(), Is.EqualTo(0));
            }
            finally
            {
                KernelLiveBootRuntime.AbortVerifiedBoot();
                UnityEngine.Object.DestroyImmediate(rogueParentObject);
                UnityEngine.Object.DestroyImmediate(loadingPrefabObject);
                UnityEngine.Object.DestroyImmediate(parentObject);
            }
        }

        [Test]
        public async System.Threading.Tasks.Task LoadingScreenService_ShowAsync_FailsClosedWithoutLeavingShowingState()
        {
            GameObject loadingPrefabObject = new GameObject("LoadingScreenPrefab");
            try
            {
                LoadingScreenMB loadingPrefab = loadingPrefabObject.AddComponent<LoadingScreenMB>();
                LoadingScreenService service = new LoadingScreenService(new TestLoadingScreenConfig(loadingPrefab));

                await service.ShowAsync("Loading...");

                Assert.That(service.IsShowing, Is.False);
                Assert.That(service.CurrentProgress, Is.EqualTo(0f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(loadingPrefabObject);
            }
        }

        [Test]
        public void LoadingScreenService_Dispose_DestroysVerifiedOwnedLoadingScene()
        {
            GameObject loadingPrefabObject = new GameObject("LoadingScreenPrefab");
            GameObject parentObject = new GameObject("LoadingParent");
            try
            {
                LoadingScreenMB loadingPrefab = loadingPrefabObject.AddComponent<LoadingScreenMB>();
                KernelLiveBootRuntime.BeginVerifiedBoot(KernelLiveBootLoadingParentKind.GlobalRoot);
                KernelLiveBootRuntime.CompleteVerifiedBoot(parentObject.transform);
                KernelLiveBootRuntime.BeginSceneHandoff();

                LoadingScreenService service = new LoadingScreenService(new TestLoadingScreenConfig(loadingPrefab));
                service.OnAcquire(null!, false);

                Assert.That(CountSingletonLoadingSceneInstances(), Is.EqualTo(1));

                service.Dispose();

                Assert.That(CountSingletonLoadingSceneInstances(), Is.EqualTo(0));
            }
            finally
            {
                KernelLiveBootRuntime.AbortVerifiedBoot();
                UnityEngine.Object.DestroyImmediate(loadingPrefabObject);
                UnityEngine.Object.DestroyImmediate(parentObject);
            }
        }

        [Test]
        public void MinimalPublishedBundle_RejectsNonDevelopmentProfiles()
        {
            Assert.Throws<InvalidOperationException>(() => KernelBootPublishedArtifactBundleFactory.CreateMinimal(
                new KernelProfile(new KernelProfileId(21002), KernelProfileKind.Release),
                new ManifestId(21002),
                new BootPolicyId(21002),
                new PlanId(21002),
                new ArtifactSetId(21002),
                formatVersion: 1,
                generatorVersion: "V21-M1"));
        }

        [Test]
        public void LiveBootBundleAsset_RequiresExplicitRootPlanIds_AndFailsClosedWithSyntheticBundle()
        {
            KernelLiveBootBundleAsset asset = ScriptableObject.CreateInstance<KernelLiveBootBundleAsset>();
            GameObject projectPrefab = new GameObject("KernelLiveBootProjectPrefab");
            GameObject globalPrefab = new GameObject("KernelLiveBootGlobalPrefab");
            try
            {
                SetNonPublicField(asset, "projectRootPrefab", projectPrefab);
                SetNonPublicField(asset, "globalRootPrefab", globalPrefab);

                Assert.That(() => asset.CreatePublishedArtifactBundle(), Throws.InvalidOperationException);

                SetNonPublicField(asset, "projectRootScopePlanId", 210);
                SetNonPublicField(asset, "platformRootScopePlanId", 220);
                SetNonPublicField(asset, "globalRootScopePlanId", 230);

                KernelBootPublishedArtifactBundle bundle = asset.CreatePublishedArtifactBundle();
                BootRootValidationState rootState = bundle.CreateRootState();
                ReadOnlySpan<RuntimeIdentityRef> requiredRootScopes = rootState.RequiredRootScopes;

                Assert.That(requiredRootScopes.Length, Is.EqualTo(3));
                Assert.That(requiredRootScopes[0], Is.EqualTo(new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, 210)));
                Assert.That(requiredRootScopes[1], Is.EqualTo(new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, 220)));
                Assert.That(requiredRootScopes[2], Is.EqualTo(new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, 230)));
                Assert.That(rootState.AvailableRootScopes.Length, Is.EqualTo(0));

                BootValidationInput input = bundle.CreateValidationInput(new BootFallbackValidationState(false, false, false, false, false, false));
                BootValidationReport report = BootValidator.Validate(input);
                KernelBootBoundaryResult result = KernelBootBoundary.Execute(input);

                Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
                Assert.That(result.IsReady, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
                UnityEngine.Object.DestroyImmediate(projectPrefab);
                UnityEngine.Object.DestroyImmediate(globalPrefab);
            }
        }

        [Test]
        public void LiveBootRuntime_TracksExplicitParentAndForbiddenFallbackSnapshot()
        {
            GameObject parentObject = new GameObject("KernelLiveBootParent");
            try
            {
                KernelLiveBootRuntime.BeginVerifiedBoot(KernelLiveBootLoadingParentKind.GlobalRoot);

                Assert.That(KernelLiveBootRuntime.IsVerifiedLiveBootActive, Is.True);
                Assert.That(KernelLiveBootRuntime.IsLegacyAutoBootstrapSuppressed, Is.True);
                Assert.That(KernelLiveBootRuntime.LoadingParentKind, Is.EqualTo(KernelLiveBootLoadingParentKind.GlobalRoot));
                Assert.That(KernelLiveBootRuntime.IsVerifiedLiveBootReady, Is.False);
                Assert.That(KernelLiveBootRuntime.IsSceneHandoffInProgress, Is.False);
                Assert.That(KernelLiveBootRuntime.IsSceneHandoffReady, Is.False);
                Assert.That(KernelLiveBootRuntime.TryGetExplicitLoadingParent(out Transform? beforeReadyParent), Is.False);
                Assert.That(beforeReadyParent, Is.Null);

                KernelLiveBootRuntime.RecordLegacyFallbackAttempt();
                KernelLiveBootRuntime.RecordRuntimeDiscoveryAttempt();
                KernelLiveBootRuntime.RecordResourcesFallbackAttempt();
                KernelLiveBootRuntime.RecordDefaultRootCreationAttempt();
                KernelLiveBootRuntime.RecordDuplicateRootCleanupAttempt();

                KernelLiveBootRuntime.CompleteVerifiedBoot(parentObject.transform);

                Assert.That(KernelLiveBootRuntime.IsVerifiedBootInProgress, Is.False);
                Assert.That(KernelLiveBootRuntime.IsVerifiedLiveBootReady, Is.True);
                Assert.That(KernelLiveBootRuntime.IsSceneHandoffInProgress, Is.False);
                Assert.That(KernelLiveBootRuntime.IsSceneHandoffReady, Is.False);
                Assert.That(KernelLiveBootRuntime.TryGetExplicitLoadingParent(out Transform? explicitParent), Is.True);
                Assert.That(explicitParent, Is.SameAs(parentObject.transform));

                KernelLiveBootRuntime.BeginSceneHandoff();

                Assert.That(KernelLiveBootRuntime.IsSceneHandoffInProgress, Is.True);
                Assert.That(KernelLiveBootRuntime.IsSceneHandoffReady, Is.False);

                KernelLiveBootRuntime.CompleteSceneHandoff();

                Assert.That(KernelLiveBootRuntime.IsSceneHandoffInProgress, Is.False);
                Assert.That(KernelLiveBootRuntime.IsSceneHandoffReady, Is.True);

                BootFallbackValidationState snapshot = KernelLiveBootRuntime.CreateFallbackStateSnapshot();
                Assert.That(snapshot.LegacyFallbackAttempted, Is.True);
                Assert.That(snapshot.RuntimeDiscoveryAttempted, Is.True);
                Assert.That(snapshot.ResourcesFallbackAttempted, Is.True);
                Assert.That(snapshot.DefaultRootCreationAttempted, Is.True);
                Assert.That(snapshot.DuplicateRootCleanupAttempted, Is.True);
                Assert.That(snapshot.NonDeterministicTestPolicy, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parentObject);
            }
        }

        [Test]
        public void SceneLifetimeScope_RejectsVerifiedBootParticipationBeforeSceneHandoff()
        {
            GameObject sceneObject = new GameObject("SceneLifetimeScopeTest");
            GameObject parentObject = new GameObject("LoadingParent");
            try
            {
                SceneLifetimeScope scope = sceneObject.AddComponent<SceneLifetimeScope>();
                KernelLiveBootRuntime.BeginVerifiedBoot(KernelLiveBootLoadingParentKind.GlobalRoot);
                KernelLiveBootRuntime.CompleteVerifiedBoot(parentObject.transform);

                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => InvokeSceneLifetimeScopeAwake(scope));
                Assert.That(exception!.Message, Does.Contain("scene handoff"));

                KernelLiveBootRuntime.BeginSceneHandoff();

                GameObject allowedObject = new GameObject("SceneLifetimeScopeAllowed");
                try
                {
                    SceneLifetimeScope allowedScope = allowedObject.AddComponent<SceneLifetimeScope>();
                    Assert.That(() => InvokeSceneLifetimeScopeAwake(allowedScope), Throws.Nothing);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(allowedObject);
                }
            }
            finally
            {
                KernelLiveBootRuntime.AbortVerifiedBoot();
                UnityEngine.Object.DestroyImmediate(sceneObject);
                UnityEngine.Object.DestroyImmediate(parentObject);
            }
        }

        [Test]
        public void SceneService_BeginsSceneHandoffWhenVerifiedHostIsReady()
        {
            GameObject parentObject = new GameObject("LoadingParent");
            try
            {
                KernelLiveBootRuntime.BeginVerifiedBoot(KernelLiveBootLoadingParentKind.GlobalRoot);
                KernelLiveBootRuntime.CompleteVerifiedBoot(parentObject.transform);

                bool began = InvokeSceneServiceBeginSceneHandoffIfRequired();

                Assert.That(began, Is.True);
                Assert.That(KernelLiveBootRuntime.IsSceneHandoffInProgress, Is.True);
                Assert.That(KernelLiveBootRuntime.IsSceneHandoffReady, Is.False);

                KernelLiveBootRuntime.CompleteSceneHandoff();

                bool secondAttempt = InvokeSceneServiceBeginSceneHandoffIfRequired();
                Assert.That(secondAttempt, Is.False);
            }
            finally
            {
                KernelLiveBootRuntime.AbortVerifiedBoot();
                UnityEngine.Object.DestroyImmediate(parentObject);
            }
        }

        static void ResetLiveBootRuntime()
        {
            MethodInfo? resetMethod = typeof(KernelLiveBootRuntime).GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(resetMethod, Is.Not.Null, "KernelLiveBootRuntime.ResetStatics must remain available for deterministic tests.");
            resetMethod!.Invoke(null, null);
        }

        static void InvokeLegacyEnsureInScene(string typeName)
        {
            MethodInfo? ensureMethod = ResolveRuntimeType(typeName)?.GetMethod("EnsureInScene", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(ensureMethod, Is.Not.Null, $"{typeName}.EnsureInScene must remain available for deterministic tests.");

            try
            {
                ensureMethod!.Invoke(null, null);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            }
        }

        static void InvokeSceneLifetimeScopeAwake(SceneLifetimeScope scope)
        {
            MethodInfo? awakeMethod = typeof(SceneLifetimeScope).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(awakeMethod, Is.Not.Null, "SceneLifetimeScope.Awake must remain available for deterministic tests.");

            try
            {
                awakeMethod!.Invoke(scope, null);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            }
        }

        static bool InvokeSceneServiceBeginSceneHandoffIfRequired()
        {
            MethodInfo? beginMethod = typeof(SceneService).GetMethod("BeginSceneHandoffIfRequired", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(beginMethod, Is.Not.Null, "SceneService.BeginSceneHandoffIfRequired must remain available for deterministic tests.");

            object? result = beginMethod!.Invoke(null, null);
            Assert.That(result, Is.TypeOf<bool>());
            return (bool)result!;
        }

        static Type? ResolveRuntimeType(string typeName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type? type = assemblies[i].GetType(typeName, throwOnError: false);
                if (type != null)
                    return type;
            }

            return null;
        }

        static int CountRuntimeComponentInstances(string typeName)
        {
            int count = 0;
            UnityEngine.Object[] objects = Resources.FindObjectsOfTypeAll<UnityEngine.Object>();
            for (int i = 0; i < objects.Length; i++)
            {
                UnityEngine.Object obj = objects[i];
                if (obj == null)
                    continue;

                if (obj is Component component && component.GetType().FullName == typeName)
                    count++;
            }

            return count;
        }

        static int CountSingletonLoadingSceneInstances()
        {
            int count = 0;
            LoadingScreenMB[] scopes = Resources.FindObjectsOfTypeAll<LoadingScreenMB>();
            for (int i = 0; i < scopes.Length; i++)
            {
                LoadingScreenMB scope = scopes[i];
                if (scope != null && scope.gameObject != null && scope.gameObject.name == "[Singleton] LoadingScene")
                    count++;
            }

            return count;
        }

        static LoadingScreenMB? FindSingletonLoadingScene()
        {
            LoadingScreenMB[] scopes = Resources.FindObjectsOfTypeAll<LoadingScreenMB>();
            for (int i = 0; i < scopes.Length; i++)
            {
                LoadingScreenMB scope = scopes[i];
                if (scope != null && scope.gameObject != null && scope.gameObject.name == "[Singleton] LoadingScene")
                    return scope;
            }

            return null;
        }

        sealed class TestLoadingScreenConfig : ILoadingScreenConfig
        {
            public TestLoadingScreenConfig(LoadingScreenMB loadingScenePrefab)
            {
                LoadingScenePrefab = loadingScenePrefab;
            }

            public float CommandLeadTimeBeforeSceneChangeSeconds => 0f;
            public LoadingScreenMB LoadingScenePrefab { get; }
            public CommandListData ShowCommands => new();
            public CommandListData HideCommands => new();
            public CommandListData ProgressCommands => new();
            public VarKeyRef MessageVar => default;
            public VarKeyRef ProgressVar => default;
        }

        static void SetNonPublicField(object target, string fieldName, object? value)
        {
            FieldInfo? field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} must remain available for deterministic tests.");
            field!.SetValue(target, value);
        }
    }
}
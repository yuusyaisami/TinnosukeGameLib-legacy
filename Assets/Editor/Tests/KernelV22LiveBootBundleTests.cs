#nullable enable
using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Game.Flow;
using Game.Kernel.Abstractions;
using Game.Kernel.Boot;
using Game.Kernel.Diagnostics;
using Game.Kernel.Generation;
using Game.Kernel.IR;
using Game.Kernel.Validation;
using Game.Project;
using Game.Project.Bootstrap;
using Game.Scene;
using NUnit.Framework;
using UnityEngine;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class KernelV22LiveBootBundleTests
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

                SetNonPublicField(asset, "projectRootScopePlanId", 220);
                SetNonPublicField(asset, "platformRootScopePlanId", 230);
                SetNonPublicField(asset, "globalRootScopePlanId", 240);

                KernelBootPublishedArtifactBundle bundle = asset.CreatePublishedArtifactBundle();
                BootRootValidationState rootState = bundle.CreateRootState();
                ReadOnlySpan<RuntimeIdentityRef> requiredRootScopes = rootState.RequiredRootScopes;

                Assert.That(requiredRootScopes.Length, Is.EqualTo(3));
                Assert.That(requiredRootScopes[0], Is.EqualTo(new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, 220)));
                Assert.That(requiredRootScopes[1], Is.EqualTo(new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, 230)));
                Assert.That(requiredRootScopes[2], Is.EqualTo(new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, 240)));
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
        public void LiveBootBundleAsset_InstantiatesGenericRootHosts_WithoutConcreteBootLayerTypes()
        {
            KernelLiveBootBundleAsset asset = ScriptableObject.CreateInstance<KernelLiveBootBundleAsset>();
            GameObject projectPrefab = new GameObject("KernelLiveBootProjectHostPrefab");
            GameObject globalPrefab = new GameObject("KernelLiveBootGlobalHostPrefab");
            GameObject? projectInstance = null;
            GameObject? globalInstance = null;
            try
            {
                projectPrefab.AddComponent<TestProjectRootScope>();
                GameObject platformChild = new GameObject("PlatformRoot");
                platformChild.transform.SetParent(projectPrefab.transform, false);
                platformChild.AddComponent<TestPlatformRootScope>();

                globalPrefab.AddComponent<TestGlobalRootScope>();

                SetNonPublicField(asset, "projectRootPrefab", projectPrefab);
                SetNonPublicField(asset, "globalRootPrefab", globalPrefab);
                SetNonPublicField(asset, "projectRootScopePlanId", 220);
                SetNonPublicField(asset, "platformRootScopePlanId", 230);
                SetNonPublicField(asset, "globalRootScopePlanId", 240);

                KernelLiveBootPersistentRootInstance projectRoot = asset.InstantiateProjectRootHost();
                projectInstance = projectRoot.RootGameObject;

                Assert.That(projectRoot.Role, Is.EqualTo(KernelLiveBootPersistentRootRole.Project));
                Assert.That(projectRoot.RootScope, Is.InstanceOf<global::Game.IScopeGraphHost>());
                Assert.That(
                    global::Game.ScopeIdentityMB.PredictKindFromComponent(projectRoot.RootScope.HostComponent, projectRoot.RootScope.Kind),
                    Is.EqualTo(global::Game.LifetimeScopeKind.Project));

                TestPlatformRootScope platformRoot = projectRoot.RootGameObject.GetComponentInChildren<TestPlatformRootScope>(true);
                Assert.That(platformRoot, Is.Not.Null);

                KernelLiveBootPersistentRootInstance globalRoot = asset.InstantiateGlobalRootHost(platformRoot.transform);
                globalInstance = globalRoot.RootGameObject;

                Assert.That(globalRoot.Role, Is.EqualTo(KernelLiveBootPersistentRootRole.Global));
                Assert.That(globalRoot.RootScope, Is.InstanceOf<global::Game.IScopeGraphHost>());
                Assert.That(
                    global::Game.ScopeIdentityMB.PredictKindFromComponent(globalRoot.RootScope.HostComponent, globalRoot.RootScope.Kind),
                    Is.EqualTo(global::Game.LifetimeScopeKind.Global));
                Assert.That(globalRoot.RootTransform.parent, Is.SameAs(platformRoot.transform));
            }
            finally
            {
                if (globalInstance != null)
                    UnityEngine.Object.DestroyImmediate(globalInstance);
                if (projectInstance != null)
                    UnityEngine.Object.DestroyImmediate(projectInstance);

                UnityEngine.Object.DestroyImmediate(asset);
                UnityEngine.Object.DestroyImmediate(globalPrefab);
                UnityEngine.Object.DestroyImmediate(projectPrefab);
            }
        }

        [Test]
        public void KernelLiveBootOrchestrator_ResolvesGenericPlatformRootHost()
        {
            GameObject projectObject = new GameObject("ProjectRootHost");
            GameObject platformObject = new GameObject("PlatformRootHost");
            try
            {
                TestProjectRootScope projectRoot = projectObject.AddComponent<TestProjectRootScope>();
                global::Game.ScopeIdentityMB projectIdentity = projectObject.GetComponent<global::Game.ScopeIdentityMB>();
                Assert.That(projectIdentity, Is.Not.Null);
                projectIdentity.kind = global::Game.LifetimeScopeKind.Project;

                platformObject.transform.SetParent(projectObject.transform, false);
                TestPlatformRootScope platformRoot = platformObject.AddComponent<TestPlatformRootScope>();
                global::Game.ScopeIdentityMB platformIdentity = platformObject.GetComponent<global::Game.ScopeIdentityMB>();
                Assert.That(platformIdentity, Is.Not.Null);
                platformIdentity.kind = global::Game.LifetimeScopeKind.Platform;

                global::Game.IScopeGraphHost resolvedPlatformRoot = InvokeResolvePlatformRootHost(projectRoot);

                Assert.That(resolvedPlatformRoot, Is.SameAs(platformRoot));
                Assert.That(
                    global::Game.ScopeIdentityMB.PredictKindFromComponent(resolvedPlatformRoot.HostComponent, resolvedPlatformRoot.Kind),
                    Is.EqualTo(global::Game.LifetimeScopeKind.Platform));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(platformObject);
                UnityEngine.Object.DestroyImmediate(projectObject);
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
                Assert.That(exception.Message, Does.Contain("scene handoff"));

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

        static global::Game.IScopeGraphHost InvokeResolvePlatformRootHost(global::Game.IScopeGraphHost projectRoot)
        {
            MethodInfo? resolveMethod = typeof(KernelLiveBootOrchestrator).GetMethod("ResolvePlatformRootHost", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(resolveMethod, Is.Not.Null, "KernelLiveBootOrchestrator.ResolvePlatformRootHost must remain available for deterministic tests.");

            object? result = resolveMethod!.Invoke(null, new object?[] { projectRoot });
            Assert.That(result, Is.InstanceOf<global::Game.IScopeGraphHost>());
            return (global::Game.IScopeGraphHost)result!;
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

        static void SetNonPublicField(object target, string fieldName, object? value)
        {
            FieldInfo? field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} must remain available for deterministic tests.");
            field!.SetValue(target, value);
        }

        sealed class TestProjectRootScope : global::Game.KernelScopeHost
        {
        }

        sealed class TestPlatformRootScope : global::Game.KernelScopeHost
        {
        }

        sealed class TestGlobalRootScope : global::Game.KernelScopeHost
        {
        }
    }
}


#nullable enable
using System;
using Cysharp.Threading.Tasks;
using Game;
using Game.DI;
using Game.Kernel.Abstractions;
using Game.Kernel.Boot;
using Game.Kernel.Diagnostics;
using Game.Kernel.Generation;
using Game.Kernel.IR;
using Game.Project.Bootstrap;
using Game.Kernel.Validation;
using NUnit.Framework;
using UnityEngine;
using KernelHash128 = Game.Kernel.IR.Hash128;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class KernelVerifiedCompositionRuntimeTests
    {
        [SetUp]
        public void SetUp()
        {
            KernelVerifiedCommandRuntime.Deactivate();
            KernelVerifiedCompositionRuntime.Deactivate();
        }

        [TearDown]
        public void TearDown()
        {
            KernelVerifiedCommandRuntime.Deactivate();
            KernelVerifiedCompositionRuntime.Deactivate();
        }

        [Test]
        public void Activate_ResolvesConfiguredRootHandles_AndBindsScopeHosts()
        {
            KernelBootPublishedArtifactBundle bundle = CreateBundleWithRootScopes(210, 220, 230);
            BootValidationInput input = bundle.CreateValidationInput(new BootFallbackValidationState(false, false, false, false, false, false));
            KernelBootBoundaryResult result = KernelBootBoundary.Execute(input);

            Assert.That(result.Status, Is.EqualTo(KernelBootBoundaryStatus.Ready));
            Assert.That(result, Is.InstanceOf<KernelBootBoundaryResult.Success>());

            KernelBootBoundaryResult.Success success = (KernelBootBoundaryResult.Success)result;
            KernelVerifiedCommandRuntime.Activate(success.RuntimeSurface);
            KernelVerifiedCompositionRuntime.Activate(success.RuntimeSurface);

            GameObject projectObject = new GameObject("verified-project-root");
            GameObject platformObject = new GameObject("verified-platform-root");
            try
            {
                TestScope projectScope = projectObject.AddComponent<TestScope>();
                TestScope platformScope = platformObject.AddComponent<TestScope>();

                Assert.That(KernelVerifiedCompositionRuntime.TryResolveRootScopeHandle(new ScopePlanId(210), out ScopeHandle projectHandle), Is.True);
                Assert.That(KernelVerifiedCompositionRuntime.TryResolveRootScopeHandle(new ScopePlanId(220), out ScopeHandle platformHandle), Is.True);

                Assert.That(KernelVerifiedCompositionRuntime.TryBindRootScope(projectScope, new ScopePlanId(210)), Is.True);
                Assert.That(KernelVerifiedCompositionRuntime.TryBindRootScope(platformScope, new ScopePlanId(220), projectScope), Is.True);

                Assert.That(KernelVerifiedCompositionRuntime.TryGetBoundScopeHandle(projectScope, out ScopeHandle boundProjectHandle), Is.True);
                Assert.That(boundProjectHandle, Is.EqualTo(projectHandle));
                Assert.That(KernelVerifiedCompositionRuntime.TryGetBoundScopeHandle(platformScope, out ScopeHandle boundPlatformHandle), Is.True);
                Assert.That(boundPlatformHandle, Is.EqualTo(platformHandle));
                Assert.That(platformScope.Parent, Is.SameAs(projectScope));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(platformObject);
                UnityEngine.Object.DestroyImmediate(projectObject);
            }
        }

        [Test]
        public void RuntimeTemplateBinding_CreatesChildScopeHandle_AndReleaseRemovesIt()
        {
            KernelBootPublishedArtifactBundle bundle = CreateBundleWithRootAndChildScope(210, 220);
            BootValidationInput input = bundle.CreateValidationInput(new BootFallbackValidationState(false, false, false, false, false, false));
            KernelBootBoundaryResult result = KernelBootBoundary.Execute(input);

            Assert.That(result, Is.InstanceOf<KernelBootBoundaryResult.Success>());

            KernelBootBoundaryResult.Success success = (KernelBootBoundaryResult.Success)result;
            KernelVerifiedCommandRuntime.Activate(success.RuntimeSurface);
            KernelVerifiedCompositionRuntime.Activate(success.RuntimeSurface);

            GameObject parentObject = new GameObject("verified-parent-root");
            GameObject childObject = new GameObject("verified-runtime-child");
            TestRuntimeTemplate template = ScriptableObject.CreateInstance<TestRuntimeTemplate>();
            template.Initialize(220);
            try
            {
                TestScope parentScope = parentObject.AddComponent<TestScope>();
                TestScope childScope = childObject.AddComponent<TestScope>();

                Assert.That(KernelVerifiedCompositionRuntime.TryBindRootScope(parentScope, new ScopePlanId(210)), Is.True);
                Assert.That(VerifiedCompositionRuntime.TryBindRuntimeScope(template, childScope, parentScope), Is.True);
                Assert.That(KernelVerifiedCompositionRuntime.TryGetBoundScopeHandle(childScope, out ScopeHandle childHandle), Is.True);

                Assert.That(KernelVerifiedCompositionRuntime.TryGetRuntimeSurface(out KernelBootRuntimeSurface? runtimeSurface), Is.True);
                Assert.That(runtimeSurface, Is.Not.Null);
                Assert.That(runtimeSurface!.Runtime.RootScopeGraph.TryGetScope(childHandle, out ScopeRuntimeSnapshot snapshot), Is.True);
                Assert.That(snapshot.PlanId, Is.EqualTo(new ScopePlanId(220)));

                VerifiedCompositionRuntime.ReleaseRuntimeScope(childScope);

                Assert.That(KernelVerifiedCompositionRuntime.TryGetBoundScopeHandle(childScope, out _), Is.False);
                Assert.That(runtimeSurface.Runtime.RootScopeGraph.TryGetScope(childHandle, out _), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(template);
                UnityEngine.Object.DestroyImmediate(childObject);
                UnityEngine.Object.DestroyImmediate(parentObject);
            }
        }

        [Test]
        public void BoundRootScope_SyncsScopeGraphState_UnityLink_AndDestroyLifecycle()
        {
            KernelBootPublishedArtifactBundle bundle = CreateBundleWithRootScopes(210, 220, 230);
            BootValidationInput input = bundle.CreateValidationInput(new BootFallbackValidationState(false, false, false, false, false, false));
            KernelBootBoundaryResult result = KernelBootBoundary.Execute(input);

            Assert.That(result, Is.InstanceOf<KernelBootBoundaryResult.Success>());

            KernelBootBoundaryResult.Success success = (KernelBootBoundaryResult.Success)result;
            KernelVerifiedCommandRuntime.Activate(success.RuntimeSurface);
            KernelVerifiedCompositionRuntime.Activate(success.RuntimeSurface);

            Assert.That(KernelVerifiedCompositionRuntime.TryGetRuntimeSurface(out KernelBootRuntimeSurface? runtimeSurface), Is.True);
            Assert.That(runtimeSurface, Is.Not.Null);

            GameObject? projectObject = new GameObject("verified-project-root");
            try
            {
                TestScope projectScope = projectObject.AddComponent<TestScope>();

                Assert.That(KernelVerifiedCompositionRuntime.TryBindRootScope(projectScope, new ScopePlanId(210)), Is.True);
                Assert.That(KernelVerifiedCompositionRuntime.TryGetBoundScopeHandle(projectScope, out ScopeHandle handle), Is.True);

                projectScope.EnsureScopeBuilt();

                Assert.That(runtimeSurface!.Runtime.RootScopeGraph.TryGetScope(handle, out ScopeRuntimeSnapshot activeSnapshot), Is.True);
                Assert.That(activeSnapshot.State, Is.EqualTo(ScopeRuntimeState.Active));
                Assert.That(activeSnapshot.UnityLink.Kind, Is.EqualTo(UnityObjectLinkKind.Runtime));
                Assert.That(activeSnapshot.UnityLink.RuntimeInstanceId, Is.GreaterThan(0));
                Assert.That(activeSnapshot.UnityLink.DebugName, Is.EqualTo(projectObject.name));

                projectScope.ReleaseIfNeeded();

                Assert.That(runtimeSurface.Runtime.RootScopeGraph.TryGetScope(handle, out ScopeRuntimeSnapshot inactiveSnapshot), Is.True);
                Assert.That(inactiveSnapshot.State, Is.EqualTo(ScopeRuntimeState.Inactive));

                projectScope.AcquireIfNeeded();

                Assert.That(runtimeSurface.Runtime.RootScopeGraph.TryGetScope(handle, out ScopeRuntimeSnapshot reacquiredSnapshot), Is.True);
                Assert.That(reacquiredSnapshot.State, Is.EqualTo(ScopeRuntimeState.Active));

                UnityEngine.Object.DestroyImmediate(projectObject);
                projectObject = null;

                Assert.That(runtimeSurface.Runtime.RootScopeGraph.TryGetScope(handle, out _), Is.False);
            }
            finally
            {
                if (projectObject != null)
                    UnityEngine.Object.DestroyImmediate(projectObject);
            }
        }

        [Test]
        public void RuntimeLifetimeScopePool_FailsClosed_WhenVerifiedBindingIsMissing()
        {
            KernelBootPublishedArtifactBundle bundle = CreateBundleWithRootAndChildScope(210, 220);
            BootValidationInput input = bundle.CreateValidationInput(new BootFallbackValidationState(false, false, false, false, false, false));
            KernelBootBoundaryResult result = KernelBootBoundary.Execute(input);

            Assert.That(result, Is.InstanceOf<KernelBootBoundaryResult.Success>());

            KernelBootBoundaryResult.Success success = (KernelBootBoundaryResult.Success)result;
            KernelVerifiedCommandRuntime.Activate(success.RuntimeSurface);
            KernelVerifiedCompositionRuntime.Activate(success.RuntimeSurface);

            GameObject? parentObject = new GameObject("verified-parent-root");
            GameObject? poolRootObject = new GameObject("verified-pool-root");
            GameObject? prefab = new GameObject("verified-runtime-prefab");
            RuntimeLifetimeScopePool? pool = null;
            TestRuntimeTemplate template = ScriptableObject.CreateInstance<TestRuntimeTemplate>();
            try
            {
                TestScope parentScope = parentObject.AddComponent<TestScope>();
                prefab.AddComponent<KernelScopeHost>();

                Assert.That(KernelVerifiedCompositionRuntime.TryBindRootScope(parentScope, new ScopePlanId(210)), Is.True);

                template.Initialize(planId: 0, prefab);
                pool = new RuntimeLifetimeScopePool(parentScope, poolRootObject.transform);

                Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await pool.AcquireAsync(template, poolRootObject.transform, Vector3.zero, Quaternion.identity).AsTask());
            }
            finally
            {
                pool?.Dispose();
                UnityEngine.Object.DestroyImmediate(template);

                if (prefab != null)
                    UnityEngine.Object.DestroyImmediate(prefab);
                if (poolRootObject != null)
                    UnityEngine.Object.DestroyImmediate(poolRootObject);
                if (parentObject != null)
                    UnityEngine.Object.DestroyImmediate(parentObject);
            }
        }

        [DisallowMultipleComponent]
        sealed class TestScope : KernelScopeHost
        {
            protected override bool UseBuildCoordinator => false;
            protected override bool AutoBuildOnAwake => false;

            protected override void ConfigureBase(IRuntimeContainerBuilder builder)
            {
                _ = builder;
            }
        }

        sealed class TestRuntimeTemplate : BaseRuntimeTemplateSO
        {
            int verifiedScopePlanId;
            GameObject? prefab;

            protected override bool UsesBasePreset => false;

            public void Initialize(int planId, GameObject? runtimePrefab = null)
            {
                verifiedScopePlanId = planId;
                prefab = runtimePrefab;
            }

            public override GameObject Prefab => prefab ?? throw new InvalidOperationException("TestRuntimeTemplate does not provide a prefab.");

            public override int VerifiedScopePlanId => verifiedScopePlanId;
        }

        static KernelBootPublishedArtifactBundle CreateBundleWithRootScopes(int projectPlanId, int platformPlanId, int globalPlanId)
        {
            KernelProfile profile = new KernelProfile(new KernelProfileId(41001), KernelProfileKind.Development);

            ScopeIR[] scopes = new[]
            {
                CreateRootScope(projectPlanId, 1, 31),
                CreateRootScope(platformPlanId, 2, 32),
                CreateRootScope(globalPlanId, 3, 33),
            };

            ServiceIR[] services = Array.Empty<ServiceIR>();
            KernelDebugMapEntry[] debugEntries = Array.Empty<KernelDebugMapEntry>();

            KernelHash128 sourceHash = VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[]
            {
                "KernelVerifiedCompositionRuntimeTests",
                "PlanId:41001",
            });

            KernelHash128 registryHash = VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[]
            {
                "KernelVerifiedCompositionRuntimeTests",
                "Registry:41001",
            });

            KernelHash128 profileHash = VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[]
            {
                "KernelVerifiedCompositionRuntimeTests",
                "Profile:" + profile.Kind,
            });

            KernelHash128 scopeHash = KernelProjectionHashing.ComputeScopeGraphHash(scopes);
            KernelHash128 serviceHash = KernelProjectionHashing.ComputeServiceGraphHash(services);
            KernelHash128 debugMapHash = KernelProjectionHashing.ComputeDebugMapHash(debugEntries);

            ServiceGraphPlan serviceGraphPlan = new ServiceGraphPlan(
                CreateHeader(new ArtifactId(1), ArtifactKind.ServiceGraph, sourceHash, registryHash, profileHash, debugMapHash, serviceHash),
                services);

            ScopeGraphPlan scopeGraphPlan = new ScopeGraphPlan(
                CreateHeader(new ArtifactId(2), ArtifactKind.ScopeGraph, sourceHash, registryHash, profileHash, debugMapHash, scopeHash),
                scopes);

            CommandCatalogPlan commandCatalogPlan = new CommandCatalogPlan(
                CreateHeader(new ArtifactId(4), ArtifactKind.CommandCatalog, sourceHash, registryHash, profileHash, debugMapHash, ComputeHash("empty-command-catalog-41001")),
                Array.Empty<CommandIR>());

            ValueSchemaPlan valueSchemaPlan = new ValueSchemaPlan(
                CreateHeader(new ArtifactId(5), ArtifactKind.ValueSchema, sourceHash, registryHash, profileHash, debugMapHash, ComputeHash("empty-value-schema-41001")),
                Array.Empty<ValueKeyIR>());

            RuntimeQueryPlan runtimeQueryPlan = new RuntimeQueryPlan(
                CreateHeader(new ArtifactId(6), ArtifactKind.RuntimeQuery, sourceHash, registryHash, profileHash, debugMapHash, ComputeHash("empty-runtime-query-41001")),
                Array.Empty<RuntimeQueryIR>());

            KernelDebugMap debugMap = new KernelDebugMap(
                CreateHeader(new ArtifactId(7), ArtifactKind.KernelDebugMap, sourceHash, registryHash, profileHash, debugMapHash, debugMapHash),
                debugEntries);

            VerifiedArtifactSetRef artifactSet = new VerifiedArtifactSetRef(
                new ArtifactSetId(41001),
                new PlanId(41001),
                sourceHash.ToString(),
                profileHash.ToString(),
                1,
                registryHash.ToString(),
                debugMapHash.ToString());

            KernelBootManifest manifest = new KernelBootManifest(
                new ManifestId(41001),
                profile.Id,
                artifactSet,
                new BootPolicyId(41001),
                BootDiagnosticsPolicy.ForKind(profile.Kind));

            return new KernelBootPublishedArtifactBundle(
                manifest,
                profile,
                serviceGraphPlan,
                scopeGraphPlan,
                lifecyclePlan: null,
                debugMap,
                commandCatalogPlan: commandCatalogPlan,
                valueSchemaPlan: valueSchemaPlan,
                runtimeQueryPlan: runtimeQueryPlan,
                availableRootScopes: new[]
                {
                    ScopeIdentity(projectPlanId),
                    ScopeIdentity(platformPlanId),
                    ScopeIdentity(globalPlanId),
                });
        }

        static KernelBootPublishedArtifactBundle CreateBundleWithRootAndChildScope(int rootPlanId, int childPlanId)
        {
            KernelProfile profile = new KernelProfile(new KernelProfileId(41002), KernelProfileKind.Development);

            ScopeIR[] scopes = new[]
            {
                CreateRootScope(rootPlanId, 1, 51),
                CreateChildScope(childPlanId, 2, new ScopeAuthoringId(1), 52),
            };

            ServiceIR[] services = Array.Empty<ServiceIR>();
            KernelDebugMapEntry[] debugEntries = Array.Empty<KernelDebugMapEntry>();

            KernelHash128 sourceHash = VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[]
            {
                "KernelVerifiedCompositionRuntimeTests",
                "PlanId:41002",
            });

            KernelHash128 registryHash = VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[]
            {
                "KernelVerifiedCompositionRuntimeTests",
                "Registry:41002",
            });

            KernelHash128 profileHash = VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[]
            {
                "KernelVerifiedCompositionRuntimeTests",
                "Profile:" + profile.Kind,
            });

            KernelHash128 scopeHash = KernelProjectionHashing.ComputeScopeGraphHash(scopes);
            KernelHash128 serviceHash = KernelProjectionHashing.ComputeServiceGraphHash(services);
            KernelHash128 debugMapHash = KernelProjectionHashing.ComputeDebugMapHash(debugEntries);

            ServiceGraphPlan serviceGraphPlan = new ServiceGraphPlan(
                CreateHeader(new ArtifactId(11), ArtifactKind.ServiceGraph, sourceHash, registryHash, profileHash, debugMapHash, serviceHash),
                services);

            ScopeGraphPlan scopeGraphPlan = new ScopeGraphPlan(
                CreateHeader(new ArtifactId(12), ArtifactKind.ScopeGraph, sourceHash, registryHash, profileHash, debugMapHash, scopeHash),
                scopes);

            CommandCatalogPlan commandCatalogPlan = new CommandCatalogPlan(
                CreateHeader(new ArtifactId(14), ArtifactKind.CommandCatalog, sourceHash, registryHash, profileHash, debugMapHash, ComputeHash("empty-command-catalog-41002")),
                Array.Empty<CommandIR>());

            ValueSchemaPlan valueSchemaPlan = new ValueSchemaPlan(
                CreateHeader(new ArtifactId(15), ArtifactKind.ValueSchema, sourceHash, registryHash, profileHash, debugMapHash, ComputeHash("empty-value-schema-41002")),
                Array.Empty<ValueKeyIR>());

            RuntimeQueryPlan runtimeQueryPlan = new RuntimeQueryPlan(
                CreateHeader(new ArtifactId(16), ArtifactKind.RuntimeQuery, sourceHash, registryHash, profileHash, debugMapHash, ComputeHash("empty-runtime-query-41002")),
                Array.Empty<RuntimeQueryIR>());

            KernelDebugMap debugMap = new KernelDebugMap(
                CreateHeader(new ArtifactId(17), ArtifactKind.KernelDebugMap, sourceHash, registryHash, profileHash, debugMapHash, debugMapHash),
                debugEntries);

            VerifiedArtifactSetRef artifactSet = new VerifiedArtifactSetRef(
                new ArtifactSetId(41002),
                new PlanId(41002),
                sourceHash.ToString(),
                profileHash.ToString(),
                1,
                registryHash.ToString(),
                debugMapHash.ToString());

            KernelBootManifest manifest = new KernelBootManifest(
                new ManifestId(41002),
                profile.Id,
                artifactSet,
                new BootPolicyId(41002),
                BootDiagnosticsPolicy.ForKind(profile.Kind));

            return new KernelBootPublishedArtifactBundle(
                manifest,
                profile,
                serviceGraphPlan,
                scopeGraphPlan,
                lifecyclePlan: null,
                debugMap,
                commandCatalogPlan: commandCatalogPlan,
                valueSchemaPlan: valueSchemaPlan,
                runtimeQueryPlan: runtimeQueryPlan,
                availableRootScopes: new[]
                {
                    ScopeIdentity(rootPlanId),
                });
        }

        static VerifiedArtifactHeader CreateHeader(
            ArtifactId artifactId,
            ArtifactKind artifactKind,
            KernelHash128 sourceHash,
            KernelHash128 registryHash,
            KernelHash128 profileHash,
            KernelHash128 debugMapHash,
            KernelHash128 contentHash)
        {
            return new VerifiedArtifactHeader(
                new PlanId(41001),
                new ArtifactSetId(41001),
                artifactId,
                artifactKind,
                1,
                sourceHash,
                registryHash,
                profileHash,
                debugMapHash,
                contentHash,
                "KernelVerifiedCompositionRuntimeTests");
        }

        static KernelHash128 ComputeHash(string value)
        {
            return VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[] { value });
        }

        static ScopeIR CreateRootScope(int planId, int authoringId, int sourceId)
        {
            return new ScopeIR(
                new ScopeAuthoringId(authoringId),
                new ScopePlanId(planId),
                "Scope" + planId,
                ScopeKind.Root,
                new ModuleId(10),
                default,
                Array.Empty<ScopeServiceRequirementIR>(),
                Array.Empty<ScopeValueInitRefIR>(),
                new ScopeServiceBoundaryIR(ScopeServiceBoundaryKind.Detached, 0, new SourceLocationId(sourceId)),
                new LifecyclePlanRefIR(new LifecyclePlanId(sourceId + 100), new SourceLocationId(sourceId + 200)),
                new SourceLocationId(sourceId));
        }

        static ScopeIR CreateChildScope(int planId, int authoringId, ScopeAuthoringId parentAuthoringId, int sourceId)
        {
            return new ScopeIR(
            new ScopeAuthoringId(authoringId),
            new ScopePlanId(planId),
            "Scope" + planId,
            ScopeKind.Child,
            new ModuleId(10),
            parentAuthoringId,
            Array.Empty<ScopeServiceRequirementIR>(),
            Array.Empty<ScopeValueInitRefIR>(),
            new ScopeServiceBoundaryIR(ScopeServiceBoundaryKind.ReferencesParent, 0, new SourceLocationId(sourceId)),
            new LifecyclePlanRefIR(new LifecyclePlanId(sourceId + 100), new SourceLocationId(sourceId + 200)),
            new SourceLocationId(sourceId));
        }

        static RuntimeIdentityRef ScopeIdentity(int value)
        {
            return new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, value);
        }
    }
}


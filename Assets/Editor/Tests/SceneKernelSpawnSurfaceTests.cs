using Game.Kernel.Abstractions;
using Game.Common;
using Game.Kernel.Authoring;
using Game.Kernel.Layers;
using Game.Kernel.Layers.Composition;
using Game.Kernel.Layers.Unity;
using Game.Spawn;
using NUnit.Framework;
using UnityEngine;
using System.Threading;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class SceneKernelSpawnSurfaceTests
    {
        [Test]
        public void SceneKernelSpawnRouteDeclaration_UsesRuntimeKindAndNormalizedTagForKernelIds()
        {
            SceneKernelSpawnRouteDeclaration route = new SceneKernelSpawnRouteDeclaration(
                SpawnerKind.RuntimeUIElement,
                " default ");

            Assert.That(route.TryValidate(out string failureReason), Is.True);
            Assert.That(failureReason, Is.Empty);
            Assert.That(route.Tag, Is.EqualTo(string.Empty));
            Assert.That(route.KernelRouteId, Is.EqualTo(SceneKernelSpawnRouteId.FromParts(SpawnerKind.RuntimeUIElement.ToString(), string.Empty)));
            Assert.That(route.PoolId, Is.EqualTo(SceneKernelSpawnPoolId.FromParts(SpawnerKind.RuntimeUIElement.ToString(), string.Empty)));
            Assert.That(route.StableRouteKey, Is.EqualTo(SpawnerKind.RuntimeUIElement.ToString()));
        }

        [Test]
        public void SceneKernelSpawnWarmupDeclaration_RejectsLegacyKinds()
        {
            SceneKernelSpawnWarmupDeclaration warmup = new SceneKernelSpawnWarmupDeclaration(
                SpawnerKind.Entity,
                "legacy",
                2);

            Assert.That(warmup.TryValidate(out string failureReason), Is.False);
            Assert.That(failureReason, Does.Contain("RuntimeEntity"));
        }

        [Test]
        public void SceneKernelSpawnHostMB_WarmsUpDeclaredEntriesIntoParkingRoot()
        {
            GameObject root = new GameObject("SpawnHost");
            GameObject parkingRoot = new GameObject("ParkingRoot");
            GameObject prefab = new GameObject("WarmupPrefab");
            try
            {
                root.AddComponent<SceneKernelHostMB>();

                SceneKernelSpawnDeclarationMB declaration = root.AddComponent<SceneKernelSpawnDeclarationMB>();
                declaration.SetRoutesForEditor(
                    new SceneKernelSpawnRouteDeclaration(SpawnerKind.RuntimeEntity, "warmup", null, parkingRoot.transform));

                BaseRuntimeTemplatePreset preset = new TestRuntimeTemplatePreset(prefab);
                declaration.SetWarmupsForEditor(
                    new SceneKernelSpawnWarmupDeclaration(
                        SpawnerKind.RuntimeEntity,
                        "warmup",
                        DynamicValue<BaseRuntimeTemplatePreset>.FromSource(new ManagedRefLiteralSource<BaseRuntimeTemplatePreset>(preset)),
                        1));

                SceneKernelSpawnHostMB host = root.AddComponent<SceneKernelSpawnHostMB>();

                Assert.That(host.TryGetSpawnBoundary(out ISceneKernelSpawnBoundary? boundary), Is.True);
                Assert.That(boundary, Is.Not.Null);
                Assert.That(boundary!.TryResolveSpawnPool(SceneKernelSpawnRouteId.FromParts(SpawnerKind.RuntimeEntity.ToString(), "warmup"), out ISceneKernelPrefabPool? typedPool), Is.True);
                Assert.That(typedPool, Is.Not.Null);

                Assert.That(parkingRoot.transform.childCount, Is.EqualTo(1));
                GameObject warmedRoot = parkingRoot.transform.GetChild(0).gameObject;
                Assert.That(warmedRoot.activeSelf, Is.False);

                SceneKernelEntityInstanceMB? anchor = warmedRoot.GetComponent<SceneKernelEntityInstanceMB>();
                Assert.That(anchor, Is.Not.Null);
                Assert.That(anchor!.HasLease, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(parkingRoot);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public async System.Threading.Tasks.Task SceneKernelSpawnBoundary_SpawnsReleasesAndBulkReleasesTypedPool()
        {
            SceneKernel kernel = new SceneKernel(new SceneKernelHandle(304), "SpawnBoundary");
            kernel.Initialize();

            SceneKernelComposition composition = SceneKernelComposition.CreatePending();
            kernel.AttachComposition(composition);

            GameObject parkingRoot = new GameObject("ParkingRoot");
            GameObject prefab = new GameObject("SpawnPrefab");
            try
            {
                SceneKernelSpawnRouteDeclaration route = new SceneKernelSpawnRouteDeclaration(
                    SpawnerKind.RuntimeEntity,
                    "typed",
                    null,
                    parkingRoot.transform);

                Assert.That(kernel.TryGetSpawnBoundary(out ISceneKernelSpawnBoundary? boundary), Is.True);
                Assert.That(boundary, Is.Not.Null);
                Assert.That(boundary!.TryBindSpawnRoute(route.KernelRouteId, route.PoolId), Is.True);

                BaseRuntimeTemplatePreset preset = new TestRuntimeTemplatePreset(prefab);
                BaseRuntimeTemplateSO runtimeTemplate = RuntimeTemplatePresetResolver.ResolveTemplateSO(preset)!;

                SceneKernelSpawnRequest request = new SceneKernelSpawnRequest(
                    route.KernelRouteId,
                    runtimeTemplate,
                    Vector3.zero,
                    Quaternion.identity,
                    Vector3.one,
                    parent: null,
                    parkingRoot: parkingRoot.transform);

                SceneKernelSpawnResult spawnResult = await boundary.SpawnAsync(request, CancellationToken.None);
                Assert.That(spawnResult.Succeeded, Is.True);
                Assert.That(spawnResult.SpawnedRoot, Is.Not.Null);
                Assert.That(spawnResult.SpawnedRoot!.activeSelf, Is.True);
                Assert.That(boundary.ActiveLeaseCount, Is.EqualTo(1));

                SceneKernelEntityInstanceMB? anchor = spawnResult.SpawnedRoot.GetComponent<SceneKernelEntityInstanceMB>();
                Assert.That(anchor, Is.Not.Null);
                Assert.That(anchor!.HasLease, Is.True);

                SceneKernelReleaseResult releaseResult = boundary.Release(spawnResult.Lease, SceneKernelReleaseReason.Despawn);
                Assert.That(releaseResult.Succeeded, Is.True);
                Assert.That(releaseResult.ReleasedCount, Is.EqualTo(1));
                Assert.That(parkingRoot.transform.childCount, Is.EqualTo(1));
                Assert.That(parkingRoot.transform.GetChild(0).gameObject.activeSelf, Is.False);

                SceneKernelBulkReleaseResult bulkResult = boundary.ReleaseAll(
                    new SceneKernelBulkReleaseQuery(route.KernelRouteId, reason: SceneKernelReleaseReason.BulkRelease, includeInactive: true));
                Assert.That(bulkResult.Succeeded, Is.True);
                Assert.That(bulkResult.ReleasedCount, Is.EqualTo(1));
                Assert.That(parkingRoot.transform.childCount, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(parkingRoot);
            }
        }

        [Test]
        public void SceneKernelSpawnDeclarationMB_BindsDeclaredRoutesIntoOperationalBoundary()
        {
            SceneKernel kernel = new SceneKernel(new SceneKernelHandle(302), "SpawnScene");
            kernel.Initialize();

            SceneKernelComposition composition = SceneKernelComposition.CreatePending();
            kernel.AttachComposition(composition);

            GameObject root = new GameObject("SpawnDeclaration");
            try
            {
                SceneKernelSpawnDeclarationMB declaration = root.AddComponent<SceneKernelSpawnDeclarationMB>();
                declaration.SetRoutesForEditor(
                    new SceneKernelSpawnRouteDeclaration(SpawnerKind.RuntimeEntity, string.Empty),
                    new SceneKernelSpawnRouteDeclaration(SpawnerKind.RuntimeUIElement, "hud"));

                Assert.That(kernel.TryGetSpawnBoundary(out ISceneKernelSpawnBoundary? boundary), Is.True);
                Assert.That(boundary, Is.Not.Null);

                TestSceneKernelSpawnPool runtimeEntityPool = new TestSceneKernelSpawnPool(
                    SceneKernelSpawnPoolId.FromParts(SpawnerKind.RuntimeEntity.ToString(), string.Empty));
                TestSceneKernelSpawnPool runtimeUiPool = new TestSceneKernelSpawnPool(
                    SceneKernelSpawnPoolId.FromParts(SpawnerKind.RuntimeUIElement.ToString(), "hud"));

                Assert.That(boundary!.TryBindSpawnPool(runtimeEntityPool), Is.True);
                Assert.That(boundary.TryBindSpawnPool(runtimeUiPool), Is.True);
                Assert.That(declaration.TryBindDeclaredRoutes(boundary, out string failureReason), Is.True);
                Assert.That(failureReason, Is.Empty);

                Assert.That(boundary.TryResolveSpawnPool(SceneKernelSpawnRouteId.FromParts(SpawnerKind.RuntimeEntity.ToString(), string.Empty), out ISceneKernelSpawnPool? resolvedEntityPool), Is.True);
                Assert.That(resolvedEntityPool, Is.SameAs(runtimeEntityPool));
                Assert.That(boundary.TryResolveSpawnPool(SceneKernelSpawnRouteId.FromParts(SpawnerKind.RuntimeUIElement.ToString(), "hud"), out ISceneKernelSpawnPool? resolvedUiPool), Is.True);
                Assert.That(resolvedUiPool, Is.SameAs(runtimeUiPool));
            }
            finally
            {
                Object.DestroyImmediate(root);
                kernel.DetachComposition(composition);
            }
        }

        [Test]
        public void KernelComponentPlacementCatalog_SceneIncludesNewSpawnSurfaceDescriptors()
        {
            Assert.That(ContainsScenePlacement(KernelMappedComponentKind.SceneSpawnHost, "Game.Kernel.Layers.Unity.SceneKernelSpawnHostMB"), Is.True);
            Assert.That(ContainsScenePlacement(KernelMappedComponentKind.SceneSpawnDeclaration, "Game.Kernel.Authoring.SceneKernelSpawnDeclarationMB"), Is.True);
            Assert.That(ContainsScenePlacement(KernelMappedComponentKind.SceneEntityInstanceAnchor, "Game.Kernel.Layers.Unity.SceneKernelEntityInstanceMB"), Is.True);
        }

        [Test]
        public void SceneKernelEntityInstanceMB_BindsAndClearsLease()
        {
            GameObject root = new GameObject("SpawnedInstance");
            try
            {
                SceneKernelEntityInstanceMB instance = root.AddComponent<SceneKernelEntityInstanceMB>();
                SceneKernelEntityLeaseHandle lease = new SceneKernelEntityLeaseHandle(
                    new SceneKernelHandle(301),
                    new EntityRef("runtime.spawned.001"),
                    11,
                    3);
                SceneKernelSpawnRouteId routeId = SceneKernelSpawnRouteId.FromParts(SpawnerKind.RuntimeEntity.ToString(), "main");

                Assert.That(instance.HasLease, Is.False);
                Assert.That(instance.TryGetLease(out _), Is.False);

                instance.BindLease(lease, routeId);

                Assert.That(instance.HasLease, Is.True);
                Assert.That(instance.TryGetLease(out SceneKernelEntityLeaseHandle resolvedLease), Is.True);
                Assert.That(resolvedLease, Is.EqualTo(lease));
                Assert.That(instance.TryGetRouteId(out SceneKernelSpawnRouteId resolvedRouteId), Is.True);
                Assert.That(resolvedRouteId, Is.EqualTo(routeId));
                Assert.That(instance.CurrentRouteKey, Is.EqualTo(routeId.Value));

                instance.ClearLease();

                Assert.That(instance.HasLease, Is.False);
                Assert.That(instance.TryGetLease(out _), Is.False);
                Assert.That(instance.TryGetRouteId(out _), Is.False);
                Assert.That(instance.CurrentRouteKey, Is.EqualTo(string.Empty));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static bool ContainsScenePlacement(KernelMappedComponentKind kind, string sourceTypeName)
        {
            for (int index = 0; index < KernelComponentPlacementCatalog.Scene.Count; index++)
            {
                KernelComponentPlacementDescriptor descriptor = KernelComponentPlacementCatalog.Scene[index];
                if (descriptor.ComponentKind == kind && descriptor.SourceTypeName == sourceTypeName)
                    return true;
            }

            return false;
        }

        sealed class TestRuntimeTemplatePreset : BaseRuntimeTemplatePreset
        {
            readonly GameObject prefab;

            readonly string templateId;

            public TestRuntimeTemplatePreset(GameObject prefab)
            {
                this.prefab = prefab;
                templateId = prefab.name + "_Template";
            }

            public override GameObject Prefab => prefab;

            public override bool UsePooling => true;

            public override string TemplateId => templateId;
        }

        sealed class TestSceneKernelSpawnPool : ISceneKernelSpawnPool
        {
            public TestSceneKernelSpawnPool(SceneKernelSpawnPoolId poolId)
            {
                PoolId = poolId;
            }

            public SceneKernelSpawnPoolId PoolId { get; }

            public int ReleaseAll(object filter)
            {
                return 0;
            }
        }
    }
}
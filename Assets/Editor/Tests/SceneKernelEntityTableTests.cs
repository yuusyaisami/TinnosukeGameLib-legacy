using System;
using Game.Kernel.Abstractions;
using Game.Kernel.Boot;
using Game.Kernel.Diagnostics;
using Game.Kernel.Generation;
using Game.Kernel.IR;
using Game.Kernel.Layers;
using Game.Kernel.Layers.Composition;
using Game.Kernel.Value;
using Game.Project.Scene.Runtime;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class SceneKernelEntityTableTests
    {
        [Test]
        public void AttachComposition_ExposesSceneKernelSpawnBoundary()
        {
            SceneKernel kernel = new SceneKernel(new SceneKernelHandle(1_500), "Battle");
            kernel.Initialize();

            TestSceneKernelComposition composition = new TestSceneKernelComposition(
                CreateEntityRegistrationPlan(CreateEntry("entity.spawned", 1501)));

            kernel.AttachComposition(composition);

            Assert.That(kernel.TryGetSpawnBoundary(out ISceneKernelSpawnBoundary? resolvedBoundary), Is.True);
            Assert.That(resolvedBoundary, Is.Not.Null);
            Assert.That(resolvedBoundary, Is.SameAs(composition.SpawnBoundary));
            Assert.That(resolvedBoundary!.IsOperational, Is.True);

            EntityRef entityRef = new EntityRef("entity.spawned");
            Assert.That(resolvedBoundary.TryAcquireLease(entityRef, out SceneKernelEntityLeaseHandle lease), Is.True);
            Assert.That(lease.LeaseId, Is.EqualTo(1));
            Assert.That(lease.Generation, Is.EqualTo(1));
            Assert.That(resolvedBoundary.ValidateLease(lease), Is.True);
            Assert.That(resolvedBoundary.ActiveLeaseCount, Is.EqualTo(1));
            Assert.That(resolvedBoundary.TryReleaseLease(lease), Is.True);
            Assert.That(resolvedBoundary.ActiveLeaseCount, Is.EqualTo(0));
            Assert.That(resolvedBoundary.ValidateLease(lease), Is.False);
        }

        [Test]
        public void DetachComposition_ClosesAndClearsSceneKernelSpawnBoundary()
        {
            SceneKernel kernel = new SceneKernel(new SceneKernelHandle(1_501), "Battle");
            kernel.Initialize();

            TestSceneKernelComposition composition = new TestSceneKernelComposition(
                CreateEntityRegistrationPlan(CreateEntry("entity.spawned", 1502)));

            kernel.AttachComposition(composition);

            Assert.That(kernel.TryGetSpawnBoundary(out ISceneKernelSpawnBoundary? resolvedBoundary), Is.True);
            Assert.That(resolvedBoundary, Is.Not.Null);
            Assert.That(resolvedBoundary.IsOperational, Is.True);

            kernel.DetachComposition(composition);

            Assert.That(resolvedBoundary.IsOperational, Is.False);
            Assert.That(resolvedBoundary.ActiveLeaseCount, Is.EqualTo(0));
            Assert.That(composition.SpawnBoundary, Is.Null);
            Assert.That(kernel.TryGetSpawnBoundary(out _), Is.False);
        }

        [Test]
        public void AttachComposition_SpawnBoundaryResolvesBoundRouteToPool()
        {
            SceneKernel kernel = new SceneKernel(new SceneKernelHandle(1_502), "Battle");
            kernel.Initialize();

            TestSceneKernelComposition composition = new TestSceneKernelComposition(
                CreateEntityRegistrationPlan(CreateEntry("entity.spawned", 1503)));

            kernel.AttachComposition(composition);

            Assert.That(kernel.TryGetSpawnBoundary(out ISceneKernelSpawnBoundary? resolvedBoundary), Is.True);
            Assert.That(resolvedBoundary, Is.Not.Null);

            SceneKernelSpawnRouteId routeId = SceneKernelSpawnRouteId.FromParts("RuntimeEntity", string.Empty);
            SceneKernelSpawnPoolId poolId = SceneKernelSpawnPoolId.FromParts("RuntimeEntity", string.Empty);
            TestSceneKernelSpawnPool pool = new TestSceneKernelSpawnPool(poolId);
            object spawnResult = new object();
            TestSceneKernelSpawnRouteHandler routeHandler = new TestSceneKernelSpawnRouteHandler(routeId, spawnResult);

            Assert.That(resolvedBoundary!.TryBindSpawnPool(pool), Is.True);
            Assert.That(resolvedBoundary.TryBindSpawnRouteHandler(routeHandler), Is.True);
            Assert.That(resolvedBoundary.TryBindSpawnRoute(routeId, poolId), Is.True);
            Assert.That(resolvedBoundary.TryResolveSpawnPool(routeId, out ISceneKernelSpawnPool? resolvedPool), Is.True);
            Assert.That(resolvedPool, Is.SameAs(pool));
            Assert.That(resolvedBoundary.TryResolveSpawnRouteHandler(routeId, out ISceneKernelSpawnRouteHandler? resolvedHandler), Is.True);
            Assert.That(resolvedHandler, Is.SameAs(routeHandler));
            Assert.That(resolvedHandler!.SpawnAsync(new object(), default).Result, Is.SameAs(spawnResult));

            RuntimeLifetimeScopeDeleteFilter filter = RuntimeLifetimeScopeDeleteFilter.Default;
            Assert.That(resolvedBoundary.TryReleaseAll(routeId, filter, out int releasedCount), Is.True);
            Assert.That(releasedCount, Is.EqualTo(0));
            Assert.That(pool.LastFilter, Is.EqualTo(filter));
            Assert.That(routeHandler.SpawnCallCount, Is.EqualTo(1));
        }

        [Test]
        public void AttachComposition_TryGetValueStoreResolvesRegisteredEntityStore()
        {
            SceneKernel kernel = new SceneKernel(new SceneKernelHandle(1_503), "Battle");
            kernel.Initialize();

            EntityRef entityRef = new EntityRef("entity.value");
            TestValueStore valueStore = new TestValueStore(ValueStoreScopeKind.Entity);
            TestSceneKernelValueStoreBoundary valueStoreBoundary = new TestSceneKernelValueStoreBoundary();
            valueStoreBoundary.Bind(entityRef, valueStore);

            TestSceneKernelComposition composition = new TestSceneKernelComposition(
                CreateEntityRegistrationPlan(CreateEntry(entityRef.Value, 1504)),
                valueStoreBoundary: valueStoreBoundary);

            kernel.AttachComposition(composition);

            Assert.That(kernel.TryGetValueStore(entityRef, out IValueStore? resolvedStore), Is.True);
            Assert.That(resolvedStore, Is.SameAs(valueStore));
            Assert.That(valueStoreBoundary.TryGetCallCount, Is.EqualTo(1));
        }

        [Test]
        public void TryGetValueStore_RejectsEntityThatIsNotRegisteredInSceneKernel()
        {
            SceneKernel kernel = new SceneKernel(new SceneKernelHandle(1_504), "Battle");
            kernel.Initialize();

            EntityRef registeredEntity = new EntityRef("entity.registered");
            EntityRef missingEntity = new EntityRef("entity.missing");
            TestValueStore valueStore = new TestValueStore(ValueStoreScopeKind.Entity);
            TestSceneKernelValueStoreBoundary valueStoreBoundary = new TestSceneKernelValueStoreBoundary();
            valueStoreBoundary.Bind(missingEntity, valueStore);

            TestSceneKernelComposition composition = new TestSceneKernelComposition(
                CreateEntityRegistrationPlan(CreateEntry(registeredEntity.Value, 1505)),
                valueStoreBoundary: valueStoreBoundary);

            kernel.AttachComposition(composition);

            Assert.That(kernel.TryGetValueStore(missingEntity, out _), Is.False);
            Assert.That(valueStoreBoundary.TryGetCallCount, Is.EqualTo(0));
        }

        [Test]
        public void SceneKernelEntityLeaseHandle_EqualityIncludesGeneration()
        {
            SceneKernelHandle sceneHandle = new SceneKernelHandle(1_500);
            EntityRef entityRef = new EntityRef("entity.spawned");

            SceneKernelEntityLeaseHandle first = new SceneKernelEntityLeaseHandle(sceneHandle, entityRef, 17, 1);
            SceneKernelEntityLeaseHandle reused = new SceneKernelEntityLeaseHandle(sceneHandle, entityRef, 17, 2);

            Assert.That(first.Equals(reused), Is.False);
            Assert.That(first == reused, Is.False);
        }

        [Test]
        public void SceneKernelEntityLeaseTable_UsesStableSlotIdsAndGenerationToInvalidateStaleLeases()
        {
            SceneKernelEntityLeaseTable table = new SceneKernelEntityLeaseTable(new SceneKernelHandle(7));
            EntityRef entityRef = new EntityRef("entity.spawned");

            Assert.That(table.TryAcquire(entityRef, out SceneKernelEntityLeaseHandle firstLease), Is.True);
            Assert.That(firstLease.LeaseId, Is.EqualTo(1));
            Assert.That(firstLease.Generation, Is.EqualTo(1));
            Assert.That(table.ActiveLeaseCount, Is.EqualTo(1));
            Assert.That(table.ValidateLease(firstLease), Is.True);

            Assert.That(table.TryAcquire(entityRef, out _), Is.False);

            Assert.That(table.TryRelease(firstLease), Is.True);
            Assert.That(table.ActiveLeaseCount, Is.EqualTo(0));
            Assert.That(table.ValidateLease(firstLease), Is.False);
            Assert.That(table.TryGetLease(entityRef, out _), Is.False);

            Assert.That(table.TryAcquire(entityRef, out SceneKernelEntityLeaseHandle secondLease), Is.True);
            Assert.That(secondLease.LeaseId, Is.EqualTo(1));
            Assert.That(secondLease.Generation, Is.EqualTo(2));
            Assert.That(table.ValidateLease(secondLease), Is.True);
            Assert.That(table.ValidateLease(firstLease), Is.False);
        }

        [Test]
        public void RegisterLookupAndUnregister_UseStableSlotsAndRejectDuplicates()
        {
            SceneKernel kernel = new SceneKernel(new SceneKernelHandle(1), "Battle");
            kernel.Initialize();

            Assert.That(kernel.TryRegisterEntity(CreateEntry("entity.player", 1), out SceneKernelEntitySlot firstSlot, out var firstDiagnostic), Is.True);
            Assert.That(firstDiagnostic, Is.Null);
            Assert.That(firstSlot.SlotIndex, Is.EqualTo(0));

            Assert.That(kernel.TryRegisterEntity(CreateEntry("entity.enemy", 2), out SceneKernelEntitySlot secondSlot, out var secondDiagnostic), Is.True);
            Assert.That(secondDiagnostic, Is.Null);
            Assert.That(secondSlot.SlotIndex, Is.EqualTo(1));

            Assert.That(kernel.TryRegisterEntity(CreateEntry("entity.player", 3), out _, out var duplicateDiagnostic), Is.False);
            Assert.That(duplicateDiagnostic, Is.Not.Null);
            Assert.That(duplicateDiagnostic!.Code.Value, Is.EqualTo("SCENE_ENTITY_REGISTRATION_DUPLICATE_ENTITY_REF"));

            Assert.That(kernel.TryGetEntitySlot(new EntityRef("entity.player"), out SceneKernelEntitySlot resolvedFirstSlot), Is.True);
            Assert.That(resolvedFirstSlot.SlotIndex, Is.EqualTo(0));

            Assert.That(kernel.TryUnregisterEntity(new EntityRef("entity.player"), out SceneKernelEntitySlot removedSlot, out var unregisterDiagnostic), Is.True);
            Assert.That(unregisterDiagnostic, Is.Null);
            Assert.That(removedSlot.SlotIndex, Is.EqualTo(0));

            Assert.That(kernel.TryGetEntitySlot(new EntityRef("entity.player"), out _), Is.False);

            Assert.That(kernel.TryRegisterEntity(CreateEntry("entity.npc", 4), out SceneKernelEntitySlot thirdSlot, out _), Is.True);
            Assert.That(thirdSlot.SlotIndex, Is.EqualTo(2));
        }

        [Test]
        public void ManualEntityRegistrationCannotBeSilentlyReplacedByCompositionHydration()
        {
            SceneKernel kernel = new SceneKernel(new SceneKernelHandle(1_001), "Battle");
            kernel.Initialize();

            Assert.That(kernel.TryRegisterEntity(CreateEntry("entity.manual", 1001), out _, out _), Is.True);
            Assert.That(kernel.TryUnregisterEntity(new EntityRef("entity.manual"), out _, out _), Is.True);

            EntityRegistrationPlan compositionPlan = CreateEntityRegistrationPlan(CreateEntry("entity.composition", 1002));
            TestSceneKernelComposition composition = new TestSceneKernelComposition(compositionPlan);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => kernel.AttachComposition(composition))!;
            Assert.That(exception.Message, Does.Contain("cannot mix manual entity registration"));
        }

        [Test]
        public void AttachComposition_HydratesEntityRegistrationPlanIntoLookupTable()
        {
            SceneKernel kernel = new SceneKernel(new SceneKernelHandle(2), "Town");
            kernel.Initialize();

            EntityRegistrationPlan entityRegistrationPlan = CreateEntityRegistrationPlan(
                CreateEntry("entity.b", 12),
                CreateEntry("entity.a", 11));

            TestSceneKernelComposition composition = new TestSceneKernelComposition(entityRegistrationPlan);
            kernel.AttachComposition(composition);

            Assert.That(kernel.RegisteredEntityCount, Is.EqualTo(2));
            Assert.That(kernel.TryGetEntitySlot(new EntityRef("entity.a"), out SceneKernelEntitySlot firstSlot), Is.True);
            Assert.That(firstSlot.SlotIndex, Is.EqualTo(0));
            Assert.That(kernel.TryGetEntitySlot(new EntityRef("entity.b"), out SceneKernelEntitySlot secondSlot), Is.True);
            Assert.That(secondSlot.SlotIndex, Is.EqualTo(1));
        }

        [Test]
        public void AttachComposition_ResolveUsesEntityRefPlusServiceId()
        {
            SceneKernel kernel = new SceneKernel(new SceneKernelHandle(3), "Arena");
            kernel.Initialize();

            EntityRegistrationPlan entityRegistrationPlan = CreateEntityRegistrationPlan(
                CreateEntry("entity.b", 12),
                CreateEntry("entity.a", 11));

            ServiceGraphPlan serviceGraphPlan = CreateServiceGraphPlan(
                CreateService(5102, 42),
                CreateService(5101, 41));
            ServiceRegistrationPlan serviceRegistrationPlan = CreateServiceRegistrationPlan(
                serviceGraphPlan,
                CreateServiceRegistrationSeed("entity.b", 5102, 62),
                CreateServiceRegistrationSeed("entity.a", 5101, 61));
            EntityServiceRoutePlan entityServiceRoutePlan = CreateEntityServiceRoutePlan(
                serviceGraphPlan,
                CreateRouteSeed("entity.b", 5102, 62),
                CreateRouteSeed("entity.a", 5101, 61));

            TestSceneKernelComposition composition = new TestSceneKernelComposition(
                entityRegistrationPlan,
                serviceRegistrationPlan,
                entityServiceRoutePlan,
                new KernelRuntimeServiceGraph(serviceGraphPlan, KernelProfileKind.Development));
            kernel.AttachComposition(composition);

            Assert.That(kernel.TryResolve(new EntityRef("entity.a"), new ServiceId(5101), out KernelRuntimeServiceSlot resolvedSlot, out var resolvedDiagnostic), Is.True);
            Assert.That(resolvedDiagnostic, Is.Null);
            Assert.That(resolvedSlot.SlotIndex, Is.EqualTo(0));
            Assert.That(resolvedSlot.ServiceIdentity.Value, Is.EqualTo(5101));

            Assert.That(kernel.TryResolve(new EntityRef("entity.a"), new ServiceId(5102), out _, out var missingRouteDiagnostic), Is.False);
            Assert.That(missingRouteDiagnostic, Is.Not.Null);
            Assert.That(missingRouteDiagnostic!.Code.Value, Is.EqualTo("SCENE_SERVICE_ROUTE_MISSING"));

            Assert.That(kernel.TryResolve(new EntityRef("entity.missing"), new ServiceId(5101), out _, out var missingEntityDiagnostic), Is.False);
            Assert.That(missingEntityDiagnostic, Is.Not.Null);
            Assert.That(missingEntityDiagnostic!.Code.Value, Is.EqualTo("SCENE_SERVICE_ROUTE_UNKNOWN_ENTITY_REF"));
        }

        [Test]
        public void AttachComposition_EntityRouteRowsRemainStableAfterOtherEntityUnregister()
        {
            SceneKernel kernel = new SceneKernel(new SceneKernelHandle(31), "Arena");
            kernel.Initialize();

            EntityRegistrationPlan entityRegistrationPlan = CreateEntityRegistrationPlan(
                CreateEntry("entity.b", 72),
                CreateEntry("entity.a", 71));

            ServiceGraphPlan serviceGraphPlan = CreateServiceGraphPlan(
                CreateService(5202, 82),
                CreateService(5201, 81));
            ServiceRegistrationPlan serviceRegistrationPlan = CreateServiceRegistrationPlan(
                serviceGraphPlan,
                CreateServiceRegistrationSeed("entity.b", 5202, 92),
                CreateServiceRegistrationSeed("entity.a", 5201, 91));
            EntityServiceRoutePlan entityServiceRoutePlan = CreateEntityServiceRoutePlan(
                serviceGraphPlan,
                CreateRouteSeed("entity.b", 5202, 92),
                CreateRouteSeed("entity.a", 5201, 91));

            TestSceneKernelComposition composition = new TestSceneKernelComposition(
                entityRegistrationPlan,
                serviceRegistrationPlan,
                entityServiceRoutePlan,
                new KernelRuntimeServiceGraph(serviceGraphPlan, KernelProfileKind.Development));
            kernel.AttachComposition(composition);

            Assert.That(kernel.TryUnregisterEntity(new EntityRef("entity.a"), out SceneKernelEntitySlot removedSlot, out KernelDiagnostic? unregisterDiagnostic), Is.True);
            Assert.That(unregisterDiagnostic, Is.Null);
            Assert.That(removedSlot.SlotIndex, Is.EqualTo(0));

            Assert.That(kernel.TryResolve(new EntityRef("entity.b"), new ServiceId(5202), out KernelRuntimeServiceSlot resolvedSlot, out KernelDiagnostic? resolvedDiagnostic), Is.True);
            Assert.That(resolvedDiagnostic, Is.Null);
            Assert.That(resolvedSlot.SlotIndex, Is.EqualTo(1));
            Assert.That(resolvedSlot.ServiceIdentity.Value, Is.EqualTo(5202));
        }

        [Test]
        public void AttachComposition_ResolveRejectsRouteWhenRuntimeSlotIndexIsMissing()
        {
            SceneKernel kernel = new SceneKernel(new SceneKernelHandle(32), "Arena");
            kernel.Initialize();

            EntityRegistrationPlan entityRegistrationPlan = CreateEntityRegistrationPlan(CreateEntry("entity.route", 101));
            ServiceGraphPlan routePlanGraph = CreateServiceGraphPlan(
                CreateService(5302, 112),
                CreateService(5301, 111));
            ServiceRegistrationPlan serviceRegistrationPlan = CreateServiceRegistrationPlan(
                routePlanGraph,
                CreateServiceRegistrationSeed("entity.route", 5302, 121));
            EntityServiceRoutePlan entityServiceRoutePlan = CreateEntityServiceRoutePlan(
                routePlanGraph,
                CreateRouteSeed("entity.route", 5302, 121));
            ServiceGraphPlan runtimeGraphPlan = CreateServiceGraphPlan(CreateService(5302, 122));

            TestSceneKernelComposition composition = new TestSceneKernelComposition(
                entityRegistrationPlan,
                serviceRegistrationPlan,
                entityServiceRoutePlan,
                new KernelRuntimeServiceGraph(runtimeGraphPlan, KernelProfileKind.Development));
            kernel.AttachComposition(composition);

            Assert.That(kernel.TryResolve(new EntityRef("entity.route"), new ServiceId(5302), out _, out KernelDiagnostic? diagnostic), Is.False);
            Assert.That(diagnostic, Is.Not.Null);
            Assert.That(diagnostic!.Code.Value, Is.EqualTo("SCENE_SERVICE_ROUTE_INVALID_SLOT"));
        }

        [Test]
        public void AttachComposition_ResolveRejectsRouteWhenRuntimeSlotIdentityMismatches()
        {
            SceneKernel kernel = new SceneKernel(new SceneKernelHandle(33), "Arena");
            kernel.Initialize();

            EntityRegistrationPlan entityRegistrationPlan = CreateEntityRegistrationPlan(CreateEntry("entity.route", 131));
            ServiceGraphPlan routePlanGraph = CreateServiceGraphPlan(CreateService(5401, 141));
            ServiceRegistrationPlan serviceRegistrationPlan = CreateServiceRegistrationPlan(
                routePlanGraph,
                CreateServiceRegistrationSeed("entity.route", 5401, 151));
            EntityServiceRoutePlan entityServiceRoutePlan = CreateEntityServiceRoutePlan(
                routePlanGraph,
                CreateRouteSeed("entity.route", 5401, 151));
            ServiceGraphPlan runtimeGraphPlan = CreateServiceGraphPlan(CreateService(6401, 142));

            TestSceneKernelComposition composition = new TestSceneKernelComposition(
                entityRegistrationPlan,
                serviceRegistrationPlan,
                entityServiceRoutePlan,
                new KernelRuntimeServiceGraph(runtimeGraphPlan, KernelProfileKind.Development));
            kernel.AttachComposition(composition);

            Assert.That(kernel.TryResolve(new EntityRef("entity.route"), new ServiceId(5401), out _, out KernelDiagnostic? diagnostic), Is.False);
            Assert.That(diagnostic, Is.Not.Null);
            Assert.That(diagnostic!.Code.Value, Is.EqualTo("SCENE_SERVICE_SLOT_METADATA_MISMATCH"));
        }

        [Test]
        public void AttachComposition_ResolveFailsClosed_WhenServiceRegistrationPlanIsMissing()
        {
            SceneKernel kernel = new SceneKernel(new SceneKernelHandle(34), "Arena");
            kernel.Initialize();

            EntityRegistrationPlan entityRegistrationPlan = CreateEntityRegistrationPlan(CreateEntry("entity.route", 161));
            ServiceGraphPlan serviceGraphPlan = CreateServiceGraphPlan(CreateService(5501, 171));
            EntityServiceRoutePlan entityServiceRoutePlan = CreateEntityServiceRoutePlan(
                serviceGraphPlan,
                CreateRouteSeed("entity.route", 5501, 181));

            TestSceneKernelComposition composition = new TestSceneKernelComposition(
                entityRegistrationPlan,
                entityServiceRoutePlan: entityServiceRoutePlan,
                runtimeServiceGraph: new KernelRuntimeServiceGraph(serviceGraphPlan, KernelProfileKind.Development));
            kernel.AttachComposition(composition);

            Assert.That(kernel.TryResolve(new EntityRef("entity.route"), new ServiceId(5501), out _, out KernelDiagnostic? diagnostic), Is.False);
            Assert.That(diagnostic, Is.Not.Null);
            Assert.That(diagnostic!.Code.Value, Is.EqualTo("SCENE_SERVICE_REGISTRATION_PLAN_MISSING"));
        }

        [Test]
        public void AttachComposition_ResolveRejectsRuntimeSlotWhenRegistrationMetadataMismatches()
        {
            SceneKernel kernel = new SceneKernel(new SceneKernelHandle(35), "Arena");
            kernel.Initialize();

            EntityRegistrationPlan entityRegistrationPlan = CreateEntityRegistrationPlan(CreateEntry("entity.route", 191));
            ServiceGraphPlan routePlanGraph = CreateServiceGraphPlan(CreateService(5601, 201));
            ServiceRegistrationPlan serviceRegistrationPlan = CreateServiceRegistrationPlan(
                routePlanGraph,
                CreateServiceRegistrationSeed("entity.route", 5601, 211));
            EntityServiceRoutePlan entityServiceRoutePlan = CreateEntityServiceRoutePlan(
                routePlanGraph,
                CreateRouteSeed("entity.route", 5601, 211));
            ServiceGraphPlan runtimeGraphPlan = CreateServiceGraphPlan(CreateService(5601, 202, ownerModuleId: 77));

            TestSceneKernelComposition composition = new TestSceneKernelComposition(
                entityRegistrationPlan,
                serviceRegistrationPlan,
                entityServiceRoutePlan,
                new KernelRuntimeServiceGraph(runtimeGraphPlan, KernelProfileKind.Development));
            kernel.AttachComposition(composition);

            Assert.That(kernel.TryResolve(new EntityRef("entity.route"), new ServiceId(5601), out _, out KernelDiagnostic? diagnostic), Is.False);
            Assert.That(diagnostic, Is.Not.Null);
            Assert.That(diagnostic!.Code.Value, Is.EqualTo("SCENE_SERVICE_SLOT_METADATA_MISMATCH"));
        }

        [Test]
        public void DispatchLifecycle_DispatchesRegisteredServiceHandler()
        {
            SceneKernel kernel = new SceneKernel(new SceneKernelHandle(4), "Lifecycle");
            kernel.Initialize();

            EntityRegistrationPlan entityRegistrationPlan = CreateEntityRegistrationPlan(CreateEntry("entity.lifecycle", 21));
            LifecyclePlan lifecyclePlan = CreateLifecyclePlan(
                701,
                CreateLifecycleStep(7101, LifecyclePhase.Acquire, 10, new LifecycleTargetRefIR(new ServiceId(9101)), LifecycleActionKind.ServiceMethod, 7201));
            KernelLifecycleDispatcher dispatcher = new KernelLifecycleDispatcher(lifecyclePlan);
            KernelLifecyclePlanResolver resolver = new KernelLifecyclePlanResolver(new[] { dispatcher });
            KernelRuntimeScopeGraph scopeGraph = new KernelRuntimeScopeGraph(CreateScopeGraphPlan(701), new[] { new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, 21) }, resolver);
            KernelRuntimeServiceGraph runtimeServiceGraph = new KernelRuntimeServiceGraph(
                CreateServiceGraphPlan(CreateService(9101, 7301)),
                KernelProfileKind.Development);

            TestSceneKernelComposition composition = new TestSceneKernelComposition(
                entityRegistrationPlan,
                runtimeServiceGraph: runtimeServiceGraph,
                runtimeScopeGraph: scopeGraph,
                lifecycleDispatcher: dispatcher,
                lifecyclePlanResolver: resolver);
            kernel.AttachComposition(composition);

            TestSceneKernelServiceLifecycleHandler handler = new TestSceneKernelServiceLifecycleHandler(new ServiceId(9101));
            Assert.That(kernel.TryRegisterServiceLifecycleHandler(handler), Is.True);

            Assert.That(kernel.TryDispatchLifecycle(LifecyclePhase.Acquire, out LifecycleDispatchResult result, out KernelDiagnostic? diagnostic), Is.True);
            Assert.That(result.AttemptedStepCount, Is.EqualTo(1));
            Assert.That(result.SucceededStepCount, Is.EqualTo(1));
            Assert.That(result.FailedStepCount, Is.EqualTo(0));
            Assert.That(diagnostic, Is.Null);
            Assert.That(handler.DispatchCount, Is.EqualTo(1));
            Assert.That(handler.RollbackCount, Is.EqualTo(0));
            Assert.That(handler.LastPhase, Is.EqualTo(LifecyclePhase.Acquire));
            Assert.That(handler.LastServiceSlot.ServiceIdentity, Is.EqualTo(new RuntimeIdentityRef(RuntimeIdentityKind.Service, 9101)));
        }

        [Test]
        public void DispatchLifecycle_FailsClosed_WhenServiceTargetHandlerMissing()
        {
            SceneKernel kernel = new SceneKernel(new SceneKernelHandle(4_001), "Lifecycle");
            kernel.Initialize();

            EntityRegistrationPlan entityRegistrationPlan = CreateEntityRegistrationPlan(CreateEntry("entity.lifecycle", 22));
            LifecyclePlan lifecyclePlan = CreateLifecyclePlan(
                702,
                CreateLifecycleStep(7102, LifecyclePhase.Acquire, 10, new LifecycleTargetRefIR(new ServiceId(9102)), LifecycleActionKind.ServiceMethod, 7202));
            KernelLifecycleDispatcher dispatcher = new KernelLifecycleDispatcher(lifecyclePlan);
            KernelLifecyclePlanResolver resolver = new KernelLifecyclePlanResolver(new[] { dispatcher });
            KernelRuntimeScopeGraph scopeGraph = new KernelRuntimeScopeGraph(CreateScopeGraphPlan(702), new[] { new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, 22) }, resolver);
            KernelRuntimeServiceGraph runtimeServiceGraph = new KernelRuntimeServiceGraph(
                CreateServiceGraphPlan(CreateService(9102, 7302)),
                KernelProfileKind.Development);

            TestSceneKernelComposition composition = new TestSceneKernelComposition(
                entityRegistrationPlan,
                runtimeServiceGraph: runtimeServiceGraph,
                runtimeScopeGraph: scopeGraph,
                lifecycleDispatcher: dispatcher,
                lifecyclePlanResolver: resolver);
            kernel.AttachComposition(composition);

            Assert.That(kernel.TryDispatchLifecycle(LifecyclePhase.Acquire, out LifecycleDispatchResult result, out KernelDiagnostic? diagnostic), Is.False);
            Assert.That(result.AttemptedStepCount, Is.EqualTo(1));
            Assert.That(result.FailedStepCount, Is.EqualTo(1));
            Assert.That(diagnostic, Is.Not.Null);
            Assert.That(diagnostic!.Code.Value, Is.EqualTo("SCENE_LIFECYCLE_SERVICE_TARGET_HANDLER_MISSING"));
        }

        [Test]
        public void DispatchLifecycle_LazilyCreatesAndCachesServiceHandlerFromFactory()
        {
            SceneKernel kernel = new SceneKernel(new SceneKernelHandle(4_002), "Lifecycle");
            kernel.Initialize();

            EntityRegistrationPlan entityRegistrationPlan = CreateEntityRegistrationPlan(CreateEntry("entity.lifecycle", 23));
            ServiceGraphPlan serviceGraphPlan = CreateServiceGraphPlan(CreateService(9103, 7303));
            ServiceRegistrationPlan serviceRegistrationPlan = CreateServiceRegistrationPlan(
                serviceGraphPlan,
                CreateServiceRegistrationSeed("entity.lifecycle", 9103, 7303));
            LifecyclePlan lifecyclePlan = CreateLifecyclePlan(
                703,
                CreateLifecycleStep(7103, LifecyclePhase.Acquire, 10, new LifecycleTargetRefIR(new ServiceId(9103)), LifecycleActionKind.ServiceMethod, 7203));
            KernelLifecycleDispatcher dispatcher = new KernelLifecycleDispatcher(lifecyclePlan);
            KernelLifecyclePlanResolver resolver = new KernelLifecyclePlanResolver(new[] { dispatcher });
            KernelRuntimeScopeGraph scopeGraph = new KernelRuntimeScopeGraph(CreateScopeGraphPlan(703), new[] { new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, 23) }, resolver);
            KernelRuntimeServiceGraph runtimeServiceGraph = new KernelRuntimeServiceGraph(serviceGraphPlan, KernelProfileKind.Development);

            TestSceneKernelComposition composition = new TestSceneKernelComposition(
                entityRegistrationPlan,
                serviceRegistrationPlan,
                runtimeServiceGraph: runtimeServiceGraph,
                runtimeScopeGraph: scopeGraph,
                lifecycleDispatcher: dispatcher,
                lifecyclePlanResolver: resolver);
            kernel.AttachComposition(composition);

            TestSceneKernelServiceLifecycleHandler producedHandler = new TestSceneKernelServiceLifecycleHandler(new ServiceId(9103));
            TestSceneKernelServiceFactory factory = new TestSceneKernelServiceFactory(new ServiceId(9103), producedHandler);
            Assert.That(kernel.TryRegisterServiceFactory(factory), Is.True);

            Assert.That(kernel.TryDispatchLifecycle(LifecyclePhase.Acquire, out LifecycleDispatchResult firstResult, out KernelDiagnostic? firstDiagnostic), Is.True);
            Assert.That(firstDiagnostic, Is.Null);
            Assert.That(firstResult.AttemptedStepCount, Is.EqualTo(1));
            Assert.That(firstResult.SucceededStepCount, Is.EqualTo(1));
            Assert.That(factory.CreateCount, Is.EqualTo(1));
            Assert.That(factory.LastActivationRegistration.EntityRef.Value, Is.EqualTo("entity.lifecycle"));
            Assert.That(factory.LastActivationRegistration.ServiceId, Is.EqualTo(new ServiceId(9103)));
            Assert.That(producedHandler.DispatchCount, Is.EqualTo(1));

            Assert.That(kernel.TryDispatchLifecycle(LifecyclePhase.Acquire, out LifecycleDispatchResult secondResult, out KernelDiagnostic? secondDiagnostic), Is.True);
            Assert.That(secondDiagnostic, Is.Null);
            Assert.That(secondResult.SucceededStepCount, Is.EqualTo(1));
            Assert.That(factory.CreateCount, Is.EqualTo(1));
            Assert.That(producedHandler.DispatchCount, Is.EqualTo(2));
        }

        [Test]
        public void TryTransitionScopeState_BuiltToActive_DispatchesAcquireThenActivate_AndCommitsState()
        {
            SceneKernel kernel = new SceneKernel(new SceneKernelHandle(5), "Lifecycle");
            kernel.Initialize();

            EntityRegistrationPlan entityRegistrationPlan = CreateEntityRegistrationPlan(CreateEntry("entity.lifecycle", 31));
            LifecyclePlan lifecyclePlan = CreateLifecyclePlan(
                801,
                CreateLifecycleStep(8101, LifecyclePhase.Acquire, 10, new LifecycleTargetRefIR(new ScopePlanId(21)), LifecycleActionKind.ScopeStateTransition, 8201),
                CreateLifecycleStep(8102, LifecyclePhase.Activate, 20, new LifecycleTargetRefIR(new ScopePlanId(21)), LifecycleActionKind.ScopeStateTransition, 8202));
            KernelLifecycleDispatcher dispatcher = new KernelLifecycleDispatcher(lifecyclePlan);
            KernelLifecyclePlanResolver resolver = new KernelLifecyclePlanResolver(new[] { dispatcher });
            KernelRuntimeScopeGraph scopeGraph = new KernelRuntimeScopeGraph(CreateScopeGraphPlan(801), new[] { new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, 21) }, resolver);
            ScopeHandle rootHandle = scopeGraph.RootScopeHandles[0];
            Assert.That(scopeGraph.TryCommitState(rootHandle, ScopeRuntimeState.Built, out _, out _), Is.True);

            TestSceneKernelComposition composition = new TestSceneKernelComposition(
                entityRegistrationPlan,
                runtimeScopeGraph: scopeGraph,
                lifecycleDispatcher: dispatcher,
                lifecyclePlanResolver: resolver);
            kernel.AttachComposition(composition);

            Assert.That(kernel.TryTransitionScopeState(rootHandle, ScopeRuntimeState.Active, out LifecycleDispatchResult result, out KernelDiagnostic? diagnostic), Is.True);
            Assert.That(diagnostic, Is.Null);
            Assert.That(result.AttemptedStepCount, Is.EqualTo(2));
            Assert.That(result.SucceededStepCount, Is.EqualTo(2));
            Assert.That(result.FailedStepCount, Is.EqualTo(0));
            Assert.That(scopeGraph.TryGetScope(rootHandle, out ScopeRuntimeSnapshot snapshot), Is.True);
            Assert.That(snapshot.State, Is.EqualTo(ScopeRuntimeState.Active));
            Assert.That(scopeGraph.TryGetLifecycleTransitionRequests(rootHandle, out _), Is.False);
        }

        [Test]
        public void TryTransitionScopeState_CreatedToBuilt_DispatchesValueStoreTargetThroughBoundary()
        {
            SceneKernel kernel = new SceneKernel(new SceneKernelHandle(5_100), "Lifecycle");
            kernel.Initialize();

            EntityRegistrationPlan entityRegistrationPlan = CreateEntityRegistrationPlan(CreateEntry("entity.lifecycle", 131));
            LifecyclePlan lifecyclePlan = CreateLifecyclePlan(
                1_801,
                CreateLifecycleStep(1_811, LifecyclePhase.Create, 10, new LifecycleTargetRefIR(LifecycleTargetKind.ValueStore, "local:blackboard"), LifecycleActionKind.ValueInit, 1_821));
            KernelLifecycleDispatcher dispatcher = new KernelLifecycleDispatcher(lifecyclePlan);
            KernelLifecyclePlanResolver resolver = new KernelLifecyclePlanResolver(new[] { dispatcher });
            KernelRuntimeScopeGraph scopeGraph = new KernelRuntimeScopeGraph(CreateScopeGraphPlan(1_801), new[] { new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, 21) }, resolver);
            ScopeHandle rootHandle = scopeGraph.RootScopeHandles[0];
            Assert.That(scopeGraph.TrySetUnityLink(rootHandle, new UnityObjectLink(UnityObjectLinkKind.Runtime, null, 0, 9_001, "Scope21")), Is.True);

            TestSceneKernelValueStoreBoundary valueStoreBoundary = new TestSceneKernelValueStoreBoundary();
            valueStoreBoundary.BindValueInitHost(9_001, "local:blackboard");

            TestSceneKernelComposition composition = new TestSceneKernelComposition(
                entityRegistrationPlan,
                runtimeScopeGraph: scopeGraph,
                lifecycleDispatcher: dispatcher,
                lifecyclePlanResolver: resolver,
                valueStoreBoundary: valueStoreBoundary);
            kernel.AttachComposition(composition);

            Assert.That(kernel.TryTransitionScopeState(rootHandle, ScopeRuntimeState.Built, out LifecycleDispatchResult result, out KernelDiagnostic? diagnostic), Is.True);
            Assert.That(diagnostic, Is.Null);
            Assert.That(result.AttemptedStepCount, Is.EqualTo(1));
            Assert.That(result.SucceededStepCount, Is.EqualTo(1));
            Assert.That(valueStoreBoundary.ValueInitDispatchCallCount, Is.EqualTo(1));
            Assert.That(valueStoreBoundary.LastValueInitRuntimeInstanceId, Is.EqualTo(9_001));
            Assert.That(valueStoreBoundary.LastValueInitTargetStoreRef, Is.EqualTo("local:blackboard"));
            Assert.That(valueStoreBoundary.LastValueInitPhase, Is.EqualTo(LifecyclePhase.Create));
            Assert.That(scopeGraph.TryGetScope(rootHandle, out ScopeRuntimeSnapshot snapshot), Is.True);
            Assert.That(snapshot.State, Is.EqualTo(ScopeRuntimeState.Built));
        }

        [Test]
        public void TryTransitionScopeState_AcquireFailure_ForcesFailedState()
        {
            SceneKernel kernel = new SceneKernel(new SceneKernelHandle(6), "Lifecycle");
            kernel.Initialize();

            EntityRegistrationPlan entityRegistrationPlan = CreateEntityRegistrationPlan(CreateEntry("entity.lifecycle", 41));
            LifecyclePlan lifecyclePlan = CreateLifecyclePlan(
                901,
                CreateLifecycleStep(9101, LifecyclePhase.Acquire, 10, new LifecycleTargetRefIR(new ServiceId(9901)), LifecycleActionKind.ServiceMethod, 9201),
                CreateLifecycleStep(9102, LifecyclePhase.Activate, 20, new LifecycleTargetRefIR(new ScopePlanId(21)), LifecycleActionKind.ScopeStateTransition, 9202));
            KernelLifecycleDispatcher dispatcher = new KernelLifecycleDispatcher(lifecyclePlan);
            KernelLifecyclePlanResolver resolver = new KernelLifecyclePlanResolver(new[] { dispatcher });
            KernelRuntimeScopeGraph scopeGraph = new KernelRuntimeScopeGraph(CreateScopeGraphPlan(901), new[] { new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, 21) }, resolver);
            ScopeHandle rootHandle = scopeGraph.RootScopeHandles[0];
            Assert.That(scopeGraph.TryCommitState(rootHandle, ScopeRuntimeState.Built, out _, out _), Is.True);

            TestSceneKernelComposition composition = new TestSceneKernelComposition(
                entityRegistrationPlan,
                runtimeScopeGraph: scopeGraph,
                lifecycleDispatcher: dispatcher,
                lifecyclePlanResolver: resolver);
            kernel.AttachComposition(composition);

            Assert.That(kernel.TryTransitionScopeState(rootHandle, ScopeRuntimeState.Active, out LifecycleDispatchResult result, out KernelDiagnostic? diagnostic), Is.False);
            Assert.That(result.AttemptedStepCount, Is.EqualTo(1));
            Assert.That(result.FailedStepCount, Is.EqualTo(1));
            Assert.That(diagnostic, Is.Not.Null);
            Assert.That(diagnostic!.Code.Value, Is.EqualTo("SCENE_LIFECYCLE_TRANSITION_FORCED_FAILED_STATE"));
            Assert.That(scopeGraph.TryGetScope(rootHandle, out ScopeRuntimeSnapshot snapshot), Is.True);
            Assert.That(snapshot.State, Is.EqualTo(ScopeRuntimeState.Failed));
        }

        [Test]
        public void TryResolveSourceLocation_UsesRuntimeDebugMap()
        {
            SceneKernel kernel = new SceneKernel(new SceneKernelHandle(7), "Diagnostics");
            kernel.Initialize();

            EntityRegistrationPlan entityRegistrationPlan = CreateEntityRegistrationPlan(CreateEntry("entity.debug", 51));
            KernelDebugMap debugMap = CreateDebugMap(new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, 21), 777);
            TestKernelBootRuntimeSurface runtimeSurface = new TestKernelBootRuntimeSurface(debugMap);
            TestSceneKernelComposition composition = new TestSceneKernelComposition(entityRegistrationPlan, runtimeSurface: runtimeSurface);
            kernel.AttachComposition(composition);

            Assert.That(kernel.TryResolveSourceLocation(new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, 21), out SourceLocationRef sourceLocation, out KernelDiagnostic? diagnostic), Is.True);
            Assert.That(diagnostic, Is.Null);
            Assert.That(sourceLocation.Value, Is.EqualTo(777));
        }

        [Test]
        public void DuplicateEntityRegistration_ReportsStructuredDiagnosticToApplicationSink()
        {
            InMemoryDiagnosticSink sink = new InMemoryDiagnosticSink(8);
            KernelDiagnosticService diagnosticService = new KernelDiagnosticService(new IKernelDiagnosticSink[] { sink });
            ApplicationKernel applicationKernel = new ApplicationKernel("DiagnosticsApp");
            applicationKernel.Initialize();
            applicationKernel.AttachComposition(new ApplicationKernelComposition(new StubRuntimeSurfaceFactory(), diagnosticService));

            SceneKernel kernel = new SceneKernel(new SceneKernelHandle(8), "Diagnostics");
            kernel.Initialize();
            applicationKernel.AttachSceneKernel(kernel);

            Assert.That(kernel.TryRegisterEntity(CreateEntry("entity.duplicate", 61), out _, out _), Is.True);
            Assert.That(kernel.TryRegisterEntity(CreateEntry("entity.duplicate", 62), out _, out KernelDiagnostic? diagnostic), Is.False);
            Assert.That(diagnostic, Is.Not.Null);
            Assert.That(diagnostic!.Code.Value, Is.EqualTo("SCENE_ENTITY_REGISTRATION_DUPLICATE_ENTITY_REF"));
            Assert.That(sink.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(sink.Diagnostics[0].Code.Value, Is.EqualTo("SCENE_ENTITY_REGISTRATION_DUPLICATE_ENTITY_REF"));
        }

        static EntityRegistrationPlanEntry CreateEntry(string entityRef, int sourceId)
        {
            return new EntityRegistrationPlanEntry(
                new ModuleId(10),
                new EntityRef(entityRef),
                entityRef + ".display",
                entityRef + ".debug",
                entityRef + ".metadata",
                new[] { "tag.shared", entityRef + ".tag" },
                new SourceLocationIR(new GeneratedSourceLocation("Test", "SceneKernelEntityTable", "Source" + sourceId)));
        }

        static EntityRegistrationPlan CreateEntityRegistrationPlan(params EntityRegistrationPlanEntry[] entityEntries)
        {
            KernelProjectionGenerationResult result = KernelProjectionGenerator.Generate(
                CreateKernelIR(),
                new PlanId(401),
                new ArtifactSetId(501),
                4,
                "1.0.0",
                "Development",
                KernelProfileMask.Development,
                entityEntries);

            return result.Projections.EntityRegistrationPlan;
        }

        static EntityServiceRoutePlan CreateEntityServiceRoutePlan(ServiceGraphPlan serviceGraphPlan, params EntityServiceRouteSeed[] seeds)
        {
            EntityServiceRoutePlanEntry[] plannedEntries = EntityServiceRoutePlanEntry.BuildEntries(seeds, serviceGraphPlan);
            Hash128 contentHash = KernelProjectionHashing.ComputeEntityServiceRouteHash(plannedEntries);
            VerifiedArtifactHeader header = new VerifiedArtifactHeader(
                new PlanId(402),
                new ArtifactSetId(502),
                new ArtifactId(4),
                ArtifactKind.EntityServiceRoute,
                4,
                new Hash128(1, 2, 3, 4),
                new Hash128(5, 6, 7, 8),
                new Hash128(9, 10, 11, 12),
                new Hash128(13, 14, 15, 16),
                contentHash,
                "1.0.0");

            return new EntityServiceRoutePlan(header, seeds, serviceGraphPlan);
        }

        static ServiceRegistrationPlan CreateServiceRegistrationPlan(ServiceGraphPlan serviceGraphPlan, params ServiceRegistrationSeed[] seeds)
        {
            ServiceRegistrationPlanEntry[] plannedEntries = ServiceRegistrationPlanEntry.BuildEntries(seeds, serviceGraphPlan);
            Hash128 contentHash = KernelProjectionHashing.ComputeServiceRegistrationHash(plannedEntries);
            VerifiedArtifactHeader header = new VerifiedArtifactHeader(
                new PlanId(407),
                new ArtifactSetId(507),
                new ArtifactId(5),
                ArtifactKind.ServiceRegistration,
                4,
                new Hash128(1, 2, 3, 4),
                new Hash128(5, 6, 7, 8),
                new Hash128(9, 10, 11, 12),
                new Hash128(13, 14, 15, 16),
                contentHash,
                "1.0.0");

            return new ServiceRegistrationPlan(header, seeds, serviceGraphPlan);
        }

        static ServiceGraphPlan CreateServiceGraphPlan(params ServiceIR[] services)
        {
            Hash128 contentHash = KernelProjectionHashing.ComputeServiceGraphHash(services);
            VerifiedArtifactHeader header = new VerifiedArtifactHeader(
                new PlanId(403),
                new ArtifactSetId(503),
                new ArtifactId(1),
                ArtifactKind.ServiceGraph,
                4,
                new Hash128(1, 2, 3, 4),
                new Hash128(5, 6, 7, 8),
                new Hash128(9, 10, 11, 12),
                new Hash128(13, 14, 15, 16),
                contentHash,
                "1.0.0");

            return new ServiceGraphPlan(header, services);
        }

        static ServiceIR CreateService(int serviceId, int sourceId, int ownerModuleId = 10, ServiceLifetimeKind lifetime = ServiceLifetimeKind.Singleton, ServiceFactoryKind factoryKind = ServiceFactoryKind.GeneratedFactory, ServiceCardinalityKind cardinality = ServiceCardinalityKind.SingletonGlobal)
        {
            return new ServiceIR(
                new ServiceId(serviceId),
                "Service" + serviceId,
                lifetime,
                new ModuleId(ownerModuleId),
                new[] { new ServiceContractIR("IService" + serviceId, new SourceLocationId(sourceId)) },
                Array.Empty<ServiceDependencyIR>(),
                factoryKind,
                new SourceLocationId(sourceId),
                cardinality);
        }

        static EntityServiceRouteSeed CreateRouteSeed(string entityRef, int serviceId, int sourceId)
        {
            return new EntityServiceRouteSeed(
                new ModuleId(10),
                new EntityRef(entityRef),
                new ServiceId(serviceId),
                "Service" + serviceId,
                "Route" + serviceId,
                new SourceLocationIR(new GeneratedSourceLocation("Test", "SceneKernelResolve", "Route" + sourceId)));
        }

        static ServiceRegistrationSeed CreateServiceRegistrationSeed(string entityRef, int serviceId, int sourceId, int ownerModuleId = 10, ServiceLifetimeKind lifetime = ServiceLifetimeKind.Singleton, ServiceCardinalityKind cardinality = ServiceCardinalityKind.SingletonGlobal, ServiceFactoryKind factoryKind = ServiceFactoryKind.GeneratedFactory)
        {
            return new ServiceRegistrationSeed(
                new ModuleId(ownerModuleId),
                new EntityRef(entityRef),
                new ServiceId(serviceId),
                "service." + serviceId,
                "Service" + serviceId,
                "Reg" + serviceId,
                new[] { "IService" + serviceId },
                Array.Empty<ServiceRegistrationDependencyPlan>(),
                lifetime,
                cardinality,
                factoryKind,
                new SourceLocationIR(new GeneratedSourceLocation("Test", "SceneKernelResolve", "Registration" + sourceId)));
        }

        static ScopeGraphPlan CreateScopeGraphPlan(int lifecyclePlanId)
        {
            ScopeIR[] scopes =
            {
                new ScopeIR(
                    new ScopeAuthoringId(1),
                    new ScopePlanId(21),
                    "Scope21",
                    ScopeKind.Root,
                    new ModuleId(10),
                    default,
                    Array.Empty<ScopeServiceRequirementIR>(),
                    Array.Empty<ScopeValueInitRefIR>(),
                    new ScopeServiceBoundaryIR(ScopeServiceBoundaryKind.Detached, 0, new SourceLocationId(301)),
                    new LifecyclePlanRefIR(new LifecyclePlanId(lifecyclePlanId), new SourceLocationId(302)),
                    new SourceLocationId(303)),
            };

            Hash128 contentHash = KernelProjectionHashing.ComputeScopeGraphHash(scopes, Array.Empty<ValueInitPlanIR>());
            VerifiedArtifactHeader header = new VerifiedArtifactHeader(
                new PlanId(404),
                new ArtifactSetId(504),
                new ArtifactId(2),
                ArtifactKind.ScopeGraph,
                4,
                new Hash128(1, 2, 3, 4),
                new Hash128(5, 6, 7, 8),
                new Hash128(9, 10, 11, 12),
                new Hash128(13, 14, 15, 16),
                contentHash,
                "1.0.0");

            return new ScopeGraphPlan(header, scopes, Array.Empty<ValueInitPlanIR>());
        }

        static LifecyclePlan CreateLifecyclePlan(int planId, params LifecycleStepIR[] steps)
        {
            LifecycleIR lifecycle = new LifecycleIR(
                new LifecyclePlanId(planId),
                "Lifecycle" + planId,
                new ModuleId(10),
                steps,
                new SourceLocationId(planId + 100),
                LifecycleFailurePolicy.FailOperation,
                true,
                KernelProfileMask.None,
                null,
                LifecycleAcquireRollbackPolicy.ReverseCompletedAcquireSteps);

            Hash128 contentHash = KernelProjectionHashing.ComputeLifecyclePlanHash(new[] { lifecycle });
            VerifiedArtifactHeader header = new VerifiedArtifactHeader(
                new PlanId(405),
                new ArtifactSetId(505),
                new ArtifactId(3),
                ArtifactKind.LifecyclePlan,
                4,
                new Hash128(1, 2, 3, 4),
                new Hash128(5, 6, 7, 8),
                new Hash128(9, 10, 11, 12),
                new Hash128(13, 14, 15, 16),
                contentHash,
                "1.0.0");

            return new LifecyclePlan(header, new[] { lifecycle });
        }

        static LifecycleStepIR CreateLifecycleStep(int id, LifecyclePhase phase, int order, LifecycleTargetRefIR target, LifecycleActionKind action, int sourceId)
        {
            return new LifecycleStepIR(
                new LifecycleStepId(id),
                phase,
                order,
                target,
                action,
                Array.Empty<DependencyEdgeId>(),
                new SourceLocationId(sourceId));
        }

        static KernelDebugMap CreateDebugMap(RuntimeIdentityRef identity, int sourceId)
        {
            KernelDebugMapEntry[] entries =
            {
                new KernelDebugMapEntry(identity, "DebugIdentity", new ModuleId(10), new SourceLocationId(sourceId), KernelProfileMask.Development, new Hash128(2, 3, 4, 5)),
            };

            Hash128 contentHash = KernelProjectionHashing.ComputeDebugMapHash(entries);
            VerifiedArtifactHeader header = new VerifiedArtifactHeader(
                new PlanId(406),
                new ArtifactSetId(506),
                new ArtifactId(9),
                ArtifactKind.KernelDebugMap,
                4,
                new Hash128(1, 2, 3, 4),
                new Hash128(5, 6, 7, 8),
                new Hash128(9, 10, 11, 12),
                contentHash,
                contentHash,
                "1.0.0");

            return new KernelDebugMap(header, entries);
        }

        static KernelIR CreateKernelIR()
        {
            SourceLocationTable sources = new SourceLocationTable(new[]
            {
                new SourceLocationIR(new GeneratedSourceLocation("Test", "SceneKernelEntityTable", "KernelIR")),
            });

            ModuleIR module = new ModuleIR(
                new ModuleId(10),
                "SceneKernelEntityTable",
                ModuleKind.Feature,
                new ModuleVersion(1),
                new ModuleAvailabilityIR(new AvailabilityIR(KernelProfileMask.Development, true, null)),
                new SourceLocationId(1));

            return new KernelIR(
                new KernelIRHeader("SceneKernelEntityTable", 1, "TinnosukeGameLib", "Development", "1.0.0", new Hash128(1, 2, 3, 4), new Hash128(5, 6, 7, 8)),
                new KernelProfileIR("Development", KernelProfileMask.Development, new AvailabilityIR(KernelProfileMask.Development, true, null)),
                new[] { module },
                Array.Empty<ScopeIR>(),
                Array.Empty<ServiceIR>(),
                Array.Empty<CommandIR>(),
                Array.Empty<ValueKeyIR>(),
                Array.Empty<LifecycleIR>(),
                Array.Empty<RuntimeQueryIR>(),
                Array.Empty<DependencyEdgeIR>(),
                sources,
                null);
        }

        sealed class TestSceneKernelComposition : ISceneKernelComposition
        {
            static readonly KernelComponentPlacementDescriptor[] PlacementsValue =
            {
                new KernelComponentPlacementDescriptor(
                    "TestSceneKernelComposition",
                    KernelMappedComponentKind.RuntimeScopeGraph,
                    KernelComponentPlacementScope.Scene,
                    "Test composition used for SceneKernel entity registration table tests."),
            };

            public TestSceneKernelComposition(
                EntityRegistrationPlan entityRegistrationPlan,
                ServiceRegistrationPlan? serviceRegistrationPlan = null,
                EntityServiceRoutePlan? entityServiceRoutePlan = null,
                KernelRuntimeServiceGraph? runtimeServiceGraph = null,
                KernelRuntimeScopeGraph? runtimeScopeGraph = null,
                ISceneKernelSpawnBoundary? spawnBoundary = null,
                ISceneKernelValueStoreBoundary? valueStoreBoundary = null,
                KernelLifecycleDispatcher? lifecycleDispatcher = null,
                ILifecyclePlanResolver? lifecyclePlanResolver = null,
                IKernelBootRuntimeSurface? runtimeSurface = null)
            {
                EntityRegistrationPlan = entityRegistrationPlan;
                ServiceRegistrationPlan = serviceRegistrationPlan;
                EntityServiceRoutePlan = entityServiceRoutePlan;
                RuntimeServiceGraph = runtimeServiceGraph;
                RuntimeScopeGraph = runtimeScopeGraph;
                this.spawnBoundary = spawnBoundary;
                this.valueStoreBoundary = valueStoreBoundary;
                LifecycleDispatcher = lifecycleDispatcher;
                LifecyclePlanResolver = lifecyclePlanResolver;
                RuntimeSurface = runtimeSurface;
            }

            public System.Collections.Generic.IReadOnlyList<KernelComponentPlacementDescriptor> Placements => PlacementsValue;

            public ISceneKernelSpawnBoundary? SpawnBoundary => spawnBoundary;

            public ISceneKernelValueStoreBoundary? ValueStoreBoundary => valueStoreBoundary;

            ISceneKernelSpawnBoundary? spawnBoundary;

            ISceneKernelValueStoreBoundary? valueStoreBoundary;

            public EntityRegistrationPlan? EntityRegistrationPlan { get; }

            public ServiceRegistrationPlan? ServiceRegistrationPlan { get; }

            public EntityServiceRoutePlan? EntityServiceRoutePlan { get; }

            public KernelRuntimeServiceGraph? RuntimeServiceGraph { get; }

            public KernelRuntimeScopeGraph? RuntimeScopeGraph { get; }

            public KernelLifecycleDispatcher? LifecycleDispatcher { get; }

            public ILifecyclePlanResolver? LifecyclePlanResolver { get; }

            public IKernelBootRuntimeSurface? RuntimeSurface { get; }

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

            public bool TryGetBoundary(SceneKernelBoundaryKind boundaryKind, out object? boundary)
            {
                if (boundaryKind == SceneKernelBoundaryKind.EntityRegistrationPlan && EntityRegistrationPlan != null)
                {
                    boundary = EntityRegistrationPlan;
                    return true;
                }

                if (boundaryKind == SceneKernelBoundaryKind.ServiceRegistrationPlan && ServiceRegistrationPlan != null)
                {
                    boundary = ServiceRegistrationPlan;
                    return true;
                }

                if (boundaryKind == SceneKernelBoundaryKind.EntityServiceRoutePlan && EntityServiceRoutePlan != null)
                {
                    boundary = EntityServiceRoutePlan;
                    return true;
                }

                if (boundaryKind == SceneKernelBoundaryKind.RuntimeServiceGraph && RuntimeServiceGraph != null)
                {
                    boundary = RuntimeServiceGraph;
                    return true;
                }

                if (boundaryKind == SceneKernelBoundaryKind.RuntimeSurface && RuntimeSurface != null)
                {
                    boundary = RuntimeSurface;
                    return true;
                }

                if (boundaryKind == SceneKernelBoundaryKind.RuntimeScopeGraph && RuntimeScopeGraph != null)
                {
                    boundary = RuntimeScopeGraph;
                    return true;
                }

                if (boundaryKind == SceneKernelBoundaryKind.SpawnBoundary && SpawnBoundary != null)
                {
                    boundary = SpawnBoundary;
                    return true;
                }

                if (boundaryKind == SceneKernelBoundaryKind.ValueStore && ValueStoreBoundary != null)
                {
                    boundary = ValueStoreBoundary;
                    return true;
                }

                if (boundaryKind == SceneKernelBoundaryKind.LifecycleDispatcher && LifecycleDispatcher != null)
                {
                    boundary = LifecycleDispatcher;
                    return true;
                }

                if (boundaryKind == SceneKernelBoundaryKind.LifecyclePlanResolver && LifecyclePlanResolver != null)
                {
                    boundary = LifecyclePlanResolver;
                    return true;
                }

                boundary = null;
                return false;
            }
        }

        sealed class TestSceneKernelValueStoreBoundary : ISceneKernelValueStoreBoundary
        {
            readonly System.Collections.Generic.Dictionary<EntityRef, IValueStore> stores = new System.Collections.Generic.Dictionary<EntityRef, IValueStore>();
            readonly System.Collections.Generic.Dictionary<int, string> valueInitTargetsByRuntimeInstanceId = new System.Collections.Generic.Dictionary<int, string>();

            public int TryGetCallCount { get; private set; }

            public int ValueInitDispatchCallCount { get; private set; }

            public int LastValueInitRuntimeInstanceId { get; private set; }

            public string LastValueInitTargetStoreRef { get; private set; } = string.Empty;

            public LifecyclePhase LastValueInitPhase { get; private set; }

            public void Bind(EntityRef entityRef, IValueStore valueStore)
            {
                stores[entityRef] = valueStore ?? throw new ArgumentNullException(nameof(valueStore));
            }

            public void BindValueInitHost(int runtimeInstanceId, string targetStoreRef)
            {
                valueInitTargetsByRuntimeInstanceId[runtimeInstanceId] = targetStoreRef;
            }

            public bool TryGetValueStore(EntityRef entityRef, out IValueStore valueStore)
            {
                TryGetCallCount++;
                return stores.TryGetValue(entityRef, out valueStore!);
            }

            public bool TryDispatchValueInit(int scopeRuntimeInstanceId, string targetStoreRef, LifecyclePhase phase, out string failureReason)
            {
                ValueInitDispatchCallCount++;
                LastValueInitRuntimeInstanceId = scopeRuntimeInstanceId;
                LastValueInitTargetStoreRef = targetStoreRef;
                LastValueInitPhase = phase;

                if (!valueInitTargetsByRuntimeInstanceId.TryGetValue(scopeRuntimeInstanceId, out string? registeredTargetStoreRef))
                {
                    failureReason = "Missing value-init host.";
                    return false;
                }

                if (!string.Equals(registeredTargetStoreRef, targetStoreRef, StringComparison.Ordinal))
                {
                    failureReason = "Unexpected target store ref.";
                    return false;
                }

                failureReason = string.Empty;
                return true;
            }
        }

        sealed class TestValueStore : IValueStore
        {
            readonly System.Collections.Generic.Dictionary<ValueKeyId, ValueVariant> values = new System.Collections.Generic.Dictionary<ValueKeyId, ValueVariant>();
            readonly System.Collections.Generic.Dictionary<ValueKeyId, uint> revisions = new System.Collections.Generic.Dictionary<ValueKeyId, uint>();
            readonly System.Collections.Generic.Dictionary<ValueKeyId, ValueKeyMetadata> metadata = new System.Collections.Generic.Dictionary<ValueKeyId, ValueKeyMetadata>();

            public TestValueStore(ValueStoreScopeKind scopeKind)
            {
                ScopeKind = scopeKind;
            }

            public ValueStoreScopeKind ScopeKind { get; }

            public bool TryRead(ValueKeyId keyId, out ValueVariant value)
            {
                return values.TryGetValue(keyId, out value);
            }

            public uint GetRevision(ValueKeyId keyId)
            {
                return revisions.TryGetValue(keyId, out uint revision) ? revision : 0u;
            }

            public bool TryGetMetadata(ValueKeyId keyId, out ValueKeyMetadata valueMetadata)
            {
                return metadata.TryGetValue(keyId, out valueMetadata);
            }

            public bool TryWrite(ValueKeyId keyId, in ValueVariant value)
            {
                values[keyId] = value;
                revisions[keyId] = GetRevision(keyId) + 1u;
                return true;
            }

            public void SetMetadata(ValueKeyMetadata valueMetadata)
            {
                metadata[valueMetadata.KeyId] = valueMetadata;
            }
        }

        sealed class TestKernelBootRuntimeSurface : IKernelBootRuntimeSurface
        {
            public TestKernelBootRuntimeSurface(KernelDebugMap debugMap)
            {
                DebugMap = debugMap ?? throw new ArgumentNullException(nameof(debugMap));
                Diagnostics = new KernelRuntimeDiagnostics(new BootValidationReport(null, null, Array.Empty<BootValidationIssue>()), debugMap);
            }

            public EntityRegistrationPlan? EntityRegistrationPlan => null;

            public ServiceRegistrationPlan? ServiceRegistrationPlan => null;

            public EntityServiceRoutePlan? EntityServiceRoutePlan => null;

            public CommandCatalogPlan? CommandCatalogPlan => null;

            public CommandExecutorTablePlan? CommandExecutorTablePlan => null;

            public KernelRuntimeDiagnostics Diagnostics { get; }

            public KernelDebugMap DebugMap { get; }

            public KernelLifecycleDispatcher? LifecycleDispatcher => null;

            public ILifecyclePlanResolver LifecyclePlanResolver => new KernelLifecyclePlanResolver(Array.Empty<KernelLifecycleDispatcher>());

            public System.Threading.Tasks.Task<LifecycleDispatchResult> DispatchAllLifecycleAsync(IAsyncLifecycleDispatchExecutor executor, System.Threading.CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public System.Threading.Tasks.Task<LifecycleDispatchResult> DispatchPhaseLifecycleAsync(LifecyclePhase phase, IAsyncLifecycleDispatchExecutor executor, System.Threading.CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }
        }

        sealed class StubRuntimeSurfaceFactory : IKernelBootRuntimeSurfaceFactory
        {
            public IKernelBootRuntimeSurface Create(KernelBootBoundaryContext context)
            {
                throw new NotSupportedException();
            }
        }

        sealed class TestSceneKernelSpawnPool : ISceneKernelSpawnPool
        {
            public TestSceneKernelSpawnPool(SceneKernelSpawnPoolId poolId)
            {
                PoolId = poolId;
            }

            public SceneKernelSpawnPoolId PoolId { get; }

            public object? LastFilter { get; private set; }

            public int ReleaseAll(object filter)
            {
                LastFilter = filter;
                return 0;
            }
        }

        sealed class TestSceneKernelSpawnRouteHandler : ISceneKernelSpawnRouteHandler
        {
            readonly object spawnResult;

            public TestSceneKernelSpawnRouteHandler(SceneKernelSpawnRouteId routeId, object spawnResult)
            {
                RouteId = routeId;
                this.spawnResult = spawnResult;
            }

            public SceneKernelSpawnRouteId RouteId { get; }

            public int SpawnCallCount { get; private set; }

            public System.Threading.Tasks.ValueTask<object?> SpawnAsync(object spawnRequest, System.Threading.CancellationToken cancellationToken)
            {
                SpawnCallCount++;
                return new System.Threading.Tasks.ValueTask<object?>(spawnResult);
            }

            public System.Threading.Tasks.ValueTask WarmupAsync(object template, int count, System.Threading.CancellationToken cancellationToken)
            {
                return default;
            }
        }

        sealed class TestSceneKernelServiceLifecycleHandler : ISceneKernelServiceLifecycleHandler
        {
            public TestSceneKernelServiceLifecycleHandler(ServiceId serviceId)
            {
                ServiceId = serviceId;
            }

            public ServiceId ServiceId { get; }

            public int DispatchCount { get; private set; }

            public int RollbackCount { get; private set; }

            public LifecyclePhase LastPhase { get; private set; }

            public KernelRuntimeServiceSlot LastServiceSlot { get; private set; }

            public bool TryDispatch(in SceneKernelServiceLifecycleContext context, out KernelDiagnostic? diagnostic)
            {
                DispatchCount++;
                LastPhase = context.Phase;
                LastServiceSlot = context.ServiceSlot;
                diagnostic = null;
                return true;
            }

            public bool TryRollback(in SceneKernelServiceLifecycleContext context, out KernelDiagnostic? diagnostic)
            {
                RollbackCount++;
                LastPhase = context.Phase;
                LastServiceSlot = context.ServiceSlot;
                diagnostic = null;
                return true;
            }
        }

        sealed class TestSceneKernelServiceFactory : ISceneKernelServiceFactory
        {
            readonly ISceneKernelServiceLifecycleHandler producedHandler;

            public TestSceneKernelServiceFactory(ServiceId serviceId, ISceneKernelServiceLifecycleHandler producedHandler)
            {
                ServiceId = serviceId;
                this.producedHandler = producedHandler ?? throw new ArgumentNullException(nameof(producedHandler));
            }

            public ServiceId ServiceId { get; }

            public int CreateCount { get; private set; }

            public ServiceRegistrationPlanEntry LastActivationRegistration { get; private set; } = null!;

            public bool TryCreate(in SceneKernelServiceActivationContext context, out ISceneKernelServiceLifecycleHandler handler, out KernelDiagnostic? diagnostic)
            {
                CreateCount++;
                LastActivationRegistration = context.Registration;
                handler = producedHandler;
                diagnostic = null;
                return true;
            }
        }
    }
}

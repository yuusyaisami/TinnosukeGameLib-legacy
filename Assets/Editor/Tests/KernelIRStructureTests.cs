using System;
using Game.Kernel.IR;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class KernelIRStructureTests
    {
        [Test]
        public void KernelIRSupportTypes_UseExplicitStableEnumValues()
        {
            Assert.That((int)ServiceLifetimeKind.Kernel, Is.EqualTo(10));
            Assert.That((int)ServiceLifetimeKind.Singleton, Is.EqualTo(10));
            Assert.That((int)ServiceLifetimeKind.Project, Is.EqualTo(20));
            Assert.That((int)ServiceLifetimeKind.Scene, Is.EqualTo(30));
            Assert.That((int)ServiceLifetimeKind.Scope, Is.EqualTo(40));
            Assert.That((int)ServiceLifetimeKind.ExplicitTransient, Is.EqualTo(50));
            Assert.That((int)ServiceFactoryKind.GeneratedFactory, Is.EqualTo(10));
            Assert.That((int)ServiceCardinalityKind.SingletonGlobal, Is.EqualTo(10));
            Assert.That((int)ServiceCardinalityKind.OnePerProject, Is.EqualTo(20));
            Assert.That((int)ServiceCardinalityKind.OnePerScene, Is.EqualTo(30));
            Assert.That((int)ServiceCardinalityKind.OnePerAuthoredScope, Is.EqualTo(40));
            Assert.That((int)ServiceCardinalityKind.BoundedPool, Is.EqualTo(50));
            Assert.That((int)ServiceCardinalityKind.UnboundedRuntime, Is.EqualTo(90));
            Assert.That((int)ScopeKind.Root, Is.EqualTo(10));
            Assert.That((int)ValueKind.Bool, Is.EqualTo(10));
            Assert.That((int)ValueKind.LayeredNumeric, Is.EqualTo(300));
            Assert.That((int)RuntimeQueryTargetKind.Service, Is.EqualTo(10));
            Assert.That((int)LifecyclePhase.Boot, Is.EqualTo(10));
            Assert.That((int)LifecyclePhase.Build, Is.EqualTo(30));
            Assert.That((int)LifecycleTargetKind.Service, Is.EqualTo(10));
            Assert.That((int)LifecycleTargetKind.ValueStore, Is.EqualTo(30));
            Assert.That((int)LifecycleTargetKind.RuntimeQuery, Is.EqualTo(40));
            Assert.That((int)LifecycleActionKind.ServiceMethod, Is.EqualTo(10));
            Assert.That((int)LifecycleActionKind.ValueInit, Is.EqualTo(50));
            Assert.That((int)LifecycleTickCardinalityKind.Hub, Is.EqualTo(10));
            Assert.That((int)LifecycleTickCardinalityKind.PerEntity, Is.EqualTo(20));
            Assert.That((int)LifecycleExecutionModeKind.Synchronous, Is.EqualTo(10));
            Assert.That((int)LifecycleExecutionModeKind.TrackedAsync, Is.EqualTo(20));
            Assert.That((int)LifecycleAsyncCancellationSourceKind.DispatcherOwned, Is.EqualTo(10));
            Assert.That((int)LifecycleAsyncCancellationSourceKind.LinkedToCaller, Is.EqualTo(20));
            Assert.That((int)LifecycleAsyncTimeoutPolicyKind.None, Is.EqualTo(10));
            Assert.That((int)LifecycleAsyncTimeoutPolicyKind.DurationMilliseconds, Is.EqualTo(20));
            Assert.That((int)LifecycleAsyncCompletionRequirementKind.BeforeNextStep, Is.EqualTo(10));
            Assert.That((int)LifecycleAsyncCompletionRequirementKind.BeforePhaseExit, Is.EqualTo(20));
            Assert.That((int)LifecycleFailurePolicy.FailOperation, Is.EqualTo(10));
            Assert.That((int)LifecycleFailurePolicy.FailScope, Is.EqualTo(20));
            Assert.That((int)LifecycleFailurePolicy.FailScene, Is.EqualTo(30));
            Assert.That((int)LifecycleFailurePolicy.FailKernel, Is.EqualTo(40));
            Assert.That((int)LifecycleFailurePolicy.ContinueWithError, Is.EqualTo(50));
            Assert.That((int)DependencyNodeKind.Module, Is.EqualTo(10));
            Assert.That((int)DependencyKind.Requires, Is.EqualTo(10));
            Assert.That((int)DependencyPhase.Build, Is.EqualTo(10));
            Assert.That((int)DependencyStrength.Required, Is.EqualTo(10));
            Assert.That((int)RuntimeCycleMediationKind.None, Is.EqualTo(0));
            Assert.That((int)RuntimeCycleMediationKind.LazyHandle, Is.EqualTo(10));
            Assert.That((int)RuntimeCycleMediationKind.EventChannel, Is.EqualTo(20));
            Assert.That((int)RuntimeCycleMediationKind.RuntimeQuery, Is.EqualTo(30));
            Assert.That((int)LegacyCompatKind.AuthoringMigration, Is.EqualTo(10));
            Assert.That((int)LegacyCompatKind.DataMigration, Is.EqualTo(20));
            Assert.That((int)LegacyCompatKind.RuntimeAdapter, Is.EqualTo(30));
            Assert.That((int)LegacyCompatKind.DiagnosticAdapter, Is.EqualTo(40));
            Assert.That((int)LegacyCompatKind.TestAdapter, Is.EqualTo(50));
            Assert.That((int)LegacyCompatKind.TemporaryBridge, Is.EqualTo(60));
            Assert.That((int)LegacyCompatKind.ForbiddenFallback, Is.EqualTo(90));
            Assert.That((int)LegacyRemovalStatus.Temporary, Is.EqualTo(10));
            Assert.That((int)LegacyRemovalStatus.MigrationOnly, Is.EqualTo(20));
            Assert.That((int)LegacyRemovalStatus.TestOnly, Is.EqualTo(30));
            Assert.That((int)LegacyRemovalStatus.Deprecated, Is.EqualTo(40));
            Assert.That((int)LegacyRemovalStatus.Forbidden, Is.EqualTo(90));
            Assert.That((int)LegacyAdapterSurface.Installer, Is.EqualTo(10));
            Assert.That((int)LegacyAdapterSurface.Resolver, Is.EqualTo(20));
            Assert.That((int)LegacyAdapterSurface.Command, Is.EqualTo(30));
            Assert.That((int)LegacyAdapterSurface.Value, Is.EqualTo(40));
            Assert.That((int)LegacyAdapterSurface.Lifecycle, Is.EqualTo(50));
            Assert.That((int)LegacyAdapterSurface.Authoring, Is.EqualTo(60));
            Assert.That((int)ScopeServiceBoundaryKind.Detached, Is.EqualTo(10));
            Assert.That((int)ScopeServiceBoundaryKind.OwnedLocal, Is.EqualTo(20));
            Assert.That((int)ScopeServiceBoundaryKind.ReferencesParent, Is.EqualTo(30));
        }

        [Test]
        public void LifecycleIR_RejectsInvalidFailurePolicyAndRequiresContinueWithErrorJustification()
        {
            LifecycleStepIR step = new LifecycleStepIR(
                new LifecycleStepId(31),
                LifecyclePhase.Boot,
                10,
                new LifecycleTargetRefIR(new ServiceId(11)),
                LifecycleActionKind.ServiceMethod,
                Array.Empty<DependencyEdgeId>(),
                new SourceLocationId(4));

            ArgumentOutOfRangeException invalidPolicyException = Assert.Throws<ArgumentOutOfRangeException>(() => new LifecycleIR(
                new LifecyclePlanId(21),
                "BattleLifecycle",
                new ModuleId(8),
                new[] { step },
                new SourceLocationId(4),
                (LifecycleFailurePolicy)999));

            Assert.That(invalidPolicyException, Is.Not.Null);

            ArgumentException missingJustificationException = Assert.Throws<ArgumentException>(() => new LifecycleIR(
                new LifecyclePlanId(21),
                "BattleLifecycle",
                new ModuleId(8),
                new[] { step },
                new SourceLocationId(4),
                LifecycleFailurePolicy.ContinueWithError));

            Assert.That(missingJustificationException, Is.Not.Null);

            ArgumentOutOfRangeException invalidRollbackPolicyException = Assert.Throws<ArgumentOutOfRangeException>(() => new LifecycleIR(
                new LifecyclePlanId(21),
                "BattleLifecycle",
                new ModuleId(8),
                new[] { step },
                new SourceLocationId(4),
                LifecycleFailurePolicy.FailScope,
                true,
                KernelProfileMask.None,
                null,
                (LifecycleAcquireRollbackPolicy)999));

            Assert.That(invalidRollbackPolicyException, Is.Not.Null);

            LifecycleIR justifiedLifecycle = new LifecycleIR(
                new LifecyclePlanId(22),
                "BattleLifecycleWithJustification",
                new ModuleId(8),
                new[] { step },
                new SourceLocationId(5),
                LifecycleFailurePolicy.ContinueWithError,
                true,
                KernelProfileMask.Development,
                "Development profile keeps diagnostics flowing during migration.");

            Assert.That(justifiedLifecycle.FailurePolicy, Is.EqualTo(LifecycleFailurePolicy.ContinueWithError));
            Assert.That(justifiedLifecycle.FailurePolicyIsExplicit, Is.True);
            Assert.That(justifiedLifecycle.FailurePolicyJustificationProfiles, Is.EqualTo(KernelProfileMask.Development));
            Assert.That(justifiedLifecycle.FailurePolicyJustification, Is.Not.Null.And.Not.Empty);
            Assert.That(justifiedLifecycle.AcquireRollbackPolicy, Is.EqualTo(LifecycleAcquireRollbackPolicy.ReverseCompletedAcquireSteps));
        }

        [Test]
        public void LifecycleStepIR_RequiresExplicitTrackedAsyncPolicy_AndValidatesAsyncPolicyShape()
        {
            ArgumentException missingPolicyException = Assert.Throws<ArgumentException>(() => new LifecycleStepIR(
                new LifecycleStepId(41),
                LifecyclePhase.Boot,
                10,
                new LifecycleTargetRefIR(new ServiceId(11)),
                LifecycleActionKind.ServiceMethod,
                Array.Empty<DependencyEdgeId>(),
                new SourceLocationId(4),
                LifecycleTickCardinalityKind.Unknown,
                LifecycleExecutionModeKind.TrackedAsync));

            Assert.That(missingPolicyException, Is.Not.Null);

            ArgumentException acquireAsyncException = Assert.Throws<ArgumentException>(() => new LifecycleStepIR(
                new LifecycleStepId(42),
                LifecyclePhase.Acquire,
                20,
                new LifecycleTargetRefIR(new ScopePlanId(12)),
                LifecycleActionKind.ScopeStateTransition,
                Array.Empty<DependencyEdgeId>(),
                new SourceLocationId(5),
                LifecycleTickCardinalityKind.Unknown,
                LifecycleExecutionModeKind.TrackedAsync,
                new LifecycleAsyncPolicyIR(
                    LifecycleAsyncCancellationSourceKind.DispatcherOwned,
                    LifecycleAsyncTimeoutPolicyKind.None,
                    0,
                    LifecycleAsyncCompletionRequirementKind.BeforePhaseExit,
                    waitForNextStep: false)));

            Assert.That(acquireAsyncException, Is.Not.Null);

            Assert.That(() => new LifecycleAsyncPolicyIR(
                LifecycleAsyncCancellationSourceKind.DispatcherOwned,
                LifecycleAsyncTimeoutPolicyKind.None,
                5,
                LifecycleAsyncCompletionRequirementKind.BeforeNextStep,
                waitForNextStep: true), Throws.ArgumentException);

            Assert.That(() => new LifecycleAsyncPolicyIR(
                LifecycleAsyncCancellationSourceKind.DispatcherOwned,
                LifecycleAsyncTimeoutPolicyKind.DurationMilliseconds,
                0,
                LifecycleAsyncCompletionRequirementKind.BeforeNextStep,
                waitForNextStep: true), Throws.ArgumentException);

            LifecycleAsyncPolicyIR validAsyncPolicy = new LifecycleAsyncPolicyIR(
                LifecycleAsyncCancellationSourceKind.LinkedToCaller,
                LifecycleAsyncTimeoutPolicyKind.DurationMilliseconds,
                25,
                LifecycleAsyncCompletionRequirementKind.BeforeNextStep,
                waitForNextStep: true);

            LifecycleStepIR asyncStep = new LifecycleStepIR(
                new LifecycleStepId(43),
                LifecyclePhase.Boot,
                30,
                new LifecycleTargetRefIR(new ServiceId(13)),
                LifecycleActionKind.ServiceMethod,
                Array.Empty<DependencyEdgeId>(),
                new SourceLocationId(6),
                LifecycleTickCardinalityKind.Unknown,
                LifecycleExecutionModeKind.TrackedAsync,
                validAsyncPolicy);

            Assert.That(asyncStep.ExecutionMode, Is.EqualTo(LifecycleExecutionModeKind.TrackedAsync));
            Assert.That(asyncStep.AsyncPolicy, Is.SameAs(validAsyncPolicy));
        }

        [Test]
        public void KernelIR_RejectsLifecycleLocalOwnerTargets_UntilLowerSpecSupportExists()
        {
            SourceLocationTable sources = new SourceLocationTable(new[]
            {
                new SourceLocationIR(new GeneratedSourceLocation("KernelIRStructureTests", "MinimalKernel", "Build")),
                new SourceLocationIR(new GeneratedSourceLocation("KernelIRStructureTests", "MinimalKernel", "Lifecycle")),
            });

            ModuleIR module = new ModuleIR(
                new ModuleId(1),
                "MinimalKernel",
                ModuleKind.Feature,
                new ModuleVersion(1),
                new ModuleAvailabilityIR(new AvailabilityIR(KernelProfileMask.Release, true, null)),
                new SourceLocationId(1));

            LifecycleIR lifecycle = new LifecycleIR(
                new LifecyclePlanId(21),
                "LegacyLifecycle",
                new ModuleId(1),
                new[]
                {
                    new LifecycleStepIR(
                        new LifecycleStepId(31),
                        LifecyclePhase.Boot,
                        10,
                        new LifecycleTargetRefIR(LifecycleTargetKind.LegacyAdapter, "legacy-bridge"),
                        LifecycleActionKind.LegacyAdapterCall,
                        Array.Empty<DependencyEdgeId>(),
                        new SourceLocationId(2)),
                },
                new SourceLocationId(2),
                LifecycleFailurePolicy.FailScope);

            Assert.That(() => new KernelIR(
                new KernelIRHeader("KernelIR-Minimal", 1, "TinnosukeGameLib", "Release", "1.0.0", new Hash128(1, 2, 3, 4), new Hash128(5, 6, 7, 8)),
                new KernelProfileIR("Release", KernelProfileMask.Release, new AvailabilityIR(KernelProfileMask.Release, true, null)),
                new[] { module },
                Array.Empty<ScopeIR>(),
                Array.Empty<ServiceIR>(),
                Array.Empty<CommandIR>(),
                Array.Empty<ValueKeyIR>(),
                new[] { lifecycle },
                Array.Empty<RuntimeQueryIR>(),
                Array.Empty<DependencyEdgeIR>(),
                sources), Throws.ArgumentException);
        }

        [Test]
        public void SourceLocationTable_RejectsDuplicatesAndResolvesBySourceLocationId()
        {
            SourceLocationIR first = CreateGeneratedSourceLocation("GeneratorA", "ModuleA");
            SourceLocationIR second = CreateGeneratedSourceLocation("GeneratorB", "ModuleB");
            SourceLocationTable table = new SourceLocationTable(new[] { first, second });

            Assert.That(table.Count, Is.EqualTo(2));
            Assert.That(table.GetSource(new SourceLocationId(1)), Is.EqualTo(first));
            Assert.That(table.GetSource(new SourceLocationId(2)), Is.EqualTo(second));
            Assert.That(table.TryGetSource(new SourceLocationId(3), out _), Is.False);

            ArgumentException duplicateException = Assert.Throws<ArgumentException>(() => new SourceLocationTable(new[] { first, first }));
            Assert.That(duplicateException, Is.Not.Null);
            Assert.That(duplicateException!.Message, Does.Contain("duplicate source locations"));
        }

        [Test]
        public void DependencyNodeIR_RejectsMismatchedEndpoints()
        {
            DependencyNodeIR serviceNode = new DependencyNodeIR(new ServiceId(9));

            Assert.That(serviceNode.Kind, Is.EqualTo(DependencyNodeKind.Service));
            Assert.That(serviceNode.ServiceId, Is.EqualTo(new ServiceId(9)));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => new DependencyNodeIR(default(ServiceId)));
            Assert.That(exception, Is.Not.Null);
        }

        [Test]
        public void DependencyEdgeIR_RejectsRuntimeCycleMediationOutsideRuntimePhase()
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(() => new DependencyEdgeIR(
                new DependencyEdgeId(1),
                new DependencyNodeIR(new ServiceId(11)),
                new DependencyNodeIR(new ServiceId(12)),
                DependencyKind.Requires,
                DependencyPhase.Build,
                DependencyStrength.Required,
                new SourceLocationId(1),
                RuntimeCycleMediationKind.LazyHandle));

            Assert.That(exception, Is.Not.Null);
            Assert.That(exception!.Message, Does.Contain("Runtime cycle mediation metadata"));
        }

        [Test]
        public void ScopeIR_PreservesExplicitServiceBoundary()
        {
            ScopeIR ownedScope = new ScopeIR(
                new ScopeAuthoringId(1),
                new ScopePlanId(101),
                "BattleScope",
                ScopeKind.Root,
                new ModuleId(8),
                default,
                new[] { new ScopeServiceRequirementIR(new ServiceId(11), DependencyStrength.Required, new SourceLocationId(3)) },
                Array.Empty<ScopeValueInitRefIR>(),
                new ScopeServiceBoundaryIR(ScopeServiceBoundaryKind.OwnedLocal, 1, new SourceLocationId(3)),
                new LifecyclePlanRefIR(new LifecyclePlanId(21), new SourceLocationId(4)),
                new SourceLocationId(3));

            ScopeIR detachedScope = new ScopeIR(
                new ScopeAuthoringId(2),
                new ScopePlanId(102),
                "DetachedScope",
                ScopeKind.Detached,
                new ModuleId(8),
                default,
                Array.Empty<ScopeServiceRequirementIR>(),
                Array.Empty<ScopeValueInitRefIR>(),
                new ScopeServiceBoundaryIR(ScopeServiceBoundaryKind.Detached, 0, new SourceLocationId(5)),
                new LifecyclePlanRefIR(new LifecyclePlanId(22), new SourceLocationId(5)),
                new SourceLocationId(5));

            Assert.That(ownedScope.ServiceBoundary.Kind, Is.EqualTo(ScopeServiceBoundaryKind.OwnedLocal));
            Assert.That(ownedScope.ServiceBoundary.ExpectedInstanceCount, Is.EqualTo(1));
            Assert.That(detachedScope.ServiceBoundary.Kind, Is.EqualTo(ScopeServiceBoundaryKind.Detached));
            Assert.That(detachedScope.ServiceBoundary.ExpectedInstanceCount, Is.EqualTo(0));
        }

        [Test]
        public void ScopeIR_PreservesExplicitUnityObjectLinkMetadata()
        {
            UnityObjectLinkIR unityObjectLink = new UnityObjectLinkIR(
                "Scene",
                "scene-guid-1",
                101,
                "BattleScopeAuthoring",
                new SourceLocationId(7));

            ScopeIR scope = new ScopeIR(
                new ScopeAuthoringId(3),
                new ScopePlanId(103),
                "LinkedScope",
                ScopeKind.Root,
                new ModuleId(8),
                default,
                Array.Empty<ScopeServiceRequirementIR>(),
                Array.Empty<ScopeValueInitRefIR>(),
                new ScopeServiceBoundaryIR(ScopeServiceBoundaryKind.Detached, 0, new SourceLocationId(7)),
                new LifecyclePlanRefIR(new LifecyclePlanId(23), new SourceLocationId(8)),
                new SourceLocationId(7),
                unityObjectLink);

            Assert.That(scope.UnityObjectLink, Is.Not.Null);
            Assert.That(scope.UnityObjectLink!.Kind, Is.EqualTo("Scene"));
            Assert.That(scope.UnityObjectLink.SourceGuid, Is.EqualTo("scene-guid-1"));
            Assert.That(scope.UnityObjectLink.LocalFileId, Is.EqualTo(101));
            Assert.That(scope.UnityObjectLink.DebugName, Is.EqualTo("BattleScopeAuthoring"));
            Assert.That(scope.UnityObjectLink.Source, Is.EqualTo(new SourceLocationId(7)));
        }

        [Test]
        public void ScopeIR_RejectsDetachedBoundaryWhenRequiredServicesExist()
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(() => new ScopeIR(
                new ScopeAuthoringId(3),
                new ScopePlanId(103),
                "InvalidScope",
                ScopeKind.Root,
                new ModuleId(8),
                default,
                new[] { new ScopeServiceRequirementIR(new ServiceId(13), DependencyStrength.Required, new SourceLocationId(6)) },
                Array.Empty<ScopeValueInitRefIR>(),
                new ScopeServiceBoundaryIR(ScopeServiceBoundaryKind.Detached, 0, new SourceLocationId(6)),
                new LifecyclePlanRefIR(new LifecyclePlanId(23), new SourceLocationId(6)),
                new SourceLocationId(6)));

            Assert.That(exception, Is.Not.Null);
            Assert.That(exception!.Message, Does.Contain("local service boundary"));
        }

        [Test]
        public void ServiceIR_RejectsUnsupportedCardinalityAndTransientLifetime()
        {
            ArgumentException cardinalityException = Assert.Throws<ArgumentException>(() => new ServiceIR(
                new ServiceId(11),
                "BattleService",
                ServiceLifetimeKind.Singleton,
                new ModuleId(8),
                new[] { new ServiceContractIR("IBattleService", new SourceLocationId(2)) },
                Array.Empty<ServiceDependencyIR>(),
                ServiceFactoryKind.GeneratedFactory,
                new SourceLocationId(2),
                ServiceCardinalityKind.BoundedPool));

            Assert.That(cardinalityException, Is.Not.Null);
            Assert.That(cardinalityException!.ParamName, Is.EqualTo("cardinality"));

            ArgumentException transientException = Assert.Throws<ArgumentException>(() => new ServiceIR(
                new ServiceId(12),
                "TemporaryBattleService",
                ServiceLifetimeKind.ExplicitTransient,
                new ModuleId(8),
                new[] { new ServiceContractIR("ITemporaryBattleService", new SourceLocationId(3)) },
                Array.Empty<ServiceDependencyIR>(),
                ServiceFactoryKind.GeneratedFactory,
                new SourceLocationId(3)));

            Assert.That(transientException, Is.Not.Null);
            Assert.That(transientException!.ParamName, Is.EqualTo("lifetime"));
        }

        [Test]
        public void ModuleIR_PreservesOwnershipAvailabilityAndSource()
        {
            ModuleIR module = new ModuleIR(
                new ModuleId(5),
                "BattleModule",
                ModuleKind.Feature,
                new ModuleVersion(1),
                new ModuleAvailabilityIR(new AvailabilityIR(KernelProfileMask.Release, true, null)),
                new SourceLocationId(1),
                new[] { new ModuleDependencyIR(new ModuleId(2), new SourceLocationId(1)) },
                new[] { new ModuleDependencyIR(new ModuleId(3), new SourceLocationId(1)) });

            Assert.That(module.Id, Is.EqualTo(new ModuleId(5)));
            Assert.That(module.Name, Is.EqualTo("BattleModule"));
            Assert.That(module.Kind, Is.EqualTo(ModuleKind.Feature));
            Assert.That(module.Version, Is.EqualTo(new ModuleVersion(1)));
            Assert.That(module.Source, Is.EqualTo(new SourceLocationId(1)));
            Assert.That(module.RequiredModules.Length, Is.EqualTo(1));
            Assert.That(module.OptionalModules.Length, Is.EqualTo(1));
        }

        [Test]
        public void ModuleIR_PreservesLegacyCompatDescriptor()
        {
            LegacyCompatDescriptorIR legacyCompat = new LegacyCompatDescriptorIR(
                LegacyCompatKind.RuntimeAdapter,
                "LegacySystem",
                "ServiceGraph",
                KernelProfileMask.Development | KernelProfileMask.Test,
                LegacyRemovalStatus.Temporary,
                "LEGACY_RUNTIME_ADAPTER_USED",
                "Remove after migration",
                "TICKET-1",
                surface: LegacyAdapterSurface.Resolver,
                legacySourceType: "RuntimeResolverHub",
                explicitTargets: new[] { new DependencyNodeIR(new ServiceId(201)) });

            ModuleIR module = new ModuleIR(
                new ModuleId(5),
                "BattleLegacyAdapter",
                ModuleKind.MigrationAdapter,
                new ModuleVersion(1),
                new ModuleAvailabilityIR(new AvailabilityIR(KernelProfileMask.Development, true, null)),
                new SourceLocationId(1),
                legacyCompat: legacyCompat);

            Assert.That(module.LegacyCompat, Is.Not.Null);
            Assert.That(module.LegacyCompat!.Kind, Is.EqualTo(LegacyCompatKind.RuntimeAdapter));
            Assert.That(module.LegacyCompat.LegacySystemName, Is.EqualTo("LegacySystem"));
            Assert.That(module.LegacyCompat.DiagnosticsCode, Is.EqualTo("LEGACY_RUNTIME_ADAPTER_USED"));
            Assert.That(module.LegacyCompat.RemovalCondition, Is.EqualTo("Remove after migration"));
            Assert.That(module.LegacyCompat.TrackingIssueOrBlockingCondition, Is.EqualTo("TICKET-1"));
            Assert.That(module.LegacyCompat.Surface, Is.EqualTo(LegacyAdapterSurface.Resolver));
            Assert.That(module.LegacyCompat.LegacySourceType, Is.EqualTo("RuntimeResolverHub"));
            Assert.That(module.LegacyCompat.ExplicitTargets.Length, Is.EqualTo(1));
            Assert.That(module.LegacyCompat.ExplicitTargets[0], Is.EqualTo(new DependencyNodeIR(new ServiceId(201))));
        }

        [Test]
        public void KernelIR_ValidatesSourceCoverageOwnershipAndDependencyCoverage()
        {
            SourceLocationTable sources = new SourceLocationTable(new[]
            {
                CreateGeneratedSourceLocation("ModuleProjector", "BattleModule"),
                CreateGeneratedSourceLocation("ServiceProjector", "BattleService"),
                CreateGeneratedSourceLocation("ScopeProjector", "BattleScope"),
                CreateGeneratedSourceLocation("LifecycleProjector", "BattleLifecycle"),
                CreateGeneratedSourceLocation("CommandProjector", "BattleCommand"),
                CreateGeneratedSourceLocation("ValueProjector", "BattleValue"),
                CreateGeneratedSourceLocation("QueryProjector", "BattleQuery"),
                CreateGeneratedSourceLocation("DependencyProjector", "BattleDependency"),
            });
            ModuleIR secondaryModule = new ModuleIR(
                new ModuleId(2),
                "SharedModule",
                ModuleKind.System,
                new ModuleVersion(1),
                new ModuleAvailabilityIR(new AvailabilityIR(KernelProfileMask.Release, true, null)),
                new SourceLocationId(1));
            ModuleIR primaryModule = new ModuleIR(
                new ModuleId(8),
                "BattleModule",
                ModuleKind.Feature,
                new ModuleVersion(1),
                new ModuleAvailabilityIR(new AvailabilityIR(KernelProfileMask.Release, true, null)),
                new SourceLocationId(1),
                new[] { new ModuleDependencyIR(new ModuleId(2), new SourceLocationId(1)) },
                Array.Empty<ModuleDependencyIR>());
            ServiceIR service = new ServiceIR(
                new ServiceId(11),
                "BattleService",
                ServiceLifetimeKind.Singleton,
                new ModuleId(8),
                new[] { new ServiceContractIR("IBattleService", new SourceLocationId(2)) },
                Array.Empty<ServiceDependencyIR>(),
                ServiceFactoryKind.GeneratedFactory,
                new SourceLocationId(2));
            ScopeIR scope = new ScopeIR(
                new ScopeAuthoringId(1),
                new ScopePlanId(101),
                "BattleScope",
                ScopeKind.Root,
                new ModuleId(8),
                default,
                new[] { new ScopeServiceRequirementIR(new ServiceId(11), DependencyStrength.Required, new SourceLocationId(3)) },
                new[] { new ScopeValueInitRefIR(new ValueInitPlanId(7), new SourceLocationId(3)) },
                new ScopeServiceBoundaryIR(ScopeServiceBoundaryKind.OwnedLocal, 1, new SourceLocationId(3)),
                new LifecyclePlanRefIR(new LifecyclePlanId(21), new SourceLocationId(4)),
                new SourceLocationId(3));
            DependencyEdgeIR edge = new DependencyEdgeIR(
                new DependencyEdgeId(1),
                new DependencyNodeIR(new ScopePlanId(101)),
                new DependencyNodeIR(new ServiceId(11)),
                DependencyKind.Requires,
                DependencyPhase.Boot,
                DependencyStrength.Required,
                new SourceLocationId(8));
            LifecycleIR lifecycle = new LifecycleIR(
                new LifecyclePlanId(21),
                "BattleLifecycle",
                new ModuleId(8),
                new[]
                {
                        new LifecycleStepIR(
                            new LifecycleStepId(31),
                            LifecyclePhase.Boot,
                            10,
                            new LifecycleTargetRefIR(new ServiceId(11)),
                            LifecycleActionKind.ServiceMethod,
                            new[] { new DependencyEdgeId(1) },
                            new SourceLocationId(4)),
                },
                new SourceLocationId(4),
                LifecycleFailurePolicy.FailScope);

            Assert.That(lifecycle.FailurePolicy, Is.EqualTo(LifecycleFailurePolicy.FailScope));
            CommandIR command = new CommandIR(
                new CommandTypeId(12),
                "BattleCommand",
                new CommandAuthoringKeyRefIR(new CommandAuthoringKeyId(6), "battle.command", new SourceLocationId(5)),
                new CommandCategoryId(3),
                new ModuleId(8),
                new CommandPayloadSchemaRefIR(new CommandPayloadSchemaId(4), new SourceLocationId(5)),
                new CommandExecutorRefIR(new CommandExecutorId(5), new SourceLocationId(5)),
                new[] { new CommandDependencyIR(new DependencyNodeIR(new ServiceId(11)), DependencyStrength.Required, new SourceLocationId(5)) },
                new SourceLocationId(5));
            ValueKeyIR valueKey = new ValueKeyIR(
                new ValueKeyId(13),
                "battle.health",
                "Battle Health",
                ValueKind.LayeredNumeric,
                new ModuleId(8),
                new ValueSchemaRefIR(new ValueSchemaId(14), new SourceLocationId(6)),
                new SavePolicyIR(true, false, "Profile"),
                new SourceLocationId(6));
            RuntimeQueryIR runtimeQuery = new RuntimeQueryIR(
                new RuntimeQueryId(15),
                "BattleServiceQuery",
                RuntimeQueryTargetKind.Service,
                new[] { new RuntimeIdentityFieldIR("ServiceId", "ServiceId", true) },
                new RuntimeQueryPolicyIR(true, false, DependencyPhase.Runtime),
                new ModuleId(8),
                new SourceLocationId(7));

            KernelIR ir = new KernelIR(
                new KernelIRHeader("KernelIR-Battle", 1, "TinnosukeGameLib", "Battle", "1.0.0", new Hash128(1, 2, 3, 4), new Hash128(5, 6, 7, 8)),
                new KernelProfileIR("Battle", KernelProfileMask.Release, new AvailabilityIR(KernelProfileMask.Release, true, null)),
                new[] { primaryModule, secondaryModule },
                new[] { scope },
                new[] { service },
                new[] { command },
                new[] { valueKey },
                new[] { lifecycle },
                new[] { runtimeQuery },
                new[] { edge },
                sources,
                new[] { new DiagnosticSeedIR("MISSING_DEPENDENCY", "Missing Dependency", new ModuleId(8), new SourceLocationId(8)) });

            Assert.That(ir.Modules.Length, Is.EqualTo(2));
            Assert.That(ir.Modules[0].Id, Is.EqualTo(new ModuleId(2)));
            Assert.That(ir.Modules[1].Id, Is.EqualTo(new ModuleId(8)));
            Assert.That(ir.Dependencies.Length, Is.EqualTo(1));
            Assert.That(ir.DiagnosticSeeds.Length, Is.EqualTo(1));
            Assert.That(ir.Sources.GetSource(new SourceLocationId(1)).GeneratedSource!.Value.GeneratorName, Is.EqualTo("ModuleProjector"));

            ArgumentException missingSourceException = Assert.Throws<ArgumentException>(() => new KernelIR(
                new KernelIRHeader("KernelIR-Battle", 1, "TinnosukeGameLib", "Battle", "1.0.0", default, default),
                new KernelProfileIR("Battle", KernelProfileMask.Release, new AvailabilityIR(KernelProfileMask.Release, true, null)),
                new[]
                {
                    new ModuleIR(new ModuleId(8), "BattleModule", ModuleKind.Feature, new ModuleVersion(1), new ModuleAvailabilityIR(new AvailabilityIR(KernelProfileMask.Release, true, null)), new SourceLocationId(9))
                },
                Array.Empty<ScopeIR>(),
                Array.Empty<ServiceIR>(),
                Array.Empty<CommandIR>(),
                Array.Empty<ValueKeyIR>(),
                Array.Empty<LifecycleIR>(),
                Array.Empty<RuntimeQueryIR>(),
                Array.Empty<DependencyEdgeIR>(),
                sources));

            Assert.That(missingSourceException, Is.Not.Null);
            Assert.That(missingSourceException!.Message, Does.Contain("SourceLocationTable"));
        }

        [Test]
        public void KernelIR_RejectsDuplicateDiagnosticSeeds()
        {
            SourceLocationTable sources = new SourceLocationTable(new[]
            {
                CreateGeneratedSourceLocation("ModuleProjector", "BattleModule"),
                CreateGeneratedSourceLocation("ServiceProjector", "BattleService"),
                CreateGeneratedSourceLocation("ScopeProjector", "BattleScope"),
                CreateGeneratedSourceLocation("LifecycleProjector", "BattleLifecycle"),
            });

            ArgumentException duplicateSeedException = Assert.Throws<ArgumentException>(() => new KernelIR(
                new KernelIRHeader("KernelIR-Battle", 1, "TinnosukeGameLib", "Battle", "1.0.0", default, default),
                new KernelProfileIR("Battle", KernelProfileMask.Release, new AvailabilityIR(KernelProfileMask.Release, true, null)),
                new[]
                {
                    new ModuleIR(new ModuleId(8), "BattleModule", ModuleKind.Feature, new ModuleVersion(1), new ModuleAvailabilityIR(new AvailabilityIR(KernelProfileMask.Release, true, null)), new SourceLocationId(1)),
                },
                Array.Empty<ScopeIR>(),
                Array.Empty<ServiceIR>(),
                Array.Empty<CommandIR>(),
                Array.Empty<ValueKeyIR>(),
                Array.Empty<LifecycleIR>(),
                Array.Empty<RuntimeQueryIR>(),
                Array.Empty<DependencyEdgeIR>(),
                sources,
                new[]
                {
                    new DiagnosticSeedIR("MISSING_DEPENDENCY", "Missing Dependency", new ModuleId(8), new SourceLocationId(1)),
                    new DiagnosticSeedIR("MISSING_DEPENDENCY", "Duplicate Dependency", new ModuleId(8), new SourceLocationId(2)),
                }));

            Assert.That(duplicateSeedException, Is.Not.Null);
            Assert.That(duplicateSeedException!.ParamName, Is.EqualTo("DiagnosticSeeds"));
        }

        [Test]
        public void KernelIR_RejectsNestedInvalidSourcesAndDuplicateModuleIds()
        {
            SourceLocationTable sources = new SourceLocationTable(new[]
            {
                CreateGeneratedSourceLocation("ModuleProjector", "BattleModule"),
                CreateGeneratedSourceLocation("ServiceProjector", "BattleService"),
                CreateGeneratedSourceLocation("ScopeProjector", "BattleScope"),
                CreateGeneratedSourceLocation("LifecycleProjector", "BattleLifecycle"),
            });

            ArgumentException nestedSourceException = Assert.Throws<ArgumentException>(() => new KernelIR(
                new KernelIRHeader("KernelIR-Battle", 1, "TinnosukeGameLib", "Battle", "1.0.0", default, default),
                new KernelProfileIR("Battle", KernelProfileMask.Release, new AvailabilityIR(KernelProfileMask.Release, true, null)),
                new[]
                {
                    new ModuleIR(
                        new ModuleId(8),
                        "BattleModule",
                        ModuleKind.Feature,
                        new ModuleVersion(1),
                        new ModuleAvailabilityIR(new AvailabilityIR(KernelProfileMask.Release, true, null)),
                        new SourceLocationId(1),
                        new[] { new ModuleDependencyIR(new ModuleId(9), new SourceLocationId(9)) })
                },
                Array.Empty<ScopeIR>(),
                Array.Empty<ServiceIR>(),
                Array.Empty<CommandIR>(),
                Array.Empty<ValueKeyIR>(),
                Array.Empty<LifecycleIR>(),
                Array.Empty<RuntimeQueryIR>(),
                Array.Empty<DependencyEdgeIR>(),
                sources));

            Assert.That(nestedSourceException, Is.Not.Null);
            Assert.That(nestedSourceException!.Message, Does.Contain("SourceLocationTable"));

            ArgumentException duplicateModuleException = Assert.Throws<ArgumentException>(() => new KernelIR(
                new KernelIRHeader("KernelIR-Battle", 1, "TinnosukeGameLib", "Battle", "1.0.0", default, default),
                new KernelProfileIR("Battle", KernelProfileMask.Release, new AvailabilityIR(KernelProfileMask.Release, true, null)),
                new[]
                {
                    new ModuleIR(new ModuleId(8), "BattleModule", ModuleKind.Feature, new ModuleVersion(1), new ModuleAvailabilityIR(new AvailabilityIR(KernelProfileMask.Release, true, null)), new SourceLocationId(1)),
                    new ModuleIR(new ModuleId(8), "BattleModuleDuplicate", ModuleKind.Feature, new ModuleVersion(1), new ModuleAvailabilityIR(new AvailabilityIR(KernelProfileMask.Release, true, null)), new SourceLocationId(1)),
                },
                Array.Empty<ScopeIR>(),
                Array.Empty<ServiceIR>(),
                Array.Empty<CommandIR>(),
                Array.Empty<ValueKeyIR>(),
                Array.Empty<LifecycleIR>(),
                Array.Empty<RuntimeQueryIR>(),
                Array.Empty<DependencyEdgeIR>(),
                sources));

            Assert.That(duplicateModuleException, Is.Not.Null);
            Assert.That(duplicateModuleException!.Message, Does.Contain("unique module identities"));
        }

        [Test]
        public void KernelIR_RejectsReferencesToMissingOwnedTargets()
        {
            SourceLocationTable sources = new SourceLocationTable(new[]
            {
                CreateGeneratedSourceLocation("ModuleProjector", "BattleModule"),
                CreateGeneratedSourceLocation("ScopeProjector", "BattleScope"),
                CreateGeneratedSourceLocation("LifecycleProjector", "BattleLifecycle"),
                CreateGeneratedSourceLocation("StepProjector", "BattleStep"),
            });

            ModuleIR module = new ModuleIR(
                new ModuleId(8),
                "BattleModule",
                ModuleKind.Feature,
                new ModuleVersion(1),
                new ModuleAvailabilityIR(new AvailabilityIR(KernelProfileMask.Release, true, null)),
                new SourceLocationId(1));
            ScopeIR scope = new ScopeIR(
                new ScopeAuthoringId(1),
                new ScopePlanId(101),
                "BattleScope",
                ScopeKind.Root,
                new ModuleId(8),
                default,
                new[] { new ScopeServiceRequirementIR(new ServiceId(77), DependencyStrength.Required, new SourceLocationId(2)) },
                Array.Empty<ScopeValueInitRefIR>(),
                new ScopeServiceBoundaryIR(ScopeServiceBoundaryKind.OwnedLocal, 1, new SourceLocationId(2)),
                new LifecyclePlanRefIR(new LifecyclePlanId(21), new SourceLocationId(3)),
                new SourceLocationId(2));
            LifecycleIR lifecycle = new LifecycleIR(
                new LifecyclePlanId(21),
                "BattleLifecycle",
                new ModuleId(8),
                new[]
                {
                    new LifecycleStepIR(
                        new LifecycleStepId(31),
                        LifecyclePhase.Boot,
                        10,
                        new LifecycleTargetRefIR(new ServiceId(77)),
                        LifecycleActionKind.ServiceMethod,
                        Array.Empty<DependencyEdgeId>(),
                        new SourceLocationId(4)),
                },
                    new SourceLocationId(3),
                    LifecycleFailurePolicy.FailKernel);

            ArgumentException missingReferenceException = Assert.Throws<ArgumentException>(() => new KernelIR(
                new KernelIRHeader("KernelIR-Battle", 1, "TinnosukeGameLib", "Battle", "1.0.0", default, default),
                new KernelProfileIR("Battle", KernelProfileMask.Release, new AvailabilityIR(KernelProfileMask.Release, true, null)),
                new[] { module },
                new[] { scope },
                Array.Empty<ServiceIR>(),
                Array.Empty<CommandIR>(),
                Array.Empty<ValueKeyIR>(),
                new[] { lifecycle },
                Array.Empty<RuntimeQueryIR>(),
                Array.Empty<DependencyEdgeIR>(),
                sources));

            Assert.That(missingReferenceException, Is.Not.Null);
            Assert.That(missingReferenceException!.Message, Does.Contain("reference"));
        }

        [Test]
        public void KernelIR_RejectsDuplicateCommandAuthoringKeyIdentities()
        {
            SourceLocationTable sources = new SourceLocationTable(new[]
            {
                CreateGeneratedSourceLocation("ModuleProjector", "BattleModule"),
                CreateGeneratedSourceLocation("CommandProjector", "BattleCommandA"),
                CreateGeneratedSourceLocation("CommandProjector", "BattleCommandB"),
            });

            ModuleIR module = new ModuleIR(
                new ModuleId(8),
                "BattleModule",
                ModuleKind.Feature,
                new ModuleVersion(1),
                new ModuleAvailabilityIR(new AvailabilityIR(KernelProfileMask.Release, true, null)),
                new SourceLocationId(1));

            CommandIR[] commands =
            {
                new CommandIR(
                    new CommandTypeId(12),
                    "BattleCommandA",
                    new CommandAuthoringKeyRefIR(new CommandAuthoringKeyId(500), "battle.command.a", new SourceLocationId(2)),
                    new CommandCategoryId(3),
                    new ModuleId(8),
                    new CommandPayloadSchemaRefIR(new CommandPayloadSchemaId(4), new SourceLocationId(2)),
                    new CommandExecutorRefIR(new CommandExecutorId(5), new SourceLocationId(2)),
                    Array.Empty<CommandDependencyIR>(),
                    new SourceLocationId(2)),
                new CommandIR(
                    new CommandTypeId(13),
                    "BattleCommandB",
                    new CommandAuthoringKeyRefIR(new CommandAuthoringKeyId(500), "battle.command.b", new SourceLocationId(3)),
                    new CommandCategoryId(3),
                    new ModuleId(8),
                    new CommandPayloadSchemaRefIR(new CommandPayloadSchemaId(6), new SourceLocationId(3)),
                    new CommandExecutorRefIR(new CommandExecutorId(7), new SourceLocationId(3)),
                    Array.Empty<CommandDependencyIR>(),
                    new SourceLocationId(3)),
            };

            ArgumentException duplicateAuthoringIdException = Assert.Throws<ArgumentException>(() => new KernelIR(
                new KernelIRHeader("KernelIR-Battle", 1, "TinnosukeGameLib", "Battle", "1.0.0", default, default),
                new KernelProfileIR("Battle", KernelProfileMask.Release, new AvailabilityIR(KernelProfileMask.Release, true, null)),
                new[] { module },
                Array.Empty<ScopeIR>(),
                Array.Empty<ServiceIR>(),
                commands,
                Array.Empty<ValueKeyIR>(),
                Array.Empty<LifecycleIR>(),
                Array.Empty<RuntimeQueryIR>(),
                Array.Empty<DependencyEdgeIR>(),
                sources));

            Assert.That(duplicateAuthoringIdException, Is.Not.Null);
            Assert.That(duplicateAuthoringIdException!.Message, Does.Contain("unique command authoring key identities"));
        }

        [Test]
        public void KernelIR_RejectsDuplicateNormalizedCommandAuthoringKeys()
        {
            SourceLocationTable sources = new SourceLocationTable(new[]
            {
                CreateGeneratedSourceLocation("ModuleProjector", "BattleModule"),
                CreateGeneratedSourceLocation("CommandProjector", "BattleCommandA"),
                CreateGeneratedSourceLocation("CommandProjector", "BattleCommandB"),
            });

            ModuleIR module = new ModuleIR(
                new ModuleId(8),
                "BattleModule",
                ModuleKind.Feature,
                new ModuleVersion(1),
                new ModuleAvailabilityIR(new AvailabilityIR(KernelProfileMask.Release, true, null)),
                new SourceLocationId(1));

            CommandIR[] commands =
            {
                new CommandIR(
                    new CommandTypeId(12),
                    "BattleCommandA",
                    new CommandAuthoringKeyRefIR(new CommandAuthoringKeyId(500), "battle.command.shared", new SourceLocationId(2)),
                    new CommandCategoryId(3),
                    new ModuleId(8),
                    new CommandPayloadSchemaRefIR(new CommandPayloadSchemaId(4), new SourceLocationId(2)),
                    new CommandExecutorRefIR(new CommandExecutorId(5), new SourceLocationId(2)),
                    Array.Empty<CommandDependencyIR>(),
                    new SourceLocationId(2)),
                new CommandIR(
                    new CommandTypeId(13),
                    "BattleCommandB",
                    new CommandAuthoringKeyRefIR(new CommandAuthoringKeyId(501), " battle.command.shared ", new SourceLocationId(3)),
                    new CommandCategoryId(3),
                    new ModuleId(8),
                    new CommandPayloadSchemaRefIR(new CommandPayloadSchemaId(6), new SourceLocationId(3)),
                    new CommandExecutorRefIR(new CommandExecutorId(7), new SourceLocationId(3)),
                    Array.Empty<CommandDependencyIR>(),
                    new SourceLocationId(3)),
            };

            ArgumentException duplicateAuthoringKeyException = Assert.Throws<ArgumentException>(() => new KernelIR(
                new KernelIRHeader("KernelIR-Battle", 1, "TinnosukeGameLib", "Battle", "1.0.0", default, default),
                new KernelProfileIR("Battle", KernelProfileMask.Release, new AvailabilityIR(KernelProfileMask.Release, true, null)),
                new[] { module },
                Array.Empty<ScopeIR>(),
                Array.Empty<ServiceIR>(),
                commands,
                Array.Empty<ValueKeyIR>(),
                Array.Empty<LifecycleIR>(),
                Array.Empty<RuntimeQueryIR>(),
                Array.Empty<DependencyEdgeIR>(),
                sources));

            Assert.That(duplicateAuthoringKeyException, Is.Not.Null);
            Assert.That(duplicateAuthoringKeyException!.Message, Does.Contain("unique normalized authoring keys"));
        }

        static SourceLocationIR CreateGeneratedSourceLocation(string generatorName, string generatedFrom)
        {
            return new SourceLocationIR(new GeneratedSourceLocation(generatorName, generatedFrom, "Build"));
        }
    }
}

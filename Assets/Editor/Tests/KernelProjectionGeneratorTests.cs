#nullable enable
using System;
using Game.Kernel.Generation;
using Game.Kernel.Diagnostics;
using Game.Kernel.IR;
using Game.Kernel.Validation;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class KernelProjectionGeneratorTests
    {
        [Test]
        public void Generate_EndToEndPassesAndBuildsAllProjectionArtifacts()
        {
            KernelIR kernelIR = CreateKernelIR(reverseNestedOrder: false);

            KernelProjectionGenerationResult result = KernelProjectionGenerator.Generate(
                kernelIR,
                new PlanId(101),
                new ArtifactSetId(202),
                4,
                "1.0.0",
                "Development",
                KernelProfileMask.Development);

            Assert.That(result.IsVerified, Is.True);
            Assert.That(result.PlanVerification.IsVerified, Is.True);
            Assert.That(result.ProjectionValidationReport.Status, Is.EqualTo(ValidationResultStatus.Passed));
            Assert.That(result.GeneratedPlan.Artifacts.Length, Is.EqualTo(9));
            Assert.That(result.Projections.ServiceGraph.Services.Length, Is.EqualTo(2));
            Assert.That(result.Projections.ServiceGraph.Entries.Length, Is.EqualTo(2));
            Assert.That(result.Projections.ServiceGraph.Slots.Length, Is.EqualTo(2));
            Assert.That(result.Projections.ServiceGraph.Entries[0].ServiceId, Is.EqualTo(new ServiceId(100)));
            Assert.That(result.Projections.ServiceGraph.Entries[0].Cardinality, Is.EqualTo(ServiceCardinalityKind.SingletonGlobal));
            Assert.That(result.Projections.ServiceGraph.Entries[0].Factory.FactoryKind, Is.EqualTo(ServiceFactoryKind.GeneratedFactory));
            Assert.That(result.Projections.ServiceGraph.Entries[1].Factory.FactoryKind, Is.EqualTo(ServiceFactoryKind.ProvidedInstance));
            Assert.That(result.Projections.ServiceGraph.Entries[1].Cardinality, Is.EqualTo(ServiceCardinalityKind.OnePerAuthoredScope));
            Assert.That(result.Projections.ServiceGraph.Slots[0].SlotIndex, Is.EqualTo(0));
            Assert.That(result.Projections.ServiceGraph.Slots[0].EntryIndex, Is.EqualTo(0));
            Assert.That(result.Projections.ServiceGraph.Slots[1].ServiceId, Is.EqualTo(new ServiceId(110)));
            Assert.That(result.Projections.ServiceGraph.Slots[0].Contracts.Length, Is.EqualTo(2));
            Assert.That(result.Projections.ServiceGraph.Slots[0].Dependencies.Length, Is.EqualTo(2));
            Assert.That(result.Projections.ScopeGraph.Scopes.Length, Is.EqualTo(1));
            Assert.That(result.Projections.LifecyclePlan.Lifecycles.Length, Is.EqualTo(1));
            Assert.That(result.Projections.LifecyclePlan.Lifecycles[0].FailurePolicy, Is.EqualTo(LifecycleFailurePolicy.FailScope));
            Assert.That(result.Projections.LifecyclePlan.Lifecycles[0].FailurePolicyIsExplicit, Is.True);
            Assert.That(result.Projections.CommandCatalog.Commands.Length, Is.EqualTo(1));
            Assert.That(result.Projections.CommandCatalog.Entries.Length, Is.EqualTo(1));
            Assert.That(result.Projections.CommandCatalog.Modules.Length, Is.EqualTo(1));
            Assert.That(result.Projections.CommandCatalog.Categories.Length, Is.EqualTo(1));
            Assert.That(result.Projections.CommandCatalog.Entries[0].TypeId, Is.EqualTo(new CommandTypeId(200)));
            Assert.That(result.Projections.CommandCatalog.Entries[0].Executor.Id, Is.EqualTo(new CommandExecutorId(202)));
            Assert.That(result.Projections.CommandCatalog.Entries[0].PayloadSchema.CommandTypeId, Is.EqualTo(new CommandTypeId(200)));
            Assert.That(result.Projections.CommandCatalog.Modules[0].ModuleId, Is.EqualTo(new ModuleId(10)));
            Assert.That(result.Projections.CommandCatalog.Modules[0].CommandCount, Is.EqualTo(1));
            Assert.That(result.Projections.CommandCatalog.Modules[0].CategoryIds[0], Is.EqualTo(new CommandCategoryId(1)));
            Assert.That(result.Projections.CommandCatalog.Categories[0].CategoryId, Is.EqualTo(new CommandCategoryId(1)));
            Assert.That(result.Projections.CommandCatalog.Categories[0].CommandCount, Is.EqualTo(1));
            Assert.That(result.Projections.CommandCatalog.Categories[0].OwnerModules[0], Is.EqualTo(new ModuleId(10)));
            Assert.That(ContainsRuntimeIdentityEntry(result.Projections.DebugMap.Entries, RuntimeIdentityKind.CommandAuthoringKey, 203, "Command200/AuthoringKey", 10, 17), Is.True);
            Assert.That(result.Projections.ValueSchema.ValueKeys.Length, Is.EqualTo(2));
            Assert.That(result.Projections.RuntimeQuery.RuntimeQueries.Length, Is.EqualTo(1));
            Assert.That(result.Projections.DebugMap.Entries.Length, Is.GreaterThan(0));
            Assert.That(result.Projections.GenerationReport.ValidationStatus, Is.EqualTo(ValidationResultStatus.Passed));
            Assert.That(result.Projections.ValidationReport.Report.Status, Is.EqualTo(ValidationResultStatus.Passed));
        }

        [Test]
        public void ServiceGraphPlan_RejectsDuplicateServiceIds()
        {
            VerifiedArtifactHeader header = new VerifiedArtifactHeader(
                new PlanId(101),
                new ArtifactSetId(202),
                new ArtifactId(1),
                ArtifactKind.ServiceGraph,
                4,
                new Hash128(1, 2, 3, 4),
                new Hash128(5, 6, 7, 8),
                new Hash128(9, 10, 11, 12),
                new Hash128(13, 14, 15, 16),
                new Hash128(17, 18, 19, 20),
                "1.0.0");

            ServiceIR first = new ServiceIR(
                new ServiceId(100),
                "Service100",
                ServiceLifetimeKind.Singleton,
                new ModuleId(10),
                new[] { new ServiceContractIR("IService100", new SourceLocationId(1)) },
                Array.Empty<ServiceDependencyIR>(),
                ServiceFactoryKind.GeneratedFactory,
                new SourceLocationId(1));

            ServiceIR second = new ServiceIR(
                new ServiceId(100),
                "Service100Duplicate",
                ServiceLifetimeKind.Singleton,
                new ModuleId(10),
                new[] { new ServiceContractIR("IService100Duplicate", new SourceLocationId(2)) },
                Array.Empty<ServiceDependencyIR>(),
                ServiceFactoryKind.GeneratedFactory,
                new SourceLocationId(2));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => new ServiceGraphPlan(header, new[] { first, second }));

            Assert.That(exception, Is.Not.Null);
            Assert.That(exception!.Message, Does.Contain("unique ServiceId values"));
        }

        [Test]
        public void CommandCatalogPlan_BuildsStructuredGroupedMetadata()
        {
            CommandIR[] commands =
            {
                new CommandIR(
                    new CommandTypeId(220),
                    "Command220",
                    new CommandAuthoringKeyRefIR(new CommandAuthoringKeyId(223), " command.220 ", new SourceLocationId(20)),
                    new CommandCategoryId(1),
                    new ModuleId(10),
                    new CommandPayloadSchemaRefIR(new CommandPayloadSchemaId(221), new SourceLocationId(21)),
                    new CommandExecutorRefIR(new CommandExecutorId(222), new SourceLocationId(22)),
                    Array.Empty<CommandDependencyIR>(),
                    new SourceLocationId(23)),
                new CommandIR(
                    new CommandTypeId(200),
                    "Command200",
                    new CommandAuthoringKeyRefIR(new CommandAuthoringKeyId(203), "command.200", new SourceLocationId(17)),
                    new CommandCategoryId(1),
                    new ModuleId(10),
                    new CommandPayloadSchemaRefIR(
                        new CommandPayloadSchemaId(201),
                        new SourceLocationId(18),
                        new[]
                        {
                            new CommandPayloadFieldIR(
                                "valueKey",
                                CommandPayloadFieldKindIR.ValueKeyId,
                                CommandPayloadFieldRequirementIR.Required,
                                new SourceLocationId(24),
                                CommandPayloadReferenceKindIR.ValueKeyId),
                        }),
                    new CommandExecutorRefIR(new CommandExecutorId(202), new SourceLocationId(19)),
                    Array.Empty<CommandDependencyIR>(),
                    new SourceLocationId(15)),
            };

            Hash128 contentHash = KernelProjectionHashing.ComputeCommandCatalogHash(commands);

            VerifiedArtifactHeader header = new VerifiedArtifactHeader(
                new PlanId(101),
                new ArtifactSetId(202),
                new ArtifactId(1),
                ArtifactKind.CommandCatalog,
                4,
                new Hash128(1, 2, 3, 4),
                new Hash128(5, 6, 7, 8),
                new Hash128(9, 10, 11, 12),
                new Hash128(13, 14, 15, 16),
                contentHash,
                "1.0.0");

            CommandCatalogPlan plan = new CommandCatalogPlan(header, commands);

            Assert.That(plan.Commands.Length, Is.EqualTo(2));
            Assert.That(plan.Commands[0].TypeId, Is.EqualTo(new CommandTypeId(200)));
            Assert.That(plan.Entries[0].TypeId, Is.EqualTo(new CommandTypeId(200)));
            Assert.That(plan.Entries[0].PayloadSchema.Fields.Length, Is.EqualTo(1));
            Assert.That(plan.Entries[0].PayloadSchema.Fields[0].FieldPath, Is.EqualTo("valueKey"));
            Assert.That(plan.Entries[0].PayloadSchema.Fields[0].ReferenceKind, Is.EqualTo(CommandPayloadReferenceKindIR.ValueKeyId));
            Assert.That(plan.Entries[1].TypeId, Is.EqualTo(new CommandTypeId(220)));
            Assert.That(plan.Modules.Length, Is.EqualTo(1));
            Assert.That(plan.Modules[0].RepresentativeCommandTypeId, Is.EqualTo(new CommandTypeId(200)));
            Assert.That(plan.Modules[0].CommandCount, Is.EqualTo(2));
            Assert.That(plan.Modules[0].CategoryIds[0], Is.EqualTo(new CommandCategoryId(1)));
            Assert.That(plan.Categories.Length, Is.EqualTo(1));
            Assert.That(plan.Categories[0].RepresentativeCommandTypeId, Is.EqualTo(new CommandTypeId(200)));
            Assert.That(plan.Categories[0].CommandCount, Is.EqualTo(2));
            Assert.That(plan.Categories[0].OwnerModules[0], Is.EqualTo(new ModuleId(10)));
        }

        [Test]
        public void Generate_IsDeterministicForEquivalentNestedConstructionOrder()
        {
            KernelIR left = CreateKernelIR(reverseNestedOrder: false);
            KernelIR right = CreateKernelIR(reverseNestedOrder: true);

            KernelProjectionGenerationResult first = KernelProjectionGenerator.Generate(
                left,
                new PlanId(101),
                new ArtifactSetId(202),
                4,
                "1.0.0",
                "Development",
                KernelProfileMask.Development);

            KernelProjectionGenerationResult second = KernelProjectionGenerator.Generate(
                right,
                new PlanId(101),
                new ArtifactSetId(202),
                4,
                "1.0.0",
                "Development",
                KernelProfileMask.Development);

            Assert.That(first.GeneratedPlan.Header.GeneratedHash, Is.EqualTo(second.GeneratedPlan.Header.GeneratedHash));
            Assert.That(first.Projections.ServiceGraph.ContentHash, Is.EqualTo(second.Projections.ServiceGraph.ContentHash));
            Assert.That(first.Projections.ScopeGraph.ContentHash, Is.EqualTo(second.Projections.ScopeGraph.ContentHash));
            Assert.That(first.Projections.LifecyclePlan.ContentHash, Is.EqualTo(second.Projections.LifecyclePlan.ContentHash));
            Assert.That(first.Projections.CommandCatalog.ContentHash, Is.EqualTo(second.Projections.CommandCatalog.ContentHash));
            Assert.That(first.Projections.ValueSchema.ContentHash, Is.EqualTo(second.Projections.ValueSchema.ContentHash));
            Assert.That(first.Projections.RuntimeQuery.ContentHash, Is.EqualTo(second.Projections.RuntimeQuery.ContentHash));
            Assert.That(first.Projections.DebugMap.ContentHash, Is.EqualTo(second.Projections.DebugMap.ContentHash));
            Assert.That(first.Projections.GenerationReport.ContentHash, Is.EqualTo(second.Projections.GenerationReport.ContentHash));
            Assert.That(first.Projections.ValidationReport.ContentHash, Is.EqualTo(second.Projections.ValidationReport.ContentHash));
        }

        [Test]
        public void Generate_RegistryHashChangesWhenServiceDefinitionChanges()
        {
            KernelIR baseline = CreateKernelIR(reverseNestedOrder: false);
            KernelIR changed = CreateKernelIR(reverseNestedOrder: false, service100Name: "Service100Renamed");

            KernelProjectionGenerationResult first = KernelProjectionGenerator.Generate(
                baseline,
                new PlanId(101),
                new ArtifactSetId(202),
                4,
                "1.0.0",
                "Development",
                KernelProfileMask.Development);

            KernelProjectionGenerationResult second = KernelProjectionGenerator.Generate(
                changed,
                new PlanId(101),
                new ArtifactSetId(202),
                4,
                "1.0.0",
                "Development",
                KernelProfileMask.Development);

            Assert.That(first.GeneratedPlan.Header.RegistryHash, Is.Not.EqualTo(second.GeneratedPlan.Header.RegistryHash));
        }

        [Test]
        public void Generate_ProfileHashChangesWhenModuleAvailabilityChanges()
        {
            KernelIR baseline = CreateKernelIR(reverseNestedOrder: false, moduleAvailabilityMask: KernelProfileMask.All);
            KernelIR changed = CreateKernelIR(reverseNestedOrder: false, moduleAvailabilityMask: KernelProfileMask.Development);

            KernelProjectionGenerationResult first = KernelProjectionGenerator.Generate(
                baseline,
                new PlanId(101),
                new ArtifactSetId(202),
                4,
                "1.0.0",
                "Development",
                KernelProfileMask.Development);

            KernelProjectionGenerationResult second = KernelProjectionGenerator.Generate(
                changed,
                new PlanId(101),
                new ArtifactSetId(202),
                4,
                "1.0.0",
                "Development",
                KernelProfileMask.Development);

            Assert.That(first.GeneratedPlan.Header.ProfileHash, Is.Not.EqualTo(second.GeneratedPlan.Header.ProfileHash));
        }

        [Test]
        public void Generate_UsesSelectedProfileMaskInDebugMapEntries()
        {
            KernelIR kernelIR = CreateKernelIR(reverseNestedOrder: false, profileMask: KernelProfileMask.Development);
            KernelProfileMask selectedProfileMask = KernelProfileMask.Release;

            KernelProjectionGenerationResult result = KernelProjectionGenerator.Generate(
                kernelIR,
                new PlanId(101),
                new ArtifactSetId(202),
                4,
                "1.0.0",
                "Release",
                selectedProfileMask);

            for (int index = 0; index < result.Projections.DebugMap.Entries.Length; index++)
                Assert.That(result.Projections.DebugMap.Entries[index].ProfileMask, Is.EqualTo(selectedProfileMask));
        }

        [Test]
        public void Generate_IncludesDiagnosticSeedsInDebugMapDeterministically()
        {
            DiagnosticSeedIR[] diagnosticSeeds =
            {
                new DiagnosticSeedIR("MISSING_DEPENDENCY", "Missing Dependency", new ModuleId(10), new SourceLocationId(24)),
                new DiagnosticSeedIR("PROFILE_MISMATCH", "Profile Mismatch", new ModuleId(10), new SourceLocationId(25)),
            };

            KernelIR left = CreateKernelIR(reverseNestedOrder: false, diagnosticSeeds: diagnosticSeeds);
            KernelIR right = CreateKernelIR(reverseNestedOrder: false, diagnosticSeeds: new[] { diagnosticSeeds[1], diagnosticSeeds[0] });
            KernelIR baseline = CreateKernelIR(reverseNestedOrder: false);

            KernelProjectionGenerationResult first = KernelProjectionGenerator.Generate(
                left,
                new PlanId(101),
                new ArtifactSetId(202),
                4,
                "1.0.0",
                "Development",
                KernelProfileMask.Development);

            KernelProjectionGenerationResult second = KernelProjectionGenerator.Generate(
                right,
                new PlanId(101),
                new ArtifactSetId(202),
                4,
                "1.0.0",
                "Development",
                KernelProfileMask.Development);

            KernelProjectionGenerationResult baselineResult = KernelProjectionGenerator.Generate(
                baseline,
                new PlanId(101),
                new ArtifactSetId(202),
                4,
                "1.0.0",
                "Development",
                KernelProfileMask.Development);

            Assert.That(first.Projections.DebugMap.ContentHash, Is.EqualTo(second.Projections.DebugMap.ContentHash));
            Assert.That(first.Projections.DebugMap.Entries.Length, Is.EqualTo(second.Projections.DebugMap.Entries.Length));
            Assert.That(first.Projections.DebugMap.Entries.Length, Is.EqualTo(baselineResult.Projections.DebugMap.Entries.Length + diagnosticSeeds.Length));
            Assert.That(first.Projections.DebugMap.ContentHash, Is.Not.EqualTo(baselineResult.Projections.DebugMap.ContentHash));
            Assert.That(ContainsDiagnosticSeedEntry(first.Projections.DebugMap.Entries, "MISSING_DEPENDENCY", "Missing Dependency", 10, 24), Is.True);
            Assert.That(ContainsDiagnosticSeedEntry(first.Projections.DebugMap.Entries, "PROFILE_MISMATCH", "Profile Mismatch", 10, 25), Is.True);
        }

        static KernelIR CreateKernelIR(bool reverseNestedOrder, string service100Name = "Service100", KernelProfileMask profileMask = KernelProfileMask.Development, KernelProfileMask moduleAvailabilityMask = KernelProfileMask.All, DiagnosticSeedIR[]? diagnosticSeeds = null)
        {
            ModuleIR module = new ModuleIR(
                new ModuleId(10),
                "Core",
                ModuleKind.Feature,
                new ModuleVersion(1),
                new ModuleAvailabilityIR(new AvailabilityIR(moduleAvailabilityMask, true, null)),
                new SourceLocationId(1));

            ServiceContractIR[] service100Contracts = reverseNestedOrder
                ? new[]
                {
                    new ServiceContractIR("IService100B", new SourceLocationId(3)),
                    new ServiceContractIR("IService100A", new SourceLocationId(2)),
                }
                : new[]
                {
                    new ServiceContractIR("IService100A", new SourceLocationId(2)),
                    new ServiceContractIR("IService100B", new SourceLocationId(3)),
                };

            ServiceDependencyIR[] service100Dependencies = reverseNestedOrder
                ? new[]
                {
                    new ServiceDependencyIR(new DependencyNodeIR(new RuntimeQueryId(400)), DependencyStrength.Optional, new SourceLocationId(5)),
                    new ServiceDependencyIR(new DependencyNodeIR(new ServiceId(110)), DependencyStrength.Required, new SourceLocationId(4)),
                }
                : new[]
                {
                    new ServiceDependencyIR(new DependencyNodeIR(new ServiceId(110)), DependencyStrength.Required, new SourceLocationId(4)),
                    new ServiceDependencyIR(new DependencyNodeIR(new RuntimeQueryId(400)), DependencyStrength.Optional, new SourceLocationId(5)),
                };

            ServiceIR service100 = new ServiceIR(
                new ServiceId(100),
                service100Name,
                ServiceLifetimeKind.Singleton,
                new ModuleId(10),
                service100Contracts,
                service100Dependencies,
                ServiceFactoryKind.GeneratedFactory,
                new SourceLocationId(6));

            ServiceIR service110 = new ServiceIR(
                new ServiceId(110),
                "Service110",
                ServiceLifetimeKind.Scoped,
                new ModuleId(10),
                new[]
                {
                    new ServiceContractIR("IService110", new SourceLocationId(7)),
                },
                null,
                ServiceFactoryKind.ProvidedInstance,
                new SourceLocationId(8));

            ScopeServiceRequirementIR[] scopeRequiredServices = reverseNestedOrder
                ? new[]
                {
                    new ScopeServiceRequirementIR(new ServiceId(110), DependencyStrength.Optional, new SourceLocationId(10)),
                    new ScopeServiceRequirementIR(new ServiceId(100), DependencyStrength.Required, new SourceLocationId(9)),
                }
                : new[]
                {
                    new ScopeServiceRequirementIR(new ServiceId(100), DependencyStrength.Required, new SourceLocationId(9)),
                    new ScopeServiceRequirementIR(new ServiceId(110), DependencyStrength.Optional, new SourceLocationId(10)),
                };

            ScopeValueInitRefIR[] scopeValueInitPlans = reverseNestedOrder
                ? new[]
                {
                    new ScopeValueInitRefIR(new ValueInitPlanId(401), new SourceLocationId(12)),
                    new ScopeValueInitRefIR(new ValueInitPlanId(400), new SourceLocationId(11)),
                }
                : new[]
                {
                    new ScopeValueInitRefIR(new ValueInitPlanId(400), new SourceLocationId(11)),
                    new ScopeValueInitRefIR(new ValueInitPlanId(401), new SourceLocationId(12)),
                };

            ScopeIR scope = new ScopeIR(
                new ScopeAuthoringId(300),
                new ScopePlanId(301),
                "RootScope",
                ScopeKind.Root,
                new ModuleId(10),
                default,
                scopeRequiredServices,
                scopeValueInitPlans,
                new ScopeServiceBoundaryIR(ScopeServiceBoundaryKind.OwnedLocal, 1, new SourceLocationId(3)),
                new LifecyclePlanRefIR(new LifecyclePlanId(500), new SourceLocationId(13)),
                new SourceLocationId(14));

            CommandDependencyIR[] commandDependencies = reverseNestedOrder
                ? new[]
                {
                    new CommandDependencyIR(new DependencyNodeIR(new RuntimeQueryId(400)), DependencyStrength.Optional, new SourceLocationId(17)),
                    new CommandDependencyIR(new DependencyNodeIR(new ServiceId(100)), DependencyStrength.Required, new SourceLocationId(16)),
                }
                : new[]
                {
                    new CommandDependencyIR(new DependencyNodeIR(new ServiceId(100)), DependencyStrength.Required, new SourceLocationId(16)),
                    new CommandDependencyIR(new DependencyNodeIR(new RuntimeQueryId(400)), DependencyStrength.Optional, new SourceLocationId(17)),
                };

            CommandIR command = new CommandIR(
                new CommandTypeId(200),
                "Command200",
                new CommandAuthoringKeyRefIR(new CommandAuthoringKeyId(203), "command.200", new SourceLocationId(17)),
                new CommandCategoryId(1),
                new ModuleId(10),
                new CommandPayloadSchemaRefIR(new CommandPayloadSchemaId(201), new SourceLocationId(18)),
                new CommandExecutorRefIR(new CommandExecutorId(202), new SourceLocationId(19)),
                commandDependencies,
                new SourceLocationId(15));

            ValueKeyIR[] valueKeys = new[]
            {
                new ValueKeyIR(
                    new ValueKeyId(300),
                    "value.current",
                    "Value Current",
                    ValueKind.Int,
                    new ModuleId(10),
                    new ValueSchemaRefIR(new ValueSchemaId(301), new SourceLocationId(21)),
                    new SavePolicyIR(false, false, null),
                    new SourceLocationId(20)),
                new ValueKeyIR(
                    new ValueKeyId(310),
                    "value.target",
                    "Value Target",
                    ValueKind.String,
                    new ModuleId(10),
                    new ValueSchemaRefIR(new ValueSchemaId(311), new SourceLocationId(23)),
                    new SavePolicyIR(true, true, "slot"),
                    new SourceLocationId(22)),
            };

            LifecycleStepIR[] lifecycleSteps = reverseNestedOrder
                ? new[]
                {
                    new LifecycleStepIR(
                        new LifecycleStepId(502),
                        LifecyclePhase.Release,
                        2,
                        new LifecycleTargetRefIR(new RuntimeQueryId(400)),
                        LifecycleActionKind.RuntimeQueryNotify,
                        new[] { new DependencyEdgeId(9002) },
                        new SourceLocationId(27)),
                    new LifecycleStepIR(
                        new LifecycleStepId(501),
                        LifecyclePhase.Create,
                        1,
                        new LifecycleTargetRefIR(new ServiceId(100)),
                        LifecycleActionKind.ServiceMethod,
                        new[] { new DependencyEdgeId(9001) },
                        new SourceLocationId(26)),
                }
                : new[]
                {
                    new LifecycleStepIR(
                        new LifecycleStepId(501),
                        LifecyclePhase.Create,
                        1,
                        new LifecycleTargetRefIR(new ServiceId(100)),
                        LifecycleActionKind.ServiceMethod,
                        new[] { new DependencyEdgeId(9001) },
                        new SourceLocationId(26)),
                    new LifecycleStepIR(
                        new LifecycleStepId(502),
                        LifecyclePhase.Release,
                        2,
                        new LifecycleTargetRefIR(new RuntimeQueryId(400)),
                        LifecycleActionKind.RuntimeQueryNotify,
                        new[] { new DependencyEdgeId(9002) },
                        new SourceLocationId(27)),
                };

            LifecycleIR lifecycle = new LifecycleIR(
                new LifecyclePlanId(500),
                "RootLifecycle",
                new ModuleId(10),
                lifecycleSteps,
                new SourceLocationId(28),
                LifecycleFailurePolicy.FailScope);

            RuntimeIdentityFieldIR[] runtimeQueryFields = reverseNestedOrder
                ? new[]
                {
                    new RuntimeIdentityFieldIR("name", "string", true),
                    new RuntimeIdentityFieldIR("id", "int", true),
                }
                : new[]
                {
                    new RuntimeIdentityFieldIR("id", "int", true),
                    new RuntimeIdentityFieldIR("name", "string", true),
                };

            RuntimeQueryIR runtimeQuery = new RuntimeQueryIR(
                new RuntimeQueryId(400),
                "RuntimeQuery400",
                RuntimeQueryTargetKind.Service,
                runtimeQueryFields,
                new RuntimeQueryPolicyIR(true, false, DependencyPhase.Runtime),
                new ModuleId(10),
                new SourceLocationId(29));

            DependencyEdgeIR[] dependencies = new[]
            {
                new DependencyEdgeIR(
                    new DependencyEdgeId(9001),
                    new DependencyNodeIR(new ServiceId(100)),
                    new DependencyNodeIR(new ServiceId(110)),
                    DependencyKind.Requires,
                    DependencyPhase.Build,
                    DependencyStrength.Required,
                    new SourceLocationId(24)),
                new DependencyEdgeIR(
                    new DependencyEdgeId(9002),
                    new DependencyNodeIR(new RuntimeQueryId(400)),
                    new DependencyNodeIR(new ServiceId(100)),
                    DependencyKind.References,
                    DependencyPhase.Runtime,
                    DependencyStrength.Optional,
                    new SourceLocationId(25)),
            };

            SourceLocationTable sources = new SourceLocationTable(new[]
            {
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Module")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Service100")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Service110")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Service100ContractA")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Service100ContractB")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Service100DependencyA")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Service100DependencyB")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Service110Contract")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Scope")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "ScopeRequiredA")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "ScopeRequiredB")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "ScopeValueInitA")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "ScopeValueInitB")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Lifecycle")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Command")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "CommandPayload")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "CommandExecutor")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "ValueKeyCurrent")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "ValueSchemaCurrent")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "ValueKeyTarget")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "ValueSchemaTarget")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "RuntimeQuery")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Extra23")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Extra24")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Extra25")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Extra26")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Extra27")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Extra28")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Extra29")),
            });

            KernelIRHeader header = new KernelIRHeader(
                "DOC",
                1,
                "TestProject",
                "Development",
                "1.0.0",
                new Hash128(1, 2, 3, 4),
                new Hash128(5, 6, 7, 8));

            KernelProfileIR profile = new KernelProfileIR("Development", profileMask, new AvailabilityIR(KernelProfileMask.All, true, null));

            return new KernelIR(
                header,
                profile,
                new[] { module },
                new[] { scope },
                new[] { service100, service110 },
                new[] { command },
                valueKeys,
                new[] { lifecycle },
                new[] { runtimeQuery },
                dependencies,
                sources,
                diagnosticSeeds);
        }

        static bool ContainsDiagnosticSeedEntry(ReadOnlySpan<KernelDebugMapEntry> entries, string seedKey, string debugName, int ownerModuleId, int sourceId)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                KernelDebugMapEntry entry = entries[i];
                if (entry.Identity.Kind != RuntimeIdentityKind.DiagnosticSeed)
                    continue;

                if (string.Equals(entry.Name, debugName, StringComparison.Ordinal)
                    && string.Equals(entry.DiagnosticSeedKey, seedKey, StringComparison.Ordinal)
                    && entry.LegacyOrigin == null
                    && entry.OwnerModule.Value == ownerModuleId
                    && entry.Source.Value == sourceId)
                {
                    return true;
                }
            }

            return false;
        }

        static bool ContainsRuntimeIdentityEntry(ReadOnlySpan<KernelDebugMapEntry> entries, RuntimeIdentityKind kind, int value, string name, int ownerModuleId, int sourceId)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                KernelDebugMapEntry entry = entries[i];
                if (entry.Identity.Kind != kind)
                    continue;

                if (entry.Identity.Value == value
                    && string.Equals(entry.Name, name, StringComparison.Ordinal)
                    && entry.OwnerModule.Value == ownerModuleId
                    && entry.Source.Value == sourceId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
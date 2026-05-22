#nullable enable
using System;
using Game.Kernel.IR;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class KernelIRHashingTests
    {
        [Test]
        public void Hash128Serialization_RoundTripsHexAndBytes()
        {
            Hash128 original = new Hash128(0x11223344u, 0x55667788u, 0x99aabbccu, 0xddeeff11u);

            string hex = Hash128Serialization.ToHexString(original);
            Hash128 parsed = Hash128Serialization.Parse(hex);
            Hash128 fromBytes = Hash128Serialization.FromBytes(Hash128Serialization.ToBytes(original));

            Assert.That(parsed, Is.EqualTo(original));
            Assert.That(fromBytes, Is.EqualTo(original));
            Assert.That(Hash128Serialization.TryParse(hex, out Hash128 tryParsed), Is.True);
            Assert.That(tryParsed, Is.EqualTo(original));
        }

        [Test]
        public void ComputeNormalizedHash_EquivalentKernelIRsMatchDespiteNestedConstructionOrder()
        {
            KernelIR first = CreateKernelIR(reverseNestedOrder: false, moduleVersion: 1, sourceGeneratorName: "ModuleProjector");
            KernelIR second = CreateKernelIR(reverseNestedOrder: true, moduleVersion: 1, sourceGeneratorName: "ModuleProjector");

            Hash128 firstNormalizedHash = KernelIRHashing.ComputeNormalizedHash(first);
            Hash128 secondNormalizedHash = KernelIRHashing.ComputeNormalizedHash(second);
            Hash128 firstSourceHash = KernelIRHashing.ComputeSourceHash(first);
            Hash128 secondSourceHash = KernelIRHashing.ComputeSourceHash(second);

            Assert.That(firstNormalizedHash, Is.EqualTo(secondNormalizedHash));
            Assert.That(firstSourceHash, Is.EqualTo(secondSourceHash));
        }

        [Test]
        public void ComputeNormalizedHash_SemanticChangesChangeHash()
        {
            KernelIR baseline = CreateKernelIR(reverseNestedOrder: false, moduleVersion: 1, sourceGeneratorName: "ModuleProjector");
            KernelIR changedVersion = CreateKernelIR(reverseNestedOrder: false, moduleVersion: 2, sourceGeneratorName: "ModuleProjector");

            Hash128 baselineHash = KernelIRHashing.ComputeNormalizedHash(baseline);
            Hash128 changedHash = KernelIRHashing.ComputeNormalizedHash(changedVersion);

            Assert.That(changedHash, Is.Not.EqualTo(baselineHash));
        }

        [Test]
        public void ComputeNormalizedHash_ScopeServiceBoundaryChangesChangeHash()
        {
            KernelIR ownedBoundary = CreateKernelIR(
                reverseNestedOrder: false,
                moduleVersion: 1,
                sourceGeneratorName: "ModuleProjector",
                scopeServiceBoundary: new ScopeServiceBoundaryIR(ScopeServiceBoundaryKind.OwnedLocal, 1, new SourceLocationId(3)));

            KernelIR parentBoundary = CreateKernelIR(
                reverseNestedOrder: false,
                moduleVersion: 1,
                sourceGeneratorName: "ModuleProjector",
                scopeServiceBoundary: new ScopeServiceBoundaryIR(ScopeServiceBoundaryKind.ReferencesParent, 0, new SourceLocationId(3)));

            Hash128 ownedBoundaryHash = KernelIRHashing.ComputeNormalizedHash(ownedBoundary);
            Hash128 parentBoundaryHash = KernelIRHashing.ComputeNormalizedHash(parentBoundary);

            Assert.That(parentBoundaryHash, Is.Not.EqualTo(ownedBoundaryHash));
        }

        [Test]
        public void ComputeNormalizedHash_ScopeUnityObjectLinkChangesChangeHash()
        {
            KernelIR baseline = CreateKernelIR(reverseNestedOrder: false, moduleVersion: 1, sourceGeneratorName: "ModuleProjector");
            KernelIR linked = CreateKernelIR(
                reverseNestedOrder: false,
                moduleVersion: 1,
                sourceGeneratorName: "ModuleProjector",
                scopeUnityObjectLink: new UnityObjectLinkIR("Scene", "scene-guid-1", 101, "BattleScopeAuthoring", new SourceLocationId(4)));

            Hash128 baselineHash = KernelIRHashing.ComputeNormalizedHash(baseline);
            Hash128 linkedHash = KernelIRHashing.ComputeNormalizedHash(linked);

            Assert.That(linkedHash, Is.Not.EqualTo(baselineHash));
        }

        [Test]
        public void ComputeNormalizedHash_LifecycleFailurePolicyChangesChangeHash()
        {
            KernelIR baseline = CreateKernelIR(reverseNestedOrder: false, moduleVersion: 1, sourceGeneratorName: "ModuleProjector", lifecycleFailurePolicy: LifecycleFailurePolicy.FailKernel);
            KernelIR changedPolicy = CreateKernelIR(reverseNestedOrder: false, moduleVersion: 1, sourceGeneratorName: "ModuleProjector", lifecycleFailurePolicy: LifecycleFailurePolicy.FailScope);

            Hash128 baselineHash = KernelIRHashing.ComputeNormalizedHash(baseline);
            Hash128 changedHash = KernelIRHashing.ComputeNormalizedHash(changedPolicy);

            Assert.That(changedHash, Is.Not.EqualTo(baselineHash));
        }

        [Test]
        public void ComputeNormalizedHash_LifecycleFailurePolicyExplicitnessChangesChangeHash()
        {
            KernelIR explicitPolicy = CreateKernelIR(reverseNestedOrder: false, moduleVersion: 1, sourceGeneratorName: "ModuleProjector", lifecycleFailurePolicy: LifecycleFailurePolicy.FailKernel, lifecycleFailurePolicyIsExplicit: true);
            KernelIR defaultedPolicy = CreateKernelIR(reverseNestedOrder: false, moduleVersion: 1, sourceGeneratorName: "ModuleProjector", lifecycleFailurePolicy: LifecycleFailurePolicy.FailKernel, lifecycleFailurePolicyIsExplicit: false);

            Hash128 explicitHash = KernelIRHashing.ComputeNormalizedHash(explicitPolicy);
            Hash128 defaultedHash = KernelIRHashing.ComputeNormalizedHash(defaultedPolicy);

            Assert.That(defaultedHash, Is.Not.EqualTo(explicitHash));
        }

        [Test]
        public void ComputeNormalizedHash_RuntimeCycleMediationChangesChangeHash()
        {
            KernelIR baseline = CreateKernelIR(reverseNestedOrder: false, moduleVersion: 1, sourceGeneratorName: "ModuleProjector");
            KernelIR changedMediation = CreateKernelIR(reverseNestedOrder: false, moduleVersion: 1, sourceGeneratorName: "ModuleProjector", dependencyRuntimeCycleMediation: RuntimeCycleMediationKind.LazyHandle);

            Hash128 baselineHash = KernelIRHashing.ComputeNormalizedHash(baseline);
            Hash128 changedHash = KernelIRHashing.ComputeNormalizedHash(changedMediation);

            Assert.That(changedHash, Is.Not.EqualTo(baselineHash));
        }

        [Test]
        public void ComputeNormalizedHash_LegacyCompatChangesChangeHash()
        {
            KernelIR baseline = CreateKernelIR(reverseNestedOrder: false, moduleVersion: 1, sourceGeneratorName: "ModuleProjector");
            KernelIR changedLegacyCompat = CreateKernelIR(reverseNestedOrder: false, moduleVersion: 1, sourceGeneratorName: "ModuleProjector", primaryModuleLegacyCompat: new LegacyCompatDescriptorIR(LegacyCompatKind.RuntimeAdapter, "LegacySystem", "ServiceGraph", KernelProfileMask.Development | KernelProfileMask.Test, LegacyRemovalStatus.Temporary, "LEGACY_RUNTIME_ADAPTER_USED", "Remove after migration", "TICKET-1", surface: LegacyAdapterSurface.Resolver, legacySourceType: "RuntimeResolverHub", explicitTargets: new[] { new DependencyNodeIR(new ServiceId(201)) }));

            Hash128 baselineHash = KernelIRHashing.ComputeNormalizedHash(baseline);
            Hash128 changedHash = KernelIRHashing.ComputeNormalizedHash(changedLegacyCompat);

            Assert.That(changedHash, Is.Not.EqualTo(baselineHash));
        }

        [Test]
        public void ComputeNormalizedHash_LegacyCompatTrackingChangesChangeHash()
        {
            KernelIR baseline = CreateKernelIR(
                reverseNestedOrder: false,
                moduleVersion: 1,
                sourceGeneratorName: "ModuleProjector",
                primaryModuleLegacyCompat: new LegacyCompatDescriptorIR(
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
                    explicitTargets: new[] { new DependencyNodeIR(new ServiceId(201)) }));

            KernelIR changed = CreateKernelIR(
                reverseNestedOrder: false,
                moduleVersion: 1,
                sourceGeneratorName: "ModuleProjector",
                primaryModuleLegacyCompat: new LegacyCompatDescriptorIR(
                    LegacyCompatKind.RuntimeAdapter,
                    "LegacySystem",
                    "ServiceGraph",
                    KernelProfileMask.Development | KernelProfileMask.Test,
                    LegacyRemovalStatus.Temporary,
                    "LEGACY_RUNTIME_ADAPTER_USED",
                    "Remove after migration",
                    "TICKET-2",
                    surface: LegacyAdapterSurface.Resolver,
                    legacySourceType: "RuntimeResolverHub",
                    explicitTargets: new[] { new DependencyNodeIR(new ServiceId(201)) }));

            Hash128 baselineHash = KernelIRHashing.ComputeNormalizedHash(baseline);
            Hash128 changedHash = KernelIRHashing.ComputeNormalizedHash(changed);

            Assert.That(changedHash, Is.Not.EqualTo(baselineHash));
        }

        [Test]
        public void ComputeNormalizedHash_LegacyCompatSurfaceChangesChangeHash()
        {
            KernelIR resolverCompat = CreateKernelIR(
                reverseNestedOrder: false,
                moduleVersion: 1,
                sourceGeneratorName: "ModuleProjector",
                primaryModuleLegacyCompat: new LegacyCompatDescriptorIR(
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
                    explicitTargets: new[] { new DependencyNodeIR(new ServiceId(201)) }));

            KernelIR commandCompat = CreateKernelIR(
                reverseNestedOrder: false,
                moduleVersion: 1,
                sourceGeneratorName: "ModuleProjector",
                primaryModuleLegacyCompat: new LegacyCompatDescriptorIR(
                    LegacyCompatKind.RuntimeAdapter,
                    "LegacySystem",
                    "ServiceGraph",
                    KernelProfileMask.Development | KernelProfileMask.Test,
                    LegacyRemovalStatus.Temporary,
                    "LEGACY_RUNTIME_ADAPTER_USED",
                    "Remove after migration",
                    "TICKET-1",
                    surface: LegacyAdapterSurface.Command,
                    legacySourceType: "LegacyCommandRunner",
                    explicitTargets: new[] { new DependencyNodeIR(new CommandTypeId(601)) }));

            Hash128 resolverHash = KernelIRHashing.ComputeNormalizedHash(resolverCompat);
            Hash128 commandHash = KernelIRHashing.ComputeNormalizedHash(commandCompat);

            Assert.That(commandHash, Is.Not.EqualTo(resolverHash));
        }

        [Test]
        public void ComputeNormalizedHash_SourceOnlyChangesDoNotChangeHash()
        {
            KernelIR baseline = CreateKernelIR(reverseNestedOrder: false, moduleVersion: 1, sourceGeneratorName: "ModuleProjector");
            KernelIR changedSource = CreateKernelIR(reverseNestedOrder: false, moduleVersion: 1, sourceGeneratorName: "ModuleProjectorUpdated");

            Hash128 baselineHash = KernelIRHashing.ComputeNormalizedHash(baseline);
            Hash128 changedHash = KernelIRHashing.ComputeNormalizedHash(changedSource);

            Assert.That(changedHash, Is.EqualTo(baselineHash));
        }

        [Test]
        public void ComputeSourceHash_SourceIdentityChangesChangeHash()
        {
            KernelIR baseline = CreateKernelIR(reverseNestedOrder: false, moduleVersion: 1, sourceGeneratorName: "ModuleProjector");
            KernelIR changedSource = CreateKernelIR(reverseNestedOrder: false, moduleVersion: 1, sourceGeneratorName: "ModuleProjectorUpdated");

            Hash128 baselineHash = KernelIRHashing.ComputeSourceHash(baseline);
            Hash128 changedHash = KernelIRHashing.ComputeSourceHash(changedSource);

            Assert.That(changedHash, Is.Not.EqualTo(baselineHash));
        }

        [Test]
        public void ComputeSourceHash_SourceReferenceRemapChangesHashWithoutChangingNormalizedHash()
        {
            KernelIR baseline = CreateKernelIR(reverseNestedOrder: false, moduleVersion: 1, sourceGeneratorName: "ModuleProjector", serviceSourceId: 3);
            KernelIR remapped = CreateKernelIR(reverseNestedOrder: false, moduleVersion: 1, sourceGeneratorName: "ModuleProjector", serviceSourceId: 4);

            Hash128 baselineNormalizedHash = KernelIRHashing.ComputeNormalizedHash(baseline);
            Hash128 remappedNormalizedHash = KernelIRHashing.ComputeNormalizedHash(remapped);
            Hash128 baselineSourceHash = KernelIRHashing.ComputeSourceHash(baseline);
            Hash128 remappedSourceHash = KernelIRHashing.ComputeSourceHash(remapped);

            Assert.That(remappedNormalizedHash, Is.EqualTo(baselineNormalizedHash));
            Assert.That(remappedSourceHash, Is.Not.EqualTo(baselineSourceHash));
        }

        [Test]
        public void ComputeSourceHash_AbsoluteUnityPathsAreExcludedFromHashInput()
        {
            KernelIR first = CreateUnitySourceKernelIR("C:/Temp/Battle.prefab", "D:/Scenes/Battle.unity");
            KernelIR second = CreateUnitySourceKernelIR("E:/Other/Battle.prefab", "F:/Maps/Battle.unity");

            Hash128 firstHash = KernelIRHashing.ComputeSourceHash(first);
            Hash128 secondHash = KernelIRHashing.ComputeSourceHash(second);
            string dump = KernelIRHashing.DumpText(first);

            Assert.That(secondHash, Is.EqualTo(firstHash));
            Assert.That(dump, Does.Contain("<excluded-absolute-path>"));
        }

        [Test]
        public void DumpText_ReportOutputIsDeterministicAndContainsCanonicalSections()
        {
            KernelIR first = CreateKernelIR(reverseNestedOrder: false, moduleVersion: 1, sourceGeneratorName: "ModuleProjector");
            KernelIR second = CreateKernelIR(reverseNestedOrder: true, moduleVersion: 1, sourceGeneratorName: "ModuleProjector");

            string firstDump = KernelIRHashing.DumpText(first);
            string secondDump = KernelIRHashing.DumpText(second);
            KernelIRDumpReport report = KernelIRHashing.CreateReport(first);

            Assert.That(secondDump, Is.EqualTo(firstDump));
            Assert.That(firstDump, Does.Contain("ComputedNormalizedHash"));
            Assert.That(firstDump, Does.Contain("Modules (2):"));
            Assert.That(firstDump, Does.Contain("Required=[ModuleId(2)@SourceLocationId(1)]"));
            Assert.That(firstDump, Does.Contain("Services (1):"));
            Assert.That(firstDump, Does.Contain("ServiceBoundary=OwnedLocal("));
            Assert.That(firstDump, Does.Contain("Contracts=[IBattleService@SourceLocationId(3), ISharedService@SourceLocationId(3)]"));
            Assert.That(firstDump, Does.Contain("AuthoringKey=battle.command@CommandAuthoringKeyId(7)@SourceLocationId(6)"));
            Assert.That(firstDump, Does.Contain("PayloadSchema=CommandPayloadSchemaId(4)@SourceLocationId(6)"));
            Assert.That(firstDump, Does.Contain("RuntimeQueries (1):"));
            Assert.That(firstDump, Does.Contain("IndexedFields=[OwnerModule:Type=ModuleId:Required=False, ServiceId:Type=ServiceId:Required=True]"));
            Assert.That(firstDump, Does.Contain("RuntimeCycleMediation=None"));
            Assert.That(firstDump, Does.Contain("LegacyCompat=<none>"));
            Assert.That(report.Modules.Length, Is.EqualTo(2));
            Assert.That(report.Sources.Length, Is.EqualTo(8));
            Assert.That(report.NormalizedHash, Is.EqualTo(KernelIRHashing.ComputeNormalizedHash(first)));
        }

        static KernelIR CreateKernelIR(bool reverseNestedOrder, int moduleVersion, string sourceGeneratorName, int serviceSourceId = 3, RuntimeCycleMediationKind dependencyRuntimeCycleMediation = RuntimeCycleMediationKind.None, LegacyCompatDescriptorIR? primaryModuleLegacyCompat = null, ScopeServiceBoundaryIR? scopeServiceBoundary = null, UnityObjectLinkIR? scopeUnityObjectLink = null, LifecycleFailurePolicy lifecycleFailurePolicy = LifecycleFailurePolicy.FailKernel, bool lifecycleFailurePolicyIsExplicit = true, KernelProfileMask lifecycleFailurePolicyJustificationProfiles = KernelProfileMask.None, string? lifecycleFailurePolicyJustification = null)
        {
            SourceLocationTable sources = new SourceLocationTable(new[]
            {
                CreateGeneratedSourceLocation(sourceGeneratorName, "BattleModule"),
                CreateGeneratedSourceLocation("SharedProjector", "SharedModule"),
                CreateGeneratedSourceLocation("ServiceProjector", "BattleService"),
                CreateGeneratedSourceLocation("ScopeProjector", "BattleScope"),
                CreateGeneratedSourceLocation("LifecycleProjector", "BattleLifecycle"),
                CreateGeneratedSourceLocation("CommandProjector", "BattleCommand"),
                CreateGeneratedSourceLocation("ValueProjector", "BattleValue"),
                CreateGeneratedSourceLocation("QueryProjector", "BattleQuery"),
            });

            ModuleIR primaryModule = new ModuleIR(
                new ModuleId(8),
                "BattleModule",
                primaryModuleLegacyCompat != null ? ModuleKind.MigrationAdapter : ModuleKind.Feature,
                new ModuleVersion(moduleVersion),
                new ModuleAvailabilityIR(new AvailabilityIR(KernelProfileMask.Release, true, null)),
                new SourceLocationId(1),
                new[] { new ModuleDependencyIR(new ModuleId(2), new SourceLocationId(1)) },
                legacyCompat: primaryModuleLegacyCompat);
            ModuleIR sharedModule = new ModuleIR(
                new ModuleId(2),
                "SharedModule",
                ModuleKind.System,
                new ModuleVersion(1),
                new ModuleAvailabilityIR(new AvailabilityIR(KernelProfileMask.Release, true, null)),
                new SourceLocationId(2));

            ServiceContractIR[] contracts = reverseNestedOrder
                ? new[]
                {
                    new ServiceContractIR("ISharedService", new SourceLocationId(3)),
                    new ServiceContractIR("IBattleService", new SourceLocationId(3)),
                }
                : new[]
                {
                    new ServiceContractIR("IBattleService", new SourceLocationId(3)),
                    new ServiceContractIR("ISharedService", new SourceLocationId(3)),
                };

            ServiceIR service = new ServiceIR(
                new ServiceId(11),
                "BattleService",
                ServiceLifetimeKind.Singleton,
                new ModuleId(8),
                contracts,
                Array.Empty<ServiceDependencyIR>(),
                ServiceFactoryKind.GeneratedFactory,
                new SourceLocationId(serviceSourceId));

            ScopeIR scope = new ScopeIR(
                new ScopeAuthoringId(1),
                new ScopePlanId(101),
                "BattleScope",
                ScopeKind.Root,
                new ModuleId(8),
                default,
                new[] { new ScopeServiceRequirementIR(new ServiceId(11), DependencyStrength.Required, new SourceLocationId(4)) },
                new[] { new ScopeValueInitRefIR(new ValueInitPlanId(7), new SourceLocationId(4)) },
                scopeServiceBoundary ?? new ScopeServiceBoundaryIR(ScopeServiceBoundaryKind.OwnedLocal, 1, new SourceLocationId(4)),
                new LifecyclePlanRefIR(new LifecyclePlanId(21), new SourceLocationId(5)),
                new SourceLocationId(4),
                scopeUnityObjectLink);

            DependencyEdgeIR dependency = new DependencyEdgeIR(
                new DependencyEdgeId(1),
                new DependencyNodeIR(new ScopePlanId(101)),
                new DependencyNodeIR(new ServiceId(11)),
                DependencyKind.Requires,
                dependencyRuntimeCycleMediation == RuntimeCycleMediationKind.None ? DependencyPhase.Boot : DependencyPhase.Runtime,
                DependencyStrength.Required,
                new SourceLocationId(5),
                dependencyRuntimeCycleMediation);

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
                        new SourceLocationId(5)),
                },
                    new SourceLocationId(5),
                    lifecycleFailurePolicy,
                    lifecycleFailurePolicyIsExplicit,
                    lifecycleFailurePolicyJustificationProfiles,
                    lifecycleFailurePolicyJustification);

            CommandIR command = new CommandIR(
                new CommandTypeId(12),
                "BattleCommand",
                new CommandAuthoringKeyRefIR(new CommandAuthoringKeyId(7), "battle.command", new SourceLocationId(6)),
                new CommandCategoryId(3),
                new ModuleId(8),
                new CommandPayloadSchemaRefIR(new CommandPayloadSchemaId(4), new SourceLocationId(6)),
                new CommandExecutorRefIR(new CommandExecutorId(5), new SourceLocationId(6)),
                new[] { new CommandDependencyIR(new DependencyNodeIR(new ServiceId(11)), DependencyStrength.Required, new SourceLocationId(6)) },
                new SourceLocationId(6));

            ValueKeyIR valueKey = new ValueKeyIR(
                new ValueKeyId(13),
                "battle.health",
                "Battle Health",
                ValueKind.LayeredNumeric,
                new ModuleId(8),
                new ValueSchemaRefIR(new ValueSchemaId(14), new SourceLocationId(7)),
                new SavePolicyIR(true, false, "Profile"),
                new SourceLocationId(7));

            RuntimeIdentityFieldIR[] indexedFields = reverseNestedOrder
                ? new[]
                {
                    new RuntimeIdentityFieldIR("OwnerModule", "ModuleId", false),
                    new RuntimeIdentityFieldIR("ServiceId", "ServiceId", true),
                }
                : new[]
                {
                    new RuntimeIdentityFieldIR("ServiceId", "ServiceId", true),
                    new RuntimeIdentityFieldIR("OwnerModule", "ModuleId", false),
                };

            RuntimeQueryIR runtimeQuery = new RuntimeQueryIR(
                new RuntimeQueryId(15),
                "BattleServiceQuery",
                RuntimeQueryTargetKind.Service,
                indexedFields,
                new RuntimeQueryPolicyIR(true, false, DependencyPhase.Runtime),
                new ModuleId(8),
                new SourceLocationId(8));

            DiagnosticSeedIR[] diagnosticSeeds = reverseNestedOrder
                ? new[]
                {
                    new DiagnosticSeedIR("PROFILE_MISMATCH", "Profile Mismatch", new ModuleId(2), new SourceLocationId(2)),
                    new DiagnosticSeedIR("MISSING_DEPENDENCY", "Missing Dependency", new ModuleId(8), new SourceLocationId(5)),
                }
                : new[]
                {
                    new DiagnosticSeedIR("MISSING_DEPENDENCY", "Missing Dependency", new ModuleId(8), new SourceLocationId(5)),
                    new DiagnosticSeedIR("PROFILE_MISMATCH", "Profile Mismatch", new ModuleId(2), new SourceLocationId(2)),
                };

            return new KernelIR(
                new KernelIRHeader("KernelIR-Battle", 1, "TinnosukeGameLib", "Battle", "1.0.0", default, default),
                new KernelProfileIR("Battle", KernelProfileMask.Release, new AvailabilityIR(KernelProfileMask.Release, true, null)),
                reverseNestedOrder ? new[] { primaryModule, sharedModule } : new[] { sharedModule, primaryModule },
                new[] { scope },
                new[] { service },
                new[] { command },
                new[] { valueKey },
                new[] { lifecycle },
                new[] { runtimeQuery },
                new[] { dependency },
                sources,
                diagnosticSeeds);
        }

        static SourceLocationIR CreateGeneratedSourceLocation(string generatorName, string generatedFrom)
        {
            return new SourceLocationIR(new GeneratedSourceLocation(generatorName, generatedFrom, "Build"));
        }

        static KernelIR CreateUnitySourceKernelIR(string assetPath, string scenePath)
        {
            SourceLocationTable sources = new SourceLocationTable(new[]
            {
                new SourceLocationIR(new UnitySourceLocation("battle-guid", assetPath, 12001, scenePath, "Root/BattleService", "BattleServiceAuthoring", "service")),
                CreateGeneratedSourceLocation("SharedProjector", "SharedModule"),
                CreateGeneratedSourceLocation("ScopeProjector", "BattleScope"),
                CreateGeneratedSourceLocation("LifecycleProjector", "BattleLifecycle"),
                CreateGeneratedSourceLocation("CommandProjector", "BattleCommand"),
                CreateGeneratedSourceLocation("ValueProjector", "BattleValue"),
                CreateGeneratedSourceLocation("QueryProjector", "BattleQuery"),
            });

            ModuleIR primaryModule = new ModuleIR(
                new ModuleId(8),
                "BattleModule",
                ModuleKind.Feature,
                new ModuleVersion(1),
                new ModuleAvailabilityIR(new AvailabilityIR(KernelProfileMask.Release, true, null)),
                new SourceLocationId(1),
                new[] { new ModuleDependencyIR(new ModuleId(2), new SourceLocationId(1)) });
            ModuleIR sharedModule = new ModuleIR(
                new ModuleId(2),
                "SharedModule",
                ModuleKind.System,
                new ModuleVersion(1),
                new ModuleAvailabilityIR(new AvailabilityIR(KernelProfileMask.Release, true, null)),
                new SourceLocationId(2));

            ServiceIR service = new ServiceIR(
                new ServiceId(11),
                "BattleService",
                ServiceLifetimeKind.Singleton,
                new ModuleId(8),
                new[] { new ServiceContractIR("IBattleService", new SourceLocationId(1)) },
                Array.Empty<ServiceDependencyIR>(),
                ServiceFactoryKind.GeneratedFactory,
                new SourceLocationId(1));

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

            DependencyEdgeIR dependency = new DependencyEdgeIR(
                new DependencyEdgeId(1),
                new DependencyNodeIR(new ScopePlanId(101)),
                new DependencyNodeIR(new ServiceId(11)),
                DependencyKind.Requires,
                DependencyPhase.Boot,
                DependencyStrength.Required,
                new SourceLocationId(4));

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
                    LifecycleFailurePolicy.FailKernel,
                    true,
                    KernelProfileMask.None,
                    null);

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
                new[]
                {
                    new RuntimeIdentityFieldIR("ServiceId", "ServiceId", true),
                    new RuntimeIdentityFieldIR("OwnerModule", "ModuleId", false),
                },
                new RuntimeQueryPolicyIR(true, false, DependencyPhase.Runtime),
                new ModuleId(8),
                new SourceLocationId(7));

            return new KernelIR(
                new KernelIRHeader("KernelIR-Battle", 1, "TinnosukeGameLib", "Battle", "1.0.0", default, default),
                new KernelProfileIR("Battle", KernelProfileMask.Release, new AvailabilityIR(KernelProfileMask.Release, true, null)),
                new[] { sharedModule, primaryModule },
                new[] { scope },
                new[] { service },
                new[] { command },
                new[] { valueKey },
                new[] { lifecycle },
                new[] { runtimeQuery },
                new[] { dependency },
                sources,
                new[] { new DiagnosticSeedIR("MISSING_DEPENDENCY", "Missing Dependency", new ModuleId(8), new SourceLocationId(4)) });
        }
    }
}

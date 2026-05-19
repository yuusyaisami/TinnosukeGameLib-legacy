#nullable enable
using Game.Kernel.Diagnostics;
using Game.Kernel.IR;
using Game.Kernel.Validation;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class ProjectionValidationTests
    {
        [Test]
        public void Validate_UnknownProjectedServiceIdFails()
        {
            ProjectionValidationInput input = CreateInput(
                selectedArtifacts: new[] { ProjectionArtifactKind.ServiceGraph },
                mappings: new[]
                {
                    CreateMapping(new RuntimeIdentityRef(RuntimeIdentityKind.Service, 100), new RuntimeIdentityRef(RuntimeIdentityKind.Service, 999), 10, 6),
                },
                coverage: new[]
                {
                    new RuntimeIdentityRef(RuntimeIdentityKind.Module, 10),
                    new RuntimeIdentityRef(RuntimeIdentityKind.Service, 100),
                });

            ProjectionValidationReport report = ProjectionValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_PROJECTION_UNKNOWN_SERVICE_ID"));
            Assert.That(report.Issues[0].Category, Is.EqualTo(ValidationIssueCategory.Projection));
        }

        [Test]
        public void Validate_DebugMapCoverageMissingFails()
        {
            ProjectionValidationInput input = CreateInput(
                selectedArtifacts: new[] { ProjectionArtifactKind.CommandCatalog },
                mappings: new[]
                {
                    CreateMapping(new RuntimeIdentityRef(RuntimeIdentityKind.CommandType, 200), new RuntimeIdentityRef(RuntimeIdentityKind.CommandType, 200), 10, 7),
                },
                coverage: new[]
                {
                    new RuntimeIdentityRef(RuntimeIdentityKind.Module, 10),
                });

            ProjectionValidationReport report = ProjectionValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_DEBUGMAP_COVERAGE_MISSING"));
        }

        [Test]
        public void Validate_DuplicateMissingOwnerCoverageIsReportedOnce()
        {
            ProjectionValidationInput input = CreateInput(
                selectedArtifacts: new[] { ProjectionArtifactKind.CommandCatalog },
                mappings: new[]
                {
                    CreateMapping(new RuntimeIdentityRef(RuntimeIdentityKind.CommandType, 200), new RuntimeIdentityRef(RuntimeIdentityKind.CommandType, 200), 10, 7),
                    CreateMapping(new RuntimeIdentityRef(RuntimeIdentityKind.CommandType, 200), new RuntimeIdentityRef(RuntimeIdentityKind.CommandType, 200), 10, 7),
                },
                coverage: new[]
                {
                    new RuntimeIdentityRef(RuntimeIdentityKind.CommandType, 200),
                });

            ProjectionValidationReport report = ProjectionValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_DEBUGMAP_COVERAGE_MISSING"));
        }

        [Test]
        public void Validate_ValueSchemaMissingProjectionFails()
        {
            ProjectionValidationInput input = CreateInput(
                selectedArtifacts: new[] { ProjectionArtifactKind.ValueSchema },
                mappings: new ProjectionMappingIR[0],
                coverage: new[]
                {
                    new RuntimeIdentityRef(RuntimeIdentityKind.Module, 10),
                });

            ProjectionValidationReport report = ProjectionValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_PROJECTION_VALUE_SCHEMA_MISSING"));
        }

        [Test]
        public void Validate_CommandCatalogMissingExecutorAndPayloadSchemaProjectionFails()
        {
            ProjectionValidationInput input = CreateInput(
                selectedArtifacts: new[] { ProjectionArtifactKind.CommandCatalog },
                mappings: new[]
                {
                    CreateMapping(new RuntimeIdentityRef(RuntimeIdentityKind.CommandType, 200), new RuntimeIdentityRef(RuntimeIdentityKind.CommandType, 200), 10, 7),
                },
                coverage: new[]
                {
                    new RuntimeIdentityRef(RuntimeIdentityKind.Module, 10),
                    new RuntimeIdentityRef(RuntimeIdentityKind.CommandType, 200),
                });

            ProjectionValidationReport report = ProjectionValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(2));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_PROJECTION_COMMAND_EXECUTOR_MISSING"));
            Assert.That(report.Issues[1].Code, Is.EqualTo("DEP_PROJECTION_COMMAND_PAYLOAD_SCHEMA_MISSING"));
        }

        [Test]
        public void Validate_DroppedDependencyProvenanceFails()
        {
            ProjectionValidationInput input = CreateInput(
                selectedArtifacts: new[] { ProjectionArtifactKind.ServiceGraph },
                mappings: new[]
                {
                    CreateMapping(new RuntimeIdentityRef(RuntimeIdentityKind.Service, 100), new RuntimeIdentityRef(RuntimeIdentityKind.Service, 100), 10, 6, hasProvenance: false),
                },
                coverage: new[]
                {
                    new RuntimeIdentityRef(RuntimeIdentityKind.Module, 10),
                    new RuntimeIdentityRef(RuntimeIdentityKind.Service, 100),
                });

            ProjectionValidationReport report = ProjectionValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_PROJECTION_PROVENANCE_MISSING"));
        }

        [Test]
        public void Validate_CleanProjectionPasses()
        {
            ProjectionValidationInput input = CreateInput(
                selectedArtifacts: new[]
                {
                    ProjectionArtifactKind.ServiceGraph,
                    ProjectionArtifactKind.CommandCatalog,
                    ProjectionArtifactKind.ValueSchema,
                },
                mappings: new[]
                {
                    CreateMapping(new RuntimeIdentityRef(RuntimeIdentityKind.Service, 100), new RuntimeIdentityRef(RuntimeIdentityKind.Service, 100), 10, 6),
                    CreateMapping(new RuntimeIdentityRef(RuntimeIdentityKind.CommandType, 200), new RuntimeIdentityRef(RuntimeIdentityKind.CommandType, 200), 10, 7),
                    CreateMapping(new RuntimeIdentityRef(RuntimeIdentityKind.ValueKey, 300), new RuntimeIdentityRef(RuntimeIdentityKind.ValueSchema, 301), 10, 8),
                },
                coverage: new[]
                {
                    new RuntimeIdentityRef(RuntimeIdentityKind.Module, 10),
                    new RuntimeIdentityRef(RuntimeIdentityKind.Service, 100),
                    new RuntimeIdentityRef(RuntimeIdentityKind.CommandType, 200),
                    new RuntimeIdentityRef(RuntimeIdentityKind.ValueSchema, 301),
                });

            ProjectionValidationReport report = ProjectionValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Passed));
            Assert.That(report.Issues, Is.Empty);
        }

        static ProjectionValidationInput CreateInput(ProjectionArtifactKind[] selectedArtifacts, ProjectionMappingIR[] mappings, RuntimeIdentityRef[] coverage)
        {
            return new ProjectionValidationInput(
                "Development",
                KernelProfileMask.Development,
                CreateKernelIR(),
                selectedArtifacts,
                mappings,
                coverage);
        }

        static ProjectionMappingIR CreateMapping(RuntimeIdentityRef sourceIdentity, RuntimeIdentityRef projectedIdentity, int ownerModule, int sourceLocation, bool hasProvenance = true)
        {
            return new ProjectionMappingIR(sourceIdentity, projectedIdentity, new ModuleId(ownerModule), new SourceLocationId(sourceLocation), hasProvenance);
        }

        static KernelIR CreateKernelIR()
        {
            ModuleIR module = new ModuleIR(
                new ModuleId(10),
                "Core",
                ModuleKind.Feature,
                new ModuleVersion(1),
                new ModuleAvailabilityIR(new AvailabilityIR(KernelProfileMask.All, true, null)),
                new SourceLocationId(1));

            ServiceIR service = new ServiceIR(
                new ServiceId(100),
                "Service100",
                ServiceLifetimeKind.Singleton,
                new ModuleId(10),
                new ServiceContractIR[]
                {
                    new ServiceContractIR("IService100", new SourceLocationId(2)),
                },
                null,
                ServiceFactoryKind.GeneratedFactory,
                new SourceLocationId(2));

            CommandIR command = new CommandIR(
                new CommandTypeId(200),
                "Command200",
                "command.200",
                new CommandCategoryId(1),
                new ModuleId(10),
                new CommandPayloadSchemaRefIR(new CommandPayloadSchemaId(201), new SourceLocationId(4)),
                new CommandExecutorRefIR(new CommandExecutorId(202), new SourceLocationId(5)),
                null,
                new SourceLocationId(3));

            ValueKeyIR valueKey = new ValueKeyIR(
                new ValueKeyId(300),
                "value.current",
                "Value Current",
                ValueKind.Int,
                new ModuleId(10),
                new ValueSchemaRefIR(new ValueSchemaId(301), new SourceLocationId(7)),
                new SavePolicyIR(false, false, null),
                new SourceLocationId(6));

            SourceLocationTable sources = new SourceLocationTable(new[]
            {
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Build")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Service")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Command")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "CommandPayload")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "CommandExecutor")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "ValueKey")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "ValueSchema")),
            });

            KernelIRHeader header = new KernelIRHeader(
                "DOC",
                1,
                "TestProject",
                "Development",
                "1.0.0",
                new Hash128(1, 2, 3, 4),
                new Hash128(5, 6, 7, 8));

            KernelProfileIR profile = new KernelProfileIR("Development", KernelProfileMask.Development, new AvailabilityIR(KernelProfileMask.All, true, null));

            return new KernelIR(
                header,
                profile,
                new[] { module },
                new ScopeIR[0],
                new[] { service },
                new[] { command },
                new[] { valueKey },
                new LifecycleIR[0],
                new RuntimeQueryIR[0],
                new DependencyEdgeIR[0],
                sources);
        }
    }
}
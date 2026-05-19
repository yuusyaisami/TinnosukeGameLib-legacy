#nullable enable
using System;
using System.Collections.Generic;
using Game.Kernel.IR;
using Game.Kernel.Validation;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class DependencyValidatorTests
    {
        [Test]
        public void Validate_CleanInputPassesWithoutIssues()
        {
            DependencyValidationInput input = CreateBaselineInput();

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Passed));
            Assert.That(report.Issues, Is.Empty);
            Assert.That(report.Summary.ErrorCount, Is.EqualTo(0));
        }

        [Test]
        public void Validate_DuplicateServiceIdProducesStableIssue()
        {
            DependencyValidationInput input = CreateBaselineInput(
                services: new[]
                {
                    CreateService(101, 10, 2),
                    CreateService(101, 10, 3),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_SERVICE_DUPLICATE_ID"));
            Assert.That(report.Issues[0].Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(report.Issues[0].Category, Is.EqualTo(ValidationIssueCategory.LocalNode));
            Assert.That(report.Issues[0].Source.Value, Is.EqualTo(3));
            Assert.That(report.Issues[0].From.Kind, Is.EqualTo(DependencyNodeKind.Service));
            Assert.That(report.Issues[0].To.HasValue, Is.True);
        }

        [Test]
        public void Validate_DuplicateValueKeyIdProducesStableIssue()
        {
            DependencyValidationInput input = CreateBaselineInput(
                valueKeys: new[]
                {
                    CreateValueKey(201, "health.current", 10, 4),
                    CreateValueKey(201, "health.max", 10, 5),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_VALUE_KEY_DUPLICATE_ID"));
            Assert.That(report.Issues[0].Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(report.Issues[0].From.Kind, Is.EqualTo(DependencyNodeKind.ValueKey));
        }

        [Test]
        public void Validate_DuplicateStableKeyProducesStableIssue()
        {
            DependencyValidationInput input = CreateBaselineInput(
                valueKeys: new[]
                {
                    CreateValueKey(201, "health.current", 10, 4),
                    CreateValueKey(202, "health.current", 10, 5),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_VALUE_STABLE_KEY_DUPLICATE"));
        }

        [Test]
        public void Validate_DuplicateLifecycleStepIdProducesStableIssue()
        {
            DependencyValidationInput input = CreateBaselineInput(
                lifecycles: new[]
                {
                    CreateLifecycle(301, 10, 6, 401, 7),
                    CreateLifecycle(302, 10, 8, 401, 9),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_LIFECYCLE_STEP_DUPLICATE_ID"));
            Assert.That(report.Issues[0].From.Kind, Is.EqualTo(DependencyNodeKind.LifecycleStep));
        }

        [Test]
        public void Validate_MissingRequiredModuleProducesStableIssue()
        {
            DependencyValidationInput input = CreateBaselineInput(
                modules: new[]
                {
                    CreateModule(10, 1, requiredModules: new[]
                    {
                        new ModuleDependencyIR(new ModuleId(20), new SourceLocationId(2)),
                    }),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_MODULE_MISSING"));
            Assert.That(report.Issues[0].Category, Is.EqualTo(ValidationIssueCategory.CrossNode));
            Assert.That(report.Issues[0].From.Kind, Is.EqualTo(DependencyNodeKind.Module));
            Assert.That(report.Issues[0].To!.Value.Kind, Is.EqualTo(DependencyNodeKind.Module));
        }

        [Test]
        public void Validate_OptionalMissingModuleWithDisableContributionPasses()
        {
            DependencyValidationInput input = CreateBaselineInput(
                modules: new[]
                {
                    CreateModule(
                        10,
                        1,
                        optionalModules: new[]
                        {
                            new ModuleDependencyIR(
                                new ModuleId(20),
                                new SourceLocationId(2),
                                OptionalDependencyAbsenceBehavior.DisableContribution,
                                disabledContribution: "tooltip.runtime"),
                        }),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Passed));
            Assert.That(report.Issues, Is.Empty);
        }

        [Test]
        public void Validate_OptionalMissingModuleWithoutBehaviorFails()
        {
            DependencyValidationInput input = CreateBaselineInput(
                modules: new[]
                {
                    CreateModule(
                        10,
                        1,
                        optionalModules: new[]
                        {
                            new ModuleDependencyIR(new ModuleId(20), new SourceLocationId(2)),
                        }),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_OPTIONAL_ABSENCE_BEHAVIOR_MISSING"));
        }

        [Test]
        public void Validate_OptionalAlternativeMissingFails()
        {
            DependencyValidationInput input = CreateBaselineInput(
                modules: new[]
                {
                    CreateModule(
                        10,
                        1,
                        optionalModules: new[]
                        {
                            new ModuleDependencyIR(
                                new ModuleId(20),
                                new SourceLocationId(2),
                                OptionalDependencyAbsenceBehavior.UseExplicitAlternative,
                                alternativeModuleId: new ModuleId(30)),
                        }),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_OPTIONAL_ALTERNATIVE_INVALID"));
        }

        [Test]
        public void Validate_OptionalAlternativeUnavailableForSelectedProfileFails()
        {
            DependencyValidationInput input = CreateBaselineInput(
                modules: new[]
                {
                    CreateModule(
                        10,
                        1,
                        optionalModules: new[]
                        {
                            new ModuleDependencyIR(
                                new ModuleId(20),
                                new SourceLocationId(2),
                                OptionalDependencyAbsenceBehavior.UseExplicitAlternative,
                                alternativeModuleId: new ModuleId(30)),
                        }),
                    CreateModule(30, 3, availability: new ModuleAvailabilityIR(new AvailabilityIR(KernelProfileMask.Release, true, null))),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_OPTIONAL_ALTERNATIVE_INVALID"));
        }

        [Test]
        public void Validate_OptionalEmitWarningWithoutExtraMetadataPasses()
        {
            DependencyValidationInput input = CreateBaselineInput(
                modules: new[]
                {
                    CreateModule(
                        10,
                        1,
                        optionalModules: new[]
                        {
                            new ModuleDependencyIR(
                                new ModuleId(20),
                                new SourceLocationId(2),
                                OptionalDependencyAbsenceBehavior.EmitWarning),
                        }),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Passed));
            Assert.That(report.Issues, Is.Empty);
        }

        [Test]
        public void Validate_OptionalProfileSpecificErrorInSelectedProfileFails()
        {
            DependencyValidationInput input = CreateBaselineInput(
                modules: new[]
                {
                    CreateModule(
                        10,
                        1,
                        optionalModules: new[]
                        {
                            new ModuleDependencyIR(
                                new ModuleId(20),
                                new SourceLocationId(2),
                                OptionalDependencyAbsenceBehavior.ProfileSpecificError,
                                profileSpecificErrorProfiles: KernelProfileMask.Development),
                        }),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_OPTIONAL_PROFILE_ERROR"));
        }

        [Test]
        public void Validate_BuildCycleRejected()
        {
            DependencyValidationInput input = CreateBaselineInput(
                services: new[]
                {
                    CreateService(101, 10, 2),
                    CreateService(102, 10, 3),
                },
                dependencies: new[]
                {
                    CreateDependency(700, DependencyNodeKind.Service, 101, DependencyNodeKind.Service, 102, DependencyPhase.Build, DependencyStrength.Required, 12),
                    CreateDependency(701, DependencyNodeKind.Service, 102, DependencyNodeKind.Service, 101, DependencyPhase.Build, DependencyStrength.Required, 13),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_CYCLE_BUILD"));
            Assert.That(report.Issues[0].Phase, Is.EqualTo(ValidationPhase.Build));
        }

        [Test]
        public void Validate_BootCycleRejected()
        {
            DependencyValidationInput input = CreateBaselineInput(
                services: new[]
                {
                    CreateService(101, 10, 2),
                    CreateService(102, 10, 3),
                },
                dependencies: new[]
                {
                    CreateDependency(700, DependencyNodeKind.Service, 101, DependencyNodeKind.Service, 102, DependencyPhase.Boot, DependencyStrength.Required, 12),
                    CreateDependency(701, DependencyNodeKind.Service, 102, DependencyNodeKind.Service, 101, DependencyPhase.Boot, DependencyStrength.Required, 13),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_CYCLE_BOOT"));
            Assert.That(report.Issues[0].Phase, Is.EqualTo(ValidationPhase.Boot));
        }

        [Test]
        public void Validate_AcquireCycleRejected()
        {
            DependencyValidationInput input = CreateBaselineInput(
                services: new[]
                {
                    CreateService(101, 10, 2),
                    CreateService(102, 10, 3),
                },
                dependencies: new[]
                {
                    CreateDependency(700, DependencyNodeKind.Service, 101, DependencyNodeKind.Service, 102, DependencyPhase.Acquire, DependencyStrength.Required, 12),
                    CreateDependency(701, DependencyNodeKind.Service, 102, DependencyNodeKind.Service, 101, DependencyPhase.Acquire, DependencyStrength.Required, 13),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_CYCLE_ACQUIRE"));
            Assert.That(report.Issues[0].Phase, Is.EqualTo(ValidationPhase.Acquire));
        }

        [Test]
        public void Validate_RuntimeLazyHandleCyclePasses()
        {
            DependencyValidationInput input = CreateBaselineInput(
                services: new[]
                {
                    CreateService(101, 10, 2),
                    CreateService(102, 10, 3),
                },
                dependencies: new[]
                {
                    CreateDependency(700, DependencyNodeKind.Service, 101, DependencyNodeKind.Service, 102, DependencyPhase.Runtime, DependencyStrength.Required, 12, RuntimeCycleMediationKind.LazyHandle, DependencyKind.References),
                    CreateDependency(701, DependencyNodeKind.Service, 102, DependencyNodeKind.Service, 101, DependencyPhase.Runtime, DependencyStrength.Required, 13, RuntimeCycleMediationKind.LazyHandle, DependencyKind.References),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Passed));
            Assert.That(report.Issues, Is.Empty);
        }

        [Test]
        public void Validate_RuntimeEventChannelCyclePasses()
        {
            DependencyValidationInput input = CreateBaselineInput(
                services: new[]
                {
                    CreateService(101, 10, 2),
                    CreateService(102, 10, 3),
                },
                dependencies: new[]
                {
                    CreateDependency(700, DependencyNodeKind.Service, 101, DependencyNodeKind.Service, 102, DependencyPhase.Runtime, DependencyStrength.Required, 12, RuntimeCycleMediationKind.EventChannel, DependencyKind.Triggers),
                    CreateDependency(701, DependencyNodeKind.Service, 102, DependencyNodeKind.Service, 101, DependencyPhase.Runtime, DependencyStrength.Required, 13, RuntimeCycleMediationKind.EventChannel, DependencyKind.Triggers),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Passed));
            Assert.That(report.Issues, Is.Empty);
        }

        [Test]
        public void Validate_RuntimeRequiredCycleRejected()
        {
            DependencyValidationInput input = CreateBaselineInput(
                services: new[]
                {
                    CreateService(101, 10, 2),
                    CreateService(102, 10, 3),
                },
                dependencies: new[]
                {
                    CreateDependency(700, DependencyNodeKind.Service, 101, DependencyNodeKind.Service, 102, DependencyPhase.Runtime, DependencyStrength.Required, 12),
                    CreateDependency(701, DependencyNodeKind.Service, 102, DependencyNodeKind.Service, 101, DependencyPhase.Runtime, DependencyStrength.Required, 13),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_CYCLE_RUNTIME_REQUIRED"));
            Assert.That(report.Issues[0].Phase, Is.EqualTo(ValidationPhase.Runtime));
        }

        [Test]
        public void Validate_RuntimeOptionalCycleDoesNotProduceRequiredDiagnostic()
        {
            DependencyValidationInput input = CreateBaselineInput(
                services: new[]
                {
                    CreateService(101, 10, 2),
                    CreateService(102, 10, 3),
                },
                dependencies: new[]
                {
                    CreateDependency(700, DependencyNodeKind.Service, 101, DependencyNodeKind.Service, 102, DependencyPhase.Runtime, DependencyStrength.Optional, 12),
                    CreateDependency(701, DependencyNodeKind.Service, 102, DependencyNodeKind.Service, 101, DependencyPhase.Runtime, DependencyStrength.Optional, 13),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Passed));
            Assert.That(report.Issues, Is.Empty);
        }

        [Test]
        public void Validate_RuntimeCycleWithMediationButWrongKindFails()
        {
            DependencyValidationInput input = CreateBaselineInput(
                services: new[]
                {
                    CreateService(101, 10, 2),
                    CreateService(102, 10, 3),
                },
                dependencies: new[]
                {
                    CreateDependency(700, DependencyNodeKind.Service, 101, DependencyNodeKind.Service, 102, DependencyPhase.Runtime, DependencyStrength.Required, 12, RuntimeCycleMediationKind.LazyHandle),
                    CreateDependency(701, DependencyNodeKind.Service, 102, DependencyNodeKind.Service, 101, DependencyPhase.Runtime, DependencyStrength.Required, 13, RuntimeCycleMediationKind.LazyHandle),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_CYCLE_RUNTIME_REQUIRED"));
        }

        [Test]
        public void Validate_CycleDetectionIsPhasePartitioned()
        {
            DependencyValidationInput input = CreateBaselineInput(
                services: new[]
                {
                    CreateService(101, 10, 2),
                    CreateService(102, 10, 3),
                },
                dependencies: new[]
                {
                    CreateDependency(700, DependencyNodeKind.Service, 101, DependencyNodeKind.Service, 102, DependencyPhase.Build, DependencyStrength.Required, 12),
                    CreateDependency(701, DependencyNodeKind.Service, 102, DependencyNodeKind.Service, 101, DependencyPhase.Build, DependencyStrength.Required, 13),
                    CreateDependency(702, DependencyNodeKind.Service, 101, DependencyNodeKind.Service, 102, DependencyPhase.Runtime, DependencyStrength.Required, 14, RuntimeCycleMediationKind.LazyHandle, DependencyKind.References),
                    CreateDependency(703, DependencyNodeKind.Service, 102, DependencyNodeKind.Service, 101, DependencyPhase.Runtime, DependencyStrength.Required, 15, RuntimeCycleMediationKind.LazyHandle, DependencyKind.References),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_CYCLE_BUILD"));
        }

        [Test]
        public void Validate_CycleIssuesOrderDeterministicallyWithOtherIssues()
        {
            DependencyValidationInput input = CreateBaselineInput(
                services: new[]
                {
                    CreateService(101, 10, 3),
                    CreateService(101, 10, 2),
                },
                dependencies: new[]
                {
                    CreateDependency(700, DependencyNodeKind.Service, 101, DependencyNodeKind.Service, 101, DependencyPhase.Build, DependencyStrength.Required, 12),
                });

            DependencyValidationReport first = DependencyValidator.Validate(input);
            DependencyValidationReport second = DependencyValidator.Validate(input);

            Assert.That(first.Issues, Has.Count.EqualTo(2));
            Assert.That(second.Issues, Has.Count.EqualTo(2));
            Assert.That(first.Issues[0].Code, Is.EqualTo(second.Issues[0].Code));
            Assert.That(first.Issues[1].Code, Is.EqualTo(second.Issues[1].Code));
            Assert.That(first.Issues[0].Source.Value, Is.EqualTo(second.Issues[0].Source.Value));
            Assert.That(first.Issues[1].Source.Value, Is.EqualTo(second.Issues[1].Source.Value));
        }

        [Test]
        public void Validate_OptionalProfileSpecificErrorOutsideSelectedProfilePasses()
        {
            DependencyValidationInput input = CreateBaselineInput(
                modules: new[]
                {
                    CreateModule(
                        10,
                        1,
                        optionalModules: new[]
                        {
                            new ModuleDependencyIR(
                                new ModuleId(20),
                                new SourceLocationId(2),
                                OptionalDependencyAbsenceBehavior.ProfileSpecificError,
                                profileSpecificErrorProfiles: KernelProfileMask.Release),
                        }),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Passed));
            Assert.That(report.Issues, Is.Empty);
        }

        [Test]
        public void Validate_MissingScopeParentProducesStableIssue()
        {
            DependencyValidationInput input = CreateBaselineInput(
                scopes: new[]
                {
                    CreateScope(301, 401, ScopeKind.Child, 10, 7, parentAuthoringId: 999),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_SCOPE_PARENT_MISSING"));
            Assert.That(report.Issues[0].Category, Is.EqualTo(ValidationIssueCategory.CrossNode));
            Assert.That(report.Issues[0].From.Kind, Is.EqualTo(DependencyNodeKind.Scope));
        }

        [Test]
        public void Validate_InvalidScopeParentKindProducesStableIssue()
        {
            DependencyValidationInput input = CreateBaselineInput(
                scopes: new[]
                {
                    CreateScope(301, 401, ScopeKind.Detached, 10, 7),
                    CreateScope(302, 402, ScopeKind.Child, 10, 8, parentAuthoringId: 301),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_SCOPE_PARENT_KIND_INVALID"));
            Assert.That(report.Issues[0].To!.Value.Kind, Is.EqualTo(DependencyNodeKind.Scope));
        }

        [Test]
        public void Validate_MissingLifecycleServiceTargetProducesStableIssue()
        {
            DependencyValidationInput input = CreateBaselineInput(
                lifecycles: new[]
                {
                    CreateLifecycle(301, 10, 5, 401, 6, new LifecycleTargetRefIR(new ServiceId(999))),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_LIFECYCLE_TARGET_SERVICE_MISSING"));
            Assert.That(report.Issues[0].Category, Is.EqualTo(ValidationIssueCategory.CrossNode));
        }

        [Test]
        public void Validate_MissingLifecycleRuntimeQueryTargetProducesStableIssue()
        {
            DependencyValidationInput input = CreateBaselineInput(
                lifecycles: new[]
                {
                    CreateLifecycle(301, 10, 5, 401, 6, new LifecycleTargetRefIR(new RuntimeQueryId(999))),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_LIFECYCLE_TARGET_INVALID"));
            Assert.That(report.Issues[0].Category, Is.EqualTo(ValidationIssueCategory.CrossNode));
        }

        [Test]
        public void Validate_MissingCommandExecutorProducesStableIssue()
        {
            DependencyValidationInput input = CreateBaselineInput(
                commandExecutors: Array.Empty<CommandExecutorId>(),
                commandPayloadSchemas: new[] { new CommandPayloadSchemaId(1) });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_COMMAND_EXECUTOR_MISSING"));
            Assert.That(report.Issues[0].Category, Is.EqualTo(ValidationIssueCategory.LocalNode));
        }

        [Test]
        public void Validate_MissingCommandPayloadSchemaProducesStableIssue()
        {
            DependencyValidationInput input = CreateBaselineInput(
                commandExecutors: new[] { new CommandExecutorId(1) },
                commandPayloadSchemas: Array.Empty<CommandPayloadSchemaId>());

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_COMMAND_PAYLOAD_SCHEMA_MISSING"));
            Assert.That(report.Issues[0].Category, Is.EqualTo(ValidationIssueCategory.LocalNode));
        }

        [Test]
        public void Validate_CommandUsesMissingValueKeyProducesStableIssue()
        {
            DependencyValidationInput input = CreateBaselineInput(
                commands: new[]
                {
                    CreateCommand(
                        501,
                        10,
                        3,
                        dependencies: new[]
                        {
                            new CommandDependencyIR(new DependencyNodeIR(new ValueKeyId(999)), DependencyStrength.Required, new SourceLocationId(11)),
                        }),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_VALUE_KEY_MISSING"));
            Assert.That(report.Issues[0].Category, Is.EqualTo(ValidationIssueCategory.CrossNode));
        }

        [Test]
        public void Validate_CommandUsesMissingRuntimeQueryProducesStableIssue()
        {
            DependencyValidationInput input = CreateBaselineInput(
                commands: new[]
                {
                    CreateCommand(
                        501,
                        10,
                        3,
                        dependencies: new[]
                        {
                            new CommandDependencyIR(new DependencyNodeIR(new RuntimeQueryId(999)), DependencyStrength.Required, new SourceLocationId(11)),
                        }),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_RUNTIME_QUERY_MISSING"));
            Assert.That(report.Issues[0].Category, Is.EqualTo(ValidationIssueCategory.CrossNode));
        }

        [Test]
        public void Validate_CommandDependencyWithUnsupportedTargetKindProducesWrongDomainIssue()
        {
            DependencyValidationInput input = CreateBaselineInput(
                commands: new[]
                {
                    CreateCommand(
                        501,
                        10,
                        3,
                        dependencies: new[]
                        {
                            new CommandDependencyIR(new DependencyNodeIR(new ScopePlanId(401)), DependencyStrength.Required, new SourceLocationId(11)),
                        }),
                },
                scopes: new[]
                {
                    CreateScope(301, 401, ScopeKind.Root, 10, 7),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_IDENTITY_DOMAIN_INVALID"));
            Assert.That(report.Issues[0].Category, Is.EqualTo(ValidationIssueCategory.LocalEdge));
        }

        [Test]
        public void Validate_ServiceDependencyWithUnsupportedTargetKindProducesWrongDomainIssue()
        {
            DependencyValidationInput input = CreateBaselineInput(
                services: new[]
                {
                    CreateService(
                        101,
                        10,
                        2,
                        dependencies: new[]
                        {
                            new ServiceDependencyIR(new DependencyNodeIR(new LifecycleStepId(401)), DependencyStrength.Required, new SourceLocationId(11)),
                        }),
                },
                lifecycles: new[]
                {
                    CreateLifecycle(301, 10, 5, 401, 6),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_IDENTITY_DOMAIN_INVALID"));
            Assert.That(report.Issues[0].Category, Is.EqualTo(ValidationIssueCategory.LocalEdge));
        }

        [Test]
        public void Validate_InvalidDependencyKindProducesStableIssue()
        {
            DependencyValidationInput input = CreateBaselineInput(
                scopes: new[]
                {
                    CreateScope(301, 401, ScopeKind.Root, 10, 7),
                    CreateScope(302, 402, ScopeKind.Child, 10, 8, parentAuthoringId: 301),
                },
                dependencies: new[]
                {
                    new DependencyEdgeIR(
                        new DependencyEdgeId(700),
                        new DependencyNodeIR(new ScopePlanId(402)),
                        new DependencyNodeIR(new ScopePlanId(401)),
                        DependencyKind.Requires,
                        DependencyPhase.Build,
                        DependencyStrength.Required,
                        new SourceLocationId(12)),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_DEPENDENCY_KIND_INVALID"));
            Assert.That(report.Issues[0].Category, Is.EqualTo(ValidationIssueCategory.LocalEdge));
        }

        [Test]
        public void Validate_ScopeServiceDependencyWithWrongKindProducesStableIssue()
        {
            DependencyValidationInput input = CreateBaselineInput(
                scopes: new[]
                {
                    CreateScope(301, 401, ScopeKind.Root, 10, 7),
                },
                dependencies: new[]
                {
                    new DependencyEdgeIR(
                        new DependencyEdgeId(700),
                        new DependencyNodeIR(new ScopePlanId(401)),
                        new DependencyNodeIR(new ServiceId(101)),
                        DependencyKind.Owns,
                        DependencyPhase.Build,
                        DependencyStrength.Required,
                        new SourceLocationId(12)),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_DEPENDENCY_KIND_INVALID"));
        }

        [Test]
        public void Validate_UnsupportedDependencyNodePairProducesStableIssue()
        {
            DependencyValidationInput input = CreateBaselineInput(
                dependencies: new[]
                {
                    new DependencyEdgeIR(
                        new DependencyEdgeId(700),
                        new DependencyNodeIR(new LifecycleStepId(401)),
                        new DependencyNodeIR(new ServiceId(101)),
                        DependencyKind.Triggers,
                        DependencyPhase.Build,
                        DependencyStrength.Required,
                        new SourceLocationId(12)),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_DEPENDENCY_KIND_INVALID"));
            Assert.That(report.Issues[0].Category, Is.EqualTo(ValidationIssueCategory.LocalEdge));
        }

        [Test]
        public void Validate_AllowedDependencyKindDoesNotProduceIssue()
        {
            DependencyValidationInput input = CreateBaselineInput(
                modules: new[]
                {
                    CreateModule(10, 1, requiredModules: new[]
                    {
                        new ModuleDependencyIR(new ModuleId(20), new SourceLocationId(2)),
                    }),
                    CreateModule(20, 2),
                },
                dependencies: new[]
                {
                    new DependencyEdgeIR(
                        new DependencyEdgeId(700),
                        new DependencyNodeIR(new ModuleId(10)),
                        new DependencyNodeIR(new ModuleId(20)),
                        DependencyKind.Requires,
                        DependencyPhase.Build,
                        DependencyStrength.Required,
                        new SourceLocationId(12)),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Issues, Is.Empty);
        }

        [Test]
        public void Validate_ServiceRuntimeQueryRequiresKindDoesNotProduceIssue()
        {
            DependencyValidationInput input = CreateBaselineInput(
                dependencies: new[]
                {
                    new DependencyEdgeIR(
                        new DependencyEdgeId(700),
                        new DependencyNodeIR(new ServiceId(101)),
                        new DependencyNodeIR(new RuntimeQueryId(601)),
                        DependencyKind.Requires,
                        DependencyPhase.Build,
                        DependencyStrength.Required,
                        new SourceLocationId(12)),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Issues, Is.Empty);
        }

        [TestCase(DependencyNodeKind.Command, 501, DependencyNodeKind.Service, 101)]
        [TestCase(DependencyNodeKind.RuntimeQuery, 601, DependencyNodeKind.ValueKey, 201)]
        [TestCase(DependencyNodeKind.LifecycleStep, 401, DependencyNodeKind.RuntimeQuery, 601)]
        public void Validate_WrongDomainDependencyProducesStableIssue(DependencyNodeKind fromKind, int fromId, DependencyNodeKind toKind, int toId)
        {
            DependencyValidationInput input = CreateBaselineInput(
                dependencies: new[]
                {
                    new DependencyEdgeIR(
                        new DependencyEdgeId(700),
                        CreateNode(fromKind, fromId),
                        CreateNode(toKind, toId),
                        DependencyKind.Requires,
                        DependencyPhase.Build,
                        DependencyStrength.Required,
                        new SourceLocationId(12)),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_IDENTITY_DOMAIN_INVALID"));
            Assert.That(report.Issues[0].Category, Is.EqualTo(ValidationIssueCategory.LocalEdge));
            Assert.That(report.Issues[0].OwnerModule.Value, Is.EqualTo(10));
            Assert.That(report.Issues[0].From.Kind, Is.EqualTo(fromKind));
            Assert.That(report.Issues[0].To!.Value.Kind, Is.EqualTo(toKind));
        }

        [Test]
        public void Validate_OrdersIssuesDeterministically()
        {
            DependencyValidationInput input = CreateBaselineInput(
                services: new[]
                {
                    CreateService(101, 10, 3),
                    CreateService(101, 10, 2),
                },
                valueKeys: new[]
                {
                    CreateValueKey(201, "health.current", 10, 5),
                    CreateValueKey(202, "health.current", 10, 4),
                });

            DependencyValidationReport first = DependencyValidator.Validate(input);
            DependencyValidationReport second = DependencyValidator.Validate(input);

            Assert.That(first.Issues, Has.Count.EqualTo(2));
            Assert.That(second.Issues, Has.Count.EqualTo(2));
            Assert.That(first.Issues[0].Code, Is.EqualTo(second.Issues[0].Code));
            Assert.That(first.Issues[1].Code, Is.EqualTo(second.Issues[1].Code));
            Assert.That(first.Issues[0].Source.Value, Is.EqualTo(second.Issues[0].Source.Value));
            Assert.That(first.Issues[1].Source.Value, Is.EqualTo(second.Issues[1].Source.Value));
        }

        [Test]
        public void Validate_DevelopmentRuntimeAdapterIsWarningVisible()
        {
            DependencyValidationInput input = CreateBaselineInput(
                selectedProfile: "Development",
                modules: new[]
                {
                    CreateModule(10, 1),
                    CreateModule(20, 2, kind: ModuleKind.MigrationAdapter, legacyCompat: CreateLegacyCompat(LegacyCompatKind.RuntimeAdapter, diagnosticsCode: "LEGACY_RUNTIME_ADAPTER_USED", removalPolicy: "Remove after ServiceGraph migration")),
                },
                services: new[]
                {
                    CreateService(101, 10, 3),
                    CreateService(201, 20, 8),
                },
                sources: CreateSources(10, 2, 8));

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.PassedWithWarnings));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("LEGACY_RUNTIME_ADAPTER_USED"));
            Assert.That(report.Issues[0].Severity, Is.EqualTo(ValidationSeverity.Warning));
        }

        [Test]
        public void Validate_CoreDependingOnLegacyBridgeFails()
        {
            DependencyValidationInput input = CreateBaselineInput(
                modules: new[]
                {
                    CreateModule(10, 1, requiredModules: new[] { new ModuleDependencyIR(new ModuleId(20), new SourceLocationId(5)) }),
                    CreateModule(20, 2, kind: ModuleKind.MigrationAdapter, legacyCompat: CreateLegacyCompat(LegacyCompatKind.RuntimeAdapter, diagnosticsCode: "LEGACY_RUNTIME_ADAPTER_USED", removalPolicy: "Remove after migration")),
                },
                services: new[]
                {
                    CreateService(101, 10, 3),
                    CreateService(201, 20, 8),
                },
                sources: CreateSources(10, 2, 8));

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Some.Matches<DependencyValidationIssue>(issue => issue.Code == "LEGACY_CORE_DEPENDENCY_FORBIDDEN"));
        }

        [Test]
        public void Validate_ForbiddenFallbackFails()
        {
            DependencyValidationInput input = CreateBaselineInput(
                modules: new[]
                {
                    CreateModule(10, 1),
                    CreateModule(20, 2, kind: ModuleKind.MigrationAdapter, legacyCompat: CreateLegacyCompat(LegacyCompatKind.ForbiddenFallback, diagnosticsCode: "LEGACY_FALLBACK_FORBIDDEN", removalPolicy: "Never ship")),
                },
                services: new[]
                {
                    CreateService(101, 10, 3),
                    CreateService(201, 20, 8),
                },
                sources: CreateSources(10, 2, 8));

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Some.Matches<DependencyValidationIssue>(issue => issue.Code == "LEGACY_FALLBACK_FORBIDDEN"));
        }

        [Test]
        public void Validate_RuntimeAdapterInReleaseFails()
        {
            DependencyValidationInput input = CreateBaselineInput(
                selectedProfile: "Release",
                modules: new[]
                {
                    CreateModule(10, 1),
                    CreateModule(20, 2, kind: ModuleKind.MigrationAdapter, availability: new ModuleAvailabilityIR(new AvailabilityIR(KernelProfileMask.Release, true, null)), legacyCompat: CreateLegacyCompat(LegacyCompatKind.RuntimeAdapter, diagnosticsCode: "LEGACY_RUNTIME_ADAPTER_USED", removalPolicy: "Remove before release")),
                },
                services: new[]
                {
                    CreateService(101, 10, 3),
                    CreateService(201, 20, 8),
                },
                sources: CreateSources(10, 2, 8));

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Some.Matches<DependencyValidationIssue>(issue => issue.Code == "LEGACY_PROFILE_FORBIDDEN"));
        }

        [Test]
        public void Validate_LegacyAdapterWithoutDiagnosticsFails()
        {
            DependencyValidationInput input = CreateBaselineInput(
                modules: new[]
                {
                    CreateModule(10, 1),
                    CreateModule(20, 2, kind: ModuleKind.MigrationAdapter, legacyCompat: CreateLegacyCompat(LegacyCompatKind.RuntimeAdapter, diagnosticsCode: null, removalPolicy: "Remove after migration")),
                },
                services: new[]
                {
                    CreateService(101, 10, 3),
                    CreateService(201, 20, 8),
                },
                sources: CreateSources(10, 2, 8));

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Some.Matches<DependencyValidationIssue>(issue => issue.Code == "LEGACY_ADAPTER_DIAGNOSTICS_MISSING"));
        }

        [Test]
        public void Validate_LegacyAdapterWithoutRemovalPolicyFails()
        {
            DependencyValidationInput input = CreateBaselineInput(
                modules: new[]
                {
                    CreateModule(10, 1),
                    CreateModule(20, 2, kind: ModuleKind.MigrationAdapter, legacyCompat: CreateLegacyCompat(LegacyCompatKind.RuntimeAdapter, diagnosticsCode: "LEGACY_RUNTIME_ADAPTER_USED", removalPolicy: null)),
                },
                services: new[]
                {
                    CreateService(101, 10, 3),
                    CreateService(201, 20, 8),
                },
                sources: CreateSources(10, 2, 8));

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Some.Matches<DependencyValidationIssue>(issue => issue.Code == "LEGACY_ADAPTER_REMOVAL_POLICY_MISSING"));
        }

        [Test]
        public void Validate_NonAdapterOwningLegacySourceFails()
        {
            DependencyValidationInput input = CreateBaselineInput(
                modules: new[]
                {
                    CreateModule(10, 1),
                },
                services: new[]
                {
                    CreateService(101, 10, 2),
                },
                sources: CreateSources(6, 2));

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Some.Matches<DependencyValidationIssue>(issue => issue.Code == "LEGACY_RESOLVER_COMPONENT_FALLBACK_FORBIDDEN"));
        }

        [Test]
        public void Validate_NonAdapterOwningLegacyRuntimeQueryFailsWithBoundarySpecificCode()
        {
            DependencyValidationInput input = CreateBaselineInput(
                modules: new[]
                {
                    CreateModule(10, 1),
                },
                runtimeQueries: new[]
                {
                    CreateRuntimeQuery(601, 10, 2),
                },
                sources: CreateSources(6, 2));

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Some.Matches<DependencyValidationIssue>(issue => issue.Code == "LEGACY_RUNTIME_QUERY_LEGACY_LOOKUP_FORBIDDEN"));
        }

        [Test]
        public void KernelIR_StillRejectsDuplicateServiceIdsDirectly()
        {
            SourceLocationTable sources = CreateSources(10);
            ArgumentException exception = Assert.Throws<ArgumentException>(() => new KernelIR(
                new KernelIRHeader("KernelIR-Test", 1, "TinnosukeGameLib", "Development", "1.0.0", default, default),
                new KernelProfileIR("Development", KernelProfileMask.Development, new AvailabilityIR(KernelProfileMask.Development, true, null)),
                new[] { CreateModule(10, 1) },
                Array.Empty<ScopeIR>(),
                new[]
                {
                    CreateService(101, 10, 2),
                    CreateService(101, 10, 3),
                },
                Array.Empty<CommandIR>(),
                Array.Empty<ValueKeyIR>(),
                Array.Empty<LifecycleIR>(),
                Array.Empty<RuntimeQueryIR>(),
                Array.Empty<DependencyEdgeIR>(),
                sources));

            Assert.That(exception, Is.Not.Null);
            Assert.That(exception!.Message, Does.Contain("unique service identities"));
        }

        [Test]
        public void Validate_KernelIRBuildCycleRejectedOnNormalPath()
        {
            SourceLocationTable sources = CreateSources(10);
            KernelIR kernelIR = new KernelIR(
                new KernelIRHeader("KernelIR-Test", 1, "TinnosukeGameLib", "Development", "1.0.0", default, default),
                new KernelProfileIR("Development", KernelProfileMask.Development, new AvailabilityIR(KernelProfileMask.Development, true, null)),
                new[] { CreateModule(10, 1) },
                Array.Empty<ScopeIR>(),
                new[]
                {
                    CreateService(101, 10, 2),
                    CreateService(102, 10, 3),
                },
                new[] { CreateCommand(501, 10, 4) },
                new[] { CreateValueKey(201, "health.current", 10, 5) },
                new[] { CreateLifecycle(301, 10, 6, 401, 7) },
                new[] { CreateRuntimeQuery(601, 10, 8) },
                new[]
                {
                    CreateDependency(700, DependencyNodeKind.Service, 101, DependencyNodeKind.Service, 102, DependencyPhase.Build, DependencyStrength.Required, 9),
                    CreateDependency(701, DependencyNodeKind.Service, 102, DependencyNodeKind.Service, 101, DependencyPhase.Build, DependencyStrength.Required, 10),
                },
                sources);

            DependencyValidationReport report = DependencyValidator.Validate(kernelIR);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_CYCLE_BUILD"));
        }

        [Test]
        public void Validate_KernelIRLegacyLeakageRejectedOnNormalPath()
        {
            SourceLocationTable sources = CreateSources(10, 2, 4);
            KernelIR kernelIR = new KernelIR(
                new KernelIRHeader("KernelIR-Test", 1, "TinnosukeGameLib", "Development", "1.0.0", default, default),
                new KernelProfileIR("Development", KernelProfileMask.Development, new AvailabilityIR(KernelProfileMask.Development, true, null)),
                new[]
                {
                    CreateModule(10, 1, requiredModules: new[] { new ModuleDependencyIR(new ModuleId(20), new SourceLocationId(5)) }),
                    CreateModule(20, 2, kind: ModuleKind.MigrationAdapter, legacyCompat: CreateLegacyCompat(LegacyCompatKind.RuntimeAdapter, diagnosticsCode: "LEGACY_RUNTIME_ADAPTER_USED", removalPolicy: "Remove after migration")),
                },
                Array.Empty<ScopeIR>(),
                new[]
                {
                    CreateService(101, 10, 3),
                    CreateService(201, 20, 10),
                },
                new[] { CreateCommand(501, 10, 6) },
                new[] { CreateValueKey(201, "health.current", 10, 7) },
                new[] { CreateLifecycle(301, 10, 8, 401, 9) },
                new[] { CreateRuntimeQuery(601, 10, 10) },
                Array.Empty<DependencyEdgeIR>(),
                sources);

            DependencyValidationReport report = DependencyValidator.Validate(kernelIR);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Some.Matches<DependencyValidationIssue>(issue => issue.Code == "LEGACY_CORE_DEPENDENCY_FORBIDDEN"));
        }

        static DependencyValidationInput CreateBaselineInput(
            string selectedProfile = "Development",
            KernelProfileMask? selectedProfileMask = null,
            ModuleIR[]? modules = null,
            ScopeIR[]? scopes = null,
            ServiceIR[]? services = null,
            CommandIR[]? commands = null,
            ValueKeyIR[]? valueKeys = null,
            LifecycleIR[]? lifecycles = null,
            RuntimeQueryIR[]? runtimeQueries = null,
            DependencyEdgeIR[]? dependencies = null,
            CommandExecutorId[]? commandExecutors = null,
            CommandPayloadSchemaId[]? commandPayloadSchemas = null,
            SourceLocationTable? sources = null)
        {
            KernelProfileMask profileMask = selectedProfileMask ?? ParseProfileMask(selectedProfile);
            SourceLocationTable sourceTable = sources ?? CreateSources(16);

            return new DependencyValidationInput(
                selectedProfile,
                profileMask,
                modules ?? new[] { CreateModule(10, 1) },
                scopes ?? Array.Empty<ScopeIR>(),
                services ?? new[] { CreateService(101, 10, 2) },
                commands ?? new[] { CreateCommand(501, 10, 3) },
                valueKeys ?? new[] { CreateValueKey(201, "health.current", 10, 4) },
                lifecycles ?? new[] { CreateLifecycle(301, 10, 5, 401, 6) },
                runtimeQueries ?? new[] { CreateRuntimeQuery(601, 10, 7) },
                dependencies ?? Array.Empty<DependencyEdgeIR>(),
                commandExecutors,
                commandPayloadSchemas,
                sourceTable);
        }

        static ModuleIR CreateModule(int moduleId, int sourceId, ModuleDependencyIR[]? requiredModules = null, ModuleDependencyIR[]? optionalModules = null, ModuleAvailabilityIR? availability = null, ModuleKind kind = ModuleKind.Feature, LegacyCompatDescriptorIR? legacyCompat = null)
        {
            if (legacyCompat != null && kind == ModuleKind.Feature)
                kind = ModuleKind.MigrationAdapter;

            return new ModuleIR(
                new ModuleId(moduleId),
                "Module-" + moduleId,
                kind,
                new ModuleVersion(1),
                availability ?? new ModuleAvailabilityIR(new AvailabilityIR(KernelProfileMask.Development, true, null)),
                new SourceLocationId(sourceId),
            requiredModules,
            optionalModules,
            legacyCompat);
        }

        static LegacyCompatDescriptorIR CreateLegacyCompat(LegacyCompatKind kind, string? diagnosticsCode, string? removalPolicy)
        {
            return new LegacyCompatDescriptorIR(
                kind,
                "LegacySystem",
                "ServiceGraph",
                KernelProfileMask.Development | KernelProfileMask.Test,
                LegacyRemovalStatus.Temporary,
                diagnosticsCode,
            removalPolicy);
        }

        static ScopeIR CreateScope(
            int authoringId,
            int planId,
            ScopeKind kind,
            int ownerModuleId,
            int sourceId,
            int parentAuthoringId = 0,
            ScopeServiceRequirementIR[]? requiredServices = null)
        {
            return new ScopeIR(
                new ScopeAuthoringId(authoringId),
                new ScopePlanId(planId),
                "Scope-" + planId,
                kind,
                new ModuleId(ownerModuleId),
                parentAuthoringId == 0 ? default : new ScopeAuthoringId(parentAuthoringId),
                requiredServices,
                Array.Empty<ScopeValueInitRefIR>(),
                new LifecyclePlanRefIR(new LifecyclePlanId(301), new SourceLocationId(sourceId)),
                new SourceLocationId(sourceId));
        }

        static ServiceIR CreateService(int serviceId, int ownerModuleId, int sourceId, ServiceDependencyIR[]? dependencies = null)
        {
            return new ServiceIR(
                new ServiceId(serviceId),
                "Service-" + serviceId,
                ServiceLifetimeKind.Singleton,
                new ModuleId(ownerModuleId),
                Array.Empty<ServiceContractIR>(),
                dependencies,
                ServiceFactoryKind.GeneratedFactory,
                new SourceLocationId(sourceId));
        }

        static CommandIR CreateCommand(int commandTypeId, int ownerModuleId, int sourceId, CommandDependencyIR[]? dependencies = null)
        {
            return new CommandIR(
                new CommandTypeId(commandTypeId),
                "Command-" + commandTypeId,
                "authoring.command." + commandTypeId,
                new CommandCategoryId(1),
                new ModuleId(ownerModuleId),
                new CommandPayloadSchemaRefIR(new CommandPayloadSchemaId(1), new SourceLocationId(sourceId)),
                new CommandExecutorRefIR(new CommandExecutorId(1), new SourceLocationId(sourceId)),
                dependencies,
                new SourceLocationId(sourceId));
        }

        static ValueKeyIR CreateValueKey(int valueKeyId, string stableKey, int ownerModuleId, int sourceId)
        {
            return new ValueKeyIR(
                new ValueKeyId(valueKeyId),
                stableKey,
                "Display-" + valueKeyId,
                ValueKind.Int,
                new ModuleId(ownerModuleId),
                new ValueSchemaRefIR(new ValueSchemaId(1), new SourceLocationId(sourceId)),
                new SavePolicyIR(true, false, null),
                new SourceLocationId(sourceId));
        }

        static LifecycleIR CreateLifecycle(int lifecyclePlanId, int ownerModuleId, int lifecycleSourceId, int stepId, int stepSourceId, LifecycleTargetRefIR? target = null)
        {
            return new LifecycleIR(
                new LifecyclePlanId(lifecyclePlanId),
                "Lifecycle-" + lifecyclePlanId,
                new ModuleId(ownerModuleId),
                new[]
                {
                    new LifecycleStepIR(
                        new LifecycleStepId(stepId),
                        LifecyclePhase.Boot,
                        10,
                        target ?? new LifecycleTargetRefIR(new ServiceId(101)),
                        LifecycleActionKind.ServiceMethod,
                        Array.Empty<DependencyEdgeId>(),
                        new SourceLocationId(stepSourceId)),
                },
                new SourceLocationId(lifecycleSourceId));
        }

        static RuntimeQueryIR CreateRuntimeQuery(int runtimeQueryId, int ownerModuleId, int sourceId)
        {
            return new RuntimeQueryIR(
                new RuntimeQueryId(runtimeQueryId),
                "RuntimeQuery-" + runtimeQueryId,
                RuntimeQueryTargetKind.Service,
                new[] { new RuntimeIdentityFieldIR("ServiceId", "int", true) },
                new RuntimeQueryPolicyIR(true, false, DependencyPhase.Runtime),
                new ModuleId(ownerModuleId),
                new SourceLocationId(sourceId));
        }

        static DependencyEdgeIR CreateDependency(
            int dependencyId,
            DependencyNodeKind fromKind,
            int fromId,
            DependencyNodeKind toKind,
            int toId,
            DependencyPhase phase,
            DependencyStrength strength,
            int sourceId,
            RuntimeCycleMediationKind runtimeCycleMediation = RuntimeCycleMediationKind.None,
            DependencyKind dependencyKind = DependencyKind.Requires)
        {
            return new DependencyEdgeIR(
                new DependencyEdgeId(dependencyId),
                CreateNode(fromKind, fromId),
                CreateNode(toKind, toId),
                dependencyKind,
                phase,
                strength,
                new SourceLocationId(sourceId),
                runtimeCycleMediation);
        }

        static DependencyNodeIR CreateNode(DependencyNodeKind kind, int value)
        {
            switch (kind)
            {
                case DependencyNodeKind.Module:
                    return new DependencyNodeIR(new ModuleId(value));
                case DependencyNodeKind.Service:
                    return new DependencyNodeIR(new ServiceId(value));
                case DependencyNodeKind.Scope:
                    return new DependencyNodeIR(new ScopePlanId(value));
                case DependencyNodeKind.Command:
                    return new DependencyNodeIR(new CommandTypeId(value));
                case DependencyNodeKind.ValueKey:
                    return new DependencyNodeIR(new ValueKeyId(value));
                case DependencyNodeKind.LifecycleStep:
                    return new DependencyNodeIR(new LifecycleStepId(value));
                case DependencyNodeKind.RuntimeQuery:
                    return new DependencyNodeIR(new RuntimeQueryId(value));
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Test nodes must use defined kinds.");
            }
        }

        static SourceLocationTable CreateSources(int count, params int[] legacySourceIds)
        {
            SourceLocationIR[] sources = new SourceLocationIR[count];
            HashSet<int> legacyIds = new HashSet<int>(legacySourceIds ?? Array.Empty<int>());
            for (int index = 0; index < count; index++)
            {
                if (legacyIds.Contains(index + 1))
                {
                    sources[index] = new SourceLocationIR(new LegacySourceLocation("LegacySystem-" + index, "LegacyOrigin-" + index, "LegacyAdapter-" + index));
                    continue;
                }

                sources[index] = new SourceLocationIR(new GeneratedSourceLocation("DependencyValidatorTests", "Generated-" + index, "Build"));
            }

            return new SourceLocationTable(sources);
        }

        static KernelProfileMask ParseProfileMask(string selectedProfile)
        {
            return Enum.TryParse(selectedProfile, true, out KernelProfileMask mask)
                ? mask
                : KernelProfileMask.None;
        }
    }
}
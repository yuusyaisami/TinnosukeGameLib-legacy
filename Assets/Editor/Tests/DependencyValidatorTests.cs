#nullable enable
using System;
using System.Collections.Generic;
using Game.Kernel.Diagnostics;
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
        public void Validate_DuplicateCommandAuthoringKeyIdProducesStableIssue()
        {
            DependencyValidationInput input = CreateBaselineInput(
                commands: new[]
                {
                    CreateCommand(501, 10, 3, authoringKeyId: 900, authoringKey: "command.alpha"),
                    CreateCommand(502, 10, 4, authoringKeyId: 900, authoringKey: "command.beta"),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_COMMAND_AUTHORING_KEY_ID_DUPLICATE"));
            Assert.That(report.Issues[0].From.Kind, Is.EqualTo(DependencyNodeKind.Command));
        }

        [Test]
        public void Validate_DuplicateCommandAuthoringKeyProducesStableIssue()
        {
            DependencyValidationInput input = CreateBaselineInput(
                commands: new[]
                {
                    CreateCommand(501, 10, 3, authoringKeyId: 901, authoringKey: "command.shared"),
                    CreateCommand(502, 10, 4, authoringKeyId: 902, authoringKey: "command.shared"),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_COMMAND_AUTHORING_KEY_DUPLICATE"));
            Assert.That(report.Issues[0].From.Kind, Is.EqualTo(DependencyNodeKind.Command));
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
        public void Validate_ScopeWithRequiredServicesRejectsDetachedBoundary()
        {
            DependencyValidationInput input = CreateBaselineInput(
                services: new[]
                {
                    CreateService(101, 10, 2),
                },
                scopes: new[]
                {
                    new ScopeIR(
                        new ScopeAuthoringId(301),
                        new ScopePlanId(401),
                        "Scope-401",
                        ScopeKind.Root,
                        new ModuleId(10),
                        default,
                        new[]
                        {
                            new ScopeServiceRequirementIR(new ServiceId(101), DependencyStrength.Required, new SourceLocationId(7)),
                        },
                        Array.Empty<ScopeValueInitRefIR>(),
                        new ScopeServiceBoundaryIR(ScopeServiceBoundaryKind.Detached, 0, new SourceLocationId(7)),
                        new LifecyclePlanRefIR(new LifecyclePlanId(301), new SourceLocationId(7)),
                        new SourceLocationId(7)),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_SCOPE_SERVICE_BOUNDARY_INVALID"));
            Assert.That(report.Issues[0].From.Kind, Is.EqualTo(DependencyNodeKind.Scope));
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
        public void Validate_LifecycleLocalOwnerTargetsAreRejectedUntilLowerSpecSupportExists()
        {
            DependencyValidationInput input = CreateBaselineInput(
                lifecycles: new[]
                {
                    CreateLifecycle(301, 10, 5, 401, 6, new LifecycleTargetRefIR(LifecycleTargetKind.LegacyAdapter, "legacy-bridge")),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_LIFECYCLE_TARGET_LOCAL_REF_UNSUPPORTED"));
            Assert.That(report.Issues[0].Category, Is.EqualTo(ValidationIssueCategory.LocalNode));
        }

        [Test]
        public void Validate_LifecycleValueStoreTargetIsRejectedUntilRuntimeSupportExists()
        {
            DependencyValidationInput input = CreateBaselineInput(
                scopes: new[]
                {
                    CreateScope(
                        1,
                        101,
                        ScopeKind.Root,
                        10,
                        8,
                        valueInitPlans: new[]
                        {
                            new ScopeValueInitRefIR(new ValueInitPlanId(801), new SourceLocationId(8)),
                        }),
                },
                lifecycles: new[]
                {
                    CreateLifecycle(
                        301,
                        10,
                        5,
                        401,
                        6,
                        new LifecycleTargetRefIR(LifecycleTargetKind.ValueStore, "local:blackboard"),
                        phase: LifecyclePhase.Create,
                        action: LifecycleActionKind.ValueInit),
                },
                valueInitPlans: new[]
                {
                    CreateValueInitPlan(801, 10, 101, 201, 8, LifecyclePhase.Create, "local:blackboard"),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_LIFECYCLE_TARGET_LOCAL_REF_UNSUPPORTED"));
            Assert.That(report.Issues[0].Category, Is.EqualTo(ValidationIssueCategory.CrossNode));
        }

        [Test]
        public void Validate_ValueInitPlanMissingValueKeyProducesStableIssue()
        {
            DependencyValidationInput input = CreateBaselineInput(
                scopes: new[]
                {
                    CreateScope(
                        1,
                        101,
                        ScopeKind.Root,
                        10,
                        8,
                        valueInitPlans: new[]
                        {
                            new ScopeValueInitRefIR(new ValueInitPlanId(801), new SourceLocationId(8)),
                        }),
                },
                valueInitPlans: new[]
                {
                    CreateValueInitPlan(801, 10, 101, 999, 8, LifecyclePhase.Create, "local:blackboard"),
                });

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("DEP_VALUE_INIT_KEY_MISSING"));
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
        public void Validate_LegacyBridgeCanDependOnV2AndRemainVisible()
        {
            DependencyValidationInput input = CreateBaselineInput(
                modules: new[]
                {
                    CreateModule(10, 1),
                    CreateModule(20, 2, kind: ModuleKind.MigrationAdapter, requiredModules: new[] { new ModuleDependencyIR(new ModuleId(10), new SourceLocationId(5)) }, legacyCompat: CreateLegacyCompat(LegacyCompatKind.RuntimeAdapter, diagnosticsCode: "LEGACY_RUNTIME_ADAPTER_USED", removalPolicy: "Remove after migration")),
                },
                services: new[]
                {
                    CreateService(101, 10, 3),
                    CreateService(201, 20, 8),
                },
                dependencies: new[]
                {
                    CreateDependency(700, DependencyNodeKind.Module, 20, DependencyNodeKind.Module, 10, DependencyPhase.Build, DependencyStrength.Required, 12),
                },
                sources: CreateSources(10, 2, 8));

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.PassedWithWarnings));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("LEGACY_RUNTIME_ADAPTER_USED"));
            Assert.That(report.Issues[0].Severity, Is.EqualTo(ValidationSeverity.Warning));
            Assert.That(report.Issues, Has.None.Matches<DependencyValidationIssue>(issue => issue.Code == "LEGACY_CORE_DEPENDENCY_FORBIDDEN"));
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
            Assert.That(report.Issues, Has.Some.Matches<DependencyValidationIssue>(issue => issue.Code == LegacyCompatBoundaryCodes.RuntimeAdapterReleaseForbidden));
        }

        [Test]
        public void Validate_ReleaseRuntimeAdapterExplicitlyAllowedStillFails()
        {
            DependencyValidationInput input = CreateBaselineInput(
                selectedProfile: "Release",
                modules: new[]
                {
                    CreateModule(10, 1, availability: new ModuleAvailabilityIR(new AvailabilityIR(KernelProfileMask.Release, true, null))),
                    CreateModule(20, 2, kind: ModuleKind.MigrationAdapter, availability: new ModuleAvailabilityIR(new AvailabilityIR(KernelProfileMask.Release, true, null)), legacyCompat: CreateLegacyCompat(LegacyCompatKind.RuntimeAdapter, diagnosticsCode: "LEGACY_RUNTIME_ADAPTER_USED", removalPolicy: "Remove before release", profiles: KernelProfileMask.Release)),
                },
                services: new[]
                {
                    CreateService(101, 10, 3),
                    CreateService(201, 20, 8),
                },
                sources: CreateSources(10, 2, 8));

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Some.Matches<DependencyValidationIssue>(issue => issue.Code == LegacyCompatBoundaryCodes.RuntimeAdapterReleaseForbidden));
        }

        [Test]
        public void Validate_ReleaseAuthoringMigrationRemainsVisible()
        {
            DependencyValidationInput input = CreateBaselineInput(
                selectedProfile: "Release",
                modules: new[]
                {
                    CreateModule(10, 1, availability: new ModuleAvailabilityIR(new AvailabilityIR(KernelProfileMask.Release, true, null))),
                    CreateModule(20, 2, kind: ModuleKind.MigrationAdapter, availability: new ModuleAvailabilityIR(new AvailabilityIR(KernelProfileMask.Release, true, null)), legacyCompat: CreateLegacyCompat(LegacyCompatKind.AuthoringMigration, diagnosticsCode: "LEGACY_BRIDGE_USED", removalPolicy: "Remove after authoring migration", surface: LegacyAdapterSurface.Authoring, legacySourceType: "UnityAuthoringBridge", profiles: KernelProfileMask.Release)),
                },
                scopes: new[]
                {
                    CreateScope(100, 200, ScopeKind.Root, 20, 8),
                },
                services: new[]
                {
                    CreateService(101, 10, 3),
                    CreateService(201, 20, 8),
                },
                sources: CreateSources(10, 2, 8));

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.PassedWithWarnings));
            Assert.That(report.Issues, Has.Some.Matches<DependencyValidationIssue>(issue => issue.Code == LegacyCompatBoundaryCodes.BridgeUsed));
            Assert.That(report.Issues, Has.None.Matches<DependencyValidationIssue>(issue => issue.Code == LegacyCompatBoundaryCodes.RuntimeAdapterReleaseForbidden));
        }

        [Test]
        public void Validate_RuntimeAdapterInDevelopmentIncludesRemovalPolicyTrackingMetadata()
        {
            DependencyValidationInput input = CreateBaselineInput(
                modules: new[]
                {
                    CreateModule(10, 1),
                    CreateModule(20, 2, kind: ModuleKind.MigrationAdapter, legacyCompat: CreateLegacyCompat(LegacyCompatKind.RuntimeAdapter, diagnosticsCode: "LEGACY_RUNTIME_ADAPTER_USED", removalPolicy: "Remove after migration", surface: LegacyAdapterSurface.Resolver, legacySourceType: "RuntimeResolverHub")),
                },
                services: new[]
                {
                    CreateService(101, 10, 3),
                    CreateService(201, 20, 8),
                },
                sources: CreateSources(10, 2, 8));

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.PassedWithWarnings));
            Assert.That(report.Issues, Has.Some.Matches<DependencyValidationIssue>(issue => issue.Code == LegacyCompatBoundaryCodes.RuntimeAdapterUsed));

            DependencyValidationIssue issue = FindIssue(report.Issues, LegacyCompatBoundaryCodes.RuntimeAdapterUsed);
            KernelDiagnostic diagnostic = issue.ToKernelDiagnostic();

            Assert.That(FindPayloadValue(diagnostic, "RemovalPolicyReason"), Is.EqualTo("Legacy bridge declared in dependency input."));
            Assert.That(FindPayloadValue(diagnostic, "RemovalPolicyTargetReplacement"), Is.EqualTo("ServiceGraph"));
            Assert.That(FindPayloadValue(diagnostic, "TrackingIssueOrBlockingCondition"), Is.EqualTo("TICKET-1"));
        }

        [Test]
        public void Validate_LegacyAdapterWithoutTrackingIssueFails()
        {
            DependencyValidationInput input = CreateBaselineInput(
                modules: new[]
                {
                    CreateModule(10, 1),
                    CreateModule(20, 2, kind: ModuleKind.MigrationAdapter, legacyCompat: CreateLegacyCompat(LegacyCompatKind.RuntimeAdapter, diagnosticsCode: "LEGACY_RUNTIME_ADAPTER_USED", removalPolicy: "Remove after migration", surface: LegacyAdapterSurface.Resolver, legacySourceType: "RuntimeResolverHub", trackingIssueOrBlockingCondition: null)),
                },
                services: new[]
                {
                    CreateService(101, 10, 3),
                    CreateService(201, 20, 8),
                },
                sources: CreateSources(10, 2, 8));

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Some.Matches<DependencyValidationIssue>(issue => issue.Code == LegacyCompatBoundaryCodes.AdapterTrackingMissing));
        }

        [Test]
        public void Validate_ValueDataMigrationRemainsVisibleAndUsesExplicitValueTarget()
        {
            DependencyValidationInput input = CreateBaselineInput(
                modules: new[]
                {
                    CreateModule(10, 1),
                    CreateModule(20, 2, kind: ModuleKind.MigrationAdapter, legacyCompat: CreateLegacyCompat(LegacyCompatKind.DataMigration, diagnosticsCode: "LEGACY_VALUE_MIGRATION_USED", removalPolicy: "Remove after value migration", surface: LegacyAdapterSurface.Value, legacySourceType: "LegacyBlackboard", profiles: KernelProfileMask.Development | KernelProfileMask.Test)),
                },
                valueKeys: new[]
                {
                    CreateValueKey(201, "health.current", 10, 4),
                    CreateValueKey(202, "legacy.health", 20, 8),
                },
                services: new[]
                {
                    CreateService(101, 10, 3),
                    CreateService(201, 20, 8),
                },
                sources: CreateSources(10, 2, 8));

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.PassedWithWarnings));
            Assert.That(report.Issues, Has.Some.Matches<DependencyValidationIssue>(issue => issue.Code == LegacyCompatBoundaryCodes.BridgeUsed));
            Assert.That(report.Issues, Has.None.Matches<DependencyValidationIssue>(issue => issue.Code == LegacyCompatBoundaryCodes.RuntimeAdapterReleaseForbidden));
        }

        [Test]
        public void Validate_DataMigrationOnResolverSurfaceIsRejected()
        {
            DependencyValidationInput input = CreateBaselineInput(
                modules: new[]
                {
                    CreateModule(10, 1),
                    CreateModule(20, 2, kind: ModuleKind.MigrationAdapter, legacyCompat: CreateLegacyCompat(LegacyCompatKind.DataMigration, diagnosticsCode: "LEGACY_VALUE_MIGRATION_USED", removalPolicy: "Remove after value migration", surface: LegacyAdapterSurface.Resolver, legacySourceType: "LegacyResolver", profiles: KernelProfileMask.Development | KernelProfileMask.Test)),
                },
                services: new[]
                {
                    CreateService(101, 10, 3),
                    CreateService(201, 20, 8),
                },
                sources: CreateSources(10, 2, 8));

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Some.Matches<DependencyValidationIssue>(issue => issue.Code == LegacyCompatBoundaryCodes.AdapterKindSurfaceMismatch));
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
        public void Validate_LegacyAdapterWithoutSurfaceFails()
        {
            DependencyValidationInput input = CreateBaselineInput(
                modules: new[]
                {
                    CreateModule(10, 1),
                    CreateModule(20, 2, kind: ModuleKind.MigrationAdapter, legacyCompat: CreateLegacyCompat(LegacyCompatKind.RuntimeAdapter, diagnosticsCode: "LEGACY_RUNTIME_ADAPTER_USED", removalPolicy: "Remove after migration", surface: LegacyAdapterSurface.None, legacySourceType: "RuntimeResolverHub", explicitTargets: Array.Empty<DependencyNodeIR>())),
                },
                services: new[]
                {
                    CreateService(101, 10, 3),
                    CreateService(201, 20, 8),
                },
                sources: CreateSources(10, 2, 8));

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Some.Matches<DependencyValidationIssue>(issue => issue.Code == LegacyCompatBoundaryCodes.AdapterSurfaceMissing));
        }

        [Test]
        public void Validate_CommandSurfaceWithoutOwnedCommandFails()
        {
            DependencyValidationInput input = CreateBaselineInput(
                modules: new[]
                {
                    CreateModule(10, 1),
                    CreateModule(20, 2, kind: ModuleKind.MigrationAdapter, legacyCompat: CreateLegacyCompat(LegacyCompatKind.RuntimeAdapter, diagnosticsCode: "LEGACY_RUNTIME_ADAPTER_USED", removalPolicy: "Remove after migration", surface: LegacyAdapterSurface.Command, legacySourceType: "LegacyCommandRunner", explicitTargets: new[] { new DependencyNodeIR(new CommandTypeId(601)) })),
                },
                services: new[]
                {
                    CreateService(101, 10, 3),
                    CreateService(201, 20, 8),
                },
                commands: new[]
                {
                    CreateCommand(501, 10, 6),
                },
                sources: CreateSources(10, 2, 8, 6));

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Some.Matches<DependencyValidationIssue>(issue => issue.Code == LegacyCompatBoundaryCodes.AdapterTargetMissing));
        }

        [Test]
        public void Validate_CommandSurfaceWithOwnedCommandRemainsVisible()
        {
            DependencyValidationInput input = CreateBaselineInput(
                modules: new[]
                {
                    CreateModule(10, 1),
                    CreateModule(20, 2, kind: ModuleKind.MigrationAdapter, legacyCompat: CreateLegacyCompat(LegacyCompatKind.RuntimeAdapter, diagnosticsCode: "LEGACY_RUNTIME_ADAPTER_USED", removalPolicy: "Remove after migration", surface: LegacyAdapterSurface.Command, legacySourceType: "LegacyCommandRunner", explicitTargets: new[] { new DependencyNodeIR(new CommandTypeId(601)) })),
                },
                services: new[]
                {
                    CreateService(101, 10, 3),
                },
                commands: new[]
                {
                    CreateCommand(501, 10, 6),
                    CreateCommand(601, 20, 9),
                },
                sources: CreateSources(10, 2, 3, 6, 9));

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.PassedWithWarnings));
            Assert.That(report.Issues, Has.Some.Matches<DependencyValidationIssue>(issue => issue.Code == LegacyCompatBoundaryCodes.RuntimeAdapterUsed));
            Assert.That(report.Issues, Has.None.Matches<DependencyValidationIssue>(issue => issue.Code == LegacyCompatBoundaryCodes.AdapterTargetMissing));
        }

        [Test]
        public void Validate_CommandSurfaceWithWrongExplicitCommandFails()
        {
            DependencyValidationInput input = CreateBaselineInput(
                modules: new[]
                {
                    CreateModule(10, 1),
                    CreateModule(20, 2, kind: ModuleKind.MigrationAdapter, legacyCompat: CreateLegacyCompat(LegacyCompatKind.RuntimeAdapter, diagnosticsCode: "LEGACY_RUNTIME_ADAPTER_USED", removalPolicy: "Remove after migration", surface: LegacyAdapterSurface.Command, legacySourceType: "LegacyCommandRunner", explicitTargets: new[] { new DependencyNodeIR(new CommandTypeId(777)) })),
                },
                commands: new[]
                {
                    CreateCommand(501, 10, 6),
                    CreateCommand(601, 20, 9),
                },
                sources: CreateSources(10, 2, 6, 9));

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Some.Matches<DependencyValidationIssue>(issue => issue.Code == LegacyCompatBoundaryCodes.AdapterTargetMissing));
        }

        [Test]
        public void Validate_ResolverSurfaceWithServiceTargetRemainsVisible()
        {
            DependencyValidationInput input = CreateBaselineInput(
                modules: new[]
                {
                    CreateModule(10, 1),
                    CreateModule(20, 2, kind: ModuleKind.MigrationAdapter, legacyCompat: CreateLegacyCompat(LegacyCompatKind.RuntimeAdapter, diagnosticsCode: "LEGACY_RUNTIME_ADAPTER_USED", removalPolicy: "Remove after migration", surface: LegacyAdapterSurface.Resolver, legacySourceType: "RuntimeResolverHub", explicitTargets: new[] { new DependencyNodeIR(new ServiceId(201)) })),
                },
                services: new[]
                {
                    CreateService(101, 10, 3),
                    CreateService(201, 20, 8),
                },
                sources: CreateSources(10, 2, 3, 8));

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.PassedWithWarnings));
            Assert.That(report.Issues, Has.None.Matches<DependencyValidationIssue>(issue => issue.Code == LegacyCompatBoundaryCodes.AdapterTargetMissing));
        }

        [Test]
        public void Validate_InstallerSurfaceRejectsRuntimeAdapterKind()
        {
            DependencyValidationInput input = CreateBaselineInput(
                modules: new[]
                {
                    CreateModule(10, 1),
                    CreateModule(20, 2, kind: ModuleKind.MigrationAdapter, legacyCompat: CreateLegacyCompat(LegacyCompatKind.RuntimeAdapter, diagnosticsCode: "LEGACY_RUNTIME_ADAPTER_USED", removalPolicy: "Remove after migration", surface: LegacyAdapterSurface.Installer, legacySourceType: "ScopeFeatureInstallerUtility", explicitTargets: new[] { new DependencyNodeIR(new ScopePlanId(200)) })),
                },
                scopes: new[]
                {
                    CreateScope(100, 200, ScopeKind.Root, 20, 8),
                },
                sources: CreateSources(10, 2, 8));

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues, Has.Some.Matches<DependencyValidationIssue>(issue => issue.Code == LegacyCompatBoundaryCodes.AdapterKindSurfaceMismatch));
        }

        [Test]
        public void Validate_AuthoringSurfaceWithExplicitScopeTargetRemainsVisible()
        {
            DependencyValidationInput input = CreateBaselineInput(
                modules: new[]
                {
                    CreateModule(10, 1),
                    CreateModule(20, 2, kind: ModuleKind.MigrationAdapter, legacyCompat: CreateLegacyCompat(LegacyCompatKind.AuthoringMigration, diagnosticsCode: "LEGACY_BRIDGE_USED", removalPolicy: "Remove after authoring migration", surface: LegacyAdapterSurface.Authoring, legacySourceType: "UnityAuthoringBridge", explicitTargets: new[] { new DependencyNodeIR(new ScopePlanId(200)) })),
                },
                scopes: new[]
                {
                    CreateScope(100, 200, ScopeKind.Root, 20, 8),
                },
                sources: CreateSources(10, 2, 8));

            DependencyValidationReport report = DependencyValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.PassedWithWarnings));
            Assert.That(report.Issues, Has.Some.Matches<DependencyValidationIssue>(issue => issue.Code == LegacyCompatBoundaryCodes.BridgeUsed));
            Assert.That(report.Issues, Has.None.Matches<DependencyValidationIssue>(issue => issue.Code == LegacyCompatBoundaryCodes.AdapterTargetMissing));
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
            Assert.That(report.Issues, Has.Some.Matches<DependencyValidationIssue>(issue => issue.Code == LegacyCompatBoundaryCodes.ResolverComponentFallbackForbidden));
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
            SourceLocationTable? sources = null,
            ValueInitPlanIR[]? valueInitPlans = null)
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
                sourceTable,
                valueInitPlans);
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

        static LegacyCompatDescriptorIR CreateLegacyCompat(LegacyCompatKind kind, string? diagnosticsCode, string? removalPolicy, LegacyAdapterSurface surface = LegacyAdapterSurface.Resolver, string? legacySourceType = "RuntimeResolverHub", DependencyNodeIR[]? explicitTargets = null, KernelProfileMask profiles = KernelProfileMask.Development | KernelProfileMask.Test, string? trackingIssueOrBlockingCondition = "TICKET-1")
        {
            return new LegacyCompatDescriptorIR(
                kind,
                "LegacySystem",
                "ServiceGraph",
            profiles,
                LegacyRemovalStatus.Temporary,
                diagnosticsCode,
                removalPolicy,
                trackingIssueOrBlockingCondition,
                surface,
                legacySourceType,
                explicitTargets ?? CreateDefaultExplicitTargets(surface));
        }

        static string? FindPayloadValue(KernelDiagnostic diagnostic, string key)
        {
            for (int index = 0; index < diagnostic.Payload.Entries.Count; index++)
            {
                DiagnosticPayloadEntry entry = diagnostic.Payload.Entries[index];
                if (string.Equals(entry.Key, key, StringComparison.Ordinal))
                    return entry.Value.ToString();
            }

            return null;
        }

        static DependencyValidationIssue FindIssue(IReadOnlyList<DependencyValidationIssue> issues, string code)
        {
            for (int index = 0; index < issues.Count; index++)
            {
                if (string.Equals(issues[index].Code, code, StringComparison.Ordinal))
                    return issues[index];
            }

            throw new AssertionException("Expected validation issue with code '" + code + "'.");
        }

        static DependencyNodeIR[] CreateDefaultExplicitTargets(LegacyAdapterSurface surface)
        {
            switch (surface)
            {
                case LegacyAdapterSurface.None:
                    return Array.Empty<DependencyNodeIR>();

                case LegacyAdapterSurface.Installer:
                case LegacyAdapterSurface.Authoring:
                    return new[] { new DependencyNodeIR(new ScopePlanId(200)) };

                case LegacyAdapterSurface.Resolver:
                    return new[] { new DependencyNodeIR(new ServiceId(201)) };

                case LegacyAdapterSurface.Command:
                    return new[] { new DependencyNodeIR(new CommandTypeId(601)) };

                case LegacyAdapterSurface.Value:
                    return new[] { new DependencyNodeIR(new ValueKeyId(202)) };

                case LegacyAdapterSurface.Lifecycle:
                    return new[] { new DependencyNodeIR(new LifecycleStepId(402)) };

                default:
                    throw new ArgumentOutOfRangeException(nameof(surface), surface, "Unsupported legacy adapter surface.");
            }
        }

        static ScopeIR CreateScope(
            int authoringId,
            int planId,
            ScopeKind kind,
            int ownerModuleId,
            int sourceId,
            int parentAuthoringId = 0,
            ScopeServiceRequirementIR[]? requiredServices = null,
            ScopeValueInitRefIR[]? valueInitPlans = null)
        {
            ScopeServiceBoundaryIR serviceBoundary = parentAuthoringId == 0
                ? new ScopeServiceBoundaryIR(requiredServices != null && requiredServices.Length > 0 ? ScopeServiceBoundaryKind.OwnedLocal : ScopeServiceBoundaryKind.Detached, requiredServices != null && requiredServices.Length > 0 ? 1 : 0, new SourceLocationId(sourceId))
                : new ScopeServiceBoundaryIR(requiredServices != null && requiredServices.Length > 0 ? ScopeServiceBoundaryKind.OwnedLocal : ScopeServiceBoundaryKind.ReferencesParent, requiredServices != null && requiredServices.Length > 0 ? 1 : 0, new SourceLocationId(sourceId));

            return new ScopeIR(
                new ScopeAuthoringId(authoringId),
                new ScopePlanId(planId),
                "Scope-" + planId,
                kind,
                new ModuleId(ownerModuleId),
                parentAuthoringId == 0 ? default : new ScopeAuthoringId(parentAuthoringId),
                requiredServices,
                valueInitPlans,
                serviceBoundary,
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

        static CommandIR CreateCommand(int commandTypeId, int ownerModuleId, int sourceId, CommandDependencyIR[]? dependencies = null, int? authoringKeyId = null, string? authoringKey = null)
        {
            return new CommandIR(
                new CommandTypeId(commandTypeId),
                "Command-" + commandTypeId,
            new CommandAuthoringKeyRefIR(new CommandAuthoringKeyId(authoringKeyId ?? commandTypeId), authoringKey ?? ("authoring.command." + commandTypeId), new SourceLocationId(sourceId)),
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

        static LifecycleIR CreateLifecycle(int lifecyclePlanId, int ownerModuleId, int lifecycleSourceId, int stepId, int stepSourceId, LifecycleTargetRefIR? target = null, LifecycleFailurePolicy failurePolicy = LifecycleFailurePolicy.FailScope, LifecyclePhase phase = LifecyclePhase.Boot, LifecycleActionKind action = LifecycleActionKind.ServiceMethod)
        {
            return new LifecycleIR(
                new LifecyclePlanId(lifecyclePlanId),
                "Lifecycle-" + lifecyclePlanId,
                new ModuleId(ownerModuleId),
                new[]
                {
                    new LifecycleStepIR(
                        new LifecycleStepId(stepId),
                        phase,
                        10,
                        target ?? new LifecycleTargetRefIR(new ServiceId(101)),
                        action,
                        Array.Empty<DependencyEdgeId>(),
                        new SourceLocationId(stepSourceId)),
                },
                    new SourceLocationId(lifecycleSourceId),
                    failurePolicy);
        }

        static ValueInitPlanIR CreateValueInitPlan(int planId, int ownerModuleId, int targetScopePlanId, int keyId, int sourceId, LifecyclePhase executionPhase, string targetStoreRef)
        {
            return new ValueInitPlanIR(
                new ValueInitPlanId(planId),
                new ModuleId(ownerModuleId),
                new ScopePlanId(targetScopePlanId),
                targetStoreRef,
                executionPhase,
                10,
                new AvailabilityIR(KernelProfileMask.Development, true, null),
                new[]
                {
                    new ValueInitEntryIR(
                        new ValueKeyId(keyId),
                        ValueInitEntrySourceKind.Literal,
                        ValueKind.Int,
                        10,
                        ValueInitOverwritePolicy.Overwrite,
                        new SourceLocationId(sourceId),
                        serializedValue: "1"),
                },
                new SourceLocationId(sourceId));
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
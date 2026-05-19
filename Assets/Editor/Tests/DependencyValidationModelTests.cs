using System;
using Game.Kernel.Diagnostics;
using Game.Kernel.IR;
using Game.Kernel.Validation;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class DependencyValidationModelTests
    {
        [Test]
        public void ValidationEnums_UseStableExplicitValues()
        {
            Assert.That((int)ValidationResultStatus.Passed, Is.EqualTo(10));
            Assert.That((int)ValidationResultStatus.PassedWithWarnings, Is.EqualTo(20));
            Assert.That((int)ValidationResultStatus.Failed, Is.EqualTo(30));
            Assert.That((int)ValidationResultStatus.Fatal, Is.EqualTo(40));

            Assert.That((int)ValidationSeverity.Info, Is.EqualTo(10));
            Assert.That((int)ValidationSeverity.Warning, Is.EqualTo(20));
            Assert.That((int)ValidationSeverity.Error, Is.EqualTo(30));
            Assert.That((int)ValidationSeverity.Fatal, Is.EqualTo(40));

            Assert.That((int)ValidationPhase.Build, Is.EqualTo(10));
            Assert.That((int)ValidationPhase.Generate, Is.EqualTo(20));
            Assert.That((int)ValidationPhase.Boot, Is.EqualTo(30));
            Assert.That((int)ValidationPhase.Acquire, Is.EqualTo(40));
            Assert.That((int)ValidationPhase.Runtime, Is.EqualTo(50));
            Assert.That((int)ValidationPhase.Save, Is.EqualTo(60));
            Assert.That((int)ValidationPhase.EditorOnly, Is.EqualTo(70));

            Assert.That((int)ValidationIssueCategory.LocalNode, Is.EqualTo(10));
            Assert.That((int)ValidationIssueCategory.LocalEdge, Is.EqualTo(20));
            Assert.That((int)ValidationIssueCategory.CrossNode, Is.EqualTo(30));
            Assert.That((int)ValidationIssueCategory.CrossModule, Is.EqualTo(40));
            Assert.That((int)ValidationIssueCategory.ProfileAware, Is.EqualTo(50));
            Assert.That((int)ValidationIssueCategory.Projection, Is.EqualTo(60));
            Assert.That((int)ValidationIssueCategory.LegacyBoundary, Is.EqualTo(70));
        }

        [Test]
        public void ValidationPhaseConversion_RoundTripsDependencyPhase()
        {
            ValidationPhase phase = ValidationPhaseConversion.FromDependencyPhase(DependencyPhase.Runtime);
            DependencyPhase roundTripped = ValidationPhaseConversion.ToDependencyPhase(phase);

            Assert.That(phase, Is.EqualTo(ValidationPhase.Runtime));
            Assert.That(roundTripped, Is.EqualTo(DependencyPhase.Runtime));
            Assert.That(() => ValidationPhaseConversion.FromDependencyPhase(default), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => ValidationPhaseConversion.ToDependencyPhase(default), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void DependencyValidationIssue_RejectsInvalidInputs()
        {
            Assert.That(
                () => new DependencyValidationIssue(
                    code: " ",
                    severity: ValidationSeverity.Error,
                    category: ValidationIssueCategory.LocalEdge,
                    from: new DependencyNodeIR(new ServiceId(1)),
                    to: null,
                    phase: ValidationPhase.Build,
                    ownerModule: new ModuleId(1),
                    source: new SourceLocationId(1),
                    profile: "Release",
                    message: "Invalid"),
                Throws.ArgumentException);

            Assert.That(
                () => new DependencyValidationIssue(
                    code: "VALIDATION_INVALID_PROFILE",
                    severity: ValidationSeverity.Error,
                    category: ValidationIssueCategory.LocalEdge,
                    from: new DependencyNodeIR(new ServiceId(1)),
                    to: null,
                    phase: ValidationPhase.Build,
                    ownerModule: new ModuleId(1),
                    source: new SourceLocationId(1),
                    profile: " ",
                    message: "Invalid"),
                Throws.ArgumentException);

            Assert.That(
                () => new DependencyValidationIssue(
                    code: "VALIDATION_INVALID_SOURCE",
                    severity: ValidationSeverity.Error,
                    category: ValidationIssueCategory.LocalEdge,
                    from: new DependencyNodeIR(new ServiceId(1)),
                    to: null,
                    phase: ValidationPhase.Build,
                    ownerModule: new ModuleId(1),
                    source: default,
                    profile: "Release",
                    message: "Invalid"),
                Throws.ArgumentException);

            Assert.That(
                () => new DependencyValidationIssue(
                    code: "VALIDATION_INVALID_OWNER",
                    severity: ValidationSeverity.Error,
                    category: ValidationIssueCategory.LocalEdge,
                    from: new DependencyNodeIR(new ServiceId(1)),
                    to: null,
                    phase: ValidationPhase.Build,
                    ownerModule: default,
                    source: new SourceLocationId(1),
                    profile: "Release",
                    message: "Invalid"),
                Throws.ArgumentException);

            Assert.That(
                () => new DependencyValidationIssue(
                    code: "VALIDATION_INVALID_MESSAGE",
                    severity: ValidationSeverity.Error,
                    category: ValidationIssueCategory.LocalEdge,
                    from: new DependencyNodeIR(new ServiceId(1)),
                    to: null,
                    phase: ValidationPhase.Build,
                    ownerModule: new ModuleId(1),
                    source: new SourceLocationId(1),
                    profile: "Release",
                    message: " "),
                Throws.ArgumentException);
        }

        [Test]
        public void DependencyValidationIssue_PreservesTypedEndpointsAndConvertsToKernelDiagnostic()
        {
            DependencyValidationIssue issue = new DependencyValidationIssue(
                code: "VALIDATION_MISSING_REQUIRED_SERVICE",
                severity: ValidationSeverity.Error,
                category: ValidationIssueCategory.CrossNode,
                from: new DependencyNodeIR(new ScopePlanId(12)),
                to: new DependencyNodeIR(new ServiceId(5)),
                phase: DependencyPhase.Boot,
                ownerModule: new ModuleId(9),
                source: new SourceLocationId(22),
                profile: "Release",
                message: "Required service is missing.",
                suggestedFix: "Add the required service contribution.");

            KernelDiagnostic diagnostic = issue.ToKernelDiagnostic();

            Assert.That(issue.Phase, Is.EqualTo(ValidationPhase.Boot));
            Assert.That(issue.Category, Is.EqualTo(ValidationIssueCategory.CrossNode));
            Assert.That(issue.From.Kind, Is.EqualTo(DependencyNodeKind.Scope));
            Assert.That(issue.To.HasValue, Is.True);
            Assert.That(issue.To!.Value.Kind, Is.EqualTo(DependencyNodeKind.Service));
            Assert.That(diagnostic.Code.Value, Is.EqualTo("VALIDATION_MISSING_REQUIRED_SERVICE"));
            Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Error));
            Assert.That(diagnostic.Domain, Is.EqualTo(DiagnosticDomain.Validation));
            Assert.That(diagnostic.Context.OwnerModule.Value, Is.EqualTo(9));
            Assert.That(diagnostic.Context.Source.Value, Is.EqualTo(22));
            Assert.That(diagnostic.Context.RuntimeIdentities, Has.Count.EqualTo(2));
            Assert.That(diagnostic.Payload.Entries, Has.Count.EqualTo(6));
            Assert.That(diagnostic.Payload.Entries[2].Key, Is.EqualTo("ValidationCategory"));
            Assert.That(diagnostic.Payload.Entries[2].Value.ToString(), Is.EqualTo("CrossNode"));
            Assert.That(diagnostic.Payload.Entries[3].Key, Is.EqualTo("FromNode"));
            Assert.That(diagnostic.Payload.Entries[4].Key, Is.EqualTo("ToNode"));
            Assert.That(diagnostic.Payload.Entries[5].Key, Is.EqualTo("SuggestedFix"));
        }

        [Test]
        public void DependencyValidationIssue_AllowsMissingSourceLocationWhenExplicitlyPermitted()
        {
            DependencyValidationIssue issue = new DependencyValidationIssue(
                code: "DEP_DIAGNOSTICS_SOURCE_LOCATION_MISSING",
                severity: ValidationSeverity.Error,
                category: ValidationIssueCategory.Projection,
                from: new DependencyNodeIR(new ServiceId(5)),
                to: null,
                phase: ValidationPhase.Generate,
                ownerModule: new ModuleId(9),
                source: default,
                profile: "Development",
                message: "Missing source location provenance.",
                suggestedFix: "Attach a stable source location before emitting the diagnostic.",
                allowMissingSourceLocation: true);

            KernelDiagnostic diagnostic = issue.ToKernelDiagnostic();

            Assert.That(diagnostic.Code.Value, Is.EqualTo("DEP_DIAGNOSTICS_SOURCE_LOCATION_MISSING"));
            Assert.That(diagnostic.Context.Source.Value, Is.EqualTo(0));
            Assert.That(diagnostic.Context.OwnerModule.Value, Is.EqualTo(9));
            Assert.That(diagnostic.Domain, Is.EqualTo(DiagnosticDomain.Validation));
        }

        [Test]
        public void DependencyValidationIssue_UsesLegacyCompatDomainForLegacyBoundaryIssues()
        {
            DependencyValidationIssue issue = new DependencyValidationIssue(
                code: "LEGACY_RUNTIME_QUERY_LEGACY_LOOKUP_FORBIDDEN",
                severity: ValidationSeverity.Error,
                category: ValidationIssueCategory.LegacyBoundary,
                from: new DependencyNodeIR(new RuntimeQueryId(6)),
                to: null,
                phase: ValidationPhase.Runtime,
                ownerModule: new ModuleId(2),
                source: new SourceLocationId(6),
                profile: "Release",
                message: "Legacy runtime query lookup is forbidden.",
                suggestedFix: "Move lookup behind explicit adapter metadata.",
                additionalPayloadEntries: new[]
                {
                    new DiagnosticPayloadEntry("LegacySystemName", DiagnosticPayloadValue.FromString("LegacySystem")),
                });

            KernelDiagnostic diagnostic = issue.ToKernelDiagnostic();

            Assert.That(diagnostic.Domain, Is.EqualTo(DiagnosticDomain.LegacyCompat));
            Assert.That(diagnostic.Payload.Entries, Has.Some.Matches<DiagnosticPayloadEntry>(entry => entry.Key == "LegacySystemName"));
        }

        [Test]
        public void DependencyValidationReport_DerivesSummaryAndStatusDeterministically()
        {
            DependencyValidationIssue[] issues =
            {
                new DependencyValidationIssue(
                    code: "VALIDATION_INFO",
                    severity: ValidationSeverity.Info,
                    category: ValidationIssueCategory.LocalNode,
                    from: new DependencyNodeIR(new ModuleId(2)),
                    to: null,
                    phase: ValidationPhase.Build,
                    ownerModule: new ModuleId(2),
                    source: new SourceLocationId(3),
                    profile: "Release",
                    message: "Info issue."),
                new DependencyValidationIssue(
                    code: "VALIDATION_WARNING",
                    severity: ValidationSeverity.Warning,
                    category: ValidationIssueCategory.ProfileAware,
                    from: new DependencyNodeIR(new ServiceId(4)),
                    to: null,
                    phase: ValidationPhase.Generate,
                    ownerModule: new ModuleId(2),
                    source: new SourceLocationId(4),
                    profile: "Release",
                    message: "Warning issue."),
                new DependencyValidationIssue(
                    code: "VALIDATION_ERROR",
                    severity: ValidationSeverity.Error,
                    category: ValidationIssueCategory.CrossNode,
                    from: new DependencyNodeIR(new ScopePlanId(5)),
                    to: new DependencyNodeIR(new ServiceId(4)),
                    phase: ValidationPhase.Boot,
                    ownerModule: new ModuleId(2),
                    source: new SourceLocationId(5),
                    profile: "Release",
                    message: "Error issue."),
                new DependencyValidationIssue(
                    code: "VALIDATION_FATAL",
                    severity: ValidationSeverity.Fatal,
                    category: ValidationIssueCategory.Projection,
                    from: new DependencyNodeIR(new RuntimeQueryId(6)),
                    to: null,
                    phase: ValidationPhase.Runtime,
                    ownerModule: new ModuleId(2),
                    source: new SourceLocationId(6),
                    profile: "Release",
                    message: "Fatal issue."),
            };

            DependencyValidationReport report = new DependencyValidationReport("Release", issues);
            issues[0] = new DependencyValidationIssue(
                code: "MUTATED",
                severity: ValidationSeverity.Info,
                category: ValidationIssueCategory.LocalNode,
                from: new DependencyNodeIR(new ModuleId(2)),
                to: null,
                phase: ValidationPhase.Build,
                ownerModule: new ModuleId(2),
                source: new SourceLocationId(3),
                profile: "Release",
                message: "Mutated");

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Fatal));
            Assert.That(report.Summary.InfoCount, Is.EqualTo(1));
            Assert.That(report.Summary.WarningCount, Is.EqualTo(1));
            Assert.That(report.Summary.ErrorCount, Is.EqualTo(1));
            Assert.That(report.Summary.FatalCount, Is.EqualTo(1));
            Assert.That(report.Issues, Has.Count.EqualTo(4));
            Assert.That(report.Issues[0].Code, Is.EqualTo("VALIDATION_INFO"));
        }

        [Test]
        public void DependencyValidationReport_RejectsProfileMismatchAndNullItems()
        {
            DependencyValidationIssue issue = new DependencyValidationIssue(
                code: "VALIDATION_PROFILE_MISMATCH",
                severity: ValidationSeverity.Warning,
                category: ValidationIssueCategory.ProfileAware,
                from: new DependencyNodeIR(new ServiceId(4)),
                to: null,
                phase: ValidationPhase.Generate,
                ownerModule: new ModuleId(2),
                source: new SourceLocationId(4),
                profile: "Development",
                message: "Profile mismatch.");

            Assert.That(() => new DependencyValidationReport("Release", new[] { issue }), Throws.ArgumentException);
            Assert.That(() => new DependencyValidationReport("Release", new DependencyValidationIssue[] { null! }), Throws.ArgumentException);
            Assert.That(() => new DependencyValidationReport(" ", Array.Empty<DependencyValidationIssue>()), Throws.ArgumentException);
        }
    }
}
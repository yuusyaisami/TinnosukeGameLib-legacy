#nullable enable
using System;
using Game.Kernel.Abstractions;
using Game.Kernel.Boot;
using Game.Kernel.Diagnostics;
using Game.Kernel.Generation;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class BootValidationTests
    {
        [Test]
        public void BootValidator_ReturnsPassedReport_ForValidInputs()
        {
            KernelBootManifest manifest = CreateManifest(new KernelProfileId(7), KernelProfileKind.Release);
            KernelProfile profile = new KernelProfile(new KernelProfileId(7), KernelProfileKind.Release);

            BootValidationReport report = BootValidator.Validate(new BootValidationInput(
                manifest,
                profile,
                artifactSetReferencePresent: true,
                dependencyValidationStatus: ValidationResultStatus.Passed,
                artifactState: new BootArtifactValidationState(
                    artifactSetComplete: true,
                    artifactHeadersCompatible: true,
                    artifactStale: false,
                    debugMapRequired: true,
                    kernelIRHash: manifest.ArtifactSet.KernelIRHash,
                    registryHash: manifest.ArtifactSet.RegistryHash,
                    profileHash: manifest.ArtifactSet.ProfileHash,
                    debugMapHash: manifest.ArtifactSet.DebugMapHash),
                rootState: new BootRootValidationState(
                    new[] { ServiceIdentity(11) },
                    new[] { ServiceIdentity(11) },
                    new[] { ScopeIdentity(21) },
                    new[] { ScopeIdentity(21) }),
                fallbackState: new BootFallbackValidationState(false, false, false, false, false, false)));

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Passed));
            Assert.That(report.Issues, Is.Empty);
        }

        [Test]
        public void BootValidator_ReportsProfileMismatch_WhenSelectedProfileDiffersFromManifest()
        {
            KernelBootManifest manifest = CreateManifest(new KernelProfileId(7), KernelProfileKind.Release);
            KernelProfile profile = new KernelProfile(new KernelProfileId(9), KernelProfileKind.Release);

            BootValidationReport report = BootValidator.Validate(new BootValidationInput(
                manifest,
                profile,
                artifactSetReferencePresent: true,
                dependencyValidationStatus: ValidationResultStatus.Passed,
                artifactState: CreatePassingArtifactState(manifest),
                rootState: CreatePassingRootState(),
                fallbackState: new BootFallbackValidationState(false, false, false, false, false, false)));

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Fatal));
            Assert.That(report.Issues.Count, Is.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo(BootValidationCodes.ProfileMismatch));
        }

        [Test]
        public void BootValidator_ReportsDependencyValidationFailure_WhenDependencyValidationFailed()
        {
            KernelBootManifest manifest = CreateManifest(new KernelProfileId(7), KernelProfileKind.Release);
            KernelProfile profile = new KernelProfile(new KernelProfileId(7), KernelProfileKind.Release);

            BootValidationReport report = BootValidator.Validate(new BootValidationInput(
                manifest,
                profile,
                artifactSetReferencePresent: true,
                dependencyValidationStatus: ValidationResultStatus.Failed,
                artifactState: CreatePassingArtifactState(manifest),
                rootState: CreatePassingRootState(),
                fallbackState: new BootFallbackValidationState(false, false, false, false, false, false)));

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Fatal));
            Assert.That(report.Issues[0].Code, Is.EqualTo(BootValidationCodes.DependencyValidationFailed));
        }

        [Test]
        public void BootValidator_ReportsMissingRequiredRootService_WhenServiceProjectionDoesNotContainIt()
        {
            KernelBootManifest manifest = CreateManifest(new KernelProfileId(7), KernelProfileKind.Release);
            KernelProfile profile = new KernelProfile(new KernelProfileId(7), KernelProfileKind.Release);

            BootValidationInput input = new BootValidationInput(
                manifest,
                profile,
                artifactSetReferencePresent: true,
                dependencyValidationStatus: ValidationResultStatus.Passed,
                artifactState: CreatePassingArtifactState(manifest),
                rootState: new BootRootValidationState(
                    new[] { ServiceIdentity(11) },
                    Array.Empty<RuntimeIdentityRef>(),
                    new[] { ScopeIdentity(21) },
                    new[] { ScopeIdentity(21) }),
                fallbackState: new BootFallbackValidationState(false, false, false, false, false, false));

            BootValidationReport report = BootValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Fatal));
            Assert.That(report.Issues[0].Code, Is.EqualTo(BootValidationCodes.RequiredRootServiceMissing));
            Assert.That(report.Issues[0].SubjectIdentity, Is.EqualTo(ServiceIdentity(11)));
        }

        [Test]
        public void BootValidator_ReportsMissingRequiredRootScope_WhenScopeProjectionDoesNotContainIt()
        {
            KernelBootManifest manifest = CreateManifest(new KernelProfileId(7), KernelProfileKind.Release);
            KernelProfile profile = new KernelProfile(new KernelProfileId(7), KernelProfileKind.Release);

            BootValidationInput input = new BootValidationInput(
                manifest,
                profile,
                artifactSetReferencePresent: true,
                dependencyValidationStatus: ValidationResultStatus.Passed,
                artifactState: CreatePassingArtifactState(manifest),
                rootState: new BootRootValidationState(
                    new[] { ServiceIdentity(11) },
                    new[] { ServiceIdentity(11) },
                    new[] { ScopeIdentity(21) },
                    Array.Empty<RuntimeIdentityRef>()),
                fallbackState: new BootFallbackValidationState(false, false, false, false, false, false));

            BootValidationReport report = BootValidator.Validate(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Fatal));
            Assert.That(report.Issues[0].Code, Is.EqualTo(BootValidationCodes.RequiredRootScopeMissing));
            Assert.That(report.Issues[0].SubjectIdentity, Is.EqualTo(ScopeIdentity(21)));
        }

        [Test]
        public void BootValidator_ReportsLegacyFallbackForbidden_ForReleaseProfiles()
        {
            KernelBootManifest manifest = CreateManifest(new KernelProfileId(7), KernelProfileKind.Release);
            KernelProfile profile = new KernelProfile(new KernelProfileId(7), KernelProfileKind.Release);

            BootValidationReport report = BootValidator.Validate(new BootValidationInput(
                manifest,
                profile,
                artifactSetReferencePresent: true,
                dependencyValidationStatus: ValidationResultStatus.Passed,
                artifactState: CreatePassingArtifactState(manifest),
                rootState: CreatePassingRootState(),
                fallbackState: new BootFallbackValidationState(true, false, false, false, false, false)));

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Fatal));
            Assert.That(report.Issues[0].Code, Is.EqualTo(BootValidationCodes.LegacyFallbackForbidden));
        }

        [Test]
        public void BootValidator_ReportsTestNonDeterministicPolicy_ForTestProfiles()
        {
            KernelBootManifest manifest = CreateManifest(new KernelProfileId(7), KernelProfileKind.Test);
            KernelProfile profile = new KernelProfile(new KernelProfileId(7), KernelProfileKind.Test);

            BootValidationReport report = BootValidator.Validate(new BootValidationInput(
                manifest,
                profile,
                artifactSetReferencePresent: true,
                dependencyValidationStatus: ValidationResultStatus.Passed,
                artifactState: CreatePassingArtifactState(manifest),
                rootState: CreatePassingRootState(),
                fallbackState: new BootFallbackValidationState(false, false, false, false, false, true)));

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Fatal));
            Assert.That(report.Issues[0].Code, Is.EqualTo(BootValidationCodes.TestNonDeterministicPolicy));
        }

        [Test]
        public void BootValidator_ReportsDiscoveryAndFallbackProhibitions_ForExplicitForbiddenPaths()
        {
            KernelBootManifest manifest = CreateManifest(new KernelProfileId(7), KernelProfileKind.Release);
            KernelProfile profile = new KernelProfile(new KernelProfileId(7), KernelProfileKind.Release);

            BootValidationReport report = BootValidator.Validate(new BootValidationInput(
                manifest,
                profile,
                artifactSetReferencePresent: true,
                dependencyValidationStatus: ValidationResultStatus.Passed,
                artifactState: CreatePassingArtifactState(manifest),
                rootState: CreatePassingRootState(),
                fallbackState: new BootFallbackValidationState(false, true, true, true, true, false)));

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Fatal));
            Assert.That(report.Issues.Count, Is.EqualTo(4));
            Assert.That(report.Issues[0].Code, Is.EqualTo(BootValidationCodes.RuntimeDiscoveryForbidden));
            Assert.That(report.Issues[1].Code, Is.EqualTo(BootValidationCodes.ResourcesFallbackForbidden));
            Assert.That(report.Issues[2].Code, Is.EqualTo(BootValidationCodes.DefaultRootCreationForbidden));
            Assert.That(report.Issues[3].Code, Is.EqualTo(BootValidationCodes.DuplicateRootCleanupForbidden));
        }

        [Test]
        public void BootValidator_ReportsArtifactHashFailures_WhenActualHashesDoNotMatch()
        {
            KernelBootManifest manifest = CreateManifest(new KernelProfileId(7), KernelProfileKind.Release);
            KernelProfile profile = new KernelProfile(new KernelProfileId(7), KernelProfileKind.Release);

            BootArtifactValidationState artifactState = new BootArtifactValidationState(
                artifactSetComplete: true,
                artifactHeadersCompatible: true,
                artifactStale: false,
                debugMapRequired: true,
                kernelIRHash: new UnityEngine.Hash128(9, 9, 9, 9).ToString(),
                registryHash: new UnityEngine.Hash128(8, 8, 8, 8).ToString(),
                profileHash: new UnityEngine.Hash128(7, 7, 7, 7).ToString(),
                debugMapHash: new UnityEngine.Hash128(6, 6, 6, 6).ToString());

            BootValidationReport report = BootValidator.Validate(new BootValidationInput(
                manifest,
                profile,
                artifactSetReferencePresent: true,
                dependencyValidationStatus: ValidationResultStatus.Passed,
                artifactState: artifactState,
                rootState: CreatePassingRootState(),
                fallbackState: new BootFallbackValidationState(false, false, false, false, false, false)));

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Fatal));
            Assert.That(report.Issues.Count, Is.EqualTo(4));
            Assert.That(report.Issues[0].Code, Is.EqualTo(BootValidationCodes.KernelIRHashMismatch));
            Assert.That(report.Issues[1].Code, Is.EqualTo(BootValidationCodes.RegistryHashMismatch));
            Assert.That(report.Issues[2].Code, Is.EqualTo(BootValidationCodes.ProfileHashMismatch));
            Assert.That(report.Issues[3].Code, Is.EqualTo(BootValidationCodes.DebugMapHashMismatch));
        }

        [Test]
        public void BootValidator_ReportsMissingDebugMap_WhenDevelopmentProfileRequiresIt()
        {
            KernelBootManifest manifest = CreateManifest(new KernelProfileId(7), KernelProfileKind.Development);
            KernelProfile profile = new KernelProfile(new KernelProfileId(7), KernelProfileKind.Development);

            BootArtifactValidationState artifactState = new BootArtifactValidationState(
                artifactSetComplete: true,
                artifactHeadersCompatible: true,
                artifactStale: false,
                debugMapRequired: false,
                kernelIRHash: manifest.ArtifactSet.KernelIRHash,
                registryHash: manifest.ArtifactSet.RegistryHash,
                profileHash: manifest.ArtifactSet.ProfileHash,
                debugMapHash: null);

            BootValidationReport report = BootValidator.Validate(new BootValidationInput(
                manifest,
                profile,
                artifactSetReferencePresent: true,
                dependencyValidationStatus: ValidationResultStatus.Passed,
                artifactState: artifactState,
                rootState: CreatePassingRootState(),
                fallbackState: new BootFallbackValidationState(false, false, false, false, false, false)));

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.Issues.Count, Is.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo(BootValidationCodes.DebugMapMissing));
        }

        [Test]
        public void BootValidationIssue_ToKernelDiagnostic_IncludesBootDiagnosticsPolicyMetadata()
        {
            KernelBootManifest manifest = CreateManifest(new KernelProfileId(7), KernelProfileKind.Development);
            KernelProfile profile = new KernelProfile(new KernelProfileId(7), KernelProfileKind.Development);

            BootValidationIssue issue = new BootValidationIssue(
                BootValidationCodes.ProfileMismatch,
                ValidationSeverity.Error,
                BootValidationGateKind.ProfileMismatch,
                "Boot profile mismatch.",
                "Regenerate the boot manifest for the selected profile.",
                ServiceIdentity(11),
                "Development",
                "Release");

            KernelDiagnostic diagnostic = issue.ToKernelDiagnostic(manifest, profile, manifest.DiagnosticsPolicy);

            AssertPayloadEntry(diagnostic, "BootDiagnosticsPolicyKind", KernelProfileKind.Development.ToString());
            AssertPayloadEntry(diagnostic, "BootDiagnosticsFailureBoundaryBehavior", BootDiagnosticsFailureBoundaryBehavior.ReportAndBlock.ToString());
            AssertPayloadEntry(diagnostic, "BootDiagnosticsDetail", KernelProfileDiagnosticsDetail.Full.ToString());
            AssertPayloadEntry(diagnostic, "BootDiagnosticsInspectionMode", BootDiagnosticsInspectionMode.Enabled.ToString());
            AssertPayloadEntry(diagnostic, "BootDiagnosticsDeterminismMode", BootDiagnosticsDeterminismMode.Disabled.ToString());
        }

        [Test]
        public void BootRootValidationState_RejectsDuplicateRootEntries()
        {
            Assert.That(() => new BootRootValidationState(
                new[] { ServiceIdentity(11), ServiceIdentity(11) },
                new[] { ServiceIdentity(11) },
                new[] { ScopeIdentity(21) },
                new[] { ScopeIdentity(21) }), Throws.ArgumentException);
        }

        static BootArtifactValidationState CreatePassingArtifactState(KernelBootManifest manifest)
        {
            return new BootArtifactValidationState(
                artifactSetComplete: true,
                artifactHeadersCompatible: true,
                artifactStale: false,
                debugMapRequired: true,
                kernelIRHash: manifest.ArtifactSet.KernelIRHash,
                registryHash: manifest.ArtifactSet.RegistryHash,
                profileHash: manifest.ArtifactSet.ProfileHash,
                debugMapHash: manifest.ArtifactSet.DebugMapHash);
        }

        static BootRootValidationState CreatePassingRootState()
        {
            return new BootRootValidationState(
                new[] { ServiceIdentity(11) },
                new[] { ServiceIdentity(11) },
                new[] { ScopeIdentity(21) },
                new[] { ScopeIdentity(21) });
        }

        static KernelBootManifest CreateManifest(KernelProfileId profileId, KernelProfileKind profileKind)
        {
            string debugMapHash = EmptyDebugMapHash();

            VerifiedArtifactSetRef artifactSet = new VerifiedArtifactSetRef(
                new ArtifactSetId(11),
                new PlanId(31),
                new UnityEngine.Hash128(1, 2, 3, 4).ToString(),
                new UnityEngine.Hash128(5, 6, 7, 8).ToString(),
                11,
                new UnityEngine.Hash128(9, 9, 9, 9).ToString(),
                debugMapHash);

            return new KernelBootManifest(
                new ManifestId(5),
                profileId,
                artifactSet,
                new BootPolicyId(9),
                BootDiagnosticsPolicy.ForKind(profileKind));
        }

        static RuntimeIdentityRef ServiceIdentity(int value)
        {
            return new RuntimeIdentityRef(RuntimeIdentityKind.Service, value);
        }

        static RuntimeIdentityRef ScopeIdentity(int value)
        {
            return new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, value);
        }

        static string EmptyDebugMapHash()
        {
            return VerifiedArtifactHeaderHashing.ComputeGeneratedHash(Array.Empty<string>()).ToString();
        }

        static void AssertPayloadEntry(KernelDiagnostic diagnostic, string key, string expectedValue)
        {
            for (int index = 0; index < diagnostic.Payload.Entries.Count; index++)
            {
                if (diagnostic.Payload.Entries[index].Key != key)
                    continue;

                Assert.That(diagnostic.Payload.Entries[index].Value.ToString(), Is.EqualTo(expectedValue), key);
                return;
            }

            Assert.Fail("Missing payload entry: " + key);
        }
    }
}
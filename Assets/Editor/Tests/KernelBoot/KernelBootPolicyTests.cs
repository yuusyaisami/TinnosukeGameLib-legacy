using System;
using Game.Kernel.Abstractions;
using Game.Kernel.Boot;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class KernelBootPolicyTests
    {
        [Test]
        public void KernelProfileKind_UsesStableExplicitValues()
        {
            Assert.That((int)KernelProfileKind.Development, Is.EqualTo(10));
            Assert.That((int)KernelProfileKind.Release, Is.EqualTo(20));
            Assert.That((int)KernelProfileKind.Test, Is.EqualTo(30));
        }

        [Test]
        public void TypedIdentityPrimitives_PreserveValueEqualityAndHashCode()
        {
            AssertIdentityCase("KernelProfileId", value => new KernelProfileId(value), () => default(KernelProfileId));
            AssertIdentityCase("BootPolicyId", value => new BootPolicyId(value), () => default(BootPolicyId));
        }

        [Test]
        public void KernelProfilePolicy_ForKind_UsesSpecMatrix()
        {
            AssertPolicy(
                KernelProfileKind.Development,
                KernelProfileStaleArtifactDisposition.ErrorAndBootBlock,
                KernelProfileMissingDebugMapDisposition.Error,
                KernelProfileLegacyBridgeDisposition.WarningIfExplicitlyAllowed,
                KernelProfileDiagnosticsDetail.Full,
                KernelProfileRuntimeAssertionsMode.Enabled,
                KernelProfileValidationStrictness.Strict,
                KernelProfileGeneratedMismatchDisposition.BootBlock,
                KernelProfileFallbackDisposition.DevOnlyBridgeAllowed);

            AssertPolicy(
                KernelProfileKind.Release,
                KernelProfileStaleArtifactDisposition.FatalAndBootBlock,
                KernelProfileMissingDebugMapDisposition.ErrorIfFatalDiagnosticsCannotBeProduced,
                KernelProfileLegacyBridgeDisposition.ForbiddenUnlessLegacyCompatSpecAllows,
                KernelProfileDiagnosticsDetail.MinimalRequired,
                KernelProfileRuntimeAssertionsMode.Minimal,
                KernelProfileValidationStrictness.Strict,
                KernelProfileGeneratedMismatchDisposition.BootBlock,
                KernelProfileFallbackDisposition.Forbidden);

            AssertPolicy(
                KernelProfileKind.Test,
                KernelProfileStaleArtifactDisposition.FatalAndBootBlock,
                KernelProfileMissingDebugMapDisposition.Fatal,
                KernelProfileLegacyBridgeDisposition.ErrorOrFatal,
                KernelProfileDiagnosticsDetail.FullCaptured,
                KernelProfileRuntimeAssertionsMode.Enabled,
                KernelProfileValidationStrictness.MaximumPractical,
                KernelProfileGeneratedMismatchDisposition.BootBlock,
                KernelProfileFallbackDisposition.Forbidden);
        }

        [Test]
        public void KernelProfilePolicy_RejectsInvalidKind()
        {
            Assert.That(() => KernelProfilePolicy.ForKind(default), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void BootDiagnosticsPolicy_ForKind_UsesSpecMatrix()
        {
            AssertDiagnosticsPolicy(
                KernelProfileKind.Development,
                BootDiagnosticsFailureBoundaryBehavior.ReportAndBlock,
                KernelProfileDiagnosticsDetail.Full,
                BootDiagnosticsInspectionMode.Enabled,
                BootDiagnosticsDeterminismMode.Disabled);

            AssertDiagnosticsPolicy(
                KernelProfileKind.Release,
                BootDiagnosticsFailureBoundaryBehavior.ReportAndBlock,
                KernelProfileDiagnosticsDetail.MinimalRequired,
                BootDiagnosticsInspectionMode.Disabled,
                BootDiagnosticsDeterminismMode.Disabled);

            AssertDiagnosticsPolicy(
                KernelProfileKind.Test,
                BootDiagnosticsFailureBoundaryBehavior.ReportAndBlock,
                KernelProfileDiagnosticsDetail.FullCaptured,
                BootDiagnosticsInspectionMode.Enabled,
                BootDiagnosticsDeterminismMode.Enabled);
        }

        [Test]
        public void BootDiagnosticsPolicy_RejectsInvalidKind()
        {
            Assert.That(() => BootDiagnosticsPolicy.ForKind(default), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void KernelProfile_ReusesCanonicalPolicyForKind()
        {
            KernelProfile profile = new KernelProfile(new KernelProfileId(7), KernelProfileKind.Release);

            Assert.That(profile.Id, Is.EqualTo(new KernelProfileId(7)));
            Assert.That(profile.Kind, Is.EqualTo(KernelProfileKind.Release));
            Assert.That(profile.Policy, Is.EqualTo(KernelProfilePolicy.ForKind(KernelProfileKind.Release)));
            Assert.That(profile.ToString(), Does.Contain("KernelProfileId(7)"));
        }

        [Test]
        public void KernelProfile_RejectsKindPolicyMismatch()
        {
            KernelProfilePolicy policy = KernelProfilePolicy.ForKind(KernelProfileKind.Development);

            Assert.That(
                () => new KernelProfile(new KernelProfileId(2), KernelProfileKind.Test, policy),
                Throws.ArgumentException);
        }

        static void AssertPolicy(
            KernelProfileKind kind,
            KernelProfileStaleArtifactDisposition staleArtifactDisposition,
            KernelProfileMissingDebugMapDisposition missingDebugMapDisposition,
            KernelProfileLegacyBridgeDisposition legacyBridgeDisposition,
            KernelProfileDiagnosticsDetail diagnosticsDetail,
            KernelProfileRuntimeAssertionsMode runtimeAssertionsMode,
            KernelProfileValidationStrictness validationStrictness,
            KernelProfileGeneratedMismatchDisposition generatedMismatchDisposition,
            KernelProfileFallbackDisposition fallbackDisposition)
        {
            KernelProfilePolicy policy = KernelProfilePolicy.ForKind(kind);

            Assert.That(policy.Kind, Is.EqualTo(kind));
            Assert.That(policy.StaleArtifactDisposition, Is.EqualTo(staleArtifactDisposition));
            Assert.That(policy.MissingDebugMapDisposition, Is.EqualTo(missingDebugMapDisposition));
            Assert.That(policy.LegacyBridgeDisposition, Is.EqualTo(legacyBridgeDisposition));
            Assert.That(policy.DiagnosticsDetail, Is.EqualTo(diagnosticsDetail));
            Assert.That(policy.RuntimeAssertionsMode, Is.EqualTo(runtimeAssertionsMode));
            Assert.That(policy.ValidationStrictness, Is.EqualTo(validationStrictness));
            Assert.That(policy.GeneratedMismatchDisposition, Is.EqualTo(generatedMismatchDisposition));
            Assert.That(policy.FallbackDisposition, Is.EqualTo(fallbackDisposition));
        }

        static void AssertDiagnosticsPolicy(
            KernelProfileKind kind,
            BootDiagnosticsFailureBoundaryBehavior failureBoundaryBehavior,
            KernelProfileDiagnosticsDetail diagnosticsDetail,
            BootDiagnosticsInspectionMode editorInspectionMode,
            BootDiagnosticsDeterminismMode testDeterminismMode)
        {
            BootDiagnosticsPolicy policy = BootDiagnosticsPolicy.ForKind(kind);

            Assert.That(policy.Kind, Is.EqualTo(kind));
            Assert.That(policy.FailureBoundaryBehavior, Is.EqualTo(failureBoundaryBehavior));
            Assert.That(policy.DiagnosticsDetail, Is.EqualTo(diagnosticsDetail));
            Assert.That(policy.EditorInspectionMode, Is.EqualTo(editorInspectionMode));
            Assert.That(policy.TestDeterminismMode, Is.EqualTo(testDeterminismMode));
        }

        static void AssertIdentityCase<T>(string name, Func<int, T> create, Func<T> createDefault)
            where T : struct
        {
            T first = create(17);
            T same = create(17);
            T different = create(21);

            Assert.That(first.Equals(same), Is.True, name + " should compare equal for the same value.");
            Assert.That(first.Equals(different), Is.False, name + " should compare unequal for different values.");
            Assert.That(first.GetHashCode(), Is.EqualTo(same.GetHashCode()), name + " should keep equal hash codes for equal values.");
            Assert.That(first.ToString(), Does.Contain("17"), name + " should render a stable debug representation.");

            T defaultValue = createDefault();
            Assert.That(defaultValue.ToString(), Is.Not.Null, name + " default value should still have a debug representation.");
        }
    }
}
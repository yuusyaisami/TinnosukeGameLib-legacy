#nullable enable

using System;
using Game.Kernel.Diagnostics;
using Game.Kernel.IR;
using Game.Kernel.Validation;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class LegacyCompatBoundaryTests
    {
        [Test]
        public void LegacyRemovalPolicy_RejectsMissingRequiredMetadata()
        {
            Assert.That(
                () => new LegacyRemovalPolicy(
                    new ModuleId(10),
                    LegacyRemovalStatus.Temporary,
                    KernelProfileMask.Development,
                    string.Empty,
                    "TargetSubsystem",
                    "remove after migration",
                    "LEGACY_RUNTIME_ADAPTER_USED",
                    "TICKET-1"),
                Throws.ArgumentException);
        }

        [Test]
        public void LegacyAdapterDescriptor_RejectsRemovalPolicyProfileMismatch()
        {
            LegacyRemovalPolicy policy = new LegacyRemovalPolicy(
                new ModuleId(10),
                LegacyRemovalStatus.Temporary,
                KernelProfileMask.Development,
                "legacy shim remains in development",
                "TargetSubsystem",
                "remove after migration",
                "LEGACY_RUNTIME_ADAPTER_USED",
                "TICKET-1");

            Assert.That(
                () => new LegacyAdapterDescriptor(
                    LegacyCompatKind.RuntimeAdapter,
                    new ModuleId(10),
                    "LegacySystem",
                    "TargetSubsystem",
                    KernelProfileMask.Release,
                    new SourceLocationId(5),
                    policy),
                Throws.ArgumentException);
        }

        [Test]
        public void LegacyMigrationReport_WarnsRuntimeAdapterInDevelopment()
        {
            LegacyAdapterDescriptor descriptor = CreateRuntimeAdapterDescriptor(
                profiles: KernelProfileMask.Development,
                status: LegacyRemovalStatus.Temporary);

            LegacyMigrationReport report = LegacyMigrationReport.Validate(
                new LegacyMigrationReportHeader("LegacyCompatBoundary", 1, "TargetSubsystem", ValidationPhase.Build, "Development", KernelProfileMask.Development),
                new[] { descriptor });

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.PassedWithWarnings));
            Assert.That(report.Header.SourceVersion, Is.EqualTo(1));
            Assert.That(report.Header.Phase, Is.EqualTo(ValidationPhase.Build));
            Assert.That(report.WarningCount, Is.EqualTo(1));
            Assert.That(report.ErrorCount, Is.EqualTo(0));
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo(LegacyCompatBoundaryCodes.RuntimeAdapterUsed));
            Assert.That(report.ToKernelDiagnostics(), Has.Length.EqualTo(1));
            Assert.That(report.ToKernelDiagnostics()[0].Domain, Is.EqualTo(DiagnosticDomain.LegacyCompat));
        }

        [Test]
        public void LegacyMigrationReport_RejectsForbiddenFallback()
        {
            LegacyAdapterDescriptor descriptor = CreateForbiddenFallbackDescriptor();

            LegacyMigrationReport report = LegacyMigrationReport.Validate(
                new LegacyMigrationReportHeader("LegacyCompatBoundary", 1, "TargetSubsystem", ValidationPhase.Build, "Development", KernelProfileMask.Development),
                new[] { descriptor });

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Failed));
            Assert.That(report.ErrorCount, Is.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo(LegacyCompatBoundaryCodes.FallbackForbidden));
        }

        [Test]
        public void LegacyMigrationReport_PreservesDescriptorOrder()
        {
            LegacyAdapterDescriptor first = CreateForbiddenFallbackDescriptor();
            LegacyAdapterDescriptor second = CreateRuntimeAdapterDescriptor(KernelProfileMask.Development, LegacyRemovalStatus.Temporary);

            LegacyMigrationReport report = LegacyMigrationReport.Validate(
                new LegacyMigrationReportHeader("LegacyCompatBoundary", 1, "TargetSubsystem", ValidationPhase.Build, "Development", KernelProfileMask.Development),
                new[] { first, second });

            Assert.That(report.Issues, Has.Count.EqualTo(2));
            Assert.That(report.Adapters[0].OwnerModule, Is.EqualTo(new ModuleId(10)));
            Assert.That(report.Adapters[1].OwnerModule, Is.EqualTo(new ModuleId(20)));
            Assert.That(report.Issues[0].Code, Is.EqualTo(LegacyCompatBoundaryCodes.RuntimeAdapterUsed));
            Assert.That(report.Issues[1].Code, Is.EqualTo(LegacyCompatBoundaryCodes.FallbackForbidden));
        }

        static LegacyAdapterDescriptor CreateRuntimeAdapterDescriptor(KernelProfileMask profiles, LegacyRemovalStatus status)
        {
            LegacyRemovalPolicy policy = new LegacyRemovalPolicy(
                new ModuleId(10),
                status,
                profiles,
                "legacy shim remains in development",
                "TargetSubsystem",
                "remove after migration",
                LegacyCompatBoundaryCodes.RuntimeAdapterUsed,
                "TICKET-1");

            return new LegacyAdapterDescriptor(
                LegacyCompatKind.RuntimeAdapter,
                new ModuleId(10),
                "LegacySystem",
                "TargetSubsystem",
                profiles,
                new SourceLocationId(5),
                policy);
        }

        static LegacyAdapterDescriptor CreateForbiddenFallbackDescriptor()
        {
            KernelProfileMask profiles = KernelProfileMask.Development;
            LegacyRemovalPolicy policy = new LegacyRemovalPolicy(
                new ModuleId(20),
                LegacyRemovalStatus.Forbidden,
                profiles,
                "legacy fallback must not ship",
                "Explicit v2 dependency",
                "never ship",
                LegacyCompatBoundaryCodes.FallbackForbidden,
                "ARCH-123");

            return new LegacyAdapterDescriptor(
                LegacyCompatKind.ForbiddenFallback,
                new ModuleId(20),
                "LegacyFallbackSystem",
                "TargetSubsystem",
                profiles,
                new SourceLocationId(6),
                policy);
        }
    }
}
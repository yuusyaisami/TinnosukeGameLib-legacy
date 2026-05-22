#nullable enable

using System;
using Game.Kernel.Diagnostics;
using NUnit.Framework;

namespace Game.Editor.Tests
{
    public sealed class KernelCorePerformanceAllocationTests
    {
        const string PerformanceProfile = "Performance";
        const string HotPath = "HotPath";

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            KernelPerformanceReportCollector.Reset();
        }

        [Test]
        public void DiagnosticsReport_IsAllocationFree_WhenNoSessionAndSinksAreHealthy()
        {
            var service = new KernelDiagnosticService(Array.Empty<IKernelDiagnosticSink>());
            var diagnostic = new KernelDiagnostic(
                new DiagnosticCode("PERF_DIAG_REPORT"),
                DiagnosticSeverity.Info,
                DiagnosticDomain.Kernel,
                DiagnosticFailureBoundary.Operation,
                message: "steady-state trace path");

            var measurement = KernelAllocationAssert.Measure(() => service.Report(in diagnostic));
            Assert.That(measurement.AllocatedBytes, Is.EqualTo(0));

            RecordPerformanceEntry(
                testId: "M13.5.DiagnosticsDisabledTracePath",
                subsystem: nameof(KernelDiagnosticService),
                operation: nameof(KernelDiagnosticService.Report),
                pathKind: HotPath,
                fixtureSize: 0,
                measurement: measurement,
                threshold: CreateDiagnosticsDisabledTracePathThreshold(),
                observedMarkerSamples: new[] { "Kernel.Diagnostics.TraceDisabled" });
        }

        static void RecordPerformanceEntry(
            string testId,
            string subsystem,
            string operation,
            string pathKind,
            int fixtureSize,
            KernelAllocationAssert.Measurement measurement,
            KernelPerformanceThresholdSpec threshold,
            string[] observedMarkerSamples,
            string baselineLabel = "",
            long baselineAllocationBytes = 0,
            double baselineElapsedMilliseconds = 0d)
        {
            bool hasBaseline = !string.IsNullOrWhiteSpace(baselineLabel);
            var regressionResult = KernelPerformanceRegressionGate.Evaluate(
                measurement,
                threshold,
                observedMarkerSamples,
                hasBaseline,
                baselineAllocationBytes,
                baselineElapsedMilliseconds);

            KernelPerformanceReportCollector.Record(new KernelPerformanceReportEntry
            {
                TestId = testId,
                Subsystem = subsystem,
                Operation = operation,
                PathKind = pathKind,
                FixtureSize = fixtureSize,
                Profile = PerformanceProfile,
                ElapsedMilliseconds = measurement.ElapsedMilliseconds,
                AllocationBytes = measurement.AllocatedBytes,
                CallCount = 1,
                MarkerSamples = observedMarkerSamples,
                Passed = regressionResult.Passed,
                ExpectedMaxAllocationBytes = threshold.ExpectedMaxAllocationBytes,
                ExpectedMaxElapsedMilliseconds = threshold.ExpectedMaxElapsedMilliseconds,
                AllowedAllocationRegressionBytes = threshold.AllowedAllocationRegressionBytes,
                AllowedElapsedRegressionMilliseconds = threshold.AllowedElapsedRegressionMilliseconds,
                FailureCode = regressionResult.FailureCode,
                BaselineLabel = baselineLabel,
                HasBaseline = hasBaseline,
                BaselineAllocationBytes = baselineAllocationBytes,
                BaselineElapsedMilliseconds = baselineElapsedMilliseconds,
                AllocationDeltaBytes = hasBaseline ? measurement.AllocatedBytes - baselineAllocationBytes : 0,
                ElapsedDeltaMilliseconds = hasBaseline ? measurement.ElapsedMilliseconds - baselineElapsedMilliseconds : 0d,
                FailureReason = regressionResult.FailureReason,
            });

            Assert.That(regressionResult.Passed, Is.True, regressionResult.FailureReason);
        }

        static KernelPerformanceThresholdSpec CreateDiagnosticsDisabledTracePathThreshold()
        {
            return KernelPerformanceRegressionGate.CreateHotPathThreshold(
                expectedMaxAllocationBytes: 0,
                expectedMaxElapsedMilliseconds: 15d,
                allowedAllocationRegressionBytes: 0,
                allowedElapsedRegressionMilliseconds: 1d,
                requiredMarkerSamples: new[] { "Kernel.Diagnostics.TraceDisabled" });
        }

    }
}

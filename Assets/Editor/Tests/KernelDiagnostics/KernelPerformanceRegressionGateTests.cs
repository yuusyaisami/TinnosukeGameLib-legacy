#nullable enable

using NUnit.Framework;
using Game.Editor.Tests;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class KernelPerformanceRegressionGateTests
    {
        [Test]
        public void Evaluate_Passes_WhenWithinCeilingsAndMarkersPresent()
        {
            var threshold = KernelPerformanceRegressionGate.CreateHotPathThreshold(
                expectedMaxAllocationBytes: 8,
                expectedMaxElapsedMilliseconds: 2d,
                allowedAllocationRegressionBytes: 2,
                allowedElapsedRegressionMilliseconds: 0.5d,
                requiredMarkerSamples: new[] { "Kernel.Perf.Sample" });

            var result = KernelPerformanceRegressionGate.Evaluate(
                new KernelAllocationAssert.Measurement(4, 1.25d),
                threshold,
                new[] { "Kernel.Perf.Sample" },
                hasBaseline: true,
                baselineAllocationBytes: 4,
                baselineElapsedMilliseconds: 1d);

            Assert.That(result.Passed, Is.True);
            Assert.That(result.FailureCode, Is.Empty);
            Assert.That(result.FailureReason, Is.Empty);
        }

        [Test]
        public void Evaluate_Fails_WhenAllocationExceedsCeiling()
        {
            var threshold = KernelPerformanceRegressionGate.CreateHotPathThreshold(
                expectedMaxAllocationBytes: 8,
                expectedMaxElapsedMilliseconds: 2d,
                requiredMarkerSamples: new[] { "Kernel.Perf.Sample" });

            var result = KernelPerformanceRegressionGate.Evaluate(
                new KernelAllocationAssert.Measurement(12, 1d),
                threshold,
                new[] { "Kernel.Perf.Sample" },
                hasBaseline: false);

            Assert.That(result.Passed, Is.False);
            Assert.That(result.FailureCode, Is.EqualTo("PERF_BENCHMARK_THRESHOLD_REGRESSION"));
            Assert.That(result.FailureReason, Does.Contain("Allocated 12 bytes"));
        }

        [Test]
        public void Evaluate_Fails_WhenElapsedExceedsCeiling()
        {
            var threshold = KernelPerformanceRegressionGate.CreateHotPathThreshold(
                expectedMaxAllocationBytes: 8,
                expectedMaxElapsedMilliseconds: 2d,
                requiredMarkerSamples: new[] { "Kernel.Perf.Sample" });

            var result = KernelPerformanceRegressionGate.Evaluate(
                new KernelAllocationAssert.Measurement(0, 4d),
                threshold,
                new[] { "Kernel.Perf.Sample" },
                hasBaseline: false);

            Assert.That(result.Passed, Is.False);
            Assert.That(result.FailureCode, Is.EqualTo("PERF_BENCHMARK_THRESHOLD_REGRESSION"));
            Assert.That(result.FailureReason, Does.Contain("Elapsed 4"));
        }

        [Test]
        public void Evaluate_Fails_WhenBaselineRegressesPastAllowance()
        {
            var threshold = KernelPerformanceRegressionGate.CreateHotPathThreshold(
                expectedMaxAllocationBytes: 8,
                expectedMaxElapsedMilliseconds: 5d,
                allowedAllocationRegressionBytes: 1,
                allowedElapsedRegressionMilliseconds: 0.25d,
                requiredMarkerSamples: new[] { "Kernel.Perf.Sample" });

            var result = KernelPerformanceRegressionGate.Evaluate(
                new KernelAllocationAssert.Measurement(12, 2d),
                threshold,
                new[] { "Kernel.Perf.Sample" },
                hasBaseline: true,
                baselineAllocationBytes: 10,
                baselineElapsedMilliseconds: 1.5d);

            Assert.That(result.Passed, Is.False);
            Assert.That(result.FailureCode, Is.EqualTo("PERF_BENCHMARK_THRESHOLD_REGRESSION"));
            Assert.That(result.FailureReason, Does.Contain("Allocation regressed"));
        }

        [Test]
        public void Evaluate_Fails_WhenRequiredMarkerIsMissing()
        {
            var threshold = KernelPerformanceRegressionGate.CreateHotPathThreshold(
                expectedMaxAllocationBytes: 8,
                expectedMaxElapsedMilliseconds: 2d,
                requiredMarkerSamples: new[] { "Kernel.Perf.Required" });

            var result = KernelPerformanceRegressionGate.Evaluate(
                new KernelAllocationAssert.Measurement(0, 1d),
                threshold,
                new[] { "Kernel.Perf.Sample" },
                hasBaseline: false);

            Assert.That(result.Passed, Is.False);
            Assert.That(result.FailureCode, Is.EqualTo("PERF_MISSING_PROFILER_MARKER"));
            Assert.That(result.FailureReason, Does.Contain("Kernel.Perf.Required"));
        }
    }
}
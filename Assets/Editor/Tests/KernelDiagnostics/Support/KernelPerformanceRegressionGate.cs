#nullable enable

using System;
using System.Collections.Generic;
using Game.Editor.Tests;

namespace Game.Kernel.Diagnostics
{
    [Serializable]
    public sealed class KernelPerformanceThresholdSpec
    {
        public long ExpectedMaxAllocationBytes;
        public double ExpectedMaxElapsedMilliseconds;
        public long AllowedAllocationRegressionBytes;
        public double AllowedElapsedRegressionMilliseconds;
        public string[] RequiredMarkerSamples = Array.Empty<string>();
    }

    [Serializable]
    public sealed class KernelPerformanceRegressionResult
    {
        public bool Passed;
        public string FailureCode = string.Empty;
        public string FailureReason = string.Empty;
    }

    public static class KernelPerformanceRegressionGate
    {
        const string ThresholdRegressionCode = "PERF_BENCHMARK_THRESHOLD_REGRESSION";
        const string MissingMarkerCode = "PERF_MISSING_PROFILER_MARKER";

        public static KernelPerformanceThresholdSpec CreateHotPathThreshold(
            long expectedMaxAllocationBytes,
            double expectedMaxElapsedMilliseconds,
            long allowedAllocationRegressionBytes = 0,
            double allowedElapsedRegressionMilliseconds = 0.5,
            params string[] requiredMarkerSamples)
        {
            if (expectedMaxAllocationBytes < 0)
                throw new ArgumentOutOfRangeException(nameof(expectedMaxAllocationBytes));
            if (expectedMaxElapsedMilliseconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(expectedMaxElapsedMilliseconds));
            if (allowedAllocationRegressionBytes < 0)
                throw new ArgumentOutOfRangeException(nameof(allowedAllocationRegressionBytes));
            if (allowedElapsedRegressionMilliseconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(allowedElapsedRegressionMilliseconds));

            return new KernelPerformanceThresholdSpec
            {
                ExpectedMaxAllocationBytes = expectedMaxAllocationBytes,
                ExpectedMaxElapsedMilliseconds = expectedMaxElapsedMilliseconds,
                AllowedAllocationRegressionBytes = allowedAllocationRegressionBytes,
                AllowedElapsedRegressionMilliseconds = allowedElapsedRegressionMilliseconds,
                RequiredMarkerSamples = requiredMarkerSamples ?? Array.Empty<string>(),
            };
        }

        public static KernelPerformanceRegressionResult Evaluate(
            in KernelAllocationAssert.Measurement measurement,
            KernelPerformanceThresholdSpec threshold,
            IReadOnlyList<string> markerSamples,
            bool hasBaseline,
            long baselineAllocationBytes = 0,
            double baselineElapsedMilliseconds = 0d)
        {
            if (threshold == null)
                throw new ArgumentNullException(nameof(threshold));
            if (markerSamples == null)
                throw new ArgumentNullException(nameof(markerSamples));

            ValidateThreshold(threshold);

            string? missingMarker = FindMissingMarker(threshold.RequiredMarkerSamples, markerSamples);
            if (missingMarker != null)
            {
                return new KernelPerformanceRegressionResult
                {
                    Passed = false,
                    FailureCode = MissingMarkerCode,
                    FailureReason = $"Missing required profiler marker '{missingMarker}'.",
                };
            }

            if (measurement.AllocatedBytes > threshold.ExpectedMaxAllocationBytes)
            {
                return CreateFailure(
                    $"Allocated {measurement.AllocatedBytes} bytes, expected at most {threshold.ExpectedMaxAllocationBytes} bytes.");
            }

            if (measurement.ElapsedMilliseconds > threshold.ExpectedMaxElapsedMilliseconds)
            {
                return CreateFailure(
                    $"Elapsed {measurement.ElapsedMilliseconds:0.###} ms, expected at most {threshold.ExpectedMaxElapsedMilliseconds:0.###} ms.");
            }

            if (hasBaseline)
            {
                long allocationDeltaBytes = measurement.AllocatedBytes - baselineAllocationBytes;
                double elapsedDeltaMilliseconds = measurement.ElapsedMilliseconds - baselineElapsedMilliseconds;

                if (allocationDeltaBytes > threshold.AllowedAllocationRegressionBytes)
                {
                    return CreateFailure(
                        $"Allocation regressed by {allocationDeltaBytes} bytes against baseline {baselineAllocationBytes} bytes; allowed regression is {threshold.AllowedAllocationRegressionBytes} bytes.");
                }

                if (elapsedDeltaMilliseconds > threshold.AllowedElapsedRegressionMilliseconds)
                {
                    return CreateFailure(
                        $"Elapsed regressed by {elapsedDeltaMilliseconds:0.###} ms against baseline {baselineElapsedMilliseconds:0.###} ms; allowed regression is {threshold.AllowedElapsedRegressionMilliseconds:0.###} ms.");
                }
            }

            return new KernelPerformanceRegressionResult
            {
                Passed = true,
                FailureCode = string.Empty,
                FailureReason = string.Empty,
            };
        }

        static void ValidateThreshold(KernelPerformanceThresholdSpec threshold)
        {
            if (threshold.ExpectedMaxAllocationBytes < 0)
                throw new ArgumentOutOfRangeException(nameof(threshold.ExpectedMaxAllocationBytes));
            if (threshold.ExpectedMaxElapsedMilliseconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(threshold.ExpectedMaxElapsedMilliseconds));
            if (threshold.AllowedAllocationRegressionBytes < 0)
                throw new ArgumentOutOfRangeException(nameof(threshold.AllowedAllocationRegressionBytes));
            if (threshold.AllowedElapsedRegressionMilliseconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(threshold.AllowedElapsedRegressionMilliseconds));
        }

        static string? FindMissingMarker(IReadOnlyList<string> requiredMarkers, IReadOnlyList<string> markerSamples)
        {
            if (requiredMarkers == null || requiredMarkers.Count == 0)
                return null;

            for (int i = 0; i < requiredMarkers.Count; i++)
            {
                string requiredMarker = requiredMarkers[i];
                bool found = false;

                for (int j = 0; j < markerSamples.Count; j++)
                {
                    if (string.Equals(requiredMarker, markerSamples[j], StringComparison.Ordinal))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                    return requiredMarker;
            }

            return null;
        }

        static KernelPerformanceRegressionResult CreateFailure(string reason)
        {
            return new KernelPerformanceRegressionResult
            {
                Passed = false,
                FailureCode = ThresholdRegressionCode,
                FailureReason = reason,
            };
        }
    }
}
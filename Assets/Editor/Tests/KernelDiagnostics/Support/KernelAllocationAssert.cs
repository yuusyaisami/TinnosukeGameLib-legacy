#nullable enable

using System;
using System.Diagnostics;
using NUnit.Framework;

namespace Game.Editor.Tests
{
    public static class KernelAllocationAssert
    {
        public readonly struct Measurement
        {
            public Measurement(long allocatedBytes, double elapsedMilliseconds)
            {
                AllocatedBytes = allocatedBytes;
                ElapsedMilliseconds = elapsedMilliseconds;
            }

            public long AllocatedBytes { get; }

            public double ElapsedMilliseconds { get; }
        }

        public static long MeasureAllocatedBytes(Action action, int warmupIterations = 1, int measuredIterations = 1)
        {
            return Measure(action, warmupIterations, measuredIterations).AllocatedBytes;
        }

        public static Measurement Measure(Action action, int warmupIterations = 1, int measuredIterations = 1)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            if (warmupIterations < 0)
                throw new ArgumentOutOfRangeException(nameof(warmupIterations));
            if (measuredIterations <= 0)
                throw new ArgumentOutOfRangeException(nameof(measuredIterations));

            for (int i = 0; i < warmupIterations; i++)
            {
                action();
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < measuredIterations; i++)
            {
                action();
            }
            stopwatch.Stop();

            long after = GC.GetAllocatedBytesForCurrentThread();
            return new Measurement(after - before, stopwatch.Elapsed.TotalMilliseconds);
        }

        public static void AssertAllocatedBytesAtMost(long expectedMaxBytes, Action action, string? scenario = null, int warmupIterations = 1, int measuredIterations = 1)
        {
            long allocatedBytes = Measure(action, warmupIterations, measuredIterations).AllocatedBytes;
            Assert.That(
                allocatedBytes,
                Is.LessThanOrEqualTo(expectedMaxBytes),
                scenario ?? $"Allocated {allocatedBytes} bytes, expected at most {expectedMaxBytes}.");
        }
    }
}
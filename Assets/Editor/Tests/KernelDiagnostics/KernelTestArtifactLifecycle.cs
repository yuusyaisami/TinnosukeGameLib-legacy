#nullable enable
using System;
using System.IO;
using Game.Kernel.Diagnostics;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [SetUpFixture]
    public sealed class KernelTestArtifactLifecycle
    {
        const string RunDirectoryEnvironmentVariable = "KERNEL_TEST_RUN_DIRECTORY";
        const string RunIdEnvironmentVariable = "KERNEL_TEST_RUN_ID";
        const string PlatformEnvironmentVariable = "KERNEL_TEST_PLATFORM";
        const string TestFilterEnvironmentVariable = "KERNEL_TEST_FILTER";
        const string TargetEnvironmentVariable = "KERNEL_TEST_TARGET";

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            KernelTestRunMetadata? metadata = CreateMetadataFromEnvironment();
            if (metadata == null)
                return;

            KernelTestArtifactCollector.Configure(metadata);
            KernelPerformanceReportCollector.Reset();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            KernelTestRunMetadata? metadata = KernelTestArtifactCollector.Metadata ?? CreateMetadataFromEnvironment();
            if (metadata == null || string.IsNullOrWhiteSpace(metadata.RunDirectory))
            {
                KernelTestArtifactCollector.Reset();
                KernelPerformanceReportCollector.Reset();
                return;
            }

            if (!Directory.Exists(metadata.RunDirectory))
                Directory.CreateDirectory(metadata.RunDirectory);

            KernelTestArtifactWriter.WriteArtifacts(metadata, KernelTestArtifactCollector.SnapshotDiagnostics());
            KernelTestArtifactCollector.Reset();
            KernelPerformanceReportCollector.Reset();
        }

        static KernelTestRunMetadata? CreateMetadataFromEnvironment()
        {
            string? runDirectory = Environment.GetEnvironmentVariable(RunDirectoryEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(runDirectory))
                return null;

            return new KernelTestRunMetadata
            {
                RunId = Environment.GetEnvironmentVariable(RunIdEnvironmentVariable) ?? string.Empty,
                Platform = Environment.GetEnvironmentVariable(PlatformEnvironmentVariable) ?? string.Empty,
                TestFilter = Environment.GetEnvironmentVariable(TestFilterEnvironmentVariable) ?? string.Empty,
                Target = Environment.GetEnvironmentVariable(TargetEnvironmentVariable) ?? string.Empty,
                FixtureIdentity = Environment.GetEnvironmentVariable(TargetEnvironmentVariable) ?? Environment.GetEnvironmentVariable(TestFilterEnvironmentVariable) ?? string.Empty,
                ProfileIdentity = "Test",
                RunDirectory = runDirectory,
                GeneratedAtUtc = DateTime.UtcNow.ToString("O"),
            };
        }
    }
}
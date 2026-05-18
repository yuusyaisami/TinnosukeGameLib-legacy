using System;
using System.IO;
using Game.Kernel.Diagnostics;
using NUnit.Framework;
using UnityEngine;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class KernelTestArtifactWriterTests
    {
        [Test]
        public void WriteArtifacts_CreatesRequiredJsonFiles()
        {
            string runDirectory = CreateTempRunDirectory();
            try
            {
                KernelTestRunMetadata metadata = CreateMetadata(runDirectory);

                KernelTestArtifactWriter.WriteArtifacts(metadata, Array.Empty<KernelDiagnostic>());

                Assert.That(File.Exists(Path.Combine(runDirectory, "DiagnosticsReport.json")), Is.True);
                Assert.That(File.Exists(Path.Combine(runDirectory, "ValidationReport.json")), Is.True);
                Assert.That(File.Exists(Path.Combine(runDirectory, "GenerationReport.json")), Is.True);
                Assert.That(File.Exists(Path.Combine(runDirectory, "PerformanceReport.json")), Is.True);
            }
            finally
            {
                DeleteDirectory(runDirectory);
            }
        }

        [Test]
        public void CreateDiagnosticsReport_PreservesStructuredDiagnosticFields()
        {
            string runDirectory = CreateTempRunDirectory();
            try
            {
                KernelTestRunMetadata metadata = CreateMetadata(runDirectory);
                KernelDiagnostic diagnostic = new KernelDiagnostic(
                    new DiagnosticCode("COMMAND_EXECUTOR_MISSING"),
                    DiagnosticSeverity.Error,
                    DiagnosticDomain.Command,
                    DiagnosticFailureBoundary.Command,
                    message: "Executor missing",
                    context: new DiagnosticContext(
                        runtimeIdentities: new[]
                        {
                            new RuntimeIdentityRef(RuntimeIdentityKind.CommandType, 10),
                            new RuntimeIdentityRef(RuntimeIdentityKind.ScopeHandle, 20, 2),
                        },
                        ownerModule: new ModuleIdentityRef(3),
                        source: new SourceLocationRef(4),
                        artifact: new ArtifactIdentityRef(5, 6),
                        profileId: 7,
                        correlationId: new DiagnosticCorrelationId(8),
                        phase: "Resolve"),
                    payload: new DiagnosticPayload(new[]
                    {
                        new DiagnosticPayloadEntry("Expected", DiagnosticPayloadValue.FromString("CommandExecutor")),
                        new DiagnosticPayloadEntry("Attempt", DiagnosticPayloadValue.FromInt32(2)),
                    }),
                    exception: new DiagnosticExceptionInfo(
                        "System.InvalidOperationException",
                        "boom",
                        "stack",
                        new DiagnosticExceptionInfo("System.Exception", "inner", "inner-stack")),
                    eventId: new DiagnosticEventId(9),
                    sessionId: new DiagnosticSessionId(10),
                    correlationId: new DiagnosticCorrelationId(8));

                DiagnosticsReport report = KernelTestArtifactWriter.CreateDiagnosticsReport(metadata, new[] { diagnostic });
                string json = JsonUtility.ToJson(report, true);

                Assert.That(report.TotalCount, Is.EqualTo(1));
                Assert.That(report.Header.Run.FixtureIdentity, Is.EqualTo("KernelTests"));
                Assert.That(report.Header.Run.ProfileIdentity, Is.EqualTo("Test"));
                Assert.That(report.Records[0].Code, Is.EqualTo("COMMAND_EXECUTOR_MISSING"));
                Assert.That(report.Records[0].Severity, Is.EqualTo("Error"));
                Assert.That(report.Records[0].Domain, Is.EqualTo("Command"));
                Assert.That(report.Records[0].FailureBoundary, Is.EqualTo("Command"));
                Assert.That(report.Records[0].OwnerModule, Is.EqualTo(3));
                Assert.That(report.Records[0].RuntimeIdentities, Has.Length.EqualTo(2));
                Assert.That(report.Records[0].Payload, Has.Length.EqualTo(2));
                Assert.That(report.Records[0].Exception, Is.Not.Null);
                Assert.That(report.Records[0].Exception!.Inner, Is.Not.Null);
                Assert.That(json, Does.Contain("COMMAND_EXECUTOR_MISSING"));
                Assert.That(json, Does.Contain("CommandExecutor"));
            }
            finally
            {
                DeleteDirectory(runDirectory);
            }
        }

        [Test]
        public void CreateEmptyReport_ProducesExplicitPlaceholderShape()
        {
            string runDirectory = CreateTempRunDirectory();
            try
            {
                KernelTestRunMetadata metadata = CreateMetadata(runDirectory);

                EmptyKernelTestReport report = KernelTestArtifactWriter.CreateEmptyReport(metadata, "ValidationReport", true);

                Assert.That(report.Header.ReportKind, Is.EqualTo("ValidationReport"));
                Assert.That(report.Header.IsPlaceholder, Is.True);
                Assert.That(report.Header.Run.FixtureIdentity, Is.EqualTo("KernelTests"));
                Assert.That(report.Header.Run.ProfileIdentity, Is.EqualTo("Test"));
                Assert.That(report.TotalCount, Is.EqualTo(0));
                Assert.That(report.Notes, Is.Empty);
            }
            finally
            {
                DeleteDirectory(runDirectory);
            }
        }

        static KernelTestRunMetadata CreateMetadata(string runDirectory)
        {
            return new KernelTestRunMetadata
            {
                RunId = "20260518-120000_EditMode_Artifacts",
                Platform = "EditMode",
                TestFilter = "KernelTests",
                Target = "KernelTests",
                FixtureIdentity = "KernelTests",
                ProfileIdentity = "Test",
                RunDirectory = runDirectory,
                GeneratedAtUtc = "2026-05-18T12:00:00.0000000Z",
            };
        }

        static string CreateTempRunDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), "KernelTestArtifacts_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
    }
}
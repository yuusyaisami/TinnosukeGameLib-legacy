#nullable enable
using System;
using System.IO;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [SetUpFixture]
    public sealed class KernelRootTestArtifactLifecycle
    {
        const string RunDirectoryEnvironmentVariable = "KERNEL_TEST_RUN_DIRECTORY";
        const string RunIdEnvironmentVariable = "KERNEL_TEST_RUN_ID";
        const string PlatformEnvironmentVariable = "KERNEL_TEST_PLATFORM";
        const string TestFilterEnvironmentVariable = "KERNEL_TEST_FILTER";
        const string TargetEnvironmentVariable = "KERNEL_TEST_TARGET";

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            string? runDirectory = Environment.GetEnvironmentVariable(RunDirectoryEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(runDirectory))
                return;

            if (!Directory.Exists(runDirectory))
                Directory.CreateDirectory(runDirectory);

            WritePlaceholderReportIfMissing(Path.Combine(runDirectory, "DiagnosticsReport.json"), "DiagnosticsReport");
            WritePlaceholderReportIfMissing(Path.Combine(runDirectory, "ValidationReport.json"), "ValidationReport");
            WritePlaceholderReportIfMissing(Path.Combine(runDirectory, "GenerationReport.json"), "GenerationReport");
            WritePlaceholderReportIfMissing(Path.Combine(runDirectory, "PerformanceReport.json"), "PerformanceReport");
            WritePlaceholderMarkdownIfMissing(Path.Combine(runDirectory, "PerformanceReport.md"), "PerformanceReport");
        }

        static void WritePlaceholderReportIfMissing(string path, string reportKind)
        {
            if (File.Exists(path))
                return;

            WritePlaceholderReport(path, reportKind);
        }

        static void WritePlaceholderMarkdownIfMissing(string path, string reportKind)
        {
            if (File.Exists(path))
                return;

            WritePlaceholderMarkdown(path, reportKind);
        }

        static void WritePlaceholderReport(string path, string reportKind)
        {
            string json =
                "{\n" +
                "  \"schemaVersion\": \"1\",\n" +
                "  \"reportKind\": \"" + reportKind + "\",\n" +
                "  \"isPlaceholder\": true,\n" +
                "  \"runId\": \"" + Escape(Environment.GetEnvironmentVariable(RunIdEnvironmentVariable) ?? string.Empty) + "\",\n" +
                "  \"platform\": \"" + Escape(Environment.GetEnvironmentVariable(PlatformEnvironmentVariable) ?? string.Empty) + "\",\n" +
                "  \"testFilter\": \"" + Escape(Environment.GetEnvironmentVariable(TestFilterEnvironmentVariable) ?? string.Empty) + "\",\n" +
                "  \"target\": \"" + Escape(Environment.GetEnvironmentVariable(TargetEnvironmentVariable) ?? string.Empty) + "\",\n" +
                "  \"generatedAtUtc\": \"" + DateTime.UtcNow.ToString("O") + "\"\n" +
                "}\n";
            File.WriteAllText(path, json);
        }

        static void WritePlaceholderMarkdown(string path, string reportKind)
        {
            string markdown =
                "# " + reportKind + "\n\n" +
                "- Placeholder: true\n" +
                "- Generated At Utc: " + DateTime.UtcNow.ToString("O") + "\n";
            File.WriteAllText(path, markdown);
        }

        static string Escape(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }
    }
}

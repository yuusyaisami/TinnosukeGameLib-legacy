#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Game.Kernel.Diagnostics
{
    [Serializable]
    public sealed class KernelPerformanceReportEntry
    {
        public string TestId = string.Empty;
        public string Subsystem = string.Empty;
        public string Operation = string.Empty;
        public string PathKind = string.Empty;
        public int FixtureSize;
        public string Profile = string.Empty;
        public double ElapsedMilliseconds;
        public long AllocationBytes;
        public int CallCount;
        public string[] MarkerSamples = Array.Empty<string>();
        public bool Passed;
        public long ExpectedMaxAllocationBytes;
        public double ExpectedMaxElapsedMilliseconds;
        public long AllowedAllocationRegressionBytes;
        public double AllowedElapsedRegressionMilliseconds;
        public string FailureCode = string.Empty;
        public string BaselineLabel = string.Empty;
        public bool HasBaseline;
        public long BaselineAllocationBytes;
        public double BaselineElapsedMilliseconds;
        public long AllocationDeltaBytes;
        public double ElapsedDeltaMilliseconds;
        public string FailureReason = string.Empty;
    }

    [Serializable]
    public sealed class KernelPerformanceReportSummary
    {
        public int TotalCount;
        public int PassedCount;
        public int FailedCount;
        public long TotalAllocationBytes;
        public double TotalElapsedMilliseconds;
        public long MaxAllocationBytes;
        public double MaxElapsedMilliseconds;
    }

    [Serializable]
    public sealed class KernelPerformanceReport
    {
        public KernelTestReportHeader Header = new KernelTestReportHeader();
        public KernelPerformanceReportSummary Summary = new KernelPerformanceReportSummary();
        public KernelPerformanceReportEntry[] Entries = Array.Empty<KernelPerformanceReportEntry>();
    }

    public static class KernelPerformanceReportCollector
    {
        static readonly List<KernelPerformanceReportEntry> Entries = new List<KernelPerformanceReportEntry>(16);

        public static void Reset()
        {
            Entries.Clear();
        }

        public static void Record(KernelPerformanceReportEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            Entries.Add(Clone(entry));
        }

        public static KernelPerformanceReportEntry[] Snapshot()
        {
            if (Entries.Count == 0)
                return Array.Empty<KernelPerformanceReportEntry>();

            return Entries.ToArray();
        }

        static KernelPerformanceReportEntry Clone(KernelPerformanceReportEntry source)
        {
            return new KernelPerformanceReportEntry
            {
                TestId = source.TestId,
                Subsystem = source.Subsystem,
                Operation = source.Operation,
                PathKind = source.PathKind,
                FixtureSize = source.FixtureSize,
                Profile = source.Profile,
                ElapsedMilliseconds = source.ElapsedMilliseconds,
                AllocationBytes = source.AllocationBytes,
                CallCount = source.CallCount,
                MarkerSamples = source.MarkerSamples != null ? (string[])source.MarkerSamples.Clone() : Array.Empty<string>(),
                Passed = source.Passed,
                ExpectedMaxAllocationBytes = source.ExpectedMaxAllocationBytes,
                ExpectedMaxElapsedMilliseconds = source.ExpectedMaxElapsedMilliseconds,
                AllowedAllocationRegressionBytes = source.AllowedAllocationRegressionBytes,
                AllowedElapsedRegressionMilliseconds = source.AllowedElapsedRegressionMilliseconds,
                FailureCode = source.FailureCode,
                BaselineLabel = source.BaselineLabel,
                HasBaseline = source.HasBaseline,
                BaselineAllocationBytes = source.BaselineAllocationBytes,
                BaselineElapsedMilliseconds = source.BaselineElapsedMilliseconds,
                AllocationDeltaBytes = source.AllocationDeltaBytes,
                ElapsedDeltaMilliseconds = source.ElapsedDeltaMilliseconds,
                FailureReason = source.FailureReason,
            };
        }
    }

    public static class KernelPerformanceReportFormatter
    {
        public static KernelPerformanceReport CreateReport(KernelTestRunMetadata metadata, IReadOnlyList<KernelPerformanceReportEntry> entries)
        {
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));

            ValidateEntries(entries);

            var report = new KernelPerformanceReport
            {
                Header = CreateHeader(metadata, entries.Count == 0),
                Summary = BuildSummary(entries),
                Entries = BuildEntries(entries),
            };

            return report;
        }

        public static string CreateMarkdown(KernelPerformanceReport report)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));

            var sb = new StringBuilder(4096);
            sb.AppendLine("# Performance Report");
            sb.AppendLine();
            AppendHeaderBlock(sb, report.Header);
            AppendSummaryBlock(sb, report.Summary);
            AppendTable(sb, report.Entries);
            return sb.ToString();
        }

        public static void WriteReportFiles(string runDirectory, KernelPerformanceReport report)
        {
            if (string.IsNullOrWhiteSpace(runDirectory))
                throw new ArgumentException("Run directory must not be blank.", nameof(runDirectory));
            if (report == null)
                throw new ArgumentNullException(nameof(report));

            Directory.CreateDirectory(runDirectory);

            string jsonPath = Path.Combine(runDirectory, "PerformanceReport.json");
            string markdownPath = Path.Combine(runDirectory, "PerformanceReport.md");

            File.WriteAllText(jsonPath, JsonUtility.ToJson(report, true) + Environment.NewLine, new UTF8Encoding(false));
            File.WriteAllText(markdownPath, CreateMarkdown(report), new UTF8Encoding(false));
        }

        static KernelTestReportHeader CreateHeader(KernelTestRunMetadata metadata, bool isPlaceholder)
        {
            return new KernelTestReportHeader
            {
                SchemaVersion = "1",
                ReportKind = "PerformanceReport",
                IsPlaceholder = isPlaceholder,
                Run = new KernelTestRunIdentity
                {
                    RunId = metadata.RunId,
                    Platform = metadata.Platform,
                    TestFilter = metadata.TestFilter,
                    Target = metadata.Target,
                    FixtureIdentity = metadata.FixtureIdentity,
                    ProfileIdentity = metadata.ProfileIdentity,
                },
                Transient = new KernelTestTransientMetadata
                {
                    RunDirectory = metadata.RunDirectory,
                    GeneratedAtUtc = metadata.GeneratedAtUtc,
                },
            };
        }

        static KernelPerformanceReportSummary BuildSummary(IReadOnlyList<KernelPerformanceReportEntry> entries)
        {
            var summary = new KernelPerformanceReportSummary
            {
                TotalCount = entries.Count,
            };

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                summary.TotalAllocationBytes += entry.AllocationBytes;
                summary.TotalElapsedMilliseconds += entry.ElapsedMilliseconds;
                if (entry.AllocationBytes > summary.MaxAllocationBytes)
                    summary.MaxAllocationBytes = entry.AllocationBytes;
                if (entry.ElapsedMilliseconds > summary.MaxElapsedMilliseconds)
                    summary.MaxElapsedMilliseconds = entry.ElapsedMilliseconds;

                if (entry.Passed)
                    summary.PassedCount++;
                else
                    summary.FailedCount++;
            }

            return summary;
        }

        static KernelPerformanceReportEntry[] BuildEntries(IReadOnlyList<KernelPerformanceReportEntry> entries)
        {
            var snapshot = new KernelPerformanceReportEntry[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                snapshot[i] = Clone(entries[i]);
            }

            return snapshot;
        }

        static void ValidateEntries(IReadOnlyList<KernelPerformanceReportEntry> entries)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                KernelPerformanceReportEntry entry = entries[i];

                if (entry == null)
                    throw new ArgumentNullException(nameof(entries), "Performance report entries must not contain null items.");

                if (string.IsNullOrWhiteSpace(entry.TestId))
                    throw new InvalidOperationException("Performance report entry is missing TestId.");
                if (string.IsNullOrWhiteSpace(entry.Subsystem))
                    throw new InvalidOperationException($"Performance report entry '{entry.TestId}' is missing Subsystem.");
                if (string.IsNullOrWhiteSpace(entry.Operation))
                    throw new InvalidOperationException($"Performance report entry '{entry.TestId}' is missing Operation.");
                if (string.IsNullOrWhiteSpace(entry.PathKind))
                    throw new InvalidOperationException($"Performance report entry '{entry.TestId}' is missing PathKind.");
                if (string.IsNullOrWhiteSpace(entry.Profile))
                    throw new InvalidOperationException($"Performance report entry '{entry.TestId}' is missing Profile.");
                if (entry.CallCount <= 0)
                    throw new InvalidOperationException($"Performance report entry '{entry.TestId}' must record at least one call.");
                if (entry.ExpectedMaxAllocationBytes < 0)
                    throw new InvalidOperationException($"Performance report entry '{entry.TestId}' has a negative allocation ceiling.");
                if (entry.ExpectedMaxElapsedMilliseconds < 0d)
                    throw new InvalidOperationException($"Performance report entry '{entry.TestId}' has a negative elapsed ceiling.");
                if (entry.AllowedAllocationRegressionBytes < 0)
                    throw new InvalidOperationException($"Performance report entry '{entry.TestId}' has a negative allocation regression allowance.");
                if (entry.AllowedElapsedRegressionMilliseconds < 0d)
                    throw new InvalidOperationException($"Performance report entry '{entry.TestId}' has a negative elapsed regression allowance.");
                if (entry.Passed && !string.IsNullOrWhiteSpace(entry.FailureCode))
                    throw new InvalidOperationException($"Performance report entry '{entry.TestId}' cannot have a failure code when it passed.");
                if (!entry.Passed && string.IsNullOrWhiteSpace(entry.FailureCode))
                    throw new InvalidOperationException($"Performance report entry '{entry.TestId}' failed without a failure code.");
            }
        }

        static KernelPerformanceReportEntry Clone(KernelPerformanceReportEntry source)
        {
            return new KernelPerformanceReportEntry
            {
                TestId = source.TestId,
                Subsystem = source.Subsystem,
                Operation = source.Operation,
                PathKind = source.PathKind,
                FixtureSize = source.FixtureSize,
                Profile = source.Profile,
                ElapsedMilliseconds = source.ElapsedMilliseconds,
                AllocationBytes = source.AllocationBytes,
                CallCount = source.CallCount,
                MarkerSamples = source.MarkerSamples != null ? (string[])source.MarkerSamples.Clone() : Array.Empty<string>(),
                Passed = source.Passed,
                ExpectedMaxAllocationBytes = source.ExpectedMaxAllocationBytes,
                ExpectedMaxElapsedMilliseconds = source.ExpectedMaxElapsedMilliseconds,
                AllowedAllocationRegressionBytes = source.AllowedAllocationRegressionBytes,
                AllowedElapsedRegressionMilliseconds = source.AllowedElapsedRegressionMilliseconds,
                FailureCode = source.FailureCode,
                BaselineLabel = source.BaselineLabel,
                HasBaseline = source.HasBaseline,
                BaselineAllocationBytes = source.BaselineAllocationBytes,
                BaselineElapsedMilliseconds = source.BaselineElapsedMilliseconds,
                AllocationDeltaBytes = source.AllocationDeltaBytes,
                ElapsedDeltaMilliseconds = source.ElapsedDeltaMilliseconds,
                FailureReason = source.FailureReason,
            };
        }

        static void AppendHeaderBlock(StringBuilder sb, KernelTestReportHeader header)
        {
            sb.AppendLine($"- Report Kind: {header.ReportKind}");
            sb.AppendLine($"- Placeholder: {header.IsPlaceholder}");
            sb.AppendLine($"- Run Id: {header.Run.RunId}");
            sb.AppendLine($"- Platform: {header.Run.Platform}");
            sb.AppendLine($"- Test Filter: {header.Run.TestFilter}");
            sb.AppendLine($"- Target: {header.Run.Target}");
            sb.AppendLine($"- Fixture Identity: {header.Run.FixtureIdentity}");
            sb.AppendLine($"- Profile Identity: {header.Run.ProfileIdentity}");
            sb.AppendLine($"- Run Directory: {header.Transient.RunDirectory}");
            sb.AppendLine($"- Generated At Utc: {header.Transient.GeneratedAtUtc}");
            sb.AppendLine();
        }

        static void AppendSummaryBlock(StringBuilder sb, KernelPerformanceReportSummary summary)
        {
            sb.AppendLine("## Summary");
            sb.AppendLine();
            sb.AppendLine($"- Total Count: {summary.TotalCount}");
            sb.AppendLine($"- Passed Count: {summary.PassedCount}");
            sb.AppendLine($"- Failed Count: {summary.FailedCount}");
            sb.AppendLine($"- Total Allocation Bytes: {summary.TotalAllocationBytes}");
            sb.AppendLine($"- Total Elapsed Milliseconds: {summary.TotalElapsedMilliseconds.ToString("0.###", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"- Max Allocation Bytes: {summary.MaxAllocationBytes}");
            sb.AppendLine($"- Max Elapsed Milliseconds: {summary.MaxElapsedMilliseconds.ToString("0.###", CultureInfo.InvariantCulture)}");
            sb.AppendLine();
        }

        static void AppendTable(StringBuilder sb, KernelPerformanceReportEntry[] entries)
        {
            sb.AppendLine("## Results");
            sb.AppendLine();
            sb.AppendLine("| Test ID | Subsystem | Operation | Path Kind | Fixture Size | Profile | Calls | Elapsed ms | Allocation B | Expected Max B | Expected Max ms | Allowed Alloc Regression B | Allowed Elapsed Regression ms | Passed | Baseline | Delta B | Delta ms | Marker Samples | Failure Code | Failure Reason |");
            sb.AppendLine("| --- | --- | --- | --- | ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- | ---: | ---: | --- | --- | --- |");

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                sb.Append("| ")
                    .Append(EscapeCell(entry.TestId)).Append(" | ")
                    .Append(EscapeCell(entry.Subsystem)).Append(" | ")
                    .Append(EscapeCell(entry.Operation)).Append(" | ")
                    .Append(EscapeCell(entry.PathKind)).Append(" | ")
                    .Append(entry.FixtureSize).Append(" | ")
                    .Append(EscapeCell(entry.Profile)).Append(" | ")
                    .Append(entry.CallCount).Append(" | ")
                    .Append(entry.ElapsedMilliseconds.ToString("0.###", CultureInfo.InvariantCulture)).Append(" | ")
                    .Append(entry.AllocationBytes).Append(" | ")
                    .Append(entry.ExpectedMaxAllocationBytes).Append(" | ")
                        .Append(entry.ExpectedMaxElapsedMilliseconds.ToString("0.###", CultureInfo.InvariantCulture)).Append(" | ")
                        .Append(entry.AllowedAllocationRegressionBytes).Append(" | ")
                        .Append(entry.AllowedElapsedRegressionMilliseconds.ToString("0.###", CultureInfo.InvariantCulture)).Append(" | ")
                    .Append(entry.Passed ? "Pass" : "Fail").Append(" | ")
                    .Append(EscapeCell(FormatBaseline(entry))).Append(" | ")
                    .Append(entry.HasBaseline ? entry.AllocationDeltaBytes.ToString(CultureInfo.InvariantCulture) : "-").Append(" | ")
                    .Append(entry.HasBaseline ? entry.ElapsedDeltaMilliseconds.ToString("0.###", CultureInfo.InvariantCulture) : "-").Append(" | ")
                    .Append(EscapeCell(FormatMarkerSamples(entry.MarkerSamples))).Append(" | ")
                        .Append(EscapeCell(entry.FailureCode)).Append(" | ")
                    .Append(EscapeCell(entry.FailureReason)).AppendLine(" |");
            }

            sb.AppendLine();
        }

        static string FormatBaseline(KernelPerformanceReportEntry entry)
        {
            if (!entry.HasBaseline)
                return string.Empty;

            return string.IsNullOrWhiteSpace(entry.BaselineLabel)
                ? $"alloc={entry.BaselineAllocationBytes}, elapsed={entry.BaselineElapsedMilliseconds.ToString("0.###", CultureInfo.InvariantCulture)}"
                : $"{entry.BaselineLabel}: alloc={entry.BaselineAllocationBytes}, elapsed={entry.BaselineElapsedMilliseconds.ToString("0.###", CultureInfo.InvariantCulture)}";
        }

        static string FormatMarkerSamples(IReadOnlyList<string> markerSamples)
        {
            if (markerSamples == null || markerSamples.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            for (int i = 0; i < markerSamples.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append(markerSamples[i]);
            }

            return sb.ToString();
        }

        static string EscapeCell(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "-";

            return value
                .Replace("|", "\\|")
                .Replace("\r\n", " ")
                .Replace("\r", " ")
                .Replace("\n", " ");
        }
    }
}
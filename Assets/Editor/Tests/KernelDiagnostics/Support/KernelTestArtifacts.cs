#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Game.Kernel.Diagnostics
{
    [Serializable]
    public sealed class KernelTestRunMetadata
    {
        public string RunId = string.Empty;
        public string Platform = string.Empty;
        public string TestFilter = string.Empty;
        public string Target = string.Empty;
        public string FixtureIdentity = string.Empty;
        public string ProfileIdentity = string.Empty;
        public string RunDirectory = string.Empty;
        public string GeneratedAtUtc = string.Empty;
    }

    [Serializable]
    public sealed class KernelTestRunIdentity
    {
        public string RunId = string.Empty;
        public string Platform = string.Empty;
        public string TestFilter = string.Empty;
        public string Target = string.Empty;
        public string FixtureIdentity = string.Empty;
        public string ProfileIdentity = string.Empty;
    }

    [Serializable]
    public sealed class KernelTestTransientMetadata
    {
        public string RunDirectory = string.Empty;
        public string GeneratedAtUtc = string.Empty;
    }

    [Serializable]
    public sealed class KernelTestReportHeader
    {
        public string SchemaVersion = "1";
        public string ReportKind = string.Empty;
        public bool IsPlaceholder;
        public KernelTestRunIdentity Run = new KernelTestRunIdentity();
        public KernelTestTransientMetadata Transient = new KernelTestTransientMetadata();
    }

    [Serializable]
    public sealed class DiagnosticCountEntry
    {
        public string Name = string.Empty;
        public int Count;
    }

    [Serializable]
    public sealed class DiagnosticRuntimeIdentityRecord
    {
        public string Kind = string.Empty;
        public int Value;
        public int Generation;
    }

    [Serializable]
    public sealed class DiagnosticPayloadRecord
    {
        public string Key = string.Empty;
        public string ValueKind = string.Empty;
        public string Value = string.Empty;
    }

    [Serializable]
    public sealed class DiagnosticExceptionRecord
    {
        public string Type = string.Empty;
        public string Message = string.Empty;
        public string StackTrace = string.Empty;
        public DiagnosticExceptionRecord? Inner;
    }

    [Serializable]
    public sealed class DiagnosticRecord
    {
        public long EventId;
        public long SessionId;
        public long CorrelationId;
        public string Code = string.Empty;
        public string Severity = string.Empty;
        public string Domain = string.Empty;
        public string FailureBoundary = string.Empty;
        public string Message = string.Empty;
        public int OwnerModule;
        public int Source;
        public int ArtifactSetId;
        public int GeneratedArtifactId;
        public int ProfileId;
        public string Phase = string.Empty;
        public DiagnosticRuntimeIdentityRecord[] RuntimeIdentities = Array.Empty<DiagnosticRuntimeIdentityRecord>();
        public DiagnosticPayloadRecord[] Payload = Array.Empty<DiagnosticPayloadRecord>();
        public DiagnosticExceptionRecord? Exception;
    }

    [Serializable]
    public sealed class DiagnosticsReport
    {
        public KernelTestReportHeader Header = new KernelTestReportHeader();
        public int TotalCount;
        public DiagnosticCountEntry[] CountBySeverity = Array.Empty<DiagnosticCountEntry>();
        public DiagnosticCountEntry[] CountByDomain = Array.Empty<DiagnosticCountEntry>();
        public DiagnosticRecord[] Records = Array.Empty<DiagnosticRecord>();
    }

    [Serializable]
    public sealed class EmptyKernelTestReport
    {
        public KernelTestReportHeader Header = new KernelTestReportHeader();
        public int TotalCount;
        public string[] Notes = Array.Empty<string>();
    }

    public static class KernelTestArtifactCollector
    {
        static readonly List<KernelDiagnostic> Diagnostics = new List<KernelDiagnostic>();
        static KernelTestRunMetadata? _metadata;

        public static bool IsEnabled => _metadata != null && !string.IsNullOrWhiteSpace(_metadata.RunDirectory);

        public static KernelTestRunMetadata? Metadata => _metadata;

        public static void Configure(KernelTestRunMetadata metadata)
        {
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));
            if (string.IsNullOrWhiteSpace(metadata.RunDirectory))
                throw new ArgumentException("Run directory must not be blank.", nameof(metadata));

            _metadata = metadata;
            Diagnostics.Clear();
        }

        public static void RecordDiagnostic(in KernelDiagnostic diagnostic)
        {
            if (!IsEnabled)
                return;

            Diagnostics.Add(diagnostic);
        }

        public static KernelDiagnostic[] SnapshotDiagnostics()
        {
            if (Diagnostics.Count == 0)
                return Array.Empty<KernelDiagnostic>();

            return Diagnostics.ToArray();
        }

        public static void Reset()
        {
            Diagnostics.Clear();
            _metadata = null;
        }
    }

    public static class KernelTestArtifactWriter
    {
        public static void WriteArtifacts(KernelTestRunMetadata metadata, IReadOnlyList<KernelDiagnostic> diagnostics)
        {
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));
            if (diagnostics == null)
                throw new ArgumentNullException(nameof(diagnostics));
            if (string.IsNullOrWhiteSpace(metadata.RunDirectory))
                throw new ArgumentException("Run directory must not be blank.", nameof(metadata));

            Directory.CreateDirectory(metadata.RunDirectory);

            WriteJson(Path.Combine(metadata.RunDirectory, "DiagnosticsReport.json"), CreateDiagnosticsReport(metadata, diagnostics));
            WriteJson(Path.Combine(metadata.RunDirectory, "ValidationReport.json"), CreateEmptyReport(metadata, "ValidationReport", true));
            WriteJson(Path.Combine(metadata.RunDirectory, "GenerationReport.json"), CreateEmptyReport(metadata, "GenerationReport", true));
            WriteJson(Path.Combine(metadata.RunDirectory, "PerformanceReport.json"), CreateEmptyReport(metadata, "PerformanceReport", true));
        }

        public static DiagnosticsReport CreateDiagnosticsReport(KernelTestRunMetadata metadata, IReadOnlyList<KernelDiagnostic> diagnostics)
        {
            var report = new DiagnosticsReport
            {
                Header = CreateHeader(metadata, "DiagnosticsReport", diagnostics.Count == 0),
                TotalCount = diagnostics.Count,
                CountBySeverity = BuildSeverityCounts(diagnostics),
                CountByDomain = BuildDomainCounts(diagnostics),
                Records = BuildDiagnosticRecords(diagnostics),
            };

            return report;
        }

        public static EmptyKernelTestReport CreateEmptyReport(KernelTestRunMetadata metadata, string reportKind, bool isPlaceholder)
        {
            return new EmptyKernelTestReport
            {
                Header = CreateHeader(metadata, reportKind, isPlaceholder),
                TotalCount = 0,
                Notes = Array.Empty<string>(),
            };
        }

        static KernelTestReportHeader CreateHeader(KernelTestRunMetadata metadata, string reportKind, bool isPlaceholder)
        {
            return new KernelTestReportHeader
            {
                SchemaVersion = "1",
                ReportKind = reportKind,
                IsPlaceholder = isPlaceholder,
                Run = CreateRunIdentity(metadata),
                Transient = CreateTransientMetadata(metadata),
            };
        }

        static KernelTestRunIdentity CreateRunIdentity(KernelTestRunMetadata metadata)
        {
            return new KernelTestRunIdentity
            {
                RunId = metadata.RunId,
                Platform = metadata.Platform,
                TestFilter = metadata.TestFilter,
                Target = metadata.Target,
                FixtureIdentity = metadata.FixtureIdentity,
                ProfileIdentity = metadata.ProfileIdentity,
            };
        }

        static KernelTestTransientMetadata CreateTransientMetadata(KernelTestRunMetadata metadata)
        {
            return new KernelTestTransientMetadata
            {
                RunDirectory = metadata.RunDirectory,
                GeneratedAtUtc = metadata.GeneratedAtUtc,
            };
        }

        static DiagnosticCountEntry[] BuildSeverityCounts(IReadOnlyList<KernelDiagnostic> diagnostics)
        {
            DiagnosticSeverity[] orderedSeverities =
            {
                DiagnosticSeverity.Trace,
                DiagnosticSeverity.Info,
                DiagnosticSeverity.Warning,
                DiagnosticSeverity.Error,
                DiagnosticSeverity.Fatal,
            };

            return BuildCountEntries(diagnostics, orderedSeverities, static diagnostic => diagnostic.Severity.ToString());
        }

        static DiagnosticCountEntry[] BuildDomainCounts(IReadOnlyList<KernelDiagnostic> diagnostics)
        {
            DiagnosticDomain[] orderedDomains =
            {
                DiagnosticDomain.Kernel,
                DiagnosticDomain.Boot,
                DiagnosticDomain.Generation,
                DiagnosticDomain.Validation,
                DiagnosticDomain.ServiceGraph,
                DiagnosticDomain.ScopeGraph,
                DiagnosticDomain.Lifecycle,
                DiagnosticDomain.Command,
                DiagnosticDomain.Value,
                DiagnosticDomain.RuntimeQuery,
                DiagnosticDomain.Save,
                DiagnosticDomain.UnityBridge,
                DiagnosticDomain.Diagnostics,
                DiagnosticDomain.LegacyCompat,
            };

            return BuildCountEntries(diagnostics, orderedDomains, static diagnostic => diagnostic.Domain.ToString());
        }

        static DiagnosticCountEntry[] BuildCountEntries<TEnum>(IReadOnlyList<KernelDiagnostic> diagnostics, TEnum[] orderedValues, Func<KernelDiagnostic, string> selector)
            where TEnum : struct
        {
            var counts = new DiagnosticCountEntry[orderedValues.Length];
            for (int i = 0; i < orderedValues.Length; i++)
            {
                counts[i] = new DiagnosticCountEntry
                {
                    Name = orderedValues[i].ToString() ?? string.Empty,
                    Count = 0,
                };
            }

            for (int i = 0; i < diagnostics.Count; i++)
            {
                string key = selector(diagnostics[i]);
                for (int j = 0; j < counts.Length; j++)
                {
                    if (string.Equals(counts[j].Name, key, StringComparison.Ordinal))
                    {
                        counts[j].Count++;
                        break;
                    }
                }
            }

            return counts;
        }

        static DiagnosticRecord[] BuildDiagnosticRecords(IReadOnlyList<KernelDiagnostic> diagnostics)
        {
            var records = new DiagnosticRecord[diagnostics.Count];
            for (int i = 0; i < diagnostics.Count; i++)
            {
                KernelDiagnostic diagnostic = diagnostics[i];
                records[i] = new DiagnosticRecord
                {
                    EventId = diagnostic.EventId.Value,
                    SessionId = diagnostic.SessionId.Value,
                    CorrelationId = diagnostic.CorrelationId.Value,
                    Code = diagnostic.Code.Value,
                    Severity = diagnostic.Severity.ToString(),
                    Domain = diagnostic.Domain.ToString(),
                    FailureBoundary = diagnostic.FailureBoundary.ToString(),
                    Message = diagnostic.Message ?? string.Empty,
                    OwnerModule = diagnostic.Context.OwnerModule.Value,
                    Source = diagnostic.Context.Source.Value,
                    ArtifactSetId = diagnostic.Context.Artifact.ArtifactSetId,
                    GeneratedArtifactId = diagnostic.Context.Artifact.GeneratedArtifactId,
                    ProfileId = diagnostic.Context.ProfileId,
                    Phase = diagnostic.Context.Phase ?? string.Empty,
                    RuntimeIdentities = BuildRuntimeIdentities(diagnostic.Context.RuntimeIdentities),
                    Payload = BuildPayloadRecords(diagnostic.Payload.Entries),
                    Exception = BuildExceptionRecord(diagnostic.Exception),
                };
            }

            return records;
        }

        static DiagnosticRuntimeIdentityRecord[] BuildRuntimeIdentities(IReadOnlyList<RuntimeIdentityRef> runtimeIdentities)
        {
            var records = new DiagnosticRuntimeIdentityRecord[runtimeIdentities.Count];
            for (int i = 0; i < runtimeIdentities.Count; i++)
            {
                records[i] = new DiagnosticRuntimeIdentityRecord
                {
                    Kind = runtimeIdentities[i].Kind.ToString(),
                    Value = runtimeIdentities[i].Value,
                    Generation = runtimeIdentities[i].Generation,
                };
            }

            return records;
        }

        static DiagnosticPayloadRecord[] BuildPayloadRecords(IReadOnlyList<DiagnosticPayloadEntry> payloadEntries)
        {
            var records = new DiagnosticPayloadRecord[payloadEntries.Count];
            for (int i = 0; i < payloadEntries.Count; i++)
            {
                records[i] = new DiagnosticPayloadRecord
                {
                    Key = payloadEntries[i].Key,
                    ValueKind = payloadEntries[i].Value.Kind.ToString(),
                    Value = payloadEntries[i].Value.RawValue?.ToString() ?? string.Empty,
                };
            }

            return records;
        }

        static DiagnosticExceptionRecord? BuildExceptionRecord(DiagnosticExceptionInfo? exception)
        {
            if (exception == null)
                return null;

            return new DiagnosticExceptionRecord
            {
                Type = exception.Type,
                Message = exception.Message ?? string.Empty,
                StackTrace = exception.StackTrace ?? string.Empty,
                Inner = BuildExceptionRecord(exception.Inner),
            };
        }

        static void WriteJson<T>(string path, T report)
        {
            string json = JsonUtility.ToJson(report, true);
            File.WriteAllText(path, json + Environment.NewLine, new UTF8Encoding(false));
        }
    }
}
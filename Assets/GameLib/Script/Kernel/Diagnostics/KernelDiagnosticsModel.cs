#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Game.Kernel.Diagnostics
{
    public enum DiagnosticSeverity
    {
        Trace = 10,
        Info = 20,
        Warning = 30,
        Error = 40,
        Fatal = 50,
    }

    public enum DiagnosticFailureBoundary
    {
        None = 0,
        Operation = 10,
        Command = 20,
        CommandFrame = 30,
        Scope = 40,
        Scene = 50,
        Kernel = 60,
        Build = 70,
    }

    public enum DiagnosticDomain
    {
        Kernel = 10,
        Boot = 20,
        Generation = 30,
        Validation = 40,
        ServiceGraph = 50,
        ScopeGraph = 60,
        Lifecycle = 70,
        Command = 80,
        Value = 90,
        RuntimeQuery = 100,
        Save = 110,
        UnityBridge = 120,
        Diagnostics = 130,
        LegacyCompat = 900,
    }

    public enum RuntimeIdentityKind
    {
        None = 0,
        Module = 10,
        Service = 20,
        ScopeAuthoring = 30,
        ScopePlan = 40,
        ScopeHandle = 50,
        LifecyclePlan = 60,
        LifecycleStep = 70,
        CommandType = 80,
        CommandExecutor = 90,
        CommandPayloadSchema = 100,
        ValueKey = 110,
        ValueSchema = 120,
        RuntimeQuery = 130,
        ArtifactSet = 140,
        GeneratedArtifact = 150,
    }

    public enum DiagnosticPayloadValueKind
    {
        None = 0,
        String = 10,
        Int32 = 20,
        Int64 = 30,
        Boolean = 40,
        Double = 50,
    }

    public readonly struct DiagnosticCode : IEquatable<DiagnosticCode>
    {
        public DiagnosticCode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Diagnostic code must not be blank.", nameof(value));

            Value = value;
        }

        public string Value { get; }

        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public bool Equals(DiagnosticCode other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is DiagnosticCode other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value != null ? StringComparer.Ordinal.GetHashCode(Value) : 0;
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool operator ==(DiagnosticCode left, DiagnosticCode right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DiagnosticCode left, DiagnosticCode right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct DiagnosticEventId : IEquatable<DiagnosticEventId>
    {
        public DiagnosticEventId(long value)
        {
            Value = value;
        }

        public long Value { get; }

        public bool Equals(DiagnosticEventId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is DiagnosticEventId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    public readonly struct DiagnosticCorrelationId : IEquatable<DiagnosticCorrelationId>
    {
        public DiagnosticCorrelationId(long value)
        {
            Value = value;
        }

        public long Value { get; }

        public bool Equals(DiagnosticCorrelationId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is DiagnosticCorrelationId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    public readonly struct DiagnosticSessionId : IEquatable<DiagnosticSessionId>
    {
        public DiagnosticSessionId(long value)
        {
            Value = value;
        }

        public long Value { get; }

        public bool Equals(DiagnosticSessionId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is DiagnosticSessionId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    public readonly struct ModuleIdentityRef : IEquatable<ModuleIdentityRef>
    {
        public ModuleIdentityRef(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool IsEmpty => Value == 0;

        public bool Equals(ModuleIdentityRef other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is ModuleIdentityRef other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    public readonly struct RuntimeIdentityRef : IEquatable<RuntimeIdentityRef>
    {
        public RuntimeIdentityRef(RuntimeIdentityKind kind, int value, int generation = 0)
        {
            Kind = kind;
            Value = value;
            Generation = generation;
        }

        public RuntimeIdentityKind Kind { get; }
        public int Value { get; }
        public int Generation { get; }

        public bool IsEmpty => Kind == RuntimeIdentityKind.None && Value == 0 && Generation == 0;

        public bool Equals(RuntimeIdentityRef other)
        {
            return Kind == other.Kind && Value == other.Value && Generation == other.Generation;
        }

        public override bool Equals(object? obj)
        {
            return obj is RuntimeIdentityRef other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ Value;
                hash = (hash * 397) ^ Generation;
                return hash;
            }
        }

        public override string ToString()
        {
            return Generation != 0
                ? Kind + ":" + Value + "@" + Generation
                : Kind + ":" + Value;
        }
    }

    public readonly struct SourceLocationRef : IEquatable<SourceLocationRef>
    {
        public SourceLocationRef(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(SourceLocationRef other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is SourceLocationRef other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    public readonly struct ArtifactIdentityRef : IEquatable<ArtifactIdentityRef>
    {
        public ArtifactIdentityRef(int artifactSetId, int generatedArtifactId = 0)
        {
            ArtifactSetId = artifactSetId;
            GeneratedArtifactId = generatedArtifactId;
        }

        public int ArtifactSetId { get; }
        public int GeneratedArtifactId { get; }

        public bool Equals(ArtifactIdentityRef other)
        {
            return ArtifactSetId == other.ArtifactSetId && GeneratedArtifactId == other.GeneratedArtifactId;
        }

        public override bool Equals(object? obj)
        {
            return obj is ArtifactIdentityRef other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (ArtifactSetId * 397) ^ GeneratedArtifactId;
            }
        }

        public override string ToString()
        {
            return GeneratedArtifactId != 0
                ? ArtifactSetId + ":" + GeneratedArtifactId
                : ArtifactSetId.ToString();
        }
    }

    public sealed class DiagnosticContext
    {
        readonly ReadOnlyCollection<RuntimeIdentityRef> _runtimeIdentities;

        public DiagnosticContext(
            RuntimeIdentityRef[]? runtimeIdentities = null,
            ModuleIdentityRef ownerModule = default,
            SourceLocationRef source = default,
            ArtifactIdentityRef artifact = default,
            int profileId = 0,
            DiagnosticCorrelationId correlationId = default,
            string? phase = null)
        {
            RuntimeIdentityRef[] snapshot = runtimeIdentities == null || runtimeIdentities.Length == 0
                ? Array.Empty<RuntimeIdentityRef>()
                : CopyRuntimeIdentities(runtimeIdentities);

            _runtimeIdentities = Array.AsReadOnly(snapshot);
            OwnerModule = ownerModule;
            Source = source;
            Artifact = artifact;
            ProfileId = profileId;
            CorrelationId = correlationId;
            Phase = phase;
        }

        public ModuleIdentityRef OwnerModule { get; }
        public SourceLocationRef Source { get; }
        public ArtifactIdentityRef Artifact { get; }
        public int ProfileId { get; }
        public IReadOnlyList<RuntimeIdentityRef> RuntimeIdentities => _runtimeIdentities;
        public DiagnosticCorrelationId CorrelationId { get; }
        public string? Phase { get; }

        static RuntimeIdentityRef[] CopyRuntimeIdentities(RuntimeIdentityRef[] runtimeIdentities)
        {
            var snapshot = new RuntimeIdentityRef[runtimeIdentities.Length];
            Array.Copy(runtimeIdentities, snapshot, runtimeIdentities.Length);
            return snapshot;
        }
    }

    public readonly struct DiagnosticPayloadValue : IEquatable<DiagnosticPayloadValue>
    {
        DiagnosticPayloadValue(object? rawValue, DiagnosticPayloadValueKind kind)
        {
            RawValue = rawValue;
            Kind = kind;
        }

        public object? RawValue { get; }
        public DiagnosticPayloadValueKind Kind { get; }

        public static DiagnosticPayloadValue FromString(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return new DiagnosticPayloadValue(value, DiagnosticPayloadValueKind.String);
        }

        public static DiagnosticPayloadValue FromInt32(int value)
        {
            return new DiagnosticPayloadValue(value, DiagnosticPayloadValueKind.Int32);
        }

        public static DiagnosticPayloadValue FromInt64(long value)
        {
            return new DiagnosticPayloadValue(value, DiagnosticPayloadValueKind.Int64);
        }

        public static DiagnosticPayloadValue FromBoolean(bool value)
        {
            return new DiagnosticPayloadValue(value, DiagnosticPayloadValueKind.Boolean);
        }

        public static DiagnosticPayloadValue FromDouble(double value)
        {
            return new DiagnosticPayloadValue(value, DiagnosticPayloadValueKind.Double);
        }

        public bool Equals(DiagnosticPayloadValue other)
        {
            return Kind == other.Kind && Equals(RawValue, other.RawValue);
        }

        public override bool Equals(object? obj)
        {
            return obj is DiagnosticPayloadValue other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Kind * 397) ^ (RawValue != null ? RawValue.GetHashCode() : 0);
            }
        }

        public override string ToString()
        {
            return RawValue?.ToString() ?? string.Empty;
        }
    }

    public readonly struct DiagnosticPayloadEntry : IEquatable<DiagnosticPayloadEntry>
    {
        public DiagnosticPayloadEntry(string key, DiagnosticPayloadValue value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Payload key must not be blank.", nameof(key));

            Key = key;
            Value = value;
        }

        public string Key { get; }
        public DiagnosticPayloadValue Value { get; }

        public bool Equals(DiagnosticPayloadEntry other)
        {
            return string.Equals(Key, other.Key, StringComparison.Ordinal) && Value.Equals(other.Value);
        }

        public override bool Equals(object? obj)
        {
            return obj is DiagnosticPayloadEntry other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (StringComparer.Ordinal.GetHashCode(Key) * 397) ^ Value.GetHashCode();
            }
        }
    }

    public sealed class DiagnosticPayload
    {
        readonly ReadOnlyCollection<DiagnosticPayloadEntry> _entries;

        public DiagnosticPayload()
            : this(Array.Empty<DiagnosticPayloadEntry>())
        {
        }

        public DiagnosticPayload(IReadOnlyList<DiagnosticPayloadEntry> entries)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));

            var snapshot = new DiagnosticPayloadEntry[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                snapshot[i] = entries[i];
            }

            _entries = Array.AsReadOnly(snapshot);
        }

        public IReadOnlyList<DiagnosticPayloadEntry> Entries => _entries;

        public static DiagnosticPayload Empty { get; } = new DiagnosticPayload();
    }

    public sealed class DiagnosticExceptionInfo
    {
        public DiagnosticExceptionInfo(string type, string? message = null, string? stackTrace = null, DiagnosticExceptionInfo? inner = null)
        {
            if (string.IsNullOrWhiteSpace(type))
                throw new ArgumentException("Exception type must not be blank.", nameof(type));

            Type = type;
            Message = message;
            StackTrace = stackTrace;
            Inner = inner;
        }

        public string Type { get; }
        public string? Message { get; }
        public string? StackTrace { get; }
        public DiagnosticExceptionInfo? Inner { get; }

        public static DiagnosticExceptionInfo FromException(Exception exception)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            return new DiagnosticExceptionInfo(
                exception.GetType().FullName ?? exception.GetType().Name,
                exception.Message,
                exception.StackTrace,
                exception.InnerException != null ? FromException(exception.InnerException) : null);
        }
    }

    public sealed class KernelDiagnostic
    {
        public KernelDiagnostic(
            DiagnosticCode code,
            DiagnosticSeverity severity,
            DiagnosticDomain domain,
            DiagnosticFailureBoundary failureBoundary,
            string? message = null,
            DiagnosticContext? context = null,
            DiagnosticPayload? payload = null,
            DiagnosticExceptionInfo? exception = null,
            DiagnosticEventId eventId = default,
            DiagnosticSessionId sessionId = default,
            DiagnosticCorrelationId correlationId = default)
        {
            if (!code.IsValid)
                throw new ArgumentException("Diagnostic code must be provided.", nameof(code));
            if (!IsDefinedSeverity(severity))
                throw new ArgumentOutOfRangeException(nameof(severity), severity, "Diagnostic severity must be a defined non-default value.");
            if (!IsDefinedDomain(domain))
                throw new ArgumentOutOfRangeException(nameof(domain), domain, "Diagnostic domain must be a defined non-default value.");
            if (!IsDefinedFailureBoundary(failureBoundary))
                throw new ArgumentOutOfRangeException(nameof(failureBoundary), failureBoundary, "Diagnostic failure boundary must be a defined value.");

            if (context != null && correlationId.Value != 0 && context.CorrelationId != correlationId)
                throw new ArgumentException("Context correlation must match the diagnostic correlation.", nameof(context));

            DiagnosticCorrelationId resolvedCorrelationId = correlationId.Value != 0
                ? correlationId
                : context != null
                    ? context.CorrelationId
                    : default;

            Code = code;
            Severity = severity;
            Domain = domain;
            FailureBoundary = failureBoundary;
            Message = message;
            Context = context ?? new DiagnosticContext(correlationId: resolvedCorrelationId);
            Payload = payload ?? DiagnosticPayload.Empty;
            Exception = exception;
            EventId = eventId;
            SessionId = sessionId;
            CorrelationId = resolvedCorrelationId;
        }

        public DiagnosticEventId EventId { get; }
        public DiagnosticSessionId SessionId { get; }
        public DiagnosticCorrelationId CorrelationId { get; }
        public DiagnosticCode Code { get; }
        public DiagnosticSeverity Severity { get; }
        public DiagnosticDomain Domain { get; }
        public DiagnosticFailureBoundary FailureBoundary { get; }
        public string? Message { get; }
        public DiagnosticContext Context { get; }
        public DiagnosticPayload Payload { get; }
        public DiagnosticExceptionInfo? Exception { get; }

        static bool IsDefinedSeverity(DiagnosticSeverity severity)
        {
            return severity == DiagnosticSeverity.Trace
                || severity == DiagnosticSeverity.Info
                || severity == DiagnosticSeverity.Warning
                || severity == DiagnosticSeverity.Error
                || severity == DiagnosticSeverity.Fatal;
        }

        static bool IsDefinedDomain(DiagnosticDomain domain)
        {
            return domain == DiagnosticDomain.Kernel
                || domain == DiagnosticDomain.Boot
                || domain == DiagnosticDomain.Generation
                || domain == DiagnosticDomain.Validation
                || domain == DiagnosticDomain.ServiceGraph
                || domain == DiagnosticDomain.ScopeGraph
                || domain == DiagnosticDomain.Lifecycle
                || domain == DiagnosticDomain.Command
                || domain == DiagnosticDomain.Value
                || domain == DiagnosticDomain.RuntimeQuery
                || domain == DiagnosticDomain.Save
                || domain == DiagnosticDomain.UnityBridge
                || domain == DiagnosticDomain.Diagnostics
                || domain == DiagnosticDomain.LegacyCompat;
        }

        static bool IsDefinedFailureBoundary(DiagnosticFailureBoundary failureBoundary)
        {
            return failureBoundary == DiagnosticFailureBoundary.None
                || failureBoundary == DiagnosticFailureBoundary.Operation
                || failureBoundary == DiagnosticFailureBoundary.Command
                || failureBoundary == DiagnosticFailureBoundary.CommandFrame
                || failureBoundary == DiagnosticFailureBoundary.Scope
                || failureBoundary == DiagnosticFailureBoundary.Scene
                || failureBoundary == DiagnosticFailureBoundary.Kernel
                || failureBoundary == DiagnosticFailureBoundary.Build;
        }
    }
}
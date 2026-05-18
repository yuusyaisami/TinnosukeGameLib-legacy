#nullable enable
using System;

namespace Game.Kernel.Diagnostics
{
    public enum DiagnosticProfileKind
    {
        Development = 10,
        Release = 20,
        Test = 30,
    }

    public readonly struct DiagnosticSessionInfo
    {
        public DiagnosticSessionInfo(string kind, string name, DiagnosticCorrelationId correlationId = default)
        {
            if (string.IsNullOrWhiteSpace(kind))
                throw new ArgumentException("Session kind must not be blank.", nameof(kind));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Session name must not be blank.", nameof(name));

            Kind = kind;
            Name = name;
            CorrelationId = correlationId;
        }

        public string Kind { get; }
        public string Name { get; }
        public DiagnosticCorrelationId CorrelationId { get; }
    }

    public readonly struct DiagnosticSessionHandle : IEquatable<DiagnosticSessionHandle>
    {
        public DiagnosticSessionHandle(DiagnosticSessionId sessionId, string kind, string name, DiagnosticCorrelationId correlationId = default)
        {
            if (sessionId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(sessionId), sessionId, "Session ID must be positive.");
            if (string.IsNullOrWhiteSpace(kind))
                throw new ArgumentException("Session kind must not be blank.", nameof(kind));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Session name must not be blank.", nameof(name));

            SessionId = sessionId;
            Kind = kind;
            Name = name;
            CorrelationId = correlationId;
        }

        public DiagnosticSessionId SessionId { get; }
        public string Kind { get; }
        public string Name { get; }
        public DiagnosticCorrelationId CorrelationId { get; }

        public bool IsValid => SessionId.Value != 0;

        public bool Equals(DiagnosticSessionHandle other)
        {
            return SessionId.Equals(other.SessionId)
                && string.Equals(Kind, other.Kind, StringComparison.Ordinal)
                && string.Equals(Name, other.Name, StringComparison.Ordinal)
                && CorrelationId.Equals(other.CorrelationId);
        }

        public override bool Equals(object? obj)
        {
            return obj is DiagnosticSessionHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SessionId.GetHashCode();
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Kind);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Name);
                hash = (hash * 397) ^ CorrelationId.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(DiagnosticSessionHandle left, DiagnosticSessionHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DiagnosticSessionHandle left, DiagnosticSessionHandle right)
        {
            return !left.Equals(right);
        }
    }

    public interface IKernelDiagnosticService
    {
        void Report(in KernelDiagnostic diagnostic);
        void ReportBatch(ReadOnlySpan<KernelDiagnostic> diagnostics);
        DiagnosticSessionHandle BeginSession(DiagnosticSessionInfo info);
        void EndSession(DiagnosticSessionHandle handle);
    }
}
#nullable enable
using System;
using System.Text;
using UnityEngine;

namespace Game.Kernel.Diagnostics
{
    public enum UnityDiagnosticOutputKind
    {
        Suppressed = 0,
        Log = 10,
        Warning = 20,
        Error = 30,
    }

    public interface IUnityDiagnosticLogTarget
    {
        void Log(string message);
        void LogWarning(string message);
        void LogError(string message);
    }

    public sealed class UnityLogDiagnosticSink : IKernelDiagnosticSink
    {
        readonly IUnityDiagnosticLogTarget _target;
        readonly DiagnosticProfileKind _profileKind;
        readonly bool _enableTrace;

        sealed class UnityDebugDiagnosticLogTarget : IUnityDiagnosticLogTarget
        {
            public void Log(string message)
            {
                Debug.Log(message);
            }

            public void LogWarning(string message)
            {
                Debug.LogWarning(message);
            }

            public void LogError(string message)
            {
                Debug.LogError(message);
            }
        }

        public UnityLogDiagnosticSink(DiagnosticProfileKind profileKind, bool enableTrace = false)
            : this(new UnityDebugDiagnosticLogTarget(), profileKind, enableTrace)
        {
        }

        public UnityLogDiagnosticSink(IUnityDiagnosticLogTarget target, DiagnosticProfileKind profileKind, bool enableTrace = false)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
            _profileKind = profileKind;
            _enableTrace = enableTrace;
        }

        public void Emit(in KernelDiagnostic diagnostic)
        {
            UnityDiagnosticOutputKind outputKind = DetermineOutputKind(in diagnostic);
            if (outputKind == UnityDiagnosticOutputKind.Suppressed)
                return;

            string message = Render(in diagnostic);
            switch (outputKind)
            {
                case UnityDiagnosticOutputKind.Log:
                    _target.Log(message);
                    break;
                case UnityDiagnosticOutputKind.Warning:
                    _target.LogWarning(message);
                    break;
                case UnityDiagnosticOutputKind.Error:
                    _target.LogError(message);
                    break;
                default:
                    throw new InvalidOperationException("Unsupported Unity diagnostic output kind.");
            }
        }

        public void Flush()
        {
        }

        public UnityDiagnosticOutputKind DetermineOutputKind(in KernelDiagnostic diagnostic)
        {
            switch (diagnostic.Severity)
            {
                case DiagnosticSeverity.Trace:
                    return (_profileKind == DiagnosticProfileKind.Development || _profileKind == DiagnosticProfileKind.Test) && _enableTrace
                        ? UnityDiagnosticOutputKind.Log
                        : UnityDiagnosticOutputKind.Suppressed;
                case DiagnosticSeverity.Info:
                    return _profileKind == DiagnosticProfileKind.Release
                        ? UnityDiagnosticOutputKind.Suppressed
                        : UnityDiagnosticOutputKind.Log;
                case DiagnosticSeverity.Warning:
                    return UnityDiagnosticOutputKind.Warning;
                case DiagnosticSeverity.Error:
                case DiagnosticSeverity.Fatal:
                    return UnityDiagnosticOutputKind.Error;
                default:
                    throw new ArgumentOutOfRangeException(nameof(diagnostic), diagnostic.Severity, "Unsupported diagnostic severity.");
            }
        }

        public string Render(in KernelDiagnostic diagnostic)
        {
            var builder = new StringBuilder(256);
            builder.Append("[KernelDiagnostic]")
                .Append(' ')
                .Append("Profile=").Append(_profileKind)
                .Append(' ')
                .Append("Severity=").Append(diagnostic.Severity)
                .Append(' ')
                .Append("Code=").Append(diagnostic.Code)
                .Append(' ')
                .Append("Domain=").Append(diagnostic.Domain)
                .Append(' ')
                .Append("Boundary=").Append(diagnostic.FailureBoundary);

            if (!string.IsNullOrWhiteSpace(diagnostic.Message))
            {
                builder.AppendLine().Append("Message: ").Append(diagnostic.Message);
            }

            AppendContext(builder, diagnostic.Context, _profileKind);
            AppendPayload(builder, diagnostic.Payload);
            AppendException(builder, diagnostic.Exception, _profileKind);
            return builder.ToString();
        }

        static void AppendContext(StringBuilder builder, DiagnosticContext context, DiagnosticProfileKind profileKind)
        {
            if (!context.OwnerModule.IsEmpty)
                builder.AppendLine().Append("OwnerModule: ").Append(context.OwnerModule.Value);
            if (profileKind != DiagnosticProfileKind.Release && context.Source.Value != 0)
                builder.AppendLine().Append("Source: ").Append(context.Source.Value);
            if (profileKind != DiagnosticProfileKind.Release && context.Artifact.ArtifactSetId != 0)
            {
                builder.AppendLine().Append("ArtifactSet: ").Append(context.Artifact.ArtifactSetId);
                if (context.Artifact.GeneratedArtifactId != 0)
                    builder.Append(" GeneratedArtifact: ").Append(context.Artifact.GeneratedArtifactId);
            }
            if (profileKind != DiagnosticProfileKind.Release && context.ProfileId != 0)
                builder.AppendLine().Append("Profile: ").Append(context.ProfileId);
            if (context.CorrelationId.Value != 0)
                builder.AppendLine().Append("Correlation: ").Append(context.CorrelationId.Value);
            if (!string.IsNullOrWhiteSpace(context.Phase))
                builder.AppendLine().Append("Phase: ").Append(context.Phase);
            if (profileKind != DiagnosticProfileKind.Release && context.RuntimeIdentities.Count > 0)
            {
                builder.AppendLine().Append("RuntimeIdentities: ");
                for (int i = 0; i < context.RuntimeIdentities.Count; i++)
                {
                    if (i > 0)
                        builder.Append(", ");
                    builder.Append(context.RuntimeIdentities[i]);
                }
            }
        }

        static void AppendPayload(StringBuilder builder, DiagnosticPayload payload)
        {
            if (payload.Entries.Count == 0)
                return;

            builder.AppendLine().Append("Payload: ");
            for (int i = 0; i < payload.Entries.Count; i++)
            {
                if (i > 0)
                    builder.Append(", ");

                builder.Append(payload.Entries[i].Key)
                    .Append('=')
                    .Append(payload.Entries[i].Value);
            }
        }

        static void AppendException(StringBuilder builder, DiagnosticExceptionInfo? exception, DiagnosticProfileKind profileKind)
        {
            if (exception == null)
                return;

            builder.AppendLine().Append("ExceptionType: ").Append(exception.Type);
            if (!string.IsNullOrWhiteSpace(exception.Message))
                builder.AppendLine().Append("ExceptionMessage: ").Append(exception.Message);
            if (profileKind != DiagnosticProfileKind.Release && !string.IsNullOrWhiteSpace(exception.StackTrace))
                builder.AppendLine().Append("ExceptionStack: ").Append(exception.StackTrace);

            if (profileKind == DiagnosticProfileKind.Release)
                return;

            DiagnosticExceptionInfo? current = exception.Inner;
            int depth = 1;
            while (current != null)
            {
                builder.AppendLine().Append("InnerException[").Append(depth).Append("] Type: ").Append(current.Type);
                if (!string.IsNullOrWhiteSpace(current.Message))
                    builder.AppendLine().Append("InnerException[").Append(depth).Append("] Message: ").Append(current.Message);
                current = current.Inner;
                depth++;
            }
        }
    }
}
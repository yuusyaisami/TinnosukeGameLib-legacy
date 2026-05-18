#nullable enable
using System;
using System.Collections.Generic;

namespace Game.Kernel.Diagnostics
{
    public sealed class KernelDiagnosticService : IKernelDiagnosticService
    {
        static readonly DiagnosticCode SinkEmitFailedCode = new DiagnosticCode("DIAG_SINK_EMIT_FAILED");

        readonly IKernelDiagnosticSink[] _sinks;
        readonly Dictionary<long, DiagnosticSessionHandle> _activeSessions = new Dictionary<long, DiagnosticSessionHandle>();
        readonly List<long> _sessionStack = new List<long>(4);
        long _nextSessionId = 1;

        public KernelDiagnosticService(IReadOnlyList<IKernelDiagnosticSink> sinks)
        {
            if (sinks == null)
                throw new ArgumentNullException(nameof(sinks));

            _sinks = CopySinks(sinks);
        }

        public int ActiveSessionCount => _activeSessions.Count;

        public void Report(in KernelDiagnostic diagnostic)
        {
            KernelDiagnostic effectiveDiagnostic = ApplyCurrentSession(in diagnostic);
            List<SinkFailure>? failures = null;

            for (int i = 0; i < _sinks.Length; i++)
            {
                try
                {
                    _sinks[i].Emit(in effectiveDiagnostic);
                }
                catch (Exception exception)
                {
                    if (failures == null)
                        failures = new List<SinkFailure>(2);

                    failures.Add(new SinkFailure(i, _sinks[i], exception));
                }
            }

            if (failures != null && failures.Count > 0)
            {
                EmitSinkFailureDiagnostics(effectiveDiagnostic, failures);
            }
        }

        public void ReportBatch(ReadOnlySpan<KernelDiagnostic> diagnostics)
        {
            for (int i = 0; i < diagnostics.Length; i++)
            {
                Report(in diagnostics[i]);
            }
        }

        public DiagnosticSessionHandle BeginSession(DiagnosticSessionInfo info)
        {
            var sessionId = new DiagnosticSessionId(_nextSessionId++);
            var handle = new DiagnosticSessionHandle(sessionId, info.Kind, info.Name, info.CorrelationId);
            _activeSessions.Add(sessionId.Value, handle);
            _sessionStack.Add(sessionId.Value);
            return handle;
        }

        public void EndSession(DiagnosticSessionHandle handle)
        {
            if (!handle.IsValid)
                throw new ArgumentException("Session handle must be valid.", nameof(handle));

            if (!_activeSessions.TryGetValue(handle.SessionId.Value, out DiagnosticSessionHandle activeHandle))
                throw new InvalidOperationException("Session handle is not active.");

            if (activeHandle != handle)
                throw new InvalidOperationException("Session handle does not match the active session metadata.");

            int lastIndex = _sessionStack.Count - 1;
            if (lastIndex < 0 || _sessionStack[lastIndex] != handle.SessionId.Value)
                throw new InvalidOperationException("Sessions must be ended in LIFO order.");

            _sessionStack.RemoveAt(lastIndex);
            _activeSessions.Remove(handle.SessionId.Value);
        }

        public void FlushSinks()
        {
            for (int i = 0; i < _sinks.Length; i++)
            {
                _sinks[i].Flush();
            }
        }

        static IKernelDiagnosticSink[] CopySinks(IReadOnlyList<IKernelDiagnosticSink> sinks)
        {
            var snapshot = new IKernelDiagnosticSink[sinks.Count];
            for (int i = 0; i < sinks.Count; i++)
            {
                snapshot[i] = sinks[i] ?? throw new ArgumentException("Sink list must not contain null.", nameof(sinks));
            }

            return snapshot;
        }

        KernelDiagnostic ApplyCurrentSession(in KernelDiagnostic diagnostic)
        {
            if (diagnostic.SessionId.Value != 0 || _sessionStack.Count == 0)
                return diagnostic;

            long currentSessionId = _sessionStack[_sessionStack.Count - 1];
            DiagnosticSessionHandle currentSession = _activeSessions[currentSessionId];
            DiagnosticCorrelationId correlationId = diagnostic.CorrelationId.Value != 0
                ? diagnostic.CorrelationId
                : currentSession.CorrelationId;

            DiagnosticContext context = diagnostic.Context;
            if (context.CorrelationId.Value == 0 && correlationId.Value != 0)
            {
                context = new DiagnosticContext(
                    CopyRuntimeIdentities(context.RuntimeIdentities),
                    context.OwnerModule,
                    context.Source,
                    context.Artifact,
                    context.ProfileId,
                    correlationId,
                    context.Phase);
            }

            return new KernelDiagnostic(
                diagnostic.Code,
                diagnostic.Severity,
                diagnostic.Domain,
                diagnostic.FailureBoundary,
                diagnostic.Message,
                context,
                new DiagnosticPayload(diagnostic.Payload.Entries),
                diagnostic.Exception,
                diagnostic.EventId,
                currentSession.SessionId,
                correlationId);
        }

        void EmitSinkFailureDiagnostics(KernelDiagnostic sourceDiagnostic, List<SinkFailure> failures)
        {
            for (int failureIndex = 0; failureIndex < failures.Count; failureIndex++)
            {
                SinkFailure failure = failures[failureIndex];
                KernelDiagnostic degradationDiagnostic = CreateSinkFailureDiagnostic(sourceDiagnostic, failure);

                for (int sinkIndex = 0; sinkIndex < _sinks.Length; sinkIndex++)
                {
                    if (sinkIndex == failure.SinkIndex)
                        continue;

                    try
                    {
                        _sinks[sinkIndex].Emit(in degradationDiagnostic);
                    }
                    catch
                    {
                    }
                }
            }
        }

        KernelDiagnostic CreateSinkFailureDiagnostic(KernelDiagnostic sourceDiagnostic, SinkFailure failure)
        {
            string sinkName = failure.Sink.GetType().FullName ?? failure.Sink.GetType().Name;
            var payload = new DiagnosticPayload(new[]
            {
                new DiagnosticPayloadEntry("FailedSink", DiagnosticPayloadValue.FromString(sinkName)),
                new DiagnosticPayloadEntry("OriginalCode", DiagnosticPayloadValue.FromString(sourceDiagnostic.Code.Value)),
                new DiagnosticPayloadEntry("ExceptionType", DiagnosticPayloadValue.FromString(failure.Exception.GetType().FullName ?? failure.Exception.GetType().Name)),
            });

            return new KernelDiagnostic(
                SinkEmitFailedCode,
                DiagnosticSeverity.Error,
                DiagnosticDomain.Diagnostics,
                DiagnosticFailureBoundary.Operation,
                message: "Diagnostic sink emission failed.",
                context: sourceDiagnostic.Context,
                payload: payload,
                exception: DiagnosticExceptionInfo.FromException(failure.Exception),
                sessionId: sourceDiagnostic.SessionId,
                correlationId: sourceDiagnostic.CorrelationId);
        }

        static RuntimeIdentityRef[] CopyRuntimeIdentities(IReadOnlyList<RuntimeIdentityRef> runtimeIdentities)
        {
            if (runtimeIdentities.Count == 0)
                return Array.Empty<RuntimeIdentityRef>();

            var snapshot = new RuntimeIdentityRef[runtimeIdentities.Count];
            for (int i = 0; i < runtimeIdentities.Count; i++)
            {
                snapshot[i] = runtimeIdentities[i];
            }

            return snapshot;
        }

        readonly struct SinkFailure
        {
            public SinkFailure(int sinkIndex, IKernelDiagnosticSink sink, Exception exception)
            {
                SinkIndex = sinkIndex;
                Sink = sink;
                Exception = exception;
            }

            public int SinkIndex { get; }
            public IKernelDiagnosticSink Sink { get; }
            public Exception Exception { get; }
        }
    }
}

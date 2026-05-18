using System;
using System.Collections.Generic;
using Game.Kernel.Diagnostics;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class KernelDiagnosticServiceTests
    {
        [Test]
        public void Report_FansOutToConfiguredSinksInOrder()
        {
            List<string> dispatchOrder = new List<string>();
            RecordingSink first = new RecordingSink("first", dispatchOrder);
            RecordingSink second = new RecordingSink("second", dispatchOrder);
            KernelDiagnosticService service = new KernelDiagnosticService(new IKernelDiagnosticSink[] { first, second });

            service.Report(CreateDiagnostic("DIAG_FANOUT"));

            Assert.That(dispatchOrder, Is.EqualTo(new[] { "first:DIAG_FANOUT", "second:DIAG_FANOUT" }));
        }

        [Test]
        public void ReportBatch_PreservesOrderingInCaptureSinks()
        {
            InMemoryDiagnosticSink inMemory = new InMemoryDiagnosticSink();
            TestDiagnosticSink testSink = new TestDiagnosticSink();
            KernelDiagnosticService service = new KernelDiagnosticService(new IKernelDiagnosticSink[] { inMemory, testSink });
            KernelDiagnostic[] batch =
            {
                CreateDiagnostic("DIAG_A"),
                CreateDiagnostic("DIAG_B"),
                CreateDiagnostic("DIAG_C"),
            };

            service.ReportBatch(batch);

            Assert.That(inMemory.Diagnostics, Has.Count.EqualTo(3));
            Assert.That(inMemory.Diagnostics[0].Code.Value, Is.EqualTo("DIAG_A"));
            Assert.That(inMemory.Diagnostics[1].Code.Value, Is.EqualTo("DIAG_B"));
            Assert.That(inMemory.Diagnostics[2].Code.Value, Is.EqualTo("DIAG_C"));
            Assert.That(testSink.Diagnostics, Has.Count.EqualTo(3));
            Assert.That(testSink.Diagnostics[0].Code.Value, Is.EqualTo("DIAG_A"));
            Assert.That(testSink.Diagnostics[1].Code.Value, Is.EqualTo("DIAG_B"));
            Assert.That(testSink.Diagnostics[2].Code.Value, Is.EqualTo("DIAG_C"));
        }

        [Test]
        public void BeginSession_AllocatesDeterministicHandles_AndEndSessionRemovesThem()
        {
            KernelDiagnosticService service = new KernelDiagnosticService(Array.Empty<IKernelDiagnosticSink>());

            DiagnosticSessionHandle first = service.BeginSession(new DiagnosticSessionInfo("boot", "initial boot"));
            DiagnosticSessionHandle second = service.BeginSession(new DiagnosticSessionInfo("validation", "module validation", new DiagnosticCorrelationId(5)));

            Assert.That(first.SessionId.Value, Is.EqualTo(1));
            Assert.That(second.SessionId.Value, Is.EqualTo(2));
            Assert.That(second.CorrelationId.Value, Is.EqualTo(5));
            Assert.That(service.ActiveSessionCount, Is.EqualTo(2));

            service.EndSession(first);

            Assert.That(service.ActiveSessionCount, Is.EqualTo(1));
            Assert.That(() => service.EndSession(first), Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void Report_AssignsCurrentSessionMetadata_WhenDiagnosticDoesNotProvideIt()
        {
            InMemoryDiagnosticSink sink = new InMemoryDiagnosticSink();
            KernelDiagnosticService service = new KernelDiagnosticService(new IKernelDiagnosticSink[] { sink });
            DiagnosticSessionHandle session = service.BeginSession(new DiagnosticSessionInfo("validation", "module validation", new DiagnosticCorrelationId(55)));

            service.Report(CreateDiagnostic("DIAG_SESSION_BIND"));

            Assert.That(sink.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(sink.Diagnostics[0].SessionId, Is.EqualTo(session.SessionId));
            Assert.That(sink.Diagnostics[0].CorrelationId.Value, Is.EqualTo(55));
            Assert.That(sink.Diagnostics[0].Context.CorrelationId.Value, Is.EqualTo(55));
        }

        [Test]
        public void EndSession_RejectsForgedHandleMetadata()
        {
            KernelDiagnosticService service = new KernelDiagnosticService(Array.Empty<IKernelDiagnosticSink>());
            DiagnosticSessionHandle session = service.BeginSession(new DiagnosticSessionInfo("boot", "initial boot", new DiagnosticCorrelationId(3)));
            DiagnosticSessionHandle forged = new DiagnosticSessionHandle(session.SessionId, "boot", "forged", new DiagnosticCorrelationId(3));

            Assert.That(() => service.EndSession(forged), Throws.TypeOf<InvalidOperationException>());
            Assert.That(service.ActiveSessionCount, Is.EqualTo(1));
        }

        [Test]
        public void Report_ContinuesToHealthySinks_WhenOneSinkThrows_AndEmitsDegradationDiagnostic()
        {
            ThrowingSink throwingSink = new ThrowingSink();
            InMemoryDiagnosticSink healthySink = new InMemoryDiagnosticSink();
            KernelDiagnosticService service = new KernelDiagnosticService(new IKernelDiagnosticSink[] { throwingSink, healthySink });

            service.Report(CreateDiagnostic("DIAG_PRIMARY"));

            Assert.That(healthySink.Diagnostics, Has.Count.EqualTo(2));
            Assert.That(healthySink.Diagnostics[0].Code.Value, Is.EqualTo("DIAG_PRIMARY"));
            Assert.That(healthySink.Diagnostics[1].Code.Value, Is.EqualTo("DIAG_SINK_EMIT_FAILED"));
        }

        [Test]
        public void TestDiagnosticSink_ResetClearsCapturedDiagnostics()
        {
            TestDiagnosticSink sink = new TestDiagnosticSink();

            sink.Emit(CreateDiagnostic("DIAG_RESET"));
            sink.Flush();
            sink.Reset();

            Assert.That(sink.Diagnostics, Is.Empty);
            Assert.That(sink.FlushCount, Is.EqualTo(0));
        }

        [Test]
        public void UnityLogDiagnosticSink_MapsSeverityThroughSingleOutputPolicy()
        {
            FakeUnityDiagnosticLogTarget target = new FakeUnityDiagnosticLogTarget();
            UnityLogDiagnosticSink sink = new UnityLogDiagnosticSink(target, DiagnosticProfileKind.Development);

            sink.Emit(CreateDiagnostic("DIAG_INFO", DiagnosticSeverity.Info));
            sink.Emit(CreateDiagnostic("DIAG_WARNING", DiagnosticSeverity.Warning));
            sink.Emit(CreateDiagnostic("DIAG_ERROR", DiagnosticSeverity.Error));
            sink.Emit(CreateDiagnostic("DIAG_FATAL", DiagnosticSeverity.Fatal));

            Assert.That(target.Events, Has.Count.EqualTo(4));
            Assert.That(target.Events[0].Kind, Is.EqualTo(UnityDiagnosticOutputKind.Log));
            Assert.That(target.Events[1].Kind, Is.EqualTo(UnityDiagnosticOutputKind.Warning));
            Assert.That(target.Events[2].Kind, Is.EqualTo(UnityDiagnosticOutputKind.Error));
            Assert.That(target.Events[3].Kind, Is.EqualTo(UnityDiagnosticOutputKind.Error));
            Assert.That(target.Events[2].Message, Does.Contain("Code=DIAG_ERROR"));
            Assert.That(target.Events[2].Message, Does.Contain("Message: message-DIAG_ERROR"));
        }

        [Test]
        public void UnityLogDiagnosticSink_SuppressesTraceByDefault_AndCanEnableIt()
        {
            FakeUnityDiagnosticLogTarget suppressedTarget = new FakeUnityDiagnosticLogTarget();
            UnityLogDiagnosticSink suppressedSink = new UnityLogDiagnosticSink(suppressedTarget, DiagnosticProfileKind.Development);

            suppressedSink.Emit(CreateDiagnostic("DIAG_TRACE", DiagnosticSeverity.Trace));

            Assert.That(suppressedTarget.Events, Is.Empty);

            FakeUnityDiagnosticLogTarget enabledTarget = new FakeUnityDiagnosticLogTarget();
            UnityLogDiagnosticSink enabledSink = new UnityLogDiagnosticSink(enabledTarget, DiagnosticProfileKind.Development, enableTrace: true);

            enabledSink.Emit(CreateDiagnostic("DIAG_TRACE", DiagnosticSeverity.Trace));

            Assert.That(enabledTarget.Events, Has.Count.EqualTo(1));
            Assert.That(enabledTarget.Events[0].Kind, Is.EqualTo(UnityDiagnosticOutputKind.Log));
        }

        [Test]
        public void UnityLogDiagnosticSink_UsesReleasePolicy_ToSuppressInfoAndReduceExceptionDetail()
        {
            FakeUnityDiagnosticLogTarget target = new FakeUnityDiagnosticLogTarget();
            UnityLogDiagnosticSink sink = new UnityLogDiagnosticSink(target, DiagnosticProfileKind.Release);

            sink.Emit(CreateDiagnostic("DIAG_INFO", DiagnosticSeverity.Info));
            sink.Emit(CreateDiagnostic("DIAG_ERROR", DiagnosticSeverity.Error));

            Assert.That(target.Events, Has.Count.EqualTo(1));
            Assert.That(target.Events[0].Kind, Is.EqualTo(UnityDiagnosticOutputKind.Error));
            Assert.That(target.Events[0].Message, Does.Not.Contain("ExceptionStack:"));
            Assert.That(target.Events[0].Message, Does.Not.Contain("Source: "));
        }

        [Test]
        public void InMemoryDiagnosticSink_BoundsItsBuffer()
        {
            InMemoryDiagnosticSink sink = new InMemoryDiagnosticSink(capacity: 2);

            sink.Emit(CreateDiagnostic("DIAG_A"));
            sink.Emit(CreateDiagnostic("DIAG_B"));
            sink.Emit(CreateDiagnostic("DIAG_C"));

            Assert.That(sink.Diagnostics, Has.Count.EqualTo(2));
            Assert.That(sink.Diagnostics[0].Code.Value, Is.EqualTo("DIAG_B"));
            Assert.That(sink.Diagnostics[1].Code.Value, Is.EqualTo("DIAG_C"));
            Assert.That(sink.WasTruncated, Is.True);
        }

        [Test]
        public void RegistrationOptions_EnableTestSinkForTestProfile_WithoutForcingInMemorySink()
        {
            KernelDiagnosticsRegistrationOptions options = new KernelDiagnosticsRegistrationOptions(
                DiagnosticProfileKind.Test,
                enableUnityLogSink: false,
                enableInMemorySink: false);

            Assert.That(options.ProfileKind, Is.EqualTo(DiagnosticProfileKind.Test));
            Assert.That(options.EnableInMemorySink, Is.False);
        }

        static KernelDiagnostic CreateDiagnostic(string code, DiagnosticSeverity severity = DiagnosticSeverity.Error)
        {
            return new KernelDiagnostic(
                new DiagnosticCode(code),
                severity,
                DiagnosticDomain.Diagnostics,
                DiagnosticFailureBoundary.Operation,
                message: "message-" + code,
                context: new DiagnosticContext(
                    runtimeIdentities: new[] { new RuntimeIdentityRef(RuntimeIdentityKind.Service, 10) },
                    ownerModule: new ModuleIdentityRef(2),
                    source: new SourceLocationRef(3),
                    artifact: new ArtifactIdentityRef(4, 5),
                    profileId: 6,
                    correlationId: new DiagnosticCorrelationId(7),
                    phase: "phase"),
                payload: new DiagnosticPayload(new[]
                {
                    new DiagnosticPayloadEntry("Attempt", DiagnosticPayloadValue.FromInt32(1)),
                }),
                exception: new DiagnosticExceptionInfo("System.InvalidOperationException", "boom", "stack"));
        }

        sealed class RecordingSink : IKernelDiagnosticSink
        {
            readonly string _name;
            readonly List<string> _dispatchOrder;

            public RecordingSink(string name, List<string> dispatchOrder)
            {
                _name = name;
                _dispatchOrder = dispatchOrder;
            }

            public void Emit(in KernelDiagnostic diagnostic)
            {
                _dispatchOrder.Add(_name + ":" + diagnostic.Code.Value);
            }

            public void Flush()
            {
            }
        }

        sealed class ThrowingSink : IKernelDiagnosticSink
        {
            public void Emit(in KernelDiagnostic diagnostic)
            {
                throw new InvalidOperationException("sink failure");
            }

            public void Flush()
            {
            }
        }

        sealed class FakeUnityDiagnosticLogTarget : IUnityDiagnosticLogTarget
        {
            public List<LogEvent> Events { get; } = new List<LogEvent>();

            public void Log(string message)
            {
                Events.Add(new LogEvent(UnityDiagnosticOutputKind.Log, message));
            }

            public void LogWarning(string message)
            {
                Events.Add(new LogEvent(UnityDiagnosticOutputKind.Warning, message));
            }

            public void LogError(string message)
            {
                Events.Add(new LogEvent(UnityDiagnosticOutputKind.Error, message));
            }
        }

        readonly struct LogEvent
        {
            public LogEvent(UnityDiagnosticOutputKind kind, string message)
            {
                Kind = kind;
                Message = message;
            }

            public UnityDiagnosticOutputKind Kind { get; }
            public string Message { get; }
        }
    }
}
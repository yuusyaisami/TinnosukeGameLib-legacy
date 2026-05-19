using System;
using System.Collections.Generic;
using Game.Kernel.Diagnostics;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class KernelDiagnosticsModelTests
    {
        [Test]
        public void DiagnosticEnums_UseStableExplicitValues()
        {
            Assert.That((int)DiagnosticSeverity.Trace, Is.EqualTo(10));
            Assert.That((int)DiagnosticSeverity.Info, Is.EqualTo(20));
            Assert.That((int)DiagnosticSeverity.Warning, Is.EqualTo(30));
            Assert.That((int)DiagnosticSeverity.Error, Is.EqualTo(40));
            Assert.That((int)DiagnosticSeverity.Fatal, Is.EqualTo(50));

            Assert.That((int)DiagnosticFailureBoundary.None, Is.EqualTo(0));
            Assert.That((int)DiagnosticFailureBoundary.Operation, Is.EqualTo(10));
            Assert.That((int)DiagnosticFailureBoundary.Command, Is.EqualTo(20));
            Assert.That((int)DiagnosticFailureBoundary.CommandFrame, Is.EqualTo(30));
            Assert.That((int)DiagnosticFailureBoundary.Scope, Is.EqualTo(40));
            Assert.That((int)DiagnosticFailureBoundary.Scene, Is.EqualTo(50));
            Assert.That((int)DiagnosticFailureBoundary.Kernel, Is.EqualTo(60));
            Assert.That((int)DiagnosticFailureBoundary.Build, Is.EqualTo(70));

            Assert.That((int)DiagnosticDomain.Kernel, Is.EqualTo(10));
            Assert.That((int)DiagnosticDomain.Diagnostics, Is.EqualTo(130));
            Assert.That((int)DiagnosticDomain.LegacyCompat, Is.EqualTo(900));

            Assert.That((int)RuntimeIdentityKind.ScopeHandle, Is.EqualTo(50));
            Assert.That((int)RuntimeIdentityKind.GeneratedArtifact, Is.EqualTo(150));
        }

        [Test]
        public void DiagnosticCode_RequiresStableSymbolicValue()
        {
            DiagnosticCode code = new DiagnosticCode("COMMAND_EXECUTOR_MISSING");

            Assert.That(code.IsValid, Is.True);
            Assert.That(code.Value, Is.EqualTo("COMMAND_EXECUTOR_MISSING"));
            Assert.That(code.ToString(), Is.EqualTo("COMMAND_EXECUTOR_MISSING"));
            Assert.That(() => new DiagnosticCode(" "), Throws.ArgumentException);
        }

        [Test]
        public void DiagnosticContext_CapturesStructuredProvenance()
        {
            RuntimeIdentityRef[] runtimeIdentities =
            {
                new RuntimeIdentityRef(RuntimeIdentityKind.ScopeHandle, 10, 2),
                new RuntimeIdentityRef(RuntimeIdentityKind.CommandType, 250),
            };

            DiagnosticContext context = new DiagnosticContext(
                runtimeIdentities,
                ownerModule: new ModuleIdentityRef(41),
                source: new SourceLocationRef(44),
                artifact: new ArtifactIdentityRef(8, 3),
                profileId: 7,
                correlationId: new DiagnosticCorrelationId(101),
                phase: "Dispatch");

            runtimeIdentities[0] = new RuntimeIdentityRef(RuntimeIdentityKind.Service, 999);

            Assert.That(context.OwnerModule.Value, Is.EqualTo(41));
            Assert.That(context.Source.Value, Is.EqualTo(44));
            Assert.That(context.Artifact.ArtifactSetId, Is.EqualTo(8));
            Assert.That(context.Artifact.GeneratedArtifactId, Is.EqualTo(3));
            Assert.That(context.ProfileId, Is.EqualTo(7));
            Assert.That(context.CorrelationId.Value, Is.EqualTo(101));
            Assert.That(context.Phase, Is.EqualTo("Dispatch"));
            Assert.That(context.RuntimeIdentities, Has.Count.EqualTo(2));
            Assert.That(context.RuntimeIdentities[0].Kind, Is.EqualTo(RuntimeIdentityKind.ScopeHandle));
            Assert.That(context.RuntimeIdentities[0].Generation, Is.EqualTo(2));
        }

        [Test]
        public void KernelDiagnostic_PreservesStructuredFields()
        {
            DiagnosticContext context = new DiagnosticContext(
                runtimeIdentities: new[] { new RuntimeIdentityRef(RuntimeIdentityKind.Service, 100) },
                ownerModule: new ModuleIdentityRef(7),
                source: new SourceLocationRef(12),
                artifact: new ArtifactIdentityRef(4),
                profileId: 2,
                correlationId: new DiagnosticCorrelationId(55),
                phase: "Resolve");

            List<DiagnosticPayloadEntry> entries = new List<DiagnosticPayloadEntry>
            {
                new DiagnosticPayloadEntry("Expected", DiagnosticPayloadValue.FromString("CommandExecutor")),
                new DiagnosticPayloadEntry("Attempt", DiagnosticPayloadValue.FromInt32(2)),
            };

            DiagnosticPayload payload = new DiagnosticPayload(entries);
            entries[0] = new DiagnosticPayloadEntry("Expected", DiagnosticPayloadValue.FromString("Mutated"));

            DiagnosticExceptionInfo exception = new DiagnosticExceptionInfo(
                type: typeof(InvalidOperationException).FullName!,
                message: "Executor missing",
                stackTrace: "stack",
                inner: new DiagnosticExceptionInfo("System.Exception", "root cause", "inner-stack"));

            KernelDiagnostic diagnostic = new KernelDiagnostic(
                code: new DiagnosticCode("COMMAND_EXECUTOR_MISSING"),
                severity: DiagnosticSeverity.Error,
                domain: DiagnosticDomain.Command,
                failureBoundary: DiagnosticFailureBoundary.Command,
                message: "Executor was not found.",
                context: context,
                payload: payload,
                exception: exception,
                eventId: new DiagnosticEventId(12),
                sessionId: new DiagnosticSessionId(99));

            Assert.That(diagnostic.Code.Value, Is.EqualTo("COMMAND_EXECUTOR_MISSING"));
            Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Error));
            Assert.That(diagnostic.Domain, Is.EqualTo(DiagnosticDomain.Command));
            Assert.That(diagnostic.FailureBoundary, Is.EqualTo(DiagnosticFailureBoundary.Command));
            Assert.That(diagnostic.Message, Is.EqualTo("Executor was not found."));
            Assert.That(diagnostic.Context, Is.SameAs(context));
            Assert.That(diagnostic.Payload.Entries, Has.Count.EqualTo(2));
            Assert.That(diagnostic.Payload.Entries[0].Key, Is.EqualTo("Expected"));
            Assert.That(diagnostic.Payload.Entries[0].Value.Kind, Is.EqualTo(DiagnosticPayloadValueKind.String));
            Assert.That(diagnostic.Payload.Entries[0].Value.ToString(), Is.EqualTo("CommandExecutor"));
            Assert.That(diagnostic.Payload.Entries[1].Value.Kind, Is.EqualTo(DiagnosticPayloadValueKind.Int32));
            Assert.That(diagnostic.Payload.Entries[1].Value.RawValue, Is.EqualTo(2));
            Assert.That(diagnostic.Exception, Is.SameAs(exception));
            Assert.That(diagnostic.Exception!.Inner, Is.Not.Null);
            Assert.That(diagnostic.Exception.Inner!.Message, Is.EqualTo("root cause"));
            Assert.That(diagnostic.EventId.Value, Is.EqualTo(12));
            Assert.That(diagnostic.SessionId.Value, Is.EqualTo(99));
            Assert.That(diagnostic.CorrelationId.Value, Is.EqualTo(55));
        }

        [Test]
        public void KernelDiagnostic_RejectsCorrelationMismatch()
        {
            DiagnosticContext context = new DiagnosticContext(
                correlationId: new DiagnosticCorrelationId(1));

            Assert.That(
                () => new KernelDiagnostic(
                    code: new DiagnosticCode("DIAG_CONTEXT_MISMATCH"),
                    severity: DiagnosticSeverity.Error,
                    domain: DiagnosticDomain.Diagnostics,
                    failureBoundary: DiagnosticFailureBoundary.Operation,
                    context: context,
                    correlationId: new DiagnosticCorrelationId(2)),
                Throws.ArgumentException);
        }

        [Test]
        public void KernelDiagnostic_RejectsUndefinedEnums()
        {
            Assert.That(
                () => new KernelDiagnostic(
                    code: new DiagnosticCode("DIAG_INVALID_SEVERITY"),
                    severity: default,
                    domain: DiagnosticDomain.Diagnostics,
                    failureBoundary: DiagnosticFailureBoundary.Operation),
                Throws.TypeOf<ArgumentOutOfRangeException>());

            Assert.That(
                () => new KernelDiagnostic(
                    code: new DiagnosticCode("DIAG_INVALID_DOMAIN"),
                    severity: DiagnosticSeverity.Error,
                    domain: default,
                    failureBoundary: DiagnosticFailureBoundary.Operation),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void DiagnosticExceptionInfo_FromException_PreservesInnerChain()
        {
            InvalidOperationException exception = new InvalidOperationException(
                "outer",
                new ArgumentException("inner"));

            DiagnosticExceptionInfo info = DiagnosticExceptionInfo.FromException(exception);

            Assert.That(info.Type, Is.EqualTo(typeof(InvalidOperationException).FullName));
            Assert.That(info.Message, Is.EqualTo("outer"));
            Assert.That(info.Inner, Is.Not.Null);
            Assert.That(info.Inner!.Type, Is.EqualTo(typeof(ArgumentException).FullName));
            Assert.That(info.Inner.Message, Is.EqualTo("inner"));
        }
    }
}

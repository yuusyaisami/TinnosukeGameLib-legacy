#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Kernel.Abstractions;
using Game.Kernel.Diagnostics;
using Game.Kernel.Validation;

namespace Game.Kernel.Boot
{
    public enum KernelBootBoundaryStatus
    {
        Ready = 10,
        Failed = 20,
    }

    public enum KernelBootBoundaryFailureKind
    {
        ValidationBlocked = 10,
        RuntimeSurfaceMissing = 20,
        RuntimeConstructionFailed = 30,
    }

    public static class KernelBootBoundaryCodes
    {
        public const string RuntimeSurfaceMissing = "BOOT_RUNTIME_SURFACE_MISSING";
        public const string RuntimeConstructionFailed = "BOOT_RUNTIME_CONSTRUCTION_FAILED";
    }

    public interface IKernelBootRuntimeSurface
    {
    }

    public interface IKernelBootRuntimeSurfaceFactory
    {
        IKernelBootRuntimeSurface Create(KernelBootBoundaryContext context);
    }

    public sealed class KernelBootBoundaryContext
    {
        public KernelBootBoundaryContext(BootValidationInput input, BootValidationReport validationReport)
        {
            Input = input ?? throw new ArgumentNullException(nameof(input));
            ValidationReport = validationReport ?? throw new ArgumentNullException(nameof(validationReport));

            if (ValidationReport.HasBlockingIssues)
                throw new ArgumentException("Kernel boot boundary context can only be created from a passing validation report.", nameof(validationReport));

            Manifest = ValidationReport.Manifest ?? throw new ArgumentException("Kernel boot boundary context requires a validated boot manifest.", nameof(validationReport));
            SelectedProfile = ValidationReport.SelectedProfile ?? throw new ArgumentException("Kernel boot boundary context requires a validated selected profile.", nameof(validationReport));
        }

        public BootValidationInput Input { get; }

        public BootValidationReport ValidationReport { get; }

        public KernelBootManifest Manifest { get; }

        public KernelProfile SelectedProfile { get; }
    }

    public abstract class KernelBootBoundaryResult
    {
        readonly ReadOnlyCollection<KernelDiagnostic> diagnostics;

        protected KernelBootBoundaryResult(
            KernelBootBoundaryStatus status,
            BootValidationReport validationReport,
            IReadOnlyList<KernelDiagnostic>? diagnostics)
        {
            if (validationReport == null)
                throw new ArgumentNullException(nameof(validationReport));

            Status = status;
            ValidationReport = validationReport;

            KernelDiagnostic[] snapshot = diagnostics == null || diagnostics.Count == 0
                ? Array.Empty<KernelDiagnostic>()
                : CloneDiagnostics(diagnostics);

            this.diagnostics = Array.AsReadOnly(snapshot);
        }

        public KernelBootBoundaryStatus Status { get; }

        public BootValidationReport ValidationReport { get; }

        public IReadOnlyList<KernelDiagnostic> Diagnostics => diagnostics;

        public bool IsReady => Status == KernelBootBoundaryStatus.Ready;

        public sealed class Success : KernelBootBoundaryResult
        {
            public Success(
                KernelBootBoundaryContext context,
                IKernelBootRuntimeSurface runtimeSurface,
                BootValidationReport validationReport,
                IReadOnlyList<KernelDiagnostic>? diagnostics = null)
                : base(KernelBootBoundaryStatus.Ready, validationReport, diagnostics)
            {
                Context = context ?? throw new ArgumentNullException(nameof(context));
                RuntimeSurface = runtimeSurface ?? throw new ArgumentNullException(nameof(runtimeSurface));
            }

            public KernelBootBoundaryContext Context { get; }

            public IKernelBootRuntimeSurface RuntimeSurface { get; }
        }

        public sealed class Failure : KernelBootBoundaryResult
        {
            public Failure(
                KernelBootBoundaryFailureKind failureKind,
                BootValidationReport validationReport,
                IReadOnlyList<KernelDiagnostic>? diagnostics = null,
                KernelBootBoundaryContext? context = null)
                : base(KernelBootBoundaryStatus.Failed, validationReport, diagnostics)
            {
                if (failureKind == default)
                    throw new ArgumentOutOfRangeException(nameof(failureKind), failureKind, "Failed boot boundary results must provide a defined failure kind.");

                FailureKind = failureKind;
                Context = context;
            }

            public KernelBootBoundaryFailureKind FailureKind { get; }

            public KernelBootBoundaryContext? Context { get; }
        }

        public static KernelBootBoundaryResult Ready(
            KernelBootBoundaryContext context,
            IKernelBootRuntimeSurface runtimeSurface,
            IReadOnlyList<KernelDiagnostic>? diagnostics = null)
        {
            return new Success(
                context,
                runtimeSurface,
                context.ValidationReport,
                diagnostics);
        }

        public static KernelBootBoundaryResult Failed(
            BootValidationReport validationReport,
            KernelBootBoundaryFailureKind failureKind,
            IReadOnlyList<KernelDiagnostic>? diagnostics = null,
            KernelBootBoundaryContext? context = null)
        {
            return new Failure(
                failureKind,
                validationReport,
                diagnostics,
                context);
        }

        static KernelDiagnostic[] CloneDiagnostics(IReadOnlyList<KernelDiagnostic> source)
        {
            KernelDiagnostic[] clone = new KernelDiagnostic[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                clone[index] = source[index] ?? throw new ArgumentException("Kernel boot boundary diagnostics must not contain null items.", nameof(source));
            }

            return clone;
        }
    }

    public static class KernelBootBoundary
    {
        public static KernelBootBoundaryResult Execute(BootValidationInput input)
        {
            return Execute(input, new KernelBootRuntimeSurfaceFactory());
        }

        public static KernelBootBoundaryResult Execute(
            BootValidationInput input,
            IKernelBootRuntimeSurfaceFactory runtimeFactory)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            if (runtimeFactory == null)
                throw new ArgumentNullException(nameof(runtimeFactory));

            BootValidationReport validationReport = BootValidator.Validate(input);
            List<KernelDiagnostic> diagnostics = BuildValidationDiagnostics(validationReport);

            if (validationReport.HasBlockingIssues)
                return KernelBootBoundaryResult.Failed(validationReport, KernelBootBoundaryFailureKind.ValidationBlocked, diagnostics);

            KernelBootBoundaryContext context = new KernelBootBoundaryContext(input, validationReport);

            try
            {
                IKernelBootRuntimeSurface runtimeSurface = runtimeFactory.Create(context);
                if (runtimeSurface == null)
                {
                    diagnostics.Add(CreateRuntimeSurfaceMissingDiagnostic(context));
                    return KernelBootBoundaryResult.Failed(validationReport, KernelBootBoundaryFailureKind.RuntimeSurfaceMissing, diagnostics, context);
                }

                return KernelBootBoundaryResult.Ready(context, runtimeSurface, diagnostics);
            }
            catch (Exception exception)
            {
                diagnostics.Add(CreateRuntimeConstructionFailedDiagnostic(context, exception));
                return KernelBootBoundaryResult.Failed(validationReport, KernelBootBoundaryFailureKind.RuntimeConstructionFailed, diagnostics, context);
            }
        }

        static List<KernelDiagnostic> BuildValidationDiagnostics(BootValidationReport validationReport)
        {
            List<KernelDiagnostic> diagnostics = new List<KernelDiagnostic>(validationReport.Issues.Count);
            for (int index = 0; index < validationReport.Issues.Count; index++)
                diagnostics.Add(validationReport.Issues[index].ToKernelDiagnostic(validationReport.Manifest, validationReport.SelectedProfile));

            return diagnostics;
        }

        static KernelDiagnostic CreateRuntimeSurfaceMissingDiagnostic(KernelBootBoundaryContext context)
        {
            DiagnosticContext diagnosticContext = new DiagnosticContext(
                artifact: new ArtifactIdentityRef(context.Manifest.ArtifactSet.ArtifactSetId.Value),
                profileId: context.SelectedProfile.Id.Value,
                phase: "Boot");

            DiagnosticPayload payload = new DiagnosticPayload(new[]
            {
                new DiagnosticPayloadEntry("ManifestId", DiagnosticPayloadValue.FromInt32(context.Manifest.ManifestId.Value)),
                new DiagnosticPayloadEntry("ArtifactSetId", DiagnosticPayloadValue.FromInt32(context.Manifest.ArtifactSet.ArtifactSetId.Value)),
                new DiagnosticPayloadEntry("BootPolicyId", DiagnosticPayloadValue.FromInt32(context.Manifest.BootPolicyId.Value)),
                new DiagnosticPayloadEntry("SelectedProfileId", DiagnosticPayloadValue.FromInt32(context.SelectedProfile.Id.Value)),
                new DiagnosticPayloadEntry("BootStage", DiagnosticPayloadValue.FromString("RuntimeConstruction")),
                new DiagnosticPayloadEntry("FailureKind", DiagnosticPayloadValue.FromString(KernelBootBoundaryFailureKind.RuntimeSurfaceMissing.ToString())),
                new DiagnosticPayloadEntry("SuggestedFix", DiagnosticPayloadValue.FromString("Return a non-null runtime surface from the boot factory after validation passes.")),
            });

            return new KernelDiagnostic(
                new DiagnosticCode(KernelBootBoundaryCodes.RuntimeSurfaceMissing),
                DiagnosticSeverity.Fatal,
                DiagnosticDomain.Boot,
                DiagnosticFailureBoundary.Kernel,
                "Boot runtime factory returned no runtime surface after validation succeeded.",
                diagnosticContext,
                payload);
        }

        static KernelDiagnostic CreateRuntimeConstructionFailedDiagnostic(KernelBootBoundaryContext context, Exception exception)
        {
            DiagnosticContext diagnosticContext = new DiagnosticContext(
                artifact: new ArtifactIdentityRef(context.Manifest.ArtifactSet.ArtifactSetId.Value),
                profileId: context.SelectedProfile.Id.Value,
                phase: "Boot");

            DiagnosticPayload payload = new DiagnosticPayload(new[]
            {
                new DiagnosticPayloadEntry("ManifestId", DiagnosticPayloadValue.FromInt32(context.Manifest.ManifestId.Value)),
                new DiagnosticPayloadEntry("ArtifactSetId", DiagnosticPayloadValue.FromInt32(context.Manifest.ArtifactSet.ArtifactSetId.Value)),
                new DiagnosticPayloadEntry("BootPolicyId", DiagnosticPayloadValue.FromInt32(context.Manifest.BootPolicyId.Value)),
                new DiagnosticPayloadEntry("SelectedProfileId", DiagnosticPayloadValue.FromInt32(context.SelectedProfile.Id.Value)),
                new DiagnosticPayloadEntry("BootStage", DiagnosticPayloadValue.FromString("RuntimeConstruction")),
                new DiagnosticPayloadEntry("FailureKind", DiagnosticPayloadValue.FromString(KernelBootBoundaryFailureKind.RuntimeConstructionFailed.ToString())),
                new DiagnosticPayloadEntry("SuggestedFix", DiagnosticPayloadValue.FromString("Ensure boot runtime construction is deterministic and side-effect free after validation passes.")),
            });

            return new KernelDiagnostic(
                new DiagnosticCode(KernelBootBoundaryCodes.RuntimeConstructionFailed),
                DiagnosticSeverity.Fatal,
                DiagnosticDomain.Boot,
                DiagnosticFailureBoundary.Kernel,
                "Boot runtime construction failed after validation succeeded.",
                diagnosticContext,
                payload,
                DiagnosticExceptionInfo.FromException(exception));
        }
    }
}
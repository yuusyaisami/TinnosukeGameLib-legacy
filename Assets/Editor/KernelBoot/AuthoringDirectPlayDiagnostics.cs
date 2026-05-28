#nullable enable

using System;
using System.Collections.Generic;
using Game.Kernel.Boot;
using Game.Kernel.Diagnostics;
using Game.Kernel.Generation;
using Game.Kernel.IR;
using Game.Kernel.Validation;
using TinnosukeGameLib.Editor.KernelBoot;

namespace Game.Kernel.Authoring
{
    public static class AuthoringDirectPlayDiagnosticCodes
    {
        public const string NormalizationMismatch = "UNITY_DIRECT_PLAY_KERNEL_IR_NORMALIZATION_MISMATCH";
        public const string UnexpectedFailure = "UNITY_DIRECT_PLAY_PREPARE_UNEXPECTED_FAILURE";
    }

    public static class AuthoringDirectPlayDiagnostics
    {
        public static KernelDiagnostic[] ToKernelDiagnostics(AuthoringDirectPlayResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            List<KernelDiagnostic> diagnostics = new List<KernelDiagnostic>(16);

            AppendExtractionDiagnostics(diagnostics, result.ExtractionReport);
            AppendNormalizationDiagnostics(diagnostics, result);
            AppendDependencyDiagnostics(diagnostics, result.DependencyValidationReport);
            AppendGenerationDiagnostics(diagnostics, result);
            AppendPromotionDiagnostics(diagnostics, result);
            AppendBootDiagnostics(diagnostics, result);

            return diagnostics.ToArray();
        }

        public static void Emit(IKernelDiagnosticService service, AuthoringDirectPlayResult result)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            if (result == null)
                throw new ArgumentNullException(nameof(result));

            DiagnosticSessionHandle? session = null;
            try
            {
                session = service.BeginSession(CreateSessionInfo(result));
                service.ReportBatch(ToKernelDiagnostics(result));
            }
            finally
            {
                if (session.HasValue)
                    service.EndSession(session.Value);
            }
        }

        static DiagnosticSessionInfo CreateSessionInfo(AuthoringDirectPlayResult result)
        {
            long correlationSeed = ((long)result.Input.PlanId.Value * 397L) ^ result.Input.ArtifactSetId.Value ^ result.Input.ManifestId.Value;
            string name = "Plan " + result.Input.PlanId.Value + " / ArtifactSet " + result.Input.ArtifactSetId.Value;
            return new DiagnosticSessionInfo("authoring-direct-play", name, new DiagnosticCorrelationId(correlationSeed));
        }

        static void AppendExtractionDiagnostics(List<KernelDiagnostic> diagnostics, ScopeAuthoringExtractionReport report)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));

            if (!report.IsValid || report.Issues.Count > 0)
                diagnostics.AddRange(report.ToKernelDiagnostics());
        }

        static void AppendNormalizationDiagnostics(List<KernelDiagnostic> diagnostics, AuthoringDirectPlayResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            if (result.NormalizationReport.IsValid)
                return;

            diagnostics.Add(result.NormalizationReport.ToKernelDiagnostic(result.Input));
        }

        static void AppendDependencyDiagnostics(List<KernelDiagnostic> diagnostics, DependencyValidationReport report)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));

            for (int index = 0; index < report.Issues.Count; index++)
                diagnostics.Add(report.Issues[index].ToKernelDiagnostic(DiagnosticFailureBoundary.Build));
        }

        static void AppendGenerationDiagnostics(List<KernelDiagnostic> diagnostics, AuthoringDirectPlayResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            KernelProjectionGenerationResult? generationResult = result.GenerationResult;
            if (generationResult == null)
                return;

            AppendProjectionValidationDiagnostics(diagnostics, result, generationResult);
            AppendPlanVerificationDiagnostics(diagnostics, result, generationResult);
        }

        static void AppendProjectionValidationDiagnostics(List<KernelDiagnostic> diagnostics, AuthoringDirectPlayResult result, KernelProjectionGenerationResult generationResult)
        {
            ProjectionValidationReport validationReport = generationResult.ProjectionValidationReport;
            if (validationReport.Issues.Count == 0)
                return;

            for (int index = 0; index < validationReport.Issues.Count; index++)
                diagnostics.Add(validationReport.Issues[index].ToKernelDiagnostic(DiagnosticFailureBoundary.Build));
        }

        static void AppendPlanVerificationDiagnostics(List<KernelDiagnostic> diagnostics, AuthoringDirectPlayResult result, KernelProjectionGenerationResult generationResult)
        {
            if (generationResult.IsVerified)
                return;

            KernelPlanHeader header = generationResult.GeneratedPlan.Header;
            for (int index = 0; index < generationResult.PlanVerification.Issues.Count; index++)
                diagnostics.Add(CreatePlanVerificationDiagnostic(result, header, generationResult.PlanVerification.Issues[index]));
        }

        static KernelDiagnostic CreatePlanVerificationDiagnostic(AuthoringDirectPlayResult result, KernelPlanHeader header, KernelPlanVerificationIssue issue)
        {
            DiagnosticContext context = new DiagnosticContext(
                null,
                artifact: new ArtifactIdentityRef(result.Input.ArtifactSetId.Value),
                profileId: result.Input.Profile.Id.Value,
                phase: "AuthoringDirectPlay/Generation");

            List<DiagnosticPayloadEntry> payloadEntries = new List<DiagnosticPayloadEntry>(8)
            {
                new DiagnosticPayloadEntry("AuthoringStage", DiagnosticPayloadValue.FromString(AuthoringDirectPlayStage.Generation.ToString())),
                new DiagnosticPayloadEntry("PlanId", DiagnosticPayloadValue.FromInt32(header.PlanId.Value)),
                new DiagnosticPayloadEntry("ArtifactSetId", DiagnosticPayloadValue.FromInt32(header.ArtifactSetId.Value)),
                new DiagnosticPayloadEntry("FormatVersion", DiagnosticPayloadValue.FromInt32(header.FormatVersion)),
                new DiagnosticPayloadEntry("GeneratorVersion", DiagnosticPayloadValue.FromString(header.GeneratorVersion)),
                new DiagnosticPayloadEntry("GeneratedHash", DiagnosticPayloadValue.FromString(header.GeneratedHash.ToString())),
            };

            return new KernelDiagnostic(
                new DiagnosticCode(issue.Code),
                DiagnosticSeverity.Error,
                DiagnosticDomain.Validation,
                DiagnosticFailureBoundary.Build,
                issue.Message,
                context,
                new DiagnosticPayload(payloadEntries));
        }

        static void AppendPromotionDiagnostics(List<KernelDiagnostic> diagnostics, AuthoringDirectPlayResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            if (result.PromotionStageResult != null && !result.PromotionStageResult.IsSuccessful)
                diagnostics.AddRange(CreatePromotionDiagnostics(result, result.PromotionStageResult, "AuthoringDirectPlay/Promotion", "Stage"));

            if (result.PromotionCommitResult != null && !result.PromotionCommitResult.IsSuccessful)
                diagnostics.AddRange(CreatePromotionDiagnostics(result, result.PromotionCommitResult, "AuthoringDirectPlay/Commit", "Commit"));
        }

        static KernelDiagnostic[] CreatePromotionDiagnostics(AuthoringDirectPlayResult result, ArtifactSetPromotionResult promotionResult, string phase, string promotionOperation)
        {
            List<KernelDiagnostic> diagnostics = new List<KernelDiagnostic>(promotionResult.Issues.Count);
            KernelBootManifest? manifest = result.Manifest;
            ArtifactSetPromotionInputs promotionInputs = promotionResult.StagingRecord != null
                ? promotionResult.StagingRecord.CurrentInputs
                : result.PromotionStageResult != null && result.PromotionStageResult.StagingRecord != null
                    ? result.PromotionStageResult.StagingRecord.CurrentInputs
                    : default;

            for (int index = 0; index < promotionResult.Issues.Count; index++)
            {
                ArtifactSetPromotionIssue issue = promotionResult.Issues[index];
                diagnostics.Add(CreatePromotionDiagnostic(result, promotionInputs, manifest, phase, promotionOperation, issue));
            }

            return diagnostics.ToArray();
        }

        static KernelDiagnostic CreatePromotionDiagnostic(AuthoringDirectPlayResult result, ArtifactSetPromotionInputs promotionInputs, KernelBootManifest? manifest, string phase, string promotionOperation, ArtifactSetPromotionIssue issue)
        {
            DiagnosticContext context = new DiagnosticContext(
                null,
                artifact: new ArtifactIdentityRef(result.Input.ArtifactSetId.Value),
                profileId: result.Input.Profile.Id.Value,
                phase: phase);

            List<DiagnosticPayloadEntry> payloadEntries = new List<DiagnosticPayloadEntry>(10)
            {
                new DiagnosticPayloadEntry("AuthoringStage", DiagnosticPayloadValue.FromString(AuthoringDirectPlayStage.Promotion.ToString())),
                new DiagnosticPayloadEntry("PromotionOperation", DiagnosticPayloadValue.FromString(promotionOperation)),
                new DiagnosticPayloadEntry("PromotionCode", DiagnosticPayloadValue.FromString(issue.Code)),
                new DiagnosticPayloadEntry("PromotionFormatVersion", DiagnosticPayloadValue.FromInt32(promotionInputs.FormatVersion)),
                new DiagnosticPayloadEntry("PromotionGeneratorVersion", DiagnosticPayloadValue.FromString(promotionInputs.GeneratorVersion)),
                new DiagnosticPayloadEntry("PromotionSourceHash", DiagnosticPayloadValue.FromString(promotionInputs.SourceHash.ToString())),
                new DiagnosticPayloadEntry("PromotionRegistryHash", DiagnosticPayloadValue.FromString(promotionInputs.RegistryHash.ToString())),
                new DiagnosticPayloadEntry("PromotionProfileHash", DiagnosticPayloadValue.FromString(promotionInputs.ProfileHash.ToString())),
                new DiagnosticPayloadEntry("PromotionDebugMapHash", DiagnosticPayloadValue.FromString(promotionInputs.DebugMapHash.ToString())),
            };

            if (manifest != null)
            {
                payloadEntries.Add(new DiagnosticPayloadEntry("ManifestId", DiagnosticPayloadValue.FromInt32(manifest.ManifestId.Value)));
                payloadEntries.Add(new DiagnosticPayloadEntry("BootPolicyId", DiagnosticPayloadValue.FromInt32(manifest.BootPolicyId.Value)));
            }

            return new KernelDiagnostic(
                new DiagnosticCode(issue.Code),
                DiagnosticSeverity.Error,
                DiagnosticDomain.Validation,
                DiagnosticFailureBoundary.Build,
                issue.Message,
                context,
                new DiagnosticPayload(payloadEntries));
        }

        static void AppendBootDiagnostics(List<KernelDiagnostic> diagnostics, AuthoringDirectPlayResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            KernelBootBoundaryResult? bootBoundaryResult = result.BootBoundaryResult;
            if (bootBoundaryResult != null && bootBoundaryResult.Diagnostics.Count > 0)
            {
                for (int index = 0; index < bootBoundaryResult.Diagnostics.Count; index++)
                    diagnostics.Add(bootBoundaryResult.Diagnostics[index]);

                return;
            }

            if (bootBoundaryResult != null)
                return;

            BootValidationReport? bootValidationReport = result.BootValidationReport;
            if (bootValidationReport != null && bootValidationReport.Issues.Count > 0)
            {
                for (int index = 0; index < bootValidationReport.Issues.Count; index++)
                    diagnostics.Add(bootValidationReport.Issues[index].ToKernelDiagnostic(result.Manifest, result.Input.Profile, result.Manifest?.DiagnosticsPolicy));
            }
        }
    }
}
#nullable enable

using System;
using System.Collections.Generic;
using Game.Kernel.Abstractions;
using Game.Kernel.Boot;
using Game.Kernel.Contributions;
using Game.Kernel.Diagnostics;
using Game.Kernel.Generation;
using Game.Kernel.IR;
using Game.Kernel.Validation;
using TinnosukeGameLib.Editor.KernelBoot;

namespace Game.Kernel.Authoring
{
    public enum AuthoringDirectPlayStage
    {
        None = 0,
        DirtyCheck = 5,
        Extraction = 10,
        Normalization = 20,
        DependencyValidation = 30,
        Generation = 40,
        Promotion = 50,
        Boot = 60,
    }

    public sealed class KernelIRNormalizationReport : IEquatable<KernelIRNormalizationReport>
    {
        public KernelIRNormalizationReport(Hash128 expectedSourceHash, Hash128 computedSourceHash, Hash128 expectedNormalizedHash, Hash128 computedNormalizedHash)
        {
            ExpectedSourceHash = expectedSourceHash;
            ComputedSourceHash = computedSourceHash;
            ExpectedNormalizedHash = expectedNormalizedHash;
            ComputedNormalizedHash = computedNormalizedHash;
        }

        public Hash128 ExpectedSourceHash { get; }

        public Hash128 ComputedSourceHash { get; }

        public Hash128 ExpectedNormalizedHash { get; }

        public Hash128 ComputedNormalizedHash { get; }

        public bool IsSourceHashCompatible => ExpectedSourceHash == ComputedSourceHash;

        public bool IsNormalizedHashCompatible => ExpectedNormalizedHash == ComputedNormalizedHash;

        public bool IsValid => IsSourceHashCompatible && IsNormalizedHashCompatible;

        public bool Equals(KernelIRNormalizationReport? other)
        {
            return other != null
                && ExpectedSourceHash == other.ExpectedSourceHash
                && ComputedSourceHash == other.ComputedSourceHash
                && ExpectedNormalizedHash == other.ExpectedNormalizedHash
                && ComputedNormalizedHash == other.ComputedNormalizedHash;
        }

        public override bool Equals(object? obj)
        {
            return obj is KernelIRNormalizationReport other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ExpectedSourceHash.GetHashCode();
                hash = (hash * 397) ^ ComputedSourceHash.GetHashCode();
                hash = (hash * 397) ^ ExpectedNormalizedHash.GetHashCode();
                hash = (hash * 397) ^ ComputedNormalizedHash.GetHashCode();
                return hash;
            }
        }

        public KernelDiagnostic ToKernelDiagnostic(AuthoringDirectPlayInput input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            DiagnosticContext context = new DiagnosticContext(
                null,
                artifact: new ArtifactIdentityRef(input.ArtifactSetId.Value),
                profileId: input.Profile.Id.Value,
                phase: "AuthoringDirectPlay/Normalization");

            List<DiagnosticPayloadEntry> payloadEntries = new List<DiagnosticPayloadEntry>(8)
            {
                new DiagnosticPayloadEntry("AuthoringStage", DiagnosticPayloadValue.FromString(AuthoringDirectPlayStage.Normalization.ToString())),
                new DiagnosticPayloadEntry("ExpectedSourceHash", DiagnosticPayloadValue.FromString(ExpectedSourceHash.ToString())),
                new DiagnosticPayloadEntry("ComputedSourceHash", DiagnosticPayloadValue.FromString(ComputedSourceHash.ToString())),
                new DiagnosticPayloadEntry("ExpectedNormalizedHash", DiagnosticPayloadValue.FromString(ExpectedNormalizedHash.ToString())),
                new DiagnosticPayloadEntry("ComputedNormalizedHash", DiagnosticPayloadValue.FromString(ComputedNormalizedHash.ToString())),
                new DiagnosticPayloadEntry("IsSourceHashCompatible", DiagnosticPayloadValue.FromBoolean(IsSourceHashCompatible)),
                new DiagnosticPayloadEntry("IsNormalizedHashCompatible", DiagnosticPayloadValue.FromBoolean(IsNormalizedHashCompatible)),
            };

            return new KernelDiagnostic(
                new DiagnosticCode(AuthoringDirectPlayDiagnosticCodes.NormalizationMismatch),
                DiagnosticSeverity.Error,
                DiagnosticDomain.Validation,
                DiagnosticFailureBoundary.Build,
                "Direct-play KernelIR normalization did not match the verified hash expectations.",
                context,
                new DiagnosticPayload(payloadEntries));
        }
    }

    public sealed class AuthoringDirectPlayInput
    {
        public AuthoringDirectPlayInput(
            IReadOnlyList<ScopeAuthoringRoot> roots,
            KernelIR kernelIR,
            KernelProfile profile,
            PlanId planId,
            ArtifactSetId artifactSetId,
            int formatVersion,
            string generatorVersion,
            ManifestId manifestId,
            BootPolicyId bootPolicyId,
            ArtifactSetPublicationState? publicationState = null,
            IKernelBootRuntimeSurfaceFactory? runtimeSurfaceFactory = null)
        {
            Roots = roots ?? throw new ArgumentNullException(nameof(roots));
            KernelIR = kernelIR ?? throw new ArgumentNullException(nameof(kernelIR));
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));

            if (planId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(planId), planId.Value, "Direct play requests must provide a positive plan identity.");

            if (artifactSetId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(artifactSetId), artifactSetId.Value, "Direct play requests must provide a positive artifact set identity.");

            if (formatVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(formatVersion), formatVersion, "Direct play requests must provide a positive format version.");

            if (string.IsNullOrWhiteSpace(generatorVersion))
                throw new ArgumentException("Direct play requests must provide a generator version.", nameof(generatorVersion));

            PlanId = planId;
            ArtifactSetId = artifactSetId;
            FormatVersion = formatVersion;
            GeneratorVersion = generatorVersion;
            ManifestId = manifestId;
            BootPolicyId = bootPolicyId;
            PublicationState = publicationState ?? ArtifactSetPublicationState.Empty;
            RuntimeSurfaceFactory = runtimeSurfaceFactory;
        }

        public IReadOnlyList<ScopeAuthoringRoot> Roots { get; }

        public KernelIR KernelIR { get; }

        public KernelProfile Profile { get; }

        public PlanId PlanId { get; }

        public ArtifactSetId ArtifactSetId { get; }

        public int FormatVersion { get; }

        public string GeneratorVersion { get; }

        public ManifestId ManifestId { get; }

        public BootPolicyId BootPolicyId { get; }

        public ArtifactSetPublicationState PublicationState { get; }

        public IKernelBootRuntimeSurfaceFactory? RuntimeSurfaceFactory { get; }
    }

    public sealed class AuthoringDirectPlayAuthoringInput
    {
        public AuthoringDirectPlayAuthoringInput(
            IReadOnlyList<ScopeAuthoringRoot> roots,
            KernelProfile profile,
            PlanId planId,
            ArtifactSetId artifactSetId,
            int formatVersion,
            string generatorVersion,
            ManifestId manifestId,
            BootPolicyId bootPolicyId,
            ArtifactSetPublicationState? publicationState = null,
            IKernelBootRuntimeSurfaceFactory? runtimeSurfaceFactory = null)
        {
            Roots = roots ?? throw new ArgumentNullException(nameof(roots));
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));

            if (planId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(planId), planId.Value, "Direct play requests must provide a positive plan identity.");

            if (artifactSetId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(artifactSetId), artifactSetId.Value, "Direct play requests must provide a positive artifact set identity.");

            if (formatVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(formatVersion), formatVersion, "Direct play requests must provide a positive format version.");

            if (string.IsNullOrWhiteSpace(generatorVersion))
                throw new ArgumentException("Direct play requests must provide a generator version.", nameof(generatorVersion));

            PlanId = planId;
            ArtifactSetId = artifactSetId;
            FormatVersion = formatVersion;
            GeneratorVersion = generatorVersion;
            ManifestId = manifestId;
            BootPolicyId = bootPolicyId;
            PublicationState = publicationState ?? ArtifactSetPublicationState.Empty;
            RuntimeSurfaceFactory = runtimeSurfaceFactory;
        }

        public IReadOnlyList<ScopeAuthoringRoot> Roots { get; }

        public KernelProfile Profile { get; }

        public PlanId PlanId { get; }

        public ArtifactSetId ArtifactSetId { get; }

        public int FormatVersion { get; }

        public string GeneratorVersion { get; }

        public ManifestId ManifestId { get; }

        public BootPolicyId BootPolicyId { get; }

        public ArtifactSetPublicationState PublicationState { get; }

        public IKernelBootRuntimeSurfaceFactory? RuntimeSurfaceFactory { get; }
    }

    public sealed class AuthoringDirectPlayResult
    {
        public AuthoringDirectPlayResult(
            AuthoringDirectPlayInput input,
            AuthoringDirectPlayStage failedStage,
            ScopeAuthoringExtractionReport extractionReport,
            KernelIRNormalizationReport normalizationReport,
            DependencyValidationReport dependencyValidationReport,
            KernelProjectionGenerationResult? generationResult,
            ArtifactSetPromotionResult? promotionStageResult,
            KernelBootManifest? manifest,
            BootValidationReport? bootValidationReport,
            KernelBootBoundaryResult? bootBoundaryResult,
            ArtifactSetPromotionResult? promotionCommitResult)
        {
            Input = input ?? throw new ArgumentNullException(nameof(input));
            FailedStage = failedStage;
            ExtractionReport = extractionReport ?? throw new ArgumentNullException(nameof(extractionReport));
            NormalizationReport = normalizationReport ?? throw new ArgumentNullException(nameof(normalizationReport));
            DependencyValidationReport = dependencyValidationReport ?? throw new ArgumentNullException(nameof(dependencyValidationReport));
            GenerationResult = generationResult;
            PromotionStageResult = promotionStageResult;
            Manifest = manifest;
            BootValidationReport = bootValidationReport;
            BootBoundaryResult = bootBoundaryResult;
            PromotionCommitResult = promotionCommitResult;
        }

        public AuthoringDirectPlayInput Input { get; }

        public AuthoringDirectPlayStage FailedStage { get; }

        public ScopeAuthoringExtractionReport ExtractionReport { get; }

        public KernelIRNormalizationReport NormalizationReport { get; }

        public DependencyValidationReport DependencyValidationReport { get; }

        public KernelProjectionGenerationResult? GenerationResult { get; }

        public ArtifactSetPromotionResult? PromotionStageResult { get; }

        public KernelBootManifest? Manifest { get; }

        public BootValidationReport? BootValidationReport { get; }

        public KernelBootBoundaryResult? BootBoundaryResult { get; }

        public ArtifactSetPromotionResult? PromotionCommitResult { get; }

        public bool IsSuccessful => FailedStage == AuthoringDirectPlayStage.None
            && BootBoundaryResult is KernelBootBoundaryResult.Success
            && PromotionCommitResult != null
            && PromotionCommitResult.IsPromoted;
    }

    public static class AuthoringBridge
    {
        public static AuthoringDirectPlayResult PrepareDirectPlay(AuthoringDirectPlayInput input)
        {
            return PrepareDirectPlay(input, null);
        }

        public static AuthoringDirectPlayResult PrepareDirectPlay(AuthoringDirectPlayInput input, KernelDiagnosticService? diagnosticService)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            if (diagnosticService == null)
            {
                ScopeAuthoringExtractionReport extractionReport = ScopeAuthoringExtractionService.Extract(input.Roots);
                return PrepareDirectPlayCore(input, extractionReport);
            }

            DiagnosticSessionHandle session = diagnosticService.BeginSession(CreateDirectPlayDiagnosticSessionInfo(input));
            try
            {
                ScopeAuthoringExtractionReport extractionReport = ScopeAuthoringExtractionService.Extract(input.Roots);
                AuthoringDirectPlayResult result = PrepareDirectPlayCore(input, extractionReport);
                diagnosticService.ReportBatch(AuthoringDirectPlayDiagnostics.ToKernelDiagnostics(result));
                return result;
            }
            catch (Exception exception)
            {
                diagnosticService.Report(CreateUnexpectedDirectPlayFailureDiagnostic(input, exception));
                throw;
            }
            finally
            {
                diagnosticService.EndSession(session);
            }
        }

        public static AuthoringDirectPlayResult PrepareDirectPlay(AuthoringDirectPlayAuthoringInput input)
        {
            return PrepareDirectPlay(input, null);
        }

        public static AuthoringDirectPlayResult PrepareDirectPlay(AuthoringDirectPlayAuthoringInput input, KernelDiagnosticService? diagnosticService)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            AuthoringValidationReport dirtyCheckReport = ScopeAuthoringValidationService.Validate(input.Roots);
            ScopeAuthoringExtractionReport dirtyExtractionReport = CreateExtractionReport(dirtyCheckReport);

            if (diagnosticService == null)
            {
                if (!dirtyCheckReport.IsValid)
                {
                    AuthoringDirectPlayInput dirtyInput = CreateDirectPlayInput(input, dirtyExtractionReport);
                    return CreateDirtyCheckFailureResult(dirtyInput, dirtyExtractionReport);
                }

                AuthoringDirectPlayInput verifiedInput = CreateVerifiedDirectPlayInput(input, out ScopeAuthoringExtractionReport extractionReport);
                return PrepareDirectPlayCore(verifiedInput, extractionReport);
            }

            DiagnosticSessionHandle session = diagnosticService.BeginSession(CreateDirectPlayDiagnosticSessionInfo(input));
            try
            {
                if (!dirtyCheckReport.IsValid)
                {
                    AuthoringDirectPlayInput dirtyInput = CreateDirectPlayInput(input, dirtyExtractionReport);
                    AuthoringDirectPlayResult dirtyResult = CreateDirtyCheckFailureResult(dirtyInput, dirtyExtractionReport);
                    diagnosticService.ReportBatch(AuthoringDirectPlayDiagnostics.ToKernelDiagnostics(dirtyResult));
                    return dirtyResult;
                }

                AuthoringDirectPlayInput verifiedInput = CreateVerifiedDirectPlayInput(input, out ScopeAuthoringExtractionReport extractionReport);
                AuthoringDirectPlayResult result = PrepareDirectPlayCore(verifiedInput, extractionReport);
                diagnosticService.ReportBatch(AuthoringDirectPlayDiagnostics.ToKernelDiagnostics(result));
                return result;
            }
            catch (Exception exception)
            {
                diagnosticService.Report(CreateUnexpectedDirectPlayFailureDiagnostic(input, exception));
                throw;
            }
            finally
            {
                diagnosticService.EndSession(session);
            }
        }

        static DiagnosticSessionInfo CreateDirectPlayDiagnosticSessionInfo(AuthoringDirectPlayInput input)
        {
            long correlationSeed = ((long)input.PlanId.Value * 397L) ^ input.ArtifactSetId.Value ^ input.ManifestId.Value;
            string name = "Plan " + input.PlanId.Value + " / ArtifactSet " + input.ArtifactSetId.Value;
            return new DiagnosticSessionInfo("authoring-direct-play", name, new DiagnosticCorrelationId(correlationSeed));
        }

        static DiagnosticSessionInfo CreateDirectPlayDiagnosticSessionInfo(AuthoringDirectPlayAuthoringInput input)
        {
            long correlationSeed = ((long)input.PlanId.Value * 397L) ^ input.ArtifactSetId.Value ^ input.ManifestId.Value;
            string name = "Plan " + input.PlanId.Value + " / ArtifactSet " + input.ArtifactSetId.Value;
            return new DiagnosticSessionInfo("authoring-direct-play", name, new DiagnosticCorrelationId(correlationSeed));
        }

        static KernelDiagnostic CreateUnexpectedDirectPlayFailureDiagnostic(AuthoringDirectPlayInput input, Exception exception)
        {
            DiagnosticContext context = new DiagnosticContext(
                null,
                artifact: new ArtifactIdentityRef(input.ArtifactSetId.Value),
                profileId: input.Profile.Id.Value,
                phase: "AuthoringDirectPlay/UnexpectedFailure");

            List<DiagnosticPayloadEntry> payloadEntries = new List<DiagnosticPayloadEntry>(6)
            {
                new DiagnosticPayloadEntry("Operation", DiagnosticPayloadValue.FromString("PrepareDirectPlay")),
                new DiagnosticPayloadEntry("PlanId", DiagnosticPayloadValue.FromInt32(input.PlanId.Value)),
                new DiagnosticPayloadEntry("ArtifactSetId", DiagnosticPayloadValue.FromInt32(input.ArtifactSetId.Value)),
                new DiagnosticPayloadEntry("ManifestId", DiagnosticPayloadValue.FromInt32(input.ManifestId.Value)),
                new DiagnosticPayloadEntry("BootPolicyId", DiagnosticPayloadValue.FromInt32(input.BootPolicyId.Value)),
                new DiagnosticPayloadEntry("RootCount", DiagnosticPayloadValue.FromInt32(input.Roots.Count)),
            };

            return new KernelDiagnostic(
                new DiagnosticCode(AuthoringDirectPlayDiagnosticCodes.UnexpectedFailure),
                DiagnosticSeverity.Fatal,
                DiagnosticDomain.UnityBridge,
                DiagnosticFailureBoundary.Operation,
                "Direct-play preparation threw an unexpected exception.",
                context,
                new DiagnosticPayload(payloadEntries),
                DiagnosticExceptionInfo.FromException(exception));
        }

        static KernelDiagnostic CreateUnexpectedDirectPlayFailureDiagnostic(AuthoringDirectPlayAuthoringInput input, Exception exception)
        {
            DiagnosticContext context = new DiagnosticContext(
                null,
                artifact: new ArtifactIdentityRef(input.ArtifactSetId.Value),
                profileId: input.Profile.Id.Value,
                phase: "AuthoringDirectPlay/UnexpectedFailure");

            List<DiagnosticPayloadEntry> payloadEntries = new List<DiagnosticPayloadEntry>(6)
            {
                new DiagnosticPayloadEntry("Operation", DiagnosticPayloadValue.FromString("PrepareDirectPlay")),
                new DiagnosticPayloadEntry("PlanId", DiagnosticPayloadValue.FromInt32(input.PlanId.Value)),
                new DiagnosticPayloadEntry("ArtifactSetId", DiagnosticPayloadValue.FromInt32(input.ArtifactSetId.Value)),
                new DiagnosticPayloadEntry("ManifestId", DiagnosticPayloadValue.FromInt32(input.ManifestId.Value)),
                new DiagnosticPayloadEntry("BootPolicyId", DiagnosticPayloadValue.FromInt32(input.BootPolicyId.Value)),
                new DiagnosticPayloadEntry("RootCount", DiagnosticPayloadValue.FromInt32(input.Roots.Count)),
            };

            return new KernelDiagnostic(
                new DiagnosticCode(AuthoringDirectPlayDiagnosticCodes.UnexpectedFailure),
                DiagnosticSeverity.Fatal,
                DiagnosticDomain.UnityBridge,
                DiagnosticFailureBoundary.Operation,
                "Direct-play preparation threw an unexpected exception.",
                context,
                new DiagnosticPayload(payloadEntries),
                DiagnosticExceptionInfo.FromException(exception));
        }

        static AuthoringDirectPlayResult CreateDirtyCheckFailureResult(AuthoringDirectPlayInput input, ScopeAuthoringExtractionReport extractionReport)
        {
            KernelIRNormalizationReport normalizationReport = CreateNormalizationReport(input.KernelIR);
            DependencyValidationReport dependencyValidationReport = DependencyValidator.Validate(input.KernelIR);

            return new AuthoringDirectPlayResult(
                input,
                AuthoringDirectPlayStage.DirtyCheck,
                extractionReport,
                normalizationReport,
                dependencyValidationReport,
                null,
                null,
                null,
                null,
                null,
                null);
        }

        static AuthoringDirectPlayInput CreateVerifiedDirectPlayInput(AuthoringDirectPlayAuthoringInput input, out ScopeAuthoringExtractionReport extractionReport)
        {
            extractionReport = ScopeAuthoringExtractionService.Extract(input.Roots);
            return CreateDirectPlayInput(input, extractionReport);
        }

        static AuthoringDirectPlayInput CreateDirectPlayInput(AuthoringDirectPlayAuthoringInput input, ScopeAuthoringExtractionReport extractionReport)
        {
            KernelIR kernelIR = CreateRepresentativeKernelIR(input, extractionReport);

            return new AuthoringDirectPlayInput(
                input.Roots,
                kernelIR,
                input.Profile,
                input.PlanId,
                input.ArtifactSetId,
                input.FormatVersion,
                input.GeneratorVersion,
                input.ManifestId,
                input.BootPolicyId,
                input.PublicationState,
                input.RuntimeSurfaceFactory);
        }

        static ScopeAuthoringExtractionReport CreateExtractionReport(AuthoringValidationReport validationReport)
        {
            if (validationReport == null)
                throw new ArgumentNullException(nameof(validationReport));

            AuthoringValidationIssue[] issues = new AuthoringValidationIssue[validationReport.Issues.Count];
            for (int index = 0; index < validationReport.Issues.Count; index++)
                issues[index] = validationReport.Issues[index];

            return new ScopeAuthoringExtractionReport(Array.Empty<ModuleContributionData>(), issues);
        }

        static AuthoringDirectPlayResult PrepareDirectPlayCore(AuthoringDirectPlayInput input, ScopeAuthoringExtractionReport extractionReport)
        {
            KernelIRNormalizationReport normalizationReport = CreateNormalizationReport(input.KernelIR);
            DependencyValidationReport dependencyValidationReport = DependencyValidator.Validate(input.KernelIR);

            if (!extractionReport.IsValid)
            {
                return new AuthoringDirectPlayResult(
                    input,
                    AuthoringDirectPlayStage.Extraction,
                    extractionReport,
                    normalizationReport,
                    dependencyValidationReport,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null);
            }

            if (!normalizationReport.IsValid)
            {
                return new AuthoringDirectPlayResult(
                    input,
                    AuthoringDirectPlayStage.Normalization,
                    extractionReport,
                    normalizationReport,
                    dependencyValidationReport,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null);
            }

            if (dependencyValidationReport.Status == ValidationResultStatus.Failed || dependencyValidationReport.Status == ValidationResultStatus.Fatal)
            {
                return new AuthoringDirectPlayResult(
                    input,
                    AuthoringDirectPlayStage.DependencyValidation,
                    extractionReport,
                    normalizationReport,
                    dependencyValidationReport,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null);
            }

            KernelProfileKind profileKind = input.Profile.Kind;
            KernelProfileMask selectedProfileMask = ToProfileMask(profileKind);
            string selectedProfile = profileKind.ToString();

            KernelProjectionGenerationResult generationResult = KernelProjectionGenerator.Generate(
                input.KernelIR,
                input.PlanId,
                input.ArtifactSetId,
                input.FormatVersion,
                input.GeneratorVersion,
                selectedProfile,
                selectedProfileMask);

            if (!generationResult.IsVerified)
            {
                return new AuthoringDirectPlayResult(
                    input,
                    AuthoringDirectPlayStage.Generation,
                    extractionReport,
                    normalizationReport,
                    dependencyValidationReport,
                    generationResult,
                    null,
                    null,
                    null,
                    null,
                    null);
            }

            VerifiedKernelPlan verifiedPlan = generationResult.PlanVerification.VerifiedPlan!;

            ArtifactSetPromotionInputs promotionInputs = new ArtifactSetPromotionInputs(
                generationResult.GeneratedPlan.Header.SourceHash,
                generationResult.GeneratedPlan.Header.RegistryHash,
                generationResult.GeneratedPlan.Header.ProfileHash,
                generationResult.GeneratedPlan.Header.DebugMapHash,
                input.FormatVersion,
                input.GeneratorVersion);

            ArtifactSetPromotionResult stageResult = ArtifactSetPromotionTransaction.Stage(input.PublicationState, promotionInputs, verifiedPlan);
            if (!stageResult.IsSuccessful)
            {
                return new AuthoringDirectPlayResult(
                    input,
                    AuthoringDirectPlayStage.Promotion,
                    extractionReport,
                    normalizationReport,
                    dependencyValidationReport,
                    generationResult,
                    stageResult,
                    null,
                    null,
                    null,
                    null);
            }

            ArtifactSetStagingRecord stagingRecord = stageResult.StagingRecord!;
            KernelBootManifest manifest = CreateBootManifest(input, stagingRecord.Candidate);
            BootValidationInput bootInput = CreateBootValidationInput(input, manifest, generationResult, dependencyValidationReport);
            BootValidationReport bootValidationReport = BootValidator.Validate(bootInput);

            if (bootValidationReport.HasBlockingIssues)
            {
                return new AuthoringDirectPlayResult(
                    input,
                    AuthoringDirectPlayStage.Boot,
                    extractionReport,
                    normalizationReport,
                    dependencyValidationReport,
                    generationResult,
                    stageResult,
                    manifest,
                    bootValidationReport,
                    KernelBootBoundaryResult.Failed(bootValidationReport, KernelBootBoundaryFailureKind.ValidationBlocked),
                    null);
            }

            KernelBootBoundaryResult bootBoundaryResult = input.RuntimeSurfaceFactory == null
                ? KernelBootBoundary.Execute(bootInput)
                : KernelBootBoundary.Execute(bootInput, input.RuntimeSurfaceFactory);

            if (!bootBoundaryResult.IsReady)
            {
                return new AuthoringDirectPlayResult(
                    input,
                    AuthoringDirectPlayStage.Boot,
                    extractionReport,
                    normalizationReport,
                    dependencyValidationReport,
                    generationResult,
                    stageResult,
                    manifest,
                    bootValidationReport,
                    bootBoundaryResult,
                    null);
            }

            ArtifactSetPromotionResult commitResult = ArtifactSetPromotionTransaction.Commit(stageResult.PublicationState, stagingRecord);
            if (!commitResult.IsSuccessful)
            {
                return new AuthoringDirectPlayResult(
                    input,
                    AuthoringDirectPlayStage.Promotion,
                    extractionReport,
                    normalizationReport,
                    dependencyValidationReport,
                    generationResult,
                    stageResult,
                    manifest,
                    bootValidationReport,
                    bootBoundaryResult,
                    commitResult);
            }

            return new AuthoringDirectPlayResult(
                input,
                AuthoringDirectPlayStage.None,
                extractionReport,
                normalizationReport,
                dependencyValidationReport,
                generationResult,
                stageResult,
                manifest,
                bootValidationReport,
                bootBoundaryResult,
                commitResult);
        }

        static KernelIR CreateRepresentativeKernelIR(AuthoringDirectPlayAuthoringInput input, ScopeAuthoringExtractionReport extractionReport)
        {
            ModuleContributionData? primaryContribution = extractionReport.Contributions.Count > 0
                ? extractionReport.Contributions[0]
                : null;

            List<SourceLocationIR> sourceLocations = new List<SourceLocationIR>(8);
            HashSet<SourceLocationIR> seenSources = new HashSet<SourceLocationIR>();

            void AddSource(SourceLocationIR source)
            {
                if (!source.IsSpecified)
                    return;

                if (seenSources.Add(source))
                    sourceLocations.Add(source);
            }

            if (primaryContribution != null)
            {
                AddSource(primaryContribution.SourceLocation);

                ReadOnlySpan<ContributionItem> items = primaryContribution.Items;
                for (int index = 0; index < items.Length; index++)
                    AddSource(items[index].SourceLocation);
            }

            if (sourceLocations.Count == 0)
            {
                AddSource(new SourceLocationIR(new GeneratedSourceLocation(
                    "AuthoringBridge",
                    "DirectPlay",
                    input.PlanId.Value.ToString())));
            }

            SourceLocationTable sourceTable = new SourceLocationTable(sourceLocations.ToArray());
            SourceLocationId moduleSourceId = new SourceLocationId(1);
            SourceLocationId bodySourceId = new SourceLocationId(sourceLocations.Count > 1 ? 2 : 1);
            KernelProfileMask profileMask = ToProfileMask(input.Profile.Kind);

            ModuleId moduleId = primaryContribution != null
                ? primaryContribution.ModuleId
                : new ModuleId(input.PlanId.Value);

            string moduleName = primaryContribution != null
                ? primaryContribution.ModuleName
                : "DirectPlayModule";

            ModuleKind moduleKind = primaryContribution != null
                ? primaryContribution.ModuleKind
                : ModuleKind.Feature;

            ModuleVersion moduleVersion = primaryContribution != null
                ? primaryContribution.ModuleVersion
                : new ModuleVersion(1);

            KernelProfileIR profile = new KernelProfileIR(
                input.Profile.Id.Value.ToString(),
                profileMask,
                new AvailabilityIR(profileMask, true, null));

            ModuleIR module = new ModuleIR(
                moduleId,
                moduleName,
                moduleKind,
                moduleVersion,
                new ModuleAvailabilityIR(new AvailabilityIR(profileMask, true, null)),
                moduleSourceId);

            ServiceIR service = new ServiceIR(
                new ServiceId(10),
                "DirectPlayService",
                ServiceLifetimeKind.Singleton,
                moduleId,
                new[]
                {
                    new ServiceContractIR("IDirectPlayService", bodySourceId),
                },
                null,
                ServiceFactoryKind.GeneratedFactory,
                bodySourceId);

            ScopeIR scope = new ScopeIR(
                new ScopeAuthoringId(1),
                new ScopePlanId(1),
                "DirectPlayScope",
                ScopeKind.Root,
                moduleId,
                default,
                Array.Empty<ScopeServiceRequirementIR>(),
                new[]
                {
                    new ScopeValueInitRefIR(new ValueInitPlanId(1), bodySourceId),
                },
                new ScopeServiceBoundaryIR(ScopeServiceBoundaryKind.OwnedLocal, 1, bodySourceId),
                new LifecyclePlanRefIR(new LifecyclePlanId(1), bodySourceId),
                bodySourceId);

            ValueKeyIR valueKey = new ValueKeyIR(
                new ValueKeyId(1),
                "direct-play.value",
                "Direct Play Value",
                ValueKind.String,
                moduleId,
                new ValueSchemaRefIR(new ValueSchemaId(1), bodySourceId),
                new SavePolicyIR(true, true, "direct-play"),
                bodySourceId);

            ValueInitPlanIR valueInitPlan = new ValueInitPlanIR(
                new ValueInitPlanId(1),
                moduleId,
                new ScopePlanId(1),
                "direct-play",
                LifecyclePhase.Create,
                0,
                new AvailabilityIR(profileMask, true, null),
                new[]
                {
                    new ValueInitEntryIR(
                        new ValueKeyId(1),
                        ValueInitEntrySourceKind.Literal,
                        ValueKind.String,
                        0,
                        ValueInitOverwritePolicy.Overwrite,
                        bodySourceId,
                        "ready"),
                },
                bodySourceId);

            CommandIR command = new CommandIR(
                new CommandTypeId(1),
                "DirectPlayCommand",
                new CommandAuthoringKeyRefIR(new CommandAuthoringKeyId(1), "direct-play.command", bodySourceId),
                new CommandCategoryId(1),
                moduleId,
                new CommandPayloadSchemaRefIR(new CommandPayloadSchemaId(1), bodySourceId),
                new CommandExecutorRefIR(new CommandExecutorId(1), bodySourceId),
                Array.Empty<CommandDependencyIR>(),
                bodySourceId);

            RuntimeQueryIR runtimeQuery = new RuntimeQueryIR(
                new RuntimeQueryId(1),
                "DirectPlayRuntimeQuery",
                RuntimeQueryTargetKind.Service,
                new[]
                {
                    new RuntimeIdentityFieldIR("serviceId", "int", true),
                },
                new RuntimeQueryPolicyIR(true, false, DependencyPhase.Runtime),
                moduleId,
                bodySourceId);

            LifecycleIR lifecycle = new LifecycleIR(
                new LifecyclePlanId(1),
                "DirectPlayLifecycle",
                moduleId,
                new[]
                {
                    new LifecycleStepIR(
                        new LifecycleStepId(1),
                        LifecyclePhase.Acquire,
                        0,
                        new LifecycleTargetRefIR(new ServiceId(10)),
                        LifecycleActionKind.ServiceMethod,
                        null,
                        bodySourceId),
                },
                bodySourceId,
                LifecycleFailurePolicy.FailKernel);

            KernelIR provisional = new KernelIR(
                new KernelIRHeader(
                    "direct-play-authoring",
                    input.FormatVersion,
                    moduleName,
                    profile.Id,
                    input.GeneratorVersion,
                    new Hash128(1, 2, 3, 4),
                    new Hash128(5, 6, 7, 8)),
                profile,
                new[] { module },
                new[] { scope },
                new[] { service },
                new[] { command },
                new[] { valueKey },
                new[] { lifecycle },
                new[] { runtimeQuery },
                Array.Empty<DependencyEdgeIR>(),
                sourceTable,
                diagnosticSeeds: null,
                valueInitPlans: new[] { valueInitPlan });

            Hash128 normalizedHash = KernelIRHashing.ComputeNormalizedHash(provisional);

            return new KernelIR(
                new KernelIRHeader(
                    "direct-play-authoring",
                    input.FormatVersion,
                    moduleName,
                    profile.Id,
                    input.GeneratorVersion,
                    normalizedHash,
                    normalizedHash),
                profile,
                new[] { module },
                new[] { scope },
                new[] { service },
                new[] { command },
                new[] { valueKey },
                new[] { lifecycle },
                new[] { runtimeQuery },
                Array.Empty<DependencyEdgeIR>(),
                sourceTable,
                diagnosticSeeds: null,
                valueInitPlans: new[] { valueInitPlan });
        }

        static KernelIRNormalizationReport CreateNormalizationReport(KernelIR kernelIR)
        {
            Hash128 computedSourceHash = VerifiedArtifactHeaderHashing.ComputeSourceHash(kernelIR);
            Hash128 computedNormalizedHash = KernelIRHashing.ComputeNormalizedHash(kernelIR);
            return new KernelIRNormalizationReport(kernelIR.Header.SourceHash, computedSourceHash, kernelIR.Header.NormalizedHash, computedNormalizedHash);
        }

        static KernelBootManifest CreateBootManifest(AuthoringDirectPlayInput input, VerifiedKernelPlan candidate)
        {
            VerifiedArtifactSetRef artifactSet = new VerifiedArtifactSetRef(
                candidate.Header.ArtifactSetId,
                candidate.Header.PlanId,
                candidate.Header.SourceHash.ToString(),
                candidate.Header.ProfileHash.ToString(),
                candidate.Header.FormatVersion,
                candidate.Header.RegistryHash.ToString(),
                candidate.Header.DebugMapHash.ToString());

            return new KernelBootManifest(
                input.ManifestId,
                input.Profile.Id,
                artifactSet,
                input.BootPolicyId,
                BootDiagnosticsPolicy.ForKind(input.Profile.Kind));
        }

        static BootValidationInput CreateBootValidationInput(
            AuthoringDirectPlayInput input,
            KernelBootManifest manifest,
            KernelProjectionGenerationResult generationResult,
            DependencyValidationReport dependencyValidationReport)
        {
            VerifiedKernelPlan verifiedPlan = generationResult.PlanVerification.VerifiedPlan!;
            KernelPlanHeader header = verifiedPlan.Header;

            BootArtifactValidationState artifactState = new BootArtifactValidationState(
                artifactSetComplete: true,
                artifactHeadersCompatible: true,
                artifactStale: false,
                debugMapRequired: true,
                kernelIRHash: header.SourceHash.ToString(),
                registryHash: header.RegistryHash.ToString(),
                profileHash: header.ProfileHash.ToString(),
                debugMapHash: header.DebugMapHash.ToString());

            BootRootValidationState rootState = CreateBootRootValidationState(generationResult.Projections.ServiceGraph, generationResult.Projections.ScopeGraph);

            BootFallbackValidationState fallbackState = new BootFallbackValidationState(
                legacyFallbackAttempted: false,
                runtimeDiscoveryAttempted: false,
                resourcesFallbackAttempted: false,
                defaultRootCreationAttempted: false,
                duplicateRootCleanupAttempted: false,
                nonDeterministicTestPolicy: false);

            return new BootValidationInput(
                manifest,
                input.Profile,
                artifactSetReferencePresent: true,
                dependencyValidationStatus: dependencyValidationReport.Status,
                artifactState,
                rootState,
                fallbackState,
                generationResult.Projections.ScopeGraph,
                generationResult.Projections.ServiceGraph,
                generationResult.Projections.LifecyclePlan,
                generationResult.Projections.DebugMap,
                generationResult.Projections.CommandCatalog,
                generationResult.Projections.ValueSchema,
                generationResult.Projections.RuntimeQuery,
                BootRequiredProjectionKind.All);
        }

        static BootRootValidationState CreateBootRootValidationState(ServiceGraphPlan serviceGraph, ScopeGraphPlan scopeGraph)
        {
            RuntimeIdentityRef[] availableRootServices = CreateAvailableRootServices(serviceGraph);
            RuntimeIdentityRef[] availableRootScopes = CreateAvailableRootScopes(scopeGraph);

            return new BootRootValidationState(
                Array.Empty<RuntimeIdentityRef>(),
                availableRootServices,
                Array.Empty<RuntimeIdentityRef>(),
                availableRootScopes);
        }

        static RuntimeIdentityRef[] CreateAvailableRootServices(ServiceGraphPlan serviceGraph)
        {
            ReadOnlySpan<ServiceIR> services = serviceGraph.Services;
            RuntimeIdentityRef[] identities = new RuntimeIdentityRef[services.Length];
            for (int index = 0; index < services.Length; index++)
                identities[index] = new RuntimeIdentityRef(RuntimeIdentityKind.Service, services[index].Id.Value);

            return identities;
        }

        static RuntimeIdentityRef[] CreateAvailableRootScopes(ScopeGraphPlan scopeGraph)
        {
            ReadOnlySpan<ScopeIR> scopes = scopeGraph.Scopes;
            List<RuntimeIdentityRef> identities = new List<RuntimeIdentityRef>(scopes.Length);

            for (int index = 0; index < scopes.Length; index++)
            {
                ScopeIR scope = scopes[index];
                if (scope.Kind == ScopeKind.Root)
                    identities.Add(new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, scope.PlanId.Value));
            }

            return identities.ToArray();
        }

        static KernelProfileMask ToProfileMask(KernelProfileKind kind)
        {
            return kind switch
            {
                KernelProfileKind.Development => KernelProfileMask.Development,
                KernelProfileKind.Release => KernelProfileMask.Release,
                KernelProfileKind.Test => KernelProfileMask.Test,
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported kernel profile kind."),
            };
        }
    }
}
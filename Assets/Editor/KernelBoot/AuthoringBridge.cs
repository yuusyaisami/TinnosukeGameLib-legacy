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

    public sealed class AuthoringDirectPlayResult
    {
        public AuthoringDirectPlayResult(
            AuthoringDirectPlayInput input,
            KernelIR effectiveKernelIR,
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
            EffectiveKernelIR = effectiveKernelIR ?? throw new ArgumentNullException(nameof(effectiveKernelIR));
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

        public KernelIR EffectiveKernelIR { get; }

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
                return PrepareDirectPlayCore(input);

            DiagnosticSessionHandle session = diagnosticService.BeginSession(CreateDirectPlayDiagnosticSessionInfo(input));
            try
            {
                AuthoringDirectPlayResult result = PrepareDirectPlayCore(input);
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

        static AuthoringDirectPlayResult PrepareDirectPlayCore(AuthoringDirectPlayInput input)
        {
            ScopeAuthoringExtractionReport extractionReport = ScopeAuthoringExtractionService.Extract(input.Roots);
            KernelIR effectiveKernelIR = CreateEffectiveKernelIR(input.KernelIR, extractionReport);
            KernelIRNormalizationReport normalizationReport = CreateNormalizationReport(effectiveKernelIR);
            DependencyValidationReport dependencyValidationReport = DependencyValidator.Validate(effectiveKernelIR);

            if (!extractionReport.IsValid)
            {
                return new AuthoringDirectPlayResult(
                    input,
                    effectiveKernelIR,
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
                    effectiveKernelIR,
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
                    effectiveKernelIR,
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
                effectiveKernelIR,
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
                    effectiveKernelIR,
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
                    effectiveKernelIR,
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
                    effectiveKernelIR,
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
                    effectiveKernelIR,
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
                    effectiveKernelIR,
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
                effectiveKernelIR,
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

        static KernelIR CreateEffectiveKernelIR(KernelIR kernelIR, ScopeAuthoringExtractionReport extractionReport)
        {
            if (kernelIR == null)
                throw new ArgumentNullException(nameof(kernelIR));

            if (extractionReport == null)
                throw new ArgumentNullException(nameof(extractionReport));

            if (extractionReport.EntityInputs.Count == 0
                && extractionReport.DeclarationInputs.Count == 0
                && extractionReport.ServiceDeclarations.Count == 0)
                return kernelIR;

            List<SourceLocationIR> sources = new List<SourceLocationIR>(kernelIR.Sources.Count + extractionReport.EntityInputs.Count + extractionReport.DeclarationInputs.Count + extractionReport.ServiceDeclarations.Count);
            AppendRange(kernelIR.Sources.Sources, sources);

            List<DiagnosticSeedIR> diagnosticSeeds = new List<DiagnosticSeedIR>(kernelIR.DiagnosticSeeds.Length + extractionReport.EntityInputs.Count + extractionReport.DeclarationInputs.Count + extractionReport.ServiceDeclarations.Count);
            AppendRange(kernelIR.DiagnosticSeeds, diagnosticSeeds);

            HashSet<string> seenSeedKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < diagnosticSeeds.Count; index++)
                seenSeedKeys.Add(diagnosticSeeds[index].SeedKey);

            List<AuthoringProvenanceSeedRecord> provenanceSeeds = BuildAuthoringProvenanceSeeds(extractionReport);
            provenanceSeeds.Sort((left, right) => StringComparer.Ordinal.Compare(left.SeedKey, right.SeedKey));

            for (int index = 0; index < provenanceSeeds.Count; index++)
            {
                AuthoringProvenanceSeedRecord provenanceSeed = provenanceSeeds[index];
                if (!seenSeedKeys.Add(provenanceSeed.SeedKey))
                    continue;

                SourceLocationId sourceId = FindOrAddSource(provenanceSeed.Source, sources);
                diagnosticSeeds.Add(new DiagnosticSeedIR(provenanceSeed.SeedKey, provenanceSeed.DebugName, provenanceSeed.OwnerModule, sourceId));
            }

            ServiceIR[] services = CreateEffectiveServices(kernelIR.Services, extractionReport.ServiceDeclarations, sources);
            return RebuildKernelIR(kernelIR, new SourceLocationTable(sources.ToArray()), diagnosticSeeds.ToArray(), services);
        }

        static List<AuthoringProvenanceSeedRecord> BuildAuthoringProvenanceSeeds(ScopeAuthoringExtractionReport extractionReport)
        {
            List<AuthoringProvenanceSeedRecord> seeds = new List<AuthoringProvenanceSeedRecord>(extractionReport.EntityInputs.Count + extractionReport.DeclarationInputs.Count + extractionReport.ServiceDeclarations.Count);

            for (int index = 0; index < extractionReport.EntityInputs.Count; index++)
            {
                EntityAuthoringInput input = extractionReport.EntityInputs[index];
                seeds.Add(new AuthoringProvenanceSeedRecord(
                    "authoring.entity." + input.EntityRef.Value,
                    input.DebugName.Length == 0 ? input.DisplayName : input.DebugName,
                    input.OwnerModule,
                    input.Source));
            }

            for (int index = 0; index < extractionReport.DeclarationInputs.Count; index++)
            {
                EntityDeclarationPlanInput input = extractionReport.DeclarationInputs[index];
                string identitySuffix = input.DeclarationId.Length != 0
                    ? input.DeclarationId
                    : (input.ServiceId.Length != 0 ? input.ServiceId : input.DeclarationType);

                seeds.Add(new AuthoringProvenanceSeedRecord(
                    "authoring.declaration." + input.OwnerEntityRef.Value + "." + identitySuffix,
                    input.DeclarationType,
                    input.OwnerModule,
                    input.Source));
            }

            for (int index = 0; index < extractionReport.ServiceDeclarations.Count; index++)
            {
                EntityServiceDeclarationInput input = extractionReport.ServiceDeclarations[index];
                seeds.Add(new AuthoringProvenanceSeedRecord(
                    "authoring.service." + input.ServiceId.Value.ToString("D10"),
                    input.DebugName.Length == 0 ? input.ServiceName : input.DebugName,
                    input.OwnerModule,
                    input.Source));
            }

            return seeds;
        }

        static SourceLocationId FindOrAddSource(SourceLocationIR source, List<SourceLocationIR> sources)
        {
            for (int index = 0; index < sources.Count; index++)
            {
                if (sources[index] == source)
                    return new SourceLocationId(index + 1);
            }

            sources.Add(source);
            return new SourceLocationId(sources.Count);
        }

        static ServiceIR[] CreateEffectiveServices(
            ReadOnlySpan<ServiceIR> existingServices,
            IReadOnlyList<EntityServiceDeclarationInput> serviceDeclarations,
            List<SourceLocationIR> sources)
        {
            ServiceIR[] services = new ServiceIR[existingServices.Length + serviceDeclarations.Count];
            for (int index = 0; index < existingServices.Length; index++)
                services[index] = existingServices[index];

            for (int index = 0; index < serviceDeclarations.Count; index++)
            {
                EntityServiceDeclarationInput declaration = serviceDeclarations[index];
                SourceLocationId sourceId = FindOrAddSource(declaration.Source, sources);
                services[existingServices.Length + index] = CreateServiceIR(declaration, sourceId);
            }

            return services;
        }

        static ServiceIR CreateServiceIR(EntityServiceDeclarationInput declaration, SourceLocationId sourceId)
        {
            ReadOnlySpan<string> contractNames = declaration.ContractNames;
            ServiceContractIR[] contracts = new ServiceContractIR[contractNames.Length];
            for (int index = 0; index < contractNames.Length; index++)
                contracts[index] = new ServiceContractIR(contractNames[index], sourceId);

            ReadOnlySpan<EntityServiceDependencyInput> declaredDependencies = declaration.Dependencies;
            ServiceDependencyIR[] dependencies = new ServiceDependencyIR[declaredDependencies.Length];
            for (int index = 0; index < declaredDependencies.Length; index++)
            {
                EntityServiceDependencyInput dependency = declaredDependencies[index];
                dependencies[index] = new ServiceDependencyIR(dependency.Target, dependency.Strength, sourceId);
            }

            return new ServiceIR(
                declaration.ServiceId,
                declaration.ServiceName,
                declaration.Lifetime,
                declaration.OwnerModule,
                contracts,
                dependencies,
                declaration.FactoryKind,
                sourceId);
        }

        static KernelIR RebuildKernelIR(KernelIR source, SourceLocationTable sources, DiagnosticSeedIR[] diagnosticSeeds, ServiceIR[] services)
        {
            KernelIR provisional = CreateKernelIRCopy(source, sources, diagnosticSeeds, services, source.Header.SourceHash, source.Header.NormalizedHash);
            Hash128 computedSourceHash = VerifiedArtifactHeaderHashing.ComputeSourceHash(provisional);
            Hash128 computedNormalizedHash = KernelIRHashing.ComputeNormalizedHash(provisional);
            return CreateKernelIRCopy(source, sources, diagnosticSeeds, services, computedSourceHash, computedNormalizedHash);
        }

        static KernelIR CreateKernelIRCopy(KernelIR source, SourceLocationTable sources, DiagnosticSeedIR[] diagnosticSeeds, ServiceIR[] services, Hash128 sourceHash, Hash128 normalizedHash)
        {
            KernelIRHeader header = new KernelIRHeader(
                source.Header.DocumentId,
                source.Header.FormatVersion,
                source.Header.ProjectName,
                source.Header.ProfileId,
                source.Header.GeneratorVersion,
                sourceHash,
                normalizedHash);

            return new KernelIR(
                header,
                source.Profile,
                CopySpan(source.Modules),
                CopySpan(source.Scopes),
                services,
                CopySpan(source.Commands),
                CopySpan(source.ValueKeys),
                CopySpan(source.Lifecycles),
                CopySpan(source.RuntimeQueries),
                CopySpan(source.Dependencies),
                sources,
                diagnosticSeeds,
                CopySpan(source.ValueInitPlans));
        }

        static T[] CopySpan<T>(ReadOnlySpan<T> span)
        {
            T[] clone = new T[span.Length];
            for (int index = 0; index < span.Length; index++)
                clone[index] = span[index];

            return clone;
        }

        static void AppendRange<T>(ReadOnlySpan<T> source, List<T> destination)
        {
            for (int index = 0; index < source.Length; index++)
                destination.Add(source[index]);
        }

        readonly struct AuthoringProvenanceSeedRecord
        {
            public AuthoringProvenanceSeedRecord(string seedKey, string debugName, ModuleId ownerModule, SourceLocationIR source)
            {
                SeedKey = seedKey;
                DebugName = debugName;
                OwnerModule = ownerModule;
                Source = source;
            }

            public string SeedKey { get; }

            public string DebugName { get; }

            public ModuleId OwnerModule { get; }

            public SourceLocationIR Source { get; }
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
                generationResult.Projections.ServiceGraph,
                generationResult.Projections.ScopeGraph,
                generationResult.Projections.LifecyclePlan,
                generationResult.Projections.DebugMap);
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

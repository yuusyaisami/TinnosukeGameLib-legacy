using System;
using System.Collections.Generic;
using Game.Kernel.Generation;
using Game.Kernel.IR;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class ArtifactSetPromotionTests
    {
        [Test]
        public void StageAndCommit_PromotesVerifiedPlanAtomically()
        {
            KernelIR kernelIR = CreateKernelIR();
            VerifiedArtifactHeader serviceGraph = CreateArtifact(ArtifactKind.ServiceGraph, 10, kernelIR, "1.0.0", "ServiceGraph");
            VerifiedArtifactHeader validationReport = CreateArtifact(ArtifactKind.ValidationReport, 20, kernelIR, "1.0.0", "ValidationReport");

            KernelPlanHeader verifiedHeader = CreatePlanHeader(kernelIR, new[] { ArtifactKind.ServiceGraph, ArtifactKind.ValidationReport }, serviceGraph, validationReport);
            VerifiedKernelPlan verifiedPlan = CreateVerifiedPlan(verifiedHeader, new[] { serviceGraph, validationReport });
            ArtifactSetPromotionInputs promotionInputs = CreatePromotionInputs(kernelIR);

            ArtifactSetPublicationState publicationState = ArtifactSetPublicationState.Empty;
            ArtifactSetPromotionResult stageResult = ArtifactSetPromotionTransaction.Stage(publicationState, promotionInputs, verifiedPlan);

            Assert.That(stageResult.IsSuccessful, Is.True);
            Assert.That(stageResult.IsStaged, Is.True);
            Assert.That(stageResult.StagingRecord, Is.Not.Null);
            Assert.That(stageResult.PublicationState, Is.EqualTo(publicationState));

            ArtifactSetPromotionResult commitResult = ArtifactSetPromotionTransaction.Commit(stageResult.PublicationState, stageResult.StagingRecord!);

            Assert.That(commitResult.IsSuccessful, Is.True);
            Assert.That(commitResult.IsPromoted, Is.True);
            Assert.That(commitResult.PromotedPlan, Is.EqualTo(verifiedPlan));
            Assert.That(commitResult.PublicationState.Current, Is.EqualTo(verifiedPlan));
            Assert.That(commitResult.PublicationState.Previous, Is.Null);
        }

        [Test]
        public void Commit_PreservesPreviousStateAfterSuccessfulPromotion()
        {
            KernelIR kernelIR = CreateKernelIR();
            VerifiedArtifactHeader currentServiceGraph = CreateArtifact(ArtifactKind.ServiceGraph, 10, kernelIR, "1.0.0", "CurrentServiceGraph");
            VerifiedArtifactHeader currentValidationReport = CreateArtifact(ArtifactKind.ValidationReport, 20, kernelIR, "1.0.0", "CurrentValidationReport");
            VerifiedArtifactHeader nextServiceGraph = CreateArtifact(ArtifactKind.ServiceGraph, 30, kernelIR, "1.0.0", "NextServiceGraph");
            VerifiedArtifactHeader nextValidationReport = CreateArtifact(ArtifactKind.ValidationReport, 40, kernelIR, "1.0.0", "NextValidationReport");

            KernelPlanHeader currentHeader = CreatePlanHeader(kernelIR, new[] { ArtifactKind.ServiceGraph, ArtifactKind.ValidationReport }, currentServiceGraph, currentValidationReport);
            KernelPlanHeader nextHeader = CreatePlanHeader(kernelIR, new[] { ArtifactKind.ServiceGraph, ArtifactKind.ValidationReport }, nextServiceGraph, nextValidationReport);

            VerifiedKernelPlan currentPlan = CreateVerifiedPlan(currentHeader, new[] { currentServiceGraph, currentValidationReport });
            VerifiedKernelPlan nextPlan = CreateVerifiedPlan(nextHeader, new[] { nextServiceGraph, nextValidationReport });
            ArtifactSetPromotionInputs promotionInputs = CreatePromotionInputs(kernelIR);

            ArtifactSetPublicationState publicationState = ArtifactSetPublicationState.Create(currentPlan);
            ArtifactSetPromotionResult stageResult = ArtifactSetPromotionTransaction.Stage(publicationState, promotionInputs, nextPlan);
            ArtifactSetPromotionResult commitResult = ArtifactSetPromotionTransaction.Commit(stageResult.PublicationState, stageResult.StagingRecord!);

            Assert.That(commitResult.IsSuccessful, Is.True);
            Assert.That(commitResult.PublicationState.Current, Is.EqualTo(nextPlan));
            Assert.That(commitResult.PublicationState.Previous, Is.EqualTo(currentPlan));
        }

        [Test]
        public void Commit_IsIdempotentWhenCandidateIsAlreadyCurrent()
        {
            KernelIR kernelIR = CreateKernelIR();
            VerifiedArtifactHeader previousServiceGraph = CreateArtifact(ArtifactKind.ServiceGraph, 10, kernelIR, "1.0.0", "PreviousServiceGraph");
            VerifiedArtifactHeader previousValidationReport = CreateArtifact(ArtifactKind.ValidationReport, 20, kernelIR, "1.0.0", "PreviousValidationReport");
            VerifiedArtifactHeader currentServiceGraph = CreateArtifact(ArtifactKind.ServiceGraph, 30, kernelIR, "1.0.0", "CurrentServiceGraph");
            VerifiedArtifactHeader currentValidationReport = CreateArtifact(ArtifactKind.ValidationReport, 40, kernelIR, "1.0.0", "CurrentValidationReport");

            KernelPlanHeader previousHeader = CreatePlanHeader(kernelIR, new[] { ArtifactKind.ServiceGraph, ArtifactKind.ValidationReport }, previousServiceGraph, previousValidationReport);
            KernelPlanHeader currentHeader = CreatePlanHeader(kernelIR, new[] { ArtifactKind.ServiceGraph, ArtifactKind.ValidationReport }, currentServiceGraph, currentValidationReport);

            VerifiedKernelPlan previousPlan = CreateVerifiedPlan(previousHeader, new[] { previousServiceGraph, previousValidationReport });
            VerifiedKernelPlan currentPlan = CreateVerifiedPlan(currentHeader, new[] { currentServiceGraph, currentValidationReport });
            ArtifactSetPromotionInputs promotionInputs = CreatePromotionInputs(kernelIR);

            ArtifactSetPublicationState publicationState = ArtifactSetPublicationState.Create(currentPlan, previousPlan);
            ArtifactSetPromotionResult stageResult = ArtifactSetPromotionTransaction.Stage(publicationState, promotionInputs, currentPlan);
            ArtifactSetPromotionResult commitResult = ArtifactSetPromotionTransaction.Commit(stageResult.PublicationState, stageResult.StagingRecord!);

            Assert.That(commitResult.IsSuccessful, Is.True);
            Assert.That(commitResult.PublicationState, Is.EqualTo(publicationState));
            Assert.That(commitResult.PublicationState.Current, Is.EqualTo(currentPlan));
            Assert.That(commitResult.PublicationState.Previous, Is.EqualTo(previousPlan));
        }

        [Test]
        public void Commit_RejectsPublicationStateChangesBetweenStageAndCommit()
        {
            KernelIR kernelIR = CreateKernelIR();
            VerifiedArtifactHeader baseServiceGraph = CreateArtifact(ArtifactKind.ServiceGraph, 10, kernelIR, "1.0.0", "BaseServiceGraph");
            VerifiedArtifactHeader baseValidationReport = CreateArtifact(ArtifactKind.ValidationReport, 20, kernelIR, "1.0.0", "BaseValidationReport");
            VerifiedArtifactHeader stagedServiceGraph = CreateArtifact(ArtifactKind.ServiceGraph, 30, kernelIR, "1.0.0", "StagedServiceGraph");
            VerifiedArtifactHeader stagedValidationReport = CreateArtifact(ArtifactKind.ValidationReport, 40, kernelIR, "1.0.0", "StagedValidationReport");
            VerifiedArtifactHeader liveServiceGraph = CreateArtifact(ArtifactKind.ServiceGraph, 50, kernelIR, "1.0.0", "LiveServiceGraph");
            VerifiedArtifactHeader liveValidationReport = CreateArtifact(ArtifactKind.ValidationReport, 60, kernelIR, "1.0.0", "LiveValidationReport");

            KernelPlanHeader baseHeader = CreatePlanHeader(kernelIR, new[] { ArtifactKind.ServiceGraph, ArtifactKind.ValidationReport }, baseServiceGraph, baseValidationReport);
            KernelPlanHeader stagedHeader = CreatePlanHeader(kernelIR, new[] { ArtifactKind.ServiceGraph, ArtifactKind.ValidationReport }, stagedServiceGraph, stagedValidationReport);
            KernelPlanHeader liveHeader = CreatePlanHeader(kernelIR, new[] { ArtifactKind.ServiceGraph, ArtifactKind.ValidationReport }, liveServiceGraph, liveValidationReport);

            VerifiedKernelPlan basePlan = CreateVerifiedPlan(baseHeader, new[] { baseServiceGraph, baseValidationReport });
            VerifiedKernelPlan stagedPlan = CreateVerifiedPlan(stagedHeader, new[] { stagedServiceGraph, stagedValidationReport });
            VerifiedKernelPlan livePlan = CreateVerifiedPlan(liveHeader, new[] { liveServiceGraph, liveValidationReport });
            ArtifactSetPromotionInputs promotionInputs = CreatePromotionInputs(kernelIR);

            ArtifactSetPublicationState baseState = ArtifactSetPublicationState.Create(basePlan);
            ArtifactSetPromotionResult stageResult = ArtifactSetPromotionTransaction.Stage(baseState, promotionInputs, stagedPlan);
            ArtifactSetPublicationState changedLiveState = ArtifactSetPublicationState.Create(livePlan, basePlan);

            ArtifactSetPromotionResult commitResult = ArtifactSetPromotionTransaction.Commit(changedLiveState, stageResult.StagingRecord!);

            Assert.That(commitResult.IsSuccessful, Is.False);
            Assert.That(commitResult.Issues, Has.Some.Matches<ArtifactSetPromotionIssue>(issue => issue.Code == "M4_3_PUBLICATION_STATE_CHANGED"));
            Assert.That(commitResult.PublicationState, Is.EqualTo(changedLiveState));
        }

        [TestCase("SourceHash", "M4_6_STALE_SOURCE_HASH")]
        [TestCase("RegistryHash", "M4_6_STALE_REGISTRY_HASH")]
        [TestCase("ProfileHash", "M4_6_STALE_PROFILE_HASH")]
        [TestCase("DebugMapHash", "M4_6_STALE_DEBUG_MAP_HASH")]
        [TestCase("FormatVersion", "M4_6_FORMAT_VERSION_MISMATCH")]
        [TestCase("GeneratorVersion", "M4_6_GENERATOR_VERSION_MISMATCH")]
        public void Stage_RejectsStaleHeaderMismatch(string field, string expectedCode)
        {
            KernelIR kernelIR = CreateKernelIR();
            ArtifactSetPromotionInputs baselineInputs = CreatePromotionInputs(kernelIR);
            VerifiedArtifactHeader serviceGraph = CreateArtifact(ArtifactKind.ServiceGraph, 10, kernelIR, baselineInputs.RegistryHash, baselineInputs.ProfileHash, baselineInputs.DebugMapHash, "1.0.0", "ServiceGraph");
            VerifiedArtifactHeader validationReport = CreateArtifact(ArtifactKind.ValidationReport, 20, kernelIR, baselineInputs.RegistryHash, baselineInputs.ProfileHash, baselineInputs.DebugMapHash, "1.0.0", "ValidationReport");

            KernelPlanHeader verifiedHeader = CreatePlanHeader(kernelIR, new[] { ArtifactKind.ServiceGraph, ArtifactKind.ValidationReport }, baselineInputs.RegistryHash, baselineInputs.ProfileHash, baselineInputs.DebugMapHash, serviceGraph, validationReport);
            VerifiedKernelPlan verifiedPlan = CreateVerifiedPlan(verifiedHeader, new[] { serviceGraph, validationReport });

            ArtifactSetPromotionInputs staleInputs = field switch
            {
                "SourceHash" => new ArtifactSetPromotionInputs(new Hash128(9, 9, 9, 9), baselineInputs.RegistryHash, baselineInputs.ProfileHash, baselineInputs.DebugMapHash, baselineInputs.FormatVersion, baselineInputs.GeneratorVersion),
                "RegistryHash" => new ArtifactSetPromotionInputs(baselineInputs.SourceHash, new Hash128(9, 9, 9, 9), baselineInputs.ProfileHash, baselineInputs.DebugMapHash, baselineInputs.FormatVersion, baselineInputs.GeneratorVersion),
                "ProfileHash" => new ArtifactSetPromotionInputs(baselineInputs.SourceHash, baselineInputs.RegistryHash, new Hash128(9, 9, 9, 9), baselineInputs.DebugMapHash, baselineInputs.FormatVersion, baselineInputs.GeneratorVersion),
                "DebugMapHash" => new ArtifactSetPromotionInputs(baselineInputs.SourceHash, baselineInputs.RegistryHash, baselineInputs.ProfileHash, new Hash128(9, 9, 9, 9), baselineInputs.FormatVersion, baselineInputs.GeneratorVersion),
                "FormatVersion" => new ArtifactSetPromotionInputs(baselineInputs.SourceHash, baselineInputs.RegistryHash, baselineInputs.ProfileHash, baselineInputs.DebugMapHash, baselineInputs.FormatVersion + 1, baselineInputs.GeneratorVersion),
                "GeneratorVersion" => new ArtifactSetPromotionInputs(baselineInputs.SourceHash, baselineInputs.RegistryHash, baselineInputs.ProfileHash, baselineInputs.DebugMapHash, baselineInputs.FormatVersion, baselineInputs.GeneratorVersion + ".mismatch"),
                _ => throw new ArgumentOutOfRangeException(nameof(field), field, null),
            };

            ArtifactSetPromotionResult stageResult = ArtifactSetPromotionTransaction.Stage(ArtifactSetPublicationState.Empty, staleInputs, verifiedPlan);

            Assert.That(stageResult.IsSuccessful, Is.False);
            Assert.That(stageResult.Issues, Has.Some.Matches<ArtifactSetPromotionIssue>(issue => issue.Code == expectedCode));
            Assert.That(stageResult.PublicationState, Is.EqualTo(ArtifactSetPublicationState.Empty));
        }

        [Test]
        public void StalenessDetector_RejectsIncompleteAndMissingHashStates()
        {
            KernelIR kernelIR = CreateKernelIR();
            ArtifactSetPromotionInputs promotionInputs = CreatePromotionInputs(kernelIR);
            VerifiedArtifactHeader serviceGraph = CreateArtifact(ArtifactKind.ServiceGraph, 10, kernelIR, promotionInputs.RegistryHash, promotionInputs.ProfileHash, promotionInputs.DebugMapHash, "1.0.0", "ServiceGraph");

            KernelPlanHeader header = new KernelPlanHeader(
                new PlanId(101),
                new ArtifactSetId(202),
                4,
                "1.0.0",
                new[] { ArtifactKind.ServiceGraph, ArtifactKind.ValidationReport },
                serviceGraph.SourceHash,
                promotionInputs.RegistryHash,
                promotionInputs.ProfileHash,
                default,
                default);

            GeneratedKernelPlan partialPlan = new GeneratedKernelPlan(header, new[] { serviceGraph });

            ArtifactSetStalenessReport report = ArtifactSetStalenessDetector.Evaluate(promotionInputs, partialPlan.Header, partialPlan.Artifacts);

            Assert.That(report.IsStale, Is.True);
            Assert.That(report.Issues, Has.Some.Matches<ArtifactSetStalenessIssue>(issue => issue.Code == "M4_6_GENERATED_HASH_MISSING"));
            Assert.That(report.Issues, Has.Some.Matches<ArtifactSetStalenessIssue>(issue => issue.Code == "M4_6_DEBUG_MAP_HASH_MISSING"));
            Assert.That(ContainsIssueCode(report.Issues, "M4_6_STALE_DEBUG_MAP_HASH"), Is.False);
            Assert.That(report.Issues, Has.Some.Matches<ArtifactSetStalenessIssue>(issue => issue.Code == "M4_6_ARTIFACT_SET_INCOMPLETE"));
        }

        static VerifiedKernelPlan CreateVerifiedPlan(KernelPlanHeader header, IReadOnlyList<VerifiedArtifactHeader> artifacts)
        {
            KernelPlanVerificationResult result = KernelPlanVerification.Verify(new GeneratedKernelPlan(header, artifacts));

            Assert.That(result.IsVerified, Is.True);
            Assert.That(result.VerifiedPlan, Is.Not.Null);

            return result.VerifiedPlan!;
        }

        static KernelPlanHeader CreatePlanHeader(KernelIR kernelIR, IReadOnlyList<ArtifactKind> requiredArtifactKinds, params VerifiedArtifactHeader[] artifacts)
        {
            KernelPlanHeader provisionalHeader = new KernelPlanHeader(
                new PlanId(101),
                new ArtifactSetId(202),
                4,
                "1.0.0",
                requiredArtifactKinds,
                VerifiedArtifactHeaderHashing.ComputeSourceHash(kernelIR),
                VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[] { "Registry" }),
                VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[] { "Profile" }),
                VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[] { "DebugMap" }),
                default);

            Hash128 generatedHash = ArtifactSetManifest.ComputeConsistencyHash(provisionalHeader, artifacts);

            return new KernelPlanHeader(
                provisionalHeader.PlanId,
                provisionalHeader.ArtifactSetId,
                provisionalHeader.FormatVersion,
                provisionalHeader.GeneratorVersion,
                provisionalHeader.RequiredArtifactKinds.ToArray(),
                provisionalHeader.SourceHash,
                provisionalHeader.RegistryHash,
                provisionalHeader.ProfileHash,
                provisionalHeader.DebugMapHash,
                generatedHash);
        }

        static KernelPlanHeader CreatePlanHeader(KernelIR kernelIR, IReadOnlyList<ArtifactKind> requiredArtifactKinds, Hash128 registryHash, Hash128 profileHash, Hash128 debugMapHash, params VerifiedArtifactHeader[] artifacts)
        {
            KernelPlanHeader provisionalHeader = new KernelPlanHeader(
                new PlanId(101),
                new ArtifactSetId(202),
                4,
                "1.0.0",
                requiredArtifactKinds,
                VerifiedArtifactHeaderHashing.ComputeSourceHash(kernelIR),
                registryHash,
                profileHash,
                debugMapHash,
                default);

            Hash128 generatedHash = ArtifactSetManifest.ComputeConsistencyHash(provisionalHeader, artifacts);

            return new KernelPlanHeader(
                provisionalHeader.PlanId,
                provisionalHeader.ArtifactSetId,
                provisionalHeader.FormatVersion,
                provisionalHeader.GeneratorVersion,
                provisionalHeader.RequiredArtifactKinds.ToArray(),
                provisionalHeader.SourceHash,
                provisionalHeader.RegistryHash,
                provisionalHeader.ProfileHash,
                provisionalHeader.DebugMapHash,
                generatedHash);
        }

            static ArtifactSetPromotionInputs CreatePromotionInputs(KernelIR kernelIR)
            {
                return new ArtifactSetPromotionInputs(
                VerifiedArtifactHeaderHashing.ComputeSourceHash(kernelIR),
                VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[] { "Registry" }),
                VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[] { "Profile" }),
                VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[] { "DebugMap" }),
                4,
                "1.0.0");
            }

        static bool ContainsIssueCode(IReadOnlyList<ArtifactSetStalenessIssue> issues, string code)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                if (string.Equals(issues[i].Code, code, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        static VerifiedArtifactHeader CreateArtifact(ArtifactKind kind, int artifactId, KernelIR kernelIR, Hash128 registryHash, Hash128 profileHash, Hash128 debugMapHash, string generatorVersion, string generatedToken)
        {
            return new VerifiedArtifactHeader(
                new PlanId(101),
                new ArtifactSetId(202),
                new ArtifactId(artifactId),
                kind,
                4,
                VerifiedArtifactHeaderHashing.ComputeSourceHash(kernelIR),
                registryHash,
                profileHash,
                debugMapHash,
                VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[] { generatedToken }),
                generatorVersion);
        }

        static VerifiedArtifactHeader CreateArtifact(ArtifactKind kind, int artifactId, KernelIR kernelIR, string generatorVersion, string generatedToken)
        {
            return VerifiedArtifactHeaderBuilder.Create(
                new PlanId(101),
                new ArtifactSetId(202),
                new ArtifactId(artifactId),
                kind,
                formatVersion: 4,
                kernelIR,
                new[] { "Registry" },
                new[] { "Profile" },
                new[] { "DebugMap" },
                new[] { generatedToken },
                generatorVersion);
        }

        static KernelIR CreateKernelIR()
        {
            SourceLocationTable sources = new SourceLocationTable(new[]
            {
                new SourceLocationIR(new GeneratedSourceLocation("PlanHeaderGenerator", "MinimalKernel", "Build")),
            });

            ModuleIR module = new ModuleIR(
                new ModuleId(1),
                "MinimalKernel",
                ModuleKind.Feature,
                new ModuleVersion(1),
                new ModuleAvailabilityIR(new AvailabilityIR(KernelProfileMask.Release, true, null)),
                new SourceLocationId(1));

            return new KernelIR(
                new KernelIRHeader("KernelIR-Minimal", 1, "TinnosukeGameLib", "Release", "1.0.0", new Hash128(1, 2, 3, 4), new Hash128(5, 6, 7, 8)),
                new KernelProfileIR("Release", KernelProfileMask.Release, new AvailabilityIR(KernelProfileMask.Release, true, null)),
                new[] { module },
                Array.Empty<ScopeIR>(),
                Array.Empty<ServiceIR>(),
                Array.Empty<CommandIR>(),
                Array.Empty<ValueKeyIR>(),
                Array.Empty<LifecycleIR>(),
                Array.Empty<RuntimeQueryIR>(),
                Array.Empty<DependencyEdgeIR>(),
                sources);
        }
    }
}
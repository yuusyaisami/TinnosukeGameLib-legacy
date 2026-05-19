using System;
using System.Collections.Generic;
using Game.Kernel.Generation;
using Game.Kernel.IR;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class VerifiedPlanTests
    {
        [Test]
        public void PlanTypes_AreDistinctAndVerifiedPlanRequiresVerification()
        {
            Assert.That(typeof(GeneratedKernelPlan), Is.Not.EqualTo(typeof(VerifiedKernelPlan)));
            Assert.That(typeof(KernelPlanHeader), Is.Not.Null);
            Assert.That(typeof(ArtifactSetManifest), Is.Not.Null);
        }

        [Test]
        public void Manifest_IsDeterministicAndOrderIndependent()
        {
            KernelIR kernelIR = CreateKernelIR();
            VerifiedArtifactHeader serviceGraph = CreateArtifact(
                ArtifactKind.ServiceGraph,
                artifactId: 10,
                kernelIR,
                generatorVersion: "1.0.0",
                generatedToken: "ServiceGraph");
            VerifiedArtifactHeader validationReport = CreateArtifact(
                ArtifactKind.ValidationReport,
                artifactId: 20,
                kernelIR,
                generatorVersion: "1.0.0",
                generatedToken: "ValidationReport");

            KernelPlanHeader header = CreatePlanHeader(
                kernelIR,
                new[] { ArtifactKind.ServiceGraph, ArtifactKind.ValidationReport },
                serviceGraph,
                validationReport);

            ArtifactSetManifest first = new ArtifactSetManifest(header, new[] { serviceGraph, validationReport });
            ArtifactSetManifest second = new ArtifactSetManifest(header, new[] { validationReport, serviceGraph });

            Assert.That(first.ConsistencyHash, Is.EqualTo(second.ConsistencyHash));
            Assert.That(first.Artifacts[0].ArtifactKind, Is.EqualTo(ArtifactKind.ServiceGraph));
            Assert.That(second.Artifacts[0].ArtifactKind, Is.EqualTo(ArtifactKind.ServiceGraph));
        }

        [Test]
        public void ComputeConsistencyHash_IsOrderIndependent()
        {
            KernelIR kernelIR = CreateKernelIR();
            VerifiedArtifactHeader serviceGraph = CreateArtifact(
                ArtifactKind.ServiceGraph,
                artifactId: 10,
                kernelIR,
                generatorVersion: "1.0.0",
                generatedToken: "ServiceGraph");
            VerifiedArtifactHeader validationReport = CreateArtifact(
                ArtifactKind.ValidationReport,
                artifactId: 20,
                kernelIR,
                generatorVersion: "1.0.0",
                generatedToken: "ValidationReport");

            KernelPlanHeader header = CreatePlanHeader(
                kernelIR,
                new[] { ArtifactKind.ServiceGraph, ArtifactKind.ValidationReport },
                serviceGraph,
                validationReport);

            Hash128 firstHash = ArtifactSetManifest.ComputeConsistencyHash(header, new[] { serviceGraph, validationReport });
            Hash128 secondHash = ArtifactSetManifest.ComputeConsistencyHash(header, new[] { validationReport, serviceGraph });

            Assert.That(firstHash, Is.EqualTo(secondHash));
        }

        [Test]
        public void Verification_RejectsPartialOrMismatchedSets()
        {
            KernelIR kernelIR = CreateKernelIR();
            VerifiedArtifactHeader serviceGraph = CreateArtifact(
                ArtifactKind.ServiceGraph,
                artifactId: 10,
                kernelIR,
                generatorVersion: "1.0.0",
                generatedToken: "ServiceGraph");
            VerifiedArtifactHeader validationReport = CreateArtifact(
                ArtifactKind.ValidationReport,
                artifactId: 20,
                kernelIR,
                generatorVersion: "1.0.0",
                generatedToken: "ValidationReport");

            ArtifactKind[] requiredArtifactKinds = new[]
            {
                ArtifactKind.ServiceGraph,
                ArtifactKind.ValidationReport,
            };

            KernelPlanHeader validHeader = CreatePlanHeader(kernelIR, requiredArtifactKinds, serviceGraph, validationReport);
            GeneratedKernelPlan validGeneratedPlan = new GeneratedKernelPlan(validHeader, new[] { serviceGraph, validationReport });

            KernelPlanVerificationResult validResult = KernelPlanVerification.Verify(validGeneratedPlan);

            Assert.That(validResult.IsVerified, Is.True);
            Assert.That(validResult.VerifiedPlan, Is.Not.Null);
            Assert.That(validResult.VerifiedPlan!.Manifest.Artifacts.Length, Is.EqualTo(2));

            GeneratedKernelPlan partialPlan = new GeneratedKernelPlan(validHeader, new[] { serviceGraph });
            KernelPlanVerificationResult partialResult = KernelPlanVerification.Verify(partialPlan);

            Assert.That(partialResult.IsVerified, Is.False);
            Assert.That(partialResult.Issues, Has.Some.Matches<KernelPlanVerificationIssue>(issue => issue.Code == "M4_2_ARTIFACT_SET_INCOMPLETE"));

            VerifiedArtifactHeader mismatchedDebugMap = new VerifiedArtifactHeader(
                new PlanId(101),
                new ArtifactSetId(202),
                new ArtifactId(10),
                ArtifactKind.ServiceGraph,
                4,
                serviceGraph.SourceHash,
                serviceGraph.RegistryHash,
                serviceGraph.ProfileHash,
                new Hash128(9, 9, 9, 9),
                serviceGraph.GeneratedHash,
                serviceGraph.GeneratorVersion);

            KernelPlanHeader mismatchHeader = new KernelPlanHeader(
                new PlanId(101),
                new ArtifactSetId(202),
                4,
                "1.0.0",
                new[] { ArtifactKind.ServiceGraph, ArtifactKind.ValidationReport },
                serviceGraph.SourceHash,
                serviceGraph.RegistryHash,
                serviceGraph.ProfileHash,
                serviceGraph.DebugMapHash,
                validHeader.GeneratedHash);

            GeneratedKernelPlan mismatchPlan = new GeneratedKernelPlan(mismatchHeader, new[] { mismatchedDebugMap, validationReport });
            KernelPlanVerificationResult mismatchResult = KernelPlanVerification.Verify(mismatchPlan);

            Assert.That(mismatchResult.IsVerified, Is.False);
            Assert.That(mismatchResult.Issues, Has.Some.Matches<KernelPlanVerificationIssue>(issue => issue.Code == "M4_2_ARTIFACT_INCONSISTENT" || issue.Code == "M4_2_HEADER_MISMATCH"));
        }

        [Test]
        public void Verification_RejectsZeroGeneratedHashAndNullPlan()
        {
            KernelIR kernelIR = CreateKernelIR();
            VerifiedArtifactHeader invalidHeader = new VerifiedArtifactHeader(
                new PlanId(101),
                new ArtifactSetId(202),
                new ArtifactId(10),
                ArtifactKind.ServiceGraph,
                4,
                VerifiedArtifactHeaderHashing.ComputeSourceHash(kernelIR),
                VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[] { "Registry" }),
                VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[] { "Profile" }),
                VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[] { "DebugMap" }),
                default,
                "1.0.0");

            KernelPlanHeader header = new KernelPlanHeader(
                new PlanId(101),
                new ArtifactSetId(202),
                4,
                "1.0.0",
                new[] { ArtifactKind.ServiceGraph },
                invalidHeader.SourceHash,
                invalidHeader.RegistryHash,
                invalidHeader.ProfileHash,
                invalidHeader.DebugMapHash,
                default);

            KernelPlanVerificationResult result = KernelPlanVerification.Verify(new GeneratedKernelPlan(header, new[] { invalidHeader }));

            Assert.That(result.IsVerified, Is.False);
            Assert.That(result.Issues, Has.Some.Matches<KernelPlanVerificationIssue>(issue => issue.Code == "M4_2_GENERATED_HASH_MISSING" || issue.Code == "M4_2_CONSISTENCY_HASH_MISMATCH"));
            Assert.That(KernelPlanVerification.Verify(null).IsVerified, Is.False);
        }

        [Test]
        public void GeneratedKernelPlan_RejectsNonCanonicalArtifactOrder()
        {
            KernelIR kernelIR = CreateKernelIR();
            VerifiedArtifactHeader serviceGraph = CreateArtifact(
                ArtifactKind.ServiceGraph,
                artifactId: 10,
                kernelIR,
                generatorVersion: "1.0.0",
                generatedToken: "ServiceGraph");
            VerifiedArtifactHeader validationReport = CreateArtifact(
                ArtifactKind.ValidationReport,
                artifactId: 20,
                kernelIR,
                generatorVersion: "1.0.0",
                generatedToken: "ValidationReport");

            KernelPlanHeader header = CreatePlanHeader(
                kernelIR,
                new[] { ArtifactKind.ServiceGraph, ArtifactKind.ValidationReport },
                serviceGraph,
                validationReport);

            Assert.That(() => new GeneratedKernelPlan(header, new[] { validationReport, serviceGraph }), Throws.ArgumentException);
        }

        [Test]
        public void KernelPlanHeader_RejectsNonCanonicalRequiredArtifactKindOrder()
        {
            KernelIR kernelIR = CreateKernelIR();
            Assert.That(() => new KernelPlanHeader(
                new PlanId(101),
                new ArtifactSetId(202),
                4,
                "1.0.0",
                new[] { ArtifactKind.ValidationReport, ArtifactKind.ServiceGraph },
                VerifiedArtifactHeaderHashing.ComputeSourceHash(kernelIR),
                VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[] { "Registry" }),
                VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[] { "Profile" }),
                VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[] { "DebugMap" }),
                VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[] { "ServiceGraph", "ValidationReport" })), Throws.ArgumentException);
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
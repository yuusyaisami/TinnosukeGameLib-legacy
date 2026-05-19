using System;
using Game.Kernel.Generation;
using Game.Kernel.IR;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class VerifiedArtifactHeaderTests
    {
        [Test]
        public void ArtifactKind_UsesStableExplicitValues()
        {
            Assert.That((int)ArtifactKind.Unknown, Is.EqualTo(0));
            Assert.That((int)ArtifactKind.ServiceGraph, Is.EqualTo(10));
            Assert.That((int)ArtifactKind.ScopeGraph, Is.EqualTo(20));
            Assert.That((int)ArtifactKind.LifecyclePlan, Is.EqualTo(30));
            Assert.That((int)ArtifactKind.CommandCatalog, Is.EqualTo(40));
            Assert.That((int)ArtifactKind.ValueSchema, Is.EqualTo(50));
            Assert.That((int)ArtifactKind.RuntimeQuery, Is.EqualTo(60));
            Assert.That((int)ArtifactKind.KernelDebugMap, Is.EqualTo(70));
            Assert.That((int)ArtifactKind.GenerationReport, Is.EqualTo(80));
            Assert.That((int)ArtifactKind.ValidationReport, Is.EqualTo(90));
        }

        [Test]
        public void SemanticHashHelper_IsDeterministicAndRejectsNullEntries()
        {
            Hash128 first = VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[] { "Registry:A", "Registry:B" });
            Hash128 second = VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[] { "Registry:A", "Registry:B" });
            Hash128 changed = VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[] { "Registry:A", "Registry:C" });
            Hash128 reordered = VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[] { "Registry:B", "Registry:A" });

            Assert.That(first, Is.EqualTo(second));
            Assert.That(reordered, Is.EqualTo(first));
            Assert.That(changed, Is.Not.EqualTo(first));
            Assert.That(() => VerifiedArtifactHeaderHashing.ComputeGeneratedHash(null!), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[] { "A", null! }), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void KernelIRHashHelper_UsesNormalizedKernelIRHash()
        {
            KernelIR kernelIR = CreateKernelIR();

            Assert.That(
                VerifiedArtifactHeaderHashing.ComputeSourceHash(kernelIR),
                Is.EqualTo(KernelIRHashing.ComputeNormalizedHash(kernelIR)));
        }

        [Test]
        public void HeaderBuilder_BindsAllStableFields()
        {
            KernelIR kernelIR = CreateKernelIR();

            VerifiedArtifactHeader header = VerifiedArtifactHeaderBuilder.Create(
                new PlanId(101),
                new ArtifactSetId(202),
                new ArtifactId(303),
                ArtifactKind.ServiceGraph,
                formatVersion: 4,
                kernelIR,
                new[] { "Registry:Battle" },
                new[] { "Profile:Release" },
                new[] { "DebugMap:Battle" },
                new[] { "Content:ServiceGraph" },
                generatorVersion: "1.2.3");

            Assert.That(header.PlanId, Is.EqualTo(new PlanId(101)));
            Assert.That(header.ArtifactSetId, Is.EqualTo(new ArtifactSetId(202)));
            Assert.That(header.ArtifactId, Is.EqualTo(new ArtifactId(303)));
            Assert.That(header.ArtifactKind, Is.EqualTo(ArtifactKind.ServiceGraph));
            Assert.That(header.FormatVersion, Is.EqualTo(4));
            Assert.That(header.SourceHash, Is.EqualTo(KernelIRHashing.ComputeNormalizedHash(kernelIR)));
            Assert.That(header.RegistryHash, Is.EqualTo(VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[] { "Registry:Battle" })));
            Assert.That(header.ProfileHash, Is.EqualTo(VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[] { "Profile:Release" })));
            Assert.That(header.DebugMapHash, Is.EqualTo(VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[] { "DebugMap:Battle" })));
            Assert.That(header.GeneratedHash, Is.EqualTo(VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[] { "Content:ServiceGraph" })));
            Assert.That(header.GeneratorVersion, Is.EqualTo("1.2.3"));
        }

        [Test]
        public void HeaderBuilder_ChangesHashesWhenSemanticInputsChange()
        {
            KernelIR kernelIR = CreateKernelIR();

            VerifiedArtifactHeader baseline = VerifiedArtifactHeaderBuilder.Create(
                new PlanId(101),
                new ArtifactSetId(202),
                new ArtifactId(303),
                ArtifactKind.ServiceGraph,
                formatVersion: 4,
                kernelIR,
                new[] { "Registry:Battle" },
                new[] { "Profile:Release" },
                new[] { "DebugMap:Battle" },
                new[] { "Content:ServiceGraph" },
                generatorVersion: "1.2.3");

            VerifiedArtifactHeader changed = VerifiedArtifactHeaderBuilder.Create(
                new PlanId(101),
                new ArtifactSetId(202),
                new ArtifactId(303),
                ArtifactKind.ServiceGraph,
                formatVersion: 4,
                kernelIR,
                new[] { "Registry:Battle" },
                new[] { "Profile:Development" },
                new[] { "DebugMap:Battle" },
                new[] { "Content:ServiceGraph" },
                generatorVersion: "1.2.3");

            Assert.That(changed.ProfileHash, Is.Not.EqualTo(baseline.ProfileHash));
            Assert.That(changed, Is.Not.EqualTo(baseline));
        }

        [Test]
        public void Header_RejectsInvalidIdentityAndUnknownKind()
        {
            Hash128 hash = new Hash128(1, 2, 3, 4);

            Assert.That(
                () => new VerifiedArtifactHeader(
                    new PlanId(0),
                    new ArtifactSetId(1),
                    new ArtifactId(1),
                    ArtifactKind.ServiceGraph,
                    1,
                    hash,
                    hash,
                    hash,
                    hash,
                    hash,
                    "1.0.0"),
                Throws.TypeOf<ArgumentOutOfRangeException>());

            Assert.That(
                () => new VerifiedArtifactHeader(
                    new PlanId(1),
                    new ArtifactSetId(0),
                    new ArtifactId(1),
                    ArtifactKind.ServiceGraph,
                    1,
                    hash,
                    hash,
                    hash,
                    hash,
                    hash,
                    "1.0.0"),
                Throws.TypeOf<ArgumentOutOfRangeException>());

            Assert.That(
                () => new VerifiedArtifactHeader(
                    new PlanId(1),
                    new ArtifactSetId(1),
                    new ArtifactId(0),
                    ArtifactKind.ServiceGraph,
                    1,
                    hash,
                    hash,
                    hash,
                    hash,
                    hash,
                    "1.0.0"),
                Throws.TypeOf<ArgumentOutOfRangeException>());

            Assert.That(
                () => new VerifiedArtifactHeader(
                    new PlanId(1),
                    new ArtifactSetId(1),
                    new ArtifactId(1),
                    ArtifactKind.Unknown,
                    1,
                    hash,
                    hash,
                    hash,
                    hash,
                    hash,
                    "1.0.0"),
                Throws.TypeOf<ArgumentOutOfRangeException>());

            Assert.That(
                () => new VerifiedArtifactHeader(
                    new PlanId(1),
                    new ArtifactSetId(1),
                    new ArtifactId(1),
                    ArtifactKind.ServiceGraph,
                    0,
                    hash,
                    hash,
                    hash,
                    hash,
                    hash,
                    "1.0.0"),
                Throws.TypeOf<ArgumentOutOfRangeException>());

            Assert.That(
                () => new VerifiedArtifactHeader(
                    new PlanId(1),
                    new ArtifactSetId(1),
                    new ArtifactId(1),
                    ArtifactKind.ServiceGraph,
                    1,
                    hash,
                    hash,
                    hash,
                    hash,
                    hash,
                    " "),
                Throws.ArgumentException);
        }

        static KernelIR CreateKernelIR()
        {
            SourceLocationTable sources = new SourceLocationTable(new[]
            {
                new SourceLocationIR(new GeneratedSourceLocation("ArtifactHeaderGenerator", "MinimalKernel", "Build")),
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
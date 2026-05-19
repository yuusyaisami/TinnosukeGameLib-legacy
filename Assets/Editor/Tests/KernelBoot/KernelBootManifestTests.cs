#nullable enable
using System;
using Game.Kernel.Abstractions;
using Game.Kernel.Boot;
using Game.Kernel.Generation;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class KernelBootManifestTests
    {
        [Test]
        public void ManifestId_UsesStableExplicitValues()
        {
            ManifestId first = new ManifestId(17);
            ManifestId same = new ManifestId(17);
            ManifestId different = new ManifestId(21);

            Assert.That(first, Is.EqualTo(same));
            Assert.That(first, Is.Not.EqualTo(different));
            Assert.That(first.GetHashCode(), Is.EqualTo(same.GetHashCode()));
            Assert.That(first.ToString(), Is.EqualTo("ManifestId(17)"));
        }

        [Test]
        public void VerifiedArtifactSetRef_UsesStableValueSemantics()
        {
            VerifiedArtifactSetRef first = CreateArtifactSetRef();
            VerifiedArtifactSetRef same = CreateArtifactSetRef();
            VerifiedArtifactSetRef different = new VerifiedArtifactSetRef(
                new ArtifactSetId(21),
                new PlanId(31),
                new UnityEngine.Hash128(9, 8, 7, 6).ToString(),
                new UnityEngine.Hash128(5, 4, 3, 2).ToString(),
                11,
                new UnityEngine.Hash128(1, 1, 1, 1).ToString(),
                null);

            Assert.That(first, Is.EqualTo(same));
            Assert.That(first, Is.Not.EqualTo(different));
            Assert.That(first.GetHashCode(), Is.EqualTo(same.GetHashCode()));
            Assert.That(first.ToString(), Does.Contain("ArtifactSetId(11)"));
            Assert.That(first.IsValid, Is.True);
        }

        [Test]
        public void KernelBootManifest_CapturesSelectionState()
        {
            VerifiedArtifactSetRef artifactSet = CreateArtifactSetRef();
            BootDiagnosticsPolicy diagnosticsPolicy = BootDiagnosticsPolicy.ForKind(KernelProfileKind.Test);
            KernelBootManifest manifest = new KernelBootManifest(
                new ManifestId(5),
                new KernelProfileId(7),
                artifactSet,
                new BootPolicyId(9),
                diagnosticsPolicy);

            Assert.That(manifest.ManifestId, Is.EqualTo(new ManifestId(5)));
            Assert.That(manifest.ProfileId, Is.EqualTo(new KernelProfileId(7)));
            Assert.That(manifest.ArtifactSet, Is.EqualTo(artifactSet));
            Assert.That(manifest.BootPolicyId, Is.EqualTo(new BootPolicyId(9)));
            Assert.That(manifest.DiagnosticsPolicy, Is.EqualTo(diagnosticsPolicy));
            Assert.That(manifest.ToString(), Does.Contain("ManifestId(5)"));
        }

        [Test]
        public void KernelBootManifestHashing_IsDeterministicForEquivalentData()
        {
            KernelBootManifest first = CreateManifest();
            KernelBootManifest same = CreateManifest();

            Assert.That(KernelBootManifestHashing.ComputeManifestHash(first), Is.EqualTo(KernelBootManifestHashing.ComputeManifestHash(same)));
            Assert.That(KernelBootManifestHashing.ComputeVerifiedArtifactSetRefHash(first.ArtifactSet), Is.EqualTo(KernelBootManifestHashing.ComputeVerifiedArtifactSetRefHash(same.ArtifactSet)));
        }

        [Test]
        public void KernelBootManifest_RejectsInvalidSelectionState()
        {
            VerifiedArtifactSetRef artifactSet = CreateArtifactSetRef();
            BootDiagnosticsPolicy diagnosticsPolicy = BootDiagnosticsPolicy.ForKind(KernelProfileKind.Release);

            Assert.That(
                () => new KernelBootManifest(default, new KernelProfileId(7), artifactSet, new BootPolicyId(9), diagnosticsPolicy),
                Throws.TypeOf<ArgumentOutOfRangeException>());

            Assert.That(
                () => new KernelBootManifest(new ManifestId(5), default, artifactSet, new BootPolicyId(9), diagnosticsPolicy),
                Throws.TypeOf<ArgumentOutOfRangeException>());

            Assert.That(
                () => new KernelBootManifest(new ManifestId(5), new KernelProfileId(7), default, new BootPolicyId(9), diagnosticsPolicy),
                Throws.TypeOf<ArgumentException>());

            Assert.That(
                () => new KernelBootManifest(new ManifestId(5), new KernelProfileId(7), artifactSet, default, diagnosticsPolicy),
                Throws.TypeOf<ArgumentOutOfRangeException>());

            Assert.That(
                () => new KernelBootManifest(new ManifestId(5), new KernelProfileId(7), artifactSet, new BootPolicyId(9), null!),
                Throws.ArgumentNullException);
        }

        [Test]
        public void VerifiedArtifactSetRef_RejectsInvalidCompatibilityData()
        {
            string kernelIRHash = new UnityEngine.Hash128(1, 2, 3, 4).ToString();
            string profileHash = new UnityEngine.Hash128(5, 6, 7, 8).ToString();
            string zeroHash = new string('0', 32);

            Assert.That(
                () => new VerifiedArtifactSetRef(default, new PlanId(31), kernelIRHash, profileHash, 11),
                Throws.TypeOf<ArgumentOutOfRangeException>());

            Assert.That(
                () => new VerifiedArtifactSetRef(new ArtifactSetId(11), default, kernelIRHash, profileHash, 11),
                Throws.TypeOf<ArgumentOutOfRangeException>());

            Assert.That(
                () => new VerifiedArtifactSetRef(new ArtifactSetId(11), new PlanId(31), default, profileHash, 11),
                Throws.TypeOf<ArgumentException>());

            Assert.That(
                () => new VerifiedArtifactSetRef(new ArtifactSetId(11), new PlanId(31), kernelIRHash, default, 11),
                Throws.TypeOf<ArgumentException>());

            Assert.That(
                () => new VerifiedArtifactSetRef(new ArtifactSetId(11), new PlanId(31), kernelIRHash, profileHash, 0),
                Throws.TypeOf<ArgumentOutOfRangeException>());

            Assert.That(
                () => new VerifiedArtifactSetRef(new ArtifactSetId(11), new PlanId(31), zeroHash, profileHash, 11),
                Throws.TypeOf<ArgumentException>());

            Assert.That(
                () => new VerifiedArtifactSetRef(new ArtifactSetId(11), new PlanId(31), kernelIRHash, profileHash, 11, zeroHash, null),
                Throws.TypeOf<ArgumentException>());
        }

        static VerifiedArtifactSetRef CreateArtifactSetRef()
        {
            return new VerifiedArtifactSetRef(
                new ArtifactSetId(11),
                new PlanId(31),
                new UnityEngine.Hash128(1, 2, 3, 4).ToString(),
                new UnityEngine.Hash128(5, 6, 7, 8).ToString(),
                11,
                new UnityEngine.Hash128(9, 9, 9, 9).ToString(),
                new UnityEngine.Hash128(10, 10, 10, 10).ToString());
        }

        static KernelBootManifest CreateManifest()
        {
            return new KernelBootManifest(
                new ManifestId(5),
                new KernelProfileId(7),
                CreateArtifactSetRef(),
                new BootPolicyId(9),
                BootDiagnosticsPolicy.ForKind(KernelProfileKind.Test));
        }
    }
}
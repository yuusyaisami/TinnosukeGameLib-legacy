#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Game.Kernel.IR;

namespace Game.Kernel.Generation
{
    public enum ArtifactKind
    {
        Unknown = 0,
        ServiceGraph = 10,
        ScopeGraph = 20,
        EntityRegistration = 25,
        ServiceRegistration = 26,
        EntityServiceRoute = 27,
        LifecyclePlan = 30,
        CommandCatalog = 40,
        CommandExecutorTable = 45,
        ValueSchema = 50,
        RuntimeQuery = 60,
        KernelDebugMap = 70,
        GenerationReport = 80,
        ValidationReport = 90,
    }

    public readonly struct PlanId : IEquatable<PlanId>
    {
        public PlanId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(PlanId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is PlanId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return "PlanId(" + Value + ")";
        }

        public static bool operator ==(PlanId left, PlanId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PlanId left, PlanId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct ArtifactSetId : IEquatable<ArtifactSetId>
    {
        public ArtifactSetId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(ArtifactSetId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is ArtifactSetId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return "ArtifactSetId(" + Value + ")";
        }

        public static bool operator ==(ArtifactSetId left, ArtifactSetId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ArtifactSetId left, ArtifactSetId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct ArtifactId : IEquatable<ArtifactId>
    {
        public ArtifactId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(ArtifactId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is ArtifactId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return "ArtifactId(" + Value + ")";
        }

        public static bool operator ==(ArtifactId left, ArtifactId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ArtifactId left, ArtifactId right)
        {
            return !left.Equals(right);
        }
    }

    public sealed class VerifiedArtifactHeader : IEquatable<VerifiedArtifactHeader>
    {
        public VerifiedArtifactHeader(
            PlanId planId,
            ArtifactSetId artifactSetId,
            ArtifactId artifactId,
            ArtifactKind artifactKind,
            int formatVersion,
            Hash128 sourceHash,
            Hash128 registryHash,
            Hash128 profileHash,
            Hash128 debugMapHash,
            Hash128 generatedHash,
            string generatorVersion)
        {
            if (planId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(planId), planId.Value, "Artifact headers must provide a positive plan identity.");

            if (artifactSetId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(artifactSetId), artifactSetId.Value, "Artifact headers must provide a positive artifact set identity.");

            if (artifactId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(artifactId), artifactId.Value, "Artifact headers must provide a positive artifact identity.");

            if (artifactKind == ArtifactKind.Unknown)
                throw new ArgumentOutOfRangeException(nameof(artifactKind), artifactKind, "Artifact headers must provide a concrete artifact kind.");

            if (formatVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(formatVersion), formatVersion, "Artifact headers must provide a positive format version.");

            if (sourceHash.IsZero)
                throw new ArgumentException("Artifact headers must provide a non-zero source hash.", nameof(sourceHash));

            if (registryHash.IsZero)
                throw new ArgumentException("Artifact headers must provide a non-zero registry hash.", nameof(registryHash));

            if (profileHash.IsZero)
                throw new ArgumentException("Artifact headers must provide a non-zero profile hash.", nameof(profileHash));

            if (debugMapHash.IsZero)
                throw new ArgumentException("Artifact headers must provide a non-zero debug map hash.", nameof(debugMapHash));

            if (generatedHash.IsZero)
                throw new ArgumentException("Artifact headers must provide a non-zero generated hash.", nameof(generatedHash));

            if (string.IsNullOrWhiteSpace(generatorVersion))
                throw new ArgumentException("Artifact headers must provide a generator version.", nameof(generatorVersion));

            PlanId = planId;
            ArtifactSetId = artifactSetId;
            ArtifactId = artifactId;
            ArtifactKind = artifactKind;
            FormatVersion = formatVersion;
            SourceHash = sourceHash;
            RegistryHash = registryHash;
            ProfileHash = profileHash;
            DebugMapHash = debugMapHash;
            GeneratedHash = generatedHash;
            GeneratorVersion = generatorVersion;
        }

        public PlanId PlanId { get; }

        public ArtifactSetId ArtifactSetId { get; }

        public ArtifactId ArtifactId { get; }

        public ArtifactKind ArtifactKind { get; }

        public int FormatVersion { get; }

        public Hash128 SourceHash { get; }

        public Hash128 RegistryHash { get; }

        public Hash128 ProfileHash { get; }

        public Hash128 DebugMapHash { get; }

        public Hash128 GeneratedHash { get; }

        public string GeneratorVersion { get; }

        public bool Equals(VerifiedArtifactHeader? other)
        {
            if (other == null)
                return false;

            return PlanId == other.PlanId
                && ArtifactSetId == other.ArtifactSetId
                && ArtifactId == other.ArtifactId
                && ArtifactKind == other.ArtifactKind
                && FormatVersion == other.FormatVersion
                && SourceHash == other.SourceHash
                && RegistryHash == other.RegistryHash
                && ProfileHash == other.ProfileHash
                && DebugMapHash == other.DebugMapHash
                && GeneratedHash == other.GeneratedHash
                && StringComparer.Ordinal.Equals(GeneratorVersion, other.GeneratorVersion);
        }

        public override bool Equals(object? obj)
        {
            return obj is VerifiedArtifactHeader other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = PlanId.GetHashCode();
                hash = (hash * 397) ^ ArtifactSetId.GetHashCode();
                hash = (hash * 397) ^ ArtifactId.GetHashCode();
                hash = (hash * 397) ^ (int)ArtifactKind;
                hash = (hash * 397) ^ FormatVersion;
                hash = (hash * 397) ^ SourceHash.GetHashCode();
                hash = (hash * 397) ^ RegistryHash.GetHashCode();
                hash = (hash * 397) ^ ProfileHash.GetHashCode();
                hash = (hash * 397) ^ DebugMapHash.GetHashCode();
                hash = (hash * 397) ^ GeneratedHash.GetHashCode();
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(GeneratorVersion);
                return hash;
            }
        }

        public static bool operator ==(VerifiedArtifactHeader? left, VerifiedArtifactHeader? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(VerifiedArtifactHeader? left, VerifiedArtifactHeader? right)
        {
            return !Equals(left, right);
        }
    }

    public static class VerifiedArtifactHeaderBuilder
    {
        public static VerifiedArtifactHeader Create(
            PlanId planId,
            ArtifactSetId artifactSetId,
            ArtifactId artifactId,
            ArtifactKind artifactKind,
            int formatVersion,
            KernelIR kernelIR,
            IReadOnlyList<string> registrySemanticTokens,
            IReadOnlyList<string> profileSemanticTokens,
            IReadOnlyList<string> debugMapSemanticTokens,
            IReadOnlyList<string> generatedContentSemanticTokens,
            string generatorVersion)
        {
            if (kernelIR == null)
                throw new ArgumentNullException(nameof(kernelIR));

            return new VerifiedArtifactHeader(
                planId,
                artifactSetId,
                artifactId,
                artifactKind,
                formatVersion,
                VerifiedArtifactHeaderHashing.ComputeSourceHash(kernelIR),
                VerifiedArtifactHeaderHashing.ComputeGeneratedHash(registrySemanticTokens),
                VerifiedArtifactHeaderHashing.ComputeGeneratedHash(profileSemanticTokens),
                VerifiedArtifactHeaderHashing.ComputeGeneratedHash(debugMapSemanticTokens),
                VerifiedArtifactHeaderHashing.ComputeGeneratedHash(generatedContentSemanticTokens),
                generatorVersion);
        }
    }

    public static class VerifiedArtifactHeaderHashing
    {
        public static Hash128 ComputeSourceHash(KernelIR kernelIR)
        {
            if (kernelIR == null)
                throw new ArgumentNullException(nameof(kernelIR));

            return KernelIRHashing.ComputeNormalizedHash(kernelIR);
        }

        public static Hash128 ComputeGeneratedHash(IReadOnlyList<string> semanticTokens)
        {
            if (semanticTokens == null)
                throw new ArgumentNullException(nameof(semanticTokens));

            string[] orderedTokens = new string[semanticTokens.Count];
            for (int i = 0; i < semanticTokens.Count; i++)
            {
                orderedTokens[i] = semanticTokens[i] ?? throw new ArgumentException("Semantic token collections must not contain null items.", nameof(semanticTokens));
            }

            Array.Sort(orderedTokens, StringComparer.Ordinal);

            using MemoryStream stream = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(orderedTokens.Length);
                for (int i = 0; i < orderedTokens.Length; i++)
                {
                    writer.Write(orderedTokens[i]);
                }
            }

            using SHA256 sha256 = SHA256.Create();
            byte[] digest = sha256.ComputeHash(stream.ToArray());
            byte[] truncated = new byte[16];
            Array.Copy(digest, truncated, truncated.Length);
            return Hash128Serialization.FromBytes(truncated);
        }

        public static Hash128 ComputeKernelIRHash(KernelIR kernelIR)
        {
            return ComputeSourceHash(kernelIR);
        }

        public static Hash128 ComputeSemanticHash(IReadOnlyList<string> semanticTokens)
        {
            return ComputeGeneratedHash(semanticTokens);
        }
    }
}

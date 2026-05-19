#nullable enable
using System;
using System.Collections.Generic;
using Game.Kernel.IR;
using System.Text;

namespace Game.Kernel.Generation
{
    public sealed class KernelPlanHeader : IEquatable<KernelPlanHeader>
    {
        readonly ArtifactKind[] requiredArtifactKinds;

        public KernelPlanHeader(
            PlanId planId,
            ArtifactSetId artifactSetId,
            int formatVersion,
            string generatorVersion,
            IReadOnlyList<ArtifactKind> requiredArtifactKinds,
            Hash128 sourceHash,
            Hash128 registryHash,
            Hash128 profileHash,
            Hash128 debugMapHash,
            Hash128 generatedHash)
        {
            if (planId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(planId), planId.Value, "Kernel plan headers must provide a positive plan identity.");

            if (artifactSetId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(artifactSetId), artifactSetId.Value, "Kernel plan headers must provide a positive artifact set identity.");

            if (formatVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(formatVersion), formatVersion, "Kernel plan headers must provide a positive format version.");

            if (string.IsNullOrWhiteSpace(generatorVersion))
                throw new ArgumentException("Kernel plan headers must provide a generator version.", nameof(generatorVersion));

            this.requiredArtifactKinds = CloneArtifactKinds(requiredArtifactKinds, nameof(requiredArtifactKinds));
            if (!KernelPlanVerification.IsCanonicalRequiredArtifactKindOrder(this.requiredArtifactKinds))
                throw new ArgumentException("Kernel plan headers must declare required artifact kinds in deterministic canonical order.", nameof(requiredArtifactKinds));

            PlanId = planId;
            ArtifactSetId = artifactSetId;
            FormatVersion = formatVersion;
            GeneratorVersion = generatorVersion;
            SourceHash = sourceHash;
            RegistryHash = registryHash;
            ProfileHash = profileHash;
            DebugMapHash = debugMapHash;
            GeneratedHash = generatedHash;
        }

        public PlanId PlanId { get; }

        public ArtifactSetId ArtifactSetId { get; }

        public int FormatVersion { get; }

        public string GeneratorVersion { get; }

        public ReadOnlySpan<ArtifactKind> RequiredArtifactKinds => requiredArtifactKinds;

        public Hash128 SourceHash { get; }

        public Hash128 RegistryHash { get; }

        public Hash128 ProfileHash { get; }

        public Hash128 DebugMapHash { get; }

        public Hash128 GeneratedHash { get; }

        public bool Equals(KernelPlanHeader? other)
        {
            if (other == null)
                return false;

            return PlanId == other.PlanId
                && ArtifactSetId == other.ArtifactSetId
                && FormatVersion == other.FormatVersion
                && StringComparer.Ordinal.Equals(GeneratorVersion, other.GeneratorVersion)
                && RequiredArtifactKindsEqual(requiredArtifactKinds, other.requiredArtifactKinds)
                && SourceHash == other.SourceHash
                && RegistryHash == other.RegistryHash
                && ProfileHash == other.ProfileHash
                && DebugMapHash == other.DebugMapHash
                && GeneratedHash == other.GeneratedHash;
        }

        public override bool Equals(object? obj)
        {
            return obj is KernelPlanHeader other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = PlanId.GetHashCode();
                hash = (hash * 397) ^ ArtifactSetId.GetHashCode();
                hash = (hash * 397) ^ FormatVersion;
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(GeneratorVersion);
                for (int i = 0; i < requiredArtifactKinds.Length; i++)
                {
                    hash = (hash * 397) ^ requiredArtifactKinds[i].GetHashCode();
                }

                hash = (hash * 397) ^ SourceHash.GetHashCode();
                hash = (hash * 397) ^ RegistryHash.GetHashCode();
                hash = (hash * 397) ^ ProfileHash.GetHashCode();
                hash = (hash * 397) ^ DebugMapHash.GetHashCode();
                hash = (hash * 397) ^ GeneratedHash.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(KernelPlanHeader? left, KernelPlanHeader? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(KernelPlanHeader? left, KernelPlanHeader? right)
        {
            return !Equals(left, right);
        }

        static ArtifactKind[] CloneArtifactKinds(IReadOnlyList<ArtifactKind> source, string parameterName)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (source.Count == 0)
                throw new ArgumentException("Kernel plan headers must declare at least one required artifact kind.", parameterName);

            ArtifactKind[] clone = new ArtifactKind[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                ArtifactKind artifactKind = source[i];
                if (artifactKind == ArtifactKind.Unknown)
                    throw new ArgumentException("Kernel plan headers must not include unknown artifact kinds.", parameterName);

                clone[i] = artifactKind;
            }

            for (int i = 1; i < clone.Length; i++)
            {
                if (clone[i] == clone[i - 1])
                    throw new ArgumentException("Kernel plan headers must not contain duplicate required artifact kinds.", parameterName);
            }

            return clone;
        }

        static bool RequiredArtifactKindsEqual(ReadOnlySpan<ArtifactKind> left, ReadOnlySpan<ArtifactKind> right)
        {
            if (left.Length != right.Length)
                return false;

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                    return false;
            }

            return true;
        }
    }

    public static class KernelPlanHeaderBuilder
    {
        public static KernelPlanHeader Create(
            PlanId planId,
            ArtifactSetId artifactSetId,
            int formatVersion,
            string generatorVersion,
            IReadOnlyList<ArtifactKind> requiredArtifactKinds,
            KernelIR kernelIR,
            IReadOnlyList<string> registrySemanticTokens,
            IReadOnlyList<string> profileSemanticTokens,
            IReadOnlyList<string> debugMapSemanticTokens,
            IReadOnlyList<string> generatedContentSemanticTokens)
        {
            if (kernelIR == null)
                throw new ArgumentNullException(nameof(kernelIR));

            return new KernelPlanHeader(
                planId,
                artifactSetId,
                formatVersion,
                generatorVersion,
                requiredArtifactKinds,
                VerifiedArtifactHeaderHashing.ComputeSourceHash(kernelIR),
                VerifiedArtifactHeaderHashing.ComputeGeneratedHash(registrySemanticTokens),
                VerifiedArtifactHeaderHashing.ComputeGeneratedHash(profileSemanticTokens),
                VerifiedArtifactHeaderHashing.ComputeGeneratedHash(debugMapSemanticTokens),
                VerifiedArtifactHeaderHashing.ComputeGeneratedHash(generatedContentSemanticTokens));
        }
    }

    public sealed class GeneratedKernelPlan : IEquatable<GeneratedKernelPlan>
    {
        readonly VerifiedArtifactHeader[] artifacts;

        public GeneratedKernelPlan(KernelPlanHeader header, IReadOnlyList<VerifiedArtifactHeader> artifacts)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));

            if (artifacts == null)
                throw new ArgumentNullException(nameof(artifacts));

            this.artifacts = CloneArtifacts(artifacts, nameof(artifacts));
            if (!KernelPlanVerification.IsCanonicalArtifactOrder(this.artifacts))
                throw new ArgumentException("GeneratedKernelPlan artifacts must be in deterministic canonical order.", nameof(artifacts));
        }

        public KernelPlanHeader Header { get; }

        public ReadOnlySpan<VerifiedArtifactHeader> Artifacts => artifacts;

        public bool Equals(GeneratedKernelPlan? other)
        {
            if (other == null)
                return false;

            if (!Header.Equals(other.Header) || artifacts.Length != other.artifacts.Length)
                return false;

            for (int i = 0; i < artifacts.Length; i++)
            {
                if (!artifacts[i].Equals(other.artifacts[i]))
                    return false;
            }

            return true;
        }

        public override bool Equals(object? obj)
        {
            return obj is GeneratedKernelPlan other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Header.GetHashCode();
                for (int i = 0; i < artifacts.Length; i++)
                {
                    hash = (hash * 397) ^ artifacts[i].GetHashCode();
                }

                return hash;
            }
        }

        public static bool operator ==(GeneratedKernelPlan? left, GeneratedKernelPlan? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(GeneratedKernelPlan? left, GeneratedKernelPlan? right)
        {
            return !Equals(left, right);
        }

        internal static VerifiedArtifactHeader[] CloneArtifacts(IReadOnlyList<VerifiedArtifactHeader> source, string parameterName)
        {
            VerifiedArtifactHeader[] clone = new VerifiedArtifactHeader[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                clone[i] = source[i] ?? throw new ArgumentException("Artifact collections must not contain null items.", parameterName);
            }

            return clone;
        }

        internal static VerifiedArtifactHeader[] CloneAndSortArtifacts(IReadOnlyList<VerifiedArtifactHeader> source, string parameterName)
        {
            VerifiedArtifactHeader[] clone = new VerifiedArtifactHeader[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                clone[i] = source[i] ?? throw new ArgumentException("Artifact collections must not contain null items.", parameterName);
            }

            Array.Sort(clone, CompareArtifacts);
            return clone;
        }

        internal static int CompareArtifacts(VerifiedArtifactHeader left, VerifiedArtifactHeader right)
        {
            int comparison = left.ArtifactKind.CompareTo(right.ArtifactKind);
            if (comparison != 0)
                return comparison;

            comparison = left.ArtifactId.Value.CompareTo(right.ArtifactId.Value);
            if (comparison != 0)
                return comparison;

            comparison = StringComparer.Ordinal.Compare(left.GeneratorVersion, right.GeneratorVersion);
            if (comparison != 0)
                return comparison;

            return StringComparer.Ordinal.Compare(left.GeneratedHash.ToString(), right.GeneratedHash.ToString());
        }
    }

    public sealed class ArtifactSetManifest : IEquatable<ArtifactSetManifest>
    {
        readonly VerifiedArtifactHeader[] artifacts;

        public ArtifactSetManifest(KernelPlanHeader header, IReadOnlyList<VerifiedArtifactHeader> artifacts)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));

            if (artifacts == null)
                throw new ArgumentNullException(nameof(artifacts));

            if (artifacts.Count == 0)
                throw new ArgumentException("Artifact set manifests must contain at least one artifact.", nameof(artifacts));

            this.artifacts = GeneratedKernelPlan.CloneAndSortArtifacts(artifacts, nameof(artifacts));
            ValidateConsistency();
            ConsistencyHash = ComputeConsistencyHash(Header, this.artifacts);
        }

        public KernelPlanHeader Header { get; }

        public ReadOnlySpan<VerifiedArtifactHeader> Artifacts => artifacts;

        public Hash128 ConsistencyHash { get; }

        public bool Equals(ArtifactSetManifest? other)
        {
            if (other == null)
                return false;

            if (!Header.Equals(other.Header) || !ConsistencyHash.Equals(other.ConsistencyHash) || artifacts.Length != other.artifacts.Length)
                return false;

            for (int i = 0; i < artifacts.Length; i++)
            {
                if (!artifacts[i].Equals(other.artifacts[i]))
                    return false;
            }

            return true;
        }

        public override bool Equals(object? obj)
        {
            return obj is ArtifactSetManifest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Header.GetHashCode();
                hash = (hash * 397) ^ ConsistencyHash.GetHashCode();
                for (int i = 0; i < artifacts.Length; i++)
                {
                    hash = (hash * 397) ^ artifacts[i].GetHashCode();
                }

                return hash;
            }
        }

        public static bool operator ==(ArtifactSetManifest? left, ArtifactSetManifest? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(ArtifactSetManifest? left, ArtifactSetManifest? right)
        {
            return !Equals(left, right);
        }

        internal void ValidateConsistency()
        {
            HashSet<int> seenArtifactIds = new HashSet<int>();
            HashSet<ArtifactKind> seenArtifactKinds = new HashSet<ArtifactKind>();
            for (int index = 0; index < artifacts.Length; index++)
            {
                VerifiedArtifactHeader artifact = artifacts[index];

                if (artifact.PlanId != Header.PlanId)
                    throw new ArgumentException("Artifact set manifests must use a single PlanId.", nameof(artifacts));

                if (artifact.ArtifactSetId != Header.ArtifactSetId)
                    throw new ArgumentException("Artifact set manifests must use a single ArtifactSetId.", nameof(artifacts));

                if (artifact.FormatVersion != Header.FormatVersion)
                    throw new ArgumentException("Artifact set manifests must use a compatible format version.", nameof(artifacts));

                if (artifact.SourceHash != Header.SourceHash)
                    throw new ArgumentException("Artifact set manifests must share a single SourceHash.", nameof(artifacts));

                if (artifact.RegistryHash != Header.RegistryHash)
                    throw new ArgumentException("Artifact set manifests must share a single RegistryHash.", nameof(artifacts));

                if (artifact.ProfileHash != Header.ProfileHash)
                    throw new ArgumentException("Artifact set manifests must share a single ProfileHash.", nameof(artifacts));

                if (artifact.DebugMapHash != Header.DebugMapHash)
                    throw new ArgumentException("Artifact set manifests must share a single DebugMapHash.", nameof(artifacts));

                if (!ContainsArtifactKind(Header.RequiredArtifactKinds, artifact.ArtifactKind))
                    throw new ArgumentException("Artifact set manifests must contain only required artifact kinds.", nameof(artifacts));

                if (!seenArtifactKinds.Add(artifact.ArtifactKind))
                    throw new ArgumentException("Artifact set manifests must not contain duplicate artifact kinds.", nameof(artifacts));

                if (artifact.GeneratedHash.IsZero)
                    throw new ArgumentException("Artifact set manifests must not contain zero generated hashes.", nameof(artifacts));

                if (!seenArtifactIds.Add(artifact.ArtifactId.Value))
                    throw new ArgumentException("Artifact set manifests must not contain duplicate artifact identities.", nameof(artifacts));
            }

            if (seenArtifactKinds.Count != Header.RequiredArtifactKinds.Length)
                throw new ArgumentException("Artifact set manifests must cover every required artifact kind.", nameof(artifacts));
        }

        public static Hash128 ComputeConsistencyHash(KernelPlanHeader header, ReadOnlySpan<VerifiedArtifactHeader> artifacts)
        {
            if (header == null)
                throw new ArgumentNullException(nameof(header));

            VerifiedArtifactHeader[] orderedArtifacts = GeneratedKernelPlan.CloneAndSortArtifacts(artifacts.ToArray(), nameof(artifacts));

            List<string> tokens = new List<string>(artifacts.Length + 1)
            {
                BuildPlanToken(header),
            };

            for (int i = 0; i < orderedArtifacts.Length; i++)
            {
                VerifiedArtifactHeader artifact = orderedArtifacts[i];
                tokens.Add(BuildArtifactToken(artifact));
            }

            return VerifiedArtifactHeaderHashing.ComputeGeneratedHash(tokens);
        }

        static string BuildPlanToken(KernelPlanHeader header)
        {
            StringBuilder builder = new StringBuilder(128);
            builder.Append("PLAN|");
            builder.Append(header.PlanId.Value);
            builder.Append('|');
            builder.Append(header.ArtifactSetId.Value);
            builder.Append('|');
            builder.Append(header.FormatVersion);
            builder.Append('|');
            builder.Append(header.GeneratorVersion);
            builder.Append('|');
            AppendArtifactKinds(builder, header.RequiredArtifactKinds);
            builder.Append('|');
            builder.Append(header.SourceHash);
            builder.Append('|');
            builder.Append(header.RegistryHash);
            builder.Append('|');
            builder.Append(header.ProfileHash);
            builder.Append('|');
            builder.Append(header.DebugMapHash);
            return builder.ToString();
        }

        static string BuildArtifactToken(VerifiedArtifactHeader artifact)
        {
            return "ARTIFACT|" + artifact.ArtifactKind.ToString() + "|" + artifact.ArtifactId.Value.ToString() + "|" + artifact.FormatVersion.ToString() + "|" + artifact.GeneratorVersion + "|" + artifact.SourceHash + "|" + artifact.RegistryHash + "|" + artifact.ProfileHash + "|" + artifact.DebugMapHash + "|" + artifact.GeneratedHash;
        }

        static void AppendArtifactKinds(StringBuilder builder, ReadOnlySpan<ArtifactKind> artifactKinds)
        {
            for (int i = 0; i < artifactKinds.Length; i++)
            {
                if (i > 0)
                    builder.Append(',');

                builder.Append((int)artifactKinds[i]);
            }
        }

        static bool ContainsArtifactKind(ReadOnlySpan<ArtifactKind> artifactKinds, ArtifactKind artifactKind)
        {
            for (int i = 0; i < artifactKinds.Length; i++)
            {
                if (artifactKinds[i] == artifactKind)
                    return true;
            }

            return false;
        }

    }

    public sealed class VerifiedKernelPlan : IEquatable<VerifiedKernelPlan>
    {
        internal VerifiedKernelPlan(KernelPlanHeader header, ArtifactSetManifest manifest)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        }

        public KernelPlanHeader Header { get; }

        public ArtifactSetManifest Manifest { get; }

        public ReadOnlySpan<VerifiedArtifactHeader> Artifacts => Manifest.Artifacts;

        public bool Equals(VerifiedKernelPlan? other)
        {
            if (other == null)
                return false;

            return Header.Equals(other.Header) && Manifest.Equals(other.Manifest);
        }

        public override bool Equals(object? obj)
        {
            return obj is VerifiedKernelPlan other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Header.GetHashCode();
                hash = (hash * 397) ^ Manifest.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(VerifiedKernelPlan? left, VerifiedKernelPlan? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(VerifiedKernelPlan? left, VerifiedKernelPlan? right)
        {
            return !Equals(left, right);
        }
    }

    public readonly struct KernelPlanVerificationIssue : IEquatable<KernelPlanVerificationIssue>
    {
        public KernelPlanVerificationIssue(string code, string message)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Verification issues must provide a code.", nameof(code));

            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Verification issues must provide a message.", nameof(message));

            Code = code;
            Message = message;
        }

        public string Code { get; }

        public string Message { get; }

        public bool Equals(KernelPlanVerificationIssue other)
        {
            return StringComparer.Ordinal.Equals(Code, other.Code) && StringComparer.Ordinal.Equals(Message, other.Message);
        }

        public override bool Equals(object? obj)
        {
            return obj is KernelPlanVerificationIssue other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.Ordinal.GetHashCode(Code);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Message);
                return hash;
            }
        }

        public static bool operator ==(KernelPlanVerificationIssue left, KernelPlanVerificationIssue right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(KernelPlanVerificationIssue left, KernelPlanVerificationIssue right)
        {
            return !left.Equals(right);
        }
    }

    public sealed class KernelPlanVerificationResult
    {
        readonly KernelPlanVerificationIssue[] issues;

        public KernelPlanVerificationResult(VerifiedKernelPlan? verifiedPlan, IReadOnlyList<KernelPlanVerificationIssue> issues)
        {
            if (issues == null)
                throw new ArgumentNullException(nameof(issues));

            VerifiedPlan = verifiedPlan;
            this.issues = CloneIssues(issues);
        }

        public bool IsVerified => VerifiedPlan != null && issues.Length == 0;

        public VerifiedKernelPlan? VerifiedPlan { get; }

        public IReadOnlyList<KernelPlanVerificationIssue> Issues => issues;

        static KernelPlanVerificationIssue[] CloneIssues(IReadOnlyList<KernelPlanVerificationIssue> source)
        {
            KernelPlanVerificationIssue[] clone = new KernelPlanVerificationIssue[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                clone[i] = source[i];
            }

            return clone;
        }
    }

    public static class KernelPlanVerification
    {
        public static KernelPlanVerificationResult Verify(GeneratedKernelPlan? generatedPlan)
        {
            List<KernelPlanVerificationIssue> issues = new List<KernelPlanVerificationIssue>();

            if (generatedPlan == null)
            {
                issues.Add(new KernelPlanVerificationIssue("M4_2_GENERATED_PLAN_MISSING", "GeneratedKernelPlan must not be null."));
                return new KernelPlanVerificationResult(null, issues);
            }

            ReadOnlySpan<VerifiedArtifactHeader> artifacts = generatedPlan.Artifacts;
            if (artifacts.Length == 0)
            {
                issues.Add(new KernelPlanVerificationIssue("M4_2_ARTIFACT_SET_EMPTY", "GeneratedKernelPlan must contain at least one artifact before verification."));
                return new KernelPlanVerificationResult(null, issues);
            }

            KernelPlanHeader header = generatedPlan.Header;

            if (!IsCanonicalRequiredArtifactKindOrder(header.RequiredArtifactKinds))
                issues.Add(new KernelPlanVerificationIssue("M4_4_REQUIRED_ARTIFACT_KIND_ORDER_NONDETERMINISTIC", "KernelPlanHeader.RequiredArtifactKinds must be in deterministic canonical order."));

            if (!IsCanonicalArtifactOrder(artifacts))
                issues.Add(new KernelPlanVerificationIssue("M4_4_ARTIFACT_ORDER_NONDETERMINISTIC", "GeneratedKernelPlan.Artifacts must be in deterministic canonical order."));

            if (issues.Count > 0)
                return new KernelPlanVerificationResult(null, issues);

            KernelPlanHeader? canonicalHeader = null;
            HashSet<int> seenArtifactIds = new HashSet<int>();
            HashSet<ArtifactKind> seenArtifactKinds = new HashSet<ArtifactKind>();

            for (int index = 0; index < artifacts.Length; index++)
            {
                VerifiedArtifactHeader artifact = artifacts[index];

                if (artifact.DebugMapHash.IsZero)
                    issues.Add(new KernelPlanVerificationIssue("M4_2_DEBUG_MAP_MISSING", "Artifact set verification requires non-zero DebugMapHash coverage."));

                if (artifact.GeneratedHash.IsZero)
                    issues.Add(new KernelPlanVerificationIssue("M4_2_GENERATED_HASH_MISSING", "Artifact set verification requires non-zero generated content hashes."));

                if (!seenArtifactIds.Add(artifact.ArtifactId.Value))
                    issues.Add(new KernelPlanVerificationIssue("M4_2_DUPLICATE_ARTIFACT_ID", "Artifact set verification requires unique artifact identities."));

                if (!ContainsArtifactKind(header.RequiredArtifactKinds, artifact.ArtifactKind))
                    issues.Add(new KernelPlanVerificationIssue("M4_2_REQUIRED_ARTIFACT_KIND_MISSING", "Artifact set verification requires only declared artifact kinds."));

                if (!seenArtifactKinds.Add(artifact.ArtifactKind))
                    issues.Add(new KernelPlanVerificationIssue("M4_2_DUPLICATE_ARTIFACT_KIND", "Artifact set verification requires unique artifact kinds."));

                if (canonicalHeader == null)
                {
                    canonicalHeader = new KernelPlanHeader(
                        artifact.PlanId,
                        artifact.ArtifactSetId,
                        artifact.FormatVersion,
                        artifact.GeneratorVersion,
                        header.RequiredArtifactKinds.ToArray(),
                        artifact.SourceHash,
                        artifact.RegistryHash,
                        artifact.ProfileHash,
                        artifact.DebugMapHash,
                        header.GeneratedHash);
                    continue;
                }

                if (artifact.PlanId != canonicalHeader.PlanId
                    || artifact.ArtifactSetId != canonicalHeader.ArtifactSetId
                    || artifact.FormatVersion != canonicalHeader.FormatVersion
                    || !StringComparer.Ordinal.Equals(artifact.GeneratorVersion, canonicalHeader.GeneratorVersion)
                    || artifact.SourceHash != canonicalHeader.SourceHash
                    || artifact.RegistryHash != canonicalHeader.RegistryHash
                    || artifact.ProfileHash != canonicalHeader.ProfileHash
                    || artifact.DebugMapHash != canonicalHeader.DebugMapHash)
                {
                    issues.Add(new KernelPlanVerificationIssue("M4_2_ARTIFACT_INCONSISTENT", "Artifact set verification requires a single compatible plan identity, hash set, and DebugMap coverage."));
                }
            }

            if (seenArtifactKinds.Count != header.RequiredArtifactKinds.Length)
                issues.Add(new KernelPlanVerificationIssue("M4_2_ARTIFACT_SET_INCOMPLETE", "Artifact set verification requires every declared artifact kind to be present exactly once."));

            if (canonicalHeader == null)
            {
                issues.Add(new KernelPlanVerificationIssue("M4_2_CANONICAL_HEADER_MISSING", "Artifact set verification could not derive a canonical header."));
                return new KernelPlanVerificationResult(null, issues);
            }

            if (header.PlanId != canonicalHeader.PlanId
                || header.ArtifactSetId != canonicalHeader.ArtifactSetId
                || header.FormatVersion != canonicalHeader.FormatVersion
                || !StringComparer.Ordinal.Equals(header.GeneratorVersion, canonicalHeader.GeneratorVersion)
                || header.SourceHash != canonicalHeader.SourceHash
                || header.RegistryHash != canonicalHeader.RegistryHash
                || header.ProfileHash != canonicalHeader.ProfileHash
                || header.DebugMapHash != canonicalHeader.DebugMapHash)
            {
                issues.Add(new KernelPlanVerificationIssue("M4_2_HEADER_MISMATCH", "KernelPlanHeader must match the consistent artifact header set."));
            }

            Hash128 consistencyHash = ArtifactSetManifest.ComputeConsistencyHash(header, artifacts);
            if (header.GeneratedHash != consistencyHash)
                issues.Add(new KernelPlanVerificationIssue("M4_2_CONSISTENCY_HASH_MISMATCH", "KernelPlanHeader.GeneratedHash must match the artifact-set consistency hash."));

            if (issues.Count > 0)
                return new KernelPlanVerificationResult(null, issues);

            ArtifactSetManifest manifest = new ArtifactSetManifest(header, CopyArtifacts(generatedPlan.Artifacts));
            VerifiedKernelPlan verifiedPlan = new VerifiedKernelPlan(header, manifest);
            return new KernelPlanVerificationResult(verifiedPlan, issues);
        }

        static VerifiedArtifactHeader[] CopyArtifacts(ReadOnlySpan<VerifiedArtifactHeader> artifacts)
        {
            VerifiedArtifactHeader[] copy = new VerifiedArtifactHeader[artifacts.Length];
            for (int i = 0; i < artifacts.Length; i++)
            {
                copy[i] = artifacts[i];
            }

            return copy;
        }

        static bool ContainsArtifactKind(ReadOnlySpan<ArtifactKind> artifactKinds, ArtifactKind artifactKind)
        {
            for (int i = 0; i < artifactKinds.Length; i++)
            {
                if (artifactKinds[i] == artifactKind)
                    return true;
            }

            return false;
        }

        internal static bool IsCanonicalRequiredArtifactKindOrder(ReadOnlySpan<ArtifactKind> artifactKinds)
        {
            if (artifactKinds.Length == 0)
                return false;

            ArtifactKind previous = artifactKinds[0];
            if (previous == ArtifactKind.Unknown)
                return false;

            for (int i = 1; i < artifactKinds.Length; i++)
            {
                ArtifactKind current = artifactKinds[i];
                if (current == ArtifactKind.Unknown || current <= previous)
                    return false;

                previous = current;
            }

            return true;
        }

        internal static bool IsCanonicalArtifactOrder(ReadOnlySpan<VerifiedArtifactHeader> artifacts)
        {
            if (artifacts.Length == 0)
                return false;

            VerifiedArtifactHeader previous = artifacts[0];
            for (int i = 1; i < artifacts.Length; i++)
            {
                VerifiedArtifactHeader current = artifacts[i];
                if (GeneratedKernelPlan.CompareArtifacts(previous, current) >= 0)
                    return false;

                previous = current;
            }

            return true;
        }
    }
}
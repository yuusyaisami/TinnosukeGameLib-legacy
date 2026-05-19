#nullable enable
using System;
using System.Collections.Generic;
using Game.Kernel.IR;

namespace Game.Kernel.Generation
{
    public enum ArtifactSetStalenessReason
    {
        Unknown = 0,
        SourceHashMismatch = 10,
        RegistryHashMismatch = 20,
        ProfileHashMismatch = 30,
        DebugMapHashMismatch = 40,
        FormatVersionMismatch = 50,
        GeneratorVersionMismatch = 60,
        GeneratedHashMissing = 70,
        DebugMapHashMissing = 80,
        ArtifactSetIncomplete = 90,
    }

    public readonly struct ArtifactSetStalenessIssue : IEquatable<ArtifactSetStalenessIssue>
    {
        public ArtifactSetStalenessIssue(ArtifactSetStalenessReason reason, string code, string field, string message)
        {
            if (reason == ArtifactSetStalenessReason.Unknown)
                throw new ArgumentOutOfRangeException(nameof(reason), reason, "Staleness issues must provide a concrete reason.");

            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Staleness issues must provide a code.", nameof(code));

            if (string.IsNullOrWhiteSpace(field))
                throw new ArgumentException("Staleness issues must provide a field name.", nameof(field));

            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Staleness issues must provide a message.", nameof(message));

            Reason = reason;
            Code = code;
            Field = field;
            Message = message;
        }

        public ArtifactSetStalenessReason Reason { get; }

        public string Code { get; }

        public string Field { get; }

        public string Message { get; }

        public ArtifactSetPromotionIssue ToPromotionIssue()
        {
            return new ArtifactSetPromotionIssue(Code, Message);
        }

        public bool Equals(ArtifactSetStalenessIssue other)
        {
            return Reason == other.Reason
                && StringComparer.Ordinal.Equals(Code, other.Code)
                && StringComparer.Ordinal.Equals(Field, other.Field)
                && StringComparer.Ordinal.Equals(Message, other.Message);
        }

        public override bool Equals(object? obj)
        {
            return obj is ArtifactSetStalenessIssue other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Reason;
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Code);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Field);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Message);
                return hash;
            }
        }

        public static bool operator ==(ArtifactSetStalenessIssue left, ArtifactSetStalenessIssue right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ArtifactSetStalenessIssue left, ArtifactSetStalenessIssue right)
        {
            return !left.Equals(right);
        }
    }

    public sealed class ArtifactSetStalenessReport
    {
        readonly ArtifactSetStalenessIssue[] issues;

        internal ArtifactSetStalenessReport(IReadOnlyList<ArtifactSetStalenessIssue> issues)
        {
            if (issues == null)
                throw new ArgumentNullException(nameof(issues));

            this.issues = CloneIssues(issues);
        }

        public IReadOnlyList<ArtifactSetStalenessIssue> Issues => issues;

        public bool IsStale => issues.Length > 0;

        static ArtifactSetStalenessIssue[] CloneIssues(IReadOnlyList<ArtifactSetStalenessIssue> source)
        {
            ArtifactSetStalenessIssue[] clone = new ArtifactSetStalenessIssue[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                clone[i] = source[i];
            }

            return clone;
        }
    }

    public static class ArtifactSetStalenessDetector
    {
        public static ArtifactSetStalenessReport Evaluate(ArtifactSetPromotionInputs currentInputs, VerifiedKernelPlan candidate)
        {
            if (candidate == null)
                throw new ArgumentNullException(nameof(candidate));

            return Evaluate(currentInputs, candidate.Header, candidate.Artifacts);
        }

        public static ArtifactSetStalenessReport Evaluate(ArtifactSetPromotionInputs currentInputs, KernelPlanHeader candidateHeader, ReadOnlySpan<VerifiedArtifactHeader> artifacts)
        {
            if (candidateHeader == null)
                throw new ArgumentNullException(nameof(candidateHeader));

            List<ArtifactSetStalenessIssue> issues = new List<ArtifactSetStalenessIssue>();

            if (candidateHeader.SourceHash != currentInputs.SourceHash)
                issues.Add(CreateIssue(ArtifactSetStalenessReason.SourceHashMismatch, "M4_6_STALE_SOURCE_HASH", "SourceHash", "Artifact promotion requires the candidate SourceHash to match the current validated KernelIR input."));

            if (candidateHeader.RegistryHash != currentInputs.RegistryHash)
                issues.Add(CreateIssue(ArtifactSetStalenessReason.RegistryHashMismatch, "M4_6_STALE_REGISTRY_HASH", "RegistryHash", "Artifact promotion requires the candidate RegistryHash to match the current registry input state."));

            if (candidateHeader.ProfileHash != currentInputs.ProfileHash)
                issues.Add(CreateIssue(ArtifactSetStalenessReason.ProfileHashMismatch, "M4_6_STALE_PROFILE_HASH", "ProfileHash", "Artifact promotion requires the candidate ProfileHash to match the selected profile."));

            if (candidateHeader.FormatVersion != currentInputs.FormatVersion)
                issues.Add(CreateIssue(ArtifactSetStalenessReason.FormatVersionMismatch, "M4_6_FORMAT_VERSION_MISMATCH", "FormatVersion", "Artifact promotion requires the candidate format version to match the current promotion inputs."));

            if (!StringComparer.Ordinal.Equals(candidateHeader.GeneratorVersion, currentInputs.GeneratorVersion))
                issues.Add(CreateIssue(ArtifactSetStalenessReason.GeneratorVersionMismatch, "M4_6_GENERATOR_VERSION_MISMATCH", "GeneratorVersion", "Artifact promotion requires the candidate generator version to match the current promotion inputs."));

            if (candidateHeader.DebugMapHash.IsZero)
                issues.Add(CreateIssue(ArtifactSetStalenessReason.DebugMapHashMissing, "M4_6_DEBUG_MAP_HASH_MISSING", "DebugMapHash", "Artifact promotion requires non-zero DebugMap coverage."));

            if (candidateHeader.DebugMapHash != currentInputs.DebugMapHash && !candidateHeader.DebugMapHash.IsZero)
                issues.Add(CreateIssue(ArtifactSetStalenessReason.DebugMapHashMismatch, "M4_6_STALE_DEBUG_MAP_HASH", "DebugMapHash", "Artifact promotion requires the candidate DebugMapHash to match the current DebugMap coverage."));

            if (candidateHeader.GeneratedHash.IsZero)
                issues.Add(CreateIssue(ArtifactSetStalenessReason.GeneratedHashMissing, "M4_6_GENERATED_HASH_MISSING", "GeneratedHash", "Artifact promotion requires a non-zero generated hash."));

            if (!HasRequiredArtifactKinds(candidateHeader.RequiredArtifactKinds, artifacts))
                issues.Add(CreateIssue(ArtifactSetStalenessReason.ArtifactSetIncomplete, "M4_6_ARTIFACT_SET_INCOMPLETE", "ArtifactKinds", "Artifact promotion requires every declared artifact kind to be present exactly once."));

            return new ArtifactSetStalenessReport(issues);
        }

        static ArtifactSetStalenessIssue CreateIssue(ArtifactSetStalenessReason reason, string code, string field, string message)
        {
            return new ArtifactSetStalenessIssue(reason, code, field, message);
        }

        static bool HasRequiredArtifactKinds(ReadOnlySpan<ArtifactKind> requiredKinds, ReadOnlySpan<VerifiedArtifactHeader> artifacts)
        {
            if (requiredKinds.Length == 0)
                return false;

            HashSet<ArtifactKind> seenKinds = new HashSet<ArtifactKind>();
            for (int i = 0; i < artifacts.Length; i++)
            {
                ArtifactKind artifactKind = artifacts[i].ArtifactKind;
                if (!Contains(requiredKinds, artifactKind) || !seenKinds.Add(artifactKind))
                    return false;
            }

            return seenKinds.Count == requiredKinds.Length;
        }

        static bool Contains(ReadOnlySpan<ArtifactKind> requiredKinds, ArtifactKind artifactKind)
        {
            for (int i = 0; i < requiredKinds.Length; i++)
            {
                if (requiredKinds[i] == artifactKind)
                    return true;
            }

            return false;
        }
    }
}
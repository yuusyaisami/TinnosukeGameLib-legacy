#nullable enable
using System;
using System.Collections.Generic;
using Game.Kernel.IR;

namespace Game.Kernel.Generation
{
    public readonly struct ArtifactSetPromotionIssue : IEquatable<ArtifactSetPromotionIssue>
    {
        public ArtifactSetPromotionIssue(string code, string message)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Promotion issues must provide a code.", nameof(code));

            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Promotion issues must provide a message.", nameof(message));

            Code = code;
            Message = message;
        }

        public string Code { get; }

        public string Message { get; }

        public bool Equals(ArtifactSetPromotionIssue other)
        {
            return StringComparer.Ordinal.Equals(Code, other.Code) && StringComparer.Ordinal.Equals(Message, other.Message);
        }

        public override bool Equals(object? obj)
        {
            return obj is ArtifactSetPromotionIssue other && Equals(other);
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

        public static bool operator ==(ArtifactSetPromotionIssue left, ArtifactSetPromotionIssue right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ArtifactSetPromotionIssue left, ArtifactSetPromotionIssue right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct ArtifactSetPromotionInputs : IEquatable<ArtifactSetPromotionInputs>
    {
        public ArtifactSetPromotionInputs(Hash128 sourceHash, Hash128 registryHash, Hash128 profileHash, Hash128 debugMapHash, int formatVersion, string generatorVersion)
        {
            if (sourceHash.IsZero)
                throw new ArgumentException("Promotion inputs must provide a non-zero source hash.", nameof(sourceHash));

            if (registryHash.IsZero)
                throw new ArgumentException("Promotion inputs must provide a non-zero registry hash.", nameof(registryHash));

            if (profileHash.IsZero)
                throw new ArgumentException("Promotion inputs must provide a non-zero profile hash.", nameof(profileHash));

            if (debugMapHash.IsZero)
                throw new ArgumentException("Promotion inputs must provide a non-zero debug map hash.", nameof(debugMapHash));

            if (formatVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(formatVersion), formatVersion, "Promotion inputs must provide a positive format version.");

            if (string.IsNullOrWhiteSpace(generatorVersion))
                throw new ArgumentException("Promotion inputs must provide a generator version.", nameof(generatorVersion));

            SourceHash = sourceHash;
            RegistryHash = registryHash;
            ProfileHash = profileHash;
            DebugMapHash = debugMapHash;
            FormatVersion = formatVersion;
            GeneratorVersion = generatorVersion;
        }

        public Hash128 SourceHash { get; }

        public Hash128 RegistryHash { get; }

        public Hash128 ProfileHash { get; }

        public Hash128 DebugMapHash { get; }

        public int FormatVersion { get; }

        public string GeneratorVersion { get; }

        public bool Equals(ArtifactSetPromotionInputs other)
        {
            return SourceHash == other.SourceHash
                && RegistryHash == other.RegistryHash
                && ProfileHash == other.ProfileHash
                && DebugMapHash == other.DebugMapHash
                && FormatVersion == other.FormatVersion
                && StringComparer.Ordinal.Equals(GeneratorVersion, other.GeneratorVersion);
        }

        public override bool Equals(object? obj)
        {
            return obj is ArtifactSetPromotionInputs other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SourceHash.GetHashCode();
                hash = (hash * 397) ^ RegistryHash.GetHashCode();
                hash = (hash * 397) ^ ProfileHash.GetHashCode();
                hash = (hash * 397) ^ DebugMapHash.GetHashCode();
                hash = (hash * 397) ^ FormatVersion;
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(GeneratorVersion);
                return hash;
            }
        }

        public static bool operator ==(ArtifactSetPromotionInputs left, ArtifactSetPromotionInputs right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ArtifactSetPromotionInputs left, ArtifactSetPromotionInputs right)
        {
            return !left.Equals(right);
        }
    }

    public sealed class ArtifactSetPublicationState : IEquatable<ArtifactSetPublicationState>
    {
        public static ArtifactSetPublicationState Empty { get; } = new ArtifactSetPublicationState(null, null);

        public static ArtifactSetPublicationState Create(VerifiedKernelPlan current, VerifiedKernelPlan? previous = null)
        {
            return new ArtifactSetPublicationState(current ?? throw new ArgumentNullException(nameof(current)), previous);
        }

        ArtifactSetPublicationState(VerifiedKernelPlan? current, VerifiedKernelPlan? previous)
        {
            if (current == null && previous != null)
                throw new ArgumentException("A previous artifact set cannot exist without a current artifact set.", nameof(previous));

            Current = current;
            Previous = previous;
        }

        public VerifiedKernelPlan? Current { get; }

        public VerifiedKernelPlan? Previous { get; }

        public bool HasCurrent => Current != null;

        public bool HasPrevious => Previous != null;

        public bool Equals(ArtifactSetPublicationState? other)
        {
            if (other == null)
                return false;

            return Equals(Current, other.Current) && Equals(Previous, other.Previous);
        }

        public override bool Equals(object? obj)
        {
            return obj is ArtifactSetPublicationState other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Current != null ? Current.GetHashCode() : 0;
                hash = (hash * 397) ^ (Previous != null ? Previous.GetHashCode() : 0);
                return hash;
            }
        }

        public static bool operator ==(ArtifactSetPublicationState? left, ArtifactSetPublicationState? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(ArtifactSetPublicationState? left, ArtifactSetPublicationState? right)
        {
            return !Equals(left, right);
        }
    }

    public sealed class ArtifactSetStagingRecord
    {
        internal ArtifactSetStagingRecord(ArtifactSetPublicationState basePublicationState, ArtifactSetPromotionInputs currentInputs, VerifiedKernelPlan candidate)
        {
            BasePublicationState = basePublicationState ?? throw new ArgumentNullException(nameof(basePublicationState));
            CurrentInputs = currentInputs;
            Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
        }

        public ArtifactSetPublicationState BasePublicationState { get; }

        public ArtifactSetPromotionInputs CurrentInputs { get; }

        public VerifiedKernelPlan Candidate { get; }
    }

    public sealed class ArtifactSetPromotionResult
    {
        readonly ArtifactSetPromotionIssue[] issues;

        internal ArtifactSetPromotionResult(ArtifactSetPublicationState publicationState, ArtifactSetStagingRecord? stagingRecord, VerifiedKernelPlan? promotedPlan, IReadOnlyList<ArtifactSetPromotionIssue> issues)
        {
            PublicationState = publicationState ?? throw new ArgumentNullException(nameof(publicationState));
            StagingRecord = stagingRecord;
            PromotedPlan = promotedPlan;

            if (issues == null)
                throw new ArgumentNullException(nameof(issues));

            this.issues = CloneIssues(issues);
        }

        public ArtifactSetPublicationState PublicationState { get; }

        public ArtifactSetStagingRecord? StagingRecord { get; }

        public VerifiedKernelPlan? PromotedPlan { get; }

        public IReadOnlyList<ArtifactSetPromotionIssue> Issues => issues;

        public bool IsSuccessful => issues.Length == 0;

        public bool IsStaged => StagingRecord != null && PromotedPlan == null && IsSuccessful;

        public bool IsPromoted => PromotedPlan != null && StagingRecord == null && IsSuccessful;

        static ArtifactSetPromotionIssue[] CloneIssues(IReadOnlyList<ArtifactSetPromotionIssue> source)
        {
            ArtifactSetPromotionIssue[] clone = new ArtifactSetPromotionIssue[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                clone[i] = source[i];
            }

            return clone;
        }
    }

    public static class ArtifactSetPromotionTransaction
    {
        public static ArtifactSetPromotionResult Stage(ArtifactSetPublicationState publicationState, ArtifactSetPromotionInputs currentInputs, VerifiedKernelPlan candidate)
        {
            List<ArtifactSetPromotionIssue> issues = new List<ArtifactSetPromotionIssue>();

            if (publicationState == null)
                throw new ArgumentNullException(nameof(publicationState));

            if (candidate == null)
                throw new ArgumentNullException(nameof(candidate));

            ArtifactSetStalenessReport stalenessReport = ArtifactSetStalenessDetector.Evaluate(currentInputs, candidate);
            for (int index = 0; index < stalenessReport.Issues.Count; index++)
                issues.Add(stalenessReport.Issues[index].ToPromotionIssue());

            if (issues.Count > 0)
                return new ArtifactSetPromotionResult(publicationState, null, null, issues);

            ArtifactSetStagingRecord stagingRecord = new ArtifactSetStagingRecord(publicationState, currentInputs, candidate);
            return new ArtifactSetPromotionResult(publicationState, stagingRecord, null, issues);
        }

        public static ArtifactSetPromotionResult Commit(ArtifactSetPublicationState currentState, ArtifactSetStagingRecord stagingRecord)
        {
            List<ArtifactSetPromotionIssue> issues = new List<ArtifactSetPromotionIssue>();

            if (currentState == null)
                throw new ArgumentNullException(nameof(currentState));

            if (stagingRecord == null)
                throw new ArgumentNullException(nameof(stagingRecord));

            if (!currentState.Equals(stagingRecord.BasePublicationState))
            {
                issues.Add(new ArtifactSetPromotionIssue("M4_3_PUBLICATION_STATE_CHANGED", "Promotion must commit against the same publication state that was staged."));
                return new ArtifactSetPromotionResult(currentState, null, null, issues);
            }

            if (currentState.Current != null && currentState.Current.Equals(stagingRecord.Candidate))
                return new ArtifactSetPromotionResult(currentState, null, stagingRecord.Candidate, issues);

            ArtifactSetPublicationState promotedState = ArtifactSetPublicationState.Create(stagingRecord.Candidate, currentState.Current);
            return new ArtifactSetPromotionResult(promotedState, null, stagingRecord.Candidate, issues);
        }
    }
}
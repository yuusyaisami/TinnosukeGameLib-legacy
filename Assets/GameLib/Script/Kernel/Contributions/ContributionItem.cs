#nullable enable
using System;
using Game.Kernel.IR;

namespace Game.Kernel.Contributions
{
    public readonly struct ContributionItem : IEquatable<ContributionItem>
    {
        readonly ContributionDependencyDeclaration[] dependencies;

        public ContributionItem(
            ContributionKind kind,
            ModuleId ownerModuleId,
            ContributionSource source,
            SourceLocationIR sourceLocation,
            string stableId,
            ContributionAvailability availability,
            ContributionDependencyDeclaration[]? dependencies = null,
            ContributionConflictPolicy conflictPolicy = ContributionConflictPolicy.ValidationError,
            string? debugName = null)
        {
            if (kind == ContributionKind.Unknown)
                throw new ArgumentException("Contribution items must provide a contribution kind.", nameof(kind));

            if (ownerModuleId.Value == 0)
                throw new ArgumentException("Contribution items must provide a non-zero owner module identity.", nameof(ownerModuleId));

            if (source == ContributionSource.Unknown)
                throw new ArgumentException("Contribution items must provide a contribution source.", nameof(source));

            if (!sourceLocation.IsSpecified)
                throw new ArgumentException("Contribution items must provide a specified source location.", nameof(sourceLocation));

            if (string.IsNullOrWhiteSpace(stableId))
                throw new ArgumentException("Contribution items must provide a stable identity input.", nameof(stableId));

            if (debugName != null && debugName.Trim().Length == 0)
                throw new ArgumentException("Debug name must be null or non-empty.", nameof(debugName));

            if (conflictPolicy == ContributionConflictPolicy.Unknown)
                throw new ArgumentException("Contribution items must provide an explicit conflict policy.", nameof(conflictPolicy));

            Kind = kind;
            OwnerModuleId = ownerModuleId;
            Source = source;
            SourceLocation = sourceLocation;
            StableId = stableId;
            Availability = availability;
            ConflictPolicy = conflictPolicy;
            DebugName = debugName;
            this.dependencies = CloneDependencies(dependencies);
        }

        public ContributionKind Kind { get; }

        public ModuleId OwnerModuleId { get; }

        public ContributionSource Source { get; }

        public SourceLocationIR SourceLocation { get; }

        public string StableId { get; }

        public ContributionAvailability Availability { get; }

        public ContributionConflictPolicy ConflictPolicy { get; }

        public string? DebugName { get; }

        public ReadOnlySpan<ContributionDependencyDeclaration> Dependencies => dependencies ?? Array.Empty<ContributionDependencyDeclaration>();

        public bool Equals(ContributionItem other)
        {
            if (Kind != other.Kind
                || OwnerModuleId != other.OwnerModuleId
                || Source != other.Source
                || SourceLocation != other.SourceLocation
                || !StringComparer.Ordinal.Equals(StableId, other.StableId)
                || Availability != other.Availability
                || ConflictPolicy != other.ConflictPolicy
                || !StringComparer.Ordinal.Equals(DebugName, other.DebugName))
            {
                return false;
            }

            ReadOnlySpan<ContributionDependencyDeclaration> leftDependencies = Dependencies;
            ReadOnlySpan<ContributionDependencyDeclaration> rightDependencies = other.Dependencies;
            if (leftDependencies.Length != rightDependencies.Length)
                return false;

            for (int i = 0; i < leftDependencies.Length; i++)
            {
                if (leftDependencies[i] != rightDependencies[i])
                    return false;
            }

            return true;
        }

        public override bool Equals(object? obj)
        {
            return obj is ContributionItem other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ OwnerModuleId.GetHashCode();
                hash = (hash * 397) ^ (int)Source;
                hash = (hash * 397) ^ SourceLocation.GetHashCode();
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(StableId);
                hash = (hash * 397) ^ Availability.GetHashCode();
                hash = (hash * 397) ^ (int)ConflictPolicy;
                hash = (hash * 397) ^ (DebugName == null ? 0 : StringComparer.Ordinal.GetHashCode(DebugName));

                ReadOnlySpan<ContributionDependencyDeclaration> dependencySpan = Dependencies;
                for (int i = 0; i < dependencySpan.Length; i++)
                {
                    hash = (hash * 397) ^ dependencySpan[i].GetHashCode();
                }

                return hash;
            }
        }

        public override string ToString()
        {
            return "ContributionItem(Kind=" + Kind + ", OwnerModuleId=" + OwnerModuleId + ", Source=" + Source + ", StableId=" + StableId + ", ConflictPolicy=" + ConflictPolicy + ")";
        }

        public static bool operator ==(ContributionItem left, ContributionItem right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ContributionItem left, ContributionItem right)
        {
            return !left.Equals(right);
        }

        static ContributionDependencyDeclaration[] CloneDependencies(ContributionDependencyDeclaration[]? source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<ContributionDependencyDeclaration>();

            ContributionDependencyDeclaration[] clone = new ContributionDependencyDeclaration[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                clone[i] = source[i];
            }

            return clone;
        }
    }
}
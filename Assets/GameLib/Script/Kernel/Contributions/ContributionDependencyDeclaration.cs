#nullable enable
using System;
using Game.Kernel.IR;

namespace Game.Kernel.Contributions
{
    public readonly struct ContributionDependencyDeclaration : IEquatable<ContributionDependencyDeclaration>
    {
        public ContributionDependencyDeclaration(ContributionKind targetKind, ModuleId targetModuleId, string targetStableId, bool isRequired)
        {
            if (targetKind == ContributionKind.Unknown)
                throw new ArgumentException("Dependency declarations must provide a contribution kind.", nameof(targetKind));

            if (targetModuleId.Value == 0)
                throw new ArgumentException("Dependency declarations must provide a non-zero target module identity.", nameof(targetModuleId));

            if (string.IsNullOrWhiteSpace(targetStableId))
                throw new ArgumentException("Dependency declarations must provide a stable target identity.", nameof(targetStableId));

            TargetKind = targetKind;
            TargetModuleId = targetModuleId;
            TargetStableId = targetStableId;
            IsRequired = isRequired;
        }

        public ContributionKind TargetKind { get; }

        public ModuleId TargetModuleId { get; }

        public string TargetStableId { get; }

        public bool IsRequired { get; }

        public bool Equals(ContributionDependencyDeclaration other)
        {
            return TargetKind == other.TargetKind
                && TargetModuleId == other.TargetModuleId
                && StringComparer.Ordinal.Equals(TargetStableId, other.TargetStableId)
                && IsRequired == other.IsRequired;
        }

        public override bool Equals(object? obj)
        {
            return obj is ContributionDependencyDeclaration other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)TargetKind;
                hash = (hash * 397) ^ TargetModuleId.GetHashCode();
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(TargetStableId);
                hash = (hash * 397) ^ IsRequired.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return "ContributionDependencyDeclaration(TargetKind=" + TargetKind + ", TargetModuleId=" + TargetModuleId + ", TargetStableId=" + TargetStableId + ", IsRequired=" + IsRequired + ")";
        }

        public static bool operator ==(ContributionDependencyDeclaration left, ContributionDependencyDeclaration right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ContributionDependencyDeclaration left, ContributionDependencyDeclaration right)
        {
            return !left.Equals(right);
        }
    }
}
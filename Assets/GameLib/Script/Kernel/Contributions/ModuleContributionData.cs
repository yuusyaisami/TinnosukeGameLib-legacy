#nullable enable
using System;
using System.Collections.Generic;
using Game.Kernel.IR;

namespace Game.Kernel.Contributions
{
    public sealed class ModuleContributionData
    {
        readonly ContributionKind[] ownedContributionKinds;
        readonly ModuleId[] requiredModuleIds;
        readonly ModuleId[] optionalModuleIds;
        readonly ContributionItem[] items;

        public ModuleContributionData(
            ModuleId moduleId,
            string moduleName,
            ModuleKind moduleKind,
            ModuleVersion moduleVersion,
            ContributionAvailability availability,
            SourceLocationIR sourceLocation,
            ContributionKind[] ownedContributionKinds,
            ModuleId[] requiredModuleIds,
            ModuleId[] optionalModuleIds,
            ContributionItem[] items)
        {
            if (moduleId.Value == 0)
                throw new ArgumentException("Module contribution data must provide a non-zero module identity.", nameof(moduleId));

            if (string.IsNullOrWhiteSpace(moduleName))
                throw new ArgumentException("Module contribution data must provide a module name.", nameof(moduleName));

            if (moduleKind == ModuleKind.Unknown)
                throw new ArgumentException("Module contribution data must provide a module kind.", nameof(moduleKind));

            if (!sourceLocation.IsSpecified)
                throw new ArgumentException("Module contribution data must provide a specified source location.", nameof(sourceLocation));

            this.ownedContributionKinds = CloneOwnedKinds(ownedContributionKinds);
            this.requiredModuleIds = CloneModuleIds(requiredModuleIds, nameof(requiredModuleIds));
            this.optionalModuleIds = CloneModuleIds(optionalModuleIds, nameof(optionalModuleIds));
            ValidateNoDependencyOverlap(this.requiredModuleIds, this.optionalModuleIds);
            this.items = CloneAndSortItems(moduleId, this.ownedContributionKinds, items);

            ModuleId = moduleId;
            ModuleName = moduleName;
            ModuleKind = moduleKind;
            ModuleVersion = moduleVersion;
            Availability = availability;
            SourceLocation = sourceLocation;
        }

        public ModuleId ModuleId { get; }

        public string ModuleName { get; }

        public ModuleKind ModuleKind { get; }

        public ModuleVersion ModuleVersion { get; }

        public ContributionAvailability Availability { get; }

        public SourceLocationIR SourceLocation { get; }

        public ReadOnlySpan<ContributionKind> OwnedContributionKinds => ownedContributionKinds;

        public ReadOnlySpan<ModuleId> RequiredModuleIds => requiredModuleIds;

        public ReadOnlySpan<ModuleId> OptionalModuleIds => optionalModuleIds;

        public ReadOnlySpan<ContributionItem> Items => items;

        static ContributionKind[] CloneOwnedKinds(ContributionKind[] source)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Module contribution data must declare at least one owned contribution kind.", nameof(source));

            ContributionKind[] clone = new ContributionKind[source.Length];
            HashSet<int> seenKinds = new HashSet<int>();
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] == ContributionKind.Unknown)
                    throw new ArgumentException("Owned contribution kinds must not include Unknown.", nameof(source));

                if (!seenKinds.Add((int)source[i]))
                    throw new ArgumentException("Owned contribution kinds must be unique.", nameof(source));

                clone[i] = source[i];
            }

            Array.Sort(clone, (left, right) => ((int)left).CompareTo((int)right));
            return clone;
        }

        static ModuleId[] CloneModuleIds(ModuleId[] source, string parameterName)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<ModuleId>();

            ModuleId[] clone = new ModuleId[source.Length];
            HashSet<int> seenIds = new HashSet<int>();
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i].Value == 0)
                    throw new ArgumentException("Module dependency identities must be non-zero.", parameterName);

                if (!seenIds.Add(source[i].Value))
                    throw new ArgumentException("Module dependency identities must be unique within the same bucket.", parameterName);

                clone[i] = source[i];
            }

            Array.Sort(clone, (left, right) => left.Value.CompareTo(right.Value));
            return clone;
        }

        static ContributionItem[] CloneAndSortItems(ModuleId moduleId, ContributionKind[] ownedContributionKinds, ContributionItem[] source)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Module contribution data must contain at least one contribution item.", nameof(source));

            ContributionItem[] clone = new ContributionItem[source.Length];
            HashSet<string> seenContributionKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i].OwnerModuleId != moduleId)
                    throw new ArgumentException("Contribution items must be owned by the declaring module.", nameof(source));

                if (!ContainsKind(ownedContributionKinds, source[i].Kind))
                    throw new ArgumentException("Contribution items must stay within the module's owned contribution kinds.", nameof(source));

                string contributionKey = CreateContributionKey(source[i]);
                if (!seenContributionKeys.Add(contributionKey))
                    throw new ArgumentException("Duplicate contribution identity detected. Conflicts must fail closed.", nameof(source));

                clone[i] = source[i];
            }

            Array.Sort(clone, CompareContributionItems);

            return clone;
        }

        static int CompareContributionItems(ContributionItem left, ContributionItem right)
        {
            int ownerComparison = left.OwnerModuleId.Value.CompareTo(right.OwnerModuleId.Value);
            if (ownerComparison != 0)
                return ownerComparison;

            int kindComparison = ((int)left.Kind).CompareTo((int)right.Kind);
            if (kindComparison != 0)
                return kindComparison;

            int stableIdComparison = StringComparer.Ordinal.Compare(left.StableId, right.StableId);
            if (stableIdComparison != 0)
                return stableIdComparison;

            return 0;
        }

        static string CreateContributionKey(ContributionItem item)
        {
            return item.OwnerModuleId.Value + "|" + (int)item.Kind + "|" + item.StableId;
        }

        static void ValidateNoDependencyOverlap(ModuleId[] required, ModuleId[] optional)
        {
            HashSet<int> requiredIds = new HashSet<int>();
            for (int i = 0; i < required.Length; i++)
            {
                requiredIds.Add(required[i].Value);
            }

            for (int i = 0; i < optional.Length; i++)
            {
                if (requiredIds.Contains(optional[i].Value))
                    throw new ArgumentException("Required and optional module dependencies must not overlap.", nameof(optional));
            }
        }

        static bool ContainsKind(ContributionKind[] kinds, ContributionKind target)
        {
            for (int i = 0; i < kinds.Length; i++)
            {
                if (kinds[i] == target)
                    return true;
            }

            return false;
        }
    }
}
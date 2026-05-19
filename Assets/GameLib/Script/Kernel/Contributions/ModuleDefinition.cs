#nullable enable
using System;
using System.Collections.Generic;
using Game.Kernel.IR;

namespace Game.Kernel.Contributions
{
    public abstract class ModuleDefinition
    {
        readonly ContributionKind[] ownedContributionKinds;
        readonly ModuleId[] requiredModuleIds;
        readonly ModuleId[] optionalModuleIds;

        protected ModuleDefinition(
            ModuleId id,
            string name,
            ModuleKind kind,
            ModuleVersion version,
            ContributionAvailability availability,
            SourceLocationIR sourceLocation,
            ContributionKind[] ownedContributionKinds,
            ModuleId[]? requiredModuleIds = null,
            ModuleId[]? optionalModuleIds = null)
        {
            if (id.Value == 0)
                throw new ArgumentException("Module definitions must provide a non-zero module identity.", nameof(id));

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Module definitions must provide a stable name.", nameof(name));

            if (kind == ModuleKind.Unknown)
                throw new ArgumentException("Module definitions must provide a module kind.", nameof(kind));

            if (!sourceLocation.IsSpecified)
                throw new ArgumentException("Module definitions must provide a specified source location.", nameof(sourceLocation));

            this.ownedContributionKinds = CloneOwnedContributionKinds(ownedContributionKinds);
            this.requiredModuleIds = CloneModuleIds(requiredModuleIds, nameof(requiredModuleIds));
            this.optionalModuleIds = CloneModuleIds(optionalModuleIds, nameof(optionalModuleIds));
            ValidateNoDependencyOverlap(this.requiredModuleIds, this.optionalModuleIds);

            Id = id;
            Name = name;
            Kind = kind;
            Version = version;
            Availability = availability;
            SourceLocation = sourceLocation;
        }

        public ModuleId Id { get; }

        public string Name { get; }

        public ModuleKind Kind { get; }

        public ModuleVersion Version { get; }

        public ContributionAvailability Availability { get; }

        public SourceLocationIR SourceLocation { get; }

        public ReadOnlySpan<ContributionKind> OwnedContributionKinds => ownedContributionKinds;

        public ReadOnlySpan<ModuleId> RequiredModuleIds => requiredModuleIds;

        public ReadOnlySpan<ModuleId> OptionalModuleIds => optionalModuleIds;

        public ModuleContributionData CollectContributions()
        {
            return new ModuleContributionData(
                Id,
                Name,
                Kind,
                Version,
                Availability,
                SourceLocation,
                ownedContributionKinds,
                requiredModuleIds,
                optionalModuleIds,
                CollectContributionItemsCore());
        }

        protected abstract ContributionItem[] CollectContributionItemsCore();

        static ContributionKind[] CloneOwnedContributionKinds(ContributionKind[] source)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Module definitions must declare at least one owned contribution kind.", nameof(source));

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

        static ModuleId[] CloneModuleIds(ModuleId[]? source, string parameterName)
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
    }
}

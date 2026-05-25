#nullable enable

using System;
using Game.Kernel.Abstractions;
using Game.Kernel.IR;

namespace Game.Kernel.Authoring
{
    public readonly struct EntityServiceDependencyInput
    {
        public EntityServiceDependencyInput(DependencyNodeIR target, DependencyStrength strength)
        {
            if (target.Kind == DependencyNodeKind.Unknown)
                throw new ArgumentException("Entity service declaration dependencies must provide an explicit target.", nameof(target));

            if (!Enum.IsDefined(typeof(DependencyStrength), strength))
                throw new ArgumentOutOfRangeException(nameof(strength), strength, "Entity service declaration dependencies must provide a defined dependency strength.");

            Target = target;
            Strength = strength;
        }

        public DependencyNodeIR Target { get; }

        public DependencyStrength Strength { get; }
    }

    public readonly struct EntityServiceDeclarationInput
    {
        readonly string[] contractNames;
        readonly EntityServiceDependencyInput[] dependencies;

        public EntityServiceDeclarationInput(
            ModuleId ownerModule,
            EntityRef ownerEntityRef,
            ServiceId serviceId,
            string stableId,
            string serviceName,
            string debugName,
            string[] contractNames,
            EntityServiceDependencyInput[]? dependencies,
            UnityAuthoringSourceKind sourceKind,
            ServiceLifetimeKind lifetime,
            ServiceFactoryKind factoryKind,
            SourceLocationIR source)
        {
            if (ownerModule.Value == 0)
                throw new ArgumentException("Entity service declarations must provide a non-zero owner module.", nameof(ownerModule));

            if (ownerEntityRef.IsDefault)
                throw new ArgumentException("Entity service declarations must provide an explicit owner entity.", nameof(ownerEntityRef));

            if (serviceId.Value == 0)
                throw new ArgumentException("Entity service declarations must provide a non-zero service identity.", nameof(serviceId));

            if (string.IsNullOrWhiteSpace(stableId))
                throw new ArgumentException("Entity service declarations must provide a stable identity.", nameof(stableId));

            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Entity service declarations must provide a stable service name.", nameof(serviceName));

            if (lifetime == ServiceLifetimeKind.Unknown)
                throw new ArgumentException("Entity service declarations must provide a service lifetime.", nameof(lifetime));

            if (factoryKind == ServiceFactoryKind.Unknown)
                throw new ArgumentException("Entity service declarations must provide a service factory kind.", nameof(factoryKind));

            if (sourceKind == UnityAuthoringSourceKind.Unknown)
                throw new ArgumentOutOfRangeException(nameof(sourceKind), sourceKind, "Entity service declarations must provide a defined Unity authoring source kind.");

            if (!source.IsSpecified)
                throw new ArgumentException("Entity service declarations must provide a specified source location.", nameof(source));

            OwnerModule = ownerModule;
            OwnerEntityRef = ownerEntityRef;
            ServiceId = serviceId;
            StableId = stableId.Trim();
            ServiceName = serviceName.Trim();
            DebugName = string.IsNullOrWhiteSpace(debugName) ? string.Empty : debugName.Trim();
            this.contractNames = CloneContractNames(contractNames);
            this.dependencies = CloneDependencies(dependencies);
            SourceKind = sourceKind;
            Lifetime = lifetime;
            FactoryKind = factoryKind;
            Source = source;
        }

        public ModuleId OwnerModule { get; }

        public EntityRef OwnerEntityRef { get; }

        public ServiceId ServiceId { get; }

        public string StableId { get; }

        public string ServiceName { get; }

        public string DebugName { get; }

        public ReadOnlySpan<string> ContractNames => contractNames;

        public ReadOnlySpan<EntityServiceDependencyInput> Dependencies => dependencies;

        public UnityAuthoringSourceKind SourceKind { get; }

        public ServiceLifetimeKind Lifetime { get; }

        public ServiceFactoryKind FactoryKind { get; }

        public SourceLocationIR Source { get; }

        static string[] CloneContractNames(string[]? source)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Entity service declarations must provide at least one contract name.", nameof(source));

            string[] clone = new string[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                string contractName = string.IsNullOrWhiteSpace(source[index]) ? string.Empty : source[index].Trim();
                if (contractName.Length == 0)
                    throw new ArgumentException("Entity service declaration contract names must be non-empty.", nameof(source));

                clone[index] = contractName;
            }

            Array.Sort(clone, StringComparer.Ordinal);

            for (int index = 1; index < clone.Length; index++)
            {
                if (StringComparer.Ordinal.Equals(clone[index - 1], clone[index]))
                    throw new ArgumentException("Entity service declaration contract names must be unique.", nameof(source));
            }

            return clone;
        }

        static EntityServiceDependencyInput[] CloneDependencies(EntityServiceDependencyInput[]? source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<EntityServiceDependencyInput>();

            EntityServiceDependencyInput[] clone = new EntityServiceDependencyInput[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                EntityServiceDependencyInput dependency = source[index];
                if (dependency.Target.Kind == DependencyNodeKind.Unknown)
                    throw new ArgumentException("Entity service declaration dependencies must not contain unknown targets.", nameof(source));

                clone[index] = dependency;
            }

            for (int index = 0; index < clone.Length; index++)
            {
                for (int inner = index + 1; inner < clone.Length; inner++)
                {
                    if (clone[index].Target == clone[inner].Target)
                        throw new ArgumentException("Entity service declaration dependencies must be unique.", nameof(source));
                }
            }

            return clone;
        }
    }

    public interface IEntityServiceDeclarationAuthoring
    {
        bool TryCreateServiceDeclarations(
            in EntityDeclarationPlanInput declarationInput,
            out EntityServiceDeclarationInput[] declarations,
            out string failureReason);
    }
}

#nullable enable

using System;
using System.Collections.Generic;
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

    public readonly struct ServiceLifecycleContributionInput
    {
        public ServiceLifecycleContributionInput(
            LifecyclePhase phase,
            int order,
            LifecycleActionKind action,
            string stableId,
            string debugName,
            SourceLocationIR source)
        {
            if (!Enum.IsDefined(typeof(LifecyclePhase), phase))
                throw new ArgumentException("Service lifecycle contributions must provide a defined lifecycle phase.", nameof(phase));

            if (order < 0)
                throw new ArgumentOutOfRangeException(nameof(order), order, "Service lifecycle contributions must provide a non-negative ordering value.");

            if (action == LifecycleActionKind.Unknown)
                throw new ArgumentException("Service lifecycle contributions must provide a defined lifecycle action.", nameof(action));

            if (string.IsNullOrWhiteSpace(stableId))
                throw new ArgumentException("Service lifecycle contributions must provide a stable identity.", nameof(stableId));

            if (string.IsNullOrWhiteSpace(debugName))
                throw new ArgumentException("Service lifecycle contributions must provide a debug name.", nameof(debugName));

            if (!source.IsSpecified)
                throw new ArgumentException("Service lifecycle contributions must provide a specified source location.", nameof(source));

            Phase = phase;
            Order = order;
            Action = action;
            StableId = stableId.Trim();
            DebugName = debugName.Trim();
            Source = source;
        }

        public LifecyclePhase Phase { get; }

        public int Order { get; }

        public LifecycleActionKind Action { get; }

        public string StableId { get; }

        public string DebugName { get; }

        public SourceLocationIR Source { get; }
    }

    public readonly struct EntityServiceDeclarationInput
    {
        readonly string[] contractNames;
        readonly EntityServiceDependencyInput[] dependencies;
        readonly ServiceLifecycleContributionInput[] lifecycleContributions;

        public EntityServiceDeclarationInput(
            ModuleId ownerModule,
            EntityRef ownerEntityRef,
            ServiceId serviceId,
            string stableId,
            string serviceName,
            string debugName,
            string[] contractNames,
            EntityServiceDependencyInput[]? dependencies,
            ServiceLifecycleContributionInput[]? lifecycleContributions,
            UnityAuthoringSourceKind sourceKind,
            ServiceLifetimeKind lifetime,
            ServiceFactoryKind factoryKind,
            SourceLocationIR source)
        {
            if (ownerModule.Value == 0)
                throw new ArgumentException("Entity service declarations must provide a non-zero owner module.", nameof(ownerModule));

            if (ownerEntityRef.IsEmpty)
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
            this.lifecycleContributions = CloneLifecycleContributions(lifecycleContributions);
        }

        public ModuleId OwnerModule { get; }

        public EntityRef OwnerEntityRef { get; }

        public ServiceId ServiceId { get; }

        public string StableId { get; }

        public string ServiceName { get; }

        public string DebugName { get; }

        public ReadOnlySpan<string> ContractNames => contractNames;

        public ReadOnlySpan<EntityServiceDependencyInput> Dependencies => dependencies;

        public ReadOnlySpan<ServiceLifecycleContributionInput> LifecycleContributions => lifecycleContributions;

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

        static ServiceLifecycleContributionInput[] CloneLifecycleContributions(ServiceLifecycleContributionInput[]? source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<ServiceLifecycleContributionInput>();

            ServiceLifecycleContributionInput[] clone = new ServiceLifecycleContributionInput[source.Length];
            HashSet<string> seenStableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < source.Length; index++)
            {
                ServiceLifecycleContributionInput contribution = source[index];
                if (!seenStableIds.Add(contribution.StableId))
                    throw new ArgumentException("Entity service declaration lifecycle contributions must be unique.", nameof(source));

                clone[index] = contribution;
            }

            Array.Sort(clone, CompareLifecycleContributions);
            return clone;
        }

        static int CompareLifecycleContributions(ServiceLifecycleContributionInput left, ServiceLifecycleContributionInput right)
        {
            int comparison = left.Phase.CompareTo(right.Phase);
            if (comparison != 0)
                return comparison;

            comparison = left.Order.CompareTo(right.Order);
            if (comparison != 0)
                return comparison;

            return StringComparer.Ordinal.Compare(left.StableId, right.StableId);
        }
    }

    public interface IEntityServiceDeclarationAuthoring
    {
        bool TryCreateServiceDeclarations(
            in EntityDeclarationPlanInput declarationInput,
            out EntityServiceDeclarationInput[] declarations,
            out string failureReason);
    }

    public readonly struct CommandDependencyDeclarationInput
    {
        public CommandDependencyDeclarationInput(DependencyNodeIR target, DependencyStrength strength, SourceLocationIR source)
        {
            if (target.Kind == DependencyNodeKind.Unknown)
                throw new ArgumentException("Command declaration dependencies must provide an explicit target.", nameof(target));

            if (!Enum.IsDefined(typeof(DependencyStrength), strength))
                throw new ArgumentOutOfRangeException(nameof(strength), strength, "Command declaration dependencies must provide a defined dependency strength.");

            if (!source.IsSpecified)
                throw new ArgumentException("Command declaration dependencies must provide a specified source location.", nameof(source));

            Target = target;
            Strength = strength;
            Source = source;
        }

        public DependencyNodeIR Target { get; }

        public DependencyStrength Strength { get; }

        public SourceLocationIR Source { get; }
    }

    public readonly struct CommandPayloadFieldDeclarationInput
    {
        public CommandPayloadFieldDeclarationInput(
            string fieldPath,
            CommandPayloadFieldKindIR kind,
            CommandPayloadFieldRequirementIR requirement,
            SourceLocationIR source,
            CommandPayloadReferenceKindIR referenceKind = CommandPayloadReferenceKindIR.None,
            bool allowNull = false)
        {
            string normalizedFieldPath = string.IsNullOrWhiteSpace(fieldPath) ? string.Empty : fieldPath.Trim();
            if (normalizedFieldPath.Length == 0)
                throw new ArgumentException("Command payload field declarations must provide a field path.", nameof(fieldPath));

            if (!Enum.IsDefined(typeof(CommandPayloadFieldKindIR), kind) || kind == CommandPayloadFieldKindIR.Unknown)
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Command payload field declarations must provide a defined field kind.");

            if (!Enum.IsDefined(typeof(CommandPayloadFieldRequirementIR), requirement))
                throw new ArgumentOutOfRangeException(nameof(requirement), requirement, "Command payload field declarations must provide a defined field requirement.");

            if (!Enum.IsDefined(typeof(CommandPayloadReferenceKindIR), referenceKind))
                throw new ArgumentOutOfRangeException(nameof(referenceKind), referenceKind, "Command payload field declarations must provide a defined reference kind.");

            if (!source.IsSpecified)
                throw new ArgumentException("Command payload field declarations must provide a specified source location.", nameof(source));

            FieldPath = normalizedFieldPath;
            Kind = kind;
            Requirement = requirement;
            ReferenceKind = referenceKind;
            AllowNull = allowNull;
            Source = source;
        }

        public string FieldPath { get; }

        public CommandPayloadFieldKindIR Kind { get; }

        public CommandPayloadFieldRequirementIR Requirement { get; }

        public CommandPayloadReferenceKindIR ReferenceKind { get; }

        public bool AllowNull { get; }

        public SourceLocationIR Source { get; }
    }

    public readonly struct CommandPayloadSchemaDeclarationInput
    {
        readonly CommandPayloadFieldDeclarationInput[] fields;

        public CommandPayloadSchemaDeclarationInput(
            CommandPayloadSchemaId schemaId,
            SourceLocationIR source,
            CommandPayloadUnknownFieldPolicyIR unknownFieldPolicy,
            CommandPayloadFieldDeclarationInput[]? fields)
        {
            if (schemaId.Value == 0)
                throw new ArgumentException("Command payload schema declarations must provide a non-zero schema identity.", nameof(schemaId));

            if (!Enum.IsDefined(typeof(CommandPayloadUnknownFieldPolicyIR), unknownFieldPolicy))
                throw new ArgumentOutOfRangeException(nameof(unknownFieldPolicy), unknownFieldPolicy, "Command payload schema declarations must provide a defined unknown-field policy.");

            if (!source.IsSpecified)
                throw new ArgumentException("Command payload schema declarations must provide a specified source location.", nameof(source));

            SchemaId = schemaId;
            Source = source;
            UnknownFieldPolicy = unknownFieldPolicy;
            this.fields = CloneFields(fields);
        }

        public CommandPayloadSchemaId SchemaId { get; }

        public SourceLocationIR Source { get; }

        public CommandPayloadUnknownFieldPolicyIR UnknownFieldPolicy { get; }

        public ReadOnlySpan<CommandPayloadFieldDeclarationInput> Fields => fields;

        static CommandPayloadFieldDeclarationInput[] CloneFields(CommandPayloadFieldDeclarationInput[]? source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<CommandPayloadFieldDeclarationInput>();

            CommandPayloadFieldDeclarationInput[] clone = new CommandPayloadFieldDeclarationInput[source.Length];
            HashSet<string> seenFieldPaths = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < source.Length; index++)
            {
                CommandPayloadFieldDeclarationInput field = source[index];
                if (!seenFieldPaths.Add(field.FieldPath))
                    throw new ArgumentException("Command payload schema declarations must not contain duplicate field paths.", nameof(source));

                clone[index] = field;
            }

            return clone;
        }
    }

    public readonly struct CommandDeclarationInput
    {
        readonly CommandDependencyDeclarationInput[] dependencies;

        public CommandDeclarationInput(
            ModuleId ownerModule,
            CommandTypeId typeId,
            string runtimeName,
            CommandAuthoringKeyId authoringKeyId,
            string stableId,
            SourceLocationIR authoringKeySource,
            CommandCategoryId categoryId,
            CommandPayloadSchemaDeclarationInput payloadSchema,
            CommandExecutorId executorId,
            SourceLocationIR executorSource,
            SourceLocationIR source,
            CommandDependencyDeclarationInput[]? dependencies = null)
        {
            if (ownerModule.Value == 0)
                throw new ArgumentException("Command declarations must provide a non-zero owner module.", nameof(ownerModule));

            if (typeId.Value == 0)
                throw new ArgumentException("Command declarations must provide a non-zero command type identity.", nameof(typeId));

            if (string.IsNullOrWhiteSpace(runtimeName))
                throw new ArgumentException("Command declarations must provide a runtime name.", nameof(runtimeName));

            if (authoringKeyId.Value == 0)
                throw new ArgumentException("Command declarations must provide a non-zero authoring key identity.", nameof(authoringKeyId));

            if (string.IsNullOrWhiteSpace(stableId))
                throw new ArgumentException("Command declarations must provide a stable identity.", nameof(stableId));

            if (categoryId.Value == 0)
                throw new ArgumentException("Command declarations must provide a non-zero command category identity.", nameof(categoryId));

            if (executorId.Value == 0)
                throw new ArgumentException("Command declarations must provide a non-zero command executor identity.", nameof(executorId));

            if (!authoringKeySource.IsSpecified)
                throw new ArgumentException("Command declarations must provide a specified authoring-key source location.", nameof(authoringKeySource));

            if (!executorSource.IsSpecified)
                throw new ArgumentException("Command declarations must provide a specified executor source location.", nameof(executorSource));

            if (!source.IsSpecified)
                throw new ArgumentException("Command declarations must provide a specified command source location.", nameof(source));

            OwnerModule = ownerModule;
            TypeId = typeId;
            RuntimeName = runtimeName.Trim();
            AuthoringKeyId = authoringKeyId;
            StableId = stableId.Trim();
            AuthoringKeySource = authoringKeySource;
            CategoryId = categoryId;
            PayloadSchema = payloadSchema;
            ExecutorId = executorId;
            ExecutorSource = executorSource;
            Source = source;
            this.dependencies = CloneDependencies(dependencies);
        }

        public ModuleId OwnerModule { get; }

        public CommandTypeId TypeId { get; }

        public string RuntimeName { get; }

        public CommandAuthoringKeyId AuthoringKeyId { get; }

        public string StableId { get; }

        public SourceLocationIR AuthoringKeySource { get; }

        public CommandCategoryId CategoryId { get; }

        public CommandPayloadSchemaDeclarationInput PayloadSchema { get; }

        public CommandExecutorId ExecutorId { get; }

        public SourceLocationIR ExecutorSource { get; }

        public SourceLocationIR Source { get; }

        public ReadOnlySpan<CommandDependencyDeclarationInput> Dependencies => dependencies;

        static CommandDependencyDeclarationInput[] CloneDependencies(CommandDependencyDeclarationInput[]? source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<CommandDependencyDeclarationInput>();

            CommandDependencyDeclarationInput[] clone = new CommandDependencyDeclarationInput[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                CommandDependencyDeclarationInput dependency = source[index];
                if (dependency.Target.Kind == DependencyNodeKind.Unknown)
                    throw new ArgumentException("Command declarations must not contain unknown dependency targets.", nameof(source));

                clone[index] = dependency;
            }

            for (int index = 0; index < clone.Length; index++)
            {
                for (int inner = index + 1; inner < clone.Length; inner++)
                {
                    if (clone[index].Target == clone[inner].Target)
                        throw new ArgumentException("Command declaration dependencies must be unique.", nameof(source));
                }
            }

            return clone;
        }
    }

    public interface ICommandDeclarationAuthoring
    {
        bool TryCreateCommandDeclarations(
            ModuleId ownerModule,
            out CommandDeclarationInput[] declarations,
            out string failureReason);
    }

    public sealed class CommandDeclarationBuildResult
    {
        readonly CommandIR[] commands;

        public CommandDeclarationBuildResult(CommandIR[] commands, SourceLocationTable sources)
        {
            if (commands == null || commands.Length == 0)
                throw new ArgumentException("Command declaration build results must contain at least one command.", nameof(commands));

            CommandIR[] clone = new CommandIR[commands.Length];
            for (int index = 0; index < commands.Length; index++)
            {
                clone[index] = commands[index] ?? throw new ArgumentException("Command declaration build results must not contain null commands.", nameof(commands));
            }

            this.commands = clone;
            Sources = sources ?? throw new ArgumentNullException(nameof(sources));
        }

        public ReadOnlySpan<CommandIR> Commands => commands;

        public SourceLocationTable Sources { get; }
    }

    public static class CommandDeclarationInputProjector
    {
        public static CommandDeclarationBuildResult Build(CommandDeclarationInput[] declarations)
        {
            if (declarations == null || declarations.Length == 0)
                throw new ArgumentException("Command declaration projection requires at least one declaration.", nameof(declarations));

            Dictionary<SourceLocationIR, SourceLocationId> sourceIds = new Dictionary<SourceLocationIR, SourceLocationId>();
            List<SourceLocationIR> sources = new List<SourceLocationIR>();
            CommandIR[] commands = new CommandIR[declarations.Length];

            for (int index = 0; index < declarations.Length; index++)
            {
                CommandDeclarationInput declaration = declarations[index];
                SourceLocationId commandSourceId = ResolveSourceId(declaration.Source, sourceIds, sources);
                SourceLocationId authoringKeySourceId = ResolveSourceId(declaration.AuthoringKeySource, sourceIds, sources);
                SourceLocationId executorSourceId = ResolveSourceId(declaration.ExecutorSource, sourceIds, sources);
                SourceLocationId payloadSchemaSourceId = ResolveSourceId(declaration.PayloadSchema.Source, sourceIds, sources);

                ReadOnlySpan<CommandPayloadFieldDeclarationInput> fieldDeclarations = declaration.PayloadSchema.Fields;
                CommandPayloadFieldIR[] payloadFields = new CommandPayloadFieldIR[fieldDeclarations.Length];
                for (int fieldIndex = 0; fieldIndex < fieldDeclarations.Length; fieldIndex++)
                {
                    CommandPayloadFieldDeclarationInput field = fieldDeclarations[fieldIndex];
                    payloadFields[fieldIndex] = new CommandPayloadFieldIR(
                        field.FieldPath,
                        field.Kind,
                        field.Requirement,
                        ResolveSourceId(field.Source, sourceIds, sources),
                        field.ReferenceKind,
                        field.AllowNull);
                }

                ReadOnlySpan<CommandDependencyDeclarationInput> dependencyDeclarations = declaration.Dependencies;
                CommandDependencyIR[] dependencies = new CommandDependencyIR[dependencyDeclarations.Length];
                for (int dependencyIndex = 0; dependencyIndex < dependencyDeclarations.Length; dependencyIndex++)
                {
                    CommandDependencyDeclarationInput dependency = dependencyDeclarations[dependencyIndex];
                    dependencies[dependencyIndex] = new CommandDependencyIR(
                        dependency.Target,
                        dependency.Strength,
                        ResolveSourceId(dependency.Source, sourceIds, sources));
                }

                commands[index] = new CommandIR(
                    declaration.TypeId,
                    declaration.RuntimeName,
                    new CommandAuthoringKeyRefIR(declaration.AuthoringKeyId, declaration.StableId, authoringKeySourceId),
                    declaration.CategoryId,
                    declaration.OwnerModule,
                    new CommandPayloadSchemaRefIR(declaration.PayloadSchema.SchemaId, payloadSchemaSourceId, payloadFields, declaration.PayloadSchema.UnknownFieldPolicy),
                    new CommandExecutorRefIR(declaration.ExecutorId, executorSourceId),
                    dependencies,
                    commandSourceId);
            }

            return new CommandDeclarationBuildResult(commands, new SourceLocationTable(sources.ToArray()));
        }

        static SourceLocationId ResolveSourceId(
            SourceLocationIR source,
            IDictionary<SourceLocationIR, SourceLocationId> sourceIds,
            ICollection<SourceLocationIR> sources)
        {
            if (sourceIds.TryGetValue(source, out SourceLocationId existing))
                return existing;

            SourceLocationId id = new SourceLocationId(sources.Count + 1);
            sourceIds.Add(source, id);
            sources.Add(source);
            return id;
        }
    }
}

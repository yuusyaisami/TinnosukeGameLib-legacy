#nullable enable
using System;

namespace Game.Kernel.IR
{
    public sealed class ModuleDependencyIR
    {
        public ModuleDependencyIR(
            ModuleId moduleId,
            SourceLocationId source,
            OptionalDependencyAbsenceBehavior? absenceBehavior = null,
            string? disabledContribution = null,
            ModuleId alternativeModuleId = default,
            KernelProfileMask profileSpecificErrorProfiles = KernelProfileMask.None)
        {
            if (moduleId.Value == 0)
                throw new ArgumentException("Module dependencies must provide a non-zero module identity.", nameof(moduleId));

            if (source.Value == 0)
                throw new ArgumentException("Module dependencies must provide a non-zero source location identity.", nameof(source));

            ValidateOptionalAbsenceMetadata(absenceBehavior, disabledContribution, alternativeModuleId, profileSpecificErrorProfiles);

            ModuleId = moduleId;
            Source = source;
            AbsenceBehavior = absenceBehavior;
            DisabledContribution = disabledContribution;
            AlternativeModuleId = alternativeModuleId;
            ProfileSpecificErrorProfiles = profileSpecificErrorProfiles;
        }

        public ModuleId ModuleId { get; }

        public SourceLocationId Source { get; }

        public OptionalDependencyAbsenceBehavior? AbsenceBehavior { get; }

        public string? DisabledContribution { get; }

        public ModuleId AlternativeModuleId { get; }

        public KernelProfileMask ProfileSpecificErrorProfiles { get; }

        static void ValidateOptionalAbsenceMetadata(
            OptionalDependencyAbsenceBehavior? absenceBehavior,
            string? disabledContribution,
            ModuleId alternativeModuleId,
            KernelProfileMask profileSpecificErrorProfiles)
        {
            if (disabledContribution != null && disabledContribution.Trim().Length == 0)
                throw new ArgumentException("Disabled contribution identifiers must be null or non-empty.", nameof(disabledContribution));

            if (!absenceBehavior.HasValue)
            {
                if (disabledContribution != null || alternativeModuleId.Value != 0 || profileSpecificErrorProfiles != KernelProfileMask.None)
                    throw new ArgumentException("Optional absence metadata requires an explicit absence behavior.", nameof(absenceBehavior));

                return;
            }

            switch (absenceBehavior.Value)
            {
                case OptionalDependencyAbsenceBehavior.DisableContribution:
                    if (string.IsNullOrWhiteSpace(disabledContribution))
                        throw new ArgumentException("DisableContribution behavior must identify the disabled contribution or projection.", nameof(disabledContribution));

                    if (alternativeModuleId.Value != 0 || profileSpecificErrorProfiles != KernelProfileMask.None)
                        throw new ArgumentException("DisableContribution behavior must not declare alternative modules or profile-specific error boundaries.", nameof(absenceBehavior));

                    return;

                case OptionalDependencyAbsenceBehavior.EmitWarning:
                    if (disabledContribution != null || alternativeModuleId.Value != 0 || profileSpecificErrorProfiles != KernelProfileMask.None)
                        throw new ArgumentException("EmitWarning behavior must not declare additional absence metadata.", nameof(absenceBehavior));

                    return;

                case OptionalDependencyAbsenceBehavior.UseExplicitAlternative:
                    if (alternativeModuleId.Value == 0)
                        throw new ArgumentException("UseExplicitAlternative behavior must declare an alternative module identity.", nameof(alternativeModuleId));

                    if (disabledContribution != null || profileSpecificErrorProfiles != KernelProfileMask.None)
                        throw new ArgumentException("UseExplicitAlternative behavior must not declare disabled contributions or profile-specific error boundaries.", nameof(absenceBehavior));

                    return;

                case OptionalDependencyAbsenceBehavior.ProfileSpecificError:
                    if (profileSpecificErrorProfiles == KernelProfileMask.None)
                        throw new ArgumentException("ProfileSpecificError behavior must declare at least one profile boundary.", nameof(profileSpecificErrorProfiles));

                    if (disabledContribution != null || alternativeModuleId.Value != 0)
                        throw new ArgumentException("ProfileSpecificError behavior must not declare disabled contributions or alternative modules.", nameof(absenceBehavior));

                    return;

                default:
                    throw new ArgumentOutOfRangeException(nameof(absenceBehavior), absenceBehavior.Value, "Unsupported optional absence behavior.");
            }
        }
    }

    public sealed class LegacyCompatDescriptorIR
    {
        public LegacyCompatDescriptorIR(
            LegacyCompatKind kind,
            string legacySystemName,
            string targetSubsystem,
            KernelProfileMask profiles,
            LegacyRemovalStatus removalStatus,
            string? diagnosticsCode = null,
            string? removalCondition = null)
        {
            if (kind == LegacyCompatKind.None)
                throw new ArgumentException("Legacy compatibility descriptors must provide a bridge kind.", nameof(kind));

            if (string.IsNullOrWhiteSpace(legacySystemName))
                throw new ArgumentException("Legacy compatibility descriptors must provide a legacy system name.", nameof(legacySystemName));

            if (string.IsNullOrWhiteSpace(targetSubsystem))
                throw new ArgumentException("Legacy compatibility descriptors must provide a target subsystem.", nameof(targetSubsystem));

            if (profiles == KernelProfileMask.None)
                throw new ArgumentException("Legacy compatibility descriptors must declare profile availability.", nameof(profiles));

            if (removalStatus == LegacyRemovalStatus.Unknown)
                throw new ArgumentException("Legacy compatibility descriptors must declare a removal status.", nameof(removalStatus));

            if (diagnosticsCode != null && string.IsNullOrWhiteSpace(diagnosticsCode))
                throw new ArgumentException("Legacy compatibility diagnostics codes must be null or non-empty.", nameof(diagnosticsCode));

            if (removalCondition != null && string.IsNullOrWhiteSpace(removalCondition))
                throw new ArgumentException("Legacy compatibility removal conditions must be null or non-empty.", nameof(removalCondition));

            Kind = kind;
            LegacySystemName = legacySystemName;
            TargetSubsystem = targetSubsystem;
            Profiles = profiles;
            RemovalStatus = removalStatus;
            DiagnosticsCode = diagnosticsCode;
            RemovalCondition = removalCondition;
        }

        public LegacyCompatKind Kind { get; }

        public string LegacySystemName { get; }

        public string TargetSubsystem { get; }

        public KernelProfileMask Profiles { get; }

        public LegacyRemovalStatus RemovalStatus { get; }

        public string? DiagnosticsCode { get; }

        public string? RemovalCondition { get; }
    }

    public sealed class ModuleIR
    {
        readonly ModuleDependencyIR[] requiredModules;
        readonly ModuleDependencyIR[] optionalModules;

        public ModuleIR(
            ModuleId id,
            string name,
            ModuleKind kind,
            ModuleVersion version,
            ModuleAvailabilityIR availability,
            SourceLocationId source,
            ModuleDependencyIR[]? requiredModules = null,
            ModuleDependencyIR[]? optionalModules = null,
            LegacyCompatDescriptorIR? legacyCompat = null)
        {
            if (id.Value == 0)
                throw new ArgumentException("Module IR must provide a non-zero module identity.", nameof(id));

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Module IR must provide a stable name.", nameof(name));

            if (kind == ModuleKind.Unknown)
                throw new ArgumentException("Module IR must provide a module kind.", nameof(kind));

            if (source.Value == 0)
                throw new ArgumentException("Module IR must provide a non-zero source location identity.", nameof(source));

            if (legacyCompat != null && kind != ModuleKind.MigrationAdapter && kind != ModuleKind.Bridge)
                throw new ArgumentException("Legacy compatibility descriptors may be declared only on bridge or migration-adapter modules.", nameof(legacyCompat));

            if (kind == ModuleKind.MigrationAdapter && legacyCompat == null)
                throw new ArgumentException("Migration-adapter modules must declare explicit legacy compatibility metadata.", nameof(legacyCompat));

            Id = id;
            Name = name;
            Kind = kind;
            Version = version;
            Availability = availability;
            Source = source;
            this.requiredModules = KernelIRNodeArrayUtilities.CloneModuleDependencies(requiredModules);
            this.optionalModules = KernelIRNodeArrayUtilities.CloneModuleDependencies(optionalModules);
            LegacyCompat = legacyCompat;
        }

        public ModuleId Id { get; }

        public string Name { get; }

        public ModuleKind Kind { get; }

        public ModuleVersion Version { get; }

        public ModuleAvailabilityIR Availability { get; }

        public SourceLocationId Source { get; }

        public LegacyCompatDescriptorIR? LegacyCompat { get; }

        public ReadOnlySpan<ModuleDependencyIR> RequiredModules => requiredModules;

        public ReadOnlySpan<ModuleDependencyIR> OptionalModules => optionalModules;
    }

    public sealed class ScopeServiceRequirementIR
    {
        public ScopeServiceRequirementIR(ServiceId serviceId, DependencyStrength strength, SourceLocationId source)
        {
            if (serviceId.Value == 0)
                throw new ArgumentException("Scope service requirements must provide a non-zero service identity.", nameof(serviceId));

            if (source.Value == 0)
                throw new ArgumentException("Scope service requirements must provide a non-zero source location identity.", nameof(source));

            ServiceId = serviceId;
            Strength = strength;
            Source = source;
        }

        public ServiceId ServiceId { get; }

        public DependencyStrength Strength { get; }

        public SourceLocationId Source { get; }
    }

    public sealed class ScopeValueInitRefIR
    {
        public ScopeValueInitRefIR(ValueInitPlanId planId, SourceLocationId source)
        {
            if (planId.Value == 0)
                throw new ArgumentException("Scope value init refs must provide a non-zero plan identity.", nameof(planId));

            if (source.Value == 0)
                throw new ArgumentException("Scope value init refs must provide a non-zero source location identity.", nameof(source));

            PlanId = planId;
            Source = source;
        }

        public ValueInitPlanId PlanId { get; }

        public SourceLocationId Source { get; }
    }

    public sealed class LifecyclePlanRefIR
    {
        public LifecyclePlanRefIR(LifecyclePlanId planId, SourceLocationId source)
        {
            if (planId.Value == 0)
                throw new ArgumentException("Lifecycle plan refs must provide a non-zero plan identity.", nameof(planId));

            if (source.Value == 0)
                throw new ArgumentException("Lifecycle plan refs must provide a non-zero source location identity.", nameof(source));

            PlanId = planId;
            Source = source;
        }

        public LifecyclePlanId PlanId { get; }

        public SourceLocationId Source { get; }
    }

    public sealed class ScopeIR
    {
        readonly ScopeServiceRequirementIR[] requiredServices;
        readonly ScopeValueInitRefIR[] valueInitPlans;

        public ScopeIR(ScopeAuthoringId authoringId, ScopePlanId planId, string name, ScopeKind kind, ModuleId ownerModule, ScopeAuthoringId parentAuthoringId, ScopeServiceRequirementIR[]? requiredServices, ScopeValueInitRefIR[]? valueInitPlans, LifecyclePlanRefIR lifecycle, SourceLocationId source)
        {
            if (authoringId.Value == 0)
                throw new ArgumentException("Scope IR must provide a non-zero authoring identity.", nameof(authoringId));

            if (planId.Value == 0)
                throw new ArgumentException("Scope IR must provide a non-zero plan identity.", nameof(planId));

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Scope IR must provide a stable name.", nameof(name));

            if (kind == ScopeKind.Unknown)
                throw new ArgumentException("Scope IR must provide a scope kind.", nameof(kind));

            if (ownerModule.Value == 0)
                throw new ArgumentException("Scope IR must provide a non-zero owner module identity.", nameof(ownerModule));

            if (source.Value == 0)
                throw new ArgumentException("Scope IR must provide a non-zero source location identity.", nameof(source));

            AuthoringId = authoringId;
            PlanId = planId;
            Name = name;
            Kind = kind;
            OwnerModule = ownerModule;
            ParentAuthoringId = parentAuthoringId;
            this.requiredServices = KernelIRNodeArrayUtilities.CloneArray(requiredServices);
            this.valueInitPlans = KernelIRNodeArrayUtilities.CloneArray(valueInitPlans);
            Lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            Source = source;
        }

        public ScopeAuthoringId AuthoringId { get; }

        public ScopePlanId PlanId { get; }

        public string Name { get; }

        public ScopeKind Kind { get; }

        public ModuleId OwnerModule { get; }

        public ScopeAuthoringId ParentAuthoringId { get; }

        public ReadOnlySpan<ScopeServiceRequirementIR> RequiredServices => requiredServices;

        public ReadOnlySpan<ScopeValueInitRefIR> ValueInitPlans => valueInitPlans;

        public LifecyclePlanRefIR Lifecycle { get; }

        public SourceLocationId Source { get; }
    }

    public sealed class ServiceContractIR
    {
        public ServiceContractIR(string contractName, SourceLocationId source)
        {
            if (string.IsNullOrWhiteSpace(contractName))
                throw new ArgumentException("Service contracts must provide a contract name.", nameof(contractName));

            if (source.Value == 0)
                throw new ArgumentException("Service contracts must provide a non-zero source location identity.", nameof(source));

            ContractName = contractName;
            Source = source;
        }

        public string ContractName { get; }

        public SourceLocationId Source { get; }
    }

    public sealed class ServiceDependencyIR
    {
        public ServiceDependencyIR(DependencyNodeIR target, DependencyStrength strength, SourceLocationId source)
        {
            if (source.Value == 0)
                throw new ArgumentException("Service dependencies must provide a non-zero source location identity.", nameof(source));

            Target = target;
            Strength = strength;
            Source = source;
        }

        public DependencyNodeIR Target { get; }

        public DependencyStrength Strength { get; }

        public SourceLocationId Source { get; }
    }

    public sealed class ServiceIR
    {
        readonly ServiceContractIR[] contracts;
        readonly ServiceDependencyIR[] dependencies;

        public ServiceIR(ServiceId id, string name, ServiceLifetimeKind lifetime, ModuleId ownerModule, ServiceContractIR[]? contracts, ServiceDependencyIR[]? dependencies, ServiceFactoryKind factoryKind, SourceLocationId source)
        {
            if (id.Value == 0)
                throw new ArgumentException("Service IR must provide a non-zero service identity.", nameof(id));

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Service IR must provide a stable name.", nameof(name));

            if (lifetime == ServiceLifetimeKind.Unknown)
                throw new ArgumentException("Service IR must provide a service lifetime.", nameof(lifetime));

            if (ownerModule.Value == 0)
                throw new ArgumentException("Service IR must provide a non-zero owner module identity.", nameof(ownerModule));

            if (factoryKind == ServiceFactoryKind.Unknown)
                throw new ArgumentException("Service IR must provide a service factory kind.", nameof(factoryKind));

            if (source.Value == 0)
                throw new ArgumentException("Service IR must provide a non-zero source location identity.", nameof(source));

            Id = id;
            Name = name;
            Lifetime = lifetime;
            OwnerModule = ownerModule;
            this.contracts = KernelIRNodeArrayUtilities.CloneArray(contracts);
            this.dependencies = KernelIRNodeArrayUtilities.CloneArray(dependencies);
            FactoryKind = factoryKind;
            Source = source;
        }

        public ServiceId Id { get; }

        public string Name { get; }

        public ServiceLifetimeKind Lifetime { get; }

        public ModuleId OwnerModule { get; }

        public ReadOnlySpan<ServiceContractIR> Contracts => contracts;

        public ReadOnlySpan<ServiceDependencyIR> Dependencies => dependencies;

        public ServiceFactoryKind FactoryKind { get; }

        public SourceLocationId Source { get; }
    }

    public sealed class CommandPayloadSchemaRefIR
    {
        public CommandPayloadSchemaRefIR(CommandPayloadSchemaId id, SourceLocationId source)
        {
            if (id.Value == 0)
                throw new ArgumentException("Command payload schema refs must provide a non-zero schema identity.", nameof(id));

            if (source.Value == 0)
                throw new ArgumentException("Command payload schema refs must provide a non-zero source location identity.", nameof(source));

            Id = id;
            Source = source;
        }

        public CommandPayloadSchemaId Id { get; }

        public SourceLocationId Source { get; }
    }

    public sealed class CommandExecutorRefIR
    {
        public CommandExecutorRefIR(CommandExecutorId id, SourceLocationId source)
        {
            if (id.Value == 0)
                throw new ArgumentException("Command executor refs must provide a non-zero executor identity.", nameof(id));

            if (source.Value == 0)
                throw new ArgumentException("Command executor refs must provide a non-zero source location identity.", nameof(source));

            Id = id;
            Source = source;
        }

        public CommandExecutorId Id { get; }

        public SourceLocationId Source { get; }
    }

    public sealed class CommandDependencyIR
    {
        public CommandDependencyIR(DependencyNodeIR target, DependencyStrength strength, SourceLocationId source)
        {
            if (source.Value == 0)
                throw new ArgumentException("Command dependencies must provide a non-zero source location identity.", nameof(source));

            Target = target;
            Strength = strength;
            Source = source;
        }

        public DependencyNodeIR Target { get; }

        public DependencyStrength Strength { get; }

        public SourceLocationId Source { get; }
    }

    public sealed class CommandIR
    {
        readonly CommandDependencyIR[] dependencies;

        public CommandIR(CommandTypeId typeId, string runtimeName, string authoringKey, CommandCategoryId categoryId, ModuleId ownerModule, CommandPayloadSchemaRefIR payloadSchema, CommandExecutorRefIR executor, CommandDependencyIR[]? dependencies, SourceLocationId source)
        {
            if (typeId.Value == 0)
                throw new ArgumentException("Command IR must provide a non-zero command type identity.", nameof(typeId));

            if (string.IsNullOrWhiteSpace(runtimeName))
                throw new ArgumentException("Command IR must provide a runtime name.", nameof(runtimeName));

            if (string.IsNullOrWhiteSpace(authoringKey))
                throw new ArgumentException("Command IR must provide an authoring key.", nameof(authoringKey));

            if (categoryId.Value == 0)
                throw new ArgumentException("Command IR must provide a non-zero command category identity.", nameof(categoryId));

            if (ownerModule.Value == 0)
                throw new ArgumentException("Command IR must provide a non-zero owner module identity.", nameof(ownerModule));

            if (source.Value == 0)
                throw new ArgumentException("Command IR must provide a non-zero source location identity.", nameof(source));

            TypeId = typeId;
            RuntimeName = runtimeName;
            AuthoringKey = authoringKey;
            CategoryId = categoryId;
            OwnerModule = ownerModule;
            PayloadSchema = payloadSchema ?? throw new ArgumentNullException(nameof(payloadSchema));
            Executor = executor ?? throw new ArgumentNullException(nameof(executor));
            this.dependencies = KernelIRNodeArrayUtilities.CloneArray(dependencies);
            Source = source;
        }

        public CommandTypeId TypeId { get; }

        public string RuntimeName { get; }

        public string AuthoringKey { get; }

        public CommandCategoryId CategoryId { get; }

        public ModuleId OwnerModule { get; }

        public CommandPayloadSchemaRefIR PayloadSchema { get; }

        public CommandExecutorRefIR Executor { get; }

        public ReadOnlySpan<CommandDependencyIR> Dependencies => dependencies;

        public SourceLocationId Source { get; }
    }

    public sealed class ValueSchemaRefIR
    {
        public ValueSchemaRefIR(ValueSchemaId id, SourceLocationId source)
        {
            if (id.Value == 0)
                throw new ArgumentException("Value schema refs must provide a non-zero schema identity.", nameof(id));

            if (source.Value == 0)
                throw new ArgumentException("Value schema refs must provide a non-zero source location identity.", nameof(source));

            Id = id;
            Source = source;
        }

        public ValueSchemaId Id { get; }

        public SourceLocationId Source { get; }
    }

    public sealed class SavePolicyIR
    {
        public SavePolicyIR(bool persists, bool saveAcrossProfiles, string? channel)
        {
            if (channel != null && channel.Trim().Length == 0)
                throw new ArgumentException("Save policy channels must be null or non-empty.", nameof(channel));

            Persists = persists;
            SaveAcrossProfiles = saveAcrossProfiles;
            Channel = channel;
        }

        public bool Persists { get; }

        public bool SaveAcrossProfiles { get; }

        public string? Channel { get; }
    }

    public sealed class ValueKeyIR
    {
        public ValueKeyIR(ValueKeyId id, string stableKey, string displayName, ValueKind kind, ModuleId ownerModule, ValueSchemaRefIR schema, SavePolicyIR savePolicy, SourceLocationId source)
        {
            if (id.Value == 0)
                throw new ArgumentException("Value key IR must provide a non-zero value key identity.", nameof(id));

            if (string.IsNullOrWhiteSpace(stableKey))
                throw new ArgumentException("Value key IR must provide a stable key.", nameof(stableKey));

            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Value key IR must provide a display name.", nameof(displayName));

            if (kind == ValueKind.Null)
                throw new ArgumentException("Value key IR must provide a non-null value kind.", nameof(kind));

            if (ownerModule.Value == 0)
                throw new ArgumentException("Value key IR must provide a non-zero owner module identity.", nameof(ownerModule));

            if (source.Value == 0)
                throw new ArgumentException("Value key IR must provide a non-zero source location identity.", nameof(source));

            Id = id;
            StableKey = stableKey;
            DisplayName = displayName;
            Kind = kind;
            OwnerModule = ownerModule;
            Schema = schema ?? throw new ArgumentNullException(nameof(schema));
            SavePolicy = savePolicy ?? throw new ArgumentNullException(nameof(savePolicy));
            Source = source;
        }

        public ValueKeyId Id { get; }

        public string StableKey { get; }

        public string DisplayName { get; }

        public ValueKind Kind { get; }

        public ModuleId OwnerModule { get; }

        public ValueSchemaRefIR Schema { get; }

        public SavePolicyIR SavePolicy { get; }

        public SourceLocationId Source { get; }
    }

    public sealed class LifecycleTargetRefIR
    {
        public LifecycleTargetRefIR(ServiceId targetService)
            : this(LifecycleTargetKind.Service, targetService, default, default, null)
        {
        }

        public LifecycleTargetRefIR(ScopePlanId targetScope)
            : this(LifecycleTargetKind.Scope, default, targetScope, default, null)
        {
        }

        public LifecycleTargetRefIR(RuntimeQueryId targetRuntimeQuery)
            : this(LifecycleTargetKind.RuntimeQuery, default, default, targetRuntimeQuery, null)
        {
        }

        public LifecycleTargetRefIR(LifecycleTargetKind kind, string targetLocalRef)
            : this(kind, default, default, default, targetLocalRef)
        {
        }

        LifecycleTargetRefIR(LifecycleTargetKind kind, ServiceId targetService, ScopePlanId targetScope, RuntimeQueryId targetRuntimeQuery, string? targetLocalRef)
        {
            Validate(kind, targetService, targetScope, targetRuntimeQuery, targetLocalRef);
            Kind = kind;
            TargetService = targetService;
            TargetScope = targetScope;
            TargetRuntimeQuery = targetRuntimeQuery;
            TargetLocalRef = targetLocalRef;
        }

        public LifecycleTargetKind Kind { get; }

        public ServiceId TargetService { get; }

        public ScopePlanId TargetScope { get; }

        public RuntimeQueryId TargetRuntimeQuery { get; }

        public string? TargetLocalRef { get; }

        static void Validate(LifecycleTargetKind kind, ServiceId targetService, ScopePlanId targetScope, RuntimeQueryId targetRuntimeQuery, string? targetLocalRef)
        {
            int populatedCount = 0;
            populatedCount += targetService.Value != 0 ? 1 : 0;
            populatedCount += targetScope.Value != 0 ? 1 : 0;
            populatedCount += targetRuntimeQuery.Value != 0 ? 1 : 0;
            populatedCount += !string.IsNullOrWhiteSpace(targetLocalRef) ? 1 : 0;

            if (kind == LifecycleTargetKind.Service && targetService.Value != 0 && populatedCount == 1)
                return;

            if (kind == LifecycleTargetKind.Scope && targetScope.Value != 0 && populatedCount == 1)
                return;

            if (kind == LifecycleTargetKind.RuntimeQuery && targetRuntimeQuery.Value != 0 && populatedCount == 1)
                return;

            if ((kind == LifecycleTargetKind.ValueStore || kind == LifecycleTargetKind.RuntimeObjectOwner || kind == LifecycleTargetKind.LegacyAdapter)
                && !string.IsNullOrWhiteSpace(targetLocalRef)
                && populatedCount == 1)
            {
                return;
            }

            throw new ArgumentException("Lifecycle targets must carry exactly one meaningful reference for the selected target kind.", nameof(kind));
        }
    }

    public sealed class LifecycleStepIR
    {
        readonly DependencyEdgeId[] dependencies;

        public LifecycleStepIR(LifecycleStepId id, LifecyclePhase phase, int order, LifecycleTargetRefIR target, LifecycleActionKind action, DependencyEdgeId[]? dependencies, SourceLocationId source)
        {
            if (id.Value == 0)
                throw new ArgumentException("Lifecycle steps must provide a non-zero step identity.", nameof(id));

            if (action == LifecycleActionKind.Unknown)
                throw new ArgumentException("Lifecycle steps must provide an action kind.", nameof(action));

            if (source.Value == 0)
                throw new ArgumentException("Lifecycle steps must provide a non-zero source location identity.", nameof(source));

            Id = id;
            Phase = phase;
            Order = order;
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Action = action;
            this.dependencies = KernelIRNodeArrayUtilities.CloneDependencyIds(dependencies);
            Source = source;
        }

        public LifecycleStepId Id { get; }

        public LifecyclePhase Phase { get; }

        public int Order { get; }

        public LifecycleTargetRefIR Target { get; }

        public LifecycleActionKind Action { get; }

        public ReadOnlySpan<DependencyEdgeId> Dependencies => dependencies;

        public SourceLocationId Source { get; }
    }

    public sealed class LifecycleIR
    {
        readonly LifecycleStepIR[] steps;

        public LifecycleIR(LifecyclePlanId planId, string name, ModuleId ownerModule, LifecycleStepIR[] steps, SourceLocationId source)
        {
            if (planId.Value == 0)
                throw new ArgumentException("Lifecycle IR must provide a non-zero plan identity.", nameof(planId));

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Lifecycle IR must provide a stable name.", nameof(name));

            if (ownerModule.Value == 0)
                throw new ArgumentException("Lifecycle IR must provide a non-zero owner module identity.", nameof(ownerModule));

            if (steps == null || steps.Length == 0)
                throw new ArgumentException("Lifecycle IR must contain at least one lifecycle step.", nameof(steps));

            if (source.Value == 0)
                throw new ArgumentException("Lifecycle IR must provide a non-zero source location identity.", nameof(source));

            PlanId = planId;
            Name = name;
            OwnerModule = ownerModule;
            this.steps = KernelIRNodeArrayUtilities.CloneArray(steps);
            Source = source;
        }

        public LifecyclePlanId PlanId { get; }

        public string Name { get; }

        public ModuleId OwnerModule { get; }

        public ReadOnlySpan<LifecycleStepIR> Steps => steps;

        public SourceLocationId Source { get; }
    }

    public sealed class RuntimeIdentityFieldIR
    {
        public RuntimeIdentityFieldIR(string name, string valueType, bool isRequired)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Runtime identity fields must provide a name.", nameof(name));

            if (string.IsNullOrWhiteSpace(valueType))
                throw new ArgumentException("Runtime identity fields must provide a value type.", nameof(valueType));

            Name = name;
            ValueType = valueType;
            IsRequired = isRequired;
        }

        public string Name { get; }

        public string ValueType { get; }

        public bool IsRequired { get; }
    }

    public sealed class RuntimeQueryPolicyIR
    {
        public RuntimeQueryPolicyIR(bool requiresUniqueResult, bool allowMissing, DependencyPhase updatePhase)
        {
            RequiresUniqueResult = requiresUniqueResult;
            AllowMissing = allowMissing;
            UpdatePhase = updatePhase;
        }

        public bool RequiresUniqueResult { get; }

        public bool AllowMissing { get; }

        public DependencyPhase UpdatePhase { get; }
    }

    public sealed class RuntimeQueryIR
    {
        readonly RuntimeIdentityFieldIR[] indexedFields;

        public RuntimeQueryIR(RuntimeQueryId id, string name, RuntimeQueryTargetKind targetKind, RuntimeIdentityFieldIR[] indexedFields, RuntimeQueryPolicyIR policy, ModuleId ownerModule, SourceLocationId source)
        {
            if (id.Value == 0)
                throw new ArgumentException("Runtime query IR must provide a non-zero runtime query identity.", nameof(id));

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Runtime query IR must provide a stable name.", nameof(name));

            if (targetKind == RuntimeQueryTargetKind.Unknown)
                throw new ArgumentException("Runtime query IR must provide a target kind.", nameof(targetKind));

            if (indexedFields == null || indexedFields.Length == 0)
                throw new ArgumentException("Runtime query IR must contain at least one indexed field.", nameof(indexedFields));

            if (ownerModule.Value == 0)
                throw new ArgumentException("Runtime query IR must provide a non-zero owner module identity.", nameof(ownerModule));

            if (source.Value == 0)
                throw new ArgumentException("Runtime query IR must provide a non-zero source location identity.", nameof(source));

            Id = id;
            Name = name;
            TargetKind = targetKind;
            this.indexedFields = KernelIRNodeArrayUtilities.CloneArray(indexedFields);
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
            OwnerModule = ownerModule;
            Source = source;
        }

        public RuntimeQueryId Id { get; }

        public string Name { get; }

        public RuntimeQueryTargetKind TargetKind { get; }

        public ReadOnlySpan<RuntimeIdentityFieldIR> IndexedFields => indexedFields;

        public RuntimeQueryPolicyIR Policy { get; }

        public ModuleId OwnerModule { get; }

        public SourceLocationId Source { get; }
    }

    static class KernelIRNodeArrayUtilities
    {
        public static T[] CloneArray<T>(T[]? source) where T : class
        {
            if (source == null || source.Length == 0)
                return Array.Empty<T>();

            T[] clone = new T[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                clone[i] = source[i] ?? throw new ArgumentException("IR helper arrays must not contain null items.", nameof(source));
            }

            return clone;
        }

        public static DependencyEdgeId[] CloneDependencyIds(DependencyEdgeId[]? source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<DependencyEdgeId>();

            DependencyEdgeId[] clone = new DependencyEdgeId[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i].Value == 0)
                    throw new ArgumentException("Lifecycle step dependency ids must be non-zero.", nameof(source));

                clone[i] = source[i];
            }

            return clone;
        }

        public static ModuleDependencyIR[] CloneModuleDependencies(ModuleDependencyIR[]? source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<ModuleDependencyIR>();

            ModuleDependencyIR[] clone = new ModuleDependencyIR[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                clone[i] = source[i] ?? throw new ArgumentException("Module dependency arrays must not contain null items.", nameof(source));
            }

            return clone;
        }
    }
}

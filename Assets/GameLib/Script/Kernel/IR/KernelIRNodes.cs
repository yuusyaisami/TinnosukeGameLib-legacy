#nullable enable
using System;
using System.Collections.Generic;

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
        readonly DependencyNodeIR[] explicitTargets;

        public LegacyCompatDescriptorIR(
            LegacyCompatKind kind,
            string legacySystemName,
            string targetSubsystem,
            KernelProfileMask profiles,
            LegacyRemovalStatus removalStatus,
            string? diagnosticsCode = null,
            string? removalCondition = null,
            string? trackingIssueOrBlockingCondition = null,
            LegacyAdapterSurface surface = LegacyAdapterSurface.None,
            string? legacySourceType = null,
            DependencyNodeIR[]? explicitTargets = null)
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

            if (trackingIssueOrBlockingCondition != null && string.IsNullOrWhiteSpace(trackingIssueOrBlockingCondition))
                throw new ArgumentException("Legacy compatibility tracking issue or blocking conditions must be null or non-empty.", nameof(trackingIssueOrBlockingCondition));

            if (legacySourceType != null && string.IsNullOrWhiteSpace(legacySourceType))
                throw new ArgumentException("Legacy compatibility source types must be null or non-empty.", nameof(legacySourceType));

            this.explicitTargets = CloneExplicitTargets(explicitTargets);

            if (surface != LegacyAdapterSurface.None && this.explicitTargets.Length == 0)
                throw new ArgumentException("Legacy compatibility descriptors with a classified surface must declare explicit target nodes.", nameof(explicitTargets));

            Kind = kind;
            LegacySystemName = legacySystemName;
            TargetSubsystem = targetSubsystem;
            Profiles = profiles;
            RemovalStatus = removalStatus;
            DiagnosticsCode = diagnosticsCode;
            RemovalCondition = removalCondition;
            TrackingIssueOrBlockingCondition = trackingIssueOrBlockingCondition;
            Surface = surface;
            LegacySourceType = legacySourceType;
        }

        public LegacyCompatKind Kind { get; }

        public string LegacySystemName { get; }

        public string TargetSubsystem { get; }

        public KernelProfileMask Profiles { get; }

        public LegacyRemovalStatus RemovalStatus { get; }

        public string? DiagnosticsCode { get; }

        public string? RemovalCondition { get; }

        public string? TrackingIssueOrBlockingCondition { get; }

        public LegacyAdapterSurface Surface { get; }

        public string? LegacySourceType { get; }

        public ReadOnlySpan<DependencyNodeIR> ExplicitTargets => explicitTargets;

        static DependencyNodeIR[] CloneExplicitTargets(DependencyNodeIR[]? source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<DependencyNodeIR>();

            DependencyNodeIR[] clone = new DependencyNodeIR[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                if (source[index].Kind == DependencyNodeKind.Unknown)
                    throw new ArgumentException("Legacy compatibility explicit targets must not contain unknown dependency nodes.", nameof(source));

                clone[index] = source[index];
            }

            return clone;
        }
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

    public sealed class ScopeServiceBoundaryIR
    {
        public ScopeServiceBoundaryIR(ScopeServiceBoundaryKind kind, int expectedInstanceCount, SourceLocationId source)
        {
            if (kind == ScopeServiceBoundaryKind.Unknown)
                throw new ArgumentException("Scope service boundaries must provide a boundary kind.", nameof(kind));

            if (expectedInstanceCount < 0)
                throw new ArgumentOutOfRangeException(nameof(expectedInstanceCount), expectedInstanceCount, "Scope service boundaries must provide a non-negative expected instance count.");

            if (source.Value == 0)
                throw new ArgumentException("Scope service boundaries must provide a non-zero source location identity.", nameof(source));

            if (kind == ScopeServiceBoundaryKind.Detached && expectedInstanceCount != 0)
                throw new ArgumentException("Detached scope service boundaries must not expect service graph instances.", nameof(expectedInstanceCount));

            if (kind == ScopeServiceBoundaryKind.OwnedLocal && expectedInstanceCount <= 0)
                throw new ArgumentException("Owned local scope service boundaries must expect at least one service graph instance.", nameof(expectedInstanceCount));

            if (kind == ScopeServiceBoundaryKind.ReferencesParent && expectedInstanceCount != 0)
                throw new ArgumentException("Parent-referencing scope service boundaries must not expect local service graph instances.", nameof(expectedInstanceCount));

            Kind = kind;
            ExpectedInstanceCount = expectedInstanceCount;
            Source = source;
        }

        public ScopeServiceBoundaryKind Kind { get; }

        public int ExpectedInstanceCount { get; }

        public SourceLocationId Source { get; }

        public bool OwnsLocalServiceGraph => Kind == ScopeServiceBoundaryKind.OwnedLocal;

        public bool ReferencesParentServiceGraph => Kind == ScopeServiceBoundaryKind.ReferencesParent;

        public bool IsDetached => Kind == ScopeServiceBoundaryKind.Detached;
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

    public sealed class ValueInitEntryIR
    {
        public ValueInitEntryIR(
            ValueKeyId keyId,
            ValueInitEntrySourceKind sourceKind,
            ValueKind valueKind,
            int order,
            ValueInitOverwritePolicy overwritePolicy,
            SourceLocationId source,
            string? serializedValue = null,
            string? evaluationLocalRef = null)
        {
            if (keyId.Value == 0)
                throw new ArgumentException("Value init entries must provide a non-zero value key identity.", nameof(keyId));

            if (!ValueInitEntrySourceKindUtilities.IsDefined(sourceKind))
                throw new ArgumentOutOfRangeException(nameof(sourceKind), sourceKind, "Value init entries must provide a defined source kind.");

            if (!ValueInitOverwritePolicyUtilities.IsDefined(overwritePolicy))
                throw new ArgumentOutOfRangeException(nameof(overwritePolicy), overwritePolicy, "Value init entries must provide a defined overwrite policy.");

            if (order < 0)
                throw new ArgumentOutOfRangeException(nameof(order), order, "Value init entries must provide a non-negative execution order.");

            if (source.Value == 0)
                throw new ArgumentException("Value init entries must provide a non-zero source location identity.", nameof(source));

            ValidatePayloadShape(sourceKind, valueKind, serializedValue, evaluationLocalRef);

            KeyId = keyId;
            SourceKind = sourceKind;
            ValueKind = valueKind;
            Order = order;
            OverwritePolicy = overwritePolicy;
            SerializedValue = string.IsNullOrWhiteSpace(serializedValue) ? null : serializedValue.Trim();
            EvaluationLocalRef = string.IsNullOrWhiteSpace(evaluationLocalRef) ? null : evaluationLocalRef.Trim();
            Source = source;
        }

        public ValueKeyId KeyId { get; }

        public ValueInitEntrySourceKind SourceKind { get; }

        public ValueKind ValueKind { get; }

        public int Order { get; }

        public ValueInitOverwritePolicy OverwritePolicy { get; }

        public string? SerializedValue { get; }

        public string? EvaluationLocalRef { get; }

        public SourceLocationId Source { get; }

        static void ValidatePayloadShape(ValueInitEntrySourceKind sourceKind, ValueKind valueKind, string? serializedValue, string? evaluationLocalRef)
        {
            bool hasSerializedValue = !string.IsNullOrWhiteSpace(serializedValue);
            bool hasEvaluationLocalRef = !string.IsNullOrWhiteSpace(evaluationLocalRef);

            switch (sourceKind)
            {
                case ValueInitEntrySourceKind.Literal:
                    if (hasEvaluationLocalRef)
                        throw new ArgumentException("Literal value init entries must not declare an evaluation local ref.", nameof(evaluationLocalRef));

                    if (valueKind == ValueKind.Null)
                    {
                        if (hasSerializedValue)
                            throw new ArgumentException("Null literal value init entries must not declare a serialized payload.", nameof(serializedValue));

                        return;
                    }

                    if (!hasSerializedValue)
                        throw new ArgumentException("Literal value init entries must declare a serialized payload.", nameof(serializedValue));

                    return;

                case ValueInitEntrySourceKind.DynamicEvaluation:
                case ValueInitEntrySourceKind.ReactiveEvaluation:
                    if (valueKind == ValueKind.Null)
                        throw new ArgumentException("Dynamic or reactive value init entries must declare a non-null target value kind.", nameof(valueKind));

                    if (hasSerializedValue)
                        throw new ArgumentException("Dynamic or reactive value init entries must not declare a serialized payload.", nameof(serializedValue));

                    if (!hasEvaluationLocalRef)
                        throw new ArgumentException("Dynamic or reactive value init entries must declare an evaluation local ref.", nameof(evaluationLocalRef));

                    return;

                default:
                    throw new ArgumentOutOfRangeException(nameof(sourceKind), sourceKind, "Unsupported value init entry source kind.");
            }
        }
    }

    public sealed class ValueInitPlanIR
    {
        readonly ValueInitEntryIR[] entries;

        public ValueInitPlanIR(
            ValueInitPlanId planId,
            ModuleId ownerModule,
            ScopePlanId targetScopePlanId,
            string targetStoreRef,
            LifecyclePhase executionPhase,
            int order,
            AvailabilityIR availability,
            ValueInitEntryIR[] entries,
            SourceLocationId source)
        {
            if (planId.Value == 0)
                throw new ArgumentException("Value init plans must provide a non-zero plan identity.", nameof(planId));

            if (ownerModule.Value == 0)
                throw new ArgumentException("Value init plans must provide a non-zero owner module identity.", nameof(ownerModule));

            if (targetScopePlanId.Value == 0)
                throw new ArgumentException("Value init plans must provide a non-zero target scope identity.", nameof(targetScopePlanId));

            if (string.IsNullOrWhiteSpace(targetStoreRef))
                throw new ArgumentException("Value init plans must provide a target store ref.", nameof(targetStoreRef));

            if (!IsSupportedExecutionPhase(executionPhase))
                throw new ArgumentOutOfRangeException(nameof(executionPhase), executionPhase, "Value init plans must use a supported lifecycle phase.");

            if (order < 0)
                throw new ArgumentOutOfRangeException(nameof(order), order, "Value init plans must provide a non-negative execution order.");

            if (source.Value == 0)
                throw new ArgumentException("Value init plans must provide a non-zero source location identity.", nameof(source));

            PlanId = planId;
            OwnerModule = ownerModule;
            TargetScopePlanId = targetScopePlanId;
            TargetStoreRef = targetStoreRef.Trim();
            ExecutionPhase = executionPhase;
            Order = order;
            Availability = availability;
            this.entries = KernelIRNodeArrayUtilities.CloneArray(entries);
            ValidateEntries(this.entries);
            Source = source;
        }

        public ValueInitPlanId PlanId { get; }

        public ModuleId OwnerModule { get; }

        public ScopePlanId TargetScopePlanId { get; }

        public string TargetStoreRef { get; }

        public LifecyclePhase ExecutionPhase { get; }

        public int Order { get; }

        public AvailabilityIR Availability { get; }

        public ReadOnlySpan<ValueInitEntryIR> Entries => entries;

        public SourceLocationId Source { get; }

        static bool IsSupportedExecutionPhase(LifecyclePhase phase)
        {
            switch (phase)
            {
                case LifecyclePhase.Create:
                case LifecyclePhase.Acquire:
                case LifecyclePhase.Reset:
                    return true;

                default:
                    return false;
            }
        }

        static void ValidateEntries(ReadOnlySpan<ValueInitEntryIR> entries)
        {
            if (entries.Length == 0)
                throw new ArgumentException("Value init plans must contain at least one entry.", nameof(entries));

            HashSet<int> seenEntryOrders = new HashSet<int>();
            Dictionary<int, ValueInitEntryIR> lastEntryByValueKey = new Dictionary<int, ValueInitEntryIR>();
            for (int index = 0; index < entries.Length; index++)
            {
                ValueInitEntryIR entry = entries[index] ?? throw new ArgumentException("Value init plan entries must not contain null items.", nameof(entries));

                if (!seenEntryOrders.Add(entry.Order))
                    throw new ArgumentException("Value init plans must use unique entry execution orders.", nameof(entries));

                if (lastEntryByValueKey.TryGetValue(entry.KeyId.Value, out ValueInitEntryIR previousEntry)
                    && entry.OverwritePolicy == ValueInitOverwritePolicy.ErrorIfExists)
                {
                    throw new ArgumentException("Duplicate value init entries require an explicit overwrite or merge policy on the later entry.", nameof(entries));
                }

                lastEntryByValueKey[entry.KeyId.Value] = entry;
            }
        }
    }

    public sealed class UnityObjectLinkIR
    {
        public UnityObjectLinkIR(string kind, string? sourceGuid, long localFileId, string debugName, SourceLocationId source)
        {
            if (string.IsNullOrWhiteSpace(kind))
                throw new ArgumentException("Unity object links must provide a kind.", nameof(kind));

            if (string.Equals(kind.Trim(), "Unknown", StringComparison.Ordinal))
                throw new ArgumentException("Unity object links must use a defined kind.", nameof(kind));

            if (source.Value == 0)
                throw new ArgumentException("Unity object links must provide a non-zero source location identity.", nameof(source));

            if (string.IsNullOrWhiteSpace(debugName))
                throw new ArgumentException("Unity object links must provide a debug name.", nameof(debugName));

            if (sourceGuid != null && string.IsNullOrWhiteSpace(sourceGuid))
                throw new ArgumentException("Unity object link source GUIDs must be null or non-empty.", nameof(sourceGuid));

            if (localFileId < 0)
                throw new ArgumentOutOfRangeException(nameof(localFileId), localFileId, "Unity object links must use a non-negative local file id.");

            if (!string.IsNullOrWhiteSpace(sourceGuid) && localFileId == 0)
                throw new ArgumentException("Unity object links with a source GUID must provide a positive local file id.", nameof(localFileId));

            Kind = kind.Trim();
            SourceGuid = sourceGuid?.Trim();
            LocalFileId = localFileId;
            DebugName = debugName.Trim();
            Source = source;
        }

        public string Kind { get; }

        public string? SourceGuid { get; }

        public long LocalFileId { get; }

        public string DebugName { get; }

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
        readonly ScopeServiceBoundaryIR serviceBoundary;
        readonly UnityObjectLinkIR? unityObjectLink;

        public ScopeIR(ScopeAuthoringId authoringId, ScopePlanId planId, string name, ScopeKind kind, ModuleId ownerModule, ScopeAuthoringId parentAuthoringId, ScopeServiceRequirementIR[]? requiredServices, ScopeValueInitRefIR[]? valueInitPlans, ScopeServiceBoundaryIR serviceBoundary, LifecyclePlanRefIR lifecycle, SourceLocationId source, UnityObjectLinkIR? unityObjectLink = null)
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
            this.serviceBoundary = serviceBoundary ?? throw new ArgumentNullException(nameof(serviceBoundary));
            ValidateServiceBoundary(this.serviceBoundary, this.requiredServices, ParentAuthoringId);
            Lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            Source = source;
            this.unityObjectLink = unityObjectLink;
        }

        public ScopeAuthoringId AuthoringId { get; }

        public ScopePlanId PlanId { get; }

        public string Name { get; }

        public ScopeKind Kind { get; }

        public ModuleId OwnerModule { get; }

        public ScopeAuthoringId ParentAuthoringId { get; }

        public ReadOnlySpan<ScopeServiceRequirementIR> RequiredServices => requiredServices;

        public ReadOnlySpan<ScopeValueInitRefIR> ValueInitPlans => valueInitPlans;

        public ScopeServiceBoundaryIR ServiceBoundary => serviceBoundary;

        public LifecyclePlanRefIR Lifecycle { get; }

        public SourceLocationId Source { get; }

        public UnityObjectLinkIR? UnityObjectLink => unityObjectLink;

        static void ValidateServiceBoundary(ScopeServiceBoundaryIR serviceBoundary, ReadOnlySpan<ScopeServiceRequirementIR> requiredServices, ScopeAuthoringId parentAuthoringId)
        {
            if (requiredServices.Length > 0 && !serviceBoundary.OwnsLocalServiceGraph)
                throw new ArgumentException("Scopes with required services must own a local service boundary.", nameof(serviceBoundary));

            if (serviceBoundary.ReferencesParentServiceGraph && parentAuthoringId.Value == 0)
                throw new ArgumentException("Parent-referencing scope service boundaries require an explicit parent scope.", nameof(serviceBoundary));
        }
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

        public ServiceIR(ServiceId id, string name, ServiceLifetimeKind lifetime, ModuleId ownerModule, ServiceContractIR[]? contracts, ServiceDependencyIR[]? dependencies, ServiceFactoryKind factoryKind, SourceLocationId source, ServiceCardinalityKind cardinality = ServiceCardinalityKind.Unknown)
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
            Cardinality = ResolveCardinality(lifetime, cardinality);
        }

        public ServiceId Id { get; }

        public string Name { get; }

        public ServiceLifetimeKind Lifetime { get; }

        public ModuleId OwnerModule { get; }

        public ReadOnlySpan<ServiceContractIR> Contracts => contracts;

        public ReadOnlySpan<ServiceDependencyIR> Dependencies => dependencies;

        public ServiceFactoryKind FactoryKind { get; }

        public SourceLocationId Source { get; }

        public ServiceCardinalityKind Cardinality { get; }

        static ServiceCardinalityKind ResolveCardinality(ServiceLifetimeKind lifetime, ServiceCardinalityKind cardinality)
        {
            ServiceCardinalityKind expectedCardinality;

            switch (lifetime)
            {
                case ServiceLifetimeKind.Singleton:
                    expectedCardinality = ServiceCardinalityKind.SingletonGlobal;
                    break;

                case ServiceLifetimeKind.Project:
                    expectedCardinality = ServiceCardinalityKind.OnePerProject;
                    break;

                case ServiceLifetimeKind.Scene:
                    expectedCardinality = ServiceCardinalityKind.OnePerScene;
                    break;

                case ServiceLifetimeKind.Scoped:
                    expectedCardinality = ServiceCardinalityKind.OnePerAuthoredScope;
                    break;

                case ServiceLifetimeKind.ExplicitTransient:
                    expectedCardinality = ServiceCardinalityKind.Unknown;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "Unsupported service lifetime kind.");
            }

            if (cardinality == ServiceCardinalityKind.Unknown)
                return expectedCardinality;

            if (cardinality != expectedCardinality)
                throw new ArgumentOutOfRangeException(nameof(cardinality), cardinality, "Service cardinality must match the verified lifetime boundary.");

            return cardinality;
        }
    }

    public enum CommandPayloadFieldKindIR
    {
        Unknown = 0,
        Bool = 10,
        Int = 20,
        Float = 30,
        String = 40,
        Object = 50,
        ValueKeyId = 60,
        RuntimeQueryId = 70,
        TargetReference = 80,
        CommandList = 90,
        VarStorePayload = 100,
    }

    public enum CommandPayloadFieldRequirementIR
    {
        Optional = 10,
        Required = 20,
    }

    public enum CommandPayloadUnknownFieldPolicyIR
    {
        Reject = 10,
        Ignore = 20,
    }

    public enum CommandPayloadReferenceKindIR
    {
        None = 0,
        ValueKeyId = 10,
        RuntimeQueryId = 20,
        TargetReference = 30,
    }

    public sealed class CommandPayloadFieldIR
    {
        public CommandPayloadFieldIR(
            string fieldPath,
            CommandPayloadFieldKindIR kind,
            CommandPayloadFieldRequirementIR requirement,
            SourceLocationId source,
            CommandPayloadReferenceKindIR referenceKind = CommandPayloadReferenceKindIR.None,
            bool allowNull = false)
        {
            string normalizedFieldPath = fieldPath == null ? string.Empty : fieldPath.Trim();
            if (string.IsNullOrWhiteSpace(normalizedFieldPath))
                throw new ArgumentException("Command payload field IR must provide a field path.", nameof(fieldPath));

            if (source.Value == 0)
                throw new ArgumentException("Command payload field IR must provide a non-zero source location identity.", nameof(source));

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

        public SourceLocationId Source { get; }
    }

    public sealed class CommandPayloadSchemaRefIR
    {
        readonly CommandPayloadFieldIR[] fields;

        public CommandPayloadSchemaRefIR(CommandPayloadSchemaId id, SourceLocationId source, CommandPayloadFieldIR[]? fields = null, CommandPayloadUnknownFieldPolicyIR unknownFieldPolicy = CommandPayloadUnknownFieldPolicyIR.Reject)
        {
            if (id.Value == 0)
                throw new ArgumentException("Command payload schema refs must provide a non-zero schema identity.", nameof(id));

            if (source.Value == 0)
                throw new ArgumentException("Command payload schema refs must provide a non-zero source location identity.", nameof(source));

            Id = id;
            Source = source;
            UnknownFieldPolicy = unknownFieldPolicy;
            this.fields = KernelIRNodeArrayUtilities.CloneArray(fields);
            ValidateFieldPaths(this.fields);
        }

        public CommandPayloadSchemaId Id { get; }

        public SourceLocationId Source { get; }

        public CommandPayloadUnknownFieldPolicyIR UnknownFieldPolicy { get; }

        public ReadOnlySpan<CommandPayloadFieldIR> Fields => fields;

        static void ValidateFieldPaths(CommandPayloadFieldIR[] fields)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < fields.Length; index++)
            {
                if (!seen.Add(fields[index].FieldPath))
                    throw new ArgumentException("Command payload schema refs require unique field paths.", nameof(fields));
            }
        }
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

    public sealed class CommandAuthoringKeyRefIR
    {
        public CommandAuthoringKeyRefIR(CommandAuthoringKeyId id, string value, SourceLocationId source)
        {
            if (id.Value == 0)
                throw new ArgumentException("Command authoring key refs must provide a non-zero authoring key identity.", nameof(id));

            string normalizedValue = value == null ? string.Empty : value.Trim();
            if (string.IsNullOrWhiteSpace(normalizedValue))
                throw new ArgumentException("Command authoring key refs must provide a non-empty authoring key value.", nameof(value));

            if (source.Value == 0)
                throw new ArgumentException("Command authoring key refs must provide a non-zero source location identity.", nameof(source));

            Id = id;
            Value = normalizedValue;
            Source = source;
        }

        public CommandAuthoringKeyId Id { get; }

        public string Value { get; }

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

        public CommandIR(CommandTypeId typeId, string runtimeName, CommandAuthoringKeyRefIR authoringKey, CommandCategoryId categoryId, ModuleId ownerModule, CommandPayloadSchemaRefIR payloadSchema, CommandExecutorRefIR executor, CommandDependencyIR[]? dependencies, SourceLocationId source)
        {
            if (typeId.Value == 0)
                throw new ArgumentException("Command IR must provide a non-zero command type identity.", nameof(typeId));

            if (string.IsNullOrWhiteSpace(runtimeName))
                throw new ArgumentException("Command IR must provide a runtime name.", nameof(runtimeName));

            if (categoryId.Value == 0)
                throw new ArgumentException("Command IR must provide a non-zero command category identity.", nameof(categoryId));

            if (ownerModule.Value == 0)
                throw new ArgumentException("Command IR must provide a non-zero owner module identity.", nameof(ownerModule));

            if (source.Value == 0)
                throw new ArgumentException("Command IR must provide a non-zero source location identity.", nameof(source));

            TypeId = typeId;
            RuntimeName = runtimeName;
            AuthoringKey = authoringKey ?? throw new ArgumentNullException(nameof(authoringKey));
            CategoryId = categoryId;
            OwnerModule = ownerModule;
            PayloadSchema = payloadSchema ?? throw new ArgumentNullException(nameof(payloadSchema));
            Executor = executor ?? throw new ArgumentNullException(nameof(executor));
            this.dependencies = KernelIRNodeArrayUtilities.CloneArray(dependencies);
            Source = source;
        }

        public CommandTypeId TypeId { get; }

        public string RuntimeName { get; }

        public CommandAuthoringKeyRefIR AuthoringKey { get; }

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

    public sealed class LifecycleAsyncPolicyIR
    {
        public LifecycleAsyncPolicyIR(
            LifecycleAsyncCancellationSourceKind cancellationSourceKind,
            LifecycleAsyncTimeoutPolicyKind timeoutPolicyKind,
            int timeoutMilliseconds,
            LifecycleAsyncCompletionRequirementKind completionRequirementKind,
            bool waitForNextStep)
        {
            if (!LifecycleAsyncCancellationSourceKindUtilities.IsDefined(cancellationSourceKind))
                throw new ArgumentOutOfRangeException(nameof(cancellationSourceKind), cancellationSourceKind, "Lifecycle async policies must provide a defined cancellation source kind.");

            if (!LifecycleAsyncTimeoutPolicyKindUtilities.IsDefined(timeoutPolicyKind))
                throw new ArgumentOutOfRangeException(nameof(timeoutPolicyKind), timeoutPolicyKind, "Lifecycle async policies must provide a defined timeout policy kind.");

            if (!LifecycleAsyncCompletionRequirementKindUtilities.IsDefined(completionRequirementKind))
                throw new ArgumentOutOfRangeException(nameof(completionRequirementKind), completionRequirementKind, "Lifecycle async policies must provide a defined completion requirement kind.");

            if (timeoutPolicyKind == LifecycleAsyncTimeoutPolicyKind.DurationMilliseconds)
            {
                if (timeoutMilliseconds <= 0)
                    throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds), timeoutMilliseconds, "Lifecycle async timeout policies that use a duration must provide a positive timeout in milliseconds.");
            }
            else if (timeoutMilliseconds != 0)
            {
                throw new ArgumentException("Lifecycle async timeout policies without a duration must not declare a timeout value.", nameof(timeoutMilliseconds));
            }

            if (waitForNextStep && completionRequirementKind != LifecycleAsyncCompletionRequirementKind.BeforeNextStep)
                throw new ArgumentException("Tracked async steps that wait for the next step must require completion before the next step.", nameof(completionRequirementKind));

            if (!waitForNextStep && completionRequirementKind != LifecycleAsyncCompletionRequirementKind.BeforePhaseExit)
                throw new ArgumentException("Tracked async steps that do not block the next step must require completion before phase exit.", nameof(completionRequirementKind));

            CancellationSourceKind = cancellationSourceKind;
            TimeoutPolicyKind = timeoutPolicyKind;
            TimeoutMilliseconds = timeoutMilliseconds;
            CompletionRequirementKind = completionRequirementKind;
            WaitForNextStep = waitForNextStep;
        }

        public LifecycleAsyncCancellationSourceKind CancellationSourceKind { get; }

        public LifecycleAsyncTimeoutPolicyKind TimeoutPolicyKind { get; }

        public int TimeoutMilliseconds { get; }

        public LifecycleAsyncCompletionRequirementKind CompletionRequirementKind { get; }

        public bool WaitForNextStep { get; }
    }

    public sealed class LifecycleStepIR
    {
        readonly DependencyEdgeId[] dependencies;

        public LifecycleStepIR(
            LifecycleStepId id,
            LifecyclePhase phase,
            int order,
            LifecycleTargetRefIR target,
            LifecycleActionKind action,
            DependencyEdgeId[]? dependencies,
            SourceLocationId source,
            LifecycleTickCardinalityKind tickCardinality = LifecycleTickCardinalityKind.Unknown,
            LifecycleExecutionModeKind executionMode = LifecycleExecutionModeKind.Synchronous,
            LifecycleAsyncPolicyIR? asyncPolicy = null)
        {
            if (id.Value == 0)
                throw new ArgumentException("Lifecycle steps must provide a non-zero step identity.", nameof(id));

            if (action == LifecycleActionKind.Unknown)
                throw new ArgumentException("Lifecycle steps must provide an action kind.", nameof(action));

            if (source.Value == 0)
                throw new ArgumentException("Lifecycle steps must provide a non-zero source location identity.", nameof(source));

            if (!LifecycleExecutionModeKindUtilities.IsDefined(executionMode))
                throw new ArgumentOutOfRangeException(nameof(executionMode), executionMode, "Lifecycle steps must provide a defined execution mode.");

            if (executionMode == LifecycleExecutionModeKind.TrackedAsync && asyncPolicy == null)
                throw new ArgumentException("Tracked async lifecycle steps must provide an async policy.", nameof(asyncPolicy));

            if (executionMode == LifecycleExecutionModeKind.Synchronous && asyncPolicy != null)
                throw new ArgumentException("Synchronous lifecycle steps must not provide an async policy.", nameof(asyncPolicy));

            if (executionMode == LifecycleExecutionModeKind.TrackedAsync && phase == LifecyclePhase.Acquire && asyncPolicy != null && !asyncPolicy.WaitForNextStep)
                throw new ArgumentException("Acquire-phase tracked async lifecycle steps must complete before the next step.", nameof(asyncPolicy));

            bool isTickPhase = phase == LifecyclePhase.Tick || phase == LifecyclePhase.FixedTick || phase == LifecyclePhase.LateTick;

            if (!isTickPhase && tickCardinality != LifecycleTickCardinalityKind.Unknown)
                throw new ArgumentException("Tick cardinality is only valid for tick phases.", nameof(tickCardinality));

            if (isTickPhase && tickCardinality == LifecycleTickCardinalityKind.Unknown)
                tickCardinality = LifecycleTickCardinalityKind.Hub;

            if (tickCardinality != LifecycleTickCardinalityKind.Unknown && !LifecycleTickCardinalityKindUtilities.IsDefined(tickCardinality))
                throw new ArgumentOutOfRangeException(nameof(tickCardinality), tickCardinality, "Lifecycle steps must provide a defined tick cardinality.");

            Id = id;
            Phase = phase;
            Order = order;
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Action = action;
            this.dependencies = KernelIRNodeArrayUtilities.CloneDependencyIds(dependencies);
            Source = source;
            TickCardinality = tickCardinality;
            ExecutionMode = executionMode;
            AsyncPolicy = asyncPolicy;
        }

        public LifecycleStepId Id { get; }

        public LifecyclePhase Phase { get; }

        public int Order { get; }

        public LifecycleTargetRefIR Target { get; }

        public LifecycleActionKind Action { get; }

        public LifecycleTickCardinalityKind TickCardinality { get; }

        public LifecycleExecutionModeKind ExecutionMode { get; }

        public LifecycleAsyncPolicyIR? AsyncPolicy { get; }

        public ReadOnlySpan<DependencyEdgeId> Dependencies => dependencies;

        public SourceLocationId Source { get; }
    }

    public sealed class LifecycleIR
    {
        readonly LifecycleStepIR[] steps;

        public LifecycleIR(
            LifecyclePlanId planId,
            string name,
            ModuleId ownerModule,
            LifecycleStepIR[] steps,
            SourceLocationId source,
            LifecycleFailurePolicy failurePolicy,
            bool failurePolicyIsExplicit = true,
            KernelProfileMask failurePolicyJustificationProfiles = KernelProfileMask.None,
            string? failurePolicyJustification = null,
            LifecycleAcquireRollbackPolicy acquireRollbackPolicy = LifecycleAcquireRollbackPolicy.ReverseCompletedAcquireSteps)
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

            if (!LifecycleFailurePolicyUtilities.IsDefined(failurePolicy))
                throw new ArgumentOutOfRangeException(nameof(failurePolicy), failurePolicy, "Lifecycle IR must provide a defined failure policy.");

            if (!LifecycleAcquireRollbackPolicyUtilities.IsDefined(acquireRollbackPolicy))
                throw new ArgumentOutOfRangeException(nameof(acquireRollbackPolicy), acquireRollbackPolicy, "Lifecycle IR must provide a defined acquire rollback policy.");

            if (!failurePolicyIsExplicit && failurePolicy == LifecycleFailurePolicy.ContinueWithError)
                throw new ArgumentException("ContinueWithError must be an explicit failure policy.", nameof(failurePolicyIsExplicit));

            if (failurePolicy == LifecycleFailurePolicy.ContinueWithError)
            {
                if (!failurePolicyIsExplicit)
                    throw new ArgumentException("ContinueWithError must be an explicit failure policy.", nameof(failurePolicyIsExplicit));

                if (failurePolicyJustificationProfiles == KernelProfileMask.None)
                    throw new ArgumentException("ContinueWithError must declare at least one justified profile.", nameof(failurePolicyJustificationProfiles));

                if (string.IsNullOrWhiteSpace(failurePolicyJustification))
                    throw new ArgumentException("ContinueWithError must provide a profile justification.", nameof(failurePolicyJustification));
            }
            else if (failurePolicyJustificationProfiles != KernelProfileMask.None || failurePolicyJustification != null)
            {
                throw new ArgumentException("Lifecycle failure policy justification metadata is only valid with ContinueWithError.", nameof(failurePolicyJustification));
            }

            if (!failurePolicyIsExplicit && failurePolicyJustificationProfiles != KernelProfileMask.None)
                throw new ArgumentException("Defaulted lifecycle failure policies must not declare profile justification metadata.", nameof(failurePolicyJustificationProfiles));

            if (!failurePolicyIsExplicit && failurePolicyJustification != null)
                throw new ArgumentException("Defaulted lifecycle failure policies must not declare justification text.", nameof(failurePolicyJustification));

            if (failurePolicyIsExplicit == false && failurePolicy == LifecycleFailurePolicy.Unknown)
                throw new ArgumentException("Lifecycle IR must provide an explicit failure policy.", nameof(failurePolicy));

            PlanId = planId;
            Name = name;
            OwnerModule = ownerModule;
            this.steps = KernelIRNodeArrayUtilities.CloneArray(steps);
            Source = source;
            FailurePolicy = failurePolicy;
            FailurePolicyIsExplicit = failurePolicyIsExplicit;
            FailurePolicyJustificationProfiles = failurePolicyJustificationProfiles;
            FailurePolicyJustification = failurePolicyJustification;
            AcquireRollbackPolicy = acquireRollbackPolicy;
        }

        public LifecyclePlanId PlanId { get; }

        public string Name { get; }

        public ModuleId OwnerModule { get; }

        public ReadOnlySpan<LifecycleStepIR> Steps => steps;

        public SourceLocationId Source { get; }

        public LifecycleFailurePolicy FailurePolicy { get; }

        public bool FailurePolicyIsExplicit { get; }

        public KernelProfileMask FailurePolicyJustificationProfiles { get; }

        public string? FailurePolicyJustification { get; }

        public LifecycleAcquireRollbackPolicy AcquireRollbackPolicy { get; }
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

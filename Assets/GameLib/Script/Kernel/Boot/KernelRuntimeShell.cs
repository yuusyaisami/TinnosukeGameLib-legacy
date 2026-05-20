#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Game.Kernel.Abstractions;
using Game.Kernel.Diagnostics;
using Game.Kernel.Generation;
using Game.Kernel.IR;

namespace Game.Kernel.Boot
{
    public static class KernelRuntimeServiceGraphCodes
    {
        public const string ServiceSlotMissing = "KERNEL_RUNTIME_SERVICE_SLOT_MISSING";
        public const string ServiceSlotDuplicate = "KERNEL_RUNTIME_SERVICE_SLOT_DUPLICATE";
        public const string ServiceSlotInvalidIdentity = "KERNEL_RUNTIME_SERVICE_SLOT_INVALID_IDENTITY";
        public const string ServicePlanMissing = "KERNEL_RUNTIME_SERVICE_PLAN_MISSING";
        public const string ServiceFactoryUnsupported = "KERNEL_RUNTIME_SERVICE_FACTORY_UNSUPPORTED";
        public const string ServiceRequiredMissing = "KERNEL_RUNTIME_SERVICE_REQUIRED_MISSING";
        public const string ServiceOptionalAbsent = "KERNEL_RUNTIME_SERVICE_OPTIONAL_ABSENT";
        public const string ServiceOptionalAlternativeMissing = "KERNEL_RUNTIME_SERVICE_OPTIONAL_ALTERNATIVE_MISSING";
        public const string ServiceOptionalAlternativeIncompatible = "KERNEL_RUNTIME_SERVICE_OPTIONAL_ALTERNATIVE_INCOMPATIBLE";
        public const string ServiceOptionalAlternativeResolved = "KERNEL_RUNTIME_SERVICE_OPTIONAL_ALTERNATIVE_RESOLVED";
        public const string ServiceOptionalProfileMissing = "KERNEL_RUNTIME_SERVICE_OPTIONAL_PROFILE_MISSING";
        public const string ServiceOptionalProfileBoundaryMissing = "KERNEL_RUNTIME_SERVICE_OPTIONAL_PROFILE_BOUNDARY_MISSING";
        public const string ServiceOptionalProfileError = "KERNEL_RUNTIME_SERVICE_OPTIONAL_PROFILE_ERROR";
        public const string ServiceOptionalUnsupportedBehavior = "KERNEL_RUNTIME_SERVICE_OPTIONAL_UNSUPPORTED_BEHAVIOR";
    }

    public sealed class KernelRuntime
    {
        public KernelRuntime(KernelBootBoundaryContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            Manifest = context.Manifest;
            SelectedProfile = context.SelectedProfile;
            DebugMap = CreateDebugMap(context);
            Diagnostics = new KernelRuntimeDiagnostics(context.ValidationReport, DebugMap);
            ServiceGraph = CreateServiceGraph(context);
            LifecycleDispatcher = context.LifecyclePlan == null ? null : new KernelLifecycleDispatcher(context.LifecyclePlan);
            LifecyclePlanResolver = LifecycleDispatcher == null
                ? new KernelLifecyclePlanResolver(Array.Empty<KernelLifecycleDispatcher>())
                : new KernelLifecyclePlanResolver(new[] { LifecycleDispatcher });
            RootScopeGraph = new KernelRuntimeScopeGraph(context.Input.ScopeGraphPlan, context.Input.RootState.AvailableRootScopes, LifecyclePlanResolver);
        }

        public KernelBootManifest Manifest { get; }

        public KernelProfile SelectedProfile { get; }

        public KernelRuntimeDiagnostics Diagnostics { get; }

        public KernelDebugMap DebugMap { get; }

        public KernelRuntimeServiceGraph ServiceGraph { get; }

        public KernelRuntimeScopeGraph RootScopeGraph { get; }

        public KernelLifecycleDispatcher? LifecycleDispatcher { get; }

        public ILifecyclePlanResolver LifecyclePlanResolver { get; }

        static KernelDebugMap CreateDebugMap(KernelBootBoundaryContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            KernelDebugMap debugMap = context.Input.DebugMap
                ?? throw new InvalidOperationException("Kernel runtime requires a verified debug map boot input.");

            string? expectedDebugMapHash = context.Manifest.ArtifactSet.DebugMapHash;
            if (expectedDebugMapHash != null
                && !StringComparer.OrdinalIgnoreCase.Equals(expectedDebugMapHash, debugMap.ContentHash.ToString()))
            {
                throw new InvalidOperationException("Kernel runtime requires a debug map whose content hash matches the verified boot manifest.");
            }

            return debugMap;
        }

        static KernelRuntimeServiceGraph CreateServiceGraph(KernelBootBoundaryContext context)
        {
            ServiceGraphPlan? serviceGraphPlan = context.Input.ServiceGraphPlan;
            if (serviceGraphPlan == null)
                throw new InvalidOperationException(KernelRuntimeServiceGraphCodes.ServicePlanMissing + ": Kernel runtime requires a verified service graph plan.");

            return new KernelRuntimeServiceGraph(serviceGraphPlan, context.SelectedProfile.Kind);
        }
    }

    public readonly struct KernelRuntimeServiceSlot : IEquatable<KernelRuntimeServiceSlot>
    {
        public KernelRuntimeServiceSlot(int slotIndex, RuntimeIdentityRef serviceIdentity)
            : this(slotIndex, -1, serviceIdentity, ServiceFactoryKind.Unknown, default, null, null, ServiceLifetimeKind.Unknown, ServiceCardinalityKind.Unknown, default)
        {
        }

        public KernelRuntimeServiceSlot(
            int slotIndex,
            int entryIndex,
            RuntimeIdentityRef serviceIdentity,
            ServiceFactoryKind factoryKind,
            SourceLocationId factorySource,
            string? serviceName,
            ServiceSlotPlan? planSlot = null,
            ServiceLifetimeKind lifetime = ServiceLifetimeKind.Unknown,
            ServiceCardinalityKind cardinality = ServiceCardinalityKind.Unknown,
            ModuleId ownerModule = default)
        {
            if (slotIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "Kernel runtime service slots must have a non-negative slot index.");

            if (entryIndex < -1)
                throw new ArgumentOutOfRangeException(nameof(entryIndex), entryIndex, "Kernel runtime service slots must have a non-negative entry index or a legacy sentinel value.");

            if (serviceIdentity.Kind != RuntimeIdentityKind.Service || serviceIdentity.Value <= 0)
                throw new ArgumentException("Kernel runtime service slots must be backed by a verified service identity.", nameof(serviceIdentity));

            if (planSlot == null)
            {
                if (entryIndex >= 0 && factoryKind != ServiceFactoryKind.GeneratedFactory && factoryKind != ServiceFactoryKind.ProvidedInstance)
                    throw new ArgumentException(KernelRuntimeServiceGraphCodes.ServiceFactoryUnsupported + ": Kernel runtime service slots only accept generated or provided-instance factories.", nameof(factoryKind));

                if (entryIndex >= 0 && factorySource.Value <= 0)
                    throw new ArgumentException("Kernel runtime service slots must carry a verified factory source.", nameof(factorySource));

                SlotIndex = slotIndex;
                EntryIndex = entryIndex;
                ServiceIdentity = serviceIdentity;
                FactoryKind = factoryKind;
                FactorySource = factorySource;
                ServiceName = serviceName;
                PlanSlot = null;
                Lifetime = lifetime;
                Cardinality = cardinality;
                OwnerModule = ownerModule;
                return;
            }

            if (slotIndex != planSlot.SlotIndex)
                throw new ArgumentException("Kernel runtime service slots must preserve the verified slot index.", nameof(slotIndex));

            SlotIndex = planSlot.SlotIndex;
            EntryIndex = planSlot.EntryIndex;
            ServiceIdentity = new RuntimeIdentityRef(RuntimeIdentityKind.Service, planSlot.ServiceId.Value);
            FactoryKind = planSlot.Factory.FactoryKind;
            FactorySource = planSlot.Factory.Source;
            ServiceName = planSlot.Entry.Name;
            PlanSlot = planSlot;
            Lifetime = planSlot.Lifetime;
            Cardinality = planSlot.Cardinality;
            OwnerModule = planSlot.OwnerModule;
        }

        public int SlotIndex { get; }

        public int EntryIndex { get; }

        public RuntimeIdentityRef ServiceIdentity { get; }

        public ServiceLifetimeKind Lifetime { get; }

        public ServiceCardinalityKind Cardinality { get; }

        public ModuleId OwnerModule { get; }

        public ServiceFactoryKind FactoryKind { get; }

        public SourceLocationId FactorySource { get; }

        public string? ServiceName { get; }

        public ServiceSlotPlan? PlanSlot { get; }

        public bool HasPlan => PlanSlot != null;

        public ReadOnlySpan<ServiceContractRef> Contracts => PlanSlot == null ? default : PlanSlot.Contracts;

        public ReadOnlySpan<ServiceDependencyIR> Dependencies => PlanSlot == null ? default : PlanSlot.Dependencies;

        public bool Equals(KernelRuntimeServiceSlot other)
        {
            return SlotIndex == other.SlotIndex
                && EntryIndex == other.EntryIndex
                && ServiceIdentity == other.ServiceIdentity
                && Lifetime == other.Lifetime
                && Cardinality == other.Cardinality
                && OwnerModule == other.OwnerModule
                && FactoryKind == other.FactoryKind
                && FactorySource == other.FactorySource
                && StringComparer.Ordinal.Equals(ServiceName, other.ServiceName);
        }

        public override bool Equals(object? obj)
        {
            return obj is KernelRuntimeServiceSlot other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SlotIndex;
                hash = (hash * 397) ^ EntryIndex;
                hash = (hash * 397) ^ ServiceIdentity.GetHashCode();
                hash = (hash * 397) ^ (int)Lifetime;
                hash = (hash * 397) ^ (int)Cardinality;
                hash = (hash * 397) ^ OwnerModule.GetHashCode();
                hash = (hash * 397) ^ (int)FactoryKind;
                hash = (hash * 397) ^ FactorySource.GetHashCode();
                hash = (hash * 397) ^ (ServiceName != null ? StringComparer.Ordinal.GetHashCode(ServiceName) : 0);
                return hash;
            }
        }

        public override string ToString()
        {
            return "KernelRuntimeServiceSlot(SlotIndex=" + SlotIndex + ", EntryIndex=" + EntryIndex + ", ServiceIdentity=" + ServiceIdentity + ", Lifetime=" + Lifetime + ", Cardinality=" + Cardinality + ", OwnerModule=" + OwnerModule.Value + ", FactoryKind=" + FactoryKind + ", FactorySource=" + FactorySource + ", ServiceName=" + (ServiceName ?? "null") + ")";
        }

        public static bool operator ==(KernelRuntimeServiceSlot left, KernelRuntimeServiceSlot right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(KernelRuntimeServiceSlot left, KernelRuntimeServiceSlot right)
        {
            return !left.Equals(right);
        }
    }

    public enum KernelRuntimeServiceResolutionKind
    {
        Unknown = 0,
        Resolved = 10,
        MissingRequired = 20,
        OptionalAbsent = 30,
        OptionalAlternativeResolved = 40,
        Rejected = 50,
    }

    public readonly struct KernelRuntimeServiceResolutionResult
    {
        public KernelRuntimeServiceResolutionResult(
            KernelRuntimeServiceResolutionKind kind,
            RuntimeIdentityRef requestedServiceIdentity,
            KernelRuntimeServiceSlot? resolvedServiceSlot,
            KernelDiagnostic? diagnostic,
            KernelRuntimeServiceSlot? requestingServiceSlot = null,
            OptionalDependencyAbsenceBehavior? absenceBehavior = null,
            KernelProfileKind? selectedProfileKind = null,
            KernelProfileMask profileSpecificErrorProfiles = KernelProfileMask.None,
            RuntimeIdentityRef? alternativeServiceIdentity = null)
        {
            if (kind == KernelRuntimeServiceResolutionKind.Unknown)
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Kernel runtime service resolution results must provide a defined kind.");

            if (requestedServiceIdentity.Kind != RuntimeIdentityKind.Service || requestedServiceIdentity.Value <= 0)
                throw new ArgumentException("Kernel runtime service resolution results must be backed by a verified service identity.", nameof(requestedServiceIdentity));

            Kind = kind;
            RequestedServiceIdentity = requestedServiceIdentity;
            ResolvedServiceSlot = resolvedServiceSlot;
            Diagnostic = diagnostic;
            RequestingServiceSlot = requestingServiceSlot;
            AbsenceBehavior = absenceBehavior;
            SelectedProfileKind = selectedProfileKind;
            ProfileSpecificErrorProfiles = profileSpecificErrorProfiles;
            AlternativeServiceIdentity = alternativeServiceIdentity;
        }

        public KernelRuntimeServiceResolutionKind Kind { get; }

        public RuntimeIdentityRef RequestedServiceIdentity { get; }

        public KernelRuntimeServiceSlot? ResolvedServiceSlot { get; }

        public KernelDiagnostic? Diagnostic { get; }

        public KernelRuntimeServiceSlot? RequestingServiceSlot { get; }

        public OptionalDependencyAbsenceBehavior? AbsenceBehavior { get; }

        public KernelProfileKind? SelectedProfileKind { get; }

        public KernelProfileMask ProfileSpecificErrorProfiles { get; }

        public RuntimeIdentityRef? AlternativeServiceIdentity { get; }

        public bool HasResolvedServiceSlot => ResolvedServiceSlot.HasValue;

        public bool HasDiagnostic => Diagnostic != null;

        public bool IsResolved => Kind == KernelRuntimeServiceResolutionKind.Resolved || Kind == KernelRuntimeServiceResolutionKind.OptionalAlternativeResolved;

        public static KernelRuntimeServiceResolutionResult Resolved(RuntimeIdentityRef requestedServiceIdentity, KernelRuntimeServiceSlot resolvedServiceSlot, KernelRuntimeServiceSlot? requestingServiceSlot = null)
        {
            return new KernelRuntimeServiceResolutionResult(
                KernelRuntimeServiceResolutionKind.Resolved,
                requestedServiceIdentity,
                resolvedServiceSlot,
                null,
                requestingServiceSlot);
        }

        public static KernelRuntimeServiceResolutionResult MissingRequired(RuntimeIdentityRef requestedServiceIdentity, KernelDiagnostic diagnostic, KernelRuntimeServiceSlot? requestingServiceSlot = null)
        {
            return new KernelRuntimeServiceResolutionResult(
                KernelRuntimeServiceResolutionKind.MissingRequired,
                requestedServiceIdentity,
                null,
                diagnostic,
                requestingServiceSlot);
        }

        public static KernelRuntimeServiceResolutionResult OptionalAbsent(
            RuntimeIdentityRef requestedServiceIdentity,
            KernelDiagnostic diagnostic,
            KernelRuntimeServiceSlot? requestingServiceSlot,
            OptionalDependencyAbsenceBehavior absenceBehavior,
            KernelProfileKind? selectedProfileKind,
            KernelProfileMask profileSpecificErrorProfiles = KernelProfileMask.None)
        {
            return new KernelRuntimeServiceResolutionResult(
                KernelRuntimeServiceResolutionKind.OptionalAbsent,
                requestedServiceIdentity,
                null,
                diagnostic,
                requestingServiceSlot,
                absenceBehavior,
                selectedProfileKind,
                profileSpecificErrorProfiles);
        }

        public static KernelRuntimeServiceResolutionResult OptionalAlternativeResolved(
            RuntimeIdentityRef requestedServiceIdentity,
            KernelRuntimeServiceSlot resolvedServiceSlot,
            KernelRuntimeServiceSlot? requestingServiceSlot,
            RuntimeIdentityRef alternativeServiceIdentity,
            OptionalDependencyAbsenceBehavior absenceBehavior,
            KernelProfileKind? selectedProfileKind,
            KernelProfileMask profileSpecificErrorProfiles = KernelProfileMask.None)
        {
            return new KernelRuntimeServiceResolutionResult(
                KernelRuntimeServiceResolutionKind.OptionalAlternativeResolved,
                requestedServiceIdentity,
                resolvedServiceSlot,
                null,
                requestingServiceSlot,
                absenceBehavior,
                selectedProfileKind,
                profileSpecificErrorProfiles,
                alternativeServiceIdentity);
        }

        public static KernelRuntimeServiceResolutionResult Rejected(
            RuntimeIdentityRef requestedServiceIdentity,
            KernelDiagnostic diagnostic,
            KernelRuntimeServiceSlot? requestingServiceSlot,
            OptionalDependencyAbsenceBehavior absenceBehavior,
            KernelProfileKind? selectedProfileKind,
            KernelProfileMask profileSpecificErrorProfiles = KernelProfileMask.None,
            RuntimeIdentityRef? alternativeServiceIdentity = null)
        {
            return new KernelRuntimeServiceResolutionResult(
                KernelRuntimeServiceResolutionKind.Rejected,
                requestedServiceIdentity,
                null,
                diagnostic,
                requestingServiceSlot,
                absenceBehavior,
                selectedProfileKind,
                profileSpecificErrorProfiles,
                alternativeServiceIdentity);
        }
    }

    public sealed class KernelRuntimeDiagnostics
    {

        public KernelRuntimeDiagnostics(BootValidationReport validationReport, KernelDebugMap debugMap)
        {
            ValidationReport = validationReport ?? throw new ArgumentNullException(nameof(validationReport));
            DebugMap = debugMap ?? throw new ArgumentNullException(nameof(debugMap));
            DebugMapHash = debugMap.ContentHash.ToString();

            KernelDiagnostic[] snapshot = validationReport.Issues.Count == 0
                ? Array.Empty<KernelDiagnostic>()
                : CloneDiagnostics(validationReport.Issues);

            diagnostics = Array.AsReadOnly(snapshot);
        }

        public BootValidationReport ValidationReport { get; }

        public KernelDebugMap DebugMap { get; }

        public string DebugMapHash { get; }

        public IReadOnlyList<KernelDiagnostic> Diagnostics => diagnostics;

        public bool HasDiagnostics => diagnostics.Count > 0;

        static KernelDiagnostic[] CloneDiagnostics(IReadOnlyList<KernelDiagnostic> source)
        {
            KernelDiagnostic[] clone = new KernelDiagnostic[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                clone[index] = source[index] ?? throw new ArgumentException("Kernel runtime diagnostics must not contain null items.", nameof(source));
            }

            return clone;
        }
    }

    public sealed class KernelRuntimeServiceGraph
    {
        readonly ReadOnlyCollection<RuntimeIdentityRef> rootServiceIdentities;
        readonly KernelRuntimeServiceSlot[] serviceSlots;
        readonly ReadOnlyCollection<KernelRuntimeServiceSlot> serviceSlotView;
        readonly KernelProfileKind? selectedProfileKind;

        public KernelRuntimeServiceGraph(ServiceGraphPlan serviceGraphPlan, KernelProfileKind? selectedProfileKind = null)
        {
            if (serviceGraphPlan == null)
                throw new ArgumentNullException(nameof(serviceGraphPlan));

            ValidateSelectedProfileKind(selectedProfileKind);

            ServiceSlotPlan[] snapshot = KernelProjectionArrayHelpers.CloneAndSort(serviceGraphPlan.Slots, static (left, right) => left.SlotIndex.CompareTo(right.SlotIndex));
            serviceSlots = BuildServiceSlots(snapshot);
            RuntimeIdentityRef[] rootSnapshot = BuildRootServiceIdentities(serviceSlots);
            ValidateRootServiceIdentities(rootSnapshot);

            this.selectedProfileKind = selectedProfileKind;
            rootServiceIdentities = Array.AsReadOnly(rootSnapshot);
            serviceSlotView = Array.AsReadOnly(serviceSlots);
        }

        public KernelProfileKind? SelectedProfileKind => selectedProfileKind;

        public IReadOnlyList<RuntimeIdentityRef> RootServiceIdentities => rootServiceIdentities;

        public int RootServiceCount => rootServiceIdentities.Count;

        public IReadOnlyList<KernelRuntimeServiceSlot> ServiceSlots => serviceSlotView;

        public int ServiceSlotCount => serviceSlots.Length;

        public bool IsEmpty => serviceSlots.Length == 0;

        public bool TryGetServiceSlot(RuntimeIdentityRef serviceIdentity, out KernelRuntimeServiceSlot slot)
        {
            if (serviceIdentity.Kind != RuntimeIdentityKind.Service || serviceIdentity.Value <= 0)
            {
                slot = default;
                return false;
            }

            int low = 0;
            int high = serviceSlots.Length - 1;

            while (low <= high)
            {
                int mid = low + ((high - low) >> 1);
                KernelRuntimeServiceSlot candidate = serviceSlots[mid];
                int comparison = CompareServiceIdentities(candidate.ServiceIdentity, serviceIdentity);

                if (comparison == 0)
                {
                    slot = candidate;
                    return true;
                }

                if (comparison < 0)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            slot = default;
            return false;
        }

        public bool TryGetServiceSlotIndex(RuntimeIdentityRef serviceIdentity, out int slotIndex)
        {
            if (TryGetServiceSlot(serviceIdentity, out KernelRuntimeServiceSlot slot))
            {
                slotIndex = slot.SlotIndex;
                return true;
            }

            slotIndex = -1;
            return false;
        }

        public KernelDiagnostic CreateMissingServiceSlotDiagnostic(RuntimeIdentityRef serviceIdentity)
        {
            if (serviceIdentity.Kind != RuntimeIdentityKind.Service || serviceIdentity.Value <= 0)
            {
                return new KernelDiagnostic(
                    new DiagnosticCode(KernelRuntimeServiceGraphCodes.ServiceSlotInvalidIdentity),
                    DiagnosticSeverity.Error,
                    DiagnosticDomain.ServiceGraph,
                    DiagnosticFailureBoundary.Kernel,
                    "Service slot lookup requires a verified service identity.",
                    new DiagnosticContext(runtimeIdentities: new[] { serviceIdentity }, phase: "ServiceGraph.Resolve"));
            }

            return new KernelDiagnostic(
                new DiagnosticCode(KernelRuntimeServiceGraphCodes.ServiceSlotMissing),
                DiagnosticSeverity.Error,
                DiagnosticDomain.ServiceGraph,
                DiagnosticFailureBoundary.Kernel,
                "Service slot could not be resolved from the verified service graph.",
                new DiagnosticContext(runtimeIdentities: new[] { serviceIdentity }, phase: "ServiceGraph.Resolve"));
        }

        public KernelRuntimeServiceResolutionResult ResolveRequiredService(RuntimeIdentityRef serviceIdentity)
        {
            return ResolveRequiredService(null, serviceIdentity);
        }

        public KernelRuntimeServiceResolutionResult ResolveRequiredService(KernelRuntimeServiceSlot? requestingSlot, RuntimeIdentityRef serviceIdentity)
        {
            if (TryGetServiceSlot(serviceIdentity, out KernelRuntimeServiceSlot slot))
                return KernelRuntimeServiceResolutionResult.Resolved(serviceIdentity, slot, requestingSlot);

            return KernelRuntimeServiceResolutionResult.MissingRequired(serviceIdentity, CreateRequiredServiceMissingDiagnostic(requestingSlot, serviceIdentity), requestingSlot);
        }

        public KernelRuntimeServiceResolutionResult ResolveOptionalService(
            KernelRuntimeServiceSlot? requestingSlot,
            RuntimeIdentityRef serviceIdentity,
            OptionalDependencyAbsenceBehavior absenceBehavior,
            KernelProfileMask profileSpecificErrorProfiles = KernelProfileMask.None,
            RuntimeIdentityRef? alternativeServiceIdentity = null)
        {
            if (TryGetServiceSlot(serviceIdentity, out KernelRuntimeServiceSlot resolvedSlot))
                return KernelRuntimeServiceResolutionResult.Resolved(serviceIdentity, resolvedSlot, requestingSlot);

            if (!selectedProfileKind.HasValue)
            {
                return KernelRuntimeServiceResolutionResult.Rejected(
                    serviceIdentity,
                    CreateOptionalProfileMissingDiagnostic(requestingSlot, serviceIdentity, absenceBehavior, profileSpecificErrorProfiles, alternativeServiceIdentity),
                    requestingSlot,
                    absenceBehavior,
                    null,
                    profileSpecificErrorProfiles,
                    alternativeServiceIdentity);
            }

            KernelProfileKind currentProfileKind = this.selectedProfileKind.Value;

            switch (absenceBehavior)
            {
                case OptionalDependencyAbsenceBehavior.DisableContribution:
                    return KernelRuntimeServiceResolutionResult.OptionalAbsent(
                        serviceIdentity,
                        CreateOptionalAbsentDiagnostic(requestingSlot, serviceIdentity, absenceBehavior, currentProfileKind, profileSpecificErrorProfiles, DiagnosticSeverity.Info),
                        requestingSlot,
                        absenceBehavior,
                        currentProfileKind,
                        profileSpecificErrorProfiles);

                case OptionalDependencyAbsenceBehavior.EmitWarning:
                    return KernelRuntimeServiceResolutionResult.OptionalAbsent(
                        serviceIdentity,
                        CreateOptionalAbsentDiagnostic(requestingSlot, serviceIdentity, absenceBehavior, currentProfileKind, profileSpecificErrorProfiles, DiagnosticSeverity.Warning),
                        requestingSlot,
                        absenceBehavior,
                        currentProfileKind,
                        profileSpecificErrorProfiles);

                case OptionalDependencyAbsenceBehavior.UseExplicitAlternative:
                    if (!alternativeServiceIdentity.HasValue)
                    {
                        return KernelRuntimeServiceResolutionResult.Rejected(
                            serviceIdentity,
                            CreateOptionalAlternativeMissingDiagnostic(requestingSlot, serviceIdentity, absenceBehavior, currentProfileKind, profileSpecificErrorProfiles, alternativeServiceIdentity),
                            requestingSlot,
                            absenceBehavior,
                            currentProfileKind,
                            profileSpecificErrorProfiles,
                            alternativeServiceIdentity);
                    }

                    if (!TryGetServiceSlot(alternativeServiceIdentity.Value, out KernelRuntimeServiceSlot alternativeSlot))
                    {
                        return KernelRuntimeServiceResolutionResult.Rejected(
                            serviceIdentity,
                            CreateOptionalAlternativeMissingDiagnostic(requestingSlot, serviceIdentity, absenceBehavior, currentProfileKind, profileSpecificErrorProfiles, alternativeServiceIdentity),
                            requestingSlot,
                            absenceBehavior,
                            currentProfileKind,
                            profileSpecificErrorProfiles,
                            alternativeServiceIdentity);
                    }

                    if (!IsAlternativeCompatible(requestingSlot, alternativeSlot))
                    {
                        return KernelRuntimeServiceResolutionResult.Rejected(
                            serviceIdentity,
                            CreateOptionalAlternativeCompatibilityDiagnostic(requestingSlot, serviceIdentity, alternativeSlot, absenceBehavior, currentProfileKind, profileSpecificErrorProfiles, alternativeServiceIdentity.Value),
                            requestingSlot,
                            absenceBehavior,
                            currentProfileKind,
                            profileSpecificErrorProfiles,
                            alternativeServiceIdentity);
                    }

                    return KernelRuntimeServiceResolutionResult.OptionalAlternativeResolved(
                        serviceIdentity,
                        alternativeSlot,
                        requestingSlot,
                        alternativeServiceIdentity.Value,
                        absenceBehavior,
                        currentProfileKind,
                        profileSpecificErrorProfiles);

                case OptionalDependencyAbsenceBehavior.ProfileSpecificError:
                    if (profileSpecificErrorProfiles == KernelProfileMask.None)
                    {
                        return KernelRuntimeServiceResolutionResult.Rejected(
                            serviceIdentity,
                            CreateOptionalProfileBoundaryMissingDiagnostic(requestingSlot, serviceIdentity, absenceBehavior, currentProfileKind),
                            requestingSlot,
                            absenceBehavior,
                            currentProfileKind,
                            profileSpecificErrorProfiles);
                    }

                    if (IsProfileInBoundary(selectedProfileKind, profileSpecificErrorProfiles))
                    {
                        return KernelRuntimeServiceResolutionResult.Rejected(
                            serviceIdentity,
                            CreateOptionalProfileErrorDiagnostic(requestingSlot, serviceIdentity, absenceBehavior, currentProfileKind, profileSpecificErrorProfiles),
                            requestingSlot,
                            absenceBehavior,
                            currentProfileKind,
                            profileSpecificErrorProfiles);
                    }

                    return KernelRuntimeServiceResolutionResult.OptionalAbsent(
                        serviceIdentity,
                        CreateOptionalAbsentDiagnostic(requestingSlot, serviceIdentity, absenceBehavior, currentProfileKind, profileSpecificErrorProfiles, DiagnosticSeverity.Warning),
                        requestingSlot,
                        absenceBehavior,
                        currentProfileKind,
                        profileSpecificErrorProfiles);

                default:
                    return KernelRuntimeServiceResolutionResult.Rejected(
                        serviceIdentity,
                        CreateUnsupportedOptionalBehaviorDiagnostic(requestingSlot, serviceIdentity, absenceBehavior, currentProfileKind, profileSpecificErrorProfiles),
                        requestingSlot,
                        absenceBehavior,
                        currentProfileKind,
                        profileSpecificErrorProfiles,
                        alternativeServiceIdentity);
            }
        }

        static bool IsAlternativeCompatible(KernelRuntimeServiceSlot? requestingSlot, KernelRuntimeServiceSlot alternativeSlot)
        {
            if (!requestingSlot.HasValue)
                return false;

            KernelRuntimeServiceSlot requested = requestingSlot.Value;
            if (requested.Lifetime == ServiceLifetimeKind.Unknown || requested.Cardinality == ServiceCardinalityKind.Unknown)
                return false;

            return requested.Lifetime == alternativeSlot.Lifetime
                && requested.Cardinality == alternativeSlot.Cardinality;
        }

        static bool IsProfileInBoundary(KernelProfileKind selectedProfileKind, KernelProfileMask profileBoundary)
        {
            if (!IsDefinedProfileKind(selectedProfileKind))
                return false;

            return (ToProfileMask(selectedProfileKind) & profileBoundary) != 0;
        }

        static bool IsDefinedProfileKind(KernelProfileKind selectedProfileKind)
        {
            switch (selectedProfileKind)
            {
                case KernelProfileKind.Development:
                case KernelProfileKind.Release:
                case KernelProfileKind.Test:
                    return true;
                default:
                    return false;
            }
        }

        static void ValidateSelectedProfileKind(KernelProfileKind? selectedProfileKind)
        {
            if (selectedProfileKind.HasValue && !IsDefinedProfileKind(selectedProfileKind.Value))
                throw new ArgumentOutOfRangeException(nameof(selectedProfileKind), selectedProfileKind.Value, "Kernel runtime service graphs require a defined selected profile kind when one is provided.");
        }

        static KernelProfileMask ToProfileMask(KernelProfileKind profileKind)
        {
            return profileKind switch
            {
                KernelProfileKind.Development => KernelProfileMask.Development,
                KernelProfileKind.Release => KernelProfileMask.Release,
                KernelProfileKind.Test => KernelProfileMask.Test,
                _ => KernelProfileMask.None,
            };
        }

        KernelDiagnostic CreateRequiredServiceMissingDiagnostic(KernelRuntimeServiceSlot? requestingSlot, RuntimeIdentityRef serviceIdentity)
        {
            List<RuntimeIdentityRef> runtimeIdentities = BuildResolutionRuntimeIdentities(requestingSlot, serviceIdentity, null);
            List<DiagnosticPayloadEntry> payloadEntries = BuildResolutionPayloadEntries(requestingSlot, serviceIdentity, null, null, null, null);
            payloadEntries.Add(new DiagnosticPayloadEntry("ResolutionKind", DiagnosticPayloadValue.FromString(KernelRuntimeServiceResolutionKind.MissingRequired.ToString())));

            return new KernelDiagnostic(
                new DiagnosticCode(KernelRuntimeServiceGraphCodes.ServiceRequiredMissing),
                DiagnosticSeverity.Error,
                DiagnosticDomain.ServiceGraph,
                DiagnosticFailureBoundary.Kernel,
                "Required service could not be resolved from the verified service graph.",
                new DiagnosticContext(runtimeIdentities.Count == 0 ? null : runtimeIdentities.ToArray(), phase: "ServiceGraph.Resolve"),
                new DiagnosticPayload(payloadEntries));
        }

        KernelDiagnostic CreateOptionalAbsentDiagnostic(
            KernelRuntimeServiceSlot? requestingSlot,
            RuntimeIdentityRef serviceIdentity,
            OptionalDependencyAbsenceBehavior absenceBehavior,
            KernelProfileKind selectedProfileKind,
            KernelProfileMask profileSpecificErrorProfiles,
            DiagnosticSeverity severity)
        {
            List<RuntimeIdentityRef> runtimeIdentities = BuildResolutionRuntimeIdentities(requestingSlot, serviceIdentity, null);
            List<DiagnosticPayloadEntry> payloadEntries = BuildResolutionPayloadEntries(requestingSlot, serviceIdentity, absenceBehavior, selectedProfileKind, profileSpecificErrorProfiles, null);
            payloadEntries.Add(new DiagnosticPayloadEntry("ResolutionKind", DiagnosticPayloadValue.FromString(KernelRuntimeServiceResolutionKind.OptionalAbsent.ToString())));

            return new KernelDiagnostic(
                new DiagnosticCode(KernelRuntimeServiceGraphCodes.ServiceOptionalAbsent),
                severity,
                DiagnosticDomain.ServiceGraph,
                DiagnosticFailureBoundary.Kernel,
                "Optional service was absent and resolved through the declared absence behavior.",
                new DiagnosticContext(runtimeIdentities.Count == 0 ? null : runtimeIdentities.ToArray(), phase: "ServiceGraph.Resolve"),
                new DiagnosticPayload(payloadEntries));
        }

        KernelDiagnostic CreateOptionalAlternativeMissingDiagnostic(
            KernelRuntimeServiceSlot? requestingSlot,
            RuntimeIdentityRef serviceIdentity,
            OptionalDependencyAbsenceBehavior absenceBehavior,
            KernelProfileKind selectedProfileKind,
            KernelProfileMask profileSpecificErrorProfiles,
            RuntimeIdentityRef? alternativeServiceIdentity)
        {
            List<RuntimeIdentityRef> runtimeIdentities = BuildResolutionRuntimeIdentities(requestingSlot, serviceIdentity, alternativeServiceIdentity);
            List<DiagnosticPayloadEntry> payloadEntries = BuildResolutionPayloadEntries(requestingSlot, serviceIdentity, absenceBehavior, selectedProfileKind, profileSpecificErrorProfiles, alternativeServiceIdentity);
            payloadEntries.Add(new DiagnosticPayloadEntry("ResolutionKind", DiagnosticPayloadValue.FromString(KernelRuntimeServiceResolutionKind.Rejected.ToString())));

            return new KernelDiagnostic(
                new DiagnosticCode(KernelRuntimeServiceGraphCodes.ServiceOptionalAlternativeMissing),
                DiagnosticSeverity.Error,
                DiagnosticDomain.ServiceGraph,
                DiagnosticFailureBoundary.Kernel,
                "Optional service declared an explicit alternative but the alternative target is missing or unavailable.",
                new DiagnosticContext(runtimeIdentities.Count == 0 ? null : runtimeIdentities.ToArray(), phase: "ServiceGraph.Resolve"),
                new DiagnosticPayload(payloadEntries));
        }

        KernelDiagnostic CreateOptionalAlternativeCompatibilityDiagnostic(
            KernelRuntimeServiceSlot? requestingSlot,
            RuntimeIdentityRef serviceIdentity,
            KernelRuntimeServiceSlot alternativeSlot,
            OptionalDependencyAbsenceBehavior absenceBehavior,
            KernelProfileKind selectedProfileKind,
            KernelProfileMask profileSpecificErrorProfiles,
            RuntimeIdentityRef alternativeServiceIdentity)
        {
            List<RuntimeIdentityRef> runtimeIdentities = BuildResolutionRuntimeIdentities(requestingSlot, serviceIdentity, alternativeServiceIdentity, alternativeSlot.ServiceIdentity);
            List<DiagnosticPayloadEntry> payloadEntries = BuildResolutionPayloadEntries(requestingSlot, serviceIdentity, absenceBehavior, selectedProfileKind, profileSpecificErrorProfiles, alternativeServiceIdentity);
            payloadEntries.Add(new DiagnosticPayloadEntry("AlternativeLifetime", DiagnosticPayloadValue.FromString(alternativeSlot.Lifetime.ToString())));
            payloadEntries.Add(new DiagnosticPayloadEntry("AlternativeCardinality", DiagnosticPayloadValue.FromString(alternativeSlot.Cardinality.ToString())));
            payloadEntries.Add(new DiagnosticPayloadEntry("ResolutionKind", DiagnosticPayloadValue.FromString(KernelRuntimeServiceResolutionKind.Rejected.ToString())));

            return new KernelDiagnostic(
                new DiagnosticCode(KernelRuntimeServiceGraphCodes.ServiceOptionalAlternativeIncompatible),
                DiagnosticSeverity.Error,
                DiagnosticDomain.ServiceGraph,
                DiagnosticFailureBoundary.Kernel,
                "Optional service alternative is present but not lifetime-compatible with the requesting service.",
                new DiagnosticContext(runtimeIdentities.Count == 0 ? null : runtimeIdentities.ToArray(), phase: "ServiceGraph.Resolve"),
                new DiagnosticPayload(payloadEntries));
        }

        KernelDiagnostic CreateOptionalProfileErrorDiagnostic(
            KernelRuntimeServiceSlot? requestingSlot,
            RuntimeIdentityRef serviceIdentity,
            OptionalDependencyAbsenceBehavior absenceBehavior,
            KernelProfileKind selectedProfileKind,
            KernelProfileMask profileSpecificErrorProfiles)
        {
            List<RuntimeIdentityRef> runtimeIdentities = BuildResolutionRuntimeIdentities(requestingSlot, serviceIdentity, null);
            List<DiagnosticPayloadEntry> payloadEntries = BuildResolutionPayloadEntries(requestingSlot, serviceIdentity, absenceBehavior, selectedProfileKind, profileSpecificErrorProfiles, null);
            payloadEntries.Add(new DiagnosticPayloadEntry("ResolutionKind", DiagnosticPayloadValue.FromString(KernelRuntimeServiceResolutionKind.Rejected.ToString())));

            return new KernelDiagnostic(
                new DiagnosticCode(KernelRuntimeServiceGraphCodes.ServiceOptionalProfileError),
                DiagnosticSeverity.Error,
                DiagnosticDomain.ServiceGraph,
                DiagnosticFailureBoundary.Kernel,
                "Optional service was absent and the selected profile upgrades the absence to an error.",
                new DiagnosticContext(runtimeIdentities.Count == 0 ? null : runtimeIdentities.ToArray(), phase: "ServiceGraph.Resolve"),
                new DiagnosticPayload(payloadEntries));
        }

        KernelDiagnostic CreateOptionalProfileMissingDiagnostic(
            KernelRuntimeServiceSlot? requestingSlot,
            RuntimeIdentityRef serviceIdentity,
            OptionalDependencyAbsenceBehavior absenceBehavior,
            KernelProfileMask profileSpecificErrorProfiles,
            RuntimeIdentityRef? alternativeServiceIdentity)
        {
            List<RuntimeIdentityRef> runtimeIdentities = BuildResolutionRuntimeIdentities(requestingSlot, serviceIdentity, alternativeServiceIdentity);
            List<DiagnosticPayloadEntry> payloadEntries = BuildResolutionPayloadEntries(requestingSlot, serviceIdentity, absenceBehavior, null, profileSpecificErrorProfiles, alternativeServiceIdentity);
            payloadEntries.Add(new DiagnosticPayloadEntry("ResolutionKind", DiagnosticPayloadValue.FromString(KernelRuntimeServiceResolutionKind.Rejected.ToString())));

            return new KernelDiagnostic(
                new DiagnosticCode(KernelRuntimeServiceGraphCodes.ServiceOptionalProfileMissing),
                DiagnosticSeverity.Error,
                DiagnosticDomain.ServiceGraph,
                DiagnosticFailureBoundary.Kernel,
                "Optional service declared profile-specific behavior but no selected profile is available in the runtime service graph.",
                new DiagnosticContext(runtimeIdentities.Count == 0 ? null : runtimeIdentities.ToArray(), phase: "ServiceGraph.Resolve"),
                new DiagnosticPayload(payloadEntries));
        }

        KernelDiagnostic CreateOptionalProfileBoundaryMissingDiagnostic(
            KernelRuntimeServiceSlot? requestingSlot,
            RuntimeIdentityRef serviceIdentity,
            OptionalDependencyAbsenceBehavior absenceBehavior,
            KernelProfileKind selectedProfileKind)
        {
            List<RuntimeIdentityRef> runtimeIdentities = BuildResolutionRuntimeIdentities(requestingSlot, serviceIdentity, null);
            List<DiagnosticPayloadEntry> payloadEntries = BuildResolutionPayloadEntries(requestingSlot, serviceIdentity, absenceBehavior, selectedProfileKind, KernelProfileMask.None, null);
            payloadEntries.Add(new DiagnosticPayloadEntry("ResolutionKind", DiagnosticPayloadValue.FromString(KernelRuntimeServiceResolutionKind.Rejected.ToString())));

            return new KernelDiagnostic(
                new DiagnosticCode(KernelRuntimeServiceGraphCodes.ServiceOptionalProfileBoundaryMissing),
                DiagnosticSeverity.Error,
                DiagnosticDomain.ServiceGraph,
                DiagnosticFailureBoundary.Kernel,
                "Optional service declared profile-specific behavior but no profile boundary was provided.",
                new DiagnosticContext(runtimeIdentities.Count == 0 ? null : runtimeIdentities.ToArray(), phase: "ServiceGraph.Resolve"),
                new DiagnosticPayload(payloadEntries));
        }

        KernelDiagnostic CreateUnsupportedOptionalBehaviorDiagnostic(
            KernelRuntimeServiceSlot? requestingSlot,
            RuntimeIdentityRef serviceIdentity,
            OptionalDependencyAbsenceBehavior absenceBehavior,
            KernelProfileKind selectedProfileKind,
            KernelProfileMask profileSpecificErrorProfiles)
        {
            List<RuntimeIdentityRef> runtimeIdentities = BuildResolutionRuntimeIdentities(requestingSlot, serviceIdentity, null);
            List<DiagnosticPayloadEntry> payloadEntries = BuildResolutionPayloadEntries(requestingSlot, serviceIdentity, absenceBehavior, selectedProfileKind, profileSpecificErrorProfiles, null);
            payloadEntries.Add(new DiagnosticPayloadEntry("ResolutionKind", DiagnosticPayloadValue.FromString(KernelRuntimeServiceResolutionKind.Rejected.ToString())));

            return new KernelDiagnostic(
                new DiagnosticCode(KernelRuntimeServiceGraphCodes.ServiceOptionalUnsupportedBehavior),
                DiagnosticSeverity.Error,
                DiagnosticDomain.ServiceGraph,
                DiagnosticFailureBoundary.Kernel,
                "Optional service resolution received an unsupported absence behavior.",
                new DiagnosticContext(runtimeIdentities.Count == 0 ? null : runtimeIdentities.ToArray(), phase: "ServiceGraph.Resolve"),
                new DiagnosticPayload(payloadEntries));
        }

        static List<RuntimeIdentityRef> BuildResolutionRuntimeIdentities(
            KernelRuntimeServiceSlot? requestingSlot,
            RuntimeIdentityRef requestedServiceIdentity,
            RuntimeIdentityRef? alternativeServiceIdentity,
            RuntimeIdentityRef? alternativeResolvedServiceIdentity = null)
        {
            List<RuntimeIdentityRef> runtimeIdentities = new List<RuntimeIdentityRef>(4);

            if (requestingSlot.HasValue)
                runtimeIdentities.Add(requestingSlot.Value.ServiceIdentity);

            runtimeIdentities.Add(requestedServiceIdentity);

            if (alternativeServiceIdentity.HasValue)
                runtimeIdentities.Add(alternativeServiceIdentity.Value);

            if (alternativeResolvedServiceIdentity.HasValue)
                runtimeIdentities.Add(alternativeResolvedServiceIdentity.Value);

            return runtimeIdentities;
        }

        static List<DiagnosticPayloadEntry> BuildResolutionPayloadEntries(
            KernelRuntimeServiceSlot? requestingSlot,
            RuntimeIdentityRef requestedServiceIdentity,
            OptionalDependencyAbsenceBehavior? absenceBehavior,
            KernelProfileKind? selectedProfileKind,
            KernelProfileMask profileSpecificErrorProfiles,
            RuntimeIdentityRef? alternativeServiceIdentity)
        {
            List<DiagnosticPayloadEntry> payloadEntries = new List<DiagnosticPayloadEntry>(12)
            {
                new DiagnosticPayloadEntry("RequestedServiceIdentity", DiagnosticPayloadValue.FromString(requestedServiceIdentity.ToString())),
                new DiagnosticPayloadEntry("ProfileSpecificErrorProfiles", DiagnosticPayloadValue.FromString(profileSpecificErrorProfiles.ToString())),
            };

            if (selectedProfileKind.HasValue)
            {
                payloadEntries.Add(new DiagnosticPayloadEntry("SelectedProfileKind", DiagnosticPayloadValue.FromString(selectedProfileKind.Value.ToString())));
                payloadEntries.Add(new DiagnosticPayloadEntry("SelectedProfileMask", DiagnosticPayloadValue.FromString(ToProfileMask(selectedProfileKind.Value).ToString())));
            }

            if (requestingSlot.HasValue)
            {
                KernelRuntimeServiceSlot slot = requestingSlot.Value;
                payloadEntries.Add(new DiagnosticPayloadEntry("RequestingServiceIdentity", DiagnosticPayloadValue.FromString(slot.ServiceIdentity.ToString())));
                payloadEntries.Add(new DiagnosticPayloadEntry("RequestingSlotIndex", DiagnosticPayloadValue.FromInt32(slot.SlotIndex)));
                payloadEntries.Add(new DiagnosticPayloadEntry("RequestingOwnerModule", DiagnosticPayloadValue.FromInt32(slot.OwnerModule.Value)));
                payloadEntries.Add(new DiagnosticPayloadEntry("RequestingLifetime", DiagnosticPayloadValue.FromString(slot.Lifetime.ToString())));
                payloadEntries.Add(new DiagnosticPayloadEntry("RequestingCardinality", DiagnosticPayloadValue.FromString(slot.Cardinality.ToString())));
                payloadEntries.Add(new DiagnosticPayloadEntry("RequestingFactoryKind", DiagnosticPayloadValue.FromString(slot.FactoryKind.ToString())));
                payloadEntries.Add(new DiagnosticPayloadEntry("RequestingFactorySource", DiagnosticPayloadValue.FromInt32(slot.FactorySource.Value)));
            }

            if (absenceBehavior.HasValue)
                payloadEntries.Add(new DiagnosticPayloadEntry("AbsenceBehavior", DiagnosticPayloadValue.FromString(absenceBehavior.Value.ToString())));

            if (alternativeServiceIdentity.HasValue)
                payloadEntries.Add(new DiagnosticPayloadEntry("AlternativeServiceIdentity", DiagnosticPayloadValue.FromString(alternativeServiceIdentity.Value.ToString())));

            return payloadEntries;
        }

        static void ValidateRootServiceIdentities(ReadOnlySpan<RuntimeIdentityRef> identities)
        {
            HashSet<RuntimeIdentityRef> seen = new HashSet<RuntimeIdentityRef>();
            for (int index = 0; index < identities.Length; index++)
            {
                RuntimeIdentityRef identity = identities[index];
                if (identity.Kind != RuntimeIdentityKind.Service || identity.Value <= 0)
                    throw new ArgumentException("Kernel runtime service graphs must be built from verified service identities.", nameof(identities));

                if (!seen.Add(identity))
                    throw new ArgumentException("Kernel runtime service graphs must not contain duplicate service identities.", nameof(identities));
            }
        }

        static RuntimeIdentityRef[] BuildRootServiceIdentities(ReadOnlySpan<KernelRuntimeServiceSlot> slots)
        {
            if (slots.Length == 0)
                return Array.Empty<RuntimeIdentityRef>();

            RuntimeIdentityRef[] identities = new RuntimeIdentityRef[slots.Length];
            for (int index = 0; index < slots.Length; index++)
            {
                KernelRuntimeServiceSlot slot = slots[index];
                RuntimeIdentityRef identity = slot.ServiceIdentity;
                if (identity.Value <= 0)
                    throw new ArgumentException("Kernel runtime service graphs must be backed by verified service identities.", nameof(slots));

                identities[index] = identity;
            }

            return identities;
        }

        static KernelRuntimeServiceSlot[] BuildServiceSlots(ReadOnlySpan<ServiceSlotPlan> slots)
        {
            if (slots.Length == 0)
                return Array.Empty<KernelRuntimeServiceSlot>();

            KernelRuntimeServiceSlot[] runtimeSlots = new KernelRuntimeServiceSlot[slots.Length];
            for (int index = 0; index < slots.Length; index++)
            {
                ServiceSlotPlan slot = slots[index];
                RuntimeIdentityRef identity = new RuntimeIdentityRef(RuntimeIdentityKind.Service, slot.ServiceId.Value);
                runtimeSlots[index] = new KernelRuntimeServiceSlot(
                    slot.SlotIndex,
                    slot.EntryIndex,
                    identity,
                    slot.Factory.FactoryKind,
                    slot.Factory.Source,
                    slot.Entry.Name,
                    slot,
                    slot.Lifetime,
                    slot.Cardinality,
                    slot.OwnerModule);
            }

            Array.Sort(runtimeSlots, CompareServiceSlot);
            return runtimeSlots;
        }

        static int CompareServiceSlot(KernelRuntimeServiceSlot left, KernelRuntimeServiceSlot right)
        {
            int comparison = CompareServiceIdentity(left.ServiceIdentity, right.ServiceIdentity);
            if (comparison != 0)
                return comparison;

            comparison = left.SlotIndex.CompareTo(right.SlotIndex);
            if (comparison != 0)
                return comparison;

            return left.EntryIndex.CompareTo(right.EntryIndex);
        }

        static int CompareServiceIdentity(RuntimeIdentityRef left, RuntimeIdentityRef right)
        {
            int comparison = left.Kind.CompareTo(right.Kind);
            if (comparison != 0)
                return comparison;

            comparison = left.Value.CompareTo(right.Value);
            if (comparison != 0)
                return comparison;

            return left.Generation.CompareTo(right.Generation);
        }
    }

    public sealed class KernelRuntimeScopeGraph
    {
        readonly KernelScopeGraphRuntime runtime;

        public KernelRuntimeScopeGraph(
            ScopeGraphPlan scopeGraphPlan,
            ReadOnlySpan<RuntimeIdentityRef> rootScopeIdentities,
            ILifecyclePlanResolver? lifecyclePlanResolver = null)
        {
            runtime = new KernelScopeGraphRuntime(scopeGraphPlan, rootScopeIdentities, lifecyclePlanResolver);
        }

        public IReadOnlyList<RuntimeIdentityRef> RootScopeIdentities => runtime.RootScopeIdentities;

        public IReadOnlyList<ScopeHandle> RootScopeHandles => runtime.RootScopeHandles;

        public int RootScopeCount => runtime.RootScopeCount;

        public bool IsEmpty => runtime.IsEmpty;

        public bool TryGetScope(ScopeHandle handle, out ScopeRuntimeSnapshot snapshot)
        {
            return runtime.TryGetScope(handle, out snapshot);
        }

        public bool TryGetScopeBoundary(ScopeHandle handle, out ScopeBoundarySnapshot snapshot)
        {
            return runtime.TryGetScopeBoundary(handle, out snapshot);
        }

        public bool TryGetScopeBoundaryChanges(ScopeHandle handle, out IReadOnlyList<ScopeBoundaryChangeRecord> changes)
        {
            return runtime.TryGetScopeBoundaryChanges(handle, out changes);
        }

        public bool TryGetScopeValueInitPlans(ScopeHandle handle, out IReadOnlyList<ValueInitPlanIR> valueInitPlans)
        {
            return runtime.TryGetScopeValueInitPlans(handle, out valueInitPlans);
        }

        public bool TryGetLifecycleTransitionRequests(ScopeHandle handle, out IReadOnlyList<ScopeLifecycleTransitionRequest> requests)
        {
            return runtime.TryGetLifecycleTransitionRequests(handle, out requests);
        }

        public bool TryGetScopeBoundary(
            ScopeHandle handle,
            out ScopeBoundarySnapshot snapshot,
            out ScopeBoundaryAccessFailureKind failureKind,
            out KernelDiagnostic? diagnostic)
        {
            return runtime.TryGetScopeBoundary(handle, out snapshot, out failureKind, out diagnostic);
        }

        public bool TrySetUnityLink(ScopeHandle handle, UnityObjectLink unityLink)
        {
            return runtime.TrySetUnityLink(handle, unityLink);
        }

        public bool TrySetUnityLink(ScopeHandle handle, UnityObjectLink unityLink, out KernelDiagnostic? diagnostic)
        {
            return runtime.TrySetUnityLink(handle, unityLink, out diagnostic);
        }

        public bool TryGetChildHandles(ScopeHandle handle, out IReadOnlyList<ScopeHandle> childHandles)
        {
            return runtime.TryGetChildHandles(handle, out childHandles);
        }

        public ScopeHandle CreateScope(ScopeCreateRequest request)
        {
            return runtime.CreateScope(request);
        }

        public bool TryDestroyScope(ScopeHandle handle)
        {
            return runtime.TryDestroyScope(handle);
        }

        public bool TryDetachScope(ScopeHandle handle)
        {
            return runtime.TryDetachScope(handle);
        }

        public bool TryReparentScope(ScopeHandle childHandle, ScopeHandle newParentHandle)
        {
            return runtime.TryReparentScope(childHandle, newParentHandle);
        }

        public bool TrySetState(ScopeHandle handle, ScopeRuntimeState nextState)
        {
            return runtime.TrySetState(handle, nextState);
        }

        public bool TrySetState(ScopeHandle handle, ScopeRuntimeState nextState, out ScopeStateTransitionFailureKind failureKind)
        {
            return runtime.TrySetState(handle, nextState, out failureKind);
        }

        public bool TrySetState(
            ScopeHandle handle,
            ScopeRuntimeState nextState,
            out ScopeStateTransitionFailureKind failureKind,
            out KernelDiagnostic? diagnostic)
        {
            return runtime.TrySetState(handle, nextState, out failureKind, out diagnostic);
        }
    }

    public sealed class KernelBootRuntimeSurface : IKernelBootRuntimeSurface
    {
        public KernelBootRuntimeSurface(KernelRuntime runtime)
        {
            Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public KernelRuntime Runtime { get; }

        public KernelLifecycleDispatcher? LifecycleDispatcher => Runtime.LifecycleDispatcher;

        public ILifecyclePlanResolver LifecyclePlanResolver => Runtime.LifecyclePlanResolver;

        public Task<LifecycleDispatchResult> DispatchAllLifecycleAsync(IAsyncLifecycleDispatchExecutor executor, CancellationToken cancellationToken = default)
        {
            KernelLifecycleDispatcher dispatcher = LifecycleDispatcher ?? throw new InvalidOperationException("Kernel runtime does not expose a lifecycle dispatcher.");
            return dispatcher.DispatchAllAsync(executor, cancellationToken);
        }

        public Task<LifecycleDispatchResult> DispatchPhaseLifecycleAsync(LifecyclePhase phase, IAsyncLifecycleDispatchExecutor executor, CancellationToken cancellationToken = default)
        {
            KernelLifecycleDispatcher dispatcher = LifecycleDispatcher ?? throw new InvalidOperationException("Kernel runtime does not expose a lifecycle dispatcher.");
            return dispatcher.DispatchPhaseAsync(phase, executor, cancellationToken);
        }
    }

    public sealed class KernelBootRuntimeSurfaceFactory : IKernelBootRuntimeSurfaceFactory
    {
        public IKernelBootRuntimeSurface Create(KernelBootBoundaryContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            return new KernelBootRuntimeSurface(new KernelRuntime(context));
        }
    }
}
#nullable enable
using System;
using System.Collections.Generic;
using Game.Kernel.Diagnostics;
using Game.Kernel.IR;
using Game.Kernel.Validation;

namespace Game.Kernel.Generation
{
    public sealed class KernelProjectionGenerationResult
    {
        public KernelProjectionGenerationResult(
            KernelProjectionSet projections,
            GeneratedKernelPlan generatedPlan,
            KernelPlanVerificationResult planVerification,
            ProjectionValidationReport projectionValidationReport)
        {
            Projections = projections ?? throw new ArgumentNullException(nameof(projections));
            GeneratedPlan = generatedPlan ?? throw new ArgumentNullException(nameof(generatedPlan));
            PlanVerification = planVerification ?? throw new ArgumentNullException(nameof(planVerification));
            ProjectionValidationReport = projectionValidationReport ?? throw new ArgumentNullException(nameof(projectionValidationReport));
        }

        public KernelProjectionSet Projections { get; }

        public GeneratedKernelPlan GeneratedPlan { get; }

        public KernelPlanVerificationResult PlanVerification { get; }

        public ProjectionValidationReport ProjectionValidationReport { get; }

        public bool IsVerified => PlanVerification.IsVerified && ProjectionValidationReport.Status == ValidationResultStatus.Passed;
    }

    public sealed class KernelProjectionSet
    {
        public KernelProjectionSet(
            ServiceGraphPlan serviceGraph,
            ScopeGraphPlan scopeGraph,
            LifecyclePlan lifecyclePlan,
            CommandCatalogPlan commandCatalog,
            ValueSchemaPlan valueSchema,
            RuntimeQueryPlan runtimeQuery,
            KernelDebugMap debugMap,
            GenerationReport generationReport,
            ValidationReport validationReport)
        {
            ServiceGraph = serviceGraph ?? throw new ArgumentNullException(nameof(serviceGraph));
            ScopeGraph = scopeGraph ?? throw new ArgumentNullException(nameof(scopeGraph));
            LifecyclePlan = lifecyclePlan ?? throw new ArgumentNullException(nameof(lifecyclePlan));
            CommandCatalog = commandCatalog ?? throw new ArgumentNullException(nameof(commandCatalog));
            ValueSchema = valueSchema ?? throw new ArgumentNullException(nameof(valueSchema));
            RuntimeQuery = runtimeQuery ?? throw new ArgumentNullException(nameof(runtimeQuery));
            DebugMap = debugMap ?? throw new ArgumentNullException(nameof(debugMap));
            GenerationReport = generationReport ?? throw new ArgumentNullException(nameof(generationReport));
            ValidationReport = validationReport ?? throw new ArgumentNullException(nameof(validationReport));
        }

        public ServiceGraphPlan ServiceGraph { get; }

        public ScopeGraphPlan ScopeGraph { get; }

        public LifecyclePlan LifecyclePlan { get; }

        public CommandCatalogPlan CommandCatalog { get; }

        public ValueSchemaPlan ValueSchema { get; }

        public RuntimeQueryPlan RuntimeQuery { get; }

        public KernelDebugMap DebugMap { get; }

        public GenerationReport GenerationReport { get; }

        public ValidationReport ValidationReport { get; }
    }

    public sealed class ServiceGraphPlan
    {
        readonly ServiceIR[] services;
        readonly ServiceEntryPlan[] entries;
        readonly ServiceSlotPlan[] slots;

        public ServiceGraphPlan(VerifiedArtifactHeader header, ReadOnlySpan<ServiceIR> services)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            KernelProjectionArtifactKindValidator.ValidateArtifactKind(header, ArtifactKind.ServiceGraph);
            (this.services, entries, slots) = BuildProjection(services);
            ContentHash = KernelProjectionHashing.ComputeServiceGraphHash(this.services, entries, slots);
            KernelProjectionHashing.ValidateHeaderHash(header, ContentHash);
        }

        public VerifiedArtifactHeader Header { get; }

        public ReadOnlySpan<ServiceIR> Services => services;

        public ReadOnlySpan<ServiceEntryPlan> Entries => entries;

        public ReadOnlySpan<ServiceSlotPlan> Slots => slots;

        public Hash128 ContentHash { get; }

        internal static (ServiceIR[] Services, ServiceEntryPlan[] Entries, ServiceSlotPlan[] Slots) BuildProjection(ReadOnlySpan<ServiceIR> services)
        {
            ServiceIR[] sortedServices = KernelProjectionArrayHelpers.CloneAndSort(services, static (left, right) => left.Id.Value.CompareTo(right.Id.Value));

            for (int index = 1; index < sortedServices.Length; index++)
            {
                if (sortedServices[index - 1].Id == sortedServices[index].Id)
                    throw new ArgumentException("ServiceGraphPlan requires unique ServiceId values.", nameof(services));
            }

            ServiceEntryPlan[] entries = new ServiceEntryPlan[sortedServices.Length];
            ServiceSlotPlan[] slots = new ServiceSlotPlan[sortedServices.Length];

            for (int index = 0; index < sortedServices.Length; index++)
            {
                ServiceEntryPlan entry = new ServiceEntryPlan(sortedServices[index]);
                entries[index] = entry;
                slots[index] = new ServiceSlotPlan(index, index, entry);
            }

            return (sortedServices, entries, slots);
        }
    }

    public sealed class ServiceEntryPlan
    {
        readonly ServiceContractRef[] contracts;
        readonly ServiceDependencyIR[] dependencies;

        public ServiceEntryPlan(ServiceIR service)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            ServiceId = service.Id;
            Name = service.Name;
            Lifetime = service.Lifetime;
            Cardinality = service.Cardinality;
            OwnerModule = service.OwnerModule;
            Factory = new ServiceFactoryRef(service.FactoryKind, service.Source, service.Name);
            Source = service.Source;

            ServiceContractIR[] serviceContracts = service.Contracts.ToArray();
            Array.Sort(serviceContracts, static (left, right) => CompareContract(left, right));
            contracts = new ServiceContractRef[serviceContracts.Length];
            for (int index = 0; index < serviceContracts.Length; index++)
                contracts[index] = new ServiceContractRef(serviceContracts[index]);

            ServiceDependencyIR[] serviceDependencies = service.Dependencies.ToArray();
            Array.Sort(serviceDependencies, static (left, right) => StringComparer.Ordinal.Compare(left.Target.ToString(), right.Target.ToString()));
            dependencies = new ServiceDependencyIR[serviceDependencies.Length];
            for (int index = 0; index < serviceDependencies.Length; index++)
                dependencies[index] = serviceDependencies[index];
        }

        public ServiceId ServiceId { get; }

        public string Name { get; }

        public ServiceLifetimeKind Lifetime { get; }

        public ServiceCardinalityKind Cardinality { get; }

        public ModuleId OwnerModule { get; }

        public ServiceFactoryRef Factory { get; }

        public ReadOnlySpan<ServiceContractRef> Contracts => contracts;

        public ReadOnlySpan<ServiceDependencyIR> Dependencies => dependencies;

        public SourceLocationId Source { get; }

        static int CompareContract(ServiceContractIR left, ServiceContractIR right)
        {
            int comparison = StringComparer.Ordinal.Compare(left.ContractName, right.ContractName);
            if (comparison != 0)
                return comparison;

            return left.Source.Value.CompareTo(right.Source.Value);
        }
    }

    public sealed class ServiceSlotPlan
    {
        public ServiceSlotPlan(int slotIndex, int entryIndex, ServiceEntryPlan entry)
        {
            if (slotIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(slotIndex));

            if (entryIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(entryIndex));

            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            SlotIndex = slotIndex;
            EntryIndex = entryIndex;
            Entry = entry;
            ServiceId = entry.ServiceId;
            Lifetime = entry.Lifetime;
            Cardinality = entry.Cardinality;
            OwnerModule = entry.OwnerModule;
            Factory = entry.Factory;
            Source = entry.Source;
        }

        public int SlotIndex { get; }

        public int EntryIndex { get; }

        public ServiceEntryPlan Entry { get; }

        public ServiceId ServiceId { get; }

        public ServiceLifetimeKind Lifetime { get; }

        public ServiceCardinalityKind Cardinality { get; }

        public ModuleId OwnerModule { get; }

        public ServiceFactoryRef Factory { get; }

        public ReadOnlySpan<ServiceContractRef> Contracts => Entry.Contracts;

        public ReadOnlySpan<ServiceDependencyIR> Dependencies => Entry.Dependencies;

        public SourceLocationId Source { get; }
    }

    public sealed class ServiceFactoryRef
    {
        public ServiceFactoryRef(ServiceFactoryKind factoryKind, SourceLocationId source, string? serviceName)
        {
            if (factoryKind == ServiceFactoryKind.Unknown)
                throw new ArgumentException("Service factory refs must provide a factory kind.", nameof(factoryKind));

            if (source.Value == 0)
                throw new ArgumentException("Service factory refs must provide a non-zero source location identity.", nameof(source));

            FactoryKind = factoryKind;
            Source = source;
            ServiceName = serviceName;
        }

        public ServiceFactoryKind FactoryKind { get; }

        public SourceLocationId Source { get; }

        public string? ServiceName { get; }
    }

    public sealed class ServiceContractRef
    {
        public ServiceContractRef(ServiceContractIR contract)
        {
            if (contract == null)
                throw new ArgumentNullException(nameof(contract));

            ContractName = contract.ContractName;
            Source = contract.Source;
        }

        public string ContractName { get; }

        public SourceLocationId Source { get; }
    }

    public sealed class ScopeGraphPlan
    {
        readonly ScopeIR[] scopes;
        readonly ValueInitPlanIR[] valueInitPlans;

        public ScopeGraphPlan(VerifiedArtifactHeader header, ReadOnlySpan<ScopeIR> scopes)
            : this(header, scopes, ReadOnlySpan<ValueInitPlanIR>.Empty)
        {
        }

        public ScopeGraphPlan(VerifiedArtifactHeader header, ReadOnlySpan<ScopeIR> scopes, ReadOnlySpan<ValueInitPlanIR> valueInitPlans)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            KernelProjectionArtifactKindValidator.ValidateArtifactKind(header, ArtifactKind.ScopeGraph);
            this.scopes = KernelProjectionArrayHelpers.CloneAndSort(scopes, static (left, right) => left.PlanId.Value.CompareTo(right.PlanId.Value));
            this.valueInitPlans = KernelProjectionArrayHelpers.CloneAndSort(valueInitPlans, static (left, right) => left.PlanId.Value.CompareTo(right.PlanId.Value));
            ContentHash = KernelProjectionHashing.ComputeScopeGraphHash(this.scopes, this.valueInitPlans);
            KernelProjectionHashing.ValidateHeaderHash(header, ContentHash);
        }

        public VerifiedArtifactHeader Header { get; }

        public ReadOnlySpan<ScopeIR> Scopes => scopes;

        public ReadOnlySpan<ValueInitPlanIR> ValueInitPlans => valueInitPlans;

        public Hash128 ContentHash { get; }
    }

    public sealed class LifecyclePlan
    {
        readonly LifecycleIR[] lifecycles;
        readonly LifecycleDispatchTable dispatchTable;

        public LifecyclePlan(VerifiedArtifactHeader header, ReadOnlySpan<LifecycleIR> lifecycles)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            KernelProjectionArtifactKindValidator.ValidateArtifactKind(header, ArtifactKind.LifecyclePlan);
            this.lifecycles = KernelProjectionArrayHelpers.CloneAndSort(lifecycles, static (left, right) => left.PlanId.Value.CompareTo(right.PlanId.Value));
            dispatchTable = new LifecycleDispatchTable(this.lifecycles);
            ContentHash = KernelProjectionHashing.ComputeLifecyclePlanHash(this.lifecycles);
            KernelProjectionHashing.ValidateHeaderHash(header, ContentHash);
        }

        public VerifiedArtifactHeader Header { get; }

        public ReadOnlySpan<LifecycleIR> Lifecycles => lifecycles;

        public LifecycleDispatchTable DispatchTable => dispatchTable;

        public Hash128 ContentHash { get; }
    }

    public sealed class LifecycleDispatchTable
    {
        readonly LifecycleDispatchStep[] allSteps;
        readonly LifecycleDispatchStep[] bootSteps;
        readonly LifecycleDispatchStep[] createSteps;
        readonly LifecycleDispatchStep[] buildSteps;
        readonly LifecycleDispatchStep[] acquireSteps;
        readonly LifecycleDispatchStep[] activateSteps;
        readonly LifecycleDispatchStep[] tickSteps;
        readonly LifecycleDispatchStep[] fixedTickSteps;
        readonly LifecycleDispatchStep[] lateTickSteps;
        readonly LifecycleDispatchStep[] preReleaseSteps;
        readonly LifecycleDispatchStep[] releaseSteps;
        readonly LifecycleDispatchStep[] resetSteps;
        readonly LifecycleDispatchStep[] destroySteps;
        readonly LifecycleDispatchStep[] disposeSteps;

        public LifecycleDispatchTable(ReadOnlySpan<LifecycleIR> lifecycles)
        {
            List<LifecycleDispatchStep> bootStepList = new List<LifecycleDispatchStep>();
            List<LifecycleDispatchStep> createStepList = new List<LifecycleDispatchStep>();
            List<LifecycleDispatchStep> buildStepList = new List<LifecycleDispatchStep>();
            List<LifecycleDispatchStep> acquireStepList = new List<LifecycleDispatchStep>();
            List<LifecycleDispatchStep> activateStepList = new List<LifecycleDispatchStep>();
            List<LifecycleDispatchStep> tickStepList = new List<LifecycleDispatchStep>();
            List<LifecycleDispatchStep> fixedTickStepList = new List<LifecycleDispatchStep>();
            List<LifecycleDispatchStep> lateTickStepList = new List<LifecycleDispatchStep>();
            List<LifecycleDispatchStep> preReleaseStepList = new List<LifecycleDispatchStep>();
            List<LifecycleDispatchStep> releaseStepList = new List<LifecycleDispatchStep>();
            List<LifecycleDispatchStep> resetStepList = new List<LifecycleDispatchStep>();
            List<LifecycleDispatchStep> destroyStepList = new List<LifecycleDispatchStep>();
            List<LifecycleDispatchStep> disposeStepList = new List<LifecycleDispatchStep>();

            for (int lifecycleIndex = 0; lifecycleIndex < lifecycles.Length; lifecycleIndex++)
            {
                LifecycleIR lifecycle = lifecycles[lifecycleIndex];
                LifecycleStepIR[] steps = KernelProjectionArrayHelpers.CloneAndSort(lifecycle.Steps, static (left, right) =>
                {
                    int orderComparison = left.Order.CompareTo(right.Order);
                    if (orderComparison != 0)
                        return orderComparison;

                    return left.Id.Value.CompareTo(right.Id.Value);
                });

                for (int stepIndex = 0; stepIndex < steps.Length; stepIndex++)
                {
                    LifecycleDispatchStep dispatchStep = new LifecycleDispatchStep(lifecycle, steps[stepIndex]);
                    ValidateStepPhase(dispatchStep.Phase, dispatchStep.StepId);

                    switch (dispatchStep.Phase)
                    {
                        case LifecyclePhase.Boot:
                            bootStepList.Add(dispatchStep);
                            break;
                        case LifecyclePhase.Create:
                            createStepList.Add(dispatchStep);
                            break;
                        case LifecyclePhase.Build:
                            buildStepList.Add(dispatchStep);
                            break;
                        case LifecyclePhase.Acquire:
                            acquireStepList.Add(dispatchStep);
                            break;
                        case LifecyclePhase.Activate:
                            activateStepList.Add(dispatchStep);
                            break;
                        case LifecyclePhase.Tick:
                            tickStepList.Add(dispatchStep);
                            break;
                        case LifecyclePhase.FixedTick:
                            fixedTickStepList.Add(dispatchStep);
                            break;
                        case LifecyclePhase.LateTick:
                            lateTickStepList.Add(dispatchStep);
                            break;
                        case LifecyclePhase.PreRelease:
                            preReleaseStepList.Add(dispatchStep);
                            break;
                        case LifecyclePhase.Release:
                            releaseStepList.Add(dispatchStep);
                            break;
                        case LifecyclePhase.Reset:
                            resetStepList.Add(dispatchStep);
                            break;
                        case LifecyclePhase.Destroy:
                            destroyStepList.Add(dispatchStep);
                            break;
                        case LifecyclePhase.Dispose:
                            disposeStepList.Add(dispatchStep);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(dispatchStep), dispatchStep.Phase, "Lifecycle dispatch tables require a defined phase.");
                    }
                }
            }

            bootSteps = bootStepList.ToArray();
            createSteps = createStepList.ToArray();
            buildSteps = buildStepList.ToArray();
            acquireSteps = acquireStepList.ToArray();
            activateSteps = activateStepList.ToArray();
            tickSteps = tickStepList.ToArray();
            fixedTickSteps = fixedTickStepList.ToArray();
            lateTickSteps = lateTickStepList.ToArray();
            preReleaseSteps = preReleaseStepList.ToArray();
            releaseSteps = releaseStepList.ToArray();
            resetSteps = resetStepList.ToArray();
            destroySteps = destroyStepList.ToArray();
            disposeSteps = disposeStepList.ToArray();
            allSteps = CombineAllSteps(
                bootSteps,
                createSteps,
                buildSteps,
                acquireSteps,
                activateSteps,
                tickSteps,
                fixedTickSteps,
                lateTickSteps,
                preReleaseSteps,
                releaseSteps,
                resetSteps,
                destroySteps,
                disposeSteps);
        }

        public ReadOnlySpan<LifecycleDispatchStep> AllSteps => allSteps;

        public ReadOnlySpan<LifecycleDispatchStep> BootSteps => bootSteps;

        public ReadOnlySpan<LifecycleDispatchStep> CreateSteps => createSteps;

        public ReadOnlySpan<LifecycleDispatchStep> BuildSteps => buildSteps;

        public ReadOnlySpan<LifecycleDispatchStep> AcquireSteps => acquireSteps;

        public ReadOnlySpan<LifecycleDispatchStep> ActivateSteps => activateSteps;

        public ReadOnlySpan<LifecycleDispatchStep> TickSteps => tickSteps;

        public ReadOnlySpan<LifecycleDispatchStep> FixedTickSteps => fixedTickSteps;

        public ReadOnlySpan<LifecycleDispatchStep> LateTickSteps => lateTickSteps;

        public ReadOnlySpan<LifecycleDispatchStep> PreReleaseSteps => preReleaseSteps;

        public ReadOnlySpan<LifecycleDispatchStep> ReleaseSteps => releaseSteps;

        public ReadOnlySpan<LifecycleDispatchStep> ResetSteps => resetSteps;

        public ReadOnlySpan<LifecycleDispatchStep> DestroySteps => destroySteps;

        public ReadOnlySpan<LifecycleDispatchStep> DisposeSteps => disposeSteps;

        public ReadOnlySpan<LifecycleDispatchStep> GetSteps(LifecyclePhase phase)
        {
            switch (phase)
            {
                case LifecyclePhase.Boot:
                    return BootSteps;
                case LifecyclePhase.Create:
                    return CreateSteps;
                case LifecyclePhase.Build:
                    return BuildSteps;
                case LifecyclePhase.Acquire:
                    return AcquireSteps;
                case LifecyclePhase.Activate:
                    return ActivateSteps;
                case LifecyclePhase.Tick:
                    return TickSteps;
                case LifecyclePhase.FixedTick:
                    return FixedTickSteps;
                case LifecyclePhase.LateTick:
                    return LateTickSteps;
                case LifecyclePhase.PreRelease:
                    return PreReleaseSteps;
                case LifecyclePhase.Release:
                    return ReleaseSteps;
                case LifecyclePhase.Reset:
                    return ResetSteps;
                case LifecyclePhase.Destroy:
                    return DestroySteps;
                case LifecyclePhase.Dispose:
                    return DisposeSteps;
                default:
                    throw new ArgumentOutOfRangeException(nameof(phase), phase, "Lifecycle dispatch tables require a defined phase.");
            }
        }

        static void ValidateStepPhase(LifecyclePhase phase, LifecycleStepId stepId)
        {
            switch (phase)
            {
                case LifecyclePhase.Boot:
                case LifecyclePhase.Create:
                case LifecyclePhase.Build:
                case LifecyclePhase.Acquire:
                case LifecyclePhase.Activate:
                case LifecyclePhase.Tick:
                case LifecyclePhase.FixedTick:
                case LifecyclePhase.LateTick:
                case LifecyclePhase.PreRelease:
                case LifecyclePhase.Release:
                case LifecyclePhase.Reset:
                case LifecyclePhase.Destroy:
                case LifecyclePhase.Dispose:
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stepId), stepId, "Lifecycle steps must declare a defined phase.");
            }
        }

        static LifecycleDispatchStep[] CombineAllSteps(
            ReadOnlySpan<LifecycleDispatchStep> bootSteps,
            ReadOnlySpan<LifecycleDispatchStep> createSteps,
            ReadOnlySpan<LifecycleDispatchStep> buildSteps,
            ReadOnlySpan<LifecycleDispatchStep> acquireSteps,
            ReadOnlySpan<LifecycleDispatchStep> activateSteps,
            ReadOnlySpan<LifecycleDispatchStep> tickSteps,
            ReadOnlySpan<LifecycleDispatchStep> fixedTickSteps,
            ReadOnlySpan<LifecycleDispatchStep> lateTickSteps,
            ReadOnlySpan<LifecycleDispatchStep> preReleaseSteps,
            ReadOnlySpan<LifecycleDispatchStep> releaseSteps,
            ReadOnlySpan<LifecycleDispatchStep> resetSteps,
            ReadOnlySpan<LifecycleDispatchStep> destroySteps,
            ReadOnlySpan<LifecycleDispatchStep> disposeSteps)
        {
            LifecycleDispatchStep[] combined = new LifecycleDispatchStep[
                bootSteps.Length +
                createSteps.Length +
                buildSteps.Length +
                acquireSteps.Length +
                activateSteps.Length +
                tickSteps.Length +
                fixedTickSteps.Length +
                lateTickSteps.Length +
                preReleaseSteps.Length +
                releaseSteps.Length +
                resetSteps.Length +
                destroySteps.Length +
                disposeSteps.Length];

            int index = 0;
            index = CopySteps(bootSteps, combined, index);
            index = CopySteps(createSteps, combined, index);
            index = CopySteps(buildSteps, combined, index);
            index = CopySteps(acquireSteps, combined, index);
            index = CopySteps(activateSteps, combined, index);
            index = CopySteps(tickSteps, combined, index);
            index = CopySteps(fixedTickSteps, combined, index);
            index = CopySteps(lateTickSteps, combined, index);
            index = CopySteps(preReleaseSteps, combined, index);
            index = CopySteps(releaseSteps, combined, index);
            index = CopySteps(resetSteps, combined, index);
            index = CopySteps(destroySteps, combined, index);
            CopySteps(disposeSteps, combined, index);
            return combined;
        }

        static int CopySteps(ReadOnlySpan<LifecycleDispatchStep> source, LifecycleDispatchStep[] destination, int index)
        {
            for (int sourceIndex = 0; sourceIndex < source.Length; sourceIndex++)
            {
                destination[index++] = source[sourceIndex];
            }

            return index;
        }
    }

    public readonly struct LifecycleDispatchStep
    {
        public LifecycleDispatchStep(LifecycleIR lifecycle, LifecycleStepIR step)
        {
            Lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            Step = step ?? throw new ArgumentNullException(nameof(step));
            ValidateTargetKind(Step.Target.Kind, Step.Id);
        }

        public LifecycleIR Lifecycle { get; }

        public LifecycleStepIR Step { get; }

        public LifecyclePlanId LifecyclePlanId => Lifecycle.PlanId;

        public string LifecycleName => Lifecycle.Name;

        public ModuleId OwnerModule => Lifecycle.OwnerModule;

        public LifecycleFailurePolicy FailurePolicy => Lifecycle.FailurePolicy;

        public bool FailurePolicyIsExplicit => Lifecycle.FailurePolicyIsExplicit;

        public KernelProfileMask FailurePolicyJustificationProfiles => Lifecycle.FailurePolicyJustificationProfiles;

        public string? FailurePolicyJustification => Lifecycle.FailurePolicyJustification;

        public LifecycleAcquireRollbackPolicy AcquireRollbackPolicy => Lifecycle.AcquireRollbackPolicy;

        public LifecycleTickCardinalityKind TickCardinality => Step.TickCardinality;

        public LifecycleExecutionModeKind ExecutionMode => Step.ExecutionMode;

        public LifecycleAsyncPolicyIR? AsyncPolicy => Step.AsyncPolicy;

        public LifecycleStepId StepId => Step.Id;

        public LifecyclePhase Phase => Step.Phase;

        public int Order => Step.Order;

        public LifecycleTargetRefIR Target => Step.Target;

        public LifecycleActionKind Action => Step.Action;

        public SourceLocationId Source => Step.Source;

        public RuntimeIdentityRef LifecycleIdentity => new RuntimeIdentityRef(RuntimeIdentityKind.LifecyclePlan, LifecyclePlanId.Value);

        public RuntimeIdentityRef StepIdentity => new RuntimeIdentityRef(RuntimeIdentityKind.LifecycleStep, StepId.Value);

        public bool TryGetTargetIdentity(out RuntimeIdentityRef targetIdentity)
        {
            switch (Target.Kind)
            {
                case LifecycleTargetKind.Service:
                    targetIdentity = new RuntimeIdentityRef(RuntimeIdentityKind.Service, Target.TargetService.Value);
                    return true;
                case LifecycleTargetKind.Scope:
                    targetIdentity = new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, Target.TargetScope.Value);
                    return true;
                case LifecycleTargetKind.RuntimeQuery:
                    targetIdentity = new RuntimeIdentityRef(RuntimeIdentityKind.RuntimeQuery, Target.TargetRuntimeQuery.Value);
                    return true;
                default:
                    throw new ArgumentOutOfRangeException(nameof(Target), Target.Kind, "Lifecycle dispatch targets must use a supported closed-world target kind.");
            }
        }

        static void ValidateTargetKind(LifecycleTargetKind kind, LifecycleStepId stepId)
        {
            switch (kind)
            {
                case LifecycleTargetKind.Service:
                case LifecycleTargetKind.Scope:
                case LifecycleTargetKind.RuntimeQuery:
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stepId), stepId, "Lifecycle dispatch tables only support service, scope, and runtime-query targets.");
            }
        }
    }

    public sealed class CommandExecutorRef
    {
        public CommandExecutorRef(CommandExecutorId id, SourceLocationId source)
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

    public sealed class CommandPayloadSchemaPlan
    {
        readonly CommandPayloadFieldPlan[] fields;

        public CommandPayloadSchemaPlan(CommandPayloadSchemaId schemaId, CommandTypeId commandTypeId, SourceLocationId source)
            : this(schemaId, commandTypeId, source, CommandPayloadUnknownFieldPolicyIR.Reject, ReadOnlySpan<CommandPayloadFieldIR>.Empty)
        {
        }

        public CommandPayloadSchemaPlan(CommandPayloadSchemaId schemaId, CommandTypeId commandTypeId, SourceLocationId source, CommandPayloadUnknownFieldPolicyIR unknownFieldPolicy, ReadOnlySpan<CommandPayloadFieldIR> fields)
        {
            if (schemaId.Value == 0)
                throw new ArgumentException("Command payload schema plans must provide a non-zero payload schema identity.", nameof(schemaId));

            if (commandTypeId.Value == 0)
                throw new ArgumentException("Command payload schema plans must provide a non-zero command type identity.", nameof(commandTypeId));

            if (source.Value == 0)
                throw new ArgumentException("Command payload schema plans must provide a non-zero source location identity.", nameof(source));

            SchemaId = schemaId;
            CommandTypeId = commandTypeId;
            Source = source;
            UnknownFieldPolicy = unknownFieldPolicy;
            this.fields = CreateFieldPlans(fields);
            EnsureUniqueFieldPaths(this.fields);
        }

        public CommandPayloadSchemaId SchemaId { get; }

        public CommandTypeId CommandTypeId { get; }

        public SourceLocationId Source { get; }

        public CommandPayloadUnknownFieldPolicyIR UnknownFieldPolicy { get; }

        public ReadOnlySpan<CommandPayloadFieldPlan> Fields => fields;

        static CommandPayloadFieldPlan[] CreateFieldPlans(ReadOnlySpan<CommandPayloadFieldIR> source)
        {
            if (source.Length == 0)
                return Array.Empty<CommandPayloadFieldPlan>();

            var plans = new CommandPayloadFieldPlan[source.Length];
            for (int index = 0; index < source.Length; index++)
                plans[index] = new CommandPayloadFieldPlan(source[index]);

            Array.Sort(plans, static (left, right) => StringComparer.Ordinal.Compare(left.FieldPath, right.FieldPath));
            return plans;
        }

        static void EnsureUniqueFieldPaths(CommandPayloadFieldPlan[] fields)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < fields.Length; index++)
            {
                if (!seen.Add(fields[index].FieldPath))
                    throw new ArgumentException("Command payload schema plans require unique field paths.", nameof(fields));
            }
        }
    }

    public sealed class CommandPayloadFieldPlan
    {
        public CommandPayloadFieldPlan(CommandPayloadFieldIR field)
        {
            if (field == null)
                throw new ArgumentNullException(nameof(field));

            FieldPath = field.FieldPath;
            Kind = field.Kind;
            Requirement = field.Requirement;
            ReferenceKind = field.ReferenceKind;
            AllowNull = field.AllowNull;
            Source = field.Source;
        }

        public string FieldPath { get; }

        public CommandPayloadFieldKindIR Kind { get; }

        public CommandPayloadFieldRequirementIR Requirement { get; }

        public CommandPayloadReferenceKindIR ReferenceKind { get; }

        public bool AllowNull { get; }

        public SourceLocationId Source { get; }
    }

    public sealed class CommandEntryPlan
    {
        readonly CommandDependencyIR[] dependencies;

        public CommandEntryPlan(CommandIR command)
            : this(
                command.TypeId,
                command.RuntimeName,
                command.AuthoringKey,
                command.CategoryId,
                command.OwnerModule,
                new CommandExecutorRef(command.Executor.Id, command.Executor.Source),
                new CommandPayloadSchemaPlan(command.PayloadSchema.Id, command.TypeId, command.PayloadSchema.Source, command.PayloadSchema.UnknownFieldPolicy, command.PayloadSchema.Fields),
                command.Dependencies,
                command.Source)
        {
        }

        public CommandEntryPlan(CommandTypeId typeId, string runtimeName, CommandAuthoringKeyRefIR authoringKey, CommandCategoryId categoryId, ModuleId ownerModule, CommandExecutorRef executor, CommandPayloadSchemaPlan payloadSchema, ReadOnlySpan<CommandDependencyIR> dependencies, SourceLocationId source)
        {
            if (typeId.Value == 0)
                throw new ArgumentException("Command entry plans must provide a non-zero command type identity.", nameof(typeId));

            if (string.IsNullOrWhiteSpace(runtimeName))
                throw new ArgumentException("Command entry plans must provide a runtime name.", nameof(runtimeName));

            if (categoryId.Value == 0)
                throw new ArgumentException("Command entry plans must provide a non-zero command category identity.", nameof(categoryId));

            if (ownerModule.Value == 0)
                throw new ArgumentException("Command entry plans must provide a non-zero owner module identity.", nameof(ownerModule));

            if (source.Value == 0)
                throw new ArgumentException("Command entry plans must provide a non-zero source location identity.", nameof(source));

            TypeId = typeId;
            RuntimeName = runtimeName;
            AuthoringKey = authoringKey ?? throw new ArgumentNullException(nameof(authoringKey));
            CategoryId = categoryId;
            OwnerModule = ownerModule;
            Executor = executor ?? throw new ArgumentNullException(nameof(executor));
            PayloadSchema = payloadSchema ?? throw new ArgumentNullException(nameof(payloadSchema));
            this.dependencies = KernelProjectionArrayHelpers.CloneAndSort(dependencies, CompareCommandDependency);
            Source = source;
        }

        public CommandTypeId TypeId { get; }

        public string RuntimeName { get; }

        public CommandAuthoringKeyRefIR AuthoringKey { get; }

        public CommandCategoryId CategoryId { get; }

        public ModuleId OwnerModule { get; }

        public CommandExecutorRef Executor { get; }

        public CommandPayloadSchemaPlan PayloadSchema { get; }

        public ReadOnlySpan<CommandDependencyIR> Dependencies => dependencies;

        public SourceLocationId Source { get; }

        static int CompareCommandDependency(CommandDependencyIR left, CommandDependencyIR right)
        {
            int comparison = StringComparer.Ordinal.Compare(left.Target.ToString(), right.Target.ToString());
            if (comparison != 0)
                return comparison;

            comparison = left.Strength.CompareTo(right.Strength);
            if (comparison != 0)
                return comparison;

            return left.Source.Value.CompareTo(right.Source.Value);
        }
    }

    public sealed class CommandModuleMetadata
    {
        readonly CommandTypeId[] commandTypeIds;
        readonly CommandCategoryId[] categoryIds;

        public CommandModuleMetadata(ModuleId moduleId, CommandTypeId representativeCommandTypeId, ReadOnlySpan<CommandTypeId> commandTypeIds, ReadOnlySpan<CommandCategoryId> categoryIds, SourceLocationId source)
        {
            if (moduleId.Value == 0)
                throw new ArgumentException("Command module metadata must provide a non-zero module identity.", nameof(moduleId));

            if (representativeCommandTypeId.Value == 0)
                throw new ArgumentException("Command module metadata must provide a non-zero representative command type identity.", nameof(representativeCommandTypeId));

            if (source.Value == 0)
                throw new ArgumentException("Command module metadata must provide a non-zero source location identity.", nameof(source));

            if (commandTypeIds.Length == 0)
                throw new ArgumentException("Command module metadata must provide at least one command type identity.", nameof(commandTypeIds));

            if (categoryIds.Length == 0)
                throw new ArgumentException("Command module metadata must provide at least one command category identity.", nameof(categoryIds));

            ModuleId = moduleId;
            RepresentativeCommandTypeId = representativeCommandTypeId;
            this.commandTypeIds = CopyIds(commandTypeIds);
            this.categoryIds = CopyIds(categoryIds);
            Source = source;
        }

        public ModuleId ModuleId { get; }

        public CommandTypeId RepresentativeCommandTypeId { get; }

        public ReadOnlySpan<CommandTypeId> CommandTypeIds => commandTypeIds;

        public ReadOnlySpan<CommandCategoryId> CategoryIds => categoryIds;

        public SourceLocationId Source { get; }

        public int CommandCount => commandTypeIds.Length;

        static T[] CopyIds<T>(ReadOnlySpan<T> source) where T : struct
        {
            T[] copy = new T[source.Length];
            for (int index = 0; index < source.Length; index++)
                copy[index] = source[index];

            return copy;
        }
    }

    public sealed class CommandCategoryMetadata
    {
        readonly ModuleId[] ownerModules;
        readonly CommandTypeId[] commandTypeIds;

        public CommandCategoryMetadata(CommandCategoryId categoryId, CommandTypeId representativeCommandTypeId, ReadOnlySpan<ModuleId> ownerModules, ReadOnlySpan<CommandTypeId> commandTypeIds, SourceLocationId source)
        {
            if (categoryId.Value == 0)
                throw new ArgumentException("Command category metadata must provide a non-zero category identity.", nameof(categoryId));

            if (representativeCommandTypeId.Value == 0)
                throw new ArgumentException("Command category metadata must provide a non-zero representative command type identity.", nameof(representativeCommandTypeId));

            if (source.Value == 0)
                throw new ArgumentException("Command category metadata must provide a non-zero source location identity.", nameof(source));

            if (ownerModules.Length == 0)
                throw new ArgumentException("Command category metadata must provide at least one owner module identity.", nameof(ownerModules));

            if (commandTypeIds.Length == 0)
                throw new ArgumentException("Command category metadata must provide at least one command type identity.", nameof(commandTypeIds));

            CategoryId = categoryId;
            RepresentativeCommandTypeId = representativeCommandTypeId;
            this.ownerModules = CopyIds(ownerModules);
            this.commandTypeIds = CopyIds(commandTypeIds);
            Source = source;
        }

        public CommandCategoryId CategoryId { get; }

        public CommandTypeId RepresentativeCommandTypeId { get; }

        public ReadOnlySpan<ModuleId> OwnerModules => ownerModules;

        public ReadOnlySpan<CommandTypeId> CommandTypeIds => commandTypeIds;

        public SourceLocationId Source { get; }

        public int CommandCount => commandTypeIds.Length;

        static T[] CopyIds<T>(ReadOnlySpan<T> source) where T : struct
        {
            T[] copy = new T[source.Length];
            for (int index = 0; index < source.Length; index++)
                copy[index] = source[index];

            return copy;
        }
    }

    public sealed class CommandCatalogPlan
    {
        readonly CommandIR[] commands;
        readonly CommandEntryPlan[] entries;
        readonly CommandModuleMetadata[] modules;
        readonly CommandCategoryMetadata[] categories;

        public CommandCatalogPlan(VerifiedArtifactHeader header, ReadOnlySpan<CommandIR> commands)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            KernelProjectionArtifactKindValidator.ValidateArtifactKind(header, ArtifactKind.CommandCatalog);
            BuildProjection(commands, out this.commands, out entries, out modules, out categories);
            this.entries = entries;
            this.modules = modules;
            this.categories = categories;
            ContentHash = KernelProjectionHashing.ComputeCommandCatalogHash(this.entries, this.modules, this.categories);
            KernelProjectionHashing.ValidateHeaderHash(header, ContentHash);
        }

        public VerifiedArtifactHeader Header { get; }

        public ReadOnlySpan<CommandIR> Commands => commands;

        public ReadOnlySpan<CommandEntryPlan> Entries => entries;

        public ReadOnlySpan<CommandModuleMetadata> Modules => modules;

        public ReadOnlySpan<CommandCategoryMetadata> Categories => categories;

        public Hash128 ContentHash { get; }

        internal static void BuildProjection(ReadOnlySpan<CommandIR> commands, out CommandIR[] normalizedCommands, out CommandEntryPlan[] entries, out CommandModuleMetadata[] modules, out CommandCategoryMetadata[] categories)
        {
            normalizedCommands = KernelProjectionArrayHelpers.CloneAndSort(commands, static (left, right) => left.TypeId.Value.CompareTo(right.TypeId.Value));

            for (int index = 1; index < normalizedCommands.Length; index++)
            {
                if (normalizedCommands[index - 1].TypeId == normalizedCommands[index].TypeId)
                    throw new ArgumentException("CommandCatalogPlan requires unique CommandTypeId values.", nameof(commands));
            }

            entries = new CommandEntryPlan[normalizedCommands.Length];
            for (int index = 0; index < normalizedCommands.Length; index++)
                entries[index] = new CommandEntryPlan(normalizedCommands[index]);

            modules = BuildModuleMetadata(normalizedCommands);
            categories = BuildCategoryMetadata(normalizedCommands);
        }

        static CommandModuleMetadata[] BuildModuleMetadata(ReadOnlySpan<CommandIR> commands)
        {
            if (commands.Length == 0)
                return Array.Empty<CommandModuleMetadata>();

            List<int> moduleIds = new List<int>();
            for (int index = 0; index < commands.Length; index++)
                AddUnique(moduleIds, commands[index].OwnerModule.Value);

            moduleIds.Sort();

            CommandModuleMetadata[] metadata = new CommandModuleMetadata[moduleIds.Count];
            for (int moduleIndex = 0; moduleIndex < moduleIds.Count; moduleIndex++)
            {
                int moduleId = moduleIds[moduleIndex];
                List<CommandTypeId> commandTypeIds = new List<CommandTypeId>();
                List<CommandCategoryId> categoryIds = new List<CommandCategoryId>();
                SourceLocationId source = default;
                bool hasSource = false;

                for (int commandIndex = 0; commandIndex < commands.Length; commandIndex++)
                {
                    CommandIR command = commands[commandIndex];
                    if (command.OwnerModule.Value != moduleId)
                        continue;

                    commandTypeIds.Add(command.TypeId);
                    AddUnique(categoryIds, command.CategoryId);
                    if (!hasSource)
                    {
                        source = command.Source;
                        hasSource = true;
                    }
                }

                commandTypeIds.Sort(static (left, right) => left.Value.CompareTo(right.Value));
                categoryIds.Sort(static (left, right) => left.Value.CompareTo(right.Value));
                metadata[moduleIndex] = new CommandModuleMetadata(new ModuleId(moduleId), commandTypeIds[0], commandTypeIds.ToArray(), categoryIds.ToArray(), source);
            }

            return metadata;
        }

        static CommandCategoryMetadata[] BuildCategoryMetadata(ReadOnlySpan<CommandIR> commands)
        {
            if (commands.Length == 0)
                return Array.Empty<CommandCategoryMetadata>();

            List<int> categoryIds = new List<int>();
            for (int index = 0; index < commands.Length; index++)
                AddUnique(categoryIds, commands[index].CategoryId.Value);

            categoryIds.Sort();

            CommandCategoryMetadata[] metadata = new CommandCategoryMetadata[categoryIds.Count];
            for (int categoryIndex = 0; categoryIndex < categoryIds.Count; categoryIndex++)
            {
                int categoryId = categoryIds[categoryIndex];
                List<ModuleId> ownerModules = new List<ModuleId>();
                List<CommandTypeId> commandTypeIds = new List<CommandTypeId>();
                SourceLocationId source = default;
                bool hasSource = false;

                for (int commandIndex = 0; commandIndex < commands.Length; commandIndex++)
                {
                    CommandIR command = commands[commandIndex];
                    if (command.CategoryId.Value != categoryId)
                        continue;

                    commandTypeIds.Add(command.TypeId);
                    AddUnique(ownerModules, command.OwnerModule);
                    if (!hasSource)
                    {
                        source = command.Source;
                        hasSource = true;
                    }
                }

                ownerModules.Sort(static (left, right) => left.Value.CompareTo(right.Value));
                commandTypeIds.Sort(static (left, right) => left.Value.CompareTo(right.Value));
                metadata[categoryIndex] = new CommandCategoryMetadata(new CommandCategoryId(categoryId), commandTypeIds[0], ownerModules.ToArray(), commandTypeIds.ToArray(), source);
            }

            return metadata;
        }

        static void AddUnique<T>(List<T> values, T value)
        {
            for (int index = 0; index < values.Count; index++)
            {
                if (EqualityComparer<T>.Default.Equals(values[index], value))
                    return;
            }

            values.Add(value);
        }
    }

    public sealed class ValueSchemaPlan
    {
        readonly ValueKeyIR[] valueKeys;

        public ValueSchemaPlan(VerifiedArtifactHeader header, ReadOnlySpan<ValueKeyIR> valueKeys)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            KernelProjectionArtifactKindValidator.ValidateArtifactKind(header, ArtifactKind.ValueSchema);
            this.valueKeys = KernelProjectionArrayHelpers.CloneAndSort(valueKeys, static (left, right) => left.Id.Value.CompareTo(right.Id.Value));
            ContentHash = KernelProjectionHashing.ComputeValueSchemaHash(this.valueKeys);
            KernelProjectionHashing.ValidateHeaderHash(header, ContentHash);
        }

        public VerifiedArtifactHeader Header { get; }

        public ReadOnlySpan<ValueKeyIR> ValueKeys => valueKeys;

        public Hash128 ContentHash { get; }
    }

    public sealed class RuntimeQueryPlan
    {
        readonly RuntimeQueryIR[] runtimeQueries;

        public RuntimeQueryPlan(VerifiedArtifactHeader header, ReadOnlySpan<RuntimeQueryIR> runtimeQueries)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            KernelProjectionArtifactKindValidator.ValidateArtifactKind(header, ArtifactKind.RuntimeQuery);
            this.runtimeQueries = KernelProjectionArrayHelpers.CloneAndSort(runtimeQueries, static (left, right) => left.Id.Value.CompareTo(right.Id.Value));
            ContentHash = KernelProjectionHashing.ComputeRuntimeQueryHash(this.runtimeQueries);
            KernelProjectionHashing.ValidateHeaderHash(header, ContentHash);
        }

        public VerifiedArtifactHeader Header { get; }

        public ReadOnlySpan<RuntimeQueryIR> RuntimeQueries => runtimeQueries;

        public Hash128 ContentHash { get; }
    }

    public sealed class KernelDebugMap
    {
        readonly KernelDebugMapEntry[] entries;

        public KernelDebugMap(VerifiedArtifactHeader header, ReadOnlySpan<KernelDebugMapEntry> entries)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            KernelProjectionArtifactKindValidator.ValidateArtifactKind(header, ArtifactKind.KernelDebugMap);
            this.entries = KernelProjectionArrayHelpers.CloneAndSort(entries, KernelDebugMapEntryComparer.Instance);
            ContentHash = KernelProjectionHashing.ComputeDebugMapHash(this.entries);
            KernelProjectionHashing.ValidateHeaderHash(header, ContentHash);
        }

        public VerifiedArtifactHeader Header { get; }

        public ReadOnlySpan<KernelDebugMapEntry> Entries => entries;

        public Hash128 ContentHash { get; }

        public bool TryGetSourceLocation(RuntimeIdentityRef identity, out SourceLocationRef sourceLocation)
        {
            if (identity.IsEmpty)
                throw new ArgumentException("Debug map lookups require a fully specified identity.", nameof(identity));

            for (int index = 0; index < entries.Length; index++)
            {
                KernelDebugMapEntry entry = entries[index];
                if (entry.Identity != identity)
                    continue;

                sourceLocation = new SourceLocationRef(entry.Source.Value);
                return true;
            }

            sourceLocation = default;
            return false;
        }
    }

    public sealed class GenerationReport
    {
        public GenerationReport(
            VerifiedArtifactHeader header,
            string selectedProfile,
            KernelProfileMask selectedProfileMask,
            int artifactCount,
            int mappingCount,
            int debugMapEntryCount,
            ValidationResultStatus validationStatus,
            Hash128 contentHash)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            KernelProjectionArtifactKindValidator.ValidateArtifactKind(header, ArtifactKind.GenerationReport);
            if (string.IsNullOrWhiteSpace(selectedProfile))
                throw new ArgumentException("Generation reports must provide a selected profile.", nameof(selectedProfile));

            SelectedProfile = selectedProfile;
            SelectedProfileMask = selectedProfileMask;
            ArtifactCount = artifactCount;
            MappingCount = mappingCount;
            DebugMapEntryCount = debugMapEntryCount;
            ValidationStatus = validationStatus;
            ContentHash = contentHash;
            KernelProjectionHashing.ValidateHeaderHash(header, contentHash);
        }

        public VerifiedArtifactHeader Header { get; }

        public string SelectedProfile { get; }

        public KernelProfileMask SelectedProfileMask { get; }

        public int ArtifactCount { get; }

        public int MappingCount { get; }

        public int DebugMapEntryCount { get; }

        public ValidationResultStatus ValidationStatus { get; }

        public Hash128 ContentHash { get; }
    }

    public sealed class ValidationReport
    {
        public ValidationReport(VerifiedArtifactHeader header, ProjectionValidationReport report, Hash128 contentHash)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            Report = report ?? throw new ArgumentNullException(nameof(report));
            KernelProjectionArtifactKindValidator.ValidateArtifactKind(header, ArtifactKind.ValidationReport);
            ContentHash = contentHash;
            KernelProjectionHashing.ValidateHeaderHash(header, contentHash);
        }

        public VerifiedArtifactHeader Header { get; }

        public ProjectionValidationReport Report { get; }

        public Hash128 ContentHash { get; }
    }

    public readonly struct KernelDebugMapEntry : IEquatable<KernelDebugMapEntry>
    {
        public KernelDebugMapEntry(
            RuntimeIdentityRef identity,
            string name,
            ModuleId ownerModule,
            SourceLocationId source,
            KernelProfileMask profileMask,
            Hash128 artifactHash,
            string? diagnosticSeedKey = null,
            string? legacyOrigin = null)
        {
            if (identity.IsEmpty)
                throw new ArgumentException("Debug map entries must provide an identity.", nameof(identity));

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Debug map entries must provide a display name.", nameof(name));

            if (ownerModule.Value == 0)
                throw new ArgumentException("Debug map entries must provide a non-zero owner module identity.", nameof(ownerModule));

            if (source.Value == 0)
                throw new ArgumentException("Debug map entries must provide a non-zero source location identity.", nameof(source));

            if (!string.IsNullOrWhiteSpace(legacyOrigin) && legacyOrigin.Trim().Length == 0)
                throw new ArgumentException("Debug map entry legacy origin values must be null or non-empty.", nameof(legacyOrigin));

            if (diagnosticSeedKey != null && diagnosticSeedKey.Trim().Length == 0)
                throw new ArgumentException("Debug map entry diagnostic seed keys must be null or non-empty.", nameof(diagnosticSeedKey));

            if (identity.Kind == RuntimeIdentityKind.DiagnosticSeed && string.IsNullOrWhiteSpace(diagnosticSeedKey))
                throw new ArgumentException("Diagnostic seed debug map entries must provide a diagnostic seed key.", nameof(diagnosticSeedKey));

            if (identity.Kind != RuntimeIdentityKind.DiagnosticSeed && diagnosticSeedKey != null)
                throw new ArgumentException("Only diagnostic seed debug map entries may provide a diagnostic seed key.", nameof(diagnosticSeedKey));

            if (identity.Kind == RuntimeIdentityKind.DiagnosticSeed && legacyOrigin != null)
                throw new ArgumentException("Diagnostic seed debug map entries must not provide a legacy origin.", nameof(legacyOrigin));

            Identity = identity;
            Name = name;
            OwnerModule = ownerModule;
            Source = source;
            ProfileMask = profileMask;
            ArtifactHash = artifactHash;
            DiagnosticSeedKey = diagnosticSeedKey;
            LegacyOrigin = legacyOrigin;
        }

        public RuntimeIdentityRef Identity { get; }

        public string Name { get; }

        public ModuleId OwnerModule { get; }

        public SourceLocationId Source { get; }

        public KernelProfileMask ProfileMask { get; }

        public Hash128 ArtifactHash { get; }

        public string? DiagnosticSeedKey { get; }

        public string? LegacyOrigin { get; }

        public bool Equals(KernelDebugMapEntry other)
        {
            return Identity == other.Identity
                && StringComparer.Ordinal.Equals(Name, other.Name)
                && OwnerModule == other.OwnerModule
                && Source == other.Source
                && ProfileMask == other.ProfileMask
                && ArtifactHash == other.ArtifactHash
                && StringComparer.Ordinal.Equals(DiagnosticSeedKey, other.DiagnosticSeedKey)
                && StringComparer.Ordinal.Equals(LegacyOrigin, other.LegacyOrigin);
        }

        public override bool Equals(object? obj)
        {
            return obj is KernelDebugMapEntry other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Identity.GetHashCode();
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Name);
                hash = (hash * 397) ^ OwnerModule.GetHashCode();
                hash = (hash * 397) ^ Source.GetHashCode();
                hash = (hash * 397) ^ (int)ProfileMask;
                hash = (hash * 397) ^ ArtifactHash.GetHashCode();
                hash = (hash * 397) ^ (DiagnosticSeedKey != null ? StringComparer.Ordinal.GetHashCode(DiagnosticSeedKey) : 0);
                hash = (hash * 397) ^ (LegacyOrigin != null ? StringComparer.Ordinal.GetHashCode(LegacyOrigin) : 0);
                return hash;
            }
        }

        public static bool operator ==(KernelDebugMapEntry left, KernelDebugMapEntry right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(KernelDebugMapEntry left, KernelDebugMapEntry right)
        {
            return !left.Equals(right);
        }
    }

    static class KernelDebugMapEntryComparer
    {
        public static readonly IComparer<KernelDebugMapEntry> Instance = Comparer<KernelDebugMapEntry>.Create(Compare);

        static int Compare(KernelDebugMapEntry left, KernelDebugMapEntry right)
        {
            int comparison = left.Identity.Kind.CompareTo(right.Identity.Kind);
            if (comparison != 0)
                return comparison;

            comparison = left.Identity.Value.CompareTo(right.Identity.Value);
            if (comparison != 0)
                return comparison;

            comparison = left.Identity.Generation.CompareTo(right.Identity.Generation);
            if (comparison != 0)
                return comparison;

            comparison = left.OwnerModule.Value.CompareTo(right.OwnerModule.Value);
            if (comparison != 0)
                return comparison;

            return left.Source.Value.CompareTo(right.Source.Value);
        }
    }

    static class KernelProjectionArrayHelpers
    {
        public static T[] CloneAndSort<T>(ReadOnlySpan<T> source, Comparison<T> comparison) where T : class
        {
            if (source.Length == 0)
                return Array.Empty<T>();

            T[] clone = new T[source.Length];
            for (int index = 0; index < source.Length; index++)
                clone[index] = source[index] ?? throw new ArgumentException("Projection arrays must not contain null items.", nameof(source));

            Array.Sort(clone, comparison);
            return clone;
        }

        public static T[] CloneAndSort<T>(ReadOnlySpan<T> source, IComparer<T> comparer)
        {
            if (source.Length == 0)
                return Array.Empty<T>();

            T[] clone = new T[source.Length];
            for (int index = 0; index < source.Length; index++)
                clone[index] = source[index];

            Array.Sort(clone, comparer);
            return clone;
        }
    }

    static class KernelProjectionHashing
    {
        public static Hash128 ComputeServiceGraphHash(ReadOnlySpan<ServiceIR> services)
        {
            (ServiceIR[] sortedServices, ServiceEntryPlan[] entries, ServiceSlotPlan[] slots) = ServiceGraphPlan.BuildProjection(services);
            return ComputeServiceGraphHash(sortedServices, entries, slots);
        }

        public static Hash128 ComputeServiceGraphHash(ReadOnlySpan<ServiceIR> services, ReadOnlySpan<ServiceEntryPlan> entries, ReadOnlySpan<ServiceSlotPlan> slots)
        {
            List<string> tokens = new List<string>(services.Length * 8 + entries.Length * 8 + slots.Length * 8);
            for (int index = 0; index < services.Length; index++)
                AddServiceTokens(tokens, services[index]);

            for (int index = 0; index < entries.Length; index++)
                AddServiceEntryTokens(tokens, entries[index]);

            for (int index = 0; index < slots.Length; index++)
                AddServiceSlotTokens(tokens, slots[index]);

            return VerifiedArtifactHeaderHashing.ComputeGeneratedHash(tokens);
        }

        public static Hash128 ComputeScopeGraphHash(ReadOnlySpan<ScopeIR> scopes)
        {
            return ComputeScopeGraphHash(scopes, ReadOnlySpan<ValueInitPlanIR>.Empty);
        }

        public static Hash128 ComputeScopeGraphHash(ReadOnlySpan<ScopeIR> scopes, ReadOnlySpan<ValueInitPlanIR> valueInitPlans)
        {
            List<string> tokens = new List<string>(scopes.Length * 4 + valueInitPlans.Length * 8);
            for (int index = 0; index < scopes.Length; index++)
                AddScopeTokens(tokens, scopes[index]);

            for (int index = 0; index < valueInitPlans.Length; index++)
                AddValueInitPlanTokens(tokens, valueInitPlans[index]);

            return VerifiedArtifactHeaderHashing.ComputeGeneratedHash(tokens);
        }

        public static Hash128 ComputeLifecyclePlanHash(ReadOnlySpan<LifecycleIR> lifecycles)
        {
            List<string> tokens = new List<string>(lifecycles.Length * 4);
            for (int index = 0; index < lifecycles.Length; index++)
                AddLifecycleTokens(tokens, lifecycles[index]);

            return VerifiedArtifactHeaderHashing.ComputeGeneratedHash(tokens);
        }

        public static Hash128 ComputeCommandCatalogHash(ReadOnlySpan<CommandIR> commands)
        {
            CommandEntryPlan[] entries;
            CommandModuleMetadata[] modules;
            CommandCategoryMetadata[] categories;
            CommandCatalogPlan.BuildProjection(commands, out _, out entries, out modules, out categories);
            return ComputeCommandCatalogHash(entries, modules, categories);
        }

        public static Hash128 ComputeCommandCatalogHash(ReadOnlySpan<CommandEntryPlan> entries, ReadOnlySpan<CommandModuleMetadata> modules, ReadOnlySpan<CommandCategoryMetadata> categories)
        {
            ValidateCommandCatalogProjection(entries, modules, categories);

            List<string> tokens = new List<string>(entries.Length * 12 + modules.Length * 8 + categories.Length * 8);

            for (int index = 0; index < entries.Length; index++)
                AddCommandEntryTokens(tokens, entries[index]);

            for (int index = 0; index < modules.Length; index++)
                AddCommandModuleTokens(tokens, modules[index]);

            for (int index = 0; index < categories.Length; index++)
                AddCommandCategoryTokens(tokens, categories[index]);

            return VerifiedArtifactHeaderHashing.ComputeGeneratedHash(tokens);
        }

        static void ValidateCommandCatalogProjection(ReadOnlySpan<CommandEntryPlan> entries, ReadOnlySpan<CommandModuleMetadata> modules, ReadOnlySpan<CommandCategoryMetadata> categories)
        {
            for (int index = 1; index < entries.Length; index++)
            {
                if (entries[index - 1].TypeId.Value > entries[index].TypeId.Value)
                    throw new ArgumentException("Command catalog entries must be sorted by command type identity.");

                if (entries[index - 1].TypeId == entries[index].TypeId)
                    throw new ArgumentException("Command catalog entries must use unique command type identities.");
            }

            for (int index = 0; index < modules.Length; index++)
            {
                CommandModuleMetadata module = modules[index];
                if (module.RepresentativeCommandTypeId.Value == 0)
                    throw new ArgumentException("Command module metadata must provide a representative command type identity.");

                ReadOnlySpan<CommandTypeId> commandTypeIds = module.CommandTypeIds;
                if (commandTypeIds.Length == 0)
                    throw new ArgumentException("Command module metadata must provide at least one command type identity.");

                if (commandTypeIds[0] != module.RepresentativeCommandTypeId)
                    throw new ArgumentException("Command module metadata must keep the representative command type as the first command type.");

                for (int commandIndex = 1; commandIndex < commandTypeIds.Length; commandIndex++)
                {
                    if (commandTypeIds[commandIndex - 1].Value > commandTypeIds[commandIndex].Value)
                        throw new ArgumentException("Command module metadata must keep command type identities sorted.");

                    if (commandTypeIds[commandIndex - 1] == commandTypeIds[commandIndex])
                        throw new ArgumentException("Command module metadata must not contain duplicate command type identities.");
                }

                ReadOnlySpan<CommandCategoryId> categoryIds = module.CategoryIds;
                for (int categoryIndex = 1; categoryIndex < categoryIds.Length; categoryIndex++)
                {
                    if (categoryIds[categoryIndex - 1].Value > categoryIds[categoryIndex].Value)
                        throw new ArgumentException("Command module metadata must keep category identities sorted.");

                    if (categoryIds[categoryIndex - 1] == categoryIds[categoryIndex])
                        throw new ArgumentException("Command module metadata must not contain duplicate category identities.");
                }
            }

            for (int index = 0; index < categories.Length; index++)
            {
                CommandCategoryMetadata category = categories[index];
                if (category.RepresentativeCommandTypeId.Value == 0)
                    throw new ArgumentException("Command category metadata must provide a representative command type identity.");

                ReadOnlySpan<CommandTypeId> commandTypeIds = category.CommandTypeIds;
                if (commandTypeIds.Length == 0)
                    throw new ArgumentException("Command category metadata must provide at least one command type identity.");

                if (commandTypeIds[0] != category.RepresentativeCommandTypeId)
                    throw new ArgumentException("Command category metadata must keep the representative command type as the first command type.");

                for (int commandIndex = 1; commandIndex < commandTypeIds.Length; commandIndex++)
                {
                    if (commandTypeIds[commandIndex - 1].Value > commandTypeIds[commandIndex].Value)
                        throw new ArgumentException("Command category metadata must keep command type identities sorted.");

                    if (commandTypeIds[commandIndex - 1] == commandTypeIds[commandIndex])
                        throw new ArgumentException("Command category metadata must not contain duplicate command type identities.");
                }

                ReadOnlySpan<ModuleId> ownerModules = category.OwnerModules;
                for (int ownerIndex = 1; ownerIndex < ownerModules.Length; ownerIndex++)
                {
                    if (ownerModules[ownerIndex - 1].Value > ownerModules[ownerIndex].Value)
                        throw new ArgumentException("Command category metadata must keep owner module identities sorted.");

                    if (ownerModules[ownerIndex - 1] == ownerModules[ownerIndex])
                        throw new ArgumentException("Command category metadata must not contain duplicate owner module identities.");
                }
            }
        }

        public static Hash128 ComputeValueSchemaHash(ReadOnlySpan<ValueKeyIR> valueKeys)
        {
            List<string> tokens = new List<string>(valueKeys.Length * 4);
            for (int index = 0; index < valueKeys.Length; index++)
                AddValueKeyTokens(tokens, valueKeys[index]);

            return VerifiedArtifactHeaderHashing.ComputeGeneratedHash(tokens);
        }

        public static Hash128 ComputeRuntimeQueryHash(ReadOnlySpan<RuntimeQueryIR> runtimeQueries)
        {
            List<string> tokens = new List<string>(runtimeQueries.Length * 4);
            for (int index = 0; index < runtimeQueries.Length; index++)
                AddRuntimeQueryTokens(tokens, runtimeQueries[index]);

            return VerifiedArtifactHeaderHashing.ComputeGeneratedHash(tokens);
        }

        public static Hash128 ComputeDebugMapHash(ReadOnlySpan<KernelDebugMapEntry> entries)
        {
            List<string> tokens = new List<string>(entries.Length * 4);
            for (int index = 0; index < entries.Length; index++)
            {
                KernelDebugMapEntry entry = entries[index];
                tokens.Add("DEBUG|" + entry.Identity + "|" + entry.Name + "|" + entry.OwnerModule.Value + "|" + entry.Source.Value + "|" + entry.ProfileMask + "|" + entry.ArtifactHash + "|" + (entry.DiagnosticSeedKey ?? string.Empty) + "|" + (entry.LegacyOrigin ?? string.Empty));
            }

            return VerifiedArtifactHeaderHashing.ComputeGeneratedHash(tokens);
        }

        public static Hash128 ComputeGenerationReportHash(
            string selectedProfile,
            KernelProfileMask selectedProfileMask,
            int artifactCount,
            int mappingCount,
            int debugMapEntryCount,
            ValidationResultStatus validationStatus,
            ReadOnlySpan<Hash128> artifactHashes)
        {
            List<string> tokens = new List<string>(artifactHashes.Length + 8)
            {
                selectedProfile,
                selectedProfileMask.ToString(),
                artifactCount.ToString(),
                mappingCount.ToString(),
                debugMapEntryCount.ToString(),
                validationStatus.ToString(),
            };

            for (int index = 0; index < artifactHashes.Length; index++)
                tokens.Add(artifactHashes[index].ToString());

            return VerifiedArtifactHeaderHashing.ComputeGeneratedHash(tokens);
        }

        public static Hash128 ComputeValidationReportHash(ProjectionValidationReport report)
        {
            List<string> tokens = new List<string>(report.Issues.Count * 10 + 8)
            {
                report.SelectedProfile,
                report.Status.ToString(),
                report.Summary.InfoCount.ToString(),
                report.Summary.WarningCount.ToString(),
                report.Summary.ErrorCount.ToString(),
                report.Summary.FatalCount.ToString(),
            };

            for (int index = 0; index < report.Issues.Count; index++)
            {
                DependencyValidationIssue issue = report.Issues[index];
                tokens.Add(issue.Code);
                tokens.Add(issue.Severity.ToString());
                tokens.Add(issue.Category.ToString());
                tokens.Add(issue.From.ToString());
                tokens.Add(issue.To.HasValue ? issue.To.Value.ToString() : string.Empty);
                tokens.Add(issue.OwnerModule.Value.ToString());
                tokens.Add(issue.Source.Value.ToString());
                tokens.Add(issue.Phase.ToString());
                tokens.Add(issue.Message);
                tokens.Add(issue.SuggestedFix ?? string.Empty);
            }

            return VerifiedArtifactHeaderHashing.ComputeGeneratedHash(tokens);
        }

        public static Hash128 ComputeRegistryHash(KernelIR kernelIR)
        {
            List<string> tokens = new List<string>();

            ReadOnlySpan<ModuleIR> modules = kernelIR.Modules;
            for (int index = 0; index < modules.Length; index++)
                AddModuleTokens(tokens, modules[index]);

            ReadOnlySpan<ServiceIR> services = kernelIR.Services;
            for (int index = 0; index < services.Length; index++)
                AddServiceTokens(tokens, services[index]);

            ReadOnlySpan<ScopeIR> scopes = kernelIR.Scopes;
            for (int index = 0; index < scopes.Length; index++)
                AddScopeTokens(tokens, scopes[index]);

            ReadOnlySpan<LifecycleIR> lifecycles = kernelIR.Lifecycles;
            for (int index = 0; index < lifecycles.Length; index++)
                AddLifecycleTokens(tokens, lifecycles[index]);

            ReadOnlySpan<CommandIR> commands = kernelIR.Commands;
            for (int index = 0; index < commands.Length; index++)
                AddCommandTokens(tokens, commands[index]);

            ReadOnlySpan<ValueKeyIR> valueKeys = kernelIR.ValueKeys;
            for (int index = 0; index < valueKeys.Length; index++)
                AddValueKeyTokens(tokens, valueKeys[index]);

            ReadOnlySpan<RuntimeQueryIR> runtimeQueries = kernelIR.RuntimeQueries;
            for (int index = 0; index < runtimeQueries.Length; index++)
                AddRuntimeQueryTokens(tokens, runtimeQueries[index]);

            return VerifiedArtifactHeaderHashing.ComputeGeneratedHash(tokens);
        }

        public static Hash128 ComputeProfileHash(KernelIR kernelIR, string selectedProfile, KernelProfileMask selectedProfileMask)
        {
            List<string> tokens = new List<string>(kernelIR.Modules.Length * 8 + 8)
            {
                selectedProfile,
                selectedProfileMask.ToString(),
                kernelIR.Profile.Id,
                kernelIR.Profile.Mask.ToString(),
                kernelIR.Profile.Availability.Profiles.ToString(),
                kernelIR.Profile.Availability.EnabledByDefault.ToString(),
                kernelIR.Profile.Availability.Condition ?? string.Empty,
            };

            ReadOnlySpan<ModuleIR> modules = kernelIR.Modules;
            for (int index = 0; index < modules.Length; index++)
                AddModuleTokens(tokens, modules[index]);

            return VerifiedArtifactHeaderHashing.ComputeGeneratedHash(tokens);
        }

        public static Hash128 ComputeSourceHash(KernelIR kernelIR)
        {
            return VerifiedArtifactHeaderHashing.ComputeSourceHash(kernelIR);
        }

        public static void ValidateHeaderHash(VerifiedArtifactHeader header, Hash128 contentHash)
        {
            if (header.GeneratedHash != contentHash)
                throw new ArgumentException("Projection artifact headers must match their generated content hash.", nameof(header));
        }

        static void AddModuleTokens(List<string> tokens, ModuleIR module)
        {
            AvailabilityIR availability = module.Availability.Value;
            tokens.Add("MODULE|" + module.Id.Value + "|" + module.Name + "|" + module.Kind + "|" + module.Version.Value + "|" + availability.Profiles + "|" + availability.EnabledByDefault + "|" + (availability.Condition ?? string.Empty));

            ModuleDependencyIR[] requiredModules = module.RequiredModules.ToArray();
            Array.Sort(requiredModules, static (left, right) => CompareModuleDependency(left, right));
            for (int index = 0; index < requiredModules.Length; index++)
            {
                ModuleDependencyIR dependency = requiredModules[index];
                tokens.Add("MODULE_REQUIRED|" + dependency.ModuleId.Value + "|" + dependency.AbsenceBehavior + "|" + (dependency.DisabledContribution ?? string.Empty) + "|" + dependency.AlternativeModuleId.Value + "|" + dependency.ProfileSpecificErrorProfiles);
            }

            ModuleDependencyIR[] optionalModules = module.OptionalModules.ToArray();
            Array.Sort(optionalModules, static (left, right) => CompareModuleDependency(left, right));
            for (int index = 0; index < optionalModules.Length; index++)
            {
                ModuleDependencyIR dependency = optionalModules[index];
                tokens.Add("MODULE_OPTIONAL|" + dependency.ModuleId.Value + "|" + dependency.AbsenceBehavior + "|" + (dependency.DisabledContribution ?? string.Empty) + "|" + dependency.AlternativeModuleId.Value + "|" + dependency.ProfileSpecificErrorProfiles);
            }

            if (module.LegacyCompat != null)
            {
                LegacyCompatDescriptorIR legacyCompat = module.LegacyCompat;
                tokens.Add("MODULE_LEGACY|" + legacyCompat.Kind + "|" + legacyCompat.LegacySystemName + "|" + legacyCompat.TargetSubsystem + "|" + legacyCompat.Profiles + "|" + legacyCompat.RemovalStatus + "|" + (legacyCompat.DiagnosticsCode ?? string.Empty) + "|" + (legacyCompat.RemovalCondition ?? string.Empty));
            }
        }

        static int CompareModuleDependency(ModuleDependencyIR left, ModuleDependencyIR right)
        {
            int comparison = left.ModuleId.Value.CompareTo(right.ModuleId.Value);
            if (comparison != 0)
                return comparison;

            comparison = left.AbsenceBehavior.HasValue.CompareTo(right.AbsenceBehavior.HasValue);
            if (comparison != 0)
                return comparison;

            comparison = left.AbsenceBehavior.GetValueOrDefault().CompareTo(right.AbsenceBehavior.GetValueOrDefault());
            if (comparison != 0)
                return comparison;

            comparison = StringComparer.Ordinal.Compare(left.DisabledContribution, right.DisabledContribution);
            if (comparison != 0)
                return comparison;

            comparison = left.AlternativeModuleId.Value.CompareTo(right.AlternativeModuleId.Value);
            if (comparison != 0)
                return comparison;

            return left.ProfileSpecificErrorProfiles.CompareTo(right.ProfileSpecificErrorProfiles);
        }

        static void AddServiceTokens(List<string> tokens, ServiceIR service)
        {
            tokens.Add("SERVICE|" + service.Id.Value + "|" + service.Name + "|" + service.Lifetime + "|" + service.Cardinality + "|" + service.OwnerModule.Value + "|" + service.FactoryKind);

            ServiceContractIR[] contracts = service.Contracts.ToArray();
            Array.Sort(contracts, static (left, right) => StringComparer.Ordinal.Compare(left.ContractName, right.ContractName));
            for (int index = 0; index < contracts.Length; index++)
                tokens.Add("CONTRACT|" + contracts[index].ContractName);

            ServiceDependencyIR[] dependencies = service.Dependencies.ToArray();
            Array.Sort(dependencies, static (left, right) => StringComparer.Ordinal.Compare(left.Target.ToString(), right.Target.ToString()));
            for (int index = 0; index < dependencies.Length; index++)
            {
                ServiceDependencyIR dependency = dependencies[index];
                tokens.Add("DEPENDENCY|" + dependency.Target + "|" + dependency.Strength);
            }
        }

        static void AddServiceEntryTokens(List<string> tokens, ServiceEntryPlan entry)
        {
            tokens.Add("SERVICE_ENTRY|" + entry.ServiceId.Value + "|" + entry.Name + "|" + entry.Lifetime + "|" + entry.Cardinality + "|" + entry.OwnerModule.Value + "|" + entry.Factory.FactoryKind + "|" + entry.Factory.Source.Value + "|" + entry.Source.Value);

            ReadOnlySpan<ServiceContractRef> contracts = entry.Contracts;
            for (int index = 0; index < contracts.Length; index++)
                tokens.Add("SERVICE_ENTRY_CONTRACT|" + entry.ServiceId.Value + "|" + contracts[index].ContractName + "|" + contracts[index].Source.Value);

            ReadOnlySpan<ServiceDependencyIR> dependencies = entry.Dependencies;
            for (int index = 0; index < dependencies.Length; index++)
            {
                ServiceDependencyIR dependency = dependencies[index];
                tokens.Add("SERVICE_ENTRY_DEP|" + entry.ServiceId.Value + "|" + dependency.Target + "|" + dependency.Strength + "|" + dependency.Source.Value);
            }
        }

        static void AddServiceSlotTokens(List<string> tokens, ServiceSlotPlan slot)
        {
            tokens.Add("SERVICE_SLOT|" + slot.SlotIndex + "|" + slot.EntryIndex + "|" + slot.ServiceId.Value + "|" + slot.Lifetime + "|" + slot.Cardinality + "|" + slot.OwnerModule.Value + "|" + slot.Factory.FactoryKind + "|" + slot.Factory.Source.Value + "|" + slot.Source.Value);

            ReadOnlySpan<ServiceContractRef> contracts = slot.Contracts;
            for (int index = 0; index < contracts.Length; index++)
                tokens.Add("SERVICE_SLOT_CONTRACT|" + slot.SlotIndex + "|" + contracts[index].ContractName + "|" + contracts[index].Source.Value);

            ReadOnlySpan<ServiceDependencyIR> dependencies = slot.Dependencies;
            for (int index = 0; index < dependencies.Length; index++)
            {
                ServiceDependencyIR dependency = dependencies[index];
                tokens.Add("SERVICE_SLOT_DEP|" + slot.SlotIndex + "|" + dependency.Target + "|" + dependency.Strength + "|" + dependency.Source.Value);
            }
        }

        static void AddScopeTokens(List<string> tokens, ScopeIR scope)
        {
            tokens.Add("SCOPE|" + scope.AuthoringId.Value + "|" + scope.PlanId.Value + "|" + scope.Name + "|" + scope.Kind + "|" + scope.OwnerModule.Value + "|" + scope.ParentAuthoringId.Value + "|" + scope.Lifecycle.PlanId.Value);

            ScopeServiceRequirementIR[] requiredServices = scope.RequiredServices.ToArray();
            Array.Sort(requiredServices, static (left, right) => left.ServiceId.Value.CompareTo(right.ServiceId.Value));
            for (int index = 0; index < requiredServices.Length; index++)
            {
                ScopeServiceRequirementIR requirement = requiredServices[index];
                tokens.Add("SCOPE_SERVICE|" + requirement.ServiceId.Value + "|" + requirement.Strength);
            }

            ScopeValueInitRefIR[] valueInits = scope.ValueInitPlans.ToArray();
            Array.Sort(valueInits, static (left, right) => left.PlanId.Value.CompareTo(right.PlanId.Value));
            for (int index = 0; index < valueInits.Length; index++)
            {
                ScopeValueInitRefIR valueInit = valueInits[index];
                tokens.Add("SCOPE_VALUE|" + valueInit.PlanId.Value);
            }

            if (scope.UnityObjectLink != null)
            {
                UnityObjectLinkIR unityObjectLink = scope.UnityObjectLink;
                tokens.Add("SCOPE_LINK|" + unityObjectLink.Kind + "|" + (unityObjectLink.SourceGuid ?? string.Empty) + "|" + unityObjectLink.LocalFileId + "|" + unityObjectLink.DebugName + "|" + unityObjectLink.Source.Value);
            }
        }

        static void AddValueInitPlanTokens(List<string> tokens, ValueInitPlanIR valueInitPlan)
        {
            tokens.Add("VALUE_INIT_PLAN|" + valueInitPlan.PlanId.Value + "|" + valueInitPlan.OwnerModule.Value + "|" + valueInitPlan.TargetScopePlanId.Value + "|" + valueInitPlan.TargetStoreRef + "|" + valueInitPlan.ExecutionPhase + "|" + valueInitPlan.Order + "|" + valueInitPlan.Availability.Profiles + "|" + valueInitPlan.Availability.EnabledByDefault + "|" + (valueInitPlan.Availability.Condition ?? string.Empty));

            ValueInitEntryIR[] entries = valueInitPlan.Entries.ToArray();
            Array.Sort(entries, static (left, right) =>
            {
                int result = left.Order.CompareTo(right.Order);
                if (result != 0)
                    return result;

                result = left.KeyId.Value.CompareTo(right.KeyId.Value);
                if (result != 0)
                    return result;

                result = ((int)left.SourceKind).CompareTo((int)right.SourceKind);
                if (result != 0)
                    return result;

                result = ((int)left.ValueKind).CompareTo((int)right.ValueKind);
                if (result != 0)
                    return result;

                result = ((int)left.OverwritePolicy).CompareTo((int)right.OverwritePolicy);
                if (result != 0)
                    return result;

                result = StringComparer.Ordinal.Compare(left.SerializedValue, right.SerializedValue);
                if (result != 0)
                    return result;

                result = StringComparer.Ordinal.Compare(left.EvaluationLocalRef, right.EvaluationLocalRef);
                return result != 0 ? result : left.Source.Value.CompareTo(right.Source.Value);
            });

            for (int index = 0; index < entries.Length; index++)
            {
                ValueInitEntryIR entry = entries[index];
                tokens.Add("VALUE_INIT_ENTRY|" + valueInitPlan.PlanId.Value + "|" + entry.KeyId.Value + "|" + entry.SourceKind + "|" + entry.ValueKind + "|" + entry.Order + "|" + entry.OverwritePolicy + "|" + (entry.SerializedValue ?? string.Empty) + "|" + (entry.EvaluationLocalRef ?? string.Empty));
            }
        }

        static void AddLifecycleTokens(List<string> tokens, LifecycleIR lifecycle)
        {
            tokens.Add("LIFECYCLE|" + lifecycle.PlanId.Value + "|" + lifecycle.Name + "|" + lifecycle.OwnerModule.Value + "|" + lifecycle.FailurePolicy + "|" + lifecycle.FailurePolicyIsExplicit + "|" + lifecycle.FailurePolicyJustificationProfiles + "|" + (lifecycle.FailurePolicyJustification ?? string.Empty) + "|" + lifecycle.AcquireRollbackPolicy);

            LifecycleStepIR[] steps = lifecycle.Steps.ToArray();
            Array.Sort(steps, static (left, right) => left.Id.Value.CompareTo(right.Id.Value));
            for (int index = 0; index < steps.Length; index++)
            {
                LifecycleStepIR step = steps[index];

                tokens.Add("STEP|" + step.Id.Value + "|" + step.Phase + "|" + step.Order + "|" + step.Target.Kind + "|" + step.Target.TargetService.Value + "|" + step.Target.TargetScope.Value + "|" + step.Target.TargetRuntimeQuery.Value + "|" + (step.Target.TargetLocalRef ?? string.Empty) + "|" + step.Action + "|" + step.TickCardinality + "|" + step.ExecutionMode);

                if (step.AsyncPolicy != null)
                {
                    LifecycleAsyncPolicyIR asyncPolicy = step.AsyncPolicy;
                    tokens.Add("STEP_ASYNC|" + step.Id.Value + "|" + asyncPolicy.CancellationSourceKind + "|" + asyncPolicy.TimeoutPolicyKind + "|" + asyncPolicy.TimeoutMilliseconds + "|" + asyncPolicy.CompletionRequirementKind + "|" + asyncPolicy.WaitForNextStep);
                }

                DependencyEdgeId[] dependencies = step.Dependencies.ToArray();
                Array.Sort(dependencies, static (left, right) => left.Value.CompareTo(right.Value));
                for (int dependencyIndex = 0; dependencyIndex < dependencies.Length; dependencyIndex++)
                    tokens.Add("STEP_DEP|" + step.Id.Value + "|" + dependencies[dependencyIndex].Value);
            }
        }

        static void AddCommandTokens(List<string> tokens, CommandIR command)
        {
            tokens.Add("COMMAND|" + command.TypeId.Value + "|" + command.RuntimeName + "|" + command.AuthoringKey.Id.Value + "|" + command.AuthoringKey.Value + "|" + command.CategoryId.Value + "|" + command.OwnerModule.Value + "|" + command.PayloadSchema.Id.Value + "|" + command.PayloadSchema.UnknownFieldPolicy + "|" + command.Executor.Id.Value);

            CommandPayloadFieldIR[] payloadFields = command.PayloadSchema.Fields.ToArray();
            Array.Sort(payloadFields, static (left, right) => StringComparer.Ordinal.Compare(left.FieldPath, right.FieldPath));
            for (int fieldIndex = 0; fieldIndex < payloadFields.Length; fieldIndex++)
            {
                CommandPayloadFieldIR field = payloadFields[fieldIndex];
                tokens.Add("COMMAND_PAYLOAD_FIELD|" + command.TypeId.Value + "|" + field.FieldPath + "|" + field.Kind + "|" + field.Requirement + "|" + field.ReferenceKind + "|" + field.AllowNull + "|" + field.Source.Value);
            }

            CommandDependencyIR[] dependencies = command.Dependencies.ToArray();
            Array.Sort(dependencies, static (left, right) => StringComparer.Ordinal.Compare(left.Target.ToString(), right.Target.ToString()));
            for (int index = 0; index < dependencies.Length; index++)
            {
                CommandDependencyIR dependency = dependencies[index];
                tokens.Add("COMMAND_DEP|" + dependency.Target + "|" + dependency.Strength);
            }
        }

        static void AddCommandEntryTokens(List<string> tokens, CommandEntryPlan command)
        {
            tokens.Add("COMMAND_ENTRY|" + command.TypeId.Value + "|" + command.RuntimeName + "|" + command.AuthoringKey.Id.Value + "|" + command.AuthoringKey.Value + "|" + command.CategoryId.Value + "|" + command.OwnerModule.Value + "|" + command.Executor.Id.Value + "|" + command.Executor.Source.Value + "|" + command.PayloadSchema.SchemaId.Value + "|" + command.PayloadSchema.CommandTypeId.Value + "|" + command.PayloadSchema.UnknownFieldPolicy + "|" + command.PayloadSchema.Source.Value + "|" + command.Source.Value);

            ReadOnlySpan<CommandPayloadFieldPlan> fields = command.PayloadSchema.Fields;
            for (int fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
            {
                CommandPayloadFieldPlan field = fields[fieldIndex];
                tokens.Add("COMMAND_ENTRY_PAYLOAD_FIELD|" + command.TypeId.Value + "|" + field.FieldPath + "|" + field.Kind + "|" + field.Requirement + "|" + field.ReferenceKind + "|" + field.AllowNull + "|" + field.Source.Value);
            }

            ReadOnlySpan<CommandDependencyIR> dependencies = command.Dependencies;
            for (int index = 0; index < dependencies.Length; index++)
            {
                CommandDependencyIR dependency = dependencies[index];
                tokens.Add("COMMAND_ENTRY_DEP|" + dependency.Target + "|" + dependency.Strength + "|" + dependency.Source.Value);
            }
        }

        static void AddCommandModuleTokens(List<string> tokens, CommandModuleMetadata module)
        {
            tokens.Add("COMMAND_MODULE|" + module.ModuleId.Value + "|" + module.RepresentativeCommandTypeId.Value + "|" + module.CommandCount + "|" + JoinCommandTypeIds(module.CommandTypeIds) + "|" + JoinCommandCategoryIds(module.CategoryIds) + "|" + module.Source.Value);
        }

        static void AddCommandCategoryTokens(List<string> tokens, CommandCategoryMetadata category)
        {
            tokens.Add("COMMAND_CATEGORY|" + category.CategoryId.Value + "|" + category.RepresentativeCommandTypeId.Value + "|" + category.CommandCount + "|" + JoinModuleIds(category.OwnerModules) + "|" + JoinCommandTypeIds(category.CommandTypeIds) + "|" + category.Source.Value);
        }

        static string JoinCommandTypeIds(ReadOnlySpan<CommandTypeId> ids)
        {
            string[] values = new string[ids.Length];
            for (int index = 0; index < ids.Length; index++)
                values[index] = ids[index].Value.ToString();

            return string.Join(",", values);
        }

        static string JoinCommandCategoryIds(ReadOnlySpan<CommandCategoryId> ids)
        {
            string[] values = new string[ids.Length];
            for (int index = 0; index < ids.Length; index++)
                values[index] = ids[index].Value.ToString();

            return string.Join(",", values);
        }

        static string JoinModuleIds(ReadOnlySpan<ModuleId> ids)
        {
            string[] values = new string[ids.Length];
            for (int index = 0; index < ids.Length; index++)
                values[index] = ids[index].Value.ToString();

            return string.Join(",", values);
        }

        static void AddValueKeyTokens(List<string> tokens, ValueKeyIR valueKey)
        {
            tokens.Add("VALUE|" + valueKey.Id.Value + "|" + valueKey.StableKey + "|" + valueKey.DisplayName + "|" + valueKey.Kind + "|" + valueKey.OwnerModule.Value + "|" + valueKey.Schema.Id.Value + "|" + valueKey.SavePolicy.Persists + "|" + valueKey.SavePolicy.SaveAcrossProfiles + "|" + (valueKey.SavePolicy.Channel ?? string.Empty));
        }

        static void AddRuntimeQueryTokens(List<string> tokens, RuntimeQueryIR runtimeQuery)
        {
            tokens.Add("QUERY|" + runtimeQuery.Id.Value + "|" + runtimeQuery.Name + "|" + runtimeQuery.TargetKind + "|" + runtimeQuery.OwnerModule.Value + "|" + runtimeQuery.Policy.RequiresUniqueResult + "|" + runtimeQuery.Policy.AllowMissing + "|" + runtimeQuery.Policy.UpdatePhase);

            RuntimeIdentityFieldIR[] indexedFields = runtimeQuery.IndexedFields.ToArray();
            Array.Sort(indexedFields, static (left, right) => StringComparer.Ordinal.Compare(left.Name, right.Name));
            for (int index = 0; index < indexedFields.Length; index++)
            {
                RuntimeIdentityFieldIR field = indexedFields[index];
                tokens.Add("QUERY_FIELD|" + field.Name + "|" + field.ValueType + "|" + field.IsRequired);
            }
        }
    }

    static class KernelProjectionArtifactKindValidator
    {
        public static void ValidateArtifactKind(VerifiedArtifactHeader header, ArtifactKind expectedKind)
        {
            if (header.ArtifactKind != expectedKind)
                throw new ArgumentException("Projection artifacts must be created with the matching artifact kind.", nameof(header));
        }
    }
}
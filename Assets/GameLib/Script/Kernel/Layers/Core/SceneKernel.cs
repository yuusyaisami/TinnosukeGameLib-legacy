#nullable enable
using System;
using System.Collections.Generic;
using Game.Kernel.Abstractions;
using Game.Kernel.Boot;
using Game.Kernel.Diagnostics;
using Game.Kernel.Generation;
using Game.Kernel.IR;
using Game.Kernel.ScopeGraph;
using Game.Kernel.Value;

namespace Game.Kernel.Layers
{
    public readonly struct SceneKernelServiceLifecycleContext
    {
        public SceneKernelServiceLifecycleContext(
            SceneKernel sceneKernel,
            KernelRuntimeServiceSlot serviceSlot,
            LifecycleDispatchStep step,
            LifecyclePhase phase,
            bool isRollback)
        {
            SceneKernel = sceneKernel ?? throw new ArgumentNullException(nameof(sceneKernel));
            ServiceSlot = serviceSlot;
            Step = step;
            Phase = phase;
            IsRollback = isRollback;
        }

        public SceneKernel SceneKernel { get; }

        public KernelRuntimeServiceSlot ServiceSlot { get; }

        public LifecycleDispatchStep Step { get; }

        public LifecyclePhase Phase { get; }

        public bool IsRollback { get; }
    }

    public interface ISceneKernelServiceLifecycleHandler
    {
        ServiceId ServiceId { get; }

        bool TryDispatch(in SceneKernelServiceLifecycleContext context, out KernelDiagnostic? diagnostic);

        bool TryRollback(in SceneKernelServiceLifecycleContext context, out KernelDiagnostic? diagnostic);
    }

    public readonly struct SceneKernelServiceActivationContext
    {
        public SceneKernelServiceActivationContext(
            SceneKernel sceneKernel,
            ServiceRegistrationPlanEntry registration,
            KernelRuntimeServiceSlot serviceSlot,
            ISceneKernelSpawnBoundary? spawnBoundary)
        {
            SceneKernel = sceneKernel ?? throw new ArgumentNullException(nameof(sceneKernel));
            Registration = registration ?? throw new ArgumentNullException(nameof(registration));
            ServiceSlot = serviceSlot;
            SpawnBoundary = spawnBoundary;
        }

        public SceneKernel SceneKernel { get; }

        public ServiceRegistrationPlanEntry Registration { get; }

        public KernelRuntimeServiceSlot ServiceSlot { get; }

        public ISceneKernelSpawnBoundary? SpawnBoundary { get; }
    }

    public interface ISceneKernelServiceFactory
    {
        ServiceId ServiceId { get; }

        bool TryCreate(in SceneKernelServiceActivationContext context, out ISceneKernelServiceLifecycleHandler handler, out KernelDiagnostic? diagnostic);
    }

    public sealed class SceneKernel
    {
        enum EntityRegistrationSourceState
        {
            None = 0,
            Manual = 10,
            CompositionPlan = 20,
        }

        readonly EntityRegistrationTable entityTable = new EntityRegistrationTable();
        readonly SceneKernelSpawnBoundary spawnBoundary;
        readonly EntityServiceSlotTable entityServiceSlotTable = new EntityServiceSlotTable();
        readonly Dictionary<ServiceId, ISceneKernelServiceFactory> serviceFactories = new Dictionary<ServiceId, ISceneKernelServiceFactory>();
        readonly Dictionary<ServiceId, ISceneKernelServiceLifecycleHandler> serviceLifecycleHandlers = new Dictionary<ServiceId, ISceneKernelServiceLifecycleHandler>();
        readonly SceneKernelLifecycleDispatchExecutor lifecycleDispatchExecutor;
        EntityRegistrationSourceState entityRegistrationSourceState;
        bool entityServiceSlotHydrated;
        bool manualEntityRegistrationTouched;

        public SceneKernel(SceneKernelHandle handle, string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                throw new ArgumentException("SceneKernel must provide a non-empty scene name.", nameof(sceneName));

            Handle = handle;
            SceneName = sceneName;
            State = KernelLayerState.Created;
            spawnBoundary = new SceneKernelSpawnBoundary(handle);
            lifecycleDispatchExecutor = new SceneKernelLifecycleDispatchExecutor(this);
        }

        public SceneKernelHandle Handle { get; }

        public string SceneName { get; }

        public KernelLayerKind LayerKind => KernelLayerKind.Scene;

        public KernelLayerState State { get; private set; }

        public ApplicationKernel? OwnerApplicationKernel { get; private set; }

        public ISceneKernelComposition? Composition { get; private set; }

        public int RegisteredEntityCount => entityTable.Count;

        public bool TryGetRuntimeDiagnostics(out KernelRuntimeDiagnostics diagnostics)
        {
            if (Composition != null
                && Composition.TryGetBoundary(SceneKernelBoundaryKind.RuntimeSurface, out object? boundary)
                && boundary is IKernelBootRuntimeSurface runtimeSurface)
            {
                diagnostics = runtimeSurface.Diagnostics;
                return true;
            }

            diagnostics = null!;
            return false;
        }

        public bool TryGetDebugMap(out KernelDebugMap debugMap)
        {
            if (Composition != null
                && Composition.TryGetBoundary(SceneKernelBoundaryKind.RuntimeSurface, out object? boundary)
                && boundary is IKernelBootRuntimeSurface runtimeSurface)
            {
                debugMap = runtimeSurface.DebugMap;
                return true;
            }

            debugMap = null!;
            return false;
        }

        public bool TryGetSpawnBoundary(out ISceneKernelSpawnBoundary spawnBoundary)
        {
            if (Composition != null
                && Composition.TryGetBoundary(SceneKernelBoundaryKind.SpawnBoundary, out object? boundary)
                && boundary is ISceneKernelSpawnBoundary typedBoundary)
            {
                spawnBoundary = typedBoundary;
                return true;
            }

            spawnBoundary = null!;
            return false;
        }

        public bool TryGetValueStore(EntityRef entityRef, out IValueStore valueStore)
        {
            EnsureOperational();
            EnsureEntityRegistrationHydrated();

            if (entityRef.IsEmpty)
            {
                valueStore = null!;
                return false;
            }

            if (!entityTable.TryGetSlot(entityRef, out _))
            {
                valueStore = null!;
                return false;
            }

            if (Composition != null
                && Composition.TryGetBoundary(SceneKernelBoundaryKind.ValueStore, out object? boundary)
                && boundary is ISceneKernelValueStoreBoundary typedBoundary
                && typedBoundary.TryGetValueStore(entityRef, out valueStore))
                return true;

            valueStore = null!;
            return false;
        }

        public bool TryRegisterServiceLifecycleHandler(ISceneKernelServiceLifecycleHandler handler)
        {
            EnsureOperational();

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            ServiceId serviceId = handler.ServiceId;
            if (serviceId.Value <= 0 || serviceLifecycleHandlers.ContainsKey(serviceId))
                return false;

            serviceLifecycleHandlers.Add(serviceId, handler);
            return true;
        }

        public bool TryRegisterServiceFactory(ISceneKernelServiceFactory factory)
        {
            EnsureOperational();

            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            ServiceId serviceId = factory.ServiceId;
            if (serviceId.Value <= 0 || serviceFactories.ContainsKey(serviceId))
                return false;

            serviceFactories.Add(serviceId, factory);
            return true;
        }

        public bool TryUnregisterServiceLifecycleHandler(ServiceId serviceId)
        {
            EnsureOperational();

            if (serviceId.Value <= 0)
                return false;

            return serviceLifecycleHandlers.Remove(serviceId);
        }

        public bool TryUnregisterServiceFactory(ServiceId serviceId)
        {
            EnsureOperational();

            if (serviceId.Value <= 0)
                return false;

            return serviceFactories.Remove(serviceId);
        }

        public void Initialize()
        {
            if (State != KernelLayerState.Created)
                throw new InvalidOperationException("SceneKernel can only be initialized from the Created state.");

            State = KernelLayerState.Initialized;
        }

        public void AttachComposition(ISceneKernelComposition composition)
        {
            if (composition == null)
                throw new ArgumentNullException(nameof(composition));

            EnsureOperational();

            if (Composition != null)
                throw new InvalidOperationException("SceneKernel already has an attached composition.");

            ValidateSceneCompositionPlacements(composition.Placements);
            Composition = composition;
            EnsureEntityRegistrationHydrated();
            spawnBoundary.Open();
            Composition.BindSpawnBoundary(spawnBoundary);
        }

        public void DetachComposition(ISceneKernelComposition composition)
        {
            if (composition == null)
                throw new ArgumentNullException(nameof(composition));

            if (!ReferenceEquals(Composition, composition))
                throw new InvalidOperationException("SceneKernel can only detach its currently attached composition.");

            if (OwnerApplicationKernel != null)
                throw new InvalidOperationException("SceneKernel must be detached from ApplicationKernel before its scene composition can be detached.");

            Composition.ClearSpawnBoundary();
            Composition.ClearValueStoreBoundary();
            spawnBoundary.Close();
            Composition = null;
            entityTable.Clear();
            entityServiceSlotTable.Clear();
            serviceFactories.Clear();
            serviceLifecycleHandlers.Clear();
            entityRegistrationSourceState = EntityRegistrationSourceState.None;
            entityServiceSlotHydrated = false;
            manualEntityRegistrationTouched = false;
        }

        public bool TryGetBoundary(SceneKernelBoundaryKind boundaryKind, out object? boundary)
        {
            if (boundaryKind == SceneKernelBoundaryKind.Unknown)
                throw new ArgumentOutOfRangeException(nameof(boundaryKind), boundaryKind, "SceneKernel boundary queries must target a defined boundary kind.");

            if (Composition == null)
            {
                boundary = null;
                return false;
            }

            EnsureEntityRegistrationHydrated();
            return Composition.TryGetBoundary(boundaryKind, out boundary);
        }

        public bool TryGetApplicationBoundary(ApplicationKernelBoundaryKind boundaryKind, out object? boundary)
        {
            if (boundaryKind == ApplicationKernelBoundaryKind.Unknown)
                throw new ArgumentOutOfRangeException(nameof(boundaryKind), boundaryKind, "SceneKernel application boundary queries must target a defined boundary kind.");

            switch (boundaryKind)
            {
                case ApplicationKernelBoundaryKind.Diagnostics:
                case ApplicationKernelBoundaryKind.SelectedManifest:
                case ApplicationKernelBoundaryKind.SelectedProfile:
                    break;
                case ApplicationKernelBoundaryKind.BootBoundary:
                case ApplicationKernelBoundaryKind.RuntimeSurfaceFactory:
                default:
                    boundary = null;
                    return false;
            }

            ApplicationKernel? owner = OwnerApplicationKernel;
            if (owner == null)
            {
                boundary = null;
                return false;
            }

            return owner.TryGetBoundary(boundaryKind, out boundary);
        }

        public void Shutdown()
        {
            if (OwnerApplicationKernel != null)
                throw new InvalidOperationException("SceneKernel must be detached from ApplicationKernel before shutdown.");

            if (State == KernelLayerState.Shutdown)
                return;

            if (Composition != null)
            {
                Composition.ClearSpawnBoundary();
                Composition.ClearValueStoreBoundary();
            }

            spawnBoundary.Close();
            entityTable.Clear();
            entityServiceSlotTable.Clear();
            serviceFactories.Clear();
            serviceLifecycleHandlers.Clear();
            entityRegistrationSourceState = EntityRegistrationSourceState.None;
            entityServiceSlotHydrated = false;
            manualEntityRegistrationTouched = false;
            Composition = null;
            State = KernelLayerState.Shutdown;
        }

        public void RegisterEntity(EntityRegistrationPlanEntry entry)
        {
            if (!TryRegisterEntity(entry, out _, out KernelDiagnostic? diagnostic))
                throw CreateRegistrationException(diagnostic!);
        }

        public bool TryRegisterEntity(EntityRegistrationPlanEntry entry, out SceneKernelEntitySlot slot, out KernelDiagnostic? diagnostic)
        {
            EnsureOperational();

            if (entityRegistrationSourceState == EntityRegistrationSourceState.None)
            {
                entityRegistrationSourceState = EntityRegistrationSourceState.Manual;
                manualEntityRegistrationTouched = true;
            }
            else if (entityRegistrationSourceState == EntityRegistrationSourceState.CompositionPlan)
                throw new InvalidOperationException("SceneKernel manual entity registration is forbidden after composition-driven entity registration has been hydrated.");

            bool registered = entityTable.TryRegister(Handle, SceneName, entry, out slot, out diagnostic);
            if (!registered)
                ReportDiagnosticIfAvailable(diagnostic);

            return registered;
        }

        public void UnregisterEntity(EntityRef entityRef)
        {
            if (!TryUnregisterEntity(entityRef, out _, out KernelDiagnostic? diagnostic))
                throw CreateRegistrationException(diagnostic!);
        }

        public bool TryUnregisterEntity(EntityRef entityRef, out SceneKernelEntitySlot removedSlot, out KernelDiagnostic? diagnostic)
        {
            EnsureOperational();
            EnsureEntityRegistrationHydrated();
            bool removed = entityTable.TryUnregister(Handle, SceneName, entityRef, out removedSlot, out diagnostic);
            if (removed)
                entityServiceSlotTable.RemoveEntity(removedSlot);
            else
                ReportDiagnosticIfAvailable(diagnostic);

            return removed;
        }

        public bool TryGetEntitySlot(EntityRef entityRef, out SceneKernelEntitySlot slot)
        {
            EnsureOperational();
            EnsureEntityRegistrationHydrated();
            return entityTable.TryGetSlot(entityRef, out slot);
        }

        public KernelRuntimeServiceSlot Resolve(EntityRef entityRef, ServiceId serviceId)
        {
            if (!TryResolve(entityRef, serviceId, out KernelRuntimeServiceSlot slot, out KernelDiagnostic? diagnostic))
                throw CreateRegistrationException(diagnostic!);

            return slot;
        }

        public bool TryResolve(EntityRef entityRef, ServiceId serviceId, out KernelRuntimeServiceSlot slot)
        {
            return TryResolve(entityRef, serviceId, out slot, out _);
        }

        public bool TryResolve(EntityRef entityRef, ServiceId serviceId, out KernelRuntimeServiceSlot slot, out KernelDiagnostic? diagnostic)
        {
            EnsureOperational();
            EnsureEntityRegistrationHydrated();

            if (entityRef.IsEmpty)
            {
                slot = default;
                diagnostic = CreateResolveDiagnostic(
                    SceneKernelServiceResolveCodes.InvalidEntityRef,
                    "SceneKernel resolve requires a non-empty EntityRef.",
                    entityRef,
                    serviceId);
                ReportDiagnosticIfAvailable(diagnostic);
                return false;
            }

            if (serviceId.Value <= 0)
            {
                slot = default;
                diagnostic = CreateResolveDiagnostic(
                    SceneKernelServiceResolveCodes.InvalidServiceId,
                    "SceneKernel resolve requires a non-zero ServiceId.",
                    entityRef,
                    serviceId);
                ReportDiagnosticIfAvailable(diagnostic);
                return false;
            }

            if (!entityTable.TryGetSlot(entityRef, out SceneKernelEntitySlot entitySlot))
            {
                slot = default;
                diagnostic = CreateResolveDiagnostic(
                    SceneKernelServiceResolveCodes.UnknownEntityRef,
                    "SceneKernel resolve requires the requested EntityRef to be registered in the scene-local entity table.",
                    entityRef,
                    serviceId);
                ReportDiagnosticIfAvailable(diagnostic);
                return false;
            }

            if (!entityServiceSlotHydrated)
            {
                if (!TryGetRuntimeServiceGraph(out _, out diagnostic))
                {
                    slot = default;
                    ReportDiagnosticIfAvailable(diagnostic);
                    return false;
                }

                if (Composition == null
                    || !Composition.TryGetBoundary(SceneKernelBoundaryKind.ServiceRegistrationPlan, out object? serviceRegistrationBoundary)
                    || serviceRegistrationBoundary is not ServiceRegistrationPlan)
                {
                    slot = default;
                    diagnostic = CreateResolveDiagnostic(
                        SceneKernelServiceResolveCodes.ServiceRegistrationPlanMissing,
                        "SceneKernel service resolution requires the verified ServiceRegistrationPlan boundary to be hydrated before hot-path lookup.",
                        entityRef,
                        serviceId);
                    ReportDiagnosticIfAvailable(diagnostic);
                    return false;
                }

                if (!Composition.TryGetBoundary(SceneKernelBoundaryKind.EntityServiceRoutePlan, out object? routeBoundary)
                    || routeBoundary is not EntityServiceRoutePlan)
                {
                    slot = default;
                    diagnostic = CreateResolveDiagnostic(
                        SceneKernelServiceResolveCodes.RoutePlanMissing,
                        "SceneKernel service resolution requires the verified EntityServiceRoutePlan boundary to be hydrated before hot-path lookup.",
                        entityRef,
                        serviceId);
                    ReportDiagnosticIfAvailable(diagnostic);
                    return false;
                }

                diagnostic = CreateResolveDiagnostic(
                    SceneKernelServiceResolveCodes.EntityTableNotHydrated,
                    "SceneKernel service resolution requires the scene-local service slot table to be hydrated during composition attachment.",
                    entityRef,
                    serviceId);
                slot = default;
                ReportDiagnosticIfAvailable(diagnostic);
                return false;
            }

            bool resolved = entityServiceSlotTable.TryResolve(Handle, SceneName, entitySlot, serviceId, out slot, out diagnostic);
            if (!resolved)
                ReportDiagnosticIfAvailable(diagnostic);

            return resolved;
        }

        public bool TryResolveSourceLocation(RuntimeIdentityRef identity, out SourceLocationRef sourceLocation, out KernelDiagnostic? diagnostic)
        {
            EnsureOperational();

            if (identity.IsEmpty)
            {
                sourceLocation = default;
                diagnostic = CreateDebugMapDiagnostic(
                    SceneKernelDiagnosticsCodes.InvalidRuntimeIdentity,
                    "SceneKernel debug-map lookup requires a fully specified runtime identity.",
                    identity);
                ReportDiagnosticIfAvailable(diagnostic);
                return false;
            }

            if (!TryGetDebugMap(out KernelDebugMap debugMap))
            {
                sourceLocation = default;
                diagnostic = CreateDebugMapDiagnostic(
                    SceneKernelDiagnosticsCodes.DebugMapMissing,
                    "SceneKernel debug-map lookup requires a bound verified debug map.",
                    identity);
                ReportDiagnosticIfAvailable(diagnostic);
                return false;
            }

            if (!debugMap.TryGetSourceLocation(identity, out sourceLocation))
            {
                diagnostic = CreateDebugMapDiagnostic(
                    SceneKernelDiagnosticsCodes.SourceLocationMissing,
                    "SceneKernel debug-map lookup could not find source provenance for the requested runtime identity.",
                    identity);
                ReportDiagnosticIfAvailable(diagnostic);
                return false;
            }

            diagnostic = null;
            return true;
        }

        public LifecycleDispatchResult DispatchLifecycle(LifecyclePhase phase)
        {
            if (!TryDispatchLifecycle(phase, out LifecycleDispatchResult result, out KernelDiagnostic? diagnostic))
                throw CreateSceneKernelException(diagnostic ?? CreateLifecycleDiagnostic(SceneKernelLifecycleCodes.DispatchFailed, "SceneKernel lifecycle dispatch failed without a diagnostic.", phase));

            return result;
        }

        public bool TryDispatchLifecycle(LifecyclePhase phase, out LifecycleDispatchResult result, out KernelDiagnostic? diagnostic)
        {
            EnsureOperational();

            if (!TryGetRuntimeScopeGraph(out KernelRuntimeScopeGraph runtimeScopeGraph, out diagnostic))
            {
                result = default;
                ReportDiagnosticIfAvailable(diagnostic);
                return false;
            }

            if (!TryGetLifecycleDispatcher(out KernelLifecycleDispatcher dispatcher, out diagnostic))
            {
                result = default;
                ReportDiagnosticIfAvailable(diagnostic);
                return false;
            }

            lifecycleDispatchExecutor.Bind(runtimeScopeGraph, phase);
            try
            {
                result = dispatcher.DispatchPhase(phase, lifecycleDispatchExecutor);
            }
            finally
            {
                lifecycleDispatchExecutor.Unbind();
            }

            diagnostic = result.FirstDiagnostic;
            if (result.HasFailures)
            {
                if (diagnostic == null)
                    diagnostic = CreateLifecycleDiagnostic(SceneKernelLifecycleCodes.DispatchFailed, "SceneKernel lifecycle dispatch reported failure without a primary diagnostic.", phase);

                ReportDiagnosticIfAvailable(diagnostic);
                return false;
            }

            return true;
        }

        public bool TryTransitionScopeState(ScopeHandle handle, ScopeRuntimeState nextState, out LifecycleDispatchResult result, out KernelDiagnostic? diagnostic)
        {
            EnsureOperational();

            if (!TryGetRuntimeScopeGraph(out KernelRuntimeScopeGraph runtimeScopeGraph, out diagnostic))
            {
                result = default;
                ReportDiagnosticIfAvailable(diagnostic);
                return false;
            }

            if (!runtimeScopeGraph.TryGetScopeBoundary(handle, out ScopeBoundarySnapshot scopeBoundary, out _, out diagnostic))
            {
                result = default;
                ReportDiagnosticIfAvailable(diagnostic);
                return false;
            }

            ScopeRuntimeState currentState = scopeBoundary.State;
            if (currentState == nextState)
            {
                result = default;
                diagnostic = null;
                return true;
            }

            if (!TryGetLifecyclePlanResolver(out ILifecyclePlanResolver lifecyclePlanResolver, out diagnostic))
            {
                result = default;
                ReportDiagnosticIfAvailable(diagnostic);
                return false;
            }

            if (!lifecyclePlanResolver.TryGetLifecycleDispatcher(scopeBoundary.Lifecycle.PlanId, out KernelLifecycleDispatcher? dispatcher) || dispatcher == null)
            {
                result = default;
                diagnostic = CreateScopeLifecycleDiagnostic(
                    SceneKernelLifecycleCodes.LifecyclePlanResolverMissing,
                    "SceneKernel scope transition requires a lifecycle dispatcher for the scope lifecycle plan.",
                    null,
                    handle,
                    scopeBoundary.PlanId,
                    scopeBoundary.Lifecycle.PlanId,
                    currentState,
                    nextState);
                ReportDiagnosticIfAvailable(diagnostic);
                return false;
            }

            if (!TryBuildTransitionSequence(handle, scopeBoundary, nextState, out SceneKernelLifecycleTransitionPhase[] phases, out diagnostic))
            {
                result = default;
                ReportDiagnosticIfAvailable(diagnostic);
                return false;
            }

            LifecycleDispatchResult aggregate = default;
            for (int index = 0; index < phases.Length; index++)
            {
                SceneKernelLifecycleTransitionPhase transitionPhase = phases[index];
                lifecycleDispatchExecutor.Bind(runtimeScopeGraph, transitionPhase.Phase, handle, scopeBoundary.PlanId);
                LifecycleDispatchResult phaseResult;
                try
                {
                    phaseResult = dispatcher.DispatchPhase(transitionPhase.Phase, lifecycleDispatchExecutor);
                }
                finally
                {
                    lifecycleDispatchExecutor.Unbind();
                }

                aggregate = CombineLifecycleResults(aggregate, phaseResult);
                if (phaseResult.HasFailures)
                {
                    if (!TryForceScopeFailed(runtimeScopeGraph, handle, scopeBoundary.PlanId, scopeBoundary.Lifecycle.PlanId, currentState, nextState, phaseResult.FirstDiagnostic, out diagnostic))
                    {
                        result = aggregate;
                        return false;
                    }

                    diagnostic = CreateScopeLifecycleDiagnostic(
                        SceneKernelLifecycleCodes.TransitionForcedFailedState,
                        "SceneKernel forced the scope into Failed after lifecycle transition dispatch failed.",
                        transitionPhase.Phase,
                        handle,
                        scopeBoundary.PlanId,
                        scopeBoundary.Lifecycle.PlanId,
                        currentState,
                        nextState,
                        phaseResult.FirstDiagnostic);
                    result = aggregate;
                    ReportDiagnosticIfAvailable(diagnostic);
                    return false;
                }

                if (!runtimeScopeGraph.TryCommitState(handle, transitionPhase.CommitState, out ScopeStateTransitionFailureKind failureKind, out KernelDiagnostic? commitDiagnostic))
                {
                    result = aggregate;
                    diagnostic = CreateScopeLifecycleDiagnostic(
                        SceneKernelLifecycleCodes.ScopeStateCommitFailed,
                        "SceneKernel lifecycle transition could not commit the intermediate scope runtime state after a successful lifecycle phase dispatch.",
                        transitionPhase.Phase,
                        handle,
                        scopeBoundary.PlanId,
                        scopeBoundary.Lifecycle.PlanId,
                        currentState,
                        transitionPhase.CommitState,
                        commitDiagnostic,
                        failureKind.ToString());
                    ReportDiagnosticIfAvailable(diagnostic);
                    return false;
                }
            }

            result = aggregate;
            diagnostic = null;
            return true;
        }

        internal void AttachToApplicationKernel(ApplicationKernel applicationKernel)
        {
            if (applicationKernel == null)
                throw new ArgumentNullException(nameof(applicationKernel));

            if (State != KernelLayerState.Initialized)
                throw new InvalidOperationException("SceneKernel must be initialized before attachment.");

            if (OwnerApplicationKernel != null && !ReferenceEquals(OwnerApplicationKernel, applicationKernel))
                throw new InvalidOperationException("SceneKernel is already attached to another ApplicationKernel.");

            OwnerApplicationKernel = applicationKernel;
        }

        internal void DetachFromApplicationKernel(ApplicationKernel applicationKernel)
        {
            if (applicationKernel == null)
                throw new ArgumentNullException(nameof(applicationKernel));

            if (!ReferenceEquals(OwnerApplicationKernel, applicationKernel))
                throw new InvalidOperationException("SceneKernel can only be detached by its owner ApplicationKernel.");

            OwnerApplicationKernel = null;
        }

        void EnsureOperational()
        {
            if (State != KernelLayerState.Initialized)
                throw new InvalidOperationException("SceneKernel must be initialized before scene composition attachment.");
        }

        void EnsureEntityRegistrationHydrated()
        {
            if (Composition == null)
                return;

            if (!Composition.TryGetBoundary(SceneKernelBoundaryKind.EntityRegistrationPlan, out object? boundary)
                || boundary is not EntityRegistrationPlan entityRegistrationPlan)
            {
                return;
            }

            if (entityRegistrationSourceState == EntityRegistrationSourceState.CompositionPlan)
                return;

            if (entityRegistrationSourceState == EntityRegistrationSourceState.Manual && manualEntityRegistrationTouched)
            {
                throw CreateRegistrationException(new KernelDiagnostic(
                    new DiagnosticCode(SceneKernelEntityRegistrationCodes.CompositionConflict),
                    DiagnosticSeverity.Error,
                    DiagnosticDomain.Kernel,
                    DiagnosticFailureBoundary.Scene,
                    "SceneKernel cannot mix manual entity registration with composition-driven EntityRegistrationPlan hydration.",
                    new DiagnosticContext(null, phase: "SceneKernel/EntityRegistration"),
                    new DiagnosticPayload(new[]
                    {
                        new DiagnosticPayloadEntry("SceneKernelHandle", DiagnosticPayloadValue.FromInt32(Handle.Value)),
                        new DiagnosticPayloadEntry("SceneName", DiagnosticPayloadValue.FromString(SceneName)),
                    })));
            }

            entityTable.Clear();
            entityServiceSlotTable.Clear();
            entityServiceSlotHydrated = false;
            manualEntityRegistrationTouched = false;
            ReadOnlySpan<EntityRegistrationPlanEntry> entries = entityRegistrationPlan.Entries;
            for (int index = 0; index < entries.Length; index++)
            {
                if (!entityTable.TryRegister(Handle, SceneName, entries[index], out _, out KernelDiagnostic? diagnostic))
                    throw CreateRegistrationException(diagnostic!);
            }

            entityRegistrationSourceState = EntityRegistrationSourceState.CompositionPlan;
            if (!TryHydrateEntityServiceSlotsIfPossible(out KernelDiagnostic? serviceDiagnostic))
                throw CreateRegistrationException(serviceDiagnostic!);
        }

        bool TryEnsureEntityServiceSlotsHydrated(KernelRuntimeServiceGraph runtimeServiceGraph, out KernelDiagnostic? diagnostic)
        {
            if (entityServiceSlotHydrated)
            {
                diagnostic = null;
                return true;
            }

            if (entityRegistrationSourceState != EntityRegistrationSourceState.CompositionPlan)
            {
                diagnostic = CreateResolveDiagnostic(
                    SceneKernelServiceResolveCodes.EntityTableNotHydrated,
                    "SceneKernel service-slot hydration requires the composition-driven entity registration table to be hydrated first.",
                    default,
                    default);
                return false;
            }

            if (Composition == null
                || !Composition.TryGetBoundary(SceneKernelBoundaryKind.ServiceRegistrationPlan, out object? serviceRegistrationBoundary)
                || serviceRegistrationBoundary is not ServiceRegistrationPlan serviceRegistrationPlan)
            {
                diagnostic = CreateResolveDiagnostic(
                    SceneKernelServiceResolveCodes.ServiceRegistrationPlanMissing,
                    "SceneKernel service slot creation requires a verified ServiceRegistrationPlan boundary.",
                    default,
                    default);
                return false;
            }

            if (Composition == null
                || !Composition.TryGetBoundary(SceneKernelBoundaryKind.EntityServiceRoutePlan, out object? boundary)
                || boundary is not EntityServiceRoutePlan entityServiceRoutePlan)
            {
                diagnostic = CreateResolveDiagnostic(
                    SceneKernelServiceResolveCodes.RoutePlanMissing,
                    "SceneKernel resolve requires a verified EntityServiceRoutePlan boundary.",
                    default,
                    default);
                return false;
            }

            if (!entityServiceSlotTable.TryHydrate(
                    Handle,
                    SceneName,
                    serviceRegistrationPlan,
                    entityServiceRoutePlan,
                    entityTable,
                    runtimeServiceGraph,
                    out diagnostic))
            {
                return false;
            }

            entityServiceSlotHydrated = true;
            diagnostic = null;
            return true;
        }

        bool TryHydrateEntityServiceSlotsIfPossible(out KernelDiagnostic? diagnostic)
        {
            if (entityServiceSlotHydrated)
            {
                diagnostic = null;
                return true;
            }

            if (Composition == null || entityRegistrationSourceState != EntityRegistrationSourceState.CompositionPlan)
            {
                diagnostic = null;
                return true;
            }

            if (!Composition.TryGetBoundary(SceneKernelBoundaryKind.RuntimeServiceGraph, out object? runtimeServiceGraphBoundary)
                || runtimeServiceGraphBoundary is not KernelRuntimeServiceGraph runtimeServiceGraph)
            {
                diagnostic = null;
                return true;
            }

            if (!Composition.TryGetBoundary(SceneKernelBoundaryKind.ServiceRegistrationPlan, out object? serviceRegistrationBoundary)
                || serviceRegistrationBoundary is not ServiceRegistrationPlan serviceRegistrationPlan)
            {
                diagnostic = null;
                return true;
            }

            if (!Composition.TryGetBoundary(SceneKernelBoundaryKind.EntityServiceRoutePlan, out object? routeBoundary)
                || routeBoundary is not EntityServiceRoutePlan entityServiceRoutePlan)
            {
                diagnostic = null;
                return true;
            }

            if (!entityServiceSlotTable.TryHydrate(
                    Handle,
                    SceneName,
                    serviceRegistrationPlan,
                    entityServiceRoutePlan,
                    entityTable,
                    runtimeServiceGraph,
                    out diagnostic))
            {
                return false;
            }

            entityServiceSlotHydrated = true;
            diagnostic = null;
            return true;
        }

        bool TryGetRuntimeScopeGraph(out KernelRuntimeScopeGraph runtimeScopeGraph, out KernelDiagnostic? diagnostic)
        {
            if (Composition != null
                && Composition.TryGetBoundary(SceneKernelBoundaryKind.RuntimeScopeGraph, out object? boundary)
                && boundary is KernelRuntimeScopeGraph graph)
            {
                runtimeScopeGraph = graph;
                diagnostic = null;
                return true;
            }

            runtimeScopeGraph = null!;
            diagnostic = CreateScopeLifecycleDiagnostic(
                SceneKernelLifecycleCodes.RuntimeScopeGraphMissing,
                "SceneKernel lifecycle dispatch requires a bound runtime scope graph.",
                null,
                default,
                default,
                default,
                ScopeRuntimeState.None,
                ScopeRuntimeState.None);
            return false;
        }

        bool TryGetLifecycleDispatcher(out KernelLifecycleDispatcher lifecycleDispatcher, out KernelDiagnostic? diagnostic)
        {
            if (Composition != null
                && Composition.TryGetBoundary(SceneKernelBoundaryKind.LifecycleDispatcher, out object? boundary)
                && boundary is KernelLifecycleDispatcher dispatcher)
            {
                lifecycleDispatcher = dispatcher;
                diagnostic = null;
                return true;
            }

            lifecycleDispatcher = null!;
            diagnostic = CreateScopeLifecycleDiagnostic(
                SceneKernelLifecycleCodes.LifecycleDispatcherMissing,
                "SceneKernel explicit lifecycle dispatch requires a bound lifecycle dispatcher.",
                null,
                default,
                default,
                default,
                ScopeRuntimeState.None,
                ScopeRuntimeState.None);
            return false;
        }

        bool TryGetLifecyclePlanResolver(out ILifecyclePlanResolver lifecyclePlanResolver, out KernelDiagnostic? diagnostic)
        {
            if (Composition != null
                && Composition.TryGetBoundary(SceneKernelBoundaryKind.LifecyclePlanResolver, out object? boundary)
                && boundary is ILifecyclePlanResolver resolver)
            {
                lifecyclePlanResolver = resolver;
                diagnostic = null;
                return true;
            }

            lifecyclePlanResolver = null!;
            diagnostic = CreateScopeLifecycleDiagnostic(
                SceneKernelLifecycleCodes.LifecyclePlanResolverMissing,
                "SceneKernel scope lifecycle transitions require a bound lifecycle plan resolver.",
                null,
                default,
                default,
                default,
                ScopeRuntimeState.None,
                ScopeRuntimeState.None);
            return false;
        }

        bool TryGetDiagnosticService(out IKernelDiagnosticService diagnosticService)
        {
            diagnosticService = null!;

            if (OwnerApplicationKernel == null)
                return false;

            if (!OwnerApplicationKernel.TryGetBoundary(ApplicationKernelBoundaryKind.Diagnostics, out object? boundary)
                || boundary is not IKernelDiagnosticService resolvedService)
            {
                return false;
            }

            diagnosticService = resolvedService;
            return true;
        }

        void ReportDiagnosticIfAvailable(KernelDiagnostic? diagnostic)
        {
            if (diagnostic == null)
                return;

            if (!TryGetDiagnosticService(out IKernelDiagnosticService diagnosticService))
                return;

            KernelDiagnostic effectiveDiagnostic = diagnostic;
            diagnosticService.Report(in effectiveDiagnostic);
        }

        bool TryGetRuntimeServiceGraph(out KernelRuntimeServiceGraph runtimeServiceGraph, out KernelDiagnostic? diagnostic)
        {
            if (Composition != null
                && Composition.TryGetBoundary(SceneKernelBoundaryKind.RuntimeServiceGraph, out object? boundary)
                && boundary is KernelRuntimeServiceGraph graph)
            {
                runtimeServiceGraph = graph;
                diagnostic = null;
                return true;
            }

            runtimeServiceGraph = null!;
            diagnostic = CreateResolveDiagnostic(
                SceneKernelServiceResolveCodes.RuntimeServiceGraphMissing,
                "SceneKernel resolve requires a bound runtime service graph.",
                default,
                default);
            return false;
        }

        bool TryBuildTransitionSequence(
            ScopeHandle handle,
            ScopeBoundarySnapshot boundary,
            ScopeRuntimeState nextState,
            out SceneKernelLifecycleTransitionPhase[] phases,
            out KernelDiagnostic? diagnostic)
        {
            switch (boundary.State)
            {
                case ScopeRuntimeState.Created when nextState == ScopeRuntimeState.Building:
                    phases = new[] { new SceneKernelLifecycleTransitionPhase(LifecyclePhase.Create, ScopeRuntimeState.Building) };
                    diagnostic = null;
                    return true;
                case ScopeRuntimeState.Created when nextState == ScopeRuntimeState.Built:
                    phases = new[]
                    {
                        new SceneKernelLifecycleTransitionPhase(LifecyclePhase.Create, ScopeRuntimeState.Building),
                        new SceneKernelLifecycleTransitionPhase(LifecyclePhase.Build, ScopeRuntimeState.Built),
                    };
                    diagnostic = null;
                    return true;
                case ScopeRuntimeState.Building when nextState == ScopeRuntimeState.Built:
                    phases = new[] { new SceneKernelLifecycleTransitionPhase(LifecyclePhase.Build, ScopeRuntimeState.Built) };
                    diagnostic = null;
                    return true;
                case ScopeRuntimeState.Built when nextState == ScopeRuntimeState.Acquiring:
                    phases = new[] { new SceneKernelLifecycleTransitionPhase(LifecyclePhase.Acquire, ScopeRuntimeState.Acquiring) };
                    diagnostic = null;
                    return true;
                case ScopeRuntimeState.Built when nextState == ScopeRuntimeState.Active:
                    phases = new[]
                    {
                        new SceneKernelLifecycleTransitionPhase(LifecyclePhase.Acquire, ScopeRuntimeState.Acquiring),
                        new SceneKernelLifecycleTransitionPhase(LifecyclePhase.Activate, ScopeRuntimeState.Active),
                    };
                    diagnostic = null;
                    return true;
                case ScopeRuntimeState.Acquiring when nextState == ScopeRuntimeState.Active:
                    phases = new[] { new SceneKernelLifecycleTransitionPhase(LifecyclePhase.Activate, ScopeRuntimeState.Active) };
                    diagnostic = null;
                    return true;
                case ScopeRuntimeState.Active when nextState == ScopeRuntimeState.Releasing:
                    phases = new[] { new SceneKernelLifecycleTransitionPhase(LifecyclePhase.PreRelease, ScopeRuntimeState.Releasing) };
                    diagnostic = null;
                    return true;
                case ScopeRuntimeState.Releasing when nextState == ScopeRuntimeState.Inactive:
                    phases = new[] { new SceneKernelLifecycleTransitionPhase(LifecyclePhase.Release, ScopeRuntimeState.Inactive) };
                    diagnostic = null;
                    return true;
                case ScopeRuntimeState.Inactive when nextState == ScopeRuntimeState.Acquiring:
                    phases = new[]
                    {
                        new SceneKernelLifecycleTransitionPhase(LifecyclePhase.Reset, ScopeRuntimeState.Inactive),
                        new SceneKernelLifecycleTransitionPhase(LifecyclePhase.Acquire, ScopeRuntimeState.Acquiring),
                    };
                    diagnostic = null;
                    return true;
                default:
                    phases = Array.Empty<SceneKernelLifecycleTransitionPhase>();
                    diagnostic = CreateScopeLifecycleDiagnostic(
                        SceneKernelLifecycleCodes.InvalidPhaseStateMapping,
                        "SceneKernel scope lifecycle transition does not define a phase mapping for the requested runtime state edge.",
                        null,
                        handle,
                        boundary.PlanId,
                        boundary.Lifecycle.PlanId,
                        boundary.State,
                        nextState);
                    return false;
            }
        }

        bool TryForceScopeFailed(
            KernelRuntimeScopeGraph runtimeScopeGraph,
            ScopeHandle handle,
            ScopePlanId scopePlanId,
            LifecyclePlanId lifecyclePlanId,
            ScopeRuntimeState currentState,
            ScopeRuntimeState requestedState,
            KernelDiagnostic? transitionDiagnostic,
            out KernelDiagnostic? diagnostic)
        {
            if (runtimeScopeGraph.TryCommitState(handle, ScopeRuntimeState.Failed, out ScopeStateTransitionFailureKind failureKind, out KernelDiagnostic? commitDiagnostic))
            {
                diagnostic = null;
                return true;
            }

            diagnostic = CreateScopeLifecycleDiagnostic(
                SceneKernelLifecycleCodes.ScopeStateCommitFailed,
                "SceneKernel lifecycle transition failed and could not commit the scope into Failed state.",
                null,
                handle,
                scopePlanId,
                lifecyclePlanId,
                currentState,
                requestedState,
                commitDiagnostic ?? transitionDiagnostic,
                failureKind.ToString());
            return false;
        }

        static LifecycleDispatchResult CombineLifecycleResults(LifecycleDispatchResult aggregate, LifecycleDispatchResult current)
        {
            KernelDiagnostic? firstDiagnostic = aggregate.FirstDiagnostic ?? current.FirstDiagnostic;
            LifecycleRollbackResult rollback = new LifecycleRollbackResult(
                aggregate.Rollback.CompletedStepCount + current.Rollback.CompletedStepCount,
                aggregate.Rollback.AttemptedStepCount + current.Rollback.AttemptedStepCount,
                aggregate.Rollback.SucceededStepCount + current.Rollback.SucceededStepCount,
                aggregate.Rollback.FailedStepCount + current.Rollback.FailedStepCount,
                aggregate.Rollback.StoppedEarly || current.Rollback.StoppedEarly,
                aggregate.Rollback.FirstDiagnostic ?? current.Rollback.FirstDiagnostic);

            return new LifecycleDispatchResult(
                aggregate.AttemptedStepCount + current.AttemptedStepCount,
                aggregate.SucceededStepCount + current.SucceededStepCount,
                aggregate.FailedStepCount + current.FailedStepCount,
                aggregate.StoppedEarly || current.StoppedEarly,
                firstDiagnostic,
                rollback);
        }

        static InvalidOperationException CreateSceneKernelException(KernelDiagnostic diagnostic)
        {
            return new InvalidOperationException(diagnostic.Code.Value + ": " + diagnostic.Message);
        }

        static InvalidOperationException CreateRegistrationException(KernelDiagnostic diagnostic)
        {
            return CreateSceneKernelException(diagnostic);
        }

        KernelDiagnostic CreateResolveDiagnostic(string code, string message, EntityRef entityRef, ServiceId serviceId)
        {
            List<DiagnosticPayloadEntry> payloadEntries = new List<DiagnosticPayloadEntry>(4)
            {
                new DiagnosticPayloadEntry("SceneKernelHandle", DiagnosticPayloadValue.FromInt32(Handle.Value)),
                new DiagnosticPayloadEntry("SceneName", DiagnosticPayloadValue.FromString(SceneName)),
            };

            if (!entityRef.IsEmpty)
                payloadEntries.Add(new DiagnosticPayloadEntry("EntityRef", DiagnosticPayloadValue.FromString(entityRef.Value)));

            if (serviceId.Value > 0)
                payloadEntries.Add(new DiagnosticPayloadEntry("ServiceId", DiagnosticPayloadValue.FromInt32(serviceId.Value)));

            return new KernelDiagnostic(
                new DiagnosticCode(code),
                DiagnosticSeverity.Error,
                DiagnosticDomain.Kernel,
                DiagnosticFailureBoundary.Scene,
                message,
                new DiagnosticContext(null, phase: "SceneKernel/Resolve"),
                new DiagnosticPayload(payloadEntries));
        }

        KernelDiagnostic CreateLifecycleDiagnostic(string code, string message, LifecyclePhase phase)
        {
            return CreateScopeLifecycleDiagnostic(
                code,
                message,
                phase,
                default,
                default,
                default,
                ScopeRuntimeState.None,
                ScopeRuntimeState.None);
        }

        KernelDiagnostic CreateServiceLifecycleDiagnostic(
            string code,
            string message,
            LifecyclePhase phase,
            ServiceId serviceId,
            LifecyclePlanId lifecyclePlanId,
            KernelDiagnostic? innerDiagnostic = null)
        {
            return CreateScopeLifecycleDiagnostic(
                code,
                message,
                phase,
                default,
                default,
                lifecyclePlanId,
                ScopeRuntimeState.None,
                ScopeRuntimeState.None,
                innerDiagnostic,
                serviceId.Value > 0 ? "TargetService=" + serviceId.Value : null);
        }

        KernelDiagnostic CreateDebugMapDiagnostic(string code, string message, RuntimeIdentityRef identity)
        {
            List<DiagnosticPayloadEntry> payloadEntries = new List<DiagnosticPayloadEntry>(4)
            {
                new DiagnosticPayloadEntry("SceneKernelHandle", DiagnosticPayloadValue.FromInt32(Handle.Value)),
                new DiagnosticPayloadEntry("SceneName", DiagnosticPayloadValue.FromString(SceneName)),
                new DiagnosticPayloadEntry("RuntimeIdentity", DiagnosticPayloadValue.FromString(identity.ToString())),
            };

            RuntimeIdentityRef[] runtimeIdentities = identity.IsEmpty ? Array.Empty<RuntimeIdentityRef>() : new[] { identity };
            return new KernelDiagnostic(
                new DiagnosticCode(code),
                DiagnosticSeverity.Error,
                DiagnosticDomain.Kernel,
                DiagnosticFailureBoundary.Scene,
                message,
                new DiagnosticContext(runtimeIdentities, phase: "SceneKernel/DebugMap"),
                new DiagnosticPayload(payloadEntries));
        }

        KernelDiagnostic CreateScopeLifecycleDiagnostic(
            string code,
            string message,
            LifecyclePhase? phase,
            ScopeHandle handle,
            ScopePlanId scopePlanId,
            LifecyclePlanId lifecyclePlanId,
            ScopeRuntimeState currentState,
            ScopeRuntimeState nextState,
            KernelDiagnostic? innerDiagnostic = null,
            string? extraDetail = null)
        {
            List<DiagnosticPayloadEntry> payloadEntries = new List<DiagnosticPayloadEntry>(10)
            {
                new DiagnosticPayloadEntry("SceneKernelHandle", DiagnosticPayloadValue.FromInt32(Handle.Value)),
                new DiagnosticPayloadEntry("SceneName", DiagnosticPayloadValue.FromString(SceneName)),
            };

            List<RuntimeIdentityRef> runtimeIdentities = new List<RuntimeIdentityRef>(3);

            if (scopePlanId.Value > 0)
            {
                payloadEntries.Add(new DiagnosticPayloadEntry("ScopePlanId", DiagnosticPayloadValue.FromInt32(scopePlanId.Value)));
                runtimeIdentities.Add(new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, scopePlanId.Value));
            }

            if (handle.IsValid)
            {
                payloadEntries.Add(new DiagnosticPayloadEntry("ScopeHandle", DiagnosticPayloadValue.FromString(handle.ToString())));
                runtimeIdentities.Add(new RuntimeIdentityRef(RuntimeIdentityKind.ScopeHandle, handle.Index, handle.Generation));
            }

            if (lifecyclePlanId.Value > 0)
            {
                payloadEntries.Add(new DiagnosticPayloadEntry("LifecyclePlanId", DiagnosticPayloadValue.FromInt32(lifecyclePlanId.Value)));
                runtimeIdentities.Add(new RuntimeIdentityRef(RuntimeIdentityKind.LifecyclePlan, lifecyclePlanId.Value));
            }

            if (currentState != ScopeRuntimeState.None)
                payloadEntries.Add(new DiagnosticPayloadEntry("CurrentState", DiagnosticPayloadValue.FromString(currentState.ToString())));

            if (nextState != ScopeRuntimeState.None)
                payloadEntries.Add(new DiagnosticPayloadEntry("RequestedState", DiagnosticPayloadValue.FromString(nextState.ToString())));

            if (!string.IsNullOrWhiteSpace(extraDetail))
                payloadEntries.Add(new DiagnosticPayloadEntry("Detail", DiagnosticPayloadValue.FromString(extraDetail)));

            if (innerDiagnostic != null)
                payloadEntries.Add(new DiagnosticPayloadEntry("InnerDiagnosticCode", DiagnosticPayloadValue.FromString(innerDiagnostic.Code.Value)));

            return new KernelDiagnostic(
                new DiagnosticCode(code),
                DiagnosticSeverity.Error,
                DiagnosticDomain.Kernel,
                DiagnosticFailureBoundary.Scene,
                message,
                new DiagnosticContext(
                    runtimeIdentities.Count == 0 ? null : runtimeIdentities.ToArray(),
                    phase: phase?.ToString() ?? "SceneKernel/Lifecycle"),
                new DiagnosticPayload(payloadEntries));
        }

        static void ValidateSceneCompositionPlacements(IReadOnlyList<KernelComponentPlacementDescriptor> placements)
        {
            if (placements == null)
                throw new ArgumentNullException(nameof(placements));

            for (int index = 0; index < placements.Count; index++)
            {
                KernelComponentPlacementDescriptor placement = placements[index];
                if (placement.PlacementScope != KernelComponentPlacementScope.Scene)
                {
                    throw new ArgumentException(
                        "SceneKernel compositions may only expose Scene placement entries. Invalid placement: " + placement,
                        nameof(placements));
                }
            }
        }

        bool TryCreateServiceActivationContext(ServiceId serviceId, KernelRuntimeServiceSlot serviceSlot, LifecyclePhase phase, LifecyclePlanId lifecyclePlanId, out SceneKernelServiceActivationContext context, out KernelDiagnostic? diagnostic)
        {
            if (Composition == null
                || !Composition.TryGetBoundary(SceneKernelBoundaryKind.ServiceRegistrationPlan, out object? serviceRegistrationBoundary)
                || serviceRegistrationBoundary is not ServiceRegistrationPlan serviceRegistrationPlan)
            {
                context = default;
                diagnostic = CreateServiceLifecycleDiagnostic(
                    SceneKernelLifecycleCodes.ServiceTargetRegistrationMissing,
                    "SceneKernel service activation requires a verified ServiceRegistrationPlan boundary for the requested service target.",
                    phase,
                    serviceId,
                    lifecyclePlanId);
                ReportDiagnosticIfAvailable(diagnostic);
                return false;
            }

            ReadOnlySpan<ServiceRegistrationPlanEntry> entries = serviceRegistrationPlan.Entries;
            for (int index = 0; index < entries.Length; index++)
            {
                ServiceRegistrationPlanEntry registration = entries[index];
                if (registration.ServiceId != serviceId)
                    continue;

                ISceneKernelSpawnBoundary? resolvedSpawnBoundary = null;
                TryGetSpawnBoundary(out resolvedSpawnBoundary);
                context = new SceneKernelServiceActivationContext(this, registration, serviceSlot, resolvedSpawnBoundary);
                diagnostic = null;
                return true;
            }

            context = default;
            diagnostic = CreateServiceLifecycleDiagnostic(
                SceneKernelLifecycleCodes.ServiceTargetRegistrationMissing,
                "SceneKernel service activation could not find verified registration metadata for the requested service target.",
                phase,
                serviceId,
                lifecyclePlanId);
            ReportDiagnosticIfAvailable(diagnostic);
            return false;
        }

        bool TryDispatchServiceLifecycle(LifecycleDispatchStep step, LifecyclePhase phase, bool isRollback, out KernelDiagnostic? diagnostic)
        {
            ServiceId serviceId = step.Target.TargetService;
            if (step.Action != LifecycleActionKind.ServiceMethod)
            {
                diagnostic = CreateServiceLifecycleDiagnostic(
                    SceneKernelLifecycleCodes.UnsupportedServiceAction,
                    "SceneKernel service lifecycle dispatch currently supports only ServiceMethod actions.",
                    phase,
                    serviceId,
                    step.LifecyclePlanId);
                return false;
            }

            if (!TryGetRuntimeServiceGraph(out KernelRuntimeServiceGraph runtimeServiceGraph, out diagnostic))
            {
                ReportDiagnosticIfAvailable(diagnostic);
                return false;
            }

            RuntimeIdentityRef serviceIdentity = new RuntimeIdentityRef(RuntimeIdentityKind.Service, serviceId.Value);
            if (!runtimeServiceGraph.TryGetServiceSlot(serviceIdentity, out KernelRuntimeServiceSlot serviceSlot))
            {
                diagnostic = CreateServiceLifecycleDiagnostic(
                    SceneKernelLifecycleCodes.ServiceTargetSlotMissing,
                    "SceneKernel service lifecycle dispatch could not resolve verified runtime metadata for the requested service target.",
                    phase,
                    serviceId,
                    step.LifecyclePlanId);
                ReportDiagnosticIfAvailable(diagnostic);
                return false;
            }

            if (!serviceLifecycleHandlers.TryGetValue(serviceId, out ISceneKernelServiceLifecycleHandler? handler) || handler == null)
            {
                if (!serviceFactories.TryGetValue(serviceId, out ISceneKernelServiceFactory? factory) || factory == null)
                {
                    diagnostic = CreateServiceLifecycleDiagnostic(
                        SceneKernelLifecycleCodes.ServiceTargetHandlerMissing,
                        "SceneKernel service lifecycle dispatch requires a registered service lifecycle handler or service factory for the requested service target.",
                        phase,
                        serviceId,
                        step.LifecyclePlanId);
                    ReportDiagnosticIfAvailable(diagnostic);
                    return false;
                }

                if (!TryCreateServiceActivationContext(serviceId, serviceSlot, phase, step.LifecyclePlanId, out SceneKernelServiceActivationContext activationContext, out diagnostic))
                    return false;

                if (!factory.TryCreate(in activationContext, out handler, out diagnostic) || handler == null)
                {
                    if (diagnostic == null)
                    {
                        diagnostic = CreateServiceLifecycleDiagnostic(
                            SceneKernelLifecycleCodes.ServiceTargetFactoryFailed,
                            "SceneKernel service factory failed without reporting a diagnostic.",
                            phase,
                            serviceId,
                            step.LifecyclePlanId);
                    }

                    ReportDiagnosticIfAvailable(diagnostic);
                    return false;
                }

                if (handler.ServiceId != serviceId)
                {
                    diagnostic = CreateServiceLifecycleDiagnostic(
                        SceneKernelLifecycleCodes.ServiceTargetFactoryProducedMismatchedHandler,
                        "SceneKernel service factory produced a lifecycle handler whose ServiceId does not match the requested service target.",
                        phase,
                        serviceId,
                        step.LifecyclePlanId);
                    ReportDiagnosticIfAvailable(diagnostic);
                    return false;
                }

                serviceLifecycleHandlers[serviceId] = handler;
            }

            SceneKernelServiceLifecycleContext context = new SceneKernelServiceLifecycleContext(this, serviceSlot, step, phase, isRollback);
            bool succeeded = isRollback
                ? handler.TryRollback(in context, out diagnostic)
                : handler.TryDispatch(in context, out diagnostic);

            if (!succeeded && diagnostic == null)
            {
                diagnostic = CreateServiceLifecycleDiagnostic(
                    SceneKernelLifecycleCodes.ServiceTargetHandlerFailed,
                    "SceneKernel service lifecycle handler failed without reporting a diagnostic.",
                    phase,
                    serviceId,
                    step.LifecyclePlanId);
            }

            if (!succeeded)
                ReportDiagnosticIfAvailable(diagnostic);

            return succeeded;
        }

        readonly struct SceneKernelLifecycleTransitionPhase
        {
            public SceneKernelLifecycleTransitionPhase(LifecyclePhase phase, ScopeRuntimeState commitState)
            {
                Phase = phase;
                CommitState = commitState;
            }

            public LifecyclePhase Phase { get; }

            public ScopeRuntimeState CommitState { get; }
        }

        sealed class SceneKernelLifecycleDispatchExecutor : ILifecycleDispatchExecutor
        {
            readonly SceneKernel owner;
            KernelRuntimeScopeGraph? runtimeScopeGraph;
            LifecyclePhase? phase;
            ScopeHandle currentScopeHandle;
            ScopePlanId currentScopePlanId;
            bool hasBoundScopeContext;

            public SceneKernelLifecycleDispatchExecutor(SceneKernel owner)
            {
                this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            }

            public void Bind(KernelRuntimeScopeGraph runtimeScopeGraph, LifecyclePhase phase, ScopeHandle currentScopeHandle = default, ScopePlanId currentScopePlanId = default)
            {
                this.runtimeScopeGraph = runtimeScopeGraph ?? throw new ArgumentNullException(nameof(runtimeScopeGraph));
                this.phase = phase;
                this.currentScopeHandle = currentScopeHandle;
                this.currentScopePlanId = currentScopePlanId;
                hasBoundScopeContext = !currentScopeHandle.IsDefault;
            }

            public void Unbind()
            {
                runtimeScopeGraph = null;
                phase = null;
                currentScopeHandle = default;
                currentScopePlanId = default;
                hasBoundScopeContext = false;
            }

            public bool TryDispatchService(in LifecycleDispatchStep step, out KernelDiagnostic? diagnostic)
            {
                return owner.TryDispatchServiceLifecycle(step, ResolvePhase(step), isRollback: false, out diagnostic);
            }

            public bool TryDispatchScope(in LifecycleDispatchStep step, out KernelDiagnostic? diagnostic)
            {
                return TryResolveScopeTarget(step, out _, out diagnostic);
            }

            public bool TryDispatchRuntimeQuery(in LifecycleDispatchStep step, out KernelDiagnostic? diagnostic)
            {
                diagnostic = owner.CreateScopeLifecycleDiagnostic(
                    SceneKernelLifecycleCodes.UnsupportedRuntimeQueryTarget,
                    "SceneKernel lifecycle dispatch does not execute runtime-query targets in M4.3.",
                    ResolvePhase(step),
                    default,
                    default,
                    step.LifecyclePlanId,
                    ScopeRuntimeState.None,
                    ScopeRuntimeState.None,
                    null,
                    "TargetRuntimeQuery=" + step.Target.TargetRuntimeQuery.Value);
                return false;
            }

            public bool TryDispatchValueStore(in LifecycleDispatchStep step, out KernelDiagnostic? diagnostic)
            {
                LifecyclePhase resolvedPhase = ResolvePhase(step);
                if (step.Action != LifecycleActionKind.ValueInit)
                {
                    diagnostic = owner.CreateScopeLifecycleDiagnostic(
                        SceneKernelLifecycleCodes.UnsupportedValueStoreAction,
                        "SceneKernel lifecycle dispatch only supports ValueInit actions for value-store targets.",
                        resolvedPhase,
                        currentScopeHandle,
                        currentScopePlanId,
                        step.LifecyclePlanId,
                        ScopeRuntimeState.None,
                        ScopeRuntimeState.None,
                        null,
                        "Action=" + step.Action);
                    return false;
                }

                KernelRuntimeScopeGraph? graph = runtimeScopeGraph;
                if (graph == null)
                {
                    diagnostic = owner.CreateScopeLifecycleDiagnostic(
                        SceneKernelLifecycleCodes.RuntimeScopeGraphMissing,
                        "SceneKernel lifecycle dispatch executor requires a bound runtime scope graph.",
                        resolvedPhase,
                        default,
                        default,
                        step.LifecyclePlanId,
                        ScopeRuntimeState.None,
                        ScopeRuntimeState.None);
                    return false;
                }

                if (!hasBoundScopeContext)
                {
                    diagnostic = owner.CreateScopeLifecycleDiagnostic(
                        SceneKernelLifecycleCodes.ValueStoreScopeContextMissing,
                        "SceneKernel value-store lifecycle dispatch requires a bound scope context.",
                        resolvedPhase,
                        default,
                        default,
                        step.LifecyclePlanId,
                        ScopeRuntimeState.None,
                        ScopeRuntimeState.None,
                        null,
                        "TargetValueStore=" + (step.Target.TargetLocalRef ?? string.Empty));
                    return false;
                }

                if (!graph.TryGetScopeBoundary(currentScopeHandle, out ScopeBoundarySnapshot scopeBoundary, out _, out KernelDiagnostic? scopeDiagnostic))
                {
                    diagnostic = owner.CreateScopeLifecycleDiagnostic(
                        SceneKernelLifecycleCodes.ValueStoreTargetScopeMissing,
                        "SceneKernel value-store lifecycle dispatch could not resolve the bound scope boundary.",
                        resolvedPhase,
                        currentScopeHandle,
                        currentScopePlanId,
                        step.LifecyclePlanId,
                        ScopeRuntimeState.None,
                        ScopeRuntimeState.None,
                        scopeDiagnostic,
                        "LifecycleStepId=" + step.StepId.Value);
                    return false;
                }

                if (scopeBoundary.UnityLink.RuntimeInstanceId <= 0)
                {
                    diagnostic = owner.CreateScopeLifecycleDiagnostic(
                        SceneKernelLifecycleCodes.ValueStoreTargetUnityLinkMissing,
                        "SceneKernel value-store lifecycle dispatch requires the target scope to expose a runtime Unity instance link.",
                        resolvedPhase,
                        currentScopeHandle,
                        scopeBoundary.PlanId,
                        step.LifecyclePlanId,
                        scopeBoundary.State,
                        scopeBoundary.State,
                        null,
                        "TargetValueStore=" + (step.Target.TargetLocalRef ?? string.Empty));
                    return false;
                }

                if (owner.Composition == null
                    || !owner.Composition.TryGetBoundary(SceneKernelBoundaryKind.ValueStore, out object? boundary)
                    || boundary is not ISceneKernelValueStoreBoundary valueStoreBoundary)
                {
                    diagnostic = owner.CreateScopeLifecycleDiagnostic(
                        SceneKernelLifecycleCodes.ValueStoreBoundaryMissing,
                        "SceneKernel value-store lifecycle dispatch requires a bound scene value-store boundary.",
                        resolvedPhase,
                        currentScopeHandle,
                        scopeBoundary.PlanId,
                        step.LifecyclePlanId,
                        scopeBoundary.State,
                        scopeBoundary.State,
                        null,
                        "TargetValueStore=" + (step.Target.TargetLocalRef ?? string.Empty));
                    return false;
                }

                if (!valueStoreBoundary.TryDispatchValueInit(scopeBoundary.UnityLink.RuntimeInstanceId, step.Target.TargetLocalRef ?? string.Empty, resolvedPhase, out string failureReason))
                {
                    diagnostic = owner.CreateScopeLifecycleDiagnostic(
                        SceneKernelLifecycleCodes.ValueStoreDispatchFailed,
                        "SceneKernel value-store lifecycle dispatch failed inside the bound scene value-store boundary.",
                        resolvedPhase,
                        currentScopeHandle,
                        scopeBoundary.PlanId,
                        step.LifecyclePlanId,
                        scopeBoundary.State,
                        scopeBoundary.State,
                        null,
                        failureReason);
                    return false;
                }

                diagnostic = null;
                return true;
            }

            public bool TryRollbackService(in LifecycleDispatchStep step, out KernelDiagnostic? diagnostic)
            {
                return owner.TryDispatchServiceLifecycle(step, ResolvePhase(step), isRollback: true, out diagnostic);
            }

            public bool TryRollbackScope(in LifecycleDispatchStep step, out KernelDiagnostic? diagnostic)
            {
                return TryResolveScopeTarget(step, out _, out diagnostic);
            }

            public bool TryRollbackRuntimeQuery(in LifecycleDispatchStep step, out KernelDiagnostic? diagnostic)
            {
                return TryDispatchRuntimeQuery(step, out diagnostic);
            }

            public bool TryRollbackValueStore(in LifecycleDispatchStep step, out KernelDiagnostic? diagnostic)
            {
                diagnostic = null;
                return true;
            }

            bool TryResolveScopeTarget(LifecycleDispatchStep step, out ScopeHandle handle, out KernelDiagnostic? diagnostic)
            {
                KernelRuntimeScopeGraph? graph = runtimeScopeGraph;
                if (graph == null)
                {
                    handle = default;
                    diagnostic = owner.CreateScopeLifecycleDiagnostic(
                        SceneKernelLifecycleCodes.RuntimeScopeGraphMissing,
                        "SceneKernel lifecycle dispatch executor requires a bound runtime scope graph.",
                        ResolvePhase(step),
                        default,
                        step.Target.TargetScope,
                        step.LifecyclePlanId,
                        ScopeRuntimeState.None,
                        ScopeRuntimeState.None);
                    return false;
                }

                if (!graph.TryGetScopeHandle(step.Target.TargetScope, out handle)
                    || !graph.TryGetScope(handle, out _))
                {
                    diagnostic = owner.CreateScopeLifecycleDiagnostic(
                        SceneKernelLifecycleCodes.UnresolvedScopePlanTarget,
                        "SceneKernel lifecycle dispatch could not resolve the scope lifecycle target to a live scope handle.",
                        ResolvePhase(step),
                        default,
                        step.Target.TargetScope,
                        step.LifecyclePlanId,
                        ScopeRuntimeState.None,
                        ScopeRuntimeState.None,
                        null,
                        "LifecycleStepId=" + step.StepId.Value);
                    return false;
                }

                diagnostic = null;
                return true;
            }

            LifecyclePhase ResolvePhase(LifecycleDispatchStep step)
            {
                return phase ?? step.Phase;
            }
        }
    }

    static class SceneKernelDiagnosticsCodes
    {
        public const string DebugMapMissing = "SCENE_DEBUG_MAP_MISSING";
        public const string InvalidRuntimeIdentity = "SCENE_DEBUG_MAP_INVALID_RUNTIME_IDENTITY";
        public const string SourceLocationMissing = "SCENE_DEBUG_MAP_SOURCE_LOCATION_MISSING";
    }

    static class SceneKernelLifecycleCodes
    {
        public const string DispatchFailed = "SCENE_LIFECYCLE_DISPATCH_FAILED";
        public const string LifecycleDispatcherMissing = "SCENE_LIFECYCLE_DISPATCHER_MISSING";
        public const string LifecyclePlanResolverMissing = "SCENE_LIFECYCLE_PLAN_RESOLVER_MISSING";
        public const string RuntimeScopeGraphMissing = "SCENE_RUNTIME_SCOPE_GRAPH_MISSING";
        public const string UnsupportedServiceTarget = "SCENE_LIFECYCLE_SERVICE_TARGET_UNSUPPORTED";
        public const string UnsupportedServiceAction = "SCENE_LIFECYCLE_SERVICE_ACTION_UNSUPPORTED";
        public const string ServiceTargetSlotMissing = "SCENE_LIFECYCLE_SERVICE_TARGET_SLOT_MISSING";
        public const string ServiceTargetRegistrationMissing = "SCENE_LIFECYCLE_SERVICE_TARGET_REGISTRATION_MISSING";
        public const string ServiceTargetHandlerMissing = "SCENE_LIFECYCLE_SERVICE_TARGET_HANDLER_MISSING";
        public const string ServiceTargetFactoryFailed = "SCENE_LIFECYCLE_SERVICE_TARGET_FACTORY_FAILED";
        public const string ServiceTargetFactoryProducedMismatchedHandler = "SCENE_LIFECYCLE_SERVICE_TARGET_FACTORY_MISMATCH";
        public const string ServiceTargetHandlerFailed = "SCENE_LIFECYCLE_SERVICE_TARGET_HANDLER_FAILED";
        public const string UnsupportedRuntimeQueryTarget = "SCENE_LIFECYCLE_RUNTIME_QUERY_TARGET_UNSUPPORTED";
        public const string UnsupportedValueStoreAction = "SCENE_LIFECYCLE_VALUE_STORE_ACTION_UNSUPPORTED";
        public const string ValueStoreScopeContextMissing = "SCENE_LIFECYCLE_VALUE_STORE_SCOPE_CONTEXT_MISSING";
        public const string ValueStoreTargetScopeMissing = "SCENE_LIFECYCLE_VALUE_STORE_SCOPE_MISSING";
        public const string ValueStoreTargetUnityLinkMissing = "SCENE_LIFECYCLE_VALUE_STORE_UNITY_LINK_MISSING";
        public const string ValueStoreBoundaryMissing = "SCENE_LIFECYCLE_VALUE_STORE_BOUNDARY_MISSING";
        public const string ValueStoreDispatchFailed = "SCENE_LIFECYCLE_VALUE_STORE_DISPATCH_FAILED";
        public const string UnresolvedScopePlanTarget = "SCENE_LIFECYCLE_SCOPE_TARGET_UNRESOLVED";
        public const string InvalidPhaseStateMapping = "SCENE_LIFECYCLE_INVALID_PHASE_STATE_MAPPING";
        public const string ScopeStateCommitFailed = "SCENE_LIFECYCLE_SCOPE_STATE_COMMIT_FAILED";
        public const string TransitionForcedFailedState = "SCENE_LIFECYCLE_TRANSITION_FORCED_FAILED_STATE";
    }

    static class SceneKernelEntityRegistrationCodes
    {
        public const string InvalidEntityRef = "SCENE_ENTITY_REGISTRATION_INVALID_ENTITY_REF";
        public const string DuplicateEntityRef = "SCENE_ENTITY_REGISTRATION_DUPLICATE_ENTITY_REF";
        public const string SourceMissing = "SCENE_ENTITY_REGISTRATION_SOURCE_MISSING";
        public const string UnknownEntityRef = "SCENE_ENTITY_REGISTRATION_UNKNOWN_ENTITY_REF";
        public const string CompositionConflict = "SCENE_ENTITY_REGISTRATION_COMPOSITION_CONFLICT";
    }

    static class SceneKernelServiceResolveCodes
    {
        public const string ServiceRegistrationPlanMissing = "SCENE_SERVICE_REGISTRATION_PLAN_MISSING";
        public const string RoutePlanMissing = "SCENE_SERVICE_ROUTE_PLAN_MISSING";
        public const string RuntimeServiceGraphMissing = "SCENE_RUNTIME_SERVICE_GRAPH_MISSING";
        public const string EntityTableNotHydrated = "SCENE_SERVICE_ROUTE_ENTITY_TABLE_NOT_HYDRATED";
        public const string ServicePlanCountMismatch = "SCENE_SERVICE_SLOT_PLAN_COUNT_MISMATCH";
        public const string ServicePlanEntryMismatch = "SCENE_SERVICE_SLOT_PLAN_ENTRY_MISMATCH";
        public const string ServiceSlotMetadataMismatch = "SCENE_SERVICE_SLOT_METADATA_MISMATCH";
        public const string InvalidEntityRef = "SCENE_SERVICE_ROUTE_INVALID_ENTITY_REF";
        public const string InvalidServiceId = "SCENE_SERVICE_ROUTE_INVALID_SERVICE_ID";
        public const string UnknownEntityRef = "SCENE_SERVICE_ROUTE_UNKNOWN_ENTITY_REF";
        public const string DuplicateRoute = "SCENE_SERVICE_ROUTE_DUPLICATE";
        public const string MissingRoute = "SCENE_SERVICE_ROUTE_MISSING";
        public const string MissingRouteRow = "SCENE_SERVICE_ROUTE_ROW_MISSING";
        public const string UnknownEntitySlot = "SCENE_SERVICE_ROUTE_UNKNOWN_ENTITY_SLOT";
        public const string InvalidServiceSlot = "SCENE_SERVICE_ROUTE_INVALID_SLOT";
        public const string ServiceSlotMismatch = "SCENE_SERVICE_ROUTE_SLOT_MISMATCH";
    }

    public readonly struct SceneKernelEntitySlot
    {
        readonly string[] classificationTags;

        public SceneKernelEntitySlot(int slotIndex, EntityRegistrationPlanEntry entry, bool isRegistered)
        {
            if (slotIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "SceneKernel entity slots must have a non-negative slot index.");

            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
            SlotIndex = slotIndex;
            IsRegistered = isRegistered;
            classificationTags = CopyTags(entry.ClassificationTags);
        }

        public int SlotIndex { get; }

        public EntityRegistrationPlanEntry Entry { get; }

        public EntityRef EntityRef => Entry.EntityRef;

        public ModuleId OwnerModule => Entry.OwnerModule;

        public string DisplayName => Entry.DisplayName;

        public string DebugName => Entry.DebugName;

        public string Metadata => Entry.Metadata;

        public ReadOnlySpan<string> ClassificationTags => classificationTags;

        public SourceLocationIR Source => Entry.Source;

        public bool IsRegistered { get; }

        public SceneKernelEntitySlot Tombstone()
        {
            return new SceneKernelEntitySlot(SlotIndex, Entry, false);
        }

        static string[] CopyTags(ReadOnlySpan<string> source)
        {
            if (source.Length == 0)
                return Array.Empty<string>();

            string[] copy = new string[source.Length];
            for (int index = 0; index < source.Length; index++)
                copy[index] = source[index];

            return copy;
        }
    }

    internal sealed class EntityRegistrationTable
    {
        readonly Dictionary<EntityRef, int> slotIndicesByEntityRef = new Dictionary<EntityRef, int>();
        readonly List<SceneKernelEntitySlot> slots = new List<SceneKernelEntitySlot>();

        public int Count => slotIndicesByEntityRef.Count;

        public int SlotCapacity => slots.Count;

        public void Clear()
        {
            slotIndicesByEntityRef.Clear();
            slots.Clear();
        }

        public bool TryRegister(
            SceneKernelHandle sceneHandle,
            string sceneName,
            EntityRegistrationPlanEntry entry,
            out SceneKernelEntitySlot slot,
            out KernelDiagnostic? diagnostic)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            if (entry.EntityRef.IsEmpty)
            {
                diagnostic = CreateDiagnostic(sceneHandle, sceneName, SceneKernelEntityRegistrationCodes.InvalidEntityRef, "SceneKernel entity registration requires a non-empty EntityRef.", entry, null);
                slot = default;
                return false;
            }

            if (!entry.Source.IsSpecified)
            {
                diagnostic = CreateDiagnostic(sceneHandle, sceneName, SceneKernelEntityRegistrationCodes.SourceMissing, "SceneKernel entity registration requires a specified source location.", entry, null);
                slot = default;
                return false;
            }

            if (slotIndicesByEntityRef.ContainsKey(entry.EntityRef))
            {
                diagnostic = CreateDiagnostic(sceneHandle, sceneName, SceneKernelEntityRegistrationCodes.DuplicateEntityRef, "SceneKernel entity registration rejected a duplicate EntityRef.", entry, null);
                slot = default;
                return false;
            }

            slot = new SceneKernelEntitySlot(slots.Count, entry, true);
            slots.Add(slot);
            slotIndicesByEntityRef.Add(entry.EntityRef, slot.SlotIndex);
            diagnostic = null;
            return true;
        }

        public bool TryUnregister(
            SceneKernelHandle sceneHandle,
            string sceneName,
            EntityRef entityRef,
            out SceneKernelEntitySlot removedSlot,
            out KernelDiagnostic? diagnostic)
        {
            if (entityRef.IsEmpty || !slotIndicesByEntityRef.TryGetValue(entityRef, out int slotIndex))
            {
                removedSlot = default;
                diagnostic = CreateDiagnostic(sceneHandle, sceneName, SceneKernelEntityRegistrationCodes.UnknownEntityRef, "SceneKernel entity unregistration requires an already registered EntityRef.", null, entityRef.IsEmpty ? string.Empty : entityRef.Value);
                return false;
            }

            removedSlot = slots[slotIndex];
            slots[slotIndex] = removedSlot.Tombstone();
            slotIndicesByEntityRef.Remove(entityRef);
            diagnostic = null;
            return true;
        }

        public bool TryGetSlot(EntityRef entityRef, out SceneKernelEntitySlot slot)
        {
            if (entityRef.IsEmpty || !slotIndicesByEntityRef.TryGetValue(entityRef, out int slotIndex))
            {
                slot = default;
                return false;
            }

            slot = slots[slotIndex];
            return slot.IsRegistered;
        }

        static KernelDiagnostic CreateDiagnostic(
            SceneKernelHandle sceneHandle,
            string sceneName,
            string code,
            string message,
            EntityRegistrationPlanEntry? entry,
            string? explicitEntityRef)
        {
            List<DiagnosticPayloadEntry> payloadEntries = new List<DiagnosticPayloadEntry>(6)
            {
                new DiagnosticPayloadEntry("SceneKernelHandle", DiagnosticPayloadValue.FromInt32(sceneHandle.Value)),
                new DiagnosticPayloadEntry("SceneName", DiagnosticPayloadValue.FromString(sceneName)),
            };

            string entityRefValue = explicitEntityRef ?? (entry == null ? string.Empty : entry.EntityRef.Value);
            if (entityRefValue.Length != 0)
                payloadEntries.Add(new DiagnosticPayloadEntry("EntityRef", DiagnosticPayloadValue.FromString(entityRefValue)));

            if (entry != null)
            {
                payloadEntries.Add(new DiagnosticPayloadEntry("OwnerModuleId", DiagnosticPayloadValue.FromInt32(entry.OwnerModule.Value)));
                payloadEntries.Add(new DiagnosticPayloadEntry("Source", DiagnosticPayloadValue.FromString(entry.Source.ToString())));
            }

            return new KernelDiagnostic(
                new DiagnosticCode(code),
                DiagnosticSeverity.Error,
                DiagnosticDomain.Kernel,
                DiagnosticFailureBoundary.Scene,
                message,
                new DiagnosticContext(null, phase: "SceneKernel/EntityRegistration"),
                new DiagnosticPayload(payloadEntries));
        }
    }

    internal sealed class EntityServiceSlotTable
    {
        readonly List<EntityServiceSlotRow?> rowsByEntitySlot = new List<EntityServiceSlotRow?>();
        int count;

        public int Count => count;

        public void Clear()
        {
            rowsByEntitySlot.Clear();
            count = 0;
        }

        public bool TryHydrate(
            SceneKernelHandle sceneHandle,
            string sceneName,
            ServiceRegistrationPlan serviceRegistrationPlan,
            EntityServiceRoutePlan entityServiceRoutePlan,
            EntityRegistrationTable entityTable,
            KernelRuntimeServiceGraph runtimeServiceGraph,
            out KernelDiagnostic? diagnostic)
        {
            if (serviceRegistrationPlan == null)
                throw new ArgumentNullException(nameof(serviceRegistrationPlan));

            if (entityServiceRoutePlan == null)
                throw new ArgumentNullException(nameof(entityServiceRoutePlan));

            if (entityTable == null)
                throw new ArgumentNullException(nameof(entityTable));

            if (runtimeServiceGraph == null)
                throw new ArgumentNullException(nameof(runtimeServiceGraph));

            Clear();

            ReadOnlySpan<ServiceRegistrationPlanEntry> registrationEntries = serviceRegistrationPlan.Entries;
            ReadOnlySpan<EntityServiceRoutePlanEntry> routeEntries = entityServiceRoutePlan.Entries;
            if (registrationEntries.Length != routeEntries.Length)
            {
                diagnostic = CreatePlanDiagnostic(
                    sceneHandle,
                    sceneName,
                    SceneKernelServiceResolveCodes.ServicePlanCountMismatch,
                    "SceneKernel service slot creation requires ServiceRegistrationPlan and EntityServiceRoutePlan to describe the same number of entity-scoped services.",
                    null,
                    null,
                    default,
                    null,
                    registrationEntries.Length,
                    routeEntries.Length);
                return false;
            }

            for (int index = 0; index < registrationEntries.Length; index++)
            {
                ServiceRegistrationPlanEntry registrationEntry = registrationEntries[index];
                EntityServiceRoutePlanEntry routeEntry = routeEntries[index];
                if (!MatchesRegistration(registrationEntry, routeEntry))
                {
                    diagnostic = CreatePlanDiagnostic(
                        sceneHandle,
                        sceneName,
                        SceneKernelServiceResolveCodes.ServicePlanEntryMismatch,
                        "SceneKernel service slot creation requires ServiceRegistrationPlan and EntityServiceRoutePlan to agree on EntityRef + ServiceId ownership.",
                        registrationEntry,
                        routeEntry,
                        default,
                        null,
                        registrationEntries.Length,
                        routeEntries.Length);
                    return false;
                }

                if (!entityTable.TryGetSlot(registrationEntry.EntityRef, out SceneKernelEntitySlot entitySlot))
                {
                    diagnostic = CreatePlanDiagnostic(
                        sceneHandle,
                        sceneName,
                        SceneKernelServiceResolveCodes.UnknownEntityRef,
                        "SceneKernel service slot creation requires every registered service owner EntityRef to exist in the hydrated entity table.",
                        registrationEntry,
                        routeEntry,
                        default,
                        null,
                        null,
                        null);
                    return false;
                }

                int entitySlotIndex = entitySlot.SlotIndex;
                if ((uint)entitySlotIndex >= (uint)entityTable.SlotCapacity)
                {
                    diagnostic = CreatePlanDiagnostic(
                        sceneHandle,
                        sceneName,
                        SceneKernelServiceResolveCodes.UnknownEntitySlot,
                        "SceneKernel service slot creation resolved an entity slot index outside the hydrated scene-local entity table.",
                        registrationEntry,
                        routeEntry,
                        default,
                        null,
                        null,
                        null);
                    return false;
                }

                if (!runtimeServiceGraph.TryGetServiceSlot(routeEntry.ServiceSlotIndex, out KernelRuntimeServiceSlot runtimeSlot))
                {
                    diagnostic = CreatePlanDiagnostic(
                        sceneHandle,
                        sceneName,
                        SceneKernelServiceResolveCodes.InvalidServiceSlot,
                        "SceneKernel service slot creation references a runtime service slot index that does not exist.",
                        registrationEntry,
                        routeEntry,
                        default,
                        routeEntry.ServiceSlotIndex,
                        null,
                        null);
                    return false;
                }

                if (!MatchesRuntimeSlot(registrationEntry, runtimeSlot))
                {
                    diagnostic = CreatePlanDiagnostic(
                        sceneHandle,
                        sceneName,
                        SceneKernelServiceResolveCodes.ServiceSlotMetadataMismatch,
                        "SceneKernel service slot creation found a runtime service slot whose metadata does not match the verified ServiceRegistrationPlan.",
                        registrationEntry,
                        routeEntry,
                        runtimeSlot.ServiceIdentity,
                        routeEntry.ServiceSlotIndex,
                        null,
                        null);
                    return false;
                }

                EnsureRowCapacity(entitySlotIndex + 1);
                EntityServiceSlotRow row = rowsByEntitySlot[entitySlotIndex] ?? new EntityServiceSlotRow(entitySlotIndex, registrationEntry.EntityRef);
                if (!row.TryRegister(registrationEntry.ServiceId, runtimeSlot))
                {
                    diagnostic = CreatePlanDiagnostic(
                        sceneHandle,
                        sceneName,
                        SceneKernelServiceResolveCodes.DuplicateRoute,
                        "SceneKernel service slot creation rejected a duplicate EntityRef + ServiceId mapping.",
                        registrationEntry,
                        routeEntry,
                        runtimeSlot.ServiceIdentity,
                        routeEntry.ServiceSlotIndex,
                        null,
                        null);
                    return false;
                }

                rowsByEntitySlot[entitySlotIndex] = row;
                count++;
            }

            diagnostic = null;
            return true;
        }

        public void RemoveEntity(SceneKernelEntitySlot entitySlot)
        {
            if (!entitySlot.IsRegistered)
                return;

            int slotIndex = entitySlot.SlotIndex;
            if ((uint)slotIndex >= (uint)rowsByEntitySlot.Count)
                return;

            EntityServiceSlotRow? row = rowsByEntitySlot[slotIndex];
            if (row == null || !row.IsAttached)
                return;

            count -= row.Count;
            rowsByEntitySlot[slotIndex] = null;
        }

        public bool TryResolve(
            SceneKernelHandle sceneHandle,
            string sceneName,
            SceneKernelEntitySlot entitySlot,
            ServiceId serviceId,
            out KernelRuntimeServiceSlot slot,
            out KernelDiagnostic? diagnostic)
        {
            EntityRef entityRef = entitySlot.EntityRef;
            if (entityRef.IsEmpty || !entitySlot.IsRegistered)
            {
                slot = default;
                diagnostic = CreateResolveDiagnostic(sceneHandle, sceneName, SceneKernelServiceResolveCodes.InvalidEntityRef, "SceneKernel resolve requires a non-empty EntityRef.", entityRef, serviceId, default, null);
                return false;
            }

            if (serviceId.Value <= 0)
            {
                slot = default;
                diagnostic = CreateResolveDiagnostic(sceneHandle, sceneName, SceneKernelServiceResolveCodes.InvalidServiceId, "SceneKernel resolve requires a non-zero ServiceId.", entityRef, serviceId, default, null);
                return false;
            }

            int entitySlotIndex = entitySlot.SlotIndex;
            if ((uint)entitySlotIndex >= (uint)rowsByEntitySlot.Count)
            {
                slot = default;
                diagnostic = CreateResolveDiagnostic(sceneHandle, sceneName, SceneKernelServiceResolveCodes.MissingRouteRow, "SceneKernel resolve could not find a dense service-slot row for the requested entity slot.", entityRef, serviceId, default, null);
                return false;
            }

            EntityServiceSlotRow? row = rowsByEntitySlot[entitySlotIndex];
            if (row == null || !row.IsAttached)
            {
                slot = default;
                diagnostic = CreateResolveDiagnostic(sceneHandle, sceneName, SceneKernelServiceResolveCodes.MissingRouteRow, "SceneKernel resolve could not find an attached dense service-slot row for the requested entity slot.", entityRef, serviceId, default, null);
                return false;
            }

            if (!row.TryResolve(serviceId, out slot))
            {
                slot = default;
                diagnostic = CreateResolveDiagnostic(sceneHandle, sceneName, SceneKernelServiceResolveCodes.MissingRoute, "SceneKernel resolve could not find a registered service slot for the requested EntityRef + ServiceId.", entityRef, serviceId, default, null);
                return false;
            }

            if (slot.ServiceIdentity.Kind != RuntimeIdentityKind.Service
                || slot.ServiceIdentity.Value != serviceId.Value)
            {
                diagnostic = CreateResolveDiagnostic(sceneHandle, sceneName, SceneKernelServiceResolveCodes.ServiceSlotMismatch, "SceneKernel resolve found a runtime service slot whose identity does not match the requested ServiceId.", entityRef, serviceId, slot.ServiceIdentity, slot.SlotIndex);
                slot = default;
                return false;
            }

            diagnostic = null;
            return true;
        }

        void EnsureRowCapacity(int capacity)
        {
            while (rowsByEntitySlot.Count < capacity)
                rowsByEntitySlot.Add(null);
        }

        static bool MatchesRegistration(ServiceRegistrationPlanEntry registrationEntry, EntityServiceRoutePlanEntry routeEntry)
        {
            return registrationEntry.OwnerModule == routeEntry.OwnerModule
                && registrationEntry.EntityRef == routeEntry.EntityRef
                && registrationEntry.ServiceId == routeEntry.ServiceId;
        }

        static bool MatchesRuntimeSlot(ServiceRegistrationPlanEntry registrationEntry, KernelRuntimeServiceSlot runtimeSlot)
        {
            return runtimeSlot.ServiceIdentity.Kind == RuntimeIdentityKind.Service
                && runtimeSlot.ServiceIdentity.Value == registrationEntry.ServiceId.Value
                && runtimeSlot.OwnerModule == registrationEntry.OwnerModule
                && runtimeSlot.Lifetime == registrationEntry.Lifetime
                && runtimeSlot.Cardinality == registrationEntry.Cardinality
                && runtimeSlot.FactoryKind == registrationEntry.FactoryKind;
        }

        static KernelDiagnostic CreateResolveDiagnostic(
            SceneKernelHandle sceneHandle,
            string sceneName,
            string code,
            string message,
            EntityRef entityRef,
            ServiceId serviceId,
            RuntimeIdentityRef runtimeIdentity,
            int? serviceSlotIndex)
        {
            List<DiagnosticPayloadEntry> payloadEntries = new List<DiagnosticPayloadEntry>(6)
            {
                new DiagnosticPayloadEntry("SceneKernelHandle", DiagnosticPayloadValue.FromInt32(sceneHandle.Value)),
                new DiagnosticPayloadEntry("SceneName", DiagnosticPayloadValue.FromString(sceneName)),
            };

            if (!entityRef.IsEmpty)
                payloadEntries.Add(new DiagnosticPayloadEntry("EntityRef", DiagnosticPayloadValue.FromString(entityRef.Value)));

            if (serviceId.Value > 0)
                payloadEntries.Add(new DiagnosticPayloadEntry("ServiceId", DiagnosticPayloadValue.FromInt32(serviceId.Value)));

            if (serviceSlotIndex.HasValue)
                payloadEntries.Add(new DiagnosticPayloadEntry("ServiceSlotIndex", DiagnosticPayloadValue.FromInt32(serviceSlotIndex.Value)));

            if (runtimeIdentity.Kind != RuntimeIdentityKind.None)
                payloadEntries.Add(new DiagnosticPayloadEntry("RuntimeServiceIdentity", DiagnosticPayloadValue.FromString(runtimeIdentity.ToString())));

            return new KernelDiagnostic(
                new DiagnosticCode(code),
                DiagnosticSeverity.Error,
                DiagnosticDomain.Kernel,
                DiagnosticFailureBoundary.Scene,
                message,
                new DiagnosticContext(null, phase: "SceneKernel/Resolve"),
                new DiagnosticPayload(payloadEntries));
        }

        static KernelDiagnostic CreatePlanDiagnostic(
            SceneKernelHandle sceneHandle,
            string sceneName,
            string code,
            string message,
            ServiceRegistrationPlanEntry? registrationEntry,
            EntityServiceRoutePlanEntry? routeEntry,
            RuntimeIdentityRef runtimeIdentity,
            int? serviceSlotIndex,
            int? registrationCount,
            int? routeCount)
        {
            List<DiagnosticPayloadEntry> payloadEntries = new List<DiagnosticPayloadEntry>(12)
            {
                new DiagnosticPayloadEntry("SceneKernelHandle", DiagnosticPayloadValue.FromInt32(sceneHandle.Value)),
                new DiagnosticPayloadEntry("SceneName", DiagnosticPayloadValue.FromString(sceneName)),
            };

            if (registrationCount.HasValue)
                payloadEntries.Add(new DiagnosticPayloadEntry("RegistrationEntryCount", DiagnosticPayloadValue.FromInt32(registrationCount.Value)));

            if (routeCount.HasValue)
                payloadEntries.Add(new DiagnosticPayloadEntry("RouteEntryCount", DiagnosticPayloadValue.FromInt32(routeCount.Value)));

            if (registrationEntry != null)
            {
                payloadEntries.Add(new DiagnosticPayloadEntry("EntityRef", DiagnosticPayloadValue.FromString(registrationEntry.EntityRef.Value)));
                payloadEntries.Add(new DiagnosticPayloadEntry("ServiceId", DiagnosticPayloadValue.FromInt32(registrationEntry.ServiceId.Value)));
                payloadEntries.Add(new DiagnosticPayloadEntry("OwnerModuleId", DiagnosticPayloadValue.FromInt32(registrationEntry.OwnerModule.Value)));
                payloadEntries.Add(new DiagnosticPayloadEntry("Source", DiagnosticPayloadValue.FromString(registrationEntry.Source.ToString())));
            }

            if (routeEntry != null)
            {
                payloadEntries.Add(new DiagnosticPayloadEntry("RouteOwnerModuleId", DiagnosticPayloadValue.FromInt32(routeEntry.OwnerModule.Value)));
                payloadEntries.Add(new DiagnosticPayloadEntry("RouteSource", DiagnosticPayloadValue.FromString(routeEntry.Source.ToString())));
            }

            if (serviceSlotIndex.HasValue)
                payloadEntries.Add(new DiagnosticPayloadEntry("ServiceSlotIndex", DiagnosticPayloadValue.FromInt32(serviceSlotIndex.Value)));

            if (runtimeIdentity.Kind != RuntimeIdentityKind.None)
                payloadEntries.Add(new DiagnosticPayloadEntry("RuntimeServiceIdentity", DiagnosticPayloadValue.FromString(runtimeIdentity.ToString())));

            return new KernelDiagnostic(
                new DiagnosticCode(code),
                DiagnosticSeverity.Error,
                DiagnosticDomain.Kernel,
                DiagnosticFailureBoundary.Scene,
                message,
                new DiagnosticContext(null, phase: "SceneKernel/ServiceSlotHydration"),
                new DiagnosticPayload(payloadEntries));
        }
    }

    sealed class EntityServiceSlotRow
    {
        ServiceId[] serviceIds;
        KernelRuntimeServiceSlot[] serviceSlots;
        int count;

        public EntityServiceSlotRow(int entitySlotIndex, EntityRef entityRef)
        {
            if (entitySlotIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(entitySlotIndex), entitySlotIndex, "SceneKernel service-slot rows must target a non-negative entity slot index.");

            if (entityRef.IsEmpty)
                throw new ArgumentException("SceneKernel service-slot rows require a non-empty owning EntityRef.", nameof(entityRef));

            EntitySlotIndex = entitySlotIndex;
            EntityRef = entityRef;
            serviceIds = Array.Empty<ServiceId>();
            serviceSlots = Array.Empty<KernelRuntimeServiceSlot>();
        }

        public int EntitySlotIndex { get; }

        public EntityRef EntityRef { get; }

        public bool IsAttached => count > 0;

        public int Count => count;

        public bool TryRegister(ServiceId serviceId, KernelRuntimeServiceSlot slot)
        {
            if (serviceId.Value <= 0 || slot.SlotIndex < 0)
                return false;

            if (count > 0)
            {
                int lastIndex = count - 1;
                int comparison = serviceIds[lastIndex].Value.CompareTo(serviceId.Value);
                if (comparison >= 0)
                    return false;
            }

            EnsureCapacity(count + 1);
            serviceIds[count] = serviceId;
            serviceSlots[count] = slot;
            count++;
            return true;
        }

        public bool TryResolve(ServiceId serviceId, out KernelRuntimeServiceSlot slot)
        {
            int low = 0;
            int high = count - 1;
            while (low <= high)
            {
                int mid = low + ((high - low) >> 1);
                int comparison = serviceIds[mid].Value.CompareTo(serviceId.Value);
                if (comparison == 0)
                {
                    slot = serviceSlots[mid];
                    return true;
                }

                if (comparison < 0)
                    low = mid + 1;
                else
                    high = mid - 1;
            }

            slot = default;
            return false;
        }

        void EnsureCapacity(int capacity)
        {
            if (serviceIds.Length >= capacity)
                return;

            int newCapacity = serviceIds.Length == 0 ? 4 : serviceIds.Length * 2;
            while (newCapacity < capacity)
                newCapacity *= 2;

            Array.Resize(ref serviceIds, newCapacity);
            Array.Resize(ref serviceSlots, newCapacity);
        }
    }
}

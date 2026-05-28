#nullable enable
using System;
using System.Collections.Generic;
using Game.Kernel.Diagnostics;
using Game.Kernel.Generation;
using Game.Kernel.IR;
using Game.Kernel.ScopeGraph;

namespace Game.Kernel.Boot
{
    public enum ScopeCreateMode
    {
        Root = 10,
        Child = 20,
    }

    public enum ScopeRuntimeState
    {
        None = 0,
        Created = 10,
        Building = 20,
        Built = 30,
        Acquiring = 40,
        Active = 50,
        Releasing = 60,
        Inactive = 70,
        Destroying = 80,
        Destroyed = 90,
        Failed = 100,
    }

    public enum ScopeStateTransitionFailureKind
    {
        None = 0,
        MissingScope = 10,
        InvalidTransition = 20,
        TerminalScopeState = 30,
        MissingLifecyclePlan = 40,
    }

    public enum ScopeBoundaryChangeKind
    {
        None = 0,
        Created = 10,
        ParentChanged = 20,
        StateChanged = 30,
        UnityLinkChanged = 40,
        UnityLinkInvalidated = 50,
        Destroyed = 60,
    }

    public enum ScopeBoundaryAccessFailureKind
    {
        None = 0,
        MissingScope = 10,
        InvalidState = 20,
    }

    public static class KernelRuntimeScopeGraphCodes
    {
        public const string ScopeStateTransitionMissingScope = "KERNEL_RUNTIME_SCOPE_STATE_TRANSITION_MISSING_SCOPE";
        public const string ScopeStateTransitionInvalid = "KERNEL_RUNTIME_SCOPE_STATE_TRANSITION_INVALID";
        public const string ScopeStateTransitionTerminalState = "KERNEL_RUNTIME_SCOPE_STATE_TRANSITION_TERMINAL_STATE";
        public const string ScopeStateTransitionMissingLifecyclePlan = "KERNEL_RUNTIME_SCOPE_STATE_TRANSITION_MISSING_LIFECYCLE_PLAN";
        public const string ScopeBoundaryMissingScope = "KERNEL_RUNTIME_SCOPE_BOUNDARY_MISSING_SCOPE";
        public const string ScopeBoundaryInvalidState = "KERNEL_RUNTIME_SCOPE_BOUNDARY_INVALID_STATE";
    }

    public readonly struct ScopeCreateRequest
    {
        public ScopeCreateRequest(ScopePlanId planId, ScopeHandle parent, ScopeCreateMode mode, UnityObjectLink unityLink, SourceLocationId source)
        {
            if (planId.Value == 0)
                throw new ArgumentException("Scope creation requests must provide a non-zero plan identity.", nameof(planId));

            if (mode != ScopeCreateMode.Root && mode != ScopeCreateMode.Child)
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Scope creation requests must use a defined creation mode.");

            if (source.Value == 0)
                throw new ArgumentException("Scope creation requests must provide a non-zero source location identity.", nameof(source));

            if (mode == ScopeCreateMode.Root && !parent.IsDefault)
                throw new ArgumentException("Root scope creation requests must not specify a parent handle.", nameof(parent));

            if (mode == ScopeCreateMode.Child && parent.IsDefault)
                throw new ArgumentException("Child scope creation requests must specify a parent handle.", nameof(parent));

            PlanId = planId;
            Parent = parent;
            Mode = mode;
            UnityLink = unityLink;
            Source = source;
        }

        public ScopePlanId PlanId { get; }

        public ScopeHandle Parent { get; }

        public ScopeCreateMode Mode { get; }

        public UnityObjectLink UnityLink { get; }

        public SourceLocationId Source { get; }

        public bool IsRootCreation => Mode == ScopeCreateMode.Root;
    }

    public readonly struct ScopeRuntimeSnapshot
    {
        public ScopeRuntimeSnapshot(
            ScopeHandle handle,
            ScopePlanId planId,
            ScopeAuthoringId authoringId,
            ScopeHandle parent,
            ScopeKind kind,
            ScopeRuntimeState state,
            UnityObjectLink unityLink)
        {
            Handle = handle;
            PlanId = planId;
            AuthoringId = authoringId;
            Parent = parent;
            Kind = kind;
            State = state;
            UnityLink = unityLink;
        }

        public ScopeHandle Handle { get; }

        public ScopePlanId PlanId { get; }

        public ScopeAuthoringId AuthoringId { get; }

        public ScopeHandle Parent { get; }

        public ScopeKind Kind { get; }

        public ScopeRuntimeState State { get; }

        public UnityObjectLink UnityLink { get; }

        public int Generation => Handle.Generation;

        public bool IsRoot => Parent.IsDefault;
    }

    public readonly struct ScopeBoundarySnapshot
    {
        readonly ScopeValueInitRefIR[] valueInitPlans;

        public ScopeBoundarySnapshot(
            ScopeHandle handle,
            ScopePlanId planId,
            ScopeAuthoringId authoringId,
            ModuleId ownerModule,
            SourceLocationId source,
            ScopeHandle parent,
            ScopeKind kind,
            ScopeRuntimeState state,
            ScopeServiceBoundaryIR serviceBoundary,
            ScopeValueInitRefIR[] valueInitPlans,
            LifecyclePlanRefIR lifecycle,
            UnityObjectLink unityLink,
            ScopeBoundaryChangeKind lastChangeKind,
            int boundaryRevision)
        {
            Handle = handle;
            PlanId = planId;
            AuthoringId = authoringId;
            OwnerModule = ownerModule;
            Source = source;
            Parent = parent;
            Kind = kind;
            State = state;
            ServiceBoundary = serviceBoundary;
            this.valueInitPlans = valueInitPlans ?? Array.Empty<ScopeValueInitRefIR>();
            Lifecycle = lifecycle;
            UnityLink = unityLink;
            LastChangeKind = lastChangeKind;
            BoundaryRevision = boundaryRevision;
        }

        public ScopeHandle Handle { get; }

        public ScopePlanId PlanId { get; }

        public ScopeAuthoringId AuthoringId { get; }

        public ModuleId OwnerModule { get; }

        public SourceLocationId Source { get; }

        public ScopeHandle Parent { get; }

        public ScopeKind Kind { get; }

        public ScopeRuntimeState State { get; }

        public ScopeServiceBoundaryIR ServiceBoundary { get; }

        public ReadOnlySpan<ScopeValueInitRefIR> ValueInitPlans => valueInitPlans;

        public LifecyclePlanRefIR Lifecycle { get; }

        public UnityObjectLink UnityLink { get; }

        public ScopeBoundaryChangeKind LastChangeKind { get; }

        public int BoundaryRevision { get; }

        public bool IsRoot => Parent.IsDefault;
    }

    public readonly struct ScopeBoundaryChangeRecord
    {
        public ScopeBoundaryChangeRecord(ScopeBoundarySnapshot snapshot)
        {
            Snapshot = snapshot;
            ChangeKind = snapshot.LastChangeKind;
        }

        public ScopeBoundarySnapshot Snapshot { get; }

        public ScopeBoundaryChangeKind ChangeKind { get; }

        public ScopeHandle Handle => Snapshot.Handle;

        public int BoundaryRevision => Snapshot.BoundaryRevision;
    }

    public readonly struct ScopeLifecycleTransitionRequest
    {
        public ScopeLifecycleTransitionRequest(ScopeHandle handle, LifecyclePlanId lifecyclePlanId, ScopeRuntimeState currentState, ScopeRuntimeState nextState)
        {
            Handle = handle;
            LifecyclePlanId = lifecyclePlanId;
            CurrentState = currentState;
            NextState = nextState;
        }

        public ScopeHandle Handle { get; }

        public LifecyclePlanId LifecyclePlanId { get; }

        public ScopeRuntimeState CurrentState { get; }

        public ScopeRuntimeState NextState { get; }
    }

    internal sealed class KernelScopeGraphRuntime
    {
        readonly List<RuntimeIdentityRef> currentRootScopeIdentities = new();
        readonly List<ScopeHandle> rootHandles = new();
        readonly HashSet<ScopeHandle> rootHandleIndex = new();
        readonly ScopeInstanceTable instanceTable = new();
        readonly ScopeParentChildTable parentChildTable = new();
        readonly Dictionary<ScopeHandle, List<ScopeBoundaryChangeRecord>> boundaryChangeJournal = new();
        readonly Dictionary<ScopeHandle, List<ScopeLifecycleTransitionRequest>> lifecycleTransitionJournal = new();
        readonly Dictionary<ScopePlanId, ScopeIR> scopesByPlanId = new();
        readonly Dictionary<ScopePlanId, ScopeHandle> liveHandlesByPlanId = new();
        readonly Dictionary<ValueInitPlanId, ValueInitPlanIR> valueInitPlansById = new();
        readonly Dictionary<ScopeAuthoringId, ScopeRuntimeNode> scopesByAuthoringId = new();
        readonly ILifecyclePlanResolver? lifecyclePlanResolver;

        public KernelScopeGraphRuntime(
            ScopeGraphPlan scopeGraphPlan,
            ReadOnlySpan<RuntimeIdentityRef> rootScopeIdentities,
            ILifecyclePlanResolver? lifecyclePlanResolver = null)
        {
            if (scopeGraphPlan == null)
                throw new ArgumentNullException(nameof(scopeGraphPlan));

            this.lifecyclePlanResolver = lifecyclePlanResolver;
            BuildPlanIndex(scopeGraphPlan);
            BuildFromPlan(scopeGraphPlan);
            ValidateRootScopeIdentities(rootScopeIdentities);
        }

        public IReadOnlyList<RuntimeIdentityRef> RootScopeIdentities => currentRootScopeIdentities;

        public IReadOnlyList<ScopeHandle> RootScopeHandles => rootHandles;

        public int RootScopeCount => rootHandles.Count;

        public bool IsEmpty => rootHandles.Count == 0;

        public bool TryGetScope(ScopeHandle handle, out ScopeRuntimeSnapshot snapshot)
        {
            snapshot = default;

            if (!instanceTable.TryGetNode(handle, out ScopeRuntimeNode? node))
                return false;

            if (!TryResolveParentHandle(handle, out ScopeHandle parentHandle))
                return false;

            snapshot = node.ToSnapshot(parentHandle);
            return true;
        }

        public bool TryGetScopeBoundary(ScopeHandle handle, out ScopeBoundarySnapshot snapshot)
        {
            return TryGetScopeBoundary(handle, out snapshot, out _);
        }

        public bool TryGetScopeBoundary(
            ScopeHandle handle,
            out ScopeBoundarySnapshot snapshot,
            out ScopeBoundaryAccessFailureKind failureKind,
            out KernelDiagnostic? diagnostic)
        {
            snapshot = default;
            diagnostic = null;
            failureKind = ScopeBoundaryAccessFailureKind.None;

            if (!instanceTable.TryGetNode(handle, out ScopeRuntimeNode? node))
            {
                failureKind = ScopeBoundaryAccessFailureKind.MissingScope;
                diagnostic = CreateBoundaryAccessDiagnostic(handle, failureKind, null);
                return false;
            }

            if (!CanParticipateInTopology(node.State))
            {
                failureKind = ScopeBoundaryAccessFailureKind.InvalidState;
                diagnostic = CreateBoundaryAccessDiagnostic(handle, failureKind, node);
                return false;
            }

            if (!TryResolveParentHandle(handle, out ScopeHandle parentHandle))
            {
                failureKind = ScopeBoundaryAccessFailureKind.InvalidState;
                diagnostic = CreateBoundaryAccessDiagnostic(handle, failureKind, node);
                return false;
            }

            snapshot = node.ToBoundarySnapshot(parentHandle);
            return true;
        }

        public bool TryGetScopeBoundaryChanges(ScopeHandle handle, out IReadOnlyList<ScopeBoundaryChangeRecord> changes)
        {
            if (!boundaryChangeJournal.TryGetValue(handle, out List<ScopeBoundaryChangeRecord>? records))
            {
                changes = Array.Empty<ScopeBoundaryChangeRecord>();
                return false;
            }

            changes = records;
            return true;
        }

        public bool TryGetScopeValueInitPlans(ScopeHandle handle, out IReadOnlyList<ValueInitPlanIR> valueInitPlans)
        {
            valueInitPlans = Array.Empty<ValueInitPlanIR>();

            if (!instanceTable.TryGetNode(handle, out ScopeRuntimeNode? node) || node == null)
                return false;

            ScopeValueInitRefIR[] valueInitRefs = node.ValueInitPlans;
            if (valueInitRefs.Length == 0)
                return true;

            ValueInitPlanIR[] resolvedPlans = new ValueInitPlanIR[valueInitRefs.Length];
            for (int index = 0; index < valueInitRefs.Length; index++)
            {
                if (!valueInitPlansById.TryGetValue(valueInitRefs[index].PlanId, out ValueInitPlanIR? valueInitPlan))
                    throw new InvalidOperationException("Kernel scope graph encountered a scope value init ref that is missing from the verified scope graph plan.");

                resolvedPlans[index] = valueInitPlan;
            }

            Array.Sort(resolvedPlans, static (left, right) =>
            {
                int result = left.Order.CompareTo(right.Order);
                return result != 0 ? result : left.PlanId.Value.CompareTo(right.PlanId.Value);
            });
            valueInitPlans = resolvedPlans;
            return true;
        }

        public bool TryGetLifecycleTransitionRequests(ScopeHandle handle, out IReadOnlyList<ScopeLifecycleTransitionRequest> requests)
        {
            if (!lifecycleTransitionJournal.TryGetValue(handle, out List<ScopeLifecycleTransitionRequest>? records))
            {
                requests = Array.Empty<ScopeLifecycleTransitionRequest>();
                return false;
            }

            requests = records;
            return true;
        }

        public bool TryGetScopeHandle(ScopePlanId planId, out ScopeHandle handle)
        {
            return liveHandlesByPlanId.TryGetValue(planId, out handle);
        }

        public bool TryGetScopeBoundary(
            ScopeHandle handle,
            out ScopeBoundarySnapshot snapshot,
            out ScopeBoundaryAccessFailureKind failureKind)
        {
            return TryGetScopeBoundary(handle, out snapshot, out failureKind, out _);
        }

        public bool TryGetChildHandles(ScopeHandle handle, out IReadOnlyList<ScopeHandle> childHandles)
        {
            childHandles = Array.Empty<ScopeHandle>();

            if (!instanceTable.TryGetNode(handle, out _))
                return false;

            childHandles = parentChildTable.GetChildHandles(handle);
            return true;
        }

        public ScopeHandle CreateScope(ScopeCreateRequest request)
        {
            ScopeIR scope = GetScopeDefinition(request.PlanId);

            if (request.IsRootCreation)
            {
                if (!scope.ParentAuthoringId.IsDefault)
                    throw new InvalidOperationException("Root scope creation requires a root-eligible scope plan.");

                return CreateNode(scope.PlanId, scope.AuthoringId, scope.Kind, scope.ParentAuthoringId, default, request.UnityLink, registerAsRoot: true);
            }

            if (scope.ParentAuthoringId.IsDefault)
                throw new InvalidOperationException("Child scope creation requires a scope plan with an explicit parent authoring identity.");

            if (!instanceTable.TryGetNode(request.Parent, out ScopeRuntimeNode? parentNode))
                throw new InvalidOperationException("Child scope creation requires a valid parent scope handle.");

            if (!CanParticipateInTopology(parentNode.State))
                throw new InvalidOperationException("Child scope creation requires a live parent scope handle.");

            if (parentNode.AuthoringId != scope.ParentAuthoringId)
                throw new InvalidOperationException("Child scope creation parent does not match the verified parent authoring identity.");

            return CreateNode(scope.PlanId, scope.AuthoringId, scope.Kind, scope.ParentAuthoringId, request.Parent, request.UnityLink, registerAsRoot: false);
        }

        public bool TryDestroyScope(ScopeHandle handle)
        {
            if (!instanceTable.TryGetNode(handle, out ScopeRuntimeNode? node))
                return false;

            if (node.State == ScopeRuntimeState.Destroyed || node.State == ScopeRuntimeState.Destroying)
                return false;

            ScopeHandle[] childHandles = parentChildTable.GetChildHandlesSnapshot(handle);
            for (int index = childHandles.Length - 1; index >= 0; index--)
            {
                if (!TryDestroyScope(childHandles[index]))
                    throw new InvalidOperationException("Kernel scope graph destroy failed to destroy a child scope.");
            }

            if (!TryResolveParentHandle(handle, out ScopeHandle parentHandle))
                parentHandle = default;

            node.State = ScopeRuntimeState.Destroying;
            node.MarkBoundaryChange(ScopeBoundaryChangeKind.UnityLinkInvalidated);
            node.UnityLink = default;
            RecordBoundaryChange(node, parentHandle);
            RemoveFromParentAndRootCollections(handle, node.PlanId);
            node.State = ScopeRuntimeState.Destroyed;
            node.MarkBoundaryChange(ScopeBoundaryChangeKind.Destroyed);
            RecordBoundaryChange(node, parentHandle);

            if (!instanceTable.TryRelease(handle))
                throw new InvalidOperationException("Kernel scope graph destroy failed to release a live handle.");

            liveHandlesByPlanId.Remove(node.PlanId);

            return true;
        }

        public bool TryDetachScope(ScopeHandle handle)
        {
            if (!instanceTable.TryGetNode(handle, out ScopeRuntimeNode? node))
                return false;

            if (!CanParticipateInTopology(node.State))
                return false;

            if (!parentChildTable.TryDetach(handle, out _))
                return rootHandleIndex.Contains(handle);

            AddRoot(handle, node.PlanId);
            node.MarkBoundaryChange(ScopeBoundaryChangeKind.ParentChanged);
            RecordBoundaryChange(node, default);
            return true;
        }

        public bool TryReparentScope(ScopeHandle childHandle, ScopeHandle newParentHandle)
        {
            if (newParentHandle.IsDefault)
                return TryDetachScope(childHandle);

            if (!instanceTable.TryGetNode(childHandle, out ScopeRuntimeNode? childNode) || !instanceTable.TryGetNode(newParentHandle, out ScopeRuntimeNode? parentNode))
                return false;

            if (!CanParticipateInTopology(childNode.State) || !CanParticipateInTopology(parentNode.State))
                return false;

            if (childHandle == newParentHandle)
                return false;

            if (parentChildTable.WouldIntroduceCycle(childHandle, newParentHandle))
                return false;

            RemoveRoot(childHandle, childNode.PlanId);
            parentChildTable.SetParent(childHandle, newParentHandle);
            childNode.MarkBoundaryChange(ScopeBoundaryChangeKind.ParentChanged);
            RecordBoundaryChange(childNode, newParentHandle);
            return true;
        }

        public bool TrySetState(ScopeHandle handle, ScopeRuntimeState nextState)
        {
            return TrySetState(handle, nextState, out _);
        }

        public bool TrySetState(ScopeHandle handle, ScopeRuntimeState nextState, out ScopeStateTransitionFailureKind failureKind)
        {
            return TrySetState(handle, nextState, out failureKind, out _);
        }

        public bool TrySetState(
            ScopeHandle handle,
            ScopeRuntimeState nextState,
            out ScopeStateTransitionFailureKind failureKind,
            out KernelDiagnostic? diagnostic)
        {
            return TrySetStateCore(handle, nextState, recordLifecycleTransition: true, out failureKind, out diagnostic);
        }

        public bool TryCommitState(
            ScopeHandle handle,
            ScopeRuntimeState nextState,
            out ScopeStateTransitionFailureKind failureKind,
            out KernelDiagnostic? diagnostic)
        {
            return TrySetStateCore(handle, nextState, recordLifecycleTransition: false, out failureKind, out diagnostic);
        }

        bool TrySetStateCore(
            ScopeHandle handle,
            ScopeRuntimeState nextState,
            bool recordLifecycleTransition,
            out ScopeStateTransitionFailureKind failureKind,
            out KernelDiagnostic? diagnostic)
        {
            diagnostic = null;
            failureKind = ScopeStateTransitionFailureKind.None;

            if (!instanceTable.TryGetNode(handle, out ScopeRuntimeNode? node))
            {
                failureKind = ScopeStateTransitionFailureKind.MissingScope;
                diagnostic = CreateStateTransitionDiagnostic(handle, default, nextState, failureKind, null);
                return false;
            }

            if (!IsValidTransition(node.State, nextState, out failureKind))
            {
                diagnostic = CreateStateTransitionDiagnostic(handle, node.State, nextState, failureKind, node);
                return false;
            }

            ScopeRuntimeState currentState = node.State;

            if (recordLifecycleTransition && currentState != nextState && lifecyclePlanResolver != null)
            {
                if (!lifecyclePlanResolver.TryGetLifecycleDispatcher(node.Lifecycle.PlanId, out _))
                {
                    failureKind = ScopeStateTransitionFailureKind.MissingLifecyclePlan;
                    diagnostic = CreateLifecyclePlanDiagnostic(handle, currentState, nextState, node);
                    return false;
                }

                RecordLifecycleTransitionRequest(node, currentState, nextState);
            }

            node.State = nextState;
            if (currentState != nextState)
            {
                node.MarkBoundaryChange(ScopeBoundaryChangeKind.StateChanged);
                RecordBoundaryChange(node, ResolveParentHandleOrDefault(handle));
            }
            return true;
        }

        public bool TrySetUnityLink(ScopeHandle handle, UnityObjectLink unityLink)
        {
            return TrySetUnityLink(handle, unityLink, out _);
        }

        public bool TrySetUnityLink(ScopeHandle handle, UnityObjectLink unityLink, out KernelDiagnostic? diagnostic)
        {
            diagnostic = null;

            if (!instanceTable.TryGetNode(handle, out ScopeRuntimeNode? node))
            {
                diagnostic = CreateBoundaryAccessDiagnostic(handle, ScopeBoundaryAccessFailureKind.MissingScope, null);
                return false;
            }

            if (!CanParticipateInTopology(node.State))
            {
                diagnostic = CreateBoundaryAccessDiagnostic(handle, ScopeBoundaryAccessFailureKind.InvalidState, node);
                return false;
            }

            if (node.UnityLink == unityLink)
                return true;

            node.UnityLink = unityLink;
            node.MarkBoundaryChange(ScopeBoundaryChangeKind.UnityLinkChanged);
            RecordBoundaryChange(node, ResolveParentHandleOrDefault(handle));
            return true;
        }

        static bool IsValidTransition(ScopeRuntimeState currentState, ScopeRuntimeState nextState, out ScopeStateTransitionFailureKind failureKind)
        {
            failureKind = ScopeStateTransitionFailureKind.None;

            if (currentState == nextState)
                return true;

            if (currentState == ScopeRuntimeState.Destroyed || currentState == ScopeRuntimeState.Failed)
            {
                failureKind = ScopeStateTransitionFailureKind.TerminalScopeState;
                return false;
            }

            if (nextState == ScopeRuntimeState.Failed)
                return currentState != ScopeRuntimeState.None;

            if (nextState == ScopeRuntimeState.Destroying)
                return currentState != ScopeRuntimeState.None;

            if (currentState == ScopeRuntimeState.None)
            {
                failureKind = ScopeStateTransitionFailureKind.InvalidTransition;
                return nextState == ScopeRuntimeState.Created;
            }

            if (currentState == ScopeRuntimeState.Created)
                return nextState == ScopeRuntimeState.Building || nextState == ScopeRuntimeState.Built;

            if (currentState == ScopeRuntimeState.Building)
                return nextState == ScopeRuntimeState.Built;

            if (currentState == ScopeRuntimeState.Built)
                return nextState == ScopeRuntimeState.Acquiring || nextState == ScopeRuntimeState.Active;

            if (currentState == ScopeRuntimeState.Acquiring)
                return nextState == ScopeRuntimeState.Active;

            if (currentState == ScopeRuntimeState.Active)
                return nextState == ScopeRuntimeState.Releasing;

            if (currentState == ScopeRuntimeState.Releasing)
                return nextState == ScopeRuntimeState.Inactive;

            if (currentState == ScopeRuntimeState.Inactive)
                return nextState == ScopeRuntimeState.Acquiring;

            if (currentState == ScopeRuntimeState.Destroying)
                return nextState == ScopeRuntimeState.Destroyed;

            failureKind = ScopeStateTransitionFailureKind.InvalidTransition;
            return false;
        }

        static bool CanParticipateInTopology(ScopeRuntimeState state)
        {
            return state != ScopeRuntimeState.Destroying
                && state != ScopeRuntimeState.Destroyed
                && state != ScopeRuntimeState.Failed;
        }

        static KernelDiagnostic CreateStateTransitionDiagnostic(
            ScopeHandle handle,
            ScopeRuntimeState currentState,
            ScopeRuntimeState nextState,
            ScopeStateTransitionFailureKind failureKind,
            ScopeRuntimeNode? node)
        {
            DiagnosticCode code = failureKind switch
            {
                ScopeStateTransitionFailureKind.MissingScope => new DiagnosticCode(KernelRuntimeScopeGraphCodes.ScopeStateTransitionMissingScope),
                ScopeStateTransitionFailureKind.TerminalScopeState => new DiagnosticCode(KernelRuntimeScopeGraphCodes.ScopeStateTransitionTerminalState),
                ScopeStateTransitionFailureKind.MissingLifecyclePlan => new DiagnosticCode(KernelRuntimeScopeGraphCodes.ScopeStateTransitionMissingLifecyclePlan),
                _ => new DiagnosticCode(KernelRuntimeScopeGraphCodes.ScopeStateTransitionInvalid),
            };

            DiagnosticPayloadEntry[] payloadEntries = node == null
                ? new[]
                {
                    new DiagnosticPayloadEntry("ScopeHandle", DiagnosticPayloadValue.FromString(handle.ToString())),
                    new DiagnosticPayloadEntry("RequestedState", DiagnosticPayloadValue.FromString(nextState.ToString())),
                    new DiagnosticPayloadEntry("FailureKind", DiagnosticPayloadValue.FromString(failureKind.ToString())),
                }
                : new[]
                {
                    new DiagnosticPayloadEntry("ScopeHandle", DiagnosticPayloadValue.FromString(handle.ToString())),
                    new DiagnosticPayloadEntry("ScopePlanId", DiagnosticPayloadValue.FromInt32(node.PlanId.Value)),
                    new DiagnosticPayloadEntry("ScopeAuthoringId", DiagnosticPayloadValue.FromInt32(node.AuthoringId.Value)),
                    new DiagnosticPayloadEntry("CurrentState", DiagnosticPayloadValue.FromString(currentState.ToString())),
                    new DiagnosticPayloadEntry("RequestedState", DiagnosticPayloadValue.FromString(nextState.ToString())),
                    new DiagnosticPayloadEntry("FailureKind", DiagnosticPayloadValue.FromString(failureKind.ToString())),
                };

            RuntimeIdentityRef[] runtimeIdentities = node == null
                ? new[] { new RuntimeIdentityRef(RuntimeIdentityKind.ScopeHandle, handle.Index, handle.Generation) }
                : new[]
                {
                    new RuntimeIdentityRef(RuntimeIdentityKind.ScopeHandle, handle.Index, handle.Generation),
                    new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, node.PlanId.Value),
                    new RuntimeIdentityRef(RuntimeIdentityKind.ScopeAuthoring, node.AuthoringId.Value),
                };

            DiagnosticContext context = new DiagnosticContext(runtimeIdentities);
            return new KernelDiagnostic(
                code,
                DiagnosticSeverity.Error,
                DiagnosticDomain.ScopeGraph,
                DiagnosticFailureBoundary.Scope,
                message: "Scope state transition failed.",
                context: context,
                payload: new DiagnosticPayload(payloadEntries));
        }

        static KernelDiagnostic CreateLifecyclePlanDiagnostic(
            ScopeHandle handle,
            ScopeRuntimeState currentState,
            ScopeRuntimeState nextState,
            ScopeRuntimeNode node)
        {
            return new KernelDiagnostic(
                new DiagnosticCode(KernelRuntimeScopeGraphCodes.ScopeStateTransitionMissingLifecyclePlan),
                DiagnosticSeverity.Error,
                DiagnosticDomain.ScopeGraph,
                DiagnosticFailureBoundary.Scope,
                message: "Scope state transition could not resolve a verified lifecycle plan.",
                context: new DiagnosticContext(
                    new[]
                    {
                        new RuntimeIdentityRef(RuntimeIdentityKind.ScopeHandle, handle.Index, handle.Generation),
                        new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, node.PlanId.Value),
                        new RuntimeIdentityRef(RuntimeIdentityKind.ScopeAuthoring, node.AuthoringId.Value),
                        new RuntimeIdentityRef(RuntimeIdentityKind.LifecyclePlan, node.Lifecycle.PlanId.Value),
                    }),
                payload: new DiagnosticPayload(new[]
                {
                    new DiagnosticPayloadEntry("ScopeHandle", DiagnosticPayloadValue.FromString(handle.ToString())),
                    new DiagnosticPayloadEntry("ScopePlanId", DiagnosticPayloadValue.FromInt32(node.PlanId.Value)),
                    new DiagnosticPayloadEntry("LifecyclePlanId", DiagnosticPayloadValue.FromInt32(node.Lifecycle.PlanId.Value)),
                    new DiagnosticPayloadEntry("CurrentState", DiagnosticPayloadValue.FromString(currentState.ToString())),
                    new DiagnosticPayloadEntry("RequestedState", DiagnosticPayloadValue.FromString(nextState.ToString())),
                }));
        }

        void RecordLifecycleTransitionRequest(ScopeRuntimeNode node, ScopeRuntimeState currentState, ScopeRuntimeState nextState)
        {
            if (!lifecycleTransitionJournal.TryGetValue(node.Handle, out List<ScopeLifecycleTransitionRequest>? requests))
            {
                requests = new List<ScopeLifecycleTransitionRequest>(4);
                lifecycleTransitionJournal.Add(node.Handle, requests);
            }

            requests.Add(new ScopeLifecycleTransitionRequest(node.Handle, node.Lifecycle.PlanId, currentState, nextState));
        }

        static KernelDiagnostic CreateBoundaryAccessDiagnostic(ScopeHandle handle, ScopeBoundaryAccessFailureKind failureKind, ScopeRuntimeNode? node)
        {
            DiagnosticCode code = failureKind == ScopeBoundaryAccessFailureKind.InvalidState
                ? new DiagnosticCode(KernelRuntimeScopeGraphCodes.ScopeBoundaryInvalidState)
                : new DiagnosticCode(KernelRuntimeScopeGraphCodes.ScopeBoundaryMissingScope);

            DiagnosticPayloadEntry[] payloadEntries = node == null
                ? new[]
                {
                    new DiagnosticPayloadEntry("ScopeHandle", DiagnosticPayloadValue.FromString(handle.ToString())),
                    new DiagnosticPayloadEntry("FailureKind", DiagnosticPayloadValue.FromString(failureKind.ToString())),
                }
                : new[]
                {
                    new DiagnosticPayloadEntry("ScopeHandle", DiagnosticPayloadValue.FromString(handle.ToString())),
                    new DiagnosticPayloadEntry("ScopePlanId", DiagnosticPayloadValue.FromInt32(node.PlanId.Value)),
                    new DiagnosticPayloadEntry("ScopeAuthoringId", DiagnosticPayloadValue.FromInt32(node.AuthoringId.Value)),
                    new DiagnosticPayloadEntry("OwnerModule", DiagnosticPayloadValue.FromInt32(node.OwnerModule.Value)),
                    new DiagnosticPayloadEntry("SourceLocation", DiagnosticPayloadValue.FromInt32(node.Source.Value)),
                    new DiagnosticPayloadEntry("CurrentState", DiagnosticPayloadValue.FromString(node.State.ToString())),
                    new DiagnosticPayloadEntry("FailureKind", DiagnosticPayloadValue.FromString(failureKind.ToString())),
                };

            RuntimeIdentityRef[] runtimeIdentities = node == null
                ? new[] { new RuntimeIdentityRef(RuntimeIdentityKind.ScopeHandle, handle.Index, handle.Generation) }
                : new[]
                {
                    new RuntimeIdentityRef(RuntimeIdentityKind.ScopeHandle, handle.Index, handle.Generation),
                    new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, node.PlanId.Value),
                    new RuntimeIdentityRef(RuntimeIdentityKind.ScopeAuthoring, node.AuthoringId.Value),
                    new RuntimeIdentityRef(RuntimeIdentityKind.Module, node.OwnerModule.Value),
                };

            DiagnosticContext context = new DiagnosticContext(runtimeIdentities);
            return new KernelDiagnostic(
                code,
                DiagnosticSeverity.Error,
                DiagnosticDomain.ScopeGraph,
                DiagnosticFailureBoundary.Scope,
                message: "Scope boundary access failed.",
                context: context,
                payload: new DiagnosticPayload(payloadEntries));
        }

        bool TryResolveParentHandle(ScopeHandle handle, out ScopeHandle parentHandle)
        {
            parentHandle = default;

            if (!instanceTable.TryGetNode(handle, out _))
                return false;

            if (parentChildTable.TryGetParent(handle, out parentHandle))
                return true;

            if (rootHandleIndex.Contains(handle))
                return true;

            return false;
        }

        void BuildFromPlan(ScopeGraphPlan scopeGraphPlan)
        {
            ReadOnlySpan<ScopeIR> scopes = scopeGraphPlan.Scopes;
            for (int index = 0; index < scopes.Length; index++)
            {
                ScopeIR scope = scopes[index];
                ScopeHandle handle = CreateNode(scope.PlanId, scope.AuthoringId, scope.Kind, scope.ParentAuthoringId, default, default, registerAsRoot: false);
                if (!scopesByAuthoringId.TryAdd(scope.AuthoringId, instanceTable.GetNodeOrThrow(handle)))
                    throw new InvalidOperationException("Kernel scope graph plan contains duplicate authoring identities.");
            }

            for (int index = 0; index < scopes.Length; index++)
            {
                ScopeIR scope = scopes[index];
                ScopeRuntimeNode node = scopesByAuthoringId[scope.AuthoringId];
                if (scope.ParentAuthoringId.IsDefault)
                {
                    AddRoot(node.Handle, node.PlanId);
                    continue;
                }

                if (!scopesByAuthoringId.TryGetValue(scope.ParentAuthoringId, out ScopeRuntimeNode? parentNode))
                    throw new InvalidOperationException("Kernel scope graph plan references a missing parent scope.");

                if (parentChildTable.WouldIntroduceCycle(node.Handle, parentNode.Handle))
                    throw new InvalidOperationException("Kernel scope graph plan contains a parent cycle.");

                parentChildTable.SetParent(node.Handle, parentNode.Handle);
                node.MarkBoundaryChange(ScopeBoundaryChangeKind.ParentChanged);
                RecordBoundaryChange(node, parentNode.Handle);
            }
        }

        ScopeHandle CreateNode(ScopePlanId planId, ScopeAuthoringId authoringId, ScopeKind kind, ScopeAuthoringId parentAuthoringId, ScopeHandle parent, UnityObjectLink unityLink, bool registerAsRoot)
        {
            if (TryHasLiveNodeWithPlanId(planId))
                throw new InvalidOperationException("Kernel scope graph already contains a live scope with the requested plan identity.");

            if (!parent.IsDefault)
            {
                if (!instanceTable.TryGetNode(parent, out _))
                    throw new InvalidOperationException("Kernel scope graph parent handle does not reference a live scope.");
            }

            ScopeHandle handle = instanceTable.AllocateHandle(out int slotIndex);
            ScopeIR scope = GetScopeDefinition(planId);
            UnityObjectLink resolvedUnityLink = ResolveUnityLink(scope.UnityObjectLink, unityLink);
            ScopeRuntimeNode node = new ScopeRuntimeNode(
                planId,
                authoringId,
                scope.OwnerModule,
                scope.Source,
                kind,
                parentAuthoringId,
                handle,
                scope.ServiceBoundary,
                scope.ValueInitPlans.ToArray(),
                scope.Lifecycle,
                resolvedUnityLink,
                ScopeRuntimeState.Created);
            instanceTable.Store(slotIndex, handle, node);
            liveHandlesByPlanId.Add(planId, handle);
            node.MarkBoundaryChange(ScopeBoundaryChangeKind.Created);
            RecordBoundaryChange(node, default);

            if (parent.IsDefault)
            {
                if (registerAsRoot)
                    AddRoot(handle, planId);
            }
            else
            {
                parentChildTable.SetParent(handle, parent);
                node.MarkBoundaryChange(ScopeBoundaryChangeKind.ParentChanged);
                RecordBoundaryChange(node, parent);
            }

            return handle;
        }

        void RecordBoundaryChange(ScopeRuntimeNode node, ScopeHandle parentHandle)
        {
            ScopeBoundaryChangeRecord record = new ScopeBoundaryChangeRecord(node.ToBoundarySnapshot(parentHandle));

            if (!boundaryChangeJournal.TryGetValue(node.Handle, out List<ScopeBoundaryChangeRecord>? records))
            {
                records = new List<ScopeBoundaryChangeRecord>(4);
                boundaryChangeJournal.Add(node.Handle, records);
            }

            records.Add(record);
        }

        static UnityObjectLink ResolveUnityLink(UnityObjectLinkIR? planLink, UnityObjectLink runtimeLink)
        {
            if (!runtimeLink.IsEmpty)
                return runtimeLink;

            if (planLink == null)
                return default;

            return new UnityObjectLink(
                ParseUnityObjectLinkKind(planLink.Kind),
                planLink.SourceGuid,
                planLink.LocalFileId,
                0,
                planLink.DebugName);
        }

        static UnityObjectLinkKind ParseUnityObjectLinkKind(string kind)
        {
            switch (kind)
            {
                case nameof(UnityObjectLinkKind.Asset):
                    return UnityObjectLinkKind.Asset;
                case nameof(UnityObjectLinkKind.Scene):
                    return UnityObjectLinkKind.Scene;
                case nameof(UnityObjectLinkKind.Runtime):
                    return UnityObjectLinkKind.Runtime;
                case nameof(UnityObjectLinkKind.Selection):
                    return UnityObjectLinkKind.Selection;
                default:
                    throw new InvalidOperationException("Kernel scope graph encountered an unsupported Unity object link kind in verified plan data.");
            }
        }

        ScopeHandle ResolveParentHandleOrDefault(ScopeHandle handle)
        {
            return TryResolveParentHandle(handle, out ScopeHandle parentHandle) ? parentHandle : default;
        }

        void AddRoot(ScopeHandle handle, ScopePlanId planId)
        {
            if (!rootHandleIndex.Add(handle))
                return;

            rootHandles.Add(handle);

            RuntimeIdentityRef identity = new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, planId.Value);
            if (!currentRootScopeIdentities.Contains(identity))
                currentRootScopeIdentities.Add(identity);
        }

        void RemoveRoot(ScopeHandle handle, ScopePlanId planId)
        {
            if (rootHandleIndex.Remove(handle))
            {
                for (int index = rootHandles.Count - 1; index >= 0; index--)
                {
                    if (rootHandles[index] == handle)
                    {
                        rootHandles.RemoveAt(index);
                        break;
                    }
                }
            }

            RuntimeIdentityRef identity = new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, planId.Value);
            for (int index = currentRootScopeIdentities.Count - 1; index >= 0; index--)
            {
                if (currentRootScopeIdentities[index] == identity)
                {
                    currentRootScopeIdentities.RemoveAt(index);
                    break;
                }
            }
        }

        void RemoveFromParentAndRootCollections(ScopeHandle handle, ScopePlanId planId)
        {
            parentChildTable.TryDetach(handle, out _);
            RemoveRoot(handle, planId);
        }

        bool WouldIntroduceCycle(ScopeHandle childHandle, ScopeHandle newParentHandle)
        {
            return parentChildTable.WouldIntroduceCycle(childHandle, newParentHandle);
        }

        bool TryHasLiveNodeWithPlanId(ScopePlanId planId)
        {
            return liveHandlesByPlanId.ContainsKey(planId);
        }

        bool TryGetNode(ScopeHandle handle, out ScopeRuntimeNode? node)
        {
            return instanceTable.TryGetNode(handle, out node);
        }

        ScopeRuntimeNode GetNodeOrThrow(ScopeHandle handle)
        {
            if (!instanceTable.TryGetNode(handle, out ScopeRuntimeNode? node) || node == null)
                throw new InvalidOperationException("Kernel scope graph handle does not reference a live scope.");

            return node;
        }

        ScopeIR GetScopeDefinition(ScopePlanId planId)
        {
            if (!scopesByPlanId.TryGetValue(planId, out ScopeIR? scope))
                throw new InvalidOperationException("Kernel scope graph root or child creation referenced a plan identity that is missing from the verified scope graph plan.");

            return scope;
        }

        void ValidateRootScopeIdentities(ReadOnlySpan<RuntimeIdentityRef> rootScopeIdentities)
        {
            HashSet<int> expectedRootPlanIds = new HashSet<int>();
            for (int index = 0; index < rootHandles.Count; index++)
            {
                if (!instanceTable.TryGetNode(rootHandles[index], out ScopeRuntimeNode? node) || node == null)
                    throw new InvalidOperationException("Kernel scope graph root handle does not reference a live scope.");

                expectedRootPlanIds.Add(node.PlanId.Value);
            }

            HashSet<int> actualRootPlanIds = new HashSet<int>();
            for (int index = 0; index < rootScopeIdentities.Length; index++)
            {
                RuntimeIdentityRef rootIdentity = rootScopeIdentities[index];
                if (rootIdentity.Kind != RuntimeIdentityKind.ScopePlan || rootIdentity.Value <= 0)
                    throw new ArgumentException("Kernel scope graph roots must be verified scope plan identities.", nameof(rootScopeIdentities));

                if (!actualRootPlanIds.Add(rootIdentity.Value))
                    throw new ArgumentException("Kernel scope graph roots must not contain duplicate plan identities.", nameof(rootScopeIdentities));
            }

            if (expectedRootPlanIds.Count != actualRootPlanIds.Count)
                throw new InvalidOperationException("Kernel scope graph root identities do not match the verified scope graph plan roots.");

            foreach (int planId in expectedRootPlanIds)
            {
                if (!actualRootPlanIds.Contains(planId))
                    throw new InvalidOperationException("Kernel scope graph root identities do not match the verified scope graph plan roots.");
            }
        }

        void BuildPlanIndex(ScopeGraphPlan scopeGraphPlan)
        {
            ReadOnlySpan<ScopeIR> scopes = scopeGraphPlan.Scopes;
            for (int index = 0; index < scopes.Length; index++)
            {
                ScopeIR scope = scopes[index];
                if (scopesByPlanId.ContainsKey(scope.PlanId))
                    throw new InvalidOperationException("Kernel scope graph plan contains duplicate plan identities.");

                scopesByPlanId.Add(scope.PlanId, scope);
            }

            ReadOnlySpan<ValueInitPlanIR> valueInitPlans = scopeGraphPlan.ValueInitPlans;
            for (int index = 0; index < valueInitPlans.Length; index++)
            {
                ValueInitPlanIR valueInitPlan = valueInitPlans[index];
                if (valueInitPlansById.ContainsKey(valueInitPlan.PlanId))
                    throw new InvalidOperationException("Kernel scope graph plan contains duplicate value init plan identities.");

                valueInitPlansById.Add(valueInitPlan.PlanId, valueInitPlan);
            }
        }

        sealed class ScopeInstanceTable
        {
            readonly List<ScopeSlot> slots = new();
            readonly Stack<int> freeSlots = new();

            public ScopeHandle AllocateHandle(out int slotIndex)
            {
                if (freeSlots.Count > 0)
                {
                    slotIndex = freeSlots.Pop();
                }
                else
                {
                    slotIndex = slots.Count;
                    slots.Add(new ScopeSlot());
                }

                ScopeSlot slot = slots[slotIndex];
                int generation = slot.Generation <= 0 ? 1 : checked(slot.Generation + 1);
                return new ScopeHandle(slotIndex + 1, generation);
            }

            public void Store(int slotIndex, ScopeHandle handle, ScopeRuntimeNode node)
            {
                ScopeSlot slot = slots[slotIndex];
                slot.Generation = handle.Generation;
                slot.Instance = node;
            }

            public bool TryGetNode(ScopeHandle handle, out ScopeRuntimeNode? node)
            {
                node = null;

                if (handle.IsDefault)
                    return false;

                int slotIndex = handle.Index - 1;
                if ((uint)slotIndex >= (uint)slots.Count)
                    return false;

                ScopeSlot slot = slots[slotIndex];
                if (slot.Generation != handle.Generation)
                    return false;

                node = slot.Instance;
                if (node == null || node.State == ScopeRuntimeState.Destroyed)
                    return false;

                return node.Handle == handle;
            }

            public bool TryRelease(ScopeHandle handle)
            {
                if (handle.IsDefault)
                    return false;

                int slotIndex = handle.Index - 1;
                if ((uint)slotIndex >= (uint)slots.Count)
                    return false;

                ScopeSlot slot = slots[slotIndex];
                if (slot.Generation != handle.Generation || slot.Instance == null)
                    return false;

                slot.Instance = null;
                freeSlots.Push(slotIndex);
                return true;
            }

            public ScopeRuntimeNode GetNodeOrThrow(ScopeHandle handle)
            {
                if (!TryGetNode(handle, out ScopeRuntimeNode? node) || node == null)
                    throw new InvalidOperationException("Kernel scope graph handle does not reference a live scope.");

                return node;
            }
        }

        sealed class ScopeSlot
        {
            public int Generation { get; set; }

            public ScopeRuntimeNode? Instance { get; set; }
        }

        sealed class ScopeParentChildTable
        {
            readonly Dictionary<ScopeHandle, ScopeHandle> parentByChild = new();
            readonly Dictionary<ScopeHandle, List<ScopeHandle>> childrenByParent = new();

            public bool TryGetParent(ScopeHandle childHandle, out ScopeHandle parentHandle)
            {
                return parentByChild.TryGetValue(childHandle, out parentHandle);
            }

            public IReadOnlyList<ScopeHandle> GetChildHandles(ScopeHandle parentHandle)
            {
                if (childrenByParent.TryGetValue(parentHandle, out List<ScopeHandle>? children))
                    return children;

                return Array.Empty<ScopeHandle>();
            }

            public ScopeHandle[] GetChildHandlesSnapshot(ScopeHandle parentHandle)
            {
                if (!childrenByParent.TryGetValue(parentHandle, out List<ScopeHandle>? children) || children.Count == 0)
                    return Array.Empty<ScopeHandle>();

                return children.ToArray();
            }

            public void SetParent(ScopeHandle childHandle, ScopeHandle parentHandle)
            {
                if (parentByChild.TryGetValue(childHandle, out ScopeHandle existingParent))
                {
                    if (existingParent == parentHandle)
                        return;

                    RemoveChildFromParent(existingParent, childHandle);
                    parentByChild[childHandle] = parentHandle;
                }
                else
                {
                    parentByChild.Add(childHandle, parentHandle);
                }

                AddChildToParent(parentHandle, childHandle);
            }

            public bool TryDetach(ScopeHandle childHandle, out ScopeHandle parentHandle)
            {
                if (!parentByChild.TryGetValue(childHandle, out parentHandle))
                    return false;

                parentByChild.Remove(childHandle);
                RemoveChildFromParent(parentHandle, childHandle);
                return true;
            }

            public bool WouldIntroduceCycle(ScopeHandle childHandle, ScopeHandle newParentHandle)
            {
                ScopeHandle current = newParentHandle;
                while (TryGetParent(current, out ScopeHandle parentHandle))
                {
                    if (parentHandle == childHandle)
                        return true;

                    current = parentHandle;
                }

                return false;
            }

            void AddChildToParent(ScopeHandle parentHandle, ScopeHandle childHandle)
            {
                if (!childrenByParent.TryGetValue(parentHandle, out List<ScopeHandle>? children))
                {
                    children = new List<ScopeHandle>();
                    childrenByParent.Add(parentHandle, children);
                }

                if (!children.Contains(childHandle))
                    children.Add(childHandle);
            }

            void RemoveChildFromParent(ScopeHandle parentHandle, ScopeHandle childHandle)
            {
                if (!childrenByParent.TryGetValue(parentHandle, out List<ScopeHandle>? children))
                    return;

                for (int index = children.Count - 1; index >= 0; index--)
                {
                    if (children[index] == childHandle)
                    {
                        children.RemoveAt(index);
                        break;
                    }
                }

                if (children.Count == 0)
                    childrenByParent.Remove(parentHandle);
            }
        }

        sealed class ScopeRuntimeNode
        {
            public ScopeRuntimeNode(
                ScopePlanId planId,
                ScopeAuthoringId authoringId,
                ModuleId ownerModule,
                SourceLocationId source,
                ScopeKind kind,
                ScopeAuthoringId parentAuthoringId,
                ScopeHandle handle,
                ScopeServiceBoundaryIR serviceBoundary,
                ScopeValueInitRefIR[] valueInitPlans,
                LifecyclePlanRefIR lifecycle,
                UnityObjectLink unityLink,
                ScopeRuntimeState state)
            {
                PlanId = planId;
                AuthoringId = authoringId;
                OwnerModule = ownerModule;
                Source = source;
                Kind = kind;
                ParentAuthoringId = parentAuthoringId;
                Handle = handle;
                ServiceBoundary = serviceBoundary;
                ValueInitPlans = valueInitPlans ?? Array.Empty<ScopeValueInitRefIR>();
                Lifecycle = lifecycle;
                UnityLink = unityLink;
                State = state;
                BoundaryRevision = 0;
                LastChangeKind = ScopeBoundaryChangeKind.None;
            }

            public ScopePlanId PlanId { get; }

            public ScopeAuthoringId AuthoringId { get; }

            public ModuleId OwnerModule { get; }

            public SourceLocationId Source { get; }

            public ScopeKind Kind { get; }

            public ScopeAuthoringId ParentAuthoringId { get; }

            public ScopeHandle Handle { get; }

            public ScopeServiceBoundaryIR ServiceBoundary { get; }

            public ScopeValueInitRefIR[] ValueInitPlans { get; }

            public LifecyclePlanRefIR Lifecycle { get; }

            public UnityObjectLink UnityLink { get; set; }

            public ScopeRuntimeState State { get; set; }

            public ScopeBoundaryChangeKind LastChangeKind { get; private set; }

            public int BoundaryRevision { get; private set; }

            public void MarkBoundaryChange(ScopeBoundaryChangeKind changeKind)
            {
                LastChangeKind = changeKind;
                BoundaryRevision = checked(BoundaryRevision + 1);
            }

            public ScopeRuntimeSnapshot ToSnapshot(ScopeHandle parent)
            {
                return new ScopeRuntimeSnapshot(Handle, PlanId, AuthoringId, parent, Kind, State, UnityLink);
            }

            public ScopeBoundarySnapshot ToBoundarySnapshot(ScopeHandle parent)
            {
                return new ScopeBoundarySnapshot(
                    Handle,
                    PlanId,
                    AuthoringId,
                    OwnerModule,
                    Source,
                    parent,
                    Kind,
                    State,
                    ServiceBoundary,
                    ValueInitPlans,
                    Lifecycle,
                    UnityLink,
                    LastChangeKind,
                    BoundaryRevision);
            }
        }
    }
}

#nullable enable
using System;
using System.Collections.Generic;
using Game.Kernel.Boot;
using Game.Kernel.Diagnostics;
using Game.Kernel.Generation;
using Game.Kernel.IR;
using Game.Kernel.ScopeGraph;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class KernelRuntimeScopeGraphTests
    {
        [Test]
        public void CreateScopeGraph_RejectsMissingRootIdentities_WhenVerifiedPlanHasRoots()
        {
            ScopeGraphPlan plan = CreatePlan(includeChild: false);

            Assert.That(() => new KernelRuntimeScopeGraph(plan, Array.Empty<RuntimeIdentityRef>()), Throws.InvalidOperationException);
        }

        [Test]
        public void ScopeHandle_DefaultValue_IsInvalid_AndFormatsAsDefault()
        {
            ScopeHandle defaultHandle = default;

            Assert.That(defaultHandle.IsDefault, Is.True);
            Assert.That(defaultHandle.IsValid, Is.False);
            Assert.That(defaultHandle.ToString(), Is.EqualTo("ScopeHandle(<default>)"));

            ScopeGraphPlan plan = CreatePlan(includeChild: false);
            KernelRuntimeScopeGraph graph = new KernelRuntimeScopeGraph(plan, new[] { ScopeIdentity(21) });

            Assert.That(graph.TryGetScope(defaultHandle, out _), Is.False);
        }

        [Test]
        public void CreateScope_ReusesSlotWithGenerationBump_AfterDestroy()
        {
            ScopeGraphPlan plan = CreatePlan(includeChild: false);
            KernelRuntimeScopeGraph graph = new KernelRuntimeScopeGraph(plan, new[] { ScopeIdentity(21) });

            ScopeHandle rootHandle = graph.RootScopeHandles[0];
            Assert.That(graph.TryGetScope(rootHandle, out ScopeRuntimeSnapshot snapshot), Is.True);
            Assert.That(snapshot.PlanId, Is.EqualTo(new ScopePlanId(21)));
            Assert.That(snapshot.State, Is.EqualTo(ScopeRuntimeState.Created));

            Assert.That(graph.TryDestroyScope(rootHandle), Is.True);
            Assert.That(graph.TryGetScope(rootHandle, out _), Is.False);

            ScopeHandle recreatedHandle = graph.CreateScope(new ScopeCreateRequest(new ScopePlanId(21), default, ScopeCreateMode.Root, default, new SourceLocationId(91)));

            Assert.That(recreatedHandle.Index, Is.EqualTo(rootHandle.Index));
            Assert.That(recreatedHandle.Generation, Is.GreaterThan(rootHandle.Generation));
            Assert.That(graph.TryGetScope(recreatedHandle, out snapshot), Is.True);
            Assert.That(snapshot.PlanId, Is.EqualTo(new ScopePlanId(21)));
            Assert.That(snapshot.State, Is.EqualTo(ScopeRuntimeState.Created));
            Assert.That(graph.TryGetScopeBoundary(recreatedHandle, out ScopeBoundarySnapshot recreatedBoundary), Is.True);
            Assert.That(recreatedBoundary.BoundaryRevision, Is.EqualTo(1));
            Assert.That(recreatedBoundary.LastChangeKind, Is.EqualTo(ScopeBoundaryChangeKind.Created));

            Assert.That(graph.TryGetScopeBoundaryChanges(recreatedHandle, out var recreatedChanges), Is.True);
            Assert.That(recreatedChanges.Count, Is.EqualTo(1));
            Assert.That(recreatedChanges[0].ChangeKind, Is.EqualTo(ScopeBoundaryChangeKind.Created));
        }

        [Test]
        public void BoundarySnapshots_PreserveVerifiedPlanFacets_AndStartWithCreatedRevision()
        {
            ScopeValueInitRefIR[] valueInitPlans = new[]
            {
                new ScopeValueInitRefIR(new ValueInitPlanId(301), new SourceLocationId(302)),
            };

            ScopeGraphPlan plan = CreatePlan(includeChild: false, rootValueInitPlans: valueInitPlans);
            KernelRuntimeScopeGraph graph = new KernelRuntimeScopeGraph(plan, new[] { ScopeIdentity(21) });

            ScopeHandle rootHandle = graph.RootScopeHandles[0];

            Assert.That(graph.TryGetScopeBoundary(rootHandle, out ScopeBoundarySnapshot boundary), Is.True);
            Assert.That(boundary.Handle, Is.EqualTo(rootHandle));
            Assert.That(boundary.PlanId, Is.EqualTo(new ScopePlanId(21)));
            Assert.That(boundary.AuthoringId, Is.EqualTo(new ScopeAuthoringId(1)));
            Assert.That(boundary.OwnerModule, Is.EqualTo(new ModuleId(10)));
            Assert.That(boundary.ServiceBoundary.Kind, Is.EqualTo(ScopeServiceBoundaryKind.Detached));
            Assert.That(boundary.Lifecycle.PlanId, Is.EqualTo(new LifecyclePlanId(131)));
            Assert.That(boundary.ValueInitPlans.Length, Is.EqualTo(1));
            Assert.That(boundary.ValueInitPlans[0].PlanId, Is.EqualTo(new ValueInitPlanId(301)));
            Assert.That(boundary.ValueInitPlans[0].Source, Is.EqualTo(new SourceLocationId(302)));
            Assert.That(boundary.UnityLink.IsEmpty, Is.True);
            Assert.That(boundary.LastChangeKind, Is.EqualTo(ScopeBoundaryChangeKind.Created));
            Assert.That(boundary.BoundaryRevision, Is.EqualTo(1));
        }

        [Test]
        public void TryGetScopeValueInitPlans_ResolvesReferencedPlansInExecutionOrder()
        {
            ScopeValueInitRefIR[] rootValueInitPlans = new[]
            {
                new ScopeValueInitRefIR(new ValueInitPlanId(302), new SourceLocationId(302)),
                new ScopeValueInitRefIR(new ValueInitPlanId(301), new SourceLocationId(301)),
            };

            ValueInitPlanIR[] valueInitPlans = new[]
            {
                CreateValueInitPlan(301, order: 20, keyId: 701),
                CreateValueInitPlan(302, order: 10, keyId: 702),
            };

            ScopeGraphPlan plan = CreatePlan(includeChild: false, rootValueInitPlans: rootValueInitPlans, valueInitPlans: valueInitPlans);
            KernelRuntimeScopeGraph graph = new KernelRuntimeScopeGraph(plan, new[] { ScopeIdentity(21) });

            ScopeHandle rootHandle = graph.RootScopeHandles[0];

            Assert.That(graph.TryGetScopeValueInitPlans(rootHandle, out IReadOnlyList<ValueInitPlanIR> resolvedPlans), Is.True);
            Assert.That(resolvedPlans, Has.Count.EqualTo(2));
            Assert.That(resolvedPlans[0].PlanId, Is.EqualTo(new ValueInitPlanId(302)));
            Assert.That(resolvedPlans[0].Order, Is.EqualTo(10));
            Assert.That(resolvedPlans[0].Entries[0].EvaluationLocalRef, Is.EqualTo("battle.value.302"));
            Assert.That(resolvedPlans[1].PlanId, Is.EqualTo(new ValueInitPlanId(301)));
            Assert.That(resolvedPlans[1].Order, Is.EqualTo(20));
        }

        [Test]
        public void BoundarySnapshots_PreserveVerifiedPlanUnityObjectLinkMetadata()
        {
            UnityObjectLinkIR rootUnityObjectLink = new UnityObjectLinkIR(
                "Scene",
                "scene-guid-21",
                21,
                "RootScope",
                new SourceLocationId(33));

            ScopeGraphPlan plan = CreatePlan(includeChild: false, rootUnityObjectLink: rootUnityObjectLink);
            KernelRuntimeScopeGraph graph = new KernelRuntimeScopeGraph(plan, new[] { ScopeIdentity(21) });

            ScopeHandle rootHandle = graph.RootScopeHandles[0];

            Assert.That(graph.TryGetScopeBoundary(rootHandle, out ScopeBoundarySnapshot boundary), Is.True);
            Assert.That(boundary.UnityLink.Kind, Is.EqualTo(UnityObjectLinkKind.Scene));
            Assert.That(boundary.UnityLink.SourceGuid, Is.EqualTo("scene-guid-21"));
            Assert.That(boundary.UnityLink.LocalFileId, Is.EqualTo(21));
            Assert.That(boundary.UnityLink.RuntimeInstanceId, Is.EqualTo(0));
            Assert.That(boundary.UnityLink.DebugName, Is.EqualTo("RootScope"));
        }

        [Test]
        public void BoundaryChangeJournal_PreservesDestroyedScopeHistory_AfterHandleRelease()
        {
            ScopeGraphPlan plan = CreatePlan(includeChild: true);
            KernelRuntimeScopeGraph graph = new KernelRuntimeScopeGraph(plan, new[] { ScopeIdentity(21) });

            ScopeHandle rootHandle = graph.RootScopeHandles[0];
            Assert.That(graph.TryGetChildHandles(rootHandle, out var childHandles), Is.True);
            ScopeHandle childHandle = childHandles[0];

            Assert.That(graph.TryDestroyScope(childHandle), Is.True);
            Assert.That(graph.TryGetScope(childHandle, out _), Is.False);
            Assert.That(graph.TryGetScopeBoundary(childHandle, out _, out ScopeBoundaryAccessFailureKind boundaryFailureKind, out KernelDiagnostic? boundaryDiagnostic), Is.False);
            Assert.That(boundaryFailureKind, Is.EqualTo(ScopeBoundaryAccessFailureKind.MissingScope));
            Assert.That(boundaryDiagnostic, Is.Not.Null);

            Assert.That(graph.TryGetScopeBoundaryChanges(childHandle, out var changes), Is.True);
            Assert.That(changes.Count, Is.EqualTo(4));
            Assert.That(changes[0].ChangeKind, Is.EqualTo(ScopeBoundaryChangeKind.Created));
            Assert.That(changes[1].ChangeKind, Is.EqualTo(ScopeBoundaryChangeKind.ParentChanged));
            Assert.That(changes[2].ChangeKind, Is.EqualTo(ScopeBoundaryChangeKind.UnityLinkInvalidated));
            Assert.That(changes[2].Snapshot.UnityLink.IsEmpty, Is.True);
            Assert.That(changes[3].ChangeKind, Is.EqualTo(ScopeBoundaryChangeKind.Destroyed));
            Assert.That(changes[3].Snapshot.State, Is.EqualTo(ScopeRuntimeState.Destroyed));
            Assert.That(changes[3].Snapshot.Parent, Is.EqualTo(rootHandle));
            Assert.That(changes[3].BoundaryRevision, Is.EqualTo(4));
        }

        [Test]
        public void BoundarySnapshots_TrackRevisionAcrossParentStateAndUnityChanges_AndFailClosedAfterDestroy()
        {
            ScopeGraphPlan plan = CreatePlan(includeChild: true);
            KernelRuntimeScopeGraph graph = new KernelRuntimeScopeGraph(plan, new[] { ScopeIdentity(21) });

            ScopeHandle rootHandle = graph.RootScopeHandles[0];
            Assert.That(graph.TryGetChildHandles(rootHandle, out IReadOnlyList<ScopeHandle> childHandles), Is.True);
            Assert.That(childHandles.Count, Is.EqualTo(1));

            ScopeHandle childHandle = childHandles[0];

            Assert.That(graph.TryGetScopeBoundary(childHandle, out ScopeBoundarySnapshot boundary), Is.True);
            Assert.That(boundary.Parent, Is.EqualTo(rootHandle));
            Assert.That(boundary.LastChangeKind, Is.EqualTo(ScopeBoundaryChangeKind.ParentChanged));
            Assert.That(boundary.BoundaryRevision, Is.EqualTo(2));

            Assert.That(graph.TrySetState(childHandle, ScopeRuntimeState.Built, out ScopeStateTransitionFailureKind stateFailureKind), Is.True);
            Assert.That(stateFailureKind, Is.EqualTo(ScopeStateTransitionFailureKind.None));
            Assert.That(graph.TryGetScopeBoundary(childHandle, out boundary), Is.True);
            Assert.That(boundary.State, Is.EqualTo(ScopeRuntimeState.Built));
            Assert.That(boundary.LastChangeKind, Is.EqualTo(ScopeBoundaryChangeKind.StateChanged));
            Assert.That(boundary.BoundaryRevision, Is.EqualTo(3));

            UnityObjectLink updatedLink = new UnityObjectLink(UnityObjectLinkKind.Scene, "scene-guid-401", 401, 12, "Scene/Child/Updated");
            Assert.That(graph.TrySetUnityLink(childHandle, updatedLink), Is.True);
            Assert.That(graph.TryGetScopeBoundary(childHandle, out boundary), Is.True);
            Assert.That(boundary.UnityLink, Is.EqualTo(updatedLink));
            Assert.That(boundary.UnityLink.Kind, Is.EqualTo(UnityObjectLinkKind.Scene));
            Assert.That(boundary.UnityLink.SourceGuid, Is.EqualTo("scene-guid-401"));
            Assert.That(boundary.UnityLink.LocalFileId, Is.EqualTo(401));
            Assert.That(boundary.UnityLink.RuntimeInstanceId, Is.EqualTo(12));
            Assert.That(boundary.UnityLink.DebugName, Is.EqualTo("Scene/Child/Updated"));
            Assert.That(boundary.LastChangeKind, Is.EqualTo(ScopeBoundaryChangeKind.UnityLinkChanged));
            Assert.That(boundary.BoundaryRevision, Is.EqualTo(4));

            Assert.That(graph.TryDestroyScope(childHandle), Is.True);
            Assert.That(graph.TryGetScopeBoundary(childHandle, out _, out ScopeBoundaryAccessFailureKind failureKind, out KernelDiagnostic? diagnostic), Is.False);
            Assert.That(failureKind, Is.EqualTo(ScopeBoundaryAccessFailureKind.MissingScope));
            Assert.That(diagnostic, Is.Not.Null);
            Assert.That(diagnostic!.Code.Value, Is.EqualTo(KernelRuntimeScopeGraphCodes.ScopeBoundaryMissingScope));
        }

        [Test]
        public void StateTransitions_AllowDocumentedShortcutSequence()
        {
            ScopeGraphPlan plan = CreatePlan(includeChild: false);
            KernelRuntimeScopeGraph graph = new KernelRuntimeScopeGraph(plan, new[] { ScopeIdentity(21) });

            ScopeHandle rootHandle = graph.RootScopeHandles[0];

            Assert.That(graph.TrySetState(rootHandle, ScopeRuntimeState.Built, out ScopeStateTransitionFailureKind failureKind), Is.True);
            Assert.That(failureKind, Is.EqualTo(ScopeStateTransitionFailureKind.None));
            Assert.That(graph.TryGetScope(rootHandle, out ScopeRuntimeSnapshot snapshot), Is.True);
            Assert.That(snapshot.State, Is.EqualTo(ScopeRuntimeState.Built));

            Assert.That(graph.TrySetState(rootHandle, ScopeRuntimeState.Active, out failureKind), Is.True);
            Assert.That(failureKind, Is.EqualTo(ScopeStateTransitionFailureKind.None));
            Assert.That(graph.TryGetScope(rootHandle, out snapshot), Is.True);
            Assert.That(snapshot.State, Is.EqualTo(ScopeRuntimeState.Active));
        }

        [Test]
        public void StateTransitions_RecordLifecycleRequests_WhenResolverMatchesPlanId()
        {
            ScopeGraphPlan plan = CreatePlan(includeChild: false);
            KernelLifecycleDispatcher dispatcher = new KernelLifecycleDispatcher(CreateLifecyclePlan(131));
            KernelLifecyclePlanResolver resolver = new KernelLifecyclePlanResolver(new[] { dispatcher });
            KernelRuntimeScopeGraph graph = new KernelRuntimeScopeGraph(plan, new[] { ScopeIdentity(21) }, resolver);

            ScopeHandle rootHandle = graph.RootScopeHandles[0];

            Assert.That(graph.TrySetState(rootHandle, ScopeRuntimeState.Built, out ScopeStateTransitionFailureKind failureKind), Is.True);
            Assert.That(failureKind, Is.EqualTo(ScopeStateTransitionFailureKind.None));
            Assert.That(graph.TryGetLifecycleTransitionRequests(rootHandle, out IReadOnlyList<ScopeLifecycleTransitionRequest> requests), Is.True);
            Assert.That(requests, Has.Count.EqualTo(1));
            Assert.That(requests[0].Handle, Is.EqualTo(rootHandle));
            Assert.That(requests[0].LifecyclePlanId, Is.EqualTo(new LifecyclePlanId(131)));
            Assert.That(requests[0].CurrentState, Is.EqualTo(ScopeRuntimeState.Created));
            Assert.That(requests[0].NextState, Is.EqualTo(ScopeRuntimeState.Built));
        }

        [Test]
        public void TryGetScopeHandle_UsesPlanIndex_AndStopsResolvingAfterDestroy()
        {
            ScopeGraphPlan plan = CreatePlan(includeChild: false);
            KernelRuntimeScopeGraph graph = new KernelRuntimeScopeGraph(plan, new[] { ScopeIdentity(21) });

            ScopeHandle rootHandle = graph.RootScopeHandles[0];
            Assert.That(graph.TryGetScopeHandle(new ScopePlanId(21), out ScopeHandle resolvedHandle), Is.True);
            Assert.That(resolvedHandle, Is.EqualTo(rootHandle));

            Assert.That(graph.TryDestroyScope(rootHandle), Is.True);
            Assert.That(graph.TryGetScopeHandle(new ScopePlanId(21), out _), Is.False);
        }

        [Test]
        public void StateTransitions_RejectMissingLifecyclePlan_WhenResolverCannotResolvePlanId()
        {
            ScopeGraphPlan plan = CreatePlan(includeChild: false);
            KernelLifecycleDispatcher dispatcher = new KernelLifecycleDispatcher(CreateLifecyclePlan(999));
            KernelLifecyclePlanResolver resolver = new KernelLifecyclePlanResolver(new[] { dispatcher });
            KernelRuntimeScopeGraph graph = new KernelRuntimeScopeGraph(plan, new[] { ScopeIdentity(21) }, resolver);

            ScopeHandle rootHandle = graph.RootScopeHandles[0];

            Assert.That(graph.TrySetState(rootHandle, ScopeRuntimeState.Built, out ScopeStateTransitionFailureKind failureKind, out KernelDiagnostic? diagnostic), Is.False);
            Assert.That(failureKind, Is.EqualTo(ScopeStateTransitionFailureKind.MissingLifecyclePlan));
            Assert.That(diagnostic, Is.Not.Null);
            Assert.That(diagnostic!.Code.Value, Is.EqualTo(KernelRuntimeScopeGraphCodes.ScopeStateTransitionMissingLifecyclePlan));
        }

        [Test]
        public void StateTransitions_RejectsInvalidDirectJump()
        {
            ScopeGraphPlan plan = CreatePlan(includeChild: false);
            KernelRuntimeScopeGraph graph = new KernelRuntimeScopeGraph(plan, new[] { ScopeIdentity(21) });

            ScopeHandle rootHandle = graph.RootScopeHandles[0];

            Assert.That(graph.TrySetState(rootHandle, ScopeRuntimeState.Active, out ScopeStateTransitionFailureKind failureKind, out KernelDiagnostic? diagnostic), Is.False);
            Assert.That(failureKind, Is.EqualTo(ScopeStateTransitionFailureKind.InvalidTransition));
            Assert.That(diagnostic, Is.Not.Null);
            Assert.That(diagnostic!.Code.Value, Is.EqualTo(KernelRuntimeScopeGraphCodes.ScopeStateTransitionInvalid));
            Assert.That(graph.TryGetScope(rootHandle, out ScopeRuntimeSnapshot snapshot), Is.True);
            Assert.That(snapshot.State, Is.EqualTo(ScopeRuntimeState.Created));
        }

        [Test]
        public void StateTransitions_FailedIsTerminal_ForLiveScope()
        {
            ScopeGraphPlan plan = CreatePlan(includeChild: false);
            KernelRuntimeScopeGraph graph = new KernelRuntimeScopeGraph(plan, new[] { ScopeIdentity(21) });

            ScopeHandle rootHandle = graph.RootScopeHandles[0];

            Assert.That(graph.TrySetState(rootHandle, ScopeRuntimeState.Failed, out ScopeStateTransitionFailureKind failureKind), Is.True);
            Assert.That(failureKind, Is.EqualTo(ScopeStateTransitionFailureKind.None));
            Assert.That(graph.TryGetScope(rootHandle, out ScopeRuntimeSnapshot snapshot), Is.True);
            Assert.That(snapshot.State, Is.EqualTo(ScopeRuntimeState.Failed));

            Assert.That(graph.TrySetState(rootHandle, ScopeRuntimeState.Active, out failureKind), Is.False);
            Assert.That(failureKind, Is.EqualTo(ScopeStateTransitionFailureKind.TerminalScopeState));
            Assert.That(graph.TryGetScope(rootHandle, out snapshot), Is.True);
            Assert.That(snapshot.State, Is.EqualTo(ScopeRuntimeState.Failed));
        }

        [Test]
        public void FailedScope_RejectsNewChildCreation()
        {
            ScopeGraphPlan plan = CreatePlan(includeChild: false);
            KernelRuntimeScopeGraph graph = new KernelRuntimeScopeGraph(plan, new[] { ScopeIdentity(21) });

            ScopeHandle rootHandle = graph.RootScopeHandles[0];

            Assert.That(graph.TrySetState(rootHandle, ScopeRuntimeState.Failed, out _), Is.True);
            Assert.That(() => graph.CreateScope(new ScopeCreateRequest(new ScopePlanId(22), rootHandle, ScopeCreateMode.Child, new UnityObjectLink(UnityObjectLinkKind.Scene, "scene-guid-92", 92, 0, "Scene/Child"), new SourceLocationId(93))), Throws.InvalidOperationException);
        }

        [Test]
        public void ReparentScope_RejectsCycle_AndPreservesUnityLinkTrace()
        {
            ScopeGraphPlan plan = CreatePlan(includeChild: true);
            KernelRuntimeScopeGraph graph = new KernelRuntimeScopeGraph(plan, new[] { ScopeIdentity(21) });

            ScopeHandle rootHandle = graph.RootScopeHandles[0];
            ScopeHandle childHandle = graph.CreateScope(new ScopeCreateRequest(new ScopePlanId(22), rootHandle, ScopeCreateMode.Child, new UnityObjectLink(UnityObjectLinkKind.Scene, "scene-guid-92", 92, 0, "Scene/Child"), new SourceLocationId(93)));
            ScopeHandle grandChildHandle = graph.CreateScope(new ScopeCreateRequest(new ScopePlanId(23), childHandle, ScopeCreateMode.Child, new UnityObjectLink(UnityObjectLinkKind.Scene, "scene-guid-94", 94, 0, "Scene/GrandChild"), new SourceLocationId(95)));

            Assert.That(graph.TryGetScope(grandChildHandle, out ScopeRuntimeSnapshot grandChildSnapshot), Is.True);
            Assert.That(grandChildSnapshot.UnityLink.DebugName, Is.EqualTo("Scene/GrandChild"));

            Assert.That(graph.TryReparentScope(rootHandle, grandChildHandle), Is.False);
            Assert.That(graph.TryGetScope(rootHandle, out ScopeRuntimeSnapshot rootSnapshot), Is.True);
            Assert.That(rootSnapshot.IsRoot, Is.True);
        }

        [Test]
        public void ChildHandles_AreStoredInExplicitParentChildTable()
        {
            ScopeGraphPlan plan = CreatePlan(includeChild: true);
            KernelRuntimeScopeGraph graph = new KernelRuntimeScopeGraph(plan, new[] { ScopeIdentity(21) });

            ScopeHandle rootHandle = graph.RootScopeHandles[0];
            ScopeHandle childHandle = graph.CreateScope(new ScopeCreateRequest(new ScopePlanId(22), rootHandle, ScopeCreateMode.Child, new UnityObjectLink(UnityObjectLinkKind.Scene, "scene-guid-92", 92, 0, "Scene/Child"), new SourceLocationId(93)));
            ScopeHandle grandChildHandle = graph.CreateScope(new ScopeCreateRequest(new ScopePlanId(23), childHandle, ScopeCreateMode.Child, new UnityObjectLink(UnityObjectLinkKind.Scene, "scene-guid-94", 94, 0, "Scene/GrandChild"), new SourceLocationId(95)));

            Assert.That(graph.TryGetChildHandles(rootHandle, out var rootChildren), Is.True);
            Assert.That(rootChildren.Count, Is.EqualTo(1));
            Assert.That(rootChildren[0], Is.EqualTo(childHandle));

            Assert.That(graph.TryGetChildHandles(childHandle, out var childChildren), Is.True);
            Assert.That(childChildren.Count, Is.EqualTo(1));
            Assert.That(childChildren[0], Is.EqualTo(grandChildHandle));
        }

        [Test]
        public void DetachScope_AllowsAuthoredChildToBecomeRoot()
        {
            ScopeGraphPlan plan = CreatePlan(includeChild: true);
            KernelRuntimeScopeGraph graph = new KernelRuntimeScopeGraph(plan, new[] { ScopeIdentity(21) });

            ScopeHandle rootHandle = graph.RootScopeHandles[0];
            ScopeHandle childHandle = graph.CreateScope(new ScopeCreateRequest(new ScopePlanId(22), rootHandle, ScopeCreateMode.Child, new UnityObjectLink(UnityObjectLinkKind.Scene, "scene-guid-92", 92, 0, "Scene/Child"), new SourceLocationId(93)));

            Assert.That(graph.TryDetachScope(childHandle), Is.True);
            Assert.That(graph.TryGetScope(childHandle, out ScopeRuntimeSnapshot detachedSnapshot), Is.True);
            Assert.That(detachedSnapshot.IsRoot, Is.True);
        }

        [Test]
        public void CreateScopeGraph_RejectsMismatchedRootIdentities()
        {
            ScopeGraphPlan plan = CreatePlan(includeChild: false);

            Assert.That(() => new KernelRuntimeScopeGraph(plan, new[] { ScopeIdentity(22) }), Throws.InvalidOperationException);
        }

        static RuntimeIdentityRef ScopeIdentity(int value)
        {
            return new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, value);
        }

        static ScopeGraphPlan CreatePlan(bool includeChild, ScopeValueInitRefIR[]? rootValueInitPlans = null, UnityObjectLinkIR? rootUnityObjectLink = null, ValueInitPlanIR[]? valueInitPlans = null)
        {
            ScopeIR[] scopes = includeChild
                ? new[]
                {
                    CreateScope(1, 21, default, ScopeKind.Root, ScopeServiceBoundaryKind.Detached, 31, rootValueInitPlans, rootUnityObjectLink),
                    CreateScope(2, 22, new ScopeAuthoringId(1), ScopeKind.Child, ScopeServiceBoundaryKind.ReferencesParent, 32),
                    CreateScope(3, 23, new ScopeAuthoringId(2), ScopeKind.Child, ScopeServiceBoundaryKind.ReferencesParent, 33),
                }
                : new[]
                {
                    CreateScope(1, 21, default, ScopeKind.Root, ScopeServiceBoundaryKind.Detached, 31, rootValueInitPlans, rootUnityObjectLink),
                };

            ValueInitPlanIR[] resolvedValueInitPlans = valueInitPlans ?? Array.Empty<ValueInitPlanIR>();
            Hash128 contentHash = KernelProjectionHashingTestAdapter.ComputeScopeGraphHash(scopes, resolvedValueInitPlans);
            VerifiedArtifactHeader header = new VerifiedArtifactHeader(
                new PlanId(32),
                new ArtifactSetId(11),
                new ArtifactId(2),
                ArtifactKind.ScopeGraph,
                11,
                new Hash128(1, 2, 3, 4),
                new Hash128(5, 6, 7, 8),
                new Hash128(9, 9, 9, 9),
                new Hash128(6, 6, 6, 6),
                contentHash,
                "KernelRuntimeScopeGraphTests");

            return new ScopeGraphPlan(header, scopes, resolvedValueInitPlans);
        }

        static ValueInitPlanIR CreateValueInitPlan(int planId, int order, int keyId)
        {
            return new ValueInitPlanIR(
                new ValueInitPlanId(planId),
                new ModuleId(10),
                new ScopePlanId(21),
                "local:blackboard",
                LifecyclePhase.Create,
                order,
                new AvailabilityIR(KernelProfileMask.Test, true, null),
                new[]
                {
                    new ValueInitEntryIR(
                        new ValueKeyId(keyId),
                        ValueInitEntrySourceKind.DynamicEvaluation,
                        ValueKind.Float,
                        10,
                        ValueInitOverwritePolicy.Overwrite,
                        new SourceLocationId(planId + 10),
                        evaluationLocalRef: "battle.value." + planId),
                },
                new SourceLocationId(planId));
        }

        static LifecyclePlan CreateLifecyclePlan(int planId)
        {
            LifecycleIR lifecycle = new LifecycleIR(
                new LifecyclePlanId(planId),
                "ScopeGraphTransitionLifecycle" + planId,
                new ModuleId(10),
                new[]
                {
                    new LifecycleStepIR(
                        new LifecycleStepId(planId + 1),
                        LifecyclePhase.Boot,
                        10,
                        new LifecycleTargetRefIR(new ScopePlanId(21)),
                        LifecycleActionKind.ScopeStateTransition,
                        Array.Empty<DependencyEdgeId>(),
                        new SourceLocationId(planId + 2)),
                },
                new SourceLocationId(planId + 3),
                LifecycleFailurePolicy.FailOperation);

            Hash128 contentHash = KernelProjectionHashingTestAdapter.ComputeLifecyclePlanHash(new[] { lifecycle });
            VerifiedArtifactHeader header = new VerifiedArtifactHeader(
                new PlanId(planId),
                new ArtifactSetId(11),
                new ArtifactId(3),
                ArtifactKind.LifecyclePlan,
                11,
                new Hash128(1, 2, 3, 4),
                new Hash128(5, 6, 7, 8),
                new Hash128(9, 9, 9, 9),
                new Hash128(6, 6, 6, 6),
                contentHash,
                "KernelRuntimeScopeGraphTests");

            return new LifecyclePlan(header, new[] { lifecycle });
        }

        static ScopeIR CreateScope(int authoringId, int planId, ScopeAuthoringId parentAuthoringId, ScopeKind kind, ScopeServiceBoundaryKind boundaryKind, int sourceId, ScopeValueInitRefIR[]? valueInitPlans = null, UnityObjectLinkIR? unityObjectLink = null)
        {
            return new ScopeIR(
                new ScopeAuthoringId(authoringId),
                new ScopePlanId(planId),
                "Scope" + planId,
                kind,
                new ModuleId(10),
                parentAuthoringId,
                Array.Empty<ScopeServiceRequirementIR>(),
                valueInitPlans ?? Array.Empty<ScopeValueInitRefIR>(),
                boundaryKind == ScopeServiceBoundaryKind.Detached
                    ? new ScopeServiceBoundaryIR(ScopeServiceBoundaryKind.Detached, 0, new SourceLocationId(sourceId))
                    : new ScopeServiceBoundaryIR(ScopeServiceBoundaryKind.ReferencesParent, 0, new SourceLocationId(sourceId)),
                new LifecyclePlanRefIR(new LifecyclePlanId(sourceId + 100), new SourceLocationId(sourceId + 200)),
                new SourceLocationId(sourceId),
                unityObjectLink);
        }
    }
}

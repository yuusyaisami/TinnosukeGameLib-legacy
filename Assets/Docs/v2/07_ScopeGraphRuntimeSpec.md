# ScopeGraph Runtime Specification

## Document Status

- Document ID: 07_ScopeGraphRuntimeSpec
- Status: Draft
- Role: defines runtime scope instance graph, scope identity, scope state, parent-child relationships, and scope lifetime boundaries for Kernel v2
- Depends on:
  - [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md)
  - [01_KernelIRSpec.md](01_KernelIRSpec.md)
  - [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md)
  - 04_DependencyValidationSpec.md
  - 05_BootManifestAndProfileSpec.md
- Consumes:
  - ScopeIR
  - ScopeGraphPlan
  - RuntimeQueryPlan references
  - LifecyclePlan references
  - ValueInitPlan references
  - ServiceGraphPlan references
  - KernelDebugMap
- Provides foundation for:
  - 08_LifecyclePlanSpec.md
  - 10_ValueSchemaAndStoreSpec.md
  - 11_DebugMapAndDiagnosticsSpec.md
  - 12_UnityAuthoringBridgeSpec.md
  - 13_LegacyCompatBoundarySpec.md
  - 14_PerformanceBudgetAndRuntimeRulesSpec.md
  - 15_TestAndValidationSpec.md

### Ownership

This specification owns runtime scope instance structure.
It does not own Unity Transform hierarchy operations, entity lifecycle internals, or scene transition algorithms.

This specification owns:

- ScopeHandle contract
- runtime scope instance graph
- runtime parent-child relationships
- scope state machine
- scope creation and destruction contracts
- attach, detach, and reparent contracts
- scope lifetime boundaries
- scene and persistent scope boundary rules
- Unity object linkage metadata policy
- scope-local boundary contracts for ServiceGraph, Lifecycle, ValueStore, and RuntimeQuery
- pooling and generation invalidation
- ScopeGraph diagnostics
- ScopeGraph runtime performance rules
- threading rules for scope structural mutation

This specification does not own:

- final Unity authoring component schema
- final Transform reparent implementation
- entity or part lifecycle internals
- ServiceGraph cache implementation
- LifecycleDispatcher step execution algorithm
- ValueStore storage layout
- RuntimeQuery index storage
- scene transition algorithm
- loading screen visual behavior

---

## Purpose

This specification defines ScopeGraph, the runtime authority for scope instances in Kernel v2.

ScopeGraph consumes verified scope plans and manages runtime scope identity, parent-child relationships, state transitions, lifetime boundaries, and Unity object linkage metadata.

ScopeGraph exists to remove Transform-hierarchy inference, scene-wide discovery, and scope-build side effects from runtime structure management.

ScopeGraph does not discover scope structure.
ScopeGraph executes verified scope structure.

The core statement of 07 is:

```text
ScopeGraph owns runtime scope structure.
Unity hierarchy only links to it; it does not define it.
```

---

## Scope

This specification defines:

- ScopeGraph runtime responsibility
- ScopeGraphPlan input contract
- ScopeAuthoringId, ScopePlanId, ScopeHandle, and UnityObjectLink distinction
- runtime scope instance model
- parent-child relationship model
- scope state model
- scope creation and destruction contracts
- attach, detach, and reparent contracts
- scene boundary policy
- persistent scope policy
- Unity object linkage policy
- scope-local ServiceGraph boundary
- scope-local Lifecycle boundary
- scope-local ValueStore boundary
- RuntimeQuery boundary
- pooling and generation invalidation
- ScopeGraph diagnostics
- ScopeGraph failure policy
- ScopeGraph performance constraints
- ScopeGraph threading rules
- ScopeGraph test case model and required tests

---

## Non-Goals

This specification does not define:

- final Unity authoring component schema
- final Transform reparent implementation
- entity or part lifecycle internals
- ServiceGraph cache implementation
- LifecycleDispatcher step execution algorithm
- ValueStore storage layout
- RuntimeQuery index storage
- scene transition algorithm
- loading screen visual behavior

This specification must not become a generic hierarchy service specification.

---

## Relationship to Other Specs

| Spec | Relationship |
|---|---|
| [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md) | Defines that Transform hierarchy is not kernel truth and runtime discovery is forbidden |
| [01_KernelIRSpec.md](01_KernelIRSpec.md) | Defines ScopeIR, ScopeAuthoringId, ScopePlanId, and the rule that ScopeHandle does not exist in KernelIR |
| [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md) | Defines ScopeContribution as declarative input for scope ownership, parent constraints, and attachment rules |
| [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md) | Produces ScopeGraphPlan and RuntimeQueryPlan as verified generation outputs |
| 04_DependencyValidationSpec.md | Validates scope parent, owner, dependency, and boundary correctness before runtime use |
| 05_BootManifestAndProfileSpec.md | Defines boot-created root scopes and persistent boot boundary |
| 06_ServiceGraphRuntimeSpec.md | Defines service runtime; 07 owns only the scope-local service lifetime boundary |
| 08_LifecyclePlanSpec.md | Defines lifecycle steps; 07 provides scope state boundaries and lifecycle request points |
| 10_ValueSchemaAndStoreSpec.md | Defines ValueStore; 07 owns only the scope-local lifetime boundary |
| 11_DebugMapAndDiagnosticsSpec.md | Owns the shared structured diagnostics substrate and DebugMap runtime contract; 07 defines required scope runtime provenance fields and failure behavior. |
| 12_UnityAuthoringBridgeSpec.md | Defines Unity authoring bridge and object linkage details |
| 13_LegacyCompatBoundarySpec.md | Defines legacy LifetimeScope compatibility boundary |
| 14_PerformanceBudgetAndRuntimeRulesSpec.md | Defines performance budgets for ScopeGraph operations |
| 15_TestAndValidationSpec.md | Defines executable validation of scope runtime behavior |

07 consumes verified scope structure.
It must not derive scope structure from Transform hierarchy, scene search, or service registration.

---

## Current Runtime Observations

### Observation Traceability

Current runtime scope observations must remain traceable to source code, profiling evidence, validation reports, or migration notes.

When this document is updated, observations that no longer match the current codebase must be removed or moved to legacy migration notes.

| Observation | Evidence Type | Expected Downstream Spec |
|---|---|---|
| Parent scope is resolved by Transform parent walk and kind constraint | Source | 07 |
| Feature installation depends on subtree discovery and nearest-scope ownership filtering | Source / Profiling | 07, 08, 14 |
| Scope build mixes resolver construction, feature installation, acquire, and registry wiring | Source | 07, 08 |
| Scope lookup uses kind/id/category registry matching rather than verified runtime scope graph data | Source | 07 |
| Pooling exists, but generation-safe runtime handles are not the explicit authority contract | Source | 07, 14 |
| Component fallback resolution leaks hierarchy discovery back into runtime scope-related behavior | Source | 06, 07 |

### Representative Anchors

- [RuntimeLifetimeScope.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs) - current scope build, parent resolution, acquire/release flow
- [IScopeNode.cs](../../GameLib/Script/Common/LTS/Core/IScopeNode.cs) - current scope interface and lifecycle handler boundary
- [ScopeFeatureInstallerUtility.cs](../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs) - current subtree discovery and nearest-scope ownership filtering
- [ScopeBuildCoordinator.cs](../../GameLib/Script/Common/LTS/Core/ScopeBuildCoordinator.cs) - parent-before-child build coordination seed
- [ScopeNodeHierarchy.cs](../../GameLib/Script/Common/LTS/Core/ScopeNodeHierarchy.cs) - explicit parent/child table seed
- [LTSIdentityMB.cs](../../GameLib/Script/Common/LTS/Identity/MB/LTSIdentityMB.cs) - current kind/id/category metadata and kind inference behavior
- [LTSIdentityService.cs](../../GameLib/Script/Common/LTS/Identity/Core/LTSIdentityService.cs) - identity and registry coupling
- [BaseLifetimeScopeRegistry.cs](../../GameLib/Script/Common/LTS/Registry/BaseLifetimeScopeRegistry.cs) - current kind/id/category scope lookup mechanism
- [RuntimeLifetimeScopePool.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScopePool.cs) - pooling seed and slot reuse constraints
- [RuntimeResolverHub.cs](../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs) - component fallback resolution anti-pattern at scope boundary

### Current Gaps

- runtime scope parentage is still inferred from Transform hierarchy
- scope build still depends on runtime subtree discovery
- runtime scope identity, plan identity, and authoring identity are not fully separated at the contract level
- pooling and slot reuse are not expressed as generation-safe runtime handle rules
- scope lookup and runtime query semantics are still mixed with registry-style matching
- runtime scope state and lifecycle boundaries are not yet defined as one explicit runtime contract

---

## Core Problem

Legacy scope architecture mixes multiple responsibilities:

- scope ownership inferred from Transform hierarchy
- feature ownership inferred by nearest scope search
- scope build, service registration, lifecycle wiring, and acquire mixed together
- Unity object hierarchy treated as runtime kernel truth
- runtime fallback used when scope structure is missing
- pooling and reuse risks stale references if generation safety is not enforced

Target ScopeGraph must separate these responsibilities.
Transform hierarchy remains an observation link only.

---

## ScopeGraph Runtime Definition

ScopeGraph is the runtime owner of scope instances.

ScopeGraph owns:

- ScopeHandle issuance
- runtime scope instance table
- parent-child relationship table
- scope state
- scope generation safety
- scope lifetime boundary
- Unity object linkage metadata
- scope diagnostics

ScopeGraph does not own:

- service construction
- lifecycle step execution
- command execution
- value storage internals
- runtime query index implementation
- Transform hierarchy mutation

ScopeGraph is the runtime authority for scope instances.
It owns runtime scope identity, parent-child relationships, state, lifetime boundary, and Unity object linkage metadata.
It must not infer scope structure from Transform hierarchy.

---

## ScopeGraphPlan Input Contract

ScopeGraph may be created only from a verified ScopeGraphPlan.

A valid ScopeGraphPlan must provide at least:

- ScopePlanId set
- ScopeKind per scope plan
- allowed parent rules
- required root scope definitions
- required service graph references
- required value init plan references
- lifecycle plan references
- RuntimeQueryPlan references required for scope events or indexing
- source and DebugMap references
- artifact header and verified artifact set metadata

ScopeGraph must not accept ad-hoc runtime scope type registration.
It must not reconstruct missing scope plans from scene objects, prefabs, or components.

---

## Scope Identity Model

ScopeGraph distinguishes four identity layers:

- ScopeAuthoringId
- ScopePlanId
- ScopeHandle
- UnityObjectLink

Their meanings are fixed:

- ScopeAuthoringId identifies authored source
- ScopePlanId identifies a verified normalized scope definition
- ScopeHandle identifies a live runtime scope instance
- UnityObjectLink preserves traceability to Unity objects and authoring context

ScopeAuthoringId and ScopePlanId are not runtime instance handles.
ScopeHandle is not an authoring identifier.
UnityObjectLink is metadata, not identity.

Forbidden:

- using ScopeAuthoringId as a live runtime handle
- using ScopePlanId as a pooled runtime slot identifier
- storing ScopeHandle inside KernelIR
- generating ScopePlanId at runtime as fallback
- using Unity object reference as kernel scope identity

---

## ScopeHandle Model

ScopeHandle must be generation-safe.

An explanatory sketch is:

```csharp
public readonly struct ScopeHandle
{
    public readonly int Index;
    public readonly int Generation;
}
```

This sketch is explanatory only and does not finalize the runtime API.

ScopeGraph must validate:

- index range
- generation match
- target scope is not Destroyed
- requested operation is allowed for the current state

A stale ScopeHandle must not resolve to a reused scope slot.
A destroyed scope slot reused from pool must increment generation.

---

## Scope Instance Model

A runtime scope instance must contain or reference at least:

- ScopeHandle
- ScopePlanId
- ScopeAuthoringId if authored
- ScopeKind
- parent ScopeHandle or explicit root marker
- child list or child index range
- state
- generation
- owner runtime domain
- Unity object link metadata
- ServiceGraph reference if the scope owns services
- ValueStore reference if the scope owns values
- Lifecycle state or boundary reference
- DebugMap or diagnostics reference

Runtime scope instance must not treat a MonoBehaviour instance as identity.
Unity object linkage is trace metadata, not kernel identity.

---

## Scope State Model

ScopeGraph must model runtime scope state explicitly.

An explanatory sketch is:

```csharp
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
}
```

Representative transition flow:

```text
None
  -> Created
  -> Built
  -> Acquiring
  -> Active
  -> Releasing
  -> Inactive
  -> Destroying
  -> Destroyed
```

07 fixes the requirement that scope state is explicit.
08 defines lifecycle step ordering details.

Scope state must not be represented only by multiple independent bool flags.
Invalid state transitions must produce structured diagnostics.

---

## Scope Parent / Child Model

Scope parent-child relationships are explicit runtime data.

A child scope must have either:

- a valid parent ScopeHandle
- an explicit root marker allowed by ScopeGraphPlan

Parent-child relationships must not be inferred from Transform.parent.

ScopeGraph must support efficient child enumeration without whole-graph scanning.

Required invariants include:

- parent exists unless the scope is a verified root
- parent generation matches
- no parent cycle exists
- child belongs to exactly one parent at a time
- destroyed scope cannot be parent
- scene boundary rules are respected

---

## Scope Creation Contract

Scope creation requires an explicit request.

A valid ScopeCreateRequest must include at least:

- ScopePlanId
- parent ScopeHandle or explicit root marker
- runtime domain
- optional Unity object link
- creation policy
- source context for diagnostics

An explanatory sketch is:

```csharp
public readonly struct ScopeCreateRequest
{
    public ScopePlanId PlanId;
    public ScopeHandle Parent;
    public ScopeCreateMode Mode;
    public UnityObjectLinkRef UnityLink;
    public SourceLocationId Source;
}
```

ScopeGraph must not create a missing parent scope automatically.
ScopeGraph must not search the scene to find an owner scope.

---

## Scope Destruction Contract

Scope destruction must be deterministic.

Destruction must define:

- child destruction order
- lifecycle release boundary
- service graph disposal boundary
- value store disposal or persistence boundary
- runtime query invalidation boundary
- Unity link cleanup behavior
- generation invalidation

Default expectation:

```text
destroy parent scope
  -> destroy or detach children according to explicit policy
  -> request lifecycle release boundary
  -> dispose or release scope-local services
  -> dispose, persist, or reset scope-local values according to policy
  -> invalidate runtime query source state
  -> invalidate ScopeHandle generation
```

07 requires the destruction order to be explicit.
08 and 10 own the detailed lifecycle and value semantics at their boundaries.

---

## Attach / Detach / Reparent Contract

ScopeGraph reparent changes kernel parent-child relationship.
It does not directly imply Unity Transform reparent unless a lower specification defines a bridge operation.

A reparent operation must validate:

- child exists
- new parent exists
- no cycle is introduced
- scope kind parent rule allows the relationship
- scene boundary rules allow the move
- lifecycle state permits the operation

Transform.SetParent must not be the source of ScopeGraph reparent.

Detach removes kernel parent-child relationship.
It must define whether the child becomes a verified root, a suspended scope, or an invalid operation.

---

## Scene Boundary Policy

ScopeGraph must define scene domain boundaries.

Scene-local scopes must not outlive their scene domain unless explicitly promoted through a verified persistent policy.

Persistent scopes must not hold required direct references to scene-local scopes across unload unless represented by a verified weak handle, RuntimeQuery, or scene transition policy.

Scene unload must invalidate scene-local scope handles unless an explicit verified policy preserves or remaps them.

---

## Persistent Scope Policy

Persistent root scopes are created from verified boot inputs.

Examples include:

- application root
- project root
- global root
- persistent presentation root
- loading presentation root

Duplicate persistent roots are errors.
They must not be resolved by keeping one and destroying the rest at runtime.

07 consumes verified boot root intent from 05.
It does not invent persistent roots on its own.

---

## Unity Object Linkage Policy

Unity object linkage preserves traceability between runtime scope and Unity objects.

Unity object link may contain:

- GameObject reference
- Transform reference
- component reference
- source asset identity
- scene path
- prefab instance metadata

Unity object link is not scope identity.

If a linked Unity object is destroyed, ScopeGraph must not silently use Unity fake-null behavior as structure truth.

The link must become invalid, and diagnostics or lifecycle policy must decide whether the scope is destroyed, detached, or marked invalid.

ScopeGraph must not reconstruct parent-child relationship from Unity link.

---

## ServiceGraph Boundary

ScopeGraph may own or reference scope-local ServiceGraph instances.

ScopeGraph is responsible for creating and destroying the service lifetime boundary for a scope.
ServiceGraph is responsible for service resolution inside that boundary.

ServiceGraph must not decide scope parent-child structure.
ScopeGraph must not perform service construction internally except through ServiceGraph boundary APIs.

---

## Lifecycle Boundary

ScopeGraph owns scope state.
LifecyclePlan owns lifecycle steps.

ScopeGraph may request lifecycle execution at scope state boundaries.

Examples include:

- on scope acquire
- on scope release
- on scope destroy
- on scope reset

ScopeGraph must not discover lifecycle handlers by scanning services or components.

---

## ValueStore Boundary

ScopeGraph may own the lifetime of scope-local ValueStore instances.

ValueStore initialization must be performed from verified ValueInitPlan references.
ScopeGraph must not directly interpret stable value strings or dynamic value expressions.

ScopeGraph must not become Blackboard.

---

## RuntimeQuery Boundary

RuntimeQuery systems may index scope instances.

ScopeGraph must provide explicit events or change records for:

- scope created
- scope destroyed
- scope parent changed
- scope state changed
- Unity link changed

RuntimeQuery owns query indexes.
ScopeGraph owns source events.

ScopeGraph must not become a generic runtime query API for all gameplay lookups.

---

## Pooling and Generation Policy

ScopeGraph may reuse internal slots.

Slot reuse must increment generation.
A stale ScopeHandle must fail validation.

Pool reset must define:

- state reset
- parent and child cleanup
- service boundary cleanup
- value boundary cleanup
- lifecycle state cleanup
- runtime query invalidation
- Unity link cleanup

Pooled scope reuse must never preserve previous owner, parent, services, values, or Unity link unless explicitly defined by reset policy.

---

## Diagnostics and DebugMap Requirements

ScopeGraph diagnostics must include at least:

- error code
- ScopeHandle if available
- ScopePlanId
- ScopeAuthoringId if available
- ScopeKind
- parent ScopeHandle if relevant
- current state
- owner module
- source location
- Unity object link if available
- selected profile

Representative error codes include:

- SCOPE_MISSING
- SCOPE_STALE_HANDLE
- SCOPE_INVALID_GENERATION
- SCOPE_PARENT_MISSING
- SCOPE_PARENT_KIND_INVALID
- SCOPE_PARENT_CYCLE
- SCOPE_INVALID_STATE_TRANSITION
- SCOPE_SCENE_BOUNDARY_VIOLATION
- SCOPE_UNITY_LINK_DESTROYED
- SCOPE_DUPLICATE_PERSISTENT_ROOT

DebugMap and runtime mappings together must make a scope failure human-readable.
DebugMap resolves plan and authoring identity.
ScopeGraph runtime state resolves live ScopeHandle context.

---

## Failure Policy

ScopeGraph must not fallback when a required scope relationship is invalid.

Failure categories include:

- MissingParent
- StaleHandle
- InvalidGeneration
- InvalidState
- InvalidParentKind
- ParentCycle
- SceneBoundaryViolation
- DuplicatePersistentRoot
- UnityLinkInvalid
- ArtifactMismatch

Failure boundary depends on the operation:

- boot root scope failure: boot failure
- scene root failure: scene kernel failure
- scope-local operation failure: operation failure or scope failure
- stale handle: operation failure
- parent cycle: operation failure with diagnostics error

A failure must not continue through silent fallback.

---

## Performance Policy

ScopeGraph is a runtime hot path.

Target requirements:

- handle validation should be O(1)
- parent lookup should be O(1)
- child add and remove should be bounded and allocation-conscious
- no scene-wide search in normal operations
- no Transform traversal for parent inference
- no component traversal for owner inference
- no LINQ in hot paths
- no allocation in the common handle validation path

Representative profiler markers include:

- ScopeGraph.CreateScope
- ScopeGraph.DestroyScope
- ScopeGraph.ValidateHandle
- ScopeGraph.Reparent
- ScopeGraph.SetState
- ScopeGraph.NotifyQuery

---

## Threading and Main Thread Policy

ScopeGraph structural mutations are main-thread operations by default.

Unity object linkage access requires the main thread.

Read-only handle validation may be allowed outside the main thread only if a lower specification defines synchronization and Unity object access is excluded.

ScopeGraph must not touch UnityEngine.Object from worker threads unless a lower specification explicitly defines a safe proxy.

---

## Legacy Compatibility Boundary

Legacy LifetimeScope compatibility belongs to 13_LegacyCompatBoundarySpec.md.

ScopeGraph core must not depend on:

- RuntimeLifetimeScopeBase
- BaseLifetimeScope
- IRuntimeResolver
- legacy ScopeFeatureInstallerUtility
- Transform-based nearest scope search

Allowed direction is:

```text
LegacyScopeAdapter -> ScopeGraph: allowed
ScopeGraph -> LegacyScopeAdapter: forbidden
```

---

## Forbidden Patterns

The following are forbidden in target ScopeGraph runtime:

- parent inference from Transform.parent
- scope discovery through FindObjectsByType
- feature ownership detection through GetComponentsInChildren
- nearest scope search through component ancestors
- creating missing parent scope by fallback
- duplicate root cleanup by keeping first and destroying others
- using MonoBehaviour instance as scope identity
- storing runtime ScopeHandle in KernelIR
- reusing pooled slot without generation increment
- resolving runtime scope through ServiceResolver
- using ScopeGraph as generic gameplay object registry
- silently ignoring stale handles

---

## Test Case Model

Each ScopeGraph test case must define:

- Test ID
- Title
- ScopeGraphPlan fixture
- initial ScopeGraph state
- operation
- expected result
- expected diagnostics
- expected state transition
- expected handle validity
- expected performance assertion if applicable

---

## Required Test Cases

### Identity and Handle Tests

#### TC_SCOPE_ID_001_CreateScopeReturnsValidHandle

Expected result:

- creating a verified root scope returns a valid ScopeHandle
- ScopeHandle resolves to the requested ScopePlanId

#### TC_SCOPE_ID_002_AuthoringIdIsNotRuntimeHandle

Expected result:

- using ScopeAuthoringId as ScopeHandle fails with a domain mismatch diagnostic

#### TC_SCOPE_ID_003_StaleHandleRejected

Expected result:

- after slot reuse, the old ScopeHandle is rejected with SCOPE_STALE_HANDLE

### Parent and Child Tests

#### TC_SCOPE_PARENT_001_CreateChildWithValidParent

Expected result:

- child parent equals the provided parent handle
- parent child set contains the child

#### TC_SCOPE_PARENT_002_MissingParentRejected

Expected result:

- creating a child with missing parent fails with SCOPE_PARENT_MISSING

#### TC_SCOPE_PARENT_003_InvalidParentKindRejected

Expected result:

- invalid parent-kind relationship fails with SCOPE_PARENT_KIND_INVALID

#### TC_SCOPE_PARENT_004_ParentCycleRejected

Expected result:

- reparent cycle introduction fails with SCOPE_PARENT_CYCLE

#### TC_SCOPE_PARENT_005_TransformParentChangeDoesNotChangeScopeParent

Expected result:

- changing Unity Transform parent alone does not change ScopeGraph parent data

### State Tests

#### TC_SCOPE_STATE_001_ValidStateTransition

Expected result:

- valid transition sequence Created -> Built -> Active succeeds

#### TC_SCOPE_STATE_002_InvalidStateTransitionRejected

Expected result:

- invalid transition Destroyed -> Active fails with SCOPE_INVALID_STATE_TRANSITION

#### TC_SCOPE_STATE_003_DestroyedScopeCannotBeParent

Expected result:

- destroyed parent scope cannot accept child creation

### Creation and Destruction Tests

#### TC_SCOPE_CREATE_001_NoSceneSearchDuringCreate

Expected result:

- create path performs no FindObjectsByType
- create path performs no Transform parent traversal for ownership inference

#### TC_SCOPE_DESTROY_001_DestroyInvalidatesChildren

Expected result:

- parent destruction invalidates or explicitly processes children according to policy

#### TC_SCOPE_DESTROY_002_DestroyDisposesScopeBoundaries

Expected result:

- service, value, lifecycle, and query boundaries are cleaned up according to explicit policy

### Reparent Tests

#### TC_SCOPE_REPARENT_001_ReparentValidScope

Expected result:

- child is removed from ParentA and attached to ParentB after validation passes

#### TC_SCOPE_REPARENT_002_ReparentAcrossInvalidSceneBoundaryRejected

Expected result:

- invalid scene-boundary reparent fails with SCOPE_SCENE_BOUNDARY_VIOLATION

#### TC_SCOPE_REPARENT_003_SetParentIsNotScopeReparent

Expected result:

- Unity Transform.SetParent does not mutate ScopeGraph relationship without explicit bridge command

### Persistent Root Tests

#### TC_SCOPE_ROOT_001_CreateRequiredRootFromBootPlan

Expected result:

- required persistent root defined by boot input is created successfully

#### TC_SCOPE_ROOT_002_DuplicatePersistentRootRejected

Expected result:

- duplicate persistent roots fail with SCOPE_DUPLICATE_PERSISTENT_ROOT

#### TC_SCOPE_ROOT_003_NoKeepFirstDestroyRest

Expected result:

- duplicate persistent roots do not trigger automatic keep-first cleanup

### Unity Link Tests

#### TC_SCOPE_UNITY_001_UnityLinkIsTraceMetadata

Expected result:

- scope identity remains ScopeHandle, not Unity object reference

#### TC_SCOPE_UNITY_002_DestroyedUnityLinkDetected

Expected result:

- destroyed linked Unity object invalidates the link and produces explicit policy outcome

#### TC_SCOPE_UNITY_003_NoParentInferenceFromUnityLink

Expected result:

- Unity link Transform parent does not define scope parentage

### Boundary Tests

#### TC_SCOPE_BOUNDARY_001_ServiceGraphDoesNotOwnScopeParent

Expected result:

- ServiceGraph cannot determine or mutate scope parent-child structure

#### TC_SCOPE_BOUNDARY_002_ScopeGraphDoesNotExecuteLifecycleDirectly

Expected result:

- ScopeGraph requests lifecycle boundary work, but LifecycleDispatcher owns step execution

#### TC_SCOPE_BOUNDARY_003_ScopeGraphDoesNotResolveValueKeys

Expected result:

- ScopeGraph cannot interpret stable value keys directly

#### TC_SCOPE_BOUNDARY_004_ScopeGraphDoesNotBecomeRuntimeQueryRegistry

Expected result:

- gameplay lookup by category is delegated to RuntimeQuery or rejected

### Pooling Tests

#### TC_SCOPE_POOL_001_ReusedSlotIncrementsGeneration

Expected result:

- slot reuse increments generation and invalidates old handle

#### TC_SCOPE_POOL_002_ResetClearsParentChildrenAndLinks

Expected result:

- reset clears parent, children, Unity link, and scope-local boundary references

### Diagnostics Tests

#### TC_SCOPE_DIAG_001_StaleHandleDiagnosticReadable

Expected result:

- stale handle diagnostic includes handle, generation, ScopePlanId, and authoring source when available

#### TC_SCOPE_DIAG_002_MissingDebugMapInDevelopment

Expected result:

- Development profile failure without required DebugMap coverage is treated as error

### Performance Tests

#### TC_SCOPE_PERF_001_ValidateHandleNoAllocation

Expected result:

- repeated handle validation does not allocate on the normal path

#### TC_SCOPE_PERF_002_CreateScopeDoesNotScanHierarchy

Expected result:

- create path performs no GetComponentsInChildren and no Transform parent traversal

#### TC_SCOPE_PERF_003_ChildEnumerationDoesNotScanAllScopes

Expected result:

- child enumeration cost is bounded by child count, not total scope count

---

## Acceptance Criteria

This specification is complete when it defines:

- ScopeGraph runtime responsibility
- ScopeGraphPlan input contract
- ScopeAuthoringId, ScopePlanId, ScopeHandle, and UnityObjectLink distinction
- ScopeHandle generation safety
- runtime scope instance model
- scope state model
- parent-child relationship model
- scope creation contract
- scope destruction contract
- attach, detach, and reparent contract
- scene boundary policy
- persistent scope policy
- Unity object linkage policy
- ServiceGraph boundary
- Lifecycle boundary
- ValueStore boundary
- RuntimeQuery boundary
- pooling and generation policy
- diagnostics and DebugMap requirements
- failure policy
- performance policy
- threading policy
- legacy compatibility boundary
- forbidden patterns
- ScopeGraph test case model
- required ScopeGraph test cases

07 is not complete if it becomes a Transform hierarchy management specification, a generic hierarchy service, or a replacement for ServiceGraph, LifecyclePlan, ValueStore, or RuntimeQuery.

## Test Cases

| Test Case | Purpose | Verification |
|---|---|---|
| TC-07-01 | Confirm ScopeGraph, not Transform hierarchy, is the runtime authority for scope structure. | The Purpose, ScopeGraph Runtime Definition, and Forbidden Patterns sections must forbid Transform-parent inference and scene or component discovery. |
| TC-07-02 | Confirm ScopeAuthoringId, ScopePlanId, ScopeHandle, and UnityObjectLink are not mixed. | The Scope Identity Model, ScopeHandle Model, and Scope Instance Model sections must define separate identity layers and reject stale or cross-domain handle use. |
| TC-07-03 | Confirm scope creation, destruction, and reparent require explicit verified structure. | The ScopeGraphPlan Input Contract, Scope Creation Contract, Scope Destruction Contract, and Attach / Detach / Reparent Contract sections must require verified input, explicit parent rules, and no fallback parent creation. |
| TC-07-04 | Confirm scope boundaries with ServiceGraph, Lifecycle, ValueStore, RuntimeQuery, and Unity linkage remain explicit. | The boundary sections must define ownership and non-ownership clearly and forbid ScopeGraph from becoming service resolution, lifecycle execution, Blackboard, or generic query registry. |
| TC-07-05 | Confirm pooling, scene boundaries, and persistent roots fail closed. | The Scene Boundary Policy, Persistent Scope Policy, Pooling and Generation Policy, and Failure Policy sections must reject stale handles, scene leaks, and duplicate persistent roots without silent cleanup. |
| TC-07-06 | Confirm diagnostics, performance, threading, and legacy rules are part of the runtime contract. | The Diagnostics and DebugMap Requirements, Performance Policy, Threading and Main Thread Policy, and Legacy Compatibility Boundary sections must remain explicit and testable. |

---

## Final Position

Transform is not truth.
ScopeGraph is truth.

ScopeGraph owns runtime scope structure.
Unity hierarchy only links to it; it does not define it.

By separating ScopeAuthoringId, ScopePlanId, ScopeHandle, and UnityObjectLink, the kernel can preserve speed, safety, and debuggability without returning to Transform inference or legacy nearest-scope discovery.
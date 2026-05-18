# Lifecycle Plan Specification

## Document Status

- Document ID: 08_LifecyclePlanSpec
- Status: Draft
- Role: defines lifecycle participation, lifecycle dispatch, phase ordering, tick policy, and lifecycle failure rules for Kernel v2
- Depends on:
  - [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md)
  - [01_KernelIRSpec.md](01_KernelIRSpec.md)
  - [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md)
  - [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md)
  - [04_DependencyValidationSpec.md](04_DependencyValidationSpec.md)
  - [05_BootManifestAndProfileSpec.md](05_BootManifestAndProfileSpec.md)
  - [06_ServiceGraphRuntimeSpec.md](06_ServiceGraphRuntimeSpec.md)
  - [07_ScopeGraphRuntimeSpec.md](07_ScopeGraphRuntimeSpec.md)
- Consumes:
  - LifecycleIR
  - LifecyclePlan
  - ServiceGraphPlan references
  - ScopeGraphPlan references
  - RuntimeQueryPlan references
  - ValueInitPlan references
  - KernelDebugMap
- Provides foundation for:
  - 09_CommandCatalogRuntimeSpec.md
  - 10_ValueSchemaAndStoreSpec.md
  - 11_DebugMapAndDiagnosticsSpec.md
  - 12_UnityAuthoringBridgeSpec.md
  - 13_LegacyCompatBoundarySpec.md
  - 14_PerformanceBudgetAndRuntimeRulesSpec.md
  - 15_TestAndValidationSpec.md

### Ownership

This specification owns lifecycle participation, lifecycle dispatch, phase ordering, tick participation policy, reset policy, and lifecycle failure behavior.
It does not own service caching, scope structure, command execution, value storage layout, runtime query storage, or Unity MonoBehaviour lifecycle semantics.

This specification owns:

- LifecyclePlan runtime authority
- lifecycle phase semantics
- lifecycle step semantics
- lifecycle target semantics
- lifecycle ordering rules
- lifecycle dependency and rollback requirements
- lifecycle dispatch table rules
- scope-boundary lifecycle dispatch contract
- service-boundary lifecycle dispatch contract
- runtime object ownership boundary for lifecycle
- tick, fixed tick, and late tick policy
- per-entity lifecycle prohibition
- lifecycle failure policy
- reset and pooling lifecycle policy
- async lifecycle policy
- lifecycle diagnostics and DebugMap requirements
- lifecycle performance and memory rules
- legacy lifecycle migration policy

This specification does not own:

- ServiceGraph cache implementation
- ScopeGraph parent-child implementation
- CommandCatalog dispatch
- ValueStore storage layout
- RuntimeQuery index implementation
- Unity update loop implementation details

08 is the runtime lifecycle authority.
It is not a replacement for ServiceGraph, ScopeGraph, CommandCatalog, or ValueStore.

---

## Purpose

This specification defines how lifecycle participation is declared, validated, and dispatched in Kernel v2.

LifecyclePlan owns lifecycle participation.
ServiceGraph only resolves explicitly targeted services.
Implemented interfaces are not lifecycle enrollment.

The core statement of 08 is:

```text
Lifecycle is declared, validated, and dispatched from plan.
It is never discovered from runtime registrations.
```

If lifecycle participation is discovered by scanning runtime services, the architecture has already regressed.

---

## Scope

This specification defines:

- LifecyclePlan runtime responsibility
- lifecycle phase, step, target, and ordering rules
- lifecycle dependency rules
- precomputed dispatch table rules
- lifecycle boundaries with ScopeGraph, ServiceGraph, RuntimeQuery, ValueStore, and runtime object owners
- tick, fixed tick, late tick, and manual tick policy
- lifecycle failure and rollback behavior
- reset and pooling policy
- async lifecycle constraints
- lifecycle diagnostics and DebugMap requirements
- lifecycle performance and memory rules
- lifecycle migration rules
- lifecycle test case model and required tests

---

## Non-Goals

This specification does not define:

- final ServiceGraph cache implementation
- final ScopeGraph handle layout
- final CommandCatalog execution algorithm
- final ValueStore storage layout
- final RuntimeQuery index storage
- Unity MonoBehaviour lifecycle itself
- Unity PlayerLoop customization details

This specification must not turn lifecycle into:

- a registration-scan subsystem
- a generic runtime callback bus
- a service registry
- a command registry
- a per-entity update table

---

## Relationship to Other Specs

| Spec | Relationship |
|---|---|
| [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md) | Defines lifecycle ordering as explicit plan data and forbids registration-driven handler discovery. |
| [01_KernelIRSpec.md](01_KernelIRSpec.md) | Defines LifecycleIR, LifecycleStepIR, LifecycleTargetRefIR, and lifecycle identity vocabulary consumed here. |
| [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md) | Defines LifecycleContribution as declarative input and rejects interface auto-collection as the target model. |
| [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md) | Produces LifecyclePlan as a verified projection and forbids implicit lifecycle step creation during projection. |
| [04_DependencyValidationSpec.md](04_DependencyValidationSpec.md) | Validates lifecycle target correctness, phase cycles, ordering determinism, and explicit lifecycle participation before runtime use. |
| [05_BootManifestAndProfileSpec.md](05_BootManifestAndProfileSpec.md) | Defines boot acceptance and allows boot lifecycle phases only from validated lifecycle plans. |
| [06_ServiceGraphRuntimeSpec.md](06_ServiceGraphRuntimeSpec.md) | Resolves explicit service targets only; 08 owns lifecycle participation and dispatch. |
| [07_ScopeGraphRuntimeSpec.md](07_ScopeGraphRuntimeSpec.md) | Owns scope state and requests lifecycle dispatch at state boundaries; 08 owns step execution and dispatch policy. |
| 09_CommandCatalogRuntimeSpec.md | Owns command dispatch; 08 only defines lifecycle boundaries around command-related services or adapters. |
| 10_ValueSchemaAndStoreSpec.md | Owns values and dynamic evaluation; 08 only defines lifecycle boundaries around value-related targets. |
| 11_DebugMapAndDiagnosticsSpec.md | Owns diagnostics presentation; 08 defines required lifecycle runtime provenance fields. |
| 12_UnityAuthoringBridgeSpec.md | Owns authoring-side lifecycle contribution sources and Unity binding generation. |
| 13_LegacyCompatBoundarySpec.md | Owns migration-only lifecycle adapters and their removal boundary. |
| 14_PerformanceBudgetAndRuntimeRulesSpec.md | Owns measurable lifecycle budgets and runtime markers referenced here. |
| 15_TestAndValidationSpec.md | Turns lifecycle dispatch rules and failure boundaries into executable test coverage. |

08 consumes verified lifecycle structure.
It must not discover lifecycle structure from service registrations, component search, or interface enumeration.

---

## Current Lifecycle Debt Observations

### Observation Traceability

Current lifecycle observations must remain traceable to source code, profiling evidence, or migration notes.

When this document is updated, observations that no longer match the current codebase must be removed or moved to legacy migration notes.

| Observation | Evidence Type | Expected Downstream Spec |
|---|---|---|
| Lifecycle enrollment is still discovered from runtime registrations and implemented interfaces. | Source | 08 |
| Acquire and release dispatch still relies on collected handler arrays rather than verified step plans. | Source | 08 |
| Runtime scope build still freezes handler arrays and tick arrays during resolver construction. | Source | 08, 07 |
| Owner inference for lifecycle handlers still depends on reflection or nearest-scope discovery. | Source | 08, 07 |
| Tick participation still depends on mutable handler lists and registration/unregistration side effects. | Source | 08, 14 |
| Scope lifecycle helpers still perform fire-and-forget async work and command/value fallback from tick paths. | Source | 08, 09, 10 |
| Representative channel and hub services still mix lifecycle participation, target resolution, runtime object ownership, and fallback repair in one class. | Source | 08, 06, 10 |

### Representative Anchors

- [IScopeNode.cs](../../GameLib/Script/Common/LTS/Core/IScopeNode.cs) - lifecycle handler interfaces, `ScopeAcquireReleaseDispatcher`, and reflection-based `ScopeHandlerOwnershipUtility`
- [RuntimeResolverHub.cs](../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs) - `CollectHandlers<THandler>()`, `GetAcquireHandlers()`, `GetReleaseHandlers()`, `GetTickHandlers()`, and `RuntimeAcquireReleaseDispatcher`
- [RuntimeLifetimeScope.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs) - resolver build, handler array caching, tick registration, and acquire/release dispatch
- [RuntimeTickHub.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeTickHub.cs) - mutable tick lists, registration-based tick enrollment, and phase split behavior
- [ScopeLifecycleMB.cs](../../GameLib/Script/Common/LTS/Lifecycle/MB/ScopeLifecycleMB.cs) - installer mutation that enrolls lifecycle behavior through handler interfaces
- [ScopeLifecycleService.cs](../../GameLib/Script/Common/LTS/Lifecycle/Service/ScopeLifecycleService.cs) - fire-and-forget `UniTask.Void`, command fallback, and value fallback in lifecycle logic
- [RuntimeScopeLifecycleService.cs](../../GameLib/Script/Common/LTS/Lifecycle/Service/RuntimeScopeLifecycleService.cs) - async despawn from tick, parent traversal for command runner lookup, and runtime key fallback
- [TooltipChannelHubService.cs](../../GameLib/Script/Project/UI/Core/Tooltip/TooltipChannelHubService.cs) - representative mixed acquire, tick, query, camera fallback, and runtime object ownership service
- [MeshChannelHubService.cs](../../GameLib/Script/Project/Scene/Channels/Mesh/MeshChannelHubService.cs) - representative hub-owned player runtime lifecycle service
- [AnimationSpriteHubService.cs](../../GameLib/Script/Project/Scene/Channels/SpriteAnimation/AnimationSpriteHubService.cs) - representative hub service that mixes lifecycle, provider contracts, and runtime player ownership

These examples are representative, not exhaustive.
Handler-interface-based services are widespread in the current repo and are broadly in scope for migration.

### Current Gaps

The current codebase still exposes lifecycle behavior that 08 must remove from target architecture:

- lifecycle truth is still registration-driven
- acquire, release, and tick enrollment are still hidden behind service contracts
- lifecycle ordering is still influenced by collection behavior
- tick participation still scales with collected handler count rather than explicit lifecycle budget
- lifecycle owner resolution still depends on discovery logic
- async lifecycle work can still escape structured failure handling
- hub-owned runtime objects are not consistently separated from direct lifecycle targets

---

## Lifecycle Authority

LifecyclePlan is the only runtime authority for lifecycle participation.

A service, runtime object, or scope does not participate in lifecycle because it implements an interface.
It participates only when a LifecycleStep exists in a verified LifecyclePlan.

Implemented interfaces may exist as legacy adapter details, but they must not be used by the target LifecycleDispatcher for enrollment.

Forbidden:

- scanning services for `IScopeAcquireHandler`
- scanning services for `IScopeReleaseHandler`
- scanning services for `IScopeTickHandler`
- scanning services for `IScopeLateTickHandler`
- scanning services for `IScopeFixedTickHandler`
- collecting `IReadOnlyList<IScopeTickHandler>`
- registering lifecycle participation through `.As<IScopeAcquireHandler>()`
- adding lifecycle steps at runtime by service registration

---

## LifecyclePlan Input Contract

LifecycleDispatcher may execute only a verified LifecyclePlan from one verified artifact set.

A valid LifecyclePlan input must provide at least:

- LifecyclePlanId
- LifecycleStepId set
- explicit phase per step
- explicit target reference per step
- explicit action per step
- explicit order metadata
- explicit dependency metadata
- explicit failure policy or defaultable failure boundary
- source provenance and DebugMap linkage
- verified artifact set metadata

LifecyclePlan may be accompanied by runtime-local or artifact-local precomputed dispatch tables derived from the plan.
The authority remains the verified plan, not the cached table itself.

Lifecycle input must not:

- add steps at runtime
- reconstruct steps from registrations
- accept partial artifact sets
- hide boot-only or scope-only lifecycle behavior inside service builders

---

## Lifecycle Identity Model

Lifecycle runtime uses explicit, typed lifecycle identities.

The lifecycle vocabulary is:

- `LifecyclePlanId`
- `LifecycleStepId`
- `LifecyclePhase`
- `LifecycleActionKind`
- `LifecycleTargetKind`
- `LifecycleFailurePolicy`
- `LifecycleTickGroup`
- `LifecycleDispatchTable`

`LifecyclePlanId`, `LifecycleStepId`, `LifecyclePhase`, `LifecycleActionKind`, and `LifecycleTargetRefIR` are rooted in the IR owned by 01.
08 defines their runtime meaning and dispatch constraints.

Lifecycle identity must not fall back to:

- raw `Type`
- interface implementation
- registration order
- GameObject name
- Transform path
- arbitrary string lookup

---

## Lifecycle Phase Model

Lifecycle phases are explicit and ordered.

An explanatory lifecycle phase model is:

```csharp
public enum LifecyclePhase
{
    Boot = 10,
    Create = 20,
    Build = 30,
    Acquire = 40,
    Activate = 50,
    Tick = 60,
    FixedTick = 70,
    LateTick = 80,
    PreRelease = 90,
    Release = 100,
    Reset = 110,
    Destroy = 120,
    Dispose = 130,
}
```

Intent:

- `Build` prepares immutable or structural runtime state from verified inputs
- `Acquire` binds the target into an active scope or runtime boundary
- `Activate` begins active participation such as visibility, input, or time-driven behavior
- `Tick`, `FixedTick`, and `LateTick` perform ongoing updates
- `PreRelease` stops outward activity before detaching
- `Release` detaches runtime bindings
- `Reset` clears state for verified reuse
- `Destroy` ends lifecycle ownership
- `Dispose` releases remaining resources

Unity MonoBehaviour callback order is not the lifecycle truth model.

---

## Lifecycle Step Model

LifecyclePlan is a list of explicit lifecycle steps.

An explanatory runtime step sketch is:

```csharp
public sealed class LifecycleStepPlan
{
    public LifecycleStepId StepId;
    public LifecyclePhase Phase;
    public LifecycleTargetRefIR Target;
    public LifecycleActionKind Action;
    public int Order;
    public LifecycleStepId[] Dependencies;
    public LifecycleFailurePolicy FailurePolicy;
    public SourceLocationId Source;
}
```

An explanatory action vocabulary is:

```csharp
public enum LifecycleActionKind
{
    ServiceMethod = 10,
    GeneratedStaticCall = 20,
    ScopeStateTransition = 30,
    RuntimeObjectOwnerCall = 40,
    ValueInit = 50,
    RuntimeQueryNotify = 60,
    LegacyAdapterCall = 90,
}
```

`RuntimeObjectOwnerCall` exists so that local player runtimes and other hub-owned objects are managed through their owner, not exploded into one lifecycle step per local runtime object.

---

## Lifecycle Target Model

Lifecycle targets are explicit and typed.

An explanatory target model is:

```csharp
public enum LifecycleTargetKind
{
    Service = 10,
    Scope = 20,
    ValueStore = 30,
    RuntimeQuery = 40,
    RuntimeObjectOwner = 50,
    LegacyAdapter = 90,
}
```

Allowed target categories include:

- `ServiceId`
- `ScopePlanId` or equivalent scope boundary reference
- explicit ValueStore boundary reference
- `RuntimeQueryId`
- hub-owned runtime object owner reference
- explicit legacy adapter target

Forbidden target resolution includes:

- raw type scan
- interface scan
- scene search
- Transform hierarchy search
- arbitrary string lookup

Runtime object owners are explicit lifecycle targets only when a lower spec defines the owner namespace and diagnostics boundary.
They are not permission to make every local runtime object a first-class lifecycle participant.

---

## Lifecycle Ordering Model

Lifecycle ordering is deterministic.

Ordering rules:

- phase order is explicit
- step order within a phase is explicit
- dependency edges may add stricter ordering
- registration order is never the authority
- same-phase ties without explicit tie policy are invalid

Validation must reject:

- non-deterministic same-phase order
- hidden ordering derived from collection order
- dependency cycles that violate the selected phase model

Generated or runtime-local dispatch tables may sort by order and dependency resolution.
They must not invent new ordering truth.

---

## Lifecycle Dependency Model

Lifecycle dependencies are explicit and phase-aware.

Lifecycle dependency rules include:

- a step may depend on one or more earlier steps
- dependency meaning is scoped to lifecycle semantics, not generic service resolution
- dependencies must be valid for the selected phase
- acquire-time and build-time cycles are invalid unless a lower spec explicitly defines a verified exception
- boot lifecycle dependencies must remain compatible with boot acceptance rules

Lifecycle dependency modeling must define rollback expectations where partial execution is possible.

If an `Acquire` step fails after earlier acquire steps succeeded, the plan must define:

- whether completed steps are released in reverse order
- whether the scope enters a failed or inactive state
- whether runtime query invalidation occurs
- how diagnostics are emitted

Validation algorithms remain owned by 04.
08 defines the runtime contract those validations protect.

---

## Lifecycle Dispatch Table Model

LifecycleDispatcher executes precomputed dispatch tables generated from LifecyclePlan.

A dispatch table is grouped by:

- lifecycle phase
- runtime domain
- scope plan or scope kind when applicable
- tick group when applicable
- deterministic order

An explanatory runtime table sketch is:

```text
LifecycleDispatchTable
  BootSteps[]
  CreateSteps[]
  BuildSteps[]
  AcquireSteps[]
  ActivateSteps[]
  TickSteps[]
  FixedTickSteps[]
  LateTickSteps[]
  PreReleaseSteps[]
  ReleaseSteps[]
  ResetSteps[]
  DestroySteps[]
  DisposeSteps[]
```

LifecycleDispatcher must not build dispatch tables by scanning ServiceGraph registrations at runtime.

Dispatch tables are runtime execution data.
They are not permission to derive lifecycle truth from mutable registrations.

---

## Scope State Boundary

ScopeGraph owns scope state.
LifecyclePlan owns side effects executed at scope state boundaries.

ScopeGraph may request lifecycle dispatch when a scope enters or exits a state.

Examples include:

- built to acquiring
- acquiring to active
- active to releasing
- releasing to inactive
- inactive to reset
- inactive to destroyed

LifecycleDispatcher must not mutate ScopeGraph parent-child structure directly.

07 remains the owner of scope structure and state transitions.
08 owns the dispatch work requested at those boundaries.

---

## ServiceGraph Boundary

ServiceGraph resolves explicit lifecycle target services.
ServiceGraph does not discover lifecycle targets.

LifecycleDispatcher may request a service instance by ServiceId only when the LifecycleStepPlan explicitly references that ServiceId.

ServiceGraph must not provide:

- `GetAcquireHandlers()`
- `GetReleaseHandlers()`
- `GetTickHandlers()`
- `GetLateTickHandlers()`
- `GetFixedTickHandlers()`
- `IReadOnlyList<IScopeTickHandler>`

06 owns service resolution.
08 owns lifecycle participation and dispatch.

---

## Runtime Object Boundary

LifecyclePlan should not enroll every local runtime object directly.

Hub-owned runtime objects are normally managed by their owner target.

Examples:

- tooltip hubs own tooltip player runtimes
- mesh hubs own mesh player runtimes
- animation sprite hubs own animation channel runtimes

The owner hub may be a lifecycle target.
The internal player runtimes are not LifecyclePlan targets by default.

These examples are representative, not exhaustive.
The same rule applies broadly across runtime object families.

---

## Tick / FixedTick / LateTick Policy

Tick participation must be explicit and budgeted.

Tick steps must declare:

- tick phase
- tick group
- order
- expected cardinality
- time domain
- pause behavior
- failure policy

An explanatory tick-group model is:

```csharp
public enum LifecycleTickGroup
{
    Kernel = 10,
    Project = 20,
    Scene = 30,
    UI = 40,
    Presentation = 50,
    Simulation = 60,
    Debug = 90,
}
```

An explanatory time-domain model is:

```csharp
public enum LifecycleTimeDomain
{
    UnityTime = 10,
    ScaledGameTime = 20,
    UnscaledGameTime = 30,
    FixedSimulationTime = 40,
    Manual = 50,
}
```

Tick steps must not implicitly use `UnityEngine.Time` unless the step declares a compatible time domain.

Per-entity tick step generation is forbidden by default.

---

## Per-Entity Lifecycle Prohibition

LifecyclePlan must not create one lifecycle step per:

- entity
- part
- renderer
- tooltip view
- mesh track
- animation player
- command instance

Per-target runtime updates belong to:

- hub-owned local runtime loops
- EntityRuntime systems
- ValueStore processing
- RuntimeQuery systems
- batched simulation services

A per-entity lifecycle exception is allowed only when all are true:

- the entity is a bounded authored aggregate root
- expected instance count is declared
- performance budget is declared
- lifecycle ownership is explicit
- diagnostics can identify the source and runtime handle

The default remains prohibition.

---

## Failure Policy

Lifecycle failure must not be silently ignored.

An explanatory failure policy model is:

```csharp
public enum LifecycleFailurePolicy
{
    FailOperation = 10,
    FailScope = 20,
    FailScene = 30,
    FailKernel = 40,
    ContinueWithError = 50,
}
```

Default boundaries:

- boot lifecycle defaults to `FailKernel`
- scope lifecycle defaults to `FailScope`

`ContinueWithError` requires explicit policy and profile justification.
It is not the default.

If a failure boundary is not declared, the runtime must apply the default boundary rather than continue silently.

---

## Reset and Pooling Policy

Reset is separate from release and destroy.

- `Release` detaches runtime from active scope usage
- `Reset` clears runtime state before reuse
- `Destroy` ends lifetime and disposes resources

Pooled scope reuse must run a verified reset sequence before acquire.

A scope or service reused from pool must not retain:

- previous subscriptions
- previous runtime query entries
- previous ValueStore transient state
- previous owner references
- previous channel player runtime state unless explicitly retained by policy

Release does not imply reset.
Reset does not imply destroy.

---

## Async / UniTask Policy

Lifecycle steps are synchronous by default.

Async lifecycle is allowed only through explicit tracked async lifecycle steps.

An async lifecycle step must define:

- cancellation source
- timeout policy
- failure boundary
- completion requirement
- whether the next step waits
- diagnostics on cancellation and failure

Fire-and-forget lifecycle work is forbidden.

`UniTask.Void` is legacy migration debt, not target lifecycle policy.

---

## Diagnostics and DebugMap Requirements

Lifecycle diagnostics must include:

- `LifecyclePlanId`
- `LifecycleStepId`
- phase
- target kind
- target reference
- owner module
- source location
- order
- failure policy
- selected profile

Representative lifecycle runtime error codes include:

- `LIFECYCLE_STEP_TARGET_MISSING`
- `LIFECYCLE_STEP_ORDER_CONFLICT`
- `LIFECYCLE_PHASE_CYCLE`
- `LIFECYCLE_INTERFACE_DISCOVERY_FORBIDDEN`
- `LIFECYCLE_REGISTRATION_SCAN_FORBIDDEN`
- `LIFECYCLE_TICK_CARDINALITY_FORBIDDEN`
- `LIFECYCLE_ASYNC_UNTRACKED`
- `LIFECYCLE_PARTIAL_ACQUIRE_FAILED`
- `LIFECYCLE_RESET_REQUIRED_BEFORE_REUSE`

A lifecycle failure without step provenance is a diagnostics degradation.

---

## Performance and Memory Policy

Lifecycle dispatch is a runtime hot path.

Target requirements:

- no registration scan during dispatch
- no interface scan during dispatch
- no LINQ in dispatch path
- no managed allocation in normal tick dispatch
- dispatch cost scales with active lifecycle steps, not registered services
- tick step count is explicit budget input
- per-entity tick steps are forbidden by default

Increasing service count must not automatically increase tick count.
Increasing command executor count must not automatically increase lifecycle count.

Lifecycle runtime should expose enough marker points for 14 to budget at least:

- `Lifecycle.DispatchBoot`
- `Lifecycle.DispatchAcquire`
- `Lifecycle.DispatchTick`
- `Lifecycle.DispatchLateTick`
- `Lifecycle.DispatchFixedTick`
- `Lifecycle.DispatchRelease`
- `Lifecycle.DispatchReset`

---

## Legacy Migration Policy

Legacy handler-interface patterns must migrate into explicit lifecycle contributions and plans.

| Legacy Pattern | Target Representation |
|---|---|
| `.As<IScopeAcquireHandler>()` | `LifecycleContribution` phase `Acquire` |
| `.As<IScopeReleaseHandler>()` | `LifecycleContribution` phase `Release` |
| `.As<IScopeTickHandler>()` | `LifecycleContribution` phase `Tick` |
| `.As<IScopeLateTickHandler>()` | `LifecycleContribution` phase `LateTick` |
| `.As<IScopeFixedTickHandler>()` | `LifecycleContribution` phase `FixedTick` |
| `RuntimeResolver.GetAcquireHandlers()` | generated `LifecycleDispatchTable` |
| `RuntimeAcquireReleaseDispatcher` | `LifecycleDispatcher` |
| service implements handler interface | not enrollment |

Representative migration examples:

- `TooltipChannelHubService`
  - `ServiceContribution`: tooltip hub
  - `LifecycleContribution`: acquire, release, tick
  - local tooltip player runtimes remain hub-owned internal state
- `MeshChannelHubService`
  - `ServiceContribution`: mesh hub
  - `LifecycleContribution`: acquire, release, tick, dispose
  - mesh player runtimes remain hub-owned internal state
- `AnimationSpriteHubService`
  - `ServiceContribution`: animation sprite hub
  - `LifecycleContribution`: acquire, release, tick
  - provider contracts remain separate from lifecycle enrollment

These examples are representative only.
The migration policy applies broadly across the current handler-interface service population.

---

## Forbidden Patterns

The following are forbidden in target LifecyclePlan runtime:

- lifecycle enrollment by implemented interface
- scanning ServiceGraph for `IScopeAcquireHandler`
- scanning ServiceGraph for `IScopeReleaseHandler`
- scanning ServiceGraph for `IScopeTickHandler`
- resolving `IReadOnlyList<IScopeTickHandler>`
- registration order as lifecycle order
- runtime-generated lifecycle steps
- adding lifecycle handlers during acquire
- per-entity lifecycle steps by default
- per-channel-player lifecycle steps by default
- fire-and-forget async lifecycle work
- swallowing lifecycle failures
- continuing after required acquire failure without explicit failure boundary
- using ServiceGraph as lifecycle registry
- using CommandCatalog as lifecycle registry

---

## Test Case Model

Each lifecycle runtime test case must define:

- Test ID
- Title
- LifecyclePlan fixture
- ServiceGraphPlan and ScopeGraphPlan fixtures when needed
- selected profile if relevant
- operation under test
- expected result
- expected diagnostics
- expected failure boundary
- notes

Example:

### TC_LIFE_001_InterfaceImplementationDoesNotEnroll

Input:

- service implements an acquire-like interface
- no lifecycle step exists

Operation:

- dispatch acquire for the containing scope

Expected:

- service is not called
- lifecycle runtime remains plan-driven

---

## Required Test Cases

### A. Enrollment Tests

#### TC_LIFE_ENROLL_001_InterfaceImplementationDoesNotEnroll

Input:

- service implements an acquire-like interface
- no lifecycle step exists

Expected:

- service is not called during acquire

#### TC_LIFE_ENROLL_002_LifecycleStepEnrollsService

Input:

- lifecycle step targets service `TooltipHub`
- phase is `Acquire`

Expected:

- tooltip hub acquire action is invoked

#### TC_LIFE_ENROLL_003_RegistrationScanForbidden

Input:

- runtime has service registrations with handler interfaces

Expected:

- LifecycleDispatcher does not scan registrations
- `LIFECYCLE_REGISTRATION_SCAN_FORBIDDEN` if attempted

### B. Phase and Order Tests

#### TC_LIFE_ORDER_001_DeterministicOrder

Input:

- steps A, B, and C exist in the same phase with explicit order

Expected:

- dispatch order is A, then B, then C

#### TC_LIFE_ORDER_002_OrderConflictRejected

Input:

- two required steps have the same phase and order with no tie policy

Expected:

- validation failed
- `LIFECYCLE_STEP_ORDER_CONFLICT`

#### TC_LIFE_PHASE_001_AcquireBeforeActivate

Input:

- acquire and activate steps exist for the same boundary

Expected:

- acquire completes before activate

### C. Scope Boundary Tests

#### TC_LIFE_SCOPE_001_ScopeStateTriggersAcquire

Operation:

- ScopeGraph transitions into acquiring state

Expected:

- LifecycleDispatcher executes the acquire dispatch table

#### TC_LIFE_SCOPE_002_InvalidScopeStateRejectsLifecycle

Operation:

- dispatch tick for a destroyed scope

Expected:

- failed
- `LIFECYCLE_INVALID_SCOPE_STATE`

#### TC_LIFE_SCOPE_003_LifecycleDoesNotMutateScopeStructure

Operation:

- lifecycle dispatch runs for a valid scope boundary

Expected:

- lifecycle performs side effects only
- scope parent-child structure remains owned by ScopeGraph

### D. Tick Tests

#### TC_LIFE_TICK_001_TickDispatchNoAllocation

Operation:

- dispatch normal tick repeatedly

Expected:

- no managed allocation in the normal path

#### TC_LIFE_TICK_002_PerEntityTickRejected

Input:

- 10,000 entity tick steps are generated

Expected:

- failed
- `LIFECYCLE_TICK_CARDINALITY_FORBIDDEN`

#### TC_LIFE_TICK_003_HubTickAllowed

Input:

- mesh hub has one tick step
- the hub owns many player runtimes internally

Expected:

- passed
- one hub tick step exists, not one step per player runtime

### E. Failure and Rollback Tests

#### TC_LIFE_FAIL_001_AcquireFailureFailsScope

Input:

- acquire step fails
- failure policy is `FailScope`

Expected:

- scope enters failed or inactive state
- completed acquire steps are released if rollback policy requires

#### TC_LIFE_FAIL_002_BootFailureFailsKernel

Input:

- boot lifecycle step fails
- failure policy is `FailKernel`

Expected:

- kernel boot fails

#### TC_LIFE_FAIL_003_ContinueWithErrorRequiresExplicitPolicy

Input:

- step failure occurs
- `ContinueWithError` is absent

Expected:

- failed
- default failure boundary is applied

### F. Async Tests

#### TC_LIFE_ASYNC_001_UntrackedAsyncRejected

Input:

- lifecycle step starts `UniTask` work without tracked async policy

Expected:

- failed
- `LIFECYCLE_ASYNC_UNTRACKED`

#### TC_LIFE_ASYNC_002_TrackedAsyncTimeout

Input:

- async lifecycle step has timeout policy

Expected:

- timeout produces structured diagnostics
- declared failure boundary is applied

### G. Reset and Pooling Tests

#### TC_LIFE_POOL_001_ResetBeforeReuseRequired

Operation:

- reuse pooled scope without reset lifecycle

Expected:

- failed
- `LIFECYCLE_RESET_REQUIRED_BEFORE_REUSE`

#### TC_LIFE_POOL_002_ResetClearsSubscriptions

Input:

- service subscribes during acquire

Operation:

- release, then reset, then acquire again

Expected:

- duplicate subscriptions do not remain

#### TC_LIFE_POOL_003_ResetClearsTransientRuntimeState

Input:

- scope accumulates transient runtime query or value state

Operation:

- release, then reset, then reuse

Expected:

- transient state from the previous lifetime is cleared

### H. Migration Tests

#### TC_LIFE_MIGRATION_001_TooltipHubMigration

Input:

- tooltip hub requires acquire, release, and tick behavior

Expected:

- service contribution defines the hub
- lifecycle contribution defines acquire, release, and tick
- tooltip player runtimes are not direct lifecycle targets

#### TC_LIFE_MIGRATION_002_MeshHubMigration

Input:

- mesh hub owns many mesh player runtimes

Expected:

- one hub lifecycle target exists
- player runtimes are managed internally

#### TC_LIFE_MIGRATION_003_AnimationSpriteInstallerMigration

Input:

- animation sprite hub is currently registered as service plus lifecycle handlers

Expected:

- lifecycle participation is represented by lifecycle contribution
- ServiceGraph does not infer participation from registered interfaces

---

## Acceptance Criteria

08 is complete when it defines:

- LifecyclePlan purpose and authority
- lifecycle input contract
- lifecycle identity vocabulary
- lifecycle phase model
- lifecycle step model
- lifecycle target model
- lifecycle ordering model
- lifecycle dependency model
- lifecycle dispatch table model
- scope state boundary
- ServiceGraph boundary
- runtime object boundary
- tick, fixed tick, and late tick policy
- per-entity lifecycle prohibition
- failure policy
- reset and pooling policy
- async lifecycle policy
- diagnostics and DebugMap requirements
- performance and memory policy
- legacy migration policy
- forbidden patterns
- lifecycle test case model
- required lifecycle test cases

The specification is not complete if lifecycle participation can still be read as interface enrollment, registration scan output, or per-entity implicit tick expansion.

---

## Test Cases

| Test Case | Purpose | Verification |
|---|---|---|
| TC-08-01 | Confirm lifecycle enrollment is explicit. | The lifecycle authority and input contract sections must forbid interface and registration-based enrollment. |
| TC-08-02 | Confirm dispatch uses precomputed lifecycle tables rather than registration scans. | The dispatch table and ServiceGraph boundary sections must reject handler list collection APIs and runtime scan-built dispatch. |
| TC-08-03 | Confirm scope, service, runtime object, query, and value boundaries remain explicit. | The boundary sections must keep lifecycle execution separate from scope structure, service discovery, generic query lookup, and value fallback. |
| TC-08-04 | Confirm tick participation stays budgeted and does not scale per entity by default. | The tick policy, per-entity prohibition, and performance sections must reject unbounded lifecycle expansion. |
| TC-08-05 | Confirm async lifecycle work is tracked and failures are fail-closed. | The async policy and failure policy sections must forbid fire-and-forget work and require explicit failure boundaries. |
| TC-08-06 | Confirm reset and pooling lifecycle remain explicit and verified. | The reset and pooling policy section must require reset before reuse and reject stale subscriptions or transient state leakage. |

---

## Final Position

Lifecycle is declared, validated, and dispatched from plan.
It is never discovered from runtime registrations.

Lifecycle participation is not an emergent side effect of service registration.
It is verified runtime intent.

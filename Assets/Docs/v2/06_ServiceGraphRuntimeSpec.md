# ServiceGraph Runtime Specification

## Document Status

- Document ID: 06_ServiceGraphRuntimeSpec
- Status: Draft
- Role: defines runtime service resolution, service eligibility, service lifetime boundaries, and service failure rules for Kernel v2
- Depends on:
  - [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md)
  - [01_KernelIRSpec.md](01_KernelIRSpec.md)
  - [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md)
  - [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md)
  - [04_DependencyValidationSpec.md](04_DependencyValidationSpec.md)
  - [05_BootManifestAndProfileSpec.md](05_BootManifestAndProfileSpec.md)
- Consumes:
  - ServiceIR
  - ServiceGraphPlan
  - ScopeGraphPlan references
  - RuntimeQueryPlan references
  - KernelDebugMap
- Provides foundation for:
  - 07_ScopeGraphRuntimeSpec.md
  - 08_LifecyclePlanSpec.md
  - 09_CommandCatalogRuntimeSpec.md
  - 10_ValueSchemaAndStoreSpec.md
  - 11_DebugMapAndDiagnosticsSpec.md
  - 12_UnityAuthoringBridgeSpec.md
  - 13_LegacyCompatBoundarySpec.md
  - 14_PerformanceBudgetAndRuntimeRulesSpec.md
  - 15_TestAndValidationSpec.md

### Ownership

This specification owns runtime service resolution for verified, coarse-grained services.
It does not own scope structure, runtime query indexes, lifecycle execution, command dispatch, or value storage internals.

This specification owns:

- ServiceGraph runtime definition
- service eligibility rules
- non-service runtime object classification
- ServiceGraphPlan runtime input contract
- service identity and service contract rules
- service lifetime and cardinality rules
- service factory rules
- resolver semantics for required and optional services
- slot and cache model requirements
- dependency resolution rules inside ServiceGraph
- scope-local service boundary rules
- diagnostics and DebugMap requirements for services
- service failure behavior
- service performance and memory rules
- service threading and shutdown rules
- legacy boundary rules for service runtime

This specification does not own:

- scope parent-child structure
- RuntimeQuery storage or lookup semantics
- lifecycle step execution
- command catalog dispatch
- value key resolution or value storage layout
- Unity authoring schema
- boot manifest selection

06 is the runtime service authority.
It is not a replacement for a general-purpose DI container.

---

## Purpose

This specification defines ServiceGraph, the runtime resolver for verified services derived from ServiceGraphPlan.

ServiceGraph exists to execute explicit service structure.
It does not exist to discover missing runtime structure, collect arbitrary behaviors, or repair incomplete plans.

The core statement of 06 is:

```text
ServiceGraph resolves coarse-grained verified services.
It does not model every runtime object as a service.
```

ServiceGraph is not a general-purpose DI container.
ServiceGraph must not become:

- a lifecycle handler collector
- a command executor registry
- a runtime object registry
- a per-entity service container
- a channel player factory registry
- a value/key resolver
- a fallback resolver

---

## Scope

This specification defines:

- ServiceGraph runtime responsibility
- service eligibility and non-eligibility
- non-service runtime object classification
- ServiceGraphPlan input contract
- service identity and service contract rules
- service lifetime and cardinality rules
- service factory rules
- required and optional service resolution behavior
- scope-local service boundary rules
- entity and per-target service prohibition
- hub, channel, and player classification
- lifecycle, command, runtime query, and value boundaries
- Unity object linkage constraints for services
- service diagnostics and DebugMap requirements
- service failure policy
- service performance, memory, threading, and shutdown policy
- service runtime test case model and required tests

---

## Non-Goals

This specification does not define:

- final ScopeGraph storage
- RuntimeQuery index shape
- lifecycle step ordering or execution algorithm
- command dispatch or payload execution rules
- ValueStore layout or serialization
- Unity object authoring schema
- boot manifest shape
- scene transition algorithms

This specification must not turn ServiceGraph into:

- a generic runtime object directory
- a generic hierarchy query API
- a replacement for RuntimeQuery
- a replacement for ValueStore
- a replacement for LifecyclePlan
- a replacement for CommandCatalog

---

## Relationship to Other Specs

| Spec | Relationship |
|---|---|
| [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md) | Defines ServiceGraph as explicit service runtime, separates RuntimeQuery from service resolution, and forbids runtime discovery and reflection activation. |
| [01_KernelIRSpec.md](01_KernelIRSpec.md) | Defines ServiceIR, ServiceId, ServiceDependencyIR, ServiceLifetimeKind, and typed identity domains used by this runtime contract. |
| [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md) | Defines ServiceContribution as declarative input and rejects installer-style runtime mutation as the target model. |
| [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md) | Produces ServiceGraphPlan as a verified projection and forbids partial artifact execution or invented ServiceId values. |
| [04_DependencyValidationSpec.md](04_DependencyValidationSpec.md) | Validates service dependency correctness, lifetime direction, optional dependency policy, and RuntimeQuery separation before runtime use. |
| [05_BootManifestAndProfileSpec.md](05_BootManifestAndProfileSpec.md) | Defines boot-time acceptance of verified service projections and fail-closed creation of essential service runtime state. |
| [07_ScopeGraphRuntimeSpec.md](07_ScopeGraphRuntimeSpec.md) | Owns scope structure and scope-local service lifetime boundary creation; 06 owns service resolution inside that boundary. |
| 08_LifecyclePlanSpec.md | Owns lifecycle participation and step execution; 06 only defines service participation boundaries. |
| 09_CommandCatalogRuntimeSpec.md | Owns command discovery, dispatch, and executor routing; 06 only defines command-related service boundaries. |
| 10_ValueSchemaAndStoreSpec.md | Owns value schemas, storage, and dynamic evaluation; 06 only defines value-related service boundaries. |
| 11_DebugMapAndDiagnosticsSpec.md | Owns the shared structured diagnostics substrate and DebugMap runtime contract; 06 defines required service runtime provenance fields and failure behavior. |
| 12_UnityAuthoringBridgeSpec.md | Owns authoring-side service bindings and Unity linkage generation inputs. |
| 13_LegacyCompatBoundarySpec.md | Owns where legacy resolver and installer compatibility may remain visible. |
| 14_PerformanceBudgetAndRuntimeRulesSpec.md | Owns measurable runtime budgets that consume the performance rules declared here. |
| 15_TestAndValidationSpec.md | Turns this runtime service contract into executable test and CI coverage. |

06 consumes verified service structure.
It must not derive service truth from registrations, scene search, ancestor traversal, or fallback creation.

---

## Current Service Debt Observations

### Observation Traceability

Current runtime service observations must remain traceable to source code, profiling evidence, or migration notes.

When this document is updated, observations that no longer match the current codebase must be removed or moved to migration notes.

| Observation | Evidence Type | Expected Downstream Spec |
|---|---|---|
| Runtime service resolution still depends on registration tables, raw Type keys, and collection-style discovery. | Source | 06 |
| Scope build still mixes feature installer discovery, builder mutation, resolver construction, lifecycle collection, and scope activation. | Source | 06, 07, 08 |
| Lifecycle and tick participation are still inferred from registered interfaces instead of explicit lifecycle plans. | Source | 06, 08 |
| Command runtime wiring still uses installer-style mutation and service registration patterns. | Source | 06, 09 |
| Tooltip, mesh, and sprite-animation "services" still mix service dependencies, runtime query behavior, dynamic value resolution, player runtime ownership, and lifecycle participation. | Source | 06, 08, 09, 10 |
| Existing runtime paths still allow ancestor resolve, `Camera.main`, `NullVarStore`, and similar fallback behavior to repair missing structure. | Source | 06, 10, 13 |

### Representative Anchors

- [RuntimeResolverHub.cs](../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs) - registration table resolver, `IReadOnlyList<T>` collection, lifecycle handler collection, type-keyed cache behavior
- [RuntimeLifetimeScope.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs) - builder construction, feature installer discovery, scope-local resolver creation, lifecycle handler extraction
- [CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs) - installer-style command registration and lifecycle handler registration
- [ModalStackChannelHubService.cs](../../GameLib/Script/Project/UI/Core/ModalStackChannel/ModalStackChannelHubService.cs) - stateful UI hub that should be modeled as a coarse-grained hub, not as per-target service expansion
- [TooltipChannelHubService.cs](../../GameLib/Script/Project/UI/Core/Tooltip/TooltipChannelHubService.cs) - mixed service resolution, ancestor lookup, tick handling, channel runtime ownership, camera fallback, and value fallback
- [MeshChannelHubService.cs](../../GameLib/Script/Project/Scene/Channels/Mesh/MeshChannelHubService.cs) - scope-bound hub that owns many `MeshChannelPlayerRuntime` instances
- [MeshChannelHubMB.cs](../../GameLib/Script/Project/Scene/Channels/Mesh/MeshChannelHubMB.cs) - installer mutation that registers a hub as service plus lifecycle handlers
- [AnimationSpriteHubService.cs](../../GameLib/Script/Project/Scene/Channels/SpriteAnimation/AnimationSpriteHubService.cs) - mixed hub, material provider, visual hub, lifecycle, tick, and player runtime ownership
- [AnimationSpriteHubMB.cs](../../GameLib/Script/Project/Scene/Channels/SpriteAnimation/AnimationSpriteHubMB.cs) - installer mutation that registers a hub through multiple contracts and lifecycle interfaces

### Current Gaps

The current codebase still exposes behaviors that 06 must remove from the target architecture:

- service truth is still partially registration-driven
- required dependencies can still be searched at acquire time
- runtime query behavior is still mixed into service resolution
- lifecycle enrollment is still discoverable through interfaces
- player runtime and session runtime ownership are not separated from coarse-grained hub identity
- fallback behavior still exists for missing camera, value, or resolver dependencies
- service count pressure is not explicitly bounded against entity and target count

---

## Core Problem

Legacy runtime service architecture mixes multiple responsibilities:

- service identity and raw C# type identity
- coarse-grained shared services and per-target runtime objects
- lifecycle participation and service resolution
- command routing and shared service access
- runtime query behavior and service dependencies
- dynamic value access and service dependencies
- scope creation and service construction
- required dependency resolution and fallback repair

The target ServiceGraph must separate these responsibilities.

If service count grows with entity count, target count, or player runtime count, the design is wrong by default.

---

## ServiceGraph Runtime Definition

ServiceGraph is the runtime resolver for verified services defined by ServiceGraphPlan.

ServiceGraph owns:

- service slot identity inside a verified graph
- service construction inside a verified lifetime boundary
- required and optional service resolution
- dependency ordering inside the service graph
- service cache lifetime inside the graph boundary
- service disposal for declared service instances
- service diagnostics

ServiceGraph does not own:

- scope parent-child structure
- runtime query indexes
- lifecycle step discovery
- command executor collection
- value key lookup
- Unity scene search
- broad runtime object directories

ServiceGraph may execute only verified ServiceGraphPlan input.

It must not:

- register new services at runtime
- discover services by scanning MonoBehaviours or interfaces
- repair missing services through fallback factories
- collect behavior lists from arbitrary contracts
- resolve runtime objects through service lookup

---

## Service Eligibility Model

A runtime object may become a ServiceGraph service only if it satisfies all required eligibility rules.

Required rules:

1. It has a stable ServiceId.
2. It has an explicit owner module.
3. It has a verified lifetime domain.
4. Its existence is discoverable from ServiceGraphPlan, not runtime search.
5. Its dependencies are declared and validated.
6. Its creation is controlled by a verified factory or verified prebuilt source.
7. Its failure can be diagnosed through DebugMap and stable diagnostics.
8. Its expected cardinality is coarse-grained enough to justify service slot and cache ownership.

A service candidate that fails any required rule is not a service.

The following must not become ServiceGraph services by default:

- every entity instance
- every part instance
- every tooltip instance
- every channel player
- every mesh track runtime
- every animation player runtime
- transient command execution frames
- dynamic value evaluation contexts
- per-target visual mutation sessions
- pooled runtime object instances

The intended rule is simple:

```text
Shared, explicit, validated runtime infrastructure may be a service.
Per-target runtime objects are not services by default.
```

---

## Non-Service Runtime Object Model

Not every long-lived runtime object is a service.

A runtime object should be modeled outside ServiceGraph when any of the following is true:

- it is owned by a specific hub or service
- it is created dynamically from channel, tag, handle, or content data
- it is many-per-scope or many-per-entity
- it does not need global or graph-level dependency resolution
- it should be pooled, reset, or recycled independently of service lifetime
- it is addressed by tag, handle, or RuntimeQuery rather than ServiceId

Examples:

- `TooltipChannelPlayerRuntime`
  - owner: tooltip hub service
  - identity: channel tag or local handle
  - not ServiceId-backed
- `MeshChannelPlayerRuntime`
  - owner: mesh hub service
  - identity: channel tag
  - not ServiceId-backed
- modal root entry or resolved layer state
  - owner: modal stack hub
  - identity: local modal root or UI root handle
  - not ServiceId-backed
- animation player runtime
  - owner: animation sprite hub
  - identity: local player tag or view linkage
  - not ServiceId-backed

ServiceGraph must not be used to give synthetic service identities to these objects just to make them easy to retrieve.

---

## ServiceGraphPlan Input Contract

ServiceGraph may be created only from a verified ServiceGraphPlan inside one verified artifact set.

A valid ServiceGraphPlan must provide at least:

- the ServiceId set
- owner module per service
- service lifetime per service
- service contract metadata per service
- dependency edges or equivalent dependency references per service
- optional dependency policy where applicable
- service cardinality metadata
- service factory metadata
- scope-boundary or root-boundary placement metadata where applicable
- source provenance and DebugMap linkage
- verified artifact header metadata

ServiceGraphPlan must not:

- invent new ServiceId values not present in KernelIR authority
- silently drop required services
- convert runtime query needs into generic service slots
- depend on runtime registration side effects

A partial ServiceGraphPlan is invalid.
A ServiceGraphPlan from a mixed artifact set is invalid.

---

## Service Identity Model

ServiceId is the primary runtime identity for services.

Service resolution must not use raw type name, arbitrary string, or Unity object identity as the source of truth.

Rules:

- a service has one stable ServiceId
- a service may expose multiple validated contracts
- contracts do not replace ServiceId as runtime identity
- a ServiceId may not satisfy another typed identity domain
- a Unity object reference may support a service, but it is not the service identity

The following are forbidden as primary identity:

- `Type`
- implementation type full name
- authoring string key
- GameObject name
- Transform path
- scene instance ID

Generated typed wrappers are allowed only if they compile down to ServiceId or a verified service slot.

---

## Service Contract Model

Service contracts describe how a verified service may be consumed.

Contracts exist for:

- validation
- generated resolver surfaces
- diagnostics
- projection consistency

Contracts do not authorize arbitrary runtime builder mutation.

Rules:

- every exposed contract must be declared in ServiceIR or ServiceGraphPlan
- contract exposure must remain stable across verification and runtime
- contract lookup must resolve to one verified service identity or one verified service family rule
- contract ambiguity must be rejected by validation or plan generation

The target architecture must not reintroduce installer-style `.As<T>()` mutation as the service truth model.

Contract metadata is a declaration surface.
It is not a registration script.

---

## Service Lifetime Model

Service lifetime is explicit and verified.

An explanatory runtime lifetime model is:

```csharp
public enum ServiceLifetimeKind
{
    Kernel = 10,
    Project = 20,
    Scene = 30,
    Scope = 40,
    ExplicitTransient = 50,
}
```

This sketch is explanatory and must remain consistent with the identity and lifetime definitions owned by 01.

Rules:

- Kernel services outlive all other services
- Project services outlive scene and scope services
- Scene services outlive scope services inside the scene boundary
- Scope services are bound to one verified scope lifetime boundary
- ExplicitTransient may exist only when a lower spec defines why the instance should not be cached as a normal service

Entity lifetime is not a ServiceLifetimeKind.

If entity-scoped behavior is needed, use:

- EntityRuntime or component runtime
- ValueStore slices
- RuntimeQuery handles
- command target handles
- hub-owned runtime objects

Longer-lived services must not require shorter-lived services unless a lower spec defines a verified indirection accepted by 04.

---

## Service Cardinality Model

Service cardinality expresses expected instance pressure for a service contribution.

An explanatory cardinality model is:

```csharp
public enum ServiceCardinalityKind
{
    SingletonGlobal = 10,
    OnePerProject = 20,
    OnePerScene = 30,
    OnePerAuthoredScope = 40,
    BoundedPool = 50,
    UnboundedRuntime = 90,
}
```

Cardinality is not the same concept as lifetime.

Rules:

- `SingletonGlobal`, `OnePerProject`, `OnePerScene`, and `OnePerAuthoredScope` are normal ServiceGraph candidates when other eligibility rules are satisfied
- `BoundedPool` is valid only when a lower spec explicitly budgets the instance family and keeps lifetime, diagnostics, and disposal explicit
- `UnboundedRuntime` is invalid for ServiceGraph services unless a lower spec explicitly approves the pattern and 14 budgets it

The target runtime must reject service designs whose expected count scales with:

- entity count
- part count
- renderer count
- tooltip instance count
- mesh track count
- animation player count
- command execution count

---

## Service Factory Model

Service creation must be explicit and verified.

Allowed service factory shapes are limited to factory kinds declared by 01 and projected into ServiceGraphPlan.

At runtime, target ServiceGraph accepts only service creation paths that are:

- generated from verified plan data
- static and explicit
- backed by an explicit prebuilt instance reference that is itself verified input

Forbidden factory behavior:

- reflection constructor injection
- `Activator.CreateInstance`
- runtime script scanning
- arbitrary `Type` activation
- scene-wide component discovery
- fallback prefab loading to repair a missing service

Service creation must not mutate the graph structure.
Factories construct verified services; they do not register new truth.

---

## ServiceResolver Contract

ServiceResolver operates on verified service identity.

The minimal required semantics are:

- resolve a required service or fail with structured diagnostics
- attempt to resolve an optional service according to validated optional policy
- resolve within the current verified lifetime boundary and any explicitly allowed parent boundary
- never repair missing required services through fallback

`GetRequired` semantics:

- accepts a verified ServiceId or generated equivalent
- returns the required service instance when valid
- fails with structured diagnostics when the service is absent, invalid, or forbidden by boundary rules

`TryGet` semantics:

- is valid only for services that are optional or for call sites that are not asserting a required dependency
- must not silently convert a required dependency into an optional one
- must not return fallback null-service or legacy-service instances unless a lower spec defines an explicit compatibility adapter

ServiceResolver must not expose generic discovery features such as:

- `ResolveAll<T>()`
- `IReadOnlyList<T>` collection as discovery
- raw type scanning
- interface enumeration for lifecycle collection

The old container convenience model is intentionally out of scope.

---

## Service Slot and Cache Model

ServiceGraph lookup must be derived from verified plan structure, not from runtime registration scans.

Runtime service resolution should use service slots or an equivalent dense graph representation.

Required properties:

- O(1) or equivalent bounded lookup for known services
- cache ownership tied to verified lifetime boundaries
- no repeated scans over every service registration on normal resolve paths
- no discovery allocations on steady-state resolve paths
- no broad contract enumeration in hot paths

Service slot metadata must preserve at least:

- ServiceId
- lifetime boundary
- contract mapping
- dependency mapping
- construction state
- diagnostics provenance

The following are forbidden as steady-state resolution strategies:

- repeated full dictionary scans by raw type
- repeated `List<T>` collection from registrations
- per-resolve interface discovery
- per-resolve reflection

---

## Dependency Resolution Contract

ServiceGraph resolves only prevalidated service dependencies.

Rules:

- dependency order must be deterministic
- required dependencies must be satisfied before a dependent service is exposed as valid
- optional dependencies must follow the absence behavior already validated by 04
- runtime query needs must remain runtime query dependencies, not service resolver tricks
- value or blackboard needs must remain value dependencies, not service resolver tricks

Forbidden dependency behavior:

- acquire-time ancestor search
- scene-wide search for a service substitute
- Unity object search to repair a missing dependency
- creating a missing dependency on demand without plan support
- substituting runtime objects for service dependencies

A dependency discovered only at runtime is a design failure unless it is represented by a lower spec as verified RuntimeQuery or another explicitly validated indirection.

---

## Optional Service Policy

Optional service behavior is governed by the optional dependency rules validated by 04.

ServiceGraph may honor only explicit absence behaviors.
ServiceGraph must not invent new absence behaviors at runtime.

Allowed absence behavior categories are the ones already defined by 04, including:

- `DisableContribution`
- `EmitWarning`
- `UseExplicitAlternative`
- `ProfileSpecificError`

Resolver rules:

- optional absence must remain diagnostic-visible
- explicit alternatives must point to validated ServiceId targets
- explicit alternatives must remain lifetime- and phase-compatible
- optional absence must not collapse into silent null-service fallback

Optional does not mean "search until something works."

---

## Scoped Service Policy

Scope services are allowed only for authored or verified runtime scopes that represent a meaningful ownership boundary.

Allowed examples:

- UI root scope hub
- scene presentation scope hub
- authored actor root scope with complex shared runtime behavior
- scene-local simulation coordinator

Forbidden by default:

- service per entity instance
- service per part
- service per renderer
- service per tooltip view
- service per channel player
- service per mesh track

A scope service must justify:

- why it requires ServiceGraph participation
- why it cannot be a runtime object owned by another service
- expected instance count
- lifetime boundary
- memory budget
- lifecycle participation boundary

ScopeGraph owns creation of the scope lifetime boundary.
ServiceGraph owns service resolution inside that boundary.

---

## Entity and Per-Target Service Prohibition

ServiceGraph must not be used as an entity component storage system.

The target architecture must not create a ServiceGraph service for every:

- entity
- part
- renderer
- UI element
- tooltip view
- mesh track
- animation player
- command target

Per-target runtime data belongs to:

- EntityRuntime
- PartRuntime
- ValueStore
- RuntimeQuery indexes and handles
- pooled runtime objects
- hub-owned local runtime objects

An entity-scoped service exception is allowed only if all are true:

- the entity is a long-lived authored aggregate root
- the service has meaningful shared dependencies
- the instance count is bounded and budgeted
- the service is declared by KernelIR
- lifecycle and disposal are verified
- diagnostics include source location and runtime handle context

Exceptions are rare.
They do not change the default prohibition.

---

## Hub / Channel / Player Classification

Existing runtime hubs and channel systems must be classified explicitly.

| Runtime concept | Default classification | Notes |
|---|---|---|
| Hub | Service candidate | Allowed only when coarse-grained and tied to a domain or authored scope boundary |
| Channel definition | Configuration or authored/runtime plan data | Usually not a service |
| PlayerRuntime | Hub-owned runtime object | Not a service by default |
| Control surface | Optional service contract on the hub | Valid only if the hub itself is the coarse-grained service |
| Telemetry surface | Diagnostics or telemetry contract | Must not force extra service instances |

Applied to current service debt:

- `ModalStackChannelHubService`
  - classify as UI domain service or UI scope service candidate
  - resolved layer and root states remain hub-owned state, not services
- `TooltipChannelHubService`
  - classify only the hub as a scope service candidate
  - channel players remain hub-owned runtime objects
  - camera, actor, target, and UI root lookup move to RuntimeQuery or explicit dependencies
- `MeshChannelHubService`
  - classify only the hub as a scope service candidate
  - `MeshChannelPlayerRuntime` remains a hub-owned runtime object
- `AnimationSpriteHubService`
  - classify only the hub as a scope service candidate
  - material provider is a contract or boundary concern
  - player runtimes remain non-service runtime objects

---

## Lifecycle Boundary

ServiceGraph does not discover lifecycle participation.

A service may be targeted by LifecyclePlan, but participation must be declared by lifecycle-oriented specs and projections.

Implemented interfaces are not enrollment.

Rules:

- ServiceGraph must not scan for `IScopeAcquireHandler`
- ServiceGraph must not scan for `IScopeReleaseHandler`
- ServiceGraph must not scan for `IScopeTickHandler`
- ServiceGraph must not collect lifecycle lists from arbitrary service contracts

Migration note:

Legacy services such as tooltip, mesh, and animation sprite hubs may map their acquire, release, and tick behavior into lifecycle contributions during migration.

The target ServiceGraph must not preserve automatic lifecycle discovery as a permanent runtime behavior.

---

## Command Boundary

ServiceGraph is not the command catalog.

Command executor discovery, routing, and dispatch belong to command runtime specifications.

ServiceGraph may resolve coarse-grained shared services used by command execution, such as diagnostics or shared domain coordinators.
It must not:

- collect `ICommandExecutor` instances as a discovery surface
- dispatch commands based on service registrations
- treat every command target as a service
- use service registration as the truth source for command availability

The current installer-style command registration pattern is migration debt, not target architecture.

---

## RuntimeQuery Boundary

ServiceGraph resolves services.
RuntimeQuery resolves runtime objects, scopes, actors, UI roots, camera targets, and channel targets.

ServiceGraph must not implement:

- ancestor scope search
- scene search
- actor lookup
- owner lookup
- UI root lookup
- camera fallback lookup

If a service needs one of these objects, the dependency must be represented as:

- a verified RuntimeQuery dependency
- an explicit authored link
- another lower-spec verified boundary contract

RuntimeQuery ownership remains outside 06.
06 defines only the service boundary that must not be crossed.

---

## ValueStore Boundary

ValueStore and dynamic value evaluation are not service resolution surfaces.

ServiceGraph must not be used for:

- value key lookup
- stable-key fallback
- blackboard fallback repair
- dynamic value evaluation context discovery

If a service needs values, the dependency must remain explicit through value-oriented specs and projections.

The following are forbidden as service repair behavior:

- `NullVarStore` fallback for required value access
- blackboard substitution for missing required value systems
- runtime stable-key search to satisfy a service dependency

Values may influence a service.
They must not be used to hide missing service structure.

---

## Unity Object Boundary

Unity object identity is not service identity.

A service may hold Unity object links only when all are true:

- the link is provided by verified authoring or scope linkage
- the lifetime boundary is explicit
- destroyed object behavior is defined
- diagnostics can identify the source object or source location

ServiceGraph must not resolve services by:

- `FindObjectsByType`
- `GetComponentsInChildren`
- Transform parent traversal
- `Camera.main`
- ad-hoc scene object search

Unity object lookup must not repair missing ServiceGraph dependencies.

---

## Diagnostics and DebugMap Requirements

Service runtime diagnostics must be stable, structured, and source-traceable.

Each service failure diagnostic must include at least:

- stable error code
- ServiceId
- owner module
- service lifetime
- service cardinality
- selected profile if relevant
- scope handle or scope plan context if scoped
- source location
- DebugMap linkage when available
- human-readable message
- suggested fix when possible

When contract-specific failure occurs, diagnostics should also include:

- requested contract
- requesting service or subsystem
- failing dependency phase if known

A service runtime error without source location is a diagnostics degradation.

Representative diagnostic codes include:

- `SERVICE_PLAN_MISSING`
- `SERVICE_REQUIRED_MISSING`
- `SERVICE_CONTRACT_MISSING`
- `SERVICE_CARDINALITY_FORBIDDEN`
- `SERVICE_RUNTIME_QUERY_FORBIDDEN`
- `SERVICE_ANCESTOR_RESOLVE_FORBIDDEN`
- `SERVICE_VALUE_FALLBACK_FORBIDDEN`
- `SERVICE_LEGACY_BRIDGE_FORBIDDEN`

---

## Failure Policy

ServiceGraph fails closed.

Representative failure categories:

- plan missing or incomplete
- required service missing
- contract mismatch
- lifetime direction violation
- invalid optional alternative
- invalid service cardinality
- runtime query dependency routed through service resolution
- forbidden value fallback behavior
- forbidden legacy bridge dependency

Failure boundaries:

- a required root or boot-time service failure invalidates the containing boot or runtime activation boundary
- a scope service failure invalidates the containing scope service boundary
- a dependency failure invalidates dependent services; it must not silently degrade into partial success

ServiceGraph must not continue with:

- null-service repair
- keep-going fallback creation
- legacy resolver substitution
- silent contract drops

---

## Performance and Memory Policy

ServiceGraph must remain small relative to total runtime object count.

Service count should scale with:

- kernel systems
- project systems
- scene systems
- authored scope hubs

Service count must not scale with:

- entity count
- part count
- renderer count
- tooltip instance count
- mesh track count
- animation player count
- command execution count

Runtime rules:

- normal service resolution should be allocation-free or near-allocation-free
- repeated registration scans are forbidden
- repeated broad contract enumeration is forbidden
- eager creation of every player runtime is forbidden unless explicitly required by a verified plan
- eager construction of every command executor is forbidden

ServiceGraph should expose enough runtime metrics or markers to let 14 budget:

- graph creation
- required service construction
- required service resolution misses
- scope-boundary service creation
- disposal of scoped service boundaries

Performance optimization must not remove diagnostics or validation-derived safety.

---

## Threading and Async Policy

ServiceGraph behavior must remain deterministic.

Rules:

- graph creation is synchronous and explicit
- service resolution must not hide asynchronous initialization
- factories touching Unity objects must run on the main thread
- background preparation is allowed only for immutable or non-Unity data explicitly approved by a lower spec

If a service needs asynchronous work:

- the async boundary belongs to lifecycle or another lower spec
- resolver truth must not change silently after exposure
- readiness and failure must remain diagnostics-visible

Implicit async construction in the resolver is forbidden.

---

## Disposal and Shutdown Policy

ServiceGraph owns disposal of service instances inside its lifetime boundary.

Rules:

- disposal order must be deterministic
- dependent services should shut down before their dependencies when required by ownership or lower-spec shutdown rules
- scope-bound services must be released when the owning scope boundary is destroyed
- project and scene service disposal must align with boot and scope lifetime boundaries

A service requiring shutdown behavior must expose that requirement explicitly through plan metadata or an explicit lower-spec contract.

ServiceGraph must not:

- search child objects to find extra disposable runtime state
- silently leak hub-owned runtime objects
- keep disposed service instances alive in caches

Hub-owned runtime objects are disposed by their owner hub.
They are not promoted into top-level service ownership just to make disposal easy.

---

## Legacy Compatibility Boundary

Legacy compatibility is allowed only through explicit adapters defined by 13.

Target ServiceGraph core must not depend on:

- `RuntimeResolverHub` as the architecture truth model
- `BaseLifetimeScopeRegistry` as service lookup authority
- installer scan as service discovery
- legacy command runner registration as command truth
- legacy null-service or null-value fallback patterns

Allowed migration shape:

- explicit adapter or bridge
- profile-visible diagnostics
- bounded scope
- removal path documented by lower specs

Legacy is not the default.
It is a temporary boundary.

---

## Forbidden Patterns

The following are forbidden in target ServiceGraph runtime:

- runtime service registration
- installer-style builder mutation
- reflection constructor injection
- `Activator.CreateInstance` fallback
- resolving by arbitrary string
- resolving by raw `Type` as primary identity
- collecting `IReadOnlyList<T>` as discovery
- scanning services for lifecycle interfaces
- collecting command executors through services
- resolving runtime objects through ServiceResolver
- resolving entities, parts, actors, UI roots, channels, or players through ServiceResolver
- creating one service per entity by default
- creating one service per channel player
- creating one service per tooltip instance
- creating one service per mesh track
- using ServiceGraph as a RuntimeQuery registry
- using ServiceGraph as a ValueStore key resolver
- ancestor scope search for dependencies
- scene-wide search fallback
- Unity object search fallback
- null-service fallback for required dependencies
- blackboard or var fallback to repair missing service truth

---

## Test Case Model

Each service runtime test case must define:

- Test ID
- Title
- ServiceGraphPlan fixture
- relevant ScopeGraphPlan or boot fixture if needed
- selected profile if relevant
- operation under test
- expected runtime result
- expected diagnostics
- expected failure boundary
- notes

Example:

### TC_SERVICE_001_RequiredServiceMissingBlocksBoundary

Input:

- ServiceGraphPlan declares `Service A`
- `Service A` requires `Service B`
- `Service B` is absent

Operation:

- create the containing service boundary

Expected:

- result: failed
- diagnostic: `SERVICE_REQUIRED_MISSING`
- boundary: containing boot or scope service boundary

---

## Required Test Cases

### A. Service Eligibility Tests

#### TC_SERVICE_ELIGIBILITY_001_CoarseGrainedHubAllowed

Input:

- `ModalStackChannelHub` is declared as a UI domain service
- cardinality is `OnePerProject` or `OnePerScene`
- dependencies are declared and validated

Expected:

- Passed

#### TC_SERVICE_ELIGIBILITY_002_ChannelPlayerRejectedAsService

Input:

- `TooltipChannelPlayerRuntime` is declared as a ServiceContribution
- cardinality is `UnboundedRuntime`

Expected:

- Failed
- `SERVICE_RUNTIME_OBJECT_NOT_SERVICE`

#### TC_SERVICE_ELIGIBILITY_003_EntityServiceRejectedByDefault

Input:

- ServiceContribution declares one service per entity

Expected:

- Failed
- `SERVICE_ENTITY_CARDINALITY_FORBIDDEN`

#### TC_SERVICE_ELIGIBILITY_004_EntityAggregateExceptionAllowed

Input:

- authored aggregate root service
- bounded count
- source-backed scope
- verified lifecycle and diagnostics

Expected:

- Passed or warning according to lower-spec policy

### B. Existing Pattern Migration Tests

#### TC_SERVICE_MIGRATION_001_TooltipHubSplitRequired

Input:

- `TooltipChannelHubService` contribution includes service dependency, lifecycle, channel runtime, dynamic value, and camera lookup behavior

Expected:

- only the hub service identity is accepted by ServiceGraph
- lifecycle participation requires lifecycle contribution or plan
- camera or target lookup requires RuntimeQuery or explicit dependency
- dynamic value access remains outside service resolver truth

#### TC_SERVICE_MIGRATION_002_MeshPlayerRuntimeNotService

Input:

- `MeshChannelHubService` owns `MeshChannelPlayerRuntime` per tag

Expected:

- hub may be a scope service
- player runtimes remain hub-owned runtime objects

#### TC_SERVICE_MIGRATION_003_AnimationSpriteInstallerPatternRejected

Input:

- installer registers animation sprite hub as service, material provider, and lifecycle handlers through builder mutation

Expected:

- Failed
- contribution must be split into service declaration, lifecycle declaration, and any optional contract declarations

### C. Boundary Tests

#### TC_SERVICE_BOUNDARY_001_NoAncestorResolve

Input:

- service implementation attempts ancestor traversal to satisfy a dependency

Expected:

- Failed
- `SERVICE_ANCESTOR_RESOLVE_FORBIDDEN`

#### TC_SERVICE_BOUNDARY_002_NoLifecycleInterfaceScan

Input:

- service implements lifecycle-like interfaces
- no lifecycle plan entry exists

Expected:

- ServiceGraph does not enroll the service into lifecycle execution

#### TC_SERVICE_BOUNDARY_003_NoCommandExecutorCollection

Input:

- module contains many command executors

Expected:

- ServiceGraph does not collect executor lists as service discovery

#### TC_SERVICE_BOUNDARY_004_NoRuntimeQueryThroughServiceResolver

Input:

- service dependency points to actor, scope, entity, UI root, or camera lookup

Expected:

- Failed
- `SERVICE_RUNTIME_QUERY_FORBIDDEN`

#### TC_SERVICE_BOUNDARY_005_NoValueFallbackThroughServiceResolver

Input:

- service runtime path attempts `NullVarStore` or blackboard fallback to repair missing value dependency

Expected:

- Failed
- `SERVICE_VALUE_FALLBACK_FORBIDDEN`

### D. Memory and Cardinality Tests

#### TC_SERVICE_MEMORY_001_ServiceCountDoesNotScaleWithEntityCount

Input:

- 10,000 entities
- shared coarse-grained services plus entity runtime and RuntimeQuery handles

Expected:

- ServiceGraph service count remains bounded and does not scale with entity count

#### TC_SERVICE_MEMORY_002_PerTooltipServiceExplosionRejected

Input:

- 1,000 tooltip views are each declared as services

Expected:

- Failed
- `SERVICE_UNBOUNDED_CARDINALITY_FORBIDDEN`

#### TC_SERVICE_MEMORY_003_PerMeshTrackServiceRejected

Input:

- mesh tracks are declared as individual services

Expected:

- Failed
- `SERVICE_TRACK_CARDINALITY_FORBIDDEN`

#### TC_SERVICE_MEMORY_004_NoEagerPlayerConstructionRequired

Input:

- graph contains hubs whose player runtimes are created from local channel use

Expected:

- graph creation does not require eager construction of every player runtime

---

## Acceptance Criteria

06 is complete when it defines:

- ServiceGraph runtime purpose and ownership
- service eligibility rules
- non-service runtime object rules
- ServiceGraphPlan input contract
- service identity and contract rules
- service lifetime and cardinality rules
- service factory rules
- resolver semantics for required and optional services
- slot and cache model requirements
- dependency resolution rules
- scoped service policy
- entity and per-target service prohibition
- hub, channel, and player classification
- lifecycle, command, runtime query, value, and Unity object boundaries
- diagnostics and DebugMap requirements
- failure policy
- performance and memory policy
- threading and async policy
- disposal and shutdown policy
- legacy boundary rules
- forbidden patterns
- service runtime test case model
- required service runtime test cases

The specification is not complete if ServiceGraph can still be read as a generic DI container, runtime object directory, lifecycle collector, command registry, or fallback resolver.

---

## Test Cases

| Test Case | Purpose | Verification |
|---|---|---|
| TC-06-01 | Confirm ServiceGraph remains a verified coarse-grained service resolver. | The purpose, runtime definition, and eligibility sections must forbid general-purpose container behavior and per-target service expansion. |
| TC-06-02 | Confirm non-service runtime objects remain outside ServiceGraph. | The non-service model and hub/channel/player classification sections must keep player runtimes, local sessions, and per-target objects out of service identity. |
| TC-06-03 | Confirm lifecycle, command, runtime query, and value boundaries stay explicit. | The boundary sections must forbid interface-scan lifecycle enrollment, executor collection, runtime query lookup through services, and value fallback through services. |
| TC-06-04 | Confirm service count does not scale with entity or target count. | The scoped service, per-target prohibition, and performance sections must reject unbounded runtime cardinality. |
| TC-06-05 | Confirm failures remain structured and fail closed. | The diagnostics and failure sections must report required-service, contract, cardinality, and boundary violations without silent fallback. |
| TC-06-06 | Confirm legacy installer and discovery patterns do not return as runtime truth. | The current debt observations, legacy boundary, and forbidden patterns sections must reject runtime registration, installer mutation, and discovery-based service resolution. |

---

## Final Position

ServiceGraph resolves coarse-grained verified services.
It does not model every runtime object as a service.

If service count grows with entity count, the design is wrong by default.

Target ServiceGraph is not a convenience container.
It is the verified runtime resolver for explicit service structure.

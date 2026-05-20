# Wave B Scope and Service Composition Cutover Specification

## Document Status

- Document ID: 02_WaveBScopeAndServiceCompositionCutoverSpec
- Status: Draft
- Role: defines the Wave B migration contract that cuts over runtime scope authority and coarse-grained service composition authority from legacy installer-driven lifetime-scope wiring to verified ScopeGraph and scope-local ServiceGraph boundaries
- Depends on:
  - [00_KernelV21MigrationOverviewSpec.md](00_KernelV21MigrationOverviewSpec.md)
  - [01_WaveABootAndSceneEntryCutoverSpec.md](01_WaveABootAndSceneEntryCutoverSpec.md)
  - [../v2/05_BootManifestAndProfileSpec.md](../v2/05_BootManifestAndProfileSpec.md)
  - [../v2/06_ServiceGraphRuntimeSpec.md](../v2/06_ServiceGraphRuntimeSpec.md)
  - [../v2/07_ScopeGraphRuntimeSpec.md](../v2/07_ScopeGraphRuntimeSpec.md)
  - [../v2/08_LifecyclePlanSpec.md](../v2/08_LifecyclePlanSpec.md)
  - [../v2/10_ValueSchemaAndStoreSpec.md](../v2/10_ValueSchemaAndStoreSpec.md)
  - [../v2/13_LegacyCompatBoundarySpec.md](../v2/13_LegacyCompatBoundarySpec.md)
  - [../v2/15_TestAndValidationSpec.md](../v2/15_TestAndValidationSpec.md)
  - [../v2/16_ImplementationMilestoneOrderSpec.md](../v2/16_ImplementationMilestoneOrderSpec.md)
  - [../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md](../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md)
- Provides foundation for:
  - Wave C command dispatch cutover
  - Wave D value, blackboard, and var cutover
  - [05_WaveERepresentativeGameplaySystemsCutoverSpec.md](05_WaveERepresentativeGameplaySystemsCutoverSpec.md)
  - later gameplay-facing migration work that depends on verified runtime composition boundaries

### Revision Note

This revision creates the second detailed v2.1 wave specification.

Wave A moves the live game onto a verified boot and scene-entry path.
Wave B exists because that cutover does not, by itself, replace the legacy runtime composition authority that still lives in `RuntimeLifetimeScopeBase`, `IFeatureInstaller`, `RuntimeResolverHub`, nearest-scope ownership inference, and registration-driven resolver construction.

The purpose of this wave is not to redesign command semantics, value semantics, or gameplay feature behavior.
Its purpose is to move scope truth and coarse-grained service truth onto verified ScopeGraph and ServiceGraph boundaries while keeping representative existing service consumers working.

---

## Ownership

This specification owns:

- the current-state inventory for runtime scope and service composition authority
- the preserved boundaries that must survive the cutover without freezing legacy composition behavior
- the target authority model for ScopeGraph-owned scope truth and scope-local ServiceGraph boundaries
- the representative service eligibility inventory needed to guide migration choices in this repository
- the runtime pooled scope and generation-safe handle boundary within the Wave B slice
- the subphase structure, diagnostics, and acceptance criteria for Wave B

This specification does not own:

- verified boot entry semantics already owned by v2 and cut over by Wave A
- command catalog truth, executor routing, or bulk command registration redesign
- value schema, blackboard semantics, or var identity truth beyond composition-boundary continuity
- representative gameplay-system migration beyond the runtime composition slice
- final deletion of every legacy adapter or compatibility path

Wave B owns runtime composition authority.
It must not become a substitute for Wave C, Wave D, or Wave E.

---

## Purpose

Wave B defines how the running game stops constructing scope and service truth from legacy installers, registration tables, and Transform hierarchy inference.

Core statements:

```text
ScopeGraph owns runtime scope truth.
ServiceGraph owns coarse-grained verified service truth inside explicit scope boundaries.

Installer discovery, builder mutation, Transform-parent ownership inference, and collection-style resolver discovery must stop being architectural truth.

Runtime pooled scopes are part of this migration slice.
Slot reuse must be generation-safe and stale runtime handles must fail.
```

This wave therefore focuses on composition authority, not on feature semantics.

---

## Scope

Wave B defines:

- persistent, scene, authored, and runtime pooled scope authority cutover
- scope parent-child truth cutover from hierarchy-derived inference to verified scope plans
- scope-local ServiceGraph boundary cutover for representative coarse-grained shared services
- representative service eligibility inventory and mixed-boundary split requirements
- generation-safe runtime scope handle and pooled-scope reuse requirements within the migration slice
- mixed-authority diagnostics and acceptance gates for the above

---

## Non-Goals

Wave B does not define:

- live boot entry redesign already owned by Wave A
- command executor catalog truth or command dispatch redesign
- value-store, blackboard, or var-registry semantics redesign
- representative gameplay-system migration beyond the composition slice
- an exhaustive all-feature service inventory for the entire project

Wave B may preserve representative service consumer continuity.
It does not preserve installer-driven architecture.

---

## Relationship to Other Specs

| Spec | Relationship to Wave B |
| --- | --- |
| [00_KernelV21MigrationOverviewSpec.md](00_KernelV21MigrationOverviewSpec.md) | Defines the preservation floor, destructive allowance, and wave partitioning that Wave B must obey. |
| [01_WaveABootAndSceneEntryCutoverSpec.md](01_WaveABootAndSceneEntryCutoverSpec.md) | Hands off a verified live boot and scene-entry path that Wave B must treat as the upstream authority boundary. |
| [../v2/05_BootManifestAndProfileSpec.md](../v2/05_BootManifestAndProfileSpec.md) | Owns boot-created persistent-root rules that still constrain Wave B scope authority. |
| [../v2/06_ServiceGraphRuntimeSpec.md](../v2/06_ServiceGraphRuntimeSpec.md) | Owns verified service-runtime meaning, service eligibility, and forbidden resolver fallback behavior consumed by Wave B. |
| [../v2/07_ScopeGraphRuntimeSpec.md](../v2/07_ScopeGraphRuntimeSpec.md) | Owns verified runtime scope meaning, ScopeHandle, parent-child rules, and pooling-generation rules consumed by Wave B. |
| [../v2/08_LifecyclePlanSpec.md](../v2/08_LifecyclePlanSpec.md) | Owns lifecycle semantics. Wave B only defines the boundary that lifecycle must stop being discovered by scope or service scanning. |
| [../v2/10_ValueSchemaAndStoreSpec.md](../v2/10_ValueSchemaAndStoreSpec.md) | Owns value semantics. Wave B only defines the boundary that scope composition must not become value truth. |
| [../v2/13_LegacyCompatBoundarySpec.md](../v2/13_LegacyCompatBoundarySpec.md) | Owns quarantine rules that govern transitional coexistence with legacy installers, registries, and resolver paths. |
| [../v2/15_TestAndValidationSpec.md](../v2/15_TestAndValidationSpec.md) | Owns executable proof requirements for the acceptance cases defined here. |
| [../v2/16_ImplementationMilestoneOrderSpec.md](../v2/16_ImplementationMilestoneOrderSpec.md) | Confirms that ScopeGraph and ServiceGraph work must follow the verified-boot baseline rather than precede it. |
| [../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md](../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md) | Prevents Wave B migration code from collapsing kernel, feature, and legacy compile boundaries into one runtime blob. |

Wave B consumes v2 semantics.
It does not soften them for migration convenience.

---

## Current-State Composition Inventory

This section records the current runtime composition authority.
It is migration evidence, not target policy.

### Observation Traceability

| Observation | Evidence Type | Migration Pressure |
| --- | --- | --- |
| `RuntimeLifetimeScopeBase` builds a local resolver, registers scope-local instances, caches owned `IFeatureInstaller` components, and calls `InstallFeature(builder, this)` during build. | Source | scope and service composition are still driven by runtime builder mutation rather than verified plans |
| owned installers are discovered via `GetComponentsInChildren` and nearest-scope filtering in `ScopeFeatureInstallerUtility`. | Source | installer ownership is still inferred from Transform hierarchy rather than explicit scope-plan authority |
| scope parent resolution still walks `transform.parent` and checks `RequiredParentKind`. | Source | scope parent truth is still hierarchy-derived instead of ScopeGraph-owned |
| `BaseLifetimeScopeRegistry` still acts as a kind/id/category lookup authority for live scopes. | Source | runtime scope lookup still depends on registries rather than verified scope graph and runtime-query boundaries |
| `RuntimeResolverHub` still builds from registration tables, supports `IReadOnlyList<T>` collection discovery, and gathers handler lists from resolver state. | Source | coarse service resolution is still registry-driven and discovery-friendly rather than verified ServiceGraph-bound |
| `ProjectLifetimeScope`, `GlobalLifetimeScope`, and `SceneLifetimeScope` still define runtime service boundaries through MonoBehaviour composition and `ConfigureBase` registration paths. | Source | authored and persistent scope composition are still rooted in legacy lifetime-scope behavior |
| `RuntimeLifetimeScopePool` and `RuntimeManagerMB` still manage runtime-scope reuse and bulk deletion outside a verified ScopeHandle authority contract. | Source | runtime pooled scopes still need generation-safe scope truth and explicit reset boundaries |
| `BlackboardMB` and `CommandRunnerMB` still piggyback on installer-driven composition and pull later-wave semantics into the current build path. | Source | Wave B must isolate composition authority without re-owning command or value semantics |

### Representative Anchors

- [../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs)
- [../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs](../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs)
- [../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs](../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs)
- [../../GameLib/Script/Common/LTS/Registry/BaseLifetimeScopeRegistry.cs](../../GameLib/Script/Common/LTS/Registry/BaseLifetimeScopeRegistry.cs)
- [../../GameLib/Script/Common/LTS/Core/IScopeNode.cs](../../GameLib/Script/Common/LTS/Core/IScopeNode.cs)
- [../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScopePool.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScopePool.cs)
- [../../GameLib/Script/Project/Scene/Runtime/RuntimeManager/RuntimeManagerMB.cs](../../GameLib/Script/Project/Scene/Runtime/RuntimeManager/RuntimeManagerMB.cs)
- [../../GameLib/Script/Project/LTS/ProjectLifetimeScope.cs](../../GameLib/Script/Project/LTS/ProjectLifetimeScope.cs)
- [../../GameLib/Script/Project/Global/LTS/GlobalLifetimeScope.cs](../../GameLib/Script/Project/Global/LTS/GlobalLifetimeScope.cs)
- [../../GameLib/Script/Project/Scene/LTS/SceneLifetimeScope.cs](../../GameLib/Script/Project/Scene/LTS/SceneLifetimeScope.cs)
- [../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs)
- [../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs](../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs)
- [../../GameLib/Script/Project/System/SceneFlow/MB/SceneFlowInstallerMB.cs](../../GameLib/Script/Project/System/SceneFlow/MB/SceneFlowInstallerMB.cs)
- [../../GameLib/Script/Common/Commands/VNext/Executors/Core/LifecycleExecutors.cs](../../GameLib/Script/Common/Commands/VNext/Executors/Core/LifecycleExecutors.cs)
- [../../GameLib/Script/Common/Commands/VNext/Executors/Scene/SceneChangeExecutor.cs](../../GameLib/Script/Common/Commands/VNext/Executors/Scene/SceneChangeExecutor.cs)

---

## Preserved Boundaries

Wave B preserves only narrow runtime-composition continuity.
It does not preserve legacy installer, registry, or hierarchy-driven implementation details.

| Boundary Surface | Current Anchor | Wave B Requirement |
| --- | --- | --- |
| Representative coarse service consumer continuity | [../../GameLib/Script/Common/Commands/VNext/Executors/Scene/SceneChangeExecutor.cs](../../GameLib/Script/Common/Commands/VNext/Executors/Scene/SceneChangeExecutor.cs) | Existing consumers that rely on `ISceneService`-compatible shared services must continue to reach equivalent coarse service boundaries after composition cutover. |
| Coarse scope-domain continuity | [../../GameLib/Script/Project/LTS/ProjectLifetimeScope.cs](../../GameLib/Script/Project/LTS/ProjectLifetimeScope.cs), [../../GameLib/Script/Project/Global/LTS/GlobalLifetimeScope.cs](../../GameLib/Script/Project/Global/LTS/GlobalLifetimeScope.cs), [../../GameLib/Script/Project/Scene/LTS/SceneLifetimeScope.cs](../../GameLib/Script/Project/Scene/LTS/SceneLifetimeScope.cs), [../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs) | Project, Global, Scene, and Runtime remain valid migration-facing scope domains, but their parent truth and lifetime boundaries move to ScopeGraph. |
| Runtime pooled scope consumer continuity | [../../GameLib/Script/Common/Commands/VNext/Executors/Core/LifecycleExecutors.cs](../../GameLib/Script/Common/Commands/VNext/Executors/Core/LifecycleExecutors.cs), [../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScopePool.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScopePool.cs) | Existing gameplay paths that create, release, reacquire, or recycle runtime scopes must continue to function once runtime-scope identity and reuse rules move under explicit ScopeGraph authority. |

The following are explicitly not preserved:

- `IFeatureInstaller` as architectural composition truth
- `RuntimeContainerBuilder` mutation as runtime truth
- nearest-scope ownership inference through Transform traversal
- `BaseLifetimeScopeRegistry` as authoritative scope lookup truth
- `RuntimeResolverHub` collection discovery and handler-list discovery as accepted runtime behavior

---

## Owned Migration Goals

Wave B must achieve all of the following:

- move runtime scope truth from hierarchy-derived lifetime-scope behavior to verified ScopeGraph ownership
- move coarse-grained service truth from registration-table and installer mutation paths to verified ServiceGraph boundaries
- eliminate installer discovery and nearest-scope ownership inference as accepted runtime truth
- eliminate Transform-parent inference as accepted scope-parent truth
- establish generation-safe runtime pooled scope identity and stale-handle rejection
- demote legacy registries, installers, and resolver paths to explicit quarantine-only compatibility surfaces if they remain temporarily
- keep representative existing service consumers working through migrated boundaries
- make mixed legacy and verified composition authority detectable and unacceptable

---

## Target Authority Model

Wave B target authority is defined by the following required model.

### Required Scope Authority Chain

1. Verified boot input or an upstream verified runtime surface selects the appropriate `ScopeGraphPlan`.
2. ScopeGraph creates persistent, scene, authored, and runtime pooled scopes only from verified plan structure.
3. ScopeGraph owns `ScopeHandle` issuance, runtime scope identity, parent-child relationships, and runtime scope state.
4. ScopeGraph creates or binds scope-local service lifetime boundaries without inferring scope truth from Unity hierarchy.
5. ScopeGraph invalidates stale runtime handles and increments generation on slot reuse.
6. ScopeGraph emits explicit structural events or diagnostics for scope creation, destruction, state changes, parent changes, and scene-boundary violations.

Wave B therefore requires the v2 rules that:

- ScopeGraph may be created only from a verified `ScopeGraphPlan`
- Transform hierarchy is not kernel scope truth
- parent-child relationships require explicit verified roots or parent handles
- stale scope handles must fail after slot reuse

### Required Service Authority Chain

1. Each migrated scope boundary references the appropriate verified `ServiceGraphPlan`.
2. ServiceGraph is created only within an explicit scope lifetime boundary.
3. ServiceGraph resolves only prevalidated coarse-grained shared services declared by plan.
4. Required and optional service behavior is derived from the verified plan rather than from runtime registration search.
5. ServiceGraph does not discover lifecycle handlers, command executors, or arbitrary runtime objects.
6. Missing required services fail through structured diagnostics instead of fallback repair.

Wave B therefore requires the v2 rules that:

- ServiceGraph is not a general-purpose DI container
- ServiceGraph may execute only verified `ServiceGraphPlan` input
- service discovery by scan, collection resolve, or fallback repair is forbidden in the accepted path
- service truth must not be derived from installer registrations or ancestor resolver repair

### Authority Replacement Rules

Wave B requires the following replacement rules:

- `RuntimeLifetimeScopeBase.InstallFeatures()` and `CacheOwnedFeatureInstallers()` must stop being the authoritative composition path by Wave B completion
- `ScopeFeatureInstallerUtility.TryGetNearestScopeNode()` must stop determining accepted scope ownership in the migrated path
- `ResolveParentCore()` walking `transform.parent` must stop being accepted scope-parent truth
- `RuntimeResolverHub` registration tables and `IReadOnlyList<T>` collection discovery must stop being accepted service authority
- runtime pooled scope reuse must stop relying on implicit reuse safety and must enforce generation-safe reset and validation
- `BaseLifetimeScopeRegistry` may remain temporarily only as a bounded compatibility read model, not as the accepted runtime truth for scope identity and relationship

### Transitional Coexistence Rules

| Transitional Condition | Allowed During Wave B | Required Rule |
| --- | --- | --- |
| Legacy scene roots still host not-yet-migrated feature code | Yes | They may remain as downstream feature hosts, but they must not decide accepted scope or service truth. |
| Existing code still calls ancestor-based resolve helpers | Temporarily | The resolved service must originate from a Wave B authority boundary or an explicit compatibility adapter; the traversal path itself must not remain the architectural truth. |
| `BaseLifetimeScopeRegistry` still exists in the repository | Yes | It may support compatibility reads during transition, but accepted parent-child truth and runtime identity must come from ScopeGraph. |
| Runtime pooled scopes still instantiate Unity prefabs | Yes | Unity placement may remain, but runtime scope identity, parent relationship, and reuse safety must be explicit and generation-safe. |
| Installer discovery, nearest-scope filtering, or Transform-parent inference remain in the accepted path | No | Accepted Wave B behavior must fail validation or diagnostics rather than silently relying on legacy discovery. |

---

## Representative Service Inventory

Wave B does not attempt a full project-wide service census.
It defines a representative inventory that is sufficient to keep the migration slice honest.

| Current anchor | Wave B classification | Why it belongs there | Required downstream treatment |
| --- | --- | --- | --- |
| [../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs](../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs) | legacy resolver boundary, not target service | registration tables, collection-style discovery, and handler harvesting are the opposite of verified coarse-grained service ownership | quarantine behind legacy compatibility; do not promote as ServiceGraph truth |
| [../../GameLib/Script/Common/LTS/Registry/BaseLifetimeScopeRegistry.cs](../../GameLib/Script/Common/LTS/Registry/BaseLifetimeScopeRegistry.cs) | legacy scope lookup boundary, not target service | kind/id/category registry matching is not verified runtime scope truth | replace authoritative lookup with ScopeGraph and RuntimeQuery boundaries; keep only bounded compatibility if temporarily needed |
| [../../GameLib/Script/Project/System/SceneFlow/SceneManager/Service/SceneService.cs](../../GameLib/Script/Project/System/SceneFlow/SceneManager/Service/SceneService.cs) | representative coarse service candidate and continuity anchor | project-level shared service with explicit consumer demand and bounded lifetime domain | move provider ownership under ServiceGraph while keeping its external service contract stable |
| [../../GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs](../../GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs) | mixed boundary, split required before service eligibility | it is coarse-grained but still mixes loading presentation ownership with legacy search and repair behavior | keep only the coarse shared service boundary after Wave A and Wave B split; eliminate search and repair behavior from accepted authority |
| [../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs](../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs) | mixed boundary, split required before service eligibility | it mixes service registration, acquire or release participation, local init, debug view wiring, and transform auto-write | isolate service-boundary concerns in Wave B; leave value and blackboard semantics to Wave D |
| [../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs) | legacy command bootstrap boundary, not target service | bulk executor registration and command-lifecycle wiring belong to command runtime, not ServiceGraph truth | keep outside Wave B target service truth and hand off redesign to Wave C |
| [../../GameLib/Script/Project/System/SceneFlow/MB/SceneFlowInstallerMB.cs](../../GameLib/Script/Project/System/SceneFlow/MB/SceneFlowInstallerMB.cs) | legacy installer boundary, not target service | it mutates builder state to register multiple coarse services and bridge blackboard initialization | replace installer-driven registration with explicit plan-projected service ownership |

Any additional inventory row must earn its way into the document by clarifying a real service-boundary decision that would otherwise remain ambiguous.

---

## Wave B Subphases

### WB-0 Current-State Inventory

Objective:
freeze the current runtime composition authority map before cutover work starts.

Required outputs:

- representative composition anchor inventory
- preserved-boundary table
- explicit list of legacy installer, resolver, registry, and hierarchy-derived authority points

Exit gate:
the current composition authority chain is traceable from persistent roots through representative scene and runtime pooled scopes.

Forbidden shortcuts:

- starting implementation before composition authority is written down
- assuming boot cutover already solved runtime composition truth

### WB-1 Installer and Resolver Quarantine

Objective:
remove legacy installer mutation and resolver discovery from accepted runtime truth.

Required outputs:

- explicit quarantine rules for `IFeatureInstaller`, `RuntimeContainerBuilder`, and `RuntimeResolverHub`
- migration policy for compatibility-only resolver use
- diagnostics for accidental continued reliance on installer and resolver discovery

Exit gate:
accepted runtime composition no longer requires installer discovery or registration-table discovery as authority.

Forbidden shortcuts:

- keeping `GetComponentsInChildren` installer discovery as a silent permanent path
- treating `IReadOnlyList<T>` collection resolve as a harmless convenience in the accepted path

### WB-2 Representative Service Eligibility Inventory

Objective:
decide which representative coarse-grained services can move under ServiceGraph and which anchors remain mixed or legacy-only.

Required outputs:

- representative service inventory table
- split-required decisions for mixed-boundary anchors
- explicit exclusions for command bootstrap and value-semantic anchors

Exit gate:
the migration can explain why each representative anchor is either service-eligible, mixed-boundary, or non-service.

Forbidden shortcuts:

- promoting mixed-boundary objects into ServiceGraph without splitting responsibilities
- using ServiceGraph to assign synthetic identities to per-target runtime objects just for retrieval convenience

### WB-3 Persistent, Scene, and Authored Scope Authority Cutover

Objective:
move persistent, scene, and authored-scope parent and state truth onto ScopeGraph.

Required outputs:

- explicit verified root-scope and child-scope authority model
- explicit replacement of Transform-parent parent inference in the accepted path
- explicit scope state boundary for created, active, inactive, and destroyed representative scopes

Exit gate:
representative persistent and scene scopes are governed by verified scope authority rather than legacy hierarchy inference.

Forbidden shortcuts:

- using `transform.parent` as accepted scope-parent truth
- auto-creating or auto-repairing missing parent scopes from runtime discovery

### WB-4 Runtime Pooled Scope and ScopeHandle Cutover

Objective:
move runtime spawned and pooled scopes onto explicit generation-safe runtime identity.

Required outputs:

- explicit runtime pooled scope authority and reset rules
- generation-safe `ScopeHandle` model for runtime instances
- stale-handle rejection and slot-reuse policy

Exit gate:
representative runtime pooled scopes can be created, reused, and released without leaking prior parent, owner, services, values, or handle validity.

Forbidden shortcuts:

- reusing scope slots without generation increment
- letting a stale handle resolve a reused pooled scope slot
- leaving pooled scope reset behavior implicit

### WB-5 Scope-Local ServiceGraph Boundary Cutover

Objective:
move representative shared services to explicit scope-local ServiceGraph boundaries.

Required outputs:

- representative scope-local ServiceGraph boundary ownership model
- explicit plan-derived service resolution rules
- replacement of runtime registration mutation as accepted service authority

Exit gate:
representative coarse-grained shared services resolve through explicit ServiceGraph boundaries inside the migrated scope domains.

Forbidden shortcuts:

- keeping registration tables as accepted service truth underneath a new name
- letting ServiceGraph discover lifecycle handlers, command executors, or arbitrary runtime objects by scan or collection resolve

### WB-6 Mixed-Authority Diagnostics and Legacy Demotion

Objective:
make remaining legacy composition paths visible, bounded, and unacceptable as the steady state.

Required outputs:

- mixed-authority diagnostics for legacy installer, registry, and hierarchy-driven participation
- explicit demotion rules for remaining legacy scope and resolver paths
- bounded compatibility rules where temporary coexistence still exists

Exit gate:
accepted profiles can distinguish verified composition authority from legacy compatibility behavior.

Forbidden shortcuts:

- claiming migration success while legacy installer or resolver paths still silently own real composition outcomes
- leaving compatibility paths enabled without diagnostics or profile control

### WB-7 Acceptance Gate

Objective:
prove that Wave B is materially complete.

Required outputs:

- executable verification plan for representative scope and service composition cases
- diagnostics coverage for mixed authority, stale handle, and missing plan inputs
- documentation updates that reflect the accepted composition authority model

Exit gate:
all Wave B acceptance criteria and required test cases pass.

Forbidden shortcuts:

- accepting Wave B because scopes still appear to work in scenes without proving the authority model changed
- accepting Wave B without a failing stale-handle or mixed-authority story

---

## Forbidden Shortcuts

The following shortcuts are explicitly forbidden for Wave B:

- treating installer discovery as acceptable because it only runs during build
- treating `Transform.parent` as acceptable because it is already available on every Unity object
- preserving `RuntimeResolverHub` registration tables and `IReadOnlyList<T>` discovery as the hidden truth under a new facade
- moving command bootstrap or blackboard semantics into Wave B simply to avoid doing the proper split later
- claiming runtime pooled scope migration while stale handles or reuse leakage are still possible
- expanding the preservation floor to include the whole legacy lifetime-scope composition architecture

---

## Diagnostics and Failure Policy

v2 specification 11 owns the diagnostics substrate.
Wave B defines the conditions that must become diagnostic-visible and acceptance-visible.

| Code | Failure Condition | Required Result |
| --- | --- | --- |
| V21-WB-SCOPE-001 | `ScopeGraphPlan` or required representative scope projection is missing for the accepted path. | Validation or runtime entry into the migrated composition path must fail before accepted scope creation. |
| V21-WB-SCOPE-002 | Accepted scope parent truth is still inferred from `Transform.parent`, nearest-scope search, or other hierarchy repair. | Validation or diagnostics must reject the path as legacy composition authority. |
| V21-WB-SCOPE-003 | A stale runtime scope handle resolves successfully after pooled slot reuse. | The operation must fail with a stale-handle diagnostic instead of silently targeting a reused scope. |
| V21-WB-SCOPE-004 | A reused pooled scope retains previous parent, owner, services, values, or lifecycle state outside explicit reset policy. | Acceptance must fail because pooled-scope reset is not explicit or generation-safe. |
| V21-WB-SERVICE-001 | `ServiceGraphPlan` is missing, partial, or mixed for a representative migrated scope boundary. | Service-boundary creation must fail closed rather than repairing missing registrations at runtime. |
| V21-WB-SERVICE-002 | Accepted service authority still depends on installer mutation, registration-table discovery, `IReadOnlyList<T>` collection discovery, or arbitrary handler harvesting. | Validation or diagnostics must reject the path as legacy service composition authority. |
| V21-WB-SERVICE-003 | A required representative coarse service is repaired by fallback resolver behavior or silent absence behavior. | The resolve must fail with structured diagnostics rather than returning a repaired or synthetic service. |
| V21-WB-LEGACY-001 | Legacy composition authority and verified ScopeGraph or ServiceGraph authority are both active for the same accepted profile. | The profile must fail validation or runtime acceptance with a mixed-authority diagnostic. |
| V21-WB-BOUNDARY-001 | Wave B attempts to silently re-own command or value semantics while splitting composition boundaries. | The migration must be treated as out-of-scope drift and rejected in review or acceptance. |

---

## Acceptance Criteria

Wave B is complete only when all of the following are true:

- representative persistent, scene, authored, and runtime pooled scopes are governed by verified ScopeGraph authority rather than hierarchy-derived lifetime-scope behavior
- accepted scope parent-child truth no longer depends on `Transform.parent`, nearest-scope search, or registry repair
- representative coarse-grained shared services resolve through explicit scope-local ServiceGraph boundaries created from verified plans
- accepted service authority no longer depends on installer discovery, registration-table discovery, or collection-style resolver discovery
- runtime pooled scope creation, reuse, and destruction are generation-safe and reject stale handles
- representative existing service consumers continue to function without feature-wide rewrites to their coarse service contracts
- mixed legacy and verified composition authority is diagnosable and unacceptable rather than silently tolerated
- command catalog truth remains outside Wave B and value or blackboard truth remains outside Wave B, except for the boundary continuity explicitly required here

---

## Out of Scope and Handoff to Later Waves

The following work is explicitly deferred:

- command executor catalog truth, bulk executor registration replacement, and dispatch authority, which belong to Wave C
- value-store, blackboard, and var identity semantics, which belong to Wave D
- representative gameplay-system migration, which belongs to Wave E
- broad legacy deletion and final hardening, which belong to [06_WaveFLegacyRemovalAndHardeningSpec.md](06_WaveFLegacyRemovalAndHardeningSpec.md)

Wave B should make those waves easier.
It must not claim to have completed them.

---

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V21-WB-01 | Confirm representative persistent and scene scopes enter through verified ScopeGraph authority. | Accepted Project, Global, and Scene scope boundaries must not depend on legacy parent inference or installer authority. |
| TC-V21-WB-02 | Confirm Transform-derived scope parent inference is rejected. | The accepted path must fail if parent truth still comes from `Transform.parent` or nearest-scope discovery. |
| TC-V21-WB-03 | Confirm representative coarse services resolve through explicit ServiceGraph boundaries. | A representative shared service such as `ISceneService` must remain consumable without registration-table authority. |
| TC-V21-WB-04 | Confirm installer and resolver discovery are not required in the accepted path. | The accepted path must not rely on `GetComponentsInChildren`, `IReadOnlyList<T>` discovery, or implicit handler harvesting. |
| TC-V21-WB-05 | Confirm runtime pooled scopes have generation-safe create, reuse, and release behavior. | Representative pooled-scope operations must increment generation and follow explicit reset policy. |
| TC-V21-WB-06 | Confirm stale runtime scope handles are rejected. | After pooled slot reuse, the old handle must fail with a stale-handle diagnostic. |
| TC-V21-WB-07 | Confirm mixed legacy and verified composition authority is rejected. | A profile that leaves legacy installer or resolver authority active alongside migrated composition must fail validation or acceptance. |
| TC-V21-WB-08 | Confirm representative existing service consumers keep working. | Existing feature code that consumes coarse shared services must remain functional without feature-wide contract rewrites. |
| TC-V21-WB-09 | Confirm command bootstrap remains outside Wave B truth. | `CommandRunnerMB` and related bulk executor registration must not be misclassified as successful Wave B service composition. |
| TC-V21-WB-10 | Confirm value and blackboard semantics remain outside Wave B truth. | `BlackboardMB` may be split at the composition boundary, but value semantics must remain owned by later waves. |

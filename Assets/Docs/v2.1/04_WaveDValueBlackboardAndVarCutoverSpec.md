# Wave D Value, Blackboard, and Var Cutover Specification

## Document Status

- Document ID: 04_WaveDValueBlackboardAndVarCutoverSpec
- Status: Draft
- Role: defines the Wave D migration contract that cuts over runtime value identity, store authority, blackboard ownership, grid value normalization, initialization ownership, and DynamicValue runtime authority from legacy Blackboard or Var or runtime key lookup behavior to verified ValueSchema, ValueStore, ValueStoreInitPlan, and DynamicEvaluationPlan authority while preserving narrow gameplay-facing value authoring surfaces
- Depends on:
  - [00_KernelV21MigrationOverviewSpec.md](00_KernelV21MigrationOverviewSpec.md)
  - [02_WaveBScopeAndServiceCompositionCutoverSpec.md](02_WaveBScopeAndServiceCompositionCutoverSpec.md)
  - [03_WaveCCommandDispatchCutoverSpec.md](03_WaveCCommandDispatchCutoverSpec.md)
  - [../v2/01_KernelIRSpec.md](../v2/01_KernelIRSpec.md)
  - [../v2/04_DependencyValidationSpec.md](../v2/04_DependencyValidationSpec.md)
  - [../v2/05_BootManifestAndProfileSpec.md](../v2/05_BootManifestAndProfileSpec.md)
  - [../v2/06_ServiceGraphRuntimeSpec.md](../v2/06_ServiceGraphRuntimeSpec.md)
  - [../v2/07_ScopeGraphRuntimeSpec.md](../v2/07_ScopeGraphRuntimeSpec.md)
  - [../v2/08_LifecyclePlanSpec.md](../v2/08_LifecyclePlanSpec.md)
  - [../v2/09_CommandCatalogRuntimeSpec.md](../v2/09_CommandCatalogRuntimeSpec.md)
  - [../v2/10_ValueSchemaAndStoreSpec.md](../v2/10_ValueSchemaAndStoreSpec.md)
  - [../v2/10_1_ScalarRuntimeAndBindingSpec.md](../v2/10_1_ScalarRuntimeAndBindingSpec.md)
  - [../v2/10_2_DynamicValueEvaluationSpec.md](../v2/10_2_DynamicValueEvaluationSpec.md)
  - [../v2/11_DebugMapAndDiagnosticsSpec.md](../v2/11_DebugMapAndDiagnosticsSpec.md)
  - [../v2/12_UnityAuthoringBridgeSpec.md](../v2/12_UnityAuthoringBridgeSpec.md)
  - [../v2/13_LegacyCompatBoundarySpec.md](../v2/13_LegacyCompatBoundarySpec.md)
  - [../v2/14_PerformanceBudgetAndRuntimeRulesSpec.md](../v2/14_PerformanceBudgetAndRuntimeRulesSpec.md)
  - [../v2/15_TestAndValidationSpec.md](../v2/15_TestAndValidationSpec.md)
  - [../v2/16_ImplementationMilestoneOrderSpec.md](../v2/16_ImplementationMilestoneOrderSpec.md)
  - [../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md](../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md)
- Provides foundation for:
  - [05_WaveERepresentativeGameplaySystemsCutoverSpec.md](05_WaveERepresentativeGameplaySystemsCutoverSpec.md)
  - [06_WaveFLegacyRemovalAndHardeningSpec.md](06_WaveFLegacyRemovalAndHardeningSpec.md)

### Revision Note

This revision creates the fourth detailed v2.1 wave specification.

Wave B moves runtime scope and service composition toward verified scope and service authority.
Wave C moves command dispatch authority toward verified command identity and catalog truth.
Wave D exists because value-state truth still remains mixed across `BlackboardMB`, `BlackboardService`, `GridBlackboardService`, `BlackboardValueInitRuntime`, `VarStore`, `VarStorePayload`, `VarIdResolver`, `VarKeyRegistryLocator`, `DynamicValue`, `DynamicEvaluationRuntime`, and runtime stable-key consumers.

The purpose of this wave is not to redesign the preserved `DynamicValue` authoring wrapper or the existing generated key identity surfaces.
Its purpose is to move runtime value truth to verified `ValueSchema`, `ValueStore`, `ValueStoreInitPlan`, and `DynamicEvaluationPlan` authority while keeping representative existing authored value content functional through explicit normalization and compatibility boundaries.

Scalar runtime and binding remain outside the main Wave D scope.
They are specialized semantics owned by [../v2/10_1_ScalarRuntimeAndBindingSpec.md](../v2/10_1_ScalarRuntimeAndBindingSpec.md) and must be treated as an explicit handoff boundary rather than silently absorbed here.

---

## Ownership

This specification owns:

- the current-state inventory for runtime value identity, store truth, blackboard ownership, grid storage, initialization ownership, and DynamicValue runtime authority
- the preserved gameplay-facing value contracts that must survive the cutover without freezing legacy Blackboard architecture in place
- the target authority model for `ValueKeyId`, `ValueStore`, `ValueStoreInitPlan`, grid normalization, and DynamicValue evaluation in the migration slice
- the quarantine rules for blackboard hierarchical fallback, runtime stable-key lookup, runtime registry lookup convenience paths, and mixed value authority
- the subphase structure, diagnostics, and acceptance criteria for Wave D

This specification does not own:

- target generic value semantics already owned by [../v2/10_ValueSchemaAndStoreSpec.md](../v2/10_ValueSchemaAndStoreSpec.md)
- target DynamicValue evaluation semantics already owned by [../v2/10_2_DynamicValueEvaluationSpec.md](../v2/10_2_DynamicValueEvaluationSpec.md)
- scalar runtime and binding semantics already owned by [../v2/10_1_ScalarRuntimeAndBindingSpec.md](../v2/10_1_ScalarRuntimeAndBindingSpec.md)
- command dispatch semantics already cut over by Wave C
- the broad gameplay-system migration that consumes the migrated value path
- the final deletion of every legacy value helper or compatibility path

Wave D owns generic runtime value authority.
It must not become a substitute for scalar specialization, Wave E gameplay migration, or Wave F cleanup.

---

## Purpose

Wave D defines how the running game stops treating Blackboard hierarchy traversal, runtime stable-key lookup, grid cells with arbitrary var payloads, and multi-path initialization as runtime value truth.

Core statements:

```text
Verified ValueStore authority owns runtime value state.
Verified ValueKeyId is the accepted runtime value identity.

DynamicValue authoring may remain stable.
Blackboard hierarchy fallback, runtime stable-key lookup, runtime registry fallback, and hidden DynamicValue evaluation inside generic initialization must stop being accepted runtime truth.

Grid-like value state must normalize to verified schema-backed structures.
Wave D does not re-own scalar runtime semantics.
```

This wave therefore focuses on authority, identity, initialization, and normalization, not on replacing every value-related authoring surface at once.

---

## Scope

Wave D defines:

- value identity boundary cutover from legacy `VarId` and stable-key runtime lookup behavior to verified runtime value identity
- generic value-store authority cutover from Blackboard-owned runtime truth to verified `ValueStore` ownership
- blackboard demotion and fallback quarantine behind preserved representative content surfaces
- initialization ownership cutover from multi-path Blackboard-driven runtime initialization to explicit verified init and lifecycle boundaries
- grid blackboard normalization into schema-backed table or record-style value structures
- DynamicValue runtime authority cutover from ad hoc wrapper-driven execution to explicit evaluation-runtime ownership
- diagnostics and acceptance gates for the above

---

## Non-Goals

Wave D does not define:

- scalar runtime or binding semantics redesign
- a blanket rewrite of every `DynamicSource` implementation
- full gameplay-system migration for every feature that reads or writes values
- final deletion of every legacy Blackboard or Var-related helper from the repository
- the final save-system payload implementation

Wave D may preserve representative value-facing authoring surfaces.
It does not preserve legacy Blackboard architecture or runtime fallback truth.

---

## Relationship to Other Specs

| Spec | Relationship to Wave D |
| --- | --- |
| [00_KernelV21MigrationOverviewSpec.md](00_KernelV21MigrationOverviewSpec.md) | Defines the preservation floor, destructive allowance, and wave partitioning that Wave D must obey. |
| [02_WaveBScopeAndServiceCompositionCutoverSpec.md](02_WaveBScopeAndServiceCompositionCutoverSpec.md) | Hands off the mixed Blackboard boundary after scope and service composition are no longer allowed to define value truth. |
| [03_WaveCCommandDispatchCutoverSpec.md](03_WaveCCommandDispatchCutoverSpec.md) | Hands off value, blackboard, and var semantics after command runtime stops owning them by accident. |
| [../v2/01_KernelIRSpec.md](../v2/01_KernelIRSpec.md) | Owns typed runtime identity domains consumed here, including the generic value identity vocabulary that Wave D must not reinterpret. |
| [../v2/04_DependencyValidationSpec.md](../v2/04_DependencyValidationSpec.md) | Owns validation rules for value identity, init legality, dynamic dependencies, and runtime fallback rejection consumed by Wave D. |
| [../v2/05_BootManifestAndProfileSpec.md](../v2/05_BootManifestAndProfileSpec.md) | Owns how verified value artifacts reach runtime. Wave D must receive value authority through that chain rather than registry lookup or runtime asset repair. |
| [../v2/06_ServiceGraphRuntimeSpec.md](../v2/06_ServiceGraphRuntimeSpec.md) | Owns coarse service runtime semantics. Wave D may consume value-store services through declared boundaries but must not treat ServiceGraph as value identity truth. |
| [../v2/07_ScopeGraphRuntimeSpec.md](../v2/07_ScopeGraphRuntimeSpec.md) | Owns scope lifetime and scope identity. Wave D consumes explicit scope boundaries for store lifetime and must not restore transform-derived ownership. |
| [../v2/08_LifecyclePlanSpec.md](../v2/08_LifecyclePlanSpec.md) | Owns lifecycle semantics. Wave D only defines how value initialization must stop relying on Blackboard-style multi-path callbacks. |
| [../v2/09_CommandCatalogRuntimeSpec.md](../v2/09_CommandCatalogRuntimeSpec.md) | Owns command semantics. Wave D only defines the value-access boundary that commands consume after command authority already moved in Wave C. |
| [../v2/10_ValueSchemaAndStoreSpec.md](../v2/10_ValueSchemaAndStoreSpec.md) | Owns target generic value-state meaning that Wave D is cutting over to. |
| [../v2/10_1_ScalarRuntimeAndBindingSpec.md](../v2/10_1_ScalarRuntimeAndBindingSpec.md) | Owns scalar specialization. Wave D must hand off to it explicitly rather than silently re-own its semantics. |
| [../v2/10_2_DynamicValueEvaluationSpec.md](../v2/10_2_DynamicValueEvaluationSpec.md) | Owns target DynamicValue evaluation semantics that Wave D must cut over to while preserving the wrapper surface. |
| [../v2/11_DebugMapAndDiagnosticsSpec.md](../v2/11_DebugMapAndDiagnosticsSpec.md) | Owns the diagnostics substrate. Wave D defines the migration-visible value failures that must become diagnostic-visible. |
| [../v2/12_UnityAuthoringBridgeSpec.md](../v2/12_UnityAuthoringBridgeSpec.md) | Owns value-init and DynamicValue authoring normalization. Wave D consumes that boundary rather than extending runtime authoring shortcuts. |
| [../v2/13_LegacyCompatBoundarySpec.md](../v2/13_LegacyCompatBoundarySpec.md) | Owns quarantine rules for legacy Blackboard, Var, and DynamicValue compatibility surfaces. |
| [../v2/14_PerformanceBudgetAndRuntimeRulesSpec.md](../v2/14_PerformanceBudgetAndRuntimeRulesSpec.md) | Owns hot-path and cache budgets for value and evaluation runtime. Wave D only defines the migration boundary that those rules must constrain. |
| [../v2/15_TestAndValidationSpec.md](../v2/15_TestAndValidationSpec.md) | Owns executable proof expectations for the acceptance cases defined here. |
| [../v2/16_ImplementationMilestoneOrderSpec.md](../v2/16_ImplementationMilestoneOrderSpec.md) | Confirms that Wave D starts from the post-M15 baseline and after earlier authority cutovers. |
| [../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md](../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md) | Prevents Wave D migration code from collapsing generic value runtime, dynamic runtime, and legacy bridges into one runtime blob. |

Wave D consumes v2 semantics.
It does not soften them for migration convenience.

---

## Current-State Value Authority Inventory

This section records the current runtime value authority.
It is migration evidence, not target policy.

### Observation Traceability

| Observation | Evidence Type | Migration Pressure |
| --- | --- | --- |
| `BlackboardMB` registers `IBlackboardService`, `IGridBlackboardService`, optional debug view wiring, optional transform auto-write, and local or grid init ownership from one MonoBehaviour. | Source | split service exposure, init ownership, transform bridge behavior, and diagnostics out of one mixed boundary |
| `BlackboardService` stores local values in `VarStore`, but global read and write paths still traverse parent scopes and can fall back to `CreateLocal`, `CreateGameLogicRoot`, or `CreateRoot`. | Source | hierarchical fallback and root-repair behavior must stop being accepted runtime value truth |
| `GridBlackboardService` stores ad hoc row or column grids of per-cell var entries without an explicit schema-backed table identity model. | Source | grid-like values must normalize into verified `Table`, `Record`, or `RecordList` structures |
| `BlackboardValueInitRuntime` evaluates `DynamicValue` during create or acquire-phase blackboard initialization and applies writes through Blackboard-owned plans. | Source | initialization and dynamic evaluation must move to explicit `ValueStoreInitPlan`, lifecycle participation, and explicit evaluation plans |
| `VarStore` already provides versioned slot and table storage with optional schema, but it still lives inside legacy value ownership surfaces rather than under verified runtime artifact authority. | Source | storage capability must be subordinated to verified `ValueStore` authority instead of being the architecture by itself |
| `VarStorePayload` and related payload helpers still combine authored payload data, dynamic evaluation, deferred writes, and grid cell payload application. | Source | payload application must split schema-backed init from explicit dynamic evaluation and grid normalization |
| `VarIdResolver` no longer repairs missing keys with runtime-only negative IDs, but runtime code still calls it to resolve stable keys during execution paths. | Source | remove remaining runtime stable-key lookup from accepted value access paths without losing generated-key continuity |
| `VarKeyRegistryLocator` still reaches value identity through explicit registry assets and runtime `Resources.Load` behavior. | Source | accepted runtime value truth must come from verified artifact input rather than registry-asset lookup |
| `DynamicValue` and `DynamicValue<T>` wrappers can still evaluate sources directly on read, while `DynamicEvaluationRuntime` remains an app-level subsystem rather than the only accepted evaluation authority. | Source | preserve wrapper authoring while moving tracker, cache, invalidation, and hot-path reuse truth to the explicit evaluation runtime boundary |
| `VarStoreSource`, `ExternalBoolBinding`, and `ExternalFloatBinding` still rely on runtime key resolution paths or migration-facing dual key surfaces during live execution. | Source | representative runtime consumers must preserve behavior while moving actual lookup truth to verified value identity |
| `TransformVarAutoWriterService` still ties transform observation and value writes into the same legacy blackboard slice. | Source | transform-to-value projection must become an explicit bridge contribution rather than hidden value ownership |

### Representative Anchors

- [../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs](../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs)
- [../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs](../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs)
- [../../GameLib/Script/Common/Variables/Blackboard/Service/GridBlackboardService.cs](../../GameLib/Script/Common/Variables/Blackboard/Service/GridBlackboardService.cs)
- [../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardValueInitRuntime.cs](../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardValueInitRuntime.cs)
- [../../GameLib/Script/Common/Variables/Blackboard/Service/TransformVarAutoWriterService.cs](../../GameLib/Script/Common/Variables/Blackboard/Service/TransformVarAutoWriterService.cs)
- [../../GameLib/Script/Common/Variables/VarStore/Core/VarStore.cs](../../GameLib/Script/Common/Variables/VarStore/Core/VarStore.cs)
- [../../GameLib/Script/Common/Variables/VarStore/Core/IVarStore.cs](../../GameLib/Script/Common/Variables/VarStore/Core/IVarStore.cs)
- [../../GameLib/Script/Common/Variables/VarStore/Authoring/VarStorePayload.cs](../../GameLib/Script/Common/Variables/VarStore/Authoring/VarStorePayload.cs)
- [../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs)
- [../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs)
- [../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistry.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistry.cs)
- [../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRef.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRef.cs)
- [../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicValue.cs](../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicValue.cs)
- [../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicEvaluationRuntime.cs](../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicEvaluationRuntime.cs)
- [../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicSources.cs](../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicSources.cs)
- [../../GameLib/Script/Common/Variables/Binding/ExternalBoolBinding.cs](../../GameLib/Script/Common/Variables/Binding/ExternalBoolBinding.cs)
- [../../GameLib/Script/Common/Variables/Binding/ExternalFloatBinding.cs](../../GameLib/Script/Common/Variables/Binding/ExternalFloatBinding.cs)
- [../../Editor/Tests/VarIdResolverTests.cs](../../Editor/Tests/VarIdResolverTests.cs)
- [../../Editor/Tests/BlackboardValueInitRuntimeTests.cs](../../Editor/Tests/BlackboardValueInitRuntimeTests.cs)
- [../../Editor/Tests/DynamicEvaluationRuntimeTests.cs](../../Editor/Tests/DynamicEvaluationRuntimeTests.cs)
- [../../Editor/Tests/ScalarRuntimePolicyTests.cs](../../Editor/Tests/ScalarRuntimePolicyTests.cs)

---

## Preserved Contracts

Wave D preserves only narrow gameplay-facing value continuity.
It does not preserve legacy Blackboard hierarchy behavior, registry lookup convenience, or runtime fallback truth.

| Contract Surface | Current Anchor | Wave D Requirement |
| --- | --- | --- |
| `DynamicValue` and `DynamicValue<T>` authoring wrapper surface | [../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicValue.cs](../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicValue.cs) | Existing `SerializeReference`-based authored dynamic wrappers must remain usable by existing content, but accepted hot-path evaluation must move under explicit evaluation-runtime authority. |
| Generated value-key identity continuity | [../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs) | Existing generated value-key identities and representative `VarIds` continuity must remain stable migration-facing inputs so content does not require silent renumbering. |
| `VarKeyRef` dual authoring surface | [../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRef.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRef.cs) | Existing `varId` plus `StableKey` dual references may remain as authoring or debug surfaces, but accepted runtime lookup must not resolve `StableKey` on demand. |
| Blackboard local-init content surface | [../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs](../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs), [../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardValueInitRuntime.cs](../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardValueInitRuntime.cs) | Representative authored local-init entries, overwrite intent, and dynamic authored values must remain consumable by existing content while ownership moves to verified init and lifecycle boundaries. |
| Blackboard grid-init content surface | [../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs](../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs), [../../GameLib/Script/Common/Variables/Blackboard/Service/GridBlackboardService.cs](../../GameLib/Script/Common/Variables/Blackboard/Service/GridBlackboardService.cs) | Representative grid-init authored content must remain consumable by existing content, but accepted runtime must normalize it into schema-backed grid value structures before execution truth is established. |
| Representative external binding consumption surfaces | [../../GameLib/Script/Common/Variables/Binding/ExternalBoolBinding.cs](../../GameLib/Script/Common/Variables/Binding/ExternalBoolBinding.cs), [../../GameLib/Script/Common/Variables/Binding/ExternalFloatBinding.cs](../../GameLib/Script/Common/Variables/Binding/ExternalFloatBinding.cs) | Existing content-facing binding references may remain usable, but runtime value access beneath them must move to verified value identity rather than runtime stable-key lookup. |

Preserved does not mean frozen implementation.
It means stable value-facing contract.

---

## Owned Migration Goals

Wave D must achieve all of the following:

- move accepted runtime value identity truth to verified `ValueKeyId` access
- move accepted generic runtime value state to verified `ValueStore` authority rather than Blackboard-owned truth
- eliminate runtime stable-key lookup, runtime registry lookup, and missing-identity repair from the accepted value path
- preserve representative authored value and DynamicValue surfaces while replacing authority underneath them
- move value initialization ownership to explicit verified init and lifecycle boundaries
- normalize grid-like blackboard state into schema-backed `Table`, `Record`, or `RecordList` representations
- separate DynamicValue wrapper preservation from tracker, cache, invalidation, and hot-path evaluation authority
- make mixed legacy and verified value authority visible, bounded, and unacceptable as the steady state

---

## Target Authority Model

Wave D target authority is defined by the following required model.

### Required Value Identity Boundary

1. Existing generated ids and representative authoring references remain valid migration-facing inputs.
2. Accepted runtime value access normalizes those inputs to verified `ValueKeyId` before read or write.
3. `StableKey` remains authoring, diagnostics, migration, and DebugMap metadata rather than runtime lookup truth.
4. Missing or ambiguous value identity fails before runtime execution instead of being repaired by runtime key generation or fallback lookup.
5. Representative consumers such as bindings and DynamicValue sources may accept migration-facing dual key surfaces only if runtime truth is already normalized.

Wave D therefore requires the v2 rules that:

- `ValueKeyId` is the accepted runtime value identity
- runtime stable-key lookup is forbidden in the accepted path
- runtime generation of missing value identities is forbidden in the accepted path
- runtime-only negative ids are forbidden in the accepted path
- generated value-key continuity may remain, but it must not hide the verified runtime identity boundary

### Required ValueStore Authority Chain

1. Verified boot or an upstream verified runtime surface supplies the artifact set that contains `ValueSchemaPlan`, `ValueStoreInitPlan`, and any required evaluation-plan references.
2. Generic value state is created only within explicit migrated scope lifetime boundaries.
3. `ValueStore` owns runtime read, write, revision, and schema-backed slot or table state for the accepted path.
4. Unknown keys, schema-incompatible writes, and missing required values fail closed.
5. `ValueStore` may use a `VarStore`-like container as an implementation detail only if verified schema and identity rules remain authoritative and no legacy fallback semantics leak through.
6. Accepted runtime value truth does not depend on Blackboard hierarchy traversal, registry lookup, or asset-convention loading.

Wave D therefore requires the replacement or demotion of the following current authority points:

- `BlackboardService` as accepted value truth owner
- `VarKeyRegistryLocator` runtime asset lookup as accepted identity truth
- `VarStorePayload` or equivalent payload helpers as hidden value-architecture owners

### Required Blackboard Boundary

1. Blackboard may remain temporarily only as a facade or compatibility adapter over verified value authority.
2. Hierarchical parent traversal and root-fallback creation are not accepted runtime truth.
3. Global read or write continuity, if preserved, must be defined through explicit scope-store policy or a bounded compatibility adapter.
4. Debug view wiring, transform auto-write, and other mixed concerns must stop deciding accepted value truth.

Wave D therefore requires the following replacement rules:

- `TryGlobalGetVariant()` walking parent scopes must stop being accepted architecture
- `CreateLocal`, `CreateGameLogicRoot`, and `CreateRoot` fallback writes must stop being accepted architecture
- `BlackboardMB` must stop being the mixed owner of service registration, initialization, transform projection, and diagnostics wiring in the accepted path

### Required Init and Lifecycle Authority Chain

1. Representative authored init surfaces normalize into verified init contributions and explicit lifecycle participation.
2. Initialization ordering and overwrite policy are explicit plan data rather than callback-order accidents.
3. If an init value depends on runtime context, that dependency is expressed through an explicit dynamic evaluation plan instead of hidden direct evaluation.
4. Construct or Start or OnAcquire multi-path initialization is forbidden in the accepted path.
5. Lifecycle invokes verified init behavior rather than Blackboard-owned callback logic becoming architecture truth.

Wave D therefore requires the v2 rules that:

- `ValueStoreInitPlan` owns init writes
- dynamic evaluation must not be hidden inside generic initialization
- lifecycle ordering is explicit plan data, not runtime component behavior

### Required Grid Normalization Chain

1. Grid-like authored content may remain a migration-facing surface.
2. Accepted runtime must normalize grid-like state into schema-backed `Table`, `Record`, or `RecordList` value structures before value truth is established.
3. Row identity, column identity, cell schema, revision policy, and save policy must be explicit.
4. Grid cells holding arbitrary `VarStorePayload` without schema are forbidden in the accepted path.
5. Legacy grid compatibility may remain only as a bounded normalization adapter rather than runtime value truth.

### Required DynamicValue Evaluation Boundary

1. `DynamicValue` wrappers remain valid authored inputs.
2. Tracker, cache, invalidation, nested dependency capture, and hot-path evaluation reuse move under the explicit evaluation-runtime authority defined by v2.
3. Direct wrapper evaluation may remain only for low-frequency compatibility paths, not as accepted hot-path architecture.
4. Source-local computed result caches, hidden reevaluation loops, and `GetOrDefault`-style masking of required failures are forbidden in accepted runtime paths.
5. Nested DynamicValue reads must participate in one explicit dependency-tracking model rather than source-local special cases.

### Transitional Coexistence Rules

| Transitional Condition | Allowed During Wave D | Required Rule |
| --- | --- | --- |
| Existing content still authors `DynamicValue` wrappers and `VarKeyRef` fields | Yes | The wrapper and dual-surface authoring may remain, but accepted runtime access must be normalized before value read or write truth is established. |
| A `VarStore`-like container still backs migrated store storage | Yes | It may remain only as an implementation detail under verified schema and identity authority, not as a reason to keep runtime fallback semantics. |
| `BlackboardService` still exists in the repository | Yes | It may remain only as a bounded facade or compatibility adapter; hierarchical fallback and root repair must not remain accepted architecture. |
| Grid init is still authored through `BlackboardMB`-style surfaces during transition | Temporarily | Accepted runtime must normalize that content into schema-backed grid value structures before execution truth. |
| External bindings still carry migration-facing stable keys in authored data | Temporarily | Accepted runtime value access beneath them must not rely on runtime stable-key lookup. |
| Scalar runtime code still exists separately | Yes | Wave D must not absorb scalar semantics or silently redefine scalar ownership. |
| Runtime stable-key lookup, registry asset fallback, or blackboard root creation still decide accepted outcomes | No | Accepted Wave D behavior must fail validation, diagnostics, or acceptance rather than silently depending on legacy truth. |

---

## Forbidden Shortcuts

The following shortcuts are explicitly forbidden for Wave D:

- treating generated key continuity as permission to keep runtime stable-key lookup
- treating `BlackboardService` parent traversal as harmless because existing content depends on it today
- keeping `CreateLocal`, `CreateGameLogicRoot`, or `CreateRoot` as hidden steady-state value repair paths
- preserving grid cells with arbitrary var payloads and calling the result a normalized table
- hiding `DynamicValue` evaluation inside generic init, generic getters, or ad hoc convenience helpers
- preserving source-local computed caches as the primary runtime evaluation architecture
- widening Wave D to absorb scalar runtime or broad gameplay migration work

---

## Diagnostics and Failure Policy

v2 specification 11 owns the diagnostics substrate.
Wave D defines the conditions that must become diagnostic-visible and acceptance-visible.

| Code | Failure Condition | Required Result |
| --- | --- | --- |
| V21-WD-ID-001 | Accepted runtime path does not prove normalization from preserved migration-facing value identity to verified runtime value identity. | Validation or runtime entry into the accepted path must fail before value access. |
| V21-WD-ID-002 | Accepted runtime path performs stable-key lookup, runtime registry lookup, or runtime-only identity repair for required values. | Validation or diagnostics must reject the path as legacy value identity fallback. |
| V21-WD-STORE-001 | `ValueSchemaPlan`, `ValueStoreInitPlan`, or equivalent required value projection is missing for the accepted path. | Entry into the migrated value path must fail closed before representative value access begins. |
| V21-WD-STORE-002 | Unknown or schema-incompatible value access is repaired by fallback creation, silent defaulting, or Blackboard-root repair. | The operation must fail with structured diagnostics rather than being silently repaired. |
| V21-WD-BB-001 | Blackboard hierarchy traversal or root-fallback creation still decides accepted value outcomes. | Validation or diagnostics must reject the path as legacy blackboard authority. |
| V21-WD-INIT-001 | Initialization still depends on Construct or Start or OnAcquire multi-path behavior, callback order, or serialized list order instead of explicit plan order. | Acceptance must fail because init authority remains implicit. |
| V21-WD-GRID-001 | Grid-like runtime value state remains arbitrary var payload storage without schema-backed normalization. | Validation or runtime acceptance must reject the path as unnormalized grid authority. |
| V21-WD-DYN-001 | Generic init or generic value access still hides direct `DynamicValue` evaluation as accepted runtime truth. | Validation or diagnostics must reject the path as hidden dynamic evaluation authority. |
| V21-WD-DYN-002 | Source-local computed caches, hidden fallback defaults, or uncaptured nested dependencies remain the accepted runtime evaluation truth. | Acceptance must fail until explicit evaluation-runtime ownership is restored. |
| V21-WD-LEGACY-001 | Legacy and verified value authority are both active for the same accepted profile. | The profile must fail validation or runtime acceptance with a mixed-authority diagnostic. |
| V21-WD-BOUNDARY-001 | Wave D silently re-owns scalar runtime or command semantics while changing value authority. | The change must be rejected as scope drift in review or acceptance. |

---

## Acceptance Criteria

Wave D is complete only when all of the following are true:

- accepted runtime value access enters through verified runtime value identity and no longer depends on runtime stable-key lookup or registry-asset lookup
- representative generated key identity continuity remains stable enough that existing content does not require silent renumbering
- accepted runtime generic value truth no longer depends on Blackboard hierarchy traversal or root creation fallback
- representative local-init and grid-init content remains usable while init ownership moves to explicit verified init and lifecycle boundaries
- grid-like value state is normalized into schema-backed table or record-style value structures rather than arbitrary per-cell var payloads
- DynamicValue wrapper authoring remains usable while tracker, cache, invalidation, and hot-path evaluation truth move under explicit evaluation-runtime authority
- representative runtime consumers such as value init, VarStore-backed reads, and external bindings continue to function without preserving legacy lookup truth
- mixed legacy and verified value authority is diagnosable and unacceptable rather than silently tolerated
- scalar runtime and binding remain explicitly outside Wave D main scope and are not silently re-owned here

---

## Out of Scope and Handoff to Later Waves

The following work is explicitly deferred:

- scalar runtime and binding specialization, which remain owned by [../v2/10_1_ScalarRuntimeAndBindingSpec.md](../v2/10_1_ScalarRuntimeAndBindingSpec.md) and are handed off beyond the main Wave D slice
- representative gameplay-system migration that consumes the migrated value path, which belongs to Wave E
- final deletion of all legacy Blackboard or Var adapters and hardening cleanup, which belong to [06_WaveFLegacyRemovalAndHardeningSpec.md](06_WaveFLegacyRemovalAndHardeningSpec.md)

Wave D should make those waves easier.
It must not claim to have completed them.

---

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V21-WD-01 | Confirm accepted runtime value access uses verified runtime value identity. | Representative runtime reads and writes must prove normalization to verified runtime identity before access. |
| TC-V21-WD-02 | Confirm runtime stable-key lookup and runtime identity repair are rejected in the accepted path. | A profile that still depends on stable-key runtime lookup, registry lookup, or identity repair must fail validation or acceptance. |
| TC-V21-WD-03 | Confirm generated value-key identity continuity remains stable. | Representative existing generated ids must remain mappable without silent renumbering. |
| TC-V21-WD-04 | Confirm blackboard hierarchical fallback is rejected. | Accepted runtime value behavior must not depend on parent traversal or root-creation fallback. |
| TC-V21-WD-05 | Confirm representative init surfaces execute through explicit verified init and lifecycle authority. | Representative local-init and acquire-init content must remain functional without Blackboard-owned multi-path initialization. |
| TC-V21-WD-06 | Confirm grid-init content normalizes into schema-backed value structures. | Representative grid content must reach a verified table or record-style runtime shape rather than arbitrary cell payload truth. |
| TC-V21-WD-07 | Confirm DynamicValue wrapper continuity and explicit evaluation-runtime authority coexist correctly. | Authored wrappers must remain usable while tracked evaluation, dependency capture, and cache rules remain explicit. |
| TC-V21-WD-08 | Confirm representative runtime consumers keep working without runtime stable-key truth. | Representative bindings, init evaluation, and VarStore-backed consumers must remain functional through migrated value authority. |
| TC-V21-WD-09 | Confirm mixed legacy and verified value authority is rejected. | A profile that leaves Blackboard fallback or runtime key lookup active alongside migrated value authority must fail validation or acceptance. |
| TC-V21-WD-10 | Confirm scalar specialization remains outside Wave D main scope. | Wave D acceptance must not claim scalar runtime or binding semantics are migrated merely because generic value authority moved. |

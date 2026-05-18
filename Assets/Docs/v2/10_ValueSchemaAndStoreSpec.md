# Value Schema and Store Specification

## Document Status

- Document ID: 10_ValueSchemaAndStoreSpec
- Status: Draft
- Role: defines abstract value identity, schema, runtime value storage, initialization plans, generic value-state boundaries, save metadata boundaries, and value diagnostics for Kernel v2; scalar runtime and binding semantics are delegated to 10-1
- Depends on:
- [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md)
- [01_KernelIRSpec.md](01_KernelIRSpec.md)
- [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md)
- [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md)
- [04_DependencyValidationSpec.md](04_DependencyValidationSpec.md)
- [05_BootManifestAndProfileSpec.md](05_BootManifestAndProfileSpec.md)
- [06_ServiceGraphRuntimeSpec.md](06_ServiceGraphRuntimeSpec.md)
- [07_ScopeGraphRuntimeSpec.md](07_ScopeGraphRuntimeSpec.md)
- [08_LifecyclePlanSpec.md](08_LifecyclePlanSpec.md)
- [09_CommandCatalogRuntimeSpec.md](09_CommandCatalogRuntimeSpec.md)
- Provides foundation for:
- [10_1_ScalarRuntimeAndBindingSpec.md](10_1_ScalarRuntimeAndBindingSpec.md)
- [10_2_DynamicValueEvaluationSpec.md](10_2_DynamicValueEvaluationSpec.md)
- [11_DebugMapAndDiagnosticsSpec.md](11_DebugMapAndDiagnosticsSpec.md)
- [12_UnityAuthoringBridgeSpec.md](12_UnityAuthoringBridgeSpec.md)
- [13_LegacyCompatBoundarySpec.md](13_LegacyCompatBoundarySpec.md)
- [14_PerformanceBudgetAndRuntimeRulesSpec.md](14_PerformanceBudgetAndRuntimeRulesSpec.md)
- [15_TestAndValidationSpec.md](15_TestAndValidationSpec.md)

10 owns the runtime contract for values after they have been declared, normalized, validated, and projected.

This specification does not finalize concrete storage layout, serialized asset API, or editor UI.

## Ownership

This specification owns:

- `ValueKeyId` runtime access policy
- `ValueSchema` runtime acceptance requirements
- `ValueStore` runtime responsibility
- `ValueStoreScope` and lifetime policy
- `ValueStoreInitPlan` behavior
- initialization ordering and overwrite policy
- value read/write access policy
- abstract value kind and generic storage policy
- record, record list, table, and layered numeric state policy
- revision and dirty signaling requirements
- DynamicEvaluation boundary
- ReactiveEvaluation boundary
- CommandLocal boundary
- command read/write access boundary
- save metadata boundary
- runtime stable-key fallback prohibition
- value diagnostics and DebugMap requirements
- value failure behavior
- value performance and memory constraints
- Blackboard, VarStore, GridBlackboard, DynamicValue, and CommandLocal migration policy

This specification does not own:

- final scalar modifier, binding, or telemetry runtime contract
- final ReactiveResolver graph implementation
- final CommandCatalog dispatch implementation
- final SaveSystem payload format
- editor registry UI
- ScopeGraph parent-child implementation
- ServiceGraph service cache implementation
- RuntimeQuery index implementation
- Unity authoring component schema

If a lower spec needs to execute value behavior, it must use the contracts defined here rather than recreating Blackboard-style fallback semantics.

## Purpose

This specification defines how Kernel v2 names, validates, initializes, stores, reads, writes, observes, and diagnoses runtime values.

Core position:

```md
ValueSchema defines what values may exist.
ValueStore stores runtime state.
ValueStoreInitPlan defines initial writes.
Float-specialized scalar runtime and binding semantics are defined in 10-1.
Dynamic / Reactive evaluation is not hidden inside generic initialization.
Runtime stable-key fallback is forbidden in target kernel paths.
```

The central runtime rule is:

```md
ValueStore stores verified values by ValueKeyId.
It does not discover keys, infer schema, evaluate hidden dynamic expressions, or repair missing data at runtime.
```

## Scope

This specification defines:

- value identity model
- stable-key boundary
- value schema model
- value kind and type model
- runtime store contract
- store scope and lifetime policy
- storage policy requirements
- read/write access policy
- init plan model
- init ordering and overwrite rules
- table, record, and record list policy
- layered numeric policy
- revision and dirty signal policy
- DynamicEvaluation boundary
- ReactiveEvaluation boundary
- CommandLocal boundary
- command value access boundary
- save metadata boundary
- RuntimeQuery boundary
- value diagnostics
- value failure policy
- value performance and memory policy
- legacy migration policy
- forbidden patterns
- required test cases

## Non-Goals

This specification does not define:

- final `ValueStore` memory layout
- final generated accessor API
- final reactive dependency graph
- final DynamicEvaluation evaluator implementation
- final SaveSystem file format
- final editor registry UI
- final Unity authoring component schema
- final command dispatch implementation
- final runtime query index implementation

This specification must not turn `ValueStore` into Blackboard v2.

## Relationship to Other Specs

| Spec | Relationship |
|---|---|
| 00 | Defines explicit runtime, no runtime fallback, and value/schema ownership boundaries |
| 01 | Defines `ValueKeyIR`, typed identity domains, source locations, and normalized value identity |
| 02 | Defines value-related contributions without allowing installer-style runtime mutation |
| 03 | Generates `ValueSchemaPlan`, `ValueStoreInitPlan`, and value projections as artifacts, not source of truth |
| 04 | Validates value keys, schema references, init compatibility, stable-key rejection, dynamic dependencies, command access, and save metadata |
| 05 | Boots only from verified value artifacts and must not use registry or `Resources.Load` fallback |
| 06 | May resolve services that own or expose value stores, but does not own values or dynamic evaluation |
| 07 | Owns scope lifetime and may reference scope-local value store boundaries without becoming a value store |
| 08 | Executes explicit lifecycle steps that may initialize stores, but does not infer value initialization |
| 09 | Declares command read/write access to `ValueKeyId` and owns CommandLocal execution context |
| 10-1 | Owns float-specialized scalar runtime, modifier, binding, telemetry, and failure semantics layered on top of verified numeric definitions from 10 |
| 10-2 | Owns `DynamicValue`, `DynamicEvaluationPlan`, `ReactiveEvaluationPlan`, tracker, cache, invalidation, and nested dependency capture semantics; 10 owns only the value-state boundary and revision signals consumed by that layer |
| 11 | Owns the shared structured diagnostics substrate and DebugMap runtime contract used by value runtime; 10 defines required value provenance fields, init or table diagnostics context, and failure behavior |
| 12 | Produces authoring inputs that normalize stable keys into `ValueKeyId` before runtime |
| 13 | Defines the limited legacy boundary for Blackboard and VarStore migration |
| 14 | Defines hot-path budgets for value access, initialization, and dirty signaling |
| 15 | Turns required value tests into executable validation and CI coverage |

## Assembly Definition and Compile Boundary Expectations

The intended assembly home for generic value schema and store runtime is `GameLib.Kernel.Value`.
Scalar specialization and dynamic evaluation belong in their own leaf assemblies defined by 10-1 and 10-2.
Detailed dependency matrices remain owned by [17_AssemblyDefinitionAndCompileBoundarySpec.md](17_AssemblyDefinitionAndCompileBoundarySpec.md).

Required compile-boundary rules for 10:

- `GameLib.Kernel.Value` must remain separate from feature assemblies, legacy Blackboard or VarStore code, and concrete command implementations
- value core should remain Unity-free and use `noEngineReferences: true`
- dynamic evaluation logic, tracker logic, and scalar-specialized binding logic must not be collapsed back into the generic value assembly
- save payload formatting, Unity authoring extraction, and runtime object lookup helpers must stay outside generic value core

If value storage cannot compile without Unity APIs, legacy fallback helpers, or feature-specific runtime code, the 10 boundary has been violated.

## Current Value Debt Observations

Current value-related systems mix multiple responsibilities:

- service registration
- local value storage
- grid/table storage
- initialization
- DynamicValue evaluation
- lifecycle participation
- debug view binding
- transform auto-write
- runtime stable-key resolution
- registry fallback
- save-adjacent metadata

These observations are migration evidence.
They are not target architecture.

### Observation Traceability

| Observation | Evidence Type | Target Pressure |
|---|---|---|
| Blackboard authoring currently registers services, handlers, debug view, and init data from one MonoBehaviour. | Source | Split schema, store, init, lifecycle, and diagnostics |
| Value identity can be resolved from stable strings at runtime. | Source | Runtime access must use `ValueKeyId` |
| Missing stable keys can receive runtime-only negative IDs. | Source | Missing identity must fail validation |
| Registry lookup can use `Resources.Load` and create fallback runtime assets. | Source | Boot and runtime must consume verified inputs |
| Dynamic values can be evaluated during generic init. | Source | Dynamic dependencies must become explicit plans |
| Grid cells can carry arbitrary var payloads. | Source | Table and record cells must be schema-backed |
| Save metadata can mix Blackboard, scalar, and runtime scope binding concerns. | Source | Save metadata must be schema-backed and deterministic |

### Representative Anchors

- [BlackboardMB.cs](../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs) - service registration, grid registration, debug view, transform auto-writer, lifecycle handler registration, and multi-path initialization
- [VarIdResolver.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs) - runtime stable-key resolution and runtime-only negative ID allocation
- [VarKeyRegistryLocator.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs) - `Resources.Load` registry lookup and runtime fallback registry creation
- [VarStore.cs](../../GameLib/Script/Common/Variables/VarStore/Core/VarStore.cs) - dictionary-backed var/table storage, optional schema, revision, and runtime type coercion
- [VarStorePayload.cs](../../GameLib/Script/Common/Variables/VarStore/Payload/VarStorePayload.cs) - dynamic value evaluation, deferred dynamic writes, and table cell payload application
- [DeferredDynamicVarValue.cs](../../GameLib/Script/Common/Variables/VarStore/Core/DeferredDynamicVarValue.cs) - deferred dynamic evaluation as runtime value payload
- [GridBlackboardService.cs](../../GameLib/Script/Common/Variables/Blackboard/Core/GridBlackboardService.cs) - grid cell storage with var payloads outside a normalized table schema
- [ScopeBindingRegistryMB.cs](../../GameLib/Script/Common/Variables/Save/Binding/ScopeBindingRegistryMB.cs) - scope binding, value resolution, profile metadata, and save registration mixing
- [SavePlanTypes.cs](../../GameLib/Script/Common/Variables/Save/Plan/SavePlanTypes.cs) - save payload concepts that must consume value metadata without owning value schema

### Current Gaps

The target architecture must close these gaps:

- value existence can still be learned from runtime write behavior
- stable-key lookup can still act as runtime identity resolution
- initialization can still occur from `Construct`, `Start`, and `OnAcquire` paths
- dynamic evaluation can still hide dependencies inside generic initialization
- grid/table data can still bypass schema validation
- save metadata can still be inferred from runtime store contents
- debug output can still lack source-level provenance for value failures

## Value Architecture Definition

Target value architecture is split into four concepts:

1. `ValueSchema` defines what values may exist.
2. `ValueStore` stores runtime value state.
3. `ValueStoreInitPlan` defines initial writes and default state.
4. `EvaluationPlan` defines dynamic, reactive, or computed evaluation and is owned in detail by 10-2.

These concepts must not be collapsed into a single component, service, MonoBehaviour, or Blackboard facade.

10-1 defines the float-specialized scalar runtime and binding layer that consumes these verified value contracts without re-owning schema, save policy, or dynamic evaluation internals.

Pipeline:

```text
ValueContribution
  -> ValueKeyIR / ValueSchemaIR
  -> ValueSchemaPlan

ValueInitContribution
  -> ValueStoreInitPlan

Dynamic / Reactive Contribution
  -> DynamicEvaluationPlan / ReactiveEvaluationPlan

Runtime:
  ValueStore consumes ValueSchemaPlan and ValueStoreInitPlan
  Evaluation runtime semantics are owned by 10-2
```

`ValueStore` must not become the owner of schema generation, dynamic evaluation graph construction, save file writing, command execution, or runtime object lookup.

## Value Identity Model

`ValueKeyId` is the runtime identity for values.

Explanatory model:

```csharp
public readonly struct ValueKeyId
{
    public readonly int Value;
}
```

Related identity vocabulary:

- `ValueKeyId`
- `ValueSchemaId`
- `ValueStoreId`
- `ValueStoreScopeId`
- `ValueSlotId`
- `ValueTableId`
- `ValueFieldId`
- `ValueRevision`
- `ValueInitPlanId`

`StableKey` may exist for authoring, diagnostics, registry validation, migration, and DebugMap output.
`StableKey` is not runtime lookup truth.

Runtime access must use:

- `ValueKeyId`
- generated accessor backed by `ValueKeyId`
- validated command payload reference to `ValueKeyId`
- validated table/record field identity

Forbidden:

- runtime value access by raw string key
- runtime generation of missing `ValueKeyId`
- runtime-only negative value IDs
- using service identity as value identity
- using command payload field name as `ValueKeyId`
- using save payload field name as runtime value identity

## StableKey Boundary

`StableKey` is not runtime truth.

Allowed `StableKey` use:

- editor search
- authoring display
- migration mapping
- diagnostics
- registry diff
- generated DebugMap

Forbidden `StableKey` use:

- runtime read/write lookup
- runtime schema creation
- runtime fallback ID generation
- runtime save key generation unless preverified
- runtime command value access

Conversion from `StableKey` to `ValueKeyId` must happen before runtime execution during normalization, validation, or generation.

If a value cannot be converted to `ValueKeyId` before runtime, that value is invalid for target kernel execution.

## ValueSchema Model

`ValueSchema` defines the allowed shape of value data.

Explanatory model:

```csharp
public sealed class ValueSchemaPlan
{
    public ValueKeyId KeyId;
    public ValueSchemaId SchemaId;
    public string DebugName;
    public ValueKind Kind;
    public ValueStorageKind StorageKind;
    public ValueDefaultPolicy DefaultPolicy;
    public ValueAccessPolicy AccessPolicy;
    public SavePolicy SavePolicy;
    public SourceLocationId Source;
}
```

`ValueSchema` must define:

- value identity
- value kind
- storage kind
- default behavior
- read/write access policy
- initialization compatibility
- save metadata policy
- owner module
- source location
- profile availability

`ValueStore` must not infer schema from runtime writes.

Writing an unknown `ValueKeyId` must fail.
Writing a value incompatible with schema must fail.

## ValueKind and Type Model

The target type model must be schema-backed.

Explanatory model:

```csharp
public enum ValueKind
{
    Null = 0,
    Bool = 10,
    Int = 20,
    Long = 30,
    Float = 40,
    Double = 50,
    String = 60,
    Vector2 = 70,
    Vector3 = 80,
    Color = 90,
    ObjectRef = 100,
    ManagedRef = 110,
    Record = 200,
    RecordList = 210,
    Table = 220,
    LayeredNumeric = 300,
}
```

This model is explanatory.
It does not finalize the serialized API and does not replace existing legacy `DynamicVariant.ValueKind` numeric values.

Target vocabulary must distinguish:

- scalar values
- Unity or runtime object references
- managed references
- records
- record lists
- tables
- layered numeric values

`ManagedRef` is allowed only when schema explicitly permits it.
`ManagedRef` values must define save, clone, reset, and diagnostics behavior.

Silent type coercion is forbidden unless schema declares a specific conversion policy.

## ValueStore Runtime Definition

`ValueStore` is a runtime state container bound to a `ValueSchemaPlan`.

`ValueStore` owns:

- value slots
- current values
- revisions
- dirty flags
- optional table storage
- optional record storage
- optional layered numeric storage
- init application state
- diagnostics context

`ValueStore` does not own:

- schema generation
- dynamic evaluation graph
- reactive dependency graph
- save file writing
- command execution
- runtime object query
- service resolution
- lifecycle enrollment

`ValueStore` must be created from verified schema and store scope inputs.
It must not build schema by observing runtime writes.

## ValueStore Scope and Lifetime Model

Value stores may exist at different runtime lifetimes.

Explanatory model:

```csharp
public enum ValueStoreScopeKind
{
    Kernel = 10,
    Project = 20,
    Scene = 30,
    Scope = 40,
    Entity = 50,
    CommandLocal = 60,
    Test = 90,
}
```

Scope ownership rules:

- `Kernel` store is valid only for kernel-wide values.
- `Project` store is valid for project runtime state.
- `Scene` store is valid for scene-local state.
- `Scope` store is valid for authored or verified runtime scopes.
- `Entity` store is valid only as compact entity state, not as a service.
- `CommandLocal` store is valid only inside command execution boundaries.
- `Test` store is valid only for deterministic test fixtures.

Entity-scoped values should prefer compact store slices or pooled store instances.
Creating heavy dictionary-backed stores per entity is forbidden by default.

`ValueStore` lifetime must be explicit.
Store reuse must obey the reset policy defined by this spec and executed through 08.

## ValueStore Storage Model

`ValueStore` storage must be schema-indexed.

Recommended structure:

- schema maps `ValueKeyId` to slot index
- slot stores typed value
- slot has revision
- store has revision
- optional per-kind backend handles records, record lists, tables, and layered numerics

Explanatory model:

```csharp
public enum ValueStorageKind
{
    InlineScalar = 10,
    InlineStruct = 20,
    ManagedReference = 30,
    RecordStorage = 40,
    RecordListStorage = 50,
    TableStorage = 60,
    LayeredNumericStorage = 70,
}
```

Forbidden:

- hot path `Dictionary<string, object>`
- hot path stable-key lookup
- schema inference by first write
- boxing common scalar values by default when avoidable
- LINQ in read/write hot paths
- `Resources.Load` in value access paths

Slot lookup should be O(1) or bounded small constant.

## ValueStore Access Policy

`ValueStore` access must be typed and schema-validated.

Allowed access forms:

- `TryRead<T>(ValueKeyId, out T)`
- `TryWrite<T>(ValueKeyId, T)`
- `ReadRequired<T>(ValueKeyId)`
- `WriteRequired<T>(ValueKeyId, T)`
- generated accessor backed by `ValueKeyId`

`TryRead` may return false for optional data or explicit absence checks.
Required reads must report structured diagnostics on failure.

Access policy must distinguish:

- read
- write
- read/write
- init-only
- command-only
- save-only metadata
- debug-only display

Forbidden:

- `TryRead(string stableKey)`
- `TryWrite(string stableKey, object value)`
- implicit key creation on write
- silent type coercion unless schema declares conversion
- treating missing required value as default without policy

## ValueStoreInitPlan Model

`ValueStoreInitPlan` defines initial writes applied to a `ValueStore`.

It must define:

- target store scope
- target schema
- entries
- ordering
- overwrite policy
- source location
- profile availability
- execution phase

Explanatory model:

```csharp
public sealed class ValueInitEntryPlan
{
    public ValueKeyId KeyId;
    public ValueInitValueKind ValueKind;
    public ValuePayload Payload;
    public ValueInitOverwritePolicy OverwritePolicy;
    public SourceLocationId Source;
}
```

`ValueStoreInitPlan` must not evaluate arbitrary `DynamicValue` by default.
Dynamic evaluation must be represented explicitly.

Initialization must execute at explicit lifecycle boundaries defined by 08.
`Construct`, `Start`, and `OnAcquire` multi-path initialization is forbidden in target kernel paths.

## Initialization Ordering and Overwrite Policy

Duplicate initialization entries for the same `ValueKeyId` are validation errors unless the plan defines deterministic merge or overwrite policy.

Explanatory model:

```csharp
public enum ValueInitOverwritePolicy
{
    ErrorIfExists = 10,
    KeepExisting = 20,
    Overwrite = 30,
    ClearIfNull = 40,
    Merge = 50,
}
```

Rules:

- `ErrorIfExists` is the default for duplicate writes.
- `KeepExisting` must define what counts as existing.
- `Overwrite` must be deterministic and source-visible.
- `ClearIfNull` must define null compatibility by schema.
- `Merge` must define merge semantics by value kind.

Last-write-wins by collection order is forbidden.

Init ordering must not depend on Unity callback order, component discovery order, or serialized list order unless the plan explicitly normalizes that list into deterministic order.

## Table / Record / RecordList Policy

Grid-like values must be represented as `Table` or `RecordList` schema, not as a separate Blackboard subsystem.

`Table` schema must define:

- row identity policy
- column identity policy
- cell schema
- sparse or dense storage policy
- default cell policy
- revision policy
- save policy
- diagnostics source

`Record` schema must define:

- field identity
- field kind
- required or optional status
- default policy
- nested schema compatibility

`RecordList` schema must define:

- element schema
- ordering policy
- identity policy
- mutation policy
- revision policy

Grid storage must not hide arbitrary var payloads per cell without schema.

Legacy grid payloads that store arbitrary `VarStorePayload` per cell are migration-required unless normalized into table or record schema before runtime.

## LayeredNumeric Policy

`LayeredNumeric` is a structured numeric value with contribution lanes.

Default lanes:

- `Base = 10`
- `PrefixMul = 20`
- `Add = 30`
- `SuffixMul = 40`
- `FinalClamp = 50`
- `Effective = 60`

Base and effective values must be distinguishable.

Effective value is derived from base and contributions.
Writing effective directly is forbidden unless schema explicitly permits override.

Changing any contribution must update `LayeredNumeric` revision.
Effective recalculation may be lazy if dependency signaling remains correct.

Layered numeric policy must define:

- numeric type
- contribution ordering
- contribution identity
- conflict policy
- clear/reset policy
- revision behavior
- save behavior

## Revision and Dirty Signal Policy

Every writable `ValueStore` slot must have revision metadata.

Required revision concepts:

- slot revision
- store revision
- optional record field revision
- optional table row revision
- optional table column revision
- optional table cell revision
- optional layered numeric effective revision

`ValueStore` may emit dirty signals, but dirty evaluation belongs to dependency and reaction systems.

Minimal dirty signal data:

- `ValueKeyId`
- old revision
- new revision
- store id
- store scope
- optional scope handle
- optional entity handle

Dirty signals must not become an implicit reactive dependency graph.
Reactive graph ownership belongs outside `ValueStore`.

## DynamicEvaluation Boundary

10 owns only the boundary between `ValueStore` initialization and dynamic evaluation.
Concrete `DynamicValue`, tracker, cache, invalidation, and nested dependency semantics are owned by 10-2.

DynamicValue-style evaluation must not be hidden inside `ValueStore` initialization.

If an initial value depends on runtime context, the dependency must be explicit.

Dynamic evaluation must declare:

- input dependencies
- evaluation timing
- fallback policy
- target `ValueKeyId`
- target store scope
- diagnostics source
- failure boundary

Legacy shape:

```text
BlackboardMB entry.Value.Evaluate(ctx) during OnAcquire
```

Target shape:

```text
DynamicEvaluationPlan
  inputs: ValueStore / RuntimeQuery / Scope / CommandFrame
  output: ValueKeyId
  phase: Init or Acquire
```

Deferred dynamic value writes are not generic init entries.
They must be represented as dynamic evaluation plans or rejected.

Detailed source contract, tracked dependency capture, shared cache ownership, and invalidation policy belong to 10-2.

## ReactiveEvaluation Boundary

10 owns only the boundary between `ValueStore` revisions or dirty signals and reactive evaluation.
Concrete tracked evaluation, cached computed value policy, invalidation rules, and scheduling semantics are owned by 10-2.

Reactive evaluation is not owned by `ValueStore`.

`ValueStore` provides:

- values
- revisions
- dirty signals

Reactive evaluation owns:

- dependency graph
- tracked evaluation
- cached computed values
- invalidation
- scheduling
- failure boundary

`ValueStore` must not become `ReactiveResolver`.

Reactive dependencies must reference `ValueKeyId`, store scope, and runtime query inputs explicitly.
The detailed tracker and reactive cache model belong to 10-2.

## CommandLocal Boundary

`CommandLocal` is execution-local value storage.

It is not a scope store.
It is not saved.
It must not leak outside its command boundary unless explicitly exported.

Valid `CommandLocal` lifetimes:

- command frame
- command sequence
- async wait boundary
- nested command block

`CommandLocal` may use value-like typed slots, but it is not allowed to become global Blackboard.

Exporting command-local data to a persistent store requires an explicit command write declaration and schema-compatible target `ValueKeyId`.

## Command Access Boundary

Command access to `ValueStore` must be declared.

`CommandContribution` must declare:

- read `ValueKeyId` set
- write `ValueKeyId` set
- target store scope
- access phase
- failure behavior

Command executor must not resolve stable keys at runtime.

Command write access must validate:

- target key exists
- schema allows write
- command has declared access
- value type matches schema
- target store scope is valid for command frame

Command read access must validate:

- target key exists
- schema allows read
- command has declared access
- absence policy is explicit for optional reads

## Save Metadata and Save Payload Boundary

Save metadata must be schema-backed.

`ValueStore` must not infer save targets by scanning runtime store contents.

Save policy may be defined by:

- `ValueSchema`
- explicit `SavePlan`
- profile-specific save contribution

Explanatory model:

```csharp
public enum SavePolicy
{
    None = 0,
    RuntimeOnly = 10,
    Save = 20,
    SaveIfDirty = 30,
    SnapshotOnly = 40,
    MigrationOnly = 90,
}
```

This specification does not define the final save payload format.

This specification defines the value metadata required so SaveSystem can build payloads deterministically.

Save metadata must include:

- `ValueKeyId`
- store scope
- value kind
- storage kind
- save policy
- version or migration metadata
- source location
- profile availability

SaveSystem must not treat arbitrary runtime store contents as save authority.

## RuntimeQuery Boundary

`ValueStore` may be associated with runtime objects through handles.

Runtime object lookup belongs to RuntimeQuery.

`ValueStore` must not locate:

- entities
- scopes
- actors
- UI roots
- scene objects
- Unity components

Value access can consume a handle provided by RuntimeQuery, but it must not perform the query itself.

`ValueStore` must not implement actor lookup, scope lookup, scene search, or hierarchy search.

## Diagnostics and DebugMap Requirements

Value diagnostics must include:

- `ValueKeyId`
- stable key if available
- display name
- `ValueKind`
- schema id
- store id
- store scope
- owner module
- source location
- current revision if available
- selected profile

Init diagnostics must also include:

- `ValueInitPlanId`
- init entry source
- overwrite policy
- execution phase
- target store scope

Table diagnostics must also include:

- `ValueTableId`
- row identity
- column identity
- cell schema id
- cell revision if available

Representative error codes:

- `VALUE_KEY_MISSING`
- `VALUE_SCHEMA_MISSING`
- `VALUE_TYPE_MISMATCH`
- `VALUE_WRITE_ACCESS_DENIED`
- `VALUE_READ_ACCESS_DENIED`
- `VALUE_STABLE_KEY_RUNTIME_LOOKUP_FORBIDDEN`
- `VALUE_RUNTIME_ID_GENERATION_FORBIDDEN`
- `VALUE_INIT_DUPLICATE_ENTRY`
- `VALUE_INIT_TYPE_MISMATCH`
- `VALUE_INIT_DYNAMIC_DEPENDENCY_UNDECLARED`
- `VALUE_INIT_MULTIPATH_FORBIDDEN`
- `VALUE_TABLE_SCHEMA_MISSING`
- `VALUE_GRID_PAYLOAD_SCHEMA_REQUIRED`
- `VALUE_LAYERED_EFFECTIVE_WRITE_FORBIDDEN`
- `VALUE_SAVE_POLICY_INVALID`
- `VALUE_COMMAND_ACCESS_UNDECLARED`

If DebugMap is missing, diagnostics must still emit stable numeric IDs and stable error codes.

## Failure Policy

Value failure must not be silently repaired.

Failure categories:

- `MissingKey`
- `MissingSchema`
- `TypeMismatch`
- `AccessDenied`
- `StoreDisposed`
- `InitConflict`
- `DynamicDependencyMissing`
- `StableKeyRuntimeLookup`
- `RuntimeIdGeneration`
- `SavePolicyInvalid`

Failure boundary:

| Failure | Boundary |
|---|---|
| Boot schema failure | boot failure |
| Missing verified schema artifact | boot failure |
| Scope init failure | scope failure |
| Command write failure | command failure or frame failure |
| Reactive evaluation failure | reactive failure boundary |
| Save metadata failure | save operation failure |
| Runtime stable-key lookup attempt | operation failure and diagnostics |

Fallback defaults are allowed only when schema explicitly defines them.
Missing required values must not be repaired by runtime key creation, silent default creation, or legacy registry fallback.

## Performance and Memory Policy

`ValueStore` read/write is a runtime hot path.

Target requirements:

- no stable-key lookup in hot path
- no `Resources.Load` in value access path
- no managed allocation in normal scalar read/write
- no boxing for common scalar types where practical
- no LINQ in hot paths
- slot lookup should be O(1) or bounded small constant
- revision update should be cheap
- table access must define sparse or dense performance expectations
- dirty signal emission must avoid unbounded allocation

Scalar-specific hot-path, binding, and handle-lifetime budgets are defined in 10-1.

Entity-level value data must use compact storage.
Creating heavy dictionary-backed stores per entity is forbidden by default.

Performance must not be optimized by skipping schema checks, access policy checks, revision updates, or diagnostics metadata required by profile.

## Legacy Migration Policy

| Legacy Pattern | Target Representation |
|---|---|
| `VarId` int | `ValueKeyId` |
| stable-key string lookup | pre-runtime `ValueKeyId` mapping |
| `VarKeyRegistry` | verified value registry input or generated artifact |
| `VarIdResolver` runtime negative IDs | forbidden in target runtime |
| `VarKeyRegistryLocator.Resources.Load` | boot-time verified artifact reference |
| `BlackboardService` | `ValueStore` service or scope store facade |
| `GridBlackboardService` | `Table`, `Record`, or `RecordList` store |
| `BlackboardMB` local init | `ValueStoreInitContribution` |
| `BlackboardMB.OnAcquire` init | `LifecycleContribution` invoking verified init |
| `DynamicValue` in init entry | `DynamicEvaluationPlan` |
| `DeferredDynamicVarValue` | explicit dynamic evaluation plan or rejected migration |
| `TransformVarAutoWriterService` | explicit Transform-to-Value bridge contribution |
| `BlackboardDebugView` | diagnostics, DebugMap, or editor inspector |
| Save blackboard/scalar metadata mixing | schema-backed save metadata projection |

Legacy migration must not preserve runtime fallback semantics.

Legacy names may appear in diagnostics and migration reports, but they must not define target runtime truth.

## Forbidden Patterns

The following are forbidden in target ValueSchema / ValueStore runtime:

- runtime stable-key lookup for required values
- runtime creation of missing `ValueKeyId`
- runtime-only negative IDs
- `Resources.Load` registry fallback
- inferring schema from runtime writes
- raw `Dictionary<string, object>` hot path
- silent type coercion
- hidden `DynamicValue` evaluation inside generic initialization
- duplicate init entries resolved by collection order
- Blackboard-style `Construct` / `Start` / `OnAcquire` multi-path initialization
- `ValueStore` resolving runtime objects or scopes
- `CommandLocal` used as global Blackboard
- SaveSystem inferring save targets by scanning arbitrary store contents
- grid cells holding arbitrary var payloads without schema
- command value access without declared read/write policy
- value diagnostics that cannot map back to source or stable error code

## Test Case Model

Each ValueSchema / ValueStore test case must define:

- Test ID
- Title
- `ValueSchemaPlan` fixture
- `ValueStore` fixture
- `ValueStoreInitPlan` fixture if applicable
- Operation
- Expected result
- Expected diagnostics
- Expected revision changes
- Expected allocation or performance assertion if applicable

## Required Test Cases

### Identity / StableKey Tests

#### TC_VALUE_ID_001_ReadByValueKeyId

```text
Input:
- ValueSchema contains ValueKeyId health.current
- Store has value

Operation:
- Read by ValueKeyId

Expected:
- Passed
```

#### TC_VALUE_ID_002_StableKeyRuntimeLookupRejected

```text
Operation:
- Read value by "health.current" at runtime

Expected:
- Failed
- VALUE_STABLE_KEY_RUNTIME_LOOKUP_FORBIDDEN
```

#### TC_VALUE_ID_003_RuntimeNegativeIdRejected

```text
Input:
- ValueKeyId = -1

Expected:
- Failed
- VALUE_RUNTIME_ID_GENERATION_FORBIDDEN
```

### Schema Tests

#### TC_VALUE_SCHEMA_001_WriteMatchingType

```text
Input:
- ValueKey kind = Int

Operation:
- Write int

Expected:
- Passed
- slot revision incremented
```

#### TC_VALUE_SCHEMA_002_WriteTypeMismatchRejected

```text
Input:
- ValueKey kind = Int

Operation:
- Write string

Expected:
- Failed
- VALUE_TYPE_MISMATCH
```

#### TC_VALUE_SCHEMA_003_WriteUnknownKeyRejected

```text
Operation:
- Write ValueKeyId not in schema

Expected:
- Failed
- VALUE_KEY_MISSING
```

### InitPlan Tests

#### TC_VALUE_INIT_001_InitPlanAppliesDefaults

```text
Input:
- InitPlan writes health.current = 100

Expected:
- Store contains health.current = 100
```

#### TC_VALUE_INIT_002_DuplicateInitWithoutPolicyRejected

```text
Input:
- InitPlan has two entries for same ValueKeyId
- no merge or overwrite policy

Expected:
- Failed
- VALUE_INIT_DUPLICATE_ENTRY
```

#### TC_VALUE_INIT_003_OverwritePolicyApplied

```text
Input:
- Existing value
- Init overwrite policy = KeepExisting

Expected:
- Existing value preserved
```

#### TC_VALUE_INIT_004_ConstructStartAcquireMultiPathForbidden

```text
Input:
- same init declared for Construct, Start, and Acquire without explicit policy

Expected:
- Failed
- VALUE_INIT_MULTIPATH_FORBIDDEN
```

### Dynamic / Reactive Tests

#### TC_VALUE_DYNAMIC_001_DynamicInitRequiresEvaluationPlan

```text
Input:
- Init entry uses DynamicValue
- no DynamicEvaluationPlan

Expected:
- Failed
- VALUE_INIT_DYNAMIC_DEPENDENCY_UNDECLARED
```

#### TC_VALUE_DYNAMIC_002_DynamicInitWithDeclaredInputs

```text
Input:
- DynamicEvaluationPlan declares inputs and output ValueKeyId

Expected:
- Passed
```

#### TC_VALUE_REACTIVE_001_RevisionChangeSignalsDirty

```text
Operation:
- Write value

Expected:
- slot revision increments
- dirty signal emitted
```

### Table / Record Tests

#### TC_VALUE_TABLE_001_TableCellWriteValid

```text
Input:
- Table schema exists
- cell schema matches value

Expected:
- Passed
```

#### TC_VALUE_TABLE_002_CellWriteWithoutSchemaRejected

```text
Input:
- Table cell write with missing schema

Expected:
- Failed
- VALUE_TABLE_SCHEMA_MISSING
```

#### TC_VALUE_TABLE_003_GridPayloadWithoutSchemaRejected

```text
Input:
- legacy grid cell has arbitrary VarStorePayload

Expected:
- Failed or migration-required
- VALUE_GRID_PAYLOAD_SCHEMA_REQUIRED
```

### LayeredNumeric Tests

#### TC_VALUE_NUMERIC_001_EffectiveRecomputedFromContributions

```text
Input:
- Base = 10
- Add +5
- PrefixMul 2

Expected:
- Effective computed by schema order
- revision updated
```

#### TC_VALUE_NUMERIC_002_WriteEffectiveRejectedByDefault

```text
Operation:
- Write Effective directly

Expected:
- Failed
- VALUE_LAYERED_EFFECTIVE_WRITE_FORBIDDEN
```

### Command Boundary Tests

#### TC_VALUE_CMD_001_CommandDeclaredWriteAllowed

```text
Input:
- Command declares write access to health.current

Expected:
- Write succeeds
```

#### TC_VALUE_CMD_002_CommandUndeclaredWriteRejected

```text
Input:
- Command writes health.current without access declaration

Expected:
- Failed
- VALUE_COMMAND_ACCESS_UNDECLARED
```

#### TC_VALUE_CMD_003_CommandStableKeyLookupRejected

```text
Input:
- Command writes by stable string key

Expected:
- Failed
- VALUE_STABLE_KEY_RUNTIME_LOOKUP_FORBIDDEN
```

### Save Tests

#### TC_VALUE_SAVE_001_SavePolicyIncludedInSchema

```text
Input:
- ValueSchema SavePolicy = Save

Expected:
- Save metadata projection includes key
```

#### TC_VALUE_SAVE_002_RuntimeStoreScanNotSaveAuthority

```text
Input:
- Runtime store contains unschematized value

Expected:
- SaveSystem does not save it
- VALUE_SAVE_SCHEMA_MISSING
```

### Performance Tests

#### TC_VALUE_PERF_001_ScalarReadNoAllocation

```text
Operation:
- Repeated scalar read

Expected:
- No managed allocation in normal path
```

#### TC_VALUE_PERF_002_ScalarWriteNoAllocation

```text
Operation:
- Repeated scalar write

Expected:
- No managed allocation in normal path
```

#### TC_VALUE_PERF_003_NoResourcesLoadDuringRuntimeAccess

```text
Operation:
- Read/write values during runtime

Expected:
- No Resources.Load registry access
```

## Acceptance Criteria

This specification is complete when it defines:

- value architecture split between Schema, Store, InitPlan, and Evaluation, with scalar runtime delegated to 10-1
- `ValueKeyId` identity model
- `StableKey` boundary
- `ValueSchema` model
- `ValueKind` and type model
- `ValueStore` runtime definition
- `ValueStore` scope and lifetime model
- storage model
- read/write access policy
- `ValueStoreInitPlan` model
- initialization ordering and overwrite policy
- table, record, and record list policy
- layered numeric policy
- revision and dirty signal policy
- DynamicEvaluation boundary
- ReactiveEvaluation boundary
- CommandLocal boundary
- command access boundary
- save metadata boundary
- RuntimeQuery boundary
- diagnostics and DebugMap requirements
- failure policy
- performance and memory policy
- legacy migration policy
- forbidden patterns
- required test cases

The specification is not complete if a required value can be created, looked up, or saved by runtime fallback.

## Test Cases

| Test Case | Purpose | Verification |
|---|---|---|
| TC-10-01 | Confirm runtime value access is `ValueKeyId` based. | Identity and stable-key sections reject runtime stable-key lookup and runtime ID generation. |
| TC-10-02 | Confirm writes are schema-bound. | Schema, access, and failure sections reject unknown keys and type mismatches. |
| TC-10-03 | Confirm init ordering is explicit. | Init and overwrite sections reject multi-path init and collection-order last-write-wins. |
| TC-10-04 | Confirm dynamic evaluation is not hidden in init. | Dynamic boundary and required tests require `DynamicEvaluationPlan`. |
| TC-10-05 | Confirm table and record values are schema-backed. | Table and record policy rejects arbitrary grid payloads without schema. |
| TC-10-06 | Confirm hot paths do not use fallback lookup. | Performance and forbidden sections reject stable-key and `Resources.Load` runtime paths. |

## Final Position

`ValueStore` stores verified values by `ValueKeyId`.

It does not discover keys, infer schema, evaluate hidden dynamic expressions, or repair missing data at runtime.

If a value can appear at runtime without `ValueSchema`, the architecture has already regressed.

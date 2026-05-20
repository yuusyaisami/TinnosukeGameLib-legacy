# CommandCatalog Runtime Specification

## Document Status

- Document ID: 09_CommandCatalogRuntimeSpec
- Status: Draft
- Role: defines command runtime identity, payload schema validation, executor resolution, command runner behavior, and command failure rules for Kernel v2
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
- Consumes:
  - CommandIR
  - CommandCatalogPlan
  - ServiceGraphPlan references
  - ValueSchemaPlan references
  - RuntimeQueryPlan references
  - ScopeGraphPlan references
  - LifecyclePlan references
  - KernelDebugMap
- Provides foundation for:
  - 10_ValueSchemaAndStoreSpec.md
  - 11_DebugMapAndDiagnosticsSpec.md
  - 12_UnityAuthoringBridgeSpec.md
  - 13_LegacyCompatBoundarySpec.md
  - 14_PerformanceBudgetAndRuntimeRulesSpec.md
  - 15_TestAndValidationSpec.md

### Ownership

This specification owns command runtime identity, payload schema validation, executor lookup, command execution flow, command-local state, command failure behavior, and command diagnostics requirements.
It does not own service caching, value storage layout, runtime query storage, scope graph structure, lifecycle dispatch, command authoring UI, visual scripting editor UI, SaveSystem internals, or SceneFlow internals.

This specification owns:

- CommandCatalog runtime responsibility
- CommandCatalogPlan runtime input contract
- CommandTypeId runtime dispatch contract
- authoring key and runtime identity boundary
- command contribution projection boundary
- command payload schema and validation policy
- command executor model
- executor factory and lifetime policy
- CommandRunner responsibility
- CommandFrame, CommandContext, and CommandLocal policy
- control-flow command policy
- async, wait, cancellation, and detached execution policy
- service, value, runtime query, scope, entity, actor, and lifecycle boundaries for commands
- command module and category policy
- command diagnostics and DebugMap requirements
- command failure policy
- command performance and memory rules
- legacy command migration policy

This specification does not own:

- ServiceGraph service cache implementation
- ValueStore storage layout
- RuntimeQuery index implementation
- ScopeGraph parent-child implementation
- LifecyclePlan phase dispatch
- command authoring UI details
- visual scripting editor UI details
- SaveSystem persistence semantics
- SceneFlow transition semantics

09 is the runtime command authority.
It is not a replacement for ServiceGraph, ValueStore, RuntimeQuery, ScopeGraph, or LifecyclePlan.

---

## Purpose

This specification defines how the target kernel resolves and executes commands from a verified CommandCatalogPlan.

CommandCatalog owns command identity, payload schema, executor resolution, and dispatch metadata.
Command executors are not discovered through ServiceGraph.
Command authoring keys are not runtime dispatch identities.
Command execution must use verified CommandTypeId and verified payload schema.

The core statement of 09 is:

```text
Command dispatch is table-driven by verified CommandTypeId.
It is not discovered from DI registrations and not resolved from authoring strings.
```

If adding a command requires editing a giant runtime installer, the architecture has regressed.

---

## Scope

This specification defines:

- CommandCatalog runtime responsibility
- CommandCatalogPlan input contract
- command identity model
- authoring key boundary
- command contribution projection boundary
- command payload schema model
- payload validation policy
- command executor model
- executor factory and lifetime policy
- CommandRunner responsibility
- CommandFrame and CommandContext model
- CommandLocal state policy
- control-flow command policy
- async, wait, cancellation, and detached execution policy
- ServiceGraph boundary
- ValueStore boundary
- RuntimeQuery boundary
- scope, entity, and actor target boundary
- LifecyclePlan boundary
- command module and category policy
- command diagnostics and DebugMap requirements
- command failure policy
- command performance and memory policy
- legacy command migration policy
- command runtime test case model and required tests

---

## Non-Goals

This specification does not define:

- final ServiceGraph cache implementation
- final ValueStore storage layout
- final RuntimeQuery index storage
- final ScopeGraph handle layout
- final LifecycleDispatcher implementation
- final SaveSystem implementation
- final SceneFlow implementation
- command authoring inspector UI
- visual scripting editor UI
- asset menu layout for command authoring

This specification must not turn CommandCatalog into:

- a general-purpose DI container
- a service registry
- a lifecycle registry
- a runtime query registry
- a value key resolver
- a string-key fallback resolver
- a giant runtime installer

---

## Relationship to Other Specs

| Spec | Relationship |
|---|---|
| [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md) | Defines command dispatch as explicit, verified, and separated from authoring-key lookup. |
| [01_KernelIRSpec.md](01_KernelIRSpec.md) | Defines CommandIR, CommandTypeId, command payload schema references, executor references, and typed identity rules consumed here. |
| [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md) | Defines CommandContribution as declarative input, not executor registration or installer mutation. |
| [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md) | Produces CommandCatalogPlan and validates projection completeness before runtime. |
| [04_DependencyValidationSpec.md](04_DependencyValidationSpec.md) | Validates command identity, executor references, payload schema references, service/value/query dependencies, and authoring-key misuse. |
| [05_BootManifestAndProfileSpec.md](05_BootManifestAndProfileSpec.md) | Boot accepts one verified artifact set and must not eagerly construct all command executors by default. |
| [06_ServiceGraphRuntimeSpec.md](06_ServiceGraphRuntimeSpec.md) | Provides declared service dependencies to command execution but must not discover command executors. |
| [07_ScopeGraphRuntimeSpec.md](07_ScopeGraphRuntimeSpec.md) | Provides explicit scope handles and scope state boundaries used by command context and target references. |
| [08_LifecyclePlanSpec.md](08_LifecyclePlanSpec.md) | Owns lifecycle participation; CommandRunner may be a lifecycle target, but command execution does not enroll lifecycle steps. |
| 10_ValueSchemaAndStoreSpec.md | Owns value schema and storage; commands only access values through verified ValueKeyId and declared access policy. |
| 11_DebugMapAndDiagnosticsSpec.md | Owns the shared structured diagnostics substrate and DebugMap runtime contract used by command runtime; 09 defines required command provenance fields, payload-related diagnostics context, and failure behavior. |
| 12_UnityAuthoringBridgeSpec.md | Normalizes command authoring objects, command keys, and payload authoring into CommandContribution/CommandIR. |
| 13_LegacyCompatBoundarySpec.md | Defines allowed legacy command adapters and migration boundary. |
| 14_PerformanceBudgetAndRuntimeRulesSpec.md | Defines command dispatch budgets, allocation rules, and profiler marker requirements. |
| 15_TestAndValidationSpec.md | Defines executable command catalog validation and regression fixtures. |

---

## Assembly Definition and Compile Boundary Expectations

The intended assembly home for this subsystem is `GameLib.Kernel.Command`.
Detailed dependency matrices remain owned by [17_AssemblyDefinitionAndCompileBoundarySpec.md](17_AssemblyDefinitionAndCompileBoundarySpec.md).

Required compile-boundary rules for 09:

- `GameLib.Kernel.Command` must remain separate from feature executor implementations, legacy command runners, and Unity authoring extraction code
- command runtime core should depend only on lower kernel assemblies and explicit public contracts from Runtime, ServiceGraph, ScopeGraph, and RuntimeQuery
- command executor discovery by service collection, installer mutation, or feature back-reference must not be compiled into `GameLib.Kernel.Command`
- Unity-specific command triggers, MonoBehaviour bridges, and feature command leaves must stay outside the command core assembly

If verified command dispatch cannot compile without feature executors, legacy runner code, or runtime string-key lookup helpers, the 09 boundary has been violated.

---

## Current Command Debt Observations

Current command runtime mixes command discovery, runner creation, catalog lookup, lifecycle enrollment, key resolution, fallback behavior, and diagnostics binding.

Observed command debt includes:

- command executor discovery through DI registration
- command executor map construction from `IReadOnlyList<ICommandExecutor>`
- command runner creation through scope-kind switch
- command runner lifecycle participation through service registration
- command key resolution as a runtime service
- command catalog lookup as a runtime service
- `Resources.Load` and runtime fallback catalog creation
- runtime-only command key IDs
- stable-key fallback from catalog entries
- command debug viewer binding through build callback
- executor count tied to boot registration cost
- command categories mixed inside a single installer
- command-local and value-store responsibilities mixed through `VarStore`
- actor, scope, target, and channel routing performed inside executors

The target CommandCatalog must not preserve this ambiguity.

### Observation Traceability

These observations are migration evidence only.
They are not target architecture.

- [CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs) - bulk executor registration, scope-kind runner registration, lifecycle handler registration, and debug viewer build callback
- [CommandRunner.cs](../../GameLib/Script/Common/Commands/VNext/Core/CommandRunner.cs) - runtime execution flow, registry lookup, failure handling, context slots, and lifecycle handler implementation
- [CommandExecutorRegistry.cs](../../GameLib/Script/Common/Commands/VNext/Core/CommandExecutorRegistry.cs) - `IReadOnlyList<ICommandExecutor>` scan into command-id lookup table
- [CommandCatalogService.cs](../../GameLib/Script/Common/Commands/VNext/Catalog/CommandCatalogService.cs) - acquire-time catalog loading through a runtime service
- [CommandCatalogLocator.cs](../../GameLib/Script/Common/Commands/VNext/Catalog/CommandCatalogLocator.cs) - editor asset lookup, runtime `Resources.Load`, and fallback ScriptableObject creation
- [CommandCatalogSO.cs](../../GameLib/Script/Common/Commands/VNext/Catalog/CommandCatalogSO.cs) - stable-key catalog entries and fallback stable-key scan
- [CommandKeyResolver.cs](../../GameLib/Script/Common/Commands/VNext/Catalog/CommandKeyResolver.cs) - runtime-only negative key IDs and stable-key fallback behavior
- [CatalogCommandSource.cs](../../GameLib/Script/Common/Commands/VNext/Sources/CatalogCommandSource.cs) - command source resolution through stable key and runtime fallback options
- [CommandContext.cs](../../GameLib/Script/Common/Commands/VNext/Core/CommandContext.cs) - actor/scope slots, resolver exposure, and registry-based scope resolution

### Representative Anchors

`CommandRunnerMB` registers many executors as `ICommandExecutor` in Project scope.
The list includes control-flow commands, actor routing commands, transform commands, movement commands, UI commands, tooltip commands, mesh commands, animation commands, camera commands, scene commands, save commands, map commands, trait commands, collision commands, value commands, and debug commands.

`CommandRunnerMB` also registers different command runner contracts depending on `LifetimeScopeKind`.
Project, Platform, Global, Scene, Field, Entity, UI, and UIElement runner variants are installed by runtime switch rather than verified plan.

`CommandExecutorRegistry` builds executor lookup by scanning an `IReadOnlyList<ICommandExecutor>`.
This makes executor availability depend on DI collection behavior and boot registration completeness.

`CommandCatalogService` participates in acquire/release lifecycle and loads catalog state at runtime.
Target command catalog creation must come from verified artifact set input, not lifecycle-time locator behavior.

`CommandCatalogLocator` performs editor asset search and runtime `Resources.Load`.
When runtime load fails, it creates a fallback catalog instance.
Target runtime must not repair missing command catalog inputs this way.

`CommandKeyResolver` can allocate runtime-only negative key IDs when fallback is allowed.
Target runtime command dispatch must not require runtime-only authoring key repair.

`CatalogCommandSource` resolves commands through stable keys and falls back to stable-key catalog lookup when enabled.
Target runtime must use normalized CommandTypeId before command execution.

`CommandRunner` owns execution flow and diagnostics, but it also depends on registry, catalog, key resolver, scope resolver, VarStore, and lifecycle handler interfaces.
Target 09 separates runner execution from catalog generation, executor discovery, value schema ownership, runtime query lookup, and lifecycle enrollment.

### Current Gaps

Current command runtime leaves these gaps:

- command identity is split between numeric command IDs, stable keys, and authoring keys
- executor discovery depends on bulk DI registration
- payload schema is implicit in command data classes rather than verified runtime schema
- catalog lookup can fall back to stable-key scans
- missing catalog assets can be repaired by runtime fallback creation
- command runner domain is inferred from scope kind switch
- command execution context exposes broad resolver access
- actor, scope, channel, and UI target lookup can happen inside executors
- command-local state and persistent value state can blur through generic VarStore usage
- async command behavior can be implemented per executor without uniform cancellation and detached execution policy
- lifecycle participation can be attached to command runner services by interface registration
- command debug binding can be implemented through build callback rather than DebugMap projection

---

## CommandCatalog Runtime Definition

CommandCatalog is the runtime owner of command executor lookup metadata.

CommandCatalog owns:

- CommandTypeId lookup
- executor reference table
- payload schema reference table
- command module metadata
- command category metadata
- command diagnostics metadata
- executor factory or instance policy
- profile availability metadata

CommandCatalog does not own:

- service resolution
- value storage
- runtime query indexes
- lifecycle enrollment
- scope graph structure
- actor search
- command authoring UI
- command source normalization

CommandCatalog must not be implemented as `IReadOnlyList<ICommandExecutor>` resolution.

---

## CommandCatalogPlan Input Contract

CommandCatalog may be created only from a verified CommandCatalogPlan.

A valid CommandCatalogPlan must include:

- artifact header
- CommandCatalogPlanId
- CommandTypeId set
- payload schema references
- executor references
- owner module metadata
- category metadata
- dependency metadata
- profile availability metadata
- diagnostics metadata
- DebugMap linkage
- generator and format version

CommandCatalog must reject:

- unverified CommandCatalogPlan
- partial artifact set
- stale command catalog artifact
- mismatched KernelIR hash
- mismatched profile hash
- missing payload schema table
- missing executor table
- missing diagnostics table
- command identity not present in CommandIR

CommandCatalog must not accept runtime ad-hoc command registration in target kernel paths.

Development-only command extension is allowed only if a lower spec defines it as a bounded, profile-scoped, diagnostic-visible extension point.
Release profile must not accept dynamic command registration unless 13 explicitly defines a compatibility bridge.

---

## Command Identity Model

CommandTypeId is the runtime dispatch identity.

Command identity domains include:

- CommandTypeId
- CommandCategoryId
- CommandPayloadSchemaId
- CommandExecutorId
- CommandAuthoringKeyId

Explanatory sketch:

```csharp
public readonly struct CommandTypeId
{
    public readonly int Value;
}

public readonly struct CommandPayloadSchemaId
{
    public readonly int Value;
}

public readonly struct CommandExecutorId
{
    public readonly int Value;
}

public readonly struct CommandCategoryId
{
    public readonly int Value;
}

public readonly struct CommandAuthoringKeyId
{
    public readonly int Value;
}
```

This sketch is explanatory and does not finalize the serialized API.

Identity rules:

- CommandTypeId is used for runtime dispatch.
- CommandExecutorId identifies executor implementation or generated function reference.
- CommandPayloadSchemaId identifies payload schema required by a command type.
- CommandCategoryId is for grouping, filtering, editor display, diagnostics, and reports.
- ModuleId identifies the owning module.
- CommandAuthoringKeyId identifies normalized authoring key metadata when preserved.

CommandCategoryId is not dispatch identity.
CommandAuthoringKey is not dispatch identity.
Raw C# type name is not dispatch identity.
Raw string command name is not dispatch identity.

When authoring-key metadata is preserved in normalized IR, it should remain typed and provenance-aware.
A bare string field is insufficient because it allows runtime-facing code to accidentally treat authoring text as authority.
Preserved command authoring-key metadata must also be traceable in projection and DebugMap artifacts.

---

## AuthoringKey Boundary

Authoring keys exist for:

- editor authoring
- search
- migration
- debug output
- human-readable display
- authoring compatibility reports

Runtime dispatch must not use arbitrary strings.

Conversion from authoring key to CommandTypeId must happen before runtime execution during normalization, validation, or verified generation.

Forbidden:

- resolving executor by string at runtime
- dispatching by raw command name
- using authoring key as CommandTypeId
- using stable key as CommandTypeId
- fallback from missing CommandTypeId to authoring key lookup
- fallback from missing CommandTypeId to stable-key catalog scan
- runtime registry lookup by raw command name
- runtime-only command key allocation for target-kernel correctness

AuthoringKey may appear in diagnostics only after runtime identity is already known or as part of a failed normalization/migration diagnostic.

---

## Command Contribution Projection

CommandContribution is the declarative source for command runtime projection.

The projection path is:

```text
CommandContribution
  -> CommandIR
  -> CommandCatalogPlan
  -> CommandCatalog
```

CommandContribution may declare:

- command authoring key
- normalized command identity request
- payload schema reference or inline schema contribution
- executor reference
- owner module
- category metadata
- service dependencies
- value dependencies
- runtime query dependencies
- profile availability
- diagnostics source

Verified command catalog projection should preserve these declarations as structured `CommandEntryPlan` rows with grouped module and category metadata rather than as a flat runtime string table.

CommandContribution must not:

- instantiate command executors
- register command executors into ServiceGraph
- register `ICommandExecutor`
- add lifecycle handlers
- resolve executor identity from arbitrary strings
- create runtime catalog entries outside generation
- make authoring keys runtime truth

Generated CommandCatalogPlan must preserve provenance from CommandContribution and CommandIR.
If projection cannot prove command identity, payload schema, executor reference, and diagnostics provenance, generation fails.

---

## Command Payload Schema Model

Every command type must define a payload schema unless it explicitly declares EmptyPayload.

Payload schema must define:

- schema identity
- field identity
- field name
- value kind
- required or optional state
- default value policy
- allowed source kinds
- serialization policy
- validation policy
- profile availability
- diagnostics source

Explanatory sketch:

```csharp
public sealed class CommandPayloadSchemaPlan
{
    public CommandPayloadSchemaId SchemaId;
    public CommandTypeId CommandTypeId;
    public CommandPayloadFieldSchema[] Fields;
    public CommandPayloadUnknownFieldPolicy UnknownFieldPolicy;
    public SourceLocationId Source;
}

public sealed class CommandPayloadFieldSchema
{
    public CommandPayloadFieldId FieldId;
    public string Name;
    public ValueKind Kind;
    public bool Required;
    public CommandPayloadDefaultPolicy DefaultPolicy;
}

public enum CommandPayloadUnknownFieldPolicy
{
    Reject = 10,
    IgnoreWithWarning = 20,
    PreserveForMigration = 30,
}
```

This sketch is explanatory and does not finalize the serialized API.

Default unknown field policy is `Reject`.

Executor must not infer payload shape by runtime reflection over arbitrary serialized objects.
Reflection may be used by editor generation or migration tooling only if the generated schema is explicit before runtime.

---

## Command Payload Validation Policy

Payload validation must occur before executor execution.

Validation must check:

- command type exists
- payload schema exists
- payload schema matches CommandTypeId
- required fields exist
- field types match schema
- unknown fields obey policy
- default values are valid
- referenced services exist
- referenced ValueKeyId values exist
- referenced RuntimeQueryId values exist
- referenced scope/entity/actor target refs are valid
- payload source location is available for diagnostics

Runtime command execution may use prevalidated payloads.
If runtime receives a payload that is not marked schema-valid, the runner must validate it or reject it before executor invocation.

Payload validation failure must not be repaired by executor-side casts, default construction, or string-key fallback.

---

## Command Executor Model

Command executor is resolved by CommandExecutorId or CommandTypeId mapping in CommandCatalogPlan.

Explanatory model:

```csharp
public enum CommandExecutorKind
{
    GeneratedFunction = 10,
    StatelessSingleton = 20,
    LazySingleton = 30,
    PooledInstance = 40,
    LegacyAdapter = 90,
}
```

Allowed executor forms:

- generated static function
- stateless shared singleton
- lazy singleton
- pooled executor instance
- explicit legacy adapter during migration

Executor discovery through ServiceGraph is forbidden.

Executor may request services, values, runtime query handles, or scope handles through CommandExecutionContext only if those dependencies are declared by CommandContribution and validated by 04.

Executor must not:

- perform ad-hoc ServiceGraph searches for undeclared dependencies
- resolve runtime targets through ServiceGraph
- resolve values by stable string key
- perform scene-wide search
- infer command payload schema by reflection
- become a lifecycle handler by interface scan

---

## Executor Factory and Lifetime Policy

CommandCatalog may instantiate executors according to executor policy.

Executor factory policy must be explicit in CommandCatalogPlan.

Allowed policies:

- generated static function
- stateless shared singleton
- lazy singleton
- pooled executor instance
- legacy adapter during migration

CommandCatalog must not eagerly construct all executors during boot unless explicitly budgeted.

Increasing command count may increase catalog metadata size.
It must not force construction of every executor at boot.

Executor factory failure is a command diagnostics failure.
It must include CommandTypeId, CommandExecutorId, owner module, source location, selected profile, and suggested fix.

---

## CommandRunner Responsibility

CommandRunner owns command execution flow.

CommandRunner may:

- create CommandFrame
- validate payload
- resolve executor through CommandCatalog
- execute command
- execute command sequences
- handle control-flow commands
- manage cancellation
- apply command failure boundary
- record command diagnostics
- maintain command-local execution state

CommandRunner must not:

- discover executors
- register executors
- own executor catalog metadata
- perform runtime authoring-key lookup
- become ServiceGraph
- become RuntimeQuery
- become ValueStore
- become LifecycleDispatcher
- create dynamic command catalog entries

Runner domain must be explicit.

Explanatory model:

```csharp
public enum CommandExecutionDomain
{
    Kernel = 10,
    Project = 20,
    Scene = 30,
    Entity = 40,
    UI = 50,
    Test = 90,
}
```

Multiple command runners are allowed only when they represent different execution domains, not merely every scope kind.

Entity-domain runner is forbidden by default for mass entities.
An entity-domain runner exception is allowed only when:

- the entity represents a bounded authored aggregate root
- expected instance count is declared
- performance budget is declared
- lifecycle ownership is explicit
- diagnostics include source location and runtime handle
- CommandRunnerInstancePolicy is generated from verified plan

---

## CommandFrame and CommandContext Model

CommandFrame represents one execution frame.

CommandFrame may contain:

- CommandFrameId
- parent frame
- execution domain
- CommandTypeId
- CommandPayloadSchemaId
- actor reference
- target reference
- scope reference
- ValueStore access handle
- RuntimeQuery access handle
- cancellation token
- command-local state reference
- diagnostics context

CommandContext represents the stable execution environment visible to a command.

CommandContext may expose:

- current scope handle
- actor reference
- target reference
- command root reference
- command-local state
- declared service dependency access
- declared ValueStore access
- declared RuntimeQuery access
- selected profile
- diagnostics writer

CommandFrame must not be a loose dictionary of arbitrary objects without schema.
CommandContext must not expose unbounded runtime resolver access in target kernel paths.

---

## CommandLocal State Policy

CommandLocal stores execution-local state.

CommandLocal must be explicitly scoped to one of:

- command frame
- command sequence
- async wait boundary
- nested command block

CommandLocal must not become global Blackboard.

CommandLocal rules:

- temporary data belongs to CommandLocal
- persistent or scope-bound runtime state belongs to ValueStore
- target handles belong to structured context slots or RuntimeQuery results
- command-local state lifetime must be visible in diagnostics
- command-local keys must not collide with ValueKeyId identity

CommandLocal must not use arbitrary string keys as the only runtime identity unless a lower spec defines a bounded migration-only adapter.

---

## Control Flow Command Policy

Control-flow commands are part of the command execution model, not arbitrary executor side effects.

Control-flow command types must define:

- child command execution order
- child command validation requirements
- failure propagation
- cancellation behavior
- local context inheritance
- async wait behavior
- loop bounds
- diagnostics behavior
- source location for child references

Control-flow command examples include:

- sequence
- if
- switch
- for
- wait
- action block
- break
- detached execution

Loop commands must define a safety limit or explicit unbounded policy.

Detached execution commands must define detached execution policy.
They must not create fire-and-forget work implicitly.

Child command references must be validated before runtime execution.

---

## Async / Wait / Cancellation Policy

Command execution may be synchronous or asynchronous.

Async command must define:

- awaited completion behavior
- cancellation token source
- timeout policy
- failure policy
- whether child commands continue on cancellation
- whether command frame remains alive while waiting
- diagnostics on cancellation and timeout

Fire-and-forget command execution is forbidden unless command type explicitly declares detached execution policy.

Detached execution policy must define:

- owner frame
- owner scope
- cancellation source
- failure reporting destination
- diagnostics visibility
- shutdown behavior

Command cancellation must produce structured diagnostics when it affects command result.
Timeout must produce structured diagnostics when it affects command result.

---

## ServiceGraph Boundary

Command executor may use ServiceGraph only through declared dependencies.

CommandCatalog does not discover executors from ServiceGraph.
ServiceGraph does not collect `ICommandExecutor`.

Forbidden:

- `IReadOnlyList<ICommandExecutor>` resolution
- `.As<ICommandExecutor>()` bulk discovery
- executor identity from service contract scan
- command dependency repair through ServiceGraph fallback
- resolving runtime objects through ServiceGraph
- resolving command targets through ServiceGraph

If an executor requires a service, the dependency must be declared in CommandContribution, projected into CommandIR or CommandCatalogPlan, and validated by 04.

---

## ValueStore Boundary

Command may read or write ValueStore only through verified ValueKeyId and declared access policy.

Explanatory model:

```csharp
public enum CommandValueAccessKind
{
    None = 0,
    Read = 10,
    Write = 20,
    ReadWrite = 30,
}
```

Command must not resolve values by runtime stable key string.

Value access must define:

- ValueKeyId
- access kind
- owner module
- scope or domain boundary
- default value policy
- failure policy
- diagnostics source

Write commands must not infer value schema from current runtime value.
Value schema belongs to 10.

---

## RuntimeQuery Boundary

Command target lookup must use verified RuntimeQuery dependencies.

Executor must not perform:

- scene search
- hierarchy search
- raw component search
- ServiceGraph-based runtime object lookup
- arbitrary actor name lookup
- transform-parent target inference

Actor routing, player routing, channel routing, hit collider target routing, UI root routing, and camera target routing must be represented as declared RuntimeQuery dependencies or explicit context targets.

RuntimeQuery dependency must be validated before runtime execution.
Missing runtime query must fail command validation or command execution with structured diagnostics.

---

## Scope / Entity / Actor Target Boundary

Commands may target runtime objects through verified handles or query results.

Allowed target references:

- ScopeHandle
- EntityHandle
- PartHandle
- ActorRef
- RuntimeQueryResult
- CommandFrame target slot
- explicit generated target handle

Forbidden:

- raw Transform search
- raw GameObject.Find
- component ancestor scan
- arbitrary string actor lookup
- fallback to current scene object
- fallback to first matching target

Target absence policy must be explicit.
Required target absence must fail closed.
Optional target absence must follow an explicit command policy and emit diagnostics when required by profile.

---

## Lifecycle Boundary

CommandRunner may have lifecycle participation through LifecyclePlan.

Command executors are not lifecycle handlers by default.
A command execution frame is not a lifecycle scope.

Executing a command must not dynamically add lifecycle steps.
Registering a command executor must not enroll lifecycle participation.
Implementing a lifecycle-like interface must not enroll command executor lifecycle behavior.

If a command runner needs acquire, release, reset, or dispose behavior, that participation belongs to 08 as a LifecycleContribution and LifecyclePlan step.

---

## Command Module and Category Policy

Every command type must belong to one owner module.

Command category is used for:

- editor grouping
- diagnostics
- generation report
- optional module enable/disable
- profile filtering
- command catalog reports
- migration status reports

Example category families:

- CoreFlow
- ActorRouting
- Transform
- Movement
- Physics
- UI
- Tooltip
- Mesh
- AnimationSprite
- Camera
- SceneFlow
- Save
- Trait
- Map
- Debug

Category is not dispatch identity.

Command module ownership must be stable enough to support deletion, migration, diagnostics, and profile filtering.

---

## Diagnostics and DebugMap Requirements

Command diagnostics must include:

- stable error code
- severity
- CommandTypeId
- authoring key if available
- command debug name if available
- owner module
- command category
- payload schema id
- executor id
- execution frame id
- actor reference if available
- target reference if available
- source location
- failure policy
- selected profile
- suggested fix

Representative command diagnostic codes:

- COMMAND_TYPE_MISSING
- COMMAND_EXECUTOR_MISSING
- COMMAND_PAYLOAD_SCHEMA_MISSING
- COMMAND_PAYLOAD_REQUIRED_FIELD_MISSING
- COMMAND_PAYLOAD_TYPE_MISMATCH
- COMMAND_PAYLOAD_UNKNOWN_FIELD
- COMMAND_AUTHORING_KEY_USED_AS_RUNTIME_ID
- COMMAND_EXECUTOR_FACTORY_FAILED
- COMMAND_SERVICE_DEPENDENCY_UNDECLARED
- COMMAND_RUNTIME_QUERY_MISSING
- COMMAND_VALUE_KEY_MISSING
- COMMAND_VALUE_STABLE_KEY_LOOKUP_FORBIDDEN
- COMMAND_ASYNC_UNTRACKED
- COMMAND_DETACHED_POLICY_MISSING
- COMMAND_CANCELLED
- COMMAND_TIMEOUT
- COMMAND_CONTROL_FLOW_INVALID
- COMMAND_LOOP_BOUND_MISSING
- COMMAND_RUNNER_CARDINALITY_FORBIDDEN
- COMMAND_BULK_DI_DISCOVERY_FORBIDDEN

A command error without source location is itself a diagnostics degradation unless the missing source location is the reported failure.

---

## Failure Policy

Command failure must not be swallowed.

Each command type must define default failure behavior.

Explanatory model:

```csharp
public enum CommandFailureBoundary
{
    FailCommand = 10,
    FailFrame = 20,
    FailSequence = 30,
    FailRunner = 40,
    FailScope = 50,
    ContinueWithError = 60,
}
```

Default failure behavior is fail-closed.
`ContinueWithError` is forbidden by default and allowed only when the command type, profile policy, and diagnostics policy explicitly allow it.

Sequence-like commands must define whether child failure:

- stops the sequence
- rolls back
- skips the child
- continues with error
- propagates to parent frame
- fails the runner
- fails the scope

Executor exceptions must be converted into structured command diagnostics.
Exceptions must not be used as normal control flow.

---

## Performance and Memory Policy

Command dispatch is a runtime hot path.

Target requirements:

- CommandTypeId lookup should be O(1) or bounded small constant
- executor lookup must not scan all executors
- normal dispatch should avoid managed allocation
- payload validation should be precomputed where possible
- ServiceGraph must not construct all executors at boot
- command frame allocation should be pooled or bounded for high-frequency paths
- command-local state must not allocate dictionaries unless explicitly required
- authoring-key lookup must not occur during normal runtime dispatch
- catalog metadata size must be measurable
- command execution must expose profiler markers defined by 14

Increasing command type count may increase catalog metadata size.
It must not force eager construction of every executor at boot.

Increasing entity count must not automatically increase command runner count.
Increasing command count must not automatically increase lifecycle step count.

---

## Legacy Migration Policy

Legacy command migration must replace runtime discovery with explicit command contribution and generated catalog metadata.

| Legacy Pattern | Target Representation |
|---|---|
| `builder.Register<XExecutor>().As<ICommandExecutor>()` | CommandContribution + CommandCatalogPlan entry |
| `ICommandExecutor` bulk list | CommandTypeId to executor mapping |
| `CommandExecutorRegistry(IReadOnlyList<ICommandExecutor>)` | Generated executor table |
| `CommandKeyResolver` runtime lookup | authoring key normalized to CommandTypeId before runtime |
| runtime-only negative command key ID | invalid for target-kernel correctness |
| `CommandCatalogService` runtime catalog builder | Verified CommandCatalogPlan |
| `CommandCatalogLocator` `Resources.Load` fallback | boot-time verified artifact reference |
| scope-kind switch for runner registration | CommandExecutionDomain + explicit runner policy |
| `.As<IScopeAcquireHandler>()` on CommandRunner | LifecycleContribution targeting CommandRunner |
| debug viewer build callback | DiagnosticsContribution / DebugMap binding |

Legacy migration does not imply runtime fallback.

Legacy adapters are allowed only inside the compatibility boundary defined by 13.
New CommandCatalog core must not depend on legacy RuntimeResolver, legacy CommandRunnerMB registration, legacy catalog locator fallback, or legacy command key runtime fallback.

---

## Forbidden Patterns

The following are forbidden in target CommandCatalog runtime:

- bulk DI registration as command discovery
- resolving `IReadOnlyList<ICommandExecutor>`
- executor lookup by arbitrary string
- authoring key used as runtime dispatch identity
- stable key used as runtime dispatch identity
- missing command fallback to no-op executor
- missing payload schema fallback
- runtime reflection over payload object as schema
- runtime scene search inside executor
- runtime Transform hierarchy search inside executor
- ServiceGraph used as runtime target registry
- CommandCatalog used as lifecycle registry
- command executor lifecycle enrollment by interface scan
- eager construction of all executors at boot by default
- command frame as arbitrary object dictionary
- fire-and-forget execution without explicit detached policy
- swallowing command failure
- command-local state used as global Blackboard
- runtime-only command key repair
- `Resources.Load` fallback for required command catalog input
- editor asset search as target runtime catalog source
- giant runtime installer edited for every new command

---

## Test Case Model

Each CommandCatalog test case must define:

- Test ID
- Title
- CommandCatalogPlan fixture
- command payload fixture
- CommandExecutionContext fixture
- selected profile
- Operation
- Expected result
- Expected diagnostics
- Expected allocation or performance assertion if applicable
- Notes

Example:

```text
Test ID: TC_CMD_ID_001_DispatchByCommandTypeId
Input:
- CommandCatalogPlan contains CommandTypeId CameraShake
- Executor mapping exists

Operation:
- Dispatch CameraShake by CommandTypeId

Expected:
- Correct executor invoked
- No authoring-key lookup occurs
```

---

## Required Test Cases

### A. Identity Tests

#### TC_CMD_ID_001_DispatchByCommandTypeId

Input:

- CommandCatalogPlan has CommandTypeId CameraShake
- Executor mapping exists

Operation:

- Dispatch CameraShake by CommandTypeId

Expected:

- Correct executor invoked

#### TC_CMD_ID_002_AuthoringKeyRuntimeDispatchRejected

Input:

- Dispatch request uses raw string `camera.shake`

Expected:

- Failed
- COMMAND_AUTHORING_KEY_USED_AS_RUNTIME_ID

#### TC_CMD_ID_003_UnknownCommandTypeRejected

Input:

- CommandTypeId 9999 is not in catalog

Expected:

- Failed
- COMMAND_TYPE_MISSING

### B. Payload Tests

#### TC_CMD_PAYLOAD_001_ValidPayloadAccepted

Input:

- Command requires float duration
- Payload contains float duration

Expected:

- Passed

#### TC_CMD_PAYLOAD_002_RequiredFieldMissing

Input:

- Command requires target
- Payload missing target

Expected:

- Failed
- COMMAND_PAYLOAD_REQUIRED_FIELD_MISSING

#### TC_CMD_PAYLOAD_003_TypeMismatchRejected

Input:

- Field duration schema is float
- Payload duration is string

Expected:

- Failed
- COMMAND_PAYLOAD_TYPE_MISMATCH

#### TC_CMD_PAYLOAD_004_UnknownFieldRejectedByDefault

Input:

- Payload contains unknown field

Expected:

- Failed
- COMMAND_PAYLOAD_UNKNOWN_FIELD

### C. Executor Tests

#### TC_CMD_EXEC_001_LazyExecutorCreatedOnFirstUse

Input:

- Executor policy is LazySingleton

Operation:

- Boot catalog
- Do not execute command

Expected:

- Executor is not constructed at boot

Operation:

- Execute command

Expected:

- Executor is constructed once

#### TC_CMD_EXEC_002_AllExecutorsNotEagerlyConstructed

Input:

- Catalog has 500 command types

Operation:

- Boot

Expected:

- No eager construction of all executors

#### TC_CMD_EXEC_003_MissingExecutorRejected

Input:

- CommandTypeId exists
- Executor reference missing

Expected:

- Failed
- COMMAND_EXECUTOR_MISSING

### D. Service / Value / Query Boundary Tests

#### TC_CMD_BOUNDARY_001_ExecutorServiceDependencyDeclared

Input:

- Executor requires TimeDomainService
- Dependency declared and service exists

Expected:

- Passed

#### TC_CMD_BOUNDARY_002_UndeclaredServiceDependencyRejected

Input:

- Executor attempts to access service not declared in CommandContribution

Expected:

- Failed
- COMMAND_SERVICE_DEPENDENCY_UNDECLARED

#### TC_CMD_BOUNDARY_003_ValueStableKeyLookupRejected

Input:

- Command tries to access value by stable string key at runtime

Expected:

- Failed
- COMMAND_VALUE_STABLE_KEY_LOOKUP_FORBIDDEN

#### TC_CMD_BOUNDARY_004_RuntimeQueryMissingRejected

Input:

- WithActor command requires actor query
- RuntimeQuery not declared

Expected:

- Failed
- COMMAND_RUNTIME_QUERY_MISSING

### E. Control Flow Tests

#### TC_CMD_FLOW_001_SequenceStopsOnFailureByPolicy

Input:

- Sequence has 3 commands
- Command 2 fails
- Policy is StopOnFailure

Expected:

- Command 3 not executed
- Failure propagated

#### TC_CMD_FLOW_002_IfBranchUsesDeclaredChildCommands

Input:

- If command references then and else blocks

Expected:

- Only selected branch executes
- child command IDs are valid

#### TC_CMD_FLOW_003_ForLoopRequiresBound

Input:

- For command has no max iteration and no explicit unbounded policy

Expected:

- Failed
- COMMAND_LOOP_BOUND_MISSING

### F. Async Tests

#### TC_CMD_ASYNC_001_WaitCommandCompletes

Input:

- Wait command with duration

Expected:

- Frame remains alive
- Completion resumes sequence

#### TC_CMD_ASYNC_002_CancellationStopsFrame

Input:

- Running async command
- Cancellation requested

Expected:

- COMMAND_CANCELLED
- frame failure policy applied

#### TC_CMD_ASYNC_003_FireAndForgetRequiresDetachedPolicy

Input:

- Forget command without detached policy

Expected:

- Failed
- COMMAND_DETACHED_POLICY_MISSING

### G. Runner Domain Tests

#### TC_CMD_RUNNER_001_ProjectRunnerUsesProjectDomain

Input:

- Project domain runner
- Project command execution

Expected:

- Passed

#### TC_CMD_RUNNER_002_EntityRunnerRejectedByDefaultForMassEntities

Input:

- Runner per entity for 10,000 entities

Expected:

- Failed
- COMMAND_RUNNER_CARDINALITY_FORBIDDEN

#### TC_CMD_RUNNER_003_ExplicitEntityAggregateRunnerAllowed

Input:

- Authored boss aggregate root has explicit runner policy

Expected:

- Passed or Warning depending budget

### H. Migration Tests

#### TC_CMD_MIGRATION_001_CommandRunnerMBBulkExecutorRegistrationRejected

Input:

- Module attempts to register executors through builder `.As<ICommandExecutor>()`

Expected:

- Failed
- COMMAND_BULK_DI_DISCOVERY_FORBIDDEN

#### TC_CMD_MIGRATION_002_CommandRunnerLifecycleSeparated

Input:

- CommandRunner requires acquire/release

Expected:

- LifecycleContribution created
- ServiceGraph does not infer lifecycle from handler interface

#### TC_CMD_MIGRATION_003_DebugViewerBuildCallbackReplaced

Input:

- Build callback binds command debug viewer

Expected:

- DiagnosticsContribution or DebugMap binding required
- runtime build callback not allowed as source of truth

### I. Performance Tests

#### TC_CMD_PERF_001_CommandLookupNoScan

Input:

- 1000 command types

Operation:

- Dispatch one command

Expected:

- Does not scan all command types

#### TC_CMD_PERF_002_NormalDispatchNoAllocation

Operation:

- Dispatch simple command repeatedly

Expected:

- No managed allocation in normal path

#### TC_CMD_PERF_003_CommandCountDoesNotEagerInstantiateExecutors

Input:

- Many command types

Operation:

- Boot catalog

Expected:

- Metadata loaded
- executors not all instantiated

---

## Acceptance Criteria

This specification is complete when it defines:

- CommandCatalog runtime responsibility
- CommandCatalogPlan input contract
- CommandTypeId identity model
- authoring key boundary
- CommandContribution projection boundary
- payload schema model
- payload validation policy
- command executor model
- executor factory and lifetime policy
- CommandRunner responsibility
- CommandFrame and CommandContext model
- CommandLocal state policy
- control-flow command policy
- async, wait, cancellation, and detached execution policy
- ServiceGraph boundary
- ValueStore boundary
- RuntimeQuery boundary
- scope, entity, and actor target boundary
- LifecyclePlan boundary
- command module and category policy
- diagnostics and DebugMap requirements
- failure policy
- performance and memory policy
- legacy migration policy
- forbidden patterns
- CommandCatalog test case model
- required CommandCatalog test cases

---

## Test Cases

| Test Case | Purpose | Expected Result |
|---|---|---|
| TC-09-01 | Verify command dispatch is by CommandTypeId, not authoring string. | Raw string dispatch is rejected and CommandTypeId dispatch succeeds. |
| TC-09-02 | Verify executors are not discovered through ServiceGraph or `IReadOnlyList<ICommandExecutor>`. | Bulk DI discovery fails validation. |
| TC-09-03 | Verify payload schema validation runs before executor invocation. | Missing, unknown, or type-mismatched fields fail before execution. |
| TC-09-04 | Verify command boundaries with ServiceGraph, ValueStore, RuntimeQuery, ScopeGraph, and LifecyclePlan. | Undeclared dependencies and target searches are rejected. |
| TC-09-05 | Verify async, wait, cancellation, and detached execution policy. | Untracked async and missing detached policy fail with diagnostics. |
| TC-09-06 | Verify performance and boot behavior. | Catalog boot does not construct every executor and dispatch does not scan all commands. |

---

## Final Position

CommandCatalog is the runtime command table for verified command identities, payload schemas, executor references, and diagnostics metadata.
It is not a DI executor list, not a string-key resolver, and not a lifecycle registry.

Runtime command execution may proceed only from verified CommandTypeId and verified payload schema.

```text
CommandContribution
  -> CommandIR
  -> CommandCatalogPlan
  -> CommandCatalog
  -> CommandTypeId lookup
  -> executor execution
```

The era of adding commands by editing a giant runtime installer must end.

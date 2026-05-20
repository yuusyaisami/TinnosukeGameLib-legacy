# Wave C Command Dispatch Cutover Specification

## Document Status

- Document ID: 03_WaveCCommandDispatchCutoverSpec
- Status: Draft
- Role: defines the Wave C migration contract that cuts over command registration, command identity truth, payload schema authority, executor lookup, and runner dispatch from legacy bulk registration and runtime key fallback to verified CommandCatalog authority while preserving representative authored command surfaces
- Depends on:
  - [00_KernelV21MigrationOverviewSpec.md](00_KernelV21MigrationOverviewSpec.md)
  - [01_WaveABootAndSceneEntryCutoverSpec.md](01_WaveABootAndSceneEntryCutoverSpec.md)
  - [02_WaveBScopeAndServiceCompositionCutoverSpec.md](02_WaveBScopeAndServiceCompositionCutoverSpec.md)
  - [../v2/01_KernelIRSpec.md](../v2/01_KernelIRSpec.md)
  - [../v2/04_DependencyValidationSpec.md](../v2/04_DependencyValidationSpec.md)
  - [../v2/05_BootManifestAndProfileSpec.md](../v2/05_BootManifestAndProfileSpec.md)
  - [../v2/06_ServiceGraphRuntimeSpec.md](../v2/06_ServiceGraphRuntimeSpec.md)
  - [../v2/07_ScopeGraphRuntimeSpec.md](../v2/07_ScopeGraphRuntimeSpec.md)
  - [../v2/09_CommandCatalogRuntimeSpec.md](../v2/09_CommandCatalogRuntimeSpec.md)
  - [../v2/10_ValueSchemaAndStoreSpec.md](../v2/10_ValueSchemaAndStoreSpec.md)
  - [../v2/11_DebugMapAndDiagnosticsSpec.md](../v2/11_DebugMapAndDiagnosticsSpec.md)
  - [../v2/13_LegacyCompatBoundarySpec.md](../v2/13_LegacyCompatBoundarySpec.md)
  - [../v2/15_TestAndValidationSpec.md](../v2/15_TestAndValidationSpec.md)
  - [../v2/16_ImplementationMilestoneOrderSpec.md](../v2/16_ImplementationMilestoneOrderSpec.md)
  - [../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md](../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md)
- Provides foundation for:
  - [04_WaveDValueBlackboardAndVarCutoverSpec.md](04_WaveDValueBlackboardAndVarCutoverSpec.md)
  - [05_WaveERepresentativeGameplaySystemsCutoverSpec.md](05_WaveERepresentativeGameplaySystemsCutoverSpec.md)
  - [06_WaveFLegacyRemovalAndHardeningSpec.md](06_WaveFLegacyRemovalAndHardeningSpec.md)

### Revision Note

This revision creates the third detailed v2.1 wave specification.

Wave B moves runtime scope and coarse service composition toward verified ScopeGraph and ServiceGraph boundaries.
Wave C exists because that cutover does not, by itself, replace the legacy command authority that still lives in `CommandRunnerMB`, `CommandExecutorCatalog(IReadOnlyList<ICommandExecutor>)`, `CommandCatalogService`, `CommandCatalogLocator`, `CommandKeyResolver`, `CatalogCommandSource`, and scope-kind runner registration.

The purpose of this wave is not to redesign the preserved authored command payload surfaces.
Its purpose is to move runtime command truth to the verified command authority defined by v2 while keeping representative existing command content functional through explicit normalization and compatibility boundaries.

---

## Ownership

This specification owns:

- the current-state inventory for command registration, command identity, catalog truth, executor lookup, and runner execution authority
- the preserved command-facing contracts that must survive the cutover without freezing legacy dispatch architecture
- the target authority model for CommandTypeId, CommandCatalog, executor mapping, runner responsibility, and payload validation in the migration slice
- the quarantine rules for runtime key fallback, stable-key fallback, and catalog-loading convenience paths
- the subphase structure, diagnostics, and acceptance criteria for Wave C

This specification does not own:

- target command semantics already owned by [../v2/09_CommandCatalogRuntimeSpec.md](../v2/09_CommandCatalogRuntimeSpec.md)
- scope and service composition semantics already cut over by Wave B
- value-store, blackboard, and var identity semantics beyond the continuity boundaries required to keep representative commands functioning
- the full gameplay-feature migration of every command-consuming system
- the final deletion of every legacy command adapter or compatibility path

Wave C owns command dispatch authority.
It must not become a substitute for Wave D, Wave E, or Wave F.

---

## Purpose

Wave C defines how the running game stops treating bulk executor registration, lifecycle-time catalog loading, and runtime key repair as command truth.

Core statements:

```text
Verified CommandCatalog authority owns runtime command dispatch.
CommandTypeId is the accepted runtime dispatch identity.

Authoring keys, stable keys, runtime-only fallback keys, and bulk DI executor registration must stop being accepted command truth.

Existing authored command payload shapes and representative high-level execution meaning may remain stable while dispatch authority changes underneath them.
```

This wave therefore focuses on authority, identity, and execution flow, not on authoring-shape churn.

---

## Scope

Wave C defines:

- command identity boundary cutover from legacy compatibility inputs to verified runtime identities
- command catalog authority cutover from lifecycle-time service loading and asset-convention lookup to verified runtime artifact input
- executor lookup cutover from bulk `ICommandExecutor` discovery to explicit verified executor mapping
- runner execution responsibility, payload validation boundary, and nested command execution continuity for representative commands
- quarantine rules for runtime key fallback, stable-key fallback, and mixed command authority
- diagnostics and acceptance gates for the above

---

## Non-Goals

Wave C does not define:

- a rewrite of every existing command payload class or inspector surface
- full redesign of value, blackboard, or var semantics
- a blanket rewrite of every feature executor internal implementation
- full gameplay-system migration beyond the representative command surfaces needed to keep the migration slice honest
- final deletion of every legacy command helper from the repository

Wave C may preserve representative authored command surfaces.
It does not preserve legacy command registration or fallback architecture.

---

## Relationship to Other Specs

| Spec | Relationship to Wave C |
| --- | --- |
| [00_KernelV21MigrationOverviewSpec.md](00_KernelV21MigrationOverviewSpec.md) | Defines the preservation floor, destructive allowance, and wave partitioning that Wave C must obey. |
| [01_WaveABootAndSceneEntryCutoverSpec.md](01_WaveABootAndSceneEntryCutoverSpec.md) | Preserves scene-transition command continuity that Wave C must keep valid while changing dispatch authority. |
| [02_WaveBScopeAndServiceCompositionCutoverSpec.md](02_WaveBScopeAndServiceCompositionCutoverSpec.md) | Hands off verified scope and coarse service boundaries that Wave C must consume rather than rediscover. |
| [../v2/01_KernelIRSpec.md](../v2/01_KernelIRSpec.md) | Owns `CommandTypeId`, `CommandExecutorId`, `CommandPayloadSchemaId`, and `CommandAuthoringKeyId` typed identity semantics consumed by Wave C. |
| [../v2/04_DependencyValidationSpec.md](../v2/04_DependencyValidationSpec.md) | Owns validation rules for command identity, executor references, payload schema references, and forbidden runtime repair behavior. |
| [../v2/05_BootManifestAndProfileSpec.md](../v2/05_BootManifestAndProfileSpec.md) | Owns how verified artifact sets reach runtime. Wave C must receive command authority through that chain rather than ad-hoc loading. |
| [../v2/06_ServiceGraphRuntimeSpec.md](../v2/06_ServiceGraphRuntimeSpec.md) | Owns service-runtime semantics. Wave C may consume declared services but must not rediscover executors through ServiceGraph. |
| [../v2/07_ScopeGraphRuntimeSpec.md](../v2/07_ScopeGraphRuntimeSpec.md) | Owns explicit scope truth. Wave C must not restore hierarchy-derived scope truth while preserving representative actor-routing behavior. |
| [../v2/09_CommandCatalogRuntimeSpec.md](../v2/09_CommandCatalogRuntimeSpec.md) | Owns the target command runtime meaning that Wave C is cutting over to. |
| [../v2/10_ValueSchemaAndStoreSpec.md](../v2/10_ValueSchemaAndStoreSpec.md) | Owns value semantics. Wave C only defines the continuity boundary needed for function vars, command-local flow, and later Wave D handoff. |
| [../v2/11_DebugMapAndDiagnosticsSpec.md](../v2/11_DebugMapAndDiagnosticsSpec.md) | Owns the diagnostics substrate. Wave C defines the migration-visible failures that must become diagnostic-visible. |
| [../v2/13_LegacyCompatBoundarySpec.md](../v2/13_LegacyCompatBoundarySpec.md) | Owns quarantine rules for legacy command adapters, stable-key shims, and demoted bootstrap helpers. |
| [../v2/15_TestAndValidationSpec.md](../v2/15_TestAndValidationSpec.md) | Owns executable proof expectations for the test cases defined here. |
| [../v2/16_ImplementationMilestoneOrderSpec.md](../v2/16_ImplementationMilestoneOrderSpec.md) | Confirms that Wave C begins from the post-M15 baseline rather than redefining kernel foundations. |
| [../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md](../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md) | Prevents Wave C migration code from collapsing kernel command core, feature executors, and legacy bootstrap into a single runtime blob. |

Wave C consumes v2 semantics.
It does not soften them for migration convenience.

---

## Current-State Command Authority Inventory

This section records the current runtime command authority.
It is migration evidence, not target policy.

### Observation Traceability

| Observation | Evidence Type | Migration Pressure |
| --- | --- | --- |
| `CommandRunnerMB` registers many executors as `ICommandExecutor`, binds `CommandExecutorCatalog`, and installs different runner contracts by `LifetimeScopeKind`. | Source | command availability and runner domain still depend on builder mutation, collection binding, and owner-kind switches rather than verified command plans |
| `CommandExecutorCatalog` builds a `Dictionary<int, ICommandExecutor>` from `IReadOnlyList<ICommandExecutor>` and throws on duplicate or invalid `CommandId`. | Source | executor lookup truth still originates from bulk collection discovery rather than plan-projected executor metadata |
| `CommandRunner` depends on `ICommandExecutorCatalog`, `ICommandCatalog`, and `ICommandKeyResolver`, and `ExecuteListAsync()` builds a `CommandResolveContext` that still carries `AllowRuntimeKeyFallback`. | Source | runner execution flow still mixes payload execution with catalog lookup, key normalization, and runtime fallback policy |
| `CommandCatalogService` loads `CommandCatalogSO` in `OnAcquire()` through `CommandCatalogLocator.GetOrCreate()` and clears it in `OnRelease()`. | Source | command catalog truth still arrives as lifecycle-time service state rather than verified artifact input |
| `CommandCatalogLocator` searches `AssetDatabase` and creates a catalog asset in the editor, while the runtime path uses `Resources.Load("CommandCatalog")`. | Source | accepted command authority still depends on asset-convention loading rather than verified artifact delivery |
| `CommandCatalogSO` builds command lookup from registered key IDs, builds payload schema tables keyed by `commandId`, and keeps editor-only stable-key scan fallbacks. | Source | command identity, payload schema lookup, and editor convenience fallback are still mixed inside a compatibility asset surface |
| `CommandKeyResolver` resolves stable keys through the registry and can allocate runtime-only negative key IDs when fallback is allowed. | Source | runtime repair can still create transient command identities instead of failing closed |
| `CatalogCommandSource` resolves stable keys through the resolver and may fall back to `ICommandCatalog.TryResolve(CommandKeyRef)` when runtime fallback is enabled. | Source | runtime dispatch may still enter through stable-key compatibility behavior instead of normalized runtime identity |
| Representative executors such as `FunctionExecutor`, `CommandChannelExecutor`, and control-flow or lifecycle executors recursively call `ExecuteListAsync()` on nested command lists. | Source | Wave C must preserve nested execution, break, cancel, detached, and reacquire semantics while changing dispatch authority |

### Representative Anchors

- [../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs)
- [../../GameLib/Script/Common/Commands/VNext/Core/CommandRunner.cs](../../GameLib/Script/Common/Commands/VNext/Core/CommandRunner.cs)
- [../../GameLib/Script/Common/Commands/VNext/Core/ICommandRunner.cs](../../GameLib/Script/Common/Commands/VNext/Core/ICommandRunner.cs)
- [../../GameLib/Script/Common/Commands/VNext/Core/CommandExecutorRegistry.cs](../../GameLib/Script/Common/Commands/VNext/Core/CommandExecutorRegistry.cs)
- [../../GameLib/Script/Common/Commands/VNext/Core/ICommandExecutor.cs](../../GameLib/Script/Common/Commands/VNext/Core/ICommandExecutor.cs)
- [../../GameLib/Script/Common/Commands/VNext/Catalog/CommandCatalogService.cs](../../GameLib/Script/Common/Commands/VNext/Catalog/CommandCatalogService.cs)
- [../../GameLib/Script/Common/Commands/VNext/Catalog/CommandCatalogLocator.cs](../../GameLib/Script/Common/Commands/VNext/Catalog/CommandCatalogLocator.cs)
- [../../GameLib/Script/Common/Commands/VNext/Catalog/CommandCatalogSO.cs](../../GameLib/Script/Common/Commands/VNext/Catalog/CommandCatalogSO.cs)
- [../../GameLib/Script/Common/Commands/VNext/Catalog/CommandKeyResolver.cs](../../GameLib/Script/Common/Commands/VNext/Catalog/CommandKeyResolver.cs)
- [../../GameLib/Script/Common/Commands/VNext/Sources/CatalogCommandSource.cs](../../GameLib/Script/Common/Commands/VNext/Sources/CatalogCommandSource.cs)
- [../../GameLib/Script/Common/Commands/VNext/Commands/Scene/SceneChangeCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/Scene/SceneChangeCommandData.cs)
- [../../GameLib/Script/Common/Commands/VNext/Executors/Scene/SceneChangeExecutor.cs](../../GameLib/Script/Common/Commands/VNext/Executors/Scene/SceneChangeExecutor.cs)
- [../../GameLib/Script/Common/Commands/VNext/Commands/Core/WithActorCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/Core/WithActorCommandData.cs)
- [../../GameLib/Script/Common/Commands/VNext/Commands/Core/FunctionCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/Core/FunctionCommandData.cs)
- [../../GameLib/Script/Common/Commands/VNext/Executors/Core/FunctionExecutor.cs](../../GameLib/Script/Common/Commands/VNext/Executors/Core/FunctionExecutor.cs)
- [../../GameLib/Script/Common/Commands/VNext/Commands/Core/CommandChannelCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/Core/CommandChannelCommandData.cs)
- [../../GameLib/Script/Common/Commands/VNext/Executors/Core/CommandChannelExecutor.cs](../../GameLib/Script/Common/Commands/VNext/Executors/Core/CommandChannelExecutor.cs)
- [../../GameLib/Script/Common/Commands/VNext/Commands/Control/ControlCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/Control/ControlCommandData.cs)
- [../../GameLib/Script/Common/Commands/VNext/Commands/Core/LifecycleCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/Core/LifecycleCommandData.cs)

---

## Preserved Command Contracts

Wave C preserves only narrow command-facing continuity.
It does not preserve legacy command bootstrap or runtime repair behavior.

| Contract Surface | Current Anchor | Wave C Requirement |
| --- | --- | --- |
| Existing authored command field shapes and compatibility-facing `CommandId` surfaces | [../../GameLib/Script/Common/Commands/VNext/Commands/Core/FunctionCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/Core/FunctionCommandData.cs), [../../GameLib/Script/Common/Commands/VNext/Commands/Core/WithActorCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/Core/WithActorCommandData.cs), [../../GameLib/Script/Common/Commands/VNext/Commands/Core/CommandChannelCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/Core/CommandChannelCommandData.cs), [../../GameLib/Script/Common/Commands/VNext/Commands/Control/ControlCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/Control/ControlCommandData.cs) | Existing payload fields, nested `CommandListData` layout, and compatibility-facing `CommandId` surfaces must remain consumable by existing content, but accepted runtime dispatch must normalize them to verified command authority before executor resolution. |
| `SceneChangeCommandData` and `SceneChangeExecutor` high-level scene-transition contract | [../../GameLib/Script/Common/Commands/VNext/Commands/Scene/SceneChangeCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/Scene/SceneChangeCommandData.cs), [../../GameLib/Script/Common/Commands/VNext/Executors/Scene/SceneChangeExecutor.cs](../../GameLib/Script/Common/Commands/VNext/Executors/Scene/SceneChangeExecutor.cs) | Existing scene-transition authoring must continue to produce equivalent high-level load, additive-load, and unload outcomes through an `ISceneService`-compatible migrated authority. |
| `WithActorCommandData` actor-routing and nested-body contract | [../../GameLib/Script/Common/Commands/VNext/Commands/Core/WithActorCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/Core/WithActorCommandData.cs) | `ActorSource`, `AwaitMode`, `ExecutionScope`, `StoreActorToContext`, `ActorContextSlot`, and `Body` semantics must remain meaningful to existing content while dispatch authority changes beneath them. |
| `FunctionCommandData` nested execution and var-diff propagation contract | [../../GameLib/Script/Common/Commands/VNext/Commands/Core/FunctionCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/Core/FunctionCommandData.cs), [../../GameLib/Script/Common/Commands/VNext/Executors/Core/FunctionExecutor.cs](../../GameLib/Script/Common/Commands/VNext/Executors/Core/FunctionExecutor.cs) | Existing function command content must continue to support nested command execution, initial-var injection, and output propagation back to caller vars without reauthoring. |
| `CommandChannelCommandData` tag, await, and background-cancel contract | [../../GameLib/Script/Common/Commands/VNext/Commands/Core/CommandChannelCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/Core/CommandChannelCommandData.cs), [../../GameLib/Script/Common/Commands/VNext/Executors/Core/CommandChannelExecutor.cs](../../GameLib/Script/Common/Commands/VNext/Executors/Core/CommandChannelExecutor.cs) | `Tag`, owner-selection, execution-actor selection, await mode, and background-cancel behavior must remain stable from the content point of view. |
| Control-flow child-list contract | [../../GameLib/Script/Common/Commands/VNext/Commands/Control/ControlCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/Control/ControlCommandData.cs) | Representative control-flow commands such as `Wait`, `Break`, `Cancel`, and `If` must preserve their high-level child-list and flow-control meaning. |
| `SelfDespawnCommandData` lifecycle-wrapper contract | [../../GameLib/Script/Common/Commands/VNext/Commands/Core/LifecycleCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/Core/LifecycleCommandData.cs) | `DelaySeconds`, `BeforeDespawnCommands`, and `OnReacquireCommands` must remain valid authored content surfaces while their execution route moves to the migrated command authority. |

Preserved does not mean frozen implementation.
It means stable command-facing contract.

---

## Owned Migration Goals

Wave C must achieve all of the following:

- move accepted runtime command identity truth to verified `CommandTypeId` dispatch
- move accepted command catalog truth to verified runtime artifact input rather than lifecycle-time service loading or asset-convention lookup
- eliminate bulk `ICommandExecutor` discovery and `IReadOnlyList<ICommandExecutor>` scan as accepted executor authority
- eliminate runtime key repair, stable-key dispatch fallback, and runtime-only negative-key allocation from the accepted path
- preserve representative authored command payload surfaces and their high-level execution meaning through explicit normalization or compatibility adapters
- keep payload validation ahead of executor invocation in the migrated path
- separate runner execution flow from catalog discovery, executor discovery, and mixed value or service ownership
- make mixed legacy and verified command authority visible, bounded, and unacceptable as the steady state

---

## Target Authority Model

Wave C target authority is defined by the following required model.

### Required Command Identity Boundary

1. Existing authored command payload surfaces remain valid migration-facing inputs.
2. Accepted runtime dispatch normalizes those inputs to verified `CommandTypeId` before executor resolution.
3. `CommandExecutorId` identifies the executor implementation or generated function binding selected for that command type.
4. `CommandPayloadSchemaId` identifies the payload schema that must validate before executor invocation.
5. `CommandAuthoringKeyId`, stable keys, and other authoring metadata remain authoring, migration, and diagnostics surfaces rather than runtime dispatch truth.
6. Missing or ambiguous normalization fails before execution rather than repairing itself through runtime fallback.

Wave C therefore requires the v2 rules that:

- `CommandTypeId` is the accepted runtime dispatch identity
- existing compatibility-facing integer `CommandId` surfaces may remain, but they must not be the only accepted runtime truth
- runtime dispatch must not use arbitrary strings, stable keys, or runtime-only negative keys as accepted identity
- payload schema identity and executor identity must be explicit verified data rather than inferred from implementation shape

### Required CommandCatalog Authority Chain

1. Verified boot or an upstream verified runtime surface supplies the artifact set that contains `CommandCatalogPlan` and required diagnostics provenance.
2. CommandCatalog is created only from verified runtime input.
3. CommandCatalog owns the table-driven mapping from `CommandTypeId` to executor reference, payload schema reference, and command metadata needed for diagnostics.
4. Payload validation occurs before executor invocation for the accepted path.
5. Missing or partial catalog tables fail closed.
6. Accepted runtime command authority does not depend on scope-acquire loading, `AssetDatabase` search, `Resources.Load`, or editor-only stable-key scan behavior.

Wave C therefore requires the replacement of the following current authority points:

- `CommandCatalogService.OnAcquire()` as accepted catalog truth creation
- `CommandCatalogLocator.GetOrCreate()` as accepted runtime command-catalog authority
- `CommandCatalogSO` as accepted runtime command truth, except through an explicit verified projection or compatibility boundary

### Required Executor Authority Chain

1. Executor availability is projected into verified command metadata rather than discovered from service collections.
2. Runner resolves executor identity through CommandCatalog metadata instead of scanning `IReadOnlyList<ICommandExecutor>`.
3. Allowed executor forms remain those permitted by v2 command runtime, including generated function, stateless singleton, lazy singleton, pooled executor, and explicit legacy adapter.
4. Executor dependencies may be consumed only through declared boundaries validated by v2 and already-supported scope or service authority.
5. Command executor availability must not imply lifecycle participation or service discovery by interface scan.

Wave C therefore requires the replacement of the following current authority points:

- `builder.Register<XExecutor>().As<ICommandExecutor>()` as accepted command truth
- `CommandExecutorCatalog(IReadOnlyList<ICommandExecutor>)` as accepted executor table construction
- collection-style executor discovery under a renamed facade

### Required Runner Authority Chain

1. Runner receives normalized command data or compatibility-normalized equivalent input.
2. Runner validates payload schema before executing the resolved executor.
3. Runner resolves executor through verified catalog authority.
4. Runner owns execution-frame creation, nested list execution, break, cancel, timeout, detached execution, and diagnostics behavior.
5. Runner records command diagnostics with runtime identity and provenance rather than relying on implementation discovery.
6. If multiple runner domains remain, they must be explicit verified execution domains rather than a silent byproduct of legacy scope-kind registration.

Wave C therefore requires the following runner rules:

- nested command execution in `Function`, `CommandChannel`, control-flow, and lifecycle-wrapper commands must continue through a single explicit runner responsibility model
- executor-side payload repair, ad-hoc detached execution policy, or hidden fallback dispatch is forbidden in the accepted path
- accepted runner authority must not depend on the legacy `LifetimeScopeKind` switch remaining the hidden truth under a new name

### Transitional Coexistence Rules

| Transitional Condition | Allowed During Wave C | Required Rule |
| --- | --- | --- |
| Existing payload classes still expose compatibility-facing `CommandId` surfaces | Yes | They may remain as migration-facing inputs, but accepted runtime dispatch must normalize them to verified command identity before executor resolution. |
| Some command implementations still use `ICommandExecutor` or legacy adapters | Yes | They may remain only when explicitly mapped through verified command authority or bounded compatibility adapters rather than collection discovery. |
| `CommandCatalogSO` still exists for editor authoring or inspection | Yes | It may remain as authoring or compatibility data, but accepted runtime command truth must not depend on `Resources.Load` or scope-acquire loading. |
| Stable-key command sources still exist in authored content during transition | Temporarily | They may remain only if they feed explicit normalization. Accepted runtime path must reject runtime-only negative-key repair and stable-key dispatch fallback. |
| `CommandRunnerMB` or bulk executor registration still decides accepted runtime outcomes | No | Accepted Wave C behavior must fail validation, diagnostics, or acceptance rather than silently depending on legacy bootstrap. |

---

## Runtime Key and Catalog Fallback Quarantine

Wave C requires explicit quarantine for the following legacy compatibility behavior:

- `CommandKeyResolver` runtime-only negative-key allocation is legacy repair behavior and must not participate in the accepted path
- `CatalogCommandSource` stable-key fallback and `CommandCatalogSO` editor-only stable-key scan helpers may exist only as migration or editor conveniences, not as accepted runtime dispatch truth
- `CommandCatalogLocator` editor asset creation and runtime `Resources.Load("CommandCatalog")` behavior may exist only outside the accepted runtime path
- `CommandCatalogService` acquire or release lifecycle behavior may exist only as a bounded compatibility surface if it no longer defines accepted command truth

Quarantined behavior must be profile-visible, diagnosable, and removable.
It must not remain a silent permanent fallback.

---

## Boundary Rules

Wave C must preserve clear subsystem boundaries while command authority changes.

### ServiceGraph Boundary

- executors may consume services only through declared dependencies and already-migrated service boundaries
- command runtime must not discover executors through ServiceGraph or treat ServiceGraph as a general runtime query mechanism
- service fallback or collection discovery must not repair missing command executor identity or metadata

### ScopeGraph and Target Boundary

- representative actor-routing commands may preserve their high-level behavior, but accepted scope or actor truth must come from explicit migrated runtime authority rather than registry repair or hierarchy discovery
- runner context inheritance and nested command execution must remain explicit
- command dispatch must not reintroduce transform-derived runtime authority that Wave B already demoted

### Value and Blackboard Boundary

- Wave C may preserve function input or output behavior, command-local flow, and representative lifecycle-wrapper observability only to the extent necessary to keep preserved command surfaces valid
- Wave C must not silently redefine `ValueStore`, generated value-key identity, blackboard ownership, or var-registry semantics, because those belong to Wave D

---

## Wave C Subphases

### WC-0 Current-State Inventory

Objective:
freeze the current command authority chain before cutover work starts.

Required outputs:

- representative command authority anchor inventory
- preserved command-contract table
- explicit list of catalog-loading, key-fallback, executor-discovery, and runner-domain authority points

Exit gate:
the current command authority chain is traceable from representative authored content and command sources through executor invocation.

Forbidden shortcuts:

- starting implementation before command authority is written down
- assuming Wave B already solved command truth just because scope and service boundaries were migrated

### WC-1 Command Identity Boundary

Objective:
separate preserved authored command surfaces from accepted runtime dispatch identity.

Required outputs:

- explicit mapping policy from preserved migration-facing inputs to `CommandTypeId`
- explicit role split for `CommandTypeId`, `CommandExecutorId`, `CommandPayloadSchemaId`, and `CommandAuthoringKeyId`
- explicit rejection policy for ambiguous or missing normalization

Exit gate:
accepted runtime dispatch can explain its command identity without using stable-key or collection-discovery truth.

Forbidden shortcuts:

- using authoring keys or stable keys as accepted runtime dispatch identity
- treating legacy compatibility-facing integer command IDs as sufficient runtime truth without explicit normalization rules

### WC-2 Catalog and Key-Fallback Quarantine

Objective:
remove lifecycle-time catalog loading and runtime key repair from accepted runtime command truth.

Required outputs:

- quarantine rules for `CommandCatalogService`, `CommandCatalogLocator`, `CommandKeyResolver`, `CommandCatalogSO`, and `CatalogCommandSource`
- explicit verified runtime input model for command catalog authority
- diagnostics for continued reliance on runtime key repair or catalog-convention loading

Exit gate:
accepted runtime command authority no longer requires scope-acquire loading, `Resources.Load`, editor-only stable-key scan behavior, or runtime-only negative-key allocation.

Forbidden shortcuts:

- keeping scope-acquire catalog loading as a hidden steady-state authority path
- relying on editor-only stable-key fallback behavior and calling it migration-complete runtime behavior

### WC-3 Executor Table Cutover

Objective:
replace bulk executor discovery with explicit verified executor mapping.

Required outputs:

- explicit plan-projected executor mapping model
- explicit allowance rules for any remaining legacy executor adapters
- removal of `IReadOnlyList<ICommandExecutor>` collection scan as accepted executor truth

Exit gate:
accepted runtime command dispatch can resolve representative executors without DI collection discovery.

Forbidden shortcuts:

- renaming collection discovery as a new catalog without changing authority
- keeping `builder.Register<XExecutor>().As<ICommandExecutor>()` as the hidden source of truth

### WC-4 Runner Domain and Execution Flow Cutover

Objective:
separate runner execution flow from discovery behavior while preserving representative nested execution semantics.

Required outputs:

- explicit runner responsibility model for payload validation, executor resolution, nested list execution, and failure behavior
- explicit runner-domain policy for any remaining multi-runner execution domains
- uniform break, cancel, timeout, and detached execution policy for representative commands

Exit gate:
representative nested command execution surfaces run through migrated runner authority with explicit diagnostics provenance.

Forbidden shortcuts:

- executor-side payload repair or schema inference in the accepted path
- leaving legacy scope-kind runner registration as the hidden truth under an abstract name
- preserving bespoke detached-execution policy as an unvalidated per-executor side effect

### WC-5 Representative Preserved Surface Validation

Objective:
prove that preserved representative command surfaces still function after authority cutover.

Required outputs:

- representative continuity matrix for `SceneChange`, `WithActor`, `Function`, `CommandChannel`, control-flow, and `SelfDespawn`
- explicit proof that nested command-list behavior still works without legacy command authority
- explicit proof that preserved authored field shapes remain usable without forced reauthoring

Exit gate:
representative command content preserves its high-level execution meaning through migrated authority.

Forbidden shortcuts:

- validating only trivial single-command execution paths
- omitting nested-body, child-list, detached, or background execution scenarios from the proof surface

### WC-6 Mixed-Authority Diagnostics and Legacy Demotion

Objective:
make remaining legacy command paths visible, bounded, and unacceptable as the steady state.

Required outputs:

- mixed-authority diagnostics for bulk executor registration, lifecycle-time catalog loading, and runtime key repair
- explicit demotion rules for remaining legacy command bootstrap helpers
- bounded profile rules for temporary compatibility behavior

Exit gate:
accepted profiles can distinguish verified command authority from legacy compatibility behavior.

Forbidden shortcuts:

- claiming migration success while bulk registration or runtime key repair still silently decides real outcomes
- leaving compatibility adapters enabled without diagnostics or profile control

### WC-7 Acceptance Gate

Objective:
prove that Wave C is materially complete.

Required outputs:

- executable verification plan for representative command authority and representative preserved surfaces
- diagnostics coverage for missing plan input, missing executor mapping, missing payload schema, and mixed authority
- documentation updates that reflect the accepted command authority model

Exit gate:
all Wave C acceptance criteria and required test cases pass.

Forbidden shortcuts:

- accepting Wave C because representative commands still appear to run without proving the authority model changed
- accepting Wave C without a fail-closed story for missing catalog, missing executor mapping, or missing payload schema

---

## Forbidden Shortcuts

The following shortcuts are explicitly forbidden for Wave C:

- treating stable-key dispatch as acceptable because the content already uses stable keys
- treating runtime-only negative-key allocation as acceptable because it only appears in debug or editor paths
- preserving bulk `ICommandExecutor` discovery under a renamed facade and calling the problem solved
- using `Resources.Load("CommandCatalog")` or scope-acquire service loading as accepted runtime command truth
- letting executors repair payload shape, schema gaps, or missing command metadata on the fly
- silently widening Wave C to absorb Wave D value semantics or broad gameplay migration work

---

## Diagnostics and Failure Policy

v2 specification 11 owns the diagnostics substrate.
Wave C defines the conditions that must become diagnostic-visible and acceptance-visible.

| Code | Failure Condition | Required Result |
| --- | --- | --- |
| V21-WC-ID-001 | Accepted runtime path does not prove normalization from preserved command-facing input to `CommandTypeId`. | Validation or runtime entry into the accepted command path must fail before dispatch. |
| V21-WC-ID-002 | Accepted runtime path uses authoring key, stable key, or runtime-only negative key as command dispatch identity. | Validation or diagnostics must reject the path as legacy identity repair rather than accepted command authority. |
| V21-WC-CATALOG-001 | `CommandCatalogPlan` or required representative command projection is missing for the accepted path. | Command entry into the migrated path must fail closed before representative command execution begins. |
| V21-WC-CATALOG-002 | Accepted runtime path still depends on `CommandCatalogService` acquire-time loading, `CommandCatalogLocator`, `Resources.Load`, or editor-only stable-key scan behavior as catalog truth. | Validation or diagnostics must reject the path as legacy catalog authority. |
| V21-WC-EXEC-001 | Accepted runtime path still depends on `builder.Register<XExecutor>().As<ICommandExecutor>()`, `IReadOnlyList<ICommandExecutor>` discovery, or `CommandExecutorCatalog` collection scan as executor truth. | Validation or diagnostics must reject the path as legacy executor discovery. |
| V21-WC-EXEC-002 | Missing executor mapping or missing payload schema is repaired by no-op behavior, default construction, or executor-side cast or reflection behavior. | The command must fail closed with structured diagnostics rather than being silently repaired. |
| V21-WC-RUNNER-001 | Payload validation is skipped or happens after executor invocation in the accepted path. | The command must not execute and must fail with structured diagnostics. |
| V21-WC-RUNNER-002 | Runner domain truth or representative break, cancel, timeout, or detached behavior still depends on legacy scope-kind wiring rather than explicit migrated policy. | Diagnostics or acceptance must reject the path as mixed runner authority. |
| V21-WC-CONTRACT-001 | A representative preserved command surface loses its high-level execution meaning after the cutover. | Wave C acceptance must fail until preserved representative behavior is restored or the contract is explicitly revised. |
| V21-WC-LEGACY-001 | Legacy and verified command authority are both active for the same accepted profile. | The profile must fail validation or runtime acceptance with a mixed-authority diagnostic. |
| V21-WC-BOUNDARY-001 | Wave C silently re-owns Wave D value semantics or broader gameplay migration work while changing command authority. | The change must be rejected as scope drift in review or acceptance. |

---

## Acceptance Criteria

Wave C is complete only when all of the following are true:

- representative accepted command dispatch enters through verified command authority and normalized `CommandTypeId` mapping
- accepted runtime command authority no longer depends on lifecycle-time catalog loading, `Resources.Load` catalog lookup, stable-key dispatch fallback, or runtime-only negative-key repair
- accepted runtime executor lookup no longer depends on bulk `ICommandExecutor` registration or collection scan behavior
- payload schema validation occurs before executor invocation and missing schema or executor mapping fails closed
- representative preserved command surfaces such as `SceneChange`, `WithActor`, `Function`, `CommandChannel`, control-flow commands, and `SelfDespawn` continue to preserve their high-level execution meaning
- nested child-list execution, background or detached execution, and break or cancel handling operate through a uniform runner responsibility model rather than hidden discovery behavior
- mixed legacy and verified command authority is diagnosable and unacceptable rather than silently tolerated
- Wave D value, blackboard, and var semantics remain outside Wave C except for the boundary continuity required to keep preserved command surfaces working

---

## Out of Scope and Handoff to Later Waves

The following work is explicitly deferred:

- value-store, blackboard, and var identity semantics, which belong to Wave D
- representative gameplay-system migration that consumes the migrated command path, which belongs to Wave E
- final deletion of all legacy command adapters, convenience paths, and hardened cleanup, which belong to [06_WaveFLegacyRemovalAndHardeningSpec.md](06_WaveFLegacyRemovalAndHardeningSpec.md)

Wave C should make those waves easier.
It must not claim to have completed them.

---

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V21-WC-01 | Confirm representative commands dispatch through verified command authority. | Accepted runtime dispatch must normalize representative command-facing input to `CommandTypeId` before executor resolution. |
| TC-V21-WC-02 | Confirm stable-key and authoring-key runtime dispatch fallback is rejected. | The accepted path must fail if it still depends on authoring-key or stable-key dispatch truth. |
| TC-V21-WC-03 | Confirm runtime-only negative-key allocation is not part of the accepted path. | A profile that relies on `CommandKeyResolver` runtime fallback must fail validation or acceptance. |
| TC-V21-WC-04 | Confirm bulk executor discovery and collection scan are not required. | The accepted path must not depend on `IReadOnlyList<ICommandExecutor>` resolution or builder mutation truth. |
| TC-V21-WC-05 | Confirm payload schema validation occurs before executor invocation. | Missing or invalid representative payload schema must fail before execution. |
| TC-V21-WC-06 | Confirm `Function` preserved behavior remains valid. | Initial-var injection, nested execution, and output propagation back to caller vars must remain functional. |
| TC-V21-WC-07 | Confirm `CommandChannel` preserved behavior remains valid. | Representative tag dispatch, await handling, and background cancel behavior must remain functional. |
| TC-V21-WC-08 | Confirm `WithActor` and representative control-flow behavior remain valid. | Actor-routing, nested body execution, and representative `Break` or `Cancel` behavior must remain functional. |
| TC-V21-WC-09 | Confirm `SceneChange` and `SelfDespawn` preserved behavior remain valid. | Representative scene-transition and lifecycle-wrapper commands must continue to work through migrated command authority. |
| TC-V21-WC-10 | Confirm accepted runtime command authority no longer requires lifecycle-time catalog loading or `Resources.Load` command catalog lookup. | A migrated profile must receive command authority through verified runtime input rather than catalog-loading convenience paths. |
| TC-V21-WC-11 | Confirm mixed legacy and verified command authority is rejected. | A profile that leaves bulk executor registration or runtime key repair active alongside migrated command authority must fail validation or acceptance. |

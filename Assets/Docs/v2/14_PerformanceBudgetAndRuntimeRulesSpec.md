# Performance Budget and Runtime Rules Specification

## Document Status

- Document ID: 14_PerformanceBudgetAndRuntimeRulesSpec
- Status: Draft
- Role: defines runtime path classification, performance budgets, forbidden runtime operations, profiler marker policy, and regression rules for Kernel v2
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
  - [10_ValueSchemaAndStoreSpec.md](10_ValueSchemaAndStoreSpec.md)
  - [10_1_ScalarRuntimeAndBindingSpec.md](10_1_ScalarRuntimeAndBindingSpec.md)
  - [10_2_DynamicValueEvaluationSpec.md](10_2_DynamicValueEvaluationSpec.md)
  - [11_DebugMapAndDiagnosticsSpec.md](11_DebugMapAndDiagnosticsSpec.md)
  - [12_UnityAuthoringBridgeSpec.md](12_UnityAuthoringBridgeSpec.md)
  - [13_LegacyCompatBoundarySpec.md](13_LegacyCompatBoundarySpec.md)
- Provides foundation for:
  - [15_TestAndValidationSpec.md](15_TestAndValidationSpec.md)

### Revision Note

This revision creates 14 as the runtime performance constitution for Kernel v2.

It does not treat performance as a late optimization concern.
It defines which runtime operations are forbidden by default, which costs must be explicit and measurable, which profiler markers must exist, and how regression must be detected before architectural debt re-enters the target runtime.

It also records the current migration debt around hierarchy scans, registration scans, reflection construction, runtime string or stable-key fallback, `Resources.Load` fallback, bulk executor registration, and missing profiler markers in the runtime core.

---

## Purpose

This specification defines the performance rules that the target kernel runtime must obey.

Core statements:

```text
Performance rules are architecture rules.

A target runtime path must not depend on hierarchy scans, registration scans, reflection discovery, runtime fallback, string-key resolution, or hidden allocation.

If a subsystem needs cost, the cost must be explicit, measured, budgeted, and tested.
```

Required central rule:

```text
Runtime performance is enforced by architecture rules, not recovered by late optimization.
```

Stronger rule:

```text
If a target runtime path needs a broad scan, hidden allocation, string lookup, reflection discovery, or fallback repair, the design is wrong by default.
```

---

## Scope

This specification defines:

- runtime path classification
- performance budget philosophy
- global runtime rules
- forbidden runtime operations
- allocation policy
- reflection and type lookup policy
- Unity API usage policy
- scene and hierarchy scan policy
- string key and runtime ID lookup policy
- generated table and slot lookup policy
- subsystem performance budgets
- diagnostics performance rules
- profiler marker policy
- benchmark fixture policy
- regression test policy
- performance report format
- performance failure policy
- forbidden patterns
- required performance tests

---

## Non-Goals

This specification does not define:

- the final implementation details of each subsystem
- Unity Profiler UI usage instructions
- final CI environment setup procedure
- per-game final frame budgets
- platform-specific tuning details
- GPU, renderer, shader, or content-pipeline optimization details
- user-facing editor UX for performance dashboards

14 must not become a dump of generic optimization advice.
It defines the minimum rules that runtime architecture must satisfy before implementation details are even considered acceptable.

---

## Relationship to Other Specs

| Spec | Relationship |
|---|---|
| [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md) | Defines root performance policy and delegates exact marker taxonomy, budgets, and regression rules to 14. |
| [01_KernelIRSpec.md](01_KernelIRSpec.md) | Owns normalized identity and artifact structure that runtime lookup tables consume. |
| [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md) | Owns contribution inputs that must not force runtime discovery or hot-path reflection. |
| [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md) | Owns generation outputs whose size, lookup form, and loading cost are constrained by 14. |
| [04_DependencyValidationSpec.md](04_DependencyValidationSpec.md) | Owns validation; 14 defines the measurable runtime and generation costs that validation must not be bypassed to avoid. |
| [05_BootManifestAndProfileSpec.md](05_BootManifestAndProfileSpec.md) | Owns boot semantics and minimum boot marker requirements; 14 owns the full performance taxonomy, caps, and regression rules. |
| [06_ServiceGraphRuntimeSpec.md](06_ServiceGraphRuntimeSpec.md) | Owns service runtime semantics; 14 constrains hot-path resolve cost, reflection use, scans, and allocations. |
| [07_ScopeGraphRuntimeSpec.md](07_ScopeGraphRuntimeSpec.md) | Owns scope runtime semantics; 14 constrains handle validation, parent lookup, and scope-create costs. |
| [08_LifecyclePlanSpec.md](08_LifecyclePlanSpec.md) | Owns lifecycle ordering; 14 constrains dispatch cost, handler collection, and per-entity tick rules. |
| [09_CommandCatalogRuntimeSpec.md](09_CommandCatalogRuntimeSpec.md) | Owns command runtime semantics; 14 constrains lookup complexity, executor construction cost, and dispatch allocation. |
| [10_ValueSchemaAndStoreSpec.md](10_ValueSchemaAndStoreSpec.md) | Owns value schema and store semantics; 14 constrains hot-path slot lookup, allocation, and fallback prohibition cost. |
| [10_1_ScalarRuntimeAndBindingSpec.md](10_1_ScalarRuntimeAndBindingSpec.md) | Owns scalar runtime semantics; 14 constrains scalar hot paths, binding cost, and measurement requirements. |
| [10_2_DynamicValueEvaluationSpec.md](10_2_DynamicValueEvaluationSpec.md) | Owns evaluation semantics; 14 constrains cache, invalidation, and evaluation hot-path costs. |
| [11_DebugMapAndDiagnosticsSpec.md](11_DebugMapAndDiagnosticsSpec.md) | Owns diagnostics semantics; 14 constrains formatting cost, emission rate, throttling, and measurement. |
| [12_UnityAuthoringBridgeSpec.md](12_UnityAuthoringBridgeSpec.md) | Owns authoring extraction and normalization semantics; 14 constrains authoring and direct-play preparation cost as editor-generation budgets. |
| [13_LegacyCompatBoundarySpec.md](13_LegacyCompatBoundarySpec.md) | Owns legacy quarantine semantics; 14 constrains legacy bridge cost and forbids legacy work on target hot paths by default. |
| [15_TestAndValidationSpec.md](15_TestAndValidationSpec.md) | Implements executable profiler-marker assertions, forbidden-operation gates, and regression enforcement using the budgets and taxonomy defined here. It does not redefine budget meaning or marker categories. |

14 owns performance rules, not subsystem meaning.
It must measure and constrain the runtime architecture without stealing semantic ownership from the domain specs.

---

## Assembly Definition and Compile Boundary Expectations

14 is cross-cutting, but its enforcement still depends on explicit assembly boundaries.
Detailed dependency matrices remain owned by [17_AssemblyDefinitionAndCompileBoundarySpec.md](17_AssemblyDefinitionAndCompileBoundarySpec.md).

Required compile-boundary rules for 14:

- hot-path production assemblies must not gain test-only, editor-only, or legacy-only references just to support measurement
- profiler integration and Unity-facing markers may live in Unity-capable runtime bridge assemblies, but performance policy itself must not force Unity dependencies into pure core assemblies
- forbidden-pattern scanners, benchmark fixtures, and regression gates belong in `GameLib.Tests.*` or editor test assemblies rather than in production runtime assemblies
- package spread for measurement utilities must not undermine the low-volatility kernel core graph

If a performance gate can only be implemented by making kernel core depend on UnityEditor, test frameworks, or legacy assemblies, the 14 boundary has been violated.

---

## Current Performance Debt Observations

Current legacy runtime contains several performance risks that the target architecture must not preserve.

### Observation Traceability

| Observation | Evidence Type | Target Pressure |
|---|---|---|
| feature discovery still uses `GetComponentsInChildren` | Source | no hierarchy scan in target runtime |
| scope ownership is still inferred through `Transform.parent` traversal | Source | no transform-derived runtime truth |
| resolver still performs component fallback after registration miss | Source | explicit service lookup tables and no hidden Unity search |
| resolver still uses reflection for constructor and parameter inspection | Source | no runtime reflection discovery |
| lifecycle handler collection still scans all registrations and allocates `List`, `HashSet`, and arrays | Source | precomputed dispatch table and no scan in target lifecycle |
| command executors are still registered eagerly through a large installer | Source | no bulk eager registration cost in target boot |
| value identity can still fall back to runtime stable-key resolution and negative IDs | Source | verified ID lookup only |
| registry lookup still uses `Resources.Load` fallback | Source | no runtime asset fallback |
| no profiler markers were found in the explored runtime anchors | Source | mandatory measurement and regression detection |

### Representative Anchors

- [ScopeFeatureInstallerUtility.cs](../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs) - installer discovery through `GetComponentsInChildren` and nearest-scope inference through `Transform.parent`
- [RuntimeLifetimeScope.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs) - installer caching and build-time collection from scope hierarchy
- [RuntimeResolverHub.cs](../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs) - component fallback, reflection construction, registration scans, handler collection, and dispatcher materialization
- [CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs) - bulk executor and lifecycle-related service registration
- [VarIdResolver.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs) - runtime stable-key fallback, negative ID creation, and lock-based miss handling
- [VarKeyRegistryLocator.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs) - throttled `Resources.Load` and runtime-created fallback registry instance
- [BlackboardMB.cs](../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs) - multi-role MonoBehaviour affecting build, acquire, debug, and transform-related paths

### Current Gaps

The target architecture must close the following gaps:

- runtime boot and scope build can still pay hierarchy scan costs
- normal runtime service access can still fall into Unity component search
- handler collection can still pay registration-scan and allocation costs
- command boot cost can still scale with bulk eager registration
- value and registry access can still pay string-key, lock, and `Resources.Load` costs
- runtime core cannot currently prove its budgets because profiler markers are not present in the explored anchors

---

## Runtime Path Classification

Not every path is constrained identically.
Performance rules must still be explicit for every path kind.

Explanatory model:

```csharp
public enum RuntimePathKind
{
    HotPath = 10,
    WarmPath = 20,
    ColdPath = 30,
    BootPath = 40,
    EditorGenerationPath = 50,
    ValidationPath = 60,
    TestOnlyPath = 70,
    LegacyMigrationPath = 90,
}
```

Definitions:

- `HotPath`: every-frame or very high-frequency operations such as service resolve, lifecycle tick dispatch, command dispatch, value read or write, handle validation, and selected runtime query operations
- `WarmPath`: frequent gameplay operations that are not per-frame hot, such as scope create or destroy, command sequence start, runtime query index update, or declared binding rebases
- `ColdPath`: infrequent operations during gameplay or transitions
- `BootPath`: kernel boot operations that still must not skip validation or fallback rules
- `EditorGenerationPath`: editor, build, or CI generation and extraction work
- `ValidationPath`: deterministic structural checks prior to runtime execution
- `TestOnlyPath`: benchmarks, analyzers, and validation instrumentation not present in shipping runtime paths
- `LegacyMigrationPath`: explicit migration tooling or quarantine-bound adapter work outside target hot paths

Representative current classification examples:

- `RuntimeResolverHub.TryResolve` is `HotPath` when used on gameplay access paths
- `RuntimeAcquireReleaseDispatcher.Acquire` and `Release` are `WarmPath` or `HotPath` depending on frequency and subsystem context
- `ScopeFeatureInstallerUtility.InstallOwnedFeatureInstallers` is current `BootPath` or `WarmPath` debt, not a target runtime pattern
- `VarKeyRegistryLocator.GetOrCreate` is current `WarmPath` debt but must not become a target runtime repair path
- authoring extraction and direct-play preparation are `EditorGenerationPath`

---

## Performance Budget Philosophy

A performance budget is not only a target number.
It is a design contract.

Every budgeted subsystem must define:

- expected cardinality
- path kind
- hot-path operations
- allowed allocation
- allowed lookup complexity
- allowed Unity API usage
- required profiler marker
- regression test strategy

Bad budget shape:

```text
Service resolve should be fast.
```

Acceptable budget shape:

```text
Service resolve is HotPath.
Normal resolve allocates 0 B.
Lookup uses slot or index tables.
Registration-wide scan is forbidden.
Profiler marker and regression test are required.
```

Budgets are invalid if they define target numbers without cardinality, path kind, allocation rule, and measurement point.

---

## Global Runtime Rules

Target runtime must follow these global rules:

1. runtime structure must come from verified plans
2. hot and warm paths must not perform broad discovery
3. hot-path normal success operations must not allocate managed memory unless an explicit exception is budgeted
4. hot and warm paths must not use string-key resolution for runtime identity
5. runtime must not use reflection discovery for structure or dependency resolution
6. runtime must not repair missing verified data through fallback
7. budgeted operations must expose profiler markers or equivalent measurable points
8. runtime failures must use the central diagnostics pipeline rather than direct Unity error logging in subsystem code
9. performance optimization must not skip validation, diagnostics provenance, or compatibility checks
10. exceptions must be explicit, bounded, measured, and covered by tests

---

## Forbidden Runtime Operations

The following operations are forbidden in target runtime hot and warm paths unless a lower spec explicitly declares a bounded exception:

- `FindObjectsByType`
- `GameObject.Find`
- `Object.FindFirstObjectByType`
- `Object.FindAnyObjectByType`
- `GetComponentsInChildren` for kernel discovery
- `GetComponentsInParent` for kernel ownership inference
- `Transform.parent` traversal for scope ownership or parent inference
- `Resources.Load` fallback for required kernel data
- reflection-based constructor discovery
- service contract discovery by implemented-interface scan
- lifecycle participation discovery by interface scan
- service registration scan for handler collection
- string-key lookup for command or value runtime identity
- runtime generation of missing IDs
- LINQ in hot paths
- `List`, `HashSet`, or array allocation in normal hot dispatch
- direct `Debug.LogError` or equivalent outside the central diagnostics sink

These are not style issues.
They are architectural failures in target runtime paths.

---

## Allocation Policy

HotPath normal success operations must be allocation-free unless explicitly budgeted.

Default policy:

| Path | Normal success allocation |
|---|---:|
| `HotPath` | 0 B |
| `WarmPath` | 0 B preferred, bounded only if justified |
| `ColdPath` | bounded allocation allowed |
| `BootPath` | bounded allocation allowed |
| `EditorGenerationPath` | allocation allowed but deterministic and measurable |
| `ValidationPath` | allocation allowed but deterministic and measurable |
| `TestOnlyPath` | allocation allowed when required by the test harness |
| `LegacyMigrationPath` | allocation allowed when explicit and outside target hot paths |

Allocation exception requirements:

- subsystem
- operation
- path kind
- expected frequency
- expected max allocation
- reason
- profiler marker
- test coverage

Pooling is allowed only when it does not hide unbounded retention, stale state, or missing cleanup.

---

## Reflection and Type Lookup Policy

Runtime reflection is forbidden for discovery.

Forbidden in target runtime paths:

- constructor selection by reflection
- service contract discovery by implemented-interface scan
- command executor discovery by interface scan
- lifecycle participation discovery by interface scan
- payload schema inference by runtime reflection
- broad `IsAssignableFrom` scans over registrations in hot or warm paths
- `Activator.CreateInstance` as fallback construction in target runtime paths

Allowed only in explicitly bounded paths:

- editor and build-time generation
- diagnostics-only type-name formatting
- test-only reflection
- explicit legacy migration tooling under 13

Type lookup in runtime is allowed only when it is precomputed, bounded, and not used to discover structure dynamically.

---

## Unity API Usage Policy

Unity API usage must be explicit and path-aware.

Rules:

- `UnityEngine.Object` access is main-thread only by default unless a lower spec explicitly defines a safe alternative
- Unity fake-null must not be used as structure truth
- Unity object search must not repair missing kernel data
- Unity `Transform` hierarchy is visual and authoring data, not runtime kernel structure
- `Resources.Load` may not act as runtime repair for required data
- target runtime subsystems must not use Unity convenience search APIs as dependency resolution or identity resolution

Allowed Unity API usage must still declare path kind, bounded cost, and failure behavior.

---

## Scene and Hierarchy Scan Policy

Scene-wide and hierarchy-wide scans are forbidden in target runtime paths.

Allowed only in:

- editor validation
- authoring extraction
- migration tooling
- explicit test fixtures

Even when allowed, scan results must be normalized into verified artifacts or validated migration output before runtime boot.

Current hierarchy-scan debt such as [ScopeFeatureInstallerUtility.cs](../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs) is migration evidence, not target runtime precedent.

---

## String Key and Runtime ID Lookup Policy

Runtime identity lookup must use verified IDs.

Forbidden in target runtime paths:

- command dispatch by string
- value read or write by stable-key string
- service resolution by string
- runtime ID generation for missing key
- fallback from missing ID to name lookup
- runtime-only negative IDs
- runtime hash-based identity creation from authoring strings

String keys may appear in authoring, diagnostics, reports, and migration maps.
They are not runtime truth.

---

## Generated Table and Slot Lookup Policy

Runtime lookup should use generated or verified lookup structures.

Preferred structures:

- dense slot table
- compact sorted table with binary search for small or cold sets
- generated switch for small static sets
- handle index plus generation table
- precomputed dispatch table
- explicit query index with declared invalidation policy

Representative subsystem examples:

- `ServiceGraph`: `ServiceId -> slot index`
- `CommandCatalog`: `CommandTypeId -> executor ref`
- `Lifecycle`: `Phase -> dispatch table`
- `ValueStore`: `ValueKeyId -> slot index`
- `ScopeGraph`: `ScopeHandle -> index + generation`
- `RuntimeQuery`: explicit indexed fields and query plans

Raw dictionary lookup may still exist in bounded cold or migration paths when explicitly justified.
It must not become a hidden substitute for verified lookup structures in target hot paths.

---

## Boot Performance Budget

`BootPath` may perform validation and artifact loading.
It must not perform runtime discovery.

Boot must not:

- search the scene for kernel roots
- use `Resources.Load` fallback for required artifacts
- instantiate all command executors eagerly
- scan all `MonoBehaviour` instances for services
- construct all optional services eagerly
- skip validation or DebugMap consistency checks for speed

Required marker family for boot includes at least:

- `KernelBoot.LoadInputs`
- `KernelBoot.LoadManifest`
- `KernelBoot.LoadArtifactSet`
- `KernelBoot.ValidateHeaders`
- `KernelBoot.ValidateHashes`
- `KernelBoot.ValidateProfile`
- `KernelBoot.CreateRuntime`
- `KernelBoot.CreateEssentialServices`
- `KernelBoot.CreateRootScopes`
- `KernelBoot.RunBootLifecycle`

Boot budget must classify cost into structural cost and activation cost.
It must not hide discovery, fallback repair, or eager construction behind aggregate timing.

---

## ServiceGraph Performance Budget

Service resolve is `HotPath`.

Rules:

- required resolve should be O(1) or bounded small constant
- normal resolve must allocate 0 B
- no registration-wide scan
- no interface scan
- no reflection construction in normal success path
- no component fallback search
- command executor collection must not be a side effect of service resolution

Required marker family includes at least:

- `ServiceGraph.Build`
- `ServiceGraph.Resolve`
- `ServiceGraph.Construct`

Current resolver caches in [RuntimeResolverHub.cs](../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs) are migration evidence, not the target implementation model.
The target direction is verified slot or index lookup, not a generic DI resolver that also discovers runtime objects.

---

## ScopeGraph Performance Budget

Scope handle validation is `HotPath`.

Rules:

- handle validation should be O(1)
- parent lookup should be O(1)
- child enumeration cost should scale with child count, not total scope count
- no `Transform` traversal for parent inference
- no scene object search
- no allocation in normal handle validation path

Required marker family includes at least:

- `ScopeGraph.CreateScope`
- `ScopeGraph.DestroyScope`
- `ScopeGraph.ValidateHandle`
- `ScopeGraph.Reparent`
- `ScopeGraph.SetState`

Warm-path scope create and destroy costs must remain explicit and bounded.
They must not hide hierarchy scan or runtime repair behavior.

---

## Lifecycle Performance Budget

Lifecycle dispatch contains both `WarmPath` and `HotPath` operations.

Rules:

- dispatch tables must be precomputed
- no service registration scan during normal dispatch
- no implemented-interface scan during normal dispatch
- no `List`, `HashSet`, or `ToArray` during normal dispatch
- tick step count must be budgeted explicitly
- per-entity tick steps are forbidden by default unless a lower spec declares a bounded exception

Required marker family includes at least:

- `Lifecycle.DispatchAcquire`
- `Lifecycle.DispatchRelease`
- `Lifecycle.DispatchTick`
- `Lifecycle.DispatchLateTick`
- `Lifecycle.DispatchFixedTick`

Patterns such as `CollectHandlers<THandler>()` in [RuntimeResolverHub.cs](../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs) are migration debt, not target lifecycle design.

---

## CommandCatalog Performance Budget

Command dispatch is `HotPath`.

Rules:

- `CommandTypeId` lookup should be O(1) or bounded small constant
- executor lookup must not scan all command types or all executors
- normal simple command dispatch should allocate 0 B where practical
- command count must not force eager construction of all executors at boot
- payload validation should use precomputed schema metadata

Required marker family includes at least:

- `CommandCatalog.Lookup`
- `Command.Execute`

Bulk eager registration patterns such as [CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs) are migration evidence and must not define target dispatch cost.

---

## ValueStore Performance Budget

Value read and write are `HotPath`.

Rules:

- `ValueKeyId -> slot` lookup should be O(1) or bounded small constant
- scalar read and write should allocate 0 B
- no stable-key lookup in runtime read or write
- no schema inference during write
- no boxing for common scalar types where practical
- revision increment and dirty signaling must remain cheap and bounded

Required marker family includes at least:

- `ValueStore.Read`
- `ValueStore.Write`
- `ValueStore.ApplyInitPlan`
- `ValueStore.EmitDirtySignal`

Current fallback patterns such as [VarIdResolver.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs) and [VarKeyRegistryLocator.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs) are explicitly outside the target budget model.

---

## RuntimeQuery Performance Budget

`RuntimeQuery` must use explicit indexes.

Rules:

- query cost must be declared per query kind
- broad scans are forbidden in hot paths
- invalidation policy must be explicit
- ambiguity policy must be explicit
- query index update cost must be budgeted

Required marker family includes at least:

- `RuntimeQuery.Lookup`
- `RuntimeQuery.UpdateIndex`
- `RuntimeQuery.InvalidateIndex`

RuntimeQuery must not become service-resolution-by-another-name or fallback scene search.

---

## Diagnostics Performance Budget

Diagnostics must not make hot-path failures catastrophically expensive.

Rules:

- producer paths should avoid expensive string formatting
- formatting belongs to sinks where practical
- repeated diagnostics should support throttling or summarization
- DebugMap lookup must be bounded
- disabled trace or info paths should avoid allocation where practical
- subsystem code must not directly emit expensive Unity logs in hot paths

Required marker family includes at least:

- `Diagnostics.Report`
- `Diagnostics.Format`
- `Diagnostics.LookupDebugMap`
- `Diagnostics.EmitSink`

This section must remain consistent with [11_DebugMapAndDiagnosticsSpec.md](11_DebugMapAndDiagnosticsSpec.md).

---

## Unity Authoring / Editor Generation Performance Budget

`EditorGenerationPath` may scan authoring data.
It must still be deterministic and preferably incremental.

Rules:

- no repeated full-project scan on every minor inspector change
- extraction must support full regeneration
- extraction must support headless or CI execution
- source hashes or equivalent invalidation data should be used for incremental generation
- performance optimization must not skip validation
- authoring diagnostics cost must remain measurable

Required marker family includes at least:

- `AuthoringBridge.CollectRoots`
- `AuthoringBridge.Extract`
- `AuthoringBridge.Normalize`
- `AuthoringBridge.Validate`
- `AuthoringBridge.PrepareDirectPlay`
- `AuthoringBridge.FullRegenerate`

This section must remain consistent with [12_UnityAuthoringBridgeSpec.md](12_UnityAuthoringBridgeSpec.md).

---

## Legacy Compatibility Performance Boundary

Legacy compatibility must not be on target hot runtime paths by default.

Forbidden in target runtime:

- legacy resolver fallback
- legacy feature discovery scan
- legacy command string lookup per execution
- legacy value stable-key fallback per read or write
- legacy handler scan per acquire or tick
- legacy adapter conversion per-frame on hot paths

Allowed only when explicit, measured, and outside target hot paths:

- migration tooling
- comparison tests
- bounded development-only diagnostics for allowed adapters

This section must remain consistent with [13_LegacyCompatBoundarySpec.md](13_LegacyCompatBoundarySpec.md).

---

## Profiler Marker Policy

Every budgeted runtime operation must have a profiler marker or equivalent diagnostic measurement point.

Canonical marker naming shape:

```text
<Family>.<Operation>
```

Required marker families include at least:

- `KernelBoot.*`
- `ServiceGraph.*`
- `ScopeGraph.*`
- `Lifecycle.*`
- `CommandCatalog.*`
- `Command.*`
- `ValueStore.*`
- `RuntimeQuery.*`
- `Diagnostics.*`
- `AuthoringBridge.*`
- `LegacyCompat.*`

Representative required markers:

- `KernelBoot.LoadInputs`
- `KernelBoot.ValidateHashes`
- `KernelBoot.CreateRuntime`
- `ServiceGraph.Build`
- `ServiceGraph.Resolve`
- `ScopeGraph.CreateScope`
- `ScopeGraph.ValidateHandle`
- `Lifecycle.DispatchAcquire`
- `Lifecycle.DispatchRelease`
- `Lifecycle.DispatchTick`
- `CommandCatalog.Lookup`
- `Command.Execute`
- `ValueStore.Read`
- `ValueStore.Write`
- `ValueStore.ApplyInitPlan`
- `RuntimeQuery.Lookup`
- `Diagnostics.Report`
- `AuthoringBridge.Extract`
- `LegacyCompat.AdapterCall`

Missing markers for budgeted paths are performance-visibility defects.

---

## Benchmark Fixture Policy

Performance tests must use stable fixtures.

Each benchmark fixture must define:

- fixture ID
- subsystem
- operation
- dataset size
- profile
- expected path kind
- expected allocation
- expected complexity
- timeout or threshold
- warm or cold start policy
- deterministic seed when relevant

Representative fixture sizes:

```text
Small:
  10 services / 10 scopes / 20 commands / 50 values

Medium:
  100 services / 1,000 scopes / 500 commands / 5,000 values

Large:
  300 services / 10,000 scopes / 2,000 commands / 50,000 values
```

Benchmarks must declare whether caches are prewarmed, cold, or mixed.

---

## Regression Test Policy

Performance regression tests must detect at least:

- new allocations in hot paths
- broad scan reintroduction
- command executor eager construction
- lifecycle handler scanning
- service resolve complexity regression
- `ValueStore` stable-key lookup regression
- direct Unity error logging regression outside the central sink
- missing profiler marker regression

Performance tests must not rely only on wall-clock time.
They should also assert structural behavior:

- no allocation
- no forbidden API
- no scan count
- no fallback path
- no eager construction count
- no direct Unity logging outside the central sink

---

## Performance Report Format

Performance reports must include:

- test ID
- subsystem
- operation
- fixture size
- profile
- elapsed time
- allocation
- call count
- marker samples
- pass or fail
- regression baseline when available

Recommended output formats:

```text
Logs/TestRuns/<timestamp>/PerformanceReport.json
Logs/TestRuns/<timestamp>/PerformanceReport.md
```

Reports must preserve enough structure to compare runs over time.

---

## Failure Policy

Performance budget violations must be treated as test failures in `Test` profile.

Representative stable diagnostics codes:

- `PERF_HIERARCHY_SCAN_FORBIDDEN`
- `PERF_TRANSFORM_PARENT_INFERENCE_FORBIDDEN`
- `PERF_RESOURCES_FALLBACK_FORBIDDEN`
- `PERF_REGISTRATION_SCAN_FORBIDDEN`
- `PERF_INTERFACE_SCAN_FORBIDDEN`
- `PERF_HOT_PATH_ALLOCATION_FORBIDDEN`
- `PERF_MISSING_PROFILER_MARKER`
- `PERF_PER_ENTITY_TICK_FORBIDDEN`
- `PERF_LEGACY_STABLE_KEY_FALLBACK_FORBIDDEN`
- `PERF_DIRECT_UNITY_LOG_FORBIDDEN`

Severity policy:

| Failure Type | Default Severity |
|---|---|
| forbidden runtime operation | Error or Fatal |
| hot-path allocation regression | Error |
| missing profiler marker | Warning or Error depending on subsystem criticality |
| benchmark threshold regression | Error |
| legacy hot-path use | Error |

Performance failure must not be hidden as a warning when it violates a forbidden runtime rule.

---

## Forbidden Patterns

The following are forbidden in target runtime performance rules:

- `GetComponentsInChildren` for kernel discovery
- `GetComponentsInParent` for kernel ownership inference
- `Transform.parent` traversal for scope ownership
- `FindObjectsByType` for required kernel runtime lookup
- `Resources.Load` fallback for required kernel data
- registration scan for lifecycle handlers
- interface scan for lifecycle enrollment
- `IReadOnlyList<ICommandExecutor>` discovery as runtime command enumeration truth
- runtime stable-key lookup for `ValueStore`
- command dispatch by string key
- reflection constructor injection in target runtime hot or warm paths
- `Activator.CreateInstance` fallback in target runtime paths
- LINQ in hot paths
- `List`, `HashSet`, or `ToArray` allocation in normal hot dispatch
- `Debug.LogError` outside the central diagnostics sink in target kernel paths
- per-entity `ServiceGraph` service creation by default
- per-entity lifecycle tick step by default

---

## Test Case Model

Each performance or runtime-rule test case must define:

- Test ID
- Title
- Subsystem
- runtime path kind
- fixture size
- operation
- expected complexity
- expected allocation
- forbidden APIs
- expected diagnostics
- expected profiler markers

---

## Required Test Cases

### A. Forbidden API Tests

#### TC_PERF_FORBID_001_NoGetComponentsInChildrenForRuntimeDiscovery

```text
Input:
- target runtime boot or scope create

Expected:
- no GetComponentsInChildren used for kernel discovery
- PERF_HIERARCHY_SCAN_FORBIDDEN if detected
```

#### TC_PERF_FORBID_002_NoTransformParentScopeInference

```text
Input:
- scope ownership resolution

Expected:
- no Transform.parent traversal
- PERF_TRANSFORM_PARENT_INFERENCE_FORBIDDEN
```

#### TC_PERF_FORBID_003_NoResourcesLoadFallback

```text
Input:
- missing required artifact

Expected:
- boot fails
- no Resources.Load fallback
```

### B. ServiceGraph Tests

#### TC_PERF_SERVICE_001_ResolveNoAllocation

```text
Input:
- ServiceGraph with 300 services

Operation:
- resolve a hot service repeatedly

Expected:
- 0 B allocation in normal path
- marker ServiceGraph.Resolve emitted
```

#### TC_PERF_SERVICE_002_ResolveDoesNotScanAllServices

```text
Input:
- ServiceGraph with many services

Operation:
- resolve one service

Expected:
- lookup does not scale with total service count
```

### C. ScopeGraph Tests

#### TC_PERF_SCOPE_001_HandleValidationNoAllocation

```text
Input:
- 10,000 scopes

Operation:
- validate handles repeatedly

Expected:
- 0 B allocation
- O(1) handle validation
```

#### TC_PERF_SCOPE_002_ChildEnumerationScalesWithChildCount

```text
Input:
- 10,000 scopes
- one parent has 8 children

Operation:
- enumerate that parent children

Expected:
- cost scales with 8 children, not 10,000 scopes
```

### D. Lifecycle Tests

#### TC_PERF_LIFE_001_TickDispatchNoRegistrationScan

```text
Input:
- lifecycle plan with tick dispatch table

Operation:
- dispatch Tick

Expected:
- no service registration scan
- no interface scan
- no List/HashSet/ToArray allocation
```

#### TC_PERF_LIFE_002_PerEntityTickRejected

```text
Input:
- 10,000 entity tick steps

Expected:
- validation failed
- PERF_PER_ENTITY_TICK_FORBIDDEN
```

### E. Command Tests

#### TC_PERF_CMD_001_CommandLookupNoScan

```text
Input:
- 2,000 command types

Operation:
- dispatch one command

Expected:
- no scan over all command types
- no scan over all executors
```

#### TC_PERF_CMD_002_NoEagerExecutorConstruction

```text
Input:
- 2,000 command types

Operation:
- boot CommandCatalog

Expected:
- executor metadata loaded
- executors not all constructed
```

### F. ValueStore Tests

#### TC_PERF_VALUE_001_ScalarReadNoAllocation

```text
Input:
- ValueStore with 50,000 values

Operation:
- repeated scalar read by ValueKeyId

Expected:
- 0 B allocation
- no stableKey lookup
```

#### TC_PERF_VALUE_002_ScalarWriteNoAllocation

```text
Input:
- ValueStore with scalar values

Operation:
- repeated scalar write

Expected:
- 0 B allocation
- revision increments
```

### G. Diagnostics Tests

#### TC_PERF_DIAG_001_DisabledTraceNoFormattingCost

```text
Input:
- Trace diagnostics disabled

Operation:
- report trace diagnostic repeatedly

Expected:
- no expensive string formatting
- minimal or zero allocation
```

#### TC_PERF_DIAG_002_RepeatedErrorThrottled

```text
Input:
- same error repeated 1,000 times

Expected:
- first occurrence emitted
- repeated occurrences summarized
- failure boundary preserved
```

### H. Legacy Tests

#### TC_PERF_LEGACY_001_LegacyResolverNotOnHotPath

```text
Input:
- v2 ServiceGraph resolve

Expected:
- no call to legacy RuntimeResolver
```

#### TC_PERF_LEGACY_002_LegacyStableKeyFallbackRejected

```text
Input:
- ValueStore read attempts legacy stableKey fallback

Expected:
- failed
- PERF_LEGACY_STABLE_KEY_FALLBACK_FORBIDDEN
```

---

## Acceptance Criteria

This specification is complete when it defines:

- runtime path classification
- performance budget philosophy
- global runtime rules
- forbidden runtime operations
- allocation policy
- reflection and type lookup policy
- Unity API usage policy
- scene and hierarchy scan policy
- string key and runtime ID lookup policy
- generated table and slot lookup policy
- boot performance budget
- `ServiceGraph` performance budget
- `ScopeGraph` performance budget
- lifecycle performance budget
- `CommandCatalog` performance budget
- `ValueStore` performance budget
- `RuntimeQuery` performance budget
- diagnostics performance budget
- Unity authoring and editor generation budget
- legacy compatibility performance boundary
- profiler marker policy
- benchmark fixture policy
- regression test policy
- performance report format
- failure policy
- forbidden patterns
- required test cases

The specification is not complete if target runtime performance still depends on broad scans, hidden allocation, string-key lookup, reflection discovery, fallback repair, or unmeasured hot paths.

---

## Final Position

Runtime performance is enforced by architecture rules, not recovered by late optimization.

If a target runtime path needs a broad scan, hidden allocation, string lookup, reflection discovery, or fallback repair, the design is wrong by default.

14 is not advice for later tuning.
It is the rule set that keeps Kernel v2 from regressing into the hidden runtime costs of the legacy architecture.

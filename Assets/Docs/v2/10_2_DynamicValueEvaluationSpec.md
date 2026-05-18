# DynamicValue Evaluation Specification

## Document Status

- Document ID: `10_2_DynamicValueEvaluationSpec`
- Status: Draft
- Role: defines the DynamicValue authoring/runtime wrapper contract, DynamicEvaluation and ReactiveEvaluation plan semantics, evaluation tracking, cache, invalidation, and evaluation diagnostics for Kernel v2
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
  - [11_DebugMapAndDiagnosticsSpec.md](11_DebugMapAndDiagnosticsSpec.md)
- Provides foundation for:
  - 12_UnityAuthoringBridgeSpec.md
  - 14_PerformanceBudgetAndRuntimeRulesSpec.md
  - 15_TestAndValidationSpec.md

### Revision Note

This revision inserts 10-2 between 10 and 11 as the owner of DynamicValue evaluation semantics.

It preserves `SerializeReference`-based authoring while moving tracker, cache, invalidation, and nested-dependency rules out of ad hoc source-local code.

It also defines the architecture direction for replacing scattered dirty flags, per-source version checks, and hidden nested `DynamicValue` reevaluation with one explicit evaluation runtime contract.

---

## Ownership

This specification owns:

- `DynamicValue` runtime-wrapper boundary
- `IDynamicSource` evaluation contract
- evaluation context contract for dynamic sources
- `DynamicEvaluationPlan` semantics
- `ReactiveEvaluationPlan` semantics
- dependency declaration modes for dynamic and reactive evaluation
- tracked evaluation model
- evaluation tracker service boundary
- shared evaluation cache ownership rules
- invalidation and dependency-stamp policy
- nested `DynamicValue` dependency capture requirements
- hot-path evaluation policy
- evaluation-specific diagnostics provenance and failure behavior
- migration of current DynamicValue, expression, and deferred dynamic patterns

This specification does not own:

- `ValueSchema`, `ValueStore`, or save metadata layout
- runtime query semantics
- service graph semantics
- scope graph semantics
- lifecycle ordering semantics
- command execution semantics
- final editor UI or inspector picker layout
- source-specific gameplay business rules
- DebugMap generation algorithms
- final binary serialization format for generated plans

10-2 owns evaluation semantics.
It must not re-own value-state semantics already owned by 10.

---

## Purpose

This specification defines how verified dynamic sources are evaluated, tracked, invalidated, cached, and diagnosed.

Core statements:

```text
DynamicValue preserves SerializeReference authoring.
It does not own hot-path cache state.

DynamicEvaluationPlan declares explicit one-shot or phase-bound evaluation.

ReactiveEvaluationPlan declares tracked recomputation, cache ownership, and invalidation behavior.

ValueStore revisions are inputs to evaluation.
They do not themselves form a reactive graph.

Nested DynamicValue reads inside formula or composite sources must be captured by the shared tracker model.
```

The target runtime must not rely on ad hoc reevaluation loops, source-local result caches, hidden dependency discovery, or silent fallback defaults to make dynamic values usable.

---

## Scope

This specification defines:

- DynamicValue authoring/runtime boundary
- source evaluation contract
- evaluation context contract
- dynamic and reactive plan model
- dependency declaration model
- tracker and shared cache model
- invalidation and revision consumption rules
- nested evaluation rules
- phase and scheduling rules
- hot-path rules
- diagnostics and failure policy
- legacy migration policy
- forbidden patterns
- required test cases

This specification intentionally does not define:

- concrete `ValueStore` slot layout
- concrete save payload format
- final runtime query index implementation
- final expression grammar
- rich-text rendering details
- source-by-source gameplay semantics
- final Unity inspector drawer implementation
- final generated C# API signatures

---

## Non-Goals

This specification does not define:

- the final concrete class names of every runtime evaluation service
- the final memory layout for every cache entry
- the final source normalization algorithm
- the final authoring UI for `SerializeReference` source selection
- the business semantics of actor, status-effect, rich-text, random, or transform sources
- the final editor-only validation UI

This specification must not become a source catalog or an expression-language specification.
It defines the evaluation layer contract.

---

## Relationship to Other Specs

| Spec | Relationship |
|---|---|
| [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md) | Defines the root rule that dynamic or reactive evaluation must be explicit and must not be hidden inside generic initialization. |
| [01_KernelIRSpec.md](01_KernelIRSpec.md) | Defines the typed identities and normalized source structure that evaluation plans and dependency references must use. |
| [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md) | Declares `DynamicEvaluationContribution` and `ReactiveEvaluationContribution` as declarative inputs to KernelIR normalization. |
| [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md) | Generates `DynamicEvaluationPlan` and `ReactiveEvaluationPlan` as verified artifacts. |
| [04_DependencyValidationSpec.md](04_DependencyValidationSpec.md) | Validates explicit dependencies, phase legality, and invalidation declarations before evaluation plans may be trusted. |
| [05_BootManifestAndProfileSpec.md](05_BootManifestAndProfileSpec.md) | Boots only from verified evaluation artifacts and profile-compatible tracker/cache policy. |
| [06_ServiceGraphRuntimeSpec.md](06_ServiceGraphRuntimeSpec.md) | May host the runtime services that execute and track evaluation, but does not own evaluator semantics. |
| [07_ScopeGraphRuntimeSpec.md](07_ScopeGraphRuntimeSpec.md) | Owns scope lifetime, runtime query, and scope identity inputs consumed by evaluation plans. |
| [08_LifecyclePlanSpec.md](08_LifecyclePlanSpec.md) | Owns lifecycle ordering; 10-2 defines what evaluation phases mean and what data they require. |
| [09_CommandCatalogRuntimeSpec.md](09_CommandCatalogRuntimeSpec.md) | Owns command execution and `CommandLocal` lifetime; 10-2 consumes command-frame context for dynamic evaluation. |
| [10_ValueSchemaAndStoreSpec.md](10_ValueSchemaAndStoreSpec.md) | 10 owns `ValueSchema`, `ValueStore`, revisions, dirty signals, and value-state boundaries; 10-2 owns evaluator, tracker, cache, invalidation, and nested dependency capture semantics that consume those signals. |
| [11_DebugMapAndDiagnosticsSpec.md](11_DebugMapAndDiagnosticsSpec.md) | 11 owns the shared diagnostics substrate and DebugMap runtime contract; 10-2 defines evaluation-specific provenance and failure behavior emitted through that substrate. |
| 12_UnityAuthoringBridgeSpec.md | Will produce authoring inputs whose serialized dynamic sources must normalize into verified evaluation artifacts. |
| 14_PerformanceBudgetAndRuntimeRulesSpec.md | Will define compile, cache hit/miss, invalidation, and reevaluation budgets for the evaluation runtime. |
| 15_TestAndValidationSpec.md | Will turn the required tests in this document into executable validation and CI coverage. |

10-2 is the owner of DynamicValue evaluation semantics.
It must not leave tracker, cache, or invalidation policy ownerless.

---

## Current Dynamic Evaluation Debt Observations

この節は現行コードベースの dynamic evaluation 負債の観測結果をまとめる。
ここは target policy ではなく、移行元の整理である。

### Observation Traceability

| Observation | Evidence Type | Target Pressure |
|---|---|---|
| `DynamicValue` evaluates the source again on each direct read by default. | Source | Shared tracked cache and explicit hot-path API |
| Expression sources keep private `_dirty` compile flags and source-local caches. | Source | Split compile cache from result cache and invalidation policy |
| `IExpressionSource.GetDependentKeys()` covers only a subset of runtime dependencies. | Source | Hybrid static plus tracked dependency declaration |
| `ActorSourceResolveCache` optimizes only specific source families. | Source | Generic cache and invalidation contract for hot paths |
| Composite sources evaluate nested `DynamicValue` instances without a shared dependency graph. | Source | Nested dependency capture and tracker propagation |
| Version and revision detection is scattered across helpers and source-local code. | Source | Unified dependency-stamp model |
| Evaluation loggers still participate in final host-output decisions. | Source | Shared diagnostics substrate with evaluation provenance |

### Representative Anchors

- [IDynamicSource.cs](../../GameLib/Script/Common/Variables/Dynamic/Core/IDynamicSource.cs) - current evaluation entry point and context shape
- [DynamicValue.cs](../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicValue.cs) - wrapper and typed convenience API
- [DynamicVariant.cs](../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicVariant.cs) - unified evaluation result carrier
- [DynamicValueResolver.cs](../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicValueResolver.cs) - resolver-centric context helper
- [IExpressionSource.cs](../../GameLib/Script/Common/Variables/Dynamic/Expression/IExpressionSource.cs) - current expression-only dependency list surface
- [FloatExpressionSource.cs](../../GameLib/Script/Common/Variables/Dynamic/Expression/FloatExpressionSource.cs) - compile cache and dirty-flag example
- [ActorSourceFastResolver.cs](../../GameLib/Script/Common/Commands/VNext/Core/ActorSourceFastResolver.cs) - current source-family-specific hot-path cache
- [WeightedRandomListSources.cs](../../GameLib/Script/Common/Variables/Dynamic/Sources/WeightedRandomListSources.cs) - nested `DynamicValue` composition example
- [RichTextSource.cs](../../GameLib/Script/Common/Variables/Dynamic/RichText/RichTextSource.cs) - composite evaluation example
- [StatusEffectStackDescriptionSource.cs](../../GameLib/Script/Common/Variables/Dynamic/StatusEffect/StatusEffectStackDescriptionSource.cs) - nested evaluation and temporary local state example
- [ExpressionRuntimeLogger.cs](../../GameLib/Script/Common/Variables/Dynamic/Expression/ExpressionRuntimeLogger.cs) - evaluation logging debt example

### Current Gaps

The current project still exposes the following gaps that 10-2 must close:

- direct reevaluation remains the default runtime behavior
- dynamic result caching does not have one owner
- nested child reads are not captured by one shared dependency model
- runtime invalidation still depends on source-local assumptions
- explicit evaluation phase semantics are not centralized
- evaluation failure often degrades to implicit default values
- hot-path actor and runtime-query caching is not generalized

---

## Evaluation Architecture Definition

Target evaluation architecture is split into five concepts:

1. `DynamicValue` is the authoring/runtime wrapper.
2. `IDynamicSource` is the normalized source contract.
3. `DynamicEvaluationPlan` is the explicit one-shot or phase-bound evaluation contract.
4. `ReactiveEvaluationPlan` is the explicit tracked recomputation and cache contract.
5. `DynamicEvaluationRuntime` is the tracker and shared-cache subsystem that executes those plans.

These concepts must not be collapsed into one convenience API, one source-local cache, or one generalized `ValueStore` helper.

Pipeline:

```text
DynamicEvaluationContribution
  -> DynamicEvaluationIR
  -> DynamicEvaluationPlan

ReactiveEvaluationContribution
  -> ReactiveEvaluationIR
  -> ReactiveEvaluationPlan

Runtime:
  DynamicEvaluationRuntime consumes plans,
  ValueStore revisions,
  RuntimeQuery invalidation,
  scope and command context,
  and source-configuration revisions.
```

`DynamicEvaluationRuntime` may apply writes, produce cached computed values, or serve explicit read requests.
It must not discover structure or invent missing dependencies at runtime.

---

## SerializeReference and Authoring Surface

`DynamicValue` authoring must continue to support `SerializeReference`-based polymorphic source fields.

Allowed direction:

- a field may store one normalized `IDynamicSource` implementation
- a typed wrapper such as `DynamicValue<T>` may exist for authoring convenience
- source-type picker expansion and nested field editing may remain editor-facing authoring features

Forbidden direction:

- relying on reflection, runtime type-name matching, or editor-only type discovery in hot runtime evaluation
- treating the serialized source instance as the final runtime truth without normalization or verification when plan generation is required
- storing long-lived cross-scope tracker state inside the serialized wrapper

Explanatory model:

```csharp
public struct DynamicValue
{
    [SerializeReference]
    IDynamicSource? Source;
}
```

The wrapper shape may remain lightweight.
The evaluation runtime owns tracking and cache behavior.

---

## IDynamicSource Contract

`IDynamicSource` is the normalized evaluation root for one source graph.

The target contract must support:

- static metadata sufficient for diagnostics and normalization
- dependency-description handoff when static declaration is possible
- evaluation against an explicit context
- deterministic local invalidation for authoring or source-configuration changes

Explanatory model:

```csharp
public interface IDynamicSource
{
    DynamicSourceKind SourceKind { get; }
    void DescribeDependencies(IDynamicDependencyCollector collector, in DynamicDependencyDescribeContext context);
    DynamicVariant Evaluate(in DynamicEvaluationContext context);
}
```

Rules:

- `Evaluate` must be side-effect free unless the plan explicitly declares output application semantics elsewhere.
- direct `ValueStore` mutation from inside arbitrary source evaluation is forbidden.
- source-local compile caches are allowed when they cache deterministic source configuration or AST state.
- source-local computed result caches must not be the primary architecture for hot-path reuse, invalidation, or nested dependency truth.
- nested `DynamicValue` or child-source calls must use the same dependency sink or tracker scope as the parent evaluation.

This specification does not freeze the exact API signature shown above.
It fixes the responsibilities the contract must carry.

---

## Evaluation Context Contract

The current resolver-centric `IDynamicContext` surface is insufficient as the final architecture contract.

The target evaluation context must make runtime inputs explicit.

Required context categories:

- value read access by `ValueKeyId`
- runtime-query access
- current scope identity and command-root identity when applicable
- command-local access when applicable
- dependency sink or tracker interface
- diagnostics sink or compatible error-reporting interface
- evaluation phase and profile metadata

Explanatory model:

```csharp
public readonly ref struct DynamicEvaluationContext
{
    public readonly DynamicEvaluationPhase Phase;
    public readonly IValueReadContext Values;
    public readonly IRuntimeQueryReadContext RuntimeQuery;
    public readonly IDynamicDependencySink DependencySink;
    public readonly IDiagnosticEmitter Diagnostics;
    public readonly ScopeHandle Scope;
    public readonly ScopeHandle? CommandRootScope;
}
```

Rules:

- runtime-query access must use explicit validated runtime-query inputs, not hidden hierarchy traversal.
- command-local access must be legal only inside declared command phases.
- missing required context input is an evaluation failure, not a reason to silently substitute a default value.
- context may expose convenience helpers, but those helpers must not reintroduce fallback discovery.

---

## DynamicValue Wrapper Contract

`DynamicValue` is a wrapper.
It is not the owner of cross-frame cache state.

Allowed wrapper responsibilities:

- hold a serialized source reference
- provide typed or untyped convenience access for low-frequency paths
- carry local authoring defaults or null-state semantics when explicitly declared

Forbidden wrapper responsibilities:

- owning a global or cross-scope tracked cache
- owning hidden revision counters that substitute for the shared dependency-stamp model
- silently masking required evaluation failures through `GetOrDefault`-style fallback in target runtime paths
- serving as the only runtime access layer for hot-path repeated reads

Hot-path runtime must use an explicit evaluation-runtime API that can track dependencies and cache hits.

---

## DynamicVariant and Type Interop Boundary

10 owns value kinds and schema legality.
10-2 owns how evaluation results map to those schema-backed outputs.

Rules:

- evaluated result kinds must be representable against the target `ValueKind` contract defined by 10
- runtime-only hidden result kinds that cannot be validated or diagnosed are forbidden
- schema-incompatible evaluation output is an evaluation failure
- silent coercion is forbidden unless the plan or schema explicitly declares the conversion policy

Evaluation must not introduce a second shadow type system beside the value schema.

---

## DynamicEvaluationPlan Model

`DynamicEvaluationPlan` defines explicit one-shot or phase-bound evaluation.

Explanatory model:

```csharp
public sealed class DynamicEvaluationPlan
{
    public DynamicEvaluationPlanId PlanId;
    public DynamicSourceHandle RootSource;
    public ValueKeyId OutputKeyId;
    public ValueStoreScopeId OutputStoreScope;
    public DynamicEvaluationPhase Phase;
    public DynamicFallbackPolicy FallbackPolicy;
    public DynamicDependencyDeclarationMode DependencyMode;
    public SourceLocationId Source;
}
```

`DynamicEvaluationPlan` must define:

- plan identity
- root source reference or equivalent normalized source payload
- output target or output contract
- allowed runtime inputs
- evaluation phase
- fallback policy
- failure boundary
- diagnostics provenance
- tracker/cache participation

`DynamicEvaluationPlan` is valid for:

- init-time dynamic writes
- acquire-time dynamic writes
- command-bound one-shot evaluation
- explicit on-demand computations whose inputs and outputs are declared

`DynamicEvaluationPlan` is not valid for:

- hidden per-frame polling
- implicit store writes from generic getters
- source-specific ad hoc cache tables

---

## ReactiveEvaluationPlan Model

`ReactiveEvaluationPlan` defines tracked recomputation and shared-cache behavior.

Explanatory model:

```csharp
public sealed class ReactiveEvaluationPlan
{
    public ReactiveEvaluationPlanId PlanId;
    public DynamicSourceHandle RootSource;
    public ReactiveEvaluationTarget Target;
    public ReactiveSchedulingPolicy Scheduling;
    public DynamicDependencyDeclarationMode DependencyMode;
    public DynamicInvalidationPolicy Invalidation;
    public DynamicCachePolicy CachePolicy;
    public SourceLocationId Source;
}
```

`ReactiveEvaluationPlan` owns:

- tracked computed values
- cache entry ownership
- invalidation sources
- recompute scheduling
- output apply policy

`ReactiveEvaluationPlan` must not degrade into generic poll-every-frame behavior unless that schedule is explicitly declared and budgeted by 14.

---

## Dependency Declaration Model

Dynamic and reactive evaluation must use explicit dependency declaration modes.

Allowed dependency classes:

- `ValueDependency`: `ValueKeyId` plus store-scope input
- `RuntimeQueryDependency`: validated runtime-query identity and target policy
- `ScopeDependency`: current scope, command-root scope, or explicit other-scope input
- `CommandLocalDependency`: command-frame-local value input
- `NestedEvaluationDependency`: child `DynamicValue` or child evaluation-plan edge
- `SourceConfigurationDependency`: source configuration or compile-cache revision input

Allowed declaration modes:

- `Static`: all dependencies are known without execution
- `Tracked`: dependencies are discovered through shared read tracking during execution
- `Hybrid`: static seed dependencies plus tracked nested reads

Rules:

- `Static` is valid only when the dependency set is fully knowable before runtime execution.
- `Tracked` is required for formula, expression, or composite sources whose actual reads depend on runtime branches or child evaluation.
- `Hybrid` is valid when static root dependencies exist but nested or conditional reads still require tracking.
- `IExpressionSource.GetDependentKeys()` may remain as an authoring or validation hint, but it must not be the only runtime invalidation truth.
- creating temporary `DynamicValue` instances inside formula or composite evaluation does not exempt them from dependency capture.

---

## Tracker and Evaluation Cache Model

Target runtime must use an explicit tracker and shared-cache subsystem.

This subsystem owns:

- evaluation instance keys
- dependency read capture
- dependency-snapshot materialization
- cache lookup by plan and runtime origin
- cached result revision
- invalidation propagation
- cycle detection
- degradation reporting

Explanatory model:

```csharp
public sealed class TrackedEvaluationEntry
{
    public DynamicPlanInstanceId InstanceId;
    public DynamicDependencyStamp DependencyStamp;
    public DynamicVariant CachedValue;
    public uint ResultRevision;
}
```

Rules:

- cache keys must include plan identity and runtime origin identity sufficient to prevent cross-scope contamination.
- cache hit on the normal hot path must not allocate managed memory.
- nested evaluation reads must merge into the parent dependency snapshot.
- source-local compile caches are allowed, but source-local computed result caches are subordinate to the shared tracker/cache model.
- cache ownership may be per-scope, per-command-frame, or shared, but the lifetime must be explicit in plan or profile policy.

The architecture target is one generic cache system, not dozens of unrelated source-family caches.

---

## Revision and Invalidation Model

10 owns revisions and dirty signals for value state.
10-2 owns how evaluation consumes them.

Target invalidation must use a unified dependency-stamp model.

`DynamicDependencyStamp` may include:

- value-slot or store revisions from 10
- runtime-query generation or invalidation tokens from 07
- scope-instance generation or attach-detach generation
- command-frame generation for command-local inputs
- source-configuration revision
- child evaluation result revision
- explicit time or nondeterministic-input token when declared by plan

Rules:

- ad hoc source-local version checks must not be the long-term invalidation architecture.
- actor-resolution helpers and similar micro-caches may optimize lookup, but invalidation truth must come from declared dependency stamps or runtime-query tokens.
- source configuration changes must invalidate both compile caches and tracked results deterministically.
- implicit default-value fallback after invalidation failure is forbidden unless explicitly declared by policy.

---

## Composite and Nested Source Rules

Composite sources must forward dependency tracking through nested evaluation.

This applies to sources such as:

- weighted lists
- split-vector builders
- rich-text sources
- status-effect description sources
- formula or expression sources with external variables

Rules:

- any source that evaluates a child `DynamicValue` must propagate the active dependency sink or tracker scope.
- temporary or internally created `DynamicValue` variables are part of the same evaluation graph.
- formula or expression variable expansion must capture reads from both direct identifiers and nested external variables.
- random, timer, or other nondeterministic sources must declare their nondeterministic input explicitly or be rejected for tracked evaluation.
- composite-source result caching must be owned by the shared tracker/cache subsystem, not hidden inside the source instance.

This rule exists so that formula-internal or composite-internal `DynamicValue` changes are not missed.

---

## Evaluation Timing and Scheduling

Evaluation timing must be explicit.

Allowed evaluation timing examples:

- `Init`
- `Acquire`
- `CommandExecute`
- `CommandWaitResume`
- `Tick`
- `ExplicitRead`
- `TestFixture`

Rules:

- each plan must declare its allowed phase or scheduling mode.
- a phase mismatch is a validation error or runtime rejection, not a reason to silently execute elsewhere.
- reactive recompute may happen on dependency change, on explicit read, or on a declared scheduled phase.
- poll-every-frame mode is allowed only when explicitly declared and budgeted.

08 owns phase ordering.
10-2 owns what evaluation means within a declared phase.

---

## Hot-Path Policy

Dynamic evaluation is a runtime hot path.

Target requirements:

- no raw stable-key lookup in evaluation hot paths
- no broad hierarchy or registry search for undeclared actor or scope discovery
- no reflection in evaluation hot paths
- no LINQ in evaluation hot paths
- no direct Unity logging in evaluation hot paths
- cache hit must avoid managed allocation in the normal path
- cache miss, hit, stale, and recompute paths must be measurable by 14
- source-family-specific micro-caches may remain only as subordinate optimizations under the shared invalidation contract

Repeated direct `DynamicValue.Evaluate()` calls without tracker participation are forbidden as the long-term hot-path architecture.

---

## Diagnostics and DebugMap Requirements

11 owns the shared diagnostics substrate.
10-2 defines the minimum evaluation-specific provenance that must be emitted through that substrate.

Required evaluation diagnostics fields:

- stable diagnostic code
- plan identity
- root source kind or source handle
- source location or authoring provenance
- output `ValueKeyId` or computed target identity when applicable
- store scope and runtime origin
- dependency summary or invalidation reason
- cache state
- fallback policy
- failure boundary

Representative stable codes:

- `DYN_EVAL_PLAN_MISSING`
- `DYN_EVAL_DEPENDENCY_UNDECLARED`
- `DYN_EVAL_TYPE_MISMATCH`
- `DYN_EVAL_CYCLE_DETECTED`
- `DYN_TRACKER_DEGRADED`
- `DYN_CACHE_POLICY_VIOLATION`
- `DYN_FALLBACK_FORBIDDEN`

Source implementations and helper utilities must not decide final Unity host output.
They emit structured diagnostics only.

---

## Failure Policy

Evaluation failure boundaries must be explicit.

| Failure Type | Default Boundary |
|---|---|
| Missing required `DynamicEvaluationPlan` | Validation failure or boot failure |
| Missing required `ReactiveEvaluationPlan` | Validation failure or boot failure |
| Missing declared runtime input during evaluation | Operation failure |
| Dependency cycle detected | Evaluation failure |
| Result type incompatible with target schema | Operation failure |
| Tracker degradation in Development or Test profile | Error and plan disablement unless explicitly tolerated |
| Cache policy violation | Operation or scope failure |
| Undeclared fallback path required to continue | Validation failure or operation failure |

Returning `0`, `false`, empty string, or `Null` as an implicit substitute for a required evaluation failure is forbidden unless the fallback policy explicitly declares that behavior and 04 validates it.

---

## Legacy Migration Policy

| Legacy Pattern | Target Representation |
|---|---|
| direct `DynamicValue.Evaluate(context)` hot-path polling | `DynamicEvaluationRuntime` tracked or phase-bound evaluation |
| expression-source private `_dirty` result invalidation | source-configuration revision plus shared tracker invalidation |
| `IExpressionSource.GetDependentKeys()` as runtime invalidation truth | authoring or validation hint plus tracked dependency capture |
| `ActorSourceResolveCache` as primary invalidation model | subordinate micro-cache under declared runtime-query invalidation |
| `DeferredDynamicVarValue` | `DynamicEvaluationContribution` and `DynamicEvaluationPlan` |
| nested `DynamicValue` inside weighted or composite sources | tracker-aware nested dependency capture |
| rich-text or status-effect composite reevaluation | explicit composite-source contract under shared tracker and cache |
| direct evaluation loggers choosing final host output | structured diagnostics emitted through 11 |

Migration must preserve authoring shape only where it does not preserve hidden runtime behavior.
`SerializeReference` authoring stays.
Ad hoc runtime invalidation and silent fallbacks do not.

---

## Forbidden Patterns

The following are forbidden in target DynamicValue evaluation runtime:

- repeated hot-path `DynamicValue.Evaluate()` polling without tracker participation
- source-local version counters used as cross-source invalidation truth
- source-local computed result caches that bypass the shared tracker/cache system
- hidden child `DynamicValue` creation without dependency capture
- implicit fallback through `GetOrDefault`, zero, false, empty string, or `Null` for required outputs
- runtime stable-key lookup for dependency resolution
- unbounded poll-every-frame reactive evaluation without explicit schedule and budget
- direct Unity logging in evaluator, source, or helper paths
- reflection or LINQ in evaluation hot paths
- runtime discovery used to repair missing declared dependencies

---

## Test Case Model

Each DynamicValue evaluation test case must define:

- Test ID
- Title
- plan fixture
- source fixture
- `ValueStore` fixture when applicable
- tracker or cache fixture when applicable
- runtime context fixture
- operation
- expected dependency snapshot or invalidation event
- expected diagnostics
- expected cache behavior
- expected allocation or performance assertion when applicable

---

## Required Test Cases

### Plan and Phase Tests

#### TC_DYNAMIC_PLAN_001_RuntimeContextWriteRequiresPlan

```text
Input:
- init data uses runtime-context-dependent DynamicValue
- no DynamicEvaluationPlan

Expected:
- Failed
- DYN_EVAL_PLAN_MISSING
```

#### TC_DYNAMIC_PLAN_002_PhaseDeclaredAndValid

```text
Input:
- DynamicEvaluationPlan declares Acquire phase
- runtime executes in Acquire

Expected:
- Passed
```

### Tracker and Nested Dependency Tests

#### TC_DYNAMIC_TRACK_001_NestedDynamicReadCaptured

```text
Input:
- composite source evaluates child DynamicValue
- tracked dependency mode enabled

Operation:
- child dependency revision changes

Expected:
- parent cache invalidated
- parent reevaluation occurs on next legal read or schedule
```

#### TC_DYNAMIC_TRACK_002_FormulaInternalVariableChangeCaptured

```text
Input:
- formula source builds internal variable from child DynamicValue

Operation:
- child value changes

Expected:
- tracker records the child dependency
- formula cache invalidates
- no stale result remains visible
```

### Cache and Invalidation Tests

#### TC_DYNAMIC_CACHE_001_TrackedCacheHitNoRecompute

```text
Input:
- stable dependency stamp

Operation:
- repeated tracked read

Expected:
- cache hit
- no recompute
```

#### TC_DYNAMIC_CACHE_002_ActorResolutionRevisionInvalidates

```text
Input:
- source depends on runtime-query-resolved actor

Operation:
- runtime-query generation or scope identity changes

Expected:
- cache invalidated through declared invalidation token
```

#### TC_DYNAMIC_CACHE_003_SourceConfigRevisionInvalidatesCompileAndResult

```text
Input:
- expression source with compile cache

Operation:
- source configuration changes

Expected:
- compile cache invalidated
- tracked result invalidated
```

### Failure and Diagnostics Tests

#### TC_DYNAMIC_FAIL_001_CycleRejected

```text
Input:
- evaluation graph contains cycle without legal bounded policy

Expected:
- Failed
- DYN_EVAL_CYCLE_DETECTED
```

#### TC_DYNAMIC_FAIL_002_ImplicitFallbackForbidden

```text
Input:
- required evaluation input missing
- no explicit fallback policy

Expected:
- Failed
- DYN_FALLBACK_FORBIDDEN
```

#### TC_DYNAMIC_DIAG_001_DiagnosticIncludesPlanSourceAndTarget

```text
Operation:
- evaluation failure emitted

Expected:
- diagnostic includes plan id, source provenance, target identity, and failure boundary
```

### Performance Tests

#### TC_DYNAMIC_PERF_001_NoAllocationOnTrackedHit

```text
Operation:
- repeated tracked cache hit

Expected:
- no managed allocation in the normal path
```

#### TC_DYNAMIC_PERF_002_NoStringLookupInHotPath

```text
Operation:
- repeated hot-path evaluation

Expected:
- no runtime stable-key lookup
```

---

## Acceptance Criteria

This specification is complete when it defines:

- DynamicValue authoring/runtime boundary
- source evaluation contract
- explicit evaluation context contract
- `DynamicEvaluationPlan` and `ReactiveEvaluationPlan`
- dependency declaration modes
- shared tracker and cache ownership
- unified invalidation and dependency-stamp policy
- nested dependency capture rules
- phase and scheduling rules
- hot-path performance rules
- diagnostics provenance and failure policy
- migration policy
- forbidden patterns
- required test cases

The specification is not complete if tracker, cache, invalidation, or nested dependency capture remains ownerless or implicit.

---

## Final Position

Dynamic evaluation is an explicit runtime subsystem.

`DynamicValue` preserves `SerializeReference` authoring.
It does not remain a bag of source-local caches, ad hoc invalidation checks, and hidden nested reevaluation.

10 owns value state.
10-2 owns evaluation semantics.
11 owns diagnostics routing.

That split is the minimum architecture needed to make DynamicValue performant, debuggable, and deterministic in Kernel v2.
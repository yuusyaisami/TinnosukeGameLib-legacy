# Scalar Runtime and Binding Specification

## Document Status

- Document ID: `10_1_ScalarRuntimeAndBindingSpec`
- Status: Draft
- Role: defines the float-specialized scalar runtime contract, modifier pipeline, binding semantics, telemetry boundary, diagnostics, and migration direction for Kernel v2
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
  - [10_2_DynamicValueEvaluationSpec.md](10_2_DynamicValueEvaluationSpec.md)
  - [11_DebugMapAndDiagnosticsSpec.md](11_DebugMapAndDiagnosticsSpec.md)
- Provides foundation for:
  - [12_UnityAuthoringBridgeSpec.md](12_UnityAuthoringBridgeSpec.md)
  - [13_LegacyCompatBoundarySpec.md](13_LegacyCompatBoundarySpec.md)
  - [14_PerformanceBudgetAndRuntimeRulesSpec.md](14_PerformanceBudgetAndRuntimeRulesSpec.md)
  - [15_TestAndValidationSpec.md](15_TestAndValidationSpec.md)

### Revision Note

This revision inserts 10-1 between 10 and 10-2 as the owner of scalar runtime and binding semantics.

It separates the current float-specialized scalar pipeline from the generic `ValueSchema` / `ValueStore` contract in 10 and from the dynamic evaluation contract in 10-2.

It also defines the architecture direction for replacing `Animator.StringToHash` identity, nearest-ancestor fallback, registry-based binding search, silent zero defaults, and embedded `DynamicValue` reads with verified scalar identities, verified endpoints, explicit plans, and structured diagnostics.

---

## Ownership

This specification owns:

- float-specialized scalar runtime semantics
- scalar identity boundary and scalar-facing runtime IDs
- scalar runtime config plan semantics
- scalar baseline, local-base, additive, multiplicative, clamp, round, and timed-contribution ordering
- scalar contribution handle lifetime and update semantics
- scalar service read, write, and inherited-access boundary
- scalar binding and rebasing semantics
- scalar subscription and change-notification boundary
- scalar telemetry and snapshot boundary
- scalar lifecycle reset and timed-update policy
- scalar-specific diagnostics provenance and failure behavior
- migration of current `BaseScalarService`, `ScalarKeyRuntime`, `ScalarBindingManager`, `ScalarKey`, and `ScalarHandle` patterns

This specification does not own:

- generic `ValueKeyId`, `ValueSchema`, or `ValueStore` ownership
- `LayeredNumeric` generic state semantics
- `DynamicValue`, `DynamicEvaluationPlan`, or `ReactiveEvaluationPlan` internals
- concrete service registration implementation
- concrete scope graph implementation
- final authoring component schema
- final binary artifact format
- final editor inspector, debug panel, or telemetry UI layout

10-1 owns scalar runtime semantics.
It must not re-own generic value-state semantics already owned by 10 or dynamic evaluation semantics already owned by 10-2.

---

## Purpose

This specification defines how verified scalar definitions are configured, evaluated, bound across scopes, observed, and diagnosed at runtime.

Core statements:

```text
Scalar is a float-specialized runtime modulation service.
It is not a generic value store.

ScalarKey string/hash data is authoring and diagnostics metadata.
It is not runtime truth.

Scalar bindings and inherited access must resolve through verified scope relationships and verified endpoints.
They must not search runtime registries or transform hierarchies.

Dynamic scalar inputs use explicit evaluation plans from 10-2.
They do not remain hidden DynamicValue reads inside runtime modifiers.
```

If a required scalar read succeeds only because runtime searched for a parent service, hashed a string key, or returned `0` as a silent repair path, the target architecture has regressed.

---

## Scope

This specification defines:

- scalar runtime identity model
- scalar and `ValueStore` boundary
- float-only numeric policy for target scalar paths
- scalar runtime config model
- scalar contribution and modifier pipeline
- scalar access model
- inherited access model
- scalar binding model
- scalar dynamic-input boundary
- scalar subscription and telemetry model
- scalar lifecycle and reset model
- scalar diagnostics and failure policy
- scalar performance policy
- scalar legacy migration policy
- forbidden patterns
- required test cases

---

## Non-Goals

This specification does not define:

- the final in-memory storage container for scalar state
- the final generated accessor API
- the final authoring asset schema for scalar profiles or bindings
- the final inspector or telemetry UI
- the final binary representation of scalar artifacts
- the generic `LayeredNumeric` state model
- generic dynamic evaluation algorithms
- generic `ValueStore` save payload rules

10-1 must not become `ValueStore v2`.
It defines the scalar-specialized runtime layer that sits on top of verified value-state contracts.

---

## Relationship to Other Specs

| Spec | Relationship |
|---|---|
| [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md) | Defines the no-discovery, no-silent-fallback, explicit-runtime rule that scalar runtime must obey. |
| [01_KernelIRSpec.md](01_KernelIRSpec.md) | Owns normalized IDs and source locations that scalar keys, endpoints, and diagnostics must derive from. |
| [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md) | Owns the contribution model that scalar config, binding, and baseline declarations must enter through. |
| [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md) | Generates verified scalar runtime inputs and binding projections from normalized declarations. |
| [04_DependencyValidationSpec.md](04_DependencyValidationSpec.md) | Validates scalar key identity, numeric compatibility, endpoint existence, binding legality, and dynamic-input references before runtime boot. |
| [05_BootManifestAndProfileSpec.md](05_BootManifestAndProfileSpec.md) | Owns how verified scalar artifacts are selected and proven compatible at boot time. |
| [06_ServiceGraphRuntimeSpec.md](06_ServiceGraphRuntimeSpec.md) | Owns service registration/runtime composition; 10-1 owns only the scalar service contract and its required semantics. |
| [07_ScopeGraphRuntimeSpec.md](07_ScopeGraphRuntimeSpec.md) | Owns explicit scope parentage and scope identity used by inherited scalar access and bindings. |
| [08_LifecyclePlanSpec.md](08_LifecyclePlanSpec.md) | Owns when scalar timed entries tick, when reset happens, and when verified reapplication steps run. |
| [09_CommandCatalogRuntimeSpec.md](09_CommandCatalogRuntimeSpec.md) | Owns command execution; 10-1 defines how commands may request scalar reads or writes against verified scalar IDs. |
| [10_ValueSchemaAndStoreSpec.md](10_ValueSchemaAndStoreSpec.md) | Owns generic value identity, schema, store, and `LayeredNumeric` state semantics; 10-1 owns only the scalar-specialized runtime service, contribution ordering, and binding semantics layered on top of verified numeric definitions. |
| [10_2_DynamicValueEvaluationSpec.md](10_2_DynamicValueEvaluationSpec.md) | Owns dynamic evaluation plans and dependency tracking used by scalar dynamic bounds or other scalar-driven reactive inputs. |
| [11_DebugMapAndDiagnosticsSpec.md](11_DebugMapAndDiagnosticsSpec.md) | Owns the shared diagnostics substrate and DebugMap contract that scalar runtime must emit through. |
| [12_UnityAuthoringBridgeSpec.md](12_UnityAuthoringBridgeSpec.md) | Normalizes Unity authoring for scalar profiles, bindings, keys, and source locations into verified scalar input. |
| [13_LegacyCompatBoundarySpec.md](13_LegacyCompatBoundarySpec.md) | Will isolate any temporary adapters needed to bridge current `BaseScalarMB` and related legacy paths. |
| [14_PerformanceBudgetAndRuntimeRulesSpec.md](14_PerformanceBudgetAndRuntimeRulesSpec.md) | Will budget scalar read, write, binding, and timed-update hot paths. |
| [15_TestAndValidationSpec.md](15_TestAndValidationSpec.md) | Will turn the required scalar tests in this document into executable validation and CI coverage. |

---

## Current Scalar Debt Observations

This section summarizes the current scalar-system debt observed in the codebase.
It is migration evidence, not target policy.

### Observation Traceability

| Observation | Evidence Type | Target Pressure |
|---|---|---|
| MonoBehaviour installs scalar services and null config providers directly into runtime builders. | Source | declarative authoring plus verified service/runtime projection |
| Inherited scalar access walks the scope path at runtime to find an ancestor service. | Source | explicit scope relation and explicit inherited endpoint resolution |
| Missing scalar reads can return `0` without structured failure. | Source | no silent fallback for required scalar access |
| Scalar binding resolves source and target services by registry search at runtime. | Source | verified binding endpoints and boot-time validation |
| Scalar identity still derives from `Animator.StringToHash(Name)`. | Source | verified scalar identity and DebugMap-backed names |
| Scalar config can embed `DynamicValue<float>` directly inside runtime modifiers. | Source | explicit dynamic evaluation plans and cached results |
| Runtime callback failures can still be logged and swallowed locally. | Source | structured diagnostics and explicit failure boundary |

### Representative Anchors

- [BaseScalarMB.cs](../../GameLib/Script/Common/Variables/Scalar/MB/BaseScalarMB.cs) - runtime installer pattern, direct component registration, and per-scope service registration
- [BaseScalarService.cs](../../GameLib/Script/Common/Variables/Scalar/Core/BaseScalarService.cs) - ancestor fallback, local/global access split, scope reset, reapply behavior, and runtime zero-default behavior
- [ScalarKeyRuntime.cs](../../GameLib/Script/Common/Variables/Scalar/Core/ScalarKeyRuntime.cs) - baseline plus contribution pipeline, cache invalidation, timed entries, and get-stage modifiers
- [ScalarBindingManager.cs](../../GameLib/Script/Common/Variables/Scalar/Registry/ScalarBindingManager.cs) - runtime binding search through scope registry and binding tick behavior
- [ScalarKeyDef.cs](../../GameLib/Script/Common/Variables/Scalar/Def/ScalarKeyDef.cs) - name/hash identity coupling and `LifetimeScopeKind`-based references
- [ScalarModifierDef.cs](../../GameLib/Script/Common/Variables/Scalar/Def/ScalarModifierDef.cs) - clamp config with embedded `DynamicValue<float>` bounds
- [ScalarTelemetryDef.cs](../../GameLib/Script/Common/Variables/Scalar/Def/ScalarTelemetryDef.cs) - telemetry snapshot surface that currently mixes runtime state and debug viewing concerns

### Current Gaps

The target architecture must close the following gaps:

- scalar service installation still depends on legacy installer mutation
- inherited scalar access still depends on runtime scope traversal
- scalar identity still depends on runtime hashing of authoring strings
- required scalar access can still degrade into silent `0` defaults
- scalar binding endpoints can still be discovered by runtime registry scan
- scalar modifiers can still evaluate hidden dynamic inputs in runtime code
- scalar callback failures can still bypass the shared diagnostics substrate

---

## Scalar Runtime Definition

Target scalar runtime is a float-specialized modulation subsystem bound to verified numeric identity.

Explanatory model:

```csharp
public interface IScalarRuntime
{
    bool TryReadLocal(ScalarKeyId keyId, out float value);
    bool TryReadInherited(ScalarKeyId keyId, out float value);
    ScalarContributionHandle Add(ScalarKeyId keyId, float delta, ScalarContributionOptions options);
    ScalarContributionHandle Mul(ScalarKeyId keyId, float factor, ScalarContributionOptions options);
}
```

This model is explanatory.
It does not lock the final API surface.

Required runtime properties:

- scalar runtime is created from verified scalar inputs
- scalar runtime may be exposed through scope-specific service facades, but those facades must not create separate scalar identity domains
- scalar runtime must not discover keys, resolve parents by traversal, or build config by observing runtime access
- scalar runtime must not require `MonoBehaviour` installer execution to become structurally valid

---

## Scalar and ValueStore Boundary

10 owns generic value identity, schema, store, and `LayeredNumeric` state semantics.
10-1 owns the scalar-specialized runtime layer built on top of that foundation.

Required boundary rules:

- every target scalar definition must be backed by validated numeric schema from 10
- scalar runtime may use `ValueStore`, `LayeredNumeric`, or another verified numeric backend internally, but that backend must preserve the scalar semantics owned here
- scalar runtime must not create new keys, new schema entries, or new numeric slots on first access
- `LayeredNumeric` remains the generic numeric state model; scalar runtime owns the service-facing contract, contribution ordering, binding behavior, and telemetry boundary
- save, generic schema compatibility, and value-store lifetime still belong to 10

10-1 does not require one final storage shape.
It requires one explicit scalar behavior contract.

---

## Scalar Identity Model

Scalar runtime must use verified numeric identity.

Explanatory model:

```csharp
public readonly struct ScalarKeyId
{
    public readonly int Value;
}
```

`ScalarKeyId` may be implemented as a typed wrapper around `ValueKeyId` if both spaces are intentionally unified.
If separate typed wrappers exist, their mapping must still be verified before runtime boot.

Allowed identity sources:

- verified generated numeric ID
- validated typed wrapper over verified numeric ID
- DebugMap-backed debug name attached to that ID

Forbidden identity sources:

- `Animator.StringToHash`
- raw string name lookup in runtime hot paths
- runtime-generated fallback IDs
- `LifetimeScopeKind` plus string key as composite runtime truth

`ScalarKey.Name`-style strings may remain in authoring, migration, and diagnostics.
They are not runtime authority.

---

## Float-Only Numeric Policy

Target scalar runtime is float-specialized.

Rules:

- target scalar values are finite `float`
- implicit runtime coercion from `int`, `double`, `string`, or `DynamicVariant` is forbidden
- if authoring supplies another numeric representation, conversion must happen before runtime execution during normalization or validation
- `NaN` and infinity must be rejected unless a lower spec explicitly defines a bounded exception and its diagnostics behavior

If future revisions need multiple numeric primitives, they must extend both 10 and 10-1 explicitly.

---

## Scalar Runtime Config Plan

Verified scalar config defines baseline and modifier behavior for a scalar key.

Explanatory model:

```csharp
public sealed class ScalarRuntimeConfigPlan
{
    public ScalarKeyId KeyId;
    public float BaseValue;
    public bool EnableEffectModifier;
    public bool EnableRoundModifier;
    public int RoundDigits;
    public bool EnableClampModifier;
    public ScalarBoundSource MinBound;
    public ScalarBoundSource MaxBound;
}
```

Required rules:

- config presence or absence must be explicit in verified input
- `NullScalarRuntimeConfigProvider`-style silent fallback is forbidden for required scalar definitions
- config application must be deterministic and idempotent
- clamp and other dynamic bounds must reference explicit evaluation results owned by 10-2 rather than embedding ad hoc `DynamicValue` execution in the runtime modifier path
- config replacement must invalidate effective-value cache deterministically

---

## Scalar Contribution and Modifier Model

Scalar runtime owns contribution ordering and handle semantics.

Explanatory contribution kinds:

```csharp
public enum ScalarContributionKind
{
    Add = 10,
    Mul = 20,
}

public enum ScalarMulPhase
{
    PreAdd = 10,
    PostAdd = 20,
}
```

Required evaluation order:

1. start from `BaseValue + LocalBase`
2. apply all `PreAdd` multiplicative contributions
3. apply additive contributions
4. apply all `PostAdd` multiplicative contributions
5. apply get-stage transforms such as round and clamp
6. publish effective value and revision change

Each contribution must define at least:

- contribution identity
- target scalar key
- contribution kind
- phase when multiplicative
- numeric value or factor
- optional layer
- optional tag
- optional remaining duration
- source metadata for diagnostics

Contribution rules:

- handles must support deterministic removal
- handle value updates must preserve contribution identity
- timed contributions must expire only through explicit lifecycle or tick processing
- replace or upsert semantics by layer and tag are allowed only when explicitly defined; duplicate resolution by collection order is forbidden
- modifier registration order must be deterministic
- cache invalidation must occur whenever an input that affects the effective value changes

---

## Scalar Access Model

Scalar runtime may expose local and inherited access.
Those are different contracts.

Rules:

- local access reads or writes only the current verified scalar runtime slice
- inherited access may read or target a parent-visible scalar definition only through explicit scope relations from 07
- optional `TryRead*` style APIs may return `false` on absence
- required reads and writes must fail through structured diagnostics when the target scalar definition is not available
- silent `0` return is forbidden for required scalar access
- inherited write target selection must not search for the first ancestor that happens to contain local data

Current `LocalGet` / `GlobalGet` naming may survive as an API facade only if the underlying semantics match these rules and do not rely on runtime discovery.

---

## Scalar Binding Model

Bindings connect a verified source scalar endpoint to a verified target scalar endpoint.

Explanatory binding modes:

```csharp
public enum ScalarBindingMode
{
    DeltaToAdd = 10,
    DeltaToMul = 20,
    ValueToAdd = 30,
    ValueToMul = 40,
}
```

Required binding fields:

- binding identity
- source endpoint identity
- target endpoint identity
- binding mode
- factor
- optional clamp
- target multiplicative phase when relevant
- optional tag
- rebase policy

Binding rules:

- source and target endpoints must be validated before runtime boot
- binding runtime must not use `ResolveAll`, nearest-scope search, or first-match selection
- rebasing behavior must be explicit and deterministic
- binding updates must execute through an explicit lifecycle step owned by 08
- missing endpoint is a validation or boot failure, not a runtime warning plus skipped behavior

---

## Dynamic Input Boundary

Scalar runtime may consume dynamic inputs.
It must not evaluate them ad hoc.

Rules:

- dynamic clamp bounds, dynamic baseline inputs, or other reactive scalar inputs must be represented through `DynamicEvaluationPlan` or `ReactiveEvaluationPlan`
- scalar runtime may consume the resulting numeric output by verified reference
- scalar runtime must not directly execute `IDynamicSource` or hold `SerializeReference`-backed authoring objects in hot runtime paths
- nested dynamic dependencies used by scalar inputs must remain visible to the 10-2 tracking model

---

## Subscription and Telemetry Model

Scalar runtime may expose change notifications and debug telemetry.

Required notification rules:

- change events must be keyed by verified scalar identity
- change events must expose old and new effective value when available
- handler failures must enter the shared diagnostics pipeline; they must not be silently swallowed
- notification policy must define whether failures are isolated to the handler, the owning scope, or the current operation

Required telemetry rules:

- telemetry is debug and diagnostics metadata, not runtime authority
- telemetry enumeration may allocate outside hot paths, but hot read and write paths must remain allocation-conscious
- telemetry snapshots must include enough data to explain contribution state in development and test profiles

Minimum telemetry snapshot fields:

- scalar key identity
- contribution identity
- contribution kind
- phase
- value or factor
- remaining duration
- source metadata when available
- tag and layer when available

---

## Lifecycle and Reset Policy

Scalar runtime participates in lifecycle only through explicit plans.

Rules:

- timed contributions update during an explicit tick phase
- scope reuse reset must clear transient contributions, transient subscriptions, and transient caches
- verified baseline, config, and binding reapplication after reset must come from explicit plan execution, not local repair logic
- acquire and release behavior must be deterministic and compatible with 08 ordering rules

Current `ResetForScopeReuse` and `ReapplyScopeBindingsIfAvailable` patterns are migration evidence.
Target behavior must come from explicit verified lifecycle steps.

---

## Diagnostics and DebugMap Requirements

11 owns the shared diagnostics substrate.
10-1 defines the scalar-specific provenance that must feed it.

Required scalar diagnostics provenance:

- scalar key identity
- debug name or stable key when available
- binding identity when relevant
- source scope identity when relevant
- target scope identity when relevant
- contribution identity when relevant
- layer and tag when relevant
- profile or artifact identity when relevant
- source location when relevant

Representative stable diagnostic codes:

- `SCALAR_KEY_UNRESOLVED`
- `SCALAR_REQUIRED_VALUE_MISSING`
- `SCALAR_RUNTIME_HASH_ID_FORBIDDEN`
- `SCALAR_INHERITED_ENDPOINT_UNRESOLVED`
- `SCALAR_BINDING_ENDPOINT_MISSING`
- `SCALAR_BINDING_RUNTIME_SEARCH_FORBIDDEN`
- `SCALAR_DYNAMIC_INPUT_PLAN_MISSING`
- `SCALAR_CONFIG_REQUIRED_MISSING`
- `SCALAR_NONFINITE_VALUE_FORBIDDEN`
- `SCALAR_SUBSCRIPTION_HANDLER_FAILED`
- `SCALAR_RUNTIME_FALLBACK_FORBIDDEN`

Warnings in Unity logs are not sufficient for required scalar failures.
Required failures must enter the structured diagnostics pipeline defined by 11.

---

## Failure Policy

| Failure Type | Default Boundary |
|---|---|
| missing required scalar identity | validation failure |
| required scalar config missing | validation or boot failure |
| inherited endpoint unresolved | operation failure or boot failure, depending on whether the endpoint is verified boot input |
| binding endpoint unresolved | validation or boot failure |
| dynamic input plan missing | boot or activation failure |
| non-finite scalar result | operation failure |
| subscription handler failure | handler failure with structured diagnostics; escalation is profile-defined |
| runtime hash/string identity generation | analyzer or validation failure |
| runtime registry search for binding resolution | analyzer or validation failure |

Required scalar access must not degrade into silent zero defaults.
Runtime must fail through explicit diagnostics instead.

---

## Performance Policy

Scalar read, write, and binding update are hot paths.

Target requirements:

- local scalar lookup must be O(1) or bounded small constant
- normal local read must avoid managed allocation
- normal contribution update must avoid managed allocation where practical
- effective-value cache is allowed only with deterministic invalidation
- no string hashing in runtime hot paths
- no runtime registry scan for binding resolution
- no transform traversal for inherited access
- no `Resources.Load` in scalar access or binding paths
- no LINQ in hot paths
- no boxing for common scalar operations where practical
- dynamic scalar inputs must consume cached or explicitly scheduled evaluation results, not perform ad hoc formula evaluation in the hot path

Suggested downstream budget categories:

- `Scalar.ReadLocal`
- `Scalar.ReadInherited`
- `Scalar.ApplyContribution`
- `Scalar.TickTimedContributions`
- `ScalarBinding.Tick`
- `Scalar.ResetForReuse`

Performance optimization must not remove diagnostics provenance, explicit failure behavior, or deterministic contribution ordering.

---

## Legacy Migration Policy

| Legacy Pattern | Target Representation |
|---|---|
| `BaseScalarMB : IFeatureInstaller` | authoring contribution input normalized through 12 and projected by 06/08 |
| `BaseScalarService.ResolveNearestAncestorScalarService()` | explicit inherited endpoint or explicit scope-graph relation from verified plan |
| `LocalGet` / `GlobalGet` returning `0` on absence | optional `TryRead*` returning `false` or required-read diagnostics failure |
| `ScalarKey.Name` plus `Animator.StringToHash` | verified `ScalarKeyId` plus DebugMap-backed debug name |
| `ScalarRef.Space = LifetimeScopeKind` | verified scalar endpoint reference |
| `NullScalarRuntimeConfigProvider` | explicit verified empty config or explicit optional scalar definition |
| `ScalarBindingManager.ResolveAll(...)` | verified source and target endpoints |
| clamp `DynamicValue<float>` embedded in runtime config | `DynamicEvaluationPlan` or `ReactiveEvaluationPlan` reference |
| local `Debug.LogWarning` / `Debug.LogException` | structured diagnostics through 11 |

Legacy migration must not preserve runtime discovery, string-hash identity, or silent fallback as the target behavior.

---

## Forbidden Patterns

The following are forbidden in target scalar runtime paths:

- `Animator.StringToHash` as runtime scalar identity authority
- runtime string-key scalar lookup in hot paths
- runtime-generated fallback scalar IDs
- runtime nearest-ancestor scalar service discovery by scope traversal
- runtime registry scan for scalar binding resolution
- silent `0` default for required scalar access
- direct execution of `DynamicValue` or `IDynamicSource` inside scalar modifier hot paths
- installer-style `MonoBehaviour` mutation as the required path to make scalar runtime valid
- swallowing callback failures through local `try/catch` plus log-only handling
- duplicate contribution resolution by collection order
- transform hierarchy as scalar ownership truth
- `Resources.Load` fallback for required scalar inputs

---

## Test Case Model

Each scalar test case must define:

- Test ID
- Title
- scalar fixture
- verified input fixture
- operation
- expected value result
- expected diagnostics
- expected allocation or profiler note when relevant

---

## Required Test Cases

### A. Identity and Access Tests

#### TC_SCALAR_ID_001_RuntimeHashIdentityForbidden

```text
Input:
- scalar runtime path attempts to derive identity from a string or Animator hash

Expected:
- failed
- SCALAR_RUNTIME_HASH_ID_FORBIDDEN
```

#### TC_SCALAR_ACCESS_001_RequiredReadDoesNotReturnSilentZero

```text
Input:
- required scalar key is missing

Expected:
- failed
- SCALAR_REQUIRED_VALUE_MISSING
```

#### TC_SCALAR_ACCESS_002_OptionalReadCanReportAbsence

```text
Input:
- optional scalar key is absent

Expected:
- TryRead-style API returns false
- no implicit key creation
```

### B. Contribution Pipeline Tests

#### TC_SCALAR_PIPE_001_ContributionOrderingIsDeterministic

```text
Input:
- base value
- pre-add multipliers
- add contributions
- post-add multipliers
- round and clamp transforms

Expected:
- effective result follows the defined ordering exactly
```

#### TC_SCALAR_PIPE_002_HandleUpdatePreservesContributionIdentity

```text
Operation:
- issue a contribution handle
- update its value

Expected:
- contribution identity is stable
- effective value updates without duplicate contribution creation
```

#### TC_SCALAR_PIPE_003_TimedContributionExpiresOnTickPhase

```text
Input:
- timed scalar contribution

Expected:
- contribution remains until the explicit tick/update phase
- contribution is removed deterministically at expiry
```

### C. Binding Tests

#### TC_SCALAR_BIND_001_BindingEndpointsMustBeVerified

```text
Input:
- scalar binding with unresolved source or target endpoint

Expected:
- failed before runtime use
- SCALAR_BINDING_ENDPOINT_MISSING
```

#### TC_SCALAR_BIND_002_BindingDoesNotUseRuntimeRegistrySearch

```text
Operation:
- execute a scalar binding update

Expected:
- no ResolveAll-style registry search
- no first-match fallback
```

#### TC_SCALAR_BIND_003_RebaseIsDeterministic

```text
Operation:
- change source baseline
- run explicit rebase

Expected:
- delta-based binding recomputes from the new base deterministically
```

### D. Dynamic Input Tests

#### TC_SCALAR_DYN_001_DynamicClampUsesEvaluationPlan

```text
Input:
- scalar clamp bound depends on runtime context

Expected:
- scalar runtime consumes verified dynamic evaluation output
- no direct DynamicValue execution in the scalar modifier hot path
```

#### TC_SCALAR_DYN_002_MissingDynamicPlanFails

```text
Input:
- scalar config references a dynamic bound without a verified plan

Expected:
- failed
- SCALAR_DYNAMIC_INPUT_PLAN_MISSING
```

### E. Lifecycle and Diagnostics Tests

#### TC_SCALAR_LIFE_001_ResetClearsTransientState

```text
Operation:
- reuse a pooled scope

Expected:
- transient contributions and transient subscriptions are cleared
- verified baseline and binding state are reapplied only through explicit lifecycle steps
```

#### TC_SCALAR_DIAG_001_HandlerFailureUsesStructuredDiagnostics

```text
Input:
- scalar subscription handler throws

Expected:
- failure enters structured diagnostics pipeline
- no local swallow plus log-only behavior
```

#### TC_SCALAR_PERF_001_LocalReadNoAllocation

```text
Operation:
- repeated local scalar read on a verified key

Expected:
- no managed allocation in the normal path
```

---

## Acceptance Criteria

This specification is complete when it defines:

- scalar runtime identity model
- scalar and `ValueStore` boundary
- float-only numeric policy
- scalar runtime config model
- scalar contribution and modifier ordering
- scalar access boundary
- scalar binding semantics
- scalar dynamic-input boundary
- scalar subscription and telemetry boundary
- scalar lifecycle and reset policy
- scalar diagnostics and failure policy
- scalar performance policy
- scalar migration policy
- forbidden patterns
- required test cases

The specification is not complete if scalar identity still depends on runtime string hashing, inherited scalar access still depends on runtime discovery, or required scalar reads can still degrade into silent zero defaults.

---

## Final Position

Scalar is a float-specialized verified runtime modulation service.

It consumes verified numeric identity, verified scope relations, verified binding endpoints, and explicit dynamic evaluation outputs.

It must not search runtime registries, walk scope ancestry for truth, hash authoring strings into runtime identity, or return `0` as a silent repair path.

If scalar behavior still depends on runtime discovery, hidden `DynamicValue` execution, or installer-style mutation from Unity components, the target architecture has already regressed.
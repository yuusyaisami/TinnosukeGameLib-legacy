# Test and Validation Specification

## Document Status

- Document ID: 15_TestAndValidationSpec
- Status: Draft
- Role: defines executable validation, test-layer policy, regression gates, fixture and artifact rules, and CI or CLI acceptance policy for Kernel v2
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
  - [14_PerformanceBudgetAndRuntimeRulesSpec.md](14_PerformanceBudgetAndRuntimeRulesSpec.md)
- Provides foundation for:
  - implementation work that turns the Kernel v2 architecture into enforced quality gates

### Revision Note

This revision creates 15 as the architecture-protection spec for Kernel v2.

It does not treat testing as an implementation afterthought.
It defines how the architecture described by 00 through 14 becomes executable validation, deterministic artifacts, runtime behavior tests, diagnostics snapshot tests, performance gates, static rule enforcement, legacy-boundary regression checks, and CI or CLI failure policy.

It also records the current reality that the repository already contains a shared Unity batch test runner and a documentation marker test, but does not yet contain the full executable suite required to protect the architecture from drift.

---

## Ownership

This specification owns:

- kernel-wide test philosophy and acceptance standard
- test-layer model and test responsibility boundaries
- test profile model
- fixture identity and fixture-size model
- executable validation policy for lower-spec rules
- generation determinism and golden-artifact test policy
- runtime behavior test policy
- diagnostics snapshot and diagnostics assertion policy
- performance and forbidden-operation test policy
- static rule and analyzer gate policy
- legacy compatibility regression test policy
- determinism rules for tests and artifacts
- snapshot, baseline, golden file, and report policy
- test artifact output policy
- failure classification and gate failure meaning
- CI or CLI gate sequencing policy
- EditMode and PlayMode responsibility boundary
- test isolation, cleanup, reset, and double-policy rules
- required test suite inventory and minimum required cases
- regression policy and forbidden testing patterns
- acceptance criteria for architecture protection

This specification does not own:

- the semantic meaning of dependency validation rules owned by 04
- the semantic meaning of generation artifacts owned by 03
- the semantic meaning of diagnostics record fields, codes, or severities owned by 11
- the semantic meaning of performance budgets or marker taxonomy owned by 14
- the semantic meaning of legacy quarantine owned by 13
- the semantic meaning of Unity authoring extraction owned by 12
- the final Unity Test Runner implementation details
- CI vendor YAML details
- editor dashboard UI for test reporting
- game-specific content tests that are outside Kernel v2 architecture protection

15 owns the executable protection model.
It does not re-own the subsystem semantics already fixed by 00 through 14.

---

## Purpose

This specification defines how Kernel v2 proves that its architecture still holds.

Core statements:

```text
A target kernel behavior is not accepted because it compiles.
It is accepted only when it passes validation, generation, runtime, diagnostics, performance, and regression gates.

A feature is not accepted until its failure modes, diagnostics, generated artifacts, runtime behavior, and performance rules are tested.

If the test suite cannot detect architecture regression, the architecture is not actually protected.
```

Required central rule:

```text
Compilation is necessary but never sufficient.
Executable validation is part of the architecture contract.
```

Additional rule:

```text
Kernel tests must fail on missing proof, not only on obvious implementation failure.
```

This specification exists to prevent the following regressions:

- a subsystem compiles but bypasses validation or plan generation trust boundaries
- runtime behavior silently falls back to discovery, reflection, string lookup, or legacy repair
- diagnostics are emitted, but they are not deterministic, structured, or traceable
- performance rules exist in documents but are not enforced by executable gates
- stale or mismatched generated artifacts are consumed without a failing test
- lower specs define required cases, but no executable suite actually proves them
- CI reports green while architecture drift has already re-entered the runtime

---

## Scope

This specification defines:

- test philosophy and acceptance model
- test-layer taxonomy
- test profile taxonomy
- fixture identity and fixture metadata requirements
- validation test execution model
- generation test execution model
- runtime behavior test execution model
- diagnostics snapshot and diagnostics assertion model
- performance and forbidden-operation test model
- static rule and analyzer test model
- legacy compatibility regression test model
- integration smoke test model
- golden file and snapshot policy
- determinism policy for test inputs and outputs
- artifact and report output policy
- failure classification policy
- CI or CLI gate sequencing
- EditMode and PlayMode boundary
- test isolation and cleanup rules
- mock, fake, and stub policy
- required suites and minimum required cases
- regression policy
- forbidden testing patterns
- architecture-protection acceptance criteria

---

## Non-Goals

This specification does not define:

- the final implementation of every test helper class
- a promise that all tests must use one exact framework outside existing Unity and .NET constraints
- CI provider configuration files
- generic unit-testing advice unrelated to Kernel v2 architecture
- gameplay feature tests that do not protect architectural rules
- profiler UI workflow instructions
- benchmark visual dashboard design
- snapshot approval UI or editor tooling UX

15 must not become a generic testing handbook.
It defines the minimum executable protection required for the plan-first kernel.

---

## Relationship to Other Specs

| Spec | Relationship |
|---|---|
| [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md) | Defines the root verification taxonomy; 15 turns that taxonomy into concrete gate structure and failure policy. |
| [01_KernelIRSpec.md](01_KernelIRSpec.md) | Owns IR identity, node, edge, and source models that tests must consume without redefining. |
| [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md) | Owns contribution semantics; 15 proves extraction and normalization outputs remain valid through executable tests. |
| [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md) | Owns generation semantics, artifact identity, hash, and determinism rules; 15 turns those rules into golden, baseline, and regeneration gates without redefining artifact meaning. |
| [04_DependencyValidationSpec.md](04_DependencyValidationSpec.md) | Owns dependency correctness, validation semantics, and diagnostic expectations; 15 consumes the fixture model and turns it into executable validation and CI gates. |
| [05_BootManifestAndProfileSpec.md](05_BootManifestAndProfileSpec.md) | Owns boot acceptance and profile semantics; 15 proves boot acceptance, rejection, and profile-dependent failure boundaries through executable tests. |
| [06_ServiceGraphRuntimeSpec.md](06_ServiceGraphRuntimeSpec.md) | Owns service runtime semantics; 15 verifies that runtime service behavior follows the validated and generated contracts under deterministic fixtures. |
| [07_ScopeGraphRuntimeSpec.md](07_ScopeGraphRuntimeSpec.md) | Owns runtime scope semantics; 15 verifies scope parentage, creation, handle, and query behavior without runtime discovery regressions. |
| [08_LifecyclePlanSpec.md](08_LifecyclePlanSpec.md) | Owns lifecycle ordering semantics; 15 verifies that lifecycle execution comes from verified plans rather than runtime scans or handler collection repair. |
| [09_CommandCatalogRuntimeSpec.md](09_CommandCatalogRuntimeSpec.md) | Owns command runtime semantics; 15 verifies executor dispatch, diagnostics, and forbidden string-key or discovery regressions. |
| [10_ValueSchemaAndStoreSpec.md](10_ValueSchemaAndStoreSpec.md) | Owns value schema and store semantics; 15 verifies value behavior, save-related diagnostics, and rejection of runtime identity fallback. |
| [10_1_ScalarRuntimeAndBindingSpec.md](10_1_ScalarRuntimeAndBindingSpec.md) | Owns scalar runtime semantics; 15 verifies scalar evaluation, rebasing, binding, and hot-path rules through executable tests. |
| [10_2_DynamicValueEvaluationSpec.md](10_2_DynamicValueEvaluationSpec.md) | Owns dynamic evaluation semantics; 15 verifies caching, invalidation, dependency capture, failure behavior, and deterministic outputs. |
| [11_DebugMapAndDiagnosticsSpec.md](11_DebugMapAndDiagnosticsSpec.md) | Owns the diagnostics substrate, code governance, and record model; 15 implements snapshot, analyzer, and regression gates that consume those contracts without redefining them. |
| [12_UnityAuthoringBridgeSpec.md](12_UnityAuthoringBridgeSpec.md) | Owns authoring extraction and direct-play semantics; 15 verifies determinism, identity stability, and authoring-to-artifact correctness. |
| [13_LegacyCompatBoundarySpec.md](13_LegacyCompatBoundarySpec.md) | Owns the quarantine policy for legacy compatibility; 15 proves that adapters remain explicit, visible, bounded, and removable. |
| [14_PerformanceBudgetAndRuntimeRulesSpec.md](14_PerformanceBudgetAndRuntimeRulesSpec.md) | Owns path classification, budget meaning, and marker taxonomy; 15 turns them into profiler assertions, forbidden-operation tests, and regression gates. |

15 is the proof layer for the rest of the architecture.
It must not weaken or reinterpret lower-spec meaning in order to make tests easier to pass.

---

## Assembly Definition and Compile Boundary Expectations

The intended assembly family for executable architecture protection is `GameLib.Tests.*`.
Detailed dependency matrices remain owned by [17_AssemblyDefinitionAndCompileBoundarySpec.md](17_AssemblyDefinitionAndCompileBoundarySpec.md).

Required compile-boundary rules for 15:

- production assemblies must not depend on `GameLib.Tests.*`
- EditMode and PlayMode test code must stay in test assemblies or editor-only assemblies rather than in production runtime code
- static scanners, doc gates, diagnostics snapshots, and artifact-writing test helpers belong in test assemblies, even when they consume kernel production contracts
- test-only packages and NUnit or Unity Test Framework references must not leak into kernel production asmdefs

If architecture protection requires production runtime assemblies to reference test frameworks or test helpers directly, the 15 boundary has been violated.

---

## Current Test Infrastructure Observations

The repository already contains a small but important part of the required infrastructure.

### Observation Traceability

| Observation | Evidence Type | Pressure |
|---|---|---|
| A shared Unity batch runner already exists. | Source | formalize one common CLI or CI test entry point |
| The runner already emits per-run log, XML, and summary files under a timestamped directory. | Source | standardize artifact and report policy instead of inventing a parallel format |
| An EditMode documentation marker test already exists. | Source | preserve traceability from spec text to executable checks |
| The current executable suite is mostly limited to documentation marker presence. | Source | define and require missing validation, generation, runtime, diagnostics, performance, and legacy suites |
| The runner already distinguishes did not start, timeout, zero tests collected, failed, and passed. | Source | align failure classification with real execution outputs |
| PlayMode support exists in the runner, but PlayMode kernel suites are not yet present. | Source | define strict responsibility boundaries rather than pushing everything into PlayMode |

### Representative Anchors

- [Run-UnityTests.ps1](../../../Tools/Run-UnityTests.ps1) - shared Unity batch runner, timeout behavior, XML parsing, summary generation, and output directory convention
- [KernelArchitectureDocTests.cs](../../../Assets/Editor/Tests/KernelArchitectureDocTests.cs) - documentation marker verification currently used as the traceability anchor for v2 specs
- [Logs/TestRuns](../../../Logs/TestRuns) - timestamped output directories containing `unity.log`, `TestResults.xml`, and `summary.md`
- [README.md](README.md) - docs index and existing runner documentation reference

### Current Gaps

The current project still exposes the following gaps that 15 must close:

- executable dependency validation suites are not yet present
- generation determinism and stale-artifact suites are not yet present
- runtime behavior suites for service, scope, lifecycle, command, value, and boot are not yet present in the required form
- diagnostics snapshot and code-governance suites are not yet present
- profiler-marker and forbidden-operation suites are not yet present
- static analyzer or code-search gates for banned APIs are not yet formalized
- legacy boundary regression suites are not yet present
- the documentation marker suite does not by itself prove runtime architectural correctness

---

## Core Problem

The main risk is not that the architecture is undocumented.
The main risk is that the architecture drifts while still compiling.

Typical failure pattern:

1. a subsystem introduces a convenient fallback or scan
2. the code still compiles and local behavior appears correct
3. no executable gate detects that the trust boundary moved
4. the fallback becomes normalized as migration debt
5. the architecture is now weaker than the specs claim

Kernel v2 is especially exposed to this drift because it relies on explicit validation, verified plans, DebugMap-backed diagnostics, runtime no-fallback policy, legacy quarantine, and performance rules that are architectural rather than cosmetic.

Without 15, the repository can contain perfect documents while still accepting:

- stale generated plans
- runtime discovery by hierarchy scan or registration scan
- ad hoc Unity logging in subsystem code
- runtime stable-key lookup or negative-ID repair
- profile-specific legacy fallback
- missing profiler markers on hot paths
- tests that assert only success cases while failure modes remain unprotected

15 exists so these regressions become failing gates rather than review comments.

---

## Test Philosophy

Kernel test philosophy is fail-closed.

Required principles:

1. compile success is not an acceptance signal
2. success-path behavior and failure-path behavior are equally important
3. a lower-spec rule that cannot be exercised by a test or analyzer is not yet protected
4. deterministic output matters as much as functional correctness for plans, diagnostics, and reports
5. broad runtime discovery, fallback repair, or direct Unity logging must be caught automatically rather than by convention only
6. tests must prove that the system rejects invalid structure, not only that it handles valid structure

Required central statement:

```text
The architecture is protected only when the suite can detect how it would fail.
```

Bad test posture:

```text
The runtime booted once, so the architecture is probably intact.
```

Acceptable test posture:

```text
The runtime accepted only validated input, generated deterministic artifacts,
emitted structured diagnostics, stayed within explicit runtime rules,
rejected forbidden fallback, and failed with traceable evidence when the fixture was invalid.
```

---

## Test Layer Model

15 defines the following kernel test layers.

| Layer | Purpose | Primary Owner of Semantics | Minimum Gate |
|---|---|---|---|
| `SpecShape` | Prove that required test markers, required suite inventory, and doc index remain traceable. | 00 through 15 | EditMode doc tests |
| `Validation` | Prove invalid structures are rejected before runtime execution. | 04 | deterministic validation fixtures |
| `Generation` | Prove verified-plan generation, artifact consistency, and determinism. | 03 | golden or baseline generation tests |
| `RuntimeBehavior` | Prove runtime behavior consumes verified plans rather than discovering structure. | 05 through 10-2 | EditMode or PlayMode runtime tests |
| `Diagnostics` | Prove structured diagnostics, DebugMap resolution, and sink rules. | 11 | snapshot and analyzer tests |
| `PerformanceRule` | Prove forbidden operations stay forbidden and markers or allocations follow rules. | 14 | profiler or analyzer gates |
| `StaticRule` | Prove code does not reintroduce banned APIs or direct logging patterns. | 11, 13, 14 | analyzer or code-search gates |
| `LegacyCompat` | Prove legacy remains quarantined and removable. | 13 | boundary regression tests |
| `IntegrationSmoke` | Prove the cross-spec pipeline still functions end to end under verified inputs. | 00 through 14 | boot or end-to-end smoke tests |

Layer rules:

- one failing lower layer invalidates higher-layer confidence
- `IntegrationSmoke` must not be used to excuse missing lower-layer coverage
- `PlayMode` must not be the default home for pure data validation
- `SpecShape` is necessary for traceability but never sufficient for acceptance

---

## Test Profile Model

Not every suite runs under one identical environment.

### Kernel Test Profiles

| Profile | Purpose | Allowed Focus | Forbidden Simplification |
|---|---|---|---|
| `Doc` | spec-shape and traceability checks | marker existence, index integrity | claiming runtime correctness |
| `Validation` | structural correctness before runtime | invalid graph fixtures, expected diagnostics, profile legality | runtime repair |
| `Generation` | plan and artifact correctness | hash, determinism, stale-artifact rejection | treating generated text as unverified runtime truth |
| `Runtime` | behavior from verified inputs | boot, resolve, scope, lifecycle, command, value, dynamic evaluation | runtime discovery as test setup |
| `Diagnostics` | structured failure proof | snapshot, provenance, code, sink routing | message-only assertion without code |
| `Performance` | runtime rule enforcement | marker presence, allocation ceilings, forbidden API absence | wall-clock-only acceptance |
| `Legacy` | boundary quarantine | adapter visibility, dependency direction, removal evidence | fallback into legacy to make tests green |
| `Integration` | end-to-end kernel sanity | verified artifact to runtime smoke | replacing focused lower-layer tests |
| `CI` | merge or publish gate | ordered execution and failure aggregation | silent skip or partial success treated as green |

Profile rules:

- every required suite must declare its profile
- profile is part of test identity and report metadata
- profile may change permitted environment setup, but not semantic expectations
- profile-specific tolerance must never hide an invalid architecture state

---

## Test Fixture Model

Tests must use fixtures that are explicit, named, deterministic, and reviewable.

### Fixture Identity

Every kernel architecture fixture must declare:

- fixture ID
- title
- owner spec
- layer
- profile
- fixture size
- source files or source asset references
- expected status
- expected diagnostics codes or marker requirements
- notes for migration context when needed

Recommended shape:

```csharp
public enum TestFixtureSize
{
    Minimal = 10,
    Small = 20,
    Medium = 30,
    Large = 40,
    Broken = 90,
}
```

Definitions:

- `Minimal`: smallest fixture that proves one rule
- `Small`: one subsystem-focused fixture with limited cross-links
- `Medium`: multi-subsystem fixture proving cross-spec interaction
- `Large`: near-production shape for smoke or regression coverage
- `Broken`: intentionally invalid fixture used to prove rejection or failure shape

### Fixture Rules

Required fixture rules:

1. fixture content must be deterministic across hosts and repeated runs
2. fixture names must not encode local machine paths or transient timestamps
3. intentionally broken fixtures must be explicit and must document the targeted failure
4. fixture setup must not rely on implicit runtime discovery
5. fixture truth must come from declared input, not from mutable global state left by earlier tests

15 must consume lower-spec fixture models where they already exist.
It must not invent a parallel incompatible fixture vocabulary just to make runner code uniform.

---

## Validation Test Model

Validation tests prove the rules owned by 04.

Required responsibilities:

- execute validation fixtures before runtime boot or execution
- assert accepted versus rejected status deterministically
- assert required `DiagnosticCode` values, not only message substrings
- assert affected identities, profile boundaries, and phase legality when required by 04
- prove invalid graphs fail before generation or runtime use

Validation tests must consume the fixture intent defined by 04.
15 may provide harness rules, fixture loading rules, snapshot rules, and CI gating rules, but must not redefine dependency semantics.

Required validation gate kinds:

- missing required dependency rejection
- illegal cycle detection
- profile-specific dependency gap detection
- optional dependency legality versus forbidden fallback distinction
- runtime-query versus service-resolution boundary enforcement
- legacy-boundary crossing legality
- provenance and coverage checks for validation diagnostics

Validation acceptance rule:

```text
If invalid dependency structure reaches runtime execution, validation coverage is insufficient.
```

---

## Generation Test Model

Generation tests prove the rules owned by 03.

Required responsibilities:

- prove the same input produces the same artifacts and hashes
- prove stale or mismatched artifacts are rejected
- prove publication is all-or-nothing for artifact sets that must remain consistent
- prove generated artifacts are not treated as trustworthy before version and hash checks pass
- prove DebugMap generation remains aligned with generated runtime identities

Required generation gate kinds:

- deterministic artifact reproduction from identical input
- hash or version mismatch rejection
- artifact completeness checks
- stale artifact detection after input change
- DebugMap or manifest consistency verification
- regeneration after intentional model change

Generation baselines may be stored as golden files when the format is stable and reviewable.
If the format is too large or unstable for line-by-line review, the gate must store a deterministic summary plus hash rather than a raw opaque dump.

Generation acceptance rule:

```text
Generated output is accepted only when it is deterministic, internally consistent,
and traceable back to validated input.
```

---

## Runtime Behavior Test Model

Runtime behavior tests prove that runtime execution consumes verified input rather than discovering or repairing structure at runtime.

Required responsibilities:

- boot from verified input only
- prove runtime resolve, scope, lifecycle, command, value, scalar, and dynamic evaluation behavior against explicit fixtures
- prove runtime rejection when required input is absent, invalid, or mismatched
- prove no silent fallback into discovery, legacy, or runtime repair occurs in target paths

Required runtime gate kinds:

- boot acceptance from verified manifest and verified plan
- boot failure on invalid or stale artifacts
- service resolve correctness without component fallback
- scope creation and parentage correctness without transform-derived truth
- lifecycle ordering and dispatch correctness without registration scan
- command dispatch correctness without runtime string lookup
- value and scalar behavior correctness without stable-key fallback
- dynamic evaluation correctness with deterministic invalidation and dependency capture

Runtime behavior tests must prefer EditMode when no actual scene or frame-driven runtime is required.
PlayMode is required only when Unity runtime behavior is part of the rule being proved.

---

## Diagnostics Test Model

Diagnostics tests prove the rules owned by 11.

Required responsibilities:

- assert structured `KernelDiagnostic` output rather than free-form string output
- assert `DiagnosticCode`, domain, severity, and failure boundary
- assert DebugMap or source provenance coverage where required
- assert central sink routing and prohibition of direct subsystem Unity logging
- assert diagnostics determinism across repeated runs and supported profiles

Required diagnostics gate kinds:

- validation diagnostics snapshot tests
- boot failure diagnostics snapshot tests
- runtime failure diagnostics snapshot tests
- diagnostics sink routing tests
- duplicate or throttled diagnostics policy tests where applicable
- analyzer or code-search gates that catch forbidden direct `Debug.Log`, `Debug.LogWarning`, `Debug.LogError`, and `Debug.LogException` usage in subsystem code

Diagnostics acceptance rule:

```text
If a failure is observable only as host-formatted text and not as a stable structured diagnostic,
the diagnostics architecture is not protected.
```

---

## Performance Test Model

Performance tests prove the rules owned by 14.

Required responsibilities:

- assert forbidden runtime operations stay forbidden
- assert required profiler markers exist on covered paths
- assert allocation policy and explicit complexity rules for budgeted paths
- detect regressions relative to approved baselines when baselines are defined

Required performance gate kinds:

- no hierarchy scan in runtime target paths
- no registration scan in target lifecycle and dispatch paths
- no reflection discovery in target runtime paths
- no `Resources.Load` fallback in target runtime paths
- no string-key lookup or stable-key repair in target runtime paths
- required marker presence for covered hot, warm, boot, validation, and generation paths
- zero or bounded allocation assertions where lower specs require them

Performance rules must prefer structural assertions first.
Wall-clock thresholds are useful only after the test has already proven the absence of forbidden work and the presence of the required measurement point.

---

## Static Rule and Analyzer Test Model

Some architecture regressions should fail without running a full runtime scenario.

Required static gates include detection of:

- direct subsystem `Debug.Log*` calls outside approved sinks
- `FindObjectsByType` in kernel runtime paths
- `GetComponentsInChildren` or similar hierarchy discovery in runtime paths
- `Resources.Load` fallback in runtime paths
- `Transform.parent` ownership inference in runtime truth paths
- runtime stable-key or negative-ID repair
- runtime command dispatch by string or unverified key
- registration-wide handler scans or reflective executor discovery in target paths

Static-rule enforcement may be implemented by Roslyn analyzer, code-search gate, IL inspection, or another deterministic static method.

Required rule:

```text
If a regression pattern is cheap to detect statically, it should not wait for a late runtime failure.
```

---

## Legacy Compatibility Test Model

Legacy compatibility tests prove the rules owned by 13.

Required responsibilities:

- prove legacy usage remains explicit and profile-visible
- prove dependency direction does not invert back into v2-core depending on legacy fallback
- prove legacy adapters remain removable and identifiable
- prove development visibility does not become runtime validity
- prove missing target-kernel data is not repaired by calling back into legacy systems

Required legacy gate kinds:

- allowed adapter-shape verification
- forbidden fallback verification
- dependency direction verification
- profile visibility and diagnostics emission verification
- adapter metadata and removal-target verification
- no hidden legacy bridge on target hot paths

Legacy acceptance rule:

```text
Legacy may remain observable during migration.
It may not become required for target-kernel success.
```

---

## Integration Smoke Test Model

Integration smoke tests prove the architecture still works across boundaries after lower gates pass.

Required smoke coverage includes:

- authoring input to normalized contribution to validated input to generated artifact to bootable runtime chain
- diagnostics emission for representative failure boundaries
- one representative runtime flow across service, scope, lifecycle, command, and value domains
- one representative legacy-visible but non-fallback migration flow where applicable

Integration smoke tests must stay intentionally small.
They prove pipeline continuity, not exhaustive subsystem semantics.

---

## Golden File and Snapshot Policy

Golden files and snapshots are allowed only when the output is deterministic, reviewable, and semantically meaningful.

Allowed snapshot targets:

- validation diagnostics summaries
- generated artifact summaries or canonical text forms
- DebugMap summaries
- boot report summaries
- profiler marker presence reports
- legacy usage visibility reports

Forbidden snapshot targets by default:

- opaque binary dumps with unstable ordering
- raw runtime object graphs with host-specific handles
- snapshots that encode transient timestamps as semantic truth
- snapshots auto-updated during normal test execution

Snapshot rules:

1. snapshots must be reviewed changes, not hidden rewrites
2. snapshot approval must not happen automatically in normal CI
3. snapshots must assert stable codes, IDs, and categories before human-readable formatting
4. message text may be included, but not as the only assertion basis

---

## Determinism Policy

Determinism is required for plans, diagnostics, reports, and baseline comparisons.

Tests and generators must not treat the following as acceptable truth sources:

- current time
- random values without fixed seed and declared seed ownership
- filesystem enumeration order
- dictionary iteration order when not explicitly normalized
- reflection enumeration order
- Unity object discovery order when not explicitly normalized
- local absolute paths as semantic identifiers
- machine-specific line ending assumptions unless normalized

Required determinism rule:

```text
If two valid executions of the same fixture can disagree without a real model change,
the gate is underspecified.
```

Determinism checks are required for:

- validation diagnostics order when the same invalid structure is evaluated repeatedly
- generated artifact summaries and hashes
- DebugMap summaries
- report metadata except for fields explicitly declared as non-semantic run metadata

---

## Test Artifact and Report Policy

Test execution must produce artifacts that are machine-readable, human-readable, and reviewable.

### Standard Output Shape

The current repository already uses a timestamped output directory under `Logs/TestRuns`.
15 adopts that shape as the standard run-output root unless a future spec intentionally replaces it.

Minimum required run outputs:

- raw execution log
- machine-readable result file
- human-readable summary
- optional JSON or canonical report files when the suite requires deterministic structured comparison

Current baseline naming shape:

```text
Logs/TestRuns/{yyyyMMdd-HHmmss}_{Platform}_{Target}/
```

Required artifact rules:

1. result files must be preserved on failure
2. summary files must include enough information to classify failure without opening the full log first
3. suite reports must include fixture identity and profile identity
4. deterministic report content must separate semantic data from transient run metadata

---

## Failure Classification Policy

Gate failures must be classified explicitly.

Minimum failure kinds:

| Failure Kind | Meaning |
|---|---|
| `CompileFailure` | build or compile did not succeed |
| `DidNotStart` | test process or runner did not begin a real suite |
| `Timeout` | execution exceeded declared limit |
| `ZeroCollected` | suite started but did not collect required tests |
| `ValidationFailure` | invalid structure was not rejected or expected diagnostics were missing |
| `GenerationFailure` | deterministic generation, hash, or artifact consistency proof failed |
| `RuntimeFailure` | verified runtime behavior or required rejection behavior failed |
| `DiagnosticsFailure` | structured diagnostics, DebugMap coverage, or sink policy failed |
| `PerformanceFailure` | budget, allocation, marker, or forbidden-operation rule failed |
| `StaticRuleFailure` | banned pattern was detected statically |
| `LegacyBoundaryFailure` | legacy quarantine rule or removability rule failed |
| `InfrastructureFailure` | environment issue prevented meaningful result collection |

Required rule:

```text
An infrastructure problem must not be reported as architectural success.
```

If the runner cannot distinguish these classes automatically, the summary must still state which class is believed to have occurred and why.

---

## CI and CLI Gate Policy

15 defines the logical gate sequence.
It does not require one exact CI vendor.

### Minimum Gate Sequence

1. compile gate
2. spec-shape and doc-traceability gate
3. validation gate
4. generation determinism and artifact gate
5. runtime behavior gate
6. diagnostics gate
7. static-rule gate
8. performance gate
9. legacy-boundary gate
10. integration smoke gate

### Required Gate Rules

- a later gate must not convert an earlier failure into success
- skipped required gates must fail the acceptance result unless a higher-level profile explicitly marks them as not applicable and the reason is reviewable
- CI must report the failing gate kind, not only a generic red result
- local CLI entry points must align with CI gate meaning as closely as practical

### Existing Repository Anchors

- build command for the repository already exists as the documented solution build entry point
- Unity batch execution already exists through the shared runner
- result classification already distinguishes timeout, missing XML, zero collected, failed, and passed

15 therefore requires the future gate implementation to extend the existing runner model rather than fragment test entry points without reason.

---

## Unity EditMode and PlayMode Boundary

EditMode and PlayMode have different costs and different responsibilities.

Required rule set:

- pure validation, generation, diagnostics snapshot, and most static-rule checks belong in EditMode or .NET test environments
- PlayMode is required only when Unity runtime behavior, scene lifecycle, frame execution, or runtime object interaction is part of the rule under test
- no suite may be moved to PlayMode only to avoid deterministic setup discipline
- no suite may stay in EditMode if the rule being proved actually depends on runtime scene behavior

Default posture:

```text
Prefer the cheapest environment that can falsify the architectural claim.
```

---

## Test Isolation and Cleanup Policy

Architecture tests must not depend on residue from other tests.

Required cleanup rules:

- reset static caches that participate in the tested subsystem
- reset or isolate diagnostics collectors and sink state
- release temporary generated artifacts used only by the fixture
- unload temporary scenes or objects when the environment requires it
- clear pooled state if pooled state affects the test contract
- remove or isolate temp directories and temp assets created during the test

Forbidden pattern:

```text
Test B passes only because Test A already built or repaired global state.
```

Isolation failures are architecture risks because they hide missing explicit ownership and lifecycle reset rules.

---

## Mock, Fake, and Stub Policy

Test doubles are allowed only when they preserve the rule being proved.

Allowed usage:

- replacing expensive host dependencies when the kernel contract under test remains intact
- capturing diagnostics or marker emissions deterministically
- isolating one subsystem rule from unrelated runtime machinery

Forbidden usage:

- mocking away the very validation, generation, diagnostics, or runtime boundary that the test claims to prove
- stubbing legacy fallback to make the target kernel appear valid
- replacing structured diagnostics with string-only assertions because a real sink was inconvenient
- replacing explicit runtime plans with ad hoc manual wiring when the test claims to prove plan-driven behavior

Required rule:

```text
If a double removes the contract under test, the test is invalid.
```

---

## Required Test Suites

The following minimum suites are required to claim that Kernel v2 architecture is protected.

| Suite | Primary Layer | Main Responsibility |
|---|---|---|
| `KernelArchitectureDocTests` | `SpecShape` | doc index and marker traceability |
| `KernelIRTests` | `Validation` | IR identity and structural sanity used by later gates |
| `ModuleContributionTests` | `Validation` | contribution input legality and normalization expectations |
| `DependencyValidationTests` | `Validation` | dependency correctness and diagnostics expectations |
| `VerifiedPlanGenerationTests` | `Generation` | deterministic publication, hash, and artifact consistency |
| `BootManifestProfileTests` | `RuntimeBehavior` | boot acceptance and profile rejection rules |
| `ServiceGraphRuntimeTests` | `RuntimeBehavior` | service resolve and lifetime behavior |
| `ScopeGraphRuntimeTests` | `RuntimeBehavior` | scope handle, parentage, and query correctness |
| `LifecyclePlanRuntimeTests` | `RuntimeBehavior` | lifecycle ordering and dispatch behavior |
| `CommandCatalogRuntimeTests` | `RuntimeBehavior` | command identity, dispatch, and diagnostics |
| `ValueSchemaAndStoreTests` | `RuntimeBehavior` | value schema, save boundary, and no fallback rules |
| `ScalarRuntimeAndBindingTests` | `RuntimeBehavior` | scalar pipeline and binding rules |
| `DynamicValueEvaluationTests` | `RuntimeBehavior` | dynamic evaluation, invalidation, and caching rules |
| `DebugMapDiagnosticsTests` | `Diagnostics` | structured diagnostics, provenance, and sink policy |
| `UnityAuthoringBridgeTests` | `Generation` or `RuntimeBehavior` | authoring extraction determinism and direct-play input correctness |
| `LegacyCompatBoundaryTests` | `LegacyCompat` | quarantine, visibility, and removability |
| `PerformanceRuntimeRulesTests` | `PerformanceRule` | markers, allocations, and forbidden operations |
| `KernelIntegrationSmokeTests` | `IntegrationSmoke` | end-to-end verified input to runtime sanity |

These suite names may be adapted to the repository naming convention.
Their responsibilities may not be silently omitted.

---

## Required Test Cases by Spec

15 does not need to duplicate every lower-spec test case verbatim.
It must define the minimum executable coverage classes that protect each spec family.

### Required Coverage Map

| Owner Spec | Minimum Executable Coverage |
|---|---|
| `00` | root architecture gates remain present and traceable |
| `01` | identity normalization and traceable source mapping remain deterministic |
| `02` | contribution declarations normalize without hidden runtime mutation |
| `03` | deterministic generation, hash checks, and stale-artifact rejection |
| `04` | missing dependency, illegal cycle, invalid profile, and expected diagnostics |
| `05` | boot accepts only verified inputs and rejects invalid profile or artifact combinations |
| `06` | service resolve follows verified graph without component fallback or hidden reflection discovery |
| `07` | scope graph uses explicit handles and relations rather than transform-derived truth |
| `08` | lifecycle execution follows plan ordering without registration scan repair |
| `09` | command dispatch uses verified identity and emits structured failure diagnostics |
| `10` | value store rejects runtime stable-key fallback and preserves diagnostics visibility |
| `10-1` | scalar pipeline stays deterministic, typed, and budget-compliant |
| `10-2` | dynamic evaluation captures dependencies deterministically and rejects invalidation regressions |
| `11` | diagnostics remain structured, coded, routed, and DebugMap-backed |
| `12` | authoring extraction and direct-play input remain deterministic and explicit |
| `13` | legacy bridges remain explicit, profile-visible, bounded, and removable |
| `14` | forbidden operations remain absent and covered by markers or analyzers |

### Mandatory Regression Families

At minimum, the executable suite must fail on reintroduction of:

- direct `Debug.LogError` style subsystem logging outside approved sinks
- `FindObjectsByType` in target runtime paths
- `GetComponentsInChildren` style runtime discovery in target paths
- `Transform.parent` ownership inference in target runtime truth paths
- registration-wide lifecycle or handler scans in target paths
- `Resources.Load` fallback in target runtime paths
- runtime stable-key lookup or negative-ID repair in target runtime paths
- string-key command dispatch in target runtime paths
- legacy fallback used to make missing target data appear valid
- stale or mismatched generated artifacts consumed as if verified
- missing profiler markers on required hot or boot paths
- diagnostics assertions that rely only on message text instead of code and structure

---

## Regression Policy

Regression is not limited to user-visible breakage.
Architectural regression counts as failure even when gameplay still appears correct.

Required regression rules:

1. a new fallback path is a regression even if it fixes a temporary missing-data scenario
2. a removed diagnostics code or degraded provenance field is a regression if required by lower specs
3. a missing profiler marker on a covered path is a regression
4. a new static-rule hit is a regression
5. a formerly deterministic artifact or report becoming nondeterministic is a regression
6. a legacy bridge losing removability or visibility is a regression

Regression baselines must be explicit and reviewable.
They must not be rewritten automatically during ordinary CI execution.

---

## Forbidden Testing Patterns

The following patterns are forbidden by default:

- using sleeps instead of deterministic completion conditions when a deterministic condition is available
- asserting only human-readable message text when stable `DiagnosticCode` or structured fields exist
- approving snapshot changes automatically during normal test runs
- using PlayMode for a pure data-validation rule with no runtime need
- sharing mutable static state across tests without explicit reset
- relying on test execution order
- hiding infrastructure failures by reporting them as skipped success
- using legacy fallback or runtime discovery as test setup for a target-kernel success assertion
- mocking away verified-plan usage while claiming to test plan-driven runtime behavior
- accepting wall-clock-only performance checks without proving the structural absence of forbidden work

---

## Acceptance Criteria

Kernel v2 is not protected until all of the following statements are true:

- the repository contains executable gates for validation, generation, runtime behavior, diagnostics, performance, static rules, legacy compatibility, and integration smoke
- the suite can prove failure-mode behavior, not only success-path behavior
- the suite can detect reintroduction of runtime discovery, fallback, direct Unity logging, stale artifact usage, or missing profiler coverage
- the suite preserves deterministic results for diagnostics, artifacts, and baseline comparisons
- CI or CLI output reports the failing gate class with reviewable evidence
- lower-spec rules are exercised without being redefined by the test harness layer

Required final rule:

```text
The architecture is accepted only when it can prove that it would reject its own regressions.
```

---

## Test Cases

| Test Case | Purpose | Execution Note |
|---|---|---|
| `TC-15-01` | Confirm 15 defines distinct layers for validation, generation, runtime behavior, diagnostics, performance, static rules, legacy compatibility, and integration smoke. | Review this file. |
| `TC-15-02` | Confirm 15 states compilation is necessary but not sufficient. | Review the Purpose and Test Philosophy sections. |
| `TC-15-03` | Confirm 15 requires deterministic artifacts, diagnostics, and reports. | Review the Generation, Diagnostics, and Determinism sections. |
| `TC-15-04` | Confirm 15 requires executable failure-mode coverage and not only success-path coverage. | Review the Purpose, Core Problem, and Acceptance Criteria sections. |
| `TC-15-05` | Confirm 15 requires gates for runtime discovery, fallback, direct Unity logging, stale artifacts, missing profiler markers, and legacy regressions. | Review the Required Test Cases by Spec and Regression Policy sections. |
| `TC-15-06` | Confirm 15 adopts the existing runner and `Logs/TestRuns` output shape as the current repository anchor instead of inventing a parallel reporting model. | Review Current Test Infrastructure Observations and Test Artifact and Report Policy. |

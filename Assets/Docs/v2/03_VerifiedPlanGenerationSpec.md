# Verified Plan Generation Specification

## Document Status

- Document ID: 03_VerifiedPlanGenerationSpec
- Status: Draft
- Role: generation contract from validated KernelIR to VerifiedKernelPlan and generated artifacts
- Depends on:
  - [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md)
  - [01_KernelIRSpec.md](01_KernelIRSpec.md)
- Provides foundation for:
  - 04_DependencyValidationSpec.md
  - 05_BootManifestAndProfileSpec.md
  - 06_ServiceGraphRuntimeSpec.md
  - 07_ScopeGraphRuntimeSpec.md
  - 08_LifecyclePlanSpec.md
  - 09_CommandCatalogRuntimeSpec.md
  - 10_ValueSchemaAndStoreSpec.md
  - 11_DebugMapAndDiagnosticsSpec.md

### Ownership

This document owns the verified plan generation pipeline, artifact set contract, hash and version policy, deterministic generation rules, DebugMap generation contract, generated code / asset split, and post-generation consistency requirements.

This document does not own dependency validation algorithms, runtime storage layout, boot manifest schema, or runtime execution behavior.

---

## Purpose

This specification defines how validated KernelIR is transformed into VerifiedKernelPlan and its generated artifact set.

03 is the generation trust boundary.
It is responsible for ensuring that generated runtime artifacts are derived from a validated KernelIR, are internally consistent, and can be verified against hash and version metadata before any runtime boot attempt.

KernelIR is the normalized authority.
VerifiedKernelPlan is the runtime execution input.
Generated artifacts are projections, not source of truth.

This specification exists to prevent the following failure modes:

- partial artifact sets being treated as valid
- generated output being trusted without hash/version compatibility checks
- editor and CLI generation producing divergent outputs
- stale artifacts surviving after source changes
- runtime fallback being used to repair generation failures

If a runtime plan cannot be traced back to validated KernelIR through this generation contract, it is not a valid target-kernel artifact.

---

## Scope

This specification defines:

- generation inputs and context
- generation pipeline stages
- VerifiedKernelPlan output contract
- artifact set composition
- plan header semantics
- hash and version policy
- deterministic generation requirements
- DebugMap generation requirements
- generated code / generated asset split
- post-generation consistency checks
- editor / CLI / CI parity requirements
- failure policy for generation

This specification intentionally does not define:

- dependency validation algorithms
- phase-aware cycle detection
- runtime service cache layout
- scope handle memory layout
- command execution algorithm
- value store storage layout
- boot manifest schema
- runtime boot policy details

This document must not become a runtime implementation document.
It is a generation contract, not a runtime architecture spec.

---

## Relationship to Other Specs

| Spec | Relationship |
|---|---|
| 00_KernelArchitectureOverviewSpec.md | Defines the root architecture contract and non-negotiable runtime constraints |
| 01_KernelIRSpec.md | Defines the normalized input model consumed by this generator |
| 04_DependencyValidationSpec.md | Defines dependency semantics and validation rules applied after generation handoff |
| 05_BootManifestAndProfileSpec.md | Consumes generated plan artifacts and boot policy references |
| 06_ServiceGraphRuntimeSpec.md | Consumes ServiceGraphPlan projection |
| 07_ScopeGraphRuntimeSpec.md | Consumes ScopeGraphPlan projection |
| 08_LifecyclePlanSpec.md | Consumes LifecyclePlan projection |
| 09_CommandCatalogRuntimeSpec.md | Consumes CommandCatalogPlan projection |
| 10_ValueSchemaAndStoreSpec.md | Consumes ValueSchemaPlan projection |
| 11_DebugMapAndDiagnosticsSpec.md | Consumes KernelDebugMap projection |

03 turns validated KernelIR into a verified artifact set.
Lower specs must consume the outputs defined here rather than reconstructing them from runtime behavior.

---

## Current Generation Observations

### Observation Traceability

Current generation observations must remain traceable to source code, generated outputs, editor tooling, or migration notes.

When this document is updated, observations that no longer match the current codebase must be removed or moved to legacy migration notes.

| Observation | Evidence Type | Expected Downstream Spec |
|---|---|---|
| Current generation is domain-specific and fragmented | Source | 03 |
| Flow compiler stores source hash and build timestamp in a single asset | Source | 03, 05 |
| Generated code exists but no unified artifact consistency model exists | Source | 03 |
| Runtime fallbacks still exist in registry-style locators | Source | 03, 05, 10 |
| No boot-time hash/version validation is present as a unified contract | Source | 03, 05 |

### Representative Anchors

- [GameStateCodeGenerator.cs](../../Game/Scripts/Flow/Editor/GameStateCodeGenerator.cs) - example of domain-specific code generation
- [MaterialFxPropertyCodeGenerator.cs](../../GameLib/Script/Shader/Core/MaterialFx/Editor/MaterialFxPropertyCodeGenerator.cs) - example of domain-specific code generation
- [RoomMapTileCodeGenerator.cs](../../GameLib/Script/Project/Scene/RoomMap/Editor/RoomMapTileCodeGenerator.cs) - example of domain-specific code generation
- [FlowCompiler.cs](../../GameLib/Script/Project/Flow/Compiler/FlowCompiler.cs) - current source hash computation and compiled asset emission
- [FlowProgramAssetSO.cs](../../GameLib/Script/Project/Flow/Core/FlowProgramAssetSO.cs) - compiled asset with source hash and build timestamp fields
- [VarKeyRegistryLocator.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs) - runtime fallback pattern that must not become a trust boundary
- [VarIdResolver.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs) - runtime-only negative ID fallback pattern

### Current Gaps

- generation is domain-scoped rather than plan-scoped
- hash metadata is partial and domain-specific
- build timestamp exists as metadata in at least one compiled asset, but it is not a compatibility proof
- artifact set consistency is not verified as a unit
- partial outputs can still exist as valid-looking assets
- editor and runtime fallback behavior are not governed by a single generation contract

---

## Generation Authority

03 defines the transformation from validated KernelIR to VerifiedKernelPlan and generated artifact outputs.

KernelIR is the only structural input authority.
Runtime discovery must not be used to compensate for missing generation inputs.
Generated artifacts must not invent new structural identities.

Generation is not runtime execution.
Generation does not create runtime instances.
Generation does not resolve live services.
Generation does not query scene state to fill missing plan data.

The core generation rule is:

```text
Validated KernelIR
  -> deterministic projection
  -> verified plan and artifact set
```

The reverse direction is forbidden.

---

## Generation Inputs

Generation must accept only immutable, verified inputs.

Required inputs:

- validated KernelIR
- generator version
- target profile
- target platform or build family when relevant
- schema or format version expectations
- registry identity inputs referenced by KernelIR

Optional inputs:

- editor-facing build notes
- migration labels
- diagnostic verbosity settings

The following are not valid generation inputs for trust purposes:

- current time
- absolute local paths
- asset enumeration order
- Unity object instance identity
- runtime service instances
- runtime scope instances
- runtime fallback data

Generation inputs must be complete enough that a missing required input causes a generation failure, not a repaired output.

---

## Generation Outputs

The generation pipeline produces a verified artifact set.

Required outputs:

- VerifiedKernelPlan
- KernelPlanHeader
- KernelDebugMap
- generated runtime assets
- generated code artifacts when applicable
- artifact manifest
- generation report

Optional outputs:

- editor diagnostics cache
- migration review notes
- non-runtime inspection artifacts

All outputs must be derived from the same generation run and the same validated KernelIR input set.

Outputs from different runs must never be merged into a single trusted artifact set unless a lower spec explicitly defines a merge protocol and consistency rule.

---

## Generation Pipeline

### 1. Preflight

The generator must verify that:

- KernelIR is validated and format-compatible
- generator version is supported
- required schema versions are available
- required input references are present
- the intended target profile is known

Preflight failure is a hard generation failure.

### 2. Canonical Projection

The generator projects KernelIR into runtime-facing shapes.

This stage must:

- preserve source provenance
- preserve owner module information
- preserve profile availability
- preserve identity domain boundaries
- preserve dependency references for later validation handoff

This stage must not:

- create runtime instances
- resolve live services
- invent fallback identities
- repair missing required inputs by discovery

### 3. Artifact Emission

The generator emits plan artifacts, debug maps, code artifacts, and any required registry or metadata assets.

Emission must occur into a staging area until consistency is proven.

The generator must not make partially emitted artifacts appear valid.

### 4. Hash Assembly

The generator computes the artifact hash set and attaches it to the generated header and manifest.

Hash assembly must cover semantic content only.

### 5. Post-Generation Consistency Check

The generator must verify that the emitted artifact set is internally consistent.

If the artifact set is not consistent, the generation run fails and the staged outputs are not committed as trusted artifacts.

### 6. Commit

Only after the consistency check succeeds may the generator publish the artifact set as the verified output of the run.

Commit failure after emission is a generation failure, not a recoverable runtime event.

---

## Plan Header Semantics

`KernelPlanHeader` is the top-level compatibility contract for a verified artifact set.

The header fields used by 00 are interpreted as follows:

- `PlanId`: stable identifier of the plan bundle or generation target
- `FormatVersion`: generation format version
- `SourceHash`: hash of the normalized KernelIR source state
- `RegistryHash`: hash of the registry identity space relevant to the plan
- `GeneratedHash`: hash of the emitted runtime projections and generated artifacts
- `DebugMapHash`: hash of the generated debug map content

The header must be derivable from the generation run and must not depend on non-semantic data.

The header must not use build time, local machine path, or asset import order as trust input.

If the header cannot be computed consistently, the generation output is invalid.

---

## Artifact Set and Consistency Model

The kernel pipeline produces multiple artifacts, but they must represent the same source state.

The following artifacts form the consistency set:

- normalized KernelIR input hash reference
- VerifiedKernelPlan
- KernelDebugMap
- generated runtime assets
- generated code artifacts
- registry or identity projection assets when applicable
- artifact manifest

A target runtime must not execute a partial artifact set.

A valid artifact set must satisfy:

- all artifacts share the same source hash or a compatible hash chain
- all artifacts declare compatible format versions
- the DebugMap corresponds to the same ID space as the runtime plan
- the registry projection corresponds to the same ValueKey / CommandType / Service ID space
- the manifest references exactly one compatible plan set

If consistency cannot be proven, generation must fail and the outputs must be treated as stale.

---

## Hash and Version Policy

Hash and version policy must prove semantic compatibility.

Hash inputs should include:

- validated KernelIR content
- module IDs and versions
- identity assignments relevant to the projection
- dependency edges relevant to the projection
- profile-affecting configuration
- generator version and supported format version

Hash inputs must not include:

- generation timestamp
- absolute local paths
- editor-only display order unless it changes semantic IDs
- formatting noise
- runtime instance identity

The `buildTimestamp` pattern used by existing compiled assets is diagnostic metadata only.
It is not a compatibility proof and must not participate in the trust boundary.

The plan header and artifact manifest must both be able to express incompatibility when a source, registry, or debug map input changes.

---

## Deterministic Generation

Given the same validated KernelIR, generator version, profile, and target family, generation must produce semantically identical output.

Generation must not depend on:

- Unity object enumeration order
- dictionary iteration order
- reflection order
- asset import timing
- current time
- random values
- machine-local paths

Recommended ordering priority:

1. explicit semantic order field
2. owner module ID
3. stable ID
4. stable name
5. source location

Determinism is a requirement for reproducible hashes, stable diffs, and CI parity.

If nondeterministic output is detected, the run must fail rather than silently accept the output.

---

## DebugMap Generation and Provenance

DebugMap is not a side effect.
It is a required artifact for verifying traceability of ID / Handle based design.

Generation must produce debug data sufficient to trace each runtime-facing identifier back to:

- stable debug name
- owner module
- source location
- profile availability
- legacy origin when applicable

DebugMap generation must share the same source hash as the rest of the artifact set.

Missing DebugMap coverage is a generation failure in Development and Test profiles.
In Release profile, debug metadata may be reduced only if the resulting artifact set can still report fatal errors with stable error codes and numeric identifiers.

DebugMap must not be reconstructed at boot as a substitute for generation-time provenance.

---

## Generated Code / Asset Split

Generated code and generated assets are different artifact kinds and must be tracked separately.

Generated code may include:

- ID tables
- strongly typed constants
- projection glue
- compile-time helpers

Generated assets may include:

- plan assets
- debug map assets
- registry projection assets
- manifest assets

Neither generated code nor generated assets are source of truth.
Both are projections of the same verified generation run.

Manual edits to generated code or generated assets are forbidden unless a lower spec defines a controlled migration path and the artifact remains marked as non-authoritative.

---

## Editor / CLI / CI Parity

The generation core must be shared by Editor, CLI, and CI hosts.

Host-specific wrappers may differ, but they must not alter generation semantics.

Parity requirements:

- the same KernelIR must produce the same artifact set regardless of host
- the same generator version must produce the same hashes regardless of host
- the same diagnostics must be reported for the same failure conditions
- the same artifact manifest must be emitted for the same input set

If Editor and CLI outputs differ, the generation core is not valid.

If CI cannot reproduce the Editor result, the generation run is not trustworthy.

---

## Validation Handoff

03 owns generation-time structural checks, not the full dependency validation model.

Generation-time checks include:

- supported KernelIR / plan format version
- required input completeness
- artifact set completeness
- hash compatibility
- debug coverage completeness
- deterministic output verification
- manifest completeness

04_DependencyValidationSpec.md owns:

- dependency cycle detection
- phase-aware dependency rules
- cross-graph dependency severity policy
- detailed dependency conflict classification

03 must provide enough structure and metadata for 04 to validate the generated outputs without reconstructing missing provenance.

---

## Failure Policy

Generation failure must be explicit.

Required failure conditions include:

- stale KernelIR input
- unsupported format version
- missing required artifact input
- hash mismatch
- inconsistent artifact set
- nondeterministic output
- missing required debug coverage
- generator version incompatibility
- invalid manifest completeness

Generation failure must not create a partially trusted artifact set.
Generation failure must not silently fall back to empty assets, runtime discovery, or negative IDs.

Failures must be reported as structured diagnostics.

The generator may write temporary staging outputs for inspection, but staging outputs are not trusted until the consistency check succeeds.

---

## Forbidden Patterns

The following are forbidden in target generation paths:

- using build timestamps as compatibility proof
- using absolute local paths as hash inputs
- treating generated code as source of truth
- treating generated assets as source of truth
- repairing missing required inputs by runtime discovery
- repairing missing required inputs by empty fallback assets
- generating different output in Editor and CLI hosts
- allowing partial artifact sets to appear valid
- using runtime fallback to create IDs that were not projected from KernelIR
- silently accepting nondeterministic output
- updating trusted artifacts before post-generation consistency passes

These are structural violations, not stylistic preferences.

---

## Migration Notes

Current code provides partial precedents, but not the target contract.

- [GameStateCodeGenerator.cs](../../Game/Scripts/Flow/Editor/GameStateCodeGenerator.cs), [MaterialFxPropertyCodeGenerator.cs](../../GameLib/Script/Shader/Core/MaterialFx/Editor/MaterialFxPropertyCodeGenerator.cs), and [RoomMapTileCodeGenerator.cs](../../GameLib/Script/Project/Scene/RoomMap/Editor/RoomMapTileCodeGenerator.cs) are examples of domain-specific generation, not a unified plan-generation system.
- [FlowCompiler.cs](../../GameLib/Script/Project/Flow/Compiler/FlowCompiler.cs) and [FlowProgramAssetSO.cs](../../GameLib/Script/Project/Flow/Core/FlowProgramAssetSO.cs) show a source-hash and build-timestamp pattern for one domain, but they do not yet establish a whole-system artifact consistency model.
- [VarKeyRegistryLocator.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs) and [VarIdResolver.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs) demonstrate why generation and trust boundaries must fail closed instead of inventing runtime IDs or empty assets.

The target architecture may reuse ideas from these systems, but it must not reuse their fallback behavior as a new contract.

---

## Acceptance Criteria

03 is complete when it defines:

- generation inputs and outputs
- generation pipeline stages
- artifact set composition
- plan header semantics
- hash and version policy
- deterministic generation rules
- DebugMap generation requirements
- generated code / asset split
- editor / CLI / CI parity requirements
- validation handoff to 04 and 05
- failure policy
- forbidden patterns

03 is not complete if any runtime storage detail, dependency validation algorithm, or boot manifest schema has escaped into the specification.

## Test Cases

| Test Case | Purpose | Verification |
|---|---|---|
| TC-03-01 | Confirm preflight fails closed on unsupported versions or incomplete inputs. | The preflight stage must require validated KernelIR, generator version, schema compatibility, and target profile. |
| TC-03-02 | Confirm artifact sets are all-or-nothing. | The artifact set and consistency model must require the whole set to share the same source state. |
| TC-03-03 | Confirm the same inputs produce deterministic hashes across hosts. | The deterministic generation section must forbid time, path, order, and random dependence. |
| TC-03-04 | Confirm DebugMap matches the same ID space as the verified plan. | The DebugMap generation section must preserve provenance and coverage. |
| TC-03-05 | Confirm stale or partial artifacts cannot be trusted for boot. | The failure policy and forbidden patterns sections must reject partial output and silent fallback. |
| TC-03-06 | Confirm Editor, CLI, and CI share the same generation core. | The parity section must prohibit semantic divergence between hosts. |

---

## Final Position

Verified plan generation is a trust boundary, not a convenience layer.

The generation system must prove that a verified artifact set is internally consistent, provenance-backed, deterministic, and version-compatible before the runtime ever sees it.

The runtime may only execute verified inputs.
The editor and CI must prove that those inputs are trustworthy.
Generated code and assets are projections, not authority.

03 exists to make stale or partial output impossible to mistake for a verified plan.
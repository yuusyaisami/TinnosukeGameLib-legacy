# Verified Plan Generation Specification

## Document Status

- Document ID: 03_VerifiedPlanGenerationSpec
- Status: Draft
- Role: defines how validated KernelIR and module-derived inputs become VerifiedKernelPlan and verified runtime artifact sets
- Depends on:
  - [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md)
  - [01_KernelIRSpec.md](01_KernelIRSpec.md)
  - [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md)
- Provides foundation for:
  - 04_DependencyValidationSpec.md
  - 05_BootManifestAndProfileSpec.md
  - 06_ServiceGraphRuntimeSpec.md
  - 07_ScopeGraphRuntimeSpec.md
  - 08_LifecyclePlanSpec.md
  - 09_CommandCatalogRuntimeSpec.md
  - 10_ValueSchemaAndStoreSpec.md
  - 10_2_DynamicValueEvaluationSpec.md
  - 11_DebugMapAndDiagnosticsSpec.md
  - 14_PerformanceBudgetAndRuntimeRulesSpec.md
  - 15_TestAndValidationSpec.md

### Revision Note

This revision consolidates the duplicated status, purpose, and scope blocks that had diverged at the top of 03.

It preserves one authoritative metadata section for the generation trust boundary and keeps the broader dependency and artifact-consistency contract as the normative version.

### Ownership

This specification owns the generation pipeline and artifact consistency contract.
It defines what must be true before a generated plan may be called VerifiedKernelPlan.

This specification owns:

- generation pipeline stages
- input artifact requirements
- output artifact set requirements
- VerifiedKernelPlan definition
- pre-generation validation gates
- KernelIR normalization gate expectations at generation time
- projection generation rules
- post-generation validation gates
- artifact set consistency
- hash, version, and format compatibility policy
- deterministic generation rules
- DebugMap generation requirements
- generated code and generated asset policy
- stale artifact detection
- Editor / CLI / CI parity
- incremental generation constraints
- generation failure behavior
- generation diagnostics requirements

This specification does not own:

- KernelIR node and edge model details
- ModuleContribution authoring details
- dependency validation algorithms
- runtime service graph implementation
- runtime scope graph implementation
- runtime command execution implementation
- runtime value storage implementation
- BootManifest final schema
- DebugMap final serialized layout

This specification owns the generation trust boundary.
It does not own the final runtime implementation of each generated plan.

---

## Purpose

This specification defines the process that turns validated KernelIR into VerifiedKernelPlan and its associated generated artifacts.

Generated does not mean verified.
Only a plan that passes all required generation gates may be used by the target runtime.

03 is the generation trust boundary.

KernelIR is the normalized authority.
VerifiedKernelPlan is the runtime execution input.
Generated code and generated assets are execution artifacts, not source of truth.

The purpose of 03 is to ensure that generated runtime inputs are:

- deterministic
- validated
- traceable
- hash-compatible
- version-compatible
- DebugMap-backed
- safe for runtime execution

Generation is a verification pipeline, not a file output step.

---

## Scope

This specification defines:

- generation pipeline overview and stages
- validated input requirements
- output artifact set requirements
- VerifiedKernelPlan minimum definition
- pre-generation validation gates
- post-generation validation gates
- artifact set consistency model
- plan and artifact header semantics
- hash and version compatibility model
- deterministic generation rules
- generated code policy
- generated asset policy
- DebugMap generation requirements
- stale artifact detection
- profile-specific generation constraints
- Editor / CLI / CI parity requirements
- incremental generation policy
- failure policy
- diagnostics requirements
- forbidden patterns

---

## Non-Goals

This specification does not define:

- KernelIR node model details
- module contribution authoring schema
- dependency validation algorithms
- runtime service resolver implementation
- runtime scope graph implementation
- command executor invocation algorithm
- ValueStore storage layout
- DebugMap final serialized format
- BootManifest final schema

This specification must not define runtime fallback behavior as a substitute for invalid generated artifacts.

---

## Relationship to Other Specs

| Spec | Relationship |
|---|---|
| [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md) | Defines the root architecture, trust boundary, and Verified Plan requirement |
| [01_KernelIRSpec.md](01_KernelIRSpec.md) | Provides KernelIR as the normalized structural input authority |
| [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md) | Defines the declarative module contribution contract that feeds KernelIR normalization |
| 04_DependencyValidationSpec.md | Defines validation algorithms whose output must be accepted before generation may continue |
| 05_BootManifestAndProfileSpec.md | Defines how BootManifest references verified artifact sets and enforces boot-time policy |
| 06_ServiceGraphRuntimeSpec.md | Consumes ServiceGraphPlan generated under this specification |
| 07_ScopeGraphRuntimeSpec.md | Consumes ScopeGraphPlan and RuntimeQueryPlan generated under this specification |
| 08_LifecyclePlanSpec.md | Consumes LifecyclePlan generated under this specification |
| 09_CommandCatalogRuntimeSpec.md | Consumes CommandCatalogPlan generated under this specification |
| 10_ValueSchemaAndStoreSpec.md | Consumes ValueSchemaPlan and ValueInitPlan generated under this specification |
| 10_2_DynamicValueEvaluationSpec.md | Consumes DynamicEvaluationPlan and ReactiveEvaluationPlan generated under this specification |
| 11_DebugMapAndDiagnosticsSpec.md | Defines the runtime-facing diagnostics contract for DebugMap generated under this specification |
| 14_PerformanceBudgetAndRuntimeRulesSpec.md | Defines generation and runtime-loading budgets that consume this pipeline's outputs |
| 15_TestAndValidationSpec.md | Defines tests and CI gates that prove generation correctness |

03 receives validated inputs and produces verified outputs.
Lower specs must consume the outputs defined here rather than reconstructing them from runtime behavior.

---

## Assembly Definition and Compile Boundary Expectations

The intended assembly home for verified plan generation is `GameLib.Kernel.Generation`.
Editor-only generated file writing and regeneration tooling belong in `GameLib.Kernel.Generation.Editor`.
Detailed dependency matrices remain owned by [17_AssemblyDefinitionAndCompileBoundarySpec.md](17_AssemblyDefinitionAndCompileBoundarySpec.md).

Required compile-boundary rules for 03:

- `GameLib.Kernel.Generation` must remain separate from runtime subsystem implementations
- the generation core should remain Unity-free and use `noEngineReferences: true`
- deterministic artifact generation logic must not depend on MonoBehaviour, ScriptableObject, or Unity scene traversal
- file-system write paths, asset refresh hooks, and editor regeneration commands belong in editor-only assemblies, not in generation core

If generation logic cannot be expressed without Unity runtime or editor objects in the core assembly, the 03 boundary has been violated.

---

## Core Problem

The target architecture removes runtime discovery.
This shifts risk from runtime inference to generated artifact correctness.

Therefore, generation must solve the following risks before runtime boot:

- missing generated entries
- stale generated code
- stale generated assets
- mismatched DebugMap
- registry and plan ID-space mismatch
- profile-specific artifact mismatch
- partial generation output
- non-deterministic output order
- hidden fallback after generation failure

The generation pipeline exists to prevent these risks from reaching runtime.

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

- [GameStateCodeGenerator.cs](../../Game/Scripts/Flow/Editor/GameStateCodeGenerator.cs) - domain-specific code generation example
- [MaterialFxPropertyCodeGenerator.cs](../../GameLib/Script/Shader/Core/MaterialFx/Editor/MaterialFxPropertyCodeGenerator.cs) - domain-specific code generation example
- [RoomMapTileCodeGenerator.cs](../../GameLib/Script/Project/Scene/RoomMap/Editor/RoomMapTileCodeGenerator.cs) - domain-specific code generation example
- [TreeCodeGeneratorBase.cs](../../GameLib/Script/Common/_Editor/CodeGen/TreeCodeGeneratorBase.cs) - shared generated code emission pattern
- [FlowCompiler.cs](../../GameLib/Script/Project/Flow/Compiler/FlowCompiler.cs) - source hash computation and compiled artifact emission
- [FlowProgramAssetSO.cs](../../GameLib/Script/Project/Flow/Core/FlowProgramAssetSO.cs) - compiled artifact with source hash, build timestamp, and report fields
- [VarKeyRegistryLocator.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs) - runtime fallback pattern that must not become a trust boundary
- [VarIdResolver.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs) - runtime-only negative ID fallback pattern
- [CommandCatalogLocator.cs](../../GameLib/Script/Common/Commands/VNext/Catalog/CommandCatalogLocator.cs) - asset locator pattern that must not become a verification substitute
- [CollisionIdCatalogLocator.cs](../../GameLib/Script/Collision/Core/CollisionIdCatalogLocator.cs) - asset locator pattern that must not become a verification substitute

### Current Gaps

- generation is domain-scoped rather than plan-scoped
- hash metadata is partial and domain-specific
- build timestamp exists as diagnostic metadata, not as compatibility proof
- artifact set consistency is not verified as a unit
- partial outputs can still exist as valid-looking assets
- stale outputs are not blocked by a unified artifact header or manifest contract
- editor and runtime fallback behavior are not governed by a single generation contract

---

## VerifiedPlan Definition

A VerifiedKernelPlan is a runtime execution input derived from validated KernelIR and accepted by the generation pipeline.

A GeneratedPlan is a plan that was produced by generation.

A VerifiedKernelPlan is a generated plan that passed all required validation and consistency checks.

Only VerifiedKernelPlan may be used by runtime boot.

A plan is verified only if:

- KernelIR is normalized and validated
- required pre-generation validation passes
- all required runtime projections are generated
- all required artifacts belong to one consistent artifact set
- hash and version compatibility checks pass
- DebugMap exists at the required profile level
- required source locations are present
- required dependency validation has already accepted the input state
- no forbidden fallback path is required to make the artifact set usable

The term VerifiedKernelPlan refers to the verified plan root together with its referenced artifact set.
It is not limited to a single file or a single generated asset.

---

## Generation Pipeline Overview

```text
Authoring Inputs
  ↓
ModuleContributionData
  ↓
KernelIR
  ↓ Pre-Generation Validation
Validated KernelIR
  ↓ Projection Generation
Generated Runtime Projections
  ↓ Post-Generation Validation
Consistent Artifact Set
  ↓ Hash / Version / DebugMap Check
VerifiedKernelPlan
```

Pipeline stages:

1. collect input artifacts
2. confirm validated KernelIR availability
3. run pre-generation validation gates
4. enforce KernelIR normalization gate
5. generate runtime projections
6. generate DebugMap
7. generate code artifacts
8. generate asset artifacts
9. run post-generation validation
10. build artifact consistency report and publish verified state

Generation must use a staging area until verification succeeds.
Trusted publication happens only after the whole artifact set is accepted.

---

## Input Artifact Model

Generation inputs are not arbitrary editor state.
They are explicit, versioned, hashable inputs to the verification pipeline.

Required inputs may include:

- validated KernelIR
- generator version
- target profile
- target platform or build family when relevant
- registry inputs referenced by KernelIR
- source location table required for DebugMap generation
- module version or module provenance metadata referenced by KernelIR

Optional inputs may include:

- module contribution provenance records
- legacy migration metadata
- editor-facing build notes
- diagnostic verbosity settings
- generation settings that affect projection semantics

Rules:

- all trust-relevant input artifacts must be versioned and hashable
- unversioned input cannot contribute runtime behavior
- raw ModuleContribution data must not bypass KernelIR normalization
- validated KernelIR is the only structural input authority
- module-derived provenance may contribute traceability and input digest coverage, but must not redefine KernelIR structure

The following are not valid generation inputs for trust purposes:

- current time
- absolute local paths
- editor selection state
- editor foldout state
- Unity object instance identity
- runtime service instances
- runtime scope instances
- runtime fallback data

Generation inputs must be complete enough that a missing required input causes generation failure, not repaired output.

---

## Output Artifact Set

Generation produces an artifact set, not isolated files.

An artifact set may include:

- VerifiedKernelPlan root artifact
- KernelPlanHeader
- ArtifactSetManifest
- ServiceGraphPlan
- ScopeGraphPlan
- LifecyclePlan
- CommandCatalogPlan
- ValueSchemaPlan
- ValueInitPlan
- DynamicEvaluationPlan
- ReactiveEvaluationPlan
- RuntimeQueryPlan
- KernelDebugMap
- generated C# files
- generated runtime assets
- generation report
- validation report

Optional outputs may include:

- editor diagnostics cache
- migration review notes
- non-runtime inspection artifacts

Runtime must not execute a partial artifact set.

If any required artifact is missing, stale, or hash-incompatible, the entire artifact set is invalid.

---

## Pre-Generation Validation

Pre-generation validation runs before runtime projections are created.

It must check at least:

- KernelIR format version compatibility
- required root nodes exist
- duplicate IDs are absent
- required owner modules are present
- required source locations are present
- unresolved authoring aliases are absent
- unresolved command authoring keys are absent from runtime-facing identity paths
- unresolved stable value keys are absent from runtime-facing identity paths
- profile availability declarations are valid
- required module dependency shape has already passed its validation gate
- forbidden legacy leakage is absent from the validated input state

03 does not own the dependency validation algorithm.
However, generation must not start until the required validation gate has already accepted the input state.

---

## KernelIR Normalization Gate

KernelIR must be fully normalized before projection generation.

Forbidden unresolved data includes:

- command authoring key used as runtime dispatch identity
- stable value key used as runtime value reference
- transform hierarchy reference used as runtime parent source
- service type name used as the only service identity
- unresolved module alias
- runtime-only fallback ID

Projection generation must not complete missing normalization.
If KernelIR is not normalized, generation must fail.

---

## Projection Generation

Projection generation creates runtime-specific plan views from validated KernelIR.

Required projections include:

- ServiceGraphPlan
- ScopeGraphPlan
- LifecyclePlan
- CommandCatalogPlan
- ValueSchemaPlan
- ValueInitPlan
- DynamicEvaluationPlan
- ReactiveEvaluationPlan
- RuntimeQueryPlan
- DebugMap source data

Projection generation may derive:

- runtime layout indexes
- lookup tables
- sorted arrays
- compact runtime metadata
- artifact-local headers and manifests

Projection generation must not create structural identities not present in KernelIR.

Forbidden projection behavior includes:

- inventing new ServiceId values
- inventing new CommandTypeId values
- inventing new ValueKeyId values
- inventing new ScopeAuthoringId values
- inventing new RuntimeQueryId values
- adding implicit lifecycle steps
- adding hidden fallback dependencies

Projection generation must preserve:

- owner module information
- source provenance
- profile availability
- identity domain boundaries
- validated dependency references required for downstream handoff

---

## Post-Generation Validation

Post-generation validation checks generated projections against KernelIR and against each other.

It must check at least:

- every required ServiceIR is represented in ServiceGraphPlan
- every required CommandIR is represented in CommandCatalogPlan
- every required command payload schema reference is represented in generated command metadata
- every required ValueKeyIR is represented in ValueSchemaPlan
- every required value initialization entry is represented in ValueInitPlan
- every required dynamic evaluation entry is represented in DynamicEvaluationPlan
- every required reactive evaluation entry is represented in ReactiveEvaluationPlan
- every required ScopeIR reference is represented in ScopeGraphPlan
- every required RuntimeQueryIR is represented in RuntimeQueryPlan
- every runtime-facing ID has DebugMap coverage at the required profile level
- projection ID spaces match KernelIR ID spaces
- no projection contains IDs unknown to KernelIR
- manifest coverage is complete for the required artifact kinds

Post-generation validation is the gate that rejects cross-artifact inconsistency before publication.

---

## Profile-Specific Generation

Profile-specific generation is allowed only through explicit availability declarations already present in validated inputs.

The same validated KernelIR may produce different verified artifact sets for different profiles only when:

- the availability difference is declared explicitly
- the difference is reflected in profile-sensitive hashes
- the difference remains traceable in diagnostics and reports

Profile-specific generation must not:

- use runtime discovery to decide which content exists
- invent profile-only fallback IDs
- silently omit required DebugMap coverage for Development or Test profiles
- rely on editor-only UI state to decide availability

Excluded content must be omitted explicitly and remain diagnosable through the manifest or generation report.

---

## Plan Header and Artifact Header Semantics

KernelPlanHeader is the top-level compatibility contract for a verified artifact set.

Every generated artifact must carry a header appropriate to its artifact kind.

The top-level or per-artifact header must be able to express at least:

- PlanId
- ArtifactSetId
- ArtifactId
- ArtifactKind
- FormatVersion
- GeneratorVersion
- SourceHash
- RegistryHash
- ProfileHash
- GeneratedHash
- DebugMapHash where applicable

The header fields used by 00 are interpreted as follows:

- PlanId: stable identifier of the logical plan target
- ArtifactSetId: stable identifier of one generation run's output set
- ArtifactId: stable identifier of one artifact inside the set
- ArtifactKind: projection or artifact category
- FormatVersion: generation format version
- GeneratorVersion: version of the generator core that emitted the artifact
- SourceHash: hash of the normalized validated KernelIR source state
- RegistryHash: hash of registry-backed identity inputs relevant to the plan
- ProfileHash: hash of profile-affecting configuration relevant to the plan
- GeneratedHash: hash of the emitted semantic content for the plan or artifact
- DebugMapHash: hash of the generated debug map content when applicable

The header must be derivable from the generation run and must not depend on non-semantic data.
The header must not use build time, local machine path, or asset import order as trust input.

If the required header cannot be computed consistently, the generation output is invalid.

---

## Artifact Consistency Model

Generated artifacts must be treated as one consistency unit.

An artifact set is valid only if:

- all artifacts share the same PlanId
- all artifacts share the same ArtifactSetId
- all artifacts share compatible format versions
- all artifacts are derived from the same SourceHash
- all artifacts are derived from the same RegistryHash set
- all artifacts are derived from the same ProfileHash
- DebugMap corresponds to the same ID space as runtime plans
- generated C# corresponds to the same ID space as generated assets
- ArtifactSetManifest references exactly one compatible verified artifact set

A runtime must not mix artifacts from different generation runs.

The kernel pipeline produces multiple artifacts, but they must represent the same source state.

If consistency cannot be proven, generation must fail and the outputs must be treated as stale.

---

## Hash and Version Model

Hash and version policy must prove semantic compatibility.

Hash must represent semantic compatibility, not file timestamp compatibility.

Include in trust-relevant hashes:

- validated KernelIR content
- module IDs and versions
- identity assignments
- dependency edges relevant to the projection
- registry content relevant to runtime IDs
- profile-affecting settings
- generator version and supported format version
- projection-relevant generation settings

Exclude from trust-relevant hashes:

- generation timestamp
- absolute local machine paths
- editor selection state
- foldout state
- non-semantic formatting
- runtime instance identity

The buildTimestamp pattern seen in current compiled assets is diagnostic metadata only.
It is not a compatibility proof and must not participate in the trust boundary.

The plan header and artifact manifest must both be able to express incompatibility when source, registry, profile, or DebugMap inputs change.

---

## Deterministic Generation Rules

Given the same inputs, profile, and generator version, generation must produce semantically equivalent output.

Generation must not depend on:

- Unity object enumeration order
- reflection order
- dictionary iteration order
- file system enumeration order
- current time
- random values
- asset import timing
- local machine paths

Generated arrays and files must use deterministic ordering.

Recommended ordering priority:

1. artifact kind
2. owner module ID
3. stable runtime ID
4. stable name
5. source location

Determinism is required for reproducible hashes, stable diffs, and CI parity.

If nondeterministic output is detected, the generation run must fail rather than silently accept the output.

---

## Generated Code Policy

Generated code is an execution artifact.
It is not source of truth.

Generated code must:

- contain an artifact header appropriate to its kind
- contain generator version metadata
- contain SourceHash metadata
- contain a warning that manual edits are forbidden
- use deterministic ordering
- avoid non-semantic output differences
- avoid embedding machine-local paths

Generated code may include:

- ID tables
- strongly typed constants
- projection glue
- compile-time helpers

Generated code must not be edited manually.
Manual edits are invalid and must be overwritten or rejected by validation.

Namespace or partial-type conventions are lower-spec or generator concerns.
03 only requires that generated code remain non-authoritative and traceable to the same verified artifact set.

---

## Generated Asset Policy

Generated assets are execution artifacts.
They are not source of truth.

Generated assets must:

- contain an artifact header appropriate to their kind
- contain SourceHash metadata
- contain FormatVersion metadata
- be marked generated
- be stale-detectable
- be inspectable in the editor

Generated assets may include:

- plan assets
- debug map assets
- registry projection assets
- manifest assets

Generated runtime assets must not be manually modified as authoritative data.
Changes must originate from authoring inputs and regenerate the artifact.

Stable asset GUID or reference layout is owned by lower specs.
03 only requires traceability, staleness detection, and consistency with the verified artifact set.

---

## DebugMap Generation Requirements

Generation must produce DebugMap data for runtime-facing identities.

Required coverage includes at least:

- ModuleId
- ServiceId
- CommandTypeId
- ValueKeyId
- ScopeAuthoringId
- ScopePlanId
- LifecycleStepId
- RuntimeQueryId

Each DebugMap entry must include:

- numeric or symbolic ID
- stable debug name
- owner module
- source location
- profile availability
- artifact hash
- legacy origin when applicable

DebugMap generation must share the same SourceHash as the rest of the artifact set.

Missing DebugMap coverage is a generation failure in Development and Test profiles.
In Release profile, reduced debug metadata is allowed only if fatal failures can still be reported with stable error codes and stable numeric or symbolic identifiers.

DebugMap must not be reconstructed at boot as a substitute for generation-time provenance.

---

## Stale Artifact Detection

An artifact is stale if:

- its SourceHash does not match the current validated KernelIR
- its RegistryHash does not match the current registry input state
- its ProfileHash does not match the selected profile
- its GeneratorVersion is incompatible
- its FormatVersion is incompatible
- its DebugMapHash does not match required DebugMap content
- its artifact set is incomplete

Staleness detection must be possible from artifact headers, manifest data, and current input digests.
It must not rely on runtime fallback behavior.

Behavior:

- editor tooling may display stale artifacts for inspection
- runtime boot must not use stale artifacts
- CI must fail if required artifacts are stale

---

## Editor / CLI / CI Parity

Generation must be executable in:

- Unity Editor
- command line generation
- CI validation

The generation core must be shared by Editor, CLI, and CI hosts.

Host-specific wrappers may differ, but they must not alter generation semantics.

Given the same inputs, these environments must produce semantically equivalent artifact sets.

Generation must not depend on:

- editor window state
- current selection
- unsaved UI state

If Editor and CLI outputs differ, the generation core is not valid.
If CI cannot reproduce the Editor result, the generation run is not trustworthy.

---

## Incremental Generation Policy

Incremental generation is allowed only if artifact consistency can still be proven.

If consistency cannot be proven, full regeneration is required.

Full regeneration is required when:

- KernelIR format version changes
- ID assignment rules change
- registry content changes
- profile changes
- generator version changes incompatibly
- DebugMap format expectations change
- dependency graph changes in a way that affects projections

Incremental generation must still rebuild or revalidate the artifact manifest and verification report for the whole set.

If any required output cannot be reverified, incremental generation must abort and require full regeneration.

---

## Validation Handoff

03 owns generation-time structural checks, not the full dependency validation model.

Generation-time checks include:

- supported KernelIR and plan format version
- required input completeness
- artifact set completeness
- hash compatibility
- DebugMap coverage completeness
- deterministic output verification
- manifest completeness

04_DependencyValidationSpec.md owns:

- dependency cycle detection
- phase-aware dependency rules
- cross-graph dependency severity policy
- detailed dependency conflict classification

05_BootManifestAndProfileSpec.md owns the final boot-time policy that decides whether a verified artifact set is selected for runtime start.

03 must provide enough structure and metadata for 04 and 05 to operate without reconstructing missing provenance.

---

## Failure Policy

Generation failure must invalidate the target artifact set.

A failed generation must not leave old artifacts marked as current verified output.

On generation failure, the system must:

- produce a validation report
- mark the new artifact set invalid
- avoid updating the current verified marker
- avoid partially updating the runtime artifact set unless transaction-safe publication is proven
- preserve a previous valid artifact set only if it is explicitly marked previous rather than current

Required failure conditions include:

- stale input state
- unsupported format version
- missing required artifact input
- hash mismatch
- inconsistent artifact set
- nondeterministic output
- missing required DebugMap coverage
- generator version incompatibility
- invalid manifest completeness

Generation failure must not silently fall back to empty assets, runtime discovery, or runtime-only negative IDs.

Temporary staging outputs may be kept for inspection, but they are not trusted until the consistency check succeeds.

---

## Diagnostics Requirements

Generation diagnostics must include:

- phase
- severity
- error code
- affected artifact
- affected IR node when available
- owner module
- source location
- suggested fix when available

Recommended ordered severity values are:

| Severity | Numeric Value | Meaning |
|---|---|---|
| Info | 10 | Non-blocking informational output |
| Warning | 20 | Deviation that does not yet invalidate the artifact set |
| Error | 30 | Blocking issue that invalidates the current generation attempt |
| Fatal | 40 | Blocking issue that invalidates generation and prevents trusted publication |

03 does not own the final runtime diagnostic catalog.
It does own the minimum data required to explain generation failures and verification failures.

---

## Forbidden Patterns

The following are forbidden in target generation paths:

- treating generated code as source of truth
- treating generated assets as source of truth
- executing unverified generated plans
- executing partial artifact sets
- silently using stale artifacts
- completing KernelIR normalization during projection generation
- inventing missing IDs during generation
- resolving missing stable keys by fallback
- using last-write-wins to resolve generation conflicts
- using reflection order as generation order
- depending on editor UI state
- allowing manual edits to generated runtime artifacts
- using build timestamps as compatibility proof
- updating trusted artifacts before post-generation validation succeeds

These are structural violations, not stylistic preferences.

---

## Legacy Migration Notes

Legacy-origin data may be converted into KernelIR and generated artifacts only through explicit migration metadata.

Legacy conversion must not introduce runtime fallback.

Examples:

- legacy command key becomes authoring metadata and CommandTypeId mapping
- legacy stable var key becomes ValueKeyIR metadata and generated ValueKeyId
- legacy installer registration becomes module contribution provenance
- legacy build callback becomes diagnostics or debug provenance metadata

Legacy migration output must be validated like any other contribution or input artifact.

Current code provides partial precedents, but not the target contract:

- [GameStateCodeGenerator.cs](../../Game/Scripts/Flow/Editor/GameStateCodeGenerator.cs), [MaterialFxPropertyCodeGenerator.cs](../../GameLib/Script/Shader/Core/MaterialFx/Editor/MaterialFxPropertyCodeGenerator.cs), and [RoomMapTileCodeGenerator.cs](../../GameLib/Script/Project/Scene/RoomMap/Editor/RoomMapTileCodeGenerator.cs) are examples of domain-specific generation, not a unified plan-generation system.
- [FlowCompiler.cs](../../GameLib/Script/Project/Flow/Compiler/FlowCompiler.cs) and [FlowProgramAssetSO.cs](../../GameLib/Script/Project/Flow/Core/FlowProgramAssetSO.cs) show a source-hash and build-timestamp pattern for one domain, but they do not yet establish a whole-system artifact consistency model.
- [VarKeyRegistryLocator.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs) and [VarIdResolver.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs) demonstrate why generation and trust boundaries must fail closed instead of inventing runtime IDs or empty assets.

The target architecture may reuse ideas from these systems, but it must not reuse their fallback behavior as a new contract.

---

## Examples

### Example 1: Command Artifact Set

CameraModule contributes CameraShake command.

Generation produces:

- CommandIR entry
- CommandCatalogPlan entry
- payload schema metadata
- executor reference metadata
- DebugMap entry
- diagnostics mapping in the generation report

If the DebugMap entry is missing, the artifact set is invalid in Development and Test profiles.

### Example 2: Stale Value Registry

ValueKey registry changes after generation.

Expected result:

- RegistryHash mismatch
- generated ValueSchemaPlan marked stale
- runtime boot blocked
- diagnostics point to the changed registry input

### Example 3: Partial Generation Failure

ServiceGraphPlan generated successfully but CommandCatalogPlan failed.

Expected result:

- entire artifact set invalid
- current verified marker not updated
- previous verified set may remain archived but must not be mislabeled as current

---

## Acceptance Criteria

03 is complete when it defines:

- VerifiedKernelPlan definition
- generation pipeline stages
- input artifact requirements
- output artifact set requirements
- pre-generation validation gates
- KernelIR normalization gate
- post-generation validation gates
- artifact consistency model
- plan and artifact header semantics
- hash and version model
- deterministic generation rules
- generated code policy
- generated asset policy
- DebugMap generation requirements
- stale artifact detection
- profile-specific generation constraints
- Editor / CLI / CI parity requirements
- incremental generation policy
- failure policy
- diagnostics requirements
- forbidden patterns

03 is not complete if runtime storage detail, dependency validation algorithm detail, or BootManifest schema detail has escaped into the specification.

## Test Cases

| Test Case | Purpose | Verification |
|---|---|---|
| TC-03-01 | Confirm GeneratedPlan is not automatically a VerifiedKernelPlan. | The VerifiedPlan Definition and Pre-Generation Validation sections must require validated input and completed gates before runtime use. |
| TC-03-02 | Confirm artifact sets are all-or-nothing consistency units. | The Output Artifact Set and Artifact Consistency Model sections must require complete sets with matching PlanId, ArtifactSetId, and trust hashes. |
| TC-03-03 | Confirm normalization must finish before projection generation and generation must remain deterministic. | The KernelIR Normalization Gate and Deterministic Generation Rules sections must forbid fallback normalization, time dependence, path dependence, and order dependence. |
| TC-03-04 | Confirm DebugMap and diagnostics are required parts of verification. | The DebugMap Generation Requirements and Diagnostics Requirements sections must require provenance, owner module, source location, and profile-aware coverage. |
| TC-03-05 | Confirm stale, partial, or failed artifacts cannot remain current. | The Stale Artifact Detection, Incremental Generation Policy, and Failure Policy sections must block stale or partially published outputs from becoming current verified artifacts. |
| TC-03-06 | Confirm Editor, CLI, and CI share one trusted generation core. | The Editor / CLI / CI Parity section must prohibit semantic divergence between hosts and UI-state dependence. |

---

## Final Position

Generated artifacts are not trusted individually.
Only a complete, deterministic, validated, hash-compatible artifact set may become a VerifiedKernelPlan.

Verified plan generation is a trust boundary, not a convenience layer.

The generation system must prove that a verified artifact set is internally consistent, provenance-backed, deterministic, and version-compatible before the runtime ever sees it.

The runtime may only execute verified inputs.
The editor, CLI, and CI must prove that those inputs are trustworthy.
Generated code and generated assets are projections, not authority.

03 exists to make stale, partial, or silently repaired output impossible to mistake for a verified plan.

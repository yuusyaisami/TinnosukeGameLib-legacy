# Boot Manifest and Profile Specification

## Document Status

- Document ID: 05_BootManifestAndProfileSpec
- Status: Draft
- Role: defines boot entry, boot input validation, KernelProfile policy, and boot-time artifact acceptance rules
- Depends on:
  - [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md)
  - [01_KernelIRSpec.md](01_KernelIRSpec.md)
  - [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md)
  - [04_DependencyValidationSpec.md](04_DependencyValidationSpec.md)
- Provides foundation for:
  - 06_ServiceGraphRuntimeSpec.md
  - 07_ScopeGraphRuntimeSpec.md
  - 08_LifecyclePlanSpec.md
  - 11_DebugMapAndDiagnosticsSpec.md
  - 12_UnityAuthoringBridgeSpec.md
  - 13_LegacyCompatBoundarySpec.md
  - 14_PerformanceBudgetAndRuntimeRulesSpec.md
  - 15_TestAndValidationSpec.md

### Ownership

This specification owns boot input acceptance, boot entry selection, KernelProfile policy, BootPolicy behavior, and boot-time failure boundaries.

It does not own runtime subsystem implementation, runtime storage layout, dependency validation algorithms, generated artifact emission, or final Unity authoring schema.

---

## Purpose

This specification defines how the target kernel selects, validates, and boots from one verified artifact set.

It also defines KernelProfile policy, BootPolicy behavior, and boot-time failure handling.

BootManifest is not a global settings dump.
BootManifest is a small, validated entry point that selects one compatible verified artifact set and one KernelProfile policy.

Boot must not discover the kernel structure.
Boot must accept a verified kernel structure.

Boot does not repair missing kernel structure.
Boot accepts one verified kernel structure or fails.

---

## Scope

This specification defines:

- KernelBootManifest purpose and responsibility boundary
- boot input model
- verified artifact set reference rules
- KernelProfile kinds and policy matrix
- BootPolicy behavior
- boot phase order
- boot validation gates
- boot-time runtime creation contract
- persistent root policy
- scene and loading boundary policy
- discovery and fallback prohibition at boot
- boot failure behavior
- boot diagnostics requirements
- stale artifact handling
- BootManifest size control
- legacy compatibility boundary at boot
- boot-related performance direction
- boot-related test cases

---

## Non-Goals

This specification does not define:

- final ServiceGraph runtime storage
- final ScopeGraph runtime storage
- final CommandCatalog runtime lookup
- final ValueStore runtime storage
- final DebugMap asset layout
- final Unity authoring component schema
- scene transition algorithm details
- loading screen visual implementation

This specification must not turn BootManifest into a global settings registry.

---

## Relationship to Other Specs

| Spec | Relationship |
|---|---|
| [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md) | Defines BootManifest as a small boot input reference, not a configuration dump. |
| [01_KernelIRSpec.md](01_KernelIRSpec.md) | Provides typed identity domains and hash-relevant source model consumed by boot acceptance. |
| [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md) | Produces the verified artifact set, plan header, and DebugMap provenance consumed by boot. |
| [04_DependencyValidationSpec.md](04_DependencyValidationSpec.md) | Defines the dependency validation gates that must succeed before boot may accept an artifact set. |
| 06_ServiceGraphRuntimeSpec.md | Boot creates essential service runtime state only from validated service projections. |
| 07_ScopeGraphRuntimeSpec.md | Boot creates required root scopes only from validated scope projections. |
| 08_LifecyclePlanSpec.md | Boot may execute boot lifecycle phases only from validated lifecycle plans. |
| 11_DebugMapAndDiagnosticsSpec.md | Boot diagnostics and failure reporting rely on DebugMap coverage and stable diagnostics contracts. |
| 12_UnityAuthoringBridgeSpec.md | May define authoring assets or references that feed manifest production, but not runtime boot discovery. |
| 13_LegacyCompatBoundarySpec.md | Defines whether a legacy boot bridge exists and where it may remain visible. |
| 14_PerformanceBudgetAndRuntimeRulesSpec.md | Defines the measurable boot markers and budgets referenced here. |
| 15_TestAndValidationSpec.md | Turns this boot acceptance contract into executable test and CI coverage. |

05 is the acceptance boundary between verified artifacts and live runtime boot.

---

## Assembly Definition and Compile Boundary Expectations

The intended assembly home for verified boot policy is `GameLib.Kernel.Boot`.
Unity-facing boot entry glue belongs in `GameLib.Kernel.Boot.Unity`.
Detailed dependency matrices remain owned by [17_AssemblyDefinitionAndCompileBoundarySpec.md](17_AssemblyDefinitionAndCompileBoundarySpec.md).

Required compile-boundary rules for 05:

- `GameLib.Kernel.Boot` must remain separate from feature assemblies, legacy assemblies, and authoring extraction assemblies
- boot core should remain Unity-free and use `noEngineReferences: true`
- `GameLib.Kernel.Boot.Unity` is the legal place for Unity-specific boot assets, startup hooks, and engine glue
- boot acceptance logic must not depend on scene discovery helpers, feature installers, or legacy fallback services

If verified boot requires feature back-references or Unity scene traversal in boot core, the 05 boundary has been violated.

---

## Current Boot Observations

Current boot observations must remain traceable to source code, migration notes, or profiling evidence.

### Observation Traceability

| Observation | Evidence Type | Boot Pressure |
|---|---|---|
| Project root creation is coupled to `BeforeSceneLoad` singleton discovery. | Source | 05, 07 |
| Global root creation is coupled to project-root auto-creation, scene search, and resource fallback. | Source | 05, 07, 13 |
| Loading presentation boot scans `SceneLifetimeScope` instances and performs duplicate cleanup. | Source | 05, 07 |
| Loading presentation parent selection searches Global, Platform, and Project roots at runtime. | Source | 05, 07 |
| Boot repair can still happen through `Resources.Load` or `new GameObject` fallback. | Source | 05, 13 |
| Persistent roots use `DontDestroyOnLoad` singleton behavior outside a verified boot entry contract. | Source | 05, 07 |

### Representative Anchors

- [ProjectLifetimeScope.cs](../../GameLib/Script/Project/LTS/ProjectLifetimeScope.cs) - `BeforeSceneLoad` boot entry, scene-wide root search, resource fallback, default `GameObject` creation
- [GlobalLifetimeScope.cs](../../GameLib/Script/Project/Global/LTS/GlobalLifetimeScope.cs) - project-first boot coupling, global root search, resource fallback, default `GameObject` creation
- [LoadingScreenService.cs](../../GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs) - loading scope discovery, duplicate cleanup, persistent parent search

### Current Gaps

The current codebase still exposes boot behaviors that 05 must remove from the target architecture:

- boot truth is still inferred from scene state during runtime startup
- missing boot roots may still be repaired through fallback prefab loads or default object creation
- loading presentation still depends on scene search and persistent-parent search
- duplicate roots can still be handled by cleanup instead of rejection
- boot input acceptance is not yet centralized around one verified artifact set and one selected profile

---

## Boot Authority

05 is the fail-closed authority for boot input acceptance.

BootManifest selects verified inputs; it does not construct truth.

03 owns artifact emission, hash assembly, and artifact-set consistency.
04 owns dependency validation and dependency failure classification.
05 owns the rule that boot may proceed only when one artifact set, one selected profile, and one boot policy satisfy all acceptance gates.

Boot must not weaken invalid inputs into valid ones through host behavior, runtime search, legacy repair, or fallback creation.

---

## BootManifest Definition

KernelBootManifest is the explicit entry point for target kernel boot.

It references:

- selected KernelProfile
- one verified artifact set
- boot policy
- diagnostics policy
- optional editor-only source metadata

```csharp
public sealed class KernelBootManifest : ScriptableObject
{
    public string ManifestId;
    public KernelProfileId ProfileId;
    public VerifiedArtifactSetRef ArtifactSet;
    public BootPolicyId BootPolicyId;
    public BootDiagnosticsPolicy DiagnosticsPolicy;
}
```

This sketch is explanatory and does not finalize the serialized API.

BootManifest is an entry selector.
It is not a replacement for KernelIR, VerifiedKernelPlan, ServiceGraphPlan, ScopeGraphPlan, or DebugMap.

---

## BootManifest Responsibility Boundary

BootManifest may contain:

- manifest identity
- selected profile reference
- verified artifact set reference
- boot policy reference
- diagnostics policy reference
- editor-only source metadata

BootManifest must not contain:

- full service list
- full command list
- full value key list
- full lifecycle step list
- full scope graph
- direct command executor definitions
- direct runtime service instances
- runtime fallback paths
- scene search rules

If BootManifest begins duplicating generated graph data, it has crossed the responsibility boundary and is invalid by design.

---

## Boot Input Model

Boot input consists of:

- KernelBootManifest
- one verified artifact set
- KernelDebugMap
- registry or identity projection assets when applicable
- selected KernelProfile
- selected BootPolicy

All boot inputs must be versioned and hash-compatible.

Unversioned boot input must not be accepted by target runtime.

Boot does not accept loose collections of files discovered at runtime.
It accepts one verified, mutually compatible input set.

---

## Verified Artifact Set Reference

BootManifest references exactly one verified artifact set.

A valid reference must include enough information to prove set identity and compatibility, including:

- ArtifactSetId
- PlanId
- KernelIRHash
- RegistryHash when a registry or identity projection asset exists
- ProfileHash
- DebugMapHash when DebugMap is required
- FormatVersion

BootManifest must not reference individual generated artifacts independently unless they belong to the same verified artifact set and the same compatibility proof.

Boot must not assemble truth by mixing artifacts from different generation runs.

---

## KernelProfile Definition

KernelProfile defines boot-time policy and validation strictness.

Required profile kinds:

- Development
- Release
- Test

```csharp
public enum KernelProfileKind
{
    Development = 10,
    Release = 20,
    Test = 30,
}
```

KernelProfile affects diagnostics detail, legacy allowance, and strictness reporting.
It must not invent or repair missing boot structure.

---

## Profile Policy Matrix

| Policy | Development | Release | Test |
|---|---|---|---|
| Stale artifact | Error and boot block | Fatal and boot block | Fatal and boot block |
| Missing DebugMap | Error | Error if fatal diagnostics cannot be produced | Fatal |
| Legacy bridge | Warning if explicitly allowed | Forbidden unless 13 explicitly allows | Error or Fatal depending on test policy |
| Diagnostics detail | Full | Minimal required | Full captured |
| Runtime assertions | Enabled | Minimal | Enabled |
| Validation strictness | Strict | Strict | Maximum practical |
| Generated mismatch | Boot block | Boot block | Boot block |
| Fallback | Forbidden except bounded dev-only bridge allowed by 13 | Forbidden | Forbidden |

Profile may change severity and diagnostics detail.
Profile must not silently convert invalid boot input into valid boot input.

---

## BootPolicy Definition

BootPolicy defines how the runtime reacts to boot validation results.

BootPolicy may define:

- failure boundary behavior
- diagnostics emission mode
- previous verified artifact handling
- editor-only inspection mode
- test deterministic mode

BootPolicy must not define fallback discovery behavior.

BootPolicy may tune observability and enforcement presentation.
It may not authorize boot from unverified or incompatible inputs.

---

## Boot Phase Model

Boot phases are deterministic and ordered:

1. Load BootManifest reference
2. Load referenced artifact set
3. Validate artifact set header
4. Validate profile compatibility
5. Validate DebugMap requirement
6. Validate dependency validation status
7. Create KernelRuntime shell
8. Create essential ServiceGraph runtime state
9. Create required root scopes
10. Run boot lifecycle steps
11. Mark kernel boot completed

Boot phases must be deterministic.
Boot phases must not perform broad runtime discovery.

Boot must not re-run normalization, generation, or dependency repair during these phases.

---

## Boot Validation Gates

Boot must validate at minimum:

- `ManifestId` exists
- selected profile exists
- artifact set reference exists
- artifact set is complete
- artifact headers are compatible
- `KernelIRHash` matches
- `RegistryHash` matches when applicable
- `ProfileHash` matches
- `DebugMapHash` matches where required
- dependency validation status is acceptable for the selected profile
- required root scope projections exist
- required root services exist

If any required gate fails, boot must not continue into runtime subsystem creation.

Boot acceptance gates are not optional host conveniences.
They are part of the target runtime contract.

---

## Boot Runtime Creation Contract

KernelRuntime may be created only from verified boot inputs.

Runtime creation must not:

- search scenes for kernel roots
- infer roots from existing `GameObject` state
- load fallback prefabs for required inputs
- create missing root services from defaults
- create missing root scopes from defaults

Runtime creation may:

- allocate KernelRuntime shell state
- instantiate explicitly referenced boot prefabs if they are part of the verified artifact set or verified boot inputs
- create required root scopes defined by validated scope projections
- create essential services defined by validated service projections

Boot creates runtime state from verified inputs.
It does not discover or repair kernel structure.

---

## Persistent Root Policy

Persistent roots must be defined by verified boot inputs.

Examples of persistent roots include:

- application root
- project root
- global root
- persistent presentation root
- loading presentation root

Persistent root creation must not rely on scene-wide search, duplicate cleanup, or fallback instantiation.

Duplicate persistent roots are validation or boot errors.
They must not be resolved by keeping the first and destroying the rest at runtime.

If the host already contains a persistent root, that host object must be explicitly referenced by the verified boot input set.
It must not be discovered as a surprise during boot.

---

## Scene Boundary and Loading Policy

Boot must define the boundary between persistent kernel runtime and scene-specific runtime.

Loading presentation must not discover `SceneLifetimeScope` instances at runtime.

Loading-related roots must be one of:

- persistent presentation root defined by verified boot input
- scene-flow service output defined by a verified scene transition plan
- explicit scene scope defined by validated scope projections

Loading must not resolve its parent by searching Global, Platform, or Project objects in the scene.

Scene boundary policy must preserve the distinction between persistent boot state and scene-local runtime state.
It must not reintroduce runtime hierarchy discovery as a composition mechanism.

---

## Fallback and Discovery Prohibition

Target boot must not use:

- `FindObjectsByType` to locate kernel roots
- `GetComponentsInChildren` to discover boot modules
- transform-parent traversal to infer kernel parentage
- `Resources.Load` fallback for required boot inputs
- `new GameObject` fallback for missing root scopes
- keep-first-destroy-rest duplicate cleanup as resolution
- runtime stable-key fallback
- legacy resolver fallback

Any exception must be defined by a lower spec as bounded, profile-scoped, diagnostic-visible, and migration-limited.

An exception is not valid merely because it happens only once at startup.

---

## Boot Failure Policy

Boot failure must stop before runtime subsystem execution when required boot inputs are invalid.

Required failure categories include:

- `ManifestMissing`
- `ArtifactSetMissing`
- `ArtifactSetIncomplete`
- `HashMismatch`
- `VersionMismatch`
- `ProfileMismatch`
- `DebugMapMissing`
- `DependencyValidationFailed`
- `RequiredRootMissing`
- `LegacyForbidden`

Boot failure boundary is whole-kernel boot.
If boot fails, no partially initialized KernelRuntime may be exposed as valid.

Boot must fail explicitly with diagnostics.
It must not continue behind a degraded success path.

---

## Diagnostics and DebugMap Requirements

Boot diagnostics must include:

- stable error code
- selected profile
- manifest ID
- artifact set ID
- failing artifact or gate
- expected hash when applicable
- actual hash when applicable
- source location if available
- suggested fix

If DebugMap is missing, boot diagnostics must still emit stable numeric IDs and stable error codes.

In Development and Test profiles, missing DebugMap is an error or fatal condition.
In Release profile, reduced debug metadata is allowed only when fatal diagnostics remain stable and interpretable.

Boot diagnostics must explain why boot was rejected without requiring runtime reproduction.

---

## Environment Differences

Boot behavior may differ by host environment, but validity rules must not be weakened silently.

Editor may provide:

- artifact inspection
- regeneration prompts
- stale artifact display
- detailed source navigation

Release player may provide:

- compact diagnostics
- minimal required DebugMap
- no regeneration prompt

Test host must provide:

- deterministic boot
- captured diagnostics
- strict artifact validation

Host differences may change tooling behavior.
They must not change whether invalid boot input is acceptable.

---

## Stale or Missing Artifact Handling

Stale artifacts must not be used for runtime boot.

An artifact is stale when:

- `KernelIRHash` mismatches
- `RegistryHash` mismatches
- `ProfileHash` mismatches
- `DebugMapHash` mismatches
- generator version is incompatible
- artifact set is incomplete

Editor may show stale artifacts for inspection.
Editor must not mark stale artifacts as current verified inputs.

Missing required artifacts are boot failures, not discovery opportunities.

---

## BootManifest Size Control

BootManifest must remain small.

BootManifest should reference:

- profile
- artifact set
- boot policy
- diagnostics policy

BootManifest must not duplicate data owned by:

- KernelIR
- VerifiedKernelPlan
- ServiceGraphPlan or equivalent service projection
- ScopeGraphPlan or equivalent scope projection
- CommandCatalogPlan
- ValueSchemaPlan
- DebugMap

Validation should detect BootManifest fields that duplicate generated artifact content.

If BootManifest becomes a second registry, the target architecture has already drifted.

---

## Legacy Compatibility Boundary at Boot

Legacy boot compatibility is allowed only through an explicit LegacyCompat boot bridge defined by 13.

Target boot core must not depend on:

- legacy LifetimeScope singleton creation
- legacy RuntimeResolver boot behavior
- legacy `CommandRunnerMB` registration discovery
- legacy Blackboard initialization
- legacy runtime value-ID fallback

Development may report an allowed legacy boot bridge as a warning.
Release must reject unapproved legacy boot dependency.

Legacy boot behavior must remain measurable, visible, and removable.

---

## Performance Budget Direction

Boot must expose profiler markers for at least:

- `KernelBoot.LoadManifest`
- `KernelBoot.LoadArtifactSet`
- `KernelBoot.ValidateHeaders`
- `KernelBoot.ValidateHashes`
- `KernelBoot.ValidateProfile`
- `KernelBoot.CreateRuntime`
- `KernelBoot.CreateEssentialServices`
- `KernelBoot.CreateRootScopes`
- `KernelBoot.RunBootLifecycle`

These are the minimum required boot markers.
Spec 14 defines the full marker taxonomy, budget ranges, profile-specific caps, and regression rules that consume this boot path.

Boot performance must not be optimized by skipping validation or DebugMap consistency checks.

Command executor count may increase structural metadata size.
It must not force eager construction of all command executors during boot.

---

## Forbidden Patterns

The following are forbidden in target boot:

- booting from unverified plan
- booting from partial artifact set
- scene-wide search for kernel root
- `Resources.Load` fallback for required boot input
- default `GameObject` creation for missing root
- duplicate root cleanup by runtime destruction
- transform parent search for persistent root
- legacy resolver fallback during boot
- BootManifest storing full runtime graphs
- profile silently weakening validation
- boot continuing after required validation failure

Forbidden patterns are architectural failures, not style issues.

---

## Test Case Model

Each boot/profile test case must define:

- Test ID
- Title
- BootManifest fixture
- ArtifactSet fixture
- Profile
- Expected boot result
- Expected diagnostics
- Expected failure boundary
- Notes

Recommended fixture format:

```md
### TC_BOOT_001_HashMismatchBlocksBoot

Input:
- BootManifest references ArtifactSet A
- ArtifactSet header KernelIRHash = X
- Registry projection reports KernelIRHash = Y

Profile:
- Development

Expected:
- BootResult: Failed
- Diagnostic: BOOT_HASH_MISMATCH
- Boundary: Whole kernel boot
```

The fixture format exists so that 15 may turn these cases into executable boot and CI tests without redefining intent.

---

## Required Test Cases

### A. Manifest Tests

#### TC_BOOT_MANIFEST_001_MissingManifest

Input:
- BootManifest reference is missing

Expected:
- Failed
- `BOOT_MANIFEST_MISSING`
- Boundary: Whole kernel boot

#### TC_BOOT_MANIFEST_002_ManifestReferencesMissingArtifactSet

Input:
- BootManifest exists
- ArtifactSet reference is missing

Expected:
- Failed
- `BOOT_ARTIFACT_SET_MISSING`

#### TC_BOOT_MANIFEST_003_ManifestStoresRuntimeGraphDirectly

Input:
- BootManifest contains full ServiceGraph or CommandCatalog content directly

Expected:
- Failed
- `BOOT_MANIFEST_DUPLICATES_GENERATED_CONTENT`

### B. Artifact Set Tests

#### TC_BOOT_ARTIFACT_001_IncompleteArtifactSet

Input:
- Service projection exists
- Command catalog projection is missing
- ArtifactSet is marked current

Expected:
- Failed
- `BOOT_ARTIFACT_SET_INCOMPLETE`

#### TC_BOOT_ARTIFACT_002_KernelIRHashMismatch

Input:
- BootManifest references an artifact set
- ArtifactSet `KernelIRHash` does not match plan header or required registry projection

Expected:
- Failed
- `BOOT_KERNEL_IR_HASH_MISMATCH`

#### TC_BOOT_ARTIFACT_003_DebugMapHashMismatch

Input:
- Plan `DebugMapHash` = A
- DebugMap asset hash = B

Expected:
- Failed
- `BOOT_DEBUGMAP_HASH_MISMATCH`

#### TC_BOOT_ARTIFACT_004_StaleArtifactBlocked

Input:
- ArtifactSet was generated by an incompatible generator version

Expected:
- Failed
- `BOOT_ARTIFACT_STALE`

#### TC_BOOT_ARTIFACT_005_MixedArtifactSetRejected

Input:
- Service projection originates from one artifact set
- Scope projection or DebugMap originates from another artifact set

Expected:
- Failed
- `BOOT_ARTIFACT_SET_MIXED_SOURCE_FORBIDDEN`

### C. Profile Tests

#### TC_BOOT_PROFILE_001_DevelopmentAllowsDetailedDiagnostics

Profile:
- Development

Input:
- Missing optional DebugMap detail while stable numeric diagnostics remain available

Expected:
- Failed or Warning only when allowed by policy
- Diagnostics include source location when available

#### TC_BOOT_PROFILE_002_ReleaseRejectsLegacyFallback

Profile:
- Release

Input:
- Boot requires legacy RuntimeResolver fallback

Expected:
- Failed
- `BOOT_LEGACY_FALLBACK_FORBIDDEN`

#### TC_BOOT_PROFILE_003_TestRequiresDeterministicBoot

Profile:
- Test

Input:
- BootPolicy uses nondeterministic behavior

Expected:
- Failed
- `BOOT_TEST_NON_DETERMINISTIC_POLICY`

#### TC_BOOT_PROFILE_004_ProfileHashMismatch

Input:
- ArtifactSet was generated for Development
- BootManifest selects Release

Expected:
- Failed
- `BOOT_PROFILE_HASH_MISMATCH`

### D. Validation Gate Tests

#### TC_BOOT_GATE_001_DependencyValidationFailed

Input:
- ArtifactSet dependency validation status is `Failed`

Expected:
- Failed
- `BOOT_DEPENDENCY_VALIDATION_FAILED`

#### TC_BOOT_GATE_002_MissingRequiredRootService

Input:
- Boot requires `KernelDiagnosticsService`
- Validated service projection does not contain it

Expected:
- Failed
- `BOOT_REQUIRED_ROOT_SERVICE_MISSING`

#### TC_BOOT_GATE_003_MissingRequiredRootScope

Input:
- Boot requires `ProjectRootScope`
- Validated scope projection does not contain it

Expected:
- Failed
- `BOOT_REQUIRED_ROOT_SCOPE_MISSING`

### E. Discovery and Fallback Prohibition Tests

#### TC_BOOT_DISCOVERY_001_FindObjectsByTypeForbidden

Input:
- Boot implementation attempts scene-wide search for kernel root

Expected:
- Failed static analyzer or boot validation
- `BOOT_RUNTIME_DISCOVERY_FORBIDDEN`

#### TC_BOOT_DISCOVERY_002_ResourcesFallbackForbidden

Input:
- Required BootManifest or root input is missing
- Boot attempts `Resources.Load` fallback

Expected:
- Failed
- `BOOT_RESOURCES_FALLBACK_FORBIDDEN`

#### TC_BOOT_DISCOVERY_003_DefaultGameObjectRootForbidden

Input:
- Required root is missing
- Boot attempts `new GameObject` fallback

Expected:
- Failed
- `BOOT_DEFAULT_ROOT_CREATION_FORBIDDEN`

#### TC_BOOT_DISCOVERY_004_DuplicateRootCleanupForbidden

Input:
- Duplicate persistent roots are present
- Boot attempts keep-first-destroy-rest behavior

Expected:
- Failed
- `BOOT_DUPLICATE_ROOT_CLEANUP_FORBIDDEN`

### F. Loading and Scene Boundary Tests

#### TC_BOOT_LOADING_001_LoadingSceneSearchForbidden

Input:
- Loading presentation searches `SceneLifetimeScope` instances at runtime

Expected:
- Failed
- `BOOT_LOADING_SCENE_SEARCH_FORBIDDEN`

#### TC_BOOT_LOADING_002_LoadingParentTransformSearchForbidden

Input:
- Loading root parent is resolved by searching Global or Project objects

Expected:
- Failed
- `BOOT_LOADING_PARENT_SEARCH_FORBIDDEN`

#### TC_BOOT_LOADING_003_LoadingRootDefinedByPlan

Input:
- Verified boot input defines `PersistentLoadingPresentationRoot`
- ArtifactSet is valid

Expected:
- Passed

### G. Performance and Marker Tests

#### TC_BOOT_PERF_001_BootMarkersExist

Input:
- Valid boot

Expected:
- Required boot profiler markers are emitted

#### TC_BOOT_PERF_002_BootDoesNotInstantiateAllCommandExecutors

Input:
- Command catalog contains many commands
- Boot needs only catalog metadata

Expected:
- Passed
- No eager construction of all command executors occurs

---

## Acceptance Criteria

05 is complete when it defines:

- BootManifest purpose and responsibility boundary
- boot input model
- verified artifact set reference rules
- KernelProfile kinds and policy matrix
- BootPolicy definition
- boot phase model
- boot validation gates
- runtime creation contract
- persistent root policy
- scene and loading boundary policy
- fallback and discovery prohibition
- boot failure policy
- diagnostics and DebugMap requirements
- environment difference rules
- stale artifact handling
- BootManifest size control
- legacy boot boundary
- performance marker direction
- forbidden patterns
- boot/profile test case model
- required boot/profile test cases

The specification is not complete if boot still depends on runtime discovery, fallback root creation, or partial artifact acceptance.

---

## Test Cases

| Test Case | Purpose | Verification |
|---|---|---|
| TC-05-01 | Confirm BootManifest remains a small verified entry selector. | The BootManifest definition and size-control sections must forbid runtime graph duplication. |
| TC-05-02 | Confirm boot accepts exactly one compatible artifact set or fails. | The boot input model, artifact-set reference, and validation gates sections must reject partial or mixed artifacts. |
| TC-05-03 | Confirm profile changes severity, not truth. | The profile matrix and environment differences sections must not allow invalid input to become valid. |
| TC-05-04 | Confirm boot runtime creation does not use scene discovery or fallback creation. | The runtime creation contract and fallback prohibition sections must forbid `FindObjectsByType`, `Resources.Load`, and default root creation for required inputs. |
| TC-05-05 | Confirm loading presentation stays inside explicit boot or scene-flow contracts. | The scene boundary and loading policy section must reject runtime search for loading scope or parent selection. |
| TC-05-06 | Confirm boot failure blocks partial kernel exposure. | The failure policy and validation gates sections must stop before runtime subsystem execution when acceptance fails. |

---

## Final Position

BootManifest selects verified inputs; it does not construct truth.

Boot does not repair missing kernel structure.
Boot accepts one verified kernel structure or fails.

Target boot is not a discovery path.
It is the explicit acceptance path from validated artifact set to live runtime.

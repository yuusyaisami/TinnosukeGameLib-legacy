# Implementation Milestone Order Specification

## Document Status

- Document ID: 16_ImplementationMilestoneOrderSpec
- Status: Draft
- Role: defines implementation phase order, milestone gates, and prohibited sequencing for GameLib Kernel v2 realization
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
  - [15_TestAndValidationSpec.md](15_TestAndValidationSpec.md)
- Provides foundation for:
  - implementation planning, architecture review, migration tracking, and gate sequencing that realize 00 through 15 without violating their trust boundaries

### Revision Note

This revision creates 16 as the implementation-order specification for Kernel v2.

It makes one rule explicit:
the specification numbering is not the implementation schedule.

Diagnostics, test gates, static forbidden-pattern detection, normalized IR, validation, verified generation, and boot acceptance must exist before runtime-rich subsystems become the target path.

It also records the immediate documentation hygiene work that must happen before implementation starts at scale, including cross-spec concept ownership, forbidden-pattern registry setup, and existing anchor inventory.

---

## Ownership

This specification owns:

- implementation milestone ordering from M0 through M15
- milestone purpose, required outputs, entry assumptions, and exit gates
- cross-spec dependency mapping from 00 through 15 into implementation phases
- forbidden sequencing and anti-patterns for migration execution
- milestone-level traceability from planned work items to lower-spec verification points
- the rule that runtime subsystem work is blocked until the proof and trust-boundary milestones are in place

This specification does not own:

- subsystem semantics already owned by 00 through 15
- final runtime API signatures
- exact class, namespace, or file names for implementation artifacts
- sprint planning, people assignment, staffing, or calendar estimates
- CI vendor configuration files
- gameplay feature prioritization outside the representative migration wave defined here

16 defines execution order.
It does not redefine the meaning of ServiceGraph, ScopeGraph, LifecyclePlan, CommandCatalog, ValueStore, DebugMap, or BootManifest.

---

## Purpose

This specification defines how Kernel v2 should be implemented.

Core statements:

```text
Specification numbering is not implementation order.

No runtime core milestone may become a target path before diagnostics, tests, static gates, IR normalization, validation, verified generation, and boot acceptance gates exist.

A milestone is complete only when its outputs are traceable to lower-spec rules and backed by executable or documentable gates.
```

00 defines root-level constraints such as no runtime discovery, no lifecycle inference from registration, no runtime stable-key fallback, no trust in unvalidated artifacts, and no direct subsystem logging to Unity outside approved sinks.

01 defines `KernelIR` as normalized authority rather than runtime format.

11 and 15 define that diagnostics and executable protection are architecture requirements, not later polish.

Therefore the implementation order must begin by creating proof boundaries and trust boundaries before building runtime richness.

If `ServiceGraph`, `ScopeGraph`, `CommandCatalog`, `ValueStore`, or feature migration start before those boundaries exist, the project will tend to recreate a faster form of the legacy architecture rather than the target kernel.

---

## Scope

This specification defines:

- implementation phase order from M0 through M15
- milestone goal, required outputs, and exit-gate model
- cross-spec dependency mapping between the architecture specs and the implementation sequence
- the required pre-runtime proof chain for diagnostics, tests, IR, validation, generation, and boot
- representative migration-wave ordering for existing features
- forbidden starting points and forbidden shortcuts
- final integration and regression-hardening requirements

This specification intentionally does not define:

- runtime API shape
- exact serialization formats
- validation or generation algorithms already owned elsewhere
- detailed team task breakdowns or schedule estimates
- project management workflow outside architecture protection

16 must not become a substitute for lower subsystem specs.
It exists to determine when each lower spec is allowed to turn into code.

---

## Non-Goals

This specification does not define:

- a promise that every milestone must be completed by one pull request
- people or team ownership of each milestone
- a requirement that all features stop shipping until M15 is complete
- gameplay feature design unrelated to Kernel v2 architecture
- temporary branch strategy or release scheduling
- permission to bypass validation or diagnostics because the current task is internal-only

16 is an architecture execution order document.
It is not a generic project management handbook.

---

## Relationship to Other Specs

| Spec | Relationship to 16 |
|---|---|
| [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md) | Defines the non-negotiable root constraints that all milestones must preserve. |
| [01_KernelIRSpec.md](01_KernelIRSpec.md) | Defines the normalized authority model implemented in M2 and consumed by M3 through M11. |
| [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md) | Defines the contribution boundary implemented in M2 and consumed by M3 through M11. |
| [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md) | Defines the generation trust boundary implemented in M4 and consumed by M5 through M15. |
| [04_DependencyValidationSpec.md](04_DependencyValidationSpec.md) | Defines the validation firewall implemented in M3 and reused by M4 through M15. |
| [05_BootManifestAndProfileSpec.md](05_BootManifestAndProfileSpec.md) | Defines the verified boot entry point implemented in M5 and required before M6 through M15. |
| [06_ServiceGraphRuntimeSpec.md](06_ServiceGraphRuntimeSpec.md) | Defines the runtime service subsystem implemented in M6 after the verified pipeline exists. |
| [07_ScopeGraphRuntimeSpec.md](07_ScopeGraphRuntimeSpec.md) | Defines the runtime scope subsystem implemented in M7 after M6. |
| [08_LifecyclePlanSpec.md](08_LifecyclePlanSpec.md) | Defines lifecycle dispatch implemented in M8 after scope runtime exists. |
| [09_CommandCatalogRuntimeSpec.md](09_CommandCatalogRuntimeSpec.md) | Defines the verified command runtime implemented in M9 after lifecycle and boot foundations exist. |
| [10_ValueSchemaAndStoreSpec.md](10_ValueSchemaAndStoreSpec.md) | Defines the value schema and store work implemented in M10 after the runtime core exists. |
| [10_1_ScalarRuntimeAndBindingSpec.md](10_1_ScalarRuntimeAndBindingSpec.md) | Defines the scalar specialization included inside M10. |
| [10_2_DynamicValueEvaluationSpec.md](10_2_DynamicValueEvaluationSpec.md) | Defines the dynamic and reactive specialization included inside M10. |
| [11_DebugMapAndDiagnosticsSpec.md](11_DebugMapAndDiagnosticsSpec.md) | Supplies the diagnostics contract that must be implemented early in M1 even though the spec number is higher. |
| [12_UnityAuthoringBridgeSpec.md](12_UnityAuthoringBridgeSpec.md) | Defines the Unity authoring bridge implemented in M11 once the verified pipeline and runtime core exist. |
| [13_LegacyCompatBoundarySpec.md](13_LegacyCompatBoundarySpec.md) | Defines quarantine-only legacy compatibility implemented in M12. |
| [14_PerformanceBudgetAndRuntimeRulesSpec.md](14_PerformanceBudgetAndRuntimeRulesSpec.md) | Defines performance and forbidden-runtime rules formalized in M13, with seed static gates already introduced in M1. |
| [15_TestAndValidationSpec.md](15_TestAndValidationSpec.md) | Supplies the executable protection model that must be partially implemented in M1 and completed in M15. |

16 is intentionally not a downstream runtime spec.
It is the execution-order contract that prevents the lower specs from being implemented in an unsafe sequence.

---

## Assembly Definition and Compile Boundary Expectations

Implementation order must treat asmdef work as architecture work, not as late cleanup.
Detailed dependency matrices remain owned by [17_AssemblyDefinitionAndCompileBoundarySpec.md](17_AssemblyDefinitionAndCompileBoundarySpec.md).

Required sequencing rules for 16:

- each runtime-facing milestone must identify the target assembly or assembly family before substantial implementation expands
- milestone completion is incomplete if code exists but still resides in an invalid monolithic compile unit that hides forbidden dependency direction
- compile-boundary tests and asmdef graph verification must appear early enough to stop later milestones from reintroducing feature, editor, or legacy back-references
- subsystem migration work must follow the low-volatility-first split order rather than starting with feature leaves

If a milestone claims completion without a credible asmdef residence and compile-boundary story, the 16 execution order has been violated.

---

## Implementation Ordering Principles

### 1. Proof Before Richness

The project must first be able to express failure, reject invalid structure, and record evidence.

This is why M1 exists before M2.
`KernelDiagnostic`, test artifact output, and static forbidden-pattern gates are not optional support work.
They are the mechanism that prevents later milestones from smuggling fallback behavior into the runtime.

### 2. Normalize Before Projecting

`KernelIR` and `ModuleContributionData` must exist before validation and generation.

No runtime plan may invent structure that cannot be traced back to normalized IR.
This is why M2 must precede M3 and M4.

### 3. Validate Before Generating

03 defines generation as a trust boundary, not a file-output step.

Therefore dependency validation, duplicate detection, cycle checks, and projection-validation hooks must exist before generated plans are considered runtime inputs.

### 4. Verify Before Booting

Boot is not a repair path.

M5 must only accept complete verified artifact sets, matching hashes, matching profile policy, and acceptable legacy-boundary status.
No later runtime milestone may assume boot can repair missing data through discovery or fallback.

### 5. Runtime Before Migration

Representative existing-feature migration begins only after the target pipeline exists.

Without M6 through M13, a migration effort tends to recreate legacy behavior behind new names.
This is why M14 and M15 are late milestones.

---

## Global Milestone Order

| Milestone | Name | Primary Goal | Exit Signal |
|---|---|---|---|
| M0 | Architecture Freeze / Spec Hygiene | Fix terminology, ownership, and forbidden-pattern visibility before implementation starts | 00 through 15 plus 10-1 and 10-2 are mapped into one concept and dependency matrix |
| M1 | Diagnostics / Test Foundation | Create the proof layer that catches violations early | Structured diagnostics, test artifacts, and static gates are operational |
| M2 | KernelIR / ModuleContribution Foundation | Create the normalized authority model | IR can be normalized, dumped, hashed, and traced to source |
| M3 | DependencyValidation | Create the pre-runtime firewall | duplicates, missing dependencies, invalid absence behavior, and cycles are rejected |
| M4 | VerifiedPlanGeneration | Create the verified artifact and projection boundary | only complete, deterministic, validated artifact sets can become verified |
| M5 | BootManifest / Profile | Create the verified boot entry point | boot accepts only compatible verified artifacts and fails closed |
| M6 | ServiceGraph | Create the coarse-grained verified service runtime | slot-based resolve works without scans, reflection fallback, or object-registry drift |
| M7 | ScopeGraph | Create the explicit runtime scope structure | generation-safe handles and explicit parent-child tables replace transform truth |
| M8 | LifecyclePlan | Create plan-driven lifecycle dispatch | lifecycle dispatch works without interface or registration scanning |
| M9 | CommandCatalog | Create verified command dispatch | `CommandTypeId` table dispatch works without executor discovery or string fallback |
| M10 | Value / Scalar / Dynamic Runtime | Create verified value runtime and its specializations | value access works without stable-key fallback or hidden evaluation |
| M11 | UnityAuthoringBridge | Connect Unity authoring to the verified pipeline | authoring extracts contributions and boots through verified artifacts only |
| M12 | LegacyCompat Boundary | Quarantine legacy compatibility | legacy is explicit, one-way, and non-repairing |
| M13 | Performance / RuntimeRules | Enforce measurable runtime architecture rules | profiler markers, forbidden-operation tests, and allocation gates exist |
| M14 | Existing Feature Migration | Move representative features through the target pipeline | selected features run through v2 without prohibited fallbacks |
| M15 | Integration / Direct Play / Regression Hardening | Prove the end-to-end architecture is protected | direct play, regression suite, CI gates, and legacy-removal evidence are operational |

Required order:

```text
M0 -> M1 -> M2 -> M3 -> M4 -> M5 -> M6 -> M7 -> M8 -> M9 -> M10 -> M11 -> M12 -> M13 -> M14 -> M15
```

The order above is normative for target-kernel implementation.
Parallel work is allowed only when it does not bypass the exit gate of an earlier milestone and does not create a target path before its prerequisites are complete.

---

## Milestone Gate Model

Each milestone has four required properties.

1. Required outputs
2. Exit gates
3. Forbidden shortcuts
4. Downstream unlocks

A milestone is not complete when code merely exists.
It is complete only when the next milestone can consume its output without runtime fallback, undocumented ownership transfer, or manual repair.

If an earlier milestone gate regresses, every later milestone depending on that gate returns to at-risk state until the regression is closed.

---

## Milestone Definitions

### M0: Architecture Freeze / Spec Hygiene

M0 fixes the document surface before implementation starts at scale.

Required outputs:

- M0.1 Spec hygiene pass across 00 through 15 and 10-1 / 10-2
- M0.2 Cross-spec concept map document at `Assets/Docs/v2/Index/KernelV2ConceptMap.md`
- M0.3 Forbidden pattern registry document at `Assets/Docs/v2/Index/ForbiddenPatternRegistry.md`
- M0.4 Cross-spec dependency matrix document at `Assets/Docs/v2/Index/CrossSpecDependencyMatrix.md`
- M0.5 Existing anchor inventory document at `Assets/Docs/v2/Index/ExistingAnchorInventory.md`

M0 concept map must cover at least the following terms without duplicated ownership:

- `KernelIR`
- `ModuleContribution`
- `VerifiedKernelPlan`
- `ArtifactSet`
- `DebugMap`
- `KernelDiagnostic`
- `ServiceGraph`
- `ScopeGraph`
- `LifecyclePlan`
- `CommandCatalog`
- `ValueSchema`
- `ValueStore`
- `RuntimeQuery`
- `UnityAuthoringBridge`
- `LegacyCompat`

Forbidden-pattern registry seed entries must include at least:

- direct `Debug.LogError`
- `GetComponentsInChildren` for runtime discovery
- `FindObjectsByType` for kernel lookup
- `Transform.parent` scope inference
- `Resources.Load` required-asset fallback
- runtime stable-key lookup
- runtime-generated negative IDs
- `IReadOnlyList<ICommandExecutor>` discovery
- `IScopeAcquireHandler` scan
- `IScopeTickHandler` scan
- ServiceGraph as runtime object registry
- BootManifest as global settings dump
- legacy fallback repair

Documentation hygiene note:

- known duplicate header content in 03 must be tracked and resolved or explicitly recorded before the team treats the doc set as frozen

Exit gates:

- every core concept has one owner spec
- every forbidden pattern in the registry maps to a lower spec or test gate
- the implementation sequence is reviewed against the dependency matrix
- implementation work can name its current anchor inventory rather than exploring the codebase ad hoc

Forbidden shortcuts:

- starting runtime subsystem implementation while concept ownership is still ambiguous
- treating review notes as a substitute for normative specs
- inferring architecture from legacy code when a current spec already exists

Downstream unlocks:

- M1 can define proof gates against a fixed forbidden-pattern vocabulary
- M2 can define IR types against stable concept ownership

### M1: Diagnostics / Test Foundation

M1 creates the proof layer that protects every later milestone.

Required outputs:

- M1.1 `KernelDiagnostic`, `DiagnosticCode`, `DiagnosticSeverity`, `DiagnosticDomain`, `DiagnosticFailureBoundary`, `DiagnosticContext`, `RuntimeIdentityRef`, `SourceLocationRef`, and `ArtifactIdentityRef`
- M1.2 `IKernelDiagnosticService`, `KernelDiagnosticService`, `IKernelDiagnosticSink`, `InMemoryDiagnosticSink`, `UnityLogDiagnosticSink`, and `TestDiagnosticSink`
- M1.3 timestamped test artifact output under `Logs/TestRuns/<timestamp>/`
- M1.4 static rule gates for forbidden APIs and direct Unity logging
- M1.5 documentation or test traceability from diagnostics codes to lower-spec failure meaning
- M1.6 static Debug gate
- M1.7 static forbidden-API gate

The minimum test artifact set must include:

- `TestRunSummary.md`
- `TestRunSummary.json`
- `DiagnosticsReport.json`
- `ValidationReport.json`
- `GenerationReport.json`
- `PerformanceReport.json`

The first static rules must detect at least:

- `Debug.LogError` outside approved sinks
- `Debug.LogWarning` outside approved sinks
- `Debug.LogException` outside approved sinks
- `Resources.Load` in target runtime paths
- `FindObjectsByType` in target runtime paths
- `GetComponentsInChildren` in target runtime paths
- `Transform.parent` scope inference in target runtime paths

Exit gates:

- every kernel subsystem can report through one structured diagnostic model
- tests can assert on `DiagnosticCode` through `TestDiagnosticSink`
- only `UnityLogDiagnosticSink` may call Unity Debug APIs for kernel diagnostics
- adding a forbidden API to a target runtime path causes a failing gate

Forbidden shortcuts:

- creating subsystem-specific logging pipelines that bypass the shared record model
- postponing forbidden-pattern gates until after runtime core work starts
- using formatted strings as diagnostic identity

Downstream unlocks:

- M2 through M15 can fail closed with structured evidence
- runtime-first work loses its justification because architecture drift is now observable

### M2: KernelIR / ModuleContribution Foundation

M2 creates the normalized authority layer.

Required outputs:

- M2.1 typed identity primitives for `ModuleId`, `ServiceId`, `ScopeAuthoringId`, `ScopePlanId`, `CommandTypeId`, `CommandExecutorId`, `CommandPayloadSchemaId`, `ValueKeyId`, `ValueSchemaId`, `LifecycleStepId`, `RuntimeQueryId`, and `SourceLocationId`
- M2.2 `SourceLocationIR`, `UnitySourceLocation`, `LegacySourceLocation`, and `GeneratedSourceLocation`
- M2.3 `ModuleDefinition`, `ModuleContributionData`, `ContributionItem`, `ContributionKind`, `ContributionSource`, `ContributionAvailability`, and `ContributionConflictPolicy`
- M2.4 `KernelIR`, `KernelIRHeader`, `ModuleIR`, `ServiceIR`, `ScopeIR`, `LifecycleIR`, `CommandIR`, `ValueKeyIR`, `RuntimeQueryIR`, `DependencyEdgeIR`, and `SourceLocationTable`
- M2.5 IR hash and dump or report output

Source location minimum provenance must include:

- asset GUID
- asset path
- local file ID
- scene path
- GameObject path
- component type
- property path
- legacy origin
- generated origin

Exit gates:

- normalized IR can be produced without touching runtime builder state
- IR nodes can be traced back to source locations
- semantic hash generation is deterministic for equivalent inputs
- contribution collection does not resolve live services or infer ownership from transform hierarchy

Forbidden shortcuts:

- touching runtime builder or live service resolution during contribution collection
- allowing raw `int` domain mixing in public identity boundaries
- delaying source provenance until after generation

Downstream unlocks:

- M3 can validate real normalized identities and dependency edges
- M4 can generate verified artifacts from a deterministic input model

### M3: DependencyValidation

M3 creates the pre-runtime firewall.

Required outputs:

- M3.1 `DependencyValidationReport`, `DependencyValidationIssue`, `ValidationResultStatus`, `ValidationSeverity`, and `ValidationPhase`
- M3.2 duplicate-ID and wrong-domain validation
- M3.3 missing required dependency and invalid dependency-kind validation
- M3.4 optional absence-behavior validation
- M3.5 phase-aware cycle detection across Build, Generate, Boot, Acquire, Runtime, Save, and EditorOnly
- M3.6 legacy leakage validation
- M3.7 projection-validation interface including `IProjectionValidationRule`, `ProjectionValidationReport`, `UnknownProjectedIdRule`, `DroppedMappingRule`, and `DebugMapCoverageRule`

Exit gates:

- duplicate, missing, invalid-phase, invalid-owner, and cycle issues are rejected before runtime
- validation issues can be converted into `KernelDiagnostic`
- optional dependencies without declared absence behavior cannot pass validation
- post-generation validation hooks are defined even if projections are still minimal

Forbidden shortcuts:

- pushing duplicate or missing dependency detection into runtime boot
- allowing runtime cycles without explicit policy
- allowing legacy leakage to survive because migration is incomplete

Downstream unlocks:

- M4 can distinguish generated from verified artifacts
- M5 can trust validation reports rather than rediscovering structure at boot

### M4: VerifiedPlanGeneration / ArtifactSet

M4 creates the verified generation trust boundary.

Required outputs:

- M4.1 artifact headers containing `ArtifactSetId`, `PlanId`, `ArtifactId`, `ArtifactKind`, `FormatVersion`, `KernelIRHash`, `RegistryHash`, `ProfileHash`, `DebugMapHash`, `GeneratedContentHash`, and `GeneratorVersion`
- M4.2 type-level separation between `GeneratedKernelPlan` and `VerifiedKernelPlan`
- M4.3 artifact-set staging and promotion transaction model
- M4.4 deterministic generation rules independent of dictionary order, reflection order, file system order, timestamp, and absolute path
- M4.5 minimal projections for ServiceGraph, ScopeGraph, LifecyclePlan, CommandCatalog, ValueSchema, RuntimeQuery, KernelDebugMap, GenerationReport, and ValidationReport
- M4.6 stale-artifact detection
- M4.7 DebugMap generation seed

Exit gates:

- partial artifact sets cannot become current verified artifacts
- stale, mismatched, or hash-incompatible artifacts are rejected
- DebugMap coverage is part of promotion, not an optional add-on
- same semantic inputs produce the same semantic hash and compatible artifact set

Forbidden shortcuts:

- passing generated output directly to runtime without verification
- treating old artifacts as valid after source or profile changes
- allowing generation to repair invalid IR by omission

Downstream unlocks:

- M5 can boot from verified artifact references only
- runtime milestones can consume projections without reintroducing discovery

### M5: BootManifest / Profile

M5 creates the verified boot entry point.

Required outputs:

- M5.1 `KernelProfile`, `KernelProfileKind`, `KernelProfilePolicy`, and `BootDiagnosticsPolicy`
- M5.2 `KernelBootManifest` containing `ManifestId`, `ProfileId`, `VerifiedArtifactSetRef`, `BootPolicyId`, and `DiagnosticsPolicy`
- M5.3 boot validation gates for artifact completeness, hash compatibility, validation success, required root scope presence, required root service presence, and legacy-bridge allowance by profile
- M5.4 boot failure boundary that never publishes a partially initialized target runtime as valid
- M5.5 minimal boot shell for empty IR, diagnostics, DebugMap, `KernelRuntime`, `ServiceGraph`, and root `ScopeGraph`
- M5.6 boot diagnostics

BootManifest must not become:

- a full service list dump
- a full command list dump
- a full value-key dump
- a full lifecycle-step dump
- a full scope-graph dump
- a direct executor-definition dump
- a fallback-rule container
- a scene-search-rule container

Exit gates:

- boot succeeds only with a complete compatible verified artifact set
- boot failure leaves no partially valid runtime published to callers
- profile policy can reject legacy or diagnostics mismatches before runtime work begins

Forbidden shortcuts:

- using boot as a repair path for missing services, scopes, or artifacts
- embedding runtime discovery rules into BootManifest
- allowing legacy fallback because the profile is Development

Downstream unlocks:

- M6 through M10 can assume a verified boot shell exists
- M11 direct-play flow has a fixed target boot entry point

### M6: ServiceGraph

M6 creates the verified service runtime for coarse-grained services only.

Required outputs:

- M6.1 service-eligibility classification rules and service-boundary inventory
- M6.2 `ServiceGraphPlan`, `ServiceEntryPlan`, `ServiceSlotPlan`, `ServiceFactoryRef`, `ServiceContractRef`, `ServiceLifetimeKind`, and `ServiceCardinalityKind`
- M6.3 slot-based resolver from `ServiceId` to slot index
- M6.4 approved factory forms: GeneratedStatic, GeneratedDelegate, ExplicitManual, and LegacyBridge only inside the legacy boundary
- M6.5 optional-service policy distinguishing required failure from declared optional absence behavior
- M6.6 scope-local service boundary definition
- M6.7 hub classification table for existing systems such as modal, tooltip, mesh, and animation sprite hubs

Services may represent:

- kernel-level coarse services
- project-level coarse services
- scene-level coarse services
- authored-scope coarse services

Services must not represent:

- per-entity runtime objects
- per-part runtime objects
- per-renderer runtime objects
- per-tooltip runtime objects
- per-channel-player runtime objects
- per-mesh-track runtime objects
- per-animation-player runtime objects

Exit gates:

- `ServiceId` resolution works through precomputed slots rather than scans
- no constructor reflection or fallback resolver is required on target paths
- optional service absence is explicit rather than silent fallback
- ServiceGraph does not degrade into a general-purpose runtime object registry

Forbidden shortcuts:

- raw type as primary lookup identity
- `IReadOnlyList<T>` discovery as service composition
- registration scan or constructor reflection in target runtime paths
- storing arbitrary gameplay runtime objects in ServiceGraph to avoid proper scope ownership

Downstream unlocks:

- M7 can define scope-local service boundaries against a real resolver
- M14 can classify existing hubs before migration rather than guessing service shape

### M7: ScopeGraph

M7 creates the explicit runtime scope structure.

Required outputs:

- M7.1 identity separation between `ScopeAuthoringId`, `ScopePlanId`, `ScopeHandle`, and `UnityObjectLink`
- M7.2 generation-safe `ScopeHandle { index, generation }`
- M7.3 `ScopeInstanceTable`, `ScopeSlot`, and explicit parent-child table
- M7.4 scope runtime state machine
- M7.5 scope-local boundaries for ServiceGraph, Lifecycle, ValueStore, RuntimeQuery notifications, and Unity links
- M7.6 `UnityObjectLink`
- M7.7 pooling invalidation rules

Representative scope states:

- Created
- Built
- Acquiring
- Active
- Releasing
- Inactive
- Destroying
- Destroyed
- Failed

Exit gates:

- stale handles are rejected after slot reuse
- parent-child relationships are explicit table data rather than transform truth
- nearest-scope search and `Transform.parent` inference are not required for target runtime behavior
- scope-local subsystem boundaries are explicit rather than hidden in MonoBehaviour ownership

Forbidden shortcuts:

- using transform hierarchy as scope authority
- using GameObject traversal to discover parentage or owner scope
- mixing runtime handle validity with Unity object lifetime assumptions

Downstream unlocks:

- M8 can dispatch lifecycle by scope state transition
- M10 and M11 can attach value and authoring links to explicit scope identity

### M8: LifecyclePlan

M8 creates plan-driven lifecycle dispatch.

Required outputs:

- M8.1 `LifecyclePlanId`, `LifecycleStepId`, `LifecyclePhase`, `LifecycleTargetRef`, `LifecycleActionKind`, and `LifecycleFailurePolicy`
- M8.2 dispatch tables for Acquire, Release, Tick, FixedTick, LateTick, Reset, and Destroy
- M8.3 ScopeGraph integration through state transitions
- M8.4 failure and rollback policy for partial acquire completion
- M8.5 tick budget policy
- M8.6 async lifecycle policy

Exit gates:

- lifecycle dispatch is driven by verified plans rather than interface or registration scans
- acquire failure can roll back completed work according to policy
- per-entity tick is rejected by default unless explicitly justified by lower-spec policy
- lifecycle failures emit `KernelDiagnostic` rather than ad hoc logs

Forbidden shortcuts:

- `GetAcquireHandlers()` style collection paths
- `IScopeTickHandler` scan
- service-registration scan to discover lifecycle participants
- assuming successful acquire because rollback is hard to implement

Downstream unlocks:

- M9 and M10 can integrate with explicit scope and phase transitions
- M14 migrations can map existing acquire or release debt into explicit steps

### M9: CommandCatalog

M9 creates verified command dispatch.

Required outputs:

- M9.1 command identities including `CommandTypeId`, `CommandCategoryId`, `CommandExecutorId`, `CommandPayloadSchemaId`, and `CommandAuthoringKeyId`
- M9.2 structured `CommandCatalogPlan` entries and grouped metadata tables: `CommandEntryPlan`, `CommandExecutorRef`, `CommandPayloadSchemaPlan`, `CommandModuleMetadata`, and `CommandCategoryMetadata`
- M9.3 executor lookup through `CommandTypeId -> ExecutorRef -> executor factory`
- M9.4 payload schema validation for required fields, type mismatch, unknown fields, target references, `ValueKeyId`, and runtime-query references
- M9.5 `CommandRunner`, `CommandFrame`, `CommandContext`, `CommandLocal`, cancellation, and failure boundary
- M9.6 control-flow and async commands including Sequence, If, Switch, For, Wait, Delay, Detached or Forget, and Cancel
- M9.7 timeout, cancellation, and loop-bound policy

Exit gates:

- command dispatch is table-driven by verified command identity
- executor discovery does not depend on bulk DI registration or runtime string lookup
- payload validation occurs before executor body execution
- control-flow commands declare their failure and timeout behavior explicitly

Forbidden shortcuts:

- `IReadOnlyList<ICommandExecutor>` discovery
- `.As<ICommandExecutor>()` or equivalent installer-driven executor registration as the target model
- string executor lookup or authoring-key dispatch on target runtime paths
- giant runtime installer as the command composition mechanism

Downstream unlocks:

- M10 and M14 can depend on verified command identity and schema
- M15 can include command dispatch in the minimal vertical slice

### M10: Value / Scalar / Dynamic Runtime

M10 creates the verified value runtime and its specializations.

Required outputs:

- M10.1 `ValueKeyId`, `ValueSchemaId`, `ValueStoreId`, `ValueKind`, `ValueStorageKind`, `ValueDefaultPolicy`, and `SavePolicy`
- M10.2 slot-based storage from `ValueKeyId` to typed backend, slot revision, store revision, and dirty signal
- M10.3 `ValueStoreInitPlan`, `ValueInitPlan`, `ValueInitEntry`, `OverwritePolicy`, `InitPhase`, and source provenance
- M10.4 table, record, record-list, row, column, and cell identity or revision model
- M10.5 `LayeredNumeric` pipeline with Base, PrefixMul, Add, SuffixMul, FinalClamp, Effective value, contribution handle, and revision tracking
- M10.6 scalar runtime specialization from 10-1
- M10.7 dynamic and reactive evaluation plans from 10-2
- M10.8 evaluation context, tracker, cache, dependency stamp, invalidation policy, and nested dependency capture
- M10.9 revision and dirty bridge

Exit gates:

- value reads and writes use verified IDs and slots rather than stable keys or runtime-generated negative IDs
- init plans define overwrite and phase behavior explicitly rather than relying on collection order
- scalar and dynamic evaluation are explicit plans rather than hidden behavior inside general value access
- hot-path value access does not depend on `Dictionary<string, object>` or schema inference from writes

Forbidden shortcuts:

- stable-key runtime lookup as normal value access
- runtime negative-ID creation as target behavior
- hidden dynamic evaluation during generic store access
- repeated construct or start initialization to repair missing state

Downstream unlocks:

- M11 can extract authoring values into verified init plans
- M14 can migrate Blackboard, Var, and DynamicValue behavior into bounded runtime forms

### M11: UnityAuthoringBridge

M11 connects Unity authoring to the verified pipeline.

Required outputs:

- M11.1 authoring source model including `UnityAuthoringSourceKind`, `UnitySourceLocation`, `UnityObjectLink`, and `AuthoringComponentKind`
- M11.2 stable `ScopeAuthoringId` policy including duplicate detection, copy and paste policy, prefab duplication policy, and variant override source tracing
- M11.3 contribution extraction pipeline from explicit authoring roots into `ModuleContributionData`
- M11.4 local authoring validation and diagnostics
- M11.5 direct-play generation path through extract, normalize, validate, generate temporary verified artifact set, and boot via BootManifest
- M11.6 authoring diagnostics

Exit gates:

- MonoBehaviour and ScriptableObject authoring describe runtime structure but do not construct runtime structure directly
- direct play uses the verified pipeline rather than runtime discovery repair
- authoring extraction produces source-traceable contributions and local diagnostics

Forbidden shortcuts:

- `IFeatureInstaller.InstallFeature` as the target authoring-to-runtime bridge
- builder mutation during authoring extraction
- `GetComponentsInChildren` runtime discovery or `Transform.parent` ownership inference as authoring truth
- allowing Play Mode to bypass validation because it is an editor path

Downstream unlocks:

- M14 can migrate representative features through real authoring inputs
- M15 can prove direct-play verified boot

### M12: LegacyCompat Boundary

M12 quarantines legacy compatibility rather than extending it.

Required outputs:

- M12.1 `LegacyCompatKind`, `LegacyAdapterDescriptor`, `LegacyRemovalPolicy`, and `LegacyMigrationReport`
- M12.2 dependency-direction enforcement allowing Legacy -> Adapter -> v2 only
- M12.3 explicit legacy adapters for installer, resolver, command, value, lifecycle, and authoring migration
- M12.4 release-profile rejection policy for runtime legacy adapters
- M12.5 resolver fallback rejection
- M12.6 value migration adapter
- M12.7 removal-policy tracking

Exit gates:

- target-kernel core does not depend on legacy types as a fallback path
- legacy usage is explicit, diagnosable, and removable
- Release profile can reject prohibited legacy runtime paths

Forbidden shortcuts:

- v2 -> Legacy -> fallback direction
- adding new target features through legacy APIs
- using legacy resolver behavior to repair missing target-kernel data

Downstream unlocks:

- M14 and M15 can measure migration residue rather than hide it
- performance and regression gates can treat legacy usage as bounded debt

### M13: Performance / RuntimeRules

M13 formalizes measurable runtime architecture rules.

Required outputs:

- M13.1 `RuntimePathKind` classification for HotPath, WarmPath, ColdPath, BootPath, EditorGenerationPath, ValidationPath, TestOnlyPath, and LegacyMigrationPath
- M13.2 profiler-marker taxonomy for Kernel.Boot, Kernel.ServiceGraph, Kernel.ScopeGraph, Kernel.Lifecycle, Kernel.CommandCatalog, Kernel.ValueStore, Kernel.DynamicEvaluation, Kernel.Diagnostics, Kernel.UnityBridge, and Kernel.LegacyCompat
- M13.3 forbidden-API tests for hierarchy scans, discovery, reflection construction, direct logging, string dispatch, stable-key access, and lifecycle scans
- M13.4 hot-path allocation tests for resolve, handle validation, tick dispatch, command dispatch, value read or write, dynamic cached read, and diagnostics-disabled trace path
- M13.5 performance report output
- M13.6 regression thresholds for allocation, elapsed time, baseline delta, and marker presence

Exit gates:

- performance rules are executable rather than documentary only
- target hot paths have profiler markers and allocation expectations
- forbidden operations are measured as architecture regressions

Forbidden shortcuts:

- claiming a path is fast without measurement
- postponing forbidden-operation tests until after feature migration
- allowing hidden allocation or string lookup because the current content scale is small

Downstream unlocks:

- M14 migrations can be accepted or rejected against explicit budgets
- M15 CI gates can include real performance smoke checks

### M14: Existing Feature Migration

M14 migrates representative existing features through the verified pipeline.

Required outputs:

- M14.1 ModalStack migration
- M14.2 Tooltip migration
- M14.3 MeshChannel migration
- M14.4 AnimationSprite migration
- M14.5 Blackboard / Var migration
- M14.6 CommandRunnerMB migration
- M14.7 Loading / Boot legacy migration

Representative migration rules:

- per-modal, per-layer, per-tooltip, per-channel-player, or per-animation-player runtime objects must not be promoted into target services
- camera fallback, null var-store fallback, scope-ancestor fallback, and stable-key fallback must be removed or quarantined behind explicit migration adapters
- hub-owned runtime objects remain hub-owned runtime objects rather than becoming service abuse

Exit gates:

- selected existing systems run through v2 contributions, validation, verified generation, boot, runtime, diagnostics, and performance rules
- representative legacy fallback patterns are removed or isolated
- debug-map traceability exists for migrated features
- Blackboard / Var migration keeps authored value surfaces usable while moving runtime authority to verified value identity and store boundaries
- CommandRunner migration keeps authored command surfaces usable while moving runtime authority to verified command identity and catalog boundaries

Forbidden shortcuts:

- renaming legacy installer patterns without changing their trust boundary
- migrating features before the target runtime path exists
- accepting gameplay success while architectural regressions remain invisible

Downstream unlocks:

- M15 can run a real minimal vertical slice using representative content
- legacy removal evidence becomes concrete rather than theoretical

### M15: Integration / Direct Play / Regression Hardening

M15 proves the end-to-end architecture.

Required outputs:

- M15.1 minimal vertical slice from Unity authoring source through contribution, IR, validation, generation, boot, service resolve, scope creation, lifecycle acquire, command dispatch, value access, and diagnostics output
- M15.2 direct-play verified flow using dirty check, extract, validate, generate, and boot
- M15.3 regression suite for direct logging, discovery, transform inference, `Resources.Load` fallback, service or executor discovery, lifecycle scan, command string dispatch, value stable-key runtime lookup, legacy fallback, stale artifact boot, and missing DebugMap in Development
- M15.4 CI gate including build, EditMode validation, EditMode generation, PlayMode minimal boot, static forbidden-pattern tests, diagnostics snapshot tests, performance smoke tests, and legacy-boundary tests
- M15.5 legacy-removal pass
- M15.6 documentation and test traceability completion

Exit gates:

- direct play and CI exercise the verified pipeline rather than a side path
- regression suite fails when architecture drift re-enters the runtime
- test and documentation traceability are complete enough that milestone status is auditable

Forbidden shortcuts:

- Play Mode fallback boot
- scene-discovery repair during integration
- treating green gameplay as sufficient proof without validation, generation, diagnostics, performance, and regression evidence

Downstream unlocks:

- the target-kernel architecture is protected rather than aspirational

---

## Forbidden Sequencing

The following starting points are explicitly forbidden as primary implementation entry points for Kernel v2.

```text
NG:
M6 ServiceGraph first
M9 CommandCatalog first
M10 ValueStore first
M14 Existing Feature Migration first
```

Reasons:

- `ServiceGraph` first tends to recreate the old DI container and runtime object registry pattern.
- `CommandCatalog` first tends to recreate bulk executor registration and runtime string dispatch debt.
- `ValueStore` first tends to recreate Blackboard v2 with stable-key fallback and hidden dynamic behavior.
- feature migration first tends to rename legacy behavior without changing trust boundaries.

The correct high-risk ordering rule is:

```text
Diagnostics / Test -> IR / Contribution -> Validation -> Generation -> Boot -> Runtime -> Migration -> Integration
```

If a team needs to prototype a later subsystem earlier, that prototype must remain explicitly non-authoritative and must not become the target runtime path until its prerequisite milestones are complete.

---

## Completion Rule

The following criteria apply to every milestone:

- all required outputs exist in source or documentation form
- at least one lower-spec rule or test gate can verify the milestone output
- the milestone's forbidden shortcuts remain absent on target runtime paths
- downstream milestones can consume the output without introducing runtime fallback

If these criteria are not met, the milestone is in progress regardless of how much code exists.

---

## Final Position

The most important implementation decision in this plan is placing M1 first.

The project does not begin by building a clever resolver.
It begins by building a structured failure surface, static forbidden-pattern gates, and test artifacts.

After that, the target kernel is built in the following order:

```text
KernelDiagnostic / Test Gates
-> KernelIR / ModuleContribution
-> DependencyValidation
-> VerifiedPlanGeneration
-> BootManifest / Profile
-> Runtime Subsystems
-> UnityAuthoringBridge
-> Legacy Quarantine
-> Performance Gates
-> Existing Feature Migration
-> Integration and Regression Hardening
```

This order makes the kernel verifiable before it becomes large.
That is the only reliable way to keep the v2 migration from collapsing into legacy behavior with better names.

## Test Cases

| Test Case | Purpose | Verification |
|---|---|---|
| TC-16-01 | Confirm implementation order is not the same as specification numbering. | The Purpose and Global Milestone Order sections must explicitly state that diagnostics, tests, IR, validation, generation, and boot precede runtime subsystem milestones. |
| TC-16-02 | Confirm M0 includes concept ownership, forbidden-pattern registry, dependency matrix, and anchor inventory work. | The M0 section must enumerate M0.2 through M0.5 and describe the minimum concept and forbidden-pattern coverage. |
| TC-16-03 | Confirm M1 is the first implementation milestone because diagnostics and tests are architecture protection, not late polish. | The Implementation Ordering Principles and M1 sections must define structured diagnostics, sink policy, test artifacts, and static gates as prerequisite work. |
| TC-16-04 | Confirm runtime-first entry points are explicitly rejected. | The Forbidden Sequencing section must list M6, M9, M10, and M14 as forbidden primary starting points and explain why. |
| TC-16-05 | Confirm final integration requires direct-play verified boot, regression gates, CI gates, and legacy-removal evidence. | The M15 section must define M15.1 through M15.6 and require end-to-end proof rather than gameplay-only success. |

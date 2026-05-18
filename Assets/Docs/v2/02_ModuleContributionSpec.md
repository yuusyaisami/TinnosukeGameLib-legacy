# Module Contribution Specification

## Document Status

- Document ID: 02_ModuleContributionSpec
- Status: Draft
- Role: declarative contribution contract from modules into KernelIR
- Depends on:
  - [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md)
  - [01_KernelIRSpec.md](01_KernelIRSpec.md)
- Provides foundation for:
  - 03_VerifiedPlanGenerationSpec.md
  - 04_DependencyValidationSpec.md
  - 05_BootManifestAndProfileSpec.md
  - 06_ServiceGraphRuntimeSpec.md
  - 07_ScopeGraphRuntimeSpec.md
  - 08_LifecyclePlanSpec.md
  - 09_CommandCatalogRuntimeSpec.md
  - 10_ValueSchemaAndStoreSpec.md
  - 11_DebugMapAndDiagnosticsSpec.md
  - 12_UnityAuthoringBridgeSpec.md

### Ownership

This document owns module identity, module ownership, contribution kinds, profile and availability declaration, source location requirements, deterministic contribution collection, conflict policy, and the rejection of installer-style mutation.

This document does not own runtime builder behavior, runtime storage layout, validation algorithms, boot policy, or generated code format.

---

## Purpose

This specification defines how module-level authoring inputs are turned into declarative contribution data that can be normalized into KernelIR.

ModuleContribution is a constrained declaration system, not an installer API.
It replaces runtime installer-style mutation with pure contribution data.

The target is to eliminate the current pattern where module code touches a runtime builder, discovers scope state, or registers services and executors directly during build.

If a module cannot describe itself without touching runtime state, it is not valid for the target kernel.

This specification exists to prevent the following failure modes:

- builder mutation as composition logic
- ownership inferred from transform hierarchy or scope traversal
- last-write-wins overrides hidden inside collection order
- silent fallback to runtime discovery or empty assets
- runtime service resolution during contribution collection
- installer logic that mixes declaration, registration, and lifecycle wiring in one path

---

## Scope

This specification defines:

- module identity and ownership
- module contribution record shape
- contribution kinds
- contribution collection pipeline
- module dependency declaration
- profile and availability declaration
- source location requirements
- determinism rules
- conflict policy
- migration boundary from legacy installers
- handoff boundaries to lower specs

This specification intentionally does not define:

- IR node layout details
- dependency validation algorithms
- runtime service storage layout
- scope handle layout
- lifecycle dispatcher implementation
- command execution algorithms
- value store memory layout
- boot manifest schema
- generated artifact format

This document must not become a hidden runtime implementation layer.
It is a declarative module contract, not a replacement runtime system.

---

## Relationship to Other Specs

| Spec | Relationship |
|---|---|
| [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md) | Defines the root architectural constraints that 02 must not violate |
| [01_KernelIRSpec.md](01_KernelIRSpec.md) | Defines the normalized IR contract that consumes module contributions |
| [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md) | Defines how validated KernelIR becomes verified plan artifacts |
| 04_DependencyValidationSpec.md | Consumes the dependency shape and ownership metadata declared here |
| 05_BootManifestAndProfileSpec.md | Consumes module availability and profile declarations |
| 06_ServiceGraphRuntimeSpec.md | Consumes service-related contributions projected from KernelIR |
| 07_ScopeGraphRuntimeSpec.md | Consumes scope-related contributions projected from KernelIR |
| 08_LifecyclePlanSpec.md | Consumes lifecycle step declarations projected from KernelIR |
| 09_CommandCatalogRuntimeSpec.md | Consumes command-related contributions projected from KernelIR |
| 10_ValueSchemaAndStoreSpec.md | Consumes value and init contributions projected from KernelIR |
| 11_DebugMapAndDiagnosticsSpec.md | Consumes source/debug metadata contributed here |
| 12_UnityAuthoringBridgeSpec.md | Produces authoring inputs that become module contribution sources |

02 is the declaration boundary that feeds normalized IR.
Lower specs must reference the concepts defined here rather than rediscovering modules from runtime behavior.

---

## Current Architecture Observations

この節は現行コードベースの観測結果を要約する。
ここは v2 target policy ではなく、移行元の事実整理である。

### Observation Traceability

Current module contribution observations must remain traceable to source code, migration notes, profiling evidence, or design review notes.

When this document is updated, observations that no longer match the current codebase must be removed or moved to legacy migration notes.

| Observation | Evidence Type | Expected Downstream Spec |
|---|---|---|
| Scope build still mixes installer discovery and build coordination | Source / Profiling | 03, 07, 08, 14 |
| Command executor registration is collected in bulk | Source / Profiling | 03, 09, 14 |
| Lifecycle and build callbacks are mixed in installer-style classes | Source | 03, 08 |
| Blackboard / Var / DynamicValue responsibilities overlap | Source | 03, 10 |
| Registry and catalog locators still hide fallback behavior | Source | 03, 05, 10, 11 |

### Representative Anchors

- [BaseLifetimeScope.cs](../../GameLib/Script/Common/LTS/Core/BaseLifetimeScope.cs) - current IFeatureInstaller entry point
- [ScopeFeatureInstallerUtility.cs](../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs) - nearest-scope ownership filtering and installer discovery
- [RuntimeLifetimeScope.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs) - install flow, installer collection, and scope build coordination
- [CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs) - bulk executor registration example
- [SceneFlowInstallerMB.cs](../../GameLib/Script/Project/System/SceneFlow/MB/SceneFlowInstallerMB.cs) - scene flow installer example
- [AudioInstallerMB.cs](../../GameLib/Script/Common/Audio/AudioInstallerMB.cs) - conditional service setup example
- [TimeInstallerMB.cs](../../GameLib/Script/Common/Time/TimeInstallerMB.cs) - acquire/release and build callback mixing example
- [LTSIdentityMB.cs](../../GameLib/Script/Common/LTS/Identity/MB/LTSIdentityMB.cs) - current scope identity authoring metadata
- [LTSIdentityService.cs](../../GameLib/Script/Common/LTS/Identity/Core/LTSIdentityService.cs) - runtime identity and registry integration
- [BaseLifetimeScopeRegistry.cs](../../GameLib/Script/Common/LTS/Registry/BaseLifetimeScopeRegistry.cs) - kind/id/category lookup style runtime query
- [CommandKeyResolver.cs](../../GameLib/Script/Common/Commands/VNext/Catalog/CommandKeyResolver.cs) - stable key and fallback behavior example
- [CommandCatalogService.cs](../../GameLib/Script/Common/Commands/VNext/Catalog/CommandCatalogService.cs) - key-based catalog lookup example
- [BlackboardMB.cs](../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs) - blackboard lifecycle and value-init mixing example
- [VarStore.cs](../../GameLib/Script/Common/Variables/VarStore/Core/VarStore.cs) - runtime value store example
- [VarIds.g.cs](../../GameLib/Script/Generated/VarIds.g.cs) - generated stable IDs example
- [DynamicVariant.cs](../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicVariant.cs) - dynamic payload example
- [LoadingScreenService.cs](../../GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs) - scene transition and discovery mixing example
- [UnityCollisionSystemMB.cs](../../GameLib/Script/Collision/Unity/UnityCollisionSystemMB.cs) - profile-like scattering example
- [CollisionIdCatalogLocator.cs](../../GameLib/Script/Collision/Core/CollisionIdCatalogLocator.cs) - asset locator example
- [CommandCatalogLocator.cs](../../GameLib/Script/Common/Commands/VNext/Catalog/CommandCatalogLocator.cs) - asset locator example

### Current Gaps

- module boundaries are not expressed as a single declarative contract
- module identity is not uniformly separated from folder, assembly, or scene structure
- profile / availability data is scattered across installers and asset locators
- contribution data is not collected through one deterministic normalization path
- fallback behavior can still hide missing declaration data
- contribution responsibilities are split across installer classes, registries, and catalog locators

---

## Module Authority

Module is the authoring-level ownership boundary.

Modules declare what they contribute.
They do not directly mutate runtime state.
They do not decide runtime boot behavior.
They do not resolve live services during contribution collection.

### Core Module Concepts

| Concept | Requirement |
|---|---|
| ModuleDefinition | Authoring-level declaration that groups one or more contributions; may be asset-backed or code-backed, but must not require runtime builder access |
| ModuleId | Stable typed identity; must not be inferred from folder name, assembly name, scene path, or component order |
| ModuleKind | Classification of module ownership, such as feature, content, bridge, system, or migration adapter; it is not a runtime behavior flag |
| ModuleVersion | Compatibility input for module semantics; if contribution meaning changes, the version must change |
| Ownership | Explicit declaration of which contribution domains the module controls |
| Availability | Declarative profile or build-target constraint; it must not depend on runtime expressions or scene discovery |

### Module Identity Rules

Module identity must be explicit and source-backed.

The following are forbidden as identity sources:

- folder path
- assembly name alone
- scene hierarchy position
- transform parentage
- component index order
- runtime object instance ID

Module identity must remain stable across editor sessions and across generation runs for the same source.

---

## Contribution Model

Contribution data is a declarative description of what a module wants KernelIR to contain.

Contribution collection must be pure, deterministic, and free of runtime side effects.

Each contribution item must include at minimum:

- contribution kind
- owner ModuleId
- source location
- stable name or stable ID input
- dependency references when relevant
- profile or availability declaration when relevant
- conflict policy metadata when relevant
- debug metadata when relevant

Contribution data is not a runtime instance container.
It must not store live services, scope instances, or mutable runtime caches.

### Contribution Pipeline

```text
Authoring Input
  -> ModuleDefinition / equivalent source
  -> ContributionData
  -> deterministic normalization
  -> KernelIR
  -> validation handoff
```

The collection and normalization path must not:

- touch runtime builders
- resolve live services
- read runtime scope state
- traverse scene hierarchy to infer ownership
- repair missing declarations through fallback assets

If a required declaration is missing, collection must fail with structured diagnostics.

---

## Contribution Kinds

This section enumerates the contribution kinds that 02 must support.
Each kind is a declaration shape, not an execution mechanism.

| Contribution Kind | Declares | Must Not | Hand-off |
|---|---|---|---|
| ServiceContribution | Service identity, lifetime intent, dependency references, factory metadata, profile availability | Instantiate the service or resolve it from the runtime container | 03, 06, 11 |
| CommandContribution | Command identity, authoring key mapping, payload schema, executor metadata | Bulk register executors or resolve executor identity from arbitrary strings | 03, 09, 11 |
| ValueContribution | Value identity, schema requirements, persistence metadata | Infer schema from the runtime store or create ad-hoc runtime keys | 03, 10, 11 |
| ValueInitContribution | Initial writes, default values, ordering hints | Hide reactive evaluation inside generic initialization | 03, 10 |
| ScopeContribution | Authored scope identity, parent constraints, ownership, attach/detach constraints | Infer scope from transform hierarchy or nearest scope ownership | 03, 07, 11 |
| LifecycleContribution | Explicit lifecycle step plan, phase ordering, dependencies | Auto-collect interface implementations or registration scans | 03, 08, 11 |
| RuntimeQueryContribution | Queryable runtime identity fields, categories, index requirements, ambiguity rules | Implement generic DI lookup or scene search as query semantics | 03, 07, 11 |
| DiagnosticsContribution | Stable debug name, source location, legacy origin, trace metadata | Reconstruct provenance at boot time | 03, 11 |
| AssetBindingContribution | Required assets, registry inputs, binding targets, availability notes | Hide Resources fallback or AssetDatabase locator behavior | 03, 05, 12 |
| CodeGenerationContribution | Generated projection requirements, target identity domains, generation prerequisites | Emit code directly or define generated artifact format here | 03 |

### ServiceContribution

ServiceContribution declares that a service must exist in the target kernel.

It may describe:

- service identity
- lifetime class
- dependency edges
- factory metadata
- profile availability

It must not create the service instance.
It must not resolve the service from a runtime container during collection.

### CommandContribution

CommandContribution declares command identity and executor-facing metadata.

It may describe:

- authoring key
- runtime identity mapping
- payload requirements
- executor metadata
- diagnostics metadata

It must not register executors as a side effect.
It must not treat authoring keys as runtime truth.

### ValueContribution

ValueContribution declares schema-level value presence.

It may describe:

- ValueKeyId or equivalent identity input
- type information
- persistence/save metadata
- default value constraints

It must not read the runtime store to infer schema.
It must not use runtime fallback to invent missing keys.

### ValueInitContribution

ValueInitContribution declares initial writes or default state for values.

It may describe:

- initial assignment
- initialization ordering
- profile-dependent defaults

It must not hide reactive evaluation or dynamic computation inside the same declaration.

### ScopeContribution

ScopeContribution declares authored scope ownership and attachment rules.

It may describe:

- scope authoring identity
- parent constraints
- attachment or spawn constraints
- ownership boundaries

It must not infer scope parentage from transform hierarchy.

### LifecycleContribution

LifecycleContribution declares explicit lifecycle ordering.

It may describe:

- lifecycle step identity
- ordering rules
- phase dependencies
- acquire/release or init semantics

It must not rely on interface auto-collection or registration scans.

### RuntimeQueryContribution

RuntimeQueryContribution declares queryable runtime identity and indexing requirements.

It may describe:

- runtime identity fields
- queryable categories
- uniqueness requirements
- ambiguity rules

It must not be implemented as generic DI resolution.

### DiagnosticsContribution

DiagnosticsContribution declares provenance and traceability data.

It may describe:

- stable debug name
- source location
- profile availability
- legacy migration origin

It must not reconstruct provenance at boot.

### AssetBindingContribution

AssetBindingContribution declares asset references or binding requirements needed by the plan.

It may describe:

- required assets
- registry inputs
- binding target identity

It must not hide asset discovery through fallback locators.

### CodeGenerationContribution

CodeGenerationContribution declares that downstream generation must produce code or generated metadata for the module.

It may describe:

- target projection kinds
- identity domains affected by generation
- generation prerequisites

It must not define generated code format or emit code directly.

---

## Dependency and Availability Rules

Modules may declare dependencies on other modules explicitly.

Hidden dependency transport through shared static access, scene order, or runtime discovery is forbidden.

### Dependency Declaration Rules

- dependency declaration must be explicit
- dependency identity must be stable and source-backed
- missing required dependency is a validation error
- dependency declaration must be deterministic
- dependency declaration must be represented in KernelIR

### Availability Rules

Availability is declarative.

It may express:

- profile selection
- build target selection
- editor/test/release availability
- platform family availability

It must not depend on runtime expression evaluation or hidden script logic.

If availability requires a more expressive condition than simple declaration, that condition must be modeled explicitly in a lower spec or feature-flag system.

### Source Location Rules

Every contribution item that can affect runtime behavior must carry source location metadata.

The source location must be sufficient for traceability into diagnostics and DebugMap generation.

---

## Conflict Policy

Conflict handling must fail closed.

The default policy is validation error.

The following are forbidden as implicit conflict resolution:

- last-write-wins
- silent override
- implicit merge by collection order
- runtime repair of duplicate ownership
- fallback to empty contribution data

If a lower spec ever needs an override mechanism, it must define the scope, reason, validation rule, diagnostics behavior, and removal condition explicitly.

02 itself does not define such an override mechanism.

---

## Determinism Rules

Contribution collection and normalization must be deterministic.

Given the same module definitions, availability inputs, and profile, the contribution set must be semantically identical.

Contribution processing must not depend on:

- enumeration order
- reflection order
- current time
- random values
- machine-local path values
- Unity object instance IDs
- scene load timing

Recommended stable ordering priority:

1. module identity
2. contribution kind
3. stable name or stable ID
4. source location
5. explicit dependency order when required

If deterministic ordering cannot be established, the contribution run must fail.

---

## Legacy Installer Rejection

The legacy `IFeatureInstaller.InstallFeature(builder, scope)` pattern is not the target model.

The following legacy behaviors are rejected as target contracts:

- scope discovery during installation
- ownership inference from runtime hierarchy
- direct service registration during collection
- bulk command executor registration as a discovery step
- lifecycle auto-collection from registrations
- debug binding mixed into installer mutation
- authoring and runtime concerns mixed in one execution path

Legacy installers may exist only as migration adapters outside the target kernel core.

Legacy adapters must be observable and must not become the source of new target-kernel behavior.

### Migration Mapping

| Legacy Pattern | Target Contribution Kind |
|---|---|
| `IFeatureInstaller` / `InstallFeature(builder, scope)` | ModuleContribution / ContributionData |
| `CommandRunnerMB` bulk executor registration | CommandContribution |
| `BlackboardMB` mixed value setup | ValueContribution / ValueInitContribution |
| `RuntimeLifetimeScope` installer discovery | ModuleContribution collection pipeline |
| `BaseLifetimeScopeRegistry` lookup role | RuntimeQueryContribution |
| `VarKeyRegistryLocator` fallback lookup | ValueContribution / AssetBindingContribution |
| `VarIdResolver` runtime negative ID repair | ValueContribution / DiagnosticsContribution |
| `CommandCatalogLocator` / `CollisionIdCatalogLocator` asset locator patterns | AssetBindingContribution |

This mapping is descriptive, not permissive.
It shows where existing responsibility must move; it does not authorize the old behavior to remain in the target kernel.

---

## Module Interaction with KernelIR

Modules do not write KernelIR directly at runtime.
They supply declarative contribution data that normalization turns into KernelIR.

The target architecture requires the following separation:

- module authoring owns contribution intent
- KernelIR owns normalized structure
- validation owns accept/reject decisions
- plan generation owns verified runtime projections

Modules must not introduce new structural identities outside the contribution contract.
If a module needs a new identity domain, that identity must be declared here and normalized in KernelIR before later specs consume it.

---

## Acceptance Criteria

02 is complete when it defines:

- module identity and ownership
- contribution record requirements
- contribution kinds
- collection and normalization pipeline
- dependency and availability declarations
- source location requirements
- determinism rules
- conflict policy
- legacy installer rejection
- migration mapping to target contribution kinds
- handoff boundaries to 03 and 04

02 is not complete if any runtime builder API, runtime resolution algorithm, or storage layout detail has escaped into the specification.

---

## Test Cases

| Test Case | Purpose | Verification |
|---|---|---|
| TC-02-01 | Confirm module contribution is declarative and does not accept runtime builder mutation. | The purpose and contribution pipeline sections must forbid builder access, live service resolution, and runtime state mutation. |
| TC-02-02 | Confirm module identity is explicit and source-backed. | The module authority section must define ModuleId, ModuleKind, ModuleVersion, Ownership, and Availability without deriving them from paths or hierarchy. |
| TC-02-03 | Confirm all required contribution kinds are represented. | The contribution kinds section must cover service, command, value, scope, lifecycle, runtime query, diagnostics, asset binding, and code generation. |
| TC-02-04 | Confirm dependency and availability are declared, not inferred. | The dependency and availability rules section must forbid hidden static access, runtime expression fallback, and scene discovery. |
| TC-02-05 | Confirm conflict policy fails closed. | The conflict policy section must reject last-write-wins, silent override, and implicit merge behavior. |
| TC-02-06 | Confirm legacy installer behavior is treated as migration-only. | The legacy installer rejection and migration mapping sections must describe old patterns as adapters, not target behavior. |

---

## Final Position

ModuleContribution is the explicit declaration boundary between authoring/module ownership and KernelIR normalization.

It exists to make runtime builder mutation impossible in the target path.
Modules declare contributions.
KernelIR normalizes them.
Validation decides whether they are acceptable.
Verified plan generation consumes the validated result.

If module behavior is not expressible as declarative contribution data, it does not belong in the target kernel core.
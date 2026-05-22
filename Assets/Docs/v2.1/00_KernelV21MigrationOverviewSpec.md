# Kernel v2.1 Migration Overview Specification

## Document Status

- Document ID: 00_KernelV21MigrationOverviewSpec
- Status: Draft
- Role: defines the live-migration contract that moves the current game from legacy runtime authority to Kernel v2 verified runtime authority while preserving a narrow gameplay-facing surface
- Depends on:
  - [../v2/00_KernelArchitectureOverviewSpec.md](../v2/00_KernelArchitectureOverviewSpec.md)
  - [../v2/01_KernelIRSpec.md](../v2/01_KernelIRSpec.md)
  - [../v2/02_ModuleContributionSpec.md](../v2/02_ModuleContributionSpec.md)
  - [../v2/03_VerifiedPlanGenerationSpec.md](../v2/03_VerifiedPlanGenerationSpec.md)
  - [../v2/04_DependencyValidationSpec.md](../v2/04_DependencyValidationSpec.md)
  - [../v2/05_BootManifestAndProfileSpec.md](../v2/05_BootManifestAndProfileSpec.md)
  - [../v2/06_ServiceGraphRuntimeSpec.md](../v2/06_ServiceGraphRuntimeSpec.md)
  - [../v2/07_ScopeGraphRuntimeSpec.md](../v2/07_ScopeGraphRuntimeSpec.md)
  - [../v2/08_LifecyclePlanSpec.md](../v2/08_LifecyclePlanSpec.md)
  - [../v2/09_CommandCatalogRuntimeSpec.md](../v2/09_CommandCatalogRuntimeSpec.md)
  - [../v2/10_ValueSchemaAndStoreSpec.md](../v2/10_ValueSchemaAndStoreSpec.md)
  - [../v2/10_1_ScalarRuntimeAndBindingSpec.md](../v2/10_1_ScalarRuntimeAndBindingSpec.md)
  - [../v2/10_2_DynamicValueEvaluationSpec.md](../v2/10_2_DynamicValueEvaluationSpec.md)
  - [../v2/11_DebugMapAndDiagnosticsSpec.md](../v2/11_DebugMapAndDiagnosticsSpec.md)
  - [../v2/12_UnityAuthoringBridgeSpec.md](../v2/12_UnityAuthoringBridgeSpec.md)
  - [../v2/13_LegacyCompatBoundarySpec.md](../v2/13_LegacyCompatBoundarySpec.md)
  - [../v2/14_PerformanceBudgetAndRuntimeRulesSpec.md](../v2/14_PerformanceBudgetAndRuntimeRulesSpec.md)
  - [../v2/15_TestAndValidationSpec.md](../v2/15_TestAndValidationSpec.md)
  - [../v2/16_ImplementationMilestoneOrderSpec.md](../v2/16_ImplementationMilestoneOrderSpec.md)
  - [../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md](../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md)
- Provides foundation for:
  - [01_WaveABootAndSceneEntryCutoverSpec.md](01_WaveABootAndSceneEntryCutoverSpec.md)
  - [02_WaveBScopeAndServiceCompositionCutoverSpec.md](02_WaveBScopeAndServiceCompositionCutoverSpec.md)
  - [03_WaveCCommandDispatchCutoverSpec.md](03_WaveCCommandDispatchCutoverSpec.md)
  - [04_WaveDValueBlackboardAndVarCutoverSpec.md](04_WaveDValueBlackboardAndVarCutoverSpec.md)
  - [05_WaveERepresentativeGameplaySystemsCutoverSpec.md](05_WaveERepresentativeGameplaySystemsCutoverSpec.md)
  - [06_WaveFLegacyRemovalAndHardeningSpec.md](06_WaveFLegacyRemovalAndHardeningSpec.md)
  - [07_KernelV21MigrationMilestoneOrderSpec.md](07_KernelV21MigrationMilestoneOrderSpec.md)

### Revision Note

This revision creates the root migration specification for Kernel v2.1.

v2.1 is not a second kernel.
It exists because the target-kernel specifications already describe the desired semantics, but the live game still enters through legacy runtime authority.

This document therefore defines the migration contract that sits between the current live game and the target kernel:

- what must be preserved while migration happens
- what may be replaced destructively
- how migration work is divided into waves
- what counts as completion versus non-completion

---

## Ownership

This specification owns:

- the role split between v2 target semantics and v2.1 live migration execution
- the preservation floor for gameplay-facing authoring and identity surfaces
- the destructive allowance for legacy wiring replacement
- the migration baseline that describes what is live today versus what already exists in the kernel path
- migration wave partitioning from Wave A through Wave F
- migration governance and completion criteria
- non-completion rules that prevent direct-play success from being misread as live-game completion

This specification does not own:

- target kernel semantics already owned by v2 specifications
- final runtime API signatures for target-kernel subsystems
- per-wave detailed normative migration steps beyond the summary level defined here
- final gameplay feature behavior unrelated to migration
- team planning, staffing, or calendar estimates

00 owns the migration contract for the live game.
It must not become a duplicate of the v2 kernel semantics.

---

## Purpose

This specification defines what v2.1 means.

Core statements:

```text
v2 defines target-kernel meaning.
v2.1 defines how the current live game reaches that target without freezing legacy architecture in place.

Gameplay-facing authoring and identity surfaces may be preserved.
Legacy boot, scene, resolver, command, loading, and blackboard wiring may be replaced destructively.

The migration is complete only when the currently playable game runs through the verified kernel path.
```

This document exists because the repository already contains real target-kernel pieces such as verified boot and direct-play preparation, but the live game remains centered on legacy `RuntimeLifetimeScope` and scene-root authority.

Without an explicit migration contract, implementation work would either:

- preserve too much of the current runtime wiring and rebrand it as v2
- or replace gameplay-facing surfaces that the project has explicitly chosen to keep stable during migration

---

## Scope

This specification defines:

- the preservation floor for live migration
- the destructive allowance for legacy architecture replacement
- the current migration baseline and representative anchors
- the wave model for migration work
- governance rules for how future wave specifications must be written
- the acceptance model for claiming v2.1 progress or completion

---

## Non-Goals

This specification does not define:

- a second semantic definition of boot, scope, lifecycle, command, value, or diagnostics behavior
- a promise that existing legacy architecture remains stable during migration
- a requirement to preserve current dependency injection, resolver traversal, scene-root composition, or loading implementation details
- a blanket backward-compatibility promise beyond the explicit preservation floor
- detailed steps for Wave B through Wave F

00 must not be used to argue that existing legacy wiring is protected simply because the live game currently depends on it.

---

## Relationship to the v2 Target Specs

| Spec | Relationship to v2.1 overview |
| --- | --- |
| [../v2/05_BootManifestAndProfileSpec.md](../v2/05_BootManifestAndProfileSpec.md) | Owns the verified boot entry semantics that live migration must eventually use. |
| [../v2/12_UnityAuthoringBridgeSpec.md](../v2/12_UnityAuthoringBridgeSpec.md) | Owns authoring extraction and direct-play verified-input policy. v2.1 must not misclassify direct play as live boot completion. |
| [../v2/13_LegacyCompatBoundarySpec.md](../v2/13_LegacyCompatBoundarySpec.md) | Owns quarantine rules for legacy coexistence. v2.1 uses that boundary when legacy wiring is demoted or removed. |
| [../v2/15_TestAndValidationSpec.md](../v2/15_TestAndValidationSpec.md) | Owns executable protection expectations. v2.1 must define acceptance in a way that 15 can test. |
| [../v2/16_ImplementationMilestoneOrderSpec.md](../v2/16_ImplementationMilestoneOrderSpec.md) | Defines the post-M15 baseline that v2.1 assumes exists before live cutover work begins. |
| [../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md](../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md) | Keeps migration code from smearing legacy and kernel compile boundaries together. |

v2.1 is downstream of v2.
When migration convenience conflicts with target semantics, v2 wins.

---

## Preservation Floor

The preservation floor is intentionally narrow.

The following surfaces must remain stable unless a later explicit v2.1 revision widens or changes the contract:

- existing Command field shape and payload meaning
- existing `DynamicValue` authoring surface
- existing `ValueStore` generated key identity

The preservation floor does not automatically include:

- lifetime-scope hierarchy shape
- resolver fallback behavior
- scene boot order implementation details
- `Resources.Load` fallback behavior
- `FindObjectsByType`-based runtime repair
- loading scene duplication cleanup behavior
- command executor bulk registration style
- blackboard initialization ownership

If a surface is not named in the preservation floor, it is replaceable.

---

## Destructive Allowance

The following areas may be replaced destructively in order to reach the target kernel path:

- boot entry and startup orchestration
- persistent root creation and ownership
- scene-root composition and authority direction
- scope build and resolver behavior
- command executor registration and dispatch authority
- loading orchestration ownership
- blackboard and value wiring ownership
- legacy adapter placement and removal

Destructive replacement is allowed precisely because v2.1 is not preserving the architecture.
It is preserving only the explicitly named gameplay-facing surfaces.

---

## Current Migration Baseline

The repository already contains target-kernel infrastructure, but the live game is not yet routed through it.

### Observation Traceability

| Observation | Evidence Type | Migration Pressure |
| --- | --- | --- |
| Verified boot artifacts and boundary surfaces already exist. | Source | live boot should converge on verified kernel entry rather than rebuild boot semantics inside legacy runtime |
| Direct-play authoring preparation already exists. | Source | direct play should be treated as a reference path, not as proof that the live game is migrated |
| `ProjectLifetimeScope` and `GlobalLifetimeScope` still create persistent roots through `RuntimeInitializeOnLoadMethod` entry points, scene search, and fallback instantiation. | Source | Wave A must replace live boot authority |
| `SceneFlowInstallerMB`, `SceneService`, and `LoadingScreenService` still own live scene transition and loading orchestration. | Source | Wave A must replace live scene entry and loading authority while preserving scene-change contracts |
| `SceneLifetimeScope`, `CommandRunnerMB`, and `BlackboardMB` still anchor live scene-root composition. | Source | Wave A and Wave B must demote scene roots from being boot authorities |
| `VarIdResolver` and `VarKeyRegistryLocator` still expose runtime fallback behavior. | Source | Wave D must preserve authoring identity while removing fallback truth creation |

### Representative Anchors

- [../../GameLib/Script/Kernel/Boot/KernelBootManifestModels.cs](../../GameLib/Script/Kernel/Boot/KernelBootManifestModels.cs)
- [../../GameLib/Script/Kernel/Boot/KernelBootBoundary.cs](../../GameLib/Script/Kernel/Boot/KernelBootBoundary.cs)
- [../../GameLib/Script/Kernel/Boot/KernelRuntimeShell.cs](../../GameLib/Script/Kernel/Boot/KernelRuntimeShell.cs)
- [../../Editor/KernelBoot/AuthoringBridge.cs](../../Editor/KernelBoot/AuthoringBridge.cs)
- [../../GameLib/Script/Project/LTS/ProjectLifetimeScope.cs](../../GameLib/Script/Project/LTS/ProjectLifetimeScope.cs)
- [../../GameLib/Script/Project/Global/LTS/GlobalLifetimeScope.cs](../../GameLib/Script/Project/Global/LTS/GlobalLifetimeScope.cs)
- [../../GameLib/Script/Project/Scene/LTS/SceneLifetimeScope.cs](../../GameLib/Script/Project/Scene/LTS/SceneLifetimeScope.cs)
- [../../GameLib/Script/Project/System/SceneFlow/MB/SceneFlowInstallerMB.cs](../../GameLib/Script/Project/System/SceneFlow/MB/SceneFlowInstallerMB.cs)
- [../../GameLib/Script/Project/System/SceneFlow/SceneManager/Service/SceneService.cs](../../GameLib/Script/Project/System/SceneFlow/SceneManager/Service/SceneService.cs)
- [../../GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs](../../GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs)
- [../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs)
- [../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs](../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs)
- [../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs)
- [../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs)
- [../../Scenes/TitleScene.unity](../../Scenes/TitleScene.unity)
- [../../Scenes/GameScene.unity](../../Scenes/GameScene.unity)

---

## Migration Waves

| Wave | Name | Primary Ownership | Exit Signal |
| --- | --- | --- | --- |
| A | [Wave A Boot and Scene Entry Cutover](01_WaveABootAndSceneEntryCutoverSpec.md) | live boot authority, persistent roots, scene entry, loading orchestration | the live game enters and transitions scenes through a verified kernel-owned path while keeping current scene-change surface stable |
| B | [Wave B Scope and Service Composition Cutover](02_WaveBScopeAndServiceCompositionCutoverSpec.md) | runtime scope and service composition authority | scene, persistent, and runtime pooled scopes stop relying on legacy installer-driven composition as architectural truth |
| C | [Wave C Command Dispatch Cutover](03_WaveCCommandDispatchCutoverSpec.md) | command registration and dispatch authority | preserved command payloads execute through the target command authority rather than legacy bulk registration |
| D | [Wave D Value, Blackboard, and Var Cutover](04_WaveDValueBlackboardAndVarCutoverSpec.md) | value, blackboard, and var identity cutover | preserved authoring identity remains stable while runtime fallback truth creation is removed |
| E | [Wave E Representative Gameplay Systems Cutover](05_WaveERepresentativeGameplaySystemsCutoverSpec.md) | representative feature migration onto the new authority path | selected gameplay systems operate on the migrated architecture without reverting to legacy authority |
| F | [Wave F Legacy Removal and Hardening](06_WaveFLegacyRemovalAndHardeningSpec.md) | legacy demotion, removal, and regression hardening | migration-only adapters are minimized or removed and acceptance is protected by executable gates |

Each wave specification must define:

- current-state anchors
- preserved contracts
- owned migration goals
- target authority model
- diagnostics and failure policy
- acceptance and non-completion rules

Wave ownership is not the same thing as milestone claim order.
Cross-wave claim order, gate sequencing, and downstream unlock rules are owned by [07_KernelV21MigrationMilestoneOrderSpec.md](07_KernelV21MigrationMilestoneOrderSpec.md).

---

## V21-M0 Baseline Freeze Artifacts

V21-M0 operational evidence lives under [Index/README.md](Index/README.md).
This overview continues to own the migration contract.
The v2.1 Index package owns the joined evidence that later milestone reviews consume.

Canonical V21-M0 artifacts:

- [Index/KernelV21BaselineLedger.md](Index/KernelV21BaselineLedger.md)
- [Index/KernelV21PreservationFloorLedger.md](Index/KernelV21PreservationFloorLedger.md)
- [Index/KernelV21ProofAnchorCatalog.md](Index/KernelV21ProofAnchorCatalog.md)

Upstream v2 references reused by V21-M0 rather than duplicated:

- [../v2/Index/KernelV2ConceptMap.md](../v2/Index/KernelV2ConceptMap.md)
- [../v2/Index/ForbiddenPatternRegistry.md](../v2/Index/ForbiddenPatternRegistry.md)
- [../v2/Index/CrossSpecDependencyMatrix.md](../v2/Index/CrossSpecDependencyMatrix.md)
- [../v2/Index/ExistingAnchorInventory.md](../v2/Index/ExistingAnchorInventory.md)

V21-M0 is therefore a migration baseline freeze layer, not a second copy of the v2 M0 architecture-freeze package.

---

## Governance Rules

Required migration governance:

- v2.1 may replace wiring, but it may not redefine v2 target semantics
- preservation floor changes require explicit documentation updates rather than silent implementation drift
- each wave must clearly separate current-state observation from target-state normativity
- direct play and live boot must be evaluated separately unless a wave explicitly proves equivalence
- legacy coexistence must remain quarantine-oriented and removable
- a wave is not complete if it preserves a legacy repair or discovery path as steady-state architecture

---

## Acceptance and Non-Completion Rules

v2.1 progress must be evaluated against the running game, not against isolated editor tooling.

The migration is not complete if any of the following remain true:

- the currently playable game still requires legacy runtime auto-bootstrap as the authoritative entry path
- direct play succeeds, but the live game still boots through legacy scene-root authority
- required scene transitions or loading presentation still depend on runtime search and fallback repair as steady-state behavior
- gameplay-facing authoring contracts were preserved only by keeping the entire legacy architecture intact

The migration is complete only when:

- the live game boots through the verified kernel path
- representative live scene transitions operate through migrated authority
- preserved authoring and identity surfaces remain stable
- remaining legacy coexistence is explicitly quarantined or removed
- executable validation can distinguish the migrated path from the legacy path

---

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V21-00-01 | Confirm v2.1 preserves a narrow surface rather than the whole legacy architecture. | The preservation-floor section must name only Command fields, DynamicValue, and ValueStore generated key identity. |
| TC-V21-00-02 | Confirm v2.1 explicitly allows destructive wiring replacement. | The destructive-allowance section must cover boot, scene, scope, command, loading, and blackboard wiring. |
| TC-V21-00-03 | Confirm the current baseline distinguishes existing kernel infrastructure from live-game routing. | The baseline section must mention both verified boot surfaces and remaining legacy live authority. |
| TC-V21-00-04 | Confirm migration is partitioned into Wave A through Wave F. | The migration-waves section must enumerate all six waves and assign each a distinct ownership slice. |
| TC-V21-00-05 | Confirm direct-play success is not treated as migration completion. | The acceptance rules must explicitly separate direct play from live-game completion. |
| TC-V21-00-06 | Confirm the overview points to the V21-M0 operational artifact set. | This file must link to Index/README.md and the three v2.1 Index ledgers. |
| TC-V21-00-07 | Confirm the overview reuses the v2 M0 package by reference rather than duplicating it. | This file must link to the v2 Index concept map, forbidden-pattern registry, dependency matrix, and anchor inventory as upstream references. |

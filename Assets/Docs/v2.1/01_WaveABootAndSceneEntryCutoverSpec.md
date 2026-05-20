# Wave A Boot and Scene Entry Cutover Specification

## Document Status

- Document ID: 01_WaveABootAndSceneEntryCutoverSpec
- Status: Draft
- Role: defines the Wave A migration contract that cuts over live boot authority, persistent root ownership, scene entry orchestration, and loading authority while preserving the existing gameplay-facing scene-transition surface
- Depends on:
  - [00_KernelV21MigrationOverviewSpec.md](00_KernelV21MigrationOverviewSpec.md)
  - [../v2/05_BootManifestAndProfileSpec.md](../v2/05_BootManifestAndProfileSpec.md)
  - [../v2/12_UnityAuthoringBridgeSpec.md](../v2/12_UnityAuthoringBridgeSpec.md)
  - [../v2/13_LegacyCompatBoundarySpec.md](../v2/13_LegacyCompatBoundarySpec.md)
  - [../v2/15_TestAndValidationSpec.md](../v2/15_TestAndValidationSpec.md)
  - [../v2/16_ImplementationMilestoneOrderSpec.md](../v2/16_ImplementationMilestoneOrderSpec.md)
  - [../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md](../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md)
- Provides foundation for:
  - [02_WaveBScopeAndServiceCompositionCutoverSpec.md](02_WaveBScopeAndServiceCompositionCutoverSpec.md)
  - [05_WaveERepresentativeGameplaySystemsCutoverSpec.md](05_WaveERepresentativeGameplaySystemsCutoverSpec.md)
  - later scene-facing migration work that depends on a verified live entry path

### Revision Note

This revision creates the first detailed v2.1 wave specification.

Wave A exists because the live game still enters through legacy `RuntimeInitializeOnLoadMethod` and lifetime-scope authority even though verified boot and direct-play preparation already exist in the repository.

The purpose of this wave is not to redesign gameplay-facing scene-change authoring.
Its purpose is to move boot and scene entry ownership to the verified kernel path without forcing the project to rewrite existing scene-transition commands first.

---

## Ownership

This specification owns:

- the current-state inventory for live boot and scene entry authority
- the preserved gameplay-facing contracts that must survive the cutover
- the target authority model for live boot, persistent roots, scene entry, and loading orchestration
- the subphase structure for Wave A execution
- the diagnostics and failure conditions that distinguish acceptable cutover from mixed authority drift
- the acceptance criteria for claiming that Wave A is complete

This specification does not own:

- target-kernel boot semantics already owned by v2
- long-term scope and service graph semantics beyond the boot and scene-entry slice
- command catalog redesign beyond the preserved scene-change contract
- value, blackboard, and var identity redesign beyond the continuity required to keep scene transitions observable
- gameplay-system migration outside boot and scene entry

Wave A owns the live entry cutover.
It must not become a substitute for Wave B, Wave C, or Wave D.

---

## Purpose

Wave A defines how the running game stops being booted by legacy root discovery and starts being booted by the verified kernel path.

Core statements:

```text
The live game must enter through verified boot input.
Legacy RuntimeInitializeOnLoadMethod root creation must stop being the authoritative path.

Existing scene-change authoring stays stable.
Boot, loading, and scene-entry ownership may change underneath it.

Direct play is a useful reference path.
It is not Wave A completion by itself.
```

This wave therefore focuses on authority, not on authoring shape.

---

## Scope

Wave A defines:

- live boot authority replacement
- persistent root ownership replacement
- initial scene entry handoff to the verified runtime surface
- scene transition authority cutover behind preserved scene-change contracts
- loading orchestration cutover behind preserved loading configuration surfaces
- mixed-authority diagnostics and acceptance gates for the above

---

## Non-Goals

Wave A does not define:

- the full replacement of legacy scope composition internals
- the full replacement of legacy command registration internals
- the full migration of blackboard, var registry, or dynamic-value runtime semantics
- representative gameplay-system migration beyond scene entry and loading behavior
- the final deletion of every legacy scene-root component from every scene

Wave A may demote legacy scene-root authority.
It does not have to finish every later-wave cleanup task.

---

## Relationship to Other Specs

| Spec | Relationship to Wave A |
| --- | --- |
| [00_KernelV21MigrationOverviewSpec.md](00_KernelV21MigrationOverviewSpec.md) | Defines the preservation floor, destructive allowance, and wave partitioning that Wave A must obey. |
| [../v2/05_BootManifestAndProfileSpec.md](../v2/05_BootManifestAndProfileSpec.md) | Owns verified boot entry semantics, deterministic boot phases, and persistent-root policy consumed by Wave A. |
| [../v2/12_UnityAuthoringBridgeSpec.md](../v2/12_UnityAuthoringBridgeSpec.md) | Owns direct-play verified-input policy. Wave A uses direct play only as a reference path and not as live authority. |
| [../v2/13_LegacyCompatBoundarySpec.md](../v2/13_LegacyCompatBoundarySpec.md) | Owns quarantine rules that govern how legacy scene roots may coexist temporarily during cutover. |
| [../v2/15_TestAndValidationSpec.md](../v2/15_TestAndValidationSpec.md) | Owns executable proof requirements for the acceptance cases defined here. |
| [../v2/16_ImplementationMilestoneOrderSpec.md](../v2/16_ImplementationMilestoneOrderSpec.md) | Confirms that Wave A starts after the target boot and validation foundations are already available. |
| [../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md](../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md) | Prevents boot-cutover code from dissolving kernel and legacy compile boundaries during migration. |

Wave A consumes v2 semantics.
It does not soften them for migration convenience.

---

## Current-State Authority Inventory

This section records the current live authority chain.
It is migration evidence, not target policy.

### Observation Traceability

| Observation | Evidence Type | Migration Pressure |
| --- | --- | --- |
| `ProjectLifetimeScope` creates the project root from `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)`, scene search, `Resources.Load`, or fallback `new GameObject`. | Source | live boot still begins from legacy auto-bootstrap rather than verified boot input |
| `GlobalLifetimeScope` creates another persistent root from `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)` and depends on `ProjectLifetimeScope.EnsureExists()`. | Source | persistent root ownership is still legacy and hierarchy-driven |
| `SceneFlowInstallerMB` registers `ILoadingScreenConfig`, `LoadingScreenService`, `SceneService`, and `SceneFlowBlackboardInitializer` inside the current project scope. | Source | scene entry and loading ownership are still being installed by legacy project wiring |
| `LoadingScreenService` scans `GlobalLifetimeScope`, `PlatformLifetimeScope`, and `ProjectLifetimeScope`, cleans up duplicates, and instantiates a loading `SceneLifetimeScope` under the discovered parent. | Source | loading authority still depends on runtime discovery and duplicate repair |
| `SceneService` owns `GameScene` to scene-name mapping, `LoadSingle`, `LoadAdditive`, `Unload`, loading display policy, active-scene switching, and project blackboard `IsLoading` writes. | Source | Wave A must preserve this external contract while replacing its ownership path |
| `SceneChangeExecutor` resolves `ISceneService` through scope-ancestor traversal and executes the preserved scene-change payload. | Source | Wave A must preserve the gameplay-facing command surface while redirecting authority underneath it |
| Live representative scenes remain composed around legacy scene-root components such as `SceneLifetimeScope`, `CommandRunnerMB`, and `BlackboardMB`. | Source | scene roots still participate as live composition authority and must be demoted from boot authority |

### Representative Anchors

- [../../GameLib/Script/Project/LTS/ProjectLifetimeScope.cs](../../GameLib/Script/Project/LTS/ProjectLifetimeScope.cs)
- [../../GameLib/Script/Project/Global/LTS/GlobalLifetimeScope.cs](../../GameLib/Script/Project/Global/LTS/GlobalLifetimeScope.cs)
- [../../GameLib/Script/Platform/LTS/PlatformLifetimeScope.cs](../../GameLib/Script/Platform/LTS/PlatformLifetimeScope.cs)
- [../../GameLib/Script/Project/Scene/LTS/SceneLifetimeScope.cs](../../GameLib/Script/Project/Scene/LTS/SceneLifetimeScope.cs)
- [../../GameLib/Script/Project/System/SceneFlow/MB/SceneFlowInstallerMB.cs](../../GameLib/Script/Project/System/SceneFlow/MB/SceneFlowInstallerMB.cs)
- [../../GameLib/Script/Project/System/SceneFlow/SceneManager/Service/SceneService.cs](../../GameLib/Script/Project/System/SceneFlow/SceneManager/Service/SceneService.cs)
- [../../GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs](../../GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs)
- [../../GameLib/Script/Common/Commands/VNext/Commands/Scene/SceneChangeCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/Scene/SceneChangeCommandData.cs)
- [../../GameLib/Script/Common/Commands/VNext/Executors/Scene/SceneChangeExecutor.cs](../../GameLib/Script/Common/Commands/VNext/Executors/Scene/SceneChangeExecutor.cs)
- [../../GameLib/Script/Generated/EventKeys.g.cs](../../GameLib/Script/Generated/EventKeys.g.cs)
- [../../GameLib/Script/Kernel/Boot/KernelBootManifestModels.cs](../../GameLib/Script/Kernel/Boot/KernelBootManifestModels.cs)
- [../../GameLib/Script/Kernel/Boot/KernelBootBoundary.cs](../../GameLib/Script/Kernel/Boot/KernelBootBoundary.cs)
- [../../Editor/KernelBoot/AuthoringBridge.cs](../../Editor/KernelBoot/AuthoringBridge.cs)
- [../../Scenes/TitleScene.unity](../../Scenes/TitleScene.unity)
- [../../Scenes/GameScene.unity](../../Scenes/GameScene.unity)

---

## Preserved Contracts

Wave A preserves the following gameplay-facing contracts.

| Contract Surface | Current Anchor | Wave A Requirement |
| --- | --- | --- |
| `SceneChangeCommandData` field shape and payload meaning | [../../GameLib/Script/Common/Commands/VNext/Commands/Scene/SceneChangeCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/Scene/SceneChangeCommandData.cs) | `Mode`, `TargetMode`, `Scene`, `SceneName`, `ForceReload`, and `DelayLoadingCommandToSceneChangeSec` must remain present and must not be silently reinterpreted. |
| `SceneChangeExecutor` scene-transition behavior contract | [../../GameLib/Script/Common/Commands/VNext/Executors/Scene/SceneChangeExecutor.cs](../../GameLib/Script/Common/Commands/VNext/Executors/Scene/SceneChangeExecutor.cs) | Existing scene-change commands must still resolve an `ISceneService`-compatible authority and produce the same high-level load, additive-load, and unload outcomes. |
| `GameScene` enum and `ToSceneName()` mapping for existing scenes | [../../GameLib/Script/Project/System/SceneFlow/SceneManager/Service/SceneService.cs](../../GameLib/Script/Project/System/SceneFlow/SceneManager/Service/SceneService.cs) | Existing canonical mappings such as `Title -> TitleScene` and `Game -> GameScene` must remain stable. |
| Loading configuration authoring surface | [../../GameLib/Script/Project/System/SceneFlow/MB/SceneFlowInstallerMB.cs](../../GameLib/Script/Project/System/SceneFlow/MB/SceneFlowInstallerMB.cs) | `ILoadingScreenConfig` semantics for lead time, loading prefab, show or progress or hide commands, and message or progress vars must remain usable by existing content. |
| Scene-loading observability | [../../GameLib/Script/Project/System/SceneFlow/SceneManager/Service/SceneService.cs](../../GameLib/Script/Project/System/SceneFlow/SceneManager/Service/SceneService.cs) | The project-level `IsLoading` signal must remain observable during representative transitions, even if its ownership moves later. |
| Generated scene event keys | [../../GameLib/Script/Generated/EventKeys.g.cs](../../GameLib/Script/Generated/EventKeys.g.cs) | Existing keys such as `GameLib.Scene.OnOpenGameScene`, `GameLib.Scene.OnCloseGameScene`, `GameLib.Scene.OnOpenTitleScene`, and `GameLib.Scene.OnCloseTitleScene` must remain valid emitted contracts. |

Preserved does not mean frozen implementation.
It means stable external contract.

---

## Owned Migration Goals

Wave A must achieve all of the following:

- move live boot entry to verified boot input and `KernelBootBoundary`-style validation
- move persistent root ownership from legacy auto-bootstrap to verified boot-defined roots
- move initial scene entry ownership to the verified runtime surface
- keep scene-change command authoring stable while replacing the authority behind it
- move loading presentation ownership away from runtime search, duplicate cleanup, and fallback instantiation
- demote legacy scene-root components so they are not the authoritative live entry path
- make mixed legacy and verified authority detectable and unacceptable

---

## Target Authority Model

Wave A target authority is defined by the following required model.

### Required Live Boot Chain

1. Live boot obtains a verified `KernelBootManifest` reference.
2. Boot validates the referenced artifact set, profile compatibility, and required debug or validation gates.
3. `KernelBootBoundary` creates the runtime shell only from verified inputs.
4. Required persistent roots are created or bound only if they are explicitly defined by verified boot input.
5. The runtime shell creates or exposes the live scene-entry authority as part of verified runtime state.
6. Initial scene opening and subsequent scene transitions are requested through that authority.
7. Loading presentation is created or bound from verified boot-owned inputs rather than scene-wide discovery.
8. Only after the verified runtime surface is ready may representative scene-change commands execute.

This model directly consumes the v2 rules that:

- boot phases must be deterministic
- boot must not perform broad runtime discovery
- persistent roots must be defined by verified inputs
- direct play must still use verified inputs

### Authority Replacement Rules

Wave A requires the following replacement rules:

- `ProjectLifetimeScope.EnsureInScene()` and `GlobalLifetimeScope.EnsureInScene()` must stop being the authoritative live boot path by Wave A completion
- live scene entry must not depend on legacy root search, fallback prefabs, or surprise `new GameObject` repair
- loading presentation must not depend on `FindObjectsByType`, duplicate cleanup, or fallback parent discovery in the accepted state
- direct play may invoke the same verified path, but it must not remain a privileged or separate success path
- legacy scene-root components may continue to exist temporarily, but only as downstream participants or quarantine adapters

### Transitional Coexistence Rules

| Transitional Condition | Allowed During Wave A | Required Rule |
| --- | --- | --- |
| Representative scenes still contain `SceneLifetimeScope`, `CommandRunnerMB`, or `BlackboardMB` | Yes | They may host migrated or not-yet-migrated systems after scene entry, but they must not own live boot authority. |
| Existing commands still resolve `ISceneService` through an ancestor path | Yes | The resolved service must originate from the Wave A authority path or a clearly bounded compatibility adapter. |
| A compatibility wrapper keeps existing loading command lists and vars usable | Yes | The wrapper must preserve the authoring surface without reintroducing scene-wide search as steady-state architecture. |
| Legacy `RuntimeInitializeOnLoadMethod` boot hooks still exist in code during transition | Temporarily | They must be diagnosable as legacy-only and must not be required for the accepted live path. |
| Runtime duplicate cleanup or keep-first behavior for persistent or loading roots | No | Accepted Wave A behavior must fail validation or boot instead of silently repairing duplicates. |

---

## Wave A Subphases

### WA-0 Current-State Inventory

Objective:
freeze the current boot and scene-entry authority map before cutover work starts.

Required outputs:

- representative anchor inventory
- preserved contract table
- explicit list of live boot entry points and loading ownership points

Exit gate:
the current live authority chain is traceable from startup through representative scene transition.

Forbidden shortcuts:

- replacing services before preserved contracts are written down
- assuming direct-play behavior proves live-game routing

### WA-1 Boot Authority Isolation

Objective:
make verified boot the only intended live entry path.

Required outputs:

- explicit live boot host or entry selection path
- legacy auto-bootstrap diagnostics for accidental activation
- removal or quarantine plan for legacy `RuntimeInitializeOnLoadMethod` authority

Exit gate:
live startup can be initiated through verified boot without depending on legacy root discovery.

Forbidden shortcuts:

- leaving legacy auto-bootstrap active as a silent fallback
- calling verified boot only after legacy roots have already established authority

### WA-2 Live Scene Entry Handoff

Objective:
transfer initial scene opening from legacy scene-root ownership to verified runtime ownership.

Required outputs:

- explicit initial scene selection contract
- explicit boundary between persistent runtime state and scene-specific state
- scene-entry authority reachable from the verified runtime shell

Exit gate:
representative live startup reaches the initial playable scene through verified authority.

Forbidden shortcuts:

- scene search to discover the initial root at runtime
- inferring initial scene ownership from already-loaded scene objects

### WA-3 Scene Transition Contract Cutover

Objective:
keep scene-change authoring stable while redirecting runtime ownership.

Required outputs:

- `ISceneService`-compatible scene-transition surface for existing commands
- preserved `GameScene` mapping for representative scenes
- explicit transition ownership from the verified runtime path

Exit gate:
representative `LoadSingle`, `LoadAdditive`, and `Unload` flows operate through the new authority while keeping existing command payloads valid.

Forbidden shortcuts:

- changing `SceneChangeCommandData` shape
- preserving behavior only by keeping the old project-scoped registration as the authoritative path

### WA-4 Loading Orchestration Cutover

Objective:
move loading presentation ownership under verified boot and scene-entry authority.

Required outputs:

- boot-defined loading presentation root or equivalent verified input
- preserved loading config adapter for existing content
- elimination of scene-wide parent discovery and duplicate cleanup from accepted behavior

Exit gate:
representative scene transitions show, update, and hide loading presentation without relying on runtime discovery repair.

Forbidden shortcuts:

- `FindObjectsByType` for required loading-root discovery in the accepted path
- silently keeping the first duplicate loading scene and destroying the rest
- using `Resources.Load` fallback for required loading presentation assets

### WA-5 Legacy Scene-Root Demotion

Objective:
ensure scene-root components are no longer mistaken for boot authority.

Required outputs:

- clear quarantine or demotion rules for legacy scene roots
- mixed-authority diagnostics when legacy boot hooks still attempt to participate
- documented handoff from live entry ownership to downstream scene participation

Exit gate:
the live game no longer requires legacy persistent-root or scene-root authority to enter and transition scenes.

Forbidden shortcuts:

- claiming demotion while boot still succeeds only because legacy roots exist
- leaving legacy boot hooks enabled without diagnostics or profile control

### WA-6 Acceptance Gate

Objective:
prove that Wave A is materially complete.

Required outputs:

- executable verification plan tied to representative startup and scene-transition cases
- diagnostics coverage for mixed authority and missing boot input
- documentation updates that reflect the accepted authority model

Exit gate:
all Wave A acceptance criteria and required test cases pass.

Forbidden shortcuts:

- accepting Wave A because editor direct play works
- accepting Wave A without a failing mixed-authority or missing-input story

---

## Forbidden Shortcuts

The following shortcuts are explicitly forbidden for Wave A:

- treating direct-play success as proof that the live game has migrated
- keeping `ProjectLifetimeScope` or `GlobalLifetimeScope` auto-bootstrap as a silent live fallback
- preserving loading behavior by leaving runtime search and duplicate cleanup as the steady-state authority path
- changing scene-change payload shape in order to make cutover easier
- expanding the preservation floor to include the whole legacy scene-flow architecture
- claiming completion while the verified path still depends on legacy scene-root existence to repair itself

---

## Diagnostics and Failure Policy

v2 specification 11 owns the diagnostics substrate.
Wave A defines the conditions that must become diagnostic-visible and acceptance-visible.

| Code | Failure Condition | Required Result |
| --- | --- | --- |
| V21-WA-BOOT-001 | BootManifest, artifact set, or required profile input is missing for the live path. | Boot must be blocked before runtime shell creation. |
| V21-WA-BOOT-002 | Legacy `RuntimeInitializeOnLoadMethod` root creation participates in a profile that claims Wave A cutover. | Validation or boot must fail with a mixed-authority diagnostic instead of silently continuing. |
| V21-WA-BOOT-003 | A required persistent root is created by search, fallback prefab load, or surprise `new GameObject` repair. | Boot acceptance must fail because persistent roots are not verified inputs. |
| V21-WA-SCENE-001 | A preserved `GameScene` mapping or representative scene-name target cannot be resolved deterministically. | Transition must fail deterministically with structured diagnostics rather than ad hoc fallback. |
| V21-WA-LOAD-001 | Loading presentation requires scene-wide discovery, duplicate cleanup, or fallback instantiation in the accepted path. | Wave A acceptance must fail because loading authority is still legacy. |
| V21-WA-EVENT-001 | Representative open or close scene events stop emitting the preserved generated keys. | Acceptance must fail because the gameplay-facing scene contract regressed. |
| V21-WA-ACCEPT-001 | Direct play succeeds but the live entry path still fails or routes differently. | Wave A must be treated as incomplete until live and direct-play paths converge on verified input semantics. |

---

## Acceptance Criteria

Wave A is complete only when all of the following are true:

- representative live startup enters through verified boot input rather than legacy auto-bootstrap authority
- persistent roots required for live startup are explicit verified inputs rather than discovered surprises
- representative startup reaches the initial scene through verified scene-entry ownership
- existing scene-change commands still perform representative `LoadSingle`, `LoadAdditive`, and `Unload` operations without payload changes
- loading presentation still works for representative transitions without scene-wide search or duplicate cleanup as accepted behavior
- the project-level loading observability contract remains available during representative transitions
- preserved generated scene event keys remain valid for representative open and close flows
- mixed-authority startup is diagnosable and unacceptable rather than silently tolerated
- direct play and live boot no longer represent different architectural truths

---

## Out of Scope and Handoff to Later Waves

The following work is explicitly deferred:

- full scope and service composition replacement beyond the live entry slice, which belongs to Wave B
- full command registration and dispatch cutover beyond the preserved scene-change slice, which belongs to Wave C
- full value, blackboard, var-registry, and fallback-removal work beyond transition observability continuity, which belongs to Wave D
- representative gameplay-system migration, which belongs to Wave E
- broad legacy deletion and final hardening, which belong to [06_WaveFLegacyRemovalAndHardeningSpec.md](06_WaveFLegacyRemovalAndHardeningSpec.md)

Wave A should leave those waves easier.
It must not claim to have completed them.

---

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V21-WA-01 | Confirm live startup enters through verified boot input. | The accepted live path must not require `ProjectLifetimeScope` or `GlobalLifetimeScope` auto-bootstrap authority. |
| TC-V21-WA-02 | Confirm representative initial scene entry is owned by the verified runtime path. | TitleScene or equivalent representative startup must be reached through the Wave A authority model. |
| TC-V21-WA-03 | Confirm preserved scene-change payloads still drive `LoadSingle`. | Existing `SceneChangeCommandData` authored for a representative single-load path must remain valid without payload shape changes. |
| TC-V21-WA-04 | Confirm preserved scene-change payloads still drive additive load and unload. | Representative additive-load and unload flows must continue to work through the migrated authority. |
| TC-V21-WA-05 | Confirm loading presentation works without legacy discovery repair. | Representative loading show, progress, and hide behavior must not depend on `FindObjectsByType`, duplicate cleanup, or fallback parent discovery in the accepted path. |
| TC-V21-WA-06 | Confirm representative loading observability and scene event contracts remain visible. | `IsLoading` continuity and generated scene open or close event keys must remain valid during representative transitions. |
| TC-V21-WA-07 | Confirm mixed authority is rejected. | A profile that activates both verified live boot and legacy auto-bootstrap must fail validation or boot with structured diagnostics. |
| TC-V21-WA-08 | Confirm direct play and live boot are not divergent truths. | Direct play may still be used, but it must consume compatible verified inputs and must not be the only passing path. |
| TC-V21-WA-09 | Confirm missing scene mapping fails deterministically. | A representative missing or invalid target scene must produce structured failure rather than runtime discovery fallback. |

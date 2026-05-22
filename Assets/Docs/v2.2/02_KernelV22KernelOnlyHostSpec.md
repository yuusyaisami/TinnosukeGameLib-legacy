# Kernel v2.2 Kernel-Only Host Specification

## Document Status

- Document ID: 02_KernelV22KernelOnlyHostSpec
- Status: Draft
- Role: defines the M1 kernel-only live host chain that replaces legacy live root authority, persistent-root ownership, and pre-shell scene-root authority on the accepted runtime path
- Depends on:
  - [00_KernelV22CompletionOverviewSpec.md](00_KernelV22CompletionOverviewSpec.md)
  - [01_KernelV22AuthorityAndServiceCensusSpec.md](01_KernelV22AuthorityAndServiceCensusSpec.md)
  - [../v2/05_BootManifestAndProfileSpec.md](../v2/05_BootManifestAndProfileSpec.md)
  - [../v2/07_ScopeGraphRuntimeSpec.md](../v2/07_ScopeGraphRuntimeSpec.md)
  - [../v2/09_CommandCatalogRuntimeSpec.md](../v2/09_CommandCatalogRuntimeSpec.md)
  - [../v2/10_ValueSchemaAndStoreSpec.md](../v2/10_ValueSchemaAndStoreSpec.md)
  - [../v2/15_TestAndValidationSpec.md](../v2/15_TestAndValidationSpec.md)
  - [../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md](../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md)
  - [../v2.1/01_WaveABootAndSceneEntryCutoverSpec.md](../v2.1/01_WaveABootAndSceneEntryCutoverSpec.md)
  - [05_KernelV22MilestoneOrderSpec.md](05_KernelV22MilestoneOrderSpec.md)

### Revision Note

This revision turns 02 from a short host-chain note into the full M1 contract.

M1 is intentionally narrower than the v2.1 Wave A boot and scene-entry slice.
v2.2 splits the work differently:

- M1 fixes live host authority
- M3 later migrates the boot and scene-flow service family that runs on top of that host

The goal of 02 is therefore not to complete SceneService and LoadingScreenService migration.
The goal is to make them downstream consumers of a kernel-owned host chain rather than hidden host owners.

---

## Ownership

This specification owns:

- live boot entry authority for M1
- persistent-root ownership for M1
- the handoff from verified boot to runtime shell and initial scene host state
- demotion rules for legacy root and scene-root authority before later family cutover begins
- direct-play convergence rules as a host-truth check rather than a completion substitute
- M1-specific mixed-authority diagnostics and fail-closed conditions

This specification does not own:

- full SceneService semantics, scene-transition behavior, or loading presentation semantics, which belong to later family cutover work
- command-host removal, which belongs to M2
- value-host removal, which belongs to M2
- representative gameplay family migration, which belongs to M4
- release-wide deletion and hardening, which belong to M5 and M6

---

## Purpose

The purpose of this document is to replace the last architectural question mark before family cutover begins: who owns the live runtime host chain.

The answer must be Kernel, end to end.

Core statements:

```text
The accepted live path must enter through verified boot input.
KernelRuntimeShell must own host truth before any scene-root participant becomes active.

Direct play may remain as a reference path.
It must converge on the same verified-input host semantics and must not be the only green path.

SceneService and LoadingScreenService may still exist during M1,
but they must consume the host chain rather than define it.
```

---

## Scope

This specification defines:

- live boot entry isolation
- persistent-root ownership isolation
- pre-shell scene-root demotion
- initial shell-to-scene host handoff
- direct-play convergence requirements for host truth
- mixed-authority diagnostics and acceptance gates for the above

---

## Non-Goals

This specification does not define:

- final SceneService migration semantics
- final LoadingScreenService migration semantics
- full scene-transition contract replacement
- full command or value host deletion
- representative gameplay proof beyond startup and host-truth convergence

M1 exists to make the host chain unambiguous.
It does not replace the later family and deletion milestones.

---

## Relationship to Other Specs

| Spec | Relationship to 02 |
| --- | --- |
| [00_KernelV22CompletionOverviewSpec.md](00_KernelV22CompletionOverviewSpec.md) | Defines the continuity contract, abolition target, and rule that release accepted path becomes kernel-only. |
| [01_KernelV22AuthorityAndServiceCensusSpec.md](01_KernelV22AuthorityAndServiceCensusSpec.md) | Defines the five-class vocabulary that 02 uses to classify host owners and downstream participants. |
| [03_KernelV22ServiceFamilyCutoverSpec.md](03_KernelV22ServiceFamilyCutoverSpec.md) | Consumes the host chain defined here before it migrates the boot and scene-flow family in M3. |
| [../v2/05_BootManifestAndProfileSpec.md](../v2/05_BootManifestAndProfileSpec.md) | Owns verified boot input and shell-creation semantics consumed by M1. |
| [../v2/07_ScopeGraphRuntimeSpec.md](../v2/07_ScopeGraphRuntimeSpec.md) | Owns scope truth that the host chain must expose before scene-root participation begins. |
| [../v2/09_CommandCatalogRuntimeSpec.md](../v2/09_CommandCatalogRuntimeSpec.md) | Supplies the command-session authority that later M2 work will attach to the host chain established here. |
| [../v2/10_ValueSchemaAndStoreSpec.md](../v2/10_ValueSchemaAndStoreSpec.md) | Supplies the value-session authority that later M2 work will attach to the host chain established here. |
| [../v2.1/01_WaveABootAndSceneEntryCutoverSpec.md](../v2.1/01_WaveABootAndSceneEntryCutoverSpec.md) | Provides the migration evidence and failure vocabulary that 02 narrows into the v2.2 host-only slice. |

02 consumes upstream semantics.
It must not reinterpret them to preserve legacy host authority.

---

## Current-State Host Inventory

This section records the current host-authority pressure.
It is evidence, not target policy.

| Observation | Evidence Type | M1 pressure |
| --- | --- | --- |
| ProjectLifetimeScope still creates a live root through RuntimeInitializeOnLoadMethod, search, Resources.Load, or fallback object creation. | Source | live boot can still begin outside the kernel-owned host chain |
| GlobalLifetimeScope still establishes a second persistent root through legacy auto-bootstrap sequencing. | Source | persistent-root ownership is still partially legacy |
| KernelLiveBootOrchestrator and KernelRuntimeShell exist, but they are not yet the only accepted live authority. | Source | M1 must make the kernel host chain the only accepted live host truth |
| AuthoringBridge already prepares verified inputs for direct play, but live boot can still diverge architecturally from that path. | Source | M1 must use direct play only as a convergence check and not as proof by itself |
| SceneLifetimeScope, CommandRunnerMB, and BlackboardMB still appear at representative scene roots and can still be misread as host owners. | Source | scene-root participants must become downstream only after kernel-owned host handoff |
| SceneService and LoadingScreenService still carry scene-flow continuity pressure, but they belong to the later boot and scene-flow family cutover. | Source | M1 must keep them downstream of the host chain rather than let them define host authority |

## Representative Anchors

- [../../GameLib/Script/Project/System/KernelLiveBoot/KernelLiveBootOrchestrator.cs](../../GameLib/Script/Project/System/KernelLiveBoot/KernelLiveBootOrchestrator.cs)
- [../../GameLib/Script/Kernel/Boot/KernelRuntimeShell.cs](../../GameLib/Script/Kernel/Boot/KernelRuntimeShell.cs)
- [../../Editor/KernelBoot/AuthoringBridge.cs](../../Editor/KernelBoot/AuthoringBridge.cs)
- [../../GameLib/Script/Project/LTS/ProjectLifetimeScope.cs](../../GameLib/Script/Project/LTS/ProjectLifetimeScope.cs)
- [../../GameLib/Script/Project/Global/LTS/GlobalLifetimeScope.cs](../../GameLib/Script/Project/Global/LTS/GlobalLifetimeScope.cs)
- [../../GameLib/Script/Project/Scene/LTS/SceneLifetimeScope.cs](../../GameLib/Script/Project/Scene/LTS/SceneLifetimeScope.cs)
- [../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs)
- [../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs](../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs)
- [../../GameLib/Script/Project/System/SceneFlow/SceneManager/Service/SceneService.cs](../../GameLib/Script/Project/System/SceneFlow/SceneManager/Service/SceneService.cs)
- [../../GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs](../../GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs)
- [../../Scenes/TitleScene.unity](../../Scenes/TitleScene.unity)
- [../../Scenes/GameScene.unity](../../Scenes/GameScene.unity)

---

## Preserved Contracts

M1 preserves only the host-facing continuity needed to keep later cutover credible.

| Contract surface | Current anchor | M1 requirement |
| --- | --- | --- |
| representative startup reachability | [../../Scenes/TitleScene.unity](../../Scenes/TitleScene.unity); [../../Scenes/GameScene.unity](../../Scenes/GameScene.unity) | representative live startup must still reach the initial playable scene after host ownership moves |
| direct-play reference comparability | [../../Editor/KernelBoot/AuthoringBridge.cs](../../Editor/KernelBoot/AuthoringBridge.cs) | direct play must remain a verified-input reference path that can be compared against the accepted live host chain |
| scene-root downstream participation boundary | [../../GameLib/Script/Project/Scene/LTS/SceneLifetimeScope.cs](../../GameLib/Script/Project/Scene/LTS/SceneLifetimeScope.cs); [../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs); [../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs](../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs) | representative scene-root participants may still exist after handoff, but they must not establish host truth before the shell-owned host chain is ready |

M1 does not preserve legacy live-root ownership.
It preserves only the continuity needed to keep later service-family migration honest.

---

## Owned Migration Goals

M1 must achieve all of the following:

- move accepted live boot entry to verified boot input and kernel-owned shell creation
- move persistent-root ownership away from legacy auto-bootstrap
- expose initial scene host state from KernelRuntimeShell before scene-root participants become authoritative
- converge live boot and direct play on the same verified-input host semantics
- demote legacy roots and scene-root participants from host ownership
- make mixed authority diagnosable and unacceptable

---

## Target Host Authority Model

### Required Live Host Chain

1. Live boot enters through KernelLiveBootOrchestrator.
2. Verified boot input is accepted before runtime shell creation.
3. KernelRuntimeShell becomes the first accepted runtime owner.
4. Persistent roots are created or bound only from verified input.
5. Project, platform, and global host roles at the boot boundary are resolved through explicit verified host identity and must not require concrete ProjectLifetimeScope, PlatformLifetimeScope, or GlobalLifetimeScope component types.
6. Initial scene host state is exposed from the shell-owned path before scene-root participants become active as downstream consumers.
7. Scope lifetime may later deepen through ScopeGraph-owned runtime state, but pre-shell host truth must not come from RuntimeLifetimeScope.
8. Direct play must reach equivalent host state through compatible verified-input semantics.
9. Downstream boot and scene-flow services may still be transitional, but they must consume this host chain rather than create it.

### Authority Replacement Rules

- ProjectLifetimeScope and GlobalLifetimeScope auto-bootstrap must stop being the accepted live host path by M1 completion.
- boot-layer project/platform/global host resolution must remain generic; verified live boot must not depend on concrete ProjectLifetimeScope, PlatformLifetimeScope, or GlobalLifetimeScope component requirements.
- accepted live host creation must not depend on scene search, fallback prefabs, Resources.Load repair, or surprise new GameObject repair.
- SceneLifetimeScope, CommandRunnerMB, and BlackboardMB may continue to exist in representative scenes during transition, but they may not become required pre-shell host owners.
- direct play may remain as a reference path, but it must not route through a different host truth than live boot.
- SceneService and LoadingScreenService may remain temporarily only as downstream consumers of the host chain until M3 migrates that family.

### Transitional Coexistence Rules

| Transitional condition | Allowed during M1 | Required rule |
| --- | --- | --- |
| Representative scenes still contain SceneLifetimeScope, CommandRunnerMB, or BlackboardMB | Yes | They may participate after host handoff, but they must not establish the accepted live host chain. |
| SceneService or LoadingScreenService still provide downstream scene-flow behavior | Yes | They must consume shell-owned host authority and must not create host truth themselves. |
| Legacy RuntimeInitializeOnLoadMethod boot hooks still exist in code | Temporarily | They must be diagnosable and must not be required for the accepted live path. |
| direct play still exists as a reference path | Yes | It must converge on compatible verified-input host semantics and must not be the only passing path. |
| accepted live host still relies on search, duplicate cleanup, or fallback root repair | No | M1 acceptance must fail because live host truth is still legacy. |

---

## M1 Subphases

### M1-0 Current-State Host Inventory

Objective:
freeze the host-authority map before cutover work starts.

Required outputs:

- representative host anchor inventory
- preserved host-facing contract table
- explicit list of live boot entry points, persistent-root owners, and pre-shell scene-root participants

Exit gate:
the current host chain is traceable from boot entry through representative initial scene reachability.

Forbidden shortcuts:

- assuming direct play already proves live host truth
- planning host cutover without writing down which components still establish pre-shell authority

### M1-1 Live Boot Authority Isolation

Objective:
make verified boot the only accepted live host entry path.

Required outputs:

- explicit live boot selection path through KernelLiveBootOrchestrator and verified boot input
- mixed-authority diagnostics for legacy auto-bootstrap participation
- removal or quarantine direction for legacy RuntimeInitializeOnLoadMethod host authority

Exit gate:
accepted live startup can begin through verified boot without depending on legacy root discovery.

Forbidden shortcuts:

- leaving legacy auto-bootstrap active as a silent fallback
- letting verified boot begin only after legacy roots have already established host authority

### M1-2 Persistent Root Demotion

Objective:
move persistent-root ownership under verified host input.

Required outputs:

- explicit persistent-root input or binding policy
- rejection path for search-based or fallback root repair
- demotion rules for ProjectLifetimeScope and GlobalLifetimeScope as host owners

Exit gate:
persistent roots required for representative startup are explicit verified inputs rather than discovered surprises.

Forbidden shortcuts:

- keeping Resources.Load, scene search, or new GameObject repair as accepted root creation behavior
- claiming demotion while accepted startup still succeeds only because legacy roots remain active

### M1-3 Runtime Shell to Scene Handoff

Objective:
ensure the runtime shell owns host truth before scene-root participants begin to consume it.

Required outputs:

- explicit shell-owned host state for representative initial scene entry
- boundary between pre-shell host ownership and downstream scene participation
- documented rule that SceneLifetimeScope, CommandRunnerMB, and BlackboardMB may participate only after handoff

Exit gate:
representative startup reaches the initial scene through kernel-owned host state rather than scene-root preemption.

Forbidden shortcuts:

- inferring host truth from already-loaded scene objects
- allowing scene-root components to decide whether the host chain exists before shell handoff is complete

### M1-4 Direct-Play Convergence and Mixed-Authority Rejection

Objective:
make direct play a convergence check instead of a separate architectural truth.

Required outputs:

- explicit comparability rule between live boot and AuthoringBridge-driven direct play
- diagnostics for mixed authority and divergent host semantics
- fail-closed path for profiles that activate both kernel-owned host truth and legacy host authority

Exit gate:
live boot and direct play no longer represent different host truths even if they remain different entry conveniences.

Forbidden shortcuts:

- treating direct play as sufficient proof that live boot migrated
- allowing a profile to silently accept both verified host ownership and legacy auto-bootstrap participation

### M1-5 Acceptance Gate

Objective:
prove that the host chain is materially complete enough for later family cutover.

Required outputs:

- executable verification plan for representative startup and host-truth convergence
- diagnostics coverage for missing boot input, mixed authority, and pre-shell scene-root takeover
- documentation updates that reflect the accepted host model and handoff boundary

Exit gate:
all M1 acceptance criteria and required test cases pass.

Forbidden shortcuts:

- accepting M1 because direct play works
- accepting M1 while host authority still depends on legacy scene-root existence to repair itself

---

## Non-Completion Rules

- direct-play reference remains a useful development path, but it does not prove the live host chain is migrated
- representative gameplay success without kernel-owned host truth does not complete v2.2
- runtime fallback repair through ancestor traversal, scene search, or registry lookup is a host-chain failure, not a convenience

---

## Forbidden Shortcuts

The following shortcuts are explicitly forbidden for M1:

- treating direct-play success as proof that the accepted live host path migrated
- keeping ProjectLifetimeScope or GlobalLifetimeScope auto-bootstrap as a silent live fallback
- allowing scene-root participants to establish host truth before KernelRuntimeShell handoff
- claiming host cutover by migrating SceneService or LoadingScreenService behavior while host ownership itself remains ambiguous
- preserving Resources.Load, scene search, duplicate cleanup, or surprise root repair as accepted host behavior

---

## Diagnostics and Failure Policy

v2 specification 11 owns the diagnostics substrate.
M1 defines the conditions that must become diagnostic-visible and acceptance-visible.

| Code | Failure condition | Required result |
| --- | --- | --- |
| V22-M1-HOST-001 | BootManifest, artifact set, or required profile input is missing for the accepted live path. | Live boot must be blocked before KernelRuntimeShell creation. |
| V22-M1-HOST-002 | Legacy RuntimeInitializeOnLoadMethod root creation participates in a profile that claims M1 host cutover. | Validation or boot must fail with a mixed-authority diagnostic instead of silently continuing. |
| V22-M1-HOST-003 | A required persistent root is created by search, Resources.Load fallback, or surprise new GameObject repair. | M1 host acceptance must fail because persistent roots are not verified host inputs. |
| V22-M1-HOST-004 | SceneLifetimeScope, CommandRunnerMB, or BlackboardMB becomes required before shell-owned host handoff is established. | Representative startup must fail as a pre-shell scene-root takeover instead of silently accepting scene-root authority. |
| V22-M1-HOST-005 | direct play and live boot route through divergent verified-input host semantics. | M1 must be treated as incomplete until both paths converge on the same host truth. |

---

## Acceptance Criteria

M1 is complete only when all of the following are true:

- representative live startup enters through verified boot input rather than legacy auto-bootstrap authority
- persistent roots required for representative startup are explicit verified host inputs rather than discovered repairs
- representative startup reaches the initial scene through shell-owned host handoff rather than scene-root preemption
- ProjectLifetimeScope and GlobalLifetimeScope no longer decide accepted live host truth
- SceneLifetimeScope, CommandRunnerMB, and BlackboardMB may still exist, but they are no longer required to establish host authority
- direct play and live boot no longer represent different host truths
- mixed authority is diagnosable and unacceptable rather than silently tolerated
- downstream boot and scene-flow services, if still transitional, are consuming the host chain rather than defining it

---

## Handoff to Later Milestones

The following work is explicitly deferred:

- command-host removal and value-host removal, which belong to M2
- SceneService and LoadingScreenService family migration, which belongs to M3
- representative gameplay/application family migration, which belongs to M4
- release deletion and hardening, which belong to M5 and M6

M1 should leave those milestones easier.
It must not claim to have completed them.

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V22-02-01 | Confirm the target host chain is explicit. | This file must name KernelLiveBootOrchestrator and KernelRuntimeShell in the live host chain. |
| TC-V22-02-02 | Confirm legacy root hosts are demoted explicitly. | This file must mention ProjectLifetimeScope and GlobalLifetimeScope as demoted host owners. |
| TC-V22-02-03 | Confirm scene-root participants are demoted from pre-shell host truth. | This file must mention SceneLifetimeScope, CommandRunnerMB, and BlackboardMB as downstream-only after handoff. |
| TC-V22-02-04 | Confirm direct play is treated as convergence evidence rather than live-host proof. | This file must mention AuthoringBridge and reject direct-play-only success as proof. |
| TC-V22-02-05 | Confirm SceneService and LoadingScreenService are deferred to later family cutover rather than misclassified as M1 host completion. | This file must mention SceneService and LoadingScreenService as downstream consumers left for later migration. |
| TC-V22-02-06 | Confirm M1 subphases are explicit. | This file must contain M1-1 through M1-5. |
| TC-V22-02-07 | Confirm mixed-authority and divergence failures are named. | This file must mention V22-M1-HOST-001 through V22-M1-HOST-005. |
| TC-V22-02-08 | Confirm acceptance criteria require shell-owned host handoff and live/direct convergence. | Acceptance Criteria must require shell-owned initial scene handoff and convergence between live boot and direct play. |
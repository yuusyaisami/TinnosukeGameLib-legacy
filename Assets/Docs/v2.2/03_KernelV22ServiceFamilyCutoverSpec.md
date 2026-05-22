# Kernel v2.2 Service Family Cutover Specification

## Document Status

- Document ID: 03_KernelV22ServiceFamilyCutoverSpec
- Status: Draft
- Role: defines the stable family bucket vocabulary and the M3 family cutover contract that later representative gameplay/application proof must consume after live-host and command/value host authority are already fixed
- Depends on:
  - [01_KernelV22AuthorityAndServiceCensusSpec.md](01_KernelV22AuthorityAndServiceCensusSpec.md)
  - [02_KernelV22KernelOnlyHostSpec.md](02_KernelV22KernelOnlyHostSpec.md)
  - [02_1_KernelV22CommandAndValueHostRemovalSpec.md](02_1_KernelV22CommandAndValueHostRemovalSpec.md)
  - [../v2/06_ServiceGraphRuntimeSpec.md](../v2/06_ServiceGraphRuntimeSpec.md)
  - [../v2/12_UnityAuthoringBridgeSpec.md](../v2/12_UnityAuthoringBridgeSpec.md)
  - [../v2/15_TestAndValidationSpec.md](../v2/15_TestAndValidationSpec.md)
  - [../v2.1/01_WaveABootAndSceneEntryCutoverSpec.md](../v2.1/01_WaveABootAndSceneEntryCutoverSpec.md)
  - [../v2.1/05_WaveERepresentativeGameplaySystemsCutoverSpec.md](../v2.1/05_WaveERepresentativeGameplaySystemsCutoverSpec.md)
- Provides foundation for:
  - [03_1_KernelV22RepresentativeGameplayAndApplicationCutoverSpec.md](03_1_KernelV22RepresentativeGameplayAndApplicationCutoverSpec.md)
  - [05_KernelV22MilestoneOrderSpec.md](05_KernelV22MilestoneOrderSpec.md)

### Revision Note

This revision expands 03 from a bucket list into the dedicated owner specification for M3 family cutover and the family boundary that M4 must consume.

M3 was previously not claimable because the repository had named families, but it did not yet have an owner contract that fixed:

- which families belong to M3 versus M4
- which preserved contracts M3 must keep stable while replacing runtime ownership
- which mixed-boundary families must split before they can claim stable ServiceGraph eligibility
- which proof anchors count as M3 family proof rather than later M4 gameplay/application proof

This document closes that gap.

---

## Ownership

This specification owns:

- the stable family-bucket vocabulary used for feature migration after M1 and M2
- the M3 cutover contract for Boot and Scene Flow plus first channel families
- the split rules that determine when a family may claim stable ServiceGraph eligibility
- the positive template rule for coarse services that must not be re-expanded into per-runtime service fragments
- the proof boundary between M3 family proof and M4 gameplay/application proof
- the diagnostics and acceptance rules that reject mixed family authority

This specification does not own:

- live-host ownership already defined by [02_KernelV22KernelOnlyHostSpec.md](02_KernelV22KernelOnlyHostSpec.md)
- command/value host ownership already defined by [02_1_KernelV22CommandAndValueHostRemovalSpec.md](02_1_KernelV22CommandAndValueHostRemovalSpec.md)
- target ServiceGraph semantics already owned by [../v2/06_ServiceGraphRuntimeSpec.md](../v2/06_ServiceGraphRuntimeSpec.md)
- direct-play verified-input policy already owned by [../v2/12_UnityAuthoringBridgeSpec.md](../v2/12_UnityAuthoringBridgeSpec.md)
- representative gameplay/application completion itself, which is owned by [03_1_KernelV22RepresentativeGameplayAndApplicationCutoverSpec.md](03_1_KernelV22RepresentativeGameplayAndApplicationCutoverSpec.md)
- release-wide deletion and hardening, which belong to [04_KernelV22LegacyDeletionAndHardeningSpec.md](04_KernelV22LegacyDeletionAndHardeningSpec.md)

03 owns family cutover boundaries.
It must not become a replacement for M1, M2, M4, or M5.

---

## Purpose

The purpose of this document is to replace ad hoc service-by-service migration with family-sized cutover that can be claimed and audited.

Core statements:

```text
Families may consume kernel-owned host, command, and value authority.
Families may not recreate those authorities inside family-local runtime glue.

M3 closes Boot and Scene Flow plus first channel families.
M4 closes representative gameplay/application families.

Gameplay/application success may not be used to close M3.
```

This document therefore focuses on ownership boundaries and claim gates, not on broad feature redesign.

---

## Scope

This specification defines:

- the three stable family buckets used for v2.2 cutover
- the M3 versus M4 milestone boundary across those buckets
- the preserved family-facing contracts that remain stable while runtime ownership changes
- the split-required family list and its gating rules
- the positive coarse-service templates that must be preserved as templates
- diagnostics and acceptance rules for family cutover claims

---

## Non-Goals

This specification does not define:

- a new host chain that bypasses M1
- a new command or value runtime that bypasses M2
- a blanket rewrite of all gameplay/application services in the repository
- a release-zero-authority deletion package
- a per-feature micro-service plan that re-expands the coarse service model

M3 may keep family-local runtime objects when they remain HubOwnedRuntimeObject.
It does not preserve mixed family authority.

---

## Relationship to Other Specs

| Spec | Relationship to 03 |
| --- | --- |
| [01_KernelV22AuthorityAndServiceCensusSpec.md](01_KernelV22AuthorityAndServiceCensusSpec.md) | Supplies the five-class vocabulary used here to classify family anchors. |
| [02_KernelV22KernelOnlyHostSpec.md](02_KernelV22KernelOnlyHostSpec.md) | Establishes the host chain that every family must consume instead of recreating. |
| [02_1_KernelV22CommandAndValueHostRemovalSpec.md](02_1_KernelV22CommandAndValueHostRemovalSpec.md) | Establishes the command/value session boundaries that every family must consume instead of hiding behind scene-facing hosts. |
| [04_KernelV22LegacyDeletionAndHardeningSpec.md](04_KernelV22LegacyDeletionAndHardeningSpec.md) | Uses the family boundaries defined here to decide what residue still counts as release-blocking. |
| [../v2/06_ServiceGraphRuntimeSpec.md](../v2/06_ServiceGraphRuntimeSpec.md) | Owns target coarse-service runtime semantics that family cutover must consume rather than reinterpret. |
| [../v2.1/01_WaveABootAndSceneEntryCutoverSpec.md](../v2.1/01_WaveABootAndSceneEntryCutoverSpec.md) | Supplies the preserved boot and scene-entry contracts that the Boot and Scene Flow family must continue to honor. |
| [../v2.1/05_WaveERepresentativeGameplaySystemsCutoverSpec.md](../v2.1/05_WaveERepresentativeGameplaySystemsCutoverSpec.md) | Supplies the representative gameplay/application boundary that remains M4 work and may not be used to close M3. |

03 consumes upstream authority boundaries.
It must not soften them for family convenience.

---

## Family Bucket Matrix

| Family bucket | Primary milestone | Representative anchors | Required target shape | Why it is grouped this way |
| --- | --- | --- | --- | --- |
| Boot and Scene Flow | M3 | KernelLiveBootOrchestrator, SceneFlowInstallerMB, SceneService, LoadingScreenService, SceneChangeExecutor | one kernel-consumed authority chain with declaration-only authoring split and no runtime discovery fallback | live entry, scene transition, and loading continuity form one authority chain |
| UI and Scene Channels | M3 | ModalStackChannelHubService, TooltipChannelHubService, ConversationChannelHubService, MeshChannelHubService, AnimationSpriteHubService, TraitListChannelHubService, TraitListChannelRuntime, GridObjectChannelRuntime | coarse KernelManagedFeatureService plus explicit HubOwnedRuntimeObject children, with split-required hubs isolated before stable claim | hubs and channel runtimes share the same coarse-service plus hub-owned-runtime-object split pressure |
| Gameplay and Application | M4 | GameStateMachineExecutors, StatusEffectService, map runtime, save/profile services | representative end-to-end consumers of M1, M2, and M3 outputs rather than owners of those slices | representative gameplay/application coordinators prove whether the earlier boundaries are actually consumed |

---

## Current-State Family Inventory

This section records the current family pressure.
It is cutover evidence, not target policy.

| Observation | Evidence type | Family pressure |
| --- | --- | --- |
| `SceneFlowInstallerMB` still installs `SceneService`, `LoadingScreenService`, and loading configuration from a scene-facing installer. | Source | Boot and Scene Flow still carries mixed authoring/runtime ownership pressure that M3 must demote without breaking the preserved scene-flow surface. |
| `LoadingScreenService` still contains duplicate cleanup and runtime parent discovery pressure. | Source | Boot and Scene Flow still contains split-required runtime-discovery behavior that cannot remain stable family authority. |
| `TooltipChannelHubService` mixes hub ownership, acquired-service resolution, and runtime-player coordination behind one hub surface. | Source | UI and Scene Channels still contains split-required mixed-boundary pressure. |
| `AnimationSpriteHubService` remains a scene-channel family anchor with mixed channel/runtime pressure. | Source | UI and Scene Channels still contains split-required mixed-boundary pressure. |
| `ModalStackChannelHubService` and `MeshChannelHubService` already look like coarse-service templates with explicit runtime state kept local. | Source | M3 must preserve these as positive templates instead of re-expanding them. |
| `GameStateMachineExecutors` and `StatusEffectService` remain the representative gameplay/application proof anchors. | Source | gameplay/application proof must stay separate from M3 family proof and close only in M4. |

## Representative Anchors

- [../../GameLib/Script/Project/System/SceneFlow/MB/SceneFlowInstallerMB.cs](../../GameLib/Script/Project/System/SceneFlow/MB/SceneFlowInstallerMB.cs)
- [../../GameLib/Script/Project/System/SceneFlow/SceneManager/Service/SceneService.cs](../../GameLib/Script/Project/System/SceneFlow/SceneManager/Service/SceneService.cs)
- [../../GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs](../../GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs)
- [../../GameLib/Script/Common/Commands/VNext/Executors/Scene/SceneChangeExecutor.cs](../../GameLib/Script/Common/Commands/VNext/Executors/Scene/SceneChangeExecutor.cs)
- [../../GameLib/Script/Project/UI/Core/ModalStackChannel/ModalStackChannelHubService.cs](../../GameLib/Script/Project/UI/Core/ModalStackChannel/ModalStackChannelHubService.cs)
- [../../GameLib/Script/Project/UI/Core/Tooltip/TooltipChannelHubService.cs](../../GameLib/Script/Project/UI/Core/Tooltip/TooltipChannelHubService.cs)
- [../../GameLib/Script/Project/UI/Core/Conversation/Channel/ConversationChannelHubService.cs](../../GameLib/Script/Project/UI/Core/Conversation/Channel/ConversationChannelHubService.cs)
- [../../GameLib/Script/Project/Scene/Channels/Mesh/MeshChannelHubService.cs](../../GameLib/Script/Project/Scene/Channels/Mesh/MeshChannelHubService.cs)
- [../../GameLib/Script/Project/Scene/Channels/SpriteAnimation/AnimationSpriteHubService.cs](../../GameLib/Script/Project/Scene/Channels/SpriteAnimation/AnimationSpriteHubService.cs)
- [../../GameLib/Script/Project/UI/TraitListChannel/Service/TraitListChannelHubService.cs](../../GameLib/Script/Project/UI/TraitListChannel/Service/TraitListChannelHubService.cs)
- [../../GameLib/Script/Project/UI/TraitListChannel/Service/TraitListChannelRuntime.cs](../../GameLib/Script/Project/UI/TraitListChannel/Service/TraitListChannelRuntime.cs)
- [../../GameLib/Script/Project/Scene/Channels/GridObjectChannel/GridObjectChannelRuntime.cs](../../GameLib/Script/Project/Scene/Channels/GridObjectChannel/GridObjectChannelRuntime.cs)
- [../../Game/Scripts/Flow/Commands/GameStateMachineExecutors.cs](../../Game/Scripts/Flow/Commands/GameStateMachineExecutors.cs)
- [../../GameLib/Script/Common/StatusEffect/Runtime/StatusEffectService.cs](../../GameLib/Script/Common/StatusEffect/Runtime/StatusEffectService.cs)

---

## Preserved Family Contracts

M3 preserves only the family-facing contracts required to move runtime authority without forcing broad content churn.

### Boot and Scene Flow Contracts

| Contract surface | Current anchor | M3 requirement |
| --- | --- | --- |
| `SceneChangeCommandData` payload meaning | [../../GameLib/Script/Common/Commands/VNext/Commands/Scene/SceneChangeCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/Scene/SceneChangeCommandData.cs) | existing scene-change payload meaning remains stable while the family consumes M1 and M2 authority instead of scene-facing installers |
| `ISceneService` load and unload contract | [../../GameLib/Script/Project/System/SceneFlow/SceneManager/Service/SceneService.cs](../../GameLib/Script/Project/System/SceneFlow/SceneManager/Service/SceneService.cs) | scene-change call surfaces remain stable while family ownership moves away from installer-driven runtime authority |
| `ILoadingScreenConfig` and `SceneFlowAuthoring` declaration surface | [../../GameLib/Script/Project/System/SceneFlow/MB/SceneFlowInstallerMB.cs](../../GameLib/Script/Project/System/SceneFlow/MB/SceneFlowInstallerMB.cs) | loading authoring stays declaration-compatible while runtime discovery and duplicate repair stop being accepted family authority |
| generated scene event keys and scene-loading observability | [../../GameLib/Script/Generated/EventKeys.g.cs](../../GameLib/Script/Generated/EventKeys.g.cs) | existing scene event identity and loading observability remain stable while family authority moves underneath them |

### UI and Scene Channel Contracts

| Contract surface | Current anchor | M3 requirement |
| --- | --- | --- |
| modal layer and default-root behavior | [../../GameLib/Script/Project/UI/Core/ModalStackChannel/ModalStackChannelHubService.cs](../../GameLib/Script/Project/UI/Core/ModalStackChannel/ModalStackChannelHubService.cs) | channel layer identity and modal-stack behavior remain stable while coarse service ownership becomes explicit |
| conversation tag and session lookup surface | [../../GameLib/Script/Project/UI/Core/Conversation/Channel/ConversationChannelHubService.cs](../../GameLib/Script/Project/UI/Core/Conversation/Channel/ConversationChannelHubService.cs) | session start, end, and lookup semantics remain stable while channel family authority consumes M1 and M2 boundaries |
| trait-list channel bind and refresh surface | [../../GameLib/Script/Project/UI/TraitListChannel/Service/TraitListChannelHubService.cs](../../GameLib/Script/Project/UI/TraitListChannel/Service/TraitListChannelHubService.cs) | tag-based binding and refresh semantics remain stable while runtime authority stops depending on scope repair or hidden host recreation |
| grid-object channel bind, choice, and refresh surface | [../../GameLib/Script/Project/Scene/Channels/GridObjectChannel/GridObjectChannelRuntime.cs](../../GameLib/Script/Project/Scene/Channels/GridObjectChannel/GridObjectChannelRuntime.cs) | grid-object channel behavior remains stable while family authority stops depending on hidden command/value host recreation |

Preserved does not mean frozen implementation.
It means stable family-facing contract while runtime authority changes.

---

## Family Rules

1. Every family must consume the host and command/value boundaries already established by M1 and M2.
2. No family may recreate live host, command session, or value session authority inside family-local runtime glue.
3. Every family must declare which anchors are KernelManagedFeatureService, which remain HubOwnedRuntimeObject, which remain AuthoringOnlyMonoBehaviour, and which are DeleteTarget.
4. Mixed-boundary families must split before they claim stable ServiceGraph eligibility.
5. M3 owns Boot and Scene Flow plus UI and Scene Channels. Gameplay and Application remains M4-only.
6. Positive coarse-service templates must not be re-expanded into per-runtime authority fragments merely to preserve legacy structure.

---

## Split-Required Families

| Family anchor | Why split is required | M3 rule |
| --- | --- | --- |
| `LoadingScreenService` | loading orchestration still carries runtime discovery, duplicate cleanup, and installer-coupled authority pressure | split or isolate the discovery and repair residue before claiming stable Boot and Scene Flow family authority |
| `TooltipChannelHubService` | one hub still mixes acquired-service resolution, player-runtime coordination, and channel ownership | split or isolate mixed runtime ownership before claiming stable channel-family authority |
| `AnimationSpriteHubService` | one channel hub still carries mixed channel/runtime ownership pressure | split or isolate mixed runtime ownership before claiming stable channel-family authority |

### Positive Coarse-Service Templates

The following anchors are positive templates and must not be re-expanded into per-runtime service fragments:

- `ModalStackChannelHubService`
- `MeshChannelHubService`

These templates show the intended shape:

- one coarse KernelManagedFeatureService authority surface
- explicit family-local runtime state that remains local rather than becoming a new global host
- no hidden recreation of host, command, or value authority

---

## M3 Subphases

### M3-0 Family Inventory Freeze

Objective:
freeze the family buckets, split-required anchors, and positive templates before M3 claims begin.

Required outputs:

- one stable family-bucket matrix
- one split-required family list
- one positive-template list that later work may reuse without re-expanding into micro-services

Exit gate:
M3 no longer reopens bucket ownership or split pressure while implementation moves.

### M3-1 Boot and Scene Flow Family Cutover

Objective:
move Boot and Scene Flow onto explicit family authority that consumes M1 and M2 rather than scene-facing installer truth.

Required outputs:

- explicit family ownership boundary for `SceneFlowInstallerMB`, `SceneService`, and `LoadingScreenService`
- preserved scene-change and loading authoring contracts
- rejection of runtime discovery, duplicate cleanup, and installer-driven repair as accepted family authority

Exit gate:
Boot and Scene Flow no longer depends on scene-facing installer truth or runtime discovery repair for accepted execution.

### M3-2 First Channel Family Cutover

Objective:
move the first UI and Scene Channels onto explicit coarse family authority.

Required outputs:

- explicit family ownership boundary for modal, tooltip, conversation, mesh, animation-sprite, trait-list, and grid-object channel anchors
- preserved tag, layer, bind, refresh, and session-facing contracts
- rejection of hidden family-local host, command, or value authority recreation

Exit gate:
the first channel families consume kernel-owned host, command, and value boundaries without recreating them.

### M3-3 Split-Required Family Enforcement

Objective:
make mixed-boundary families unclaimable until the split is explicit.

Required outputs:

- split or isolation plan for `LoadingScreenService`, `TooltipChannelHubService`, and `AnimationSpriteHubService`
- explicit rule that split-required families may not claim stable ServiceGraph eligibility early
- positive-template guidance that preserves `ModalStackChannelHubService` and `MeshChannelHubService` as coarse-service references

Exit gate:
split-required families are either split or explicitly blocked from stable claim.

### M3-4 Acceptance Gate

Objective:
prove that family cutover is real rather than a rename of legacy runtime glue.

Required outputs:

- representative family proof for Boot and Scene Flow
- representative family proof for the first channel families
- diagnostics that reject mixed family authority and proof borrowing from M4 gameplay/application success

Exit gate:
all M3 acceptance criteria and test cases pass.

---

## Gameplay/Application Deferral Boundary

Gameplay and Application remains deferred to [03_1_KernelV22RepresentativeGameplayAndApplicationCutoverSpec.md](03_1_KernelV22RepresentativeGameplayAndApplicationCutoverSpec.md).

M3 may prepare that work by freezing family boundaries, but it must not claim completion through:

- `GameStateMachineExecutors`
- `StatusEffectService`
- GameScene-visible success that still depends on earlier family ambiguity

Gameplay/application proof may only close M4 through [03_1_KernelV22RepresentativeGameplayAndApplicationCutoverSpec.md](03_1_KernelV22RepresentativeGameplayAndApplicationCutoverSpec.md).
It may not be used to close M3.

---

## Diagnostics and Failure Policy

v2 specification 11 owns the diagnostics substrate.
M3 defines the failure conditions that must become diagnostic-visible and acceptance-visible.

| Code | Failure condition | Required result |
| --- | --- | --- |
| V22-M3-FAMILY-001 | Boot and Scene Flow still depends on `SceneFlowInstallerMB` or `LoadingScreenService` runtime discovery or repair as accepted family authority. | M3 acceptance must fail instead of silently treating installer-driven flow as migrated. |
| V22-M3-FAMILY-002 | A first channel family recreates host, command, or value authority inside family-local runtime glue. | M3 acceptance must fail with mixed-family-authority diagnostics. |
| V22-M3-FAMILY-003 | A split-required family claims stable ServiceGraph eligibility before the split is explicit. | Validation or runtime must fail closed rather than silently accepting the mixed boundary. |
| V22-M3-FAMILY-004 | Positive coarse-service templates are re-expanded into per-runtime authority fragments. | M3 acceptance must fail because the coarse-service model regressed. |
| V22-M3-FAMILY-005 | Gameplay/application success is used to claim M3 without Boot and Scene Flow plus first channel family proof. | Claim review must reject the milestone because M4 proof is being borrowed to close M3. |

---

## Acceptance Criteria

M3 is complete only when all of the following are true:

- Boot and Scene Flow consumes the M1 and M2 boundaries instead of scene-facing installer authority
- first channel families consume the M1 and M2 boundaries instead of recreating them locally
- split-required families are split or explicitly blocked from stable claim
- `ModalStackChannelHubService` and `MeshChannelHubService` remain positive coarse-service templates rather than being re-expanded
- gameplay/application proof remains deferred to M4 and is not used to close M3
- mixed family authority is diagnosable and unacceptable rather than silently tolerated

---

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V22-03-01 | Confirm family cutover is organized into three stable buckets. | This file must contain Boot and Scene Flow, UI and Scene Channels, and Gameplay and Application. |
| TC-V22-03-02 | Confirm the bucket matrix fixes M3 versus M4 ownership. | This file must assign Boot and Scene Flow and UI and Scene Channels to M3, and Gameplay and Application to M4. |
| TC-V22-03-03 | Confirm major UI and channel anchors are included. | This file must mention ModalStackChannelHubService, TooltipChannelHubService, ConversationChannelHubService, MeshChannelHubService, AnimationSpriteHubService, TraitListChannelRuntime, and GridObjectChannelRuntime. |
| TC-V22-03-04 | Confirm Boot and Scene Flow preserved contracts are explicit. | This file must mention SceneChangeCommandData, ISceneService, ILoadingScreenConfig, and generated scene event keys. |
| TC-V22-03-05 | Confirm split-required mixed-boundary families are explicit. | This file must mention LoadingScreenService, TooltipChannelHubService, and AnimationSpriteHubService under split pressure. |
| TC-V22-03-06 | Confirm service-candidate templates are preserved as coarse services. | This file must mention ModalStackChannelHubService and MeshChannelHubService as positive templates. |
| TC-V22-03-07 | Confirm M3 subphases are explicit. | This file must contain M3-1 Boot and Scene Flow Family Cutover, M3-2 First Channel Family Cutover, and M3-4 Acceptance Gate. |
| TC-V22-03-08 | Confirm gameplay/application proof is deferred to M4. | This file must state that gameplay/application proof may not be used to close M3. |
| TC-V22-03-09 | Confirm M3 diagnostics are explicit. | This file must mention V22-M3-FAMILY-001 through V22-M3-FAMILY-005. |
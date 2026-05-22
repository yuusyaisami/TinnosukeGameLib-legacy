# Kernel v2.2 Representative Gameplay and Application Cutover Specification

## Document Status

- Document ID: 03_1_KernelV22RepresentativeGameplayAndApplicationCutoverSpec
- Status: Draft
- Role: defines the M4 cutover contract that moves representative gameplay and application families onto the kernel-owned host, command, value, and family boundaries already established by M1 through M3
- Depends on:
  - [00_KernelV22CompletionOverviewSpec.md](00_KernelV22CompletionOverviewSpec.md)
  - [01_KernelV22AuthorityAndServiceCensusSpec.md](01_KernelV22AuthorityAndServiceCensusSpec.md)
  - [02_KernelV22KernelOnlyHostSpec.md](02_KernelV22KernelOnlyHostSpec.md)
  - [02_1_KernelV22CommandAndValueHostRemovalSpec.md](02_1_KernelV22CommandAndValueHostRemovalSpec.md)
  - [03_KernelV22ServiceFamilyCutoverSpec.md](03_KernelV22ServiceFamilyCutoverSpec.md)
  - [../v2/06_ServiceGraphRuntimeSpec.md](../v2/06_ServiceGraphRuntimeSpec.md)
  - [../v2/10_1_ScalarRuntimeAndBindingSpec.md](../v2/10_1_ScalarRuntimeAndBindingSpec.md)
  - [../v2/11_DebugMapAndDiagnosticsSpec.md](../v2/11_DebugMapAndDiagnosticsSpec.md)
  - [../v2/15_TestAndValidationSpec.md](../v2/15_TestAndValidationSpec.md)
  - [../v2.1/05_WaveERepresentativeGameplaySystemsCutoverSpec.md](../v2.1/05_WaveERepresentativeGameplaySystemsCutoverSpec.md)
  - [05_KernelV22MilestoneOrderSpec.md](05_KernelV22MilestoneOrderSpec.md)
- Provides foundation for:
  - [04_KernelV22LegacyDeletionAndHardeningSpec.md](04_KernelV22LegacyDeletionAndHardeningSpec.md)
  - [05_KernelV22MilestoneOrderSpec.md](05_KernelV22MilestoneOrderSpec.md)

### Revision Note

This revision creates the dedicated M4 specification that was previously missing from the v2.2 package.

03 fixed the family buckets and the M3 boundary.
That was necessary, but it did not make M4 claimable.

Representative gameplay and application proof was still under-specified because the package had not yet fixed:

- the real representative GameScene proof anchors that M4 must exercise
- the preserved gameplay-facing contracts that M4 must keep stable while replacing authority
- the gameplay/application slices that must consume M1 through M3 boundaries
- the diagnostics and traceability rules that distinguish migrated authority from visible-success-only proof

This document closes that gap.

---

## Ownership

This specification owns:

- the M4 representative gameplay/application cutover contract
- the representative GameScene anchor policy for accepted gameplay/application proof
- the preserved gameplay/application-facing contracts that remain stable while runtime authority changes
- the representative gameplay/application slices for GameStateMachine flow, Conversation and Dialogue session flow, GridObject and Trait presentation flow, and StatusEffect runtime flow
- the subordinate dependency rules for TargetChannel and AreaChannel inside representative gameplay/application proof
- the diagnostics, traceability rules, and acceptance criteria that distinguish migrated gameplay/application consumption from gameplay-only success

This specification does not own:

- live-host ownership already defined by [02_KernelV22KernelOnlyHostSpec.md](02_KernelV22KernelOnlyHostSpec.md)
- command/value host ownership already defined by [02_1_KernelV22CommandAndValueHostRemovalSpec.md](02_1_KernelV22CommandAndValueHostRemovalSpec.md)
- the M3 family bucket and split rules already owned by [03_KernelV22ServiceFamilyCutoverSpec.md](03_KernelV22ServiceFamilyCutoverSpec.md)
- target ServiceGraph semantics already owned by [../v2/06_ServiceGraphRuntimeSpec.md](../v2/06_ServiceGraphRuntimeSpec.md)
- scalar semantics already owned by [../v2/10_1_ScalarRuntimeAndBindingSpec.md](../v2/10_1_ScalarRuntimeAndBindingSpec.md)
- final legacy gameplay-path deletion and hardening, which belong to [04_KernelV22LegacyDeletionAndHardeningSpec.md](04_KernelV22LegacyDeletionAndHardeningSpec.md)

03_1 owns representative gameplay/application consumption of migrated authority.
It must not become a replacement for M1, M2, M3, or M5.

---

## Purpose

The purpose of this document is to prove that player-visible representative gameplay and application families consume the migrated architecture rather than merely surviving beside it.

Core statements:

```text
Representative gameplay/application families must consume migrated authority.
Visible correctness is required but is not sufficient proof by itself.

If representative behavior still depends on compatibility traversal,
local blackboard merge, hidden value fallback, or mixed authority,
M4 is not complete even when the scene still looks correct.

M4 preserves narrow gameplay/application-facing contracts.
It does not preserve legacy architecture underneath them.
```

This document therefore focuses on representative end-to-end consumption of migrated authority, not on broad feature redesign.

---

## Scope

This specification defines:

- the representative GameScene anchor set used for accepted gameplay/application proof
- the current-state inventory for representative gameplay/application slices in that anchor set
- the preserved gameplay/application-facing contracts for those slices
- the cross-slice target authority model those slices must consume after M1 through M3
- diagnostics, traceability rules, and acceptance gates that reject gameplay-only success without architectural proof

---

## Non-Goals

This specification does not define:

- a full project-wide feature census for every gameplay feature in the repository
- a new host, command, or value model that bypasses M1 through M3
- a standalone redesign of TargetChannel or AreaChannel semantics
- scalar semantics redesign
- the final deletion of every compatibility adapter, helper shim, or scene-facing MonoBehaviour host

M4 may preserve representative gameplay/application-facing contracts.
It does not preserve legacy authority that happened to make them work.

---

## Relationship to Other Specs

| Spec | Relationship to 03_1 |
| --- | --- |
| [02_KernelV22KernelOnlyHostSpec.md](02_KernelV22KernelOnlyHostSpec.md) | Establishes the host chain that representative gameplay/application families must consume rather than bypass. |
| [02_1_KernelV22CommandAndValueHostRemovalSpec.md](02_1_KernelV22CommandAndValueHostRemovalSpec.md) | Establishes the command/value session boundaries that representative gameplay/application families must consume rather than recreate. |
| [03_KernelV22ServiceFamilyCutoverSpec.md](03_KernelV22ServiceFamilyCutoverSpec.md) | Fixes the family buckets, split rules, and M3 boundary that M4 must consume rather than reopen. |
| [04_KernelV22LegacyDeletionAndHardeningSpec.md](04_KernelV22LegacyDeletionAndHardeningSpec.md) | Uses representative gameplay/application proof from M4 to decide what residue still counts as release-blocking. |
| [../v2/10_1_ScalarRuntimeAndBindingSpec.md](../v2/10_1_ScalarRuntimeAndBindingSpec.md) | Owns scalar semantics that M4 may consume through StatusEffect but must not silently redefine. |
| [../v2.1/05_WaveERepresentativeGameplaySystemsCutoverSpec.md](../v2.1/05_WaveERepresentativeGameplaySystemsCutoverSpec.md) | Supplies the representative gameplay/application slices, preserved contracts, and diagnostics pressure that M4 narrows into the v2.2 completion slice. |

03_1 consumes earlier cutover boundaries.
It must not dilute them for gameplay convenience.

---

## Representative GameScene Anchor Set

Accepted M4 gameplay/application proof must run through the following real scene anchors:

- [../../Scenes/GameScene.unity](../../Scenes/GameScene.unity)
- [../../Scenes/GameScene/GameScene.unity](../../Scenes/GameScene/GameScene.unity)

[../../Scenes/TitleScene.unity](../../Scenes/TitleScene.unity) remains an upstream scene-entry reference anchor inherited from M1.
It is not the primary gameplay/application proof anchor for M4.

---

## Current-State Representative Family Inventory

This section records the current representative gameplay/application bundle.
It is cutover evidence, not target policy.

| Observation | Evidence type | M4 pressure |
| --- | --- | --- |
| `ChangeGameStateExecutor` can still rely on compatibility traversal when resolving `IGameStateMachineService`. | Source | representative gameplay flow still carries fallback pressure that must not remain accepted truth |
| `ConversationFlowExecutor`, `ConversationChannelHubService`, and dialogue runtime still coordinate player-visible session progression through scene-facing hosts and runtime glue. | Source | representative session flow must consume migrated command, scope, and value authority without silent repair |
| `GridObjectChannelRuntime` and `TraitListChannelRuntime` still carry local payload, helper-driven scope preparation, or presentation-time convenience behavior. | Source | representative presentation flow must consume migrated scope, command, and value boundaries without those helpers becoming architecture truth |
| `StatusEffectService` still represents a representative gameplay-state runtime slice that can drift back toward optional convenience resolution. | Source | visible effect behavior must consume migrated service, command, and value authority while keeping scalar ownership explicit |
| `TargetChannelHubService` and `AreaChannelHubService` remain dense subordinate dependencies in the representative scene bundle. | Source | subordinate dependencies must remain subordinate and must not become hidden authority owners for accepted representative behavior |
| `CommandExecutionTrace` and `DynamicRuntimeLogUtility` already provide traceable diagnostics surfaces. | Source | accepted gameplay/application proof must require traceable architecture evidence rather than gameplay appearance alone |

## Representative Anchors

- [../../Scenes/GameScene.unity](../../Scenes/GameScene.unity)
- [../../Scenes/GameScene/GameScene.unity](../../Scenes/GameScene/GameScene.unity)
- [../../Game/Scripts/Flow/Commands/GameStateMachineExecutors.cs](../../Game/Scripts/Flow/Commands/GameStateMachineExecutors.cs)
- [../../Game/Scripts/Flow/Commands/GameStateMachineCommandData.cs](../../Game/Scripts/Flow/Commands/GameStateMachineCommandData.cs)
- [../../GameLib/Script/Common/Commands/VNext/Executors/UI/ConversationExecutors.cs](../../GameLib/Script/Common/Commands/VNext/Executors/UI/ConversationExecutors.cs)
- [../../GameLib/Script/Common/Commands/VNext/Commands/UI/ConversationCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/UI/ConversationCommandData.cs)
- [../../GameLib/Script/Project/UI/Core/Conversation/Channel/ConversationChannelHubService.cs](../../GameLib/Script/Project/UI/Core/Conversation/Channel/ConversationChannelHubService.cs)
- [../../GameLib/Script/Project/UI/Core/Dialog/Channel/DialogChannelRuntime.cs](../../GameLib/Script/Project/UI/Core/Dialog/Channel/DialogChannelRuntime.cs)
- [../../GameLib/Script/Project/Scene/Channels/GridObjectChannel/GridObjectChannelRuntime.cs](../../GameLib/Script/Project/Scene/Channels/GridObjectChannel/GridObjectChannelRuntime.cs)
- [../../GameLib/Script/Project/Scene/Channels/GridObjectChannel/GridObjectChannelPayloadBuilder.cs](../../GameLib/Script/Project/Scene/Channels/GridObjectChannel/GridObjectChannelPayloadBuilder.cs)
- [../../GameLib/Script/Common/Commands/VNext/Commands/Channels/GridObjectChannelCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/Channels/GridObjectChannelCommandData.cs)
- [../../GameLib/Script/Project/UI/TraitListChannel/Service/TraitListChannelRuntime.cs](../../GameLib/Script/Project/UI/TraitListChannel/Service/TraitListChannelRuntime.cs)
- [../../GameLib/Script/Common/Commands/VNext/Commands/Trait/List/TraitListChannelCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/Trait/List/TraitListChannelCommandData.cs)
- [../../GameLib/Script/Common/StatusEffect/Runtime/StatusEffectService.cs](../../GameLib/Script/Common/StatusEffect/Runtime/StatusEffectService.cs)
- [../../GameLib/Script/Common/Commands/VNext/Commands/StatusEffect/StatusEffectCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/StatusEffect/StatusEffectCommandData.cs)
- [../../GameLib/Script/Project/Scene/Channels/Targeting/Core/TargetChannelHubService.cs](../../GameLib/Script/Project/Scene/Channels/Targeting/Core/TargetChannelHubService.cs)
- [../../GameLib/Script/Project/Scene/Channels/Area/AreaChannelHubService.cs](../../GameLib/Script/Project/Scene/Channels/Area/AreaChannelHubService.cs)
- [../../GameLib/Script/Common/Commands/VNext/Core/CommandExecutionTrace.cs](../../GameLib/Script/Common/Commands/VNext/Core/CommandExecutionTrace.cs)
- [../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicRuntimeLogUtility.cs](../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicRuntimeLogUtility.cs)

---

## Preserved Representative Contracts

M4 preserves narrow gameplay/application-facing continuity.
It does not preserve the legacy architecture that currently hosts those systems.

| Contract surface | Current anchor | M4 requirement |
| --- | --- | --- |
| `ChangeGameStateCommandData` state-command meaning | [../../Game/Scripts/Flow/Commands/GameStateMachineCommandData.cs](../../Game/Scripts/Flow/Commands/GameStateMachineCommandData.cs) | existing state identifiers, start or end command lists, initial-state settings, and high-level state-transition meaning remain consumable by content |
| `ConversationCommandData` and representative conversation-session meaning | [../../GameLib/Script/Common/Commands/VNext/Commands/UI/ConversationCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/UI/ConversationCommandData.cs) | existing conversation start, continue, end, node progression, hook execution, and dialogue-tag routing meaning remain stable to content |
| representative dialogue close and choice-display continuity | [../../GameLib/Script/Project/UI/Core/Dialog/Channel/DialogChannelRuntime.cs](../../GameLib/Script/Project/UI/Core/Dialog/Channel/DialogChannelRuntime.cs) | dialogue close behavior and representative choice-display surfaces remain visible and meaningful to content |
| `GridObjectChannelCommandData` list, choice, and payload meaning | [../../GameLib/Script/Common/Commands/VNext/Commands/Channels/GridObjectChannelCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/Channels/GridObjectChannelCommandData.cs) | existing bind, refresh, clear, choice, row or column identity, and visible item-payload meaning remain consumable by content |
| `TraitListChannelCommandData` bind, refresh, and presentation meaning | [../../GameLib/Script/Common/Commands/VNext/Commands/Trait/List/TraitListChannelCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/Trait/List/TraitListChannelCommandData.cs) | existing list binding, refresh, placement, duplicate handling, and player-visible presentation meaning remain consumable by content |
| `StatusEffectCommandData` visible effect behavior | [../../GameLib/Script/Common/Commands/VNext/Commands/StatusEffect/StatusEffectCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/StatusEffect/StatusEffectCommandData.cs) | existing apply, remove, use, global-state visibility, and player-visible effect lifecycle meaning remain valid to content |
| existing `DynamicValue` authoring and generated value-key continuity inside representative systems | [../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicValue.cs](../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicValue.cs) | representative gameplay/application systems may keep authored DynamicValue surfaces and generated ids, but accepted runtime behavior beneath them must consume migrated authority |

Preserved does not mean frozen implementation.
It means stable gameplay/application-facing contract while runtime authority changes.

---

## Owned Migration Goals

M4 must achieve all of the following:

- prove representative gameplay/application behavior through the real GameScene anchors rather than isolated subsystem readiness
- make representative gameplay/application families consume the host, command, value, and family boundaries already established by M1 through M3
- preserve narrow gameplay/application-facing contracts while replacing the legacy authority beneath them
- reject gameplay/application success that still depends on compatibility traversal, local blackboard merge, helper-driven scope repair, hidden value fallback, or mixed authority
- keep TargetChannel and AreaChannel explicitly subordinate to player-visible system ownership inside this milestone
- keep scalar semantics explicitly outside M4 ownership while still proving representative systems can consume that boundary safely
- make representative gameplay/application migration traceable through diagnostics rather than through visual success alone

---

## Cross-Slice Target Authority Model

### Required Consumer Boundary

1. Representative gameplay/application families consume the verified live-host, command, value, and family authority established by M1 through M3.
2. A gameplay/application MonoBehaviour, hub, runtime, or scene-local service may remain as an authoring host, runtime facade, or debug surface only if it no longer defines architecture truth by itself.
3. Compatibility traversal, helper-driven scope preparation, local blackboard merge, direct stable-key lookup, and hidden DynamicValue evaluation may remain only inside bounded compatibility and must not decide accepted outcomes.
4. Representative gameplay/application success must be attributable to migrated authority with traceable diagnostics provenance.

### Required GameScene Anchor Policy

1. Accepted representative gameplay/application proof must run through the real GameScene anchors defined above.
2. TitleScene remains an upstream scene-entry reference, not the primary gameplay/application proof anchor.
3. Direct play or editor-only setup success is insufficient unless the same migrated authority is proven through the representative GameScene bundle.

### Required GameStateMachine Slice

1. Representative GameStateMachine flow consumes migrated command, value, and scope truth.
2. Existing state-command authoring remains stable for content.
3. Compatibility traversal in `ChangeGameStateExecutor` is current-state evidence, not accepted runtime truth.
4. Accepted GameStateMachine behavior must resolve its required runtime boundary through explicit migrated authority rather than compatibility traversal deciding the outcome.
5. Failed state-command execution must remain visible and diagnosable rather than silently downgraded to a visual no-op.

### Required Conversation and Dialogue Slice

1. `ConversationExecutors`, `ConversationChannelHubService`, and `DialogChannelRuntime` form a representative player-visible session slice.
2. Existing conversation start, continue, end, node progression, hook execution, dialogue display, and dialogue close meaning remain stable for content.
3. Session creation, branch execution, choice progression, dialogue display, and dialogue close must consume migrated command and value authority.
4. Missing conversation hub, missing dialogue service, failed session end, or failed dialogue close must remain explicit failure, not silent repair.

### Required Grid and Trait Presentation Slice

1. `GridObjectChannelRuntime` and `TraitListChannelRuntime` form representative presentation-flow slices.
2. Existing bind, refresh, choice, payload, placement, and player-visible list presentation meaning remain stable for content.
3. Spawned scope use, item payload projection, and presentation-time command execution must consume migrated scope, command, and value authority.
4. Local blackboard merge behavior and helper-driven scope preparation are current-state evidence, not accepted architecture truth.
5. Accepted presentation behavior must not require local blackboard merge, hidden scope repair, or convenience value fallback to achieve visible correctness.

### Required StatusEffect Slice and Scalar Boundary

1. `StatusEffectService` forms a representative gameplay-state runtime slice.
2. Existing apply, remove, use, global-state visibility, and player-visible effect behavior remain stable for content.
3. Required runtime outcomes must consume migrated service, command, and value authority rather than optional convenience paths deciding accepted truth.
4. When StatusEffect consumes scalar boundaries, scalar semantics remain owned by [../v2/10_1_ScalarRuntimeAndBindingSpec.md](../v2/10_1_ScalarRuntimeAndBindingSpec.md).
5. M4 must prove explicit scalar-boundary consumption, not silently redefine scalar behavior under a gameplay heading.

### Required Subordinate Dependency Rules

1. `TargetChannelHubService` and `AreaChannelHubService` are subordinate dependencies of representative player-visible systems inside M4.
2. They may appear in dependency, diagnostics, or representative-anchor tables.
3. They must not become the main ownership target of M4.
4. If a representative gameplay/application system only succeeds because a subordinate dependency silently restores legacy authority, M4 acceptance must fail.

---

## M4 Subphases

### M4-0 Representative Bundle Inventory

Objective:
freeze the representative gameplay/application bundle before cutover claims are made.

Required outputs:

- representative GameScene anchor set
- representative gameplay/application slice inventory
- preserved representative-contract table

Exit gate:
the representative bundle is traceable from scene anchors to runtime files and preserved contracts.

### M4-1 GameStateMachine Slice

Objective:
prove that representative gameplay flow consumes migrated command and value authority.

Required outputs:

- GameStateMachine target authority rules
- compatibility-traversal inventory for state-service resolution
- diagnostics for failed or compatibility-only state transitions

Exit gate:
representative state transitions no longer require compatibility traversal as accepted truth.

### M4-2 Conversation and Dialogue Slice

Objective:
prove that representative player-visible session flow consumes migrated command and value authority.

Required outputs:

- conversation-session target authority rules
- dialogue-close and session-end failure rules
- preserved session and choice-display contract table

Exit gate:
conversation and dialogue progression no longer relies on silent missing-service repair or hidden legacy authority.

### M4-3 Grid and Trait Presentation Slice

Objective:
prove that representative presentation systems consume migrated scope, command, and value authority.

Required outputs:

- grid and trait presentation target rules
- local-blackboard merge and helper-driven scope-preparation inventory
- preserved payload and presentation contract table

Exit gate:
representative presentation remains correct without local blackboard or helper truth deciding accepted outcomes.

### M4-4 StatusEffect Slice and Scalar Boundary

Objective:
prove that representative gameplay-state runtime consumes migrated authority while keeping scalar ownership explicit.

Required outputs:

- StatusEffect target authority rules
- explicit scalar-boundary handoff
- diagnostics for required behavior that still depends on legacy convenience paths

Exit gate:
StatusEffect visible behavior is attributable to migrated command, value, and service authority while scalar semantics remain explicitly out of scope.

### M4-5 Mixed-Authority Diagnostics and Acceptance

Objective:
make representative gameplay/application migration falsifiable rather than impressionistic.

Required outputs:

- mixed-authority failure list
- diagnostics and traceability requirements
- acceptance matrix for representative gameplay/application proof

Exit gate:
gameplay/application-only success can be distinguished from representative migrated authority.

---

## Forbidden Shortcuts

The following shortcuts are explicitly forbidden for M4:

- claiming M4 completion because the scene still appears playable without proving migrated authority through the representative GameScene anchors
- treating compatibility traversal as an acceptable steady-state architecture for representative gameplay/application systems
- allowing local blackboard merge, helper-driven scope repair, or hidden value fallback to decide accepted presentation outcomes
- promoting TargetChannel or AreaChannel into the main ownership target simply because they are dense in the representative scene
- rewriting scalar semantics inside the StatusEffect slice instead of consuming the explicit scalar boundary
- treating direct play or isolated editor tooling as sufficient proof of representative gameplay/application migration

---

## Diagnostics and Failure Policy

v2 specification 11 owns the diagnostics substrate.
M4 defines the representative gameplay/application failures that must become diagnostic-visible and acceptance-visible.

| Code | Failure condition | Required result |
| --- | --- | --- |
| V22-M4-ANCHOR-001 | Claimed gameplay/application proof does not run through the representative GameScene anchor set. | Acceptance must fail because representative live gameplay/application was not actually exercised. |
| V22-M4-GSM-001 | GameStateMachine required behavior still depends on compatibility traversal or other nearest-ancestor fallback. | Validation or acceptance must reject the path as unmigrated gameplay-flow authority. |
| V22-M4-CONV-001 | Conversation start, continue, or end only succeeds through silent missing-service repair or hidden legacy resolution. | Acceptance must fail with explicit representative-session diagnostics. |
| V22-M4-CONV-002 | Dialogue close or session end silently fails while visible flow continues. | Acceptance must fail because visible continuity masked architectural failure. |
| V22-M4-GRID-001 | GridObject presentation still requires local blackboard merge or other non-migrated value truth for accepted behavior. | Acceptance must fail with explicit presentation-slice diagnostics. |
| V22-M4-TRAIT-001 | TraitList presentation still depends on helper-driven scope repair or hidden value fallback as accepted truth. | Acceptance must fail because the representative presentation slice remains mixed-authority. |
| V22-M4-STATUS-001 | StatusEffect required visible behavior still depends on legacy blackboard truth or optional convenience resolution as accepted authority. | Acceptance must fail with explicit gameplay-state diagnostics. |
| V22-M4-TARGET-001 | TargetChannel or AreaChannel acts as a hidden authority owner that restores legacy behavior for a representative player-visible system. | The dependent representative slice must fail acceptance until authority is explicit again. |
| V22-M4-SCALAR-001 | M4 silently redefines scalar semantics while describing representative gameplay/application behavior. | Review or acceptance must reject the change as scope drift. |
| V22-M4-MIXED-001 | Representative gameplay/application still depends on both migrated and legacy authority for accepted outcomes. | Acceptance must fail with mixed-authority diagnostics. |
| V22-M4-PROOF-001 | Gameplay/application appears correct but no `CommandExecutionTrace`, dynamic-runtime diagnostics, or equivalent traceable evidence proves migrated authority. | Acceptance must fail because gameplay/application-only success is insufficient proof. |

---

## Acceptance Criteria

M4 is complete only when all of the following are true:

- representative gameplay/application proof runs through the real GameScene anchor set rather than isolated or direct-play-only harnesses
- GameStateMachine transitions consume migrated command and value authority without compatibility traversal deciding accepted outcomes
- conversation and dialogue session flow consumes migrated command and value authority and fails explicitly when required boundaries are missing
- representative GridObject and Trait presentation consumes migrated scope, command, and value authority without local blackboard or helper truth deciding accepted outcomes
- StatusEffect visible runtime behavior consumes migrated service, command, and value authority while keeping scalar ownership explicit and external to M4
- TargetChannel and AreaChannel remain subordinate dependencies and do not become hidden architecture owners for representative player-visible behavior
- mixed migrated and legacy authority remains diagnosable and unacceptable
- M4 proof includes traceable diagnostics evidence rather than gameplay/application appearance alone

---

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V22-031-01 | Confirm M4 uses the real GameScene anchors as representative proof. | This file must contain Scenes/GameScene.unity and Scenes/GameScene/GameScene.unity and state that TitleScene is not the primary proof anchor. |
| TC-V22-031-02 | Confirm GameStateMachine is a mandatory M4 slice. | This file must mention GameStateMachineExecutors, ChangeGameStateCommandData, and compatibility traversal rejection. |
| TC-V22-031-03 | Confirm Conversation and Dialogue are mandatory M4 slices. | This file must mention ConversationExecutors, ConversationCommandData, ConversationChannelHubService, and DialogChannelRuntime. |
| TC-V22-031-04 | Confirm GridObject and Trait presentation are mandatory M4 slices. | This file must mention GridObjectChannelRuntime, GridObjectChannelPayloadBuilder, TraitListChannelRuntime, and TraitListChannelCommandData. |
| TC-V22-031-05 | Confirm StatusEffect and the scalar boundary are explicit. | This file must mention StatusEffectService, StatusEffectCommandData, and 10_1_ScalarRuntimeAndBindingSpec.md. |
| TC-V22-031-06 | Confirm subordinate dependencies remain subordinate. | This file must mention TargetChannelHubService and AreaChannelHubService and state that they are not the main ownership target of M4. |
| TC-V22-031-07 | Confirm diagnostics and traceability are mandatory. | This file must mention CommandExecutionTrace, DynamicRuntimeLogUtility, V22-M4-ANCHOR-001, and V22-M4-PROOF-001. |
| TC-V22-031-08 | Confirm gameplay/application appearance alone cannot close M4. | This file must state that visual correctness or direct-play-only success is insufficient proof. |
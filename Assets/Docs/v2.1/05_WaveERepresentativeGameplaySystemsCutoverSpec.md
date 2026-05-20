# Wave E Representative Gameplay Systems Cutover Specification

## Document Status

- Document ID: 05_WaveERepresentativeGameplaySystemsCutoverSpec
- Status: Draft
- Role: defines the Wave E migration contract that moves representative gameplay-facing systems in the live GameScene bundle onto the verified boot, scope, service, command, and value authority established by Wave A through Wave D while preserving narrow gameplay-facing command, DynamicValue, and generated-value identity continuity
- Depends on:
  - [00_KernelV21MigrationOverviewSpec.md](00_KernelV21MigrationOverviewSpec.md)
  - [01_WaveABootAndSceneEntryCutoverSpec.md](01_WaveABootAndSceneEntryCutoverSpec.md)
  - [02_WaveBScopeAndServiceCompositionCutoverSpec.md](02_WaveBScopeAndServiceCompositionCutoverSpec.md)
  - [03_WaveCCommandDispatchCutoverSpec.md](03_WaveCCommandDispatchCutoverSpec.md)
  - [04_WaveDValueBlackboardAndVarCutoverSpec.md](04_WaveDValueBlackboardAndVarCutoverSpec.md)
  - [../v2/01_KernelIRSpec.md](../v2/01_KernelIRSpec.md)
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
  - [06_WaveFLegacyRemovalAndHardeningSpec.md](06_WaveFLegacyRemovalAndHardeningSpec.md)

### Revision Note

This revision creates the fifth detailed v2.1 wave specification.

Wave A moves the live game onto a verified live-entry path.
Wave B moves runtime scope and service composition toward verified scope and service boundaries.
Wave C moves command dispatch authority toward verified command identity and catalog truth.
Wave D moves generic value, blackboard, and DynamicValue runtime authority toward verified value and evaluation truth.

Wave E exists because the playable game can still appear correct while representative gameplay systems remain coupled to legacy resolver traversal, scene-local service hosts, local blackboard merge behavior, convenience scope repair, or hidden mixed authority.

The purpose of this wave is not to perform an exhaustive full-game feature census.
Its purpose is to prove that representative live gameplay systems in the GameScene bundle consume the migrated architecture rather than merely surviving beside it.

TargetChannel and AreaChannel appear heavily in the current scene bundle, but they are treated here as subordinate dependencies of player-visible systems rather than as the main ownership target of Wave E.

Scalar runtime semantics remain owned by [../v2/10_1_ScalarRuntimeAndBindingSpec.md](../v2/10_1_ScalarRuntimeAndBindingSpec.md).
Wave E may consume that boundary through representative systems such as StatusEffect, but it must not silently re-own scalar semantics.

---

## Ownership

This specification owns:

- the representative GameScene gameplay-bundle inventory that proves where live gameplay still depends on mixed architecture
- the preserved gameplay-facing contracts that must survive the cutover without freezing legacy runtime wiring in place
- the cross-slice target authority model that representative gameplay systems must consume after Waves A through D
- the representative gameplay slices for GameStateMachine flow, Conversation and Dialogue session flow, GridObject and Trait presentation flow, and StatusEffect runtime flow
- the subordinate dependency rules for TargetChannel and AreaChannel within the representative gameplay slice
- the diagnostics, subphases, and acceptance criteria that distinguish migrated gameplay consumption from gameplay-only success

This specification does not own:

- boot-entry semantics already owned by v2 and cut over by Wave A
- generic scope and service composition semantics already cut over by Wave B
- command identity and dispatch semantics already cut over by Wave C
- generic value, blackboard, and DynamicValue runtime semantics already cut over by Wave D
- scalar runtime semantics owned by [../v2/10_1_ScalarRuntimeAndBindingSpec.md](../v2/10_1_ScalarRuntimeAndBindingSpec.md)
- a full project-wide migration specification for every gameplay feature in the repository
- final legacy gameplay-path deletion and hardening work that belongs to Wave F

Wave E owns representative gameplay consumption of migrated authority.
It must not become a duplicate of Waves A through D, a hidden targeting spec, or a substitute for Wave F cleanup.

---

## Purpose

Wave E defines how the running game stops proving migration only by isolated subsystem readiness and instead proves migration through representative player-visible systems.

Core statements:

```text
Representative gameplay systems must consume migrated architecture.
Visible gameplay success is required but is not sufficient proof by itself.

If a representative system still depends on legacy resolver traversal,
scene-local repair, blackboard fallback, or mixed command or value truth,
Wave E is not complete even when the scene still looks correct.

Wave E preserves narrow gameplay-facing contracts.
It does not preserve legacy architecture underneath them.
```

This wave therefore focuses on representative end-to-end consumption of migrated authority, not on broad feature redesign.

---

## Scope

Wave E defines:

- the representative live gameplay scene-anchor set used as migration proof
- the current-state inventory for representative gameplay systems in that anchor set
- the preserved gameplay-facing contracts for those representative systems
- the cross-slice target authority model those systems must consume
- representative gameplay slices for GameStateMachine, Conversation and Dialogue, GridObject and Trait presentation, and StatusEffect runtime
- subordinate dependency rules for TargetChannel and AreaChannel in the Wave E slice
- diagnostics and acceptance gates that reject gameplay-only success without architectural proof

---

## Non-Goals

Wave E does not define:

- a full game-wide feature census or one specification row for every feature in the repository
- a standalone redesign of TargetChannel or AreaChannel semantics
- scalar runtime or binding semantics redesign
- a rewrite of every existing gameplay command payload or inspector surface
- final deletion of every compatibility adapter, legacy MonoBehaviour host, or legacy helper

Wave E may preserve representative gameplay-facing contracts.
It does not preserve the old architecture that happened to make them work.

---

## Relationship to Other Specs

| Spec | Relationship to Wave E |
| --- | --- |
| [00_KernelV21MigrationOverviewSpec.md](00_KernelV21MigrationOverviewSpec.md) | Defines the preservation floor, destructive allowance, migration-wave split, and non-completion rules that Wave E must obey. |
| [01_WaveABootAndSceneEntryCutoverSpec.md](01_WaveABootAndSceneEntryCutoverSpec.md) | Provides the verified live-entry and scene-transition baseline that representative gameplay systems must consume rather than bypass. |
| [02_WaveBScopeAndServiceCompositionCutoverSpec.md](02_WaveBScopeAndServiceCompositionCutoverSpec.md) | Provides the scope and coarse service boundaries that representative gameplay systems must consume rather than rediscover. |
| [03_WaveCCommandDispatchCutoverSpec.md](03_WaveCCommandDispatchCutoverSpec.md) | Provides migrated command identity and runner authority that representative gameplay command flows must consume. |
| [04_WaveDValueBlackboardAndVarCutoverSpec.md](04_WaveDValueBlackboardAndVarCutoverSpec.md) | Provides migrated generic value and DynamicValue runtime authority that representative gameplay systems must consume. |
| [../v2/05_BootManifestAndProfileSpec.md](../v2/05_BootManifestAndProfileSpec.md) | Owns the verified boot chain that still constrains how representative gameplay scenes may become active. |
| [../v2/06_ServiceGraphRuntimeSpec.md](../v2/06_ServiceGraphRuntimeSpec.md) | Owns service-runtime meaning. Wave E may consume services but must not recreate service truth through gameplay convenience paths. |
| [../v2/07_ScopeGraphRuntimeSpec.md](../v2/07_ScopeGraphRuntimeSpec.md) | Owns scope truth. Wave E may consume explicit scope boundaries but must not restore hierarchy-derived scope truth through gameplay helpers. |
| [../v2/08_LifecyclePlanSpec.md](../v2/08_LifecyclePlanSpec.md) | Owns lifecycle semantics that representative gameplay systems must consume rather than reconstruct from MonoBehaviour timing or implicit callbacks. |
| [../v2/09_CommandCatalogRuntimeSpec.md](../v2/09_CommandCatalogRuntimeSpec.md) | Owns command semantics and runner responsibility that representative gameplay flows must consume. |
| [../v2/10_ValueSchemaAndStoreSpec.md](../v2/10_ValueSchemaAndStoreSpec.md) | Owns generic value semantics. Wave E only defines how representative systems must consume migrated value truth. |
| [../v2/10_1_ScalarRuntimeAndBindingSpec.md](../v2/10_1_ScalarRuntimeAndBindingSpec.md) | Owns scalar specialization consumed by representative systems such as StatusEffect. Wave E must not rewrite it. |
| [../v2/10_2_DynamicValueEvaluationSpec.md](../v2/10_2_DynamicValueEvaluationSpec.md) | Owns DynamicValue evaluation semantics that representative gameplay systems must consume through explicit runtime authority. |
| [../v2/11_DebugMapAndDiagnosticsSpec.md](../v2/11_DebugMapAndDiagnosticsSpec.md) | Owns the diagnostics substrate. Wave E defines the representative gameplay failures that must become diagnostic-visible. |
| [../v2/12_UnityAuthoringBridgeSpec.md](../v2/12_UnityAuthoringBridgeSpec.md) | Owns authoring normalization boundaries that representative gameplay systems must consume rather than bypass with runtime shortcuts. |
| [../v2/13_LegacyCompatBoundarySpec.md](../v2/13_LegacyCompatBoundarySpec.md) | Owns quarantine rules for any compatibility paths that remain during representative gameplay migration. |
| [../v2/14_PerformanceBudgetAndRuntimeRulesSpec.md](../v2/14_PerformanceBudgetAndRuntimeRulesSpec.md) | Owns hot-path and runtime-budget rules. Wave E only defines the gameplay-consumption boundary that those rules must constrain. |
| [../v2/15_TestAndValidationSpec.md](../v2/15_TestAndValidationSpec.md) | Owns executable proof expectations. Wave E must prove architectural migration, not only visible gameplay success. |
| [../v2/16_ImplementationMilestoneOrderSpec.md](../v2/16_ImplementationMilestoneOrderSpec.md) | Confirms that representative gameplay proof happens after the core architecture slices already exist. |
| [../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md](../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md) | Prevents representative gameplay migration code from collapsing gameplay hosts, kernel runtime, and legacy compatibility into one blob. |

Wave E consumes target semantics defined elsewhere.
It must not dilute them for gameplay convenience.

---

## Representative Scene Anchor Set

Wave E uses the following live scene anchors as representative gameplay proof:

- [../../Scenes/GameScene.unity](../../Scenes/GameScene.unity)
- [../../Scenes/GameScene/GameScene.unity](../../Scenes/GameScene/GameScene.unity)

These scene anchors contain the representative gameplay bundle used by this document, including GameStateMachine, Conversation, GridObject, TraitList, StatusEffect, and subordinate targeting-area infrastructure.

[../../Scenes/TitleScene.unity](../../Scenes/TitleScene.unity) remains an upstream scene-entry reference anchor inherited from Wave A.
It is not the primary representative gameplay proof anchor for Wave E.

---

## Current-State Representative Bundle Inventory

This section records the current representative gameplay bundle.
It is migration evidence, not target policy.

### Observation Traceability

| Observation | Evidence Type | Migration Pressure |
| --- | --- | --- |
| Representative GameScene anchors contain `GameStateMachineMB`, multiple `TraitListChannelHubMB`, `ConversationChannelHubMB`, `GridObjectChannelHubMB`, `StatusEffectMB`, and subordinate `AreaChannelHubMB` anchors. | Source | Wave E can and must use real live scene anchors instead of hypothetical gameplay proof |
| `GameStateMachineMB` registers scene-facing state-command settings and `GameStateMachineService`, while `GameStateMachineService` executes state command lists through `ICommandRunner` with a local `VarStore`. | Source | representative gameplay flow already consumes command and value boundaries and must stop depending on legacy fallback around them |
| `ChangeGameStateExecutor` still resolves `IGameStateMachineService` by walking nearest `Scene`, `Field`, or `Project` scope ancestors if the origin scope does not resolve it directly. | Source | current gameplay flow still carries nearest-ancestor compatibility behavior that must not remain accepted truth |
| `ConversationFlowExecutor` resolves target scope, conversation hub, flow preset, dialogue service, and branch commands to drive player-visible session progression. | Source | representative session flow must consume migrated command, scope, and value authority without silent missing-service repair |
| `ConversationChannelHubService` rebuilds per-scope definitions from `ConversationChannelHubMB` and enforces one active session at a time inside that scope. | Source | scene-facing MonoBehaviour hosts may remain, but they must not remain hidden architecture owners |
| `GridObjectChannelRuntime` drives bind, refresh, choice, spawned scope, and visual update flow through `SimpleDynamicContext`, runtime payload building, and spawned-instance coordination. | Source | representative presentation flow still mixes migrated command or value consumption with convenience runtime behavior that must be bounded |
| `GridObjectChannelPayloadBuilder` still merges payload into local blackboard when available before producing command vars. | Source | representative presentation proof must stop depending on local blackboard merge as accepted value truth |
| `TraitListChannelRuntime` drives bind, refresh, spawned-instance placement, and queued re-entry behavior through `SimpleDynamicContext`, helper-based scope preparation, and runtime visual management. | Source | representative UI list flow must consume migrated command and value boundaries without helper-driven scope or value truth becoming architecture |
| `StatusEffectMB` installs `StatusEffectService` and debug behavior, and `StatusEffectService` lazily resolves command, rich-text, mutation, event, and blackboard dependencies while owning visible effect runtime behavior. | Source | representative gameplay-state runtime must consume migrated command, value, and explicit scalar boundaries rather than optional legacy convenience paths |
| `TargetChannelHubService` and `AreaChannelHubService` are dense GameScene dependencies, but they act as subordinate infrastructure for player-visible systems rather than as the player-visible system by themselves. | Source | Wave E must keep player-visible ownership centered on representative gameplay systems rather than inflating into a targeting-only wave |
| `CommandExecutionTrace` and `DynamicRuntimeLogUtility` already provide runtime-origin and dynamic-context diagnostics that can distinguish migrated authority from visible-success-only proof. | Source | Wave E acceptance must require traceable architecture evidence instead of visual success alone |

### Representative Anchors

- [../../Scenes/GameScene.unity](../../Scenes/GameScene.unity)
- [../../Scenes/GameScene/GameScene.unity](../../Scenes/GameScene/GameScene.unity)
- [../../Game/Scripts/Flow/GameStateMachineMB.cs](../../Game/Scripts/Flow/GameStateMachineMB.cs)
- [../../Game/Scripts/Flow/GameStateMachineService.cs](../../Game/Scripts/Flow/GameStateMachineService.cs)
- [../../Game/Scripts/Flow/Commands/GameStateMachineExecutors.cs](../../Game/Scripts/Flow/Commands/GameStateMachineExecutors.cs)
- [../../Game/Scripts/Flow/Commands/GameStateMachineCommandData.cs](../../Game/Scripts/Flow/Commands/GameStateMachineCommandData.cs)
- [../../GameLib/Script/Common/Commands/VNext/Executors/UI/ConversationExecutors.cs](../../GameLib/Script/Common/Commands/VNext/Executors/UI/ConversationExecutors.cs)
- [../../GameLib/Script/Common/Commands/VNext/Commands/UI/ConversationCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/UI/ConversationCommandData.cs)
- [../../GameLib/Script/Project/UI/Core/Conversation/MB/ConversationChannelHubMB.cs](../../GameLib/Script/Project/UI/Core/Conversation/MB/ConversationChannelHubMB.cs)
- [../../GameLib/Script/Project/UI/Core/Conversation/Channel/ConversationChannelHubService.cs](../../GameLib/Script/Project/UI/Core/Conversation/Channel/ConversationChannelHubService.cs)
- [../../GameLib/Script/Project/UI/Core/Dialog/Channel/DialogChannelRuntime.cs](../../GameLib/Script/Project/UI/Core/Dialog/Channel/DialogChannelRuntime.cs)
- [../../GameLib/Script/Project/Scene/Channels/GridObjectChannel/GridObjectChannelHubMB.cs](../../GameLib/Script/Project/Scene/Channels/GridObjectChannel/GridObjectChannelHubMB.cs)
- [../../GameLib/Script/Project/Scene/Channels/GridObjectChannel/GridObjectChannelRuntime.cs](../../GameLib/Script/Project/Scene/Channels/GridObjectChannel/GridObjectChannelRuntime.cs)
- [../../GameLib/Script/Project/Scene/Channels/GridObjectChannel/GridObjectChannelPayloadBuilder.cs](../../GameLib/Script/Project/Scene/Channels/GridObjectChannel/GridObjectChannelPayloadBuilder.cs)
- [../../GameLib/Script/Common/Commands/VNext/Commands/Channels/GridObjectChannelCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/Channels/GridObjectChannelCommandData.cs)
- [../../GameLib/Script/Project/UI/TraitListChannel/MB/TraitListChannelHubMB.cs](../../GameLib/Script/Project/UI/TraitListChannel/MB/TraitListChannelHubMB.cs)
- [../../GameLib/Script/Project/UI/TraitListChannel/Service/TraitListChannelRuntime.cs](../../GameLib/Script/Project/UI/TraitListChannel/Service/TraitListChannelRuntime.cs)
- [../../GameLib/Script/Project/UI/TraitListChannel/Service/TraitListChannelRuntimeDebugProbeMB.cs](../../GameLib/Script/Project/UI/TraitListChannel/Service/TraitListChannelRuntimeDebugProbeMB.cs)
- [../../GameLib/Script/Common/Commands/VNext/Commands/Trait/List/TraitListChannelCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/Trait/List/TraitListChannelCommandData.cs)
- [../../GameLib/Script/Common/StatusEffect/MB/StatusEffectMB.cs](../../GameLib/Script/Common/StatusEffect/MB/StatusEffectMB.cs)
- [../../GameLib/Script/Common/StatusEffect/Runtime/StatusEffectService.cs](../../GameLib/Script/Common/StatusEffect/Runtime/StatusEffectService.cs)
- [../../GameLib/Script/Common/Commands/VNext/Commands/StatusEffect/StatusEffectCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/StatusEffect/StatusEffectCommandData.cs)
- [../../GameLib/Script/Project/Scene/Channels/Targeting/Core/TargetChannelHubService.cs](../../GameLib/Script/Project/Scene/Channels/Targeting/Core/TargetChannelHubService.cs)
- [../../GameLib/Script/Project/Scene/Channels/Area/AreaChannelHubService.cs](../../GameLib/Script/Project/Scene/Channels/Area/AreaChannelHubService.cs)
- [../../GameLib/Script/Common/Commands/VNext/Core/CommandExecutionTrace.cs](../../GameLib/Script/Common/Commands/VNext/Core/CommandExecutionTrace.cs)
- [../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicRuntimeLogUtility.cs](../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicRuntimeLogUtility.cs)

---

## Preserved Gameplay Contracts

Wave E preserves narrow gameplay-facing continuity.
It does not preserve the legacy architecture that currently hosts those systems.

| Contract Surface | Current Anchor | Wave E Requirement |
| --- | --- | --- |
| `ChangeGameStateCommandData` and `GameStateMachineMB` state-command authoring | [../../Game/Scripts/Flow/Commands/GameStateMachineCommandData.cs](../../Game/Scripts/Flow/Commands/GameStateMachineCommandData.cs), [../../Game/Scripts/Flow/GameStateMachineMB.cs](../../Game/Scripts/Flow/GameStateMachineMB.cs) | Existing state identifiers, start-command lists, end-command lists, initial-state settings, and high-level state-transition meaning must remain consumable by existing content. |
| `ConversationCommandData` and representative conversation-session behavior | [../../GameLib/Script/Common/Commands/VNext/Commands/UI/ConversationCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/UI/ConversationCommandData.cs), [../../GameLib/Script/Project/UI/Core/Conversation/Channel/ConversationChannelHubService.cs](../../GameLib/Script/Project/UI/Core/Conversation/Channel/ConversationChannelHubService.cs) | Existing conversation start, continue, end, node progression, hook execution, and dialogue-tag routing meaning must remain stable to content. |
| Representative dialogue close and choice-display continuity | [../../GameLib/Script/Project/UI/Core/Dialog/Channel/DialogChannelRuntime.cs](../../GameLib/Script/Project/UI/Core/Dialog/Channel/DialogChannelRuntime.cs), [../../GameLib/Script/Project/Scene/Channels/GridObjectChannel/GridObjectChannelPayloadBuilder.cs](../../GameLib/Script/Project/Scene/Channels/GridObjectChannel/GridObjectChannelPayloadBuilder.cs) | Dialogue close behavior and representative choice display surfaces such as `GameLib.UI.DialogueChannel.Choice.DisplayName` must remain visible and meaningful to content. |
| `GridObjectChannelCommandData` list, choice, and payload meaning | [../../GameLib/Script/Common/Commands/VNext/Commands/Channels/GridObjectChannelCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/Channels/GridObjectChannelCommandData.cs), [../../GameLib/Script/Project/Scene/Channels/GridObjectChannel/GridObjectChannelRuntime.cs](../../GameLib/Script/Project/Scene/Channels/GridObjectChannel/GridObjectChannelRuntime.cs) | Existing bind, refresh, clear, choice, row or column identity, and visible item-payload meaning must remain consumable by content. |
| `TraitListChannelCommandData` bind, refresh, and presentation meaning | [../../GameLib/Script/Common/Commands/VNext/Commands/Trait/List/TraitListChannelCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/Trait/List/TraitListChannelCommandData.cs), [../../GameLib/Script/Project/UI/TraitListChannel/Service/TraitListChannelRuntime.cs](../../GameLib/Script/Project/UI/TraitListChannel/Service/TraitListChannelRuntime.cs) | Existing list binding, refresh, placement, duplicate handling, and player-visible presentation meaning must remain consumable by content. |
| `StatusEffectCommandData` visible effect behavior | [../../GameLib/Script/Common/Commands/VNext/Commands/StatusEffect/StatusEffectCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/StatusEffect/StatusEffectCommandData.cs), [../../GameLib/Script/Common/StatusEffect/Runtime/StatusEffectService.cs](../../GameLib/Script/Common/StatusEffect/Runtime/StatusEffectService.cs) | Existing apply, remove, use, global-state visibility, and player-visible effect lifecycle meaning must remain valid to content. |
| Existing `DynamicValue` authoring and generated value-key continuity inside representative gameplay systems | [../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicValue.cs](../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicValue.cs), [../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs) | Representative gameplay systems may keep their authored DynamicValue surfaces and generated ids, but accepted runtime behavior beneath them must consume migrated authority. |

Preserved does not mean frozen implementation.
It means stable gameplay-facing contract.

---

## Owned Migration Goals

Wave E must achieve all of the following:

- prove representative live gameplay through real GameScene anchors rather than isolated subsystem readiness
- make representative gameplay systems consume migrated scope, service, command, and value authority rather than recreate those boundaries locally
- preserve representative gameplay-facing contracts while replacing the legacy authority beneath them
- reject gameplay success that still depends on resolver traversal, local blackboard merge, scene-local self-bootstrap, or mixed migrated and legacy truth
- keep TargetChannel and AreaChannel explicitly subordinate to player-visible system ownership inside this wave
- keep scalar semantics explicitly outside Wave E ownership while still proving representative systems can consume that boundary safely
- make representative gameplay migration traceable through diagnostics rather than through visual success alone

---

## Cross-Slice Target Authority Model

Wave E target authority is defined by the following required model.

### Required Consumer Boundary

1. Representative gameplay systems consume the verified live-entry, scope, command, and generic value authority established by Waves A through D.
2. A gameplay MonoBehaviour, hub, runtime, or scene-local service may remain as an authoring host, runtime facade, or debug surface only if it no longer defines architecture truth by itself.
3. Resolver traversal, convenience scope preparation, local blackboard merge, direct stable-key lookup, and hidden DynamicValue evaluation may remain only inside bounded compatibility and must not decide accepted outcomes.
4. Representative gameplay success must be attributable to migrated authority with traceable diagnostics provenance.

### Required Scene Anchor Policy

1. The representative gameplay proof path must run through the real GameScene anchor set defined above.
2. TitleScene remains an upstream scene-entry reference, not the primary gameplay proof anchor.
3. Direct play or editor-only setup success is insufficient unless the same migrated authority is proven through the representative GameScene bundle.

### Required GameStateMachine Slice

1. `GameStateMachineService` consumes migrated `ICommandRunner`, migrated value context, and migrated scope truth.
2. Existing state-command authoring remains stable for content.
3. The nearest `Scene`, `Field`, or `Project` fallback path in `ChangeGameStateExecutor` is current-state evidence, not accepted runtime truth.
4. Accepted GameStateMachine behavior must resolve its required runtime boundary through explicit migrated authority rather than compatibility traversal deciding the outcome.
5. Failed state-command execution must remain visible and diagnosable rather than silently downgraded to a visual no-op.

### Required Conversation and Dialogue Slice

1. `ConversationFlowExecutor`, `ConversationChannelHubService`, and `DialogueChannelRuntime` form a representative player-visible session slice.
2. Existing conversation start, continue, end, node progression, hook execution, and dialogue-tag meaning remain stable for content.
3. Session creation, branch execution, choice progression, dialogue display, and dialogue close must consume migrated command and value authority.
4. Missing conversation hub, missing dialogue service, failed session end, or failed dialogue close must remain explicit failure, not silent repair.
5. `ConversationChannelHubMB` may remain a scene-facing authoring host, but it must not remain the hidden source of architectural truth.

### Required Grid and Trait Presentation Slice

1. `GridObjectChannelRuntime` and `TraitListChannelRuntime` form representative presentation-flow slices.
2. Existing bind, refresh, choice, payload, placement, and player-visible list presentation meaning remain stable for content.
3. Spawned scope use, item payload projection, and presentation-time command execution must consume migrated scope, command, and value authority.
4. `GridObjectChannelPayloadBuilder` local-blackboard merge behavior and helper-driven scope preparation are current-state evidence, not accepted architecture truth.
5. Accepted presentation behavior must not require local blackboard merge, hidden scope repair, or convenience value fallback to achieve visible correctness.

### Required StatusEffect Slice and Scalar Boundary

1. `StatusEffectService` forms a representative gameplay-state runtime slice.
2. Existing apply, remove, use, global-state visibility, and player-visible effect behavior remain stable for content.
3. Required runtime outcomes must consume migrated service, command, and value authority rather than optional lazy convenience paths deciding accepted truth.
4. When StatusEffect consumes scalar boundaries, the scalar semantics themselves remain owned by [../v2/10_1_ScalarRuntimeAndBindingSpec.md](../v2/10_1_ScalarRuntimeAndBindingSpec.md).
5. Wave E must prove explicit boundary consumption, not silently redefine scalar behavior under a gameplay heading.

### Required Subordinate Dependency Rules

1. `TargetChannelHubService` and `AreaChannelHubService` are subordinate dependencies of representative player-visible systems inside Wave E.
2. They may appear in dependency, diagnostics, or representative-anchor tables.
3. They must not become the main ownership target of Wave E.
4. If a representative gameplay system only succeeds because a subordinate dependency silently restores legacy authority, Wave E acceptance must fail.

### Transitional Coexistence Rules

| Transitional Condition | Allowed During Wave E | Required Rule |
| --- | --- | --- |
| Representative scene anchors still contain MonoBehaviour hosts such as `GameStateMachineMB`, `ConversationChannelHubMB`, `GridObjectChannelHubMB`, `TraitListChannelHubMB`, or `StatusEffectMB` | Yes | They may remain as authoring or debug hosts, but accepted authority must come from migrated A-D outputs. |
| Compatibility traversal helpers still exist in code | Temporarily | They may remain as bounded compatibility only; accepted Wave E proof must not depend on them deciding required outcomes. |
| Presentation runtimes still allocate local temporary vars for command execution | Yes | Local execution-state containers may remain, but accepted value truth must not be repaired by blackboard fallback or runtime key repair. |
| TargetChannel or AreaChannel remains dense in the representative scene | Yes | They remain subordinate dependencies and must not be used to avoid documenting the player-visible system ownership. |
| Gameplay appears correct but no diagnostics or traceability prove migrated authority | No | Accepted Wave E proof must fail because gameplay-only success is insufficient. |

---

## Wave E Subphases

### WE-0 Representative Bundle Inventory

Objective:
freeze the representative live gameplay bundle before cutover claims are made.

Required outputs:

- representative GameScene anchor set
- representative gameplay-system inventory
- preserved gameplay-contract table

Exit gate:
the representative live bundle is traceable from scene anchors to runtime files and preserved contracts.

Forbidden shortcuts:

- choosing representative systems only by folder name or preference
- proving gameplay through editor-only harnesses while ignoring live scene anchors

### WE-1 Preserved Contracts and Scene Anchors

Objective:
state exactly which player-visible contracts must remain stable while authority changes underneath them.

Required outputs:

- preserved contract table for representative systems
- scene-anchor policy for GameScene proof
- explicit exclusion of gameplay-only success as sufficient completion proof

Exit gate:
the preserved gameplay surface is documented without freezing the legacy implementation.

Forbidden shortcuts:

- treating every current runtime behavior as preserved simply because players can currently see it
- making TitleScene or direct-play setup the only proof path for gameplay migration

### WE-2 GameStateMachine Flow Slice

Objective:
prove that representative gameplay flow consumes migrated command and value authority.

Required outputs:

- GameStateMachine target authority rules
- current-state fallback inventory for state-service resolution
- diagnostics for failed or compatibility-only state transitions

Exit gate:
representative state transitions no longer require nearest-ancestor fallback as accepted truth.

Forbidden shortcuts:

- accepting nearest `Scene`, `Field`, or `Project` fallback as the hidden steady-state architecture
- swallowing failed state commands because the scene still appears playable

### WE-3 Conversation and Dialogue Slice

Objective:
prove that representative player-visible session flow consumes migrated command and value authority.

Required outputs:

- conversation-session target authority rules
- dialogue-close and session-end failure rules
- preserved session and choice-display contract table

Exit gate:
conversation and dialogue progression no longer rely on silent missing-service repair or hidden legacy authority.

Forbidden shortcuts:

- keeping session or dialogue close failure silent because the user can still continue manually
- treating missing conversation-hub or dialogue resolution as non-fatal for the accepted path

### WE-4 Grid and Trait Presentation Slice

Objective:
prove that representative presentation systems consume migrated scope, command, and value authority.

Required outputs:

- grid and trait presentation target rules
- local-blackboard merge and helper-driven scope-preparation inventory
- preserved payload and presentation contract table

Exit gate:
representative presentation remains correct without legacy blackboard or helper truth deciding accepted outcomes.

Forbidden shortcuts:

- accepting local blackboard merge as the hidden accepted value authority
- treating helper-based scope repair as harmless because the visuals still look correct

### WE-5 StatusEffect Slice and Scalar Boundary

Objective:
prove that representative gameplay-state runtime consumes migrated authority while keeping scalar ownership explicit.

Required outputs:

- StatusEffect target authority rules
- explicit scalar boundary handoff
- diagnostics for required behavior that still depends on legacy convenience paths

Exit gate:
StatusEffect visible behavior is attributable to migrated command, value, and service authority while scalar semantics remain explicitly out of scope.

Forbidden shortcuts:

- silently redefining scalar rules inside the StatusEffect section
- relying on legacy blackboard or optional service resolution as accepted truth for required visible behavior

### WE-6 Mixed-Authority Diagnostics and Acceptance

Objective:
make representative gameplay migration falsifiable rather than impressionistic.

Required outputs:

- mixed-authority failure list
- diagnostics and traceability requirements
- acceptance matrix for representative gameplay proof

Exit gate:
gameplay-only success can be distinguished from representative migrated authority.

Forbidden shortcuts:

- accepting green gameplay while architectural regressions remain invisible
- substituting screenshots, manual feel, or direct-play-only success for representative migrated proof

---

## Forbidden Shortcuts

The following shortcuts are explicitly forbidden for Wave E:

- claiming Wave E completion because the game still appears playable without proving migrated authority through representative scene anchors
- treating nearest-scope resolver fallback as an acceptable steady-state architecture for representative gameplay systems
- allowing local blackboard merge, convenience scope repair, or hidden value fallback to decide accepted presentation outcomes
- promoting TargetChannel or AreaChannel into the main ownership target simply because they are dense in the current scene
- rewriting scalar semantics inside the StatusEffect slice instead of consuming the explicit scalar boundary
- treating direct play or isolated editor tooling as sufficient proof of representative gameplay migration

---

## Diagnostics and Failure Policy

v2 specification 11 owns the diagnostics substrate.
Wave E defines the representative gameplay migration failures that must become diagnostic-visible.

| Code | Failure Condition | Required Result |
| --- | --- | --- |
| V21-WE-ANCHOR-001 | Claimed gameplay proof does not run through the representative GameScene anchor set. | Acceptance must fail because representative live gameplay was not actually exercised. |
| V21-WE-GSM-001 | GameStateMachine required behavior still depends on nearest-ancestor service fallback or other compatibility traversal. | Validation or acceptance must reject the path as unmigrated gameplay flow authority. |
| V21-WE-CONV-001 | Conversation start, continue, or end only succeeds through legacy resolution or silent missing-service repair. | Acceptance must fail with explicit representative-session diagnostics. |
| V21-WE-CONV-002 | Dialogue close or session end silently fails while gameplay continues visually. | Acceptance must fail because visible continuity masked architectural failure. |
| V21-WE-GRID-001 | GridObject presentation still requires local blackboard merge or other non-migrated value truth for accepted behavior. | Acceptance must fail with explicit presentation-slice diagnostics. |
| V21-WE-TRAIT-001 | TraitList presentation still depends on helper-driven scope repair or hidden value fallback as accepted truth. | Acceptance must fail because the representative presentation slice remains mixed-authority. |
| V21-WE-STATUS-001 | StatusEffect required visible behavior still depends on legacy blackboard truth or optional convenience resolution as accepted authority. | Acceptance must fail with explicit gameplay-state diagnostics. |
| V21-WE-TARGET-001 | TargetChannel or AreaChannel acts as a hidden authority owner that restores legacy behavior for a representative player-visible system. | The dependent representative slice must fail acceptance until authority is explicit again. |
| V21-WE-SCALAR-001 | Wave E silently redefines scalar semantics while describing representative gameplay. | Review or acceptance must reject the change as scope drift. |
| V21-WE-MIXED-001 | Representative gameplay still depends on both migrated and legacy authority for accepted outcomes. | Acceptance must fail with mixed-authority diagnostics. |
| V21-WE-PROOF-001 | Gameplay appears correct but no `CommandExecutionTrace`, dynamic-runtime diagnostics, or equivalent traceable evidence proves migrated authority. | Acceptance must fail because gameplay-only success is insufficient proof. |

---

## Acceptance Criteria

Wave E is complete only when all of the following are true:

- representative live gameplay proof runs through the real GameScene anchor set rather than through isolated or direct-play-only harnesses
- GameStateMachine transitions consume migrated command and value authority without compatibility traversal deciding accepted outcomes
- conversation and dialogue session flow consumes migrated command and value authority and fails explicitly when required boundaries are missing
- representative GridObject and Trait presentation consumes migrated scope, command, and value authority without legacy blackboard or helper truth deciding accepted outcomes
- StatusEffect visible runtime behavior consumes migrated service, command, and value authority while keeping scalar ownership explicit and external to Wave E
- TargetChannel and AreaChannel remain subordinate dependencies and do not become hidden architecture owners for representative player-visible behavior
- mixed migrated and legacy authority remains diagnosable and unacceptable
- Wave E proof includes traceable diagnostics evidence rather than gameplay appearance alone

---

## Out of Scope and Handoff to [06_WaveFLegacyRemovalAndHardeningSpec.md](06_WaveFLegacyRemovalAndHardeningSpec.md)

The following work is explicitly deferred:

- final deletion of representative gameplay compatibility adapters, helper shims, and legacy scene-local hosts that are no longer needed after migrated proof exists
- broad repository cleanup and hardening once representative gameplay migration proof is complete
- standalone targeting-system or scalar-system redesign outside the representative gameplay-consumption slice

That deferred work belongs to [06_WaveFLegacyRemovalAndHardeningSpec.md](06_WaveFLegacyRemovalAndHardeningSpec.md) or to the owning target specifications.
Wave E must make it possible, but it must not claim to have completed it.

---

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V21-WE-01 | Confirm representative gameplay proof uses the real GameScene anchors. | Acceptance evidence must reference the representative GameScene anchor set rather than only direct play or isolated tooling. |
| TC-V21-WE-02 | Confirm representative GameStateMachine flow consumes migrated command authority. | State transitions must succeed without nearest-ancestor fallback deciding accepted service truth. |
| TC-V21-WE-03 | Confirm representative conversation and dialogue flow consumes migrated authority. | Start, progression, choice, and end behavior must fail explicitly when migrated boundaries are missing. |
| TC-V21-WE-04 | Confirm representative GridObject presentation consumes migrated scope, command, and value authority. | Visible list or choice behavior must not depend on accepted local blackboard merge or other legacy value truth. |
| TC-V21-WE-05 | Confirm representative TraitList presentation consumes migrated authority. | Bind, refresh, and player-visible list updates must not require helper-driven scope repair as accepted truth. |
| TC-V21-WE-06 | Confirm representative StatusEffect runtime consumes migrated service and value authority. | Player-visible effect behavior must remain stable without silently re-owning scalar semantics inside Wave E. |
| TC-V21-WE-07 | Confirm subordinate TargetChannel and AreaChannel dependencies do not become the hidden authority owner. | If a representative slice only succeeds through subordinate dependency fallback, acceptance must fail. |
| TC-V21-WE-08 | Confirm mixed migrated and legacy authority is rejected for representative gameplay. | Representative gameplay must fail acceptance when both paths remain required for accepted outcomes. |
| TC-V21-WE-09 | Confirm diagnostics evidence is required in addition to visible gameplay success. | Acceptance must require command-trace or dynamic-runtime traceability, not visual success alone. |
| TC-V21-WE-10 | Confirm Wave E does not silently absorb scalar semantics or standalone targeting ownership. | Review and acceptance must reject scope drift into scalar or targeting redesign. |

# Kernel v2.1 Baseline Ledger

## Document Status

- Document ID: KernelV21BaselineLedger
- Status: Draft
- Role: joined migration baseline ledger for the V21-M0 baseline freeze
- Depends on:
  - [../00_KernelV21MigrationOverviewSpec.md](../00_KernelV21MigrationOverviewSpec.md)
  - [../01_WaveABootAndSceneEntryCutoverSpec.md](../01_WaveABootAndSceneEntryCutoverSpec.md)
  - [../02_WaveBScopeAndServiceCompositionCutoverSpec.md](../02_WaveBScopeAndServiceCompositionCutoverSpec.md)
  - [../03_WaveCCommandDispatchCutoverSpec.md](../03_WaveCCommandDispatchCutoverSpec.md)
  - [../04_WaveDValueBlackboardAndVarCutoverSpec.md](../04_WaveDValueBlackboardAndVarCutoverSpec.md)
  - [../05_WaveERepresentativeGameplaySystemsCutoverSpec.md](../05_WaveERepresentativeGameplaySystemsCutoverSpec.md)
  - [../06_WaveFLegacyRemovalAndHardeningSpec.md](../06_WaveFLegacyRemovalAndHardeningSpec.md)
  - [../07_KernelV21MigrationMilestoneOrderSpec.md](../07_KernelV21MigrationMilestoneOrderSpec.md)
  - [../../v2/05_BootManifestAndProfileSpec.md](../../v2/05_BootManifestAndProfileSpec.md)
  - [../../v2/06_ServiceGraphRuntimeSpec.md](../../v2/06_ServiceGraphRuntimeSpec.md)
  - [../../v2/07_ScopeGraphRuntimeSpec.md](../../v2/07_ScopeGraphRuntimeSpec.md)
  - [../../v2/09_CommandCatalogRuntimeSpec.md](../../v2/09_CommandCatalogRuntimeSpec.md)
  - [../../v2/10_ValueSchemaAndStoreSpec.md](../../v2/10_ValueSchemaAndStoreSpec.md)
  - [../../v2/13_LegacyCompatBoundarySpec.md](../../v2/13_LegacyCompatBoundarySpec.md)
  - [../../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md](../../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md)
- Provides foundation for:
  - [../01_WaveABootAndSceneEntryCutoverSpec.md](../01_WaveABootAndSceneEntryCutoverSpec.md)
  - [../02_WaveBScopeAndServiceCompositionCutoverSpec.md](../02_WaveBScopeAndServiceCompositionCutoverSpec.md)
  - [../03_WaveCCommandDispatchCutoverSpec.md](../03_WaveCCommandDispatchCutoverSpec.md)
  - [../04_WaveDValueBlackboardAndVarCutoverSpec.md](../04_WaveDValueBlackboardAndVarCutoverSpec.md)
  - [../05_WaveERepresentativeGameplaySystemsCutoverSpec.md](../05_WaveERepresentativeGameplaySystemsCutoverSpec.md)
  - [../06_WaveFLegacyRemovalAndHardeningSpec.md](../06_WaveFLegacyRemovalAndHardeningSpec.md)

### Revision Note

This document records the joined migration baseline required by V21-M0.

It does not replace the wave-local inventories.
It normalizes them into one stable ledger so later milestone reviews can point to row IDs instead of reopening discovery from scratch.

---

## Purpose

The purpose of this ledger is to freeze the claim-critical migration debt that later milestones must cut over.

Every row below is intentionally concrete.
If a row is not concrete enough to tell a reviewer what still owns the current outcome, the row is not finished.

---

## Scope

This ledger covers the migration baseline across six domains:

- boot and scene entry
- scope and service composition
- command authority
- value and dynamic evaluation authority
- representative gameplay consumption
- residue and compile-boundary hardening

It does not attempt to inventory every file in the repository.
It inventories only the rows that later milestone claims must not rediscover.

---

## Ledger Rules

1. Every row has a stable Row ID.
2. A row may be retired or split later, but its original Row ID must remain traceable in history.
3. Direct-play reference rows must never be cited as proof that live boot has migrated.
4. Representative gameplay rows must never be cited as proof that residue hardening is complete.
5. If a later milestone removes the need for a row, the row should be marked replaced or quarantined rather than silently deleted from review context.

---

## Joined Baseline Ledger

| Row ID | Domain | Current authority problem | Current anchor | Owning wave | First blocking milestone | Upstream v2 owner | Primary proof family | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| V21-BL-BOOT-001 | Boot and Scene | live startup still depends on legacy auto-bootstrap and fallback root repair | [ProjectLifetimeScope.cs](../../../GameLib/Script/Project/LTS/ProjectLifetimeScope.cs) | [01_WaveABootAndSceneEntryCutoverSpec.md](../01_WaveABootAndSceneEntryCutoverSpec.md) | V21-M1 | [05_BootManifestAndProfileSpec.md](../../v2/05_BootManifestAndProfileSpec.md) | Live boot | the accepted live path must stop depending on RuntimeInitializeOnLoadMethod authority, scene search, Resources.Load fallback, or surprise GameObject repair |
| V21-BL-BOOT-002 | Boot and Scene | persistent root ownership is still hierarchy-driven and chained through legacy ensure paths | [GlobalLifetimeScope.cs](../../../GameLib/Script/Project/Global/LTS/GlobalLifetimeScope.cs) | [01_WaveABootAndSceneEntryCutoverSpec.md](../01_WaveABootAndSceneEntryCutoverSpec.md) | V21-M1 | [05_BootManifestAndProfileSpec.md](../../v2/05_BootManifestAndProfileSpec.md) | Live boot | Wave A must replace persistent-root authority instead of layering verified boot after legacy roots already exist |
| V21-BL-BOOT-003 | Boot and Scene | scene entry, loading display, and duplicate cleanup are still owned by legacy scene-flow services | [SceneService.cs](../../../GameLib/Script/Project/System/SceneFlow/SceneManager/Service/SceneService.cs); [LoadingScreenService.cs](../../../GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs) | [01_WaveABootAndSceneEntryCutoverSpec.md](../01_WaveABootAndSceneEntryCutoverSpec.md) | V21-M1 | [05_BootManifestAndProfileSpec.md](../../v2/05_BootManifestAndProfileSpec.md) | Live boot | loading authority must become verified input rather than runtime discovery and keep-first duplicate repair |
| V21-BL-BOOT-004 | Boot and Scene | direct play already uses verified-input machinery and must remain classified as a reference path rather than live authority | [AuthoringBridge.cs](../../../Editor/KernelBoot/AuthoringBridge.cs); [AuthoringBridgeDirectPlayTests.cs](../../../Editor/Tests/KernelBoot/AuthoringBridgeDirectPlayTests.cs) | [01_WaveABootAndSceneEntryCutoverSpec.md](../01_WaveABootAndSceneEntryCutoverSpec.md) | V21-M0 | [12_UnityAuthoringBridgeSpec.md](../../v2/12_UnityAuthoringBridgeSpec.md) | Direct-play reference | this row exists to stop later reviews from misreading direct-play success as live migration completion |
| V21-BL-SCOPE-001 | Scope and Service | scope composition still depends on installer discovery, builder mutation, and nearest-scope ownership inference | [RuntimeLifetimeScope.cs](../../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs); [ScopeFeatureInstallerUtility.cs](../../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs) | [02_WaveBScopeAndServiceCompositionCutoverSpec.md](../02_WaveBScopeAndServiceCompositionCutoverSpec.md) | V21-M2 | [07_ScopeGraphRuntimeSpec.md](../../v2/07_ScopeGraphRuntimeSpec.md) | Residue hardening | accepted composition cannot continue to derive scope truth from Transform hierarchy or installer ownership heuristics |
| V21-BL-SCOPE-002 | Scope and Service | coarse service and scope lookup still depend on registration-driven resolver and registry authority | [RuntimeResolverHub.cs](../../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs); [BaseLifetimeScopeRegistry.cs](../../../GameLib/Script/Common/LTS/Registry/BaseLifetimeScopeRegistry.cs) | [02_WaveBScopeAndServiceCompositionCutoverSpec.md](../02_WaveBScopeAndServiceCompositionCutoverSpec.md) | V21-M2 | [06_ServiceGraphRuntimeSpec.md](../../v2/06_ServiceGraphRuntimeSpec.md); [07_ScopeGraphRuntimeSpec.md](../../v2/07_ScopeGraphRuntimeSpec.md) | Residue hardening | registry tables and collection-style discovery remain current truth until Wave B closes them |
| V21-BL-SCOPE-003 | Scope and Service | runtime pooled scope reuse still lacks explicit generation-safe identity in the accepted path | [RuntimeLifetimeScopePool.cs](../../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScopePool.cs); [RuntimeManagerMB.cs](../../../GameLib/Script/Project/Scene/Runtime/RuntimeManager/RuntimeManagerMB.cs) | [02_WaveBScopeAndServiceCompositionCutoverSpec.md](../02_WaveBScopeAndServiceCompositionCutoverSpec.md) | V21-M2 | [07_ScopeGraphRuntimeSpec.md](../../v2/07_ScopeGraphRuntimeSpec.md) | Residue hardening | stale handles and slot-reuse leakage remain explicit baseline risks until ScopeHandle authority closes them |
| V21-BL-CMD-001 | Command Authority | accepted command outcomes still depend on bulk executor registration and scene-facing runner installation | [CommandRunnerMB.cs](../../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs) | [03_WaveCCommandDispatchCutoverSpec.md](../03_WaveCCommandDispatchCutoverSpec.md) | V21-M3 | [09_CommandCatalogRuntimeSpec.md](../../v2/09_CommandCatalogRuntimeSpec.md) | Residue hardening | command availability must stop being a byproduct of installer and collection registration |
| V21-BL-CMD-002 | Command Authority | command identity and catalog truth still depend on acquire-time loading, catalog locators, and key fallback | [CommandCatalogService.cs](../../../GameLib/Script/Common/Commands/VNext/Catalog/CommandCatalogService.cs); [CommandCatalogLocator.cs](../../../GameLib/Script/Common/Commands/VNext/Catalog/CommandCatalogLocator.cs); [CommandKeyResolver.cs](../../../GameLib/Script/Common/Commands/VNext/Catalog/CommandKeyResolver.cs) | [03_WaveCCommandDispatchCutoverSpec.md](../03_WaveCCommandDispatchCutoverSpec.md) | V21-M3 | [09_CommandCatalogRuntimeSpec.md](../../v2/09_CommandCatalogRuntimeSpec.md) | Residue hardening | stable-key dispatch fallback and convention-based catalog loading remain explicit command-authority debt |
| V21-BL-VALUE-001 | Value and Dynamic | value truth is still mixed inside blackboard hosts, hierarchical traversal, and root-repair behavior | [BlackboardMB.cs](../../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs); [BlackboardService.cs](../../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs) | [04_WaveDValueBlackboardAndVarCutoverSpec.md](../04_WaveDValueBlackboardAndVarCutoverSpec.md) | V21-M4 | [10_ValueSchemaAndStoreSpec.md](../../v2/10_ValueSchemaAndStoreSpec.md) | Residue hardening | accepted generic value truth must stop depending on parent traversal, CreateLocal, CreateGameLogicRoot, or CreateRoot repair |
| V21-BL-VALUE-002 | Value and Dynamic | grid-like runtime state remains arbitrary per-cell var payload storage rather than schema-backed value structures | [GridBlackboardService.cs](../../../GameLib/Script/Common/Variables/Blackboard/Service/GridBlackboardService.cs) | [04_WaveDValueBlackboardAndVarCutoverSpec.md](../04_WaveDValueBlackboardAndVarCutoverSpec.md) | V21-M4 | [10_ValueSchemaAndStoreSpec.md](../../v2/10_ValueSchemaAndStoreSpec.md) | Residue hardening | Wave D must normalize grid state into Table, Record, or RecordList structures before claiming migrated value truth |
| V21-BL-VALUE-003 | Value and Dynamic | runtime value identity still leaks through stable-key and registry lookup convenience surfaces | [VarIdResolver.cs](../../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs); [VarKeyRegistryLocator.cs](../../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs) | [04_WaveDValueBlackboardAndVarCutoverSpec.md](../04_WaveDValueBlackboardAndVarCutoverSpec.md) | V21-M4 | [10_ValueSchemaAndStoreSpec.md](../../v2/10_ValueSchemaAndStoreSpec.md) | Residue hardening | generated key continuity may remain, but runtime stable-key lookup and runtime registry-asset lookup must not remain accepted truth |
| V21-BL-VALUE-004 | Value and Dynamic | DynamicValue wrapper evaluation can still act as runtime truth instead of explicit evaluation-runtime authority | [DynamicValue.cs](../../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicValue.cs); [DynamicEvaluationRuntime.cs](../../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicEvaluationRuntime.cs) | [04_WaveDValueBlackboardAndVarCutoverSpec.md](../04_WaveDValueBlackboardAndVarCutoverSpec.md) | V21-M4 | [10_2_DynamicValueEvaluationSpec.md](../../v2/10_2_DynamicValueEvaluationSpec.md) | Residue hardening | generic init and generic reads must stop hiding direct wrapper evaluation as accepted runtime truth |
| V21-BL-GAME-001 | Representative Gameplay | representative state-flow still carries nearest-ancestor compatibility traversal as a possible service-resolution truth | [GameStateMachineExecutors.cs](../../../Game/Scripts/Flow/Commands/GameStateMachineExecutors.cs); [GameStateMachineService.cs](../../../Game/Scripts/Flow/GameStateMachineService.cs) | [05_WaveERepresentativeGameplaySystemsCutoverSpec.md](../05_WaveERepresentativeGameplaySystemsCutoverSpec.md) | V21-M5 | [09_CommandCatalogRuntimeSpec.md](../../v2/09_CommandCatalogRuntimeSpec.md); [10_ValueSchemaAndStoreSpec.md](../../v2/10_ValueSchemaAndStoreSpec.md) | Representative gameplay | GameStateMachine must consume migrated command and value authority instead of compatibility traversal deciding required outcomes |
| V21-BL-GAME-002 | Representative Gameplay | conversation and dialogue progression still depend on scene-facing hub hosts and could hide missing migrated boundaries | [ConversationExecutors.cs](../../../GameLib/Script/Common/Commands/VNext/Executors/UI/ConversationExecutors.cs); [ConversationChannelHubService.cs](../../../GameLib/Script/Project/UI/Core/Conversation/Channel/ConversationChannelHubService.cs); [DialogChannelRuntime.cs](../../../GameLib/Script/Project/UI/Core/Dialog/Channel/DialogChannelRuntime.cs) | [05_WaveERepresentativeGameplaySystemsCutoverSpec.md](../05_WaveERepresentativeGameplaySystemsCutoverSpec.md) | V21-M5 | [09_CommandCatalogRuntimeSpec.md](../../v2/09_CommandCatalogRuntimeSpec.md); [10_ValueSchemaAndStoreSpec.md](../../v2/10_ValueSchemaAndStoreSpec.md) | Representative gameplay | visible session continuity is not sufficient if missing services or dialogue-close failures are still masked |
| V21-BL-GAME-003 | Representative Gameplay | presentation flow still mixes migrated behavior with local blackboard merge and helper-driven scope preparation | [GridObjectChannelPayloadBuilder.cs](../../../GameLib/Script/Project/Scene/Channels/GridObjectChannel/GridObjectChannelPayloadBuilder.cs); [TraitListChannelRuntime.cs](../../../GameLib/Script/Project/UI/TraitListChannel/Service/TraitListChannelRuntime.cs) | [05_WaveERepresentativeGameplaySystemsCutoverSpec.md](../05_WaveERepresentativeGameplaySystemsCutoverSpec.md) | V21-M5 | [07_ScopeGraphRuntimeSpec.md](../../v2/07_ScopeGraphRuntimeSpec.md); [10_ValueSchemaAndStoreSpec.md](../../v2/10_ValueSchemaAndStoreSpec.md) | Representative gameplay | GridObject and TraitList visible correctness must stop depending on legacy helper truth |
| V21-BL-GAME-004 | Representative Gameplay | visible effect behavior can still depend on optional lazy dependency resolution and mixed service or value truth | [StatusEffectService.cs](../../../GameLib/Script/Common/StatusEffect/Runtime/StatusEffectService.cs) | [05_WaveERepresentativeGameplaySystemsCutoverSpec.md](../05_WaveERepresentativeGameplaySystemsCutoverSpec.md) | V21-M5 | [10_1_ScalarRuntimeAndBindingSpec.md](../../v2/10_1_ScalarRuntimeAndBindingSpec.md); [10_ValueSchemaAndStoreSpec.md](../../v2/10_ValueSchemaAndStoreSpec.md) | Representative gameplay | Wave E must prove explicit boundary consumption without silently re-owning scalar semantics |
| V21-BL-RES-001 | Residue and Compile Boundary | migration residue already has metadata and validation infrastructure that later hardening must consume rather than reinvent | [LegacyMigrationModel.cs](../../../GameLib/Script/Kernel/Validation/LegacyMigrationModel.cs); [LegacyCompatBoundaryTests.cs](../../../Editor/Tests/LegacyCompatBoundaryTests.cs) | [06_WaveFLegacyRemovalAndHardeningSpec.md](../06_WaveFLegacyRemovalAndHardeningSpec.md) | V21-M6 | [13_LegacyCompatBoundarySpec.md](../../v2/13_LegacyCompatBoundarySpec.md) | Residue hardening | residue can already be classified, profiled, and validated as explicit compatibility rather than silent repair |
| V21-BL-RES-002 | Residue and Compile Boundary | the repo already has a partial asmdef split and executable boundary checks, but legacy or gameplay residue is not yet fully quarantined | [GameLib.Kernel.Diagnostics.asmdef](../../../GameLib/Script/Kernel/Diagnostics/Core/GameLib.Kernel.Diagnostics.asmdef); [GameLib.Kernel.Validation.asmdef](../../../GameLib/Script/Kernel/Validation/GameLib.Kernel.Validation.asmdef); [GameLib.Kernel.Boot.asmdef](../../../GameLib/Script/Kernel/Boot/GameLib.Kernel.Boot.asmdef); [KernelDiagnosticsAsmdefBoundaryTests.cs](../../../Editor/Tests/KernelDiagnostics/KernelDiagnosticsAsmdefBoundaryTests.cs) | [06_WaveFLegacyRemovalAndHardeningSpec.md](../06_WaveFLegacyRemovalAndHardeningSpec.md) | V21-M6 | [17_AssemblyDefinitionAndCompileBoundarySpec.md](../../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md) | Residue hardening | V21-M0 must freeze the fact that compile-boundary work starts from a partial split, not from a monolith |

---

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V21-BL-01 | Confirm the joined baseline covers all claim-critical domains. | The ledger must include boot and scene, scope and service, command, value and dynamic, representative gameplay, and residue or compile-boundary rows. |
| TC-V21-BL-02 | Confirm live boot and direct play are separated in the baseline. | The ledger must include a live-boot row and a direct-play reference row with different proof-family classifications. |
| TC-V21-BL-03 | Confirm representative gameplay debt is frozen as real GameScene-facing rows. | The ledger must include GameStateMachine, conversation or dialogue, GridObject or TraitList, and StatusEffect rows. |
| TC-V21-BL-04 | Confirm residue and compile-boundary debt are frozen explicitly. | The ledger must include LegacyMigrationModel or LegacyCompatBoundaryTests and kernel asmdef or asmdef-boundary rows. |
| TC-V21-BL-05 | Confirm every row has a stable Row ID and milestone boundary. | The ledger must expose Row ID, Owning wave, and First blocking milestone columns. |
| TC-V21-BL-06 | Confirm the ledger is a joined review artifact rather than a second per-wave narrative. | Purpose and Ledger Rules must state that this document normalizes the wave-local inventories instead of replacing them. |

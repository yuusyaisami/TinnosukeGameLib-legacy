# Kernel v2.1 Preservation Floor Ledger

## Document Status

- Document ID: KernelV21PreservationFloorLedger
- Status: Draft
- Role: preservation ledger that freezes the V21 global preservation floor while separating it from wave-local continuity and explicitly replaceable surfaces
- Depends on:
  - [../00_KernelV21MigrationOverviewSpec.md](../00_KernelV21MigrationOverviewSpec.md)
  - [../01_WaveABootAndSceneEntryCutoverSpec.md](../01_WaveABootAndSceneEntryCutoverSpec.md)
  - [../02_WaveBScopeAndServiceCompositionCutoverSpec.md](../02_WaveBScopeAndServiceCompositionCutoverSpec.md)
  - [../03_WaveCCommandDispatchCutoverSpec.md](../03_WaveCCommandDispatchCutoverSpec.md)
  - [../04_WaveDValueBlackboardAndVarCutoverSpec.md](../04_WaveDValueBlackboardAndVarCutoverSpec.md)
  - [../05_WaveERepresentativeGameplaySystemsCutoverSpec.md](../05_WaveERepresentativeGameplaySystemsCutoverSpec.md)
  - [../06_WaveFLegacyRemovalAndHardeningSpec.md](../06_WaveFLegacyRemovalAndHardeningSpec.md)
  - [../07_KernelV21MigrationMilestoneOrderSpec.md](../07_KernelV21MigrationMilestoneOrderSpec.md)
- Provides foundation for:
  - [../01_WaveABootAndSceneEntryCutoverSpec.md](../01_WaveABootAndSceneEntryCutoverSpec.md)
  - [../02_WaveBScopeAndServiceCompositionCutoverSpec.md](../02_WaveBScopeAndServiceCompositionCutoverSpec.md)
  - [../03_WaveCCommandDispatchCutoverSpec.md](../03_WaveCCommandDispatchCutoverSpec.md)
  - [../04_WaveDValueBlackboardAndVarCutoverSpec.md](../04_WaveDValueBlackboardAndVarCutoverSpec.md)
  - [../05_WaveERepresentativeGameplaySystemsCutoverSpec.md](../05_WaveERepresentativeGameplaySystemsCutoverSpec.md)
  - [../06_WaveFLegacyRemovalAndHardeningSpec.md](../06_WaveFLegacyRemovalAndHardeningSpec.md)

### Revision Note

This document records the preserved-surface ledger required by V21-M0.

The v2.1 preservation floor is intentionally narrow.
This ledger exists to stop later milestones from silently widening that floor by confusing wave-local continuity with globally preserved architecture.

---

## Purpose

The purpose of this ledger is to answer one question unambiguously:

```text
Is this surface globally preserved, only slice-local continuity, explicitly replaceable, or quarantine-only?
```

If the answer is ambiguous, later milestone claims become impossible to audit.

---

## Scope

This ledger classifies surfaces into four classes:

- GlobalFloor
- WaveLocalContinuity
- Replaceable
- QuarantineOnly

Only GlobalFloor rows are globally preserved across every later milestone.
WaveLocalContinuity rows remain important, but only within the slice that owns them.

---

## Ledger Rules

1. `GlobalFloor` rows are the only globally preserved surfaces unless [../00_KernelV21MigrationOverviewSpec.md](../00_KernelV21MigrationOverviewSpec.md) and [../07_KernelV21MigrationMilestoneOrderSpec.md](../07_KernelV21MigrationMilestoneOrderSpec.md) are both updated explicitly.
2. `WaveLocalContinuity` rows do not widen the global floor.
3. `Replaceable` rows may be changed destructively when the owning milestone preserves the required global floor or slice-local continuity above them.
4. `QuarantineOnly` rows may remain temporarily, but they must never be treated as accepted-path truth.
5. A row must name the drift trigger that would prove the classification has been silently violated.

---

## Global Preservation Floor

| Row ID | Class | Surface | Current anchor | Owning doc | Allowed milestone interaction | Drift trigger |
| --- | --- | --- | --- | --- | --- | --- |
| V21-PF-001 | GlobalFloor | compatibility-facing command field shape and payload meaning | [FunctionCommandData.cs](../../../GameLib/Script/Common/Commands/VNext/Commands/Core/FunctionCommandData.cs); [WithActorCommandData.cs](../../../GameLib/Script/Common/Commands/VNext/Commands/Core/WithActorCommandData.cs); [SceneChangeCommandData.cs](../../../GameLib/Script/Common/Commands/VNext/Commands/Scene/SceneChangeCommandData.cs) | [../00_KernelV21MigrationOverviewSpec.md](../00_KernelV21MigrationOverviewSpec.md) | V21-M1 through V21-M6 may replace authority beneath these surfaces, but may not silently remove or reinterpret the fields | adding, removing, renaming, or silently reinterpreting command payload fields without an explicit v2.1 contract revision |
| V21-PF-002 | GlobalFloor | SerializeReference-based DynamicValue authoring wrapper surface | [DynamicValue.cs](../../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicValue.cs) | [../00_KernelV21MigrationOverviewSpec.md](../00_KernelV21MigrationOverviewSpec.md) | V21-M4 may replace runtime authority beneath the wrapper, but may not silently replace the authored wrapper surface | replacing the authored wrapper with a new authoring surface while calling the change internal-only |
| V21-PF-003 | GlobalFloor | generated ValueStore key identity continuity | [VarIdResolver.cs](../../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs); [VarKeyRef.cs](../../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRef.cs) | [../00_KernelV21MigrationOverviewSpec.md](../00_KernelV21MigrationOverviewSpec.md) | V21-M4 and V21-M6 may remove runtime lookup convenience beneath this surface, but may not silently renumber generated identities | silent key renumbering, alias-driven identity drift, or runtime-only identity repair being reintroduced under the name of continuity |

---

## Wave-Local Continuity Surfaces

| Row ID | Class | Surface | Current anchor | Owning wave | Allowed milestone interaction | Drift trigger |
| --- | --- | --- | --- | --- | --- | --- |
| V21-PF-101 | WaveLocalContinuity | scene-change payload shape and high-level transition meaning | [SceneChangeCommandData.cs](../../../GameLib/Script/Common/Commands/VNext/Commands/Scene/SceneChangeCommandData.cs); [SceneChangeExecutor.cs](../../../GameLib/Script/Common/Commands/VNext/Executors/Scene/SceneChangeExecutor.cs) | [../01_WaveABootAndSceneEntryCutoverSpec.md](../01_WaveABootAndSceneEntryCutoverSpec.md) | V21-M1 and V21-M3 may move authority behind the surface without changing the authored payload contract | a scene-change payload stays syntactically valid but no longer carries the same high-level load, additive-load, or unload meaning |
| V21-PF-102 | WaveLocalContinuity | GameScene mapping, loading config semantics, loading observability, and generated scene event continuity | [SceneService.cs](../../../GameLib/Script/Project/System/SceneFlow/SceneManager/Service/SceneService.cs); [SceneFlowInstallerMB.cs](../../../GameLib/Script/Project/System/SceneFlow/MB/SceneFlowInstallerMB.cs); [EventKeys.g.cs](../../../GameLib/Script/Generated/EventKeys.g.cs) | [../01_WaveABootAndSceneEntryCutoverSpec.md](../01_WaveABootAndSceneEntryCutoverSpec.md) | V21-M1 may replace ownership and root placement beneath these surfaces | loading still appears to work only because observability or generated scene event contracts regressed silently |
| V21-PF-201 | WaveLocalContinuity | representative coarse-service consumer continuity and scope-domain continuity | [ProjectLifetimeScope.cs](../../../GameLib/Script/Project/LTS/ProjectLifetimeScope.cs); [GlobalLifetimeScope.cs](../../../GameLib/Script/Project/Global/LTS/GlobalLifetimeScope.cs); [SceneLifetimeScope.cs](../../../GameLib/Script/Project/Scene/LTS/SceneLifetimeScope.cs); [RuntimeLifetimeScope.cs](../../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs) | [../02_WaveBScopeAndServiceCompositionCutoverSpec.md](../02_WaveBScopeAndServiceCompositionCutoverSpec.md) | V21-M2 may move parent truth and service boundaries while keeping representative consumers functional | accepted composition claims succeed only because the old scope or service boundary is still the hidden truth |
| V21-PF-301 | WaveLocalContinuity | representative command surface continuity for Function, WithActor, CommandChannel, control-flow, and lifecycle wrappers | [FunctionCommandData.cs](../../../GameLib/Script/Common/Commands/VNext/Commands/Core/FunctionCommandData.cs); [CommandChannelCommandData.cs](../../../GameLib/Script/Common/Commands/VNext/Commands/Core/CommandChannelCommandData.cs); [ControlCommandData.cs](../../../GameLib/Script/Common/Commands/VNext/Commands/Control/ControlCommandData.cs); [LifecycleCommandData.cs](../../../GameLib/Script/Common/Commands/VNext/Commands/Core/LifecycleCommandData.cs) | [../03_WaveCCommandDispatchCutoverSpec.md](../03_WaveCCommandDispatchCutoverSpec.md) | V21-M3 may change dispatch authority beneath these surfaces | representative command content still runs only because legacy discovery or fallback remained authoritative |
| V21-PF-401 | WaveLocalContinuity | local-init, grid-init, and representative external binding surfaces remain consumable | [BlackboardValueInitRuntime.cs](../../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardValueInitRuntime.cs); [ExternalBoolBinding.cs](../../../GameLib/Script/Common/Variables/Binding/ExternalBoolBinding.cs); [ExternalFloatBinding.cs](../../../GameLib/Script/Common/Variables/Binding/ExternalFloatBinding.cs) | [../04_WaveDValueBlackboardAndVarCutoverSpec.md](../04_WaveDValueBlackboardAndVarCutoverSpec.md) | V21-M4 may move init and evaluation authority beneath these surfaces | representative authored init content survives only because legacy blackboard fallback is still doing the real work |
| V21-PF-501 | WaveLocalContinuity | GameStateMachine, conversation, and dialogue player-visible flow continuity | [GameStateMachineCommandData.cs](../../../Game/Scripts/Flow/Commands/GameStateMachineCommandData.cs); [ConversationCommandData.cs](../../../GameLib/Script/Common/Commands/VNext/Commands/UI/ConversationCommandData.cs); [DialogChannelRuntime.cs](../../../GameLib/Script/Project/UI/Core/Dialog/Channel/DialogChannelRuntime.cs) | [../05_WaveERepresentativeGameplaySystemsCutoverSpec.md](../05_WaveERepresentativeGameplaySystemsCutoverSpec.md) | V21-M5 may cut over migrated consumption while keeping player-visible flow semantics stable | gameplay looks correct only because missing required boundaries are still being repaired silently |
| V21-PF-502 | WaveLocalContinuity | GridObject and TraitList payload, bind, refresh, choice, and presentation continuity | [GridObjectChannelCommandData.cs](../../../GameLib/Script/Common/Commands/VNext/Commands/Channels/GridObjectChannelCommandData.cs); [TraitListChannelCommandData.cs](../../../GameLib/Script/Common/Commands/VNext/Commands/Trait/List/TraitListChannelCommandData.cs) | [../05_WaveERepresentativeGameplaySystemsCutoverSpec.md](../05_WaveERepresentativeGameplaySystemsCutoverSpec.md) | V21-M5 may replace hidden scope or value authority while preserving visible presentation meaning | visible list behavior only remains correct because local blackboard merge or helper-driven scope repair is still deciding outcomes |
| V21-PF-503 | WaveLocalContinuity | StatusEffect visible runtime behavior continuity | [StatusEffectCommandData.cs](../../../GameLib/Script/Common/Commands/VNext/Commands/StatusEffect/StatusEffectCommandData.cs); [StatusEffectService.cs](../../../GameLib/Script/Common/StatusEffect/Runtime/StatusEffectService.cs) | [../05_WaveERepresentativeGameplaySystemsCutoverSpec.md](../05_WaveERepresentativeGameplaySystemsCutoverSpec.md) | V21-M5 may replace service, command, and value authority while scalar ownership remains external | Wave E starts redefining scalar semantics instead of consuming the external scalar boundary |

---

## Explicitly Replaceable or Quarantine-Only Surfaces

| Row ID | Class | Surface | Current anchor | First milestone allowed to replace or demote it | Rule |
| --- | --- | --- | --- | --- | --- |
| V21-PF-901 | Replaceable | boot entry wiring, persistent-root creation, and scene-root authority | [ProjectLifetimeScope.cs](../../../GameLib/Script/Project/LTS/ProjectLifetimeScope.cs); [GlobalLifetimeScope.cs](../../../GameLib/Script/Project/Global/LTS/GlobalLifetimeScope.cs); [SceneLifetimeScope.cs](../../../GameLib/Script/Project/Scene/LTS/SceneLifetimeScope.cs) | V21-M1 | these are replaceable architecture details, not preserved gameplay-facing surfaces |
| V21-PF-902 | Replaceable | lifetime-scope hierarchy shape and Transform-parent scope inference | [RuntimeLifetimeScope.cs](../../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs); [ScopeFeatureInstallerUtility.cs](../../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs) | V21-M2 | accepted scope truth must move to explicit ScopeGraph authority |
| V21-PF-903 | Replaceable | installer discovery, registration-table resolver discovery, and registry authority | [ScopeFeatureInstallerUtility.cs](../../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs); [RuntimeResolverHub.cs](../../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs); [BaseLifetimeScopeRegistry.cs](../../../GameLib/Script/Common/LTS/Registry/BaseLifetimeScopeRegistry.cs) | V21-M2 | these may be replaced destructively and must not be reclassified as preserved because existing scenes still contain them |
| V21-PF-904 | Replaceable | loading fallback discovery, duplicate cleanup, and required-asset repair | [LoadingScreenService.cs](../../../GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs) | V21-M1 | loading may remain visible to content while its discovery-based architecture is replaced underneath |
| V21-PF-905 | Replaceable | bulk command registration, catalog-convention loading, and runtime key fallback | [CommandRunnerMB.cs](../../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs); [CommandCatalogService.cs](../../../GameLib/Script/Common/Commands/VNext/Catalog/CommandCatalogService.cs); [CommandKeyResolver.cs](../../../GameLib/Script/Common/Commands/VNext/Catalog/CommandKeyResolver.cs) | V21-M3 | command authority must change even if legacy helpers remain temporarily callable during migration |
| V21-PF-906 | Replaceable | blackboard hierarchical fallback, root-creation repair, runtime registry lookup convenience, and hidden generic DynamicValue evaluation | [BlackboardService.cs](../../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs); [VarKeyRegistryLocator.cs](../../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs); [DynamicValue.cs](../../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicValue.cs) | V21-M4 | generic value truth must stop depending on these conveniences even if authored content remains stable |
| V21-PF-907 | Replaceable | representative gameplay helper traversal, local blackboard merge, and helper-driven scope preparation | [GameStateMachineExecutors.cs](../../../Game/Scripts/Flow/Commands/GameStateMachineExecutors.cs); [GridObjectChannelPayloadBuilder.cs](../../../GameLib/Script/Project/Scene/Channels/GridObjectChannel/GridObjectChannelPayloadBuilder.cs); [TraitListChannelRuntime.cs](../../../GameLib/Script/Project/UI/TraitListChannel/Service/TraitListChannelRuntime.cs) | V21-M5 | gameplay helper residue must not become hidden preserved architecture simply because it still makes scenes look correct |
| V21-PF-908 | QuarantineOnly | migration-only legacy adapters, residue hosts, and bounded compatibility shims | [RuntimeLifetimeScope.cs](../../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs); [CommandRunnerMB.cs](../../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs); [BlackboardMB.cs](../../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs); [LegacyMigrationModel.cs](../../../GameLib/Script/Kernel/Validation/LegacyMigrationModel.cs) | V21-M6 | these may remain only as explicit quarantine with diagnostics, profile bounds, and removal policy; they are never accepted-path truth |

---

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V21-PF-01 | Confirm the global preservation floor stays narrow. | This document must classify only command field shape, DynamicValue authoring surface, and generated value-key identity as GlobalFloor. |
| TC-V21-PF-02 | Confirm wave-local continuity is separated from the global floor. | This document must contain WaveLocalContinuity rows for Wave A through Wave E surfaces. |
| TC-V21-PF-03 | Confirm explicitly replaceable architecture is recorded instead of silently treated as preserved. | This document must contain Replaceable rows for boot wiring, composition, command bootstrap, value fallback, and gameplay helpers. |
| TC-V21-PF-04 | Confirm migration-only residue is classified as quarantine-only rather than preserved. | This document must contain a QuarantineOnly row for legacy adapters or residue hosts. |
| TC-V21-PF-05 | Confirm every row names the drift trigger that would violate the classification. | All tables must have a drift-trigger or rule column. |
| TC-V21-PF-06 | Confirm the ledger requires explicit documentation to widen the global floor. | Ledger Rules must point back to 00_KernelV21MigrationOverviewSpec.md and 07_KernelV21MigrationMilestoneOrderSpec.md for any GlobalFloor expansion. |

# Kernel v2.2 Baseline Ledger

## Document Status

- Document ID: KernelV22BaselineLedger
- Status: Draft
- Role: joined baseline ledger for the debts that still block kernel-only release authority in v2.2
- Depends on:
  - [README.md](README.md)
  - [../01_KernelV22AuthorityAndServiceCensusSpec.md](../01_KernelV22AuthorityAndServiceCensusSpec.md)
  - [../../v2.1/Index/KernelV21BaselineLedger.md](../../v2.1/Index/KernelV21BaselineLedger.md)

## Joined Baseline Ledger

| Row ID | Domain | Current debt | Primary anchor | Target class | First blocking milestone | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| V22-BL-HOST-001 | Live host | ProjectLifetimeScope, GlobalLifetimeScope, and SceneLifetimeScope still represent legacy live-root pressure in the repository | ProjectLifetimeScope.cs; GlobalLifetimeScope.cs; SceneLifetimeScope.cs | DeleteTarget | M1 | accepted release path must move root ownership to kernel-only host chain |
| V22-BL-HOST-002 | Scope and service authority | RuntimeLifetimeScope and RuntimeResolverHub still embody legacy scope/service authority pressure | RuntimeLifetimeScope.cs; RuntimeResolverHub.cs | DeleteTarget | M1 | accepted scope and service truth must stop flowing through legacy builder or resolver ownership |
| V22-BL-HOST-003 | Live/direct convergence | verified direct-play preparation already exists, but the accepted live path can still diverge architecturally through legacy host entry and mixed authority | KernelLiveBootOrchestrator.cs; AuthoringBridge.cs; ProjectLifetimeScope.cs | KernelCoreAuthority | M1 | M1 must converge live boot and direct play on the same verified-input host truth and reject mixed-authority activation |
| V22-BL-CMD-001 | Command host | CommandRunnerMB still acts as a bulk executor registration host | CommandRunnerMB.cs | DeleteTarget | M2 | verified command authority must replace scene-facing command bootstrap |
| V22-BL-VALUE-001 | Value host | BlackboardMB and BlackboardService fallback still represent mixed value authority pressure | BlackboardMB.cs; BlackboardService.cs | DeleteTarget | M2 | value init, store, and fallback repair must stop flowing through legacy value hosts |
| V22-BL-FAMILY-001 | Mixed-boundary family | LoadingScreenService, TooltipChannelHubService, and AnimationSpriteHubService still require split before stable service eligibility | LoadingScreenService.cs; TooltipChannelHubService.cs; AnimationSpriteHubService.cs | KernelManagedFeatureService | M3 | split first, then claim coarse service authority |
| V22-BL-FAMILY-002 | Coarse service template | ModalStackChannelHubService and MeshChannelHubService already model the preferred coarse-service plus hub-owned-runtime-object shape | ModalStackChannelHubService.cs; MeshChannelHubService.cs | KernelManagedFeatureService | M3 | use these as positive templates rather than re-expanding them |
| V22-BL-GAME-001 | Representative gameplay | GameStateMachine, Conversation and Dialogue, GridObject and Trait presentation, and StatusEffect remain the critical gameplay/application family slices | GameStateMachineExecutors.cs; ConversationExecutors.cs; GridObjectChannelRuntime.cs; TraitListChannelRuntime.cs; StatusEffectService.cs | KernelManagedFeatureService | M4 | they prove whether gameplay/application services actually consume kernel-only runtime authority rather than legacy convenience paths |
| V22-BL-HARD-001 | Hardening | partial asmdef split and legacy gate infrastructure exist, but release zero-authority proof is not yet closed | LegacyCompatBoundaryTests.cs; KernelForbiddenPatternTests.cs; KernelDiagnosticsAsmdefBoundaryTests.cs | DeleteTarget | M5 | v2.2 must convert existing hardening substrate into release completion evidence |

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V22-BL-01 | Confirm the ledger joins host, command, value, family, gameplay, and hardening debt. | This file must contain V22-BL-HOST-001, V22-BL-HOST-003, V22-BL-CMD-001, V22-BL-VALUE-001, V22-BL-FAMILY-001, V22-BL-GAME-001, and V22-BL-HARD-001. |
| TC-V22-BL-02 | Confirm delete targets are explicit in the ledger. | The Target class column must contain DeleteTarget. |
| TC-V22-BL-03 | Confirm coarse-service templates are preserved positively. | This file must contain ModalStackChannelHubService.cs and MeshChannelHubService.cs. |
| TC-V22-BL-04 | Confirm representative gameplay anchors are fixed in the ledger. | This file must contain GameStateMachineExecutors.cs and StatusEffectService.cs. |
| TC-V22-BL-05 | Confirm live/direct host divergence is frozen explicitly as M1 debt. | This file must contain AuthoringBridge.cs and describe convergence between live boot and direct play. |
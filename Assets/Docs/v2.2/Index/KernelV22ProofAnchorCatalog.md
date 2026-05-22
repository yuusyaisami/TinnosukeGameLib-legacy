# Kernel v2.2 Proof Anchor Catalog

## Document Status

- Document ID: KernelV22ProofAnchorCatalog
- Status: Draft
- Role: separates the proof families that v2.2 completion claims are allowed to use
- Depends on:
  - [README.md](README.md)
  - [../00_KernelV22CompletionOverviewSpec.md](../00_KernelV22CompletionOverviewSpec.md)
  - [../04_KernelV22LegacyDeletionAndHardeningSpec.md](../04_KernelV22LegacyDeletionAndHardeningSpec.md)
  - [../04_1_KernelV22FullProofAndReleaseHardeningSpec.md](../04_1_KernelV22FullProofAndReleaseHardeningSpec.md)
  - [../../v2.1/Index/KernelV21ProofAnchorCatalog.md](../../v2.1/Index/KernelV21ProofAnchorCatalog.md)

## Proof Family Rules

1. Spec package proof shows that the v2.2 claim surface is fixed and testable.
2. Live kernel-only host proof shows that the accepted live host chain is kernel-owned.
3. Command/value host-removal proof shows that accepted execution no longer depends on scene-facing command/value hosts.
4. Service-family cutover proof shows that representative service families consume kernel-owned host authority.
5. Release hardening proof shows that forbidden patterns, compile-boundary inversion, and runtime-capable legacy authority are rejected for release.
6. direct-play reference may remain useful, but it is inherited reference evidence rather than a primary v2.2 completion proof family.
7. Live kernel-only host proof may use direct-play reference only as a convergence check; it may not count as live-host success by itself.
8. Gameplay/application proof may not be used to close M3 family proof.
9. Representative gameplay/application proof must run through the real GameScene anchors and include traceable diagnostics evidence.
10. Release hardening proof fixes M5 deletion, bounded residue, compile-boundary enforcement, and forbidden-pattern rejection. It does not by itself close M6 full-proof aggregation.
11. M6 full-proof aggregation consumes the earlier proof families and required gate classes without collapsing them into one interchangeable signal.
12. Integration smoke may summarize the release claim, but it may not excuse missing Validation, Generation, Diagnostics, PerformanceRule, StaticRule, or LegacyCompat gates.
13. [../04_1_KernelV22FullProofAndReleaseHardeningSpec.md](../04_1_KernelV22FullProofAndReleaseHardeningSpec.md) owns the canonical M6 gate-class bundle map, current runner references, and release-claim row shape.
14. This catalog owns proof-family separation and current anchor identity; no gate-class row may replace a missing proof-family anchor, and no proof family may be treated as an interchangeable gate.

## Proof Anchor Catalog

| Anchor ID | Proof family | Evidence form | Current anchor | Proves | Does not prove |
| --- | --- | --- | --- | --- | --- |
| V22-PA-SPEC-001 | Spec package proof | Contract document | 00_KernelV22CompletionOverviewSpec.md; Index/README.md | the v2.2 completion package and continuity/abolition vocabulary are fixed | runtime cutover is already complete |
| V22-PA-LIVE-001 | Live kernel-only host proof | Source anchor | KernelLiveBootOrchestrator.cs; KernelRuntimeShell.cs | the intended kernel-only live host chain has concrete runtime anchors | representative gameplay and deletion hardening are already proven |
| V22-PA-LIVE-002 | Live kernel-only host proof | Representative scene anchor | TitleScene.unity; GameScene.unity | the live scene anchors that host proof must exercise are fixed | scene anchors alone do not prove kernel-only runtime authority |
| V22-PA-LIVE-003 | Live kernel-only host proof | Contract document | 02_KernelV22KernelOnlyHostSpec.md; 05_KernelV22MilestoneOrderSpec.md | the M1 host chain, direct-play convergence, and mixed-authority rejection rules are fixed for review | executable host proof is already green |
| V22-PA-LIVE-004 | Live kernel-only host proof | Inherited reference anchor | AuthoringBridge.cs; AuthoringBridgeDirectPlayTests.cs | direct play remains a verified-input reference path that can be compared against the accepted live host chain | direct play by itself does not prove accepted live host migration |
| V22-PA-CV-001 | Command/value host-removal proof | Contract document | 02_1_KernelV22CommandAndValueHostRemovalSpec.md; 05_KernelV22MilestoneOrderSpec.md | the M2 command/value host-removal rules and claim gate are fixed for review | family execution and release hardening are already proven |
| V22-PA-CV-002 | Command/value host-removal proof | Source anchor | CommandRunnerMB.cs; VerifiedCommandRuntimeBridge.cs; BlackboardMB.cs; BlackboardService.cs; DynamicEvaluationRuntime.cs | representative command/value host-removal anchors and handoff boundaries are explicit | accepted execution is already green |
| V22-PA-FAMILY-001 | Service-family cutover proof | Contract document | 03_KernelV22ServiceFamilyCutoverSpec.md; 05_KernelV22MilestoneOrderSpec.md | the family buckets, split rules, and M3 versus M4 claim boundaries are fixed for review | executable family proof is already green |
| V22-PA-FAMILY-002 | Service-family cutover proof | Boot and Scene Flow source anchor | SceneFlowInstallerMB.cs; SceneService.cs; LoadingScreenService.cs; SceneChangeExecutor.cs | the M3 Boot and Scene Flow family anchors and cutover pressure are explicit | first-channel and gameplay/application proof are not already complete |
| V22-PA-FAMILY-003 | Service-family cutover proof | First channel family source anchor | ModalStackChannelHubService.cs; TooltipChannelHubService.cs; ConversationChannelHubService.cs; MeshChannelHubService.cs; AnimationSpriteHubService.cs; TraitListChannelRuntime.cs; GridObjectChannelRuntime.cs | the M3 first-channel family anchors, split pressure, and positive templates are explicit | gameplay/application proof and release hardening are not already complete |
| V22-PA-FAMILY-004 | Service-family cutover proof | Contract document | 03_1_KernelV22RepresentativeGameplayAndApplicationCutoverSpec.md; 05_KernelV22MilestoneOrderSpec.md | the M4 representative gameplay/application rules and claim gate are fixed for review | executable gameplay/application proof is already green |
| V22-PA-FAMILY-005 | Service-family cutover proof | Representative gameplay/application source anchor | GameStateMachineExecutors.cs; ConversationExecutors.cs; GridObjectChannelRuntime.cs; TraitListChannelRuntime.cs; StatusEffectService.cs | the representative gameplay/application slice anchors are explicit and remain distinct from M3 family proof | release hardening is not already complete |
| V22-PA-FAMILY-006 | Service-family cutover proof | Representative scene and diagnostics anchor | Scenes/GameScene/GameScene.unity; CommandExecutionTrace.cs; DynamicRuntimeLogUtility.cs; TargetChannelHubService.cs; AreaChannelHubService.cs | M4 proof requires the real GameScene anchors, traceability, and subordinate dependency visibility | gameplay/application appearance alone does not prove migrated authority |
| V22-PA-HARD-001 | Release hardening proof | Contract document | 04_KernelV22LegacyDeletionAndHardeningSpec.md; 05_KernelV22MilestoneOrderSpec.md | the M5 deletion, bounded residue, and compile-boundary rules are fixed for review | M6 full-proof aggregation is already complete |
| V22-PA-HARD-002 | Release hardening proof | Residue source anchor | ScopeFeatureInstallerUtility.cs; RuntimeLifetimeScope.cs; RuntimeResolverHub.cs; CommandRunnerMB.cs; BlackboardMB.cs; BlackboardService.cs; VarKeyRegistryLocator.cs | release-blocking residue and delete-target pressure are explicit | hardening gates are already green |
| V22-PA-HARD-003 | Release hardening proof | Executable gate | LegacyCompatBoundaryTests.cs; LegacyMigrationModel.cs | runtime-capable adapter legality, profile bounds, and removal metadata are testable | compile-boundary direction is already proven |
| V22-PA-HARD-004 | Release hardening proof | Static gate | KernelForbiddenPatternTests.cs | forbidden discovery, fallback, lookup, and legacy re-entry patterns are testable | residue deletion is already complete |
| V22-PA-HARD-005 | Release hardening proof | Compile-boundary gate | KernelDiagnosticsAsmdefBoundaryTests.cs; GameLib.Kernel.Boot.asmdef; GameLib.Tests.Kernel.Boot.Editor.asmdef | partial-split dependency direction is visible and enforceable | full release proof is already complete |
| V22-PA-HARD-006 | Release hardening proof | Contract document | 04_1_KernelV22FullProofAndReleaseHardeningSpec.md; 05_KernelV22MilestoneOrderSpec.md | the M6 full-proof aggregation rules, canonical bundle map ownership, and required gate bundle are fixed for review | lower-layer executable bundles are already green |
| V22-PA-HARD-007 | Release hardening proof | Runtime aggregation bundle | KernelV22LiveBootBundleTests.cs; KernelV22RepresentativeGameSceneBundleTests.cs; KernelMinimalBootPlayModeTests.cs; AuthoringBridgeDirectPlayTests.cs; GameStateMachineMigrationTests.cs; ConversationDialogueMigrationTests.cs; GameplayAuthorityRegressionTests.cs; GridObjectAuthorityMigrationTests.cs; TraitListAuthorityMigrationTests.cs; StatusEffectServiceDependencyCaptureTests.cs | the accepted live boot, direct-play convergence reference, and representative gameplay/application executable anchors that M6 must aggregate are concrete | diagnostics, static, legacy, and performance gates are already green |
| V22-PA-HARD-008 | Release hardening proof | Diagnostics and performance gate | KernelDiagnosticsModelTests.cs; KernelDiagnosticServiceTests.cs; DiagnosticCodeTraceabilityTests.cs; KernelPerformanceAllocationTests.cs; KernelPerformanceRegressionGateTests.cs; KernelProfilerMarkerTaxonomyTests.cs | diagnostics provenance and performance-rule enforcement are executable parts of the M6 claim bundle | live boot and residue deletion are already complete |
| V22-PA-HARD-009 | Release hardening proof | Static, legacy, and aggregation gate | KernelForbiddenPatternTests.cs; KernelForbiddenPatternScannerTests.cs; LegacyCompatBoundaryTests.cs; KernelDiagnosticsAsmdefBoundaryTests.cs; Tools/Run-UnityTests.ps1; Tools/Run-M15.4Gate.ps1 | M6 final aggregation consumes static-rule, legacy-compat, compile-boundary, and ordered runner evidence with reviewable gate-class failure output | test runner infrastructure by itself does not prove release completion |

## Harness Note

Tools/Run-UnityTests.ps1 remains execution infrastructure.
It is not itself a proof family.

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V22-PA-01 | Confirm the five v2.2 proof families are separated explicitly. | This file must distinguish spec package proof, live kernel-only host proof, command/value host-removal proof, service-family cutover proof, and release hardening proof. |
| TC-V22-PA-02 | Confirm direct play is not promoted to a primary completion proof family. | Proof Family Rules must treat direct-play reference as inherited reference evidence only. |
| TC-V22-PA-03 | Confirm live-host proof uses concrete runtime, scene, and contract anchors. | This file must contain KernelLiveBootOrchestrator.cs, KernelRuntimeShell.cs, 02_KernelV22KernelOnlyHostSpec.md, TitleScene.unity, and GameScene.unity. |
| TC-V22-PA-04 | Confirm command/value host-removal proof uses explicit contract and source anchors. | This file must contain 02_1_KernelV22CommandAndValueHostRemovalSpec.md, CommandRunnerMB.cs, BlackboardMB.cs, and VerifiedCommandRuntimeBridge.cs. |
| TC-V22-PA-05 | Confirm family-cutover proof uses Boot and Scene Flow plus first-channel anchors. | This file must contain SceneFlowInstallerMB.cs, SceneService.cs, LoadingScreenService.cs, ModalStackChannelHubService.cs, and TooltipChannelHubService.cs. |
| TC-V22-PA-06 | Confirm representative gameplay/application proof has an explicit contract anchor and remains distinct from M3 family proof. | This file must contain 03_1_KernelV22RepresentativeGameplayAndApplicationCutoverSpec.md, GameStateMachineExecutors.cs, and StatusEffectService.cs and state that gameplay/application proof may not be used to close M3. |
| TC-V22-PA-07 | Confirm representative gameplay/application proof requires GameScene anchors and traceability. | This file must contain Scenes/GameScene/GameScene.unity, CommandExecutionTrace.cs, DynamicRuntimeLogUtility.cs, TargetChannelHubService.cs, and AreaChannelHubService.cs. |
| TC-V22-PA-08 | Confirm release hardening proof uses explicit contract, residue, and gate anchors. | This file must contain 04_KernelV22LegacyDeletionAndHardeningSpec.md, ScopeFeatureInstallerUtility.cs, RuntimeLifetimeScope.cs, LegacyCompatBoundaryTests.cs, and KernelDiagnosticsAsmdefBoundaryTests.cs. |
| TC-V22-PA-09 | Confirm M6 final proof is not collapsed into M5 hardening proof. | Proof Family Rules must state that M5 release hardening proof does not by itself close M6. |
| TC-V22-PA-10 | Confirm live-host proof treats direct play only as a convergence reference. | This file must contain AuthoringBridgeDirectPlayTests.cs and state that direct play by itself does not prove accepted live host migration. |
| TC-V22-PA-11 | Confirm M6 full-proof aggregation uses an explicit contract and executable bundle anchors. | This file must contain 04_1_KernelV22FullProofAndReleaseHardeningSpec.md, KernelMinimalBootPlayModeTests.cs, GameStateMachineMigrationTests.cs, and KernelPerformanceAllocationTests.cs. |
| TC-V22-PA-12 | Confirm integration smoke cannot replace lower-layer gates in M6. | Proof Family Rules must state that Integration smoke may not excuse missing Validation, Generation, Diagnostics, PerformanceRule, StaticRule, or LegacyCompat gates. |
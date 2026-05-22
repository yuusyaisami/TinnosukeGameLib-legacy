# Kernel v2.2 Implementation Plan

## Document Status

- Document ID: 06_KernelV22ImplementationPlan
- Status: Draft
- Role: translates the v2.2 claimable milestones into executable implementation work packages, validation order, and evidence capture rules for actual runtime migration work
- Depends on:
  - [00_KernelV22CompletionOverviewSpec.md](00_KernelV22CompletionOverviewSpec.md)
  - [01_KernelV22AuthorityAndServiceCensusSpec.md](01_KernelV22AuthorityAndServiceCensusSpec.md)
  - [02_KernelV22KernelOnlyHostSpec.md](02_KernelV22KernelOnlyHostSpec.md)
  - [02_1_KernelV22CommandAndValueHostRemovalSpec.md](02_1_KernelV22CommandAndValueHostRemovalSpec.md)
  - [03_KernelV22ServiceFamilyCutoverSpec.md](03_KernelV22ServiceFamilyCutoverSpec.md)
  - [03_1_KernelV22RepresentativeGameplayAndApplicationCutoverSpec.md](03_1_KernelV22RepresentativeGameplayAndApplicationCutoverSpec.md)
  - [04_KernelV22LegacyDeletionAndHardeningSpec.md](04_KernelV22LegacyDeletionAndHardeningSpec.md)
  - [04_1_KernelV22FullProofAndReleaseHardeningSpec.md](04_1_KernelV22FullProofAndReleaseHardeningSpec.md)
  - [05_KernelV22MilestoneOrderSpec.md](05_KernelV22MilestoneOrderSpec.md)
  - [../v2/15_TestAndValidationSpec.md](../v2/15_TestAndValidationSpec.md)
  - [../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md](../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md)

### Revision Note

This revision creates the missing execution companion for v2.2.

00 through 05 and 04_1 define what completion means and how it is claimed.
That was necessary, but it is still not the same thing as a practical implementation board.

The missing piece was the document that answers the execution questions directly:

```text
Which milestone is active now?
Which code anchors are allowed to move?
Which validation must run immediately after the first real edit?
What evidence must exist before the next milestone begins?
```

06 owns those answers.
It is the implementation companion, not a replacement for the milestone owner specs.

---

## Ownership

This document owns:

- the executable implementation sequence for M0 through M6
- the milestone-by-milestone work-package breakdown used to start runtime changes
- the validation ladder and proof-capture order used during implementation
- the current repository execution constraints that must shape the plan
- the allowed overlap and handoff rules between milestone slices

This document does not own:

- milestone semantics already owned by 00 through 05 and 04_1
- the final release claim itself
- performance budget meaning already owned by [../v2/14_PerformanceBudgetAndRuntimeRulesSpec.md](../v2/14_PerformanceBudgetAndRuntimeRulesSpec.md)
- compile-boundary semantics already owned by [../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md](../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md)
- unrelated repository debt outside the active milestone slice

06 is the execution board.
It must not silently rewrite the completion contract to make implementation easier.

---

## Purpose

This document turns the v2.2 milestones into an implementation plan that can actually be executed.

Core statements:

```text
Claim order and implementation order must agree.

One accepted-path milestone slice should be active at a time.
Each slice must name code anchors, validation anchors, and close evidence before implementation expands.

If a slice cannot be validated narrowly,
the slice is too large.

M0 is frozen input.
It should reopen only when a later milestone discovers a real claim-surface inconsistency.
```

---

## Scope

This document defines:

- the current execution constraints for this repository
- the validation ladder for runtime migration work
- the global milestone execution board for M0 through M6
- the work-package breakdown, primary anchors, first validation, and close evidence for each milestone
- the handoff rules between milestones during implementation

---

## Non-Goals

This document does not define:

- a new semantic contract for any milestone
- a second backlog for unrelated cleanup
- a promise that multiple accepted-path milestones should be edited freely in one change set
- a replacement for the milestone owner specs when reviews ask what completion means

06 exists to make implementation executable.
It does not change what completion means.

---

## Relationship to Other Specs

| Spec | Relationship to 06 |
| --- | --- |
| [05_KernelV22MilestoneOrderSpec.md](05_KernelV22MilestoneOrderSpec.md) | Owns claim order. 06 converts that order into implementation slices and validation cadence. |
| [02_KernelV22KernelOnlyHostSpec.md](02_KernelV22KernelOnlyHostSpec.md) | Owns M1 semantics. 06 defines the order in which live host entry, root demotion, and convergence work should land. |
| [02_1_KernelV22CommandAndValueHostRemovalSpec.md](02_1_KernelV22CommandAndValueHostRemovalSpec.md) | Owns M2 semantics. 06 defines the implementation wave for command/value demotion and declaration split. |
| [03_KernelV22ServiceFamilyCutoverSpec.md](03_KernelV22ServiceFamilyCutoverSpec.md) | Owns M3 semantics. 06 defines the family-by-family landing order and test-first expectations. |
| [03_1_KernelV22RepresentativeGameplayAndApplicationCutoverSpec.md](03_1_KernelV22RepresentativeGameplayAndApplicationCutoverSpec.md) | Owns M4 semantics. 06 defines the representative gameplay/application execution order and required proof captures. |
| [04_KernelV22LegacyDeletionAndHardeningSpec.md](04_KernelV22LegacyDeletionAndHardeningSpec.md) | Owns M5 semantics. 06 defines the residue-removal and boundary-hardening landing order. |
| [04_1_KernelV22FullProofAndReleaseHardeningSpec.md](04_1_KernelV22FullProofAndReleaseHardeningSpec.md) | Owns M6 semantics. 06 defines the order in which the final gate bundle is assembled and validated. |
| [../v2/15_TestAndValidationSpec.md](../v2/15_TestAndValidationSpec.md) | Owns gate taxonomy and acceptance logic. 06 turns that into a concrete implementation-time validation ladder. |

---

## Current Execution Constraints

The implementation plan must respect the current repository and environment constraints.

1. Full builds are not yet a reliable primary signal because unrelated pre-existing repo errors still exist.
2. Focused csproj builds and targeted test slices are therefore the first executable validation layer for milestone work.
3. Unity batch execution in this workspace can false-green if the runner exits without real `TestRunner` output or without writing results. Such runs do not count as a passing gate.
4. The repository already has a partial kernel asmdef split. Compile-boundary work must complete that split rather than describe the repo as if it were still monolithic.
5. Performance and hardening gates already exist in the workspace and should be consumed early rather than treated as end-of-project surprises.

---

## Planning Rules

1. One accepted-path milestone slice should be in progress at a time.
2. One change set should normally close one subphase or one tightly bounded representative anchor set, not multiple milestones at once.
3. Each slice must edit the authority-owning surface rather than a nearby forwarding layer whenever possible.
4. After the first substantive edit in a slice, the next step is focused validation for that same slice.
5. Unrelated blockers should be recorded and isolated, not used as a reason to widen the current milestone.
6. Legacy owners must be deleted or demoted, not wrapped in new convenience fallbacks.
7. M0 remains frozen input unless later implementation proves that the claim surface itself is inconsistent.

---

## Validation Ladder

| Level | Use | Primary anchor or command | Pass rule |
| --- | --- | --- | --- |
| Doc and shape | package or plan drift | [../../Editor/Tests/KernelV22ArchitectureDocTests.cs](../../Editor/Tests/KernelV22ArchitectureDocTests.cs) | doc surfaces remain mutually consistent |
| Focused editor compile | editor-side slice validation | `C:\Program Files\dotnet\dotnet.exe build Assembly-CSharp-Editor.csproj -v minimal` | touched editor-side slice adds no new compile errors |
| Focused runtime compile | runtime slice validation | `C:\Program Files\dotnet\dotnet.exe build Assembly-CSharp.csproj -v minimal` | touched runtime slice adds no new compile errors |
| Targeted EditMode tests | slice-local behavior and authority checks | representative EditMode test files named below per milestone | the touched authority slice remains fail-closed and reviewable |
| Targeted PlayMode or integration smoke | accepted live path and real scene anchor checks | [../../Tests/Integration/PlayMode/KernelMinimalBootPlayModeTests.cs](../../Tests/Integration/PlayMode/KernelMinimalBootPlayModeTests.cs); [../../../Tools/Run-UnityTests.ps1](../../../Tools/Run-UnityTests.ps1) | accepted live or representative scene proof executes with real runner output |
| Hardening and static gates | forbidden patterns, residue, and boundary direction | [../../Editor/Tests/KernelDiagnostics/KernelForbiddenPatternTests.cs](../../Editor/Tests/KernelDiagnostics/KernelForbiddenPatternTests.cs); [../../Editor/Tests/LegacyCompatBoundaryTests.cs](../../Editor/Tests/LegacyCompatBoundaryTests.cs); [../../Editor/Tests/KernelDiagnostics/KernelDiagnosticsAsmdefBoundaryTests.cs](../../Editor/Tests/KernelDiagnostics/KernelDiagnosticsAsmdefBoundaryTests.cs) | hidden legacy repair and boundary inversion remain rejected |
| Performance gate | marker, allocation, and regression enforcement | [../../Editor/Tests/KernelDiagnostics/KernelPerformanceAllocationTests.cs](../../Editor/Tests/KernelDiagnostics/KernelPerformanceAllocationTests.cs); [../../Editor/Tests/KernelDiagnostics/KernelPerformanceRegressionGateTests.cs](../../Editor/Tests/KernelDiagnostics/KernelPerformanceRegressionGateTests.cs); [../../../Tools/Run-M15.4Gate.ps1](../../../Tools/Run-M15.4Gate.ps1) | required marker and allocation rules remain executable and green |

If a Unity batch run exits without real runner evidence, treat that as an execution failure, not a pass.

---

## Global Milestone Execution Board

| Milestone | Execution posture | Primary implementation surfaces | First focused validation | Close only when |
| --- | --- | --- | --- | --- |
| M0 | frozen input and audit baseline | [01_KernelV22AuthorityAndServiceCensusSpec.md](01_KernelV22AuthorityAndServiceCensusSpec.md); [Index/README.md](Index/README.md); [Index/KernelV22BaselineLedger.md](Index/KernelV22BaselineLedger.md); [Index/KernelV22ProofAnchorCatalog.md](Index/KernelV22ProofAnchorCatalog.md); [05_KernelV22MilestoneOrderSpec.md](05_KernelV22MilestoneOrderSpec.md) | [../../Editor/Tests/KernelV22ArchitectureDocTests.cs](../../Editor/Tests/KernelV22ArchitectureDocTests.cs) | claim surface is stable enough that runtime implementation can proceed without rediscovery |
| M1 | host-authority cutover | [../../GameLib/Script/Project/System/KernelLiveBoot/KernelLiveBootOrchestrator.cs](../../GameLib/Script/Project/System/KernelLiveBoot/KernelLiveBootOrchestrator.cs); [../../GameLib/Script/Kernel/Boot/KernelRuntimeShell.cs](../../GameLib/Script/Kernel/Boot/KernelRuntimeShell.cs); [../../GameLib/Script/Project/LTS/ProjectLifetimeScope.cs](../../GameLib/Script/Project/LTS/ProjectLifetimeScope.cs); [../../GameLib/Script/Project/Global/LTS/GlobalLifetimeScope.cs](../../GameLib/Script/Project/Global/LTS/GlobalLifetimeScope.cs) | [../../Editor/Tests/KernelBoot/BootValidationTests.cs](../../Editor/Tests/KernelBoot/BootValidationTests.cs); [../../Editor/Tests/KernelBoot/KernelBootManifestTests.cs](../../Editor/Tests/KernelBoot/KernelBootManifestTests.cs) | accepted live host truth is kernel-owned, direct play is only a convergence reference, and boot-boundary project/platform/global host roles stay generic |
| M2 | command and value authority cutover | [../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs); [../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs](../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs); [../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs](../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs) | [../../Editor/Tests/KernelVerifiedCommandRuntimeTests.cs](../../Editor/Tests/KernelVerifiedCommandRuntimeTests.cs); [../../Editor/Tests/BlackboardServiceAuthorityTests.cs](../../Editor/Tests/BlackboardServiceAuthorityTests.cs) | accepted command/value execution no longer routes through scene-facing hosts |
| M3 | boot-scene-flow and first-channel cutover | [../../GameLib/Script/Project/System/SceneFlow/MB/SceneFlowInstallerMB.cs](../../GameLib/Script/Project/System/SceneFlow/MB/SceneFlowInstallerMB.cs); [../../GameLib/Script/Project/System/SceneFlow/SceneManager/Service/SceneService.cs](../../GameLib/Script/Project/System/SceneFlow/SceneManager/Service/SceneService.cs); [../../GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs](../../GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs); [../../GameLib/Script/Project/UI/Core/ModalStackChannel/ModalStackChannelHubService.cs](../../GameLib/Script/Project/UI/Core/ModalStackChannel/ModalStackChannelHubService.cs); [../../GameLib/Script/Project/UI/Core/Tooltip/TooltipChannelHubService.cs](../../GameLib/Script/Project/UI/Core/Tooltip/TooltipChannelHubService.cs); [../../GameLib/Script/Project/UI/Core/Conversation/Channel/ConversationChannelHubService.cs](../../GameLib/Script/Project/UI/Core/Conversation/Channel/ConversationChannelHubService.cs); [../../GameLib/Script/Project/Scene/Channels/Mesh/MeshChannelHubService.cs](../../GameLib/Script/Project/Scene/Channels/Mesh/MeshChannelHubService.cs); [../../GameLib/Script/Project/Scene/Channels/SpriteAnimation/AnimationSpriteHubService.cs](../../GameLib/Script/Project/Scene/Channels/SpriteAnimation/AnimationSpriteHubService.cs); [../../GameLib/Script/Project/UI/TraitListChannel/Service/TraitListChannelRuntime.cs](../../GameLib/Script/Project/UI/TraitListChannel/Service/TraitListChannelRuntime.cs); [../../GameLib/Script/Project/Scene/Channels/GridObjectChannel/GridObjectChannelRuntime.cs](../../GameLib/Script/Project/Scene/Channels/GridObjectChannel/GridObjectChannelRuntime.cs) | [../../Editor/Tests/SceneFlowInstallerMBTests.cs](../../Editor/Tests/SceneFlowInstallerMBTests.cs); [../../Editor/Tests/KernelV21LiveBootTests.cs](../../Editor/Tests/KernelV21LiveBootTests.cs); [../../Editor/Tests/ModalStackChannelHubServiceTests.cs](../../Editor/Tests/ModalStackChannelHubServiceTests.cs); [../../Editor/Tests/MeshChannelMigrationTests.cs](../../Editor/Tests/MeshChannelMigrationTests.cs); [../../Editor/Tests/TooltipChannelMigrationTests.cs](../../Editor/Tests/TooltipChannelMigrationTests.cs); [../../Editor/Tests/AnimationSpriteMigrationTests.cs](../../Editor/Tests/AnimationSpriteMigrationTests.cs); [../../Editor/Tests/ConversationDialogueMigrationTests.cs](../../Editor/Tests/ConversationDialogueMigrationTests.cs); [../../Editor/Tests/GameplayAuthorityRegressionTests.cs](../../Editor/Tests/GameplayAuthorityRegressionTests.cs) | Boot and Scene Flow plus first channel families consume M1 and M2 boundaries without split drift |
| M4 | representative GameScene cutover | [../../Game/Scripts/Flow/Commands/GameStateMachineExecutors.cs](../../Game/Scripts/Flow/Commands/GameStateMachineExecutors.cs); [../../GameLib/Script/Common/Commands/VNext/Executors/UI/ConversationExecutors.cs](../../GameLib/Script/Common/Commands/VNext/Executors/UI/ConversationExecutors.cs); [../../GameLib/Script/Project/UI/TraitListChannel/Service/TraitListChannelRuntime.cs](../../GameLib/Script/Project/UI/TraitListChannel/Service/TraitListChannelRuntime.cs); [../../GameLib/Script/Common/StatusEffect/Runtime/StatusEffectService.cs](../../GameLib/Script/Common/StatusEffect/Runtime/StatusEffectService.cs) | [../../Editor/Tests/GameStateMachineMigrationTests.cs](../../Editor/Tests/GameStateMachineMigrationTests.cs); [../../Editor/Tests/ConversationDialogueMigrationTests.cs](../../Editor/Tests/ConversationDialogueMigrationTests.cs); [../../Editor/Tests/StatusEffectServiceDependencyCaptureTests.cs](../../Editor/Tests/StatusEffectServiceDependencyCaptureTests.cs) | representative gameplay/application proof is green through the real GameScene anchors |
| M5 | deletion and compile-boundary hardening | [../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs](../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs); [../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs); [../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs](../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs); [../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs) | [../../Editor/Tests/LegacyCompatBoundaryTests.cs](../../Editor/Tests/LegacyCompatBoundaryTests.cs); [../../Editor/Tests/KernelDiagnostics/KernelForbiddenPatternTests.cs](../../Editor/Tests/KernelDiagnostics/KernelForbiddenPatternTests.cs); [../../Editor/Tests/KernelDiagnostics/KernelDiagnosticsAsmdefBoundaryTests.cs](../../Editor/Tests/KernelDiagnostics/KernelDiagnosticsAsmdefBoundaryTests.cs) | accepted release execution no longer depends on runtime-capable legacy residue |
| M6 | final gate aggregation and release proof | [04_1_KernelV22FullProofAndReleaseHardeningSpec.md](04_1_KernelV22FullProofAndReleaseHardeningSpec.md); [../../../Tools/Run-UnityTests.ps1](../../../Tools/Run-UnityTests.ps1); [../../../Tools/Run-M15.4Gate.ps1](../../../Tools/Run-M15.4Gate.ps1) | [../../Tests/Integration/PlayMode/KernelMinimalBootPlayModeTests.cs](../../Tests/Integration/PlayMode/KernelMinimalBootPlayModeTests.cs); [../../Editor/Tests/KernelDiagnostics/KernelPerformanceAllocationTests.cs](../../Editor/Tests/KernelDiagnostics/KernelPerformanceAllocationTests.cs) | required gate classes from SpecShape through IntegrationSmoke remain green together |

---

## M0 Execution Posture

M0 is already complete enough to start runtime work.
Treat it as frozen input.

Implementation rule:

- do not reopen M0 for convenience planning changes
- reopen M0 only if later implementation proves that the service census, baseline ledger, proof catalog, and milestone order no longer agree

Primary anchors:

- [01_KernelV22AuthorityAndServiceCensusSpec.md](01_KernelV22AuthorityAndServiceCensusSpec.md)
- [Index/README.md](Index/README.md)
- [Index/KernelV22BaselineLedger.md](Index/KernelV22BaselineLedger.md)
- [Index/KernelV22ProofAnchorCatalog.md](Index/KernelV22ProofAnchorCatalog.md)
- [05_KernelV22MilestoneOrderSpec.md](05_KernelV22MilestoneOrderSpec.md)
- [../../Editor/Tests/KernelV22ArchitectureDocTests.cs](../../Editor/Tests/KernelV22ArchitectureDocTests.cs)

Close evidence:

- later runtime slices no longer need rediscovery of continuity surfaces, delete targets, proof families, or milestone ordering

---

## M1 Execution Plan

### V22-IMP-M1-1 Live Entry Isolation

Primary anchors:

- [../../GameLib/Script/Project/System/KernelLiveBoot/KernelLiveBootOrchestrator.cs](../../GameLib/Script/Project/System/KernelLiveBoot/KernelLiveBootOrchestrator.cs)
- [../../GameLib/Script/Kernel/Boot/KernelRuntimeShell.cs](../../GameLib/Script/Kernel/Boot/KernelRuntimeShell.cs)
- [../../GameLib/Script/Project/LTS/ProjectLifetimeScope.cs](../../GameLib/Script/Project/LTS/ProjectLifetimeScope.cs)
- [../../GameLib/Script/Project/Global/LTS/GlobalLifetimeScope.cs](../../GameLib/Script/Project/Global/LTS/GlobalLifetimeScope.cs)

Required implementation:

- move accepted live entry to verified boot input and kernel-owned shell creation
- demote legacy auto-bootstrap from accepted host truth
- make legacy boot participation explicit failure rather than silent repair
- keep project/platform/global host roles generic at the boot boundary so BundleAsset and Orchestrator do not require concrete ProjectLifetimeScope, PlatformLifetimeScope, or GlobalLifetimeScope types

First focused validation:

- [../../Editor/Tests/KernelBoot/BootValidationTests.cs](../../Editor/Tests/KernelBoot/BootValidationTests.cs)
- [../../Editor/Tests/KernelBoot/KernelBootManifestTests.cs](../../Editor/Tests/KernelBoot/KernelBootManifestTests.cs)
- `C:\Program Files\dotnet\dotnet.exe build Assembly-CSharp-Editor.csproj -v minimal`

### V22-IMP-M1-2 Scene-Root Demotion and Handoff

Primary anchors:

- [../../GameLib/Script/Project/Scene/LTS/SceneLifetimeScope.cs](../../GameLib/Script/Project/Scene/LTS/SceneLifetimeScope.cs)
- [../../GameLib/Script/Project/System/SceneFlow/SceneManager/Service/SceneService.cs](../../GameLib/Script/Project/System/SceneFlow/SceneManager/Service/SceneService.cs)
- [../../GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs](../../GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs)

Required implementation:

- make scene-root participants downstream of shell-owned host state
- keep SceneService and LoadingScreenService as consumers, not host owners

First focused validation:

- [../../Editor/Tests/KernelBoot/KernelBootBoundaryTests.cs](../../Editor/Tests/KernelBoot/KernelBootBoundaryTests.cs)
- [../../Tests/Integration/PlayMode/KernelMinimalBootPlayModeTests.cs](../../Tests/Integration/PlayMode/KernelMinimalBootPlayModeTests.cs)

### V22-IMP-M1-3 Direct-Play Convergence

Primary anchors:

- [../../Editor/KernelBoot/AuthoringBridge.cs](../../Editor/KernelBoot/AuthoringBridge.cs)
- [../../Editor/Tests/KernelBoot/AuthoringBridgeDirectPlayTests.cs](../../Editor/Tests/KernelBoot/AuthoringBridgeDirectPlayTests.cs)

Required implementation:

- keep direct play only as a convergence reference
- reject a second host truth for accepted live runtime

Close evidence:

- accepted live boot is kernel-owned
- direct play no longer hides a different host truth
- no M3 family migration work was used to fake host closure

---

## M2 Execution Plan

### V22-IMP-M2-1 Command Host Demotion

Primary anchors:

- [../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs)
- [../../GameLib/Script/Common/Commands/VNext/Core/VerifiedCommandRuntimeBridge.cs](../../GameLib/Script/Common/Commands/VNext/Core/VerifiedCommandRuntimeBridge.cs)
- [../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs)

Required implementation:

- remove scene-facing bulk command authority from accepted execution
- keep declaration surfaces declaration-only
- route accepted command execution through verified kernel-owned sessions

First focused validation:

- [../../Editor/Tests/KernelVerifiedCommandRuntimeTests.cs](../../Editor/Tests/KernelVerifiedCommandRuntimeTests.cs)
- [../../Editor/Tests/CommandRuntimeLegacyRemovalTests.cs](../../Editor/Tests/CommandRuntimeLegacyRemovalTests.cs)
- [../../Editor/Tests/CommandRunnerMigrationTests.cs](../../Editor/Tests/CommandRunnerMigrationTests.cs)

### V22-IMP-M2-2 Value Host Demotion

Primary anchors:

- [../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs](../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs)
- [../../GameLib/Script/Common/Variables/VarStore/Core/VerifiedValueRuntimeBridge.cs](../../GameLib/Script/Common/Variables/VarStore/Core/VerifiedValueRuntimeBridge.cs)
- [../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs](../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs)
- [../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicEvaluationRuntime.cs](../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicEvaluationRuntime.cs)

Required implementation:

- remove BlackboardMB and BlackboardService fallback from accepted value truth
- keep authoring continuity while moving runtime authority to explicit sessions

First focused validation:

- [../../Editor/Tests/BlackboardMigrationTests.cs](../../Editor/Tests/BlackboardMigrationTests.cs)
- [../../Editor/Tests/BlackboardServiceAuthorityTests.cs](../../Editor/Tests/BlackboardServiceAuthorityTests.cs)
- [../../Editor/Tests/DynamicEvaluationRuntimeTests.cs](../../Editor/Tests/DynamicEvaluationRuntimeTests.cs)

### V22-IMP-M2-3 Declaration-Only Split Closeout

Primary anchors:

- [../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardPayloadProjectionUtility.cs](../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardPayloadProjectionUtility.cs)
- [../../GameLib/Script/Project/Scene/Channels/GridObjectChannel/GridObjectChannelPayloadBuilder.cs](../../GameLib/Script/Project/Scene/Channels/GridObjectChannel/GridObjectChannelPayloadBuilder.cs)
- [../../Editor/Tests/BlackboardPayloadProjectionTests.cs](../../Editor/Tests/BlackboardPayloadProjectionTests.cs)
- [../../Editor/Tests/CommandRunnerMigrationTests.cs](../../Editor/Tests/CommandRunnerMigrationTests.cs)
- [../../Editor/Tests/BlackboardMigrationTests.cs](../../Editor/Tests/BlackboardMigrationTests.cs)

Required implementation:

- reduce shared payload projection to explicit payload-to-command-vars copy semantics
- keep declaration surfaces declaration-only without resolver-driven or blackboard-driven convenience repair
- normalize M2 implementation anchors to actual source files instead of nonexistent split-file assumptions

First focused validation:

- [../../Editor/Tests/BlackboardPayloadProjectionTests.cs](../../Editor/Tests/BlackboardPayloadProjectionTests.cs)
- `C:\Program Files\dotnet\dotnet.exe build Assembly-CSharp.csproj -v minimal`
- `C:\Program Files\dotnet\dotnet.exe build Assembly-CSharp-Editor.csproj -v minimal`

Close evidence:

- shared projection helpers no longer imply blackboard mutation or resolver-driven repair for accepted execution
- declaration-only boundary no longer depends on nonexistent split-file assumptions or scene-facing host repair
- M3 can consume the shared boundary while family-local local-blackboard merge remains deferred to M4 representative slices

---

## M3 Execution Plan

### V22-IMP-M3-1 Boot and Scene Flow Family Cutover

Primary anchors:

- [../../GameLib/Script/Project/System/SceneFlow/MB/SceneFlowInstallerMB.cs](../../GameLib/Script/Project/System/SceneFlow/MB/SceneFlowInstallerMB.cs)
- [../../GameLib/Script/Project/System/SceneFlow/SceneManager/Service/SceneService.cs](../../GameLib/Script/Project/System/SceneFlow/SceneManager/Service/SceneService.cs)
- [../../GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs](../../GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs)

Required implementation:

- cut over boot and scene-flow family consumption onto the M1 and M2 boundaries
- keep installer discovery and hidden host recreation out of the accepted path

First focused validation:

- [../../Editor/Tests/SceneFlowInstallerMBTests.cs](../../Editor/Tests/SceneFlowInstallerMBTests.cs)
- [../../Editor/Tests/KernelV21LiveBootTests.cs](../../Editor/Tests/KernelV21LiveBootTests.cs)
- focused editor compile for the touched slice

### V22-IMP-M3-2 First Channel Family Cutover

Primary anchors:

- [../../GameLib/Script/Project/UI/Core/ModalStackChannel/ModalStackChannelHubService.cs](../../GameLib/Script/Project/UI/Core/ModalStackChannel/ModalStackChannelHubService.cs)
- [../../GameLib/Script/Project/UI/Core/Tooltip/TooltipChannelHubService.cs](../../GameLib/Script/Project/UI/Core/Tooltip/TooltipChannelHubService.cs)
- [../../GameLib/Script/Project/UI/Core/Conversation/Channel/ConversationChannelHubService.cs](../../GameLib/Script/Project/UI/Core/Conversation/Channel/ConversationChannelHubService.cs)
- [../../GameLib/Script/Project/Scene/Channels/Mesh/MeshChannelHubService.cs](../../GameLib/Script/Project/Scene/Channels/Mesh/MeshChannelHubService.cs)
- [../../GameLib/Script/Project/Scene/Channels/SpriteAnimation/AnimationSpriteHubService.cs](../../GameLib/Script/Project/Scene/Channels/SpriteAnimation/AnimationSpriteHubService.cs)
- [../../GameLib/Script/Project/UI/TraitListChannel/Service/TraitListChannelRuntime.cs](../../GameLib/Script/Project/UI/TraitListChannel/Service/TraitListChannelRuntime.cs)
- [../../GameLib/Script/Project/Scene/Channels/GridObjectChannel/GridObjectChannelRuntime.cs](../../GameLib/Script/Project/Scene/Channels/GridObjectChannel/GridObjectChannelRuntime.cs)

Required implementation:

- keep `ModalStackChannelHubService` and `MeshChannelHubService` as coarse-service positive templates without re-fragmenting authority
- harden tooltip, conversation, animation-sprite, trait-list, and grid-object slices so explicit family authority failures do not silently downgrade into mixed-boundary execution
- preserve tag, layer, bind, refresh, and session-facing contracts while removing hidden host, command, or value authority recreation

First focused validation:

- [../../Editor/Tests/ModalStackChannelHubServiceTests.cs](../../Editor/Tests/ModalStackChannelHubServiceTests.cs)
- [../../Editor/Tests/MeshChannelMigrationTests.cs](../../Editor/Tests/MeshChannelMigrationTests.cs)
- [../../Editor/Tests/TooltipChannelMigrationTests.cs](../../Editor/Tests/TooltipChannelMigrationTests.cs)
- [../../Editor/Tests/AnimationSpriteMigrationTests.cs](../../Editor/Tests/AnimationSpriteMigrationTests.cs)
- [../../Editor/Tests/ConversationDialogueMigrationTests.cs](../../Editor/Tests/ConversationDialogueMigrationTests.cs)
- [../../Editor/Tests/GameplayAuthorityRegressionTests.cs](../../Editor/Tests/GameplayAuthorityRegressionTests.cs)

### V22-IMP-M3-3 Split-Required Family Enforcement

Primary anchors:

- [../../GameLib/Script/Project/UI/Core/Tooltip/TooltipChannelHubService.cs](../../GameLib/Script/Project/UI/Core/Tooltip/TooltipChannelHubService.cs)
- [../../GameLib/Script/Project/Scene/Channels/SpriteAnimation/AnimationSpriteHubService.cs](../../GameLib/Script/Project/Scene/Channels/SpriteAnimation/AnimationSpriteHubService.cs)
- [../../GameLib/Script/Project/UI/Core/Conversation/Channel/ConversationChannelHubService.cs](../../GameLib/Script/Project/UI/Core/Conversation/Channel/ConversationChannelHubService.cs)
- [../../GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs](../../GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs)

Required implementation:

- keep `LoadingScreenService`, `TooltipChannelHubService`, and `AnimationSpriteHubService` unclaimable until their mixed-boundary residue is split or explicitly isolated
- reject warning-disable, runtime repair, and implicit authority borrowing as accepted family execution
- do not borrow gameplay/application success to hide unsplit family drift

Close evidence:

- Boot and Scene Flow plus first channel families consume kernel-owned host, command, and value authority
- gameplay/application proof remains deferred to M4

---

## M4 Execution Plan

### V22-IMP-M4-1 GameStateMachine Representative Slice

Primary anchors:

- [../../Game/Scripts/Flow/Commands/GameStateMachineExecutors.cs](../../Game/Scripts/Flow/Commands/GameStateMachineExecutors.cs)
- [../../Game/Scripts/Flow/Commands/GameStateMachineCommandData.cs](../../Game/Scripts/Flow/Commands/GameStateMachineCommandData.cs)

Required implementation:

- remove compatibility traversal from accepted GameStateMachine authority resolution
- keep gameplay-facing state-command meaning stable

First focused validation:

- [../../Editor/Tests/GameStateMachineMigrationTests.cs](../../Editor/Tests/GameStateMachineMigrationTests.cs)

### V22-IMP-M4-2 Conversation and Dialogue Representative Slice

Primary anchors:

- [../../GameLib/Script/Common/Commands/VNext/Executors/UI/ConversationExecutors.cs](../../GameLib/Script/Common/Commands/VNext/Executors/UI/ConversationExecutors.cs)
- [../../GameLib/Script/Project/UI/Core/Conversation/Channel/ConversationChannelHubService.cs](../../GameLib/Script/Project/UI/Core/Conversation/Channel/ConversationChannelHubService.cs)
- [../../GameLib/Script/Project/UI/Core/Dialog/Channel/DialogChannelRuntime.cs](../../GameLib/Script/Project/UI/Core/Dialog/Channel/DialogChannelRuntime.cs)

Required implementation:

- make representative session flow consume migrated command and value authority without scene-facing repair

First focused validation:

- [../../Editor/Tests/ConversationDialogueMigrationTests.cs](../../Editor/Tests/ConversationDialogueMigrationTests.cs)

### V22-IMP-M4-3 Grid and Trait Presentation Representative Slice

Primary anchors:

- [../../GameLib/Script/Project/Scene/Channels/GridObjectChannel/GridObjectChannelRuntime.cs](../../GameLib/Script/Project/Scene/Channels/GridObjectChannel/GridObjectChannelRuntime.cs)
- [../../GameLib/Script/Project/UI/TraitListChannel/Service/TraitListChannelRuntime.cs](../../GameLib/Script/Project/UI/TraitListChannel/Service/TraitListChannelRuntime.cs)

Required implementation:

- remove local blackboard merge and helper-driven scope preparation from accepted presentation truth
- add or extend focused slice tests if current coverage is not enough to prove this boundary explicitly

First focused validation:

- focused runtime or editor compile for the touched slice
- targeted slice tests created or extended for GridObject and TraitList if existing coverage is insufficient

### V22-IMP-M4-4 StatusEffect and Scalar Boundary Representative Slice

Primary anchors:

- [../../GameLib/Script/Common/StatusEffect/Runtime/StatusEffectService.cs](../../GameLib/Script/Common/StatusEffect/Runtime/StatusEffectService.cs)
- [../../GameLib/Script/Common/Commands/VNext/Commands/StatusEffect/StatusEffectCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/StatusEffect/StatusEffectCommandData.cs)

Required implementation:

- keep scalar ownership explicit while removing convenience resolution from accepted gameplay truth

First focused validation:

- [../../Editor/Tests/StatusEffectServiceDependencyCaptureTests.cs](../../Editor/Tests/StatusEffectServiceDependencyCaptureTests.cs)

Close evidence:

- representative gameplay/application proof is green through the real GameScene anchors
- diagnostics traceability is present for representative behavior

---

## M5 Execution Plan

### V22-IMP-M5-1 Scope and Resolver Residue Removal

Primary anchors:

- [../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs](../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs)
- [../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs)
- [../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs](../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs)

Required implementation:

- remove hierarchy-driven installer discovery and legacy container truth from accepted execution

First focused validation:

- [../../Editor/Tests/KernelDiagnostics/KernelForbiddenPatternTests.cs](../../Editor/Tests/KernelDiagnostics/KernelForbiddenPatternTests.cs)
- [../../Editor/Tests/LegacyCompatBoundaryTests.cs](../../Editor/Tests/LegacyCompatBoundaryTests.cs)

### V22-IMP-M5-2 Command, Value, and Registry Residue Removal

Primary anchors:

- [../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs)
- [../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs](../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs)
- [../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs](../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs)
- [../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs)

Required implementation:

- eliminate scene-facing command/value fallback and runtime `Resources.Load` identity repair from accepted release paths

First focused validation:

- [../../Editor/Tests/CommandRuntimeLegacyRemovalTests.cs](../../Editor/Tests/CommandRuntimeLegacyRemovalTests.cs)
- [../../Editor/Tests/BlackboardMigrationTests.cs](../../Editor/Tests/BlackboardMigrationTests.cs)
- [../../Editor/Tests/KernelDiagnostics/KernelForbiddenPatternTests.cs](../../Editor/Tests/KernelDiagnostics/KernelForbiddenPatternTests.cs)

### V22-IMP-M5-3 Representative Helper Residue Removal

Primary anchors:

- [../../Game/Scripts/Flow/Commands/GameStateMachineExecutors.cs](../../Game/Scripts/Flow/Commands/GameStateMachineExecutors.cs)

Required implementation:

- remove helper traversal or compatibility repair that still decides accepted gameplay truth after M4 proof already exists

First focused validation:

- [../../Editor/Tests/GameStateMachineMigrationTests.cs](../../Editor/Tests/GameStateMachineMigrationTests.cs)

### V22-IMP-M5-4 Compile-Boundary and Quarantine Completion

This bounded implementation slice closes the spec-defined M5-6 compile-boundary and package-quarantine work together with the M5-7 hardening-gate and residue-evidence work.
It remains V22-IMP-M5-4 in the execution plan because V22-IMP-M5-1 through V22-IMP-M5-3 already consumed the earlier residue-domain cleanup slices.

Primary anchors:

- [../../GameLib/Script/Kernel/Validation/LegacyMigrationModel.cs](../../GameLib/Script/Kernel/Validation/LegacyMigrationModel.cs)
- [../../GameLib/Script/Kernel/Boot/GameLib.Kernel.Boot.asmdef](../../GameLib/Script/Kernel/Boot/GameLib.Kernel.Boot.asmdef)
- [../../Editor/Tests/KernelDiagnostics/KernelDiagnosticsAsmdefBoundaryTests.cs](../../Editor/Tests/KernelDiagnostics/KernelDiagnosticsAsmdefBoundaryTests.cs)

Required implementation:

- make remaining adapters explicit quarantine only
- enforce kernel-to-legacy and production-to-test directionality against the current partial split

First focused validation:

- [../../Editor/Tests/LegacyCompatBoundaryTests.cs](../../Editor/Tests/LegacyCompatBoundaryTests.cs)
- [../../Editor/Tests/KernelDiagnostics/KernelDiagnosticsAsmdefBoundaryTests.cs](../../Editor/Tests/KernelDiagnostics/KernelDiagnosticsAsmdefBoundaryTests.cs)

Close evidence:

- accepted release execution no longer depends on runtime-capable legacy residue
- remaining quarantine, if any, is explicit, diagnosable, removable, and non-authoritative

---

## M6 Execution Plan

### V22-IMP-M6-1 Final Bundle Map

Primary anchors:

- [04_1_KernelV22FullProofAndReleaseHardeningSpec.md](04_1_KernelV22FullProofAndReleaseHardeningSpec.md)
- [Index/KernelV22ProofAnchorCatalog.md](Index/KernelV22ProofAnchorCatalog.md)

Required implementation:

- add canonical M6 bundle-map rows to [04_1_KernelV22FullProofAndReleaseHardeningSpec.md](04_1_KernelV22FullProofAndReleaseHardeningSpec.md) covering gate class, required proof-family intake, primary executable anchors, current runner or lane anchor, required review output, and failure code family
- synchronize [Index/KernelV22ProofAnchorCatalog.md](Index/KernelV22ProofAnchorCatalog.md) so proof-family separation and gate-class bundle ownership cannot collapse into one interchangeable green signal
- harden [../../Editor/Tests/KernelV22ArchitectureDocTests.cs](../../Editor/Tests/KernelV22ArchitectureDocTests.cs) so bundle-map drift fails closed before later M6 slices start
- do not start final report schema or runner aggregation in this slice

First focused validation:

- [../../Editor/Tests/KernelV22ArchitectureDocTests.cs](../../Editor/Tests/KernelV22ArchitectureDocTests.cs)

### V22-IMP-M6-2 Accepted Live Boot and Convergence Bundle

Primary anchors:

- [../../Editor/Tests/KernelV22LiveBootBundleTests.cs](../../Editor/Tests/KernelV22LiveBootBundleTests.cs)
- [../../Tests/Integration/PlayMode/KernelMinimalBootPlayModeTests.cs](../../Tests/Integration/PlayMode/KernelMinimalBootPlayModeTests.cs)
- [../../Editor/Tests/KernelBoot/AuthoringBridgeDirectPlayTests.cs](../../Editor/Tests/KernelBoot/AuthoringBridgeDirectPlayTests.cs)
- [../../GameLib/Script/Project/System/KernelLiveBoot/KernelLiveBootOrchestrator.cs](../../GameLib/Script/Project/System/KernelLiveBoot/KernelLiveBootOrchestrator.cs)
- [../../GameLib/Script/Project/System/KernelLiveBoot/KernelLiveBootRuntime.cs](../../GameLib/Script/Project/System/KernelLiveBoot/KernelLiveBootRuntime.cs)

Required implementation:

- materialize a v22-owned accepted live-boot bundle fixture around verified boot session state, explicit loading parent, legacy auto-bootstrap quarantine, and pre-scene-handoff rejection
- keep the accepted live-boot bundle separate from representative GameScene proof, which remains a later runtime bundle slice
- keep direct play as convergence reference only

First focused validation:

- [../../Editor/Tests/KernelV22LiveBootBundleTests.cs](../../Editor/Tests/KernelV22LiveBootBundleTests.cs)
- [../../Editor/Tests/KernelBoot/AuthoringBridgeDirectPlayTests.cs](../../Editor/Tests/KernelBoot/AuthoringBridgeDirectPlayTests.cs)

### V22-IMP-M6-3 Representative GameScene Bundle

Primary anchors:

- [../../Editor/Tests/KernelV22RepresentativeGameSceneBundleTests.cs](../../Editor/Tests/KernelV22RepresentativeGameSceneBundleTests.cs)
- [../../Editor/Tests/GameStateMachineMigrationTests.cs](../../Editor/Tests/GameStateMachineMigrationTests.cs)
- [../../Editor/Tests/ConversationDialogueMigrationTests.cs](../../Editor/Tests/ConversationDialogueMigrationTests.cs)
- [../../Editor/Tests/GridObjectAuthorityMigrationTests.cs](../../Editor/Tests/GridObjectAuthorityMigrationTests.cs)
- [../../Editor/Tests/TraitListAuthorityMigrationTests.cs](../../Editor/Tests/TraitListAuthorityMigrationTests.cs)
- [../../Editor/Tests/StatusEffectServiceDependencyCaptureTests.cs](../../Editor/Tests/StatusEffectServiceDependencyCaptureTests.cs)
- [../../Editor/Tests/GameplayAuthorityRegressionTests.cs](../../Editor/Tests/GameplayAuthorityRegressionTests.cs)
- [../../Editor/Tests/KernelDiagnostics/DiagnosticCodeTraceabilityTests.cs](../../Editor/Tests/KernelDiagnostics/DiagnosticCodeTraceabilityTests.cs)
- [../../GameLib/Script/Common/Commands/VNext/Core/CommandExecutionTrace.cs](../../GameLib/Script/Common/Commands/VNext/Core/CommandExecutionTrace.cs)
- [../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicRuntimeLogUtility.cs](../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicRuntimeLogUtility.cs)

Required implementation:

- add a v22-owned representative GameScene bundle fixture that checks the real GameScene anchors and required representative slice coverage
- bundle GameStateMachine, conversation and dialogue, grid and trait presentation, and status-effect proof without promoting gameplay-only appearance to proof
- keep CommandExecutionTrace, DynamicRuntimeLogUtility, and representative diagnostic-code traceability reviewable inside the bundle
- keep diagnostics snapshot, static-rule, legacy-compat, performance evidence, and final report aggregation out of this slice

First focused validation:

- [../../Editor/Tests/KernelV22RepresentativeGameSceneBundleTests.cs](../../Editor/Tests/KernelV22RepresentativeGameSceneBundleTests.cs)
- [../../Editor/Tests/GameStateMachineMigrationTests.cs](../../Editor/Tests/GameStateMachineMigrationTests.cs)
- [../../Editor/Tests/ConversationDialogueMigrationTests.cs](../../Editor/Tests/ConversationDialogueMigrationTests.cs)

### V22-IMP-M6-4 Hardening, Static, Legacy, and Compile-Boundary Consolidation

Primary anchors:

- [../../Editor/Tests/LegacyCompatBoundaryTests.cs](../../Editor/Tests/LegacyCompatBoundaryTests.cs)
- [../../Editor/Tests/KernelDiagnostics/KernelForbiddenPatternTests.cs](../../Editor/Tests/KernelDiagnostics/KernelForbiddenPatternTests.cs)
- [../../Editor/Tests/KernelDiagnostics/KernelDebugGateTests.cs](../../Editor/Tests/KernelDiagnostics/KernelDebugGateTests.cs)
- [../../Editor/Tests/KernelDiagnostics/KernelForbiddenPatternScannerTests.cs](../../Editor/Tests/KernelDiagnostics/KernelForbiddenPatternScannerTests.cs)
- [../../Editor/Tests/KernelDiagnostics/KernelDiagnosticsAsmdefBoundaryTests.cs](../../Editor/Tests/KernelDiagnostics/KernelDiagnosticsAsmdefBoundaryTests.cs)
- [../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs](../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs)
- [../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs)
- [../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs](../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs)
- [../../../Tools/Run-M15.4Gate.ps1](../../../Tools/Run-M15.4Gate.ps1)

Required implementation:

- consume the M5 hardening bundle as explicit M6-4 intake rather than treating compile success as proof
- make static gates cover M5 residue anchors without broadening accepted runtime ownership or collapsing kernel-only gates into all-runtime scanning
- keep legacy adapter metadata, profile bounds, removal visibility, and compile-boundary direction reviewable in the lane output
- do not add final report schema or overall release-decision logic in this slice

First focused validation:

- [../../Editor/Tests/KernelV22ArchitectureDocTests.cs](../../Editor/Tests/KernelV22ArchitectureDocTests.cs)
- [../../Editor/Tests/KernelDiagnostics/KernelForbiddenPatternScannerTests.cs](../../Editor/Tests/KernelDiagnostics/KernelForbiddenPatternScannerTests.cs)
- [../../Editor/Tests/LegacyCompatBoundaryTests.cs](../../Editor/Tests/LegacyCompatBoundaryTests.cs)

### V22-IMP-M6-5 Performance and Report Aggregation

Primary anchors:

- [../../Editor/Tests/KernelDiagnostics/KernelPerformanceAllocationTests.cs](../../Editor/Tests/KernelDiagnostics/KernelPerformanceAllocationTests.cs)
- [../../Editor/Tests/KernelDiagnostics/KernelPerformanceRegressionGateTests.cs](../../Editor/Tests/KernelDiagnostics/KernelPerformanceRegressionGateTests.cs)
- [../../Editor/Tests/KernelDiagnostics/KernelProfilerMarkerTaxonomyTests.cs](../../Editor/Tests/KernelDiagnostics/KernelProfilerMarkerTaxonomyTests.cs)
- [../../Editor/Tests/KernelDiagnostics/Support/KernelPerformanceReport.cs](../../Editor/Tests/KernelDiagnostics/Support/KernelPerformanceReport.cs)
- [../../../Tools/Run-UnityTests.ps1](../../../Tools/Run-UnityTests.ps1)
- [../../../Tools/Run-M15.4Gate.ps1](../../../Tools/Run-M15.4Gate.ps1)

Required implementation:

- aggregate profiler-marker taxonomy and allocation or regression evidence for covered runtime paths
- produce one reviewable report shape that names gate class, profile, anchor, and failure reason
- reject wall-clock-only performance claims and green summaries that omit lower-layer gate failures

First focused validation:

- [../../Editor/Tests/KernelDiagnostics/KernelPerformanceAllocationTests.cs](../../Editor/Tests/KernelDiagnostics/KernelPerformanceAllocationTests.cs)
- [../../Editor/Tests/KernelDiagnostics/KernelPerformanceRegressionGateTests.cs](../../Editor/Tests/KernelDiagnostics/KernelPerformanceRegressionGateTests.cs)

### V22-IMP-M6-6 Final Acceptance Gate

Primary anchors:

- [04_1_KernelV22FullProofAndReleaseHardeningSpec.md](04_1_KernelV22FullProofAndReleaseHardeningSpec.md)
- [../../../Tools/Run-UnityTests.ps1](../../../Tools/Run-UnityTests.ps1)
- [../../../Tools/Run-M15.4Gate.ps1](../../../Tools/Run-M15.4Gate.ps1)
- [../../Tests/Integration/PlayMode/KernelMinimalBootPlayModeTests.cs](../../Tests/Integration/PlayMode/KernelMinimalBootPlayModeTests.cs)

Required implementation:

- decide one final pass or fail result over the full required gate-class bundle
- fail explicitly when any lower-layer gate is missing, skipped, or red
- keep the rule that green integration smoke cannot rescue missing lower-layer evidence

First focused validation:

- [../../../Tools/Run-M15.4Gate.ps1](../../../Tools/Run-M15.4Gate.ps1)

---

## Allowed Overlap and Handoffs

1. Test scaffolding for a later milestone may be prepared early, but accepted-path authority changes still land in milestone order.
2. M3 family refactors may prepare M4 representative work, but M4 does not claim closure until M3 boundaries are green.
3. M5 gates may be introduced early, but deletion claims wait until M4 proof is real.
4. M6 runner or report work may begin before M5 is fully complete, but the final aggregation claim waits for green M5 evidence.

---

## Completion Output per Milestone

Every milestone implementation closeout should capture the following:

- the code anchors that changed
- the focused validation that ran immediately after the first substantive edit
- the final slice-local compile result
- the specific tests or gate scripts that prove the milestone close
- any remaining unrelated blockers explicitly called out as out of scope

This prevents milestone closure from becoming a verbal summary instead of reviewable evidence.

---

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V22-06-01 | Confirm 06 is an implementation companion rather than a new semantic owner. | Ownership and Non-Goals must reject replacing the milestone owner specs. |
| TC-V22-06-02 | Confirm 06 records current repository execution constraints. | Current Execution Constraints must mention focused builds, Unity false-green risk, and the partial asmdef split. |
| TC-V22-06-03 | Confirm 06 defines a validation ladder for actual implementation work. | Validation Ladder must mention focused compile, targeted tests, hardening gates, and performance gates. |
| TC-V22-06-04 | Confirm 06 treats M0 as frozen input. | Purpose or M0 Execution Posture must state that M0 is frozen input. |
| TC-V22-06-05 | Confirm 06 defines a practical execution board for M0 through M6. | Global Milestone Execution Board must contain every milestone. |
| TC-V22-06-06 | Confirm 06 ties M1 and M2 to concrete runtime authority anchors. | This file must contain KernelLiveBootOrchestrator.cs, KernelRuntimeShell.cs, CommandRunnerMB.cs, and BlackboardService.cs. |
| TC-V22-06-07 | Confirm 06 ties M3 and M4 to concrete family and representative slice anchors. | This file must contain SceneFlowInstallerMB.cs, ModalStackChannelHubService.cs, GameStateMachineExecutors.cs, and ConversationExecutors.cs. |
| TC-V22-06-08 | Confirm 06 ties M5 and M6 to hardening and performance anchors. | This file must contain ScopeFeatureInstallerUtility.cs, LegacyCompatBoundaryTests.cs, KernelPerformanceAllocationTests.cs, and Run-UnityTests.ps1. |
| TC-V22-06-09 | Confirm 06 prevents oversized multi-milestone change sets. | Planning Rules or Allowed Overlap and Handoffs must limit accepted-path slices to one milestone at a time. |
| TC-V22-06-10 | Confirm 06 requires reviewable close evidence per milestone. | Completion Output per Milestone must list code anchors, focused validation, compile result, tests or gates, and unrelated blockers. |

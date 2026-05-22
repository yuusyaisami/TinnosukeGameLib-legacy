# Kernel v2.2 Authority and Service Census Specification

## Document Status

- Document ID: 01_KernelV22AuthorityAndServiceCensusSpec
- Status: Draft
- Role: classifies current runtime owners and target service families so v2.2 cutover can replace runtime authority without collapsing into a new monolith
- Depends on:
  - [00_KernelV22CompletionOverviewSpec.md](00_KernelV22CompletionOverviewSpec.md)
  - [../v2/06_ServiceGraphRuntimeSpec.md](../v2/06_ServiceGraphRuntimeSpec.md)
  - [../v2/12_UnityAuthoringBridgeSpec.md](../v2/12_UnityAuthoringBridgeSpec.md)
  - [../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md](../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md)
  - [../v2.1/Index/KernelV21BaselineLedger.md](../v2.1/Index/KernelV21BaselineLedger.md)
- Provides foundation for:
  - [02_KernelV22KernelOnlyHostSpec.md](02_KernelV22KernelOnlyHostSpec.md)
  - [02_1_KernelV22CommandAndValueHostRemovalSpec.md](02_1_KernelV22CommandAndValueHostRemovalSpec.md)
  - [03_KernelV22ServiceFamilyCutoverSpec.md](03_KernelV22ServiceFamilyCutoverSpec.md)
  - [04_KernelV22LegacyDeletionAndHardeningSpec.md](04_KernelV22LegacyDeletionAndHardeningSpec.md)

## Purpose

The purpose of this document is to stop v2.2 from using the word service too loosely.

Every runtime owner that matters to completion must fall into one of five target classes.

## Classification Vocabulary

| Class | Meaning |
| --- | --- |
| KernelCoreAuthority | required kernel-owned runtime authority such as boot session, runtime shell, scope session, or validation-owned runtime coordination |
| KernelManagedFeatureService | coarse-grained feature service allowed to live under Kernel-managed runtime ownership |
| HubOwnedRuntimeObject | runtime object that remains owned by a coarse hub or service and is not promoted to a service identity |
| AuthoringOnlyMonoBehaviour | scene or prefab declaration surface that survives only as authoring/link input |
| DeleteTarget | runtime owner that must disappear from accepted release execution |

## Census Rules

1. ServiceGraph eligibility applies only to KernelManagedFeatureService.
2. HubOwnedRuntimeObject is never promoted to a coarse service merely for convenience.
3. AuthoringOnlyMonoBehaviour may describe runtime structure but may not own runtime truth.
4. DeleteTarget may continue to exist temporarily in the repository during transition, but it may not remain an accepted release authority.

## Initial Family Census

| Family or anchor | Current pressure | Target class | Notes |
| --- | --- | --- | --- |
| KernelRuntimeShell, KernelLiveBootOrchestrator | live-entry and shell continuity | KernelCoreAuthority | root runtime host chain |
| SceneService | coarse shared live service | KernelManagedFeatureService | scene flow family anchor |
| LoadingScreenService | mixed loading/presentation boundary | KernelManagedFeatureService | split required before final eligibility |
| ModalStackChannelHubService | coarse UI hub | KernelManagedFeatureService | service-candidate template |
| TooltipChannelHubService | hub plus query plus placement mixed boundary | KernelManagedFeatureService | split required before final eligibility |
| MeshChannelHubService | coarse scene channel hub | KernelManagedFeatureService | players remain hub-owned |
| MeshChannelPlayerRuntime | per-player runtime object | HubOwnedRuntimeObject | not a service |
| AnimationSpriteHubService | hub plus material plus lifecycle mixed boundary | KernelManagedFeatureService | split required before final eligibility |
| AnimationSpriteChannelPlayer | per-player runtime object | HubOwnedRuntimeObject | not a service |
| ConversationChannelHubService | coarse conversation scope service | KernelManagedFeatureService | session progression family |
| CommandRunnerAuthoring | serialized declaration surface | AuthoringOnlyMonoBehaviour | default vars and debug viewer authoring only |
| BlackboardAuthoring | serialized declaration surface | AuthoringOnlyMonoBehaviour | init/link data only |
| CommandRunnerMB | legacy bulk command bootstrap | DeleteTarget | accepted runtime command authority must not depend on it |
| BlackboardMB | mixed value/bootstrap host | DeleteTarget | accepted value authority must not depend on it |
| VerifiedCommandRuntimeBridge | verified command session handoff | KernelCoreAuthority | host-side command authority handoff anchor for M2 |
| BlackboardService | legacy value service and fallback owner | DeleteTarget | accepted value authority must not depend on fallback truth |
| DynamicEvaluationRuntime | explicit dynamic evaluation runtime | KernelCoreAuthority | host-side value evaluation authority anchor for M2 |
| RuntimeLifetimeScope | legacy scope/composition authority | DeleteTarget | must not remain accepted runtime authority |
| RuntimeResolverHub | legacy registration resolver | DeleteTarget | must not remain accepted runtime authority |
| GameStateMachineService | representative gameplay coordinator | KernelManagedFeatureService | gameplay/application family anchor |
| StatusEffectService | representative gameplay runtime | KernelManagedFeatureService | gameplay/application family anchor |

## Split Pressure Rules

The following families are explicitly split-required before they may claim stable ServiceGraph eligibility:

- LoadingScreenService
- TooltipChannelHubService
- AnimationSpriteHubService

Representative mixed-boundary pressure must be resolved by moving non-service ownership to RuntimeQuery, lifecycle contributions, or hub-owned runtime objects.

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V22-01-01 | Confirm the five-class vocabulary is fixed. | This file must contain KernelCoreAuthority, KernelManagedFeatureService, HubOwnedRuntimeObject, AuthoringOnlyMonoBehaviour, and DeleteTarget. |
| TC-V22-01-02 | Confirm service census keeps players out of service identity. | The census must classify MeshChannelPlayerRuntime and AnimationSpriteChannelPlayer as HubOwnedRuntimeObject. |
| TC-V22-01-03 | Confirm authoring and runtime owners are split. | The census must classify CommandRunnerAuthoring and BlackboardAuthoring differently from CommandRunnerMB and BlackboardMB. |
| TC-V22-01-04 | Confirm legacy scope and resolver hosts are explicit delete targets. | The census must mention RuntimeLifetimeScope and RuntimeResolverHub as DeleteTarget. |
| TC-V22-01-05 | Confirm representative gameplay anchors are included. | The census must mention GameStateMachineService and StatusEffectService. |
| TC-V22-01-06 | Confirm M2 handoff anchors are visible in the census. | The census must mention VerifiedCommandRuntimeBridge, BlackboardService, and DynamicEvaluationRuntime. |
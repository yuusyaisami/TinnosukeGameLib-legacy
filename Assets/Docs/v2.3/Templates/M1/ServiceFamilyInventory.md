# ServiceFamilyInventory table

Source Spec: [06_KernelV23M1SpecLockAndCensusExecutionSpec.md](../../06_KernelV23M1SpecLockAndCensusExecutionSpec.md)
Execution Step: M1.4 Service Family Inventory Freeze
Artifact Owner: Copilot Draft (handoff required)
Last Updated: 2026-05-23
Approval State: Draft (Ready for reviewer review)

## Inventory Records

| ServiceFamilyName | CurrentAuthorityPath | TargetServiceForm | MigrationOwner | NameContinuityRisk | ReferenceContinuityRisk | PlannedDeletePoint |
| --- | --- | --- | --- | --- | --- | --- |
| Scope Identity and Registry | `KernelScopeHost.ConfigureCore` + `BaseLifetimeScopeRegistry` resolver-traversal path | Scope-ServiceInstance | Core Runtime Owner | low | medium | M5.2 |
| Command Runtime Registration (`CommandRunnerAuthoring` bridge) | `RuntimeScopeContributionBridge.InstallAcceptedAuthoringContributions` | Scope-ServiceInstance | Commands Owner | medium | medium | M5.2 |
| Blackboard Runtime Registration (`BlackboardAuthoring` bridge) | `RuntimeScopeContributionBridge.InstallAcceptedAuthoringContributions` | Scope-ServiceInstance | Vars and Blackboard Owner | medium | medium | M5.2 |
| Scene Flow (`SceneFlowInstallerMB`, `SceneService`, `LoadingScreenService`) | `SceneFlowInstallerMB.InstallSceneFlowRuntime` + scene-root runtime registration | Scope-ServiceInstance | Scene Flow Owner | high | high | M5.5 |
| Runtime Spawner and Pool (`RuntimeLifetimeScopePool`, spawner service) | `RuntimeManagerMB.InstallRuntimeManagerRuntime` | Scope-ServiceInstance | Runtime Spawn Owner | medium | high | M5.2 |
| Channel Scroll Runtime | Scope resolver-driven lookup (`ScrollChannelHubService.ResolveVars/ResolveRegistry/ResolveRunner`) | AoS | Scene Channels Owner | medium | medium | M5.2 |
| Chunk Runtime (`ChunkFactoryService`) | Scope resolver coupling (`scopeNode.Resolver.TryResolve<IChunkAdapter>`) | Scope-ServiceInstance | Chunk Systems Owner | medium | medium | M5.2 |
| Transform Channel Runtime | Scope resolver + nearest-scope resolution (`TransformChannelHubService`, `TransformFollowService`) | AoS | Transform Runtime Owner | medium | medium | M5.2 |
| AutoSpawn Runtime | Scope resolver and spawner registry coupling (`AutoSpawnChannelHubService`) | AoS | AutoSpawn Owner | medium | medium | M5.2 |
| Selection and Pointer Bridge Runtime | MB callback + resolver lookup (`SelectableRuntimeMB`, `WorldPointerTargetMB`, bridge services) | Scope-ServiceInstance | Interaction Runtime Owner | high | high | M5.5 |
| Map Node Runtime | Resolver-coupled node instance updates (`MapNodePlayerService`) | Scope-ServiceInstance | Map Runtime Owner | medium | medium | M5.2 |
| Background Runtime | Spawned lifetime handle + scope-resolver attached element lifecycle | AoS | Background Runtime Owner | low | medium | M5.2 |
| Root Scope Host Services (`ProjectRootScopeServicesMB`, `SceneRootScopeServicesMB`) | Root host contribution bridge + root registration | Scope-ServiceInstance | Root Integration Owner | medium | medium | M5.2 |
| Legacy LTS LifetimeScope Family | `LifetimeScope.Configure` class set under `Assets/GameLib/Script/**/LTS/` | Scope-ServiceInstance (through compatibility bridge only) | Legacy Migration Owner | high | high | M5.2 |

## Inventory Summary

- Total families: 14
- Target AoS: 4
- Target Scope-ServiceInstance: 10
- High continuity-risk families: 3

## Exit Check (M1.4)

- Every known service family has inventory record: [x]
- Every family has MigrationOwner: [x]
- Every family has TargetServiceForm: [x]
- PlannedDeletePoint assigned: [x]

## Reviewer Sign-off

- Reviewer:
- Review date:
- Decision: Approve / Reject / Conditional
- Notes:

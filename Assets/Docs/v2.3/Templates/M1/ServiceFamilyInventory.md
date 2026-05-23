# ServiceFamilyInventory テーブル

Source Spec: [06_KernelV23M1SpecLockAndCensusExecutionSpec.md](../../06_KernelV23M1SpecLockAndCensusExecutionSpec.md)
実行 Step: M1.4 サービス Family 在庫 Freeze
成果物担当: Copilot 下書き (handoff 必須)
最終更新日: 2026-05-23
承認状態: 下書き (Ready for reviewer review)

## 在庫レコード

| ServiceFamilyName | CurrentAuthorityPath | TargetServiceForm | MigrationOwner | NameContinuityRisk | ReferenceContinuityRisk | PlannedDeletePoint |
| --- | --- | --- | --- | --- | --- | --- |
| 範囲 Identity and Registry | `KernelScopeHost.ConfigureCore` + `BaseLifetimeScopeRegistry` resolver-traversal path | 範囲-ServiceInstance | Core 実行時 担当 | low | medium | M5.2 |
| Command 実行時 Registration (`CommandRunnerAuthoring` bridge) | `RuntimeScopeContributionBridge.InstallAcceptedAuthoringContributions` | 範囲-ServiceInstance | Commands 担当 | medium | medium | M5.2 |
| Blackboard 実行時 Registration (`BlackboardAuthoring` bridge) | `RuntimeScopeContributionBridge.InstallAcceptedAuthoringContributions` | 範囲-ServiceInstance | Vars and Blackboard 担当 | medium | medium | M5.2 |
| Scene Flow (`SceneFlowInstallerMB`, `SceneService`, `LoadingScreenService`) | `SceneFlowInstallerMB.InstallSceneFlowRuntime` + scene-root 実行時 registration | 範囲-ServiceInstance | Scene Flow 担当 | high | high | M5.5 |
| 実行時 Spawner and Pool (`RuntimeLifetimeScopePool`, spawner サービス) | `RuntimeManagerMB.InstallRuntimeManagerRuntime` | 範囲-ServiceInstance | 実行時 Spawn 担当 | medium | high | M5.2 |
| Channel Scroll 実行時 | 範囲 resolver-driven lookup (`ScrollChannelHubService.ResolveVars/ResolveRegistry/ResolveRunner`) | AoS | Scene Channels 担当 | medium | medium | M5.2 |
| Chunk 実行時 (`ChunkFactoryService`) | 範囲 resolver coupling (`scopeNode.Resolver.TryResolve<IChunkAdapter>`) | 範囲-ServiceInstance | Chunk Systems 担当 | medium | medium | M5.2 |
| Transform Channel 実行時 | 範囲 resolver + nearest-スコープ resolution (`TransformChannelHubService`, `TransformFollowService`) | AoS | Transform 実行時 担当 | medium | medium | M5.2 |
| AutoSpawn 実行時 | 範囲 resolver and spawner registry coupling (`AutoSpawnChannelHubService`) | AoS | AutoSpawn 担当 | medium | medium | M5.2 |
| Selection and Pointer Bridge 実行時 | MB callback + resolver lookup (`SelectableRuntimeMB`, `WorldPointerTargetMB`, bridge services) | 範囲-ServiceInstance | Interaction 実行時 担当 | high | high | M5.5 |
| Map Node 実行時 | Resolver-coupled node instance updates (`MapNodePlayerService`) | 範囲-ServiceInstance | Map 実行時 担当 | medium | medium | M5.2 |
| Background 実行時 | Spawned lifetime handle + スコープ-resolver attached element lifecycle | AoS | Background 実行時 担当 | low | medium | M5.2 |
| Root 範囲 Host Services (`ProjectRootScopeServicesMB`, `SceneRootScopeServicesMB`) | Root host contribution bridge + root registration | 範囲-ServiceInstance | Root Integration 担当 | medium | medium | M5.2 |
| Legacy LTS LifetimeScope Family | `LifetimeScope.Configure` class set under `Assets/GameLib/Script/**/LTS/` | 範囲-ServiceInstance (through 互換 bridge only) | Legacy 移行 担当 | high | high | M5.2 |

## 在庫サマリー

- Total families: 14
- Target AoS: 4
- Target 範囲-ServiceInstance: 10
- High continuity-risk families: 3

## 完了チェック（M1.4）

- Every known サービスファミリー has 在庫 レコード: [x]
- Every family has MigrationOwner: [x]
- Every family has TargetServiceForm: [x]
- PlannedDeletePoint assigned: [x]

## レビュー承認

- Reviewer:
- Review date:
- Decision: Approve / 拒否 / Conditional
- Notes:





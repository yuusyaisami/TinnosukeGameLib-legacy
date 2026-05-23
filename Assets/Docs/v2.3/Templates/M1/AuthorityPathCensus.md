# AuthorityPathCensus テーブル

Source Spec: [06_KernelV23M1SpecLockAndCensusExecutionSpec.md](../../06_KernelV23M1SpecLockAndCensusExecutionSpec.md)
実行 Step: M1.2 Authority Path Census
成果物担当: Copilot 下書き (handoff 必須)
最終更新日: 2026-05-23
承認状態: 下書き (Ready for reviewer approval)

## 台帳レコード

| PathId | SourceAnchor | CurrentOwnerClass | LegacyAuthorityResidueFlag | 証拠 |
| --- | --- | --- | --- | --- |
| AP-001 | Assets/GameLib/Script/Common/範囲/実行時/RuntimeLifetimeScope.cs :: KernelScopeHost.Build() | mixed | yes | Call path: EnsureScopeBuilt -> Build -> RuntimeContainerBuilder -> builder.Build(). 範囲 host performs local resolver construction. |
| AP-002 | Assets/GameLib/Script/Common/範囲/実行時/RuntimeLifetimeScope.cs :: KernelScopeHost.ConfigureCore(IRuntimeContainerBuilder) | スコープ-owned | yes | Per-スコープ local registrations (identity/node/tick/実行時 handlers) are pushed from スコープ host into local builder. |
| AP-003 | Assets/GameLib/Script/Common/範囲/実行時/RuntimeLifetimeScope.cs :: KernelScopeHost.InstallLocalFeatures(IRuntimeContainerBuilder) | スコープ-owned | yes | 実行時 component scan: GetComponents + IScopeInstaller projection. This is local installer discovery. |
| AP-004 | Assets/GameLib/Script/Common/範囲/実行時/RuntimeLifetimeScope.cs :: RuntimeScopeContributionBridge.ThrowIfVerifiedRuntimeWouldUseLegacyInstallerProjection | mixed | no | Guard path rejects 旧系 installer projection when VerifiedCompositionRuntime is active. |
| AP-005 | Assets/GameLib/Script/Common/範囲/実行時/RuntimeLifetimeScope.cs :: RuntimeScopeContributionBridge.InstallAcceptedAuthoringContributions | mixed | no | Deterministic accepted authoring bridge (CommandRunnerAuthoring/BlackboardAuthoring/SceneFlowInstallerMB). |
| AP-006 | Assets/GameLib/Script/Common/範囲/実行時/RuntimeLifetimeScope.cs :: RuntimeScopeContributionBridge.InstallVerifiedCompositionContributions | mixed | no | Verified contribution host path (explicit contribution bridge), no broad 実行時 installer discovery. |
| AP-007 | Assets/GameLib/Script/Common/範囲/実行時/RuntimeLifetimeScope.cs :: KernelScopeHost.GetParentCached/ResolveParentCore | スコープ-owned | yes | Transform-parent traversal used when verified composition inactive. Discovery-based parent inference remains. |
| AP-008 | Assets/GameLib/Script/Common/範囲/実行時/RuntimeLifetimeScope.cs :: KernelScopeHost.TryEnsureScopeRegistryResolved | mixed | yes | Parent resolver traversal + static cache フォールバック to locate IBaseLifetimeScopeRegistry. Dynamic 実行時 lookup remains. |
| AP-009 | Assets/GameLib/Script/Project/Scene/Spawner/BaseLTS/BaseLifetimeScopeSpawner.cs :: BaseLifetimeScopeSpawner.SpawnAsync | mixed | no | Spawn path drives EnsureScopeBuilt/WhenBuiltAsync/HandleSpawnAsync. Build authority deleゲートd to KernelScopeHost pipeline. |
| AP-010 | Assets/GameLib/Script/Project/Scene/Channels/Scroll/ScrollChannelHubService.cs :: ResolveVars/ResolveRegistry/ResolveRunner | mixed | yes | 実行時 サービス access through スコープ.Resolver.TryResolve at call sites. Indicates resolver-authority coupling in 実行時 leaf サービス logic. |
| AP-011 | Assets/GameLib/Script/Common/LTS/実行時/RuntimeLifetimeScope.cs :: RuntimeLifetimeScope : LifetimeScope.Configure | スコープ-owned | yes | Legacy VContainer LifetimeScope class remains. Local DI authority type still present in codebase. |
| AP-012 | Assets/GameLib/Script/Project/LTS/ProjectLifetimeScope.cs :: ProjectLifetimeScope : LifetimeScope.Configure | スコープ-owned | yes | Legacy root-level local DI authority class present. |
| AP-013 | Assets/GameLib/Script/Project/Global/LTS/GlobalLifetimeScope.cs :: GlobalLifetimeScope : LifetimeScope.Configure | スコープ-owned | yes | Legacy global local DI authority class present. |
| AP-014 | Assets/GameLib/Script/Platform/LTS/PlatformLifetimeScope.cs :: PlatformLifetimeScope : LifetimeScope.Configure | スコープ-owned | yes | Legacy platform local DI authority class present. |
| AP-015 | Assets/GameLib/Script/Project/Scene/LTS/SceneLifetimeScope.cs :: SceneLifetimeScope : LifetimeScope.Configure | スコープ-owned | yes | Legacy scene local DI authority class present. |
| AP-016 | Assets/GameLib/Script/Project/Scene/項目/LTS/FieldLifetimeScope.cs :: FieldLifetimeScope : LifetimeScope.Configure | スコープ-owned | yes | Legacy field local DI authority class present. |
| AP-017 | Assets/GameLib/Script/Project/Scene/項目/Entity/LTS/EntityLifetimeScope.cs :: EntityLifetimeScope : LifetimeScope.Configure | スコープ-owned | yes | Legacy entity local DI authority class present. |

## 台帳サマリー

- Total paths: 17
- kernel-owned: 0
- スコープ-owned: 10
- mixed: 7
- unknown: 0
- Legacy authority residue flagged: 13

## M2 進入ブロック候補

- AP-003 local installer discovery via IScopeInstaller projection
- AP-007 transform-parent discovery path for スコープ parent inference
- AP-011 through AP-017 旧系 LifetimeScope class set

## 完了チェック（M1.2）

- Accepted-path 実行権限 edges enumerated: [x]
- Source anchors recorded for each edge: [x]
- 担当 class assigned (kernel/スコープ/mixed/unknown): [x]
- No unknown 担当 class remaining in this census: [x]

## レビュー承認

- Reviewer:
- Review date:
- Decision: Approve / 拒否 / Conditional
- Notes:





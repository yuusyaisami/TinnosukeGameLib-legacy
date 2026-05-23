# MBResponsibilityClassification テーブル

Source Spec: [06_KernelV23M1SpecLockAndCensusExecutionSpec.md](../../06_KernelV23M1SpecLockAndCensusExecutionSpec.md)
実行 Step: M1.3 MB Responsibility 分類
成果物担当: Copilot 下書き (handoff 必須)
最終更新日: 2026-05-23
承認状態: 下書き (Ready for reviewer approval)

## 分類レコード

| MBFamilyName | CurrentResponsibilityClass | TargetResponsibilityClass | RequiredAction | BreakRisk | EvidenceAnchor |
| --- | --- | --- | --- | --- | --- |
| Legacy LTS LifetimeScope family (`RuntimeLifetimeScope`, `ProjectLifetimeScope`, `GlobalLifetimeScope`, `PlatformLifetimeScope`, `SceneLifetimeScope`, `FieldLifetimeScope`, `EntityLifetimeScope`) | 実行時-authority residue | 宣言-only | remove | high | `Assets/GameLib/Script/**/LTS/*LifetimeScope.cs` (`LifetimeScope : Configure(IContainerBuilder)`) |
| KernelScopeHost family (`KernelScopeHost` and derived スコープ MBs) | mixed | 宣言-only | convert | high | `Assets/GameLib/Script/Common/範囲/実行時/RuntimeLifetimeScope.cs` (`Build`, `ConfigureCore`, `InstallLocalFeatures`, `builder.Build`) |
| RuntimeLifetimeScope 互換 shell MB (`RuntimeLifetimeScope : KernelScopeHost`) | mixed | 宣言-only | convert | medium | `Assets/GameLib/Script/Common/範囲/実行時/RuntimeLifetimeScope.cs` (互換 shell + inherited build authority) |
| Verified installer contribution host family (`IVerifiedInstallerContributionHost` implementers) | mixed | 宣言-only | convert | medium | `EntityInstallerContributionHostMB.InstallVerifiedInstallerContributions`, `FieldInstallerContributionHostMB.InstallVerifiedInstallerContributions` |
| Local installer projection MB family (`IScopeInstaller` implementers) | 実行時-authority residue | 宣言-only | convert | high | `KernelScopeHost.InstallLocalFeatures` 実行時 `GetComponents` projection + `installer.InstallScopeServices(...)` |
| Root スコープ host contribution MB family (`ProjectRootScopeServicesMB`, `PlatformRootScopeServicesMB`, `SceneRootScopeServicesMB`) | mixed | 宣言-only | convert | medium | `RuntimeScopeContributionBridge.InstallHostContributions(...)` |
| SceneFlow authoring/installer family (`SceneFlowInstallerMB`) | mixed | 宣言-only | convert | medium | `SceneFlowInstallerMB.InstallSceneFlowRuntime(...)` + `RegisterSceneFlowServices(...)` |
| Selectable 実行時 bridge MB family (`SelectableRuntimeMB`, `WorldPointerTargetMB`, related manager MBs) | mixed | 宣言-only | convert | medium | `NotifyBridgeRefresh/Release` + `スコープ.Resolver.TryResolve(...)` in MB path |
| 範囲 identity authoring MB family (`ScopeIdentityMB`) | mixed | 宣言-only | convert | low | `InstallExplicitInstallerContribution(... ScopeIdentityMB ...)` in verified composition path |
| Visual/実行時 helper MB family with nearest-スコープ lookup (`TryGetNearestScopeNode` callers) | mixed | 宣言-only | convert | medium | `ScopeFeatureInstallerUtility.TryGetScopeNode/TryGetNearestScopeNode` usage from MB 実行時 callbacks |

## 分類サマリー

- Total MB families classified: 10
- 宣言-only (current): 0
- mixed (current): 7
- 実行時-authority residue (current): 3

## 即時ブロック条件（M1.3）

- BC-001: Any 許可経路 MB requiring 実行時 `GetComponents + IScopeInstaller` projection (`InstallLocalFeatures`) remains.
- BC-002: Any 許可経路 MB that still derives from 旧系 `LifetimeScope` remains active.
- BC-003: Any MB 実行時 callback path that requires direct resolver authority lookup to function in accepted path.

## 完了チェック（M1.3）

- 実行時-affecting MB families classified: [x]
- Each family assigned RequiredAction (retain/convert/remove): [x]
- BreakRisk assigned for each family: [x]
- High-risk families identified with block conditions: [x]

## レビュー承認

- Reviewer:
- Review date:
- Decision: Approve / 拒否 / Conditional
- Notes:





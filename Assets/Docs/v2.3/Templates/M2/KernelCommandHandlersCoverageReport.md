# KernelCommandHandlersCoverageReport

Source Spec: [07_KernelV23M2KernelCommandSurfaceExecutionSpec.md](../../07_KernelV23M2KernelCommandSurfaceExecutionSpec.md)
実行 Step: M2.3 Kernel Handler Implementation and Ownership Enforcement
成果物担当: Copilot 下書き (handoff 必須)
最終更新日: 2026-05-23
承認状態: 下書き (Ready for reviewer review)

## カバレッジ規則（M2.3 基準）

- CoveredCommand set is fixed to M2.1 lifecycle commands.
- Handler ownership is kernel-side only; MB and local installer projection are 宣言/contribution input only.
- Any 許可経路 attempt to use local IScopeInstaller projection is treated as authority bypass risk.

## レコード

| HandlerName | CoveredCommand | OwnershipCheck | LegacyBypassRisk | CoverageEvidence |
| --- | --- | --- | --- | --- |
| `ScopeDeclarationSubmitHandler` (current anchor: `RuntimeScopeContributionBridge.InstallHostContributions` + `InstallAcceptedAuthoringContributions`) | `KernelScope.Register` | 宣言 payload enters kernel build pipeline via explicit contribution entry points only; null/invalid inputs rejected by argument checks | medium | `Assets/GameLib/Script/Common/範囲/実行時/RuntimeLifetimeScope.cs`: `InstallHostContributions`, `InstallAcceptedAuthoringContributions` |
| `KernelScopeBuildHandler` (current anchor: `KernelScopeHost.Build`) | `KernelScope.Build` | `builder.SetHostScope(this)` + `DisableHandlerCollectionResolution()` under verified 実行時; guarded contribution path before `builder.Build()` | medium | `Assets/GameLib/Script/Common/範囲/実行時/RuntimeLifetimeScope.cs`: `Build`, `ConfigureCore`, `builder.Build` |
| `KernelScopeActivateHandler` (current anchor: `KernelScopeHost.AcquireIfNeeded`) | `KernelScope.Activate` | activation executes through `RuntimeAcquireReleaseDispatcher.Acquire`; state transition tracked via verified スコープ state update path | low | `Assets/GameLib/Script/Common/範囲/実行時/RuntimeLifetimeScope.cs`: `AcquireIfNeeded`, `TryUpdateVerifiedRuntimeScopeState` |
| `KernelScopeDeactivateHandler` (current anchor: `KernelScopeHost.ReleaseIfNeeded`) | `KernelScope.Deactivate` | deactivation executes through kernel dispatcher release path and tick-handler unregister sequence; no MB-owned direct lifecycle mutation in accepted path | low | `Assets/GameLib/Script/Common/範囲/実行時/RuntimeLifetimeScope.cs`: `ReleaseIfNeeded` |
| `KernelScopeReleaseHandler` (current anchor: `ScopeDespawnCoordinator.DespawnAsync`) | `KernelScope.Release` | release/destroy path is centralized in スコープ coordinator and dispatches lifecycle despawn before object destruction | low | `Assets/GameLib/Script/Common/範囲/実行時/RuntimeLifetimeScope.cs`: `ScopeDespawnCoordinator.DespawnAsync`, `DespawnAsync` |
| `KernelScopeBuildOrderHandler` (current anchor: `ScopeBuildCoordinator`) | `KernelScope.Build` | parent-first coordinated build scheduling prevents child-side autonomous build authority and enforces kernel-side order | medium | `Assets/GameLib/Script/Common/範囲/Core/ScopeBuildCoordinator.cs`: `Register`, `WaitUntilBuiltAsync`, `NotifyBuilt` |
| `AuthorityBypassRejectHandler` (current anchor: `RuntimeScopeContributionBridge.ThrowIfVerifiedRuntimeWouldUseLegacyInstallerProjection`) | `KernelScope.Build` | verified 実行時 blocks unsupported local `IScopeInstaller` projection and throws explicit 失敗 before container build finalization | high | `Assets/GameLib/Script/Common/範囲/実行時/RuntimeLifetimeScope.cs`: `ShouldRejectLegacyFeatureInstallerProjection`, `ThrowIfVerifiedRuntimeWouldUseLegacyInstallerProjection` |

## レビューノート

- Current 証拠 shows handler-equivalent 実行 points are present, but command surface is still coupled to 互換 host methods.
- Highest remaining bypass risk is 旧系 installer projection when verified composition guard is disabled or 旧系 projection allowance is opened.
- M2.4 必須である convert this baseline into explicit rejection matrix with fixed error code mapping.

## ゲートチェック

- Design ownership enforcement 完了: [x]
- Design coverage 完了: [x]
- 実行時 ownership proof 完了: [ ]
- 実行時 coverage proof 完了: [ ]
- 承認済み: [ ]





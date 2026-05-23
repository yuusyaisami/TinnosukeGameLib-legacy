# LeafDomainRuntimePathReplacementReport

Source Spec: [08_KernelV23M3LeafScopeDemotionExecutionSpec.md](../../08_KernelV23M3LeafScopeDemotionExecutionSpec.md)
実行 Step: M3.3 Leaf 実行時 Path Replacement
成果物担当: Copilot 下書き (handoff 必須)
最終更新日: 2026-05-23
承認状態: 下書き (Replacement baseline recorded; implementation rollout pending)

## 置換方針（M3.3）

- ReplacedLegacyPath identifies 許可経路 旧系 実行権限 that 必須である be removed from leaf domains.
- NewKernelPath 必須である point to kernel-owned lifecycle/registration authority (AoS or 範囲-ServiceInstance) aligned with M3.2 design lock.
- `ResidueFlag=yes` means 許可経路 replacement is not yet fully completed for that row.

## 所掌Evidence Format

Each `OwnershipEvidence` value 必須である contain the following parts:

- `DesignAnchor`: corresponding M3-DES id
- `CodeAnchor`: concrete symbol(s) in 実行時 path
- `VerificationLink`: M3.5 negative case id or focused 検証 id
- `ObservedState`: `design-only` / `実行時-verified`

## レコード

| ReplacementId | ReplacedLegacyPath | NewKernelPath | OwnershipEvidence | ResidueFlag |
| --- | --- | --- | --- | --- |
| M3-RPL-001 | Selection/pointer MB callback path using nearest-スコープ resolve + direct resolver lookup (`SelectableRuntimeMB.NotifyBridgeRefresh/Release`) | Declaration-only MB trigger + kernel command lifecycle path for bridge サービス binding (範囲-ServiceInstance) | `DesignAnchor=M3-DES-001; CodeAnchor=SelectableRuntimeMB.TryResolveActorScope + KernelScopeHost.AcquireIfNeeded/ReleaseIfNeeded; VerificationLink=M3-NEG-001 (pending); ObservedState=design-only` | yes |
| M3-RPL-002 | 実行時 spawner/pool initialization via スコープ-scoped registration/build callbacks (`RuntimeManagerMB.InstallRuntimeManagerRuntime`) | Explicit 宣言 mapping to kernel command chain (`KernelScope.Register -> Build -> Activate`) for 実行時 spawner/pool services | `DesignAnchor=M3-DES-002; CodeAnchor=RuntimeManagerMB.InstallRuntimeManagerRuntime + KernelScopeHost.Build; VerificationLink=M3-NEG-002 (pending); ObservedState=design-only` | yes |
| M3-RPL-003 | Chunk 実行時 resolver coupling (`ChunkStreamerMB.InstallScopeServices`, `ChunkFactoryService` resolver dependence) | Kernel-managed 範囲-ServiceInstance for chunk 実行時 factory/streamer with explicit 宣言 mapping | `DesignAnchor=M3-DES-003; CodeAnchor=ChunkStreamerMB.InstallScopeServices; VerificationLink=M3-NEG-003 (pending); ObservedState=design-only` | yes |
| M3-RPL-004 | Scroll channel 実行時 dependency resolution at acquire (`ScrollChannelHubService.OnAcquire` -> `ResolveVars/ResolveRegistry/ResolveRunner`) | AoS target form with command-driven lifecycle and explicit dependency boundary | `DesignAnchor=M3-DES-004; CodeAnchor=ScrollChannelHubService.OnAcquire; VerificationLink=M3-NEG-004 (pending); ObservedState=design-only` | yes |
| M3-RPL-005 | Transform 実行時 フォールバック and ancestor-resolver traversal (`MovementExecutors.TryResolveTransformChannelRuntime`) | AoS transform 実行時 ownership via kernel command 実行 contracts, with フォールバック removal in accepted path | `DesignAnchor=M3-DES-005; CodeAnchor=MovementExecutors.TryResolveTransformChannelRuntime; VerificationLink=M3-NEG-005 (pending); ObservedState=design-only` | yes |
| M3-RPL-006 | AutoSpawn 実行時 hub resolution traversal (`AutoSpawnChannelControlExecutor.TryResolveHub`, `IAutoSpawnChannelHubService`) | AoS autospawn ownership with explicit kernel command-surface lifecycle management | `DesignAnchor=M3-DES-006; CodeAnchor=AutoSpawnChannelControlExecutor.TryResolveHub + IAutoSpawnChannelHubService; VerificationLink=M3-NEG-006 (pending); ObservedState=design-only` | yes |
| M3-RPL-007 | Map node 実行時 resolver-coupled updates (`MapNodePlayerService.TryGetNodeRunner`, `MapNodeVisualizer.BuildRuntimeAsync`) | 範囲-ServiceInstance ownership controlled by kernel lifecycle/command boundary | `DesignAnchor=M3-DES-007; CodeAnchor=MapNodePlayerService.TryGetNodeRunner + MapNodeVisualizer.BuildRuntimeAsync; VerificationLink=M3-NEG-007 (pending); ObservedState=design-only` | yes |
| M3-RPL-008 | Background/slider visual 実行時 lifecycle attached to active スコープ resolver and フォールバック destruction behavior | AoS background 実行時 ownership with explicit kernel-managed spawn/release path | `DesignAnchor=M3-DES-008; CodeAnchor=WorldSpaceSliderVisualBackend.OnAcquire/OnRelease + ScreenSliderVisualBackend.OnAcquire/OnRelease; VerificationLink=M3-NEG-008 (pending); ObservedState=design-only` | yes |

## ゲートチェック

- Design replacement coverage 完了: [x]
- 実行時 replacement verified: [ ]
- Accepted-path replacement 完了: [ ]
- Legacy residue absent: [ ]





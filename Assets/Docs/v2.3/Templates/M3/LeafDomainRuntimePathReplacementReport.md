# LeafDomainRuntimePathReplacementReport

Source Spec: [08_KernelV23M3LeafScopeDemotionExecutionSpec.md](../../08_KernelV23M3LeafScopeDemotionExecutionSpec.md)
Execution Step: M3.3 Leaf Runtime Path Replacement
Artifact Owner: Copilot Draft (handoff required)
Last Updated: 2026-05-23
Approval State: Draft (Replacement baseline recorded; implementation rollout pending)

## Replacement Policy (M3.3)

- ReplacedLegacyPath identifies accepted-path legacy runtime authority that must be removed from leaf domains.
- NewKernelPath must point to kernel-owned lifecycle/registration authority (AoS or Scope-ServiceInstance) aligned with M3.2 design lock.
- `ResidueFlag=yes` means accepted-path replacement is not yet fully completed for that row.

## OwnershipEvidence Format

Each `OwnershipEvidence` value must contain the following parts:

- `DesignAnchor`: corresponding M3-DES id
- `CodeAnchor`: concrete symbol(s) in runtime path
- `VerificationLink`: M3.5 negative case id or focused verification id
- `ObservedState`: `design-only` / `runtime-verified`

## Records

| ReplacementId | ReplacedLegacyPath | NewKernelPath | OwnershipEvidence | ResidueFlag |
| --- | --- | --- | --- | --- |
| M3-RPL-001 | Selection/pointer MB callback path using nearest-scope resolve + direct resolver lookup (`SelectableRuntimeMB.NotifyBridgeRefresh/Release`) | Declaration-only MB trigger + kernel command lifecycle path for bridge service binding (Scope-ServiceInstance) | `DesignAnchor=M3-DES-001; CodeAnchor=SelectableRuntimeMB.TryResolveActorScope + KernelScopeHost.AcquireIfNeeded/ReleaseIfNeeded; VerificationLink=M3-NEG-001 (pending); ObservedState=design-only` | yes |
| M3-RPL-002 | Runtime spawner/pool initialization via scope-scoped registration/build callbacks (`RuntimeManagerMB.InstallRuntimeManagerRuntime`) | Explicit declaration mapping to kernel command chain (`KernelScope.Register -> Build -> Activate`) for runtime spawner/pool services | `DesignAnchor=M3-DES-002; CodeAnchor=RuntimeManagerMB.InstallRuntimeManagerRuntime + KernelScopeHost.Build; VerificationLink=M3-NEG-002 (pending); ObservedState=design-only` | yes |
| M3-RPL-003 | Chunk runtime resolver coupling (`ChunkStreamerMB.InstallScopeServices`, `ChunkFactoryService` resolver dependence) | Kernel-managed Scope-ServiceInstance for chunk runtime factory/streamer with explicit declaration mapping | `DesignAnchor=M3-DES-003; CodeAnchor=ChunkStreamerMB.InstallScopeServices; VerificationLink=M3-NEG-003 (pending); ObservedState=design-only` | yes |
| M3-RPL-004 | Scroll channel runtime dependency resolution at acquire (`ScrollChannelHubService.OnAcquire` -> `ResolveVars/ResolveRegistry/ResolveRunner`) | AoS target form with command-driven lifecycle and explicit dependency boundary | `DesignAnchor=M3-DES-004; CodeAnchor=ScrollChannelHubService.OnAcquire; VerificationLink=M3-NEG-004 (pending); ObservedState=design-only` | yes |
| M3-RPL-005 | Transform runtime fallback and ancestor-resolver traversal (`MovementExecutors.TryResolveTransformChannelRuntime`) | AoS transform runtime ownership via kernel command execution contracts, with fallback removal in accepted path | `DesignAnchor=M3-DES-005; CodeAnchor=MovementExecutors.TryResolveTransformChannelRuntime; VerificationLink=M3-NEG-005 (pending); ObservedState=design-only` | yes |
| M3-RPL-006 | AutoSpawn runtime hub resolution traversal (`AutoSpawnChannelControlExecutor.TryResolveHub`, `IAutoSpawnChannelHubService`) | AoS autospawn ownership with explicit kernel command-surface lifecycle management | `DesignAnchor=M3-DES-006; CodeAnchor=AutoSpawnChannelControlExecutor.TryResolveHub + IAutoSpawnChannelHubService; VerificationLink=M3-NEG-006 (pending); ObservedState=design-only` | yes |
| M3-RPL-007 | Map node runtime resolver-coupled updates (`MapNodePlayerService.TryGetNodeRunner`, `MapNodeVisualizer.BuildRuntimeAsync`) | Scope-ServiceInstance ownership controlled by kernel lifecycle/command boundary | `DesignAnchor=M3-DES-007; CodeAnchor=MapNodePlayerService.TryGetNodeRunner + MapNodeVisualizer.BuildRuntimeAsync; VerificationLink=M3-NEG-007 (pending); ObservedState=design-only` | yes |
| M3-RPL-008 | Background/slider visual runtime lifecycle attached to active scope resolver and fallback destruction behavior | AoS background runtime ownership with explicit kernel-managed spawn/release path | `DesignAnchor=M3-DES-008; CodeAnchor=WorldSpaceSliderVisualBackend.OnAcquire/OnRelease + ScreenSliderVisualBackend.OnAcquire/OnRelease; VerificationLink=M3-NEG-008 (pending); ObservedState=design-only` | yes |

## Gate Check

- Design replacement coverage complete: [x]
- Runtime replacement verified: [ ]
- Accepted-path replacement complete: [ ]
- Legacy residue absent: [ ]

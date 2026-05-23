# LeafDomainServiceCutoverPlan

Source Spec: [08_KernelV23M3LeafScopeDemotionExecutionSpec.md](../../08_KernelV23M3LeafScopeDemotionExecutionSpec.md)
実行 Step: M3.1 Leaf Domain Freeze + M3.2 サービス Cutover Design Lock (下書き)
成果物担当: Copilot 下書き (handoff 必須)
最終更新日: 2026-05-23
承認状態: 下書き (M3.1 freeze 完了; M3.2 design lock drafted)

## 凍結規則（M3.1）

- Leaf-domain スコープ for M3 is limited to entity/ui-element 実行時 families and their direct leaf-実行時 support services.
- Each family 必須である have one 移行担当 and one target サービス form.
- Cutover order is fixed by risk class: `high -> medium -> low`.

## DomainClass 語彙

- `UIElementLeaf`: UI/visual element 実行時 families that 必須である not own スコープ-local 実行権限.
- `EntityLeaf`: gameplay/実行時 entity-facing leaf families.
- `EntityLeafInfrastructure`: entity-facing infrastructural families that create/manage leaf 実行時 entities (e.g. spawn/pool root services).

## レコード

| ServiceFamilyName | DomainClass | MigrationOwner | TargetServiceForm | CutoverWave | RiskClass |
| --- | --- | --- | --- | --- | --- |
| Selection and Pointer Bridge 実行時 | UIElementLeaf | Interaction 実行時 担当 | 範囲-ServiceInstance | M3-W1 | high |
| 実行時 Spawner and Pool | EntityLeafInfrastructure | 実行時 Spawn 担当 | 範囲-ServiceInstance | M3-W1 | high |
| Chunk 実行時 | EntityLeaf | Chunk Systems 担当 | 範囲-ServiceInstance | M3-W2 | medium |
| Channel Scroll 実行時 | UIElementLeaf | Scene Channels 担当 | AoS | M3-W2 | medium |
| Transform Channel 実行時 | UIElementLeaf | Transform 実行時 担当 | AoS | M3-W2 | medium |
| AutoSpawn 実行時 | EntityLeaf | AutoSpawn 担当 | AoS | M3-W2 | medium |
| Map Node 実行時 | EntityLeaf | Map 実行時 担当 | 範囲-ServiceInstance | M3-W2 | medium |
| Background 実行時 | UIElementLeaf | Background 実行時 担当 | AoS | M3-W3 | low |

## 設計ロック項目

状態: M3.2 design lock draft completed. Reviewer approval pending.

Design-lock rules:

- Legacy authority paths in accepted flow 必須である be replaced by kernel command surface or explicit contribution-mapped kernel-owned サービス forms.
- 互換 shell usage is allowed for serialization/reference continuity only.
- Any path that reintroduces local installer discovery, nearest-スコープ inference authority, or direct resolver authority bypass is 拒否.

| FamilyDesignId | LegacyAuthorityPath | TargetKernelPath | CompatibilityShellPlan | RejectCondition |
| --- | --- | --- | --- | --- |
| M3-DES-001 | `SelectableRuntimeMB.NotifyBridgeRefresh/Release` + nearest-スコープ resolve (`TryResolveActorScope`) + resolver lookup of bridge サービス | Selection/pointer bridge binding invoked via kernel-owned lifecycle handlers with 宣言-only MB trigger surface | Keep `SelectableRuntimeMB` and `WorldPointerTargetMB` MonoScript binding for reference continuity; disallow shell-owned サービス registration authority | 拒否 when MB callback path directly resolves 実行権限 in accepted flow or relies on nearest-スコープ inference as ownership truth |
| M3-DES-002 | `RuntimeManagerMB.InstallRuntimeManagerRuntime` scoped registrations and build callbacks for pool/spawner materialization | 実行時 spawner/pool instantiated through explicit 宣言 mapping and kernel lifecycle command route (`Register -> Build -> Activate`) | Keep existing 実行時 manager authoring component as 宣言 source only; keep serialization 項目 unchanged | 拒否 when accepted path uses local `IScopeInstaller` discovery or bypasses kernel command ordering for spawner/pool lifecycle |
| M3-DES-003 | `ChunkStreamerMB.InstallScopeServices` + `ChunkFactoryService` スコープ resolver coupling (`scopeNode.Resolver.TryResolve<IChunkAdapter>`) | Chunk 実行時 factory and streamer ownership moved to kernel-managed 範囲-ServiceInstance path with explicit 宣言 mapping | Preserve chunk authoring 項目/prefab references; 互換 shell can forward 宣言 payload only | 拒否 when chunk 実行時 performs resolver traversal as フォールバック authority or reintroduces local container build path |
| M3-DES-004 | `ScrollChannelHubService.OnAcquire` 実行時 dependency resolution (`ResolveVars`, `ResolveRegistry`, `ResolveRunner`) | Channel scroll 実行時 shifted to AoS target form with command-driven lifecycle and explicit dependency injection boundary | Preserve existing channel authoring assets and tags; shell is read-only for serialization continuity | 拒否 when scroll channel 実行時 obtains mutable authority via direct スコープ resolver path in accepted flow |
| M3-DES-005 | Transform channel 実行時 lookup traversal in command executors (`MovementExecutors.TryResolveTransformChannelRuntime`, parent walk + `TryResolve<ITransformChannelHubService>`) | Transform channel 実行時 (`TransformChannelHubService`) managed through AoS boundary and kernel-owned command 実行 contracts | Keep transform channel tags/options serialization intact; 互換 shell 必須である not own 実行時 state mutation | 拒否 when transform channel 実行 falls back to ancestor resolver discovery as authority source |
| M3-DES-006 | AutoSpawn hub authority resolved through channel executor hub traversal (`AutoSpawnChannelControlExecutor.TryResolveHub`, `IAutoSpawnChannelHubService`) | AutoSpawn 実行時 moved to AoS target form with explicit kernel command-surface lifecycle ownership | Preserve existing autospawn 宣言 and references; 互換 shell forwards 宣言 only | 拒否 when autospawn accepted path uses implicit hub/resolver discovery as 実行権限 source |
| M3-DES-007 | Map node 実行時 resolver-coupled updates (`MapNodePlayerService`, `MapNodeVisualizer` scopeParent resolver usage) | Map node 実行時 moved to kernel-owned 範囲-ServiceInstance route with explicit command/lifecycle control | Preserve map profile/visualization authoring references and 実行時 prefab links; shell non-authoritative | 拒否 when map 実行時 mutates authority through direct resolver coupling outside kernel command path |
| M3-DES-008 | Background/slider visual 実行時 spawned instance lifecycle coupled to active スコープ resolver and release-time フォールバック destruction path | Background 実行時 migrated to AoS target form with kernel lifecycle ownership and explicit spawn/release contracts | Keep visual authoring references and spawned 実行時 binding continuity; shell only for serialization continuity | 拒否 when background 実行時 keeps 許可経路 authority in スコープ-resolver attached 実行時 lifecycle without kernel command mediation |

## ゲートチェック

- All leaf families assigned: [x]
- Design lock draft 完了: [x]
- Design lock anchor quality verified: [x]
- Design lock 承認済み: [ ]





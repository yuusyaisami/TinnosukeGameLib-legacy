# NameReferenceContinuityValidationReport

Source Spec: [08_KernelV23M3LeafScopeDemotionExecutionSpec.md](../../08_KernelV23M3LeafScopeDemotionExecutionSpec.md)
実行 Step: M3.4 Name/Reference Continuity 検証
成果物担当: Copilot 下書き (handoff 必須)
最終更新日: 2026-05-23
承認状態: design-ready (Case definition 完了; 実行時 validation pending)

## 承認状態語彙

- `not-started`: 必須 レコード are missing or not reviewable
- `design-ready`: all 必須 レコード are defined and internally reviewed
- `実行時-verified`: 実行時 証拠 is captured and pass/fail is fixed
- `承認済み`: reviewer sign-off is completed

## 検証方針（M3.4）

- 検証 targets all M3 leaf families and their integration boundaries (サービス name, prefab/scene/script reference, 実行時 binding continuity).
- `PassFail` allowed values: `pass`, `fail`, `pending`, `ブロック`.
- `pending` means case is defined but 実行時 continuity 証拠 has not been captured.
- Any unresolved `fail` or `ブロック` case keeps M3.4 ゲート open.

## 証拠最小項目

Each 実行時 証拠 update 必須である include:

- test run id / 実行 timestamp
- target scene or harness
- observed symbol/reference payload (before/after comparison)
- pass/fail rationale linked to `ValidationCaseId`
- 証拠 source location (log path or capture reference)

## レコード

| ValidationCaseId | BoundaryType | ExpectedContinuity | ObservedContinuity | PassFail | EvidenceAnchor |
| --- | --- | --- | --- | --- | --- |
| M3-CNT-001 | ServiceNameBoundary (Selection/Pointer) | `SelectableRuntimeMB` and `WorldPointerTargetMB` サービス-facing names/bindings remain unchanged while authority path is replaced | pending-実行時-observation | pending | `M3-DES-001`, `M3-RPL-001`, `Assets/GameLib/Script/Project/Scene/SelectableRuntime/MB/SelectableRuntimeMB.cs` |
| M3-CNT-002 | PrefabSceneReferenceBoundary (実行時 Spawner/Pool) | 実行時 manager authoring 項目 and scene/prefab references remain valid after command-surface routing | pending-実行時-observation | pending | `M3-DES-002`, `M3-RPL-002`, `Assets/GameLib/Script/Project/Scene/実行時/RuntimeManager/RuntimeManagerMB.cs` |
| M3-CNT-003 | ServiceNameBoundary (Chunk 実行時) | Chunk サービス and authoring entry names remain stable across スコープ-サービス authority replacement | pending-実行時-observation | pending | `M3-DES-003`, `M3-RPL-003`, `Assets/GameLib/Script/Project/Scene/Chunk/MB/ChunkStreamerMB.cs` |
| M3-CNT-004 | RuntimeBindingBoundary (Scroll Channel) | Scroll channel 実行時 bindings remain functional with AoS target form and no name/reference break | pending-実行時-observation | pending | `M3-DES-004`, `M3-RPL-004`, `Assets/GameLib/Script/Project/Scene/Channels/Scroll/ScrollChannelHubService.cs` |
| M3-CNT-005 | RuntimeBindingBoundary (Transform Channel) | Transform channel tag and 実行時 binding continuity remain intact while ancestor-resolver フォールバック is demoted | pending-実行時-observation | pending | `M3-DES-005`, `M3-RPL-005`, `Assets/GameLib/Script/Common/Commands/VNext/Executors/Movement/MovementExecutors.cs` |
| M3-CNT-006 | RuntimeBindingBoundary (AutoSpawn Channel) | AutoSpawn channel hub references remain valid while authority shifts to AoS command-surface lifecycle | pending-実行時-observation | pending | `M3-DES-006`, `M3-RPL-006`, `Assets/GameLib/Script/Common/Commands/VNext/Executors/Channels/AutoSpawnChannelControlExecutor.cs` |
| M3-CNT-007 | ServiceNameBoundary (Map Node 実行時) | Map node 実行時 APIs and runner-resolution consumer references remain intact during 範囲-ServiceInstance 移行 | pending-実行時-observation | pending | `M3-DES-007`, `M3-RPL-007`, `Assets/GameLib/Script/Project/System/Map/実行時/MapNodePlayerService.cs` |
| M3-CNT-008 | VisualReferenceBoundary (Background/Slider 実行時) | Visual backend references and spawned 実行時 bindings remain intact across AoS ownership 移行 | pending-実行時-observation | pending | `M3-DES-008`, `M3-RPL-008`, `Assets/GameLib/Script/Project/UI/Core/Elements/Slider/Visual/WorldSpaceSliderVisualBackend.cs`, `Assets/GameLib/Script/Project/UI/Core/Elements/Slider/Visual/ScreenSliderVisualBackend.cs` |

## レビューノート

- This artifact is in M3.4 start state: case set is 完了, 実行時 observation is pending.
- Each case is linked to M3 design-lock and replacement レコード for traceability.
- Approval progression target: `design-ready -> 実行時-verified -> 承認済み`.

## ゲートチェック

- Name continuity design coverage 完了: [x]
- Reference continuity design coverage 完了: [x]
- Name continuity verified (実行時): [ ]
- Reference continuity verified (実行時): [ ]
- Continuity validation 承認済み: [ ]





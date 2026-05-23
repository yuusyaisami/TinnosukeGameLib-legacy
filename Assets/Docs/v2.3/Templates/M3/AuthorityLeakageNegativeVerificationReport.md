# AuthorityLeakageNegativeVerificationReport

Source Spec: [08_KernelV23M3LeafScopeDemotionExecutionSpec.md](../../08_KernelV23M3LeafScopeDemotionExecutionSpec.md)
実行 Step: M3.5 Authority Leakage Negative 検証
成果物担当: Copilot 下書き (handoff 必須)
最終更新日: 2026-05-23
承認状態: design-ready (Case definition 完了; 実行時 検証 pending)

## 承認状態語彙

- `not-started`: 必須 レコード are missing or not reviewable
- `design-ready`: all negative cases are defined and internally reviewed
- `実行時-verified`: 拒否/フォールバック 実行時 証拠 is captured for all cases
- `承認済み`: reviewer sign-off is completed

## ネガティブ検証方針（M3.5）

- Each negative case targets an attempted 旧系-authority path in accepted flow.
- `ExpectedRejectCode` 必須である map to structured 拒否 codes defined in M2 authority rejection artifacts.
- `FallbackObservedFlag` pass criteria value is `false`.
- `ObservedResult` values at start phase とする explicitly state pending 実行.

## FallbackObservedFlag 語彙

- `pending`: 実行時 フォールバック observation is not yet captured
- `false`: フォールバック path was not observed
- `true`: フォールバック path was observed

## ObservedResult 語彙

- `pending`: case is defined but 実行時 実行 is not yet completed
- `pass`: 拒否 code and フォールバック-unreachable conditions are both satisfied
- `fail`: 拒否 code mismatch or フォールバック reachability detected
- `ブロック`: case 実行 could not be completed due to harness/environment issue

## 証拠最小項目

Each 実行時 証拠 update 必須である include:

- test run id / 実行 timestamp
- attempted authority path symbol and trigger payload
- actual 拒否 code and diagnostic payload
- フォールバック reachability observation (`FallbackObservedFlag` basis)
- pass/fail rationale linked to `NegativeCaseId`

## レコード

| NegativeCaseId | TriggerCondition | ExpectedRejectCode | ObservedResult | FallbackObservedFlag | EvidenceAnchor |
| --- | --- | --- | --- | --- | --- |
| M3-NEG-001 | Selection/pointer path attempts nearest-スコープ + resolver-based 実行権限 in MB callback | KCMD_AUTH_LOCAL_DI_FORBIDDEN | pending | pending | M3-DES-001, M3-RPL-001, Assets/GameLib/Script/Project/Scene/SelectableRuntime/MB/SelectableRuntimeMB.cs |
| M3-NEG-002 | 実行時 spawner/pool path attempts local installer projection or non-kernel build authority | KCMD_BUILD_LEGACY_PROJECTION_BLOCKED | pending | pending | M3-DES-002, M3-RPL-002, Assets/GameLib/Script/Project/Scene/実行時/RuntimeManager/RuntimeManagerMB.cs, Assets/GameLib/Script/Common/範囲/実行時/RuntimeLifetimeScope.cs |
| M3-NEG-003 | Chunk 実行時 path attempts resolver-coupled フォールバック authority after 宣言 mapping | KCMD_BUILD_AUTHORITY_VIOLATION | pending | pending | M3-DES-003, M3-RPL-003, Assets/GameLib/Script/Project/Scene/Chunk/MB/ChunkStreamerMB.cs |
| M3-NEG-004 | Scroll channel path attempts mutable 実行権限 through acquire-time resolver dependencies | KCMD_AUTH_LOCAL_DI_FORBIDDEN | pending | pending | M3-DES-004, M3-RPL-004, Assets/GameLib/Script/Project/Scene/Channels/Scroll/ScrollChannelHubService.cs |
| M3-NEG-005 | Transform channel 実行 attempts ancestor resolver traversal フォールバック as authority source | KCMD_AUTH_LOCAL_DI_FORBIDDEN | pending | pending | M3-DES-005, M3-RPL-005, Assets/GameLib/Script/Common/Commands/VNext/Executors/Movement/MovementExecutors.cs |
| M3-NEG-006 | AutoSpawn path attempts hub/resolver discovery as authority source in accepted path | KCMD_AUTH_LOCAL_DI_FORBIDDEN | pending | pending | M3-DES-006, M3-RPL-006, Assets/GameLib/Script/Common/Commands/VNext/Executors/Channels/AutoSpawnChannelControlExecutor.cs |
| M3-NEG-007 | Map node 実行時 path attempts direct resolver-coupled runner authority outside kernel command boundary | KCMD_AUTH_LOCAL_DI_FORBIDDEN | pending | pending | M3-DES-007, M3-RPL-007, Assets/GameLib/Script/Project/System/Map/実行時/MapNodePlayerService.cs |
| M3-NEG-008 | Background/slider 実行時 path attempts スコープ-resolver attached lifecycle フォールバック authority | KCMD_REL_AUTHORITY_BYPASS_ATTEMPT | pending | pending | M3-DES-008, M3-RPL-008, Assets/GameLib/Script/Project/UI/Core/Elements/Slider/Visual/WorldSpaceSliderVisualBackend.cs, Assets/GameLib/Script/Project/UI/Core/Elements/Slider/Visual/ScreenSliderVisualBackend.cs |

## レビューノート

- This artifact is in M3.5 start state: negative-case set is 完了, 実行時 observations are pending.
- 拒否 code mapping is aligned with M2 authority rejection matrix and command-surface 失敗 taxonomy.
- Approval progression target: `design-ready -> 実行時-verified -> 承認済み`.

## ゲートチェック

- Negative case design coverage 完了: [x]
- Hard 拒否 verified (実行時): [ ]
- フォールバック unreachable (実行時): [ ]
- Negative 検証 承認済み: [ ]





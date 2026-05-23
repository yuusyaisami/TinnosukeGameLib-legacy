# ObsoleteAuthorityDeletionBoundaryInventory

Source Spec: [10_KernelV23M5HardeningAndDeleteExecutionSpec.md](../../10_KernelV23M5HardeningAndDeleteExecutionSpec.md)
実行 Step: M5.1 Deletion Boundary Freeze
成果物担当: Copilot 下書き (handoff 必須)
最終更新日: 2026-05-23
承認状態: design-ready (Deletion boundary defined; physical delete pending)

## 承認状態語彙

- `not-started`: boundary レコード are missing or not reviewable
- `design-ready`: boundary レコード are 完了 and internally reviewed
- `実行時-verified`: delete 実行 証拠 is captured for all targets
- `承認済み`: reviewer sign-off is completed

## 境界凍結方針（M5.1）

- Targets classified as `delete` 必須である be physically removed in M5.2 (disable-only is not accepted).
- `retain-for-serialization` targets are allowed only for reference continuity and 必須である remain non-authoritative.
- `remove-later` targets require explicit blocking rationale and a bounded follow-up action.
- Any 許可経路 authority still reachable from a non-delete target is treated as 方針 violation.

## レコード

| TargetId | TargetPath | 分類 | MigrationOwner | DeleteWave | ContinuityConstraint |
| --- | --- | --- | --- | --- | --- |
| M5-DEL-001 | Root boot bypass route before plan handshake (`RootBootEntry -> scene-local trigger -> KernelScope.Register/Build/Activate` direct path) | delete | Root Boot 実行時 担当 | M5-W1 | No continuity exception; accepted path 必須である start at verified plan ゲート only |
| M5-DEL-002 | Non-plan registration expansion route (`Register` stage target injection from discovery source) | delete | Root Registration 担当 | M5-W1 | No continuity exception; non-plan registration target path 必須である be unreachable |
| M5-DEL-003 | Ad-hoc build authority route (`KernelScope.Build` invoked from non-kernel 担当 / unregistered handle path) | delete | Root Composition 担当 | M5-W1 | No continuity exception; build path 必須である be kernel-owned only |
| M5-DEL-004 | Activation shortcut route (scene callback timing path bypassing plan-derived order signature) | delete | 実行時 Ordering 担当 | M5-W2 | No continuity exception; deterministic activation order 必須である remain plan-derived |
| M5-DEL-005 | Deactivate/release local フォールバック route (`KernelScope.Deactivate/Release` フォールバック callback authority path) | delete | Lifecycle 実行時 担当 | M5-W2 | No continuity exception; フォールバック reachability 必須である remain false |
| M5-DEL-006 | Post-拒否 continuation route (plan mismatch 拒否 boundary followed by lifecycle continuation) | delete | Plan 検証 担当 | M5-W2 | No continuity exception; 拒否 path 必須である terminate flow fail-closed |
| M5-DEL-007 | 互換 shell serialized member set used only for reference continuity (`serialization-only shell surface`) | retain-for-serialization | 互換 実行時 担当 | M5-W3 | Allowed only for serialized reference continuity; authority behavior flag 必須である stay false |
| M5-DEL-008 | Diagnostic 互換 adapter path for 旧系 tooling (`schema-translation adapter path`) | remove-later | Diagnostics 実行時 担当 | M5-W3 | Temporary retention allowed for tool 互換; 必須である not be reachable in 許可実行経路 |

## remove-later 管理

| TargetId | FollowUpAction | DueBy | ExitCondition |
| --- | --- | --- | --- |
| M5-DEL-008 | Remove diagnostic 互換 adapter after downstream tooling migrates to stable schema contract | 2026-06-30 | adapter path physically deleted and no 許可経路 reference remains |

## レビューノート

- This artifact is in M5.1 start state: deletion boundary is frozen at design level.
- `delete` targets align with M4 cutover/negative 検証 boundaries that identified obsolete authority routes.
- `retain-for-serialization` and `remove-later` rows require strict non-authoritative 実行時 proof in M5.5/M5.3.

## ゲートチェック

- Deletion boundary design lock 完了: [x]
- 分類 完了: [x]
- Deletion boundary 承認済み: [ ]





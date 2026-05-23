# ObsoleteAuthorityDeletionExecutionReport

Source Spec: [10_KernelV23M5HardeningAndDeleteExecutionSpec.md](../../10_KernelV23M5HardeningAndDeleteExecutionSpec.md)
実行 Step: M5.2 Obsolete Authority Path Physical Delete
成果物担当: Copilot 下書き (handoff 必須)
最終更新日: 2026-05-23
承認状態: design-ready (Delete レコード defined; physical delete 実行 pending)

## 承認状態語彙

- `not-started`: deletion レコード are missing or not reviewable
- `design-ready`: deletion レコード are 完了 and internally reviewed
- `実行時-verified`: physical delete 証拠 is captured for all delete targets
- `承認済み`: reviewer sign-off is completed

## 削除実行方針（M5.2）

- Only targets classified as `delete` in M5.1 are processed by this レポート.
- Delete 必須である be physical removal; disable-only handling is invalid.
- Accepted-path references to deleted authority 必須である also be removed.
- Reachability check 必須である prove deleted route is not reachable after delete.

## ReachabilityAfterDelete 語彙

- `pending`: 実行時 reachability check not completed
- `unreachable`: deleted path is not reachable in 許可実行経路
- `reachable`: deleted path is still reachable (失敗)
- `ブロック`: validation 実行 could not 完了 due to harness/environment issue

## ReintroductionRiskFlag 語彙

- `pending`: risk evaluation not completed
- `low`: no practical reintroduction path found under current contracts
- `medium`: potential reintroduction vector exists and requires mitigation
- `high`: active reintroduction vector or regression observed

## RemovedReferenceEvidence 形式

Each レコード 必須である include:

- `BoundaryAnchor`: corresponding M5-DEL target id
- `ReferenceType`: code / 宣言 / scene-authoring / 互換-shim
- `RemovalProof`: removed symbol/path or commit diff reference
- `VerificationLink`: M5.3 or M5.5 validation case id
- `ObservedState`: `design-only` / `実行時-verified`

## レコード

| DeletionRecordId | DeletedTargetPath | RemovedReferenceEvidence | ReachabilityAfterDelete | ReintroductionRiskFlag |
| --- | --- | --- | --- | --- |
| M5-EXE-001 | Root boot scene-local trigger authority bypass path (M5-DEL-001) | `BoundaryAnchor=M5-DEL-001; ReferenceType=code; RemovalProof=pending-delete-proof; VerificationLink=M5-HRD-001 (pending); ObservedState=design-only` | pending | medium |
| M5-EXE-002 | Discovery-sourced root registration target expansion path (M5-DEL-002) | `BoundaryAnchor=M5-DEL-002; ReferenceType=code+宣言; RemovalProof=pending-delete-proof; VerificationLink=M5-HRD-002 (pending); ObservedState=design-only` | pending | high |
| M5-EXE-003 | Non-kernel/ad-hoc build invocation path in root scene flow (M5-DEL-003) | `BoundaryAnchor=M5-DEL-003; ReferenceType=code; RemovalProof=pending-delete-proof; VerificationLink=M5-HRD-003 (pending); ObservedState=design-only` | pending | high |
| M5-EXE-004 | Scene callback-coupled activation ordering shortcut path (M5-DEL-004) | `BoundaryAnchor=M5-DEL-004; ReferenceType=code; RemovalProof=pending-delete-proof; VerificationLink=M5-HRD-004 (pending); ObservedState=design-only` | pending | medium |
| M5-EXE-005 | Local callback フォールバック in deactivation/release lifecycle (M5-DEL-005) | `BoundaryAnchor=M5-DEL-005; ReferenceType=code+互換-shim; RemovalProof=pending-delete-proof; VerificationLink=M5-HRD-005 (pending); ObservedState=design-only` | pending | high |
| M5-EXE-006 | Plan mismatch tolerant continuation path after 拒否 boundary (M5-DEL-006) | `BoundaryAnchor=M5-DEL-006; ReferenceType=code; RemovalProof=pending-delete-proof; VerificationLink=M5-HRD-006 (pending); ObservedState=design-only` | pending | high |

## レビューノート

- This artifact is in M5.2 start state: delete 実行 レコード are defined, 実行時 delete proof is pending.
- レコード are scoped to M5.1 `delete` 分類 targets only.
- `retain-for-serialization` and `remove-later` targets are verified through M5.5/M5.3, not this 実行 レポート.

## ゲートチェック

- Delete 実行 design coverage 完了: [x]
- Physical delete verified (実行時): [ ]
- Reachability after delete verified (実行時): [ ]
- Reintroduction ブロック: [ ]
- Deletion 実行 承認済み: [ ]





# Kernel v2.3 M0 完全移行契約凍結 実行仕様

## 文書状態

- 文書 ID: 05_KernelV23M0FullMigrationContractFreezeExecutionSpec
- 状態: 下書き
- 役割: v2.3 における M0 の実行レベル定義
- 依存先:
  - [00_KernelV23OverviewSpec.md](00_KernelV23OverviewSpec.md)
  - [01_KernelV23ServiceRuntimeModelSpec.md](01_KernelV23ServiceRuntimeModelSpec.md)
  - [02_KernelV23AuthoringRegistrationFlowSpec.md](02_KernelV23AuthoringRegistrationFlowSpec.md)
  - [03_KernelV23MilestoneOrderSpec.md](03_KernelV23MilestoneOrderSpec.md)
  - [04_KernelV23ServiceReconstructionAndCompatibilitySpec.md](04_KernelV23ServiceReconstructionAndCompatibilitySpec.md)

## 目的

M0 は実装規模作業開始前に、v2.3 の非交渉契約を凍結する。

M0 は後続マイルストーンで中核要件を再解釈できない状態を保証する。

## 範囲

M0 の対象範囲:

- completion contract freeze (完全移行 mandatory)
- 互換 contract freeze (name/reference continuity mandatory)
- release rejection trigger freeze (旧系 authority residue is 拒否 condition)

M0 の非対象:

- サービス-by-サービス implementation 移行
- 実行時 command handler coding work

## 入力

- normative requirements from 00/01/02/04
- known 移行 constraints from active 実行経路s

## 出力

- M0 Contract Decision レコード
- M0 Rejection Trigger Matrix
- M0 Invariant List for M1 and M2 entry ゲートs

必須項目:

- M0 Contract Decision レコード:
  - ContractRuleId
  - CanonicalStatement
  - DecisionState
  - DecisionRationale
  - 担当
- M0 Rejection Trigger Matrix:
  - TriggerId
  - TriggerCondition
  - EvidenceRequirement
  - RejectDecisionRule
  - DiagnosticCode
- M0 Invariant List for M1 and M2 entry ゲートs:
  - InvariantId
  - InvariantStatement
  - 範囲
  - VerificationMethod
  - GateBinding

## M0 で凍結する必須不変条件

M0 必須である freeze these invariants as non-overridable:

1. 許可経路 authority invariant
- 許可実行経路 必須である have zero スコープ-local DI 実行権限 at completion

2. サービス model invariant
- only AoS and 範囲-ServiceInstance forms are accepted

3. 互換 invariant
- all サービスファミリー migrations 必須である preserve external サービス identity names
- scene/prefab/script references 必須である remain valid throughout 移行

4. release rejection invariant
- any residual 許可経路 dependency on local DI authority blocks release claim

## 実行手順

### M0.1 Contract Canonicalization

- normalize contract statements from 00/01/02/04 into one canonical glossary
- resolve wording conflicts and alias ambiguity

Deliverable:
- canonical contract glossary

必須項目:
- Term
- CanonicalDefinition
- DeprecatedAliases
- ConflictResolutionNote

### M0.2 Rejection Trigger Definition

- define exact 拒否 triggers for release ゲート
- map each trigger to measurable 証拠

Deliverable:
- rejection trigger matrix

必須項目:
- TriggerId
- TriggerCondition
- EvidenceRequirement
- RejectDecisionRule
- DiagnosticCode

### M0.3 互換 Boundary Lock

- freeze allowed 互換 shell behavior
- freeze disallowed 互換 shell behavior

Deliverable:
- 互換 boundary テーブル

必須項目:
- BoundaryId
- AllowedBehavior
- DisallowedBehavior
- ValidationMethod
- ViolationHandling

### M0.4 Governance Lock

- define who can approve contract changes
- define exceptional change process and 必須 justification

Deliverable:
- governance lock protocol

必須項目:
- GovernanceRuleId
- ApproverRole
- ChangeRequestCondition
- RequiredJustification
- DecisionRecordFormat

## 完了条件

M0 は次の条件をすべて満たした場合のみ完了とする:

- canonical contract glossary 承認済み
- rejection trigger matrix 承認済み
- 互換 boundary テーブル 承認済み
- governance lock protocol 承認済み
- no unresolved contradiction remains across 00/01/02/03/04/05

## 失敗条件

M0 は次のいずれかが発生した場合に失敗とする:

- contract remains interpretable in multiple conflicting ways
- rejection trigger cannot be measured objectively
- 互換 boundary allows 実行権限 leakage

## テストケース

| テストケース | 目的 | 実行注記 |
| --- | --- | --- |
| TC-V23-05-01 | 確認 M0 freezes 完全移行 as non-optional. | Spec 必須である declare 完全移行 as mandatory invariant. |
| TC-V23-05-02 | 確認 M0 freezes 互換 constraints. | 仕様は次を必須とする サービス name and reference continuity. |
| TC-V23-05-03 | 確認 M0 defines objective release rejection triggers. | 仕様は次を定義する measurable 拒否 conditions. |
| TC-V23-05-04 | 確認 M0 governs change authority after freeze. | 仕様は次を定義する governance lock protocol. |






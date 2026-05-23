# Kernel v2.3 M1 仕様ロックおよび台帳化 実行仕様

## 文書状態

- 文書 ID: 06_KernelV23M1SpecLockAndCensusExecutionSpec
- 状態: 下書き
- 役割: v2.3 における M1 および M1.x の実行レベル定義
- 依存先:
  - [03_KernelV23MilestoneOrderSpec.md](03_KernelV23MilestoneOrderSpec.md)
  - [04_KernelV23ServiceReconstructionAndCompatibilitySpec.md](04_KernelV23ServiceReconstructionAndCompatibilitySpec.md)
  - [05_KernelV23M0FullMigrationContractFreezeExecutionSpec.md](05_KernelV23M0FullMigrationContractFreezeExecutionSpec.md)

## 目的

M1 は凍結済み M0 契約を実行可能な移行計画成果物へ変換する。

M1 は許可実行経路に未知の権限所有経路を残してはならない。

## 範囲

M1 の対象範囲:

- M1.1 rule lock 検証
- M1.2 実行権限 census
- M1.3 MB responsibility 分類
- M1.4 サービスファミリー 在庫 freeze
- M1.5 risk and ゲート baseline for M2 entry

M1 の非対象:

- M2 command 面の実装
- 実行切替の実施（M3 以降）

## 必須成果物

M1 は次の成果物をすべて作成しなければならない:

- AuthorityPathCensus テーブル
- MBResponsibilityClassification テーブル
- ServiceFamilyInventory テーブル
- M2EntryGate package
- MigrationRiskRegister

## M1.x 実行詳細

### M1.1 Rule Lock 検証

作業:

- verify normative consistency of 00/01/02/04 against M0 invariants
- resolve unresolved conflicts before census starts

出力:

- RuleLockVerification レポート

必須項目:

- VerificationRuleId
- CheckedSpecSet
- ConflictDetectedFlag
- ResolutionState
- EvidenceAnchor

### M1.2 Authority Path Census

作業:

- enumerate 許可経路 実行権限 edges
- レコード source anchor for each edge (file path, symbol, call chain)
- classify edge authority 担当 (kernel/スコープ/mixed/unknown)

出力:

- AuthorityPathCensus テーブル

必須項目:

- PathId
- SourceAnchor
- CurrentOwnerClass
- LegacyAuthorityResidueFlag
- 証拠

### M1.3 MB Responsibility 分類

作業:

- classify 実行時-affecting MB families
- assign each MB family to 宣言-only/mixed/residue
- define action type per family (retain/convert/remove)

出力:

- MBResponsibilityClassification テーブル

必須項目:

- MBFamilyName
- CurrentResponsibilityClass
- TargetResponsibilityClass
- RequiredAction
- BreakRisk

### M1.4 サービス Family 在庫 Freeze

作業:

- instantiate サービス 在庫 for all サービス families
- map each サービスファミリー to target サービス form
- assign 移行担当 and planned delete point

出力:

- ServiceFamilyInventory テーブル

必須項目:

- ServiceFamilyName
- CurrentAuthorityPath
- TargetServiceForm
- MigrationOwner
- NameContinuityRisk
- ReferenceContinuityRisk
- PlannedDeletePoint

### M1.5 Risk and M2 ゲート Baseline

作業:

- define 移行 blocker taxonomy
- define M2 entry ゲートs from M1 outputs
- define 拒否 triggers for hidden or unclassified 旧系 authority

出力:

- M2EntryGate package
- MigrationRiskRegister

必須項目:

- M2EntryGate package:
  - GateItemId
  - RequiredArtifact
  - PresenceFlag
  - ApprovalState
  - BlockingCondition
- MigrationRiskRegister:
  - RiskId
  - RiskDescription
  - Severity
  - MitigationPlan
  - 担当

## 完了条件

M1 は次の条件をすべて満たした場合のみ完了とする:

- every 許可経路 実行権限 edge is classified
- every 実行時-affecting MB family is classified
- every サービスファミリー has 在庫 レコード and 移行担当
- no unknown 担当 class remains for 許可経路 authority edges
- M2EntryGate package is 承認済み

## 失敗条件

M1 は次のいずれかが発生した場合に失敗とする:

- census coverage is partial or unverifiable
- any サービスファミリー is missing from 在庫
- 担当 class is unknown for any 許可経路 authority edge
- M2 starts before M1 artifacts are 承認済み

## テストケース

| テストケース | 目的 | 実行注記 |
| --- | --- | --- |
| TC-V23-06-01 | 確認 M1 defines all mandatory artifacts. | 仕様は次を必須とする census, MB, サービス 在庫, risk register, and M2 ゲート package. |
| TC-V23-06-02 | 確認 M1.2 requires anchored authority 証拠. | 仕様は次を必須とする source anchors and 証拠 項目. |
| TC-V23-06-03 | 確認 M1.4 enforces full サービス-family coverage. | 仕様は次を禁止する missing サービス レコード. |
| TC-V23-06-04 | 確認 M1 exit blocks unknown authority ownership. | 仕様は次の場合に完了失敗とする unknown 担当 class exists. |
| TC-V23-06-05 | 確認 M1 blocks premature M2 start. | 仕様は次を必須とする 承認済み M2EntryGate package before M2. |





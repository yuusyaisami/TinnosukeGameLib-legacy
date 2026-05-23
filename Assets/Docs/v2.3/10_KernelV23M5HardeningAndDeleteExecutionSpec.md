# Kernel v2.3 M5 ハードニングおよび削除 実行仕様

## 文書状態

- 文書 ID: 10_KernelV23M5HardeningAndDeleteExecutionSpec
- 状態: 下書き
- 役割: v2.3 における M5 および M5.x の実行レベル定義
- 依存先:
  - [03_KernelV23MilestoneOrderSpec.md](03_KernelV23MilestoneOrderSpec.md)
  - [04_KernelV23ServiceReconstructionAndCompatibilitySpec.md](04_KernelV23ServiceReconstructionAndCompatibilitySpec.md)
  - [08_KernelV23M3LeafScopeDemotionExecutionSpec.md](08_KernelV23M3LeafScopeDemotionExecutionSpec.md)
  - [09_KernelV23M4RootSceneIntegrationCutoverExecutionSpec.md](09_KernelV23M4RootSceneIntegrationCutoverExecutionSpec.md)

## 目的

M5 は旧権限経路の削除と実行失敗挙動のハードニングにより移行実行を完了段階へ進める。

M5 は許可実行経路から禁止権限・フォールバック・不透明失敗挙動が排除された場合のみ成功とする。

## 範囲

M5 の対象範囲:

- physical deletion of obsolete スコープ-local DI authority paths
- diagnostics and 失敗 hardening for 実行時 拒否 classes
- performance budget validation after hardening/delete operations
- 互換-shell retirement validation
- M6 entry ゲート 証拠 packaging

M5 の非対象:

- final release proof and claim decision process (M6)

## 非交渉規則

The following are mandatory and non-waivable in M5:

1. deletion completeness rule
- obsolete 許可経路 authority paths でなければならない physically removed, not only disabled

2. explicit 失敗 rule
- 必須 rejection cases 必須である emit structured diagnostics and explicit 失敗 codes

3. no フォールバック rule
- failures 必須である not recover through 旧系 authority or hidden 互換 behavior

4. performance safety rule
- hardening/delete changes 必須である not violate 実行時 hot-path budget constraints

## 必須成果物

M5 は次の成果物をすべて作成しなければならない:

- ObsoleteAuthorityDeletionBoundaryInventory
- ObsoleteAuthorityDeletionExecutionReport
- DiagnosticsFailureHardeningVerificationReport
- PerformanceBudgetValidationReport
- CompatibilityShellRetirementValidationReport
- M6EntryGateEvidencePackage

## M5.x 実行詳細

### M5.1 Deletion Boundary Freeze

作業:

- define final obsolete authority deletion boundary
- classify each target path as delete/retain-for-serialization/remove-later
- assign 担当 and 実行 wave per deletion target

出力:

- ObsoleteAuthorityDeletionBoundaryInventory

必須項目:

- TargetId
- TargetPath
- 分類
- MigrationOwner
- DeleteWave
- ContinuityConstraint

### M5.2 Obsolete Authority Path Physical Delete

作業:

- delete classified obsolete authority targets
- remove 許可経路 references to deleted authority
- verify no 互換 shim reopens deleted route

出力:

- ObsoleteAuthorityDeletionExecutionReport

必須項目:

- DeletionRecordId
- DeletedTargetPath
- RemovedReferenceEvidence
- ReachabilityAfterDelete
- ReintroductionRiskFlag

### M5.3 Diagnostics and 失敗 Hardening

作業:

- define and enforce structured 失敗 codes for 必須 拒否 classes
- verify diagnostics payload completeness for each 拒否 class
- verify no silent フォールバック and no swallow behavior in 許可経路

出力:

- DiagnosticsFailureHardeningVerificationReport

必須項目:

- RejectClass
- FailureCode
- DiagnosticSchema
- FallbackObservedFlag
- EvidenceAnchor

### M5.4 Performance Budget 検証

作業:

- run post-delete/hardening budget checks on 実行時 hot paths
- compare before/after metrics for critical サービス families
- classify and track any budget regressions

出力:

- PerformanceBudgetValidationReport

必須項目:

- BudgetCaseId
- HotPathName
- BaselineMetric
- CurrentMetric
- BudgetPassFail
- RegressionRisk

### M5.5 互換 Shell Retirement 検証

作業:

- 検証する retirement of obsolete 互換 shells
- 検証する retained shells are serialization-only and non-authoritative
- 検証する no reference break after retirement actions

出力:

- CompatibilityShellRetirementValidationReport

必須項目:

- ShellId
- RetirementState
- AuthorityBehaviorFlag
- ReferenceContinuityPassFail
- EvidenceAnchor

### M5.6 M6 Entry ゲート 証拠 Package

作業:

- compose M6 ゲート package from all M5 mandatory artifacts
- document unresolved risks that block release claim
- block M6 start when 証拠 package is incomplete

出力:

- M6EntryGateEvidencePackage

必須項目:

- RequiredArtifact
- PresenceFlag
- ApprovalState
- BlockingCondition

## 完了条件

M5 は次の条件をすべて満たした場合のみ完了とする:

- all mandatory M5 artifacts are present and 承認済み
- 許可実行経路 contains no reachable obsolete authority path
- diagnostics/失敗 hardening 検証 passes with no フォールバック observed
- performance budget validation passes with no unresolved violation
- M6EntryGateEvidencePackage is 承認済み

## 失敗条件

M5 は次のいずれかが発生した場合に失敗とする:

- any obsolete authority path remains reachable in 許可実行経路
- 拒否 class is missing explicit 失敗 code or diagnostics schema
- フォールバック path is observed during hardening 検証
- performance budget has unresolved regression violation
- M6 starts without 承認済み M6EntryGateEvidencePackage

## テストケース

| テストケース | 目的 | 実行注記 |
| --- | --- | --- |
| TC-V23-10-01 | 確認 M5 requires physical deletion of obsolete authority paths. | 仕様は次を必須とする delete 実行 証拠, not disable-only handling. |
| TC-V23-10-02 | 確認 M5 requires explicit 失敗 and diagnostics hardening. | 仕様は次を定義する 拒否 classes with 失敗 codes and schema. |
| TC-V23-10-03 | 確認 M5 forbids フォールバック after hardening/delete. | 仕様は次を必須とする フォールバック-observed checks. |
| TC-V23-10-04 | 確認 M5 requires performance budget validation. | 仕様は次を必須とする hot-path budget pass or explicit unresolved risk block. |
| TC-V23-10-05 | 確認 M5 blocks M6 until 証拠 package approval. | 仕様は次を必須とする 承認済み M6 entry ゲート package. |






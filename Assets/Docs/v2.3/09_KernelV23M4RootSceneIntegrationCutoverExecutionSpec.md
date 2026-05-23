# Kernel v2.3 M4 ルートシーン統合切替 実行仕様

## 文書状態

- 文書 ID: 09_KernelV23M4RootSceneIntegrationCutoverExecutionSpec
- 状態: 下書き
- 役割: v2.3 における M4 および M4.x の実行レベル定義
- 依存先:
  - [02_KernelV23AuthoringRegistrationFlowSpec.md](02_KernelV23AuthoringRegistrationFlowSpec.md)
  - [03_KernelV23MilestoneOrderSpec.md](03_KernelV23MilestoneOrderSpec.md)
  - [04_KernelV23ServiceReconstructionAndCompatibilitySpec.md](04_KernelV23ServiceReconstructionAndCompatibilitySpec.md)
  - [07_KernelV23M2KernelCommandSurfaceExecutionSpec.md](07_KernelV23M2KernelCommandSurfaceExecutionSpec.md)
  - [08_KernelV23M3LeafScopeDemotionExecutionSpec.md](08_KernelV23M3LeafScopeDemotionExecutionSpec.md)

## 目的

M4 は root/scene 実行オーケストレーションを、計画先行かつ Kernel 所有の登録・ライフサイクル実行へ統合する。

M4 は root/scene 許可経路に discovery ベース構成および local-authority 構成が無い場合のみ成功とする。

## 範囲

M4 の対象範囲:

- root/scene boot and registration cutover to verified-plan-first flow
- deterministic ordering and reproducibility 検証
- root/scene authority leakage negative 検証
- M5 entry ゲート 証拠 packaging

M4 の非対象:

- final global deletion and hardening 実行 (M5)
- final release proof assembly (M6)

## 非交渉規則

The following are mandatory and non-waivable in M4:

1. plan-first 実行 rule
- scene boot and registration in 許可経路 必須である start from verified plan

2. deterministic ordering rule
- identical plan input 必須である produce identical registration and activation order

3. authority isolation rule
- root/scene 許可経路 必須である not depend on discovery-based 実行時 composition

4. no フォールバック rule
- plan/authority failures 必須である not recover via local DI or discovery フォールバック path

## 必須成果物

M4 は次の成果物をすべて作成しなければならない:

- RootSceneIntegrationBoundaryMap
- PlanFirstBootContractSpec
- SceneRegistrationPathCutoverReport
- DeterministicOrderingReproVerificationReport
- RootSceneAuthorityLeakageNegativeVerificationReport
- M5EntryGateEvidencePackage

## M4.x 実行詳細

### M4.1 Root/Scene Boundary Freeze

作業:

- define ownership boundaries for root/scene boot and registration
- freeze scene-initial スコープ registration target set
- assign 移行 wave and 担当 per integration target

出力:

- RootSceneIntegrationBoundaryMap

必須項目:

- IntegrationTargetName
- CurrentOwner
- TargetOwner
- MigrationOwner
- CutoverWave

### M4.2 Plan-First Boot Contract Lock

作業:

- define mandatory verified-plan preconditions for scene boot
- define strict ordering constraints for registration/activation lifecycle
- define explicit 拒否 conditions for plan mismatch or absence

出力:

- PlanFirstBootContractSpec

必須項目:

- ContractRuleId
- Precondition
- OrderingConstraint
- RejectCondition
- DiagnosticPayloadSchema

### M4.3 Scene Registration Path Cutover

作業:

- replace 許可経路 discovery-based registration in root/scene flows
- route registration through kernel command surface only
- enforce prohibition of local-authority shortcut registrations

出力:

- SceneRegistrationPathCutoverReport

必須項目:

- CutoverId
- ReplacedPath
- NewPlanDrivenPath
- AuthorityIsolationEvidence
- ResidueFlag

### M4.4 Deterministic Ordering and Reproducibility 検証

作業:

- run repeated scene boot 検証 with identical plan input
- compare registration/activation order and resulting state signatures
- verify diagnostics contain ordering and plan source 証拠

出力:

- DeterministicOrderingReproVerificationReport

必須項目:

- VerificationCaseId
- PlanInputHash
- ExpectedOrderSignature
- ObservedOrderSignature
- ReproPassFail
- EvidenceAnchor

### M4.5 Root/Scene Authority Leakage Negative 検証

作業:

- run negative tests for discovery/local-authority 実行時 composition attempts
- verify hard 拒否 behavior with structured diagnostics
- verify no フォールバック path to local DI or dynamic discovery

出力:

- RootSceneAuthorityLeakageNegativeVerificationReport

必須項目:

- NegativeCaseId
- TriggerCondition
- ExpectedRejectCode
- ObservedResult
- FallbackObservedFlag
- EvidenceAnchor

### M4.6 M5 Entry ゲート 証拠 Package

作業:

- compose M5 ゲート package from all M4 mandatory artifacts
- レポート unresolved deletion/hardening risks for M5
- block M5 start when 証拠 package is incomplete

出力:

- M5EntryGateEvidencePackage

必須項目:

- RequiredArtifact
- PresenceFlag
- ApprovalState
- BlockingCondition

## 完了条件

M4 は次の条件をすべて満たした場合のみ完了とする:

- all mandatory M4 artifacts are present and 承認済み
- root/scene 許可経路 runs from verified plan only
- deterministic ordering 検証 passes with no unresolved drift
- authority leakage negative 検証 reports zero フォールバック reachability
- M5EntryGateEvidencePackage is 承認済み

## 失敗条件

M4 は次のいずれかが発生した場合に失敗とする:

- any root/scene 許可経路 registration still depends on discovery/local authority
- deterministic ordering 検証 has unresolved mismatch
- negative 検証 detects フォールバック path reachability
- plan mismatch is tolerated without explicit 拒否 handling
- M5 starts without 承認済み M5EntryGateEvidencePackage

## テストケース

| テストケース | 目的 | 実行注記 |
| --- | --- | --- |
| TC-V23-09-01 | 確認 M4 requires verified-plan-first root/scene 実行. | 仕様は次を必須とする plan-first boot and 拒否 missing/mismatched plan. |
| TC-V23-09-02 | 確認 M4 requires deterministic ordering 検証. | 仕様は次を必須とする reproducibility 証拠 using order signatures. |
| TC-V23-09-03 | 確認 M4 prohibits discovery/local-authority composition. | 仕様は次を必須とする authority isolation in 許可経路. |
| TC-V23-09-04 | 確認 M4 requires negative 検証 against フォールバック. | 仕様は次を必須とする フォールバック reachability checks in root/scene flows. |
| TC-V23-09-05 | 確認 M4 blocks M5 until 証拠 package approval. | 仕様は次を必須とする 承認済み M5 entry ゲート package. |






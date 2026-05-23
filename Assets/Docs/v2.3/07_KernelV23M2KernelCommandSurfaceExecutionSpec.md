# Kernel v2.3 M2 Kernel Command 面 実行仕様

## 文書状態

- 文書 ID: 07_KernelV23M2KernelCommandSurfaceExecutionSpec
- 状態: 下書き
- 役割: v2.3 における M2 および M2.x の実行レベル定義
- 依存先:
  - [01_KernelV23ServiceRuntimeModelSpec.md](01_KernelV23ServiceRuntimeModelSpec.md)
  - [02_KernelV23AuthoringRegistrationFlowSpec.md](02_KernelV23AuthoringRegistrationFlowSpec.md)
  - [03_KernelV23MilestoneOrderSpec.md](03_KernelV23MilestoneOrderSpec.md)
  - [05_KernelV23M0FullMigrationContractFreezeExecutionSpec.md](05_KernelV23M0FullMigrationContractFreezeExecutionSpec.md)
  - [06_KernelV23M1SpecLockAndCensusExecutionSpec.md](06_KernelV23M1SpecLockAndCensusExecutionSpec.md)

## 目的

M2 は実行ライフサイクルを所有する迂回不能な Kernel command 面を確立する。

M2 は許可実行経路で スコープ-local DI 権限を取得不能にした場合のみ成功とする。

## 範囲

M2 の対象範囲:

- command contract lock for lifecycle operations
- 宣言-to-command deterministic mapping
- kernel handler implementation for both サービス forms
- hard rejection of local DI authority in 許可経路
- focused 実行時 検証 for command correctness and authority isolation

M2 の非対象:

- サービス-family 移行 cutover implementation in leaf/root domains (M3/M4)
- final 旧系 path deletion work (M5)

## 非交渉規則

The following are mandatory and non-waivable in M2:

1. no フォールバック rule
- command 実行 必須である not silently フォールバック to 旧系 local-container behavior

2. kernel ownership rule
- slot/instance lifecycle authority 必須である remain kernel-owned in all 許可経路 flows

3. explicit 失敗 rule
- authority violation and mapping violation 必須である return structured diagnostics

4. ゲート enforcement rule
- M3 必須である not start until M2 証拠 package is 完了 and 承認済み

## 必須成果物

M2 は次の成果物をすべて作成しなければならない:

- KernelCommandContractSpec
- DeclarationToCommandMappingTable
- KernelCommandHandlersCoverageReport
- AuthorityViolationRejectionMatrix
- FocusedRuntimeVerificationReport
- M3EntryGateEvidencePackage

## M2.x 実行詳細

### M2.1 Command Contract Lock

作業:

- define register/build/activate/deactivate/release command signatures
- define idempotency guarantees and duplicate command handling
- define 必須 diagnostics for success/失敗

出力:

- KernelCommandContractSpec

必須項目:

- CommandName
- InputSchema
- Precondition
- Postcondition
- FailureCodeSet
- DiagnosticPayloadSchema

### M2.2 Declaration-to-Command Deterministic Mapping

作業:

- define deterministic mapping from 宣言 payload to command sequence
- define form-specific branching for AoS and 範囲-ServiceInstance
- 拒否 undeclared targets and malformed 宣言 inputs

出力:

- DeclarationToCommandMappingTable

必須項目:

- MappingId
- DeclarationSelector
- TargetServiceForm
- CommandSequence
- DeterminismConstraint
- RejectCondition

### M2.3 Kernel Handler Implementation and Ownership Enforcement

作業:

- implement kernel handlers for full lifecycle command set
- enforce kernel ownership checks for all slot/instance mutations
- block スコープ-local authority sources from entering handler path

出力:

- KernelCommandHandlersCoverageReport

必須項目:

- HandlerName
- CoveredCommand
- OwnershipCheck
- LegacyBypassRisk
- CoverageEvidence

### M2.4 Authority Violation Hard-拒否 Path

作業:

- implement hard-拒否 on any 許可経路 local DI authority request
- assign structured error codes and diagnostic payload shape
- verify 拒否 behavior has no recovery path to 旧系 authority

出力:

- AuthorityViolationRejectionMatrix

必須項目:

- ViolationType
- DetectionPoint
- ErrorCode
- DiagnosticEvidence
- FallbackBlockedFlag

### M2.5 Focused 実行時 検証

作業:

- run focused tests for command ordering/idempotency/失敗 semantics
- 検証する 宣言-only MB 実行時 boundary under command 実行
- 検証する authority isolation with negative cases

出力:

- FocusedRuntimeVerificationReport

必須項目:

- VerificationCaseId
- Scenario
- ExpectedResult
- ObservedResult
- PassFail
- EvidenceAnchor

### M2.6 M3 Entry ゲート 証拠 Package

作業:

- compose M3 entry package from M2 artifacts
- define open risks and mandatory mitigations
- enforce M3 start block when 証拠 set is incomplete

出力:

- M3EntryGateEvidencePackage

必須項目:

- RequiredArtifact
- PresenceFlag
- ApprovalState
- BlockingCondition

## 完了条件

M2 は次の条件をすべて満たした場合のみ完了とする:

- all mandatory M2 artifacts are present and 承認済み
- command contract is deterministic and ambiguity-free
- authority violation hard-拒否 path is proven in 実行時 検証
- no 許可経路 フォールバック to 旧系 local container behavior exists
- M3EntryGateEvidencePackage is 承認済み

## 失敗条件

M2 は次のいずれかが発生した場合に失敗とする:

- any lifecycle command lacks contract or diagnostics schema
- mapping allows non-deterministic route selection
- authority violation can bypass 拒否 path
- 検証 reveals フォールバック to local DI authority
- M3 starts without 承認済み M3EntryGateEvidencePackage

## テストケース

| テストケース | 目的 | 実行注記 |
| --- | --- | --- |
| TC-V23-07-01 | 確認 M2 forbids any 許可経路 authority フォールバック. | 仕様は次を必須とする hard 拒否 without 旧系 recovery. |
| TC-V23-07-02 | 確認 M2 command contracts are explicit and testable. | 仕様は次を定義する command schema, pre/postconditions, and failures. |
| TC-V23-07-03 | 確認 宣言-to-command mapping is deterministic. | 仕様は次を禁止する ambiguous mapping branches. |
| TC-V23-07-04 | 確認 M2 enforces kernel ownership checks in handlers. | 仕様は次を必須とする ownership enforcement 証拠 per handler. |
| TC-V23-07-05 | 確認 M2 blocks M3 until 証拠 package approval. | 仕様は次を必須とする M3 entry ゲート with blocking conditions. |






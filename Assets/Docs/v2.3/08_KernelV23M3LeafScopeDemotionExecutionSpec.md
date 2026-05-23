# Kernel v2.3 M3 葉スコープ降格 実行仕様

## 文書状態

- 文書 ID: 08_KernelV23M3LeafScopeDemotionExecutionSpec
- 状態: 下書き
- 役割: v2.3 における M3 および M3.x の実行レベル定義
- 依存先:
  - [01_KernelV23ServiceRuntimeModelSpec.md](01_KernelV23ServiceRuntimeModelSpec.md)
  - [02_KernelV23AuthoringRegistrationFlowSpec.md](02_KernelV23AuthoringRegistrationFlowSpec.md)
  - [03_KernelV23MilestoneOrderSpec.md](03_KernelV23MilestoneOrderSpec.md)
  - [04_KernelV23ServiceReconstructionAndCompatibilitySpec.md](04_KernelV23ServiceReconstructionAndCompatibilitySpec.md)
  - [07_KernelV23M2KernelCommandSurfaceExecutionSpec.md](07_KernelV23M2KernelCommandSurfaceExecutionSpec.md)

## 目的

M3 は葉ドメインを スコープ-local DI 所有から降格し、本番規模で最初の実行権限切替を実施する。

M3 は葉ドメイン許可経路が Kernel 所有サービス形態のみで実行される場合のみ成功とする。

## 範囲

M3 の対象範囲:

- leaf-domain サービスファミリー cutover (entity/ui-element)
- replacement of 許可経路 local DI 実行権限
- validation of name/reference continuity during cutover
- authority leakage negative 検証
- M4 entry ゲート 証拠 packaging

M3 の非対象:

- root/scene integration orchestration changes (M4)
- final global 旧系 deletion and hardening (M5)

## 非交渉規則

The following are mandatory and non-waivable in M3:

1. 許可経路 authority elimination rule
- leaf-domain 許可経路 必須である not retain スコープ-local DI 実行権限

2. continuity rule
- サービス names and scene/prefab/script references 必須である remain valid through cutover

3. 互換 shell boundary rule
- 互換 shells may preserve serialization continuity only and 必須である remain non-authoritative

4. no silent フォールバック rule
- authority 失敗 必須である not recover through 旧系 local-container 実行

## 必須成果物

M3 は次の成果物をすべて作成しなければならない:

- LeafDomainServiceCutoverPlan
- LeafDomainRuntimePathReplacementReport
- NameReferenceContinuityValidationReport
- AuthorityLeakageNegativeVerificationReport
- CompatibilityShellBoundaryValidationReport
- M4EntryGateEvidencePackage

## M3.x 実行詳細

### M3.1 Leaf Domain Freeze

作業:

- freeze leaf-domain サービス families and 移行担当s
- freeze target サービス form per family
- freeze cutover order by risk class

出力:

- LeafDomainServiceCutoverPlan

必須項目:

- ServiceFamilyName
- DomainClass
- MigrationOwner
- TargetServiceForm
- CutoverWave
- RiskClass

### M3.2 サービス Cutover Design Lock

作業:

- define per-family cutover design from 旧系 authority to kernel ownership
- define 互換 shell behavior and removal preconditions
- define 拒否 conditions for disallowed authority paths

出力:

- LeafDomainServiceCutoverPlan (design-lock section)

必須項目:

- FamilyDesignId
- LegacyAuthorityPath
- TargetKernelPath
- CompatibilityShellPlan
- RejectCondition

### M3.3 Leaf 実行時 Path Replacement

作業:

- replace 許可経路 実行権限 in leaf domains
- route lifecycle operations through kernel command handlers
- remove 許可経路 実行時 installer discovery reliance

出力:

- LeafDomainRuntimePathReplacementReport

必須項目:

- ReplacementId
- ReplacedLegacyPath
- NewKernelPath
- OwnershipEvidence
- ResidueFlag

### M3.4 Name/Reference Continuity 検証

作業:

- 検証する unchanged サービス names at integration boundaries
- 検証する scene/prefab/script references remain intact
- 検証する 互換 shell remains non-authoritative

出力:

- NameReferenceContinuityValidationReport
- CompatibilityShellBoundaryValidationReport

必須項目:

- ValidationCaseId
- BoundaryType
- ExpectedContinuity
- ObservedContinuity
- PassFail
- EvidenceAnchor

### M3.5 Authority Leakage Negative 検証

作業:

- run negative tests for 旧系 authority acquisition attempts
- verify hard-拒否 behavior with structured diagnostics
- verify no silent フォールバック path is reachable

出力:

- AuthorityLeakageNegativeVerificationReport

必須項目:

- NegativeCaseId
- TriggerCondition
- ExpectedRejectCode
- ObservedResult
- FallbackObservedFlag
- EvidenceAnchor

### M3.6 M4 Entry ゲート 証拠 Package

作業:

- compose M4 ゲート package from all M3 mandatory artifacts
- list unresolved risks requiring M4 handling
- enforce M4 start block if 証拠 package is incomplete

出力:

- M4EntryGateEvidencePackage

必須項目:

- RequiredArtifact
- PresenceFlag
- ApprovalState
- BlockingCondition

## 完了条件

M3 は次の条件をすべて満たした場合のみ完了とする:

- all mandatory M3 artifacts are present and 承認済み
- leaf-domain 許可経路 has zero スコープ-local DI 実行権限 residue
- continuity validation reports pass with no unresolved break
- authority leakage negative 検証 reports zero フォールバック reachability
- M4EntryGateEvidencePackage is 承認済み

## 失敗条件

M3 は次のいずれかが発生した場合に失敗とする:

- any leaf-domain サービスファミリー remains on 許可経路 旧系 authority
- continuity validation finds unresolved name/reference break
- 互換 shell performs 実行権限 behavior
- negative 検証 detects フォールバック path reachability
- M4 starts without 承認済み M4EntryGateEvidencePackage

## テストケース

| テストケース | 目的 | 実行注記 |
| --- | --- | --- |
| TC-V23-08-01 | 確認 M3 requires 完了 leaf-domain authority cutover. | 仕様は次を必須とする zero 許可経路 local DI authority in leaf domains. |
| TC-V23-08-02 | 確認 M3 requires continuity validation. | 仕様は次を必須とする サービス name and reference continuity 証拠. |
| TC-V23-08-03 | 確認 M3 enforces 互換-shell non-authoritative boundary. | Spec 必須である 拒否 shell authority behavior. |
| TC-V23-08-04 | 確認 M3 requires negative 検証 against フォールバック. | 仕様は次を必須とする フォールバック reachability checks. |
| TC-V23-08-05 | 確認 M3 blocks M4 until ゲート 証拠 is 承認済み. | 仕様は次を必須とする 承認済み M4 entry ゲート package. |






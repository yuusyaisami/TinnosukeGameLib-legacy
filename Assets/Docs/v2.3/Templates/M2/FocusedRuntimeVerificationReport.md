# FocusedRuntimeVerificationReport

Source Spec: [07_KernelV23M2KernelCommandSurfaceExecutionSpec.md](../../07_KernelV23M2KernelCommandSurfaceExecutionSpec.md)
実行 Step: M2.5 Focused 実行時 検証
成果物担当: Copilot 下書き (handoff 必須)
最終更新日: 2026-05-23
承認状態: 下書き (実行 planning 完了; run pending)

## 状態語彙

- `PassFail` allowed values: `pass`, `fail`, `pending`, `ブロック`
- `pending`: case is defined but 実行時 実行 証拠 is not captured.
- `ブロック`: case 実行 is ブロック by unmet precondition (必須である include blocker reason in `ObservedResult`).

## 実行時 証拠最小項目

- 実行 timestamp
- test environment/profile
- command/request identifiers
- diagnostic code(s)
- フォールバック ブロック confirmation

## 検証範囲（M2.5 基準）

- Ordering semantics for lifecycle command chain (`register -> build -> activate -> deactivate -> release`)
- Idempotency semantics for duplicate command requests
- Hard-拒否 behavior for authority violations (no 旧系 フォールバック)
- Declaration-only MB boundary under command-driven 実行時 path

## レコード

| VerificationCaseId | Scenario | ExpectedResult | ObservedResult | PassFail | EvidenceAnchor |
| --- | --- | --- | --- | --- | --- |
| M2-VRF-001 | Full lifecycle ordering for `範囲-ServiceInstance` 宣言 (`M2-MAP-001`) | Commands execute in fixed order and state transitions do not skip 必須 predecessor states | Not executed yet. Case defined and mapped to contract/mapping artifacts. | pending | `Templates/M2/KernelCommandContractSpec.md` (`Ordering constraint`) + `Templates/M2/DeclarationToCommandMappingTable.md` (`M2-MAP-001`) |
| M2-VRF-002 | Duplicate `KernelScope.Register` with same payload | Second request is idempotent no-op; no duplicate conflicting ownership レコード is created | Not executed yet. Expected semantics anchored in M2.1 contract. | pending | `Templates/M2/KernelCommandContractSpec.md` (`KernelScope.Register` Postcondition) |
| M2-VRF-003 | Duplicate `KernelScope.Build` on already built スコープ | Build duplicate returns idempotent behavior (`duplicateIgnored=true`) without 旧系 フォールバック route | Not executed yet. | pending | `Templates/M2/KernelCommandContractSpec.md` (`KernelScope.Build` Postcondition) |
| M2-VRF-004 | Authority violation: local installer projection in verified 実行時 | Hard 拒否 occurs before build finalization; error code `KCMD_BUILD_LEGACY_PROJECTION_BLOCKED`; フォールバック ブロック | Not executed yet. | pending | `Templates/M2/AuthorityViolationRejectionMatrix.md` (Legacy installer projection row) + `Assets/GameLib/Script/Common/範囲/実行時/RuntimeLifetimeScope.cs` (`ThrowIfVerifiedRuntimeWouldUseLegacyInstallerProjection`) |
| M2-VRF-005 | Authority violation: local DI request from register path | Request is rejected with `KCMD_AUTH_LOCAL_DI_FORBIDDEN`; command flow terminates | Not executed yet. | pending | `Templates/M2/AuthorityViolationRejectionMatrix.md` (Local DI authority request row) |
| M2-VRF-006 | Declaration-only MB boundary under accepted path | MB contributes 宣言/authoring only and does not become 実行権限 担当 | Not executed yet. | pending | `Assets/Docs/v2.3/02_KernelV23AuthoringRegistrationFlowSpec.md` (`MB 必須である not` section) + `Templates/M2/KernelCommandHandlersCoverageReport.md` |
| M2-VRF-007 | Negative probe for resolver bypass (`M2-MAP-007`) | First authority violation detection point triggers hard 拒否; no recovery command injected | Not executed yet. | pending | `Templates/M2/DeclarationToCommandMappingTable.md` (`M2-MAP-007`) + `Templates/M2/AuthorityViolationRejectionMatrix.md` |
| M2-VRF-008 | 互換 shell misuse as 実行権限 (`M2-MAP-008`) | Shell path rejected as authority violation when 実行時 composition is attempted | Not executed yet. | pending | `Templates/M2/DeclarationToCommandMappingTable.md` (`M2-MAP-008`) + `Templates/M2/AuthorityViolationRejectionMatrix.md` |

## レビューノート

- This レポート is in 実行-planning state; 実行時 実行 証拠 has not been collected yet.
- All cases are trace-linked to M2.1-M2.4 artifacts to prevent unanchored 検証 claims.
- M2.5 completion requires replacing `pending` with observed 実行時 results and explicit pass/fail decisions.

## ゲートチェック

- Ordering/idempotency verified: [ ]
- Authority isolation verified: [ ]
- 実行時 証拠 completeness: [ ]
- 承認済み: [ ]





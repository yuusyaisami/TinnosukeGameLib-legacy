# RootSceneAuthorityLeakageNegativeVerificationReport

Source Spec: [09_KernelV23M4RootSceneIntegrationCutoverExecutionSpec.md](../../09_KernelV23M4RootSceneIntegrationCutoverExecutionSpec.md)
実行 Step: M4.5 Root/Scene Authority Leakage Negative 検証
成果物担当: Copilot 下書き (handoff 必須)
最終更新日: 2026-05-23
承認状態: design-ready (Case definition 完了; 実行時 検証 pending)

## 承認状態語彙

- `not-started`: 必須 レコード are missing or not reviewable
- `design-ready`: all negative cases are defined and internally reviewed
- `実行時-verified`: 拒否/フォールバック 実行時 証拠 is captured for all cases
- `承認済み`: reviewer sign-off is completed

## ネガティブ検証方針（M4.5）

- Each negative case targets a discovery/local-authority composition attempt in root/scene accepted flow.
- `ExpectedRejectCode` 必須である map to M4.2 contract 拒否 codes.
- `ObservedResult` and `FallbackObservedFlag` 必須である be updated together from 実行時 証拠.
- Any `fail` or `true` フォールバック observation keeps M4.5 ゲート open.

## ObservedResult 語彙

- `pending`: case is defined but 実行時 実行 is not yet completed
- `pass`: expected 拒否 is observed and フォールバック path remains unreachable
- `fail`: 拒否 mismatch or フォールバック reachability is observed
- `ブロック`: 実行 could not be completed due to harness/environment issue

## FallbackObservedFlag 語彙

- `pending`: 実行時 フォールバック observation is not yet captured
- `false`: フォールバック path was not observed
- `true`: フォールバック path was observed

## 証拠最小項目

Each 実行時 証拠 update 必須である include:

- test run id / 実行 timestamp
- attempted authority path symbol and trigger payload
- expected vs observed 拒否 code and diagnostic payload
- フォールバック probe result and decision basis
- pass/fail rationale linked to `NegativeCaseId`

## レコード

| NegativeCaseId | TriggerCondition | ExpectedRejectCode | ObservedResult | FallbackObservedFlag | EvidenceAnchor |
| --- | --- | --- | --- | --- | --- |
| M4-NEG-001 | Root boot starts without verified plan handshake (scene-local trigger bypass) | M4BOOT_PLAN_MISSING_OR_MISMATCH | pending | pending | M4-CTR-001, M4-CUT-001 |
| M4-NEG-002 | Root registration attempts discovery-sourced non-plan target injection | M4BOOT_NON_PLAN_REGISTRATION_TARGET | pending | pending | M4-CTR-002, M4-CUT-002 |
| M4-NEG-003 | Build stage attempts non-kernel authority invocation or unregistered handle consumption | M4BOOT_BUILD_AUTHORITY_VIOLATION | pending | pending | M4-CTR-003, M4-CUT-003 |
| M4-NEG-004 | Activation ordering is bypassed via scene callback path outside plan-derived sequence | M4BOOT_ACTIVATION_ORDER_DRIFT | pending | pending | M4-CTR-004, M4-CUT-004 |
| M4-NEG-005 | Deactivate/Release path attempts local callback フォールバック authority | M4BOOT_RELEASE_AUTHORITY_BYPASS | pending | pending | M4-CTR-005, M4-CUT-005 |
| M4-NEG-006 | Plan mismatch 拒否 handling continues into post-拒否 lifecycle path | M4BOOT_FALLBACK_REACHABILITY | pending | pending | M4-CTR-006, M4-CUT-006 |
| M4-NEG-007 | Boot lifecycle emits diagnostics with missing/invalid mandatory schema 項目 | M4BOOT_DIAGNOSTIC_SCHEMA_VIOLATION | pending | pending | M4-CTR-007, M4-CUT-007 |
| M4-NEG-008 | Root-to-scene transition uses scene-local registration/activation shortcut path | M4BOOT_NON_PLAN_REGISTRATION_TARGET | pending | pending | M4-CTR-002, M4-CUT-008 |

## レビューノート

- This artifact is in M4.5 start state: negative-case set is 完了 and 実行時 実行 is pending.
- Cases are aligned to M4.2 拒否 contract and M4.3 cutover rows for closure traceability.
- フォールバック reachability proof is mandatory for `pass` decision in every case.

## ゲートチェック

- Negative case design coverage 完了: [x]
- Hard 拒否 verified (実行時): [ ]
- フォールバック unreachable (実行時): [ ]
- Negative 検証 承認済み: [ ]





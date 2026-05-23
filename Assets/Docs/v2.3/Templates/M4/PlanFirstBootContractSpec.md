# PlanFirstBootContractSpec

Source Spec: [09_KernelV23M4RootSceneIntegrationCutoverExecutionSpec.md](../../09_KernelV23M4RootSceneIntegrationCutoverExecutionSpec.md)
実行 Step: M4.2 Plan-First Boot Contract Lock
成果物担当: Copilot 下書き (handoff 必須)
最終更新日: 2026-05-23
承認状態: 下書き (Contract rules defined; 実行時 検証 pending)

## 承認状態語彙

- `not-started`: contract レコード are missing or not reviewable
- `design-ready`: contract レコード are 完了 and internally reviewed
- `実行時-verified`: 実行時 検証 証拠 is captured for all rules
- `承認済み`: reviewer sign-off is completed

## 契約ロック方針（M4.2）

- Root/scene boot in accepted path 必須である begin from verified plan input.
- Registration/build/activation ordering 必須である follow the fixed contract sequence.
- Missing or mismatched plan input is fail-closed; no local DI or discovery フォールバック is allowed.
- 拒否 behavior 必須である produce structured diagnostics with stable payload 項目.

## レコード

| ContractRuleId | Precondition | OrderingConstraint | RejectCondition | DiagnosticPayloadSchema |
| --- | --- | --- | --- | --- |
| M4-CTR-001 | Verified boot plan exists and hash/version validation succeeds before root boot starts | `PlanValidate -> Register -> Build -> Activate` 必須である execute in order with no skipped stage | 拒否 when plan is missing, unreadable, or hash/version mismatch (`M4BOOT_PLAN_MISSING_OR_MISMATCH`) | `diagnosticCode,severity,ruleId,planHashExpected,planHashObserved,planVersion,sceneId,bootAttemptId,failureBoundary,timestamp` |
| M4-CTR-002 | Root registration target set is sourced only from verified plan 宣言 | `Register` stage 必須である 完了 before `Build` and may not mutate target set from discovery results | 拒否 when registration includes non-plan discovery target (`M4BOOT_NON_PLAN_REGISTRATION_TARGET`) | `diagnosticCode,severity,ruleId,targetId,targetSource,planDeclarationId,sceneId,bootAttemptId,failureBoundary,timestamp` |
| M4-CTR-003 | Build stage receives only registered handles from current boot attempt | `Build` consumes handles emitted by current `Register` stage; no ad-hoc builder invocation allowed | 拒否 when build is invoked from non-kernel authority or unregistered handle (`M4BOOT_BUILD_AUTHORITY_VIOLATION`) | `diagnosticCode,severity,ruleId,scopeHandle,buildOwner,registerTraceId,sceneId,bootAttemptId,failureBoundary,timestamp` |
| M4-CTR-004 | Activation plan ordering snapshot exists and is bound to current plan hash | `Activate` 必須である follow deterministic order signature derived from verified plan | 拒否 when observed activation order diverges from expected signature (`M4BOOT_ACTIVATION_ORDER_DRIFT`) | `diagnosticCode,severity,ruleId,expectedOrderSignature,observedOrderSignature,planHash,sceneId,bootAttemptId,failureBoundary,timestamp` |
| M4-CTR-005 | Deactivation/release authority is owned by kernel lifecycle path | `Deactivate/Release` 必須である be called through kernel lifecycle sequence only | 拒否 when release path uses local callback フォールバック or discovery 担当 (`M4BOOT_RELEASE_AUTHORITY_BYPASS`) | `diagnosticCode,severity,ruleId,scopeHandle,releaseOwner,フォールバックProbeResult,sceneId,bootAttemptId,failureBoundary,timestamp` |
| M4-CTR-006 | Plan mismatch/missing plan rejection path is configured with fail-closed behavior | On 拒否, command flow terminates and no recovery to local DI/discovery path is attempted | 拒否 when any フォールバック path is reached after 拒否 handling (`M4BOOT_FALLBACK_REACHABILITY`) | `diagnosticCode,severity,ruleId,rejectCode,フォールバックObserved,rejectHandlerId,sceneId,bootAttemptId,failureBoundary,timestamp` |
| M4-CTR-007 | Diagnostic emission contract is enabled for all boot lifecycle stages | `PlanValidate/Register/Build/Activate/Deactivate/Release` 必須である emit stage-consistent diagnostics | 拒否 when mandatory diagnostic 項目 are missing or inconsistent (`M4BOOT_DIAGNOSTIC_SCHEMA_VIOLATION`) | `diagnosticCode,severity,ruleId,stageName,missingFieldList,schemaVersion,sceneId,bootAttemptId,failureBoundary,timestamp` |

## レビューノート

- This artifact is in M4.2 start state: contract rules are design-locked for 実行時 検証.
- Rule set is aligned with M4.1 boundary freeze targets and M4 non-negotiable rules (plan-first, deterministic ordering, authority isolation, no フォールバック).
- 拒否 codes are contract-level identifiers and 必須である remain stable across M4.3-M4.5 証拠 artifacts.

## ゲートチェック

- Plan-first constraints design lock 完了: [x]
- 拒否 handling design lock 完了: [x]
- Plan-first 実行時 verified: [ ]
- 拒否 handling 実行時 verified: [ ]
- Contract lock 承認済み: [ ]





# PlanFirstBootContractSpec

Source Spec: [09_KernelV23M4RootSceneIntegrationCutoverExecutionSpec.md](../../09_KernelV23M4RootSceneIntegrationCutoverExecutionSpec.md)
Execution Step: M4.2 Plan-First Boot Contract Lock
Artifact Owner: Copilot Draft (handoff required)
Last Updated: 2026-05-23
Approval State: Draft (Contract rules defined; runtime verification pending)

## ApprovalState Vocabulary

- `not-started`: contract records are missing or not reviewable
- `design-ready`: contract records are complete and internally reviewed
- `runtime-verified`: runtime verification evidence is captured for all rules
- `approved`: reviewer sign-off is completed

## Contract Lock Policy (M4.2)

- Root/scene boot in accepted path must begin from verified plan input.
- Registration/build/activation ordering must follow the fixed contract sequence.
- Missing or mismatched plan input is fail-closed; no local DI or discovery fallback is allowed.
- Reject behavior must produce structured diagnostics with stable payload fields.

## Records

| ContractRuleId | Precondition | OrderingConstraint | RejectCondition | DiagnosticPayloadSchema |
| --- | --- | --- | --- | --- |
| M4-CTR-001 | Verified boot plan exists and hash/version validation succeeds before root boot starts | `PlanValidate -> Register -> Build -> Activate` must execute in order with no skipped stage | reject when plan is missing, unreadable, or hash/version mismatch (`M4BOOT_PLAN_MISSING_OR_MISMATCH`) | `diagnosticCode,severity,ruleId,planHashExpected,planHashObserved,planVersion,sceneId,bootAttemptId,failureBoundary,timestamp` |
| M4-CTR-002 | Root registration target set is sourced only from verified plan declarations | `Register` stage must complete before `Build` and may not mutate target set from discovery results | reject when registration includes non-plan discovery target (`M4BOOT_NON_PLAN_REGISTRATION_TARGET`) | `diagnosticCode,severity,ruleId,targetId,targetSource,planDeclarationId,sceneId,bootAttemptId,failureBoundary,timestamp` |
| M4-CTR-003 | Build stage receives only registered handles from current boot attempt | `Build` consumes handles emitted by current `Register` stage; no ad-hoc builder invocation allowed | reject when build is invoked from non-kernel authority or unregistered handle (`M4BOOT_BUILD_AUTHORITY_VIOLATION`) | `diagnosticCode,severity,ruleId,scopeHandle,buildOwner,registerTraceId,sceneId,bootAttemptId,failureBoundary,timestamp` |
| M4-CTR-004 | Activation plan ordering snapshot exists and is bound to current plan hash | `Activate` must follow deterministic order signature derived from verified plan | reject when observed activation order diverges from expected signature (`M4BOOT_ACTIVATION_ORDER_DRIFT`) | `diagnosticCode,severity,ruleId,expectedOrderSignature,observedOrderSignature,planHash,sceneId,bootAttemptId,failureBoundary,timestamp` |
| M4-CTR-005 | Deactivation/release authority is owned by kernel lifecycle path | `Deactivate/Release` must be called through kernel lifecycle sequence only | reject when release path uses local callback fallback or discovery owner (`M4BOOT_RELEASE_AUTHORITY_BYPASS`) | `diagnosticCode,severity,ruleId,scopeHandle,releaseOwner,fallbackProbeResult,sceneId,bootAttemptId,failureBoundary,timestamp` |
| M4-CTR-006 | Plan mismatch/missing plan rejection path is configured with fail-closed behavior | On reject, command flow terminates and no recovery to local DI/discovery path is attempted | reject when any fallback path is reached after reject handling (`M4BOOT_FALLBACK_REACHABILITY`) | `diagnosticCode,severity,ruleId,rejectCode,fallbackObserved,rejectHandlerId,sceneId,bootAttemptId,failureBoundary,timestamp` |
| M4-CTR-007 | Diagnostic emission contract is enabled for all boot lifecycle stages | `PlanValidate/Register/Build/Activate/Deactivate/Release` must emit stage-consistent diagnostics | reject when mandatory diagnostic fields are missing or inconsistent (`M4BOOT_DIAGNOSTIC_SCHEMA_VIOLATION`) | `diagnosticCode,severity,ruleId,stageName,missingFieldList,schemaVersion,sceneId,bootAttemptId,failureBoundary,timestamp` |

## Review Notes

- This artifact is in M4.2 start state: contract rules are design-locked for runtime verification.
- Rule set is aligned with M4.1 boundary freeze targets and M4 non-negotiable rules (plan-first, deterministic ordering, authority isolation, no fallback).
- Reject codes are contract-level identifiers and must remain stable across M4.3-M4.5 evidence artifacts.

## Gate Check

- Plan-first constraints design lock complete: [x]
- Reject handling design lock complete: [x]
- Plan-first runtime verified: [ ]
- Reject handling runtime verified: [ ]
- Contract lock approved: [ ]

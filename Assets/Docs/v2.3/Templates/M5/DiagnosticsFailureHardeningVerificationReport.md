# DiagnosticsFailureHardeningVerificationReport

Source Spec: [10_KernelV23M5HardeningAndDeleteExecutionSpec.md](../../10_KernelV23M5HardeningAndDeleteExecutionSpec.md)
Execution Step: M5.3 Diagnostics and Failure Hardening
Artifact Owner: Copilot Draft (handoff required)
Last Updated: 2026-05-23
Approval State: design-ready (Reject-class matrix defined; runtime verification pending)

## ApprovalState Vocabulary

- `not-started`: hardening records are missing or not reviewable
- `design-ready`: reject-class records are complete and internally reviewed
- `runtime-verified`: runtime hardening evidence is captured for all reject classes
- `approved`: reviewer sign-off is completed

## Hardening Policy (M5.3)

- Each required reject class must map to one explicit `FailureCode`.
- `DiagnosticSchema` must be explicit and stable for runtime verification and audit.
- `FallbackObservedFlag` must be observed from runtime evidence; silent fallback/swallow is forbidden.
- Any reject class with unresolved fallback or missing diagnostics keeps M5.3 gate open.

## FallbackObservedFlag Vocabulary

- `pending`: runtime fallback observation is not yet captured
- `false`: fallback path was not observed
- `true`: fallback path was observed

## Evidence Minimum Fields

Each runtime evidence update must include:

- test run id / execution timestamp
- reject class trigger and attempted authority path
- expected vs observed failure code
- diagnostic payload completeness check result
- fallback observation basis linked to `RejectClass`

## Records

| RejectClass | FailureCode | DiagnosticSchema | FallbackObservedFlag | EvidenceAnchor |
| --- | --- | --- | --- | --- |
| M5-HRD-001 RootPlanValidationReject | M4BOOT_PLAN_MISSING_OR_MISMATCH | `diagnosticCode,severity,ruleId,planHashExpected,planHashObserved,planVersion,sceneId,bootAttemptId,failureBoundary,timestamp` | pending | M4-CTR-001, M4-NEG-001, M5-EXE-001 |
| M5-HRD-002 RootRegistrationReject | M4BOOT_NON_PLAN_REGISTRATION_TARGET | `diagnosticCode,severity,ruleId,targetId,targetSource,planDeclarationId,sceneId,bootAttemptId,failureBoundary,timestamp` | pending | M4-CTR-002, M4-NEG-002, M4-NEG-008, M5-EXE-002 |
| M5-HRD-003 RootBuildAuthorityReject | M4BOOT_BUILD_AUTHORITY_VIOLATION | `diagnosticCode,severity,ruleId,scopeHandle,buildOwner,registerTraceId,sceneId,bootAttemptId,failureBoundary,timestamp` | pending | M4-CTR-003, M4-NEG-003, M5-EXE-003 |
| M5-HRD-004 RootActivationOrderingReject | M4BOOT_ACTIVATION_ORDER_DRIFT | `diagnosticCode,severity,ruleId,expectedOrderSignature,observedOrderSignature,planHash,sceneId,bootAttemptId,failureBoundary,timestamp` | pending | M4-CTR-004, M4-NEG-004, M5-EXE-004 |
| M5-HRD-005 RootReleaseAuthorityReject | M4BOOT_RELEASE_AUTHORITY_BYPASS | `diagnosticCode,severity,ruleId,scopeHandle,releaseOwner,fallbackProbeResult,sceneId,bootAttemptId,failureBoundary,timestamp` | pending | M4-CTR-005, M4-NEG-005, M5-EXE-005 |
| M5-HRD-006 RootFallbackReachabilityReject | M4BOOT_FALLBACK_REACHABILITY | `diagnosticCode,severity,ruleId,rejectCode,fallbackObserved,rejectHandlerId,sceneId,bootAttemptId,failureBoundary,timestamp` | pending | M4-CTR-006, M4-NEG-006, M5-EXE-006 |
| M5-HRD-007 RootDiagnosticSchemaReject | M4BOOT_DIAGNOSTIC_SCHEMA_VIOLATION | `diagnosticCode,severity,ruleId,stageName,missingFieldList,schemaVersion,sceneId,bootAttemptId,failureBoundary,timestamp` | pending | M4-CTR-007, M4-NEG-007 |

## Review Notes

- This artifact is in M5.3 start state: reject-class matrix is defined, runtime hardening verification is pending.
- Failure codes and schemas are aligned with M4 contract reject definitions to prevent divergence.
- Delete execution links are attached where reject classes are directly tied to M5.2 delete targets.

## Gate Check

- Explicit failure coverage design lock complete: [x]
- Diagnostics schema coverage design lock complete: [x]
- Silent fallback absent (runtime): [ ]
- Hardening verification approved: [ ]

# RootSceneAuthorityLeakageNegativeVerificationReport

Source Spec: [09_KernelV23M4RootSceneIntegrationCutoverExecutionSpec.md](../../09_KernelV23M4RootSceneIntegrationCutoverExecutionSpec.md)
Execution Step: M4.5 Root/Scene Authority Leakage Negative Verification
Artifact Owner: Copilot Draft (handoff required)
Last Updated: 2026-05-23
Approval State: design-ready (Case definition complete; runtime verification pending)

## ApprovalState Vocabulary

- `not-started`: required records are missing or not reviewable
- `design-ready`: all negative cases are defined and internally reviewed
- `runtime-verified`: reject/fallback runtime evidence is captured for all cases
- `approved`: reviewer sign-off is completed

## Negative Verification Policy (M4.5)

- Each negative case targets a discovery/local-authority composition attempt in root/scene accepted flow.
- `ExpectedRejectCode` must map to M4.2 contract reject codes.
- `ObservedResult` and `FallbackObservedFlag` must be updated together from runtime evidence.
- Any `fail` or `true` fallback observation keeps M4.5 gate open.

## ObservedResult Vocabulary

- `pending`: case is defined but runtime execution is not yet completed
- `pass`: expected reject is observed and fallback path remains unreachable
- `fail`: reject mismatch or fallback reachability is observed
- `blocked`: execution could not be completed due to harness/environment issue

## FallbackObservedFlag Vocabulary

- `pending`: runtime fallback observation is not yet captured
- `false`: fallback path was not observed
- `true`: fallback path was observed

## Evidence Minimum Fields

Each runtime evidence update must include:

- test run id / execution timestamp
- attempted authority path symbol and trigger payload
- expected vs observed reject code and diagnostic payload
- fallback probe result and decision basis
- pass/fail rationale linked to `NegativeCaseId`

## Records

| NegativeCaseId | TriggerCondition | ExpectedRejectCode | ObservedResult | FallbackObservedFlag | EvidenceAnchor |
| --- | --- | --- | --- | --- | --- |
| M4-NEG-001 | Root boot starts without verified plan handshake (scene-local trigger bypass) | M4BOOT_PLAN_MISSING_OR_MISMATCH | pending | pending | M4-CTR-001, M4-CUT-001 |
| M4-NEG-002 | Root registration attempts discovery-sourced non-plan target injection | M4BOOT_NON_PLAN_REGISTRATION_TARGET | pending | pending | M4-CTR-002, M4-CUT-002 |
| M4-NEG-003 | Build stage attempts non-kernel authority invocation or unregistered handle consumption | M4BOOT_BUILD_AUTHORITY_VIOLATION | pending | pending | M4-CTR-003, M4-CUT-003 |
| M4-NEG-004 | Activation ordering is bypassed via scene callback path outside plan-derived sequence | M4BOOT_ACTIVATION_ORDER_DRIFT | pending | pending | M4-CTR-004, M4-CUT-004 |
| M4-NEG-005 | Deactivate/Release path attempts local callback fallback authority | M4BOOT_RELEASE_AUTHORITY_BYPASS | pending | pending | M4-CTR-005, M4-CUT-005 |
| M4-NEG-006 | Plan mismatch reject handling continues into post-reject lifecycle path | M4BOOT_FALLBACK_REACHABILITY | pending | pending | M4-CTR-006, M4-CUT-006 |
| M4-NEG-007 | Boot lifecycle emits diagnostics with missing/invalid mandatory schema fields | M4BOOT_DIAGNOSTIC_SCHEMA_VIOLATION | pending | pending | M4-CTR-007, M4-CUT-007 |
| M4-NEG-008 | Root-to-scene transition uses scene-local registration/activation shortcut path | M4BOOT_NON_PLAN_REGISTRATION_TARGET | pending | pending | M4-CTR-002, M4-CUT-008 |

## Review Notes

- This artifact is in M4.5 start state: negative-case set is complete and runtime execution is pending.
- Cases are aligned to M4.2 reject contract and M4.3 cutover rows for closure traceability.
- Fallback reachability proof is mandatory for `pass` decision in every case.

## Gate Check

- Negative case design coverage complete: [x]
- Hard reject verified (runtime): [ ]
- Fallback unreachable (runtime): [ ]
- Negative verification approved: [ ]

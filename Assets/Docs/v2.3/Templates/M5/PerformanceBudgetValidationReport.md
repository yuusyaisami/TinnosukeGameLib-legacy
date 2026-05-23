# PerformanceBudgetValidationReport

Source Spec: [10_KernelV23M5HardeningAndDeleteExecutionSpec.md](../../10_KernelV23M5HardeningAndDeleteExecutionSpec.md)
Execution Step: M5.4 Performance Budget Validation
Artifact Owner: Copilot Draft (handoff required)
Last Updated: 2026-05-23
Approval State: design-ready (Budget case set defined; runtime metrics pending)

## ApprovalState Vocabulary

- `not-started`: budget records are missing or not reviewable
- `design-ready`: budget records are complete and internally reviewed
- `runtime-verified`: runtime metric evidence is captured and pass/fail is fixed
- `approved`: reviewer sign-off is completed

## Budget Validation Policy (M5.4)

- Post-delete/hardening verification must compare baseline and current metrics on critical runtime hot paths.
- Any unresolved regression with `BudgetPassFail=fail` keeps M5.4 gate open.
- Metrics must be collected under reproducible run conditions and attached to evidence.

## BudgetPassFail Vocabulary

- `pending`: measurement is not yet completed
- `pass`: current metric is within allowed budget threshold
- `fail`: current metric violates budget threshold
- `blocked`: measurement could not complete due to harness/environment issue

## RegressionRisk Vocabulary

- `pending`: risk evaluation not completed
- `low`: no material regression risk identified
- `medium`: watchlist regression risk requiring follow-up
- `high`: budget violation risk likely or observed

## Metric Format

Use this normalized format for metrics:

- `time_ms=<value>` for elapsed-time based cases
- `alloc_bytes=<value>` for allocation based cases
- `cpu_ms=<value>` for CPU budget cases
- Use `pending-capture` when metrics are not yet collected

## Budget Threshold Rules

Pass/fail must be decided by the following normalized rules:

- time and cpu metrics: `current <= baseline * 1.05` (max +5%)
- allocation metrics: `current <= baseline * 1.03` (max +3%)
- if baseline is missing or invalid, `BudgetPassFail=blocked`
- if any metric in a case fails the threshold, case-level `BudgetPassFail=fail`

## Measurement Protocol (Reproducibility)

Each runtime measurement batch must lock these conditions:

- build profile and scripting backend
- target platform/device profile
- sample count: 30 runs per case
- warm-up count: 5 runs excluded from aggregation
- aggregation rule: median for time/cpu, p95 for alloc
- outlier handling: single-run spikes are kept (no silent drop)

## Evidence Minimum Fields

Each runtime evidence update must include:

- test run id / execution timestamp
- environment fingerprint (build profile, platform, backend)
- baseline source id and capture timestamp
- raw sample summary (count, warm-up count, aggregation result)
- threshold evaluation result per metric and final case decision

## Records

| BudgetCaseId | HotPathName | BaselineMetric | CurrentMetric | BudgetPassFail | RegressionRisk |
| --- | --- | --- | --- | --- | --- |
| M5-BGT-001 | Root boot plan validation + register/build/activate lifecycle hot path | time_ms=pending-capture; alloc_bytes=pending-capture | time_ms=pending-capture; alloc_bytes=pending-capture | pending | pending |
| M5-BGT-002 | Root registration target expansion guard and non-plan rejection hot path | cpu_ms=pending-capture; alloc_bytes=pending-capture | cpu_ms=pending-capture; alloc_bytes=pending-capture | pending | pending |
| M5-BGT-003 | Build authority rejection guard path (`M4BOOT_BUILD_AUTHORITY_VIOLATION`) | time_ms=pending-capture; alloc_bytes=pending-capture | time_ms=pending-capture; alloc_bytes=pending-capture | pending | pending |
| M5-BGT-004 | Activation ordering signature verification path | time_ms=pending-capture; cpu_ms=pending-capture | time_ms=pending-capture; cpu_ms=pending-capture | pending | pending |
| M5-BGT-005 | Deactivate/release fallback rejection path (`M4BOOT_RELEASE_AUTHORITY_BYPASS`) | time_ms=pending-capture; alloc_bytes=pending-capture | time_ms=pending-capture; alloc_bytes=pending-capture | pending | pending |
| M5-BGT-006 | Fallback reachability reject path (`M4BOOT_FALLBACK_REACHABILITY`) | time_ms=pending-capture; cpu_ms=pending-capture | time_ms=pending-capture; cpu_ms=pending-capture | pending | pending |
| M5-BGT-007 | Diagnostics schema completeness verification path | time_ms=pending-capture; alloc_bytes=pending-capture | time_ms=pending-capture; alloc_bytes=pending-capture | pending | pending |

## Evidence Anchors

- M5-BGT-001 -> M5-EXE-001, M5-HRD-001
- M5-BGT-002 -> M5-EXE-002, M5-HRD-002
- M5-BGT-003 -> M5-EXE-003, M5-HRD-003
- M5-BGT-004 -> M5-EXE-004, M5-HRD-004
- M5-BGT-005 -> M5-EXE-005, M5-HRD-005
- M5-BGT-006 -> M5-EXE-006, M5-HRD-006
- M5-BGT-007 -> M5-HRD-007

## Threshold Evaluation Map

| BudgetCaseId | ThresholdProfile | DecisionRule |
| --- | --- | --- |
| M5-BGT-001 | perf-default-v1 | pass only if all metrics satisfy threshold rules |
| M5-BGT-002 | perf-default-v1 | pass only if all metrics satisfy threshold rules |
| M5-BGT-003 | perf-default-v1 | pass only if all metrics satisfy threshold rules |
| M5-BGT-004 | perf-default-v1 | pass only if all metrics satisfy threshold rules |
| M5-BGT-005 | perf-default-v1 | pass only if all metrics satisfy threshold rules |
| M5-BGT-006 | perf-default-v1 | pass only if all metrics satisfy threshold rules |
| M5-BGT-007 | perf-default-v1 | pass only if all metrics satisfy threshold rules |

## Review Notes

- This artifact is in M5.4 start state: case matrix is defined and runtime metrics are pending.
- Cases are aligned to M5.2 delete execution rows and M5.3 hardening reject classes.
- Metric units and placeholders are normalized to avoid mixed-format audit failures.

## Gate Check

- Budget case design coverage complete: [x]
- Budget conformance verified (runtime): [ ]
- Unresolved regression absent (runtime): [ ]
- Budget validation approved: [ ]

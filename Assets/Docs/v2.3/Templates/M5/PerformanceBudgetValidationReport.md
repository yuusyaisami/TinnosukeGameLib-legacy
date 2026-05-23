# PerformanceBudgetValidationReport

Source Spec: [10_KernelV23M5HardeningAndDeleteExecutionSpec.md](../../10_KernelV23M5HardeningAndDeleteExecutionSpec.md)
実行 Step: M5.4 Performance Budget 検証
成果物担当: Copilot 下書き (handoff 必須)
最終更新日: 2026-05-23
承認状態: design-ready (Budget case set defined; 実行時 metrics pending)

## 承認状態語彙

- `not-started`: budget レコード are missing or not reviewable
- `design-ready`: budget レコード are 完了 and internally reviewed
- `実行時-verified`: 実行時 metric 証拠 is captured and pass/fail is fixed
- `承認済み`: reviewer sign-off is completed

## 予算検証方針（M5.4）

- Post-delete/hardening 検証 必須である compare baseline and current metrics on critical 実行時 hot paths.
- Any unresolved regression with `BudgetPassFail=fail` keeps M5.4 ゲート open.
- Metrics 必須である be collected under reproducible run conditions and attached to 証拠.

## BudgetPassFail 語彙

- `pending`: measurement is not yet completed
- `pass`: current metric is within allowed budget threshold
- `fail`: current metric violates budget threshold
- `ブロック`: measurement could not 完了 due to harness/environment issue

## RegressionRisk 語彙

- `pending`: risk evaluation not completed
- `low`: no material regression risk identified
- `medium`: watchlist regression risk requiring follow-up
- `high`: budget violation risk likely or observed

## メトリクス形式

Use this normalized format for metrics:

- `time_ms=<value>` for elapsed-time based cases
- `alloc_bytes=<value>` for allocation based cases
- `cpu_ms=<value>` for CPU budget cases
- Use `pending-capture` when metrics are not yet collected

## 予算閾値規則

Pass/fail 必須である be decided by the following normalized rules:

- time and cpu metrics: `current <= baseline * 1.05` (max +5%)
- allocation metrics: `current <= baseline * 1.03` (max +3%)
- if baseline is missing or invalid, `BudgetPassFail=ブロック`
- if any metric in a case fails the threshold, case-level `BudgetPassFail=fail`

## 計測プロトコル（再現性）

Each 実行時 measurement batch 必須である lock these conditions:

- build profile and scripting backend
- target platform/device profile
- sample count: 30 runs per case
- warm-up count: 5 runs excluded from aggregation
- aggregation rule: median for time/cpu, p95 for alloc
- outlier handling: single-run spikes are kept (no silent drop)

## 証拠最小項目

Each 実行時 証拠 update 必須である include:

- test run id / 実行 timestamp
- environment fingerprint (build profile, platform, backend)
- baseline source id and capture timestamp
- raw sample summary (count, warm-up count, aggregation result)
- threshold evaluation result per metric and final case decision

## レコード

| BudgetCaseId | HotPathName | BaselineMetric | CurrentMetric | BudgetPassFail | RegressionRisk |
| --- | --- | --- | --- | --- | --- |
| M5-BGT-001 | Root boot plan validation + register/build/activate lifecycle hot path | time_ms=pending-capture; alloc_bytes=pending-capture | time_ms=pending-capture; alloc_bytes=pending-capture | pending | pending |
| M5-BGT-002 | Root registration target expansion guard and non-plan rejection hot path | cpu_ms=pending-capture; alloc_bytes=pending-capture | cpu_ms=pending-capture; alloc_bytes=pending-capture | pending | pending |
| M5-BGT-003 | Build authority rejection guard path (`M4BOOT_BUILD_AUTHORITY_VIOLATION`) | time_ms=pending-capture; alloc_bytes=pending-capture | time_ms=pending-capture; alloc_bytes=pending-capture | pending | pending |
| M5-BGT-004 | Activation ordering signature 検証 path | time_ms=pending-capture; cpu_ms=pending-capture | time_ms=pending-capture; cpu_ms=pending-capture | pending | pending |
| M5-BGT-005 | Deactivate/release フォールバック rejection path (`M4BOOT_RELEASE_AUTHORITY_BYPASS`) | time_ms=pending-capture; alloc_bytes=pending-capture | time_ms=pending-capture; alloc_bytes=pending-capture | pending | pending |
| M5-BGT-006 | フォールバック reachability 拒否 path (`M4BOOT_FALLBACK_REACHABILITY`) | time_ms=pending-capture; cpu_ms=pending-capture | time_ms=pending-capture; cpu_ms=pending-capture | pending | pending |
| M5-BGT-007 | Diagnostics schema completeness 検証 path | time_ms=pending-capture; alloc_bytes=pending-capture | time_ms=pending-capture; alloc_bytes=pending-capture | pending | pending |

## 証拠アンカー

- M5-BGT-001 -> M5-EXE-001, M5-HRD-001
- M5-BGT-002 -> M5-EXE-002, M5-HRD-002
- M5-BGT-003 -> M5-EXE-003, M5-HRD-003
- M5-BGT-004 -> M5-EXE-004, M5-HRD-004
- M5-BGT-005 -> M5-EXE-005, M5-HRD-005
- M5-BGT-006 -> M5-EXE-006, M5-HRD-006
- M5-BGT-007 -> M5-HRD-007

## 閾値評価マップ

| BudgetCaseId | ThresholdProfile | DecisionRule |
| --- | --- | --- |
| M5-BGT-001 | perf-default-v1 | pass only if all metrics satisfy threshold rules |
| M5-BGT-002 | perf-default-v1 | pass only if all metrics satisfy threshold rules |
| M5-BGT-003 | perf-default-v1 | pass only if all metrics satisfy threshold rules |
| M5-BGT-004 | perf-default-v1 | pass only if all metrics satisfy threshold rules |
| M5-BGT-005 | perf-default-v1 | pass only if all metrics satisfy threshold rules |
| M5-BGT-006 | perf-default-v1 | pass only if all metrics satisfy threshold rules |
| M5-BGT-007 | perf-default-v1 | pass only if all metrics satisfy threshold rules |

## レビューノート

- This artifact is in M5.4 start state: case matrix is defined and 実行時 metrics are pending.
- Cases are aligned to M5.2 delete 実行 rows and M5.3 hardening 拒否 classes.
- Metric units and placeholders are normalized to avoid mixed-format audit failures.

## ゲートチェック

- Budget case design coverage 完了: [x]
- Budget conformance verified (実行時): [ ]
- Unresolved regression absent (実行時): [ ]
- Budget validation 承認済み: [ ]





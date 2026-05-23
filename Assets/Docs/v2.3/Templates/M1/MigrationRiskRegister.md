# MigrationRiskRegister

Source Spec: [06_KernelV23M1SpecLockAndCensusExecutionSpec.md](../../06_KernelV23M1SpecLockAndCensusExecutionSpec.md)
実行 Step: M1.5 Risk and M2 ゲート Baseline
成果物担当: Copilot 下書き (handoff 必須)
最終更新日: 2026-05-23
承認状態: 下書き

## リスクレコード

| RiskId | RiskDescription | Severity | MitigationPlan | 担当 |
| --- | --- | --- | --- | --- |
| M1-RISK-001 | Local installer projection (`InstallLocalFeatures`) can re-enable スコープ-owned 実行権限 in accepted path. | high | Disable 許可経路 local projection in M2; enforce hard 拒否 path and verified contribution-only install route. | Core 実行時 担当 |
| M1-RISK-002 | Legacy LTS `LifetimeScope` classes remain and may be referenced by scenes/prefabs. | high | Maintain temporary 互換 shell for serialization continuity only; prohibit 実行権限 and schedule physical delete in M5.2. | Legacy 移行 担当 |
| M1-RISK-003 | Resolver-coupled MB callbacks (`TryResolve` in 実行時 callbacks) cause hidden authority coupling. | high | Move 実行時 binding logic to kernel-managed handlers/services; MB becomes 宣言-only signal source. | Interaction 実行時 担当 |
| M1-RISK-004 | サービス family target form mismatch (AoS vs 範囲-ServiceInstance) can cause rework and delay. | medium | Freeze target forms in M1.4 在庫; require explicit variance approval before M2 implementation starts. | Architecture 担当 |
| M1-RISK-005 | Reference continuity break during 互換 shell retirement for scene flow and selection families. | high | Add continuity validation ゲートs before M5.5 retirements; block deletion when unresolved reference diffs exist. | Scene Flow 担当 |
| M1-RISK-006 | Performance regressions after authority isolation and hard 拒否 insertion on hot paths. | medium | Add M5.4 baseline/perf diff checks and treat unresolved budget violations as release blockers. | Performance 担当 |
| M1-RISK-007 | Incomplete 証拠 package may allow premature M2 start. | medium | Enforce M2EntryGate package approval workflow; block start unless all 必須 artifacts are present and 承認済み. | Program 担当 |

## リスクサマリー

- Total risks: 7
- High: 4
- Medium: 3
- Low: 0

## 完了チェック（M1.5）

- 移行 blocker taxonomy defined: [x]
- M2 entry ゲートs defined from M1 outputs: [x]
- 拒否 triggers for hidden/unclassified 旧系 authority defined: [x]

## レビュー承認

- Reviewer:
- Review date:
- Decision: Approve / 拒否 / Conditional
- Notes:





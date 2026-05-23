# MigrationRiskRegister

Source Spec: [06_KernelV23M1SpecLockAndCensusExecutionSpec.md](../../06_KernelV23M1SpecLockAndCensusExecutionSpec.md)
Execution Step: M1.5 Risk and M2 Gate Baseline
Artifact Owner: Copilot Draft (handoff required)
Last Updated: 2026-05-23
Approval State: Draft

## Risk Records

| RiskId | RiskDescription | Severity | MitigationPlan | Owner |
| --- | --- | --- | --- | --- |
| M1-RISK-001 | Local installer projection (`InstallLocalFeatures`) can re-enable scope-owned runtime authority in accepted path. | high | Disable accepted-path local projection in M2; enforce hard reject path and verified contribution-only install route. | Core Runtime Owner |
| M1-RISK-002 | Legacy LTS `LifetimeScope` classes remain and may be referenced by scenes/prefabs. | high | Maintain temporary compatibility shell for serialization continuity only; prohibit runtime authority and schedule physical delete in M5.2. | Legacy Migration Owner |
| M1-RISK-003 | Resolver-coupled MB callbacks (`TryResolve` in runtime callbacks) cause hidden authority coupling. | high | Move runtime binding logic to kernel-managed handlers/services; MB becomes declaration-only signal source. | Interaction Runtime Owner |
| M1-RISK-004 | Service family target form mismatch (AoS vs Scope-ServiceInstance) can cause rework and delay. | medium | Freeze target forms in M1.4 inventory; require explicit variance approval before M2 implementation starts. | Architecture Owner |
| M1-RISK-005 | Reference continuity break during compatibility shell retirement for scene flow and selection families. | high | Add continuity validation gates before M5.5 retirements; block deletion when unresolved reference diffs exist. | Scene Flow Owner |
| M1-RISK-006 | Performance regressions after authority isolation and hard reject insertion on hot paths. | medium | Add M5.4 baseline/perf diff checks and treat unresolved budget violations as release blockers. | Performance Owner |
| M1-RISK-007 | Incomplete evidence package may allow premature M2 start. | medium | Enforce M2EntryGate package approval workflow; block start unless all required artifacts are present and approved. | Program Owner |

## Risk Summary

- Total risks: 7
- High: 4
- Medium: 3
- Low: 0

## Exit Check (M1.5)

- Migration blocker taxonomy defined: [x]
- M2 entry gates defined from M1 outputs: [x]
- Reject triggers for hidden/unclassified legacy authority defined: [x]

## Reviewer Sign-off

- Reviewer:
- Review date:
- Decision: Approve / Reject / Conditional
- Notes:

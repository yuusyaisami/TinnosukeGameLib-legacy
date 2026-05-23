# M2EntryGate package

Source Spec: [06_KernelV23M1SpecLockAndCensusExecutionSpec.md](../../06_KernelV23M1SpecLockAndCensusExecutionSpec.md)
実行 Step: M1.5 Risk and M2 ゲート Baseline
成果物担当: Copilot 下書き (handoff 必須)
最終更新日: 2026-05-23
承認状態: 下書き (Pending approval)

## ゲートレコード

| GateItemId | RequiredArtifact | PresenceFlag | ApprovalState | BlockingCondition |
| --- | --- | --- | --- | --- |
| M2-GATE-001 | RuleLockVerificationReport | yes | pending-review | M1.1 reviewer sign-off missing |
| M2-GATE-002 | AuthorityPathCensus | yes | pending-review | Any unknown 担当 class or unanchored path remains |
| M2-GATE-003 | MBResponsibilityClassification | yes | pending-review | Any 実行時-affecting MB family lacks RequiredAction |
| M2-GATE-004 | ServiceFamilyInventory | yes | pending-review | Any サービスファミリー missing 担当 or target form |
| M2-GATE-005 | MigrationRiskRegister | yes | pending-review | High severity risks without mitigation 担当 |

## 拒否トリガー基準

| TriggerId | TriggerCondition | DetectionMethod | GateImpact |
| --- | --- | --- | --- |
| M2-REJECT-001 | Hidden スコープ-local DI authority path discovered after census freeze | Code review + targeted search (`LifetimeScope`, `InstallLocalFeatures`, dynamic スコープ resolver walks) | M2 start ブロック |
| M2-REJECT-002 | Any 許可経路 MB still depends on 実行時 installer discovery | MB 実行時 callback review + build path trace | M2 start ブロック |
| M2-REJECT-003 | サービス family 在庫 and authority census disagree on 担当/form | Cross-テーブル consistency check | M2 start ブロック |
| M2-REJECT-004 | High risk family lacks mitigation and 担当 | Risk register validation | M2 start ブロック |

## ゲート判定

- M2 start allowed: [ ]
- Decision 担当:
- Decision date:
- Notes:





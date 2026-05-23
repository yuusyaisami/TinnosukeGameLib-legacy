# DeclarationToCommandMappingTable

Source Spec: [07_KernelV23M2KernelCommandSurfaceExecutionSpec.md](../../07_KernelV23M2KernelCommandSurfaceExecutionSpec.md)
実行 Step: M2.2 Declaration-to-Command Deterministic Mapping
成果物担当: Copilot 下書き (handoff 必須)
最終更新日: 2026-05-23
承認状態: 下書き (Ready for reviewer review)

## 対応規則（M2.2 ロック）

- Mapping key is exactly `(DeclarationSelector, TargetServiceForm)`; no implicit フォールバック selector is allowed.
- CommandSequence uses M2.1 locked names only: `KernelScope.Register`, `KernelScope.Build`, `KernelScope.Activate`, `KernelScope.Deactivate`, `KernelScope.Release`.
- Missing selector, malformed payload, undeclared target, or form mismatch is hard-拒否.
- Any path requiring local DI authority in accepted flow is hard-拒否.

## レコード

| MappingId | DeclarationSelector | TargetServiceForm | CommandSequence | DeterminismConstraint | RejectCondition |
| --- | --- | --- | --- | --- | --- |
| M2-MAP-001 | `ScopeDeclaration.Kind in {Project, Platform, Scene, 項目, Entity}` + `LifecycleIntent=Full` | 範囲-ServiceInstance | `KernelScope.Register -> KernelScope.Build -> KernelScope.Activate -> KernelScope.Deactivate -> KernelScope.Release` | fixed 1:1 mapping by `(Kind, Form, LifecycleIntent)`; no 実行時 branch by scene hierarchy discovery | 拒否 when 宣言 hash invalid, スコープ handle undeclared, form absent, or local installer projection 必須 |
| M2-MAP-002 | `ScopeDeclaration.Kind in {Project, Platform, Scene, 項目, Entity}` + `LifecycleIntent=StartActive` | 範囲-ServiceInstance | `KernelScope.Register -> KernelScope.Build -> KernelScope.Activate` | activation inclusion depends only on explicit `LifecycleIntent`; no hidden activation from MB callback | 拒否 when activation requested before successful build, duplicate conflicting 宣言, or authority check fails |
| M2-MAP-003 | `ServiceDeclaration.Category=RuntimeObjectSet` + `RuntimeObjectPolicy=KernelOwned` | AoS | `KernelScope.Register -> KernelScope.Build -> KernelScope.Activate` | AoS branch selected only by explicit form marker; no implicit conversion from スコープ-サービス 宣言 | 拒否 when 宣言 requests スコープ-local mutable ownership, target slot not declared, or payload schema mismatch |
| M2-MAP-004 | `ServiceDeclaration.Category=RuntimeObjectSet` + `LifecycleIntent=TeardownOnly` | AoS | `KernelScope.Deactivate -> KernelScope.Release` | teardown path is valid only for previously activated 宣言 with matching request lineage | 拒否 when prior activation lineage missing, terminal state already reached with conflicting request, or undeclared target referenced |
| M2-MAP-005 | `AuthoringBridge=CommandRunnerAuthoring` + `ContributionType=AcceptedPath` | 範囲-ServiceInstance | `KernelScope.Register -> KernelScope.Build` | accepted authoring bridge cannot choose alternate command order; contribution normalization is pre-mapping requirement | 拒否 when contribution is unnormalized, references 旧系 key フォールバック, or requires direct resolver authority |
| M2-MAP-006 | `AuthoringBridge=BlackboardAuthoring` + `ContributionType=AcceptedPath` | 範囲-ServiceInstance | `KernelScope.Register -> KernelScope.Build` | deterministic by normalized 宣言 digest; same digest 必須である always yield same sequence and same target form | 拒否 when value schema reference missing, 宣言 malformed, or mapping attempts non-deterministic branch selection |
| M2-MAP-007 | `VerificationDirective=AuthorityIsolationProbe` + `ProbeType=Negative` | 範囲-ServiceInstance | `KernelScope.Register -> KernelScope.Build` then hard-拒否 on authority request | negative path is fixed: rejection occurs at first authority-violation detection point with no recovery command injection | 拒否 immediately when accepted path touches local DI source (`LifetimeScope`, `InstallLocalFeatures`, resolver bypass path); フォールバック ブロック |
| M2-MAP-008 | `CompatibilityShellIntent=SerializationContinuityOnly` | 範囲-ServiceInstance | `KernelScope.Register -> KernelScope.Build` (no activate unless explicit lifecycle 宣言 exists) | 互換 shell has strictly 宣言-only mapping; 実行権限 cannot be inferred from shell presence | 拒否 when shell attempts 実行時 composition authority, hidden activate/deactivate side effects, or 旧系 container build |

## レビューノート

- Mapping rows intentionally separate AoS and 範囲-ServiceInstance to prevent implicit conversion.
- RejectCondition vocabulary is aligned with M2 no-フォールバック and explicit-失敗 rules.
- This テーブル is contract-level and 必須である be updated before adding any new DeclarationSelector class.

## ゲートチェック

- Design deterministic mapping verified: [x]
- Design フォールバック path absent: [x]
- 実行時 determinism verified: [ ]
- 実行時 フォールバック absence verified: [ ]
- 承認済み: [ ]





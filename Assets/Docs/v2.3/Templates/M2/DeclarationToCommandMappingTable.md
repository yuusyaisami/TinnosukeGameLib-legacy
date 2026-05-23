# DeclarationToCommandMappingTable

Source Spec: [07_KernelV23M2KernelCommandSurfaceExecutionSpec.md](../../07_KernelV23M2KernelCommandSurfaceExecutionSpec.md)
Execution Step: M2.2 Declaration-to-Command Deterministic Mapping
Artifact Owner: Copilot Draft (handoff required)
Last Updated: 2026-05-23
Approval State: Draft (Ready for reviewer review)

## Mapping Rules (M2.2 Lock)

- Mapping key is exactly `(DeclarationSelector, TargetServiceForm)`; no implicit fallback selector is allowed.
- CommandSequence uses M2.1 locked names only: `KernelScope.Register`, `KernelScope.Build`, `KernelScope.Activate`, `KernelScope.Deactivate`, `KernelScope.Release`.
- Missing selector, malformed payload, undeclared target, or form mismatch is hard-reject.
- Any path requiring local DI authority in accepted flow is hard-reject.

## Records

| MappingId | DeclarationSelector | TargetServiceForm | CommandSequence | DeterminismConstraint | RejectCondition |
| --- | --- | --- | --- | --- | --- |
| M2-MAP-001 | `ScopeDeclaration.Kind in {Project, Platform, Scene, Field, Entity}` + `LifecycleIntent=Full` | Scope-ServiceInstance | `KernelScope.Register -> KernelScope.Build -> KernelScope.Activate -> KernelScope.Deactivate -> KernelScope.Release` | fixed 1:1 mapping by `(Kind, Form, LifecycleIntent)`; no runtime branch by scene hierarchy discovery | reject when declaration hash invalid, scope handle undeclared, form absent, or local installer projection required |
| M2-MAP-002 | `ScopeDeclaration.Kind in {Project, Platform, Scene, Field, Entity}` + `LifecycleIntent=StartActive` | Scope-ServiceInstance | `KernelScope.Register -> KernelScope.Build -> KernelScope.Activate` | activation inclusion depends only on explicit `LifecycleIntent`; no hidden activation from MB callback | reject when activation requested before successful build, duplicate conflicting declaration, or authority check fails |
| M2-MAP-003 | `ServiceDeclaration.Category=RuntimeObjectSet` + `RuntimeObjectPolicy=KernelOwned` | AoS | `KernelScope.Register -> KernelScope.Build -> KernelScope.Activate` | AoS branch selected only by explicit form marker; no implicit conversion from scope-service declaration | reject when declaration requests scope-local mutable ownership, target slot not declared, or payload schema mismatch |
| M2-MAP-004 | `ServiceDeclaration.Category=RuntimeObjectSet` + `LifecycleIntent=TeardownOnly` | AoS | `KernelScope.Deactivate -> KernelScope.Release` | teardown path is valid only for previously activated declaration with matching request lineage | reject when prior activation lineage missing, terminal state already reached with conflicting request, or undeclared target referenced |
| M2-MAP-005 | `AuthoringBridge=CommandRunnerAuthoring` + `ContributionType=AcceptedPath` | Scope-ServiceInstance | `KernelScope.Register -> KernelScope.Build` | accepted authoring bridge cannot choose alternate command order; contribution normalization is pre-mapping requirement | reject when contribution is unnormalized, references legacy key fallback, or requires direct resolver authority |
| M2-MAP-006 | `AuthoringBridge=BlackboardAuthoring` + `ContributionType=AcceptedPath` | Scope-ServiceInstance | `KernelScope.Register -> KernelScope.Build` | deterministic by normalized declaration digest; same digest must always yield same sequence and same target form | reject when value schema reference missing, declaration malformed, or mapping attempts non-deterministic branch selection |
| M2-MAP-007 | `VerificationDirective=AuthorityIsolationProbe` + `ProbeType=Negative` | Scope-ServiceInstance | `KernelScope.Register -> KernelScope.Build` then hard-reject on authority request | negative path is fixed: rejection occurs at first authority-violation detection point with no recovery command injection | reject immediately when accepted path touches local DI source (`LifetimeScope`, `InstallLocalFeatures`, resolver bypass path); fallback blocked |
| M2-MAP-008 | `CompatibilityShellIntent=SerializationContinuityOnly` | Scope-ServiceInstance | `KernelScope.Register -> KernelScope.Build` (no activate unless explicit lifecycle declaration exists) | compatibility shell has strictly declaration-only mapping; runtime authority cannot be inferred from shell presence | reject when shell attempts runtime composition authority, hidden activate/deactivate side effects, or legacy container build |

## Review Notes

- Mapping rows intentionally separate AoS and Scope-ServiceInstance to prevent implicit conversion.
- RejectCondition vocabulary is aligned with M2 no-fallback and explicit-failure rules.
- This table is contract-level and must be updated before adding any new DeclarationSelector class.

## Gate Check

- Design deterministic mapping verified: [x]
- Design fallback path absent: [x]
- Runtime determinism verified: [ ]
- Runtime fallback absence verified: [ ]
- Approved: [ ]

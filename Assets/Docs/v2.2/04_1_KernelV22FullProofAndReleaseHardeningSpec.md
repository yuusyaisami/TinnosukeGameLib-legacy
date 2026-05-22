# Kernel v2.2 Full Proof and Release Hardening Specification

## Document Status

- Document ID: 04_1_KernelV22FullProofAndReleaseHardeningSpec
- Status: Draft
- Role: defines the M6 full-proof aggregation and release-claim contract that turns the separated M1 through M5 proof families and executable gates into one auditable completion decision
- Depends on:
  - [00_KernelV22CompletionOverviewSpec.md](00_KernelV22CompletionOverviewSpec.md)
  - [02_KernelV22KernelOnlyHostSpec.md](02_KernelV22KernelOnlyHostSpec.md)
  - [02_1_KernelV22CommandAndValueHostRemovalSpec.md](02_1_KernelV22CommandAndValueHostRemovalSpec.md)
  - [03_KernelV22ServiceFamilyCutoverSpec.md](03_KernelV22ServiceFamilyCutoverSpec.md)
  - [03_1_KernelV22RepresentativeGameplayAndApplicationCutoverSpec.md](03_1_KernelV22RepresentativeGameplayAndApplicationCutoverSpec.md)
  - [04_KernelV22LegacyDeletionAndHardeningSpec.md](04_KernelV22LegacyDeletionAndHardeningSpec.md)
  - [05_KernelV22MilestoneOrderSpec.md](05_KernelV22MilestoneOrderSpec.md)
  - [Index/KernelV22ProofAnchorCatalog.md](Index/KernelV22ProofAnchorCatalog.md)
  - [../v2/11_DebugMapAndDiagnosticsSpec.md](../v2/11_DebugMapAndDiagnosticsSpec.md)
  - [../v2/14_PerformanceBudgetAndRuntimeRulesSpec.md](../v2/14_PerformanceBudgetAndRuntimeRulesSpec.md)
  - [../v2/15_TestAndValidationSpec.md](../v2/15_TestAndValidationSpec.md)
  - [../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md](../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md)
  - [../v2.1/Index/KernelV21ProofAnchorCatalog.md](../v2.1/Index/KernelV21ProofAnchorCatalog.md)
- Provides foundation for:
  - [05_KernelV22MilestoneOrderSpec.md](05_KernelV22MilestoneOrderSpec.md)
  - [Index/KernelV22ProofAnchorCatalog.md](Index/KernelV22ProofAnchorCatalog.md)

### Revision Note

This revision creates the dedicated M6 specification that the v2.2 package was still missing.

M5 already fixed deletion pressure, bounded residue, compile-boundary direction, and hardening gates.
That was necessary, but it did not yet make release completion claimable.

The missing owner was the document that answers the final question:

```text
Can the repository prove, under one reviewable bundle,
that release would reject regressions across startup, representative gameplay,
legacy re-entry, compile-boundary inversion, diagnostics drift, and performance drift?
```

Without that owner, the package could still collapse into cherry-picked green signals such as:

- a green doc package with no executable closure
- a green smoke pass with missing lower-layer gates
- a green M5 hardening bundle treated as if it already closed M6

04_1 closes that gap.

---

## Ownership

This specification owns:

- the M6 full-proof aggregation contract
- the mandatory release-claim bundle across proof families and executable gate classes
- the rule that M6 consumes earlier proof families without collapsing them into one interchangeable signal
- the release-claim report shape and failure-aggregation requirements
- the profile rules that decide whether the proved bundle is credible for Release
- the final acceptance and non-completion rules for v2.2 release completion

This specification does not own:

- the host, command/value, family, representative gameplay/application, or residue-deletion semantics already owned by M1 through M5
- performance budget meaning already owned by [../v2/14_PerformanceBudgetAndRuntimeRulesSpec.md](../v2/14_PerformanceBudgetAndRuntimeRulesSpec.md)
- diagnostics semantics already owned by [../v2/11_DebugMapAndDiagnosticsSpec.md](../v2/11_DebugMapAndDiagnosticsSpec.md)
- the gate-layer taxonomy already owned by [../v2/15_TestAndValidationSpec.md](../v2/15_TestAndValidationSpec.md)
- a blanket claim that every unrelated test in the repository must be green before the kernel release-completion bundle can be reviewed

04_1 owns final proof aggregation.
It must not reopen earlier milestone ownership or weaken lower-layer gates just to make the bundle easier to close.

---

## Purpose

The purpose of this document is to define what final release proof means after the runtime cutover work is already individually claimable.

Core statements:

```text
M6 is not another migration slice.
It is the point where earlier proof families become one release claim.

A green smoke run is not enough.
A green hardening gate is not enough.
A green doc package is not enough.

Release completion is credible only when required gate classes remain green together,
fail closed together,
and produce reviewable evidence about why the claim passes or fails.
```

The required final rule inherited from [../v2/15_TestAndValidationSpec.md](../v2/15_TestAndValidationSpec.md) remains in force:

```text
The architecture is accepted only when it can prove that it would reject its own regressions.
```

---

## Scope

This specification defines:

- the proof families that M6 must consume from earlier milestones
- the required executable gate bundle that must support the release claim
- the real live-boot, representative gameplay/application, diagnostics, static, legacy, compile-boundary, and performance anchors that the bundle must exercise
- the release-profile and report-shape rules for final acceptance
- the acceptance criteria and non-completion rules for v2.2 release completion

---

## Non-Goals

This specification does not define:

- a new host, command, value, service-family, or gameplay migration slice
- a replacement for M5 deletion and quarantine policy
- new budget numbers, diagnostics semantics, or asmdef semantics beyond the upstream v2 owners
- permission to skip lower-layer gates because a later runtime smoke happened to pass once
- a report format that hides which gate class actually failed

M6 aggregates proof.
It does not reinterpret the lower specs or excuse missing evidence.

---

## Relationship to Other Specs

| Spec | Relationship to 04_1 |
| --- | --- |
| [02_KernelV22KernelOnlyHostSpec.md](02_KernelV22KernelOnlyHostSpec.md) | Supplies the accepted live-boot proof that M6 must consume rather than replace with direct-play-only success. |
| [02_1_KernelV22CommandAndValueHostRemovalSpec.md](02_1_KernelV22CommandAndValueHostRemovalSpec.md) | Supplies the command/value authority proof that M6 must keep explicit inside the final bundle. |
| [03_1_KernelV22RepresentativeGameplayAndApplicationCutoverSpec.md](03_1_KernelV22RepresentativeGameplayAndApplicationCutoverSpec.md) | Supplies the real GameScene gameplay/application proof that M6 must consume rather than dilute into generic playability. |
| [04_KernelV22LegacyDeletionAndHardeningSpec.md](04_KernelV22LegacyDeletionAndHardeningSpec.md) | Supplies the explicit residue, quarantine, compile-boundary, and hardening state that M6 must aggregate rather than re-litigate. |
| [Index/KernelV22ProofAnchorCatalog.md](Index/KernelV22ProofAnchorCatalog.md) | Separates the proof families that M6 must intake without collapsing them into one interchangeable signal. |
| [../v2/11_DebugMapAndDiagnosticsSpec.md](../v2/11_DebugMapAndDiagnosticsSpec.md) | Owns diagnostics semantics and provenance rules that M6 must require as evidence rather than as comments. |
| [../v2/14_PerformanceBudgetAndRuntimeRulesSpec.md](../v2/14_PerformanceBudgetAndRuntimeRulesSpec.md) | Owns marker, allocation, forbidden-operation, and regression-budget meaning that M6 must consume as executable evidence. |
| [../v2/15_TestAndValidationSpec.md](../v2/15_TestAndValidationSpec.md) | Owns the gate-layer model and final acceptance rule that M6 must apply to the v2.2 completion claim. |
| [../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md](../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md) | Owns the target compile graph that M6 must treat as already enforced, not merely discussed. |
| [05_KernelV22MilestoneOrderSpec.md](05_KernelV22MilestoneOrderSpec.md) | Owns claim order and the M6 claim rule that consumes this specification. |

04_1 is downstream of the earlier proofs.
It must not use final aggregation as an excuse to blur their boundaries.

---

## Consumed Proof Families

M6 consumes the same five proof families fixed by [Index/KernelV22ProofAnchorCatalog.md](Index/KernelV22ProofAnchorCatalog.md).
It does not introduce a sixth family that erases them.

| Proof family | M6 intake rule | Not enough by itself |
| --- | --- | --- |
| Spec package proof | required for traceability and claim-surface consistency | doc-marker presence alone does not prove release completion |
| Live kernel-only host proof | required to prove accepted live startup uses kernel-owned host truth | direct play by itself does not close M6 |
| Command/value host-removal proof | required to prove accepted execution still does not route back through scene-facing command/value authority | host-removal proof alone does not prove gameplay/application or release hardening |
| Service-family cutover proof | required to prove real GameScene representative behavior consumes migrated authority | gameplay/application success alone does not prove residue deletion, static-rule, legacy, or performance closure |
| Release hardening proof | required to prove deletion, bounded residue, compile-boundary direction, and forbidden-pattern rejection | M5 hardening alone does not close M6 final aggregation |

Inherited direct-play reference remains reference evidence only.
It may confirm convergence with live boot, but it may not become a replacement for accepted live-boot proof.

---

## Required M6 Gate Bundle

M6 must aggregate the executable gate classes defined by [../v2/15_TestAndValidationSpec.md](../v2/15_TestAndValidationSpec.md).

| Gate class | Example current anchor | M6 requirement | Shortcut rejected |
| --- | --- | --- | --- |
| `SpecShape` | [../../Editor/Tests/KernelV22ArchitectureDocTests.cs](../../Editor/Tests/KernelV22ArchitectureDocTests.cs); [README.md](README.md); [05_KernelV22MilestoneOrderSpec.md](05_KernelV22MilestoneOrderSpec.md) | the claim surface, milestone roles, and proof-family separation remain traceable and mutually consistent | treating doc shape as if it already proved runtime closure |
| `Validation` | [../../Editor/Tests/KernelBoot/BootValidationTests.cs](../../Editor/Tests/KernelBoot/BootValidationTests.cs); [../../Editor/Tests/DependencyValidationModelTests.cs](../../Editor/Tests/DependencyValidationModelTests.cs); [../../Editor/Tests/ProjectionValidationTests.cs](../../Editor/Tests/ProjectionValidationTests.cs) | invalid boot, dependency, and projection structures are rejected before accepted runtime proof is trusted | letting runtime repair or smoke success substitute for invalid-structure rejection |
| `Generation` | [../../Editor/Tests/KernelIRHashingTests.cs](../../Editor/Tests/KernelIRHashingTests.cs); [../../Editor/Tests/KernelIRIdentitiesTests.cs](../../Editor/Tests/KernelIRIdentitiesTests.cs); [../../Editor/Tests/KernelModuleContributionTests.cs](../../Editor/Tests/KernelModuleContributionTests.cs); [../../Editor/Tests/KernelBoot/KernelBootManifestTests.cs](../../Editor/Tests/KernelBoot/KernelBootManifestTests.cs) | verified inputs, identities, manifests, and contribution normalization remain deterministic and reviewable | trusting generated or cached artifacts because a later runtime path looked correct |
| `RuntimeBehavior` | [../../Editor/Tests/KernelV22LiveBootBundleTests.cs](../../Editor/Tests/KernelV22LiveBootBundleTests.cs); [../../Editor/Tests/KernelV22RepresentativeGameSceneBundleTests.cs](../../Editor/Tests/KernelV22RepresentativeGameSceneBundleTests.cs); [../../Tests/Integration/PlayMode/KernelMinimalBootPlayModeTests.cs](../../Tests/Integration/PlayMode/KernelMinimalBootPlayModeTests.cs); [../../Editor/Tests/KernelBoot/AuthoringBridgeDirectPlayTests.cs](../../Editor/Tests/KernelBoot/AuthoringBridgeDirectPlayTests.cs); [../../Editor/Tests/GameStateMachineMigrationTests.cs](../../Editor/Tests/GameStateMachineMigrationTests.cs); [../../Editor/Tests/ConversationDialogueMigrationTests.cs](../../Editor/Tests/ConversationDialogueMigrationTests.cs); [../../Editor/Tests/StatusEffectServiceDependencyCaptureTests.cs](../../Editor/Tests/StatusEffectServiceDependencyCaptureTests.cs) | accepted live boot and representative gameplay/application paths consume migrated authority under real anchors | direct-play-only success, editor-only setup, or one representative slice standing in for the bundle |
| `Diagnostics` | [../../Editor/Tests/KernelDiagnostics/KernelDiagnosticsModelTests.cs](../../Editor/Tests/KernelDiagnostics/KernelDiagnosticsModelTests.cs); [../../Editor/Tests/KernelDiagnostics/KernelDiagnosticServiceTests.cs](../../Editor/Tests/KernelDiagnostics/KernelDiagnosticServiceTests.cs); [../../Editor/Tests/KernelDiagnostics/DiagnosticCodeTraceabilityTests.cs](../../Editor/Tests/KernelDiagnostics/DiagnosticCodeTraceabilityTests.cs); [../../GameLib/Script/Common/Commands/VNext/Core/CommandExecutionTrace.cs](../../GameLib/Script/Common/Commands/VNext/Core/CommandExecutionTrace.cs); [../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicRuntimeLogUtility.cs](../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicRuntimeLogUtility.cs) | success and failure remain code-bearing, provenance-visible, and traceable through the accepted proof bundle | message-only logging, silent failure, or visual success without traceability |
| `PerformanceRule` | [../../Editor/Tests/KernelDiagnostics/KernelPerformanceAllocationTests.cs](../../Editor/Tests/KernelDiagnostics/KernelPerformanceAllocationTests.cs); [../../Editor/Tests/KernelDiagnostics/KernelPerformanceRegressionGateTests.cs](../../Editor/Tests/KernelDiagnostics/KernelPerformanceRegressionGateTests.cs); [../../Editor/Tests/KernelDiagnostics/KernelProfilerMarkerTaxonomyTests.cs](../../Editor/Tests/KernelDiagnostics/KernelProfilerMarkerTaxonomyTests.cs); [../../../Tools/Run-M15.4Gate.ps1](../../../Tools/Run-M15.4Gate.ps1) | required markers, allocation ceilings, and regression thresholds remain explicit and executable on covered paths | wall-clock-only acceptance or missing marker coverage |
| `StaticRule` | [../../Editor/Tests/KernelDiagnostics/KernelForbiddenPatternTests.cs](../../Editor/Tests/KernelDiagnostics/KernelForbiddenPatternTests.cs); [../../Editor/Tests/KernelDiagnostics/KernelForbiddenPatternScannerTests.cs](../../Editor/Tests/KernelDiagnostics/KernelForbiddenPatternScannerTests.cs) | forbidden discovery, fallback, lookup, and direct-logging patterns are rejected without waiting for late runtime failure | treating runtime success as proof that static regressions are acceptable |
| `LegacyCompat` | [../../Editor/Tests/LegacyCompatBoundaryTests.cs](../../Editor/Tests/LegacyCompatBoundaryTests.cs); [../../GameLib/Script/Kernel/Validation/LegacyMigrationModel.cs](../../GameLib/Script/Kernel/Validation/LegacyMigrationModel.cs); [../../Editor/Tests/KernelDiagnostics/KernelDiagnosticsAsmdefBoundaryTests.cs](../../Editor/Tests/KernelDiagnostics/KernelDiagnosticsAsmdefBoundaryTests.cs) | bounded residue, adapter metadata, compile-boundary direction, and removal evidence remain explicit and executable | describing quarantine only in docs while the executable bundle cannot prove it |
| `IntegrationSmoke` | [../../../Tools/Run-UnityTests.ps1](../../../Tools/Run-UnityTests.ps1); [../../Tests/Integration/PlayMode/KernelMinimalBootPlayModeTests.cs](../../Tests/Integration/PlayMode/KernelMinimalBootPlayModeTests.cs) | the ordered end-to-end release bundle is reviewable and can summarize the final claim | using one smoke run to excuse missing lower-layer gates |

---

## M6 Final Bundle Map

The table below is the canonical M6-1 bundle map owner.
[Index/KernelV22ProofAnchorCatalog.md](Index/KernelV22ProofAnchorCatalog.md) continues to own proof-family separation and current anchor identity.
A green gate row may consume multiple proof families, but no gate row may replace a missing proof family or hide a lower-layer failure.

| Gate class | Required proof-family intake | Primary executable anchors | Current runner or lane anchor | Required review output | Failure code family |
| --- | --- | --- | --- | --- | --- |
| `SpecShape` | Spec package proof; Release hardening proof for claim-shape credibility | [../../Editor/Tests/KernelV22ArchitectureDocTests.cs](../../Editor/Tests/KernelV22ArchitectureDocTests.cs); [05_KernelV22MilestoneOrderSpec.md](05_KernelV22MilestoneOrderSpec.md); [Index/KernelV22ProofAnchorCatalog.md](Index/KernelV22ProofAnchorCatalog.md) | [../../../Tools/Run-UnityTests.ps1](../../../Tools/Run-UnityTests.ps1) EditMode document lane | bundle-map rows remain synchronized with proof-family rules, milestone roles, and release-claim diagnostics vocabulary | `V22-M6-BUNDLE-001`; `V22-M6-AGG-001` |
| `Validation` | all five proof families; validation legality is a shared prerequisite | [../../Editor/Tests/KernelBoot/BootValidationTests.cs](../../Editor/Tests/KernelBoot/BootValidationTests.cs); [../../Editor/Tests/DependencyValidationModelTests.cs](../../Editor/Tests/DependencyValidationModelTests.cs); [../../Editor/Tests/ProjectionValidationTests.cs](../../Editor/Tests/ProjectionValidationTests.cs) | [../../../Tools/Run-UnityTests.ps1](../../../Tools/Run-UnityTests.ps1) EditMode validation lane; [../../../Tools/Run-M15.4Gate.ps1](../../../Tools/Run-M15.4Gate.ps1) validation segment | boot, dependency, and projection rejection remains reviewable before any runtime bundle is trusted | `V22-M6-VALID-001` |
| `Generation` | all five proof families; deterministic verified inputs remain a shared prerequisite | [../../Editor/Tests/KernelIRHashingTests.cs](../../Editor/Tests/KernelIRHashingTests.cs); [../../Editor/Tests/KernelIRIdentitiesTests.cs](../../Editor/Tests/KernelIRIdentitiesTests.cs); [../../Editor/Tests/KernelModuleContributionTests.cs](../../Editor/Tests/KernelModuleContributionTests.cs); [../../Editor/Tests/KernelBoot/KernelBootManifestTests.cs](../../Editor/Tests/KernelBoot/KernelBootManifestTests.cs) | [../../../Tools/Run-UnityTests.ps1](../../../Tools/Run-UnityTests.ps1) EditMode generation lane; [../../../Tools/Run-M15.4Gate.ps1](../../../Tools/Run-M15.4Gate.ps1) generation segment | manifests, identities, hashing, and contribution normalization remain deterministic and reviewable | `V22-M6-VALID-001` |
| `RuntimeBehavior` | Live kernel-only host proof; Service-family cutover proof; direct-play reference remains inherited reference only | [../../Editor/Tests/KernelV22LiveBootBundleTests.cs](../../Editor/Tests/KernelV22LiveBootBundleTests.cs); [../../Editor/Tests/KernelV22RepresentativeGameSceneBundleTests.cs](../../Editor/Tests/KernelV22RepresentativeGameSceneBundleTests.cs); [../../Tests/Integration/PlayMode/KernelMinimalBootPlayModeTests.cs](../../Tests/Integration/PlayMode/KernelMinimalBootPlayModeTests.cs); [../../Editor/Tests/KernelBoot/AuthoringBridgeDirectPlayTests.cs](../../Editor/Tests/KernelBoot/AuthoringBridgeDirectPlayTests.cs); [../../Editor/Tests/GameStateMachineMigrationTests.cs](../../Editor/Tests/GameStateMachineMigrationTests.cs); [../../Editor/Tests/ConversationDialogueMigrationTests.cs](../../Editor/Tests/ConversationDialogueMigrationTests.cs); [../../Editor/Tests/StatusEffectServiceDependencyCaptureTests.cs](../../Editor/Tests/StatusEffectServiceDependencyCaptureTests.cs) | [../../../Tools/Run-UnityTests.ps1](../../../Tools/Run-UnityTests.ps1) PlayMode minimal-boot lane; [../../../Tools/Run-M15.4Gate.ps1](../../../Tools/Run-M15.4Gate.ps1) live-boot and representative-gameplay segments | accepted live boot and representative GameScene evidence stays distinct, traceable, and non-replaceable by direct-play-only success | `V22-M6-LIVE-001`; `V22-M6-GAME-001` |
| `Diagnostics` | Service-family cutover proof; Release hardening proof | [../../Editor/Tests/KernelDiagnostics/KernelDiagnosticsModelTests.cs](../../Editor/Tests/KernelDiagnostics/KernelDiagnosticsModelTests.cs); [../../Editor/Tests/KernelDiagnostics/KernelDiagnosticServiceTests.cs](../../Editor/Tests/KernelDiagnostics/KernelDiagnosticServiceTests.cs); [../../Editor/Tests/KernelDiagnostics/DiagnosticCodeTraceabilityTests.cs](../../Editor/Tests/KernelDiagnostics/DiagnosticCodeTraceabilityTests.cs) | [../../../Tools/Run-M15.4Gate.ps1](../../../Tools/Run-M15.4Gate.ps1) diagnostics snapshot segment | success and failure remain gate-class visible, provenance-bearing, and reviewable through the final claim | `V22-M6-DIAG-001`; `V22-M6-AGG-001` |
| `PerformanceRule` | Release hardening proof; Service-family cutover proof where covered runtime paths exist | [../../Editor/Tests/KernelDiagnostics/KernelPerformanceAllocationTests.cs](../../Editor/Tests/KernelDiagnostics/KernelPerformanceAllocationTests.cs); [../../Editor/Tests/KernelDiagnostics/KernelPerformanceRegressionGateTests.cs](../../Editor/Tests/KernelDiagnostics/KernelPerformanceRegressionGateTests.cs); [../../Editor/Tests/KernelDiagnostics/KernelProfilerMarkerTaxonomyTests.cs](../../Editor/Tests/KernelDiagnostics/KernelProfilerMarkerTaxonomyTests.cs) | [../../../Tools/Run-M15.4Gate.ps1](../../../Tools/Run-M15.4Gate.ps1) performance smoke segment | profiler-marker, allocation, and regression evidence stays visible instead of collapsing into wall-clock-only acceptance | `V22-M6-PERF-001`; `V22-M6-AGG-001` |
| `StaticRule` | Release hardening proof | [../../Editor/Tests/KernelDiagnostics/KernelForbiddenPatternTests.cs](../../Editor/Tests/KernelDiagnostics/KernelForbiddenPatternTests.cs); [../../Editor/Tests/KernelDiagnostics/KernelForbiddenPatternScannerTests.cs](../../Editor/Tests/KernelDiagnostics/KernelForbiddenPatternScannerTests.cs) | [../../../Tools/Run-M15.4Gate.ps1](../../../Tools/Run-M15.4Gate.ps1) static forbidden-pattern segment | forbidden discovery, fallback, lookup, and direct-logging regressions remain explicitly rejected | `V22-M6-LEGACY-001`; `V22-M6-AGG-001` |
| `LegacyCompat` | Release hardening proof | [../../Editor/Tests/LegacyCompatBoundaryTests.cs](../../Editor/Tests/LegacyCompatBoundaryTests.cs); [../../Editor/Tests/KernelDiagnostics/KernelDiagnosticsAsmdefBoundaryTests.cs](../../Editor/Tests/KernelDiagnostics/KernelDiagnosticsAsmdefBoundaryTests.cs); [../../GameLib/Script/Kernel/Validation/LegacyMigrationModel.cs](../../GameLib/Script/Kernel/Validation/LegacyMigrationModel.cs) | [../../../Tools/Run-M15.4Gate.ps1](../../../Tools/Run-M15.4Gate.ps1) legacy-boundary segment | bounded residue, adapter metadata, compile-boundary direction, and quarantine evidence remain reviewable inside the same release claim | `V22-M6-LEGACY-001`; `V22-M6-AGG-001` |
| `IntegrationSmoke` | all five proof families; summary only after lower layers are green | [../../../Tools/Run-UnityTests.ps1](../../../Tools/Run-UnityTests.ps1); [../../../Tools/Run-M15.4Gate.ps1](../../../Tools/Run-M15.4Gate.ps1); [../../Tests/Integration/PlayMode/KernelMinimalBootPlayModeTests.cs](../../Tests/Integration/PlayMode/KernelMinimalBootPlayModeTests.cs) | [../../../Tools/Run-M15.4Gate.ps1](../../../Tools/Run-M15.4Gate.ps1) final ordered bundle | the final summary names gate class, profile, anchor set, and lower-layer failure reason instead of hiding them behind one green smoke result | `V22-M6-BUNDLE-001`; `V22-M6-AGG-001` |

---

## Required Release Claim Rules

1. Every required gate class above must be mapped to reviewable evidence in the canonical bundle map before M6 can claim completion.
2. One failing lower layer invalidates higher-layer confidence.
3. `SpecShape` remains necessary for traceability but is never sufficient for acceptance.
4. `IntegrationSmoke` may summarize the release claim, but it may not excuse missing `Validation`, `Generation`, `Diagnostics`, `PerformanceRule`, `StaticRule`, or `LegacyCompat` gates.
5. direct-play reference may confirm live-boot convergence only; it may not stand in for accepted live-boot proof.
6. representative gameplay/application proof must still run through the real GameScene anchors fixed by M4.
7. M5 deletion, bounded residue, forbidden-pattern, and compile-boundary gates must already be green before the final M6 aggregation can close.
8. performance evidence must include required marker and allocation or regression proof where the lower specs require it; wall-clock-only claims are insufficient.
9. release-claim output must identify the failing gate class, profile, and anchor set whenever the claim is not green.
10. skipped required gates fail the release claim unless a stricter upstream rule explicitly marks them not applicable and the reason remains reviewable.
11. the Release profile may not loosen the proved rules by re-enabling runtime-capable legacy authority, compile-boundary inversion, or forbidden runtime repair.

---

## Release Claim Bundle Anchors

The current repository already contains concrete anchors that M6 must aggregate rather than merely mention.

- [../../Editor/Tests/KernelV22ArchitectureDocTests.cs](../../Editor/Tests/KernelV22ArchitectureDocTests.cs)
- [../../Editor/Tests/KernelBoot/BootValidationTests.cs](../../Editor/Tests/KernelBoot/BootValidationTests.cs)
- [../../Editor/Tests/DependencyValidationModelTests.cs](../../Editor/Tests/DependencyValidationModelTests.cs)
- [../../Editor/Tests/ProjectionValidationTests.cs](../../Editor/Tests/ProjectionValidationTests.cs)
- [../../Editor/Tests/KernelIRHashingTests.cs](../../Editor/Tests/KernelIRHashingTests.cs)
- [../../Editor/Tests/KernelIRIdentitiesTests.cs](../../Editor/Tests/KernelIRIdentitiesTests.cs)
- [../../Editor/Tests/KernelModuleContributionTests.cs](../../Editor/Tests/KernelModuleContributionTests.cs)
- [../../Editor/Tests/KernelBoot/KernelBootManifestTests.cs](../../Editor/Tests/KernelBoot/KernelBootManifestTests.cs)
- [../../Editor/Tests/KernelV22LiveBootBundleTests.cs](../../Editor/Tests/KernelV22LiveBootBundleTests.cs)
- [../../Editor/Tests/KernelV22RepresentativeGameSceneBundleTests.cs](../../Editor/Tests/KernelV22RepresentativeGameSceneBundleTests.cs)
- [../../Tests/Integration/PlayMode/KernelMinimalBootPlayModeTests.cs](../../Tests/Integration/PlayMode/KernelMinimalBootPlayModeTests.cs)
- [../../Editor/Tests/KernelBoot/AuthoringBridgeDirectPlayTests.cs](../../Editor/Tests/KernelBoot/AuthoringBridgeDirectPlayTests.cs)
- [../../Editor/Tests/GameStateMachineMigrationTests.cs](../../Editor/Tests/GameStateMachineMigrationTests.cs)
- [../../Editor/Tests/ConversationDialogueMigrationTests.cs](../../Editor/Tests/ConversationDialogueMigrationTests.cs)
- [../../Editor/Tests/StatusEffectServiceDependencyCaptureTests.cs](../../Editor/Tests/StatusEffectServiceDependencyCaptureTests.cs)
- [../../Editor/Tests/KernelDiagnostics/KernelDiagnosticsModelTests.cs](../../Editor/Tests/KernelDiagnostics/KernelDiagnosticsModelTests.cs)
- [../../Editor/Tests/KernelDiagnostics/KernelDiagnosticServiceTests.cs](../../Editor/Tests/KernelDiagnostics/KernelDiagnosticServiceTests.cs)
- [../../Editor/Tests/KernelDiagnostics/DiagnosticCodeTraceabilityTests.cs](../../Editor/Tests/KernelDiagnostics/DiagnosticCodeTraceabilityTests.cs)
- [../../Editor/Tests/KernelDiagnostics/KernelPerformanceAllocationTests.cs](../../Editor/Tests/KernelDiagnostics/KernelPerformanceAllocationTests.cs)
- [../../Editor/Tests/KernelDiagnostics/KernelPerformanceRegressionGateTests.cs](../../Editor/Tests/KernelDiagnostics/KernelPerformanceRegressionGateTests.cs)
- [../../Editor/Tests/KernelDiagnostics/KernelProfilerMarkerTaxonomyTests.cs](../../Editor/Tests/KernelDiagnostics/KernelProfilerMarkerTaxonomyTests.cs)
- [../../Editor/Tests/KernelDiagnostics/KernelForbiddenPatternTests.cs](../../Editor/Tests/KernelDiagnostics/KernelForbiddenPatternTests.cs)
- [../../Editor/Tests/KernelDiagnostics/KernelForbiddenPatternScannerTests.cs](../../Editor/Tests/KernelDiagnostics/KernelForbiddenPatternScannerTests.cs)
- [../../Editor/Tests/LegacyCompatBoundaryTests.cs](../../Editor/Tests/LegacyCompatBoundaryTests.cs)
- [../../Editor/Tests/KernelDiagnostics/KernelDiagnosticsAsmdefBoundaryTests.cs](../../Editor/Tests/KernelDiagnostics/KernelDiagnosticsAsmdefBoundaryTests.cs)
- [../../../Tools/Run-UnityTests.ps1](../../../Tools/Run-UnityTests.ps1)
- [../../../Tools/Run-M15.4Gate.ps1](../../../Tools/Run-M15.4Gate.ps1)

M6 is not credible if these anchors remain disconnected and unaudited as one bundle.

---

## M6 Subphases

### M6-0 Proof-Family Intake and Bundle Map

Objective:
freeze the final proof intake before release claim begins.

Required outputs:

- explicit intake of spec, live-host, command/value, family-cutover, and release-hardening proof families
- gate-bundle map from `SpecShape` through `IntegrationSmoke`
- rule that the final bundle may aggregate earlier proof families but may not collapse them into one interchangeable green signal

Exit gate:
every required proof family and gate class has a reviewable place in the release bundle.

### M6-1 Validation and Generation Intake

Objective:
prove the release claim still rests on trusted inputs rather than runtime luck.

Required outputs:

- validation-gate intake for boot, dependency, and projection legality
- generation-gate intake for manifests, identities, hashing, and contribution normalization
- fail-closed rule for missing lower-layer evidence

Exit gate:
M6 no longer depends on compile success or runtime smoke to hide missing validation or generation proof.

### M6-2 Accepted Live Boot and Convergence Proof

Objective:
aggregate the accepted startup proof without promoting direct play to the accepted path.

Required outputs:

- accepted live-boot runtime proof through the kernel-owned host chain
- direct-play convergence evidence that remains inherited reference only
- fail-closed diagnostics for live-boot proof missing, divergence, or replacement by direct-play-only success

Exit gate:
accepted live boot is green and reviewable without reintroducing direct-play-as-proof.

### M6-3 Representative GameScene Proof

Objective:
aggregate representative gameplay/application proof through the real GameScene anchors.

Required outputs:

- representative GameScene proof for the gameplay/application slices already fixed by M4
- diagnostics and traceability evidence for that representative bundle
- fail-closed rule for gameplay-only appearance with no architecture evidence

Exit gate:
representative gameplay/application proof remains green and traceable under the final release bundle.

### M6-4 Hardening, Static, Legacy, and Compile-Boundary Consolidation

Objective:
prove that the final runtime claim still rejects legacy re-entry and compile-boundary drift.

Required outputs:

- M5 hardening-bundle intake for deletion, bounded residue, and compile-boundary enforcement
- static-rule intake for forbidden discovery, fallback, lookup, and direct-logging regressions
- legacy-compat intake for adapter metadata, profile bounds, and removal visibility

Exit gate:
final release proof still fails closed on legacy re-entry, forbidden patterns, and production-to-test or kernel-to-legacy inversion.

### M6-5 Performance and Report Aggregation

Objective:
make the final claim measurable and reviewable rather than rhetorical.

Required outputs:

- profiler-marker taxonomy and allocation or regression evidence for covered runtime paths
- explicit rejection of wall-clock-only performance claims
- one reviewable report shape that identifies gate class, profile, anchor, and failure reason

Exit gate:
performance and reporting evidence remain executable and reviewable rather than implied.

### M6-6 Final Acceptance Gate

Objective:
decide whether v2.2 release completion is actually claimable.

Required outputs:

- one final pass or fail decision over the full release bundle
- explicit failure when any required lower-layer gate is missing, skipped, or red
- explicit rule that green integration smoke cannot rescue missing lower-layer evidence

Exit gate:
the final bundle proves both success-path consumption and fail-closed regression rejection for the accepted release path.

---

## Diagnostics

M6 must use explicit failure codes for claim review.

| Code | Failure condition |
| --- | --- |
| `V22-M6-BUNDLE-001` | a required proof family or gate class is missing from the final release bundle |
| `V22-M6-VALID-001` | validation or generation evidence is missing, skipped, or replaced by runtime-only success |
| `V22-M6-LIVE-001` | accepted live-boot proof is missing, diverges, or is replaced by direct-play-only success |
| `V22-M6-GAME-001` | representative GameScene proof is missing, non-traceable, or reduced to visible gameplay-only success |
| `V22-M6-DIAG-001` | required diagnostics provenance, failure code, or traceability evidence is missing |
| `V22-M6-PERF-001` | required marker, allocation, or regression evidence is missing or reduced to wall-clock-only claims |
| `V22-M6-LEGACY-001` | legacy-compat, forbidden-pattern, or compile-boundary evidence is missing or contradicted by the final bundle |
| `V22-M6-AGG-001` | the final report hides a failing lower layer, silently skips a required gate, or otherwise makes the claim non-reviewable |

---

## Acceptance Criteria

M6 is complete only when all of the following are true:

- every required proof family from [Index/KernelV22ProofAnchorCatalog.md](Index/KernelV22ProofAnchorCatalog.md) is explicitly consumed by the final bundle
- every required gate class from `SpecShape` through `IntegrationSmoke` is present and reviewable
- accepted live boot remains green without promoting direct play into accepted proof
- representative gameplay/application proof remains green through the real GameScene anchors with traceable diagnostics evidence
- M5 deletion, bounded residue, forbidden-pattern, legacy-compat, and compile-boundary evidence remains green inside the same final bundle
- performance evidence includes required marker and allocation or regression proof where lower specs demand it
- one failing lower layer invalidates the final claim rather than being hidden by later aggregation
- release-claim output identifies gate class, profile, anchor set, and failure reason in reviewable form
- the release profile does not re-enable runtime-capable legacy authority or loosen the proved hardening rules
- the final rule remains true: the architecture can prove that it would reject its own regressions

---

## Non-Completion Rules

M6 is not complete under any of the following conditions:

- compile success is used as the main acceptance signal
- doc-shape success is used as the main acceptance signal
- a single smoke run is used to excuse missing lower-layer gates
- direct play is used as accepted live-boot proof
- representative gameplay/application visibility is used as final release proof by itself
- M5 hardening proof is treated as if it already closed M6
- wall-clock-only performance claims replace marker or allocation evidence
- message-only logging replaces structured diagnostics evidence
- required gates are skipped without reviewable explicit not-applicable justification
- release proof is reported in a way that hides which gate class actually failed

---

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V22-041-01 | Confirm M6 is defined as final proof aggregation rather than another runtime cutover slice. | Purpose and Non-Goals must state that M6 aggregates proof rather than redefining earlier milestones. |
| TC-V22-041-02 | Confirm M6 consumes the existing five proof families without collapsing them. | Consumed Proof Families must mention spec, live-host, command/value, service-family, and release-hardening proof separately. |
| TC-V22-041-03 | Confirm M6 requires the full gate bundle from SpecShape through IntegrationSmoke. | Required M6 Gate Bundle must list SpecShape, Validation, Generation, RuntimeBehavior, Diagnostics, PerformanceRule, StaticRule, LegacyCompat, and IntegrationSmoke. |
| TC-V22-041-04 | Confirm direct play remains only a convergence reference and representative gameplay stays on the real GameScene anchors. | Required Release Claim Rules must reject direct-play-as-proof and keep representative gameplay on the real GameScene bundle. |
| TC-V22-041-05 | Confirm M5 hardening does not by itself close M6. | Consumed Proof Families or Non-Completion Rules must say M5 hardening alone is insufficient. |
| TC-V22-041-06 | Confirm performance proof requires markers and allocation or regression evidence rather than wall-clock-only claims. | Required M6 Gate Bundle or Required Release Claim Rules must reject wall-clock-only performance proof. |
| TC-V22-041-07 | Confirm M6 requires reviewable failure aggregation and fail-closed skip policy. | Required Release Claim Rules must require gate-class failure visibility and reject silent skips. |
| TC-V22-041-08 | Confirm M6 protects release-profile credibility. | Acceptance Criteria must forbid release proof that re-enables runtime-capable legacy authority or loosens hardening rules. |
| TC-V22-041-09 | Confirm M6 ties the final bundle to concrete executable anchors. | Release Claim Bundle Anchors must contain KernelMinimalBootPlayModeTests.cs, GameStateMachineMigrationTests.cs, KernelPerformanceAllocationTests.cs, and LegacyCompatBoundaryTests.cs. |
| TC-V22-041-10 | Confirm M6 inherits the final rejection-of-regressions rule from v2 test policy. | Purpose or Acceptance Criteria must contain the sentence about proving that the architecture would reject its own regressions. |
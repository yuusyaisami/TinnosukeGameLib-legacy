# Kernel v2.2 Legacy Deletion and Hardening Specification

## Document Status

- Document ID: 04_KernelV22LegacyDeletionAndHardeningSpec
- Status: Draft
- Role: defines the M5 compile-boundary, legacy-deletion, and bounded-residue contract that converts remaining runtime-capable residue into explicit quarantine or release-rejected debt before M6 full-proof aggregation
- Depends on:
  - [00_KernelV22CompletionOverviewSpec.md](00_KernelV22CompletionOverviewSpec.md)
  - [01_KernelV22AuthorityAndServiceCensusSpec.md](01_KernelV22AuthorityAndServiceCensusSpec.md)
  - [02_KernelV22KernelOnlyHostSpec.md](02_KernelV22KernelOnlyHostSpec.md)
  - [02_1_KernelV22CommandAndValueHostRemovalSpec.md](02_1_KernelV22CommandAndValueHostRemovalSpec.md)
  - [03_KernelV22ServiceFamilyCutoverSpec.md](03_KernelV22ServiceFamilyCutoverSpec.md)
  - [03_1_KernelV22RepresentativeGameplayAndApplicationCutoverSpec.md](03_1_KernelV22RepresentativeGameplayAndApplicationCutoverSpec.md)
  - [../v2/13_LegacyCompatBoundarySpec.md](../v2/13_LegacyCompatBoundarySpec.md)
  - [../v2/15_TestAndValidationSpec.md](../v2/15_TestAndValidationSpec.md)
  - [../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md](../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md)
  - [../v2.1/06_WaveFLegacyRemovalAndHardeningSpec.md](../v2.1/06_WaveFLegacyRemovalAndHardeningSpec.md)
- Provides foundation for:
  - [05_KernelV22MilestoneOrderSpec.md](05_KernelV22MilestoneOrderSpec.md)

### Revision Note

This revision expands 04 from an end-state checklist into the dedicated owner specification for M5.

M1 through M4 can already establish kernel-owned host, command, value, family, and representative gameplay/application proof.
That is necessary, but it is not enough.

Accepted release execution can still remain architecturally legacy if runtime-capable residue, compile-boundary leakage, or invisible compatibility adapters remain required for accepted success.

M5 therefore exists to convert the remaining runtime-capable residue into one of two final states:

- deleted because the accepted path no longer needs it
- explicit, diagnosable, profile-bounded, removable, and non-authoritative quarantine-only residue

M6 later aggregates the proof families and performance gates that make release completion fully credible.
04 does not own that final aggregation.

---

## Ownership

This specification owns:

- the repository-wide current-state inventory of release-blocking residue left after M1 through M4
- the final end-state rules for live-host residue, scope and resolver residue, command residue, value and blackboard residue, representative gameplay helper residue, compile boundaries, and bounded quarantine remainder
- the distinction between accepted-path deletion and quarantine-only remainder
- the compile-boundary completion rules that finish the repo's current partial asmdef split without pretending the project is still a monolith
- the executable hardening bundle that proves M5 is materially closed
- the diagnostics and evidence requirements that make remaining residue auditable rather than implicit

This specification does not own:

- target boot, scope, command, value, scalar, or diagnostics semantics already owned by v2
- the host, command/value, family, or representative gameplay/application cutover semantics already owned by M1 through M4
- final M6 full-proof aggregation across all milestones and performance gates
- a blanket requirement to delete every historical legacy file immediately when the accepted path no longer depends on it

04 owns M5 residue deletion and compile-boundary hardening.
It must not reopen earlier milestones just because cleanup happens late.

---

## Purpose

The purpose of this document is to say what must be gone, what may remain only as bounded quarantine, and what must fail before M5 can be claimed.

Core statements:

```text
Representative migrated behavior is necessary, but it is not sufficient.

M5 is complete only when accepted runtime paths no longer depend on runtime-capable legacy residue.

If compatibility code remains, it must be explicit, diagnosable, profile-bounded, removable, and non-authoritative.

Release acceptance must fail closed on prohibited runtime legacy paths and compile-boundary leakage.
```

This specification therefore answers the M5 question:

```text
Is legacy still deciding accepted release outcomes,
or is it only explicit, bounded, removable residue?
```

If the answer is the former, M5 is not complete.

---

## Scope

This specification defines:

- the current-state inventory of residue that remains after M1 through M4
- the preserved contract floor that still survives final cleanup
- the target end-state for accepted runtime paths and any bounded quarantine remainder
- the residue-domain requirements for live host and scene residue, scope and resolver residue, command residue, value and blackboard residue, representative gameplay helper residue, compile boundaries, and executable hardening evidence
- the diagnostics, non-completion rules, and acceptance gates required to close M5

---

## Non-Goals

This specification does not define:

- a redesign of host, scope, service, command, value, scalar, or diagnostics semantics
- a new representative gameplay/application migration slice beyond M4
- a promise that every historical legacy text artifact is deleted immediately even when accepted release execution no longer depends on it
- a substitute for the v2 LegacyCompat, validation, or asmdef semantics
- the full M6 proof aggregation model

M5 finishes residue deletion and compile-boundary hardening.
It does not create a third architecture state between legacy and kernel.

---

## Relationship to Other Specs

| Spec | Relationship to 04 |
| --- | --- |
| [02_KernelV22KernelOnlyHostSpec.md](02_KernelV22KernelOnlyHostSpec.md) | Hands off remaining live-host residue that must no longer decide accepted outcomes. |
| [02_1_KernelV22CommandAndValueHostRemovalSpec.md](02_1_KernelV22CommandAndValueHostRemovalSpec.md) | Hands off remaining command and value residue for final deletion or quarantine. |
| [03_KernelV22ServiceFamilyCutoverSpec.md](03_KernelV22ServiceFamilyCutoverSpec.md) | Hands off remaining family-local mixed-boundary residue for final hardening. |
| [03_1_KernelV22RepresentativeGameplayAndApplicationCutoverSpec.md](03_1_KernelV22RepresentativeGameplayAndApplicationCutoverSpec.md) | Hands off representative gameplay/application helper residue whose authority must be removed after migrated proof exists. |
| [../v2/13_LegacyCompatBoundarySpec.md](../v2/13_LegacyCompatBoundarySpec.md) | Owns the quarantine model. M5 decides what still qualifies for that quarantine and what must be removed. |
| [../v2/15_TestAndValidationSpec.md](../v2/15_TestAndValidationSpec.md) | Owns the executable proof substrate that M5 must turn into deletion and hardening gates. |
| [../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md](../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md) | Owns the target compile graph. M5 defines what completion means relative to the repo's current partial split. |
| [05_KernelV22MilestoneOrderSpec.md](05_KernelV22MilestoneOrderSpec.md) | Owns the M5 claim order and the downstream M6 relationship that consumes this specification. |

04 is downstream of M1 through M4 and the v2 quarantine and validation specs.
It must not weaken them to make closeout easier.

---

## Current-State Residue Inventory

This section records the residue that still matters after M1 through M4.
It is release-hardening evidence, not target policy.

| Observation | Evidence type | M5 pressure |
| --- | --- | --- |
| `ScopeFeatureInstallerUtility` still uses `GetComponentsInChildren` and `Transform.parent` traversal to decide feature-installer ownership. | Source | accepted runtime composition must stop depending on hierarchy-derived installer discovery |
| `RuntimeLifetimeScope` still caches owned installers, constructs a runtime builder, and invokes installer authority during scope build. | Source | accepted scope composition must stop depending on legacy installer-owned build authority |
| `RuntimeResolverHub` still represents registration-driven resolver or container pressure. | Source | accepted service and scope truth must not depend on legacy resolver or container authority |
| `CommandRunnerMB` still acts as a scene-facing bulk command installer that references legacy runtime code paths. | Source | accepted command authority must no longer depend on a giant bulk-registration host |
| `BlackboardMB` still combines installer behavior, lifecycle participation, local value initialization, and debug wiring. | Source | final value authority must not keep a mixed-responsibility MonoBehaviour as architectural owner |
| `BlackboardService` still exposes hierarchical fallback and root-creation behavior. | Source | accepted value truth must not rely on hierarchical blackboard repair or root creation fallback |
| `VarKeyRegistryLocator` still performs runtime registry lookup through `Resources.Load`. | Source | accepted value identity must not quietly depend on runtime asset lookup convenience |
| `ChangeGameStateExecutor` ancestor traversal can still appear as representative gameplay helper residue. | Source | representative gameplay/application proof must not still depend on helper traversal deciding accepted service truth |
| Kernel asmdefs and editor test asmdefs already exist, but common gameplay or migration residue is not yet visibly completed as explicit `GameLib.Legacy.*` quarantine. | Workspace and asmdef anchors | M5 must complete a partial split rather than describe the repo as if no split exists |
| `LegacyMigrationModel` and `LegacyCompatBoundaryTests` already validate adapter metadata, profile bounds, and removal policy shape. | Source | M5 must consume these as deletion and quarantine evidence rather than leave them disconnected |

## Representative Anchors

- [../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs](../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs)
- [../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs)
- [../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs](../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs)
- [../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs)
- [../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs](../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs)
- [../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs](../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs)
- [../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs)
- [../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs)
- [../../Game/Scripts/Flow/Commands/GameStateMachineExecutors.cs](../../Game/Scripts/Flow/Commands/GameStateMachineExecutors.cs)
- [../../GameLib/Script/Kernel/Validation/LegacyMigrationModel.cs](../../GameLib/Script/Kernel/Validation/LegacyMigrationModel.cs)
- [../../GameLib/Script/Kernel/Boot/GameLib.Kernel.Boot.asmdef](../../GameLib/Script/Kernel/Boot/GameLib.Kernel.Boot.asmdef)
- [../../GameLib/Script/Kernel/IR/GameLib.Kernel.IR.asmdef](../../GameLib/Script/Kernel/IR/GameLib.Kernel.IR.asmdef)
- [../../Editor/Tests/KernelBoot/GameLib.Tests.Kernel.Boot.Editor.asmdef](../../Editor/Tests/KernelBoot/GameLib.Tests.Kernel.Boot.Editor.asmdef)
- [../../Editor/Tests/KernelDiagnostics/KernelDiagnosticsAsmdefBoundaryTests.cs](../../Editor/Tests/KernelDiagnostics/KernelDiagnosticsAsmdefBoundaryTests.cs)
- [../../Editor/Tests/LegacyCompatBoundaryTests.cs](../../Editor/Tests/LegacyCompatBoundaryTests.cs)

---

## Preserved Contracts

M5 preserves the same narrow continuity floor as the rest of v2.2.
It does not preserve the legacy hosts that happened to carry those contracts earlier in migration.

| Contract surface | Current anchor | M5 requirement |
| --- | --- | --- |
| existing command payload meaning | [../../GameLib/Script/Common/Commands/VNext/Commands](../../GameLib/Script/Common/Commands/VNext/Commands) | authored command payload shapes remain consumable while final cleanup removes legacy command authority beneath them |
| existing `DynamicValue` authoring surface | [../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicValue.cs](../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicValue.cs) | authoring continuity remains stable even while runtime helper residue is removed or quarantined |
| generated value-key identity continuity | [../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs) | generated identity remains stable while runtime lookup convenience stops deciding accepted truth |
| representative gameplay/application continuity already proven by M4 | [03_1_KernelV22RepresentativeGameplayAndApplicationCutoverSpec.md](03_1_KernelV22RepresentativeGameplayAndApplicationCutoverSpec.md) | final cleanup must not break the representative gameplay/application continuity already accepted as migrated behavior |

The following are explicitly not preserved surfaces:

- `RuntimeLifetimeScope` as accepted architecture owner
- `RuntimeResolverHub` as accepted resolver or container boundary
- `CommandRunnerMB` as authoritative command composition
- `BlackboardMB` as mixed value-authority host
- `BlackboardService` hierarchical fallback as accepted runtime truth
- representative gameplay helper traversal that only exists to bridge earlier migration gaps

---

## Owned Migration Goals

M5 must achieve all of the following:

- remove accepted-path dependence on runtime-capable legacy hosts, adapters, helper shims, and residue left after M1 through M4
- convert any remaining compatibility code into explicit, diagnosable, profile-bounded, removable quarantine-only residue
- make release acceptance fail closed when prohibited runtime legacy adapters, repair paths, or compile-boundary inversions remain required
- finish compile-boundary quarantine relative to the repo's current partial asmdef split
- preserve the narrow continuity floor while refusing to preserve legacy architecture ownership beneath it
- make M5 auditable through executable gates and legacy-removal evidence rather than by visible success alone

---

## Release Zero-Authority Rule

The release profile must contain zero runtime-capable legacy authority on the accepted path.

This is stronger than v2.1 bounded quarantine.

## Allowed Temporary Residue Matrix

| Residue state | Allowed during M5 | Required bound |
| --- | --- | --- |
| a legacy code file still exists in the repository but accepted runtime paths no longer depend on it | Yes | removal direction or ownership must still be understandable from docs, asmdef placement, or explicit quarantine metadata |
| a development-only or test-only adapter remains | Yes | explicit adapter metadata, diagnostics visibility, removal policy, and profile bounds are mandatory |
| a runtime-capable legacy adapter remains enabled for Release | No | release acceptance must fail |
| representative gameplay/application only stays green because helper traversal or compatibility repair still decides a required outcome | No | M5 acceptance must fail |
| a kernel asmdef references legacy asmdefs, or production assemblies reference test asmdefs | No | compile-boundary gate must fail |

M5 does not require an empty repository.
It requires a non-authoritative residue state.

---

## M5 Subphases

### M5-0 Residue Inventory and Preservation Floor

Objective:
make residue explicit before deletion and hardening claims begin.

Required outputs:

- residue inventory by domain: live host and scene, scope and resolver, command, value and blackboard, representative gameplay helpers, compile boundaries, and hardening gates
- preserved-contract floor confirmation
- explicit rule that no new preserved surface may be inferred from late cleanup work

Exit gate:
residue is explicitly classified and the preservation floor has not silently widened.

### M5-1 Live Host and Scene Residue

Objective:
finalize the live-host and scene residue left after M1.

Required outputs:

- demotion or deletion rules for legacy live-entry and scene-root residue
- explicit prohibition on legacy scene-root authority deciding accepted release outcomes
- release rejection rule for runtime boot bridges that keep legacy authority alive as repair

Exit gate:
accepted live boot and accepted direct play no longer depend on legacy live-host or scene-root residue.

### M5-2 Scope, Installer, Resolver, and Runtime Host Residue

Objective:
finalize the composition cleanup left after M1 through M3.

Required outputs:

- demotion or deletion rules for `ScopeFeatureInstallerUtility`, `RuntimeLifetimeScope`, and `RuntimeResolverHub`
- explicit rejection of hierarchy discovery, installer-owned build authority, and resolver-table authority as accepted runtime truth
- dependency-direction rule that accepted kernel paths may not depend on legacy scope or resolver types in reverse

Exit gate:
accepted runtime composition no longer depends on hierarchy-driven, installer-driven, or resolver-driven legacy authority.

### M5-3 Command Residue

Objective:
finalize the command cleanup left after M2.

Required outputs:

- demotion or deletion rules for `CommandRunnerMB` and any remaining command compatibility shim
- explicit rejection of bulk executor registration hosts as accepted command authority
- release rejection rule for runtime command adapters that keep legacy command authority shippable

Exit gate:
accepted command execution no longer depends on `CommandRunnerMB` or equivalent legacy bulk-registration ownership.

### M5-4 Value, Blackboard, and Registry Residue

Objective:
finalize the value cleanup left after M2.

Required outputs:

- demotion or deletion rules for `BlackboardMB`, `BlackboardService` fallback, runtime registry lookup, and runtime stable-key convenience
- explicit rejection of blackboard fallback, root-creation repair, `Resources.Load`, and stable-key runtime truth as accepted value authority
- bounded-quarantine rules for any remaining value-facing compatibility adapter

Exit gate:
accepted value behavior no longer depends on blackboard fallback, root creation fallback, runtime registry lookup convenience, or stable-key runtime truth.

### M5-5 Representative Gameplay Helper Cleanup

Objective:
finalize the helper cleanup left after M4.

Required outputs:

- explicit residue policy for representative gameplay/application helper traversal and compatibility wrappers
- rejection of scene-local hosts or helper shims becoming hidden authority owners after M4 proof exists
- explicit rule that representative gameplay/application proof may not stay green only because helper cleanup was deferred

Exit gate:
representative gameplay/application no longer needs compatibility helpers to decide required outcomes.

### M5-6 Compile Boundary and Package Quarantine

Implementation note:
the current execution plan closes this section together with M5-7 under V22-IMP-M5-4 so compile-boundary quarantine and hardening evidence stay one bounded review slice.

Objective:
finish compile-boundary hardening relative to the current repo state.

Required outputs:

- explicit acknowledgement of the current partial split: kernel asmdefs and some editor test asmdefs already exist
- quarantine direction for remaining residue toward `GameLib.Legacy.*` or equivalent bounded residence instead of unlabeled common code
- hard dependency-direction rules for `GameLib.Kernel.*`, `GameLib.Legacy.*`, `GameLib.Features.*`, `GameLib.Tests.*`, and bounded `VContainer` use

Exit gate:
kernel, legacy, and test dependency directions are auditable and enforceable relative to the current partial split.

### M5-7 Hardening Gates and Legacy-Removal Evidence

Objective:
bind residue cleanup to executable gates and concrete evidence.

Required outputs:

- hardening gate bundle for legacy boundary legality, forbidden patterns, and compile-boundary direction
- explicit residue evidence showing what runtime-capable residue was deleted and what remains only as bounded adapter debt
- fail-closed rule for missing M5 hardening evidence even when gameplay still looks correct

Exit gate:
M5 deletion and hardening are auditable through gates and residue evidence rather than visible success alone.

---

## Compile-Boundary End-State

- `GameLib.Kernel.*` may not reference `GameLib.Legacy.*`
- `GameLib.Features.*` may consume only public kernel APIs
- `GameLib.Legacy.*` may exist only as explicit quarantine during transition
- production assemblies may not reference `GameLib.Tests.*`
- `VContainer` usage must remain inside bounded legacy quarantine assemblies if it remains at all
- release acceptance must not depend on VContainer-backed legacy runtime hosts

---

## Diagnostics and Failure Policy

v2 specification 11 owns the diagnostics substrate.
M5 defines the residue and hardening failures that must become diagnostic-visible and acceptance-visible.

Required evidence includes:

- structured legacy-boundary output for adapter kind, source, target replacement, removal condition, and blocking issue
- asmdef or compile-boundary reports that show dependency inversion clearly
- deterministic static-rule reports for forbidden discovery, fallback, and lookup patterns
- command or dynamic trace evidence where representative gameplay/application helper cleanup is under review

| Code | Failure condition | Required result |
| --- | --- | --- |
| V22-M5-BOOT-001 | Accepted live boot or direct play still depends on legacy auto-bootstrap or scene-root authority. | M5 acceptance fails. |
| V22-M5-SCOPE-001 | Accepted runtime composition still depends on hierarchy discovery, installer-owned build authority, or legacy resolver boundary. | M5 acceptance fails. |
| V22-M5-CMD-001 | Accepted command execution still depends on legacy bulk registration or unbounded command adapters. | M5 acceptance fails. |
| V22-M5-VALUE-001 | Accepted value behavior still depends on blackboard fallback, root creation fallback, runtime registry lookup, or stable-key runtime truth. | M5 acceptance fails. |
| V22-M5-GAME-001 | Representative gameplay/application only remains green because helper traversal or compatibility repair still decides required outcomes. | M5 acceptance fails. |
| V22-M5-ASMDEF-001 | Kernel-to-legacy or production-to-test dependency inversion remains. | Compile-boundary gate fails. |
| V22-M5-LEGACY-001 | A remaining adapter lacks removal policy, diagnostics code, profile bounds, or explicit target replacement metadata. | Legacy-boundary gate fails. |
| V22-M5-GATE-001 | Required M5 executable hardening evidence or legacy-removal evidence is missing. | M5 claim fails. |

M5 does not allow silent residue.
If residue remains important enough to keep, it is important enough to diagnose.

---

## Acceptance Criteria

M5 is complete only when all of the following are true:

- accepted live boot and direct play no longer depend on legacy live-host or scene residue deciding accepted success
- accepted runtime composition no longer depends on hierarchy-derived installer ownership, legacy installer build authority, or legacy resolver authority
- accepted command execution no longer depends on `CommandRunnerMB` or equivalent legacy bulk-registration ownership
- accepted value behavior no longer depends on blackboard hierarchical fallback, root-creation repair, runtime registry lookup convenience, or stable-key runtime truth
- representative gameplay/application systems proven by M4 no longer require compatibility helpers to decide required outcomes
- any remaining legacy adapters are explicit, diagnosable, profile-bounded, removable, and non-authoritative
- release profile rejects prohibited runtime legacy adapters and compile-boundary leakage
- compile boundaries make kernel, legacy, and test dependency direction auditable and enforceable enough to catch regression
- M5 hardening gates and legacy-removal evidence can distinguish true deletion/hardening closure from gameplay-only success
- the v2.2 continuity floor remains stable

---

## Non-Completion Rules

M5 is not complete if any of the following remain true:

- accepted runtime paths still require legacy live-host, scope, command, value, or gameplay-helper residue
- remaining adapters are visible only in code archaeology and not in explicit metadata, diagnostics, or profile policy
- Release can still ship runtime-capable legacy adapters or dependency inversions without a failing gate
- representative gameplay/application remains green only because helper cleanup was deferred indefinitely
- legacy-removal evidence cannot explain what was removed, what remains, and why the remainder is bounded

The following by themselves do not automatically mean failure:

- a legacy file still exists in the repository
- a development-only adapter exists for bounded migration support

Those states become failures only when they remain authoritative, invisible, or unremovable.

---

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V22-04-01 | Confirm 04 owns M5 residue deletion and compile-boundary hardening rather than earlier cutover semantics. | Ownership and Non-Goals must exclude redesign of host, command, value, scalar, and representative gameplay semantics. |
| TC-V22-04-02 | Confirm actual residue anchors are inventoried rather than abstract categories only. | The residue inventory must name concrete runtime, test, and asmdef anchors from the current repository. |
| TC-V22-04-03 | Confirm the continuity floor remains narrow during final cleanup. | Preserved Contracts must still be limited to command payload meaning, DynamicValue authoring surface, generated value-key identity continuity, and representative gameplay/application continuity already proven by M4. |
| TC-V22-04-04 | Confirm allowed temporary residue is bounded and Release-forbidden when runtime-capable. | The allowed residue matrix must distinguish code existence from accepted-path dependence and must forbid runtime-capable legacy adapters for Release. |
| TC-V22-04-05 | Confirm scope or resolver residue is treated as final cleanup debt rather than accepted architecture. | M5-2 and diagnostics must reject hierarchy-driven installer ownership and legacy resolver authority as accepted truth. |
| TC-V22-04-06 | Confirm command, value, and gameplay-helper residue are finally demoted or removed. | M5-3 through M5-5 and Acceptance must reject legacy bulk-registration hosts, blackboard fallback, runtime registry lookup, and helper traversal as accepted truth. |
| TC-V22-04-07 | Confirm compile-boundary completion is written against the current partial split rather than a monolith assumption. | M5-6 must mention existing kernel and editor test asmdefs and define completion relative to that current state. |
| TC-V22-04-08 | Confirm remaining adapters are explicit, removable, and Release-forbidden when runtime-capable. | Diagnostics, Allowed Temporary Residue Matrix, and Acceptance must require metadata, profile bounds, and removal policy. |
| TC-V22-04-09 | Confirm M5 hardening gates and residue evidence are required for claim closure. | M5-7 and Acceptance must require gates and evidence, not only visible gameplay success. |
| TC-V22-04-10 | Confirm M5 does not incorrectly require deleting every historical legacy line. | Non-Completion Rules must distinguish file existence from authoritative residue. |
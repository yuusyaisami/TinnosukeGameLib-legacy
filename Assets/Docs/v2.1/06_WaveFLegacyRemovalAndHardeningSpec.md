# Wave F Legacy Removal and Hardening Specification

## Document Status

- Document ID: 06_WaveFLegacyRemovalAndHardeningSpec
- Status: Draft
- Role: defines the final v2.1 migration contract that removes or strictly quarantines remaining migration-only legacy residue, completes compile-boundary hardening, and binds migration completion to executable gates while preserving the narrow gameplay-facing contracts established by v2.1
- Depends on:
  - [00_KernelV21MigrationOverviewSpec.md](00_KernelV21MigrationOverviewSpec.md)
  - [01_WaveABootAndSceneEntryCutoverSpec.md](01_WaveABootAndSceneEntryCutoverSpec.md)
  - [02_WaveBScopeAndServiceCompositionCutoverSpec.md](02_WaveBScopeAndServiceCompositionCutoverSpec.md)
  - [03_WaveCCommandDispatchCutoverSpec.md](03_WaveCCommandDispatchCutoverSpec.md)
  - [04_WaveDValueBlackboardAndVarCutoverSpec.md](04_WaveDValueBlackboardAndVarCutoverSpec.md)
  - [05_WaveERepresentativeGameplaySystemsCutoverSpec.md](05_WaveERepresentativeGameplaySystemsCutoverSpec.md)
  - [../v2/01_KernelIRSpec.md](../v2/01_KernelIRSpec.md)
  - [../v2/04_DependencyValidationSpec.md](../v2/04_DependencyValidationSpec.md)
  - [../v2/05_BootManifestAndProfileSpec.md](../v2/05_BootManifestAndProfileSpec.md)
  - [../v2/06_ServiceGraphRuntimeSpec.md](../v2/06_ServiceGraphRuntimeSpec.md)
  - [../v2/07_ScopeGraphRuntimeSpec.md](../v2/07_ScopeGraphRuntimeSpec.md)
  - [../v2/08_LifecyclePlanSpec.md](../v2/08_LifecyclePlanSpec.md)
  - [../v2/09_CommandCatalogRuntimeSpec.md](../v2/09_CommandCatalogRuntimeSpec.md)
  - [../v2/10_ValueSchemaAndStoreSpec.md](../v2/10_ValueSchemaAndStoreSpec.md)
  - [../v2/10_1_ScalarRuntimeAndBindingSpec.md](../v2/10_1_ScalarRuntimeAndBindingSpec.md)
  - [../v2/10_2_DynamicValueEvaluationSpec.md](../v2/10_2_DynamicValueEvaluationSpec.md)
  - [../v2/11_DebugMapAndDiagnosticsSpec.md](../v2/11_DebugMapAndDiagnosticsSpec.md)
  - [../v2/12_UnityAuthoringBridgeSpec.md](../v2/12_UnityAuthoringBridgeSpec.md)
  - [../v2/13_LegacyCompatBoundarySpec.md](../v2/13_LegacyCompatBoundarySpec.md)
  - [../v2/14_PerformanceBudgetAndRuntimeRulesSpec.md](../v2/14_PerformanceBudgetAndRuntimeRulesSpec.md)
  - [../v2/15_TestAndValidationSpec.md](../v2/15_TestAndValidationSpec.md)
  - [../v2/16_ImplementationMilestoneOrderSpec.md](../v2/16_ImplementationMilestoneOrderSpec.md)
  - [../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md](../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md)
- Provides foundation for:
  - v2.1 migration closeout review
  - legacy-removal implementation work
  - release-profile hardening and regression gate completion

### Revision Note

This revision creates the sixth and final detailed v2.1 wave specification.

Wave A moves live boot and scene entry onto a verified path.
Wave B moves runtime scope and service composition toward verified composition boundaries.
Wave C moves command dispatch authority toward verified command truth.
Wave D moves generic value, blackboard, and DynamicValue runtime authority toward verified value truth.
Wave E proves that representative gameplay systems consume that migrated authority.

Wave F exists because migrated proof is not the same thing as migration completion.
The playable game can already look correct while migration-only adapters, helper shims, compile-boundary leakage, or profile-only convenience paths still determine accepted outcomes.

The purpose of this wave is therefore not to introduce a new feature migration slice.
Its purpose is to convert tolerated migration residue into one of two final states:

- deleted because the migrated path no longer needs it
- explicitly quarantined, diagnosable, profile-bounded, and removable because migration still needs it temporarily

Wave F is the final cleanup and hardening wave.
It must not become a stealth redesign of earlier subsystem semantics.

---

## Ownership

This specification owns:

- the repository-wide current-state inventory of migration residue left after Waves A through E
- the final end-state rules for legacy residue in boot, scene, scope, resolver, command, value, blackboard, representative gameplay helpers, and compile boundaries
- the distinction between accepted-path deletion and bounded quarantine-only remainder
- the compile-boundary completion rules that finish the partial asmdef split without pretending the repo is still a total monolith
- the executable hardening model that turns legacy removability, profile bounds, and regression prevention into acceptance gates
- the diagnostics and evidence requirements that make remaining residue auditable rather than implicit
- the non-completion rules that prevent green gameplay from being misread as migration completion when residue still decides accepted outcomes

This specification does not own:

- boot semantics already owned by v2 and cut over by Wave A
- scope and service semantics already owned by v2 and cut over by Wave B
- command semantics already owned by v2 and cut over by Wave C
- generic value and DynamicValue semantics already owned by v2 and cut over by Wave D
- representative gameplay migration semantics already owned by Wave E
- scalar specialization semantics owned by [../v2/10_1_ScalarRuntimeAndBindingSpec.md](../v2/10_1_ScalarRuntimeAndBindingSpec.md)
- a new gameplay feature census or a new representative gameplay bundle
- a blanket promise that every historical legacy file is deleted immediately even when a bounded quarantine bridge remains temporarily necessary

Wave F owns migration completion residue.
It must not reopen earlier waves just because cleanup happens late.

---

## Purpose

Wave F defines what it means for v2.1 migration to actually finish.

Core statements:

```text
Representative migrated behavior is necessary, but it is not sufficient.

Wave F is complete only when accepted runtime paths no longer depend on migration-only legacy residue.

If compatibility code remains, it must be explicit, diagnosable, profile-bounded, removable, and non-authoritative.

Release acceptance must fail closed on prohibited runtime legacy paths.
```

This wave therefore answers the final migration question:

```text
Is legacy still deciding accepted outcomes,
or is it only visible as bounded, removable debt?
```

If the answer is the former, Wave F is not complete.

---

## Scope

Wave F defines:

- the current-state inventory of migration residue that remains after Waves A through E
- the preserved contracts that still survive final cleanup
- the target end-state for accepted runtime paths and any bounded quarantine remainder
- the residue-domain requirements for boot and scene, scope and resolver, command, value and blackboard, representative gameplay helpers, compile boundaries, and executable hardening
- the allowed-temporary remainder rules for development and test profiles
- the diagnostics, non-completion rules, acceptance gates, and legacy-removal evidence required to close v2.1

---

## Non-Goals

Wave F does not define:

- a redesign of boot, scope, service, lifecycle, command, value, scalar, or diagnostics semantics
- a new representative gameplay migration wave beyond the GameScene bundle already owned by Wave E
- a promise to preserve `RuntimeLifetimeScope`, `RuntimeContainerBuilder`, `CommandRunnerMB`, `BlackboardMB`, `BlackboardService`, or other legacy hosts as first-class architecture APIs
- a requirement to keep development convenience paths simply because direct play or local gameplay still succeeds
- a substitute for v2 `LegacyCompat`, test, or asmdef semantics
- a rule that all legacy text or dead code must be deleted even when that work is unrelated to migration completion

Wave F finishes migration.
It does not create a third architecture state between legacy and v2.

---

## Relationship to Other Specs

| Spec | Relationship to Wave F |
| --- | --- |
| [00_KernelV21MigrationOverviewSpec.md](00_KernelV21MigrationOverviewSpec.md) | Defines Wave F as the final legacy-removal and hardening slice within v2.1. |
| [01_WaveABootAndSceneEntryCutoverSpec.md](01_WaveABootAndSceneEntryCutoverSpec.md) | Hands off remaining live-entry, loading, and scene-root residue that must no longer decide accepted outcomes. |
| [02_WaveBScopeAndServiceCompositionCutoverSpec.md](02_WaveBScopeAndServiceCompositionCutoverSpec.md) | Hands off remaining installer-driven composition, resolver, and scope-authority residue for final cleanup or quarantine. |
| [03_WaveCCommandDispatchCutoverSpec.md](03_WaveCCommandDispatchCutoverSpec.md) | Hands off remaining command adapters, convenience paths, and command-host cleanup for final demotion or removal. |
| [04_WaveDValueBlackboardAndVarCutoverSpec.md](04_WaveDValueBlackboardAndVarCutoverSpec.md) | Hands off final blackboard, var, registry-lookup, and value-adapter cleanup. |
| [05_WaveERepresentativeGameplaySystemsCutoverSpec.md](05_WaveERepresentativeGameplaySystemsCutoverSpec.md) | Hands off representative gameplay helper shims and scene-local compatibility hosts whose authority must be removed after migrated proof exists. |
| [../v2/13_LegacyCompatBoundarySpec.md](../v2/13_LegacyCompatBoundarySpec.md) | Owns the quarantine model. Wave F decides what still qualifies for that quarantine and what must be removed. |
| [../v2/15_TestAndValidationSpec.md](../v2/15_TestAndValidationSpec.md) | Owns the executable proof model that Wave F must turn into migration-completion gates. |
| [../v2/16_ImplementationMilestoneOrderSpec.md](../v2/16_ImplementationMilestoneOrderSpec.md) | Supplies the M12 and M15 hardening expectations that Wave F mirrors at migration-spec level. |
| [../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md](../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md) | Owns the target compile graph. Wave F defines what completion means relative to the repo's current partial split. |

Wave F is downstream of both the earlier migration waves and the v2 quarantine or validation specs.
It must not weaken them to make closeout easier.

---

## Current-State Migration Residue Inventory

This section records the residue that still matters after Waves A through E.
It is migration evidence, not target policy.

### Observation Traceability

| Observation | Evidence Type | Migration Pressure |
| --- | --- | --- |
| `ScopeFeatureInstallerUtility` still uses `GetComponentsInChildren` and `Transform.parent` traversal to decide feature-installer ownership. | Source | accepted runtime composition must stop depending on hierarchy-derived installer discovery |
| `RuntimeLifetimeScope` still caches owned installers, constructs a `RuntimeContainerBuilder`, and invokes `InstallFeature(builder, this)` during scope build. | Source | accepted scope composition must stop depending on legacy installer-owned build authority |
| The runtime resolver boundary in [../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs](../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs) is still registration-driven, even though `RuntimeResolverHubTests` already prove host-component fallback is not accepted behavior. | Source | Wave F must finish removing dependency on legacy resolver or container authority rather than restating already-hardened fail-closed pieces |
| `CommandRunnerMB` still acts as a giant scene-facing installer that bulk-registers command executors and services, and it still references VContainer-facing runtime code. | Source | accepted command authority must no longer depend on a legacy bulk-registration host |
| `BlackboardMB` still combines installer behavior, acquire or release participation, local value initialization, debug wiring, and optional transform auto-write. | Source | final value authority must not keep a mixed-responsibility MonoBehaviour as architectural owner |
| `BlackboardService` still exposes hierarchical global get or set behavior and create-root style fallback paths. | Source | accepted value truth must not rely on hierarchical blackboard repair or root creation fallback |
| `VarIdResolver` now fails closed instead of inventing negative IDs, but stable-key resolution still remains a runtime-facing convenience surface. | Source | Wave F must keep stable-key convenience out of accepted runtime truth and hot paths |
| `VarKeyRegistryLocator` no longer creates runtime fallback registry instances, but it still performs runtime registry lookup through `Resources.Load`. | Source | accepted value identity must not quietly depend on runtime asset lookup convenience |
| `ChangeGameStateExecutor` still resolves `IGameStateMachineService` by walking nearest `Scene`, `Field`, or `Project` scope ancestors when the origin scope does not resolve it directly. | Source | representative gameplay success must not still depend on helper traversal deciding accepted service truth |
| Kernel asmdefs and editor test asmdefs already exist, but common legacy or gameplay residues are not yet visibly finished as explicit `GameLib.Legacy.*` quarantine assemblies. | Workspace search | Wave F must complete a partial split rather than describe the repo as if no split existed |
| `LegacyMigrationModel` and `LegacyCompatBoundaryTests` already validate adapter metadata, profile bounds, and removal policy shape. | Source | Wave F acceptance must consume those gates as migration-completion evidence rather than leave them as disconnected kernel-only infrastructure |

### Representative Anchors

- [../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs](../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs)
- [../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs)
- [../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs](../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs)
- [../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs)
- [../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs](../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs)
- [../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs](../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs)
- [../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs)
- [../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs)
- [../../Game/Scripts/Flow/Commands/GameStateMachineExecutors.cs](../../Game/Scripts/Flow/Commands/GameStateMachineExecutors.cs)
- [../../GameLib/Script/Kernel/Validation/LegacyMigrationModel.cs](../../GameLib/Script/Kernel/Validation/LegacyMigrationModel.cs)
- [../../GameLib/Script/Kernel/Abstractions/GameLib.Kernel.Abstractions.asmdef](../../GameLib/Script/Kernel/Abstractions/GameLib.Kernel.Abstractions.asmdef)
- [../../GameLib/Script/Kernel/IR/GameLib.Kernel.IR.asmdef](../../GameLib/Script/Kernel/IR/GameLib.Kernel.IR.asmdef)
- [../../GameLib/Script/Kernel/Validation/GameLib.Kernel.Validation.asmdef](../../GameLib/Script/Kernel/Validation/GameLib.Kernel.Validation.asmdef)
- [../../GameLib/Script/Kernel/Generation/GameLib.Kernel.Generation.asmdef](../../GameLib/Script/Kernel/Generation/GameLib.Kernel.Generation.asmdef)
- [../../GameLib/Script/Kernel/Boot/GameLib.Kernel.Boot.asmdef](../../GameLib/Script/Kernel/Boot/GameLib.Kernel.Boot.asmdef)
- [../../Editor/Tests/KernelBoot/GameLib.Tests.Kernel.Boot.Editor.asmdef](../../Editor/Tests/KernelBoot/GameLib.Tests.Kernel.Boot.Editor.asmdef)
- [../../Editor/Tests/KernelDiagnostics/GameLib.Tests.Kernel.Editor.asmdef](../../Editor/Tests/KernelDiagnostics/GameLib.Tests.Kernel.Editor.asmdef)
- [../../Editor/Tests/LegacyCompatBoundaryTests.cs](../../Editor/Tests/LegacyCompatBoundaryTests.cs)
- [../../Editor/Tests/RuntimeResolverHubTests.cs](../../Editor/Tests/RuntimeResolverHubTests.cs)
- [../../Editor/Tests/KernelDiagnostics/KernelDiagnosticsAsmdefBoundaryTests.cs](../../Editor/Tests/KernelDiagnostics/KernelDiagnosticsAsmdefBoundaryTests.cs)

---

## Preserved Contracts

Wave F preserves the same narrow contract strategy as v2.1 overall.
It does not preserve the legacy hosts that happened to carry those contracts earlier in migration.

| Contract Surface | Current Anchor | Wave F Requirement |
| --- | --- | --- |
| existing command field shape and payload meaning | [../../GameLib/Script/Common/Commands/VNext/Commands](../../GameLib/Script/Common/Commands/VNext/Commands) | existing authored command payload shapes remain consumable while final cleanup removes legacy command authority beneath them |
| existing `DynamicValue` authoring surface | [../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicValue.cs](../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicValue.cs) | authoring continuity remains stable even while runtime helper residue is removed or quarantined |
| existing `ValueStore` generated key identity | [../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs) | generated value-key identity remains stable while runtime lookup convenience stops deciding accepted truth |
| representative player-visible gameplay continuity already proven by Wave E | [05_WaveERepresentativeGameplaySystemsCutoverSpec.md](05_WaveERepresentativeGameplaySystemsCutoverSpec.md) | final cleanup must not break the representative gameplay continuity already accepted as migrated behavior |

The following are explicitly not preserved surfaces:

- `RuntimeLifetimeScope` as a target architecture owner
- the runtime resolver or container boundary in [../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs](../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs)
- `CommandRunnerMB` as authoritative command composition
- `BlackboardMB` as mixed value-authority host
- `BlackboardService` global fallback behavior as accepted runtime truth
- representative gameplay helper traversal that only exists to bridge legacy gaps

---

## Owned Migration Goals

Wave F must achieve all of the following:

- remove accepted-path dependence on migration-only legacy adapters, helper shims, and residue hosts
- convert any remaining compatibility code into explicit, diagnosable, profile-bounded, removable quarantine-only residue
- make release acceptance fail closed when runtime legacy adapters or prohibited compatibility paths remain required
- finish compile-boundary quarantine relative to the repo's current partial kernel and test asmdef split
- preserve the narrow gameplay-facing contract while refusing to preserve legacy architecture ownership beneath it
- make migration completion auditable through executable gates and legacy-removal evidence rather than by visual success alone

---

## Target End-State

Wave F target end-state is intentionally asymmetric.

1. The accepted runtime path for live boot, direct play, representative gameplay, command execution, and value access runs through verified kernel authority without migration-only legacy residue deciding required outcomes.
2. Compatibility code may remain only when it is explicit, diagnosable, profile-bounded, removable, and not authoritative for accepted success.
3. Release profile rejects runtime-capable legacy adapters, temporary bridges, forbidden fallbacks, and compile-boundary leakage that would make migration residue shippable.
4. Development and test profiles may expose bounded residue only when removal policy, diagnostics code, target replacement, and tracking condition are all explicit.
5. Compile boundaries make `GameLib.Kernel.*`, `GameLib.Legacy.*`, and `GameLib.Tests.*` directional rules visible enough that regression can be detected by tooling.
6. Migration completion evidence is concrete enough to answer which residue was deleted, which residue remains bounded, and which gates prove it.

### Allowed Temporary Remainders Matrix

| Residue State | Allowed? | Required Bound |
| --- | --- | --- |
| a legacy code file still exists in the repository but accepted runtime paths no longer depend on it | Yes | removal direction or ownership must still be understandable from docs or code placement |
| a development-only or test-only adapter remains | Yes | explicit adapter metadata, diagnostics visibility, removal policy, and profile bounds are mandatory |
| a runtime-capable legacy adapter remains enabled for Release | No | release acceptance must fail |
| representative gameplay succeeds only because helper traversal or compatibility repair still decides a required outcome | No | Wave F acceptance must fail |
| a kernel asmdef references legacy asmdefs, or production assemblies reference test asmdefs | No | compile-boundary gate must fail |

Wave F does not require an empty repository.
It requires a non-authoritative legacy residue state.

---

## Residue Domain Requirements

### WF-0 Inventory and Preservation Floor

Wave F begins by making residue explicit.

Required rules:

1. Residue must be inventoried by domain: boot and scene, scope and resolver, command, value and blackboard, representative gameplay helpers, compile boundaries, and hardening gates.
2. The v2.1 preservation floor remains unchanged unless an explicit v2.1 doc revision says otherwise.
3. Legacy hosts that currently carry preserved contracts must not be reclassified as preserved just because cleanup happens late.
4. Any claimed preserved surface outside the existing preservation floor requires explicit documentation, not assumption.

### WF-1 Boot and Scene Residue

Wave F finalizes the live-entry cleanup left after Wave A.

Required rules:

1. Accepted live boot and accepted direct play must both enter through the verified kernel path rather than through runtime auto-bootstrap as steady-state authority.
2. RuntimeInitializeOnLoadMethod authority, scene-root duplicate cleanup, loading fallback discovery, and temporary live-entry wrappers may remain only as bounded quarantine residue, not as accepted architecture owners.
3. If the live game still requires legacy scene-root authority to reach a playable state, Wave F is not complete.
4. Release profile must reject runtime boot bridges that keep legacy authority alive as a repair path.

### WF-2 Scope, Service, Installer, and Resolver Residue

Wave F finalizes the composition cleanup left after Wave B.

Required rules:

1. `GetComponentsInChildren` installer collection and `Transform.parent` ownership inference must not remain accepted runtime composition truth.
2. `RuntimeLifetimeScope`-owned installer caching and `InstallFeature(builder, this)` build authority must either be deleted or demoted to explicit quarantine-only residue.
3. Registration-driven runtime resolver or container construction may remain only as a bounded legacy boundary; the accepted kernel path must not depend on it as authoritative scope or service truth.
4. Ancestor-based service or scope traversal that survives in gameplay helpers must be treated as residue, not as valid composition semantics.
5. Kernel-side accepted paths must not depend on legacy scope or resolver types in reverse dependency direction.

### WF-3 Command Residue

Wave F finalizes the command cleanup left after Wave C.

Required rules:

1. `CommandRunnerMB` must not remain the authoritative command registration or executor composition owner for accepted runtime behavior.
2. Preserved command payload surfaces may remain, but accepted command execution must not require a legacy giant installer or bulk executor registration host.
3. Any remaining command adapter or compatibility shim must be explicitly classified, diagnosable, and removable.
4. Release profile must reject runtime command adapters that would keep legacy command authority shippable.

### WF-4 Value, Blackboard, and Var Residue

Wave F finalizes the value cleanup left after Wave D.

Required rules:

1. `BlackboardMB` may not remain a mixed-responsibility owner of value authority, lifecycle, debug, and transform write behavior in accepted architecture.
2. `BlackboardService` hierarchical global lookup, create-local, create-game-logic-root, and create-root fallback behavior must not decide accepted value truth.
3. Runtime stable-key convenience and runtime registry lookup must not be accepted truth paths for value identity.
4. If a value-facing compatibility adapter remains, it must be explicit, profile-bounded, and removable rather than silently reachable from accepted gameplay flows.
5. Generated value-key identity and authored `DynamicValue` surfaces remain preserved even while runtime lookup convenience is finally hardened.

### WF-5 Representative Gameplay Helper Cleanup

Wave F finalizes the gameplay helper cleanup left after Wave E.

Required rules:

1. Representative gameplay systems must continue to succeed through migrated authority without helper traversal deciding accepted outcomes.
2. Scene-local hosts, helper shims, or compatibility wrappers that remain around representative gameplay systems must not become hidden authority owners.
3. The `ChangeGameStateExecutor` ancestor traversal pattern is accepted only as residue evidence, not as steady-state authority.
4. If representative gameplay only stays green because helper cleanup was deferred, Wave F is not complete.

### WF-6 Compile Boundary and Package Quarantine

Wave F finalizes compile-boundary work relative to the current repo state.

Required rules:

1. Wave F must acknowledge the existing partial split: kernel asmdefs and some editor test asmdefs already exist.
2. Remaining legacy runtime and migration residue must move toward explicit `GameLib.Legacy.*` or equivalent quarantine residence rather than continue as unlabeled common code.
3. `GameLib.Kernel.*` must not reference `GameLib.Legacy.*`, and production assemblies must not reference `GameLib.Tests.*`.
4. `VContainer` usage must remain inside bounded legacy quarantine assemblies if it remains at all.
5. Unity Test Framework references must remain in `GameLib.Tests.*` assemblies only.
6. If a shared type cannot be moved without breaking dependency direction, the correct remedy is a boundary split, not silent cross-reference tolerance.

### WF-7 Executable Hardening and Legacy-Removal Evidence

Wave F finalizes acceptance by binding residue cleanup to gates.

Required rules:

1. Legacy boundary tests, asmdef boundary tests, static forbidden-pattern tests, direct-play verified flow, and integration smoke must be part of Wave F acceptance evidence.
2. Required regression families include hierarchy discovery, transform inference, runtime `Resources.Load` fallback, command or service discovery, stable-key runtime lookup, legacy fallback re-entry, and compile-boundary inversion.
3. Legacy-removal evidence must be concrete enough to show which runtime-capable residues were deleted and which remain only as bounded adapters.
4. Missing hardening evidence is a Wave F failure even when gameplay still appears correct.

---

## Subphases

| Phase | Name | Main Output | Exit Signal |
| --- | --- | --- | --- |
| WF-0 | Inventory and Preservation Floor | residue inventory and preserved-contract confirmation | residue is explicitly classified and preservation floor has not silently widened |
| WF-1 | Boot and Scene Residue | live-entry and scene-residue demotion rules | accepted live boot and direct play no longer depend on legacy boot authority |
| WF-2 | Scope and Resolver Residue | installer, resolver, and scope-residue demotion rules | accepted composition no longer depends on hierarchy-driven or installer-driven legacy authority |
| WF-3 | Command Residue | command-host and adapter demotion rules | accepted command execution no longer depends on legacy bulk registration hosts |
| WF-4 | Value and Blackboard Residue | final value-residue demotion rules | accepted value truth no longer depends on blackboard fallback or runtime lookup convenience |
| WF-5 | Representative Gameplay Helper Cleanup | representative gameplay helper cleanup rules | representative gameplay no longer needs compatibility helpers to decide required outcomes |
| WF-6 | Compile Boundary and Package Quarantine | partial-split completion rules | kernel, legacy, and test dependency directions are auditable and enforceable |
| WF-7 | Gates and Legacy-Removal Evidence | executable hardening and residue evidence | migration completion is auditable through gates and residue evidence rather than visual success |

Subphases may overlap in implementation.
They must not overlap in ownership.

---

## Diagnostics and Failure Policy

Wave F requires residue visibility that is specific enough to fail reviews and gates.

Required diagnostics evidence includes:

- `KernelDiagnostic` output for legacy boundary failures and profile violations
- `LegacyMigrationReport` output that captures adapter kind, source, target replacement, removal condition, and blocking issue
- command or dynamic trace evidence where representative gameplay cleanup is under review
- asmdef or compile-boundary reports that can show dependency inversion clearly
- deterministic static-rule reports for forbidden discovery, fallback, and lookup patterns

Representative failure classes:

| Failure ID | Condition | Required Result |
| --- | --- | --- |
| `V21-WF-BOOT-001` | accepted live boot or direct play still depends on legacy auto-bootstrap or scene-root authority | Wave F acceptance fails |
| `V21-WF-SCOPE-001` | accepted runtime composition still depends on hierarchy discovery, installer-owned build authority, or legacy resolver boundary | Wave F acceptance fails |
| `V21-WF-CMD-001` | accepted command execution still depends on legacy bulk registration or unbounded command adapters | Wave F acceptance fails |
| `V21-WF-VALUE-001` | accepted value behavior still depends on blackboard fallback, root creation fallback, runtime registry lookup, or stable-key runtime truth | Wave F acceptance fails |
| `V21-WF-GAME-001` | representative gameplay only remains green because helper traversal or compatibility repair still decides required outcomes | Wave F acceptance fails |
| `V21-WF-ASMDEF-001` | kernel-to-legacy or production-to-test dependency inversion remains | compile-boundary gate fails |
| `V21-WF-LEGACY-001` | a remaining adapter lacks removal policy, diagnostics code, profile bounds, or explicit target replacement metadata | legacy-boundary gate fails |
| `V21-WF-GATE-001` | required executable hardening evidence or legacy-removal evidence is missing | migration completion claim fails |

Wave F does not allow silent residue.
If residue remains important enough to keep, it is important enough to diagnose.

---

## Acceptance Criteria

Wave F is complete only when all of the following are true:

- live boot and direct play both use verified kernel authority rather than a legacy side path deciding accepted success
- accepted runtime composition no longer depends on hierarchy-derived installer ownership, legacy installer build authority, or legacy resolver authority
- accepted command execution no longer depends on `CommandRunnerMB` or equivalent legacy bulk-registration ownership
- accepted value behavior no longer depends on blackboard hierarchical fallback, root-creation repair, runtime registry lookup convenience, or stable-key runtime truth
- representative gameplay systems proven by Wave E no longer require compatibility helpers to decide required outcomes
- any remaining legacy adapters are explicit, diagnosable, profile-bounded, removable, and non-authoritative
- release profile rejects prohibited runtime legacy adapters and compile-boundary leakage
- compile boundaries make kernel, legacy, and test dependency direction auditable and enforceable enough to catch regression
- executable hardening and legacy-removal evidence can distinguish true migration completion from gameplay-only success
- the v2.1 preservation floor remains stable

---

## Non-Completion Rules

Wave F is not complete if any of the following remain true:

- the repository still claims migration completion even though accepted runtime paths require legacy boot, scope, command, value, or gameplay-helper residue
- remaining adapters are visible only in code archaeology and not in explicit metadata, diagnostics, or profile policy
- Release can still ship runtime-capable legacy adapters or dependency inversions without a failing gate
- representative gameplay remains green only because helper cleanup was deferred indefinitely
- legacy-removal evidence cannot explain what was removed, what remains, and why the remainder is bounded

The following by themselves do not automatically mean failure:

- a legacy file still exists in the repository
- a development-only adapter exists for bounded migration support

Those states become failures only when they remain authoritative, invisible, or unremovable.

---

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V21-WF-01 | Confirm Wave F owns final residue cleanup and hardening rather than new subsystem semantics. | Ownership and Non-Goals must exclude redesign of boot, scope, command, value, scalar, and feature semantics. |
| TC-V21-WF-02 | Confirm Wave F inventories actual residue anchors rather than abstract categories only. | The residue inventory must name concrete runtime, test, and asmdef anchors from the current repository. |
| TC-V21-WF-03 | Confirm the preservation floor remains narrow during final cleanup. | Preserved Contracts must still be limited to command field shape, DynamicValue authoring surface, generated value-key identity, and representative gameplay continuity already proven by Wave E. |
| TC-V21-WF-04 | Confirm boot and direct play are not accepted when legacy live-entry authority still decides the outcome. | Acceptance and Non-Completion rules must reject legacy auto-bootstrap as accepted authority. |
| TC-V21-WF-05 | Confirm scope or resolver residue is treated as final cleanup debt rather than accepted architecture. | WF-2 and diagnostics must reject hierarchy-driven installer ownership and legacy resolver authority as accepted truth. |
| TC-V21-WF-06 | Confirm command residue is finally demoted or removed. | WF-3 and Acceptance must reject legacy bulk-registration hosts as accepted command authority. |
| TC-V21-WF-07 | Confirm value and blackboard residue is finally demoted or removed. | WF-4 must reject blackboard fallback, root creation fallback, runtime registry lookup convenience, and stable-key runtime truth as accepted value authority. |
| TC-V21-WF-08 | Confirm representative gameplay helper cleanup is mandatory after Wave E proof exists. | WF-5 and Acceptance must reject gameplay success that still depends on compatibility traversal or helper repair. |
| TC-V21-WF-09 | Confirm compile-boundary completion is written against the current partial split rather than an outdated monolith assumption. | WF-6 must mention the existing kernel and editor test asmdefs and define completion relative to that current state. |
| TC-V21-WF-10 | Confirm remaining adapters are explicit, removable, and Release-forbidden when runtime-capable. | Target End-State, diagnostics, and Acceptance must require adapter metadata and Release rejection policy. |
| TC-V21-WF-11 | Confirm executable hardening and legacy-removal evidence are required for migration completion. | WF-7 and Acceptance must require gates and evidence, not only visible gameplay success. |
| TC-V21-WF-12 | Confirm Wave F does not incorrectly require deleting every historical legacy line. | Allowed Temporary Remainders and Non-Completion rules must distinguish code existence from accepted-path dependence. |

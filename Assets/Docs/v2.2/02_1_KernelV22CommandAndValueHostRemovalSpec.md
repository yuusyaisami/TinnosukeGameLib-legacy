# Kernel v2.2 Command and Value Host Removal Specification

## Document Status

- Document ID: 02_1_KernelV22CommandAndValueHostRemovalSpec
- Status: Draft
- Role: defines the M2 cutover that removes scene-facing command and value hosts from accepted runtime execution and moves command/value session ownership under the kernel-owned host chain
- Depends on:
  - [00_KernelV22CompletionOverviewSpec.md](00_KernelV22CompletionOverviewSpec.md)
  - [01_KernelV22AuthorityAndServiceCensusSpec.md](01_KernelV22AuthorityAndServiceCensusSpec.md)
  - [02_KernelV22KernelOnlyHostSpec.md](02_KernelV22KernelOnlyHostSpec.md)
  - [../v2/09_CommandCatalogRuntimeSpec.md](../v2/09_CommandCatalogRuntimeSpec.md)
  - [../v2/10_ValueSchemaAndStoreSpec.md](../v2/10_ValueSchemaAndStoreSpec.md)
  - [../v2/10_2_DynamicValueEvaluationSpec.md](../v2/10_2_DynamicValueEvaluationSpec.md)
  - [../v2/12_UnityAuthoringBridgeSpec.md](../v2/12_UnityAuthoringBridgeSpec.md)
  - [../v2/13_LegacyCompatBoundarySpec.md](../v2/13_LegacyCompatBoundarySpec.md)
  - [../v2/15_TestAndValidationSpec.md](../v2/15_TestAndValidationSpec.md)
  - [../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md](../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md)
  - [../v2.1/03_WaveCCommandDispatchCutoverSpec.md](../v2.1/03_WaveCCommandDispatchCutoverSpec.md)
  - [../v2.1/04_WaveDValueBlackboardAndVarCutoverSpec.md](../v2.1/04_WaveDValueBlackboardAndVarCutoverSpec.md)
  - [../v2.1/06_WaveFLegacyRemovalAndHardeningSpec.md](../v2.1/06_WaveFLegacyRemovalAndHardeningSpec.md)
  - [05_KernelV22MilestoneOrderSpec.md](05_KernelV22MilestoneOrderSpec.md)
- Provides foundation for:
  - [03_KernelV22ServiceFamilyCutoverSpec.md](03_KernelV22ServiceFamilyCutoverSpec.md)
  - [04_KernelV22LegacyDeletionAndHardeningSpec.md](04_KernelV22LegacyDeletionAndHardeningSpec.md)
  - [05_KernelV22MilestoneOrderSpec.md](05_KernelV22MilestoneOrderSpec.md)

### Revision Note

This revision creates the dedicated M2 specification that was previously missing from the v2.2 package.

M1 establishes a kernel-owned live host chain.
That is necessary, but it is not enough.

The accepted runtime path can still remain architecturally legacy if command execution still depends on CommandRunnerMB and value truth still depends on BlackboardMB or BlackboardService fallback.

M2 therefore exists to remove the scene-facing command and value hosts from accepted runtime authority before feature-family cutover begins.

---

## Ownership

This specification owns:

- command host removal for M2
- value host removal for M2
- the authoring-only split for CommandRunnerAuthoring and BlackboardAuthoring
- the handoff from scene-facing command/value hosts to kernel-owned command/value sessions
- mixed command/value host diagnostics and acceptance gates for M2
- the rule that feature families may consume command/value sessions after M2 but may not create those sessions themselves

This specification does not own:

- live host ownership already defined by [02_KernelV22KernelOnlyHostSpec.md](02_KernelV22KernelOnlyHostSpec.md)
- full boot and scene-flow family migration, which belongs to [03_KernelV22ServiceFamilyCutoverSpec.md](03_KernelV22ServiceFamilyCutoverSpec.md)
- representative gameplay/application family migration, which belongs to [03_KernelV22ServiceFamilyCutoverSpec.md](03_KernelV22ServiceFamilyCutoverSpec.md) and later milestones
- release-wide deletion and hardening, which belong to [04_KernelV22LegacyDeletionAndHardeningSpec.md](04_KernelV22LegacyDeletionAndHardeningSpec.md)
- target command semantics or target value semantics already owned by v2

---

## Purpose

The purpose of this document is to remove the last scene-facing command/value host ambiguity before family cutover begins.

Core statements:

```text
Kernel-owned host truth is not sufficient if command execution still depends on CommandRunnerMB.
Kernel-owned host truth is not sufficient if value init or value access still depends on BlackboardMB or BlackboardService fallback.

CommandRunnerAuthoring and BlackboardAuthoring may remain as declaration inputs.
CommandRunnerMB and BlackboardMB must stop deciding accepted runtime execution.
```

M2 is therefore about host authority, not about redesigning every command payload or every value surface.

---

## Scope

This specification defines:

- command host demotion from CommandRunnerMB to kernel-owned command session authority
- value host demotion from BlackboardMB to kernel-owned value session/init authority
- declaration-only preservation rules for CommandRunnerAuthoring and BlackboardAuthoring
- host-side rejection of command/value fallback repair that remains required for accepted execution
- diagnostics and acceptance gates for the above

---

## Non-Goals

This specification does not define:

- a blanket rewrite of every command payload class
- full command identity/catalog redesign beyond the host boundary needed for accepted execution
- a blanket rewrite of every value-binding or DynamicSource implementation
- the final gameplay-family migration of every command/value consumer
- final repository-wide deletion of every legacy helper

M2 may preserve narrow authoring surfaces.
It does not preserve scene-facing command/value host ownership.

---

## Relationship to Other Specs

| Spec | Relationship to 02_1 |
| --- | --- |
| [00_KernelV22CompletionOverviewSpec.md](00_KernelV22CompletionOverviewSpec.md) | Defines the continuity contract, abolition target, and rule that release accepted path becomes kernel-only. |
| [01_KernelV22AuthorityAndServiceCensusSpec.md](01_KernelV22AuthorityAndServiceCensusSpec.md) | Defines the five-class vocabulary used here to separate declaration inputs, delete targets, and kernel-owned authority. |
| [02_KernelV22KernelOnlyHostSpec.md](02_KernelV22KernelOnlyHostSpec.md) | Supplies the host chain that command/value sessions must attach to. |
| [03_KernelV22ServiceFamilyCutoverSpec.md](03_KernelV22ServiceFamilyCutoverSpec.md) | Consumes the command/value host boundary established here before migrating feature families. |
| [04_KernelV22LegacyDeletionAndHardeningSpec.md](04_KernelV22LegacyDeletionAndHardeningSpec.md) | Uses the M2 boundary to decide what residue may still remain before release hardening closes. |
| [../v2/09_CommandCatalogRuntimeSpec.md](../v2/09_CommandCatalogRuntimeSpec.md) | Owns target command runtime meaning that M2 must consume rather than reinterpret. |
| [../v2/10_ValueSchemaAndStoreSpec.md](../v2/10_ValueSchemaAndStoreSpec.md) | Owns target value runtime meaning that M2 must consume rather than reinterpret. |
| [../v2/10_2_DynamicValueEvaluationSpec.md](../v2/10_2_DynamicValueEvaluationSpec.md) | Owns dynamic evaluation runtime semantics that M2 must attach to the host chain instead of leaving under Blackboard-owned truth. |
| [../v2.1/03_WaveCCommandDispatchCutoverSpec.md](../v2.1/03_WaveCCommandDispatchCutoverSpec.md) | Supplies command migration evidence and authority vocabulary that M2 narrows into the host-removal slice. |
| [../v2.1/04_WaveDValueBlackboardAndVarCutoverSpec.md](../v2.1/04_WaveDValueBlackboardAndVarCutoverSpec.md) | Supplies value migration evidence and authority vocabulary that M2 narrows into the host-removal slice. |

02_1 consumes upstream semantics.
It must not reinterpret them to preserve legacy command/value hosts.

---

## Current-State Command and Value Host Inventory

This section records the current command/value host pressure.
It is evidence, not target policy.

| Observation | Evidence type | M2 pressure |
| --- | --- | --- |
| `CommandRunnerMB` still bulk-registers executors, binds runner-domain behavior, and bridges verified command runtime session access through a scene-facing MonoBehaviour. | Source | accepted command execution can still depend on a legacy scene-facing host even after M1 host truth is fixed |
| `CommandRunnerAuthoring` still carries default var and debug-view intent in the same file as the runtime host. | Source | declaration surface must be split from runtime authority rather than deleted blindly |
| `CommandRunner` and `CommandCatalogService` already represent command-session runtime pressure, but accepted command truth still reaches them through scene-facing installation paths. | Source | M2 must attach command session creation to the kernel-owned host chain before families consume it |
| `VerifiedCommandRuntimeBridge` already exposes verified command runtime session access. | Source | M2 should reuse this as an authority handoff boundary instead of rebuilding command truth in scene hosts |
| `BlackboardMB` still registers services, local/grid init, debug wiring, and transform-write behavior from one scene-facing MonoBehaviour. | Source | accepted value init and store truth can still depend on a mixed scene-facing host |
| `BlackboardService` still exposes hierarchical fallback and root-repair behavior. | Source | accepted value access can still depend on fallback truth even if host ownership is otherwise migrated |
| `BlackboardValueInitRuntime` and `DynamicEvaluationRuntime` exist, but accepted init/evaluation ownership can still remain behind Blackboard-owned paths. | Source | M2 must move init/evaluation host ownership under the kernel-owned chain before family cutover begins |

## Representative Anchors

- [../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs)
- [../../GameLib/Script/Common/Commands/VNext/Core/CommandRunner.cs](../../GameLib/Script/Common/Commands/VNext/Core/CommandRunner.cs)
- [../../GameLib/Script/Common/Commands/VNext/Core/VerifiedCommandRuntimeBridge.cs](../../GameLib/Script/Common/Commands/VNext/Core/VerifiedCommandRuntimeBridge.cs)
- [../../GameLib/Script/Common/Commands/VNext/Catalog/CommandCatalogService.cs](../../GameLib/Script/Common/Commands/VNext/Catalog/CommandCatalogService.cs)
- [../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs](../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs)
- [../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs](../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs)
- [../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardValueInitRuntime.cs](../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardValueInitRuntime.cs)
- [../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicEvaluationRuntime.cs](../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicEvaluationRuntime.cs)

---

## Preserved Contracts

M2 preserves only the declaration and continuity surfaces needed to remove runtime hosts without forcing broad content churn.

| Contract surface | Current anchor | M2 requirement |
| --- | --- | --- |
| `CommandRunnerAuthoring` serialized default vars and debug-view intent | [../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs) | declaration data remains consumable as authoring input, but accepted runtime command execution must not depend on `CommandRunnerMB` installation |
| existing command payload meaning | [../../GameLib/Script/Common/Commands/VNext/Commands](../../GameLib/Script/Common/Commands/VNext/Commands) | payload meaning remains stable while command session ownership moves away from scene-facing hosts |
| `BlackboardAuthoring` local/grid init and debug intent | [../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs](../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs) | declaration data remains consumable as authoring input, but accepted runtime value init and access must not depend on `BlackboardMB` |
| existing `DynamicValue` authoring surface | [../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicValue.cs](../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicValue.cs) | wrapper authoring remains stable while evaluation ownership moves under explicit runtime authority |
| generated value-key identity continuity | [../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs) | generated identity continuity remains stable while host ownership moves away from legacy Blackboard slices |

Preserved does not mean frozen implementation.
It means stable declaration/compatibility surfaces while runtime host ownership changes.

---

## Owned Migration Goals

M2 must achieve all of the following:

- move accepted command execution to a kernel-owned command session path instead of CommandRunnerMB installation
- move accepted value init/access to a kernel-owned value session path instead of BlackboardMB ownership
- reduce CommandRunnerAuthoring and BlackboardAuthoring to declaration-only surfaces
- make CommandRunnerMB and BlackboardMB unnecessary for representative startup and accepted execution
- make BlackboardService hierarchical fallback non-authoritative for accepted M2 execution
- keep mixed command/value host authority diagnosable and unacceptable
- leave feature families consuming command/value sessions rather than creating them ad hoc

---

## Target Authority Model

### Required Command Host Boundary

1. The kernel-owned live host chain exposes command session creation from verified input.
2. `CommandRunnerAuthoring` may remain only as declaration input.
3. `CommandRunnerMB` must not install accepted command authority.
4. Missing verified command session must fail before representative execution rather than silently reconstructing a scene-facing runner host.
5. Bulk executor registration, scene-facing runner-domain install, and lifecycle-time host creation must not remain accepted command host truth.

### Required Value Host Boundary

1. The kernel-owned live host chain exposes value session/init creation from verified input.
2. `BlackboardAuthoring` may remain only as declaration input.
3. `BlackboardMB` must not install accepted value authority.
4. Missing verified value session or init path must fail before representative execution rather than silently reconstructing Blackboard-owned truth.
5. Blackboard hierarchical fallback, root repair, and hidden init/evaluation ownership must not remain accepted value host truth.

### Shared Host Removal Rules

- feature-family migration may consume command/value sessions after M2, but it may not define those sessions itself
- temporary adapters may remain only when explicit, diagnosable, profile-bounded, and non-authoritative
- authoring-only MonoBehaviours must not be promoted back into runtime hosts merely to simplify migration sequencing

### Transitional Coexistence Rules

| Transitional condition | Allowed during M2 | Required rule |
| --- | --- | --- |
| `CommandRunnerMB` or `BlackboardMB` still exists in representative scenes | Yes | they may exist temporarily, but they must not decide accepted command/value execution truth |
| verified command/value runtime bridges coexist with legacy host code in the repository | Yes | accepted execution must originate from the verified side and diagnostics must identify legacy participation |
| family code still consumes command/value services during transition | Yes | families must consume the M2 boundary rather than creating new scene-facing hosts |
| accepted execution still requires bulk executor registration, Blackboard fallback, or root repair | No | M2 acceptance must fail because scene-facing hosts remain authoritative |

---

## M2 Subphases

### M2-0 Current-State Host Inventory

Objective:
freeze the command/value host chain before cutover work starts.

Required outputs:

- representative command/value host anchor inventory
- preserved declaration/compatibility contract table
- explicit list of scene-facing command/value hosts and verified runtime bridge anchors

Exit gate:
the current command/value host chain is traceable from representative startup into representative execution.

### M2-1 Command Host Demotion

Objective:
move accepted command execution off CommandRunnerMB.

Required outputs:

- explicit kernel-owned command-session handoff boundary
- demotion rules for CommandRunnerMB as accepted runtime host
- rejection path for bulk executor registration or runner-domain install as accepted truth

Exit gate:
representative accepted command execution no longer depends on CommandRunnerMB.

### M2-2 Value Host Demotion

Objective:
move accepted value init/access off BlackboardMB.

Required outputs:

- explicit kernel-owned value-session/init handoff boundary
- demotion rules for BlackboardMB as accepted runtime host
- rejection path for Blackboard fallback or root repair as accepted truth

Exit gate:
representative accepted value execution no longer depends on BlackboardMB.

### M2-3 Declaration-Only Split

Objective:
reduce scene-facing authoring hosts to declaration input only.

Required outputs:

- explicit declaration-only role for CommandRunnerAuthoring
- explicit declaration-only role for BlackboardAuthoring
- documented boundary between declaration input and runtime authority

Exit gate:
declaration surfaces remain usable without reauthoring while runtime ownership no longer depends on them.

### M2-4 Mixed-Authority Rejection

Objective:
make command/value host ambiguity diagnosable and unacceptable.

Required outputs:

- diagnostics for missing verified command/value session
- diagnostics for CommandRunnerMB or BlackboardMB participation as accepted host authority
- diagnostics for fallback repair still required by accepted execution

Exit gate:
mixed command/value host authority fails closed rather than being silently tolerated.

### M2-5 Acceptance Gate

Objective:
prove that command/value hosts are materially removed from accepted runtime authority.

Required outputs:

- executable verification plan for representative command/value execution on top of the M1 host chain
- diagnostics coverage for missing session, mixed authority, and fallback repair
- documentation updates that reflect the accepted command/value host boundary

Exit gate:
all M2 acceptance criteria and required test cases pass.

---

## Forbidden Shortcuts

The following shortcuts are explicitly forbidden for M2:

- keeping CommandRunnerMB or BlackboardMB as silent accepted runtime hosts
- reducing authoring surfaces to declaration input in name only while runtime execution still depends on them
- claiming M2 by deleting scene-facing hosts without establishing kernel-owned command/value sessions
- treating command/value family behavior as if it proves host removal while command/value sessions are still created ad hoc
- allowing Blackboard hierarchical fallback or root repair to remain required for accepted execution

---

## Diagnostics and Failure Policy

v2 specification 11 owns the diagnostics substrate.
M2 defines the conditions that must become diagnostic-visible and acceptance-visible.

| Code | Failure condition | Required result |
| --- | --- | --- |
| V22-M2-CMD-001 | Verified command session is missing for the accepted runtime path. | Representative command execution must fail before dispatch instead of recreating scene-facing host authority. |
| V22-M2-CMD-002 | CommandRunnerMB participates as accepted runtime host authority in a profile that claims M2 cutover. | Validation or runtime must fail with mixed-authority diagnostics instead of silently continuing. |
| V22-M2-CMD-003 | Accepted execution still requires bulk executor registration or scene-facing runner-domain installation. | M2 acceptance must fail because command host truth is still legacy. |
| V22-M2-VAL-001 | Verified value session or init path is missing for the accepted runtime path. | Representative value execution must fail before runtime fallback repair. |
| V22-M2-VAL-002 | BlackboardMB participates as accepted runtime host authority in a profile that claims M2 cutover. | Validation or runtime must fail with mixed-authority diagnostics instead of silently continuing. |
| V22-M2-VAL-003 | Accepted execution still requires BlackboardService hierarchical fallback or root repair. | M2 acceptance must fail because value host truth is still legacy. |

---

## Acceptance Criteria

M2 is complete only when all of the following are true:

- representative accepted command execution no longer depends on CommandRunnerMB
- representative accepted value execution no longer depends on BlackboardMB
- CommandRunnerAuthoring and BlackboardAuthoring remain usable as declaration-only inputs
- the kernel-owned host chain established by M1 now owns command/value session creation for accepted execution
- BlackboardService hierarchical fallback is no longer required for representative accepted execution
- mixed command/value host authority is diagnosable and unacceptable rather than silently tolerated
- feature-family migration may consume command/value sessions, but it no longer needs to create them through scene-facing hosts

---

## Handoff to Later Milestones

The following work is explicitly deferred:

- full boot/scene-flow family migration, which belongs to M3
- representative gameplay/application family migration, which belongs to M4
- final deletion and release hardening, which belong to M5 and M6

M2 should leave those milestones easier.
It must not claim to have completed them.

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V22-021-01 | Confirm CommandRunnerMB and BlackboardMB are explicit M2 delete-target hosts. | This file must mention CommandRunnerMB and BlackboardMB as scene-facing hosts that must stop deciding accepted execution. |
| TC-V22-021-02 | Confirm CommandRunnerAuthoring and BlackboardAuthoring are preserved only as declaration inputs. | This file must mention declaration-only role for both authoring surfaces. |
| TC-V22-021-03 | Confirm M2 defines explicit command/value host handoff under the M1 chain. | This file must require kernel-owned command/value session creation. |
| TC-V22-021-04 | Confirm fallback repair is rejected as accepted M2 truth. | This file must mention bulk executor registration, BlackboardService hierarchical fallback, or root repair as forbidden accepted behavior. |
| TC-V22-021-05 | Confirm M2 has explicit diagnostics coverage. | This file must mention V22-M2-CMD-001 through V22-M2-VAL-003. |
| TC-V22-021-06 | Confirm feature families are deferred consumers rather than M2 owners. | This file must state that family migration belongs to later milestones. |
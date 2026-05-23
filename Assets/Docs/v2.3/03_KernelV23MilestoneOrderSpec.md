# Kernel v2.3 Milestone Order Specification

## Document Status

- Document ID: 03_KernelV23MilestoneOrderSpec
- Status: Draft
- Role: defines execution order for v2.3 architectural correction
- Depends on:
  - [00_KernelV23OverviewSpec.md](00_KernelV23OverviewSpec.md)
  - [01_KernelV23ServiceRuntimeModelSpec.md](01_KernelV23ServiceRuntimeModelSpec.md)
  - [02_KernelV23AuthoringRegistrationFlowSpec.md](02_KernelV23AuthoringRegistrationFlowSpec.md)

## Detailed Execution Specs

This milestone-order spec is the controlling order contract.
Execution specs (05-11) are subordinate detail specifications and must conform to this document.

- [05_KernelV23M0FullMigrationContractFreezeExecutionSpec.md](05_KernelV23M0FullMigrationContractFreezeExecutionSpec.md)
- [06_KernelV23M1SpecLockAndCensusExecutionSpec.md](06_KernelV23M1SpecLockAndCensusExecutionSpec.md)
- [07_KernelV23M2KernelCommandSurfaceExecutionSpec.md](07_KernelV23M2KernelCommandSurfaceExecutionSpec.md)
- [08_KernelV23M3LeafScopeDemotionExecutionSpec.md](08_KernelV23M3LeafScopeDemotionExecutionSpec.md)
- [09_KernelV23M4RootSceneIntegrationCutoverExecutionSpec.md](09_KernelV23M4RootSceneIntegrationCutoverExecutionSpec.md)
- [10_KernelV23M5HardeningAndDeleteExecutionSpec.md](10_KernelV23M5HardeningAndDeleteExecutionSpec.md)
- [11_KernelV23M6FullProofAndReleaseClaimExecutionSpec.md](11_KernelV23M6FullProofAndReleaseClaimExecutionSpec.md)

## Milestones

### M0: Full-Migration Contract Freeze

- freeze non-negotiable completion target: 100% deletion of scope-local DI runtime authority
- freeze non-negotiable service rebuild target: all services migrated with stable name/reference contract
- freeze release rejection rule for any residual local-container authority on accepted path

Detailed execution is defined in:
- [05_KernelV23M0FullMigrationContractFreezeExecutionSpec.md](05_KernelV23M0FullMigrationContractFreezeExecutionSpec.md)

Exit criteria:
- full-migration contract approved
- no ambiguity remains on allowed service forms

### M1: Spec Lock and Census

- freeze two-form service rule (AoS / Scope-ServiceInstance)
- census all runtime paths still using scope-local DI authority
- classify MBs into declaration-only vs runtime-authority residue
- create complete service family inventory with migration owner and target form

Detailed execution is defined in:
- [06_KernelV23M1SpecLockAndCensusExecutionSpec.md](06_KernelV23M1SpecLockAndCensusExecutionSpec.md)

M1 overall exit criteria:
- residue inventory complete
- forbidden authority paths listed with source anchors
- M1.1 through M1.5 exit criteria all satisfied

### M1.x Breakdown (Execution Sequence)

#### M1.1: Rule Lock

- lock final normative text for two service forms
- lock prohibition text for scope-local DI runtime authority
- lock compatibility boundary text (name/reference continuity)

Exit criteria:
- no unresolved normative conflicts between 00/01/02/04

#### M1.2: Authority Path Census

- enumerate all runtime authority paths currently in accepted path
- tag each path with source anchor (file, symbol, call path)
- tag ownership class: kernel-owned, scope-owned, mixed, unknown

Exit criteria:
- authority census coverage reaches 100% for accepted path

#### M1.3: MB Responsibility Classification

- classify MBs into declaration-only, mixed, runtime-authority residue
- record migration action per MB family
- record immediate block conditions for high-risk MB families

Exit criteria:
- MB classification table complete for all runtime-affecting MB families

#### M1.4: Service Family Inventory Freeze

- create mandatory inventory records for all service families
- assign migration owner and target service form
- assign compatibility risk levels (name/reference)

Exit criteria:
- no service family remains without owner or target form

#### M1.5: Risk and Gate Baseline

- set baseline risk register for migration blockers
- define M2 entry gates using M1 inventory outputs
- define rejection triggers for hidden legacy authority residue

Exit criteria:
- M2 start gate package approved

### M2: Kernel Command Surface

- implement kernel-side registration/build command surface
- implement scope declaration submission endpoint contract
- block accepted path from local container build authority
- provide kernel registration/build/activate/release command handlers for both service forms

Detailed execution is defined in:
- [07_KernelV23M2KernelCommandSurfaceExecutionSpec.md](07_KernelV23M2KernelCommandSurfaceExecutionSpec.md)

M2 overall exit criteria:
- kernel command surface artifacts complete and approved
- accepted path rejects local DI authority acquisition at runtime
- M2.1 through M2.6 exit criteria all satisfied

### M2.x Breakdown (Execution Sequence)

#### M2.1: Command Contract Lock

- lock command contract for register/build/activate/deactivate/release
- lock idempotency, ordering, and failure semantics
- lock diagnostics payload schema for all command outcomes

Exit criteria:
- no unresolved command contract ambiguity remains

#### M2.2: Authoring-to-Command Mapping

- map declaration payload to deterministic command sequence
- map ServiceForm (AoS / Scope-ServiceInstance) to execution branch
- reject undeclared command target at mapping stage

Exit criteria:
- deterministic mapping table approved with no fallback path

#### M2.3: Kernel Command Handler Implementation

- implement kernel command handlers for both service forms
- enforce kernel ownership for slot/instance lifecycle operations
- prohibit scope-local authority injection into handler execution path

Exit criteria:
- all required handlers implemented and ownership checks enforced

#### M2.4: Runtime Authority Block Enforcement

- introduce hard reject path when local DI authority is requested in accepted path
- define explicit error codes and diagnostics for authority violation
- verify no silent fallback to legacy construction path

Exit criteria:
- authority violation always returns explicit structured failure

#### M2.5: Focused Runtime Verification

- execute focused runtime tests for command order, idempotency, and failure behavior
- verify declaration-only MB behavior under command-driven runtime
- verify no accepted-path runtime fallback to local container build

Exit criteria:
- focused runtime verification passes with zero authority leakage findings

#### M2.6: M3 Entry Gate Proof Package

- publish command surface proof package for M3 handoff
- publish unresolved risks and required mitigations
- block M3 start when required M2 evidence is missing

Exit criteria:
- M3 entry gate approved with complete M2 evidence set

### M3: Leaf Scope Demotion

- demote entity/ui-element domains from scope-local DI authority
- route leaf services to AoS or kernel-owned instance registry
- keep compatibility bridges only for serialization continuity
- preserve existing service names and references while replacing internal ownership model

Detailed execution is defined in:
- [08_KernelV23M3LeafScopeDemotionExecutionSpec.md](08_KernelV23M3LeafScopeDemotionExecutionSpec.md)

M3 overall exit criteria:
- all leaf-domain target services cut over to kernel-owned service forms
- accepted path in leaf domains contains zero scope-local DI runtime authority
- M3.1 through M3.6 exit criteria all satisfied

### M3.x Breakdown (Execution Sequence)

#### M3.1: Leaf Domain Freeze

- freeze leaf-domain migration scope (entity and ui-element families)
- freeze per-domain service family list and migration owner
- freeze per-family target service form (AoS or Scope-ServiceInstance)

Exit criteria:
- no leaf-domain service family remains unassigned

#### M3.2: Service Cutover Design Lock

- lock cutover design for each leaf service family
- lock compatibility bridge behavior (serialization continuity only)
- lock disallowed behavior (runtime authority via legacy local DI)

Exit criteria:
- all leaf service families have approved cutover design

#### M3.3: Leaf Runtime Path Replacement

- replace accepted-path runtime authority from local DI to kernel command handlers
- route runtime state ownership to AoS slots or kernel instance registry
- remove accepted-path runtime installer discovery in leaf domains

Exit criteria:
- leaf accepted path executes without local DI authority dependency

#### M3.4: Name/Reference Continuity Validation

- validate service naming continuity for all migrated leaf families
- validate scene/prefab/script reference continuity
- validate compatibility shell non-authoritative behavior

Exit criteria:
- no name/reference break detected in leaf-domain cutover

#### M3.5: Authority Leakage Negative Verification

- run negative verification for authority leakage and fallback behavior
- assert hard reject on legacy authority acquisition attempts
- assert no silent recovery to local-container execution path

Exit criteria:
- zero authority leakage findings in leaf-domain accepted path

#### M3.6: M4 Entry Gate Evidence Package

- publish M3 completion evidence and open risk list
- publish unresolved items requiring root/scene integration handling
- block M4 start when mandatory M3 evidence is incomplete

Exit criteria:
- M4 entry gate approved with complete M3 evidence set

### M4: Root/Scene Integration Cutover

- align scene-initial scope compile output with runtime registration flow
- enforce plan-first boot and registration ordering
- remove runtime discovery as accepted composition mechanism
- enforce declaration-only MB runtime behavior for migrated services

Detailed execution is defined in:
- [09_KernelV23M4RootSceneIntegrationCutoverExecutionSpec.md](09_KernelV23M4RootSceneIntegrationCutoverExecutionSpec.md)

M4 overall exit criteria:
- root/scene accepted path runs only from verified plan and kernel command surface
- scene-initial registration ordering is deterministic and reproducible
- M4.1 through M4.6 exit criteria all satisfied

### M4.x Breakdown (Execution Sequence)

#### M4.1: Root/Scene Boundary Freeze

- freeze root/scene integration boundary and ownership map
- freeze scene-initial scope set and registration targets
- freeze M4 migration wave order and owner assignment

Exit criteria:
- no root/scene integration target remains unassigned

#### M4.2: Plan-First Boot Contract Lock

- lock boot contract requiring verified-plan-first execution
- lock ordering rules for scene load, scope materialization, and registration commands
- lock reject behavior when verified plan is missing or mismatched

Exit criteria:
- boot contract approved with explicit reject conditions

#### M4.3: Scene Registration Path Cutover

- replace residual root/scene accepted-path discovery with plan-driven registration
- route scene-initial scope registration through kernel command surface only
- prohibit accepted-path shortcut registration from local authority holders

Exit criteria:
- scene registration accepted path is plan-driven only

#### M4.4: Deterministic Ordering and Reproducibility Verification

- verify deterministic execution order across repeated scene boot runs
- verify identical registration outcome for identical verified plan input
- verify diagnostics include plan source and ordering evidence

Exit criteria:
- reproducibility verification passes with zero ordering drift

#### M4.5: Root/Scene Authority Leakage Negative Verification

- run negative verification for root/scene legacy authority entry attempts
- assert hard reject on discovery-based runtime composition attempts
- assert no fallback to scope-local DI authority in root/scene accepted path

Exit criteria:
- zero authority leakage findings in root/scene accepted path

#### M4.6: M5 Entry Gate Evidence Package

- publish M4 completion evidence and unresolved global deletion risks
- publish compatibility shell retirement readiness status
- block M5 start when mandatory M4 evidence is incomplete

Exit criteria:
- M5 entry gate approved with complete M4 evidence set

### M5: Hardening and Delete

- delete obsolete scope-local DI authority paths
- harden diagnostics and failure behavior
- validate performance budget for high-cardinality domains
- remove temporary compatibility shells that are no longer needed after reference-safe cutover validation

Detailed execution is defined in:
- [10_KernelV23M5HardeningAndDeleteExecutionSpec.md](10_KernelV23M5HardeningAndDeleteExecutionSpec.md)

M5 overall exit criteria:
- obsolete authority paths are physically removed from accepted runtime path
- diagnostics/failure behavior satisfy hardening requirements with structured evidence
- M5.1 through M5.6 exit criteria all satisfied

### M5.x Breakdown (Execution Sequence)

#### M5.1: Deletion Boundary Freeze

- freeze final deletion boundary for obsolete authority paths
- freeze compatibility-shell retirement targets and owner assignments
- freeze protected continuity boundary (allowed serialization continuity only)

Exit criteria:
- deletion boundary inventory approved with no unknown target

#### M5.2: Obsolete Authority Path Physical Delete

- physically delete obsolete scope-local DI authority paths
- remove accepted-path entry points to deleted legacy authority
- block reintroduction routes through compatibility shims

Exit criteria:
- no deleted authority path remains reachable from accepted runtime path

#### M5.3: Diagnostics and Failure Hardening

- harden structured diagnostics for authority violations and contract failures
- harden explicit failure codes for missing plan/mapping/ownership preconditions
- prohibit silent fallback and exception swallow behavior in accepted path

Exit criteria:
- hardening verification shows explicit failure behavior for all required reject classes

#### M5.4: Performance Budget Validation

- validate runtime performance budgets after deletion and hardening changes
- verify no regression from compatibility shell retirement and authority path removal
- verify hot path constraints remain within budget envelope

Exit criteria:
- performance validation passes with no unresolved budget violation

#### M5.5: Compatibility Shell Retirement Validation

- validate retirement of temporary compatibility shells where obsolete
- validate retained shells are serialization-only and non-authoritative
- validate no reference break introduced by shell retirement

Exit criteria:
- compatibility shell state is compliant with retirement policy

#### M5.6: M6 Entry Gate Evidence Package

- publish M5 completion evidence and residual proof risks for M6
- publish final unresolved issues that block release claim
- block M6 start when mandatory M5 evidence is incomplete

Exit criteria:
- M6 entry gate approved with complete M5 evidence set

### M6: Full-Proof and Release Claim

- prove 100% migration completion across all service families
- prove zero accepted-path scope-local DI runtime authority residue
- prove service name stability and reference continuity constraints remained intact during migration

Detailed execution is defined in:
- [11_KernelV23M6FullProofAndReleaseClaimExecutionSpec.md](11_KernelV23M6FullProofAndReleaseClaimExecutionSpec.md)

M6 overall exit criteria:
- full-proof package is complete, internally consistent, and independently reviewable
- release claim satisfies all non-negotiable v2.3 completion contracts
- M6.1 through M6.6 exit criteria all satisfied

### M6.x Breakdown (Execution Sequence)

#### M6.1: Proof Scope Freeze

- freeze final proof scope covering all service families and accepted runtime paths
- freeze required evidence set and ownership for each proof section
- freeze claim boundary (what is asserted, what is out of scope)

Exit criteria:
- no mandatory proof target remains unassigned or undefined

#### M6.2: Migration Completion Proof Assembly

- assemble evidence that all service families completed migration
- prove no exempt family remains on legacy authority in accepted path
- prove migration inventory closure and owner accountability

Exit criteria:
- migration completion proof passes completeness and traceability checks

#### M6.3: Authority-Zero Proof Assembly

- assemble evidence for zero accepted-path scope-local DI runtime authority residue
- prove deletion/cutover/hardening outputs converge to zero-authority state
- prove no reachable fallback to legacy authority path

Exit criteria:
- authority-zero proof passes reachability and residue checks

#### M6.4: Continuity Proof Assembly

- assemble evidence for service name continuity at integration boundaries
- assemble evidence for scene/prefab/script reference continuity
- prove retained compatibility shells are non-authoritative and policy-compliant

Exit criteria:
- continuity proof passes with no unresolved break or policy violation

#### M6.5: Independent Validation and Claim Review

- perform independent validation of proof package consistency
- run claim review against frozen M0 contract and all milestone gate outputs
- record formal accept/reject decision with rationale

Exit criteria:
- review board decision is recorded with explicit acceptance conditions

#### M6.6: Release Claim Finalization and Publication

- finalize v2.3 release claim artifact set
- publish unresolved risks and post-release obligations if any
- block release publication when mandatory claim evidence is incomplete

Exit criteria:
- release claim package approved and publication-ready

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V23-03-01 | Confirm milestone order starts with spec lock and residue census. | M1 must include inventory and classification. |
| TC-V23-03-02 | Confirm kernel command surface is delivered before leaf demotion completion. | M2 must precede M3 exit claim. |
| TC-V23-03-03 | Confirm leaf demotion explicitly targets entity/ui-element domains. | M3 must name leaf domains and authority removal. |
| TC-V23-03-04 | Confirm final hardening requires deletion of obsolete authority paths. | M5 must require delete and gate pass. |
| TC-V23-03-05 | Confirm milestone order includes full-migration contract freeze. | M0 must require 100% deletion target and name/reference continuity target. |
| TC-V23-03-06 | Confirm milestone order includes full-proof release claim. | M6 must require all-service migration proof and zero-authority-residue proof. |
| TC-V23-03-07 | Confirm M1 is broken down into M1.x execution sequence. | Spec must define M1.1 through M1.5 with exit criteria. |
| TC-V23-03-08 | Confirm M0/M1 detailed execution specs are linked. | Spec must reference 05 and 06 as detailed execution documents. |
| TC-V23-03-09 | Confirm M2 is broken down into M2.x execution sequence. | Spec must define M2.1 through M2.6 with exit criteria. |
| TC-V23-03-10 | Confirm M2 detailed execution spec is linked. | Spec must reference 07 as detailed execution document. |
| TC-V23-03-11 | Confirm M3 is broken down into M3.x execution sequence. | Spec must define M3.1 through M3.6 with exit criteria. |
| TC-V23-03-12 | Confirm M3 detailed execution spec is linked. | Spec must reference 08 as detailed execution document. |
| TC-V23-03-13 | Confirm M4 is broken down into M4.x execution sequence. | Spec must define M4.1 through M4.6 with exit criteria. |
| TC-V23-03-14 | Confirm M4 detailed execution spec is linked. | Spec must reference 09 as detailed execution document. |
| TC-V23-03-15 | Confirm M5 is broken down into M5.x execution sequence. | Spec must define M5.1 through M5.6 with exit criteria. |
| TC-V23-03-16 | Confirm M5 detailed execution spec is linked. | Spec must reference 10 as detailed execution document. |
| TC-V23-03-17 | Confirm M6 is broken down into M6.x execution sequence. | Spec must define M6.1 through M6.6 with exit criteria. |
| TC-V23-03-18 | Confirm M6 detailed execution spec is linked. | Spec must reference 11 as detailed execution document. |

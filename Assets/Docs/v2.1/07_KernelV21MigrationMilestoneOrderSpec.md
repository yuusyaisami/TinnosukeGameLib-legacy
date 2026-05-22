# Kernel v2.1 Migration Milestone Order Specification

## Document Status

- Document ID: 07_KernelV21MigrationMilestoneOrderSpec
- Status: Draft
- Role: defines the cross-wave milestone order, gate sequencing, and completion-claim rules that turn the v2.1 migration waves into auditable implementation milestones after the post-M15 kernel baseline already exists
- Depends on:
  - [00_KernelV21MigrationOverviewSpec.md](00_KernelV21MigrationOverviewSpec.md)
  - [01_WaveABootAndSceneEntryCutoverSpec.md](01_WaveABootAndSceneEntryCutoverSpec.md)
  - [02_WaveBScopeAndServiceCompositionCutoverSpec.md](02_WaveBScopeAndServiceCompositionCutoverSpec.md)
  - [03_WaveCCommandDispatchCutoverSpec.md](03_WaveCCommandDispatchCutoverSpec.md)
  - [04_WaveDValueBlackboardAndVarCutoverSpec.md](04_WaveDValueBlackboardAndVarCutoverSpec.md)
  - [05_WaveERepresentativeGameplaySystemsCutoverSpec.md](05_WaveERepresentativeGameplaySystemsCutoverSpec.md)
  - [06_WaveFLegacyRemovalAndHardeningSpec.md](06_WaveFLegacyRemovalAndHardeningSpec.md)
  - [../v2/13_LegacyCompatBoundarySpec.md](../v2/13_LegacyCompatBoundarySpec.md)
  - [../v2/15_TestAndValidationSpec.md](../v2/15_TestAndValidationSpec.md)
  - [../v2/16_ImplementationMilestoneOrderSpec.md](../v2/16_ImplementationMilestoneOrderSpec.md)
  - [../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md](../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md)
- Provides foundation for:
  - implementation planning, migration tracking, milestone review, release-readiness review, and acceptance-gate discussions for Kernel v2.1

### Revision Note

This revision creates the milestone-order specification for Kernel v2.1.

The detailed wave specifications already define ownership slices, preserved contracts, target authority, diagnostics, and acceptance criteria.
What was still missing was the claim order that says when those wave results may be treated as materially complete and what must already be true before the next migration claim is credible.

This document therefore does not replace Wave A through Wave F.
It turns them into V21-M0 through V21-M6 claim gates.

---

## Ownership

This specification owns:

- milestone ordering from V21-M0 through V21-M6
- the distinction between a wave ownership slice and a claimable milestone gate
- milestone purpose, required outputs, exit gates, forbidden shortcuts, and downstream unlocks
- cross-wave dependency rules and prohibited sequencing for v2.1 migration claims
- regression rules that return later milestones to at-risk state when an earlier authority cutover regresses
- the rule that gameplay-only success, direct-play-only success, or local subsystem success do not close a milestone by themselves

This specification does not own:

- migration semantics already owned by [00_KernelV21MigrationOverviewSpec.md](00_KernelV21MigrationOverviewSpec.md) or Wave A through Wave F
- target-kernel semantics already owned by the v2 specifications
- final runtime API signatures, class names, or file names for implementation artifacts
- staffing, calendar estimates, task board layout, or pull-request granularity
- any preservation surface beyond the narrow preservation floor already defined by [00_KernelV21MigrationOverviewSpec.md](00_KernelV21MigrationOverviewSpec.md)

07 defines claim order.
It does not redefine what the waves mean.

---

## Purpose

This specification defines how v2.1 migration work is allowed to claim progress.

Core statements:

```text
v2 M0 through M15 define the post-baseline kernel that v2.1 assumes already exists.
Wave specifications define ownership slices.
Milestones define the order in which those slices may be claimed as materially complete.

A milestone closes one class of legacy authority and unlocks the next class of cutover work.
Direct play success, visual gameplay success, or partial subsystem success do not complete a milestone by themselves.
```

Without this layer, the project would still be vulnerable to two bad readings:

- treating a wave inventory or partial cutover as if it already justified downstream claims
- treating a playable scene or direct-play success as if it proved that the accepted live path has migrated

---

## Scope

This specification defines:

- milestone order from V21-M0 through V21-M6
- the post-M15 entry assumption for v2.1 migration work
- the mapping from overview and wave documents to claimable milestones
- milestone-level entry assumptions, required outputs, exit gates, forbidden shortcuts, and downstream unlocks
- overlap rules for implementation work versus completion claims
- non-completion and regression rules that keep later claims falsifiable

This specification does not define:

- a second set of wave semantics
- a substitute for the acceptance criteria already written in Wave A through Wave F
- team planning or calendar sequencing
- a requirement that every wave subphase must become a top-level milestone
- permission to bypass executable gates because a representative path happens to look correct manually

---

## Non-Goals

This specification does not define:

- a new target architecture separate from v2 or v2.1
- a rewrite of the preservation floor
- a promise that milestone work must be delivered one pull request at a time
- a rule that all implementation overlap is forbidden
- a rule that Wave F may reopen earlier wave ownership just because cleanup happens last

07 exists to make migration claims auditable.
It is not a generic project-management handbook.

---

## Relationship to Other Specs

| Spec | Relationship to 07 |
| --- | --- |
| [00_KernelV21MigrationOverviewSpec.md](00_KernelV21MigrationOverviewSpec.md) | Owns the preservation floor, destructive allowance, migration baseline, and global non-completion rules that every milestone claim must preserve. |
| [01_WaveABootAndSceneEntryCutoverSpec.md](01_WaveABootAndSceneEntryCutoverSpec.md) | Supplies the first authority cutover milestone for live boot, scene entry, and loading ownership. |
| [02_WaveBScopeAndServiceCompositionCutoverSpec.md](02_WaveBScopeAndServiceCompositionCutoverSpec.md) | Supplies the composition-authority milestone that must close before command or value claims are credible. |
| [03_WaveCCommandDispatchCutoverSpec.md](03_WaveCCommandDispatchCutoverSpec.md) | Supplies the command-authority milestone that must close before representative gameplay proof can claim migrated dispatch. |
| [04_WaveDValueBlackboardAndVarCutoverSpec.md](04_WaveDValueBlackboardAndVarCutoverSpec.md) | Supplies the generic value-authority milestone that must close before representative gameplay proof can claim migrated value truth. |
| [05_WaveERepresentativeGameplaySystemsCutoverSpec.md](05_WaveERepresentativeGameplaySystemsCutoverSpec.md) | Supplies the representative gameplay proof milestone that demonstrates migrated authority is actually consumed by the live game. |
| [06_WaveFLegacyRemovalAndHardeningSpec.md](06_WaveFLegacyRemovalAndHardeningSpec.md) | Supplies the final residue-hardening milestone that turns tolerated migration residue into deleted or bounded quarantine-only remainder. |
| [../v2/13_LegacyCompatBoundarySpec.md](../v2/13_LegacyCompatBoundarySpec.md) | Owns the quarantine model used when a milestone cannot yet delete legacy residue but must still bound and diagnose it. |
| [../v2/15_TestAndValidationSpec.md](../v2/15_TestAndValidationSpec.md) | Owns the executable proof model that milestone exit gates must consume. |
| [../v2/16_ImplementationMilestoneOrderSpec.md](../v2/16_ImplementationMilestoneOrderSpec.md) | Defines the post-M15 baseline that must already exist before any v2.1 milestone claim is meaningful. |
| [../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md](../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md) | Supplies compile-boundary rules that Wave F must turn into final v2.1 migration-closeout evidence. |

07 is downstream of both the overview and the detailed wave specs.
It is not a replacement for them.

---

## Entry Assumptions

Every v2.1 milestone assumes all of the following are already true:

- the project is operating from the post-M15 baseline defined by [../v2/16_ImplementationMilestoneOrderSpec.md](../v2/16_ImplementationMilestoneOrderSpec.md)
- verified boot, scope, command, value, diagnostics, authoring-bridge, legacy-compat, and executable-test foundations already exist as target-kernel capabilities
- v2.1 work is moving the live game onto that capability set rather than rebuilding those semantics inside legacy architecture
- direct play may exist as a reference path, but it is not a substitute for accepted live-path migration proof

If these assumptions are not true, the correct action is to close the missing v2 baseline gap first rather than claim v2.1 milestone progress.

---

## Milestone Design Principles

### 1. Claimable Gates, Not Activity Logs

Milestones are not progress diaries.
They are claim gates that determine whether a later migration statement is credible.

### 2. Authority Before Representative Gameplay Proof

Live entry, composition, command authority, and generic value authority must close before representative gameplay proof is allowed to claim migrated architecture.

### 3. Inventories Are Evidence, Not Optional Preface

The current-state inventory, preserved-contract, diagnostics, and acceptance sections in Wave A through Wave F are not decorative.
They are the minimum evidence that allows a milestone claim to be audited.

### 4. Hardening Is Final, Not Optional

Representative gameplay proof is necessary, but migration completion is not claimable until residue is deleted or bounded and executable hardening makes regressions fail closed.

---

## Global Milestone Order

| Milestone | Name | Primary Goal | Exit Signal |
| --- | --- | --- | --- |
| V21-M0 | Migration Baseline Freeze | freeze the migration claim surface before cutover claims begin | preserved surfaces, representative anchors, residue domains, and proof families are explicit and no longer need rediscovery |
| V21-M1 | Live Boot and Scene Entry Authority | close legacy live-entry authority | the live game enters and transitions scenes through verified boot and scene-entry authority |
| V21-M2 | Scope and Service Authority | close legacy composition authority | representative scope and coarse-service truth come from verified ScopeGraph and ServiceGraph boundaries |
| V21-M3 | Command Authority | close legacy command identity and dispatch authority | representative command execution enters through verified command authority rather than bulk registration or key fallback |
| V21-M4 | Value Authority | close legacy generic value and DynamicValue runtime authority | representative value access and init use verified value identity, store, and evaluation authority |
| V21-M5 | Representative Gameplay Proof | prove that real live gameplay consumes migrated authority | representative GameScene gameplay runs through the migrated authority stack with traceable diagnostics evidence |
| V21-M6 | Legacy Removal and Hardening | turn migration residue into deleted or bounded debt and make completion auditable | accepted paths no longer depend on migration-only residue and release-facing hardening gates fail closed |

Required order:

```text
V21-M0 -> V21-M1 -> V21-M2 -> V21-M3 -> V21-M4 -> V21-M5 -> V21-M6
```

Implementation overlap is allowed only when it does not turn a later milestone into an accepted path before every earlier exit gate it depends on is complete.

---

## Wave-to-Milestone Mapping

| Source spec or section family | Primary milestone use | Why |
| --- | --- | --- |
| [00_KernelV21MigrationOverviewSpec.md](00_KernelV21MigrationOverviewSpec.md) preservation floor, destructive allowance, migration baseline, governance, and non-completion rules | V21-M0 entry evidence and V21-M1 through V21-M6 claim guardrail | every later milestone inherits the same preservation floor and may not widen it silently |
| [01_WaveABootAndSceneEntryCutoverSpec.md](01_WaveABootAndSceneEntryCutoverSpec.md) | V21-M1 | closes live boot, scene entry, loading ownership, and scene-root demotion |
| [02_WaveBScopeAndServiceCompositionCutoverSpec.md](02_WaveBScopeAndServiceCompositionCutoverSpec.md) | V21-M2 | closes installer-driven composition truth and establishes verified scope and coarse-service authority |
| [03_WaveCCommandDispatchCutoverSpec.md](03_WaveCCommandDispatchCutoverSpec.md) | V21-M3 | closes bulk executor discovery, catalog-loading convenience, and key-fallback command truth |
| [04_WaveDValueBlackboardAndVarCutoverSpec.md](04_WaveDValueBlackboardAndVarCutoverSpec.md) | V21-M4 | closes blackboard fallback, runtime value lookup convenience, and hidden DynamicValue runtime truth |
| [05_WaveERepresentativeGameplaySystemsCutoverSpec.md](05_WaveERepresentativeGameplaySystemsCutoverSpec.md) | V21-M5 | proves that real GameScene gameplay consumes the migrated A-D authority stack |
| [06_WaveFLegacyRemovalAndHardeningSpec.md](06_WaveFLegacyRemovalAndHardeningSpec.md) | V21-M6 | closes migration by deleting or bounding residue and binding completion to hardening gates |

Wave-local current-state inventories and preserved-contract tables are entry evidence for their corresponding milestone.
Wave-local diagnostics, acceptance, and non-completion rules define the minimum exit signal for the corresponding milestone.

---

## Milestone Claim Rules

Each milestone has five required properties:

1. Entry assumptions
2. Required outputs
3. Exit gates
4. Forbidden shortcuts
5. Downstream unlocks

A milestone is not complete when code merely exists.
It is complete only when the next milestone can consume its results without accepted-path fallback to an earlier legacy authority.

If an earlier milestone regresses, every later milestone depending on that gate returns to at-risk state until the regression is closed.

---

## Overlap and Prohibited Sequencing

The following rules are normative:

- inventories, representative-anchor review, and split analysis for later waves may begin early, but milestone completion claims must follow the required order above
- V21-M2 may not close while the accepted live path still needs legacy auto-bootstrap, scene-root authority, or loading discovery repair from Wave A
- V21-M3 may not close while accepted command outcomes still depend on unresolved installer, resolver, or scope-authority residue from Wave B
- V21-M4 may not close while accepted value truth still depends on unresolved command-identity, command-runner, or composition fallback paths
- V21-M5 may not close while representative GameScene gameplay still succeeds only because compatibility traversal, local blackboard merge, helper-driven scope repair, or hidden fallback truth remains required
- V21-M6 may delete or quarantine residue, but it may not silently reinterpret earlier wave semantics, widen the preservation floor, or reclassify accepted-path residue as harmless legacy history

---

## Milestone Definitions

### V21-M0: Migration Baseline Freeze

Entry assumptions:

- the post-M15 v2 baseline is accepted
- the overview and all six wave documents are treated as the source of truth for migration ownership

Required outputs:

- one traceable baseline at [Index/KernelV21BaselineLedger.md](Index/KernelV21BaselineLedger.md) that joins the current-state inventories recorded in Wave A through Wave F
- one preserved-surface ledger at [Index/KernelV21PreservationFloorLedger.md](Index/KernelV21PreservationFloorLedger.md) that keeps the preservation floor narrow and consistent with [00_KernelV21MigrationOverviewSpec.md](00_KernelV21MigrationOverviewSpec.md)
- one representative proof-anchor set at [Index/KernelV21ProofAnchorCatalog.md](Index/KernelV21ProofAnchorCatalog.md) covering live entry, representative gameplay, and residue review
- one wave-to-milestone mapping and milestone-claim order for later review, exposed through this spec and [Index/README.md](Index/README.md)

Canonical V21-M0 artifact set:

- [Index/README.md](Index/README.md)
- [Index/KernelV21BaselineLedger.md](Index/KernelV21BaselineLedger.md)
- [Index/KernelV21PreservationFloorLedger.md](Index/KernelV21PreservationFloorLedger.md)
- [Index/KernelV21ProofAnchorCatalog.md](Index/KernelV21ProofAnchorCatalog.md)
- this specification

Exit gates:

- later milestone claims no longer need to reopen discovery about preserved surfaces, representative anchors, residue domains, or proof families
- direct play, live boot, representative gameplay, and residue hardening are already separated as distinct proof families
- no preserved surface beyond Command field shape, DynamicValue authoring surface, and ValueStore generated key identity has been silently added

Forbidden shortcuts:

- treating wave inventories as optional narrative instead of claim evidence
- widening the preservation floor to avoid difficult cutovers
- using direct-play-only or gameplay-only success as the baseline proof model

Downstream unlocks:

- V21-M1 through V21-M6 claim review

### V21-M1: Live Boot and Scene Entry Authority

Entry assumptions:

- V21-M0 is complete

Required outputs:

- the Wave A boot-authority isolation, live scene-entry handoff, scene-transition contract cutover, loading-orchestration cutover, and legacy scene-root demotion outputs
- explicit mixed-authority diagnostics for legacy auto-bootstrap and legacy loading repair
- representative startup and scene-transition proof for the accepted live path

Exit gates:

- all Wave A acceptance criteria are satisfied
- the Wave A diagnostic failure classes have a fail-closed story

Forbidden shortcuts:

- treating direct play as if it already proves the live path migrated
- keeping legacy auto-bootstrap, scene-root search, duplicate cleanup, or fallback loading discovery as silent accepted truth

Downstream unlocks:

- V21-M2 composition cutover on top of verified live entry
- V21-M5 representative gameplay proof on top of a credible live-entry baseline

### V21-M2: Scope and Service Authority

Entry assumptions:

- V21-M1 is complete

Required outputs:

- the Wave B installer and resolver quarantine outputs
- representative service-eligibility and split decisions for mixed-boundary anchors
- persistent, scene, authored, and runtime pooled scope authority cutover with generation-safe handle rules
- explicit scope-local ServiceGraph boundaries and mixed-authority diagnostics

Exit gates:

- all Wave B acceptance criteria are satisfied
- stale-handle rejection, scope-parent truth, and coarse-service authority are executable and falsifiable rather than inferred

Forbidden shortcuts:

- accepting `Transform.parent`, nearest-scope search, installer discovery, or collection resolve as hidden steady-state truth
- treating command bootstrap or blackboard semantics as if they were already migrated merely because composition was split

Downstream unlocks:

- V21-M3 command-authority cutover on top of verified composition boundaries
- V21-M4 value-authority cutover on top of verified scope boundaries
- V21-M5 representative gameplay proof that no longer rests on composition ambiguity

### V21-M3: Command Authority

Entry assumptions:

- V21-M2 is complete

Required outputs:

- the Wave C command-identity boundary, catalog and key-fallback quarantine, executor-table cutover, runner-domain policy, and preserved-surface validation outputs
- explicit rejection policy for missing normalization, missing executor mapping, and missing payload schema
- mixed-authority diagnostics for bulk registration, lifecycle-time catalog loading, and runtime key repair

Exit gates:

- all Wave C acceptance criteria are satisfied
- representative preserved command surfaces remain functional through migrated command authority rather than legacy dispatch truth

Forbidden shortcuts:

- using stable keys, authoring keys, runtime-only negative keys, or bulk `ICommandExecutor` registration as accepted runtime truth
- treating `Resources.Load("CommandCatalog")` or lifecycle-time catalog loading as a harmless compatibility detail in the accepted path

Downstream unlocks:

- V21-M4 value-authority cutover with a stable migrated command boundary
- V21-M5 representative gameplay proof that can attribute command execution to migrated authority
- V21-M6 residue cleanup for command adapters and bulk-registration hosts

### V21-M4: Value Authority

Entry assumptions:

- V21-M3 is complete

Required outputs:

- the Wave D value-identity boundary, ValueStore authority, blackboard demotion, explicit init ownership, grid normalization, and DynamicValue evaluation-boundary outputs
- explicit runtime rejection for stable-key lookup, registry lookup, root-creation repair, and hidden dynamic evaluation truth
- explicit handoff boundary that keeps scalar specialization outside Wave D main ownership

Exit gates:

- all Wave D acceptance criteria are satisfied
- representative runtime consumers can keep working without accepted runtime value truth depending on Blackboard fallback or runtime identity repair

Forbidden shortcuts:

- treating generated-id continuity as permission to keep runtime stable-key or registry lookup
- keeping blackboard hierarchical fallback, root creation, arbitrary grid payload truth, or hidden DynamicValue evaluation as accepted architecture
- silently absorbing scalar semantics into generic value migration

Downstream unlocks:

- V21-M5 representative gameplay proof with credible migrated value authority
- V21-M6 residue cleanup for blackboard, var, and runtime lookup conveniences

### V21-M5: Representative Gameplay Proof

Entry assumptions:

- V21-M1 through V21-M4 are complete

Required outputs:

- the Wave E representative GameScene anchor proof
- migrated GameStateMachine, Conversation and Dialogue, GridObject and Trait presentation, and StatusEffect consumption proof
- explicit subordinate-dependency handling for TargetChannel and AreaChannel
- traceable diagnostics evidence that distinguishes migrated authority from visual success only

Exit gates:

- all Wave E acceptance criteria are satisfied
- representative gameplay proof runs through the real GameScene anchors and can be attributed to migrated authority rather than compatibility helpers

Forbidden shortcuts:

- accepting gameplay because the scene still looks correct without proving the migrated authority path
- treating direct-play-only harnesses, screenshots, manual feel, or isolated tooling as sufficient representative proof
- silently redefining scalar semantics or promoting subordinate targeting dependencies into the main ownership target

Downstream unlocks:

- V21-M6 final residue cleanup and migration-closeout hardening

### V21-M6: Legacy Removal and Hardening

Entry assumptions:

- V21-M5 is complete

Required outputs:

- the Wave F residue inventory, residue-domain demotion rules, compile-boundary quarantine rules, executable hardening gates, and legacy-removal evidence
- explicit classification of any remaining adapters as diagnosable, profile-bounded, removable, and non-authoritative
- release-facing failure gates for runtime-capable legacy residue and dependency inversion

Exit gates:

- all Wave F acceptance criteria are satisfied
- migration completion is auditable through executable gates and residue evidence rather than by gameplay appearance alone

Forbidden shortcuts:

- leaving runtime-capable legacy adapters active in accepted Release behavior
- keeping compile-boundary leakage, helper-traversal residue, or legacy lookup convenience invisible to diagnostics and gate review
- using Wave F as an excuse to reopen earlier wave semantics instead of deleting or bounding residue

Downstream unlocks:

- credible v2.1 migration-completion claims

---

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V21-MO-01 | Confirm 07 explicitly assumes the post-M15 baseline rather than redefining v2 implementation order. | Entry Assumptions and Purpose must say that v2.1 begins after the v2 baseline exists. |
| TC-V21-MO-02 | Confirm the milestone order is enumerated from V21-M0 through V21-M6. | Global Milestone Order must list all seven milestones and the normative order string. |
| TC-V21-MO-03 | Confirm overview and Wave A through Wave F are all mapped into milestone usage. | Wave-to-Milestone Mapping must reference the overview plus all six waves. |
| TC-V21-MO-04 | Confirm live-entry authority closes before composition, command, value, gameplay, and hardening claims. | V21-M1 must unlock later milestones rather than appear optional. |
| TC-V21-MO-05 | Confirm command and value milestones cannot close on top of unresolved composition truth. | Overlap and Prohibited Sequencing must block V21-M3 and V21-M4 from bypassing V21-M2. |
| TC-V21-MO-06 | Confirm representative gameplay proof is later than the A-D authority cutovers. | V21-M5 must require V21-M1 through V21-M4 as completed entry assumptions. |
| TC-V21-MO-07 | Confirm hardening and residue cleanup are the final migration-closeout milestone. | V21-M6 must consume Wave F outputs and unlock the migration-completion claim. |
| TC-V21-MO-08 | Confirm gameplay-only or direct-play-only success cannot close milestones. | Purpose, Milestone Claim Rules, and milestone definitions must all reject those proofs. |
| TC-V21-MO-09 | Confirm the preservation floor stays narrow while milestones are defined. | V21-M0 must explicitly keep the preservation floor limited to the overview contract. |
| TC-V21-MO-10 | Confirm V21-M0 names the canonical artifact set that later milestones consume. | The V21-M0 definition must name Index/README.md plus the baseline, preservation, and proof-anchor ledgers. |

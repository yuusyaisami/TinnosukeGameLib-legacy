# Kernel v2.2 Milestone Order Specification

## Document Status

- Document ID: 05_KernelV22MilestoneOrderSpec
- Status: Draft
- Role: defines the claim order for v2.2 completion work so kernel-only host cutover, family migration, deletion hardening, and final proof aggregation remain auditable
- Depends on:
  - [00_KernelV22CompletionOverviewSpec.md](00_KernelV22CompletionOverviewSpec.md)
  - [01_KernelV22AuthorityAndServiceCensusSpec.md](01_KernelV22AuthorityAndServiceCensusSpec.md)
  - [02_KernelV22KernelOnlyHostSpec.md](02_KernelV22KernelOnlyHostSpec.md)
  - [02_1_KernelV22CommandAndValueHostRemovalSpec.md](02_1_KernelV22CommandAndValueHostRemovalSpec.md)
  - [03_KernelV22ServiceFamilyCutoverSpec.md](03_KernelV22ServiceFamilyCutoverSpec.md)
  - [03_1_KernelV22RepresentativeGameplayAndApplicationCutoverSpec.md](03_1_KernelV22RepresentativeGameplayAndApplicationCutoverSpec.md)
  - [04_KernelV22LegacyDeletionAndHardeningSpec.md](04_KernelV22LegacyDeletionAndHardeningSpec.md)
  - [04_1_KernelV22FullProofAndReleaseHardeningSpec.md](04_1_KernelV22FullProofAndReleaseHardeningSpec.md)

## Purpose

This document turns the v2.2 implementation program into claimable milestones.

Operational implementation slicing and validation order are delegated to [06_KernelV22ImplementationPlan.md](06_KernelV22ImplementationPlan.md).

## Global Milestone Order

| Milestone | Name | Primary goal | Exit signal |
| --- | --- | --- | --- |
| M0 | Charter and Completion Package | fix the v2.2 claim surface before runtime cutover expands | authority and service census, completion-package entry index, baseline ledger, proof catalog, and claim-order contract are testable and mutually consistent |
| M1 | Kernel-Only Live Host | replace legacy live host authority | kernel-only live host chain exists, legacy roots and pre-shell scene roots are demoted, and live/direct paths converge on the same verified-input host truth |
| M2 | Command and Value Host Removal | remove the legacy scene-facing command/value authorities | accepted command/value execution no longer depends on CommandRunnerMB, BlackboardMB, or host-repair fallback that those scene-facing hosts used to hide |
| M3 | Service Family Cutover A | cut over boot/scene flow and first channel families | Boot and Scene Flow plus first channel families consume kernel-owned host, command, and value authority without split-required drift |
| M4 | Service Family Cutover B | cut over representative gameplay/application families | representative gameplay/application services consume kernel-owned host, command, value, and family boundaries through the real GameScene anchors with traceable diagnostics proof |
| M5 | Compile Boundary and Legacy Deletion | close runtime deletion and dependency direction | core delete targets are absent from accepted release execution, remaining residue is bounded quarantine only, and dependency direction is enforced |
| M6 | Full Proof and Release Hardening | prove completion under executable gates | required gate classes from SpecShape through IntegrationSmoke form one reviewable bundle that keeps live boot, representative gameplay, diagnostics, hardening, compile-boundary, static, legacy, and performance evidence green together |

Required order:

```text
M0 -> M1 -> M2 -> M3 -> M4 -> M5 -> M6
```

## M0 Definition

### M0: Charter and Completion Package

Entry assumptions:

- v2 remains the only owner of target semantics and compile or runtime rules
- v2.1 baseline debt and proof-family artifacts are accepted as input evidence rather than completion proof
- no later milestone may claim completion while continuity surfaces, delete targets, family buckets, or proof roles still need rediscovery

Required outputs:

- one canonical authority and service census at [01_KernelV22AuthorityAndServiceCensusSpec.md](01_KernelV22AuthorityAndServiceCensusSpec.md) that classifies current runtime owners and family anchors with a stable five-class vocabulary
- one completion-package entry index at [Index/README.md](Index/README.md) that exposes the v2.2 M0 artifacts and the authoritative upstream references they depend on
- one joined completion baseline at [Index/KernelV22BaselineLedger.md](Index/KernelV22BaselineLedger.md) that freezes the debts blocking kernel-only release authority
- one proof-anchor catalog at [Index/KernelV22ProofAnchorCatalog.md](Index/KernelV22ProofAnchorCatalog.md) that separates spec-package proof, live kernel-only host proof, command/value host-removal proof, service-family cutover proof, and release-hardening proof
- one milestone-order and claim-review contract exposed by this specification

Canonical V22-M0 artifact set:

- [01_KernelV22AuthorityAndServiceCensusSpec.md](01_KernelV22AuthorityAndServiceCensusSpec.md)
- [Index/README.md](Index/README.md)
- [Index/KernelV22BaselineLedger.md](Index/KernelV22BaselineLedger.md)
- [Index/KernelV22ProofAnchorCatalog.md](Index/KernelV22ProofAnchorCatalog.md)
- this specification

Exit gates:

- later milestone claims no longer need to reopen discovery about continuity surfaces, delete targets, family buckets, or proof-family roles
- the five-class service-census vocabulary is fixed and used consistently across the M0 artifacts
- direct-play reference, command/value host-removal proof, service-family cutover proof, and release-hardening proof are separated clearly enough that later reviews cannot treat them as interchangeable
- no preserved surface beyond existing command payload meaning, existing DynamicValue authoring surface, and generated value-key identity continuity has been silently added

Forbidden shortcuts:

- treating the authority and service census as optional inventory instead of claim evidence
- widening the continuity contract or shrinking the delete-target set to avoid hard cutovers
- treating direct-play-only or gameplay-only success as the M0 proof model
- promoting authoring-only MonoBehaviours or hub-owned runtime objects into coarse services just to simplify planning

Downstream unlocks:

- M1 kernel-only live host claim review
- M2 command and value host removal claim review
- M3 and M4 service-family cutover planning on a fixed census

### M1: Kernel-Only Live Host

Entry assumptions:

- M0 is complete
- continuity surfaces, delete targets, family buckets, and proof roles are already fixed by the M0 package
- boot and scene-flow family migration may still be incomplete, but accepted live host truth must no longer depend on that ambiguity

Required outputs:

- the 02 live boot authority isolation, persistent-root demotion, runtime shell to scene handoff, direct-play convergence, and mixed-authority rejection outputs
- explicit fail-closed diagnostics for legacy auto-bootstrap, root repair, pre-shell scene-root takeover, and live/direct host divergence
- representative startup proof for the accepted live path

Exit gates:

- all 02 acceptance criteria are satisfied
- M1 failure classes have a fail-closed story and live/direct paths no longer represent different host truths

Forbidden shortcuts:

- treating direct play as if it already proves the accepted live host path migrated
- keeping ProjectLifetimeScope, GlobalLifetimeScope, SceneLifetimeScope, CommandRunnerMB, or BlackboardMB as silent accepted host authority
- claiming M1 by migrating SceneService or LoadingScreenService behavior while host ownership itself remains ambiguous

Downstream unlocks:

- M2 command and value host removal on top of a kernel-owned host chain
- M3 boot and scene-flow family cutover on top of credible host authority
- M4 representative gameplay/application cutover on top of non-ambiguous startup truth

### M2: Command and Value Host Removal

Entry assumptions:

- M1 is complete
- the kernel-owned live host chain already exists and is treated as the only accepted startup truth
- feature-family migration may still be incomplete, but accepted command/value execution must no longer depend on scene-facing hosts

Required outputs:

- the 02_1 command host demotion, value host demotion, declaration-only split, and mixed-authority rejection outputs
- explicit fail-closed diagnostics for missing verified command/value session, CommandRunnerMB or BlackboardMB participation, and fallback repair still required for accepted execution
- representative accepted command/value execution proof on top of the M1 host chain

Exit gates:

- all 02_1 acceptance criteria are satisfied
- M2 failure classes have a fail-closed story and representative accepted command/value execution no longer depends on scene-facing hosts

Forbidden shortcuts:

- keeping CommandRunnerMB or BlackboardMB as silent accepted runtime hosts
- treating declaration-only authoring surfaces as if they still owned runtime execution
- claiming M2 by migrating family behavior while command/value sessions are still created ad hoc by scene-facing hosts
- claiming M2 only by deleting scene-facing hosts without establishing kernel-owned command/value sessions

Downstream unlocks:

- M3 boot and scene-flow family cutover on top of a kernel-owned host and command/value boundary
- M4 representative gameplay/application cutover on top of non-ambiguous command/value authority
- M5 residue deletion and release hardening on top of explicit command/value host demotion

### M3: Service Family Cutover A

Entry assumptions:

- M2 is complete
- the family buckets, split-required anchors, and positive templates are already fixed by 03 and may not be rediscovered during claim review
- gameplay/application migration may still be incomplete, but Boot and Scene Flow plus first channel families must already consume the M1 and M2 boundaries

Required outputs:

- the 03 Boot and Scene Flow family cutover, first channel family cutover, split-required family enforcement, and acceptance-gate outputs
- explicit fail-closed diagnostics for mixed family authority, hidden host or command or value authority recreation, unsplit family claims, and gameplay/application proof being borrowed to close M3
- representative family proof for Boot and Scene Flow plus first channel families

Exit gates:

- all 03 M3 acceptance criteria are satisfied
- Boot and Scene Flow no longer depends on scene-facing installer truth or discovery repair for accepted family authority
- first channel families consume kernel-owned host, command, and value authority without recreating them locally
- gameplay/application proof remains deferred to M4 and is not used to close M3

Forbidden shortcuts:

- claiming M3 through GameScene-visible gameplay/application success alone
- keeping `SceneFlowInstallerMB`, `LoadingScreenService`, `TooltipChannelHubService`, or `AnimationSpriteHubService` as silent accepted family authority while calling the family migrated
- treating unsplit mixed-boundary families as stable ServiceGraph families
- re-expanding `ModalStackChannelHubService` or `MeshChannelHubService` into per-runtime authority fragments just to match legacy structure

Downstream unlocks:

- M4 representative gameplay/application cutover on top of fixed family boundaries
- M5 deletion and hardening on top of family boundaries that no longer depend on scene-facing repair

### M4: Service Family Cutover B

Entry assumptions:

- M3 is complete
- representative gameplay/application families must consume the already-fixed host, command, value, and family boundaries rather than reopening them
- accepted proof must run through the real GameScene anchors rather than direct-play-only or editor-only harnesses

Required outputs:

- the 03_1 representative bundle inventory, GameStateMachine slice, Conversation and Dialogue slice, Grid and Trait presentation slice, StatusEffect slice and scalar boundary, and mixed-authority acceptance outputs
- explicit fail-closed diagnostics for compatibility traversal, local blackboard merge, helper-driven scope repair, subordinate dependency authority restoration, scalar-boundary drift, and gameplay/application appearance with no traceable proof
- representative GameScene gameplay/application proof with traceable diagnostics evidence

Exit gates:

- all 03_1 acceptance criteria are satisfied
- representative gameplay/application behavior consumes the M1 through M3 boundaries instead of legacy convenience paths deciding accepted outcomes
- proof runs through the real GameScene anchors and includes traceable diagnostics evidence rather than gameplay/application appearance alone

Forbidden shortcuts:

- claiming M4 through visible playability alone
- treating direct play or editor-only setup as if it already proves representative GameScene migration
- keeping compatibility traversal, local blackboard merge, helper-driven scope repair, or hidden value fallback as silent accepted truth for representative slices
- promoting `TargetChannelHubService` or `AreaChannelHubService` into the main ownership target of M4
- silently redefining scalar semantics inside representative gameplay/application slices

Downstream unlocks:

- M5 deletion and hardening on top of representative gameplay/application proof that no longer depends on mixed authority
- M6 full proof and release hardening on top of both startup and representative gameplay/application credibility

### M5: Compile Boundary and Legacy Deletion

Entry assumptions:

- M4 is complete
- representative gameplay/application proof already exists, so remaining legacy residue can no longer be excused as required for accepted behavior
- compile-boundary work must be evaluated relative to the repo's current partial asmdef split rather than a monolith assumption

Required outputs:

- the 04 residue inventory and preservation-floor outputs, residue-domain cleanup rules, compile-boundary quarantine rules, and hardening-gate evidence outputs
- explicit fail-closed diagnostics for legacy live-host residue, scope or resolver residue, command residue, value or blackboard residue, gameplay-helper residue, compile-boundary inversion, adapter metadata gaps, and missing hardening evidence
- auditable deletion or quarantine evidence for release-blocking residue and dependency direction

Exit gates:

- all 04 acceptance criteria are satisfied
- accepted release execution no longer depends on runtime-capable legacy residue deciding required outcomes
- remaining adapters, if any, are explicit, diagnosable, profile-bounded, removable, and non-authoritative
- compile boundaries are auditable and enforceable against kernel-to-legacy and production-to-test inversion

Forbidden shortcuts:

- claiming M5 through representative gameplay/application success alone
- treating code existence by itself as failure while ignoring whether accepted release execution still depends on that code
- leaving VContainer-backed legacy runtime hosts, hierarchy discovery, runtime `Resources.Load`, or helper traversal as silent accepted release repair
- describing compile-boundary completion as if the repo were still an undifferentiated monolith

Downstream unlocks:

- M6 full proof and release hardening on top of explicit deletion or quarantine state

### M6: Full Proof and Release Hardening

Entry assumptions:

- M5 is complete
- the earlier proof families are already individually fixed, separated, and reviewable; M6 aggregates them rather than reopening their ownership
- release completion must be decided by executable gate bundles and reviewable failure output rather than by doc shape, compile success, or an ad hoc smoke pass

Required outputs:

- the 04_1 proof-family intake, required gate-bundle mapping, validation and generation intake, live boot runtime bundle, representative gameplay/application runtime bundle, diagnostics and failure-report bundle, performance or static or legacy or compile-boundary bundle, and final aggregation outputs
- explicit fail-closed diagnostics for missing gate class, skipped lower layer, direct-play-as-proof, gameplay-only-as-proof, non-traceable runtime success, wall-clock-only performance claims, and non-reviewable release reporting
- one reviewable release-claim bundle that identifies which gate class and anchor proves each required completion domain

Exit gates:

- all 04_1 acceptance criteria are satisfied
- required gate classes from SpecShape through IntegrationSmoke are present and reviewable in the release claim bundle
- accepted live boot, representative gameplay/application, deletion or bounded quarantine, compile-boundary, static-rule, diagnostics, and performance evidence remain green together
- a failing lower layer invalidates the M6 claim instead of being hidden by later smoke or report aggregation

Forbidden shortcuts:

- claiming M6 through compile success, doc shape, or a single smoke run
- treating direct play as accepted live proof or representative gameplay visibility as final proof
- treating M5 hardening proof as if it already aggregated M6
- using wall-clock-only performance claims or message-only logging as release evidence
- silently skipping required gate classes because the current environment is inconvenient

Downstream unlocks:

- v2.2 release completion claim review
- release-ready kernel-only runtime authority sign-off

## Claim Rules

1. A later milestone may overlap in implementation, but it may not claim completion before every earlier exit signal it depends on is true.
2. Direct-play success does not close M1 or M6.
3. Representative gameplay success without deletion and hardening does not close M6.
4. M0 is not complete while [01_KernelV22AuthorityAndServiceCensusSpec.md](01_KernelV22AuthorityAndServiceCensusSpec.md), [Index/README.md](Index/README.md), [Index/KernelV22BaselineLedger.md](Index/KernelV22BaselineLedger.md), [Index/KernelV22ProofAnchorCatalog.md](Index/KernelV22ProofAnchorCatalog.md), and this specification still disagree about continuity, delete targets, or proof roles.
5. M1 is not complete while [02_KernelV22KernelOnlyHostSpec.md](02_KernelV22KernelOnlyHostSpec.md), [Index/KernelV22ProofAnchorCatalog.md](Index/KernelV22ProofAnchorCatalog.md), and this specification still disagree about what counts as live-host proof versus later family-cutover proof.
6. M2 is not complete while [02_1_KernelV22CommandAndValueHostRemovalSpec.md](02_1_KernelV22CommandAndValueHostRemovalSpec.md), [Index/KernelV22ProofAnchorCatalog.md](Index/KernelV22ProofAnchorCatalog.md), and this specification still disagree about what counts as command/value host-removal proof versus later family-cutover or release-hardening proof.
7. M3 is not complete while [03_KernelV22ServiceFamilyCutoverSpec.md](03_KernelV22ServiceFamilyCutoverSpec.md), [Index/KernelV22ProofAnchorCatalog.md](Index/KernelV22ProofAnchorCatalog.md), and this specification still disagree about what counts as Boot and Scene Flow plus first-channel family proof versus later gameplay/application proof.
8. M4 is not complete while [03_1_KernelV22RepresentativeGameplayAndApplicationCutoverSpec.md](03_1_KernelV22RepresentativeGameplayAndApplicationCutoverSpec.md), [Index/KernelV22ProofAnchorCatalog.md](Index/KernelV22ProofAnchorCatalog.md), and this specification still disagree about what counts as representative gameplay/application proof versus later release-hardening proof.
9. M5 is not complete while [04_KernelV22LegacyDeletionAndHardeningSpec.md](04_KernelV22LegacyDeletionAndHardeningSpec.md), [Index/KernelV22ProofAnchorCatalog.md](Index/KernelV22ProofAnchorCatalog.md), and this specification still disagree about what counts as deletion or compile-boundary proof versus later M6 full-proof aggregation.
10. M6 is not complete while [04_1_KernelV22FullProofAndReleaseHardeningSpec.md](04_1_KernelV22FullProofAndReleaseHardeningSpec.md), [Index/KernelV22ProofAnchorCatalog.md](Index/KernelV22ProofAnchorCatalog.md), and this specification still disagree about the required gate bundle, proof-family intake, or what counts as final release-claim aggregation.

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V22-MO-01 | Confirm the v2.2 milestone chain starts from a completion package. | M0 must mention the completion package. |
| TC-V22-MO-02 | Confirm kernel-only live host is the first runtime cutover gate. | M1 must be Kernel-Only Live Host. |
| TC-V22-MO-03 | Confirm command/value host removal is separated from family cutover. | M2 must mention CommandRunnerMB and BlackboardMB replacement pressure implicitly through its goal. |
| TC-V22-MO-04 | Confirm family cutover is split across two milestones. | M3 and M4 must both be present. |
| TC-V22-MO-05 | Confirm compile-boundary plus legacy deletion precedes final proof. | M5 must come before M6. |
| TC-V22-MO-06 | Confirm M0 names the canonical artifact set explicitly. | M0 must identify the authority and service census, index package, baseline ledger, proof catalog, and this specification as the canonical M0 artifacts. |
| TC-V22-MO-07 | Confirm M0 forbids silent continuity widening and proof conflation. | M0 must forbid widening the continuity contract and treating direct-play or gameplay-only success as interchangeable proof. |
| TC-V22-MO-08 | Confirm M1 defines a dedicated host-authority gate rather than collapsing into family cutover. | M1 must require the 02 host outputs and must forbid claiming completion through SceneService or LoadingScreenService family migration alone. |
| TC-V22-MO-09 | Confirm M1 requires live/direct host convergence. | M1 must require that live boot and direct play no longer represent different host truths. |
| TC-V22-MO-10 | Confirm M2 defines a dedicated command/value host-removal gate. | M2 must require the 02_1 outputs and must forbid claiming completion through family migration alone. |
| TC-V22-MO-11 | Confirm M2 requires kernel-owned command/value sessions rather than scene-facing host repair. | M2 must require command/value execution to stop depending on CommandRunnerMB, BlackboardMB, or fallback repair. |
| TC-V22-MO-12 | Confirm M3 defines a dedicated Boot and Scene Flow plus first-channel family gate. | M3 must require the 03 family outputs and must forbid claiming completion through gameplay/application success alone. |
| TC-V22-MO-13 | Confirm M3 requires split enforcement and family-proof separation. | M3 must require split-required families to stay unclaimable until split and must keep gameplay/application proof deferred to M4. |
| TC-V22-MO-14 | Confirm M4 defines a dedicated representative gameplay/application gate. | M4 must require the 03_1 outputs and must forbid claiming completion through visible playability alone. |
| TC-V22-MO-15 | Confirm M4 requires real GameScene anchors and traceable diagnostics evidence. | M4 must require proof through the representative GameScene anchors and must forbid gameplay/application appearance alone as sufficient proof. |
| TC-V22-MO-16 | Confirm M5 defines a dedicated deletion and compile-boundary gate. | M5 must require the 04 outputs and must forbid claiming completion through representative gameplay/application success alone. |
| TC-V22-MO-17 | Confirm M5 requires explicit bounded residue rather than silent tolerated legacy authority. | M5 must require remaining adapters to be diagnosable, profile-bounded, removable, and non-authoritative. |
| TC-V22-MO-18 | Confirm M5 writes compile-boundary completion against the current partial split. | M5 must mention the current partial asmdef split and must forbid kernel-to-legacy or production-to-test inversion. |
| TC-V22-MO-19 | Confirm M6 defines a dedicated full-proof aggregation gate. | M6 must require the 04_1 outputs and must forbid claiming completion through compile success, doc shape, or a single smoke run. |
| TC-V22-MO-20 | Confirm M6 requires the full gate bundle and lower-layer failure propagation. | M6 must require SpecShape through IntegrationSmoke and must state that a failing lower layer invalidates the M6 claim. |
| TC-V22-MO-21 | Confirm M6 does not collapse into M5 hardening. | M6 must forbid treating M5 hardening proof as already sufficient final aggregation. |
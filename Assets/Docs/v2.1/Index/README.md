# GameLib Kernel v2.1 Index

## Document Status

- Document ID: KernelV21Index
- Status: Draft
- Role: entry index for the V21-M0 migration baseline-freeze evidence package
- Depends on:
  - [../00_KernelV21MigrationOverviewSpec.md](../00_KernelV21MigrationOverviewSpec.md)
  - [../07_KernelV21MigrationMilestoneOrderSpec.md](../07_KernelV21MigrationMilestoneOrderSpec.md)
  - [../../v2/Index/KernelV2ConceptMap.md](../../v2/Index/KernelV2ConceptMap.md)
  - [../../v2/Index/ForbiddenPatternRegistry.md](../../v2/Index/ForbiddenPatternRegistry.md)
  - [../../v2/Index/CrossSpecDependencyMatrix.md](../../v2/Index/CrossSpecDependencyMatrix.md)
  - [../../v2/Index/ExistingAnchorInventory.md](../../v2/Index/ExistingAnchorInventory.md)
- Provides foundation for:
  - [../01_WaveABootAndSceneEntryCutoverSpec.md](../01_WaveABootAndSceneEntryCutoverSpec.md)
  - [../02_WaveBScopeAndServiceCompositionCutoverSpec.md](../02_WaveBScopeAndServiceCompositionCutoverSpec.md)
  - [../03_WaveCCommandDispatchCutoverSpec.md](../03_WaveCCommandDispatchCutoverSpec.md)
  - [../04_WaveDValueBlackboardAndVarCutoverSpec.md](../04_WaveDValueBlackboardAndVarCutoverSpec.md)
  - [../05_WaveERepresentativeGameplaySystemsCutoverSpec.md](../05_WaveERepresentativeGameplaySystemsCutoverSpec.md)
  - [../06_WaveFLegacyRemovalAndHardeningSpec.md](../06_WaveFLegacyRemovalAndHardeningSpec.md)

### Revision Note

This revision creates the v2.1 Index package required by V21-M0.

The repository already contains the v2 M0 outputs that fix target-kernel concept ownership, forbidden-pattern vocabulary, dependency order, and initial target-kernel anchor inventory.
V21-M0 does not fork those documents.

Instead, this package adds the migration-specific evidence layer that later v2.1 milestone reviews need:

- one joined migration baseline ledger
- one preservation-floor ledger
- one proof-anchor catalog that separates live boot, direct-play reference, representative gameplay, and residue hardening

---

## Purpose

The purpose of this package is to make V21-M0 claimable without forcing every later milestone to rediscover the same baseline evidence.

This package answers four practical questions:

1. What concrete migration debt is frozen as the current baseline?
2. Which surfaces are globally preserved, which are only slice-local continuity, and which are explicitly replaceable?
3. Which proof families exist already, and what do they prove versus not prove?
4. Where does the migration-specific evidence stop and the upstream v2 M0 package remain authoritative?

---

## Scope

This package defines and exposes the V21-M0 artifact set only.

It covers:

- the migration baseline frozen from Wave A through Wave F inventories
- the narrow preservation ledger that prevents silent widening of the preservation floor
- the proof-anchor catalog that separates proof families before any later claim is made
- the relationship between this package and the upstream v2 M0 package

It does not define:

- target-kernel concept ownership, which remains owned by the v2 concept map
- target-kernel forbidden-pattern policy, which remains owned by the v2 forbidden-pattern registry
- target-kernel dependency order, which remains owned by the v2 dependency matrix
- later wave acceptance criteria, which remain owned by the wave specifications themselves

---

## Artifact Set

| Artifact | V21-M0 role | Why it exists |
| --- | --- | --- |
| [KernelV21BaselineLedger.md](KernelV21BaselineLedger.md) | joined current-state baseline | freezes the migration debt that later milestones must cut over instead of rediscovering |
| [KernelV21PreservationFloorLedger.md](KernelV21PreservationFloorLedger.md) | preservation ledger | keeps the global floor narrow and distinguishes it from wave-local continuity |
| [KernelV21ProofAnchorCatalog.md](KernelV21ProofAnchorCatalog.md) | proof-family catalog | prevents direct play, representative gameplay, and residue hardening from being misread as equivalent proof |
| [../07_KernelV21MigrationMilestoneOrderSpec.md](../07_KernelV21MigrationMilestoneOrderSpec.md) | canonical claim-order contract | remains the only owner of wave-to-milestone mapping and completion-claim order |

---

## Upstream References

The following upstream v2 M0 artifacts remain authoritative and are intentionally reused by reference:

| Upstream artifact | Why V21-M0 reuses it |
| --- | --- |
| [../../v2/Index/KernelV2ConceptMap.md](../../v2/Index/KernelV2ConceptMap.md) | v2 still owns target-kernel concept ownership |
| [../../v2/Index/ForbiddenPatternRegistry.md](../../v2/Index/ForbiddenPatternRegistry.md) | v2 still owns forbidden-pattern vocabulary and required gate intent |
| [../../v2/Index/CrossSpecDependencyMatrix.md](../../v2/Index/CrossSpecDependencyMatrix.md) | v2 still owns target-kernel dependency order |
| [../../v2/Index/ExistingAnchorInventory.md](../../v2/Index/ExistingAnchorInventory.md) | v2 still owns the initial target-kernel anchor inventory pattern |

---

## Operating Rules

1. This package must not clone the upstream v2 concept map, forbidden-pattern registry, dependency matrix, or anchor inventory under new v2.1 names.
2. A migration-specific row belongs here only when it affects V21-M0 claimability for the live game.
3. Direct-play reference evidence must stay separate from live-boot evidence.
4. Representative gameplay evidence must stay separate from residue-hardening evidence.
5. Any attempt to add a new globally preserved surface must update [../00_KernelV21MigrationOverviewSpec.md](../00_KernelV21MigrationOverviewSpec.md) and [../07_KernelV21MigrationMilestoneOrderSpec.md](../07_KernelV21MigrationMilestoneOrderSpec.md) explicitly.

---

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V21-INDEX-01 | Confirm the V21-M0 artifact set is exposed in one place. | This file must link to the baseline ledger, preservation ledger, proof-anchor catalog, and milestone-order spec. |
| TC-V21-INDEX-02 | Confirm the v2 M0 package remains authoritative by reference. | This file must link to the v2 concept map, forbidden-pattern registry, dependency matrix, and anchor inventory. |
| TC-V21-INDEX-03 | Confirm this package is migration-specific rather than a second v2 M0 package. | Scope and Operating Rules must say that target-kernel ownership remains upstream. |
| TC-V21-INDEX-04 | Confirm proof families are intentionally separated before later milestone claims. | Operating Rules must distinguish direct play, live boot, representative gameplay, and residue hardening. |
| TC-V21-INDEX-05 | Confirm 07 remains the canonical owner of wave-to-milestone claim order. | The artifact table must point back to 07_KernelV21MigrationMilestoneOrderSpec.md as the canonical mapping owner. |

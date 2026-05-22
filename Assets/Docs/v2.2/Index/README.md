# GameLib Kernel v2.2 Index

## Document Status

- Document ID: KernelV22Index
- Status: Draft
- Role: entry index for the V22-M0 completion package
- Depends on:
  - [../00_KernelV22CompletionOverviewSpec.md](../00_KernelV22CompletionOverviewSpec.md)
  - [../05_KernelV22MilestoneOrderSpec.md](../05_KernelV22MilestoneOrderSpec.md)
  - [../../v2.1/Index/KernelV21BaselineLedger.md](../../v2.1/Index/KernelV21BaselineLedger.md)
  - [../../v2.1/Index/KernelV21ProofAnchorCatalog.md](../../v2.1/Index/KernelV21ProofAnchorCatalog.md)
  - [../../v2/Index/ForbiddenPatternRegistry.md](../../v2/Index/ForbiddenPatternRegistry.md)
  - [../../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md](../../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md)

## Purpose

This package fixes the v2.2 completion claim surface before runtime implementation expands.

It answers three questions:

1. Which baseline debts still block kernel-only release authority?
2. Which proof families count for v2.2 completion?
3. Which upstream artifacts remain authoritative by reference?

## Artifact Set

| Artifact | V22-M0 role | Why it exists |
| --- | --- | --- |
| [../01_KernelV22AuthorityAndServiceCensusSpec.md](../01_KernelV22AuthorityAndServiceCensusSpec.md) | canonical authority and service census | fixes the five-class vocabulary and the initial family/deletion classification before runtime cutover claims expand |
| [KernelV22BaselineLedger.md](KernelV22BaselineLedger.md) | joined completion baseline | freezes the debts that block kernel-only release authority |
| [KernelV22ProofAnchorCatalog.md](KernelV22ProofAnchorCatalog.md) | proof-family catalog | keeps spec-package proof, live-host proof, family-cutover proof, and release-hardening proof separate |
| [../05_KernelV22MilestoneOrderSpec.md](../05_KernelV22MilestoneOrderSpec.md) | canonical claim-order contract | remains the only owner of v2.2 completion claim order |

## Upstream References

| Upstream artifact | Why v2.2 reuses it |
| --- | --- |
| [../../v2.1/Index/KernelV21BaselineLedger.md](../../v2.1/Index/KernelV21BaselineLedger.md) | v2.1 already froze the migration debt surface that v2.2 consumes as input |
| [../../v2.1/Index/KernelV21ProofAnchorCatalog.md](../../v2.1/Index/KernelV21ProofAnchorCatalog.md) | v2.1 already separated live boot, direct-play reference, representative gameplay, and residue hardening proof families |
| [../../v2/Index/ForbiddenPatternRegistry.md](../../v2/Index/ForbiddenPatternRegistry.md) | v2 still owns forbidden-pattern vocabulary |
| [../../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md](../../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md) | v2 still owns the target compile-boundary graph |

## Operating Rules

1. This package must not fork v2 semantics or v2.1 baseline evidence.
2. This package exists only to make v2.2 completion claims auditable.
3. direct-play reference may remain useful, but it is not a primary v2.2 completion proof family.
4. command/value host-removal proof must remain separate from live-host proof, service-family cutover proof, and release-hardening proof.
5. V22-M0 is incomplete while the service census, baseline ledger, proof catalog, and milestone-order contract disagree about continuity, delete targets, or proof roles.

## Companion Execution Document

- [../06_KernelV22ImplementationPlan.md](../06_KernelV22ImplementationPlan.md) translates the claimable milestones into a practical implementation board.
- It is not part of the V22-M0 claim surface.
- It must not be allowed to rewrite the artifact set or proof-family boundaries defined above.

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V22-INDEX-01 | Confirm the V22-M0 artifact set is exposed in one place. | This file must link to 01_KernelV22AuthorityAndServiceCensusSpec.md, KernelV22BaselineLedger.md, KernelV22ProofAnchorCatalog.md, and 05_KernelV22MilestoneOrderSpec.md. |
| TC-V22-INDEX-02 | Confirm v2.1 baseline and proof artifacts remain authoritative by reference. | This file must link to KernelV21BaselineLedger.md and KernelV21ProofAnchorCatalog.md. |
| TC-V22-INDEX-03 | Confirm v2 forbidden-pattern ownership remains upstream. | This file must link to ForbiddenPatternRegistry.md. |
| TC-V22-INDEX-04 | Confirm compile-boundary ownership remains upstream. | This file must link to 17_AssemblyDefinitionAndCompileBoundarySpec.md. |
| TC-V22-INDEX-05 | Confirm direct play is not promoted to a primary v2.2 completion proof family. | Operating Rules must reject direct-play reference as primary completion proof. |
| TC-V22-INDEX-06 | Confirm the implementation plan is exposed only as a companion document. | This file must link to 06_KernelV22ImplementationPlan.md and state that it is not part of the V22-M0 claim surface. |
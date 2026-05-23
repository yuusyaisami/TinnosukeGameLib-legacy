# Kernel v2.3 M3 Leaf Scope Demotion Execution Specification

## Document Status

- Document ID: 08_KernelV23M3LeafScopeDemotionExecutionSpec
- Status: Draft
- Role: execution-level definition for M3 and M3.x in v2.3
- Depends on:
  - [01_KernelV23ServiceRuntimeModelSpec.md](01_KernelV23ServiceRuntimeModelSpec.md)
  - [02_KernelV23AuthoringRegistrationFlowSpec.md](02_KernelV23AuthoringRegistrationFlowSpec.md)
  - [03_KernelV23MilestoneOrderSpec.md](03_KernelV23MilestoneOrderSpec.md)
  - [04_KernelV23ServiceReconstructionAndCompatibilitySpec.md](04_KernelV23ServiceReconstructionAndCompatibilitySpec.md)
  - [07_KernelV23M2KernelCommandSurfaceExecutionSpec.md](07_KernelV23M2KernelCommandSurfaceExecutionSpec.md)

## Purpose

M3 performs the first production-scale runtime authority cutover by demoting leaf domains from scope-local DI ownership.

M3 is successful only when leaf-domain accepted path runs entirely on kernel-owned service forms.

## Scope

M3 covers:

- leaf-domain service family cutover (entity/ui-element)
- replacement of accepted-path local DI runtime authority
- validation of name/reference continuity during cutover
- authority leakage negative verification
- M4 entry gate evidence packaging

M3 does not cover:

- root/scene integration orchestration changes (M4)
- final global legacy deletion and hardening (M5)

## Non-Negotiable Rules

The following are mandatory and non-waivable in M3:

1. accepted-path authority elimination rule
- leaf-domain accepted path must not retain scope-local DI runtime authority

2. continuity rule
- service names and scene/prefab/script references must remain valid through cutover

3. compatibility shell boundary rule
- compatibility shells may preserve serialization continuity only and must remain non-authoritative

4. no silent fallback rule
- authority failure must not recover through legacy local-container execution

## Mandatory Artifacts

M3 must produce all of the following:

- LeafDomainServiceCutoverPlan
- LeafDomainRuntimePathReplacementReport
- NameReferenceContinuityValidationReport
- AuthorityLeakageNegativeVerificationReport
- CompatibilityShellBoundaryValidationReport
- M4EntryGateEvidencePackage

## M3.x Execution Details

### M3.1 Leaf Domain Freeze

Tasks:

- freeze leaf-domain service families and migration owners
- freeze target service form per family
- freeze cutover order by risk class

Output:

- LeafDomainServiceCutoverPlan

Required fields:

- ServiceFamilyName
- DomainClass
- MigrationOwner
- TargetServiceForm
- CutoverWave
- RiskClass

### M3.2 Service Cutover Design Lock

Tasks:

- define per-family cutover design from legacy authority to kernel ownership
- define compatibility shell behavior and removal preconditions
- define reject conditions for disallowed authority paths

Output:

- LeafDomainServiceCutoverPlan (design-lock section)

Required fields:

- FamilyDesignId
- LegacyAuthorityPath
- TargetKernelPath
- CompatibilityShellPlan
- RejectCondition

### M3.3 Leaf Runtime Path Replacement

Tasks:

- replace accepted-path runtime authority in leaf domains
- route lifecycle operations through kernel command handlers
- remove accepted-path runtime installer discovery reliance

Output:

- LeafDomainRuntimePathReplacementReport

Required fields:

- ReplacementId
- ReplacedLegacyPath
- NewKernelPath
- OwnershipEvidence
- ResidueFlag

### M3.4 Name/Reference Continuity Validation

Tasks:

- validate unchanged service names at integration boundaries
- validate scene/prefab/script references remain intact
- validate compatibility shell remains non-authoritative

Output:

- NameReferenceContinuityValidationReport
- CompatibilityShellBoundaryValidationReport

Required fields:

- ValidationCaseId
- BoundaryType
- ExpectedContinuity
- ObservedContinuity
- PassFail
- EvidenceAnchor

### M3.5 Authority Leakage Negative Verification

Tasks:

- run negative tests for legacy authority acquisition attempts
- verify hard-reject behavior with structured diagnostics
- verify no silent fallback path is reachable

Output:

- AuthorityLeakageNegativeVerificationReport

Required fields:

- NegativeCaseId
- TriggerCondition
- ExpectedRejectCode
- ObservedResult
- FallbackObservedFlag
- EvidenceAnchor

### M3.6 M4 Entry Gate Evidence Package

Tasks:

- compose M4 gate package from all M3 mandatory artifacts
- list unresolved risks requiring M4 handling
- enforce M4 start block if evidence package is incomplete

Output:

- M4EntryGateEvidencePackage

Required fields:

- RequiredArtifact
- PresenceFlag
- ApprovalState
- BlockingCondition

## Exit Criteria

M3 is complete only when all are true:

- all mandatory M3 artifacts are present and approved
- leaf-domain accepted path has zero scope-local DI runtime authority residue
- continuity validation reports pass with no unresolved break
- authority leakage negative verification reports zero fallback reachability
- M4EntryGateEvidencePackage is approved

## Failure Conditions

M3 fails if any of the following occurs:

- any leaf-domain service family remains on accepted-path legacy authority
- continuity validation finds unresolved name/reference break
- compatibility shell performs runtime authority behavior
- negative verification detects fallback path reachability
- M4 starts without approved M4EntryGateEvidencePackage

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V23-08-01 | Confirm M3 requires complete leaf-domain authority cutover. | Spec must require zero accepted-path local DI authority in leaf domains. |
| TC-V23-08-02 | Confirm M3 requires continuity validation. | Spec must require service name and reference continuity evidence. |
| TC-V23-08-03 | Confirm M3 enforces compatibility-shell non-authoritative boundary. | Spec must reject shell authority behavior. |
| TC-V23-08-04 | Confirm M3 requires negative verification against fallback. | Spec must require fallback reachability checks. |
| TC-V23-08-05 | Confirm M3 blocks M4 until gate evidence is approved. | Spec must require approved M4 entry gate package. |
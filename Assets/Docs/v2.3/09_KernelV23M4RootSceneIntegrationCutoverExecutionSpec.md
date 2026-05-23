# Kernel v2.3 M4 Root Scene Integration Cutover Execution Specification

## Document Status

- Document ID: 09_KernelV23M4RootSceneIntegrationCutoverExecutionSpec
- Status: Draft
- Role: execution-level definition for M4 and M4.x in v2.3
- Depends on:
  - [02_KernelV23AuthoringRegistrationFlowSpec.md](02_KernelV23AuthoringRegistrationFlowSpec.md)
  - [03_KernelV23MilestoneOrderSpec.md](03_KernelV23MilestoneOrderSpec.md)
  - [04_KernelV23ServiceReconstructionAndCompatibilitySpec.md](04_KernelV23ServiceReconstructionAndCompatibilitySpec.md)
  - [07_KernelV23M2KernelCommandSurfaceExecutionSpec.md](07_KernelV23M2KernelCommandSurfaceExecutionSpec.md)
  - [08_KernelV23M3LeafScopeDemotionExecutionSpec.md](08_KernelV23M3LeafScopeDemotionExecutionSpec.md)

## Purpose

M4 integrates root/scene runtime orchestration onto plan-first, kernel-owned registration and lifecycle execution.

M4 is successful only when root/scene accepted path has no discovery-based or local-authority runtime composition.

## Scope

M4 covers:

- root/scene boot and registration cutover to verified-plan-first flow
- deterministic ordering and reproducibility verification
- root/scene authority leakage negative verification
- M5 entry gate evidence packaging

M4 does not cover:

- final global deletion and hardening execution (M5)
- final release proof assembly (M6)

## Non-Negotiable Rules

The following are mandatory and non-waivable in M4:

1. plan-first execution rule
- scene boot and registration in accepted path must start from verified plan

2. deterministic ordering rule
- identical plan input must produce identical registration and activation order

3. authority isolation rule
- root/scene accepted path must not depend on discovery-based runtime composition

4. no fallback rule
- plan/authority failures must not recover via local DI or discovery fallback path

## Mandatory Artifacts

M4 must produce all of the following:

- RootSceneIntegrationBoundaryMap
- PlanFirstBootContractSpec
- SceneRegistrationPathCutoverReport
- DeterministicOrderingReproVerificationReport
- RootSceneAuthorityLeakageNegativeVerificationReport
- M5EntryGateEvidencePackage

## M4.x Execution Details

### M4.1 Root/Scene Boundary Freeze

Tasks:

- define ownership boundaries for root/scene boot and registration
- freeze scene-initial scope registration target set
- assign migration wave and owner per integration target

Output:

- RootSceneIntegrationBoundaryMap

Required fields:

- IntegrationTargetName
- CurrentOwner
- TargetOwner
- MigrationOwner
- CutoverWave

### M4.2 Plan-First Boot Contract Lock

Tasks:

- define mandatory verified-plan preconditions for scene boot
- define strict ordering constraints for registration/activation lifecycle
- define explicit reject conditions for plan mismatch or absence

Output:

- PlanFirstBootContractSpec

Required fields:

- ContractRuleId
- Precondition
- OrderingConstraint
- RejectCondition
- DiagnosticPayloadSchema

### M4.3 Scene Registration Path Cutover

Tasks:

- replace accepted-path discovery-based registration in root/scene flows
- route registration through kernel command surface only
- enforce prohibition of local-authority shortcut registrations

Output:

- SceneRegistrationPathCutoverReport

Required fields:

- CutoverId
- ReplacedPath
- NewPlanDrivenPath
- AuthorityIsolationEvidence
- ResidueFlag

### M4.4 Deterministic Ordering and Reproducibility Verification

Tasks:

- run repeated scene boot verification with identical plan input
- compare registration/activation order and resulting state signatures
- verify diagnostics contain ordering and plan source evidence

Output:

- DeterministicOrderingReproVerificationReport

Required fields:

- VerificationCaseId
- PlanInputHash
- ExpectedOrderSignature
- ObservedOrderSignature
- ReproPassFail
- EvidenceAnchor

### M4.5 Root/Scene Authority Leakage Negative Verification

Tasks:

- run negative tests for discovery/local-authority runtime composition attempts
- verify hard reject behavior with structured diagnostics
- verify no fallback path to local DI or dynamic discovery

Output:

- RootSceneAuthorityLeakageNegativeVerificationReport

Required fields:

- NegativeCaseId
- TriggerCondition
- ExpectedRejectCode
- ObservedResult
- FallbackObservedFlag
- EvidenceAnchor

### M4.6 M5 Entry Gate Evidence Package

Tasks:

- compose M5 gate package from all M4 mandatory artifacts
- report unresolved deletion/hardening risks for M5
- block M5 start when evidence package is incomplete

Output:

- M5EntryGateEvidencePackage

Required fields:

- RequiredArtifact
- PresenceFlag
- ApprovalState
- BlockingCondition

## Exit Criteria

M4 is complete only when all are true:

- all mandatory M4 artifacts are present and approved
- root/scene accepted path runs from verified plan only
- deterministic ordering verification passes with no unresolved drift
- authority leakage negative verification reports zero fallback reachability
- M5EntryGateEvidencePackage is approved

## Failure Conditions

M4 fails if any of the following occurs:

- any root/scene accepted-path registration still depends on discovery/local authority
- deterministic ordering verification has unresolved mismatch
- negative verification detects fallback path reachability
- plan mismatch is tolerated without explicit reject handling
- M5 starts without approved M5EntryGateEvidencePackage

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V23-09-01 | Confirm M4 requires verified-plan-first root/scene execution. | Spec must require plan-first boot and reject missing/mismatched plan. |
| TC-V23-09-02 | Confirm M4 requires deterministic ordering verification. | Spec must require reproducibility evidence using order signatures. |
| TC-V23-09-03 | Confirm M4 prohibits discovery/local-authority composition. | Spec must require authority isolation in accepted path. |
| TC-V23-09-04 | Confirm M4 requires negative verification against fallback. | Spec must require fallback reachability checks in root/scene flows. |
| TC-V23-09-05 | Confirm M4 blocks M5 until evidence package approval. | Spec must require approved M5 entry gate package. |
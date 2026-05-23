# Kernel v2.3 M5 Hardening and Delete Execution Specification

## Document Status

- Document ID: 10_KernelV23M5HardeningAndDeleteExecutionSpec
- Status: Draft
- Role: execution-level definition for M5 and M5.x in v2.3
- Depends on:
  - [03_KernelV23MilestoneOrderSpec.md](03_KernelV23MilestoneOrderSpec.md)
  - [04_KernelV23ServiceReconstructionAndCompatibilitySpec.md](04_KernelV23ServiceReconstructionAndCompatibilitySpec.md)
  - [08_KernelV23M3LeafScopeDemotionExecutionSpec.md](08_KernelV23M3LeafScopeDemotionExecutionSpec.md)
  - [09_KernelV23M4RootSceneIntegrationCutoverExecutionSpec.md](09_KernelV23M4RootSceneIntegrationCutoverExecutionSpec.md)

## Purpose

M5 finalizes migration execution by deleting obsolete authority paths and hardening runtime failure behavior.

M5 is successful only when accepted runtime path is free of forbidden authority, fallback, and opaque failure behavior.

## Scope

M5 covers:

- physical deletion of obsolete scope-local DI authority paths
- diagnostics and failure hardening for runtime reject classes
- performance budget validation after hardening/delete operations
- compatibility-shell retirement validation
- M6 entry gate evidence packaging

M5 does not cover:

- final release proof and claim decision process (M6)

## Non-Negotiable Rules

The following are mandatory and non-waivable in M5:

1. deletion completeness rule
- obsolete accepted-path authority paths must be physically removed, not only disabled

2. explicit failure rule
- required rejection cases must emit structured diagnostics and explicit failure codes

3. no fallback rule
- failures must not recover through legacy authority or hidden compatibility behavior

4. performance safety rule
- hardening/delete changes must not violate runtime hot-path budget constraints

## Mandatory Artifacts

M5 must produce all of the following:

- ObsoleteAuthorityDeletionBoundaryInventory
- ObsoleteAuthorityDeletionExecutionReport
- DiagnosticsFailureHardeningVerificationReport
- PerformanceBudgetValidationReport
- CompatibilityShellRetirementValidationReport
- M6EntryGateEvidencePackage

## M5.x Execution Details

### M5.1 Deletion Boundary Freeze

Tasks:

- define final obsolete authority deletion boundary
- classify each target path as delete/retain-for-serialization/remove-later
- assign owner and execution wave per deletion target

Output:

- ObsoleteAuthorityDeletionBoundaryInventory

Required fields:

- TargetId
- TargetPath
- Classification
- MigrationOwner
- DeleteWave
- ContinuityConstraint

### M5.2 Obsolete Authority Path Physical Delete

Tasks:

- delete classified obsolete authority targets
- remove accepted-path references to deleted authority
- verify no compatibility shim reopens deleted route

Output:

- ObsoleteAuthorityDeletionExecutionReport

Required fields:

- DeletionRecordId
- DeletedTargetPath
- RemovedReferenceEvidence
- ReachabilityAfterDelete
- ReintroductionRiskFlag

### M5.3 Diagnostics and Failure Hardening

Tasks:

- define and enforce structured failure codes for required reject classes
- verify diagnostics payload completeness for each reject class
- verify no silent fallback and no swallow behavior in accepted path

Output:

- DiagnosticsFailureHardeningVerificationReport

Required fields:

- RejectClass
- FailureCode
- DiagnosticSchema
- FallbackObservedFlag
- EvidenceAnchor

### M5.4 Performance Budget Validation

Tasks:

- run post-delete/hardening budget checks on runtime hot paths
- compare before/after metrics for critical service families
- classify and track any budget regressions

Output:

- PerformanceBudgetValidationReport

Required fields:

- BudgetCaseId
- HotPathName
- BaselineMetric
- CurrentMetric
- BudgetPassFail
- RegressionRisk

### M5.5 Compatibility Shell Retirement Validation

Tasks:

- validate retirement of obsolete compatibility shells
- validate retained shells are serialization-only and non-authoritative
- validate no reference break after retirement actions

Output:

- CompatibilityShellRetirementValidationReport

Required fields:

- ShellId
- RetirementState
- AuthorityBehaviorFlag
- ReferenceContinuityPassFail
- EvidenceAnchor

### M5.6 M6 Entry Gate Evidence Package

Tasks:

- compose M6 gate package from all M5 mandatory artifacts
- document unresolved risks that block release claim
- block M6 start when evidence package is incomplete

Output:

- M6EntryGateEvidencePackage

Required fields:

- RequiredArtifact
- PresenceFlag
- ApprovalState
- BlockingCondition

## Exit Criteria

M5 is complete only when all are true:

- all mandatory M5 artifacts are present and approved
- accepted runtime path contains no reachable obsolete authority path
- diagnostics/failure hardening verification passes with no fallback observed
- performance budget validation passes with no unresolved violation
- M6EntryGateEvidencePackage is approved

## Failure Conditions

M5 fails if any of the following occurs:

- any obsolete authority path remains reachable in accepted runtime path
- reject class is missing explicit failure code or diagnostics schema
- fallback path is observed during hardening verification
- performance budget has unresolved regression violation
- M6 starts without approved M6EntryGateEvidencePackage

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V23-10-01 | Confirm M5 requires physical deletion of obsolete authority paths. | Spec must require delete execution evidence, not disable-only handling. |
| TC-V23-10-02 | Confirm M5 requires explicit failure and diagnostics hardening. | Spec must define reject classes with failure codes and schema. |
| TC-V23-10-03 | Confirm M5 forbids fallback after hardening/delete. | Spec must require fallback-observed checks. |
| TC-V23-10-04 | Confirm M5 requires performance budget validation. | Spec must require hot-path budget pass or explicit unresolved risk block. |
| TC-V23-10-05 | Confirm M5 blocks M6 until evidence package approval. | Spec must require approved M6 entry gate package. |
# Kernel v2.3 M1 Spec Lock and Census Execution Specification

## Document Status

- Document ID: 06_KernelV23M1SpecLockAndCensusExecutionSpec
- Status: Draft
- Role: execution-level definition for M1 and M1.x in v2.3
- Depends on:
  - [03_KernelV23MilestoneOrderSpec.md](03_KernelV23MilestoneOrderSpec.md)
  - [04_KernelV23ServiceReconstructionAndCompatibilitySpec.md](04_KernelV23ServiceReconstructionAndCompatibilitySpec.md)
  - [05_KernelV23M0FullMigrationContractFreezeExecutionSpec.md](05_KernelV23M0FullMigrationContractFreezeExecutionSpec.md)

## Purpose

M1 converts frozen M0 contract into executable migration planning artifacts.

M1 must leave no unknown ownership path in accepted runtime path.

## Scope

M1 covers:

- M1.1 rule lock verification
- M1.2 runtime authority census
- M1.3 MB responsibility classification
- M1.4 service family inventory freeze
- M1.5 risk and gate baseline for M2 entry

M1 does not cover:

- implementation of M2 command surface
- runtime cutover execution (M3 and later)

## Mandatory Artifacts

M1 must produce all of the following:

- AuthorityPathCensus table
- MBResponsibilityClassification table
- ServiceFamilyInventory table
- M2EntryGate package
- MigrationRiskRegister

## M1.x Execution Details

### M1.1 Rule Lock Verification

Tasks:

- verify normative consistency of 00/01/02/04 against M0 invariants
- resolve unresolved conflicts before census starts

Output:

- RuleLockVerification report

Required fields:

- VerificationRuleId
- CheckedSpecSet
- ConflictDetectedFlag
- ResolutionState
- EvidenceAnchor

### M1.2 Authority Path Census

Tasks:

- enumerate accepted-path runtime authority edges
- record source anchor for each edge (file path, symbol, call chain)
- classify edge authority owner (kernel/scope/mixed/unknown)

Output:

- AuthorityPathCensus table

Required fields:

- PathId
- SourceAnchor
- CurrentOwnerClass
- LegacyAuthorityResidueFlag
- Evidence

### M1.3 MB Responsibility Classification

Tasks:

- classify runtime-affecting MB families
- assign each MB family to declaration-only/mixed/residue
- define action type per family (retain/convert/remove)

Output:

- MBResponsibilityClassification table

Required fields:

- MBFamilyName
- CurrentResponsibilityClass
- TargetResponsibilityClass
- RequiredAction
- BreakRisk

### M1.4 Service Family Inventory Freeze

Tasks:

- instantiate service inventory for all service families
- map each service family to target service form
- assign migration owner and planned delete point

Output:

- ServiceFamilyInventory table

Required fields:

- ServiceFamilyName
- CurrentAuthorityPath
- TargetServiceForm
- MigrationOwner
- NameContinuityRisk
- ReferenceContinuityRisk
- PlannedDeletePoint

### M1.5 Risk and M2 Gate Baseline

Tasks:

- define migration blocker taxonomy
- define M2 entry gates from M1 outputs
- define reject triggers for hidden or unclassified legacy authority

Output:

- M2EntryGate package
- MigrationRiskRegister

Required fields:

- M2EntryGate package:
  - GateItemId
  - RequiredArtifact
  - PresenceFlag
  - ApprovalState
  - BlockingCondition
- MigrationRiskRegister:
  - RiskId
  - RiskDescription
  - Severity
  - MitigationPlan
  - Owner

## Exit Criteria

M1 is complete only when all are true:

- every accepted-path runtime authority edge is classified
- every runtime-affecting MB family is classified
- every service family has inventory record and migration owner
- no unknown owner class remains for accepted-path authority edges
- M2EntryGate package is approved

## Failure Conditions

M1 fails if any of the following occurs:

- census coverage is partial or unverifiable
- any service family is missing from inventory
- owner class is unknown for any accepted-path authority edge
- M2 starts before M1 artifacts are approved

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V23-06-01 | Confirm M1 defines all mandatory artifacts. | Spec must require census, MB, service inventory, risk register, and M2 gate package. |
| TC-V23-06-02 | Confirm M1.2 requires anchored authority evidence. | Spec must require source anchors and evidence fields. |
| TC-V23-06-03 | Confirm M1.4 enforces full service-family coverage. | Spec must prohibit missing service records. |
| TC-V23-06-04 | Confirm M1 exit blocks unknown authority ownership. | Spec must fail completion when unknown owner class exists. |
| TC-V23-06-05 | Confirm M1 blocks premature M2 start. | Spec must require approved M2EntryGate package before M2. |
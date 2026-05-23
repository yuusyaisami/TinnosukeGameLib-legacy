# Kernel v2.3 M2 Kernel Command Surface Execution Specification

## Document Status

- Document ID: 07_KernelV23M2KernelCommandSurfaceExecutionSpec
- Status: Draft
- Role: execution-level definition for M2 and M2.x in v2.3
- Depends on:
  - [01_KernelV23ServiceRuntimeModelSpec.md](01_KernelV23ServiceRuntimeModelSpec.md)
  - [02_KernelV23AuthoringRegistrationFlowSpec.md](02_KernelV23AuthoringRegistrationFlowSpec.md)
  - [03_KernelV23MilestoneOrderSpec.md](03_KernelV23MilestoneOrderSpec.md)
  - [05_KernelV23M0FullMigrationContractFreezeExecutionSpec.md](05_KernelV23M0FullMigrationContractFreezeExecutionSpec.md)
  - [06_KernelV23M1SpecLockAndCensusExecutionSpec.md](06_KernelV23M1SpecLockAndCensusExecutionSpec.md)

## Purpose

M2 establishes the non-bypassable kernel command surface that owns runtime lifecycle execution.

M2 is successful only if accepted runtime path cannot acquire scope-local DI authority.

## Scope

M2 covers:

- command contract lock for lifecycle operations
- declaration-to-command deterministic mapping
- kernel handler implementation for both service forms
- hard rejection of local DI authority in accepted path
- focused runtime verification for command correctness and authority isolation

M2 does not cover:

- service-family migration cutover implementation in leaf/root domains (M3/M4)
- final legacy path deletion work (M5)

## Non-Negotiable Rules

The following are mandatory and non-waivable in M2:

1. no fallback rule
- command execution must not silently fallback to legacy local-container behavior

2. kernel ownership rule
- slot/instance lifecycle authority must remain kernel-owned in all accepted-path flows

3. explicit failure rule
- authority violation and mapping violation must return structured diagnostics

4. gate enforcement rule
- M3 must not start until M2 evidence package is complete and approved

## Mandatory Artifacts

M2 must produce all of the following:

- KernelCommandContractSpec
- DeclarationToCommandMappingTable
- KernelCommandHandlersCoverageReport
- AuthorityViolationRejectionMatrix
- FocusedRuntimeVerificationReport
- M3EntryGateEvidencePackage

## M2.x Execution Details

### M2.1 Command Contract Lock

Tasks:

- define register/build/activate/deactivate/release command signatures
- define idempotency guarantees and duplicate command handling
- define required diagnostics for success/failure

Output:

- KernelCommandContractSpec

Required fields:

- CommandName
- InputSchema
- Precondition
- Postcondition
- FailureCodeSet
- DiagnosticPayloadSchema

### M2.2 Declaration-to-Command Deterministic Mapping

Tasks:

- define deterministic mapping from declaration payload to command sequence
- define form-specific branching for AoS and Scope-ServiceInstance
- reject undeclared targets and malformed declaration inputs

Output:

- DeclarationToCommandMappingTable

Required fields:

- MappingId
- DeclarationSelector
- TargetServiceForm
- CommandSequence
- DeterminismConstraint
- RejectCondition

### M2.3 Kernel Handler Implementation and Ownership Enforcement

Tasks:

- implement kernel handlers for full lifecycle command set
- enforce kernel ownership checks for all slot/instance mutations
- block scope-local authority sources from entering handler path

Output:

- KernelCommandHandlersCoverageReport

Required fields:

- HandlerName
- CoveredCommand
- OwnershipCheck
- LegacyBypassRisk
- CoverageEvidence

### M2.4 Authority Violation Hard-Reject Path

Tasks:

- implement hard-reject on any accepted-path local DI authority request
- assign structured error codes and diagnostic payload shape
- verify reject behavior has no recovery path to legacy authority

Output:

- AuthorityViolationRejectionMatrix

Required fields:

- ViolationType
- DetectionPoint
- ErrorCode
- DiagnosticEvidence
- FallbackBlockedFlag

### M2.5 Focused Runtime Verification

Tasks:

- run focused tests for command ordering/idempotency/failure semantics
- validate declaration-only MB runtime boundary under command execution
- validate authority isolation with negative cases

Output:

- FocusedRuntimeVerificationReport

Required fields:

- VerificationCaseId
- Scenario
- ExpectedResult
- ObservedResult
- PassFail
- EvidenceAnchor

### M2.6 M3 Entry Gate Evidence Package

Tasks:

- compose M3 entry package from M2 artifacts
- define open risks and mandatory mitigations
- enforce M3 start block when evidence set is incomplete

Output:

- M3EntryGateEvidencePackage

Required fields:

- RequiredArtifact
- PresenceFlag
- ApprovalState
- BlockingCondition

## Exit Criteria

M2 is complete only when all are true:

- all mandatory M2 artifacts are present and approved
- command contract is deterministic and ambiguity-free
- authority violation hard-reject path is proven in runtime verification
- no accepted-path fallback to legacy local container behavior exists
- M3EntryGateEvidencePackage is approved

## Failure Conditions

M2 fails if any of the following occurs:

- any lifecycle command lacks contract or diagnostics schema
- mapping allows non-deterministic route selection
- authority violation can bypass reject path
- verification reveals fallback to local DI authority
- M3 starts without approved M3EntryGateEvidencePackage

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V23-07-01 | Confirm M2 forbids any accepted-path authority fallback. | Spec must require hard reject without legacy recovery. |
| TC-V23-07-02 | Confirm M2 command contracts are explicit and testable. | Spec must define command schema, pre/postconditions, and failures. |
| TC-V23-07-03 | Confirm declaration-to-command mapping is deterministic. | Spec must prohibit ambiguous mapping branches. |
| TC-V23-07-04 | Confirm M2 enforces kernel ownership checks in handlers. | Spec must require ownership enforcement evidence per handler. |
| TC-V23-07-05 | Confirm M2 blocks M3 until evidence package approval. | Spec must require M3 entry gate with blocking conditions. |
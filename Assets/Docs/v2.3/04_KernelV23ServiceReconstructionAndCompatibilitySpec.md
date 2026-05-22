# Kernel v2.3 Service Reconstruction and Compatibility Specification

## Document Status

- Document ID: 04_KernelV23ServiceReconstructionAndCompatibilitySpec
- Status: Draft
- Role: defines mandatory full-service reconstruction policy with name stability and reference continuity during v2.3 migration
- Depends on:
  - [00_KernelV23OverviewSpec.md](00_KernelV23OverviewSpec.md)
  - [01_KernelV23ServiceRuntimeModelSpec.md](01_KernelV23ServiceRuntimeModelSpec.md)
  - [02_KernelV23AuthoringRegistrationFlowSpec.md](02_KernelV23AuthoringRegistrationFlowSpec.md)
  - [03_KernelV23MilestoneOrderSpec.md](03_KernelV23MilestoneOrderSpec.md)

## Purpose

This specification makes two requirements non-optional:

1. full migration to v2.3 runtime ownership model
2. complete service-family internal rebuild without external name/reference break

## Full Migration Requirement (Normative)

The accepted runtime path must satisfy all of the following:

- scope-local DI runtime authority residue is zero
- all service families run under kernel-owned AoS or kernel-owned Scope-ServiceInstance form
- MBs are declaration-only in accepted runtime path

Any remaining accepted-path runtime dependency on scope-local container build invalidates v2.3 completion.

## Service Reconstruction Contract (Normative)

All existing service families must be reconstructed internally to new authoring/runtime model.

Allowed:

- full internal refactor
- data layout redesign
- command/runtime ownership rewrite
- replacing builder/injector internals

Required constraints:

- keep service name identity stable at integration boundary
- keep scene/prefab/script references intact
- keep migration-time compatibility bridges strictly non-authoritative

Disallowed:

- rename/delete migration that breaks serialized or script references without approved bridge
- partial migration that leaves accepted-path authority in legacy local DI container

## Migration Inventory and Ownership Plan

Each service family must have an inventory record containing at least:

- ServiceFamilyName
- CurrentAuthorityPath
- TargetServiceForm (AoS or Scope-ServiceInstance)
- AuthoringDeclarationSurface
- KernelCommandHandlers
- CompatibilityBridgeNeeded (yes/no)
- NameContinuityRisk
- ReferenceContinuityRisk
- PlannedDeletePoint

No service family may skip inventory.

## Validation Gates

v2.3 completion gates must include:

- authority residue gate: zero accepted-path scope-local DI authority
- service inventory gate: all service families mapped to target form
- compatibility gate: no name/reference break across migration milestones
- delete gate: temporary compatibility bridges removed when obsolete

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V23-04-01 | Confirm full migration is explicit and mandatory. | Spec must require zero accepted-path local DI authority residue. |
| TC-V23-04-02 | Confirm all service families are mandatory migration targets. | Spec must prohibit exempt service families. |
| TC-V23-04-03 | Confirm internal rebuild with stable names/references is explicit. | Spec must require name identity and reference continuity contract. |
| TC-V23-04-04 | Confirm inventory schema is defined. | Spec must list mandatory per-service migration record fields. |
| TC-V23-04-05 | Confirm completion gates include authority, inventory, compatibility, and delete checks. | Spec must define all four gate classes. |
# Kernel v2.3 Service Runtime Model Specification

## Document Status

- Document ID: 01_KernelV23ServiceRuntimeModelSpec
- Status: Draft
- Role: defines service runtime ownership and execution model for v2.3
- Depends on:
  - [00_KernelV23OverviewSpec.md](00_KernelV23OverviewSpec.md)
  - [../v2/06_ServiceGraphRuntimeSpec.md](../v2/06_ServiceGraphRuntimeSpec.md)
  - [../v2/07_ScopeGraphRuntimeSpec.md](../v2/07_ScopeGraphRuntimeSpec.md)

## Ownership

This specification owns:

- runtime service form classification
- kernel-side ownership rule for service state and instances
- rejection rules for scope-local DI service authority
- runtime dispatch shape between ScopeGraph and Service runtime

This specification does not own:

- command catalog internals
- value schema internals
- Unity authoring schema details

## Normative Runtime Model

### Service Form A: AoS Service

Definition:
- service holds runtime data by ScopeHandle in AoS slots
- service methods process slots in batches or indexed operations
- scope does not own service object instance

Required properties:
- slot creation/destruction is kernel-command driven
- slot access is handle-indexed and generation-safe
- slot lifetime is bound to scope lifetime plan

### Service Form B: Scope-ServiceInstance Service

Definition:
- kernel owns one runtime instance per scope where declared
- instance creation/destruction is kernel-command driven
- scope may reference service capability but does not own the instance container

Required properties:
- instance registry is kernel-side
- runtime ownership remains outside MB and outside scope-local DI container
- diagnostics include scope handle and declaration source

## Prohibited Runtime Model

The following are prohibited in accepted runtime path:

- per-scope local DI container as service runtime authority
- per-scope autonomous service construction based on local component scan
- runtime fallback creation of undeclared service instances

## Service Reconstruction Contract (Normative)

All existing service families must be migrated to v2.3 runtime model under these constraints:

- keep service names stable at integration boundary
- keep serialized/script reference continuity during migration
- replace internal execution ownership with kernel-owned AoS or kernel-owned Scope-ServiceInstance form
- remove scope-local DI ownership semantics from the migrated service implementation

No service family is exempt from migration.
Partial migration that leaves accepted runtime authority in legacy scope-local DI is invalid.

## Runtime API Direction (Conceptual)

Conceptual authority flow:

1. ScopeGraph signals scope lifecycle transitions to Kernel runtime
2. Kernel runtime issues service registration/build/activate/release commands
3. Service runtime applies commands to AoS slots or instance registry
4. Diagnostics/DebugMap record declaration source and execution provenance

## Performance Policy

- AoS services are default for high-cardinality scope domains (entity-like domains)
- Scope-ServiceInstance is allowed where encapsulation/stateful orchestration is required
- service count growth must not track entity count unless explicitly justified by form B
- migration must not reintroduce per-scope container build cost through compatibility layers

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V23-01-01 | Confirm AoS service form is defined as kernel-owned slot model. | Spec must define slot ownership and lifecycle commands. |
| TC-V23-01-02 | Confirm Scope-ServiceInstance form is kernel-owned. | Spec must define kernel-side instance registry ownership. |
| TC-V23-01-03 | Confirm third service form is disallowed. | Spec must explicitly prohibit per-scope local DI authority. |
| TC-V23-01-04 | Confirm fallback runtime construction is rejected. | Spec must prohibit undeclared runtime instance creation. |
| TC-V23-01-05 | Confirm all service families are covered by migration contract. | Spec must require non-exempt migration for all existing services. |
| TC-V23-01-06 | Confirm name/reference continuity requirement is explicit. | Spec must require stable names and reference-safe migration. |

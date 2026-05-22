# Kernel v2.3 Authoring Registration Flow Specification

## Document Status

- Document ID: 02_KernelV23AuthoringRegistrationFlowSpec
- Status: Draft
- Role: defines MB declaration flow from editor compilation to runtime kernel registration/execution in v2.3
- Depends on:
  - [00_KernelV23OverviewSpec.md](00_KernelV23OverviewSpec.md)
  - [01_KernelV23ServiceRuntimeModelSpec.md](01_KernelV23ServiceRuntimeModelSpec.md)
  - [../v2/03_VerifiedPlanGenerationSpec.md](../v2/03_VerifiedPlanGenerationSpec.md)
  - [../v2/12_UnityAuthoringBridgeSpec.md](../v2/12_UnityAuthoringBridgeSpec.md)

## Purpose

This specification makes MB declaration-only policy executable.

It defines how authoring attached to scopes is compiled and registered without granting runtime authority to MB or scope-local DI container.

## Flow

### Phase 1: Editor Compile/Registration Plan

- editor collects scope declarations and attached authoring declarations
- declarations are normalized into verified registration plans
- initial scene scopes are compiled as scene-initial scope set
- each declaration record includes:
  - ScopePlanId
  - ScopeKind
  - ServiceForm target (AoS or Scope-ServiceInstance)
  - declaration payload hash
  - source location for diagnostics

### Phase 2: Runtime Scene Load

- scene kernel loads verified registration plans for initial scene scopes
- kernel creates runtime scope identities from verified plans
- kernel emits build/registration commands in verified order

### Phase 3: Scope Declaration Submit

- scope host provides declaration endpoint only
- scope sends declaration references to kernel runtime
- kernel validates declaration compatibility with verified plan

### Phase 4: Kernel Registration Execute

- kernel maps declaration into one of two service forms
- kernel updates AoS slot service or instance registry
- kernel records diagnostics and debug provenance

### Phase 5: Lifecycle Commands

- kernel sends activate/deactivate/release commands based on lifecycle plan
- services apply lifecycle using kernel-owned state

## MB Responsibility Rules

MB is limited to:

- declaration data surface
- optional editor-time validation
- optional debug authoring helpers

MB must not:

- build runtime container
- own runtime resolver authority
- instantiate runtime service authority autonomously

For migrated legacy services, MB rename/removal that breaks existing references is prohibited in accepted migration path.
Internal structure may change completely as long as external name/reference continuity contract is preserved.

## Scope Host Responsibility Rules

Scope host is limited to:

- identity endpoint
- declaration submission endpoint
- kernel command receiver

Scope host must not:

- own local DI container authority
- perform runtime installer discovery as accepted path

## Legacy-to-New Authoring Cutover Rules

- all existing service MB families must be cut over to declaration-only authoring
- declaration payload must map to ServiceForm (AoS or Scope-ServiceInstance) deterministically
- runtime execution authority must move to kernel runtime command handlers
- migration must preserve service naming and scene/prefab/script references
- accepted runtime path must not contain scope-local DI authority residue

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V23-02-01 | Confirm editor compile stage produces declaration plans for initial scene scopes. | Spec must require scene-initial scope compile registration. |
| TC-V23-02-02 | Confirm runtime registration is kernel-driven. | Spec must require kernel-issued build/registration commands. |
| TC-V23-02-03 | Confirm MB declaration-only boundary is explicit. | Spec must prohibit MB-owned runtime container authority. |
| TC-V23-02-04 | Confirm scope host declaration endpoint model is explicit. | Spec must limit scope host to submit/receive responsibilities. |
| TC-V23-02-05 | Confirm legacy service authoring cutover rule is explicit. | Spec must require all existing service MB families to move to declaration-only model. |
| TC-V23-02-06 | Confirm reference-safe migration rule is explicit. | Spec must require no reference break during service internal rebuild. |

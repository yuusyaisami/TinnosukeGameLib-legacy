# Kernel v2.3 Overview Specification

## Document Status

- Document ID: 00_KernelV23OverviewSpec
- Status: Draft
- Role: defines the v2.3 architectural correction that removes scope-local DI runtime authority and restores Kernel-centered execution
- Depends on:
  - [../v2/06_ServiceGraphRuntimeSpec.md](../v2/06_ServiceGraphRuntimeSpec.md)
  - [../v2/07_ScopeGraphRuntimeSpec.md](../v2/07_ScopeGraphRuntimeSpec.md)
  - [../v2/12_UnityAuthoringBridgeSpec.md](../v2/12_UnityAuthoringBridgeSpec.md)
  - [../v2.2/00_KernelV22CompletionOverviewSpec.md](../v2.2/00_KernelV22CompletionOverviewSpec.md)
- Provides foundation for:
  - [01_KernelV23ServiceRuntimeModelSpec.md](01_KernelV23ServiceRuntimeModelSpec.md)
  - [02_KernelV23AuthoringRegistrationFlowSpec.md](02_KernelV23AuthoringRegistrationFlowSpec.md)
  - [03_KernelV23MilestoneOrderSpec.md](03_KernelV23MilestoneOrderSpec.md)

## Purpose

v2.3 is an architectural correction release.

v2.3 completion target is a full migration target, not a partial coexistence target.

The target is to align runtime ownership with the intended model:

- runtime authority is centralized in Scene Kernel
- MBs are authoring declarations, not runtime authorities
- service execution model is restricted to exactly two forms

v2.3 rejects the following as accepted runtime ownership:

- per-scope local DI container build
- scope-owned runtime resolver authority
- runtime feature discovery via arbitrary local installer projection

v2.3 requires the following completion guarantees:

- scope-local DI runtime authority is removed 100% from accepted runtime path
- all existing service families are re-authored into the new declaration model
- service names remain stable and external references remain unbroken during migration

## Core Statements

```text
Runtime authority belongs to Kernel.
MBs declare; Kernel executes.
Service form is limited to two kinds only.
```

```text
Scopes do not own DI containers.
Scopes submit declarations and receive kernel-issued lifecycle/build commands.
```

```text
Complete migration is mandatory.
Legacy authority residue is not an accepted release state.
```

## Two Service Forms (Normative)

Only these two service forms are allowed in v2.3:

1. AoS Service
- one service owns all runtime instances in structure-of-arrays style by ScopeHandle
- per-scope runtime state is stored in service-managed slots
- no per-scope service object allocation is required by default

2. Scope-ServiceInstance Service
- one service family where Kernel owns explicit per-scope instances
- instance lifecycle is driven by verified plans and kernel commands
- ownership remains kernel-side, not scope-side

No third form is accepted.
In particular, per-scope DI-container-as-authority is prohibited.

## Scope and MB Ownership Policy

- Scope unit remains a runtime identity/lifecycle boundary entity
- MB role is declaration-only (authoring input, optional editor validation)
- MB must not become runtime authority through autonomous container build
- ScopeHost behavior must be reduced to registration endpoint and kernel-command receiver

## Compatibility Policy

v2.3 may keep temporary compatibility shells only to preserve serialized scene/prefab bindings.
Compatibility shells must not retain runtime authority semantics.

Compatibility policy additionally requires:

- service type names and integration-facing identifiers remain stable during rebuild
- scene/prefab/script references must stay valid throughout migration
- compatibility shells must be removable after migration completion gates pass

## Non-Goals

This specification does not define:

- gameplay content tuning
- UI/scene art authoring details
- command payload semantics redefinition
- value key identity remapping

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V23-00-01 | Confirm v2.3 defines kernel-central runtime authority. | This file must state runtime authority belongs to Kernel. |
| TC-V23-00-02 | Confirm MBs are declaration-only in v2.3. | This file must prohibit MB-owned runtime authority. |
| TC-V23-00-03 | Confirm exactly two service forms are normative. | This file must define only AoS and Scope-ServiceInstance service forms. |
| TC-V23-00-04 | Confirm scope-local DI ownership is rejected. | This file must explicitly reject per-scope DI container authority. |
| TC-V23-00-05 | Confirm full migration requirement is explicit. | This file must require 100% removal of scope-local DI runtime authority from accepted path. |
| TC-V23-00-06 | Confirm service name/reference continuity is explicit. | This file must require name-stable and reference-safe migration for all services. |

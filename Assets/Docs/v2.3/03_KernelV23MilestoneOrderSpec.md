# Kernel v2.3 Milestone Order Specification

## Document Status

- Document ID: 03_KernelV23MilestoneOrderSpec
- Status: Draft
- Role: defines execution order for v2.3 architectural correction
- Depends on:
  - [00_KernelV23OverviewSpec.md](00_KernelV23OverviewSpec.md)
  - [01_KernelV23ServiceRuntimeModelSpec.md](01_KernelV23ServiceRuntimeModelSpec.md)
  - [02_KernelV23AuthoringRegistrationFlowSpec.md](02_KernelV23AuthoringRegistrationFlowSpec.md)

## Milestones

### M0: Full-Migration Contract Freeze

- freeze non-negotiable completion target: 100% deletion of scope-local DI runtime authority
- freeze non-negotiable service rebuild target: all services migrated with stable name/reference contract
- freeze release rejection rule for any residual local-container authority on accepted path

Exit criteria:
- full-migration contract approved
- no ambiguity remains on allowed service forms

### M1: Spec Lock and Census

- freeze two-form service rule (AoS / Scope-ServiceInstance)
- census all runtime paths still using scope-local DI authority
- classify MBs into declaration-only vs runtime-authority residue
- create complete service family inventory with migration owner and target form

Exit criteria:
- residue inventory complete
- forbidden authority paths listed with source anchors

### M2: Kernel Command Surface

- implement kernel-side registration/build command surface
- implement scope declaration submission endpoint contract
- block accepted path from local container build authority
- provide kernel registration/build/activate/release command handlers for both service forms

Exit criteria:
- kernel command surface exercised in focused runtime tests

### M3: Leaf Scope Demotion

- demote entity/ui-element domains from scope-local DI authority
- route leaf services to AoS or kernel-owned instance registry
- keep compatibility bridges only for serialization continuity
- preserve existing service names and references while replacing internal ownership model

Exit criteria:
- leaf scope accepted path no longer depends on local DI ownership

### M4: Root/Scene Integration Cutover

- align scene-initial scope compile output with runtime registration flow
- enforce plan-first boot and registration ordering
- remove runtime discovery as accepted composition mechanism
- enforce declaration-only MB runtime behavior for migrated services

Exit criteria:
- scene load and initial registration run from verified plan only

### M5: Hardening and Delete

- delete obsolete scope-local DI authority paths
- harden diagnostics and failure behavior
- validate performance budget for high-cardinality domains
- remove temporary compatibility shells that are no longer needed after reference-safe cutover validation

Exit criteria:
- accepted path contains no forbidden authority path
- gate tests pass

### M6: Full-Proof and Release Claim

- prove 100% migration completion across all service families
- prove zero accepted-path scope-local DI runtime authority residue
- prove service name stability and reference continuity constraints remained intact during migration

Exit criteria:
- release claim package approved
- complete migration accepted

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V23-03-01 | Confirm milestone order starts with spec lock and residue census. | M1 must include inventory and classification. |
| TC-V23-03-02 | Confirm kernel command surface is delivered before leaf demotion completion. | M2 must precede M3 exit claim. |
| TC-V23-03-03 | Confirm leaf demotion explicitly targets entity/ui-element domains. | M3 must name leaf domains and authority removal. |
| TC-V23-03-04 | Confirm final hardening requires deletion of obsolete authority paths. | M5 must require delete and gate pass. |
| TC-V23-03-05 | Confirm milestone order includes full-migration contract freeze. | M0 must require 100% deletion target and name/reference continuity target. |
| TC-V23-03-06 | Confirm milestone order includes full-proof release claim. | M6 must require all-service migration proof and zero-authority-residue proof. |

# Kernel V2 Concept Map

## Document Status

- Document ID: KernelV2ConceptMap
- Status: Draft
- Role: cross-spec concept ownership map for Kernel v2 implementation planning
- Depends on:
  - [00_KernelArchitectureOverviewSpec.md](../00_KernelArchitectureOverviewSpec.md)
  - [01_KernelIRSpec.md](../01_KernelIRSpec.md)
  - [02_ModuleContributionSpec.md](../02_ModuleContributionSpec.md)
  - [03_VerifiedPlanGenerationSpec.md](../03_VerifiedPlanGenerationSpec.md)
  - [04_DependencyValidationSpec.md](../04_DependencyValidationSpec.md)
  - [05_BootManifestAndProfileSpec.md](../05_BootManifestAndProfileSpec.md)
  - [06_ServiceGraphRuntimeSpec.md](../06_ServiceGraphRuntimeSpec.md)
  - [07_ScopeGraphRuntimeSpec.md](../07_ScopeGraphRuntimeSpec.md)
  - [08_LifecyclePlanSpec.md](../08_LifecyclePlanSpec.md)
  - [09_CommandCatalogRuntimeSpec.md](../09_CommandCatalogRuntimeSpec.md)
  - [10_ValueSchemaAndStoreSpec.md](../10_ValueSchemaAndStoreSpec.md)
  - [10_1_ScalarRuntimeAndBindingSpec.md](../10_1_ScalarRuntimeAndBindingSpec.md)
  - [10_2_DynamicValueEvaluationSpec.md](../10_2_DynamicValueEvaluationSpec.md)
  - [11_DebugMapAndDiagnosticsSpec.md](../11_DebugMapAndDiagnosticsSpec.md)
  - [12_UnityAuthoringBridgeSpec.md](../12_UnityAuthoringBridgeSpec.md)
  - [13_LegacyCompatBoundarySpec.md](../13_LegacyCompatBoundarySpec.md)
  - [14_PerformanceBudgetAndRuntimeRulesSpec.md](../14_PerformanceBudgetAndRuntimeRulesSpec.md)
  - [15_TestAndValidationSpec.md](../15_TestAndValidationSpec.md)
- Provides foundation for:
  - [16_ImplementationMilestoneOrderSpec.md](../16_ImplementationMilestoneOrderSpec.md)

### Revision Note

This document records the first-pass ownership map required by M0.2.

It does not introduce new architecture semantics.
It collapses the shared concept vocabulary into one traceable map so later implementation work can check ownership before code or tests are written.

---

## Purpose

The purpose of this document is to ensure that the Kernel v2 concept vocabulary has one clear owner per concept.

If a concept appears in multiple specs, this map distinguishes the owning specification from the consuming or related specifications.

The map is intentionally conservative.
When ownership is unclear, the concept should be treated as a review item rather than silently duplicated.

---

## Scope

This document covers the minimum shared concept set identified by M0.2:

- KernelIR
- ModuleContribution
- VerifiedKernelPlan
- ArtifactSet
- DebugMap
- KernelDiagnostic
- ServiceGraph
- ScopeGraph
- LifecyclePlan
- CommandCatalog
- ValueSchema
- ValueStore
- RuntimeQuery
- UnityAuthoringBridge
- LegacyCompat

It also records closely related cross-cutting concepts that affect ownership boundaries:

- SourceLocation
- DiagnosticCode
- ArtifactHash
- BootManifest
- KernelProfile
- ScopeHandle
- CommandTypeId
- ValueKeyId
- RuntimePathKind
- TestRunReport

This document does not define subsystem behavior.
It only records where each concept is owned and which specs consume it.

---

## Concept Ownership Matrix

| Concept | Owning Spec | Primary Responsibility | Consuming or Related Specs | Notes |
|---|---|---|---|---|
| KernelIR | 01_KernelIRSpec.md | normalized intermediate representation and source-traceable semantic authority | 02, 03, 04, 05, 11, 12, 16 | normalized input, not runtime format |
| ModuleContribution | 02_ModuleContributionSpec.md | declarative module contribution contract into KernelIR | 01, 03, 04, 05, 11, 12, 16 | installer-style mutation is out of scope |
| VerifiedKernelPlan | 03_VerifiedPlanGenerationSpec.md | verified runtime execution input and artifact promotion boundary | 04, 05, 06, 07, 08, 09, 10, 11, 15, 16 | generated only after validation |
| ArtifactSet | 03_VerifiedPlanGenerationSpec.md | generated artifact bundle and consistency contract | 05, 11, 15, 16 | partial sets are invalid |
| DebugMap | 03_VerifiedPlanGenerationSpec.md for generation, 11_DebugMapAndDiagnosticsSpec.md for runtime contract | source-to-identity projection and diagnostics traceability | 05, 06, 07, 08, 09, 10, 12, 13, 14, 15, 16 | generation and runtime contract are split across 03 and 11 |
| KernelDiagnostic | 11_DebugMapAndDiagnosticsSpec.md | structured diagnostics record model and sink contract | 04, 05, 06, 07, 08, 09, 10, 12, 13, 14, 15, 16 | unity logging is centralized |
| ServiceGraph | 06_ServiceGraphRuntimeSpec.md | coarse-grained verified service resolver | 05, 07, 08, 09, 10, 11, 13, 14, 15, 16 | not a general object registry |
| ScopeGraph | 07_ScopeGraphRuntimeSpec.md | runtime scope structure, explicit handles, and ownership boundary | 05, 06, 08, 09, 10, 11, 12, 13, 14, 15, 16 | transform hierarchy is not truth |
| LifecyclePlan | 08_LifecyclePlanSpec.md | lifecycle dispatch tables and phase ownership | 05, 07, 09, 10, 11, 13, 14, 15, 16 | plan-driven, not scan-driven |
| CommandCatalog | 09_CommandCatalogRuntimeSpec.md | verified command identity, payload, and dispatch | 05, 06, 08, 10, 11, 13, 14, 15, 16 | string dispatch and executor discovery are forbidden |
| ValueSchema | 10_ValueSchemaAndStoreSpec.md | schema model for value keys, storage, init, and related runtime rules | 05, 06, 07, 09, 11, 12, 13, 14, 15, 16 | generic value contract |
| ValueStore | 10_ValueSchemaAndStoreSpec.md | runtime value storage and slot-based lookup | 05, 06, 07, 09, 11, 12, 13, 14, 15, 16 | stable-key fallback is forbidden |
| RuntimeQuery | 07_ScopeGraphRuntimeSpec.md and 10_ValueSchemaAndStoreSpec.md as consumers; 07 owns query semantics | explicit runtime lookup contract for scopes and related runtime identities | 05, 06, 08, 09, 10, 11, 13, 14, 15, 16 | query identity must remain explicit |
| UnityAuthoringBridge | 12_UnityAuthoringBridgeSpec.md | authoring extraction and direct-play bridge into verified pipeline | 02, 03, 05, 11, 13, 15, 16 | authoring informs runtime but does not build it directly |
| LegacyCompat | 13_LegacyCompatBoundarySpec.md | quarantine boundary for migration-only compatibility | 05, 06, 07, 08, 09, 10, 11, 12, 14, 15, 16 | one-way adapter path only |
| SourceLocation | 01_KernelIRSpec.md and 11_DebugMapAndDiagnosticsSpec.md | source provenance and diagnostics traceability | 02, 03, 04, 05, 06, 07, 08, 09, 10, 12, 15, 16 | shared provenance concept |
| DiagnosticCode | 11_DebugMapAndDiagnosticsSpec.md | stable diagnostic identity and governance | 04, 05, 06, 07, 08, 09, 10, 12, 13, 14, 15, 16 | codes must not be message text |
| ArtifactHash | 03_VerifiedPlanGenerationSpec.md | hash and version compatibility for verified artifacts | 05, 11, 15, 16 | promotion uses hash equality |
| BootManifest | 05_BootManifestAndProfileSpec.md | verified boot entry point and profile selection | 03, 06, 07, 08, 09, 10, 11, 13, 15, 16 | not a settings dump |
| KernelProfile | 05_BootManifestAndProfileSpec.md | profile selection and boot policy | 03, 11, 13, 15, 16 | Development, Release, Test |
| ScopeHandle | 07_ScopeGraphRuntimeSpec.md | explicit scope identity with generation safety | 08, 10, 11, 13, 14, 15, 16 | generation protects reuse |
| CommandTypeId | 01_KernelIRSpec.md and 09_CommandCatalogRuntimeSpec.md | typed command identity domain | 03, 04, 05, 11, 13, 14, 15, 16 | identity domain, not string key |
| ValueKeyId | 01_KernelIRSpec.md and 10_ValueSchemaAndStoreSpec.md | typed value identity domain | 03, 04, 05, 11, 13, 14, 15, 16 | stable-key fallback is forbidden |
| RuntimePathKind | 14_PerformanceBudgetAndRuntimeRulesSpec.md | path classification for performance and measurement | 06, 07, 08, 09, 10, 11, 13, 15, 16 | hot, warm, cold, boot, editor, validation, test, migration |
| TestRunReport | 15_TestAndValidationSpec.md | test output and acceptance evidence | 03, 11, 14, 16 | runtime evidence must be persisted |

---

## Ownership Rules

1. Every concept listed above has one owning specification.
2. A specification may consume a concept that it does not own, but it must not rename or redefine that concept.
3. If a concept is split across multiple specs, the split must be explicit in this table.
4. If a concept is absent from this table, it is either non-normative or still under review.
5. If a later implementation introduces a new meaning for one of these concepts, the change must be reflected here before the change is treated as stable.

---

## Review Notes

- `DebugMap` is intentionally split between generation ownership in 03 and runtime contract ownership in 11.
- `RuntimeQuery` appears in both scope and value discussions, but the explicit query semantics remain owned by 07.
- `SourceLocation` is shared between normalized IR and diagnostics traceability; the concept map must not be read as duplicating source model ownership.
- `ArtifactHash`, `BootManifest`, `KernelProfile`, and `TestRunReport` are cross-cutting supporting concepts, not independent runtime subsystems.

The central implementation rule is simple:

```text
One concept, one owner, many consumers.
```

If that rule cannot be preserved, the concept needs refinement before implementation starts.
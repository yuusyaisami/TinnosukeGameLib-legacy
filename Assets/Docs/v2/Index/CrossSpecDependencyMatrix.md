# Cross-Spec Dependency Matrix

## Document Status

- Document ID: CrossSpecDependencyMatrix
- Status: Draft
- Role: cross-spec dependency matrix for Kernel v2 implementation planning
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

This document records the first-pass dependency matrix required by M0.4.

It is intentionally aligned with the already-created concept map and forbidden-pattern registry.
The matrix answers a different question: which spec depends on which other specs, and which specs it unlocks.

---

## Purpose

The purpose of this document is to make cross-spec implementation order explicit.

The dependency matrix is the bridge between the conceptual ownership map and the milestone execution order.

It should be used to verify that no runtime-heavy milestone is started before the proof, normalization, validation, and boot prerequisites exist.

---

## Scope

This matrix covers the normalized dependency and foundation relationships across the Kernel v2 spec set:

- 00 through 15
- 10_1 and 10_2 as specialized sub-specs
- 16 as the implementation-order contract
- 17 as the compile-boundary contract

It records direct dependency intent, not semantic detail.

---

## Dependency Matrix

| Spec | Depends On | Provides Foundation For | Notes |
|---|---|---|---|
| 00_KernelArchitectureOverviewSpec.md | none | 01, 02, 03, 04, 05, 06, 07, 08, 09, 10, 10_1, 10_2, 11, 12, 13, 14, 15, 16, 17 | root trust boundary and architecture constraints |
| 01_KernelIRSpec.md | 00 | 02, 03, 04, 05, 06, 07, 08, 09, 10, 11, 12, 16, 17 | normalized IR authority |
| 02_ModuleContributionSpec.md | 00, 01 | 03, 04, 05, 06, 07, 08, 09, 10, 10_2, 11, 12, 16, 17 | declarative module contribution boundary |
| 03_VerifiedPlanGenerationSpec.md | 00, 01, 02 | 04, 05, 06, 07, 08, 09, 10, 10_2, 11, 14, 15, 16, 17 | generation trust boundary |
| 04_DependencyValidationSpec.md | 00, 01, 02, 03 | 05, 06, 07, 08, 09, 10, 10_2, 11, 12, 13, 14, 15, 16, 17 | dependency firewall |
| 05_BootManifestAndProfileSpec.md | 00, 01, 02, 03, 04 | 06, 07, 08, 09, 10, 11, 13, 15, 16, 17 | verified boot entry point |
| 06_ServiceGraphRuntimeSpec.md | 00, 01, 02, 03, 04, 05 | 07, 08, 09, 10, 11, 13, 14, 15, 16, 17 | coarse-grained service runtime |
| 07_ScopeGraphRuntimeSpec.md | 00, 01, 02, 03, 04, 05, 06 | 08, 09, 10, 11, 12, 13, 14, 15, 16, 17 | explicit runtime scope structure |
| 08_LifecyclePlanSpec.md | 00, 01, 02, 03, 04, 05, 07 | 09, 10, 11, 13, 14, 15, 16, 17 | lifecycle dispatch tables |
| 09_CommandCatalogRuntimeSpec.md | 00, 01, 02, 03, 04, 05, 06, 08 | 10, 11, 13, 14, 15, 16, 17 | verified command dispatch |
| 10_ValueSchemaAndStoreSpec.md | 00, 01, 02, 03, 04, 05, 06, 07, 09 | 10_1, 10_2, 11, 12, 13, 14, 15, 16, 17 | value schema and store authority |
| 10_1_ScalarRuntimeAndBindingSpec.md | 00, 01, 02, 03, 04, 05, 06, 07, 08, 09, 10 | 12, 13, 14, 15, 16, 17 | scalar specialization |
| 10_2_DynamicValueEvaluationSpec.md | 00, 01, 02, 03, 04, 05, 06, 07, 08, 09, 10 | 12, 13, 14, 15, 16, 17 | dynamic evaluation specialization |
| 11_DebugMapAndDiagnosticsSpec.md | 00, 01, 02, 03, 04, 05, 06, 07, 08, 09, 10 | 12, 13, 14, 15, 16, 17 | shared diagnostics substrate |
| 12_UnityAuthoringBridgeSpec.md | 00, 01, 02, 03, 04, 05, 06, 07, 08, 09, 10, 11 | 13, 15, 16, 17 | Unity authoring bridge |
| 13_LegacyCompatBoundarySpec.md | 00, 01, 02, 03, 04, 05, 06, 07, 08, 09, 10, 10_1, 10_2, 11, 12 | 14, 15, 16, 17 | legacy quarantine boundary |
| 14_PerformanceBudgetAndRuntimeRulesSpec.md | 00, 01, 02, 03, 04, 05, 06, 07, 08, 09, 10, 10_1, 10_2, 11, 12, 13 | 15, 16, 17 | runtime rules and forbidden operations |
| 15_TestAndValidationSpec.md | 00, 01, 02, 03, 04, 05, 06, 07, 08, 09, 10, 10_1, 10_2, 11, 12, 13, 14 | 17, implementation work after validation gates | executable protection layer |
| 16_ImplementationMilestoneOrderSpec.md | 00, 01, 02, 03, 04, 05, 06, 07, 08, 09, 10, 10_1, 10_2, 11, 12, 13, 14, 15 | 17 | execution-order contract |
| 17_AssemblyDefinitionAndCompileBoundarySpec.md | 00, 01, 02, 03, 04, 05, 06, 07, 08, 09, 10, 10_1, 10_2, 11, 12, 13, 14, 15, 16 | implementation work that creates asmdefs and compile boundary enforcement | compile boundary contract |

---

## Matrix Rules

1. A spec row should list only the specs that are direct dependencies of that spec.
2. A spec row may list a broader set in Provides Foundation For when the lower spec set is intentionally wide.
3. This matrix is a planning artifact, not a substitute for the normative dependency list in each spec.
4. If the matrix and a lower spec disagree, the lower spec and the architecture overview take priority.
5. The matrix should be updated when a new spec is introduced or when a dependency boundary changes.

---

## Review Notes

- 00, 01, 02, 03, 04, and 05 form the pre-runtime trust pipeline.
- 06 through 11 form the runtime core and shared runtime substrate.
- 12 through 15 form the bridge, quarantine, performance, and validation layers.
- 16 explains why implementation order must follow the proof chain rather than the numbering chain.
- 17 defines the compile-time boundary that should make the implementation order enforceable.

The matrix exists so milestone planning can be checked against the actual specification graph instead of intuition.
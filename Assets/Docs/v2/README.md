# GameLib Kernel v2 Docs

このフォルダには、新しい Kernel 基盤の上位仕様と、その前提を固めるためのレビュー文書を置きます。

- [00 Kernel Architecture Overview Review](00_KernelArchitectureOverviewReview.md)
- [00 Kernel Architecture Overview Specification](00_KernelArchitectureOverviewSpec.md)
- [01 Kernel IR Specification](01_KernelIRSpec.md)
- [02 Module Contribution Specification](02_ModuleContributionSpec.md)
- [03 Verified Plan Generation Specification](03_VerifiedPlanGenerationSpec.md)
- [04 Dependency Validation Specification](04_DependencyValidationSpec.md)
- [05 Boot Manifest and Profile Specification](05_BootManifestAndProfileSpec.md)
- [06 Service Graph Runtime Specification](06_ServiceGraphRuntimeSpec.md)
- [07 Scope Graph Runtime Specification](07_ScopeGraphRuntimeSpec.md)
- [08 Lifecycle Plan Specification](08_LifecyclePlanSpec.md)
- [09 Command Catalog Runtime Specification](09_CommandCatalogRuntimeSpec.md)
- [10 Value Schema and Store Specification](10_ValueSchemaAndStoreSpec.md)
- [10-1 Scalar Runtime and Binding Specification](10_1_ScalarRuntimeAndBindingSpec.md)
- [10-2 DynamicValue Evaluation Specification](10_2_DynamicValueEvaluationSpec.md)
- [11 DebugMap and Diagnostics Specification](11_DebugMapAndDiagnosticsSpec.md)
- [12 Unity Authoring Bridge Specification](12_UnityAuthoringBridgeSpec.md)
- [13 Legacy Compatibility Boundary Specification](13_LegacyCompatBoundarySpec.md)
- [14 Performance Budget and Runtime Rules Specification](14_PerformanceBudgetAndRuntimeRulesSpec.md)
- [15 Test and Validation Specification](15_TestAndValidationSpec.md)
- [16 Implementation Milestone Order Specification](16_ImplementationMilestoneOrderSpec.md)
- [17 Assembly Definition and Compile Boundary Specification](17_AssemblyDefinitionAndCompileBoundarySpec.md)
- [Diagnostic Code Traceability Catalog](Index/DiagnosticCodeTraceabilityCatalog.md)

初回の v2 文書では、現行実装の観測結果と移行先の target policy を分離することを最優先にしています。
特に、KernelIR と ModuleContribution と DependencyValidation と BootManifest/Profile policy を下位仕様の先頭に置く方針を固定します。
実装順については 16 で別途固定し、runtime 実装より先に diagnostics/test/static gate と verified pipeline を成立させます。

## Test Cases

| Test Case | Purpose | Execution Note |
|---|---|---|
| TC-README-01 | Confirm the docs index exposes the review memo and created specs. | This file must link to 00, 01, 02, 03, 04, 05, 06, 07, 08, 09, 10, 10-1, 10-2, 11, 12, 13, 14, 15, 16, and 17. |
| TC-README-02 | Confirm the shared test runner is documented. | Use [Run-UnityTests.ps1](../../../Tools/Run-UnityTests.ps1) for EditMode checks. |

These cases are validated by the EditMode doc tests in the workspace.

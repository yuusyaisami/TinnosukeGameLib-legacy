# GameLib Kernel v2 Docs

このフォルダには、新しい Kernel 基盤の上位仕様と、その前提を固めるためのレビュー文書を置きます。

- [00 Kernel Architecture Overview Review](00_KernelArchitectureOverviewReview.md)
- [00 Kernel Architecture Overview Specification](00_KernelArchitectureOverviewSpec.md)
- [01 Kernel IR Specification](01_KernelIRSpec.md)
- [02 Module Contribution Specification](02_ModuleContributionSpec.md)
- [03 Verified Plan Generation Specification](03_VerifiedPlanGenerationSpec.md)
- [04 Dependency Validation Specification](04_DependencyValidationSpec.md)
- [05 Boot Manifest and Profile Specification](05_BootManifestAndProfileSpec.md)

初回の v2 文書では、現行実装の観測結果と移行先の target policy を分離することを最優先にしています。
特に、KernelIR と ModuleContribution と DependencyValidation と BootManifest/Profile policy を下位仕様の先頭に置く方針を固定します。

## Test Cases

| Test Case | Purpose | Execution Note |
|---|---|---|
| TC-README-01 | Confirm the docs index exposes the review memo and created specs. | This file must link to 00, 01, 02, 03, 04, and 05. |
| TC-README-02 | Confirm the shared test runner is documented. | Use [Run-UnityTests.ps1](../../../Tools/Run-UnityTests.ps1) for EditMode checks. |

These cases are validated by the EditMode doc tests in the workspace.

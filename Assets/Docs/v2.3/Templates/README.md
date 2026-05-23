# v2.3 Operational Artifact Templates (M1-M6)

このフォルダは、M1〜M6 の Mandatory Artifacts を実運用で埋めるためのテンプレート群です。

## M1 Templates

- [RuleLockVerificationReport](M1/RuleLockVerificationReport.md)
- [AuthorityPathCensus](M1/AuthorityPathCensus.md)
- [MBResponsibilityClassification](M1/MBResponsibilityClassification.md)
- [ServiceFamilyInventory](M1/ServiceFamilyInventory.md)
- [MigrationRiskRegister](M1/MigrationRiskRegister.md)
- [M2EntryGatePackage](M1/M2EntryGatePackage.md)

## M2 Templates

- [KernelCommandContractSpec](M2/KernelCommandContractSpec.md)
- [DeclarationToCommandMappingTable](M2/DeclarationToCommandMappingTable.md)
- [KernelCommandHandlersCoverageReport](M2/KernelCommandHandlersCoverageReport.md)
- [AuthorityViolationRejectionMatrix](M2/AuthorityViolationRejectionMatrix.md)
- [FocusedRuntimeVerificationReport](M2/FocusedRuntimeVerificationReport.md)
- [M3EntryGateEvidencePackage](M2/M3EntryGateEvidencePackage.md)

## M3 Templates

- [LeafDomainServiceCutoverPlan](M3/LeafDomainServiceCutoverPlan.md)
- [LeafDomainRuntimePathReplacementReport](M3/LeafDomainRuntimePathReplacementReport.md)
- [NameReferenceContinuityValidationReport](M3/NameReferenceContinuityValidationReport.md)
- [AuthorityLeakageNegativeVerificationReport](M3/AuthorityLeakageNegativeVerificationReport.md)
- [CompatibilityShellBoundaryValidationReport](M3/CompatibilityShellBoundaryValidationReport.md)
- [M4EntryGateEvidencePackage](M3/M4EntryGateEvidencePackage.md)

## M4 Templates

- [RootSceneIntegrationBoundaryMap](M4/RootSceneIntegrationBoundaryMap.md)
- [PlanFirstBootContractSpec](M4/PlanFirstBootContractSpec.md)
- [SceneRegistrationPathCutoverReport](M4/SceneRegistrationPathCutoverReport.md)
- [DeterministicOrderingReproVerificationReport](M4/DeterministicOrderingReproVerificationReport.md)
- [RootSceneAuthorityLeakageNegativeVerificationReport](M4/RootSceneAuthorityLeakageNegativeVerificationReport.md)
- [M5EntryGateEvidencePackage](M4/M5EntryGateEvidencePackage.md)

## M5 Templates

- [ObsoleteAuthorityDeletionBoundaryInventory](M5/ObsoleteAuthorityDeletionBoundaryInventory.md)
- [ObsoleteAuthorityDeletionExecutionReport](M5/ObsoleteAuthorityDeletionExecutionReport.md)
- [DiagnosticsFailureHardeningVerificationReport](M5/DiagnosticsFailureHardeningVerificationReport.md)
- [PerformanceBudgetValidationReport](M5/PerformanceBudgetValidationReport.md)
- [CompatibilityShellRetirementValidationReport](M5/CompatibilityShellRetirementValidationReport.md)
- [M6EntryGateEvidencePackage](M5/M6EntryGateEvidencePackage.md)

## M6 Templates

- [FullProofScopeAndCoverageMatrix](M6/FullProofScopeAndCoverageMatrix.md)
- [MigrationCompletionProofReport](M6/MigrationCompletionProofReport.md)
- [AuthorityZeroProofReport](M6/AuthorityZeroProofReport.md)
- [ContinuityProofReport](M6/ContinuityProofReport.md)
- [IndependentClaimReviewDecisionRecord](M6/IndependentClaimReviewDecisionRecord.md)
- [FinalReleaseClaimPackage](M6/FinalReleaseClaimPackage.md)

## Usage Rules

- テンプレート名と成果物名は 1:1 で対応させる。
- 各ファイルの Gate Check と Decision 欄は空欄のまま運用時に埋める。
- 実行時は対応する実行仕様（07〜11）の Required fields を満たすこと。
# Diagnostic Code And Static Rule Traceability Catalog

## Document Status

- Document ID: DiagnosticCodeTraceabilityCatalog
- Status: Draft
- Role: source-of-truth catalog that maps currently implemented diagnostic codes and static rule identifiers to lower-spec failure meaning and verifying test anchors
- Depends on:
  - [../09_CommandCatalogRuntimeSpec.md](../09_CommandCatalogRuntimeSpec.md)
  - [../11_DebugMapAndDiagnosticsSpec.md](../11_DebugMapAndDiagnosticsSpec.md)
  - [../15_TestAndValidationSpec.md](../15_TestAndValidationSpec.md)
  - [../16_ImplementationMilestoneOrderSpec.md](../16_ImplementationMilestoneOrderSpec.md)
  - [ForbiddenPatternRegistry.md](ForbiddenPatternRegistry.md)
- Provides foundation for:
  - implementation work that adds new diagnostic codes after M1.5

### Revision Note

This document is introduced by M1.5.

It is intentionally narrow.
It catalogs only the diagnostic codes and static rule identifiers that are currently implemented or intentionally asserted by the active M1.1 through M1.7 slice.

---

## Purpose

The purpose of this catalog is to keep diagnostic codes and static rule identifiers traceable.

Each catalog entry must answer all of the following:

1. which code is in scope
2. where that code currently enters the implementation surface
3. which lower specification gives the code architectural meaning
4. what failure meaning the code represents
5. which test anchor currently proves the code or rule is exercised

This document is the source of truth for the current traceability layer.
Tests consume this catalog and fail when current implementation identifiers drift without a matching traceability entry.

---

## Scope

This catalog currently covers:

- current runtime-owned diagnostic infrastructure code
- current representative lower-spec code asserted by diagnostics and artifact tests
- current `STATIC_RULE_*` identifiers introduced by the active M1.4 through M1.7 slice

This catalog does not currently cover:

- future subsystem code inventories that are not yet implemented
- generic placeholder codes used only to exercise sink ordering or buffer behavior
- inferred or speculative codes not present in the active implementation slice

---

## Catalog

| Identifier | Identifier Kind | Current Owner | Owning Specs | Spec Evidence | Failure Meaning | Verifying Test Anchor | Notes |
|---|---|---|---|---|---|---|---|
| COMMAND_EXECUTOR_MISSING | DiagnosticCode | Assets/Editor/Tests/KernelDiagnosticsModelTests.cs; Assets/Editor/Tests/KernelTestArtifactWriterTests.cs | Assets/Docs/v2/09_CommandCatalogRuntimeSpec.md | COMMAND_EXECUTOR_MISSING; executor | Required command executor could not be resolved for verified command dispatch, so command execution must fail at the command boundary instead of falling back. | KernelDiagnosticsModelTests.KernelDiagnostic_PreservesStructuredFields | Representative current command-domain code carried through the diagnostics model and artifact writer. |
| DIAG_SINK_EMIT_FAILED | DiagnosticCode | Assets/GameLib/Script/Kernel/Diagnostics/Service/KernelDiagnosticService.cs | Assets/Docs/v2/11_DebugMapAndDiagnosticsSpec.md; Assets/Docs/v2/15_TestAndValidationSpec.md | diagnostics degradation; structured diagnostics | One configured diagnostic sink failed during fan-out and the diagnostics pipeline had to report degradation without suppressing the primary diagnostic. | KernelDiagnosticServiceTests.Report_ContinuesToHealthySinks_WhenOneSinkThrows_AndEmitsDegradationDiagnostic | First runtime-owned diagnostics infrastructure failure code in the current implementation slice. |
| STATIC_RULE_DEBUG_LOG_OUTSIDE_SINK | StaticRuleId | Assets/Editor/Tests/KernelForbiddenPatternScanner.cs | Assets/Docs/v2/11_DebugMapAndDiagnosticsSpec.md | Only the central Unity diagnostic sink may call Debug.Log / Debug.LogWarning / Debug.LogError / Debug.LogException.; Subsystems do not log to Unity directly. | Direct Unity info logging was introduced outside the approved diagnostics sink boundary in Kernel code paths. | KernelForbiddenPatternScannerTests.ScanText_ReportsDebugLogCallsOutsideApprovedSink | Static debug-gate identifier introduced by M1.6. |
| STATIC_RULE_DEBUG_LOG_ERROR_OUTSIDE_SINK | StaticRuleId | Assets/Editor/Tests/KernelForbiddenPatternScanner.cs | Assets/Docs/v2/11_DebugMapAndDiagnosticsSpec.md; Assets/Docs/v2/Index/ForbiddenPatternRegistry.md | Only the central Unity diagnostic sink may call Debug.Log / Debug.LogWarning / Debug.LogError / Debug.LogException.; Direct `Debug.LogError` call outside approved sinks | Direct Unity error logging was introduced outside the approved diagnostics sink boundary in Kernel code paths. | KernelForbiddenPatternScannerTests.ScanText_DoesNotAllowUnapprovedDebugCallInsideApprovedFile | Static rule ID introduced by M1.4. |
| STATIC_RULE_DEBUG_LOG_WARNING_OUTSIDE_SINK | StaticRuleId | Assets/Editor/Tests/KernelForbiddenPatternScanner.cs | Assets/Docs/v2/11_DebugMapAndDiagnosticsSpec.md; Assets/Docs/v2/Index/ForbiddenPatternRegistry.md | Only the central Unity diagnostic sink may call Debug.Log / Debug.LogWarning / Debug.LogError / Debug.LogException.; Direct `Debug.LogWarning` call outside approved sinks | Direct Unity warning logging was introduced outside the approved diagnostics sink boundary in Kernel code paths. | KernelForbiddenPatternScannerTests.ScanText_ReportsDebugLogWarningCallsOutsideApprovedSink | Static rule ID introduced by M1.4. |
| STATIC_RULE_DEBUG_LOG_EXCEPTION_OUTSIDE_SINK | StaticRuleId | Assets/Editor/Tests/KernelForbiddenPatternScanner.cs | Assets/Docs/v2/11_DebugMapAndDiagnosticsSpec.md; Assets/Docs/v2/Index/ForbiddenPatternRegistry.md | Only the central Unity diagnostic sink may call Debug.Log / Debug.LogWarning / Debug.LogError / Debug.LogException.; Direct `Debug.LogException` call outside approved sinks | Direct Unity exception logging was introduced outside the approved diagnostics sink boundary in Kernel code paths. | KernelForbiddenPatternScannerTests.ScanText_ReportsDebugLogExceptionCalls | Static rule ID introduced by M1.4. |
| STATIC_RULE_RESOURCES_LOAD_IN_KERNEL_RUNTIME | StaticRuleId | Assets/Editor/Tests/KernelForbiddenPatternScanner.cs | Assets/Docs/v2/00_KernelArchitectureOverviewSpec.md; Assets/Docs/v2/05_BootManifestAndProfileSpec.md; Assets/Docs/v2/14_PerformanceBudgetAndRuntimeRulesSpec.md; Assets/Docs/v2/Index/ForbiddenPatternRegistry.md | scattered `Resources.Load`; `Resources.Load` required-asset fallback | Kernel runtime attempted required-asset loading through `Resources.Load`, which would reintroduce fallback-style discovery into verified runtime paths. | KernelForbiddenPatternScannerTests.ScanText_ReportsForbiddenApiWithStableRuleIdAndLineNumber | Static rule ID introduced by M1.4. |
| STATIC_RULE_FIND_OBJECTS_BY_TYPE_IN_KERNEL_RUNTIME | StaticRuleId | Assets/Editor/Tests/KernelForbiddenPatternScanner.cs | Assets/Docs/v2/00_KernelArchitectureOverviewSpec.md; Assets/Docs/v2/07_ScopeGraphRuntimeSpec.md; Assets/Docs/v2/14_PerformanceBudgetAndRuntimeRulesSpec.md; Assets/Docs/v2/Index/ForbiddenPatternRegistry.md | FindObjectsByType; `FindObjectsByType` for kernel lookup | Kernel runtime attempted scene-wide object discovery through `FindObjectsByType`, which violates explicit scope and service authority rules. | KernelForbiddenPatternScannerTests.ScanText_ReportsQualifiedFindObjectsByTypeCalls | Static rule ID introduced by M1.4. |
| STATIC_RULE_GET_COMPONENTS_IN_CHILDREN_IN_KERNEL_RUNTIME | StaticRuleId | Assets/Editor/Tests/KernelForbiddenPatternScanner.cs | Assets/Docs/v2/00_KernelArchitectureOverviewSpec.md; Assets/Docs/v2/06_ServiceGraphRuntimeSpec.md; Assets/Docs/v2/07_ScopeGraphRuntimeSpec.md; Assets/Docs/v2/12_UnityAuthoringBridgeSpec.md; Assets/Docs/v2/14_PerformanceBudgetAndRuntimeRulesSpec.md; Assets/Docs/v2/Index/ForbiddenPatternRegistry.md | GetComponentsInChildren; `GetComponentsInChildren` for runtime discovery | Kernel runtime attempted hierarchy-based child discovery through `GetComponentsInChildren`, which violates explicit runtime structure and authoring extraction rules. | KernelForbiddenPatternScannerTests.ScanText_ReportsGetComponentsInChildrenCalls | Static rule ID introduced by M1.4. |
| STATIC_RULE_TRANSFORM_PARENT_SCOPE_INFERENCE_IN_KERNEL_RUNTIME | StaticRuleId | Assets/Editor/Tests/KernelForbiddenPatternScanner.cs | Assets/Docs/v2/00_KernelArchitectureOverviewSpec.md; Assets/Docs/v2/07_ScopeGraphRuntimeSpec.md; Assets/Docs/v2/14_PerformanceBudgetAndRuntimeRulesSpec.md; Assets/Docs/v2/15_TestAndValidationSpec.md; Assets/Docs/v2/16_ImplementationMilestoneOrderSpec.md; Assets/Docs/v2/Index/ForbiddenPatternRegistry.md | transform parent traversal for scope ownership inference; Parent-child relationships must not be inferred from Transform.parent.; `Transform.parent` traversal for scope ownership; `Transform.parent` ownership inference in runtime truth paths; `Transform.parent` scope inference in target runtime paths; `Transform.parent` scope inference | Kernel runtime attempted to derive scope ownership from Transform hierarchy instead of verified ScopeGraph authority. | KernelForbiddenPatternScannerTests.ScanText_ReportsTransformParentScopeInferenceCalls | Static forbidden-API rule introduced by M1.7. |

---

## Explicit Non-Catalog Codes

The following strings currently appear only as generic scaffold or behavior-test placeholders and are not architectural catalog entries in M1.5:

- `DIAG_A`
- `DIAG_B`
- `DIAG_C`
- `DIAG_FANOUT`
- `DIAG_PRIMARY`
- `DIAG_RESET`
- `DIAG_INFO`
- `DIAG_WARNING`
- `DIAG_ERROR`
- `DIAG_FATAL`
- `DIAG_TRACE`
- `DIAG_SESSION_BIND`
- `DIAG_CONTEXT_MISMATCH`
- `DIAG_INVALID_SEVERITY`
- `DIAG_INVALID_DOMAIN`

If any of these codes becomes a real architectural diagnostic code later, that promotion must happen explicitly together with a new catalog row and a verifying test anchor.
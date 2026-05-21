# Forbidden Pattern Registry

## Document Status

- Document ID: ForbiddenPatternRegistry
- Status: Draft
- Role: cross-spec registry of forbidden runtime and authoring patterns for Kernel v2 implementation planning
- Depends on:
  - [00_KernelArchitectureOverviewSpec.md](../00_KernelArchitectureOverviewSpec.md)
  - [03_VerifiedPlanGenerationSpec.md](../03_VerifiedPlanGenerationSpec.md)
  - [05_BootManifestAndProfileSpec.md](../05_BootManifestAndProfileSpec.md)
  - [06_ServiceGraphRuntimeSpec.md](../06_ServiceGraphRuntimeSpec.md)
  - [07_ScopeGraphRuntimeSpec.md](../07_ScopeGraphRuntimeSpec.md)
  - [08_LifecyclePlanSpec.md](../08_LifecyclePlanSpec.md)
  - [09_CommandCatalogRuntimeSpec.md](../09_CommandCatalogRuntimeSpec.md)
  - [10_ValueSchemaAndStoreSpec.md](../10_ValueSchemaAndStoreSpec.md)
  - [11_DebugMapAndDiagnosticsSpec.md](../11_DebugMapAndDiagnosticsSpec.md)
  - [12_UnityAuthoringBridgeSpec.md](../12_UnityAuthoringBridgeSpec.md)
  - [13_LegacyCompatBoundarySpec.md](../13_LegacyCompatBoundarySpec.md)
  - [14_PerformanceBudgetAndRuntimeRulesSpec.md](../14_PerformanceBudgetAndRuntimeRulesSpec.md)
  - [15_TestAndValidationSpec.md](../15_TestAndValidationSpec.md)
- Provides foundation for:
  - [16_ImplementationMilestoneOrderSpec.md](../16_ImplementationMilestoneOrderSpec.md)

### Revision Note

This document records the forbidden pattern vocabulary required by M0.3 and its later M13.3 expansion.

It is a registry, not a justification memo.
Each entry maps a forbidden pattern to the owning lower specification or to the test gate that must reject it.

---

## Purpose

The purpose of this document is to make architecture failures visible before they re-enter implementation work.

If a pattern is listed here, it must not be introduced into target runtime paths, authoring-to-runtime bridges, or migration code that is expected to become target-kernel code.

The registry is meant to be consumed by static tests, code review, and implementation planning.

---

## Scope

This registry covers the initial M0.3 seed set and the later M13.3 forbidden-API expansion that already appears in 00, 05, 06, 07, 08, 09, 10, 11, 12, 13, 14, 15, and 16.

The seed set is intentionally small and high-value.
It should grow only when a new forbidden pattern has a clear owner and a clear regression gate.

---

## Registry

| Forbidden Pattern | Category | Owner Spec | Required Gate | Notes |
|---|---|---|---|---|
| Direct `Debug.Log` call outside approved sinks | Diagnostics | 11_DebugMapAndDiagnosticsSpec.md | static doc or source gate | info output must also stay inside the central diagnostics sink boundary |
| Direct `Debug.LogError` call outside approved sinks | Diagnostics | 11_DebugMapAndDiagnosticsSpec.md | static doc or source gate | only `UnityLogDiagnosticSink` may emit Unity error output for kernel diagnostics |
| Direct `Debug.LogWarning` call outside approved sinks | Diagnostics | 11_DebugMapAndDiagnosticsSpec.md | static doc or source gate | warning output must also flow through the diagnostic pipeline |
| Direct `Debug.LogException` call outside approved sinks | Diagnostics | 11_DebugMapAndDiagnosticsSpec.md | static doc or source gate | exception reporting must be routed through the shared diagnostics boundary |
| `GetComponentsInChildren` for runtime discovery | Runtime Discovery | 00_KernelArchitectureOverviewSpec.md, 06_ServiceGraphRuntimeSpec.md, 07_ScopeGraphRuntimeSpec.md, 12_UnityAuthoringBridgeSpec.md, 14_PerformanceBudgetAndRuntimeRulesSpec.md | static forbidden-API gate | forbidden in target runtime paths and authoring extraction paths that must remain explicit |
| `FindObjectsByType` for kernel lookup | Runtime Discovery | 00_KernelArchitectureOverviewSpec.md, 05_BootManifestAndProfileSpec.md, 07_ScopeGraphRuntimeSpec.md, 14_PerformanceBudgetAndRuntimeRulesSpec.md | static forbidden-API gate | scene-wide search is not the authority for kernel structure |
| `Transform.parent` scope inference | Scope Authority | 00_KernelArchitectureOverviewSpec.md, 07_ScopeGraphRuntimeSpec.md, 14_PerformanceBudgetAndRuntimeRulesSpec.md | static forbidden-API gate | runtime scope truth must come from ScopeGraph, not hierarchy inference |
| `Resources.Load` required-asset fallback | Boot and Asset Loading | 00_KernelArchitectureOverviewSpec.md, 05_BootManifestAndProfileSpec.md, 14_PerformanceBudgetAndRuntimeRulesSpec.md | static forbidden-API gate | boot must fail closed on missing required artifact input |
| Runtime stable-key lookup | Value and Identity | 00_KernelArchitectureOverviewSpec.md, 10_ValueSchemaAndStoreSpec.md, 10_1_ScalarRuntimeAndBindingSpec.md, 10_2_DynamicValueEvaluationSpec.md | validation and source review gate | stable-key fallback is a migration smell, not target behavior |
| Runtime-generated negative IDs | Identity | 01_KernelIRSpec.md, 10_ValueSchemaAndStoreSpec.md | validation and source review gate | negative ID creation must not be used to repair missing identity at runtime |
| `IReadOnlyList<ICommandExecutor>` discovery | Command Runtime | 00_KernelArchitectureOverviewSpec.md, 09_CommandCatalogRuntimeSpec.md, 14_PerformanceBudgetAndRuntimeRulesSpec.md | static source gate | command dispatch must be table-driven, not executor-list discovered |
| `FindFirstObjectByType` for kernel lookup | Runtime Discovery | 00_KernelArchitectureOverviewSpec.md, 05_BootManifestAndProfileSpec.md, 07_ScopeGraphRuntimeSpec.md, 14_PerformanceBudgetAndRuntimeRulesSpec.md | static forbidden-API gate | scene-wide search is not the authority for kernel structure |
| `FindAnyObjectByType` for kernel lookup | Runtime Discovery | 00_KernelArchitectureOverviewSpec.md, 05_BootManifestAndProfileSpec.md, 07_ScopeGraphRuntimeSpec.md, 14_PerformanceBudgetAndRuntimeRulesSpec.md | static forbidden-API gate | scene-wide search is not the authority for kernel structure |
| `GetComponentsInParent` for runtime discovery | Runtime Discovery | 00_KernelArchitectureOverviewSpec.md, 06_ServiceGraphRuntimeSpec.md, 07_ScopeGraphRuntimeSpec.md, 12_UnityAuthoringBridgeSpec.md, 14_PerformanceBudgetAndRuntimeRulesSpec.md | static forbidden-API gate | parent traversal is not an explicit runtime ownership model |
| `GameObject.Find` for kernel lookup | Runtime Discovery | 00_KernelArchitectureOverviewSpec.md, 05_BootManifestAndProfileSpec.md, 07_ScopeGraphRuntimeSpec.md, 14_PerformanceBudgetAndRuntimeRulesSpec.md | static forbidden-API gate | scene hierarchy search is not the authority for kernel structure |
| `Activator.CreateInstance` fallback construction | Construction | 06_ServiceGraphRuntimeSpec.md, 14_PerformanceBudgetAndRuntimeRulesSpec.md | static forbidden-API gate | construction must stay on verified plans, not reflection fallback |
| `CommandKeyResolver` string dispatch | Command Runtime | 00_KernelArchitectureOverviewSpec.md, 09_CommandCatalogRuntimeSpec.md, 14_PerformanceBudgetAndRuntimeRulesSpec.md | static forbidden-API gate | command keys must be resolved from verified catalog data, not ad-hoc string dispatch |
| `IScopeLateTickHandler` scan | Lifecycle Runtime | 00_KernelArchitectureOverviewSpec.md, 08_LifecyclePlanSpec.md, 14_PerformanceBudgetAndRuntimeRulesSpec.md | static source gate | lifecycle participation must come from verified plans, not interface scans |
| `IScopeReleaseHandler` scan | Lifecycle Runtime | 00_KernelArchitectureOverviewSpec.md, 08_LifecyclePlanSpec.md, 14_PerformanceBudgetAndRuntimeRulesSpec.md | static source gate | lifecycle participation must come from verified plans, not interface scans |
| `IScopeAcquireHandler` scan | Lifecycle Runtime | 00_KernelArchitectureOverviewSpec.md, 08_LifecyclePlanSpec.md, 14_PerformanceBudgetAndRuntimeRulesSpec.md | static source gate | lifecycle participation must come from verified plans, not interface scans |
| `IScopeTickHandler` scan | Lifecycle Runtime | 00_KernelArchitectureOverviewSpec.md, 08_LifecyclePlanSpec.md, 14_PerformanceBudgetAndRuntimeRulesSpec.md | static source gate | tick dispatch must be table-driven and budgeted |
| ServiceGraph as runtime object registry | Service Runtime | 06_ServiceGraphRuntimeSpec.md | architecture review gate | ServiceGraph is a coarse-grained resolver, not a general object registry |
| BootManifest as global settings dump | Boot and Profile | 05_BootManifestAndProfileSpec.md | architecture review gate | BootManifest selects verified artifacts and profile policy only |
| Legacy fallback repair | Legacy Boundary | 00_KernelArchitectureOverviewSpec.md, 13_LegacyCompatBoundarySpec.md | architecture review gate | legacy can adapt only through explicit quarantine boundaries, never as a repair path |

---

## Registry Rules

1. A forbidden pattern is invalid by default in target-kernel code paths.
2. The owning spec defines the semantic reason the pattern is forbidden.
3. The required gate defines how the pattern is caught early.
4. If a pattern is only acceptable in migration tooling, that exception must be explicit and profile-scoped.
5. If a new pattern overlaps an existing entry, the registry must be updated instead of introducing a silent duplicate.

---

## Enforcement Notes

- Static gates should prefer exact source matching for high-risk APIs and patterns.
- Architecture review should treat these patterns as regression items, not style preferences.
- Test gates should fail on reintroduction even when the runtime behavior appears to work.

The registry exists to make the implementation order in 16 meaningful.
If the team can name the forbidden pattern, the team can detect it before it becomes architecture debt.
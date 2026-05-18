# Existing Anchor Inventory

## Document Status

- Document ID: ExistingAnchorInventory
- Status: Draft
- Role: inventory of current code locations that anchor Kernel v2 migration planning
- Depends on:
  - [00_KernelArchitectureOverviewSpec.md](../00_KernelArchitectureOverviewSpec.md)
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

This document records the initial M0.5 anchor inventory.

It does not claim to be exhaustive.
It lists the current code locations that the architecture docs already identify as the clearest migration anchors.

---

## Purpose

The purpose of this document is to pin the current codebase to concrete migration anchors.

The inventory is used to keep implementation planning grounded in real code locations rather than in abstract subsystem names.

Each anchor below has three jobs:

1. identify the current code location
2. show why the location matters to Kernel v2 migration
3. indicate which architecture specs are directly pressured by the location

---

## Scope

This inventory covers the current anchor set most directly relevant to the v2 migration plan.

It groups anchors into four categories:

- runtime build and boot anchors
- service, scope, lifecycle, and command anchors
- value, registry, and loading anchors
- diagnostics and logging anchors

The inventory does not attempt to catalogue every file in the repository.
It captures the anchor set already called out by the architecture specs as representative current debt.

---

## Anchor Inventory

### Runtime Build and Boot Anchors

| Current Code Location | Why It Matters | Pressured Specs |
|---|---|---|
| [Assets/GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs](../../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs) | scope build flow, parent resolution, and installer discovery are currently co-located | 00, 02, 05, 06, 07, 08, 14, 16 |
| [Assets/GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs](../../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs) | nearest-scope filtering and installer discovery show how transform-derived ownership currently leaks into scope build | 00, 02, 07, 12, 14, 16 |
| [Assets/GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs](../../../GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs) | scene transition and loading behavior still depend on discovery-oriented boot flow | 00, 05, 12, 14, 16 |

### Service, Scope, Lifecycle, and Command Anchors

| Current Code Location | Why It Matters | Pressured Specs |
|---|---|---|
| [Assets/GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs](../../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs) | resolver build, acquire/release dispatch, and runtime resolution behavior are centralized here | 00, 04, 06, 08, 11, 14, 16 |
| [Assets/GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs](../../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs) | bulk command executor registration remains a major boot-time anchor | 00, 02, 09, 14, 16 |
| [Assets/GameLib/Script/Common/Commands/VNext/Core/CommandExecutorRegistry.cs](../../../GameLib/Script/Common/Commands/VNext/Core/CommandExecutorRegistry.cs) | executor lookup and duplicate/invalid ID behavior still represent the current command dispatch path | 09, 11, 14, 15, 16 |

### Value, Registry, and Loading Anchors

| Current Code Location | Why It Matters | Pressured Specs |
|---|---|---|
| [Assets/GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs](../../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs) | blackboard behavior still overlaps with value and dynamic-evaluation responsibilities | 00, 10, 10_2, 14, 16 |
| [Assets/GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs](../../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs) | stable-key resolution and runtime-only negative IDs are explicit target-kernel risks | 00, 10, 10_1, 10_2, 14, 16 |
| [Assets/GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs](../../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs) | resource fallback for registry lookup is the clearest current loading fallback anchor | 00, 05, 10, 11, 14, 16 |

### Diagnostics and Logging Anchors

| Current Code Location | Why It Matters | Pressured Specs |
|---|---|---|
| [Assets/GameLib/Script/Common/LTS/LTSLog.cs](../../../GameLib/Script/Common/LTS/LTSLog.cs) | direct Unity Debug usage is the canonical current logging wrapper | 00, 11, 13, 14, 15, 16 |
| [Assets/GameLib/Script/Common/Variables/Dynamic/Core/DynamicRuntimeLogUtility.cs](../../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicRuntimeLogUtility.cs) | dynamic runtime logging already carries structured context that should move behind the diagnostics pipeline | 10, 11, 14, 16 |
| [Assets/GameLib/Script/Common/Variables/Dynamic/Expression/ExpressionRuntimeLogger.cs](../../../GameLib/Script/Common/Variables/Dynamic/Expression/ExpressionRuntimeLogger.cs) | structured context still ends in direct logging, making it a useful migration anchor | 10, 11, 14, 16 |
| [Assets/GameLib/Script/Common/Variables/Save/Unity/UnitySaveLogger.cs](../../../GameLib/Script/Common/Variables/Save/Unity/UnitySaveLogger.cs) | save-path logging remains an example of a subsystem-specific Unity logging bridge | 10, 11, 14, 16 |

---

## Coverage Notes

- The runtime build and boot anchors are the primary M0/M1/M5 pressure points.
- The service, scope, lifecycle, and command anchors demonstrate the current runtime composition surface that later specs must replace.
- The value, registry, and loading anchors are the strongest evidence for M10 and M14 migration work.
- The diagnostics and logging anchors are the clearest proof that M1 and M11 must exist before runtime work expands.

---

## Inventory Rules

1. An anchor is current if the code location still exists in the workspace and is named in the architecture docs as a representative location.
2. An anchor should remain in this inventory until the associated migration task either replaces it or explicitly quarantines it.
3. If a future anchor becomes more representative than one listed here, the inventory should be updated rather than duplicated.
4. The inventory is a planning aid, not a runtime dependency list.

---

## Review Notes

The existing anchor inventory is intentionally smaller than the concept map and dependency matrix.

The reason is simple:

```text
concept map = what exists in the architecture vocabulary
dependency matrix = how specs relate
anchor inventory = where the current code still shows the migration debt
```

That distinction matters because M0.5 is about concrete code locations, not conceptual ownership.
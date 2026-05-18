# Assembly Definition and Compile Boundary Specification

## Document Status

- Document ID: 17_AssemblyDefinitionAndCompileBoundarySpec
- Status: Draft
- Role: defines the asmdef layer model, compile boundary enforcement, dependency direction rules, and external package policy for Kernel v2
- Depends on:
  - [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md)
  - [01_KernelIRSpec.md](01_KernelIRSpec.md)
  - [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md)
  - [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md)
  - [04_DependencyValidationSpec.md](04_DependencyValidationSpec.md)
  - [05_BootManifestAndProfileSpec.md](05_BootManifestAndProfileSpec.md)
  - [06_ServiceGraphRuntimeSpec.md](06_ServiceGraphRuntimeSpec.md)
  - [07_ScopeGraphRuntimeSpec.md](07_ScopeGraphRuntimeSpec.md)
  - [08_LifecyclePlanSpec.md](08_LifecyclePlanSpec.md)
  - [09_CommandCatalogRuntimeSpec.md](09_CommandCatalogRuntimeSpec.md)
  - [10_ValueSchemaAndStoreSpec.md](10_ValueSchemaAndStoreSpec.md)
  - [10_1_ScalarRuntimeAndBindingSpec.md](10_1_ScalarRuntimeAndBindingSpec.md)
  - [10_2_DynamicValueEvaluationSpec.md](10_2_DynamicValueEvaluationSpec.md)
  - [11_DebugMapAndDiagnosticsSpec.md](11_DebugMapAndDiagnosticsSpec.md)
  - [12_UnityAuthoringBridgeSpec.md](12_UnityAuthoringBridgeSpec.md)
  - [13_LegacyCompatBoundarySpec.md](13_LegacyCompatBoundarySpec.md)
  - [14_PerformanceBudgetAndRuntimeRulesSpec.md](14_PerformanceBudgetAndRuntimeRulesSpec.md)
  - [15_TestAndValidationSpec.md](15_TestAndValidationSpec.md)
  - [16_ImplementationMilestoneOrderSpec.md](16_ImplementationMilestoneOrderSpec.md)
- Provides foundation for:
  - implementation work that creates the asmdef split, dependency gates, and compile-boundary enforcement tests

### Revision Note

This revision creates 17 as the asmdef and compile-boundary specification.

It is intentionally numbered after 16 because 16 is reserved for implementation order.
This document owns the dependency graph that makes the implementation order enforceable at compile time.

It also records the current compile debt state: GameLib code is still effectively compiled as a large monolith, while the visible asmdefs in the workspace are third-party and tooling assemblies rather than a GameLib v2 split.

---

## Ownership

This specification owns:

- assembly layer model and naming convention
- runtime / editor / test boundary policy
- pure C# / Unity runtime / Unity editor separation rules
- kernel core assembly model
- runtime subsystem assembly model
- Unity authoring assembly model
- feature module assembly model
- legacy assembly quarantine policy
- generated code assembly policy
- external package dependency policy
- allowed and forbidden dependency matrices
- circular dependency avoidance policy
- test assembly policy
- migration order for introducing asmdefs
- static rule enforcement and failure policy

This specification does not own:

- KernelIR semantics
- dependency validation semantics
- generation algorithm semantics
- diagnostics record semantics
- runtime execution semantics
- boot acceptance semantics
- gameplay feature semantics
- CI vendor configuration files

17 defines compile boundaries.
It does not redefine the meaning of KernelIR, ServiceGraph, ScopeGraph, LifecyclePlan, CommandCatalog, ValueStore, or LegacyCompat.

---

## Purpose

This specification defines how the Kernel v2 codebase must be split into assemblies so that dependency direction is enforced by the compiler instead of by convention.

Core statements:

```text
asmdef is not only a compile-time optimization.
It is an architecture boundary enforcement tool.

Lower layers must not reference higher layers.
Runtime must not reference Editor.
Kernel core must not reference Feature.
Kernel core must not reference Legacy.
Diagnostics may be shared, but direct Unity Debug output must remain isolated in the diagnostics Unity sink assembly.
```

If the architecture permits invalid dependency direction through assembly references, the runtime design will eventually absorb that invalid direction.

The purpose of 17 is to prevent that drift before it becomes expensive to unwind.

---

## Scope

This specification defines:

- assembly layer philosophy
- assembly naming convention
- runtime / Editor / Test boundaries
- pure C# / Unity runtime / Unity editor boundaries
- kernel core assembly set
- runtime subsystem assembly set
- Unity authoring assembly set
- feature module assembly set
- legacy quarantine assembly set
- generated code assembly policy
- external package dependency policy
- allowed dependency matrix
- forbidden dependency matrix
- circular dependency avoidance policy
- test assembly policy
- migration order
- static rule enforcement
- failure policy
- required test cases
- acceptance criteria

This specification intentionally does not define:

- the runtime behavior of the subsystems themselves
- the exact IR or validation algorithms
- the final serialized data formats
- gameplay feature design
- CI or release pipeline YAML

17 is a compile boundary document.
It must not become a disguised runtime architecture spec.

---

## Non-Goals

This specification does not define:

- a promise that every assembly split must happen in one pass
- a requirement that every folder move happens immediately with the asmdef move
- editor UI design for assembly inspection tools
- a replacement for the lower subsystem specs
- a guarantee that package dependencies disappear
- a requirement to split assemblies so finely that the dependency graph becomes brittle

The goal is clear boundaries, not maximal fragmentation.

---

## Relationship to Other Specs

| Spec | Relationship |
|---|---|
| [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md) | Defines the root constraints that the compile boundary must enforce at the assembly level. |
| [01_KernelIRSpec.md](01_KernelIRSpec.md) | Defines normalized data authority that must remain Unity-free and assembly-stable. |
| [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md) | Defines declarative contributions that must not depend on runtime builder mutation. |
| [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md) | Defines generation trust boundaries that should not be contaminated by editor-only or feature-only dependencies. |
| [04_DependencyValidationSpec.md](04_DependencyValidationSpec.md) | Defines validation rules that are easiest to enforce when the assembly graph already reflects the dependency graph. |
| [05_BootManifestAndProfileSpec.md](05_BootManifestAndProfileSpec.md) | Defines boot policy and verified artifact selection that must remain separate from feature and legacy assemblies. |
| [06_ServiceGraphRuntimeSpec.md](06_ServiceGraphRuntimeSpec.md) | Defines coarse-grained service runtime that must not become a DI-container-shaped compile dependency sink. |
| [07_ScopeGraphRuntimeSpec.md](07_ScopeGraphRuntimeSpec.md) | Defines scope authority that must remain distinct from Unity hierarchy and feature code. |
| [08_LifecyclePlanSpec.md](08_LifecyclePlanSpec.md) | Defines lifecycle dispatch that should depend on small boundary interfaces, not on concrete feature assemblies. |
| [09_CommandCatalogRuntimeSpec.md](09_CommandCatalogRuntimeSpec.md) | Defines command dispatch that must stay isolated from value, feature, and legacy implementation details. |
| [10_ValueSchemaAndStoreSpec.md](10_ValueSchemaAndStoreSpec.md) | Defines value schema and store semantics that must not be polluted by command or feature assemblies. |
| [10_1_ScalarRuntimeAndBindingSpec.md](10_1_ScalarRuntimeAndBindingSpec.md) | Defines scalar specialization that should remain a leaf assembly. |
| [10_2_DynamicValueEvaluationSpec.md](10_2_DynamicValueEvaluationSpec.md) | Defines dynamic evaluation that should remain runtime leaf code, not core scaffolding. |
| [11_DebugMapAndDiagnosticsSpec.md](11_DebugMapAndDiagnosticsSpec.md) | Defines diagnostics ownership, including the rule that only one assembly may emit Unity Debug output for kernel diagnostics. |
| [12_UnityAuthoringBridgeSpec.md](12_UnityAuthoringBridgeSpec.md) | Defines authoring extraction and direct-play bridge rules that should be isolated in editor assemblies. |
| [13_LegacyCompatBoundarySpec.md](13_LegacyCompatBoundarySpec.md) | Defines the quarantine boundary that the kernel core must not cross in reverse. |
| [14_PerformanceBudgetAndRuntimeRulesSpec.md](14_PerformanceBudgetAndRuntimeRulesSpec.md) | Defines runtime path and forbidden-operation rules that should be reflected in the assembly graph. |
| [15_TestAndValidationSpec.md](15_TestAndValidationSpec.md) | Defines the executable gate model that should validate assembly boundaries and dependency regressions. |
| [16_ImplementationMilestoneOrderSpec.md](16_ImplementationMilestoneOrderSpec.md) | Defines the implementation sequence that should place the asmdef split before runtime-rich subsystem expansion. |

17 is not a runtime subsystem spec.
It is the dependency wall that keeps the runtime subsystem specs honest.

---

## Current Compile Debt Observations

This section records the current codebase state that motivates the asmdef split.

### Observation Traceability

| Observation | Evidence Type | Pressure |
|---|---|---|
| No GameLib asmdef was found under the GameLib source tree. | Workspace search | current GameLib code still behaves like a large monolith; M17, M15, and M16 pressure are high |
| The visible asmdefs are primarily third-party or tooling assemblies. | Workspace search | the target kernel split still needs its own assembly graph |
| The project manifest includes UniTask, InputSystem, VContainer, URP, uGUI, and Unity Test Framework. | `Packages/manifest.json` | external package access must be explicit by layer |
| The generated solution can fail early on a hardcoded analyzer path in the Unity-generated csproj files. | Build log / environment evidence | compile boundary work should not be coupled to environment-specific analyzer leakage |

### Representative Anchors

- [Packages/manifest.json](../../../Packages/manifest.json) - package graph showing UniTask, InputSystem, VContainer, URP, uGUI, and Unity Test Framework dependencies
- [Assets/vInspector/VInspector.asmdef](../../vInspector/VInspector.asmdef) - example of existing tooling asmdef in the workspace
- [Assets/vHierarchy/VHierarchy.asmdef](../../vHierarchy/VHierarchy.asmdef) - example of existing tooling asmdef in the workspace
- [Assets/vFolders/VFolders.asmdef](../../vFolders/VFolders.asmdef) - example of existing tooling asmdef in the workspace
- [Assets/GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs) - current GameLib runtime monolith pressure point
- [Assets/GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs](../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs) - current GameLib runtime monolith pressure point
- [Assets/GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs) - current command registration pressure point
- [Assets/GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs](../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs) - current value-system pressure point
- [Assets/GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs) - current loading fallback pressure point
- [Assets/GameLib/Script/Common/LTS/LTSLog.cs](../../GameLib/Script/Common/LTS/LTSLog.cs) - current Unity logging pressure point

### Compile Debt Summary

The current structure allows the following failure modes by default:

- feature code can leak into kernel core compile paths
- Editor code can leak into runtime compile paths
- legacy code can be reached from v2 core by reference rather than quarantine
- external packages can spread into the wrong layer when assemblies are not explicit
- direct logging paths can remain available across the codebase instead of being centralized

The asmdef split is the compile-time tool that closes those paths.

---

## Assembly Layer Philosophy

The assembly graph should be shallow enough to understand and strict enough to enforce.

```text
Core data and contracts first.
Runtime orchestration next.
Unity bridge and authoring only where the engine is required.
Features only at the leaf.
Legacy only in quarantine.
Tests only as consumers.
```

The design should favor stable low-volatility assemblies for typed IDs, diagnostics contracts, IR, and validation rules.

Higher-volatility code such as authoring, feature logic, editor UI, and runtime integrations should live in leaf assemblies so that frequent changes do not force the core graph to recompile.

---

## Assembly Naming Convention

Use a single namespace prefix with layer suffixes.

Recommended convention:

- `GameLib.Foundation`
- `GameLib.Foundation.Unity`
- `GameLib.Foundation.Editor`
- `GameLib.Kernel.Diagnostics`
- `GameLib.Kernel.Diagnostics.Unity`
- `GameLib.Kernel.Diagnostics.Editor`
- `GameLib.Kernel.Abstractions`
- `GameLib.Kernel.IR`
- `GameLib.Kernel.Contributions`
- `GameLib.Kernel.Validation`
- `GameLib.Kernel.Generation`
- `GameLib.Kernel.Boot`
- `GameLib.Kernel.Boot.Unity`
- `GameLib.Kernel.Runtime`
- `GameLib.Kernel.ServiceGraph`
- `GameLib.Kernel.ScopeGraph`
- `GameLib.Kernel.Lifecycle`
- `GameLib.Kernel.Command`
- `GameLib.Kernel.Value`
- `GameLib.Kernel.Value.Scalar`
- `GameLib.Kernel.Value.Dynamic`
- `GameLib.Kernel.RuntimeQuery`
- `GameLib.Kernel.Unity`
- `GameLib.Kernel.Authoring`
- `GameLib.Kernel.Authoring.Editor`
- `GameLib.Features.*`
- `GameLib.Legacy.*`
- `GameLib.Tests.*`

Rules:

- `rootNamespace` should match the assembly name.
- `Editor` assemblies should use `includePlatforms: [Editor]`.
- Leaf runtime assemblies should prefer a narrow public surface over broad friend access.
- Generated assemblies, if used, should be named explicitly as generated artifacts rather than blended into core.

---

## Runtime / Editor / Test Boundary

The three most important compile partitions are runtime, editor, and test.

- Runtime assemblies may not reference `UnityEditor`.
- Editor assemblies may reference runtime assemblies, but runtime assemblies may not reference editor assemblies.
- Test assemblies may reference runtime and editor assemblies as needed, but production assemblies may not reference test assemblies.
- `GameLib.Kernel.Diagnostics.Unity` and `GameLib.Kernel.Authoring.Editor` are the intended choke points for engine-specific kernel work.

This boundary exists so that editor-only conveniences do not become runtime dependencies.

---

## Pure C# / Unity Runtime / Unity Editor Boundary

The kernel should be pure C# by default.

Recommended default:

- `noEngineReferences: true` for `GameLib.Foundation`, `GameLib.Kernel.Diagnostics`, `GameLib.Kernel.Abstractions`, `GameLib.Kernel.IR`, `GameLib.Kernel.Contributions`, `GameLib.Kernel.Validation`, `GameLib.Kernel.Generation`, `GameLib.Kernel.Boot`, `GameLib.Kernel.Runtime`, `GameLib.Kernel.ServiceGraph`, `GameLib.Kernel.ScopeGraph`, `GameLib.Kernel.Lifecycle`, `GameLib.Kernel.Command`, `GameLib.Kernel.Value`, `GameLib.Kernel.Value.Scalar`, `GameLib.Kernel.Value.Dynamic`, and `GameLib.Kernel.RuntimeQuery`.
- `UnityEngine` access should be isolated to Unity bridge, authoring, feature, or compatibility assemblies that truly need it.
- `UnityEditor` access should be isolated to Editor-only assemblies.
- Direct `Debug.Log*` usage should be isolated to `GameLib.Kernel.Diagnostics.Unity`.

If a pure core assembly needs `UnityEngine`, the design should be reviewed before the reference is added.

---

## Kernel Core Assembly Model

The core kernel assemblies should be small, explicit, and stable.

| Assembly | Primary Responsibility | Notes |
|---|---|---|
| `GameLib.Foundation` | typed ID primitives, Result/Error, hashing, deterministic helpers | no Unity references |
| `GameLib.Foundation.Unity` | Unity bridge primitives and profiler wrappers | only Unity-safe primitives |
| `GameLib.Foundation.Editor` | GUID, local file ID, and path helpers | Editor-only |
| `GameLib.Kernel.Diagnostics` | `KernelDiagnostic`, code, severity, domain, failure boundary, context, sink interfaces, in-memory and test sinks | no direct Unity logging |
| `GameLib.Kernel.Diagnostics.Unity` | `UnityLogDiagnosticSink` and the only legal Unity Debug output path for kernel diagnostics | only approved Unity output |
| `GameLib.Kernel.Diagnostics.Editor` | diagnostics browsing, asset navigation, source lookup | Editor-only |
| `GameLib.Kernel.Abstractions` | kernel-specific typed IDs, shared small contracts, and cross-layer boundary interfaces | keep interfaces small |
| `GameLib.Kernel.IR` | normalized KernelIR and source-traceable dependency structure | no runtime execution logic |
| `GameLib.Kernel.Contributions` | declarative module contributions that normalize into KernelIR | no runtime builder mutation |
| `GameLib.Kernel.Validation` | dependency correctness, duplicate checks, cycle checks, and validation reports | no runtime repair |
| `GameLib.Kernel.Generation` | generated and verified plans, artifact headers, hash checks, deterministic generation | no scene search or runtime fallback |
| `GameLib.Kernel.Boot` | boot policy, boot manifest model, profile selection, verified artifact acceptance | pure boot contract |

---

## Runtime Subsystem Assembly Model

The runtime subsystem assemblies should depend on the core kernel, not on features or legacy.

| Assembly | Primary Responsibility | Notes |
|---|---|---|
| `GameLib.Kernel.Runtime` | runtime session, shared handles, runtime orchestration | minimal orchestration only |
| `GameLib.Kernel.ServiceGraph` | coarse-grained verified service resolution | not a general DI container |
| `GameLib.Kernel.ScopeGraph` | explicit scope ownership, handles, and state | Unity hierarchy is not truth |
| `GameLib.Kernel.Lifecycle` | verified lifecycle dispatch tables and phases | no registration scans |
| `GameLib.Kernel.Command` | table-driven command identity, payload, and dispatch | no string dispatch |
| `GameLib.Kernel.Value` | schema and store boundary, slot lookup, init, save policy | no stable-key runtime fallback |
| `GameLib.Kernel.Value.Scalar` | float-specialized scalar runtime | leaf specialization |
| `GameLib.Kernel.Value.Dynamic` | dynamic and reactive evaluation | leaf specialization |
| `GameLib.Kernel.RuntimeQuery` | explicit runtime query and lookup contracts | not a global search layer |

Runtime subsystem assemblies may reference shared plans and handles, but they should not directly depend on each other unless the lower spec explicitly requires it.

---

## Unity Authoring Assembly Model

Unity authoring and runtime must not be the same compile unit.

| Assembly | Primary Responsibility | Notes |
|---|---|---|
| `GameLib.Kernel.Unity` | Unity object bridge, MonoBehaviour runtime links, scene binding | runtime bridge only |
| `GameLib.Kernel.Authoring` | authoring components and serialized declaration data | declaration only |
| `GameLib.Kernel.Authoring.Editor` | extraction, validation UI, source collection, direct-play preparation | Editor-only |
| `GameLib.Kernel.Generation.Editor` | generated file write and deterministic editor generation | Editor-only |
| `GameLib.Kernel.Boot.Unity` | ScriptableObject boot asset and Unity boot entry | keep boot contract separate from engine glue |

The authoring side may describe runtime structure, but it must not construct runtime structure directly.

---

## Feature Module Assembly Model

Feature assemblies belong at the edge of the graph.

| Assembly Family | Examples | Notes |
|---|---|---|
| `GameLib.Features.UI` | modal stack, tooltip, UI element bridge | may depend on kernel public APIs and uGUI where needed |
| `GameLib.Features.SceneChannels` | mesh channel, animation sprite channel, material FX targets | keep runtime players out of kernel core |
| `GameLib.Features.Audio` | audio feature modules | feature leaf |
| `GameLib.Features.SceneFlow` | loading and scene flow features | feature leaf |
| `GameLib.Features.Collision` | collision features | feature leaf |
| `GameLib.Features.Save` | save features | may depend on value contracts but not on value core internals |

Feature assemblies may reference kernel public APIs and the Unity packages they need.
They must not become a way for kernel core to learn feature internals.

---

## Legacy Assembly Quarantine

Legacy code is allowed only as a quarantined migration boundary.

| Assembly Family | Allowed Purpose | Notes |
|---|---|---|
| `GameLib.Legacy` | legacy root quarantine | not a target runtime API |
| `GameLib.Legacy.LTS` | legacy lifetime-scope migration support | migration only |
| `GameLib.Legacy.Commands` | legacy command migration support | migration only |
| `GameLib.Legacy.Blackboard` | legacy value migration support | migration only |
| `GameLib.Legacy.Compat` | explicit adapters and bridges | one-way only |
| `GameLib.Legacy.Editor` | migration tooling | Editor-only |

Rules:

- `GameLib.Kernel.*` may not reference `GameLib.Legacy.*`.
- `GameLib.Legacy.*` may reference the public kernel APIs only.
- Legacy is not a fallback repair path.

---

## Generated Code Assembly Policy

Generated output should not widen the compile graph.

Rules:

- Generated code should be kept in leaf assemblies or generated assets that do not force the core graph to recompile.
- Generated assemblies should not define the source of truth for kernel semantics.
- The core kernel should prefer reading verified generated data or minimal generated accessors rather than depending on a large generated code surface.
- If a generated assembly becomes volatile, its outputs should move back to data or asset form rather than being pushed into core.

Possible generated names:

- `GameLib.Generated.KernelPlans`
- `GameLib.Generated.ValueKeys`
- `GameLib.Generated.CommandIds`

Those names are reserved for later use only if the generated surface is stable enough to justify them.

---

## External Package Dependency Policy

External packages must be isolated to the layers that actually need them.

| Package | Allowed Layers | Forbidden Layers |
|---|---|---|
| UniTask | `GameLib.Kernel.Lifecycle`, `GameLib.Kernel.Command`, `GameLib.Kernel.Value.Dynamic`, `GameLib.Features.*` | `GameLib.Foundation`, `GameLib.Kernel.Abstractions`, `GameLib.Kernel.IR`, `GameLib.Kernel.Contributions`, `GameLib.Kernel.Validation`, `GameLib.Kernel.Generation`, `GameLib.Kernel.Boot` |
| InputSystem | feature or authoring leaf assemblies that explicitly own input | core kernel assemblies |
| URP | rendering feature assemblies only | core kernel assemblies |
| uGUI / UnityEngine.UI | UI feature assemblies only | core kernel assemblies |
| VContainer | `GameLib.Legacy.*` only, if it remains necessary for quarantine | all v2 kernel core assemblies |
| Odin | authoring and editor assemblies only | core kernel assemblies |
| Unity Test Framework | `GameLib.Tests.*` only | production assemblies |

If a package appears in a core assembly, the assembly split is too loose.

---

## Allowed Dependency Matrix

This matrix states the intended positive dependencies.

| Assembly | May Reference |
|---|---|
| `GameLib.Foundation` | none |
| `GameLib.Foundation.Unity` | `GameLib.Foundation`, UnityEngine |
| `GameLib.Foundation.Editor` | `GameLib.Foundation`, `GameLib.Foundation.Unity`, UnityEditor |
| `GameLib.Kernel.Diagnostics` | `GameLib.Foundation` |
| `GameLib.Kernel.Diagnostics.Unity` | `GameLib.Foundation`, `GameLib.Foundation.Unity`, `GameLib.Kernel.Diagnostics` |
| `GameLib.Kernel.Diagnostics.Editor` | `GameLib.Foundation`, `GameLib.Foundation.Editor`, `GameLib.Kernel.Diagnostics`, `GameLib.Kernel.Diagnostics.Unity` |
| `GameLib.Kernel.Abstractions` | `GameLib.Foundation`, `GameLib.Kernel.Diagnostics` |
| `GameLib.Kernel.IR` | `GameLib.Foundation`, `GameLib.Kernel.Diagnostics`, `GameLib.Kernel.Abstractions` |
| `GameLib.Kernel.Contributions` | `GameLib.Foundation`, `GameLib.Kernel.Diagnostics`, `GameLib.Kernel.Abstractions`, `GameLib.Kernel.IR` |
| `GameLib.Kernel.Validation` | `GameLib.Foundation`, `GameLib.Kernel.Diagnostics`, `GameLib.Kernel.Abstractions`, `GameLib.Kernel.IR`, `GameLib.Kernel.Contributions` |
| `GameLib.Kernel.Generation` | `GameLib.Foundation`, `GameLib.Kernel.Diagnostics`, `GameLib.Kernel.Abstractions`, `GameLib.Kernel.IR`, `GameLib.Kernel.Contributions`, `GameLib.Kernel.Validation` |
| `GameLib.Kernel.Boot` | `GameLib.Foundation`, `GameLib.Kernel.Diagnostics`, `GameLib.Kernel.Abstractions`, `GameLib.Kernel.IR`, `GameLib.Kernel.Contributions`, `GameLib.Kernel.Validation`, `GameLib.Kernel.Generation` |
| `GameLib.Kernel.Boot.Unity` | `GameLib.Foundation.Unity`, `GameLib.Kernel.Diagnostics.Unity`, `GameLib.Kernel.Boot` |
| `GameLib.Kernel.Runtime` | `GameLib.Foundation`, `GameLib.Kernel.Diagnostics`, `GameLib.Kernel.Abstractions`, `GameLib.Kernel.Validation`, `GameLib.Kernel.Generation`, `GameLib.Kernel.Boot` |
| `GameLib.Kernel.ServiceGraph` | `GameLib.Foundation`, `GameLib.Kernel.Diagnostics`, `GameLib.Kernel.Abstractions`, `GameLib.Kernel.Runtime` |
| `GameLib.Kernel.ScopeGraph` | `GameLib.Foundation`, `GameLib.Kernel.Diagnostics`, `GameLib.Kernel.Abstractions`, `GameLib.Kernel.Runtime` |
| `GameLib.Kernel.Lifecycle` | `GameLib.Foundation`, `GameLib.Kernel.Diagnostics`, `GameLib.Kernel.Abstractions`, `GameLib.Kernel.Runtime`, `GameLib.Kernel.ServiceGraph`, `GameLib.Kernel.ScopeGraph` |
| `GameLib.Kernel.Command` | `GameLib.Foundation`, `GameLib.Kernel.Diagnostics`, `GameLib.Kernel.Abstractions`, `GameLib.Kernel.Runtime`, `GameLib.Kernel.ServiceGraph`, `GameLib.Kernel.ScopeGraph`, `GameLib.Kernel.RuntimeQuery` |
| `GameLib.Kernel.Value` | `GameLib.Foundation`, `GameLib.Kernel.Diagnostics`, `GameLib.Kernel.Abstractions`, `GameLib.Kernel.Runtime`, `GameLib.Kernel.ScopeGraph`, `GameLib.Kernel.RuntimeQuery` |
| `GameLib.Kernel.Value.Scalar` | `GameLib.Kernel.Value`, `GameLib.Kernel.ScopeGraph`, `GameLib.Kernel.Diagnostics` |
| `GameLib.Kernel.Value.Dynamic` | `GameLib.Kernel.Value`, `GameLib.Kernel.RuntimeQuery`, `GameLib.Kernel.Diagnostics` |
| `GameLib.Kernel.RuntimeQuery` | `GameLib.Foundation`, `GameLib.Kernel.Diagnostics`, `GameLib.Kernel.Abstractions`, `GameLib.Kernel.ScopeGraph` |
| `GameLib.Kernel.Unity` | `GameLib.Foundation.Unity`, `GameLib.Kernel.Diagnostics.Unity`, `GameLib.Kernel.Runtime`, `GameLib.Kernel.ScopeGraph` |
| `GameLib.Kernel.Authoring` | `GameLib.Foundation.Unity`, `GameLib.Kernel.Abstractions`, `GameLib.Kernel.Contributions`, `GameLib.Kernel.Diagnostics` |
| `GameLib.Kernel.Authoring.Editor` | `GameLib.Foundation`, `GameLib.Foundation.Unity`, `GameLib.Foundation.Editor`, `GameLib.Kernel.Diagnostics`, `GameLib.Kernel.Diagnostics.Editor`, `GameLib.Kernel.Abstractions`, `GameLib.Kernel.Authoring`, `GameLib.Kernel.Contributions`, `GameLib.Kernel.IR`, `GameLib.Kernel.Validation`, `GameLib.Kernel.Generation` |
| `GameLib.Features.*` | kernel public APIs plus the specific Unity packages needed by the feature |
| `GameLib.Legacy.*` | public kernel APIs required for quarantine bridges |
| `GameLib.Tests.*` | target production assemblies plus Unity Test Framework and other test-only dependencies |

---

## Forbidden Dependency Matrix

The following relationships are explicitly disallowed.

| Assembly | Must Not Reference |
|---|---|
| `GameLib.Foundation` | UnityEngine, UnityEditor, kernel subsystem assemblies, feature assemblies, legacy assemblies |
| `GameLib.Kernel.Diagnostics` | direct Unity Debug output APIs, feature assemblies, legacy assemblies |
| `GameLib.Kernel.Diagnostics.Unity` | feature assemblies, legacy assemblies |
| `GameLib.Kernel.Abstractions` | runtime implementations, UnityEditor, feature assemblies, legacy assemblies |
| `GameLib.Kernel.IR` | runtime implementations, UnityEditor, feature assemblies, legacy assemblies |
| `GameLib.Kernel.Contributions` | runtime builder mutation APIs, scene search APIs, feature assemblies, legacy assemblies |
| `GameLib.Kernel.Validation` | runtime mutation, feature assemblies, legacy assemblies |
| `GameLib.Kernel.Generation` | scene search, runtime fallback repair, feature assemblies, legacy assemblies |
| `GameLib.Kernel.Boot` | feature assemblies, legacy fallback repair, scene discovery helpers |
| `GameLib.Kernel.Runtime` | Editor-only APIs, tests, feature assemblies, legacy assemblies |
| `GameLib.Kernel.ServiceGraph` | command executor discovery, feature assemblies, legacy assemblies |
| `GameLib.Kernel.ScopeGraph` | `Transform.parent` as truth, feature assemblies, legacy assemblies |
| `GameLib.Kernel.Lifecycle` | interface scanning, feature assemblies, legacy assemblies |
| `GameLib.Kernel.Command` | giant installer patterns, string dispatch, feature assemblies, legacy assemblies |
| `GameLib.Kernel.Value` | command concrete implementations, stable-key runtime fallback, feature assemblies, legacy assemblies |
| `GameLib.Kernel.Unity` | feature internals, legacy internals |
| `GameLib.Kernel.Authoring` | runtime mutation APIs |
| `GameLib.Kernel.Authoring.Editor` | runtime fallback repair |
| `GameLib.Features.*` | kernel core internals and legacy internals |
| `GameLib.Legacy.*` | any direct dependency from `GameLib.Kernel.*` |
| `GameLib.Tests.*` | production assemblies depending on tests |

Any dependency not listed in the allowed matrix should be treated as suspect and reviewed before it is added.

---

## Circular Dependency Avoidance Policy

Keep the cross-layer interface surface small.

Rules:

- Put tiny boundary interfaces in `GameLib.Kernel.Abstractions` when two runtime layers need to cooperate without knowing each other's concrete types.
- Prefer plan and handle references over concrete service references.
- Keep `Command` talking to value access contracts, not to `Value` concrete implementations.
- Keep `Lifecycle` talking to lifecycle target contracts, not to command concrete implementations.
- Keep `ServiceGraph` and `ScopeGraph` aligned through handles and plans, not through ownership hacks.

If a boundary interface grows too large, split it before it becomes a hidden cross-assembly coupling sink.

---

## Test Assembly Policy

Tests are consumers of the production graph.

Recommended test assemblies:

- `GameLib.Tests.Common`
- `GameLib.Tests.Foundation`
- `GameLib.Tests.Diagnostics`
- `GameLib.Tests.IR`
- `GameLib.Tests.Validation`
- `GameLib.Tests.Generation`
- `GameLib.Tests.Boot`
- `GameLib.Tests.ServiceGraph`
- `GameLib.Tests.ScopeGraph`
- `GameLib.Tests.Lifecycle`
- `GameLib.Tests.Command`
- `GameLib.Tests.Value`
- `GameLib.Tests.Authoring.Editor`
- `GameLib.Tests.Legacy`
- `GameLib.Tests.Performance`
- `GameLib.Tests.Integration.PlayMode`

Rules:

- Test assemblies may reference the production assemblies they validate.
- Production assemblies must never reference test assemblies.
- Shared helpers should live in test-only utility assemblies, not in runtime code.

---

## Migration Order

Introduce the assembly split in a controlled order.

1. Inventory current compile edges and locate monolithic GameLib code paths.
2. Create the lowest-volatility assemblies first: Foundation, Diagnostics, and Abstractions.
3. Move IR, Contributions, Validation, Generation, and Boot onto those base assemblies.
4. Split runtime core and runtime subsystems.
5. Add Unity bridge and authoring assemblies.
6. Quarantine legacy into explicit adapter assemblies.
7. Add test assemblies and compile-boundary tests.
8. Only then split feature modules and any generated code surface that is stable enough to deserve its own assembly.

Do not begin with feature assemblies.
Do not begin with legacy adapters.
Do not begin with a broad runtime split that leaves the core graph monolithic.

---

## Static Rule Enforcement

The assembly graph should be enforced by a combination of asmdef configuration and executable tests.

Minimum checks:

- core asmdefs must have `noEngineReferences: true` unless they are explicit Unity bridge or Editor assemblies
- `UnityEditor` references must be restricted to Editor assemblies
- `Debug.Log*` calls must be restricted to `GameLib.Kernel.Diagnostics.Unity`
- `VContainer` references must remain in the legacy quarantine boundary
- `Unity Test Framework` references must remain in `GameLib.Tests.*`
- `Feature` assemblies must not be allowed to back-reference kernel internals
- `Legacy` assemblies must not be referenced by `GameLib.Kernel.*`

Static rule tests should compare the actual asmdef graph against the allowed dependency matrix.

---

## Failure Policy

A compile boundary violation is an architecture failure, not a harmless refactor inconvenience.

If a new reference would violate the matrix:

1. reject the change
2. identify the boundary interface that was missing
3. move the shared contract downward
4. or move the implementation upward into the correct leaf assembly

If a package dependency cannot be isolated, the design is wrong and the reference should not be added silently.

If the build graph is not explicit, the kernel cannot remain explicit.

---

## Required Test Cases

| Test Case | Purpose | Verification |
|---|---|---|
| TC-17-01 | Confirm the kernel core stays Unity-free by default. | Foundation, Abstractions, IR, Contributions, Validation, Generation, Boot, and Runtime asmdefs must not reference UnityEngine or UnityEditor directly. |
| TC-17-02 | Confirm direct Unity Debug output is isolated. | Only `GameLib.Kernel.Diagnostics.Unity` may contain direct `Debug.Log*` calls for kernel diagnostics. |
| TC-17-03 | Confirm legacy remains quarantined. | `GameLib.Kernel.*` asmdefs must not reference `GameLib.Legacy.*`; legacy asmdefs may only reference public kernel APIs. |
| TC-17-04 | Confirm external package use is layer-specific. | UniTask, InputSystem, URP, uGUI, VContainer, Odin, and Unity Test Framework references must appear only in the permitted layers. |
| TC-17-05 | Confirm test assemblies are consumers only. | `GameLib.Tests.*` asmdefs may reference production assemblies, but no production asmdef may reference a test asmdef. |

---

## Acceptance Criteria

This specification is complete when all of the following are true:

- The assembly naming convention is fixed and the kernel layers have assigned asmdef names.
- The allowed and forbidden dependency matrices are explicit enough to be used in review and tests.
- Core assemblies can be created with `noEngineReferences: true` where appropriate.
- Diagnostics Unity output is isolated to one sink assembly.
- Runtime, editor, feature, legacy, and test boundaries are separated in compile-time references.
- The migration order is specific enough to implement incrementally without broad monolith churn.
- The doc test suite can detect when the README or dependency matrix forgets the new specification.

The target is not just faster compilation.
The target is a compile graph that makes the v2 architecture harder to violate.
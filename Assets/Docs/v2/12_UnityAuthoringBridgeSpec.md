# Unity Authoring Bridge Specification

## Document Status

- Document ID: 12_UnityAuthoringBridgeSpec
- Status: Draft
- Role: defines the Unity authoring bridge, authoring identity, source location, extraction, normalization, validation, and direct-play authoring boundary for Kernel v2
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
  - [10_2_DynamicValueEvaluationSpec.md](10_2_DynamicValueEvaluationSpec.md)
  - [11_DebugMapAndDiagnosticsSpec.md](11_DebugMapAndDiagnosticsSpec.md)
- Provides foundation for:
  - [13_LegacyCompatBoundarySpec.md](13_LegacyCompatBoundarySpec.md)
  - [14_PerformanceBudgetAndRuntimeRulesSpec.md](14_PerformanceBudgetAndRuntimeRulesSpec.md)
  - [15_TestAndValidationSpec.md](15_TestAndValidationSpec.md)

### Revision Note

This revision defines 12 as the boundary where Unity authoring stops being runtime composition authority.

It fixes the target rule that MonoBehaviour and ScriptableObject may provide declaration data, but they must not directly build runtime service graphs, command catalogs, lifecycle tables, or value stores.

It also defines ScopeAuthoringId ownership, prefab and scene authoring identity rules, direct-play verified-input policy, and the migration path away from `IFeatureInstaller`-style runtime builder mutation.

---

## Ownership

This specification owns:

- Unity Scene, Prefab, Prefab Variant, Nested Prefab, and ScriptableObject authoring boundary for the kernel pipeline
- explicit authoring root concept for extraction and direct play
- `ScopeAuthoringId` generation, stability, duplication, and regeneration policy
- `ScopeAuthoringLink`, `KernelRoot`, and equivalent authoring component responsibility boundaries
- authoring-side `SourceLocation` and `UnityObjectLink` requirements
- authoring component classification
- deterministic contribution extraction from Unity authoring sources
- authoring normalization and local validation rules before KernelIR handoff
- Transform hierarchy boundary for authoring versus runtime truth
- Unity object reference normalization boundary
- DynamicValue authoring boundary in Unity-facing components
- `OnValidate`, `Reset`, and editor utility policy for kernel-facing authoring components
- generated artifact reference policy from Unity authoring and direct-play entry points
- runtime Unity linkage metadata boundary
- authoring diagnostics and failure boundary requirements
- legacy installer migration policy for Unity authoring components

This specification does not own:

- runtime service graph implementation
- runtime scope graph implementation
- runtime scope handle storage
- command execution behavior
- value storage layout
- DynamicValue evaluation runtime implementation
- final editor window or inspector UI layout
- final Odin group layout or custom drawer implementation
- generated artifact binary container format

12 owns the authoring bridge.
It must not re-own runtime semantics already owned by 06 through 11.

---

## Purpose

This specification defines how Unity authoring data becomes verified declaration input for the plan-first kernel.

Core statements:

```text
Unity authoring describes runtime structure.
It does not build runtime structure.

Unity objects are authoring sources.
Verified plans are runtime composition authority.

MonoBehaviour and ScriptableObject may provide declaration data.
They must not directly build runtime service graphs, command catalogs, lifecycle tables, or value stores.
```

The bridge exists so that Unity can remain an effective authoring environment without becoming a fallback runtime composition mechanism.

This specification exists to prevent the following regressions:

- MonoBehaviour mutating runtime builders in target-kernel paths
- runtime feature discovery through `GetComponentsInChildren` or similar traversal
- scope ownership inferred through `Transform.parent`
- prefab duplication silently duplicating authored kernel identity
- `OnValidate` or `Reset` silently changing kernel semantics
- direct play entering runtime boot with stale or incomplete artifacts
- required Unity references surviving into runtime plans as unresolved fallback work

---

## Scope

This specification defines:

- authoring-source categories
- authoring authority versus runtime authority
- authoring-side identity model
- source location and Unity object linkage metadata
- authoring component roles and classification
- extraction pipeline from Unity objects to contribution data
- normalization and validation pipeline from Unity authoring to KernelIR-ready input
- prefab, variant, nested-prefab, and scene identity policy
- ScriptableObject authoring policy
- MonoBehaviour authoring policy
- direct-play authoring flow
- generated artifact reference policy from Unity-facing tooling
- runtime Unity linkage boundary
- diagnostics and failure policy for Unity authoring
- performance and editor cost policy
- legacy migration policy
- forbidden patterns
- required tests

---

## Non-Goals

This specification does not define:

- the final editor window implementation for authoring diagnostics
- the final Odin Inspector field layout
- the final runtime service cache or resolver implementation
- the final runtime scope handle layout
- the final command execution algorithm
- the final value store storage layout
- the final scene transition algorithm
- the final debug UI appearance
- the final generated asset serialization container

This specification must not become a generic Unity editor-style guide.
It defines the kernel-facing authoring contract.

---

## Relationship to Other Specs

| Spec | Relationship |
|---|---|
| [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md) | Defines the root rule that Unity authoring is not runtime composition authority and delegates exact bridge behavior to 12. |
| [01_KernelIRSpec.md](01_KernelIRSpec.md) | Defines `ScopeAuthoringId`, `SourceLocationId`, and normalized IR identity domains that 12 must feed without ambiguity. |
| [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md) | Defines the constrained declaration system that 12 must produce inputs for. |
| [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md) | Generates verified artifacts from normalized inputs produced through the 12 boundary. |
| [04_DependencyValidationSpec.md](04_DependencyValidationSpec.md) | Validates authoring-derived dependency declarations before runtime may trust them. |
| [05_BootManifestAndProfileSpec.md](05_BootManifestAndProfileSpec.md) | Defines boot acceptance and allows authoring assets or references to feed manifest production, but not runtime boot discovery. |
| [06_ServiceGraphRuntimeSpec.md](06_ServiceGraphRuntimeSpec.md) | Owns runtime service semantics; 12 owns authoring-side service contribution sources and Unity linkage inputs. |
| [07_ScopeGraphRuntimeSpec.md](07_ScopeGraphRuntimeSpec.md) | Owns runtime scope semantics; 12 owns authored scope identity, source traceability, and Unity object linkage input. |
| [08_LifecyclePlanSpec.md](08_LifecyclePlanSpec.md) | Owns runtime lifecycle ordering; 12 owns authoring-side lifecycle contribution sources. |
| [09_CommandCatalogRuntimeSpec.md](09_CommandCatalogRuntimeSpec.md) | Owns runtime command semantics; 12 owns command authoring objects, authoring keys, and payload authoring normalization. |
| [10_ValueSchemaAndStoreSpec.md](10_ValueSchemaAndStoreSpec.md) | Owns runtime value state; 12 owns stable-key-facing authoring input and value-init authoring sources. |
| [10_2_DynamicValueEvaluationSpec.md](10_2_DynamicValueEvaluationSpec.md) | Owns dynamic evaluation runtime semantics; 12 owns Unity-facing DynamicValue authoring inputs and their normalization boundary. |
| [11_DebugMapAndDiagnosticsSpec.md](11_DebugMapAndDiagnosticsSpec.md) | Owns the shared diagnostics substrate; 12 defines the authoring-side source fields and failure contexts emitted through that substrate. |
| [13_LegacyCompatBoundarySpec.md](13_LegacyCompatBoundarySpec.md) | Will define the only legal boundary where legacy authoring adapters may remain visible. |
| [14_PerformanceBudgetAndRuntimeRulesSpec.md](14_PerformanceBudgetAndRuntimeRulesSpec.md) | Will budget extraction, normalization, direct-play preparation, and authoring diagnostics costs defined here. |
| [15_TestAndValidationSpec.md](15_TestAndValidationSpec.md) | Implements executable tests for authoring extraction determinism, direct-play input correctness, identity stability, and CI coverage using the rules defined here. It does not redefine authoring roles or extraction semantics. |

12 is the authoring-entry contract for the kernel pipeline.
It must not leave Unity-facing identity, extraction, or direct-play rules ownerless.

---

## Assembly Definition and Compile Boundary Expectations

Unity authoring bridge intentionally spans multiple assemblies:

- `GameLib.Kernel.Authoring`
- `GameLib.Kernel.Authoring.Editor`
- `GameLib.Kernel.Unity`
- `GameLib.Kernel.Boot.Unity` where boot-entry glue is required

Detailed dependency matrices remain owned by [17_AssemblyDefinitionAndCompileBoundarySpec.md](17_AssemblyDefinitionAndCompileBoundarySpec.md).

Required compile-boundary rules for 12:

- serialized declaration components and authoring-side data contracts belong in `GameLib.Kernel.Authoring`
- extraction, normalization, direct-play preparation, asset refresh, and editor validation belong in `GameLib.Kernel.Authoring.Editor`
- runtime MonoBehaviour bridge code belongs in `GameLib.Kernel.Unity`, not in authoring editor assemblies
- authoring assemblies must not mutate runtime builders or pull feature internals into kernel core assemblies

If Unity authoring logic cannot be placed into an authoring, editor, or Unity bridge assembly without back-referencing kernel internals or legacy fallback code, the 12 boundary has been violated.

---

## Current Unity Authoring Debt Observations

この節は現行コードベースの Unity authoring 負債の観測結果をまとめる。
ここは target policy ではなく、移行元の整理である。

### Observation Traceability

| Observation | Evidence Type | Target Pressure |
|---|---|---|
| Feature installers are discovered through runtime hierarchy traversal. | Source | explicit authoring roots and extraction pipeline |
| Scope ownership is inferred by walking `Transform.parent`. | Source | explicit authored scope relation and no nearest-scope rule |
| `IFeatureInstaller.InstallFeature` mutates the runtime builder directly. | Source | declaration-only authoring components |
| Identity components repair kind and id by heuristic editor logic. | Source | explicit `ScopeAuthoringId` policy and authoring diagnostics |
| MonoBehaviours mix authoring data, DynamicValue preview, defaults, and runtime registration. | Source | authoring/runtime separation and DynamicValue authoring boundary |
| Prefab-spawn paths assume runtime scope components directly on prefab assets. | Source | prefab template versus instance policy and explicit binding requirements |

### Representative Anchors

- [ScopeFeatureInstallerUtility.cs](../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs) - `GetComponentsInChildren` discovery and nearest-scope lookup through `Transform.parent`
- [BaseLifetimeScope.cs](../../GameLib/Script/Common/LTS/Core/BaseLifetimeScope.cs) - `IFeatureInstaller` runtime builder mutation contract
- [RuntimeLifetimeScope.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs) - owned-installer caching and runtime execution of installer mutation
- [LTSIdentityMB.cs](../../GameLib/Script/Common/LTS/Identity/MB/LTSIdentityMB.cs) - guessed scope kind, id repair, runtime registration, and dynamic registry opt-in
- [TooltipChannelHubMB.cs](../../GameLib/Script/Project/UI/Core/Tooltip/TooltipChannelHubMB.cs) - DynamicValue authoring, editor inference, root override, and runtime registration mixed in one MB
- [MeshChannelHubMB.cs](../../GameLib/Script/Project/Scene/Channels/Mesh/MeshChannelHubMB.cs) - serialized channel entries directly used for service and lifecycle registration
- [EntityLifetimeScopeSpawnerMB.cs](../../GameLib/Script/Project/Scene/Field/Entity/Spawner/EntityLifetimeScopeSpawnerMB.cs) - prefab-scope assumptions and build-callback-driven service instantiation

### Current Gaps

The current project still exposes the following gaps that 12 must close:

- Unity authoring can still act as runtime composition logic instead of declaration input
- scope identity and ownership are not yet expressed as one explicit authoring contract
- direct play can still be tempted toward runtime repair instead of verified artifact generation
- authoring-side DynamicValue usage is not yet uniformly normalized into explicit evaluation plans
- prefab duplication and scene duplication do not yet have one explicit kernel-identity policy
- editor convenience hooks can still drift into kernel-semantics mutation

---

## Unity Authoring Bridge Definition

`UnityAuthoringBridge` converts Unity authoring data into kernel contribution input.

It is an editor/build-time bridge, not a runtime service locator.

Core definition:

```text
UnityAuthoringBridge converts Unity authoring data into ModuleContributionData and KernelIR-ready normalized inputs.

It owns extraction, source location attachment, authoring identity resolution, authoring diagnostics, and source-to-artifact traceability.

UnityAuthoringBridge must not execute runtime composition.
```

The bridge may traverse explicit authoring roots in editor/build-time contexts.
It must not reuse runtime ownership inference, runtime service resolution, or runtime fallback logic to understand those roots.

---

## Authoring Source Categories

Explanatory model:

```csharp
public enum UnityAuthoringSourceKind
{
    SceneObject = 10,
    PrefabAsset = 20,
    PrefabInstance = 30,
    PrefabVariant = 40,
    ScriptableObjectAsset = 50,
    GeneratedAsset = 60,
    CodeDefinedModule = 70,
    LegacyBridge = 90,
}
```

Authoring source is the origin of declaration data.

It is not:

- runtime owner
- runtime identity
- runtime scope handle
- generated artifact

Required source-category meanings:

- `SceneObject`: a scene-resident authored object that provides declaration input
- `PrefabAsset`: a prefab template that declares authored structure or reusable config
- `PrefabInstance`: a scene or prefab-hosted placement of a prefab template
- `PrefabVariant`: a derived prefab definition with traceable override source
- `ScriptableObjectAsset`: an authored asset that contributes module, registry, profile, or reusable definition data
- `GeneratedAsset`: an inspection or boot reference target, not authoring truth
- `CodeDefinedModule`: a code-backed module declaration source when explicitly allowed by lower specs
- `LegacyBridge`: migration-only authoring surface kept visible through 13

---

## Authoring vs Runtime Authority

Authoring data may describe intended runtime structure.
Runtime structure is executed only from verified plans.

Allowed:

- MonoBehaviour or ScriptableObject provides declaration data
- MonoBehaviour or ScriptableObject provides source location
- MonoBehaviour or ScriptableObject provides Unity object link metadata
- editor tools extract contributions from Unity authoring sources

Forbidden:

- MonoBehaviour registers runtime services directly
- MonoBehaviour adds lifecycle steps at runtime
- ScriptableObject mutates runtime service graphs directly
- authoring extraction resolves live runtime services
- authoring extraction discovers scope ownership at runtime
- authoring object becomes runtime composition authority because play mode was entered

If a MonoBehaviour can call `builder.Register` in the target architecture, the architecture has regressed.

---

## SourceLocation and UnityObjectLink Model

Every Unity authoring input that can produce runtime behavior must carry source traceability.

Explanatory `SourceLocation` model:

```csharp
public sealed class UnitySourceLocation
{
    public UnityAuthoringSourceKind Kind;
    public string AssetGuid;
    public string AssetPath;
    public long LocalFileId;
    public string ScenePath;
    public string GameObjectPath;
    public string ComponentType;
    public string PropertyPath;
}
```

Explanatory `UnityObjectLink` model:

```csharp
public sealed class UnityObjectLink
{
    public UnityObjectLinkKind Kind;
    public string SourceGuid;
    public long LocalFileId;
    public int RuntimeInstanceId;
    public string DebugName;
}
```

Rules:

- `SourceLocation` is for authoring traceability.
- `UnityObjectLink` is for runtime debug, bridge, and selection metadata.
- neither `SourceLocation` nor `UnityObjectLink` is kernel identity.
- runtime instance id may appear only as runtime debug metadata and must never become stable kernel identity.
- scene path, prefab path, GameObject path, and property path are traceability data only.

Required provenance fields for kernel-facing authoring diagnostics are defined in the diagnostics section of this document and must remain compatible with 11.

---

## ScopeAuthoringId Policy

`ScopeAuthoringId` identifies an authored scope definition.

It must be:

- stable across editor sessions
- source-backed
- duplicate-detectable
- preserved through safe edits
- regenerated only through explicit editor action or validated duplication policy

Target ownership rule:

```text
ScopeAuthoringLink owns ScopeAuthoringId for target-kernel authoring paths.
Legacy identity components may carry migration metadata, but they must not remain the final owner of authored scope identity.
```

`ScopeAuthoringId` must not be derived from:

- GameObject name
- Transform sibling index
- runtime instance id
- scene traversal order
- component enumeration order

Generation and regeneration rules:

- a new authored scope receives a `ScopeAuthoringId` through explicit authoring tool flow when the authored scope definition is created
- moving a GameObject, renaming a GameObject, reordering siblings, or reserializing components must not regenerate `ScopeAuthoringId`
- duplication policy must either regenerate identity through an explicit duplication path or fail validation until the collision is resolved
- `OnValidate` must not silently mint a new `ScopeAuthoringId` as semantic repair

Duplicate `ScopeAuthoringId` is an authoring error unless duplication policy explicitly creates a new authored definition and records the regeneration outcome.

---

## Prefab / Variant / Nested Prefab Policy

The bridge must distinguish:

- prefab template identity
- prefab instance placement
- prefab variant identity
- nested prefab source identity
- scene override source

Rules:

- a prefab template that declares a scope definition owns its own `ScopeAuthoringId`
- a scene instance of a prefab template does not silently become a new authored definition merely because it exists in a scene
- prefab variant that overrides authored kernel structure becomes a distinct authoring source and must preserve traceability to its base prefab plus its own override source
- nested prefabs retain their own source identity and must not be flattened into parent identity by hierarchy inference
- scene overrides must preserve both base source trace and override source trace where relevant

Prefab duplication policy:

- duplicating a prefab asset must not silently duplicate authored kernel identity
- the bridge must either regenerate authored identity for the duplicated definition or reject the duplicate until explicit user action resolves it
- unpacking a prefab, copying a component, or moving authored content between scenes and prefabs must preserve source traceability and trigger duplicate detection where identity conflicts arise

Minimum required handled cases:

- prefab asset duplicated in the Project window
- prefab instance duplicated in a scene
- prefab variant created from a base prefab
- nested prefab overridden
- component copied and pasted
- scene object moved into a prefab or out of a prefab
- prefab unpacked

Silent collision is forbidden.

---

## Scene Authoring Policy

Scene authoring may define:

- scene-local scope declarations
- service hubs and adapters as declaration sources
- value-init authoring data
- command blocks
- runtime-query source data
- Unity object links

Scene authoring must not be used as runtime discovery input.

Rules:

- scenes must expose explicit authoring roots such as `KernelRoot` or equivalent scene-entry authoring marker
- scene object enumeration order must not affect generated KernelIR
- scene object names and hierarchy positions are not stable kernel identity
- entering play mode does not authorize runtime scene search to repair missing authoring declarations

`KernelRoot` responsibilities in target paths:

- declare that a scene or authoring set participates in kernel extraction
- reference boot-relevant authoring inputs where allowed by 05
- anchor bounded extraction roots for scene authoring
- provide authoring diagnostics entry points

`KernelRoot` is not a runtime service locator.

---

## ScriptableObject Authoring Policy

ScriptableObject assets may define:

- module definitions
- registries
- command catalogs
- value key registries
- profiles
- authoring presets
- channel definitions
- reusable configs

Rules:

- ScriptableObject assets are declaration sources, not mutable runtime state containers, unless a lower spec explicitly defines a separate runtime-state asset and removes it from authoring truth
- required authoring assets must be referenced by verified inputs
- runtime `Resources.Load` fallback for required kernel assets is forbidden
- duplicated authored identity inside ScriptableObject assets must trigger the same duplicate-detection and traceability policy as scene and prefab sources

ScriptableObject assets may be convenient authoring surfaces.
They must not become hidden runtime registries that repair missing verified inputs.

---

## MonoBehaviour Authoring Policy

MonoBehaviour authoring components may provide serialized declaration data.

They must not perform runtime registration in target-kernel paths.

Target MonoBehaviour authoring roles include:

- `KernelRoot`
- `ScopeAuthoringLink`
- `FeatureAuthoring<TSpec>`
- `ServiceHubAuthoring<TSpec>`
- `ValueInitAuthoring`
- `CommandBlockAuthoring`
- `RuntimeQueryAuthoring`
- `UnityObjectLinkAuthoring`

Representative migration examples:

```text
MeshChannelHubMB:
  old: IFeatureInstaller + builder.Register + lifecycle and tick enrollment
  new: MeshChannelHubAuthoring
       -> ServiceContribution
       -> LifecycleContribution
       -> channel-definition contribution

TooltipChannelHubMB:
  old: MonoBehaviour owns DynamicValue, root override, editor inference, and runtime registration
  new: TooltipChannelHubAuthoring
       -> ServiceContribution
       -> LifecycleContribution
       -> DynamicEvaluationContribution when runtime context is required
       -> RuntimeQueryContribution or UnityObjectLink metadata when needed
```

Legacy `IFeatureInstaller`-implementing components may remain only through 13’s migration boundary.
They are not valid target authoring components.

---

## Authoring Component Classification

Explanatory model:

```csharp
public enum AuthoringComponentKind
{
    Declaration = 10,
    Link = 20,
    Bridge = 30,
    ViewBinding = 40,
    DebugOnly = 50,
    LegacyAdapter = 90,
}
```

Meaning:

- `Declaration`: input to `ModuleContributionData` or KernelIR normalization
- `Link`: Unity-to-runtime traceability metadata
- `Bridge`: bounded Unity event or object boundary that feeds verified runtime paths
- `ViewBinding`: output binding between runtime data and Unity view objects
- `DebugOnly`: editor or debug visualization only
- `LegacyAdapter`: migration-only bridge controlled by 13

Rules:

- component kind must be explicit
- one component should not mix declaration, runtime bridge, lifecycle semantics, and debug behavior unless a lower spec explicitly approves that combination
- view binding and event bridging are not authoring truth
- declaration components must be valid without play mode state

---

## Contribution Extraction Pipeline

Unity authoring extraction pipeline:

1. collect explicit authoring roots
2. read authoring components and assets under those roots
3. attach `SourceLocation`
4. resolve stable authoring identities
5. normalize Unity references
6. emit `ModuleContributionData`
7. validate local authoring shape
8. hand off to KernelIR normalization

Extraction rules:

- extraction is editor/build-time
- extraction must be deterministic
- extraction must not depend on live runtime service state
- extraction may traverse explicit authoring roots, but traversal order must be normalized before any hash-relevant or ordering-relevant output is emitted
- extraction must not call runtime `ServiceGraph`, `ScopeGraph`, `CommandCatalog`, or `ValueStore`

Allowed bounded traversal:

- enumerating components under an explicit `KernelRoot` in editor/build-time context
- traversing prefab contents as serialized authoring data
- resolving referenced ScriptableObject inputs from verified authoring references

Forbidden extraction behavior:

- runtime nearest-scope ownership inference
- scene-wide blind search as kernel truth
- consulting play mode runtime containers to understand authoring
- mutating runtime builders while extracting

---

## Normalization and Validation Pipeline

Raw Unity authoring data must be normalized before entering KernelIR.

Normalization must resolve:

- authoring component references
- `ScopeAuthoringId`
- module ownership
- `SourceLocation`
- profile availability
- asset references
- prefab source metadata
- scene override metadata
- command authoring keys
- stable value keys used only on the authoring side

Required pipeline direction:

```text
Unity Authoring Source
  -> Extraction
  -> ModuleContributionData
  -> Normalization
  -> KernelIR
  -> Validation
  -> Verified artifacts
```

Rules:

- unresolved Unity references must not be carried into runtime plans as fallback work
- unresolved identity collisions must fail before runtime boot
- normalization must not invent runtime identities that were absent from authoring input
- play mode state, live runtime handles, and runtime-created fallback identities are not valid normalization inputs

---

## Transform Hierarchy Boundary

Transform hierarchy may help editor authoring, visual organization, and default suggestion.

Transform hierarchy must not be runtime kernel truth.

Allowed:

- editor-only suggestion for default parent or grouping
- editor validation display
- GameObject path for diagnostics and source traceability
- prefab nesting traceability

Forbidden:

- runtime parent inference
- runtime nearest-scope search
- feature ownership detection by `Transform` ancestry
- plan generation depending on sibling order unless that order is explicitly authored and normalized as data

Representative forbidden legacy patterns include:

- `ScopeFeatureInstallerUtility.TryGetNearestScopeNode(...)`
- `GetComponentsInChildren<IFeatureInstaller>(...)` used as runtime composition logic
- `Transform.parent` traversal used to discover scope ownership or service ownership

12 does not forbid hierarchy-aware editor UX.
It forbids hierarchy-derived runtime truth.

---

## Unity Object Reference Boundary

Unity object references in authoring must normalize into one of the following outcomes:

- `SourceLocation`
- `UnityObjectLink`
- `AssetReference`
- `RuntimeBindingRequirement`
- `AuthoringError`

Rules:

- destroyed-object and fake-null behavior must be explicit
- Unity fake-null must not silently erase required authoring references
- runtime object references must not become stable generated identity
- authoring reference normalization must preserve whether a reference is asset-backed, scene-backed, runtime-link-only, or invalid

If a required Unity reference cannot be normalized, generation or validation must fail.
Runtime must not repair it through discovery.

---

## DynamicValue Authoring Boundary

DynamicValue inside Unity authoring is allowed only as authoring data.

Rules:

- if a `DynamicValue` is context-free and editor-only, the bridge may evaluate it only for preview or validation assistance
- if a `DynamicValue` requires runtime context, it must produce `DynamicEvaluationContribution` or `ReactiveEvaluationContribution`
- `DynamicValue` must not be evaluated during contribution extraction as runtime truth
- editor preview result must not suppress diagnostics or replace declared runtime evaluation semantics

Representative policy:

```text
DynamicValue in Unity authoring is declaration input.
Runtime-context-dependent DynamicValue becomes an evaluation contribution.
It does not remain a hidden runtime getter path attached to a MonoBehaviour.
```

This section must remain consistent with 10-2.

---

## OnValidate / Reset / Editor Utility Policy

`OnValidate`, `Reset`, and editor utilities may improve authoring usability.

Allowed:

- assign default references
- normalize display-only fields
- warn about invalid state
- generate missing authoring id through approved explicit editor utility
- mark asset dirty when explicit and editor-safe

Forbidden:

- silently changing runtime semantics
- repairing missing required dependencies without diagnostics
- performing registry fallback
- performing scene-wide discovery as source of truth
- generating identities without duplicate detection
- changing `ScopeAuthoringId` because a Transform parent changed

Current heuristic repair patterns such as type-guessing scope kind or inferring UI-space defaults may exist as migration evidence.
They are not the final target contract.

---

## Generated Artifact Reference Policy

Unity authoring may reference generated artifacts only as inspection targets or boot references.

Generated artifacts are not authoring truth.

Generated artifact references must include enough compatibility data to prove what they point at.

Minimum fields:

- `ArtifactSetId`
- `KernelIRHash`
- `ProfileHash`
- `RegistryHash`
- `GeneratorVersion`

Rules:

- manually editing generated artifacts as authoring input is forbidden
- generated artifacts referenced from Unity authoring must be treated as derived data
- stale generated references must fail validation or direct-play boot

---

## Direct Play / Editor Boot Policy

Editor direct play must still use verified inputs.

Allowed direct-play flow:

1. detect dirty authoring sources
2. run extraction
3. run normalization
4. run validation
5. generate temporary or persistent verified artifact set
6. boot using BootManifest and profile policy

Forbidden:

- runtime fallback because the user pressed Play
- `FindObjectsByType` repair for missing required authoring data
- `Resources.Load` fallback for required kernel assets
- booting from stale artifacts when dirty authoring has not been reconciled

If direct play cannot prove compatible verified input, boot must be blocked.

---

## Runtime Unity Linkage Policy

Runtime Unity linkage connects verified runtime handles to Unity objects.

It is used for:

- view binding
- diagnostics
- editor selection
- debug overlay
- Unity event bridge
- object lifecycle observation

It is not used for:

- runtime identity generation
- service discovery
- scope parent inference
- command target fallback
- runtime repair of missing authoring declarations

`UnityObjectLink` is metadata.
It is not kernel truth.

---

## Diagnostics and DebugMap Requirements

11 owns the shared diagnostics substrate.
12 defines the minimum authoring-side provenance and failure contexts that must feed it.

Unity authoring diagnostics must include:

- authoring source kind
- asset GUID
- asset path
- local file id
- scene path when relevant
- GameObject path when relevant
- component type
- property path when relevant
- module id if available
- contribution kind
- profile when relevant
- prefab base or override source when relevant

Representative stable codes:

- `UNITY_AUTHORING_SOURCE_MISSING`
- `UNITY_AUTHORING_ID_DUPLICATE`
- `UNITY_AUTHORING_ID_UNSTABLE`
- `UNITY_PREFAB_ID_COLLISION`
- `UNITY_PREFAB_VARIANT_OVERRIDE_INVALID`
- `UNITY_TRANSFORM_PARENT_INFERENCE_FORBIDDEN`
- `UNITY_RUNTIME_BUILDER_MUTATION_FORBIDDEN`
- `UNITY_DIRECT_PLAY_ARTIFACT_STALE`
- `UNITY_OBJECT_REFERENCE_UNRESOLVED`
- `UNITY_DYNAMIC_VALUE_REQUIRES_EVALUATION_PLAN`
- `UNITY_ONVALIDATE_SEMANTIC_MUTATION_FORBIDDEN`

Inspector warnings are not sufficient for required failures.
Required failures must enter the structured diagnostics pipeline.

---

## Failure Policy

Invalid Unity authoring must fail before runtime boot.

| Failure Type | Default Boundary |
|---|---|
| authoring extraction failure | generation failure |
| duplicate `ScopeAuthoringId` | validation failure |
| unresolved required reference | validation failure |
| prefab identity collision | validation failure |
| stale direct-play artifact | boot blocked |
| runtime builder mutation in target authoring component | analyzer or validation failure |
| runtime nearest-scope inference required for correctness | validation failure |

Invalid authoring must not be repaired by runtime fallback.

---

## Performance and Editor Cost Policy

Authoring extraction should be incremental when practical.

Requirements:

- deterministic ordering
- no repeated full-project scan on every minor operation
- cache authoring-source hashes or equivalent invalidation data where practical
- support explicit full regeneration
- support CI and headless extraction
- avoid reflection-heavy extraction in hot editor paths where practical

Performance optimization must not skip:

- validation
- source-location generation
- duplicate detection
- identity normalization

Suggested measurable editor cost categories for downstream budgeting defined by 14 include:

- `AuthoringBridge.CollectRoots`
- `AuthoringBridge.Extract`
- `AuthoringBridge.Normalize`
- `AuthoringBridge.Validate`
- `AuthoringBridge.PrepareDirectPlay`

---

## Legacy Migration Policy

| Legacy Pattern | Target Representation |
|---|---|
| `IFeatureInstaller.InstallFeature(builder, scope)` | authoring contribution provider or authoring component extracted into `ModuleContributionData` |
| `GetComponentsInChildren<IFeatureInstaller>` | explicit authoring root collection and deterministic extraction |
| nearest scope by `Transform.parent` | explicit `ScopeAuthoringId` and authored relation normalized into plan data |
| `builder.Register<Service>().As<...>()` from MonoBehaviour | `ServiceContribution` plus `LifecycleContribution` |
| `.As<IScopeTickHandler>()` from authoring component | `LifecycleContribution` with explicit phase |
| MonoBehaviour-owned command registration | `CommandContribution` |
| MonoBehaviour-owned blackboard init | `ValueInitContribution` |
| runtime object reference as identity | `UnityObjectLink` or `RuntimeBindingRequirement` |
| runtime `Resources.Load` required asset fallback | verified artifact or verified authoring reference |
| tooltip or mesh channel runtime installers | explicit authoring components normalized into contributions |

Legacy migration must not preserve installer-style runtime mutation as the target shape.

---

## Forbidden Patterns

The following are forbidden in target Unity authoring bridge paths:

- MonoBehaviour calling `builder.Register`
- MonoBehaviour implementing target-path runtime composition through `IFeatureInstaller`
- ScriptableObject mutating runtime service graphs
- runtime feature discovery through `GetComponentsInChildren`
- runtime scope ownership inference through `Transform.parent`
- nearest-scope search as authoring ownership rule
- authoring extraction depending on runtime `ServiceGraph`
- authoring extraction depending on live runtime state
- runtime `Resources.Load` fallback for required kernel assets
- generated artifact treated as authoring source of truth
- GameObject name as stable kernel identity
- sibling index as stable kernel identity
- runtime instance id as stable kernel identity
- `OnValidate` silently changing kernel semantics
- duplicate authored identity resolved by last-write-wins
- prefab duplication causing silent identity collision

---

## Test Case Model

Each UnityAuthoringBridge test case must define:

- Test ID
- Title
- Unity fixture type
- authoring source fixture
- operation
- expected contribution output
- expected diagnostics
- expected source location
- expected artifact impact

---

## Required Test Cases

### A. SourceLocation Tests

#### TC_UNITY_SRC_001_ComponentSourceLocationGenerated

```text
Input:
- Scene GameObject with TooltipChannelHubAuthoring

Expected:
- SourceLocation includes scene path, GameObject path, component type, and property path
```

#### TC_UNITY_SRC_002_PrefabSourceLocationGenerated

```text
Input:
- Prefab asset with MeshChannelHubAuthoring

Expected:
- SourceLocation includes asset GUID, asset path, local file id, and component type
```

### B. ScopeAuthoringId Tests

#### TC_UNITY_ID_001_NewScopeGetsStableAuthoringId

```text
Input:
- New ScopeAuthoringLink component

Operation:
- Generate authoring id

Expected:
- Stable ScopeAuthoringId assigned
- diagnostics clean
```

#### TC_UNITY_ID_002_DuplicateAuthoringIdRejected

```text
Input:
- Two scene objects with the same ScopeAuthoringId

Expected:
- Failed
- UNITY_AUTHORING_ID_DUPLICATE
```

#### TC_UNITY_ID_003_CopyPasteRequiresIdentityPolicy

```text
Input:
- Copy and paste GameObject containing ScopeAuthoringId

Expected:
- duplicate detected or regenerated through explicit policy
```

### C. Prefab Tests

#### TC_UNITY_PREFAB_001_PrefabInstanceDoesNotSilentlyDuplicateRuntimeIdentity

```text
Input:
- Prefab with authored scope instantiated twice

Expected:
- template identity and runtime instance identity are distinguished
```

#### TC_UNITY_PREFAB_002_PrefabVariantOverridePreservesSourceTrace

```text
Input:
- Prefab variant overrides channel config

Expected:
- SourceLocation can trace both base prefab and variant override
```

#### TC_UNITY_PREFAB_003_NestedPrefabIdentityCollisionRejected

```text
Input:
- Nested prefab contains duplicated authored ids

Expected:
- Failed
- UNITY_PREFAB_ID_COLLISION
```

### D. Installer Migration Tests

#### TC_UNITY_INSTALLER_001_IFeatureInstallerRejectedInTargetPath

```text
Input:
- Component implements IFeatureInstaller and calls builder.Register

Expected:
- Failed
- UNITY_RUNTIME_BUILDER_MUTATION_FORBIDDEN
```

#### TC_UNITY_INSTALLER_002_MeshChannelHubExtractsContributions

```text
Input:
- MeshChannelHubAuthoring with entries

Expected:
- ServiceContribution for hub
- LifecycleContribution for acquire, release, and tick
- channel-definition contribution
- no runtime builder mutation
```

#### TC_UNITY_INSTALLER_003_TooltipHubExtractsEvaluationPlan

```text
Input:
- TooltipChannelHubAuthoring with DynamicValue preset

Expected:
- ServiceContribution
- LifecycleContribution
- DynamicEvaluationContribution if runtime context is required
- RuntimeQueryContribution or UnityObjectLink metadata if required
```

### E. Transform Boundary Tests

#### TC_UNITY_TRANSFORM_001_TransformParentNotScopeParent

```text
Input:
- Child GameObject under parent Transform

Expected:
- scope parent is not inferred unless explicit authored relation exists
```

#### TC_UNITY_TRANSFORM_002_NearestScopeSearchForbidden

```text
Input:
- authoring extraction attempts nearest-scope search through Transform.parent

Expected:
- Failed
- UNITY_TRANSFORM_PARENT_INFERENCE_FORBIDDEN
```

### F. Direct Play Tests

#### TC_UNITY_PLAY_001_DirectPlayGeneratesVerifiedArtifacts

```text
Operation:
- Press Play with dirty authoring

Expected:
- extraction, validation, and generation run
- boot uses verified artifact set
```

#### TC_UNITY_PLAY_002_DirectPlayDoesNotUseRuntimeFallback

```text
Input:
- missing required artifact

Operation:
- Press Play

Expected:
- boot blocked
- no FindObjectsByType repair
- no Resources.Load fallback
```

### G. OnValidate Tests

#### TC_UNITY_VALIDATE_001_OnValidateMayAssignEditorDefault

```text
Input:
- Tooltip root override missing
- component can suggest local RectTransform

Expected:
- allowed if treated as editor convenience and diagnostics remain available
```

#### TC_UNITY_VALIDATE_002_OnValidateCannotSilentlyChangeKernelIdentity

```text
Operation:
- OnValidate changes ScopeAuthoringId without explicit tool action

Expected:
- Failed
- UNITY_ONVALIDATE_SEMANTIC_MUTATION_FORBIDDEN
```

### H. Extraction and Reference Tests

#### TC_UNITY_EXTRACT_001_ExtractionDeterministic

```text
Input:
- Same authoring roots and profile

Operation:
- run extraction twice

Expected:
- normalized contribution ordering is deterministic
- hash-relevant output is semantically identical
```

#### TC_UNITY_EXTRACT_002_UnresolvedReferenceRejectedBeforeKernelIR

```text
Input:
- authoring component references missing required Unity object or asset

Expected:
- Failed before runtime boot
- UNITY_OBJECT_REFERENCE_UNRESOLVED
```

---

## Acceptance Criteria

This specification is complete when it defines:

- Unity authoring bridge responsibility
- authoring source categories
- authoring versus runtime authority boundary
- `SourceLocation` and `UnityObjectLink` model
- `ScopeAuthoringId` policy
- prefab, variant, and nested-prefab policy
- scene authoring policy
- ScriptableObject authoring policy
- MonoBehaviour authoring policy
- authoring component classification
- contribution extraction pipeline
- normalization and validation pipeline
- Transform hierarchy boundary
- Unity object reference boundary
- DynamicValue authoring boundary
- `OnValidate` and `Reset` policy
- generated artifact reference policy
- direct-play and editor-boot policy
- runtime Unity linkage policy
- diagnostics and DebugMap requirements
- failure policy
- performance and editor cost policy
- legacy migration policy
- forbidden patterns
- required test cases

The specification is not complete if Unity authoring can still build runtime structure directly or if runtime fallback is still needed to repair invalid authoring.

---

## Final Position

Unity authoring describes runtime structure.
It does not build runtime structure.

The target migration is:

```text
old:
MonoBehaviour / ScriptableObject
  -> IFeatureInstaller
  -> builder.Register
  -> runtime service, lifecycle, and command registration

new:
MonoBehaviour / ScriptableObject
  -> authoring source
  -> ModuleContributionData
  -> KernelIR
  -> VerifiedPlan
  -> runtime
```

This keeps Inspector, Prefab, Scene, and ScriptableObject usability while terminating runtime composition leakage from the Unity authoring layer.

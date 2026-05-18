# KernelIR Specification

## Document Status

- Document ID: 01_KernelIRSpec
- Status: Draft
- Role: normalized intermediate representation specification for the GameLib Kernel v2 architecture
- Depends on: [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md)
- Provides foundation for:
  - 02_ModuleContributionSpec.md
  - 03_VerifiedPlanGenerationSpec.md
  - 04_DependencyValidationSpec.md
  - 05_BootManifestAndProfileSpec.md
  - 06_ServiceGraphRuntimeSpec.md
  - 07_ScopeGraphRuntimeSpec.md
  - 08_LifecyclePlanSpec.md
  - 09_CommandCatalogRuntimeSpec.md
  - 10_ValueSchemaAndStoreSpec.md
  - 11_DebugMapAndDiagnosticsSpec.md
  - 12_UnityAuthoringBridgeSpec.md

### Ownership

This document owns the canonical IR node model, IR identity model, source location model, dependency edge representation, normalization invariants, deterministic ordering requirements, and hash-relevant semantic data for KernelIR.

This document does not own runtime storage layout, runtime handle layout, command execution algorithms, ValueStore memory layout, or validation algorithm details.

---

## Purpose

This specification defines KernelIR, the normalized intermediate representation used by the GameLib Kernel v2 architecture.

KernelIR is the canonical model produced from authoring inputs and consumed by validation, plan generation, debug map generation, and runtime projection generation.

KernelIR is not a runtime execution format.
KernelIR is not generated code.
KernelIR is not a Unity authoring asset.
KernelIR is the normalized authority from which verified runtime artifacts are derived.

KernelIR exists to prevent runtime discovery and ad-hoc generated artifact coupling.
It provides a single normalized model that can be:

- validated
- hashed
- diffed
- converted into runtime plans
- converted into DebugMap entries
- tested in CI

If a runtime plan cannot be traced back to KernelIR, it is not a valid target-kernel artifact.

---

## Scope

This specification defines:

- KernelIR root structure
- IR node categories
- common ID model
- source location model
- dependency edge model
- normalized contribution representation
- profile availability representation
- deterministic ordering requirements
- hash-relevant semantic data
- validation handoff boundaries

This specification intentionally does not define:

- final runtime service resolver implementation
- final scope handle memory layout
- final command executor invocation algorithm
- final ValueStore storage layout
- final save format
- final Unity component schema
- final validation algorithms
- final generated code format

This document must not become a dumping ground for runtime implementation details.
Runtime-specific details belong to lower runtime specs.

---

## Relationship to Other Specs

| Spec | Relationship |
|---|---|
| 02_ModuleContributionSpec.md | Defines how modules contribute raw data into KernelIR |
| 03_VerifiedPlanGenerationSpec.md | Defines how KernelIR becomes VerifiedKernelPlan |
| 04_DependencyValidationSpec.md | Defines validation algorithms over KernelIR dependencies |
| 05_BootManifestAndProfileSpec.md | Defines boot inputs and profile policy consumed by KernelIR outputs |
| 06_ServiceGraphRuntimeSpec.md | Consumes ServiceIR projection |
| 07_ScopeGraphRuntimeSpec.md | Consumes ScopeIR projection |
| 08_LifecyclePlanSpec.md | Consumes LifecycleIR projection |
| 09_CommandCatalogRuntimeSpec.md | Consumes CommandIR projection |
| 10_ValueSchemaAndStoreSpec.md | Consumes ValueKeyIR and value-init related projections |
| 11_DebugMapAndDiagnosticsSpec.md | Consumes SourceLocation and debug metadata from KernelIR |
| 12_UnityAuthoringBridgeSpec.md | Produces authoring inputs normalized into KernelIR |

KernelIR is the source model for the downstream runtime projections.
Lower specs must reference the concepts defined here rather than redefining them.

---

## IR Pipeline Position

```text
Authoring Inputs
  - Scene / Prefab
  - ModuleDefinitionAsset or equivalent authoring contribution source
  - ValueKey registry inputs
  - Command authoring inputs
  - Profile inputs

        ↓ Normalize

KernelIR

        ↓ Validate

Validated KernelIR

        ↓ Generate

VerifiedKernelPlan
  - ServiceGraphPlan
  - ScopeGraphPlan
  - CommandCatalogPlan
  - ValueSchemaPlan
  - LifecyclePlan
  - DebugMap
```

KernelIR is produced before runtime plan generation.
Runtime plans must not introduce new structural identities that do not exist in KernelIR.

KernelIR is the input to validation, hash generation, dependency analysis, debug-map generation, and runtime projection generation.

---

## IR Design Principles

### 1. Normalized, not raw

KernelIR must not preserve raw authoring ambiguity.
Prefab overrides, module aliases, authoring keys, and editor-only references must be normalized before entering KernelIR.

### 2. Explicit, not inferred

KernelIR must express relationships explicitly.
Parent scope, module ownership, lifecycle dependency, service requirement, command requirement, runtime query requirement, and value dependency must not be reconstructed later by runtime inference.

### 3. Deterministic

Given the same authoring inputs and profile, KernelIR generation must produce semantically identical output and stable ordering.

### 4. Traceable

Every IR node that can produce runtime behavior must be traceable to a source location.

### 5. Hashable

KernelIR must contain enough normalized semantic data to support hash-based artifact consistency checks.

### 6. Projection-safe

KernelIR must be structured so that ServiceGraphPlan, ScopeGraphPlan, CommandCatalogPlan, ValueSchemaPlan, LifecyclePlan, and DebugMap can be derived without adding hidden behavior.

### 7. Validation-friendly

KernelIR must expose missing dependency conditions, invalid ownership conditions, duplicate identity conditions, and profile mismatch conditions as structured data rather than hiding them in generation side effects.

---

## IR Identity Model

KernelIR uses typed identities.

An ID must not be interpreted outside its declared domain.

Examples:

- ServiceId cannot be used as CommandTypeId
- ValueKeyId cannot be used as ServiceId
- ScopeAuthoringId cannot be used as runtime ScopeHandle
- RuntimeQueryId cannot be used as lifecycle step identity

Each IR identity must define:

- domain
- stable name
- numeric or symbolic value
- owner module
- source location
- profile availability
- debug representation

Typed identity domains owned by KernelIR include at minimum:

- KernelIRId
- ModuleId
- ScopeAuthoringId
- ScopePlanId
- ServiceId
- CommandTypeId
- ValueKeyId
- LifecycleStepId
- RuntimeQueryId
- DependencyNodeId
- DependencyEdgeId
- SourceLocationId

ScopeAuthoringId and runtime ScopeHandle are different concepts.

ScopeAuthoringId identifies an authored scope definition.
ScopePlanId identifies a normalized scope definition in KernelIR.
ScopeHandle identifies a runtime scope instance.

KernelIR may contain ScopeAuthoringId and ScopePlanId.
It must not contain live runtime ScopeHandle values.

---

## Source Location Model

Every IR node that contributes runtime behavior must have source location metadata.

Source location may represent:

- Unity asset GUID
- asset path
- local file ID
- scene path
- prefab path
- component type
- serialized property path
- generated source reference
- legacy migration origin

SourceLocationIR is an explanatory sketch, not a final runtime API.

```csharp
public readonly struct SourceLocationIR
{
    public SourceLocationKind Kind;
    public string AssetGuid;
    public string AssetPath;
    public long LocalFileId;
    public string ObjectName;
    public string ComponentType;
    public string PropertyPath;
    public string GeneratedFrom;
}
```

KernelIR must preserve enough source information to support DebugMap generation, validation diagnostics, and migration tracing.

---

## KernelIR Root Structure

KernelIR is the root normalized model.

```csharp
public sealed class KernelIR
{
    public KernelIRHeader Header;
    public KernelProfileIR Profile;

    public ModuleIR[] Modules;
    public ScopeIR[] Scopes;
    public ServiceIR[] Services;
    public CommandIR[] Commands;
    public ValueKeyIR[] ValueKeys;
    public LifecycleIR[] Lifecycles;
    public RuntimeQueryIR[] RuntimeQueries;

    public DependencyEdgeIR[] Dependencies;
    public SourceLocationIR[] Sources;
    public DiagnosticSeedIR[] DiagnosticSeeds;
}
```

KernelIR must contain all structural information required to generate runtime plans, validate dependencies, generate DebugMap, and verify artifact consistency without performing runtime discovery.

KernelIR must not contain runtime instances or runtime caches.

---

## KernelIRHeader

```csharp
public sealed class KernelIRHeader
{
    public string DocumentId;
    public int FormatVersion;
    public string ProjectName;
    public string ProfileId;
    public string GeneratorVersion;
    public Hash128 SourceHash;
    public Hash128 NormalizedHash;
}
```

Header must not include non-semantic data such as generation timestamp or absolute machine-local paths.

Header fields are used by validation, diffing, and artifact consistency checks.

---

## ModuleIR

ModuleIR is the contribution owner inside KernelIR.

```csharp
public sealed class ModuleIR
{
    public ModuleId Id;
    public string Name;
    public ModuleKind Kind;
    public ModuleVersion Version;

    public ModuleAvailabilityIR Availability;
    public SourceLocationId Source;

    public ModuleDependencyIR[] RequiredModules;
    public ModuleDependencyIR[] OptionalModules;
}
```

Every ServiceIR, CommandIR, ValueKeyIR, LifecycleIR, RuntimeQueryIR, and ScopeIR must be owned by exactly one module unless lower specs explicitly define a shared ownership model.

Shared ownership is not the default.
If a lower spec introduces shared ownership, it must define how diagnostics, deletion, and migration behave.

ModuleIR defines module identity, version, availability, ownership, and dependency contribution boundaries.

ModuleIR does not define runtime registration order.

---

## ScopeIR

ScopeIR is a normalized scope definition, not a runtime scope instance.

```csharp
public sealed class ScopeIR
{
    public ScopeAuthoringId AuthoringId;
    public ScopePlanId PlanId;
    public string Name;
    public ScopeKind Kind;

    public ModuleId OwnerModule;
    public ScopeAuthoringId ParentAuthoringId;

    public ScopeServiceRequirementIR[] RequiredServices;
    public ScopeValueInitRefIR[] ValueInitPlans;
    public LifecyclePlanRefIR Lifecycle;
    public SourceLocationId Source;
}
```

ScopeIR must not infer parentage from Unity transform hierarchy.

If a scope parent is derived from authoring hierarchy during normalization, the result must be written explicitly into ScopeIR.
Runtime must consume the explicit parent relationship, not re-run the hierarchy inference.

ScopeIR may reference required services, initial value plans, and lifecycle plans, but it must not embed runtime handles or runtime object references.

---

## ServiceIR

ServiceIR defines service identity, lifetime category, ownership, and dependencies.

```csharp
public sealed class ServiceIR
{
    public ServiceId Id;
    public string Name;
    public ServiceLifetimeKind Lifetime;
    public ModuleId OwnerModule;

    public ServiceContractIR[] Contracts;
    public ServiceDependencyIR[] Dependencies;

    public ServiceFactoryKind FactoryKind;
    public SourceLocationId Source;
}
```

ServiceIR does not define the final runtime cache layout or resolver implementation.

Service type metadata may include C# type names for generation and diagnostics, but ServiceId is the runtime identity.
C# type name must not be the only stable identity.

ServiceIR must be valid without runtime reflection.
If runtime reflection is ever used in a lower specification, that lower specification must mark it as an explicit exception and define why the exception is bounded and removable.

---

## CommandIR

CommandIR defines the normalized boundary between authoring keys and runtime dispatch identity.

```csharp
public sealed class CommandIR
{
    public CommandTypeId TypeId;
    public string RuntimeName;
    public string AuthoringKey;
    public ModuleId OwnerModule;

    public CommandPayloadSchemaRefIR PayloadSchema;
    public CommandExecutorRefIR Executor;
    public CommandDependencyIR[] Dependencies;

    public SourceLocationId Source;
}
```

AuthoringKey is not runtime dispatch identity.

Runtime command dispatch must use CommandTypeId or an equivalent verified runtime identity.
AuthoringKey may be preserved for editor, migration, and diagnostics.

Conversion from authoring key to runtime identity happens during normalization or validation, not during target runtime dispatch.

CommandIR must be able to represent both the current ID-based executor path and the authoring-key-based catalog path if the migration layer needs that information.

CommandIR does not define executor construction policy.

---

## ValueKeyIR

ValueKeyIR defines the normalized representation of value identity and schema ownership.

```csharp
public sealed class ValueKeyIR
{
    public ValueKeyId Id;
    public string StableKey;
    public string DisplayName;
    public ValueKind Kind;

    public ModuleId OwnerModule;
    public ValueSchemaRefIR Schema;
    public SavePolicyIR SavePolicy;
    public SourceLocationId Source;
}
```

StableKey is not runtime lookup truth.

StableKey exists for authoring, migration, diagnostics, and registry validation.
Runtime access must use ValueKeyId or verified generated accessors.

ValueKeyIR defines what values may exist.
It does not define runtime storage layout.

ValueKeyIR should not contain initial values directly unless a lower spec explicitly defines that a value key can own a default value and explain why that does not blur schema and initialization responsibility.

---

## LifecycleIR

LifecycleIR represents explicit lifecycle participation.

```csharp
public sealed class LifecycleIR
{
    public LifecyclePlanId PlanId;
    public string Name;
    public ModuleId OwnerModule;

    public LifecycleStepIR[] Steps;
    public SourceLocationId Source;
}
```

```csharp
public sealed class LifecycleStepIR
{
    public LifecycleStepId Id;
    public LifecyclePhase Phase;
    public int Order;

    public ServiceId TargetService;
    public LifecycleActionKind Action;
    public DependencyEdgeId[] Dependencies;

    public SourceLocationId Source;
}
```

A service implementing an interface is not enough to participate in lifecycle dispatch.
Participation must be represented by LifecycleIR.

LifecycleIR must not be derived from registration scanning at runtime.

Lifecycle ordering is explicit data, not emergent behavior.

---

## RuntimeQueryIR

RuntimeQueryIR defines queryable runtime identity and index requirements.

```csharp
public sealed class RuntimeQueryIR
{
    public RuntimeQueryId Id;
    public string Name;
    public RuntimeQueryTargetKind TargetKind;

    public RuntimeIdentityFieldIR[] IndexedFields;
    public RuntimeQueryPolicyIR Policy;

    public ModuleId OwnerModule;
    public SourceLocationId Source;
}
```

Runtime query is separate from service resolution.
Runtime query must not be implemented as generic DI resolution.

Runtime query systems must define:

- queryable identity fields
- ownership of runtime indexes
- update timing
- invalidation behavior
- generation safety
- diagnostics on missing or ambiguous results
- performance budget

The replacement for legacy kind / id / category lookup must be specified separately from ServiceGraph and must not be hidden inside it.

RuntimeQueryIR exists to prevent service resolution from becoming the dumping ground for runtime identity lookup semantics.

---

## DependencyEdgeIR

Dependency edges model explicit graph relationships used by validation and projection generation.

```csharp
public readonly struct DependencyEdgeIR
{
    public DependencyNodeIR From;
    public DependencyNodeIR To;
    public DependencyKind Kind;
    public DependencyPhase Phase;
    public DependencyStrength Strength;
    public SourceLocationId Source;
}
```

```csharp
public enum DependencyPhase
{
    Build = 10,
    Generate = 20,
    Boot = 30,
    Acquire = 40,
    Runtime = 50,
    Save = 60,
    EditorOnly = 70,
}
```

```csharp
public enum DependencyStrength
{
    Required = 10,
    Optional = 20,
    Weak = 30,
    DiagnosticOnly = 40,
}
```

Dependency edges must be sufficient for 04_DependencyValidationSpec to detect missing relationships, cycles, and phase violations without reconstructing dependencies from runtime behavior.

DependencyEdgeIR must not encode runtime call stacks or executor instances.

---

## Profile and Conditional Availability

KernelIR is profile-aware.

```csharp
public sealed class AvailabilityIR
{
    public KernelProfileMask Profiles;
    public bool EnabledByDefault;
    public string Condition;
}
```

Profile and availability conditions must be resolved during normalization or validation unless a lower spec explicitly defines a runtime feature-flag system.

KernelIR should not contain arbitrary runtime-evaluated availability expressions.

Profile availability may differ across Development, Release, and Test, but the profile rules themselves must be explicit and deterministic.

---

## Normalization Rules

KernelIR generation must normalize:

- authoring aliases into canonical IDs
- authoring command keys into CommandTypeId references
- stable value keys into ValueKeyId references
- prefab / scene authoring references into source locations and authoring IDs
- module contribution order into deterministic order
- optional modules into explicit availability state
- legacy inputs into explicit migration-origin metadata

KernelIR must not contain unresolved authoring aliases.

Examples of forbidden unresolved data:

- unresolved command authoring key used as runtime dispatch identity
- unresolved stable value key used as runtime value reference
- unresolved service type name used as only identity
- unresolved scope parent inferred later from Transform hierarchy

Normalization must produce a canonical representation suitable for validation, hashing, diffing, and DebugMap generation.

Normalization is not a place for runtime fallback.

---

## Deterministic Ordering Rules

KernelIR arrays must have deterministic ordering.

Ordering must not depend on:

- Unity object enumeration order
- file system enumeration order
- dictionary iteration order
- reflection order
- asset import timing
- generation timestamp

Recommended ordering priority:

1. explicit order field if semantically meaningful
2. owner module ID
3. stable ID
4. stable name
5. source location

Deterministic ordering is required for reproducible hashes, predictable diffs, and stable artifact generation.

---

## Hash Input Rules

KernelIR hash must include semantic data required to determine runtime compatibility.

Include:

- module IDs and versions
- normalized service IDs
- normalized command IDs
- normalized value key IDs
- lifecycle step definitions
- dependency edges
- profile-affecting availability
- source reference identity where it affects generated output

Exclude:

- generation timestamp
- absolute local paths
- editor selection state
- non-semantic display foldout state
- non-semantic formatting

Hash checks must represent semantic compatibility, not file timestamp compatibility.

Lower specs must define exact hash algorithms and normalization rules.

---

## Diagnostics and DebugMap Requirements

KernelIR must contain enough source and debug metadata to generate DebugMap.

Every runtime-facing ID must be resolvable to:

- debug name
- owner module
- source location
- profile availability
- legacy origin if applicable

KernelIR should supply diagnostic seed information for missing dependency cases, duplicate identity cases, and profile mismatch cases.

DiagnosticSeedIR is a placeholder for structured validation or debug metadata, not a runtime execution construct.

For runtime diagnostics, an ID without sufficient debug metadata is a diagnostics degradation.

---

## Validation Handoff

01 defines the data required for validation.
04 defines validation algorithms and error severity policy.

KernelIR must expose enough structure to validate:

- missing dependencies
- duplicated IDs
- invalid ownership
- invalid source locations
- invalid profile availability
- dependency cycles
- forbidden runtime fallback dependencies
- legacy leakage into target kernel

KernelIR itself does not validate.
It provides the structure required for deterministic validation.

---

## Forbidden Contents

KernelIR must not contain:

- live UnityEngine.Object references as runtime authority
- runtime ScopeHandle values
- runtime service instances
- runtime command executor instances
- raw unresolved authoring keys
- raw unresolved stable value keys as runtime references
- reflection-only type identity as the only service identity
- generated code as source of truth
- mutable runtime state
- fallback-generated IDs
- non-deterministic ordering dependencies

KernelIR must not contain any artifact that depends on runtime search as its only way of becoming valid.

Forbidden contents are structural violations, not stylistic preferences.

---

## Compatibility and Migration Notes

Legacy-origin data may appear in KernelIR only as explicit migration metadata.

Legacy metadata must not become runtime fallback behavior.

Example metadata:

- legacy type name
- legacy asset path
- legacy command key
- legacy var stable key
- migration status
- removal target

If a lower spec needs to support a migration-only bridge, it must describe the bridge explicitly and define its removal condition.

---

## Open Questions for Lower Specs

The following are intentionally deferred:

- exact ServiceResolver storage layout
- exact ScopeHandle bit layout
- exact CommandPayload binary or serialized representation
- exact ValueStore memory layout
- exact DebugMap asset format
- exact Unity authoring component schema
- exact validation error code list

01 intentionally does not decide these details because they belong to lower specs.

---

## Acceptance Criteria

01 is complete when it defines:

- KernelIR purpose and authority
- IR identity model
- source location model
- root IR node categories
- dependency edge model
- normalization rules
- deterministic ordering rules
- hash input policy
- diagnostics and debug metadata requirements
- forbidden contents
- validation handoff to 04
- clear boundaries with lower specs

01 is not complete if any runtime execution detail, storage layout detail, or validation algorithm detail has escaped into the specification.

---

## Final Position

KernelIR must contain all structural information required to generate runtime plans, validate dependencies, generate DebugMap, and verify artifact consistency without performing runtime discovery.

This specification exists to keep the rest of the architecture honest.
If KernelIR is underspecified, every downstream spec will drift.
If KernelIR is overextended into runtime implementation detail, every downstream spec will be forced into the same shape.

KernelIR is the normalized authority.
VerifiedKernelPlan is the runtime projection.
Lower specs define how each projection is realized.
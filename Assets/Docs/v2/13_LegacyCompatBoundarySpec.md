# Legacy Compatibility Boundary Specification

## Document Status

- Document ID: `13_LegacyCompatBoundarySpec`
- Status: Draft
- Role: defines the quarantine boundary where legacy compatibility may remain visible, the allowed adapter shapes, profile constraints, diagnostics visibility, and removal rules for Kernel v2
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
- Provides foundation for:
  - [14_PerformanceBudgetAndRuntimeRulesSpec.md](14_PerformanceBudgetAndRuntimeRulesSpec.md)
  - [15_TestAndValidationSpec.md](15_TestAndValidationSpec.md)

### Revision Note

This revision creates 13 as the quarantine boundary for legacy compatibility.

It does not preserve legacy behavior as a design extension point.
It defines where migration-only adapters may remain visible, how they must be measured, and how they are prevented from re-entering the target kernel core as fallback behavior.

It also tightens the architecture direction around installer mutation, resolver fallback, command bulk registration, runtime stable-key fallback, and temporary runtime adapters so they remain explicit, profile-scoped, diagnostic-visible, and removable.

---

## Ownership

This specification owns:

- the purpose and prohibition model of `LegacyCompat`
- `LegacyBoundary`, `LegacyBridge`, `LegacyAdapter`, and `LegacyFallback` definitions
- dependency direction rules between legacy systems and the target kernel
- classification of allowed and forbidden legacy bridge kinds
- profile and availability rules for migration-only adapters
- diagnostics visibility requirements for every legacy bridge
- installer, resolver, service, scope, lifecycle, command, value, authoring, save, and runtime-query legacy boundary rules
- adapter shape, metadata, ownership, and removal policy requirements
- migration data policy for legacy-to-v2 mapping artifacts
- fallback prohibition rules for all profiles
- legacy compatibility failure policy
- forbidden patterns and required tests for the compatibility boundary

This specification does not own:

- final `ServiceGraph`, `ScopeGraph`, `LifecycleDispatcher`, `CommandCatalog`, or `ValueStore` implementation
- final Unity authoring component schema
- final save payload format
- final editor UI for migration tools
- final performance budget values for runtime subsystems
- complete reimplementation of individual legacy systems
- long-term maintenance of legacy APIs as first-class target APIs

13 owns the compatibility quarantine.
It must not re-own runtime semantics already owned by 05 through 12.

---

## Purpose

This specification defines where legacy code may interact with Kernel v2 during migration and, more importantly, where it may not.

Core statements:

```text
Legacy compatibility is a quarantine boundary, not a design extension point.

Legacy code may adapt into the target kernel through explicit, profiled, diagnostic-visible bridges.
Target kernel core must not depend on legacy runtime behavior.

Legacy may call into v2 through adapters.
v2 core must not call back into legacy as fallback.
```

This specification exists to stop old patterns from re-entering the target kernel under a new name.

If v2 core asks legacy code to repair missing data, repair missing services, invent missing IDs, or discover missing structure at runtime, the architecture has regressed.

---

## Scope

This specification defines:

- the compatibility philosophy for migration-only legacy use
- the allowed dependency direction between legacy systems and the target kernel
- allowed bridge kinds and forbidden bridge kinds
- profile restrictions for authoring migration, data migration, diagnostic adapters, and runtime adapters
- diagnostics and visibility requirements for legacy usage
- per-domain legacy boundaries for installer, resolver, service, scope, lifecycle, command, value, authoring, save, and runtime query surfaces
- fallback prohibition rules
- adapter metadata, shape, ownership, and removal requirements
- migration data rules
- failure behavior, forbidden patterns, and required test cases

---

## Non-Goals

This specification does not define:

- a full porting guide for every legacy feature
- the runtime implementation of the target kernel subsystems
- the final authoring UI for migration tools
- a promise that legacy APIs remain stable long-term
- the final save-format migration payload schema
- the final runtime packaging of legacy reports or dashboards

13 must not become a place where legacy behavior is re-specified as a second kernel.

---

## Relationship to Other Specs

| Spec | Relationship |
|---|---|
| [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md) | Defines the root asymmetry that legacy compatibility remains outside the new kernel core and delegates the exact quarantine contract to 13. |
| [01_KernelIRSpec.md](01_KernelIRSpec.md) | Owns normalized IDs and source locations that migration maps and legacy adapter diagnostics must target. |
| [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md) | Owns the contribution model that authoring migration adapters must emit into instead of mutating runtime builders. |
| [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md) | Owns generation of verified artifacts from migrated and normalized inputs. |
| [04_DependencyValidationSpec.md](04_DependencyValidationSpec.md) | Enforces whether crossing into or out of `LegacyCompat` is legal. |
| [05_BootManifestAndProfileSpec.md](05_BootManifestAndProfileSpec.md) | Owns boot and profile policy; 13 defines whether any legacy boot bridge may remain visible and under what constraints. |
| [06_ServiceGraphRuntimeSpec.md](06_ServiceGraphRuntimeSpec.md) | Owns target service runtime; 13 defines where legacy resolver or service adapters may remain visible without becoming service fallback. |
| [07_ScopeGraphRuntimeSpec.md](07_ScopeGraphRuntimeSpec.md) | Owns target scope runtime; 13 defines how legacy LifetimeScope surfaces are quarantined. |
| [08_LifecyclePlanSpec.md](08_LifecyclePlanSpec.md) | Owns lifecycle plan execution; 13 defines how legacy handler interfaces may be migrated without runtime scanning. |
| [09_CommandCatalogRuntimeSpec.md](09_CommandCatalogRuntimeSpec.md) | Owns target command runtime; 13 defines how legacy command runners and key systems are quarantined. |
| [10_ValueSchemaAndStoreSpec.md](10_ValueSchemaAndStoreSpec.md) | Owns target value schema and store; 13 defines how legacy Blackboard and Var bridges are isolated and prevented from acting as fallback truth. |
| [10_1_ScalarRuntimeAndBindingSpec.md](10_1_ScalarRuntimeAndBindingSpec.md) | Owns scalar runtime semantics; 13 defines where any legacy scalar adapters may remain visible without reintroducing string or hash identity fallback. |
| [10_2_DynamicValueEvaluationSpec.md](10_2_DynamicValueEvaluationSpec.md) | Owns dynamic evaluation runtime; 13 defines how any legacy dynamic wrappers or deferred runtime bridges remain quarantined. |
| [11_DebugMapAndDiagnosticsSpec.md](11_DebugMapAndDiagnosticsSpec.md) | Owns the shared diagnostics substrate; 13 defines what legacy bridge visibility and error codes must feed into it. |
| [12_UnityAuthoringBridgeSpec.md](12_UnityAuthoringBridgeSpec.md) | Owns extraction of Unity authoring into contribution input; 13 defines the only legal boundary where legacy authoring adapters may remain visible. |
| [14_PerformanceBudgetAndRuntimeRulesSpec.md](14_PerformanceBudgetAndRuntimeRulesSpec.md) | Will budget the allowed cost of legacy diagnostics and explicitly bounded adapters. |
| [15_TestAndValidationSpec.md](15_TestAndValidationSpec.md) | Implements executable boundary, visibility, removability, and regression checks using the quarantine rules defined here. It does not redefine adapter policy or legacy-boundary semantics. |

13 is the isolation contract for migration.
It must not duplicate domain ownership already fixed by the lower subsystem specs.

---

## Current Legacy Debt Observations

This section summarizes the current legacy-related debt observed in the codebase.
It is migration evidence, not target policy.

### Observation Traceability

| Observation | Evidence Type | Target Pressure |
|---|---|---|
| Installer discovery still uses `GetComponentsInChildren` and `Transform.parent` ownership inference. | Source | no runtime discovery and no hierarchy-derived truth |
| Legacy scope build still caches installers and calls `InstallFeature(builder, scope)` directly. | Source | contribution-driven runtime composition |
| Resolver fallback still uses component search and parent resolver chaining. | Source | explicit `ServiceGraph` and no resolver fallback |
| `CommandRunnerMB` still performs bulk executor, service, and lifecycle registration in one installer. | Source | explicit command and lifecycle contribution pipeline |
| `VarIdResolver` still creates runtime-only negative IDs for unresolved stable keys. | Source | verified `ValueKeyId` mapping and no runtime ID invention |
| `VarKeyRegistryLocator` still uses `Resources.Load` and runtime-created fallback registry instances. | Source | verified boot input and no runtime asset fallback |
| `BlackboardMB` still mixes installer mutation, acquire/release participation, init, debug, and transform auto-write. | Source | split authoring, value init, lifecycle, and diagnostics responsibilities |

### Representative Anchors

- [ScopeFeatureInstallerUtility.cs](../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs) - collects `IFeatureInstaller` via `GetComponentsInChildren` and uses `Transform.parent` through `TryGetNearestScopeNode`
- [RuntimeLifetimeScope.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs) - caches owned installers and invokes `InstallFeature(builder, this)` during build
- [RuntimeResolverHub.cs](../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs) - falls back to `GetComponent`, `GetComponentInChildren`, and `_parentResolver`
- [CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs) - bulk-registers many `ICommandExecutor` implementations plus lifecycle-related services
- [VarIdResolver.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs) - allocates runtime-only negative IDs for unresolved stable keys
- [VarKeyRegistryLocator.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs) - uses `Resources.Load` and creates runtime fallback registry instances
- [BlackboardMB.cs](../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs) - combines `IFeatureInstaller`, acquire/release hooks, init logic, debug-view wiring, and transform auto-write

### Current Gaps

The target architecture must close the following gaps:

- runtime discovery still exists in migration-era installer and resolver paths
- builder mutation still exists as a live pattern in legacy MonoBehaviours
- fallback still exists for services, value IDs, and registry lookup
- lifecycle intent can still be learned by registration or interface scanning
- legacy authoring objects can still act as runtime composition authorities
- missing target data can still be repaired indirectly by legacy systems instead of failing

---

## Legacy Compatibility Philosophy

Legacy compatibility exists only to support migration, inspection, and controlled coexistence.

It must not become:

- a fallback mechanism
- a hidden runtime dependency
- a second source of truth
- a way to bypass validation
- a way to keep installer-style runtime mutation alive

Required central rule:

```text
Legacy compatibility is allowed only when it is explicit, profiled, diagnostic-visible, and removable.
```

Additional rule:

```text
Development may make legacy usage more visible.
It must not make invalid target data valid through legacy fallback.
```

---

## Legacy Boundary Definitions

`LegacyCompat` is the migration-only quarantine zone where approved legacy adapters may remain visible.

Explanatory classification:

```csharp
public enum LegacyCompatKind
{
    None = 0,
    AuthoringMigration = 10,
    DataMigration = 20,
    RuntimeAdapter = 30,
    DiagnosticAdapter = 40,
    TestAdapter = 50,
    TemporaryBridge = 60,
    ForbiddenFallback = 90,
}
```

### LegacyBoundary

A declared boundary where legacy code may interact with the target kernel only through approved adapter rules.

### LegacyAdapter

A small, owned wrapper that converts legacy input or behavior into target-kernel concepts.

### LegacyBridge

A migration-limited connection between legacy systems and the target kernel.

### LegacyFallback

An implicit fallback from the target kernel to legacy behavior when target data or target runtime structure is missing.

`LegacyFallback` is forbidden by default.

---

## Dependency Direction Rules

Allowed by default:

- `LegacyAdapter` depends on v2 interfaces
- legacy authoring migration emits `ModuleContributionData`
- legacy diagnostic adapters report `KernelDiagnostic`
- legacy data migration reads legacy data and writes verified migration output
- legacy-facing shims may call into v2 runtime through explicit adapters during migration

Forbidden by default:

- v2 core depends on `RuntimeResolver`
- v2 `ServiceGraph` depends on legacy `LifetimeScope`
- v2 `ScopeGraph` depends on `Transform.parent` nearest-scope inference
- v2 `CommandCatalog` depends on `CommandRunnerMB` executor registration
- v2 `ValueStore` depends on `VarIdResolver` runtime fallback
- v2 runtime asks legacy code to repair missing target data

Dependency direction must be one-way:

```text
Legacy -> Adapter -> v2

Not:

v2 -> Legacy -> fallback
```

---

## Legacy Bridge Classification

Legacy bridges must be classified before they are legal.

Allowed bridge kinds:

| Kind | Purpose | Runtime allowed by default |
|---|---|---|
| `AuthoringMigration` | Convert legacy MonoBehaviour or ScriptableObject data into contribution input | No target runtime dependency |
| `DataMigration` | Convert legacy assets, registries, or save payloads into verified v2 data | Build, editor, migration, or load-prevalidation only |
| `RuntimeAdapter` | Temporarily expose legacy behavior through an explicit migration bridge | Development and Test only by default |
| `DiagnosticAdapter` | Forward legacy logs, failures, or migration status into 11 diagnostics | Allowed when it does not change runtime truth |
| `TestAdapter` | Compare legacy and v2 behavior in migration tests | Test only |
| `TemporaryBridge` | Short-lived emergency bridge with explicit expiration and owner | Development and Test only by default |
| `ForbiddenFallback` | Attempted repair of missing target data through legacy behavior | Never allowed |

Unclassified legacy bridges are invalid.

Forbidden bridge kinds include:

- fallback resolver bridge
- missing service repair bridge
- missing value-key repair bridge
- command executor discovery bridge
- lifecycle handler scan bridge
- scope-parent inference bridge

---

## Profile and Availability Policy

Legacy compatibility must declare profile availability.

Default policy:

| Profile | AuthoringMigration | DataMigration | RuntimeAdapter | DiagnosticAdapter | LegacyFallback |
|---|---|---|---|---|---|
| Development | Allowed with warning | Allowed with warning | Allowed only if declared, owned, and removable | Allowed | Forbidden |
| Test | Allowed | Allowed | Allowed for comparison tests or declared migration checks | Allowed | Forbidden |
| Release | Allowed only through prevalidated migrated input or prebuilt verified artifacts | Allowed only as explicit prevalidation or import step | Forbidden by default | Allowed only when forwarding diagnostics from an otherwise allowed bridge | Forbidden |

Release profile may ship migrated results.
That is not the same thing as shipping live runtime legacy dependency.

Development profile may increase visibility.
It must not increase fallback permissiveness.

---

## Diagnostics and Visibility Requirements

Every legacy bridge use must be diagnostic-visible through 11’s structured diagnostics pipeline.

A `LegacyCompat` diagnostic must include at least:

- legacy system name
- bridge kind
- owner module
- target v2 subsystem
- source location
- active profile
- removal status
- expiration condition or blocking issue when applicable
- stable diagnostics code

Representative stable diagnostics codes:

- `LEGACY_BRIDGE_USED`
- `LEGACY_RUNTIME_ADAPTER_USED`
- `LEGACY_FALLBACK_FORBIDDEN`
- `LEGACY_CORE_DEPENDENCY_FORBIDDEN`
- `LEGACY_PROFILE_FORBIDDEN`
- `LEGACY_MIGRATION_REQUIRED`
- `LEGACY_ADAPTER_EXPIRED`
- `LEGACY_DIRECT_BUILDER_MUTATION_FORBIDDEN`
- `LEGACY_RUNTIME_ID_FALLBACK_FORBIDDEN`
- `LEGACY_INSTALLER_DISCOVERY_FORBIDDEN`
- `LEGACY_RESOLVER_COMPONENT_FALLBACK_FORBIDDEN`
- `LEGACY_COMMAND_BULK_REGISTRATION_FORBIDDEN`
- `LEGACY_COMMAND_STRING_FALLBACK_FORBIDDEN`
- `LEGACY_LIFECYCLE_HANDLER_SCAN_FORBIDDEN`
- `LEGACY_ADAPTER_DIAGNOSTICS_MISSING`
- `LEGACY_ADAPTER_REMOVAL_POLICY_MISSING`
- `LEGACY_RUNTIME_QUERY_LEGACY_LOOKUP_FORBIDDEN`

Inspector warnings or local `Debug.LogWarning` calls are not sufficient for required failures.

---

## Legacy Installer Boundary

Legacy installer-style mutation is not allowed in target kernel runtime.

The following legacy pattern is forbidden in target paths:

```csharp
void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope)
```

Current evidence:

- [ScopeFeatureInstallerUtility.cs](../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs) discovers `IFeatureInstaller` components through `GetComponentsInChildren` and infers ownership through `Transform.parent`
- [RuntimeLifetimeScope.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs) caches owned installers and invokes `InstallFeature(builder, this)` directly during build

Target replacement:

```text
Legacy MB/SO data
  -> AuthoringMigration adapter
  -> ModuleContributionData
  -> KernelIR
  -> VerifiedPlan
  -> runtime
```

Allowed within the boundary:

- read serialized legacy fields
- attach source location and legacy component metadata
- emit contribution data
- report migration diagnostics

Forbidden within target paths:

- calling `InstallFeature` during target boot
- mutating `IRuntimeContainerBuilder` from legacy authoring components
- collecting legacy features through `GetComponentsInChildren`
- inferring installer ownership from `Transform.parent`

---

## Legacy Resolver Boundary

Target `ServiceGraph` must not depend on legacy `RuntimeResolver` or VContainer-like fallback resolution.

Current evidence:

- [RuntimeResolverHub.cs](../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs) falls back to `GetComponent`, `GetComponentInChildren`, and `_parentResolver` after registration miss

Allowed:

- `LegacyResolverAdapter` may expose v2 services to legacy callers during migration
- legacy diagnostic tools may inspect legacy resolver state

Forbidden:

- target service resolution falling back to legacy resolver
- target service resolution falling back to host-component search
- lifecycle handler lookup through legacy resolver chain
- command executor lookup through legacy resolver chain
- runtime query lookup through legacy resolver chain

Core rule:

```text
If v2 service resolution fails, the result is a v2 diagnostics failure.
It must not ask legacy resolver for repair.
```

---

## Legacy Service Boundary

Legacy services may be adapted only through explicit `RuntimeAdapter` or `AuthoringMigration` declarations.

A legacy service adapter must declare:

- target `ServiceId`
- legacy source type
- lifetime
- dependency list
- profile availability
- diagnostics code
- removal plan

Forbidden:

- broad `.AsImplementedInterfaces()`-style exposure
- exposing whatever interfaces the legacy object happens to implement as target truth
- implicit service substitution after target resolution failure

Each exposed target contract must be explicit.

---

## Legacy Scope Boundary

Legacy `LifetimeScope` may coexist only through an explicit `LegacyScopeAdapter`.

Target `ScopeGraph` must not infer runtime parent-child relationships from legacy scope hierarchy.

Forbidden:

- nearest-scope search
- `Transform.parent` ownership inference
- automatic scope creation from legacy object presence
- duplicate root cleanup through legacy runtime behavior
- using legacy scope hierarchy as target scope truth

Allowed within migration:

- explicit mapping from legacy scope identity to `ScopeAuthoringId` or verified scope IDs
- diagnostics-visible legacy scope inspection

---

## Legacy Lifecycle Boundary

Legacy lifecycle handler interfaces are not target lifecycle enrollment.

Legacy `IScopeAcquireHandler`, `IScopeReleaseHandler`, and `IScopeTickHandler` may be mapped to `LifecycleContribution` during migration.

Runtime scanning for implemented handler interfaces is forbidden.

Core rule:

```text
The migration adapter may read legacy handler intent.
The target LifecycleDispatcher must execute LifecyclePlan, not scan legacy handlers.
```

---

## Legacy Command Boundary

Legacy command executors must not be discovered through DI registration.

Target migration path:

- legacy executor metadata
- `CommandContribution`
- `CommandIR`
- `CommandCatalogPlan`
- `CommandCatalog` runtime lookup

Current evidence:

- [CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs) bulk-registers many `ICommandExecutor` implementations and mixes command services with lifecycle handlers

Forbidden:

- resolving `IReadOnlyList<ICommandExecutor>` as target command discovery
- registering executors through `CommandRunnerMB` in target runtime paths
- using legacy command authoring key lookup as runtime dispatch truth
- falling back from missing `CommandTypeId` to legacy command key resolver

`CommandRunnerMB` may remain only as migration source or legacy-facing facade inside the compatibility boundary.
It is not a target runtime registrar.

---

## Legacy Value / Blackboard / Var Boundary

Legacy value systems may be migrated into `ValueSchema` and `ValueStore`, but target value runtime must not depend on legacy key fallback.

Current evidence:

- [VarIdResolver.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs) allocates runtime-only negative IDs for unresolved stable keys
- [VarKeyRegistryLocator.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs) uses `Resources.Load` and runtime-created fallback registry instances
- [BlackboardMB.cs](../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs) mixes installer mutation, init, lifecycle, and debug responsibilities

Forbidden in target paths:

- runtime stable-key resolution for required values
- runtime-only negative IDs
- `Resources.Load` registry fallback
- legacy Blackboard as schema authority
- inferring `ValueSchema` from legacy runtime store contents

Migration-only allowance:

```text
Legacy stable keys may be used during migration to map old data to ValueKeyId.
The mapping result must be verified before runtime.
```

The same rule applies to other legacy key systems that derive runtime IDs from strings or hashes.

---

## Legacy Unity Authoring Boundary

Legacy MonoBehaviours may be used as authoring sources only through `AuthoringMigration` adapters.

Allowed:

- read serialized fields
- attach `SourceLocation`
- emit contribution data
- report migration diagnostics

Forbidden:

- calling `InstallFeature` during target boot
- calling `builder.Register` from legacy authoring components in target paths
- using `OnValidate` to silently repair target identity
- runtime discovery of legacy MonoBehaviours as target contributions

This section must remain consistent with [12_UnityAuthoringBridgeSpec.md](12_UnityAuthoringBridgeSpec.md).

---

## Legacy Save Boundary

Legacy save data may be migrated only through explicit `SaveMigrationPlan` behavior.

A save migration path must define:

- source legacy format
- target schema version
- ID mapping
- missing-key policy
- failure policy
- diagnostics behavior

Forbidden:

- target `SaveSystem` reading legacy save payload opportunistically as fallback for missing v2 save data
- save-schema inference from legacy runtime state during target runtime execution

Migrated save output must be explicit and versioned.

---

## Legacy Runtime Query Boundary

Legacy lookup systems may be adapted into `RuntimeQuery` only when query identity, target kind, invalidation, and ambiguity policy are explicit.

Forbidden:

- using legacy resolver chain as `RuntimeQuery`
- using `Transform` traversal as query truth
- using string lookup as target identity without verified mapping
- using legacy kind or category lookup as target runtime query truth without explicit adapter metadata

Legacy lookup may remain inside migration reports or explicit adapters.
It is not target runtime truth.

---

## Fallback Prohibition Policy

Legacy compatibility must not repair missing target-kernel data.

Core rule:

```text
Fallback converts validation failure into hidden runtime behavior.
Target kernel must reject this.
```

Forbidden fallback examples:

- missing `ServiceId` resolved from `RuntimeResolver`
- missing scope parent inferred from `Transform.parent`
- missing `CommandTypeId` resolved from command string key
- missing `ValueKeyId` generated as negative runtime ID
- missing `BootManifest` loaded from `Resources`
- missing `LifecycleStep` discovered from implemented interface
- missing installer contribution repaired through `GetComponentsInChildren`

Development may warn more.
It must not heal more.

---

## Adapter Shape and Ownership Policy

A legacy adapter must be:

- explicitly declared
- owned by a module
- profile-scoped
- diagnostics-visible
- one-way where possible
- removable
- covered by tests

Explanatory metadata shape:

```csharp
public sealed class LegacyAdapterDescriptor
{
    public LegacyCompatKind Kind;
    public ModuleId OwnerModule;
    public string LegacySystemName;
    public string TargetSubsystemName;
    public KernelProfileMask Profiles;
    public SourceLocationId Source;
    public LegacyRemovalStatus RemovalStatus;
    public string DiagnosticsCode;
    public string RemovalCondition;
}
```

Additional rules:

- one adapter should solve one compatibility problem
- adapters must not become parallel kernels or hidden registries
- adapters must not create new target-kernel features through legacy APIs
- if an adapter becomes performance-critical on a target hot path, the migration has regressed

---

## Migration Data Policy

Migration data must be explicit and versioned.

Examples:

- legacy command key to `CommandTypeId` map
- legacy var stable key to `ValueKeyId` map
- legacy scope identity to `ScopeAuthoringId` map
- legacy service type to `ServiceId` map
- legacy save schema to target value schema map

Required properties:

- owner module
- source format or system name
- source version
- target artifact or subsystem
- compatibility or generation hash when relevant
- source locations for traced entries when available

Forbidden:

- inferring migration data during runtime execution
- creating migration maps lazily as repair during target runtime access

---

## Sunset / Removal Policy

Every runtime legacy adapter must declare a removal policy.

Explanatory status model:

```csharp
public enum LegacyRemovalStatus
{
    Temporary = 10,
    MigrationOnly = 20,
    TestOnly = 30,
    Deprecated = 40,
    Forbidden = 90,
}
```

Removal policy must include at least:

- owner module
- reason for temporary existence
- target replacement
- allowed profiles
- expiration condition
- diagnostics code
- tracking issue or blocking condition

Expired adapters are not warnings.
They are validation failures.

---

## Performance and Memory Policy

Legacy bridges must not reintroduce runtime discovery cost.

Forbidden in target runtime paths:

- full hierarchy scans
- full registration scans
- reflection-heavy resolver lookup
- per-frame legacy adapter conversion
- per-command legacy key lookup
- per-value stable-key fallback
- per-access `Resources.Load`

Important rule:

```text
Legacy compatibility may be slower during migration tooling.
It must not be on hot runtime paths by default.
```

Performance optimization must not remove diagnostics visibility, ownership metadata, or removal policy checks.

---

## Failure Policy

Legacy compatibility failure must be explicit.

Representative failure categories:

- `LegacyAdapterMissing`
- `LegacyProfileForbidden`
- `LegacyFallbackAttempt`
- `LegacyMappingMissing`
- `LegacyAdapterExpired`
- `LegacySourceInvalid`
- `LegacyRuntimeDependencyForbidden`

Default failure boundaries:

| Failure Type | Default Boundary |
|---|---|
| authoring migration failure | generation failure |
| runtime adapter forbidden in current profile | boot failure or subsystem failure |
| fallback attempt from target core to legacy | operation failure in Development and Test; boot failure or fatal failure in Release depending on timing |
| required migration mapping missing | generation, validation, load-prevalidation, or boot failure |
| expired adapter | validation failure |
| diagnostics metadata missing on adapter | validation failure or analyzer failure |

Legacy compatibility failure must not continue through silent repair or last-write-wins fallback.

---

## Forbidden Patterns

The following are forbidden in the target legacy compatibility boundary:

- v2 core depending on legacy `RuntimeResolver`
- v2 `ServiceGraph` fallback to legacy resolver
- v2 `ScopeGraph` fallback to `Transform` nearest-scope inference
- v2 `CommandCatalog` fallback to `CommandRunnerMB`
- v2 `ValueStore` fallback to `VarIdResolver` negative IDs
- v2 `LifecyclePlan` scanning `IScopeAcquireHandler` or `IScopeTickHandler`
- target boot invoking `IFeatureInstaller`
- runtime `GetComponentsInChildren` to collect legacy features in target paths
- runtime `Resources.Load` fallback for required kernel assets
- runtime component fallback used as target service resolution
- runtime parent-resolver chain used to repair target dependency misses
- legacy adapter without owner module
- legacy adapter without diagnostics metadata
- legacy adapter without profile declaration
- legacy adapter without removal policy
- legacy compatibility used as a permanent extension point

---

## Test Case Model

Each `LegacyCompat` test case must define:

- Test ID
- Title
- legacy fixture
- target subsystem
- active profile
- operation
- expected result
- expected diagnostics
- expected dependency direction
- expected migration output when applicable

---

## Required Test Cases

### A. Dependency Direction Tests

#### TC_LEGACY_DEP_001_LegacyAdapterMayDependOnV2

```text
Input:
- LegacyCommandAdapter calls v2 CommandCatalog through declared adapter

Expected:
- Passed
- Diagnostic LEGACY_BRIDGE_USED warning in Development
```

#### TC_LEGACY_DEP_002_V2CoreCannotDependOnLegacyResolver

```text
Input:
- ServiceGraph attempts fallback to RuntimeResolver

Expected:
- Failed
- LEGACY_CORE_DEPENDENCY_FORBIDDEN
```

#### TC_LEGACY_DEP_003_V2ScopeGraphCannotUseNearestScopeSearch

```text
Input:
- ScopeGraph tries to use Transform.parent nearest-scope logic

Expected:
- Failed
- LEGACY_CORE_DEPENDENCY_FORBIDDEN
```

### B. Profile Tests

#### TC_LEGACY_PROFILE_001_RuntimeAdapterAllowedInDevelopmentWithWarning

```text
Profile:
- Development

Input:
- explicit runtime legacy adapter with owner and removal policy

Expected:
- PassedWithWarnings
- LEGACY_RUNTIME_ADAPTER_USED
```

#### TC_LEGACY_PROFILE_002_RuntimeAdapterRejectedInRelease

```text
Profile:
- Release

Input:
- runtime legacy adapter enabled

Expected:
- Failed
- LEGACY_PROFILE_FORBIDDEN
```

#### TC_LEGACY_PROFILE_003_LegacyFallbackRejectedInAllProfiles

```text
Profile:
- Development / Test / Release

Input:
- missing ServiceId fallback to legacy resolver

Expected:
- Failed
- LEGACY_FALLBACK_FORBIDDEN
```

### C. Installer Boundary Tests

#### TC_LEGACY_INSTALLER_001_IFeatureInstallerNotInvokedByTargetBoot

```text
Input:
- legacy component implements IFeatureInstaller

Operation:
- target kernel boot

Expected:
- InstallFeature is not called
- target uses contribution data only
```

#### TC_LEGACY_INSTALLER_002_GetComponentsInChildrenFeatureCollectionForbidden

```text
Input:
- target boot attempts to collect installers by GetComponentsInChildren

Expected:
- Failed
- LEGACY_INSTALLER_DISCOVERY_FORBIDDEN
```

#### TC_LEGACY_INSTALLER_003_LegacyMBExtractedAsContribution

```text
Input:
- legacy MeshChannelHub-like MonoBehaviour serialized fields

Operation:
- AuthoringMigration adapter extracts data

Expected:
- ServiceContribution
- LifecycleContribution
- no builder mutation
```

### D. Resolver and Service Boundary Tests

#### TC_LEGACY_RESOLVER_001_ComponentFallbackRejectedInTargetPath

```text
Input:
- target service resolution attempts host GetComponent or GetComponentInChildren fallback

Expected:
- Failed
- LEGACY_RESOLVER_COMPONENT_FALLBACK_FORBIDDEN
```

#### TC_LEGACY_SERVICE_001_LegacyServiceAdapterRequiresExplicitContracts

```text
Input:
- legacy service adapter exposes broad implemented-interface set without explicit target contracts

Expected:
- Failed
- LEGACY_CORE_DEPENDENCY_FORBIDDEN
```

### E. Command Boundary Tests

#### TC_LEGACY_CMD_001_CommandRunnerMBBulkRegistrationRejected

```text
Input:
- CommandRunnerMB registers executors as ICommandExecutor

Expected:
- Failed in target runtime path
- LEGACY_COMMAND_BULK_REGISTRATION_FORBIDDEN
```

#### TC_LEGACY_CMD_002_LegacyCommandKeyMigrationAllowed

```text
Input:
- legacy command key camera.shake

Operation:
- migration maps key to CommandTypeId

Expected:
- Passed
- mapping output included in migration report
```

#### TC_LEGACY_CMD_003_RuntimeCommandStringFallbackRejected

```text
Input:
- runtime dispatch uses string key because CommandTypeId is missing

Expected:
- Failed
- LEGACY_COMMAND_STRING_FALLBACK_FORBIDDEN
```

### F. Value Boundary Tests

#### TC_LEGACY_VALUE_001_StableKeyMigrationAllowed

```text
Input:
- legacy stableKey health.current

Operation:
- migration maps stableKey to ValueKeyId

Expected:
- Passed
```

#### TC_LEGACY_VALUE_002_RuntimeNegativeIdRejected

```text
Input:
- VarIdResolver returns negative runtime-only id

Expected:
- Failed in target runtime
- LEGACY_RUNTIME_ID_FALLBACK_FORBIDDEN
```

#### TC_LEGACY_VALUE_003_LegacyBlackboardNotSchemaAuthority

```text
Input:
- legacy Blackboard contains key not present in ValueSchema

Expected:
- Failed or migration required
- LEGACY_MIGRATION_REQUIRED
```

### G. Lifecycle Boundary Tests

#### TC_LEGACY_LIFE_001_HandlerInterfaceMigrationAllowed

```text
Input:
- legacy service implements IScopeTickHandler

Operation:
- migration creates LifecycleContribution Tick step

Expected:
- Passed
```

#### TC_LEGACY_LIFE_002_RuntimeHandlerScanRejected

```text
Input:
- LifecycleDispatcher scans ServiceGraph for IScopeTickHandler

Expected:
- Failed
- LEGACY_LIFECYCLE_HANDLER_SCAN_FORBIDDEN
```

### H. Authoring, Save, and Runtime Query Tests

#### TC_LEGACY_AUTHOR_001_LegacyMonoBehaviourUsedOnlyThroughAuthoringMigration

```text
Input:
- legacy MonoBehaviour with serialized migration data

Operation:
- extraction under 12 and 13 boundary

Expected:
- contribution data emitted
- SourceLocation attached
- no runtime builder mutation
```

#### TC_LEGACY_SAVE_001_SaveMigrationPlanRequired

```text
Input:
- target load encounters legacy save payload

Expected:
- explicit SaveMigrationPlan required or failure
- no opportunistic runtime fallback
```

#### TC_LEGACY_QUERY_001_LegacyResolverChainNotRuntimeQuery

```text
Input:
- RuntimeQuery attempts to use legacy resolver chain lookup

Expected:
- Failed
- LEGACY_RUNTIME_QUERY_LEGACY_LOOKUP_FORBIDDEN
```

#### TC_LEGACY_QUERY_002_TransformTraversalQueryRejected

```text
Input:
- query implementation uses Transform traversal as runtime identity source

Expected:
- Failed
- LEGACY_RUNTIME_QUERY_LEGACY_LOOKUP_FORBIDDEN
```

### I. Diagnostics and Sunset Tests

#### TC_LEGACY_DIAG_001_LegacyAdapterRequiresDiagnostics

```text
Input:
- legacy adapter without diagnostics metadata

Expected:
- Failed
- LEGACY_ADAPTER_DIAGNOSTICS_MISSING
```

#### TC_LEGACY_SUNSET_001_RuntimeAdapterRequiresRemovalPolicy

```text
Input:
- runtime legacy adapter without removal policy

Expected:
- Failed
- LEGACY_ADAPTER_REMOVAL_POLICY_MISSING
```

#### TC_LEGACY_SUNSET_002_ExpiredAdapterRejected

```text
Input:
- legacy adapter marked expired

Expected:
- Failed
- LEGACY_ADAPTER_EXPIRED
```

---

## Acceptance Criteria

This specification is complete when it defines:

- legacy compatibility philosophy
- `LegacyBoundary`, `LegacyBridge`, `LegacyAdapter`, and `LegacyFallback` definitions
- dependency direction rules
- bridge classification
- profile and availability policy
- diagnostics and visibility requirements
- legacy installer boundary
- legacy resolver boundary
- legacy service boundary
- legacy scope boundary
- legacy lifecycle boundary
- legacy command boundary
- legacy value, Blackboard, and Var boundary
- legacy Unity authoring boundary
- legacy save boundary
- legacy runtime query boundary
- fallback prohibition policy
- adapter shape and ownership policy
- migration data policy
- sunset and removal policy
- performance and memory policy
- failure policy
- forbidden patterns
- required test cases

The specification is not complete if legacy compatibility can still act as a hidden repair path for missing target data or if v2 core can still depend on legacy runtime behavior as fallback.

---

## Final Position

Legacy compatibility must be explicit, one-way, diagnostic-visible, profile-scoped, and removable.

Legacy may call into v2 through adapters.
v2 core must not call back into legacy as fallback.

13 is not a specification for preserving old behavior indefinitely.
It is the quarantine contract that keeps migration necessary, measurable, and bounded while protecting the target kernel from regression.
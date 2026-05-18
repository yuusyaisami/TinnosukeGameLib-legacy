# Dependency Validation Specification

## Document Status

- Document ID: 04_DependencyValidationSpec
- Status: Draft
- Role: dependency correctness contract across KernelIR and generated runtime projections
- Depends on:
  - [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md)
  - [01_KernelIRSpec.md](01_KernelIRSpec.md)
  - [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md)
  - [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md)
- Provides foundation for:
  - 05_BootManifestAndProfileSpec.md
  - 06_ServiceGraphRuntimeSpec.md
  - 07_ScopeGraphRuntimeSpec.md
  - 08_LifecyclePlanSpec.md
  - 09_CommandCatalogRuntimeSpec.md
  - 10_ValueSchemaAndStoreSpec.md
  - 10_2_DynamicValueEvaluationSpec.md
  - 11_DebugMapAndDiagnosticsSpec.md
  - 12_UnityAuthoringBridgeSpec.md
  - 13_LegacyCompatBoundarySpec.md
  - 14_PerformanceBudgetAndRuntimeRulesSpec.md
  - 15_TestAndValidationSpec.md

### Ownership

04 owns dependency correctness, phase-aware validation semantics, severity policy, diagnostics requirements, and the validation result model.

It does not own KernelIR layout, generation algorithms, runtime execution behavior, runtime storage layout, or artifact manifest format.

---

## Purpose

This specification defines how dependency correctness is validated before KernelIR and its generated projections are allowed to become runtime execution inputs.

Dependency validation is the gate that prevents invalid KernelIR from becoming runtime plans.

Invalid dependency graphs must fail before runtime plan execution.

A dependency discovered only at runtime is a validation failure unless a lower spec explicitly defines it as a verified runtime query.

This specification exists to prevent the following failure modes:

- missing required dependencies reaching boot or acquire
- cycles in invalid phases
- profile-specific dependency gaps being discovered after generation
- optional dependencies collapsing into silent fallback
- runtime query semantics leaking into service resolution
- legacy compatibility leaking into the target kernel core
- generated projections introducing unknown identities or losing provenance

---

## Scope

This specification defines:

- dependency validation inputs and outputs
- dependency identity validation rules
- phase-aware dependency validation
- dependency strength interpretation for validation
- validation severity policy
- module dependency validation
- service dependency validation
- scope dependency validation
- lifecycle dependency validation
- command dependency validation
- value dependency validation
- runtime query dependency validation
- diagnostics and debug coverage validation
- profile-aware validation
- optional dependency policy
- cycle detection policy
- conflict and duplicate validation
- forbidden dependency patterns
- legacy leakage validation
- validation diagnostics requirements
- validation test case format

This specification does not redefine the canonical meaning or wire shape of KernelIR nodes, dependency edges, generated artifacts, DebugMap assets, or runtime subsystem APIs.

---

## Relationship to Other Specs

| Spec | Relationship |
|---|---|
| [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md) | Defines validation as part of the trust boundary and forbids silent fallback in the target kernel. |
| [01_KernelIRSpec.md](01_KernelIRSpec.md) | Defines typed IR identity domains, dependency edge representation, lifecycle data, and runtime query data interpreted by this spec. |
| [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md) | Defines declarative contribution inputs and dependency declarations that must become valid before normalization output is accepted. |
| [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md) | Defines generation, staging, and artifact consistency checks around the dependency validation gates owned here. |
| 05_BootManifestAndProfileSpec.md | Consumes only inputs that have passed dependency validation for the selected profile. |
| 06_ServiceGraphRuntimeSpec.md | Consumes validated service dependencies and lifetime direction rules. |
| 07_ScopeGraphRuntimeSpec.md | Consumes validated scope parentage, runtime query rules, and explicit graph boundaries. |
| 08_LifecyclePlanSpec.md | Consumes validated lifecycle participation, ordering, and phase dependencies. |
| 09_CommandCatalogRuntimeSpec.md | Consumes validated command identity, executor, payload, and runtime query dependencies. |
| 10_ValueSchemaAndStoreSpec.md | Consumes validated value schema, init, save, and value-state boundary rules. |
| 10_2_DynamicValueEvaluationSpec.md | Consumes validated dynamic and reactive evaluation dependencies, phase legality, and invalidation declarations. |
| 11_DebugMapAndDiagnosticsSpec.md | Consumes validation diagnostics, source provenance, and debug coverage requirements defined here. |
| 12_UnityAuthoringBridgeSpec.md | Produces authoring inputs whose normalized dependency declarations must survive validation before acceptance. |
| 13_LegacyCompatBoundarySpec.md | Defines the only legal boundary where legacy usage may remain observable and controlled. |
| 15_TestAndValidationSpec.md | Turns the validation model and required cases in this document into executable test and CI coverage. |

04 is the dependency firewall between declarative architecture and runtime execution.

---

## Current Validation Observations

Current dependency validation observations must remain traceable to source code, design review notes, or migration evidence.

### Observation Traceability

| Observation | Evidence Type | Validation Pressure |
|---|---|---|
| Lifecycle and tick participation can be collected by scanning runtime registrations. | Source | 04, 08 |
| Runtime handler dispatch can be finalized after resolver build instead of before validation. | Source | 04, 06, 08 |
| Command executors and lifecycle services can be bulk-registered as a build side effect. | Source | 04, 09 |
| Missing value registry entries can fall back to runtime-only negative IDs. | Source | 04, 10 |
| Missing value registry assets can fall back to `Resources.Load` or an empty runtime asset. | Source | 04, 05, 10 |
| Runtime identity lookup can be satisfied through registry traversal after boot. | Source | 04, 07 |

### Representative Anchors

- [RuntimeResolverHub.cs](../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs) - registration indexing, `CollectHandlers<THandler>()`, `CollectAll(...)`, and `RuntimeAcquireReleaseDispatcher`
- [RuntimeLifetimeScope.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs) - feature installation during build, resolver construction, and handler extraction after runtime registration
- [CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs) - bulk executor and lifecycle registration patterns coupled to scope kind
- [VarIdResolver.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs) - runtime-only negative ID fallback when a stable key is not present in the registry
- [VarKeyRegistryLocator.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs) - `Resources.Load` lookup and empty runtime fallback asset creation
- [BaseLifetimeScopeRegistry.cs](../../GameLib/Script/Common/LTS/Registry/BaseLifetimeScopeRegistry.cs) - runtime identity lookup over registered scopes

### Current Gaps

The current project still exposes several dependency gaps that 04 must close in the target architecture:

- dependency truth can still be finalized after runtime build rather than before generation and boot
- optional behavior can still degrade into fallback rather than explicit absence policy
- runtime query and service resolution are not yet fully separated by validation semantics
- profile-specific failure boundaries are not yet fixed at the dependency level
- projection mismatch and provenance loss do not yet have a single authoritative rejection gate

---

## Validation Authority

04 is the fail-closed authority for dependency correctness.

If dependency correctness cannot be proven from explicit data, the graph is invalid for the target kernel.

01 owns typed identity domains, dependency edge representation, and normalized IR structure.
03 owns generation-time staging, artifact completeness, hash compatibility, and deterministic publication.
04 owns the rules that determine whether the declared dependency graph is legal before runtime execution may begin.

Validation is not a best-effort warning pass.
It is the gate that decides whether a graph may proceed to projection, publication, boot, or execution.

---

## Validation Position in Pipeline

Dependency validation runs at two distinct gates.

```text
Authoring / ModuleContribution
  -> Normalize
  -> KernelIR
  -> Pre-Generation Dependency Validation
  -> Projection Generation and Staging
  -> Post-Generation Dependency Validation
  -> Verified artifact set
  -> Boot
```

### 1. Pre-Generation Validation

Pre-Generation Validation runs against normalized KernelIR before runtime-facing projections are generated.

It validates dependency correctness within the declared graph, including:

- missing dependencies
- invalid identity-domain satisfaction
- invalid ownership
- invalid phase usage
- forbidden lifecycle enrollment patterns
- invalid optional dependency policy
- cycles in invalid phases
- forbidden legacy leakage

If Pre-Generation Validation fails, generation must not proceed as a trusted run.

### 2. Post-Generation Validation

Post-Generation Validation runs against staged projections emitted by 03 before those outputs become a trusted artifact set.

It validates that generated projections preserve dependency correctness, provenance, and identity boundaries, including:

- no unknown identities introduced by projection
- no dependency edges or required mappings dropped during projection
- no profile availability drift introduced by projection
- no debug coverage or source provenance required by validation lost in generation
- no projection-specific fallback introduced to repair invalid inputs

If Post-Generation Validation fails, staged artifacts are rejected and must not be published as verified outputs.

Both gates must pass before a runtime may treat the resulting artifact set as trusted.

---

## Dependency Validation Model

04 interprets the dependency data defined by 01 and 02.
It does not redefine their canonical shapes.

### Validation Inputs

Dependency validation operates on explicit, immutable inputs:

- normalized KernelIR
- selected kernel profile
- availability and version inputs resolved through normalization
- staged projections emitted by 03 for the selected profile
- DebugMap provenance metadata and identity coverage required for diagnostics
- lower-spec allowances that are explicitly declared, such as verified runtime query behavior or LegacyCompat boundaries

Hidden runtime state, scene discovery, reflection-derived handler lists, and fallback-created identities are not valid validation inputs.

### Validation Outputs

Dependency validation produces an explicit result contract:

- pass or fail status
- issue list with stable codes
- severity summary
- affected nodes and phases
- selected profile association
- enough provenance for 11 and 15 to render diagnostics and tests deterministically

A successful validation result is a prerequisite for trust.
It is not a hint that runtime may ignore.

### Dependency Identity Rules

Typed identity domains are owned by 01 and enforced here.

Validation must reject cross-domain satisfaction.

Examples:

- `ServiceId` cannot satisfy `CommandTypeId`
- `ValueKeyId` cannot satisfy `RuntimeQueryId`
- `ScopeAuthoringId` cannot satisfy a runtime `ScopeHandle` dependency
- `RuntimeQueryId` cannot satisfy `LifecycleStepId`

Unknown identity references are invalid whether they appear in KernelIR or only in a generated projection.

### Phase Model

04 uses the dependency phase set defined by 01.

| Phase | Value | Validation Meaning |
|---|---:|---|
| Build | 10 | Dependency required while constructing normalized or generated structural state. |
| Generate | 20 | Dependency required while producing verified projections. |
| Boot | 30 | Dependency required before the target runtime reaches ready state. |
| Acquire | 40 | Dependency required while entering active or acquired lifecycle participation. |
| Runtime | 50 | Dependency required during steady-state runtime operations. |
| Save | 60 | Dependency required while persisting validated runtime state. |
| EditorOnly | 70 | Dependency that exists only in editor-facing validation or authoring tooling paths. |

Validation is phase-aware.
A graph valid in one phase is not automatically valid in another.

### Strength Model

04 uses the dependency strength set defined by 01.

| Strength | Value | Validation Meaning |
|---|---:|---|
| Required | 10 | Absence is invalid for the selected profile and phase. |
| Optional | 20 | Absence is allowed only if explicit absence behavior is declared and itself valid. |
| Weak | 30 | Dependency is non-owning, but may still become invalid for the selected profile or phase. |
| DiagnosticOnly | 40 | Dependency exists to preserve observability or traceability rather than execution. |

Optional does not mean fallback.
Weak does not mean warning-only.

### Severity Model

Validation severity is distinct from dependency strength.

```csharp
public enum ValidationSeverity
{
    Info = 10,
    Warning = 20,
    Error = 30,
    Fatal = 40,
}
```

Default severity expectations:

- `Fatal`: trust-boundary violations, projection introducing unknown identities, unrecoverable provenance loss, or forbidden release-profile legacy leakage
- `Error`: missing required dependencies, invalid lifetime direction, invalid cycles, invalid optional policy, invalid runtime query/service mixing, duplicate identities
- `Warning`: explicitly allowed migration or profile exceptions that remain observable and bounded
- `Info`: non-blocking trace data that does not change acceptance

If a lower spec changes severity for a specific case, it must do so explicitly and preserve fail-closed behavior where required.

---

## Validation Rule Categories

Validation rules are grouped into the following categories:

- `Local Node`: validity of a single node, such as source location, owner module, or profile declaration
- `Local Edge`: validity of one dependency edge, such as identity domain, phase, or strength
- `Cross-Node`: relationships such as lifetime direction, ordering, or missing targets
- `Cross-Module`: module dependency and ownership interactions
- `Profile-Aware`: dependency legality under Development, Release, or Test policy
- `Projection`: preservation of dependency truth across generated artifacts
- `Legacy Boundary`: legality of crossing into or out of LegacyCompat

This grouping exists to keep validation deterministic, auditable, and extensible without turning it into an unbounded heuristic pass.

---

## Module Dependency Validation

Validation must check:

- every required module dependency exists
- every required module dependency is enabled in the selected profile
- module version compatibility rules are satisfied when a version constraint exists
- optional module dependencies declare explicit absence behavior
- core modules do not depend on legacy modules unless 13 explicitly allows the boundary
- module ownership of contributed identities remains explicit and non-ambiguous

Validation must reject:

- missing required modules
- disabled required modules in the selected profile
- optional module dependencies without absence behavior
- undeclared forbidden module dependencies
- target-kernel core depending on LegacyCompat as if it were a normal dependency

---

## Service Dependency Validation

Validation must check:

- every required `ServiceId` exists
- the owner module of each required service is available in the selected profile
- required contracts are actually provided by the target service identity
- lifetime direction is compatible with the requester and phase
- the declared dependency phase is legal for the participating lifetimes
- build, generate, boot, or acquire dependencies do not rely on runtime-only objects or discovery

Longer-lived services must not require shorter-lived services during Build, Generate, Boot, or Acquire unless a lower spec defines a verified indirection.

Validation must reject:

- missing required services
- invalid service lifetime direction
- service contract mismatch
- service dependencies satisfied only through runtime component discovery
- generation or boot dependencies that rely on runtime query semantics without an explicit runtime query declaration

---

## Scope Dependency Validation

Validation must check:

- every explicit parent scope exists
- parent scope kind is legal for the child scope kind
- the owner module exists and is available
- required scope services exist and are profile-valid
- referenced value init plans exist and target valid values
- scene and runtime boundary rules are respected
- parentage is explicit rather than deferred to runtime hierarchy inference

Validation must reject:

- missing parent scope definitions
- invalid parent kind relationships
- unresolved scope parentage that depends on transform hierarchy inference
- scope dependencies that cross forbidden scene or ownership boundaries

---

## Lifecycle Dependency Validation

Validation must check:

- every lifecycle target reference is valid for its declared target kind
- every lifecycle target service exists when the target kind is `Service`
- every lifecycle target scope exists when the target kind is `Scope`
- every lifecycle target runtime query exists when the target kind is `RuntimeQuery`
- every lifecycle target local owner reference is explicit and valid when the target kind is `ValueStore`, `RuntimeObjectOwner`, or `LegacyAdapter`
- lifecycle phase declarations are valid
- lifecycle step order is deterministic
- source location exists for lifecycle plans and steps
- lifecycle dependencies do not introduce invalid phase cycles
- the target reference is available in the required scope and profile

Participation must be represented by `LifecycleIR`.
Interface implementation alone is not lifecycle enrollment.

Validation must reject:

- interface-only lifecycle discovery assumptions
- lifecycle participation derived from registration scanning
- lifecycle steps targeting missing services
- lifecycle steps targeting missing scopes
- lifecycle steps targeting missing runtime queries
- lifecycle steps targeting invalid local owner references
- non-deterministic lifecycle ordering within the same phase
- lifecycle dependencies that create invalid Build, Generate, Boot, or Acquire cycles

---

## Command Dependency Validation

Validation must check:

- every `CommandTypeId` exists
- every command executor reference exists
- every payload schema reference exists
- every command payload field reference is compatible with its payload schema
- required service, value, and runtime query dependencies exist
- runtime dispatch identity is not satisfied by a raw authoring key
- command executor availability is not satisfied by ServiceGraph bulk discovery
- control-flow child command references are valid
- command runner domain and cardinality are valid where declared
- command-level module dependencies are declared where required

Validation must reject:

- missing command executors
- missing payload schemas
- payload fields missing required schema metadata
- payload field type mismatches
- use of authoring keys as runtime dispatch identity
- command dependencies satisfied only by bulk DI discovery
- command dependencies on missing values or runtime queries
- command runner cardinality that scales with mass entity count without explicit budget

---

## Value Dependency Validation

Validation must check:

- every `ValueKeyId` exists
- stable keys are unique where required
- every schema reference exists
- every required `ValueSchemaPlan` projection exists
- every init plan target exists
- every init plan target schema exists
- init value type is compatible with schema
- duplicate init entries have explicit deterministic overwrite or merge policy
- save policy references are valid
- save policy metadata is compatible with schema and profile
- dynamic or reactive evaluation dependencies are explicit
- every required `DynamicEvaluationPlan` projection exists
- every required `ReactiveEvaluationPlan` projection exists
- every dynamic evaluation output target exists and is legal for the declared phase
- every reactive evaluation plan declares dependency-discovery mode and invalidation policy
- dynamic evaluation inputs are declared and valid for the target phase
- table, record, and cell schema references are valid
- command read/write access declarations reference valid `ValueKeyId` values and store scopes
- runtime stable-key fallback is not required for target-kernel correctness
- runtime-only negative value IDs are absent

Validation must reject:

- duplicate value IDs or stable keys
- missing `ValueSchemaPlan` projection
- init plans that target missing keys
- type-mismatched initialization
- duplicate init entries resolved by collection order
- implicit dynamic dependencies hidden inside generic initialization
- hidden DynamicValue or deferred dynamic dependencies without explicit evaluation plan
- reactive evaluation that depends on hidden source-local version checks rather than declared invalidation policy
- table or cell payloads without schema
- invalid save policy metadata
- command value access without declared access policy
- value access that requires runtime stable-key lookup fallback
- runtime-only negative value IDs

---

## Runtime Query Dependency Validation

Validation must check:

- every `RuntimeQueryId` exists
- query target kind exists
- indexed fields are defined
- owner module is available in the selected profile
- invalidation policy exists
- ambiguity policy exists
- every requester explicitly declares the runtime query dependency

Runtime query dependency must not be satisfied by generic service resolution.

Validation must reject:

- missing runtime queries
- query dependencies silently redirected through service resolution
- missing invalidation or ambiguity policy
- query target kinds that are not defined in the validated graph

---

## Diagnostics and Debug Coverage Validation

Validation must check:

- error codes are stable and unique within their category
- diagnostic category ownership exists
- runtime-facing identities have required DebugMap coverage
- fatal and error diagnostics can resolve to human-readable metadata
- validation issues preserve enough provenance for 11 and 15 to render them deterministically

Validation must reject:

- missing diagnostics coverage for runtime-facing IDs where required by profile
- duplicate diagnostic codes in the same category
- fatal or error diagnostics that cannot be resolved beyond raw unknown identity values
- projection output that strips the provenance needed to explain a dependency failure

---

## Profile-Aware Validation

Validation always evaluates a selected kernel profile.

A dependency graph valid in Development may be invalid in Release or Test.

Validation must check:

- contribution availability per profile
- dependency availability per profile
- legacy allowance per profile
- DebugMap strictness per profile
- diagnostics detail requirements per profile

Profile-aware policy must remain explicit.
Silent fallback is forbidden in every profile.

Typical profile expectations:

- Development: maximum diagnostics and debug coverage; migration allowances may degrade to warnings only when explicitly bounded
- Release: no silent fallback; forbidden legacy leakage and unknown identity introduction are `Error` or `Fatal`
- Test: deterministic validation, maximum practical strictness, and reproducible diagnostics

---

## Optional Dependency Policy

Optional dependency is allowed only when absence behavior is explicit and valid.

```csharp
public enum OptionalDependencyAbsenceBehavior
{
    DisableContribution = 10,
    EmitWarning = 20,
    UseExplicitAlternative = 30,
    ProfileSpecificError = 40,
}
```

Optional dependency policy rules:

- silent absence is forbidden
- absence behavior must be declared explicitly
- `DisableContribution` must identify what contribution or projection is disabled
- `EmitWarning` must preserve observability without inventing runtime behavior
- `UseExplicitAlternative` must identify an explicit alternative target that exists, is type-compatible, and is valid for the same phase and profile
- `ProfileSpecificError` must define the profile boundary that upgrades absence into failure

An optional dependency without absence behavior is invalid.

An explicit alternative that is missing, cross-domain, phase-incompatible, or profile-incompatible is also invalid.

---

## Cycle Detection Policy

Cycle detection is phase-aware and must be evaluated separately for each dependency phase.

Invalid by default:

- Build cycle
- Generate cycle
- Boot cycle
- Acquire cycle
- Save cycle, unless a lower spec explicitly defines a safe and validated exception

Conditionally allowed:

- Runtime cycle through a verified lazy handle
- Runtime cycle through an explicit event channel
- Runtime cycle through a verified runtime query indirection

Still invalid at Runtime:

- direct required cycle with no verified indirection
- cycle repaired only by fallback or deferred discovery
- cycle whose observability cannot be explained through diagnostics

Validation must prove why an allowed Runtime cycle remains bounded and explicit.

---

## Conflict and Duplicate Validation

Validation must reject duplicate or conflicting structural identities by default, including:

- duplicate `ModuleId`
- duplicate `ServiceId`
- duplicate `CommandTypeId`
- duplicate `ValueKeyId`
- duplicate `RuntimeQueryId`
- duplicate `LifecycleStepId`
- duplicate stable key where uniqueness is required
- duplicate authoring key in the same runtime command namespace
- conflicting lifecycle order declarations within the same phase

Last-write-wins conflict resolution is forbidden.

Implicit merge behavior is also forbidden unless a lower spec explicitly defines a deterministic merge protocol and validation policy.

---

## Forbidden Dependency Patterns

The following dependency patterns are forbidden in the target kernel:

- target-kernel dependency satisfied by runtime scene search
- scope parent dependency satisfied by transform hierarchy inference
- service dependency satisfied by broad component traversal or generic runtime discovery
- command executor dependency satisfied by bulk DI registration discovery
- lifecycle participation inferred from implemented interface alone
- value dependency satisfied by runtime stable-key fallback
- missing required dependency repaired by generated runtime fallback identity
- runtime query dependency satisfied by generic service resolution
- generated projection introducing dependency semantics not present in KernelIR

If a lower spec needs an exception, it must define the allowed caller, timing, bounds, diagnostics behavior, and removal condition explicitly.

---

## Legacy Leakage Validation

Legacy dependencies are allowed only inside the boundary defined by 13.

The only default legal direction is:

```text
LegacyCompat -> New Kernel
New Kernel -> LegacyCompat is forbidden
```

Validation must reject:

- new kernel core depending on legacy runtime APIs
- runtime plan depending on legacy resolver fallback
- command catalog depending on legacy command runner registration
- value schema depending on legacy negative-ID repair or runtime stable-key repair
- lifecycle participation depending on interface-scan or registration-scan discovery rooted in legacy behavior

Legacy allowances must remain explicit, observable, profile-aware, and removable.

---

## Validation Report Model

The report model below is explanatory.
Lower specs or implementation may rename fields if the semantics remain equivalent.

```csharp
public sealed class DependencyValidationReport
{
    public ValidationResultStatus Status;
    public string SelectedProfile;
    public DependencyValidationIssue[] Issues;
    public DependencyValidationSummary Summary;
}
```

```csharp
public enum ValidationResultStatus
{
    Passed = 10,
    PassedWithWarnings = 20,
    Failed = 30,
    Fatal = 40,
}
```

```csharp
public sealed class DependencyValidationIssue
{
    public string Code;
    public ValidationSeverity Severity;
    public DependencyNodeIR From;
    public DependencyNodeIR To;
    public DependencyPhase Phase;
    public ModuleId OwnerModule;
    public SourceLocationId Source;
    public string Profile;
    public string Message;
    public string SuggestedFix;
}
```

```csharp
public sealed class DependencyValidationSummary
{
    public int InfoCount;
    public int WarningCount;
    public int ErrorCount;
    public int FatalCount;
}
```

The report must make acceptance and rejection explicit.
It must not require runtime reproduction to understand why validation failed.

---

## Validation Diagnostics Requirements

Every validation issue must provide:

- stable error code
- severity
- dependency phase
- dependency kind or rule category
- source node
- target node when applicable
- owner module
- source location
- selected profile
- suggested fix where possible

A validation error without source location is itself a diagnostics degradation.

Diagnostics must remain deterministic across hosts, profiles, and repeated runs over the same validated input.

---

## Validation Test Case Model

Each validation test case must define:

- Test ID
- Title
- Input KernelIR fixture
- Selected profile
- Expected status
- Expected diagnostics codes
- Expected affected nodes
- Expected phase
- Notes

Recommended fixture format:

```md
### TC_DEP_001_MissingRequiredService

Input:
- Service A requires Service B
- Service B is not defined

Profile:
- Development

Expected:
- Status: Failed
- Diagnostic: DEP_SERVICE_MISSING
- Severity: Error
- Phase: Boot
- Affected From: Service A
- Affected To: Service B
```

The fixture format exists so that 15 may turn these cases into executable validation tests without redefining intent.

---

## Required Test Cases

### A. Module Dependency Tests

#### TC_DEP_MODULE_001_MissingRequiredModule

Input:
- Module Gameplay requires Module Physics
- Module Physics is absent

Profile:
- Development

Expected:
- Status: Failed
- Diagnostic: DEP_MODULE_MISSING

#### TC_DEP_MODULE_002_DisabledRequiredModule

Input:
- Module Gameplay requires Module Save
- Module Save exists but is disabled in the Release profile

Profile:
- Release

Expected:
- Status: Failed
- Diagnostic: DEP_MODULE_DISABLED_FOR_PROFILE

#### TC_DEP_MODULE_003_OptionalModuleAbsentWithDisableBehavior

Input:
- Module UI optionally depends on Module Tooltip
- Absence behavior = DisableContribution

Profile:
- Development

Expected:
- Status: Passed
- Notes: Tooltip-related contributions are disabled explicitly

#### TC_DEP_MODULE_004_OptionalModuleAbsentWithoutBehavior

Input:
- Module UI optionally depends on Module Tooltip
- No absence behavior is declared

Profile:
- Development

Expected:
- Status: Failed
- Diagnostic: DEP_OPTIONAL_ABSENCE_BEHAVIOR_MISSING

### B. Service Dependency Tests

#### TC_DEP_SERVICE_001_MissingRequiredService

Input:
- Service CommandRunner requires Service CommandCatalog
- Service CommandCatalog is absent

Profile:
- Development

Expected:
- Status: Failed
- Diagnostic: DEP_SERVICE_MISSING

#### TC_DEP_SERVICE_002_InvalidLifetimeDirection

Input:
- Project-lifetime service depends on Scene-lifetime service during Boot

Profile:
- Development

Expected:
- Status: Failed
- Diagnostic: DEP_SERVICE_LIFETIME_INVALID

#### TC_DEP_SERVICE_003_ServiceContractMissing

Input:
- Service A requires contract `ITimeDomainService`
- Service B exists but does not provide the required contract

Profile:
- Development

Expected:
- Status: Failed
- Diagnostic: DEP_SERVICE_CONTRACT_MISSING

### C. Scope Dependency Tests

#### TC_DEP_SCOPE_001_MissingParentScope

Input:
- Scene scope declares parent ProjectRoot
- ProjectRoot scope is absent

Profile:
- Development

Expected:
- Status: Failed
- Diagnostic: DEP_SCOPE_PARENT_MISSING

#### TC_DEP_SCOPE_002_InvalidParentKind

Input:
- Project scope declares Entity scope as parent

Profile:
- Development

Expected:
- Status: Failed
- Diagnostic: DEP_SCOPE_PARENT_KIND_INVALID

#### TC_DEP_SCOPE_003_TransformParentInferenceDetected

Input:
- Scope parent is unresolved and marked as derived from runtime transform inference

Profile:
- Development

Expected:
- Status: Failed
- Diagnostic: DEP_SCOPE_RUNTIME_TRANSFORM_INFERENCE_FORBIDDEN

### D. Lifecycle Dependency Tests

#### TC_DEP_LIFECYCLE_001_TargetServiceMissing

Input:
- Lifecycle step targets Service `HealthService`
- `HealthService` is absent

Profile:
- Development

Expected:
- Status: Failed
- Diagnostic: DEP_LIFECYCLE_TARGET_SERVICE_MISSING

#### TC_DEP_LIFECYCLE_002_InterfaceOnlyParticipationRejected

Input:
- Service implements an acquire-like interface
- No `LifecycleIR` step exists

Profile:
- Development

Expected:
- Status: Failed when the graph expects automatic lifecycle discovery
- Diagnostic: DEP_LIFECYCLE_INTERFACE_DISCOVERY_FORBIDDEN

#### TC_DEP_LIFECYCLE_003_AcquireCycleRejected

Input:
- Acquire step A requires Acquire step B
- Acquire step B requires Acquire step A

Profile:
- Development

Expected:
- Status: Failed
- Diagnostic: DEP_CYCLE_ACQUIRE

#### TC_DEP_LIFECYCLE_004_TargetValidatedByKind

Input:
- Lifecycle step target kind is `RuntimeQuery`
- Referenced runtime query is absent

Profile:
- Development

Expected:
- Status: Failed
- Diagnostic: DEP_LIFECYCLE_TARGET_INVALID

### E. Command Dependency Tests

#### TC_DEP_COMMAND_001_CommandExecutorMissing

Input:
- `CommandTypeId` `CameraShake` exists
- Executor reference is missing

Profile:
- Development

Expected:
- Status: Failed
- Diagnostic: DEP_COMMAND_EXECUTOR_MISSING

#### TC_DEP_COMMAND_002_PayloadSchemaMissing

Input:
- `CommandTypeId` `SpawnEntity` exists
- Payload schema is absent

Profile:
- Development

Expected:
- Status: Failed
- Diagnostic: DEP_COMMAND_PAYLOAD_SCHEMA_MISSING

#### TC_DEP_COMMAND_003_AuthoringKeyUsedAsRuntimeIdentity

Input:
- Command runtime identity is raw string `camera.shake`

Profile:
- Development

Expected:
- Status: Failed
- Diagnostic: DEP_COMMAND_AUTHORING_KEY_USED_AS_RUNTIME_ID

#### TC_DEP_COMMAND_004_CommandUsesMissingValueKey

Input:
- `DamageCommand` requires `ValueKey` `health.current`
- `health.current` is missing

Profile:
- Development

Expected:
- Status: Failed
- Diagnostic: DEP_VALUE_KEY_MISSING

### F. Value Dependency Tests

#### TC_DEP_VALUE_001_DuplicateValueKeyId

Input:
- Two `ValueKeyIR` entries use `ValueKeyId` 1001

Profile:
- Development

Expected:
- Status: Failed
- Diagnostic: DEP_VALUE_KEY_DUPLICATE_ID

#### TC_DEP_VALUE_002_DuplicateStableKey

Input:
- Two `ValueKeyIR` entries use stable key `health.current`

Profile:
- Development

Expected:
- Status: Failed
- Diagnostic: DEP_VALUE_STABLE_KEY_DUPLICATE

#### TC_DEP_VALUE_003_InitPlanMissingKey

Input:
- Value init plan writes `ValueKeyId` 2000
- `ValueKeyId` 2000 is absent

Profile:
- Development

Expected:
- Status: Failed
- Diagnostic: DEP_VALUE_INIT_KEY_MISSING

#### TC_DEP_VALUE_004_InitTypeMismatch

Input:
- Value key `health.current` uses `Int` schema
- Init value is `String`

Profile:
- Development

Expected:
- Status: Failed
- Diagnostic: DEP_VALUE_INIT_TYPE_MISMATCH

### G. Runtime Query Dependency Tests

#### TC_DEP_QUERY_001_QueryTargetMissing

Input:
- Command `WithTarget` requires runtime query `TargetById`
- `TargetById` is absent

Profile:
- Development

Expected:
- Status: Failed
- Diagnostic: DEP_RUNTIME_QUERY_MISSING

#### TC_DEP_QUERY_002_QueryWithoutInvalidationPolicy

Input:
- Runtime query indexes Entity by category
- Invalidation policy is missing

Profile:
- Development

Expected:
- Status: Failed
- Diagnostic: DEP_RUNTIME_QUERY_INVALIDATION_POLICY_MISSING

#### TC_DEP_QUERY_003_QuerySatisfiedByServiceResolverRejected

Input:
- Runtime query dependency is satisfied by service resolver lookup instead of explicit runtime query semantics

Profile:
- Development

Expected:
- Status: Failed
- Diagnostic: DEP_RUNTIME_QUERY_AS_SERVICE_FORBIDDEN

### H. Cycle Detection Tests

#### TC_DEP_CYCLE_001_BuildCycleRejected

Input:
- Service A requires Service B in Build phase
- Service B requires Service A in Build phase

Profile:
- Development

Expected:
- Status: Failed
- Diagnostic: DEP_CYCLE_BUILD

#### TC_DEP_CYCLE_002_BootCycleRejected

Input:
- ProjectKernel boot requires SceneFlow
- SceneFlow boot requires ProjectKernel

Profile:
- Development

Expected:
- Status: Failed
- Diagnostic: DEP_CYCLE_BOOT

#### TC_DEP_CYCLE_003_RuntimeLazyCycleAllowed

Input:
- Service A references Service B by verified lazy handle
- Service B references Service A by verified lazy handle
- Phase = Runtime

Profile:
- Development

Expected:
- Status: Passed

#### TC_DEP_CYCLE_004_RuntimeRequiredCycleRejected

Input:
- Service A requires Service B directly
- Service B requires Service A directly
- Phase = Runtime
- Strength = Required
- No lazy, event, or runtime query indirection exists

Profile:
- Development

Expected:
- Status: Failed
- Diagnostic: DEP_CYCLE_RUNTIME_REQUIRED

### I. Profile-Aware Tests

#### TC_DEP_PROFILE_001_DebugMapMissingInDevelopment

Input:
- Runtime-facing `ServiceId` exists
- Required DebugMap entry is missing

Profile:
- Development

Expected:
- Status: Failed
- Diagnostic: DEP_DEBUGMAP_ENTRY_MISSING

#### TC_DEP_PROFILE_002_LegacyAllowedInDevelopmentWarning

Input:
- LegacyCompat module uses a legacy resolver bridge
- The bridge is explicitly allowed by 13 for Development

Profile:
- Development

Expected:
- Status: PassedWithWarnings
- Diagnostic: DEP_LEGACY_USAGE_WARNING

#### TC_DEP_PROFILE_003_LegacyRejectedInRelease

Input:
- TargetKernelCore depends on `LegacyRuntimeResolver`

Profile:
- Release

Expected:
- Status: Failed
- Diagnostic: DEP_LEGACY_FORBIDDEN_IN_RELEASE

### J. Projection Validation Tests

#### TC_DEP_PROJ_001_ProjectionIntroducesUnknownServiceId

Input:
- KernelIR contains `ServiceId` 100
- Generated `ServiceGraphPlan` contains `ServiceId` 999

Profile:
- Development

Expected:
- Status: Failed
- Diagnostic: DEP_PROJECTION_UNKNOWN_SERVICE_ID

#### TC_DEP_PROJ_002_DebugMapDoesNotMatchProjection

Input:
- `CommandCatalogPlan` contains `CommandTypeId` 200
- DebugMap has no entry for `CommandTypeId` 200

Profile:
- Development

Expected:
- Status: Failed
- Diagnostic: DEP_DEBUGMAP_COVERAGE_MISSING

#### TC_DEP_PROJ_003_ValueSchemaMissingProjection

Input:
- KernelIR contains `ValueKeyId` 300
- `ValueSchemaPlan` omits `ValueKeyId` 300

Profile:
- Development

Expected:
- Status: Failed
- Diagnostic: DEP_PROJECTION_VALUE_SCHEMA_MISSING

### K. Additional Required Cases

#### TC_DEP_DIAG_001_MissingSourceLocationForValidationIssue

Input:
- A dependency-bearing node triggers a validation failure
- The node and resulting issue have no source location provenance

Profile:
- Development

Expected:
- Status: Failed
- Diagnostic: DEP_DIAGNOSTICS_SOURCE_LOCATION_MISSING

#### TC_DEP_ID_001_DuplicateServiceId

Input:
- Two `ServiceIR` entries use the same `ServiceId`

Profile:
- Development

Expected:
- Status: Failed
- Diagnostic: DEP_SERVICE_DUPLICATE_ID

#### TC_DEP_PROJ_004_ProjectionDropsDependencyProvenance

Input:
- KernelIR contains a valid dependency edge
- Generated projection preserves the target identity but drops provenance required to trace the dependency through validation diagnostics

Profile:
- Development

Expected:
- Status: Failed
- Diagnostic: DEP_PROJECTION_PROVENANCE_MISSING

#### TC_DEP_OPTIONAL_001_ExplicitAlternativeInvalid

Input:
- Optional dependency is absent
- Absence behavior is `UseExplicitAlternative`
- The declared alternative target is missing or phase-incompatible

Profile:
- Development

Expected:
- Status: Failed
- Diagnostic: DEP_OPTIONAL_ALTERNATIVE_INVALID

---

## Acceptance Criteria

04 is complete when it defines:

- validation authority and pipeline position
- dependency identity enforcement based on 01-owned typed domains
- phase-aware validation rules
- dependency strength interpretation for validation
- severity policy
- validation rule categories
- module dependency validation
- service dependency validation
- scope dependency validation
- lifecycle dependency validation
- command dependency validation
- value dependency validation
- runtime query dependency validation
- diagnostics and debug coverage validation
- profile-aware validation
- optional dependency policy
- cycle detection policy
- conflict and duplicate validation
- forbidden dependency patterns
- legacy leakage validation
- validation report semantics
- validation diagnostics requirements
- validation test case format
- required validation case coverage

The specification is not complete if dependency correctness still relies on runtime discovery, silent fallback, or projection-time repair.

---

## Test Cases

| Test Case | Purpose | Verification |
|---|---|---|
| TC-04-01 | Confirm invalid dependency graphs fail before runtime execution. | The purpose, validation authority, and pipeline sections must make validation a hard gate. |
| TC-04-02 | Confirm cycle detection is phase-aware. | The phase model and cycle detection policy must distinguish Build, Generate, Boot, Acquire, Runtime, and Save. |
| TC-04-03 | Confirm optional dependency does not mean fallback. | The strength model and optional dependency policy must require explicit absence behavior. |
| TC-04-04 | Confirm runtime query remains separate from service resolution. | The runtime query validation and forbidden dependency patterns sections must reject generic DI substitution. |
| TC-04-05 | Confirm legacy leakage is rejected by default. | The legacy leakage validation section must preserve the `LegacyCompat -> New Kernel` one-way boundary. |
| TC-04-06 | Confirm projection mismatch remains a dependency validation failure. | The pipeline and projection validation rules must reject unknown identities, dropped provenance, and missing mappings after generation. |

---

## Final Position

Dependency validation is the firewall between explicit architecture and runtime failure.

KernelIR is only trustworthy when its dependency graph has survived explicit, profile-aware, phase-aware validation.

Runtime may execute only dependency graphs that have already survived explicit validation.

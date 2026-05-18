# DebugMap and Diagnostics Specification

## Document Status

- Document ID: 11_DebugMapAndDiagnosticsSpec
- Status: Draft
- Role: defines the unified DebugMap contract, structured diagnostics model, central diagnostics pipeline, and Unity logging sink policy for Kernel v2
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
- Provides foundation for:
  - 12_UnityAuthoringBridgeSpec.md
  - 13_LegacyCompatBoundarySpec.md
  - 14_PerformanceBudgetAndRuntimeRulesSpec.md
  - 15_TestAndValidationSpec.md

### Revision Note

This revision defines 11 as more than a DebugMap lookup note.
It establishes DebugMap and diagnostics as one unified runtime contract.

It also fixes the architecture rule that subsystems produce structured diagnostics, while Unity logging is emitted only by the central diagnostic sink.

### Ownership

This specification owns:

- DebugMap logical runtime contract
- SourceLocation and diagnostics provenance model
- KernelDiagnostic record model
- diagnostic code governance and stable identity rules
- diagnostic severity, domain, category, failure-boundary model
- diagnostic context, runtime identity, artifact identity, and exception payload model
- central diagnostic service contract
- diagnostic processor and sink contract
- UnityLogDiagnosticSink policy
- profile-based diagnostics emission policy
- diagnostics degradation rules
- diagnostics de-duplication, throttling, and aggregation policy
- cross-subsystem diagnostics integration contract
- diagnostics-related forbidden patterns
- diagnostics test model and acceptance criteria

This specification does not own:

- ServiceGraph runtime semantics
- ScopeGraph runtime semantics
- Lifecycle ordering semantics
- command execution semantics
- value storage layout or save format
- runtime query semantics
- editor window UI details
- crash reporting backend implementation
- Roslyn analyzer implementation details
- generation algorithms owned by 03
- validation algorithms owned by 04
- boot acceptance policy owned by 05

11 defines the shared diagnostics substrate.
06, 07, 08, 09, and 10 continue to own their domain-specific failure behavior and minimum provenance fields.

03 continues to own DebugMap generation.
04 continues to own validation semantics.
05 continues to own boot acceptance and boot failure boundaries.

---

## Purpose

This specification defines the target-kernel contract for DebugMap and diagnostics.

Core statements:

```text
DebugMap resolves verified runtime identities into human-readable source information.

Diagnostics is the unified structured error reporting pipeline for Kernel v2.

Subsystems do not log to Unity directly.
Subsystems emit structured KernelDiagnostic records.
Only the central Unity diagnostic sink may call Debug.Log / Debug.LogWarning / Debug.LogError / Debug.LogException.
```

This specification exists to prevent the following architectural regressions:

- runtime failures represented only as formatted strings
- subsystem-specific Unity logging paths that bypass shared diagnostics policy
- numeric ID failures that cannot be traced back to source
- profile-dependent diagnostics behavior that silently hides required failure information
- duplicated logging infrastructure per subsystem
- exception output paths that bypass diagnostics routing and failure policy

This specification is not merely about making IDs readable.
It is the error substrate that keeps a plan-first kernel observable, testable, and fail-closed.

---

## Scope

This specification defines:

- DebugMap purpose and contract boundary
- DebugMap coverage requirements for runtime-facing identities
- SourceLocation contract and provenance rules
- runtime identity mapping and artifact identity mapping for diagnostics
- KernelDiagnostic record model
- DiagnosticCode governance
- DiagnosticSeverity, DiagnosticDomain, DiagnosticCategory, and DiagnosticFailureBoundary model
- diagnostic context, payload, and exception capture policy
- KernelDiagnosticService contract
- diagnostic processor and sink contract
- UnityLogDiagnosticSink behavior and host-output separation
- profile-based diagnostics policy
- diagnostics degradation rules
- de-duplication, throttling, and aggregation rules
- performance rules for diagnostics hot paths
- subsystem integration rules for Boot, Generation, Validation, ServiceGraph, ScopeGraph, Lifecycle, Command, Value, RuntimeQuery, and Save
- legacy migration guidance for current logging debt
- diagnostics test model and acceptance criteria

---

## Non-Goals

This specification does not define:

- the final binary serialization container for DebugMap assets
- the final editor console or diagnostics window UI layout
- the final remote crash-report schema
- the final save-system architecture
- the final runtime code API signatures for every subsystem
- command payload schema details
- scope handle layout
- service factory layout
- profiler marker taxonomy beyond diagnostics-specific requirements

This specification must not turn diagnostics into a generic text logging guideline.
It defines runtime contract and structured reporting requirements, not stylistic preferences for console output.

---

## Relationship to Other Specs

| Spec | Relationship |
|---|---|
| [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md) | Defines DebugMap-backed diagnostics and no-silent-fallback as root constraints. |
| [01_KernelIRSpec.md](01_KernelIRSpec.md) | Defines identity domains and normalized source structure that diagnostics must trace back to. |
| [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md) | Provides DiagnosticsContribution provenance input consumed by DebugMap generation and diagnostics. |
| [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md) | Owns DebugMap generation and artifact consistency; 11 defines the runtime-facing DebugMap contract and diagnostics record shape consumed at runtime. |
| [04_DependencyValidationSpec.md](04_DependencyValidationSpec.md) | Owns validation semantics and validation failure meaning; 11 defines the compatible diagnostics substrate used to report them. |
| [05_BootManifestAndProfileSpec.md](05_BootManifestAndProfileSpec.md) | Owns boot acceptance and boot failure boundaries; 11 defines the shared diagnostics contract and central sink rules used during boot reporting. |
| [06_ServiceGraphRuntimeSpec.md](06_ServiceGraphRuntimeSpec.md) | 06 defines required service failure provenance and behavior; 11 defines the shared record, routing, DebugMap, and sink contract used to emit those failures. |
| [07_ScopeGraphRuntimeSpec.md](07_ScopeGraphRuntimeSpec.md) | 07 defines scope failure provenance and behavior; 11 defines the shared diagnostics substrate and DebugMap runtime contract used to emit those failures. |
| [08_LifecyclePlanSpec.md](08_LifecyclePlanSpec.md) | 08 defines lifecycle provenance fields and failure behavior; 11 defines the shared diagnostics substrate and central logging policy. |
| [09_CommandCatalogRuntimeSpec.md](09_CommandCatalogRuntimeSpec.md) | 09 defines command-local diagnostics requirements and failure behavior; 11 defines the shared diagnostic record, sink routing, and Unity output policy used by command runtime. |
| [10_ValueSchemaAndStoreSpec.md](10_ValueSchemaAndStoreSpec.md) | 10 defines value-specific provenance, access, and failure behavior; 11 defines the shared diagnostics and DebugMap contract used to emit value failures. |
| 12_UnityAuthoringBridgeSpec.md | Will consume DebugMap source mapping and diagnostics contracts for editor-facing authoring diagnostics. |
| 13_LegacyCompatBoundarySpec.md | Will define bounded legacy adapters that may forward legacy errors into the 11 diagnostics pipeline. |
| 14_PerformanceBudgetAndRuntimeRulesSpec.md | Will budget diagnostics emission and formatting costs using the rules defined here. |
| 15_TestAndValidationSpec.md | Will turn this specification into executable diagnostics validation, snapshots, analyzer checks, and CI coverage. |

11 is the shared diagnostics substrate.
It must not re-own domain semantics already owned elsewhere.

---

## Current Diagnostics Debt Observations

この節は現行コードベースの diagnostics 負債の観測結果をまとめる。
ここは target policy ではなく、移行元の整理である。

### Observation Traceability

| Observation | Evidence Type | Expected Downstream |
|---|---|---|
| `LTSLog` is a thin runtime-controllable wrapper over Unity Debug APIs. | Source | 11, 13 |
| Command VNext formats rich command errors but still emits directly to Unity Debug. | Source | 09, 11 |
| `CommandExecutorRegistry` reports invalid or duplicate IDs by direct `Debug.LogError`. | Source | 09, 11 |
| Save keeps a separate logger abstraction, but `UnitySaveLogger` still calls Unity Debug APIs directly. | Source | 10, 11 |
| Dynamic runtime logging utilities already carry structured context, but producer paths still decide final host formatting and emission. | Source | 10, 11 |
| Monitor and command runtime contain many inline `Debug.Log*` and `Debug.LogException` paths in hot or semi-hot runtime flows. | Source | 09, 11, 14 |
| Diagnostics identity is often implicit in message text rather than stable diagnostic codes. | Source | 11, 15 |
| Exception output and failure boundary routing are not unified across subsystems. | Source | 11, 15 |

### Representative Anchors

- [LTSLog.cs](../../GameLib/Script/Common/LTS/LTSLog.cs) - thin wrapper over Unity `Debug.Log`, `Debug.LogWarning`, and `Debug.LogError`
- [UnityCommandResolveLogger.cs](../../GameLib/Script/Common/Commands/VNext/Core/UnityCommandResolveLogger.cs) - command-specific rich formatting plus direct Unity output
- [CommandExecutorRegistry.cs](../../GameLib/Script/Common/Commands/VNext/Core/CommandExecutorRegistry.cs) - invalid/duplicate command ID logging through direct Unity error output
- [ISaveLogger.cs](../../GameLib/Script/Common/Variables/Save/Core/ISaveLogger.cs) - save-specific logger abstraction outside unified kernel diagnostics
- [UnitySaveLogger.cs](../../GameLib/Script/Common/Variables/Save/Unity/UnitySaveLogger.cs) - save log output directly to Unity Debug APIs
- [DynamicRuntimeLogUtility.cs](../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicRuntimeLogUtility.cs) - reusable structured context formatting helpers that should move behind sink-specific rendering
- [ExpressionRuntimeLogger.cs](../../GameLib/Script/Common/Variables/Dynamic/Expression/ExpressionRuntimeLogger.cs) - structured context present, final output still direct `Debug.LogError`
- [MonitorChannelRuntime.cs](../../GameLib/Script/Common/Commands/Core/MonitorChannelRuntime.cs) - scattered direct warnings, errors, and exception logs in runtime rule execution

### Current Gaps

The current project exposes the following diagnostics gaps that 11 must close:

- subsystem-specific loggers decide final host output instead of producing structured records
- the same runtime failure family may be represented by different message formats in different subsystems
- message text often acts as identity
- exception output bypasses shared routing
- profile-aware diagnostics policy is fragmented
- direct Unity logging appears inside runtime behavior paths and registry validation paths
- structured context exists in places, but there is no shared record model or sink model
- diagnostics de-duplication and throttling are ad hoc or absent

---

## Root Diagnostics Problems

Current diagnostics debt is not one missing utility class.
It is a structural architecture problem.

The core problems are:

1. sink ownership is fragmented
2. message strings act as identity
3. source provenance is inconsistent
4. exceptions are treated as output, not data
5. diagnostics performance policy is not unified

### 1. Fragmented Sink Ownership

Different subsystems own their own final Unity output path.

Examples:

- `LTSLog` forwards directly to Unity
- command-specific logger emits directly to Unity
- save-specific logger emits directly to Unity
- runtime command and monitor paths call `Debug.Log*` inline

This prevents consistent profile policy, de-duplication, testing, and migration control.

### 2. Message-Text Identity

Many current errors are identified by formatted strings, colors, or section headers rather than stable diagnostic codes.

This makes tests fragile and prevents deterministic diagnostics behavior when wording changes.

### 3. Inconsistent Provenance

Some paths capture source or runtime context, while others only emit text.

The target kernel requires stable mapping from runtime identities to human-readable source information.
Current diagnostics do not provide that consistently.

### 4. Exception-as-Output

Exceptions are often logged directly to Unity as side effects.

This bypasses shared routing, shared profile policy, failure-boundary handling, and deterministic test capture.

### 5. Missing Diagnostics Cost Contract

Current diagnostics formatting and output paths are not governed by a shared hot-path policy.

This makes it too easy for expensive string construction or spammy output to leak into runtime execution paths.

---

## DebugMap Definition

DebugMap is a generated artifact that maps verified runtime identities to human-readable source information.

DebugMap is used by:

- runtime diagnostics
- editor navigation
- validation reports
- generation reports
- boot failure reporting
- test snapshots
- migration reports

DebugMap is not runtime truth.
DebugMap is provenance metadata attached to verified runtime identities.

### Required DebugMap Role

```text
Runtime uses verified identities for execution.
DebugMap resolves those identities for humans.
```

DebugMap must not be used as:

- runtime service lookup by debug name
- runtime command lookup by authoring key
- runtime value lookup by stable key
- boot-time reconstruction of missing provenance
- fallback repair for missing required runtime inputs

### DebugMap Logical Model

Explanatory sketch:

```csharp
public sealed class KernelDebugMap
{
    public ArtifactSetId ArtifactSetId;
    public int FormatVersion;
    public Hash128 SourceHash;
    public Hash128 DebugMapHash;
    public DebugMapEntry[] Entries;
    public SourceLocationEntry[] SourceLocations;
    public ArtifactIdentityEntry[] Artifacts;
}
```

This sketch is explanatory.
03 owns generation mechanics and publication.
11 owns the logical runtime contract and required fields.

---

## Diagnostic Identity and Coverage Model

Runtime-facing identities must be debuggable through DebugMap.

Required coverage includes at least:

- `ModuleId`
- `ServiceId`
- `ScopeAuthoringId`
- `ScopePlanId`
- `LifecyclePlanId` when lifecycle plans are runtime-visible
- `LifecycleStepId`
- `CommandTypeId`
- `CommandExecutorId` when executor identity is runtime-visible
- `CommandPayloadSchemaId`
- `ValueKeyId`
- `ValueSchemaId`
- `RuntimeQueryId`
- `ArtifactSetId`
- generated artifact identity when referenced by diagnostics

`ScopeHandle` is partly different.
DebugMap resolves the verified scope authoring and plan identity behind a handle.
Live handle generation and instance state are supplied by runtime state, not by static DebugMap alone.

### Minimum DebugMap Entry

Each DebugMap entry must include enough information to trace one verified runtime identity back to source.

Minimum fields:

- numeric or symbolic runtime identity
- identity kind
- stable debug name
- owner module
- source location reference
- profile availability
- artifact identity or artifact hash
- legacy origin when applicable

Recommended additional fields:

- authoring label
- category or subtype
- generated projection kind
- related identity references

### Coverage Rules

Required rules:

- missing required coverage is a generation or validation failure where 03 and 04 say it is required
- runtime diagnostics must treat missing coverage as diagnostics degradation
- Development and Test profiles treat diagnostics degradation as an error
- Release profile may reduce metadata only when failures remain stable, interpretable, and source-resolvable to the minimum level allowed by 00 and 05

DebugMap coverage must remain deterministic.
Coverage must not depend on host order, runtime discovery, or editor display state.

---

## SourceLocation Model

SourceLocation represents the human-traceable origin of authoring, generated, or migrated data.

Explanatory sketch:

```csharp
public readonly struct SourceLocationId
{
    public readonly int Value;
}

public sealed class SourceLocationEntry
{
    public SourceLocationId Id;
    public SourceLocationKind Kind;
    public string AssetPath;
    public string AssetGuid;
    public long LocalObjectId;
    public string ComponentType;
    public string PropertyPath;
    public string GeneratedSource;
    public string DisplayLabel;
}
```

Required SourceLocation capabilities:

- trace to scene, prefab, ScriptableObject, generated artifact, or generated source location
- identify enough authoring context for human debugging
- remain stable enough for diagnostics snapshots and migration reports

SourceLocation must support at least the following origin kinds:

- scene object
- prefab object
- prefab variant object
- ScriptableObject asset
- generated code location
- generated asset location
- migration-produced legacy origin

SourceLocation does not authorize runtime discovery.
It exists for diagnostics and traceability only.

---

## Runtime Identity Mapping

Diagnostics may contain runtime identities from multiple domains.

Explanatory sketch:

```csharp
public enum RuntimeIdentityKind
{
    None = 0,
    Module = 10,
    Service = 20,
    ScopeAuthoring = 30,
    ScopePlan = 40,
    ScopeHandle = 50,
    LifecyclePlan = 60,
    LifecycleStep = 70,
    CommandType = 80,
    CommandExecutor = 90,
    CommandPayloadSchema = 100,
    ValueKey = 110,
    ValueSchema = 120,
    RuntimeQuery = 130,
    ArtifactSet = 140,
    GeneratedArtifact = 150,
}

public readonly struct RuntimeIdentityRef
{
    public readonly RuntimeIdentityKind Kind;
    public readonly int Value;
    public readonly int Generation;
}
```

Rules:

- every runtime identity carried in diagnostics must have an explicit kind
- generation is required when the identity is generation-sensitive, such as handle-like identities
- diagnostics must not rely on ambiguous bare integers

Examples:

- `ServiceId 100`
- `CommandTypeId 250`
- `ValueKeyId 300`
- `ScopeHandle index=10 generation=2`
- `LifecycleStepId 90`

---

## Artifact Identity Mapping

Diagnostics must be able to refer to artifact-set and generated-artifact identity when failures involve generation, validation, boot compatibility, or stale artifact handling.

Explanatory sketch:

```csharp
public readonly struct ArtifactSetId
{
    public readonly int Value;
}

public readonly struct GeneratedArtifactId
{
    public readonly int Value;
}
```

Artifact identity mapping must support:

- artifact set compatibility diagnostics
- stale artifact diagnostics
- generation report correlation
- boot manifest rejection diagnostics
- test snapshot traceability

Artifact identity must not be replaced by only a file path.
Stable artifact identity and compatibility hashes must remain available.

---

## Diagnostic Pipeline Definition

Diagnostics flow through a central pipeline.

```text
Producer
  -> KernelDiagnosticBuilder or typed diagnostic adapter
  -> KernelDiagnosticService
  -> DiagnosticProcessor pipeline
  -> DiagnosticSink(s)
  -> Unity / File / Editor / Test / Remote outputs
```

Representative producers include:

- boot
- verified plan generation
- dependency validation
- service graph runtime
- scope graph runtime
- lifecycle dispatcher
- command runner and command catalog
- value store and value init
- runtime query
- save system
- legacy compatibility adapters

### Producer Rules

Producers may:

- detect failures or warnings
- create typed diagnostic payloads
- attach runtime identity, source, and artifact context
- batch diagnostics before flush when a bounded local buffer is required

Producers must not:

- call Unity Debug APIs directly in target-kernel paths
- decide final Unity console formatting
- represent the failure only as a formatted message string
- swallow exceptions without reporting a diagnostic when the exception matters to failure behavior

### Local Buffer Rule

An approved local diagnostic buffer is allowed when:

- batch reporting materially reduces overhead
- buffering does not change failure meaning
- ordering remains deterministic enough for the consumer
- the buffer flushes to `KernelDiagnosticService`

Local buffering must not create a parallel diagnostics architecture.

---

## Central Logging Rule

Unity Debug APIs are allowed only inside approved diagnostic sinks.

In the target kernel, the only Unity-facing sink is:

- `UnityLogDiagnosticSink`

The following are forbidden outside approved sinks:

- `Debug.Log`
- `Debug.LogWarning`
- `Debug.LogError`
- `Debug.LogException`
- subsystem-specific Unity logger as a final output path
- `LTSLog.LogError` as a final output path

This is a root architecture rule, not a formatting preference.

```text
Errors are produced where they occur.
Unity logging is emitted only by the central diagnostic sink.
```

If a subsystem calls `Debug.LogError` directly in target-kernel architecture, diagnostics architecture has regressed.

Migration-only exceptions, if any, must be isolated by 13 and remain explicit, measurable, and temporary.

---

## Diagnostic Code Governance

DiagnosticCode is the stable identity of a diagnostic family.

Message text is not identity.

Explanatory sketch:

```csharp
public readonly struct DiagnosticCode
{
    public readonly int Value;
}
```

This sketch does not require the final implementation to expose raw integers publicly.
The architecture requirement is stable identity, not one exact public type.

### Diagnostic Code Rules

Every diagnostic code must have:

- stable symbolic identity for specification and testing
- one owning domain
- one meaning
- stable failure meaning across message wording changes

Optional generated numeric representation is allowed for runtime efficiency, provided that:

- symbolic identity remains stable in documentation and tests
- numeric mapping is deterministic
- DebugMap or a related diagnostics table can explain the mapping when needed

### Ownership Split for Codes

11 owns:

- the shared model for diagnostic identity
- naming and allocation rules
- reserved shared diagnostics families such as diagnostics degradation and sink violations

Owner specs such as 04, 06, 07, 08, 09, and 10 own:

- their domain-specific code families
- the semantics of those failures
- the minimum provenance fields required for those failures

11 must not erase domain ownership by centralizing every subsystem rule in one list.

### Reserved Shared Diagnostics Families

Representative shared diagnostics families include:

- `DIAG_CODE_MISSING`
- `DIAG_DOMAIN_MISSING`
- `DIAG_FAILURE_BOUNDARY_MISSING`
- `DIAG_SOURCE_LOCATION_MISSING`
- `DIAG_DEBUGMAP_ENTRY_MISSING`
- `DIAG_DEBUGMAP_HASH_MISMATCH`
- `DIAG_DIRECT_UNITY_LOG_FORBIDDEN`
- `DIAG_EXCEPTION_SWALLOWED`
- `DIAG_MESSAGE_ONLY_RECORD_FORBIDDEN`
- `DIAG_DEDUPLICATION_CONFIGURATION_INVALID`

---

## Diagnostic Severity Model

Diagnostic severity is independent from failure boundary.

Explanatory model:

```csharp
public enum DiagnosticSeverity
{
    Trace = 10,
    Info = 20,
    Warning = 30,
    Error = 40,
    Fatal = 50,
}
```

Rules:

- severity describes seriousness of the diagnostic record
- severity does not by itself define what runtime boundary stops
- profile may suppress `Trace` or `Info` output
- profile must not silently suppress required `Error` or `Fatal` reporting

Example:

- `Error` may fail one command frame
- `Error` may fail one scope operation
- `Fatal` may fail kernel boot

---

## Diagnostic Failure Boundary Model

Failure boundary defines where execution must stop or be invalidated.

Explanatory model:

```csharp
public enum DiagnosticFailureBoundary
{
    None = 0,
    Operation = 10,
    Command = 20,
    CommandFrame = 30,
    Scope = 40,
    Scene = 50,
    Kernel = 60,
    Build = 70,
}
```

Rules:

- failure boundary must be explicit when a diagnostic participates in failure handling
- severity must not be used as an implicit substitute for failure boundary
- owner specs continue to define which failures map to which boundaries in their domain
- 11 defines the shared representation used to carry that decision

---

## Diagnostic Domain and Category Model

Every diagnostic must belong to one domain.
Diagnostics should also belong to one category.

Domain is the subsystem area.
Category is the narrower error family.

Explanatory domain model:

```csharp
public enum DiagnosticDomain
{
    Kernel = 10,
    Boot = 20,
    Generation = 30,
    Validation = 40,
    ServiceGraph = 50,
    ScopeGraph = 60,
    Lifecycle = 70,
    Command = 80,
    Value = 90,
    RuntimeQuery = 100,
    Save = 110,
    UnityBridge = 120,
    Diagnostics = 130,
    LegacyCompat = 900,
}
```

Category may be represented by a stable symbolic or numeric family owned by the domain owner.

Rules:

- every diagnostic must have exactly one primary domain
- category must not contradict domain ownership
- shared infrastructure diagnostics may use `Diagnostics` as their domain

---

## Diagnostic Event, Correlation, and Session Model

Diagnostic identity has multiple layers.

- `DiagnosticCode` identifies the failure family
- `DiagnosticEventId` identifies one emitted event instance
- `DiagnosticCorrelationId` links related events
- `DiagnosticSessionId` groups events from one higher-level activity

Explanatory sketch:

```csharp
public readonly struct DiagnosticEventId
{
    public readonly long Value;
}

public readonly struct DiagnosticCorrelationId
{
    public readonly long Value;
}

public readonly struct DiagnosticSessionId
{
    public readonly long Value;
}
```

Representative session scopes:

- one boot attempt
- one generation run
- one validation run
- one command frame
- one save operation

Rules:

- event identity need not be stable across runs
- correlation must be stable enough within one session to connect related records
- tests should assert stable code and relevant context before asserting event-instance details

---

## Diagnostic Record Model

KernelDiagnostic is the shared structured record reported by target-kernel subsystems.

Explanatory sketch:

```csharp
public sealed class KernelDiagnostic
{
    public DiagnosticEventId EventId;
    public DiagnosticSessionId SessionId;
    public DiagnosticCorrelationId CorrelationId;

    public DiagnosticCode Code;
    public DiagnosticSeverity Severity;
    public DiagnosticDomain Domain;
    public DiagnosticFailureBoundary FailureBoundary;

    public string Message;
    public DiagnosticContext Context;
    public DiagnosticPayload Payload;
    public DiagnosticExceptionInfo Exception;
}
```

Required properties of the record model:

- message text is optional display data, not the only semantic data
- code, domain, severity, and context are first-class data
- payload must be structured enough for testing and non-Unity sinks
- exception data must be capturable without direct host output

### Message Policy

Message may exist for display and summary.
Message must not be the only diagnostic data.

Forbidden patterns:

- message-only diagnostic with no code
- message-only diagnostic with no domain
- message-only diagnostic with no provenance when provenance is required

Tests should prefer:

- diagnostic code
- domain
- failure boundary
- relevant context fields

over exact message-string matching.

---

## Diagnostic Context Model

DiagnosticContext carries shared structured context used across domains.

Explanatory sketch:

```csharp
public sealed class DiagnosticContext
{
    public ModuleId OwnerModule;
    public SourceLocationId Source;
    public ArtifactSetId ArtifactSet;
    public int ProfileId;
    public RuntimeIdentityRef[] RuntimeIdentities;
    public DiagnosticCorrelationId CorrelationId;
    public string Phase;
}
```

Required context dimensions:

- owner module when applicable
- selected profile
- source location when applicable
- artifact-set context when applicable
- runtime identity references when applicable
- phase or operation label when required by the domain

Context must remain structured.
It must not be flattened only into one formatted summary string.

### Context Rules

- owner specs define which provenance fields are mandatory for their domain failures
- 11 defines the shared representation and minimum compatibility rules
- missing required source location or required runtime identity is diagnostics degradation unless the missing item is itself the failure being reported

---

## Diagnostic Payload Model

Diagnostic payload carries domain-specific structured data that does not belong in the shared context header.

Payload may be represented as:

- typed domain-specific structs
- deterministic key-value records
- batch-oriented diagnostic entries for generation or validation

Payload must satisfy:

- deterministic field meaning
- testability without Unity string formatting
- compatibility with non-Unity sinks

Payload must not satisfy meaning only through rich-text formatting.

Representative payload contents include:

- expected and actual hash
- requested and resolved contract
- expected and actual value kind
- duplicate ID and conflicting owner
- timeout duration and policy
- cancellation reason

---

## Exception Capture Model

Exceptions are diagnostics data, not autonomous output paths.

Explanatory sketch:

```csharp
public sealed class DiagnosticExceptionInfo
{
    public string ExceptionType;
    public string Message;
    public string StackTrace;
    public DiagnosticExceptionInfo Inner;
}
```

Required rules:

- subsystems must capture exception data into `KernelDiagnostic` when the exception matters to failure behavior or observability
- subsystems must not call `Debug.LogException` directly in target-kernel paths
- exceptions must not be swallowed without diagnostics when the failure remains relevant
- cancellation exceptions may map to cancellation diagnostics instead of generic error diagnostics when the owner spec allows it

Recommended rules:

- preserve exception type, message, and stack
- preserve inner-exception chain when available
- keep exception capture deterministic enough for tests when a test sink is used

---

## KernelDiagnosticService Contract

KernelDiagnosticService is the central entry point for reporting diagnostics.

Subsystems must report diagnostics through `KernelDiagnosticService` or an approved local buffer that flushes to it.

Explanatory sketch:

```csharp
public interface IKernelDiagnosticService
{
    void Report(in KernelDiagnostic diagnostic);
    void ReportBatch(ReadOnlySpan<KernelDiagnostic> diagnostics);
    DiagnosticSessionHandle BeginSession(DiagnosticSessionInfo info);
    void EndSession(DiagnosticSessionHandle handle);
}
```

Required service behavior:

- accept individual and batch diagnostics
- preserve deterministic enough ordering for the selected sink and host policy
- support session and correlation metadata
- forward diagnostics through the configured processing pipeline and sinks

KernelDiagnosticService must not:

- be bypassed by subsystem-specific final sinks
- perform Unity console formatting in producer code paths
- silently drop required `Error` or `Fatal` diagnostics

---

## Diagnostic Processor Contract

Diagnostic processing sits between reporting and final sinks.

The processing stage may perform:

- diagnostics enrichment
- de-duplication
- throttling
- aggregation
- profile filtering
- sink fan-out preparation

Processing must not:

- mutate diagnostic meaning
- erase required failure boundaries
- silently drop the first occurrence of a required error
- convert one domain's failure into another domain's meaning

Processing is a policy layer, not a semantic rewrite layer.

---

## DiagnosticSink Contract

DiagnosticSink consumes `KernelDiagnostic` records.

Allowed sink families include:

- `UnityLogDiagnosticSink`
- `FileDiagnosticSink`
- `EditorDiagnosticSink`
- `TestDiagnosticSink`
- `InMemoryDiagnosticSink`
- `RemoteDiagnosticSink`

Explanatory sketch:

```csharp
public interface IKernelDiagnosticSink
{
    void Emit(in KernelDiagnostic diagnostic);
    void Flush();
}
```

Rules:

- sinks may render the same structured diagnostic differently for different hosts
- sinks must not redefine diagnostic semantics
- sink choice must not change whether a failure is considered an error or fatal condition

---

## UnityLogDiagnosticSink Policy

UnityLogDiagnosticSink is the only target-kernel component allowed to call Unity Debug APIs directly.

It maps `KernelDiagnostic` records to Unity console output according to profile policy.

Representative severity mapping:

- `Trace` -> usually suppressed or routed only when explicitly enabled
- `Info` -> `Debug.Log`
- `Warning` -> `Debug.LogWarning`
- `Error` -> `Debug.LogError`
- `Fatal` -> `Debug.LogError` with fatal marker or equivalent stable formatting rule

### Exception Output Rule

If the diagnostic contains exception information, `UnityLogDiagnosticSink` may:

- call `Debug.LogException`
- render exception text inside one `Debug.LogError`

The choice must be deterministic for the selected profile and sink configuration.

No other subsystem may make that decision directly.

### Rendering Rule

Rich text, section formatting, and console emphasis belong to sink-specific rendering.

Producer paths must not depend on Unity rich-text rendering for semantic meaning.

This allows:

- rich Unity console output in Development
- compact Unity output in Release
- structured snapshots in tests
- non-Unity file or remote sinks without information loss

---

## Profile-Based Diagnostics Policy

Diagnostics behavior is profile-aware.

Required profile kinds are aligned with 05:

- Development
- Release
- Test

### Profile Matrix

| Policy | Development | Release | Test |
|---|---|---|---|
| Full source mapping | Required | Reduced allowed only within 00/05 limits | Required |
| DebugMap degradation | Error | Allowed only with stable code and interpretable identity | Error or Fatal depending on boundary |
| Trace / Info output | Enabled or configurable | Usually suppressed | Captured when required by test policy |
| Exception detail | Full | Minimal required or policy-limited | Full captured |
| Rich Unity formatting | Allowed | Optional | Not required |
| Test sink capture | Optional | Optional | Required |
| Silent fallback | Forbidden | Forbidden | Forbidden |

Profile may change:

- output detail
- sink routing
- verbosity
- exception rendering detail

Profile must not change:

- diagnostic identity
- required failure boundary
- whether invalid runtime input is considered valid

---

## Diagnostics Degradation Policy

Diagnostics degradation occurs when required diagnostic information is missing or unusable.

Representative degradation conditions include:

- missing `DiagnosticCode`
- missing required domain
- missing required failure boundary
- missing required source location
- missing required DebugMap entry
- missing required runtime identity kind
- message-only record for a failure that requires structured data

Rules:

- diagnostics degradation must itself be representable as a diagnostic
- Development and Test profiles treat diagnostics degradation as an error unless a lower spec defines a stricter rule
- Release profile may allow reduced detail only when stable code and interpretable identity remain available

Diagnostics degradation must not be hidden by sink formatting or profile filtering.

---

## De-duplication, Throttling, and Aggregation Policy

Diagnostics may be de-duplicated, throttled, or aggregated by the processor or sink pipeline.

Representative de-duplication key parts may include:

- `DiagnosticCode`
- `DiagnosticDomain`
- `SourceLocationId`
- `RuntimeIdentityRef`
- selected profile

Rules:

- the first occurrence must not be hidden
- de-duplication must not change failure boundary
- throttling must not suppress the fact that a fatal or required error occurred
- aggregation summaries must remain source-traceable enough for debugging

Representative use cases:

- repeated tick failure in lifecycle or monitor loops
- repeated value type mismatch under one broken authored binding
- repeated command timeout under one detached or invalid execution policy

---

## Diagnostics Performance Policy

Diagnostics must be safe for runtime hot paths.

Requirements:

- producer paths should avoid expensive string formatting
- rich text formatting should be sink-local where possible
- repeated diagnostics should support throttling or aggregation
- disabled `Trace` or suppressed `Info` paths should avoid avoidable allocation where practical
- no LINQ in hot diagnostics reporting path
- batch reporting should be supported for generation and validation

Performance must not be achieved by:

- dropping required structured context
- removing stable error codes
- removing required DebugMap coverage
- hiding failure boundaries

Observability is part of the runtime contract.

---

## Subsystem Integration Contract

11 integrates multiple subsystem owners under one diagnostics substrate.

Rule:

```text
Owner specs define what a failure means.
11 defines how that failure is represented, routed, resolved through DebugMap, and emitted.
```

### Generation Diagnostics Integration

Generation diagnostics produced under 03 must use a record shape compatible with `KernelDiagnostic`.

03 continues to own:

- generation failure meaning
- generation completeness rules
- artifact consistency rules

11 owns:

- shared record compatibility
- sink separation
- code identity governance
- DebugMap runtime contract used by generation outputs

Generation may use batch diagnostics heavily.

### Validation Diagnostics Integration

Validation diagnostics produced under 04 must use a record shape compatible with `KernelDiagnostic`.

04 continues to own:

- dependency failure meaning
- validation phase semantics
- validation status classification

11 owns:

- shared record compatibility
- stable diagnostics substrate
- sink and output separation

### Boot Diagnostics Integration

Boot diagnostics produced under 05 must use the 11 record model.

05 continues to own:

- boot acceptance gates
- boot failure boundaries
- profile selection and boot policy

11 owns:

- central diagnostics routing
- DebugMap runtime contract used by boot reporting
- sink policy and Unity output centralization

`BootDiagnosticsPolicy` may configure boot-specific presentation or capture behavior.
It must not define a parallel diagnostics architecture.

### ServiceGraph Diagnostics Integration

06 defines required service failure provenance and behavior.
11 requires those diagnostics to be emitted as `KernelDiagnostic` records.

Required integration rules:

- service diagnostics must include the provenance fields required by 06
- service diagnostics must not bypass `KernelDiagnosticService`
- service diagnostics may resolve service identities through DebugMap for human-readable output

### ScopeGraph Diagnostics Integration

07 defines required scope failure provenance and behavior.
11 requires those diagnostics to be emitted as `KernelDiagnostic` records.

Required integration rules:

- handle-like diagnostics must carry generation-aware runtime identity data
- DebugMap resolves verified scope plan and authoring identity
- runtime state supplies live handle instance information

### Lifecycle Diagnostics Integration

08 defines required lifecycle provenance and failure behavior.
11 requires those diagnostics to be emitted as `KernelDiagnostic` records.

Required integration rules:

- lifecycle diagnostics must carry lifecycle step provenance required by 08
- timeout and cancellation must be expressible without ad hoc text-only logging
- automatic handler discovery failure must remain expressible as structured diagnostics

### Command Diagnostics Integration

09 defines required command diagnostics fields and failure behavior.
11 defines the shared substrate used to emit them.

Required command context includes the fields required by 09, including at minimum:

- `CommandTypeId`
- authoring key when allowed by 09
- payload schema identity
- executor identity
- execution frame or equivalent command-local execution context
- actor and target references when available
- source location

Migration direction:

```text
UnityCommandResolveLogger
  -> CommandDiagnosticAdapter
  -> KernelDiagnosticService
  -> UnityLogDiagnosticSink
```

Command-specific rich formatting may survive as a sink renderer.
It must not remain a producer-owned Unity logger.

### Value Diagnostics Integration

10 defines required value diagnostics fields and failure behavior.
11 defines the shared substrate used to emit them.

Required value context includes the fields required by 10, including at minimum:

- `ValueKeyId`
- `ValueSchemaId`
- store identity and store scope when relevant
- value kind and revision context when relevant
- source location

Value diagnostics must not use stable key as runtime truth.
Stable key may appear only as diagnostics metadata or migration metadata where 10 allows it.

### RuntimeQuery Diagnostics Integration

RuntimeQuery diagnostics must use `KernelDiagnostic`.

Required context includes at minimum:

- query identity or query kind
- requested target identity
- ambiguity or missing-result classification
- owner module
- source location when the query comes from a verified authored request

RuntimeQuery failure meaning remains owned by the spec that defines runtime query semantics.
11 defines the shared diagnostics substrate and sink rules used to emit those failures.

### Save Diagnostics Integration

Save has no dedicated v2 spec in the current doc set.
Therefore 11 defines the temporary shared diagnostics contract for Save-domain failures without taking ownership of save format semantics.

Save diagnostics must use `KernelDiagnostic`.

Required save context includes at minimum:

- save operation identity or label
- save slot, profile, or target
- owner scope or runtime target when relevant
- `ValueKeyId` or entity/runtime identity when relevant
- source location when authored save metadata exists
- exception payload when an exception participates in failure behavior

Migration direction:

```text
ISaveLogger / UnitySaveLogger
  -> SaveDiagnosticReporter or SaveDiagnosticAdapter
  -> KernelDiagnosticService
  -> UnityLogDiagnosticSink
```

11 does not define save storage semantics or save format.
It defines only how save failures and warnings enter the unified diagnostics substrate.

---

## Legacy Migration Policy

Legacy logging paths should become adapters into the unified diagnostics substrate.

| Legacy Pattern | Target Representation |
|---|---|
| `Debug.LogError(...)` in subsystem | `KernelDiagnosticService.Report(KernelDiagnostic)` |
| `LTSLog.LogError(...)` | legacy diagnostics adapter or direct reporter into `KernelDiagnosticService` |
| `UnityCommandResolveLogger` | `CommandDiagnosticAdapter` |
| `CommandExecutorRegistry` direct Unity log | command diagnostics record with command-domain code |
| `ISaveLogger` / `UnitySaveLogger` | save diagnostics reporter |
| dynamic or expression runtime direct Unity log | typed diagnostics payload plus central sink rendering |
| direct exception log | exception payload plus central sink policy |

13 owns whether a temporary legacy adapter remains allowed.
11 defines the target representation that adapters must feed.

---

## Forbidden Patterns

The following are forbidden in the target diagnostics architecture:

- `Debug.LogError` outside `UnityLogDiagnosticSink`
- `Debug.LogWarning` outside approved sinks
- `Debug.LogException` outside approved sinks
- subsystem-specific Unity logger as a final output path
- error represented only as formatted string
- exception swallowed without diagnostics when failure remains relevant
- diagnostic without `DiagnosticCode`
- diagnostic without domain
- required failure boundary inferred from severity only
- runtime-facing ID without DebugMap resolution in Development or Test when coverage is required
- diagnostics pipeline bypass for command or save failures
- separate incompatible error shapes for generation, validation, and runtime failures
- using DebugMap as runtime lookup truth or fallback repair

---

## Test Case Model

Each diagnostics test case must define:

- Test ID
- Title
- Input or fixture
- Selected profile
- Expected diagnostics codes
- Expected domains
- Expected failure boundary when applicable
- Expected provenance fields
- Expected sink behavior when relevant

Recommended fixture format:

```md
### TC_DIAG_001_Example

Input:
- ...

Profile:
- Development

Expected:
- Diagnostic: ...
- Domain: ...
- FailureBoundary: ...
- Required Context: ...
```

Tests should prefer stable code and structured context assertions over full message-string identity.

---

## Required Test Cases

### Central Logging Tests

#### TC_DIAG_LOG_001_SubsystemCannotCallDebugLogError

```text
Input:
- subsystem code path attempts direct Debug.LogError

Expected:
- analyzer or forbidden API validation fails
- DIAG_DIRECT_UNITY_LOG_FORBIDDEN
```

#### TC_DIAG_LOG_002_UnitySinkEmitsUnityOutput

```text
Input:
- KernelDiagnostic severity = Error
- UnityLogDiagnosticSink enabled

Expected:
- exactly one logical Unity-side error emission according to sink policy
```

#### TC_DIAG_LOG_003_CommandErrorUsesCentralPipeline

```text
Input:
- command executor missing

Expected:
- Domain = Command
- DiagnosticCode = COMMAND_EXECUTOR_MISSING
- emitted through KernelDiagnosticService
- no command-specific direct Unity logger as final output path
```

### DebugMap Tests

#### TC_DIAG_MAP_001_ServiceIdResolved

```text
Input:
- diagnostic contains ServiceId
- DebugMap has matching entry

Expected:
- output includes service debug name and source location
```

#### TC_DIAG_MAP_002_MissingDebugMapEntryFailsInDevelopment

```text
Profile:
- Development

Input:
- diagnostic contains runtime-facing identity
- required DebugMap entry is missing

Expected:
- DIAG_DEBUGMAP_ENTRY_MISSING
- diagnostics degradation treated as error
```

#### TC_DIAG_MAP_003_RuntimeHandleIncludesGeneration

```text
Input:
- diagnostic contains ScopeHandle index and generation

Expected:
- emitted diagnostics include both index and generation
- output remains human-readable through DebugMap plus runtime handle data
```

### Structured Payload Tests

#### TC_DIAG_PAYLOAD_001_MessageIsNotOnlyData

```text
Input:
- diagnostic has message but no code

Expected:
- failed
- DIAG_CODE_MISSING
```

#### TC_DIAG_PAYLOAD_002_ExceptionCapturedAsPayload

```text
Input:
- subsystem catches exception

Expected:
- KernelDiagnostic contains exception type, message, and stack
- subsystem does not call Debug.LogException directly
```

### Integration Tests

#### TC_DIAG_COMMAND_001_CommandResolveFailure

```text
Input:
- command resolve fails

Expected:
- Domain = Command
- Code = COMMAND_RUNTIME_QUERY_MISSING or another command-owned resolve failure code
- Context includes command source and execution context required by 09
```

#### TC_DIAG_SAVE_001_SaveFailure

```text
Input:
- save operation throws exception

Expected:
- Domain = Save
- save-domain code is present
- exception payload present
- Unity output only through UnityLogDiagnosticSink
```

#### TC_DIAG_VALUE_001_ValueTypeMismatch

```text
Input:
- ValueStore write type mismatch

Expected:
- Domain = Value
- Code = VALUE_TYPE_MISMATCH
- Context includes ValueKeyId and schema identity
```

### De-duplication and Throttling Tests

#### TC_DIAG_THROTTLE_001_FirstOccurrenceAlwaysEmitted

```text
Input:
- same error repeated 100 times

Expected:
- first occurrence emitted
- repeated occurrences summarized or throttled by policy
```

#### TC_DIAG_THROTTLE_002_FailureBoundaryNotSuppressed

```text
Input:
- fatal diagnostic repeated

Expected:
- failure boundary still applies
- output may be throttled
- failure meaning is not suppressed
```

---

## Acceptance Criteria

This specification is complete when it defines:

- DebugMap purpose and coverage requirements
- SourceLocation and runtime/artifact identity mapping
- KernelDiagnostic record model
- DiagnosticCode governance
- severity, domain, category, and failure-boundary model
- central diagnostics pipeline
- KernelDiagnosticService contract
- DiagnosticSink contract
- UnityLogDiagnosticSink policy
- central Unity logging rule
- exception capture policy
- subsystem integration rules for Generation, Validation, Boot, ServiceGraph, ScopeGraph, Lifecycle, Command, Value, RuntimeQuery, and Save
- diagnostics degradation rules
- profile-based diagnostics policy
- de-duplication, throttling, and aggregation policy
- diagnostics performance policy
- legacy migration policy
- forbidden patterns
- required diagnostics test cases

Completion also requires that 11 not be treated as a presentation-only note.
It must remain the shared structured diagnostics substrate for Kernel v2.

---

## Final Position

DebugMap and diagnostics are not optional tooling.
They are part of the runtime contract.

DebugMap exists so verified runtime identities remain traceable to human-readable source.
Diagnostics exists so every important failure, warning, and relevant informational event can move through one structured, testable, profile-aware pipeline.

Subsystems produce diagnostics where failures occur.
Subsystems do not own Unity output.
Only the central Unity diagnostic sink may emit Unity logs.

This rule is required for correctness, scale, testing, observability, and migration control.
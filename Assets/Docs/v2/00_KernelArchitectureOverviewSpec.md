# Kernel Architecture Overview Specification

## Document Status

- Document ID: 00_KernelArchitectureOverviewSpec
- Status: Draft
- Role: top-level architecture specification for the GameLib Kernel v2 migration target
- Scope: architecture principles, trust boundary, root concepts, migration constraints, and downstream spec split
- Non-goal: this document does not finalize runtime API details, serialized layouts, or implementation algorithms owned by lower specifications

### Revision Note

This revision separates observations about the current project from v2 target policy.
It also reduces premature API fixation in the overview layer and makes KernelIR plus dependency validation the primary authority for downstream runtime specs.

---

## 目的

本仕様書は、現行の LifetimeScope / RuntimeResolver / FeatureInstaller / CommandRunner / Blackboard 系アーキテクチャを、検証可能な plan-first kernel へ移行するための最上位仕様である。

本仕様の目的は、単なる高速化ではない。
中核目的は次のとおりである。

- Runtime の曖昧な探索を削減する
- 起動時の eager 初期化コストを制御する
- Service / Scope / Command / Value / Lifecycle を明示的な runtime plan に落とす
- generated data を信頼せず、検証済み plan だけを実行する
- ID / Handle ベース設計でも debugability を失わない
- Legacy 互換層を新基盤の外周に隔離する
- Unity authoring と runtime kernel の責務を分離する
- 将来の大規模ゲームおよび複数ゲーム再利用に耐える基盤を定義する

本仕様の中心思想は以下である。

**Runtime は scene / transform / component から構造を推論しない。Editor / Build / Test で検証された Verified Plan のみを実行する。**

---

## Document Role

00 は root specification である。
したがって本書は以下を定義する。

- architecture の目的
- 現行実装から観測できる問題領域
- v2 target が守るべき原則
- trust boundary
- lower specs へ渡す責務境界

一方で、以下は 00 で最終確定しない。

- final runtime API shape
- serialized asset schema の細部
- graph validation algorithm の詳細
- command payload schema の詳細設計
- value layout と save format の詳細
- unity bridge の実装プロトコル

本書中の C# type sketch は explanatory であり、final API を意味しない。

### Normative Authority

This document does not finalize concrete runtime APIs.
However, it defines non-negotiable architectural constraints.

Lower specifications and implementation code must not violate the following 00-level constraints:

- runtime discovery must not be used as the primary composition mechanism
- lifecycle must not be inferred from service registration
- command executor discovery must not depend on bulk DI registration
- runtime stable-key fallback must not be part of the target kernel
- generated artifacts must not be trusted without validation and hash/version compatibility checks
- ID/Handle based runtime paths must preserve diagnostics through DebugMap or equivalent metadata
- legacy compatibility must remain outside the new kernel core

If a lower specification needs to violate one of these constraints, it must explicitly define:

- the reason
- the affected scope
- the validation rule
- the diagnostics behavior
- the migration or removal condition

### Terminology

- Authoring: Unity scene, prefab, ScriptableObject, or editor-facing configuration.
- KernelIR: normalized intermediate representation generated from authoring inputs.
- VerifiedKernelPlan: validated runtime execution input derived from KernelIR.
- Runtime Projection: runtime-specific plan generated from VerifiedKernelPlan.
- Target Kernel: the final v2 runtime architecture described by this spec.
- Legacy: pre-v2 LifetimeScope / RuntimeResolver / FeatureInstaller / Blackboard / CommandRunner style systems.
- Discovery: runtime search or inference of structure from scene, hierarchy, component layout, or registry fallback.
- Fallback: continuing execution by substituting inferred, default, legacy, or generated-at-runtime data after a required dependency is missing.
- Runtime Query: runtime lookup of scopes, actors, entities, or parts by explicit runtime identity.

---

## Current Architecture Observations

この節は現行コードベースの観測結果を要約する。
ここは v2 target policy ではなく、移行元の事実整理である。

### Observation Traceability

Current architecture observations must remain traceable to source code, profiling evidence, validation reports, migration issues, or design review notes.

When this document is updated, observations that no longer match the current codebase must be removed or moved to legacy migration notes.

| Observation | Evidence Type | Expected Downstream Spec |
|---|---|---|
| Scope build performs runtime installer discovery | Source / Profiling | 07, 08, 14 |
| RuntimeResolver mixes service resolution and collection behavior | Source | 06, 08 |
| Command executor registration is boot-cost sensitive | Source / Profiling | 09, 14 |
| Blackboard / Var / DynamicValue responsibilities overlap | Source | 10 |
| Boot and loading rely on scene object discovery | Source | 05, 12 |

### Representative Anchors

- [RuntimeLifetimeScope.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs) - scope build flow, parent resolution, installer discovery
- [ScopeFeatureInstallerUtility.cs](../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs) - nearest scope ownership filtering
- [RuntimeResolverHub.cs](../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs) - resolver build and acquire/release dispatch
- [CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs) - bulk command executor registration
- [CommandExecutorRegistry.cs](../../GameLib/Script/Common/Commands/VNext/Core/CommandExecutorRegistry.cs) - executor lookup registry
- [BlackboardService.cs](../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs) - current blackboard behavior
- [VarIdResolver.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs) - stable key resolution and runtime-only negative IDs
- [VarKeyRegistryLocator.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs) - Resources fallback for registry lookup
- [LoadingScreenService.cs](../../GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs) - scene transition and discovery behavior

### Scope Build and Installer Discovery

現行の RuntimeLifetimeScope 系は、scope build の中で feature installer discovery、resolver build、acquire/release dispatch など複数責務を抱えている。

ただし、これは単純な全面探索ではない。
現行 build は次の特徴を持つ。

- parent build completion を前提とした coordination がある
- subtree installer discovery は nearest scope ownership filtering を伴う
- scope parent の解決は transform hierarchy と kind 制約に依存する

この構成は、柔軟さと引き換えに runtime build path を複雑化している。

### Service Resolution and Runtime Lookup

現行では service resolution と runtime identity lookup は別システムである。

- RuntimeResolver は type-driven DI resolution を行う
- BaseLifetimeScopeRegistry 系は kind / id / category ベースの runtime lookup を担当する

この分離は重要である。
v2 は単に「DI をやめる」では足りず、service resolution と runtime query の両方を定義し直す必要がある。

### Command System

現行 command system は少なくとも次の三層を持つ。

- executor の大量登録
- runtime ID lookup 用 registry
- authoring key 解決用 catalog / resolver

したがって現行の問題は registration volume だけではない。
authoring-level key semantics と runtime dispatch semantics が二重化していることも主要課題である。

### Value and Variable Systems

現行の value 領域は単一システムではない。
少なくとも以下が分離している。

- Blackboard hierarchy
- Var registry と stable key 解決
- DynamicValue evaluation

そのため、v2 の ValueStore は単なる rename ではなく、複数責務の再定義として扱う必要がある。

### Boot and Scene Integration

現行 boot は BeforeSceneLoad entry と project/global scope 初期化に強く結びついている。
また、scene flow や loading 系は runtime scene object discovery に依存する部分を持つ。

したがって v2 boot は、単独の新 object を足せば完了する問題ではない。
boot、scene boundary、authoring bridge、legacy coexistence を一体として扱う必要がある。

---

## Root Problems

現行アーキテクチャの問題は単一の遅い処理ではない。
根本問題は、runtime が複数の責務を同時に引き受けていることである。

主要な問題を以下に整理する。

1. runtime が scene / transform / component から構造を推論している
2. scope build が discovery、registration、instantiation、lifecycle wiring をまとめて抱えている
3. service registration が lifecycle や command discovery の器になっている
4. command dispatch semantics が executor registration と authoring key resolution に分裂している
5. value / variable / dynamic evaluation の責務境界が曖昧である
6. registry / stable key 解決が runtime fallback を持つ
7. boot 導線が project/global scope と scene integration に分散している
8. validation より runtime fallback が優先される経路が残っている

この結果、機能追加に比例して以下のコストと不透明性が増加する。

- 起動時構築コスト
- scope build complexity
- registration index volume
- handler wiring complexity
- command initialization cost
- value initialization ambiguity
- scene transition 時の探索・重複排除コスト
- error 再現性の低下

### Root Cause Classification

The problems above are classified into four root causes.

#### 1. Runtime Inference

Runtime derives structure from scene objects, transform hierarchy, component layout, or registry fallback.

Examples:

- scope ownership inferred from transform hierarchy
- feature ownership inferred from nearest runtime scope
- boot objects discovered by scene-wide search

#### 2. Registration-as-Discovery

Service registration is used not only for dependency resolution, but also as a discovery mechanism for lifecycle handlers, command executors, and runtime collections.

Examples:

- lifecycle handlers collected from registrations
- command executors discovered as service registrations
- dynamic list injection used as runtime enumeration

#### 3. Mixed Lifecycle Responsibility

Build, acquire, initialization, value setup, tick registration, and debug binding are coupled in the same execution path.

Examples:

- scope build triggering resolver construction and acquire
- Blackboard initialization repeated across multiple lifecycle hooks
- loading UI creation mixed with scene scope discovery

#### 4. Unverified Runtime Fallback

Runtime continues execution by inventing or discovering missing data instead of failing through structured diagnostics.

Examples:

- runtime-only negative IDs
- resource fallback for required registries
- legacy fallback without explicit migration boundary

---

## v2 Target Principles

本節は migration target としての v2 runtime contract を定義する。
ここでの禁止事項は target kernel に対する規則であり、移行期間の一時的許可は 13 に隔離する。

### 1. Runtime discovery must not be a composition mechanism

Target kernel runtime must not use discovery as its primary composition mechanism.

The following are forbidden in target runtime paths unless a lower spec explicitly defines a bounded exception:

- broad component traversal for feature discovery
- transform parent traversal for scope ownership inference
- scene-wide search for kernel object discovery
- runtime stable-key resolution for required value keys
- scattered Resources.Load for required kernel inputs
- constructor reflection for service activation
- registration-wide scans for lifecycle, command, or executor collection
- dynamic list injection as a discovery mechanism

Allowed exceptions must define:

- allowed caller
- allowed timing
- maximum scope of search
- caching behavior
- diagnostics behavior
- performance budget
- removal condition, if the exception is migration-only

### 2. Transform hierarchy is not kernel truth

Unity hierarchy は visual / authoring / object lifetime の情報であり、kernel の親子関係の source of truth ではない。

v2 における scope parent / child は plan と runtime graph により明示される。
ただし Unity object との追跡性は維持しなければならない。

### 3. Validation precedes execution

generated output は信頼境界ではない。
信頼してよいのは、KernelIR から生成され、validation を通過し、hash と version 整合性を満たした VerifiedKernelPlan のみである。

### 4. Service lifecycle is explicit

service registration と lifecycle dispatch は別概念である。
Acquire / Release / Tick などの実行順と所有関係は、明示的 plan で定義されなければならない。

### 5. Commands are cataloged runtime operations, not a bulk DI side effect

v2 command dispatch は command identity、executor resolution、payload validation、diagnostics を explicit に扱う。
command runtime は、DI registration の副作用として executor 群を集めて成立してはならない。

### 6. Values are schema-driven runtime state

value access は schema と init plan を前提に行う。
string stable key は runtime lookup の truth ではない。
stable key は debug、migration、authoring bridge でのみ扱う。

### 7. No silent fallback

required dependency、required plan、required registry、required runtime object が欠けた場合、target kernel は silent fallback してはならない。
failure は structured diagnostics として記録される必要がある。

---

## Trust Boundary

本アーキテクチャで信頼境界を構成するのは、CodeGen ではない。
信頼境界は以下である。

1. normalized KernelIR
2. validation rules
3. VerifiedKernelPlan
4. hash / version compatibility checks
5. DebugMap-backed diagnostics

Generated code、generated assets、serialized runtime data は execution artifact であり、それ自体を source of truth とみなしてはならない。

### Artifact Consistency Model

The kernel pipeline produces multiple artifacts, but they must represent the same source state.

The following artifacts must be consistency-checked as a set:

- normalized KernelIR
- VerifiedKernelPlan
- generated code
- generated runtime assets
- KernelRegistryAsset
- KernelDebugMap
- BootManifest references

A target runtime must not execute a partial artifact set.

A valid artifact set must satisfy:

- all artifacts share the same source hash or compatible hash chain
- all artifacts declare compatible format versions
- DebugMap corresponds to the same ID space as the runtime plan
- Registry corresponds to the same ValueKey / CommandType / Service ID space
- BootManifest references exactly one compatible plan set

If consistency cannot be proven, boot must fail through structured diagnostics.

---

## Architecture Layers

v2 architecture は次の責務層を持つ。

```text
Authoring Layer
  - Scene / Prefab / ScriptableObject authoring
  - authoring links and module definitions
  - registries and profile assets

        ↓ Normalize

KernelIR
  - normalized intermediate representation
  - source locations
  - dependency edges

        ↓ Validate

VerifiedKernelPlan
  - validated runtime projection set
  - hash/version checked
  - debug-map attached

        ↓ Boot

KernelRuntime
  - service graph runtime
  - scope graph runtime
  - command catalog runtime
  - value store runtime
  - lifecycle dispatcher
  - diagnostics
```

このレイヤ分離は、実装分割ではなく trust boundary 分割である。

### One-Way Derivation Rule

Runtime projections are derived from KernelIR.
They must not become independent sources of truth.

The allowed direction is:

```text
Authoring
  -> KernelIR
  -> VerifiedKernelPlan
  -> Runtime projections
```

The reverse direction is forbidden.

Runtime plans, generated code, and debug maps must not be manually edited as authoritative data.
If a runtime projection is edited or becomes stale, it must be regenerated from KernelIR.

### Staleness Rule

Any artifact that cannot prove compatibility with the current KernelIR must be treated as stale.

Stale artifacts must not be used for runtime boot.
Editor tooling may show stale artifacts for inspection, but must mark them invalid.

---

## Core Concepts

### KernelIR

KernelIR は normalized intermediate representation である。
すべての runtime plan は KernelIR から導出される。

KernelIR は editor / build / test / CI における authority document であり、runtime が直接実行する形式ではない。

### VerifiedKernelPlan

VerifiedKernelPlan は validated runtime projection である。
これは runtime execution input であって、authoring source ではない。

VerifiedKernelPlan が満たすべき最低条件:

- generated from KernelIR
- validation error count is zero for required severities
- format and hash compatibility are satisfied
- debug map is present at the required profile level
- runtime boot check passes

### KernelRuntime

KernelRuntime は plan を実行する runtime 本体である。
KernelRuntime は runtime discovery を行わず、verified plan に従って subsystems を初期化する。

KernelRuntime may create runtime state from a VerifiedKernelPlan, but it must not mutate the verified structural plan itself.

Forbidden runtime mutations:

- adding new ServiceId definitions
- adding new CommandTypeId definitions
- adding new ValueKeyId schema entries
- changing lifecycle step ordering
- changing module dependency definitions
- changing verified scope authoring definitions

Allowed runtime mutations:

- creating and destroying runtime scope instances from verified scope plans
- updating runtime value state
- changing active / visible state
- dispatching commands defined by the verified command catalog
- creating runtime handles whose type and plan are already verified

If runtime extensibility is required, it must be represented as a pre-validated extension point in KernelIR.

### KernelBootManifest

KernelBootManifest は boot input であり、巨大な設定倉庫ではない。
その責務は runtime 開始に必要な verified inputs と policy の参照点を提供することに限る。

---

## Runtime Policies

### Runtime Error Policy

required runtime references が未解決である場合、target kernel は fallback してはならない。

対象例:

- missing ServiceId
- missing CommandTypeId
- missing ValueKeyId in schema
- invalid ScopeHandle generation
- unsatisfied module dependency
- hash mismatch between boot inputs and generated outputs

Error は structured diagnostics として記録される必要がある。
profile に応じて detail level は変化してよいが、failure を隠してはならない。

### Failure Scope

Runtime failures must define their stopping boundary.

| Failure Type | Default Boundary |
|---|---|
| Boot input hash mismatch | Boot failure |
| Missing required root service | Boot failure |
| Missing scene service | Scene kernel failure |
| Invalid ScopeHandle generation | Operation failure |
| Missing command executor | Command execution failure |
| Missing value schema | Operation failure or scope failure |
| Lifecycle step failure | Scope failure unless marked fatal |
| DebugMap missing in Development profile | Boot failure |
| DebugMap missing in Release profile | Fatal only if required diagnostics cannot be produced |

A failure boundary must be explicit.
A failure must not continue through silent fallback.

### DebugMap Policy

ID / Handle ベース設計では DebugMap を必須とする。
少なくとも fatal and error diagnostics に必要な human-readable resolution 情報は維持されなければならない。

対象 ID 例:

- ModuleId
- ScopeAuthoringId
- ScopeHandle
- ServiceId
- CommandTypeId
- ValueKeyId
- LifecycleStepId

### Minimum DebugMap Entry

Each DebugMap entry must provide enough information to trace an ID back to its authoring source.

Minimum fields:

- numeric ID
- stable debug name
- kind
- owning module
- source asset path or generated source location
- profile availability
- generated artifact hash
- optional legacy origin, if migrated from legacy data

For runtime diagnostics, an ID without a DebugMap entry must be treated as a diagnostics degradation.
In Development and Test profiles, diagnostics degradation is an error.
In Release profile, diagnostics degradation is allowed only if the failure can still be reported with a stable error code and numeric ID.

### Hash and Version Policy

runtime boot では、plan format と関連 hash の互換性を検証する。
不一致時は起動継続ではなく failure path へ入る。

### Hash Input Policy

Hash checks must represent semantic compatibility, not file timestamp compatibility.

Hash inputs should include:

- normalized KernelIR content
- module IDs and versions
- service / command / value / lifecycle ID assignments
- dependency edges
- registry content relevant to runtime IDs
- profile-affecting configuration
- generated schema version

Hash inputs must not include:

- generation timestamp
- editor-only display order unless it affects generated IDs
- non-semantic formatting
- absolute local machine paths

Lower specs must define exact hash algorithms and normalization rules.

### Profile Policy

kernel runtime は profile-aware である。

- Development: detailed diagnostics, broad debug map retention, migration warnings enabled
- Release: minimal required debug map, no silent fallback, invalid plan blocks boot
- Test: deterministic execution, diagnostics capture, validation strictness at maximum practical level

---

## Subsystem Directions

この節は lower specs への責務の受け渡しを明確にするための high-level direction である。
具体 shape は各 spec に委譲する。

### Service Graph

Service graph runtime は explicit factory、lifetime、dependency edges を扱う。
runtime reflection activation は target architecture に含めない。

00 で確定すること:

- service resolution is explicit
- lifetime is explicit
- dependency validation exists

00 で確定しないこと:

- exact factory delegate signature
- caching structure
- scope-local storage layout

### Runtime Query and Registry

Runtime query is separate from service resolution.

The target architecture must distinguish:

- service resolution: obtaining kernel services by verified service identity
- runtime query: finding runtime scopes, entities, actors, parts, or groups by runtime identity

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

### Scope Graph

Scope graph runtime は scope parent / child、state、attach / detach、spawn / despawn を明示的に扱う。
transform hierarchy は observation link には使えても source of truth にはならない。

00 で確定すること:

- scope graph exists as a first-class runtime subsystem
- scope parentage is explicit
- unity object linkage remains traceable

00 で確定しないこと:

- final handle layout
- pooling representation
- scene-boundary serialization rules

### Lifecycle Plan

lifecycle ordering は explicit step plan で定義される。
registration 走査から handler を自動抽出してはならない。

### Command Catalog

command runtime は command identity と executor resolution を explicit に持つ。
Authoring command keys are editor-facing identities.
Runtime command dispatch uses verified CommandTypeId or equivalent generated runtime identity.
Runtime command dispatch must not resolve executor identity from arbitrary strings.
Authoring keys may be preserved in DebugMap and migration metadata.
Conversion from authoring key to runtime identity happens before runtime execution, during normalization or validation.
ただし、authoring key と runtime ID の最終契約は 09 で確定する。

### Value Schema and Store

value runtime は schema-driven である。
現在分離している Blackboard、Var registry、dynamic evaluation の責務は 10 で整理する。
00 の時点では、string stable key を runtime truth にしないことだけを固定する。

00 fixes the following value boundaries:

- ValueSchema defines what values may exist.
- ValueStore stores runtime state.
- ValueStoreInitPlan defines initial writes.
- Dynamic or reactive evaluation must be represented as explicit evaluation plans, not hidden inside generic initialization.
- Save metadata must be attached to schema or explicit save plan, not inferred from runtime store contents.
- Runtime value creation outside schema is forbidden in target kernel paths.

Lower spec 10 defines concrete storage layout, revision model, table/record representation, and save integration.

### Debug and Diagnostics

debug map と structured diagnostics は optional tooling ではなく runtime contract の一部である。

---

## Unity Authoring Bridge

v2 kernel は Unity authoring から完全独立ではない。
ただし runtime truth を hierarchy discovery に戻してはならない。

authoring bridge が担う責務:

- authoring object と runtime scope / module / command / value の対応付け
- authoring validation
- bake or normalize inputs の供給
- editor-facing diagnostics

00 では、ScopeAuthoringLink や KernelRoot の exact component schema は固定しない。
それらは 12 で確定する。

### Authoring ID Stability

Unity authoring bridge must define stable identity rules for scene objects, prefabs, prefab variants, and runtime-instantiated authored objects.

The target architecture must prevent accidental identity collision caused by:

- duplicated prefabs
- nested prefabs
- prefab variants
- scene overrides
- copy/paste in editor
- runtime instantiation of authored templates

Lower spec 12 must define:

- who owns ScopeAuthoringId generation
- when IDs are regenerated
- how duplicated IDs are detected
- how prefab template identity differs from runtime instance identity
- how source location is preserved for diagnostics

---

## Legacy Compatibility Boundary

legacy compatibility は新基盤の core responsibility ではない。
それは migration boundary の責務である。

許可される依存方向は次のとおりである。

```text
LegacyCompat -> New Kernel
New Kernel -> LegacyCompat is forbidden
```

移行期間中の temporary allowance は 13 に明示する。
00 で固定するのは次の原則だけである。

- legacy fallback must be observable
- legacy fallback must not be silent in release
- new kernel core must not depend on legacy runtime APIs

### Legacy Growth Control

Legacy compatibility is a migration tool, not an extension point.

The following are forbidden:

- adding new target-kernel features through legacy APIs
- adding new legacy fallback paths without a migration issue
- allowing target kernel core to reference legacy runtime types
- using legacy resolver paths for new service discovery
- using legacy command registration for new command executors

Legacy usage must be measurable.

Each legacy bridge must report:

- caller
- target legacy API
- reason
- migration status
- removal target or blocking issue

CI should be able to fail when legacy usage increases beyond an approved baseline.

---

## Performance Policy

performance は primary requirement の一つである。
ただし、debuggability、validation、determinism を壊す高速化は禁止する。

### Boot target

boot では次のみを target kernel の許容処理とする。

- manifest and plan input loading
- hash / version checks
- kernel runtime creation
- essential root initialization
- required root scope creation

boot で禁止する方向:

- broad scene object search
- full component discovery
- unconditional eager construction of all command executors
- unconditional eager acquire of all scopes
- runtime stable key resolution

### Scope spawn target

scope spawn は explicit request と explicit plan によって行う。
subtree search、parent transform inference、registration scan は target path に含めない。

### Command dispatch target

command dispatch は、command identity lookup、payload validation、executor resolution、execute、diagnostics の順に明示的に追跡できなければならない。

### Performance Measurement Contract

Performance rules must be measurable.

Lower spec 14 must define profiler markers and budget categories for at least:

- KernelBoot.LoadInputs
- KernelBoot.ValidateHashes
- KernelBoot.CreateRuntime
- ServiceGraph.Build
- ScopeGraph.CreateScope
- Lifecycle.DispatchAcquire
- Lifecycle.DispatchRelease
- CommandCatalog.Lookup
- Command.Execute
- ValueStore.ApplyInitPlan
- RuntimeQuery.Lookup

The target architecture should distinguish:

- structural cost: cost of loading and validating plan metadata
- activation cost: cost of constructing required runtime services
- dispatch cost: cost per operation
- authoring-only cost: editor validation and generation cost

Command executor count may increase structural metadata size, but must not force eager executor construction or runtime registration scan during boot.

---

## Specification Split

本アーキテクチャは大きいため、下位仕様に責務を分割する。

```text
00_KernelArchitectureOverviewSpec.md
01_KernelIRSpec.md
02_ModuleContributionSpec.md
03_VerifiedPlanGenerationSpec.md
04_DependencyValidationSpec.md
05_BootManifestAndProfileSpec.md
06_ServiceGraphRuntimeSpec.md
07_ScopeGraphRuntimeSpec.md
08_LifecyclePlanSpec.md
09_CommandCatalogRuntimeSpec.md
10_ValueSchemaAndStoreSpec.md
11_DebugMapAndDiagnosticsSpec.md
12_UnityAuthoringBridgeSpec.md
13_LegacyCompatBoundarySpec.md
14_PerformanceBudgetAndRuntimeRulesSpec.md
15_TestAndValidationSpec.md
```

### Cross-Spec Consistency Rule

Lower specifications must not redefine root concepts independently.

The following concepts are owned by specific specs:

| Concept | Owner Spec |
|---|---|
| KernelIR node and edge model | 01 |
| Module contribution model | 02 |
| Plan generation and artifact consistency | 03 |
| Dependency validation | 04 |
| Boot input and profile policy | 05 |
| Service runtime | 06 |
| Runtime query and registry semantics | 07 |
| Scope runtime | 07 |
| Lifecycle ordering | 08 |
| Command identity and payload | 09 |
| Value schema and store | 10 |
| DebugMap and diagnostics | 11 |
| Unity authoring bridge | 12 |
| Legacy boundary | 13 |
| Performance rules | 14 |
| Test and CI validation | 15 |

If a lower spec needs a concept owned by another spec, it must reference it rather than redefine it.

### Dependency Order

最初に固めるべきなのは 06 や 07 ではなく、01 と 04 である。

理由:

- runtime plans are projections of KernelIR
- validation rules determine what plans are allowed to exist
- service graph, scope graph, command catalog, and value schema cannot be specified rigorously before IR ownership and dependency validation are fixed

つまり、速さより先に検証可能性を仕様として固定しなければならない。

### Verification Method

Each success criterion must be verifiable.

Lower specs and tests must provide verification through at least one of:

- static validation
- generated artifact validation
- runtime boot test
- profiler marker assertion
- diagnostics snapshot test
- code search / forbidden API analyzer
- CI regression test

| Success Criterion | Verification |
|---|---|
| no broad scene search in boot | forbidden API analyzer + boot profiler marker |
| no subtree discovery in scope spawn | forbidden API analyzer + scope spawn test |
| no registration scan for lifecycle wiring | lifecycle plan validation test |
| no runtime stable key fallback | forbidden API analyzer + runtime value test |
| hash mismatch detected | boot failure test |
| dependency cycle detected | validation test |
| ID failure is human-readable | diagnostics snapshot test |
| legacy usage observable | legacy usage report test |

---

## Success Criteria

この architecture が成立している状態は以下である。

- boot で broad scene search が発生しない
- scope spawn/build で subtree discovery が target path から除去されている
- command executor 数が増えても boot cost が単純線形には肥大しない
- acquire or lifecycle wiring で registration 全走査が不要になる
- value initialization が schema と init plan で追跡できる
- runtime stable key fallback が target kernel から除去されている
- generated data の hash mismatch が検出される
- graph dependency cycle が validation で検出される
- ID / Handle failure が human-readable diagnostics になる
- legacy usage が観測可能である
- release build で silent fallback がない

### Minimum Definition of Done

The success criteria above are not merely state descriptions.
They are only complete when each criterion has a corresponding verification method in lower specs or tests.

## Test Cases

| Test Case | Purpose | Verification |
|---|---|---|
| TC-00-01 | Confirm runtime discovery is not the composition mechanism for the target kernel. | The v2 target principles section must forbid broad discovery, transform inference, and registration scans. |
| TC-00-02 | Confirm KernelIR and VerifiedKernelPlan are the trust boundary. | The trust boundary and core concepts sections must name both explicitly. |
| TC-00-03 | Confirm the specification split keeps 01 and 04 ahead of runtime specs. | The split order and dependency order sections must state that runtime specs depend on validated IR and validation rules. |
| TC-00-04 | Confirm every success criterion maps to a verification method. | The verification matrix must remain present and reference lower specs or tests. |

---

## Final Position

This architecture is not centered on CodeGen.

It is centered on a validated KernelIR pipeline.

KernelIR is the normalized authority.
VerifiedKernelPlan is the runtime execution input.
CodeGen is only one way to produce execution projections.
Validation, hash checks, DebugMap, diagnostics, and tests are what make the pipeline trustworthy.

Runtime must execute only verified inputs.
Editor / CI / Test must prove that those inputs are valid.
DebugMap and diagnostics preserve traceability for an ID / Handle based architecture.
Legacy compatibility remains isolated for migration only.

この設計により、現行の runtime 探索、eager 初期化、registration 肥大化を抑制しつつ、generated data 不整合による新しい破綻も防ぐ。
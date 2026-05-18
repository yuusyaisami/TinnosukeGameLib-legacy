# 00 Kernel Architecture Overview Review

## Status

- Document role: review memo for the root architecture draft
- Scope: review findings for the initial Kernel Architecture Overview draft
- Goal: identify mismatches against the current project and define what 00 must fix before downstream specs are written
- Review stance: strict, migration-oriented, implementation-grounded

---

## Executive Summary

The direction of the proposed Kernel architecture is broadly correct.
The project does need a verified-plan-first runtime, a stricter trust boundary around generated data, and isolation of legacy fallback behavior.

However, the current draft mixes three different things in the same sections:

1. observations about the current runtime
2. design goals for v2
3. final-form API shapes that have not been justified yet

That mix is the main risk.
If 00 remains in that state, downstream specs will inherit false assumptions about current behavior and over-constrained assumptions about the replacement runtime.

The most important correction is to split 00 into two explicit layers:

- current architecture observations grounded in the codebase
- v2 target policies that describe what the new kernel must guarantee

## Test Cases

| Test Case | Purpose | Execution Note |
|---|---|---|
| TC-RV-01 | Confirm each high-severity finding is anchored to concrete code or design evidence. | The anchor list under each finding must remain source-grounded. |
| TC-RV-02 | Confirm current observations and v2 target policy stay separated. | The memo must not collapse fact review and target policy into one section. |
| TC-RV-03 | Confirm the recommended rewrite strategy keeps final API shape deferred. | Final runtime details must remain in the lower specs. |

---

## High Severity Findings

### 1. The draft overstates what the current runtime actually does

The current architecture does rely on runtime discovery and broad authoring-time-to-runtime coupling, but some statements in the draft flatten important distinctions.

Observed code paths show the following:

- scope build is coordinated and parent-gated rather than being a single naive recursive walk
- installer discovery is subtree-based but owner-filtered, not a blind collection of every installer under a scope
- service resolution and runtime identity lookup are already split into separate mechanisms

Concrete anchors:

- `RuntimeLifetimeScopeBase.Build` and related build flow in Assets/GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs
- installer ownership filtering in Assets/GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs
- resolver and acquire/release dispatch infrastructure in Assets/GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs

Required change in 00:

- add a dedicated "Current Architecture Observations" section
- only make descriptive claims there if they are directly supported by code
- move generalized anti-pattern language into a separate target-policy section

### 2. The draft conflates service resolution with runtime scope lookup

The current project has at least two distinct runtime mechanisms:

- type-driven dependency resolution through RuntimeResolver
- identity- and filter-driven scope lookup through BaseLifetimeScopeRegistry and related APIs

The root draft currently reads as if the new kernel will replace one broad DI/runtime-discovery blob.
That is too imprecise for migration.

This matters because command targeting, actor resolution, and some scene-flow behavior do not map directly to service resolution.

Concrete anchors:

- RuntimeResolver in Assets/GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs
- registry usage introduced from project bootstrap in Assets/GameLib/Script/Project/LTS/ProjectLifetimeScope.cs

Required change in 00:

- explicitly separate service graph semantics from runtime lookup semantics
- state whether ScopeGraph replaces registry queries, coexists with them, or requires a new query layer
- defer exact query APIs to 06 and 07

### 3. The command section jumps to a target architecture without describing the actual migration problem

The current command system is not just "many executors registered in DI".
It is a hybrid of:

- bulk executor registration through CommandRunnerMB
- registry-based ID lookup through CommandExecutorRegistry
- key-based authoring resolution through catalog services and key resolvers

That means the migration problem is not merely performance.
It is also semantic consolidation.

Concrete anchors:

- bulk registration in Assets/GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs
- executor registry in Assets/GameLib/Script/Common/Commands/VNext/Core/CommandExecutorRegistry.cs

Required change in 00:

- define the dual nature of the current command system as a migration input
- declare that 00 does not finalize the authoring-key versus runtime-ID contract
- leave concrete executor lifetime policy and payload schema details to 09

### 4. The value section collapses distinct current responsibilities into a single target sentence

The current project has separate but interacting systems for:

- Blackboard hierarchy and variant storage
- Var registry and stable key resolution
- DynamicValue and dynamic source evaluation

The statement "Blackboard will be unified into ValueStore" is directionally acceptable, but at 00 level it hides several migration decisions that are still open.

Concrete anchors:

- BlackboardService in Assets/GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs
- VarIdResolver in Assets/GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs
- VarKeyRegistryLocator in Assets/GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs

Required change in 00:

- keep ValueStore unification as target policy only
- explicitly state that hierarchical read/write behavior, dynamic evaluation, save semantics, and authoring references are deferred to 10

### 5. Boot ownership and scene integration are under-specified for migration

The draft introduces KernelBootManifest and KernelRuntime as if they can replace the current boot path in one step.
But the current runtime boot is coupled to BeforeSceneLoad initialization and project/global scope setup.

Concrete anchors:

- project bootstrap in Assets/GameLib/Script/Project/LTS/ProjectLifetimeScope.cs
- global bootstrap in Assets/GameLib/Script/Project/Global/LTS/GlobalLifetimeScope.cs
- scene-flow behavior in Assets/GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs

Required change in 00:

- state that the v2 boot entry replaces current bootstrap incrementally
- define BootManifest as runtime input, not as a promise that the entire boot migration is already solved
- defer concrete coexistence rules to 05, 12, and 13

---

## Medium Severity Findings

### 6. 00 fixes too many concrete API shapes too early

The current draft includes many illustrative C# types and fields.
That is useful for explanation, but risky if readers interpret them as locked runtime API.

Required change in 00:

- retain only a small number of illustrative types
- mark all such samples as non-final, explanatory structures
- move normative data layout rules into 01, 05, 06, 07, 09, 10, and 11

### 7. Runtime prohibition language is correct in direction but too absolute for a transition root spec

The root document should define the target runtime contract.
It should not accidentally imply that the current codebase is already invalid in every migration phase.

Required change in 00:

- introduce a distinction between target-kernel prohibitions and temporary migration allowances
- point all temporary allowances to 13

### 8. Hash and debug-map policy needs a clearer trust boundary statement

The current draft correctly elevates hash checks and debug maps, but the root spec should be clearer about what exactly is trusted.

Required change in 00:

- define KernelIR as the source of truth
- define VerifiedKernelPlan as a validated projection
- define generated code and assets as transport or execution artifacts, not trust anchors

---

## Low Severity Findings

### 9. Terminology should align more closely with the current project during migration

Using entirely new vocabulary at 00 level increases migration ambiguity.
The current codebase already contains important concepts such as LifetimeScopeKind, acquire/release handlers, identity services, and runtime registries.

Required change in 00:

- mention current terms when describing migration inputs
- keep v2 terms as the target language
- avoid implying that old names never existed or were conceptually meaningless

### 10. The split order is strong, but the reason should be embedded in the document structure itself

The proposed order of 01 then 04 before runtime specs is correct.
That priority should not appear only as an end note.

Required change in 00:

- tie the split order directly to the dependency structure of the architecture
- make it obvious that runtime specs depend on validated IR and graph rules

---

## Recommended Rewrite Strategy

00 should be rewritten with the following top-level structure:

1. Purpose and document role
2. Current architecture observations
3. Root problems to solve
4. v2 target principles
5. Core concepts and trust boundary
6. Runtime policies
7. Migration boundary
8. Specification split and dependency order
9. Success criteria

The rewrite should preserve the direction of the original draft while removing two kinds of ambiguity:

- ambiguity about the current codebase
- ambiguity about which details are intentionally deferred

---

## Review Outcome

The document should proceed.
It should not be discarded.

But it should proceed only after the root specification is rewritten so that:

- current-state observations are implementation-grounded
- v2 target rules are explicit and separated
- concrete runtime API shapes are treated as illustrative unless owned by a lower spec
- migration constraints are acknowledged rather than hidden

That is the threshold required before 01 and 04 can be written as real authority documents.
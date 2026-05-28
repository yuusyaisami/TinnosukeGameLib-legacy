# Scene Asset Migration Specification

## Document Status

- Document ID: `11_4_SceneAssetMigrationSpec`
- Status: Draft
- Role: define the fail-closed report, validation, and rewrite contract for M11.4 scene / prefab migration
- Depends on:
  - [05_ImplementationMilestoneSpec.md](05_ImplementationMilestoneSpec.md)
  - [08_FullReplacementCompletionSpec.md](08_FullReplacementCompletionSpec.md)
  - [Index/ScenePrefabInventory.md](Index/ScenePrefabInventory.md)
- Non-goals:
  - invent missing successor runtime features
  - preserve legacy runtime authority through temporary scene-local bridges
  - treat manual per-scene repair as a normal completion path

### Revision Note

M11.4 is not an ad-hoc scene cleanup pass.
It is the point where shipped asset truth must be audited, reported, and then rewritten in place under a field-preserving, fail-closed contract.
This document fixes the required migration report shape and validation codes before destructive asset rewrites proceed.

---

## 1. Mission

M11.4 owns four things:

1. freeze the shipped scene / prefab asset set that will be rewritten
2. produce an asset migration report that exposes missing successor anchors and remaining legacy residue
3. block rewrite when the report contains unresolved items
4. rewrite assets in place only after the report and validator agree that the target contract is explicit

If the report cannot describe an asset deterministically, rewrite must stop.

---

## 2. Baseline

Current shipped baseline comes from [Index/ScenePrefabInventory.md](Index/ScenePrefabInventory.md).

- shipped scenes: `Assets/Scenes/TitleScene.unity`, `Assets/Scenes/GameScene.unity`
- prefab baseline: `0` assets under `Assets`
- current legacy anchors in shipped scenes:
  - `CommandRunnerMB`
  - `BlackboardMB`
  - `RuntimeLifetimeScope` in `GameScene`

Current required successor anchor baseline for the first report pass is:

| Asset | Required Anchors | Legacy Anchors That Must Reach Zero |
| --- | --- | --- |
| `Assets/Scenes/TitleScene.unity` | `EntityIdentityMB`, `SceneKernelHostMB` | `CommandRunnerMB`, `BlackboardMB` |
| `Assets/Scenes/GameScene.unity` | `EntityIdentityMB`, `SceneKernelHostMB`, `SceneKernelSpawnDeclarationMB`, `SceneKernelSpawnHostMB` | `RuntimeLifetimeScope`, `CommandRunnerMB`, `BlackboardMB` |

This baseline is intentionally conservative.
If a scene requires additional declaration-backed successor anchors, add them to the migration target contract in the same change that introduces the successor surface.

---

## 3. Report Contract

The asset migration report must be deterministic and machine-readable.
Each asset record must contain at least:

- asset kind (`Scene` or `Prefab`)
- asset path
- asset guid when Unity can resolve it
- whether live roots were successfully resolved
- matched required anchors with source trace
- matched legacy anchors with source trace
- missing required anchor type names
- per-asset unresolved item count

The aggregate report must contain:

- sorted asset records
- unexpected prefab paths when the prefab baseline is frozen at zero
- aggregate unresolved item count
- `IsValid == false` whenever unresolved item count is non-zero

The report is allowed to be incomplete only in one direction: it may over-report blockers, but it must never silently drop them.

---

## 4. Validation Codes

The validator must emit stable codes for the first M11.4 execution slice.

| Code | Meaning | Required Action |
| --- | --- | --- |
| `UNITY_ASSET_MIGRATION_TARGET_INVALID` | target contract is structurally invalid | fix the migration target definition before scanning again |
| `UNITY_ASSET_MIGRATION_ASSET_ROOTS_EMPTY` | the asset did not resolve to live roots | stop rewrite and verify the asset can still be opened deterministically |
| `UNITY_ASSET_MIGRATION_REQUIRED_ANCHOR_MISSING` | a required successor anchor is absent | add the explicit successor host / declaration before legacy removal |
| `UNITY_ASSET_MIGRATION_LEGACY_ANCHOR_PRESENT` | legacy runtime authority is still serialized | migrate fields to successor authoring, then remove the legacy component |
| `UNITY_ASSET_MIGRATION_PREFAB_BASELINE_DRIFT` | prefabs were found although the frozen baseline is zero | re-audit the asset set and update the migration inventory before rewrite |

All five codes are build-blocking for M11.4.

---

## 5. Rewrite Rules

Allowed rewrite behavior:

- add successor host / declaration / identity components in place
- copy serialized fields from legacy authoring bridges into successor authoring surfaces
- preserve scene object identity and GUID-linked references
- remove legacy runtime species only after successor fields are present and serialized

Forbidden rewrite behavior:

- add scene-specific temporary runtime installers
- keep `CommandRunnerMB`, `BlackboardMB`, or `RuntimeLifetimeScope*` as target-path authority
- delete and recreate scene assets to perform migration
- silently patch missing successor anchors at runtime
- declare migration success while unresolved report items remain

---

## 6. Execution Order

M11.4 execution order is fixed:

1. regenerate the shipped asset report
2. validate the report and require unresolved item count `0` for the rewrite precondition of the targeted asset slice
3. rewrite `TitleScene` first as the narrow proof
4. rewrite `GameScene` second as the dense proof
5. rerun the report and validator after each rewrite
6. only if prefab baseline changes from `0`, add prefab records and apply the same contract

If a required successor anchor is missing in code, stop and treat it as an upstream blocker.

---

## 7. Verification Gate

M11.4 is complete for an asset only when all of the following hold:

- the asset migration report returns unresolved item count `0` for that asset
- legacy runtime species are absent from serialized scene / prefab data
- successor host / declaration / identity anchors are serialized explicitly
- the migrated asset still supports the verification flow declared in [Index/ScenePrefabInventory.md](Index/ScenePrefabInventory.md)

The first execution slice implemented in editor code is the report/validator foundation:

- `SceneAssetMigrationModel`
- `SceneAssetMigrationReportService`
- `SceneAssetMigrationValidationService`
- focused editor tests for legacy residue, required anchor absence, and prefab baseline drift

Destructive scene rewrite code must build on that foundation instead of bypassing it.
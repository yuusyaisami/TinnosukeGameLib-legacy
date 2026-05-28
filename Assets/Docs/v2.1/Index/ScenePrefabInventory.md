# Scene / Prefab Inventory

## Document Status

- Document ID: `ScenePrefabInventory`
- Status: Draft
- Role: canonical machine-readable inventory for shipped scene and prefab migration anchors required by M11.1
- Depends on:
  - [05_ImplementationMilestoneSpec.md](../05_ImplementationMilestoneSpec.md)
  - [07_SpawnPoolLifecycleSpec.md](../07_SpawnPoolLifecycleSpec.md)
  - [08_FullReplacementCompletionSpec.md](../08_FullReplacementCompletionSpec.md)
  - [11_4_SceneAssetMigrationSpec.md](../11_4_SceneAssetMigrationSpec.md)

### Revision Note

This inventory freezes the shipped asset baseline that M11.4 and M11.5 must migrate and verify.
The fail-closed report and validator contract for that rewrite wave is fixed in [11_4_SceneAssetMigrationSpec.md](../11_4_SceneAssetMigrationSpec.md).
The current workspace scan found 2 shipped scene assets under `Assets/Scenes` and no `.prefab` assets under `Assets`.

---

## Scope

This inventory covers 2 scene records and 0 prefab family records.

### State Definitions

- `進行中`: the scene already carries new anchors, but legacy runtime species or bridges are still serialized.
- `該当なし`: no asset matching the inventory unit was found in the current workspace scan.

---

## Summary

- `進行中` scene records: 2
- runtime prefab family records: 0

---

## 進行中

| Asset | State | New Anchor | Legacy Anchor | Required Migration Tool | Verification Flow | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| [Assets/Scenes/GameScene.unity](../../../Scenes/GameScene.unity) | `進行中` | `EntityIdentityMB`, `ButtonChannelHubMB` | `RuntimeLifetimeScope`, `CommandRunnerMB`, `BlackboardMB` | field-preserving editor migration that rewrites scene-local hosts to `SceneKernel` / declaration / identity and removes legacy runtime species after reserialize | boot -> spawn/release/delete mediation -> UI interaction -> command execution -> shutdown | this is the densest shipped scene anchor; both new and legacy surfaces currently coexist in the serialized scene |
| [Assets/Scenes/TitleScene.unity](../../../Scenes/TitleScene.unity) | `進行中` | `EntityIdentityMB`, `ButtonChannelHubMB` | `CommandRunnerMB`, `BlackboardMB` | field-preserving editor migration that rewrites title-flow command/UI hosts to declaration-backed runtime wiring and removes legacy command/value bridges after reserialize | boot -> title UI interaction -> button command -> scene change to `GameScene` | no `RuntimeLifetimeScope` hit was observed in the current scene scan, but legacy command/value bridges still remain serialized |

---

## Prefab Baseline

| Inventory Unit | State | Asset Anchor | Notes |
| --- | --- | --- | --- |
| runtime prefab family | `該当なし` | no `.prefab` files were found under `Assets` in the current workspace scan | M11.1 freezes the prefab baseline at 0 for this workspace; rerun the inventory freeze if prefab assets are added or unignored |
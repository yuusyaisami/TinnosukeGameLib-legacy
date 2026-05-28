# Value / Scalar / Query Inventory

## Document Status

- Document ID: `ValueScalarQueryInventory`
- Status: Draft
- Role: canonical machine-readable inventory for the value / scalar / query cutover units required by M11.1
- Depends on:
  - [02_ConcreteMigrationArchitectureSpec.md](../02_ConcreteMigrationArchitectureSpec.md)
  - [04_VarStoreCommandScalarTreatmentSpec.md](../04_VarStoreCommandScalarTreatmentSpec.md)
  - [05_ImplementationMilestoneSpec.md](../05_ImplementationMilestoneSpec.md)
  - [08_FullReplacementCompletionSpec.md](../08_FullReplacementCompletionSpec.md)

### Revision Note

This inventory freezes the current workspace value / scalar / query boundaries at the unit defined by [08_FullReplacementCompletionSpec.md](../08_FullReplacementCompletionSpec.md): 1 boundary or 1 runtime contract.
The current scan shows explicit ValueStore and RuntimeQuery boundary surfaces, while shipped scenes still serialize legacy value bridges through `BlackboardMB` and `CommandRunnerMB`.

---

## Scope

This inventory covers 5 boundary records.

### State Definitions

- `進行中`: explicit new-path boundary exists, but legacy bridge or asset cutover is not yet closed.
- `隔離/削除対象`: legacy authority or fallback that must not survive M11 / M12 runtime truth.

---

## Summary

- `進行中`: 3
- `隔離/削除対象`: 2

---

## 進行中

| Inventory Unit | Owner | Bridge / Fallback | Asset Anchor | Notes | Locations |
| --- | --- | --- | --- | --- | --- |
| ValueStore public contract | `SceneKernel` value boundary / `Game.Kernel.Value` | `VarStoreBackedValueStore` and `SceneKernelValueStoreBoundary` bridge the current backend into `IValueStore` | [Assets/Scenes/GameScene.unity](../../../Scenes/GameScene.unity)<br>[Assets/Scenes/TitleScene.unity](../../../Scenes/TitleScene.unity) | `ValueKeyId`-based API exists, but M11.3 still has to remove `Blackboard` and stable-key callers from the target path | [Assets/GameLib/Script/Kernel/Value/ValueStoreContracts.cs](../../../GameLib/Script/Kernel/Value/ValueStoreContracts.cs)<br>[Assets/GameLib/Script/Common/Variables/VarStore/Core/VarStoreValueStoreBridge.cs](../../../GameLib/Script/Common/Variables/VarStore/Core/VarStoreValueStoreBridge.cs) |
| RuntimeQuery verified boundary | verified plan / generated artifact set | generated `RuntimeQueryPlan` is the current explicit query artifact | no direct scene YAML anchor observed in the current scan | `RuntimeQueryId` and plan artifacts exist, but gameplay callers still retain helper-based actor/query fallback outside the final runtime contract | [Assets/GameLib/Script/Kernel/Generation/KernelProjectionArtifacts.cs](../../../GameLib/Script/Kernel/Generation/KernelProjectionArtifacts.cs) |
| Scalar declaration/runtime shell boundary | `Game.Scalar` declaration/runtime layer | `BaseScalarMB` currently installs `IScalarRuntimeShell` beside legacy-style service contracts | no direct scene YAML anchor pinned in the current M11.1 scan | explicit scalar runtime shell exists, but the public runtime still exposes fallback-shaped scalar service access and has not finished the M9/M11 cutover | [Assets/GameLib/Script/Common/Variables/Scalar/Def/ScalarServiceDef.cs](../../../GameLib/Script/Common/Variables/Scalar/Def/ScalarServiceDef.cs)<br>[Assets/GameLib/Script/Common/Variables/Scalar/MB/BaseScalarMB.cs](../../../GameLib/Script/Common/Variables/Scalar/MB/BaseScalarMB.cs) |

---

## 隔離/削除対象

| Inventory Unit | Owner | Bridge / Fallback | Asset Anchor | Notes | Locations |
| --- | --- | --- | --- | --- | --- |
| Legacy value fallback authority | legacy value / `VarStore` fallback path | `BlackboardMB`, `BlackboardService`, `VarIdResolver`, and `VarKeyRegistryLocator` preserve parent/root fallback and stable-key runtime resolve | [Assets/Scenes/GameScene.unity](../../../Scenes/GameScene.unity)<br>[Assets/Scenes/TitleScene.unity](../../../Scenes/TitleScene.unity) | both shipped scenes still serialize `BlackboardMB`; M11.3 must remove this path from runtime truth and leave `VarStore` as backend only | [Assets/GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs](../../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs)<br>[Assets/GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs](../../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs)<br>[Assets/GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs](../../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs)<br>[Assets/GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs](../../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs) |
| Legacy scalar / query fallback authority | legacy scalar and actor-query helper path | `IBaseScalarService` / `BaseScalarService` still expose fallback-shaped access, and `ActorSourceFastResolver` still resolves actor targets through scope/helper logic | [Assets/Scenes/GameScene.unity](../../../Scenes/GameScene.unity)<br>[Assets/Scenes/TitleScene.unity](../../../Scenes/TitleScene.unity) | current scene assets still depend on `CommandRunnerMB`-anchored command flows, so these helpers remain active migration debt until explicit scalar/runtime-query contracts replace them | [Assets/GameLib/Script/Common/Variables/Scalar/Def/ScalarServiceDef.cs](../../../GameLib/Script/Common/Variables/Scalar/Def/ScalarServiceDef.cs)<br>[Assets/GameLib/Script/Common/Variables/Scalar/Core/BaseScalarService.cs](../../../GameLib/Script/Common/Variables/Scalar/Core/BaseScalarService.cs)<br>[Assets/GameLib/Script/Common/Variables/Scalar/MB/BaseScalarMB.cs](../../../GameLib/Script/Common/Variables/Scalar/MB/BaseScalarMB.cs)<br>[Assets/GameLib/Script/Common/Commands/VNext/Core/ActorSourceFastResolver.cs](../../../GameLib/Script/Common/Commands/VNext/Core/ActorSourceFastResolver.cs) |
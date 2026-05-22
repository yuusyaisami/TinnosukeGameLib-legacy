# Hub Classification Inventory

## Document Status

- Document ID: HubClassificationInventory
- Status: Draft
- Role: machine-readable inventory of the current M6.7 hub classification decisions
- Depends on:
  - [06_ServiceGraphRuntimeSpec.md](../06_ServiceGraphRuntimeSpec.md)
  - [07_ScopeGraphRuntimeSpec.md](../07_ScopeGraphRuntimeSpec.md)
  - [08_LifecyclePlanSpec.md](../08_LifecyclePlanSpec.md)
  - [12_UnityAuthoringBridgeSpec.md](../12_UnityAuthoringBridgeSpec.md)
  - [13_LegacyCompatBoundarySpec.md](../13_LegacyCompatBoundarySpec.md)
  - [14_PerformanceBudgetAndRuntimeRulesSpec.md](../14_PerformanceBudgetAndRuntimeRulesSpec.md)
  - [15_TestAndValidationSpec.md](../15_TestAndValidationSpec.md)
- Provides foundation for:
  - [06_ServiceGraphRuntimeSpec.md](../06_ServiceGraphRuntimeSpec.md)
  - [16_ImplementationMilestoneOrderSpec.md](../16_ImplementationMilestoneOrderSpec.md)

### Revision Note

This inventory is the canonical, machine-readable companion to M6.7.

The prose in 06 explains the rule set.
This inventory pins the current classification rows so tests can detect drift.

---

## Purpose

The purpose of this inventory is to keep the hub classification table stable enough for review, implementation, and regression gates.

If a hub changes classification, the update must be reflected here first or in the same change set.

---

## Scope

This inventory covers the current hub systems explicitly named by M6.7:

- modal stack hub
- tooltip hub
- mesh hub
- animation sprite hub

It also preserves the default classification vocabulary used by the current ServiceGraph specification:

- service candidate
- mixed boundary
- hub-owned runtime object

---

## Inventory

| Code Location | Classification | Cardinality / Boundary | Why It Matters | Required Treatment | Eligible for ServiceGraph |
|---|---|---|---|---|---|
| [ModalStackChannelHubService.cs](../../GameLib/Script/Project/UI/Core/ModalStackChannel/ModalStackChannelHubService.cs) | service candidate | OnePerProject or OnePerScene; bounded | coarse-grained UI hub with shared state and explicit ownership boundary | keep layer and root state hub-owned | yes |
| [TooltipChannelHubService.cs](../../GameLib/Script/Project/UI/Core/Tooltip/TooltipChannelHubService.cs) | mixed boundary | n/a until split | mixes runtime query, value access, player ownership, and explicit camera lookup | split service declaration, lifecycle declaration, and player runtime ownership before ServiceGraph eligibility | no |
| [MeshChannelHubService.cs](../../GameLib/Script/Project/Scene/Channels/Mesh/MeshChannelHubService.cs) | service candidate | OnePerScene; bounded | owns many player runtimes but the players themselves are not service identities | keep MeshChannelPlayerRuntime hub-owned and non-ServiceId-backed | yes |
| [AnimationSpriteHubService.cs](../../GameLib/Script/Project/Scene/Channels/SpriteAnimation/AnimationSpriteHubService.cs) | mixed boundary | n/a until split | mixes hub service, material/provider behavior, lifecycle, and player runtime ownership | split service declaration, lifecycle declaration, and player runtime ownership before target migration | no |

---

## Rules

1. A hub candidate may be a ServiceGraph service only when it remains coarse-grained and domain-owned.
2. Player runtimes are hub-owned runtime objects and are not ServiceGraph services by default.
3. A mixed boundary remains non-eligible until its player/runtime responsibilities are split out.
4. Any change to the inventory must keep the prose in 06 aligned with the same classification.

---

## Review Notes

The inventory is intentionally small.

The goal is not to classify every runtime object in the repo.
The goal is to lock the current hub debt that M6.7 explicitly names.

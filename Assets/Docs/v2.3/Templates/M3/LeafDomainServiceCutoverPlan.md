# LeafDomainServiceCutoverPlan

Source Spec: [08_KernelV23M3LeafScopeDemotionExecutionSpec.md](../../08_KernelV23M3LeafScopeDemotionExecutionSpec.md)
Execution Step: M3.1 Leaf Domain Freeze + M3.2 Service Cutover Design Lock (Draft)
Artifact Owner: Copilot Draft (handoff required)
Last Updated: 2026-05-23
Approval State: Draft (M3.1 freeze complete; M3.2 design lock drafted)

## Freeze Rules (M3.1)

- Leaf-domain scope for M3 is limited to entity/ui-element runtime families and their direct leaf-runtime support services.
- Each family must have one migration owner and one target service form.
- Cutover order is fixed by risk class: `high -> medium -> low`.

## DomainClass Vocabulary

- `UIElementLeaf`: UI/visual element runtime families that must not own scope-local runtime authority.
- `EntityLeaf`: gameplay/runtime entity-facing leaf families.
- `EntityLeafInfrastructure`: entity-facing infrastructural families that create/manage leaf runtime entities (e.g. spawn/pool root services).

## Records

| ServiceFamilyName | DomainClass | MigrationOwner | TargetServiceForm | CutoverWave | RiskClass |
| --- | --- | --- | --- | --- | --- |
| Selection and Pointer Bridge Runtime | UIElementLeaf | Interaction Runtime Owner | Scope-ServiceInstance | M3-W1 | high |
| Runtime Spawner and Pool | EntityLeafInfrastructure | Runtime Spawn Owner | Scope-ServiceInstance | M3-W1 | high |
| Chunk Runtime | EntityLeaf | Chunk Systems Owner | Scope-ServiceInstance | M3-W2 | medium |
| Channel Scroll Runtime | UIElementLeaf | Scene Channels Owner | AoS | M3-W2 | medium |
| Transform Channel Runtime | UIElementLeaf | Transform Runtime Owner | AoS | M3-W2 | medium |
| AutoSpawn Runtime | EntityLeaf | AutoSpawn Owner | AoS | M3-W2 | medium |
| Map Node Runtime | EntityLeaf | Map Runtime Owner | Scope-ServiceInstance | M3-W2 | medium |
| Background Runtime | UIElementLeaf | Background Runtime Owner | AoS | M3-W3 | low |

## Design-Lock Section

Status: M3.2 design lock draft completed. Reviewer approval pending.

Design-lock rules:

- Legacy authority paths in accepted flow must be replaced by kernel command surface or explicit contribution-mapped kernel-owned service forms.
- Compatibility shell usage is allowed for serialization/reference continuity only.
- Any path that reintroduces local installer discovery, nearest-scope inference authority, or direct resolver authority bypass is reject.

| FamilyDesignId | LegacyAuthorityPath | TargetKernelPath | CompatibilityShellPlan | RejectCondition |
| --- | --- | --- | --- | --- |
| M3-DES-001 | `SelectableRuntimeMB.NotifyBridgeRefresh/Release` + nearest-scope resolve (`TryResolveActorScope`) + resolver lookup of bridge service | Selection/pointer bridge binding invoked via kernel-owned lifecycle handlers with declaration-only MB trigger surface | Keep `SelectableRuntimeMB` and `WorldPointerTargetMB` MonoScript binding for reference continuity; disallow shell-owned service registration authority | reject when MB callback path directly resolves runtime authority in accepted flow or relies on nearest-scope inference as ownership truth |
| M3-DES-002 | `RuntimeManagerMB.InstallRuntimeManagerRuntime` scoped registrations and build callbacks for pool/spawner materialization | Runtime spawner/pool instantiated through explicit declaration mapping and kernel lifecycle command route (`Register -> Build -> Activate`) | Keep existing runtime manager authoring component as declaration source only; keep serialization fields unchanged | reject when accepted path uses local `IScopeInstaller` discovery or bypasses kernel command ordering for spawner/pool lifecycle |
| M3-DES-003 | `ChunkStreamerMB.InstallScopeServices` + `ChunkFactoryService` scope resolver coupling (`scopeNode.Resolver.TryResolve<IChunkAdapter>`) | Chunk runtime factory and streamer ownership moved to kernel-managed Scope-ServiceInstance path with explicit declaration mapping | Preserve chunk authoring fields/prefab references; compatibility shell can forward declaration payload only | reject when chunk runtime performs resolver traversal as fallback authority or reintroduces local container build path |
| M3-DES-004 | `ScrollChannelHubService.OnAcquire` runtime dependency resolution (`ResolveVars`, `ResolveRegistry`, `ResolveRunner`) | Channel scroll runtime shifted to AoS target form with command-driven lifecycle and explicit dependency injection boundary | Preserve existing channel authoring assets and tags; shell is read-only for serialization continuity | reject when scroll channel runtime obtains mutable authority via direct scope resolver path in accepted flow |
| M3-DES-005 | Transform channel runtime lookup traversal in command executors (`MovementExecutors.TryResolveTransformChannelRuntime`, parent walk + `TryResolve<ITransformChannelHubService>`) | Transform channel runtime (`TransformChannelHubService`) managed through AoS boundary and kernel-owned command execution contracts | Keep transform channel tags/options serialization intact; compatibility shell must not own runtime state mutation | reject when transform channel execution falls back to ancestor resolver discovery as authority source |
| M3-DES-006 | AutoSpawn hub authority resolved through channel executor hub traversal (`AutoSpawnChannelControlExecutor.TryResolveHub`, `IAutoSpawnChannelHubService`) | AutoSpawn runtime moved to AoS target form with explicit kernel command-surface lifecycle ownership | Preserve existing autospawn declarations and references; compatibility shell forwards declarations only | reject when autospawn accepted path uses implicit hub/resolver discovery as runtime authority source |
| M3-DES-007 | Map node runtime resolver-coupled updates (`MapNodePlayerService`, `MapNodeVisualizer` scopeParent resolver usage) | Map node runtime moved to kernel-owned Scope-ServiceInstance route with explicit command/lifecycle control | Preserve map profile/visualization authoring references and runtime prefab links; shell non-authoritative | reject when map runtime mutates authority through direct resolver coupling outside kernel command path |
| M3-DES-008 | Background/slider visual runtime spawned instance lifecycle coupled to active scope resolver and release-time fallback destruction path | Background runtime migrated to AoS target form with kernel lifecycle ownership and explicit spawn/release contracts | Keep visual authoring references and spawned runtime binding continuity; shell only for serialization continuity | reject when background runtime keeps accepted-path authority in scope-resolver attached runtime lifecycle without kernel command mediation |

## Gate Check

- All leaf families assigned: [x]
- Design lock draft complete: [x]
- Design lock anchor quality verified: [x]
- Design lock approved: [ ]

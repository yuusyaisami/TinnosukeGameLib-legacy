# AuthorityPathCensus table

Source Spec: [06_KernelV23M1SpecLockAndCensusExecutionSpec.md](../../06_KernelV23M1SpecLockAndCensusExecutionSpec.md)
Execution Step: M1.2 Authority Path Census
Artifact Owner: Copilot Draft (handoff required)
Last Updated: 2026-05-23
Approval State: Draft (Ready for reviewer approval)

## Census Records

| PathId | SourceAnchor | CurrentOwnerClass | LegacyAuthorityResidueFlag | Evidence |
| --- | --- | --- | --- | --- |
| AP-001 | Assets/GameLib/Script/Common/Scope/Runtime/RuntimeLifetimeScope.cs :: KernelScopeHost.Build() | mixed | yes | Call path: EnsureScopeBuilt -> Build -> RuntimeContainerBuilder -> builder.Build(). Scope host performs local resolver construction. |
| AP-002 | Assets/GameLib/Script/Common/Scope/Runtime/RuntimeLifetimeScope.cs :: KernelScopeHost.ConfigureCore(IRuntimeContainerBuilder) | scope-owned | yes | Per-scope local registrations (identity/node/tick/runtime handlers) are pushed from scope host into local builder. |
| AP-003 | Assets/GameLib/Script/Common/Scope/Runtime/RuntimeLifetimeScope.cs :: KernelScopeHost.InstallLocalFeatures(IRuntimeContainerBuilder) | scope-owned | yes | Runtime component scan: GetComponents + IScopeInstaller projection. This is local installer discovery. |
| AP-004 | Assets/GameLib/Script/Common/Scope/Runtime/RuntimeLifetimeScope.cs :: RuntimeScopeContributionBridge.ThrowIfVerifiedRuntimeWouldUseLegacyInstallerProjection | mixed | no | Guard path rejects legacy installer projection when VerifiedCompositionRuntime is active. |
| AP-005 | Assets/GameLib/Script/Common/Scope/Runtime/RuntimeLifetimeScope.cs :: RuntimeScopeContributionBridge.InstallAcceptedAuthoringContributions | mixed | no | Deterministic accepted authoring bridge (CommandRunnerAuthoring/BlackboardAuthoring/SceneFlowInstallerMB). |
| AP-006 | Assets/GameLib/Script/Common/Scope/Runtime/RuntimeLifetimeScope.cs :: RuntimeScopeContributionBridge.InstallVerifiedCompositionContributions | mixed | no | Verified contribution host path (explicit contribution bridge), no broad runtime installer discovery. |
| AP-007 | Assets/GameLib/Script/Common/Scope/Runtime/RuntimeLifetimeScope.cs :: KernelScopeHost.GetParentCached/ResolveParentCore | scope-owned | yes | Transform-parent traversal used when verified composition inactive. Discovery-based parent inference remains. |
| AP-008 | Assets/GameLib/Script/Common/Scope/Runtime/RuntimeLifetimeScope.cs :: KernelScopeHost.TryEnsureScopeRegistryResolved | mixed | yes | Parent resolver traversal + static cache fallback to locate IBaseLifetimeScopeRegistry. Dynamic runtime lookup remains. |
| AP-009 | Assets/GameLib/Script/Project/Scene/Spawner/BaseLTS/BaseLifetimeScopeSpawner.cs :: BaseLifetimeScopeSpawner.SpawnAsync | mixed | no | Spawn path drives EnsureScopeBuilt/WhenBuiltAsync/HandleSpawnAsync. Build authority delegated to KernelScopeHost pipeline. |
| AP-010 | Assets/GameLib/Script/Project/Scene/Channels/Scroll/ScrollChannelHubService.cs :: ResolveVars/ResolveRegistry/ResolveRunner | mixed | yes | Runtime service access through scope.Resolver.TryResolve at call sites. Indicates resolver-authority coupling in runtime leaf service logic. |
| AP-011 | Assets/GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs :: RuntimeLifetimeScope : LifetimeScope.Configure | scope-owned | yes | Legacy VContainer LifetimeScope class remains. Local DI authority type still present in codebase. |
| AP-012 | Assets/GameLib/Script/Project/LTS/ProjectLifetimeScope.cs :: ProjectLifetimeScope : LifetimeScope.Configure | scope-owned | yes | Legacy root-level local DI authority class present. |
| AP-013 | Assets/GameLib/Script/Project/Global/LTS/GlobalLifetimeScope.cs :: GlobalLifetimeScope : LifetimeScope.Configure | scope-owned | yes | Legacy global local DI authority class present. |
| AP-014 | Assets/GameLib/Script/Platform/LTS/PlatformLifetimeScope.cs :: PlatformLifetimeScope : LifetimeScope.Configure | scope-owned | yes | Legacy platform local DI authority class present. |
| AP-015 | Assets/GameLib/Script/Project/Scene/LTS/SceneLifetimeScope.cs :: SceneLifetimeScope : LifetimeScope.Configure | scope-owned | yes | Legacy scene local DI authority class present. |
| AP-016 | Assets/GameLib/Script/Project/Scene/Field/LTS/FieldLifetimeScope.cs :: FieldLifetimeScope : LifetimeScope.Configure | scope-owned | yes | Legacy field local DI authority class present. |
| AP-017 | Assets/GameLib/Script/Project/Scene/Field/Entity/LTS/EntityLifetimeScope.cs :: EntityLifetimeScope : LifetimeScope.Configure | scope-owned | yes | Legacy entity local DI authority class present. |

## Census Summary

- Total paths: 17
- kernel-owned: 0
- scope-owned: 10
- mixed: 7
- unknown: 0
- Legacy authority residue flagged: 13

## Blocking Candidates for M2 Entry

- AP-003 local installer discovery via IScopeInstaller projection
- AP-007 transform-parent discovery path for scope parent inference
- AP-011 through AP-017 legacy LifetimeScope class set

## Exit Check (M1.2)

- Accepted-path runtime authority edges enumerated: [x]
- Source anchors recorded for each edge: [x]
- Owner class assigned (kernel/scope/mixed/unknown): [x]
- No unknown owner class remaining in this census: [x]

## Reviewer Sign-off

- Reviewer:
- Review date:
- Decision: Approve / Reject / Conditional
- Notes:

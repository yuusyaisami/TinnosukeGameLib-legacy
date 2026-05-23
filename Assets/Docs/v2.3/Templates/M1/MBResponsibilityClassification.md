# MBResponsibilityClassification table

Source Spec: [06_KernelV23M1SpecLockAndCensusExecutionSpec.md](../../06_KernelV23M1SpecLockAndCensusExecutionSpec.md)
Execution Step: M1.3 MB Responsibility Classification
Artifact Owner: Copilot Draft (handoff required)
Last Updated: 2026-05-23
Approval State: Draft (Ready for reviewer approval)

## Classification Records

| MBFamilyName | CurrentResponsibilityClass | TargetResponsibilityClass | RequiredAction | BreakRisk | EvidenceAnchor |
| --- | --- | --- | --- | --- | --- |
| Legacy LTS LifetimeScope family (`RuntimeLifetimeScope`, `ProjectLifetimeScope`, `GlobalLifetimeScope`, `PlatformLifetimeScope`, `SceneLifetimeScope`, `FieldLifetimeScope`, `EntityLifetimeScope`) | runtime-authority residue | declaration-only | remove | high | `Assets/GameLib/Script/**/LTS/*LifetimeScope.cs` (`LifetimeScope : Configure(IContainerBuilder)`) |
| KernelScopeHost family (`KernelScopeHost` and derived scope MBs) | mixed | declaration-only | convert | high | `Assets/GameLib/Script/Common/Scope/Runtime/RuntimeLifetimeScope.cs` (`Build`, `ConfigureCore`, `InstallLocalFeatures`, `builder.Build`) |
| RuntimeLifetimeScope compatibility shell MB (`RuntimeLifetimeScope : KernelScopeHost`) | mixed | declaration-only | convert | medium | `Assets/GameLib/Script/Common/Scope/Runtime/RuntimeLifetimeScope.cs` (compatibility shell + inherited build authority) |
| Verified installer contribution host family (`IVerifiedInstallerContributionHost` implementers) | mixed | declaration-only | convert | medium | `EntityInstallerContributionHostMB.InstallVerifiedInstallerContributions`, `FieldInstallerContributionHostMB.InstallVerifiedInstallerContributions` |
| Local installer projection MB family (`IScopeInstaller` implementers) | runtime-authority residue | declaration-only | convert | high | `KernelScopeHost.InstallLocalFeatures` runtime `GetComponents` projection + `installer.InstallScopeServices(...)` |
| Root scope host contribution MB family (`ProjectRootScopeServicesMB`, `PlatformRootScopeServicesMB`, `SceneRootScopeServicesMB`) | mixed | declaration-only | convert | medium | `RuntimeScopeContributionBridge.InstallHostContributions(...)` |
| SceneFlow authoring/installer family (`SceneFlowInstallerMB`) | mixed | declaration-only | convert | medium | `SceneFlowInstallerMB.InstallSceneFlowRuntime(...)` + `RegisterSceneFlowServices(...)` |
| Selectable runtime bridge MB family (`SelectableRuntimeMB`, `WorldPointerTargetMB`, related manager MBs) | mixed | declaration-only | convert | medium | `NotifyBridgeRefresh/Release` + `scope.Resolver.TryResolve(...)` in MB path |
| Scope identity authoring MB family (`ScopeIdentityMB`) | mixed | declaration-only | convert | low | `InstallExplicitInstallerContribution(... ScopeIdentityMB ...)` in verified composition path |
| Visual/runtime helper MB family with nearest-scope lookup (`TryGetNearestScopeNode` callers) | mixed | declaration-only | convert | medium | `ScopeFeatureInstallerUtility.TryGetScopeNode/TryGetNearestScopeNode` usage from MB runtime callbacks |

## Classification Summary

- Total MB families classified: 10
- declaration-only (current): 0
- mixed (current): 7
- runtime-authority residue (current): 3

## Immediate Block Conditions (M1.3)

- BC-001: Any accepted-path MB requiring runtime `GetComponents + IScopeInstaller` projection (`InstallLocalFeatures`) remains.
- BC-002: Any accepted-path MB that still derives from legacy `LifetimeScope` remains active.
- BC-003: Any MB runtime callback path that requires direct resolver authority lookup to function in accepted path.

## Exit Check (M1.3)

- Runtime-affecting MB families classified: [x]
- Each family assigned RequiredAction (retain/convert/remove): [x]
- BreakRisk assigned for each family: [x]
- High-risk families identified with block conditions: [x]

## Reviewer Sign-off

- Reviewer:
- Review date:
- Decision: Approve / Reject / Conditional
- Notes:

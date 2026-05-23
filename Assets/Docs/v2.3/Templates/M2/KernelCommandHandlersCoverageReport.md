# KernelCommandHandlersCoverageReport

Source Spec: [07_KernelV23M2KernelCommandSurfaceExecutionSpec.md](../../07_KernelV23M2KernelCommandSurfaceExecutionSpec.md)
Execution Step: M2.3 Kernel Handler Implementation and Ownership Enforcement
Artifact Owner: Copilot Draft (handoff required)
Last Updated: 2026-05-23
Approval State: Draft (Ready for reviewer review)

## Coverage Rules (M2.3 Baseline)

- CoveredCommand set is fixed to M2.1 lifecycle commands.
- Handler ownership is kernel-side only; MB and local installer projection are declaration/contribution input only.
- Any accepted-path attempt to use local IScopeInstaller projection is treated as authority bypass risk.

## Records

| HandlerName | CoveredCommand | OwnershipCheck | LegacyBypassRisk | CoverageEvidence |
| --- | --- | --- | --- | --- |
| `ScopeDeclarationSubmitHandler` (current anchor: `RuntimeScopeContributionBridge.InstallHostContributions` + `InstallAcceptedAuthoringContributions`) | `KernelScope.Register` | declaration payload enters kernel build pipeline via explicit contribution entry points only; null/invalid inputs rejected by argument checks | medium | `Assets/GameLib/Script/Common/Scope/Runtime/RuntimeLifetimeScope.cs`: `InstallHostContributions`, `InstallAcceptedAuthoringContributions` |
| `KernelScopeBuildHandler` (current anchor: `KernelScopeHost.Build`) | `KernelScope.Build` | `builder.SetHostScope(this)` + `DisableHandlerCollectionResolution()` under verified runtime; guarded contribution path before `builder.Build()` | medium | `Assets/GameLib/Script/Common/Scope/Runtime/RuntimeLifetimeScope.cs`: `Build`, `ConfigureCore`, `builder.Build` |
| `KernelScopeActivateHandler` (current anchor: `KernelScopeHost.AcquireIfNeeded`) | `KernelScope.Activate` | activation executes through `RuntimeAcquireReleaseDispatcher.Acquire`; state transition tracked via verified scope state update path | low | `Assets/GameLib/Script/Common/Scope/Runtime/RuntimeLifetimeScope.cs`: `AcquireIfNeeded`, `TryUpdateVerifiedRuntimeScopeState` |
| `KernelScopeDeactivateHandler` (current anchor: `KernelScopeHost.ReleaseIfNeeded`) | `KernelScope.Deactivate` | deactivation executes through kernel dispatcher release path and tick-handler unregister sequence; no MB-owned direct lifecycle mutation in accepted path | low | `Assets/GameLib/Script/Common/Scope/Runtime/RuntimeLifetimeScope.cs`: `ReleaseIfNeeded` |
| `KernelScopeReleaseHandler` (current anchor: `ScopeDespawnCoordinator.DespawnAsync`) | `KernelScope.Release` | release/destroy path is centralized in scope coordinator and dispatches lifecycle despawn before object destruction | low | `Assets/GameLib/Script/Common/Scope/Runtime/RuntimeLifetimeScope.cs`: `ScopeDespawnCoordinator.DespawnAsync`, `DespawnAsync` |
| `KernelScopeBuildOrderHandler` (current anchor: `ScopeBuildCoordinator`) | `KernelScope.Build` | parent-first coordinated build scheduling prevents child-side autonomous build authority and enforces kernel-side order | medium | `Assets/GameLib/Script/Common/Scope/Core/ScopeBuildCoordinator.cs`: `Register`, `WaitUntilBuiltAsync`, `NotifyBuilt` |
| `AuthorityBypassRejectHandler` (current anchor: `RuntimeScopeContributionBridge.ThrowIfVerifiedRuntimeWouldUseLegacyInstallerProjection`) | `KernelScope.Build` | verified runtime blocks unsupported local `IScopeInstaller` projection and throws explicit failure before container build finalization | high | `Assets/GameLib/Script/Common/Scope/Runtime/RuntimeLifetimeScope.cs`: `ShouldRejectLegacyFeatureInstallerProjection`, `ThrowIfVerifiedRuntimeWouldUseLegacyInstallerProjection` |

## Review Notes

- Current evidence shows handler-equivalent execution points are present, but command surface is still coupled to compatibility host methods.
- Highest remaining bypass risk is legacy installer projection when verified composition guard is disabled or legacy projection allowance is opened.
- M2.4 must convert this baseline into explicit rejection matrix with fixed error code mapping.

## Gate Check

- Design ownership enforcement complete: [x]
- Design coverage complete: [x]
- Runtime ownership proof complete: [ ]
- Runtime coverage proof complete: [ ]
- Approved: [ ]

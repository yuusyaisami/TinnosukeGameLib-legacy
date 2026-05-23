# AuthorityViolationRejectionMatrix

Source Spec: [07_KernelV23M2KernelCommandSurfaceExecutionSpec.md](../../07_KernelV23M2KernelCommandSurfaceExecutionSpec.md)
Execution Step: M2.4 Authority Violation Hard-Reject Path
Artifact Owner: Copilot Draft (handoff required)
Last Updated: 2026-05-23
Approval State: Draft (Ready for reviewer review)

## Rejection Rules (M2.4 Lock)

- Accepted path authority violations are fail-closed and return structured diagnostics.
- Rejection path must terminate current command flow and must not enqueue legacy recovery actions.
- `FallbackBlockedFlag` is mandatory evidence and must remain `true` for every authority violation row.

## Records

| ViolationType | DetectionPoint | ErrorCode | DiagnosticEvidence | FallbackBlockedFlag |
| --- | --- | --- | --- | --- |
| Local DI authority request from register path | Command submission precondition guard in kernel declaration submit stage | `KCMD_AUTH_LOCAL_DI_FORBIDDEN` | payload requires `diagnosticCode`, `severity`, `commandName=KernelScope.Register`, `requestId`, `scopeHandle`, `ownerPath`, `failureBoundary=Authority` and source anchor to declaration record | true |
| Legacy installer projection detected during build | `RuntimeScopeContributionBridge.ThrowIfVerifiedRuntimeWouldUseLegacyInstallerProjection` before final build | `KCMD_BUILD_LEGACY_PROJECTION_BLOCKED` | rejected component list, scope type, detection method, and `rejectPoint=Build.PreFinalize` are recorded; exception path is converted to structured diagnostic record | true |
| Non-kernel build authority mutation attempt | `KernelScopeHost.Build` ownership guard (`builder.SetHostScope(this)` + verified composition runtime checks) | `KCMD_BUILD_AUTHORITY_VIOLATION` | diagnostic includes `commandName=KernelScope.Build`, `ownerCheckResult`, `scopeHandle`, `planRevision`, `failureBoundary=Ownership`; no alternative builder path executed | true |
| Activation-time authority bypass attempt | Activation handler guard path (`AcquireIfNeeded` with dispatcher-owned acquire) | `KCMD_ACT_AUTHORITY_BYPASS_ATTEMPT` | diagnostic includes `commandName=KernelScope.Activate`, `orderedPhase`, `handlerName`, and bypass source classification; command terminates before side-effect retries | true |
| Deactivation-time authority bypass attempt | Deactivation handler guard path (`ReleaseIfNeeded` with dispatcher-owned release) | `KCMD_DEACT_AUTHORITY_BYPASS_ATTEMPT` | diagnostic includes `commandName=KernelScope.Deactivate`, `orderedPhase`, `handlerName`, and state snapshot; no legacy release fallback is attempted | true |
| Release-time authority bypass attempt | Despawn/release coordinator rejection boundary (`ScopeDespawnCoordinator.DespawnAsync` + release authority ownership contract) | `KCMD_REL_AUTHORITY_BYPASS_ATTEMPT` | diagnostic includes `commandName=KernelScope.Release`, `releaseReason`, `terminalState`, `scopeHandle`, and authority-source tag; destruction path is not delegated to legacy resolver authority | true |
| Compatibility shell attempts runtime composition authority | Compatibility shell mapping boundary from M2.2 (`CompatibilityShellIntent=SerializationContinuityOnly`) | `KCMD_BUILD_AUTHORITY_VIOLATION` | evidence requires mapping row id, declaration digest, shell type, and mismatch reason between declared shell intent and attempted runtime authority | true |
| Resolver bypass path touches local legacy source in accepted flow | Negative probe detection from M2.2 (`AuthorityIsolationProbe`) and M2.3 reject handler anchor | `KCMD_AUTH_LOCAL_DI_FORBIDDEN` | evidence records probe id, violating symbol path (`LifetimeScope`, `InstallLocalFeatures`, resolver bypass), and failure boundary with blocked fallback confirmation | true |

## Review Notes

- Error codes are aligned with M2.1 FailureCodeSet and M2.3 handler coverage anchors.
- High-risk rejection rows are centered on build/register boundaries because they can otherwise re-enable local DI authority.
- M2.5 must execute focused negative cases for every row where `ViolationType` is authority bypass related.

## Gate Check

- Design hard-reject behavior verified: [x]
- Design fallback blocked contract verified: [x]
- Runtime hard-reject behavior verified: [ ]
- Runtime fallback blocked verified: [ ]
- Approved: [ ]

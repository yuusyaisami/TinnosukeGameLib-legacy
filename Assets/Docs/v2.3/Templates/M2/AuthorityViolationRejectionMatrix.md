# AuthorityViolationRejectionMatrix

Source Spec: [07_KernelV23M2KernelCommandSurfaceExecutionSpec.md](../../07_KernelV23M2KernelCommandSurfaceExecutionSpec.md)
実行 Step: M2.4 Authority Violation Hard-拒否 Path
成果物担当: Copilot 下書き (handoff 必須)
最終更新日: 2026-05-23
承認状態: 下書き (Ready for reviewer review)

## 拒否規則（M2.4 ロック）

- Accepted path authority violations are fail-closed and return structured diagnostics.
- Rejection path 必須である terminate current command flow and 必須である not enqueue 旧系 recovery actions.
- `FallbackBlockedFlag` is mandatory 証拠 and 必須である remain `true` for every authority violation row.

## レコード

| ViolationType | DetectionPoint | ErrorCode | DiagnosticEvidence | FallbackBlockedFlag |
| --- | --- | --- | --- | --- |
| Local DI authority request from register path | Command submission precondition guard in kernel 宣言 submit stage | `KCMD_AUTH_LOCAL_DI_FORBIDDEN` | payload requires `diagnosticCode`, `severity`, `commandName=KernelScope.Register`, `requestId`, `scopeHandle`, `ownerPath`, `failureBoundary=Authority` and source anchor to 宣言 レコード | true |
| Legacy installer projection detected during build | `RuntimeScopeContributionBridge.ThrowIfVerifiedRuntimeWouldUseLegacyInstallerProjection` before final build | `KCMD_BUILD_LEGACY_PROJECTION_BLOCKED` | rejected component list, スコープ type, detection method, and `rejectPoint=Build.PreFinalize` are recorded; exception path is converted to structured diagnostic レコード | true |
| Non-kernel build authority mutation attempt | `KernelScopeHost.Build` ownership guard (`builder.SetHostScope(this)` + verified composition 実行時 checks) | `KCMD_BUILD_AUTHORITY_VIOLATION` | diagnostic includes `commandName=KernelScope.Build`, `ownerCheckResult`, `scopeHandle`, `planRevision`, `failureBoundary=Ownership`; no alternative builder path executed | true |
| Activation-time authority bypass attempt | Activation handler guard path (`AcquireIfNeeded` with dispatcher-owned acquire) | `KCMD_ACT_AUTHORITY_BYPASS_ATTEMPT` | diagnostic includes `commandName=KernelScope.Activate`, `orderedPhase`, `handlerName`, and bypass source 分類; command terminates before side-effect retries | true |
| Deactivation-time authority bypass attempt | Deactivation handler guard path (`ReleaseIfNeeded` with dispatcher-owned release) | `KCMD_DEACT_AUTHORITY_BYPASS_ATTEMPT` | diagnostic includes `commandName=KernelScope.Deactivate`, `orderedPhase`, `handlerName`, and state snapshot; no 旧系 release フォールバック is attempted | true |
| Release-time authority bypass attempt | Despawn/release coordinator rejection boundary (`ScopeDespawnCoordinator.DespawnAsync` + release authority ownership contract) | `KCMD_REL_AUTHORITY_BYPASS_ATTEMPT` | diagnostic includes `commandName=KernelScope.Release`, `releaseReason`, `terminalState`, `scopeHandle`, and authority-source tag; destruction path is not deleゲートd to 旧系 resolver authority | true |
| 互換 shell attempts 実行時 composition authority | 互換 shell mapping boundary from M2.2 (`CompatibilityShellIntent=SerializationContinuityOnly`) | `KCMD_BUILD_AUTHORITY_VIOLATION` | 証拠 requires mapping row id, 宣言 digest, shell type, and mismatch reason between declared shell intent and attempted 実行権限 | true |
| Resolver bypass path touches local 旧系 source in accepted flow | Negative probe detection from M2.2 (`AuthorityIsolationProbe`) and M2.3 拒否 handler anchor | `KCMD_AUTH_LOCAL_DI_FORBIDDEN` | 証拠 レコード probe id, violating symbol path (`LifetimeScope`, `InstallLocalFeatures`, resolver bypass), and 失敗 boundary with ブロック フォールバック confirmation | true |

## レビューノート

- Error codes are aligned with M2.1 FailureCodeSet and M2.3 handler coverage anchors.
- High-risk rejection rows are centered on build/register boundaries because they can otherwise re-enable local DI authority.
- M2.5 必須である execute focused negative cases for every row where `ViolationType` is authority bypass related.

## ゲートチェック

- Design hard-拒否 behavior verified: [x]
- Design フォールバック ブロック contract verified: [x]
- 実行時 hard-拒否 behavior verified: [ ]
- 実行時 フォールバック ブロック verified: [ ]
- 承認済み: [ ]





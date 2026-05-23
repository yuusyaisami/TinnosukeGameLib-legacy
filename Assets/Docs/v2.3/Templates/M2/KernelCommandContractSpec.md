# KernelCommandContractSpec

Source Spec: [07_KernelV23M2KernelCommandSurfaceExecutionSpec.md](../../07_KernelV23M2KernelCommandSurfaceExecutionSpec.md)
実行 Step: M2.1 Command Contract Lock
成果物担当: Copilot 下書き (handoff 必須)
最終更新日: 2026-05-23
承認状態: 下書き (Ready for reviewer review)

## 契約規則（M2.1 ロック）

- Ordering constraint: register -> build -> activate -> deactivate -> release
- Duplicate command handling is idempotent for accepted duplicates; non-idempotent duplicates return explicit 失敗.
- Any authority violation uses hard 拒否 (no 旧系 フォールバック) and 必須である emit structured diagnostics.

## レコード

| CommandName | InputSchema | Precondition | Postcondition | FailureCodeSet | DiagnosticPayloadSchema |
| --- | --- | --- | --- | --- | --- |
| `KernelScope.Register` | `requestId`, `scopeHandle`, `scopeKind`, `serviceForm`, `declarationHash`, `parentScopeHandle?`, `sourceLocation` | スコープ is not destroyed; 宣言 hash is valid; target `scopeHandle` is not already owned by different 宣言 | 宣言 entry is recorded in kernel registry; registration state becomes `Registered`; duplicate same-payload register is accepted as idempotent no-op | `KCMD_REG_INVALID_INPUT`, `KCMD_REG_HASH_MISMATCH`, `KCMD_REG_DUPLICATE_CONFLICT`, `KCMD_AUTH_LOCAL_DI_FORBIDDEN` | `diagnosticCode`, `severity`, `commandName`, `requestId`, `scopeHandle`, `serviceForm`, `failureBoundary`, `sourceLocation`, `ownerPath`, `フォールバックBlocked=true` |
| `KernelScope.Build` | `requestId`, `scopeHandle`, `serviceForm`, `planRevision`, `requiresVerifiedComposition=true`, `allowLegacyProjection=false` | target スコープ is `Registered` and not already `Built`; build authority is kernel-owned; no 許可経路 local installer projection | 実行時 instance graph is materialized by kernel handlers only; state becomes `Built`; duplicate build on already built スコープ returns idempotent success with `duplicateIgnored=true` | `KCMD_BUILD_INVALID_STATE`, `KCMD_BUILD_PLAN_REVISION_MISMATCH`, `KCMD_BUILD_AUTHORITY_VIOLATION`, `KCMD_BUILD_LEGACY_PROJECTION_BLOCKED` | `diagnosticCode`, `severity`, `commandName`, `requestId`, `scopeHandle`, `planRevision`, `ownerCheckResult`, `rejectPoint`, `フォールバックBlocked=true` |
| `KernelScope.Activate` | `requestId`, `scopeHandle`, `activationToken`, `orderedPhase`, `trigger` | target スコープ is `Built`; スコープ not already `Active`; activation order constraints satisfied | スコープ lifecycle handlers run under kernel dispatcher; state becomes `Active`; duplicate activate on active スコープ returns idempotent success with no extra handler invocation | `KCMD_ACT_INVALID_STATE`, `KCMD_ACT_ORDER_VIOLATION`, `KCMD_ACT_HANDLER_FAILURE`, `KCMD_ACT_AUTHORITY_BYPASS_ATTEMPT` | `diagnosticCode`, `severity`, `commandName`, `requestId`, `scopeHandle`, `orderedPhase`, `handlerName?`, `failureBoundary`, `フォールバックBlocked=true` |
| `KernelScope.Deactivate` | `requestId`, `scopeHandle`, `deactivationReason`, `orderedPhase` | target スコープ is `Active`; deactivation order constraints satisfied | active handlers are released in kernel-defined order; state becomes `Built` or `Inactive`; duplicate deactivate on non-active スコープ is idempotent no-op 次の場合のみ prior terminal state is consistent | `KCMD_DEACT_INVALID_STATE`, `KCMD_DEACT_ORDER_VIOLATION`, `KCMD_DEACT_HANDLER_FAILURE`, `KCMD_DEACT_AUTHORITY_BYPASS_ATTEMPT` | `diagnosticCode`, `severity`, `commandName`, `requestId`, `scopeHandle`, `orderedPhase`, `handlerName?`, `failureBoundary`, `フォールバックBlocked=true` |
| `KernelScope.Release` | `requestId`, `scopeHandle`, `releaseReason`, `destroyFlag`, `expectedFinalState` | スコープ is not in illegal transition; 必須 deactivate completed unless forced terminal 方針 is declared; kernel owns release authority | スコープ 実行時 references are detached; registry marks スコープ terminal; terminal state becomes `Released`/`Destroyed`; duplicate release on terminal スコープ is idempotent no-op | `KCMD_REL_INVALID_STATE`, `KCMD_REL_PRECONDITION_UNMET`, `KCMD_REL_REFERENCE_LEAK`, `KCMD_REL_AUTHORITY_BYPASS_ATTEMPT` | `diagnosticCode`, `severity`, `commandName`, `requestId`, `scopeHandle`, `releaseReason`, `terminalState`, `leakCount`, `failureBoundary`, `フォールバックBlocked=true` |

## レビューノート

- Command naming is locked to kernel-スコープ lifecycle surface for M2.
- FailureCodeSet is provisional but fixed-shape: `{Domain=KCMD, Category, Cause}`.
- Any future command additions require explicit M2.1 contract update and re-approval.

## ゲートチェック

- Design completeness: [x]
- Design conformance: [x]
- 実行時 proof linked: [ ]
- 承認済み: [ ]





# KernelCommandContractSpec

Source Spec: [07_KernelV23M2KernelCommandSurfaceExecutionSpec.md](../../07_KernelV23M2KernelCommandSurfaceExecutionSpec.md)
Execution Step: M2.1 Command Contract Lock
Artifact Owner: Copilot Draft (handoff required)
Last Updated: 2026-05-23
Approval State: Draft (Ready for reviewer review)

## Contract Rules (M2.1 Lock)

- Ordering constraint: register -> build -> activate -> deactivate -> release
- Duplicate command handling is idempotent for accepted duplicates; non-idempotent duplicates return explicit failure.
- Any authority violation uses hard reject (no legacy fallback) and must emit structured diagnostics.

## Records

| CommandName | InputSchema | Precondition | Postcondition | FailureCodeSet | DiagnosticPayloadSchema |
| --- | --- | --- | --- | --- | --- |
| `KernelScope.Register` | `requestId`, `scopeHandle`, `scopeKind`, `serviceForm`, `declarationHash`, `parentScopeHandle?`, `sourceLocation` | scope is not destroyed; declaration hash is valid; target `scopeHandle` is not already owned by different declaration | declaration entry is recorded in kernel registry; registration state becomes `Registered`; duplicate same-payload register is accepted as idempotent no-op | `KCMD_REG_INVALID_INPUT`, `KCMD_REG_HASH_MISMATCH`, `KCMD_REG_DUPLICATE_CONFLICT`, `KCMD_AUTH_LOCAL_DI_FORBIDDEN` | `diagnosticCode`, `severity`, `commandName`, `requestId`, `scopeHandle`, `serviceForm`, `failureBoundary`, `sourceLocation`, `ownerPath`, `fallbackBlocked=true` |
| `KernelScope.Build` | `requestId`, `scopeHandle`, `serviceForm`, `planRevision`, `requiresVerifiedComposition=true`, `allowLegacyProjection=false` | target scope is `Registered` and not already `Built`; build authority is kernel-owned; no accepted-path local installer projection | runtime instance graph is materialized by kernel handlers only; state becomes `Built`; duplicate build on already built scope returns idempotent success with `duplicateIgnored=true` | `KCMD_BUILD_INVALID_STATE`, `KCMD_BUILD_PLAN_REVISION_MISMATCH`, `KCMD_BUILD_AUTHORITY_VIOLATION`, `KCMD_BUILD_LEGACY_PROJECTION_BLOCKED` | `diagnosticCode`, `severity`, `commandName`, `requestId`, `scopeHandle`, `planRevision`, `ownerCheckResult`, `rejectPoint`, `fallbackBlocked=true` |
| `KernelScope.Activate` | `requestId`, `scopeHandle`, `activationToken`, `orderedPhase`, `trigger` | target scope is `Built`; scope not already `Active`; activation order constraints satisfied | scope lifecycle handlers run under kernel dispatcher; state becomes `Active`; duplicate activate on active scope returns idempotent success with no extra handler invocation | `KCMD_ACT_INVALID_STATE`, `KCMD_ACT_ORDER_VIOLATION`, `KCMD_ACT_HANDLER_FAILURE`, `KCMD_ACT_AUTHORITY_BYPASS_ATTEMPT` | `diagnosticCode`, `severity`, `commandName`, `requestId`, `scopeHandle`, `orderedPhase`, `handlerName?`, `failureBoundary`, `fallbackBlocked=true` |
| `KernelScope.Deactivate` | `requestId`, `scopeHandle`, `deactivationReason`, `orderedPhase` | target scope is `Active`; deactivation order constraints satisfied | active handlers are released in kernel-defined order; state becomes `Built` or `Inactive`; duplicate deactivate on non-active scope is idempotent no-op only when prior terminal state is consistent | `KCMD_DEACT_INVALID_STATE`, `KCMD_DEACT_ORDER_VIOLATION`, `KCMD_DEACT_HANDLER_FAILURE`, `KCMD_DEACT_AUTHORITY_BYPASS_ATTEMPT` | `diagnosticCode`, `severity`, `commandName`, `requestId`, `scopeHandle`, `orderedPhase`, `handlerName?`, `failureBoundary`, `fallbackBlocked=true` |
| `KernelScope.Release` | `requestId`, `scopeHandle`, `releaseReason`, `destroyFlag`, `expectedFinalState` | scope is not in illegal transition; required deactivate completed unless forced terminal policy is declared; kernel owns release authority | scope runtime references are detached; registry marks scope terminal; terminal state becomes `Released`/`Destroyed`; duplicate release on terminal scope is idempotent no-op | `KCMD_REL_INVALID_STATE`, `KCMD_REL_PRECONDITION_UNMET`, `KCMD_REL_REFERENCE_LEAK`, `KCMD_REL_AUTHORITY_BYPASS_ATTEMPT` | `diagnosticCode`, `severity`, `commandName`, `requestId`, `scopeHandle`, `releaseReason`, `terminalState`, `leakCount`, `failureBoundary`, `fallbackBlocked=true` |

## Review Notes

- Command naming is locked to kernel-scope lifecycle surface for M2.
- FailureCodeSet is provisional but fixed-shape: `{Domain=KCMD, Category, Cause}`.
- Any future command additions require explicit M2.1 contract update and re-approval.

## Gate Check

- Design completeness: [x]
- Design conformance: [x]
- Runtime proof linked: [ ]
- Approved: [ ]

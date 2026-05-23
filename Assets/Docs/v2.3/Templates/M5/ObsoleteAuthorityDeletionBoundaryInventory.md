# ObsoleteAuthorityDeletionBoundaryInventory

Source Spec: [10_KernelV23M5HardeningAndDeleteExecutionSpec.md](../../10_KernelV23M5HardeningAndDeleteExecutionSpec.md)
Execution Step: M5.1 Deletion Boundary Freeze
Artifact Owner: Copilot Draft (handoff required)
Last Updated: 2026-05-23
Approval State: design-ready (Deletion boundary defined; physical delete pending)

## ApprovalState Vocabulary

- `not-started`: boundary records are missing or not reviewable
- `design-ready`: boundary records are complete and internally reviewed
- `runtime-verified`: delete execution evidence is captured for all targets
- `approved`: reviewer sign-off is completed

## Boundary Freeze Policy (M5.1)

- Targets classified as `delete` must be physically removed in M5.2 (disable-only is not accepted).
- `retain-for-serialization` targets are allowed only for reference continuity and must remain non-authoritative.
- `remove-later` targets require explicit blocking rationale and a bounded follow-up action.
- Any accepted-path authority still reachable from a non-delete target is treated as policy violation.

## Records

| TargetId | TargetPath | Classification | MigrationOwner | DeleteWave | ContinuityConstraint |
| --- | --- | --- | --- | --- | --- |
| M5-DEL-001 | Root boot bypass route before plan handshake (`RootBootEntry -> scene-local trigger -> KernelScope.Register/Build/Activate` direct path) | delete | Root Boot Runtime Owner | M5-W1 | No continuity exception; accepted path must start at verified plan gate only |
| M5-DEL-002 | Non-plan registration expansion route (`Register` stage target injection from discovery source) | delete | Root Registration Owner | M5-W1 | No continuity exception; non-plan registration target path must be unreachable |
| M5-DEL-003 | Ad-hoc build authority route (`KernelScope.Build` invoked from non-kernel owner / unregistered handle path) | delete | Root Composition Owner | M5-W1 | No continuity exception; build path must be kernel-owned only |
| M5-DEL-004 | Activation shortcut route (scene callback timing path bypassing plan-derived order signature) | delete | Runtime Ordering Owner | M5-W2 | No continuity exception; deterministic activation order must remain plan-derived |
| M5-DEL-005 | Deactivate/release local fallback route (`KernelScope.Deactivate/Release` fallback callback authority path) | delete | Lifecycle Runtime Owner | M5-W2 | No continuity exception; fallback reachability must remain false |
| M5-DEL-006 | Post-reject continuation route (plan mismatch reject boundary followed by lifecycle continuation) | delete | Plan Validation Owner | M5-W2 | No continuity exception; reject path must terminate flow fail-closed |
| M5-DEL-007 | Compatibility shell serialized member set used only for reference continuity (`serialization-only shell surface`) | retain-for-serialization | Compatibility Runtime Owner | M5-W3 | Allowed only for serialized reference continuity; authority behavior flag must stay false |
| M5-DEL-008 | Diagnostic compatibility adapter path for legacy tooling (`schema-translation adapter path`) | remove-later | Diagnostics Runtime Owner | M5-W3 | Temporary retention allowed for tool compatibility; must not be reachable in accepted runtime path |

## Remove-Later Control

| TargetId | FollowUpAction | DueBy | ExitCondition |
| --- | --- | --- | --- |
| M5-DEL-008 | Remove diagnostic compatibility adapter after downstream tooling migrates to stable schema contract | 2026-06-30 | adapter path physically deleted and no accepted-path reference remains |

## Review Notes

- This artifact is in M5.1 start state: deletion boundary is frozen at design level.
- `delete` targets align with M4 cutover/negative verification boundaries that identified obsolete authority routes.
- `retain-for-serialization` and `remove-later` rows require strict non-authoritative runtime proof in M5.5/M5.3.

## Gate Check

- Deletion boundary design lock complete: [x]
- Classification complete: [x]
- Deletion boundary approved: [ ]

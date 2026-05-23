# RootSceneIntegrationBoundaryMap

Source Spec: [09_KernelV23M4RootSceneIntegrationCutoverExecutionSpec.md](../../09_KernelV23M4RootSceneIntegrationCutoverExecutionSpec.md)
Execution Step: M4.1 Root/Scene Boundary Freeze
Artifact Owner: Copilot Draft (handoff required)
Last Updated: 2026-05-23
Approval State: Draft (Boundary set defined; runtime cutover pending)

## ApprovalState Vocabulary

- `not-started`: boundary records are missing or not reviewable
- `design-ready`: boundary records are complete and internally reviewed
- `runtime-verified`: runtime cutover evidence is collected for all targets
- `approved`: reviewer sign-off is completed

## Boundary Freeze Policy (M4.1)

- Root/scene accepted path must not rely on discovery/local-authority runtime composition.
- Integration targets below are frozen as M4 cutover scope; new targets require explicit change note.
- `CurrentOwner` represents pre-cutover authority in accepted path.
- `TargetOwner` represents post-cutover kernel-owned authority model.

## Records

| IntegrationTargetName | CurrentOwner | TargetOwner | MigrationOwner | CutoverWave |
| --- | --- | --- | --- | --- |
| Root Scene Boot Entry and Plan Handshake | Legacy root scene bootstrap entry (scene-local boot trigger authority) | Verified-plan-first kernel boot authority | Root Boot Runtime Owner | M4-W1 |
| Root Scene Registration Dispatch (`Register` path) | Discovery-coupled root registration path | Kernel command-surface registration authority (`KernelScope.Register`) | Root Registration Owner | M4-W1 |
| Root Scene Build Dispatch (`Build` path) | Local composition build authority in scene bootstrap flow | Kernel lifecycle build authority (`KernelScope.Build`) | Root Composition Owner | M4-W1 |
| Root Scene Activation Ordering (`Activate` path) | Scene bootstrap callback sequencing (non-plan deterministic risk) | Kernel-owned deterministic activation ordering authority (`KernelScope.Activate`) | Runtime Ordering Owner | M4-W2 |
| Root Scene Deactivation/Release Coordination | Legacy release callback chain with local fallback tolerance | Kernel-owned deactivation/release authority (`KernelScope.Deactivate` / `KernelScope.Release`) | Lifecycle Runtime Owner | M4-W2 |
| Root Scene Plan Source Validation and Mismatch Rejection | Mixed runtime path where missing/mismatch plan handling can drift by caller | Centralized verified-plan validation and explicit reject authority | Plan Validation Owner | M4-W2 |
| Root Scene Integration Diagnostics Emission | Fragmented diagnostics ownership across scene-local handlers | Kernel command-surface diagnostics authority with structured payload contract | Diagnostics Runtime Owner | M4-W3 |
| Scene Transition Integration Boundary (root to scene handoff) | Transition-time registration/activation handoff coupled to scene-local routing | Plan-driven root-to-scene handoff authority under kernel runtime orchestration | Scene Integration Owner | M4-W3 |

## Review Notes

- This artifact is in M4.1 start state: boundary scope is frozen at design level.
- Cutover waves are ordered to prioritize plan-first registration/build authority before downstream ordering/diagnostics stabilization.
- Any accepted-path target outside this map is treated as out-of-policy until mapped and approved.

## Gate Check

- Ownership boundary design lock complete: [x]
- Target assignments complete: [x]
- Boundary lock approved: [ ]

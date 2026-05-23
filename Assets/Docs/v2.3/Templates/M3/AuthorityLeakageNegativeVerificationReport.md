# AuthorityLeakageNegativeVerificationReport

Source Spec: [08_KernelV23M3LeafScopeDemotionExecutionSpec.md](../../08_KernelV23M3LeafScopeDemotionExecutionSpec.md)
Execution Step: M3.5 Authority Leakage Negative Verification
Artifact Owner: Copilot Draft (handoff required)
Last Updated: 2026-05-23
Approval State: design-ready (Case definition complete; runtime verification pending)

## ApprovalState Vocabulary

- `not-started`: required records are missing or not reviewable
- `design-ready`: all negative cases are defined and internally reviewed
- `runtime-verified`: reject/fallback runtime evidence is captured for all cases
- `approved`: reviewer sign-off is completed

## Negative Verification Policy (M3.5)

- Each negative case targets an attempted legacy-authority path in accepted flow.
- `ExpectedRejectCode` must map to structured reject codes defined in M2 authority rejection artifacts.
- `FallbackObservedFlag` pass criteria value is `false`.
- `ObservedResult` values at start phase should explicitly state pending execution.

## FallbackObservedFlag Vocabulary

- `pending`: runtime fallback observation is not yet captured
- `false`: fallback path was not observed
- `true`: fallback path was observed

## ObservedResult Vocabulary

- `pending`: case is defined but runtime execution is not yet completed
- `pass`: reject code and fallback-unreachable conditions are both satisfied
- `fail`: reject code mismatch or fallback reachability detected
- `blocked`: case execution could not be completed due to harness/environment issue

## Evidence Minimum Fields

Each runtime evidence update must include:

- test run id / execution timestamp
- attempted authority path symbol and trigger payload
- actual reject code and diagnostic payload
- fallback reachability observation (`FallbackObservedFlag` basis)
- pass/fail rationale linked to `NegativeCaseId`

## Records

| NegativeCaseId | TriggerCondition | ExpectedRejectCode | ObservedResult | FallbackObservedFlag | EvidenceAnchor |
| --- | --- | --- | --- | --- | --- |
| M3-NEG-001 | Selection/pointer path attempts nearest-scope + resolver-based runtime authority in MB callback | KCMD_AUTH_LOCAL_DI_FORBIDDEN | pending | pending | M3-DES-001, M3-RPL-001, Assets/GameLib/Script/Project/Scene/SelectableRuntime/MB/SelectableRuntimeMB.cs |
| M3-NEG-002 | Runtime spawner/pool path attempts local installer projection or non-kernel build authority | KCMD_BUILD_LEGACY_PROJECTION_BLOCKED | pending | pending | M3-DES-002, M3-RPL-002, Assets/GameLib/Script/Project/Scene/Runtime/RuntimeManager/RuntimeManagerMB.cs, Assets/GameLib/Script/Common/Scope/Runtime/RuntimeLifetimeScope.cs |
| M3-NEG-003 | Chunk runtime path attempts resolver-coupled fallback authority after declaration mapping | KCMD_BUILD_AUTHORITY_VIOLATION | pending | pending | M3-DES-003, M3-RPL-003, Assets/GameLib/Script/Project/Scene/Chunk/MB/ChunkStreamerMB.cs |
| M3-NEG-004 | Scroll channel path attempts mutable runtime authority through acquire-time resolver dependencies | KCMD_AUTH_LOCAL_DI_FORBIDDEN | pending | pending | M3-DES-004, M3-RPL-004, Assets/GameLib/Script/Project/Scene/Channels/Scroll/ScrollChannelHubService.cs |
| M3-NEG-005 | Transform channel execution attempts ancestor resolver traversal fallback as authority source | KCMD_AUTH_LOCAL_DI_FORBIDDEN | pending | pending | M3-DES-005, M3-RPL-005, Assets/GameLib/Script/Common/Commands/VNext/Executors/Movement/MovementExecutors.cs |
| M3-NEG-006 | AutoSpawn path attempts hub/resolver discovery as authority source in accepted path | KCMD_AUTH_LOCAL_DI_FORBIDDEN | pending | pending | M3-DES-006, M3-RPL-006, Assets/GameLib/Script/Common/Commands/VNext/Executors/Channels/AutoSpawnChannelControlExecutor.cs |
| M3-NEG-007 | Map node runtime path attempts direct resolver-coupled runner authority outside kernel command boundary | KCMD_AUTH_LOCAL_DI_FORBIDDEN | pending | pending | M3-DES-007, M3-RPL-007, Assets/GameLib/Script/Project/System/Map/Runtime/MapNodePlayerService.cs |
| M3-NEG-008 | Background/slider runtime path attempts scope-resolver attached lifecycle fallback authority | KCMD_REL_AUTHORITY_BYPASS_ATTEMPT | pending | pending | M3-DES-008, M3-RPL-008, Assets/GameLib/Script/Project/UI/Core/Elements/Slider/Visual/WorldSpaceSliderVisualBackend.cs, Assets/GameLib/Script/Project/UI/Core/Elements/Slider/Visual/ScreenSliderVisualBackend.cs |

## Review Notes

- This artifact is in M3.5 start state: negative-case set is complete, runtime observations are pending.
- Reject code mapping is aligned with M2 authority rejection matrix and command-surface failure taxonomy.
- Approval progression target: `design-ready -> runtime-verified -> approved`.

## Gate Check

- Negative case design coverage complete: [x]
- Hard reject verified (runtime): [ ]
- Fallback unreachable (runtime): [ ]
- Negative verification approved: [ ]

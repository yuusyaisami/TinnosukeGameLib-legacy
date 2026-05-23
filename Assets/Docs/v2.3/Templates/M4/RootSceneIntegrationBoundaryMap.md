# RootSceneIntegrationBoundaryMap

Source Spec: [09_KernelV23M4RootSceneIntegrationCutoverExecutionSpec.md](../../09_KernelV23M4RootSceneIntegrationCutoverExecutionSpec.md)
実行 Step: M4.1 Root/Scene Boundary Freeze
成果物担当: Copilot 下書き (handoff 必須)
最終更新日: 2026-05-23
承認状態: 下書き (Boundary set defined; 実行時 cutover pending)

## 承認状態語彙

- `not-started`: boundary レコード are missing or not reviewable
- `design-ready`: boundary レコード are 完了 and internally reviewed
- `実行時-verified`: 実行時 cutover 証拠 is collected for all targets
- `承認済み`: reviewer sign-off is completed

## Boundary Freeze 方針 (M4.1)

- Root/scene accepted path 必須である not rely on discovery/local-authority 実行時 composition.
- Integration targets below are frozen as M4 cutover スコープ; new targets require explicit change note.
- `CurrentOwner` represents pre-cutover authority in accepted path.
- `TargetOwner` represents post-cutover kernel-owned authority model.

## レコード

| IntegrationTargetName | CurrentOwner | TargetOwner | MigrationOwner | CutoverWave |
| --- | --- | --- | --- | --- |
| Root Scene Boot Entry and Plan Handshake | Legacy root scene bootstrap entry (scene-local boot trigger authority) | Verified-plan-first kernel boot authority | Root Boot 実行時 担当 | M4-W1 |
| Root Scene Registration Dispatch (`Register` path) | Discovery-coupled root registration path | Kernel command-surface registration authority (`KernelScope.Register`) | Root Registration 担当 | M4-W1 |
| Root Scene Build Dispatch (`Build` path) | Local composition build authority in scene bootstrap flow | Kernel lifecycle build authority (`KernelScope.Build`) | Root Composition 担当 | M4-W1 |
| Root Scene Activation Ordering (`Activate` path) | Scene bootstrap callback sequencing (non-plan deterministic risk) | Kernel-owned deterministic activation ordering authority (`KernelScope.Activate`) | 実行時 Ordering 担当 | M4-W2 |
| Root Scene Deactivation/Release Coordination | Legacy release callback chain with local フォールバック tolerance | Kernel-owned deactivation/release authority (`KernelScope.Deactivate` / `KernelScope.Release`) | Lifecycle 実行時 担当 | M4-W2 |
| Root Scene Plan Source 検証 and Mismatch Rejection | Mixed 実行時 path where missing/mismatch plan handling can drift by caller | Centralized verified-plan validation and explicit 拒否 authority | Plan 検証 担当 | M4-W2 |
| Root Scene Integration Diagnostics Emission | Fragmented diagnostics ownership across scene-local handlers | Kernel command-surface diagnostics authority with structured payload contract | Diagnostics 実行時 担当 | M4-W3 |
| Scene Transition Integration Boundary (root to scene handoff) | Transition-time registration/activation handoff coupled to scene-local routing | Plan-driven root-to-scene handoff authority under kernel 実行時 orchestration | Scene Integration 担当 | M4-W3 |

## レビューノート

- This artifact is in M4.1 start state: boundary スコープ is frozen at design level.
- Cutover waves are ordered to prioritize plan-first registration/build authority before downstream ordering/diagnostics stabilization.
- Any 許可経路 target outside this map is treated as out-of-方針 until mapped and 承認済み.

## ゲートチェック

- Ownership boundary design lock 完了: [x]
- Target assignments 完了: [x]
- Boundary lock 承認済み: [ ]




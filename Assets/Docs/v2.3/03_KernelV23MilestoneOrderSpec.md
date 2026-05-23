# Kernel v2.3 マイルストーン順序仕様

## 文書状態

- 文書 ID: 03_KernelV23MilestoneOrderSpec
- 状態: 下書き
- 役割: v2.3 是正設計の実行順序を定義する
- 依存先:
  - [00_KernelV23OverviewSpec.md](00_KernelV23OverviewSpec.md)
  - [01_KernelV23ServiceRuntimeModelSpec.md](01_KernelV23ServiceRuntimeModelSpec.md)
  - [02_KernelV23AuthoringRegistrationFlowSpec.md](02_KernelV23AuthoringRegistrationFlowSpec.md)

## 詳細実行仕様

この順序仕様は実行順序の統制契約である。
実行仕様（05-11）は下位詳細仕様であり、本書に準拠しなければならない。

- [05_KernelV23M0FullMigrationContractFreezeExecutionSpec.md](05_KernelV23M0FullMigrationContractFreezeExecutionSpec.md)
- [06_KernelV23M1SpecLockAndCensusExecutionSpec.md](06_KernelV23M1SpecLockAndCensusExecutionSpec.md)
- [07_KernelV23M2KernelCommandSurfaceExecutionSpec.md](07_KernelV23M2KernelCommandSurfaceExecutionSpec.md)
- [08_KernelV23M3LeafScopeDemotionExecutionSpec.md](08_KernelV23M3LeafScopeDemotionExecutionSpec.md)
- [09_KernelV23M4RootSceneIntegrationCutoverExecutionSpec.md](09_KernelV23M4RootSceneIntegrationCutoverExecutionSpec.md)
- [10_KernelV23M5HardeningAndDeleteExecutionSpec.md](10_KernelV23M5HardeningAndDeleteExecutionSpec.md)
- [11_KernelV23M6FullProofAndReleaseClaimExecutionSpec.md](11_KernelV23M6FullProofAndReleaseClaimExecutionSpec.md)

## マイルストーン

### M0: Full-移行 Contract Freeze

- freeze non-negotiable completion target: 100% deletion of スコープ-local DI 実行権限
- freeze non-negotiable サービス rebuild target: all services migrated with stable name/reference contract
- freeze release rejection rule for any residual local-container authority on 許可経路

Detailed 実行 is defined in:
- [05_KernelV23M0FullMigrationContractFreezeExecutionSpec.md](05_KernelV23M0FullMigrationContractFreezeExecutionSpec.md)

Exit criteria:
- full-移行 contract 承認済み
- no ambiguity remains on allowed サービス forms

### M1: Spec Lock and Census

- freeze two-form サービス rule (AoS / 範囲-ServiceInstance)
- census all 実行経路s still using スコープ-local DI authority
- classify MBs into 宣言-only vs 実行時-authority residue
- create 完了 サービスファミリー 在庫 with 移行担当 and target form

Detailed 実行 is defined in:
- [06_KernelV23M1SpecLockAndCensusExecutionSpec.md](06_KernelV23M1SpecLockAndCensusExecutionSpec.md)

M1 overall exit criteria:
- residue 在庫 完了
- 禁止 authority paths listed with source anchors
- M1.1 through M1.5 exit criteria all satisfied

### M1.x Breakdown (実行 Sequence)

#### M1.1: Rule Lock

- lock final normative text for two サービス forms
- lock prohibition text for スコープ-local DI 実行権限
- lock 互換 boundary text (name/reference continuity)

Exit criteria:
- no unresolved normative conflicts between 00/01/02/04

#### M1.2: Authority Path Census

- enumerate all 実行権限 paths currently in 許可経路
- tag each path with source anchor (file, symbol, call path)
- tag ownership class: kernel-owned, スコープ-owned, mixed, unknown

Exit criteria:
- authority census coverage reaches 100% for 許可経路

#### M1.3: MB Responsibility 分類

- classify MBs into 宣言-only, mixed, 実行時-authority residue
- レコード 移行 action per MB family
- レコード immediate block conditions for high-risk MB families

Exit criteria:
- MB 分類 テーブル 完了 for all 実行時-affecting MB families

#### M1.4: サービス Family 在庫 Freeze

- create mandatory 在庫 レコード for all サービス families
- assign 移行担当 and target サービス form
- assign 互換 risk levels (name/reference)

Exit criteria:
- no サービスファミリー remains without 担当 or target form

#### M1.5: Risk and ゲート Baseline

- set baseline risk register for 移行 blockers
- define M2 entry ゲートs using M1 在庫 outputs
- define rejection triggers for hidden 旧系 authority residue

Exit criteria:
- M2 start ゲート package 承認済み

### M2: Kernel Command Surface

- implement kernel-side registration/build command surface
- implement スコープ 宣言 submission endpoint contract
- block 許可経路 from local container build authority
- provide kernel registration/build/activate/release command handlers for both サービス forms

Detailed 実行 is defined in:
- [07_KernelV23M2KernelCommandSurfaceExecutionSpec.md](07_KernelV23M2KernelCommandSurfaceExecutionSpec.md)

M2 overall exit criteria:
- kernel command surface artifacts 完了 and 承認済み
- 許可経路 rejects local DI authority acquisition at 実行時
- M2.1 through M2.6 exit criteria all satisfied

### M2.x Breakdown (実行 Sequence)

#### M2.1: Command Contract Lock

- lock command contract for register/build/activate/deactivate/release
- lock idempotency, ordering, and 失敗 semantics
- lock diagnostics payload schema for all command outcomes

Exit criteria:
- no unresolved command contract ambiguity remains

#### M2.2: Authoring-to-Command Mapping

- map 宣言 payload to deterministic command sequence
- map ServiceForm (AoS / 範囲-ServiceInstance) to 実行 branch
- 拒否 undeclared command target at mapping stage

Exit criteria:
- deterministic mapping テーブル 承認済み with no フォールバック path

#### M2.3: Kernel Command Handler Implementation

- implement kernel command handlers for both サービス forms
- enforce kernel ownership for slot/instance lifecycle operations
- prohibit スコープ-local authority injection into handler 実行 path

Exit criteria:
- all 必須 handlers implemented and ownership checks enforced

#### M2.4: 実行時 Authority Block Enforcement

- introduce hard 拒否 path when local DI authority is requested in 許可経路
- define explicit error codes and diagnostics for authority violation
- verify no silent フォールバック to 旧系 construction path

Exit criteria:
- authority violation always returns explicit structured 失敗

#### M2.5: Focused 実行時 検証

- execute focused 実行時 tests for command order, idempotency, and 失敗 behavior
- verify 宣言-only MB behavior under command-driven 実行時
- verify no 許可経路 実行時 フォールバック to local container build

Exit criteria:
- focused 実行時 検証 passes with zero authority leakage findings

#### M2.6: M3 Entry ゲート Proof Package

- publish command surface proof package for M3 handoff
- publish unresolved risks and 必須 mitigations
- block M3 start when 必須 M2 証拠 is missing

Exit criteria:
- M3 entry ゲート 承認済み with 完了 M2 証拠 set

### M3: Leaf 範囲 Demotion

- demote entity/ui-element domains from スコープ-local DI authority
- route leaf services to AoS or kernel-owned instance registry
- keep 互換 bridges only for serialization continuity
- preserve existing サービス names and references while replacing internal ownership model

Detailed 実行 is defined in:
- [08_KernelV23M3LeafScopeDemotionExecutionSpec.md](08_KernelV23M3LeafScopeDemotionExecutionSpec.md)

M3 overall exit criteria:
- all leaf-domain target services cut over to kernel-owned サービス forms
- 許可経路 in leaf domains contains zero スコープ-local DI 実行権限
- M3.1 through M3.6 exit criteria all satisfied

### M3.x Breakdown (実行 Sequence)

#### M3.1: Leaf Domain Freeze

- freeze leaf-domain 移行 スコープ (entity and ui-element families)
- freeze per-domain サービスファミリー list and 移行担当
- freeze per-family target サービス form (AoS or 範囲-ServiceInstance)

Exit criteria:
- no leaf-domain サービスファミリー remains unassigned

#### M3.2: サービス Cutover Design Lock

- lock cutover design for each leaf サービスファミリー
- lock 互換 bridge behavior (serialization continuity only)
- lock disallowed behavior (実行権限 via 旧系 local DI)

Exit criteria:
- all leaf サービス families have 承認済み cutover design

#### M3.3: Leaf 実行時 Path Replacement

- replace 許可経路 実行権限 from local DI to kernel command handlers
- route 実行時 state ownership to AoS slots or kernel instance registry
- remove 許可経路 実行時 installer discovery in leaf domains

Exit criteria:
- leaf 許可経路 executes without local DI authority dependency

#### M3.4: Name/Reference Continuity 検証

- 検証する サービス naming continuity for all migrated leaf families
- 検証する scene/prefab/script reference continuity
- 検証する 互換 shell non-authoritative behavior

Exit criteria:
- no name/reference break detected in leaf-domain cutover

#### M3.5: Authority Leakage Negative 検証

- run negative 検証 for authority leakage and フォールバック behavior
- assert hard 拒否 on 旧系 authority acquisition attempts
- assert no silent recovery to local-container 実行 path

Exit criteria:
- zero authority leakage findings in leaf-domain 許可経路

#### M3.6: M4 Entry ゲート 証拠 Package

- publish M3 completion 証拠 and open risk list
- publish unresolved items requiring root/scene integration handling
- block M4 start when mandatory M3 証拠 is incomplete

Exit criteria:
- M4 entry ゲート 承認済み with 完了 M3 証拠 set

### M4: Root/Scene Integration Cutover

- align scene-initial スコープ compile output with 実行時 registration flow
- enforce plan-first boot and registration ordering
- remove 実行時 discovery as accepted composition mechanism
- enforce 宣言-only MB 実行時 behavior for migrated services

Detailed 実行 is defined in:
- [09_KernelV23M4RootSceneIntegrationCutoverExecutionSpec.md](09_KernelV23M4RootSceneIntegrationCutoverExecutionSpec.md)

M4 overall exit criteria:
- root/scene 許可経路 runs only from verified plan and kernel command surface
- scene-initial registration ordering is deterministic and reproducible
- M4.1 through M4.6 exit criteria all satisfied

### M4.x Breakdown (実行 Sequence)

#### M4.1: Root/Scene Boundary Freeze

- freeze root/scene integration boundary and ownership map
- freeze scene-initial スコープ set and registration targets
- freeze M4 移行 wave order and 担当 assignment

Exit criteria:
- no root/scene integration target remains unassigned

#### M4.2: Plan-First Boot Contract Lock

- lock boot contract requiring verified-plan-first 実行
- lock ordering rules for scene load, スコープ materialization, and registration commands
- lock 拒否 behavior when verified plan is missing or mismatched

Exit criteria:
- boot contract 承認済み with explicit 拒否 conditions

#### M4.3: Scene Registration Path Cutover

- replace residual root/scene 許可経路 discovery with plan-driven registration
- route scene-initial スコープ registration through kernel command surface only
- prohibit 許可経路 shortcut registration from local authority holders

Exit criteria:
- scene registration 許可経路 is plan-driven only

#### M4.4: Deterministic Ordering and Reproducibility 検証

- verify deterministic 実行 order across repeated scene boot runs
- verify identical registration outcome for identical verified plan input
- verify diagnostics include plan source and ordering 証拠

Exit criteria:
- reproducibility 検証 passes with zero ordering drift

#### M4.5: Root/Scene Authority Leakage Negative 検証

- run negative 検証 for root/scene 旧系 authority entry attempts
- assert hard 拒否 on discovery-based 実行時 composition attempts
- assert no フォールバック to スコープ-local DI authority in root/scene 許可経路

Exit criteria:
- zero authority leakage findings in root/scene 許可経路

#### M4.6: M5 Entry ゲート 証拠 Package

- publish M4 completion 証拠 and unresolved global deletion risks
- publish 互換 shell retirement readiness status
- block M5 start when mandatory M4 証拠 is incomplete

Exit criteria:
- M5 entry ゲート 承認済み with 完了 M4 証拠 set

### M5: Hardening and Delete

- delete obsolete スコープ-local DI authority paths
- harden diagnostics and 失敗 behavior
- 検証する performance budget for high-cardinality domains
- remove temporary 互換 shells that are no longer needed after reference-safe cutover validation

Detailed 実行 is defined in:
- [10_KernelV23M5HardeningAndDeleteExecutionSpec.md](10_KernelV23M5HardeningAndDeleteExecutionSpec.md)

M5 overall exit criteria:
- obsolete authority paths are physically removed from 許可実行経路
- diagnostics/失敗 behavior satisfy hardening requirements with structured 証拠
- M5.1 through M5.6 exit criteria all satisfied

### M5.x Breakdown (実行 Sequence)

#### M5.1: Deletion Boundary Freeze

- freeze final deletion boundary for obsolete authority paths
- freeze 互換-shell retirement targets and 担当 assignments
- freeze protected continuity boundary (allowed serialization continuity only)

Exit criteria:
- deletion boundary 在庫 承認済み with no unknown target

#### M5.2: Obsolete Authority Path Physical Delete

- physically delete obsolete スコープ-local DI authority paths
- remove 許可経路 entry points to deleted 旧系 authority
- block reintroduction routes through 互換 shims

Exit criteria:
- no deleted authority path remains reachable from 許可実行経路

#### M5.3: Diagnostics and 失敗 Hardening

- harden structured diagnostics for authority violations and contract failures
- harden explicit 失敗 codes for missing plan/mapping/ownership preconditions
- prohibit silent フォールバック and exception swallow behavior in 許可経路

Exit criteria:
- hardening 検証 shows explicit 失敗 behavior for all 必須 拒否 classes

#### M5.4: Performance Budget 検証

- 検証する 実行時 performance budgets after deletion and hardening changes
- verify no regression from 互換 shell retirement and authority path removal
- verify hot path constraints remain within budget envelope

Exit criteria:
- performance validation passes with no unresolved budget violation

#### M5.5: 互換 Shell Retirement 検証

- 検証する retirement of temporary 互換 shells where obsolete
- 検証する retained shells are serialization-only and non-authoritative
- 検証する no reference break introduced by shell retirement

Exit criteria:
- 互換 shell state is compliant with retirement 方針

#### M5.6: M6 Entry ゲート 証拠 Package

- publish M5 completion 証拠 and residual proof risks for M6
- publish final unresolved issues that block release claim
- block M6 start when mandatory M5 証拠 is incomplete

Exit criteria:
- M6 entry ゲート 承認済み with 完了 M5 証拠 set

### M6: Full-Proof and Release Claim

- prove 100% 移行 completion across all サービス families
- prove zero 許可経路 スコープ-local DI 実行権限 residue
- prove サービス name stability and reference continuity constraints remained intact during 移行

Detailed 実行 is defined in:
- [11_KernelV23M6FullProofAndReleaseClaimExecutionSpec.md](11_KernelV23M6FullProofAndReleaseClaimExecutionSpec.md)

M6 overall exit criteria:
- full-proof package is 完了, internally consistent, and independently reviewable
- release claim satisfies all non-negotiable v2.3 completion contracts
- M6.1 through M6.6 exit criteria all satisfied

### M6.x Breakdown (実行 Sequence)

#### M6.1: Proof 範囲 Freeze

- freeze final proof スコープ covering all サービス families and 許可実行経路s
- freeze 必須 証拠 set and ownership for each proof section
- freeze claim boundary (what is asserted, what is out of スコープ)

Exit criteria:
- no mandatory proof target remains unassigned or undefined

#### M6.2: 移行 Completion Proof Assembly

- assemble 証拠 that all サービス families completed 移行
- prove no exempt family remains on 旧系 authority in 許可経路
- prove 移行 在庫 closure and 担当 accountability

Exit criteria:
- 移行 completion proof passes completeness and traceability checks

#### M6.3: Authority-Zero Proof Assembly

- assemble 証拠 for zero 許可経路 スコープ-local DI 実行権限 residue
- prove deletion/cutover/hardening outputs converge to zero-authority state
- prove no reachable フォールバック to 旧系 authority path

Exit criteria:
- authority-zero proof passes reachability and residue checks

#### M6.4: Continuity Proof Assembly

- assemble 証拠 for サービス name continuity at integration boundaries
- assemble 証拠 for scene/prefab/script reference continuity
- prove retained 互換 shells are non-authoritative and 方針-compliant

Exit criteria:
- continuity proof passes with no unresolved break or 方針 violation

#### M6.5: Independent 検証 and Claim Review

- perform independent validation of proof package consistency
- run claim review against frozen M0 contract and all milestone ゲート outputs
- レコード formal accept/拒否 decision with rationale

Exit criteria:
- review board decision is recorded with explicit acceptance conditions

#### M6.6: Release Claim Finalization and Publication

- finalize v2.3 release claim artifact set
- publish unresolved risks and post-release obligations if any
- block release publication when mandatory claim 証拠 is incomplete

Exit criteria:
- release claim package 承認済み and publication-ready

## テストケース

| テストケース | 目的 | 実行注記 |
| --- | --- | --- |
| TC-V23-03-01 | 確認 milestone order starts with spec lock and residue census. | M1 必須である include 在庫 and 分類. |
| TC-V23-03-02 | 確認 kernel command surface is delivered before leaf demotion completion. | M2 必須である precede M3 exit claim. |
| TC-V23-03-03 | 確認 leaf demotion explicitly targets entity/ui-element domains. | M3 必須である name leaf domains and authority removal. |
| TC-V23-03-04 | 確認 final hardening requires deletion of obsolete authority paths. | M5 を必須とする delete and ゲート pass. |
| TC-V23-03-05 | 確認 milestone order includes full-移行 contract freeze. | M0 を必須とする 100% deletion target and name/reference continuity target. |
| TC-V23-03-06 | 確認 milestone order includes full-proof release claim. | M6 を必須とする all-サービス 移行 proof and zero-authority-residue proof. |
| TC-V23-03-07 | 確認 M1 is broken down into M1.x 実行 sequence. | 仕様は次を定義する M1.1 through M1.5 with exit criteria. |
| TC-V23-03-08 | 確認 M0/M1 detailed 実行 specs are linked. | 仕様は次を参照する 05 and 06 as detailed 実行 documents. |
| TC-V23-03-09 | 確認 M2 is broken down into M2.x 実行 sequence. | 仕様は次を定義する M2.1 through M2.6 with exit criteria. |
| TC-V23-03-10 | 確認 M2 detailed 実行 spec is linked. | 仕様は次を参照する 07 as detailed 実行 document. |
| TC-V23-03-11 | 確認 M3 is broken down into M3.x 実行 sequence. | 仕様は次を定義する M3.1 through M3.6 with exit criteria. |
| TC-V23-03-12 | 確認 M3 detailed 実行 spec is linked. | 仕様は次を参照する 08 as detailed 実行 document. |
| TC-V23-03-13 | 確認 M4 is broken down into M4.x 実行 sequence. | 仕様は次を定義する M4.1 through M4.6 with exit criteria. |
| TC-V23-03-14 | 確認 M4 detailed 実行 spec is linked. | 仕様は次を参照する 09 as detailed 実行 document. |
| TC-V23-03-15 | 確認 M5 is broken down into M5.x 実行 sequence. | 仕様は次を定義する M5.1 through M5.6 with exit criteria. |
| TC-V23-03-16 | 確認 M5 detailed 実行 spec is linked. | 仕様は次を参照する 10 as detailed 実行 document. |
| TC-V23-03-17 | 確認 M6 is broken down into M6.x 実行 sequence. | 仕様は次を定義する M6.1 through M6.6 with exit criteria. |
| TC-V23-03-18 | 確認 M6 detailed 実行 spec is linked. | 仕様は次を参照する 11 as detailed 実行 document. |







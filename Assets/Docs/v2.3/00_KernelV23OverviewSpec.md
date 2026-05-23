# Kernel v2.3 概要仕様

## 文書状態

- 文書 ID: 00_KernelV23OverviewSpec
- 状態: 下書き
- 役割: defines the v2.3 architectural correction that removes スコープ-local DI 実行権限 and restores Kernel-centered 実行
- 依存先:
  - [../v2/06_ServiceGraphRuntimeSpec.md](../v2/06_ServiceGraphRuntimeSpec.md)
  - [../v2/07_ScopeGraphRuntimeSpec.md](../v2/07_ScopeGraphRuntimeSpec.md)
  - [../v2/12_UnityAuthoringBridgeSpec.md](../v2/12_UnityAuthoringBridgeSpec.md)
  - [../v2.2/00_KernelV22CompletionOverviewSpec.md](../v2.2/00_KernelV22CompletionOverviewSpec.md)
- 基盤提供先:
  - [01_KernelV23ServiceRuntimeModelSpec.md](01_KernelV23ServiceRuntimeModelSpec.md)
  - [02_KernelV23AuthoringRegistrationFlowSpec.md](02_KernelV23AuthoringRegistrationFlowSpec.md)
  - [03_KernelV23MilestoneOrderSpec.md](03_KernelV23MilestoneOrderSpec.md)

## 目的

v2.3 is an architectural correction release.

v2.3 completion target is a 完全移行 target, not a partial coexistence target.

The target is to align 実行時 ownership with the intended model:

- 実行権限 is centralized in Scene Kernel
- MBs are authoring 宣言, not 実行時 authorities
- サービス 実行 model is restricted to exactly two forms

v2.3 rejects the following as accepted 実行時 ownership:

- per-スコープ local DI container build
- スコープ-owned 実行時 resolver authority
- 実行時 feature discovery via arbitrary local installer projection

v2.3 requires the following completion guarantees:

- スコープ-local DI 実行権限 is removed 100% from 許可実行経路
- all existing サービス families are re-authored into the new 宣言 model
- サービス names remain stable and external references remain unbroken during 移行

## 中核宣言

```text
実行時 authority belongs to Kernel.
MBs declare; Kernel executes.
サービス form is limited to two kinds only.
```

```text
Scopes do not own DI containers.
Scopes submit 宣言 and receive kernel-issued lifecycle/build commands.
```

```text
完了 移行 is mandatory.
Legacy authority residue is not an accepted release state.
```

## 2つのサービス形態（規範）

Only these two サービス forms are allowed in v2.3:

1. AoS サービス
- one サービス owns all 実行時 instances in structure-of-arrays style by ScopeHandle
- per-スコープ 実行時 state is stored in サービス-managed slots
- no per-スコープ サービス object allocation is 必須 by default

2. 範囲-ServiceInstance サービス
- one サービスファミリー where Kernel owns explicit per-スコープ instances
- instance lifecycle is driven by verified plans and kernel commands
- ownership remains kernel-side, not スコープ-side

No third form is accepted.
In particular, per-スコープ DI-container-as-authority is 禁止.

## 範囲 and MB Ownership 方針

- 範囲 unit remains a 実行時 identity/lifecycle boundary entity
- MB role is 宣言-only (authoring input, optional editor validation)
- MB 必須である not become 実行権限 through autonomous container build
- ScopeHost behavior でなければならない reduced to registration endpoint and kernel-command receiver

## 互換方針

v2.3 may keep temporary 互換 shells only to preserve serialized scene/prefab bindings.
互換 shells 必須である not retain 実行権限 semantics.

互換 方針 additionally requires:

- サービス type names and integration-facing identifiers remain stable during rebuild
- scene/prefab/script references 必須である stay valid throughout 移行
- 互換 shells でなければならない removable after 移行 completion ゲートs pass

## 非目標

This specification does not define:

- gameplay content tuning
- UI/scene art authoring details
- command payload semantics redefinition
- value key identity remapping

## テストケース

| テストケース | 目的 | 実行注記 |
| --- | --- | --- |
| TC-V23-00-01 | 確認 v2.3 defines kernel-central 実行権限. | この文書は次を state 実行権限 belongs to Kernel. |
| TC-V23-00-02 | 確認 MBs are 宣言-only in v2.3. | この文書は次を prohibit MB-owned 実行権限. |
| TC-V23-00-03 | 確認 exactly two サービス forms are normative. | この文書は次を define only AoS and 範囲-ServiceInstance サービス forms. |
| TC-V23-00-04 | 確認 スコープ-local DI ownership is rejected. | この文書は次を explicitly 拒否 per-スコープ DI container authority. |
| TC-V23-00-05 | 確認 完全移行 requirement is explicit. | この文書は次を require 100% removal of スコープ-local DI 実行権限 from 許可経路. |
| TC-V23-00-06 | 確認 サービス name/reference continuity is explicit. | この文書は次を require name-stable and reference-safe 移行 for all services. |






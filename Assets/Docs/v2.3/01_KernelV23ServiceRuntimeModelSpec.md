# Kernel v2.3 サービス実行モデル仕様

## 文書状態

- 文書 ID: 01_KernelV23ServiceRuntimeModelSpec
- 状態: 下書き
- 役割: v2.3 のサービス実行所有と実行モデルを定義する
- 依存先:
  - [00_KernelV23OverviewSpec.md](00_KernelV23OverviewSpec.md)
  - [../v2/06_ServiceGraphRuntimeSpec.md](../v2/06_ServiceGraphRuntimeSpec.md)
  - [../v2/07_ScopeGraphRuntimeSpec.md](../v2/07_ScopeGraphRuntimeSpec.md)

## 所掌

本仕様の所掌:

- 実行時 サービス form 分類
- kernel-side ownership rule for サービス state and instances
- rejection rules for スコープ-local DI サービス authority
- 実行時 dispatch shape between ScopeGraph and サービス 実行時

本仕様の非所掌:

- command catalog internals
- value schema internals
- Unity authoring schema details

## 規範実行モデル

### サービス Form A: AoS サービス

定義:
- サービス holds 実行時 data by ScopeHandle in AoS slots
- サービス methods process slots in batches or indexed operations
- スコープ does not own サービス object instance

必須特性:
- slot creation/destruction is kernel-command driven
- slot access is handle-indexed and generation-safe
- slot lifetime is bound to スコープ lifetime plan

### サービス Form B: 範囲-ServiceInstance サービス

定義:
- kernel owns one 実行時 instance per スコープ where declared
- instance creation/destruction is kernel-command driven
- スコープ may reference サービス capability but does not own the instance container

必須特性:
- instance registry is kernel-side
- 実行時 ownership remains outside MB and outside スコープ-local DI container
- diagnostics include スコープ handle and 宣言 source

## 禁止実行モデル

許可実行経路で次を禁止する:

- per-スコープ local DI container as サービス 実行権限
- per-スコープ autonomous サービス construction based on local component scan
- 実行時 フォールバック creation of undeclared サービス instances

## サービス再構成契約（規範）

既存の全サービスファミリーは次の制約で v2.3 実行モデルへ移行する。

- keep サービス names stable at integration boundary
- keep serialized/script reference continuity during 移行
- replace internal 実行 ownership with kernel-owned AoS or kernel-owned 範囲-ServiceInstance form
- remove スコープ-local DI ownership semantics from the migrated サービス implementation

No サービスファミリー is exempt from 移行.
Partial 移行 that leaves accepted 実行権限 in 旧系 スコープ-local DI is invalid.

## 実行API 方向（概念）

Conceptual authority flow:

1. ScopeGraph signals スコープ lifecycle transitions to Kernel 実行時
2. Kernel 実行時 issues サービス registration/build/activate/release commands
3. サービス 実行時 applies commands to AoS slots or instance registry
4. Diagnostics/DebugMap レコード 宣言 source and 実行 provenance

## 性能方針

- AoS services are default for high-cardinality スコープ domains (entity-like domains)
- 範囲-ServiceInstance is allowed where encapsulation/stateful orchestration is 必須
- サービス count growth 必須である not track entity count unless explicitly justified by form B
- 移行 必須である not reintroduce per-スコープ container build cost through 互換 layers

## テストケース

| テストケース | 目的 | 実行注記 |
| --- | --- | --- |
| TC-V23-01-01 | 確認 AoS サービス form is defined as kernel-owned slot model. | 仕様は次を定義する slot ownership and lifecycle commands. |
| TC-V23-01-02 | 確認 範囲-ServiceInstance form is kernel-owned. | 仕様は次を定義する kernel-side instance registry ownership. |
| TC-V23-01-03 | 確認 third サービス form is disallowed. | Spec を明示的に prohibit per-スコープ local DI authority. |
| TC-V23-01-04 | 確認 フォールバック 実行時 construction is rejected. | 仕様は次を禁止する undeclared 実行時 instance creation. |
| TC-V23-01-05 | 確認 all サービス families are covered by 移行 contract. | 仕様は次を必須とする non-exempt 移行 for all existing services. |
| TC-V23-01-06 | 確認 name/reference continuity requirement is explicit. | 仕様は次を必須とする stable names and reference-safe 移行. |







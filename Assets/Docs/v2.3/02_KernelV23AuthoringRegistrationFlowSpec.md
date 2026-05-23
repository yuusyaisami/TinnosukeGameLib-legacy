# Kernel v2.3 Authoring 登録フロー仕様

## 文書状態

- 文書 ID: 02_KernelV23AuthoringRegistrationFlowSpec
- 状態: 下書き
- 役割: v2.3 における editor compile から 実行時 kernel 登録/実行までの MB 宣言フローを定義する
- 依存先:
  - [00_KernelV23OverviewSpec.md](00_KernelV23OverviewSpec.md)
  - [01_KernelV23ServiceRuntimeModelSpec.md](01_KernelV23ServiceRuntimeModelSpec.md)
  - [../v2/03_VerifiedPlanGenerationSpec.md](../v2/03_VerifiedPlanGenerationSpec.md)
  - [../v2/12_UnityAuthoringBridgeSpec.md](../v2/12_UnityAuthoringBridgeSpec.md)

## 目的

本仕様は MB 宣言専用方針を実行可能な運用へ固定する。

本仕様は、スコープ に付随する authoring を MB や スコープ-local DI container に実行権限を与えずに、コンパイルおよび登録する手順を定義する。

## フロー

### Phase 1: Editor Compile/Registration Plan

- editor collects スコープ 宣言 and attached authoring 宣言
- 宣言 are normalized into verified registration plans
- initial scene scopes are compiled as scene-initial スコープ set
- each 宣言 レコード includes:
  - ScopePlanId
  - ScopeKind
  - ServiceForm target (AoS or 範囲-ServiceInstance)
  - 宣言 payload hash
  - source location for diagnostics

### Phase 2: 実行時 Scene Load

- scene kernel loads verified registration plans for initial scene scopes
- kernel creates 実行時 スコープ identities from verified plans
- kernel emits build/registration commands in verified order

### Phase 3: 範囲 Declaration Submit

- スコープ host provides 宣言 endpoint only
- スコープ sends 宣言 references to kernel 実行時
- kernel validates 宣言 互換 with verified plan

### Phase 4: Kernel Registration Execute

- kernel maps 宣言 into one of two サービス forms
- kernel updates AoS slot サービス or instance registry
- kernel レコード diagnostics and debug provenance

### Phase 5: Lifecycle Commands

- kernel sends activate/deactivate/release commands based on lifecycle plan
- services apply lifecycle using kernel-owned state

## MB 責務規則

MB is limited to:

- 宣言 data surface
- optional editor-time validation
- optional debug authoring helpers

MB 必須である not:

- build 実行時 container
- own 実行時 resolver authority
- instantiate 実行時 サービス authority autonomously

For migrated 旧系 services, MB rename/removal that breaks existing references is 禁止 in accepted 移行 path.
Internal structure may change completely as long as external name/reference continuity contract is preserved.

## 範囲 Host Responsibility Rules

範囲 host is limited to:

- identity endpoint
- 宣言 submission endpoint
- kernel command receiver

範囲 host 必須である not:

- own local DI container authority
- perform 実行時 installer discovery as 許可経路

## 旧系から新系への Authoring 切替規則

- all existing サービス MB families でなければならない cut over to 宣言-only authoring
- 宣言 payload 必須である map to ServiceForm (AoS or 範囲-ServiceInstance) deterministically
- 実行時 実行 authority 必須である move to kernel 実行時 command handlers
- 移行 必須である preserve サービス naming and scene/prefab/script references
- 許可実行経路 必須である not contain スコープ-local DI authority residue

## テストケース

| テストケース | 目的 | 実行注記 |
| --- | --- | --- |
| TC-V23-02-01 | 確認 editor compile stage produces 宣言 plans for initial scene scopes. | 仕様は次を必須とする scene-initial スコープ compile registration. |
| TC-V23-02-02 | 確認 実行時 registration is kernel-driven. | 仕様は次を必須とする kernel-issued build/registration commands. |
| TC-V23-02-03 | 確認 MB 宣言-only boundary is explicit. | 仕様は次を禁止する MB-owned 実行時 container authority. |
| TC-V23-02-04 | 確認 スコープ host 宣言 endpoint model is explicit. | Spec 必須である limit スコープ host to submit/receive responsibilities. |
| TC-V23-02-05 | 確認 旧系 サービス authoring cutover rule is explicit. | 仕様は次を必須とする all existing サービス MB families to move to 宣言-only model. |
| TC-V23-02-06 | 確認 reference-safe 移行 rule is explicit. | 仕様は次を必須とする no reference break during サービス internal rebuild. |







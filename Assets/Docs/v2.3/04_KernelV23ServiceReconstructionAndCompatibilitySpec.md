# Kernel v2.3 サービス再構成および互換性仕様

## 文書状態

- 文書 ID: 04_KernelV23ServiceReconstructionAndCompatibilitySpec
- 状態: 下書き
- 役割: v2.3 移行中の名前安定性と参照継続性を保った全サービス再構成の必須方針を定義する
- 依存先:
  - [00_KernelV23OverviewSpec.md](00_KernelV23OverviewSpec.md)
  - [01_KernelV23ServiceRuntimeModelSpec.md](01_KernelV23ServiceRuntimeModelSpec.md)
  - [02_KernelV23AuthoringRegistrationFlowSpec.md](02_KernelV23AuthoringRegistrationFlowSpec.md)
  - [03_KernelV23MilestoneOrderSpec.md](03_KernelV23MilestoneOrderSpec.md)

## 目的

本仕様は次の2要件を必須化する。

1. 完全移行 to v2.3 実行時 ownership model
2. 完了 サービス-family internal rebuild without external name/reference break

## 完全移行要件（規範）

The 許可実行経路 必須である satisfy all of the following:

- スコープ-local DI 実行権限 residue is zero
- all サービス families run under kernel-owned AoS or kernel-owned 範囲-ServiceInstance form
- MBs are 宣言-only in 許可実行経路

Any remaining 許可経路 実行時 dependency on スコープ-local container build invalidates v2.3 completion.

## サービス再構成契約（規範）

All existing サービス families でなければならない reconstructed internally to new authoring/実行時 model.

Allowed:

- full internal refactor
- data layout redesign
- command/実行時 ownership rewrite
- replacing builder/injector internals

必須 constraints:

- keep サービス name identity stable at integration boundary
- keep scene/prefab/script references intact
- keep 移行-time 互換 bridges strictly non-authoritative

Disallowed:

- rename/delete 移行 that breaks serialized or script references without 承認済み bridge
- partial 移行 that leaves 許可経路 authority in 旧系 local DI container

## 移行在庫および所有計画

Each サービスファミリー 必須である have an 在庫 レコード containing at least:

- ServiceFamilyName
- CurrentAuthorityPath
- TargetServiceForm (AoS or 範囲-ServiceInstance)
- AuthoringDeclarationSurface
- KernelCommandHandlers
- CompatibilityBridgeNeeded (yes/no)
- NameContinuityRisk
- ReferenceContinuityRisk
- PlannedDeletePoint

No サービスファミリー may skip 在庫.

## 検証ゲート

v2.3 completion ゲートs 必須である include:

- authority residue ゲート: zero 許可経路 スコープ-local DI authority
- サービス 在庫 ゲート: all サービス families mapped to target form
- 互換 ゲート: no name/reference break across 移行 milestones
- delete ゲート: temporary 互換 bridges removed when obsolete

## テストケース

| テストケース | 目的 | 実行注記 |
| --- | --- | --- |
| TC-V23-04-01 | 確認 完全移行 is explicit and mandatory. | 仕様は次を必須とする zero 許可経路 local DI authority residue. |
| TC-V23-04-02 | 確認 all サービス families are mandatory 移行 targets. | 仕様は次を禁止する exempt サービス families. |
| TC-V23-04-03 | 確認 internal rebuild with stable names/references is explicit. | 仕様は次を必須とする name identity and reference continuity contract. |
| TC-V23-04-04 | 確認 在庫 schema is defined. | Spec 必須である list mandatory per-サービス 移行 レコード 項目. |
| TC-V23-04-05 | 確認 completion ゲートs include authority, 在庫, 互換, and delete checks. | 仕様は次を定義する all four ゲート classes. |






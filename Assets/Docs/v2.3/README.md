# GameLib Kernel v2.3 Docs

このフォルダには、v2.2 までに残った scope-local DI 妥協を解消し、Kernel 集約モデルへ再統合する v2.3 仕様を置きます。

v2.3 の中心方針:

- MB は原則 Authoring surface のみ
- Runtime 実行主体は Scene Kernel
- Service 形態は次の 2 種だけ
  - AoS Service: 1サービスが scope ごとの runtime data slot を持つ
  - Scope-ServiceInstance Service: Kernel が scope ごとの instance を所有する
- scope ごとの local DI container build を禁止
- scope は Authoring declaration を Kernel に登録要求するだけ
- 旧アーキテクチャである scope-local DI runtime authority は accepted path から 100% 削除する
- すべての既存サービスは「名前維持・参照非破壊」を守ったまま新Authoring構造へ内部再構築する
- 完了条件は「完全移行」であり、互換 shell は serialization continuity のみを許可し runtime authority は禁止する

- [00 Kernel v2.3 Overview Specification](00_KernelV23OverviewSpec.md)
- [01 Kernel v2.3 Service Runtime Model Specification](01_KernelV23ServiceRuntimeModelSpec.md)
- [02 Kernel v2.3 Authoring Registration Flow Specification](02_KernelV23AuthoringRegistrationFlowSpec.md)
- [03 Kernel v2.3 Milestone Order Specification](03_KernelV23MilestoneOrderSpec.md)
- [04 Kernel v2.3 Service Reconstruction and Compatibility Specification](04_KernelV23ServiceReconstructionAndCompatibilitySpec.md)

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V23-README-01 | Confirm v2.3 defines exactly two service forms. | This file must name AoS Service and Scope-ServiceInstance Service only. |
| TC-V23-README-02 | Confirm MBs are declaration-only in v2.3. | This file must explicitly prohibit MB-owned runtime authority. |
| TC-V23-README-03 | Confirm scope-local DI container removal is part of v2.3. | This file must explicitly reject per-scope local DI build. |
| TC-V23-README-04 | Confirm complete migration is required. | This file must require 100% deletion of scope-local DI authority from accepted path. |
| TC-V23-README-05 | Confirm service reconstruction keeps names and references. | This file must require name-stable and reference-safe service rebuild. |

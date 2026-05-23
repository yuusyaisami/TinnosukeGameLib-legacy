# GameLib Kernel v2.3 Docs

このフォルダには、v2.2 までに残った スコープ-local DI 妥協を解消し、Kernel 集約モデルへ再統合する v2.3 仕様を置きます。

v2.3 の中心方針:

- MB は原則 Authoring surface のみ
- 実行時 実行主体は Scene Kernel
- サービス 形態は次の 2 種だけ
  - AoS サービス: 1サービスが スコープ ごとの 実行時 data slot を持つ
  - 範囲-ServiceInstance サービス: Kernel が スコープ ごとの instance を所有する
- スコープ ごとの local DI container build を禁止
- スコープ は Authoring 宣言 を Kernel に登録要求するだけ
- 旧アーキテクチャである スコープ-local DI 実行権限 は 許可経路 から 100% 削除する
- すべての既存サービスは「名前維持・参照非破壊」を守ったまま新Authoring構造へ内部再構築する
- 完了条件は「完全移行」であり、互換 shell は serialization continuity のみを許可し 実行権限 は禁止する

- [00 Kernel v2.3 概要仕様](00_KernelV23OverviewSpec.md)
- [01 Kernel v2.3 サービス実行モデル仕様](01_KernelV23ServiceRuntimeModelSpec.md)
- [02 Kernel v2.3 Authoring 登録フロー仕様](02_KernelV23AuthoringRegistrationFlowSpec.md)
- [03 Kernel v2.3 マイルストーン順序仕様](03_KernelV23MilestoneOrderSpec.md)
- [04 Kernel v2.3 サービス再構成および互換性仕様](04_KernelV23ServiceReconstructionAndCompatibilitySpec.md)
- [05 Kernel v2.3 M0 完全移行契約凍結 実行仕様](05_KernelV23M0FullMigrationContractFreezeExecutionSpec.md)
- [06 Kernel v2.3 M1 仕様ロックおよび台帳化 実行仕様](06_KernelV23M1SpecLockAndCensusExecutionSpec.md)
- [07 Kernel v2.3 M2 Kernel Command 面 実行仕様](07_KernelV23M2KernelCommandSurfaceExecutionSpec.md)
- [08 Kernel v2.3 M3 葉スコープ降格 実行仕様](08_KernelV23M3LeafScopeDemotionExecutionSpec.md)
- [09 Kernel v2.3 M4 ルートシーン統合切替 実行仕様](09_KernelV23M4RootSceneIntegrationCutoverExecutionSpec.md)
- [10 Kernel v2.3 M5 ハードニングおよび削除 実行仕様](10_KernelV23M5HardeningAndDeleteExecutionSpec.md)
- [11 Kernel v2.3 M6 完全証明およびリリース判定 実行仕様](11_KernelV23M6FullProofAndReleaseClaimExecutionSpec.md)
- [運用成果物テンプレート（M1-M6）](Templates/README.md)

## テストケース

| テストケース | 目的 | 実行注記 |
| --- | --- | --- |
| TC-V23-README-01 | 確認 v2.3 defines exactly two サービス forms. | この文書は次を name AoS サービス and 範囲-ServiceInstance サービス only. |
| TC-V23-README-02 | 確認 MBs are 宣言-only in v2.3. | この文書は次を explicitly prohibit MB-owned 実行権限. |
| TC-V23-README-03 | 確認 スコープ-local DI container removal is part of v2.3. | この文書は次を explicitly 拒否 per-スコープ local DI build. |
| TC-V23-README-04 | 確認 完了 移行 is 必須. | この文書は次を require 100% deletion of スコープ-local DI authority from 許可経路. |
| TC-V23-README-05 | 確認 サービス reconstruction keeps names and references. | この文書は次を require name-stable and reference-safe サービス rebuild. |





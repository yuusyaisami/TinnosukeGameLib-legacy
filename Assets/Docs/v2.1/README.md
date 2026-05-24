# GameLib Kernel v2.1 Docs

このフォルダには、v2 の target-kernel 仕様を前提とした、現行ゲームの live migration 仕様を置きます。

v2 と v2.1 の役割差は明確に分ける。

- v2: target kernel の意味論、trust boundary、runtime subsystem の正規仕様
- v2.1: M15 相当の基盤を前提として、現行ゲームを旧アーキテクチャから新アーキテクチャへ段階移行するための実行仕様

v2.1 は second kernel ではない。
v2 の意味論を再定義せず、live game migration の entry condition、preservation floor、destructive allowance、migration wave、acceptance を定義する。

- [00 Kernel v2.1 Migration Overview Specification](00_KernelV21MigrationOverviewSpec.md)
  - v2.1 の役割、`SceneKernel`、entity-scoped `ServiceGraph`、UI 方針、AoS への移行波を定義する。
- [01 Legacy System Replacement Specification](01_LegacySystemReplacementSpec.md)
  - `RuntimeLTS`、`IScopeNode`、`IRuntimeResolver`、`IFeatureInstaller`、`LTSIdentityMB` をどう target path から外すかを定義する。
- [02 Concrete Migration Architecture Specification](02_ConcreteMigrationArchitectureSpec.md)
  - `SceneKernel`、`EntityIdentityMB`、declaration MB、`ServiceGraph`、`Lifecycle`、`ValueStore`、`Scalar`、`CommandCatalog`、`RuntimeQuery`、UI subsystem の具体 runtime 像を定義する。
- [03 Legacy Removal Examples Specification](03_LegacyRemovalExamplesSpec.md)
  - 実コードのアンカーを使って、legacy 接続点をどのように切断し、どの owner に載せ替えるかを cookbook として定義する。
- [04 VarStore / Command / Scalar Treatment Specification](04_VarStoreCommandScalarTreatmentSpec.md)
  - `VarStore`、`CommandRunner`、`Scalar` をどこまで再利用し、`Blackboard` や legacy bootstrap をどこで切るかを定義する。
- [05 Implementation Milestone Specification](05_ImplementationMilestoneSpec.md)
  - foundation、legacy teardown、value/command/scalar、UI、feature port、legacy purge の実装順序と出口条件を定義する。
- [06 Kernel Layer Composition Specification](06_KernelLayerCompositionSpec.md)
  - `ApplicationKernel` と `SceneKernel` の 2 層構成を定義し、V2 の kernel 部品をどこにまとめるかを決める。

## Principles

- gameplay logic surface は守るが、architecture wiring は置換する
- Command field shape、DynamicValue authoring surface、ValueStore generated key identity は preservation floor とする
- direct-play side path の成功だけでは移行完了とみなさない
- 最終目標は、現在動いているゲーム本体が verified kernel path で起動・進行・終了すること

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V21-README-01 | Confirm v2.1 is explicitly separated from v2 target-kernel semantics. | This file must describe the role split between v2 and v2.1. |
| TC-V21-README-02 | Confirm the overview spec is exposed as the first v2.1 root document. | This file must link to 00_KernelV21MigrationOverviewSpec.md. |
| TC-V21-README-03 | Confirm preservation floor is stated at the index level. | This file must mention Command fields, DynamicValue surface, and ValueStore generated keys. |
| TC-V21-README-04 | Confirm the legacy dismantling and concrete runtime specs are exposed at the index level. | This file must link to 01_LegacySystemReplacementSpec.md, 02_ConcreteMigrationArchitectureSpec.md, and 03_LegacyRemovalExamplesSpec.md. |
| TC-V21-README-05 | Confirm 02 and 03 are described as runtime architecture and removal cookbook, not loose notes. | This file must explain the role of 02 and 03 in one-line summaries. |
| TC-V21-README-06 | Confirm subsystem treatment for VarStore/Command/Scalar is exposed at the index level. | This file must link to 04_VarStoreCommandScalarTreatmentSpec.md and summarize its role. |
| TC-V21-README-07 | Confirm the v2.1 implementation order spec is exposed at the index level. | This file must link to 05_ImplementationMilestoneSpec.md and summarize milestone ordering. |
| TC-V21-README-08 | Confirm kernel layer composition is exposed at the index level. | This file must link to 06_KernelLayerCompositionSpec.md and summarize ApplicationKernel / SceneKernel. |

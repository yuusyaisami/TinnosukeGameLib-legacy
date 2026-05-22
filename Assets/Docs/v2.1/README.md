# GameLib Kernel v2.1 Docs

このフォルダには、v2 の target-kernel 仕様を前提とした、現行ゲームの live migration 仕様を置きます。

v2 と v2.1 の役割差は明確に分ける。

- v2: target kernel の意味論、trust boundary、runtime subsystem の正規仕様
- v2.1: M15 相当の基盤を前提として、現行ゲームを旧アーキテクチャから新アーキテクチャへ段階移行するための実行仕様

v2.1 は second kernel ではない。
v2 の意味論を再定義せず、live game migration の entry condition、preservation floor、destructive allowance、migration wave、acceptance を定義する。

- [00 Kernel v2.1 Migration Overview Specification](00_KernelV21MigrationOverviewSpec.md)
- [01 Wave A Boot and Scene Entry Cutover Specification](01_WaveABootAndSceneEntryCutoverSpec.md)
- [02 Wave B Scope and Service Composition Cutover Specification](02_WaveBScopeAndServiceCompositionCutoverSpec.md)
- [03 Wave C Command Dispatch Cutover Specification](03_WaveCCommandDispatchCutoverSpec.md)
- [04 Wave D Value, Blackboard, and Var Cutover Specification](04_WaveDValueBlackboardAndVarCutoverSpec.md)
- [05 Wave E Representative Gameplay Systems Cutover Specification](05_WaveERepresentativeGameplaySystemsCutoverSpec.md)
- [06 Wave F Legacy Removal and Hardening Specification](06_WaveFLegacyRemovalAndHardeningSpec.md)
- [07 Kernel v2.1 Migration Milestone Order Specification](07_KernelV21MigrationMilestoneOrderSpec.md)
- [Index / V21-M0 Baseline Freeze Package](Index/README.md)

## Principles

- gameplay logic surface は守るが、architecture wiring は置換する
- Command field shape、DynamicValue authoring surface、ValueStore generated key identity は preservation floor とする
- Wave A は live boot authority、persistent root ownership、scene entry、loading orchestration の切り替えを最初の詳細 wave として扱う
- Wave B は installer-driven composition authority を verified ScopeGraph と scope-local ServiceGraph authority へ切り替える
- Wave C は command registration と dispatch truth を bulk executor registration と runtime key fallback から verified CommandCatalog authority へ切り替える
- Wave D は generic value truth と blackboard ownership と DynamicValue runtime authority を verified ValueStore and DynamicEvaluation authority へ切り替える
- Wave E は representative gameplay systems が migrated authority を実際に consume し、gameplay success only を completion proof に使えないことを明文化する
- Wave F は migration-only residue を deletion または audited quarantine に詰め、Release or direct-play or CI acceptance を executable gates で harden する
- 07 は overview と Wave A-F を V21-M0 から V21-M6 の claimable milestone に束ね、wave ownership と completion claim order を分離する
- V21-M0 は v2 の M0成果物を複製せず、v2.1 固有の baseline ledger、preservation floor ledger、proof-anchor catalog を Index package として固定する
- direct-play side path の成功だけでは移行完了とみなさない
- 最終目標は、現在動いているゲーム本体が verified kernel path で起動・進行・終了すること

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V21-README-01 | Confirm v2.1 is explicitly separated from v2 target-kernel semantics. | This file must describe the role split between v2 and v2.1. |
| TC-V21-README-02 | Confirm the overview spec is exposed as the first v2.1 root document. | This file must link to 00_KernelV21MigrationOverviewSpec.md. |
| TC-V21-README-03 | Confirm preservation floor is stated at the index level. | This file must mention Command fields, DynamicValue surface, and ValueStore generated keys. |
| TC-V21-README-04 | Confirm the first detailed migration wave is exposed. | This file must link to 01_WaveABootAndSceneEntryCutoverSpec.md. |
| TC-V21-README-05 | Confirm the second detailed migration wave is exposed. | This file must link to 02_WaveBScopeAndServiceCompositionCutoverSpec.md. |
| TC-V21-README-06 | Confirm the third detailed migration wave is exposed. | This file must link to 03_WaveCCommandDispatchCutoverSpec.md. |
| TC-V21-README-07 | Confirm the fourth detailed migration wave is exposed. | This file must link to 04_WaveDValueBlackboardAndVarCutoverSpec.md. |
| TC-V21-README-08 | Confirm the fifth detailed migration wave is exposed. | This file must link to 05_WaveERepresentativeGameplaySystemsCutoverSpec.md. |
| TC-V21-README-09 | Confirm the sixth detailed migration wave is exposed. | This file must link to 06_WaveFLegacyRemovalAndHardeningSpec.md. |
| TC-V21-README-10 | Confirm the milestone-order spec is exposed at the index level. | This file must link to 07_KernelV21MigrationMilestoneOrderSpec.md. |
| TC-V21-README-11 | Confirm the V21-M0 index package is exposed at the index level. | This file must link to Index/README.md. |
| TC-V21-README-12 | Confirm V21-M0 is described as migration-specific evidence rather than a fork of the v2 M0 artifacts. | This file must state that v2.1 adds baseline, preservation, and proof ledgers instead of duplicating the v2 M0 documents. |

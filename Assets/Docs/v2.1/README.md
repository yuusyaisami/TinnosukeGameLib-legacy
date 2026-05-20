# GameLib Kernel v2.1 Docs

このフォルダには、v2 の target-kernel 仕様を前提とした、現行ゲームの live migration 仕様を置きます。

v2 と v2.1 の役割差は明確に分ける。

- v2: target kernel の意味論、trust boundary、runtime subsystem の正規仕様
- v2.1: M15 相当の基盤を前提として、現行ゲームを旧アーキテクチャから新アーキテクチャへ段階移行するための実行仕様

v2.1 は second kernel ではない。
v2 の意味論を再定義せず、live game migration の entry condition、preservation floor、destructive allowance、migration wave、acceptance を定義する。

- [00 Kernel v2.1 Migration Overview Specification](00_KernelV21MigrationOverviewSpec.md)

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

# Kernel v2.1 VarStore / Command / Scalar 取扱い仕様

## 文書ステータス

- 文書 ID: `04_VarStoreCommandScalarTreatmentSpec`
- 状態: Draft
- 役割: v2.1 において `VarStore`、`CommandRunner`、`Scalar` をどこまで再利用し、どこを破棄し、何に置き換えるかを固定する
- 範囲: `VarStore` backend 採用方針、`Blackboard` 廃止方針、`CommandRunner` 再構成方針、`Scalar` subsystem 再構成方針、v2 仕様との整合、性能上の注意
- 非目標: 05 の milestone 順序の確定、最終 API シグネチャの固定、全 command / scalar 実装の即時全面改修

### 改訂メモ

この文書は、v2.1 が旧式 subsystem を「service として登録し直すだけ」で終わることを防ぐために作る。

特に `VarStore`、`CommandRunner`、`Scalar` は、すべて legacy origin だが同列ではない。

- `VarStore` は backend data structure として再利用余地が大きい
- `CommandRunner` は execution engine の一部に再利用余地がある
- `Blackboard` architecture は再利用してはならない
- `BaseScalarService` architecture も再利用してはならない

---

## 所有範囲

この仕様が所有するもの:

- `VarStore` を v2.1 target path でどう位置付けるか
- `BlackboardMB` / `BlackboardService` / `VarIdResolver` / `VarKeyRegistryLocator` の扱い
- `CommandRunnerMB` / `CommandRunner` / command executor bootstrap の扱い
- `BaseScalarService` / scalar binding / scalar identity の扱い
- `ServiceGraph`、`CommandCatalog`、`ValueStore`、`Scalar` subsystem の境界
- temporary bridge を許可する条件
- subsystem ごとの受け入れ基準

この仕様が所有しないもの:

- 05 の milestone 実行順
- individual command executor の個別書き換え手順
- final binary artifact format
- final editor tooling UI

04 は、残すべきものと残してはいけないものの線引きを所有する。

---

## 目的

v2.1 におけるこの仕様の目的は次の通り。

```text
1. VarStore core を backend として残す。
2. Blackboard architecture を target path から排除する。
3. CommandRunner は bootstrap を作り直し、execution engine だけを慎重に流用する。
4. Scalar は subsystem として残すが、BaseScalarService architecture は捨てる。
5. v2 の CommandCatalog / ValueStore / Scalar の責務分離を破らない。
```

中心ルール:

```text
再利用してよいのは domain logic と data structure である。
resolver、installer、hierarchy fallback、runtime stable-key repair は再利用してはならない。
```

---

## v2 仕様との整合

この仕様は次の v2 文書を上位制約として扱う。

| v2 仕様 | v2.1 での意味 |
|---|---|
| [06_ServiceGraphRuntimeSpec.md](../v2/06_ServiceGraphRuntimeSpec.md) | `ServiceGraph` は generic DI container ではなく、command registry、value key resolver、entity component directory でもない |
| [09_CommandCatalogRuntimeSpec.md](../v2/09_CommandCatalogRuntimeSpec.md) | command は `CommandCatalog` の責務であり、`ICommandExecutor` discovery や巨大 installer を target path に残さない |
| [10_ValueSchemaAndStoreSpec.md](../v2/10_ValueSchemaAndStoreSpec.md) | `ValueStore` は generic value storage であり、runtime stable-key fallback を持つ `Blackboard v2` ではない |
| [10_1_ScalarRuntimeAndBindingSpec.md](../v2/10_1_ScalarRuntimeAndBindingSpec.md) | scalar は float-specialized subsystem であり、parent walk、registry binding search、silent zero fallback を許可しない |
| [13_LegacyCompatBoundarySpec.md](../v2/13_LegacyCompatBoundarySpec.md) | temporary bridge は quarantine に閉じ込め、target runtime truth にしない |
| [14_PerformanceBudgetAndRuntimeRulesSpec.md](../v2/14_PerformanceBudgetAndRuntimeRulesSpec.md) | hot path で discovery、reflection、無制限 allocation、repair fallback を残さない |

v2.1 は v2 と完全同一実装である必要はない。
ただし、上記の責務分離を壊してはならない。

---

## 現行コードの観測

この仕様は次の実装観測を前提にする。

- [VarStore.cs](../../GameLib/Script/Common/Variables/VarStore/Core/VarStore.cs)
- [BlackboardService.cs](../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs)
- [BlackboardMB.cs](../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs)
- [VarIdResolver.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs)
- [VarKeyRegistryLocator.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs)
- [CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs)
- [CommandRunner.cs](../../GameLib/Script/Common/Commands/VNext/Core/CommandRunner.cs)
- [BaseScalarService.cs](../../GameLib/Script/Common/Variables/Scalar/Core/BaseScalarService.cs)

重要な観測:

- `VarStore` は data structure としてかなり自立している
- `BlackboardService` は storage ではなく parent fallback resolver の性格が強い
- `VarIdResolver` は runtime stable-key resolve を持つ
- `VarKeyRegistryLocator` は runtime `Resources.Load` fallback を持つ
- `CommandRunnerMB` は bulk executor registration と `LifetimeScopeKind` branch を持つ
- `CommandRunner` は execution engine と bootstrap 前提が混在している
- `BaseScalarService` は `IScopeNode`、ancestor fallback、silent zero behavior、runtime binding search を持つ

---

## VarStore 方針

### 1. 結論

`VarStore` core data structure は v2.1 で再利用してよい。

ただし、再利用対象は `VarStore` 本体とその table / revision / schema-aware storage behavior であり、
`Blackboard` architecture や runtime stable-key resolver ではない。

### 2. Target position

v2.1 では `VarStore` を次のどちらかとして使う。

- `ValueStore` subsystem の backend implementation
- value-oriented shared store service の internal storage

この場合でも external runtime contract は `ValueKeyId` / verified schema / explicit boundary で表す。

### 3. Keep

残してよいもの:

- `int varId` ベースの slot storage
- version / revision
- table / row / cell storage
- optional schema check
- merge や clear の基本挙動

### 4. Remove

target path で残してはならないもの:

- `VarIdResolver` による runtime stable-key resolution
- `VarKeyRegistryLocator` による `Resources.Load`
- missing var を runtime で invent すること
- `BlackboardService` による parent/root fallback write
- installer-based store registration

### 5. Design rule

`VarStore` は backend であって、runtime truth ではない。
runtime truth は `ValueStore` contract 側にある。

つまり:

- `ValueStore` public contract を先に定義する
- 内部実装として `VarStore` を使ってよい
- だが public API を `VarStore` に引きずられてはならない

### 6. Failure rule

次は failure にする:

- stable key がないと読めない値アクセス
- nearest parent blackboard がないと書けない値アクセス
- registry asset fallback がないと boot できない構成

### 7. Performance rule

`VarStore` backend を採用しても、次を hot path に残してはならない。

- stable-key string lookup
- parent walk
- runtime asset lookup
- `BlackboardMB` 由来の暗黙 init

---

## Blackboard 方針

### 1. 結論

`Blackboard` は subsystem として引き継がない。

引き継ぐのは `VarStore` backend であり、
`BlackboardService` の architecture ではない。

### 2. Why

[BlackboardService.cs](../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs) は、主に次を持っている。

- local store
- parent walk
- global fallback
- root write fallback
- nearest owner search

これは `ValueStore` ではなく、legacy hierarchy-aware resolver である。

### 3. Target replacement

置換先:

- entity-local `ValueStore`
- explicit shared/global store boundary
- `ValueInitPlan`
- verified `ValueKeyId`

### 4. Keep

残してよいもの:

- local storage intent
- merge semantics の一部
- inspector payload surface の一部

### 5. Remove

残してはならないもの:

- `IBlackboardService` を target value truth とすること
- `IScopeNode.Parent` による read/write fallback
- root/game-logic-root 自動選択
- blackboard registration installer

### 6. Transitional note

`BlackboardMB` の公開フィールドは preservation floor に応じて declaration MB へ移すことはできる。
しかし `BlackboardMB` をそのまま target runtime installer にしてはならない。

---

## Command 方針

### 1. 結論

`CommandRunnerMB` は削除対象である。

`CommandRunner` execution engine は、一部を暫定流用してよい。
ただし、bootstrap、executor registration、catalog/key resolve、scope-kind branch は全部作り直す。

### 2. v2 alignment

v2 の [09_CommandCatalogRuntimeSpec.md](../v2/09_CommandCatalogRuntimeSpec.md) では、command runtime の authority は `CommandCatalog` にある。

したがって v2.1 でも、次は target path に残してはならない。

- `ICommandExecutor` discovery
- `CommandRunnerMB` による bulk registration
- `LifetimeScopeKind` で分ける runner registration
- `Resources.Load` ベースの catalog fallback
- runtime negative key / stable-key repair

### 3. Allowed transitional shape

phase 1 では、`CommandRunner` を 1 つの coarse-grained service として持ってよい。

ただし、その意味は次である。

- `SceneKernel` / `ServiceGraph` が owner になる
- command dispatch entry は verified `CommandCatalog` 側に置く
- `CommandRunner` は catalog から指示を受けて execute する runtime engine として扱う
- executor list は verified command plan から固定される

### 4. Keep

残してよいもの:

- command execution flow
- `CommandContext` 的な概念
- frame / trace / failure boundary の一部
- 既存 command executor 本体の domain logic

### 5. Remove

残してはならないもの:

- `CommandRunnerMB`
- `IFeatureInstaller`
- `LifetimeScopeKind` 分岐
- DI collection から executor を集める path
- command key fallback
- command catalog locator fallback

### 6. UniTask judgment

`CommandRunner` に `UniTask` が使われていることは、長期的には問題である。

特に次は危険である。

- every-frame 大量生成
- command 数に比例した task churn
- polling / wait / detached execution が allocation を生む path

ただし、全 command を一気に書き換えるコストは高い。
そのため v2.1 phase 1 では、`CommandRunner` を全部捨てずに execution engine として一時利用する判断は許可する。

その代わり必須条件:

- bootstrap は新設計に置き換える
- every-frame command churn を profiler で監視する
- `UniTask` を target architecture truth に昇格させない
- 05 の milestone で command engine replacement wave を別途持つ

### 7. Failure rule

次は failure にする:

- new path が `CommandRunnerMB` に依存する
- command dispatch が `ICommandExecutor` discovery を必要とする
- command 実行前に stable-key fallback が必要になる

---

## Scalar 方針

### 1. 結論

`Scalar` は subsystem として残す。
しかし `BaseScalarService` architecture は target path に残さない。

### 2. v2 alignment

v2 の [10_1_ScalarRuntimeAndBindingSpec.md](../v2/10_1_ScalarRuntimeAndBindingSpec.md) では、scalar は:

- float-specialized runtime
- generic `ValueStore` ではない
- verified scalar identity を持つ
- inherited access は verified endpoint で行う
- registry binding search をしない
- silent zero fallback をしない

したがって v2.1 でも、次は残してはならない。

- `IScopeNode` ancestor fallback
- nearest ancestor scalar cache を truth にすること
- `Animator.StringToHash` ベースの runtime truth
- missing read を silent `0` で通すこと
- runtime binding registry search

### 3. Keep

残してよいもの:

- modulation pipeline の考え方
- additive / multiplicative / clamp / timed contribution の main logic
- telemetry surface の一部
- existing authored scalar profile surface の一部

### 4. Remove

残してはならないもの:

- `BaseScalarService` public contract
- `GlobalGet` / `GlobalTryGet` の ancestor walk semantics
- runtime-created key truth
- scalar installer MB

### 5. Target replacement

置換先:

- scalar declaration MB
- verified scalar identity
- explicit binding endpoint
- lifecycle-driven timed update
- `ValueStore` と分離された scalar subsystem

### 6. Failure rule

次は failure にする:

- scalar read が nearest parent search に依存する
- required scalar read が silent zero で通る
- binding endpoint が registry scan でしか見つからない

### 7. Performance rule

scalar hot path で残してはならないもの:

- parent traversal
- string/hash lookup
- hidden `DynamicValue<float>` evaluation
- allocation-heavy timed update path

---

## Temporary Bridge Rule

次の条件を満たす場合だけ temporary bridge を許可する。

- owner は `SceneKernel` / verified plan 側にある
- legacy subsystem を target truth に昇格させない
- removability が先に定義されている
- diagnostics-visible である
- performance cost を測定できる

bridge が次のいずれかを再侵入させるなら禁止:

- `IRuntimeResolver`
- `IScopeNode.Parent` fallback
- `Resources.Load` fallback
- installer mutation
- runtime stable-key repair

---

## Recommended v2.1 Position

厳しめの推奨結論は次の通り。

### 採用してよい

- `VarStore` core
- `CommandRunner` execution engine の一部
- scalar modulation logic の一部
- existing MB field surface

### 採用してはいけない

- `BlackboardService` architecture
- `CommandRunnerMB`
- `BaseScalarService` architecture
- `VarIdResolver`
- `VarKeyRegistryLocator`
- `IFeatureInstaller`
- `IScopeNode` / `IRuntimeResolver` 依存

### 実務判断

phase 1 の target shape:

- `ValueStore` public subsystem は新設計
- backend に `VarStore` を使う
- command は `CommandCatalog` + `CommandRunnerService` 的な構成にする
- scalar は独立 subsystem として登録する
- `Blackboard` は subsystem として再構築しない

---

## 受け入れ基準

- `VarStore` が backend data structure としてのみ再利用されると明記されている
- `BlackboardService` architecture を再利用しないと明記されている
- `CommandRunnerMB` を target path に残さないと明記されている
- `CommandRunner` engine の暫定流用条件が書かれている
- `BaseScalarService` architecture を target path に残さないと明記されている
- `Scalar` が `ValueStore` と別 subsystem であると明記されている
- runtime stable-key fallback、parent walk、installer mutation を禁止している

---

## テストケース

| テストケース | 目的 | 検証 |
|---|---|---|
| `TC-V21-04-01` | `VarStore` が backend としてだけ扱われることを確認する | `ValueStore` truth と `VarStore` backend が分離されて書かれていなければならない |
| `TC-V21-04-02` | `Blackboard` architecture が target path に残らないことを確認する | `BlackboardService` の parent/root fallback を禁止していなければならない |
| `TC-V21-04-03` | `CommandRunnerMB` が削除対象であることを確認する | `CommandRunnerMB` の bulk registration を残さないと書かれていなければならない |
| `TC-V21-04-04` | `CommandRunner` の暫定流用が strict conditions 付きであることを確認する | bootstrap 作り直し、discovery 禁止、profiling 必須が書かれていなければならない |
| `TC-V21-04-05` | `Scalar` が subsystem として残るが legacy architecture は残らないことを確認する | `BaseScalarService` の ancestor fallback と silent zero を禁止していなければならない |
| `TC-V21-04-06` | forbidden fallback が再侵入しないことを確認する | `IRuntimeResolver`、`Parent` walk、`Resources.Load`、stable-key repair、installer mutation が bridge rule で禁止されていなければならない |

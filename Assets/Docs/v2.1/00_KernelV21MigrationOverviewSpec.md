# Kernel v2.1 移行概要仕様

## 文書ステータス

- 文書 ID: `00_KernelV21MigrationOverviewSpec`
- 状態: Draft
- 役割: 現行ゲームを旧アーキテクチャから v2 target path へ移すための、v2.1 移行アーキテクチャの初期概念を定義する
- 範囲: SceneKernel、Entity(Scope) 単位のサービス所有、AoS への移行方針、既存資産の保全、legacy 隔離、移行波の順序
- 非目標: v2 target kernel の意味論の再定義、別の kernel の新設、runtime discovery の温存、隠れた fallback の拡張

### 改訂メモ

この文書は v2.1 を「第二の kernel」として扱わない。
v2.1 は、v2 target kernel の意味論を前提にした live migration の実行層である。

したがって本書は、既存の Scene / Prefab / Inspector の見た目を守りながら、内部の wiring を置き換えるための仕様として書く。
内部実装の破壊は許容するが、それは必ず保全対象と quarantine 境界を明示した上で行う。

---

## 所有範囲

この仕様が所有するもの:

- v2.1 の移行目的と優先順位
- `SceneKernel` の責務
- `EntityRef`（= `ScopeRef`）/ `AuthoringMB` の役割分担
- サービスを 2 つの形に分類するルール
- 既存資産の preservation floor
- legacy と target path の境界
- 移行波の順序
- 受け入れ基準と検証条件

この仕様が所有しないもの:

- v2 target kernel の意味論そのもの
- `KernelIR`、`ServiceGraph`、`ScopeGraph`、`BootManifest` など v2 の用語定義
- 各サブシステムの最終 API 形状
- 個別 gameplay 機能の設計
- 既存アーキテクチャの修復を暗黙に行うこと

v2.1 は既存ゲームの移行層であり、v2 kernel core の代替ではない。

---

## 目的

v2.1 の目的は、現行ゲームを止めずに、旧アーキテクチャ依存を段階的に外し、検証済みの v2 path へ移すことである。

このときの基本方針は次の通り。

```text
1. 既存の見た目と保存データを急に壊さない。
2. しかし内部の wiring は容赦なく置き換える。
3. runtime discovery と fallback repair は target path から追い出す。
4. サービスの所有は SceneKernel に集約する。
5. 1 つのサービス形で全てを統一しようとしない。
```

v2.1 は、旧 DI 型の利便性を一時的に受け継ぎつつ、最終的には `SceneKernel` 主導の明示的な所有へ移行する。

このため、v2.1 のサービスは次の 2 形に分類する。

1. `Entity(Scope)` ごとに runtime instance を持つサービス
2. `Entity(Scope)` ごとに AoS 形式の runtime data を持ち、data をまとめて処理するサービス

最初の段階では 1 を主軸とし、2 は将来の理想形として仕様化する。

---

## v2 target specs との関係

v2.1 は v2 target kernel を前提とする。
特に次の仕様は、v2.1 の移行方針に対する上位制約として扱う。

| v2 仕様 | v2.1 での意味 |
|---|---|
| [00_KernelArchitectureOverviewSpec.md](../v2/00_KernelArchitectureOverviewSpec.md) | runtime discovery 非依存、validated path 優先、legacy quarantine の根本制約 |
| [01_KernelIRSpec.md](../v2/01_KernelIRSpec.md) | 変換元 / 変換先の正規化データの権威 |
| [02_ModuleContributionSpec.md](../v2/02_ModuleContributionSpec.md) | authoring からの寄与抽出の正規化 |
| [03_VerifiedPlanGenerationSpec.md](../v2/03_VerifiedPlanGenerationSpec.md) | plan-first の verified generation |
| [04_DependencyValidationSpec.md](../v2/04_DependencyValidationSpec.md) | 欠落・循環・重複の明示的拒否 |
| [05_BootManifestAndProfileSpec.md](../v2/05_BootManifestAndProfileSpec.md) | boot の入口と profile による受理条件 |
| [06_ServiceGraphRuntimeSpec.md](../v2/06_ServiceGraphRuntimeSpec.md) | coarse-grained service のみを kernel core に置く制約 |
| [07_ScopeGraphRuntimeSpec.md](../v2/07_ScopeGraphRuntimeSpec.md) | scope authority を transform から切り離す制約 |
| [08_LifecyclePlanSpec.md](../v2/08_LifecyclePlanSpec.md) | lifecycle を plan 駆動にする制約 |
| [09_CommandCatalogRuntimeSpec.md](../v2/09_CommandCatalogRuntimeSpec.md) | command dispatch を table 駆動にする制約 |
| [10_ValueSchemaAndStoreSpec.md](../v2/10_ValueSchemaAndStoreSpec.md) | value / variable / dynamic の再定義 |
| [10_1_ScalarRuntimeAndBindingSpec.md](../v2/10_1_ScalarRuntimeAndBindingSpec.md) | scalar specialisation の境界 |
| [10_2_DynamicValueEvaluationSpec.md](../v2/10_2_DynamicValueEvaluationSpec.md) | dynamic / reactive evaluation の境界 |
| [11_DebugMapAndDiagnosticsSpec.md](../v2/11_DebugMapAndDiagnosticsSpec.md) | traceability と diagnostics の必須性 |
| [12_UnityAuthoringBridgeSpec.md](../v2/12_UnityAuthoringBridgeSpec.md) | authoring trace と direct-play bridge |
| [13_LegacyCompatBoundarySpec.md](../v2/13_LegacyCompatBoundarySpec.md) | legacy を独立境界に閉じ込める制約 |
| [14_PerformanceBudgetAndRuntimeRulesSpec.md](../v2/14_PerformanceBudgetAndRuntimeRulesSpec.md) | instance / data 形の選択を性能で判断する制約 |
| [15_TestAndValidationSpec.md](../v2/15_TestAndValidationSpec.md) | 移行の失敗を見える化する test / validation 制約 |
| [16_ImplementationMilestoneOrderSpec.md](../v2/16_ImplementationMilestoneOrderSpec.md) | v2 移行の順序制御 |
| [17_AssemblyDefinitionAndCompileBoundarySpec.md](../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md) | 旧アーキテクチャと target path の compile boundary |

v2.1 は v2 の一部を再定義するための文書ではない。
v2 の制約を、現行ゲームの移行計画に落とし込むための文書である。

---

## 現在のアーキテクチャ観測

この節は、現行コードベースに見える圧力点を整理したものである。
ここでの観測は推測ではなく、移行仕様に反映すべき実装上の事実である。

| 観測 | 現行アンカー | 問題 | v2.1 での修正方針 |
|---|---|---|---|
| scope build が discovery、registration、acquire/release をまとめて抱えている | [RuntimeLifetimeScopeBase.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs) | runtime authority が暗黙で、責務が過密 | 旧実装は legacy bridge へ隔離し、`SceneKernel` 側は explicit plan からのみ service を組み立てる |
| 子階層の component を走査して feature installer を探している | [ScopeFeatureInstallerUtility.cs](../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs) | `GetComponentsInChildren` と `Transform.parent` 依存が強い | 走査は authoring extraction に限定し、runtime path では使わない |
| command executor の大量登録を scope ごとに行っている | [CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs) | bulk registration が composition の主役になっている | command 系は SceneKernel の明示的 catalog へ移し、旧登録は quarantine adapter とする |
| blackboard / var / dynamic の責務が重なっている | [BlackboardService.cs](../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs) | hierarchical fallback と owner 推定が混ざる | value 系は target path では明示的 boundary に整理し、fallback repair を標準経路にしない |
| runtime registry が `Resources.Load` fallback を持つ | [VarKeyRegistryLocator.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs) | asset 発見を runtime へ持ち込んでいる | migration 中は quarantine でのみ許可し、target path では explicit asset を必須化する |
| loading / singleton 修復が scene discovery に依存する | [LoadingScreenService.cs](../../GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs) | `FindObjectsByType` による修復が入っている | boot / loading は explicit manifest 化し、scene search を標準化しない |
| authoring trace はすでに存在する | [UnityAuthoringBridge.cs](../../GameLib/Script/Kernel/Authoring/UnityAuthoringBridge.cs)、[ScopeAuthoringRoot.cs](../../GameLib/Script/Kernel/Boot/ScopeAuthoringRoot.cs)、[ScopeAuthoringLink.cs](../../GameLib/Script/Kernel/Boot/ScopeAuthoringLink.cs)、[UnityObjectLinkAuthoring.cs](../../GameLib/Script/Kernel/Boot/UnityObjectLinkAuthoring.cs) | 移行で最も活きる既存資産 | これらを authoring の trace / identity の基盤として再利用する |

### 旧アーキテクチャの意味

現行の DI 型 scope は、`Entity(Scope)` ごとに service instance を持つという意味では、v2.1 の type 1 に近い。
しかし、現行実装は以下の理由でそのままは使えない。

- hierarchy / component discovery が暗黙の authority になっている
- `IFeatureInstaller` が registration の主役になっている
- fallback repair が設計の一部になっている
- command / value / loading が registry と discovery に依存している

したがって v2.1 は、既存の「見た目に似たもの」を残しつつ、authority と registration を explicit 化する必要がある。

---

## SceneKernel の責務

`SceneKernel` は scene-local の移行制御点である。
これは v2 target kernel の `ServiceGraph` そのものではなく、現行ゲームを target path に載せるための scene-level orchestration root である。

`SceneKernel` の責務は次の通り。

- scene 内の service を明示的に所有する
- `EntityRef` をキーに service を登録・更新・破棄する
- authoring 由来の plan / metadata を runtime data に変換する
- service instance 型と AoS data 型の両方を扱う
- diagnostics と validation を経由して失敗を閉じる
- legacy adapter を quarantine し、target path へ漏らさない

`SceneKernel` は次のことをしてはならない。

- `GetComponentsInChildren` で runtime service を発見する
- `FindObjectsByType` で service の修復を行う
- `Transform.parent` を authority として扱う
- `Resources.Load` を標準的な registry repair に使う
- per-entity DI scope を無制限に増殖させる

`SceneKernel` は、テーブル駆動の所有者である。
resolver 駆動の探索者であってはならない。

---

## Entity(Scope) / Authoring の役割

v2.1 では `Entity` と `Scope` は同じ意味である。
この文書では、1 つの論理単位を `Entity(Scope)` と表記する。
`Entity` と `Scope` を別の階層や別の所有者として扱ってはならない。

### `EntityRef`

- `EntityRef`: scene-local に安定した Entity(Scope) 識別子

### `ScopeRef`

- `ScopeRef`: `EntityRef` の別名

v2.1 では `ScopeRef` は別概念ではない。
v2 target 文書との語彙合わせのために残すラベルであり、実体は `EntityRef` と同じである。

### `AuthoringMB`

`AuthoringMB` は単一の base class 名を意味しない。
これは role 名であり、実装では次の既存資産を基盤にしてよい。

- `ScopeAuthoringRoot`
- `ScopeAuthoringLink`
- `UnityObjectLinkAuthoring`
- feature 固有の declaration / bridge component

`AuthoringMB` の役割は、Entity(Scope) の境界、source trace、追加 metadata を明示することである。
物理的に root に付いているか子に付いているかは、authoring の都合であって authority ではない。

---

## サービスの 2 形

v2.1 では、サービスは次の 2 形に分ける。

| 形 | 意味 | `SceneKernel` の持ち方 | 向いているもの | 現段階の扱い |
|---|---|---|---|---|
| 1. Entity(Scope) ごとの runtime instance | 1 Entity(Scope) に 1 service instance を持つ | `EntityRef -> service instance` の table | current prefab / inspector surface を守りたいもの | phase 1 の主軸 |
| 2. Entity(Scope) ごとの AoS runtime data | 1 service manager が Entity(Scope) record 群を保持し、まとめて処理する | `service manager -> record array -> processing loop` | 高密度・高頻度・高スケールのもの | v2.1 では model として定義し、実装は段階導入 |

### 形 1

形 1 は、既存の旧アーキテクチャに最も近い。

- 現行の public serialized field を保ちやすい
- scene / prefab の保存形を壊しにくい
- `AnimationSpriteHubService` のような service class をそのまま移しやすい
- 既存資産の保全に向いている

ただし、形 1 は「DI をそのまま残す」ことではない。
`SceneKernel` が所有し、explicit registration で組み立てる。

### 形 2

形 2 は、将来的な理想形である。

- runtime instance の数を減らせる
- 1 つの processing loop で大量の entity を処理できる
- 高密度の channel / animation / transform 系と相性がよい

ただし、v2.1 の初期実装では AoS はモデルであって義務ではない。
phase 1 で無理に実装しない。

v2.1 はまず 1 を全体に適用し、その後に 2 へ移る。

---

## 代表的なサービス例

### `AnimationSpriteHub` 系

ユーザーが例として挙げた `SpriteAnimationChannelHub` 系は、現行コードでは `AnimationSpriteHubMB` / `AnimationSpriteHubService` が代表例に近い。

この種のサービスは phase 1 では次の方針で扱う。

- public serialized field は保つ
- service 生成の見た目はできるだけ変えない
- ただし registration の主体は `IFeatureInstaller` ではなく `SceneKernel` に移す
- owner の識別は `EntityRef` で行う
- scope / scene の所属は explicit に保持する

この型は、v2.1 の形 1 を最初に実装する代表候補である。

### `Tooltip`、`ModalStack`、`Mesh`、`Direction`、`Transform` 系

これらは、同じく entity-scope 由来の service として扱える。

- 1 entity につき 1 instance を持つ型
- 1 scene に 1 manager を持つ型
- 高密度になってから AoS に移す型

を分けて扱う。

### `Command`、`Value`、`Loading`、`Boot` 系

これらは、単純な instance service ではなく、migration boundary になりやすい。

- `CommandRunnerMB`
- `BlackboardService`
- `VarKeyRegistryLocator`
- `LoadingScreenService`

は、単純な新規移植ではなく、target path への再設計が必要な候補である。

---

## `SceneKernel` の registration contract

`SceneKernel` が service を持つとき、登録は明示的でなければならない。

最低限、登録には次の情報を持たせる。

- `EntityRef`
- service の種類
- service shape（instance / data）
- source location
- authoring metadata
- compatibility / legacy bridge tag
- 既存 serialized payload の hash または trace

登録時のルール:

- 同じ `EntityRef` で同じ service を二重登録しない
- missing `EntityRef` を silent fallback で埋めない
- owner が曖昧な service は validation failure にする
- service dependency は explicit に渡す
- per-entity DI scope を新しく増殖させない

`SceneKernel` は、service を discovery ではなく catalog で受け入れる。

---

## Preservation Floor

v2.1 は破壊的移行を許すが、何を守るかは先に固定する。

### 守るもの

- 既存 prefab / scene の見た目と、当面の公開 serialized field
- 既存 `*HubMB` 系の inspector surface
- 既存の command field shape
- 既存の `DynamicValue` authoring surface
- 既存の `ValueStore` generated key identity
- `ScopeAuthoringRoot` / `ScopeAuthoringLink` が持つ source trace
- `UnityObjectLinkAuthoring` が持つ link trace
- 既存 gameplay の見た目上の動作

### 変えてよいもの

- 内部 wiring
- registration method
- lifecycle の持ち方
- service の内部データ layout
- dependency injection の実装
- discovery / fallback / repair の方法
- class の分割・統合・移動

### 絶対に守るものではないもの

v2.1 は、内部の古い実装形を守るための仕様ではない。
`RuntimeLifetimeScopeBase` や `CommandRunnerMB` の現状の作りを保存することは目的ではない。
守るのは、現行ゲームの資産とプレイヤー向けの見え方である。

---

## 旧アーキテクチャの quarantine

次のものは v2.1 の target path ではなく、legacy quarantine として扱う。

- [RuntimeLifetimeScopeBase.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs)
- [ScopeFeatureInstallerUtility.cs](../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs)
- [CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs)
- [BlackboardService.cs](../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs)
- [VarKeyRegistryLocator.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs)
- [LoadingScreenService.cs](../../GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs)

これらを直接 target path に増築してはならない。

必要なら、explicit adapter を介して使う。
しかし adapter は repair path ではなく、一時的な migration bridge である。

### 明示的に禁止すること

- `GetComponentsInChildren` を runtime service discovery に使うこと
- `FindObjectsByType` を singleton 修復に使うこと
- `Transform.parent` を authority にすること
- `Resources.Load` を runtime registry repair に使うこと
- bulk executor registration を command architecture の中心にすること
- fallback repair を value / loading / boot の正常経路にすること

---

## 既存資産との整合

v2.1 は、すでに存在する v2 target 由来の trace 系資産を活用する。

- `UnityAuthoringBridge` は source / runtime link の橋渡しとして再利用する
- `ScopeAuthoringRoot` は module metadata と scope identity の基礎として再利用する
- `ScopeAuthoringLink` は prefab / variant の base trace を守る
- `UnityObjectLinkAuthoring` は object link の trace を守る

つまり、v2.1 は新しい trace system を乱立させず、既存の trace 資産を migration の基礎にする。

---

## 検証と失敗

v2.1 の失敗は、silent に修復してはいけない。

次のものは diagnostics / validation failure として扱う。

- `EntityRef`（= `ScopeRef`）の欠落
- `EntityRef`（= `ScopeRef`）の不整合
- 同一 entity への重複 service 登録
- owner 不明の service
- source trace を持たない authoring
- legacy bridge が target path に漏れた場合
- 形 2 へ移すべき service を、見えない fallback で形 1 に戻してしまうこと

v2.1 は「動いたから成功」ではない。
「どの entity が、どの service を、どの trace で持っているか」が監査できて初めて成功である。

---

## 矛盾点と修正方針

ここでは、現行の考え方と v2 target specs の間で特に気をつけるべき矛盾を明示する。

### 1. `SceneKernel` が全 service を持つことと、v2 `ServiceGraph` の意味は同じではない

`SceneKernel` は migration 層の所有者である。
v2 target の `ServiceGraph` は coarse-grained service の verified runtime であり、entity ごとの object registry ではない。

したがって、v2.1 で `SceneKernel` が entity-local service を持つことは許されるが、それをそのまま v2 core の `ServiceGraph` に流し込んではいけない。

### 2. GameObject 上の位置は authority ではない

`AuthoringMB` を root に置くか child に置くかは authoring 都合であり、runtime authority ではない。

authority は `EntityRef`（= `ScopeRef`）、source trace、validated plan に置く。

### 3. 旧 DI の「似ているから残す」は危険である

形 1 は旧 DI に似ているが、同一ではない。

- discovery を消す
- scope build の暗黙さを消す
- fallback repair を消す
- registration を SceneKernel の catalog へ寄せる

この 4 つができて初めて、似ているものを残す意味がある。

### 4. AoS は理想であり、phase 1 の口実ではない

AoS は性能とスケーラビリティのための将来目標である。
しかし、v2.1 の phase 1 で「AoS を採用するから instance の移行をしなくてよい」とはならない。

まずは instance で existing assets を守り、その後に AoS に移る。

### 5. legacy repair を正規経路にしてはいけない

`Resources.Load`、`FindObjectsByType`、`Transform.parent` 推定、`GetComponentsInChildren` discovery、bulk registration などは、旧アーキテクチャの圧力点である。
これらを「便利だから」と標準化すると、v2.1 は v2 ではなく再包装された旧アーキテクチャになる。

---

## 移行波

v2.1 の移行は、型の綺麗さではなく、資産保全と失敗半径の小ささで順序を決める。

### Wave 0: trace と identity の固定

- `AuthoringMB` role を固定する
- `EntityRef`（= `ScopeRef`）を導入する
- `ScopeAuthoringRoot` / `ScopeAuthoringLink` / `UnityObjectLinkAuthoring` を基礎にする
- 既存 scene / prefab を壊さずに trace を取る

### Wave 1: 形 1 の explicit registration への移行

- `AnimationSpriteHub` 系のような代表的 service から始める
- 既存 serialized field を保つ
- registration を `SceneKernel` に寄せる
- `IFeatureInstaller` 依存を quarantine へ移す

### Wave 2: boot / command / value の整理

- `CommandRunnerMB` の bulk registration を整理する
- `BlackboardService` / `VarKeyRegistryLocator` を target path から切り離す
- loading / boot の discovery 修復を排除する

### Wave 3: high-volume service の AoS 化

- 高頻度・高密度の service を AoS 形へ移す
- manager が record 群をまとめて処理する形にする
- performance budget と diagnostics で妥当性を示す

### Wave 4: legacy fallback の削除

- legacy bridge を閉じる
- discovery / repair / fallback を target path から消す
- direct-play と verified boot だけを残す

この順序は固定ではなく、測定と validation に応じて調整してよい。
ただし、Wave 0 を飛ばして後ろから始めてはならない。

---

## 受け入れ基準

この仕様は、以下が満たされたときに完了である。

- `SceneKernel` が scene-local service authority として定義されている
- service が type 1 と type 2 に分類されている
- type 1 は既存資産の保存に使える
- type 2 は将来の理想形として明示されているが、v2.1 phase 1 での実装義務ではない
- `EntityRef`（= `ScopeRef`）/ `AuthoringMB` の役割が明確である
- 既存の public serialized field を壊さずに移行できる
- discovery / fallback / repair を standard path にしない
- legacy quarantine が明示されている
- v2 target specs と矛盾しない
- 移行波の順序が監査可能である

この仕様の成功は、単に古いコードが動くことではない。
現行ゲームが、検証済みの v2 path に向けて、どの資産をどう守り、どの wiring をどう置き換えるかが説明できることである。

---

## テストケース

| テストケース | 目的 | 検証 |
|---|---|---|
| TC-V21-00-01 | `SceneKernel` が scene-local の service owner として明示されていることを確認する。 | `SceneKernel` が discovery ではなく explicit registration を受けると仕様に書かれていなければならない。 |
| TC-V21-00-02 | `EntityRef`（= `ScopeRef`）/ `AuthoringMB` の役割分担が明確であることを確認する。 | authority が transform hierarchy ではなく explicit identity にあると書かれていなければならない。 |
| TC-V21-00-03 | 形 1 が既存資産の preservation floor を守るための主軸であることを確認する。 | 既存 serialized field を保ったまま service registration を SceneKernel に移せると書かれていなければならない。 |
| TC-V21-00-04 | 形 2 が将来の理想形であり、v2.1 phase 1 の実装義務ではないことを確認する。 | AoS が model として定義され、phase 1 では必須ではないと明記されていなければならない。 |
| TC-V21-00-05 | legacy repair が target path に漏れないことを確認する。 | `GetComponentsInChildren`、`FindObjectsByType`、`Transform.parent`、`Resources.Load` が standard path ではないと書かれていなければならない。 |
| TC-V21-00-06 | v2 target specs と矛盾しないことを確認する。 | `SceneKernel` が v2 `ServiceGraph` の代用品ではなく migration 層であると明記されていなければならない。 |

# Kernel v2.1 Legacy System Replacement 仕様

## 文書ステータス

- 文書 ID: `01_LegacySystemReplacementSpec`
- 状態: Draft
- 役割: v2.1 において legacy system をどの順序で廃止し、何に置き換え、何を再利用し、何を破棄するかを定義する
- 範囲: `RuntimeLTS` 廃止、`IScopeNode` / `IRuntimeResolver` / `IFeatureInstaller` 置換、`LTSIdentityMB` の `EntityIdentityMB` 化、service main logic の再利用境界、value / command / kernel 接続点の作り直し
- 非目標: legacy API の延命、temporary dual-runtime の長期維持、old/new resolver の共存を前提にした設計

### 改訂メモ

この文書は、legacy system を quarantine するだけでは足りず、どこから先に壊すかを固定するために作る。

v2.1 では `LTS` を新設計の一部として残さない。
残すのは service の domain logic だけであり、resolver、installer、scope build、hierarchy-derived identity、runtime fallback は target path から撤去する。

---

## 所有範囲

この仕様が所有するもの:

- legacy replacement の基本方針
- 削除対象と再利用対象の分類
- `RuntimeLTS` 廃止順序
- `IScopeNode` / `IRuntimeResolver` / `IFeatureInstaller` の置換方針
- `LTSIdentityMB` を `EntityIdentityMB` に置き換える rule
- authoring MB を declaration MB に変える rule
- service main logic と composition logic の分離 rule
- command / value / loading / kernel 接続点の再設計 rule
- migration wave と gate
- diagnostics / test / compile-boundary に対する要求

この仕様が所有しないもの:

- 各 service の最終 gameplay 挙動
- 各 service の最終 runtime API 詳細
- `ServiceGraph` / `ScopeGraph` / `LifecyclePlan` / `CommandCatalog` / `ValueStore` の v2 core semantics そのもの
- individual service port の実装手順の全文

01 は v2.1 の dismantling plan を所有する。
legacy をどう再解釈するかではなく、どう解体して target path に載せ替えるかを所有する。

---

## 目的

v2.1 の legacy replacement の目的は次の通り。

```text
1. RuntimeLTS を target path から最初に外す。
2. IScopeNode + Resolver を service 接続の authority から外す。
3. IFeatureInstaller を plan declaration に置き換える。
4. LTSIdentityMB を EntityIdentityMB に置き換える。
5. service の main logic だけを残し、接続点はすべて作り直す。
```

中心ルール:

```text
legacy の service logic は再利用してよい。
legacy の composition path は再利用してはならない。
```

追加ルール:

```text
target path では、LTS を使わないことが既定である。
```

---

## v2.1 00 との関係

[00_KernelV21MigrationOverviewSpec.md](00_KernelV21MigrationOverviewSpec.md) は、`ApplicationKernel` / `SceneKernel` の 2 層と entity-scoped `ServiceGraph` を移行層の中心に置いた。

01 はその続きとして、既存コードベースをその形へ載せ替えるための dismantling order を定義する。

00 が「どういう移行アーキテクチャにするか」を所有するなら、
01 は「現在の legacy をどう壊してそこへ移すか」を所有する。

---

## 現行コードの監査結果

### 中心的な legacy authority

現在の旧アーキテクチャでは、次のものが runtime authority を持っている。

- [IScopeNode.cs](../../GameLib/Script/Common/LTS/Core/IScopeNode.cs)
- [RuntimeLifetimeScope.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs)
- [RuntimeResolverHub.cs](../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs)
- [BaseLifetimeScope.cs](../../GameLib/Script/Common/LTS/Core/BaseLifetimeScope.cs)
- [LTSIdentityMB.cs](../../GameLib/Script/Common/LTS/Identity/MB/LTSIdentityMB.cs)
- [ScopeFeatureInstallerUtility.cs](../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs)

この構造で起きていること:

- `IScopeNode` が identity、parent-child、resolver、visible/active state、lifecycle path を同時に持つ
- `RuntimeLifetimeScopeBase` が build、acquire/release、tick hub、identity registration、parent inference を同時に持つ
- `RuntimeResolverHub` / `RuntimeResolver` が type-based resolution と parent fallback を持つ
- `ScopeFeatureInstallerUtility` が `GetComponentsInChildren` と `Transform.parent` を使って installer ownership を推定する
- `LTSIdentityMB` が hierarchy から kind を推定し、installer と identity registration を両方持つ

このままでは、v2.1 の `EntityRef`、`ServiceGraph`、`LifecyclePlan`、`ValueStore` に authority を移せない。

### 主要な secondary legacy authority

legacy replacement で特に影響が大きい接続点:

- [CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs)
- [BlackboardMB.cs](../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs)
- [BlackboardService.cs](../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs)
- [VarKeyRegistryLocator.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs)
- [VarIdResolver.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs)

ここで起きていること:

- command は `CommandRunnerMB` が bulk registration と lifecycle wiring を担う
- blackboard は `IScopeNode.Parent` と `Resolver` を使って hierarchical fallback を行う
- value key は runtime で registry lookup や stable-key fallback に依存する
- authoring MB が service registration と init timing を同時に持つ

### 残すべきもの

再利用対象は service の domain logic である。

代表例:

- `MaterialFx` 系の処理本体
- `ButtonChannel` 系の処理本体
- `UISelectionService` 系の処理本体
- `ModalLayerStack` 系の処理本体
- `UINavigation` 系の処理本体
- channel / hub / visual / animation の player 以外の core behavior

再利用条件:

- main logic が resolver fallback、installer mutation、hierarchy search を内部前提にしない形へ分離されること
- `IScopeNode` や `IRuntimeResolver` を public dependency として残さないこと

### 残してはいけないもの

次は target path で残してはならない。

- `RuntimeLifetimeScopeBase`
- `BaseLifetimeScope`
- `IRuntimeResolver`
- `RuntimeContainerBuilder`
- `RuntimeResolver`
- `IFeatureInstaller`
- `ScopeFeatureInstallerUtility`
- `LTSIdentityMB`
- `IScopeAcquireHandler` / `IScopeReleaseHandler` / `IScopeTickHandler` scan 前提
- `IScopeNode.Parent` を使う upward fallback
- `Transform.parent` から owner を推定する path

---

## Replacement Philosophy

legacy replacement の基本哲学は単純である。

### 1. runtime composition と service logic を分離する

service class が次を同時に持っているなら、分解対象である。

- registration
- resolver access
- lifecycle enrollment
- GameObject ownership inference
- domain logic

target path では、domain logic だけを残し、composition は `ApplicationKernel` / `SceneKernel` / `ServiceGraph` / `LifecyclePlan` 側へ移す。

### 2. old/new dual path を長期維持しない

01 では dual runtime を正式構成にしない。

許可されるのは短命の migration bridge のみである。
bridge は削除順が先に決まっていなければならない。

### 3. first cut は RuntimeLTS の除去である

`RuntimeLTS` が残っている限り、

- `IScopeNode`
- `Resolver`
- installer
- hierarchy-derived parent

が再侵入する。

したがって最初に削るべき中心は `RuntimeLifetimeScopeBase` である。

---

## New Ownership Model

### Entity

v2.1 では `ProjectLTS`、`SceneLTS`、`EntityLTS` を別の runtime species として扱わない。
すべて `Entity` である。

違いは次だけで表す。

- `EntityIdentityMB` が持つ metadata
- その entity に登録される service declaration
- そこから構築される entity-scoped `ServiceGraph`

### Identity

`LTSIdentityMB` は削除対象である。
置換先は `EntityIdentityMB` である。

`EntityIdentityMB` の責務:

- `EntityRef` を明示する
- source trace を保持する
- entity classification metadata を持つ
- authoring declaration の root になる

`EntityIdentityMB` がしてはならないこと:

- hierarchy から kind を推定すること
- installer として service registration を実行すること
- runtime resolver に identity service を登録すること

### Authoring MB

service 用 MB は `IFeatureInstaller` ではなく declaration MB として扱う。

declaration MB の責務:

- この entity にどの service が載るかを示す
- service 設定値を保持する
- source trace を提供する
- plan generation に必要な metadata を提供する

declaration MB がしてはならないこと:

- `InstallFeature` を実装すること
- runtime container mutation を行うこと
- `Resolver.TryResolve` を使って依存接続をその場で組み立てること

### Runtime

runtime で service を持つのは `SceneKernel` 配下の entity-scoped `ServiceGraph` である。

resolve の基本形:

- `Resolve(EntityRef, ServiceId)`
- `TryResolve(EntityRef, ServiceId, out value)`

必要なら generated typed wrapper を上に載せてよいが、
authority は `EntityRef + ServiceId` の組である。

---

## Replacement Matrix

| Legacy Surface | 問題 | v2.1 置換先 | 置換 rule |
|---|---|---|---|
| `RuntimeLifetimeScopeBase` | build / resolver / lifecycle / hierarchy authority の混在 | `ApplicationKernel` + `SceneKernel` + `EntityIdentityMB` + entity-scoped `ServiceGraph` | target path から除去する |
| `IScopeNode` | entity identity と resolver と parent fallback の混在 | `EntityRef` + `ScopeGraph` handle + explicit runtime contract | service main logic の public dependency から外す |
| `IRuntimeResolver` | type-based resolution、parent fallback、interface collection | `ServiceGraph` / `CommandCatalog` / `ValueStore` / generated accessors | service resolution に使わない |
| `IFeatureInstaller` | MB が runtime container mutation を行う | declaration MB + plan contribution | MB は declarative input に限定する |
| `LTSIdentityMB` | hierarchy-derived kind、installer、dynamic registry 混在 | `EntityIdentityMB` | name と責務を置換する |
| `CommandRunnerMB` | bulk executor registration、scope-kind branch、lifecycle混在 | command declaration + verified `CommandCatalog` contribution | MB は command catalog input のみを持つ |
| `BlackboardMB` | installer、lifecycle、init、debug、auto-write 混在 | value declaration MB + value init plan + lifecycle plan | runtime registration をやめる |
| `BlackboardService` | parent walk と fallback write | explicit `ValueStore` boundary | hierarchy fallback を禁止する |
| `VarKeyRegistryLocator` | runtime `Resources.Load` 依存 | explicit verified registry / artifact input | runtime fallback を禁止する |
| `VarIdResolver` | runtime stable-key resolution | generated `ValueKeyId` / verified mapping | runtime ID invention を禁止する |

---

## Required Deletions

### Immediate deletion target

v2.1 target path で最初に削除対象にするもの:

- `RuntimeLifetimeScopeBase` 依存
- `BaseLifetimeScope` 依存
- `IFeatureInstaller` 依存
- `InstallFeature(...)` path
- `ScopeFeatureInstallerUtility.InstallOwnedFeatureInstallers(...)`
- `ScopeFeatureInstallerUtility.TryGetNearestScopeNode(...)`
- service 内の `Resolver.TryResolve(...)` による service-to-service wiring

### Rename / replacement target

- `LTSIdentityMB` -> `EntityIdentityMB`
- `LifetimeScopeKind` 由来の種別分岐 -> entity metadata / service composition
- `ProjectLTS` / `SceneLTS` / `EntityLTS` という概念名 -> `Entity`

### Delayed removal target

main logic を先に分離してから消すもの:

- `BlackboardService`
- `CommandRunnerMB`
- actor / target resolution helpers
- old UI hierarchy coupling helper

ただし delayed removal でも、target path の authority にしてはならない。

---

## Service Reuse Rule

service main logic を残す場合の rule:

### 許可

- algorithm
- state transition
- gameplay rule
- rendering / animation / UI behavior
- pure helper
- data transform

### 不許可

- `IScopeNode` を constructor / public method の必須引数にすること
- `IRuntimeResolver` を constructor / field に保持すること
- `Resolver.TryResolve` で他 service を取ること
- `Parent` を walk して fallback すること
- `Transform.parent` や `GetComponentInParent` で owner を推定すること
- service 自身が installer になること

### 分離方法

main logic を残す service は、少なくとも次の 2 層に割る。

1. declaration / bridge layer
2. runtime logic layer

必要ならさらに:

3. service-owned graph / cache layer

UI、channel、MaterialFx の多くはこの分離が必要である。

---

## Legacy-to-Target Subsystem Mapping

### Kernel 接続点

旧:

- `RuntimeLifetimeScopeBase`
- `IRuntimeResolver`
- `IFeatureInstaller`

新:

- `SceneKernel`
- entity-scoped `ServiceGraph`
- verified plan generation

### Lifecycle 接続点

旧:

- `IScopeAcquireHandler`
- `IScopeReleaseHandler`
- `IScopeTickHandler`
- `RuntimeAcquireReleaseDispatcher`

新:

- `LifecyclePlan`
- explicit lifecycle target declaration
- table-driven dispatcher

### Command 接続点

旧:

- `CommandRunnerMB`
- bulk executor registration
- scope kind branch

新:

- command declaration MB
- verified `CommandCatalog`
- explicit command dependency declaration

### Value 接続点

旧:

- `BlackboardMB`
- `BlackboardService`
- `VarKeyRegistryLocator`
- `VarIdResolver`

新:

- value declaration MB
- `ValueSchema`
- `ValueStore`
- value init plan
- verified key mapping

### UI 接続点

旧:

- hierarchy-dependent service ownership
- local resolver walk
- `IScopeNode` based neighbor resolution

新:

- `ServiceGraph` registration
- service-owned explicit hierarchy graph
- cached handle / dense lookup
- lifecycle-driven update

---

## Migration Waves

### Wave 1: RuntimeLTS shutdown

最初の gate:

- `RuntimeLifetimeScopeBase` を target path の authority から外す
- `IScopeNode` を new service API から外す
- `LTSIdentityMB` の新規利用を禁止する
- `EntityIdentityMB` を導入する

完了条件:

- 新規 service path が `RuntimeLTS` に依存しない
- 新規 entity registration が `SceneKernel` 経由で行われる

### Wave 2: installer to declaration

- `IFeatureInstaller` を declaration MB に置き換える
- MB は plan input だけを持つ
- `InstallFeature(...)` を new path で禁止する

完了条件:

- service 登録は verified plan 由来のみ
- `GetComponentsInChildren` で installer を探さない

### Wave 3: resolve path rewrite

- `Resolver.TryResolve(...)` を service-to-service wiring から除去する
- `IScopeNode.Parent` fallback を除去する
- `ActorSourceFastResolver` 系の old identity search を段階的に target graph へ置き換える

完了条件:

- main service が `EntityRef + ServiceId` resolve を使う
- hierarchy fallback が service normal path に残っていない

### Wave 4: command / value replacement

- `CommandRunnerMB` を command declaration へ置換
- `BlackboardMB` / `BlackboardService` を value plan / store へ置換
- `VarKeyRegistryLocator` / `VarIdResolver` の runtime fallback を除去

完了条件:

- command / value は target path の verified authority を通る
- runtime stable-key fallback がない

### Wave 5: bridge closure

- temporary bridge を削除する
- old resolver API を削除する
- `LTS` assembly family を削除対象にする

完了条件:

- target path から `LTS` namespace 参照が消えている
- bridge を落としても runtime semantics が維持される

---

## Compile Boundary Rule

01 に必要な compile-boundary rule:

- new kernel / v2.1 assembly は `Common/LTS` に依存してはならない
- legacy bridge が必要なら `GameLib.Legacy.*` または同等の quarantine assembly に閉じ込める
- target service logic は `IFeatureInstaller` を参照してはならない
- target identity MB は `LTSIdentityMB` を継承してはならない

新アーキテクチャが `Common/LTS` を直接参照しないと成立しないなら、01 は失敗である。

---

## Performance Rule

legacy replacement は設計を綺麗にするだけでなく、性能も改善しなければならない。

必須 rule:

- service resolution は type scan ではなく bounded lookup にする
- parent walk fallback を hot path から除去する
- command executor の bulk registration scan を steady state に残さない
- value lookup は runtime stable-key resolve を使わない
- UI / channel / selection 系は cached handle と dense table を使う
- migration bridge は計測可能で、temporary cost として隔離される

性能のために diagnostics や explicit ownership を削ってはならない。

---

## Diagnostics と Failure Policy

次は validation / diagnostics failure にする:

- target path で `RuntimeLTS` が必要になること
- service main logic が `IRuntimeResolver` を public dependency に持つこと
- declaration MB が `IFeatureInstaller` のまま残ること
- `LTSIdentityMB` が target path の entity identity として使われること
- `Resolver.TryResolve` による service 接続が target path に残ること
- `BlackboardService` の parent fallback が target path に残ること
- `VarIdResolver` の runtime stable-key generation が残ること

成功条件:

- どの legacy surface を何に置換したかが監査できる
- main logic と composition logic の分離がコード上で追える
- bridge を消す順番が文書化されている

---

## 受け入れ基準

この仕様は、以下が満たされたときに完了である。

- `RuntimeLTS` を最初の削除対象として明記している
- `IScopeNode` / `IRuntimeResolver` / `IFeatureInstaller` が target path に残らないと明記している
- `LTSIdentityMB` を `EntityIdentityMB` に置換すると明記している
- declaration MB が plan input だけを持つと明記している
- service main logic は残してよいが、接続点は再利用しないと明記している
- command / value / kernel 接続点を再設計対象として明記している
- migration wave が削除順に沿っている
- compile-boundary と performance rule が含まれている

---

## テストケース

| テストケース | 目的 | 検証 |
|---|---|---|
| `TC-V21-01-01` | `RuntimeLTS` が target path の authority でないことを確認する | `RuntimeLifetimeScopeBase` が replacement の最初の削除対象として定義されていなければならない |
| `TC-V21-01-02` | `LTSIdentityMB` が target identity でないことを確認する | `EntityIdentityMB` への置換が明記されていなければならない |
| `TC-V21-01-03` | declaration MB が installer でないことを確認する | `IFeatureInstaller` を plan declaration に置換すると書かれていなければならない |
| `TC-V21-01-04` | service main logic だけを再利用することを確認する | resolver / parent fallback / installer mutation を再利用対象から除外していなければならない |
| `TC-V21-01-05` | command / value の接続点が再設計対象であることを確認する | `CommandRunnerMB`、`BlackboardService`、`VarKeyRegistryLocator`、`VarIdResolver` が replacement matrix に入っていなければならない |
| `TC-V21-01-06` | performance rule が replacement に含まれることを確認する | bounded lookup、no parent walk fallback、no runtime stable-key resolve が明記されていなければならない |

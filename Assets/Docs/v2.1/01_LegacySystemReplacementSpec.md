# Kernel v2.1 Legacy System Replacement 仕槁E

## 斁E��スチE�Eタス

- 斁E�� ID: `01_LegacySystemReplacementSpec`
- 状慁E Draft
- 役割: v2.1 において legacy system をどの頁E��で廁E��し、何に置き換え、何を再利用し、何を破棁E��るかを定義する
- 篁E��: `RuntimeLTS` 廁E��、`IScopeNode` / `IRuntimeResolver` / `IFeatureInstaller` 置換、`LTSIdentityMB` の `EntityIdentityMB` 化、service main logic の再利用墁E��、value / command / kernel 接続点の作り直ぁE
- 非目樁E legacy API の延命、temporary dual-runtime の長期維持、old/new resolver の共存を前提にした設訁E

### 改訂メモ

こ�E斁E��は、legacy system めEquarantine するだけでは足りず、どこから�Eに壊すかを固定するために作る、E

v2.1 では `LTS` を新設計�E一部として残さなぁE��E
残すのは service の domain logic だけであり、resolver、installer、scope build、hierarchy-derived identity、runtime fallback は target path から撤去する、E

---

## 所有篁E��

こ�E仕様が所有するもの:

- legacy replacement の基本方釁E
- 削除対象と再利用対象の刁E��E
- `RuntimeLTS` 廁E��頁E��E
- `IScopeNode` / `IRuntimeResolver` / `IFeatureInstaller` の置換方釁E
- `LTSIdentityMB` めE`EntityIdentityMB` に置き換える rule
- authoring MB めEdeclaration MB に変えめErule
- service main logic と composition logic の刁E�� rule
- command / value / loading / kernel 接続点の再設訁Erule
- migration wave と gate
- diagnostics / test / compile-boundary に対する要汁E

こ�E仕様が所有しなぁE��の:

- 吁Eservice の最絁Egameplay 挙動
- 吁Eservice の最絁Eruntime API 詳細
- `ServiceGraph` / `ScopeGraph` / `LifecyclePlan` / `CommandCatalog` / `ValueStore` の v2 core semantics そ�Eも�E
- individual service port の実裁E��頁E�E全斁E

01 は v2.1 の dismantling plan を所有する、E
legacy をどぁE�E解釈するかではなく、どぁE��体して target path に載せ替えるかを所有する、E

---

## 目皁E

v2.1 の legacy replacement の目皁E�E次の通り、E

```text
1. RuntimeLTS めEtarget path から最初に外す、E
2. IScopeNode + Resolver めEservice 接続�E authority から外す、E
3. IFeatureInstaller めEplan declaration に置き換える、E
4. LTSIdentityMB めEEntityIdentityMB に置き換える、E
5. service の main logic だけを残し、接続点はすべて作り直す、E
```

中忁E��ール:

```text
legacy の service logic は再利用してよい、E
legacy の composition path は再利用してはならなぁE��E
```

追加ルール:

```text
target path では、LTS を使わなぁE��とが既定である、E
```

---

## v2.1 00 との関俁E

[00_KernelV21MigrationOverviewSpec.md](00_KernelV21MigrationOverviewSpec.md) は、`ApplicationKernel` / `SceneKernel` の 2 層と entity-scoped `ServiceGraph` を移行層の中忁E��置ぁE��、E

01 はそ�E続きとして、既存コード�Eースをその形へ載せ替えるための dismantling order を定義する、E

00 が「どぁE��ぁE��行アーキチE��チャにするか」を所有するなら、E
01 は「現在の legacy をどぁE��してそこへ移すか」を所有する、E

---

## 現行コード�E監査結果

### 中忁E��な legacy authority

現在の旧アーキチE��チャでは、次のも�EぁEruntime authority を持ってぁE��、E

- [IScopeNode.cs](../../GameLib/Script/Common/LTS/Core/IScopeNode.cs)
- [RuntimeLifetimeScope.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs)
- [RuntimeResolverHub.cs](../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs)
- [BaseLifetimeScope.cs](../../GameLib/Script/Common/LTS/Core/BaseLifetimeScope.cs)
- [LTSIdentityMB.cs](../../GameLib/Script/Kernel/Authoring/EntityIdentityMB.cs)
- [ScopeFeatureInstallerUtility.cs](../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs)

こ�E構造で起きてぁE��こと:

- `IScopeNode` ぁEidentity、parent-child、resolver、visible/active state、lifecycle path を同時に持つ
- `RuntimeLifetimeScopeBase` ぁEbuild、acquire/release、tick hub、identity registration、parent inference を同時に持つ
- `RuntimeResolverHub` / `RuntimeResolver` ぁEtype-based resolution と parent fallback を持つ
- `ScopeFeatureInstallerUtility` ぁE`GetComponentsInChildren` と `Transform.parent` を使って installer ownership を推定すめE
- `LTSIdentityMB` ぁEhierarchy から kind を推定し、installer と identity registration を両方持つ

こ�Eままでは、v2.1 の `EntityRef`、`ServiceGraph`、`LifecyclePlan`、`ValueStore` に authority を移せなぁE��E

### 主要な secondary legacy authority

legacy replacement で特に影響が大きい接続点:

- [CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs)
- [BlackboardMB.cs](../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs)
- [BlackboardService.cs](../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs)
- [VarKeyRegistryLocator.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs)
- [VarIdResolver.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs)

ここで起きてぁE��こと:

- command は `CommandRunnerMB` ぁEbulk registration と lifecycle wiring を担ぁE
- blackboard は `IScopeNode.Parent` と `Resolver` を使って hierarchical fallback を行う
- value key は runtime で registry lookup めEstable-key fallback に依存すめE
- authoring MB ぁEservice registration と init timing を同時に持つ

### 残すべきもの

再利用対象は service の domain logic である、E

代表侁E

- `MaterialFx` 系の処琁E��佁E
- `ButtonChannel` 系の処琁E��佁E
- `UISelectionService` 系の処琁E��佁E
- `ModalLayerStack` 系の処琁E��佁E
- `UINavigation` 系の処琁E��佁E
- channel / hub / visual / animation の player 以外�E core behavior

再利用条件:

- main logic ぁEresolver fallback、installer mutation、hierarchy search を�E部前提にしなぁE��へ刁E��されること
- `IScopeNode` めE`IRuntimeResolver` めEpublic dependency として残さなぁE��と

### 残してはぁE��なぁE��の

次は target path で残してはならなぁE��E

- `RuntimeLifetimeScopeBase`
- `BaseLifetimeScope`
- `IRuntimeResolver`
- `RuntimeContainerBuilder`
- `RuntimeResolver`
- `IFeatureInstaller`
- `ScopeFeatureInstallerUtility`
- `LTSIdentityMB`
- `IScopeAcquireHandler` / `IScopeReleaseHandler` / `IScopeTickHandler` scan 前提
- `IScopeNode.Parent` を使ぁEupward fallback
- `Transform.parent` から owner を推定すめEpath

---

## Replacement Philosophy

legacy replacement の基本哲学は単純である、E

### 1. runtime composition と service logic を�E離する

service class が次を同時に持ってぁE��なら、�E解対象である、E

- registration
- resolver access
- lifecycle enrollment
- GameObject ownership inference
- domain logic

target path では、domain logic だけを残し、composition は `ApplicationKernel` / `SceneKernel` / `ServiceGraph` / `LifecyclePlan` 側へ移す、E

### 2. old/new dual path を長期維持しなぁE

01 では dual runtime を正式構�EにしなぁE��E

許可される�Eは短命の migration bridge のみである、E
bridge は削除頁E��先に決まってぁE��ければならなぁE��E

### 3. first cut は RuntimeLTS の除去である

`RuntimeLTS` が残ってぁE��限り、E

- `IScopeNode`
- `Resolver`
- installer
- hierarchy-derived parent

が�E侵入する、E

したがって最初に削るべき中忁E�E `RuntimeLifetimeScopeBase` である、E

---

## New Ownership Model

### Entity

v2.1 では `ProjectLTS`、`SceneLTS`、`EntityLTS` を別の runtime species として扱わなぁE��E
すべて `Entity` である、E

違いは次だけで表す、E

- `EntityIdentityMB` が持つ metadata
- そ�E entity に登録されめEservice declaration
- そこから構築される entity-scoped `ServiceGraph`

### Identity

`LTSIdentityMB` は削除対象である、E
置換�Eは `EntityIdentityMB` である、E

`EntityIdentityMB` の責勁E

- `EntityRef` を�E示する
- source trace を保持する
- entity classification metadata を持つ
- authoring declaration の root になめE

`EntityIdentityMB` がしてはならなぁE��と:

- hierarchy から kind を推定すること
- installer として service registration を実行すること
- runtime resolver に identity service を登録すること

### Authoring MB

service 用 MB は `IFeatureInstaller` ではなぁEdeclaration MB として扱ぁE��E

declaration MB の責勁E

- こ�E entity にどの service が載るかを示ぁE
- service 設定値を保持する
- source trace を提供すめE
- plan generation に忁E��な metadata を提供すめE

declaration MB がしてはならなぁE��と:

- `InstallFeature` を実裁E��ること
- runtime container mutation を行うこと
- `Resolver.TryResolve` を使って依存接続をそ�E場で絁E��立てること

### Runtime

runtime で service を持つのは `SceneKernel` 配下�E entity-scoped `ServiceGraph` である、E

resolve の基本形:

- `Resolve(EntityRef, ServiceId)`
- `TryResolve(EntityRef, ServiceId, out value)`

忁E��なめEgenerated typed wrapper を上に載せてよいが、E
authority は `EntityRef + ServiceId` の絁E��ある、E

---

## Replacement Matrix

| Legacy Surface | 問顁E| v2.1 置換�E | 置揁Erule |
|---|---|---|---|
| `RuntimeLifetimeScopeBase` | build / resolver / lifecycle / hierarchy authority の混在 | `ApplicationKernel` + `SceneKernel` + `EntityIdentityMB` + entity-scoped `ServiceGraph` | target path から除去する |
| `IScopeNode` | entity identity と resolver と parent fallback の混在 | `EntityRef` + `ScopeGraph` handle + explicit runtime contract | service main logic の public dependency から外す |
| `IRuntimeResolver` | type-based resolution、parent fallback、interface collection | `ServiceGraph` / `CommandCatalog` / `ValueStore` / generated accessors | service resolution に使わなぁE|
| `IFeatureInstaller` | MB ぁEruntime container mutation を行う | declaration MB + plan contribution | MB は declarative input に限定すめE|
| `LTSIdentityMB` | hierarchy-derived kind、installer、dynamic registry 混在 | `EntityIdentityMB` | name と責務を置換すめE|
| `RuntimeManagerMB` / `RuntimeLifetimeScopePool` | runtime spawn / pool authority と parent-scoped reuse | `SceneKernel` 配下の unified spawn core + prefab-family pool + explicit reparent policy | target path から除去する |
| `CommandRunnerMB` | bulk executor registration、scope-kind branch、lifecycle混在 | command declaration + verified `CommandCatalog` contribution | MB は command catalog input のみを持つ |
| `BlackboardMB` | installer、lifecycle、init、debug、auto-write 混在 | value declaration MB + value init plan + lifecycle plan | runtime registration をやめる |
| `BlackboardService` | parent walk と fallback write | explicit `ValueStore` boundary | hierarchy fallback を禁止する |
| `VarKeyRegistryLocator` | runtime `Resources.Load` 依孁E| explicit verified registry / artifact input | runtime fallback を禁止する |
| `VarIdResolver` | runtime stable-key resolution | generated `ValueKeyId` / verified mapping | runtime ID invention を禁止する |

---

## Required Deletions

### Immediate deletion target

v2.1 target path で最初に削除対象にするも�E:

- `RuntimeLifetimeScopeBase` 依孁E
- `BaseLifetimeScope` 依孁E
- `IFeatureInstaller` 依孁E
- `InstallFeature(...)` path
- `ScopeFeatureInstallerUtility.InstallOwnedFeatureInstallers(...)`
- `ScopeFeatureInstallerUtility.TryGetNearestScopeNode(...)`
- `RuntimeManagerMB`
- `RuntimeLifetimeScopePool`
- service 冁E�E `Resolver.TryResolve(...)` による service-to-service wiring

### Rename / replacement target

- `LTSIdentityMB` -> `EntityIdentityMB`
- `LifetimeScopeKind` 由来の種別刁E��E-> entity metadata / service composition
- `ProjectLTS` / `SceneLTS` / `EntityLTS` とぁE��概念吁E-> `Entity`

### Delayed removal target

main logic を�Eに刁E��してから消すも�E:

- `BlackboardService`
- `CommandRunnerMB`
- actor / target resolution helpers
- old UI hierarchy coupling helper

ただぁEdelayed removal でも、target path の authority にしてはならなぁE��E

---

## Service Reuse Rule

service main logic を残す場合�E rule:

### 許可

- algorithm
- state transition
- gameplay rule
- rendering / animation / UI behavior
- pure helper
- data transform

### 不許可

- `IScopeNode` めEconstructor / public method の忁E��引数にすること
- `IRuntimeResolver` めEconstructor / field に保持すること
- `Resolver.TryResolve` で仁Eservice を取ること
- `Parent` めEwalk して fallback すること
- `Transform.parent` めE`GetComponentInParent` で owner を推定すること
- service 自身ぁEinstaller になること

### 刁E��方況E

main logic を残す service は、少なくとも次の 2 層に割る、E

1. declaration / bridge layer
2. runtime logic layer

忁E��ならさらに:

3. service-owned graph / cache layer

UI、channel、MaterialFx の多くはこ�E刁E��が忁E��である、E

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

---
### Spawn / Pool 接続点

旧:

- `RuntimeManagerMB`
- `RuntimeLifetimeScopePool`
- `RuntimeLifetimeScopeSpawnerService`
- `RuntimeLifetimeScope` / `SceneLifetimeScope` / `EntityLifetimeScope` / `UIElementLifetimeScope`
- parent-scoped pool key
- hierarchy scan delete

新:

- `SceneKernel`-owned unified spawn core
- `EntityRef`-keyed entity instance lease / handle
- scene-local spawn boundary / entity instance lease table
- scene host / declaration bridge
- prefab-family pool
- explicit attach / reparent policy
- mediator-based release / delete

hard rule:

- new target path の public naming と public contract に `RuntimeLifetimeScope` / `RuntimeManager` の語を持ち込まない
- compatibility adapter は許可するが、old manager / old pool / old runtime species を truth source として残さない
- scene / prefab の旧 component 参照は editor migration で置換し、temporary bridge は quarantine に限定する

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

最初�E gate:

- `RuntimeLifetimeScopeBase` めEtarget path の authority から外す
- `IScopeNode` めEnew service API から外す
- `LTSIdentityMB` の新規利用を禁止する
- `EntityIdentityMB` を導�Eする
- `SceneKernel` に scene-local spawn mediation boundary を追加する
- `RuntimeManagerMB` / `RuntimeLifetimeScopeSpawnerService` を asset migration 後に削除できる replacement host を先に用意する

完亁E��件:

- 新要Eservice path ぁE`RuntimeLTS` に依存しなぁE
- 新要Eentity registration ぁE`SceneKernel` 経由で行われる
- runtime-created entity の spawn / release / delete も `SceneKernel` 経由で行われる

### Wave 2: installer to declaration

- `IFeatureInstaller` めEdeclaration MB に置き換える
- MB は plan input だけを持つ
- `InstallFeature(...)` めEnew path で禁止する

完亁E��件:

- service 登録は verified plan 由来のみ
- `GetComponentsInChildren` で installer を探さなぁE

### Wave 3: resolve path rewrite

- `Resolver.TryResolve(...)` めEservice-to-service wiring から除去する
- `IScopeNode.Parent` fallback を除去する
- `ActorSourceFastResolver` 系の old identity search を段階的に target graph へ置き換える

完亁E��件:

- main service ぁE`EntityRef + ServiceId` resolve を使ぁE
- hierarchy fallback ぁEservice normal path に残ってぁE��ぁE

### Wave 4: command / value replacement

- `CommandRunnerMB` めEcommand declaration へ置揁E
- `BlackboardMB` / `BlackboardService` めEvalue plan / store へ置揁E
- `VarKeyRegistryLocator` / `VarIdResolver` の runtime fallback を除去

完亁E��件:

- command / value は target path の verified authority を通る
- runtime stable-key fallback がなぁE

### Wave 5: bridge closure

- temporary bridge を削除する
- old resolver API を削除する
- `LTS` assembly family を削除対象にする

完亁E��件:

- target path から `LTS` namespace 参�Eが消えてぁE��
- bridge を落としてめEruntime semantics が維持される

---

## Compile Boundary Rule

01 に忁E��な compile-boundary rule:

- new kernel / v2.1 assembly は `Common/LTS` に依存してはならなぁE
- legacy bridge が忁E��なめE`GameLib.Legacy.*` また�E同等�E quarantine assembly に閉じ込める
- target service logic は `IFeatureInstaller` を参照してはならなぁE
- target identity MB は `LTSIdentityMB` を継承してはならなぁE

新アーキチE��チャぁE`Common/LTS` を直接参�EしなぁE��成立しなぁE��ら、E1 は失敗である、E

---

## Performance Rule

legacy replacement は設計を綺麗にするだけでなく、性能も改喁E��なければならなぁE��E

忁E��Erule:

- service resolution は type scan ではなぁEbounded lookup にする
- parent walk fallback めEhot path から除去する
- command executor の bulk registration scan めEsteady state に残さなぁE
- value lookup は runtime stable-key resolve を使わなぁE
- UI / channel / selection 系は cached handle と dense table を使ぁE
- migration bridge は計測可能で、temporary cost として隔離されめE

性能のために diagnostics めEexplicit ownership を削ってはならなぁE��E

---

## Diagnostics と Failure Policy

次は validation / diagnostics failure にする:

- target path で `RuntimeLTS` が忁E��になること
- service main logic ぁE`IRuntimeResolver` めEpublic dependency に持つこと
- declaration MB ぁE`IFeatureInstaller` のまま残ること
- `LTSIdentityMB` ぁEtarget path の entity identity として使われること
- `Resolver.TryResolve` による service 接続が target path に残ること
- `BlackboardService` の parent fallback ぁEtarget path に残ること
- `VarIdResolver` の runtime stable-key generation が残ること

成功条件:

- どの legacy surface を何に置換したかが監査できる
- main logic と composition logic の刁E��がコード上で追える
- bridge を消す頁E��が文書化されてぁE��

---

## 受け入れ基溁E

こ�E仕様�E、以下が満たされたときに完亁E��ある、E

- `RuntimeLTS` を最初�E削除対象として明記してぁE��
- `IScopeNode` / `IRuntimeResolver` / `IFeatureInstaller` ぁEtarget path に残らなぁE��明記してぁE��
- `LTSIdentityMB` めE`EntityIdentityMB` に置換すると明記してぁE��
- declaration MB ぁEplan input だけを持つと明記してぁE��
- service main logic は残してよいが、接続点は再利用しなぁE��明記してぁE��
- command / value / kernel 接続点を�E設計対象として明記してぁE��
- migration wave が削除頁E��沿ってぁE��
- compile-boundary と performance rule が含まれてぁE��

---

## チE��トケース

| チE��トケース | 目皁E| 検証 |
|---|---|---|
| `TC-V21-01-01` | `RuntimeLTS` ぁEtarget path の authority でなぁE��とを確認すめE| `RuntimeLifetimeScopeBase` ぁEreplacement の最初�E削除対象として定義されてぁE��ければならなぁE|
| `TC-V21-01-02` | `LTSIdentityMB` ぁEtarget identity でなぁE��とを確認すめE| `EntityIdentityMB` への置換が明記されてぁE��ければならなぁE|
| `TC-V21-01-03` | declaration MB ぁEinstaller でなぁE��とを確認すめE| `IFeatureInstaller` めEplan declaration に置換すると書かれてぁE��ければならなぁE|
| `TC-V21-01-04` | service main logic だけを再利用することを確認すめE| resolver / parent fallback / installer mutation を�E利用対象から除外してぁE��ければならなぁE|
| `TC-V21-01-05` | command / value の接続点が�E設計対象であることを確認すめE| `CommandRunnerMB`、`BlackboardService`、`VarKeyRegistryLocator`、`VarIdResolver` ぁEreplacement matrix に入ってぁE��ければならなぁE|
| `TC-V21-01-06` | performance rule ぁEreplacement に含まれることを確認すめE| bounded lookup、no parent walk fallback、no runtime stable-key resolve が�E記されてぁE��ければならなぁE|

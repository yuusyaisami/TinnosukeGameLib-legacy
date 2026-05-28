# Kernel v2.1 実装マイルストーン仕様

## 文書ステータス

- 文書 ID: `05_ImplementationMilestoneSpec`
- 状態: Draft
- 役割: v2.1 において、旧アーキテクチャを完全削除しつつ新アーキテクチャへ差し替えるための実装順序、主要セクション、入口条件、出口条件、禁止ショートカットを定義する
- 範囲: `ApplicationKernel` / `SceneKernel` の 2 層 composition、`EntityIdentityMB`、declaration MB、entity-scoped `ServiceGraph`、`Lifecycle`、`VarStore` backend の `ValueStore`、`DynamicSource` / `DynamicValue` authoring surface、`CommandRunner` 再構成、`Scalar` 再構成、UI graph 化、legacy registry / hierarchy / LTS の削除順、spawn / pool authority cutover
- 非目標: sprint 見積もり、人員割当、個々の PR 粒度、最終 API 名称の固定

### 改訂メモ

この文書は、v2.1 の「何を作るか」ではなく「どの順で作るか」を固定する。

特に v2.1 では、次を守る必要がある。

- 高級 service の挙動はできるだけ保つ
- 旧 LTS / DI / hierarchy authority は消す
- 公開フィールドや authoring surface は preservation floor の範囲で守る
- `VarStore` は backend として残すが、`Blackboard` architecture は残さない
- `CommandRunner` は bootstrap を作り直し、engine の一部だけを暫定流用する
- `Scalar` は subsystem として残すが、`BaseScalarService` architecture は残さない
- runtime spawn / pool authority は 07 で定義し、05 では M5 の cutover と M6 の接続順に反映する

最も重要な実装ルール:

```text
コード削除より先に authority を切り替える。
ただし authority が切り替わった後は、legacy code を長期に温存しない。
```

---

## 所有範囲

この仕様が所有するもの:

- v2.1 の実装マイルストーン順序
- 各マイルストーンの主目標
- 各マイルストーンの実装セクション順
- 各マイルストーンの入口条件と出口条件
- 各マイルストーンで削除対象に入る legacy authority
- subsystem ごとの移行開始タイミング
- implementation short-cut の禁止条件

この仕様が所有しないもの:

- subsystem の意味論そのもの
- final class / namespace 名
- team planning
- CI ベンダーや branch 戦略

05 は実装順序を所有する。
00 から 04 に書かれた意味論や境界を再定義するものではない。

---

## 依存仕様

この仕様は次を前提にする。

- [00_KernelV21MigrationOverviewSpec.md](00_KernelV21MigrationOverviewSpec.md)
- [01_LegacySystemReplacementSpec.md](01_LegacySystemReplacementSpec.md)
- [02_ConcreteMigrationArchitectureSpec.md](02_ConcreteMigrationArchitectureSpec.md)
- [03_LegacyRemovalExamplesSpec.md](03_LegacyRemovalExamplesSpec.md)
- [04_VarStoreCommandScalarTreatmentSpec.md](04_VarStoreCommandScalarTreatmentSpec.md)
- [06_KernelLayerCompositionSpec.md](06_KernelLayerCompositionSpec.md)
- [07_SpawnPoolLifecycleSpec.md](07_SpawnPoolLifecycleSpec.md)
- [10_CommandCutoverMilestoneSpec.md](10_CommandCutoverMilestoneSpec.md)
- [11_DynamicSourceCutoverMilestoneSpec.md](11_DynamicSourceCutoverMilestoneSpec.md)
- [16_ImplementationMilestoneOrderSpec.md](../v2/16_ImplementationMilestoneOrderSpec.md)

05 は v2 の `16` をそのまま写す文書ではない。
v2.1 の live migration と legacy dismantling に合わせて、より implementation-facing に順序を固定する。

---

## 実装順序の基本原則

### 1. Delete-first の意味

v2.1 で言う「旧システムを先に消す」は、次の順で行う。

1. target path の authority から外す
2. replacement path から参照しない状態にする
3. compile boundary を切る
4. 物理削除する

つまり、replacement がないまま source file だけを先に消すことはしない。
だが、authority を旧 path に残したまま新 path だけを積み上げることも禁止する。

### 2. Main logic は残してよいが wiring は残さない

残してよいもの:

- service の domain logic
- `VarStore` backend
- command execution semantics の一部
- scalar modulation logic の一部
- UI の state machine / channel logic

残してはいけないもの:

- `IFeatureInstaller`
- `IScopeNode` / `IRuntimeResolver`
- `LifetimeScopeKind` に依存した runtime authority
- `Transform.parent` による owner 推定
- runtime stable-key fallback
- `Resources.Load` fallback
- `RuntimeManagerMB` / `RuntimeLifetimeScopePool`
- parent-scoped pool key
- same-parent-only reuse
- hierarchy scan delete

### 3. 一度に複数 subsystem を target truth にしない

次は避ける。

- `ApplicationKernel` / `SceneKernel` と `RuntimeLTS` が同時に authority
- `ValueStore` と `BlackboardService` が同時に truth
- `CommandCatalog` の authority と entity-fixed `CommandRunner` execution core を混同しない
- new scalar と `BaseScalarService` ancestor fallback が同時に truth

temporary bridge は許可するが、truth は 1 つでなければならない。

### 4. UI は後回しではなく中盤で入れる

UI は GameObject 依存が強いが、依存先が多い。
そのため最終盤まで後回しにすると、service graph / lifecycle / query / value / command の不整合が見えにくくなる。

したがって UI core は command / value / scalar の後、feature mass-port の前に入れる。

---

## 全体順序

| マイルストーン | 名称 | 主目標 | 終了シグナル |
| --- | --- | --- | --- |
| M0 | Spec / Inventory Freeze | 00 から 04 の前提と legacy inventory を固定する | legacy authority と replacement target が文書化されている |
| M1 | Compile Boundary / Kill Switch | 新 runtime が `Common/LTS` を直接参照しない足場を作る | target asmdef / namespace / bridge 境界が立つ |
| M2 | Kernel Layer Composition | `ApplicationKernel` / `SceneKernel` の 2 層 root と handoff を作る | DDOL root と scene-local root が分離され、scene transition が明示化される |
| M3 | Entity Identity / Declaration | `EntityIdentityMB` と declaration MB を導入する | 新規 authoring が `LTSIdentityMB` / `IFeatureInstaller` に依存しない |
| M4 | SceneKernel Skeleton | `SceneKernel`、`EntityRef` registration table、entity-scoped `ServiceGraph` の殻を作る | `EntityRef + ServiceId` resolve の入口が存在する |
| M5 | Legacy Authority Shutdown | `RuntimeLTS`、`RuntimeManagerMB`、`RuntimeLifetimeScopePool`、registry、hierarchy authority、spawn / pool authority を target path から外す | `IScopeNode` / resolver / hierarchy scan / direct destroy が target path から消える |
| M6 | Instance-Service Runtime | type 1 service の registration / lifecycle / owner model を確立し、07 の unified spawn / pool mediation を service runtime に接続する | 高級 service を `SceneKernel` 経由で 1 つ登録できる |
| M7 | Value Runtime with VarStore Backend | `VarStore` backend の `ValueStore` を導入し `Blackboard` truth を切る | new value path が `BlackboardService` なしで動く |
| M8 | Entity-Fixed Command Runtime Transition | `CommandRunnerService` + `CommandCatalog` path を立てる | entity-fixed `CommandRunner` が Entity ごとに存在し、`CommandRunnerMB` を使わずに command 実行できる |
| M9 | Scalar Runtime Transition | scalar identity / binding / lifecycle を新 path に載せる | `BaseScalarService` ancestor fallback なしで scalar が動く |
| M10 | UI Core Migration | UI graph、selection、navigation、modal、button channel を新 path へ移す | UI core が service-owned graph で動く |
| M11 | Full Feature / Asset Cutover | 全 service / command / value / scalar / query / spawn route と既存 scene / prefab 参照を new path へ切り替える | 残存 inventory が migration-only quarantine を除いて 0 になり、既存 scene / prefab が少修正で new path へ移行される |
| M12 | Full Replacement Verification / Legacy Purge | 実ゲーム全体が新アーキテクチャのみで動くことを確認し、bridge と legacy code を物理削除する | コンパイル済みゲームが existing scene / prefab を使って new path だけで起動・進行・終了し、legacy runtime dependency が 0 になる |

必要順序:

```text
M0 -> M1 -> M2 -> M3 -> M4 -> M5 -> M6 -> M7 -> M8 -> M9 -> M10 -> M11 -> M12
```

---

## 細分化フェーズ

以下は、各 milestone の内部をさらに実装順に切り分けたものだ。
ここでの順序は、前の段階が完了して初めて次に進む前提で読む。

| 親 milestone | sub-phase | 実装順 | 目的 | 出口条件 |
| --- | --- | ---: | --- | --- |
| M0 | M0-1 | 1 | 00 から 04 の vocabulary と inventory を揃える | 仕様間の用語ズレが解消されている |
| M0 | M0-2 | 2 | legacy authority を一覧化する | `RuntimeLTS` / `IScopeNode` / `IFeatureInstaller` / `Blackboard` / `CommandRunner` の削除候補が確定する |
| M0 | M0-3 | 3 | replacement target を一覧化する | `ApplicationKernel` / `SceneKernel` / `EntityIdentityMB` / `ValueStore` / `CommandCatalog` の置換先が確定する |
| M1 | M1-1 | 1 | target asmdef / namespace を切る | new runtime 用の compile boundary が立つ |
| M1 | M1-2 | 2 | bridge/quarantine assembly を分離する | legacy bridge が core runtime から切り離される |
| M1 | M1-3 | 3 | build-only の kill switch を作る | new path が `Common/LTS` 直参照なしで build できる |
| M2 | M2-1 | 1 | `ApplicationKernel` shell を作る | DDOL root の最小実装が存在する |
| M2 | M2-2 | 2 | `SceneKernel` shell を作る | scene-local root の最小実装が存在する |
| M2 | M2-3 | 3 | handoff / disposal contract を作る | scene transition の生存・破棄が明示化される |
| M2 | M2-4 | 4 | V2 部品の配置先を固定する | boot/runtime component mapping が文書化される |
| M3 | M3-1 | 1 | `EntityIdentityMB` を作る | `EntityRef` を持つ root bridge がある |
| M3 | M3-2 | 2 | declaration MB base contract を作る | installer ではない input surface ができる |
| M3 | M3-3 | 3 | authoring trace / source metadata を接続する | plan へ入れる provenance が取れる |
| M3 | M3-4 | 4 | 代表 MB を 1 つ declaration 化する | 1 本の実例が plan 経由で流れる |
| M4 | M4-1 | 1 | `EntityRef` ベースの entity registration table を作る | entity ごとの slot 管理が始まり、resolve hot path の入口ができる |
| M4 | M4-2 | 2 | `Resolve(EntityRef, ServiceId)` を作る | service resolve の唯一入口がある |
| M4 | M4-3 | 3 | lifecycle dispatch entry を作る | lifecycle が plan 駆動で回る |
| M4 | M4-4 | 4 | diagnostics entry を作る | missing / invalid / duplicate を structured failure にできる |
| M5 | M5-1 | 1 | `RuntimeLifetimeScopeBase` / `RuntimeManagerMB` / `RuntimeLifetimeScopePool` を target path から切る | old authority が new path に入らない |
| M5 | M5-2 | 2 | `IScopeNode` / `IRuntimeResolver` を外す | parent fallback と type scan が止まる |
| M5 | M5-3 | 3 | registry / hierarchy helper を外す | `BaseLifetimeScopeRegistry` / `ScopeNodeHierarchy` が authority でなくなる |
| M5 | M5-4 | 4 | dynamic registry / search path を外す | `Resources.Load` / dynamic object registry が quarantine へ落ちる |
| M5 | M5-5 | 5 | `RuntimeManagerMB` / `RuntimeLifetimeScopePool` / `RuntimeLifetimeScopeSpawnerService` を削除前提で `SceneKernel` の unified spawn core へ置換する | `SceneKernel` spawn boundary / host bridge / asset migration が成立し、parent-scoped pool key / same-parent-only reuse / hierarchy scan delete / old manager authority が target path から消える |
| M6 | M6-1 | 1 | service registration plan を作る | service input が table 化される |
| M6 | M6-2 | 2 | service slot creation を作る | `EntityRef + ServiceId` に対する slot が立つ |
| M6 | M6-3 | 3 | lifecycle plan hookup を作る | service が plan で起動・停止する |
| M6 | M6-4 | 4 | 代表 service を 1 本移す | 1 つの high-level service が new path で動く |
| M6 | M6-5 | 5 | 完成済みの 07 unified spawn / pool mediation を service runtime に接続する | service instance の生成 / 破棄が完成済みの `SceneKernel` spawn / pool mediation を経由する |
| M7 | M7-1 | 1 | `ValueStore` public contract を作る | `ValueKeyId` ベースの read/write contract が明確になる |
| M7 | M7-2 | 2 | `VarStore` backend を接続する | backend が runtime truth になる |
| M7 | M7-3 | 3 | `ValueInitPlan` を接続する | init が plan 経由に閉じる |
| M7 | M7-4 | 4 | `Blackboard` truth を切る | fallback blackboard が target truth でなくなる |
| M7 | M7-5 | 5 | `DynamicSource` surface を value runtime に閉じる | `DynamicValue` / `IDynamicSource` / `DynamicEvaluationRuntime` が new path で閉じる |
| M8 | M8-1 | 1 | command declaration input を作る | command catalog の source が明示される |
| M8 | M8-2 | 2 | verified executor table を作る | executor discovery をしない table が立つ |
| M8 | M8-3 | 3 | `CommandRunnerService` shell を作る | command runner が service として起動する |
| M8 | M8-4 | 4 | 既存 `CommandRunner` engine を暫定接続する | engine の再利用点が切り出される |
| M8 | M8-5 | 5 | legacy command host / authoring bridge を退避する | `CommandRunnerMB` による bulk registration host が target path から消える |
| M9 | M9-1 | 1 | scalar identity model を作る | scalar の owner と binding が明示される |
| M9 | M9-2 | 2 | scalar declaration input を作る | scalar が installer ではなく declaration になる |
| M9 | M9-3 | 3 | scalar runtime shell を作る | scalar が new path で起動する |
| M9 | M9-4 | 4 | ancestor fallback を消す | `BaseScalarService` 的 fallback が消える |
| M10 | M10-1 | 1 | UI graph plan / handle を作る | UI の node graph が explicit になる |
| M10 | M10-2 | 2 | modal / selection / navigation を接続する | UI の状態遷移が service-owned になる |
| M10 | M10-3 | 3 | button channel を移す | input bridge が new path へ行く |
| M10 | M10-4 | 4 | UI hot path を最適化する | bounded traversal / cached handle が入る |
| M11 | M11-1 | 1 | 残存 service / command / value / scalar / query / dynamic source / scene / prefab inventory を確定する | [ServiceCutoverInventory](Index/ServiceCutoverInventory.md)、[CommandCutoverInventory](Index/CommandCutoverInventory.md)、[ValueScalarQueryInventory](Index/ValueScalarQueryInventory.md)、[DynamicSourceCutoverInventory](Index/DynamicSourceCutoverInventory.md)、[ScenePrefabInventory](Index/ScenePrefabInventory.md) に完了までに差し替える対象と owner / bridge / asset anchor が一覧化される |
| M11 | M11-2 | 2 | service wave を全件消化する | target gameplay service が `IRuntimeResolver` / legacy registry / installer mutation なしで new path に載る |
| M11 | M11-3 | 3 | command / value / scalar / query / dynamic source の残存接続を全件切り替える | entity-fixed `CommandRunner` への切替と legacy command host / authoring bridge の退避が完了し、`BlackboardService` / old scalar fallback / runtime stable-key repair が target path から消える |
| M11 | M11-4 | 4 | existing scene / prefab を editor migration で新 host / declaration / identity へ付け替える | 対象 asset が少修正で field-preserving reserialize され、old runtime species 参照が target path から消える |
| M11 | M11-5 | 5 | shipped gameplay surface を通しで検証する | 主要 scene flow と prefab spawn path が new path のみで direct play できる |
| M12 | M12-1 | 1 | 残存 bridge / adapter / resolve usage を 0 ベースで再監査する | new-path runtime に参加する bridge が残っていない |
| M12 | M12-2 | 2 | dead path と旧 asset reference を物理削除する | legacy authority が source / asset / manifest から消える |
| M12 | M12-3 | 3 | forbidden pattern / compile / validation gate を通す | `Common/LTS` / hierarchy scan / resolver fallback / runtime search が target build に再侵入しない |
| M12 | M12-4 | 4 | existing scene / prefab を用いた end-to-end direct play を通す | 起動・scene transition・spawn/despawn・UI・command execution・shutdown が new path で成立する |
| M12 | M12-5 | 5 | diagnostics / regression / performance hardening を完了する | missing owner / service / command / value / scalar / route を structured failure で追え、性能 budget と regression gate が成立する |

## V2.1 完了定義

v2.1 の完了は、代表的な slice が動くことではない。
完了とは、現在のゲーム本体が compile 済みの build / editor direct play の双方で、既存 scene / prefab を少修正で維持したまま、`ApplicationKernel` / `SceneKernel` と new path の service / command / value / scalar / query / dynamic source / spawn runtime だけで起動・進行・終了する状態を指す。

M12 完了時に満たすべき条件:

- shipped gameplay surface に含まれる全 service / command / value / scalar / query / dynamic source / spawn route が new authority へ切り替わっている
- 既存 scene / prefab の runtime 参照が editor migration により new host / declaration / identity へ付け替わっている
- `IRuntimeResolver`、`IScopeNode`、`RuntimeManagerMB`、`RuntimeLifetimeScope*`、entity-fixed `CommandRunner`、`BlackboardService`、old scalar fallback、route bridge が new-path runtime authority に参加していない
- 一時 bridge が残る場合でも quarantine か diagnostics-only に閉じ、resolve / execute / spawn / value truth に参加しない
- 詳細な inventory / acceptance / verification gate は `08_FullReplacementCompletionSpec` に従う

### M1-M5 詳細サブフェーズ

M1 から M5 は `細分化フェーズ` の一部として、さらに詳細な粒度を持つ。
ここでは `M1.1` 形式で、各 milestone の中身をより具体化する。

| 親 milestone | sub-phase | 実装順 | 目的 | 出口条件 |
| --- | --- | ---: | --- | --- |
| M1 | M1.1 | 1 | new runtime 用の asmdef / namespace を切る | 旧 `Common/LTS` と新 runtime が同一 assembly にいない |
| M1 | M1.2 | 2 | bridge / quarantine assembly を分離する | legacy bridge が quarantine 側に寄る |
| M1 | M1.3 | 3 | compile-time kill switch を入れる | new path が LTS 非依存で build できる |
| M1 | M1.4 | 4 | new runtime entry の最小 shell を用意する | `ApplicationKernel` / `SceneKernel` の空 shell が build 可能 |
| M2 | M2.1 | 1 | `ApplicationKernel` DDOL shell を作る | boot / profile / shared service の入口がある |
| M2 | M2.2 | 2 | `SceneKernel` scene-local shell を作る | scene-local registry の入口がある |
| M2 | M2.3 | 3 | scene handoff / disposal contract を作る | scene transition の生成・破棄が明示される |
| M2 | M2.4 | 4 | V2 部品の配置先を固定する | V2 boot/runtime mapping が確定する |
| M2 | M2.5 | 5 | app-wide と scene-local の boundary を固定する | shared service と scene-local service が混線しない |
| M3 | M3.1 | 1 | `EntityIdentityMB` を実装する | `EntityRef` を持つ root bridge が存在する |
| M3 | M3.2 | 2 | declaration MB base contract を実装する | installer ではない input surface が存在する |
| M3 | M3.3 | 3 | provenance / trace を plan input に接続する | source metadata が verified plan に入る |
| M3 | M3.4 | 4 | 代表 MB を 1 つ declaration 化する | 1 本の実例が plan 経由で流れる |
| M3 | M3.5 | 5 | declaration MB から runtime mutation を排除する | `InstallFeature` 系の責務が残らない |
| M4 | M4.1 | 1 | `EntityRef` ベースの entity registration table を作る | entity ごとの slot 管理が始まり、resolve hot path の入口ができる |
| M4 | M4.2 | 2 | `Resolve(EntityRef, ServiceId)` を作る | resolve の唯一入口がある |
| M4 | M4.3 | 3 | lifecycle dispatch entry を作る | lifecycle が plan 駆動で回る |
| M4 | M4.4 | 4 | diagnostics / debug map entry を作る | missing / invalid / duplicate が structured failure になる |
| M4 | M4.5 | 5 | service slot と plan の対応を固定する | `EntityRef + ServiceId` が dense table 化される |
| M5 | M5.1 | 1 | `RuntimeLifetimeScopeBase` / `RuntimeManagerMB` / `RuntimeLifetimeScopePool` を target path から切る | old authority が new path に入らない |
| M5 | M5.2 | 2 | `IScopeNode` / `IRuntimeResolver` を外す | parent fallback と type scan が止まる |
| M5 | M5.3 | 3 | registry / hierarchy helper を外す | `BaseLifetimeScopeRegistry` / `ScopeNodeHierarchy` / `ScopeFeatureInstallerUtility` が authority でなくなる |
| M5 | M5.4 | 4 | dynamic registry / search path を外す | `Resources.Load` / dynamic object registry が quarantine へ落ちる |
| M5 | M5.5 | 5 | `RuntimeManagerMB` / `RuntimeLifetimeScopePool` を 07 の unified spawn core へ切り替える | parent-scoped pool key / same-parent-only reuse / hierarchy scan delete が target path から消える |
| M5 | M5.6 | 6 | remaining scope-kind branching を消す | service main logic に scope authority が残らない |

---

## マイルストーン詳細

### M0: Spec / Inventory Freeze

主目標:

- 00 から 04 の責務を確定する
- legacy authority 一覧を作る
- replacement target 一覧を作る

主要セクション順:

1. `Entity = Scope` の用語統一
2. `ApplicationKernel` / `SceneKernel` / `ServiceGraph` / `Lifecycle` / `ValueStore` / `CommandCatalog` / `Scalar` の責務再確認
3. legacy authority inventory 固定
4. preservation floor 固定

入口条件:

- v2.1 00 から 04 が存在すること

出口条件:

- legacy authority と replacement target の対応表が文書で確定している
- subsystem ごとの keep/remove 判断が確定している

禁止ショートカット:

- inventory を作らずに feature port を始めること

#### M0.1 〜 M0.3 実施結果

M0 では、以後の実装判断がぶれないように次の 3 点を確定する。

##### M0.1 Vocabulary Freeze

| 語彙 | 確定値 | 補足 |
| --- | --- | --- |
| `Entity` / `Scope` | 同義 | 文書内では `Entity(Scope)` と表記してもよい |
| `ApplicationKernel` | DDOL の game-wide root | 旧 `PlatformLTS` / `GlobalLTS` / `ProjectLTS` に近い |
| `SceneKernel` | scene-local root | 1 scene に 1 つ |
| `ServiceGraph` | verified service ownership / resolve layer | generic DI container ではない |
| `ValueStore` | explicit value storage subsystem | `Blackboard` の後継ではあるが同一ではない |
| `Scalar` | float-specialized subsystem | `ValueStore` と別 subsystem |
| `CommandCatalog` | command dispatch subsystem | `ServiceGraph` ではない |
| `RuntimeQuery` | explicit lookup subsystem | string / hierarchy discovery を使わない |

##### M0.2 Legacy Authority Inventory

| legacy authority | 残すか | 置換先 / 退避先 | 理由 |
| --- | --- | --- | --- |
| `RuntimeLifetimeScopeBase` | 削除 | `ApplicationKernel` + `SceneKernel` + `EntityIdentityMB` + `ServiceGraph` | build / resolver / lifecycle / hierarchy authority が混在する |
| `BaseLifetimeScope` | 削除 | 上記と同じ | scope authority の混在を残すため |
| `BaseLifetimeScopeRegistry` | 削除 | なし | registry を authority にしない |
| `ScopeNodeHierarchy` | 削除 | explicit entity / service graph へ置換 | hierarchy discovery を authority にしない |
| `IScopeNode` | 削除 | `EntityRef` + `ServiceGraph` + explicit contract | owner 推定を残すため |
| `IRuntimeResolver` | 削除 | `ServiceGraph` / `CommandCatalog` / `ValueStore` / generated accessors | type scan / parent fallback を持つため |
| `IFeatureInstaller` | 削除 | declaration MB + plan contribution | MB mutation を authority にしない |
| `ScopeFeatureInstallerUtility` | 削除 | declaration extraction | `GetComponentsInChildren` に依存する |
| `LTSIdentityMB` | 削除 | `EntityIdentityMB` | hierarchy-derived kind を持つ |
| `RuntimeResolverHub` | quarantine / 削除 | `ServiceGraph` | resolver hub を残さない |
| `DynamicObjectRegistryService` | quarantine / 削除 | explicit registry / manifest | runtime asset discovery を残す |
| `DynamicObjectRegistryMB` | quarantine / 削除 | explicit registry / manifest | search-based repair を残す |
| `CommandRunnerMB` | 削除 | entity-fixed `CommandRunner` + `CommandCatalog` + successor Authoring | bulk registration host を残さない |
| `BlackboardMB` / `BlackboardService` | 削除 | `ValueStore` + `VarStore` backend | hierarchical fallback を残す |
| `VarKeyRegistryLocator` / `VarIdResolver` | 削除 | generated `ValueKeyId` mapping | runtime stable-key fallback を残す |
| `BaseScalarService` | 削除 | scalar runtime subsystem | ancestor fallback を残す |

##### M0.3 Replacement Target Inventory

| target | owner | runtime role | 備考 |
| --- | --- | --- | --- |
| `ApplicationKernel` | DDOL root | boot / profile / shared service / scene handoff | game-wide authority |
| `SceneKernel` | scene-local root | entity registration / resolve / lifecycle / diagnostics | scene-local authority |
| `EntityIdentityMB` | `EntityRef` bridge | ownership unit の宣言 | `LTSIdentityMB` の完全置換 |
| declaration MB | `SceneKernel` / plan input | service / value / command / UI の宣言 | runtime mutation をしない |
| entity-scoped `ServiceGraph` | `SceneKernel` | `EntityRef + ServiceId` resolve | 1 entity に複数 service を載せる |
| `LifecyclePlan` | verified plan | acquire / release / tick dispatch | scan ではなく plan 駆動 |
| `ValueStore` | value subsystem | `ValueKeyId` storage | `VarStore` backend を使う |
| `CommandCatalog` | command subsystem | `CommandTypeId` dispatch | executor discovery をしない |
| `Scalar` runtime | scalar subsystem | float modulation | `ValueStore` と分離する |
| `RuntimeQuery` | explicit lookup subsystem | query identity lookup | UI / actor / neighbor search の置換先 |
| UI service-owned graph | UI subsystem | hierarchy / selection / navigation / modal | Transform authority を使わない |

##### M0.3 Preservation Floor

以下は、M0 で残してよいものとして明示する。

- service の domain logic
- `VarStore` backend data structure
- `CommandRunner` の execution semantics の一部
- scalar modulation logic の一部
- UI の state machine / channel logic

以下は、M0 の時点で target path に載せない。

- resolver / installer / registry / hierarchy authority
- parent fallback
- runtime stable-key fallback
- `Resources.Load` fallback
- `IFeatureInstaller` ベースの runtime mutation

---

### M1: Compile Boundary / Kill Switch

主目標:

- new runtime が `Common/LTS` に直接依存しない足場を作る
- bridge を quarantine へ押し込む準備をする

主要セクション順:

1. target asmdef / namespace の切り出し
2. `ApplicationKernel` / `SceneKernel` family の置き場所決定
3. legacy bridge の仮置き場を分離
4. compile error なしで new path を build できる最小構成へする

入口条件:

- M0 完了

出口条件:

- new runtime code が `Common/LTS` 直参照なしでコンパイルできる
- `EntityIdentityMB` / declaration MB 用の置き場ができている

禁止ショートカット:

- bridge と core runtime を同じ assembly に置くこと
- old/new の依存方向を曖昧にすること

#### M1.1 〜 M1.4 実施結果

M1 では、compile boundary と shell 配置を次で固定する。

| sub-phase | 実施内容 | 固定結果 |
| --- | --- | --- |
| `M1.1` | target asmdef / namespace 切り出し | `GameLib.Kernel.Layers.Core` を新設し、root namespace を `Game.Kernel.Layers` に固定する |
| `M1.2` | bridge / quarantine assembly 分離 | `GameLib.Kernel.Layers.Quarantine` を新設し、legacy adapter はここに閉じ込める。`autoReferenced = false` を必須にする |
| `M1.3` | build-only kill switch | core asmdef は `GameLib.Kernel.Abstractions` 以外の legacy 依存を持たず、`Common/LTS` 直参照を禁止する |
| `M1.4` | new runtime entry の最小 shell | `ApplicationKernel` / `SceneKernel` の no-engine shell を `Game.Kernel.Layers` に置く |

M1 時点での新規アンカー:

- [GameLib.Kernel.Layers.Core.asmdef](C:/Users/macar/TinnosukeGameLib/Assets/GameLib/Script/Kernel/Layers/Core/GameLib.Kernel.Layers.Core.asmdef)
- [GameLib.Kernel.Layers.Quarantine.asmdef](C:/Users/macar/TinnosukeGameLib/Assets/GameLib/Script/Kernel/Layers/Quarantine/GameLib.Kernel.Layers.Quarantine.asmdef)
- [ApplicationKernel.cs](C:/Users/macar/TinnosukeGameLib/Assets/GameLib/Script/Kernel/Layers/Core/ApplicationKernel.cs)
- [SceneKernel.cs](C:/Users/macar/TinnosukeGameLib/Assets/GameLib/Script/Kernel/Layers/Core/SceneKernel.cs)
- [KernelLayerCompileBoundary.cs](C:/Users/macar/TinnosukeGameLib/Assets/GameLib/Script/Kernel/Layers/Core/KernelLayerCompileBoundary.cs)
- [KernelLayerQuarantineAssemblyAnchor.cs](C:/Users/macar/TinnosukeGameLib/Assets/GameLib/Script/Kernel/Layers/Quarantine/KernelLayerQuarantineAssemblyAnchor.cs)

---

### M2: Kernel Layer Composition

主目標:

- `ApplicationKernel` と `SceneKernel` の 2 層 root を作る
- DDOL の game-wide authority と scene-local authority の handoff を明示化する
- V2 の boot/runtime 部品をどちらの kernel に属するか決める

主要セクション順:

1. `ApplicationKernel` shell
2. `SceneKernel` shell
3. scene handoff / disposal path
4. V2 component mapping の確定

入口条件:

- M1 完了

出口条件:

- DDOL root と scene-local root が独立して存在する
- `ApplicationKernel` から `SceneKernel` への handoff が明示される
- 旧 `LTS` authority を kernel pair に残さない

禁止ショートカット:

- entity-scoped `ServiceGraph` を kernel pair がないまま先に立てること
- app-wide boot / scene-local registration を同じ authority に戻すこと

#### M2.1 〜 M2.3 実施結果

M2 の前半では、kernel pair の host と handoff を次で固定する。

| sub-phase | 実施内容 | 固定結果 |
| --- | --- | --- |
| `M2.1` | `ApplicationKernel` DDOL shell | `ApplicationKernelHostMB` を追加し、DDOL root と current scene owner を持つ |
| `M2.2` | `SceneKernel` scene-local shell | `SceneKernelHostMB` を追加し、scene root に 1 つの scene-local host を置く |
| `M2.3` | scene handoff / disposal contract | `SceneManager.sceneLoaded` / `activeSceneChanged` / `sceneUnloaded` を使って attach / detach を明示化する |

M2.1 〜 M2.3 時点での新規アンカー:

- [GameLib.Kernel.Layers.Unity.asmdef](/C:/Users/macar/TinnosukeGameLib/Assets/GameLib/Script/Kernel/Layers/Unity/GameLib.Kernel.Layers.Unity.asmdef)
- [ApplicationKernelHostMB.cs](/C:/Users/macar/TinnosukeGameLib/Assets/GameLib/Script/Kernel/Layers/Unity/ApplicationKernelHostMB.cs)
- [SceneKernelHostMB.cs](/C:/Users/macar/TinnosukeGameLib/Assets/GameLib/Script/Kernel/Layers/Unity/SceneKernelHostMB.cs)

#### M2.4 〜 M2.5 実施結果

M2 の後半では、V2 concrete type の差し込み口と kernel pair の boundary を次で固定する。

| sub-phase | 実施内容 | 固定結果 |
| --- | --- | --- |
| `M2.4` | V2 部品の配置先を実コードで固定 | `GameLib.Kernel.Layers.Composition` を追加し、`KernelBootBoundary` / `KernelBootRuntimeSurfaceFactory` / `KernelDiagnosticService` を app-wide、`KernelRuntimeServiceGraph` / `KernelRuntimeScopeGraph` / `KernelLifecycleDispatcher` を scene-local boundary として mapping した |
| `M2.5` | app-wide / scene-local boundary を public contract に固定 | `ApplicationKernel` は application composition boundary、`SceneKernel` は scene composition boundary を持ち、scene 側は owner 経由で app boundary を参照するだけに制限した |

M2.4 〜 M2.5 時点での新規アンカー:

- [KernelLayerCompositionContracts.cs](/C:/Users/macar/TinnosukeGameLib/Assets/GameLib/Script/Kernel/Layers/Core/KernelLayerCompositionContracts.cs)
- [ApplicationKernel.cs](/C:/Users/macar/TinnosukeGameLib/Assets/GameLib/Script/Kernel/Layers/Core/ApplicationKernel.cs)
- [SceneKernel.cs](/C:/Users/macar/TinnosukeGameLib/Assets/GameLib/Script/Kernel/Layers/Core/SceneKernel.cs)
- [GameLib.Kernel.Layers.Composition.asmdef](/C:/Users/macar/TinnosukeGameLib/Assets/GameLib/Script/Kernel/Layers/Composition/GameLib.Kernel.Layers.Composition.asmdef)
- [KernelComponentPlacementCatalog.cs](/C:/Users/macar/TinnosukeGameLib/Assets/GameLib/Script/Kernel/Layers/Composition/KernelComponentPlacementCatalog.cs)
- [ApplicationKernelBootBoundaryAdapter.cs](/C:/Users/macar/TinnosukeGameLib/Assets/GameLib/Script/Kernel/Layers/Composition/ApplicationKernelBootBoundaryAdapter.cs)
- [ApplicationKernelComposition.cs](/C:/Users/macar/TinnosukeGameLib/Assets/GameLib/Script/Kernel/Layers/Composition/ApplicationKernelComposition.cs)
- [SceneKernelComposition.cs](/C:/Users/macar/TinnosukeGameLib/Assets/GameLib/Script/Kernel/Layers/Composition/SceneKernelComposition.cs)

---

### M3: Entity Identity / Declaration

主目標:

- `LTSIdentityMB` を replacement path から外し、`EntityIdentityMB` を導入する
- service MB を declaration MB に変える

主要セクション順:

1. `EntityIdentityMB` 実装
2. declaration MB の base contract 実装
3. authoring trace と source metadata 接続
4. representative MB を 1 つ declaration 化

入口条件:

- M2 完了

出口条件:

- 新規 authoring が `IFeatureInstaller` を実装しない
- `EntityRef` を明示した root が存在する

禁止ショートカット:

- declaration MB の中で runtime resolve を始めること
- `LTSIdentityMB` を継承して済ませること

---

### M4: SceneKernel Skeleton

主目標:

- `SceneKernel`、`EntityRef` registration table、entity-scoped `ServiceGraph`、lifecycle entry の最小骨格を作る

主要セクション順:

1. `SceneKernel` shell
2. entity registration table
3. `Resolve(EntityRef, ServiceId)` / `TryResolve(...)`
4. minimal lifecycle dispatch entry
5. diagnostics entry

入口条件:

- M3 完了

出口条件:

- `EntityRef + ServiceId` で 1 つの service を解決できる
- service owner が `SceneKernel` 側に移る入口がある
- `EntityRef` ルックアップは scan なしの hot path である

禁止ショートカット:

- `SceneKernel` を DI container にすること
- `GameObject` / `Transform` を resolve key にすること

---

### M5: Legacy Authority Shutdown

主目標:

- old authority を target path から外す
- 旧 registry / hierarchy / resolver / spawn authority を replacement path へ入れない

主要セクション順:

1. `RuntimeLifetimeScopeBase` / `RuntimeManagerMB` / `RuntimeLifetimeScopePool` を target path から切る
2. `SceneKernel` に 07 の unified spawn boundary を追加し、host / declaration bridge から scene-local spawn settings を渡せるようにする
3. 07 で定義する unified spawn core へ authority を切り替え、pool key と delete mediation を prefab-family / mediator 前提へ揃える
4. scene / prefab を新 host / declaration へ自動移行し、旧 manager 参照を除去する
5. `IScopeNode` / `IRuntimeResolver` public dependency を new path から外す
6. `BaseLifetimeScopeRegistry` を target path から外す
7. `ScopeNodeHierarchy` を target path から外す
8. `DynamicObjectRegistryService` / `DynamicObjectRegistryMB` を target truth から外し、残存する scope-kind branching を畳む

対象アンカー:

- [RuntimeLifetimeScope.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs)
- [RuntimeManagerMB.cs](../../GameLib/Script/Project/Scene/Runtime/RuntimeManager/RuntimeManagerMB.cs)
- [RuntimeLifetimeScopePool.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScopePool.cs)
- [BaseLifetimeScopeRegistry.cs](../../GameLib/Script/Common/LTS/Registry/BaseLifetimeScopeRegistry.cs)
- [ScopeNodeHierarchy.cs](../../GameLib/Script/Common/LTS/Core/ScopeNodeHierarchy.cs)
- [DynamicObjectRegistryService.cs](../../GameLib/Script/Common/Search/Core/DynamicObjectRegistryService.cs)
- [DynamicObjectRegistryMB.cs](../../GameLib/Script/Project/Scene/Entity/Search/DynamicObjectRegistryMB.cs)

入口条件:

- M4 完了

出口条件:

- runtime spawn / pool authority が `SceneKernel` 側に閉じている
- replacement path が `IScopeNode` / resolver / registry / hierarchy helper / direct destroy を authority としない
- temporary bridge があっても truth は `SceneKernel` 側にある
- `RuntimeManagerMB` / `RuntimeLifetimeScopeSpawnerService` / old runtime species の scene-local authority が asset migration 後に削除されている
- `SceneSpawnerRegistryBridge` と inner legacy registry は quarantine でのみ存在してよく、new-path resolution の truth source にはならない

禁止ショートカット:

- old authority を残したまま new service port を増やすこと
- dynamic registry を query 代用品にすること
- old manager / old pool を adapter 名義で延命し、truth source を残すこと

---

### M6: Instance-Service Runtime

主目標:

- type 1 service を `SceneKernel` の entity-scoped `ServiceGraph` へ載せる
- lifecycle と registration の standard path を確立する
- 07 で定義した unified spawn / pool mediation を service runtime に接続する

主要セクション順:

1. service registration plan
2. service slot creation
3. lifecycle plan hookup
4. 07 の unified spawn / pool mediation を service runtime に接続する
5. 代表 service 1 本の移行

代表候補:

- `AnimationSpriteHub`
- `ButtonChannelHubService` の簡易版

入口条件:

- M5 完了
- 07 の SceneKernel-owned unified spawn core と asset migration が完了している

出口条件:

- declaration MB -> plan -> `SceneKernel` -> service instance の 1 本線が成立する
- service instance の生成 / 破棄が「完成済みの」`SceneKernel` spawn / pool mediation を経由する
- `IFeatureInstaller` なしで type 1 service を動かせる

禁止ショートカット:

- service ごとに新しい DI scope を作ること
- lifecycle target を interface scan で集めること

---

### M7: Value Runtime with VarStore Backend

主目標:

- `ValueStore` public contract を立てる
- backend に `VarStore` を採用する
- `DynamicSource` / `DynamicValue` authoring surface を value runtime に閉じる
- `Blackboard` truth を切る

主要セクション順:

1. `ValueStore` public contract 定義
2. `VarStore` adapter / backend 実装
3. `ValueInitPlan` 接続
4. `DynamicSource` / `DynamicValue` authoring surface の explicit declaration 接続
5. `BlackboardMB` runtime owner 削除と payload declaration 抽出
6. `BlackboardService` fallback path の停止
7. `VarIdResolver` / `VarKeyRegistryLocator` の target path 排除

runtime layout rule:

- entity-local value runtime は per-entity MB や per-entity heavyweight service proliferation にしない
- `ValueStore` は AoS 互換の compact record 群、または同等の scalable runtime layout に乗せる
- per-entity object が残る場合でも、truth storage owner は manager-owned runtime data である

入口条件:

- M6 完了

出口条件:

- new value access が `ValueKeyId` 経由で動く
- `BlackboardService` が target truth ではなくなる
- backend としての `VarStore` だけが残る
- entity-local value runtime が per-entity MB / heavyweight service 常設に依存しない

禁止ショートカット:

- `IBlackboardService` を facade として残し続けること
- stable-key lookup を public runtime contract に残すこと
- entity 数に比例して `ValueStore` service class / runtime owner object を無制限に増殖させること

---

### M8: Entity-Fixed Command Runtime Transition

主目標:

- `CommandRunner` をすべての Entity に固定配置する
- `CommandRunnerMB` を target path の前提にしない
- `CommandCatalog` + `CommandRunnerService` 的な path を立てる

主要セクション順:

1. command declaration input
2. verified executor table
3. `CommandRunnerService` shell と entity-fixed `CommandRunner` ownership
4. existing `CommandRunner` engine の暫定接続
5. profile / payload / context 接続
6. legacy command host / authoring bridge の退避

入口条件:

- M7 完了

出口条件:

- entity-fixed `CommandRunner` が Entity ごとに存在し、`CommandRunnerMB` なしで command 実行できる
- executor discovery が不要
- command truth が `CommandCatalog` 側にある

禁止ショートカット:

- `IReadOnlyList<ICommandExecutor>` を再び集めること
- `LifetimeScopeKind` 分岐で runner を増やすこと
- `Resources.Load` catalog fallback を残すこと

注意:

- `UniTask` は phase 1 では残り得る
- ただし profiler 監視を必須とし、M12 まで無制限に温存しない

---

### M9: Scalar Runtime Transition

主目標:

- scalar identity / binding / inherited access を新 path へ載せる
- `BaseScalarService` architecture を切る

主要セクション順:

1. scalar identity model
2. scalar declaration input
3. scalar runtime shell
4. binding endpoint の explicit 化
5. timed update の lifecycle 接続
6. ancestor fallback の除去

入口条件:

- M8 完了

出口条件:

- scalar read/write が `IScopeNode.Parent` を使わない
- required read が silent zero に落ちない
- binding が registry search に依存しない

禁止ショートカット:

- `BaseScalarService` を facade として残すこと
- string/hash key を runtime truth にすること

---

### M10: UI Core Migration

主目標:

- UI hierarchy を service-owned graph として構築する
- selection / navigation / modal / button channel を new path へ移す

主要セクション順:

1. `UINodeHandle` / UI graph plan
2. `ModalStackChannelHubService`
3. `UISelectionService`
4. `UINavigationService`
5. `ButtonChannelHubService`
6. command / value / scalar との接続整理

入口条件:

- M9 完了

出口条件:

- UI core が `Transform.parent` authority なしで動く
- `IScopeNode` なしで selection/navigation/modal が動く
- UI hot path が dense handle / bounded traversal で動く

禁止ショートカット:

- UI hierarchy 再構築を every-frame で行うこと
- neighbor resolve を transform search に戻すこと

---

### M11: Full Feature / Asset Cutover

主目標:

- shipped gameplay surface を構成する全 service を new path へ移す
- command / value / scalar / query / spawn route の残存 legacy authority を全件置換する
- existing scene / prefab を editor migration で新 host / declaration / identity へ付け替える
- internal wiring だけを差し替え、preservation floor を守る
- service surface の exhaustive inventory は [09_ServiceCutoverMilestoneSpec.md](09_ServiceCutoverMilestoneSpec.md) で固定する

主要セクション順:

1. 残存 inventory freeze
2. service wave completion
3. command / value / scalar / query closure
4. scene / prefab asset migration
5. real gameplay flow proof

wave の固定ルール:

- wave ごとに残存 inventory を減らし、代表例だけで終わらせない
- owner、bridge owner、asset anchor、検証 scene を 1 単位ごとに紐づける
- scene / prefab rewrite を同 wave の出口条件に含める
- unresolved dependency は temporary resolver ではなく declaration / plan / explicit contract に戻す

入口条件:

- M10 完了
- `08_FullReplacementCompletionSpec` の inventory baseline が作成済み
- `09_ServiceCutoverMilestoneSpec` の inventory baseline が作成済み

出口条件:

- shipped gameplay surface に含まれる service / command / value / scalar / query / spawn route の残存 inventory が migration-only quarantine を除いて 0 になる
- 既存 scene / prefab が少修正の editor migration で new host / declaration / identity へ付け替わり、old runtime species 参照が target path から消える
- target gameplay flow で `IRuntimeResolver`、`IScopeNode`、entity-fixed `CommandRunner`、`BlackboardService`、`RuntimeManagerMB`、`RuntimeLifetimeScope*` が不要になる
- representative service ではなく、実際の scene flow / prefab spawn / UI / command 実行の通し検証が成立する

禁止ショートカット:

- 代表 service 群だけで M11 完了を宣言すること
- scene / prefab migration を M12 へ先送りすること
- feature ごとに独自 bridge を量産すること
- unresolved dependency を temporary resolver や hidden adapter で吸収すること

---

### M12: Full Replacement Verification / Legacy Purge

主目標:

- コンパイル済みゲーム全体が新アーキテクチャのみで動くことを確認する
- bridge と legacy code / asset reference を物理削除する
- diagnostics / regression / performance gate を release 条件として固定する

主要セクション順:

1. residual bridge zero check
2. dead path と旧 asset reference の物理削除
3. forbidden pattern / compile / validation gate
4. existing scene / prefab end-to-end direct play verification
5. regression / diagnostics / performance hardening

物理削除候補:

- `RuntimeLifetimeScopeBase`
- `RuntimeManagerMB`
- `RuntimeLifetimeScopePool`
- `BaseLifetimeScope`
- `BaseLifetimeScopeRegistry`
- `ScopeNodeHierarchy`
- `ScopeFeatureInstallerUtility`
- `LTSIdentityMB`
- `CommandRunnerMB`
- `BlackboardService`
- `VarIdResolver`
- `VarKeyRegistryLocator`
- old scalar installer / fallback path
- `SceneSpawnerRegistryBridge`
- new-path runtime に参加している route / resolver / adapter bridge

入口条件:

- M11 完了
- 既存 scene / prefab migration report に unresolved item がない

出口条件:

- コンパイル済みゲームが existing scene / prefab を使って `ApplicationKernel` / `SceneKernel` + new subsystems のみで起動・進行・終了する
- 全 service / command / value / scalar / query / spawn route が new authority で resolve / execute / mutate される
- legacy authority が source / asset / manifest / serialized reference から削除されるか diagnostics-only quarantine に退く
- forbidden pattern scan、direct play、regression、diagnostics、performance gate が通る
- `SceneSpawnerRegistryBridge` / route adapter は削除されるか diagnostics-only へ退き、runtime authority に参加している残存 bridge は完了条件違反である

禁止ショートカット:

- runtime authority に参加する bridge を残したまま M12 完了を宣言すること
- 旧 component を scene / prefab に残したまま「たまたま動く」状態で終えること
- special test scene だけの成功で game-wide cutover を主張すること
- 残存 asset rewrite や command / service 移植を post-M12 backlog に送ること

---

## セクション別の実装優先順位

実装者が迷いやすい主要セクションの優先順位を固定する。

### A. Foundation

1. `ApplicationKernel`
2. `SceneKernel`
3. `EntityIdentityMB`
4. declaration MB
5. entity-scoped `ServiceGraph`
6. `Lifecycle` entry

### B. Legacy teardown

1. `RuntimeLifetimeScopeBase` / `RuntimeManagerMB` / `RuntimeLifetimeScopePool`
2. `IScopeNode` / `IRuntimeResolver`
3. `BaseLifetimeScopeRegistry`
4. `ScopeNodeHierarchy`
5. dynamic object registry / legacy hierarchy search

### C. Data and execution

1. `ValueStore` public contract
2. `VarStore` backend
3. `CommandCatalog`
4. `CommandRunnerService`
5. `Scalar` runtime

### D. UI core

1. modal
2. selection
3. navigation
4. button channel

### E. Feature port

1. low-risk hub service
2. UI-adjacent service
3. animation / transform / material service
4. gameplay coordinator

この順を崩すと、後ろのセクションが前の未整備を temporary workaround で埋めやすくなる。

---

## 禁止シーケンス

次の順序は禁止する。

- `BlackboardService` を残したまま `ValueStore` を feature migration の truth にすること
- command authority と authoring bridge を同一 host にまとめること
- `BaseScalarService` を残したまま scalar identity だけ差し替えること
- UI migration を先に始めて `SceneKernel` / `Lifecycle` を後回しにすること
- `BaseLifetimeScopeRegistry` や `ScopeNodeHierarchy` を query 代用品として残すこと
- `RuntimeManagerMB` / `RuntimeLifetimeScopePool` を parent-scoped pool authority として残すこと
- hierarchy scan delete を runtime delete の標準経路として残すこと

---

## 完了判定

05 の成功条件は、順序が「もっともらしく見える」ことではない。
実装者が次に何をすべきか、何を後回しにしてはいけないか、どこで old truth を切るべきかが明確であることが成功条件である。

---

## 受け入れ基準

- LTS / registry / hierarchy authority を先に外す順序が定義されている
- `VarStore` backend と `Blackboard` 廃止の順序が定義されている
- entity-fixed `CommandRunner` と legacy command host / authoring bridge の切替順が定義されている
- `Scalar` の再構成が command/value の後、UI の前に置かれている
- UI core migration が独立マイルストーンとして定義されている
- feature port より前に foundation / teardown / data-execution がある
- 物理削除は authority 切替後に行うと明記されている
- runtime spawn / pool authority の切替順と M6 の 07 接続が整合している

---

## テストケース

| テストケース | 目的 | 検証 |
| --- | --- | --- |
| `TC-V21-05-01` | old authority の切替順が定義されていることを確認する | `RuntimeLifetimeScopeBase`、`IScopeNode`、registry、hierarchy helper を M5 で target path から外すと書かれていなければならない |
| `TC-V21-05-02` | `VarStore` / `DynamicSource` / `Blackboard` の順序が定義されていることを確認する | `ValueStore` public contract -> `VarStore` backend -> `DynamicSource` cutover -> `Blackboard` truth cutover の順が書かれていなければならない |
| `TC-V21-05-03` | `CommandRunner` の扱いが具体的であることを確認する | entity-fixed `CommandRunner`、`CommandRunnerService`、engine 暫定流用、discovery 禁止、`CommandRunnerMB` 非依存が書かれていなければならない |
| `TC-V21-05-04` | `Scalar` の扱いが具体的であることを確認する | scalar identity / binding / lifecycle の再構成が `BaseScalarService` fallback 除去とセットで書かれていなければならない |
| `TC-V21-05-05` | UI core migration が command/value/scalar の後にあることを確認する | UI graph、selection、navigation、modal、button channel が M10 に置かれていなければならない |
| `TC-V21-05-06` | feature port の前に foundation と teardown があることを確認する | M11 より前に M1 から M10 が存在し、それぞれ出口条件を持っていなければならない |
| `TC-V21-05-07` | spawn / pool authority の切替順が定義されていることを確認する | `RuntimeLifetimeScopeBase`、`RuntimeManagerMB`、`RuntimeLifetimeScopePool`、parent-scoped pool key、hierarchy scan delete を M5 で target path から外すと書かれていなければならない |
| `TC-V21-05-08` | M6 が 07 の unified spawn / pool mediation を前提にしていることを確認する | service instance の生成 / 破棄が `SceneKernel` の spawn / pool mediation を経由すると書かれていなければならない |
| `TC-V21-05-09` | dynamic source cutover が M7 に置かれていることを確認する | `DynamicSource` / `DynamicValue` authoring surface と `IDynamicSource` / `DynamicEvaluationRuntime` が M7-5 の出口条件にあると書かれていなければならない |

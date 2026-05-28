# Kernel v2.1 完全差し替え完了仕様

## 文書ステータス

- 文書 ID: `08_FullReplacementCompletionSpec`
- 状態: Draft
- 役割: v2.1 の完了条件を、全 service / command / dynamic source / asset cutover と実ゲーム検証まで含めて固定する
- 範囲: residual inventory 管理、service / command / value / scalar / query / dynamic source / spawn cutover、scene / prefab migration、bridge closure、verification gate
- 非目標: v2 target kernel の意味論の再定義、個別 gameplay mechanic の設計、legacy compatibility の延命

### 改訂メモ

v2.1 の完了は「代表的な slice が新 path で動くこと」ではない。
完了は、現行ゲーム全体が既存 scene / prefab を基準として、新アーキテクチャのみで compile / boot / play / transition / shutdown できる状態を指す。

---

## 1. 完了定義

v2.1 完了時は、次を同時に満たす。

- compile 済みのゲーム本体が `ApplicationKernel` / `SceneKernel` と new subsystems だけで起動・進行・終了する
- shipped gameplay surface に含まれる全 service / command / value / scalar / query / dynamic source / spawn route が new authority へ切り替わっている
- existing scene / prefab が editor migration により new host / declaration / identity へ移行済みであり、少修正の範囲を超える asset 手作業を要求しない
- legacy bridge は runtime authority から外れ、残る場合でも quarantine か diagnostics-only に閉じる
- legacy source、legacy asset reference、legacy manifest entry、legacy serialized reference の purge が完了している

完了ではない状態:

- 代表 service や代表 scene だけが新 path で動く
- `CommandRunnerMB`、`BlackboardService`、`RuntimeManagerMB`、`RuntimeLifetimeScope*`、`IRuntimeResolver`、`IScopeNode` が target gameplay flow に残る
- service / command / value / scalar / query / dynamic source inventory に未移行 item が残る
- scene / prefab の旧 component を残したまま、temporary bridge で挙動を成立させる
- special test scene の成功だけで game-wide cutover を主張する

---

## 2. Inventory 管理単位

完了判定は、以下の inventory を 0 まで減らすことで行う。

| Inventory | Canonical Doc | 完了単位 | 必須メタデータ | 完了証跡 |
| --- | --- | --- | --- | --- |
| service inventory | [ServiceCutoverInventory](Index/ServiceCutoverInventory.md) | 1 service type または 1 owner-bound service contract | owner、`ServiceId`、依存 command/value/query、旧 authority、対象 scene / prefab | new path registration、legacy dependency removal、real-scene verification |
| command inventory | [CommandCutoverInventory](Index/CommandCutoverInventory.md) | 1 command type | `CommandTypeId`、executor owner、payload schema、旧 bootstrap、呼び出し元 scene / UI | `CommandCatalog` dispatch、executor registration closure、実行検証 |
| value / scalar / query inventory | [ValueScalarQueryInventory](Index/ValueScalarQueryInventory.md) | 1 boundary または 1 runtime contract | owner、key/id、旧 fallback、依存 service | explicit boundary への移行、fallback removal、diagnostics verification |
| dynamic source inventory | [DynamicSourceCutoverInventory](Index/DynamicSourceCutoverInventory.md) | 1 dynamic source surface または 1 owner-bound dynamic source contract | owner、`IDynamicSource`、`DynamicValue` / editor surface、旧 authority、依存 value / expression / Flow entry | verified runtime / authoring path への移行、legacy authority removal、editor / Flow verification |
| scene inventory | [ScenePrefabInventory](Index/ScenePrefabInventory.md) | 1 shipped scene | 新 host 配置、旧 component 参照、必要 migration tool、検証 flow | reserialize 済み asset、direct play proof、旧参照 0 |
| prefab inventory | [ScenePrefabInventory](Index/ScenePrefabInventory.md) | 1 runtime prefab family | `EntityIdentityMB`、declaration、spawn route、旧 species 参照 | prefab migration 完了、spawn / release proof、旧 species 0 |
| bridge / adapter inventory | - | 1 compatibility surface | owner、authority 参加有無、quarantine 位置、削除条件 | M12 時点で diagnostics-only か物理削除 |

inventory は単なる一覧ではなく、M11 / M12 の出口条件そのものとして扱う。
未分類の service、command、value / scalar / query、dynamic source、scene、prefab、bridge が 1 件でも残る場合、完了宣言は無効である。

---

## 3. 差し替えルール

### 3.1 Service

- 再利用してよいのは service main logic だけである
- registration、resolver access、installer mutation、hierarchy fallback、search-based repair は再利用対象ではない
- service ごとに explicit owner、`EntityRef`、`ServiceId`、lifecycle entry を持つこと
- 旧 service が command / value / query に依存する場合、その接続も同 wave で new authority へ閉じること

### 3.2 Command

- 全 command は `CommandCatalog` と `CommandRunnerService` 経由で実行されなければならない
- `CommandRunner` engine の暫定流用は許可するが、bootstrap、executor discovery、bulk registration、runtime stable-key resolve は完了条件に含めない
- command executor は service discovery で見つけず、verified table から解決すること

### 3.3 Value / Scalar / Query

- value は `ValueStore` boundary へ閉じること
- scalar は scalar subsystem に閉じ、ancestor fallback を残さないこと
- query は `RuntimeQuery` の explicit contract を通すこと
- `IScopeNode.Parent`、root auto-select、runtime stable-key repair を target path に残さないこと

### 3.4 Scene / Prefab

既存 asset に対する許可変更は、少修正の原則に従う。

許可:

- new host / declaration / identity component の配置
- serialized data の copy / rewrite
- scene / prefab 内参照の付け替え
- reserialize 後の旧 component 削除
- command / authoring 系は旧 MB の serialized field data を successor Authoring に移してから component swap する

禁止:

- scene ごとに別の temporary runtime installer を足すこと
- old component を authority のまま残して「移行済み」と扱うこと
- gameplay logic の意味を変える asset 手修正を completion の前提にすること
- editor migration を通さず、手作業差分を常態化させること

### 3.5 Bridge

- bridge は migration-only とする
- bridge は quarantine に閉じる
- bridge が resolve / execute / spawn / value truth / route authority に参加するのは M11 までの一時状態に限る
- M12 完了時には diagnostics-only へ退くか、物理削除されていなければならない

---

## 4. M11 / M12 の所有範囲

| Milestone | 所有するもの | 必須成果物 | 完了条件 |
| --- | --- | --- | --- |
| M11 | 全 service / command / value / scalar / query / dynamic source / scene / prefab inventory の cutover | inventory baseline、wave 計画、editor migration tool、migrated asset 群、real gameplay proof bundle | 残存 inventory が migration-only quarantine を除いて 0 になり、existing scene / prefab が少修正で new path へ付け替わる |
| M12 | full-game verification、authoring-preserving legacy purge、release gate 固定 | purge manifest、forbidden pattern scan、compile / validation report、real-scene end-to-end verification、diagnostics / performance report | コンパイル済みゲーム全体が new path のみで動き、legacy runtime dependency が 0 になり、command / authoring field data が successor に保持される |

M11 は「代表例を移植する段階」ではなく、全対象を切り替え終える段階である。
M12 は「最後に少し掃除する段階」ではなく、full-game verification と legacy purge を同時に閉じる release gate である。

---

## 5. 検証ゲート

### 5.1 Compile / Static Gate

- target runtime assembly が `Common/LTS` へ依存しない
- `IRuntimeResolver`、`IScopeNode`、hierarchy scan、runtime search-based repair が target path へ再侵入していない
- forbidden pattern scan が quarantine の外で legacy authority を検出しない

### 5.2 Asset Gate

- shipped scene / prefab に `RuntimeManagerMB`、`RuntimeLifetimeScope*`、`CommandRunnerMB`、`BlackboardMB`、legacy scalar installer、new-path runtime に参加する bridge が残っていない
- command / authoring fields は successor Authoring に field-preserving migration 済みである
- `EntityIdentityMB` と declaration による replacement host が揃っている
- asset migration report が unresolved item 0 を示す

### 5.3 Runtime Gate

少なくとも以下を existing scene / prefab で通す。

1. boot
2. scene transition
3. spawn / release / delete mediation
4. UI interaction
5. command execution
6. value / scalar / query / dynamic source access
7. shutdown

いずれか 1 つでも legacy authority を経由する場合、完了ではない。

### 5.4 Diagnostics / Performance Gate

- missing owner / service / command / value / scalar / route が structured failure になる
- bridge usage、fallback usage、asset migration failure が追跡できる
- hot path が performance budget を満たす
- regression gate が full-game verification を継続的に監視できる

---

## 6. 成果物

完了宣言の前に、最低限次を揃える。

- 全 inventory の closure report
- scene / prefab editor migration tool とその execution report
- real gameplay verification bundle
- purge manifest と削除差分
- diagnostics / performance / regression の gate report

これらの成果物がない場合、「移行できているはず」という判断は認めない。

---

## テストケース

| テストケース | 目的 | 検証 |
| --- | --- | --- |
| `TC-V21-08-01` | v2.1 完了が representative slice ではなく full-game cutover であることを確認する | 本書が existing scene / prefab と game-wide runtime を完了条件に含めていなければならない |
| `TC-V21-08-02` | 全 service / command / dynamic source surface が inventory 管理対象であることを確認する | service inventory、command inventory、dynamic source inventory の完了単位、メタデータ、証跡が書かれていなければならない |
| `TC-V21-08-03` | scene / prefab migration が完了条件に含まれることを確認する | asset migration が M11 / M12 の出口条件に入り、少修正ルールが定義されていなければならない |
| `TC-V21-08-04` | M11 が全件 cutover の milestone であることを確認する | representative service ではなく residual inventory 0 が M11 の条件として書かれていなければならない |
| `TC-V21-08-05` | M12 が full-game verification と legacy purge の release gate であることを確認する | compile、asset、runtime、diagnostics/performance gate が同時に要求されていなければならない |
| `TC-V21-08-06` | bridge が完了時に authority を持てないことを確認する | bridge が diagnostics-only か物理削除であると書かれていなければならない |
| `TC-V21-08-07` | lingering legacy asset reference が完了条件違反であることを確認する | shipped scene / prefab から旧 component と旧 serialized reference を除去すると書かれていなければならない |
| `TC-V21-08-08` | 完了宣言に必要な成果物が定義されていることを確認する | inventory report、migration report、verification bundle、purge manifest が必須成果物として列挙されていなければならない |
| `TC-V21-08-09` | dynamic source 完了条件が completion spec に露出していることを確認する | `dynamic source inventory` の追加と `DynamicSource` / `DynamicValue` の cutover が M11 / M12 の対象として書かれていなければならない |
| `TC-V21-08-10` | command / authoring の field-preserving migration が定義されていることを確認する | 旧 MB の field data を successor Authoring に移してから component swap すると書かれていなければならない |

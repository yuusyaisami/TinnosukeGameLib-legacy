# Kernel v2.1 Legacy Removal Examples 仕様

## 文書ステータス

- 文書 ID: `03_LegacyRemovalExamplesSpec`
- 状態: Draft
- 役割: legacy system をどのように削除し、何を残し、何に載せ替えるかを、実コードの vertical example で固定する
- 範囲: 基盤削除例、UI 移行例、Command / Value 移行例、actor / query / neighbor resolve 削除例
- 非目標: 全 service の個別移行手順の網羅、legacy bridge の延命

### 改訂メモ

この文書は sample 集ではない。
legacy removal の型を固定する cookbook である。

特に v2.1 では `LTS` 系を先に全部消す前提なので、
「残したまま徐々に慣らす」例は扱わない。

今回の改訂では、foundation/UI/command-value/actor-query の 4 系統に分けて、delete-first の手順と keep-only-main-logic の線引きを固定する。

---

## 所有範囲

この仕様が所有するもの:

- legacy removal example format
- 基盤削除の代表例
- UI migration の代表例
- Command / Value migration の代表例
- actor / query / neighbor resolve migration の代表例
- remove / keep / new path / failure / performance の記述 rule

この仕様が所有しないもの:

- v2.1 runtime の core semantics
- final gameplay tuning
- 全 subsystem の exhaustive migration list

03 は「どう削除するかの型」を所有する。
「何を作るかの一般論」は 02 が所有する。

---

## 例のフォーマット

すべての例は次の形式に従う。

- Legacy anchor
- Target owner
- Remove
- Keep
- New data path
- New lifecycle path
- Failure rule
- Performance rule
- Migration notes

このフォーマットから外れる例は normative example として扱わない。

補足:

- `Remove` は target path に残してはいけない責務を指す
- `Keep` は domain logic として再利用してよい部分を指す
- `New data path` は declaration から verified runtime までの authority を指す
- `New lifecycle path` は acquire/release/tick/reset/init の owner を指す

---

## Delete-First Rule

v2.1 の legacy removal では、基盤を残したまま周辺 service だけを新設計化してはならない。

最初に target path から外すもの:

- `RuntimeLifetimeScopeBase`
- `BaseLifetimeScope`
- `IScopeNode`
- `IRuntimeResolver`
- `IFeatureInstaller`
- `ScopeFeatureInstallerUtility`
- `LTSIdentityMB`

これらは quarantine bridge に押し込めることはあっても、new runtime authority にしてはならない。

---

## 基盤削除例

### Example A: `RuntimeLifetimeScopeBase`

Legacy anchor:

- [RuntimeLifetimeScope.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs)

Target owner:

- `SceneKernel`
- `EntityIdentityMB`
- entity-scoped `ServiceGraph`
- `LifecyclePlan`

Remove:

- build coordinator coupling
- resolver creation
- acquire / release ownership
- tick hub ownership
- hierarchy parent inference
- `IScopeNode` exposure

Keep:

- gameplay-visible active/visible intent だけを、entity metadata または lifecycle policy として移植してよい

New data path:

- `EntityIdentityMB` が entity identity を提供
- declaration MB が service declarations を提供
- verified plan が entity/service/lifecycle data を構築
- `SceneKernel` が registration table を作る

New lifecycle path:

- `OnEnable` / `OnDisable` 主導ではなく plan-driven dispatch

Failure rule:

- target path で `RuntimeLifetimeScopeBase` が必要になったら failure

Performance rule:

- build-time hierarchy walk を steady-state runtime に残さない

Migration notes:

- `RuntimeLifetimeScopeBase` は最初の削除対象である
- これを bridge の owner にしてはならない

### Example B: `BaseLifetimeScope`

Legacy anchor:

- [BaseLifetimeScope.cs](../../GameLib/Script/Common/LTS/Core/BaseLifetimeScope.cs)

Target owner:

- `EntityIdentityMB`
- declaration MB

Remove:

- obsolete shell 継承
- typed parent shell
- installer entry point

Keep:

- 何も keep しない

New data path:

- inheritance ではなく entity metadata と verified plan

New lifecycle path:

- parent type ではなく explicit entity relation

Failure rule:

- new code が `BaseLifetimeScope` を継承したら failure

Performance rule:

- inheritance-based composition を再導入しない

Migration notes:

- API 互換用 alias を target path に持ち込まない

### Example C: `IScopeNode`

Legacy anchor:

- [IScopeNode.cs](../../GameLib/Script/Common/LTS/Core/IScopeNode.cs)

Target owner:

- `EntityRef`
- `ScopeGraph`
- explicit runtime contracts

Remove:

- `Parent`
- `Resolver`
- `Kind`
- acquire/release/tick handler contract
- `GetPathFromRoot` authority

Keep:

- domain logic が必要とする「owner identity」だけを `EntityRef` として残す

New data path:

- owner reference は `EntityRef`
- hierarchy relation は `ScopeGraph`
- service access は `ServiceGraph`

New lifecycle path:

- handler interface ではなく lifecycle plan target

Failure rule:

- target runtime public API が `IScopeNode` を要求したら failure

Performance rule:

- `Parent` walk fallback を hot path から除去

Migration notes:

- service main logic には `EntityRef` or explicit handle だけを渡す

### Example D: `IRuntimeResolver`

Legacy anchor:

- [RuntimeResolverHub.cs](../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs)

Target owner:

- `ServiceGraph`
- `CommandCatalog`
- `ValueStore`
- `RuntimeQuery`

Remove:

- type-based resolve
- parent fallback
- collection resolve
- build callback mutation

Keep:

- explicit dependency declarationに変換可能な constructor dependency 情報だけ

New data path:

- service -> `EntityRef + ServiceId`
- command -> `CommandTypeId`
- value -> `ValueKeyId`
- query -> `RuntimeQueryId`

New lifecycle path:

- resolver build callback なし

Failure rule:

- target path で `Resolver.TryResolve` が残ったら failure

Performance rule:

- no full dictionary scan
- no type-interface enumeration

Migration notes:

- typed convenience API は generated accessor としてのみ許可

### Example E: `IFeatureInstaller`

Legacy anchor:

- [BaseLifetimeScope.cs](../../GameLib/Script/Common/LTS/Core/BaseLifetimeScope.cs)
- [ScopeFeatureInstallerUtility.cs](../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs)

Target owner:

- declaration MB
- contribution extraction

Remove:

- `InstallFeature(...)`
- runtime builder mutation
- owned-installer discovery

Keep:

- serialized config payload

New data path:

- MB -> contribution -> IR -> verified plan

New lifecycle path:

- installer callback なし

Failure rule:

- new target MB が `IFeatureInstaller` を実装したら failure

Performance rule:

- `GetComponentsInChildren` based installer scan を廃止

Migration notes:

- 旧 MB は declaration MB へ置換し、name を引き継いでも installer にはしない

### Example F: `ScopeFeatureInstallerUtility`

Legacy anchor:

- [ScopeFeatureInstallerUtility.cs](../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs)

Target owner:

- contribution extraction
- declaration MB collector

Remove:

- owned-installer discovery
- nearest-scope inference
- transform/hierarchy based ownership repair

Keep:

- editor-time or extraction-time filtering rule only if it can be moved outside runtime

New data path:

- `EntityIdentityMB` root
- explicit declaration enumeration
- verified contribution set

New lifecycle path:

- runtime installer scan なし

Failure rule:

- target path が `GetComponentsInChildren` installer scan を必要としたら failure

Performance rule:

- hierarchy walk を boot steady-state path に残さない

Migration notes:

- utility の便利さを残すために runtime discovery を温存してはならない

### Example G: `LTSIdentityMB`

Legacy anchor:

- [LTSIdentityMB.cs](../../GameLib/Script/Common/LTS/Identity/MB/LTSIdentityMB.cs)

Target owner:

- `EntityIdentityMB`

Remove:

- hierarchy-derived kind inference
- installer behavior
- dynamic registry auto-registration coupling

Keep:

- display/debug metadata
- source trace と name mapping に流用できる authoring data

New data path:

- `EntityIdentityMB` -> `EntityRef` -> entity metadata

New lifecycle path:

- identity install ではなく static registration

Failure rule:

- target path の identity owner が `LTSIdentityMB` なら failure

Performance rule:

- parent hierarchy scan で kind を決めない

Migration notes:

- rename だけでは不十分で、責務分離まで必要

---

## UI 移行例

### Example UI-1: `ButtonChannel`

Legacy anchor:

- [ButtonChannelHubMB.cs](../../GameLib/Script/Project/UI/Core/Elements/ButtonChannel/ButtonChannelHubMB.cs)
- [ButtonChannelHubService.cs](../../GameLib/Script/Project/UI/Core/Elements/ButtonChannel/ButtonChannelHubService.cs)
- [ButtonChannelRuntime.cs](../../GameLib/Script/Project/UI/Core/Elements/ButtonChannel/ButtonChannelRuntime.cs)

Target owner:

- entity-scoped `ServiceGraph`
- UI service-owned graph
- `LifecyclePlan`

Remove:

- installer-based registration
- resolver-based neighbor/service acquisition
- hierarchy-derived ownership

Keep:

- channel logic
- phase transition
- input consumption behavior
- telemetry semantics

New data path:

- declaration MB が channel definition を保持
- plan が button node graph を生成
- runtime は `ButtonChannel` service と node handles を構築

New lifecycle path:

- UI update / reset / selection reaction を plan-driven phase に接続

Failure rule:

- missing node graph
- duplicate channel declaration
- invalid UI edge

Performance rule:

- node lookup は dense table
- interaction path は cached consumer handle

Migration notes:

- runtime の `ButtonChannelRuntime` は残してよいが、owner 接続を作り直す

### Example UI-2: `UISelectionService`

Legacy anchor:

- [UISelectionService.cs](../../GameLib/Script/Project/UI/Core/Selection/UISelectionService.cs)

Target owner:

- scene-level or entity-scoped UI service registration
- service-owned selection graph

Remove:

- scope/resolver coupling
- hidden nearest-owner assumptions

Keep:

- selection state machine
- block mask logic
- telemetry behavior

New data path:

- explicit selectable node graph
- explicit modal / navigation membership
- `EntityRef`-anchored UI root ownership

New lifecycle path:

- activation and reset via lifecycle phases

Failure rule:

- selection target not present in graph
- modal constraints violated
- implicit `IScopeNode` ownership requirement remains

Performance rule:

- current target and selection sets are cached
- no hierarchy traversal on navigate/select

Migration notes:

- selection は UI graph owner service に寄せる

### Example UI-3: `UINavigationService`

Legacy anchor:

- [UINavigationService.cs](../../GameLib/Script/Project/UI/Core/UINavigation/UINavigationService.cs)
- [UINavigationMB.cs](../../GameLib/Script/Project/UI/Core/UINavigation/UINavigationMB.cs)

Target owner:

- UI service registration
- navigation edge graph

Remove:

- installer-based service composition
- runtime binding through container callbacks

Keep:

- navigation repeat logic
- control-scheme aware behavior
- telemetry behavior

New data path:

- declaration MB provides navigation options
- plan builds navigation edge table
- runtime consumes edge table + selection service handle

New lifecycle path:

- input-consumer binding occurs through verified UI graph activation

Failure rule:

- invalid navigation edge
- missing selection dependency

Performance rule:

- navigation lookup is edge-table based
- repeat timer path allocates nothing

Migration notes:

- `UINavigationMB` は options declaration MB へ変える

### Example UI-4: `ModalStack`

Legacy anchor:

- [ModalStackChannelHubService.cs](../../GameLib/Script/Project/UI/Core/ModalStackChannel/ModalStackChannelHubService.cs)
- [ModalStackChannelHubMB.cs](../../GameLib/Script/Project/UI/Core/ModalStackChannel/ModalStackChannelHubMB.cs)

Target owner:

- UI service registration
- service-owned modal layer graph

Remove:

- scope/installer ownership
- runtime hidden layering assumptions

Keep:

- modal push/pop logic
- layer semantics
- telemetry/debug semantics

New data path:

- declaration MB defines modal layers
- plan builds layer membership table
- runtime stack service owns active stack
- input root resolution comes from UI graph handles, not descendant search

New lifecycle path:

- open/close/reset through explicit lifecycle events

Failure rule:

- invalid layer reference
- cyclic modal ownership
- modal root resolution requiring `IScopeNode.IsDescendant` remains

Performance rule:

- stack operations are O(1) or bounded
- layer membership is cached

Migration notes:

- modal state is graph-owned, not hierarchy-owned

---

## Command / Value 移行例

### Example CV-1: `CommandRunnerMB`

Legacy anchor:

- [CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs)

Target owner:

- `CommandCatalog`
- declaration MB
- `LifecyclePlan`

Remove:

- bulk executor registration
- scope-kind branch
- build callback debug binding
- command runner lifecycle enrollment via installer

Keep:

- command domain grouping metadata
- debug/telemetry semantics that can move to diagnostics surface

New data path:

- command declaration MB -> `CommandIR` -> `CommandCatalogPlan`
- executor references are verified, not discovered

New lifecycle path:

- command runtime activation handled by lifecycle plan

Failure rule:

- unknown `CommandTypeId`
- missing executor reference
- invalid payload schema

Performance rule:

- no `IReadOnlyList<ICommandExecutor>` scan
- dispatch is table lookup

Migration notes:

- `CommandRunnerMB` は delete-first 対象の一つ

### Example CV-2: `BlackboardMB`

Legacy anchor:

- [BlackboardMB.cs](../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs)

Target owner:

- value declaration MB
- `ValueStore`
- `ValueInitPlan`
- `LifecyclePlan`

Remove:

- installer mutation
- acquire/release hook ownership
- debug binding ownership
- transform auto-write ownership

Keep:

- local init payload as declaration input
- grid/table init payload if schema-backed に正規化できる部分

New data path:

- declaration payload -> `ValueKeyIR` / `ValueInitPlanIR`
- runtime store consumes init plan

New lifecycle path:

- init apply phase is explicit lifecycle step

Failure rule:

- unknown `ValueKeyId`
- invalid table schema
- init timing leak outside lifecycle plan

Performance rule:

- no runtime dynamic registration
- no init from `Construct`/`Start`

Migration notes:

- `BlackboardMB` の field surface は preservation floor を見ながら declaration 化する

### Example CV-3: `BlackboardService`

Legacy anchor:

- [BlackboardService.cs](../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs)

Target owner:

- `ValueStore`

Remove:

- `Parent` walk
- global fallback write
- root/game-logic-root auto-targeting

Keep:

- local storage intent
- merge semantics if explicit store contract に移せるもの

New data path:

- explicit store boundary
- explicit shared/global store if needed

New lifecycle path:

- reset and reapply through value/lifecycle subsystem

Failure rule:

- missing store boundary is failure
- implicit parent write is failure

Performance rule:

- no upward traversal on read/write

Migration notes:

- `BlackboardService` を Blackboard v2 として再利用しない

### Example CV-4: `VarKeyRegistryLocator`

Legacy anchor:

- [VarKeyRegistryLocator.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs)

Target owner:

- verified artifact input

Remove:

- runtime `Resources.Load`
- runtime asset fallback
- runtime-created registry

Keep:

- editor-time seed generation only if migration tooling で明示的に分離される場合

New data path:

- verified `ValueSchemaPlan`

New lifecycle path:

- none at runtime

Failure rule:

- missing schema artifact is boot failure

Performance rule:

- no runtime asset lookup

Migration notes:

- editor tooling と runtime authority を完全に分離する

### Example CV-5: `VarIdResolver`

Legacy anchor:

- [VarIdResolver.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs)

Target owner:

- generated `ValueKeyId`
- verified mapping

Remove:

- stable-key runtime resolve
- negative runtime ID creation

Keep:

- debug label mapping only if diagnostics metadata として分離されるもの

New data path:

- authoring normalization -> `ValueKeyId`

New lifecycle path:

- none

Failure rule:

- unknown stable key at runtime is failure, not repair

Performance rule:

- slot/key lookup only

Migration notes:

- generated key surface は preservation floor として残してよい

---

## Actor / Query / Neighbor Resolve 削除例

### Example AQ-1: `ActorSourceFastResolver`

Legacy anchor:

- [ActorSourceFastResolver.cs](../../GameLib/Script/Common/Commands/VNext/Core/ActorSourceFastResolver.cs)
- [ActorScopeResolver.cs](../../GameLib/Script/Common/Commands/VNext/Core/ActorScopeResolver.cs)

Target owner:

- `RuntimeQuery`
- explicit query graph
- explicit entity handle references

Remove:

- hierarchy fallback
- transform subtree search
- scope registry fallback
- shared channel fallback search

Keep:

- actor targeting semantics
- command-facing source model if normalized

New data path:

- `RuntimeQueryId`
- explicit actor reference
- explicit UI graph endpoint

New lifecycle path:

- query target availability follows entity/service lifecycle

Failure rule:

- unresolved query target is command/query failure

Performance rule:

- no transform search
- cached query result if validity can be proven

Migration notes:

- actor targeting must become query-driven, not resolver-driven

### Example AQ-2: scope/parent walk helper

Legacy anchor:

- any helper or service code path that resolves owner/dependency by repeated `IScopeNode.Parent` walk
- concrete examples include `BlackboardService` and `ActorSourceFastResolver.ResolveShared(...)`

Target owner:

- `ServiceGraph`
- `ValueStore`
- `RuntimeQuery`

Remove:

- upward fallback search
- nearest-parent owner inference
- "find first parent that has X" helper pattern

Keep:

- semantic notion of shared/global/nearest owner only if it is normalized into explicit registration metadata

New data path:

- explicit shared service declaration
- explicit store boundary declaration
- explicit query target declaration

New lifecycle path:

- availability is determined by registration/lifecycle plan, not parent traversal

Failure rule:

- dependency that can only be found by parent walk is failure

Performance rule:

- no repeated parent traversal on read/write/dispatch paths

Migration notes:

- parent walk helper は generic utility として残さず subsystem ごとに分解する

### Example AQ-3: UI neighbor search helper

Legacy anchor:

- any helper that derives next selectable/input target from runtime hierarchy or resolver adjacency

Target owner:

- UI graph
- navigation edge table

Remove:

- next-target discovery from transform structure

Keep:

- directional navigation semantics

New data path:

- explicit edge graph

New lifecycle path:

- graph rebuild only on explicit UI plan/state changes

Failure rule:

- missing edge is navigation failure, not hierarchy fallback

Performance rule:

- O(1) or bounded directional lookup

Migration notes:

- this applies even when old helper is spread across multiple services

### Example AQ-4: runtime target search helper

Legacy anchor:

- any helper that walks parents, roots, registries, or scene objects to find runtime target owner

Target owner:

- `RuntimeQuery`
- `ServiceGraph`
- UI graph

Remove:

- mixed ownership search helper

Keep:

- target semantics only

New data path:

- query id or explicit service dependency

New lifecycle path:

- target availability tied to registered entity/service lifecycle

Failure rule:

- ambiguous target is failure

Performance rule:

- no broad search

Migration notes:

- target helpers must split by subsystem instead of remaining as generic resolver utility

---

## Temporary Bridge Rule

temporary bridge は許可されるが、次を満たす場合だけである。

- target runtime authority にならない
- diagnostics-visible
- removability が先に定義されている
- data path を old resolver へ戻さない

bridge が `RuntimeLTS`、`Resolver`、`BlackboardService fallback`、stable-key runtime resolve を再び authority にするなら、それは bridge ではなく regression である。

---

## 受け入れ基準

- `RuntimeLTS` 系が先に全部削除対象として扱われている
- `IScopeNode` / `IRuntimeResolver` / `IFeatureInstaller` を残さないと明記している
- `CommandRunnerMB` / `BlackboardService` / `VarIdResolver` が具体例に含まれている
- UI 例が 1 本以上ではなく、主要 4 例として含まれている
- `ScopeFeatureInstallerUtility` が基盤削除例として独立に扱われている
- 各例に `Remove` / `Keep` / `New data path` / `New lifecycle path` / `Failure rule` / `Performance rule` が揃っている
- temporary bridge を target runtime として扱っていない

---

## テストケース

| テストケース | 目的 | 検証 |
|---|---|---|
| `TC-V21-03-01` | `RuntimeLTS` 系が先に全部削除対象であることを確認する | 基盤削除例に `RuntimeLifetimeScopeBase`、`BaseLifetimeScope`、`IScopeNode`、`IRuntimeResolver`、`IFeatureInstaller`、`ScopeFeatureInstallerUtility`、`LTSIdentityMB` が含まれていなければならない |
| `TC-V21-03-02` | command/value 接続点の dismantling が明記されていることを確認する | `CommandRunnerMB`、`BlackboardMB`、`BlackboardService`、`VarKeyRegistryLocator`、`VarIdResolver` が individual example として含まれていなければならない |
| `TC-V21-03-03` | UI migration examples が explicit graph 前提で書かれていることを確認する | `ButtonChannel`、`UISelectionService`、`UINavigationService`、`ModalStackChannelHubService` が graph-owned model で記述されていなければならない |
| `TC-V21-03-04` | actor/query migration が fallback search を再導入しないことを確認する | `ActorSourceFastResolver` と scope/parent walk helper が explicit query/dependency path に置換されると書かれていなければならない |
| `TC-V21-03-05` | example format が統一されていることを確認する | すべての例が共通フォーマットを満たしていなければならない |
| `TC-V21-03-06` | temporary bridge が authority でないことを確認する | bridge rule に removability と no-authority 条件が含まれていなければならない |

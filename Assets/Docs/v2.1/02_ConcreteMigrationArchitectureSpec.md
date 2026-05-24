# Kernel v2.1 Concrete Migration Architecture 仕様

## 文書ステータス

- 文書 ID: `02_ConcreteMigrationArchitectureSpec`
- 状態: Draft
- 役割: v2.1 における実装直結の移行 runtime 像、public contract、subsystem boundary、data flow、failure rule を decision-complete に定義する
- 範囲: `ApplicationKernel` / `SceneKernel`、`EntityIdentityMB`、declaration MB、entity-scoped `ServiceGraph`、`Lifecycle`、`ValueStore`、`Scalar`、`CommandCatalog`、`RuntimeQuery`、UI subsystem、diagnostics、compile boundary
- 非目標: legacy 共存の長期運用、旧 `LTS` contract の再定義、最終 gameplay tuning、editor UX の詳細

### 改訂メモ

この文書は、v2.1 の architecture sketch を implementation-ready な runtime contract に落とすために作る。

ここでの目的は「何を作るか」を明確にすることであり、
「どう legacy としばらく共存させるか」を伸ばすことではない。

v2.1 は second kernel ではない。
v2 target kernel へ到達するための migration runtime である。

今回の改訂では、次を decision-complete に固定する。

- `Entity` を唯一の runtime ownership unit とすること
- `ApplicationKernel` が DDOL root として game-wide authority を持ち、`SceneKernel` が scene-local root として entity-scoped `ServiceGraph` を所有すること
- `ValueStore`、`Scalar`、`CommandCatalog`、`RuntimeQuery` を `ServiceGraph` と別 subsystem に分けること
- UI hierarchy を service-owned graph として扱うこと
- `Common/LTS` 非依存の compile boundary を target path の必須条件にすること

---

## 所有範囲

この仕様が所有するもの:

- `ApplicationKernel` の role と `SceneKernel` との boundary
- `SceneKernel` の最終責務
- `Entity` の runtime model
- `EntityIdentityMB` の contract
- declaration MB の contract
- verified plan family の role
- entity-scoped `ServiceGraph` の contract
- `Lifecycle`、`ValueStore`、`Scalar`、`CommandCatalog`、`RuntimeQuery` の v2.1 での接続位置
- UI subsystem の runtime model
- diagnostics / failure policy
- compile boundary と dependency direction

この仕様が所有しないもの:

- v2 core semantics の再定義
- 各 service の最終内部アルゴリズム
- visual/inspector/editor の最終 UX
- save payload format の最終仕様

02 は v2.1 の concrete runtime shape を所有する。
01 が dismantling order を所有するなら、02 は replacement runtime の形を所有する。
`ApplicationKernel` と `SceneKernel` の 2 層 composition の詳細は [06_KernelLayerCompositionSpec.md](06_KernelLayerCompositionSpec.md) で固定する。

---

## 目的

v2.1 の concrete migration architecture の目的は次の通り。

```text
1. Entity を唯一の runtime ownership unit に統一する。
2. service 接続を EntityRef + ServiceId に固定する。
3. command / value / query / UI hierarchy を明示 subsystem に分離する。
4. declaration から verified plan を作り、runtime discovery を排除する。
5. legacy main logic を残しても、legacy composition は残さない。
```

中心ルール:

```text
target path の authority は `ApplicationKernel`、`SceneKernel`、`EntityRef`、verified plan、typed identity にある。
Transform hierarchy、runtime resolver、installer mutation、string/stable-key fallback にはない。
```

---

## 現行コードとの接続観測

この仕様は次の実装圧力を前提にしている。

- [RuntimeLifetimeScope.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs)
- [ProjectLifetimeScope.cs](../../GameLib/Script/Project/LTS/ProjectLifetimeScope.cs)
- [GlobalLifetimeScope.cs](../../GameLib/Script/Project/Global/LTS/GlobalLifetimeScope.cs)
- [LTSIdentityMB.cs](../../GameLib/Script/Common/LTS/Identity/MB/LTSIdentityMB.cs)
- [ScopeFeatureInstallerUtility.cs](../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs)
- [CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs)
- [BlackboardMB.cs](../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs)
- [BlackboardService.cs](../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs)
- [VarKeyRegistryLocator.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs)
- [VarIdResolver.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs)
- [ActorSourceFastResolver.cs](../../GameLib/Script/Common/Commands/VNext/Core/ActorSourceFastResolver.cs)
- [ButtonChannelHubMB.cs](../../GameLib/Script/Project/UI/Core/Elements/ButtonChannel/ButtonChannelHubMB.cs)
- [ButtonChannelHubService.cs](../../GameLib/Script/Project/UI/Core/Elements/ButtonChannel/ButtonChannelHubService.cs)
- [UISelectionService.cs](../../GameLib/Script/Project/UI/Core/Selection/UISelectionService.cs)
- [UINavigationService.cs](../../GameLib/Script/Project/UI/Core/UINavigation/UINavigationService.cs)
- [ModalStackChannelHubService.cs](../../GameLib/Script/Project/UI/Core/ModalStackChannel/ModalStackChannelHubService.cs)

ここで見えている問題:

- ownership unit が `LTS` 種別に分かれすぎている
- service 接続が `Resolver.TryResolve` に依存している
- command と value が installer / lifecycle / fallback を引きずっている
- UI hierarchy が scope / resolver / transform coupling を多く持つ
- `ButtonChannelHubService` が `IScopeNode` と scope lifecycle handler を直接前提にしている
- `UISelectionService` と `UINavigationService` が `IScopeNode` ベースの current/hover/selection owner を内部 state として持っている
- `ModalStackChannelHubService` が `IScopeNode` descendant 判定を modal ownership 判定に使っている

02 はこれらを 1 つの runtime model へ落とし直す。

---

## Runtime Architecture Definition

### 1. Kernel pair

v2.1 runtime の authority は `ApplicationKernel` と `SceneKernel` の 2 層である。
`ApplicationKernel` は DDOL の game-wide root、`SceneKernel` は scene-local root である。

#### `ApplicationKernel`

`ApplicationKernel` が所有するもの:

- boot manifest / profile selection
- cross-scene shared service ownership
- persistent/global state coordination
- current `SceneKernel` の生成・破棄管理
- scene transition orchestration
- app-wide diagnostics sink

`ApplicationKernel` が所有しないもの:

- scene-local entity registry
- scene-local UI graph
- per-scene entity-scoped service slots

`ApplicationKernel` は game-wide boot と shared ownership の orchestrator であり、汎用 DI container ではない。

`ApplicationKernel` の必須 public contract:

```text
SelectBootManifest(...)
SelectKernelProfile(...)
RequestSceneKernelCreate(...)
RequestSceneKernelDispose(...)
ResolveSharedService(ServiceId serviceId)
TryResolveSharedService(ServiceId serviceId, out object service)
GetDiagnosticsSink()
```

#### `SceneKernel`

`SceneKernel` が所有するもの:

- scene-local entity registry
- contribution intake
- verified plan activation
- entity-scoped `ServiceGraph`
- lifecycle dispatch entry
- command runtime access boundary
- value runtime access boundary
- runtime query access boundary
- diagnostics sink entry
- debug/provenance lookup entry

`SceneKernel` が所有しないもの:

- gameplay service 内部アルゴリズム
- UI 表示階層そのもの
- final save persistence

`SceneKernel` は scene-local registry と orchestrator であり、汎用 DI container ではない。

`SceneKernel` の必須 public contract:

```text
RegisterEntity(EntityRegistrationPlan plan)
UnregisterEntity(EntityRef entityRef)
Resolve(EntityRef entityRef, ServiceId serviceId)
TryResolve(EntityRef entityRef, ServiceId serviceId, out object service)
GetValueStore(EntityRef entityRef or StoreBoundaryId)
GetCommandCatalog()
GetRuntimeQuery(RuntimeQueryId queryId)
DispatchLifecycle(LifecyclePhase phase)
```

typed wrapper はこの上に載せてよい。
ただし authority は常に `ApplicationKernel` / `SceneKernel` が持つ verified registration table にある。

### 2. Entity

v2.1 では runtime ownership unit はすべて `Entity` である。

廃止する概念:

- `ProjectLTS`
- `ProjectLifetimeScope`
- `PlatformLifetimeScope`
- `GlobalLifetimeScope`
- `SceneLifetimeScope`
- `SceneLTS`
- `FieldLifetimeScope`
- `EntityLifetimeScope`
- `EntityLTS`
- `UILifetimeScope`
- `UIElementLifetimeScope`

これらは runtime species ではなく、旧 ownership 断片として扱う。
新しい runtime ではすべて `Entity` に統一する。

entity の差分は次だけで表す。

- `EntityRef`
- entity metadata
- registered services
- lifecycle plan association
- value store boundary
- runtime query exposure
- UI graph participation

Entity の違いは service composition で決まる。
つまり旧 `ProjectLTS`、`SceneLTS`、`EntityLTS` の差は、型ではなく「どの service が登録されるか」によって表す。

### 3. Typed identity

v2.1 runtime が扱う主な identity:

- `EntityRef`
- `ServiceId`
- `CommandTypeId`
- `ValueKeyId`
- `RuntimeQueryId`
- `LifecyclePlanId`
- `UINodeHandle`

補助 metadata として持つもの:

- authoring trace
- declaration source location
- service config trace

禁止:

- runtime stable key lookup を authority にすること
- runtime negative ID を作ること
- `Transform.parent` から owner identity を推測すること

---

## Verified Plan Model

v2.1 で言う `plan` は、曖昧な設定ファイルではない。
declaration から生成される immutable な runtime input である。

最低限、次の plan family を持つ。

- `EntityRegistrationPlan`
- `ServiceGraphPlan`
- `LifecyclePlan`
- `ValueSchemaPlan`
- `ValueInitPlan`
- `CommandCatalogPlan`
- `RuntimeQueryPlan`
- `UIGraphPlan`

役割:

- declaration MB と `EntityIdentityMB` から contribution を集める
- `KernelIR` または同等の正規化中間表現へ落とす
- duplicate、missing owner、invalid dependency を検証する
- runtime がそのまま引ける dense table に投影する

plan の意味:

- runtime は plan を実行する
- runtime は scene/hierarchy を再探索して structure を発見しない
- validation failure は boot/register failure で止める

禁止:

- declaration MB が自分で plan を mutate すること
- runtime が plan を silent repair すること
- missing data を fallback search で埋めること

---

## EntityIdentityMB Contract

`EntityIdentityMB` は `LTSIdentityMB` の完全置換である。

必須 field:

- `EntityRef`
- display/debug name
- entity metadata
- source trace
- optional classification tags

役割:

- GameObject を runtime ownership unit と結び付ける bridge
- declaration MB 群の root
- verified plan へ入る identity source

禁止:

- hierarchy を走査して entity kind を推定すること
- `IFeatureInstaller` を実装すること
- runtime service registration を行うこと
- dynamic registry auto-registration を内部で行うこと

実装前提:

- 1 Entity root につき 1 つ
- child object に付与してもよいが、その場合でも owner authority は `EntityRef` であり transform 階層ではない

---

## Declaration MB Contract

service 用 MB は installer ではなく declaration MB とする。

declaration MB が持つもの:

- service declaration identity
- service config payload
- optional UI graph metadata
- optional lifecycle declaration metadata
- source trace

declaration MB がしてはならないこと:

- runtime service を new すること
- `InstallFeature(...)` を持つこと
- `Resolver.TryResolve(...)` で依存 service を探すこと
- `GetComponentsInChildren(...)` や `GetComponentInParent(...)` で owner を確定すること

declaration MB は `EntityIdentityMB` 配下の declarative input である。
`SceneKernel` が plan 構築時にこれらを集め、runtime は plan からのみ service を構築する。

declaration MB の典型例:

- `ButtonChannelHubMB`
- `UINavigationMB`
- `ModalStackChannelHubMB`
- `BlackboardMB` の移行後 replacement
- command declaration MB 群

これらは見た目の inspector surface を維持してよい。
ただし runtime authority は inspector ではなく verified plan にある。

---

## Entity-Scoped ServiceGraph Contract

### 1. Ownership

runtime service owner は `SceneKernel` 配下の entity-scoped `ServiceGraph` である。

`ServiceGraph` が持つもの:

- `EntityRef` ごとの service registration table
- `ServiceId` ごとの verified service slot
- dependency metadata
- lifetime metadata
- diagnostics provenance

### 2. Resolve API

authority API:

- `Resolve(EntityRef entity, ServiceId serviceId)`
- `TryResolve(EntityRef entity, ServiceId serviceId, out value)`

generated typed accessor は許可する。
ただし generated accessor は上記 API の sugar であり、別の runtime truth ではない。

禁止:

- type-based full scan
- `ResolveAll<T>()`
- hierarchy walk による parent fallback
- service missing 時の silent repair
- `IScopeNode` を引数に取る resolve API
- `GameObject` / `Transform` を key にした runtime resolve

### 3. Dependency wiring

service-to-service wiring は verified dependency で行う。

許可:

- same entity の explicit dependency
- explicitly declared shared/global service dependency
- explicit `RuntimeQuery` or `ValueStore` boundary access

禁止:

- `IRuntimeResolver` による ad-hoc resolve
- `IScopeNode.Parent` による upward search
- UI hierarchy を使った implicit resolve

### 4. Cardinality

v2.1 で許可する主要 cardinality:

- one-per-entity instance
- one-per-scene manager
- explicitly bounded shared manager

未許可:

- unbounded runtime registration
- command execution count に比例する service instance
- UI node count に比例して無制限増殖する service

### 5. What ServiceGraph is not

`ServiceGraph` は次ではない。

- entity ごとの DI container
- command executor registry
- value key registry
- UI hierarchy authority
- runtime repair の窓口

`ServiceGraph` は entity-scoped service ownership と resolve の subsystem である。
command/value/query/UI graph はそこへぶら下がるのではなく、明示的に分離した subsystem として接続する。

---

## Lifecycle Contract

`Lifecycle` は plan-driven dispatch とする。

v2.1 での前提:

- entity registration 時に lifecycle target が確定している
- acquire / release / tick / async phase は table-driven
- service scan で handler を収集しない

lifecycle target にできるもの:

- service
- entity boundary
- explicit runtime query target

禁止:

- `IScopeAcquireHandler` scan
- `IScopeTickHandler` scan
- UI hierarchy 走査から tick 対象を集めること

UI を含む hot path では、phase table と cached handle を必須とする。

lifecycle phase の代表:

- registration apply
- acquire
- initialize value
- activate
- tick
- deactivate
- release
- teardown

service が tick されるかどうかは plan で確定していなければならない。
runtime scan で「tick 可能な object」を集めてはならない。

---

## Value Runtime Contract

### ValueStore

`ValueStore` は generic runtime value storage である。

役割:

- `ValueKeyId` ベースの read/write
- revision 管理
- dirty signal
- entity-local あるいは explicit shared store boundary
- `ValueInitPlan` 適用

禁止:

- stable-key runtime resolve
- missing key の runtime invention
- parent fallback write
- `BlackboardService` 風の upward search
- `VarKeyRegistryLocator` への runtime asset resolve

### Store boundary

v2.1 では store boundary は entity に結び付く。

許可:

- entity-local store
- explicitly shared store

禁止:

- hidden parent store fallback
- hierarchy-derived nearest blackboard

### Initialization

init は `ValueInitPlan` 経由のみ。

禁止:

- `Construct`
- `Start`
- `OnAcquire`

から暗黙に value 初期化を始めること。

### ValueStore API shape

最低限の API 期待:

```text
bool TryRead(ValueKeyId key, out ValueVariant value)
bool TryWrite(ValueKeyId key, in ValueVariant value)
uint GetRevision(ValueKeyId key)
bool TryGetMetadata(ValueKeyId key, out ValueKeyMetadata metadata)
```

debug label は metadata として持ってよい。
しかし runtime authority を stable key string に戻してはならない。

---

## Scalar Contract

`Scalar` は `ValueStore` の別 subsystem である。

役割:

- float 専用 modulation
- baseline / additive / multiplicative / clamp / timed contribution
- verified binding
- explicit inherited endpoint

禁止:

- `BaseScalarService` 的 parent walk
- `Animator.StringToHash` 的 identity authority
- scalar missing 時の silent `0`
- hidden `DynamicValue<float>` read

scalar は `ValueStore` 上に重なる specialized runtime として扱うが、
generic value storage と同一 subsystem にしない。

典型用途:

- animation influence
- material effect intensity
- UI feedback amplitude
- time-based modulation

scalar binding も plan で確定していなければならない。

---

## Command Runtime Contract

`CommandCatalog` は `ServiceGraph` ではない。

役割:

- `CommandTypeId` ベースの table-driven dispatch
- payload schema validation
- executor reference ownership
- command-local state boundary
- command diagnostics

禁止:

- `CommandRunnerMB` 的 bulk registration
- `IReadOnlyList<ICommandExecutor>` discovery
- stable-key runtime command resolve
- command executor を service discovery で見つけること

`CommandCatalog` が service に依存する場合は、明示 dependency として `ServiceGraph` を利用する。
しかし command subsystem 自体を service collection として表現してはならない。

### CommandCatalog API shape

最低限の API 期待:

```text
bool TryDispatch(CommandTypeId typeId, in CommandPayload payload, in CommandExecutionContext context)
bool TryGetSchema(CommandTypeId typeId, out CommandPayloadSchema schema)
```

executor collection を外から列挙して dispatch する構造を target path に残してはならない。

---

## RuntimeQuery Contract

`RuntimeQuery` は explicit runtime lookup subsystem である。

役割:

- verified query identity
- bounded lookup
- explicit target set

禁止:

- `ActorSourceFastResolver` 的 hierarchy fallback
- transform subtree search
- scope registry search
- service search との混同

新しい query path は `RuntimeQueryId` を authority にする。
query が actor/entity/UI node を返す場合でも、runtime search helper に戻してはならない。

`RuntimeQuery` は「何を返すか」を query identity で固定する。
例えば:

- entity query
- service-backed actor query
- UI node query
- modal root query

generic search utility に戻してはならない。

---

## UI Subsystem Contract

### 1. Registration

次は `ServiceGraph` 登録対象である。

- `ButtonChannelHubService`
- `UISelectionService`
- `UINavigationService`
- `ModalStackChannelHubService`

必要なら関連 service も同じ rule に従う。

この 4 つは current codebase で hierarchy/state/input coupling が強い代表例であるため、v2.1 では優先的に `ServiceGraph` registration + service-owned graph へ載せ替える。

### 2. UI hierarchy ownership

UI hierarchy は service-owned graph として構築する。

authority:

- verified plan
- declaration metadata
- explicit node handle

authority ではないもの:

- `Transform.parent`
- scene hierarchy
- resolver-based neighbor search

### 3. UI graph model

UI subsystem は少なくとも次を内部 graph に持つ。

- node handle
- parent handle
- child index / sibling order
- selection eligibility
- navigation edge
- modal layer membership
- input consumer binding
- optional visual binding handle
- optional button channel tag table

### 4. Performance rule

UI hot path で必須:

- cached handle
- dense table
- bounded traversal
- no per-frame hierarchy rebuild
- no broad search allocation

### 5. Value and command connection

UI service が value や command と接続する場合:

- value は `ValueKeyId` access policy で結ぶ
- command は `CommandTypeId` / catalog dispatch で結ぶ
- `Resolver.TryResolve` に戻らない

UI graph node は必要なら `EntityRef` と別に `UINodeHandle` を持ってよい。
ただし owner authority は依然として `EntityRef` 側にある。

---

## Data Flow

基本パイプライン:

```text
EntityIdentityMB
+ declaration MB
  -> Contribution extraction
  -> KernelIR normalization
  -> verified plan generation
  -> SceneKernel registration tables
  -> ServiceGraph / Lifecycle / ValueStore / CommandCatalog / RuntimeQuery runtime
```

service runtime の data flow:

```text
declaration config
  -> service declaration contribution
  -> service plan entry
  -> entity-scoped service slot
  -> explicit resolve
```

UI runtime の data flow:

```text
UI declaration MB
  -> UI graph contribution
  -> verified UI node graph metadata
  -> service-owned graph
  -> selection/navigation/modal processing
```

value runtime の data flow:

```text
value declaration
  -> ValueKeyIR / ValueInitPlanIR
  -> ValueSchemaPlan / ValueInitPlan
  -> ValueStore
```

command runtime の data flow:

```text
command declaration
  -> CommandIR
  -> CommandCatalogPlan
  -> CommandCatalog runtime
  -> explicit executor dispatch
```

query runtime の data flow:

```text
query declaration
  -> RuntimeQueryIR
  -> RuntimeQueryPlan
  -> bounded query runtime
```

---

## Compile Boundary

必須 rule:

- new v2.1 runtime assembly は `Common/LTS` 非依存
- bridge は quarantine assembly のみ
- `EntityIdentityMB` と declaration MB は legacy installer assembly を参照しない
- UI runtime core は `Resolver`、`IScopeNode`、`LTSIdentityMB` を参照しない
- command/value runtime core は `CommandRunnerMB`、`BlackboardMB`、`BlackboardService` を参照しない
- target command/value/query runtime core は `IScopeNode` を参照してはならない

dependency direction:

- legacy bridge -> v2.1 runtime は許可
- v2.1 runtime -> legacy core は禁止

---

## Diagnostics and Failure Policy

次は structured failure にする:

- missing `EntityRef`
- duplicate entity registration
- duplicate `(EntityRef, ServiceId)` registration
- invalid service dependency
- invalid lifecycle target
- missing `ValueKeyId`
- invalid command payload schema
- invalid `RuntimeQueryId`
- UI graph inconsistency
- legacy API leakage
- command executor missing because of legacy bulk registration assumption
- value access that depends on stable-key runtime fallback
- UI navigation that depends on transform hierarchy search

禁止:

- null でごまかす
- fallback entity を作る
- fallback service を作る
- fallback value key / command key を invent する

---

## Performance Rule

v2.1 concrete architecture に対する必須性能 rule:

- service resolve は bounded lookup
- lifecycle dispatch は precomputed table
- value access は slot-based access
- command dispatch は table-driven lookup
- runtime query は explicit bounded lookup
- UI graph は dense handle access
- hot path で `GetComponentsInChildren` / `FindObjectsByType` / `Resources.Load` を使わない
- hot path で LINQ、reflection、string-key dictionary walk を使わない

性能のために explicit diagnostics を削ってはならない。

---

## 受け入れ基準

- `EntityIdentityMB` が `LTSIdentityMB` の完全置換として定義されている
- declaration MB が runtime mutation を持たないと定義されている
- `ServiceGraph` resolve が `EntityRef + ServiceId` で固定されている
- `ValueStore`、`Scalar`、`CommandCatalog`、`RuntimeQuery` が `ServiceGraph` と別 subsystem として定義されている
- `ProjectLTS` / `SceneLTS` / `EntityLTS` の差が service composition に吸収されると定義されている
- UI hierarchy が service-owned graph として定義されている
- `Common/LTS` 非依存の compile boundary が書かれている
- missing entity/service/key/query/legacy leakage が structured failure になる

---

## テストケース

| テストケース | 目的 | 検証 |
|---|---|---|
| `TC-V21-02-01` | `EntityIdentityMB` が legacy identity の完全置換であることを確認する | hierarchy 推定、installer、resolver registration が禁止されていなければならない |
| `TC-V21-02-02` | declaration MB が declarative input のみであることを確認する | runtime mutation と `InstallFeature` 相当を禁止していなければならない |
| `TC-V21-02-03` | `ServiceGraph` resolve authority が固定されていることを確認する | `Resolve(EntityRef, ServiceId)` と `TryResolve(EntityRef, ServiceId, out value)` が明記されていなければならない |
| `TC-V21-02-04` | command/value/scalar/query が独立 subsystem であることを確認する | `ServiceGraph` の下位 detail として書かれていてはならない |
| `TC-V21-02-05` | UI hierarchy authority が explicit graph にあることを確認する | `Transform.parent` が authority ではないと書かれていなければならない |
| `TC-V21-02-06` | compile boundary が legacy 非依存であることを確認する | `Common/LTS` 非依存と quarantine bridge rule が含まれていなければならない |
| `TC-V21-02-07` | 旧 `ProjectLTS` / `SceneLTS` / `EntityLTS` 区分が runtime species として残らないことを確認する | entity の差が metadata と service composition で表されると書かれていなければならない |

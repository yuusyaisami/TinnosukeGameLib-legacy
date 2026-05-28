# Kernel v2.1 Concrete Migration Architecture 仕槁E

## 斁E��スチE�Eタス

- 斁E�� ID: `02_ConcreteMigrationArchitectureSpec`
- 状慁E Draft
- 役割: v2.1 における実裁E��結�E移衁Eruntime 像、public contract、subsystem boundary、data flow、failure rule めEdecision-complete に定義する
- 篁E��: `ApplicationKernel` / `SceneKernel`、`EntityIdentityMB`、declaration MB、entity-scoped `ServiceGraph`、`Lifecycle`、`ValueStore`、`Scalar`、`CommandCatalog`、`RuntimeQuery`、UI subsystem、diagnostics、compile boundary
- 非目樁E legacy 共存�E長期運用、旧 `LTS` contract の再定義、最絁Egameplay tuning、editor UX の詳細

### 改訂メモ

こ�E斁E��は、v2.1 の architecture sketch めEimplementation-ready な runtime contract に落とすために作る、E

ここでの目皁E�E「何を作るか」を明確にすることであり、E
「どぁElegacy とし�Eらく共存させるか」を伸ばすことではなぁE��E

v2.1 は second kernel ではなぁE��E
v2 target kernel へ到達するため�E migration runtime である、E

今回の改訂では、次めEdecision-complete に固定する、E

- `Entity` を唯一の runtime ownership unit とすること
- `ApplicationKernel` ぁEDDOL root として game-wide authority を持ち、`SceneKernel` ぁEscene-local root として entity-scoped `ServiceGraph` を所有すること
- `ValueStore`、`Scalar`、`CommandCatalog`、`RuntimeQuery` めE`ServiceGraph` と別 subsystem に刁E��ること
- UI hierarchy めEservice-owned graph として扱ぁE��と
- `Common/LTS` 非依存�E compile boundary めEtarget path の忁E��条件にすること

---

## 所有篁E��

こ�E仕様が所有するもの:

- `ApplicationKernel` の role と `SceneKernel` との boundary
- `SceneKernel` の最終責勁E
- `Entity` の runtime model
- `EntityIdentityMB` の contract
- declaration MB の contract
- verified plan family の role
- entity-scoped `ServiceGraph` の contract
- `Lifecycle`、`ValueStore`、`Scalar`、`CommandCatalog`、`RuntimeQuery` の v2.1 での接続位置
- UI subsystem の runtime model
- diagnostics / failure policy
- compile boundary と dependency direction

こ�E仕様が所有しなぁE��の:

- v2 core semantics の再定義
- 吁Eservice の最終�E部アルゴリズム
- visual/inspector/editor の最絁EUX
- save payload format の最終仕槁E

02 は v2.1 の concrete runtime shape を所有する、E
01 ぁEdismantling order を所有するなら、E2 は replacement runtime の形を所有する、E
`ApplicationKernel` と `SceneKernel` の 2 層 composition の詳細は [06_KernelLayerCompositionSpec.md](06_KernelLayerCompositionSpec.md) で固定する、E

---

## 目皁E

v2.1 の concrete migration architecture の目皁E�E次の通り、E

```text
1. Entity を唯一の runtime ownership unit に統一する、E
2. service 接続を EntityRef + ServiceId に固定する、E
3. command / value / query / UI hierarchy を�E示 subsystem に刁E��する、E
4. declaration から verified plan を作り、runtime discovery を排除する、E
5. legacy main logic を残しても、legacy composition は残さなぁE��E
```

中忁E��ール:

```text
target path の authority は `ApplicationKernel`、`SceneKernel`、`EntityRef`、verified plan、typed identity にある、E
Transform hierarchy、runtime resolver、installer mutation、string/stable-key fallback にはなぁE��E
```

---

## 現行コードとの接続観測

こ�E仕様�E次の実裁E��力を前提にしてぁE��、E

- [RuntimeLifetimeScope.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs)
- [ProjectLifetimeScope.cs](../../GameLib/Script/Project/LTS/ProjectLifetimeScope.cs)
- [GlobalLifetimeScope.cs](../../GameLib/Script/Project/Global/LTS/GlobalLifetimeScope.cs)
- [LTSIdentityMB.cs](../../GameLib/Script/Kernel/Authoring/EntityIdentityMB.cs)
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

ここで見えてぁE��問顁E

- ownership unit ぁE`LTS` 種別に刁E��れすぎてぁE��
- service 接続が `Resolver.TryResolve` に依存してぁE��
- command と value ぁEinstaller / lifecycle / fallback を引きずってぁE��
- UI hierarchy ぁEscope / resolver / transform coupling を多く持つ
- `ButtonChannelHubService` ぁE`IScopeNode` と scope lifecycle handler を直接前提にしてぁE��
- `UISelectionService` と `UINavigationService` ぁE`IScopeNode` ベ�Eスの current/hover/selection owner を�E部 state として持ってぁE��
- `ModalStackChannelHubService` ぁE`IScopeNode` descendant 判定を modal ownership 判定に使ってぁE��

02 はこれらを 1 つの runtime model へ落とし直す、E

---

## Runtime Architecture Definition

### 1. Kernel pair

v2.1 runtime の authority は `ApplicationKernel` と `SceneKernel` の 2 層である、E
`ApplicationKernel` は DDOL の game-wide root、`SceneKernel` は scene-local root である、E

#### `ApplicationKernel`

`ApplicationKernel` が所有するもの:

- boot manifest / profile selection
- cross-scene shared service ownership
- persistent/global state coordination
- current `SceneKernel` の生�E・破棁E��琁E
- scene transition orchestration
- app-wide diagnostics sink

`ApplicationKernel` が所有しなぁE��の:

- scene-local entity registry
- scene-local UI graph
- per-scene entity-scoped service slots

`ApplicationKernel` は game-wide boot と shared ownership の orchestrator であり、汎用 DI container ではなぁE��E

`ApplicationKernel` の忁E��Epublic contract:

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
- runtime-created entity の spawn / despawn / pool mediation
- lifecycle dispatch entry
- command runtime access boundary
- value runtime access boundary
- runtime query access boundary
- diagnostics sink entry
- debug/provenance lookup entry

`SceneKernel` が所有しなぁE��の:

- gameplay service 冁E��アルゴリズム
- UI 表示階層そ�Eも�E
- final save persistence

`SceneKernel` は scene-local registry と orchestrator であり、汎用 DI container ではなぁE��E

`SceneKernel` の忁E��Epublic contract:

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

This section defines the v2.1 migration-layer runtime shape.
The entity-scoped `ServiceGraph` owned by `SceneKernel` preserves current entity-scoped runtime instances during migration, but it is not the same abstraction as the coarse-grained v2 target `ServiceGraph`.
`Resolve(EntityRef, ServiceId)` は public route だが、SceneKernel は current lease / generation を検証してから返す。
`EntityRef` 単独は liveness truth ではない。

typed wrapper はこ�E上に載せてよい、E
ただぁEauthority は常に `ApplicationKernel` / `SceneKernel` が持つ verified registration table にある、E

### 2. Entity

v2.1 では runtime ownership unit はすべて `Entity` である、E

廁E��する概念:

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

これら�E runtime species ではなく、旧 ownership 断牁E��して扱ぁE��E
新しい runtime ではすべて `Entity` に統一する、E

entity の差刁E�E次だけで表す、E

- `EntityRef`
- entity metadata
- registered services
- lifecycle plan association
- value store boundary
- runtime query exposure
- UI graph participation

Entity の違いは service composition で決まる、E
つまり旧 `ProjectLTS`、`SceneLTS`、`EntityLTS` の差は、型ではなく「どの service が登録されるか」によって表す、E

### 3. Typed identity

v2.1 runtime が扱ぁE��な identity:

- `EntityRef`
- `ServiceId`
- `CommandTypeId`
- `ValueKeyId`
- `RuntimeQueryId`
- `LifecyclePlanId`
- `UINodeHandle`

補助 metadata として持つも�E:

- authoring trace
- declaration source location
- service config trace

禁止:

- runtime stable key lookup めEauthority にすること
- runtime negative ID を作ること
- `Transform.parent` から owner identity を推測すること

---

## Verified Plan Model

v2.1 で言ぁE`plan` は、曖昧な設定ファイルではなぁE��E
declaration から生�EされめEimmutable な runtime input である、E

最低限、次の plan family を持つ、E

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
- `KernelIR` また�E同等�E正規化中間表現へ落とぁE
- duplicate、missing owner、invalid dependency を検証する
- runtime がそのまま引けめEdense table に投影する

plan の意味:

- runtime は plan を実行すめE
- runtime は scene/hierarchy を�E探索して structure を発見しなぁE
- validation failure は boot/register failure で止める

禁止:

- declaration MB が�E刁E�� plan めEmutate すること
- runtime ぁEplan めEsilent repair すること
- missing data めEfallback search で埋めること

---

## EntityIdentityMB Contract

`EntityIdentityMB` は `LTSIdentityMB` の完全置換である。

責務:

- `EntityRef` を明示する
- source trace を保持する
- entity classification metadata を持つ
- authoring declaration の root になる

補足:

- `SceneKernel` の entity registration table は `EntityIdentityMB` 1 件ごとの slot を持つ
- child GameObject に `EntityIdentityMB` が無い場合、その GameObject 自体は別 entity ではなく、親 entity の declarative subtree として扱う
- child GameObject に別の `EntityIdentityMB` がある場合は、親 entity とは別の entity として登録する

役割:

- GameObject を runtime ownership unit に結び付ける bridge
- declaration MB 群の root
- verified plan へ入る identity source

禁止:

- hierarchy を走査して entity kind を推定すること
- `IFeatureInstaller` を実装すること
- runtime service registration を行うこと
- dynamic registry auto-registration を行うこと

## Declaration MB Contract

service 用 MB は installer ではなぁEdeclaration MB とする、E

declaration MB が持つも�E:

- service declaration identity
- service config payload
- optional UI graph metadata
- optional lifecycle declaration metadata
- source trace

declaration MB がしてはならなぁE��と:

- runtime service めEnew すること
- `InstallFeature(...)` を持つこと
- `Resolver.TryResolve(...)` で依孁Eservice を探すこと
- `GetComponentsInChildren(...)` めE`GetComponentInParent(...)` で owner を確定すること

declaration MB は `EntityIdentityMB` 配下�E declarative input である、E
`SceneKernel` ぁEplan 構築時にこれらを雁E��、runtime は plan からのみ service を構築する、E

declaration MB の典型侁E

- `ButtonChannelHubMB`
- `UINavigationMB`
- `ModalStackChannelHubMB`
- `BlackboardMB` の移行征Ereplacement
- command declaration MB 群

これら�E見た目の inspector surface を維持してよい、E
ただぁEruntime authority は inspector ではなぁEverified plan にある、E

---

## Entity-Scoped ServiceGraph Contract

### 1. Ownership

runtime service owner は `SceneKernel` 配下�E entity-scoped `ServiceGraph` である、E

`ServiceGraph` が持つも�E:

- `EntityRef` ごとの service registration table
- `ServiceId` ごとの verified service slot
- dependency metadata
- lifetime metadata
- diagnostics provenance

### 2. Resolve API

authority API:

- `Resolve(EntityRef entity, ServiceId serviceId)`
- `TryResolve(EntityRef entity, ServiceId serviceId, out value)`

generated typed accessor は許可する、E
ただぁEgenerated accessor は上訁EAPI の sugar であり、別の runtime truth ではなぁE��E

禁止:

- type-based full scan
- `ResolveAll<T>()`
- hierarchy walk による parent fallback
- service missing 時�E silent repair
- `IScopeNode` を引数に取る resolve API
- `GameObject` / `Transform` めEkey にした runtime resolve

### 3. Dependency wiring

service-to-service wiring は verified dependency で行う、E

許可:

- same entity の explicit dependency
- explicitly declared shared/global service dependency
- explicit `RuntimeQuery` or `ValueStore` boundary access

禁止:

- `IRuntimeResolver` による ad-hoc resolve
- `IScopeNode.Parent` による upward search
- UI hierarchy を使っぁEimplicit resolve

### 4. Cardinality

v2.1 で許可する主要Ecardinality:

- one-per-entity instance
- one-per-scene manager
- explicitly bounded shared manager

未許可:

- unbounded runtime registration
- command execution count に比例すめEservice instance
- UI node count に比例して無制限増殖すめEservice

### 5. What ServiceGraph is not

`ServiceGraph` は次ではなぁE��E

- entity ごとの DI container
- command executor registry
- value key registry
- UI hierarchy authority
- runtime repair の窓口

`ServiceGraph` は entity-scoped service ownership と resolve の subsystem である、E
command/value/query/UI graph はそこへぶら下がる�Eではなく、�E示皁E��刁E��した subsystem として接続する、E

---

## Lifecycle Contract

`Lifecycle` は plan-driven dispatch とする、E

v2.1 での前提:

- entity registration 時に lifecycle target が確定してぁE��
- acquire / release / tick / async phase は table-driven
- service scan で handler を収雁E��なぁE

lifecycle target にできるも�E:

- service
- entity boundary
- explicit runtime query target

禁止:

- `IScopeAcquireHandler` scan
- `IScopeTickHandler` scan
- UI hierarchy 走査から tick 対象を集めること

UI を含む hot path では、phase table と cached handle を忁E��とする、E

lifecycle phase の代表:

- registration apply
- acquire
- initialize value
- activate
- tick
- deactivate
- release
- teardown

service ぁEtick されるかどぁE��は plan で確定してぁE��ければならなぁE��E
runtime scan で「tick 可能な object」を雁E��てはならなぁE��E

---

## Value Runtime Contract

### ValueStore

`ValueStore` は generic runtime value storage である、E

役割:

- `ValueKeyId` ベ�Eスの read/write
- revision 管琁E
- dirty signal
- entity-local あるぁE�E explicit shared store boundary
- `ValueInitPlan` 適用

禁止:

- stable-key runtime resolve
- missing key の runtime invention
- parent fallback write
- `BlackboardService` 風の upward search
- `VarKeyRegistryLocator` への runtime asset resolve

### Store boundary

v2.1 では store boundary は entity に結�E付く、E

許可:

- entity-local store
- explicitly shared store

禁止:

- hidden parent store fallback
- hierarchy-derived nearest blackboard

### Initialization

init は `ValueInitPlan` 経由のみ、E

禁止:

- `Construct`
- `Start`
- `OnAcquire`

から暗黙に value 初期化を始めること、E

### ValueStore API shape

最低限の API 期征E

```text
bool TryRead(ValueKeyId key, out ValueVariant value)
bool TryWrite(ValueKeyId key, in ValueVariant value)
uint GetRevision(ValueKeyId key)
bool TryGetMetadata(ValueKeyId key, out ValueKeyMetadata metadata)
```

debug label は metadata として持ってよい、E
しかぁEruntime authority めEstable key string に戻してはならなぁE��E

---

## Scalar Contract

`Scalar` は `ValueStore` の別 subsystem である、E

役割:

- float 専用 modulation
- baseline / additive / multiplicative / clamp / timed contribution
- verified binding
- explicit inherited endpoint

禁止:

- `BaseScalarService` 皁Eparent walk
- `Animator.StringToHash` 皁Eidentity authority
- scalar missing 時�E silent `0`
- hidden `DynamicValue<float>` read

scalar は `ValueStore` 上に重なめEspecialized runtime として扱ぁE��、E
generic value storage と同一 subsystem にしなぁE��E

典型用送E

- animation influence
- material effect intensity
- UI feedback amplitude
- time-based modulation

scalar binding めEplan で確定してぁE��ければならなぁE��E

---

## Command Runtime Contract

`CommandCatalog` は `ServiceGraph` ではなぁE��E

役割:

- `CommandTypeId` ベ�Eスの table-driven dispatch
- payload schema validation
- executor reference ownership
- command-local state boundary
- command diagnostics

禁止:

- `CommandRunnerMB` 皁Ebulk registration
- `IReadOnlyList<ICommandExecutor>` discovery
- stable-key runtime command resolve
- command executor めEservice discovery で見つけること

`CommandCatalog` ぁEservice に依存する場合�E、�E示 dependency として `ServiceGraph` を利用する、E
しかぁEcommand subsystem 自体を service collection として表現してはならなぁE��E

### CommandCatalog API shape

最低限の API 期征E

```text
bool TryDispatch(CommandTypeId typeId, in CommandPayload payload, in CommandExecutionContext context)
bool TryGetSchema(CommandTypeId typeId, out CommandPayloadSchema schema)
```

executor collection を外から�E挙して dispatch する構造めEtarget path に残してはならなぁE��E

---

## RuntimeQuery Contract

`RuntimeQuery` は explicit runtime lookup subsystem である、E

役割:

- verified query identity
- bounded lookup
- explicit target set

禁止:

- `ActorSourceFastResolver` 皁Ehierarchy fallback
- transform subtree search
- scope registry search
- service search との混吁E

新しい query path は `RuntimeQueryId` めEauthority にする、E
query ぁEactor/entity/UI node を返す場合でも、runtime search helper に戻してはならなぁE��E

`RuntimeQuery` は「何を返すか」を query identity で固定する、E
例えば:

- entity query
- service-backed actor query
- UI node query
- modal root query

generic search utility に戻してはならなぁE��E

---

## UI Subsystem Contract

### 1. Registration

次は `ServiceGraph` 登録対象である、E

- `ButtonChannelHubService`
- `UISelectionService`
- `UINavigationService`
- `ModalStackChannelHubService`

忁E��なら関連 service も同ぁErule に従う、E

こ�E 4 つは current codebase で hierarchy/state/input coupling が強ぁE��表例であるため、v2.1 では優先的に `ServiceGraph` registration + service-owned graph へ載せ替える、E

### 2. UI hierarchy ownership

UI hierarchy は service-owned graph として構築する、E

authority:

- verified plan
- declaration metadata
- explicit node handle

authority ではなぁE��の:

- `Transform.parent`
- scene hierarchy
- resolver-based neighbor search

### 3. UI graph model

UI subsystem は少なくとも次を�E部 graph に持つ、E

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

UI hot path で忁E��E

- cached handle
- dense table
- bounded traversal
- no per-frame hierarchy rebuild
- no broad search allocation

### 5. Value and command connection

UI service ぁEvalue めEcommand と接続する場吁E

- value は `ValueKeyId` access policy で結�E
- command は `CommandTypeId` / catalog dispatch で結�E
- `Resolver.TryResolve` に戻らなぁE

UI graph node は忁E��なめE`EntityRef` と別に `UINodeHandle` を持ってよい、E
ただぁEowner authority は依然として `EntityRef` 側にある、E

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

忁E��Erule:

- new v2.1 runtime assembly は `Common/LTS` 非依孁E
- bridge は quarantine assembly のみ
- `EntityIdentityMB` と declaration MB は legacy installer assembly を参照しなぁE
- UI runtime core は `Resolver`、`IScopeNode`、`LTSIdentityMB` を参照しなぁE
- command/value runtime core は `CommandRunnerMB`、`BlackboardMB`、`BlackboardService` を参照しなぁE
- target command/value/query runtime core は `IScopeNode` を参照してはならなぁE

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
- fallback value key / command key めEinvent する

---

## Performance Rule

v2.1 concrete architecture に対する忁E��性能 rule:

- service resolve は bounded lookup
- lifecycle dispatch は precomputed table
- value access は slot-based access
- command dispatch は table-driven lookup
- runtime query は explicit bounded lookup
- UI graph は dense handle access
- hot path で `GetComponentsInChildren` / `FindObjectsByType` / `Resources.Load` を使わなぁE
- hot path で LINQ、reflection、string-key dictionary walk を使わなぁE

性能のために explicit diagnostics を削ってはならなぁE��E

---

## 受け入れ基溁E

- `EntityIdentityMB` ぁE`LTSIdentityMB` の完�E置換として定義されてぁE��
- declaration MB ぁEruntime mutation を持たなぁE��定義されてぁE��
- `ServiceGraph` resolve ぁE`EntityRef + ServiceId` で固定されてぁE��
- `ValueStore`、`Scalar`、`CommandCatalog`、`RuntimeQuery` ぁE`ServiceGraph` と別 subsystem として定義されてぁE��
- `ProjectLTS` / `SceneLTS` / `EntityLTS` の差ぁEservice composition に吸収されると定義されてぁE��
- UI hierarchy ぁEservice-owned graph として定義されてぁE��
- `Common/LTS` 非依存�E compile boundary が書かれてぁE��
- missing entity/service/key/query/legacy leakage ぁEstructured failure になめE

---

## チE��トケース

| チE��トケース | 目皁E| 検証 |
|---|---|---|
| `TC-V21-02-01` | `EntityIdentityMB` ぁElegacy identity の完�E置換であることを確認すめE| hierarchy 推定、installer、resolver registration が禁止されてぁE��ければならなぁE|
| `TC-V21-02-02` | declaration MB ぁEdeclarative input のみであることを確認すめE| runtime mutation と `InstallFeature` 相当を禁止してぁE��ければならなぁE|
| `TC-V21-02-03` | `ServiceGraph` resolve authority が固定されてぁE��ことを確認すめE| `Resolve(EntityRef, ServiceId)` と `TryResolve(EntityRef, ServiceId, out value)` が�E記されてぁE��ければならなぁE|
| `TC-V21-02-04` | command/value/scalar/query が独竁Esubsystem であることを確認すめE| `ServiceGraph` の下佁Edetail として書かれてぁE��はならなぁE|
| `TC-V21-02-05` | UI hierarchy authority ぁEexplicit graph にあることを確認すめE| `Transform.parent` ぁEauthority ではなぁE��書かれてぁE��ければならなぁE|
| `TC-V21-02-06` | compile boundary ぁElegacy 非依存であることを確認すめE| `Common/LTS` 非依存と quarantine bridge rule が含まれてぁE��ければならなぁE|
| `TC-V21-02-07` | 旧 `ProjectLTS` / `SceneLTS` / `EntityLTS` 区刁E�� runtime species として残らなぁE��とを確認すめE| entity の差ぁEmetadata と service composition で表されると書かれてぁE��ければならなぁE|

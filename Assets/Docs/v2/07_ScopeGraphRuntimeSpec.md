# ScopeGraph Runtime 仕様

## 文書ステータス

- 文書 ID: `07_ScopeGraphRuntimeSpec`
- 状態: Draft
- 役割: Kernel v2 における runtime scope instance graph、scope identity、scope state、parent-child relationship、scope lifetime boundary を定義する
- 依存先:
  - [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md)
  - [01_KernelIRSpec.md](01_KernelIRSpec.md)
  - [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md)
  - [04_DependencyValidationSpec.md](04_DependencyValidationSpec.md)
  - [05_BootManifestAndProfileSpec.md](05_BootManifestAndProfileSpec.md)
- この仕様が消費するもの:
  - ScopeIR
  - ScopeGraphPlan
  - RuntimeQueryPlan 参照
  - LifecyclePlan 参照
  - ValueInitPlan 参照
  - ServiceGraphPlan 参照
  - `KernelDebugMap`
- この仕様を基盤としている文書:
  - [08_LifecyclePlanSpec.md](08_LifecyclePlanSpec.md)
  - [10_ValueSchemaAndStoreSpec.md](10_ValueSchemaAndStoreSpec.md)
  - [11_DebugMapAndDiagnosticsSpec.md](11_DebugMapAndDiagnosticsSpec.md)
  - [12_UnityAuthoringBridgeSpec.md](12_UnityAuthoringBridgeSpec.md)
  - [13_LegacyCompatBoundarySpec.md](13_LegacyCompatBoundarySpec.md)
  - [14_PerformanceBudgetAndRuntimeRulesSpec.md](14_PerformanceBudgetAndRuntimeRulesSpec.md)
  - [15_TestAndValidationSpec.md](15_TestAndValidationSpec.md)

### 所有範囲

本仕様は runtime scope instance の structure を所有する。
Unity Transform hierarchy の操作、entity lifecycle の内部、scene transition algorithm は所有しない。

本仕様が所有するもの:

- `ScopeHandle` contract
- runtime scope instance graph
- runtime parent-child relationship
- scope state machine
- scope creation / destruction contract
- attach / detach / reparent contract
- scope lifetime boundary
- scene / persistent scope boundary rule
- Unity object linkage metadata policy
- ServiceGraph、Lifecycle、ValueStore、RuntimeQuery に対する scope-local boundary contract
- pooling と generation invalidation
- ScopeGraph diagnostics
- ScopeGraph runtime performance rule
- scope structural mutation に対する threading rule

本仕様が所有しないもの:

- 最終的な Unity authoring component schema
- 最終的な Transform reparent implementation
- entity / part lifecycle の内部
- ServiceGraph cache implementation
- LifecycleDispatcher の step execution algorithm
- ValueStore storage layout
- RuntimeQuery index storage
- scene transition algorithm
- loading screen の見た目

---

## 目的

本仕様は、Kernel v2 における scope instance の runtime authority として ScopeGraph を定義する。

ScopeGraph は verified scope plan を消費し、runtime scope identity、parent-child relationship、state transition、lifetime boundary、Unity object linkage metadata を管理する。

ScopeGraph は、Transform hierarchy からの推測、scene-wide discovery、scope-build side effect を runtime structure management から取り除くために存在する。

ScopeGraph は scope structure を discovery しない。
verified scope structure を実行する。

07 の中心文は次である。

```text
ScopeGraph が runtime scope structure を所有する。
Unity hierarchy はそれにつながるだけで、定義はしない。
```

---

## 範囲

本仕様は次を定義する。

- ScopeGraph の runtime responsibility
- ScopeGraphPlan input contract
- `ScopeAuthoringId`、`ScopePlanId`、`ScopeHandle`、`UnityObjectLink` の区別
- runtime scope instance model
- parent-child relationship model
- scope state model
- scope creation / destruction contract
- attach / detach / reparent contract
- scene boundary policy
- persistent scope policy
- Unity object linkage policy
- scope-local ServiceGraph boundary
- scope-local Lifecycle boundary
- scope-local ValueStore boundary
- RuntimeQuery boundary
- pooling と generation invalidation
- ScopeGraph diagnostics
- ScopeGraph failure policy
- ScopeGraph performance constraint
- ScopeGraph threading rule
- ScopeGraph test case model と required test

---

## 非目標

本仕様は次を定義しない。

- 最終的な Unity authoring component schema
- 最終的な Transform reparent implementation
- entity / part lifecycle の内部
- ServiceGraph cache implementation
- LifecycleDispatcher の step execution algorithm
- ValueStore storage layout
- RuntimeQuery index storage
- scene transition algorithm
- loading screen の見た目

本仕様は、generic hierarchy service specification になってはならない。

---

## 他仕様との関係

| 仕様 | 関係 |
|---|---|
| [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md) | Transform hierarchy は kernel truth ではなく、runtime discovery は禁止される |
| [01_KernelIRSpec.md](01_KernelIRSpec.md) | ScopeIR、ScopeAuthoringId、ScopePlanId、そして `ScopeHandle` が KernelIR に存在しないという rule を定義する |
| [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md) | scope ownership、parent constraint、attachment rule の宣言的 input として ScopeContribution を定義する |
| [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md) | verified generation output として ScopeGraphPlan と RuntimeQueryPlan を生成する |
| [04_DependencyValidationSpec.md](04_DependencyValidationSpec.md) | scope dependency を事前検証する |
| [05_BootManifestAndProfileSpec.md](05_BootManifestAndProfileSpec.md) | verified boot root intent を与える |
| [08_LifecyclePlanSpec.md](08_LifecyclePlanSpec.md) | lifecycle step ordering と step execution を定義する |
| [10_ValueSchemaAndStoreSpec.md](10_ValueSchemaAndStoreSpec.md) | value schema / store / init の boundary を定義する |
| [11_DebugMapAndDiagnosticsSpec.md](11_DebugMapAndDiagnosticsSpec.md) | debug / diagnostics の共通基盤を定義する |
| [12_UnityAuthoringBridgeSpec.md](12_UnityAuthoringBridgeSpec.md) | Unity authoring bridge を定義する |
| [13_LegacyCompatBoundarySpec.md](13_LegacyCompatBoundarySpec.md) | legacy boundary を定義する |
| [14_PerformanceBudgetAndRuntimeRulesSpec.md](14_PerformanceBudgetAndRuntimeRulesSpec.md) | performance budget を定義する |
| [15_TestAndValidationSpec.md](15_TestAndValidationSpec.md) | test と CI gate を定義する |

---

## Assembly Definition と Compile Boundary の期待値

ScopeGraph の想定配置先は `GameLib.Kernel.Scopes` である。
詳細な dependency matrix は [17_AssemblyDefinitionAndCompileBoundarySpec.md](17_AssemblyDefinitionAndCompileBoundarySpec.md) が管理する。

07 に対する必須の compile-boundary ルール:

- `GameLib.Kernel.Scopes` は Unity Transform 操作、entity lifecycle 実装、scene transition algorithm から分離する
- core assembly は Unity 非依存のまま維持し、`noEngineReferences: true` を使うべきである
- scope structural mutation の runtime truth は explicit plan から来なければならない
- Transform-based nearest scope search を permanent mechanism として再導入してはならない

---

## 現行の Runtime 観測

現行の runtime 観測は、source code、migration note、profiling evidence に遡れなければならない。

### 観測のトレーサビリティ

| 観測 | 根拠の種類 | Scope 圧力 |
|---|---|---|
| project root creation が `BeforeSceneLoad` singleton discovery に結びついている | Source | 05, 07 |
| global root creation が project-root auto-creation、scene search、resource fallback に結びついている | Source | 05, 07, 13 |
| loading presentation boot が `SceneLifetimeScope` instance を scan し、duplicate cleanup を行っている | Source | 05, 07 |
| loading presentation の parent 選択が runtime で Global / Platform / Project root を探している | Source | 05, 07 |
| boot repair が `Resources.Load` や `new GameObject` fallback で起こりうる | Source | 05, 13 |
| persistent root が verified boot entry contract の外側で `DontDestroyOnLoad` singleton 挙動を使っている | Source | 05, 07 |

### 代表的なアンカー

- [ProjectLifetimeScope.cs](../../GameLib/Script/Project/LTS/ProjectLifetimeScope.cs) - `BeforeSceneLoad` boot entry、scene-wide root search、resource fallback、default `GameObject` creation
- [GlobalLifetimeScope.cs](../../GameLib/Script/Project/Global/LTS/GlobalLifetimeScope.cs) - project-first boot coupling、global root search、resource fallback、default `GameObject` creation
- [LoadingScreenService.cs](../../GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs) - loading scope discovery、duplicate cleanup、persistent parent search

### 現行の不足点

現行コードベースには、07 が target architecture から取り除くべき boot 挙動がまだ残っている。

- boot truth が runtime startup 中の scene state から推測されている
- missing boot root が fallback prefab load や default object creation で修復できる
- loading presentation が scene search と persistent-parent search に依存している
- duplicate root を rejection ではなく cleanup で処理できる
- boot input acceptance が 1 つの verified artifact set と 1 つの selected profile を中心に centralize されていない

---

## Core Problem

現行 runtime は、Transform hierarchy と runtime scope truth を混同している。

ScopeGraph が解くべき主要問題:

- runtime scope の identity を explicit に持つ
- parent-child relation を verified plan によって定義する
- state transition を explicit に持つ
- scene boundary と persistent boundary を明示する
- Unity object linkage を trace metadata として保持する
- scope mutation を deterministic にする
- scope failure を structured にする

ScopeGraph が修正すべきなのは、単なる「親を見つける方法」ではない。
runtime scope structure の truth source を Transform から verified scope plan へ移すことである。

---

## ScopeGraph Runtime Definition

ScopeGraph は runtime scope instance の authority である。

ScopeGraph が所有するもの:

- `ScopeHandle` issuance
- runtime scope instance table
- parent-child relationship table
- scope state
- scope generation safety
- scope lifetime boundary
- Unity object linkage metadata
- scope diagnostics

ScopeGraph が所有しないもの:

- service construction
- lifecycle step execution
- command execution
- value storage internals
- runtime query index implementation
- Transform hierarchy mutation

ScopeGraph は runtime scope identity、parent-child relationship、state、lifetime boundary、Unity object linkage metadata の authority である。
Transform hierarchy から scope structure を推測してはならない。

---

## ScopeGraphPlan Input Contract

ScopeGraph は、verified ScopeGraphPlan からのみ生成できる。

有効な ScopeGraphPlan には少なくとも次が必要である。

- ScopePlanId の set
- scope plan ごとの ScopeKind
- allowed parent rule
- required root scope definition
- required service graph reference
- required value init plan reference
- lifecycle plan reference
- scope event または indexing に必要な RuntimeQueryPlan reference
- source / DebugMap reference
- artifact header と verified artifact set metadata

ScopeGraph は ad-hoc な runtime scope type registration を受け付けてはならない。
scene object、prefab、component から不足した scope plan を再構成してはならない。

---

## Scope Identity Model

ScopeGraph は 4 つの identity layer を区別する。

- `ScopeAuthoringId`
- `ScopePlanId`
- `ScopeHandle`
- `UnityObjectLink`

それぞれの意味は固定されている。

- `ScopeAuthoringId` は authoring source を識別する
- `ScopePlanId` は verified された正規化済み scope definition を識別する
- `ScopeHandle` は live runtime scope instance を識別する
- `UnityObjectLink` は Unity object と authoring context への traceability を保持する

`ScopeAuthoringId` と `ScopePlanId` は runtime instance handle ではない。
`ScopeHandle` は authoring identifier ではない。
`UnityObjectLink` は identity ではなく metadata である。

禁止:

- `ScopeAuthoringId` を live runtime handle として使うこと
- `ScopePlanId` を pooled runtime slot identifier として使うこと
- `ScopeHandle` を KernelIR に保存すること
- runtime で fallback として `ScopePlanId` を生成すること
- Unity object reference を kernel scope identity として使うこと

---

## ScopeHandle Model

`ScopeHandle` は generation-safe でなければならない。

説明用スケッチ:

```csharp
public readonly struct ScopeHandle
{
    public readonly int Index;
    public readonly int Generation;
}
```

このスケッチは説明用であり、runtime API を最終確定するものではない。

ScopeGraph は次を検証しなければならない。

- index range
- generation match
- target scope が Destroyed ではないこと
- current state に対して requested operation が許可されること

stale な `ScopeHandle` が再利用された scope slot を指してはならない。
pool から再利用された destroyed scope slot は generation を増やさなければならない。

---

## Scope Instance Model

runtime scope instance は少なくとも次を含む、または参照する必要がある。

- `ScopeHandle`
- `ScopePlanId`
- authored なら `ScopeAuthoringId`
- `ScopeKind`
- parent `ScopeHandle` または explicit root marker
- child list または child index range
- state
- generation
- owner runtime domain
- Unity object link metadata
- scope が service を所有する場合の ServiceGraph reference
- scope が values を所有する場合の ValueStore reference
- lifecycle state または boundary reference
- DebugMap または diagnostics reference

runtime scope instance は MonoBehaviour instance を identity として扱ってはならない。
Unity object linkage は trace metadata であり、kernel identity ではない。

---

## Scope State Model

ScopeGraph は runtime scope state を explicit にモデル化しなければならない。

説明用スケッチ:

```csharp
public enum ScopeRuntimeState
{
    None = 0,
    Created = 10,
    Building = 20,
    Built = 30,
    Acquiring = 40,
    Active = 50,
    Releasing = 60,
    Inactive = 70,
    Destroying = 80,
    Destroyed = 90,
}
```

代表的な transition flow:

```text
None
  -> Created
  -> Built
  -> Acquiring
  -> Active
  -> Releasing
  -> Inactive
  -> Destroying
  -> Destroyed
```

07 は scope state が explicit であることを固定する。
08 は lifecycle step ordering の詳細を定義する。

scope state は、複数の独立した bool flag だけで表現してはならない。
invalid state transition は structured diagnostics を生む必要がある。

---

## Scope Parent / Child Model

scope の parent-child relationship は explicit な runtime data である。

child scope は次のどちらかを持たなければならない。

- valid な parent `ScopeHandle`
- ScopeGraphPlan で許可された explicit root marker

parent-child relationship は Transform.parent から推測してはならない。

ScopeGraph は、whole-graph scanning を行わずに効率的な child enumeration をサポートしなければならない。

必要な invariant:

- verified root でない限り parent が存在する
- parent の generation が一致する
- parent cycle が存在しない
- child は一度に 1 つの parent にのみ属する
- destroyed scope は parent になれない
- scene boundary rule が守られる

---

## Scope Creation Contract

scope creation には explicit request が必要である。

有効な `ScopeCreateRequest` には少なくとも次が必要である。

- `ScopePlanId`
- parent `ScopeHandle` または explicit root marker
- runtime domain
- 必要なら Unity object link
- creation policy
- diagnostics 用 source context

説明用スケッチ:

```csharp
public readonly struct ScopeCreateRequest
{
    public ScopePlanId PlanId;
    public ScopeHandle Parent;
    public ScopeCreateMode Mode;
    public UnityObjectLinkRef UnityLink;
    public SourceLocationId Source;
}
```

ScopeGraph は missing parent scope を自動生成してはならない。
scene を検索して owner scope を見つけてはならない。

---

## Scope Destruction Contract

scope destruction は決定論的でなければならない。

破棄は次を定義しなければならない。

- child destruction order
- lifecycle release boundary
- service graph disposal boundary
- value store disposal または persistence boundary
- runtime query invalidation boundary
- Unity link cleanup behavior
- generation invalidation

既定の流れ:

```text
destroy parent scope
  -> explicit policy に従って children を destroy または detach
  -> lifecycle release boundary を request
  -> scope-local services を dispose または release
  -> policy に従って scope-local values を dispose / persist / reset
  -> runtime query source state を invalidate
  -> ScopeHandle generation を invalidate
```

07 は destruction order を explicit にすることを求める。
08 と 10 が、それぞれの boundary で詳細な lifecycle と value semantics を所有する。

---

## Attach / Detach / Reparent Contract

ScopeGraph の reparent は kernel の parent-child relationship を変える。
ただし、下位仕様が bridge operation を定義しない限り、Unity Transform reparent を直接意味しない。

reparent operation は次を検証しなければならない。

- child が存在する
- 新しい parent が存在する
- cycle を導入しない
- scope kind の parent rule がこの relation を許可する
- scene boundary rule がこの move を許可する
- lifecycle state が operation を許可する

`Transform.SetParent` は ScopeGraph reparent の source ではない。

Detach は kernel parent-child relationship を削除する。
その結果 child が verified root になるのか、suspended scope になるのか、invalid operation になるのかを定義しなければならない。

---

## Scene Boundary Policy

ScopeGraph は scene domain boundary を定義しなければならない。

scene-local scope は、explicit な persistent policy で昇格されない限り、自分の scene domain より長生きしてはならない。

persistent scope は、verified weak handle、RuntimeQuery、または scene transition policy で表現されない限り、unload をまたいで scene-local scope への required direct reference を持ってはならない。

scene unload は、explicit verified policy がそれらを保持または remap しない限り、scene-local scope handle を invalid にしなければならない。

---

## Persistent Scope Policy

persistent root scope は verified boot input から作られる。

例:

- application root
- project root
- global root
- persistent presentation root
- loading presentation root

duplicate persistent root は error である。
runtime で 1 つを残して残りを消すことで解決してはならない。

07 は 05 から verified boot root intent を受け取る。
独自に persistent root を発明してはならない。

---

## Unity Object Linkage Policy

Unity object linkage は runtime scope と Unity object の traceability を保つ。

Unity object link には次を含めてよい。

- GameObject reference
- Transform reference
- component reference
- source asset identity
- scene path
- prefab instance metadata

Unity object link は scope identity ではない。

linked Unity object が破棄されても、ScopeGraph は Unity の fake-null 挙動を structure truth として silently 使ってはならない。

link は invalid になり、diagnostics または lifecycle policy が scope を destroy するか、detach するか、invalid とマークするかを決める必要がある。

ScopeGraph は Unity link から parent-child relationship を再構成してはならない。

---

## ServiceGraph Boundary

ScopeGraph は scope-local な ServiceGraph instance を所有または参照できる。

ScopeGraph は scope の service lifetime boundary を作成・破棄する責務を持つ。
ServiceGraph はその boundary 内の service resolution を担当する。

ServiceGraph は scope parent-child structure を決めてはならない。
ScopeGraph は ServiceGraph boundary API を通さずに service construction を内部で行ってはならない。

---

## Lifecycle Boundary

ScopeGraph は scope state を所有する。
LifecyclePlan は lifecycle step を所有する。

ScopeGraph は scope state boundary で lifecycle execution を request してよい。

例:

- scope acquire 時
- scope release 時
- scope destroy 時
- scope reset 時

ScopeGraph は service や component を scan して lifecycle handler を発見してはならない。

---

## ValueStore Boundary

ScopeGraph は scope-local ValueStore instance の lifetime を所有してよい。

ValueStore initialization は verified `ValueInitPlan` reference から行わなければならない。
ScopeGraph は stable value string や dynamic value expression を直接解釈してはならない。

ScopeGraph は Blackboard になってはならない。

---

## RuntimeQuery Boundary

RuntimeQuery system は scope instance を index してよい。

ScopeGraph は次のための explicit event または change record を提供しなければならない。

- scope created
- scope destroyed
- scope parent changed
- scope state changed
- Unity link changed

RuntimeQuery は query index を所有する。
ScopeGraph は source event を所有する。

ScopeGraph は、すべての gameplay lookup に対する generic runtime query API になってはならない。

---

## Pooling と Generation Policy

ScopeGraph は内部 slot を再利用してよい。

slot reuse は generation を増やさなければならない。
stale な `ScopeHandle` は validation に失敗しなければならない。

pool reset は次を定義しなければならない。

- state reset
- parent / child cleanup
- service boundary cleanup
- value boundary cleanup
- lifecycle state cleanup
- runtime query invalidation
- Unity link cleanup

pool から再利用された scope は、reset policy が explicit に定義しない限り、以前の owner、parent、service、value、Unity link を残してはならない。

---

## Diagnostics と DebugMap 要件

ScopeGraph diagnostics には少なくとも次を含める。

- error code
- `ScopeHandle`（利用可能なら）
- `ScopePlanId`
- `ScopeAuthoringId`（利用可能なら）
- `ScopeKind`
- parent `ScopeHandle`（関係するなら）
- current state
- owner module
- source location
- Unity object link（利用可能なら）
- selected profile

代表的 error code:

- `SCOPE_MISSING`
- `SCOPE_STALE_HANDLE`
- `SCOPE_INVALID_GENERATION`
- `SCOPE_PARENT_MISSING`
- `SCOPE_PARENT_KIND_INVALID`
- `SCOPE_PARENT_CYCLE`
- `SCOPE_INVALID_STATE_TRANSITION`
- `SCOPE_SCENE_BOUNDARY_VIOLATION`
- `SCOPE_UNITY_LINK_DESTROYED`
- `SCOPE_DUPLICATE_PERSISTENT_ROOT`

DebugMap と runtime mapping を合わせることで、scope failure は human-readable になる必要がある。
DebugMap は plan と authoring identity を解決する。
ScopeGraph runtime state は live `ScopeHandle` context を解決する。

---

## Failure Policy

required scope relationship が invalid な場合、ScopeGraph は fallback してはならない。

failure category:

- MissingParent
- StaleHandle
- InvalidGeneration
- InvalidState
- InvalidParentKind
- ParentCycle
- SceneBoundaryViolation
- DuplicatePersistentRoot
- UnityLinkInvalid
- ArtifactMismatch

failure boundary は operation に依存する。

- boot root scope failure: boot failure
- scene root failure: scene kernel failure
- scope-local operation failure: operation failure または scope failure
- stale handle: operation failure
- parent cycle: diagnostics error を伴う operation failure

failure は silent fallback を通して続行してはならない。

---

## Performance Policy

ScopeGraph は runtime hot path である。

target requirements:

- handle validation は O(1)
- parent lookup は O(1)
- child add / remove は bounded かつ allocation-conscious
- normal operation で scene-wide search をしない
- parent inference のために Transform traversal をしない
- owner inference のために component traversal をしない
- hot path で LINQ を使わない
- common handle validation path で allocation をしない

代表的 profiler marker:

- `ScopeGraph.CreateScope`
- `ScopeGraph.DestroyScope`
- `ScopeGraph.ValidateHandle`
- `ScopeGraph.Reparent`
- `ScopeGraph.SetState`
- `ScopeGraph.NotifyQuery`

---

## Threading と Main Thread Policy

ScopeGraph の structural mutation は既定で main-thread operation である。

Unity object linkage access には main thread が必要である。

read-only な handle validation は、下位仕様が synchronization を定義し、Unity object access を除外する場合に限り、main thread 外でも許可されうる。

ScopeGraph は、下位仕様が安全な proxy を明示しない限り、worker thread から `UnityEngine.Object` に触ってはならない。

---

## Legacy Compatibility Boundary

legacy `LifetimeScope` compatibility は [13_LegacyCompatBoundarySpec.md](13_LegacyCompatBoundarySpec.md) に属する。

ScopeGraph core は次に依存してはならない。

- `RuntimeLifetimeScopeBase`
- `BaseLifetimeScope`
- `IRuntimeResolver`
- legacy `ScopeFeatureInstallerUtility`
- Transform-based nearest scope search

allowed direction:

```text
LegacyScopeAdapter -> ScopeGraph: allowed
ScopeGraph -> LegacyScopeAdapter: forbidden
```

---

## Forbidden Patterns

target ScopeGraph runtime で禁止されるもの:

- Transform.parent からの parent inference
- `FindObjectsByType` による scope discovery
- `GetComponentsInChildren` による feature ownership detection
- component ancestor からの nearest scope search
- fallback による missing parent scope creation
- 最初を残して他を消す duplicate root cleanup
- MonoBehaviour instance を scope identity として使うこと
- KernelIR に runtime `ScopeHandle` を保存すること
- generation を増やさずに pooled slot を再利用すること
- ServiceResolver を通じて runtime scope を解決すること
- ScopeGraph を generic gameplay object registry として使うこと
- stale handle を silently 無視すること

---

## Test Case Model

各 ScopeGraph test case は次を定義しなければならない。

- Test ID
- Title
- ScopeGraphPlan fixture
- initial ScopeGraph state
- operation
- expected result
- expected diagnostics
- expected state transition
- expected handle validity
- 必要なら expected performance assertion

---

## Required Test Cases

### Identity と Handle のテスト

#### TC_SCOPE_ID_001_CreateScopeReturnsValidHandle

期待される結果:

- verified root scope の作成は valid な `ScopeHandle` を返す
- `ScopeHandle` は要求された `ScopePlanId` を解決する

#### TC_SCOPE_ID_002_AuthoringIdIsNotRuntimeHandle

期待される結果:

- `ScopeAuthoringId` を `ScopeHandle` として使うと domain mismatch diagnostic で失敗する

#### TC_SCOPE_ID_003_StaleHandleRejected

期待される結果:

- slot reuse 後、古い `ScopeHandle` は `SCOPE_STALE_HANDLE` で拒否される

### Parent と Child のテスト

#### TC_SCOPE_PARENT_001_CreateChildWithValidParent

期待される結果:

- child の parent は与えられた parent handle と等しい
- parent の child set に child が含まれる

#### TC_SCOPE_PARENT_002_MissingParentRejected

期待される結果:

- missing parent で child を作成すると `SCOPE_PARENT_MISSING` で失敗する

#### TC_SCOPE_PARENT_003_InvalidParentKindRejected

期待される結果:

- invalid parent-kind relationship は `SCOPE_PARENT_KIND_INVALID` で失敗する

#### TC_SCOPE_PARENT_004_ParentCycleRejected

期待される結果:

- reparent に cycle を導入すると `SCOPE_PARENT_CYCLE` で失敗する

#### TC_SCOPE_PARENT_005_TransformParentChangeDoesNotChangeScopeParent

期待される結果:

- Unity Transform の parent 変更だけでは ScopeGraph の parent data は変わらない

### State のテスト

#### TC_SCOPE_STATE_001_ValidStateTransition

期待される結果:

- valid な transition sequence `Created -> Built -> Active` は成功する

#### TC_SCOPE_STATE_002_InvalidStateTransitionRejected

期待される結果:

- invalid transition `Destroyed -> Active` は `SCOPE_INVALID_STATE_TRANSITION` で失敗する

#### TC_SCOPE_STATE_003_DestroyedScopeCannotBeParent

期待される結果:

- destroyed parent scope は child creation を受け付けない

### Creation と Destruction のテスト

#### TC_SCOPE_CREATE_001_NoSceneSearchDuringCreate

期待される結果:

- create path は `FindObjectsByType` を実行しない
- create path は ownership inference のために Transform parent traversal を実行しない

#### TC_SCOPE_DESTROY_001_DestroyInvalidatesChildren

期待される結果:

- parent destruction は policy に従って children を invalid にする、または明示的に処理する

#### TC_SCOPE_DESTROY_002_DestroyDisposesScopeBoundaries

期待される結果:

- service / value / lifecycle / query boundary は explicit policy に従って clean up される

### Reparent のテスト

#### TC_SCOPE_REPARENT_001_ReparentValidScope

期待される結果:

- child は validation 後に ParentA から削除され、ParentB に attach される

#### TC_SCOPE_REPARENT_002_ReparentAcrossInvalidSceneBoundaryRejected

期待される結果:

- invalid scene boundary の reparent は `SCOPE_SCENE_BOUNDARY_VIOLATION` で失敗する

#### TC_SCOPE_REPARENT_003_SetParentIsNotScopeReparent

期待される結果:

- Unity Transform.SetParent は explicit bridge command なしには ScopeGraph relation を mutate しない

### Persistent Root のテスト

#### TC_SCOPE_ROOT_001_CreateRequiredRootFromBootPlan

期待される結果:

- boot input で定義された required persistent root は正常に作成される

#### TC_SCOPE_ROOT_002_DuplicatePersistentRootRejected

期待される結果:

- duplicate persistent root は `SCOPE_DUPLICATE_PERSISTENT_ROOT` で失敗する

#### TC_SCOPE_ROOT_003_NoKeepFirstDestroyRest

期待される結果:

- duplicate persistent root は自動 keep-first cleanup を引き起こさない

### Unity Link のテスト

#### TC_SCOPE_UNITY_001_UnityLinkIsTraceMetadata

期待される結果:

- scope identity は Unity object reference ではなく `ScopeHandle` のままである

#### TC_SCOPE_UNITY_002_DestroyedUnityLinkDetected

期待される結果:

- destroyed linked Unity object は link を invalid にし、explicit な policy outcome を生む

#### TC_SCOPE_UNITY_003_NoParentInferenceFromUnityLink

期待される結果:

- Unity link の Transform parent は scope parentage を定義しない

### Boundary のテスト

#### TC_SCOPE_BOUNDARY_001_ServiceGraphDoesNotOwnScopeParent

期待される結果:

- ServiceGraph は scope parent-child structure を決定も変更もできない

#### TC_SCOPE_BOUNDARY_002_ScopeGraphDoesNotExecuteLifecycleDirectly

期待される結果:

- ScopeGraph は lifecycle boundary work を request するが、step execution は LifecycleDispatcher が所有する

#### TC_SCOPE_BOUNDARY_003_ScopeGraphDoesNotResolveValueKeys

期待される結果:

- ScopeGraph は stable value key を直接解釈できない

#### TC_SCOPE_BOUNDARY_004_ScopeGraphDoesNotBecomeRuntimeQueryRegistry

期待される結果:

- category による gameplay lookup は RuntimeQuery に委譲されるか、拒否される

### Pooling のテスト

#### TC_SCOPE_POOL_001_ReusedSlotIncrementsGeneration

期待される結果:

- slot reuse は generation を増やし、古い handle を invalid にする

#### TC_SCOPE_POOL_002_ResetClearsParentChildrenAndLinks

期待される結果:

- reset は parent、children、Unity link、scope-local boundary reference を消す

### Diagnostics のテスト

#### TC_SCOPE_DIAG_001_StaleHandleDiagnosticReadable

期待される結果:

- stale handle diagnostic には、利用可能なら handle、generation、ScopePlanId、authoring source が含まれる

#### TC_SCOPE_DIAG_002_MissingDebugMapInDevelopment

期待される結果:

- Development profile で required DebugMap coverage がない failure は error として扱われる

### Performance のテスト

#### TC_SCOPE_PERF_001_ValidateHandleNoAllocation

期待される結果:

- repeated handle validation は normal path で allocation しない

#### TC_SCOPE_PERF_002_CreateScopeDoesNotScanHierarchy

期待される結果:

- create path は `GetComponentsInChildren` も Transform parent traversal も行わない

#### TC_SCOPE_PERF_003_ChildEnumerationDoesNotScanAllScopes

期待される結果:

- child enumeration cost は total scope count ではなく child count によって bounded である

---

## 受け入れ条件

本仕様が完成していると見なす条件は次のとおり。

- ScopeGraph runtime responsibility
- ScopeGraphPlan input contract
- `ScopeAuthoringId`、`ScopePlanId`、`ScopeHandle`、`UnityObjectLink` の区別
- `ScopeHandle` の generation safety
- runtime scope instance model
- scope state model
- parent-child relationship model
- scope creation contract
- scope destruction contract
- attach / detach / reparent contract
- scene boundary policy
- persistent scope policy
- Unity object linkage policy
- ServiceGraph boundary
- Lifecycle boundary
- ValueStore boundary
- RuntimeQuery boundary
- pooling と generation policy
- diagnostics / DebugMap 要件
- failure policy
- performance policy
- threading policy
- legacy compatibility boundary
- forbidden pattern
- ScopeGraph test case model
- required ScopeGraph test

07 が、Transform hierarchy management specification、generic hierarchy service、あるいは ServiceGraph / LifecyclePlan / ValueStore / RuntimeQuery の代替になったら未完成である。

---

## テストケース

| テストケース | 目的 | 検証 |
|---|---|---|
| TC-07-01 | Transform hierarchy ではなく ScopeGraph が scope structure の runtime authority であることを確認する。 | Purpose、ScopeGraph Runtime Definition、Forbidden Patterns の節で、Transform-parent inference と scene / component discovery を禁止していること。 |
| TC-07-02 | `ScopeAuthoringId`、`ScopePlanId`、`ScopeHandle`、`UnityObjectLink` が混同されないことを確認する。 | Scope Identity Model、ScopeHandle Model、Scope Instance Model の節で、identity layer を分離し、stale または cross-domain の handle use を拒否していること。 |
| TC-07-03 | scope creation、destruction、reparent が explicit で verified な structure を要求することを確認する。 | ScopeGraphPlan Input Contract、Scope Creation Contract、Scope Destruction Contract、Attach / Detach / Reparent Contract の節で、verified input、explicit parent rule、fallback parent creation の不在を要求していること。 |
| TC-07-04 | ServiceGraph、Lifecycle、ValueStore、RuntimeQuery、Unity linkage との boundary が explicit であることを確認する。 | boundary の節で ownership / non-ownership を明確にし、ScopeGraph が service resolution、lifecycle execution、Blackboard、generic query registry にならないようにしていること。 |
| TC-07-05 | pooling、scene boundary、persistent root が fail closed であることを確認する。 | Scene Boundary Policy、Persistent Scope Policy、Pooling and Generation Policy、Failure Policy の節で、stale handle、scene leak、duplicate persistent root を silent cleanup なしで拒否していること。 |
| TC-07-06 | diagnostics、performance、threading、legacy rule が runtime contract の一部であることを確認する。 | Diagnostics and DebugMap Requirements、Performance Policy、Threading and Main Thread Policy、Legacy Compatibility Boundary の節が explicit で testable なままであること。 |

---

## 最終見解

Transform は truth ではない。
ScopeGraph が truth である。

ScopeGraph は runtime scope structure を所有する。
Unity hierarchy はそれにつながるだけで、定義はしない。

`ScopeAuthoringId`、`ScopePlanId`、`ScopeHandle`、`UnityObjectLink` を分離することで、kernel は Transform inference や legacy nearest-scope discovery に戻らずに、速度、安全性、debuggability を保てる。

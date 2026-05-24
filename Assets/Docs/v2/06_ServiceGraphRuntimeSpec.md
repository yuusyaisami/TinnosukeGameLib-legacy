# ServiceGraph Runtime 仕様

## 文書ステータス

- 文書 ID: `06_ServiceGraphRuntimeSpec`
- 状態: Draft
- 役割: Kernel v2 における runtime service resolution、service eligibility、service lifetime boundary、service failure rule を定義する
- 依存先:
  - [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md)
  - [01_KernelIRSpec.md](01_KernelIRSpec.md)
  - [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md)
  - [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md)
  - [04_DependencyValidationSpec.md](04_DependencyValidationSpec.md)
  - [05_BootManifestAndProfileSpec.md](05_BootManifestAndProfileSpec.md)
- この仕様が消費するもの:
  - ServiceIR
  - ServiceGraphPlan
  - ScopeGraphPlan 参照
  - RuntimeQueryPlan 参照
  - `KernelDebugMap`
- この仕様を基盤としている文書:
  - [07_ScopeGraphRuntimeSpec.md](07_ScopeGraphRuntimeSpec.md)
  - [08_LifecyclePlanSpec.md](08_LifecyclePlanSpec.md)
  - [09_CommandCatalogRuntimeSpec.md](09_CommandCatalogRuntimeSpec.md)
  - [10_ValueSchemaAndStoreSpec.md](10_ValueSchemaAndStoreSpec.md)
  - [11_DebugMapAndDiagnosticsSpec.md](11_DebugMapAndDiagnosticsSpec.md)
  - [12_UnityAuthoringBridgeSpec.md](12_UnityAuthoringBridgeSpec.md)
  - [13_LegacyCompatBoundarySpec.md](13_LegacyCompatBoundarySpec.md)
  - [14_PerformanceBudgetAndRuntimeRulesSpec.md](14_PerformanceBudgetAndRuntimeRulesSpec.md)
  - [15_TestAndValidationSpec.md](15_TestAndValidationSpec.md)

### 所有範囲

本仕様は、verified された coarse-grained service の runtime service resolution を所有する。
scope structure、runtime query index、lifecycle execution、command dispatch、value storage の内部までは所有しない。

本仕様が所有するもの:

- ServiceGraph runtime definition
- service eligibility rule
- non-service runtime object classification
- ServiceGraphPlan runtime input contract
- service identity と service contract の rule
- service lifetime と cardinality の rule
- service factory の rule
- required / optional service resolution semantics
- slot / cache model の要件
- ServiceGraph 内の dependency resolution rule
- scope-local service boundary rule
- service diagnostics / DebugMap 要件
- service failure behavior
- service performance / memory rule
- service threading / shutdown rule
- service runtime における legacy boundary rule

本仕様が所有しないもの:

- scope parent-child structure
- RuntimeQuery の storage または lookup semantics
- lifecycle step execution
- command catalog dispatch
- value key lookup または value storage layout
- Unity authoring schema
- boot manifest selection

06 は runtime service authority である。
しかし、汎用 DI container の代用品ではない。

---

## 目的

本仕様は、ServiceGraph を定義する。ServiceGraph は ServiceGraphPlan から導かれた verified service の runtime resolver である。

ServiceGraph は、明示された service structure を実行するために存在する。
欠落した runtime structure を discovery したり、任意の behavior を集めたり、不完全な plan を repair したりするために存在するのではない。

06 の中心文は次である。

```text
ServiceGraph は coarse-grained で verified な service を解決する。
すべての runtime object を service として扱うわけではない。
```

ServiceGraph は汎用 DI container ではない。
ServiceGraph は次のものになってはならない。

- lifecycle handler collector
- command executor registry
- runtime object registry
- per-entity service container
- channel player factory registry
- value / key resolver
- fallback resolver

---

## 範囲

本仕様は次を定義する。

- ServiceGraph の runtime responsibility
- service eligibility と non-eligibility
- non-service runtime object の分類
- ServiceGraphPlan input contract
- service identity と service contract rule
- service lifetime と cardinality rule
- service factory rule
- required / optional service resolution behavior
- scope-local service boundary rule
- entity / per-target service の禁止
- hub / channel / player の分類
- lifecycle / command / runtime query / value boundary
- service 向け Unity object linkage constraint
- service diagnostics / DebugMap 要件
- service failure policy
- service performance / memory / threading / shutdown policy
- service runtime の test case model と required test

---

## 非目標

本仕様は次を定義しない。

- 最終的な ScopeGraph storage
- RuntimeQuery の index 形状
- lifecycle step の順序または実行アルゴリズム
- command dispatch または payload execution rule
- ValueStore の layout または serialization
- Unity object authoring schema
- boot manifest の形状
- scene transition algorithm

本仕様は ServiceGraph を次のものに変えてはならない。

- generic runtime object directory
- generic hierarchy query API
- RuntimeQuery の代替
- ValueStore の代替

---

## 他仕様との関係

| 仕様 | 関係 |
|---|---|
| [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md) | target kernel では service discovery を主な composition mechanism にしてはならない |
| [01_KernelIRSpec.md](01_KernelIRSpec.md) | ServiceIR、identity domain、dependency edge、source location を定義する |
| [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md) | service contribution を宣言的な input として定義する |
| [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md) | verified ServiceGraphPlan を生成する |
| [04_DependencyValidationSpec.md](04_DependencyValidationSpec.md) | service dependency を事前検証する |
| [05_BootManifestAndProfileSpec.md](05_BootManifestAndProfileSpec.md) | boot-time の verified service input を選ぶ |
| [07_ScopeGraphRuntimeSpec.md](07_ScopeGraphRuntimeSpec.md) | scope instance graph の runtime authority を定義する |
| [08_LifecyclePlanSpec.md](08_LifecyclePlanSpec.md) | lifecycle step を定義する |
| [09_CommandCatalogRuntimeSpec.md](09_CommandCatalogRuntimeSpec.md) | command runtime を定義する |
| [10_ValueSchemaAndStoreSpec.md](10_ValueSchemaAndStoreSpec.md) | value schema と storage を定義する |
| [11_DebugMapAndDiagnosticsSpec.md](11_DebugMapAndDiagnosticsSpec.md) | diagnostics / DebugMap の共通基盤を定義する |
| [12_UnityAuthoringBridgeSpec.md](12_UnityAuthoringBridgeSpec.md) | Unity authoring bridge を定義する |
| [13_LegacyCompatBoundarySpec.md](13_LegacyCompatBoundarySpec.md) | legacy boundary を定義する |
| [14_PerformanceBudgetAndRuntimeRulesSpec.md](14_PerformanceBudgetAndRuntimeRulesSpec.md) | performance budget を定義する |
| [15_TestAndValidationSpec.md](15_TestAndValidationSpec.md) | test と CI gate を定義する |

06 は verified coarse-grained service を解決する runtime の authority である。

---

## Assembly Definition と Compile Boundary の期待値

ServiceGraph の想定配置先は `GameLib.Kernel.Services` である。
詳細な dependency matrix は [17_AssemblyDefinitionAndCompileBoundarySpec.md](17_AssemblyDefinitionAndCompileBoundarySpec.md) が管理する。

06 に対する必須の compile-boundary ルール:

- `GameLib.Kernel.Services` は runtime mutation code と分離する
- core assembly は Unity 非依存のまま維持し、`noEngineReferences: true` を使うべきである
- runtime service implementation、feature implementation、legacy adapter は別 assembly に分ける
- service assembly に installer-style mutation を再導入してはならない

core assembly で service truth を完結させることができないなら、設計を見直す必要がある。

---

## 現行の Service Debt 観測

現行の service debt 観測は、source code、design review note、migration evidence に遡れなければならない。

### 観測のトレーサビリティ

| 観測 | 根拠の種類 | Service 圧力 |
|---|---|---|
| legacy runtime service architecture は複数の責務を混在させている | Source | 06, 07, 08, 09, 10 |
| service identity と raw C# type identity が混ざっている | Source | 06 |
| coarse-grained service と per-target runtime object が混在している | Source | 06, 07 |
| lifecycle participation と service resolution が混ざっている | Source | 06, 08 |
| command routing と shared service access が混ざっている | Source | 06, 09 |
| runtime query behavior と service dependency が混ざっている | Source | 06, 07 |
| dynamic value access と service dependency が混ざっている | Source | 06, 10, 10-2 |
| scope creation と service construction が混ざっている | Source | 06, 07 |
| required dependency resolution と fallback repair が混ざっている | Source | 06, 13 |

### 代表的なアンカー

- [RuntimeResolverHub.cs](../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs) - registration table、`CollectHandlers<THandler>()`、`CollectAll(...)`、`RuntimeAcquireReleaseDispatcher`
- [RuntimeLifetimeScope.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs) - build-time installer discovery、resolver construction、lifecycle extraction
- [ScopeFeatureInstallerUtility.cs](../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs) - nearest-scope filtering と installer discovery
- [LoadingScreenService.cs](../../GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs) - loading behavior と scene search / persistent-parent repair
- [CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs) - bulk executor registration と lifecycle wiring
- [CommandExecutorRegistry.cs](../../GameLib/Script/Common/Commands/VNext/Core/CommandExecutorRegistry.cs) - executor lookup と invalid-ID behavior
- [ModalStackChannelHubService.cs](../../GameLib/Script/Project/UI/Core/ModalStackChannel/ModalStackChannelHubService.cs) - hub だが coarse-grained service 候補
- [TooltipChannelHubService.cs](../../GameLib/Script/Project/UI/Core/Tooltip/TooltipChannelHubService.cs) - mixed boundary
- [MeshChannelHubService.cs](../../GameLib/Script/Project/Scene/Channels/Mesh/MeshChannelHubService.cs) - hub service 候補
- [AnimationSpriteHubService.cs](../../GameLib/Script/Project/Scene/Channels/SpriteAnimation/AnimationSpriteHubService.cs) - mixed boundary

### 現行の不足点

現行コードベースには、target architecture で ServiceGraph が解消すべき debt が残っている。

- service count が entity count とともに増える設計が残っている
- service eligibility が十分に明示されていない
- per-target runtime object と coarse-grained service の境界が曖昧である
- optional service が silent fallback に落ちうる
- service resolution と runtime query がまだ混ざりうる
- diagnostics / DebugMap の透明性が不足している

---

## Core Problem

legacy runtime service architecture は、複数の責務を 1 つの path に押し込めている。

混在している責務:

- service identity と raw C# type identity
- coarse-grained shared service と per-target runtime object
- lifecycle participation と service resolution
- command routing と shared service access
- runtime query behavior と service dependency
- dynamic value access と service dependency
- scope creation と service construction
- required dependency resolution と fallback repair

target ServiceGraph はこれらを分離しなければならない。

entity count、target count、player runtime count に応じて service count が増えるなら、その設計は既定で誤っている。

---

## ServiceGraph Runtime Definition

ServiceGraph は、ServiceGraphPlan で定義された verified service の runtime resolver である。

ServiceGraph が所有するもの:

- verified graph 内の service slot identity
- verified lifetime boundary 内の service construction
- required / optional service resolution
- service graph 内の dependency ordering
- graph boundary 内の service cache lifetime
- declared service instance の disposal
- service diagnostics

ServiceGraph が所有しないもの:

- scope parent-child structure
- runtime query index
- lifecycle step discovery
- command executor collection
- value key lookup
- Unity scene search
- broad runtime object directory

ServiceGraph は、verified ServiceGraphPlan input のみを実行できる。

次のことをしてはならない。

- runtime で新しい service を登録する
- MonoBehaviour や interface を scan して service を発見する
- fallback factory で missing service を修復する
- 任意 contract の behavior list を集める
- service lookup を通じて runtime object を解決する

### M6.1 Service Eligibility Classification Rules and Service-Boundary Inventory

M6.1 は、ServiceGraph が何を表現してよいかを固定する。
slot、cache、factory の作業を complete と見なす前に、ここで解決しなければならない。

分類 rule は意図的に狭い。

```text
Shared, explicit, validated runtime infrastructure may be a service.
Per-target runtime objects are not services by default.
```

service eligibility は次のすべてで決まる。

1. 候補に stable な `ServiceId` がある。
2. 候補に explicit な owner module がある。
3. 候補に verified な lifetime domain がある。
4. 候補が runtime search ではなく ServiceGraphPlan から見える。
5. 候補の dependency が runtime 前に宣言・検証されている。
6. 候補が verified factory または verified prebuilt source で構築される。
7. 候補の failure を DebugMap と structured diagnostics で診断できる。
8. 候補の cardinality が service-slot ownership を正当化できる程度に coarse である。

上の rule を満たす場合に service eligible となるカテゴリ:

- kernel-level coarse services
- project-level coarse services
- scene-level coarse services
- authored-scope coarse services

既定で service eligible ではないもの:

- per-entity runtime object
- per-part runtime object
- per-renderer runtime object
- per-tooltip runtime object
- per-channel-player runtime object
- per-mesh-track runtime object
- per-animation-player runtime object
- transient command execution frame
- dynamic value evaluation context
- pooled runtime object instance

ServiceGraph は、これらの non-service object に synthetic `ServiceId` を割り当てて、取得を便利にするためだけに使ってはならない。

現在の migration anchor に対する boundary inventory:

| Current anchor | M6.1 classification | Lifetime / cardinality | その理由 | 必要な下流の扱い |
|---|---|---|---|---|
| [RuntimeResolverHub.cs](../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs) | legacy boundary、target service ではない | n/a | registration table、collection-style discovery、runtime resolver coupling は verified coarse-grained service ownership の対極にある | legacy boundary に quarantine し、ServiceGraph truth に昇格させない |
| [RuntimeLifetimeScope.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs) | legacy scope/build boundary、target service ではない | n/a | build-time installer discovery、resolver construction、lifecycle extraction が 1 つの path に混ざっている | verified boot と explicit scope/runtime boundary に分割してから target ServiceGraph に使う |
| [ScopeFeatureInstallerUtility.cs](../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs) | legacy discovery boundary、target service ではない | n/a | nearest-scope filtering と installer discovery は transform 由来の ownership leak である | discovery behavior を quarantine し、scope ownership を explicit のままにする |
| [LoadingScreenService.cs](../../GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs) | boot-adjacent legacy boundary、target service ではない | n/a | loading behavior が scene search と persistent-parent repair flow に依存している | boot と scope boundary が entry path を所有するまで target ServiceGraph の外に置く |
| [CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs) | legacy command bootstrap boundary、target service ではない | n/a | bulk executor registration と lifecycle wiring が registration-driven のままである | ServiceGraph から除外する。command ownership は M9 と explicit lifecycle boundary に属する |
| [CommandExecutorRegistry.cs](../../GameLib/Script/Common/Commands/VNext/Core/CommandExecutorRegistry.cs) | legacy command-dispatch boundary、target service ではない | n/a | executor lookup と invalid-ID behavior が registry 型の command dispatch のままである | ServiceGraph から外し、command truth は M9 に置く |
| [ModalStackChannelHubService.cs](../../GameLib/Script/Project/UI/Core/ModalStackChannel/ModalStackChannelHubService.cs) | service candidate、hub service | OnePerProject または OnePerScene; bounded | shared state と explicit ownership boundary を持つ coarse-grained UI hub である | hub identity だけをモデル化し、layer/root state は hub 内に置く |
| [TooltipChannelHubService.cs](../../GameLib/Script/Project/UI/Core/Tooltip/TooltipChannelHubService.cs) | mixed boundary、service eligible にする前に split が必要 | split まで n/a | coarse-grained hub だが runtime query、value access、player ownership を混在させている | split 後に hub だけを ServiceGraph に残し、player runtime と lookup path を service truth から外す |
| [MeshChannelHubService.cs](../../GameLib/Script/Project/Scene/Channels/Mesh/MeshChannelHubService.cs) | service candidate、hub service | OnePerScene; bounded | 多数の player runtime を所有するが、player 自体は service identity ではない | `MeshChannelPlayerRuntime` は hub-owned かつ non-ServiceId-backed のままにする |
| [AnimationSpriteHubService.cs](../../GameLib/Script/Project/Scene/Channels/SpriteAnimation/AnimationSpriteHubService.cs) | mixed boundary、service eligible にする前に split が必要 | split まで n/a | hub service、material/provider behavior、lifecycle、player runtime ownership が混ざっている | target migration の前に service 宣言、lifecycle 宣言、player runtime ownership を分割する |

上の inventory は M6.1 の初期 seed set である。
新しい candidate は、service boundary を分類する必要があるとき、または split requirement を明示する必要があるときだけ追加する。
mixed boundary とマークされた row は、split が完了するまで ServiceGraph に非適格である。

---

## Service Eligibility Model

runtime object が ServiceGraph の service になれるのは、必須 eligibility rule をすべて満たすときだけである。

必須 rule:

1. stable な `ServiceId` を持つ。
2. explicit な owner module を持つ。
3. verified な lifetime domain を持つ。
4. 存在が runtime search ではなく ServiceGraphPlan から分かる。
5. dependency が宣言・検証されている。
6. verified factory または verified prebuilt source によって生成される。
7. failure を DebugMap と stable diagnostics で診断できる。
8. 期待 cardinality が service slot / cache ownership を正当化できる程度に coarse である。

どれか 1 つでも満たさない service candidate は service ではない。

既定で ServiceGraph service にしてはならないもの:

- すべての entity instance
- すべての part instance
- すべての tooltip instance
- すべての channel player
- すべての mesh track runtime
- すべての animation player runtime
- transient command execution frame
- dynamic value evaluation context
- per-target visual mutation session
- pooled runtime object instance

狙いは単純である。

```text
Shared, explicit, validated runtime infrastructure may be a service.
Per-target runtime objects are not services by default.
```

---

## Non-Service Runtime Object Model

すべての長寿命 runtime object が service とは限らない。

次のどれかに当てはまるなら、runtime object は ServiceGraph の外でモデル化すべきである。

- 特定の hub または service が所有している
- channel、tag、handle、content data から動的に作られる
- scope ごと、entity ごとに多数存在する
- global または graph-level の dependency resolution を必要としない
- service lifetime から独立に pool、reset、recycle されるべきである
- tag、handle、RuntimeQuery でアドレスされる

例:

- `TooltipChannelPlayerRuntime`
  - owner: tooltip hub service
  - identity: channel tag または local handle
  - ServiceId-backed ではない
- `MeshChannelPlayerRuntime`
  - owner: mesh hub service
  - identity: channel tag
  - ServiceId-backed ではない
- modal root entry または resolved layer state
  - owner: modal stack hub
  - identity: local modal root または UI root handle
  - ServiceId-backed ではない
- animation player runtime
  - owner: animation sprite hub
  - identity: local player tag または view linkage
  - ServiceId-backed ではない

ServiceGraph は、これらを取得しやすくするためだけに synthetic service identity を与えてはならない。

---

## ServiceGraphPlan Input Contract

ServiceGraph は、1 つの verified artifact set 内の verified ServiceGraphPlan からのみ作成できる。

有効な ServiceGraphPlan には少なくとも次が必要である。

- `ServiceId` の set
- service ごとの owner module
- service ごとの lifetime
- service ごとの contract metadata
- service ごとの dependency edge または同等の dependency reference
- 必要に応じた optional dependency policy
- service cardinality metadata
- service factory metadata
- 必要に応じた scope-boundary / root-boundary placement metadata
- source provenance と DebugMap linkage
- verified artifact header metadata

ServiceGraphPlan は次をしてはならない。

- KernelIR の authority に存在しない新しい `ServiceId` を発明する
- required service を黙って落とす
- runtime query の必要を generic service slot に変換する
- runtime registration side effect に依存する

partial な ServiceGraphPlan は無効である。
mixed artifact set 由来の ServiceGraphPlan も無効である。

---

## Service Identity Model

`ServiceId` は service の primary runtime identity である。

service resolution は raw type name、任意 string、Unity object identity を truth source として使ってはならない。

ルール:

- service は 1 つの stable `ServiceId` を持つ
- service は複数の validated contract を公開してよい
- contract は runtime identity として `ServiceId` を置き換えない
- `ServiceId` は別の typed identity domain を満たせない
- Unity object reference は service を支援してよいが、service identity ではない

primary identity として禁止するもの:

- `Type`
- implementation type の full name
- authoring string key
- GameObject name
- Transform path
- scene instance ID

generated typed wrapper は、`ServiceId` または verified service slot にコンパイルされる場合にのみ許可される。

---

## Service Contract Model

service contract は、verified service がどのように consume されるかを定義する。

contract の用途:

- validation
- generated resolver surface
- diagnostics
- projection consistency

contract は任意の runtime builder mutation を許可しない。

ルール:

- 公開される各 contract は ServiceIR か ServiceGraphPlan で宣言されていなければならない
- contract exposure は verification と runtime をまたいで安定していなければならない
- contract lookup は 1 つの verified service identity、または 1 つの verified service family rule に解決されなければならない
- contract ambiguity は validation または plan generation で拒否しなければならない

target architecture は installer 風の `.As<T>()` mutation を service truth model として再導入してはならない。

contract metadata は宣言 surface である。
registration script ではない。

---

## Service Lifetime Model

service lifetime は explicit かつ verified である。

説明用の runtime lifetime model:

```csharp
public enum ServiceLifetimeKind
{
    Kernel = 10,
    Project = 20,
    Scene = 30,
    Scope = 40,
    ExplicitTransient = 50,
}
```

このスケッチは説明用であり、01 が所有する identity / lifetime 定義と整合していなければならない。

ルール:

- Kernel service は他のすべての service より長寿命である
- Project service は scene / scope service より長寿命である
- Scene service は scene boundary 内の scope service より長寿命である
- Scope service は 1 つの verified scope lifetime boundary に結びつく
- ExplicitTransient は、下位仕様が normal service として cache すべきでない理由を定義する場合にのみ存在できる

entity lifetime は `ServiceLifetimeKind` ではない。

entity-scoped な挙動が必要なら、次を使う。

- EntityRuntime または component runtime
- ValueStore slice
- RuntimeQuery handle
- command target handle
- hub-owned runtime object

より長寿命の service は、04 が受理した verified indirection を下位仕様が定義しない限り、より短寿命の service を必要としてはならない。

---

## Service Cardinality Model

service cardinality は、service contribution に期待される instance pressure を表す。

説明用 model:

```csharp
public enum ServiceCardinalityKind
{
    SingletonGlobal = 10,
    OnePerProject = 20,
    OnePerScene = 30,
    OnePerAuthoredScope = 40,
    BoundedPool = 50,
    UnboundedRuntime = 90,
}
```

cardinality は lifetime とは別概念である。

ルール:

- `SingletonGlobal`、`OnePerProject`、`OnePerScene`、`OnePerAuthoredScope` は、他の eligibility rule を満たすなら normal な ServiceGraph candidate である
- `BoundedPool` は、下位仕様が instance family を明示的に budget 化し、lifetime、diagnostics、disposal を explicit に保つ場合にのみ valid
- `UnboundedRuntime` は、下位仕様がその pattern を明示的に承認し、14 が budget を与える場合を除き、ServiceGraph service としては invalid

target runtime は、期待 count が次に比例して増える service design を拒否しなければならない。

- entity count
- part count
- renderer count
- tooltip instance count
- mesh track count
- animation player count
- command execution count

---

## Service Factory Model

service creation は explicit かつ verified でなければならない。

runtime の target ServiceGraph が受け付ける service creation path は、01 が定義し ServiceGraphPlan に投影された factory kind のみである。

runtime で許可される service creation path:

- verified plan data から生成されたもの
- static かつ explicit なもの
- それ自体が verified input である explicit prebuilt instance reference によるもの

禁止される factory behavior:

- reflection constructor injection
- `Activator.CreateInstance`
- runtime script scanning
- 任意 `Type` の activation
- scene-wide component discovery
- missing service を修復するための fallback prefab loading

service creation は graph structure を mutate してはならない。
factory は verified service を construct するだけであり、新しい truth を register しない。

---

## ServiceResolver Contract

ServiceResolver は verified service identity を扱う。

必要な最小 semantics:

- required service を resolve するか、structured diagnostics 付きで失敗する
- validated optional policy に従って optional service を resolve する
- current verified lifetime boundary と、明示的に許可された parent boundary の内側で resolve する
- missing required service を fallback で修復しない

`GetRequired` の semantics:

- verified `ServiceId` または generated equivalent を受け付ける
- valid なら required service instance を返す
- service が欠落、無効、boundary rule により禁止されている場合は structured diagnostics 付きで失敗する

`TryGet` の semantics:

- optional である service、または required dependency を主張していない call site にのみ valid
- required dependency を黙って optional に変えてはならない
- 下位仕様が explicit compatibility adapter を定義しない限り、fallback の null-service や legacy-service instance を返してはならない

ServiceResolver は次のような generic discovery features を公開してはならない。

- `ResolveAll<T>()`
- discovery としての `IReadOnlyList<T>` collection
- raw type scanning
- lifecycle collection のための interface enumeration

旧 container の convenience model は意図的に scope 外である。

---

## Service Slot と Cache Model

ServiceGraph lookup は、runtime registration scan ではなく verified plan structure から導かれなければならない。

runtime service resolution は service slot、または同等の dense graph representation を使うべきである。

必要な property:

- known service に対して O(1) または同等の bounded lookup
- cache ownership が verified lifetime boundary に結びつく
- normal resolve path で毎回 registration を全 scan しない
- steady-state resolve path で discovery allocation を行わない
- hot path で broad contract enumeration を行わない

service slot metadata は少なくとも次を保持しなければならない。

- `ServiceId`
- lifetime boundary
- contract mapping
- dependency mapping
- construction state
- diagnostics provenance

steady-state resolution strategy として禁止されるもの:

- raw type による repeated full dictionary scan
- registration からの repeated `List<T>` collection
- resolve ごとの interface discovery
- resolve ごとの reflection

---

## Dependency Resolution Contract

ServiceGraph は、事前検証済みの service dependency のみを解決する。

ルール:

- dependency order は決定論的でなければならない
- required dependency は、依存 service を valid として露出する前に満たされていなければならない
- optional dependency は、04 で既に検証された absence behavior に従わなければならない
- runtime query の必要は runtime query dependency のままでなければならない。service resolver の小細工にしてはならない
- value や blackboard の必要は value dependency のままでなければならない。service resolver の小細工にしてはならない

禁止される dependency behavior:

- acquire-time の ancestor search
- service 代替物のための scene-wide search
- missing dependency を修復するための Unity object search
- plan support なしでの on-demand missing dependency creation
- service dependency としての runtime object 代替

runtime で初めて発見された dependency は、下位仕様が verified RuntimeQuery などの explicit validated indirection として表現しない限り、設計 failure である。

---

## Optional Service Policy

optional service behavior は、04 で検証された optional dependency rule に従う。

ServiceGraph は explicit absence behavior だけを許可できる。
runtime で新しい absence behavior を invent してはならない。

許可される absence behavior category は、04 で定義されたものだけである。

- `DisableContribution`
- `EmitWarning`
- `UseExplicitAlternative`
- `ProfileSpecificError`

resolver rule:

- optional absence は diagnostics-visible のままでなければならない
- explicit alternative は validated `ServiceId` target を指さなければならない
- explicit alternative は lifetime と phase の互換性を保たなければならない
- optional absence は silent null-service fallback に落ちてはならない

optional は「動くものを見つけるまで探す」ことを意味しない。

---

## Scoped Service Policy

scope service は、意味のある ownership boundary を表す authored または verified runtime scope に対してのみ許可される。

許可される例:

- UI root scope hub
- scene presentation scope hub
- 複雑な shared runtime behavior を持つ authored actor root scope
- scene-local simulation coordinator

既定で禁止されるもの:

- entity instance ごとの service
- part ごとの service
- renderer ごとの service
- tooltip view ごとの service
- channel player ごとの service
- mesh track ごとの service

scope service は次を正当化しなければならない。

- なぜ ServiceGraph に参加する必要があるのか
- なぜ別 service が所有する runtime object ではだめなのか
- 期待 instance count
- lifetime boundary
- memory budget
- lifecycle participation boundary

ScopeGraph は scope lifetime boundary の creation を所有する。
ServiceGraph は、その boundary の内側で service resolution を所有する。

---

## Entity と Per-Target Service の禁止

ServiceGraph は entity component storage system として使ってはならない。

target architecture は、次のそれぞれに 1 つずつ service を作ってはならない。

- entity
- part
- renderer
- UI element
- tooltip view
- mesh track
- animation player
- command target

per-target runtime data は次に属する。

- EntityRuntime
- PartRuntime
- ValueStore
- RuntimeQuery index と handle
- pooled runtime object
- hub-owned local runtime object

entity-scoped service exception が許されるのは、次のすべてが成り立つときだけである。

- entity が長寿命の authored aggregate root である
- service に意味のある shared dependency がある
- instance count が bounded かつ budgeted である
- service が KernelIR によって宣言されている
- lifecycle と disposal が verified である
- diagnostics に source location と runtime handle context が含まれる

例外はまれである。
既定の禁止を変えるものではない。

---

### M6.7 Hub / Channel / Player Classification

既存の runtime hub と channel system は、明示的に分類しなければならない。
machine-readable な canonical inventory は [Index/HubClassificationInventory.md](Index/HubClassificationInventory.md) である。この節はその inventory を要約したものであり、常に一致していなければならない。

| Runtime concept | 既定分類 | 注記 |
|---|---|---|
| Hub | Service candidate | coarse-grained で、ドメインまたは authored scope boundary に結びついている場合のみ許可 |
| Channel definition | 設定または authored/runtime plan data | 通常は service ではない |
| PlayerRuntime | Hub-owned runtime object | 既定では service ではない |
| Control surface | hub に対する optional service contract | hub 自体が coarse-grained service の場合のみ valid |
| Telemetry surface | diagnostics または telemetry contract | 追加の service instance を強制してはならない |

現在の service debt に適用すると:

- `ModalStackChannelHubService`
  - UI domain service または UI scope service candidate として分類する
  - resolved layer と root state は service ではなく hub-owned state とする
- `TooltipChannelHubService`
  - hub だけを scope service candidate として分類する
  - channel player は hub-owned runtime object のままにする
  - camera、actor、target、UI root の lookup は RuntimeQuery または explicit dependency に移す
- `MeshChannelHubService`
  - hub だけを scope service candidate として分類する
  - `MeshChannelPlayerRuntime` は hub-owned runtime object のままにする
- `AnimationSpriteHubService`
  - hub だけを scope service candidate として分類する
  - material provider は contract または boundary concern とする
  - player runtime は non-service runtime object のままにする

---

## Lifecycle Boundary

ServiceGraph は lifecycle participation を discovery しない。

service は LifecyclePlan の target になりうるが、その participation は lifecycle-oriented spec と projection によって宣言されなければならない。

implemented interface は enrollment ではない。

ルール:

- ServiceGraph は `IScopeAcquireHandler` を scan してはならない
- ServiceGraph は `IScopeReleaseHandler` を scan してはならない
- ServiceGraph は `IScopeTickHandler` を scan してはならない
- ServiceGraph は任意の service contract から lifecycle list を集めてはならない

migration note:

tooltip、mesh、animation sprite のような legacy service は、migration 中に acquire / release / tick 行動を lifecycle contribution に写してよい。

target ServiceGraph は automatic lifecycle discovery を permanent runtime behavior として保持してはならない。

---

## Command Boundary

ServiceGraph は command catalog ではない。

command executor discovery、routing、dispatch は command runtime specification の担当である。

ServiceGraph は command execution に使われる coarse-grained shared service、たとえば diagnostics や shared domain coordinator を解決してよい。
しかし、次のことをしてはならない。

- `ICommandExecutor` instance を discovery surface として集める
- service registration を基に command を dispatch する
- すべての command target を service にする
- command availability の truth source として service registration を使う

現在の installer-style command registration pattern は migration debt であって target architecture ではない。

---

## RuntimeQuery Boundary

ServiceGraph は service を解決する。
RuntimeQuery は runtime object、scope、actor、UI root、camera target、channel target を解決する。

ServiceGraph は次を実装してはならない。

- ancestor scope search
- scene search
- actor lookup
- owner lookup
- UI root lookup
- camera fallback lookup

service がこれらの object を必要とするなら、その dependency は次のいずれかとして表現しなければならない。

- verified RuntimeQuery dependency
- explicit authored link
- 別の lower-spec verified boundary contract

RuntimeQuery の ownership は 06 の外側にある。
06 が定義するのは、越えてはならない service boundary だけである。

---

## ValueStore Boundary

ValueStore と dynamic value evaluation は service resolution surface ではない。

ServiceGraph は次の用途に使ってはならない。

- value key lookup
- stable-key fallback
- blackboard fallback repair
- dynamic value evaluation context discovery

service が values を必要とするなら、その dependency は value-oriented spec と projection を通じて explicit でなければならない。

service repair として禁止されるもの:

- required value access に対する `NullVarStore` fallback
- missing required value system に対する blackboard substitution
- service dependency を満たすための runtime stable-key search

value は service に影響してよい。
しかし、欠落した service structure を隠すために使ってはならない。

---

## Unity Object Boundary

Unity object identity は service identity ではない。

service は Unity object link を次のすべてが成り立つ場合にのみ持てる。

- link が verified authoring または scope linkage で提供されている
- lifetime boundary が explicit である
- destroyed object の behavior が定義されている
- diagnostics が source object または source location を特定できる

ServiceGraph は次の方法で service を解決してはならない。

- `FindObjectsByType`
- `GetComponentsInChildren`
- Transform parent traversal
- `Camera.main`
- ad-hoc scene object search

Unity object lookup で missing ServiceGraph dependency を修復してはならない。

---

## Diagnostics と DebugMap 要件

service runtime diagnostics は stable、structured、source-traceable でなければならない。

service failure diagnostic には少なくとも次を含める。

- stable error code
- `ServiceId`
- owner module
- service lifetime
- service cardinality
- 必要なら selected profile
- scope handle または scope plan context（scope service の場合）
- source location
- 利用可能なら DebugMap linkage
- human-readable message
- 可能なら suggested fix

contract-specific failure の場合、diagnostics には次も含めるべきである。

- requested contract
- requesting service または subsystem
- 既知なら失敗した dependency phase

source location のない service runtime error は diagnostics degradation である。

代表的 diagnostic code:

- `SERVICE_PLAN_MISSING`
- `SERVICE_REQUIRED_MISSING`
- `SERVICE_CONTRACT_MISSING`
- `SERVICE_CARDINALITY_FORBIDDEN`
- `SERVICE_RUNTIME_QUERY_FORBIDDEN`
- `SERVICE_ANCESTOR_RESOLVE_FORBIDDEN`
- `SERVICE_VALUE_FALLBACK_FORBIDDEN`
- `SERVICE_LEGACY_BRIDGE_FORBIDDEN`

---

## Failure Policy

ServiceGraph は fail closed である。

代表的 failure category:

- plan missing または incomplete
- required service missing
- contract mismatch
- lifetime direction violation
- invalid optional alternative
- invalid service cardinality
- runtime query dependency が service resolution に流用されている
- forbidden value fallback behavior
- forbidden legacy bridge dependency

failure boundary:

- required root または boot-time service failure は、包含する boot または runtime activation boundary を無効にする
- scope service failure は、包含する scope service boundary を無効にする
- dependency failure は依存 service を無効にする。silent partial success に落ちてはならない

ServiceGraph は次を続行してはならない。

- null-service repair
- keep-going fallback creation
- legacy resolver substitution
- silent contract drop

---

## Performance と Memory Policy

ServiceGraph は総 runtime object count に対して小さく保たなければならない。

service count は次に比例して増えるべきである。

- kernel system
- project system
- scene system
- authored scope hub

service count は次に比例して増えてはならない。

- entity count
- part count
- renderer count
- tooltip instance count
- mesh track count
- animation player count
- command execution count

runtime rule:

- normal service resolution は allocation-free か、それに近いものであるべき
- repeated registration scan は禁止
- repeated broad contract enumeration は禁止
- verified plan で explicitly required でない限り、すべての player runtime の eager creation は禁止
- すべての command executor の eager construction は禁止

ServiceGraph は 14 が budget できるように、十分な runtime metric または marker を露出すべきである。

- graph creation
- required service construction
- required service resolution miss
- scope-boundary service creation
- scoped service boundary の disposal

performance optimization は diagnostics や validation-derived safety を取り除いてはならない。

---

## Threading と Async Policy

ServiceGraph の振る舞いは決定論的でなければならない。

ルール:

- graph creation は synchronous かつ explicit である
- service resolution は asynchronous initialization を隠してはならない
- Unity object を触る factory は main thread で動く
- background preparation は、下位仕様が明示的に承認した immutable または non-Unity data に限る

service が asynchronous work を必要とするなら:

- async boundary は lifecycle または別の lower spec に属する
- resolver truth は公開後に silently 変わってはならない
- readiness と failure は diagnostics-visible のままでなければならない

resolver 内での implicit async construction は禁止である。

---

## Disposal と Shutdown Policy

ServiceGraph は、その lifetime boundary 内の service instance disposal を所有する。

ルール:

- disposal order は決定論的でなければならない
- ownership または lower-spec の shutdown rule が要求する場合、依存 service は依存元より先に shut down されるべきである
- scope-bound service は所有 scope boundary が破棄されたときに release されなければならない
- project / scene service disposal は boot と scope lifetime boundary に整合しなければならない

shutdown behavior が必要な service は、その要件を plan metadata または explicit lower-spec contract で明示しなければならない。

ServiceGraph は次をしてはならない。

- child object を探して余分な disposable runtime state を見つける
- hub-owned runtime object を silently leak する
- disposed service instance を cache に生かし続ける

hub-owned runtime object は所有 hub によって dispose される。
dispose を簡単にするためだけに top-level service ownership に昇格させてはならない。

---

## Legacy Compatibility Boundary

legacy compatibility は 13 が定義する explicit adapter を通じてのみ許可される。

target ServiceGraph core は次に依存してはならない。

- アーキテクチャ truth model としての `RuntimeResolverHub`
- service lookup authority としての `BaseLifetimeScopeRegistry`
- service discovery としての installer scan
- command truth としての legacy command runner registration
- legacy null-service または null-value fallback pattern

許可される migration 形:

- explicit adapter または bridge
- profile-visible diagnostics
- bounded scope
- lower spec が文書化した removal path

legacy は default ではない。
temporary boundary である。

---

## Forbidden Patterns

target ServiceGraph runtime で禁止されるもの:

- runtime service registration
- installer-style builder mutation
- reflection constructor injection
- `Activator.CreateInstance` fallback
- 任意 string による resolve
- primary identity としての raw `Type` resolve
- discovery としての `IReadOnlyList<T>` collection
- lifecycle interface のための service scan
- service を通じた command executor collection
- ServiceResolver を通じた runtime object resolve
- ServiceResolver を通じた entity、part、actor、UI root、channel、player resolve
- default で entity ごとに 1 service を作ること
- channel player ごとに 1 service を作ること
- tooltip instance ごとに 1 service を作ること
- mesh track ごとに 1 service を作ること
- ServiceGraph を RuntimeQuery registry として使うこと
- ServiceGraph を ValueStore key resolver として使うこと
- dependency の ancestor scope search
- scene-wide search fallback
- Unity object search fallback
- required dependency に対する null-service fallback
- service truth を修復するための blackboard / var fallback

---

## Test Case Model

各 service runtime test case は次を定義しなければならない。

- Test ID
- Title
- ServiceGraphPlan fixture
- 必要なら relevant ScopeGraphPlan または boot fixture
- 必要なら selected profile
- operation under test
- expected runtime result
- expected diagnostics
- expected failure boundary
- notes

例:

### TC_SERVICE_001_RequiredServiceMissingBlocksBoundary

Input:

- ServiceGraphPlan は `Service A` を宣言している
- `Service A` は `Service B` を required とする
- `Service B` は存在しない

Operation:

- containing service boundary を作る

Expected:

- result: failed
- diagnostic: `SERVICE_REQUIRED_MISSING`
- boundary: containing boot または scope service boundary

---

## Required Test Cases

### A. Service Eligibility Tests

#### TC_SERVICE_ELIGIBILITY_001_CoarseGrainedHubAllowed

Input:

- `ModalStackChannelHub` が UI domain service として宣言されている
- cardinality は `OnePerProject` または `OnePerScene`
- dependency は宣言・検証済みである

Expected:

- Passed

#### TC_SERVICE_ELIGIBILITY_002_ChannelPlayerRejectedAsService

Input:

- `TooltipChannelPlayerRuntime` が ServiceContribution として宣言されている
- cardinality は `UnboundedRuntime`

Expected:

- Failed
- `SERVICE_RUNTIME_OBJECT_NOT_SERVICE`

#### TC_SERVICE_ELIGIBILITY_003_EntityServiceRejectedByDefault

Input:

- ServiceContribution が entity ごとに 1 service を宣言している

Expected:

- Failed
- `SERVICE_ENTITY_CARDINALITY_FORBIDDEN`

#### TC_SERVICE_ELIGIBILITY_004_EntityAggregateExceptionAllowed

Input:

- authored aggregate root service
- bounded count
- source-backed scope
- verified lifecycle と diagnostics

Expected:

- 下位仕様の policy に応じて Passed または warning

### B. Existing Pattern Migration Tests

#### TC_SERVICE_MIGRATION_001_TooltipHubSplitRequired

Input:

- `TooltipChannelHubService` contribution が service dependency、lifecycle、channel runtime、dynamic value、camera lookup behavior を含んでいる

Expected:

- ServiceGraph が受理するのは hub service identity のみ
- lifecycle participation には lifecycle contribution または plan が必要
- camera または target lookup には RuntimeQuery または explicit dependency が必要
- dynamic value access は service resolver truth の外に残る

#### TC_SERVICE_MIGRATION_002_MeshPlayerRuntimeNotService

Input:

- `MeshChannelHubService` が tag ごとに `MeshChannelPlayerRuntime` を所有している

Expected:

- hub は scope service でよい
- player runtime は hub-owned runtime object のまま

#### TC_SERVICE_MIGRATION_003_AnimationSpriteInstallerPatternRejected

Input:

- installer が animation sprite hub を service、material provider、lifecycle handler として builder mutation で登録している

Expected:

- Failed
- contribution は service declaration、lifecycle declaration、必要なら optional contract declaration に分割しなければならない

### C. Boundary Tests

#### TC_SERVICE_BOUNDARY_001_NoAncestorResolve

Input:

- service implementation が dependency を満たすために ancestor traversal を試みる

Expected:

- Failed
- `SERVICE_ANCESTOR_RESOLVE_FORBIDDEN`

#### TC_SERVICE_BOUNDARY_002_NoLifecycleInterfaceScan

Input:

- service が lifecycle-like interface を実装している
- lifecycle plan entry が存在しない

Expected:

- ServiceGraph はその service を lifecycle execution に enrollment しない

#### TC_SERVICE_BOUNDARY_003_NoCommandExecutorCollection

Input:

- module に多数の command executor がある

Expected:

- ServiceGraph は executor list を service discovery として集めない

#### TC_SERVICE_BOUNDARY_004_NoRuntimeQueryThroughServiceResolver

Input:

- service dependency が actor、scope、entity、UI root、camera lookup を指している

Expected:

- Failed
- `SERVICE_RUNTIME_QUERY_FORBIDDEN`

#### TC_SERVICE_BOUNDARY_005_NoValueFallbackThroughServiceResolver

Input:

- service runtime path が missing value dependency を修復するために `NullVarStore` または blackboard fallback を試みる

Expected:

- Failed
- `SERVICE_VALUE_FALLBACK_FORBIDDEN`

### D. Memory and Cardinality Tests

#### TC_SERVICE_MEMORY_001_ServiceCountDoesNotScaleWithEntityCount

Input:

- 10,000 entities
- shared coarse-grained service と entity runtime、RuntimeQuery handle

Expected:

- ServiceGraph の service count は bounded のままで、entity count に比例して増えない

#### TC_SERVICE_MEMORY_002_PerTooltipServiceExplosionRejected

Input:

- 1,000 の tooltip view をそれぞれ service として宣言する

Expected:

- Failed
- `SERVICE_UNBOUNDED_CARDINALITY_FORBIDDEN`

#### TC_SERVICE_MEMORY_003_PerMeshTrackServiceRejected

Input:

- mesh track を個別 service として宣言する

Expected:

- Failed
- `SERVICE_TRACK_CARDINALITY_FORBIDDEN`

#### TC_SERVICE_MEMORY_004_NoEagerPlayerConstructionRequired

Input:

- graph に、local channel use から player runtime を作る hub が含まれている

Expected:

- graph creation に、すべての player runtime の eager construction は不要である

---

## 受け入れ条件

06 が完成していると見なす条件は次のとおり。

- ServiceGraph runtime の目的と所有範囲が定義されている
- service eligibility rule が定義されている
- non-service runtime object rule が定義されている
- ServiceGraphPlan input contract が定義されている
- service identity と contract rule が定義されている
- service lifetime と cardinality rule が定義されている
- service factory rule が定義されている
- required / optional service に対する resolver semantics が定義されている
- slot / cache model 要件が定義されている
- dependency resolution rule が定義されている
- scoped service policy が定義されている
- entity / per-target service の禁止が定義されている
- hub / channel / player の分類が定義されている
- lifecycle / command / runtime query / value / Unity object boundary が定義されている
- diagnostics / DebugMap 要件が定義されている
- failure policy が定義されている
- performance / memory policy が定義されている
- threading / async policy が定義されている
- disposal / shutdown policy が定義されている
- legacy boundary rule が定義されている
- forbidden pattern が定義されている
- service runtime test case model が定義されている
- required service runtime test が定義されている

ServiceGraph を generic DI container、runtime object directory、lifecycle collector、command registry、fallback resolver として読めてしまうなら未完成である。

---

## テストケース

| テストケース | 目的 | 検証 |
|---|---|---|
| TC-06-01 | ServiceGraph が verified な coarse-grained service resolver であり続けることを確認する。 | purpose、runtime definition、eligibility の節で、汎用 container behavior と per-target service expansion を禁止していること。 |
| TC-06-02 | non-service runtime object が ServiceGraph の外側に残ることを確認する。 | non-service model と hub / channel / player classification の節で、player runtime、local session、per-target object を service identity から外していること。 |
| TC-06-03 | lifecycle / command / runtime query / value boundary が explicit であることを確認する。 | boundary の節で、interface-scan lifecycle enrollment、executor collection、service 経由の runtime query lookup、service 経由の value fallback を禁止していること。 |
| TC-06-04 | service count が entity や target count に比例しないことを確認する。 | scoped service、per-target prohibition、performance の節で、unbounded runtime cardinality を拒否していること。 |
| TC-06-05 | failure が structured で fail closed のままであることを確認する。 | diagnostics と failure の節で、required-service、contract、cardinality、boundary violation を silent fallback なしで報告していること。 |
| TC-06-06 | legacy installer と discovery pattern が runtime truth として戻らないことを確認する。 | current debt observation、legacy boundary、forbidden pattern の節で、runtime registration、installer mutation、discovery-based service resolution を拒否していること。 |

---

## 最終見解

ServiceGraph は coarse-grained で verified な service を解決する。
すべての runtime object を service として扱うわけではない。

service count が entity count とともに増えるなら、設計は既定で誤っている。

target ServiceGraph は convenience container ではない。
それは、explicit な service structure を解決する verified runtime resolver である。

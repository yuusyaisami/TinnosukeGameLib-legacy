# モジュール貢献仕様

## 文書ステータス

- 文書 ID: `02_ModuleContributionSpec`
- 状態: Draft
- 役割: modules から KernelIR へ向けた宣言的な contribution 契約
- 依存先:
  - [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md)
  - [01_KernelIRSpec.md](01_KernelIRSpec.md)
- この仕様を基盤としている文書:
  - [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md)
  - [04_DependencyValidationSpec.md](04_DependencyValidationSpec.md)
  - [05_BootManifestAndProfileSpec.md](05_BootManifestAndProfileSpec.md)
  - [06_ServiceGraphRuntimeSpec.md](06_ServiceGraphRuntimeSpec.md)
  - [07_ScopeGraphRuntimeSpec.md](07_ScopeGraphRuntimeSpec.md)
  - [08_LifecyclePlanSpec.md](08_LifecyclePlanSpec.md)
  - [09_CommandCatalogRuntimeSpec.md](09_CommandCatalogRuntimeSpec.md)
  - [10_ValueSchemaAndStoreSpec.md](10_ValueSchemaAndStoreSpec.md)
  - [10_2_DynamicValueEvaluationSpec.md](10_2_DynamicValueEvaluationSpec.md)
  - [11_DebugMapAndDiagnosticsSpec.md](11_DebugMapAndDiagnosticsSpec.md)
  - [12_UnityAuthoringBridgeSpec.md](12_UnityAuthoringBridgeSpec.md)

### 所有範囲

本書は、module identity、module ownership、contribution kind、profile / availability 宣言、source location 要件、決定論的な contribution 収集、conflict policy、そして installer 風の mutation を拒否する方針を所有する。

runtime builder の挙動、runtime storage layout、検証アルゴリズム、boot policy、generated code format は本書の担当外である。

---

## 目的

本仕様は、module レベルの authoring input を、KernelIR に正規化できる宣言的な contribution data に変換する方法を定義する。

ModuleContribution は制約付きの宣言システムであり、installer API ではない。
runtime installer 風の mutation を、純粋な contribution data に置き換えるための仕組みである。

狙いは、module code が runtime builder に触れたり、scope state を探索したり、build 中に service や executor を直接登録したりする現在のパターンを排除することにある。

module が runtime state に触れずに自分自身を記述できないなら、その module は target kernel に適さない。

この仕様は次の失敗モードを防ぐために存在する。

- composition logic としての builder mutation
- transform hierarchy や scope traversal から ownership を推測すること
- collection order の中に隠れた last-write-wins override
- runtime discovery や空 asset への silent fallback
- contribution collection 中の runtime service resolution
- declaration、registration、lifecycle wiring を 1 本の path に混ぜる installer logic

---

## 範囲

本仕様は次を定義する。

- module identity と ownership
- module contribution record の形
- contribution kind
- contribution collection pipeline
- module dependency 宣言
- profile / availability 宣言
- source location 要件
- deterministic ルール
- conflict policy
- legacy installer からの migration boundary
- 下位仕様への handoff boundary

本仕様は次を意図的に定義しない。

- IR node layout の詳細
- dependency validation algorithm
- runtime service storage layout
- scope handle layout
- lifecycle dispatcher 実装
- command execution algorithm
- value store メモリレイアウト
- boot manifest schema
- generated artifact format

この文書は、隠れた runtime 実装層になってはならない。
これは宣言的な module 契約であり、代替 runtime system ではない。

---

## 他仕様との関係

| 仕様 | 関係 |
|---|---|
| [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md) | 02 が破ってはならない根本的なアーキテクチャ制約を定義する |
| [01_KernelIRSpec.md](01_KernelIRSpec.md) | module contribution を消費する正規化済み IR 契約を定義する |
| [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md) | 検証済み KernelIR が verified plan artifact になる方法を定義する |
| `04_DependencyValidationSpec.md` | ここで宣言された dependency 形状と ownership metadata を消費する |
| `05_BootManifestAndProfileSpec.md` | module availability と profile 宣言を消費する |
| `06_ServiceGraphRuntimeSpec.md` | KernelIR から投影された service 関連 contribution を消費する |
| `07_ScopeGraphRuntimeSpec.md` | KernelIR から投影された scope 関連 contribution を消費する |
| `08_LifecyclePlanSpec.md` | KernelIR から投影された lifecycle step 宣言を消費する |
| `09_CommandCatalogRuntimeSpec.md` | KernelIR から投影された command 関連 contribution を消費する |
| `10_ValueSchemaAndStoreSpec.md` | KernelIR から投影された value / init contribution を消費する |
| `10_2_DynamicValueEvaluationSpec.md` | KernelIR から投影された dynamic / reactive evaluation contribution を消費する |
| `11_DebugMapAndDiagnosticsSpec.md` | ここで付与された source / debug metadata を消費する |
| `12_UnityAuthoringBridgeSpec.md` | module contribution source となる authoring input を生成する |

02 は、正規化済み IR にデータを流し込む宣言境界である。
下位仕様は、runtime 挙動から modules を再発見するのではなく、ここで定義した概念を参照しなければならない。

---

## Assembly Definition と Compile Boundary の期待値

宣言的な module contribution 契約の想定配置先は `GameLib.Kernel.Contributions` である。
詳細な dependency matrix は [17_AssemblyDefinitionAndCompileBoundarySpec.md](17_AssemblyDefinitionAndCompileBoundarySpec.md) が管理する。

02 に対する必須の compile-boundary ルールは次のとおり。

- `GameLib.Kernel.Contributions` は runtime service / scope / lifecycle / command / value 実装 assembly と分離し続ける
- contribution collection contract は core assembly で Unity 非依存のまま維持し、Unity からの抽出は `GameLib.Kernel.Authoring` または `GameLib.Kernel.Authoring.Editor` に置く
- runtime registration helper を contribution assembly に入れて、installer 風の mutation を再導入してはならない
- legacy installer migration code は `GameLib.Kernel.Contributions` ではなく quarantine assembly に置く

contribution 型が runtime builder access や Unity scene search を必要とするなら、それは 02 の外側に置くべきである。

---

## 現行アーキテクチャの観測

この節は、現行コードベースの観測結果を要約する。
ここは v2 target policy ではなく、移行元の事実整理である。

### 観測のトレーサビリティ

現在の module contribution 観測は、source code、migration note、profiling evidence、design review note に遡れなければならない。

この文書を更新したとき、現行コードベースに合わなくなった観測は削除するか legacy migration note に移す。

| 観測 | 根拠の種類 | 期待される下流仕様 |
|---|---|---|
| scope build が installer discovery と build coordination をまだ混ぜている | Source / Profiling | 03, 07, 08, 14 |
| command executor registration が一括収集されている | Source / Profiling | 03, 09, 14 |
| lifecycle と build callback が installer 風 class に混在している | Source | 03, 08 |
| Blackboard / Var / DynamicValue の責務が重なっている | Source | 03, 10 |
| registry / catalog locator が fallback 行動を隠している | Source | 03, 05, 10, 11 |

### 代表的なアンカー

- [BaseLifetimeScope.cs](../../GameLib/Script/Common/LTS/Core/BaseLifetimeScope.cs) - 現行 `IFeatureInstaller` の entry point
- [ScopeFeatureInstallerUtility.cs](../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs) - nearest-scope ownership filtering と installer discovery
- [RuntimeLifetimeScope.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs) - install flow、installer collection、scope build coordination
- [CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs) - bulk executor registration の例
- [SceneFlowInstallerMB.cs](../../GameLib/Script/Project/System/SceneFlow/MB/SceneFlowInstallerMB.cs) - scene flow installer の例
- [AudioInstallerMB.cs](../../GameLib/Script/Common/Audio/AudioInstallerMB.cs) - conditional service setup の例
- [TimeInstallerMB.cs](../../GameLib/Script/Common/Time/TimeInstallerMB.cs) - acquire/release と build callback の混在例
- [LTSIdentityMB.cs](../../GameLib/Script/Common/LTS/Identity/MB/LTSIdentityMB.cs) - 現行 scope identity authoring metadata
- [LTSIdentityService.cs](../../GameLib/Script/Common/LTS/Identity/Core/LTSIdentityService.cs) - runtime identity と registry の統合
- [BaseLifetimeScopeRegistry.cs](../../GameLib/Script/Common/LTS/Registry/BaseLifetimeScopeRegistry.cs) - kind / id / category ベースの runtime query
- [CommandKeyResolver.cs](../../GameLib/Script/Common/Commands/VNext/Catalog/CommandKeyResolver.cs) - stable key と fallback 行動の例
- [CommandCatalogService.cs](../../GameLib/Script/Common/Commands/VNext/Catalog/CommandCatalogService.cs) - key ベース catalog lookup の例
- [BlackboardMB.cs](../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs) - blackboard lifecycle と value-init の混在例
- [VarStore.cs](../../GameLib/Script/Common/Variables/VarStore/Core/VarStore.cs) - runtime value store の例
- [VarIds.g.cs](../../GameLib/Script/Generated/VarIds.g.cs) - generated stable ID の例
- [DynamicVariant.cs](../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicVariant.cs) - dynamic payload の例
- [LoadingScreenService.cs](../../GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs) - scene transition と discovery の混在例
- [UnityCollisionSystemMB.cs](../../GameLib/Script/Collision/Unity/UnityCollisionSystemMB.cs) - profile 風の散在例
- [CollisionIdCatalogLocator.cs](../../GameLib/Script/Collision/Core/CollisionIdCatalogLocator.cs) - asset locator の例
- [CommandCatalogLocator.cs](../../GameLib/Script/Common/Commands/VNext/Catalog/CommandCatalogLocator.cs) - asset locator の例

### 現行の不足点

- module boundary が 1 つの宣言契約として表現されていない
- module identity が folder、assembly、scene 構造から明確に分離されていない
- profile / availability data が installer や asset locator に散在している
- contribution data が 1 本の決定論的な正規化 path で収集されていない
- fallback 行動が declaration data の欠落を隠せてしまう
- contribution の責務が installer class、registry、catalog locator に分割されている

---

## Module Authority

module は authoring-level の ownership boundary である。

module は、自分が何を貢献するかを宣言する。
runtime state を直接 mutate しない。
runtime boot behavior を決めない。
contribution collection 中に live service を解決しない。

### Core Module Concepts

| 概念 | 要件 |
|---|---|
| ModuleDefinition | 1 つ以上の contribution を束ねる authoring-level 宣言。asset-backed でも code-backed でもよいが、runtime builder access は不要でなければならない |
| ModuleId | 安定した typed identity。folder 名、assembly 名、scene path、component order から推測してはならない |
| ModuleKind | feature、content、bridge、system、migration adapter などの分類。runtime behavior flag ではない |
| ModuleVersion | module semantics の互換性入力。meaning が変わるなら version を変えなければならない |
| Ownership | どの contribution domain を module が制御するかの明示宣言 |
| Availability | 宣言的な profile / build target 制約。runtime expression や scene discovery に依存してはならない |

### Module Identity Rules

module identity は明示的で、source-backed でなければならない。

identity source として禁止するもの:

- folder path
- assembly 名だけ
- scene hierarchy の位置
- transform parentage
- component index order
- runtime object instance ID

module identity は、同じ source に対して editor session をまたいでも、generation run をまたいでも安定していなければならない。

---

## Contribution Model

contribution data は、module が KernelIR に入れたい内容を宣言するものだ。

contribution collection は pure、deterministic、かつ runtime side effect なしで行われなければならない。

各 contribution item は少なくとも次を含む必要がある。

- contribution kind
- owner ModuleId
- source location
- stable name または stable ID input
- 必要なら dependency reference
- 必要なら profile / availability 宣言
- 必要なら conflict policy metadata
- 必要なら debug metadata

contribution data は runtime instance container ではない。
live service、scope instance、mutable runtime cache を格納してはならない。

### Contribution Pipeline

```text
Authoring Input
  -> ModuleDefinition / equivalent source
  -> ContributionData
  -> deterministic normalization
  -> KernelIR
  -> validation handoff
```

collection と normalization の path は次をしてはならない。

- runtime builder に触れる
- live service を解決する
- runtime scope state を読む
- ownership を推測するために scene hierarchy を走査する
- 不足宣言を fallback asset で修復する

required declaration が欠けているなら、collection は structured diagnostics を伴って失敗しなければならない。

---

## Contribution Kinds

この節は、02 がサポートすべき contribution kind を列挙する。
各 kind は execution mechanism ではなく declaration 形状である。

| Contribution Kind | 宣言するもの | してはならないこと | 引き渡し先 |
|---|---|---|---|
| ServiceContribution | service identity、lifetime intent、dependency reference、factory metadata、profile availability | service を instantiate する、runtime container から解決する | 03, 06, 11 |
| CommandContribution | command identity、authoring key mapping、payload schema、executor metadata | executor を bulk register する、任意文字列から executor identity を解決する | 03, 09, 11 |
| ValueContribution | value identity、schema requirement、persistence metadata | runtime store から schema を推測する、その場で runtime key を作る | 03, 10, 11 |
| ValueInitContribution | 初期書き込み、default value、順序ヒント | reactive evaluation を generic initialization の中に隠す | 03, 10 |
| DynamicEvaluationContribution | one-shot または phase-bound な dynamic evaluation、出力先、fallback policy、宣言済み input | DynamicValue evaluation を `ValueInitContribution` や generic getter に隠す | 03, 10_2, 11 |
| ReactiveEvaluationContribution | tracked recomputation、cache policy、invalidation policy、scheduling | source-local な ad hoc version check や隠れた poll loop を契約にする | 03, 10_2, 11 |
| ScopeContribution | authored scope identity、parent constraint、ownership、attach/detach constraint | transform hierarchy や nearest scope ownership から scope を推測する | 03, 07, 11 |
| LifecycleContribution | explicit lifecycle step plan、phase ordering、dependencies | interface 実装や registration scan を自動収集する | 03, 08, 11 |
| RuntimeQueryContribution | queryable runtime identity field、category、index requirement、ambiguity rule | generic DI lookup や scene search を query semantics として使う | 03, 07, 11 |
| DiagnosticsContribution | stable debug name、source location、legacy origin、trace metadata | boot 時に provenance を組み立て直す | 03, 11 |
| AssetBindingContribution | 必要 asset、registry input、binding target、availability note | Resources fallback や AssetDatabase locator の挙動を隠す | 03, 05, 12 |
| CodeGenerationContribution | generated projection requirement、target identity domain、generation prerequisite | ここで code を直接 emit する、generated artifact format を定義する | 03 |

### ServiceContribution

ServiceContribution は、target kernel に service が存在しなければならないことを宣言する。

含めてよいもの:

- service identity
- lifetime class
- dependency edge
- factory metadata
- profile availability

してはならないこと:

- service instance を作ること
- collection 中に runtime container から service を解決すること

### CommandContribution

CommandContribution は command identity と executor 向け metadata を宣言する。

含めてよいもの:

- authoring key
- runtime identity mapping
- payload requirement
- executor metadata
- diagnostics metadata

してはならないこと:

- side effect として executor を登録すること
- authoring key を runtime truth とみなすこと

### ValueContribution

ValueContribution は schema レベルの value 存在を宣言する。

含めてよいもの:

- `ValueKeyId` または同等の identity input
- type information
- persistence / save metadata
- default value constraint

してはならないこと:

- runtime store を読んで schema を推測すること
- runtime fallback で不足 key を作ること

### ValueInitContribution

ValueInitContribution は value の初期書き込みや default state を宣言する。

含めてよいもの:

- initial assignment
- initialization ordering
- profile-dependent defaults

してはならないこと:

- reactive evaluation や dynamic computation を同じ宣言に隠すこと

### DynamicEvaluationContribution

DynamicEvaluationContribution は one-shot または phase-bound な評価を宣言する。

含めてよいもの:

- root source reference
- output target
- target store scope
- phase
- fallback policy
- declared runtime inputs

generic init data や隠れた getter logic に潰してはならない。

### ReactiveEvaluationContribution

ReactiveEvaluationContribution は tracked recomputation と shared-cache 行動を宣言する。

含めてよいもの:

- root source reference
- computed target または cached result target
- dependency declaration mode
- invalidation policy
- scheduling policy
- cache policy

source-local な ad hoc cache や散在した version check を契約にしてはならない。

### ScopeContribution

ScopeContribution は authored scope ownership と attachment rule を宣言する。

含めてよいもの:

- scope authoring identity
- parent constraint
- attachment / spawn constraint
- ownership boundary

transform hierarchy から scope parentage を推測してはならない。

### LifecycleContribution

LifecycleContribution は explicit な lifecycle ordering を宣言する。

含めてよいもの:

- lifecycle step identity
- ordering rule
- phase dependency
- acquire / release または init の意味

interface 自動収集や registration scan に依存してはならない。

### RuntimeQueryContribution

RuntimeQueryContribution は queryable runtime identity と indexing requirement を宣言する。

含めてよいもの:

- runtime identity field
- queryable category
- uniqueness requirement
- ambiguity rule

generic DI resolution として実装してはならない。

### DiagnosticsContribution

DiagnosticsContribution は provenance と traceability の data を宣言する。

含めてよいもの:

- stable debug name
- source location
- profile availability
- legacy migration origin

boot 時に provenance を再構築してはならない。

### AssetBindingContribution

AssetBindingContribution は plan に必要な asset reference や binding requirement を宣言する。

含めてよいもの:

- required assets
- registry inputs
- binding target identity

fallback locator で asset discovery を隠してはならない。

### CodeGenerationContribution

CodeGenerationContribution は、下流 generation が module 用の code または generated metadata を作るべきことを宣言する。

含めてよいもの:

- target projection kind
- generation に影響する identity domain
- generation prerequisite

generated code format を定義したり、ここで直接 code を出力したりしてはならない。

---

## Dependency と Availability ルール

module は、他の module に explicit に dependency を宣言できる。

shared static access、scene order、runtime discovery を通じた hidden dependency transport は禁止する。

### Dependency Declaration Rules

- dependency 宣言は explicit でなければならない
- dependency identity は安定していて source-backed でなければならない
- required dependency の欠落は validation error である
- dependency 宣言は deterministic でなければならない
- dependency 宣言は KernelIR に表現されなければならない

### Availability Rules

Availability は宣言的である。

次のような内容を表現できる。

- profile 選択
- build target 選択
- editor / test / release availability
- platform family availability

runtime expression evaluation や隠れた script logic に依存してはならない。

もし simple declaration より表現力が必要なら、その条件は下位仕様か feature-flag system で明示的にモデル化しなければならない。

### Source Location Rules

runtime 挙動に影響しうる contribution item は、すべて source location metadata を持たなければならない。

source location は、diagnostics と DebugMap 生成への traceability を支えられるだけ十分でなければならない。

---

## Conflict Policy

Conflict handling は fail closed でなければならない。

既定の policy は validation error である。

implicit な conflict resolution として禁止するもの:

- last-write-wins
- silent override
- collection order による implicit merge
- duplicate ownership の runtime repair
- 空の contribution data への fallback

もし下位仕様で override mechanism が必要なら、その scope、理由、validation rule、diagnostics 行動、削除条件を明示しなければならない。

02 自体は override mechanism を定義しない。

---

## Determinism ルール

contribution collection と normalization は deterministic でなければならない。

同じ module definition、availability input、profile なら、contribution set は意味的に同一である必要がある。

contribution processing は次に依存してはならない。

- enumeration order
- reflection order
- current time
- random value
- machine-local path value
- Unity object instance ID
- scene load timing

推奨される stable ordering priority:

1. module identity
2. contribution kind
3. stable name または stable ID
4. source location
5. 必要なら explicit dependency order

決定論的な順序が確立できないなら、その contribution run は失敗しなければならない。

---

## Legacy Installer の拒否

legacy の `IFeatureInstaller.InstallFeature(builder, scope)` パターンは target model ではない。

target contract として拒否する legacy behavior:

- installation 中の scope discovery
- runtime hierarchy からの ownership 推測
- collection 中の direct service registration
- discovery step としての bulk command executor registration
- registration からの lifecycle auto-collection
- installer mutation に混ざった debug binding
- 1 本の execution path に混ざった authoring / runtime concern

legacy installer は、target kernel core の外側にある migration adapter としてのみ存在できる。

legacy adapter は観測可能でなければならず、新しい target-kernel behavior の源になってはならない。

### Migration Mapping

| Legacy Pattern | Target Contribution Kind |
|---|---|
| `IFeatureInstaller` / `InstallFeature(builder, scope)` | `ModuleContribution` / `ContributionData` |
| `CommandRunnerMB` の bulk executor registration | `CommandContribution` |
| `BlackboardMB` の混在した value setup | `ValueContribution` / `ValueInitContribution` |
| `RuntimeLifetimeScope` の installer discovery | `ModuleContribution` collection pipeline |
| `BaseLifetimeScopeRegistry` の lookup role | `RuntimeQueryContribution` |
| `VarKeyRegistryLocator` の fallback lookup | `ValueContribution` / `AssetBindingContribution` |
| `VarIdResolver` の runtime negative ID repair | `ValueContribution` / `DiagnosticsContribution` |
| `CommandCatalogLocator` / `CollisionIdCatalogLocator` の asset locator pattern | `AssetBindingContribution` |

この mapping は説明的であり、許可ではない。
既存責務をどこへ移すかを示すものであって、旧挙動を target kernel に残す許可ではない。

---

## Module と KernelIR の関係

module は runtime 中に KernelIR を直接書かない。
module は宣言的な contribution data を供給し、それを normalization が KernelIR に変換する。

target architecture に必要な分離は次のとおり。

- module authoring が contribution intent を所有する
- KernelIR が normalized structure を所有する
- validation が accept / reject を決める
- plan generation が verified runtime projection を所有する

module は contribution contract の外側に新しい structural identity を持ち込んではならない。
新しい identity domain が必要なら、ここで宣言し、後続仕様が消費する前に KernelIR へ正規化しなければならない。

---

## 受け入れ条件

02 が完成していると見なす条件は次のとおり。

- module identity と ownership が定義されている
- contribution record 要件が定義されている
- contribution kind が定義されている
- collection / normalization pipeline が定義されている
- dependency / availability 宣言が定義されている
- source location 要件が定義されている
- determinism ルールが定義されている
- conflict policy が定義されている
- legacy installer の拒否が定義されている
- target contribution kind への migration mapping が定義されている
- 03 と 04 への handoff boundary が定義されている

runtime builder API、runtime resolution algorithm、storage layout の詳細が仕様に入り込んだ時点で未完成である。

---

## テストケース

| テストケース | 目的 | 検証 |
|---|---|---|
| TC-02-01 | module contribution が宣言的であり、runtime builder mutation を受け付けないことを確認する。 | 目的と contribution pipeline の節で、builder access、live service resolution、runtime state mutation を禁止していること。 |
| TC-02-02 | module identity が explicit かつ source-backed であることを確認する。 | module authority の節で、ModuleId、ModuleKind、ModuleVersion、Ownership、Availability を path や hierarchy から導出せずに定義していること。 |
| TC-02-03 | 必要な contribution kind がすべて表現されていることを確認する。 | contribution kind の節が、service、command、value、value init、dynamic evaluation、reactive evaluation、scope、lifecycle、runtime query、diagnostics、asset binding、code generation を含んでいること。 |
| TC-02-04 | dependency と availability が推測ではなく宣言されることを確認する。 | dependency / availability rule の節で、隠れた static access、runtime expression fallback、scene discovery を禁止していること。 |
| TC-02-05 | conflict policy が fail closed であることを確認する。 | conflict policy の節で、last-write-wins、silent override、implicit merge を拒否していること。 |
| TC-02-06 | legacy installer behavior が migration-only として扱われることを確認する。 | legacy installer rejection と migration mapping の節で、旧パターンを adapter として記述し、target behavior として扱っていないこと。 |

---

## 最終見解

ModuleContribution は、authoring / module ownership と KernelIR normalization の間にある explicit な宣言境界である。

その目的は、target path で runtime builder mutation を不可能にすることにある。
module は contribution を宣言する。
KernelIR はそれを正規化する。
validation が受け入れ可否を決める。
verified plan generation が、その検証済み結果を消費する。

module 挙動が declarative contribution data として表現できないなら、それは target kernel core に属さない。

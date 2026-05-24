# Value Schema and Store 仕様

## 文書ステータス

- 文書 ID: 10_ValueSchemaAndStoreSpec
- ステータス: Draft
- 役割: Kernel v2 における抽象的な value identity、スキーマ、runtime value storage、初期化 plan、汎用 value-state 境界、save metadata 境界、および value diagnostics を定義する。scalar runtime と binding の意味論は 10-1 に委譲する
- 依存先:
  - [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md)
  - [01_KernelIRSpec.md](01_KernelIRSpec.md)
  - [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md)
  - [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md)
  - [04_DependencyValidationSpec.md](04_DependencyValidationSpec.md)
  - [05_BootManifestAndProfileSpec.md](05_BootManifestAndProfileSpec.md)
  - [06_ServiceGraphRuntimeSpec.md](06_ServiceGraphRuntimeSpec.md)
  - [07_ScopeGraphRuntimeSpec.md](07_ScopeGraphRuntimeSpec.md)
  - [08_LifecyclePlanSpec.md](08_LifecyclePlanSpec.md)
  - [09_CommandCatalogRuntimeSpec.md](09_CommandCatalogRuntimeSpec.md)
- 基盤を提供するもの:
  - [10_1_ScalarRuntimeAndBindingSpec.md](10_1_ScalarRuntimeAndBindingSpec.md)
  - [10_2_DynamicValueEvaluationSpec.md](10_2_DynamicValueEvaluationSpec.md)
  - [11_DebugMapAndDiagnosticsSpec.md](11_DebugMapAndDiagnosticsSpec.md)
  - [12_UnityAuthoringBridgeSpec.md](12_UnityAuthoringBridgeSpec.md)
  - [13_LegacyCompatBoundarySpec.md](13_LegacyCompatBoundarySpec.md)
  - [14_PerformanceBudgetAndRuntimeRulesSpec.md](14_PerformanceBudgetAndRuntimeRulesSpec.md)
  - [15_TestAndValidationSpec.md](15_TestAndValidationSpec.md)

10 は、値が宣言され、正規化され、検証され、projection された後の runtime contract を所有する。

この仕様は、具体的な保存構造、シリアライズ済み asset API、editor UI を確定しない。

## 所有範囲

この仕様が所有するもの:

- `ValueKeyId` の runtime access policy
- `ValueSchema` の runtime  स्वीकार要件
- `ValueStore` の runtime 責務
- `ValueStoreScope` と lifetime policy
- `ValueStoreInitPlan` の挙動
- initialization 順序と overwrite policy
- value の read/write access policy
- abstract value kind と generic storage policy
- record、record list、table、layered numeric state の policy
- revision と dirty signal の要件
- DynamicEvaluation 境界
- ReactiveEvaluation 境界
- CommandLocal 境界
- command の read/write access 境界
- save metadata 境界
- runtime stable-key fallback の禁止
- value diagnostics と DebugMap 要件
- value failure の挙動
- value performance と memory 制約
- Blackboard、VarStore、GridBlackboard、DynamicValue、CommandLocal の移行ポリシー

この仕様が所有しないもの:

- final な scalar modifier / binding / telemetry の runtime contract
- final な ReactiveResolver graph 実装
- final な CommandCatalog dispatch 実装
- final な SaveSystem payload format
- editor registry UI
- ScopeGraph の親子構造実装
- ServiceGraph の service cache 実装
- RuntimeQuery の index 実装
- Unity authoring component schema

下位仕様が value 挙動を実行する必要がある場合、Blackboard 風の fallback semantics を再構築するのではなく、ここで定義された contract を使わなければならない。

## 目的

この仕様は、Kernel v2 が runtime value をどのように命名し、検証し、初期化し、保存し、読み書きし、観測し、診断するかを定義する。

中心的な立場:

```md
ValueSchema は、どの value が存在し得るかを定義する。
ValueStore は runtime state を保存する。
ValueStoreInitPlan は初期書き込みを定義する。
Float 専用の scalar runtime と binding の意味論は 10-1 が定義する。
Dynamic / Reactive evaluation は generic initialization の中に隠さない。
Runtime stable-key fallback は target kernel path で禁止である。
```

中核となる runtime rule は次の通りである:

```md
ValueStore は、`ValueKeyId` によって検証済み value を保存する。
キーを発見したり、スキーマを推論したり、隠れた dynamic expression を評価したり、runtime で欠落データを修復したりしない。
```

## スコープ

この仕様が定義するもの:

- value identity model
- stable-key 境界
- value schema model
- value kind と type model
- runtime store contract
- store scope と lifetime policy
- storage policy 要件
- read/write access policy
- init plan model
- initialization 順序と overwrite ルール
- table / record / record list policy
- layered numeric policy
- revision と dirty signal policy
- DynamicEvaluation 境界
- ReactiveEvaluation 境界
- CommandLocal 境界
- command value access 境界
- save metadata 境界
- RuntimeQuery 境界
- value diagnostics
- value failure policy
- value performance と memory policy
- legacy migration policy
- forbidden patterns
- required test cases

## 対象外

この仕様が定義しないもの:

- final な `ValueStore` memory layout
- final な generated accessor API
- final な reactive dependency graph
- final な DynamicEvaluation evaluator 実装
- final な SaveSystem file format
- final な editor registry UI
- final な Unity authoring component schema
- final な command dispatch 実装
- final な runtime query index 実装

この仕様は `ValueStore` を Blackboard v2 に変えてはならない。

## 他仕様との関係

| 仕様 | 関係 |
|---|---|
| 00 | 明示的な runtime、runtime fallback の禁止、value/schema の所有境界を定義する。 |
| 01 | `ValueKeyIR`、typed identity domain、source location、正規化済み value identity を定義する。 |
| 02 | installer 風の runtime mutation を許さず、value 関連 contribution を定義する。 |
| 03 | `ValueSchemaPlan`、`ValueStoreInitPlan`、value projection を source of truth ではなく artifact として生成する。 |
| 04 | value key、schema reference、init compatibility、stable-key rejection、dynamic dependency、command access、save metadata を検証する。 |
| 05 | 検証済み value artifact のみで boot し、registry や `Resources.Load` fallback を使ってはならない。 |
| 06 | value store を所有または露出する service を解決してよいが、value や dynamic evaluation は所有しない。 |
| 07 | scope lifetime を所有し、value store そのものにはならずに scope-local value store boundary を参照してよい。 |
| 08 | store を初期化し得る明示的 lifecycle step を実行するが、value 初期化を推論してはならない。 |
| 09 | `ValueKeyId` への command read/write access を定義し、CommandLocal execution context を所有する。 |
| 10-1 | 10 の検証済み numeric definition の上に載る、float 専用 scalar runtime、modifier、binding、telemetry、failure semantics を所有する。 |
| 10-2 | `DynamicValue`、`DynamicEvaluationPlan`、`ReactiveEvaluationPlan`、tracker、cache、invalidation、nested dependency capture を所有する。10 はその層が消費する value-state 境界と revision signal のみを所有する。 |
| 11 | value runtime が使う共有 structured diagnostics substrate と DebugMap runtime contract を所有する。10 は必要な value provenance fields、init / table diagnostics context、failure behavior を定義する。 |
| 12 | runtime 前に stable key を `ValueKeyId` に正規化する authoring input を生成する。 |
| 13 | Blackboard と VarStore の移行に限定した legacy boundary を定義する。 |
| 14 | value access、initialization、dirty signal の hot-path budget を定義する。 |
| 15 | 必須 value test を実行可能な validation と CI coverage に落とし込む。 |

## asmdef とコンパイル境界の期待値

generic value schema と store runtime の想定 asmdef は `GameLib.Kernel.Value` である。
scalar specialization と dynamic evaluation は、10-1 と 10-2 が定義する別々の leaf assembly に属する。
詳細な依存行列は [17_AssemblyDefinitionAndCompileBoundarySpec.md](17_AssemblyDefinitionAndCompileBoundarySpec.md) が所有する。

10 に必要なコンパイル境界ルール:

- `GameLib.Kernel.Value` は feature assembly、legacy Blackboard / VarStore コード、具体的な command 実装から分離されていなければならない
- value core は Unity-free のまま保ち、`noEngineReferences: true` を使うべきである
- dynamic evaluation logic、tracker logic、scalar-specific binding logic は generic value assembly に戻してはならない
- save payload formatting、Unity authoring 抽出、runtime object lookup helper は generic value core の外に置かなければならない

value storage が Unity API、legacy fallback helper、feature-specific runtime code なしにコンパイルできないなら、10 の境界は破られている。

## 現在の value 負債の観測

現在の value 系システムは複数の責務を混在させている:

- service registration
- local value storage
- grid / table storage
- initialization
- `DynamicValue` evaluation
- lifecycle participation
- debug view binding
- transform auto-write
- runtime stable-key resolution
- registry fallback
- save 近接 metadata

これらの観測は移行証拠であり、対象アーキテクチャではない。

### 観測の追跡可能性

| 観測 | 証拠種別 | 想定される圧力先 |
|---|---|---|
| `BlackboardMB` が 1 つの MonoBehaviour から service registration、handler、debug view、init data を登録している。 | ソース | schema、store、init、lifecycle、diagnostics を分離する |
| value identity が runtime で stable string から解決できる。 | ソース | runtime access は `ValueKeyId` を使わなければならない |
| 欠落した stable key に runtime-only の negative ID を割り当てられる。 | ソース | 欠落 identity は validation で失敗しなければならない |
| registry lookup が `Resources.Load` を使い、fallback runtime asset を生成できる。 | ソース | boot と runtime は検証済み input のみを消費すべきである |
| dynamic value が generic init の最中に評価できる。 | ソース | dynamic dependency は明示的 plan にしなければならない |
| grid cell に任意の var payload を載せられる。 | ソース | table / record cell は schema-backed でなければならない |
| save metadata が Blackboard、scalar、runtime scope binding の関心を混在させる。 | ソース | save metadata は schema-backed かつ deterministic でなければならない |

### 代表的な参照先

- [BlackboardMB.cs](../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs) - service registration、grid registration、debug view、transform auto-writer、lifecycle handler registration、複数経路の initialization
- [VarIdResolver.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs) - runtime stable-key resolution と runtime-only negative ID allocation
- [VarKeyRegistryLocator.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs) - `Resources.Load` による registry lookup と runtime fallback registry の生成
- [VarStore.cs](../../GameLib/Script/Common/Variables/VarStore/Core/VarStore.cs) - dictionary-backed な var/table storage、optional schema、revision、runtime type coercion
- [VarStorePayload.cs](../../GameLib/Script/Common/Variables/VarStore/Payload/VarStorePayload.cs) - dynamic value evaluation、deferred dynamic writes、table cell payload application
- [DeferredDynamicVarValue.cs](../../GameLib/Script/Common/Variables/VarStore/Core/DeferredDynamicVarValue.cs) - runtime value payload としての deferred dynamic evaluation
- [GridBlackboardService.cs](../../GameLib/Script/Common/Variables/Blackboard/Core/GridBlackboardService.cs) - 正規化された table schema の外にある、任意 payload の grid cell storage
- [ScopeBindingRegistryMB.cs](../../GameLib/Script/Common/Variables/Save/Binding/ScopeBindingRegistryMB.cs) - scope binding、value resolution、profile metadata、save registration の混在
- [SavePlanTypes.cs](../../GameLib/Script/Common/Variables/Save/Plan/SavePlanTypes.cs) - value metadata を消費すべきだが value schema は所有しない save payload の概念

### 現在のギャップ

対象アーキテクチャは次のギャップを塞がなければならない:

- value の存在が runtime write behavior からまだ学習できてしまう
- stable-key lookup が runtime identity resolution として振る舞えてしまう
- initialization が `Construct`、`Start`、`OnAcquire` から発生してしまう
- dynamic evaluation が generic initialization の中に依存関係を隠せてしまう
- grid / table data が schema validation をすり抜けられる
- save metadata が runtime store contents から推論されてしまう
- debug output が value failure の source-level provenance を欠くことがある

## Value Architecture Definition

対象 value architecture は 4 つの概念に分かれる:

1. `ValueSchema` は、どの value が存在し得るかを定義する。
2. `ValueStore` は runtime value state を保存する。
3. `ValueStoreInitPlan` は初期書き込みと既定 state を定義する。
4. `EvaluationPlan` は dynamic / reactive / computed evaluation を定義し、その詳細は 10-2 が所有する。

これらの概念を 1 つの component、service、MonoBehaviour、Blackboard facade にまとめてはならない。

10-1 は、検証済み value contract を消費する float 専用 scalar runtime と binding layer を定義するが、schema、save policy、dynamic evaluation の内部は再所有しない。

パイプライン:

```text
ValueContribution
  -> ValueKeyIR / ValueSchemaIR
  -> ValueSchemaPlan

ValueInitContribution
  -> ValueStoreInitPlan

Dynamic / Reactive Contribution
  -> DynamicEvaluationPlan / ReactiveEvaluationPlan

Runtime:
  ValueStore consumes ValueSchemaPlan and ValueStoreInitPlan
  Evaluation runtime semantics are owned by 10-2
```

## Value Identity Model

`ValueKeyId` は value の runtime identity である。

説明用モデル:

```csharp
public readonly struct ValueKeyId
{
    public readonly int Value;
}
```

関連する identity vocabulary:

- `ValueKeyId`
- `ValueSchemaId`
- `ValueStoreId`
- `ValueStoreScopeId`
- `ValueSlotId`
- `ValueTableId`
- `ValueFieldId`
- `ValueRevision`
- `ValueInitPlanId`

`StableKey` は authoring、diagnostics、registry validation、migration、DebugMap output のために存在し得る。
`StableKey` は runtime lookup の真実ではない。

runtime access は次を使わなければならない:

- `ValueKeyId`
- `ValueKeyId` に基づく generated accessor
- `ValueKeyId` への検証済み command payload reference
- 検証済み table / record field identity

禁止事項:

- raw string key による runtime value access
- 欠落した `ValueKeyId` の runtime 生成
- runtime-only negative value ID
- service identity を value identity として使うこと
- command payload の field name を `ValueKeyId` として使うこと
- save payload の field name を runtime value identity として使うこと

## StableKey 境界

`StableKey` は runtime truth ではない。

許可される `StableKey` の用途:

- editor search
- authoring display
- migration mapping
- diagnostics
- registry diff
- generated DebugMap

禁止される `StableKey` の用途:

- runtime read/write lookup
- runtime schema creation
- runtime fallback ID generation
- 事前検証されていない限り runtime save key generation
- runtime command value access

`StableKey` から `ValueKeyId` への変換は、runtime 実行の前に、normalization / validation / generation の過程で完了していなければならない。

値を runtime 前に `ValueKeyId` へ変換できないなら、その value は target kernel 実行に対して無効である。

## ValueSchema Model

`ValueSchema` は value data の許可された shape を定義する。

説明用モデル:

```csharp
public sealed class ValueSchemaPlan
{
    public ValueKeyId KeyId;
    public ValueSchemaId SchemaId;
    public string DebugName;
    public ValueKind Kind;
    public ValueStorageKind StorageKind;
    public ValueDefaultPolicy DefaultPolicy;
    public ValueAccessPolicy AccessPolicy;
    public SavePolicy SavePolicy;
    public SourceLocationId Source;
}
```

`ValueSchema` は次を定義しなければならない:

- value identity
- value kind
- storage kind
- default behavior
- read/write access policy
- initialization compatibility
- save metadata policy
- owner module
- source location
- profile availability

`ValueStore` は runtime write から schema を推論してはならない。

未知の `ValueKeyId` への書き込みは失敗しなければならない。
schema と互換性のない value の書き込みも失敗しなければならない。

## ValueKind and Type Model

対象の type model は schema-backed でなければならない。

説明用モデル:

```csharp
public enum ValueKind
{
    Null = 0,
    Bool = 10,
    Int = 20,
    Long = 30,
    Float = 40,
    Double = 50,
    String = 60,
    Vector2 = 70,
    Vector3 = 80,
    Color = 90,
    ObjectRef = 100,
    ManagedRef = 110,
    Record = 200,
    RecordList = 210,
    Table = 220,
    LayeredNumeric = 300,
}
```

このモデルは説明用であり、シリアライズ API を確定せず、既存の legacy `DynamicVariant.ValueKind` の numeric 値を置き換えない。

対象 vocabulary は次を区別しなければならない:

- scalar value
- Unity あるいは runtime object reference
- managed reference
- record
- record list
- table
- layered numeric value

`ManagedRef` は、schema が明示的に許可する場合に限り認められる。
`ManagedRef` 値は save、clone、reset、diagnostics の挙動を定義しなければならない。

schema が特定の conversion policy を宣言していない限り、silent type coercion は禁止である。

## ValueStore Runtime Definition

`ValueStore` は、`ValueSchemaPlan` に結び付いた runtime state container である。

`ValueStore` が所有するもの:

- value slot
- current value
- revision
- dirty flag
- optional table storage
- optional record storage
- optional layered numeric storage
- init application state
- diagnostics context

`ValueStore` が所有しないもの:

- schema generation
- dynamic evaluation graph
- reactive dependency graph
- save file writing
- command execution
- runtime object query
- service resolution
- lifecycle enrollment

`ValueStore` は検証済み schema と store scope input から作成されなければならない。
runtime write を観察して schema を組み立ててはならない。

## ValueStore Scope and Lifetime Model

value store は異なる runtime lifetime に存在し得る。

説明用モデル:

```csharp
public enum ValueStoreScopeKind
{
    Kernel = 10,
    Project = 20,
    Scene = 30,
    Scope = 40,
    Entity = 50,
    CommandLocal = 60,
    Test = 90,
}
```

scope ownership ルール:

- `Kernel` store は kernel-wide value にのみ有効である
- `Project` store は project runtime state に有効である
- `Scene` store は scene-local state に有効である
- `Scope` store は authored または verified runtime scope に有効である
- `Entity` store は service ではなく compact entity state にのみ有効である
- `CommandLocal` store は command execution boundary の内側にのみ有効である
- `Test` store は deterministic test fixture にのみ有効である

entity-scoped value は compact store slice か pooled store instance を優先すべきである。
entity ごとに heavy な dictionary-backed store を作ることは既定で禁止である。

`ValueStore` の lifetime は明示的でなければならない。
store reuse は、この仕様で定義され、08 を通じて実行される reset policy に従わなければならない。

## ValueStore Storage Model

`ValueStore` の storage は schema-indexed でなければならない。

推奨構造:

- schema が `ValueKeyId` を slot index に map する
- slot が型付き value を保持する
- slot には revision がある
- store には revision がある
- optional な kind ごとの backend が record、record list、table、layered numeric を扱う

説明用モデル:

```csharp
public enum ValueStorageKind
{
    InlineScalar = 10,
    InlineStruct = 20,
    ManagedReference = 30,
    RecordStorage = 40,
    RecordListStorage = 50,
    TableStorage = 60,
    LayeredNumericStorage = 70,
}
```

禁止事項:

- hot path での `Dictionary<string, object>`
- hot path での stable-key lookup
- first write による schema inference
- 避けられるのに common scalar を boxing すること
- read/write hot path での LINQ
- value access path での `Resources.Load`

slot lookup は O(1) か、小さな定数時間であるべきである。

## ValueStore Access Policy

`ValueStore` access は型付けされ、schema-validated でなければならない。

許可される access 形:

- `TryRead<T>(ValueKeyId, out T)`
- `TryWrite<T>(ValueKeyId, T)`
- `ReadRequired<T>(ValueKeyId)`
- `WriteRequired<T>(ValueKeyId, T)`
- `ValueKeyId` に基づく generated accessor

`TryRead` は、任意データや明示的 absence check に対して false を返してよい。
required read の失敗は structured diagnostics を報告しなければならない。

access policy は次を区別しなければならない:

- read
- write
- read/write
- init-only
- command-only
- save-only metadata
- debug-only display

禁止事項:

- `TryRead(string stableKey)`
- `TryWrite(string stableKey, object value)`
- write 時の暗黙的 key creation
- schema が変換を宣言していない限り silent type coercion
- policy なしで required value の欠落を default 扱いにすること

## ValueStoreInitPlan Model

`ValueStoreInitPlan` は、`ValueStore` に適用される初期書き込みを定義する。

次を定義しなければならない:

- target store scope
- target schema
- entries
- ordering
- overwrite policy
- source location
- profile availability
- execution phase

説明用モデル:

```csharp
public sealed class ValueInitEntryPlan
{
    public ValueKeyId KeyId;
    public ValueInitValueKind ValueKind;
    public ValuePayload Payload;
    public ValueInitOverwritePolicy OverwritePolicy;
    public SourceLocationId Source;
}
```

`ValueStoreInitPlan` は、既定では任意の `DynamicValue` を評価してはならない。
dynamic evaluation は明示的に表現されなければならない。

initialization は、08 で定義された明示的な lifecycle boundary で実行されなければならない。
`Construct`、`Start`、`OnAcquire` にまたがる multi-path initialization は target kernel path で禁止である。

## Initialization Ordering and Overwrite Policy

同じ `ValueKeyId` に対する重複 initialization entry は、plan が決定的な merge または overwrite policy を定義していない限り validation error である。

説明用モデル:

```csharp
public enum ValueInitOverwritePolicy
{
    ErrorIfExists = 10,
    KeepExisting = 20,
    Overwrite = 30,
    ClearIfNull = 40,
    Merge = 50,
}
```

ルール:

- `ErrorIfExists` は重複書き込みの既定である
- `KeepExisting` は existing の定義を明示しなければならない
- `Overwrite` は決定的で source-visible でなければならない
- `ClearIfNull` は schema による null compatibility を定義しなければならない
- `Merge` は value kind ごとの merge semantics を定義しなければならない

collection order による last-write-wins は禁止である。

init 順序は、Unity callback order、component discovery order、serialized list order に依存してはならない。list を deterministic order に正規化することを plan が明示している場合を除く。

## Table / Record / RecordList Policy

grid-like value は、別の Blackboard subsystem ではなく、`Table` または `RecordList` schema として表現しなければならない。

`Table` schema は次を定義しなければならない:

- row identity policy
- column identity policy
- cell schema
- sparse / dense storage policy
- default cell policy
- revision policy
- save policy
- diagnostics source

`Record` schema は次を定義しなければならない:

- field identity
- field kind
- required / optional status
- default policy
- nested schema compatibility

`RecordList` schema は次を定義しなければならない:

- element schema
- ordering policy
- identity policy
- mutation policy
- revision policy

grid storage は、schema なしで任意の var payload を cell ごとに隠してはならない。

legacy な grid payload で、cell ごとに任意の `VarStorePayload` を保存しているものは、runtime 前に table または record schema に正規化しない限り移行必須である。

## LayeredNumeric Policy

`LayeredNumeric` は contribution lane を持つ構造化 numeric value である。

既定の lane:

- `Base = 10`
- `PrefixMul = 20`
- `Add = 30`
- `SuffixMul = 40`
- `FinalClamp = 50`
- `Effective = 60`

Base と effective は区別できなければならない。

effective value は base と contributions から導出される。
schema が override を明示的に許可しない限り、effective を直接書くことは禁止である。

どの contribution を変更しても `LayeredNumeric` の revision は更新されなければならない。
依存シグナリングが正しければ、effective の再計算は lazy でもよい。

layered numeric policy は次を定義しなければならない:

- numeric type
- contribution ordering
- contribution identity
- conflict policy
- clear / reset policy
- revision behavior
- save behavior

## Revision and Dirty Signal Policy

書き込み可能な `ValueStore` slot には、すべて revision metadata が必要である。

必要な revision 概念:

- slot revision
- store revision
- optional record field revision
- optional table row revision
- optional table column revision
- optional table cell revision
- optional layered numeric effective revision

`ValueStore` は dirty signal を emit してもよいが、dirty evaluation は dependency system と reaction system に属する。

最小限の dirty signal data:

- `ValueKeyId`
- old revision
- new revision
- store id
- store scope
- optional scope handle
- optional entity handle

dirty signal は implicit reactive dependency graph になってはならない。
reactive graph の所有権は `ValueStore` の外にある。

## DynamicEvaluation Boundary

10 は、`ValueStore` initialization と dynamic evaluation の境界のみを所有する。
具体的な `DynamicValue`、tracker、cache、invalidation、nested dependency semantics は 10-2 が所有する。

DynamicValue 風 evaluation を `ValueStore` initialization の中に隠してはならない。

初期 value が runtime context に依存するなら、その依存関係は明示的でなければならない。

dynamic evaluation は次を宣言しなければならない:

- input dependencies
- evaluation timing
- fallback policy
- target `ValueKeyId`
- target store scope
- diagnostics source
- failure boundary

legacy 形:

```text
BlackboardMB entry.Value.Evaluate(ctx) during OnAcquire
```

target 形:

```text
DynamicEvaluationPlan
  inputs: ValueStore / RuntimeQuery / Scope / CommandFrame
  output: ValueKeyId
  phase: Init or Acquire
```

deferred dynamic value writes は generic init entry ではない。
dynamic evaluation plan として表現するか、拒否しなければならない。

詳細な source contract、tracked dependency capture、shared cache ownership、invalidation policy は 10-2 が所有する。

## ReactiveEvaluation Boundary

10 は、`ValueStore` の revision または dirty signal と reactive evaluation の境界のみを所有する。
具体的な tracked evaluation、cached computed value policy、invalidation rule、scheduling semantics は 10-2 が所有する。

reactive evaluation は `ValueStore` の所有ではない。

`ValueStore` が提供するもの:

- values
- revisions
- dirty signals

reactive evaluation が所有するもの:

- dependency graph
- tracked evaluation
- cached computed values
- invalidation
- scheduling
- failure boundary

`ValueStore` は `ReactiveResolver` になってはならない。

reactive dependency は、`ValueKeyId`、store scope、runtime query input を明示的に参照しなければならない。
詳細な tracker と reactive cache model は 10-2 が所有する。

## CommandLocal Boundary

`CommandLocal` は execution-local な value storage である。

`CommandLocal` は scope store ではない。
保存されない。
明示的に export されない限り、command boundary の外へ漏れてはならない。

有効な `CommandLocal` lifetime:

- command frame
- command sequence
- async wait boundary
- nested command block

`CommandLocal` は value-like な typed slot を使ってよいが、global Blackboard になることは許されない。

command-local data を persistent store に export するには、明示的な command write declaration と schema-compatible な target `ValueKeyId` が必要である。

## Command Access Boundary

`ValueStore` に対する command access は宣言されなければならない。

`CommandContribution` は次を宣言しなければならない:

- read `ValueKeyId` set
- write `ValueKeyId` set
- target store scope
- access phase
- failure behavior

command executor は runtime で stable key を解決してはならない。

command の write access は次を検証しなければならない:

- target key が存在する
- schema が write を許可している
- command に宣言済み access がある
- value type が schema に一致する
- target store scope が command frame に対して有効である

command の read access は次を検証しなければならない:

- target key が存在する
- schema が read を許可している
- command に宣言済み access がある
- optional read の absence policy が明示的である

## Save Metadata and Save Payload Boundary

save metadata は schema-backed でなければならない。

`ValueStore` は runtime store contents を走査して save target を推論してはならない。

save policy は次で定義してよい:

- `ValueSchema`
- 明示的な `SavePlan`
- profile-specific な save contribution

説明用モデル:

```csharp
public enum SavePolicy
{
    None = 0,
    RuntimeOnly = 10,
    Save = 20,
    SaveIfDirty = 30,
    SnapshotOnly = 40,
    MigrationOnly = 90,
}
```

この仕様は最終的な save payload format を定義しない。

ここで定義するのは、SaveSystem が deterministically payload を構築するために必要な value metadata である。

save metadata には次を含めなければならない:

- `ValueKeyId`
- store scope
- value kind
- storage kind
- save policy
- version または migration metadata
- source location
- profile availability

SaveSystem は、任意の runtime store contents を save authority とみなしてはならない。

## RuntimeQuery Boundary

`ValueStore` は handle を通じて runtime object と関連付けられてよい。

runtime object lookup は RuntimeQuery の責務である。

`ValueStore` は次を locate してはならない:

- entity
- scope
- actor
- UI root
- scene object
- Unity component

value access は RuntimeQuery が提供する handle を consume してよいが、自分で query を実行してはならない。

`ValueStore` は actor lookup、scope lookup、scene search、hierarchy search を実装してはならない。

## Diagnostics and DebugMap Requirements

value diagnostics には次を含めなければならない:

- `ValueKeyId`
- 利用可能なら stable key
- display name
- `ValueKind`
- schema id
- store id
- store scope
- owner module
- source location
- 利用可能なら current revision
- selected profile

init diagnostics にはさらに次を含めなければならない:

- `ValueInitPlanId`
- init entry source
- overwrite policy
- execution phase
- target store scope

table diagnostics にはさらに次を含めなければならない:

- `ValueTableId`
- row identity
- column identity
- cell schema id
- 利用可能なら cell revision

代表的な error code:

- `VALUE_KEY_MISSING`
- `VALUE_SCHEMA_MISSING`
- `VALUE_TYPE_MISMATCH`
- `VALUE_WRITE_ACCESS_DENIED`
- `VALUE_READ_ACCESS_DENIED`
- `VALUE_STABLE_KEY_RUNTIME_LOOKUP_FORBIDDEN`
- `VALUE_RUNTIME_ID_GENERATION_FORBIDDEN`
- `VALUE_INIT_DUPLICATE_ENTRY`
- `VALUE_INIT_TYPE_MISMATCH`
- `VALUE_INIT_DYNAMIC_DEPENDENCY_UNDECLARED`
- `VALUE_INIT_MULTIPATH_FORBIDDEN`
- `VALUE_TABLE_SCHEMA_MISSING`
- `VALUE_GRID_PAYLOAD_SCHEMA_REQUIRED`
- `VALUE_LAYERED_EFFECTIVE_WRITE_FORBIDDEN`
- `VALUE_SAVE_POLICY_INVALID`
- `VALUE_COMMAND_ACCESS_UNDECLARED`

DebugMap がなくても、diagnostics は stable numeric ID と stable error code を emit しなければならない。

## Failure Policy

value failure は、黙って修復してはならない。

failure category:

- `MissingKey`
- `MissingSchema`
- `TypeMismatch`
- `AccessDenied`
- `StoreDisposed`
- `InitConflict`
- `DynamicDependencyMissing`
- `StableKeyRuntimeLookup`
- `RuntimeIdGeneration`
- `SavePolicyInvalid`

failure boundary:

| Failure | Boundary |
|---|---|
| Boot schema failure | boot failure |
| Missing verified schema artifact | boot failure |
| Scope init failure | scope failure |
| Command write failure | command failure または frame failure |
| Reactive evaluation failure | reactive failure boundary |
| Save metadata failure | save operation failure |
| Runtime stable-key lookup attempt | operation failure and diagnostics |

fallback default は、schema が明示した場合にのみ許可される。
required value の欠落を runtime key creation、silent default creation、legacy registry fallback で修復してはならない。

## Performance and Memory Policy

`ValueStore` の read/write は runtime hot path である。

目標要件:

- hot path で stable-key lookup を行わない
- value access path で `Resources.Load` を使わない
- 通常の scalar read/write で managed allocation をしない
- 実用上可能な範囲で common scalar type を boxing しない
- hot path で LINQ を使わない
- slot lookup は O(1) か、小さな定数時間であるべきである
- revision update は安価でなければならない
- table access は sparse または dense の性能期待値を定義しなければならない
- dirty signal emission は無制限 allocation を避けなければならない

scalar 専用の hot-path、binding、handle-lifetime の budget は 10-1 が定義する。

entity-level の value data は compact storage を使わなければならない。
entity ごとに heavy な dictionary-backed store を作ることは既定で禁止である。

schema check、access policy check、revision update、profile で要求される diagnostics metadata を飛ばして performance を得てはならない。

## Legacy Migration Policy

| Legacy Pattern | Target Representation |
|---|---|
| `VarId` int | `ValueKeyId` |
| stable-key string lookup | pre-runtime の `ValueKeyId` mapping |
| `VarKeyRegistry` | 検証済み value registry input または generated artifact |
| `VarIdResolver` runtime negative IDs | target runtime では禁止 |
| `VarKeyRegistryLocator.Resources.Load` | boot-time の検証済み artifact reference |
| `BlackboardService` | `ValueStore` service または scope store facade |
| `GridBlackboardService` | `Table`、`Record`、`RecordList` store |
| `BlackboardMB` の local init | `ValueStoreInitContribution` |
| `BlackboardMB.OnAcquire` の init | 検証済み init を呼び出す `LifecycleContribution` |
| init entry 内の `DynamicValue` | `DynamicEvaluationPlan` |
| `DeferredDynamicVarValue` | 明示的な dynamic evaluation plan または移行拒否 |
| `TransformVarAutoWriterService` | 明示的な Transform-to-Value bridge contribution |
| `BlackboardDebugView` | diagnostics、DebugMap、または editor inspector |
| save における Blackboard / scalar metadata の混在 | schema-backed な save metadata projection |

legacy migration は runtime fallback semantics を保持してはならない。

legacy 名は diagnostics と migration report に現れてよいが、target runtime truth を定義してはならない。

## Forbidden Patterns

target の `ValueSchema` / `ValueStore` runtime で禁止されるもの:

- required value に対する runtime stable-key lookup
- 欠落した `ValueKeyId` の runtime creation
- runtime-only negative ID
- `Resources.Load` registry fallback
- runtime write からの schema 推論
- hot path の raw `Dictionary<string, object>`
- silent type coercion
- generic initialization の中に隠れた `DynamicValue` evaluation
- collection order で解決される duplicate init entry
- `Construct` / `Start` / `OnAcquire` にまたがる Blackboard 風 multi-path initialization
- `ValueStore` による runtime object または scope の解決
- global Blackboard として使われる `CommandLocal`
- arbitrary store contents を scan して save target を推論する SaveSystem
- schema のない arbitrary var payload を持つ grid cell
- 宣言済み read/write policy なしの command value access
- source または stable error code に戻せない value diagnostics

## Test Case Model

各 `ValueSchema` / `ValueStore` テストケースは次を定義しなければならない:

- Test ID
- Title
- `ValueSchemaPlan` fixture
- `ValueStore` fixture
- 必要に応じた `ValueStoreInitPlan` fixture
- Operation
- Expected result
- Expected diagnostics
- Expected revision changes
- 必要に応じた expected allocation または performance assertion

## Required Test Cases

### Identity / StableKey Tests

#### TC_VALUE_ID_001_ReadByValueKeyId

```text
入力:
- ValueSchema に `health.current` の `ValueKeyId` がある
- Store に value がある

操作:
- `ValueKeyId` で読む

期待結果:
- Passed
```

#### TC_VALUE_ID_002_StableKeyRuntimeLookupRejected

```text
操作:
- runtime で `"health.current"` によって value を読む

期待結果:
- Failed
- `VALUE_STABLE_KEY_RUNTIME_LOOKUP_FORBIDDEN`
```

#### TC_VALUE_ID_003_RuntimeNegativeIdRejected

```text
入力:
- `ValueKeyId = -1`

期待結果:
- Failed
- `VALUE_RUNTIME_ID_GENERATION_FORBIDDEN`
```

### Schema Tests

#### TC_VALUE_SCHEMA_001_WriteMatchingType

```text
入力:
- `ValueKey` の kind = `Int`

操作:
- int を書く

期待結果:
- Passed
- slot revision が増える
```

#### TC_VALUE_SCHEMA_002_WriteTypeMismatchRejected

```text
入力:
- `ValueKey` の kind = `Int`

操作:
- string を書く

期待結果:
- Failed
- `VALUE_TYPE_MISMATCH`
```

#### TC_VALUE_SCHEMA_003_WriteUnknownKeyRejected

```text
操作:
- schema に存在しない `ValueKeyId` に書く

期待結果:
- Failed
- `VALUE_KEY_MISSING`
```

### InitPlan Tests

#### TC_VALUE_INIT_001_InitPlanAppliesDefaults

```text
入力:
- InitPlan が `health.current = 100` を書く

期待結果:
- Store に `health.current = 100` が含まれる
```

#### TC_VALUE_INIT_002_DuplicateInitWithoutPolicyRejected

```text
入力:
- InitPlan に同じ `ValueKeyId` の entry が 2 つある
- merge / overwrite policy がない

期待結果:
- Failed
- `VALUE_INIT_DUPLICATE_ENTRY`
```

#### TC_VALUE_INIT_003_OverwritePolicyApplied

```text
入力:
- 既存 value がある
- Init overwrite policy = `KeepExisting`

期待結果:
- 既存 value が保持される
```

#### TC_VALUE_INIT_004_ConstructStartAcquireMultiPathForbidden

```text
入力:
- 同じ init が `Construct`、`Start`、`Acquire` に対して、明示的 policy なしで宣言されている

期待結果:
- Failed
- `VALUE_INIT_MULTIPATH_FORBIDDEN`
```

### Dynamic / Reactive Tests

#### TC_VALUE_DYNAMIC_001_DynamicInitRequiresEvaluationPlan

```text
入力:
- Init entry が `DynamicValue` を使う
- `DynamicEvaluationPlan` がない

期待結果:
- Failed
- `VALUE_INIT_DYNAMIC_DEPENDENCY_UNDECLARED`
```

#### TC_VALUE_DYNAMIC_002_DynamicInitWithDeclaredInputs

```text
入力:
- `DynamicEvaluationPlan` が input と output `ValueKeyId` を宣言している

期待結果:
- Passed
```

#### TC_VALUE_REACTIVE_001_RevisionChangeSignalsDirty

```text
操作:
- value を書く

期待結果:
- slot revision が増える
- dirty signal が emit される
```

### Table / Record Tests

#### TC_VALUE_TABLE_001_TableCellWriteValid

```text
入力:
- Table schema が存在する
- cell schema が value と一致する

期待結果:
- Passed
```

#### TC_VALUE_TABLE_002_CellWriteWithoutSchemaRejected

```text
入力:
- schema なしで Table cell を書く

期待結果:
- Failed
- `VALUE_TABLE_SCHEMA_MISSING`
```

#### TC_VALUE_TABLE_003_GridPayloadWithoutSchemaRejected

```text
入力:
- legacy grid cell が任意の `VarStorePayload` を持つ

期待結果:
- Failed または migration required
- `VALUE_GRID_PAYLOAD_SCHEMA_REQUIRED`
```

### LayeredNumeric Tests

#### TC_VALUE_NUMERIC_001_EffectiveRecomputedFromContributions

```text
入力:
- Base = 10
- Add +5
- PrefixMul 2

期待結果:
- Effective が schema order に従って計算される
- revision が更新される
```

#### TC_VALUE_NUMERIC_002_WriteEffectiveRejectedByDefault

```text
操作:
- Effective を直接書く

期待結果:
- Failed
- `VALUE_LAYERED_EFFECTIVE_WRITE_FORBIDDEN`
```

### Command Boundary Tests

#### TC_VALUE_CMD_001_CommandDeclaredWriteAllowed

```text
入力:
- command が `health.current` への write access を宣言している

期待結果:
- Write succeeds
```

#### TC_VALUE_CMD_002_CommandUndeclaredWriteRejected

```text
入力:
- command が access declaration なしで `health.current` を書く

期待結果:
- Failed
- `VALUE_COMMAND_ACCESS_UNDECLARED`
```

#### TC_VALUE_CMD_003_CommandStableKeyLookupRejected

```text
入力:
- command が stable string key で書こうとする

期待結果:
- Failed
- `VALUE_STABLE_KEY_RUNTIME_LOOKUP_FORBIDDEN`
```

### Save Tests

#### TC_VALUE_SAVE_001_SavePolicyIncludedInSchema

```text
入力:
- `ValueSchema` の `SavePolicy = Save`

期待結果:
- save metadata projection に key が含まれる
```

#### TC_VALUE_SAVE_002_RuntimeStoreScanNotSaveAuthority

```text
入力:
- runtime store に schema なしの value がある

期待結果:
- SaveSystem はそれを保存しない
- `VALUE_SAVE_SCHEMA_MISSING`
```

### Performance Tests

#### TC_VALUE_PERF_001_ScalarReadNoAllocation

```text
操作:
- scalar read を繰り返す

期待結果:
- 通常 path で managed allocation はない
```

#### TC_VALUE_PERF_002_ScalarWriteNoAllocation

```text
操作:
- scalar write を繰り返す

期待結果:
- 通常 path で managed allocation はない
```

#### TC_VALUE_PERF_003_NoResourcesLoadDuringRuntimeAccess

```text
操作:
- runtime 中に value を読み書きする

期待結果:
- `Resources.Load` の registry access はない
```

## 受け入れ基準

この仕様は、次を定義するときに完了である:

- Schema、Store、InitPlan、Evaluation に分かれた value architecture と、10-1 に委譲された scalar runtime
- `ValueKeyId` identity model
- `StableKey` 境界
- `ValueSchema` model
- `ValueKind` と type model
- `ValueStore` runtime definition
- `ValueStore` scope と lifetime model
- storage model
- read/write access policy
- `ValueStoreInitPlan` model
- initialization ordering と overwrite policy
- table、record、record list policy
- layered numeric policy
- revision と dirty signal policy
- DynamicEvaluation 境界
- ReactiveEvaluation 境界
- CommandLocal 境界
- command access 境界
- save metadata 境界
- RuntimeQuery 境界
- diagnostics と DebugMap 要件
- failure policy
- performance と memory policy
- legacy migration policy
- forbidden patterns
- required test cases

この仕様は、required value が runtime fallback によって作成、lookup、保存できる状態のままでは完了していない。

## テストケース

| テストケース | 目的 | 検証 |
|---|---|---|
| TC-10-01 | runtime value access が `ValueKeyId` ベースであることを確認する。 | identity と stable-key の節で runtime stable-key lookup と runtime ID generation を拒否する。 |
| TC-10-02 | 書き込みが schema-bound であることを確認する。 | schema、access、failure の節で unknown key と type mismatch を拒否する。 |
| TC-10-03 | init ordering が明示的であることを確認する。 | init と overwrite の節で multi-path init と collection-order の last-write-wins を拒否する。 |
| TC-10-04 | dynamic evaluation が init に隠れていないことを確認する。 | Dynamic boundary と required tests で `DynamicEvaluationPlan` を要求する。 |
| TC-10-05 | table / record value が schema-backed であることを確認する。 | table と record policy で schema なしの arbitrary grid payload を拒否する。 |
| TC-10-06 | hot path で fallback lookup を使わないことを確認する。 | performance と forbidden の節で stable-key と `Resources.Load` の runtime path を拒否する。 |

## 最終見解

`ValueStore` は、`ValueKeyId` によって検証済み value を保存する。

キーを発見したり、スキーマを推論したり、隠れた dynamic expression を評価したり、runtime で欠落データを修復したりしない。

value が `ValueSchema` なしで runtime に現れ得るなら、その時点でアーキテクチャは退化している。

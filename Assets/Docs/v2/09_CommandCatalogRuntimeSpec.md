# CommandCatalog ランタイム仕様

## 文書ステータス

- 文書 ID: 09_CommandCatalogRuntimeSpec
- ステータス: Draft
- 役割: Kernel v2 におけるコマンドの runtime identity、payload schema 検証、executor 解決、CommandRunner 挙動、およびコマンド失敗ルールを定義する
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
- 取り込むもの:
  - CommandIR
  - CommandCatalogPlan
  - ServiceGraphPlan references
  - ValueSchemaPlan references
  - RuntimeQueryPlan references
  - ScopeGraphPlan references
  - LifecyclePlan references
  - KernelDebugMap
- 基盤を提供するもの:
  - 10_ValueSchemaAndStoreSpec.md
  - 11_DebugMapAndDiagnosticsSpec.md
  - 12_UnityAuthoringBridgeSpec.md
  - 13_LegacyCompatBoundarySpec.md
  - 14_PerformanceBudgetAndRuntimeRulesSpec.md
  - 15_TestAndValidationSpec.md

### 所有範囲

この仕様は、コマンドの runtime identity、payload schema 検証、executor lookup、コマンド実行フロー、command-local state、コマンド失敗挙動、コマンド診断要件を所有する。
サービスキャッシュ、value storage 構成、runtime query 保存、scope graph 構造、lifecycle dispatch、command authoring UI、visual scripting editor UI、SaveSystem の内部、SceneFlow の内部は所有しない。

この仕様が所有するもの:

- CommandCatalog の runtime 責務
- CommandCatalogPlan の runtime 入力契約
- CommandTypeId の runtime dispatch 契約
- authoring key と runtime identity の境界
- command contribution projection の境界
- command payload schema と validation policy
- command executor モデル
- executor factory と lifetime policy
- CommandRunner の責務
- CommandFrame、CommandContext、CommandLocal の policy
- control-flow command policy
- async / wait / cancellation / detached execution policy
- command の service / value / runtime query / scope / entity / actor / lifecycle 境界
- command module と category policy
- command 診断と DebugMap 要件
- command 失敗ポリシー
- command 性能とメモリのルール
- レガシー command 移行ポリシー

この仕様が所有しないもの:

- ServiceGraph の service cache 実装
- ValueStore の保存構成
- RuntimeQuery の index 実装
- ScopeGraph の親子構造実装
- LifecyclePlan の phase dispatch
- command authoring UI の詳細
- visual scripting editor UI の詳細
- SaveSystem の永続化意味論
- SceneFlow の遷移意味論

09 は runtime のコマンド権威である。
ServiceGraph、ValueStore、RuntimeQuery、ScopeGraph、LifecyclePlan の代替ではない。

---

## 目的

この仕様は、対象 kernel が検証済み CommandCatalogPlan からコマンドをどのように解決し、実行するかを定義する。

CommandCatalog は、コマンド identity、payload schema、executor 解決、および dispatch metadata を所有する。
コマンド executor は ServiceGraph から発見しない。
command authoring key は runtime dispatch identity ではない。
コマンド実行は、検証済み CommandTypeId と検証済み payload schema を使わなければならない。

09 の中心的な主張は次の通りである。

```text
Command dispatch は検証済み CommandTypeId によって table-driven に行われる。
DI registration から発見されるものでも、authoring string から解決されるものでもない。
```

新しいコマンドを追加するたびに巨大な runtime installer を編集しなければならないなら、その時点でアーキテクチャは退化している。

---

## スコープ

この仕様が定義するもの:

- CommandCatalog の runtime 責務
- CommandCatalogPlan の入力契約
- command identity モデル
- authoring key の境界
- command contribution projection の境界
- command payload schema モデル
- payload validation policy
- command executor モデル
- executor factory と lifetime policy
- CommandRunner の責務
- CommandFrame と CommandContext モデル
- CommandLocal state policy
- control-flow command policy
- async / wait / cancellation / detached execution policy
- ServiceGraph 境界
- ValueStore 境界
- RuntimeQuery 境界
- scope / entity / actor target 境界
- LifecyclePlan 境界
- command module と category policy
- command 診断と DebugMap 要件
- command 失敗ポリシー
- command 性能とメモリの policy
- レガシー command 移行ポリシー
- command runtime テストケースモデルと必須テスト

---

## 対象外

この仕様が定義しないもの:

- 最終的な ServiceGraph cache 実装
- 最終的な ValueStore 保存構成
- 最終的な RuntimeQuery index 保存
- 最終的な ScopeGraph ハンドル構成
- 最終的な LifecycleDispatcher 実装
- 最終的な SaveSystem 実装
- 最終的な SceneFlow 実装
- command authoring inspector UI
- visual scripting editor UI
- command authoring 用 asset menu layout

この仕様は CommandCatalog を次のものへ変質させてはならない:

- 汎用 DI container
- service registry
- lifecycle registry
- runtime query registry
- value key resolver
- string-key fallback resolver
- 巨大な runtime installer

---

## 他仕様との関係

| 仕様 | 関係 |
|---|---|
| [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md) | コマンドディスパッチを、明示的・検証済み・authoring-key lookup から分離されたものとして定義する。 |
| [01_KernelIRSpec.md](01_KernelIRSpec.md) | ここで参照する CommandIR、CommandTypeId、command payload schema reference、executor reference、typed identity rules を定義する。 |
| [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md) | CommandContribution を宣言的入力として定義し、executor registration や installer mutation ではないとする。 |
| [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md) | CommandCatalogPlan を生成し、runtime 前に projection の完全性を検証する。 |
| [04_DependencyValidationSpec.md](04_DependencyValidationSpec.md) | command identity、executor reference、payload schema reference、service/value/query dependency、authoring-key の誤用を検証する。 |
| [05_BootManifestAndProfileSpec.md](05_BootManifestAndProfileSpec.md) | boot は 1 つの検証済み artifact set を受け入れ、既定で全 executor を早期構築してはならない。 |
| [06_ServiceGraphRuntimeSpec.md](06_ServiceGraphRuntimeSpec.md) | コマンド実行に必要な明示的 service dependency を提供するが、command executor は発見してはならない。 |
| [07_ScopeGraphRuntimeSpec.md](07_ScopeGraphRuntimeSpec.md) | command context と target reference が使う明示的な scope handle と scope state boundary を提供する。 |
| [08_LifecyclePlanSpec.md](08_LifecyclePlanSpec.md) | ライフサイクル参加を所有する。CommandRunner は lifecycle target になり得るが、command execution 自体は lifecycle step を enroll しない。 |
| 10_ValueSchemaAndStoreSpec.md | value schema と storage を所有する。command は検証済み ValueKeyId と宣言済み access policy を通じてのみ value にアクセスする。 |
| 11_DebugMapAndDiagnosticsSpec.md | command runtime が使う shared structured diagnostics substrate と DebugMap runtime contract を所有する。09 は必要な command provenance fields、payload 関連 diagnostics context、失敗挙動を定義する。 |
| 12_UnityAuthoringBridgeSpec.md | command authoring object、command key、payload authoring を CommandContribution / CommandIR に正規化する。 |
| 13_LegacyCompatBoundarySpec.md | 許可された legacy command adapter と migration 境界を定義する。 |
| 14_PerformanceBudgetAndRuntimeRulesSpec.md | command dispatch budget、allocation rule、profiler marker 要件を定義する。 |
| 15_TestAndValidationSpec.md | 実行可能な command catalog 検証と regression fixture を定義する。 |

---

## asmdef とコンパイル境界の期待値

このサブシステムの想定 asmdef は `GameLib.Kernel.Command` である。
詳細な依存行列は [17_AssemblyDefinitionAndCompileBoundarySpec.md](17_AssemblyDefinitionAndCompileBoundarySpec.md) が所有する。

09 に必要なコンパイル境界ルール:

- `GameLib.Kernel.Command` は feature executor 実装、legacy command runner、Unity authoring 抽出コードから分離されたままでなければならない
- command runtime core は、下位の kernel assembly と Runtime / ServiceGraph / ScopeGraph / RuntimeQuery が提供する明示的 public contract のみに依存すべきである
- service collection、installer mutation、feature back-reference による command executor discovery を `GameLib.Kernel.Command` に入れてはならない
- Unity 固有の command trigger、MonoBehaviour bridge、feature command leaf は command core assembly の外に置かなければならない

検証済みの command dispatch が feature executor、legacy runner code、runtime string-key lookup helper なしにコンパイルできないなら、09 の境界は破られている。

---

## 現在のコマンド負債の観測

現在の command runtime は、command 発見、runner 生成、catalog lookup、lifecycle enrollment、key 解決、fallback 挙動、diagnostics binding を混在させている。

観測されている command debt には次が含まれる:

- DI registration を通じた command executor 発見
- `IReadOnlyList<ICommandExecutor>` からの command executor map 構築
- scope kind switch による command runner 生成
- service registration による command runner の lifecycle 参加
- runtime service としての command key 解決
- runtime service としての command catalog lookup
- `Resources.Load` と runtime fallback catalog 生成
- runtime-only の command key ID
- catalog entry からの stable-key fallback
- build callback を通じた command debug viewer binding
- boot registration コストに連動する executor 数
- 1 つの installer に混在する command category
- `VarStore` を通じて混ざる command-local と value-store の責務
- executor 内部で行われる actor / scope / target / channel routing

対象の CommandCatalog は、この曖昧さを残してはならない。

### 観測の追跡可能性

これらの観測は移行証拠であり、対象アーキテクチャではない。

- [CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs) - bulk executor registration、scope kind ごとの runner registration、lifecycle handler registration、debug viewer build callback
- [CommandRunner.cs](../../GameLib/Script/Common/Commands/VNext/Core/CommandRunner.cs) - runtime execution flow、registry lookup、failure handling、context slot、lifecycle handler 実装
- [CommandExecutorRegistry.cs](../../GameLib/Script/Common/Commands/VNext/Core/CommandExecutorRegistry.cs) - `IReadOnlyList<ICommandExecutor>` を走査して command-id lookup table を構築
- [CommandCatalogService.cs](../../GameLib/Script/Common/Commands/VNext/Catalog/CommandCatalogService.cs) - runtime service 経由の acquire-time catalog loading
- [CommandCatalogLocator.cs](../../GameLib/Script/Common/Commands/VNext/Catalog/CommandCatalogLocator.cs) - editor asset lookup、runtime `Resources.Load`、fallback ScriptableObject 生成
- [CommandCatalogSO.cs](../../GameLib/Script/Common/Commands/VNext/Catalog/CommandCatalogSO.cs) - stable-key catalog entry と fallback stable-key scan
- [CommandKeyResolver.cs](../../GameLib/Script/Common/Commands/VNext/Catalog/CommandKeyResolver.cs) - runtime-only の negative key ID と stable-key fallback 挙動
- [CatalogCommandSource.cs](../../GameLib/Script/Common/Commands/VNext/Sources/CatalogCommandSource.cs) - stable key と runtime fallback option を通じた command source 解決
- [CommandContext.cs](../../GameLib/Script/Common/Commands/VNext/Core/CommandContext.cs) - actor / scope slot、resolver exposure、registry ベースの scope resolution

### 代表的な参照先

`CommandRunnerMB` は、Project scope に大量の executor を `ICommandExecutor` として登録している。
その一覧には、control-flow command、actor routing command、transform command、movement command、UI command、tooltip command、mesh command、animation command、camera command、scene command、save command、map command、trait command、collision command、value command、debug command が含まれる。

`CommandRunnerMB` は、`LifetimeScopeKind` に応じて異なる command runner contract も登録している。
Project、Platform、Global、Scene、Field、Entity、UI、UIElement の runner variant が、検証済み plan ではなく runtime switch で install されている。

`CommandExecutorRegistry` は、`IReadOnlyList<ICommandExecutor>` を走査して executor lookup を構築する。
そのため executor の利用可否は、DI collection の挙動と boot registration の完全性に依存する。

`CommandCatalogService` は acquire / release lifecycle に参加し、runtime で catalog state を load する。
対象の command catalog 生成は、lifecycle-time の locator 挙動ではなく、検証済み artifact set 入力から行われなければならない。

`CommandCatalogLocator` は editor asset search と runtime `Resources.Load` を行う。
runtime load が失敗すると fallback catalog instance を作成する。
対象 runtime は、この方法で不足した command catalog 入力を修復してはならない。

`CommandKeyResolver` は、fallback が許可されている場合に runtime-only の negative key ID を割り当てられる。
対象 runtime command dispatch は、runtime-only の authoring key 修復を必要としてはならない。

`CatalogCommandSource` は stable key を通じて command を解決し、許可されている場合は stable-key catalog lookup にフォールバックする。
対象 runtime は、command 実行の前に正規化された CommandTypeId を使わなければならない。

`CommandRunner` は execution flow と diagnostics を所有する一方で、registry、catalog、key resolver、scope resolver、VarStore、lifecycle handler interface にも依存している。
対象の 09 では、runner execution を catalog generation、executor 発見、value schema 所有、runtime query lookup、lifecycle enrollment から分離する。

### 現在のギャップ

現在の command runtime には次のギャップがある:

- command identity が numeric command ID、stable key、authoring key に分裂している
- executor 発見が bulk DI registration に依存している
- payload schema が command data class ではなく、検証済み runtime schema として暗黙化されている
- catalog lookup が stable-key scan にフォールバックできる
- 不足した catalog asset が runtime fallback 生成で修復され得る
- command runner domain が scope kind switch から推論されている
- command execution context が広範な resolver access を露出している
- actor、scope、channel、UI target の lookup が executor 内で行われ得る
- command-local state と persistent value state が generic VarStore の使用を通じて曖昧になる
- async command 挙動が executor ごとに、統一されていない cancellation / detached execution policy なしで実装され得る
- lifecycle 参加が interface registration によって command runner service に付与され得る
- command debug binding が DebugMap projection ではなく build callback で実装され得る

---

## CommandCatalog の runtime 定義

CommandCatalog は、command executor lookup metadata の runtime owner である。

CommandCatalog が所有するもの:

- CommandTypeId lookup
- executor reference table
- payload schema reference table
- command module metadata
- command category metadata
- command diagnostics metadata
- executor factory または instance policy
- profile availability metadata

CommandCatalog が所有しないもの:

- service resolution
- value storage
- runtime query index
- lifecycle enrollment
- scope graph 構造
- actor search
- command authoring UI
- command source normalization

CommandCatalog は `IReadOnlyList<ICommandExecutor>` の解決として実装してはならない。

---

## CommandCatalogPlan 入力契約

CommandCatalog は、検証済み CommandCatalogPlan からのみ作成できる。

有効な CommandCatalogPlan には次が含まれなければならない:

- artifact header
- CommandCatalogPlanId
- CommandTypeId の集合
- payload schema references
- executor references
- owner module metadata
- category metadata
- dependency metadata
- profile availability metadata
- diagnostics metadata
- DebugMap の連携
- generator と format の version

CommandCatalog は次を拒否しなければならない:

- 未検証の CommandCatalogPlan
- 不完全な artifact set
- 古い command catalog artifact
- 不一致の KernelIR hash
- 不一致の profile hash
- payload schema table の欠落
- executor table の欠落
- diagnostics table の欠落
- CommandIR に存在しない command identity

CommandCatalog は、target kernel path で runtime の ad-hoc command registration を受け入れてはならない。

development-only の command 拡張は、下位仕様がそれを境界付き・profile-scoped・diagnostic-visible な extension point として定義している場合にのみ許可される。
release profile は、13 が互換ブリッジを明示的に定義していない限り、dynamic command registration を受け入れてはならない。

---

## コマンド識別モデル

CommandTypeId は runtime dispatch identity である。

command identity domain には次が含まれる:

- CommandTypeId
- CommandCategoryId
- CommandPayloadSchemaId
- CommandExecutorId
- CommandAuthoringKeyId

説明用のスケッチ:

```csharp
public readonly struct CommandTypeId
{
    public readonly int Value;
}

public readonly struct CommandPayloadSchemaId
{
    public readonly int Value;
}

public readonly struct CommandExecutorId
{
    public readonly int Value;
}

public readonly struct CommandCategoryId
{
    public readonly int Value;
}

public readonly struct CommandAuthoringKeyId
{
    public readonly int Value;
}
```

このスケッチは説明用であり、シリアライズ API を確定するものではない。

識別ルール:

- CommandTypeId は runtime dispatch に使う
- CommandExecutorId は executor 実装または生成された function reference を識別する
- CommandPayloadSchemaId は command type に必要な payload schema を識別する
- CommandCategoryId は grouping、filtering、editor display、diagnostics、report 用である
- ModuleId は所有モジュールを識別する
- CommandAuthoringKeyId は、保持される場合の正規化済み authoring key metadata を識別する

CommandCategoryId は dispatch identity ではない。
CommandAuthoringKey も dispatch identity ではない。
生の C# type name も dispatch identity ではない。
生の string command name も dispatch identity ではない。

authoring-key metadata が正規化済み IR に保持される場合、それは型付けされ provenance-aware であるべきである。
単なる string field では不十分である。runtime-facing code が authoring text を権威として誤認しやすくなるからである。
保持された command authoring-key metadata は、projection と DebugMap artifact にも追跡可能でなければならない。

---

## AuthoringKey 境界

authoring key は次のために存在する:

- editor authoring
- search
- migration
- debug output
- human-readable display
- authoring compatibility reports

runtime dispatch は任意の文字列を使ってはならない。

authoring key から CommandTypeId への変換は、runtime 実行の前に、normalization、validation、または verified generation の段階で行わなければならない。

禁止事項:

- runtime で string から executor を解決すること
- raw command name で dispatch すること
- authoring key を CommandTypeId として使うこと
- stable key を CommandTypeId として使うこと
- 欠落した CommandTypeId から authoring key lookup へ fallback すること
- 欠落した CommandTypeId から stable-key catalog scan へ fallback すること
- raw command name による runtime registry lookup
- target-kernel correctness のための runtime-only command key 割り当て

AuthoringKey は、runtime identity がすでに分かっている場合の diagnostics、または失敗した normalization / migration diagnostics の一部としてのみ現れてよい。

---

## Command Contribution Projection

CommandContribution は、command runtime projection の宣言的 source である。

projection path は次の通りである:

```text
CommandContribution
  -> CommandIR
  -> CommandCatalogPlan
  -> CommandCatalog
```

CommandContribution は次を宣言してよい:

- command authoring key
- 正規化済み command identity request
- payload schema reference または inline schema contribution
- executor reference
- owner module
- category metadata
- service dependencies
- value dependencies
- runtime query dependencies
- profile availability
- diagnostics source

検証済みの command catalog projection は、これらの宣言を、平坦な runtime string table ではなく、module と category の metadata をまとめた構造化 `CommandEntryPlan` row として保持すべきである。

CommandContribution は次を行ってはならない:

- command executor を instantiate する
- command executor を ServiceGraph に登録する
- `ICommandExecutor` を登録する
- lifecycle handler を追加する
- arbitrary string から executor identity を解決する
- generation の外側で runtime catalog entry を作る
- authoring key を runtime の真実にする

生成された CommandCatalogPlan は、CommandContribution と CommandIR から provenance を保持しなければならない。
projection が command identity、payload schema、executor reference、diagnostics provenance を証明できないなら、generation は失敗する。

---

## Command Payload Schema モデル

すべての command type は、EmptyPayload を明示的に宣言していない限り payload schema を定義しなければならない。

payload schema は次を定義しなければならない:

- schema identity
- field identity
- field name
- value kind
- 必須か任意か
- default value policy
- 許可される source kind
- serialization policy
- validation policy
- profile availability
- diagnostics source

説明用スケッチ:

```csharp
public sealed class CommandPayloadSchemaPlan
{
    public CommandPayloadSchemaId SchemaId;
    public CommandTypeId CommandTypeId;
    public CommandPayloadFieldSchema[] Fields;
    public CommandPayloadUnknownFieldPolicy UnknownFieldPolicy;
    public SourceLocationId Source;
}

public sealed class CommandPayloadFieldSchema
{
    public CommandPayloadFieldId FieldId;
    public string Name;
    public ValueKind Kind;
    public bool Required;
    public CommandPayloadDefaultPolicy DefaultPolicy;
```

```csharp
public enum CommandPayloadUnknownFieldPolicy
{
    Reject = 10,
    IgnoreWithWarning = 20,
    PreserveForMigration = 30,
}
```

このスケッチは説明用であり、シリアライズ API を確定するものではない。

既定の unknown field policy は `Reject` である。

executor は、任意にシリアライズされた object を runtime reflection して payload shape を推測してはならない。
reflection は、editor generation または migration tooling に限って使用してよいが、runtime の前に生成済み schema が明示されていなければならない。

---

## Command Payload Validation Policy

payload validation は executor 実行の前に行われなければならない。

validation は次を確認しなければならない:

- command type が存在する
- payload schema が存在する
- payload schema が CommandTypeId と一致する
- 必須 field が存在する
- field type が schema と一致する
- unknown field が policy に従う
- default value が有効である
- 参照された service が存在する
- 参照された ValueKeyId が存在する
- 参照された RuntimeQueryId が存在する
- 参照された scope / entity / actor target ref が有効である
- payload source location が diagnostics に利用できる

runtime の command execution は、事前検証済み payload を使ってよい。
runtime が schema-valid と印されていない payload を受け取った場合、runner は executor 呼び出しの前にそれを検証するか、拒否しなければならない。

payload validation failure は、executor 側の cast、default construction、string-key fallback によって修復してはならない。

---

## Command Executor モデル

command executor は、CommandCatalogPlan 内の CommandExecutorId または CommandTypeId の対応で解決される。

説明用モデル:

```csharp
public enum CommandExecutorKind
{
    GeneratedFunction = 10,
    StatelessSingleton = 20,
    LazySingleton = 30,
    PooledInstance = 40,
    LegacyAdapter = 90,
}
```

許可される executor 形式:

- 生成された static function
- state を持たない共有 singleton
- lazy singleton
- pooled executor instance
- 移行中の明示的 legacy adapter

ServiceGraph を通じた executor 発見は禁止である。

executor は、CommandContribution によって宣言され 04 で検証された依存のみを、CommandExecutionContext を通じて要求してよい。

executor は次を行ってはならない:

- 宣言されていない依存に対して ServiceGraph を ad-hoc に検索すること
- ServiceGraph を通じて runtime target を解決すること
- stable string key で value を解決すること
- scene 全体検索を行うこと
- reflection により command payload schema を推測すること
- interface scan によって lifecycle handler になること

---

## Executor Factory と Lifetime ポリシー

CommandCatalog は、executor policy に従って executor を instantiate してよい。

executor factory policy は CommandCatalogPlan において明示的でなければならない。

許可される policy:

- 生成された static function
- state を持たない共有 singleton
- lazy singleton
- pooled executor instance
- 移行中の legacy adapter

CommandCatalog は、明示的に予算化されていない限り、boot 中にすべての executor を早期構築してはならない。

command 数が増えると catalog metadata size は増えてよい。
しかし、boot 時にすべての executor を構築する必要はない。

executor factory failure は command diagnostics failure である。
そこには CommandTypeId、CommandExecutorId、owner module、source location、selected profile、suggested fix を含めなければならない。

---

## CommandRunner の責務

CommandRunner は command execution flow を所有する。

CommandRunner は次を行ってよい:

- CommandFrame を作成する
- payload を validate する
- CommandCatalog を通じて executor を resolve する
- command を実行する
- command sequence を実行する
- control-flow command を処理する
- cancellation を管理する
- command failure boundary を適用する
- command diagnostics を記録する
- command-local execution state を維持する

CommandRunner は次を行ってはならない:

- executor を発見する
- executor を登録する
- executor catalog metadata を所有する
- runtime authoring-key lookup を行う
- ServiceGraph になる
- RuntimeQuery になる
- ValueStore になる
- LifecycleDispatcher になる
- dynamic な command catalog entry を作る

runner domain は明示的でなければならない。

説明用モデル:

```csharp
public enum CommandExecutionDomain
{
    Kernel = 10,
    Project = 20,
    Scene = 30,
    Entity = 40,
    UI = 50,
    Test = 90,
}
```

複数の command runner が許可されるのは、単にすべての scope kind ごとである場合ではなく、異なる execution domain を表している場合のみである。

mass entity に対する entity-domain runner は既定で禁止である。
entity-domain runner の例外は、次をすべて満たす場合にのみ許可される:

- entity が境界付きの作成済み aggregate root である
- 予想 instance 数が宣言されている
- performance budget が宣言されている
- lifecycle ownership が明示的である
- diagnostics に source location と runtime handle が含まれている
- `CommandRunnerInstancePolicy` が検証済み plan から生成されている

---

## CommandFrame と CommandContext モデル

CommandFrame は 1 回の実行フレームを表す。

CommandFrame には次を含めてよい:

- CommandFrameId
- parent frame
- execution domain
- CommandTypeId
- CommandPayloadSchemaId
- actor reference
- target reference
- scope reference
- ValueStore access handle
- RuntimeQuery access handle
- cancellation token
- command-local state reference
- diagnostics context

CommandContext は、command から見える安定した実行環境を表す。

CommandContext は次を公開してよい:

- current scope handle
- actor reference
- target reference
- command root reference
- command-local state
- 宣言済み service dependency access
- 宣言済み ValueStore access
- 宣言済み RuntimeQuery access
- 選択された profile
- diagnostics writer

CommandFrame は、schema を持たない任意 object の loose dictionary であってはならない。
CommandContext は、対象 kernel path で無制限の runtime resolver access を露出してはならない。

---

## CommandLocal State Policy

CommandLocal は execution-local state を保存する。

CommandLocal は明示的に次のいずれか 1 つにスコープされなければならない:

- command frame
- command sequence
- async wait boundary
- nested command block

CommandLocal は global Blackboard になってはならない。

CommandLocal のルール:

- 一時データは CommandLocal に属する
- 永続 state または scope-bound runtime state は ValueStore に属する
- target handle は structured context slot または RuntimeQuery result に属する
- command-local state の寿命は diagnostics から見える必要がある
- command-local key は ValueKeyId identity と衝突してはならない

CommandLocal は、下位仕様が境界付きの migration-only adapter を定義していない限り、任意の string key を唯一の runtime identity として使ってはならない。

---

## Control Flow Command Policy

control-flow command は、任意の executor side effect ではなく、command execution model の一部である。

control-flow command type は次を定義しなければならない:

- child command の実行順序
- child command の validation 要件
- failure propagation
- cancellation 挙動
- local context の継承
- async wait 挙動
- loop の境界
- diagnostics 挙動
- child reference の source location

control-flow command の例:

- sequence
- if
- switch
- for
- wait
- action block
- break
- detached execution

loop command は、安全限界または明示的な unbounded policy を定義しなければならない。

detached execution command は detached execution policy を定義しなければならない。
それらは暗黙的に fire-and-forget 作業を作ってはならない。

child command reference は、runtime execution の前に検証されなければならない。

---

## Async / Wait / Cancellation ポリシー

command execution は同期でも非同期でもよい。

async command は次を定義しなければならない:

- awaited completion behavior
- cancellation token source
- timeout policy
- failure policy
- cancellation 時に child command が継続するかどうか
- wait 中に command frame が生き続けるかどうか
- cancellation と timeout の diagnostics

command type が detached execution policy を明示的に宣言していない限り、fire-and-forget の command execution は禁止である。

detached execution policy は次を定義しなければならない:

- owner frame
- owner scope
- cancellation source
- failure reporting destination
- diagnostics visibility
- shutdown behavior

command cancellation が result に影響する場合、structured diagnostics を生成しなければならない。
timeout が result に影響する場合、structured diagnostics を生成しなければならない。

---

## ServiceGraph 境界

command executor は、宣言済み dependency を通じてのみ ServiceGraph を使ってよい。

CommandCatalog は ServiceGraph から executor を発見しない。
ServiceGraph は `ICommandExecutor` を収集しない。

禁止事項:

- `IReadOnlyList<ICommandExecutor>` の解決
- `.As<ICommandExecutor>()` による bulk discovery
- service contract scan からの executor identity
- ServiceGraph fallback を通じた command dependency repair
- ServiceGraph を通じた runtime object の解決
- ServiceGraph を通じた command target の解決

executor が service を必要とするなら、その依存は CommandContribution で宣言され、CommandIR または CommandCatalogPlan に projection され、04 で検証されなければならない。

---

## ValueStore 境界

command は、検証済み ValueKeyId と宣言済み access policy を通じてのみ ValueStore を read / write してよい。

説明用モデル:

```csharp
public enum CommandValueAccessKind
{
    None = 0,
    Read = 10,
    Write = 20,
    ReadWrite = 30,
}
```

command は runtime の stable string key で value を解決してはならない。

value access は次を定義しなければならない:

- ValueKeyId
- access kind
- owner module
- scope または domain boundary
- default value policy
- failure policy
- diagnostics source

write command は、現在の runtime value から value schema を推測してはならない。
value schema は 10 が所有する。

---

## RuntimeQuery 境界

command target lookup は、検証済み RuntimeQuery dependency を使わなければならない。

executor は次を行ってはならない:

- scene search
- hierarchy search
- raw component search
- ServiceGraph ベースの runtime object lookup
- 任意の actor name lookup
- transform-parent による target 推論

actor routing、player routing、channel routing、hit collider target routing、UI root routing、camera target routing は、宣言済み RuntimeQuery dependency または明示的な context target として表現されなければならない。

RuntimeQuery dependency は runtime execution の前に検証されなければならない。
runtime query が不足している場合は、command validation または command execution が structured diagnostics とともに失敗しなければならない。

---

## Scope / Entity / Actor Target 境界

command は、検証済み handle または query result を通じて runtime object を target にできる。

許可される target reference:

- ScopeHandle
- EntityHandle
- PartHandle
- ActorRef
- RuntimeQueryResult
- CommandFrame の target slot
- 明示的に生成された target handle

禁止事項:

- raw Transform search
- raw GameObject.Find
- component ancestor scan
- 任意の string による actor lookup
- current scene object への fallback
- first matching target への fallback

target absence policy は明示的でなければならない。
必須 target の欠如は fail closed しなければならない。
任意 target の欠如は明示的な command policy に従い、profile で必要とされる場合は diagnostics を emit しなければならない。

---

## Lifecycle 境界

CommandRunner は、LifecyclePlan を通じて lifecycle 参加を持ってよい。

command executor は既定では lifecycle handler ではない。
command execution frame は lifecycle scope ではない。

command を実行しても、動的に lifecycle step を追加してはならない。
command executor を登録しても、lifecycle 参加を enroll してはならない。
lifecycle 風 interface を実装しても、command executor の lifecycle 挙動を enroll してはならない。

command runner に acquire、release、reset、dispose の挙動が必要なら、その参加は 08 の LifecycleContribution と LifecyclePlan step に属する。

---

## Command Module と Category Policy

すべての command type は 1 つの owner module に属さなければならない。

command category の用途:

- editor grouping
- diagnostics
- generation report
- optional module enable / disable
- profile filtering
- command catalog report
- migration status report

category family の例:

- CoreFlow
- ActorRouting
- Transform
- Movement
- Physics
- UI
- Tooltip
- Mesh
- AnimationSprite
- Camera
- SceneFlow
- Save
- Trait
- Map
- Debug

category は dispatch identity ではない。

command module ownership は、削除、移行、diagnostics、profile filtering を支えられる程度に安定していなければならない。

---

## 診断と DebugMap 要件

command diagnostics には次を含めなければならない:

- stable error code
- severity
- CommandTypeId
- 利用可能なら authoring key
- 利用可能なら command debug name
- owner module
- command category
- payload schema id
- executor id
- execution frame id
- 利用可能なら actor reference
- 利用可能なら target reference
- source location
- failure policy
- selected profile
- suggested fix

代表的な command diagnostic code:

- COMMAND_TYPE_MISSING
- COMMAND_EXECUTOR_MISSING
- COMMAND_PAYLOAD_SCHEMA_MISSING
- COMMAND_PAYLOAD_REQUIRED_FIELD_MISSING
- COMMAND_PAYLOAD_TYPE_MISMATCH
- COMMAND_PAYLOAD_UNKNOWN_FIELD
- COMMAND_AUTHORING_KEY_USED_AS_RUNTIME_ID
- COMMAND_EXECUTOR_FACTORY_FAILED
- COMMAND_SERVICE_DEPENDENCY_UNDECLARED
- COMMAND_RUNTIME_QUERY_MISSING
- COMMAND_VALUE_KEY_MISSING
- COMMAND_VALUE_STABLE_KEY_LOOKUP_FORBIDDEN
- COMMAND_ASYNC_UNTRACKED
- COMMAND_DETACHED_POLICY_MISSING
- COMMAND_CANCELLED
- COMMAND_TIMEOUT
- COMMAND_CONTROL_FLOW_INVALID
- COMMAND_LOOP_BOUND_MISSING
- COMMAND_RUNNER_CARDINALITY_FORBIDDEN
- COMMAND_BULK_DI_DISCOVERY_FORBIDDEN

source location を持たない command error は、それ自体が diagnostics degradation である。
ただし、欠落した source location 自体が報告対象の failure である場合は例外である。

---

## 失敗ポリシー

command failure は握りつぶしてはならない。

各 command type は、既定の failure 挙動を定義しなければならない。

説明用モデル:

```csharp
public enum CommandFailureBoundary
{
    FailCommand = 10,
    FailFrame = 20,
    FailSequence = 30,
    FailRunner = 40,
    FailScope = 50,
    ContinueWithError = 60,
}
```

既定の failure 挙動は fail-closed である。
`ContinueWithError` は既定では禁止であり、command type、profile policy、diagnostics policy が明示的に許可した場合にのみ認められる。

sequence 風 command は、child failure が次のどれを行うかを定義しなければならない:

- sequence を止める
- rollback する
- child をスキップする
- error を伴って継続する
- parent frame に伝播する
- runner を失敗させる
- scope を失敗させる

executor exception は structured command diagnostics に変換しなければならない。
exception は通常の control flow として使ってはならない。

---

## 性能とメモリのポリシー

command dispatch は runtime の hot path である。

目標要件:

- CommandTypeId lookup は O(1) または小さな定数時間であるべき
- executor lookup は全 executor を走査してはならない
- 通常 dispatch は managed allocation を避けるべき
- payload validation は可能な限り事前計算されるべき
- ServiceGraph は boot 時に全 executor を構築してはならない
- command frame allocation は高頻度 path では pool 化または bounded にすべき
- command-local state は、明示的に必要でない限り dictionary を割り当ててはならない
- authoring-key lookup は通常 runtime dispatch 中に起きてはならない
- catalog metadata size は計測可能でなければならない
- command execution は 14 で定義される profiler marker を公開しなければならない

command type 数が増えると catalog metadata size は増えてよい。
しかし、boot 時に全 executor を eager instantiate させてはならない。

entity 数が増えても command runner 数が自動的に増えてはならない。
command 数が増えても lifecycle step 数が自動的に増えてはならない。

---

## レガシー移行ポリシー

レガシー command 移行は、runtime discovery を明示的な command contribution と生成済み catalog metadata に置き換えなければならない。

| レガシーパターン | 対象表現 |
|---|---|
| `builder.Register<XExecutor>().As<ICommandExecutor>()` | CommandContribution + CommandCatalogPlan entry |
| `ICommandExecutor` の bulk list | CommandTypeId から executor への mapping |
| `CommandExecutorRegistry(IReadOnlyList<ICommandExecutor>)` | 生成済み executor table |
| `CommandKeyResolver` の runtime lookup | runtime 前に authoring key を CommandTypeId に正規化 |
| runtime-only の negative command key ID | 対象 kernel 正確性の観点では無効 |
| `CommandCatalogService` の runtime catalog builder | 検証済み CommandCatalogPlan |
| `CommandCatalogLocator` の `Resources.Load` fallback | boot-time の検証済み artifact reference |
| runner registration の scope-kind switch | CommandExecutionDomain + 明示的 runner policy |
| CommandRunner 上の `.As<IScopeAcquireHandler>()` | CommandRunner を target にした LifecycleContribution |
| debug viewer build callback | DiagnosticsContribution / DebugMap binding |

レガシー移行は runtime fallback を意味しない。

レガシー adapter は、13 で定義された互換境界の内部でのみ許可される。
新しい CommandCatalog core は、legacy RuntimeResolver、legacy CommandRunnerMB registration、legacy catalog locator fallback、legacy command key runtime fallback に依存してはならない。

---

## 禁止パターン

対象 CommandCatalog runtime で禁止されるもの:

- bulk DI registration を command discovery として使うこと
- `IReadOnlyList<ICommandExecutor>` を解決すること
- 任意の string による executor lookup
- authoring key を runtime dispatch identity として使うこと
- stable key を runtime dispatch identity として使うこと
- command 欠落時に no-op executor へ fallback すること
- payload schema 欠落時の fallback
- payload object に対する runtime reflection を schema として扱うこと
- executor 内部での runtime scene search
- executor 内部での runtime Transform hierarchy search
- ServiceGraph を runtime target registry として使うこと
- CommandCatalog を lifecycle registry として使うこと
- interface scan による command executor lifecycle enrollment
- 既定での全 executor 早期構築
- command frame を任意 object dictionary として扱うこと
- explicit detached policy なしで fire-and-forget 実行すること
- command failure の握りつぶし
- command-local state を global Blackboard として扱うこと
- runtime-only command key repair
- 必須 command catalog input に対する `Resources.Load` fallback
- 対象 runtime catalog source としての editor asset search
- 新しい command ごとに編集する巨大な runtime installer

---

## テストケースモデル

各 CommandCatalog テストケースは次を定義しなければならない:

- Test ID
- Title
- CommandCatalogPlan fixture
- command payload fixture
- CommandExecutionContext fixture
- selected profile
- Operation
- Expected result
- Expected diagnostics
- 必要に応じた expected allocation または performance assertion
- Notes

例:

```text
Test ID: TC_CMD_ID_001_DispatchByCommandTypeId
Input:
- CommandCatalogPlan に CommandTypeId CameraShake がある
- Executor mapping が存在する

Operation:
- CommandTypeId で CameraShake を dispatch する

Expected:
- 正しい executor が呼ばれる
- authoring-key lookup は発生しない
```

---

## 必須テストケース

### A. Identity テスト

#### TC_CMD_ID_001_DispatchByCommandTypeId

入力:

- CommandCatalogPlan に CommandTypeId CameraShake がある
- Executor mapping が存在する

操作:

- CommandTypeId で CameraShake を dispatch する

期待結果:

- 正しい executor が呼ばれる

#### TC_CMD_ID_002_AuthoringKeyRuntimeDispatchRejected

入力:

- dispatch 要求が raw string `camera.shake` を使っている

期待結果:

- failed
- `COMMAND_AUTHORING_KEY_USED_AS_RUNTIME_ID`

#### TC_CMD_ID_003_UnknownCommandTypeRejected

入力:

- CommandTypeId 9999 が catalog に存在しない

期待結果:

- failed
- `COMMAND_TYPE_MISSING`

### B. Payload テスト

#### TC_CMD_PAYLOAD_001_ValidPayloadAccepted

入力:

- command が float の duration を必要とする
- payload に float の duration が含まれている

期待結果:

- passed

#### TC_CMD_PAYLOAD_002_RequiredFieldMissing

入力:

- command が target を必要とする
- payload に target がない

期待結果:

- failed
- `COMMAND_PAYLOAD_REQUIRED_FIELD_MISSING`

#### TC_CMD_PAYLOAD_003_TypeMismatchRejected

入力:

- duration field の schema は float
- payload の duration は string

期待結果:

- failed
- `COMMAND_PAYLOAD_TYPE_MISMATCH`

#### TC_CMD_PAYLOAD_004_UnknownFieldRejectedByDefault

入力:

- payload に未知の field が含まれている

期待結果:

- failed
- `COMMAND_PAYLOAD_UNKNOWN_FIELD`

### C. Executor テスト

#### TC_CMD_EXEC_001_LazyExecutorCreatedOnFirstUse

入力:

- executor policy は LazySingleton

操作:

- catalog を boot する
- command は実行しない

期待結果:

- boot 時に executor は構築されない

操作:

- command を実行する

期待結果:

- executor は 1 回だけ構築される

#### TC_CMD_EXEC_002_AllExecutorsNotEagerlyConstructed

入力:

- catalog に 500 個の command type がある

操作:

- boot する

期待結果:

- すべての executor の eager construction は発生しない

#### TC_CMD_EXEC_003_MissingExecutorRejected

入力:

- CommandTypeId は存在する
- Executor reference がない

期待結果:

- failed
- `COMMAND_EXECUTOR_MISSING`

### D. Service / Value / Query 境界テスト

#### TC_CMD_BOUNDARY_001_ExecutorServiceDependencyDeclared

入力:

- executor が TimeDomainService を必要とする
- その依存は宣言されており、service も存在する

期待結果:

- passed

#### TC_CMD_BOUNDARY_002_UndeclaredServiceDependencyRejected

入力:

- executor が CommandContribution で宣言されていない service にアクセスしようとする

期待結果:

- failed
- `COMMAND_SERVICE_DEPENDENCY_UNDECLARED`

#### TC_CMD_BOUNDARY_003_ValueStableKeyLookupRejected

入力:

- command が runtime で stable string key によって value を access しようとする

期待結果:

- failed
- `COMMAND_VALUE_STABLE_KEY_LOOKUP_FORBIDDEN`

#### TC_CMD_BOUNDARY_004_RuntimeQueryMissingRejected

入力:

- WithActor command が actor query を必要とする
- RuntimeQuery が宣言されていない

期待結果:

- failed
- `COMMAND_RUNTIME_QUERY_MISSING`

### E. Control Flow テスト

#### TC_CMD_FLOW_001_SequenceStopsOnFailureByPolicy

入力:

- Sequence に 3 つの command がある
- command 2 が失敗する
- policy は StopOnFailure

期待結果:

- command 3 は実行されない
- failure は伝播する

#### TC_CMD_FLOW_002_IfBranchUsesDeclaredChildCommands

入力:

- If command が then block と else block を参照している

期待結果:

- 選択された branch だけが実行される
- child command ID は有効である

#### TC_CMD_FLOW_003_ForLoopRequiresBound

入力:

- For command に max iteration がなく、明示的な unbounded policy もない

期待結果:

- failed
- `COMMAND_LOOP_BOUND_MISSING`

### F. Async テスト

#### TC_CMD_ASYNC_001_WaitCommandCompletes

入力:

- duration 付きの Wait command

期待結果:

- frame は生存し続ける
- completion により sequence が再開する

#### TC_CMD_ASYNC_002_CancellationStopsFrame

入力:

- async command を実行中である
- cancellation が要求される

期待結果:

- `COMMAND_CANCELLED`
- frame の failure policy が適用される

#### TC_CMD_ASYNC_003_FireAndForgetRequiresDetachedPolicy

入力:

- detached policy のない Forget command

期待結果:

- failed
- `COMMAND_DETACHED_POLICY_MISSING`

### G. Runner Domain テスト

#### TC_CMD_RUNNER_001_ProjectRunnerUsesProjectDomain

入力:

- Project domain runner
- Project command の実行

期待結果:

- passed

#### TC_CMD_RUNNER_002_EntityRunnerRejectedByDefaultForMassEntities

入力:

- 10,000 entities に対して entity ごとの runner がある

期待結果:

- failed
- `COMMAND_RUNNER_CARDINALITY_FORBIDDEN`

#### TC_CMD_RUNNER_003_ExplicitEntityAggregateRunnerAllowed

入力:

- 作成済み boss aggregate root に明示的な runner policy がある

期待結果:

- budget に応じて Passed または Warning

### H. 移行テスト

#### TC_CMD_MIGRATION_001_CommandRunnerMBBulkExecutorRegistrationRejected

入力:

- module が builder の `.As<ICommandExecutor>()` を通じて executor を登録しようとする

期待結果:

- failed
- `COMMAND_BULK_DI_DISCOVERY_FORBIDDEN`

#### TC_CMD_MIGRATION_002_CommandRunnerLifecycleSeparated

入力:

- CommandRunner に acquire / release が必要である

期待結果:

- LifecycleContribution が作成される
- ServiceGraph は handler interface から lifecycle を推論しない

#### TC_CMD_MIGRATION_003_DebugViewerBuildCallbackReplaced

入力:

- build callback が command debug viewer を bind している

期待結果:

- DiagnosticsContribution または DebugMap binding が必要である
- runtime build callback は truth source として許可されない

### I. Performance テスト

#### TC_CMD_PERF_001_CommandLookupNoScan

入力:

- 1000 個の command type がある

操作:

- 1 つの command を dispatch する

期待結果:

- 全 command type を走査しない

#### TC_CMD_PERF_002_NormalDispatchNoAllocation

操作:

- 簡単な command を繰り返し dispatch する

期待結果:

- 通常 path で managed allocation はない

#### TC_CMD_PERF_003_CommandCountDoesNotEagerInstantiateExecutors

入力:

- 多数の command type がある

操作:

- catalog を boot する

期待結果:

- metadata は読み込まれる
- executor はすべて instantiate されない

---

## 受け入れ基準

この仕様は、次を定義するときに完了である:

- CommandCatalog の runtime 責務
- CommandCatalogPlan の入力契約
- CommandTypeId の識別モデル
- authoring key の境界
- CommandContribution の projection 境界
- payload schema モデル
- payload validation policy
- command executor モデル
- executor factory と lifetime policy
- CommandRunner の責務
- CommandFrame と CommandContext モデル
- CommandLocal state policy
- control-flow command policy
- async / wait / cancellation / detached execution policy
- ServiceGraph 境界
- ValueStore 境界
- RuntimeQuery 境界
- scope / entity / actor target 境界
- LifecyclePlan 境界
- command module と category policy
- diagnostics と DebugMap 要件
- failure policy
- performance と memory policy
- legacy migration policy
- forbidden patterns
- CommandCatalog のテストケースモデル
- 必須 CommandCatalog テストケース

---

## テストケース

| テストケース | 目的 | 期待結果 |
|---|---|---|
| TC-09-01 | コマンドの dispatch が authoring string ではなく CommandTypeId によることを確認する。 | raw string での dispatch は拒否され、CommandTypeId での dispatch は成功する。 |
| TC-09-02 | executor が ServiceGraph や `IReadOnlyList<ICommandExecutor>` から発見されないことを確認する。 | bulk DI discovery は validation で失敗する。 |
| TC-09-03 | payload schema validation が executor 呼び出しの前に実行されることを確認する。 | 欠落、未知、型不一致の field は実行前に失敗する。 |
| TC-09-04 | ServiceGraph、ValueStore、RuntimeQuery、ScopeGraph、LifecyclePlan との境界を確認する。 | 宣言されていない依存や target search は拒否される。 |
| TC-09-05 | async、wait、cancellation、detached execution policy を確認する。 | 追跡されない async と detached policy 欠落は diagnostics 付きで失敗する。 |
| TC-09-06 | performance と boot behavior を確認する。 | catalog boot ではすべての executor を構築せず、dispatch では全 command を走査しない。 |

---

## 最終見解

CommandCatalog は、検証済みの command identity、payload schema、executor reference、diagnostics metadata のための runtime command table である。
DI executor list でも、string-key resolver でも、lifecycle registry でもない。

runtime command execution は、検証済み CommandTypeId と検証済み payload schema からのみ進められる。

```text
CommandContribution
  -> CommandIR
  -> CommandCatalogPlan
  -> CommandCatalog
  -> CommandTypeId lookup
  -> executor execution
```

巨大な runtime installer を編集してコマンドを追加する時代は終わらせなければならない。

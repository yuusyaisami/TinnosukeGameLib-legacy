# DebugMap と Diagnostics 仕様

## 文書ステータス

- 文書 ID: 11_DebugMapAndDiagnosticsSpec
- ステータス: Draft
- 役割: Kernel v2 における統一された DebugMap 契約、structured diagnostics モデル、中央 diagnostics パイプライン、および Unity logging sink ポリシーを定義する
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
  - [10_ValueSchemaAndStoreSpec.md](10_ValueSchemaAndStoreSpec.md)
  - [10_2_DynamicValueEvaluationSpec.md](10_2_DynamicValueEvaluationSpec.md)
- 基盤を提供するもの:
  - 12_UnityAuthoringBridgeSpec.md
  - 13_LegacyCompatBoundarySpec.md
  - 14_PerformanceBudgetAndRuntimeRulesSpec.md
  - 15_TestAndValidationSpec.md

### 改訂メモ

この改訂では、11 を単なる DebugMap lookup の注記ではなく、DebugMap と diagnostics を 1 つの統一された runtime contract として定義する。

また、subsystem は structured diagnostics を生成し、Unity logging は中央 diagnostic sink からのみ emit される、というアーキテクチャルールを明確にする。

### 所有範囲

この仕様が所有するもの:

- DebugMap の論理 runtime contract
- SourceLocation と diagnostics provenance model
- KernelDiagnostic record model
- diagnostic code のガバナンスと stable identity ルール
- diagnostic severity、domain、category、failure-boundary model
- diagnostic context、runtime identity、artifact identity、exception payload model
- central diagnostic service contract
- diagnostic processor と sink contract
- `UnityLogDiagnosticSink` policy
- profile-based diagnostics emission policy
- diagnostics degradation ルール
- diagnostics de-duplication、throttling、aggregation policy
- cross-subsystem diagnostics integration contract
- diagnostics 関連の forbidden pattern
- diagnostics test model と acceptance criteria

この仕様が所有しないもの:

- ServiceGraph の runtime semantics
- ScopeGraph の runtime semantics
- Lifecycle ordering の意味論
- command execution の意味論
- value storage の layout または save format
- runtime query の意味論
- editor window UI の詳細
- crash reporting backend の実装
- Roslyn analyzer の実装詳細
- 03 が所有する generation algorithm
- 04 が所有する validation algorithm
- 05 が所有する boot acceptance policy

11 は共有 diagnostics substrate を定義する。
06、07、08、09、10、10-2 がそれぞれ所有している domain-specific failure behavior と minimum provenance field を奪わない。

03 は引き続き DebugMap generation を所有する。
04 は引き続き validation semantics を所有する。
05 は引き続き boot acceptance と boot failure boundary を所有する。

---

## 目的

この仕様は、DebugMap と diagnostics に対する target-kernel contract を定義する。

中心的な記述:

```text
DebugMap は、検証済み runtime identity を人が読める source information に解決する。

Diagnostics は、Kernel v2 における統一された structured error reporting pipeline である。

Subsystem は Unity に直接 log しない。
Subsystem は structured KernelDiagnostic record を emit する。
中央の Unity diagnostic sink だけが `Debug.Log` / `Debug.LogWarning` / `Debug.LogError` / `Debug.LogException` を呼べる。
```

この仕様は、次の退化を防ぐために存在する:

- runtime failure が formatted string だけで表現される
- shared diagnostics policy を迂回する subsystem-specific Unity logging path
- source に遡れない numeric ID failure
- 必要な failure information を黙って隠す profile-dependent diagnostics behavior
- subsystem ごとに複製された logging infrastructure
- diagnostics routing と failure policy を迂回する exception output path

これは ID を読みやすくするためだけの仕様ではない。
plan-first kernel を observable、testable、fail-closed に保つ error substrate である。

---

## スコープ

この仕様が定義するもの:

- DebugMap の目的と contract boundary
- runtime-facing identity に対する DebugMap coverage 要件
- SourceLocation contract と provenance rule
- diagnostics の runtime identity mapping と artifact identity mapping
- KernelDiagnostic record model
- DiagnosticCode のガバナンス
- DiagnosticSeverity、DiagnosticDomain、DiagnosticCategory、DiagnosticFailureBoundary model
- diagnostic context、payload、exception capture policy
- `KernelDiagnosticService` contract
- diagnostic processor と sink contract
- `UnityLogDiagnosticSink` の挙動と host-output 分離
- profile-based diagnostics policy
- diagnostics degradation rule
- de-duplication、throttling、aggregation rule
- diagnostics hot path に対する performance rule
- Boot / Generation / Validation / ServiceGraph / ScopeGraph / Lifecycle / Command / Value / RuntimeQuery / Save に対する subsystem integration rule
- 現在の logging debt に対する legacy migration guidance
- diagnostics test model と acceptance criteria

---

## 対象外

この仕様が定義しないもの:

- DebugMap asset の最終 binary serialization container
- 最終 editor console / diagnostics window UI layout
- 最終 remote crash-report schema
- 最終 save-system architecture
- 各 subsystem に対する最終 runtime code API signature
- command payload schema の詳細
- scope handle layout
- service factory layout
- diagnostics 固有要件を超える profiler marker taxonomy

この仕様は diagnostics を generic text logging guideline に変えてはならない。
ここで定義するのは runtime contract と structured reporting の要件であり、console output の見た目の好みではない。

---

## 他仕様との関係

| 仕様 | 関係 |
|---|---|
| [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md) | DebugMap-backed diagnostics と no-silent-fallback を根本制約として定義する。 |
| [01_KernelIRSpec.md](01_KernelIRSpec.md) | diagnostics がたどるべき identity domain と normalized source structure を定義する。 |
| [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md) | DebugMap generation と diagnostics が消費する DiagnosticsContribution provenance input を提供する。 |
| [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md) | DebugMap generation と artifact consistency を所有する。11 は runtime-facing DebugMap contract と runtime で消費される diagnostics record shape を定義する。 |
| [04_DependencyValidationSpec.md](04_DependencyValidationSpec.md) | validation semantics と validation failure の意味を所有する。11 はそれを報告するための互換 diagnostics substrate を定義する。 |
| [05_BootManifestAndProfileSpec.md](05_BootManifestAndProfileSpec.md) | boot acceptance と boot failure boundary を所有する。11 は boot reporting で使う shared diagnostics contract と central sink rule を定義する。 |
| [06_ServiceGraphRuntimeSpec.md](06_ServiceGraphRuntimeSpec.md) | 06 は required service failure provenance と behavior を定義する。11 はそれらの failure を emit するための shared record、routing、DebugMap、sink contract を定義する。 |
| [07_ScopeGraphRuntimeSpec.md](07_ScopeGraphRuntimeSpec.md) | 07 は scope failure provenance と behavior を定義する。11 はそれらを emit する shared diagnostics substrate と DebugMap runtime contract を定義する。 |
| [08_LifecyclePlanSpec.md](08_LifecyclePlanSpec.md) | 08 は lifecycle provenance fields と failure behavior を定義する。11 は shared diagnostics substrate と central logging policy を定義する。 |
| [09_CommandCatalogRuntimeSpec.md](09_CommandCatalogRuntimeSpec.md) | 09 は command-local diagnostics 要件と failure behavior を定義する。11 はそれらを emit する shared diagnostic record、sink routing、Unity output policy を定義する。 |
| [10_ValueSchemaAndStoreSpec.md](10_ValueSchemaAndStoreSpec.md) | 10 は value-state provenance、access、failure behavior を定義する。11 は value failure を emit する shared diagnostics と DebugMap contract を定義する。 |
| [10_2_DynamicValueEvaluationSpec.md](10_2_DynamicValueEvaluationSpec.md) | 10-2 は evaluation-specific provenance、cache / tracker degradation の意味、failure behavior を定義する。11 はそれらを emit する shared record、routing、Unity output policy を定義する。 |
| 12_UnityAuthoringBridgeSpec.md | editor-facing authoring diagnostics のための DebugMap source mapping と diagnostics contract を消費する。 |
| 13_LegacyCompatBoundarySpec.md | legacy error を 11 の diagnostics pipeline に forward する bounded adapter を定義する。 |
| 14_PerformanceBudgetAndRuntimeRulesSpec.md | ここで定義される規則を使って diagnostics emission と formatting cost を予算化する。 |
| 15_TestAndValidationSpec.md | ここで定義する diagnostics contract を使って executable diagnostics snapshot、analyzer gate、CI coverage を実装する。`KernelDiagnostic`、`DiagnosticCode`、severity、sink ownership は再定義しない。 |

11 は共有 diagnostics substrate である。
domain semantics を所有し直してはならない。

---

## asmdef とコンパイル境界の期待値

diagnostics は複数 assembly に意図的に分割される:

- `GameLib.Kernel.Diagnostics`
- `GameLib.Kernel.Diagnostics.Unity`
- `GameLib.Kernel.Diagnostics.Editor`

詳細な依存行列は [17_AssemblyDefinitionAndCompileBoundarySpec.md](17_AssemblyDefinitionAndCompileBoundarySpec.md) が所有する。

11 に必要なコンパイル境界ルール:

- `GameLib.Kernel.Diagnostics` は structured record model、diagnostic service contract、non-Unity sink を所有し、Unity-free のままであるべきである
- `GameLib.Kernel.Diagnostics.Unity` は、target kernel path で直接 Unity `Debug.Log*` を emit できる唯一の合法 assembly である
- editor-facing diagnostics browsing、source navigation、authoring tooling は `GameLib.Kernel.Diagnostics.Editor` に置かなければならない
- subsystem-specific final logger、legacy direct log wrapper、feature-specific log sink を diagnostics core に入れてはならない

structured diagnostics が core で Unity logging API なしに、または feature-specific formatting code なしにコンパイルできないなら、11 の境界は破られている。

---

## 現在の diagnostics 負債の観測

この節は、現行コードベースにある diagnostics 負債の観測結果をまとめる。
ここは target policy ではなく、移行元の整理である。

### 観測の追跡可能性

| 観測 | 証拠種別 | 想定される下流 |
|---|---|---|
| `LTSLog` は Unity Debug API の薄い runtime-controllable wrapper である。 | ソース | 11, 13 |
| Command VNext は rich な command error を整形するが、まだ Unity Debug に直接 emit している。 | ソース | 09, 11 |
| `CommandExecutorRegistry` は invalid / duplicate ID を直接 `Debug.LogError` で報告している。 | ソース | 09, 11 |
| Save は別の logger abstraction を持つが、`UnitySaveLogger` は依然として Unity Debug API を直接呼んでいる。 | ソース | 10, 11 |
| Dynamic runtime logging utilities は structured context を既に持つが、最終的な host formatting と emission は producer 側が決めている。 | ソース | 10, 11 |
| Monitor と command runtime には、hot / semi-hot path の中に多くの inline `Debug.Log*` と `Debug.LogException` がある。 | ソース | 09, 11, 14 |
| diagnostics identity が stable diagnostic code ではなく、message text に暗黙化されがちである。 | ソース | 11, 15 |
| exception output と failure boundary routing が subsystem 間で統一されていない。 | ソース | 11, 15 |

### 代表的な参照先

- [LTSLog.cs](../../GameLib/Script/Common/LTS/LTSLog.cs) - Unity `Debug.Log`、`Debug.LogWarning`、`Debug.LogError` の薄い wrapper
- [UnityCommandResolveLogger.cs](../../GameLib/Script/Common/Commands/VNext/Core/UnityCommandResolveLogger.cs) - command 専用の rich formatting と direct Unity output
- [CommandExecutorRegistry.cs](../../GameLib/Script/Common/Commands/VNext/Core/CommandExecutorRegistry.cs) - invalid / duplicate command ID を direct Unity error output で報告
- [ISaveLogger.cs](../../GameLib/Script/Common/Variables/Save/Core/ISaveLogger.cs) - 統一された kernel diagnostics の外にある save-specific logger abstraction
- [UnitySaveLogger.cs](../../GameLib/Script/Common/Variables/Save/Unity/UnitySaveLogger.cs) - Unity Debug API に直接出力する save logger
- [DynamicRuntimeLogUtility.cs](../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicRuntimeLogUtility.cs) - sink-specific rendering の後ろに移すべき structured context formatting helper
- [ExpressionRuntimeLogger.cs](../../GameLib/Script/Common/Variables/Dynamic/Expression/ExpressionRuntimeLogger.cs) - structured context はあるが、最終出力は依然として direct `Debug.LogError`
- [MonitorChannelRuntime.cs](../../GameLib/Script/Common/Commands/Core/MonitorChannelRuntime.cs) - runtime rule execution の中に散在する direct warning / error / exception log

### 現在のギャップ

現在の project には 11 が塞ぐべき diagnostics gap がある:

- subsystem-specific logger が structured record ではなく最終 host output を決めている
- 同じ runtime failure family が subsystem ごとに異なる message format で表現されている
- message text が identity として使われがちである
- exception output が shared routing を迂回している
- profile-aware diagnostics policy が断片化している
- direct Unity logging が runtime behavior path と registry validation path の中に入っている
- structured context は存在する箇所もあるが、shared record model と sink model がない
- diagnostics の de-duplication と throttling が ad hoc か、存在しない

---

## 根本的な diagnostics 問題

現在の diagnostics debt は、単に utility class が 1 つ足りないという話ではない。
構造的な architecture problem である。

根本問題は次の通りである:

1. sink ownership が分散している
2. message string が identity として扱われている
3. source provenance が不整合である
4. exception が data ではなく output として扱われている
5. diagnostics の cost contract が統一されていない

### 1. 分散した Sink Ownership

異なる subsystem が、それぞれ独自の最終 Unity output path を所有している。

例:

- `LTSLog` は Unity へ直接 forward する
- command-specific logger は Unity へ直接 emit する
- save-specific logger は Unity へ直接 emit する
- runtime command と monitor path は inline で `Debug.Log*` を呼ぶ

これでは、profile policy、de-duplication、testing、migration control を統一できない。

### 2. Message-Text Identity

現在の多くの error は、stable diagnostic code ではなく、formatted string、色、section header で識別されている。

これにより test は壊れやすくなり、文言が変わっただけで deterministic な diagnostics behavior を失う。

### 3. 不整合な Provenance

ある path は source や runtime context を capture するが、別の path は text しか emit しない。

target kernel は runtime identity から human-readable source information への stable mapping を必要とする。
現在の diagnostics はそれを一貫して提供していない。

### 4. Exception-as-Output

exception は side effect として Unity へ直接 log されがちである。

これでは shared routing、shared profile policy、failure-boundary handling、deterministic test capture を迂回する。

### 5. Diagnostics Cost Contract の欠如

現在の diagnostics formatting と output path は、shared hot-path policy によって統治されていない。

そのため、重い string construction や spammy output が runtime execution path に漏れやすい。

---

## DebugMap Definition

DebugMap は、検証済み runtime identity を human-readable な source information に map する生成 artifact である。

DebugMap が使われる場面:

- runtime diagnostics
- editor navigation
- validation report
- generation report
- boot failure reporting
- test snapshot
- migration report

DebugMap は runtime truth ではない。
DebugMap は、検証済み runtime identity に付随する provenance metadata である。

### Required DebugMap Role

```text
Runtime は、実行に verified identity を使う。
DebugMap は、その identity を人間向けに解決する。
```

DebugMap を次の用途に使ってはならない:

- debug name による runtime service lookup
- authoring key による runtime command lookup
- stable key による runtime value lookup
- provenance 欠落の boot-time reconstruction
- 必須 runtime input の欠落に対する fallback repair

### DebugMap Logical Model

説明用スケッチ:

```csharp
public sealed class KernelDebugMap
{
    public ArtifactSetId ArtifactSetId;
    public int FormatVersion;
    public Hash128 SourceHash;
    public Hash128 DebugMapHash;
    public DebugMapEntry[] Entries;
    public SourceLocationEntry[] SourceLocations;
    public ArtifactIdentityEntry[] Artifacts;
}
```

このスケッチは説明用である。
03 が generation mechanics と publication を所有する。
11 は runtime contract と必須フィールドを所有する。

---

## Diagnostic Identity and Coverage Model

runtime-facing identity は、DebugMap を通じて debug 可能でなければならない。

少なくとも次を coverage に含める:

- `ModuleId`
- `ServiceId`
- `ScopeAuthoringId`
- `ScopePlanId`
- lifecycle plan が runtime-visible である場合の `LifecyclePlanId`
- `LifecycleStepId`
- `CommandTypeId`
- executor identity が runtime-visible である場合の `CommandExecutorId`
- `CommandPayloadSchemaId`
- `ValueKeyId`
- `ValueSchemaId`
- `RuntimeQueryId`
- `ArtifactSetId`
- diagnostics が参照する generated artifact identity

`ScopeHandle` は一部だけ異なる。
DebugMap は、handle の背後にある検証済みの scope authoring identity と plan identity を解決する。
live handle generation と instance state は static DebugMap ではなく runtime state が供給する。

### Minimum DebugMap Entry

各 DebugMap entry には、1 つの検証済み runtime identity を source に遡れるだけの情報が必要である。

最小フィールド:

- numeric または symbolic な runtime identity
- identity kind
- stable debug name
- owner module
- source location reference
- profile availability
- artifact identity または artifact hash
- 該当する場合は legacy origin

推奨される追加フィールド:

- authoring label
- category または subtype
- generated projection kind
- 関連 identity reference

### Coverage Rules

必要なルール:

- 必要 coverage の欠落は、03 と 04 がそれを要求する場合、generation または validation failure である
- runtime diagnostics は coverage 欠落を diagnostics degradation として扱う
- Development と Test profile は diagnostics degradation を error として扱う
- Release profile は、00 と 05 が許す最小限の範囲で、failure が安定・解釈可能・source-resolvable である場合に限り metadata を削減してよい

DebugMap coverage は deterministic でなければならない。
coverage は host order、runtime discovery、editor display state に依存してはならない。

---

## SourceLocation Model

SourceLocation は、authoring、生成、移行済みデータの human-traceable な origin を表す。

説明用スケッチ:

```csharp
public readonly struct SourceLocationId
{
    public readonly int Value;
}

public sealed class SourceLocationEntry
{
    public SourceLocationId Id;
    public SourceLocationKind Kind;
    public string AssetPath;
    public string AssetGuid;
    public long LocalObjectId;
    public string ComponentType;
    public string PropertyPath;
    public string GeneratedSource;
    public string DisplayLabel;
}
```

必要な SourceLocation capabilities:

- scene、prefab、ScriptableObject、generated artifact、generated source location への追跡
- human debugging に十分な authoring context の特定
- diagnostics snapshot と migration report のために十分安定していること

SourceLocation は少なくとも次の origin kind をサポートしなければならない:

- scene object
- prefab object
- prefab variant object
- ScriptableObject asset
- generated code location
- generated asset location
- migration-produced legacy origin

SourceLocation は runtime discovery を許可しない。
それは diagnostics と traceability のためだけに存在する。

---

## Runtime Identity Mapping

diagnostics には複数 domain の runtime identity が含まれ得る。

説明用スケッチ:

```csharp
public enum RuntimeIdentityKind
{
    None = 0,
    Module = 10,
    Service = 20,
    ScopeAuthoring = 30,
    ScopePlan = 40,
    ScopeHandle = 50,
    LifecyclePlan = 60,
    LifecycleStep = 70,
    CommandType = 80,
    CommandExecutor = 90,
    CommandPayloadSchema = 100,
    ValueKey = 110,
    ValueSchema = 120,
    RuntimeQuery = 130,
    ArtifactSet = 140,
    GeneratedArtifact = 150,
}

public readonly struct RuntimeIdentityRef
{
    public readonly RuntimeIdentityKind Kind;
    public readonly int Value;
    public readonly int Generation;
}
```

ルール:

- diagnostics が持つすべての runtime identity には明示的な kind が必要である
- identity が generation-sensitive、たとえば handle-like である場合は generation が必要である
- diagnostics は曖昧な裸の整数に依存してはならない

例:

- `ServiceId 100`
- `CommandTypeId 250`
- `ValueKeyId 300`
- `ScopeHandle index=10 generation=2`
- `LifecycleStepId 90`

---

## Artifact Identity Mapping

generation、validation、boot compatibility、stale artifact handling に関わる failure では、diagnostics が artifact-set identity と generated-artifact identity を参照できなければならない。

説明用スケッチ:

```csharp
public readonly struct ArtifactSetId
{
    public readonly int Value;
}

public readonly struct GeneratedArtifactId
{
    public readonly int Value;
}
```

artifact identity mapping は次をサポートしなければならない:

- artifact set compatibility diagnostics
- stale artifact diagnostics
- generation report correlation
- boot manifest rejection diagnostics
- test snapshot traceability

artifact identity は file path だけで置き換えてはならない。
stable artifact identity と compatibility hash は残し続けなければならない。

---

## Diagnostic Pipeline Definition

diagnostics は中央 pipeline を通る。

```text
Producer
  -> KernelDiagnosticBuilder or typed diagnostic adapter
  -> KernelDiagnosticService
  -> DiagnosticProcessor pipeline
  -> DiagnosticSink(s)
  -> Unity / File / Editor / Test / Remote outputs
```

代表的な producer:

- boot
- verified plan generation
- dependency validation
- service graph runtime
- scope graph runtime
- lifecycle dispatcher
- command runner と command catalog
- value store と value init
- runtime query
- save system
- legacy compatibility adapter

### Producer Rules

producer は次を行ってよい:

- failure や warning を検出する
- typed diagnostic payload を作る
- runtime identity、source、artifact context を付与する
- bounded local buffer が必要なときは flush 前に diagnostics を batch する

producer は次を行ってはならない:

- target-kernel path で Unity Debug API を直接呼ぶこと
- 最終的な Unity console formatting を決めること
- failure を formatted message string だけで表現すること
- exception が failure behavior に関係するのに、diagnostic を報告せずに exception を握りつぶすこと

### Local Buffer Rule

以下の条件を満たす場合、approved な local diagnostic buffer を使ってよい:

- batch reporting が overhead を実質的に下げる
- buffering が failure の意味を変えない
- consumer にとって order が deterministic enough である
- buffer が `KernelDiagnosticService` に flush する

local buffering は parallel diagnostics architecture を作ってはならない。

---

## Central Logging Rule

Unity Debug API は、approved diagnostic sink の中でのみ使用できる。

target kernel で唯一の Unity-facing sink は次である:

- `UnityLogDiagnosticSink`

承認された sink 以外で禁止されるもの:

- `Debug.Log`
- `Debug.LogWarning`
- `Debug.LogError`
- `Debug.LogException`
- final output path としての subsystem-specific Unity logger
- final output path としての `LTSLog.LogError`

これは formatting preference ではなく、根本的な architecture rule である。

```text
Errors are produced where they occur.
Unity logging is emitted only by the central diagnostic sink.
```

target-kernel architecture で subsystem が直接 `Debug.LogError` を呼ぶなら、diagnostics architecture は退化している。

temporary legacy adapter があるとしても、それは 13 で分離され、明示的、計測可能、かつ一時的でなければならない。

---

## Diagnostic Code Governance

DiagnosticCode は diagnostic family の stable identity である。

message text は identity ではない。

説明用スケッチ:

```csharp
public readonly struct DiagnosticCode
{
    public readonly int Value;
}
```

このスケッチは、最終実装が raw integer を public に公開しなければならないことを意味しない。
architecture の要件は stable identity であり、唯一の具体的な public type ではない。

### Diagnostic Code Rules

すべての diagnostic code には次が必要である:

- specification と testing のための stable symbolic identity
- 1 つの owning domain
- 1 つの意味
- message wording が変わっても stable な failure meaning

runtime efficiency のために optional な generated numeric representation を使ってよい。ただし次の条件が必要である:

- symbolic identity が documentation と test で stable であること
- numeric mapping が deterministic であること
- 必要なときに DebugMap または関連 diagnostics table で mapping を説明できること

### Ownership Split for Codes

11 が所有するもの:

- diagnostic identity の shared model
- naming と allocation ルール
- diagnostics degradation や sink violation のような reserved shared diagnostics family

04、06、07、08、09、10 のような owner spec が所有するもの:

- domain-specific code family
- それら failure の意味
- それら failure に必要な minimum provenance field

11 は、すべての subsystem rule を 1 つの list に集約して domain ownership を消してはならない。

### Reserved Shared Diagnostics Families

代表的な shared diagnostics family:

- `DIAG_CODE_MISSING`
- `DIAG_DOMAIN_MISSING`
- `DIAG_FAILURE_BOUNDARY_MISSING`
- `DIAG_SOURCE_LOCATION_MISSING`
- `DIAG_DEBUGMAP_ENTRY_MISSING`
- `DIAG_DEBUGMAP_HASH_MISMATCH`
- `DIAG_DIRECT_UNITY_LOG_FORBIDDEN`
- `DIAG_EXCEPTION_SWALLOWED`
- `DIAG_MESSAGE_ONLY_RECORD_FORBIDDEN`
- `DIAG_DEDUPLICATION_CONFIGURATION_INVALID`

---

## Diagnostic Severity Model

diagnostic severity は failure boundary と独立である。

説明用モデル:

```csharp
public enum DiagnosticSeverity
{
    Trace = 10,
    Info = 20,
    Warning = 30,
    Error = 40,
    Fatal = 50,
}
```

ルール:

- severity は diagnostic record の深刻さを表す
- severity だけで runtime boundary が停止するかは決まらない
- profile は `Trace` や `Info` を suppress してよい
- profile は required な `Error` や `Fatal` の報告を黙って suppress してはならない

例:

- `Error` は 1 つの command frame を失敗させ得る
- `Error` は 1 つの scope operation を失敗させ得る
- `Fatal` は kernel boot を失敗させ得る

---

## Diagnostic Failure Boundary Model

failure boundary は、どこで execution を止めるか、または invalidated にするかを定義する。

説明用モデル:

```csharp
public enum DiagnosticFailureBoundary
{
    None = 0,
    Operation = 10,
    Command = 20,
    CommandFrame = 30,
    Scope = 40,
    Scene = 50,
    Kernel = 60,
    Build = 70,
}
```

ルール:

- diagnostic が failure handling に関与する場合、failure boundary は明示的でなければならない
- severity を failure boundary の暗黙的な代用品として使ってはならない
- owner spec は、それぞれの domain で failure がどの boundary に対応するかを引き続き定義する
- 11 は、その決定を運ぶ shared representation を定義する

---

## Diagnostic Domain and Category Model

すべての diagnostic は 1 つの domain に属さなければならない。
diagnostic はさらに 1 つの category に属してよい。

domain は subsystem 領域である。
category は、より狭い error family である。

説明用 domain model:

```csharp
public enum DiagnosticDomain
{
    Kernel = 10,
    Boot = 20,
    Generation = 30,
    Validation = 40,
    ServiceGraph = 50,
    ScopeGraph = 60,
    Lifecycle = 70,
    Command = 80,
    Value = 90,
    RuntimeQuery = 100,
    Save = 110,
    UnityBridge = 120,
    Diagnostics = 130,
    LegacyCompat = 900,
}
```

category は stable symbolic family または numeric family として、domain owner が所有してよい。

ルール:

- すべての diagnostic はちょうど 1 つの primary domain を持たなければならない
- category は domain ownership と矛盾してはならない
- shared infrastructure diagnostics は domain として `Diagnostics` を使ってよい

---

## Diagnostic Event, Correlation, and Session Model

diagnostic identity には複数の層がある。

- `DiagnosticCode` は failure family を識別する
- `DiagnosticEventId` は 1 回 emit された event instance を識別する
- `DiagnosticCorrelationId` は関連する event を結び付ける
- `DiagnosticSessionId` は 1 つの高位 activity からの event をまとめる

説明用スケッチ:

```csharp
public readonly struct DiagnosticEventId
{
    public readonly long Value;
}

public readonly struct DiagnosticCorrelationId
{
    public readonly long Value;
}

public readonly struct DiagnosticSessionId
{
    public readonly long Value;
}
```

代表的な session scope:

- 1 回の boot attempt
- 1 回の generation run
- 1 回の validation run
- 1 回の command frame
- 1 回の save operation

ルール:

- event identity は run 間で stable である必要はない
- correlation は 1 つの session 内で関連 record を結び付けるのに十分 stable であるべきである
- test は event-instance の詳細を assert する前に、stable code と relevant context を assert すべきである

---

## Diagnostic Record Model

`KernelDiagnostic` は、target-kernel subsystem が報告する shared structured record である。

説明用スケッチ:

```csharp
public sealed class KernelDiagnostic
{
    public DiagnosticEventId EventId;
    public DiagnosticSessionId SessionId;
    public DiagnosticCorrelationId CorrelationId;

    public DiagnosticCode Code;
    public DiagnosticSeverity Severity;
    public DiagnosticDomain Domain;
    public DiagnosticFailureBoundary FailureBoundary;

    public string Message;
    public DiagnosticContext Context;
    public DiagnosticPayload Payload;
    public DiagnosticExceptionInfo Exception;
}
```

record model に必要な性質:

- message text は optional な display data であり、唯一の semantic data ではない
- code、domain、severity、context は first-class data である
- payload は test と non-Unity sink に十分構造化されていなければならない
- exception data は host output を直接出さずに capture できなければならない

### Message Policy

message は display と summary のために存在してよい。
message は唯一の diagnostic data であってはならない。

禁止される pattern:

- code のない message-only diagnostic
- domain のない message-only diagnostic
- provenance が必要なのに provenance のない message-only diagnostic

test は、正確な message string の一致よりも、diagnostic code、domain、failure boundary、関連 context field を優先すべきである。

---

## Diagnostic Context Model

`DiagnosticContext` は domain をまたいで使われる共有 structured context を運ぶ。

説明用スケッチ:

```csharp
public sealed class DiagnosticContext
{
    public ModuleId OwnerModule;
    public SourceLocationId Source;
    public ArtifactSetId ArtifactSet;
    public int ProfileId;
    public RuntimeIdentityRef[] RuntimeIdentities;
    public DiagnosticCorrelationId CorrelationId;
    public string Phase;
}
```

必要な context dimension:

- 該当する場合の owner module
- 選択された profile
- 該当する場合の source location
- 該当する場合の artifact-set context
- 該当する場合の runtime identity reference
- domain が要求する場合の phase または operation label

context は structured のままでなければならない。
1 つの formatted summary string にだけ flatten してはならない。

### Context Rules

- owner spec が、その domain failure に対してどの provenance field が必須かを定義する
- 11 は shared representation と minimum compatibility rule を定義する
- 必須 source location または必須 runtime identity の欠落は diagnostics degradation である。ただし、欠落した項目自体が報告対象の failure である場合は除く

---

## Diagnostic Payload Model

diagnostic payload は、shared context header には入らない domain-specific な structured data を運ぶ。

payload は次の形で表現してよい:

- typed domain-specific struct
- deterministic key-value record
- generation または validation のための batch-oriented diagnostic entry

payload は次を満たさなければならない:

- field meaning が deterministic であること
- Unity string formatting なしで test 可能であること
- non-Unity sink と互換であること

payload は rich-text formatting だけで意味を成立させてはならない。

代表的な payload 内容:

- expected と actual の hash
- requested と resolved の contract
- expected と actual の value kind
- duplicate ID と conflicting owner
- timeout duration と policy
- cancellation reason

---

## Exception Capture Model

exception は diagnostics data であり、自律的な output path ではない。

説明用スケッチ:

```csharp
public sealed class DiagnosticExceptionInfo
{
    public string ExceptionType;
    public string Message;
    public string StackTrace;
    public DiagnosticExceptionInfo Inner;
}
```

必要なルール:

- exception が failure behavior または observability に関係する場合、subsystem は exception data を `KernelDiagnostic` に capture しなければならない
- target-kernel path で `Debug.LogException` を直接呼んではならない
- failure に関係する exception を diagnostics なしで握りつぶしてはならない
- cancellation exception は、owner spec が許す場合、generic error diagnostics ではなく cancellation diagnostics に map してよい

推奨ルール:

- exception type、message、stack を保持する
- inner exception chain があるなら保持する
- test sink を使うときは test 用に deterministic enough であるよう exception capture を保つ

---

## KernelDiagnosticService Contract

`KernelDiagnosticService` は、diagnostics を report する central entry point である。

subsystem は `KernelDiagnosticService` か、それに flush する承認済み local buffer を通じて diagnostics を report しなければならない。

説明用スケッチ:

```csharp
public interface IKernelDiagnosticService
{
    void Report(in KernelDiagnostic diagnostic);
    void ReportBatch(ReadOnlySpan<KernelDiagnostic> diagnostics);
    DiagnosticSessionHandle BeginSession(DiagnosticSessionInfo info);
    void EndSession(DiagnosticSessionHandle handle);
}
```

必要な service behavior:

- individual と batch の diagnostics を受け入れる
- 選択された sink と host policy に対して deterministic enough な順序を保つ
- session と correlation metadata をサポートする
- configured processing pipeline と sink を通じて diagnostics を forward する

`KernelDiagnosticService` は次を行ってはならない:

- subsystem-specific final sink によって bypass されること
- producer code path で Unity console formatting を行うこと
- required な `Error` または `Fatal` diagnostics を黙って drop すること

---

## Diagnostic Processor Contract

diagnostic processing は report と final sink の間にある。

processing stage は次を行ってよい:

- diagnostics enrichment
- de-duplication
- throttling
- aggregation
- profile filtering
- sink fan-out の準備

processing は次を行ってはならない:

- diagnostic の意味を変えること
- required failure boundary を消すこと
- required error の first occurrence を黙って捨てること
- ある domain の failure を別の domain の意味へ変換すること

processing は policy layer であり、semantic rewrite layer ではない。

---

## DiagnosticSink Contract

`DiagnosticSink` は `KernelDiagnostic` record を consume する。

許可される sink family:

- `UnityLogDiagnosticSink`
- `FileDiagnosticSink`
- `EditorDiagnosticSink`
- `TestDiagnosticSink`
- `InMemoryDiagnosticSink`
- `RemoteDiagnosticSink`

説明用スケッチ:

```csharp
public interface IKernelDiagnosticSink
{
    void Emit(in KernelDiagnostic diagnostic);
    void Flush();
}
```

ルール:

- sink は同じ structured diagnostic を host ごとに異なる方法で render してよい
- sink は diagnostic semantics を再定義してはならない
- sink の選択によって failure が error か fatal かが変わってはならない

---

## UnityLogDiagnosticSink Policy

`UnityLogDiagnosticSink` は、target-kernel で直接 Unity Debug API を呼べる唯一の component である。

`KernelDiagnostic` record を profile policy に従って Unity console output に map する。

代表的な severity mapping:

- `Trace` -> 通常は suppressed、または明示的に有効化された場合のみ routed
- `Info` -> `Debug.Log`
- `Warning` -> `Debug.LogWarning`
- `Error` -> `Debug.LogError`
- `Fatal` -> `Debug.LogError` with fatal marker または同等の stable formatting rule

### Exception Output Rule

diagnostic が exception information を含む場合、`UnityLogDiagnosticSink` は次を選んでよい:

- `Debug.LogException` を呼ぶ
- exception text を 1 つの `Debug.LogError` の中に描画する

この選択は、選択された profile と sink configuration に対して deterministic でなければならない。

この決定を直接行えるのは他の subsystem ではない。

### Rendering Rule

rich text、section formatting、console emphasis は sink-specific rendering に属する。

producer path は、意味論のために Unity rich-text rendering に依存してはならない。

これにより次が可能になる:

- Development では rich な Unity console output
- Release では compact な Unity output
- test では structured snapshot
- non-Unity file / remote sink でも情報損失なし

---

## Profile-Based Diagnostics Policy

diagnostics behavior は profile-aware である。

必要な profile kind は 05 に揃える:

- Development
- Release
- Test

### Profile Matrix

| Policy | Development | Release | Test |
|---|---|---|---|
| Full source mapping | Required | 00/05 の制約内でのみ削減可 | Required |
| DebugMap degradation | Error | stable code と解釈可能な identity がある場合のみ許可 | Boundary に応じて Error か Fatal |
| Trace / Info output | Enabled または configurable | 通常は suppressed | test policy で必要なら capture |
| Exception detail | Full | 最小限 required または policy-limited | Full captured |
| Rich Unity formatting | Allowed | Optional | 必須ではない |
| Test sink capture | Optional | Optional | Required |
| Silent fallback | Forbidden | Forbidden | Forbidden |

profile は次を変えてよい:

- output detail
- sink routing
- verbosity
- exception rendering detail

profile は次を変えてはならない:

- diagnostic identity
- required failure boundary
- invalid runtime input を valid と見なすかどうか

---

## Diagnostics Degradation Policy

diagnostics degradation は、必要な diagnostic information が欠落または利用不能なときに発生する。

代表的な degradation 条件:

- `DiagnosticCode` の欠落
- 必要な domain の欠落
- 必要な failure boundary の欠落
- 必要な source location の欠落
- 必要な DebugMap entry の欠落
- 必要な runtime identity kind の欠落
- structured data を必要とする failure に対する message-only record

ルール:

- diagnostics degradation 自体も diagnostic として表現できなければならない
- Development と Test profile は、下位仕様がより厳しい rule を定めない限り、diagnostics degradation を error として扱う
- Release profile は、stable code と解釈可能な identity が残る場合に限り、detail を削減してよい

diagnostics degradation は sink formatting や profile filtering によって隠してはならない。

---

## De-duplication, Throttling, and Aggregation Policy

diagnostics は processor または sink pipeline によって de-duplicate、throttle、aggregate してよい。

代表的な de-duplication key の要素:

- `DiagnosticCode`
- `DiagnosticDomain`
- `SourceLocationId`
- `RuntimeIdentityRef`
- selected profile

ルール:

- first occurrence を隠してはならない
- de-duplication は failure boundary を変えてはならない
- throttling は fatal または required error が起きた事実を suppress してはならない
- aggregation summary は debugging に十分 traceable でなければならない

代表的な use case:

- lifecycle や monitor loop における repeated tick failure
- 1 つの壊れた authored binding に対する repeated value type mismatch
- 1 つの detached または invalid execution policy に対する repeated command timeout

---

## Diagnostics Performance Policy

diagnostics は runtime hot path でも安全でなければならない。

要件:

- producer path で高コストな string formatting を避ける
- rich text formatting は可能なら sink-local に置く
- repeated diagnostics は throttling または aggregation をサポートする
- disabled `Trace` や suppressed `Info` path は、可能な範囲で不要 allocation を避ける
- hot diagnostics reporting path で LINQ を使わない
- generation と validation では batch reporting をサポートする

performance は次を犠牲にして得てはならない:

- required structured context の削除
- stable error code の削除
- required DebugMap coverage の削除
- failure boundary の隠蔽

Observability は runtime contract の一部である。

---

## Subsystem Integration Contract

11 は、複数の subsystem owner を 1 つの diagnostics substrate の下で統合する。

ルール:

```text
Owner spec が failure の意味を定義する。
11 は、その failure をどう表現し、どう routing し、DebugMap でどう解決し、どう emit するかを定義する。
```

### Generation Diagnostics Integration

03 の下で生成される diagnostics は、`KernelDiagnostic` と互換な record shape を使わなければならない。

03 が引き続き所有するもの:

- generation failure の意味
- generation completeness rule
- artifact consistency rule

11 が所有するもの:

- shared record compatibility
- sink separation
- code identity governance
- generation output が使う DebugMap runtime contract

generation は batch diagnostics を多用してよい。

### Validation Diagnostics Integration

04 の下で生成される diagnostics は、`KernelDiagnostic` と互換な record shape を使わなければならない。

04 が引き続き所有するもの:

- dependency failure の意味
- validation phase semantics
- validation status classification

11 が所有するもの:

- shared record compatibility
- stable diagnostics substrate
- sink と output の分離

### Boot Diagnostics Integration

05 の下で生成される boot diagnostics は、11 の record model を使わなければならない。

05 が引き続き所有するもの:

- boot acceptance gate
- boot failure boundary
- profile selection と boot policy

11 が所有するもの:

- central diagnostics routing
- boot reporting が使う DebugMap runtime contract
- sink policy と Unity output の中央集約

`BootDiagnosticsPolicy` は boot-specific な presentation や capture behavior を設定してよい。
それは parallel diagnostics architecture を定義してはならない。

### ServiceGraph Diagnostics Integration

06 は required service failure provenance と behavior を定義する。
11 は、それら diagnostics が `KernelDiagnostic` record として emit されることを要求する。

必要な integration rule:

- service diagnostics は 06 が要求する provenance field を含まなければならない
- service diagnostics は `KernelDiagnosticService` を迂回してはならない
- service diagnostics は人が読む出力のために DebugMap で service identity を解決してよい

### ScopeGraph Diagnostics Integration

07 は required scope failure provenance と behavior を定義する。
11 は、それら diagnostics が `KernelDiagnostic` record として emit されることを要求する。

必要な integration rule:

- handle-like diagnostics は generation-aware な runtime identity data を持たなければならない
- DebugMap は検証済み scope plan と authoring identity を解決する
- live handle instance information は runtime state が供給する

### Lifecycle Diagnostics Integration

08 は required lifecycle provenance と failure behavior を定義する。
11 は、それら diagnostics が `KernelDiagnostic` record として emit されることを要求する。

必要な integration rule:

- lifecycle diagnostics は 08 が要求する lifecycle step provenance を持たなければならない
- timeout と cancellation は ad hoc な text-only logging なしで表現できなければならない
- automatic handler discovery failure は structured diagnostics として表現できなければならない

### Command Diagnostics Integration

09 は required command diagnostics fields と failure behavior を定義する。
11 は、それらを emit する shared substrate を定義する。

必要な command context には、少なくとも 09 が要求する次の field が含まれる:

- `CommandTypeId`
- 09 が許す場合の authoring key
- payload schema identity
- executor identity
- execution frame または同等の command-local execution context
- 利用可能な場合の actor と target reference
- source location

移行方向:

```text
UnityCommandResolveLogger
  -> CommandDiagnosticAdapter
  -> KernelDiagnosticService
  -> UnityLogDiagnosticSink
```

command-specific な rich formatting は sink renderer として残ってよい。
producer-owned な Unity logger として残してはならない。

### Value Diagnostics Integration

10 は required value diagnostics fields と failure behavior を定義する。
11 は、それらを emit する shared substrate を定義する。

必要な value context には、少なくとも 10 が要求する次の field が含まれる:

- `ValueKeyId`
- `ValueSchemaId`
- 関連する場合の store identity と store scope
- 関連する場合の value kind と revision context
- source location

value diagnostics は stable key を runtime truth として使ってはならない。
stable key は、10 が許す場合に限り diagnostics metadata または migration metadata として現れてよい。

### RuntimeQuery Diagnostics Integration

RuntimeQuery diagnostics は `KernelDiagnostic` を使わなければならない。

必要な context:

- query identity または query kind
- 要求された target identity
- ambiguity または missing-result の分類
- owner module
- verified authored request から来る場合の source location

RuntimeQuery failure の意味は、それを定義する spec が所有し続ける。
11 は、それら failure を emit するための shared diagnostics substrate と sink rule を定義する。

### Save Diagnostics Integration

Save には current doc set に dedicated な v2 spec がない。
したがって 11 は、save format semantics を所有せずに、Save-domain failure のための一時的な shared diagnostics contract を定義する。

save diagnostics は `KernelDiagnostic` を使わなければならない。

必要な save context:

- save operation identity または label
- save slot、profile、target
- 関連する場合の owner scope または runtime target
- 関連する場合の `ValueKeyId` または entity / runtime identity
- authored save metadata がある場合の source location
- exception が failure behavior に関与する場合の exception payload

移行方向:

```text
ISaveLogger / UnitySaveLogger
  -> SaveDiagnosticReporter or SaveDiagnosticAdapter
  -> KernelDiagnosticService
  -> UnityLogDiagnosticSink
```

11 は save storage semantics や save format を定義しない。
save failure と warning が unified diagnostics substrate に入る方法だけを定義する。

---

## Legacy Migration Policy

legacy logging path は、統一された diagnostics substrate への adapter にならなければならない。

| Legacy Pattern | Target Representation |
|---|---|
| subsystem 内の `Debug.LogError(...)` | `KernelDiagnosticService.Report(KernelDiagnostic)` |
| `LTSLog.LogError(...)` | legacy diagnostics adapter または `KernelDiagnosticService` への direct reporter |
| `UnityCommandResolveLogger` | `CommandDiagnosticAdapter` |
| `CommandExecutorRegistry` の direct Unity log | command-domain code を持つ command diagnostics record |
| `ISaveLogger` / `UnitySaveLogger` | save diagnostics reporter |
| dynamic / expression runtime の direct Unity log | typed diagnostics payload + central sink rendering |
| direct exception log | exception payload + central sink policy |

13 は、一時的な legacy adapter を残してよいかを所有する。
11 は、adapter が feed すべき target representation を定義する。

---

## Forbidden Patterns

target diagnostics architecture で禁止されるもの:

- `UnityLogDiagnosticSink` 以外での `Debug.LogError`
- approved sink 以外での `Debug.LogWarning`
- approved sink 以外での `Debug.LogException`
- final output path としての subsystem-specific Unity logger
- formatted string のみで表現された error
- failure が relevant なのに diagnostics なしで exception を握りつぶすこと
- `DiagnosticCode` のない diagnostic
- domain のない diagnostic
- severity だけから推測された required failure boundary
- coverage が必要なのに Development または Test で DebugMap 解決のない runtime-facing ID
- command または save failure に対する diagnostics pipeline bypass
- generation、validation、runtime failure に対する互換性のない別々の error shape
- runtime lookup truth または fallback repair として DebugMap を使うこと

---

## Test Case Model

各 diagnostics test case は次を定義しなければならない:

- Test ID
- Title
- Input または fixture
- Selected profile
- Expected diagnostics codes
- Expected domains
- applicable な場合の Expected failure boundary
- Expected provenance fields
- relevant な場合の Expected sink behavior

推奨 fixture format:

```md
### TC_DIAG_001_Example

入力:
- ...

Profile:
- Development

期待結果:
- Diagnostic: ...
- Domain: ...
- FailureBoundary: ...
- Required Context: ...
```

test は、完全な message-string identity よりも stable code と structured context assertion を優先すべきである。

---

## Required Test Cases

### Central Logging Tests

#### TC_DIAG_LOG_001_SubsystemCannotCallDebugLogError

```text
入力:
- subsystem の code path が直接 `Debug.LogError` を呼ぼうとする

期待結果:
- analyzer または forbidden API validation が失敗する
- `DIAG_DIRECT_UNITY_LOG_FORBIDDEN`
```

#### TC_DIAG_LOG_002_UnitySinkEmitsUnityOutput

```text
入力:
- `KernelDiagnostic` の severity = `Error`
- `UnityLogDiagnosticSink` が有効

期待結果:
- sink policy に従って、論理的に 1 回だけ Unity 側 error が emit される
```

#### TC_DIAG_LOG_003_CommandErrorUsesCentralPipeline

```text
入力:
- command executor が見つからない

期待結果:
- Domain = `Command`
- DiagnosticCode = `COMMAND_EXECUTOR_MISSING`
- `KernelDiagnosticService` を通じて emit される
- command-specific direct Unity logger が final output path にならない
```

### DebugMap Tests

#### TC_DIAG_MAP_001_ServiceIdResolved

```text
入力:
- diagnostic に `ServiceId` が含まれている
- DebugMap に一致する entry がある

期待結果:
- service の debug name と source location が出力に含まれる
```

#### TC_DIAG_MAP_002_MissingDebugMapEntryFailsInDevelopment

```text
Profile:
- Development

入力:
- diagnostic に runtime-facing identity が含まれている
- 必要な DebugMap entry が欠けている

期待結果:
- `DIAG_DEBUGMAP_ENTRY_MISSING`
- diagnostics degradation は error として扱われる
```

#### TC_DIAG_MAP_003_RuntimeHandleIncludesGeneration

```text
入力:
- diagnostic に ScopeHandle の index と generation が含まれている

期待結果:
- emit された diagnostics に index と generation の両方が含まれる
- DebugMap と runtime handle data によって human-readable 性が保たれる
```

### Structured Payload Tests

#### TC_DIAG_PAYLOAD_001_MessageIsNotOnlyData

```text
入力:
- diagnostic に message はあるが code がない

期待結果:
- Failed
- `DIAG_CODE_MISSING`
```

#### TC_DIAG_PAYLOAD_002_ExceptionCapturedAsPayload

```text
入力:
- subsystem が exception を catch する

期待結果:
- `KernelDiagnostic` に exception type、message、stack が含まれる
- subsystem は `Debug.LogException` を直接呼ばない
```

### Integration Tests

#### TC_DIAG_COMMAND_001_CommandResolveFailure

```text
入力:
- command resolve が失敗する

期待結果:
- Domain = `Command`
- Code = `COMMAND_RUNTIME_QUERY_MISSING` か、別の command-owned な resolve failure code
- Context には 09 が要求する command source と execution context が含まれる
```

#### TC_DIAG_SAVE_001_SaveFailure

```text
入力:
- save operation が exception を投げる

期待結果:
- Domain = `Save`
- save-domain code がある
- exception payload がある
- Unity output は `UnityLogDiagnosticSink` 経由のみ
```

#### TC_DIAG_VALUE_001_ValueTypeMismatch

```text
入力:
- `ValueStore` の write で type mismatch が起きる

期待結果:
- Domain = `Value`
- Code = `VALUE_TYPE_MISMATCH`
- Context に `ValueKeyId` と schema identity が含まれる
```

### De-duplication and Throttling Tests

#### TC_DIAG_THROTTLE_001_FirstOccurrenceAlwaysEmitted

```text
入力:
- 同じ error が 100 回繰り返される

期待結果:
- first occurrence は emit される
- repeated occurrences は policy に応じて summarized または throttled される
```

#### TC_DIAG_THROTTLE_002_FailureBoundaryNotSuppressed

```text
入力:
- fatal diagnostic が繰り返される

期待結果:
- failure boundary は引き続き適用される
- output は throttled されてもよい
- failure meaning は suppress されない
```

---

## 受け入れ基準

この仕様は、次を定義するときに完了である:

- DebugMap の目的と coverage 要件
- SourceLocation と runtime / artifact identity mapping
- KernelDiagnostic record model
- DiagnosticCode のガバナンス
- severity、domain、category、failure-boundary model
- central diagnostics pipeline
- `KernelDiagnosticService` contract
- `DiagnosticSink` contract
- `UnityLogDiagnosticSink` policy
- central Unity logging rule
- exception capture policy
- Generation / Validation / Boot / ServiceGraph / ScopeGraph / Lifecycle / Command / Value / RuntimeQuery / Save に対する subsystem integration rule
- diagnostics degradation rule
- profile-based diagnostics policy
- de-duplication、throttling、aggregation policy
- diagnostics performance policy
- legacy migration policy
- forbidden patterns
- required diagnostics test case

完了には、11 が presentation-only note として扱われないことも必要である。
Kernel v2 の shared structured diagnostics substrate であり続けなければならない。

---

## 最終見解

DebugMap と diagnostics は optional な tooling ではない。
runtime contract の一部である。

DebugMap は、検証済み runtime identity を human-readable source に trace 可能にするために存在する。
diagnostics は、重要な failure、warning、関連情報を 1 つの structured で testable、profile-aware な pipeline に通すために存在する。

subsystem は failure が起きた場所で diagnostics を生成する。
subsystem は Unity output を所有しない。
Unity log を emit できるのは中央の Unity diagnostic sink だけである。

この rule は、correctness、scale、testing、observability、migration control に必要である。

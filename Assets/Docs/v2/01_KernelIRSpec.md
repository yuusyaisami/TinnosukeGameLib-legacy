# KernelIR 仕様書

## 文書ステータス

- 文書 ID: `01_KernelIRSpec`
- 状態: Draft
- 役割: GameLib Kernel v2 アーキテクチャにおける、正規化済み中間表現の仕様
- 依存先: [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md)
- この仕様を基盤としている文書:
  - [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md)
  - [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md)
  - [04_DependencyValidationSpec.md](04_DependencyValidationSpec.md)
  - [05_BootManifestAndProfileSpec.md](05_BootManifestAndProfileSpec.md)
  - [06_ServiceGraphRuntimeSpec.md](06_ServiceGraphRuntimeSpec.md)
  - [07_ScopeGraphRuntimeSpec.md](07_ScopeGraphRuntimeSpec.md)
  - [08_LifecyclePlanSpec.md](08_LifecyclePlanSpec.md)
  - [09_CommandCatalogRuntimeSpec.md](09_CommandCatalogRuntimeSpec.md)
  - [10_ValueSchemaAndStoreSpec.md](10_ValueSchemaAndStoreSpec.md)
  - [11_DebugMapAndDiagnosticsSpec.md](11_DebugMapAndDiagnosticsSpec.md)
  - [12_UnityAuthoringBridgeSpec.md](12_UnityAuthoringBridgeSpec.md)

### 所有範囲

本書は、KernelIR における正規の IR ノードモデル、IR 識別子モデル、ソース位置モデル、依存エッジ表現、正規化不変条件、決定論的な順序要件、およびハッシュに関係する意味データを定義する。

一方で、実行時ストレージのレイアウト、ランタイムハンドルのレイアウト、コマンド実行アルゴリズム、ValueStore のメモリ配置、検証アルゴリズムの詳細は本書の担当外である。

---

## 目的

本仕様は、GameLib Kernel v2 で使用する正規化済み中間表現 `KernelIR` を定義する。

KernelIR は、authoring 入力から生成され、検証、plan 生成、DebugMap 生成、runtime projection 生成に消費される正規モデルである。

KernelIR は runtime 実行形式ではない。
KernelIR は generated code ではない。
KernelIR は Unity の authoring asset そのものでもない。
KernelIR は、検証済みの runtime artifact を導き出すための正規化済み権威である。

KernelIR は、runtime discovery や、その場しのぎの generated artifact 依存を防ぐために存在する。
単一の正規化モデルとして、次の用途に使える必要がある。

- 検証
- ハッシュ化
- 差分比較
- runtime plan への変換
- DebugMap entry への変換
- CI でのテスト

runtime plan を KernelIR まで遡れないなら、その成果物は target kernel の有効な artifact ではない。

---

## 範囲

本仕様は以下を定義する。

- KernelIR の root 構造
- IR ノード分類
- 共通 ID モデル
- ソース位置モデル
- 依存エッジモデル
- 正規化済み contribution 表現
- profile 可用性表現
- 決定論的な順序要件
- ハッシュ対象となる意味データ
- 検証への引き渡し境界

本仕様は、次の内容は意図的に定義しない。

- 最終的な service resolver 実装
- 最終的な scope handle メモリレイアウト
- 最終的な command executor 呼び出しアルゴリズム
- 最終的な ValueStore 格納レイアウト
- 最終的な save format
- 最終的な Unity component schema
- 最終的な検証アルゴリズム
- 最終的な generated code format

この文書は runtime 実装詳細のごみ箱にしてはならない。
runtime 固有の詳細は、下位の runtime 仕様に属する。

---

## 他仕様との関係

| 仕様 | 関係 |
|---|---|
| `02_ModuleContributionSpec.md` | modules が KernelIR に raw data をどう渡すかを定義する |
| `03_VerifiedPlanGenerationSpec.md` | KernelIR が VerifiedKernelPlan になる手順を定義する |
| `04_DependencyValidationSpec.md` | KernelIR の依存関係に対する検証アルゴリズムを定義する |
| `05_BootManifestAndProfileSpec.md` | KernelIR 出力が消費する boot input と profile policy を定義する |
| `06_ServiceGraphRuntimeSpec.md` | ServiceIR projection を消費する |
| `07_ScopeGraphRuntimeSpec.md` | ScopeIR projection を消費する |
| `08_LifecyclePlanSpec.md` | LifecycleIR projection を消費する |
| `09_CommandCatalogRuntimeSpec.md` | CommandIR projection を消費する |
| `10_ValueSchemaAndStoreSpec.md` | ValueKeyIR と value-init 関連 projection を消費する |
| `11_DebugMapAndDiagnosticsSpec.md` | KernelIR の SourceLocation と debug metadata を消費する |
| `12_UnityAuthoringBridgeSpec.md` | authoring 入力を KernelIR に正規化して生成する |

KernelIR は下流 runtime projection の source model である。
下位仕様は、ここで定義した概念を参照し、独自に再定義してはならない。

---

## Assembly Definition と Compile Boundary の期待値

正規化済み IR モデルの想定配置先は `GameLib.Kernel.IR` である。
詳細な dependency matrix は [17_AssemblyDefinitionAndCompileBoundarySpec.md](17_AssemblyDefinitionAndCompileBoundarySpec.md) が管理する。

01 に対する必須の compile-boundary ルールは次のとおり。

- `GameLib.Kernel.IR` は Unity 非依存、Editor 非依存のまま維持する
- `GameLib.Kernel.IR` は `noEngineReferences: true` を使うべきである
- runtime 実行ロジック、feature 実装、legacy adapter を IR assembly に同居させてはならない
- authoring 抽出は `GameLib.Kernel.Authoring.Editor` から IR に流してよいが、IR 自体は正規化済みの core assembly のままである

IR 型が Unity 非依存の core assembly に置けないなら、それは 01 に属さない。

---

## IR パイプライン上の位置

```text
Authoring Inputs
  - Scene / Prefab
  - ModuleDefinitionAsset あるいは同等の contribution source
  - ValueKey registry inputs
  - Command authoring inputs
  - Profile inputs

        ↓ Normalize

KernelIR

        ↓ Validate

Validated KernelIR

        ↓ Generate

VerifiedKernelPlan
  - ServiceGraphPlan
  - ScopeGraphPlan
  - CommandCatalogPlan
  - ValueSchemaPlan
  - LifecyclePlan
  - DebugMap
```

KernelIR は runtime plan 生成の前に作られる。
runtime plan は、KernelIR に存在しない新しい構造的 identity を導入してはならない。

KernelIR は、検証、ハッシュ生成、依存分析、DebugMap 生成、runtime projection 生成の入力である。

---

## IR 設計原則

### 1. Raw ではなく Normalized

KernelIR は、authoring の曖昧さをそのまま保持してはならない。
Prefab override、module alias、authoring key、Editor 専用参照は、KernelIR に入る前に正規化されている必要がある。

### 2. 推測ではなく Explicit

KernelIR は関係を明示的に表現しなければならない。
parent scope、module ownership、lifecycle dependency、service requirement、command requirement、runtime query requirement、value dependency を runtime の推測で再構成してはならない。

### 3. Deterministic

同じ authoring input と profile からは、意味的に同一で、順序も安定した出力が得られなければならない。

### 4. Traceable

runtime 挙動を生みうるすべての IR ノードは、source location に遡れなければならない。

### 5. Hashable

KernelIR には、hash ベースの artifact consistency check を支えられるだけの正規化済み意味データが含まれていなければならない。

### 6. Projection-safe

KernelIR は、ServiceGraphPlan、ScopeGraphPlan、CommandCatalogPlan、ValueSchemaPlan、LifecyclePlan、DebugMap を、隠れた挙動を足さずに導ける構造である必要がある。

### 7. Validation-friendly

KernelIR は、missing dependency、invalid ownership、duplicate identity、profile mismatch を、生成副作用に隠さず structured data として露出しなければならない。

---

## IR 識別子モデル

KernelIR は typed identity を使う。

ID は、宣言された domain の外で解釈してはならない。

例:

- `ServiceId` を `CommandTypeId` として使ってはならない
- `ValueKeyId` を `ServiceId` として使ってはならない
- `ScopeAuthoringId` を runtime `ScopeHandle` として使ってはならない
- `RuntimeQueryId` を lifecycle step identity として使ってはならない

各 IR identity は少なくとも次を定義する必要がある。

- domain
- 安定した name
- 数値または記号値
- owner module
- source location
- profile availability
- debug 表現

KernelIR が持つ typed identity domain は少なくとも次を含む。

- KernelIRId
- ModuleId
- ScopeAuthoringId
- ScopePlanId
- ServiceId
- CommandTypeId
- CommandCategoryId
- CommandPayloadSchemaId
- CommandExecutorId
- CommandAuthoringKeyId
- ValueKeyId
- ValueSchemaId
- ValueStoreId
- ValueStoreScopeId
- ValueInitPlanId
- ValueFieldId
- LifecycleStepId
- RuntimeQueryId
- DependencyNodeId
- DependencyEdgeId
- SourceLocationId

`ScopeAuthoringId` と runtime `ScopeHandle` は別概念である。

- `ScopeAuthoringId` は authoring された scope 定義を識別する
- `ScopePlanId` は KernelIR 内の正規化済み scope 定義を識別する
- `ScopeHandle` は runtime scope instance を識別する

KernelIR には `ScopeAuthoringId` と `ScopePlanId` を含めてよいが、live な `ScopeHandle` は含めてはならない。

---

## Source Location モデル

runtime 挙動に寄与する IR ノードは、すべて source location metadata を持たなければならない。

source location が表しうるものには次が含まれる。

- Unity asset GUID
- asset path
- local file ID
- scene path
- prefab path
- component type
- serialized property path
- generated source reference
- legacy migration origin

`SourceLocationIR` は説明用スケッチであり、最終 runtime API ではない。

```csharp
public readonly struct SourceLocationIR
{
    public SourceLocationKind Kind;
    public string AssetGuid;
    public string AssetPath;
    public long LocalFileId;
    public string ObjectName;
    public string ComponentType;
    public string PropertyPath;
    public string GeneratedFrom;
}
```

KernelIR は、DebugMap 生成、検証 diagnostics、migration tracing を支えられるだけの source information を保持しなければならない。

---

## KernelIR のルート構造

KernelIR は正規化済みモデルの root である。

```csharp
public sealed class KernelIR
{
    public KernelIRHeader Header;
    public KernelProfileIR Profile;

    public ModuleIR[] Modules;
    public ScopeIR[] Scopes;
    public ServiceIR[] Services;
    public CommandIR[] Commands;
    public ValueKeyIR[] ValueKeys;
    public LifecycleIR[] Lifecycles;
    public RuntimeQueryIR[] RuntimeQueries;

    public DependencyEdgeIR[] Dependencies;
    public SourceLocationIR[] Sources;
    public DiagnosticSeedIR[] DiagnosticSeeds;
}
```

KernelIR には、runtime plan 生成、依存検証、DebugMap 生成、artifact consistency 検証に必要な構造情報がすべて含まれていなければならない。

runtime instance や runtime cache は含めてはならない。

---

## KernelIRHeader

```csharp
public sealed class KernelIRHeader
{
    public string DocumentId;
    public int FormatVersion;
    public string ProjectName;
    public string ProfileId;
    public string GeneratorVersion;
    public Hash128 SourceHash;
    public Hash128 NormalizedHash;
}
```

Header には、generation timestamp や machine-local path のような非意味データを含めてはならない。

Header の各項目は、検証、差分比較、artifact consistency check に使われる。

---

## ModuleIR

`ModuleIR` は KernelIR 内における contribution owner である。

```csharp
public sealed class ModuleIR
{
    public ModuleId Id;
    public string Name;
    public ModuleKind Kind;
    public ModuleVersion Version;

    public ModuleAvailabilityIR Availability;
    public SourceLocationId Source;

    public ModuleDependencyIR[] RequiredModules;
    public ModuleDependencyIR[] OptionalModules;
}
```

`ServiceIR`、`CommandIR`、`ValueKeyIR`、`LifecycleIR`、`RuntimeQueryIR`、`ScopeIR` は、下位仕様が shared ownership model を明示しない限り、必ず 1 つの module に owned される必要がある。

shared ownership は既定ではない。
もし下位仕様が shared ownership を導入するなら、diagnostics、削除、migration の振る舞いを明示しなければならない。

`ModuleIR` は module identity、version、availability、ownership、dependency contribution boundary を定義する。
runtime registration order は定義しない。

---

## ScopeIR

`ScopeIR` は runtime scope instance ではなく、正規化済み scope 定義である。

```csharp
public sealed class ScopeIR
{
    public ScopeAuthoringId AuthoringId;
    public ScopePlanId PlanId;
    public string Name;
    public ScopeKind Kind;

    public ModuleId OwnerModule;
    public ScopeAuthoringId ParentAuthoringId;

    public ScopeServiceRequirementIR[] RequiredServices;
    public ScopeValueInitRefIR[] ValueInitPlans;
    public LifecyclePlanRefIR Lifecycle;
    public SourceLocationId Source;
}
```

`ScopeIR` は Unity の transform hierarchy から parentage を推測してはならない。

もし normalization 時に authoring hierarchy から parent を導くなら、その結果は明示的に `ScopeIR` に書き込まなければならない。
runtime は、その明示的な parent 関係だけを消費し、階層推測を再実行してはならない。

`ScopeIR` は required service、initial value plan、lifecycle plan を参照してよいが、runtime handle や runtime object reference は埋め込まない。

---

## ServiceIR

`ServiceIR` は service identity、lifetime category、ownership、dependency を定義する。

```csharp
public sealed class ServiceIR
{
    public ServiceId Id;
    public string Name;
    public ServiceLifetimeKind Lifetime;
    public ModuleId OwnerModule;

    public ServiceContractIR[] Contracts;
    public ServiceDependencyIR[] Dependencies;

    public ServiceFactoryKind FactoryKind;
    public SourceLocationId Source;
}
```

`ServiceIR` は、最終的な runtime cache レイアウトや resolver 実装を定義しない。

service type metadata には生成や diagnostics 用に C# type 名を含めてもよいが、runtime identity は `ServiceId` である。
C# type 名だけを唯一の stable identity としてはいけない。

`ServiceIR` は runtime reflection に依存せず成立しなければならない。
下位仕様で runtime reflection を使う場合は、その例外が bounded で取り除き可能であることを明示しなければならない。

---

## CommandIR

`CommandIR` は、authoring key と runtime dispatch identity の正規化された境界を定義する。

```csharp
public sealed class CommandIR
{
    public CommandTypeId TypeId;
    public string RuntimeName;
    public CommandAuthoringKeyRefIR AuthoringKey;
    public CommandCategoryId CategoryId;
    public ModuleId OwnerModule;

    public CommandPayloadSchemaRefIR PayloadSchema;
    public CommandExecutorRefIR Executor;
    public CommandDependencyIR[] Dependencies;

    public SourceLocationId Source;
}
```

`AuthoringKey` は runtime dispatch identity ではない。

保持する場合、authoring-key metadata は bare な runtime string ではなく、typed かつ provenance-aware であるべきである。
正規化済み IR には少なくとも次を含めるべきである。

- `CommandAuthoringKeyId`
- 正規化された authoring key 文字列値（KernelIR へ入る前に前後空白を除去）
- 保持された authoring-key metadata 用の `SourceLocationId`

runtime の command dispatch は `CommandTypeId`、あるいはそれに相当する検証済み runtime identity を使わなければならない。
`AuthoringKey` は editor、migration、diagnostics のために保持できる。

authoring key から runtime identity への変換は、target runtime dispatch の最中ではなく、normalization か validation 中に行う。

`CommandIR` は、移行層が必要とするなら、現在の ID ベース executor path と authoring-key ベース catalog path の両方を表現できなければならない。

`CommandIR` は executor construction policy を定義しない。
最終的な command catalog、payload validation、executor lifetime、command category、authoring-key boundary policy は 09 が定義する。

---

## ValueKeyIR

`ValueKeyIR` は、value identity と schema ownership の正規化済み表現を定義する。

```csharp
public sealed class ValueKeyIR
{
    public ValueKeyId Id;
    public string StableKey;
    public string DisplayName;
    public ValueKind Kind;

    public ModuleId OwnerModule;
    public ValueSchemaRefIR Schema;
    public SavePolicyIR SavePolicy;
    public SourceLocationId Source;
}
```

`StableKey` は runtime lookup の truth ではない。

`StableKey` は authoring、migration、diagnostics、registry validation のために存在する。
runtime access は `ValueKeyId`、または検証済みの generated accessor を使う必要がある。

`ValueKeyIR` は、どの値が存在しうるかを定義する。
runtime storage layout は定義しない。

`ValueKeyIR` は、`ValueStore` の格納、初期化順序、動的評価、reactive invalidation、CommandLocal lifetime、save payload format を所有しない。
値 schema、runtime store、init plan、evaluation boundary、command-local boundary、save metadata policy は 10 が定義する。

`ValueKeyIR` に初期値を直接入れるべきではない。もし下位仕様が default value を持てると定義するなら、それが schema と initialization の責務を曖昧にしない理由まで説明する必要がある。

---

## LifecycleIR

`LifecycleIR` は explicit な lifecycle 参加を表す。

```csharp
public sealed class LifecycleIR
{
    public LifecyclePlanId PlanId;
    public string Name;
    public ModuleId OwnerModule;

    public LifecycleStepIR[] Steps;
    public SourceLocationId Source;
}
```

```csharp
public sealed class LifecycleStepIR
{
    public LifecycleStepId Id;
    public LifecyclePhase Phase;
    public int Order;

    public LifecycleTargetRefIR Target;
    public LifecycleActionKind Action;
    public DependencyEdgeId[] Dependencies;

    public SourceLocationId Source;
}
```

interface を実装しているだけでは lifecycle dispatch に参加したことにはならない。
参加は `LifecycleIR` として表現される必要がある。

`LifecycleIR` は runtime の registration scan から導出してはならない。

lifecycle order は emergent behavior ではなく、明示的な data である。

lifecycle target は explicit かつ typed でなければならない。
`LifecycleIR` は、すべての lifecycle step が service を対象にするとは仮定しない。

```csharp
public sealed class LifecycleTargetRefIR
{
    public LifecycleTargetKind Kind;

    public ServiceId TargetService;
    public ScopePlanId TargetScope;
    public RuntimeQueryId TargetRuntimeQuery;
    public string TargetLocalRef;
}
```

```csharp
public enum LifecycleTargetKind
{
    Service = 10,
    Scope = 20,
    ValueStore = 30,
    RuntimeQuery = 40,
    RuntimeObjectOwner = 50,
    LegacyAdapter = 90,
}
```

`TargetLocalRef` は generic な runtime search key ではない。
ValueStore の boundary role や explicit owner namespace 内の runtime-object-owner slot のような、下位仕様で検証済みの local reference にのみ使われる。

どの field が意味を持つかは、選択された `LifecycleTargetKind` によって決まる。

各 target kind の具体的な runtime 実体化は下位仕様が定義する。
01 は normalized target boundary のみを定義する。

---

## RuntimeQueryIR

`RuntimeQueryIR` は、queryable な runtime identity と index requirements を定義する。

```csharp
public sealed class RuntimeQueryIR
{
    public RuntimeQueryId Id;
    public string Name;
    public RuntimeQueryTargetKind TargetKind;

    public RuntimeIdentityFieldIR[] IndexedFields;
    public RuntimeQueryPolicyIR Policy;

    public ModuleId OwnerModule;
    public SourceLocationId Source;
}
```

runtime query は service resolution とは別物である。
runtime query を generic DI resolution として実装してはならない。

runtime query system は次を定義しなければならない。

- query 可能な identity field
- runtime index の所有者
- 更新タイミング
- 無効化の振る舞い
- generation safety
- missing / ambiguous 結果の diagnostics
- performance budget

legacy の kind / id / category lookup の置き換えは、ServiceGraph とは別に仕様化し、内部に隠してはならない。

`RuntimeQueryIR` は、service resolution が runtime identity lookup のごみ箱になるのを防ぐために存在する。

---

## DependencyEdgeIR

依存エッジは、検証と projection generation で使う明示的な graph 関係を表す。

```csharp
public readonly struct DependencyEdgeIR
{
    public DependencyNodeIR From;
    public DependencyNodeIR To;
    public DependencyKind Kind;
    public DependencyPhase Phase;
    public DependencyStrength Strength;
    public SourceLocationId Source;
}
```

```csharp
public enum DependencyPhase
{
    Build = 10,
    Generate = 20,
    Boot = 30,
    Acquire = 40,
    Runtime = 50,
    Save = 60,
    EditorOnly = 70,
}
```

```csharp
public enum DependencyStrength
{
    Required = 10,
    Optional = 20,
    Weak = 30,
    DiagnosticOnly = 40,
}
```

`DependencyEdgeIR` は、runtime 挙動から依存を再構成せずに、`04_DependencyValidationSpec` が missing relationship、cycle、phase violation を検出できるだけの情報を持たなければならない。

`DependencyEdgeIR` には runtime call stack や executor instance を入れてはならない。

---

## Profile と Conditional Availability

### `KernelProfileIR`

profile は、generation、validation、runtime の振る舞いに影響する。

```csharp
public sealed class KernelProfileIR
{
    public string ProfileId;
    public string DisplayName;
    public KernelProfileKind Kind;
    public ModuleAvailabilityIR[] ModuleAvailability;
    public DebugMapPolicyIR DebugMapPolicy;
}
```

profile は宣言的でなければならず、runtime discovery で決めてはならない。

### ModuleAvailabilityIR

```csharp
public sealed class ModuleAvailabilityIR
{
    public ProfileSetId ProfileSetId;
    public BuildTargetSetId BuildTargetSetId;
    public AvailabilityKind Kind;
    public SourceLocationId Source;
}
```

availability は、profile 選択や build target 選択を表現できる。
ただし、runtime expression evaluation に依存してはならない。

---

## 正規化ルール

KernelIR は、入力を正規化した後の唯一の構造モデルである。

正規化では少なくとも次を保証する。

- raw authoring alias を解消する
- 余分な空白や表記揺れを除去する
- typed identity に統一する
- owner module を明示する
- source location を付与する
- dependency edge を明示する
- profile availability を明示する

正規化で未解決のまま残してよいものは、あらかじめ未解決として表現された structured data だけである。
暗黙 fallback で埋めてはいけない。

---

## 決定論的順序ルール

同じ input からは、常に同じ意味順序で KernelIR を生成しなければならない。

推奨順序は次のとおり。

1. owner module
2. identity domain
3. stable name
4. source location
5. 明示された dependency order

次に依存することを前提に、同じ意味の並びが保たれなければならない。

順序が決定論的に確定できないなら、その generation は失敗させる。

---

## ハッシュ入力ルール

ハッシュは、意味的に重要なデータだけを含める必要がある。

含めるべきもの:

- normalized KernelIR content
- module identity と version
- identity assignment
- projection に関係する dependency edge
- runtime ID に関係する registry content
- profile に影響する設定
- generator version
- 対応する format version

含めてはいけないもの:

- generation timestamp
- absolute local path
- editor の selection state
- foldout state
- 非意味的な formatting
- runtime instance identity

詳細な hash アルゴリズムと normalization ルールは、下位仕様で定義する。

---

## Diagnostics と DebugMap の要件

KernelIR には、DebugMap を生成できるだけの source と debug metadata が必要である。

runtime-facing ID はすべて、少なくとも次に遡れなければならない。

- debug name
- owner module
- source location
- profile availability
- 必要に応じて legacy origin

KernelIR は、missing dependency、duplicate identity、profile mismatch のための diagnostic seed 情報を提供すべきである。

`DiagnosticSeedIR` は、structured validation または debug metadata のための placeholder であり、runtime 実行構造ではない。

runtime diagnostics では、十分な debug metadata を持たない ID は diagnostics degradation として扱う。

---

## 検証への引き渡し

01 は検証に必要なデータを定義し、04 は検証アルゴリズムと error severity policy を定義する。

KernelIR は、次を検証できるだけの構造を露出しなければならない。

- missing dependency
- duplicate ID
- invalid ownership
- invalid source location
- invalid profile availability
- dependency cycle
- forbidden runtime fallback dependency
- target kernel への legacy leakage

KernelIR 自体は検証しない。
決定論的な検証に必要な構造を提供するだけである。

---

## 禁止内容

KernelIR には次を含めてはならない。

- runtime authority としての live な `UnityEngine.Object` 参照
- runtime `ScopeHandle`
- runtime service instance
- runtime command executor instance
- raw の未解決 authoring key
- raw の未解決 stable value key を runtime reference として使ったもの
- reflection だけに頼った type identity を唯一の service identity とすること
- generated code を source of truth とすること
- mutable runtime state
- fallback で生成した ID
- 非決定論的な順序依存

KernelIR には、runtime search だけで有効化される artifact を入れてはならない。

禁止内容は、見た目の問題ではなく構造上の違反である。

---

## 互換性と移行メモ

legacy 由来のデータは、明示的な migration metadata としてのみ KernelIR に現れてよい。

legacy metadata は runtime fallback 行動に変えてはならない。

例:

- legacy type name
- legacy asset path
- legacy command key
- legacy var stable key
- migration status
- removal target

もし下位仕様が migration-only bridge を要するなら、その bridge を明示し、削除条件まで定義しなければならない。

---

## 下位仕様への未決事項

以下は意図的に後回しにする。

- service resolver の正確な格納レイアウト
- `ScopeHandle` の正確な bit layout
- command payload の binary / serialized representation
- ValueStore の正確なメモリレイアウト
- DebugMap asset の正確な形式
- Unity authoring component schema の正確な形
- validation error code の完全な一覧

01 は、これらをここで決めない。下位仕様の担当だからである。

---

## 受け入れ条件

01 が完成していると見なす条件は次のとおり。

- KernelIR の目的と権威が定義されている
- IR identity model が定義されている
- source location model が定義されている
- root IR node category が定義されている
- lifecycle の explicit target model が定義されている
- dependency edge model が定義されている
- normalization rule が定義されている
- deterministic ordering rule が定義されている
- hash input policy が定義されている
- diagnostics / debug metadata 要件が定義されている
- forbidden contents が定義されている
- 04 への validation handoff が定義されている
- 下位仕様との境界が明確である

runtime 実行詳細、storage layout 詳細、検証アルゴリズム詳細が 01 に入り込んだ時点で未完成である。

## テストケース

| テストケース | 目的 | 検証 |
|---|---|---|
| TC-01-01 | KernelIR の identity が安定的で、明示され、source-backed であることを確認する。 | IR identity model と source location model の節で、安定 ID、owner module、source provenance が定義されていること。 |
| TC-01-02 | 正規化が runtime 順序依存を除去することを確認する。 | normalization と deterministic ordering の節で、enumeration order、reflection order、その他の runtime artifact への依存を禁止していること。 |
| TC-01-03 | hash input から非意味データが除外されることを確認する。 | hash input rule の節で、timestamp、absolute path、runtime instance identity を禁止していること。 |
| TC-01-04 | runtime-facing ID に対して DebugMap coverage が必須であることを確認する。 | diagnostics と DebugMap 要件の節が、存在し続け、具体的であること。 |
| TC-01-05 | lifecycle target が explicit かつ typed であることを確認する。 | `LifecycleIR` の節で、interface や registration discovery に戻らずに target identity を定義していること。 |
| TC-01-06 | validation handoff が IR contract の外側に留まることを確認する。 | validation handoff の節が dependency validation algorithm を吸い込んでいないこと。 |

---

## 最終見解

KernelIR には、runtime discovery を行わずに runtime plan を生成し、dependency を検証し、DebugMap を作成し、artifact consistency を確認するために必要な構造情報がすべて含まれていなければならない。

この仕様は、他の仕様群が誤った前提で進まないようにするための土台である。
KernelIR が不足していれば、下流の仕様はすべてずれていく。
KernelIR が runtime 実装詳細まで広がりすぎれば、下流の仕様は同じ形に押し込められてしまう。

KernelIR は正規化済みの権威である。
VerifiedKernelPlan は runtime projection である。
下位仕様が、それぞれの projection の実現方法を定義する。

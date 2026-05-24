# Verified Plan Generation 仕様

## 文書ステータス

- 文書 ID: `03_VerifiedPlanGenerationSpec`
- 状態: Draft
- 役割: validated KernelIR と module 由来入力が VerifiedKernelPlan と verified runtime artifact set になる方法を定義する
- 依存先:
  - [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md)
  - [01_KernelIRSpec.md](01_KernelIRSpec.md)
  - [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md)
- この仕様を基盤としている文書:
  - [04_DependencyValidationSpec.md](04_DependencyValidationSpec.md)
  - [05_BootManifestAndProfileSpec.md](05_BootManifestAndProfileSpec.md)
  - [06_ServiceGraphRuntimeSpec.md](06_ServiceGraphRuntimeSpec.md)
  - [07_ScopeGraphRuntimeSpec.md](07_ScopeGraphRuntimeSpec.md)
  - [08_LifecyclePlanSpec.md](08_LifecyclePlanSpec.md)
  - [09_CommandCatalogRuntimeSpec.md](09_CommandCatalogRuntimeSpec.md)
  - [10_ValueSchemaAndStoreSpec.md](10_ValueSchemaAndStoreSpec.md)
  - [10_2_DynamicValueEvaluationSpec.md](10_2_DynamicValueEvaluationSpec.md)
  - [11_DebugMapAndDiagnosticsSpec.md](11_DebugMapAndDiagnosticsSpec.md)
  - [14_PerformanceBudgetAndRuntimeRulesSpec.md](14_PerformanceBudgetAndRuntimeRulesSpec.md)
  - [15_TestAndValidationSpec.md](15_TestAndValidationSpec.md)

### Revision Note

本改訂では、03 の冒頭で分かれていた status / purpose / scope の重複を統合した。

generation の trust boundary に対する単一の正規 metadata 節を維持しつつ、依存関係と artifact consistency に関する広い契約を規範文として残している。

### 所有範囲

本仕様は generation pipeline と artifact consistency 契約を所有する。
生成された plan を VerifiedKernelPlan と呼べるようになる前に、何が真でなければならないかを定義する。

本仕様が所有するもの:

- generation pipeline stage
- input artifact 要件
- output artifact set 要件
- VerifiedKernelPlan の定義
- pre-generation validation gate
- generation 時の KernelIR normalization gate 期待値
- projection generation rule
- post-generation validation gate
- artifact set consistency
- hash / version / format compatibility policy
- deterministic generation rule
- DebugMap generation 要件
- generated code / generated asset policy
- stale artifact detection
- Editor / CLI / CI の parity
- incremental generation 制約
- generation failure 挙動
- generation diagnostics 要件

本仕様が所有しないもの:

- KernelIR の node / edge モデルの詳細
- ModuleContribution の authoring 詳細
- dependency validation algorithm
- runtime service graph 実装
- runtime scope graph 実装
- runtime command execution 実装
- runtime value storage 実装
- BootManifest の最終 schema
- DebugMap の最終 serialized layout

本仕様は generation の trust boundary を所有する。
各 generated plan の最終 runtime 実装までは所有しない。

---

## 目的

本仕様は、validated KernelIR から VerifiedKernelPlan と、それに付随する generated artifact を作るプロセスを定義する。

generated だからといって verified ではない。
required generation gate をすべて通過した plan だけが target runtime で使える。

03 は generation の trust boundary である。

KernelIR は正規の権威である。
VerifiedKernelPlan は runtime execution input である。
generated code と generated asset は execution artifact であり、source of truth ではない。

03 の目的は、generated runtime input が次を満たすようにすることだ。

- deterministic
- validated
- traceable
- hash-compatible
- version-compatible
- DebugMap-backed
- runtime execution に安全

generation は file output step ではなく、verification pipeline である。

---

## 範囲

本仕様は次を定義する。

- generation pipeline の概要と stage
- validated input 要件
- output artifact set 要件
- VerifiedKernelPlan の最小定義
- pre-generation validation gate
- post-generation validation gate
- artifact set consistency model
- plan / artifact header semantics
- hash / version compatibility model
- deterministic generation rule
- generated code policy
- generated asset policy
- DebugMap generation 要件
- stale artifact detection
- profile-specific generation 制約
- Editor / CLI / CI parity 要件
- incremental generation policy
- failure policy
- diagnostics 要件
- forbidden pattern

---

## 非目標

本仕様は次を定義しない。

- KernelIR node model の詳細
- module contribution authoring schema
- dependency validation algorithm
- runtime service resolver 実装
- runtime scope graph 実装
- command executor 呼び出しアルゴリズム
- ValueStore storage layout
- DebugMap の最終 serialized format
- BootManifest の最終 schema

本仕様は、無効な generated artifact の代わりとして runtime fallback 行動を定義してはならない。

---

## 他仕様との関係

| 仕様 | 関係 |
|---|---|
| [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md) | root architecture、trust boundary、Verified Plan 要件を定義する |
| [01_KernelIRSpec.md](01_KernelIRSpec.md) | 正規化された構造入力の権威として KernelIR を提供する |
| [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md) | KernelIR normalization に流れ込む module contribution 契約を定義する |
| `04_DependencyValidationSpec.md` | generation 前に受理されるべき validation algorithm を定義する |
| `05_BootManifestAndProfileSpec.md` | Verified artifact set を BootManifest がどう参照し、boot-time policy をどう強制するかを定義する |
| `06_ServiceGraphRuntimeSpec.md` | この仕様で生成された ServiceGraphPlan を消費する |
| `07_ScopeGraphRuntimeSpec.md` | この仕様で生成された ScopeGraphPlan と RuntimeQueryPlan を消費する |
| `08_LifecyclePlanSpec.md` | この仕様で生成された LifecyclePlan を消費する |
| `09_CommandCatalogRuntimeSpec.md` | この仕様で生成された CommandCatalogPlan を消費する |
| `10_ValueSchemaAndStoreSpec.md` | この仕様で生成された ValueSchemaPlan と ValueInitPlan を消費する |
| `10_2_DynamicValueEvaluationSpec.md` | この仕様で生成された DynamicEvaluationPlan と ReactiveEvaluationPlan を消費する |
| `11_DebugMapAndDiagnosticsSpec.md` | この仕様で生成された DebugMap の runtime-facing diagnostics 契約を定義する |
| `14_PerformanceBudgetAndRuntimeRulesSpec.md` | この pipeline の output を消費する generation / runtime-loading budget を定義する |
| `15_TestAndValidationSpec.md` | generation correctness を証明する test と CI gate を定義する |

03 は検証済み input を受け取り、検証済み output を作る。
下位仕様は、runtime behavior から再構成するのではなく、ここで定義された output を消費しなければならない。

---

## Assembly Definition と Compile Boundary の期待値

Verified plan generation の想定配置先は `GameLib.Kernel.Generation` である。
Editor 専用の generated file 書き込みや再生成ツールは `GameLib.Kernel.Generation.Editor` に置く。
詳細な dependency matrix は [17_AssemblyDefinitionAndCompileBoundarySpec.md](17_AssemblyDefinitionAndCompileBoundarySpec.md) が管理する。

03 に対する必須の compile-boundary ルールは次のとおり。

- `GameLib.Kernel.Generation` は runtime subsystem 実装と分離する
- generation core は Unity 非依存で、`noEngineReferences: true` を使うべきである
- deterministic artifact generation logic は MonoBehaviour、ScriptableObject、Unity scene traversal に依存してはならない
- file-system write path、asset refresh hook、editor regeneration command は editor-only assembly に属するべきであり、generation core に入れてはならない

core assembly で Unity runtime または editor object なしに generation logic を表現できないなら、03 の boundary は破れている。

---

## Core Problem

target architecture は runtime discovery をなくす。
その結果、risk は runtime inference から generated artifact の正しさへ移る。

したがって generation は、runtime boot の前に次のリスクを解決しなければならない。

- generated entry の欠落
- stale generated code
- stale generated asset
- DebugMap の不一致
- registry と plan の ID space 不一致
- profile ごとの artifact 不一致
- partial generation output
- 非決定論的な output 順序
- generation failure 後の hidden fallback

generation pipeline は、これらのリスクが runtime に到達するのを防ぐために存在する。

---

## 現行の generation 観測

### 観測のトレーサビリティ

現行の generation 観測は、source code、generated output、editor tool、migration note に遡れなければならない。

この文書を更新したとき、現行コードベースに合わなくなった観測は削除するか legacy migration note に移す。

| 観測 | 根拠の種類 | 期待される下流仕様 |
|---|---|---|
| 現行 generation は domain-specific で断片化している | Source | 03 |
| Flow compiler は source hash と build timestamp を 1 つの asset に保存している | Source | 03, 05 |
| generated code は存在するが、統一された artifact consistency model はまだない | Source | 03 |

### 代表的なアンカー

ここでは、具体的な現在実装の参照先を列挙する。

### 現行の不足点

- generation が 1 つの verified pipeline として表現されていない
- artifact set の一貫性が明示されていない
- stale output の検出が全体契約になっていない
- profile 依存の差異が trust boundary に沿っていない
- Editor、CLI、CI が同じ意味論を共有していない

---

## VerifiedPlan の定義

`VerifiedKernelPlan` は、validated KernelIR と accepted generation pipeline から導かれた runtime execution input である。

`GeneratedPlan` は、generation によって作られた plan である。

`VerifiedKernelPlan` は、必要なすべての validation と consistency check を通過した generated plan である。

runtime boot で使えるのは `VerifiedKernelPlan` だけである。

plan が verified と言えるのは、少なくとも次が成り立つ場合である。

- KernelIR が正規化され、検証されている
- required pre-generation validation が通っている
- 必要な runtime projection がすべて生成されている
- すべての artifact が 1 つの consistent artifact set に属している
- hash と version の互換性が満たされている
- required profile level に応じた DebugMap が存在する
- required source location が揃っている
- required dependency validation がすでに input state を受理している
- artifact set を使えるようにするための forbidden fallback path が不要である

`VerifiedKernelPlan` という語は、verified plan root と、それが参照する artifact set をまとめて指す。
単一ファイルや単一 generated asset に限定されない。

---

## Generation Pipeline 概要

```text
Authoring Inputs
  ↓
ModuleContributionData
  ↓
KernelIR
  ↓ Pre-Generation Validation
Validated KernelIR
  ↓ Projection Generation
Generated Runtime Projections
  ↓ Post-Generation Validation
Consistent Artifact Set
  ↓ Hash / Version / DebugMap Check
VerifiedKernelPlan
```

Pipeline stage:

1. input artifact を収集する
2. validated KernelIR の存在を確認する
3. pre-generation validation gate を実行する
4. KernelIR normalization gate を強制する
5. runtime projection を生成する
6. DebugMap を生成する
7. code artifact を生成する
8. asset artifact を生成する
9. post-generation validation を実行する
10. artifact consistency report を作成し、verified state を公開する

generation は、verification に成功するまで staging area を使う必要がある。
trusted publication は、artifact set 全体が受理された後にのみ行う。

---

## Input Artifact Model

generation input は、任意の editor state ではない。
verification pipeline に対する explicit、versioned、hashable な input である。

required input には次が含まれうる。

- validated KernelIR
- generator version
- target profile
- 必要なら target platform / build family
- KernelIR が参照する registry input
- DebugMap 生成に必要な source location table
- KernelIR が参照する module version / module provenance metadata

optional input には次が含まれうる。

- module contribution provenance record
- legacy migration metadata
- editor-facing build note
- diagnostic verbosity setting
- projection semantics に影響する generation setting

ルール:

- trust-relevant input artifact はすべて versioned かつ hashable でなければならない
- unversioned input は runtime behavior に寄与できない
- raw の ModuleContribution data は KernelIR normalization を迂回してはならない
- validated KernelIR だけが structural input authority である
- module 由来 provenance は traceability と input digest coverage に寄与してよいが、KernelIR structure を再定義してはならない

信頼目的で有効ではない input:

- 現在時刻
- absolute local path
- editor selection state
- editor foldout state
- Unity object instance identity
- runtime service instance
- runtime scope instance
- runtime fallback data

required input が欠けているなら、generation は修復出力ではなく failure しなければならない。

---

## Output Artifact Set

generation は isolated file ではなく、artifact set を生成する。

artifact set には次が含まれうる。

- VerifiedKernelPlan root artifact
- KernelPlanHeader
- ArtifactSetManifest
- ServiceGraphPlan
- ScopeGraphPlan
- LifecyclePlan
- CommandCatalogPlan
- ValueSchemaPlan
- ValueInitPlan
- DynamicEvaluationPlan
- ReactiveEvaluationPlan
- RuntimeQueryPlan
- KernelDebugMap
- generated C# files
- generated runtime assets
- generation report
- validation report

optional output には次が含まれうる。

- editor diagnostics cache
- migration review note
- non-runtime inspection artifact

runtime は partial artifact set を実行してはならない。

required artifact が 1 つでも欠ける、stale である、hash 不一致であるなら、artifact set 全体が無効である。

---

## Pre-Generation Validation

pre-generation validation は runtime projection を作る前に走る。

少なくとも次を確認しなければならない。

- KernelIR format version の互換性
- required root node の存在
- duplicate ID の不在
- required owner module の存在
- required source location の存在
- unresolved authoring alias の不在
- unresolved command authoring key が runtime-facing identity path に残っていないこと
- unresolved stable value key が runtime-facing identity path に残っていないこと
- profile availability 宣言が妥当であること
- required module dependency shape がすでに validation gate を通過していること
- forbidden legacy leakage が validated input state に存在しないこと

03 は dependency validation algorithm 自体は所有しない。
ただし、required validation gate が input state を受理するまでは generation を始めてはならない。

---

## KernelIR Normalization Gate

KernelIR は projection generation の前に完全に normalized でなければならない。

未解決のまま残してはいけない data には次が含まれる。

- command authoring key を runtime dispatch identity として使うこと
- stable value key を runtime value reference として使うこと
- transform hierarchy reference を runtime parent source として使うこと
- service type name を唯一の service identity とすること
- unresolved module alias
- runtime-only fallback ID

projection generation は、足りない normalization を補って完了したことにしてはならない。
KernelIR が normalized でないなら、generation は失敗しなければならない。

---

## Projection Generation

projection generation は、validated KernelIR から runtime-specific な plan view を作る。

必要な projection は次のとおり。

- ServiceGraphPlan
- ScopeGraphPlan
- LifecyclePlan
- CommandCatalogPlan
- ValueSchemaPlan
- ValueInitPlan
- DynamicEvaluationPlan
- ReactiveEvaluationPlan
- RuntimeQueryPlan
- DebugMap source data

projection generation は、次のような補助データを導いてよい。

- runtime layout index
- lookup table
- sorted array
- compact runtime metadata
- artifact-local header / manifest

projection generation は、KernelIR に存在しない structural identity を作ってはならない。

禁止される projection 行動:

- 新しい `ServiceId` を発明する
- 新しい `CommandTypeId` を発明する
- 新しい `ValueKeyId` を発明する
- 新しい `ScopeAuthoringId` を発明する
- 新しい `RuntimeQueryId` を発明する
- 暗黙の lifecycle step を足す
- hidden fallback dependency を足す

projection generation は次を保持しなければならない。

- owner module 情報
- source provenance
- profile availability
- identity domain boundary
- downstream handoff に必要な validated dependency reference

---

## Post-Generation Validation

post-generation validation は、generated projection を KernelIR と、相互同士で照合する。

少なくとも次を確認しなければならない。

- 必要な `ServiceIR` がすべて `ServiceGraphPlan` に反映されている
- 必要な `CommandIR` がすべて `CommandCatalogPlan` に反映されている
- 必要な command payload schema reference が generated command metadata に反映されている
- 必要な `ValueKeyIR` がすべて `ValueSchemaPlan` に反映されている
- 必要な value initialization entry がすべて `ValueInitPlan` に反映されている
- 必要な dynamic evaluation entry がすべて `DynamicEvaluationPlan` に反映されている
- 必要な reactive evaluation entry がすべて `ReactiveEvaluationPlan` に反映されている
- 必要な `ScopeIR` reference がすべて `ScopeGraphPlan` に反映されている
- 必要な `RuntimeQueryIR` がすべて `RuntimeQueryPlan` に反映されている
- runtime-facing ID が required profile level に応じた DebugMap coverage を持っている
- projection ID space が KernelIR の ID space と一致している
- projection に KernelIR 未知の ID が含まれていない
- manifest coverage が required artifact kind をすべて含んでいる

post-generation validation は、公開前に cross-artifact inconsistency を拒否する gate である。

---

## Profile-Specific Generation

profile-specific generation は、validated input にすでにある explicit availability declaration を通じてのみ許可される。

同じ validated KernelIR でも、異なる profile に対して異なる verified artifact set を出してよいのは、次の条件を満たすときだけである。

- availability の違いが明示的に宣言されている
- その違いが profile-sensitive hash に反映されている
- その違いが diagnostics と report で追跡できる

profile-specific generation は次をしてはならない。

- runtime discovery で内容の有無を決める
- profile-only fallback ID を発明する
- Development / Test profile に必要な DebugMap coverage を黙って省く
- availability の判定に editor-only UI state を使う

除外した content は、manifest か generation report で明示的に省き、診断可能でなければならない。

---

## Plan Header と Artifact Header の意味論

`KernelPlanHeader` は、verified artifact set に対する最上位の互換契約である。

すべての generated artifact は、自分の kind に応じた header を持たなければならない。

最上位 header または artifact ごとの header が表現できるべきもの:

- PlanId
- ArtifactSetId
- ArtifactId
- ArtifactKind
- FormatVersion
- GeneratorVersion
- SourceHash
- RegistryHash
- ProfileHash
- GeneratedHash
- 必要に応じて DebugMapHash

00 で使う header field の意味:

- PlanId: logical plan target の安定 ID
- ArtifactSetId: 1 回の generation run が出した output set の安定 ID
- ArtifactId: set 内の 1 artifact の安定 ID
- ArtifactKind: projection / artifact の分類
- FormatVersion: generation format version
- GeneratorVersion: artifact を出した generator core の version
- SourceHash: 正規化・検証済み KernelIR source state の hash
- RegistryHash: plan に関係する registry-backed identity input の hash
- ProfileHash: plan に関係する profile-affecting configuration の hash
- GeneratedHash: plan または artifact の emitted semantic content の hash
- DebugMapHash: 必要時の generated debug map content の hash

header は generation run から導出できなければならず、非意味データに依存してはならない。
build time、local machine path、asset import order は trust input に使ってはならない。

必要な header を一貫して計算できないなら、その generation output は無効である。

---

## Artifact Consistency Model

generated artifact は 1 つの consistency unit として扱わなければならない。

artifact set が valid であるのは、少なくとも次を満たす場合である。

- すべての artifact が同じ PlanId を共有する
- すべての artifact が同じ ArtifactSetId を共有する
- すべての artifact が互換ある format version を共有する
- すべての artifact が同じ SourceHash から導かれている
- すべての artifact が同じ RegistryHash set から導かれている
- すべての artifact が同じ ProfileHash から導かれている
- DebugMap が runtime plan と同じ ID space に対応している
- generated C# が generated asset と同じ ID space に対応している
- ArtifactSetManifest がちょうど 1 つの互換な verified artifact set を参照している

runtime は異なる generation run の artifact を混ぜてはならない。

kernel pipeline は複数 artifact を作るが、それらは同じ source state を表していなければならない。

consistency を証明できないなら generation は失敗し、output は stale とみなす。

---

## Hash と Version のモデル

hash / version policy は、意味的な互換性を証明しなければならない。

hash は file timestamp ではなく、semantic compatibility を表す必要がある。

trust-relevant hash に含めるもの:

- validated KernelIR content
- module ID と version
- identity assignment
- projection に関係する dependency edge
- runtime ID に関係する registry content
- profile-affecting setting
- generator version と対応 format version
- projection に関係する generation setting

trust-relevant hash から除外するもの:

- generation timestamp
- absolute local path
- editor selection state
- foldout state
- 非意味的 formatting
- runtime instance identity

現在の compiled asset に見られる `buildTimestamp` パターンは diagnostic metadata にすぎない。
互換性の証明ではなく、trust boundary には入れてはならない。

plan header と artifact manifest は、source、registry、profile、DebugMap input が変わったときに不一致を表明できなければならない。

---

## Deterministic Generation Rules

同じ input、profile、generator version からは、意味的に等しい output を生成しなければならない。

generation は次に依存してはならない。

- Unity object の enumeration order
- reflection order
- dictionary iteration order
- file system enumeration order
- current time
- random value
- asset import timing
- local machine path

generated array と file は deterministic ordering を使わなければならない。

推奨される ordering priority:

1. artifact kind
2. owner module ID
3. stable runtime ID
4. stable name
5. source location

determinism は、reproducible hash、stable diff、CI parity のために必要である。

non-deterministic output が検出されたら、その generation run は output を黙認せず失敗しなければならない。

---

## Generated Code Policy

generated code は execution artifact であり、source of truth ではない。

generated code は次を満たさなければならない。

- kind に適した artifact header を含む
- generator version metadata を含む
- `SourceHash` metadata を含む
- manual edit が禁止である旨を示す
- deterministic ordering を使う
- 非意味的な差分を避ける
- machine-local path を埋め込まない

generated code に含めてよいもの:

- ID table
- strongly typed constant
- projection glue
- compile-time helper

generated code は手で編集してはならない。
manual edit は無効であり、validation で上書きまたは拒否されるべきである。

namespace や partial type の慣例は下位仕様か generator 側の関心事である。
03 が要求するのは、generated code が非権威であり、同じ verified artifact set に遡れることだけである。

---

## Generated Asset Policy

generated asset は execution artifact であり、source of truth ではない。

generated asset は次を満たさなければならない。

- kind に適した artifact header を含む
- `SourceHash` metadata を含む
- `FormatVersion` metadata を含む
- generated であることが分かる
- stale 検出可能である
- editor で inspect 可能である

generated asset に含めてよいもの:

- plan asset
- debug map asset
- registry projection asset
- manifest asset

generated runtime asset を authoritative data として手で変更してはならない。
変更は authoring input から始まり、artifact を再生成して反映される必要がある。

stable asset GUID や reference layout は下位仕様が所有する。
03 が要求するのは、traceability、staleness detection、verified artifact set との整合性である。

---

## DebugMap Generation 要件

generation は runtime-facing identity のための DebugMap data を作らなければならない。

required coverage は少なくとも次を含む。

- `ModuleId`
- `ServiceId`
- `CommandTypeId`
- `ValueKeyId`
- `ScopeAuthoringId`
- `ScopePlanId`
- `LifecycleStepId`
- `RuntimeQueryId`

各 DebugMap entry は次を含まなければならない。

- 数値または記号 ID
- stable debug name
- owner module
- source location
- profile availability
- artifact hash
- 必要なら legacy origin

DebugMap generation は、artifact set の他の部分と同じ `SourceHash` を共有しなければならない。

Development / Test profile で DebugMap coverage が不足しているのは generation failure である。
Release profile では、致命的 failure を stable error code と stable numeric / symbolic identifier で報告できる場合に限り、縮小された debug metadata を許可する。

DebugMap は、generation 時の provenance の代わりとして boot 時に再構築してはならない。

---

## Stale Artifact Detection

artifact が stale である条件:

- `SourceHash` が現在の validated KernelIR と一致しない
- `RegistryHash` が現在の registry input state と一致しない
- `ProfileHash` が選択中 profile と一致しない
- `GeneratorVersion` が互換ではない
- `FormatVersion` が互換ではない
- `DebugMapHash` が required DebugMap content と一致しない
- artifact set が incomplete である

staleness detection は artifact header、manifest data、current input digest から可能でなければならない。
runtime fallback behavior に頼ってはならない。

振る舞い:

- editor tooling は inspection のために stale artifact を表示してよい
- runtime boot は stale artifact を使ってはならない
- required artifact が stale なら CI は失敗しなければならない

---

## Editor / CLI / CI の parity

generation は次の環境で実行できなければならない。

- Unity Editor
- command line generation
- CI validation

generation core は Editor、CLI、CI のホストで共通でなければならない。

ホスト固有 wrapper は違ってよいが、generation semantics を変えてはならない。

同じ input なら、これらの環境は意味的に等しい artifact set を出さなければならない。

generation は次に依存してはならない。

- editor window state
- current selection
- unsaved UI state

Editor と CLI の output が異なるなら、その generation core は無効である。
CI が Editor result を再現できないなら、その generation は信頼できない。

---

## Incremental Generation Policy

incremental generation は、artifact consistency をなお証明できる場合にのみ許可される。

consistency を証明できないなら、full regeneration が必要である。

次の場合は full regeneration が必要になる。

- KernelIR format version が変わる
- ID assignment rule が変わる
- registry content が変わる
- profile が変わる
- generator version が互換性を失って変わる
- DebugMap format の期待が変わる
- dependency graph が projection に影響する形で変わる

incremental generation でも、artifact manifest と verification report は whole set に対して再構築または再検証しなければならない。

required output のどれかを再検証できないなら、incremental generation は中断し、full regeneration を要求しなければならない。

---

## Validation Handoff

03 が所有するのは generation-time の構造検査であり、完全な dependency validation model ではない。

generation-time checks には次を含む。

- 支持される KernelIR / plan format version
- required input completeness
- artifact set completeness
- hash compatibility
- DebugMap coverage completeness
- deterministic output verification
- manifest completeness

`04_DependencyValidationSpec.md` が所有するのは次である。

- dependency cycle 検出
- phase-aware dependency rule
- cross-graph dependency severity policy
- 詳細な dependency conflict 分類

`05_BootManifestAndProfileSpec.md` が所有するのは、verified artifact set を runtime start に選ぶかどうかの最終 boot-time policy である。

03 は、04 と 05 が provenance を再構成せずに動けるだけの構造と metadata を提供しなければならない。

---

## Failure Policy

generation failure は target artifact set を無効化しなければならない。

失敗した generation によって、古い artifact を current verified output として残してはならない。

generation failure 時には次を行う。

- validation report を出す
- 新しい artifact set を invalid とする
- current verified marker を更新しない
- transaction-safe publication が証明されない限り、runtime artifact set を部分更新しない
- 以前の valid artifact set は、current ではなく previous と明示されている場合にのみ保持する

required failure condition:

- stale input state
- unsupported format version
- required artifact input の欠落
- hash mismatch
- artifact set inconsistency
- non-deterministic output
- required DebugMap coverage の欠落
- generator version incompatibility
- manifest completeness の不正

generation failure は、空 asset、runtime discovery、runtime-only negative ID へ silent fallback してはならない。

一時的な staging output は inspection のために残してよいが、consistency check が成功するまでは信頼してはならない。

---

## Diagnostics 要件

generation diagnostics には次が含まれる必要がある。

- phase
- severity
- error code
- affected artifact
- 利用可能なら affected IR node
- owner module
- source location
- 利用可能なら suggested fix

推奨される severity の順序は次のとおり。

| Severity | 数値 | 意味 |
|---|---|---|
| Info | 10 | ブロックしない情報出力 |
| Warning | 20 | artifact set をまだ無効化しない逸脱 |
| Error | 30 | 現在の generation attempt を無効化する block issue |
| Fatal | 40 | generation を無効化し、trusted publication を防ぐ block issue |

03 は最終 runtime diagnostic catalog を所有しない。
ただし、generation failure と verification failure を説明するための最低限の data は所有する。

---

## Forbidden Patterns

target generation path で禁止されるもの:

- generated code を source of truth とみなすこと
- generated asset を source of truth とみなすこと
- unverified generated plan を実行すること
- partial artifact set を実行すること
- stale artifact を黙って使うこと
- projection generation 中に KernelIR normalization を完了させたことにすること
- generation 中に不足 ID を発明すること
- fallback で不足 stable key を解決すること
- generation conflict を last-write-wins で処理すること
- generation order に reflection order を使うこと
- editor UI state に依存すること
- generated runtime artifact を手で編集して authoritative data にすること
- compatibility proof として build timestamp を使うこと
- post-generation validation が成功する前に trusted artifact を更新すること

これらは見た目の問題ではなく、構造上の違反である。

---

## Legacy Migration Notes

legacy 由来の data は、明示的な migration metadata を通じてのみ KernelIR と generated artifact に変換してよい。

legacy conversion は runtime fallback を導入してはならない。

例:

- legacy command key は authoring metadata と `CommandTypeId` mapping になる
- legacy stable var key は `ValueKeyIR` metadata と generated `ValueKeyId` になる
- legacy installer registration は module contribution provenance になる
- legacy build callback は diagnostics または debug provenance metadata になる

legacy migration output は、他の contribution や input artifact と同じように検証しなければならない。

現行コードには部分的な前例があるが、target contract ではない。

- [GameStateCodeGenerator.cs](../../Game/Scripts/Flow/Editor/GameStateCodeGenerator.cs)、[MaterialFxPropertyCodeGenerator.cs](../../GameLib/Script/Shader/Core/MaterialFx/Editor/MaterialFxPropertyCodeGenerator.cs)、[RoomMapTileCodeGenerator.cs](../../GameLib/Script/Project/Scene/RoomMap/Editor/RoomMapTileCodeGenerator.cs) は domain-specific generation の例であり、統一された plan-generation system ではない。
- [FlowCompiler.cs](../../GameLib/Script/Project/Flow/Compiler/FlowCompiler.cs) と [FlowProgramAssetSO.cs](../../GameLib/Script/Project/Flow/Core/FlowProgramAssetSO.cs) は、1 つの domain について source-hash と build-timestamp パターンを示すが、まだ全体の artifact consistency model は成立していない。
- [VarKeyRegistryLocator.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs) と [VarIdResolver.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs) は、generation と trust boundary が runtime ID や空 asset を発明するのではなく、fail closed でなければならない理由を示している。

target architecture はこれらの system の考え方を再利用してよいが、fallback behavior を新しい契約として再利用してはならない。

---

## Examples

### Example 1: Command Artifact Set

CameraModule が CameraShake command を contribution する。

generation の結果として出るもの:

- `CommandIR` entry
- `CommandCatalogPlan` entry
- payload schema metadata
- executor reference metadata
- DebugMap entry
- generation report における diagnostics mapping

もし DebugMap entry が欠けていれば、Development / Test profile では artifact set は invalid である。

### Example 2: Stale Value Registry

generation 後に ValueKey registry が変わる。

期待される結果:

- RegistryHash mismatch
- generated `ValueSchemaPlan` を stale として扱う
- runtime boot を止める
- diagnostics が変化した registry input を指し示す

### Example 3: Partial Generation Failure

ServiceGraphPlan は成功したが、CommandCatalogPlan が失敗した。

期待される結果:

- artifact set 全体が invalid
- current verified marker は更新されない
- previous verified set は archive として残してよいが、current と誤表示してはならない

---

## 受け入れ条件

03 が完成していると見なす条件は次のとおり。

- VerifiedKernelPlan の定義
- generation pipeline stage
- input artifact 要件
- output artifact set 要件
- pre-generation validation gate
- KernelIR normalization gate
- post-generation validation gate
- artifact consistency model
- plan / artifact header semantics
- hash / version model
- deterministic generation rule
- generated code policy
- generated asset policy
- DebugMap generation 要件
- stale artifact detection
- profile-specific generation 制約
- Editor / CLI / CI parity 要件
- incremental generation policy
- failure policy
- diagnostics 要件
- forbidden pattern

runtime storage 詳細、dependency validation algorithm 詳細、BootManifest schema 詳細が 03 に入り込んだら未完成である。

## テストケース

| テストケース | 目的 | 検証 |
|---|---|---|
| TC-03-01 | `GeneratedPlan` が自動的に `VerifiedKernelPlan` になるわけではないことを確認する。 | VerifiedPlan Definition と Pre-Generation Validation の節で、runtime use の前に validated input と完了済み gate を要求していること。 |
| TC-03-02 | artifact set が all-or-nothing の consistency unit であることを確認する。 | Output Artifact Set と Artifact Consistency Model の節で、同一 PlanId / ArtifactSetId / trust hash を持つ完全な set を要求していること。 |
| TC-03-03 | normalization が projection generation の前に完了し、generation が deterministic であることを確認する。 | KernelIR Normalization Gate と Deterministic Generation Rules の節で、fallback normalization、time dependence、path dependence、order dependence を禁止していること。 |
| TC-03-04 | DebugMap と diagnostics が verification の必須要素であることを確認する。 | DebugMap Generation Requirements と Diagnostics Requirements の節で、provenance、owner module、source location、profile-aware coverage を要求していること。 |
| TC-03-05 | stale、partial、failed artifact が current のまま残れないことを確認する。 | Stale Artifact Detection、Incremental Generation Policy、Failure Policy の節で、stale / partial / failed output が current verified artifact になれないこと。 |
| TC-03-06 | Editor、CLI、CI が 1 つの trusted generation core を共有することを確認する。 | Editor / CLI / CI Parity の節で、ホスト間の semantic divergence と UI-state 依存を禁止していること。 |

---

## 最終見解

generated artifact は個別には信頼しない。
完全で、決定論的で、検証済みで、hash-compatible な artifact set だけが VerifiedKernelPlan になれる。

verified plan generation は便利機能ではなく、trust boundary である。

generation system は、runtime が触れる前に、verified artifact set が内部的に整合し、provenance を持ち、決定論的で、version-compatible であることを証明しなければならない。

runtime が実行できるのは verified input だけである。
editor、CLI、CI は、それらの input が信頼できることを証明しなければならない。
generated code と generated asset は projection であり、権威ではない。

03 は、stale、partial、あるいは silently repaired な output を verified plan と誤認させないために存在する。

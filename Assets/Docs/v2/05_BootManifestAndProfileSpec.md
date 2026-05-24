# Boot Manifest と Profile 仕様

## 文書ステータス

- 文書 ID: `05_BootManifestAndProfileSpec`
- 状態: Draft
- 役割: boot entry、boot input validation、KernelProfile policy、boot-time artifact acceptance rule を定義する
- 依存先:
  - [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md)
  - [01_KernelIRSpec.md](01_KernelIRSpec.md)
  - [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md)
  - [04_DependencyValidationSpec.md](04_DependencyValidationSpec.md)
- この仕様を基盤としている文書:
  - [06_ServiceGraphRuntimeSpec.md](06_ServiceGraphRuntimeSpec.md)
  - [07_ScopeGraphRuntimeSpec.md](07_ScopeGraphRuntimeSpec.md)
  - [08_LifecyclePlanSpec.md](08_LifecyclePlanSpec.md)
  - [11_DebugMapAndDiagnosticsSpec.md](11_DebugMapAndDiagnosticsSpec.md)
  - [12_UnityAuthoringBridgeSpec.md](12_UnityAuthoringBridgeSpec.md)
  - [13_LegacyCompatBoundarySpec.md](13_LegacyCompatBoundarySpec.md)
  - [14_PerformanceBudgetAndRuntimeRulesSpec.md](14_PerformanceBudgetAndRuntimeRulesSpec.md)
  - [15_TestAndValidationSpec.md](15_TestAndValidationSpec.md)

### 所有範囲

本仕様は、boot input acceptance、boot entry selection、KernelProfile policy、BootPolicy の振る舞い、boot-time failure boundary を所有する。

runtime subsystem の実装、runtime storage layout、dependency validation algorithm、generated artifact emission、最終的な Unity authoring schema は担当外である。

---

## 目的

本仕様は、target kernel がどの verified artifact set を選び、どのように検証し、boot するかを定義する。

あわせて、KernelProfile policy、BootPolicy の振る舞い、boot-time failure handling も定義する。

BootManifest は global settings dump ではない。
BootManifest は、小さく検証済みの entry point であり、互換性のある verified artifact set を 1 つと、KernelProfile policy を 1 つ選ぶためのものだ。

boot は kernel structure を discovery してはならない。
boot は verified された kernel structure を受け入れなければならない。

boot は欠落した kernel structure を修復してはならない。
boot は 1 つの verified kernel structure を受け入れるか、失敗しなければならない。

---

## 範囲

本仕様は次を定義する。

- `KernelBootManifest` の目的と責務境界
- boot input model
- verified artifact set reference rule
- `KernelProfile` の種類と policy matrix
- BootPolicy の振る舞い
- boot phase の順序
- boot validation gate
- boot-time runtime creation contract
- persistent root policy
- scene boundary と loading boundary の policy
- boot における discovery と fallback の禁止
- boot failure の振る舞い
- boot diagnostics 要件
- stale artifact の扱い
- BootManifest のサイズ制御
- boot における legacy compatibility boundary
- boot 関連 performance direction
- boot 関連 test case

---

## 非目標

本仕様は次を定義しない。

- 最終的な ServiceGraph runtime storage
- 最終的な ScopeGraph runtime storage
- 最終的な CommandCatalog runtime lookup
- 最終的な ValueStore runtime storage
- 最終的な DebugMap asset layout
- 最終的な Unity authoring component schema
- scene transition algorithm の詳細
- loading screen の見た目の実装

本仕様は BootManifest を global settings registry にしてはならない。

---

## 他仕様との関係

| 仕様 | 関係 |
|---|---|
| [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md) | BootManifest を大きな設定ダンプではなく、小さな boot input reference として定義する |
| [01_KernelIRSpec.md](01_KernelIRSpec.md) | boot acceptance が消費する typed identity domain と hash-relevant source model を提供する |
| [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md) | boot が消費する verified artifact set、plan header、DebugMap provenance を生成する |
| [04_DependencyValidationSpec.md](04_DependencyValidationSpec.md) | boot が artifact set を受け入れる前に成功していなければならない dependency validation gate を定義する |
| 06_ServiceGraphRuntimeSpec.md | boot は validated service projection からのみ必要な service runtime state を作る |
| 07_ScopeGraphRuntimeSpec.md | boot は validated scope projection からのみ必要な root scope を作る |
| 08_LifecyclePlanSpec.md | boot は validated lifecycle plan からのみ boot lifecycle phase を実行できる |
| 11_DebugMapAndDiagnosticsSpec.md | boot diagnostics と failure reporting は DebugMap coverage と stable diagnostics contract に依存する |
| 12_UnityAuthoringBridgeSpec.md | manifest production に流れる authoring asset や reference を定義できるが、runtime boot discovery は定義しない |
| 13_LegacyCompatBoundarySpec.md | legacy boot bridge が存在するかどうかと、その可視範囲を定義する |
| 14_PerformanceBudgetAndRuntimeRulesSpec.md | ここで参照する measurable boot marker と budget を定義する |
| 15_TestAndValidationSpec.md | この boot acceptance contract を executable test と CI coverage に変換する |

05 は、verified artifact と live runtime boot の間の acceptance boundary である。

---

## Assembly Definition と Compile Boundary の期待値

verified boot policy の想定配置先は `GameLib.Kernel.Boot` である。
Unity-facing な boot entry glue は `GameLib.Kernel.Boot.Unity` に置く。
詳細な dependency matrix は [17_AssemblyDefinitionAndCompileBoundarySpec.md](17_AssemblyDefinitionAndCompileBoundarySpec.md) が管理する。

05 に対する必須の compile-boundary ルールは次のとおり。

- `GameLib.Kernel.Boot` は feature assembly、legacy assembly、authoring extraction assembly と分離する
- boot core は Unity 非依存のまま維持し、`noEngineReferences: true` を使うべきである
- `GameLib.Kernel.Boot.Unity` は Unity 固有の boot asset、startup hook、engine glue の合法な配置先である
- boot acceptance logic は scene discovery helper、feature installer、legacy fallback service に依存してはならない

verified boot に feature back-reference や Unity scene traversal が必要なら、05 の boundary は破れている。

---

## 現行の boot 観測

現行の boot 観測は、source code、migration note、profiling evidence に遡れなければならない。

### 観測のトレーサビリティ

| 観測 | 根拠の種類 | Boot 圧力 |
|---|---|---|
| Project root creation が `BeforeSceneLoad` singleton discovery に結びついている | Source | 05, 07 |
| Global root creation が project-root の自動生成、scene search、resource fallback に結びついている | Source | 05, 07, 13 |
| Loading presentation boot が `SceneLifetimeScope` instance を scan し、duplicate cleanup を行っている | Source | 05, 07 |
| Loading presentation の parent 選択が runtime で Global / Platform / Project root を探している | Source | 05, 07 |
| Boot repair が `Resources.Load` や `new GameObject` fallback で起こりうる | Source | 05, 13 |
| Persistent root が verified boot entry contract の外側で `DontDestroyOnLoad` singleton 挙動を使っている | Source | 05, 07 |

### 代表的なアンカー

- [ProjectLifetimeScope.cs](../../GameLib/Script/Project/LTS/ProjectLifetimeScope.cs) - `BeforeSceneLoad` boot entry、scene-wide root search、resource fallback、default `GameObject` creation
- [GlobalLifetimeScope.cs](../../GameLib/Script/Project/Global/LTS/GlobalLifetimeScope.cs) - project-first boot coupling、global root search、resource fallback、default `GameObject` creation
- [LoadingScreenService.cs](../../GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs) - loading scope discovery、duplicate cleanup、persistent parent search

### 現行の不足点

現行コードベースには、05 が target architecture から取り除くべき boot 挙動がまだ残っている。

- boot truth が runtime startup 中の scene state から推測されている
- missing boot root が fallback prefab load や default object creation で修復できてしまう
- loading presentation が scene search と persistent-parent search に依存している
- duplicate root を rejection ではなく cleanup で処理できてしまう
- boot input acceptance が 1 つの verified artifact set と 1 つの selected profile を中心に中央集約されていない

---

## Boot Authority

05 は、boot input acceptance に対する fail-closed な権威である。

BootManifest は verified input を選ぶのであって、truth を作るのではない。

03 は artifact emission、hash assembly、artifact-set consistency を所有する。
04 は dependency validation と dependency failure classification を所有する。
05 は、1 つの artifact set、1 つの selected profile、1 つの boot policy がすべての acceptance gate を満たす場合にだけ boot を進めてよい、という rule を所有する。

boot は host behavior、runtime search、legacy repair、fallback creation を通じて invalid input を valid にしてはならない。

---

## BootManifest の定義

`KernelBootManifest` は target kernel boot の明示的な entry point である。

参照するもの:

- selected `KernelProfile`
- 1 つの verified artifact set
- boot policy
- diagnostics policy
- 必要に応じた editor-only source metadata

```csharp
public sealed class KernelBootManifest : ScriptableObject
{
    public string ManifestId;
    public KernelProfileId ProfileId;
    public VerifiedArtifactSetRef ArtifactSet;
    public BootPolicyId BootPolicyId;
    public BootDiagnosticsPolicy DiagnosticsPolicy;
}
```

このスケッチは説明用であり、serialized API を最終確定するものではない。

BootManifest は entry selector である。
KernelIR、VerifiedKernelPlan、ServiceGraphPlan、ScopeGraphPlan、DebugMap の代替ではない。

---

## BootManifest の責務境界

BootManifest に含めてよいもの:

- manifest identity
- selected profile reference
- verified artifact set reference
- boot policy reference
- diagnostics policy reference
- editor-only source metadata

BootManifest に含めてはならないもの:

- full service list
- full command list
- full value key list
- full lifecycle step list
- full scope graph
- direct command executor definition
- direct runtime service instance
- runtime fallback path
- scene search rule

BootManifest が generated graph data を複製し始めたら、責務境界を越えており、設計上無効である。

---

## Boot Input Model

boot input は次で構成される。

- `KernelBootManifest`
- 1 つの verified artifact set
- `KernelDebugMap`
- 必要に応じた registry / identity projection asset
- selected `KernelProfile`
- selected `BootPolicy`

すべての boot input は versioned かつ hash-compatible でなければならない。

version のない boot input は target runtime に受理されてはならない。

boot は runtime 中に見つかった loose な file collection を受け付けない。
1 つの verified かつ互換な input set を受け付ける。

---

## Verified Artifact Set Reference

BootManifest は、ちょうど 1 つの verified artifact set を参照する。

有効な reference は、set identity と互換性を証明できるだけの情報を持たなければならない。

- `ArtifactSetId`
- `PlanId`
- `KernelIRHash`
- registry または identity projection asset が存在するなら `RegistryHash`
- `ProfileHash`
- DebugMap が必要なら `DebugMapHash`
- `FormatVersion`

BootManifest は、同じ verified artifact set と同じ compatibility proof に属していない個別 artifact を独立に参照してはならない。

boot は異なる generation run の artifact を混ぜて truth を組み立ててはならない。

---

## KernelProfile の定義

`KernelProfile` は boot-time policy と validation strictness を定義する。

必須 profile:

- Development
- Release
- Test

```csharp
public enum KernelProfileKind
{
    Development = 10,
    Release = 20,
    Test = 30,
}
```

KernelProfile は diagnostics detail、legacy allowance、strictness reporting に影響する。
失われた boot structure を発明したり修復したりしてはならない。

---

## Profile Policy Matrix

| Policy | Development | Release | Test |
|---|---|---|---|
| Stale artifact | Error で boot block | Fatal で boot block | Fatal で boot block |
| Missing DebugMap | Error | fatal diagnostics を出せないなら Error | Fatal |
| Legacy bridge | 13 が明示的に許可するなら Warning | 13 が明示しない限り Forbidden | test policy に応じて Error または Fatal |
| Diagnostics detail | Full | Minimal required | Full captured |
| Runtime assertions | Enabled | Minimal | Enabled |
| Validation strictness | Strict | Strict | Maximum practical |
| Generated mismatch | Boot block | Boot block | Boot block |
| Fallback | 13 が許可する bounded dev-only bridge を除き禁止 | 禁止 | 禁止 |

profile は severity と diagnostics detail を変えてよい。
profile は invalid boot input を silently valid に変えてはならない。

---

## BootPolicy の定義

`BootPolicy` は、runtime が boot validation result にどう反応するかを定義する。

BootPolicy が定義しうるもの:

- failure boundary behavior
- diagnostics emission mode
- previous verified artifact の扱い
- editor-only inspection mode
- test deterministic mode

BootPolicy は fallback discovery behavior を定義してはならない。

BootPolicy は observability と enforcement の見え方を調整してよい。
しかし、unverified または incompatible な input から boot する権限は与えられない。

---

## Boot Phase Model

boot phase は決定論的で、順序付きである。

1. BootManifest reference を読み込む
2. 参照された artifact set を読み込む
3. artifact header を検証する
4. profile compatibility を検証する
5. DebugMap の要件を検証する
6. dependency validation status を検証する
7. KernelRuntime shell を作る
8. 必要な ServiceGraph runtime state を作る
9. 必要な root scope を作る
10. boot lifecycle step を実行する
11. kernel boot completed とマークする

boot phase は決定論的でなければならない。
boot phase は broad runtime discovery を行ってはならない。

boot はこれらの phase の中で normalization、generation、dependency repair を再実行してはならない。

---

## Boot Validation Gates

boot は少なくとも次を検証しなければならない。

- `ManifestId` が存在する
- selected profile が存在する
- artifact set reference が存在する
- artifact set が complete である
- artifact header が互換である
- `KernelIRHash` が一致する
- 必要に応じて `RegistryHash` が一致する
- `ProfileHash` が一致する
- 必要な場合 `DebugMapHash` が一致する
- dependency validation status が selected profile に対して acceptable である
- 必要な root scope projection が存在する
- 必要な root service が存在する

required gate のどれかが失敗したら、boot は runtime subsystem creation に進んではならない。

boot acceptance gate は optional な host convenience ではない。
target runtime contract の一部である。

---

## Boot Runtime Creation Contract

`KernelRuntime` は verified boot input からのみ生成できる。

runtime creation は次をしてはならない。

- scene を検索して kernel root を探す
- 既存の `GameObject` state から root を推測する
- required input のために fallback prefab を読み込む
- missing root service を default から作る
- missing root scope を default から作る

runtime creation は次をしてよい。

- KernelRuntime shell state を確保する
- verified artifact set または verified boot input の一部である明示的に参照された boot prefab を instantiate する
- validated scope projection で定義された required root scope を作る
- validated service projection で定義された essential service を作る

boot は verified input から runtime state を作る。
kernel structure を discovery したり repair したりしない。

---

## Persistent Root Policy

persistent root は verified boot input によって定義されなければならない。

例:

- application root
- project root
- global root
- persistent presentation root
- loading presentation root

persistent root creation は、scene-wide search、duplicate cleanup、fallback instantiation に頼ってはならない。

duplicate persistent root は validation error か boot error である。
runtime で先に見つけた方を残して他を消すことで解決してはならない。

host に既に persistent root があるなら、その host object は verified boot input set で明示的に参照されていなければならない。
boot 中の surprise discovery にしてはならない。

---

## Scene Boundary と Loading Policy

boot は persistent kernel runtime と scene-specific runtime の boundary を定義しなければならない。

loading presentation は runtime で `SceneLifetimeScope` instance を discovery してはならない。

loading-related root は次のいずれかでなければならない。

- verified boot input で定義された persistent presentation root
- verified scene transition plan で定義された scene-flow service output
- validated scope projection で定義された explicit scene scope

loading は、scene 内の Global / Platform / Project object を検索して parent を解決してはならない。

scene boundary policy は、persistent boot state と scene-local runtime state の区別を保たなければならない。
runtime hierarchy discovery を composition mechanism として再導入してはならない。

---

## Fallback と Discovery の禁止

target boot では次を使ってはならない。

- kernel root を探すための `FindObjectsByType`
- boot module を discovery するための `GetComponentsInChildren`
- kernel parentage を推測するための transform-parent traversal
- required boot input のための `Resources.Load` fallback
- missing root scope に対する `new GameObject` fallback
- 先に見つけたものを残して残りを消す duplicate cleanup を解決策として使うこと
- runtime stable-key fallback
- legacy resolver fallback

例外が必要なら、下位仕様で bounded、profile-scoped、diagnostic-visible、migration-limited として定義しなければならない。

1 回しか startup で起きないというだけでは例外として有効ではない。

---

## Boot Failure Policy

required boot input が invalid の場合、boot failure は runtime subsystem execution の前で止めなければならない。

required failure category:

- `ManifestMissing`
- `ArtifactSetMissing`
- `ArtifactSetIncomplete`
- `HashMismatch`
- `VersionMismatch`
- `ProfileMismatch`
- `DebugMapMissing`
- `DependencyValidationFailed`
- `RequiredRootMissing`
- `LegacyForbidden`

boot failure boundary は whole-kernel boot である。
boot に失敗したら、部分初期化された `KernelRuntime` を valid として露出してはならない。

boot は diagnostics 付きで explicit に失敗しなければならない。
degraded success path の裏で継続してはならない。

---

## Diagnostics と DebugMap の要件

boot diagnostics には次を含める。

- stable error code
- selected profile
- manifest ID
- artifact set ID
- failure した artifact または gate
- 必要なら expected hash
- 必要なら actual hash
- 利用可能なら source location
- suggested fix

DebugMap が欠けていても、boot diagnostics は stable numeric ID と stable error code を出力しなければならない。

Development / Test profile では、DebugMap 欠落は error か fatal である。
Release profile では、fatal diagnostics が stable かつ解釈可能である場合に限り、縮小された debug metadata を許可する。

boot diagnostics は、runtime reproduction を必要とせずに boot が拒否された理由を説明しなければならない。

---

## 環境差分

boot 挙動は host environment によって違ってよいが、validity rule は静かに弱めてはならない。

Editor は次を提供してよい。

- artifact inspection
- regeneration prompt
- stale artifact 表示
- 詳細な source navigation

Release player は次を提供してよい。

- compact diagnostics
- minimal required DebugMap
- regeneration prompt なし

Test host は次を提供しなければならない。

- deterministic boot
- captured diagnostics
- strict artifact validation

host 差分は tooling behavior を変えてよい。
invalid boot input を受理するかどうかは変えてはならない。

---

## Stale または Missing Artifact の扱い

stale artifact は runtime boot に使ってはならない。

次の場合、artifact は stale である。

- `KernelIRHash` が不一致
- `RegistryHash` が不一致
- `ProfileHash` が不一致
- `DebugMapHash` が不一致
- generator version が互換でない
- artifact set が incomplete

Editor は inspection のために stale artifact を見せてよい。
Editor は stale artifact を current verified input としてマークしてはならない。

required artifact の欠落は discovery opportunity ではなく boot failure である。

---

## BootManifest Size Control

BootManifest は小さいまま維持しなければならない。

BootManifest が参照すべきもの:

- profile
- artifact set
- boot policy
- diagnostics policy

BootManifest は次の所有データを複製してはならない。

- KernelIR
- VerifiedKernelPlan
- ServiceGraphPlan または同等の service projection
- ScopeGraphPlan または同等の scope projection
- CommandCatalogPlan
- ValueSchemaPlan
- DebugMap

validation は、generated artifact content を重複して持つ BootManifest field を検出しなければならない。

BootManifest が second registry になり始めたら、target architecture はすでに drift している。

---

## Boot における Legacy Compatibility Boundary

legacy boot compatibility は、13 が定義する explicit LegacyCompat boot bridge を通じてのみ許可される。

target boot core は次に依存してはならない。

- legacy `LifetimeScope` singleton creation
- legacy `RuntimeResolver` boot behavior
- legacy `CommandRunnerMB` registration discovery
- legacy Blackboard initialization
- legacy runtime value-ID fallback

Development は、許可された legacy boot bridge を warning として報告してよい。
Release は、未承認の legacy boot dependency を拒否しなければならない。

legacy boot behavior は、測定可能で、見える形で、削除可能なままでなければならない。

---

## Performance Budget 方向性

boot は少なくとも次の profiler marker を露出しなければならない。

- `KernelBoot.LoadManifest`
- `KernelBoot.LoadArtifactSet`
- `KernelBoot.ValidateHeaders`
- `KernelBoot.ValidateHashes`
- `KernelBoot.ValidateProfile`
- `KernelBoot.CreateRuntime`
- `KernelBoot.CreateEssentialServices`
- `KernelBoot.CreateRootScopes`
- `KernelBoot.RunBootLifecycle`

これらは最小必須 boot marker である。
Spec 14 が、この boot path を消費する full marker taxonomy、budget range、profile-specific cap、regression rule を定義する。

boot performance は、validation や DebugMap consistency check を飛ばすことで最適化してはならない。

command executor 数は structural metadata size を増やしてよい。
しかし、boot 中に全 command executor の eager construction を強制してはならない。

---

## Forbidden Patterns

target boot で禁止されるもの:

- unverified plan からの boot
- partial artifact set からの boot
- kernel root を探すための scene-wide search
- required boot input に対する `Resources.Load` fallback
- missing root に対する default `GameObject` creation
- runtime destruction による duplicate root cleanup
- persistent root を探すための transform parent search
- boot 中の legacy resolver fallback
- BootManifest に full runtime graph を格納すること
- validation を静かに弱める profile
- required validation failure の後も boot を続けること

禁止事項は、スタイルの問題ではなくアーキテクチャの失敗である。

---

## Test Case Model

各 boot / profile test case は次を定義しなければならない。

- Test ID
- Title
- BootManifest fixture
- ArtifactSet fixture
- Profile
- Expected boot result
- Expected diagnostics
- Expected failure boundary
- Notes

推奨 fixture 形式:

```md
### TC_BOOT_001_HashMismatchBlocksBoot

Input:
- BootManifest references ArtifactSet A
- ArtifactSet header KernelIRHash = X
- Registry projection reports KernelIRHash = Y

Profile:
- Development

Expected:
- BootResult: Failed
- Diagnostic: BOOT_HASH_MISMATCH
- Boundary: Whole kernel boot
```

この fixture 形式は、15 が intent を再定義せずに executable boot / CI test に変換できるようにするためにある。

---

## Required Test Cases

### A. Manifest Tests

#### TC_BOOT_MANIFEST_001_MissingManifest

入力:
- BootManifest reference が欠落している

期待値:
- Failed
- `BOOT_MANIFEST_MISSING`
- Boundary: Whole kernel boot

#### TC_BOOT_MANIFEST_002_ManifestReferencesMissingArtifactSet

入力:
- BootManifest は存在する
- ArtifactSet reference が欠落している

期待値:
- Failed
- `BOOT_ARTIFACT_SET_MISSING`

#### TC_BOOT_MANIFEST_003_ManifestStoresRuntimeGraphDirectly

入力:
- BootManifest が full ServiceGraph または CommandCatalog content を直接含んでいる

期待値:
- Failed
- `BOOT_MANIFEST_DUPLICATES_GENERATED_CONTENT`

### B. Artifact Set Tests

#### TC_BOOT_ARTIFACT_001_IncompleteArtifactSet

入力:
- Service projection は存在する
- Command catalog projection が存在しない
- ArtifactSet は current とマークされている

期待値:
- Failed
- `BOOT_ARTIFACT_SET_INCOMPLETE`

#### TC_BOOT_ARTIFACT_002_KernelIRHashMismatch

入力:
- BootManifest が artifact set を参照している
- ArtifactSet の `KernelIRHash` が plan header または required registry projection と一致しない

期待値:
- Failed
- `BOOT_KERNEL_IR_HASH_MISMATCH`

#### TC_BOOT_ARTIFACT_003_DebugMapHashMismatch

入力:
- Plan の `DebugMapHash` = A
- DebugMap asset hash = B

期待値:
- Failed
- `BOOT_DEBUGMAP_HASH_MISMATCH`

#### TC_BOOT_ARTIFACT_004_StaleArtifactBlocked

入力:
- ArtifactSet が互換のない generator version で生成されている

期待値:
- Failed
- `BOOT_ARTIFACT_STALE`

#### TC_BOOT_ARTIFACT_005_MixedArtifactSetRejected

入力:
- Service projection が 1 つの artifact set 由来
- Scope projection または DebugMap が別の artifact set 由来

期待値:
- Failed
- `BOOT_ARTIFACT_SET_MIXED_SOURCE_FORBIDDEN`

### C. Profile Tests

#### TC_BOOT_PROFILE_001_DevelopmentAllowsDetailedDiagnostics

Profile:
- Development

入力:
- optional な DebugMap detail は欠けているが、stable numeric diagnostics は利用可能である

期待値:
- policy により Failed または Warning のみ
- diagnostics に source location が利用可能なら含まれる

#### TC_BOOT_PROFILE_002_ReleaseRejectsLegacyFallback

Profile:
- Release

入力:
- Boot が legacy `RuntimeResolver` fallback を要求する

期待値:
- Failed
- `BOOT_LEGACY_FALLBACK_FORBIDDEN`

#### TC_BOOT_PROFILE_003_TestRequiresDeterministicBoot

Profile:
- Test

入力:
- BootPolicy が nondeterministic behavior を使っている

期待値:
- Failed
- `BOOT_TEST_NON_DETERMINISTIC_POLICY`

#### TC_BOOT_PROFILE_004_ProfileHashMismatch

入力:
- ArtifactSet が Development 向けに生成されている
- BootManifest が Release を選択している

期待値:
- Failed
- `BOOT_PROFILE_HASH_MISMATCH`

### D. Validation Gate Tests

#### TC_BOOT_GATE_001_DependencyValidationFailed

入力:
- ArtifactSet の dependency validation status が `Failed`

期待値:
- Failed
- `BOOT_DEPENDENCY_VALIDATION_FAILED`

#### TC_BOOT_GATE_002_MissingRequiredRootService

入力:
- Boot が `KernelDiagnosticsService` を必要とする
- validated service projection にそれが含まれていない

期待値:
- Failed
- `BOOT_REQUIRED_ROOT_SERVICE_MISSING`

#### TC_BOOT_GATE_003_MissingRequiredRootScope

入力:
- Boot が `ProjectRootScope` を必要とする
- validated scope projection にそれが含まれていない

期待値:
- Failed
- `BOOT_REQUIRED_ROOT_SCOPE_MISSING`

### E. Discovery and Fallback Prohibition Tests

#### TC_BOOT_DISCOVERY_001_FindObjectsByTypeForbidden

入力:
- Boot implementation が kernel root を探すために scene-wide search を試みる

期待値:
- Failed static analyzer または boot validation
- `BOOT_RUNTIME_DISCOVERY_FORBIDDEN`

#### TC_BOOT_DISCOVERY_002_ResourcesFallbackForbidden

入力:
- required BootManifest または root input が欠落している
- Boot が `Resources.Load` fallback を試みる

期待値:
- Failed
- `BOOT_RESOURCES_FALLBACK_FORBIDDEN`

#### TC_BOOT_DISCOVERY_003_DefaultGameObjectRootForbidden

入力:
- required root が欠落している
- Boot が `new GameObject` fallback を試みる

期待値:
- Failed
- `BOOT_DEFAULT_ROOT_CREATION_FORBIDDEN`

#### TC_BOOT_DISCOVERY_004_DuplicateRootCleanupForbidden

入力:
- duplicate persistent root が存在する
- Boot が先に見つけた方を残して残りを消す処理を試みる

期待値:
- Failed
- `BOOT_DUPLICATE_ROOT_CLEANUP_FORBIDDEN`

### F. Loading and Scene Boundary Tests

#### TC_BOOT_LOADING_001_LoadingSceneSearchForbidden

入力:
- loading presentation が runtime で `SceneLifetimeScope` instance を検索する

期待値:
- Failed
- `BOOT_LOADING_SCENE_SEARCH_FORBIDDEN`

#### TC_BOOT_LOADING_002_LoadingParentTransformSearchForbidden

入力:
- loading root の parent が Global または Project object の検索で解決される

期待値:
- Failed
- `BOOT_LOADING_PARENT_SEARCH_FORBIDDEN`

#### TC_BOOT_LOADING_003_LoadingRootDefinedByPlan

入力:
- verified boot input が `PersistentLoadingPresentationRoot` を定義している
- ArtifactSet は valid

期待値:
- Passed

### G. Performance and Marker Tests

#### TC_BOOT_PERF_001_BootMarkersExist

入力:
- valid boot

期待値:
- required boot profiler marker が emit される

#### TC_BOOT_PERF_002_BootDoesNotInstantiateAllCommandExecutors

入力:
- command catalog に多数の command がある
- Boot は catalog metadata だけを必要とする

期待値:
- Passed
- すべての command executor の eager construction は起こらない

---

## 受け入れ条件

05 が完成していると見なす条件は次のとおり。

- BootManifest の目的と責務境界が定義されている
- boot input model が定義されている
- verified artifact set reference rule が定義されている
- KernelProfile の種類と policy matrix が定義されている
- BootPolicy が定義されている
- boot phase model が定義されている
- boot validation gate が定義されている
- runtime creation contract が定義されている
- persistent root policy が定義されている
- scene / loading boundary policy が定義されている
- fallback / discovery の禁止が定義されている
- boot failure policy が定義されている
- diagnostics / DebugMap の要件が定義されている
- environment difference rule が定義されている
- stale artifact handling が定義されている
- BootManifest size control が定義されている
- legacy boot boundary が定義されている
- performance marker direction が定義されている
- forbidden pattern が定義されている
- boot / profile test case model が定義されている
- required boot / profile test case が定義されている

boot が runtime discovery、fallback root creation、partial artifact acceptance に依存したままなら、この仕様は未完成である。

---

## テストケース

| テストケース | 目的 | 検証 |
|---|---|---|
| TC-05-01 | BootManifest が小さな verified entry selector のままであることを確認する。 | BootManifest の定義と size-control の節で、runtime graph の複製を禁止していること。 |
| TC-05-02 | boot が互換な artifact set をちょうど 1 つ受け入れるか、失敗することを確認する。 | boot input model、artifact-set reference、validation gate の節で、partial または mixed artifact を拒否していること。 |
| TC-05-03 | profile が truth ではなく severity を変えることを確認する。 | profile matrix と environment differences の節で、invalid input を valid にしないこと。 |
| TC-05-04 | boot runtime creation が scene discovery や fallback creation を使わないことを確認する。 | runtime creation contract と fallback prohibition の節で、required input に対する `FindObjectsByType`、`Resources.Load`、default root creation を禁じていること。 |
| TC-05-05 | loading presentation が explicit な boot / scene-flow contract の内側に留まることを確認する。 | scene boundary と loading policy の節で、loading scope や parent selection の runtime search を拒否していること。 |
| TC-05-06 | boot failure が partial kernel exposure を止めることを確認する。 | failure policy と validation gate の節で、acceptance failure 時に runtime subsystem execution の前で止まること。 |

---

## 最終見解

BootManifest は verified input を選ぶのであって、truth を作るのではない。

boot は欠落した kernel structure を修復しない。
boot は 1 つの verified kernel structure を受け入れるか、失敗する。

target boot は discovery path ではない。
それは、validated artifact set から live runtime へ進む explicit acceptance path である。

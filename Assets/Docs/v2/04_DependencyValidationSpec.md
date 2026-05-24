# 依存関係検証仕様

## 文書ステータス

- 文書 ID: `04_DependencyValidationSpec`
- 状態: Draft
- 役割: KernelIR と生成済み runtime projection をまたぐ依存関係正当性の契約
- 依存先:
  - [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md)
  - [01_KernelIRSpec.md](01_KernelIRSpec.md)
  - [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md)
  - [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md)
- この仕様を基盤としている文書:
  - [05_BootManifestAndProfileSpec.md](05_BootManifestAndProfileSpec.md)
  - [06_ServiceGraphRuntimeSpec.md](06_ServiceGraphRuntimeSpec.md)
  - [07_ScopeGraphRuntimeSpec.md](07_ScopeGraphRuntimeSpec.md)
  - [08_LifecyclePlanSpec.md](08_LifecyclePlanSpec.md)
  - [09_CommandCatalogRuntimeSpec.md](09_CommandCatalogRuntimeSpec.md)
  - [10_ValueSchemaAndStoreSpec.md](10_ValueSchemaAndStoreSpec.md)
  - [10_2_DynamicValueEvaluationSpec.md](10_2_DynamicValueEvaluationSpec.md)
  - [11_DebugMapAndDiagnosticsSpec.md](11_DebugMapAndDiagnosticsSpec.md)
  - [12_UnityAuthoringBridgeSpec.md](12_UnityAuthoringBridgeSpec.md)
  - [13_LegacyCompatBoundarySpec.md](13_LegacyCompatBoundarySpec.md)
  - [14_PerformanceBudgetAndRuntimeRulesSpec.md](14_PerformanceBudgetAndRuntimeRulesSpec.md)
  - [15_TestAndValidationSpec.md](15_TestAndValidationSpec.md)

### 所有範囲

04 は、dependency correctness、phase-aware な検証意味論、severity policy、diagnostics 要件、および validation result model を所有する。

KernelIR のレイアウト、generation algorithm、runtime 実行挙動、runtime storage layout、artifact manifest 形式は担当外である。

---

## 目的

本仕様は、KernelIR とその生成済み projection が runtime execution input になる前に、依存関係の正しさをどのように検証するかを定義する。

依存関係検証は、無効な KernelIR が runtime plan に変わるのを防ぐ gate である。

無効な依存関係 graph は、runtime plan 実行の前に失敗しなければならない。

runtime になって初めて見つかる依存関係は、下位仕様がそれを verified runtime query として明示していない限り、検証失敗である。

この仕様は次の失敗モードを防ぐために存在する。

- 必須 dependency の欠落が boot または acquire まで届くこと
- invalid phase に cycle が存在すること
- profile 固有の dependency gap が generation 後に見つかること
- optional dependency が silent fallback に落ちること
- runtime query の意味論が service resolution に漏れること
- legacy compatibility が target kernel core に漏れること
- generated projection が未知の identity を導入したり provenance を失ったりすること

---

## 範囲

本仕様は次を定義する。

- dependency validation の input / output
- dependency identity の検証ルール
- phase-aware validation
- validation における dependency strength の解釈
- validation severity policy
- module dependency validation
- service dependency validation
- scope dependency validation
- lifecycle dependency validation
- command dependency validation
- value dependency validation
- runtime query dependency validation
- diagnostics / debug coverage validation
- profile-aware validation
- optional dependency policy
- cycle detection policy
- conflict / duplicate validation
- forbidden dependency pattern
- legacy leakage validation
- validation diagnostics 要件
- validation test case 形式

本仕様は、KernelIR node、dependency edge、generated artifact、DebugMap asset、runtime subsystem API の canonical な意味や wire shape を再定義しない。

---

## 他仕様との関係

| 仕様 | 関係 |
|---|---|
| [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md) | validation を trust boundary の一部として定義し、target kernel で silent fallback を禁じる |
| [01_KernelIRSpec.md](01_KernelIRSpec.md) | typed IR identity domain、dependency edge 表現、lifecycle data、runtime query data を定義し、本仕様が解釈する |
| [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md) | 正規化出力が受理される前に valid でなければならない宣言的 contribution input と dependency 宣言を定義する |
| [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md) | 本仕様が所有する dependency validation gate を中心に、generation、staging、artifact consistency check を定義する |
| 05_BootManifestAndProfileSpec.md | 選択された profile に対して dependency validation を通過した input だけを消費する |
| 06_ServiceGraphRuntimeSpec.md | validated された service dependency と lifetime direction rule を消費する |
| 07_ScopeGraphRuntimeSpec.md | validated された scope parentage、runtime query rule、explicit graph boundary を消費する |
| 08_LifecyclePlanSpec.md | validated された lifecycle participation、ordering、phase dependency を消費する |
| 09_CommandCatalogRuntimeSpec.md | validated された command identity、executor、payload、runtime query dependency を消費する |
| 10_ValueSchemaAndStoreSpec.md | validated された value schema、init、save、value-state boundary rule を消費する |
| 10_2_DynamicValueEvaluationSpec.md | validated された dynamic / reactive evaluation dependency、phase legality、invalidation declaration を消費する |
| 11_DebugMapAndDiagnosticsSpec.md | ここで定義される validation diagnostics、source provenance、debug coverage 要件を消費する |
| 12_UnityAuthoringBridgeSpec.md | 正規化された dependency 宣言が受理前に validation を通過しなければならない authoring input を生成する |
| 13_LegacyCompatBoundarySpec.md | legacy usage が観測可能で制御可能な唯一の合法 boundary を定義する |
| 15_TestAndValidationSpec.md | ここで定義される validation fixture と required case を実行可能な test と CI gate に変換する。validation 意味論、severity の意味、diagnostics code の意図は再定義しない |

04 は、宣言的アーキテクチャと runtime execution を分ける dependency firewall である。

---

## Assembly Definition と Compile Boundary の期待値

依存関係検証の想定配置先は `GameLib.Kernel.Validation` である。
詳細な dependency matrix は [17_AssemblyDefinitionAndCompileBoundarySpec.md](17_AssemblyDefinitionAndCompileBoundarySpec.md) が管理する。

04 に対する必須の compile-boundary ルールは次のとおり。

- `GameLib.Kernel.Validation` は runtime mutation code と runtime subsystem 実装から分離する
- validation core は Unity 非依存のまま維持し、`noEngineReferences: true` を使うべきである
- validation helper は `GameLib.Tests.*` や authoring editor assembly で使ってよいが、production validation core は test / editor package を参照してはならない
- legacy repair code、fallback lookup helper、runtime discovery utility を validation assembly に引き込んではならない

validation correctness が runtime side effect や Editor-only API に依存するなら、04 の boundary は破れている。

---

## 現行の検証観測

現行の依存関係検証観測は、source code、design review note、migration evidence に遡れなければならない。

### 観測のトレーサビリティ

| 観測 | 根拠の種類 | 検証圧力 |
|---|---|---|
| lifecycle と tick 参加を runtime registration の scan で集められる | Source | 04, 08 |
| runtime handler dispatch を validation 前ではなく resolver build 後に確定できる | Source | 04, 06, 08 |
| command executor と lifecycle service を build の副作用として一括登録できる | Source | 04, 09 |
| 欠落した value registry entry を runtime-only negative ID にフォールバックできる | Source | 04, 10 |
| 欠落した value registry asset を `Resources.Load` または空の runtime asset にフォールバックできる | Source | 04, 05, 10 |
| runtime identity lookup を boot 後に registry traversal で満たせる | Source | 04, 07 |

### 代表的なアンカー

- [RuntimeResolverHub.cs](../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs) - registration indexing、`CollectHandlers<THandler>()`、`CollectAll(...)`、`RuntimeAcquireReleaseDispatcher`
- [RuntimeLifetimeScope.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs) - build 中の feature installation、resolver construction、runtime registration 後の handler extraction
- [CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs) - scope kind に結びついた bulk executor / lifecycle registration pattern
- [VarIdResolver.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs) - stable key が見つからないときの runtime-only negative ID fallback
- [VarKeyRegistryLocator.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs) - `Resources.Load` lookup と empty runtime fallback asset の生成
- [BaseLifetimeScopeRegistry.cs](../../GameLib/Script/Common/LTS/Registry/BaseLifetimeScopeRegistry.cs) - registered scope に対する runtime identity lookup

### 現行の不足点

現行プロジェクトには、04 が target architecture で埋めるべき依存関係の穴がまだ残っている。

- dependency truth を generation と boot の前ではなく runtime build 後に確定できてしまう
- optional behavior が explicit absence policy ではなく fallback に退化できてしまう
- runtime query と service resolution が、まだ検証意味論で完全に分離されていない
- profile 固有の failure boundary が dependency レベルで固定されていない
- projection mismatch と provenance loss に対する単一の権威ある拒否 gate がない

---

## 検証権威

04 は、dependency correctness に対する fail-closed な権威である。

explicit data から dependency correctness を証明できないなら、その graph は target kernel において無効である。

01 は typed identity domain、dependency edge 表現、正規化済み IR structure を所有する。
03 は generation-time の staging、artifact completeness、hash compatibility、決定論的な publication を所有する。
04 は、runtime execution が始まる前に宣言された dependency graph が合法かどうかを決める rule を所有する。

validation は best-effort の warning pass ではない。
graph が projection、publication、boot、execution に進んでよいかを決める gate である。

---

## パイプラインにおける位置

dependency validation は 2 つの異なる gate で実行される。

```text
Authoring / ModuleContribution
  -> Normalize
  -> KernelIR
  -> Pre-Generation Dependency Validation
  -> Projection Generation and Staging
  -> Post-Generation Dependency Validation
  -> Verified artifact set
  -> Boot
```

### 1. Pre-Generation Validation

Pre-Generation Validation は、runtime-facing projection が生成される前に、正規化済み KernelIR に対して実行される。

宣言された graph の内部で次を検証する。

- missing dependency
- invalid identity-domain satisfaction
- invalid ownership
- invalid phase usage
- forbidden lifecycle enrollment pattern
- invalid optional dependency policy
- invalid phase の cycle
- forbidden legacy leakage

Pre-Generation Validation が失敗したら、generation は trusted run として進めてはならない。

### 2. Post-Generation Validation

Post-Generation Validation は、03 が出した staged projection に対して実行され、それらが trusted artifact set になる前に検証する。

projection が dependency correctness、provenance、identity boundary を保っていることを検証する。

- projection によって unknown identity が導入されていない
- projection 中に dependency edge や required mapping が落ちていない
- projection によって profile availability drift が導入されていない
- validation に必要な debug coverage や source provenance が generation 中に失われていない
- invalid input を修復するための projection-specific fallback が導入されていない

Post-Generation Validation が失敗したら、staged artifact は拒否され、verified output として公開してはならない。

runtime が結果の artifact set を trusted とみなす前に、両方の gate を通過しなければならない。

---

## Dependency Validation Model

04 は、01 と 02 で定義された dependency data を解釈する。
canonical shape を再定義しない。

### Validation Inputs

dependency validation は、次の explicit で immutable な input に対して動作する。

- normalized KernelIR
- selected kernel profile
- normalization を通じて解決された availability / version input
- 選択した profile 向けに 03 が出した staged projection
- diagnostics に必要な DebugMap provenance metadata と identity coverage
- verified runtime query behavior や LegacyCompat boundary のような、明示的に宣言された lower-spec allowance

hidden runtime state、scene discovery、reflection 由来の handler list、fallback 生成 identity は有効な validation input ではない。

### Validation Outputs

dependency validation は、明示的な result contract を返す。

- pass / fail status
- stable code を持つ issue list
- severity summary
- affected node と phase
- selected profile association
- 11 と 15 が diagnostics と test を決定論的に描画するための provenance

成功した validation result は trust の前提である。
runtime が無視してよいヒントではない。

### Dependency Identity Rules

KernelIR が typed identity を使うため、validation も同じ domain を尊重する。

- `ServiceId` は `CommandTypeId` として扱えない
- `ValueKeyId` は `ServiceId` として扱えない
- `ScopeAuthoringId` は runtime `ScopeHandle` として扱えない
- `RuntimeQueryId` は lifecycle step identity として扱えない

### Phase Model

validation は phase-aware である。

| Phase | 意味 |
|---|---|
| Build | generation 前の組み立て |
| Generate | projection 生成中 |
| Boot | runtime 開始時 |
| Acquire | acquire / release などの lifecycle 取得段階 |
| Runtime | 通常の runtime 実行 |
| Save | save / persistence に関する段階 |
| EditorOnly | editor 専用段階 |

phase によって許可される dependency と cycle は変わる。

### Strength Model

dependency strength は、validation での扱いを決める。

| Strength | 意味 |
|---|---|
| Required | 欠けたら失敗する |
| Optional | 欠けてもよいが、absence behavior が必要 |
| Weak | 関係はあるが、強制依存ではない |
| DiagnosticOnly | 診断には使うが、成立条件にはしない |

### Severity Model

severity は次の意味を持つ。

- `Fatal`: trust boundary violation、projection が未知 identity を導入すること、回復不能な provenance loss、Release profile における forbidden legacy leakage
- `Error`: required dependency の欠落、invalid lifetime direction、invalid cycle、invalid optional policy、invalid runtime query / service mixing、duplicate identity
- `Warning`: 明示的に許可された migration 例外や profile 例外で、なお観測可能かつ bounded なもの
- `Info`: acceptance を変えない非ブロッキングな trace data

下位仕様が特定ケースの severity を変える場合は、それを明示し、必要な fail-closed 振る舞いを保たなければならない。

---

## Validation Rule Categories

validation rule は次のカテゴリに分ける。

- `Local Node`: source location、owner module、profile 宣言など、単一 node の妥当性
- `Local Edge`: identity domain、phase、strength など、1 本の dependency edge の妥当性
- `Cross-Node`: lifetime direction、ordering、missing target などの関係
- `Cross-Module`: module dependency と ownership の相互作用
- `Profile-Aware`: Development / Release / Test における dependency 合法性
- `Projection`: 生成済み artifact をまたいだ dependency truth の保持
- `Legacy Boundary`: LegacyCompat へ入る・出ることの合法性

この分類は、validation を決定論的で監査可能、かつ拡張可能に保つためのものであり、無制限な heuristic pass にしないためのものである。

---

## Module Dependency Validation

validation は次を確認しなければならない。

- 必須 module dependency がすべて存在する
- 必須 module dependency が選択 profile で enabled である
- version constraint がある場合、その互換条件を満たす
- optional module dependency が explicit な absence behavior を持つ
- core module が 13 の明示的許可なしに legacy module に依存していない
- 貢献された identity の module ownership が明示的で、曖昧でない

validation は次を拒否しなければならない。

- required module の欠落
- 選択 profile で disabled の required module
- absence behavior を持たない optional module dependency
- 宣言されていない forbidden module dependency
- target-kernel core が LegacyCompat を通常 dependency のように頼ること

---

## Service Dependency Validation

validation は次を確認しなければならない。

- 必須 `ServiceId` がすべて存在する
- 各 required service の owner module が選択 profile で available である
- required contract が実際に target service identity から提供されている
- lifetime direction が requester と phase に対して互換である
- 宣言された dependency phase が参与する lifetime に対して合法である
- Build / Generate / Boot / Acquire dependency が runtime-only object や discovery に依存していない

より長命な service は、下位仕様が verified indirection を定義しない限り、Build / Generate / Boot / Acquire の段階でより短命な service を必要としてはならない。

validation は次を拒否しなければならない。

- missing required service
- invalid service lifetime direction
- service contract mismatch
- runtime component discovery のみで満たされる service dependency
- explicit runtime query 宣言なしに runtime query 意味論に依存する generation / boot dependency

---

## Scope Dependency Validation

validation は次を確認しなければならない。

- 明示的な parent scope がすべて存在する
- parent scope kind が child scope kind に対して合法である
- owner module が存在し、available である
- required scope service が存在し、profile-valid である
- 参照された value init plan が存在し、正しい value を対象にしている
- scene と runtime boundary の rule が守られている
- parentage が runtime hierarchy inference に委ねられず explicit である

validation は次を拒否しなければならない。

- missing parent scope definition
- invalid parent kind relationship
- transform hierarchy inference に依存した未解決の scope parentage
- scene または ownership boundary を越える scope dependency

---

## Lifecycle Dependency Validation

validation は次を確認しなければならない。

- 各 lifecycle target reference が宣言された target kind に対して有効である
- target kind が `Service` のとき、target service が存在する
- target kind が `Scope` のとき、target scope が存在する
- target kind が `RuntimeQuery` のとき、target runtime query が存在する
- target kind が `ValueStore`、`RuntimeObjectOwner`、`LegacyAdapter` のとき、target local owner reference が明示的で有効である
- lifecycle phase 宣言が妥当である
- lifecycle step order が決定論的である
- lifecycle plan と step に source location がある
- lifecycle dependency が invalid な phase cycle を導入しない
- target reference が required scope と profile で利用可能である

参加は `LifecycleIR` で表現されなければならない。
interface 実装だけでは lifecycle enrollment にならない。

validation は次を拒否しなければならない。

- interface-only の lifecycle discovery 想定
- registration scanning から導かれた lifecycle participation
- missing service を target にした lifecycle step
- missing scope を target にした lifecycle step
- missing runtime query を target にした lifecycle step
- invalid local owner reference を target にした lifecycle step
- 同一 phase 内で非決定論的な lifecycle ordering
- Build / Generate / Boot / Acquire cycle を生む lifecycle dependency

---

## Command Dependency Validation

validation は次を確認しなければならない。

- すべての `CommandTypeId` が存在する
- すべての command executor reference が存在する
- すべての payload schema reference が存在する
- 各 command payload field reference が payload schema と互換である
- required service / value / runtime query dependency が存在する
- runtime dispatch identity が raw authoring key で満たされていない
- command executor availability が ServiceGraph の bulk discovery で満たされていない
- control-flow の child command reference が妥当である
- command runner domain と cardinality が宣言されている場合に妥当である
- 必要な場合、command-level module dependency が宣言されている

validation は次を拒否しなければならない。

- missing command executor
- missing payload schema
- required schema metadata を欠いた payload field
- payload field type mismatch
- authoring key を runtime dispatch identity として使うこと
- bulk DI discovery のみで満たされる command dependency
- missing value や runtime query への command dependency
- 明示的 budget なしに mass entity count に比例して増大する command runner cardinality

---

## Value Dependency Validation

validation は次を確認しなければならない。

- すべての `ValueKeyId` が存在する
- 必要な箇所で stable key が一意である
- すべての schema reference が存在する
- 必要な `ValueSchemaPlan` projection が存在する
- すべての init plan target が存在する
- すべての init plan target schema が存在する
- init value type が schema と互換である
- duplicate init entry に explicit かつ deterministic な overwrite / merge policy がある
- save policy reference が妥当である
- save policy metadata が schema と profile に互換である
- dynamic / reactive evaluation dependency が explicit である
- 必要な `DynamicEvaluationPlan` projection が存在する
- 必要な `ReactiveEvaluationPlan` projection が存在する
- 各 dynamic evaluation output target が存在し、宣言 phase に対して合法である
- 各 reactive evaluation plan が dependency-discovery mode と invalidation policy を宣言している
- dynamic evaluation input が宣言され、target phase に対して妥当である
- table / record / cell schema reference が妥当である
- command の read/write access 宣言が有効な `ValueKeyId` と store scope を参照している
- target-kernel correctness に runtime stable-key fallback が必要でない
- runtime-only negative value ID が存在しない

validation は次を拒否しなければならない。

- duplicate value ID または stable key
- missing `ValueSchemaPlan` projection
- missing key を target にした init plan
- type mismatch の initialization
- collection order によって解決される duplicate init entry
- generic initialization の中に隠された implicit dynamic dependency
- explicit evaluation plan のない hidden DynamicValue または deferred dynamic dependency
- declared invalidation policy ではなく source-local な version check に依存する reactive evaluation
- schema を持たない table / cell payload
- invalid save policy metadata
- declared access policy のない command value access
- runtime stable-key lookup fallback を必要とする value access
- runtime-only negative value ID

---

## Runtime Query Dependency Validation

validation は次を確認しなければならない。

- すべての `RuntimeQueryId` が存在する
- query target kind が存在する
- indexed field が定義されている
- owner module が選択 profile で available である
- invalidation policy が存在する
- ambiguity policy が存在する
- 各 requester が runtime query dependency を明示している

runtime query dependency は generic service resolution では満たしてはならない。

validation は次を拒否しなければならない。

- missing runtime query
- service resolution に silently redirected された query dependency
- missing invalidation / ambiguity policy
- validated graph に定義されていない query target kind

---

## Diagnostics と Debug Coverage Validation

validation は次を確認しなければならない。

- error code が category 内で stable かつ unique である
- diagnostic category ownership が存在する
- runtime-facing identity に required DebugMap coverage がある
- fatal / error diagnostics が human-readable metadata に解決できる
- validation issue が、11 と 15 が決定論的に描画するための provenance を十分に保持している

validation は次を拒否しなければならない。

- profile で必要な runtime-facing ID に対する diagnostics coverage の欠落
- 同一 category 内の duplicate diagnostic code
- raw の未知 identity 値以外に解決できない fatal / error diagnostics
- dependency failure を説明するために必要な provenance を落とした projection output

---

## Profile-Aware Validation

validation は常に selected kernel profile を評価する。

Development で valid な dependency graph が Release や Test で invalid になることがある。

validation は次を確認しなければならない。

- profile ごとの contribution availability
- profile ごとの dependency availability
- profile ごとの legacy allowance
- profile ごとの DebugMap strictness
- profile ごとの diagnostics detail requirements

profile-aware policy は explicit でなければならない。
silent fallback はすべての profile で禁止である。

典型的な profile 期待値:

- Development: 最大限の diagnostics と debug coverage。migration allowance は、明示的に bounded である場合に限り warning に落ちてよい
- Release: silent fallback なし。forbidden legacy leakage と未知 identity の導入は `Error` または `Fatal`
- Test: deterministic validation、実用上最大の strictness、再現可能な diagnostics

---

## Optional Dependency Policy

optional dependency は、absence behavior が explicit かつ valid である場合にのみ許可される。

```csharp
public enum OptionalDependencyAbsenceBehavior
{
    DisableContribution = 10,
    EmitWarning = 20,
    UseExplicitAlternative = 30,
    ProfileSpecificError = 40,
}
```

optional dependency policy のルール:

- silent absence は禁止
- absence behavior は explicit に宣言しなければならない
- `DisableContribution` は、どの contribution / projection が無効になるかを示さなければならない
- `EmitWarning` は、runtime behavior を発明せずに observability を保たなければならない
- `UseExplicitAlternative` は、存在し、型互換で、同じ phase と profile で valid な explicit alternative target を示さなければならない
- `ProfileSpecificError` は、absence を failure に引き上げる profile boundary を定義しなければならない

absence behavior を持たない optional dependency は無効である。

欠落している、cross-domain、phase-incompatible、profile-incompatible な explicit alternative も無効である。

---

## Cycle Detection Policy

cycle detection は phase-aware であり、dependency phase ごとに個別に評価しなければならない。

既定では無効:

- Build cycle
- Generate cycle
- Boot cycle
- Acquire cycle
- Save cycle。ただし、下位仕様が安全で検証済みの例外を明示している場合を除く

条件付きで許可:

- verified lazy handle を通じた Runtime cycle
- explicit event channel を通じた Runtime cycle
- verified runtime query indirection を通じた Runtime cycle

それでも Runtime で無効なもの:

- verified indirection のない直接 required cycle
- fallback または deferred discovery のみで修復された cycle
- diagnostics で説明できない observability の cycle

許可された Runtime cycle が bounded かつ explicit である理由を、validation は証明しなければならない。

---

## Conflict と Duplicate Validation

validation は、既定で duplicate または conflicting な structural identity を拒否しなければならない。

例:

- duplicate `ModuleId`
- duplicate `ServiceId`
- duplicate `CommandTypeId`
- duplicate `ValueKeyId`
- duplicate `RuntimeQueryId`
- duplicate `LifecycleStepId`
- 一意性が必要な stable key の重複
- 同一 runtime command namespace における duplicate authoring key
- 同一 phase 内の conflicting lifecycle order 宣言

last-write-wins の conflict resolution は禁止である。

下位仕様が deterministic merge protocol と validation policy を明示しない限り、implicit merge も禁止である。

---

## Forbidden Dependency Patterns

target kernel で禁止される dependency pattern は次のとおり。

- runtime scene search で満たされた target-kernel dependency
- transform hierarchy inference で満たされた scope parent dependency
- broad component traversal または generic runtime discovery で満たされた service dependency
- bulk DI registration discovery で満たされた command executor dependency
- 実装した interface だけから推測された lifecycle participation
- runtime stable-key fallback で満たされた value dependency
- generated runtime fallback identity で修復された missing required dependency
- generic service resolution で満たされた runtime query dependency
- KernelIR に存在しない dependency semantics を導入する generated projection

下位仕様が例外を必要とする場合は、allowed caller、timing、bounds、diagnostics behavior、removal condition を明示しなければならない。

---

## Legacy Leakage Validation

legacy dependency は、13 が定義する boundary の内側でのみ許可される。

既定で合法な方向は次の 1 つだけである。

```text
LegacyCompat -> New Kernel
New Kernel -> LegacyCompat is forbidden
```

validation は次を拒否しなければならない。

- legacy runtime API に依存する new kernel core
- legacy resolver fallback に依存する runtime plan
- legacy command runner registration に依存する command catalog
- legacy negative-ID repair または runtime stable-key repair に依存する value schema
- legacy behavior に根ざした interface-scan または registration-scan discovery に依存する lifecycle participation

legacy allowance は、explicit、observable、profile-aware、removable でなければならない。

---

## Validation Report Model

下の report model は説明用である。
下位仕様や実装は、意味が等価であれば field 名を変えてよい。

```csharp
public sealed class DependencyValidationReport
{
    public ValidationResultStatus Status;
    public string SelectedProfile;
    public DependencyValidationIssue[] Issues;
    public DependencyValidationSummary Summary;
}
```

```csharp
public enum ValidationResultStatus
{
    Passed = 10,
    PassedWithWarnings = 20,
    Failed = 30,
    Fatal = 40,
}
```

```csharp
public sealed class DependencyValidationIssue
{
    public string Code;
    public ValidationSeverity Severity;
    public DependencyNodeIR From;
    public DependencyNodeIR To;
    public DependencyPhase Phase;
    public ModuleId OwnerModule;
    public SourceLocationId Source;
    public string Profile;
    public string Message;
    public string SuggestedFix;
}
```

```csharp
public sealed class DependencyValidationSummary
{
    public int InfoCount;
    public int WarningCount;
    public int ErrorCount;
    public int FatalCount;
}
```

report は、acceptance と rejection を明示しなければならない。
validation が失敗した理由を理解するのに runtime reproduction を必要としてはならない。

---

## Validation Diagnostics 要件

各 validation issue は次を提供しなければならない。

- stable error code
- severity
- dependency phase
- dependency kind または rule category
- source node
- 必要なら target node
- owner module
- source location
- selected profile
- 可能なら suggested fix

source location のない validation error は、それ自体が diagnostics degradation である。

diagnostics は、同じ validated input に対して host、profile、反復実行をまたいでも決定論的でなければならない。

---

## Validation Test Case Model

各 validation test case は次を定義しなければならない。

- Test ID
- Title
- Input KernelIR fixture
- Selected profile
- Expected status
- Expected diagnostics codes
- Expected affected nodes
- Expected phase
- Notes

推奨 fixture 形式:

```md
### TC_DEP_001_MissingRequiredService

Input:
- Service A requires Service B
- Service B is not defined

Profile:
- Development

Expected:
- Status: Failed
- Diagnostic: DEP_SERVICE_MISSING
- Severity: Error
- Phase: Boot
- Affected From: Service A
- Affected To: Service B
```

この fixture 形式は、15 が intent を再定義せずに executable validation test に変換できるようにするためにある。

---

## Required Test Cases

### A. Module Dependency Tests

#### TC_DEP_MODULE_001_MissingRequiredModule

入力:
- Module Gameplay は Module Physics を required とする
- Module Physics は存在しない

Profile:
- Development

期待値:
- Status: Failed
- Diagnostic: `DEP_MODULE_MISSING`

#### TC_DEP_MODULE_002_DisabledRequiredModule

入力:
- Module Gameplay は Module Save を required とする
- Module Save は存在するが Release profile で disabled である

Profile:
- Release

期待値:
- Status: Failed
- Diagnostic: `DEP_MODULE_DISABLED_FOR_PROFILE`

#### TC_DEP_MODULE_003_OptionalModuleAbsentWithDisableBehavior

入力:
- Module UI は Module Tooltip に optional で依存する
- absence behavior = `DisableContribution`

Profile:
- Development

期待値:
- Status: Passed
- Notes: Tooltip 関連 contribution は明示的に無効化される

#### TC_DEP_MODULE_004_OptionalModuleAbsentWithoutBehavior

入力:
- Module UI は Module Tooltip に optional で依存する
- absence behavior が宣言されていない

Profile:
- Development

期待値:
- Status: Failed
- Diagnostic: `DEP_OPTIONAL_ABSENCE_BEHAVIOR_MISSING`

### B. Service Dependency Tests

#### TC_DEP_SERVICE_001_MissingRequiredService

入力:
- `CommandRunner` は `CommandCatalog` を required とする
- `CommandCatalog` が存在しない

Profile:
- Development

期待値:
- Status: Failed
- Diagnostic: `DEP_SERVICE_MISSING`

#### TC_DEP_SERVICE_002_InvalidLifetimeDirection

入力:
- Project-lifetime service が Boot 中に Scene-lifetime service に依存する

Profile:
- Development

期待値:
- Status: Failed
- Diagnostic: `DEP_SERVICE_LIFETIME_INVALID`

#### TC_DEP_SERVICE_003_ServiceContractMissing

入力:
- Service A は `ITimeDomainService` contract を required とする
- Service B は存在するが required contract を提供していない

Profile:
- Development

期待値:
- Status: Failed
- Diagnostic: `DEP_SERVICE_CONTRACT_MISSING`

### C. Scope Dependency Tests

#### TC_DEP_SCOPE_001_MissingParentScope

入力:
- Scene scope が親として ProjectRoot を宣言する
- ProjectRoot scope が存在しない

Profile:
- Development

期待値:
- Status: Failed
- Diagnostic: `DEP_SCOPE_PARENT_MISSING`

#### TC_DEP_SCOPE_002_InvalidParentKind

入力:
- Project scope が Entity scope を親として宣言する

Profile:
- Development

期待値:
- Status: Failed
- Diagnostic: `DEP_SCOPE_PARENT_KIND_INVALID`

#### TC_DEP_SCOPE_003_TransformParentInferenceDetected

入力:
- scope parent が未解決で、runtime transform inference から導かれたとマークされている

Profile:
- Development

期待値:
- Status: Failed
- Diagnostic: `DEP_SCOPE_RUNTIME_TRANSFORM_INFERENCE_FORBIDDEN`

### D. Lifecycle Dependency Tests

#### TC_DEP_LIFECYCLE_001_TargetServiceMissing

入力:
- lifecycle step が Service `HealthService` を target とする
- `HealthService` が存在しない

Profile:
- Development

期待値:
- Status: Failed
- Diagnostic: `DEP_LIFECYCLE_TARGET_SERVICE_MISSING`

#### TC_DEP_LIFECYCLE_002_InterfaceOnlyParticipationRejected

入力:
- service が acquire-like interface を実装している
- `LifecycleIR` step が存在しない

Profile:
- Development

期待値:
- graph が automatic lifecycle discovery を期待している場合は Failed
- Diagnostic: `DEP_LIFECYCLE_INTERFACE_DISCOVERY_FORBIDDEN`

#### TC_DEP_LIFECYCLE_003_AcquireCycleRejected

入力:
- Acquire step A が Acquire step B を required とする
- Acquire step B が Acquire step A を required とする

Profile:
- Development

期待値:
- Status: Failed
- Diagnostic: `DEP_CYCLE_ACQUIRE`

#### TC_DEP_LIFECYCLE_004_TargetValidatedByKind

入力:
- lifecycle step の target kind が `RuntimeQuery`
- 参照された runtime query が存在しない

Profile:
- Development

期待値:
- Status: Failed
- Diagnostic: `DEP_LIFECYCLE_TARGET_INVALID`

### E. Command Dependency Tests

#### TC_DEP_COMMAND_001_CommandExecutorMissing

入力:
- `CommandTypeId` `CameraShake` は存在する
- executor reference が欠落している

Profile:
- Development

期待値:
- Status: Failed
- Diagnostic: `DEP_COMMAND_EXECUTOR_MISSING`

#### TC_DEP_COMMAND_002_PayloadSchemaMissing

入力:
- `CommandTypeId` `SpawnEntity` は存在する
- payload schema が存在しない

Profile:
- Development

期待値:
- Status: Failed
- Diagnostic: `DEP_COMMAND_PAYLOAD_SCHEMA_MISSING`

#### TC_DEP_COMMAND_003_AuthoringKeyUsedAsRuntimeIdentity

入力:
- command の runtime identity が raw string `camera.shake` である

Profile:
- Development

期待値:
- Status: Failed
- Diagnostic: `DEP_COMMAND_AUTHORING_KEY_USED_AS_RUNTIME_ID`

#### TC_DEP_COMMAND_004_CommandUsesMissingValueKey

入力:
- `DamageCommand` が `ValueKey` `health.current` を required とする
- `health.current` が存在しない

Profile:
- Development

期待値:
- Status: Failed
- Diagnostic: `DEP_VALUE_KEY_MISSING`

### F. Value Dependency Tests

#### TC_DEP_VALUE_001_DuplicateValueKeyId

入力:
- 2 つの `ValueKeyIR` が同じ `ValueKeyId` 1001 を使う

Profile:
- Development

期待値:
- Status: Failed
- Diagnostic: `DEP_VALUE_KEY_DUPLICATE_ID`

#### TC_DEP_VALUE_002_DuplicateStableKey

入力:
- 2 つの `ValueKeyIR` が stable key `health.current` を使う

Profile:
- Development

期待値:
- Status: Failed
- Diagnostic: `DEP_VALUE_STABLE_KEY_DUPLICATE`

#### TC_DEP_VALUE_003_InitPlanMissingKey

入力:
- value init plan が `ValueKeyId` 2000 に書き込む
- `ValueKeyId` 2000 が存在しない

Profile:
- Development

期待値:
- Status: Failed
- Diagnostic: `DEP_VALUE_INIT_KEY_MISSING`

#### TC_DEP_VALUE_004_InitTypeMismatch

入力:
- Value key `health.current` は `Int` schema を使う
- 初期値が `String` である

Profile:
- Development

期待値:
- Status: Failed
- Diagnostic: `DEP_VALUE_INIT_TYPE_MISMATCH`

### G. Runtime Query Dependency Tests

#### TC_DEP_QUERY_001_QueryTargetMissing

入力:
- Command `WithTarget` が runtime query `TargetById` を required とする
- `TargetById` が存在しない

Profile:
- Development

期待値:
- Status: Failed
- Diagnostic: `DEP_RUNTIME_QUERY_MISSING`

#### TC_DEP_QUERY_002_QueryWithoutInvalidationPolicy

入力:
- runtime query が Entity を category で index している
- invalidation policy が存在しない

Profile:
- Development

期待値:
- Status: Failed
- Diagnostic: `DEP_RUNTIME_QUERY_INVALIDATION_POLICY_MISSING`

#### TC_DEP_QUERY_003_QuerySatisfiedByServiceResolverRejected

入力:
- runtime query dependency が、explicit な runtime query semantics ではなく service resolver lookup で満たされている

Profile:
- Development

期待値:
- Status: Failed
- Diagnostic: `DEP_RUNTIME_QUERY_AS_SERVICE_FORBIDDEN`

### H. Cycle Detection Tests

#### TC_DEP_CYCLE_001_BuildCycleRejected

入力:
- Service A が Build phase で Service B を required とする
- Service B が Build phase で Service A を required とする

Profile:
- Development

期待値:
- Status: Failed
- Diagnostic: `DEP_CYCLE_BUILD`

#### TC_DEP_CYCLE_002_BootCycleRejected

入力:
- ProjectKernel boot が SceneFlow を required とする
- SceneFlow boot が ProjectKernel を required とする

Profile:
- Development

期待値:
- Status: Failed
- Diagnostic: `DEP_CYCLE_BOOT`

#### TC_DEP_CYCLE_003_RuntimeLazyCycleAllowed

入力:
- Service A が verified lazy handle で Service B を参照する
- Service B が verified lazy handle で Service A を参照する
- Phase = Runtime

Profile:
- Development

期待値:
- Status: Passed

#### TC_DEP_CYCLE_004_RuntimeRequiredCycleRejected

入力:
- Service A が Service B を直接 required とする
- Service B が Service A を直接 required とする
- Phase = Runtime
- Strength = Required
- lazy / event / runtime query の indirection が存在しない

Profile:
- Development

期待値:
- Status: Failed
- Diagnostic: `DEP_CYCLE_RUNTIME_REQUIRED`

### I. Profile-Aware Tests

#### TC_DEP_PROFILE_001_DebugMapMissingInDevelopment

入力:
- runtime-facing `ServiceId` が存在する
- required DebugMap entry が存在しない

Profile:
- Development

期待値:
- Status: Failed
- Diagnostic: `DEP_DEBUGMAP_ENTRY_MISSING`

#### TC_DEP_PROFILE_002_LegacyAllowedInDevelopmentWarning

入力:
- LegacyCompat module が legacy resolver bridge を使う
- その bridge は Development について 13 により明示的に許可されている

Profile:
- Development

期待値:
- Status: PassedWithWarnings
- Diagnostic: `DEP_LEGACY_USAGE_WARNING`

#### TC_DEP_PROFILE_003_LegacyRejectedInRelease

入力:
- TargetKernelCore が `LegacyRuntimeResolver` に依存する

Profile:
- Release

期待値:
- Status: Failed
- Diagnostic: `DEP_LEGACY_FORBIDDEN_IN_RELEASE`

### J. Projection Validation Tests

#### TC_DEP_PROJ_001_ProjectionIntroducesUnknownServiceId

入力:
- KernelIR に `ServiceId` 100 が含まれる
- Generated `ServiceGraphPlan` に `ServiceId` 999 が含まれる

Profile:
- Development

期待値:
- Status: Failed
- Diagnostic: `DEP_PROJECTION_UNKNOWN_SERVICE_ID`

#### TC_DEP_PROJ_002_DebugMapDoesNotMatchProjection

入力:
- `CommandCatalogPlan` に `CommandTypeId` 200 が含まれる
- DebugMap に `CommandTypeId` 200 の entry がない

Profile:
- Development

期待値:
- Status: Failed
- Diagnostic: `DEP_DEBUGMAP_COVERAGE_MISSING`

#### TC_DEP_PROJ_003_ValueSchemaMissingProjection

入力:
- KernelIR に `ValueKeyId` 300 が含まれる
- `ValueSchemaPlan` が `ValueKeyId` 300 を省いている

Profile:
- Development

期待値:
- Status: Failed
- Diagnostic: `DEP_PROJECTION_VALUE_SCHEMA_MISSING`

### K. Additional Required Cases

#### TC_DEP_DIAG_001_MissingSourceLocationForValidationIssue

入力:
- 依存関係を持つ node が validation failure を引き起こす
- その node と結果 issue に source location provenance がない

Profile:
- Development

期待値:
- Status: Failed
- Diagnostic: `DEP_DIAGNOSTICS_SOURCE_LOCATION_MISSING`

#### TC_DEP_ID_001_DuplicateServiceId

入力:
- 2 つの `ServiceIR` entry が同じ `ServiceId` を使う

Profile:
- Development

期待値:
- Status: Failed
- Diagnostic: `DEP_SERVICE_DUPLICATE_ID`

#### TC_DEP_PROJ_004_ProjectionDropsDependencyProvenance

入力:
- KernelIR に valid な dependency edge が含まれる
- Generated projection は target identity を保持しているが、validation diagnostics を通じて dependency を追跡するために必要な provenance を落としている

Profile:
- Development

期待値:
- Status: Failed
- Diagnostic: `DEP_PROJECTION_PROVENANCE_MISSING`

#### TC_DEP_OPTIONAL_001_ExplicitAlternativeInvalid

入力:
- optional dependency が absent である
- absence behavior = `UseExplicitAlternative`
- 宣言された alternative target が存在しない、または phase と互換でない

Profile:
- Development

期待値:
- Status: Failed
- Diagnostic: `DEP_OPTIONAL_ALTERNATIVE_INVALID`

---

## 受け入れ条件

04 が完成していると見なす条件は次のとおり。

- validation authority と pipeline position が定義されている
- 01 が所有する typed domain に基づく dependency identity enforcement が定義されている
- phase-aware validation rule が定義されている
- validation における dependency strength の解釈が定義されている
- severity policy が定義されている
- validation rule category が定義されている
- module / service / scope / lifecycle / command / value / runtime query dependency validation が定義されている
- diagnostics と debug coverage validation が定義されている
- profile-aware validation が定義されている
- optional dependency policy が定義されている
- cycle detection policy が定義されている
- conflict / duplicate validation が定義されている
- forbidden dependency pattern が定義されている
- legacy leakage validation が定義されている
- validation report semantics が定義されている
- validation diagnostics requirements が定義されている
- validation test case format が定義されている
- required validation case coverage が定義されている

dependency correctness が runtime discovery、silent fallback、projection-time repair に依存したままなら未完成である。

---

## テストケース

| テストケース | 目的 | 検証 |
|---|---|---|
| TC-04-01 | 無効な dependency graph は runtime execution より前に失敗することを確認する。 | 目的、validation authority、pipeline の節で、validation を hard gate として扱っていること。 |
| TC-04-02 | cycle detection が phase-aware であることを確認する。 | phase model と cycle detection policy の節で、Build / Generate / Boot / Acquire / Runtime / Save を区別していること。 |
| TC-04-03 | optional dependency が fallback を意味しないことを確認する。 | strength model と optional dependency policy の節で、明示的な absence behavior を要求していること。 |
| TC-04-04 | runtime query が service resolution と別物であることを確認する。 | runtime query validation と forbidden dependency patterns の節で、generic DI 代替を拒否していること。 |
| TC-04-05 | legacy leakage が既定で拒否されることを確認する。 | legacy leakage validation の節で、`LegacyCompat -> New Kernel` の一方向 boundary を維持していること。 |
| TC-04-06 | projection mismatch も依存関係検証失敗であることを確認する。 | pipeline と projection validation rule の節で、generation 後の unknown identity、provenance drop、missing mapping を拒否していること。 |

---

## 最終見解

依存関係検証は、明示的なアーキテクチャと runtime failure の間にある firewall である。

KernelIR は、明示的で profile-aware、phase-aware な validation を通過して初めて信頼できる。

runtime が実行できるのは、すでに explicit validation を生き残った dependency graph だけである。

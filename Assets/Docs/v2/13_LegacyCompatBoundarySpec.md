# Legacy Compatibility Boundary 仕様

## 文書ステータス

- 文書 ID: 13_LegacyCompatBoundarySpec
- ステータス: Draft
- 役割: Kernel v2 において legacy compatibility が見えてよい quarantine boundary、許可される adapter 形状、profile 制約、diagnostics の可視性、および削除ルールを定義する
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
  - [10_1_ScalarRuntimeAndBindingSpec.md](10_1_ScalarRuntimeAndBindingSpec.md)
  - [10_2_DynamicValueEvaluationSpec.md](10_2_DynamicValueEvaluationSpec.md)
  - [11_DebugMapAndDiagnosticsSpec.md](11_DebugMapAndDiagnosticsSpec.md)
  - [12_UnityAuthoringBridgeSpec.md](12_UnityAuthoringBridgeSpec.md)
- 基盤を提供するもの:
  - [14_PerformanceBudgetAndRuntimeRulesSpec.md](14_PerformanceBudgetAndRuntimeRulesSpec.md)
  - [15_TestAndValidationSpec.md](15_TestAndValidationSpec.md)

### 改訂メモ

この改訂では、13 を legacy compatibility の quarantine boundary として作る。

legacy behavior を design extension point として保存するものではない。
migration-only adapter が見えてよい場所、それをどう計測するか、そして target kernel core に fallback behavior として再侵入させない方法を定義する。

また、installer mutation、resolver fallback、command bulk registration、runtime stable-key fallback、temporary runtime adapter まわりのアーキテクチャ方向をさらに締め、explicit・profile-scoped・diagnostic-visible・removable なものだけを残す。

---

## 所有範囲

この仕様が所有するもの:

- `LegacyCompat` の目的と禁止モデル
- `LegacyBoundary`、`LegacyBridge`、`LegacyAdapter`、`LegacyFallback` の定義
- legacy system と target kernel の dependency direction ルール
- 許可される legacy bridge kind と禁止される bridge kind の分類
- migration-only adapter に対する profile と availability ルール
- すべての legacy bridge に対する diagnostics visibility 要件
- installer、resolver、service、scope、lifecycle、command、value、authoring、save、runtime-query に関する legacy boundary ルール
- adapter の形、metadata、ownership、削除ポリシー要件
- legacy-to-v2 mapping artifact に対する migration data policy
- すべての profile に対する fallback 禁止ルール
- legacy compatibility failure policy
- compatibility boundary に対する forbidden pattern と required test

この仕様が所有しないもの:

- 最終的な `ServiceGraph`、`ScopeGraph`、`LifecycleDispatcher`、`CommandCatalog`、`ValueStore` 実装
- 最終的な Unity authoring component schema
- 最終的な save payload format
- migration tool 用の最終 editor UI
- runtime subsystem に対する最終 performance budget 値
- 個々の legacy system の全面再実装
- legacy API を長期にわたって first-class target API として維持すること

13 は compatibility quarantine を所有する。
05 から 12 までが所有している runtime semantics を再所有してはならない。

---

## 目的

この仕様は、legacy code が migration 中に Kernel v2 とどう関わってよいか、そしてより重要なこととして、どこでは関わってはならないかを定義する。

中心的な記述:

```text
Legacy compatibility は quarantine boundary であり、design extension point ではない。

Legacy code は explicit で profile が付けられ diagnostics-visible な bridge を通して target kernel に適応してよい。
target kernel core は legacy runtime behavior に依存してはならない。

Legacy は adapter 経由で v2 に呼び出してよい。
v2 core は fallback として legacy を呼び戻してはならない。
```

この仕様は、古い pattern が別名で target kernel に再侵入するのを止めるために存在する。

もし v2 core が missing data、missing service、missing ID、missing structure を legacy code に修復させたり、runtime でそれを発見させたりするなら、アーキテクチャは退化している。

---

## スコープ

この仕様が定義するもの:

- migration-only legacy での使用に対する compatibility philosophy
- legacy system と target kernel の allowed dependency direction
- allowed bridge kind と forbidden bridge kind
- authoring migration、data migration、diagnostic adapter、runtime adapter に対する profile restriction
- legacy usage に対する diagnostics と visibility 要件
- installer、resolver、service、scope、lifecycle、command、value、authoring、save、runtime query 各 surface に対する legacy boundary
- fallback 禁止ルール
- adapter metadata、shape、ownership、削除要件
- migration data ルール
- failure behavior、forbidden pattern、required test

---

## 対象外

この仕様が定義しないもの:

- すべての legacy feature に対する完全な porting guide
- target kernel subsystem の runtime 実装
- migration tool の最終 authoring UI
- legacy API が長期にわたり安定するという約束
- final save-format migration payload schema
- legacy report や dashboard の最終 runtime packaging

13 は、legacy behavior を second kernel として再定義する場所になってはならない。

---

## 他仕様との関係

| 仕様 | 関係 |
|---|---|
| [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md) | legacy compatibility が新しい kernel core の外に残る、という根本の非対称性を定義し、13 に quarantine contract の詳細を委譲する。 |
| [01_KernelIRSpec.md](01_KernelIRSpec.md) | migration map と legacy adapter diagnostics が参照すべき normalized ID と source location を所有する。 |
| [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md) | runtime builder を mutating するのではなく、authoring migration adapter が出力すべき contribution model を所有する。 |
| [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md) | migrated かつ正規化された input から verified artifact を生成する。 |
| [04_DependencyValidationSpec.md](04_DependencyValidationSpec.md) | `LegacyCompat` への出入りが合法かを enforce する。 |
| [05_BootManifestAndProfileSpec.md](05_BootManifestAndProfileSpec.md) | boot と profile policy を所有する。13 は legacy boot bridge が見えてよいかとその制約を定義する。 |
| [06_ServiceGraphRuntimeSpec.md](06_ServiceGraphRuntimeSpec.md) | target service runtime を所有する。13 は legacy resolver や service adapter が service fallback にならずに見えてよい場所を定義する。 |
| [07_ScopeGraphRuntimeSpec.md](07_ScopeGraphRuntimeSpec.md) | target scope runtime を所有する。13 は legacy LifetimeScope surface の quarantine の方法を定義する。 |
| [08_LifecyclePlanSpec.md](08_LifecyclePlanSpec.md) | lifecycle plan execution を所有する。13 は legacy handler interface を runtime scanning なしでどう移行するかを定義する。 |
| [09_CommandCatalogRuntimeSpec.md](09_CommandCatalogRuntimeSpec.md) | target command runtime を所有する。13 は legacy command runner と key system の quarantine を定義する。 |
| [10_ValueSchemaAndStoreSpec.md](10_ValueSchemaAndStoreSpec.md) | target value schema と store を所有する。13 は legacy Blackboard と Var bridge をどう分離し、fallback truth として振る舞わせないかを定義する。 |
| [10_1_ScalarRuntimeAndBindingSpec.md](10_1_ScalarRuntimeAndBindingSpec.md) | scalar runtime semantics を所有する。13 は string または hash identity fallback を再導入しない legacy scalar adapter の居場所を定義する。 |
| [10_2_DynamicValueEvaluationSpec.md](10_2_DynamicValueEvaluationSpec.md) | dynamic evaluation runtime を所有する。13 は legacy dynamic wrapper や deferred runtime bridge をどう quarantine するかを定義する。 |
| [11_DebugMapAndDiagnosticsSpec.md](11_DebugMapAndDiagnosticsSpec.md) | shared diagnostics substrate を所有する。13 は legacy bridge の visibility と error code がそこにどう流れるかを定義する。 |
| [12_UnityAuthoringBridgeSpec.md](12_UnityAuthoringBridgeSpec.md) | Unity authoring を contribution input に抽出する。13 は legacy authoring adapter が見えてよい唯一の boundary を定義する。 |
| [14_PerformanceBudgetAndRuntimeRulesSpec.md](14_PerformanceBudgetAndRuntimeRulesSpec.md) | legacy diagnostics と bounded adapter の許容コストを予算化する。 |
| [15_TestAndValidationSpec.md](15_TestAndValidationSpec.md) | quarantine rule を使って boundary、visibility、removability、regression を executable test として実装する。adapter policy や legacy-boundary semantics は再定義しない。 |

13 は migration の isolation contract である。
下位 subsystem spec ですでに固定された domain ownership を複製してはならない。

---

## asmdef とコンパイル境界の期待値

migration-only compatibility の想定 assembly family は `GameLib.Legacy.*` である。
詳細な依存行列は [17_AssemblyDefinitionAndCompileBoundarySpec.md](17_AssemblyDefinitionAndCompileBoundarySpec.md) が所有する。

13 に必要なコンパイル境界ルール:

- legacy adapter は `GameLib.Kernel.*` ではなく explicit quarantine assembly に置かなければならない
- dependency direction は一方向である。`GameLib.Legacy.*` は public kernel API に依存してよいが、`GameLib.Kernel.*` は legacy assembly に依存してはならない
- installer、resolver、Blackboard、command runner、scope bridge の migration helper は kernel core assembly に同居させてはならない
- temporary compatibility code は、kernel core を外科的にほどくのではなく、quarantine assembly を削除することで取り除ける状態でなければならない

新しい compatibility path のために kernel assembly が legacy internals を直接参照しなければならないなら、13 の boundary は破られている。

---

## 現在の Legacy 負債の観測

この節は現行コードベースで観測された legacy 負債を要約する。
ここは移行証拠であって、target policy ではない。

### 観測の追跡可能性

| 観測 | 証拠種別 | 想定される圧力先 |
|---|---|---|
| installer discovery がまだ `GetComponentsInChildren` と `Transform.parent` の ownership inference を使っている。 | ソース | runtime discovery なし、hierarchy-derived truth なし |
| legacy scope build が installer を cache し、`InstallFeature(builder, scope)` を直接呼んでいる。 | ソース | contribution-driven runtime composition |
| resolver fallback が component search と parent resolver chaining をまだ使っている。 | ソース | explicit `ServiceGraph` と no resolver fallback |
| `CommandRunnerMB` が 1 つの installer の中で bulk executor、service、lifecycle registration を行っている。 | ソース | explicit command / lifecycle contribution pipeline |
| `VarIdResolver` が unresolved stable key に対して runtime-only negative ID をまだ作っている。 | ソース | verified `ValueKeyId` mapping と no runtime ID invention |
| `VarKeyRegistryLocator` がまだ `Resources.Load` と runtime-created fallback registry instance を使っている。 | ソース | verified boot input と no runtime asset fallback |
| `BlackboardMB` が installer mutation、acquire/release participation、init、debug、transform auto-write を混在させている。 | ソース | authoring、value init、lifecycle、diagnostics の責務分離 |

### 代表的な参照先

- [ScopeFeatureInstallerUtility.cs](../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs) - `GetComponentsInChildren` で `IFeatureInstaller` を収集し、`TryGetNearestScopeNode` を通じて `Transform.parent` を使う
- [RuntimeLifetimeScope.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs) - owned installer を cache し、build 時に `InstallFeature(builder, this)` を呼ぶ
- [RuntimeResolverHub.cs](../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs) - `GetComponent`、`GetComponentInChildren`、`_parentResolver` へ fallback する
- [CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs) - 多数の `ICommandExecutor` 実装と lifecycle-related service を bulk register する
- [VarIdResolver.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs) - unresolved stable key に対して runtime-only negative ID を割り当てる
- [VarKeyRegistryLocator.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs) - `Resources.Load` を使い、runtime fallback registry instance を作成する
- [BlackboardMB.cs](../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs) - `IFeatureInstaller`、acquire/release hook、init logic、debug-view wiring、transform auto-write をまとめて持つ

### 現在のギャップ

対象アーキテクチャは次のギャップを塞がなければならない:

- migration-era の installer / resolver path にまだ runtime discovery が残っている
- builder mutation が legacy MonoBehaviour で生きた pattern として残っている
- service、value ID、registry lookup に fallback が残っている
- lifecycle intent が registration や interface scanning からまだ学べてしまう
- legacy authoring object がまだ runtime composition authority として振る舞える
- missing target data が legacy system によって indirect に修復されてしまえる

---

## Legacy Compatibility Philosophy

legacy compatibility は migration、inspection、controlled coexistence を支援するためだけに存在する。

次のものになってはならない:

- fallback mechanism
- hidden runtime dependency
- second source of truth
- validation bypass の手段
- installer-style runtime mutation を生かし続ける手段

中心ルール:

```text
Legacy compatibility は、explicit で、profile が付けられ、diagnostic-visible で、removable な場合にのみ許可される。
```

追加ルール:

```text
Development は legacy usage をより見えるようにしてよい。
invalid な target data を legacy fallback で valid にしてはならない。
```

---

## Legacy Boundary Definitions

`LegacyCompat` は、承認された legacy adapter が見えてよい migration-only quarantine zone である。

説明用分類:

```csharp
public enum LegacyCompatKind
{
    None = 0,
    AuthoringMigration = 10,
    DataMigration = 20,
    RuntimeAdapter = 30,
    DiagnosticAdapter = 40,
    TestAdapter = 50,
    TemporaryBridge = 60,
    ForbiddenFallback = 90,
}
```

### LegacyBoundary

legacy code が target kernel と関われる declared boundary。承認された adapter rule 経由に限る。

### LegacyAdapter

legacy input または behavior を target-kernel concept に変換する、小さくて ownership を持つ wrapper。

### LegacyBridge

legacy system と target kernel をつなぐ、migration-limited な connection。

### LegacyFallback

target data または target runtime structure が欠けているときに、target kernel から legacy behavior へ暗黙に fallback すること。

`LegacyFallback` は既定で禁止である。

---

## Dependency Direction Rules

既定で許可されるもの:

- `LegacyAdapter` が v2 interface に依存する
- legacy authoring migration が `ModuleContributionData` を emit する
- legacy diagnostic adapter が `KernelDiagnostic` を report する
- legacy data migration が legacy data を読み、verified migration output を書く
- migration 中に legacy-facing shim が explicit adapter 経由で v2 runtime を呼ぶ

既定で禁止されるもの:

- v2 core が `RuntimeResolver` に依存する
- v2 `ServiceGraph` が legacy `LifetimeScope` に依存する
- v2 `ScopeGraph` が `Transform.parent` の nearest-scope inference に依存する
- v2 `CommandCatalog` が `CommandRunnerMB` の executor registration に依存する
- v2 `ValueStore` が `VarIdResolver` の runtime fallback に依存する
- v2 runtime が missing target data の修復を legacy code に求める

dependency direction は一方向でなければならない:

```text
Legacy -> Adapter -> v2

Not:

v2 -> Legacy -> fallback
```

---

## Legacy Bridge Classification

legacy bridge は legal になる前に分類されなければならない。

許可される bridge kind:

| Kind | Purpose | 既定で runtime 可否 |
|---|---|---|
| `AuthoringMigration` | legacy MonoBehaviour または ScriptableObject data を contribution input に変換する | target runtime dependency なし |
| `DataMigration` | legacy asset、registry、save payload を verified v2 data に変換する | build、editor、migration、または load-prevalidation のみ |
| `RuntimeAdapter` | explicit な migration bridge を通して一時的に legacy behavior を露出する | 既定では Development と Test のみ |
| `DiagnosticAdapter` | legacy log、failure、migration status を 11 diagnostics に forward する | runtime truth を変えない限り許可 |
| `TestAdapter` | migration test で legacy と v2 の behavior を比較する | test のみ |
| `TemporaryBridge` | 明示的な期限と owner を持つ短命の緊急 bridge | 既定では Development と Test のみ |
| `ForbiddenFallback` | legacy behavior による missing target data の修復試行 | 決して許可しない |

未分類の legacy bridge は無効である。

禁止される bridge kind:

- fallback resolver bridge
- missing service repair bridge
- missing value-key repair bridge
- command executor discovery bridge
- lifecycle handler scan bridge
- scope-parent inference bridge

---

## Profile and Availability Policy

legacy compatibility は profile availability を宣言しなければならない。

既定ポリシー:

| Profile | AuthoringMigration | DataMigration | RuntimeAdapter | DiagnosticAdapter | LegacyFallback |
|---|---|---|---|---|---|
| Development | warning 付きで許可 | warning 付きで許可 | 宣言済み・owner あり・removable の場合のみ許可 | 許可 | 禁止 |
| Test | 許可 | 許可 | 比較 test または宣言済み migration check の場合に許可 | 許可 | 禁止 |
| Release | prevalidated migrated input または prebuilt verified artifact を通る場合のみ許可 | explicit な prevalidation または import step としてのみ許可 | 既定では禁止 | それ以外で許可された bridge から diagnostics を forward する場合のみ許可 | 禁止 |

Release profile は migrated result を ship してよい。
しかし、それは live runtime legacy dependency を ship することと同じではない。

Development profile は visibility を上げてよい。
しかし fallback permissiveness を上げてはならない。

---

## Diagnostics and Visibility Requirements

すべての legacy bridge usage は、11 の structured diagnostics pipeline を通じて diagnostic-visible でなければならない。

`LegacyCompat` diagnostic には少なくとも次を含める:

- legacy system name
- bridge kind
- owner module
- target v2 subsystem
- source location
- active profile
- removal status
- 該当する場合の expiration condition または blocking issue
- stable diagnostics code

代表的な stable diagnostics code:

- `LEGACY_BRIDGE_USED`
- `LEGACY_RUNTIME_ADAPTER_USED`
- `LEGACY_FALLBACK_FORBIDDEN`
- `LEGACY_CORE_DEPENDENCY_FORBIDDEN`
- `LEGACY_PROFILE_FORBIDDEN`
- `LEGACY_MIGRATION_REQUIRED`
- `LEGACY_ADAPTER_EXPIRED`
- `LEGACY_DIRECT_BUILDER_MUTATION_FORBIDDEN`
- `LEGACY_RUNTIME_ID_FALLBACK_FORBIDDEN`
- `LEGACY_INSTALLER_DISCOVERY_FORBIDDEN`
- `LEGACY_RESOLVER_COMPONENT_FALLBACK_FORBIDDEN`
- `LEGACY_COMMAND_BULK_REGISTRATION_FORBIDDEN`
- `LEGACY_COMMAND_STRING_FALLBACK_FORBIDDEN`
- `LEGACY_LIFECYCLE_HANDLER_SCAN_FORBIDDEN`
- `LEGACY_ADAPTER_DIAGNOSTICS_MISSING`
- `LEGACY_ADAPTER_REMOVAL_POLICY_MISSING`
- `LEGACY_RUNTIME_QUERY_LEGACY_LOOKUP_FORBIDDEN`

Inspector warning や local `Debug.LogWarning` だけでは required failure に足りない。

---

## Legacy Installer Boundary

legacy installer-style mutation は target kernel runtime では許可されない。

target path で禁止される legacy pattern:

```csharp
void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope)
```

現在の証拠:

- [ScopeFeatureInstallerUtility.cs](../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs) は `GetComponentsInChildren` で `IFeatureInstaller` component を発見し、`Transform.parent` を通じて ownership を推論する
- [RuntimeLifetimeScope.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs) は owned installer を cache し、build 中に `InstallFeature(builder, this)` を直接呼ぶ

対象 replacement:

```text
Legacy MB / SO data
  -> AuthoringMigration adapter
  -> ModuleContributionData
  -> KernelIR
  -> VerifiedPlan
  -> runtime
```

boundary の内側で許可されるもの:

- serialized legacy field を読む
- source location と legacy component metadata を付ける
- contribution data を emit する
- migration diagnostics を report する

target path で禁止されるもの:

- target boot 中に `InstallFeature` を呼ぶこと
- legacy authoring component から `IRuntimeContainerBuilder` を mutating すること
- `GetComponentsInChildren` で legacy feature を収集すること
- `Transform.parent` から installer ownership を推論すること

---

## Legacy Resolver Boundary

target `ServiceGraph` は legacy `RuntimeResolver` や VContainer 風 fallback resolution に依存してはならない。

現在の証拠:

- [RuntimeResolverHub.cs](../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs) は registration miss の後に `GetComponent`、`GetComponentInChildren`、`_parentResolver` に fallback する

許可されるもの:

- `LegacyResolverAdapter` は migration 中に legacy caller に v2 service を露出してよい
- legacy diagnostic tool は legacy resolver state を inspect してよい

禁止されるもの:

- target service resolution が legacy resolver に fallback すること
- target service resolution が host-component search に fallback すること
- legacy resolver chain 経由で lifecycle handler を lookup すること
- legacy resolver chain 経由で command executor を lookup すること
- legacy resolver chain 経由で runtime query を lookup すること

中心ルール:

```text
v2 service resolution が失敗したら、その結果は v2 diagnostics failure である。
legacy resolver に修復を求めてはならない。
```

---

## Legacy Service Boundary

legacy service は explicit `RuntimeAdapter` または `AuthoringMigration` declaration を通じてのみ適応してよい。

legacy service adapter は次を宣言しなければならない:

- target `ServiceId`
- legacy source type
- lifetime
- dependency list
- profile availability
- diagnostics code
- removal plan

禁止事項:

- broad な `.AsImplementedInterfaces()` 型の露出
- legacy object がたまたま実装している interface を target truth としてそのまま露出すること
- target resolution failure 後の implicit service substitution

露出する target contract はすべて explicit でなければならない。

---

## Legacy Scope Boundary

legacy `LifetimeScope` は、explicit `LegacyScopeAdapter` がある場合にのみ共存できる。

target `ScopeGraph` は、legacy scope hierarchy から runtime parent-child relation を推論してはならない。

禁止事項:

- nearest-scope search
- `Transform.parent` による ownership inference
- legacy object の存在から automatic scope creation をすること
- legacy runtime behavior による duplicate root cleanup
- legacy scope hierarchy を target scope truth として使うこと

migration 中に許可されるもの:

- legacy scope identity から `ScopeAuthoringId` または verified scope ID への explicit mapping
- diagnostics-visible な legacy scope inspection

---

## Legacy Lifecycle Boundary

legacy lifecycle handler interface は target lifecycle enrollment ではない。

legacy `IScopeAcquireHandler`、`IScopeReleaseHandler`、`IScopeTickHandler` は、migration 中に `LifecycleContribution` へ map してよい。

implemented handler interface の runtime scanning は禁止である。

中心ルール:

```text
migration adapter は legacy handler intent を読んでよい。
target LifecycleDispatcher は legacy handler を scan するのではなく、LifecyclePlan を実行しなければならない。
```

---

## Legacy Command Boundary

legacy command executor は DI registration によって発見してはならない。

target migration path:

- legacy executor metadata
- `CommandContribution`
- `CommandIR`
- `CommandCatalogPlan`
- `CommandCatalog` runtime lookup

現在の証拠:

- [CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs) は多数の `ICommandExecutor` 実装を bulk register し、command service と lifecycle handler を混在させる

禁止事項:

- target command discovery として `IReadOnlyList<ICommandExecutor>` を解決すること
- target runtime path で `CommandRunnerMB` を通じて executor を register すること
- legacy command authoring key lookup を runtime dispatch truth として使うこと
- 欠落した `CommandTypeId` から legacy command key resolver に fallback すること

`CommandRunnerMB` は compatibility boundary の内側での migration source または legacy-facing facade としてのみ残ってよい。
target runtime registrar ではない。

---

## Legacy Value / Blackboard / Var Boundary

legacy value system は `ValueSchema` と `ValueStore` へ migration してよいが、target value runtime は legacy key fallback に依存してはならない。

現在の証拠:

- [VarIdResolver.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs) は unresolved stable key に対して runtime-only negative ID を割り当てる
- [VarKeyRegistryLocator.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs) は `Resources.Load` と runtime-created fallback registry instance を使う
- [BlackboardMB.cs](../../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs) は installer mutation、init、lifecycle、debug responsibility を混在させる

target path で禁止されるもの:

- required value に対する runtime stable-key resolution
- runtime-only negative ID
- `Resources.Load` registry fallback
- schema authority としての legacy Blackboard
- legacy runtime store contents からの `ValueSchema` 推論

migration-only allowance:

```text
Legacy stable key は migration 中に old data を `ValueKeyId` に map するために使ってよい。
その mapping result は runtime 前に検証されなければならない。
```

string や hash から runtime ID を導出する他の legacy key system も同じルールに従う。

---

## Legacy Unity Authoring Boundary

legacy MonoBehaviour は、`AuthoringMigration` adapter を通してのみ authoring source として使ってよい。

許可されるもの:

- serialized field を読む
- `SourceLocation` を付ける
- contribution data を emit する
- migration diagnostics を report する

禁止されるもの:

- target boot 中に `InstallFeature` を呼ぶこと
- target path で legacy authoring component から `builder.Register` を呼ぶこと
- `OnValidate` で target identity を黙って修復すること
- legacy MonoBehaviour を target contribution として runtime discovery すること

この節は [12_UnityAuthoringBridgeSpec.md](12_UnityAuthoringBridgeSpec.md) と整合していなければならない。

---

## Legacy Save Boundary

legacy save data は、explicit `SaveMigrationPlan` の振る舞いを通してのみ migration できる。

save migration path は次を定義しなければならない:

- source legacy format
- target schema version
- ID mapping
- missing-key policy
- failure policy
- diagnostics behavior

禁止事項:

- target `SaveSystem` が missing v2 save data の fallback として legacy save payload を opportunistic に読むこと
- target runtime execution 中の legacy runtime state からの save-schema 推論

migrated save output は explicit で versioned でなければならない。

---

## Legacy Runtime Query Boundary

legacy lookup system は、query identity、target kind、invalidation、ambiguity policy が explicit である場合に限り `RuntimeQuery` へ適応してよい。

禁止事項:

- legacy resolver chain を `RuntimeQuery` として使うこと
- `Transform` traversal を query truth として使うこと
- verified mapping なしで string lookup を target identity として使うこと
- explicit adapter metadata なしで legacy kind や category lookup を target runtime query truth として使うこと

legacy lookup は migration report または explicit adapter の内側に残ってよい。
target runtime truth ではない。

---

## Fallback 禁止ポリシー

legacy compatibility は missing target-kernel data を修復してはならない。

中心ルール:

```text
Fallback は validation failure を hidden runtime behavior に変える。
target kernel はこれを拒否しなければならない。
```

禁止される fallback の例:

- missing `ServiceId` を `RuntimeResolver` から解決すること
- missing scope parent を `Transform.parent` から推論すること
- missing `CommandTypeId` を command string key から解決すること
- missing `ValueKeyId` を negative runtime ID として生成すること
- missing `BootManifest` を `Resources` から load すること
- missing `LifecycleStep` を implemented interface から発見すること
- missing installer contribution を `GetComponentsInChildren` で修復すること

Development は warning を増やしてよい。
しかし、修復を増やしてはならない。

---

## Adapter Shape and Ownership Policy

legacy adapter は次を満たさなければならない:

- explicit に宣言されている
- module に owned されている
- profile-scoped である
- diagnostics-visible である
- 可能な限り one-way である
- removable である
- test でカバーされている

説明用 metadata shape:

```csharp
public sealed class LegacyAdapterDescriptor
{
    public LegacyCompatKind Kind;
    public ModuleId OwnerModule;
    public string LegacySystemName;
    public string TargetSubsystemName;
    public KernelProfileMask Profiles;
    public SourceLocationId Source;
    public LegacyRemovalStatus RemovalStatus;
    public string DiagnosticsCode;
    public string RemovalCondition;
}
```

追加ルール:

- 1 つの adapter は 1 つの compatibility problem だけを解決すべきである
- adapter は parallel kernel や hidden registry になってはならない
- adapter は legacy API を通じて新しい target-kernel feature を作ってはならない
- adapter が target hot path で performance-critical になったら、migration は退化している

---

## Migration Data Policy

migration data は explicit かつ versioned でなければならない。

例:

- legacy command key から `CommandTypeId` への map
- legacy var stable key から `ValueKeyId` への map
- legacy scope identity から `ScopeAuthoringId` への map
- legacy service type から `ServiceId` への map
- legacy save schema から target value schema への map

必要な性質:

- owner module
- source format または system name
- source version
- target artifact または subsystem
- 該当する場合の compatibility または generation hash
- traced entry がある場合は source location

禁止事項:

- runtime execution 中に migration data を推論すること
- target runtime access の修復として migration map を lazy 生成すること

---

## Sunset / Removal Policy

すべての runtime legacy adapter は removal policy を宣言しなければならない。

説明用 status model:

```csharp
public enum LegacyRemovalStatus
{
    Temporary = 10,
    MigrationOnly = 20,
    TestOnly = 30,
    Deprecated = 40,
    Forbidden = 90,
}
```

removal policy には少なくとも次を含める:

- owner module
- 一時的に存在する理由
- 置き換え先
- 許可される profile
- expiration condition
- diagnostics code
- tracking issue または blocking condition

期限切れ adapter は warning ではない。
validation failure である。

---

## Performance and Memory Policy

legacy bridge は runtime discovery cost を再導入してはならない。

target runtime path で禁止されるもの:

- full hierarchy scan
- full registration scan
- reflection-heavy resolver lookup
- per-frame legacy adapter conversion
- per-command legacy key lookup
- per-value stable-key fallback
- per-access `Resources.Load`

重要なルール:

```text
Legacy compatibility は migration tooling では遅くてもよい。
しかし、default で hot runtime path に乗ってはならない。
```

performance 最適化は diagnostics visibility、ownership metadata、removal policy check を取り除いてはならない。

---

## Failure Policy

legacy compatibility failure は explicit でなければならない。

代表的な failure category:

- `LegacyAdapterMissing`
- `LegacyProfileForbidden`
- `LegacyFallbackAttempt`
- `LegacyMappingMissing`
- `LegacyAdapterExpired`
- `LegacySourceInvalid`
- `LegacyRuntimeDependencyForbidden`

default failure boundary:

| Failure Type | Default Boundary |
|---|---|
| authoring migration failure | generation failure |
| current profile で runtime adapter が forbidden | boot failure または subsystem failure |
| target core から legacy への fallback attempt | Development と Test では operation failure、Release では timing に応じて boot failure または fatal failure |
| required migration mapping missing | generation、validation、load-prevalidation、または boot failure |
| expired adapter | validation failure |
| adapter の diagnostics metadata missing | validation failure または analyzer failure |

legacy compatibility failure は silent repair や last-write-wins fallback を通して継続してはならない。

---

## Forbidden Patterns

target legacy compatibility boundary で禁止されるもの:

- v2 core が legacy `RuntimeResolver` に依存すること
- v2 `ServiceGraph` が legacy resolver に fallback すること
- v2 `ScopeGraph` が `Transform` nearest-scope inference に fallback すること
- v2 `CommandCatalog` が `CommandRunnerMB` に fallback すること
- v2 `ValueStore` が `VarIdResolver` の negative ID に fallback すること
- v2 `LifecyclePlan` が `IScopeAcquireHandler` または `IScopeTickHandler` を scan すること
- target boot が `IFeatureInstaller` を呼ぶこと
- target path で legacy feature を集めるための runtime `GetComponentsInChildren`
- required kernel asset に対する runtime `Resources.Load` fallback
- target service resolution として使われる runtime component fallback
- target dependency miss を修復するための runtime parent-resolver chain
- owner module のない legacy adapter
- diagnostics metadata のない legacy adapter
- profile declaration のない legacy adapter
- removal policy のない legacy adapter
- 永続的な extension point として使われる legacy compatibility

---

## Test Case Model

各 `LegacyCompat` test case は次を定義しなければならない:

- Test ID
- Title
- legacy fixture
- target subsystem
- active profile
- operation
- expected result
- expected diagnostics
- expected dependency direction
- 該当する場合の expected migration output

---

## Required Test Cases

### A. Dependency Direction Tests

#### TC_LEGACY_DEP_001_LegacyAdapterMayDependOnV2

```text
入力:
- `LegacyCommandAdapter` が宣言済み adapter 経由で v2 `CommandCatalog` を呼ぶ

期待結果:
- Passed
- Development では `LEGACY_BRIDGE_USED` warning
```

#### TC_LEGACY_DEP_002_V2CoreCannotDependOnLegacyResolver

```text
入力:
- `ServiceGraph` が `RuntimeResolver` へ fallback しようとする

期待結果:
- Failed
- `LEGACY_CORE_DEPENDENCY_FORBIDDEN`
```

#### TC_LEGACY_DEP_003_V2ScopeGraphCannotUseNearestScopeSearch

```text
入力:
- `ScopeGraph` が `Transform.parent` の nearest-scope logic を使おうとする

期待結果:
- Failed
- `LEGACY_CORE_DEPENDENCY_FORBIDDEN`
```

### B. Profile Tests

#### TC_LEGACY_PROFILE_001_RuntimeAdapterAllowedInDevelopmentWithWarning

```text
Profile:
- Development

入力:
- owner と removal policy を持つ explicit runtime legacy adapter

期待結果:
- PassedWithWarnings
- `LEGACY_RUNTIME_ADAPTER_USED`
```

#### TC_LEGACY_PROFILE_002_RuntimeAdapterRejectedInRelease

```text
Profile:
- Release

入力:
- runtime legacy adapter が有効

期待結果:
- Failed
- `LEGACY_PROFILE_FORBIDDEN`
```

#### TC_LEGACY_PROFILE_003_LegacyFallbackRejectedInAllProfiles

```text
Profile:
- Development / Test / Release

入力:
- missing `ServiceId` を legacy resolver へ fallback しようとする

期待結果:
- Failed
- `LEGACY_FALLBACK_FORBIDDEN`
```

### C. Installer Boundary Tests

#### TC_LEGACY_INSTALLER_001_IFeatureInstallerNotInvokedByTargetBoot

```text
入力:
- legacy component が `IFeatureInstaller` を実装している

操作:
- target kernel boot

期待結果:
- `InstallFeature` は呼ばれない
- target は contribution data のみを使う
```

#### TC_LEGACY_INSTALLER_002_GetComponentsInChildrenFeatureCollectionForbidden

```text
入力:
- target boot が `GetComponentsInChildren` で installer を収集しようとする

期待結果:
- Failed
- `LEGACY_INSTALLER_DISCOVERY_FORBIDDEN`
```

#### TC_LEGACY_INSTALLER_003_LegacyMBExtractedAsContribution

```text
入力:
- legacy MeshChannelHub-like MonoBehaviour の serialized field

操作:
- AuthoringMigration adapter が data を抽出する

期待結果:
- ServiceContribution
- LifecycleContribution
- builder mutation なし
```

### D. Resolver and Service Boundary Tests

#### TC_LEGACY_RESOLVER_001_ComponentFallbackRejectedInTargetPath

```text
入力:
- target service resolution が host `GetComponent` または `GetComponentInChildren` fallback を試みる

期待結果:
- Failed
- `LEGACY_RESOLVER_COMPONENT_FALLBACK_FORBIDDEN`
```

#### TC_LEGACY_SERVICE_001_LegacyServiceAdapterRequiresExplicitContracts

```text
入力:
- legacy service adapter が explicit target contract なしに broad な implemented-interface set を露出する

期待結果:
- Failed
- `LEGACY_CORE_DEPENDENCY_FORBIDDEN`
```

### E. Command Boundary Tests

#### TC_LEGACY_CMD_001_CommandRunnerMBBulkRegistrationRejected

```text
入力:
- `CommandRunnerMB` が executors を `ICommandExecutor` として register する

期待結果:
- target runtime path で Failed
- `LEGACY_COMMAND_BULK_REGISTRATION_FORBIDDEN`
```

#### TC_LEGACY_CMD_002_LegacyCommandKeyMigrationAllowed

```text
入力:
- legacy command key `camera.shake`

操作:
- migration が key を `CommandTypeId` に map する

期待結果:
- Passed
- mapping output が migration report に含まれる
```

#### TC_LEGACY_CMD_003_RuntimeCommandStringFallbackRejected

```text
入力:
- `CommandTypeId` が missing だから runtime dispatch が string key を使う

期待結果:
- Failed
- `LEGACY_COMMAND_STRING_FALLBACK_FORBIDDEN`
```

### F. Value Boundary Tests

#### TC_LEGACY_VALUE_001_StableKeyMigrationAllowed

```text
入力:
- legacy stableKey `health.current`

操作:
- migration が stableKey を `ValueKeyId` に map する

期待結果:
- Passed
```

#### TC_LEGACY_VALUE_002_RuntimeNegativeIdRejected

```text
入力:
- `VarIdResolver` が negative runtime-only id を返す

期待結果:
- target runtime で Failed
- `LEGACY_RUNTIME_ID_FALLBACK_FORBIDDEN`
```

#### TC_LEGACY_VALUE_003_LegacyBlackboardNotSchemaAuthority

```text
入力:
- legacy Blackboard に `ValueSchema` に存在しない key がある

期待結果:
- Failed または migration required
- `LEGACY_MIGRATION_REQUIRED`
```

### G. Lifecycle Boundary Tests

#### TC_LEGACY_LIFE_001_HandlerInterfaceMigrationAllowed

```text
入力:
- legacy service が `IScopeTickHandler` を実装している

操作:
- migration が `LifecycleContribution` の Tick step を作る

期待結果:
- Passed
```

#### TC_LEGACY_LIFE_002_RuntimeHandlerScanRejected

```text
入力:
- `LifecycleDispatcher` が `IScopeTickHandler` を探して ServiceGraph を scan する

期待結果:
- Failed
- `LEGACY_LIFECYCLE_HANDLER_SCAN_FORBIDDEN`
```

### H. Authoring, Save, and Runtime Query Tests

#### TC_LEGACY_AUTHOR_001_LegacyMonoBehaviourUsedOnlyThroughAuthoringMigration

```text
入力:
- serialized migration data を持つ legacy MonoBehaviour

操作:
- 12 と 13 の boundary の下で extraction する

期待結果:
- contribution data が emit される
- SourceLocation が付く
- runtime builder mutation はない
```

#### TC_LEGACY_SAVE_001_SaveMigrationPlanRequired

```text
入力:
- target load が legacy save payload に遭遇する

期待結果:
- explicit な SaveMigrationPlan が必要、または failure
- opportunistic な runtime fallback はない
```

#### TC_LEGACY_QUERY_001_LegacyResolverChainNotRuntimeQuery

```text
入力:
- RuntimeQuery が legacy resolver chain lookup を使おうとする

期待結果:
- Failed
- `LEGACY_RUNTIME_QUERY_LEGACY_LOOKUP_FORBIDDEN`
```

#### TC_LEGACY_QUERY_002_TransformTraversalQueryRejected

```text
入力:
- query implementation が runtime identity source として Transform traversal を使う

期待結果:
- Failed
- `LEGACY_RUNTIME_QUERY_LEGACY_LOOKUP_FORBIDDEN`
```

### I. Diagnostics and Sunset Tests

#### TC_LEGACY_DIAG_001_LegacyAdapterRequiresDiagnostics

```text
入力:
- diagnostics metadata のない legacy adapter

期待結果:
- Failed
- `LEGACY_ADAPTER_DIAGNOSTICS_MISSING`
```

#### TC_LEGACY_SUNSET_001_RuntimeAdapterRequiresRemovalPolicy

```text
入力:
- removal policy のない runtime legacy adapter

期待結果:
- Failed
- `LEGACY_ADAPTER_REMOVAL_POLICY_MISSING`
```

#### TC_LEGACY_SUNSET_002_ExpiredAdapterRejected

```text
入力:
- expired とマークされた legacy adapter

期待結果:
- Failed
- `LEGACY_ADAPTER_EXPIRED`
```

---

## 受け入れ基準

この仕様は、次を定義するときに完了である:

- legacy compatibility philosophy
- `LegacyBoundary`、`LegacyBridge`、`LegacyAdapter`、`LegacyFallback` の定義
- dependency direction ルール
- bridge classification
- profile と availability policy
- diagnostics と visibility 要件
- legacy installer boundary
- legacy resolver boundary
- legacy service boundary
- legacy scope boundary
- legacy lifecycle boundary
- legacy command boundary
- legacy value、Blackboard、Var boundary
- legacy Unity authoring boundary
- legacy save boundary
- legacy runtime query boundary
- fallback 禁止ポリシー
- adapter shape と ownership policy
- migration data policy
- sunset と removal policy
- performance と memory policy
- failure policy
- forbidden pattern
- required test

この仕様は、legacy compatibility が missing target data の hidden repair path として機能し続ける、または v2 core が legacy runtime behavior を fallback として依存し続ける状態では完了していない。

---

## 最終見解

legacy compatibility は explicit で、一方向で、diagnostic-visible で、profile-scoped で、removable でなければならない。

Legacy は adapter 経由で v2 に呼び出してよい。
v2 core は fallback として legacy を呼び戻してはならない。

13 は old behavior を無期限に保存するための仕様ではない。
migration を必要、計測可能、境界付きに保ちつつ、target kernel を退化から守るための quarantine contract である。

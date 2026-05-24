# Unity Authoring Bridge 仕様

## 文書ステータス

- 文書 ID: 12_UnityAuthoringBridgeSpec
- ステータス: Draft
- 役割: Kernel v2 における Unity authoring bridge、authoring identity、source location、抽出、正規化、検証、direct-play authoring boundary を定義する
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
  - [11_DebugMapAndDiagnosticsSpec.md](11_DebugMapAndDiagnosticsSpec.md)
- 基盤を提供するもの:
  - [13_LegacyCompatBoundarySpec.md](13_LegacyCompatBoundarySpec.md)
  - [14_PerformanceBudgetAndRuntimeRulesSpec.md](14_PerformanceBudgetAndRuntimeRulesSpec.md)
  - [15_TestAndValidationSpec.md](15_TestAndValidationSpec.md)

### 改訂メモ

この改訂では、12 を「Unity authoring が runtime composition authority であることをやめる境界」として定義する。

また、MonoBehaviour と ScriptableObject は declaration data を提供してよいが、runtime の service graph、command catalog、lifecycle table、value store を直接構築してはならない、というルールを明確にする。

さらに、`ScopeAuthoringId` の所有、prefab と scene の authoring identity ルール、direct-play の verified input ポリシー、そして `IFeatureInstaller` 風の runtime builder mutation から離れる移行経路も定義する。

---

## 所有範囲

この仕様が所有するもの:

- kernel pipeline に対する Unity Scene、Prefab、Prefab Variant、Nested Prefab、ScriptableObject の authoring boundary
- 抽出と direct play のための explicit authoring root 概念
- `ScopeAuthoringId` の生成、安定性、重複検出、再生成ポリシー
- `ScopeAuthoringLink`、`KernelRoot`、および同等の authoring component の責務境界
- authoring 側の `SourceLocation` と `UnityObjectLink` 要件
- authoring component の分類
- Unity authoring source からの deterministic な contribution 抽出
- KernelIR への handoff 前に行う authoring 正規化とローカル検証
- authoring と runtime truth を分ける Transform hierarchy boundary
- Unity object reference の正規化境界
- Unity-facing component における DynamicValue authoring 境界
- kernel-facing authoring component に対する `OnValidate`、`Reset`、editor utility のポリシー
- Unity authoring と direct-play entry point から生成 artifact を参照するポリシー
- runtime Unity linkage metadata boundary
- authoring diagnostics と failure boundary 要件
- Unity authoring component に対する legacy installer migration policy

この仕様が所有しないもの:

- runtime service graph の実装
- runtime scope graph の実装
- runtime scope handle の保存
- command execution の挙動
- value storage の layout
- DynamicValue evaluation の runtime 実装
- 最終的な editor window / inspector UI の layout
- 最終的な Odin group layout や custom drawer の実装
- generated artifact の binary container format

12 は authoring bridge を所有する。
06 から 11 までが所有している runtime semantics を再所有してはならない。

---

## 目的

この仕様は、Unity authoring data が plan-first kernel のための verified declaration input にどう変換されるかを定義する。

中心的な記述:

```text
Unity authoring は runtime structure を記述する。
runtime structure を構築するものではない。

Unity object は authoring source である。
verified plan が runtime composition authority である。

MonoBehaviour と ScriptableObject は declaration data を提供してよい。
しかし、runtime の service graph、command catalog、lifecycle table、value store を直接構築してはならない。
```

この bridge は、Unity を有効な authoring 環境のまま保ちつつ、fallback 的な runtime composition 機構にしてしまわないために存在する。

この仕様は、次の退化を防ぐために存在する:

- MonoBehaviour が target-kernel path で runtime builder を mutating する
- `GetComponentsInChildren` や同等の走査で runtime feature を発見する
- `Transform.parent` から scope ownership を推論する
- prefab duplication が authored kernel identity を黙って複製する
- `OnValidate` や `Reset` が kernel semantics を黙って変更する
- direct play が stale または incomplete な artifact のまま runtime boot に入る
- 必須の Unity reference が unresolved fallback work として runtime plan に残る

---

## スコープ

この仕様が定義するもの:

- authoring-source category
- authoring authority と runtime authority の区別
- authoring 側 identity model
- source location と Unity object linkage metadata
- authoring component の役割と分類
- Unity object から contribution data への抽出パイプライン
- Unity authoring から KernelIR-ready input への正規化と検証パイプライン
- prefab、variant、nested-prefab、scene の identity policy
- ScriptableObject authoring policy
- MonoBehaviour authoring policy
- direct-play authoring flow
- Unity-facing tooling から生成 artifact を参照するポリシー
- runtime Unity linkage boundary
- Unity authoring に対する diagnostics と failure policy
- performance と editor cost policy
- legacy migration policy
- forbidden pattern
- required test

---

## 対象外

この仕様が定義しないもの:

- authoring diagnostics の最終 editor window 実装
- 最終 Odin Inspector field layout
- 最終 runtime service cache / resolver 実装
- 最終 runtime scope handle layout
- 最終 command execution algorithm
- 最終 value store storage layout
- 最終 scene transition algorithm
- 最終 debug UI appearance
- 最終 generated asset serialization container

この仕様は、一般的な Unity editor guide になってはならない。
kernel-facing な authoring contract を定義する。

---

## 他仕様との関係

| 仕様 | 関係 |
|---|---|
| [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md) | Unity authoring は runtime composition authority ではない、という根本ルールを定義し、12 に bridge の詳細を委譲する。 |
| [01_KernelIRSpec.md](01_KernelIRSpec.md) | `ScopeAuthoringId`、`SourceLocationId`、正規化済み IR identity domain を定義する。12 はそれらを曖昧さなく供給しなければならない。 |
| [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md) | 12 が input を生成するべき制約付き declaration system を定義する。 |
| [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md) | 12 の boundary を通って正規化された input から verified artifact を生成する。 |
| [04_DependencyValidationSpec.md](04_DependencyValidationSpec.md) | runtime が信用する前に、authoring 由来の dependency declaration を検証する。 |
| [05_BootManifestAndProfileSpec.md](05_BootManifestAndProfileSpec.md) | boot acceptance を定義し、authoring asset や reference が manifest 生成に入ることは許すが、runtime boot discovery は許さない。 |
| [06_ServiceGraphRuntimeSpec.md](06_ServiceGraphRuntimeSpec.md) | runtime service semantics を所有する。12 は authoring 側の service contribution source と Unity linkage input を所有する。 |
| [07_ScopeGraphRuntimeSpec.md](07_ScopeGraphRuntimeSpec.md) | runtime scope semantics を所有する。12 は authored scope identity、source traceability、Unity object linkage input を所有する。 |
| [08_LifecyclePlanSpec.md](08_LifecyclePlanSpec.md) | runtime lifecycle ordering を所有する。12 は authoring 側の lifecycle contribution source を所有する。 |
| [09_CommandCatalogRuntimeSpec.md](09_CommandCatalogRuntimeSpec.md) | runtime command semantics を所有する。12 は command authoring object、authoring key、payload authoring の正規化を所有する。 |
| [10_ValueSchemaAndStoreSpec.md](10_ValueSchemaAndStoreSpec.md) | runtime value state を所有する。12 は stable-key-facing authoring input と value-init authoring source を所有する。 |
| [10_2_DynamicValueEvaluationSpec.md](10_2_DynamicValueEvaluationSpec.md) | dynamic evaluation の runtime semantics を所有する。12 は Unity-facing DynamicValue authoring input とその正規化境界を所有する。 |
| [11_DebugMapAndDiagnosticsSpec.md](11_DebugMapAndDiagnosticsSpec.md) | 共有 diagnostics substrate を所有する。12 は authoring 側の source fields と failure context をそこへ供給する。 |
| [13_LegacyCompatBoundarySpec.md](13_LegacyCompatBoundarySpec.md) | legacy authoring adapter が見えてよい唯一の boundary を定義する。 |
| [14_PerformanceBudgetAndRuntimeRulesSpec.md](14_PerformanceBudgetAndRuntimeRulesSpec.md) | ここで定義する extraction、normalization、direct-play preparation、authoring diagnostics cost を予算化する。 |
| [15_TestAndValidationSpec.md](15_TestAndValidationSpec.md) | ここで定義した規則を使って authoring extraction の決定性、direct-play input の正しさ、identity stability、CI coverage の test を実装する。authoring role や extraction semantics は再定義しない。 |

12 は kernel pipeline に対する authoring-entry contract である。
Unity-facing な identity、extraction、direct-play rule が ownerless のままではいけない。

---

## asmdef とコンパイル境界の期待値

Unity authoring bridge は意図的に複数 assembly にまたがる:

- `GameLib.Kernel.Authoring`
- `GameLib.Kernel.Authoring.Editor`
- `GameLib.Kernel.Unity`
- 必要な boot-entry glue は `GameLib.Kernel.Boot.Unity`

詳細な依存行列は [17_AssemblyDefinitionAndCompileBoundarySpec.md](17_AssemblyDefinitionAndCompileBoundarySpec.md) が所有する。

12 に必要なコンパイル境界ルール:

- serialized declaration component と authoring-side data contract は `GameLib.Kernel.Authoring` に属する
- extraction、normalization、direct-play preparation、asset refresh、editor validation は `GameLib.Kernel.Authoring.Editor` に属する
- runtime MonoBehaviour bridge code は `GameLib.Kernel.Unity` に属し、authoring editor assembly には置かない
- authoring assembly は runtime builder を mutating してはならず、feature internals を kernel core assembly に引き込んではならない

Unity authoring logic を authoring / editor / Unity bridge assembly に置けず、kernel internals や legacy fallback code を逆参照しなければならないなら、12 の boundary は破られている。

---

## 現在の Unity Authoring 負債の観測

この節は現行コードベースの Unity authoring 負債の観測結果をまとめる。
ここは target policy ではなく、移行元の整理である。

### 観測の追跡可能性

| 観測 | 証拠種別 | 想定される圧力先 |
|---|---|---|
| feature installer が runtime hierarchy traversal で発見されている。 | ソース | explicit authoring root と extraction pipeline |
| scope ownership が `Transform.parent` をたどって推論されている。 | ソース | explicit authored scope relation と no-nearest-scope rule |
| `IFeatureInstaller.InstallFeature` が runtime builder を直接 mutating している。 | ソース | declaration-only authoring component |
| identity component が kind と id を heuristic editor logic で修復している。 | ソース | explicit `ScopeAuthoringId` policy と authoring diagnostics |
| MonoBehaviour が authoring data、DynamicValue preview、defaults、runtime registration を混在させている。 | ソース | authoring/runtime separation と DynamicValue authoring boundary |
| prefab-spawn path が prefab asset 上に runtime scope component がそのままある前提で動いている。 | ソース | prefab template と instance policy、および explicit binding 要件 |

### 代表的な参照先

- [ScopeFeatureInstallerUtility.cs](../../GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs) - `GetComponentsInChildren` による `IFeatureInstaller` discovery と `Transform.parent` 経由の nearest-scope lookup
- [BaseLifetimeScope.cs](../../GameLib/Script/Common/LTS/Core/BaseLifetimeScope.cs) - `IFeatureInstaller` runtime builder mutation contract
- [RuntimeLifetimeScope.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs) - owned installer caching と build 時の runtime 実行
- [LTSIdentityMB.cs](../../GameLib/Script/Common/LTS/Identity/MB/LTSIdentityMB.cs) - 推測された scope kind、id repair、runtime registration、dynamic registry opt-in
- [TooltipChannelHubMB.cs](../../GameLib/Script/Project/UI/Core/Tooltip/TooltipChannelHubMB.cs) - DynamicValue authoring、editor inference、root override、runtime registration の混在
- [MeshChannelHubMB.cs](../../GameLib/Script/Project/Scene/Channels/Mesh/MeshChannelHubMB.cs) - serialized channel entry を service / lifecycle registration に直接使う
- [EntityLifetimeScopeSpawnerMB.cs](../../GameLib/Script/Project/Scene/Field/Entity/Spawner/EntityLifetimeScopeSpawnerMB.cs) - prefab-scope 前提と build-callback 駆動の service instantiation

### 現在のギャップ

現在の project には、12 が塞ぐべき次のギャップが残っている:

- Unity authoring が declaration input ではなく runtime composition logic として振る舞えてしまう
- scope identity と ownership が 1 つの explicit authoring contract としてまだ表現されていない
- direct play が verified artifact generation ではなく runtime repair に寄ってしまえる
- authoring-side の DynamicValue usage が explicit evaluation plan にまだ一様に正規化されていない
- prefab duplication と scene duplication に対する単一の explicit kernel-identity policy がまだない
- editor convenience hook が kernel semantics mutation に滑り込めてしまう

---

## Unity Authoring Bridge の定義

`UnityAuthoringBridge` は Unity authoring data を kernel contribution input に変換する。

これは editor / build-time の bridge であり、runtime service locator ではない。

中心定義:

```text
UnityAuthoringBridge は Unity authoring data を ModuleContributionData と KernelIR-ready normalized input に変換する。

この bridge は extraction、source location attachment、authoring identity resolution、authoring diagnostics、source-to-artifact traceability を所有する。

UnityAuthoringBridge は runtime composition を実行してはならない。
```

bridge は editor / build-time 文脈で explicit authoring root を traverse してよい。
しかし、その root を理解するために runtime ownership inference、runtime service resolution、runtime fallback logic を再利用してはならない。

---

## Authoring Source Categories

説明用モデル:

```csharp
public enum UnityAuthoringSourceKind
{
    SceneObject = 10,
    PrefabAsset = 20,
    PrefabInstance = 30,
    PrefabVariant = 40,
    ScriptableObjectAsset = 50,
    GeneratedAsset = 60,
    CodeDefinedModule = 70,
    LegacyBridge = 90,
}
```

authoring source は declaration data の origin である。

それは次ではない:

- runtime owner
- runtime identity
- runtime scope handle
- generated artifact

必要な source-category の意味:

- `SceneObject`: declaration input を提供する scene resident な authored object
- `PrefabAsset`: authored structure または再利用可能 config を宣言する prefab template
- `PrefabInstance`: prefab template の scene または prefab-hosted placement
- `PrefabVariant`: traceable な override source を持つ派生 prefab definition
- `ScriptableObjectAsset`: module、registry、profile、再利用可能 definition data を提供する authored asset
- `GeneratedAsset`: inspection または boot reference target であり、authoring truth ではない
- `CodeDefinedModule`: 下位仕様が明示的に許可する場合の code-backed module declaration source
- `LegacyBridge`: 13 経由で可視のままにしておく migration-only authoring surface

---

## Authoring vs Runtime Authority

authoring data は意図された runtime structure を記述してよい。
しかし runtime structure は検証済み plan からのみ実行される。

許可されるもの:

- MonoBehaviour または ScriptableObject が declaration data を提供する
- MonoBehaviour または ScriptableObject が source location を提供する
- MonoBehaviour または ScriptableObject が Unity object link metadata を提供する
- editor tool が Unity authoring source から contribution を抽出する

禁止されるもの:

- MonoBehaviour が runtime service を直接 register する
- MonoBehaviour が runtime で lifecycle step を追加する
- ScriptableObject が runtime service graph を直接 mutating する
- authoring extraction が live runtime service を resolve する
- authoring extraction が runtime で scope ownership を discovery する
- play mode に入ったから authoring object が runtime composition authority になる

ターゲットアーキテクチャで MonoBehaviour から `builder.Register` が呼べるなら、アーキテクチャは退化している。

---

## SourceLocation と UnityObjectLink モデル

runtime 行動を生み得る Unity authoring input は、すべて source traceability を持たなければならない。

説明用 `SourceLocation` モデル:

```csharp
public sealed class UnitySourceLocation
{
    public UnityAuthoringSourceKind Kind;
    public string AssetGuid;
    public string AssetPath;
    public long LocalFileId;
    public string ScenePath;
    public string GameObjectPath;
    public string ComponentType;
    public string PropertyPath;
}
```

説明用 `UnityObjectLink` モデル:

```csharp
public sealed class UnityObjectLink
{
    public UnityObjectLinkKind Kind;
    public string SourceGuid;
    public long LocalFileId;
    public int RuntimeInstanceId;
    public string DebugName;
}
```

ルール:

- `SourceLocation` は authoring traceability のためのもの
- `UnityObjectLink` は runtime debug、bridge、selection metadata のためのもの
- `SourceLocation` も `UnityObjectLink` も kernel identity ではない
- runtime instance id は runtime debug metadata としてのみ現れ得て、stable kernel identity になってはならない
- scene path、prefab path、GameObject path、property path は traceability data にすぎない

kernel-facing authoring diagnostics に必要な provenance field は、この文書の diagnostics section で定義され、11 と互換でなければならない。

---

## ScopeAuthoringId Policy

`ScopeAuthoringId` は authored scope definition を識別する。

次を満たさなければならない:

- editor session をまたいで安定している
- source-backed である
- duplicate-detectable である
- 安全な編集を通じて保持される
- explicit editor action または validated duplication policy によってのみ再生成される

対象の ownership rule:

```text
ScopeAuthoringLink が、target-kernel authoring path における ScopeAuthoringId を所有する。
legacy identity component は migration metadata を持ってよいが、著作された scope identity の最終所有者であり続けてはならない。
```

`ScopeAuthoringId` は次から導出してはならない:

- GameObject name
- Transform sibling index
- runtime instance id
- scene traversal order
- component enumeration order

生成と再生成のルール:

- 新しい authored scope は、authoring tool の明示的 flow によって `ScopeAuthoringId` を得る
- GameObject の移動、リネーム、sibling の並び替え、component の再シリアライズでは `ScopeAuthoringId` は再生成されてはならない
- duplication policy は、explicit duplication path で identity を再生成するか、collision が解決されるまで validation で失敗しなければならない
- `OnValidate` は、semantic repair として新しい `ScopeAuthoringId` を黙って発行してはならない

duplicate `ScopeAuthoringId` は authoring error である。
ただし duplication policy が新しい authored definition を明示的に作成し、再生成結果を記録する場合は除く。

---

## Prefab / Variant / Nested Prefab Policy

bridge は次を区別しなければならない:

- prefab template identity
- prefab instance placement
- prefab variant identity
- nested prefab source identity
- scene override source

ルール:

- scope definition を宣言する prefab template は自分自身の `ScopeAuthoringId` を持つ
- prefab template の scene instance は、scene に存在するという理由だけで黙って新しい authored definition になってはならない
- authored kernel structure を override する prefab variant は distinct な authoring source となり、base prefab と自身の override source の両方への traceability を保持しなければならない
- nested prefab は自分自身の source identity を保持し、hierarchy inference で parent identity に flatten してはならない
- scene override は、関係する箇所では base source trace と override source trace の両方を保持しなければならない

prefab duplication policy:

- prefab asset の複製で authored kernel identity が黙って複製されてはならない
- bridge は、複製された definition に対して authored identity を再生成するか、明示的 user action で collision を解決するまで duplicate を拒否しなければならない
- prefab の unpack、component の copy、scene と prefab の間での authored content 移動は、source traceability を保持し、identity conflict が起きた場合は duplicate detection を発火しなければならない

最低限必要な対応ケース:

- Project window で prefab asset を duplicate した場合
- scene で prefab instance を duplicate した場合
- base prefab から prefab variant を作成した場合
- nested prefab が overridden された場合
- component が copy/paste された場合
- scene object を prefab に入れる、または prefab から出す場合
- prefab が unpack された場合

silent collision は禁止である。

---

## Scene Authoring Policy

scene authoring は次を定義してよい:

- scene-local な scope declaration
- declaration source としての service hub と adapter
- value-init authoring data
- command block
- runtime-query source data
- Unity object link

scene authoring は runtime discovery input として使ってはならない。

ルール:

- scene は `KernelRoot` または同等の scene-entry authoring marker のような explicit authoring root を露出しなければならない
- scene object の enumeration order は generated KernelIR に影響してはならない
- scene object name と hierarchy position は stable kernel identity ではない
- play mode に入ったことは、missing な authoring declaration を修復するための runtime scene search を許可しない

target path における `KernelRoot` の責務:

- scene または authoring set が kernel extraction に参加することを宣言する
- 05 が許す boot-relevant authoring input を参照する
- scene authoring の bounded extraction root を anchor する
- authoring diagnostics の entry point を提供する

`KernelRoot` は runtime service locator ではない。

---

## ScriptableObject Authoring Policy

ScriptableObject asset は次を定義してよい:

- module definition
- registry
- command catalog
- value key registry
- profile
- authoring preset
- channel definition
- reusable config

ルール:

- ScriptableObject asset は declaration source であり、下位仕様が runtime-state asset を別途定義して authoring truth から外していない限り、mutable な runtime state container ではない
- 必須 authoring asset は検証済み input から参照されなければならない
- 必須 kernel asset に対する runtime `Resources.Load` fallback は禁止である
- ScriptableObject asset 内の duplicated authored identity は、scene / prefab source と同じ duplicate-detection と traceability policy を発火しなければならない

ScriptableObject asset は便利な authoring surface であってよい。
しかし、missing verified input を修復する hidden runtime registry になってはならない。

---

## MonoBehaviour Authoring Policy

MonoBehaviour authoring component は serialized declaration data を提供してよい。

しかし target-kernel path で runtime registration を行ってはならない。

対象 MonoBehaviour authoring role:

- `KernelRoot`
- `ScopeAuthoringLink`
- `FeatureAuthoring<TSpec>`
- `ServiceHubAuthoring<TSpec>`
- `ValueInitAuthoring`
- `CommandBlockAuthoring`
- `RuntimeQueryAuthoring`
- `UnityObjectLinkAuthoring`

代表的な移行例:

```text
MeshChannelHubMB:
  old: IFeatureInstaller + builder.Register + lifecycle と tick enrollment
  new: MeshChannelHubAuthoring
       -> ServiceContribution
       -> LifecycleContribution
       -> channel-definition contribution

TooltipChannelHubMB:
  old: MonoBehaviour が DynamicValue、root override、editor inference、runtime registration を所有
  new: TooltipChannelHubAuthoring
       -> ServiceContribution
       -> LifecycleContribution
       -> runtime context が必要な場合は DynamicEvaluationContribution
       -> 必要に応じて RuntimeQueryContribution または UnityObjectLink metadata
```

legacy `IFeatureInstaller` 実装 component は、13 の migration boundary を通してのみ残ってよい。
それらは target authoring component ではない。

---

## Authoring Component Classification

説明用モデル:

```csharp
public enum AuthoringComponentKind
{
    Declaration = 10,
    Link = 20,
    Bridge = 30,
    ViewBinding = 40,
    DebugOnly = 50,
    LegacyAdapter = 90,
}
```

意味:

- `Declaration`: `ModuleContributionData` または KernelIR 正規化への input
- `Link`: Unity と runtime の traceability metadata
- `Bridge`: verified runtime path に入力を渡す bounded な Unity event または object boundary
- `ViewBinding`: runtime data と Unity view object の出力 binding
- `DebugOnly`: editor または debug visualization 専用
- `LegacyAdapter`: 13 によって制御される migration-only bridge

ルール:

- component kind は明示的でなければならない
- 下位仕様がその組み合わせを明示的に許可しない限り、1 つの component が declaration、runtime bridge、lifecycle semantics、debug behavior を混在させてはならない
- view binding と event bridging は authoring truth ではない
- declaration component は play mode state なしでも valid でなければならない

---

## Contribution Extraction Pipeline

Unity authoring extraction pipeline:

1. explicit authoring root を収集する
2. その root 配下の authoring component と asset を読む
3. `SourceLocation` を付与する
4. stable authoring identity を解決する
5. Unity reference を正規化する
6. `ModuleContributionData` を emit する
7. local authoring shape を検証する
8. KernelIR 正規化へ handoff する

抽出ルール:

- extraction は editor / build-time である
- extraction は deterministic でなければならない
- extraction は live runtime service state に依存してはならない
- extraction は explicit authoring root を traverse してよいが、hash-relevant または ordering-relevant な出力を emit する前に traversal order を正規化しなければならない
- extraction は runtime `ServiceGraph`、`ScopeGraph`、`CommandCatalog`、`ValueStore` を呼び出してはならない

許可される bounded traversal:

- editor / build-time コンテキストで explicit `KernelRoot` 配下の component を列挙する
- prefab contents を serialized authoring data としてたどる
- 検証済み authoring reference から参照された ScriptableObject input を解決する

禁止される extraction behavior:

- runtime の nearest-scope ownership inference
- scene-wide blind search を kernel truth として扱うこと
- authoring を理解するために play mode runtime container を参照すること
- extraction 中に runtime builder を mutating すること

---

## Normalization and Validation Pipeline

raw な Unity authoring data は、KernelIR に入る前に正規化しなければならない。

正規化が解決しなければならないもの:

- authoring component reference
- `ScopeAuthoringId`
- module ownership
- `SourceLocation`
- profile availability
- asset reference
- prefab source metadata
- scene override metadata
- command authoring key
- authoring 側でのみ使う stable value key

必要な pipeline 方向:

```text
Unity Authoring Source
  -> Extraction
  -> ModuleContributionData
  -> Normalization
  -> KernelIR
  -> Validation
  -> Verified artifacts
```

ルール:

- unresolved Unity reference を runtime plan に fallback work として持ち込んではならない
- unresolved identity collision は runtime boot の前に失敗しなければならない
- normalization は authoring input に存在しなかった runtime identity を invent してはならない
- play mode state、live runtime handle、runtime-created fallback identity は有効な normalization input ではない

---

## Transform Hierarchy Boundary

Transform hierarchy は、editor authoring、visual organization、default suggestion に役立ってよい。

しかし Transform hierarchy は runtime kernel truth ではない。

許可されるもの:

- default parent または grouping の editor-only suggestion
- editor validation display
- diagnostics と source traceability のための GameObject path
- prefab nesting traceability

禁止されるもの:

- runtime parent inference
- runtime nearest-scope search
- `Transform` ancestry による feature ownership detection
- sibling order に依存した plan generation（その order が明示的に authored され、data として正規化されている場合を除く）

代表的な禁止 legacy pattern:

- `ScopeFeatureInstallerUtility.TryGetNearestScopeNode(...)`
- runtime composition logic として使われる `GetComponentsInChildren<IFeatureInstaller>(...)`
- scope ownership または service ownership の discovery に使われる `Transform.parent` traversal

12 は hierarchy-aware な editor UX を禁止しない。
hierarchy 由来の runtime truth を禁止する。

---

## Unity Object Reference Boundary

authoring における Unity object reference は、次のいずれかに正規化されなければならない:

- `SourceLocation`
- `UnityObjectLink`
- `AssetReference`
- `RuntimeBindingRequirement`
- `AuthoringError`

ルール:

- destroyed-object と fake-null の挙動は明示的でなければならない
- Unity fake-null は required authoring reference を黙って消してはならない
- runtime object reference は stable generated identity になってはならない
- authoring reference の正規化は、その reference が asset-backed、scene-backed、runtime-link-only、invalid のどれかを保持しなければならない

required Unity reference を正規化できない場合、generation または validation は失敗しなければならない。
runtime は discovery で修復してはならない。

---

## DynamicValue Authoring Boundary

Unity authoring 内の DynamicValue は、authoring data としてのみ許可される。

ルール:

- `DynamicValue` が context-free かつ editor-only なら、bridge は preview または validation assistance のためだけに評価してよい
- `DynamicValue` が runtime context を必要とするなら、`DynamicEvaluationContribution` または `ReactiveEvaluationContribution` を生成しなければならない
- `DynamicValue` を contribution extraction 中に runtime truth として評価してはならない
- editor preview の結果は diagnostics を suppress してはならず、宣言済み runtime evaluation semantics の代わりにもなってはならない

代表的なポリシー:

```text
Unity authoring における DynamicValue は declaration input である。
runtime context に依存する DynamicValue は evaluation contribution になる。
MonoBehaviour に付いた hidden な runtime getter path のまま残ってはならない。
```

この節は 10-2 と整合していなければならない。

---

## OnValidate / Reset / Editor Utility Policy

`OnValidate`、`Reset`、editor utility は authoring usability を改善してよい。

許可されるもの:

- default reference の割り当て
- display-only field の正規化
- invalid state への warning
- approved な explicit editor utility を通じた missing authoring id の生成
- 明示的かつ editor-safe な場合の asset dirty 付与

禁止されるもの:

- runtime semantics の黙った変更
- diagnostics なしでの required dependency の修復
- registry fallback
- scene-wide discovery を source of truth として行うこと
- duplicate detection なしで identity を生成すること
- Transform parent が変わったからといって `ScopeAuthoringId` を変えること

type-guessing による scope kind 修復や UI-space default の推論のような現在の heuristic repair pattern は migration evidence としては存在してよい。
しかし、それは最終 target contract ではない。

---

## Generated Artifact Reference Policy

Unity authoring は generated artifact を inspection target または boot reference としてのみ参照してよい。

generated artifact は authoring truth ではない。

generated artifact reference には、それが何を指しているのかを証明できるだけの compatibility data が必要である。

最低限の field:

- `ArtifactSetId`
- `KernelIRHash`
- `ProfileHash`
- `RegistryHash`
- `GeneratorVersion`

ルール:

- generated artifact を手で編集して authoring input とすることは禁止
- Unity authoring から参照される generated artifact は derived data として扱わなければならない
- stale generated reference は validation または direct-play boot で失敗しなければならない

---

## Direct Play / Editor Boot Policy

editor direct play でも、verified input を使わなければならない。

許可される direct-play flow:

1. dirty な authoring source を検出する
2. extraction を実行する
3. normalization を実行する
4. validation を実行する
5. temporary または persistent な verified artifact set を生成する
6. BootManifest と profile policy を使って boot する

禁止されるもの:

- ユーザーが Play を押したから runtime fallback すること
- required authoring data の欠落に対する `FindObjectsByType` 修復
- required kernel asset に対する `Resources.Load` fallback
- dirty な authoring が reconciliation されていないのに stale artifact から boot すること

direct play が compatible な verified input を証明できないなら、boot は block されなければならない。

---

## Runtime Unity Linkage Policy

runtime Unity linkage は、verified runtime handle と Unity object を接続する。

用途:

- view binding
- diagnostics
- editor selection
- debug overlay
- Unity event bridge
- object lifecycle observation

用途ではないもの:

- runtime identity generation
- service discovery
- scope parent inference
- command target fallback
- missing authoring declaration の runtime repair

`UnityObjectLink` は metadata である。
kernel truth ではない。

---

## Diagnostics and DebugMap Requirements

11 は共有 diagnostics substrate を所有する。
12 は、それに流し込まれる authoring-side provenance と failure context の最小値を定義する。

Unity authoring diagnostics には次を含めなければならない:

- authoring source kind
- asset GUID
- asset path
- local file id
- 必要に応じた scene path
- 必要に応じた GameObject path
- component type
- 必要に応じた property path
- 利用可能なら module id
- contribution kind
- 必要に応じた profile
- 必要に応じた prefab base または override source

代表的な stable code:

- `UNITY_AUTHORING_SOURCE_MISSING`
- `UNITY_AUTHORING_ID_DUPLICATE`
- `UNITY_AUTHORING_ID_UNSTABLE`
- `UNITY_PREFAB_ID_COLLISION`
- `UNITY_PREFAB_VARIANT_OVERRIDE_INVALID`
- `UNITY_TRANSFORM_PARENT_INFERENCE_FORBIDDEN`
- `UNITY_RUNTIME_BUILDER_MUTATION_FORBIDDEN`
- `UNITY_DIRECT_PLAY_ARTIFACT_STALE`
- `UNITY_OBJECT_REFERENCE_UNRESOLVED`
- `UNITY_DYNAMIC_VALUE_REQUIRES_EVALUATION_PLAN`
- `UNITY_ONVALIDATE_SEMANTIC_MUTATION_FORBIDDEN`

inspector warning だけでは required failure に足りない。
required failure は structured diagnostics pipeline に入らなければならない。

---

## Failure Policy

invalid な Unity authoring は runtime boot の前に失敗しなければならない。

| Failure Type | Default Boundary |
|---|---|
| authoring extraction failure | generation failure |
| duplicate `ScopeAuthoringId` | validation failure |
| unresolved required reference | validation failure |
| prefab identity collision | validation failure |
| stale direct-play artifact | boot blocked |
| runtime builder mutation in target authoring component | analyzer or validation failure |
| runtime nearest-scope inference が correctness のために必要 | validation failure |

invalid authoring は runtime fallback で修復してはならない。

---

## Performance and Editor Cost Policy

authoring extraction は、可能なら incremental であるべきである。

要件:

- deterministic ordering
- minor operation のたびに full-project scan を繰り返さない
- 実用上可能なら authoring-source hash または同等の invalidation data を cache する
- explicit full regeneration をサポートする
- CI と headless extraction をサポートする
- 実用上可能なら reflection-heavy extraction を hot editor path で避ける

performance 最適化は次を飛ばしてはならない:

- validation
- source-location generation
- duplicate detection
- identity normalization

14 で定義される downstream budgeting のための、測定可能な editor cost category の提案:

- `AuthoringBridge.CollectRoots`
- `AuthoringBridge.Extract`
- `AuthoringBridge.Normalize`
- `AuthoringBridge.Validate`
- `AuthoringBridge.PrepareDirectPlay`

---

## Legacy Migration Policy

| Legacy Pattern | Target Representation |
|---|---|
| `IFeatureInstaller.InstallFeature(builder, scope)` | authoring contribution provider または `ModuleContributionData` に抽出される authoring component |
| `GetComponentsInChildren<IFeatureInstaller>` | explicit authoring root collection と deterministic extraction |
| `Transform.parent` による nearest scope | explicit `ScopeAuthoringId` と plan data に正規化された authored relation |
| MonoBehaviour からの `builder.Register<Service>().As<...>()` | `ServiceContribution` + `LifecycleContribution` |
| authoring component からの `.As<IScopeTickHandler>()` | explicit phase を持つ `LifecycleContribution` |
| MonoBehaviour-owned な command registration | `CommandContribution` |
| MonoBehaviour-owned な blackboard init | `ValueInitContribution` |
| runtime object reference を identity として扱う | `UnityObjectLink` または `RuntimeBindingRequirement` |
| required asset に対する runtime `Resources.Load` fallback | verified artifact または verified authoring reference |
| tooltip / mesh channel runtime installer | contribution に正規化された explicit authoring component |

legacy migration は installer-style runtime mutation を target shape として残してはならない。

---

## Forbidden Patterns

target Unity authoring bridge path で禁止されるもの:

- MonoBehaviour が `builder.Register` を呼ぶこと
- MonoBehaviour が `IFeatureInstaller` を通じて target-path runtime composition を実装すること
- ScriptableObject が runtime service graph を mutating すること
- `GetComponentsInChildren` による runtime feature discovery
- `Transform.parent` による runtime scope ownership inference
- authoring ownership rule としての nearest-scope search
- authoring extraction が runtime `ServiceGraph` に依存すること
- authoring extraction が live runtime state に依存すること
- required kernel asset に対する runtime `Resources.Load` fallback
- generated artifact を authoring source of truth として扱うこと
- GameObject name を stable kernel identity として使うこと
- sibling index を stable kernel identity として使うこと
- runtime instance id を stable kernel identity として使うこと
- `OnValidate` が kernel semantics を黙って変更すること
- duplicate authored identity を last-write-wins で解決すること
- prefab duplication が silent identity collision を起こすこと

---

## Test Case Model

各 UnityAuthoringBridge test case は次を定義しなければならない:

- Test ID
- Title
- Unity fixture type
- authoring source fixture
- operation
- expected contribution output
- expected diagnostics
- expected source location
- expected artifact impact

---

## Required Test Cases

### A. SourceLocation Tests

#### TC_UNITY_SRC_001_ComponentSourceLocationGenerated

```text
入力:
- `TooltipChannelHubAuthoring` を持つ Scene GameObject

期待結果:
- SourceLocation に scene path、GameObject path、component type、property path が含まれる
```

#### TC_UNITY_SRC_002_PrefabSourceLocationGenerated

```text
入力:
- `MeshChannelHubAuthoring` を持つ Prefab asset

期待結果:
- SourceLocation に asset GUID、asset path、local file id、component type が含まれる
```

### B. ScopeAuthoringId Tests

#### TC_UNITY_ID_001_NewScopeGetsStableAuthoringId

```text
入力:
- 新しい `ScopeAuthoringLink` component

操作:
- authoring id を生成する

期待結果:
- Stable な `ScopeAuthoringId` が割り当てられる
- diagnostics は clean
```

#### TC_UNITY_ID_002_DuplicateAuthoringIdRejected

```text
入力:
- 同じ `ScopeAuthoringId` を持つ 2 つの scene object

期待結果:
- Failed
- `UNITY_AUTHORING_ID_DUPLICATE`
```

#### TC_UNITY_ID_003_CopyPasteRequiresIdentityPolicy

```text
入力:
- `ScopeAuthoringId` を含む GameObject を copy & paste する

期待結果:
- duplicate が検出されるか、explicit policy によって再生成される
```

### C. Prefab Tests

#### TC_UNITY_PREFAB_001_PrefabInstanceDoesNotSilentlyDuplicateRuntimeIdentity

```text
入力:
- authored scope を持つ prefab を 2 回 instantiate する

期待結果:
- template identity と runtime instance identity が区別される
```

#### TC_UNITY_PREFAB_002_PrefabVariantOverridePreservesSourceTrace

```text
入力:
- prefab variant が channel config を override する

期待結果:
- SourceLocation で base prefab と variant override の両方を trace できる
```

#### TC_UNITY_PREFAB_003_NestedPrefabIdentityCollisionRejected

```text
入力:
- nested prefab に duplicated authored id が含まれる

期待結果:
- Failed
- `UNITY_PREFAB_ID_COLLISION`
```

### D. Installer Migration Tests

#### TC_UNITY_INSTALLER_001_IFeatureInstallerRejectedInTargetPath

```text
入力:
- component が `IFeatureInstaller` を実装し、`builder.Register` を呼ぶ

期待結果:
- Failed
- `UNITY_RUNTIME_BUILDER_MUTATION_FORBIDDEN`
```

#### TC_UNITY_INSTALLER_002_MeshChannelHubExtractsContributions

```text
入力:
- entries を持つ `MeshChannelHubAuthoring`

期待結果:
- hub に対する ServiceContribution
- acquire、release、tick に対する LifecycleContribution
- channel-definition contribution
- runtime builder mutation はない
```

#### TC_UNITY_INSTALLER_003_TooltipHubExtractsEvaluationPlan

```text
入力:
- DynamicValue preset を持つ `TooltipChannelHubAuthoring`

期待結果:
- ServiceContribution
- LifecycleContribution
- runtime context が必要なら DynamicEvaluationContribution
- 必要なら RuntimeQueryContribution または UnityObjectLink metadata
```

### E. Transform Boundary Tests

#### TC_UNITY_TRANSFORM_001_TransformParentNotScopeParent

```text
入力:
- 親 Transform の下にある child GameObject

期待結果:
- explicit authored relation がない限り scope parent は推論されない
```

#### TC_UNITY_TRANSFORM_002_NearestScopeSearchForbidden

```text
入力:
- authoring extraction が Transform.parent を通じた nearest-scope search を試みる

期待結果:
- Failed
- `UNITY_TRANSFORM_PARENT_INFERENCE_FORBIDDEN`
```

### F. Direct Play Tests

#### TC_UNITY_PLAY_001_DirectPlayGeneratesVerifiedArtifacts

```text
操作:
- dirty な authoring のまま Play を押す

期待結果:
- extraction、validation、generation が走る
- boot は verified artifact set を使う
```

#### TC_UNITY_PLAY_002_DirectPlayDoesNotUseRuntimeFallback

```text
入力:
- required artifact が欠けている

操作:
- Play を押す

期待結果:
- boot blocked
- `FindObjectsByType` repair なし
- `Resources.Load` fallback なし
```

### G. OnValidate Tests

#### TC_UNITY_VALIDATE_001_OnValidateMayAssignEditorDefault

```text
入力:
- tooltip root override がない
- component が local `RectTransform` を提案できる

期待結果:
- editor convenience として扱われ、diagnostics が残るなら許可される
```

#### TC_UNITY_VALIDATE_002_OnValidateCannotSilentlyChangeKernelIdentity

```text
操作:
- explicit tool action なしで `OnValidate` が `ScopeAuthoringId` を変更する

期待結果:
- Failed
- `UNITY_ONVALIDATE_SEMANTIC_MUTATION_FORBIDDEN`
```

### H. Extraction and Reference Tests

#### TC_UNITY_EXTRACT_001_ExtractionDeterministic

```text
入力:
- 同じ authoring root と profile

操作:
- extraction を 2 回実行する

期待結果:
- normalized contribution ordering が deterministic
- hash-relevant output が semantic に等しい
```

#### TC_UNITY_EXTRACT_002_UnresolvedReferenceRejectedBeforeKernelIR

```text
入力:
- authoring component が missing required Unity object または asset を参照している

期待結果:
- runtime boot の前に Failed
- `UNITY_OBJECT_REFERENCE_UNRESOLVED`
```

---

## 受け入れ基準

この仕様は、次を定義するときに完了である:

- Unity authoring bridge の責務
- authoring source category
- authoring と runtime authority の境界
- `SourceLocation` と `UnityObjectLink` モデル
- `ScopeAuthoringId` のポリシー
- prefab、variant、nested-prefab のポリシー
- scene authoring policy
- ScriptableObject authoring policy
- MonoBehaviour authoring policy
- authoring component classification
- contribution extraction pipeline
- normalization と validation pipeline
- Transform hierarchy boundary
- Unity object reference boundary
- DynamicValue authoring boundary
- `OnValidate` と `Reset` のポリシー
- generated artifact reference policy
- direct-play と editor-boot policy
- runtime Unity linkage policy
- diagnostics と DebugMap 要件
- failure policy
- performance と editor cost policy
- legacy migration policy
- forbidden pattern
- required test case

この仕様は、Unity authoring が runtime structure を直接 build できるまま、または invalid authoring を修復するために runtime fallback がまだ必要なままでは完了していない。

---

## 最終見解

Unity authoring は runtime structure を記述する。
runtime structure を build するものではない。

対象 migration は次の通りである:

```text
old:
MonoBehaviour / ScriptableObject
  -> IFeatureInstaller
  -> builder.Register
  -> runtime service, lifecycle, and command registration

new:
MonoBehaviour / ScriptableObject
  -> authoring source
  -> ModuleContributionData
  -> KernelIR
  -> VerifiedPlan
  -> runtime
```

これにより、Inspector、Prefab、Scene、ScriptableObject の使いやすさを保ちながら、Unity authoring 層からの runtime composition leakage を止める。

# Lifecycle Plan 仕様

## 文書ステータス

- 文書 ID: 08_LifecyclePlanSpec
- ステータス: Draft
- 役割: Kernel v2 におけるライフサイクル参加、ライフサイクルディスパッチ、フェーズ順序、tick ポリシー、およびライフサイクル失敗ルールを定義する
- 依存先:
  - [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md)
  - [01_KernelIRSpec.md](01_KernelIRSpec.md)
  - [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md)
  - [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md)
  - [04_DependencyValidationSpec.md](04_DependencyValidationSpec.md)
  - [05_BootManifestAndProfileSpec.md](05_BootManifestAndProfileSpec.md)
  - [06_ServiceGraphRuntimeSpec.md](06_ServiceGraphRuntimeSpec.md)
  - [07_ScopeGraphRuntimeSpec.md](07_ScopeGraphRuntimeSpec.md)
- 取り込むもの:
  - LifecycleIR
  - LifecyclePlan
  - ServiceGraphPlan references
  - ScopeGraphPlan references
  - RuntimeQueryPlan references
  - ValueInitPlan references
  - KernelDebugMap
- 基盤を提供するもの:
  - 09_CommandCatalogRuntimeSpec.md
  - 10_ValueSchemaAndStoreSpec.md
  - 11_DebugMapAndDiagnosticsSpec.md
  - 12_UnityAuthoringBridgeSpec.md
  - 13_LegacyCompatBoundarySpec.md
  - 14_PerformanceBudgetAndRuntimeRulesSpec.md
  - 15_TestAndValidationSpec.md

### 所有範囲

この仕様は、ライフサイクル参加、ライフサイクルディスパッチ、フェーズ順序、tick 参加ポリシー、reset ポリシー、およびライフサイクル失敗挙動を所有する。
サービスキャッシュ、スコープ構造、コマンド実行、値ストレージ構成、ランタイムクエリの保存、あるいは Unity MonoBehaviour のライフサイクル意味論は所有しない。

この仕様が所有するもの:

- LifecyclePlan の実行時権威
- ライフサイクルフェーズの意味論
- ライフサイクルステップの意味論
- ライフサイクルターゲットの意味論
- ライフサイクル順序ルール
- ライフサイクル依存関係とロールバック要件
- ライフサイクルディスパッチテーブルのルール
- スコープ境界におけるライフサイクルディスパッチ契約
- サービス境界におけるライフサイクルディスパッチ契約
- ライフサイクルに対するランタイムオブジェクト所有境界
- tick / fixed tick / late tick のポリシー
- エンティティ単位ライフサイクルの禁止
- ライフサイクル失敗ポリシー
- reset とプーリングのライフサイクルポリシー
- 非同期ライフサイクルポリシー
- ライフサイクル診断と DebugMap 要件
- ライフサイクル性能およびメモリのルール
- レガシーライフサイクル移行ポリシー

この仕様が所有しないもの:

- ServiceGraph のキャッシュ実装
- ScopeGraph の親子構造実装
- CommandCatalog のディスパッチ
- ValueStore の保存構成
- RuntimeQuery のインデックス実装
- Unity の update loop 実装詳細

08 はランタイムのライフサイクル権威である。
ServiceGraph、ScopeGraph、CommandCatalog、ValueStore の代替ではない。

---

## 目的

この仕様は、Kernel v2 においてライフサイクル参加がどのように宣言され、検証され、ディスパッチされるかを定義する。

LifecyclePlan がライフサイクル参加を所有する。
ServiceGraph は明示的に対象指定されたサービスのみを解決する。
実装されたインターフェースはライフサイクル登録ではない。

08 の中心的な主張は次の通りである。

```text
Lifecycle は plan から宣言され、検証され、ディスパッチされる。
実行時登録を走査して発見されるものではない。
```

ライフサイクル参加が runtime service のスキャンから発見されているなら、その時点でアーキテクチャは退化している。

---

## スコープ

この仕様が定義するもの:

- LifecyclePlan の実行時責務
- ライフサイクルフェーズ、ステップ、ターゲット、順序ルール
- ライフサイクル依存関係ルール
- 事前計算されたディスパッチテーブルのルール
- ScopeGraph、ServiceGraph、RuntimeQuery、ValueStore、およびランタイムオブジェクト所有者との境界
- tick / fixed tick / late tick / manual tick のポリシー
- ライフサイクル失敗およびロールバック挙動
- reset とプーリングのポリシー
- 非同期ライフサイクル制約
- ライフサイクル診断と DebugMap 要件
- ライフサイクル性能およびメモリのルール
- ライフサイクル移行ルール
- ライフサイクルテストケースモデルと必須テスト

---

## 対象外

この仕様が定義しないもの:

- 最終的な ServiceGraph キャッシュ実装
- 最終的な ScopeGraph ハンドル構成
- 最終的な CommandCatalog 実行アルゴリズム
- 最終的な ValueStore 保存構成
- 最終的な RuntimeQuery インデックス保存
- Unity MonoBehaviour のライフサイクルそのもの
- Unity PlayerLoop のカスタマイズ詳細

この仕様は、ライフサイクルを次のものへ変質させてはならない:

- 登録スキャン型サブシステム
- 汎用ランタイムコールバックバス
- サービスレジストリ
- コマンドレジストリ
- エンティティごとの update テーブル

---

## 他仕様との関係

| 仕様 | 関係 |
|---|---|
| [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md) | ライフサイクル順序を明示的な plan データとして定義し、登録駆動のハンドラ発見を禁止する。 |
| [01_KernelIRSpec.md](01_KernelIRSpec.md) | ここで参照する LifecycleIR、LifecycleStepIR、LifecycleTargetRefIR、ライフサイクル識別語彙を定義する。 |
| [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md) | LifecycleContribution を宣言的入力として定義し、インターフェースの自動収集を対象モデルとして認めない。 |
| [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md) | LifecyclePlan を検証済み projection として生成し、projection 中の暗黙的なライフサイクルステップ生成を禁止する。 |
| [04_DependencyValidationSpec.md](04_DependencyValidationSpec.md) | 実行前にライフサイクルターゲットの妥当性、フェーズの循環、順序決定性、明示的なライフサイクル参加を検証する。 |
| [05_BootManifestAndProfileSpec.md](05_BootManifestAndProfileSpec.md) | boot の受け入れを定義し、boot ライフサイクルフェーズは検証済み lifecycle plan からのみ許可する。 |
| [06_ServiceGraphRuntimeSpec.md](06_ServiceGraphRuntimeSpec.md) | 明示的な service target のみを解決する。08 はライフサイクル参加とディスパッチを所有する。 |
| [07_ScopeGraphRuntimeSpec.md](07_ScopeGraphRuntimeSpec.md) | scope 状態を所有し、状態境界でのライフサイクルディスパッチを要求する。08 は step 実行とディスパッチポリシーを所有する。 |
| 09_CommandCatalogRuntimeSpec.md | コマンドディスパッチを所有する。08 はコマンド関連サービスやアダプタの周辺にあるライフサイクル境界のみを定義する。 |
| 10_ValueSchemaAndStoreSpec.md | 値と動的評価を所有する。08 は値関連ターゲットの周辺にあるライフサイクル境界のみを定義する。 |
| 11_DebugMapAndDiagnosticsSpec.md | 共有された structured diagnostics 基盤と DebugMap の実行時契約を所有する。08 は必要なライフサイクル実行時 provenance フィールドと失敗挙動を定義する。 |
| 12_UnityAuthoringBridgeSpec.md | authoring 側のライフサイクル貢献元と Unity バインディング生成を所有する。 |
| 13_LegacyCompatBoundarySpec.md | 移行専用のレガシーライフサイクルアダプタと、その削除境界を所有する。 |
| 14_PerformanceBudgetAndRuntimeRulesSpec.md | ここで参照する計測可能なライフサイクル予算と runtime marker を所有する。 |
| 15_TestAndValidationSpec.md | ライフサイクルディスパッチ規則と失敗境界を実行可能なテストカバレッジに落とし込む。 |

08 は検証済みのライフサイクル構造を取り込む。
Service registration、component search、interface enumeration からライフサイクル構造を発見してはならない。

---

## asmdef とコンパイル境界の期待値

このサブシステムの想定 asmdef は `GameLib.Kernel.Lifecycle` である。
詳細な依存行列は [17_AssemblyDefinitionAndCompileBoundarySpec.md](17_AssemblyDefinitionAndCompileBoundarySpec.md) が所有する。

08 に必要なコンパイル境界ルール:

- `GameLib.Kernel.Lifecycle` は feature assembly、legacy handler 実装、authoring 抽出コードから分離されたままでなければならない
- lifecycle core は、下位の kernel assembly と Runtime / ServiceGraph / ScopeGraph が提供する明示的な runtime contract のみに依存すべきである
- interface scan ヘルパー、registration scan ヘルパー、legacy dispatcher コードは Lifecycle assembly に取り込んではならない
- Unity player loop のフックや MonoBehaviour ブリッジコードは、lifecycle core ではなく Unity-facing な leaf assembly に置かなければならない

ライフサイクルディスパッチが registration discovery、Unity hierarchy helper、feature-specific handler code なしにコンパイルできないなら、08 の境界は破られている。

---

## 現在のライフサイクル負債の観測

### 観測の追跡可能性

現在のライフサイクル観測は、ソースコード、プロファイリング証拠、または移行メモに追跡可能でなければならない。

この文書を更新する際、現行コードベースと一致しなくなった観測は削除するか、legacy migration note に移さなければならない。

| 観測 | 証拠種別 | 想定される下流仕様 |
|---|---|---|
| ライフサイクル参加が今でも runtime registration と実装済み interface から発見されている。 | ソース | 08 |
| acquire / release のディスパッチが、検証済み step plan ではなく収集済み handler 配列に依存している。 | ソース | 08 |
| runtime scope の build が、resolver 構築時に handler 配列と tick 配列を固定している。 | ソース | 08, 07 |
| ライフサイクル handler の所有者推論が、reflection または最寄り scope 発見に依存している。 | ソース | 08, 07 |
| tick 参加が、mutable な handler list と register / unregister の副作用に依存している。 | ソース | 08, 14 |
| scope ライフサイクル helper が、fire-and-forget 非同期処理や command / value の fallback を tick パスで行っている。 | ソース | 08, 09, 10 |
| 代表的な channel / hub サービスが、ライフサイクル参加、ターゲット解決、ランタイムオブジェクト所有、fallback 修復を 1 クラスに混在させている。 | ソース | 08, 06, 10 |

### 代表的な参照先

- [IScopeNode.cs](../../GameLib/Script/Common/LTS/Core/IScopeNode.cs) - ライフサイクル handler interface、`ScopeAcquireReleaseDispatcher`、および reflection ベースの `ScopeHandlerOwnershipUtility`
- [RuntimeResolverHub.cs](../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs) - `CollectHandlers<THandler>()`、`GetAcquireHandlers()`、`GetReleaseHandlers()`、`GetTickHandlers()`、`RuntimeAcquireReleaseDispatcher`
- [RuntimeLifetimeScope.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs) - resolver build、handler 配列キャッシュ、tick 登録、acquire / release ディスパッチ
- [RuntimeTickHub.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeTickHub.cs) - mutable な tick list、登録ベースの tick 参加、phase split 挙動
- [ScopeLifecycleMB.cs](../../GameLib/Script/Common/LTS/Lifecycle/MB/ScopeLifecycleMB.cs) - handler interface 経由でライフサイクル振る舞いを enroll する installer 変更
- [ScopeLifecycleService.cs](../../GameLib/Script/Common/LTS/Lifecycle/Service/ScopeLifecycleService.cs) - `UniTask.Void` の fire-and-forget、command fallback、value fallback を含むライフサイクルロジック
- [RuntimeScopeLifecycleService.cs](../../GameLib/Script/Common/LTS/Lifecycle/Service/RuntimeScopeLifecycleService.cs) - tick からの async despawn、command runner 検索のための親階層走査、runtime key fallback
- [TooltipChannelHubService.cs](../../GameLib/Script/Project/UI/Core/Tooltip/TooltipChannelHubService.cs) - acquire、tick、query、camera fallback、ランタイムオブジェクト所有を混在させた代表例
- [MeshChannelHubService.cs](../../GameLib/Script/Project/Scene/Channels/Mesh/MeshChannelHubService.cs) - player runtime を hub が所有する代表的サービス
- [AnimationSpriteHubService.cs](../../GameLib/Script/Project/Scene/Channels/SpriteAnimation/AnimationSpriteHubService.cs) - ライフサイクル、provider contract、runtime player ownership を混在させる代表的 hub service

これらの例は代表例であり、網羅的ではない。
handler interface ベースのサービスは現行 repo 全体に広く存在しており、移行対象として広く扱う必要がある。

### 現在のギャップ

現行コードベースは、08 が対象アーキテクチャから削除すべきライフサイクル挙動をまだ露出している:

- ライフサイクルの真実は今も registration 駆動である
- acquire、release、tick の参加は service contract の背後に隠れている
- ライフサイクル順序は collection の挙動に影響されている
- tick 参加は明示的な lifecycle budget ではなく、収集された handler 数に比例して増える
- ライフサイクル所有者の解決は今も発見ロジックに依存している
- 非同期ライフサイクル作業は今も structured failure handling をすり抜けられる
- hub 所有のランタイムオブジェクトは、直接的なライフサイクル target と一貫して分離されていない

---

## ライフサイクルの権威

LifecyclePlan は、ライフサイクル参加に関する唯一の実行時権威である。

サービス、ランタイムオブジェクト、スコープは、インターフェースを実装しているからといってライフサイクルに参加するわけではない。
参加するのは、検証済み LifecyclePlan に LifecycleStep が存在するときだけである。

実装済みインターフェースはレガシーアダプタの詳細としては存在し得るが、対象の LifecycleDispatcher による enrollment に使ってはならない。

禁止事項:

- `IScopeAcquireHandler` を対象にサービスを走査すること
- `IScopeReleaseHandler` を対象にサービスを走査すること
- `IScopeTickHandler` を対象にサービスを走査すること
- `IScopeLateTickHandler` を対象にサービスを走査すること
- `IScopeFixedTickHandler` を対象にサービスを走査すること
- `IReadOnlyList<IScopeTickHandler>` を収集すること
- `.As<IScopeAcquireHandler>()` によってライフサイクル参加を登録すること
- サービス登録によって runtime でライフサイクルステップを追加すること

---

## LifecyclePlan 入力契約

LifecycleDispatcher は、1 つの検証済み artifact set から得られた検証済み LifecyclePlan のみを実行できる。

有効な LifecyclePlan の入力には少なくとも次が必要である:

- LifecyclePlanId
- LifecycleStepId の集合
- 各ステップの明示的な phase
- 各ステップの明示的な target reference
- 各ステップの明示的な action
- 明示的な order metadata
- 明示的な dependency metadata
- 明示的な failure policy、または default を許す failure boundary
- source provenance と DebugMap の連携
- verified artifact set の metadata

LifecyclePlan には、plan から派生した runtime-local または artifact-local の事前計算済みディスパッチテーブルを伴わせてもよい。
権威は cached table ではなく、あくまで検証済み plan にある。

Lifecycle 入力は次を行ってはならない:

- runtime でステップを追加する
- registration からステップを再構成する
- 不完全な artifact set を受け入れる
- boot-only または scope-only のライフサイクル挙動を service builder の中に隠す

---

## ライフサイクル識別モデル

ライフサイクルの runtime は、明示的で型付けされたライフサイクル識別子を使う。

ライフサイクル語彙は次の通りである:

- `LifecyclePlanId`
- `LifecycleStepId`
- `LifecyclePhase`
- `LifecycleActionKind`
- `LifecycleTargetKind`
- `LifecycleFailurePolicy`
- `LifecycleTickGroup`
- `LifecycleDispatchTable`

`LifecyclePlanId`、`LifecycleStepId`、`LifecyclePhase`、`LifecycleActionKind`、`LifecycleTargetRefIR` は 01 が所有する IR に根ざしている。
08 はそれらの runtime 上の意味とディスパッチ制約を定義する。

ライフサイクル識別は次へフォールバックしてはならない:

- 生の `Type`
- interface 実装
- registration 順序
- GameObject 名
- Transform path
- 任意の文字列 lookup

---

## ライフサイクルフェーズモデル

ライフサイクルフェーズは明示的で、順序付けられている。

説明用のライフサイクルフェーズモデルは次の通りである:

```csharp
public enum LifecyclePhase
{
    Boot = 10,
    Create = 20,
    Build = 30,
    Acquire = 40,
    Activate = 50,
    Tick = 60,
    FixedTick = 70,
    LateTick = 80,
    PreRelease = 90,
    Release = 100,
    Reset = 110,
    Destroy = 120,
    Dispose = 130,
}
```

意図:

- `Build` は、検証済み入力から不変または構造的な runtime 状態を準備する
- `Acquire` は、対象をアクティブな scope または runtime 境界へ結び付ける
- `Activate` は、可視性、入力、時間駆動動作などのアクティブ参加を開始する
- `Tick`、`FixedTick`、`LateTick` は継続的な更新を行う
- `PreRelease` は、切り離し前に外向きの活動を停止する
- `Release` は、runtime の結び付きを解除する
- `Reset` は、検証済みの再利用に向けて状態を消去する
- `Destroy` は、ライフサイクル所有を終了する
- `Dispose` は、残存リソースを解放する

Unity MonoBehaviour の callback 順序は、ライフサイクルの真実モデルではない。

---

## ライフサイクルステップモデル

LifecyclePlan は、明示的なライフサイクルステップの一覧である。

説明用の runtime step sketch は次の通りである:

```csharp
public sealed class LifecycleStepPlan
{
    public LifecycleStepId StepId;
    public LifecyclePhase Phase;
    public LifecycleTargetRefIR Target;
    public LifecycleActionKind Action;
    public int Order;
    public LifecycleStepId[] Dependencies;
    public LifecycleFailurePolicy FailurePolicy;
    public SourceLocationId Source;
}
```

説明用の action 語彙は次の通りである:

```csharp
public enum LifecycleActionKind
{
    ServiceMethod = 10,
    GeneratedStaticCall = 20,
    ScopeStateTransition = 30,
    RuntimeObjectOwnerCall = 40,
    ValueInit = 50,
    RuntimeQueryNotify = 60,
    LegacyAdapterCall = 90,
}
```

`RuntimeObjectOwnerCall` は、local player runtime や他の hub-owned object を、1 つずつの lifecycle step に分解するのではなく、その所有者を通じて管理するために存在する。

---

## ライフサイクルターゲットモデル

ライフサイクルターゲットは明示的で型付けされている。

説明用の target model は次の通りである:

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

許可される target category には次が含まれる:

- `ServiceId`
- `ScopePlanId` または同等の scope 境界参照
- 明示的な ValueStore 境界参照
- `RuntimeQueryId`
- hub-owned runtime object owner 参照
- 明示的な legacy adapter target

禁止される target 解決には次が含まれる:

- 生の type scan
- interface scan
- scene search
- Transform 階層検索
- 任意の文字列 lookup

runtime object owner は、下位仕様が owner namespace と diagnostics boundary を定義している場合にのみ、明示的な lifecycle target となる。
これは、すべての local runtime object を first-class な lifecycle participant にしてよいという意味ではない。

---

## ライフサイクル順序モデル

ライフサイクル順序は決定的である。

順序ルール:

- phase 順序は明示的である
- phase 内の step 順序は明示的である
- 依存エッジはより厳しい順序を追加してよい
- registration 順序は決して権威ではない
- 明示的な tie policy を持たない同一 phase の同順位は無効である

検証は次を拒否しなければならない:

- 決定性のない同一 phase 順序
- collection 順序から導かれた隠れた順序
- 選択された phase model に反する dependency cycle

生成済みまたは runtime-local なディスパッチテーブルは、order と dependency resolution によって sort してもよい。
しかし、新しい順序の真実を勝手に作り出してはならない。

---

## ライフサイクル依存関係モデル

ライフサイクル依存関係は明示的で、phase を意識する。

ライフサイクル依存関係ルールには次が含まれる:

- 1 つの step は、1 つ以上の earlier step に依存してよい
- 依存の意味は、汎用 service resolution ではなくライフサイクル意味論の範囲に限定される
- 依存は、選択された phase に対して有効でなければならない
- 下位仕様が検証済みの例外を明示していない限り、acquire-time と build-time の cycle は無効である
- boot ライフサイクル依存関係は boot acceptance ルールと整合していなければならない

ライフサイクル依存関係モデルは、部分実行があり得る場合の rollback 期待値を定義しなければならない。

もし `Acquire` step が、先行する acquire step の成功後に失敗したなら、plan は次を定義しなければならない:

- 完了済み step を逆順で release するかどうか
- scope を failed または inactive state にするかどうか
- runtime query の invalidation を行うかどうか
- diagnostics をどう emit するか

検証アルゴリズムは 04 が所有する。
08 は、それらの検証が守る runtime 契約を定義する。

---

## ライフサイクルディスパッチテーブルモデル

LifecycleDispatcher は、LifecyclePlan から生成された事前計算済みディスパッチテーブルを実行する。

ディスパッチテーブルは次で group 化される:

- ライフサイクル phase
- runtime domain
- scope plan または scope kind（適用可能な場合）
- tick group（適用可能な場合）
- 決定的な order

説明用の runtime table sketch は次の通りである:

```text
LifecycleDispatchTable
  BootSteps[]
  CreateSteps[]
  BuildSteps[]
  AcquireSteps[]
  ActivateSteps[]
  TickSteps[]
  FixedTickSteps[]
  LateTickSteps[]
  PreReleaseSteps[]
  ReleaseSteps[]
  ResetSteps[]
  DestroySteps[]
  DisposeSteps[]
```

LifecycleDispatcher は、runtime で ServiceGraph registration を走査してディスパッチテーブルを構築してはならない。

ディスパッチテーブルは runtime 実行データである。
mutable な registration からライフサイクルの真実を導出してよいという許可ではない。

---

## スコープ状態境界

ScopeGraph は scope state を所有する。
LifecyclePlan は、scope state 境界で実行される副作用を所有する。

ScopeGraph は、scope がある state に入る、または抜けるときにライフサイクルディスパッチを要求してよい。

例:

- built から acquiring へ
- acquiring から active へ
- active から releasing へ
- releasing から inactive へ
- inactive から reset へ
- inactive から destroyed へ

LifecycleDispatcher は、ScopeGraph の親子構造を直接変更してはならない。

07 が scope 構造と state transition の所有者である。
08 は、それらの境界で要求されたディスパッチ作業を所有する。

---

## ServiceGraph 境界

ServiceGraph は、明示的なライフサイクルターゲットサービスを解決する。
ServiceGraph はライフサイクルターゲットを発見しない。

LifecycleDispatcher は、LifecycleStepPlan が明示的にその ServiceId を参照している場合にのみ、service instance を要求してよい。

ServiceGraph は次を提供してはならない:

- `GetAcquireHandlers()`
- `GetReleaseHandlers()`
- `GetTickHandlers()`
- `GetLateTickHandlers()`
- `GetFixedTickHandlers()`
- `IReadOnlyList<IScopeTickHandler>`

06 が service resolution を所有する。
08 がライフサイクル参加とディスパッチを所有する。

---

## ランタイムオブジェクト境界

LifecyclePlan は、すべての local runtime object を直接 enroll すべきではない。

hub-owned な runtime object は、通常、その owner target によって管理される。

例:

- tooltip hub は tooltip player runtime を所有する
- mesh hub は mesh player runtime を所有する
- animation sprite hub は animation channel runtime を所有する

owner hub 自身は lifecycle target になってよい。
内部の player runtime は既定では LifecyclePlan target ではない。

これらの例は代表例であり、網羅的ではない。
同じルールは、広く runtime object family 全体に適用される。

---

## Tick / FixedTick / LateTick ポリシー

tick 参加は明示的で、予算化されていなければならない。

tick step は次を宣言しなければならない:

- tick phase
- tick group
- order
- 想定個数
- 時間ドメイン
- pause 時の挙動
- failure policy

説明用の tick-group model は次の通りである:

```csharp
public enum LifecycleTickGroup
{
    Kernel = 10,
    Project = 20,
    Scene = 30,
    UI = 40,
    Presentation = 50,
    Simulation = 60,
    Debug = 90,
}
```

説明用の time-domain model は次の通りである:

```csharp
public enum LifecycleTimeDomain
{
    UnityTime = 10,
    ScaledGameTime = 20,
    UnscaledGameTime = 30,
    FixedSimulationTime = 40,
    Manual = 50,
}
```

step が互換性のある time domain を宣言していない限り、tick step は暗黙に `UnityEngine.Time` を使ってはならない。

エンティティ単位の tick step 生成は既定で禁止される。

---

## エンティティ単位ライフサイクルの禁止

LifecyclePlan は、次の 1 つごとに lifecycle step を作ってはならない:

- entity
- part
- renderer
- tooltip view
- mesh track
- animation player
- command instance

各ターゲットに対する runtime update は次の責務である:

- hub-owned の local runtime loop
- EntityRuntime system
- ValueStore processing
- RuntimeQuery system
- バッチ化された simulation service

エンティティ単位のライフサイクル例外は、次のすべてが真である場合にのみ許可される:

- entity が境界付きの作成済み aggregate root である
- 予想 instance 数が宣言されている
- performance budget が宣言されている
- lifecycle ownership が明示されている
- diagnostics で source と runtime handle を識別できる

既定はあくまで禁止である。

---

## 失敗ポリシー

ライフサイクル failure は、黙って無視してはならない。

説明用の failure policy model は次の通りである:

```csharp
public enum LifecycleFailurePolicy
{
    FailOperation = 10,
    FailScope = 20,
    FailScene = 30,
    FailKernel = 40,
    ContinueWithError = 50,
}
```

既定の境界:

- boot ライフサイクルの既定は `FailKernel`
- scope ライフサイクルの既定は `FailScope`

`ContinueWithError` には明示的な policy と profile の正当化が必要である。
既定ではない。

failure boundary が宣言されていない場合、runtime は黙って継続するのではなく、既定の境界を適用しなければならない。

---

## 部分 Acquire ロールバックポリシー

部分的に acquire が完了した場合は、明示的に扱わなければならない。

- acquire rollback の既定は `ReverseCompletedAcquireSteps`
- `None` は、plan が意図的に部分 acquire の結果を保持する場合にのみ使用できる
- rollback は、すでに完了した acquire step の逆順で評価される
- rollback の失敗は `KernelDiagnostic` を emit しなければならない
- 部分 acquire failure は forward failure と rollback summary の両方を報告しなければならない

---

## Reset とプーリングのポリシー

reset は release や destroy と別物である。

- `Release` は、runtime をアクティブな scope 利用から切り離す
- `Reset` は、再利用前に runtime state を消去する
- `Destroy` は、寿命を終え、resources を dispose する

pool から再利用される scope は、acquire の前に検証済み reset sequence を実行しなければならない。

pool から再利用される scope または service は、次を保持してはならない:

- 以前の subscription
- 以前の runtime query entry
- 以前の ValueStore transient state
- 以前の owner reference
- ポリシーで明示的に保持されていない限り、以前の channel player runtime state

Release は reset を意味しない。
Reset は destroy を意味しない。

---

## 非同期 / UniTask ポリシー

ライフサイクル step は既定では同期的である。

非同期ライフサイクルは、明示的に追跡される async lifecycle step を通じてのみ許可される。

async lifecycle step は次を定義しなければならない:

- cancellation source
- timeout policy
- failure boundary
- completion 要件
- 次の step が待機するかどうか
- cancellation と failure に関する diagnostics

fire-and-forget のライフサイクル作業は禁止である。

`UniTask.Void` はレガシー移行負債であり、対象のライフサイクルポリシーではない。

---

## 診断と DebugMap 要件

ライフサイクル診断には次を含めなければならない:

- `LifecyclePlanId`
- `LifecycleStepId`
- phase
- target kind
- target reference
- owner module
- source location
- order
- failure policy
- 選択された profile

代表的なライフサイクル runtime エラーコードには次が含まれる:

- `LIFECYCLE_STEP_TARGET_MISSING`
- `LIFECYCLE_STEP_ORDER_CONFLICT`
- `LIFECYCLE_PHASE_CYCLE`
- `LIFECYCLE_INTERFACE_DISCOVERY_FORBIDDEN`
- `LIFECYCLE_REGISTRATION_SCAN_FORBIDDEN`
- `LIFECYCLE_TICK_CARDINALITY_FORBIDDEN`
- `LIFECYCLE_ASYNC_UNTRACKED`
- `LIFECYCLE_PARTIAL_ACQUIRE_FAILED`
- `LIFECYCLE_ROLLBACK_STEP_FAILED`
- `LIFECYCLE_RESET_REQUIRED_BEFORE_REUSE`

step provenance を持たないライフサイクル failure は、診断品質の劣化である。

---

## 性能とメモリのポリシー

ライフサイクルディスパッチは runtime の hot path である。

目標要件:

- ディスパッチ中に registration scan を行わない
- ディスパッチ中に interface scan を行わない
- ディスパッチ path で LINQ を使わない
- 通常の tick ディスパッチで managed allocation を行わない
- ディスパッチコストは registered services ではなく active lifecycle step に比例して増える
- tick step 数は明示的な予算入力である
- エンティティ単位の tick step は既定で禁止である

service 数が増えても、tick 数が自動的に増えてはならない。
command executor 数が増えても、ライフサイクル数が自動的に増えてはならない。

ライフサイクル runtime は、14 が少なくとも次を予算化できるよう十分な marker point を公開すべきである:

- `Lifecycle.DispatchBoot`
- `Lifecycle.DispatchAcquire`
- `Lifecycle.DispatchTick`
- `Lifecycle.DispatchLateTick`
- `Lifecycle.DispatchFixedTick`
- `Lifecycle.DispatchRelease`
- `Lifecycle.DispatchReset`

---

## レガシー移行ポリシー

レガシーな handler-interface パターンは、明示的なライフサイクル貢献と plan に移行しなければならない。

| レガシーパターン | 対象表現 |
|---|---|
| `.As<IScopeAcquireHandler>()` | `Acquire` phase の `LifecycleContribution` |
| `.As<IScopeReleaseHandler>()` | `Release` phase の `LifecycleContribution` |
| `.As<IScopeTickHandler>()` | `Tick` phase の `LifecycleContribution` |
| `.As<IScopeLateTickHandler>()` | `LateTick` phase の `LifecycleContribution` |
| `.As<IScopeFixedTickHandler>()` | `FixedTick` phase の `LifecycleContribution` |
| `RuntimeResolver.GetAcquireHandlers()` | 生成済み `LifecycleDispatchTable` |
| `RuntimeAcquireReleaseDispatcher` | `LifecycleDispatcher` |
| service が handler interface を実装している | enrollment ではない |

代表的な移行例:

- `TooltipChannelHubService`
  - `ServiceContribution`: tooltip hub
  - `LifecycleContribution`: acquire, release, tick
  - local tooltip player runtime は hub-owned の内部 state のまま
- `MeshChannelHubService`
  - `ServiceContribution`: mesh hub
  - `LifecycleContribution`: acquire, release, tick, dispose
  - mesh player runtime は hub-owned の内部 state のまま
- `AnimationSpriteHubService`
  - `ServiceContribution`: animation sprite hub
  - `LifecycleContribution`: acquire, release, tick
  - provider contract は lifecycle enrollment とは分離されたまま

これらの例は代表例にすぎない。
この移行ポリシーは、現行の handler-interface サービス群全体に広く適用される。

---

## 禁止パターン

対象 LifecyclePlan runtime で禁止されるもの:

- 実装済み interface によるライフサイクル enrollment
- `IScopeAcquireHandler` を対象とした ServiceGraph の走査
- `IScopeReleaseHandler` を対象とした ServiceGraph の走査
- `IScopeTickHandler` を対象とした ServiceGraph の走査
- `IReadOnlyList<IScopeTickHandler>` の解決
- ライフサイクル順序としての registration 順序
- runtime 生成のライフサイクル step
- acquire 中のライフサイクル handler 追加
- 既定でのエンティティ単位ライフサイクル step
- 既定での channel-player 単位ライフサイクル step
- fire-and-forget の非同期ライフサイクル作業
- ライフサイクル failure の握りつぶし
- 明示的な failure boundary なしに required acquire failure の後も継続すること
- ServiceGraph をライフサイクルレジストリとして使うこと
- CommandCatalog をライフサイクルレジストリとして使うこと

---

## テストケースモデル

各ライフサイクル runtime テストケースは次を定義しなければならない:

- Test ID
- Title
- LifecyclePlan fixture
- 必要に応じた ServiceGraphPlan と ScopeGraphPlan の fixture
- 関連する場合は selected profile
- テスト対象の operation
- 期待結果
- 期待される diagnostics
- 期待される failure boundary
- notes

例:

### TC_LIFE_001_InterfaceImplementationDoesNotEnroll

入力:

- service が acquire 風の interface を実装している
- ライフサイクル step は存在しない

操作:

- 含まれる scope に対して acquire をディスパッチする

期待結果:

- service は呼ばれない
- ライフサイクル runtime は plan 駆動のままである

---

## 必須テストケース

### A. Enrollment テスト

#### TC_LIFE_ENROLL_001_InterfaceImplementationDoesNotEnroll

入力:

- service が acquire 風の interface を実装している
- ライフサイクル step は存在しない

期待結果:

- acquire 中に service は呼ばれない

#### TC_LIFE_ENROLL_002_LifecycleStepEnrollsService

入力:

- lifecycle step が service `TooltipHub` を target にしている
- phase は `Acquire`

期待結果:

- tooltip hub の acquire action が呼ばれる

#### TC_LIFE_ENROLL_003_RegistrationScanForbidden

入力:

- runtime に handler interface を持つ service registration がある

期待結果:

- LifecycleDispatcher は registration を走査しない
- 試行された場合は `LIFECYCLE_REGISTRATION_SCAN_FORBIDDEN`

### B. Phase と Order のテスト

#### TC_LIFE_ORDER_001_DeterministicOrder

入力:

- 同じ phase に A、B、C の step があり、order が明示されている

期待結果:

- ディスパッチ順は A、B、C である

#### TC_LIFE_ORDER_002_OrderConflictRejected

入力:

- 2 つの required step が、同じ phase と order を持ち、tie policy がない

期待結果:

- validation failed
- `LIFECYCLE_STEP_ORDER_CONFLICT`

#### TC_LIFE_PHASE_001_AcquireBeforeActivate

入力:

- 同じ境界に acquire と activate の step がある

期待結果:

- acquire が activate より先に完了する

### C. Scope 境界テスト

#### TC_LIFE_SCOPE_001_ScopeStateTriggersAcquire

操作:

- ScopeGraph が acquiring state に遷移する

期待結果:

- LifecycleDispatcher が acquire dispatch table を実行する

#### TC_LIFE_SCOPE_002_InvalidScopeStateRejectsLifecycle

操作:

- 破棄済み scope に対して tick をディスパッチする

期待結果:

- failed
- `LIFECYCLE_INVALID_SCOPE_STATE`

#### TC_LIFE_SCOPE_003_LifecycleDoesNotMutateScopeStructure

操作:

- 有効な scope 境界に対してライフサイクルディスパッチを実行する

期待結果:

- ライフサイクルは副作用のみを行う
- scope の親子構造は ScopeGraph の所有のままである

### D. Tick テスト

#### TC_LIFE_TICK_001_TickDispatchNoAllocation

操作:

- 通常 tick を繰り返しディスパッチする

期待結果:

- 通常 path で managed allocation は発生しない

#### TC_LIFE_TICK_002_PerEntityTickRejected

入力:

- 10,000 個の entity tick step が生成される

期待結果:

- failed
- `LIFECYCLE_TICK_CARDINALITY_FORBIDDEN`

#### TC_LIFE_TICK_003_HubTickAllowed

入力:

- mesh hub に 1 つの tick step がある
- hub は内部で多数の player runtime を所有している

期待結果:

- passed
- player runtime ごとの step ではなく、hub tick step が 1 つだけ存在する

### E. Failure と Rollback のテスト

#### TC_LIFE_FAIL_001_AcquireFailureFailsScope

入力:

- acquire step が失敗する
- failure policy は `FailScope`

期待結果:

- scope は failed または inactive state になる
- rollback が必要な場合、完了済み acquire step は逆順で release される
- rollback failure は隠されず diagnostics として emit される

#### TC_LIFE_FAIL_002_BootFailureFailsKernel

入力:

- boot lifecycle step が失敗する
- failure policy は `FailKernel`

期待結果:

- kernel boot が失敗する

#### TC_LIFE_FAIL_003_ContinueWithErrorRequiresExplicitPolicy

入力:

- step failure が発生する
- `ContinueWithError` がない

期待結果:

- failed
- 既定の failure boundary が適用される

### F. Async テスト

#### TC_LIFE_ASYNC_001_UntrackedAsyncRejected

入力:

- lifecycle step が追跡されない async policy なしで `UniTask` 作業を開始する

期待結果:

- failed
- `LIFECYCLE_ASYNC_UNTRACKED`

#### TC_LIFE_ASYNC_002_TrackedAsyncTimeout

入力:

- async lifecycle step に timeout policy がある

期待結果:

- timeout は structured diagnostics を生成する
- 宣言された failure boundary が適用される

### G. Reset と Pooling のテスト

#### TC_LIFE_POOL_001_ResetBeforeReuseRequired

操作:

- reset lifecycle なしで pool 済み scope を再利用する

期待結果:

- failed
- `LIFECYCLE_RESET_REQUIRED_BEFORE_REUSE`

#### TC_LIFE_POOL_002_ResetClearsSubscriptions

入力:

- service が acquire 中に subscription する

操作:

- release、reset、再 acquire の順で行う

期待結果:

- 重複 subscription は残らない

#### TC_LIFE_POOL_003_ResetClearsTransientRuntimeState

入力:

- scope が一時的な runtime query または value state を蓄積する

操作:

- release、reset、再利用を行う

期待結果:

- 以前の寿命に属する transient state は消去される

### H. 移行テスト

#### TC_LIFE_MIGRATION_001_TooltipHubMigration

入力:

- tooltip hub に acquire、release、tick の挙動が必要である

期待結果:

- service contribution が hub を定義する
- lifecycle contribution が acquire、release、tick を定義する
- tooltip player runtime は直接の lifecycle target ではない

#### TC_LIFE_MIGRATION_002_MeshHubMigration

入力:

- mesh hub が多数の mesh player runtime を所有している

期待結果:

- 1 つの hub lifecycle target が存在する
- player runtime は内部で管理される

#### TC_LIFE_MIGRATION_003_AnimationSpriteInstallerMigration

入力:

- animation sprite hub が現在 service と lifecycle handler の両方として登録されている

期待結果:

- ライフサイクル参加は lifecycle contribution で表現される
- ServiceGraph は登録済み interface から participation を推論しない

---

## 受け入れ基準

08 は次を定義したときに完了である:

- LifecyclePlan の目的と権威
- ライフサイクル入力契約
- ライフサイクル識別語彙
- ライフサイクルフェーズモデル
- ライフサイクルステップモデル
- ライフサイクルターゲットモデル
- ライフサイクル順序モデル
- ライフサイクル依存関係モデル
- ライフサイクルディスパッチテーブルモデル
- スコープ状態境界
- ServiceGraph 境界
- ランタイムオブジェクト境界
- tick / fixed tick / late tick ポリシー
- エンティティ単位ライフサイクルの禁止
- 失敗ポリシー
- reset とプーリングのポリシー
- 非同期ライフサイクルポリシー
- 診断と DebugMap 要件
- 性能とメモリのポリシー
- レガシー移行ポリシー
- 禁止パターン
- ライフサイクルテストケースモデル
- 必須ライフサイクルテストケース

ライフサイクル参加が、インターフェース登録、registration scan の出力、またはエンティティごとの暗黙的 tick 拡張としてまだ読めるなら、この仕様は未完了である。

---

## テストケース

| テストケース | 目的 | 検証 |
|---|---|---|
| TC-08-01 | ライフサイクル enrollment が明示的であることを確認する。 | ライフサイクル権威と入力契約の節で、interface と registration ベースの enrollment を禁止しなければならない。 |
| TC-08-02 | ディスパッチが registration scan ではなく事前計算済み lifecycle table を使うことを確認する。 | ディスパッチテーブルと ServiceGraph 境界の節で、handler list 収集 API と runtime scan ベースのディスパッチを拒否しなければならない。 |
| TC-08-03 | scope、service、runtime object、query、value の境界が明示的に保たれていることを確認する。 | 境界の節で、ライフサイクル実行を scope 構造、service 発見、汎用 query lookup、value fallback から切り離しておかなければならない。 |
| TC-08-04 | tick 参加が予算化され、既定では entity ごとに増えないことを確認する。 | tick ポリシー、エンティティ単位禁止、性能節で、無制限なライフサイクル拡張を拒否しなければならない。 |
| TC-08-05 | 非同期ライフサイクル作業が追跡され、失敗は fail-closed であることを確認する。 | 非同期ポリシーと失敗ポリシーの節で、fire-and-forget 作業を禁止し、明示的な failure boundary を要求しなければならない。 |
| TC-08-06 | reset と pooling のライフサイクルが明示的で検証済みであることを確認する。 | reset と pooling の節で、再利用前の reset を要求し、古い subscription や transient state の漏れを拒否しなければならない。 |

---

## 最終見解

Lifecycle は、検証済みの plan から宣言され、検証され、ディスパッチされる。
runtime registration から発見されるものではない。

ライフサイクル参加は、service registration の創発的副作用ではない。
それは検証済みの runtime intent である。

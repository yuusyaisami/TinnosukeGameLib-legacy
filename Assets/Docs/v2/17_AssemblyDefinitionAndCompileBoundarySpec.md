# アセンブリ定義およびコンパイル境界仕様

## 文書ステータス

- 文書ID: 17_AssemblyDefinitionAndCompileBoundarySpec
- 状態: Draft
- 役割: Kernel v2 における asmdef レイヤーモデル、コンパイル境界の強制、依存方向ルール、外部パッケージ方針を定義する
- 依存元:
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
  - [13_LegacyCompatBoundarySpec.md](13_LegacyCompatBoundarySpec.md)
  - [14_PerformanceBudgetAndRuntimeRulesSpec.md](14_PerformanceBudgetAndRuntimeRulesSpec.md)
  - [15_TestAndValidationSpec.md](15_TestAndValidationSpec.md)
  - [16_ImplementationMilestoneOrderSpec.md](16_ImplementationMilestoneOrderSpec.md)
- 提供する基盤:
  - asmdef 分割、依存ゲート、コンパイル境界検証テストを実現するための実装作業

### 改訂メモ

この改訂は、17 を asmdef およびコンパイル境界の仕様として作成するものである。

16 が実装順序専用であるため、この文書はその後ろに番号付けされている。
本書は、実装順序をコンパイル時に強制可能にする依存グラフを担当する。

また、現在のコンパイル負債の状態も記録する。
GameLib のコードは依然として大きなモノリスとしてコンパイルされており、ワークスペースに見えている asmdef は、GameLib v2 の分割ではなく、主にサードパーティおよびツール用アセンブリである。

---

## 所有範囲

この仕様が所有するもの:

- アセンブリ層モデルと命名規則
- ランタイム / エディタ / テスト境界方針
- 純 C# / Unity ランタイム / Unity エディタの分離ルール
- kernel core のアセンブリモデル
- runtime subsystem のアセンブリモデル
- Unity authoring のアセンブリモデル
- feature module のアセンブリモデル
- legacy アセンブリの隔離方針
- 生成コードのアセンブリ方針
- 外部パッケージ依存方針
- 許可依存マトリクスと禁止依存マトリクス
- 循環依存回避方針
- テストアセンブリ方針
- asmdef 導入の移行順序
- 静的ルール強制と失敗ポリシー

この仕様が所有しないもの:

- KernelIR の意味論
- 依存検証の意味論
- 生成アルゴリズムの意味論
- 診断レコードの意味論
- ランタイム実行の意味論
- boot 受理の意味論
- ゲームプレイ機能の意味論
- CI ベンダー設定ファイル

17 はコンパイル境界を定義する。
ServiceGraph、ScopeGraph、LifecyclePlan、CommandCatalog、ValueStore、LegacyCompat の意味を再定義するものではない。

---

## 目的

この仕様は、Kernel v2 のコードベースをどのようにアセンブリへ分割し、依存方向を慣習ではなくコンパイラで強制するかを定義する。

中核となる記述:

```text
asmdef は単なるコンパイル最適化ではない。
asmdef はアーキテクチャ境界を強制するための手段である。

下位層は上位層を参照してはならない。
Runtime は Editor を参照してはならない。
Kernel core は Feature を参照してはならない。
Kernel core は Legacy を参照してはならない。
Diagnostics は共有してよいが、Kernel diagnostics の直接的な Unity Debug 出力は、必ず diagnostics 用の Unity sink アセンブリに隔離しなければならない。
```

アーキテクチャがアセンブリ参照を通じて無効な依存方向を許してしまうと、ランタイム設計はやがてその無効な方向を吸収してしまう。

17 の目的は、その崩れを高コストになる前に防ぐことにある。

---

## スコープ

この仕様が定義するもの:

- アセンブリ層の考え方
- アセンブリ命名規則
- Runtime / Editor / Test の境界
- 純 C# / Unity runtime / Unity editor の境界
- kernel core のアセンブリ集合
- runtime subsystem のアセンブリ集合
- Unity authoring のアセンブリ集合
- feature module のアセンブリ集合
- legacy 隔離用アセンブリ集合
- 生成コードのアセンブリ方針
- 外部パッケージ依存方針
- 許可依存マトリクス
- 禁止依存マトリクス
- 循環依存回避方針
- テストアセンブリ方針
- 移行順序
- 静的ルール強制
- 失敗ポリシー
- 必要なテストケース
- 受け入れ基準

この仕様は、ランタイムそのものの振る舞いは定義しない。
IR や検証アルゴリズムの詳細、最終的なシリアライズ形式、ゲームプレイ設計、CI やリリースパイプラインの YAML も定義しない。

17 はコンパイル境界の文書である。
ランタイムアーキテクチャ仕様を装うべきではない。

---

## 非目標

この仕様は以下を定義しない:

- すべてのアセンブリ分割を一度の作業で完了させること
- asmdef 移動と同時にすべてのフォルダ移動を行うこと
- アセンブリ閲覧ツールのエディタ UI 設計
- 下位サブシステム仕様の代替
- パッケージ依存が完全になくなることの保証
- 依存グラフを脆くするほど細かく分割すること

目的は断片化の最大化ではなく、明確な境界である。

---

## 他仕様との関係

| 仕様 | 関係 |
|---|---|
| [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md) | コンパイル境界がアセンブリレベルで守るべき根本制約を定義する。 |
| [01_KernelIRSpec.md](01_KernelIRSpec.md) | Unity から独立し、アセンブリとして安定していなければならない正規化データの権威を定義する。 |
| [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md) | ランタイムビルダーの変異に依存してはならない宣言的な寄与を定義する。 |
| [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md) | エディタ専用または feature 専用依存に汚染されるべきではない生成時の信頼境界を定義する。 |
| [04_DependencyValidationSpec.md](04_DependencyValidationSpec.md) | アセンブリグラフが依存グラフを反映していれば、最も強く強制しやすい検証ルールを定義する。 |
| [05_BootManifestAndProfileSpec.md](05_BootManifestAndProfileSpec.md) | feature や legacy とは分離されたままであるべき boot 方針と検証済みアーティファクト選択を定義する。 |
| [06_ServiceGraphRuntimeSpec.md](06_ServiceGraphRuntimeSpec.md) | DI コンテナ風のコンパイル依存の受け皿になってはならない、粗粒度の service runtime を定義する。 |
| [07_ScopeGraphRuntimeSpec.md](07_ScopeGraphRuntimeSpec.md) | Unity 階層や feature コードとは別に保たれるべき scope の権威を定義する。 |
| [08_LifecyclePlanSpec.md](08_LifecyclePlanSpec.md) | 具体的な feature アセンブリではなく、小さな境界インターフェースに依存すべき lifecycle dispatch を定義する。 |
| [09_CommandCatalogRuntimeSpec.md](09_CommandCatalogRuntimeSpec.md) | value、feature、legacy の実装詳細から切り離された command dispatch を定義する。 |
| [10_ValueSchemaAndStoreSpec.md](10_ValueSchemaAndStoreSpec.md) | command や feature の実装詳細に汚されてはならない value schema と store の意味論を定義する。 |
| [10_1_ScalarRuntimeAndBindingSpec.md](10_1_ScalarRuntimeAndBindingSpec.md) | リーフアセンブリとして残すべき scalar specialization を定義する。 |
| [10_2_DynamicValueEvaluationSpec.md](10_2_DynamicValueEvaluationSpec.md) | core の足場ではなく、runtime のリーフコードとして残すべき dynamic evaluation を定義する。 |
| [11_DebugMapAndDiagnosticsSpec.md](11_DebugMapAndDiagnosticsSpec.md) | Kernel diagnostics に対する Unity Debug 出力を 1 つのアセンブリに限定するルールを含む、診断の所有権を定義する。 |
| [12_UnityAuthoringBridgeSpec.md](12_UnityAuthoringBridgeSpec.md) | エディタアセンブリに隔離されるべき authoring 抽出と direct-play ブリッジのルールを定義する。 |
| [13_LegacyCompatBoundarySpec.md](13_LegacyCompatBoundarySpec.md) | kernel core が逆方向に越えてはならない quarantine 境界を定義する。 |
| [14_PerformanceBudgetAndRuntimeRulesSpec.md](14_PerformanceBudgetAndRuntimeRulesSpec.md) | アセンブリグラフにも反映されるべき runtime path と禁止操作ルールを定義する。 |
| [15_TestAndValidationSpec.md](15_TestAndValidationSpec.md) | アセンブリ境界と依存回帰を検証すべき実行可能なガードモデルを定義する。 |
| [16_ImplementationMilestoneOrderSpec.md](16_ImplementationMilestoneOrderSpec.md) | asmdef 分割を runtime-rich な subsystem 拡張より先に置くべき実装順序を定義する。 |

17 は runtime subsystem 仕様ではない。
runtime subsystem 仕様を正直に保つための依存の壁である。

---

## 現在のコンパイル負債の観測

この節は、asmdef 分割の必要性を示す現在のコードベース状態を記録する。

### 観測の追跡可能性

| 観測 | 証拠種別 | 圧力 |
|---|---|---|
| GameLib ソースツリー下に GameLib 用 asmdef は見当たらなかった。 | ワークスペース検索 | 現在の GameLib コードは依然として大きなモノリスとして振る舞っているため、M17、M15、M16 の圧力が高い |
| 見えている asmdef は主にサードパーティまたはツール用アセンブリである。 | ワークスペース検索 | 目標とする kernel 分割には独自のアセンブリグラフがまだ必要 |
| プロジェクトマニフェストには UniTask、InputSystem、VContainer、URP、uGUI、Unity Test Framework が含まれている。 | `Packages/manifest.json` | 外部パッケージへのアクセスはレイヤーごとに明示される必要がある |
| 生成済みソリューションは、Unity が生成した csproj 内のハードコードされた analyzer パスで早期失敗しうる。 | ビルドログ / 環境証拠 | コンパイル境界の作業は、環境固有の analyzer 漏れと結びつけるべきではない |

### 代表的なアンカー

- [Packages/manifest.json](../../../Packages/manifest.json) - UniTask、InputSystem、VContainer、URP、uGUI、Unity Test Framework の依存を示すパッケージグラフ
- [Assets/vInspector/VInspector.asmdef](../../vInspector/VInspector.asmdef) - ワークスペース内にある既存ツール用 asmdef の例
- [Assets/vHierarchy/VHierarchy.asmdef](../../vHierarchy/VHierarchy.asmdef) - ワークスペース内にある既存ツール用 asmdef の例
- [Assets/vFolders/VFolders.asmdef](../../vFolders/VFolders.asmdef) - ワークスペース内にある既存ツール用 asmdef の例
- [Assets/GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs) - 現在の GameLib runtime モノリスの圧力点
- [Assets/GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs](../../GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs) - 現在の GameLib runtime モノリスの圧力点
- [Assets/GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs](../../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs) - 現在の command registration 圧力点
- [Assets/GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs](../../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs) - 現在の value system 圧力点
- [Assets/GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs](../../GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs) - 現在の loading fallback 圧力点
- [Assets/GameLib/Script/Common/LTS/LTSLog.cs](../../GameLib/Script/Common/LTS/LTSLog.cs) - 現在の Unity ロギング圧力点

### コンパイル負債の要約

現在の構造は、既定で次の失敗モードを許している:

- feature コードが kernel core のコンパイル経路へ漏れる
- Editor コードが runtime コンパイル経路へ漏れる
- legacy コードが参照を通じて v2 core から到達可能になる
- アセンブリが明示化されていないため、外部パッケージが誤ったレイヤーへ広がる
- 直接ロギング経路が、コードベース全体で中央集約されないまま残る

asmdef 分割は、これらの経路を閉じるためのコンパイル時手段である。

---

## アセンブリ層の考え方

アセンブリグラフは、理解しやすい程度に浅く、かつ強制可能なほど厳密であるべきだ。

```text
Core データと契約を最初に置く。
次に runtime orchestration を置く。
Unity bridge と authoring は、エンジンが本当に必要な場所にだけ置く。
Feature はリーフにのみ置く。
Legacy は quarantine にのみ置く。
Test は消費者としてのみ振る舞う。
```

設計は、型付き ID、診断契約、IR、検証ルールのような低変動で安定したアセンブリを優先すべきである。

authoring、feature ロジック、エディタ UI、runtime integration のような高変動コードは、変更のたびに core グラフ全体を再コンパイルさせないよう、リーフアセンブリに置くべきだ。

---

## アセンブリ命名規則

単一の namespace プレフィックスにレイヤー接尾辞を付ける。

推奨規則:

- `GameLib.Foundation`
- `GameLib.Foundation.Unity`
- `GameLib.Foundation.Editor`
- `GameLib.Kernel.Diagnostics`
- `GameLib.Kernel.Diagnostics.Unity`
- `GameLib.Kernel.Diagnostics.Editor`
- `GameLib.Kernel.Abstractions`
- `GameLib.Kernel.IR`
- `GameLib.Kernel.Contributions`
- `GameLib.Kernel.Validation`
- `GameLib.Kernel.Generation`
- `GameLib.Kernel.Boot`
- `GameLib.Kernel.Boot.Unity`
- `GameLib.Kernel.Runtime`
- `GameLib.Kernel.ServiceGraph`
- `GameLib.Kernel.ScopeGraph`
- `GameLib.Kernel.Lifecycle`
- `GameLib.Kernel.Command`
- `GameLib.Kernel.Value`
- `GameLib.Kernel.Value.Scalar`
- `GameLib.Kernel.Value.Dynamic`
- `GameLib.Kernel.RuntimeQuery`
- `GameLib.Kernel.Unity`
- `GameLib.Kernel.Authoring`
- `GameLib.Kernel.Authoring.Editor`
- `GameLib.Features.*`
- `GameLib.Legacy.*`
- `GameLib.Tests.*`

ルール:

- `rootNamespace` はアセンブリ名と一致させる。
- `Editor` アセンブリは `includePlatforms: [Editor]` を使う。
- リーフの runtime アセンブリは、広い friend access よりも狭い public surface を優先する。
- 生成アセンブリを使う場合は、core に溶け込ませず、生成成果物であることを明示する。

---

## Runtime / Editor / Test 境界

最も重要なコンパイル区分は、runtime、editor、test の 3 つである。

- Runtime アセンブリは `UnityEditor` を参照してはならない。
- Editor アセンブリは runtime アセンブリを参照してよいが、runtime アセンブリは Editor アセンブリを参照してはならない。
- Test アセンブリは必要に応じて runtime と Editor を参照してよいが、production アセンブリは test を参照してはならない。
- `GameLib.Kernel.Diagnostics.Unity` と `GameLib.Kernel.Authoring.Editor` は、エンジン固有の kernel 作業のための適切な集中点である。

この境界は、Editor 専用の利便性が runtime 依存に変質するのを防ぐためにある。

---

## 純 C# / Unity Runtime / Unity Editor 境界

kernel は既定で純 C# であるべきだ。

推奨の既定:

- `GameLib.Foundation`、`GameLib.Kernel.Diagnostics`、`GameLib.Kernel.Abstractions`、`GameLib.Kernel.IR`、`GameLib.Kernel.Contributions`、`GameLib.Kernel.Validation`、`GameLib.Kernel.Generation`、`GameLib.Kernel.Boot`、`GameLib.Kernel.Runtime`、`GameLib.Kernel.ServiceGraph`、`GameLib.Kernel.ScopeGraph`、`GameLib.Kernel.Lifecycle`、`GameLib.Kernel.Command`、`GameLib.Kernel.Value`、`GameLib.Kernel.Value.Scalar`、`GameLib.Kernel.Value.Dynamic`、`GameLib.Kernel.RuntimeQuery` では `noEngineReferences: true` を優先する。
- `UnityEngine` へのアクセスは、本当に必要な Unity bridge、authoring、feature、compatibility アセンブリにだけ隔離する。
- `UnityEditor` へのアクセスは、Editor 専用アセンブリに隔離する。
- 直接の `Debug.Log*` 利用は `GameLib.Kernel.Diagnostics.Unity` に隔離する。

純 core アセンブリが `UnityEngine` を必要とする場合は、参照を追加する前に設計を見直すべきだ。

---

## Kernel Core アセンブリモデル

core kernel アセンブリは、小さく、明示的で、安定しているべきだ。

| アセンブリ | 主責務 | 補足 |
|---|---|---|
| `GameLib.Foundation` | 型付き ID 基本型、Result/Error、ハッシュ、決定論的ヘルパー | Unity 参照なし |
| `GameLib.Foundation.Unity` | Unity bridge の基本型と profiler ラッパー | Unity 安全な基本型のみ |
| `GameLib.Foundation.Editor` | GUID、local file ID、path ヘルパー | Editor 専用 |
| `GameLib.Kernel.Diagnostics` | `KernelDiagnostic`、code、severity、domain、failure boundary、context、sink インターフェース、in-memory / test sink | Unity へ直接ロギングしない |
| `GameLib.Kernel.Diagnostics.Unity` | `UnityLogDiagnosticSink` と、kernel diagnostics に対する唯一の正当な Unity Debug 出力経路 | 許可された Unity 出力のみ |
| `GameLib.Kernel.Diagnostics.Editor` | diagnostics 閲覧、asset ナビゲーション、source lookup | Editor 専用 |
| `GameLib.Kernel.Abstractions` | kernel 固有の型付き ID、共有の小さな契約、レイヤー横断の境界インターフェース | インターフェースは小さく保つ |
| `GameLib.Kernel.IR` | 正規化された KernelIR と source-traceable な依存構造 | runtime 実行ロジックなし |
| `GameLib.Kernel.Contributions` | KernelIR に正規化される宣言的 module contribution | runtime builder の変異なし |
| `GameLib.Kernel.Validation` | 依存正当性、重複検査、循環検査、検証レポート | runtime repair なし |
| `GameLib.Kernel.Generation` | 生成済みおよび検証済みの plan、artifact header、hash 検証、決定論的生成 | scene search や runtime fallback なし |
| `GameLib.Kernel.Boot` | boot 方針、boot manifest モデル、profile 選択、検証済み artifact 受理 | 純粋な boot 契約 |

---

## Runtime サブシステムのアセンブリモデル

runtime サブシステムのアセンブリは、feature や legacy ではなく core kernel に依存するべきだ。

| アセンブリ | 主責務 | 補足 |
|---|---|---|
| `GameLib.Kernel.Runtime` | runtime session、共有ハンドル、runtime orchestration | 最小限の orchestration のみ |
| `GameLib.Kernel.ServiceGraph` | 粗粒度の検証済み service resolve | 汎用 DI コンテナではない |
| `GameLib.Kernel.ScopeGraph` | 明示的な scope の所有、ハンドル、状態 | Unity 階層は真実ではない |
| `GameLib.Kernel.Lifecycle` | 検証済み lifecycle dispatch table と phase | registration scan はしない |
| `GameLib.Kernel.Command` | table 駆動の command identity、payload、dispatch | string dispatch はしない |
| `GameLib.Kernel.Value` | schema と store の境界、slot lookup、初期化、save 方針 | stable-key の runtime fallback なし |
| `GameLib.Kernel.Value.Scalar` | float 専用の scalar runtime | リーフ specialization |
| `GameLib.Kernel.Value.Dynamic` | dynamic および reactive evaluation | リーフ specialization |
| `GameLib.Kernel.RuntimeQuery` | 明示的な runtime query と lookup 契約 | global search layer ではない |

runtime サブシステムのアセンブリは、共有された plan や handle を参照してよいが、下位仕様で明示的に必要とされていない限り、互いに直接依存してはならない。

---

## Unity Authoring のアセンブリモデル

Unity authoring と runtime は、同じコンパイル単位であってはならない。

| アセンブリ | 主責務 | 補足 |
|---|---|---|
| `GameLib.Kernel.Unity` | Unity object bridge、MonoBehaviour runtime link、scene binding | runtime bridge のみ |
| `GameLib.Kernel.Authoring` | authoring component と serialized declaration data | 宣言のみ |
| `GameLib.Kernel.Authoring.Editor` | 抽出、検証 UI、source 収集、direct-play 準備 | Editor 専用 |
| `GameLib.Kernel.Generation.Editor` | 生成ファイル書き込みと決定論的な editor generation | Editor 専用 |
| `GameLib.Kernel.Boot.Unity` | ScriptableObject boot asset と Unity boot entry | boot 契約を engine glue から分離する |

authoring 側は runtime 構造を記述してよいが、runtime 構造を直接組み立ててはならない。

---

## Feature Module のアセンブリモデル

feature アセンブリはグラフの端に置くべきだ。

| アセンブリ群 | 例 | 補足 |
|---|---|---|
| `GameLib.Features.UI` | modal stack、tooltip、UI element bridge | 必要に応じて kernel の public API と uGUI に依存してよい |
| `GameLib.Features.SceneChannels` | mesh channel、animation sprite channel、material FX 対象 | runtime player を kernel core に入れない |
| `GameLib.Features.Audio` | audio feature module | feature のリーフ |
| `GameLib.Features.SceneFlow` | loading と scene flow 機能 | feature のリーフ |
| `GameLib.Features.Collision` | collision 機能 | feature のリーフ |
| `GameLib.Features.Save` | save 機能 | value 契約には依存してよいが、value core の内部には依存しない |

feature アセンブリは、kernel の public API と必要な Unity パッケージを参照してよい。
ただし、kernel core が feature の内部を学習するための経路になってはならない。

---

## Legacy アセンブリの隔離

legacy コードは、隔離された migration 境界としてのみ許可される。

| アセンブリ群 | 許可される用途 | 補足 |
|---|---|---|
| `GameLib.Legacy` | legacy ルート隔離領域 | target runtime API ではない |
| `GameLib.Legacy.LTS` | legacy lifetime-scope の移行支援 | 移行のみ |
| `GameLib.Legacy.Commands` | legacy command の移行支援 | 移行のみ |
| `GameLib.Legacy.Blackboard` | legacy value の移行支援 | 移行のみ |
| `GameLib.Legacy.Compat` | 明示的な adapter と bridge | 一方向のみ |
| `GameLib.Legacy.Editor` | 移行ツール | Editor 専用 |

ルール:

- `GameLib.Kernel.*` は `GameLib.Legacy.*` を参照してはならない。
- `GameLib.Legacy.*` は public kernel API のみに参照を許される。
- Legacy は fallback repair path ではない。

---

## 生成コードのアセンブリ方針

生成出力はコンパイルグラフを広げるべきではない。

ルール:

- 生成コードは、core グラフの再コンパイルを強制しないリーフアセンブリか生成アセットに保持する。
- 生成アセンブリは、kernel 意味論の source of truth を定義してはならない。
- core kernel は、大きな生成コード面に依存するよりも、検証済み生成データや最小限の生成 accessor を読むことを優先する。
- 生成アセンブリが不安定になった場合は、core に押し込むのではなく、出力を data または asset 形式へ戻すべきだ。

予約候補名:

- `GameLib.Generated.KernelPlans`
- `GameLib.Generated.ValueKeys`
- `GameLib.Generated.CommandIds`

これらの名前は、生成面が安定していて初めて使う予約名である。

---

## 外部パッケージ依存方針

外部パッケージは、実際に必要とするレイヤーにだけ隔離しなければならない。

| パッケージ | 許可レイヤー | 禁止レイヤー |
|---|---|---|
| UniTask | `GameLib.Kernel.Lifecycle`、`GameLib.Kernel.Command`、`GameLib.Kernel.Value.Dynamic`、`GameLib.Features.*` | `GameLib.Foundation`、`GameLib.Kernel.Abstractions`、`GameLib.Kernel.IR`、`GameLib.Kernel.Contributions`、`GameLib.Kernel.Validation`、`GameLib.Kernel.Generation`、`GameLib.Kernel.Boot` |
| InputSystem | 入力を明示的に所有する feature または authoring のリーフアセンブリ | core kernel アセンブリ |
| URP | rendering feature アセンブリのみ | core kernel アセンブリ |
| uGUI / UnityEngine.UI | UI feature アセンブリのみ | core kernel アセンブリ |
| VContainer | quarantine のために必要なら `GameLib.Legacy.*` のみ | すべての v2 kernel core アセンブリ |
| Odin | authoring および editor アセンブリのみ | core kernel アセンブリ |
| Unity Test Framework | `GameLib.Tests.*` のみ | production アセンブリ |

パッケージが core アセンブリに現れた場合、その asmdef 分割は緩すぎる。

---

## 許可依存マトリクス

このマトリクスは、意図された正の依存を示す。

| アセンブリ | 参照してよいもの |
|---|---|
| `GameLib.Foundation` | なし |
| `GameLib.Foundation.Unity` | `GameLib.Foundation`、UnityEngine |
| `GameLib.Foundation.Editor` | `GameLib.Foundation`、`GameLib.Foundation.Unity`、UnityEditor |
| `GameLib.Kernel.Diagnostics` | `GameLib.Foundation` |
| `GameLib.Kernel.Diagnostics.Unity` | `GameLib.Foundation`、`GameLib.Foundation.Unity`、`GameLib.Kernel.Diagnostics` |
| `GameLib.Kernel.Diagnostics.Editor` | `GameLib.Foundation`、`GameLib.Foundation.Editor`、`GameLib.Kernel.Diagnostics`、`GameLib.Kernel.Diagnostics.Unity` |
| `GameLib.Kernel.Abstractions` | `GameLib.Foundation`、`GameLib.Kernel.Diagnostics` |
| `GameLib.Kernel.IR` | `GameLib.Foundation`、`GameLib.Kernel.Diagnostics`、`GameLib.Kernel.Abstractions` |
| `GameLib.Kernel.Contributions` | `GameLib.Foundation`、`GameLib.Kernel.Diagnostics`、`GameLib.Kernel.Abstractions`、`GameLib.Kernel.IR` |
| `GameLib.Kernel.Validation` | `GameLib.Foundation`、`GameLib.Kernel.Diagnostics`、`GameLib.Kernel.Abstractions`、`GameLib.Kernel.IR`、`GameLib.Kernel.Contributions` |
| `GameLib.Kernel.Generation` | `GameLib.Foundation`、`GameLib.Kernel.Diagnostics`、`GameLib.Kernel.Abstractions`、`GameLib.Kernel.IR`、`GameLib.Kernel.Contributions`、`GameLib.Kernel.Validation` |
| `GameLib.Kernel.Boot` | `GameLib.Foundation`、`GameLib.Kernel.Diagnostics`、`GameLib.Kernel.Abstractions`、`GameLib.Kernel.IR`、`GameLib.Kernel.Contributions`、`GameLib.Kernel.Validation`、`GameLib.Kernel.Generation` |
| `GameLib.Kernel.Boot.Unity` | `GameLib.Foundation.Unity`、`GameLib.Kernel.Diagnostics.Unity`、`GameLib.Kernel.Boot` |
| `GameLib.Kernel.Runtime` | `GameLib.Foundation`、`GameLib.Kernel.Diagnostics`、`GameLib.Kernel.Abstractions`、`GameLib.Kernel.Validation`、`GameLib.Kernel.Generation`、`GameLib.Kernel.Boot` |
| `GameLib.Kernel.ServiceGraph` | `GameLib.Foundation`、`GameLib.Kernel.Diagnostics`、`GameLib.Kernel.Abstractions`、`GameLib.Kernel.Runtime` |
| `GameLib.Kernel.ScopeGraph` | `GameLib.Foundation`、`GameLib.Kernel.Diagnostics`、`GameLib.Kernel.Abstractions`、`GameLib.Kernel.Runtime` |
| `GameLib.Kernel.Lifecycle` | `GameLib.Foundation`、`GameLib.Kernel.Diagnostics`、`GameLib.Kernel.Abstractions`、`GameLib.Kernel.Runtime`、`GameLib.Kernel.ServiceGraph`、`GameLib.Kernel.ScopeGraph` |
| `GameLib.Kernel.Command` | `GameLib.Foundation`、`GameLib.Kernel.Diagnostics`、`GameLib.Kernel.Abstractions`、`GameLib.Kernel.Runtime`、`GameLib.Kernel.ServiceGraph`、`GameLib.Kernel.ScopeGraph`、`GameLib.Kernel.RuntimeQuery` |
| `GameLib.Kernel.Value` | `GameLib.Foundation`、`GameLib.Kernel.Diagnostics`、`GameLib.Kernel.Abstractions`、`GameLib.Kernel.Runtime`、`GameLib.Kernel.ScopeGraph`、`GameLib.Kernel.RuntimeQuery` |
| `GameLib.Kernel.Value.Scalar` | `GameLib.Kernel.Value`、`GameLib.Kernel.ScopeGraph`、`GameLib.Kernel.Diagnostics` |
| `GameLib.Kernel.Value.Dynamic` | `GameLib.Kernel.Value`、`GameLib.Kernel.RuntimeQuery`、`GameLib.Kernel.Diagnostics` |
| `GameLib.Kernel.RuntimeQuery` | `GameLib.Foundation`、`GameLib.Kernel.Diagnostics`、`GameLib.Kernel.Abstractions`、`GameLib.Kernel.ScopeGraph` |
| `GameLib.Kernel.Unity` | `GameLib.Foundation.Unity`、`GameLib.Kernel.Diagnostics.Unity`、`GameLib.Kernel.Runtime`、`GameLib.Kernel.ScopeGraph` |
| `GameLib.Kernel.Authoring` | `GameLib.Foundation.Unity`、`GameLib.Kernel.Abstractions`、`GameLib.Kernel.Contributions`、`GameLib.Kernel.Diagnostics` |
| `GameLib.Kernel.Authoring.Editor` | `GameLib.Foundation`、`GameLib.Foundation.Unity`、`GameLib.Foundation.Editor`、`GameLib.Kernel.Diagnostics`、`GameLib.Kernel.Diagnostics.Editor`、`GameLib.Kernel.Abstractions`、`GameLib.Kernel.Authoring`、`GameLib.Kernel.Contributions`、`GameLib.Kernel.IR`、`GameLib.Kernel.Validation`、`GameLib.Kernel.Generation` |
| `GameLib.Features.*` | kernel の public API と、feature に必要な個別の Unity パッケージ |
| `GameLib.Legacy.*` | quarantine bridge に必要な public kernel API |
| `GameLib.Tests.*` | 対象となる production アセンブリと、Unity Test Framework およびその他の test-only 依存 |

---

## 禁止依存マトリクス

以下の関係は禁止される。

| アセンブリ | 参照してはならないもの |
|---|---|
| `GameLib.Foundation` | UnityEngine、UnityEditor、kernel subsystem アセンブリ、feature アセンブリ、legacy アセンブリ |
| `GameLib.Kernel.Diagnostics` | 直接的な Unity Debug 出力 API、feature アセンブリ、legacy アセンブリ |
| `GameLib.Kernel.Diagnostics.Unity` | feature アセンブリ、legacy アセンブリ |
| `GameLib.Kernel.Abstractions` | runtime 実装、UnityEditor、feature アセンブリ、legacy アセンブリ |
| `GameLib.Kernel.IR` | runtime 実装、UnityEditor、feature アセンブリ、legacy アセンブリ |
| `GameLib.Kernel.Contributions` | runtime builder 変異 API、scene search API、feature アセンブリ、legacy アセンブリ |
| `GameLib.Kernel.Validation` | runtime 変異、feature アセンブリ、legacy アセンブリ |
| `GameLib.Kernel.Generation` | scene search、runtime fallback repair、feature アセンブリ、legacy アセンブリ |
| `GameLib.Kernel.Boot` | feature アセンブリ、legacy fallback repair、scene discovery ヘルパー |
| `GameLib.Kernel.Runtime` | Editor 専用 API、test、feature アセンブリ、legacy アセンブリ |
| `GameLib.Kernel.ServiceGraph` | command executor discovery、feature アセンブリ、legacy アセンブリ |
| `GameLib.Kernel.ScopeGraph` | `Transform.parent` を真実として扱うこと、feature アセンブリ、legacy アセンブリ |
| `GameLib.Kernel.Lifecycle` | interface scanning、feature アセンブリ、legacy アセンブリ |
| `GameLib.Kernel.Command` | 巨大 installer パターン、string dispatch、feature アセンブリ、legacy アセンブリ |
| `GameLib.Kernel.Value` | command の具体実装、stable-key の runtime fallback、feature アセンブリ、legacy アセンブリ |
| `GameLib.Kernel.Unity` | feature の内部、legacy の内部 |
| `GameLib.Kernel.Authoring` | runtime 変異 API |
| `GameLib.Kernel.Authoring.Editor` | runtime fallback repair |
| `GameLib.Features.*` | kernel core の内部と legacy の内部 |
| `GameLib.Legacy.*` | `GameLib.Kernel.*` からの直接依存すべて |
| `GameLib.Tests.*` | production アセンブリが tests に依存すること |

許可マトリクスに載っていない依存は、追加前に疑わしいものとして扱い、レビューすべきである。

---

## 循環依存回避方針

レイヤー間インターフェースの表面積は小さく保つ。

ルール:

- 2 つの runtime レイヤーが互いの具体型を知らずに協調する必要がある場合は、小さな境界インターフェースを `GameLib.Kernel.Abstractions` に置く。
- 具体的な service 参照よりも、plan と handle 参照を優先する。
- `Command` は value の具体実装ではなく value access 契約に話しかける。
- `Lifecycle` は command の具体実装ではなく lifecycle target 契約に話しかける。
- `ServiceGraph` と `ScopeGraph` は、所有の裏技ではなく handle と plan を通じて整合させる。

境界インターフェースが大きくなりすぎたら、隠れたクロスアセンブリ結合の受け皿になる前に分割する。

---

## テストアセンブリ方針

テストは production グラフの消費者である。

推奨テストアセンブリ:

- `GameLib.Tests.Common`
- `GameLib.Tests.Foundation`
- `GameLib.Tests.Diagnostics`
- `GameLib.Tests.IR`
- `GameLib.Tests.Validation`
- `GameLib.Tests.Generation`
- `GameLib.Tests.Boot`
- `GameLib.Tests.ServiceGraph`
- `GameLib.Tests.ScopeGraph`
- `GameLib.Tests.Lifecycle`
- `GameLib.Tests.Command`
- `GameLib.Tests.Value`
- `GameLib.Tests.Authoring.Editor`
- `GameLib.Tests.Legacy`
- `GameLib.Tests.Performance`
- `GameLib.Tests.Integration.PlayMode`

ルール:

- Test アセンブリは、検証対象の production アセンブリを参照してよい。
- Production アセンブリは、test アセンブリを参照してはならない。
- 共有ヘルパーは runtime コードではなく、test 専用ユーティリティアセンブリに置く。

---

## 移行順序

アセンブリ分割は制御された順序で導入する。

1. 現在のコンパイルエッジを棚卸しし、モノリシックな GameLib コードパスを特定する。
2. 最も変動が少ないアセンブリから作る。Foundation、Diagnostics、Abstractions を最初に作る。
3. IR、Contributions、Validation、Generation、Boot をそれらの基盤アセンブリへ移す。
4. runtime core と runtime subsystem を分割する。
5. Unity bridge と authoring アセンブリを追加する。
6. legacy を明示的な adapter アセンブリへ隔離する。
7. test アセンブリとコンパイル境界テストを追加する。
8. その後でのみ、feature module と、独自アセンブリに値するほど安定した生成コード面を分割する。

feature アセンブリから始めてはならない。
legacy adapter から始めてはならない。
core グラフをモノリシックに残したまま広い runtime split を始めてはならない。

---

## 静的ルール強制

アセンブリグラフは、asmdef 設定と実行可能テストの組み合わせで強制されるべきだ。

最低限の確認項目:

- core asmdef は、明示的な Unity bridge または Editor アセンブリでない限り `noEngineReferences: true` を持つこと
- `UnityEditor` 参照は Editor アセンブリに限定すること
- `Debug.Log*` 呼び出しは `GameLib.Kernel.Diagnostics.Unity` に限定すること
- `VContainer` 参照は legacy 隔離境界に留めること
- `Unity Test Framework` 参照は `GameLib.Tests.*` に留めること
- `Feature` アセンブリが kernel 内部を逆参照できないこと
- `Legacy` アセンブリが `GameLib.Kernel.*` から参照されないこと

静的ルールテストは、実際の asmdef グラフを許可依存マトリクスと比較すべきだ。

---

## 失敗ポリシー

コンパイル境界違反は、無害なリファクタの不便ではなく、アーキテクチャ失敗である。

新しい参照がマトリクスに違反する場合:

1. 変更を却下する
2. 不足していた境界インターフェースを特定する
3. 共有契約を下位へ移す
4. あるいは実装を正しいリーフアセンブリへ上げる

パッケージ依存を分離できないなら、その設計は誤っているので、参照を黙って追加してはならない。

ビルドグラフが明示的でなければ、kernel も明示的であり続けられない。

---

## 必要なテストケース

| テストケース | 目的 | 検証 |
|---|---|---|
| TC-17-01 | kernel core が既定で Unity から独立していることを確認する。 | Foundation、Abstractions、IR、Contributions、Validation、Generation、Boot、Runtime の各 asmdef は UnityEngine または UnityEditor を直接参照してはならない。 |
| TC-17-02 | 直接的な Unity Debug 出力が隔離されていることを確認する。 | kernel diagnostics に関する直接 `Debug.Log*` 呼び出しは、`GameLib.Kernel.Diagnostics.Unity` のみが持てる。 |
| TC-17-03 | legacy が隔離されたままであることを確認する。 | `GameLib.Kernel.*` の asmdef は `GameLib.Legacy.*` を参照してはならない。legacy asmdef は public kernel API のみを参照してよい。 |
| TC-17-04 | 外部パッケージ利用がレイヤーごとに分離されていることを確認する。 | UniTask、InputSystem、URP、uGUI、VContainer、Odin、Unity Test Framework の参照は、許可されたレイヤーにのみ現れなければならない。 |
| TC-17-05 | テストアセンブリが消費者に徹していることを確認する。 | `GameLib.Tests.*` の asmdef は production アセンブリを参照してよいが、production 側の asmdef が test asmdef を参照してはならない。 |

---

## 受け入れ基準

この仕様は、以下がすべて真であるときに完了する。

- アセンブリ命名規則が固定され、kernel レイヤーに asmdef 名が割り当てられている。
- 許可および禁止の依存マトリクスが、レビューとテストで使える程度に明示されている。
- 適切な箇所では `noEngineReferences: true` を使って core アセンブリを作成できる。
- Diagnostics の Unity 出力が 1 つの sink アセンブリに隔離されている。
- Runtime、Editor、Feature、Legacy、Test の境界が、コンパイル時参照で分離されている。
- 移行順序が、巨大なモノリスの churn なしに段階的に実装できるほど具体的である。
- README または依存マトリクスが新しい仕様を忘れたときに、doc test suite がそれを検出できる。

目標は単にコンパイルを速くすることではない。
目標は、v2 アーキテクチャを破りにくくするコンパイルグラフである。

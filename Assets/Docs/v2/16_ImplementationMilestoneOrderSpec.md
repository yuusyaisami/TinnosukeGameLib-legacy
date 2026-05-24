# 実装マイルストーン順序仕様

## 文書ステータス

- 文書ID: 16_ImplementationMilestoneOrderSpec
- 状態: Draft
- 役割: GameLib Kernel v2 の実現における実装フェーズの順序、マイルストーンゲート、禁止シーケンスを定義する
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
- 提供する基盤:
  - 00 から 15 までを、trust boundary を壊さずに実現するための実装計画、アーキテクチャレビュー、移行管理、ゲート順序

### 改訂メモ

この改訂は、Kernel v2 における実装順序仕様として 16 を作成する。

この仕様は 1 つのルールを明確化する:
仕様番号は実装順序そのものではない。

Diagnostics、test gate、静的な禁止パターン検出、正規化された IR、検証、検証済み生成、boot 受理は、runtime を多く含む subsystem より先に存在しなければならない。

また、実装を大規模に始める前に必要な文書整備、すなわちクロス仕様の概念責務、禁止パターン台帳、既存アンカー目録も記録する。

---

## 所有範囲

この仕様が所有するもの:

- M0 から M15 までの実装マイルストーン順序
- マイルストーンの目的、必要な出力、入口前提、出口ゲート
- 00 から 15 へのクロス仕様依存マッピング
- 移行実行における禁止シーケンスとアンチパターン
- 計画済み作業項目から下位仕様の検証点へのマイルストーン追跡性
- proof と trust boundary のマイルストーンが整うまで runtime subsystem 作業を止めるルール

この仕様が所有しないもの:

- 00 から 15 までがすでに所有している subsystem の意味論
- 最終的な runtime API のシグネチャ
- 実装成果物の正確なクラス名、namespace 名、ファイル名
- sprint 計画、人員割り当て、工数見積もり
- CI ベンダー設定ファイル
- ここで定義された代表的な移行ウェーブ以外のゲームプレイ優先順位

16 は実行順序を定義する。
ServiceGraph、ScopeGraph、LifecyclePlan、CommandCatalog、ValueStore、DebugMap、BootManifest の意味を再定義するものではない。

---

## 目的

この仕様は、Kernel v2 をどのような順番で実装すべきかを定義する。

中核となる記述:

```text
仕様番号は実装順序ではない。

diagnostics、tests、static gate、IR 正規化、検証、検証済み生成、boot 受理のゲートが整う前に、いかなる runtime core マイルストーンも target path になってはならない。

マイルストーンは、その出力が下位仕様のルールに追跡可能であり、実行可能または文書化可能なゲートで裏付けられている場合にのみ完了である。
```

00 は、runtime discovery なし、registration からの lifecycle 推測なし、runtime stable-key fallback なし、未検証アーティファクトを信頼しないこと、許可された sink 以外での Unity 直接ロギングなし、というルート制約を定義する。

01 は `KernelIR` を runtime 形式ではなく正規化された権威として定義する。

11 と 15 は、diagnostics と実行可能な保護が後付けの飾りではなくアーキテクチャ要件であることを定義する。

したがって実装順序は、runtime の豊かさを積む前に proof boundary と trust boundary を作ることから始めなければならない。

`ServiceGraph`、`ScopeGraph`、`CommandCatalog`、`ValueStore`、あるいは feature 移行が、これらの境界が存在しないまま始まると、結果として target kernel ではなく legacy アーキテクチャの高速版を再生してしまう。

---

## スコープ

この仕様が定義するもの:

- M0 から M15 までの実装フェーズ順序
- マイルストーンの目標、必要な出力、出口ゲートモデル
- アーキテクチャ仕様と実装シーケンスのクロス仕様依存マッピング
- diagnostics、tests、IR、validation、generation、boot に対する必須の事前 proof chain
- 既存機能の代表的移行ウェーブ順序
- 禁止される開始点と禁止ショートカット
- 最終統合と regression hardening の要件

この仕様は以下を意図的に定義しない:

- runtime API の形
- 正確なシリアライズ形式
- すでに別仕様が所有している検証または生成アルゴリズム
- 詳細なチーム作業分解やスケジュール見積もり
- アーキテクチャ保護の外側にあるプロジェクト管理ワークフロー

16 は下位 subsystem 仕様の代替であってはならない。
各下位仕様を、いつコードへ変えてよいかを決めるために存在する。

---

## 非目標

この仕様は以下を定義しない:

- すべてのマイルストーンを 1 つの pull request で終えるという約束
- 各マイルストーンの人員またはチーム責任
- M15 が完了するまで全機能の出荷を止める要件
- Kernel v2 アーキテクチャと無関係なゲームプレイ設計
- 一時的な branch 戦略やリリース日程
- 内部作業だからという理由で検証や diagnostics を回避する許可

16 はアーキテクチャ実行順序の文書である。
汎用的なプロジェクト管理ハンドブックではない。

---

## 他仕様との関係

| 仕様 | 16 との関係 |
|---|---|
| [00_KernelArchitectureOverviewSpec.md](00_KernelArchitectureOverviewSpec.md) | すべてのマイルストーンが守るべき譲れない根本制約を定義する。 |
| [01_KernelIRSpec.md](01_KernelIRSpec.md) | M2 で実装され、M3 から M11 で利用される正規化権威モデルを定義する。 |
| [02_ModuleContributionSpec.md](02_ModuleContributionSpec.md) | M2 で実装され、M3 から M11 で利用される contribution 境界を定義する。 |
| [03_VerifiedPlanGenerationSpec.md](03_VerifiedPlanGenerationSpec.md) | M4 で実装され、M5 から M15 で利用される生成信頼境界を定義する。 |
| [04_DependencyValidationSpec.md](04_DependencyValidationSpec.md) | M3 で実装され、M4 から M15 で再利用される validation firewall を定義する。 |
| [05_BootManifestAndProfileSpec.md](05_BootManifestAndProfileSpec.md) | M5 で実装され、M6 から M15 の前提となる検証済み boot entry point を定義する。 |
| [06_ServiceGraphRuntimeSpec.md](06_ServiceGraphRuntimeSpec.md) | 検証済みパイプライン成立後に M6 で実装される runtime service subsystem を定義する。 |
| [07_ScopeGraphRuntimeSpec.md](07_ScopeGraphRuntimeSpec.md) | M6 の後に M7 で実装される runtime scope subsystem を定義する。 |
| [08_LifecyclePlanSpec.md](08_LifecyclePlanSpec.md) | scope runtime 成立後に M8 で実装される lifecycle dispatch を定義する。 |
| [09_CommandCatalogRuntimeSpec.md](09_CommandCatalogRuntimeSpec.md) | lifecycle と boot の基盤が整った後に M9 で実装される verified command runtime を定義する。 |
| [10_ValueSchemaAndStoreSpec.md](10_ValueSchemaAndStoreSpec.md) | runtime core 成立後に M10 で実装される value schema と store の作業を定義する。 |
| [10_1_ScalarRuntimeAndBindingSpec.md](10_1_ScalarRuntimeAndBindingSpec.md) | M10 内に含まれる scalar specialization を定義する。 |
| [10_2_DynamicValueEvaluationSpec.md](10_2_DynamicValueEvaluationSpec.md) | M10 内に含まれる dynamic と reactive specialization を定義する。 |
| [11_DebugMapAndDiagnosticsSpec.md](11_DebugMapAndDiagnosticsSpec.md) | 仕様番号は高いが、M1 に早期実装しなければならない diagnostics 契約を提供する。 |
| [12_UnityAuthoringBridgeSpec.md](12_UnityAuthoringBridgeSpec.md) | 検証済みパイプラインと runtime core が整った後に M11 で実装される Unity authoring bridge を定義する。 |
| [13_LegacyCompatBoundarySpec.md](13_LegacyCompatBoundarySpec.md) | M12 で実装される quarantine 専用の legacy compatibility を定義する。 |
| [14_PerformanceBudgetAndRuntimeRulesSpec.md](14_PerformanceBudgetAndRuntimeRulesSpec.md) | M13 で正式化される performance と禁止 runtime ルールを定義するが、静的 gate の初期導入は M1 で始まる。 |
| [15_TestAndValidationSpec.md](15_TestAndValidationSpec.md) | M1 で部分実装し、M15 で完了する実行可能な保護モデルを提供する。 |

16 は down-stream の runtime 仕様ではない。
下位仕様を危険な順序で実装しないようにするための実行順序契約である。

---

## アセンブリ定義とコンパイル境界に関する期待

実装順序は asmdef 作業を architecture work として扱うべきであり、後片付けではない。
詳細な依存マトリクスは [17_AssemblyDefinitionAndCompileBoundarySpec.md](17_AssemblyDefinitionAndCompileBoundarySpec.md) が所有する。

16 に必要なシーケンスルール:

- runtime に面する各マイルストーンは、本格実装が広がる前に対象アセンブリまたはアセンブリ群を特定しなければならない
- コードが存在しても、禁止された依存方向を隠す無効な monolithic compile unit に残っているなら、そのマイルストーン完了は未完である
- compile-boundary テストと asmdef グラフ検証は、後のマイルストーンが feature、editor、legacy の逆参照を再導入する前に十分早い段階で現れなければならない
- subsystem 移行は、feature leaf から始めるのではなく、低変動から順に分割する

マイルストーンが、credible な asmdef の居場所と compile-boundary の説明なしに完了を主張するなら、16 の実行順序は破られている。

---

## 実装順序の原則

### 1. 豊かさの前に proof

project はまず、失敗を表現し、無効な構造を拒否し、証拠を記録できるようにならなければならない。

そのため M1 は M2 より先に存在する。
`KernelDiagnostic`、test artifact 出力、静的禁止パターン gate は、任意の支援作業ではない。
後続マイルストーンが runtime に fallback 行動を紛れ込ませるのを防ぐ仕組みである。

### 2. 投影の前に正規化

`KernelIR` と `ModuleContributionData` は、検証と生成の前に存在しなければならない。

どの runtime plan も、正規化 IR に追跡できない構造を勝手に発明してはならない。
そのため M2 は M3 と M4 に先行する。

### 3. 生成の前に検証

03 は、生成をファイル出力ステップではなく信頼境界として定義する。

したがって、dependency validation、重複検出、循環検査、projection-validation フックは、生成された plan を runtime input とみなす前に存在しなければならない。

### 4. boot の前に検証

boot は修復経路ではない。

M5 は、完全な verified artifact set、整合する hash、整合する profile policy、受理可能な legacy boundary 状態だけを受け入れるべきだ。
後続の runtime マイルストーンは、欠落データを discovery や fallback で修復できると仮定してはならない。

### 5. 移行の前に runtime

既存機能の代表的移行は、target pipeline が存在してから始める。

M6 から M13 がなければ、移行作業は新しい名前の下で legacy 行動を再生しがちである。
そのため M14 と M15 は後半のマイルストーンでなければならない。

---

## 全体のマイルストーン順序

| マイルストーン | 名称 | 主目標 | 終了シグナル |
|---|---|---|---|
| M0 | Architecture Freeze / Spec Hygiene | 実装開始前に用語、責務、禁止パターンの可視性を固定する | 00 から 15、さらに 10-1 と 10-2 が 1 つの概念表と依存マトリクスにマッピングされている |
| M1 | Diagnostics / Test Foundation | 早期に違反を捉える proof layer を作る | 構造化 diagnostics、test artifact、静的 gate が稼働している |
| M2 | KernelIR / ModuleContribution Foundation | 正規化された権威モデルを作る | IR を正規化、ダンプ、ハッシュ化でき、source に追跡できる |
| M3 | DependencyValidation | runtime 前の firewall を作る | 重複、欠落 dependency、無効な absence behavior、循環が拒否される |
| M4 | VerifiedPlanGeneration | 検証済み artifact と projection の境界を作る | 完全で決定論的、検証済みの artifact set だけが verified になれる |
| M5 | BootManifest / Profile | 検証済み boot entry point を作る | boot は互換な verified artifact のみを受理し、closed に失敗する |
| M6 | ServiceGraph | 粗粒度の検証済み service runtime を作る | scan、reflection fallback、object registry drift なしで slot ベース resolve が動く |
| M7 | ScopeGraph | 明示的な runtime scope 構造を作る | generation-safe handle と明示的な親子テーブルが transform の真実に取って代わる |
| M8 | LifecyclePlan | plan 駆動 lifecycle dispatch を作る | interface や registration scan なしで lifecycle dispatch が動く |
| M9 | CommandCatalog | 検証済み command dispatch を作る | `CommandTypeId` テーブル dispatch が executor discovery や string fallback なしで動く |
| M10 | Value / Scalar / Dynamic Runtime | 検証済み value runtime とその specialization を作る | stable-key fallback や隠れた evaluation なしで value access が動く |
| M11 | UnityAuthoringBridge | Unity authoring を検証済みパイプラインへ接続する | authoring が contribution を抽出し、検証済み artifact を通じて boot する |
| M12 | LegacyCompat Boundary | legacy compatibility を quarantine する | legacy は明示的、片方向、非修復である |
| M13 | Performance / RuntimeRules | 測定可能な runtime アーキテクチャルールを強制する | profiler marker、禁止操作テスト、allocation gate が存在する |
| M14 | Existing Feature Migration | 代表的 feature を target pipeline に通す | 選択した feature が禁止 fallback なしで v2 を通過する |
| M15 | Integration / Direct Play / Regression Hardening | end-to-end のアーキテクチャが保護されていることを証明する | direct play、regression suite、CI gate、legacy 削除の証拠が稼働する |

必要な順序:

```text
M0 -> M1 -> M2 -> M3 -> M4 -> M5 -> M6 -> M7 -> M8 -> M9 -> M10 -> M11 -> M12 -> M13 -> M14 -> M15
```

この順序は target-kernel 実装における規範である。
並列作業は、より前のマイルストーンの出口ゲートを迂回しないこと、そして前提条件が完了する前に target path を作らないことが条件でのみ許可される。

---

## マイルストーンゲートモデル

各マイルストーンには 4 つの必須属性がある。

1. 必要な出力
2. 出口ゲート
3. 禁止ショートカット
4. 下流の解放条件

コードが存在するだけでは、マイルストーンは完了ではない。
次のマイルストーンが runtime fallback、文書化されていない責務移譲、手動修復なしでその出力を消費できるときのみ完了である。

より前のマイルストーンゲートが退行したら、そのゲートに依存する後続マイルストーンは、退行が閉じられるまで at-risk 状態に戻る。

---

## マイルストーン定義

### M0: Architecture Freeze / Spec Hygiene

M0 は、大規模な実装開始前に文書面を固定する。

必要な出力:

- M0.1 00 から 15、10-1 / 10-2 を横断する spec hygiene pass
- M0.2 `Assets/Docs/v2/Index/KernelV2ConceptMap.md` にあるクロス仕様概念マップ文書
- M0.3 `Assets/Docs/v2/Index/ForbiddenPatternRegistry.md` にある禁止パターン台帳文書
- M0.4 `Assets/Docs/v2/Index/CrossSpecDependencyMatrix.md` にあるクロス仕様依存マトリクス文書
- M0.5 `Assets/Docs/v2/Index/ExistingAnchorInventory.md` にある既存アンカー目録文書

M0 の概念マップは、重複した責務所有なしで少なくとも次の用語をカバーしなければならない:

- `KernelIR`
- `ModuleContribution`
- `VerifiedKernelPlan`
- `ArtifactSet`
- `DebugMap`
- `KernelDiagnostic`
- `ServiceGraph`
- `ScopeGraph`
- `LifecyclePlan`
- `CommandCatalog`
- `ValueSchema`
- `ValueStore`
- `RuntimeQuery`
- `UnityAuthoringBridge`
- `LegacyCompat`

禁止パターン台帳の初期登録には、少なくとも次を含める:

- 直接の `Debug.LogError`
- runtime discovery のための `GetComponentsInChildren`
- kernel lookup のための `FindObjectsByType`
- scope 推定のための `Transform.parent`
- 必須 asset の fallback としての `Resources.Load`
- runtime stable-key lookup
- runtime 生成の負の ID
- `IReadOnlyList<ICommandExecutor>` の discovery
- `IScopeAcquireHandler` scan
- `IScopeTickHandler` scan
- runtime object registry としての ServiceGraph
- global settings dump としての BootManifest
- legacy fallback repair

文書整備に関する注意:

- 03 にある既知の重複ヘッダー内容は、チームが文書セットを凍結済みと見なす前に、追跡して解消するか、明示的に記録しなければならない

出口ゲート:

- すべての core 概念に 1 つの所有仕様がある
- 台帳にあるすべての禁止パターンが、下位仕様または test gate にマッピングされている
- 実装順序が dependency matrix に照らしてレビューされている
- 実装作業が、コードベースを ad hoc に探索するのではなく、現在のアンカー目録を指名できる

禁止ショートカット:

- 概念責務がまだ曖昧なまま runtime subsystem 実装を始めること
- レビューコメントを規範仕様の代わりにすること
- すでに current spec があるのに legacy code からアーキテクチャを推測すること

下流の解放条件:

- M1 は固定された禁止パターン語彙に対して proof gate を定義できる
- M2 は安定した概念責務に対して IR 型を定義できる

### M1: Diagnostics / Test Foundation

M1 は、以降すべてのマイルストーンを守る proof layer を作る。

必要な出力:

- M1.1 `KernelDiagnostic`、`DiagnosticCode`、`DiagnosticSeverity`、`DiagnosticDomain`、`DiagnosticFailureBoundary`、`DiagnosticContext`、`RuntimeIdentityRef`、`SourceLocationRef`、`ArtifactIdentityRef`
- M1.2 `IKernelDiagnosticService`、`KernelDiagnosticService`、`IKernelDiagnosticSink`、`InMemoryDiagnosticSink`、`UnityLogDiagnosticSink`、`TestDiagnosticSink`
- M1.3 `Logs/TestRuns/<timestamp>/` 配下のタイムスタンプ付き test artifact 出力
- M1.4 禁止 API と Unity 直接ロギングに対する静的ルール gate
- M1.5 diagnostics code と下位仕様の失敗意味を結び付ける文書または test の追跡可能性
- M1.6 静的 Debug gate
- M1.7 静的 forbidden-API gate

最低限の test artifact セット:

- `TestRunSummary.md`
- `TestRunSummary.json`
- `DiagnosticsReport.json`
- `ValidationReport.json`
- `GenerationReport.json`
- `PerformanceReport.json`

最初の静的ルールは、少なくとも次を検出しなければならない:

- 許可された sink の外側にある `Debug.LogError`
- 許可された sink の外側にある `Debug.LogWarning`
- 許可された sink の外側にある `Debug.LogException`
- target runtime path にある `Resources.Load`
- target runtime path にある `FindObjectsByType`
- target runtime path にある `GetComponentsInChildren`
- target runtime path にある `Transform.parent` 由来の scope 推定

出口ゲート:

- すべての kernel subsystem が 1 つの構造化 diagnostic model で報告できる
- test は `TestDiagnosticSink` を通じて `DiagnosticCode` を検証できる
- kernel diagnostics に対して Unity Debug API を呼べるのは `UnityLogDiagnosticSink` のみである
- target runtime path に forbidden API を追加すると失敗する gate が発動する

禁止ショートカット:

- 共有レコード model を迂回する subsystem 固有の logging pipeline を作ること
- runtime core 作業が始まった後に forbidden-pattern gate を先延ばしにすること
- diagnostic identity を整形済み文字列で扱うこと

下流の解放条件:

- M2 から M15 は、構造化された証拠付きで closed fail できる
- architecture drift が今や観測可能なので、runtime-first 作業の正当化が失われる

### M2: KernelIR / ModuleContribution Foundation

M2 は正規化された権威層を作る。

必要な出力:

- M2.1 `ModuleId`、`ServiceId`、`ScopeAuthoringId`、`ScopePlanId`、`CommandTypeId`、`CommandExecutorId`、`CommandPayloadSchemaId`、`ValueKeyId`、`ValueSchemaId`、`LifecycleStepId`、`RuntimeQueryId`、`SourceLocationId` の型付き ID 基本型
- M2.2 `SourceLocationIR`、`UnitySourceLocation`、`LegacySourceLocation`、`GeneratedSourceLocation`
- M2.3 `ModuleDefinition`、`ModuleContributionData`、`ContributionItem`、`ContributionKind`、`ContributionSource`、`ContributionAvailability`、`ContributionConflictPolicy`
- M2.4 `KernelIR`、`KernelIRHeader`、`ModuleIR`、`ServiceIR`、`ScopeIR`、`LifecycleIR`、`CommandIR`、`ValueKeyIR`、`RuntimeQueryIR`、`DependencyEdgeIR`、`SourceLocationTable`
- M2.5 IR hash とダンプまたはレポート出力

source location の最低限の provenance には次を含める:

- asset GUID
- asset path
- local file ID
- scene path
- GameObject path
- component type
- property path
- legacy origin
- generated origin

出口ゲート:

- runtime builder state に触れずに正規化 IR を生成できる
- IR node を source location へ追跡できる
- semantic hash の生成が等価入力に対して決定論的である
- contribution 収集が live service を解決したり、transform 階層から所有権を推測したりしない

禁止ショートカット:

- contribution 収集中に runtime builder または live service resolve に触ること
- public identity 境界で raw `int` のドメイン混在を許すこと
- 生成後まで source provenance を遅らせること

下流の解放条件:

- M3 は、実際の正規化 ID と依存エッジを検証できる
- M4 は、決定論的な入力モデルから verified artifact を生成できる

### M3: DependencyValidation

M3 は runtime 前の firewall を作る。

必要な出力:

- M3.1 `DependencyValidationReport`、`DependencyValidationIssue`、`ValidationResultStatus`、`ValidationSeverity`、`ValidationPhase`
- M3.2 重複 ID と誤ったドメインの検証
- M3.3 必須 dependency の欠落と無効な dependency kind の検証
- M3.4 optional の absence behavior 検証
- M3.5 Build、Generate、Boot、Acquire、Runtime、Save、EditorOnly をまたぐ phase-aware 循環検出
- M3.6 legacy 漏れの検証
- M3.7 `IProjectionValidationRule`、`ProjectionValidationReport`、`UnknownProjectedIdRule`、`DroppedMappingRule`、`DebugMapCoverageRule` を含む projection-validation インターフェース

出口ゲート:

- 重複、欠落、無効な phase、無効な owner、循環の問題が runtime より前に拒否される
- validation issue を `KernelDiagnostic` に変換できる
- 明示された absence behavior を持たない optional dependency は validation を通過できない
- projection がまだ最小であっても、生成後の validation hook が定義されている

禁止ショートカット:

- 重複または欠落 dependency 検出を runtime boot に押し込むこと
- 明示的な policy なしに runtime cycle を許すこと
- 移行が未完だからといって legacy 漏れを生き残らせること

下流の解放条件:

- M4 は、生成済み artifact と検証済み artifact を区別できる
- M5 は、boot 時に構造を再発見するのではなく validation report を信頼できる

### M4: VerifiedPlanGeneration / ArtifactSet

M4 は verified generation の信頼境界を作る。

必要な出力:

- M4.1 `ArtifactSetId`、`PlanId`、`ArtifactId`、`ArtifactKind`、`FormatVersion`、`KernelIRHash`、`RegistryHash`、`ProfileHash`、`DebugMapHash`、`GeneratedContentHash`、`GeneratorVersion` を含む artifact header
- M4.2 `GeneratedKernelPlan` と `VerifiedKernelPlan` の型レベル分離
- M4.3 artifact set の staging と promotion の transaction model
- M4.4 dictionary 順、reflection 順、file system 順、timestamp、absolute path に依存しない決定論的生成ルール
- M4.5 ServiceGraph、ScopeGraph、LifecyclePlan、CommandCatalog、ValueSchema、RuntimeQuery、KernelDebugMap、GenerationReport、ValidationReport の最小 projection
- M4.6 stale artifact 検出
- M4.7 DebugMap 生成 seed

出口ゲート:

- 部分的な artifact set は current verified artifact になれない
- stale、mismatch、hash 非互換の artifact は拒否される
- DebugMap coverage は promotion の一部であり、任意の追加ではない
- 同じ意味論的入力は同じ semantic hash と互換な artifact set を生む

禁止ショートカット:

- 生成出力を verification なしで直接 runtime に渡すこと
- source または profile が変わった後も古い artifact を有効とみなすこと
- 生成が無効 IR を省略によって修復することを許すこと

下流の解放条件:

- M5 は verified artifact reference のみから boot できる
- runtime マイルストーンは discovery を再導入せずに projection を消費できる

### M5: BootManifest / Profile

M5 は verified boot entry point を作る。

必要な出力:

- M5.1 `KernelProfile`、`KernelProfileKind`、`KernelProfilePolicy`、`BootDiagnosticsPolicy`
- M5.2 `ManifestId`、`ProfileId`、`VerifiedArtifactSetRef`、`BootPolicyId`、`DiagnosticsPolicy` を含む `KernelBootManifest`
- M5.3 artifact 完全性、hash 互換性、validation 成功、required root scope の存在、required root service の存在、profile による legacy-bridge 許可に対する boot validation gate
- M5.4 partially initialized な target runtime を決して valid と公開しない boot failure boundary
- M5.5 empty IR、diagnostics、DebugMap、`KernelRuntime`、`ServiceGraph`、root `ScopeGraph` のための最小 boot shell
- M5.6 boot diagnostics

BootManifest は次のようなものになってはならない:

- 完全な service list dump
- 完全な command list dump
- 完全な value-key dump
- 完全な lifecycle-step dump
- 完全な scope-graph dump
- 直接的な executor definition dump
- fallback-rule container
- scene-search-rule container

出口ゲート:

- boot は、完全で互換な verified artifact set がある場合にのみ成功する
- boot failure は、partially valid な runtime を呼び出し側に残さない
- profile policy は、runtime 作業開始前に legacy または diagnostics の不一致を拒否できる

禁止ショートカット:

- 欠落した service、scope、artifact の修復経路として boot を使うこと
- runtime discovery ルールを BootManifest に埋め込むこと
- Development だからという理由で legacy fallback を許すこと

下流の解放条件:

- M6 から M10 は、verified boot shell が存在すると仮定できる
- M11 の direct-play flow は固定された target boot entry point を持つ

### M6: ServiceGraph

M6 は、粗粒度 service のみを対象とする verified service runtime を作る。

必要な出力:

- M6.1 service eligibility 分類ルールと service boundary 目録
- M6.2 `ServiceGraphPlan`、`ServiceEntryPlan`、`ServiceSlotPlan`、`ServiceFactoryRef`、`ServiceContractRef`、`ServiceLifetimeKind`、`ServiceCardinalityKind`
- M6.3 `ServiceId` から slot index への slot-based resolver
- M6.4 許可された factory 形式: GeneratedStatic、GeneratedDelegate、ExplicitManual、LegacyBridge は legacy boundary 内のみ
- M6.5 必須失敗と明示的 optional absence behavior を分ける optional service policy
- M6.6 scope-local service boundary の定義
- M6.7 modal、tooltip、mesh、animation sprite hub など既存システムの hub 分類表

Service は次のようなものを表してよい:

- kernel レベルの粗粒度 service
- project レベルの粗粒度 service
- scene レベルの粗粒度 service
- authoring された scope の粗粒度 service

Service は次のようなものを表してはならない:

- per-entity runtime object
- per-part runtime object
- per-renderer runtime object
- per-tooltip runtime object
- per-channel-player runtime object
- per-mesh-track runtime object
- per-animation-player runtime object

出口ゲート:

- `ServiceId` resolve は scan ではなく事前計算された slot を通じて動く
- target path で constructor reflection も fallback resolver も必要ない
- optional service の欠落は silent fallback ではなく明示的である
- ServiceGraph は汎用 runtime object registry に劣化しない

禁止ショートカット:

- primary lookup identity として raw type を使うこと
- service composition として `IReadOnlyList<T>` discovery を使うこと
- target runtime path で registration scan や constructor reflection を使うこと
- 正しい scope 所有を避けるために任意の gameplay runtime object を ServiceGraph に格納すること

下流の解放条件:

- M7 は、実際の resolver に対して scope-local service boundary を定義できる
- M14 は、移行前に既存 hub を分類できる

### M7: ScopeGraph

M7 は明示的な runtime scope 構造を作る。

必要な出力:

- M7.1 `ScopeAuthoringId`、`ScopePlanId`、`ScopeHandle`、`UnityObjectLink` の間の identity separation
- M7.2 generation-safe な `ScopeHandle { index, generation }`
- M7.3 `ScopeInstanceTable`、`ScopeSlot`、明示的な parent-child table
- M7.4 scope runtime state machine
- M7.5 ServiceGraph、Lifecycle、ValueStore、RuntimeQuery 通知、Unity link に対する scope-local boundary
- M7.6 `UnityObjectLink`
- M7.7 pooling invalidation ルール

代表的な scope 状態:

- Created
- Built
- Acquiring
- Active
- Releasing
- Inactive
- Destroying
- Destroyed
- Failed

出口ゲート:

- slot 再利用後に stale handle が拒否される
- 親子関係は transform の真実ではなく明示的な table data である
- nearest-scope search や `Transform.parent` 推定は target runtime 行動に必要ない
- scope-local subsystem boundary は MonoBehaviour の所有に隠れておらず明示的である

禁止ショートカット:

- transform 階層を scope authority として使うこと
- GameObject traversal で親子や owner scope を発見すること
- runtime handle の有効性を Unity object lifetime の仮定と混ぜること

下流の解放条件:

- M8 は scope state transition によって lifecycle を dispatch できる
- M10 と M11 は明示的な scope identity に value と authoring の link を付けられる

### M8: LifecyclePlan

M8 は plan 駆動の lifecycle dispatch を作る。

必要な出力:

- M8.1 `LifecyclePlanId`、`LifecycleStepId`、`LifecyclePhase`、`LifecycleTargetRef`、`LifecycleActionKind`、`LifecycleFailurePolicy`
- M8.2 Acquire、Release、Tick、FixedTick、LateTick、Reset、Destroy の dispatch table
- M8.3 state transition を通じた ScopeGraph 連携
- M8.4 部分的な acquire 完了に対する失敗と rollback の方針
- M8.5 tick budget 方針
- M8.6 async lifecycle 方針

出口ゲート:

- lifecycle dispatch は interface や registration scan ではなく verified plan によって駆動される
- acquire 失敗は、方針に従って完了済み作業を rollback できる
- 個々の entity の tick は、下位仕様で明示的に正当化されない限り既定で拒否される
- lifecycle failure は ad hoc なログではなく `KernelDiagnostic` を出力する

禁止ショートカット:

- `GetAcquireHandlers()` 風の collection path
- `IScopeTickHandler` scan
- lifecycle 参加者を探すための service-registration scan
- rollback 実装が大変だからといって acquire 成功を前提にすること

下流の解放条件:

- M9 と M10 は、明示的な scope と phase transition に統合できる
- M14 の移行は、既存の acquire / release 負債を明示的な step に写像できる

### M9: CommandCatalog

M9 は検証済み command dispatch を作る。

必要な出力:

- M9.1 `CommandTypeId`、`CommandCategoryId`、`CommandExecutorId`、`CommandPayloadSchemaId`、`CommandAuthoringKeyId` を含む command identity
- M9.2 構造化された `CommandCatalogPlan` エントリと、グループ化された metadata table: `CommandEntryPlan`、`CommandExecutorRef`、`CommandPayloadSchemaPlan`、`CommandModuleMetadata`、`CommandCategoryMetadata`
- M9.3 `CommandTypeId -> ExecutorRef -> executor factory` による executor lookup
- M9.4 必須 field、type mismatch、unknown field、target reference、`ValueKeyId`、runtime-query reference に対する payload schema validation
- M9.5 `CommandRunner`、`CommandFrame`、`CommandContext`、`CommandLocal`、cancellation、failure boundary
- M9.6 Sequence、If、Switch、For、Wait、Delay、Detached/Forget、Cancel を含む control-flow と async command
- M9.7 timeout、cancellation、loop-bound 方針

出口ゲート:

- command dispatch は検証済み command identity によって table 駆動される
- executor discovery は bulk DI registration や runtime string lookup に依存しない
- payload validation は executor 本体の実行前に行われる
- control-flow command は失敗と timeout の挙動を明示する

禁止ショートカット:

- `IReadOnlyList<ICommandExecutor>` の discovery
- target model としての `.As<ICommandExecutor>()` またはそれに相当する installer 駆動 executor registration
- target runtime path における string executor lookup や authoring-key dispatch
- command composition 機構としての巨大な runtime installer

下流の解放条件:

- M10 と M14 は、検証済み command identity と schema に依存できる
- M15 は、minimal vertical slice に command dispatch を含められる

### M10: Value / Scalar / Dynamic Runtime

M10 は検証済み value runtime とその specialization を作る。

必要な出力:

- M10.1 `ValueKeyId`、`ValueSchemaId`、`ValueStoreId`、`ValueKind`、`ValueStorageKind`、`ValueDefaultPolicy`、`SavePolicy`
- M10.2 `ValueKeyId` から typed backend、slot revision、store revision、dirty signal への slot-based storage
- M10.3 `ValueStoreInitPlan`、`ValueInitPlan`、`ValueInitEntry`、`OverwritePolicy`、`InitPhase`、source provenance
- M10.4 table、record、record-list、row、column、cell の identity / revision model
- M10.5 Base、PrefixMul、Add、SuffixMul、FinalClamp、Effective value、contribution handle、revision tracking を持つ `LayeredNumeric` pipeline
- M10.6 10-1 からの scalar runtime specialization
- M10.7 10-2 からの dynamic および reactive evaluation plan
- M10.8 evaluation context、tracker、cache、dependency stamp、invalidation policy、nested dependency capture
- M10.9 revision と dirty bridge

出口ゲート:

- value の read/write は stable-key や runtime-generated negative ID ではなく検証済み ID と slot を使う
- init plan は collection 順序に頼らず overwrite と phase の振る舞いを明示する
- scalar と dynamic の evaluation は、一般的な value access の中の隠れた挙動ではなく明示的な plan である
- hot-path の value access は `Dictionary<string, object>` や write からの schema 推論に依存しない

禁止ショートカット:

- 通常の value access として stable-key runtime lookup を使うこと
- target behavior として runtime negative ID を作ること
- generic store access 中に hidden dynamic evaluation を行うこと
- 状態不足を修復するために construct / start initialization を繰り返すこと

下流の解放条件:

- M11 は authoring value を検証済み init plan に抽出できる
- M14 は Blackboard、Var、DynamicValue の振る舞いを bounded な runtime 形式へ移行できる

### M11: UnityAuthoringBridge

M11 は Unity authoring を検証済みパイプラインへ接続する。

必要な出力:

- M11.1 `UnityAuthoringSourceKind`、`UnitySourceLocation`、`UnityObjectLink`、`AuthoringComponentKind` を含む authoring source model
- M11.2 重複検出、copy/paste 方針、prefab duplication 方針、variant override の source tracing を含む安定した `ScopeAuthoringId` 方針
- M11.3 明示的な authoring root から `ModuleContributionData` への contribution extraction pipeline
- M11.4 ローカル authoring 検証と diagnostics
- M11.5 extract、normalize、validate、一時的な verified artifact set の generate、BootManifest 経由の boot を通る direct-play generation path
- M11.6 authoring diagnostics

出口ゲート:

- MonoBehaviour と ScriptableObject の authoring は runtime 構造を記述するが、runtime 構造を直接構築しない
- direct play は runtime discovery repair ではなく verified pipeline を使う
- authoring extraction は source-traceable な contribution と local diagnostics を生成する

禁止ショートカット:

- target authoring-to-runtime bridge としての `IFeatureInstaller.InstallFeature`
- authoring extraction 中の builder mutation
- authoring の真実としての `GetComponentsInChildren` による runtime discovery や `Transform.parent` による所有権推定
- Play Mode だからという理由で validation を迂回すること

下流の解放条件:

- M14 は実際の authoring input を通じて代表的 feature を移行できる
- M15 は direct-play の検証済み boot を証明できる

### M12: LegacyCompat Boundary

M12 は legacy compatibility を拡張するのではなく quarantine する。

必要な出力:

- M12.1 `LegacyCompatKind`、`LegacyAdapterDescriptor`、`LegacyRemovalPolicy`、`LegacyMigrationReport`
- M12.2 Legacy -> Adapter -> v2 のみを許す依存方向強制
- M12.3 installer、resolver、command、value、lifecycle、authoring の移行に対する明示的 legacy adapter
- M12.4 runtime legacy adapter に対する release profile 拒否方針
- M12.5 resolver fallback 拒否
- M12.6 value migration adapter
- M12.7 removal-policy tracking

出口ゲート:

- target-kernel core は fallback path として legacy 型に依存しない
- legacy 利用は明示的で、診断可能で、削除可能である
- Release profile は禁止された legacy runtime path を拒否できる

禁止ショートカット:

- v2 -> Legacy -> fallback の方向
- legacy API を通じた新しい target feature の追加
- 欠落した target-kernel data を修復するために legacy resolver 振る舞いを使うこと

下流の解放条件:

- M14 と M15 は、migration residue を隠さずに測定できる
- performance と regression gate は legacy 利用を bounded debt として扱える

### M13: Performance / RuntimeRules

M13 は測定可能な runtime アーキテクチャルールを正式化する。

必要な出力:

- M13.1 HotPath、WarmPath、ColdPath、BootPath、EditorGenerationPath、ValidationPath、TestOnlyPath、LegacyMigrationPath の `RuntimePathKind` 分類
- M13.2 Kernel.Boot、Kernel.ServiceGraph、Kernel.ScopeGraph、Kernel.Lifecycle、Kernel.CommandCatalog、Kernel.ValueStore、Kernel.DynamicEvaluation、Kernel.Diagnostics、Kernel.UnityBridge、Kernel.LegacyCompat の profiler-marker taxonomy
- M13.3 階層 scan、discovery、reflection construction、direct logging、string dispatch、stable-key access、lifecycle scan に対する forbidden-API テスト
- M13.4 resolve、handle validation、tick dispatch、command dispatch、value read/write、dynamic cached read、diagnostics-disabled trace path に対する hot-path allocation テスト
- M13.5 performance report 出力
- M13.6 regression 閾値

出口ゲート:

- performance ルールは文書だけでなく実行可能である
- target hot path には profiler marker と allocation 期待値がある
- forbidden operation はアーキテクチャ回帰として測定される

禁止ショートカット:

- 測定なしで path が速いと主張すること
- feature migration の後まで forbidden-operation テストを延期すること
- 現在のコンテンツ規模が小さいからといって hidden allocation や string lookup を許すこと

下流の解放条件:

- M14 の移行を explicit budgets に照らして受理または拒否できる
- M15 の CI gate に実際の performance smoke check を含められる

### M14: Existing Feature Migration

M14 は代表的な既存 feature を検証済みパイプラインに通して移行する。

必要な出力:

- M14.1 ModalStack migration
- M14.2 Tooltip migration
- M14.3 MeshChannel migration
- M14.4 AnimationSprite migration
- M14.5 Blackboard / Var migration
- M14.6 CommandRunnerMB migration
- M14.7 Loading / Boot legacy migration

代表的な移行ルール:

- per-modal、per-layer、per-tooltip、per-channel-player、per-animation-player の runtime object を target service に昇格させてはならない
- camera fallback、null var-store fallback、scope-ancestor fallback、stable-key fallback は削除するか、明示的な migration adapter の背後に隔離しなければならない
- hub 所有の runtime object は、service の濫用ではなく hub 所有の runtime object のままである

出口ゲート:

- 選択した既存 system が v2 の contribution、validation、verified generation、boot、runtime、diagnostics、performance ルールを通過する
- 代表的 legacy fallback パターンが削除または隔離される
- 移行された feature には debug map の追跡可能性がある

禁止ショートカット:

- trust boundary を変えずに legacy installer パターンの名前だけを変えること
- target runtime path が存在する前に feature を移行すること
- アーキテクチャ回帰が見えないまま gameplay の成功だけで満足すること

下流の解放条件:

- M15 は代表的コンテンツを使った実際の minimal vertical slice を実行できる
- legacy 削除の証拠が理論ではなく具体になる

### M15: Integration / Direct Play / Regression Hardening

M15 は end-to-end のアーキテクチャを証明する。

必要な出力:

- M15.1 Unity authoring source から contribution、IR、validation、generation、boot、service resolve、scope creation、lifecycle acquire、command dispatch、value access、diagnostics output までの minimal vertical slice
- M15.2 dirty check、extract、validate、generate、boot を使う direct-play verified flow
- M15.3 direct logging、discovery、transform 推定、`Resources.Load` fallback、service または executor discovery、lifecycle scan、command string dispatch、value stable-key runtime lookup、legacy fallback、stale artifact boot、Development での DebugMap 欠落に対する regression suite
- M15.4 build、EditMode validation、EditMode generation、PlayMode minimal boot、静的禁止パターンテスト、diagnostics snapshot テスト、performance smoke テスト、legacy boundary テストを含む CI gate
- M15.5 legacy-removal pass
- M15.6 文書化と test 追跡可能性の完了

出口ゲート:

- direct play と CI は side path ではなく verified pipeline を使う
- regression suite は、architecture drift が runtime に戻ってきたとき失敗する
- test と文書の追跡可能性は、マイルストーン状態を監査できるほど十分である

禁止ショートカット:

- Play Mode fallback boot
- 統合時の scene-discovery repair
- validation、generation、diagnostics、performance、regression の証拠なしに gameplay が緑なら十分とみなすこと

下流の解放条件:

- target-kernel アーキテクチャは、望ましいだけでなく保護されている

---

## 禁止シーケンス

以下の開始点は、Kernel v2 の primary implementation entry point として明確に禁止される。

```text
NG:
M6 ServiceGraph first
M9 CommandCatalog first
M10 ValueStore first
M14 Existing Feature Migration first
```

理由:

- `ServiceGraph` first は、古い DI コンテナと runtime object registry パターンを再生しがちである。
- `CommandCatalog` first は、bulk executor registration と runtime string dispatch の負債を再生しがちである。
- `ValueStore` first は、stable-key fallback と隠れた dynamic behavior を持つ Blackboard v2 を再生しがちである。
- feature migration first は、trust boundary を変えずに legacy 行動の名前だけを変えがちである。

高リスクな正しい順序ルールは次の通り:

```text
Diagnostics / Test -> IR / Contribution -> Validation -> Generation -> Boot -> Runtime -> Migration -> Integration
```

より後の subsystem を先に試作する必要がある場合でも、その試作は明示的に non-authoritative のままにしなければならず、前提マイルストーンが完了するまでは target runtime path になってはならない。

---

## 完了ルール

すべてのマイルストーンに次の基準が適用される:

- 必要な出力が source または documentation 形式で存在する
- 少なくとも 1 つの下位仕様ルールまたは test gate が、そのマイルストーン出力を検証できる
- マイルストーンの禁止ショートカットが target runtime path に存在しない
- 下流マイルストーンが runtime fallback を導入せずにその出力を消費できる

これらの基準を満たさない限り、コードがどれだけ存在してもマイルストーンは進行中である。

---

## 最終的位置づけ

この計画で最も重要な実装判断は、M1 を最初に置くことである。

project は clever な resolver から始まらない。
structured な failure surface、静的 forbidden-pattern gate、test artifact から始まる。

その後、target kernel は次の順で構築される:

```text
KernelDiagnostic / Test Gates
-> KernelIR / ModuleContribution
-> DependencyValidation
-> VerifiedPlanGeneration
-> BootManifest / Profile
-> Runtime Subsystems
-> UnityAuthoringBridge
-> Legacy Quarantine
-> Performance Gates
-> Existing Feature Migration
-> Integration and Regression Hardening
```

この順序により、kernel は大きくなる前に検証可能になる。
それが、v2 移行がより良い名前を持つ legacy 行動へ崩れるのを防ぐ唯一の信頼できる方法である。

## テストケース

| テストケース | 目的 | 検証 |
|---|---|---|
| TC-16-01 | 実装順序が仕様番号と同じではないことを確認する。 | Purpose と Global Milestone Order の節が、diagnostics、tests、IR、validation、generation、boot が runtime subsystem マイルストーンより先であることを明示していなければならない。 |
| TC-16-02 | M0 に concept ownership、禁止パターン台帳、依存マトリクス、アンカー目録の作業が含まれていることを確認する。 | M0 の節が M0.2 から M0.5 を列挙し、最小限の concept および forbidden-pattern カバレッジを説明していなければならない。 |
| TC-16-03 | diagnostics と test がアーキテクチャ保護であり、後付けの磨きではないため、M1 が最初の実装マイルストーンであることを確認する。 | Implementation Ordering Principles と M1 の節が、構造化 diagnostics、sink 方針、test artifact、静的 gate を前提作業として定義していなければならない。 |
| TC-16-04 | runtime-first の入口点が明示的に拒否されていることを確認する。 | Forbidden Sequencing の節が、M6、M9、M10、M14 を禁止された主要開始点として列挙し、その理由を説明していなければならない。 |
| TC-16-05 | 最終統合に direct-play verified boot、regression gate、CI gate、legacy 削除の証拠が必要であることを確認する。 | M15 の節が M15.1 から M15.6 を定義し、ゲームプレイだけの成功ではなく end-to-end の証明を要求していなければならない。 |

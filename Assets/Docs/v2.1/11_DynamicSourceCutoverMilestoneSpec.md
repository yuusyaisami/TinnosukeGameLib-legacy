# Kernel v2.1 Dynamic Source Cutover Milestone Specification

## 文書ステータス

- 文書 ID: `11_DynamicSourceCutoverMilestoneSpec`
- 状態: Draft
- 役割: v2.1 における dynamic source 専用の cutover milestone を定義し、存在するすべての dynamic source surface を一覧化して置換状態を追跡する
- 範囲: Assets/GameLib と Assets/Game にある dynamic runtime substrate、`IDynamicSource` 実装、expression / rich-text / counter family、Flow authoring entry point、editor drawer / preview / sampling support surface
- 非目標: service surface、command surface、scene / prefab migration、spawn lifecycle、general full replacement definition

### 改訂メモ

dynamic source は runtime semantics と editor authoring surface が密接に結びついているため、代表例だけでは移行完了を宣言できない。
この文書は、workspace scan で見つかった dynamic source related surface を 1 件も抜かさずに inventory 化し、`置換済み` / `進行中` / `隔離/削除対象` / `要差し替え` を明示するための milestone である。

canonical inventory は [Index/DynamicSourceCutoverInventory.md](Index/DynamicSourceCutoverInventory.md) である。

---

## 1. ミッション

dynamic source cutover milestone の目的は次の 4 つである。

1. current workspace に存在する dynamic source surface を全件列挙する
2. 各 dynamic source surface が new architecture に置換済みかどうかを記録する
3. legacy dynamic authority surface を quarantine / deletion / replacement に振り分ける
4. shipped gameplay surface に残る dynamic source authority を 0 にする

この milestone では、1 件でも未把握の dynamic source surface が残っている限り完了とはみなさない。

---

## 2. Inventory Baseline

current baseline は次の通りである。

| 指標 | 値 | 意味 |
| --- | --- | --- |
| 定義レコード | 236 | class / interface / enum / struct surface の dynamic source 定義数 |
| unique dynamic surface | 236 | partial を束ねた dynamic source surface 数 |
| class surfaces | 187 | runtime surface / editor surface / authoring helper surface |
| interface surfaces | 17 | `IDynamicSource` family / runtime contract surface |
| enum surfaces | 20 | evaluation policy / source mode / token kind / value kind surface |
| struct surfaces | 12 | handle / key / context / diagnostic surface |
| `置換済み` | 146 | core runtime または verified authoring path に already anchored |
| `進行中` | 0 | explicit declaration companion を持つ surface は current scan で未発見 |
| `隔離/削除対象` | 12 | legacy authority / quarantine / deletion-only |
| `要差し替え` | 78 | current scan で new-path evidence がない |

この baseline は完了宣言の根拠ではなく、移行の出発点である。

current scan では explicit declaration companion surface が見つかっていないため、`進行中` は空である。

---

## 3. 状態定義

### 3.1 `置換済み`

- dynamic source surface が kernel-native runtime か verified authoring path にある
- もしくは、既に verified new architecture の truth source にある
- 代表例を増やすのではなく、実際に target runtime へ入っていることを証跡で示す

### 3.2 `進行中`

- dynamic source surface に explicit new-path declaration companion がある
- ただし legacy surface はまだ workspace に残っている
- 置換作業は完了しておらず、`置換済み` に昇格するまで milestone 完了には数えない

### 3.3 `隔離/削除対象`

- `DeferredDynamicVarValue` 系、`DynamicValueResolver` 系、`DynamicObjectRegistryService` 系、`DynamicObjectRegistryMB` 系、`BlackboardSourceUtility` 系、`GridBlackboardSourceUtility` 系、`VarIdResolver` 系、`VarKeyRegistryLocator` 系、またはそれに準ずる legacy authority surface
- runtime authority としての存続は認めない
- 残す場合でも quarantine または diagnostics-only に閉じる

### 3.4 `要差し替え`

- current scan で new-path evidence が見つからない dynamic source surface
- shipped gameplay surface に含まれる限り、いずれかの wave で replacement される必要がある
- 78 件の open surface があるため、この milestone はまだ未完了である

---

## 4. Dynamic Source Cutover Rule

dynamic source replacement の判定は surface 単位で行う。

許可される再利用:

- dynamic runtime semantics
- pure data holder
- verified helper that does not own runtime authority
- editor drawing / preview / sampling のうち、truth source を変えないもの

禁止される再利用:

- registration authority
- resolver authority
- editor discovery
- hierarchy fallback
- runtime discovery
- hidden adapter での延命
- `Resources.Load` fallback
- string-key repair を runtime truth にすること
- `DynamicObjectRegistryService` / `DynamicObjectRegistryMB` を authority に残すこと
- `Blackboard` truth を dynamic source の裏側に戻すこと

surface は type definition 単位で扱う。partial class は 1 surface として扱うが、すべての定義ファイルが同じ replacement state に達するまで `置換済み` にはしない。

---

## 5. Milestone Phases

この dynamic milestone は、M7 の value runtime wave と M11 / M12 の verification の前提になる。

### M7-D1: Inventory Freeze

- dynamic source surface を全件固定する
- new dynamic surface の追加があれば同じ変更セットで inventory を更新する
- 236 definition records の baseline を fixture 化する

出口条件:

- inventory companion が current workspace と一致している
- 未把握の dynamic source surface がない

### M7-D2: Kernel-Native Verification

- `置換済み` surface を検証する
- target-native dynamic runtime / expression / rich-text / counter surface が runtime authority を持つことを確認する

出口条件:

- `DynamicEvaluationRuntime`
- `DynamicValue`
- `DynamicVariant`
- `IDynamicSource`
- `IDynamicValueAsset`
- `RichTextSource`
- `IExpressionSource`
- `DynamicCounterController`

が new-path truth にある

### M7-D3: Declaration-Backed Cutover

- `要差し替え` surface を new declaration / authoring / plan に接続する
- editor drawer / preview / sampling surface を legacy authority から切り離す
- legacy surface を残す場合は quarantine に限定する

出口条件:

- `DynamicValueCompactDrawer`
- `TypedDynamicValueDrawer`
- `DynamicManagedRefSourceCatalog`
- `ExpressionGraphPreviewWindow`
- `ExpressionGraphSamplingService`

が new-path の authoring support として接続されている

### M7-D4: Legacy Dynamic Withdrawal

- `DeferredDynamicVarValue`
- `DynamicValueResolver`
- `DynamicObjectRegistryService`
- `DynamicObjectRegistryMB`
- `BlackboardSourceUtility`
- `GridBlackboardSourceUtility`
- `VarIdResolver`
- `VarKeyRegistryLocator`

を target path から外す

出口条件:

- legacy dynamic authority が resolution / evaluation / authoring truth を持たない
- quarantine surface があっても diagnostics-only に閉じる
- dynamic source の new-path truth が value runtime から独立して追跡できる

---

## 6. Target Anchors

この milestone で追跡する主要アンカーは次である。

- [IDynamicSource.cs](../../GameLib/Script/Common/Variables/Dynamic/Core/IDynamicSource.cs)
- [DynamicSources.cs](../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicSources.cs)
- [DynamicEvaluationRuntime.cs](../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicEvaluationRuntime.cs)
- [DynamicValue.cs](../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicValue.cs)
- [DynamicVariant.cs](../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicVariant.cs)
- [DynamicCounterController.cs](../../GameLib/Script/Common/Variables/Dynamic/Counter/DynamicCounterController.cs)
- [RichTextSource.cs](../../GameLib/Script/Common/Variables/Dynamic/RichText/RichTextSource.cs)
- [RichTextNodes.cs](../../GameLib/Script/Common/Variables/Dynamic/RichText/RichTextNodes.cs)
- [ExpressionAST.cs](../../GameLib/Script/Common/Variables/Dynamic/Expression/ExpressionAST.cs)
- [ExpressionTokenizer.cs](../../GameLib/Script/Common/Variables/Dynamic/Expression/ExpressionTokenizer.cs)
- [DynamicValueCompactDrawer.cs](../../GameLib/Script/Common/_Editor/Dynamic/DynamicValueCompactDrawer.cs)
- [TypedDynamicValueDrawer.cs](../../GameLib/Script/Common/_Editor/Dynamic/TypedDynamicValueDrawer.cs)
- [DynamicManagedRefSourceCatalog.cs](../../GameLib/Script/Common/_Editor/Dynamic/DynamicManagedRefSourceCatalog.cs)
- [ExpressionGraphPreviewWindow.cs](../../GameLib/Script/Common/_Editor/Dynamic/Expression/ExpressionGraphPreviewWindow.cs)
- [ExpressionGraphSamplingService.cs](../../GameLib/Script/Common/_Editor/Dynamic/Expression/ExpressionGraphSamplingService.cs)
- [FlowArg.cs](../../GameLib/Script/Project/Flow/Core/FlowArg.cs)
- [FlowArgDef.cs](../../GameLib/Script/Project/Flow/Authoring/FlowArgDef.cs)
- [FlowArgKind.cs](../../GameLib/Script/Project/Flow/Core/FlowArgKind.cs)
- [FlowCompiler.cs](../../GameLib/Script/Project/Flow/Compiler/FlowCompiler.cs)
- [CollisionDynamicSources.cs](../../GameLib/Script/Collision/Core/CollisionDynamicSources.cs)

---

## 7. テストケース

| テストケース | 目的 | 検証 |
| --- | --- | --- |
| `TC-V21-11-01` | dynamic source scope が inventory 化されていることを確認する | 本書が runtime substrate、expression / rich-text / counter、Flow authoring、editor support surface を含めていなければならない |
| `TC-V21-11-02` | inventory baseline が workspace scan と一致していることを確認する | 236 definition records、236 unique surface、146 replaced、0 in-progress、12 quarantine、78 todo が書かれていなければならない |
| `TC-V21-11-03` | `IDynamicSource` family が kernel-native surface として扱われることを確認する | `DynamicEvaluationRuntime`、`DynamicValue`、`DynamicVariant`、`IDynamicSource`、`RichTextSource`、`IExpressionSource`、`DynamicCounterController` が置換済みとして扱われていなければならない |
| `TC-V21-11-04` | legacy dynamic authority が quarantine / deletion-only であることを確認する | `DeferredDynamicVarValue`、`DynamicValueResolver`、`DynamicObjectRegistryService`、`DynamicObjectRegistryMB`、`BlackboardSourceUtility`、`VarIdResolver`、`VarKeyRegistryLocator` が隔離対象として書かれていなければならない |
| `TC-V21-11-05` | editor / Flow anchors が inventory 範囲に含まれることを確認する | `DynamicValueCompactDrawer`、`TypedDynamicValueDrawer`、`DynamicManagedRefSourceCatalog`、`ExpressionGraphPreviewWindow`、`ExpressionGraphSamplingService`、`FlowArg` / `FlowArgDef` / `FlowArgKind` / `FlowCompiler` が追跡対象として列挙されていなければならない |
| `TC-V21-11-06` | progress-state dynamic authoring が現在空であることを確認する | `進行中: 0` と explicit declaration companion が未発見であることが明示されていなければならない |
| `TC-V21-11-07` | implementation order への接続が定義されていることを確認する | `05_ImplementationMilestoneSpec` に `DynamicSource` が M7-5 として追加されていなければならない |

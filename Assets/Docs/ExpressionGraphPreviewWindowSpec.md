<!--
Spec Version: v0.1
Status: Draft (Concept)
Updated: 2026-03-24
Change Summary:
- 本仕様書は新規作成。
- FloatExpressionSource / IntExpressionSource の式を EditorWindow で 2D グラフ表示する構想を定義。
- x軸変数の特別指定、非x変数の固定値入力、string混在時の表示不可理由の提示要件を明記。
- 本書は仕様策定フェーズであり、実装コードは含まない。

Primary References Read:
- Assets/GameLib/Script/Common/Variables/Dynamic/Expression/FloatExpressionSource.cs
- Assets/GameLib/Script/Common/Variables/Dynamic/Expression/IntExpressionSource.cs
- Assets/GameLib/Script/Common/Variables/Dynamic/Expression/BoolExpressionSource.cs
- Assets/GameLib/Script/Common/Variables/Dynamic/Expression/ExpressionAST.cs
- Assets/GameLib/Script/Common/Variables/Dynamic/Expression/ExpressionParser.cs
- Assets/GameLib/Script/Common/Variables/Dynamic/Expression/ExpressionTokenizer.cs
- Assets/GameLib/Script/Common/Variables/Dynamic/Expression/ExpressionVariable.cs
- Assets/GameLib/Script/Common/Variables/Dynamic/Expression/ExpressionFunctionRegistry.cs
- Assets/GameLib/Script/Common/Variables/Dynamic/Expression/IExpressionSource.cs
- Assets/GameLib/Script/Common/_Editor/Dynamic/TypedDynamicValueDrawer.cs
- Assets/GameLib/Script/Common/Commands/VNext/Editor/CommandListEditorWindow.cs
-->

# Expression Graph Preview Window 仕様書 v0.1

## 1. 目的

`FloatExpressionSource` / `IntExpressionSource` で記述された式を、Editor 上で `y = f(x)` として可視化する。

今回の主目的は次の 4 点。

1. 式の挙動をプレイ前に視覚確認できること。
2. x軸に使う変数を必ず明示指定できること。
3. x軸以外の変数を固定値で入力できること。
4. string が式に含まれる場合に、表示不可理由を Window に明示すること。

## 2. 適用範囲

対象:

- `FloatExpressionSource`
- `IntExpressionSource`

対象外 (v0.1):

- `BoolExpressionSource` のグラフ化
- RichText 側式の直接可視化
- 3D サーフェス表示 (z軸)
- 実行時 (Runtime) デバッグウィンドウ

## 3. 要件

### 3.1 基本要件

1. Expression Source から専用 EditorWindow を開けること。
2. Window は 2D グラフを描画すること。
3. x軸変数を必須選択にすること。
4. 非x変数は固定値入力 UI を持つこと。
5. 式が描画不能な場合、Window 内に理由を表示すること。

### 3.2 数式要件

1. 基本は二次グラフ用途を想定する。
2. 実装上は二次式に限定せず、`Float/Int Expression` が評価可能なら描画対象にできる。
3. `IntExpressionSource` は評価値を `RoundToInt` 後に描画する。

### 3.3 例外要件

次のケースは描画不可とし、理由を表示する。

1. 式内に文字列リテラル (`"..."`) が存在する。
2. 式評価結果が数値に収束しない (型推定または実行時評価で非数値)。
3. x軸変数が未指定。
4. x軸変数が数値として扱えない。
5. 評価時に解決不能変数が残る。

## 4. 画面仕様 (EditorWindow)

## 4.1 Window 名称

`Expression Graph Preview`

## 4.2 主な表示領域

1. Header
- Source 種別 (`FloatExpression` / `IntExpression`)
- 対象式文字列 (readonly)

2. Variable Binding
- x軸変数選択ドロップダウン (必須)
- 非x変数の固定値入力リスト
- 型表示 (Number/Bool/String/Unknown)

3. Plot Settings
- X Min / X Max
- Sample Count
- Auto Fit Y
- Show Grid / Show Axes / Show Points

4. Graph Area
- 折れ線表示
- 軸線
- 原点表示
- マウス位置の x, y readout

5. Diagnostics
- `Can Plot` / `Cannot Plot`
- 描画不可理由一覧
- 式パースエラー or 評価エラー

## 4.3 非x変数入力ルール

1. 数値変数:
- 固定値 (float) 入力を必須化。

2. bool 変数:
- v0.1 は「固定値入力対象外」扱い。
- 含まれていても式が数値に評価できる場合は暫定許容可。
- ただし挙動が不明瞭なため、Diagnostics に警告を出す。

3. string 変数:
- 描画不可。
- Diagnostics に理由を表示する。

## 5. 技術仕様

## 5.1 解析フロー

1. Source から式文字列取得。
2. `ExpressionTokenizer` でトークン化。
3. `ExpressionParser` で AST 構築。
4. 識別子一覧抽出 (`IExpressionSource.GetDependentKeys` + parser used identifiers)
5. 変数型判定。
6. x軸変数確定。
7. 非x固定値を適用してサンプリング評価。
8. グラフ描画。

## 5.2 型判定

優先順:

1. `ExpressionVariable.ExpectedKind`
2. `ExpressionVariable.Value.Evaluate(...).Kind` の推定
3. parser 上のリテラル/演算子からの補助推定
4. Unknown

Unknown は v0.1 では数値入力許可するが、Diagnostics に警告を表示する。

## 5.3 string 混在判定

次のいずれかで string 混在とみなす。

1. Tokenizer が `ExprTokenKind.String` を検出。
2. 変数型が `ValueKind.String`。
3. 文字列返却関数の使用 (将来 `ExpressionFunctionRegistry` に戻り値型メタを追加して厳密判定)。

v0.1 では 1 と 2 を必須、3 は既知関数の暫定ブラックリスト方式で運用する。

## 5.4 評価コンテキスト

Editor 用の簡易 `IDynamicContext` を用意し、固定値を `IVarStore` に詰めて評価する。

注意:

- implicit key 解決 (`VarIdResolver`) が必要なため、key 未登録は明示エラーにする。
- Scope 依存 source がある場合は、評価不能として理由表示する。

## 6. UX 要件

1. エラーは Console だけでなく Window 内表示を必須。
2. `Cannot Plot` 状態でも、理由が分かるように一覧表示。
3. x軸未選択時はグラフを描かず明示誘導。
4. 値域が極端な場合、`Auto Fit Y` で見切れを回避。

## 7. 既存実装との接続方針

## 7.1 起動導線

`TypedDynamicValueDrawer` から Expression Source 用ボタンを追加し、Window を開く。

対象:

- `FloatExpressionSource`
- `IntExpressionSource`

## 7.2 Editor クラス配置案

1. `Assets/GameLib/Script/Common/_Editor/Dynamic/Expression/ExpressionGraphPreviewWindow.cs`
2. `Assets/GameLib/Script/Common/_Editor/Dynamic/Expression/ExpressionGraphSamplingService.cs`
3. `Assets/GameLib/Script/Common/_Editor/Dynamic/Expression/ExpressionPlotDiagnostics.cs`

## 8. 描画仕様 (v0.1)

1. `x` 範囲: 既定 `[-10, 10]`
2. サンプル数: 既定 `201`
3. 線描画: `Handles.DrawAAPolyLine`
4. `IntExpression` は `RoundToInt` 後値を y に採用
5. NaN/Infinity は区間分断扱いで線を切る

## 9. 受け入れ条件

1. Float/Int の式で Window を開ける。
2. x軸変数を選ばない限り描画されない。
3. 非x数値変数に固定値入力できる。
4. string 混在時に描画されず理由が表示される。
5. parser エラー時に理由が表示される。
6. 単純二次式 (`a*x*x+b*x+c`) を正常描画できる。

## 10. リスクと対応

1. 型推定の曖昧さ
- 対応: v0.1 は警告表示 + 固定値入力で回避。

2. 関数戻り値型の不確実性
- 対応: `ExpressionFunctionRegistry` へ将来メタ追加を計画。

3. Scope 依存 source の評価不一致
- 対応: Editor では未対応とし、理由表示を明確化。

## 11. 今後の拡張案

1. BoolExpression のしきい値可視化。
2. パラメータスイープ (非x変数を複数系列で同時描画)。
3. 凸性判定、極値推定、交点推定。
4. CSV 出力。
5. 関数辞書の戻り値型メタ化。

## 12. 実装フェーズ提案

Phase 1 (最小実装):

1. Window 枠 + 解析 + 単線描画
2. x軸選択 + 固定値入力
3. string 混在時の理由表示

Phase 2 (安定化):

1. Diagnostics 強化
2. Auto Fit / Grid / Hover readout
3. 既知関数の戻り値判定改善

Phase 3 (運用強化):

1. Drawer 導線の改善
2. 複数系列比較
3. Export 機能

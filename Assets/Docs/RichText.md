# RichText ドキュメント

このドキュメントは `RichTextSource` の使い方をまとめたものです。

RichText はただの文字列結合ではなく、Expression と同じ式エンジンを使って値を取り出し、整形し、必要なら色やタグを付けて返します。UI の説明文、ステータス表示、Trait の説明、共通メッセージの再利用に向いています。

## 2つのモード

`RichTextSource` には 2 つのモードがあります。

- `Template`
  - テンプレート文字列の中でプレースホルダを展開します。
  - もっとも一般的な使い方です。
- `RefService`
  - `Ref Key` で指定したキーを `IRichTextRefService` から引いて返します。
  - 同じ文面を複数箇所で共有したいときに便利です。

## Template モードの基本

テンプレートは、文字列の中にプレースホルダを書いて値を差し込みます。

```text
HP: {Hp}
```

プレースホルダの基本形は次の通りです。

```text
{identifier|option|option|...}
```

- `identifier` は変数キーです。
- `identifier` が式っぽい内容なら、そのまま Expression として評価されます。
- `option` は書式オプションや装飾オプションです。

### 波括弧のエスケープ

- `{{` は `{` になります。
- `}}` は `}` になります。

```text
{{HP}} => {HP}
```

### Expression として解釈される書き方

`identifier` に次のような文字が入ると、式として解釈されます。

- 空白
- `(` `)`
- `+ - * / %`
- `< > = !`
- `& |`
- `,`
- `"`

例:

```text
{CurrentHp / MaxHp|percent=1}
{Round(Score / 100, 1)|suffix="%"}
```

### 変数の扱い

RichText の変数は `ExpressionVariable` から読み込みます。

- `ExpressionKey` がプレースホルダで使う名前です。
- `Use Custom Key` が有効なら、カスタムキーが優先されます。
- `Use Scalar Leaf Key` が有効なら、スカラー系ソースの leaf 名を短いキーとして使えます。
- `Expected Type` は型ヒントです。`Auto` の場合はソースから推論します。

`Allow Implicit Keys` を有効にすると、Variables に明示していないキーでも、現在の VarStore から解決できる場合があります。厳密に管理したい場合はオフにしてください。

外部から変数を渡す場合は、`TrySetExternalExpressionVariables` / `SetExternalVariables` を使います。

- `includeLocalVariables = false` なら外部変数だけを使います。
- `includeLocalVariables = true` ならローカル変数も混ぜます。
- 外部変数があるときは、ローカル変数の重複扱いが切り替わります。

### `value` は予約名

`value` は RichText の内部で予約されています。

- プレースホルダの先頭識別子としては使えません。
- `cond` の中では、そのプレースホルダの値を表す変数として使えます。

例:

```text
{Hp|cond=value > 0}
```

## 書式オプション

書式オプションは値の見た目を整えます。数値向けのオプションは、bool / int / float の値に対して使う想定です。

- `empty="..."`
  - 値が空、または解決に失敗したときの代替文字列です。
  - 例: `{Hp|empty="--"}`

- `sign=auto` / `sign=always`
  - `always` にすると、0 以上の数値に `+` を付けます。
  - 例: `{Delta|sign=always}`

- `round=n`
  - 小数第 `n` 位で丸めます。
  - `n` は 0 以上の整数です。
  - RichText 側の `round` は `Math.Round(..., MidpointRounding.AwayFromZero)` です。
  - これは Expression の `Round()` とは完全には同じではありません。
  - 例: `{Hp|round=1}`

- `fixed=n`
  - 小数第 `n` 位まで固定表示します。
  - 足りない桁は 0 で埋めます。
  - 例: `{HpRatio|fixed=2}`

- `percent`
  - 値を 100 倍して `%` を付けます。
  - 例: `{HpRatio|percent}`

- `percent=n`
  - 値を 100 倍して、必要なら小数第 `n` 位まで固定表示します。
  - `round` / `fixed` を別で指定していなければ、`fixed=n` と同じ扱いになります。
  - 例: `{HpRatio|percent=1}`

- `prefix="..."`
  - 先頭に文字列を付けます。
  - 例: `{Hp|prefix="HP: "}`

- `suffix="..."`
  - 末尾に文字列を付けます。
  - 例: `{Hp|suffix=" pt"}`

### 書式オプションの例

```text
{Hp|fixed=0|prefix="HP: "}
{Score|sign=always|fixed=0}
{HpRatio|percent=1}
{Value|empty="---"}
```

## 装飾オプション

装飾オプションは、結果文字列に RichText タグを追加します。

- `color="#RRGGBB"`
  - `<color=...>` を付けます。
  - 6 桁の 16 進色コードのみを受け付けます。
  - 例: `{Hp|color="#66FF66"}`

- `wrap="..."`
  - 前後に任意のタグや文字列を付けます。
  - 例: `{Name|wrap="<b>"|wrapEnd="</b>"}`

- `wrapEnd="..."`
  - `wrap` の後ろに付ける文字列です。
  - 省略した場合は空文字です。

- `effect=color_if`
  - `cond` の結果に応じて色を切り替えます。
  - `trueColor` と `falseColor` が必要です。
  - `color` とは併用できません。

- `trueColor="#RRGGBB"`
  - `effect=color_if` のとき、条件が真のときの色です。

- `falseColor="#RRGGBB"`
  - `effect=color_if` のとき、条件が偽のときの色です。

### `cond` の扱い

`cond` は Expression です。`value` という変数名で、そのプレースホルダの値を参照できます。

- `effect=color_if` を使わない場合
  - `cond` が false のとき、プレースホルダ全体は空になります。
- `effect=color_if` を使う場合
  - `cond` が true なら `trueColor`、false なら `falseColor` が使われます。
  - この場合、`cond` は可視/不可視ではなく色分岐に使われます。

### 装飾オプションの例

```text
{Hp|cond=value > 0|color="#66FF66"}
{Hp|effect=color_if|cond=value > 0|trueColor="#66FF66"|falseColor="#FF6666"}
{Name|wrap="<b>"|wrapEnd="</b>"}
```

## RefService モード

`RefService` モードは、登録済みの RichText をキーで引く方法です。

- `Ref Key` には `DynamicValue<string>` を指定できます。
- `RichTextRefServiceMB` をスコープに置くと `IRichTextRefService` が登録されます。
- `TryRegister` でキーと `RichTextProvider` を紐付けます。
- 共有文言、Trait の説明、共通ラベルの再利用に向いています。

### 例

```csharp
var source = new RichTextSource();
// Inspector で Source Mode = RefService、Ref Key = "Trait.Speed.Description" を設定する

richTextRefService.TryRegister(
    "Trait.Speed.Description",
    new RichTextProvider(source),
    overwrite: true);
```

このモードでは、キーが見つからない、または `IRichTextRefService` が見つからない場合は空文字を返し、開発環境では警告ログが出ます。

## よくある使い方

### 1. シンプルな数値表示

```text
HP: {Hp|fixed=0}/{MaxHp|fixed=0}
```

### 2. パーセンテージ表示

```text
{HpRatio|percent=1}
```

### 3. 条件付き色分け

```text
{Hp|effect=color_if|cond=value > 0|trueColor="#66FF66"|falseColor="#FF6666"}
```

### 4. 式をそのまま埋め込む

```text
{Round(CurrentHp / MaxHp * 100, 1)|suffix="%"}
```

### 5. 文字列にタグを付ける

```text
{Title|wrap="<b>"|wrapEnd="</b>"}
```

## つまずきやすい点

- `value` は予約名です。Variables で同じ名前は使わないでください。
- 数値書式オプションは、数値に変換できない値には使えません。
- `effect=color_if` では `cond`、`trueColor`、`falseColor` が必須です。
- `color` と `effect=color_if` は同時に使えません。
- `round` は Expression の `Round()` と実装が違います。RichText は表示用、Expression は式計算用です。
- 文字列の中に `|` や `=` を入れたいときは、値を `"..."` で囲むと安全です。

## どのファイルを見るか

- Template / RefService の本体: `Assets/GameLib/Script/Common/Variables/Dynamic/RichText/RichTextSource.cs`
- テンプレート解析: `Assets/GameLib/Script/Common/Variables/Dynamic/RichText/RichTextTemplateCompiler.cs`
- 数値整形: `Assets/GameLib/Script/Common/Variables/Dynamic/RichText/RichTextOptions.cs`
- 装飾処理: `Assets/GameLib/Script/Common/Variables/Dynamic/RichText/RichTextEffects.cs`
- RefService 実装: `Assets/GameLib/Script/Common/Variables/Dynamic/RichText/RichTextRefService.cs`

# Expression 関数リファレンス

このドキュメントは、`BoolExpressionSource`、`FloatExpressionSource`、`IntExpressionSource`、`Vector2ExpressionSource`、`Vector2XYExpressionSource`、`Vector3ExpressionSource`、`Vector3XYZExpressionSource` で共通の Expression 言語と、`ExpressionFunctionRegistry` に登録されている関数をまとめたものです。

RichText の `cond` や、RichText の「式として解釈されるプレースホルダ」でも同じ Expression エンジンを使います。先にここを読んでおくと、RichText 側の説明がかなり読みやすくなります。

## まず押さえるポイント

- 関数名は大文字小文字を区別します。`Round()` は有効ですが `round()` は別名として登録されていません。
- 変数名も大文字小文字を区別します。
- `BoolExpressionSource` は最終結果を bool に変換します。
- `FloatExpressionSource` は最終結果を float に変換します。
- `IntExpressionSource` は最終結果を `Mathf.RoundToInt` で int に丸めます。
- `Vector2ExpressionSource` は Vector2 互換の結果が必要です。
- `Vector2XYExpressionSource` は X と Y を別々に評価して Vector2 を作ります。
- `Vector3ExpressionSource` は Vector3 互換の結果が必要です。
- `Vector3XYZExpressionSource` は X, Y, Z を別々に評価して Vector3 を作ります。
- 数値コンテキストでは、bool は 1 / 0 として扱われます。
- 比較では、float の微小誤差はほぼ同一として扱われます。

## 式の基本文法

- 数値: `12`、`3.5`、`.25`
- 文字列: `"hello"`、`"line\nbreak"`
- bool: `true`、`false`
- 変数: `HP`、`MaxHp`、`Player.Speed`
- 関数呼び出し: `Round(Score / 100, 1)`
- 単項演算子: `!x`、`-x`
- 二項演算子: `+ - * / %`
- 比較演算子: `== != < <= > >=`
- 論理演算子: `&& ||`
- 括弧: `( ... )`

補足:

- `a <= b < c` のような連鎖比較は、`(a <= b) && (b < c)` と同じ意味になります。
- 文字列比較は ordinal 比較です。
- `true` / `false` は識別子ではなく bool リテラルとして解釈されます。
- 文字列は `\"`、`\\`、`\n`、`\r`、`\t` をエスケープできます。

## 変数の扱い

式の変数は `ExpressionVariable` から読み込まれます。

- `ExpressionKey` が式内で使う名前です。
- `Use Custom Key` が有効なら、そのキーが優先されます。
- `Use Scalar Leaf Key` が有効なら、スカラー系ソースの leaf 名を短いキーとして使えます。
- `Expected Type` は型ヒントです。`Auto` の場合はソース値から推論します。

外部変数を注入したい場合は、`TrySetExternalExpressionVariables` / `SetExternalVariables` を使います。

- `includeLocalVariables = false` の場合、外部変数だけが有効です。
- `includeLocalVariables = true` の場合、ローカル変数も混ぜて使えます。
- `Allow Implicit Keys` をオフにすると、Variables に定義されていない識別子はエラーになります。

## Round の意味

`Round` は少し紛らわしいので、ここだけ先に明確にします。

- `Round(x)` は、`x` を整数桁に丸めた値を float で返します。
- `Round(x, digits)` は、`x` を小数第 `digits` 位まで丸めます。
- `digits` はそのまま使うのではなく、整数に丸めたあと `0..6` にクランプされます。
- 実装は `Mathf.Round(value * 10^digits) / 10^digits` です。
- `RoundToInt(x)` は別関数で、int を返します。

例:

```text
Round(12.345)
=> 12

Round(12.345, 2)
=> 12.35

Round(12.345, 0)
=> 12

Round(-12.345, 2)
=> -12.35
```

## 関数一覧

### 汎用数値

- `Random()`
  - `0..1` の乱数を返します。`UnityEngine.Random.value` と同じ用途です。
  - 例: `Random() < 0.5`

- `Random(a, b)`
  - `a` から `b` の範囲で float の乱数を返します。`UnityEngine.Random.Range(a, b)` の float 版です。
  - 例: `Random(10, 20)`

- `Min(a, b, ...)`
  - 1 以上 32 までの引数を受け取り、最小値を返します。
  - 例: `Min(HP, MaxHp)`

- `Max(a, b, ...)`
  - 1 以上 32 までの引数を受け取り、最大値を返します。
  - 例: `Max(0, Damage - Defense)`

- `Abs(x)`
  - 絶対値を返します。
  - 例: `Abs(Delta)`

- `Sign(x)`
  - 符号を返します。正なら `1`、0 なら `0`、負なら `-1` です。
  - 例: `Sign(Direction)`

- `Floor(x)` / `Ceil(x)`
  - `Floor` は切り捨て、`Ceil` は切り上げです。
  - 例: `Floor(3.9)`、`Ceil(3.1)`

- `Round(x)` / `Round(x, digits)` / `RoundToInt(x)`
  - `Round` は丸め、`RoundToInt` は int 返却です。
  - `Round(x, digits)` の `digits` は小数点以下の桁数です。
  - 例: `Round(Score / 100, 1)`、`RoundToInt(PlayerLevel * 1.2)`

- `TimeHMS(totalSeconds, showHours, showMinutes, showSeconds, padMinutesSeconds)`
  - 秒数を `1h 02m 03s` のような文字列に整形します。
  - `showHours` / `showMinutes` / `showSeconds` は 0 / 1 でも指定できます。
  - `padMinutesSeconds` を有効にすると、時間が表示されているときに分と秒が 2 桁になります。
  - 例: `TimeHMS(RemainingSeconds, 1, 1, 1, 1)`

### 三角関数と角度変換

- `Sin(x)` / `Cos(x)` / `Tan(x)`
  - それぞれ正弦 / 余弦 / 正接です。角度はラジアンです。
  - 例: `Sin(Deg2Rad(90))`

- `Asin(x)` / `Acos(x)` / `Atan(x)` / `Atan2(y, x)`
  - それぞれ逆三角関数です。
  - `Atan2` は `y, x` の順です。
  - 例: `Atan2(DeltaY, DeltaX)`

- `Deg2Rad(x)` / `Rad2Deg(x)`
  - 度とラジアンを相互変換します。
  - 例: `Sin(Deg2Rad(AngleDeg))`

### 範囲、補間、変換

- `Pow(x, y)` / `Sqrt(x)` / `Exp(x)` / `Log(x)` / `Log(x, base)` / `Log10(x)`
  - べき乗、平方根、指数、対数です。
  - 例: `Sqrt(HP)`, `Pow(Base, Exp)`

- `Clamp(value, min, max)` / `Clamp01(value)`
  - 値を範囲内に制限します。
  - 例: `Clamp(HP, 0, MaxHp)`

- `Lerp(a, b, t)` / `LerpUnclamped(a, b, t)` / `LerpAngle(a, b, t)`
  - 線形補間、範囲外補間、角度補間です。
  - 例: `Lerp(0, 100, Clamp01(T))`

- `InverseLerp(a, b, value)`
  - `value` が `a..b` のどこにあるかを 0..1 で返します。
  - 例: `InverseLerp(MinHp, MaxHp, CurrentHp)`

- `MoveTowards(current, target, maxDelta)` / `MoveTowardsAngle(current, target, maxDelta)`
  - 現在値から目標値へ最大差分だけ近づけます。
  - 例: `MoveTowards(Current, Target, Speed * DeltaTime)`

- `SmoothStep(from, to, t)`
  - なめらかな補間です。
  - 例: `SmoothStep(0, 1, Clamp01(Progress))`

- `Gamma(value, absmax, gamma)`
  - Unity の `Mathf.Gamma` のラッパーです。ガンマ補正系の用途で使います。

- `Repeat(t, length)` / `PingPong(t, length)` / `DeltaAngle(current, target)`
  - `Repeat` は繰り返し、`PingPong` は往復、`DeltaAngle` は最短角度差です。
  - 例: `PingPong(Time, 2)`

- `PerlinNoise(x, y)`
  - 0..1 の滑らかなノイズを返します。
  - 例: `PerlinNoise(Time * 0.25, 0)`

- `Approximately(a, b)`
  - 2 つの値がほぼ等しいかを bool で返します。
  - 例: `Approximately(Ammo, 0)`

- `SinOut(t)`
  - `sin(clamp01(t) * PI / 2)` の形の簡易イージングです。
  - 例: `SinOut(Progress)`

### Vector2

Vector 系の関数は、`Vec2` で明示的に作った値だけでなく、Vector2 相当の値を扱えます。

- `Vec2(x, y)`
  - Vector2 を作ります。
  - 例: `Vec2(1, 0)`

- `Dot(a, b)` / `Cross(a, b)`
  - 内積と 2D 外積です。
  - `Cross` はスカラー値を返します。
  - 例: `Dot(Vec2(1, 0), Direction)`

- `Magnitude2(v)` / `SqrMagnitude2(v)`
  - ベクトル長と長さの二乗です。
  - 例: `Magnitude2(Velocity)`

- `Normalize2(v)` / `Perp(v)`
  - 正規化ベクトルと直交ベクトルを返します。
  - 例: `Normalize2(Direction)`

- `Project2(vector, onto)` / `Reflect2(direction, normal)`
  - 投影と反射です。
  - 例: `Project2(Velocity, GroundNormal)`

- `ClampMagnitude2(v, maxLength)`
  - ベクトル長を最大値に制限します。
  - 例: `ClampMagnitude2(Velocity, MaxSpeed)`

- `Lerp2(a, b, t)` / `MoveTowards2(current, target, maxDelta)`
  - Vector2 用の補間と接近です。
  - 例: `Lerp2(Start, End, Clamp01(T))`

- `Angle2(from, to)` / `SignedAngle2(from, to)`
  - ベクトル間の角度を度で返します。
  - `SignedAngle2` は符号付きです。
  - 例: `SignedAngle2(Vec2(1, 0), Direction)`

### Vector3

Vector3 は `Vec3(x, y, z)` で作れます。Inspector では `DynamicValue<Vector3>` に対して次の 2 つの Expression ソースを使えます。

- `Vector3ExpressionSource`
  - 単一の式を Vector3 として評価します。
  - 例: `Vec3(Cos(Deg2Rad(AngleDeg)), Sin(Deg2Rad(AngleDeg)), Height)`

- `Vector3XYZExpressionSource`
  - X, Y, Z を個別の式で評価して Vector3 を作ります。
  - 例: X=`OffsetX` / Y=`OffsetY` / Z=`OffsetZ`

- `Vec3(x, y, z)`
  - Vector3 を作ります。
  - 例: `Vec3(1, 2, 3)`

## 具体例

```text
Round(RequestBaseQuota * QuotaMul)
```

`RequestBaseQuota * QuotaMul` を整数桁に丸めます。`IntExpressionSource` ではさらに最後に int 化されます。

```text
Clamp(PlayerHp / MaxHp, 0, 1)
```

体力比率を 0..1 に制限します。

```text
TimeHMS(RemainingSeconds, 1, 1, 1, 1)
```

残り時間を `1h 02m 03s` のように表示します。

```text
Vec2(Cos(Deg2Rad(AngleDeg)), Sin(Deg2Rad(AngleDeg))) * Speed
```

角度から進行方向ベクトルを作り、速度を掛けます。

```text
Vec3(Cos(Deg2Rad(AngleDeg)), Sin(Deg2Rad(AngleDeg)), Height)
```

Vector3 をそのまま組み立てます。

```text
PerlinNoise(Time * 0.2, 0) * 10
```

0..10 の揺らぎを作ります。

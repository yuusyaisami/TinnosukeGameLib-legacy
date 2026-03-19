<!--
Spec Version: v0.1
Status: Draft / Planning
Updated: 2026-03-18
Change Summary:
- 本仕様書を新規作成。
- Command 実行文脈へ複数の LTS スロットを持たせる改修案を定義。
- HitColliderController の Self 実行から Other LTS を OnUse などへ引き継ぐための設計を整理。
- 旧 Editor 参照維持や legacy 互換は考慮しない前提で記述。
- 本書はコード変更前の事前仕様であり、関連コードを読んだ上で作成している。

Primary References Read:
- Assets/GameLib/Script/Common/Commands/VNext/Core/CommandContext.cs
- Assets/GameLib/Script/Common/Commands/VNext/Core/CommandRunner.cs
- Assets/GameLib/Script/Common/Commands/VNext/Core/ActorScopeResolver.cs
- Assets/GameLib/Script/Common/Commands/VNext/Core/ActorSourceFastResolver.cs
- Assets/GameLib/Script/Common/Commands/VNext/Commands/Core/WithActorCommandData.cs
- Assets/GameLib/Script/Common/Commands/VNext/Executors/Core/WithActorExecutor.cs
- Assets/GameLib/Script/Common/Commands/VNext/Executors/Core/WithPlayerExecutor.cs
- Assets/GameLib/Script/Common/Commands/VNext/Executors/Core/WithActorDescendantRouterExecutor.cs
- Assets/GameLib/Script/Common/Commands/VNext/Executors/UI/UIControlExecutor.cs
- Assets/GameLib/Script/Collision/Channel/HitColliderControllerService.cs
- Assets/GameLib/Script/Collision/Commands/WithHitColliderTargetsExecutor.cs
- Assets/GameLib/Script/Common/StatusEffect/Core/StatusEffectService.cs
- Assets/GameLib/Script/Common/StatusEffect/Core/StatusEffectRuntime.cs
- Assets/GameLib/Script/Common/StatusEffect/Core/StatusEffectDefinitionTypes.cs
- Assets/GameLib/Script/Common/Commands/VNext/Commands/StatusEffect/StatusEffectCommandData.cs
- Assets/GameLib/Script/Common/Commands/VNext/Executors/StatusEffect/StatusEffectExecutors.cs
- Assets/GameLib/Script/Common/Commands/VNext/Sources/CatalogCommandSource.cs
- Assets/GameLib/Script/Common/Commands/VNext/Catalog/CommandCatalogSO.cs
- Assets/GameLib/Script/Common/Commands/VNext/Catalog/CommandKeyRegistry.cs
- Assets/GameLib/Script/Common/Commands/VNext/Catalog/CommandKeyResolver.cs
-->

# Command Context LTS Slot Refactor Spec v0.1

## 1. 結論

`CommandContext` が現在持っている固定スロット

- `Scope`
- `Actor`
- `CommandRootScope`
- `RootActor`
- `CallerActor`

だけでは、`HitColliderController` のように「Self 側で command を実行しつつ、Other 側の LTS を後続 command でも参照したい」ケースを十分に吸収できない。

このため、次の方向で全面置換する。

1. `CommandContext` に「任意数ではなく、enum で識別された固定長の LTS スロット配列」を導入する。
2. 既存の `Actor / CommandRoot / RootActor / CallerActor` も、この新スロット系へ寄せて扱う。
3. `ActorSource` は `Current / Player / Global` だけでなく、「CommandContext 上の指定 LTS スロット」を参照できるように拡張する。
4. `HitColliderControllerService` は Self 実行時に `Other` を文脈スロットへ書き込み、その文脈を引き継いで `StatusEffect OnUse` などへ渡す。
5. 旧 asset 互換や旧 Editor 配線維持は不要とし、既存データは破壊的に置き換える。

今回の主眼は「文脈内の LTS 参照経路を一般化し、特定機能ごとの専用 if を増やさないこと」である。

## 2. 現行仕様の整理

### 2.1 `CommandContext` は固定スロット型

現行 `CommandContext` は実質的に次の 5 つだけを持つ。

- 実行スコープ `Scope`
- 実行 actor `Actor`
- 実行開始元 `CommandRootScope`
- 最初の actor `RootActor`
- 一段前の actor `CallerActor`

`WithActorExecutor`、`WithPlayerExecutor`、`UIControlExecutor`、`WithHitColliderTargetsExecutor` などは、新しい `CommandContext` を作る際にこの 5 つを手作業で引き継いでいる。

つまり現状は「文脈継承の拡張点」が固定で、追加用途を入れるたびに `CommandContext` のメンバ自体を増やす必要がある。

### 2.2 `ActorSource` は固定の取得元しか持たない

現行 `ActorSourceKind` は以下。

- `Current`
- `ByIdentity`
- `FromUnityObject`
- `GameLogicRoot`
- `Player`
- `CommandRootActor`
- `Global`
- `Shared`

ここに「Hit 相手」「任意の一時 LTS スロット」「文脈上の caller 以外の補助 actor」のような概念はない。

そのため、文脈に載っている追加情報を `ActorSource` 経由で自然に引く仕組みがない。

### 2.3 `HitColliderControllerService` は今でも Other を知っている

`HitColliderControllerService` は衝突時に `OtherScope` を解決できており、現在も次の情報を書いている。

- `Hit`
- `HitMeta`
- `IsOtherSide`
- `HitEvent`
- `SelfScope`
- `OtherScope`

ただしこれは `VarStore` に managed ref として積んでいるだけで、`ActorSource` や `CommandContext` の標準経路には乗っていない。

その結果、`StatusEffectExecutor` や `ScalarModifierOperationDefinition` のように `ActorSource` ベースで対象 actor を決める処理からは、この `OtherScope` を直接使えない。

### 2.4 `StatusEffect OnUse` は元の `CommandContext` を持ち越していない

`StatusEffectRuntime.Use` は `ExecuteHook(StatusEffectHookKind.Use, userScope ?? _scope)` を呼ぶが、`ExecuteHook` 内では新しく

```csharp
new CommandContext(_scope, _vars, runner, actorScope, CommandRunOptions.Default)
```

を生成している。

この時点で元の `CommandRootScope / RootActor / CallerActor` や、将来追加したい補助 LTS スロットは落ちる。

つまり、仮に `HitColliderControllerService` から Self 実行コンテキストに `Other` を入れても、現状の `StatusEffect OnUse` はその情報を保持できない。

### 2.5 `WithHitColliderTargetsExecutor` は「対象切り替え」はできるが「文脈保持」ではない

`WithHitColliderTargetsExecutor` は現在 hit 対象の scope を列挙し、その target を actor とした新規 `CommandContext` を作って body を回す。

これは「hit 対象へ切り替えて実行する」用途には使える。
一方で今回欲しいのは「Self 実行のまま、Other を文脈追加参照として保持する」ことであり、用途が異なる。

## 3. 今回解決したい課題

解きたい問題は次の形で一般化できる。

1. command 実行主体は Self のままにしたい。
2. だが処理対象の一部は Self ではなく Other になる。
3. その対象は機能ごとに異なり、StatusEffect、Trait、Health、将来の別機能でも起こる。
4. そのたびに専用 var 名や専用 executor を増やすと、文脈設計が破綻する。

したがって必要なのは、

- command 実行文脈に複数の `IScopeNode` を標準保持できること
- その保持先を enum で安定指定できること
- `ActorSource` からその enum を指定して引けること
- 後続 command / sub-command / hook 実行でも文脈が自然に継承されること

である。

## 4. 採用する設計方針

### 4.1 可変長 `List` ではなく enum ベース固定長配列を採用する

今回の用途では、毎回自由に名前を増やせる dictionary よりも、次を優先する。

- 実行時コストが軽い
- 参照が速い
- inspector と code の両方で扱いが安定する
- typo を避けられる

そのため、保有場所は `List` ではなく

- `enum CommandLtsSlot`
- `IScopeNode?[]`

の組み合わせを採用する。

### 4.2 「固定の意味を持つスロット」と「汎用スロット」を両立させる

推奨 enum の考え方は次の通り。

```csharp
public enum CommandLtsSlot
{
    None = 0,

    Scope = 10,
    Actor = 20,
    CommandRoot = 30,
    RootActor = 40,
    CallerActor = 50,

    ContextA = 100,
    ContextB = 110,
    ContextC = 120,
    ContextD = 130,
}
```

ポイント:

1. `Scope` など既存の意味を持つスロットは enum に明示する。
2. `ContextA` 以降は機能横断の汎用スロットとして予約する。
3. `HitColliderController` はまず `ContextA` を `Other` 用として使う。
4. 将来、`ContextB` を summon 元、`ContextC` を lock target、`ContextD` を UI owner などに使える。

ここで重要なのは、「Other 専用 enum」を先に大量追加しないこと」である。

最初から `HitOther`, `SummonOwner`, `DamageSource`, `Target`, `SubTarget` と機能名で増やすと、また固定スロット地獄に戻る。

### 4.3 `CommandContext` はスロット所有者になる

`CommandContext` の責務を次のようにする。

- `Scope` 等の既存プロパティは残してよい
- ただし内部的にはスロット配列へ正規化して保持する
- `GetScope(CommandLtsSlot slot)` / `SetScope(CommandLtsSlot slot, IScopeNode? scope)` を提供する
- 新しい `CommandContext` 生成時に、スロット配列を明示的に継承できる

これにより既存コードは段階的に移行できる。

### 4.4 `ActorSource` に「Context Slot 参照」を追加する

`ActorSourceKind` に新規種別を追加する。　(できたら10の倍数で)

```csharp
public enum ActorSourceKind
{
    Current = 0,
    ByIdentity = 3,
    FromUnityObject = 4,
    GameLogicRoot = 5,
    Player = 6,
    CommandRootActor = 7,
    Global = 8,
    Shared = 9,
    ContextSlot = 10,
}
```

`ActorSource` には追加で以下を持たせる。

```csharp
[ShowIf("@Kind == ActorSourceKind.ContextSlot")]
public CommandLtsSlot ContextSlot;
```

これにより、例えば `StatusEffect` の `TargetActorSource` で `ContextSlot(ContextA)` を選べば、OnUse から hit 相手の LTS を直接参照できる。

### 4.5 `ActorSourceFastResolver` の起点は `ctx` 全体にする

現行 `ActorSourceFastResolver.Resolve` は

```csharp
Resolve(IScopeNode? origin, in ActorSource source, IScopeNode? commandRootScope = null)
```

であり、`ContextSlot` を解決するには情報が足りない。

そのため、最終形では次のどちらかへ寄せる。

1. `Resolve(CommandContext ctx, in ActorSource source)`
2. 既存 overload を残しつつ、`CommandContext` を受ける overload を追加する

推奨は 2 である。

理由:

- 既存の `StatusEffectBuildContext` など `CommandContext` ではない文脈もある
- すべてを一気に置換すると差分が大きい
- `ContextSlot` が必要な箇所だけ `CommandContext` overload を使えばよい

## 5. 具体的な文脈継承ルール

### 5.1 `CommandContext` 生成時の初期化ルール

新規 `CommandContext` 作成時は次を基本ルールとする。

1. `Scope` スロットには実行 scope を入れる
2. `Actor` スロットには実行 actor を入れる
3. `CommandRoot` は最初の root を維持する
4. `RootActor` は最初の actor を維持する
5. `CallerActor` は直前の actor を入れる
6. `ContextA-D` は原則そのまま継承する

つまり補助スロットは「明示的に上書きするまで持続する」。

### 5.2 `WithActor` 系は補助スロットを壊さない

次の executor は新しい context を作るが、補助スロットは継承する。

- `WithActorExecutor`
- `WithPlayerExecutor`
- `WithActorDescendantRouterExecutor`
- `UIControlExecutor`
- `WithHitColliderTargetsExecutor`

これにより、上位で積んだ `ContextA` が body 内でも使える。

### 5.3 `StatusEffectRuntime` も元の context を継承できるようにする

ここが今回の最重要点のひとつである。

現行は `StatusEffectRuntime` の hook 実行で元 context を失う。
これを次のように変える。

1. `Use` 実行時、`StatusEffectRuntime.Use` は `IScopeNode? userScope` だけでなく `CommandContext? sourceContext` を受け取れるようにする。
2. `StatusEffectService.Use` も `userScope` だけでなく `CommandContext? sourceContext` を受け取る overload を持つ。
3. `StatusEffectExecutor` の `Use` は `service.Use(filter, ctx.Actor ?? ctx.Scope, ctx)` のように元 context ごと渡す。
4. `StatusEffectRuntime.ExecuteHook` は、元 context があればそのスロット群を継承しつつ、必要な actor だけ差し替えた context を生成する。

これにより、`HitColliderController -> Self command -> StatusEffect Use -> OnUse command` の流れで `ContextA=Other` を保てる。

## 6. HitColliderController での使用仕様

### 6.1 Self 実行時に Other を補助スロットへ積む

`HitColliderControllerService.ExecuteOnSelf` と `ExecuteOnSelfAsync` では、現在

```csharp
new CommandContext(_ownerScope, vars, runner, actor: _ownerScope, options)
```

を作っている。

これを新仕様では次のようにする。

1. まず通常の Self context を作る
2. `OtherScope` が解決できていれば `CommandLtsSlot.ContextA` にその `otherScope` を設定する
3. その context で Self command を実行する

この時点で Self command 群は

- 実行主体は Self
- `ContextA` には Other

という状態になる。

### 6.2 Other 実行時の扱い

`ExecuteOnOther` は現在、Other 側を actor / scope にした context を新規作成している。

新仕様ではここでも、必要なら次を設定できる。

- `Actor = Other`
- `CommandRoot = Self`
- `RootActor = Self`
- `CallerActor = Self`
- `ContextA = Self`

つまり Self 実行と鏡像の文脈を作れる。

これにより、Other 側 command でも「自分を殴った相手」を `ContextA` から逆参照できる。

### 6.3 `VarStore` の `OtherScope` は残してよい

今回の改修で `OtherScope` var を消す必要はない。

理由:

- DynamicSource や debug 用には var の方が便利な場面がある
- 既存の var 読み取り系ロジックを即時に破壊せず済む

ただし、今後の「actor 解決」は var ではなく `ContextSlot` を正規経路とする。

## 7. 推奨 API 変更

### 7.1 `CommandContext`

追加または変更する責務:

- `IScopeNode?[] _ltsSlots`
- `IScopeNode? GetScope(CommandLtsSlot slot)`
- `void SetScope(CommandLtsSlot slot, IScopeNode? scope)`
- `CommandContext CloneWith(...)`
- `CommandContext WithSlot(CommandLtsSlot slot, IScopeNode? scope)`
- 既存の `Actor / CommandRootScope / RootActor / CallerActor` は slot の sugar property 化

### 7.2 `ActorSource`

追加:

- `ActorSourceKind.ContextSlot`
- `CommandLtsSlot ContextSlot`

Editor 表示:

- `ActorSourceOdinLabelHelper` は `ContextSlot` の summary 表記に対応する

### 7.3 `ActorSourceFastResolver`

追加:

- `Resolve(CommandContext ctx, in ActorSource source)`
- `ResolveCached(CommandContext ctx, in ActorSource source, ref ActorSourceResolveCache cache)`

既存の `Resolve(origin, source, commandRootScope)` は残してよいが、`ContextSlot` では `null` を返す。

### 7.4 `StatusEffectService` / `StatusEffectRuntime`

追加:

- `StatusEffectService.Use(StatusEffectRuntimeFilter filter, IScopeNode? userScope, CommandContext? sourceContext)`
- `StatusEffectRuntime.Use(IScopeNode? userScope, CommandContext? sourceContext)`

内部保持:

- 直近 use context を永続保存する必要はない
- hook 実行時に引数で受けた context をそのまま使えばよい

### 7.5 `StatusEffectBuildContext`

将来的に operation build 時点でも `ContextSlot` を使いたい場合に備え、`CommandContext` 互換の LTS スロット参照口を追加できる形にしておく。

ただし初回実装では無理に統一しなくてよい。

理由:

- build 時点は apply request 文脈であり、runtime use 文脈とは用途が違う
- 今回の主問題は OnUse 実行時である

## 8. 影響範囲

### 8.1 必須改修

- `CommandContext`
- `ActorSourceKind`
- `ActorSource`
- `ActorSourceFastResolver`
- `ActorScopeResolver`
- `ActorSourceOdinLabelHelper`
- `WithActorExecutor`
- `WithPlayerExecutor`
- `WithActorDescendantRouterExecutor`
- `UIControlExecutor`
- `WithHitColliderTargetsExecutor`
- `HitColliderControllerService`
- `StatusEffectService`
- `StatusEffectRuntime`
- `StatusEffectExecutor`
- `StatusEffectDefinitionTypes` の `ActorSourceFastResolver` 利用箇所

### 8.2 任意追従

- command debug 表示に `ContextA-D` を出す
- `CommandRunFrame` にスロット snapshot を入れるかは任意
- DynamicSource 側で `ContextSlot` を露出するかは後続判断

## 9. 非採用案

### 9.1 `VarStore` に全部積んで済ませる

非採用理由:

- actor 解決と var 解決が別経路になり、設計が二重化する
- `ActorSource` を使う既存 command 群から自然に使えない
- 型安全性が低い

### 9.2 `HitColliderController` 専用の `OtherActorSourceKind` を増やす

非採用理由:

- 問題の本質は hit 専用ではない
- 同じ要求が別機能でも再発する
- 専用 enum を増やすほど文脈設計が硬直化する

### 9.3 `List<(enum, scope)>` 形式にする

非採用理由:

- 線形探索が必要になる
- 毎回の boxing や比較が増えやすい
- 実質固定スロット用途なのに可変構造を持つ利点が薄い

## 10. 実装順

### Phase 1: 基盤

1. `CommandLtsSlot` 追加

2. `CommandContext` の内部スロット化
3. 既存 property を slot wrapper 化
4. `ActorSourceKind.ContextSlot` 追加
5. `ActorSourceFastResolver.Resolve(CommandContext, ActorSource)` 追加

### Phase 2: 継承ルール統一

1. `WithActor` 系 executor を slot 継承対応
2. `UIControlExecutor` など新規 context 生成箇所を slot 継承対応
3. debug 表示更新

### Phase 3: HitCollider 統合

1. `HitColliderControllerService` の Self 実行で `ContextA=Other`
2. Other 実行で `ContextA=Self`
3. 必要なら rule 単位で使用スロットを指定可能にする

初回は固定で `ContextA` を使ってよい。

### Phase 4: StatusEffect 統合

1. `StatusEffectService.Use` に source context 引き継ぎを追加
2. `StatusEffectRuntime.ExecuteHook` が source context を継承
3. `OnUse` 内から `ContextSlot(ContextA)` が使えることを確認

## 11. 最終的な利用イメージ

Hit 時の Self command で:

1. `ContextA = OtherScope`
2. `StatusEffect Use` 実行

その `OnUse` command 内で:

1. `TargetActorSource = ContextSlot(ContextA)`
2. その target に対して Health / Trait / Scalar / 別 StatusEffect を操作

これにより、

- command 実行主体は Self のまま
- 実処理対象だけ Other に切り替える
- しかもその参照経路は hit 専用ではなく汎用

という形になる。

## 12. 補足判断

### 12.1 `CommandRootActor` と `CommandRootScope` の整理

現行 `ActorSourceKind.CommandRootActor` は実装上 `commandRootScope` を返しており、名前と実体が少しずれている。

今回スロット化するなら、将来的には

- `CommandRoot`
- `RootActor`

を明示的に使い分けられる enum 設計へ寄せる方がよい。

ただし初回改修では既存挙動を壊しすぎないため、互換 property は残してよい。

### 12.2 `CatalogCommandSource` 問題は別件

`CatalogCommandSource` の `Key` 問題は command 文脈スロット改修とは独立した別トピックである。

ただし、command を stable key 参照へ切り替えている流れ自体は「command 本体を asset 直参照しない」という設計方向で一貫している。

本仕様では `CatalogCommandSource` 自体は改修対象にしない。

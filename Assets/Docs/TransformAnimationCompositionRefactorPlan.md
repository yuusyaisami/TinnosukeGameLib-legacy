<!--
Spec Version: v0.4
Status: Implemented
Updated: 2026-03-13
Note:
- 本仕様書は新規作成です。
- 既存コードを読んだ上で、TransformAnimationChannel 系と TransformController 系の責務分離、および Transform 合成器導入方針を整理しています。
- 本版は構成案の整理を目的としており、実装コードの変更方針を含みますが、この仕様書自体には実装コードは含みません。
- 合成器は必要ですが、TransformAnimationPlayer 内に閉じ込める案は採用しません。
- v0.2 では、Preset / Clip の再生命令ポリシー、`DynamicValue<T>` での preset 指定、Editor Play 後ドリフト対策コードの全面撤去方針、Step 表示改善、PathMode 維持、`SerializeReference` 化と Scene preview 継続案を追記しています。
- v0.3 では、`SerializeReference` 化を進める場合でも、既存と同等の Scene 上 path 描画と handle 操作を維持することを必須要件として明記しています。
- v0.4 では、コードレビューにより発見した次の不足・不整合を補完しています。
  - `TransformContributionMask` の定義追加（6.1 節）
  - `TransformContributionProperty` と既存 `TransformAnimationProperty` の関係明記（7.2 節）
  - `ITransformAnimationOutputSink` / `ITransformAnimationOutputRegistry` の移行方針追加（10.2 節）
  - `TransformAnimationOutput.Clear()` の既存挙動バグへの言及と新設計での対処方針追加（10.2 節）
  - 4.1 全体像図の `TransformAnimationOutput / new composed output` 表記の曖昧さを解消（4.1 節注記）
  - `WriteContribution(ref TransformPoseAccumulator)` の `ref` 指定意図の明記（6.1 節）
- 参照した主なコード:
  - Assets/GameLib/Script/Project/Scene/Channels/Transform/TransformAnimationChannelPlayer.cs
  - Assets/GameLib/Script/Project/Scene/Channels/Transform/TransformAnimationHubService.cs
  - Assets/GameLib/Script/Project/Scene/Channels/Transform/TransformFollowService.cs
  - Assets/GameLib/Script/Project/Scene/Transform/Core/TransformAnimationOutput.cs
  - Assets/GameLib/Script/Project/Scene/Transform/Core/TransformControllerService.cs
  - Assets/GameLib/Script/Project/Scene/Transform/MB/TransformController.cs
  - Assets/GameLib/Script/Common/Commands/VNext/Executors/Channels/TransformAnimationChannelExecutor.cs
-->

# Transform Animation 合成アーキテクチャ再設計案

## 1. 目的

現在の `TransformAnimationChannelPlayer` は、

- Preset / Clip の再生
- Follow
- Shake
- Scroll
- Acquire / Release 時の baseline 管理
- `TransformAnimationOutput` への書き込み
- DOTween 実行

を 1 クラスで抱えています。

その結果、同一 `Transform` に対して複数命令が来たときの扱いが、

- 本来は「合成」
- 現状は「競合相手を止める」

という状態になっています。

今回の再設計目的は次の通りです。

1. 同一 `Transform` への複数命令を停止ではなく合成で扱えるようにする
2. `Follow` `Shake` `Preset/Clip` の責務を分離する
3. `DOTween` の直接 `Transform` 書き換えをやめ、最終適用点を 1 箇所へ寄せる
4. `TransformController` を出力制御に専念させ、アニメーション責務を減らす
5. `MonoBehaviour` を増やさず、plain C# service / track で構成する


## 2. 現状の問題点

### 2.1 `TransformAnimationChannelPlayer` に責務が集まりすぎている

現状の `TransformAnimationChannelPlayer` は player であると同時に、

- effect 実装本体
- lifecycle 管理
- baseline 保存復元
- follow state 管理
- shake state 管理
- tween 実行機

でもあります。

この構成だと、

- クラスが大きくなる
- 機能単位で差し替えづらい
- 「同じ target にぶら下がる別 player」との調停が player 外に逃げる

という問題が起きます。

### 2.2 競合解決が「停止」になっている

`TransformAnimationChannelExecutor` では、Preset / Follow 実行時に
同じ `TargetTransform` を使う他 player を止めています。

これは安全ではありますが、次ができません。

- Follow しながら Shake
- Follow の上に一時的な local additive 演出
- Preset 本体の移動に対して別系統の補正を重ねる

つまり「合成したい」要件と構造が逆向きです。

### 2.3 `TransformAnimationOutput` に合成規則がない

`TransformAnimationOutput` は、現状では各 property に対して

- active ref count
- 最後に書かれた値

だけを持っています。

この形では、

- additive
- override
- multiply
- priority
- layer

といった合成ルールを表現できません。

### 2.4 `DOTween` が直接 `Transform` を触っている

一番重要な点です。

現在の player は、output sink が無い場合に `DOMove` `DOLocalMove` `DOLocalRotate`
`DOScale` `DOAnchorPos` などを使っています。

これは `DOTween` が `Transform` / `RectTransform` へ直接値を書き込むため、
合成器を導入しても経路をすり抜けてしまいます。

つまり、**合成器を作るなら、Tween は Transform を直接触ってはいけません。**


## 3. 結論

### 3.1 合成器は作るべきか

**作るべきです。**

ただし、`TransformAnimationPlayer` の中に閉じ込めてはいけません。

理由:

- `TransformAnimationPlayer` はチャネル単位です
- 合成したい対象は target `Transform` 単位です
- 同じ target を複数 player が共有する以上、player 内合成器では全体調停できません

したがって、合成器は

- `TransformAnimationPlayer` の上位
- かつ `Transform` target 単位

に置く必要があります。

### 3.2 推奨する責務分離

責務は次の 4 層へ分けます。

1. Command / Channel 層
2. Target 合成層
3. Final Output 層
4. 物理 / Movement / Rotation 出力層

#### 1. Command / Channel 層

`TransformAnimationChannelPlayer` は thin facade にします。

役割:

- command を受ける
- channel tag を保持する
- 自分の target を引く
- target 用の合成器へ依頼する

ここで Follow や Shake の本体ロジックを持たせません。

#### 2. Target 合成層

target `Transform` ごとに 1 つだけ合成器を持ちます。

仮称:

- `TransformTargetDirector`
- `TransformPoseAccumulator`
- `TransformTrackRegistry`

この層が今回の中核です。

役割:

- その target に対して動作中の track を管理する
- track から pose 寄与を集める
- priority / layer / compose mode に従って合成する
- 最終 pose を `TransformController` へ流す

#### 3. Final Output 層

`TransformControllerService` は最終適用だけを担当します。

役割:

- `Transform`
- `RectTransform`
- `Rigidbody2D`
- `CharacterController`
- `BulkTransform`

への最終出力

ここに演出ロジックを増やさない方がよいです。

#### 4. 物理 / Movement / Rotation 出力層

現状の `MovementHub` / `RotateHub` と各 adapter は維持してよいです。

ただし、animation 側からは「直接止める」のではなく、

- base pose
- animation composed pose

を 1 回だけ統合して適用する形へ寄せるべきです。


## 4. 推奨アーキテクチャ

### 4.1 全体像

```text
CommandExecutor
  -> TransformAnimationChannelPlayer
    -> TransformAnimationTargetRegistry
      -> TransformTargetDirector (per target)
        -> TransformPresetTrack
        -> TransformFollowTrack
        -> TransformShakeTrack
        -> TransformScrollTrack
        -> TransformClipTrack
        -> TransformPoseAccumulator
          -> TransformAnimationOutput (composed pose バッファとして再利用 or 新バッファへ置換)
            -> TransformControllerService
              -> Transform / RectTransform / Rigidbody2D / BulkTransform
```

> **注記 (v0.4)**: 図中の `TransformAnimationOutput` は、移行期は既存クラスを composed pose バッファとして再利用します（9.3 節参照）。Phase 4 以降に内部を ref count 方式から composed pose 方式へ差し替えた時点で「実質的な新バッファ」になります。スラッシュ表記は廃止し、段階的な差し替えで対応します。

### 4.2 中核判断

#### 判断 A: 合成器は player 内ではなく target 単位

採用します。

#### 判断 B: `DOTween` は値だけを tween する

採用します。

`DOMove` 系は禁止し、`DOTween.To` で track 内の state 値だけを更新します。

その state を合成器が読む構造にします。

#### 判断 C: `TransformController` は最終適用器として残す

採用します。

`TransformController` はすでに出力先ごとの差分を吸収しているため、
ここを「演出システム本体」にすると再度肥大化します。

したがって、

- 合成は controller の手前
- 適用は controller

で責務を切ります。


## 5. `DOTween` 利用方針

### 5.1 禁止する使い方

合成器導入後は、track 内で次を直接使わない方針にします。

- `transform.DOMove`
- `transform.DOLocalMove`
- `transform.DOLocalRotate`
- `transform.DOScale`
- `rectTransform.DOAnchorPos`
- `rectTransform.DOSizeDelta`

理由:

- 直接 target を書き換える
- 合成器をバイパスする
- 別 track の寄与を消す
- `TransformController` の出力規則とも衝突する

### 5.2 許可する使い方

`DOTween.To` で、track 専用 state を tween します。

例:

- `Vector3 currentWorldTarget`
- `Vector3 currentLocalOffset`
- `Vector3 currentEulerOffset`
- `Vector3 currentScale`
- `Vector2 currentAnchored`

track はこの state を持ち、毎 frame それを `TransformPoseContribution` として返します。

### 5.3 重要な利点

この方針だと、

- Tween 実装はそのまま使える
- Ease や path 概念もそのまま流用できる
- 直接適用は 1 箇所へ集約できる
- Shake / Follow / Preset を同じルールで混ぜられる

### 5.4 Editor Play 後の Transform ドリフト対策

現行コードには、Editor で Play 終了後に Scene 上の初期位置へ戻らず、
Transform が徐々にずれていく現象への対策として、

- baseline snapshot の保存
- release 時の復元
- target 直接補正

が多く入っています。

ただし今回の再設計では、

- `DOTween` が `Transform` を直接触る構造自体を廃止する
- 最終適用点を controller へ一本化する
- 再生本体をほぼ作り直す

ため、**現行のドリフト回避コード群は引き継がず、全面削除対象** とします。

判断理由:

- 原因が `DOTween` 直接書き込み由来か、PlayMode 中の state 残留由来かを個別に追い続けるより、
  経路を単純化して再発余地を潰した方がよい
- 現行 workaround は player 肥大化の主要因になっている
- これから作る構造では同じ問題を別の責務分離で回避できる

注意:

- 新システム移行後に、Editor Play 終了時に scene state が残らないことを確認する専用の検証手順は必要
- ただしその確認は「workaround を維持する」ためではなく、「新構造で不要になっている」ことを確認するために行う


## 6. Track ベースの分離案

### 6.1 基本 interface

仮称:

- `ITransformModifierTrack`
- `ITransformTrackHandle`

`ITransformModifierTrack` の責務:

- `Tick(float deltaTime)`
- `bool IsAlive`
- `int Priority`
- `TransformContributionMask ContributedProperties`
- `void WriteContribution(ref TransformPoseAccumulator accumulator)`
- `void Stop()`
- `void Reset()`

> **`TransformContributionMask` 定義 (v0.4)**:
> `TransformContributionMask` は各 property の寄与有無を示す flags enum です。
> 既存の `TransformAnimationProperty` に相当しますが、合成器専用に改めて定義します。
>
> ```csharp
> [Flags]
> public enum TransformContributionMask
> {
>     None             = 0,
>     WorldPosition    = 1 << 0,
>     LocalPosition    = 1 << 1,
>     LocalRotation    = 1 << 2,
>     LocalScale       = 1 << 3,
>     AnchoredPosition = 1 << 4,
>     SizeDelta        = 1 << 5,
>     Pivot            = 1 << 6,
> }
> ```
>
> 既存の `TransformAnimationProperty` とビット割り当てを合わせているため、移行期に相互変換が不要です。

> **`WriteContribution` の `ref` について (v0.4)**:
> `TransformPoseAccumulator` は struct として設計します。
> `WriteContribution` は呼び出し側の accumulator インスタンスへ直接寄与を書き込みます。
> class にする場合は `ref` は不要になりますが、track が 1 frame に何度も呼ばれる箇所では struct + ref の方がヒープアロケーションを避けられます。

初期化やイベント購読が必要な service 化を行う場合は、

- `IScopeAcquireHandler`
- `IScopeReleaseHandler`

を使います。

### 6.2 Track の種類

#### `TransformPresetTrack`

役割:

- Preset / Clip / Step 再生
- path / ease / duration / loop の管理
- preset 実行ポリシーの適用
- `DynamicValue<T>` からの preset 解決

備考:

- `DOTween.To` で内部 state を更新する
- `Command` step はここから command runner を叩く
- world / local / anchored / scale / pivot の absolute 系寄与を出す
- `Position` 系 step は既存の `PathMode` 機能を維持する

### 6.3 Preset / Clip の再生命令ポリシー

Preset / Clip 再生中に別の preset が来たときの動きは、明示的に選べるようにします。

デフォルトは、**現在の再生を止めて新しい preset を再生する** です。

推奨 enum:

```csharp
public enum TransformPresetExecutionPolicy
{
    StopPrevious = 10,
    Parallel = 20,
    Interrupt = 30,
}
```

#### `StopPrevious = 10`

- デフォルト
- 現在動作中の preset / clip track を停止してから新規再生する
- もっとも安全で、初期移行にも向く

#### `Parallel = 20`

- 既存 preset track を残したまま新規 track を追加する
- 合成器が priority / compose mode に従って結果を解決する
- additive 系や別 property 系の共存用

#### `Interrupt = 30`

- 現在の再生を「中断」し、その時点の pose を基準に新規再生へ移る
- 実装上は `StopPrevious` と似るが、遷移時の初期値取得や blend 起点の扱いが異なる
- 途中 pose を利用したい演出向け

初期実装の推奨:

- まずは `StopPrevious` を確実に作る
- 次に `Parallel`
- 最後に `Interrupt`

の順に実装する

### 6.4 Preset / Clip 指定は `DynamicValue<T>` 対応にする

再生対象の preset / clip は固定参照だけでなく、
`DynamicValue<T>` で解決できるようにします。

最低要件:

- command 側で `DynamicValue<TransformAnimationPreset>` を受けられる
- runtime で解決された preset をそのまま track へ流せる

将来拡張:

- `DynamicValue<ITransformClipSource>` のような source wrapper を導入し、
  preset asset / runtime生成 clip / table参照 を同じ入口に統一する

判断:

- 今回のリファクタで「preset は inspector 固定参照のみ」という制約は外す
- 再生入力は必ず runtime 解決可能にする

### 6.5 `Position` 系の `PathMode` は維持する

既存の `Position` step が持つ path 機能は廃止しません。

維持対象:

- `Linear = 10`
- `Curve = 20`
- `Poly = 30`

維持方針:

- 新システムでも同等挙動を持つ
- path 計算式はなるべく既存実装を流用する
- 変えるのは「target に直接適用するか」ではなく、「どこへ値を書くか」だけにする

つまり、

- path evaluation は継続
- direct transform write は廃止

という整理にします。

### 6.6 Step の Editor 表示改善

現状の Step 一覧は `0` `1` `2` のような index がそのまま見出しになっており、
内容が一覧で判別しづらいです。

新システムでは、各 step は index ではなく
**その step の設定内容を要約した表示名** を持つようにします。

例:

- `Move World -> (-0.40, -2.35, 0.00) / Curve / 1.0s`
- `Rotate Local -> (0, 0, 90) / 0.25s`
- `Wait / 0.5s`
- `Command / 3 items`

推奨:

- runtime 用 field とは別に `EditorLabel` 相当の要約文字列を持つ
- Odin の list 要素ラベルや custom drawer でその文字列を見出しに使う
- index は補助情報に落とし、主表示にしない

判断:

- 「step 番号」は識別子ではなく並び順にすぎない
- 主表示は必ず step 内容を表す文字列にする

### 6.7 `SerializeReference` 化と Interface / Scene Preview の両立

方向性としては、step の mode 切り替えを enum ではなく
`SerializeReference` ベースの差し替えに寄せるのは賛成です。

理由:

- 将来 step 種類を増やしやすい
- operation ごとの field を分離できる
- `ShowIf` 依存を減らせる
- step 定義が mode ごとに独立する

ただし、**純粋な interface 1 本だけ** にすると Editor 側が弱くなります。

弱くなる点:

- 共通の要約表示を付けづらい
- Scene preview 用の共通 API が無いと描画側が分岐しにくい
- path を持つ step と持たない step の差が editor から見えにくい

そのため推奨は次です。

#### 推奨構成

- runtime の根本型は `ITransformClipStep`
- 実運用の serialize root は abstract base class `TransformClipStepBase`
- editor / scene preview 用に capability interface を分ける

例:

```csharp
public interface ITransformClipStep
{
    string EditorLabel { get; }
}

public interface ITransformScenePreviewProvider
{
    bool TryBuildPreviewPath(TransformPreviewBuildContext context, out TransformPreviewPath path);
}
```

この形なら、

- `SerializeReference` で step 種類を増やせる
- path を持つ step だけ preview interface を実装できる
- Scene 表示機能は `ITransformScenePreviewProvider` を見ればよい

となります。

結論:

- `SerializeReference` 化は進めてよい
- ただし preview 維持のため、editor capability interface は必須
- pure enum mode より拡張性は高い
- pure interface だけより、base class + capability interface の方が実務上安定する

### 6.8 `SerializeReference` 化時の必須要件

`SerializeReference` ベースへ移行する場合でも、**既存の Scene 上可視化と handle 編集体験は維持対象** とします。

維持必須の機能:

- path 軌道の Scene 上描画
- 終点 position handle による直接編集
- curve control point handle による直接編集
- local / world の差を考慮した preview
- `PathMode` ごとの差分表示

判断:

- `SerializeReference` 化は editor UX を劣化させてはいけない
- 「拡張性のために preview を捨てる」は不採用
- 新システムでも、現行 `TransformAnimationHubMB` と同等以上の確認性を持つこと

### 6.9 Editor / Scene Preview 実装方針

新システムで path preview と handle 編集を成立させるため、
step 実装は runtime interface だけでなく、editor 向け capability を持つ前提にします。

推奨:

- `ITransformClipStep`
- `TransformClipStepBase`
- `ITransformScenePreviewProvider`
- `ITransformSceneHandleEditable`

役割:

- `ITransformScenePreviewProvider`
  - preview 用 path 点列を返す
  - local / world を考慮した描画情報を返す
  - preview に必要な control point 情報を返す

- `ITransformSceneHandleEditable`
  - end point handle 操作結果を step へ反映する
  - control point handle 操作結果を step へ反映する
  - `PathMode` ごとの差分編集ルールを持つ

重要:

- editor 側は enum の分岐に依存しすぎない
- editor 側は `SerializedProperty` の深い内部パス文字列に依存しすぎない
- step 自身が「自分は preview 可能か」「どの handle を持つか」を示す

### 6.10 実装上の注意

`SerializeReference` 化すると、今のような

- `FindPropertyRelative("vector3")`
- `FindPropertyRelative("_source")`
- `FindPropertyRelative("curveControlOffset")`

のような固定パス前提の editor 実装は壊れやすくなります。

そのため新設計では、次を前提にします。

- editor は step の型能力を参照して描画する
- preview path 計算は step 側 API から取得する
- handle 編集時の値変換も step 側 API へ委譲する

つまり、

- path の描画責務は preview capability
- path の書き戻し責務は handle editable capability

として分離します。

### 6.11 仕様としての最終判断

この仕様書では、`SerializeReference` 化を許容します。

ただし前提条件として、次を必須要件にします。

1. 既存と同等以上の Scene preview を維持すること
2. 既存と同等以上の handle 編集を維持すること
3. `PathMode` ごとの差分が Scene 上で確認できること
4. editor 実装が特定 field 名の固定文字列に過度依存しないこと

この 4 条件を満たせない場合は、

- `SerializeReference` 化の導入方法を見直す
- base class と editor 専用 API を追加する

ことを優先し、preview 機能を削る方向では進めない

### 6.12 残りの Track 種類

#### `TransformFollowTrack`

役割:

- `TransformFollowService` のアルゴリズムを使い target 追従する

備考:

- 主に `WorldPosition` absolute contributor
- 必要なら follow offset だけ additive に分けてもよい

#### `TransformShakeTrack`

役割:

- ノイズによる位置 / 回転揺れ

備考:

- `LocalPosition` additive
- `LocalRotation` additive

Shake は additive が明確なので、最初に分離しやすいです。

#### `TransformScrollTrack`

役割:

- duration 中 velocity を積分して移動量を作る

備考:

- world / local のどちらで積むかを track 側 state で持つ


## 7. 合成器の設計

### 7.1 必要な理由

同じ `Transform` を複数機能で扱う場合、必要なのは
「誰が owner か」ではなく「最終 pose をどう決めるか」です。

そのため、合成器は次を持つ必要があります。

- property ごとの寄与スロット
- priority
- compose mode
- space
- 最終解決順

### 7.2 推奨データモデル

#### `TransformComposeMode`

```csharp
public enum TransformComposeMode
{
    Replace = 10,
    Add = 20,
    Multiply = 30,
}
```

#### `TransformContributionProperty`

```csharp
public enum TransformContributionProperty
{
    WorldPosition = 10,
    LocalPosition = 20,
    LocalRotation = 30,
    LocalScale = 40,
    AnchoredPosition = 50,
    SizeDelta = 60,
    Pivot = 70,
}
```

> **既存 `TransformAnimationProperty` との関係 (v0.4)**:
> 既存の `TransformAnimationProperty` は `[Flags]` enum (ビット演算用) で、現行の ref count 管理に使われています。
> `TransformContributionProperty` は合成器内で「各寄与スロットの種別を識別する」ための enum であり、flags ではありません。
> 両者は役割が異なります。
>
> - `TransformAnimationProperty` → ref count 管理・IsActive 判定（既存コードで使用中、段階的に廃止予定）
> - `TransformContributionProperty` → 合成器内 `TransformPoseContribution` の種別識別（新規）
> - `TransformContributionMask` → track が「どの property に寄与するか」を示す flags（新規、6.1 節参照）
>
> 移行完了後は `TransformAnimationProperty` を廃止し、`TransformContributionMask` へ一本化します。

#### `TransformPoseContribution`

持つべき情報:

- `TransformContributionProperty Property`
- `TransformComposeMode ComposeMode`
- `int Priority`
- `int Layer`
- `Vector4 Value`
- `bool IsValid`

`Vector4` なのは汎用化のためです。  
実装時に読みづらければ `Vector3` / `Vector2` 別 struct に分けてもよいです。

### 7.3 合成ルール

初期版は複雑にしすぎず、次で十分です。

#### Position 系

- `WorldPosition.Replace` は最優先の absolute 位置
- `LocalPosition.Replace` は world が無い場合に採用
- `LocalPosition.Add` は local offset として加算

#### Rotation 系

- `LocalRotation.Replace` を基準
- `LocalRotation.Add` をその上に加算

#### Scale 系

- `LocalScale.Replace` を基準
- `LocalScale.Multiply` を乗算補正

#### UI 系

- `Pivot` と `AnchoredPosition` は密結合で扱う
- `Pivot` 単独変更でも anchored 補正込みで 1 セットとして適用する

### 7.4 同時採用しない方がよいもの

次は同 frame に両方 active でも、同時適用しない方が安全です。

- `WorldPosition.Replace`
- `LocalPosition.Replace`

理由:

- 両方 absolute で意味が競合する
- parent 有無で結果が変わる

この場合は、

- priority の高い方を採用
- もう片方は無視

の規則で十分です。


## 8. `TransformAnimationPlayer` の位置づけ

### 8.1 player に残すもの

- channel tag
- target 解決
- command 受付
- target director へのルーティング
- telemetry 表示用の窓口

### 8.2 player から外すもの

- shake 実体
- follow 実体
- preset 実体
- output ref count 管理
- baseline snapshot
- tween と target の直接結合

### 8.3 判断

`TransformAnimationPlayer` に合成器を内包する案は不採用とします。

理由:

- player は channel 単位で増える
- 合成したい単位は target 単位
- player 内合成器では cross-channel の統合点になれない


## 9. `TransformController` との役割分担

### 9.1 残すべき責務

`TransformControllerService` に残すべきもの:

- 出力 target 判定
- `Transform` / `RectTransform` / `Rigidbody2D` / `CharacterController` / `BulkTransform` への適用
- movement / rotation adapter との接続
- teleport / final apply

### 9.2 減らすべき責務

将来的に減らしたいもの:

- animation 独自の property 調停
- output ref count に依存した override 判定

理想形は、

- controller は「合成済み出力」を受けて apply
- animation 側の複雑さは controller の外に出す

です。

### 9.3 注意点

ただし controller から animation 出力点を完全に消す必要はありません。

段階的移行では、

- まず output sink は controller に残す
- 中身だけ ref count 方式から composed pose 方式へ差し替える

の方が安全です。


## 10. 推奨サービス構成

### 10.1 新規候補

- `ITransformAnimationTargetRegistry`
- `TransformAnimationTargetRegistryService`
- `ITransformTargetDirector`
- `TransformTargetDirector`
- `ITransformModifierTrack`
- `TransformPresetTrack`
- `TransformFollowTrack`
- `TransformShakeTrack`
- `TransformScrollTrack`
- `TransformPoseAccumulator`

### 10.2 既存クラスの役割変更

#### `TransformAnimationHubService`

現在:

- channel def から player を作るだけ

変更後:

- player 群の管理
- target registry の利用
- 必要なら telemetry 集約

#### `TransformAnimationChannelPlayer`

現在:

- 再生本体

変更後:

- command facade / route handler
- preset policy と target director への再生命令窓口

#### `TransformAnimationOutput`

現在:

- property active ref count + 値

変更後:

- composed pose snapshot
- もしくは accumulator 適用結果を保持する出力バッファ

> **既存 `Clear()` 挙動の問題点 (v0.4)**:
> 現行の `TransformAnimationOutput.Clear()` は ref count（`_worldPositionRefs` 等）を 0 にリセットしますが、
> 実際の値フィールド（`_worldPosition`, `_localPosition`, `_localEulerAngles`, `_localScale`, 等）はリセットしません。
>
> 特に `_localScale` は `Vector3.one` で初期化されていますが、`Clear()` 後も `_localScale` の値は変わらず残ります。
> IsActive が false になっても値フィールドには前回の値が残るため、意図しない読み出しが起きやすいです。
>
> 新設計での対処方針:
> - `TransformAnimationOutput` を composed pose バッファとして再設計する際に、`Clear()` は値フィールドも含めて完全にリセットするよう修正します。
> - `_localScale` 相当の初期値は `Vector3.one` のまま維持し、`Clear()` で明示的に `Vector3.one` へ戻します。
> - ref count 方式自体は composed pose 方式に置き換えるため、最終的には ref count フィールドは削除します。

#### `ITransformAnimationOutputSink` / `ITransformAnimationOutputRegistry`

現在:

- `ITransformAnimationOutputSink`: Transform + output を対にする sink インタフェース
- `ITransformAnimationOutputRegistry`: Transform をキーに sink を登録・解決するレジストリ
- `TransformAnimationOutputRegistryService`: Dictionary 実装

変更後:

- `ITransformAnimationOutputRegistry` の役割は `ITransformAnimationTargetRegistry` に統合・代替する方向で検討します。
- 移行期は `ITransformAnimationOutputSink` を経由した output 書き込みを維持し、内部だけを composed pose 方式へ差し替えます（9.3 節参照）。
- Phase 5 完了時に `ITransformAnimationOutputSink` / `ITransformAnimationOutputRegistry` / `TransformAnimationOutputRegistryService` の廃止可否を改めて判断します。

#### 現行の baseline / restore / drift workaround 群

現在:

- Editor Play 終了後の drift 回避のため player 内に多く持っている

変更後:

- 新システムへ引き継がない
- 全面削除前提で再設計する


## 11. 実装マイルストーン

### Phase 1: 合成器の骨組みだけ追加

目的:

- target registry
- target director
- accumulator

を追加する

この段階では、既存 player をまだ残してよいです。

完了条件:

- target ごとに director を解決できる
- player から director を経由して output へ値を書ける
- 既存機能を壊さずに併用できる

### Phase 2: Shake を最初に分離

理由:

- additive で単純
- `Follow` や `Preset` と共存させやすい

完了条件:

- `Shake` が独立 track になる
- `StopConflictingPlayers` に頼らず共存できる

### Phase 3: Follow を分離

理由:

- 位置 absolute 寄与として扱いやすい
- 既存 `TransformFollowService` を再利用しやすい

完了条件:

- follow target の切り替え
- snap
- velocity offset

が track 化される

### Phase 4: Preset / Clip を分離

この段階で、`DOMove` 系の直接 transform 書き換えを完全廃止します。

完了条件:

- world / local / anchored / scale / pivot の tween が state tween 化される
- preset と follow / shake が合成で共存できる
- preset execution policy のうち `StopPrevious` が最低限成立する
- `PathMode` が既存同等で使える
- step 一覧が内容ベースの editor 表示へ変わる

### Phase 5: 競合停止ロジック削除

完了条件:

- `StopConflictingPlayers` を削除できる
- 競合は停止ではなく合成規則で解決する


## 12. 最終判断

今回のケースでは、**合成器は必要です。**

ただし置き場所は `TransformAnimationPlayer` 内ではありません。

正しい分け方は次です。

- `TransformAnimationPlayer`: command / channel の窓口
- `TransformTargetDirector`: 同一 target の合成責務
- `TransformController`: 最終適用責務

そして `DOTween` は、

- `Transform` を直接動かす用途ではなく
- track state を動かす用途

へ切り替えるべきです。

この構成にすると、

- Component は増やさない
- 機能ごとには分離できる
- 同一 target への複数命令を合成できる
- `TransformController` の肥大化も抑えられる

という 4 つを同時に満たせます。

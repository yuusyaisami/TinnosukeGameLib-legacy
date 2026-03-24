<!--
Spec Version: v1.3
Status: Draft
Updated: 2026-03-21
Change Summary:
- v1.0 初版。
- TraitHolder を起点にした TraitRuntime Placement / WorldPointer / Selectable / UserMoveRotate の統合仕様を新規作成。
- UpgradeSystem は依存点のみ記載し、詳細仕様から除外。
- コード変更は含まない。現行コードを読んだ上で作成。
- v1.1 で MB と service の責務境界を修正。
- MB は authoring / bridge 専用、主要ロジックは service 側で処理する方針を明記。
- v1.2 で UserMoveRotateRuntime の Editor mode lifecycle を追記。
- SimpleMode 配置直後の自動補正、Editor mode 中の左/右 click 解除、SelectManager global 無効時の自動解除、Enter/Exit command 実行を追加。
- v1.3 で TraitPlacementService の配置方針と UITraitList 連携を追記。
- TraitPlacementService は TraitHolderHubService と同一 LTS に配置し、UITraitList は trait ごとの visible / hidden 状態を参照して表示制御する方針を追加。

Primary References Read:
- Assets/GameLib/Script/Common/Trait/Service/TraitHolderService.cs
- Assets/GameLib/Script/Common/Trait/Service/TraitHolderService.Vars.cs
- Assets/GameLib/Script/Common/Trait/Hub/TraitHolderHubService.cs
- Assets/GameLib/Script/Common/Trait/Hub/TraitHolderSettings.cs
- Assets/GameLib/Script/Common/Trait/SO/TraitDefinitionSO.cs
- Assets/GameLib/Script/Common/Commands/VNext/Executors/Trait/WriteTraitDataExecutor.cs
- Assets/GameLib/Script/Common/Trait/Equip/EquipTraitTypes.cs
- Assets/GameLib/Script/Common/Trait/Equip/EquipTraitSlotRuntime.cs
- Assets/GameLib/Script/Common/Commands/VNext/Commands/Trait/EquipTraitCommandData.cs
- Assets/GameLib/Script/Common/Spawner/SpawnerCore.cs
- Assets/GameLib/Script/Common/Spawner/SpawnParams.cs
- Assets/GameLib/Script/Common/Spawner/SceneSpawnerResolver.cs
- Assets/GameLib/Script/Common/DI/Runtime/BaseRuntimeTemplateSO.cs
- Assets/GameLib/Script/Common/DI/Runtime/RuntimeIdentityData.cs
- Assets/GameLib/Script/Project/Scene/Runtime/RuntimeManager/RuntimeManagerMB.cs
- Assets/GameLib/Script/Project/System/Inputs/InputMiddle/InputRouter.cs
- Assets/GameLib/Script/Project/System/Inputs/Struct/InputFrame.cs
- Assets/GameLib/Script/Project/System/Inputs/InputService/IInputService.cs
- Assets/GameLib/Script/Project/System/Inputs/InputService/PointerService.cs
- Assets/GameLib/Script/Project/Scene/Channels/Area/AreaChannelTypes.cs
- Assets/GameLib/Script/Project/Scene/Channels/Area/AreaChannelHubService.cs
- Assets/GameLib/Script/Common/Variables/Dynamic/Sources/AreaChannelPositionSources.cs
- Assets/GameLib/Script/Project/UI/Core/Selection/UISelectionService.cs
- Assets/GameLib/Script/Project/UI/Core/UIInput/UIInputService.cs
- Assets/GameLib/Script/Project/UI/TraitList/Services/UITraitListVisualizerService.cs
-->

# Trait Runtime Placement & Selectable System Spec v1

## 1. 結論

本機能は 1 つの巨大システムとして作らない。

以下の 4 層に分割して設計する。

1. `WorldPointerRuntimeSystem`
2. `SelectableRuntimeSystem`
3. `UserMoveRotateRuntimeSystem`
4. `TraitRuntimePlacementSystem`

この順で土台から積み上げる。

理由:

- クリック / ホバー / 長押しは Upgrade を含む将来機能の共通基盤だから
- Selectable は Placement に依存しないが、Placement 後の Runtime がそのまま乗れる形にすべきだから
- Trait と Runtime の 1:1 紐付けは Holder 起点の責務であり、Input 系と混ぜるべきではないから

また、現行コードを前提にすると以下は必須の設計判断になる。

1. `InputFrame.Click` だけでは左クリック / 右クリックを区別できないため、Input 基盤は破壊的に拡張する。
2. `AreaChannel` は現状「位置サンプル」しかできず「範囲内判定」ができないため、Contains 系 API を追加する。
3. `TraitHolderService` には要素ごとの明示キーがまだ無いため、v1 の `TraitKey` は `ITraitInstance.InstanceId` を採用する。
4. Trait と Runtime の実体化マッピングは `TraitDefinitionSO` 側に持たせ、リンク状態は PlacementService 側に持たせる。

Upgrade 機能は本書では作らない。
ただし Upgrade が必要とする「左クリック短押し」「ホバー」「対象特定」は本仕様の中で必ず成立させる。

---

## 2. スコープ

本仕様に含むもの:

- Trait を RuntimeLTS として実体化する `SimpleMode`
- 実体化された Runtime と TraitHolder 要素の 1:1 リンク
- `RuntimeTraitMB` によるリンク情報保持
- Trait データの Blackboard 書き込み
- 非 UI の world object に対する Hover / Click / LongPress 基盤
- `SelectRuntimeManager` / `SelectableRuntimeMB`
- `UserMoveRotateRuntime` の移動 / 回転 / 設置可否判定
- `SelectRuntimeManagerMB` の `DynamicValue<bool>` 有効フラグ
- 将来の Upgrade / 右クリック hide のための拡張ポイント

本仕様に含まないもの:

- Upgrade 選択 UI の具体的な見た目
- Upgrade 候補の生成ルール
- 右クリック hide 実行の最終仕様
- セーブ / ロード
- 複数選択
- ネットワーク同期
- 2D Collider ベースの別系統実装

---

## 3. 現行コードから見た前提と不足

### 3.1 TraitHolder 側

現行の `TraitHolderService` / `TraitHolderHubService` は以下を提供している。

- `HolderKey` で holder を引く
- `ITraitInstance` の配列を持つ
- `ITraitInstance.InstanceId` と `Definition.DefinitionId` を持つ
- `OnTraitsChanged` を購読できる
- Holder 基本情報を Blackboard に書ける

不足しているもの:

- Holder 内の Trait 要素を安定参照するための明示的 `TraitKey`
- Trait 要素と RuntimeLTS のリンク管理
- Trait 削除時の linked runtime 自動解除

### 3.2 Trait データ書き込み

現行の `WriteTraitDataExecutor` は、
`TraitDefinitionSO` から `TraitInstanceContext` を新規生成し、
その vars を Blackboard に merge している。

これは「Trait の定義データを展開して書く」動作であり、
「Holder 内の既存 instance の差分状態を丸ごと写す」動作ではない。

本仕様では、`RuntimeTraitMB` の Blackboard 書き込みはまずこの既存意味論に合わせる。

つまり v1 は:

- `TraitDefinitionSO` ベースの標準 trait vars を書く
- 追加で link vars を書く

で統一する。

### 3.3 Spawn 基盤

現行の Runtime spawn 基盤はすでにある。

- `ISceneSpawnerRegistry`
- `IAsyncSpawnerService`
- `RuntimeLifetimeScopeSpawnerService`
- `SpawnParams.ForRuntime(...)`

したがって Placement の `SimpleMode` は新しい spawn 基盤を作らず、既存 Runtime spawner に乗せる。

### 3.4 Input 基盤

現行 `InputRouter` / `InputFrame` は world selectable 基盤としては不足がある。

現状:

- `PointerScreen`
- `Move`
- `Scroll`
- `Click`

不足:

- 左クリック / 右クリックの分離
- world object 用の長押し状態管理
- world hit test の統一窓口

このため、world selectable 系は `UIInputService` を流用せず、別系統の `WorldPointerRuntimeSystem` として作る。

### 3.5 AreaChannel

現行 `AreaChannel` は `TrySamplePosition(...)` による位置サンプルはあるが、
「候補位置が area 内か」を問う API が無い。

Move / placement validation に必要なのは sample ではなく contains である。

したがって v1 で以下を追加する。

- `IAreaShape.ContainsLocal(...)`
- `IAreaChannelPlayer.ContainsPosition(...)`
- `IAreaChannelHubService.ContainsPosition(...)`

---

## 4. 設計方針

### 4.1 再利用優先

入力判定、選択、移動編集、Trait placement を同一クラスに押し込めない。

再利用単位は以下に固定する。

- world pointer 判定
- manager を持つ single-select
- selected object に対する transform 編集
- trait と runtime のリンク付き spawn

### 4.2 Holder 起点のリンク管理

TraitRuntimePlacement は「TraitDefinition を spawn する」だけでは不十分である。
重要なのは、spawn 後の RuntimeLTS が Holder 内の 1 要素と 1:1 で追跡されること。

そのため PlacementService は以下の双方向 index を持つ。

- `TraitLinkKey -> RuntimeLifetimeScope`
- `RuntimeLifetimeScope -> TraitLinkData`

### 4.3 Transform 参照の直接持ちは避ける

外部 scope の依存は `ActorSource` で解決する。

この方針を以下に適用する。

- Placement 対象 holder
- AreaChannel の参照元
- 将来の upgrade 実行元

### 4.4 既存参照の維持は目的にしない

本仕様は基盤の再定義である。

互換性のために以下を温存しない。

- `InputFrame.Click` の単一クリック前提
- ad-hoc な world click 実装
- Trait 指定ロジックの重複

必要なら破壊的に置き換える。

### 4.5 MB と Service の責務境界

本プロジェクトでは、MB は基本的に service に渡すための設定フィールドと lifecycle の橋渡しに留める。

したがって本仕様でも以下を原則とする。

- MB は authoring data を保持する
- MB は `OnEnable` / `OnDisable` などで service に登録解除を依頼する
- 主な状態遷移、判定、コマンド実行、blackboard 更新、link 管理は service 側で行う
- MB 自身は主要な業務ロジックを持たない

例外として MB に許容するのは、薄い bridge として必要な最小限の処理だけである。

---

## 5. システム全体像

```text
InputSystem
  -> InputRouter / InputFrame
      -> WorldPointerRuntimeService
          -> SelectRuntimeManagerService
              -> SelectableRuntimeMB
              -> UserMoveRotateRuntimeService
                  -> UserMoveRotateRuntimeMB

TraitHolderHubService
  -> TraitPlacementService
      -> RuntimeLifetimeScopeSpawnerService
          -> RuntimeLTS
              -> RuntimeTraitMB
              -> SelectableRuntimeMB
              -> UserMoveRotateRuntimeMB
```

責務分離:

- `WorldPointerRuntimeService`
  world object の hover / click / long press 判定だけを担当する

- `SelectRuntimeManagerService`
  manager 配下の selectable の current / hovered を持つ

- `UserMoveRotateRuntimeService`
  selection 上に乗る編集モードを担当する

- `TraitPlacementService`
  holder element と runtime のリンク付き spawn / despawn を担当する

---

## 6. 主要な新規型

| 型 | 役割 |
| --- | --- |
| `WorldPointerRuntimeService` | 非 UI world object 向け hover / click / long press 基盤 |
| `WorldPointerTargetMB` | world hit target 用の設定と collider 参照を持つ authoring MB |
| `SelectRuntimeManagerService` | manager 単位の hover / selection 管理 |
| `SelectRuntimeManagerMB` | manager 設定を service へ渡す authoring MB |
| `SelectableRuntimeMB` | selectable 設定と service 登録用 bridge を持つ MB |
| `UserMoveRotateRuntimeService` | 編集モードの session 管理 |
| `UserMoveRotateRuntimeMB` | move / rotate / validation 設定を持つ authoring MB |
| `TraitPlacementService` | Trait と RuntimeLTS のリンク付き spawn / despawn |
| `RuntimeTraitMB` | runtime link 情報を保持し service に渡す薄い bridge MB |
| `PlaceableTraitSettings` | TraitDefinitionSO にぶら下がる placeable runtime 設定 |
| `TraitElementSelector` | Holder 内の対象 Trait 要素の指定を共通化する |

`CustomInputTransformMB` は仮名ではなく、本仕様では `UserMoveRotateRuntimeMB` に統一する。

---

## 7. 共通データモデル

### 7.1 Trait 要素指定の共通化

重複 Trait を扱う都合上、`TraitDefinitionSO` 指定だけでは不十分である。

そのため Holder 内の対象指定は shared selector に統一する。

```csharp
public enum TraitElementSelectorKind
{
    ByTraitKey = 10,
    ByDefinition = 20,
    ByDefinitionId = 30,
    ByIndex = 40,
    First = 50,
    Last = 60,
}
```

`TraitElementSelector` が持つ代表フィールド:

- `Kind`
- `string TraitKey`
- `DynamicValue<TraitDefinitionSO> Definition`
- `string DefinitionId`
- `DynamicValue<int> Index`

方針:

- Placement / Upgrade / Hide / future commands はこれを共通利用する
- `EquipTraitTargetKind` は将来的にこれへ寄せてよい
- 互換レイヤーは作らなくてよい

### 7.2 v1 の TraitKey

現行 Holder に明示的 key が無いため、v1 では以下を `TraitKey` として採用する。

- `ITraitInstance.InstanceId`

理由:

- index は順番変更に弱い
- definitionId は重複 Trait を区別できない
- `InstanceId` は既存実装ですでに存在する

### 7.3 Runtime link key

PlacementService が内部で持つ一意キーは以下とする。

```csharp
public readonly struct TraitRuntimeLinkKey
{
    public readonly LifetimeScopeKind SourceScopeKind;
    public readonly string SourceScopeId;
    public readonly string HolderKey;
    public readonly string TraitKey;
}
```

`SourceScopeId` は linked holder を保有する LTS の identity id。

### 7.4 RuntimeTrait link data

```csharp
public sealed class TraitRuntimeLinkData
{
    public LifetimeScopeKind SourceScopeKind;
    public string SourceScopeId = string.Empty;
    public string SourceScopeCategory = string.Empty;
    public string HolderKey = string.Empty;
    public string TraitKey = string.Empty;
    public string TraitDefinitionId = string.Empty;
}
```

用途:

- `RuntimeTraitMB` に保持
- runtime blackboard へ書き込み
- PlacementService の reverse lookup

### 7.5 Trait 実体化の表示状態

UITraitList 連携では、単純な「実体化済み / 未実体化」では足りない。

理由:

- runtime が存在していても hidden 状態なら、UITraitList 上では再表示したいから
- hidden は delete ではなく「遠くへ飛ばして非表示」に近く、runtime 内データは保持したいから

そのため Placement 側の公開状態は以下の 3 値にする。

```csharp
public enum TraitRuntimePresentationState
{
    None = 10,
    Visible = 20,
    Hidden = 30,
}
```

意味:

- `None`
  まだ runtime が存在しない

- `Visible`
  runtime は存在し、現在ゲーム内で表示対象である

- `Hidden`
  runtime は存在するが、現在は非表示扱いでデータも保持されている

---

## 8. Trait 側の placeable 定義

Trait=Object のマッピングは `TraitDefinitionSO` 側に置く。

理由:

- どの runtime を実体化するかは Trait 定義の責務だから
- PlacementService は「何を spawn するか」ではなく「誰と誰を結ぶか」を担当すべきだから

### 8.1 `PlaceableTraitSettings`

`TraitDefinitionSO` に以下の serializable class を追加する。

- `bool Enabled`
- `DynamicValue<BaseRuntimeTemplatePreset> RuntimeTemplate`
- `bool ApplyRuntimeTraitMB = true`
- `TraitRuntimePlacementMode DefaultPlacementMode = Simple`

```csharp
public enum TraitRuntimePlacementMode
{
    Simple = 10,
}
```

v1 では `Simple` のみ実装する。

将来のモード例:

- GridSnap = 20
- SocketAttach = 30
- PreviewThenCommit = 40

ただし本書では定義だけに留め、実装対象は `Simple` のみ。

---

## 9. WorldPointerRuntimeSystem

### 9.1 目的

非 UI オブジェクトに対する以下の共通判定を提供する。

- Hover Enter / Exit
- Left Click
- Right Click
- Left LongPress Start / End
- Right LongPress Start / End

この層は selection を知らない。
selection / upgrade / hide など上位システムが利用する共通基盤である。

### 9.2 Input 基盤の破壊的変更

`InputFrame` は v1 で以下の形に変更する。

```csharp
public struct InputFrame
{
    public ButtonState PointerLeft;
    public ButtonState PointerRight;
    public Vector2 PointerScreen;
    public Vector2 Move;
    public Vector2 Scroll;
}
```

方針:

- 既存 `Click` 単独フィールドは廃止してよい
- world / UI の両方で左右区別が必要になった時点で単一 `Click` は足かせになる
- 長押し状態そのものは `InputFrame` に持たせず、consumer 側で時間管理する

### 9.3 WorldPointer target

raycast hit から対象を引く単位として `WorldPointerTargetMB` を置く。

`WorldPointerTargetMB` 自体は主処理を持たず、service が hit 解決に使う設定と参照だけを持つ。

MB が持つもの:

- world pointer 判定対象であることを示す
- 自身を代表する collider 群を持つ
- target 分類や判定補助に必要な設定

service 側が持つもの:

- hit collider から target owner を逆引きする処理
- hover / click / long press の状態管理

MB が持たない責務:

- selection state
- upgrade logic
- transform edit logic

### 9.4 WorldPointer service の責務

`WorldPointerRuntimeService` は以下を担当する。

1. `IInputConsumer` として `InputRouter` から入力を受ける
2. `PointerScreen` と camera から world ray を計算する
3. hit collider から `WorldPointerTargetMB` を引く
4. hover / down / held / up / click / long press の状態遷移を管理する
5. 上位へ event を通知する

### 9.5 Camera と hit test

v1 は 3D `Collider` を対象にする。

camera の取得は scene 側の world pointer installer から行う。
`Camera.main` fallback は許容するが、authoring では明示指定を基本にする。

raycast の初期要件:

- `LayerMask HitMask`
- `bool QueryTriggerInteraction`
- target 解決は hit collider から親方向に `WorldPointerTargetMB` を辿る

### 9.6 長押しの状態機械

左ボタンについての状態機械:

1. `Down` 時に hover target があれば `PressedTarget` と `PressedTime` を記録
2. その target 上で保持が続き、`HoldSeconds >= Threshold` で `LongPressStart`
3. long press 中は release まで click を出さない
4. release 時:
   - long press 未到達なら `Click`
   - long press 到達済みなら `LongPressEnd`
5. 途中で hover target が外れた、block された、manager が無効化された場合は cancel

右ボタンも同様だが、v1 では主に将来拡張用とする。

### 9.7 event 形式

`WorldPointerRuntimeService` は少なくとも以下を publish する。

- `OnHoveredChanged`
- `OnLeftClicked`
- `OnRightClicked`
- `OnLeftLongPressStarted`
- `OnLeftLongPressEnded`
- `OnRightLongPressStarted`
- `OnRightLongPressEnded`

event payload には以下を含める。

- `WorldPointerTargetMB Target`
- `Vector2 ScreenPosition`
- `Vector3 WorldPosition`
- `Vector3 HitNormal`
- `Collider HitCollider`

---

## 10. SelectableRuntimeSystem

### 10.1 目的

UI と独立した world selection を manager 単位で持つ。

仕様:

- single selection
- hovered と selected を分離
- `SelectableRuntimeMB` は自身の設定を持ち、lifecycle 時に service へ登録解除を依頼する

### 10.2 `SelectRuntimeManagerMB`

manager 設定を service に渡す authoring MB として以下を持つ。

- `DynamicValue<bool> IsEnabled`

`IsEnabled` は単なる表示用ではなく、
world selectable 系全体に対する global な選択可能フラグとして扱う。

したがって `SelectRuntimeManagerService` と `UserMoveRotateRuntimeService` は、
この値を effective enabled state として監視する。

必要であれば将来追加してよいもの:

- empty click で clear するか
- disable 時に clear するか

ただし v1 の既定動作は固定する。

v1 既定動作:

- `IsEnabled == false`
  - hover を clear
  - current selection を clear
  - active edit mode を cancel
  - 新規 pointer event を受け付けない

### 10.3 `SelectableRuntimeMB`

`SelectableRuntimeMB` は主要処理を持たず、以下の設定と bridge だけを持つ。

- `CommandListData OnSelectedCommands`
- `CommandListData OnDeselectedCommands`
- `WorldPointerTargetMB` 参照または同居前提

MB の lifecycle bridge ルール:

1. `OnEnable` 時に親 transform を辿る
2. 最初に見つかった `SelectRuntimeManagerMB` に対応する service へ登録を依頼
3. `OnDisable` / `OnDestroy` / `OnTransformParentChanged` で service へ再登録または解除を依頼

### 10.4 manager の責務

`SelectRuntimeManagerService` は以下を担当する。

- 配下 selectable 一覧の保持
- hovered selectable の更新
- selected selectable の更新
- select / deselect command 実行トリガ
- future upgrade / move-rotate への中継

### 10.5 選択ルール

v1 の selection ルール:

- hover target が manager 配下の selectable なら hovered 更新
- 左短押し click:
  - target が selectable なら selected にする
  - empty click なら selection を clear
- すでに選択中の target を再 click しても再 select command は流さない
- 選択切替時は old を deselect してから new を select する

### 10.6 command 実行コンテキスト

`SelectableRuntimeMB` に設定された command は、
`SelectRuntimeManagerService` が selectable 自身の runtime scope を actor にして実行する。

vars は最低限以下を含める。

- runtime 自身の blackboard vars
- `selected = true/false`
- `hovered = true/false`

ただし hover command 自体は本仕様ではまだ必須にしない。
hover は service event として公開し、command 化は必要になった時に追加する。

### 10.7 将来機能への公開 API

manager は future system のために以下を公開する。

- `Current`
- `Hovered`
- `OnSelectionChanged`
- `OnHoveredChanged`
- `OnLeftClickSelectable`
- `OnRightClickSelectable`
- `OnLeftLongPressSelectable`

UpgradeSystem は `OnLeftClickSelectable` を利用する。
UserMoveRotateSystem は `OnLeftLongPressSelectable` を利用する。

---

## 11. UserMoveRotateRuntimeSystem

### 11.1 目的

選択済み runtime を user input で移動 / 回転できるようにする。

本システムは selection の上位に乗る。
未選択 object は動かせない。

### 11.2 構成

- `UserMoveRotateRuntimeService`
- `UserMoveRotateRuntimeMB`

`UserMoveRotateRuntimeMB` は authoring data のみを持ち、
実際の editing session 管理、制約判定、入力解釈は service が行う。

`UserMoveRotateRuntimeMB` が service に渡す主要設定には、
少なくとも以下を含める。

- move / rotate / validation 設定
- `CommandListData OnEditorEnterCommands`
- `CommandListData OnEditorExitCommands`

### 11.3 起動条件

v1 の edit mode 開始条件:

- manager が有効
- target が selected または long press 対象である
- target に `UserMoveRotateRuntimeMB` が付いている
- 左 long press が発生した

開始時の挙動:

1. target を selected にする
2. edit session を開始する
3. 以後、pointer move / move input / scroll をその target に適用する

### 11.4 edit mode の継続

v1 では long press は「開始トリガ」であり、保持継続条件ではない。

つまり:

- long press 開始後は edit mode に入る
- その後は left button を離しても mode は維持できる

終了条件:

- selection が外れる
- manager が無効化される
- 親 `SelectRuntimeManager` の `IsEnabled` が false になり、global に select 不可になった
- target が destroy / unregister される
- 明示 cancel が呼ばれる
- Editor mode 開始後に `LeftClick` が発生した
- Editor mode 開始後に `RightClick` が発生した

click による解除ルール:

- Editor mode に入った後の click だけを解除条件にする
- 解除判定は hit 対象を問わない
- 特に `RightClick` は空間上のどこで押されても解除として扱う
- 開始に使った long press と同一入力サイクルで即解除しない

session lifecycle command:

- Editor mode 開始時に `OnEditorEnterCommands` を 1 回だけ実行する
- Editor mode 解除時に `OnEditorExitCommands` を 1 回だけ実行する
- 解除理由が click、manager 無効化、selection 喪失、destroy のどれでも `OnEditorExitCommands` は流す

### 11.5 入力ソース

Move は以下 3 モードを持つ。

```csharp
public enum UserMoveSourceMode
{
    PointerFollow = 10,
    InputMove = 20,
    Hybrid = 30,
}
```

v1 の既定は `Hybrid`。

意味:

- `PointerFollow`
  pointer の world 投影位置に追従

- `InputMove`
  `InputFrame.Move` を速度換算して移動

- `Hybrid`
  pointer 有効時は pointer 優先、それ以外は move input

Rotate は `InputFrame.Scroll.y` を使用し、area plane の法線を軸に回す。

### 11.6 移動制約

`UserMoveRotateRuntimeMB` は以下の制約設定を service に渡す。

- `ActorSource AreaActorSource`
- `string AreaTag`
- `float MinDistanceToOtherSelectable`
- `LayerMask BlockLayerMask`
- `List<Collider> ValidationColliders`
- `UserMoveSourceMode MoveSourceMode`
- `CommandListData OnEditorEnterCommands`
- `CommandListData OnEditorExitCommands`

### 11.7 Area 制約

Area は多くの場合、外部 LTS の `AreaChannelHubService` にあるため、
参照は `ActorSource` で解決する。

必要 API:

- `IAreaChannelHubService.TryGetPlayer(tag, out player)`
- `IAreaChannelPlayer.ContainsPosition(basePosition, worldPosition, request)`

判定は area plane に従う。

これにより:

- `XY` のゲームでも
- `XZ` のゲームでも

同じ move system を流用できる。

### 11.8 他 selectable との距離制約

manager が保持している登録 selectable 一覧を使い、
candidate 位置と他 selectable の位置を比較する。

ルール:

- self は除外
- 非 active / unregister 済み対象は除外
- 判定距離は area plane 上の 2D 距離で見る

### 11.9 Collider 制約

block layer 判定は `ValidationColliders` を用いる。

v1 方針:

- runtime 自身の validation collider 群と
- `BlockLayerMask` 対象 collider が
- candidate pose で重なっていないこと

判定に失敗した場合:

- candidate pose は commit しない
- object は直前の valid pose に留まる

つまり「動かせなかったらその場で止まる」を採用する。

### 11.10 candidate 評価順

1. input から candidate pose を作る
2. area contains
3. min distance
4. collider overlap
5. 全通過なら commit
6. どれか失敗なら previous valid を維持

この順に固定する。

理由:

- area 判定が最も安い
- distance は manager 局所情報だけで見られる
- collider 判定は最も重い

---

## 12. TraitRuntimePlacementSystem

### 12.1 目的

TraitHolder 内の 1 要素と RuntimeLTS を 1:1 で結びつけたまま spawn / despawn する。

### 12.2 主要サービス

`TraitPlacementService` は以下を担当する。

- `TraitHolderHubService` と同一 LTS 上での holder-centric 管理
- Holder 解決
- Trait 要素解決
- Placeable settings 解決
- Runtime spawn
- `RuntimeTraitMB` への link data 注入
- 初期配置が invalid な場合の nearest valid pose 探索
- trait ごとの `TraitRuntimePresentationState` 管理
- placement state change event の発火
- link table 登録
- holder change 監視
- linked runtime の auto cleanup

`TraitPlacementService` の配置方針:

- `TraitPlacementService` は `TraitHolderHubService` と同じ LTS に置く
- 同じ LTS にあることで、`HolderKey` を軸にした trait placement 状態を局所的に管理できる
- 外部からは `ActorSource + HolderKey` で holder を引く既存パターンをそのまま流用できる
- `UITraitList` も同じ `HolderHubSource` の解決先から `TraitPlacementService` を参照する

### 12.3 SimpleMode

`SimpleMode` の定義:

- 指定位置に RuntimeLTS を spawn する
- spawn 後に Trait 要素との link を確立する
- spawn 直後の pose が invalid なら、設置可能な最近傍位置へ自動補正する
- preview / ghost / grid snap は行わない

初期配置補正の方針:

- runtime に `UserMoveRotateRuntimeMB` がある場合は、その制約を初期配置にも適用する
- 判定ルールは Editor mode 中の move validation と同一にする
- 実装は `TraitPlacementService` と `UserMoveRotateRuntimeService` の重複実装ではなく、共有 validator / search utility を使う
- できるだけ近い位置を優先し、要求位置からの距離が最小の valid pose を採用する
- valid pose が見つからない場合は invalid のまま残さず、SimpleMode の配置を失敗扱いにする

### 12.4 Spawn フロー

1. `ActorSource` で holder owner scope を解決
2. その scope の `ITraitHolderHubService` から `HolderKey` で holder を取る
3. `TraitElementSelector` で対象 `ITraitInstance` を解決する
4. `TraitDefinitionSO` の `PlaceableTraitSettings` から template を解決する
5. 既存リンクがあるか確認する
6. 無ければ Runtime spawner で spawn する
7. runtime に `UserMoveRotateRuntimeMB` がある場合、requested pose の valid 判定を行う
8. invalid なら nearest valid pose を探索し、見つかればその位置へ補正する
9. valid pose が見つからなければ spawn を失敗として release する
10. spawned runtime の `RuntimeTraitMB` へ link data を注入する
11. service が trait data + link data を blackboard に書く
12. link table に登録する
13. holder 監視対象に追加する

### 12.5 重複 placement の扱い

v1 では 1 trait element に対して active runtime は 1 つだけ。

同じ `TraitRuntimeLinkKey` に対する再 spawn 要求は:

- 既存 runtime が生きていればそれを返す
- 壊れていれば cleanup 後に再 spawn する

つまり v1 は idempotent に寄せる。
同一 trait から複数 runtime を生やすモードは作らない。

### 12.6 `RuntimeTraitMB`

spawned runtime には `RuntimeTraitMB` を必須で持たせる。

`RuntimeTraitMB` は主処理を持たない。
runtime 内に link 情報を保持し、service が参照できるようにする薄い bridge として扱う。

MB が持つ情報:

- `SourceScopeKind`
- `SourceScopeId`
- `SourceScopeCategory`
- `HolderKey`
- `TraitKey`
- `TraitDefinitionId`

MB の役割:

1. 自身がどの holder element に属するかを表す link data を保持する
2. `OnEnable` / `OnDisable` / `OnDestroy` などの lifecycle を service へ伝える

service 側の役割:

1. blackboard に trait data を書く
2. blackboard に link vars を書く
3. release / destroy 時の unlink を処理する

### 12.7 Blackboard 書き込み

`TraitPlacementService` は `RuntimeTraitMB` に保持された link data を使い、
少なくとも以下を blackboard に反映する。

- `WriteTraitDataExecutor` と同じ標準 trait vars
- `HolderKey`
- `TraitKey`
- `TraitDefinitionId`
- source scope identity

これにより runtime 側 command / visual / upgrade 側は、
「自分がどの Trait から来たか」を blackboard ベースで参照できる。

### 12.8 Holder change 監視

PlacementService は link を持つ holder の `OnTraitsChanged` を購読する。

ルール:

- linked `TraitKey` が holder から消えたら、その runtime を自動 release
- definition が同じでも新しい trait は別物として扱う

これにより「Holder から Trait を外したのに world に残骸 runtime が残る」状態を防ぐ。

### 12.9 runtime 側 destroy の扱い

runtime が外部都合で destroy / release された場合:

- PlacementService は reverse lookup を削除する
- holder 側 Trait までは消さない

PlacementService は holder と runtime の両方を同期するが、
runtime 消滅を holder 削除に直結させない。

### 12.10 実体化の visible / hidden 管理

本仕様における hidden は delete ではない。

方針:

- hidden に入っても runtime instance は保持する
- runtime 内の blackboard / service state / link は保持する
- 表示上は「遠くへ飛ばす」「描画しない」などで非表示化してよい
- PlacementService は trait ごとに `TraitRuntimePresentationState` を返せるようにする

最低限必要な公開 API:

- `TryGetPresentationState(holderKey, traitKey, out state)`
- `OnPresentationStateChanged`

### 12.11 UITraitList 連携

`UITraitListSystemMB` には以下の bool を追加する。

- `bool HideVisiblePlacedTraits`

意味:

- `false`
  従来通り holder の trait をそのまま list 表示する

- `true`
  TraitPlacementService を追加参照し、trait ごとの表示状態で list 要素を出し分ける

`HideVisiblePlacedTraits == true` の時のルール:

- `TraitRuntimePresentationState.Visible`
  UITraitList の該当要素は list から外す

- `TraitRuntimePresentationState.Hidden`
  UITraitList の該当要素は表示する

- `TraitRuntimePresentationState.None`
  UITraitList の該当要素は表示する

設計意図:

- visible 状態の trait はすでに実体化されているため、UI list 上では「出撃済み」の扱いにする
- hidden 状態の trait は runtime データを保持したまま UI list に戻したい
- 結果として UITraitList の要素自体が、そのまま実体化開始ボタンになる

実装方針:

- 実体化自体は UITraitList item の button command から command 経由で行う
- UITraitList 側は spawn 処理を直接持たない
- `UITraitListBuilderService` は slot 生成前に trait 一覧を filter する
- filter 判定には `TraitPlacementService` の presentation state を使う
- holder の `OnTraitsChanged` だけでなく、`TraitPlacementService.OnPresentationStateChanged` でも list refresh する

これにより、visible -> hidden、hidden -> visible の切り替えだけでも
UITraitList が追従できる。

---

## 13. UpgradeSystem との接続点

UpgradeSystem 自体は本書の対象外だが、以下は必須で残す。

1. 左短押し click を world selectable 単位で取れること
2. hover target を安定して取れること
3. selected target の blackboard から trait link を読めること

UpgradeSystem は将来、以下を利用する。

- `SelectRuntimeManagerService.OnLeftClickSelectable`
- `RuntimeTraitMB` の link data
- runtime blackboard 上の trait vars

つまり Upgrade を後で作るために必要な土台は本仕様で完成させる。

---

## 14. 右クリック hide との接続点

右クリック hide も本書の対象外だが、
foundation は必ず `RightClick` を独立 event として出す。

理由:

- 右クリック hide は selectable / upgrade と別物だから
- `Click` 単独 event に混ぜると後で分岐だらけになるから

将来の hide 実装は以下の組み合わせで作れる状態にする。

- `OnRightClickSelectable`
- `RuntimeTraitMB`
- `TraitPlacementService`

---

## 15. 実装段階

### Phase 1: WorldPointer 基盤

対象:

- `InputFrame` 左右分離
- `InputRouter` 左右入力配線
- `WorldPointerRuntimeService`
- `WorldPointerTargetMB`

完成条件:

- hover / left click / right click / long press が world object に対して安定して取れる

### Phase 2: SelectableRuntime

対象:

- `SelectRuntimeManagerMB`
- `SelectRuntimeManagerService`
- `SelectableRuntimeMB`

完成条件:

- manager 単位で hovered / current が取れる
- select / deselect command が流れる
- MB は authoring / bridge に留まり、選択処理本体は service 側にある

### Phase 3: UserMoveRotate

対象:

- `UserMoveRotateRuntimeMB`
- `UserMoveRotateRuntimeService`
- `AreaChannel` contains API 拡張

完成条件:

- long press で edit mode に入り
- move / scroll rotate が動き
- area / distance / collider 制約で止まる
- MB は制約設定の保持だけを行う
- Editor mode 中の left / right click で解除できる
- Enter / Exit command が流れる
- SelectManager global 無効化で自動解除される

### Phase 4: TraitPlacement SimpleMode

対象:

- `PlaceableTraitSettings`
- `TraitElementSelector`
- `TraitPlacementService`
- `RuntimeTraitMB`
- `UITraitList` との placement state 連携

完成条件:

- holder element から runtime を spawn
- spawn 位置が invalid なら最近傍 valid pose へ自動補正される
- link が残る
- trait removal で auto cleanup される
- `RuntimeTraitMB` は link data の保持と lifecycle bridge のみに留まる
- `HideVisiblePlacedTraits == true` の時、visible 状態の trait は UITraitList から外れ、hidden / none は表示される

### Phase 5: future systems

対象外:

- UpgradeSystem
- RightClick hide
- save / load

---

## 16. 受け入れ条件

以下をすべて満たした時点で本仕様の v1 実装完了とする。

1. 左クリックと右クリックが別 event として扱える
2. world object hover enter / exit が取れる
3. 左短押しで selection できる
4. 左長押しで move-rotate edit mode に入れる
5. edit 中の move は area 制約を超えない
6. edit 中の move は他 selectable との最小距離制約を守る
7. edit 中の move は block layer collider と重なる位置へ commit しない
8. Editor mode 中の left / right click で解除できる
9. Editor mode の Enter / Exit で command が流れる
10. parent `SelectRuntimeManager` が global select 不可になったら edit mode が自動解除される
11. Trait から RuntimeLTS を `SimpleMode` で spawn できる
12. spawn 位置が invalid なら最近傍 valid pose へ補正され、見つからなければ配置失敗になる
13. spawn された runtime に `RuntimeTraitMB` が入り、trait data が blackboard に書かれる
14. linked trait が holder から消えたら runtime も cleanup される
15. manager 無効化で hover / selection / edit mode が全て止まる
16. `HideVisiblePlacedTraits == true` の時、visible な trait は UITraitList で非表示になり、hidden / none は表示される
17. placement state の変化だけでも UITraitList が refresh される
18. 同じ scene 内に複数 manager が存在しても、親階層ベースで独立して動く

---

## 17. 保留事項

本書で意図的に保留したもの:

- Upgrade 候補の定義モデル
- 右クリック hide の最終 UX
- placement preview / ghost 表示
- placement 成功 / 失敗演出
- runtime pose の永続化
- 2D collider world pointer 対応

これらは本仕様の上に追加する。

重要なのは、v1 の時点で基盤責務を混ぜないこと。

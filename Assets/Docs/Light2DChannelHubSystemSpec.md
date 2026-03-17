<!--
Spec Version: v0.1
Status: Draft
Updated: 2026-03-14
Note:
- 本仕様書は新規作成です。
- 既存コードを読んだ上で、`AnimationSpriteHubService` 系に近い `2DLight ChannelHub` の設計案を整理しています。
- 本版は設計と実装方針の明確化を目的としており、仕様書自体には実装コードを含みません。
- `Light2D` の公開 API 制約も反映しています。特に `Parametric` 固有値や `normalMapQuality` 等の一部は runtime 変更対象から外します。
- 参照した主なコード:
  - Assets/GameLib/Script/Project/Scene/Channels/SpriteAnimation/AnimationSpriteHubService.cs
  - Assets/GameLib/Script/Project/Scene/Channels/SpriteAnimation/AnimationSpriteChannelPlayer.cs
  - Assets/GameLib/Script/Project/Scene/Channels/SpriteAnimation/AnimationSpriteChannelDef.cs
  - Assets/GameLib/Script/Project/Scene/Channels/SpriteAnimation/AnimationSpriteHubMB.cs
  - Assets/GameLib/Script/Project/Scene/Channels/Text/TextChannelHubService.cs
  - Assets/GameLib/Script/Project/Scene/SharedTexture/Core/SharedTextureChannelHubService.cs
  - Assets/GameLib/Script/Common/Commands/VNext/Commands/Channels/AnimationSpriteChannelCommandData.cs
  - Assets/GameLib/Script/Common/Commands/VNext/Executors/Channels/AnimationSpriteChannelExecutor.cs
  - Assets/GameLib/Script/Project/Scene/Visual/IVisualHub.cs
  - Assets/GameLib/Script/Project/Scene/Visual/VisualSystemService.cs
  - Library/PackageCache/com.unity.render-pipelines.universal@e9f15c489688/Runtime/2D/Light2D.cs
  - Library/PackageCache/com.unity.render-pipelines.universal@e9f15c489688/Runtime/2D/Light2DPoint.cs
  - Library/PackageCache/com.unity.render-pipelines.universal@e9f15c489688/Runtime/2D/Light2DShape.cs
-->

# Light2D ChannelHub システム仕様書

## 1. 目的

`AnimationSpriteChannelHub` と同じ文化で、`Light2D` を tag 経由で取得・操作できる基盤を作る。

今回の `Light2D ChannelHub` の主目的は次の通り。

1. `tag -> player -> Light2D` の解決経路を標準化する
2. 外部システムが `Intensity` `Color` `Enabled` などを直接 `Light2D` に触らず変更できるようにする
3. `Point / Freeform / Sprite / Global` の light type ごとの差分を player 側で吸収する
4. authoring 時の初期値を baseline として保持し、Scope release 時に安全に戻せるようにする
5. 将来的な command / preset / flicker / pulse / timeline 演出の受け皿にする


## 2. 既存コードを読んだ上での結論

### 2.1 既存 Hub パターン

既存の `AnimationSpriteHubService` / `TextChannelHubService` は、

- `tag -> def`
- `tag -> player`
- `Players` の列挙
- `Register / Unregister`

を Hub が持ち、実処理は player 側に寄せている。

この構成は `Light2D` にもそのまま適用できる。

### 2.2 `AnimationSpriteHub` との差分

`AnimationSpriteHub` は `MaterialFx` を hub state として broadcast / persistent state 管理しているが、
`Light2D` は MaterialFx のような stable key registry を前提にしない。

そのため `Light2D ChannelHub` では、MaterialFx の key-value ではなく、

- `Light2D` の writable property 群をまとめた state object
- state の partial override
- context tag と priority による layering

を player 側で扱う方が自然。

### 2.3 `Light2D` API の制約

URP 17 の `Light2D` 公開 API を確認した結果、runtime で安全に変更できる項目と、公開 setter が無い項目がある。

runtime 変更しやすい主な項目:

- `lightType`
- `color`
- `intensity`
- `blendStyleIndex`
- `falloffIntensity`
- `overlapOperation`
- `lightOrder`
- `volumeIntensity`
- `volumetricEnabled`
- `shadowsEnabled`
- `shadowIntensity`
- `shadowSoftness`
- `shadowSoftnessFalloffIntensity`
- `shadowVolumeIntensity`
- `volumetricShadowsEnabled`
- `targetSortingLayers`
- `lightCookieSprite`
- `pointLightInnerAngle`
- `pointLightOuterAngle`
- `pointLightInnerRadius`
- `pointLightOuterRadius`
- `shapeLightFalloffSize`
- `SetShapePath(...)`

runtime 変更対象から外すべき項目:

- `shapeLightParametricSides`
- `shapeLightParametricAngleOffset`
- `shapeLightParametricRadius`
- `normalMapDistance`
- `normalMapQuality`

理由:

- 公開 setter が無い
- runtime service 側で責任を持って変更できない
- reflection 前提の設計に寄せるべきではない


## 3. 設計方針

### 3.1 Hub は薄く、player に寄せる

`Light2DChannelHubService` は薄い管理層にする。

Hub の責務:

- channel def の保持
- player の生成・破棄
- tag 解決
- 一括操作のルーティング

player の責務:

- `Light2D` 参照の保持
- baseline の保存と復元
- context 単位の state layering
- writable property への最終適用
- light type ごとの差分吸収

### 3.2 baseline 復元を最優先にする

`Light2D` は scene authoring で調整されることが多く、外部システムが直接書き換えると原状復帰が壊れやすい。

そのため player は初期状態を snapshot し、`IScopeReleaseHandler` 時に確実に戻す前提にする。

### 3.3 v1 では「1 player = 1 Light2D」

今回の要求は「Player ごとに `2DLight` をセットするフィールドがあり、Player ごとに Tag がある」なので、
v1 は次の単位に固定する。

- 1 `Light2DChannelDef`
- 1 `Light2DChannelPlayer`
- 1 `Light2D`
- 1 `tag`

複数 Light 同時制御は v1 の対象外にする。

### 3.4 v1 は partial override + priority を標準にする

単発の `SetIntensity(tag, value)` だけだと、複数システムからの制御がすぐ競合する。

そのため v1 から、

- `contextTag`
- `priority`
- partial state
- clear / restore

を持つ layering 前提の設計にする。


## 4. 推奨クラス構成

### 4.1 新規クラス案

- `Assets/GameLib/Script/Project/Scene/Channels/Light2D/Light2DChannelHubMB.cs`
- `Assets/GameLib/Script/Project/Scene/Channels/Light2D/Light2DChannelHubService.cs`
- `Assets/GameLib/Script/Project/Scene/Channels/Light2D/ILight2DChannelHubService.cs`
- `Assets/GameLib/Script/Project/Scene/Channels/Light2D/Light2DChannelPlayer.cs`
- `Assets/GameLib/Script/Project/Scene/Channels/Light2D/Light2DChannelDef.cs`
- `Assets/GameLib/Script/Project/Scene/Channels/Light2D/Light2DChannelTypes.cs`
- `Assets/GameLib/Script/Project/Scene/Channels/Light2D/Light2DChannelPreset.cs`
- `Assets/GameLib/Script/Common/Commands/VNext/Commands/Channels/Light2DChannelCommandData.cs`
- `Assets/GameLib/Script/Common/Commands/VNext/Executors/Channels/Light2DChannelExecutor.cs`

必要なら後続で追加:

- `Light2DChannelHubDebugViewer.cs`
- `Light2DChannelPresetSO.cs`
- `Light2DChannelTweenService.cs`

### 4.2 MB / Service の責務

#### `Light2DChannelHubMB`

- `Light2DChannelDef[]` を serialize
- `InstallFeature` で `Light2DChannelHubService` を登録
- `EnsureIntegrity` を各 def に実行
- `IScopeAcquireHandler` / `IScopeReleaseHandler` を service 側に委譲

#### `Light2DChannelHubService`

- `IChannelHubService`
- `ILight2DChannelHubService`
- `IScopeAcquireHandler`
- `IScopeReleaseHandler`
- 必要なら `IDisposable`

### 4.3 DI 方針

このシステムは既存方針に合わせる。

- `Installer` 内で `Resolver` から解決する
- `[Inject]` は使わない
- 初期化は `IScopeAcquireHandler`
- リセットは `IScopeReleaseHandler`
- `IStartable` `IInitializable` は使わない


## 5. ChannelDef 仕様

### 5.1 `Light2DChannelDef`

`ChannelDefBase` 継承。

保持項目:

- `string tag`
- `Light2D targetLight`
- `bool restoreOnRelease`
- `bool applyOnAcquire`
- `Light2DChannelPreset initialPreset`
- `bool allowRuntimeLightTypeChange`
- `bool debugLog`

### 5.2 `EnsureIntegrity`

`EnsureIntegrity(Component owner)` で行うこと:

1. tag が空なら `default`
2. `targetLight == null` なら `owner.GetComponentInChildren<Light2D>(true)` を試す
3. `Light2D` が見つからない場合は def は無効扱い

### 5.3 Odin / Inspector 方針

`LightType` ごとの差分が大きいため、preset と state の inspector は `ShowIf` を使う前提にする。

例:

- `Point` のときだけ `InnerRadius / OuterRadius / InnerAngle / OuterAngle`
- `Freeform` のときだけ `ShapeFalloff / ShapePath`
- `Sprite` のときだけ `CookieSprite`
- `Global` は shape 系を非表示


## 6. Player 仕様

### 6.1 `Light2DChannelPlayer` の責務

player は次を持つ。

- `Tag`
- `Light2D Target`
- authored baseline snapshot
- runtime layer dictionary
- resolved state scratch
- dirty flag

player は Hub から命令を受け、最終的に `Light2D` に値を書き込む唯一の窓口にする。

### 6.2 baseline snapshot

baseline として保存する項目:

- `enabled`
- `lightType`
- `color`
- `intensity`
- `blendStyleIndex`
- `falloffIntensity`
- `overlapOperation`
- `lightOrder`
- `volumeIntensity`
- `volumetricEnabled`
- `shadowsEnabled`
- `shadowIntensity`
- `shadowSoftness`
- `shadowSoftnessFalloffIntensity`
- `shadowVolumeIntensity`
- `volumetricShadowsEnabled`
- `targetSortingLayers`
- `lightCookieSprite`
- `pointLightInnerAngle`
- `pointLightOuterAngle`
- `pointLightInnerRadius`
- `pointLightOuterRadius`
- `shapeLightFalloffSize`
- `shapePath`

注意:

- `shapePath` は deep copy が必要
- `targetSortingLayers` も copy が必要

### 6.3 layer state

1 つの player は複数 context を持てる。

1 context の保持項目:

- `string ContextTag`
- `int Priority`
- `Light2DChannelState State`
- `float LifetimeSeconds`
- `bool AutoRemoveOnLifetimeEnd`
- `bool Dirty`

解決ルール:

1. baseline を起点にする
2. priority 昇順で適用する
3. 同 priority は後勝ち
4. partial state の指定項目だけを上書きする

### 6.4 player 公開 API

推奨 API:

- `string Tag { get; }`
- `Light2D Target { get; }`
- `void SetState(string contextTag, in Light2DChannelState state, int priority = 0, float lifetimeSeconds = -1f)`
- `void ClearState(string contextTag)`
- `void ClearAllStates()`
- `void ApplyPreset(string contextTag, Light2DChannelPreset preset, int priority = 0, float lifetimeSeconds = -1f)`
- `void RestoreBaseline()`
- `Light2DChannelSnapshot CaptureCurrentSnapshot()`
- `bool TryGetResolvedState(out Light2DResolvedState state)`

### 6.5 適用タイミング

v1 は即時適用でよい。

流れ:

1. `SetState`
2. layer dictionary 更新
3. resolved state 再計算
4. `Light2D` へ即時反映

`LifetimeSeconds` を使う layer がある場合だけ、HubService か Player が `ITickable` を持つ。


## 7. State モデル

### 7.1 `Light2DChannelState` の考え方

`Light2DChannelState` は「すべて必須」ではなく、「指定された値だけ上書きする partial state」にする。

各項目は次のどちらかを持つ。

- 未指定
- 値あり

そのため実装は次のどちらかを推奨する。

1. `bool HasX + X Value`
2. `Optional<T>` 相当 struct

v1 は warning を増やしにくい `HasX + Value` でよい。

### 7.2 common state

全 light type 共通の候補:

- `Enabled`
- `Color`
- `Intensity`
- `BlendStyleIndex`
- `FalloffIntensity`
- `OverlapOperation`
- `LightOrder`
- `VolumeIntensity`
- `VolumetricEnabled`
- `ShadowsEnabled`
- `ShadowIntensity`
- `ShadowSoftness`
- `ShadowSoftnessFalloffIntensity`
- `ShadowVolumeIntensity`
- `VolumetricShadowsEnabled`
- `TargetSortingLayers`
- `CookieSprite`

### 7.3 point specific state

- `PointLightInnerAngle`
- `PointLightOuterAngle`
- `PointLightInnerRadius`
- `PointLightOuterRadius`

### 7.4 freeform specific state

- `ShapeLightFalloffSize`
- `ShapePath`

### 7.5 sprite specific state

- `CookieSprite`

### 7.6 global specific state

`Global` は shape 系を持たないため、common state のみ。

### 7.7 runtime 変更対象外

v1 では次を state に含めない。

- `NormalMapQuality`
- `NormalMapDistance`
- `ShapeLightParametricSides`
- `ShapeLightParametricAngleOffset`
- `ShapeLightParametricRadius`

### 7.8 `lightType` の扱い

`lightType` 自体の runtime 変更は API 上は可能だが、v1 は既定で無効にする。

理由:

- property の意味が大きく変わる
- shape path / point radius / cookie の整合が複雑
- authored state を壊しやすい

方針:

- `allowRuntimeLightTypeChange == false` を既定値
- 必要な player だけ opt-in


## 8. HubService 仕様

### 8.1 `ILight2DChannelHubService`

推奨 API:

- `IReadOnlyList<ILight2DChannelPlayer> Players { get; }`
- `ILight2DChannelPlayer GetPlayer(string tag)`
- `bool TryGetPlayer(string tag, out ILight2DChannelPlayer player)`
- `bool TryGetChannelDef(string tag, out ChannelDefBase def)`
- `bool RegisterChannel(ChannelDefBase def, bool overwrite = false)`
- `bool UnregisterChannel(string tag)`
- `bool SetState(string tag, string contextTag, in Light2DChannelState state, int priority = 0, float lifetimeSeconds = -1f)`
- `bool ClearState(string tag, string contextTag)`
- `void ClearAllStates(string tag)`
- `void RestoreBaseline(string tag)`
- `int BroadcastState(in Light2DChannelState state, string contextTag, int priority = 0, float lifetimeSeconds = -1f)`

### 8.2 tag 解決

既存チャネルと同様に、

- null / empty / whitespace は `default`
- `StringComparer.Ordinal`

を採用する。

### 8.3 Register / Unregister

既存 `AnimationSpriteHubService` と同じ方針:

- duplicate tag は `overwrite = false` なら失敗
- `overwrite = true` なら既存 player を破棄して差し替え

### 8.4 acquire / release

`OnAcquire`:

- baseline 再取得
- `applyOnAcquire` が有効なら `initialPreset` を適用
- lifetime layer の残骸があれば clear

`OnRelease`:

- 全 runtime state を clear
- `restoreOnRelease` が有効な player は baseline 復元

### 8.5 ticking

`LifetimeSeconds` や built-in flicker を service 側で管理するなら `ITickable` を実装する。

v1 の最低限:

- finite lifetime layer の減算
- 期限切れ context の削除


## 9. Command 連携仕様

### 9.1 `Light2DChannelCommandData`

`AnimationSpriteChannelCommandData` を参考に、apply flag ごとの構成にする。

候補:

- `ChannelTag`
- `ContextTag`
- `ClearContextFirst`
- `ApplyPreset`
- `PresetSource`
- `ApplyState`
- `StateSource`
- `ApplyEnabled`
- `EnabledSource`
- `ApplyIntensity`
- `IntensitySource`
- `ApplyColor`
- `ColorSource`
- `ApplyPointShape`
- `PointSettingsSource`
- `ApplySortingLayers`
- `SortingLayersSource`
- `ApplyRestoreBaseline`

### 9.2 `Light2DChannelExecutor`

executor の流れ:

1. `ILight2DChannelHubService` を subtree 優先で解決
2. tag から player を取得
3. command payload を `Light2DChannelState` へ変換
4. `SetState` または `ApplyPreset`
5. `RestoreBaseline` 指定時は baseline 復元

### 9.3 `DynamicValue` と editor wiring

後で `DynamicValue<Light2DChannelPreset>` や `DynamicValue<Light2DChannelStatePayload>` を増やす場合は、
editor 側の配線追加を忘れないことを前提にする。


## 10. Preset 仕様

### 10.1 `Light2DChannelPreset`

preset は `Light2DChannelState` の薄いラッパにする。

理由:

- SO 側を薄くしたい既存方針に合う
- `DynamicValue<T>` で扱いやすい
- serialize class をそのまま状態本体にできる

構成:

- `Light2DChannelState State`
- `bool ClearMissing`
- `int DefaultPriority`

### 10.2 preset 適用ルール

- `ClearMissing = true`
  - その context の以前の partial state を全消去してから preset を入れる
- `ClearMissing = false`
  - preset の指定項目だけ更新


## 11. エラー・競合方針

### 11.1 無効 light

`Light2D` が無い player は登録しない。

### 11.2 無効 property

例:

- `Point` でないのに `PointInnerRadius` 指定
- `Global` に `ShapePath` 指定

方針:

- 例外は投げない
- 開発ビルドで warning
- 適用可能な項目だけ適用

### 11.3 unsupported runtime property

公開 setter が無い項目は v1 では受け付けない。

### 11.4 `Global Light` の重複

URP 側は同 sorting layer + blend style に複数 global light があるとエラーを出す。

そのため仕様として:

- Hub は global light の重複自体は管理しない
- ただし debug viewer で検出可能にする余地を残す


## 12. v1 実装範囲

### 12.1 必須

- Hub / Player / Def / MB
- baseline snapshot / restore
- partial state
- contextTag + priority
- common property の変更
- point light 固有 property の変更
- sprite light cookie の変更
- freeform の `shapeLightFalloffSize` と `SetShapePath`
- command executor

### 12.2 後回しでよいもの

- runtime `lightType` 切り替え
- parametric light の詳細制御
- tween / fade の built-in
- VisualSystem 連携
- group tag / label selector
- debug viewer
- preset SO


## 13. Player に付けると有効な追加機能案

### 13.1 built-in fade

`enabled` のオンオフを即切り替えではなく、`intensity` fade 経由で切り替える。

用途:

- ライト出現
- 消灯
- アラート灯のゆっくり点滅

### 13.2 pulse

一定時間だけ `intensity` と `outerRadius` を膨らませる。

用途:

- ヒット
- 取得演出
- ギミック反応

### 13.3 flicker / noise

乱数または noise で `intensity` `color` を揺らす。

用途:

- ロウソク
- 故障した蛍光灯
- 危険領域の警告灯

### 13.4 follow offset

`Light2D` 自体の transform を service で追従させる。

用途:

- キャラの手持ちランタン
- 攻撃エフェクトに追従する点光

注意:

transform 制御は `TransformAnimation` 系と責務が被るため、v1 では optional に留める。

### 13.5 cookie swap / animation

`lightCookieSprite` をフレーム的に差し替える。

用途:

- 炎の揺らぎ
- 水面反射
- 壊れたネオン

### 13.6 sorting layer mask preset

`targetSortingLayers` の組を preset 化する。

用途:

- UI だけ照らす
- 背景だけ照らす
- 前景とキャラだけ照らす

### 13.7 local tag alias / sub tag

1 player に主 tag 以外の alias を持たせる。

用途:

- `boss/core`
- `boss/emergency`
- `boss/all`

ただし v1 は単一 tag を維持し、alias は v2 以降でよい。

### 13.8 event hook

player が state 変更時に event を出す。

用途:

- デバッグ表示
- SE 連動
- analytics / telemetry

### 13.9 hub-level batch API

複数 tag に同じ state を一括送信する。

用途:

- 部屋全体の消灯
- 警報時の一括赤化
- フェーズ遷移時の一括変更


## 14. 推奨実装マイルストーン

### 14.1 Phase 1: 基盤

実装:

- `Def`
- `Player`
- `HubService`
- `HubMB`
- baseline restore
- common state の即時適用

完了条件:

- tag で `Intensity` `Color` `Enabled` を変更できる
- release 時に baseline に戻る

### 14.2 Phase 2: type-specific

実装:

- point light settings
- sprite cookie
- freeform falloff / shape path
- command executor

完了条件:

- 主要 light type ごとの差分設定を command から変更できる

### 14.3 Phase 3: 演出拡張

実装:

- lifetime state
- built-in fade / pulse / flicker
- preset
- debug viewer

完了条件:

- 単発変更だけでなく、ゲーム演出用途の反復利用が可能になる


## 15. 最終結論

`Light2D ChannelHub` は、

- `AnimationSpriteHub` のような tag 解決 Hub
- `MaterialFx` のような key registry ではなく partial state layering
- `Light2D` の baseline 復元を前提にした player

として作るのが最も自然。

特に重要なのは次の 3 点。

1. `Hub` は薄くし、`player` に状態解決を寄せる
2. `Light2D` を直接書き換えず、baseline + context layering で扱う
3. `Light2D` の公開 API で安全に変更できる範囲だけを v1 の対象にする

この方針なら、まずは tag 指定での `Intensity / Color / Enabled / Point設定` を安全に提供し、
その上で pulse / flicker / preset / command へ拡張しやすい。

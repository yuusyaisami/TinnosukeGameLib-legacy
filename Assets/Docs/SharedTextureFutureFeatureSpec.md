<!--
Spec Version: v0.1
Status: Draft / Focused
Updated: 2026-03-13
Note:
- 本仕様書は `CameraCaptureTextureSharingSystemPlan.md` の後続仕様書として新規作成しています。
- SharedTexture 基盤の現実装を読んだ上で、未実装の次項目に重点を置いて整理しています。
  - `SharedTextureDisplay2D / ExternalTextureComposite2D`
  - `Noise -> SharedTexture` 統一
  - `Compute` 依存の完全削除
  - `ScreenFx` 系コードの実削除
- 参照した主なコード:
  - Assets/GameLib/Script/Project/Scene/SharedTexture/Core/ISharedTextureChannelHub.cs
  - Assets/GameLib/Script/Project/Scene/SharedTexture/Core/SharedTextureChannelHubService.cs
  - Assets/GameLib/Script/Project/Scene/SharedTexture/Core/SharedTextureChannelTypes.cs
  - Assets/GameLib/Script/Project/Scene/SharedTexture/Binder/ITaggedMaterialFxProvider.cs
  - Assets/GameLib/Script/Project/Scene/SharedTexture/Binder/SharedTextureMaterialBinderService.cs
  - Assets/GameLib/Script/Project/Scene/SharedTexture/MB/SharedTextureMaterialBinderMB.cs
  - Assets/GameLib/Script/Project/Scene/SharedTexture/Debug/SharedTextureChannelDebugView.cs
  - Assets/GameLib/Script/Project/Scene/CameraSystem/Core/ICameraRenderContext.cs
  - Assets/GameLib/Script/Project/Scene/CameraSystem/Capture/CameraCaptureService.cs
  - Assets/GameLib/Script/Project/Scene/CameraSystem/Capture/CameraCaptureRenderPass.cs
  - Assets/GameLib/Script/Project/Scene/CameraSystem/Capture/CameraCaptureRegistry.cs
  - Assets/GameLib/Script/Project/Scene/CameraSystem/OutputOverride/CameraOutputOverrideService.cs
  - Assets/GameLib/Script/Project/Scene/TextureEffect/Core/TextureEffectPipelineService.cs
  - Assets/GameLib/Script/Project/Scene/TextureEffect/Core/TextureEffectTypes.cs
  - Assets/GameLib/Script/Project/Scene/Channels/SpriteAnimation/AnimationSpriteChannelDef.cs
  - Assets/GameLib/Script/Project/Scene/Channels/SpriteAnimation/AnimationSpriteChannelPlayer.cs
  - Assets/GameLib/Script/Project/Scene/Channels/SpriteAnimation/AnimationSpriteHubService.cs
  - Assets/GameLib/Script/Shader/Core/BaseShader/BaseShader.shader
  - Assets/GameLib/Script/Shader/Core/BaseShader/Surface2D.hlsl
  - Assets/GameLib/Script/Shader/Core/BaseShader/Features/TextureSlot2D.hlsl
  - Assets/GameLib/Script/Shader/Core/BaseShader/Features/ScreenFx2D.hlsl
  - Assets/GameLib/Script/Shader/Core/MaterialFx/TextureSlotTypes.cs
  - Assets/GameLib/Script/Shader/Core/MaterialFx/MaterialFxMB.cs
  - Assets/GameLib/Script/Shader/Core/MaterialFx/Services/MaterialFxKernelService.cs
  - Assets/GameLib/Script/Shader/Core/MaterialFx/MaterialFxValueTypes.cs
  - Assets/GameLib/Script/Shader/Core/MaterialFx/Services/MaterialFxDispatchService.cs
  - Assets/GameLib/Script/Shader/Core/MaterialFx/CSV/AdditionalEntries.csv
  - Assets/GameLib/Script/Project/Render/ScreenFx/*
- 本版注記:
  - 一部記述は 2026-03-14 時点の現コードへ合わせて補正しています
  - 特に `MaterialFxMB` / `AnimationSprite` / `ScreenFx2D` に関する旧記述は、実コードとの差分を明示する方針に変更しています
  - `NoiseAtlas2D.hlsl` / `_AtlasSlot0-4` / `BaseShader/TextureSlot/*` は 2026-03-14 時点で削除済みです
  - 本文中の Atlas / NoiseAtlas 記述の一部は、削除前の分析履歴として残しています
-->

# SharedTexture 拡張・移行仕様書

## 1. 目的

本仕様書は、すでに実装が始まっている `CameraCapture + SharedTexture + TextureEffect + CameraOutputOverride` の次段階を整理する。

今回の焦点は、次の 4 項目だけに絞る。

1. BaseShader で外部 Texture をそのまま描画するための `SharedTextureDisplay2D / ExternalTextureComposite2D`
2. Noise 出力面を `SharedTexture` に統一する方針
3. `ComputeShader` 依存を最終的に撤去するための移行設計
4. 旧 `ScreenFx` 系コードをどの条件で削除するか


## 2. 現状コードを読んだ上での整理

### 2.1 すでに成立しているもの

- `CameraCaptureService` と `CameraCaptureRenderPass` により、Camera の描画結果を `camera/{cameraTag}/source` として publish する基盤はある
- `SharedTextureChannelHubService` により、tag ベースで Texture と Camera metadata を共有できる
- `TextureEffectPipelineService` により、入力 tag から出力 tag へ effect をかけて再 publish する流れは成立している
- `CameraOutputOverrideService` により、Camera の最終出力を SharedTexture 由来の Texture に差し替える流れは成立している

### 2.2 今回の重点領域で見えた事実

#### BaseShader / MaterialFx 側

- `TextureSlot2D.hlsl` には `_ExtTexA` `_ExtTexB` `_CustomRT` があり、外部 Texture の受け口自体は存在する
- `SharedTextureMaterialBinderService` も `ExternalA / ExternalB / CustomRT` の bind 先を持っている
- `BaseShader.shader` / `Surface2D.hlsl` には external texture slot はあるが、外部 texture を汎用表示する専用 composite は未実装だった
- つまり、外部 Texture の受け口はあるが、「受け取った外部 Texture をそのまま表示する専用機能」はまだない

#### Noise / Compute 側

- `MaterialFxMB` 自体はすでに Compute 前提初期化を持っていない
- 一方で `TextureSlot2D.hlsl` / `NoiseAtlas2D.hlsl` / `_AtlasSlot0-4` は残っており、BaseShader 側に旧 Atlas ラインが残存している
- `TextureSlotType` も `AtlasSlot0-4` と `ExternalA/B/CustomRT` を同じ型に抱えている
- つまり、現在の主要 legacy は Compute 初期化そのものより、**Atlas slot と NoiseAtlas サンプリングの残存** である

#### SharedTexture Binder 側

- `ITaggedMaterialFxProvider` は定義されている
- ただし、今回読んだ範囲では `AnimationSpriteHubService` 側の concrete 実装はまだ見当たらない
- そのため、SharedTexture を MaterialFx へ流す責務分離は正しいが、consumer への最終接続はまだ暫定段階にある

### 2.3 今回の仕様書の基本判断

今回の 4 項目は、すべて次の原則で統一する。

- 共有の正本は `SharedTextureChannelHubService`
- BaseShader は「受け取った Texture を使う consumer」
- Noise も Camera capture も、最終的には同じ SharedTexture 契約へ寄せる
- 新機能は `ScreenFx` 専用プロパティを増やさず、既存 `TextureSlotRef + External Texture` 系へ寄せる


## 3. 命名と責務

`SharedTextureDisplay2D` と `ExternalTextureComposite2D` は同じものとして扱わない。

推奨する切り分けは次の通り。

- `ExternalTextureComposite2D`
  - BaseShader / HLSL / MaterialFx 上の **汎用機能名**
  - 入力が SharedTexture である必要はない
  - `_ExtTexA / _ExtTexB / _CustomRT` を使う一般機能

- `SharedTextureDisplay2D`
  - `ExternalTextureComposite2D` を使った **用途名 / プリセット名**
  - 入力元が SharedTexture である場合の利用パターン
  - 「写真を貼る」「撮影画像を UI に出す」などの上位概念

結論:

- 実装名は `ExternalTextureComposite2D`
- SharedTexture を表示する preset / profile 名として `SharedTextureDisplay2D` を使う

この方が、機能名が SharedTexture に縛られず、ImportedTexture や生成 RT も同じ経路で使える。


## 4. ExternalTextureComposite2D 仕様

### 4.1 目的

`ExternalTextureComposite2D` は、「MaterialFx が bind した外部 Texture を BaseShader 側でそのまま描画・合成する」ための専用 CompositeSystem である。

これにより次が可能になる。

- Camera capture 画像を Sprite / UI に写真のように表示する
- Effect pipeline の出力 Texture を UI / Sprite / Screen object に貼る
- Noise / Distortion 用に受け取った外部 Texture を、ScreenFx を経由せずそのまま使う

### 4.2 既存 ScreenFx とどう違うか

`ScreenFx` は専用の `_ScreenFxSourceTex` `_ScreenFxBlurTex` と mode enum に依存している。  
`ExternalTextureComposite2D` はそれをやめ、**既存の TextureSlotRef と External Texture slot を使う**。

つまり違いは次である。

- `ScreenFx`: Screen 専用の固定入力
- `ExternalTextureComposite2D`: 外部 Texture を汎用入力とする表示レイヤ

### 4.3 入力契約

入力は `TextureSlotRef` ベースとする。

許可する `SlotType` は初期段階では次のみ。

- `ExternalA = 5`
- `ExternalB = 6`
- `CustomRT = 7`

初期段階では `AtlasSlot0-4` は許可しない。

理由:

- この機能の責務は SharedTexture / ImportedTexture / RT 表示であり、Atlas/Kernel 再利用ではない
- Atlas slot を許可すると NoiseAtlas 旧設計と責務が再混線する

### 4.4 推奨パラメータ

最低限必要なパラメータは次。

- `Enabled`
- `Source` (`TextureSlotRef`)
- `BlendMode`
- `Intensity`
- `UseTextureAlpha`
- `Tint`
- `DisableWhenTextureMissing`
- `AffectSurfaceAlpha`

必要なら後から追加するもの:

- `FitMode`
- `PreserveAspect`
- `PremultiplySource`
- `UVScroll`

### 4.5 Blend の意味

初期実装の `BlendMode` は次でよい。

- `Replace = 0`
- `Lerp = 10`
- `Add = 20`
- `Multiply = 30`

初期挙動:

- `Replace`: source をそのまま使う
- `Lerp`: base と source を mix する
- `Add`: source を加算する
- `Multiply`: base に source を乗算する

### 4.6 Shader 上の処理手順

`Surface2D_ApplyExternalTextureComposite()` の処理は次で統一する。

1. `Enabled == false` なら no-op
2. `Source.SlotType` が `ExternalA/B/CustomRT` 以外なら no-op
3. `SampleSlotRGBA()` 相当で source color/alpha を取得する
4. `Tint` を適用する
5. `UseTextureAlpha` に応じて blend weight を決定する
6. `Intensity` を掛ける
7. `BlendMode` に応じて `surface.color` を更新する
8. `AffectSurfaceAlpha` が有効な場合だけ `surface.alpha` を更新する

重要:

- この feature は「表示 / 合成」の責務だけを持つ
- 外部 Texture を bind する責務は持たない
- Texture 不在時の clear は binder / MaterialFx 側で行う

### 4.7 UV と表示の考え方

UV は新規独自設計を作らず、既存の `TextureSlotRef.UVSpace` を流用する。

初期段階での想定は次。

- `SpriteLocal`
  - 写真を Sprite / UI の矩形に収めて表示する
- `Screen`
  - スクリーン空間準拠のサンプリングを行う
- `AtlasRaw`
  - 必要最小限の互換用
- `WorldXY`
  - 将来用

注意:

- 「画面全体をそのまま写真のように貼る」用途の主力は `SpriteLocal`
- 「スクリーン上の現位置の絵を参照する」用途の主力は `Screen`

### 4.8 BaseShader / MaterialFx キー構成

新規に次の key root を持つ。

- `BaseShader/CompositeSystems/ExternalTextureComposite/Enabled`
- `BaseShader/CompositeSystems/ExternalTextureComposite/Source/SlotType`
- `BaseShader/CompositeSystems/ExternalTextureComposite/Source/Channel`
- `BaseShader/CompositeSystems/ExternalTextureComposite/Source/UVSpace`
- `BaseShader/CompositeSystems/ExternalTextureComposite/Source/TilingOffset`
- `BaseShader/CompositeSystems/ExternalTextureComposite/Source/Remap`
- `BaseShader/CompositeSystems/ExternalTextureComposite/BlendMode`
- `BaseShader/CompositeSystems/ExternalTextureComposite/Intensity`
- `BaseShader/CompositeSystems/ExternalTextureComposite/UseTextureAlpha`
- `BaseShader/CompositeSystems/ExternalTextureComposite/Tint`
- `BaseShader/CompositeSystems/ExternalTextureComposite/DisableWhenTextureMissing`
- `BaseShader/CompositeSystems/ExternalTextureComposite/AffectSurfaceAlpha`

加えて、`External Texture` 実体の正規キーを揃える。

- `BaseShader/ExternalTextures/ExtTexA`
- `BaseShader/ExternalTextures/ExtTexB`
- `BaseShader/ExternalTextures/CustomRT`

重要:

- `ExternalTextures/*` は Texture 実体 bind 用
- `CompositeSystems/ExternalTextureComposite/*` は使い方指定用

この分離を明文化することが重要である。

### 4.9 SharedTextureDisplay2D preset

`SharedTextureDisplay2D` は shader 機能ではなく preset / profile として定義する。

初期プリセット候補:

- `SharedTextureDisplay2D.Photo`
  - `Source = ExternalA`
  - `BlendMode = Replace`
  - `UseTextureAlpha = false`
  - `DisableWhenTextureMissing = true`
  - `UVSpace = SpriteLocal`

- `SharedTextureDisplay2D.Window`
  - `Source = ExternalA`
  - `BlendMode = Lerp`
  - `UseTextureAlpha = true`
  - `UVSpace = Screen`

### 4.10 実装上の完了条件

- `ScreenFx` を使わずに Camera capture を Sprite / UI 上へ表示できる
- `SharedTextureMaterialBinderService` が `ExternalTextures.CustomRT` を正規キーで bind できる
- BaseShader 側に `_ScreenFxSourceTex/_ScreenFxBlurTex` を使わない表示パスができる


## 5. Noise -> SharedTexture 統一仕様

### 5.1 目的

Noise は生成方法ではなく、**出力面を SharedTexture に統一する**。

Consumer から見える契約は次のみとする。

- どの tag を読むか
- その tag がどの解像度 / 更新頻度で出るか

Consumer は次を意識しない。

- Compute で作られたか
- Fragment で作られたか
- 事前生成画像か

### 5.2 Producer モデル

今後のノイズ生成は `NoiseProducer` 群として整理する。

推奨クラス:

- `NoiseProducerMB`
- `NoiseProducerService`
- `INoiseTextureProducer`
- `NoisePublishDef`

Producer の種類は複数あってよい。

- `ImportedNoiseProducer`
- `MaterialBlitNoiseProducer`
- `AnimatedNoiseSequenceProducer`
- `LegacyComputeNoiseProducer`（移行期間のみ）

### 5.3 SharedTexture 側の tag 命名

Noise 系 tag は `noise/` で始める。

例:

- `noise/common/base`
- `noise/common/detail`
- `noise/common/distortion`
- `noise/scene/fog`
- `noise/scene/heat.low`

推奨ルール:

- 用途名を tag に含める
- tier/slice ではなく意味名を使う
- 低解像度版は `.low` など suffix で表す

### 5.4 Consumer 側の受け取り方

Consumer が noise を使いたい場合、やることは次だけにする。

1. Binder で SharedTexture tag を `ExternalA/B/CustomRT` のどれかへ bind する
2. BaseShader feature 側は `TextureSlotRef.ForExternal()` を使う

これにより、Camera capture と noise の利用経路を一致させる。

### 5.5 どの生成方法を優先するか

WebGL を重視するなら、優先順位は次でよい。

1. 事前生成 Texture
2. Fragment / Blit Material 生成
3. アニメーション済みノイズ列
4. CPU 生成 Texture（低頻度）
5. Compute 生成（移行期間限定）

理由:

- SharedTexture は GPU リソース参照の共有に向く
- WebGL では Compute が使えない
- CPU upload は SharedTexture の利点を殺しやすい

### 5.6 新規機能の原則

新しく追加する visual feature は、noise 参照元として Atlas slot を前提にしない。

今後の原則:

- 新規 feature は `ExternalA/B/CustomRT` を優先する
- `NoiseAtlasHub` 直結は legacy 扱いにする
- SharedTexture tag を通した noise 供給を標準にする

### 5.7 既存 NoiseAtlas との関係

既存 `NoiseAtlasHub` は即削除しない。  
ただし役割は段階的に縮小する。

初期方針:

- 既存 Atlas/Kernel 系は legacy ラインとして維持
- 新規 SharedTexture 系 feature は Atlas を前提にしない
- 移行後に Atlas/Kernel 系利用箇所が 0 になった時点で撤去する


## 6. Compute 依存完全削除の仕様

### 6.1 今すぐやるべきことと、今やらないこと

この仕様で重要なのは、**いきなり全削除しない**ことではなく、**新規実装を Compute に寄せないこと**である。

今すぐやるべきこと:

- 新規機能で `MaterialFxSenderKind.ComputeKernel` を増やさない
- 新規 Noise 機能を SharedTexture 出力前提で作る
- `AnimationSpriteChannelDef` の新規用途に `KernelBindEnabled` を使わない

今やらないこと:

- 旧 preset を一括自動変換すること
- 旧 atlas ベース asset を即時削除すること

### 6.2 廃止対象

長期的な撤去対象は次。

- `TextureSlotType.AtlasSlot0-4`
- `AtlasSlotBinding`
- `CompositeEffectBundle.Slot0-4`
- `CompositeEffectKeys.SetAtlasSlotBindings()`
- `AtlasSlotBindingExtensions`
- `IMaterialFxKernelService`
- `MaterialFxKernelService`
- `MaterialFxSenderKind.ComputeKernel`
- `MaterialFxComputeKernelKind`
- `KernelHub`
- `ProceduralNoise2D.compute`
- `Kernel2D.hlsl`
- `BaseShader/NoiseAtlas/*`
- `_AtlasSlot0-4`
- `NoiseAtlas2D.hlsl`

補足:

- `NoiseAtlas2D.hlsl` や Atlas slot 自体は、すべての legacy 参照が消えるまで残してよい
- ただし「新規依存の入口」は早い段階で閉じるべき

### 6.3 置き換え先

Compute / Kernel / Atlas slot でやっていたことは、最終的に次へ置き換える。

- Noise 生成 -> `NoiseProducerService`
- 出力格納 -> `SharedTextureChannelHubService`
- 各 consumer への受け渡し -> `SharedTextureMaterialBinderService`
- BaseShader での利用 -> `TextureSlotRef.ForExternal()`

### 6.4 AnimationSprite 側の置換方針

現状の `AnimationSprite` は、以前の Kernel bind 直結ではなくなっている。
今後さらに Atlas / SharedTexture 分離を進める場合の置換方針は次。

- Atlas slot を前提にした helper / preset 入口 -> 廃止
- texture の受け渡し -> `SharedTextureBindingDef` へ移行
- `AnimationSpriteHubService` -> `ITaggedMaterialFxProvider` 実装を標準接続にする

推奨方針:

- ChannelDef 自体は「どの tag を受けるか」だけを持つ
- 実際の Texture 実体 bind は binder service が行う

これにより、AnimationSprite 自体が NoiseAtlas / Kernel の詳細を知らなくて済む。

### 6.5 完了条件

Compute 依存の完全削除は、次の条件を満たしたときに完了とする。

- WebGL / 非 WebGL で visual パスの構成が大きく分岐しない
- 新規 visual 機能が `ComputeKernel` sender を参照していない
- `AnimationSpriteChannelDef` が Kernel 専用フィールドを持たない
- Noise 系の consumer が Atlas slot 前提ではなく SharedTexture 前提になっている

### 6.6 Atlas 依存監査

現時点で Atlas に依存している主な箇所:

- `Assets/GameLib/Script/Shader/Core/MaterialFx/TextureSlotTypes.cs`
- `Assets/GameLib/Script/Shader/Core/BaseShader/Features/TextureSlot2D.hlsl`
- `Assets/GameLib/Script/Shader/Core/BaseShader/Features/NoiseAtlas2D.hlsl`
- `Assets/GameLib/Script/Shader/Core/BaseShader/BaseShader.shader`
- `Assets/GameLib/Script/Shader/Core/BaseShader/Surface2D.hlsl`
- `Assets/GameLib/Script/Shader/Core/MaterialFx/CompositeEffectParams.cs`
- `Assets/GameLib/Script/Shader/Core/MaterialFx/CompositeEffectKeys.cs`
- `Assets/GameLib/Script/Shader/Core/MaterialFx/AtlasSlotBindingExtensions.cs`
- `Assets/GameLib/SO/MaterialFx/MaterialFxPropertyRegistry.asset`

推奨削除順:

1. `SharedTexture + ExternalTextureComposite2D` を consumer の正規ルートにする
2. 新規実装で Atlas slot を使う入口を閉じる
3. 既存 preset / helper の Atlas 前提を SharedTexture bind へ寄せる
4. `CompositeEffectBundle.Slot0-4` と関連 helper を削除する
5. 最後に `TextureSlot2D.hlsl` の Atlas 分岐、`NoiseAtlas2D.hlsl`、`_AtlasSlot0-4` を削除する


## 7. ScreenFx 実削除の仕様

### 7.1 削除方針

`ScreenFx` は「残す機能」ではなく、「移行完了後に削除する legacy」と明記する。

削除対象は次。

- `Assets/GameLib/Script/Project/Render/ScreenFx/*`
- `Assets/GameLib/Script/Shader/Core/BaseShader/Features/ScreenFx2D.hlsl`
- `BaseShader.shader` 内の `_ScreenFx*` property
- `Surface2D.hlsl` の `ScreenFx2DParams` / `MakeScreenFx2DParams` / `ApplyScreenFx`
- `MaterialFx/CSV/AdditionalEntries.csv` 内の `ScreenFx` key 群
- `ScreenFxMigrationBridgeMB`

### 7.2 削除前提条件

次が揃うまでは削除しない。

1. `ExternalTextureComposite2D` が実装済み
2. `SharedTextureDisplay2D` preset で写真表示用途を代替できる
3. Blur / Mosaic / Distort 等が `TextureEffectPipelineService` 側で代替できる
4. Camera 最終出力差し替えが `CameraOutputOverrideService` で安定している
5. Scene / Prefab / Preset / SO に `ScreenFx` key 参照が残っていない

### 7.3 削除手順

推奨手順は次。

1. 新規利用を禁止する
2. `ScreenFxMigrationBridgeMB` を obsolete 扱いにする
3. `ScreenFx` key を preset / asset から除去する
4. BaseShader に `ExternalTextureComposite2D` を入れる
5. `ScreenFx` folder の C# と shader を削除する
6. `AdditionalEntries.csv` から `ScreenFx` 項目を削除し、registry を再生成する
7. `rg "ScreenFx"` で参照残りを確認してから final cleanup する

### 7.4 削除判定に使う grep 基準

最終削除前に、少なくとも次を確認する。

- `ScreenFxBufferFeature`
- `ScreenFxBinderService`
- `IScreenFxBuffer`
- `IScreenFxBinder`
- `_ScreenFxEnabled`
- `_ScreenFxSourceTex`
- `_ScreenFxBlurTex`
- `BaseShader/CompositeSystems/ScreenFx`

これらの runtime 参照が 0 になった時点で、削除へ進む。


## 8. 推奨実装順

この仕様書の対象は、次の 3 段階で進めるのがよい。

### 8.1 Step 1: ExternalTextureComposite2D

先にやること:

- `BaseShader/ExternalTextures/CustomRT` 正規キー追加
- `ExternalTextureComposite2D.hlsl` 実装
- MaterialFx key 追加
- `SharedTextureDisplay2D` preset 作成

この段階の成果:

- ScreenFx を経由しなくても、SharedTexture を Sprite / UI に表示できる

### 8.2 Step 2: Noise を SharedTexture 側へ寄せる

次にやること:

- `NoiseProducerService` 系の設計と実装
- noise tag 命名の統一
- 新規 noise consumer を `ExternalA/B/CustomRT` 前提へ変更

この段階の成果:

- Noise の生成方法と consumer が分離される

### 8.3 Step 3: Compute と ScreenFx の legacy を閉じる

最後にやること:

- Kernel bind 専用 API を段階的に除去
- `ScreenFx` 専用 C# / shader / key 群を削除
- 旧 atlas / kernel 依存を grep ベースで潰す

この段階の成果:

- SharedTexture 系が visual pipeline の正規ルートになる


## 9. 最終結論

今後の正規ルートは次に固定するのがよい。

1. Texture は `SharedTextureChannelHubService` に置く
2. BaseShader は `ExternalTextureComposite2D` でそれを使う
3. Noise も Camera capture も同じ SharedTexture 契約に載せる
4. Compute と ScreenFx は legacy として縮退させ、最終的に削除する

この方針なら、`SpriteRenderer`、`UI Image`、`Camera override`、`Texture effect` のすべてが同じ texture sharing 契約に揃う。

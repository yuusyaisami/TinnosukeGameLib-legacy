<!--
Spec Version: v0.9
Status: Draft / Reviewed
Updated: 2026-03-13
Note:
- 本仕様書は新規作成です。
- 既存コードを読んだ上で、ScreenFx の分離方針と Camera Capture / Shared Texture Channel の再設計案を整理しています。
- v0.2 では、Sprite 範囲マスクのための Camera Capture メタデータ保持方針を追記しています。
- v0.3 では、ScreenFx 系の不要化、AnimationSpriteHubService 経由の MaterialFx 受け渡し、MaterialFx/Noise/WebGL の再評価、Camera 出力差し替え案を追記しています。
- v0.4 では、ノイズ生成の最終出力面を SharedTexture に統一する将来方針を追記しています。
- v0.5 では、SharedTexture の CPU/性能評価と、Effect Layer定義とMask登録を分離する方針を追記しています。
- v0.6 では、Consumer Layer のうち SpriteRenderer/UI Image 利用を MaterialFx consumer 経由の統一路線として表現を修正しています。
- v0.7 では、Mask は「対象の形」ではなく「カメラに実際に見えている部分」を使う方針を追記しています。
- v0.8 では、レビューに基づき以下を追記・修正しています:
  - 見出しレベルの不整合を修正（4.x / 9.x セクション）
  - SharedTextureFrame の Width/Height が Descriptor と二重管理になる点を注記
  - タグ命名規約を明文化（4.2.3）
  - エラー・障害ハンドリング方針を追記（4.2.5）
  - フレームタイミング方針を追記（4.2.6）
  - Section 5 と Section 4.3 の関係を明記
  - Noise セクション（4.4.4）のスコープ注記を追加
  - RTHandle 所有権の初期推奨を追記（Section 12）
- v0.9 では、実装に着手しやすくするため、3段階の実装マイルストーンと各完了条件を追記しています。
- 参照した主なコード:
  - Assets/GameLib/Script/Project/Scene/CameraSystem/MB/CameraSystemMB.cs
  - Assets/GameLib/Script/Project/Scene/CameraSystem/Core/CameraSystemService.cs
  - Assets/GameLib/Script/Project/Render/ScreenFx/*
  - Assets/GameLib/Script/Shader/Core/BaseShader/Surface2D.hlsl
  - Assets/GameLib/Script/Shader/Core/BaseShader/Features/ScreenFx2D.hlsl
  - Assets/GameLib/Script/Shader/Core/BaseShader/Features/TextureSlot2D.hlsl
  - Assets/GameLib/Script/Shader/Core/MaterialFx/TextureSlotTypes.cs
  - Assets/GameLib/Script/Shader/Core/MaterialFx/IMaterialFxReceiver.cs
  - Assets/GameLib/Script/Shader/Core/MaterialFx/MaterialFxMB.cs
  - Assets/GameLib/Script/Shader/Core/MaterialFx/Services/MaterialFxKernelService.cs
  - Assets/GameLib/Script/Shader/Core/SO/BaseShaderFxPresetSO.cs
  - Assets/GameLib/Script/Shader/Core/BaseShader/Features/NoiseAtlas2D.hlsl
  - Assets/GameLib/Script/Project/Scene/Channels/MeshFx/MeshFxChannelHubService.cs
  - Assets/GameLib/Script/Project/Scene/Channels/SpriteAnimation/AnimationSpriteHubService.cs
-->

# Camera Capture / Shared Texture Channel 設計計画

## 1. 目的

現在の `ScreenFx` は、

- カメラに映っている画像を取得する
- その画像をブラーなどで加工する
- 加工後の画像を Sprite / Material / Camera 側で使う

という複数の責務を一つに抱えています。

今後やりたい表現は、少なくとも次を含みます。

- カメラの現在画像を写真のように Sprite へ貼る
- カメラ全体にブラー / モザイク / 歪みをかける
- 特定の Sprite が映っている範囲だけにカメラ側エフェクトをかける
- 加工結果を別システムから再利用する
- WebGL を含む複数プラットフォームで、品質段階を落としながら動かす

このため、`ScreenFx` は今後「1機能」ではなく、以下の 4 レイヤへ分離する。

1. Capture Layer
2. Shared Texture Channel Layer
3. Effect Process Layer
4. Consumer Layer


## 2. 現行コードを読んだ上での評価

### 2.1 良い点

- `ScreenFxBufferFeature` は Camera の描画結果を RT 化する発想として正しい。
- `ScreenFxBinderService` は MaterialFx へ Texture を流し込む役割として正しい。
- BaseShader には既に `External Texture A / B` と `CustomRT` があり、外部 Texture を受ける基盤がある。
- `CameraSystem` は `MB -> Service` 分離ができており、LTS ごとのサービス構成に合っている。
- `MeshFxChannelHubService` / `AnimationSpriteHubService` には、タグベース Hub + Player の設計パターンが存在する。

### 2.2 問題点

- 現 `ScreenFx` は `Capture` と `Blur生成` と `Material配布` が密結合。
- `ScreenFx2D.hlsl` は Mode を持つが実装はほぼ Blur のみで、将来の Distort / Glass / Shatter を 1 ファイルに押し込みやすい。
- `CameraSystemService` は内部に `Camera` を持つが、外部へ Camera を公開していない。
- 共有手段が `Shader Global Texture` と `Binder` に寄っており、多カメラ・多段加工・再利用に不向き。
- `ScreenFx` 専用プロパティを増やすと、BaseShader 側が肥大化しやすい。


## 3. 結論

### 3.1 中核に置くべきもの

中核は `ScreenFx` ではなく、**Shared Texture Channel Hub** とする。

Camera はそこへ `source` を送るだけにする。  
Effect Processor は Hub から入力 Texture を受け、別チャネルへ加工結果を書き戻す。  
BaseShader / PostProcess / PhotoSprite などは、Hub から必要なチャネルを読む「利用者」とする。

### 3.2 BaseShader の立ち位置

BaseShader は **末端の利用者** とし、共有システムの中核にはしない。

理由:

- BaseShader は Texture の保存場所ではない
- 多カメラ / 多チャネル管理に向かない
- PostProcess や CPU 側の別システムから扱いづらい

そのため、

- 共有の正本は Service 側に置く
- BaseShader には既存の `_ExtTexA` `_ExtTexB` `_CustomRT` を使って流す
- `ScreenFx` 専用の新規固定変数はできるだけ増やさない

という方針を推奨する。

`ScreenFx` 系の専用変数は段階的に縮小し、将来的には「汎用共有 Texture を合成する 1 利用例」に寄せる。


## 4. レイヤ分離

### 4.1 Capture Layer

責務:

- 特定 Camera の描画結果を取得する
- 必要な解像度 / Format / Filter を決める
- Shared Texture Channel Hub に `source` を発行する

置き場所:

- `CameraSystemMB` と同じ LTS

理由:

- どの Camera を扱うかはその LTS の CameraSystem に従うべき
- 同一 Camera に紐づく Zoom / PostProcess / Capture を同じスコープで管理できる

### 4.1.1 推奨クラス

- `CameraCaptureMB`
- `CameraCaptureService`
- `ICameraCaptureService`
- `ICameraRenderContext`

### 4.1.2 重要判断

`CameraCaptureMB` は Camera を自前 SerializeField で持たず、**CameraSystemMB が持つ Camera を受け取る**。

ただし現状の `CameraSystemService` / `ICameraSystemService` は `Camera` を外へ出していないため、以下のどちらかを追加する。

1. `CameraSystemMB` で `Camera` を `RegisterInstance(camera)` する  
2. `ICameraRenderContext` のような専用 interface を追加し、その中に `Camera` を保持する

推奨は 2。

理由:

- 単なる `Camera` 生登録だと、同一スコープに複数 Camera が来たとき意味が曖昧
- `Transform` や `Volume` など将来の描画文脈も同時に載せやすい
- Capture 以外の描画系も同じ文脈を参照できる


### 4.2 Shared Texture Channel Layer

責務:

- Texture / RTHandle をタグ単位で共有する
- どのシステムがどのチャネルを publish したかを管理する
- Texture の解像度・Format・寿命を管理する
- 利用者へ読み取り API を提供する

置き場所:

- `SceneLTS` 推奨
- Scene をまたいで持ち回る必要があるなら `GlobalLTS`

推奨方針:

- 基本は `SceneLTS`
- グローバルに残す必然があるものだけ `GlobalLTS`

理由:

- Camera 画像は基本的に Scene 単位の資産
- シーン切り替え時に古い RT を引きずりにくい
- メモリリークや不要保持を避けやすい

### 4.2.1 推奨クラス

- `SharedTextureChannelHubMB`
- `SharedTextureChannelHubService`
- `ISharedTextureChannelHub`
- `SharedTextureChannelTag`
- `SharedTextureDescriptor`
- `SharedTextureFrame`

### 4.2.2 データ構造案

#### SharedTextureChannelTag

- `string Tag`
- 命名例:
  - `camera/main/source`
  - `camera/main/source.low`
  - `camera/main/blur.low`
  - `camera/main/warp.low`
  - `camera/main/final`
  - `camera/main/mask/photo`

#### SharedTextureDescriptor

- `int Width`
- `int Height`
- `GraphicsFormat Format`
- `FilterMode FilterMode`
- `TextureWrapMode WrapMode`
- `int MsaaSamples`
- `bool UseDynamicScale`
- `bool Persistent`

#### SharedTextureSourceKind

`SharedTextureFrame` の生成元種別を表す。

- `Unknown = 0`
- `CameraCapture = 10`
- `ProcessorOutput = 20`
- `ImportedTexture = 30`
- `ExternalTexture = 40`

#### SharedTextureCameraCaptureInfo

Camera 由来のチャネルにだけ付与されるメタデータ。

- `Camera CaptureCamera`
- `Matrix4x4 ViewMatrix`
- `Matrix4x4 ProjectionMatrix`
- `Matrix4x4 ViewProjectionMatrix`
- `Rect PixelRect`
- `bool IsOrthographic`
- `float OrthographicSize`
- `float Aspect`
- `int PixelWidth`
- `int PixelHeight`

重要:

- `Camera` の参照だけでは不十分
- キャプチャ後に Camera が動くと、後段 Processor で位置がずれる可能性がある
- そのため、**キャプチャ時点の Camera 状態を snapshot として保持する**

#### SharedTextureFrame

- `Texture Texture`
- `RenderTextureDescriptor Descriptor`
- `int FrameId`
- `string ProducerTag`
- `SharedTextureSourceKind SourceKind`
- `SharedTextureCameraCaptureInfo? CameraCapture`
- `int Width` — convenience accessor（`Descriptor.width` から取得する想定）
- `int Height` — convenience accessor（`Descriptor.height` から取得する想定）

注意:

- `Width` / `Height` は `RenderTextureDescriptor` にも含まれるため二重管理のリスクがある
- 実装時は Descriptor を正本とし、`Width` / `Height` は computed property として提供するのが望ましい

重要:

- Hub の責務は「保存場所」であり、加工ロジックは持たない
- 読み取りは snapshot ベースにし、利用側が Hub 内部辞書を直接触らない
- ただし Camera 由来チャネルについては、**後段で Screen-space 処理を行うためのメタデータを保持する**

#### SharedTexturePublishOptions

Publish 時に渡す生成元メタデータ。

- `string ProducerTag`
- `SharedTextureSourceKind SourceKind`
- `bool IsCameraCapture`
- `Camera? CaptureCamera`

初期方針:

- 内部表現は `SourceKind` を正本とする
- MB / Inspector では開発者が分かりやすいように `IsCameraCapture` の bool を持ってよい
- `IsCameraCapture = true` のとき、Service 側で `SourceKind = CameraCapture` と `SharedTextureCameraCaptureInfo` を生成する

### 4.2.3 API 方針

例:

- `bool Publish(string tag, Texture texture, in SharedTextureDescriptor descriptor, in SharedTexturePublishOptions options)`
- `bool TryGet(string tag, out SharedTextureFrame frame)`
- `bool Contains(string tag)`
- `bool Remove(string tag, string producerTag)`
- `void ClearByProducer(string producerTag)`
- `void ClearAll()`

必要に応じて後から追加:

- `bool AcquireWritable(string tag, in SharedTextureDescriptor descriptor, out RTHandle handle)`
- `bool TryGetWritable(string tag, out RTHandle handle)`

初期段階では、Hub 自体に高度な RenderGraph 的責務は持たせず、**タグ付き Texture 共有に徹する**。

#### タグ命名規約

タグ文字列は以下のルールに従う。

- 使用可能文字: 小文字英数字 `a-z0-9`、スラッシュ `/`、ドット `.`、ハイフン `-`
- 先頭・末尾にスラッシュやドットを置かない
- 空文字列は禁止
- 例: `camera/main/source`, `camera/main/blur.low`, `noise/common/base`

Hub は publish 時にタグを正規化・バリデーションし、不正タグは拒否する。

追加方針:

- Screen-space Mask 系 Processor は、`TryGet()` で取った `SharedTextureFrame` から Camera 情報を復元する
- Request 側は Camera を明示指定しない
- `SourceKind != CameraCapture` のチャネルに対しては、Camera 範囲マスク系の effect request を拒否または no-op にする

### 4.2.4 SharedTexture のパフォーマンスについて

`SharedTexture` 自体の受け渡しで CPU が大きく重くなるかというと、**実装を正しく限定すれば大きな問題にはなりにくい**。

理由:

- Hub が持つのは基本的に `Texture / RTHandle の参照` と metadata
- publish / read 自体は辞書参照と状態更新が中心
- GPU 側の画像データを CPU が毎回コピーする前提ではない

つまり、`SharedTexture` のコストは「画像転送」ではなく、主に次に出る。

- タグ検索
- 状態更新
- RT の確保 / 再確保
- effect pass の追加 blit
- mask 生成 pass の追加描画

重要:

- `Texture2D.ReadPixels`
- `AsyncGPUReadback`
- `Texture2D.Apply`
- 毎フレームの `Texture2D` upload

のような **GPU -> CPU -> GPU の往復**をしなければ、SharedTexture は十分現実的である。

推奨ルール:

1. SharedTexture は GPU リソース参照の共有に徹する
2. CPU は pixel data を読まない
3. RTHandle / RenderTexture は再利用する
4. publish 時に毎回新規 allocation しない
5. 文字列タグの churn を抑え、固定 tag を前提にする

結論:

- CPU コストの本体は SharedTexture そのものより、**effect の数・mask 描画回数・RT 再確保回数**
- SharedTexture 自体は「参照共有」に徹すれば許容範囲
- 本当に注意すべきは readback と upload を混ぜないこと

### 4.2.5 エラー・障害ハンドリング方針

以下のケースについて方針を定める。

#### 同一タグへの複数 Producer publish

- 後勝ち（上書き）を基本とする
- ただし `ProducerTag` が異なる場合は Warning ログを出す
- 意図的な上書きは同一 `ProducerTag` で行う前提

#### Consumer が未 publish のタグを TryGet した場合

- `false` を返却し、ログは出さない（毎フレーム呼ばれうるため）
- Debug ビルドでは初回のみ Verbose ログを出してもよい

#### RT 確保失敗時

- `Publish` が `false` を返す
- Consumer 側は Texture 不在として正常系で扱う
- WebGL では特にメモリ制約が厳しいため、Descriptor の downsample 方針と組み合わせて対策する

#### Camera 破棄時

- `CameraCaptureService` の `IScopeReleaseHandler` で `ClearByProducer` を呼び、Hub 側を確実にクリーンアップする
- Hub 側は producer 単位の一括削除を保証する

### 4.2.6 フレームタイミング方針

Capture と消費のタイミングについて。

- Capture は Render Pipeline 内（`RenderPassEvent` ベース）で行われる
- Consumer の読み取りは次フレームの `Update()` 以降になる

したがって、初期実装では **1 フレーム遅延を許容する**。

理由:

- 同一フレーム内の同期は Render Pipeline と MonoBehaviour の実行順に強く依存する
- 1 フレーム遅延は視覚的にほぼ気づかない
- 将来、同一フレーム同期が必要になった場合は `RenderPipelineManager.endCameraRendering` コールバック等で対処できる


### 4.3 Effect Process Layer

責務:

- 入力チャネルから Texture を読む
- Blur / Mosaic / Distort / Warp / ColorShift などを処理する
- 出力チャネルへ書き戻す

置き場所:

- `SceneLTS`
- または該当 Camera と強く紐づくなら Camera LTS

推奨:

- まずは `SceneLTS`

理由:

- Processor が複数 Camera / 複数入力を扱う余地を残せる
- Capture と加工を薄く接続できる
- Layer / Channel ベースの責務分離に合う

### 4.3.1 推奨クラス

- `TextureEffectPipelineMB`
- `TextureEffectPipelineService`
- `ITextureEffectPipeline`
- `ITextureEffectLayerRegistry`
- `ITextureEffectMaskRegistry`
- `TextureEffectLayerDef`
- `TextureEffectMaskEntry`

### 4.3.2 レイヤ設計

Processor は、チャネルごとの単一処理ではなく、**Layer を順に適用するパイプライン**として持つ。

ただし、ここで管理するものは 2 種類に分離する。

1. Layer の加工命令データ
2. Layer に属する Mask 登録データ

例:

1. Input: `camera/main/source`
2. Layer: `blur.low`
3. Layer: `warp.heat`
4. Layer: `mosaic.enemy`
5. Output: `camera/main/final`

ここでいう Layer は既存の `LayeredFloat` や `CameraPostProcessService` と同じく、**tag と順序を持つ上書き可能な薄い単位**にする。

#### Layer 命令データに必要な情報

- `string LayerTag`
- `int Order`
- `string InputTag`
- `string OutputTag`
- `TextureEffectKind`
- `bool Enabled`
- `float ResolutionScale`
- `Material / ComputeShader / Params`

#### Mask 登録データに必要な情報

- `string LayerTag`
- `int RegistrationId`
- `Renderer / SpriteRenderer`
- `MaskShapeKind`
- `bool Enabled`
- `Mask 用追加パラメータ`

重要:

- Layer 命令データは「何をするか」
- Mask 登録データは「どこに効かせるか」

を表す。

Processor は 1 Layer を処理するとき、

1. その Layer の命令データを取得する
2. 同じ `LayerTag` で登録された複数の Mask 情報を集める
3. それらを合算して 1 枚の Mask RT を作る
4. その Mask 範囲に対して Layer の加工を 1 回適用する

という流れにする。

これにより、同じ加工命令に対して対象者が複数いても、effect 本体の blit は Layer 単位でまとめられる。

### 4.3.3 Effect の種類と作り方

Effect は「対象者が effect を直接指定して実行する」のではなく、**Layer 命令データを定義し、その LayerTag に対して対象者が Mask 登録する**形にする。

#### 推奨 Effect 種別

`TextureEffectKind` の初期候補は次の通り。

- `None = 0`
- `Blur = 10`
- `Mosaic = 20`
- `Distort = 30`
- `Refraction = 40`
- `Ripple = 50`
- `ColorShift = 60`
- `Posterize = 70`
- `Shatter = 80`

理由:

- 画面キャプチャとの相性がよい
- Mask 範囲加工の価値が高い
- WebGL でも比較的構築しやすい

#### 各 Effect をどう加工するか

##### Blur

- 入力 Texture を downsample
- 横 blur / 縦 blur の 2 pass で ping-pong
- 合算済み Mask を使って元画像と blur 結果を合成

##### Mosaic

- 1 pass の fullscreen blit で block 単位の UV 量子化を行う
- 必要なら color step 量子化も加える
- 合算済み Mask 範囲だけ置き換える

##### Distort

- 別のノイズ / flow / ベクトル Texture を参照して UV をずらす
- ずらした UV で source を再サンプルする
- 合算済み Mask 範囲だけ反映する

##### Refraction

- Distort の一種だが、法線や勾配ベースの offset を使う
- 必要なら RGB ごとにサンプル位置をずらして色収差を加える
- ガラス系表現用として扱う

##### Ripple

- 波紋中心、半径、時間パラメータから offset を計算する
- 直接 UV をずらすか、事前に ripple field を作って参照する
- 局所イベントやヒット表現向け

##### ColorShift

- Hue / Saturation / RGB offset / 乗算色 などを 1 pass で適用する
- Blur や Distort と違い中間 RT を増やさずに済むことが多い

##### Posterize

- 色段数を減らす
- Mosaic と組み合わせると retro 表現に向く

##### Shatter

- 破片分割、ノイズセル、UV 分離などが必要
- 初期実装対象ではなく、後段で追加する

例:

- Layer定義 `blur.enemy`
  - `InputTag = camera/main/source`
  - `OutputTag = camera/main/final`
  - `EffectKind = Blur`
  - `ResolutionScale = 0.5`

- Mask登録 A
  - `LayerTag = blur.enemy`
  - `MaskRenderer = EnemyA.SpriteRenderer`

- Mask登録 B
  - `LayerTag = blur.enemy`
  - `MaskRenderer = EnemyB.SpriteRenderer`

Processor は `blur.enemy` に属する A/B etc... の Mask を合算し、1 つの blur 処理として流す。

これが今後の Effect の基本形になる。

#### 実際の加工手順

1. `TextureEffectLayerDef` から `InputTag` を解決して入力 Texture を取る
2. 同じ `LayerTag` に属する `TextureEffectMaskEntry` をすべて集める
3. それらを描いて 1 枚の合算 Mask RT を作る
4. `TextureEffectKind` に応じた Material / Pass を実行する
5. 出力結果を `OutputTag` に publish するか、直後の Layer 入力へ渡す
6. 最後の Layer 結果を `camera/main/final` などへ流す

重要:

- Effect 本体は Layer 単位で 1 回だけ走る
- 対象者が増えても、増えるのは Mask 合成コストであって、effect blit 回数ではない
- これが Layer 命令と Mask 登録を分離する主な利点である

##### 各 Effect の pass 数目安

- `Blur`: 2 pass 以上
- `Mosaic`: 1 pass
- `Distort`: 1 pass
- `Refraction`: 1-2 pass
- `Ripple`: 1 pass
- `ColorShift`: 1 pass
- `Posterize`: 1 pass

##### どのように作るか

基本的には、各 Effect は **SharedTexture を入力に取る Material ベースの blit 処理**として作る。

つまり実装単位は次になる。

- `TextureEffectMaterial`
- `TextureEffectPass`
- `TextureEffectLayerDef`

`LayerDef` が

- 何を入力に取るか
- どの Material を使うか
- どこへ出力するか
- 解像度をどう落とすか

を決め、Processor がそれを実行する。

### 4.3.4 Effect の登録元

Effect の登録元は `SpriteAnimationChannelPlayer` に限定しない。

`SpriteAnimationChannelPlayer` は将来の request producer の一つにはなれるが、基盤の中心に置くべきではない。

理由:

- Camera 側効果の要求元は Sprite 以外にも増える
- UI / Trigger / Command / Scene Event / Gameplay Rule も要求元になる
- AnimationSprite 依存にすると設計が不必要に狭くなる

したがって、基盤としては次を用意する。

- `ITextureEffectLayerRegistry`
- `RegisterLayer(in TextureEffectLayerDef layer)`
- `UpdateLayer(...)`
- `UnregisterLayer(layerTag)`
- `ITextureEffectMaskRegistry`
- `RegisterMask(in TextureEffectMaskEntry entry)`
- `UpdateMask(...)`
- `UnregisterMask(registrationId)`

`AnimationSpriteChannelPlayer` は将来この registry へ「Mask登録を送る利用者」として接続する。


### 4.4 Consumer Layer

責務:

- Hub のチャネルを読み、末端で使う

利用先:

- BaseShader
- MaterialFx consumer への外部 Texture 供給
- CameraSystem による最終出力差し替え
- Debug 表示

ここでいう `MaterialFx consumer` には、少なくとも次を含む。

- `AnimationSpriteChannelPlayer` 経由の `SpriteRenderer`
- `AnimationSpriteChannelPlayer` 経由の `UI Image`

重要:

- `SpriteRenderer` と `UI Image` は別系統の consumer として扱わない
- 実際にはどちらも `MaterialFx` へ Texture を流し、その結果として描画される
- したがって SharedTexture から見ると、両者は同じ bind 経路を取る

### 4.4.1 BaseShader との接続

BaseShader は既存の外部 Texture スロットを使う。

- `_ExtTexA`
- `_ExtTexB`
- `_CustomRT`

理由:

- 既存 `TextureSlot2D.hlsl` と整合する
- MaterialFxKeys に既にキーがある
- TransitionController でも `_ExtTexA` 利用実績がある

推奨は、共通の binder を後で作ること。

例:

- `SharedTextureMaterialBinderMB`
- `SharedTextureMaterialBinderService`

役割:

- Hub の `camera/main/source` を `_ExtTexA` に流す
- Hub の `camera/main/blur.low` を `_ExtTexB` に流す
- Hub の `camera/main/final` を `_CustomRT` に流す

これにより BaseShader は `ScreenFx` 専用の固定変数を増やさずに利用できる。

また、ここでいう利用は「SpriteRenderer に直接 Texture を貼る」ことではない。  
正確には、`SharedTextureMaterialBinderService` が `MaterialFx` に Texture を渡し、
その `MaterialFx` を使っている `AnimationSprite` 系の `SpriteRenderer / UI Image` が同じ仕組みで描画する。

ただし、実装は「任意 Renderer を総当たりで探す」形ではなく、**Channel 側が持つ MaterialFx を tag で受け取る**形にする。

理由:

- 実際に MaterialFx を最も多く使っているのは `AnimationSpriteChannel`
- `AnimationSpriteChannelPlayer` はすでに `IMaterialFxReceiver` を実装している
- SharedTexture の受け取り先は「どの Player か」が重要
- 利用者側を tag 指定で薄くつなげる方が既存 Channel 設計に合う

### 4.4.2 AnimationSpriteHubService から MaterialFx を受け取る interface

`SharedTextureMaterialBinderService` は、`AnimationSpriteHubService` から MaterialFx を tag 指定で受け取る。

このため、`AnimationSpriteHubService` に次の interface を実装させる案を推奨する。

#### 推奨 interface 名

- `ITaggedMaterialFxProvider`

#### 推奨 API

- `bool TryGetMaterialFxReceiver(string tag, out IMaterialFxReceiver receiver)`
- `bool TryGetMaterialFx(string tag, out IMaterialFxService materialFx)`

方針:

- 正本は `IMaterialFxReceiver`
- `TryGetMaterialFx` は binder 向けの便宜 API
- `AnimationSpriteChannelPlayer` は既に `IMaterialFxReceiver` 実装済みのため、Hub 側は tag から Player を引けばよい

この interface は今後、tag ベースで Player を持つ他 Channel Hub へも横展開しやすい。

#### SharedTextureMaterialBinder の binding 定義案

- `string TargetPlayerTag`
- `string SharedTextureTag`
- `SharedTextureBindSlot BindSlot`
- `string ContextTag`
- `int Priority`
- `bool ClearWhenMissing`

`SharedTextureBindSlot` の初期値案:

- `ExternalA = 10`
- `ExternalB = 20`
- `CustomRT = 30`

`SharedTextureMaterialBinderService` は以下を行う。

1. `ITaggedMaterialFxProvider` から `TargetPlayerTag` の MaterialFx を取得する
2. `ISharedTextureChannelHub` から `SharedTextureTag` の Texture を取得する
3. `BindSlot` に応じた MaterialFx キーへ Texture を流す
4. Texture 不在時は `ClearWhenMissing` に応じて null または context clear を行う

#### 実際に流すべき MaterialFx キー

- `ExternalA` -> `MaterialFxKeys.BaseShader.ExternalTextures.ExtTexA`
- `ExternalB` -> `MaterialFxKeys.BaseShader.ExternalTextures.ExtTexB`
- `CustomRT` -> 現状はキー体系の見直しが必要

重要:

- CompositeSystem に「どのソーススロットを使うか」を教えるキーと
- 実際の Texture オブジェクトを `_ExtTexA/_ExtTexB/_CustomRT` に流すキーは

責務が別である。

つまり、

- Binder は **Texture オブジェクトを流す**
- BaseShader / MaterialFx preset は **その Texture をどの CompositeSystem が使うかを指定する**

の 2 段構成にする。

### 4.4.3 MaterialFx 側の不足と必要な改修

キャプチャーした画像を「そのまま描写する」「自由に有効/無効を切り替える」用途は、現 MaterialFx では弱い。

現状コードから見えること:

- `TextureSlotType` は `ExternalA = 5`, `ExternalB = 6`, `CustomRT = 7` を持っている
- `TextureSlot2D.hlsl` も `_ExtTexA/_ExtTexB/_CustomRT` をサンプルできる
- `BaseShaderFxPresetSO` でも SlotType 5/6/7 を選べる

つまり、**シェーダー側と preset 側は外部 Texture を使える**。

一方で問題は、**C# / MaterialFx キー体系が整理されていない**ことにある。

代表的な問題:

- `BaseShader/ExternalTextures/ExtTexA` と `BaseShader/TextureSlot/ExtTexA` が同じ `_ExtTexA` を指しており、責務が混線している
- `TextureSlot.CustomRT` が「Texture の実体」なのか「Slot 指定」なのかが曖昧
- `_CustomRT` に対する正規の `ExternalTextures.CustomRT` 系キーが不足している
- Atlas Slot / Kernel / Noise 前提の設計が強く、外部 Texture を第一級の入力として扱っていない

結論:

「外部 Texture が使えない」のではなく、**使う経路が混線していて、SharedTexture を主役に据えるには再設計が必要**というのが正確。

#### 必要な改修

1. Texture 実体の受け口と、CompositeSystem の Slot 指定を分離する
2. `BaseShader.ExternalTextures.CustomRT` 相当の正規キーを追加する
3. Binder が使うキー群を `External Texture` 系として一本化する
4. CompositeSystem は `SlotType = ExternalA / ExternalB / CustomRT` を素直に選ぶだけにする

#### 将来必要な BaseShader / MaterialFx 機能

今回は実装しないが、今後は「受け取った Texture をそのまま描写する」ための専用機能を MaterialFx 側へ追加するのがよい。

例:

- `SharedTextureDisplay2D`
- `ExternalTextureComposite2D`

最低限ほしいパラメータ:

- `Enabled`
- `SourceSlot`
- `BlendMode`
- `Intensity`
- `UseTextureAlpha`
- `Tint`
- `DisableWhenTextureMissing`

これがあると、撮影画像を `MaterialFx consumer` 上で直接表示する用途を `ScreenFx` なしで扱える。

このときも、`SpriteRenderer` と `UI Image` は別仕様ではなく、
`AnimationSpriteChannelPlayer -> MaterialFx -> 描画対象` の同一路線で扱う。

### 4.4.4 Noise / Kernel / WebGL についての評価

> 本セクションは Consumer Layer 固有の話ではなく、アーキテクチャ全体の将来方針に関わる内容を含む。将来的に独立セクションへ昇格する可能性がある。

現状の Kernel/Noise 系は、実質的に **ComputeShader 前提**で構築されている。

コード上の事実:

- `MaterialFxMB` は WebGL 時に `IMaterialFxKernelService` を `NullMaterialFxKernelService` に置き換えている
- `Kernel2D.hlsl` は Compute 出力前提
- `NoiseAtlasHub` は Tier 固定解像度の `Texture2DArray` 管理
- `NoiseAtlas2D.hlsl` も WebGL safe mode では atlas sampling を簡易値へ落としている

したがって、現在の Runtime Noise 生成は **WebGL ではそのままでは使えない**。

ここで重要なのは、今後の SharedTexture 系と Noise 系を同一パイプラインとして考えすぎないこと。

推奨方針:

- SharedTexture 系は RenderTexture / Camera capture / blit ベースで進める
- Noise 系は別ラインで WebGL 対応を考える

### 4.4.4.1 将来方針: ノイズの受け渡し面は SharedTexture に統一する

将来的には、ノイズ生成ロジックの最終出力も `SharedTexture` に統一する。

つまり、生成手段が何であっても外部から見える流れは次に揃える。

1. ノイズ生成
2. SharedTexture へ publish
3. Consumer が SharedTexture から受け取る

このとき、生成手段の候補は複数あってよい。

- 事前生成ノイズ Texture
- Fragment Shader で生成したノイズ RT
- 将来必要なら CPU 生成 Texture
- 既存 Compute ベースの過渡実装

重要なのは、**Consumer が生成手段を意識しないこと**である。

つまり Consumer から見える契約は、

- `noise/common/base`
- `noise/common/detail`
- `noise/common/distortion`

のような SharedTexture tag を受け取ることだけにする。

これにより、

- 事前生成ノイズ
- Fragment Shader ノイズ
- 将来の別実装

を同じ利用コードで扱える。

### 4.4.4.2 Compute 依存の最終撤去方針

長期的には、ノイズ生成から **Compute 依存を完全に削除する** 方針でよい。

理由:

- WebGL で Compute が使えない
- SharedTexture があれば、生成手段に依存しない受け渡しができる
- 現在の Tier / Slice / Atlas 固定解像度設計に引っ張られずに済む

つまり将来的な理想形は、

- ノイズ生成は Fragment Shader または事前生成 Texture を使う
- 生成結果は SharedTexture に publish する
- Consumer は SharedTexture から読む

という構造になる。

この構造なら、最終的には「ノイズ画像を外部 Texture として使う」ことと
「カメラ capture 画像を外部 Texture として使う」ことが同じパイプラインに揃う。

### 4.4.4.3 実装上の意味

この方針を採る場合、ノイズ系は `NoiseAtlasHub` 中心ではなく、将来的には次のように整理される。

- `NoiseProducerService`
- `NoiseRenderPass` または `NoiseBlitPass`
- `SharedTextureChannelHubService`

役割:

- Producer がノイズを生成する
- 生成結果を SharedTexture の tag へ publish する
- Consumer は SharedTextureBinder や Camera Processor から受け取る

この形にすると、ノイズもカメラ画像も「SharedTexture の一種」になるため、
MaterialFx / Camera / UI / Debug の利用経路を揃えられる。

WebGL 向けの現実的代替案:

1. 事前生成ノイズ Texture を使う
2. Fragment Shader で軽量な手続きノイズを直接計算する
3. CPU 生成ノイズを低頻度更新で Texture2D に書く
4. Fullscreen blit マテリアルでノイズ RT を生成する

推奨順は 1 -> 2 -> 4。  
3 は CPU コストと upload コストが重くなりやすい。

結論:

- WebGL を重視するなら、Noise は「Compute 依存の Atlas システム」から切り離して再設計するべき
- SharedTexture 系の実装判断を、現 NoiseAtlas の都合に引っ張らない方がよい
- ノイズの最終出力面は SharedTexture に統一し、Consumer は生成手段ではなく SharedTexture tag を見るべき

### 4.4.5 CameraSystem による出力差し替え

ここで欲しいのは「PostProcess」より、**Camera の最終出力をどの Texture で置き換えるか**の制御である。

そのため、これは `CameraSystem` が持つのが適切。

推奨クラス:

- `CameraOutputOverrideService`
- `ICameraOutputOverrideService`

推奨 enum:

- `None = 0`
- `SharedTexture = 10`

推奨設定:

- `bool Enabled`
- `CameraOutputOverrideMode Mode`
- `string SharedTextureTag`

責務:

- CameraSystem が「最終出力を override するか」を判断する
- `Mode == SharedTexture` なら `ISharedTextureChannelHub` から指定 tag の Texture を取得する
- RendererFeature / Pass へ「この Texture で camera color を置き換えろ」と渡す

重要:

- effect 処理そのものは別サービスが行う
- CameraSystem は最終出力の採用だけ行う
- つまり、ここは「ポストエフェクトを作る場所」ではなく「出力の選択場所」である

この構造にすると、

- Processor が `camera/main/final` を生成する
- CameraSystem が `camera/main/final` を出力に採用する

という役割分離になる


## 5. 特定 Sprite の範囲だけ Camera 側加工を入れる件

> 本セクションは Section 4.3 の Mask 設計をさらに詳細化したものである。Layer 命令と Mask 登録の基本設計は 4.3.2 を参照のこと。

これは可能だが、設計上は **Sprite 自体の加工** ではなく **Screen-space Masked Camera Effect** として扱うべき。

### 5.1 正しい責務

必要なのは、

- 対象 Sprite が画面上のどこに映っているか
- その範囲へどの加工を入れるか

の 2 点であり、Sprite の Material 自体を直接いじることではない。

### 5.2 推奨構造

以下の 2 段構成にする。

1. Request Registry
2. Mask / Region Processor

#### Request Registry

登録内容:

- `string InputChannelTag`
- 対象 Renderer / SpriteRenderer
- LayerTag
- Mask の作り方

重要:

- Request 側は Camera を指定しない
- Request 側は effect 内容自体を持たない
- **入力チャネルが持つ `SharedTextureFrame.CameraCapture` を Processor 側で参照する**
- これにより、登録者が毎回 Camera を解決しなくてよい
- どの Camera に対する処理かは、`InputChannelTag` とそのチャネル metadata で決まる
- 何をするかは `LayerTag` に対応する Layer 命令定義が別途持つ

#### Mask / Region Processor

役割:

- 登録された Renderer 群を元に、専用 Mask RT を生成する
- 同じ LayerTag に属する複数 Mask を合算する
- その Mask を使って Camera Texture の一部だけ処理する

推奨フロー:

1. `InputChannelTag` から `SharedTextureFrame` を取得する
2. `frame.SourceKind == CameraCapture` を確認する
3. `frame.CameraCapture` から capture 時 Camera 情報を得る
4. 同じ `LayerTag` に登録された複数 Renderer を集める
5. それらを、その Camera 情報を使って Mask RT へ描く
6. 合算済み Mask を effect pass へ渡す

### 5.3 重要判断

この「Sprite 範囲だけ Camera 効果」は、単純な `Bounds` ベースでは精度が足りない。

本命は以下:

- Renderer 形状をマスク RT に描く
- そのマスクを加工パスで参照する

これにより、少なくとも次を正しく反映できる。

- 位置
- 回転
- スケール
- Sprite の見た目の形
- Pivot / Flip
- Camera に対する実際の投影位置
- 画面外にはみ出した部分のクリップ
- カメラに映っていない部分の除外
- 他オブジェクトに隠れて見えていない部分の除外

つまり、`SpriteRenderer` を mask source として使う場合は、単に矩形を送るのではなく、**実 Renderer を capture 時 Camera の文脈で描画して Mask を作る**のが正しい。

### 5.3.1 Mask は「対象の全体」ではなく「最終的に見えている部分」を使う

ここで使う Mask は、対象 Renderer のローカル形状そのものではなく、**capture camera から見た最終可視部分**でなければならない。

具体的には、次のケースで隠れた部分を mask に含めない。

- 他オブジェクトの後ろにあり、前面オブジェクトに隠れている
- 画面外にはみ出している
- camera pixel rect の外に出ている
- clipping / stencil / depth / sorting の結果として最終表示されていない

つまり、

- 「対象の Sprite がどこにあるか」だけでは不十分
- 「capture 時の camera で、どの fragment が最終的に見えているか」が必要

### 5.3.2 推奨実装方針

精度を優先するなら、Mask は **最終可視 fragment を使って生成する専用 pass** として作る。

推奨方針:

1. capture camera と同じ projection / viewport / clipping 条件を使う
2. mask 対象 Renderer を同じ描画順の文脈で描く
3. depth / stencil / clipping / sorting を反映した後の surviving fragment だけを mask に書く

これにより、

- 部分的に隠れた Sprite は見えている部分だけ mask 化される
- 画面外部分は自動的に切り落とされる

### 5.3.3 実装レベルでの考え方

実装としては、単に `Renderer.bounds` や `screen rect` を使うのではなく、次のどちらかに寄せる。

#### 推奨案: 可視 fragment ベースの mask pass

- camera capture と同じ文脈の render pass を用意する
- register 済み対象だけを mask RT へ描く
- そのとき実際の深度 / クリップ / viewport 条件を反映する

#### 簡易案: Renderer 単体描画ベース

- 対象 Renderer だけを camera 空間で mask RT へ描く
- 画面外クリップは取れる
- ただし他オブジェクトによる隠れは正確に取れない

この仕様では、**最終目標は推奨案**とする。

簡易案は初期実装として許容してもよいが、最終仕様としては不足とみなす。

### 5.4 Camera 情報を Request 側へ持たせない理由

Request 側が毎回 Camera を指定すると、以下の問題が出る。

- 登録 API が重くなる
- 間違った Camera を指定しやすい
- 同じ effect を別 Camera へ流すときに管理が煩雑になる
- 入力元 Texture と Camera が不整合でもコンパイル時に防げない

そのため、Camera は request ではなく **入力チャネル metadata の責務**に置く。

方針:

- Camera が生成したチャネルは、自分が Camera 由来であることを publish 時に宣言する
- Hub はそのチャネルに Camera Capture metadata を保持する
- Request は `InputChannelTag` のみ指定し、Processor が必要 Camera をそこから解決する

### 5.5 登録命令側の選択

実際に `SharedTextureChannelHub` へ publish する producer は、**自分が Camera Capture かどうかを選択して登録する**。

初期実装では次の形がよい。

- `CameraCaptureMB`: `IsCameraCapture = true` 固定
- 将来の Processor 出力: `IsCameraCapture = false`
- 外部 Texture 差し込み: `IsCameraCapture = false`

内部的には次に変換する。

- `IsCameraCapture = true` -> `SourceKind = CameraCapture`
- `IsCameraCapture = false` -> `SourceKind = ProcessorOutput` または他種別

これにより、チャネルごとに「この Texture は Camera から来たものか」を確実に判定できる。

つまり将来的には、

- `camera/main/source`
- `camera/main/mask/effectA`
- `camera/main/effect/effectA`

のようなチャネル構造になる。

`SpriteAnimationChannelPlayer` が request producer になるのは良いが、最初からそれ専用にはしない。


## 6. Shader Global Texture を共有の正本にしない理由

`cmd.SetGlobalTexture()` は RendererFeature 内の一時的受け渡しには便利だが、システム全体の共有基盤には向かない。

理由:

- 多カメラ時に衝突しやすい
- 誰がいつ上書きしたか追いにくい
- CPU 側サービスで状態として扱いづらい
- 加工段階が増えるほど命名競合しやすい
- WebGL / 低スペック環境で切り替え制御が見えづらい

結論:

- `GlobalTexture` はレンダーパス内部の補助用途まで
- システム全体の正本は `SharedTextureChannelHubService`


## 7. サイズと品質段階

Camera 画像は固定サイズ前提ではなく、**Descriptor ベース**で扱う。

理由:

- 端末解像度差
- WebGL のメモリ制約
- effect ごとの downsample
- source / blur / mask で必要サイズが違う

### 7.1 品質段階案

- `Full`
- `Half`
- `Quarter`

または float で

- `1.0`
- `0.5`
- `0.25`

チャネルタグに解像度を埋める案:

- `camera/main/source`
- `camera/main/source.half`
- `camera/main/blur.quarter`

初期段階では、Hub 側は単純に descriptor を保持し、生成側がサイズ方針を決める。


## 8. 第一段階で作るべきもの

最初に作るのは、**Capture と Shared Texture Hub のみ**でよい。

この段階では Blur や Warp の Processor はまだ作らない。

### 8.1 追加対象

#### Camera LTS 側

- `CameraCaptureMB`
- `CameraCaptureService`
- `ICameraCaptureService`
- `ICameraRenderContext`

#### SceneLTS 側

- `SharedTextureChannelHubMB`
- `SharedTextureChannelHubService`
- `ISharedTextureChannelHub`
- `SharedTextureChannelTypes`

### 8.2 この段階で達成すること

- CameraSystem と同じ LTS の Camera を Capture できる
- Capture 結果を `camera/main/source` のようなタグで共有できる
- 共有チャネルに Camera Capture metadata を保持できる
- 他サービスが Hub に問い合わせれば Texture を取得できる
- BaseShader 側はまだ ScreenFx 専用 bind を使わなくてもよい

### 8.3 まだやらないこと

- 多段 blur pipeline
- Sprite 領域限定 mask
- ScreenFx2D の全面撤去
- すべての consumer binder


## 9. 具体的な実装方針

### 9.1 CameraSystem 側への追加

`CameraSystemMB` か同一階層に、`ICameraRenderContext` を register する。

推奨情報:

- `Camera Camera`
- `Transform CameraTransform`
- `Transform FxTransform`
- `string CameraTag`

`CameraTag` 命名例:

- `main`
- `sub`
- `ui`

これにより Capture は CameraSystem に依存しつつ、直接 `CameraSystemService` の内部実装へ触れなくて済む。

補足:

- `CameraCaptureService` はこの `ICameraRenderContext` を参照し、publish 時に `IsCameraCapture = true` と `CaptureCamera = context.Camera` を渡す
- 実際の Hub 保存時には、Camera 本体に加えて capture 時の投影情報 snapshot も `SharedTextureCameraCaptureInfo` に展開する


### 9.2 Shared Texture Hub の責務境界

Hub は以下のみ行う。

- タグ正規化
- publish / replace
- read snapshot
- producer 単位の cleanup
- release

Hub は以下を持たない。

- blur 実装
- material bind 実装
- sprite registration 実装
- effect graph 解釈


### 9.3 Capture Service の責務境界

Capture Service は以下のみ行う。

- CameraRenderContext から Camera を得る
- 描画結果を受け取る
- Hub へ publish する
- Acquire / Release で購読解除と cleanup を行う

Capture Service は以下を持たない。

- blur pass
- warp pass
- consumer bind


## 10. 既存 ScreenFx の扱い

この計画を正式採用するなら、`ScreenFx` 系のサービスと shader は最終的に不要になる。

不要化の対象:

- `ScreenFxBufferFeature`
- `ScreenFxCapturePass`
- `ScreenFxBlurPass`
- `ScreenFxBufferService`
- `ScreenFxBinderService`
- `ScreenFx2D.hlsl`
- `ScreenFxBlur.shader`

理由:

- Capture は `CameraCaptureService` 側へ分離される
- Blur / Warp などの加工は `TextureEffectPipelineService` 側へ分離される
- BaseShader への受け渡しは `SharedTextureMaterialBinderService` 側へ移る
- Camera 最終出力差し替えは `CameraOutputOverrideService` 側へ移る

つまり `ScreenFx` は「機能を残して名前だけ変える」のではなく、**責務ごとに解体して別レイヤへ移す対象**になる。

ただし即時削除ではなく、移行順は次の通り。

1. `CameraCaptureService` 実装
2. `SharedTextureChannelHubService` 実装
3. `SharedTextureMaterialBinderService` 実装
4. `CameraOutputOverrideService` 実装
5. 必要 Processor 実装
6. 旧 `ScreenFx` 参照を除去
7. 最後に `ScreenFx` 系コードを削除


## 11. 実装マイルストーン

実装は **3 段階** に分けるのがよい。

理由:

- 依存の中心は `SharedTextureChannelHubService` であり、最初に基盤だけを安定化させる必要がある
- Consumer 接続と Camera 出力差し替えは、Effect より先に単体価値を出せる
- Mask 付き Effect は最も不確定要素が多く、最後に回す方が安全

### 11.1 Milestone 1: Capture + SharedTexture 基盤

目的:

- `CameraSystemMB` に紐づく Camera の描画結果を `SharedTexture` へ publish できる状態にする

この段階で作るもの:

- `ICameraRenderContext`
- `CameraSystemMB` からの render context 登録
- `SharedTextureChannelHubMB`
- `SharedTextureChannelHubService`
- `ISharedTextureChannelHub`
- `CameraCaptureMB`
- `CameraCaptureService`
- `camera/main/source` publish
- `SharedTextureFrame` の Camera metadata snapshot
- Debug 用の簡易確認手段

この段階では作らないもの:

- `TextureEffectPipelineService`
- `SharedTextureMaterialBinderService`
- `CameraOutputOverrideService`
- 旧 `ScreenFx` の削除

完了条件:

- `CameraCaptureService` が CameraSystem から Camera を受け取れる
- 毎フレーム `camera/main/source` が publish される
- 別 Service から `TryGet("camera/main/source")` で取得できる
- `SharedTextureCameraCaptureInfo` に capture 時点の Camera 情報が入る
- Scope 解放時に `ClearByProducer` で cleanup される
- Scene 再読込や Camera disable で RT が残留しない

この段階の価値:

- 以後の Consumer / Effect はすべてこの publish 面を前提に進められる
- まず「カメラ画像を共有できる」ことだけを確定できる

### 11.2 Milestone 2: Consumer 接続 + Camera 出力差し替え

目的:

- `SharedTexture` に置かれた画像を、MaterialFx 利用者と Camera 出力の両方で使えるようにする

この段階で作るもの:

- `ITaggedMaterialFxProvider`
- `AnimationSpriteHubService` における tag -> MaterialFx 解決 API
- `SharedTextureMaterialBinderMB`
- `SharedTextureMaterialBinderService`
- `CameraOutputOverrideMB`
- `CameraOutputOverrideService`
- `CameraOutputOverrideMode.SharedTexture`
- `CameraSystem` 側の最終出力差し替え分岐

この段階では作らないもの:

- Layer ベース Effect
- Mask 集約
- 可視 fragment ベースの Mask pass

完了条件:

- `AnimationSpriteChannelPlayer` 系の MaterialFx consumer が SharedTexture の指定 tag を受け取れる
- `SpriteRenderer` / `UI Image` が MaterialFx 経由で同じ流れで表示できる
- CameraSystem が SharedTexture の指定 tag を最終出力として使える
- `ScreenFxBinderService` なしでも「写真表示」と「Camera 出力差し替え」の基本用途が成立する

この段階の価値:

- 画面キャプチャを「見せる」「貼る」「差し替える」が成立する
- Effect 実装前でも、SharedTexture 基盤が利用価値を持つ

### 11.3 Milestone 3: Effect Pipeline + Mask + 旧 ScreenFx 移行

目的:

- Layer 単位の加工命令と Mask 登録を統合し、`SharedTexture` を入力とする effect 処理を完成させる

この段階で作るもの:

- `TextureEffectPipelineMB`
- `TextureEffectPipelineService`
- `ITextureEffectPipeline`
- `ITextureEffectLayerRegistry`
- `ITextureEffectMaskRegistry`
- `TextureEffectLayerDef`
- `TextureEffectMaskEntry`
- `Blur / Mosaic / Distort / Refraction / Ripple / ColorShift / Posterize` の初期実装
- LayerTag ごとの mask 合算処理
- Camera 可視部分ベースの mask 生成 pass
- `camera/main/final` などの加工後 tag publish
- 旧 `ScreenFx` 参照の段階的削除

実装順の推奨:

1. fullscreen 対応だけで動く `Blur` と `Mosaic` から入る
2. 次に LayerTag と Mask registry を実装する
3. 最後に可視 fragment ベースの精密 mask に置き換える

完了条件:

- 入力 tag から出力 tag へ effect 結果を書き戻せる
- 同じ LayerTag に対する複数 Mask 登録が 1 枚の mask に合算される
- 画面外、遮蔽、見切れ部分を含まない mask を生成できる
- Camera 全体 effect と局所 effect が同じパイプラインで扱える
- `ScreenFxBufferFeature` / `ScreenFxCapturePass` / `ScreenFxBlurPass` / `ScreenFxBinderService` を不要化できる

この段階の価値:

- 目的だった GameMaker 的な effect 基盤が完成する
- Capture / 共有 / 加工 / 利用の各責務が独立し、今後の拡張がやりやすくなる

### 11.4 着手順の結論

最初に着手するべきは `Milestone 1` である。

理由:

- ここが成立しない限り、Consumer も Effect も仮実装しかできない
- 逆にここが成立すると、後続は SharedTexture tag を介して薄く接続できる

したがって、次の実装タスクは以下に固定する。

1. `ICameraRenderContext`
2. `SharedTextureChannelHubService`
3. `CameraCaptureService`
4. `camera/main/source` publish
5. Debug 確認手段


## 12. この計画の妥当性

この案は、現行コードベースと整合している。

整合する点:

- `MB -> Service` 分離に合う
- `IScopeAcquireHandler / IScopeReleaseHandler` 中心のライフサイクルに合う
- `ChannelHubService` 文化と相性が良い
- MaterialFx の `External Texture / CustomRT` を再利用できる
- ScreenFx 専用変数を増やさずに済む

注意点:

- `Camera` をどう露出するかは最初に決める必要がある
- RTHandle の所有権を Hub と Processor のどちらに置くかは、初期版ではシンプルにする
  - 初期推奨: **Hub が RTHandle を所有し、Processor は Hub から書き込み可能な handle を借りる形**とする
  - Processor が独自に確保した RT は、publish 時に Hub へ所有権を移転する
- WebGL では descriptor と downsample 方針を厳しく管理する必要がある


## 13. 最終判断

今回の段階では、次を正式方針とするのがよい。

- `ScreenFx` は今後「Capture 基盤 + Texture 共有 + Processor + Consumer」へ分離する
- 共有の正本は `SharedTextureChannelHubService`
- BaseShader は利用者であり、共有場所にはしない
- Camera Capture は `CameraSystemMB` と同じ LTS に置く
- `SpriteAnimationChannelPlayer` は将来の request producer にはなるが、基盤の中心には置かない

以上を前提として、第一実装は **Camera Capture + Shared Texture Hub** から着手する。

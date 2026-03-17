<!--
Spec Version: v0.3
Status: Draft / Planning
Updated: 2026-03-14
Note:
- 本仕様書は新規作成です。
- 2026-03-14 時点の現コードを読んだ上で、`NoiseProducerService` 系の再設計案を整理しています。
- 同日、旧 Atlas / NoiseAtlas ラインはコード上から削除済みです。
- v0.2 では、レビューに基づき以下を見直しています。
  - dirty 判定を `構造更新 / 内容更新 / 時間更新` へ分離
  - `SetFloat/SetVector2/SetColor` 直列 API を command ベースへ変更
  - LayerType を単一 enum から `StageKind + Operation` へ再設計
  - `RebuildChannel` を `RequestRefresh(flags)` へ置換
- v0.3 では、コード熟読に基づくレビューで以下を修正しています。
  - `INoiseGraphRenderer` を廃止し、service 内部に直接 blit ロジックを持つ構成へ変更
  - Stage Definition 4 サブクラスを廃止し、フラット構成 + OdinInspector ShowIf へ変更
  - Parameter Layered 型を既存 `LayeredFloat/Vector2/Color/Bool` の共有化へ変更
  - `NoiseParameterValue` の実装方針を FieldOffset explicit struct として明確化
  - Channel 登録 API (`RegisterChannel/UnregisterChannel`) を追加
  - `NoiseChannelState` の定義を追加
  - `SharedTextureSourceKind.NoiseGenerator` 拡張を明記
  - `IDisposable` を service 実装に追加
  - FeatureInstaller の scope kind 方針を明記
  - DynamicValue 連携ポイントを追記
  - OdinInspector 活用方針を追記
  - RT メモリ管理方針を追記
  - エラーハンドリング方針を追記
  - CommandRunner Executor 対応を Phase 計画に追加
- 参照した主なコード:
  - Assets/GameLib/Script/Project/Scene/SharedTexture/Core/ISharedTextureChannelHub.cs
  - Assets/GameLib/Script/Project/Scene/SharedTexture/Core/SharedTextureChannelHubService.cs
  - Assets/GameLib/Script/Project/Scene/CameraSystem/Capture/CameraCaptureService.cs
  - Assets/GameLib/Script/Project/Scene/TextureEffect/Core/TextureEffectPipelineService.cs
  - Assets/GameLib/Script/Project/Scene/Channels/SpriteAnimation/AnimationSpriteHubService.cs
  - Assets/GameLib/Script/Project/Scene/SharedTexture/Binder/SharedTextureMaterialBinderService.cs
  - Assets/GameLib/Script/Project/Scene/CameraSystem/Core/CameraLayeredFloat.cs
  - Assets/GameLib/Script/Project/Scene/CameraSystem/Core/CameraLayeredVector2.cs
  - Assets/GameLib/Script/Shader/Core/MaterialFx/Services/MaterialFxLayerService.cs
  - Assets/GameLib/Script/Shader/Core/MaterialFx/MaterialFxLayer.cs
  - Assets/GameLib/Script/Shader/Core/MaterialFx/TextureSlotTypes.cs
  - Assets/GameLib/Script/Shader/Core/BaseShader/BaseShader.shader
  - Assets/GameLib/Script/Shader/Core/BaseShader/Surface2D.hlsl
  - Assets/GameLib/Script/Shader/Core/BaseShader/Features/TextureSlot2D.hlsl
-->

# NoiseProducerService 計画書

## 1. 目的

本計画の目的は、`ComputeShader` に依存しないノイズ生成基盤を新設し、生成結果を `SharedTexture` へ publish できるようにすることである。

今回の新設計では、単一ノイズを直接生成するだけではなく、以下を Layer として積み上げられる構成を前提にする。

- 単純ノイズ
- fBm
- warp
- gradient
- scroll
- UV flow
- 時間変化を伴う各種補助処理

また、各パラメータは外部から自由に変更でき、`duration + ease + target` による補間更新をパラメータ単位で扱えるようにする。

## 2. 現状認識

### 2.1 すでに使える基盤

- `SharedTextureChannelHubService` が tag 単位の texture publish/read を提供している
- `CameraCaptureService` が producer -> `SharedTexture` publish の基本形を成立させている
- `TextureEffectPipelineService` が `inputTag -> effect -> outputTag` の 1 段パイプラインを持っている
- `SharedTextureMaterialBinderService` と `ITaggedMaterialFxProvider` により、SharedTexture を MaterialFx consumer へ流し込む導線がある
- `BaseShader` は `ExternalA / ExternalB / CustomRT` を受け取る単純な構成に整理済みである

### 2.2 今回新設が必要なもの

- ノイズ生成専用の producer service
- ノイズ用 channel definition
- ノイズ用 parameter animation レイヤー
- レイヤーグラフを実行して 1 枚の出力 texture に合成する仕組み
- ノイズチャンネルの inspector/definition authoring 導線

## 3. 基本方針

### 3.1 出力面

ノイズはすべて `SharedTexture` に publish する。

- producer tag 例: `noise-producer/{scopeTag}/{channelName}`
- output tag 例: `noise/{channelName}/main`
- 必要なら派生 tag 例:
  - `noise/{channelName}/mask`
  - `noise/{channelName}/normal`
  - `noise/{channelName}/debug`

consumer 側は「どう生成したか」を知らず、`SharedTexture` tag のみを参照する。

### 3.2 実装方式

初期実装では `Material + Blit` を基本とする。

- 理由1: `ComputeShader` 非依存にできる
- 理由2: WebGL 系でも成立しやすい
- 理由3: layer を shader pass 単位で増やしやすい

Phase 1 では service 内部に blit ロジックを直接持つ。  
`INoiseGraphRenderer` のような renderer 抽象は設けない。

理由:

- 初期実装が 1 つしかない段階で interface を切るのは YAGNI
- 既存の `CameraCaptureService` も renderer abstraction なしで直接実装している
- 抽象が必要になった時点（ComputeShader 実装追加時など）で抽出する方が正確な契約になる

### 3.3 エラーハンドリング方針

プロジェクト方針に従い、例外処理は使わない。  
全 API は `bool` 戻り値で成否を返し、失敗時は `UnityEngine.Debug.LogWarning` で記録する。

- `TryWriteParameter` → address 不正時は `false` を返す
- `RequestRefresh` → channelId 不在時は `false` を返す
- RT 作成失敗時はチャンネルを `Disabled` 状態にし、次回 tick で再試行する

### 3.4 RenderTexture メモリ管理

RT は GPU メモリを直接消費するため、以下の制約を設ける。

- 同時アクティブ RT の上限はサービス単位で制限する（初期: 8 枚）
- 解像度は channel definition に従うが、上限を 2048x2048 とする
- `TextureEffectPipelineService.EnsureRT()` と同様の lazy creation + reuse パターンを採る
- stage 間の中間 RT は ping-pong 方式で最大 2 枚に抑える
- release 時は全 RT を `Release() + Destroy()` する

### 3.5 OdinInspector 活用方針

definition の Inspector 表示は OdinInspector を活用し、開発者が迷わない設計にする。

- `NoiseStageDefinition` は `StageKind` に応じて `[ShowIf]` でフィールドを出し分ける
- channel definition は `[FoldoutGroup]` で stage list と parameter list を整理する
- `NoiseParameterDefinition` は `[ValueDropdown]` で AffectsStage を stage list から選択可能にする
- debug view は `[ReadOnly]` + `[ProgressBar]` で runtime state を視覚化する

### 3.6 時間処理

ノイズは時間依存を前提とする。

- service は `ITickable` で更新する
- 各 channel は `timeScale` を持つ
- 各 layer は `UseGlobalTime / LocalTimeOffset / Speed / Scroll / Phase` を持てる
- 停止や手動 scrub を考慮し、`Now`, `DeltaTime`, `ChannelTime` を明示的に扱う

### 3.7 評価条件

`dirty channel のみ再評価` という表現は、この機能では不十分である。  
時間依存ノイズは、parameter が変わらなくても結果が毎フレーム変わるため、評価条件を次の 3 系統に分ける。

- `StructuralDirty`
  - definition 差し替え
  - material/pass キャッシュ更新
  - render target 再確保
- `ContentDirty`
  - parameter 値変更
  - layer enable/disable
  - seed や入力参照の変更
- `TemporalActive`
  - time 依存 stage を持つ
  - parameter animation が進行中
  - manual scrub / playback 中

channel は次のいずれかを満たしたときに評価する。

- `StructuralDirty != None`
- `ContentDirty == true`
- `TemporalActive == true`

つまり、service が tick されることと、毎フレーム必ず再描画することは同義ではない。  
`ITickable` は scheduler 更新の責務であり、render 実行判定は上記 3 系統で行う。

## 4. 提案アーキテクチャ

### 4.1 中核サービス

#### `INoiseProducerService`

責務:

- noise channel の登録/解除
- definition の解決
- tick ごとの評価
- output texture の publish
- 外部からの parameter 更新 API 提供

主要 API 案:

```csharp
// --- Flags enum は [Flags] のため 2^n を使う（一般 enum の 10 刻みとは異なる） ---
[Flags]
public enum NoiseChannelRefreshFlags
{
    None = 0,
    ResolveParameters = 1,
    RebuildMaterials = 2,
    RecreateTargets = 4,
    ReloadDefinition = 8,
    Full = ResolveParameters | RebuildMaterials | RecreateTargets | ReloadDefinition,
}

public readonly struct NoiseParameterAddress
{
    public readonly string ChannelId;
    public readonly string ParameterKey;
    public readonly string LayerTag;
}

// --- NoiseParameterValue: explicit layout による discriminated union ---
[StructLayout(LayoutKind.Explicit)]
public struct NoiseParameterValue
{
    [FieldOffset(0)] public NoiseParameterValueKind Kind;
    [FieldOffset(4)] public float FloatValue;
    [FieldOffset(4)] public Vector2 Vector2Value;
    [FieldOffset(4)] public Color ColorValue;
    [FieldOffset(4)] public bool BoolValue;
    [FieldOffset(4)] public int IntValue;

    public static NoiseParameterValue Float(float v) => new() { Kind = NoiseParameterValueKind.Float, FloatValue = v };
    public static NoiseParameterValue Vec2(Vector2 v) => new() { Kind = NoiseParameterValueKind.Vector2, Vector2Value = v };
    public static NoiseParameterValue Col(Color v) => new() { Kind = NoiseParameterValueKind.Color, ColorValue = v };
    public static NoiseParameterValue Bool(bool v) => new() { Kind = NoiseParameterValueKind.Bool, BoolValue = v };
    public static NoiseParameterValue Int(int v) => new() { Kind = NoiseParameterValueKind.Int, IntValue = v };
}

public enum NoiseParameterValueKind
{
    Float = 10,
    Vector2 = 20,
    Color = 30,
    Bool = 40,
    Int = 50,
}

public readonly struct NoiseParameterWriteRequest
{
    public readonly NoiseParameterAddress Address;
    public readonly NoiseParameterValue Value;
    public readonly float Duration;
    public readonly Ease Ease;
}

// --- 外部公開の read-only 状態 ---
public readonly struct NoiseChannelState
{
    public readonly bool IsActive;
    public readonly bool IsTemporalActive;
    public readonly float ChannelTime;
    public readonly int LastRenderedFrame;
    public readonly int ParameterCount;
    public readonly int StageCount;
}

public interface INoiseProducerService
{
    bool ContainsChannel(string channelId);
    bool TryGetChannelState(string channelId, out NoiseChannelState state);
    bool RegisterChannel(string channelId, NoiseChannelDefinition definition);
    bool UnregisterChannel(string channelId);
    bool TryWriteParameter(in NoiseParameterWriteRequest request);
    bool ClearParameterLayer(in NoiseParameterAddress address);
    bool RequestRefresh(string channelId, NoiseChannelRefreshFlags flags);
}
```

補足:

- typed な `SetFloat/SetVector2/SetColor` は extension として生やす（service 本体の API 増殖を防ぐ）
- `NoiseParameterValue` は `FieldOffset` explicit layout で boxing を回避する
- `NoiseChannelState` は外部が channel の状態を read-only で参照するための構造体
- `RegisterChannel/UnregisterChannel` により runtime からの動的チャンネル管理が可能
- 初期の definition 登録は FeatureInstaller 経由の DI で `OnAcquire` 時に `RegisterChannel` を呼ぶ
- 全 API は `bool` で成否を返す（例外は投げない）

#### `NoiseProducerService`

実装責務:

- `IScopeAcquireHandler / IScopeReleaseHandler / ITickable / IDisposable`
- definition から runtime channel を構築
- `StructuralDirty / ContentDirty / TemporalActive` を統合して評価判定
- output render target の確保と解放（`EnsureRT` パターン）
- `ISharedTextureChannelHub` への publish
- stage list の blit 実行（Material + `Graphics.Blit` を直接保持）

`IDisposable` は `IScopeReleaseHandler.OnRelease` と併用する。  
`OnRelease` ではチャンネル状態をクリアし、`Dispose` では RT を含む全 native resource を解放する。  
これは `SharedTextureChannelHubService`, `TextureEffectPipelineService` と同様のパターンである。

### 4.2 runtime channel

#### `NoiseChannelRuntime`

保持内容:

- channel definition
- output descriptor
- runtime time state
- parameter state table
- stage runtime table
- render target
- latest publish frame/version
- `NoiseChannelRefreshFlags PendingRefreshFlags`
- `bool ContentDirty`
- `bool HasTimeReactiveStage`
- `bool HasAnimatingParameters`
- `int LastRenderedFrame`
- `int LastPublishedFrame`

### 4.3 definition 群

#### `NoiseChannelDefinition`

1 channel の構成を持つ `[Serializable]` クラス。  
SO は薄いラッパ（後述 9.4）として、このクラスを 1 フィールドで保持する。

候補フィールド:

- `ChannelId`
- `OutputTag`
- `Resolution` (`Vector2Int`, 上限 2048x2048)
- `GraphicsFormat`
- `FilterMode`
- `WrapMode`
- `ClearColor`
- `AutoPublish`
- `TimeScale`
- `LoopSeconds` (0 以下でループ無効)
- `Seed`
- `List<NoiseStageDefinition>`
- `List<NoiseParameterDefinition>`

#### `NoiseStageDefinition`

全 stage kind を 1 クラスで表現する `[Serializable]` クラス。  
`StageKind` に応じて OdinInspector の `[ShowIf]` でフィールドを出し分ける。

v0.2 では `NoiseGeneratorStageDefinition`, `NoiseUvStageDefinition` 等 4 サブクラスに分離していたが、以下の理由で廃止した。

- Phase 1 で使う stage が合計 4 つしかないのに型を 5 つ作るのは過剰
- Unity の `[SerializeReference]` は Inspector が煩雑になりやすい
- OdinInspector の `[ShowIf]` でフラット構成でも可読性を十分に保てる
- 将来サブクラスが必要になった時点で抽出すればよい

共通フィールド:

- `StageId` (`string`)
- `StageKind` (`StageKind` enum)
- `Enabled` (`bool`)
- `OutputSlot` (`string`)
- `CommonTimeSettings` (後述)

Generator 固有フィールド (`[ShowIf("StageKind", StageKind.Generator)]`):

- `GeneratorOp` (`NoiseGeneratorOp`)
- `Seed` (`int`)
- `BaseUvInput` (`string`, optional)
- `Scale` (`Vector2`)
- `Offset` (`Vector2`)
- `Rotation` (`float`)
- `GradientA` (`Color`)
- `GradientB` (`Color`)
- `Octaves` (`int`)
- `Lacunarity` (`float`)
- `Gain` (`float`)

Uv 固有フィールド (`[ShowIf("StageKind", StageKind.Uv)]`):

- `UvOp` (`NoiseUvOp`)
- `BaseUvInput` (`string`, optional)
- `VectorInput` (`string`, optional)
- `Scroll` (`Vector2`)
- `FlowStrength` (`float`)
- `Rotation` (`float`)
- `PolarCenter` (`Vector2`)

Filter 固有フィールド (`[ShowIf("StageKind", StageKind.Filter)]`):

- `FilterOp` (`NoiseFilterOp`)
- `PrimaryInput` (`string`)
- `UvInput` (`string`, optional)
- `Strength` (`float`)
- `Threshold` (`float`)
- `Softness` (`float`)
- `WarpVectorInput` (`string`, optional)
- `NormalStrength` (`float`)

Composite 固有フィールド (`[ShowIf("StageKind", StageKind.Composite)]`):

- `CompositeOp` (`NoiseCompositeOp`)
- `PrimaryInput` (`string`)
- `SecondaryInput` (`string`)
- `MaskInput` (`string`, optional)
- `Blend` (`float`)
- `Opacity` (`float`)

#### `NoiseParameterDefinition`

外部公開する parameter の定義。

候補フィールド:

- `ParameterKey` (`string`)
- `ValueKind` (`NoiseParameterValueKind`)
- `DefaultValue` (`NoiseParameterValue`)
- `Min` (`float`)
- `Max` (`float`)
- `Exposed` (`bool`)
- `AffectsStageId` (`string`, `[ValueDropdown]` で stage list から選択)
- `AffectsField` (`string`)
- `Description` (`string`)

## 5. Stage モデル

### 5.1 `StageKind`

単一の `LayerType` に全責務を混在させず、まず大分類を分ける。

- `Generator = 10`
- `Uv = 20`
- `Filter = 30`
- `Composite = 40`

`enum` はすべて数値付きで定義する。

### 5.2 operation enum

各 `StageKind` の下に個別 operation enum を持つ。

#### `NoiseGeneratorOp`

- `SolidColor = 10`
- `GradientLinear = 20`
- `GradientRadial = 30`
- `ValueNoise = 40`
- `PerlinLike = 50`
- `SimplexLike = 60`
- `Fbm = 70`

#### `NoiseUvOp`

- `Scroll = 10`
- `Flow = 20`
- `Rotate = 30`
- `Polar = 40`

#### `NoiseFilterOp`

- `Warp = 10`
- `Levels = 20`
- `Clamp = 30`
- `Invert = 40`
- `NormalFromHeight = 50`

#### `NoiseCompositeOp`

- `Blend = 10`
- `Add = 20`
- `Multiply = 30`
- `Min = 40`
- `Max = 50`
- `MaskBlend = 60`

### 5.3 入力契約

renderer 実装の分岐を抑えるため、各 stage は入力契約を固定する。

- `Generator`
  - input 不要
  - optional な `BaseUvInput` のみ参照可能
- `Uv`
  - 画素値ではなく UV context を出力
  - 後続 stage が参照する
- `Filter`
  - `PrimaryInput` 必須
  - `UvInput` は optional
- `Composite`
  - `PrimaryInput` と `SecondaryInput` 必須
  - `MaskInput` は optional

### 5.4 実行モデル

初期段階では、複雑な自由 DAG ではなく「stage list + 明示 input 参照」とする。

理由:

- authoring が単純
- inspector 実装が軽い
- デバッグしやすい
- まず必要なのは自由度より保守性

ただし、各 stage は `OutputSlot` を持ち、後続 stage が slot を参照できるようにする。  
これにより、自由 DAG までは行かずとも、入力契約が壊れない範囲で再利用ができる。

### 5.5 推奨責務

- `Generator`: パターンや基底色を作る
- `Uv`: 座標系だけを変える
- `Filter`: 既存出力を加工する
- `Composite`: 複数結果を束ねる

## 6. Parameter システム

### 6.1 MaterialFx とは分ける

今回は `MaterialFx` を流用せず、ノイズ専用の parameter layer system を持つ。

理由:

- `MaterialFx` は shader property bind が主責務
- ノイズ側は definition 値の runtime override が主責務
- 将来 BaseShader 以外の renderer/material 実装を使う可能性が高い

### 6.2 既存 Layered 型の共有化

v0.2 では `NoiseLayeredFloat/Vector2/Color/Bool` を新規作成する方針だったが、  
既存の `LayeredFloat`, `LayeredVector2`, `LayeredColor`, `LayeredBool`（CameraSystem）と構造が同一であるため、  
これらを `Common/Utility` に移動して共有する方針に変更する。

移動先: `Assets/GameLib/Script/Common/Layered/`

理由:

- 既存 Layered 型は DOTween 統合 + layer tag + 即時/補間設定を完備しており、ノイズ parameter の要件を満たす
- 重複実装は保守コストを増やし、バグ修正の二重管理が発生する
- CameraSystem 側の既存参照は `using` の変更のみで対応可能

差分が必要になる場合（例: priority 合成、lifetime 管理）は、その時点で `NoiseLayered*` として派生させる。

### 6.3 Parameter layer stack

各 parameter は `parameterKey` 単位で layer stack を持つ。

候補クラス:

- `NoiseParameterLayer`
- `NoiseParameterLayerStack`
- `NoiseParameterAnimationState`
- `NoiseParameterRuntimeTable`

`NoiseParameterRuntimeTable` 内の Dictionary は `StringComparer.Ordinal` を使用し、  
頻繁な per-frame ルックアップのパフォーマンスを確保する。

### 6.4 補間仕様

最低限必要な契約:

- 即時設定
- `target + duration + ease` による補間
- layerTag 単位の削除
- parameter 単位の reset

優先度は初期導入では不要とする。

その代わり、同一 `parameterKey` 内では以下を採る。

- `Default` レイヤーを基底に持つ
- override は `layerTag` 単位で管理する
- 合成は初期版では last-writer-wins とする
- 将来必要になれば `Add / Multiply / Min / Max` を追加する

### 6.5 補間実装の指針

既存の `LayeredFloat / LayeredVector2 / LayeredColor / LayeredBool` を共有 utility として使用する（6.2 参照）。

float, Vector2, Color, bool の 4 系統で Phase 1 は十分。  
将来 `Int / Vector3 / Gradient` が必要になった場合は共有 Layered 系に追加する。

### 6.6 parameter 書き込み契約

service の public API は `NoiseParameterWriteRequest` を受け取る 1 本に寄せる。  
型ごとの補助関数は、service 本体ではなく helper/extension に分離する。

理由:

- API 増殖を防げる
- command queue 化しやすい
- 将来 `Int / Vector3 / Gradient` を足しても service 契約を壊さない

補助関数例:

- `NoiseProducerServiceExtensions.SetFloat(...)`
- `NoiseProducerServiceExtensions.SetVector2(...)`
- `NoiseProducerServiceExtensions.SetColor(...)`

これらは `NoiseParameterWriteRequest` を内部生成する薄いラッパとする。

## 7. Refresh 契約

### 7.1 `RebuildChannel` は使わない

`RebuildChannel` という名前は曖昧なので使わない。  
代わりに `RequestRefresh(channelId, flags)` を使う。

### 7.2 refresh の意味

最低限、次の 4 種類を区別する。

- `ResolveParameters`
  - definition と runtime parameter binding の再解決
- `RebuildMaterials`
  - material / pass / renderer cache の再構築
- `RecreateTargets`
  - render target の再確保
- `ReloadDefinition`
  - definition の再読込を伴うフル更新

### 7.3 internal 運用

service 内では `PendingRefreshFlags` を持ち、tick 冒頭で処理する。

- `ReloadDefinition` は最優先
- `RecreateTargets` は resource 差し替えを伴う
- `RebuildMaterials` は renderer cache 差し替え
- `ResolveParameters` は binding 再同期

その後で `ContentDirty / TemporalActive` を見て render 実行へ進む。

## 8. SharedTexture との接続

### 8.1 SharedTextureSourceKind の拡張

`SharedTextureSourceKind` に新しい値を追加する。

```csharp
public enum SharedTextureSourceKind
{
    Unknown = 0,
    CameraCapture = 10,
    ProcessorOutput = 20,
    ImportedTexture = 30,
    ExternalTexture = 40,
    NoiseGenerator = 50,    // 新規追加
}
```

`SharedTexturePublishOptions` にファクトリメソッドを追加する。

```csharp
public static SharedTexturePublishOptions ForNoiseProducer(string producerTag)
    => new(producerTag, SharedTextureSourceKind.NoiseGenerator);
```

### 8.2 publish 契約

1 channel は 1 つ以上の output tag を publish できる。

初期版はまず 1 channel -> 1 output tag で十分。

publish 時の descriptor は channel definition が正とする。  
`SharedTexturePublishOptions.ForNoiseProducer(producerTag)` を使用する。

### 8.3 producer tag

producer tag は channel 固有にする。

例:

- `noise-producer/field/fog-main`
- `noise-producer/ui/wipe-mask`

release 時は `ClearByProducer()` でまとめて掃除する。

## 9. 主要クラス案

### 9.1 runtime / service

- `INoiseProducerService`
- `NoiseProducerService` (`IScopeAcquireHandler / IScopeReleaseHandler / ITickable / IDisposable`)
- `NoiseChannelRuntime`
- `NoiseChannelState` (readonly struct)
- `NoiseProducerTickContext`
- `NoiseChannelRefreshFlags`

### 9.2 definitions

- `NoiseChannelDefinition` (`[Serializable]` class)
- `NoiseStageDefinition` (`[Serializable]` class, フラット構成)
- `NoiseParameterDefinition` (`[Serializable]` class)
- `NoiseOutputDefinition` (`[Serializable]` class)

### 9.3 parameter runtime

- `NoiseParameterAddress` (readonly struct)
- `NoiseParameterWriteRequest` (readonly struct)
- `NoiseParameterValue` (explicit layout struct)
- `NoiseParameterValueKind` (enum)
- `NoiseParameterLayer`
- `NoiseParameterLayerStack`
- `NoiseParameterRuntimeTable`

### 9.4 MB / installer / SO

- `NoiseProducerMB` (`IFeatureInstaller`)
- `NoiseProducerChannelDefinitionSO` — `NoiseChannelDefinition` の薄いラッパ SO
- `NoiseProducerInstaller`
- `NoiseProducerDebugViewMB`

SO 設計方針（AGENTS.md 準拠）:

```csharp
[CreateAssetMenu(menuName = "GameLib/Noise/ChannelDefinition")]
public sealed class NoiseProducerChannelDefinitionSO : ScriptableObject
{
    [SerializeField] NoiseChannelDefinition _definition;
    public NoiseChannelDefinition Definition => _definition;
}
```

`DynamicValue<NoiseChannelDefinition>` として使用する場合は、  
`NoiseChannelDefinition` を `T` に入れるだけで連携できる。

### 9.5 既存コードへの変更

- `SharedTextureSourceKind` に `NoiseGenerator = 50` を追加
- `SharedTexturePublishOptions` に `ForNoiseProducer()` ファクトリメソッドを追加
- `LayeredFloat/Vector2/Color/Bool` を `Common/Layered/` に移動（CameraSystem からの参照を更新）

### 9.6 FeatureInstaller scope 方針

`NoiseProducerMB` は `IFeatureInstaller` を実装し、`InstallFeature` 内で scope kind をチェックする。

```csharp
public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
{
    // Scene scope で動作（Project scope では不要）
    if (scope.Kind != LifetimeScopeKind.Scene) return;

    builder.Register<NoiseProducerService>(Lifetime.Singleton)
        .As<INoiseProducerService>()
        .As<IScopeAcquireHandler>()
        .As<IScopeReleaseHandler>()
        .As<ITickable>()
        .As<IDisposable>();
}
```

Project scope でも動作させる場合は、definition の提供元に応じて判断する。  
初期は Scene scope のみとし、必要に応じて拡張する。

## 10. 実装順序

### Phase 1: 最小成立

- `NoiseChannelDefinition` + `NoiseStageDefinition` (フラット構成)
- `NoiseParameterDefinition` + `NoiseParameterValue` (explicit layout)
- `NoiseProducerService` (blit ロジック内蔵、`IDisposable` 含む)
- `NoiseProducerChannelDefinitionSO` (薄いラッパ SO)
- `NoiseProducerMB` (`IFeatureInstaller`)
- `SharedTextureSourceKind.NoiseGenerator` 追加
- `SharedTexturePublishOptions.ForNoiseProducer()` 追加
- `LayeredFloat/Vector2` の `Common/Layered/` 移動
- `Generator(ValueNoise, GradientLinear)` + `Uv(Scroll)` + `Filter(Levels)` + `Composite(Blend)`
- `float / Vector2` parameter animation
- `SharedTexture` publish
- ノイズ用 shader (Material + Blit)

完了条件:

- 1 channel を生成し、`noise/{channel}/main` を publish できる
- time 依存 channel が毎フレーム正しく再評価される
- 外部から `duration + ease` で主要 float/vector を変更できる
- RT メモリ制限（8 枚上限、2048x2048 上限）が機能している
- OdinInspector で StageKind に応じたフィールド出し分けが動作する

### Phase 2: 実用化

- `Generator(Fbm)`
- `Filter(Warp)`
- `Color` parameter animation (`LayeredColor` 共有化)
- `MaskInput / SecondaryInput`
- debug telemetry
- `NoiseParameterWriteCommand` executor + `CommandRunnerMB` 登録

完了条件:

- 複数 layer を順に重ねて絵作りできる
- warp/fBm を含む実用的なノイズを 1 channel で構築できる
- Command 系からノイズパラメータを操作できる

### Phase 3: 拡張

- `NormalFromHeight`
- multi output
- composite mode 拡張
- 再生停止 / manual time / scrub
- editor authoring 強化
- `DynamicValue<NoiseChannelDefinition>` 連携
- renderer 抽象化（ComputeShader 実装追加時）

## 11. 非目標

今回の初期実装では以下はやらない。

- parameter priority 合成
- 汎用ノードグラフ editor
- CPU ノイズ生成との二重実装
- ComputeShader 互換レイヤー
- legacy atlas 互換
- renderer 抽象化 (`INoiseGraphRenderer` は Phase 3 以降で必要時に導入)

## 12. 時間ループ仕様

### 12.1 LoopSeconds の動作

`LoopSeconds > 0` の場合、`ChannelTime` は `0 ~ LoopSeconds` で循環する。

```
ChannelTime = fmod(rawTime * TimeScale, LoopSeconds)
```

### 12.2 ループ境界の扱い

- ノイズ自体のシームレス性は shader 側で対応する（タイル可能なノイズ関数の使用）
- parameter animation がループ境界を跨ぐ場合:
  - 進行中の tween は境界で中断せず、`rawTime` ベースで継続する
  - ChannelTime のリセットは tween の進捗に影響しない
- `LoopSeconds <= 0` の場合はループしない（rawTime がそのまま ChannelTime になる）

## 13. DynamicValue 連携

### 13.1 Phase 1 では直接連携しない

初期は `NoiseParameterWriteRequest` による command ベース操作のみ。

### 13.2 Phase 3 での拡張ポイント

- `DynamicValue<NoiseChannelDefinition>` を DynamicSource として登録可能にする
- `NoiseParameterAddress` を `IDynamicSource` から参照できるようにする
- ノイズ出力値を他の DynamicValue chain の入力として使えるようにする

これにより、ノイズパラメータを VarStore / Blackboard / Expression 等から駆動できるようになる。

## 14. 補足意見

この機能は、最初から「万能ノイズシステム」を作るより、`SharedTexture producer` として安定に動く最小核を先に作る方がよい。

そのため、初手は以下を推奨する。

1. `NoiseChannelDefinition + NoiseProducerService`（blit ロジック内蔵）
2. `Generator / Uv / Filter / Composite` の 4 分類を先に固定
3. `NoiseParameterWriteRequest` ベースの parameter animation
4. `SharedTexture` publish と binder consumer での実利用確認

この順なら、構造を壊さずに拡張しやすく、今の `CameraCapture` / `SharedTexture` 路線とも揃う。

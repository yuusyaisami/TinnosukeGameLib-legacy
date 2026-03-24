# MaterialFx Stencil Sprite System Spec

Revision: v1
Date: 2026-03-21
Status: Spec only, no code changes

## 1. Goal

UI で `Mask` / `RectMask2D` による stencil を扱う `Sprite` 系描画に対して、既存の `MaterialFx` 基盤のまま、より自由度の高い stencil 制御を追加する。

狙いは次の 3 つ。

1. Unity 標準の Mask が持つ stencil 挙動を壊さずに、MaterialFx 側からも stencil 関連パラメータを調整できるようにする。
2. `AnimationSpriteChannel` など既存の `MaterialFx` 初期化経路と競合しない。
3. Editor 上で最初から instance を持ち、保存済みの初期パラメータをそのままプレビュー・継承できるようにする。

## 2. Current State

### 2.1 MaterialFx の現在の入口

- `MaterialFxMB` が `IMaterialFxPropertyRegistry` と `IMaterialFxServiceFactory` を scope に登録する。
- `MaterialFxServiceFactory` は target ごとに `IMaterialFxService` を作成する。
- `MaterialFxService` は `SetLayer` / `ApplyPreset` / `SetLayerFade` / `Tick` を統合する実装である。
- `MaterialFxPropertyRegistrySO` が `StableKey -> ShaderPropertyName` の対応表を持ち、`MaterialFxPropertyCodeGenerator` が `MaterialFxKeys.g.cs` を生成する。

### 2.2 AnimationSprite 連携の現在の入口

- `AnimationSpriteChannelPlayer` は SpriteRenderer / Image に対して MaterialFx を初期化する。
- 初期化時に `BaseShaderPreset` を適用し、その後 `MaterialFxPresetEntries` を適用する。
- `AnimationSpriteChannelPlayer` の `Graphic` 側は `MaterialFxGraphicModifier` を使い、`Graphic.material` から `Material` instance を作成する。
- `MaterialFxGraphicModifier` は base material が変わった時に stencil 系プロパティをコピーする。

### 2.3 既存の stencil 関連

- `MaterialFxGraphicModifier` は `_Stencil` / `_StencilComp` / `_StencilOp` / `_StencilWriteMask` / `_StencilReadMask` / `_ColorMask` / `_UseUIAlphaClip` を base material から同期する。
- つまり、Unity の Mask / RectMask2D が作る stencil 値はすでに MaterialFx 側の instance に流れている。
- ただしこれは「Unity が作った値をコピーする」だけであり、MaterialFx 側の authoring data とは統合されていない。

## 3. Requirements

### 3.1 Functional

- UI の stencil を反転表示できること。
- Unity Mask / RectMask2D と共存できること。
- MaterialFx の preset / entry / registry で stencil を扱えること。
- editor 上で保存済みの初期値を持つ instance を再利用できること。
- `AnimationSpriteChannel` が既存の MaterialFx 連携を壊さないこと。

### 3.2 Non-Functional

- 1 target に複数の MaterialFx instance を作らない。
- Release / Rebuild / Mask 変更時の挙動が決定的であること。
- runtime で毎フレーム new しない。
- existing `BaseShaderPreset` / `MaterialFxPresetEntries` の流れを維持する。

## 4. Proposed Architecture

### 4.1 3 層分離

1. Unity 標準 stencil layer
   - `Mask` / `RectMask2D` が生成する stencil 値。
   - これは hierarchy 構造の結果として扱う。

2. MaterialFx authored stencil layer
   - `BaseShader` の追加パラメータとして実装する。
   - 反転 mask、compare mode、write mask、read mask、color mask などをここで扱う。

3. Channel / Runtime layer
   - `AnimationSpriteChannelPlayer` / `MeshFxVisualService` / `MaterialFxService` が preset を適用する。
   - ここは stencil の意味を持たず、単に layer を流すだけにする。

### 4.2 Material instance ownership

- `Graphic` 系の instance 所有者は `MaterialFxGraphicModifier` を中心にする。
- Editor 上で既に instance が存在する場合はそれを再利用する。
- `AnimationSpriteChannelPlayer` は新規 instance を無条件生成しない。
- 既存 owner がある場合は、その owner の instance に preset を載せる。

### 4.3 Initial preset handoff

- authoring 側の preset は `BaseShaderFxPresetReference` と同じく、`inline` と `asset` の両対応を維持する。
- `BaseShaderFxPreset` の初期値は、instance creation 時に target に移し替える。
- `AnimationSpriteChannelPlayer` は `BaseShaderPreset` と `MaterialFxPresetEntries` を今まで通り適用する。
- 追加の stencil preset も同じ順序で適用し、最終値は後勝ちにする。

## 5. Data Model

### 5.1 Add to BaseShader

`BaseShaderFxPreset` に stencil 系のセクションを追加する。

推奨項目:

- `StencilEnabled`
- `StencilMode`
- `StencilRef`
- `StencilComp`
- `StencilPass`
- `StencilFail`
- `StencilZFail`
- `StencilReadMask`
- `StencilWriteMask`
- `StencilColorMask`
- `UseUIAlphaClip`
- `InvertMask` or `ReverseVisibleArea`

### 5.2 Registry / Key generation

- `MaterialFxPropertyRegistrySO` に stencil 系ノードを追加する。
- `MaterialFxPropertyCodeGenerator` により `MaterialFxKeys.BaseShader.Stencil.*` を生成する。
- `ValueKind` は少なくとも `Int` / `Bool` を中心に設計し、必要なら `Float` や enum-like registry を併用する。

### 5.3 Editor authoring wrapper

MaterialFx の既存設計は「SO は薄いラッパ、実データは serializable class」である。

この方針に合わせて、stencil preset も次のどちらかで表現する。

1. `BaseShaderFxPreset` に直接追加する
2. stencil 専用の serializable class を `BaseShaderFxPreset` の1セクションとして保持する

推奨は 2 だが、既存の `BaseShaderFxPresetReference` / `MaterialFxPresetSOBase` と同じく、最終的には `BaseShaderFxPreset` に集約されること。

## 6. Runtime Flow

### 6.1 Graphic target

1. `GraphicAdapter` が `MaterialFxGraphicModifier` を確保する。
2. `MaterialFxGraphicModifier` が `Graphic.material` から instance を作る。
3. Unity stencil 値を base material からコピーする。
4. MaterialFx authored stencil values を preset layer として適用する。
5. `Apply()` 時に base stencil と authored stencil の最終値を反映する。

### 6.2 SpriteRenderer target

1. 既存の `SpriteRendererAdapter` 経路を使う。
2. 同じ `BaseShader` key を流すが、UI stencil の copy は行わない。
3. stencil 系設定は、UI 専用のものと分離して扱う。

### 6.3 AnimationSpriteChannel

- `AnimationSpriteChannelPlayer` は `CreateForSpriteRenderer` / `CreateForGraphic` のどちらでも、すでにある MaterialFx owner を再利用できること。
- `BaseShaderPreset` は initialization の第一段階で適用する。
- `MaterialFxPresetEntries` はその後に適用する。
- authoring script がある場合は、その instance を優先する。

## 7. Merge Policy

### 7.1 Unity stencil vs MaterialFx stencil

優先順位は次のとおり。

1. Unity が UI 階層から計算した stencil state
2. MaterialFx authored stencil preset
3. runtime channel の追加 layer

ただし、実際の shader 実装では「Unity stencil をそのまま使う」か「MaterialFx stencil で上書く」かを明示的に分ける必要がある。

### 7.2 反転 mask

反転表示は単一フラグでは足りない可能性があるため、以下のどちらかで表現する。

1. 高レベル preset mode
   - `Normal`
   - `Inverted`
   - `Custom`

2. Raw stencil override
   - compare / pass / fail / zfail / read / write を直接編集する

推奨は 1 を UI 入口にして、advanced foldout で 2 を露出する方式。

## 8. Editor Workflow

- Inspector 上では stencil セクションを `BaseShader` 内の独立 group として表示する。
- `MaterialFxPropertyExplorerWindow` で `MaterialFxKeys` を確認できるようにする。
- `MaterialFxPropertyPicker` は新しい stencil key を選べる必要がある。
- `Graphic` に authoring component が存在する場合は、まずそこに instance を生成し、その instance に preset を反映する。
- `AnimationSpriteChannel` の初期化時は、既存 instance があるならそれを流用する。

## 9. Integration Points

### 9.1 Files that likely need changes

- `Assets/GameLib/Script/Shader/Core/SO/BaseShaderFxPresetSO.cs`
- `Assets/GameLib/Script/Shader/Core/SO/BaseShaderFxPresetReference.cs`
- `Assets/GameLib/Script/Shader/Core/MaterialFx/MaterialFxGraphicModifier.cs`
- `Assets/GameLib/Script/Shader/Core/MaterialFx/Services/MaterialFxTargetAdapters.cs`
- `Assets/GameLib/Script/Shader/Core/MaterialFx/MaterialFxPropertyRegistrySO.cs`
- `Assets/GameLib/Script/Shader/Core/MaterialFx/Editor/MaterialFxPropertyCodeGenerator.cs`
- `Assets/GameLib/Script/Common/Commands/VNext/Commands/Channels/AnimationSpriteChannelCommandData.cs`
- `Assets/GameLib/Script/Project/Scene/Channels/SpriteAnimation/AnimationSpriteChannelPlayer.cs`
- `Assets/GameLib/Script/Project/Scene/Channels/SpriteAnimation/AnimationSpriteHubService.cs`

### 9.2 Existing behavior to preserve

- `AnimationSpriteChannel` の playback / flip / sorting order の処理
- `MaterialFxServiceFactory` の single target lease 方式
- `MaterialFxGraphicModifier` の stencil copy behavior
- `MeshFxVisualService` の `BaseShaderPreset` + `MaterialFxPresetEntries` の適用順

## 10. Risks

- Unity stencil と MaterialFx authored stencil の責務が曖昧だと、Mask が壊れる。
- 1 target に 2 つ以上の MaterialFx instance がぶら下がると、既存の MPB / material write 競合が再発する。
- Shader property 名を直接 UI に出しすぎると、Stencil の意図が伝わりにくくなる。
- `Graphic` と `SpriteRenderer` の両対応を同じ設計に押し込むと、UI stencil の概念が漏れる。

## 11. Implementation Order

1. stencil 用 property registry と `BaseShader` preset fields を追加する。
2. `MaterialFxGraphicModifier` に authored preset の初期注入と instance reuse を整理する。
3. `AnimationSpriteChannelPlayer` が既存 instance を優先利用するようにする。
4. Inspector / Explorer / codegen を stencil key に対応させる。
5. UI Mask / RectMask2D / reverse mask の実機確認を行う。

## 12. Acceptance Criteria

- UI `Graphic` の stencil 設定が MaterialFx preset から編集できる。
- Mask の内側 / 外側を明示的に切り替えられる。
- `AnimationSpriteChannel` の既存挙動が壊れない。
- 同一 target に対して MaterialFx instance が多重生成されない。
- release / reload / mask change 後も設定が安定して再現される。


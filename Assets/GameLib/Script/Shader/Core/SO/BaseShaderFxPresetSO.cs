#nullable enable
using System;
using System.Collections.Generic;
using Game.MaterialFx.Generated;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.MaterialFx
{
    [Flags]
    public enum OutlineDirectionMask
    {
        None = 0,
        Left = 1,
        Right = 2,
        Up = 4,
        Down = 8,
        All = Left | Right | Up | Down,
    }

    public enum OutlineAutoColorMode
    {
        Hsl = 0,
        HslPlus = 1,
    }

    [Flags]
    public enum TextOutlineDirectionMask
    {
        None = 0,
        Left = 1,
        Right = 2,
        Up = 4,
        Down = 8,
        All = Left | Right | Up | Down,
    }

    public enum TextOutlineAutoColorMode
    {
        Hsl = 0,
        HslPlus = 1,
    }

    /// <summary>
    /// BaseShader 専用の MaterialFx プリセット。
    /// Inspector で BaseShader の全プロパティを編集可能。
    /// フィールド変更時に AutoEntries を自動更新。
    /// 
    /// ## 概要
    /// このSOは、BaseShader の CompositeSystem プロパティを Inspector 上で
    /// 直感的に編集できるようにしたプリセットです。
    /// 各エフェクトの有効/無効を切り替え、パラメータを調整することで
    /// マテリアルエフェクトの組み合わせを保存・再利用できます。
    /// 
    /// ## テクスチャソース設定について
    /// - SlotType: テクスチャを取得するスロット (5=ExternalA, 6=ExternalB, 7=CustomRT)
    /// - Channel: 使用するチャンネル (R/G/B/A)
    /// - UVSpace: UV座標系 (0=SpriteLocal, 1=Screen, 2=TextureRaw, 3=WorldXY)
    /// </summary>
    [Serializable]
    public sealed class BaseShaderFxPreset : MaterialFxPresetDataBase
    {
        public enum BaseShaderRenderBlendPreset
        {
            Alpha = 0,
            Additive = 1,
            AdditiveAlpha = 2,
            Premultiply = 3,
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Dissolve (ディゾルブ効果)
        // ═══════════════════════════════════════════════════════════════════════════

        [TitleGroup("Dissolve", "テクスチャパターンに基づいてスプライトを徐々に消失させるエフェクト")]
        [ToggleLeft]
        [Tooltip("ディゾルブエフェクトを有効にする")]
        public bool dissolveEnabled = false;

        [TitleGroup("Dissolve")]
        [ShowIf(nameof(dissolveEnabled))]
        [Range(0f, 1f)]
        [Tooltip("消失の進行度。0=完全に表示、1=完全に消失")]
        public float dissolveThreshold = 0f;

        [TitleGroup("Dissolve")]
        [ShowIf(nameof(dissolveEnabled))]
        [Range(0f, 1f)]
        [Tooltip("消失エッジの幅。大きいほどグラデーションが広くなる")]
        public float dissolveEdgeWidth = 0.1f;

        [TitleGroup("Dissolve")]
        [ShowIf(nameof(dissolveEnabled))]
        [Tooltip("消失エッジの発光色")]
        public Color dissolveEdgeColor = Color.white;

        [TitleGroup("Dissolve/Source", "ディゾルブパターンを取得するテクスチャソース設定")]
        [ShowIf(nameof(dissolveEnabled))]
        [ValueDropdown(nameof(GetSlotTypeOptions))]
        [Tooltip("テクスチャスロット (ExtA/ExtB/CustomRT)")]
        public int dissolveSourceSlotType = 5;

        [TitleGroup("Dissolve/Source")]
        [ShowIf(nameof(dissolveEnabled))]
        [ValueDropdown(nameof(GetChannelOptions))]
        [Tooltip("使用するチャンネル (R/G/B/A)")]
        public int dissolveSourceChannel = 1;

        [TitleGroup("Dissolve/Source")]
        [ShowIf(nameof(dissolveEnabled))]
        [ValueDropdown(nameof(GetUVSpaceOptions))]
        [Tooltip("UV座標系 (SpriteLocal/Screen/TextureRaw/WorldXY)")]
        public int dissolveSourceUVSpace = 0;

        // ═══════════════════════════════════════════════════════════════════════════
        // Flow Warp (フロー歪み効果)
        // ═══════════════════════════════════════════════════════════════════════════

        [TitleGroup("Flow Warp", "テクスチャに基づいてUVを歪ませる水面・熱気流のようなエフェクト")]
        [ToggleLeft]
        [Tooltip("フローワープエフェクトを有効にする")]
        public bool flowWarpEnabled = false;

        [TitleGroup("Flow Warp")]
        [ShowIf(nameof(flowWarpEnabled))]
        [Tooltip("歪みの強度 (X, Y)。値が大きいほど歪みが強くなる")]
        public Vector2 flowWarpStrength = new Vector2(0.1f, 0.1f);

        [TitleGroup("Flow Warp")]
        [ShowIf(nameof(flowWarpEnabled))]
        [Tooltip("歪みアニメーションの速度")]
        public float flowWarpSpeed = 1f;

        [TitleGroup("Flow Warp/Source", "歪みパターンを取得するテクスチャソース設定")]
        [ShowIf(nameof(flowWarpEnabled))]
        [ValueDropdown(nameof(GetSlotTypeOptions))]
        [Tooltip("テクスチャスロット (ExtA/ExtB/CustomRT)")]
        public int flowWarpSourceSlotType = 5;

        [TitleGroup("Flow Warp/Source")]
        [ShowIf(nameof(flowWarpEnabled))]
        [ValueDropdown(nameof(GetChannelOptions))]
        [Tooltip("使用するチャンネル (R/G/B/A)")]
        public int flowWarpSourceChannel = 1;

        [TitleGroup("Flow Warp/Source")]
        [ShowIf(nameof(flowWarpEnabled))]
        [ValueDropdown(nameof(GetUVSpaceOptions))]
        [Tooltip("UV座標系 (SpriteLocal/Screen/TextureRaw/WorldXY)")]
        public int flowWarpSourceUVSpace = 0;

        // ═══════════════════════════════════════════════════════════════════════════
        // Color Overlay (カラーオーバーレイ)
        // ═══════════════════════════════════════════════════════════════════════════

        [TitleGroup("Color Overlay", "テクスチャマスクに基づいて色を重ねるエフェクト")]
        [ToggleLeft]
        [Tooltip("カラーオーバーレイを有効にする")]
        public bool colorOverlayEnabled = false;

        [TitleGroup("Color Overlay")]
        [ShowIf(nameof(colorOverlayEnabled))]
        [Tooltip("オーバーレイする色")]
        public Color colorOverlayColor = Color.white;

        [TitleGroup("Color Overlay")]
        [ShowIf(nameof(colorOverlayEnabled))]
        [ValueDropdown(nameof(GetBlendModeOptions))]
        [Tooltip("ブレンドモード (Normal/Multiply/Screen/Overlay/Add/SoftLight等)")]
        public int colorOverlayBlendMode = 0;

        [TitleGroup("Color Overlay")]
        [ShowIf(nameof(colorOverlayEnabled))]
        [Range(0f, 1f)]
        [Tooltip("エフェクトの強度。0=無効、1=最大")]
        public float colorOverlayIntensity = 1f;

        [TitleGroup("Color Overlay/Source", "オーバーレイマスクを取得するテクスチャソース設定")]
        [ShowIf(nameof(colorOverlayEnabled))]
        [ValueDropdown(nameof(GetSlotTypeOptions))]
        [Tooltip("テクスチャスロット (ExtA/ExtB/CustomRT)")]
        public int colorOverlaySourceSlotType = 5;

        [TitleGroup("Color Overlay/Source")]
        [ShowIf(nameof(colorOverlayEnabled))]
        [ValueDropdown(nameof(GetChannelOptions))]
        [Tooltip("使用するチャンネル (R/G/B/A)")]
        public int colorOverlaySourceChannel = 1;

        [TitleGroup("Color Overlay/Source")]
        [ShowIf(nameof(colorOverlayEnabled))]
        [ValueDropdown(nameof(GetUVSpaceOptions))]
        [Tooltip("UV座標系 (SpriteLocal/Screen/TextureRaw/WorldXY)")]
        public int colorOverlaySourceUVSpace = 0;

        // ═══════════════════════════════════════════════════════════════════════════
        // Color Ramp (カラーランプ)
        // ═══════════════════════════════════════════════════════════════════════════

        [TitleGroup("Color Ramp", "グレースケール値を元にカラーランプテクスチャで色を置換するエフェクト")]
        [ToggleLeft]
        [Tooltip("カラーランプエフェクトを有効にする")]
        public bool colorRampEnabled = false;

        [TitleGroup("Color Ramp")]
        [ShowIf(nameof(colorRampEnabled))]
        [Tooltip("カラーランプテクスチャ (1Dまたは横方向のグラデーション)")]
        public Texture? colorRampTexture;

        [TitleGroup("Color Ramp")]
        [ShowIf(nameof(colorRampEnabled))]
        [Range(0f, 1f)]
        [Tooltip("エフェクトの強度")]
        public float colorRampIntensity = 1f;

        [TitleGroup("Color Ramp")]
        [ShowIf(nameof(colorRampEnabled))]
        [ToggleLeft]
        [Tooltip("元のアルファ値を保持する")]
        public bool colorRampPreserveAlpha = true;

        [TitleGroup("Color Ramp/Source", "ランプ参照値を取得するテクスチャソース設定")]
        [ShowIf(nameof(colorRampEnabled))]
        [ValueDropdown(nameof(GetSlotTypeOptions))]
        [Tooltip("テクスチャスロット (ExtA/ExtB/CustomRT)")]
        public int colorRampSourceSlotType = 5;

        [TitleGroup("Color Ramp/Source")]
        [ShowIf(nameof(colorRampEnabled))]
        [ValueDropdown(nameof(GetChannelOptions))]
        [Tooltip("使用するチャンネル (R/G/B/A)")]
        public int colorRampSourceChannel = 1;

        [TitleGroup("Color Ramp/Source")]
        [ShowIf(nameof(colorRampEnabled))]
        [ValueDropdown(nameof(GetUVSpaceOptions))]
        [Tooltip("UV座標系 (SpriteLocal/Screen/TextureRaw/WorldXY)")]
        public int colorRampSourceUVSpace = 0;

        // ═══════════════════════════════════════════════════════════════════════════
        // Refraction (屈折効果)
        // ═══════════════════════════════════════════════════════════════════════════

        [TitleGroup("Refraction", "背景を歪ませる屈折エフェクト (ガラス、水、熱気楼など)")]
        [ToggleLeft]
        [Tooltip("屈折エフェクトを有効にする")]
        public bool refractionEnabled = false;

        [TitleGroup("Refraction")]
        [ShowIf(nameof(refractionEnabled))]
        [Tooltip("屈折の強度 (X, Y)")]
        public Vector2 refractionStrength = new Vector2(0.1f, 0.1f);

        [TitleGroup("Refraction")]
        [ShowIf(nameof(refractionEnabled))]
        [Range(0f, 1f)]
        [Tooltip("色収差の強度。プリズム効果を追加")]
        public float refractionChromaticAberration = 0f;

        [TitleGroup("Refraction/Source", "屈折パターンを取得するテクスチャソース設定")]
        [ShowIf(nameof(refractionEnabled))]
        [ValueDropdown(nameof(GetSlotTypeOptions))]
        [Tooltip("テクスチャスロット (ExtA/ExtB/CustomRT)")]
        public int refractionSourceSlotType = 5;

        [TitleGroup("Refraction/Source")]
        [ShowIf(nameof(refractionEnabled))]
        [ValueDropdown(nameof(GetChannelOptions))]
        [Tooltip("使用するチャンネル (R/G/B/A)")]
        public int refractionSourceChannel = 1;

        [TitleGroup("Refraction/Source")]
        [ShowIf(nameof(refractionEnabled))]
        [ValueDropdown(nameof(GetUVSpaceOptions))]
        [Tooltip("UV座標系 (SpriteLocal/Screen/TextureRaw/WorldXY)")]
        public int refractionSourceUVSpace = 0;

        // ═══════════════════════════════════════════════════════════════════════════
        // Caustics (コースティクス/水面の光屈折模様)
        // ═══════════════════════════════════════════════════════════════════════════

        [TitleGroup("Caustics", "水面を通した光の屈折パターンを描画するエフェクト")]
        [ToggleLeft]
        [Tooltip("コースティクスエフェクトを有効にする")]
        public bool causticsEnabled = false;

        [TitleGroup("Caustics")]
        [ShowIf(nameof(causticsEnabled))]
        [Tooltip("コースティクスの発光色")]
        public Color causticsColor = Color.white;

        [TitleGroup("Caustics")]
        [ShowIf(nameof(causticsEnabled))]
        [Range(0f, 5f)]
        [Tooltip("発光の強度")]
        public float causticsIntensity = 1f;

        [TitleGroup("Caustics")]
        [ShowIf(nameof(causticsEnabled))]
        [Range(0f, 1f)]
        [Tooltip("パターンが表示されるしきい値")]
        public float causticsThreshold = 0.5f;

        [TitleGroup("Caustics")]
        [ShowIf(nameof(causticsEnabled))]
        [Range(0f, 1f)]
        [Tooltip("エッジのぼかし具合")]
        public float causticsSoftness = 0.1f;

        [TitleGroup("Caustics")]
        [ShowIf(nameof(causticsEnabled))]
        [Tooltip("パターンAのスクロール速度 (X, Y)")]
        public Vector2 causticsScrollA = new Vector2(0.1f, 0.1f);

        [TitleGroup("Caustics")]
        [ShowIf(nameof(causticsEnabled))]
        [Tooltip("パターンBのスクロール速度 (X, Y)。Aと異なる方向にすると自然な動きになる")]
        public Vector2 causticsScrollB = new Vector2(-0.1f, 0.05f);

        [TitleGroup("Caustics/Source A", "コースティクスパターンA のテクスチャソース")]
        [ShowIf(nameof(causticsEnabled))]
        [ValueDropdown(nameof(GetSlotTypeOptions))]
        [Tooltip("テクスチャスロット")]
        public int causticsSourceASlotType = 5;

        [TitleGroup("Caustics/Source A")]
        [ShowIf(nameof(causticsEnabled))]
        [ValueDropdown(nameof(GetChannelOptions))]
        [Tooltip("使用するチャンネル")]
        public int causticsSourceAChannel = 0;

        [TitleGroup("Caustics/Source A")]
        [ShowIf(nameof(causticsEnabled))]
        [ValueDropdown(nameof(GetUVSpaceOptions))]
        [Tooltip("UV座標系")]
        public int causticsSourceAUVSpace = 0;

        [TitleGroup("Caustics/Source B", "コースティクスパターンB のテクスチャソース (AとBが乗算される)")]
        [ShowIf(nameof(causticsEnabled))]
        [ValueDropdown(nameof(GetSlotTypeOptions))]
        [Tooltip("テクスチャスロット")]
        public int causticsSourceBSlotType = 6;

        [TitleGroup("Caustics/Source B")]
        [ShowIf(nameof(causticsEnabled))]
        [ValueDropdown(nameof(GetChannelOptions))]
        [Tooltip("使用するチャンネル")]
        public int causticsSourceBChannel = 0;

        [TitleGroup("Caustics/Source B")]
        [ShowIf(nameof(causticsEnabled))]
        [ValueDropdown(nameof(GetUVSpaceOptions))]
        [Tooltip("UV座標系")]
        public int causticsSourceBUVSpace = 0;

        // ═══════════════════════════════════════════════════════════════════════════
        // Ripple (波紋効果)
        // ═══════════════════════════════════════════════════════════════════════════

        [TitleGroup("Ripple", "中心から広がる波紋エフェクト")]
        [ToggleLeft]
        [Tooltip("波紋エフェクトを有効にする")]
        public bool rippleEnabled = false;

        [TitleGroup("Ripple")]
        [ShowIf(nameof(rippleEnabled))]
        [Tooltip("波紋の中心座標 (UV空間: 0-1)")]
        public Vector2 rippleCenter = new Vector2(0.5f, 0.5f);

        [TitleGroup("Ripple")]
        [ShowIf(nameof(rippleEnabled))]
        [Tooltip("波のパラメータ: X=周波数, Y=振幅, Z=減衰, W=速度")]
        public Vector4 rippleWaveParams = new Vector4(10f, 0.05f, 2f, 5f);

        [TitleGroup("Ripple")]
        [ShowIf(nameof(rippleEnabled))]
        [Range(0f, 1f)]
        [Tooltip("波紋の振幅 (歪みの強さ)")]
        public float rippleAmplitude = 0.1f;

        [TitleGroup("Ripple")]
        [ShowIf(nameof(rippleEnabled))]
        [Tooltip("波紋のフェーズオフセット (アニメーション制御用)")]
        public float ripplePhase = 0f;

        [TitleGroup("Ripple")]
        [ShowIf(nameof(rippleEnabled))]
        [ToggleLeft]
        [Tooltip("UVを歪ませる (無効にすると色のみ変化)")]
        public bool rippleDistortUV = true;

        [TitleGroup("Ripple")]
        [ShowIf(nameof(rippleEnabled))]
        [Range(0f, 1f)]
        [Tooltip("波紋色のブレンド量")]
        public float rippleColorBlend = 0f;

        [TitleGroup("Ripple")]
        [ShowIf(nameof(rippleEnabled))]
        [Tooltip("波紋のハイライト色")]
        public Color rippleColor = Color.white;

        // ═══════════════════════════════════════════════════════════════════════════
        // Hue Shift (色相シフト)
        // ═══════════════════════════════════════════════════════════════════════════

        [TitleGroup("Hue Shift", "色相・彩度・明度を調整するエフェクト")]
        [ToggleLeft]
        [Tooltip("色相シフトエフェクトを有効にする")]
        public bool hueShiftEnabled = false;

        [TitleGroup("Hue Shift")]
        [ShowIf(nameof(hueShiftEnabled))]
        [Range(-1f, 1f)]
        [Tooltip("色相のシフト量 (-1 ~ 1で色相環を一周)")]
        public float hueShiftAmount = 0f;

        [TitleGroup("Hue Shift")]
        [ShowIf(nameof(hueShiftEnabled))]
        [Range(-1f, 1f)]
        [Tooltip("彩度の調整量 (-1=モノクロ, 0=変化なし, 1=最大彩度)")]
        public float hueSaturationMod = 0f;

        [TitleGroup("Hue Shift")]
        [ShowIf(nameof(hueShiftEnabled))]
        [Range(-1f, 1f)]
        [Tooltip("明度の調整量 (-1=真っ黒, 0=変化なし, 1=真っ白)")]
        public float hueValueMod = 0f;

        [TitleGroup("Hue Shift/Mask Source", "色相シフトの適用範囲を制御するマスクテクスチャ")]
        [ShowIf(nameof(hueShiftEnabled))]
        [ValueDropdown(nameof(GetSlotTypeOptions))]
        [Tooltip("テクスチャスロット")]
        public int hueShiftMaskSlotType = 0;

        [TitleGroup("Hue Shift/Mask Source")]
        [ShowIf(nameof(hueShiftEnabled))]
        [ValueDropdown(nameof(GetChannelOptions))]
        [Tooltip("使用するチャンネル")]
        public int hueShiftMaskChannel = 0;

        [TitleGroup("Hue Shift/Mask Source")]
        [ShowIf(nameof(hueShiftEnabled))]
        [ValueDropdown(nameof(GetUVSpaceOptions))]
        [Tooltip("UV座標系")]
        public int hueShiftMaskUVSpace = 0;

        // ═══════════════════════════════════════════════════════════════════════════
        // Normal Map (ノーマルマップ)
        // ═══════════════════════════════════════════════════════════════════════════

        [TitleGroup("Normal Map", "擬似的な立体感を与えるノーマルマップライティング")]
        [ToggleLeft]
        [Tooltip("ノーマルマップエフェクトを有効にする")]
        public bool normalMapEnabled = false;

        [TitleGroup("Normal Map")]
        [ShowIf(nameof(normalMapEnabled))]
        [Range(0f, 2f)]
        [Tooltip("ノーマルマップの強度")]
        public float normalMapStrength = 1f;

        [TitleGroup("Normal Map")]
        [ShowIf(nameof(normalMapEnabled))]
        [Tooltip("ライトの方向ベクトル (正規化推奨)")]
        public Vector3 normalMapLightDir = new Vector3(0f, 0f, 1f);

        [TitleGroup("Normal Map/Source", "ノーマルマップテクスチャのソース")]
        [ShowIf(nameof(normalMapEnabled))]
        [ValueDropdown(nameof(GetSlotTypeOptions))]
        [Tooltip("テクスチャスロット")]
        public int normalMapSourceSlotType = 5;

        [TitleGroup("Normal Map/Source")]
        [ShowIf(nameof(normalMapEnabled))]
        [ValueDropdown(nameof(GetChannelOptions))]
        [Tooltip("使用するチャンネル")]
        public int normalMapSourceChannel = 1;

        [TitleGroup("Normal Map/Source")]
        [ShowIf(nameof(normalMapEnabled))]
        [ValueDropdown(nameof(GetUVSpaceOptions))]
        [Tooltip("UV座標系")]
        public int normalMapSourceUVSpace = 0;

        // ═══════════════════════════════════════════════════════════════════════════
        // Emission (発光)
        // ═══════════════════════════════════════════════════════════════════════════

        [TitleGroup("Emission", "スプライトに発光効果を追加")]
        [ToggleLeft]
        [Tooltip("発光エフェクトを有効にする")]
        public bool emissionEnabled = false;

        [TitleGroup("Emission")]
        [ShowIf(nameof(emissionEnabled))]
        [Tooltip("発光色 (HDRカラー推奨)")]
        public Color emissionColor = Color.white;

        [TitleGroup("Emission")]
        [ShowIf(nameof(emissionEnabled))]
        [Range(0f, 10f)]
        [Tooltip("発光の強度")]
        public float emissionIntensity = 1f;

        // ═══════════════════════════════════════════════════════════════════════════
        // Mask (マスク)
        // ═══════════════════════════════════════════════════════════════════════════

        [TitleGroup("Mask", "テクスチャベースのアルファマスク")]
        [ToggleLeft]
        [Tooltip("マスクエフェクトを有効にする")]
        public bool maskEnabled = false;

        [TitleGroup("Mask")]
        [ShowIf(nameof(maskEnabled))]
        [Range(0f, 1f)]
        [Tooltip("マスクのしきい値。この値より高いマスク値の部分が表示される")]
        public float maskThreshold = 0.5f;

        [TitleGroup("Mask")]
        [ShowIf(nameof(maskEnabled))]
        [Range(0f, 1f)]
        [Tooltip("マスク境界のソフトネス。0=ハードエッジ、1=フルソフト")]
        public float maskSoftness = 0.1f;

        [TitleGroup("Mask/Source", "マスクテクスチャのソース")]
        [ShowIf(nameof(maskEnabled))]
        [ValueDropdown(nameof(GetSlotTypeOptions))]
        [Tooltip("テクスチャスロット")]
        public int maskSourceSlotType = 5;

        [TitleGroup("Mask/Source")]
        [ShowIf(nameof(maskEnabled))]
        [ValueDropdown(nameof(GetChannelOptions))]
        [Tooltip("使用するチャンネル")]
        public int maskSourceChannel = 1;

        [TitleGroup("Mask/Source")]
        [ShowIf(nameof(maskEnabled))]
        [ValueDropdown(nameof(GetUVSpaceOptions))]
        [Tooltip("UV座標系")]
        public int maskSourceUVSpace = 0;

        // ═══════════════════════════════════════════════════════════════════════════
        // Outline 2D
        // ═══════════════════════════════════════════════════════════════════════════

        [TitleGroup("Outline", "通常のアウトラインエフェクト")]
        [ToggleLeft]
        [Tooltip("通常アウトラインを有効にする")]
        public bool outlineEnabled = false;

        [TitleGroup("Outline")]
        [ShowIf(nameof(outlineEnabled))]
        [LabelText("Mode")]
        [ValueDropdown(nameof(GetOutlineModeOptions))]
        [Tooltip("Outside は外側、Inside は内側へ描画します")]
        public int outlineMode = 10;

        [TitleGroup("Outline")]
        [ShowIf(nameof(outlineEnabled))]
        [Tooltip("アウトライン色。Auto Color 有効時は最終アウトライン色への Tint として扱います")]
        public Color outlineColor = Color.white;

        [TitleGroup("Outline")]
        [ShowIf(nameof(outlineEnabled))]
        [LabelText("Direction")]
        [EnumToggleButtons]
        [Tooltip("アウトラインを出す方向。複数選択可。上下左右をすべて選ぶと全方位になります")]
        public OutlineDirectionMask outlineDirectionMask = OutlineDirectionMask.All;

        [TitleGroup("Outline")]
        [ShowIf(nameof(outlineEnabled))]
        [LabelText("Auto Color")]
        [ToggleLeft]
        [Tooltip("現在の最終色からアウトライン色を自動計算します。Outline Color は Tint として残ります")]
        public bool outlineAutoColorEnabled = false;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAutoColorSettings))]
        [LabelText("Mode")]
        [EnumToggleButtons]
        [Tooltip("HSL は通常の加算オフセット、HSL+ は現在値の残り幅へ押し込む調整です")]
        public OutlineAutoColorMode outlineAutoColorMode = OutlineAutoColorMode.Hsl;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAutoColorSettings))]
        [LabelText("H")]
        [Range(-1f, 1f)]
        [Tooltip("自動計算色に対する Hue オフセットです。0 がデフォルトで、1 または -1 で 1 周します")]
        public float outlineAutoHue = 0f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAutoColorSettings))]
        [LabelText("S")]
        [Range(-1f, 1f)]
        [Tooltip("自動計算色に対する Saturation 調整です。0 がデフォルトです")]
        public float outlineAutoSaturation = 0f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAutoColorSettings))]
        [LabelText("L")]
        [Range(-1f, 1f)]
        [Tooltip("自動計算色に対する Lightness 調整です。0 がデフォルトです")]
        public float outlineAutoLightness = 0f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(outlineEnabled))]
        [LabelText("Animated Gradient")]
        [ToggleLeft]
        [Tooltip("アウトライン色を最終色近傍で滑らかに揺らします")]
        public bool outlineAnimatedGradientEnabled = false;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Pattern Type")]
        [ValueDropdown(nameof(GetAnimatedNoisePatternOptions))]
        public int outlineAnimatedGradientPatternType = 10;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Master Strength")]
        [Min(0f)]
        public float outlineAnimatedGradientMasterStrength = 1f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Noise Scale")]
        [Min(0.0001f)]
        public float outlineAnimatedGradientNoiseScale = 6f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Direction")]
        public Vector2 outlineAnimatedGradientNoiseDirection = new Vector2(1f, 0f);

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Speed")]
        public float outlineAnimatedGradientNoiseSpeed = 0.2f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Offset")]
        public Vector2 outlineAnimatedGradientNoiseOffset = Vector2.zero;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Rotation Speed")]
        public float outlineAnimatedGradientRotationSpeed = 0f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Pulse Amplitude")]
        [Min(0f)]
        public float outlineAnimatedGradientPulseAmplitude = 0f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Pulse Speed")]
        public float outlineAnimatedGradientPulseSpeed = 1f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Warp Pattern")]
        [ValueDropdown(nameof(GetAnimatedNoisePatternOptions))]
        [HideInInspector]
        public int outlineAnimatedGradientWarpPatternType = 10;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Warp Scale")]
        [Min(0.0001f)]
        public float outlineAnimatedGradientWarpScale = 2f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Warp Strength")]
        [Min(0f)]
        public float outlineAnimatedGradientWarpStrength = 0.1f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Warp Direction")]
        public Vector2 outlineAnimatedGradientWarpDirection = new Vector2(0.71f, 0.43f);

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Warp Speed")]
        public float outlineAnimatedGradientWarpSpeed = 0.35f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Loop Seconds")]
        [Min(0f)]
        public float outlineAnimatedGradientLoopSeconds = 0f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Octaves")]
        [Min(1f)]
        public float outlineAnimatedGradientOctaves = 4f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Lacunarity")]
        [Min(1f)]
        public float outlineAnimatedGradientLacunarity = 2f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Gain")]
        [Range(0f, 1f)]
        public float outlineAnimatedGradientGain = 0.5f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Cell Sharpness")]
        [Min(0.01f)]
        public float outlineAnimatedGradientCellSharpness = 1.5f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Pattern Contrast")]
        [Min(0f)]
        public float outlineAnimatedGradientPatternContrast = 1f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Hue Amp")]
        [Min(0f)]
        public float outlineAnimatedGradientHueAmplitude = 0.0025f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Sat Amp")]
        [Min(0f)]
        public float outlineAnimatedGradientSaturationAmplitude = 0.008f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(ShowOutlineAnimatedGradientSettings))]
        [LabelText("Light Amp")]
        [Min(0f)]
        public float outlineAnimatedGradientLightnessAmplitude = 0.015f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(outlineEnabled))]
        [Min(0f)]
        [Tooltip("アウトラインの太さ")]
        public float outlineWidth = 1f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(outlineEnabled))]
        [Range(0f, 1f)]
        [Tooltip("アウトラインの不透明度")]
        public float outlineOpacity = 1f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(outlineEnabled))]
        [Range(0f, 1f)]
        [Tooltip("アウトラインのソフトネス")]
        public float outlineSoftness = 0f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(outlineEnabled))]
        [LabelText("Blend Mode")]
        [ValueDropdown(nameof(GetOutlineBlendModeOptions))]
        [Tooltip("アウトラインの合成方法です")]
        public int outlineBlendMode = 10;

        [TitleGroup("Outline")]
        [ShowIf(nameof(outlineEnabled))]
        [ToggleLeft]
        [Tooltip("アウトライン幅をピクセルステップ単位に量子化します")]
        public bool outlinePixelPerfect = true;

        [TitleGroup("Outline")]
        [ShowIf(nameof(outlineEnabled))]
        [LabelText("Width Unit")]
        [ValueDropdown(nameof(GetOutlineWidthUnitOptions))]
        [Tooltip("太さの単位です。Texel はテクスチャ基準、Screen Pixel は画面ピクセル基準です")]
        public int outlineWidthUnit = 10;

        [TitleGroup("Outline")]
        [ShowIf(nameof(outlineEnabled))]
        [Min(0.0001f)]
        [Tooltip("Pixel Perfect 有効時の量子化ステップです")]
        public float outlinePixelStep = 1f;

        [TitleGroup("Outline")]
        [ShowIf(nameof(outlineEnabled))]
        [LabelText("Sample Pattern")]
        [ValueDropdown(nameof(GetOutlineSamplePatternOptions))]
        [Tooltip("サンプリングパターンです。Direction と組み合わせた場合、選択方向だけで評価します")]
        public int outlineSamplePattern = 10;

        [TitleGroup("Outline")]
        [ShowIf(nameof(outlineEnabled))]
        [ToggleLeft]
        [Tooltip("マスクや alpha factor を尊重してアウトライン alpha を計算します")]
        public bool outlineMaskRespect = true;

        [TitleGroup("Outline")]
        [ShowIf(nameof(outlineEnabled))]
        [ToggleLeft]
        [Tooltip("Auto Color 無効時のみ、現在色をアウトライン色へ乗算します")]
        public bool outlineUseVertexColor = false;

        [TitleGroup("Outline")]
        [ShowIf(nameof(outlineEnabled))]
        [ToggleLeft]
        [Tooltip("サンプルUVを sprite rect 内へ clamp します")]
        public bool outlineUvClampEnabled = true;

        [TitleGroup("Outline")]
        [ShowIf(nameof(outlineEnabled))]
        [LabelText("ZTest Mode")]
        [ValueDropdown(nameof(GetOutlineZTestModeOptions))]
        [Tooltip("予約フィールドです。現在は描画条件の指定に使います")]
        public int outlineZTestMode = 10;

        // ═══════════════════════════════════════════════════════════════════════════
        // Text FX (Outline / Shadow)
        // ═══════════════════════════════════════════════════════════════════════════

        [TitleGroup("Text Fx", "Text向けアウトライン/影エフェクト")]
        [TitleGroup("Text Fx/Outline")]
        [ToggleLeft]
        [Tooltip("テキストアウトラインを有効にする")]
        public bool textOutlineEnabled = false;

        [TitleGroup("Text Fx/Outline")]
        [ShowIf(nameof(textOutlineEnabled))]
        [Tooltip("アウトライン色。Auto Color 有効時は最終アウトライン色への Tint として扱います")]
        public Color textOutlineColor = Color.black;

        [TitleGroup("Text Fx/Outline")]
        [ShowIf(nameof(textOutlineEnabled))]
        [Min(0f)]
        [Tooltip("アウトラインの太さ (px)")]
        public float textOutlineThickness = 1f;

        [TitleGroup("Text Fx/Outline")]
        [ShowIf(nameof(textOutlineEnabled))]
        [Range(0f, 1f)]
        [Tooltip("アウトラインのソフトネス")]
        public float textOutlineSoftness = 0.1f;

        [TitleGroup("Text Fx/Outline")]
        [ShowIf(nameof(textOutlineEnabled))]
        [LabelText("Direction")]
        [EnumToggleButtons]
        [Tooltip("アウトラインを出す方向。複数選択可。上下左右をすべて選ぶと全方位になります")]
        public TextOutlineDirectionMask textOutlineDirectionMask = TextOutlineDirectionMask.All;

        [TitleGroup("Text Fx/Outline")]
        [ShowIf(nameof(textOutlineEnabled))]
        [LabelText("Auto Color")]
        [ToggleLeft]
        [Tooltip("現在のテキスト最終色からアウトライン色を自動計算します。Outline Color は Tint として残ります")]
        public bool textOutlineAutoColorEnabled = false;

        [TitleGroup("Text Fx/Outline")]
        [ShowIf(nameof(ShowTextOutlineAutoColorSettings))]
        [LabelText("Mode")]
        [EnumToggleButtons]
        [Tooltip("HSL は通常の加算オフセット、HSL+ は現在値の残り幅へ押し込む調整です")]
        public TextOutlineAutoColorMode textOutlineAutoColorMode = TextOutlineAutoColorMode.Hsl;

        [TitleGroup("Text Fx/Outline")]
        [ShowIf(nameof(ShowTextOutlineAutoColorSettings))]
        [LabelText("H")]
        [Range(-1f, 1f)]
        [Tooltip("自動計算色に対する Hue オフセットです。0 がデフォルトで、1 または -1 で 1 周します")]
        public float textOutlineAutoHue = 0f;

        [TitleGroup("Text Fx/Outline")]
        [ShowIf(nameof(ShowTextOutlineAutoColorSettings))]
        [LabelText("S")]
        [Range(-1f, 1f)]
        [Tooltip("自動計算色に対する Saturation 調整です。0 がデフォルトです")]
        public float textOutlineAutoSaturation = 0f;

        [TitleGroup("Text Fx/Outline")]
        [ShowIf(nameof(ShowTextOutlineAutoColorSettings))]
        [LabelText("L")]
        [Range(-1f, 1f)]
        [Tooltip("自動計算色に対する Lightness 調整です。0 がデフォルトです")]
        public float textOutlineAutoLightness = 0f;

        [TitleGroup("Text Fx/Shadow")]
        [ToggleLeft]
        [Tooltip("テキストシャドウを有効にする")]
        public bool textShadowEnabled = false;

        [TitleGroup("Text Fx/Shadow")]
        [ShowIf(nameof(textShadowEnabled))]
        [Tooltip("シャドウ色")]
        public Color textShadowColor = new Color(0, 0, 0, 0.5f);

        [TitleGroup("Text Fx/Shadow")]
        [ShowIf(nameof(textShadowEnabled))]
        [Tooltip("シャドウのオフセット (px)")]
        public Vector2 textShadowOffset = new Vector2(1f, -1f);

        [TitleGroup("Text Fx/Shadow")]
        [ShowIf(nameof(textShadowEnabled))]
        [Range(0f, 1f)]
        [Tooltip("シャドウのソフトネス")]
        public float textShadowSoftness = 0.1f;

        [TitleGroup("Text Fx/Glow")]
        [ToggleLeft]
        [Tooltip("テキストグローを有効にする")]
        public bool textGlowEnabled = false;

        [TitleGroup("Text Fx/Glow")]
        [ShowIf(nameof(textGlowEnabled))]
        [Tooltip("グロー色")]
        public Color textGlowColor = new Color(1f, 1f, 1f, 0.5f);

        [TitleGroup("Text Fx/Glow")]
        [ShowIf(nameof(textGlowEnabled))]
        [Min(0f)]
        [Tooltip("グローの太さ")]
        public float textGlowThickness = 2f;

        [TitleGroup("Text Fx/Glow")]
        [ShowIf(nameof(textGlowEnabled))]
        [Range(0f, 1f)]
        [Tooltip("グローのソフトネス")]
        public float textGlowSoftness = 0.2f;

        // --- BlendColor2D ---
        [TitleGroup("Blend Color", "方向グラデーション付きブレンドカラー")]
        [ToggleLeft]
        [Tooltip("BlendColor2D を有効にする")]
        public bool blendColor2DEnabled = false;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(blendColor2DEnabled))]
        [Tooltip("ブレンドに使う基準色。Animated Gradient 有効時はこの色近傍で揺れます")]
        public Color blendColor2DColor = Color.white;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(blendColor2DEnabled))]
        [Range(0f, 1f)]
        [LabelText("Intensity")]
        public float blendColor2DBlendIntensity = 0f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(blendColor2DEnabled))]
        [LabelText("Gradient Direction")]
        [ValueDropdown(nameof(GetBlendColorGradientDirectionOptions))]
        public int blendColor2DBlendGradDirection = 0;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(blendColor2DEnabled))]
        [Range(0f, 1f)]
        [LabelText("Gradient Amount")]
        public float blendColor2DBlendGradationAmount = 0f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(blendColor2DEnabled))]
        [Range(0f, 1f)]
        [LabelText("Softness")]
        public float blendColor2DBlendSoftness = 1f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(blendColor2DEnabled))]
        [LabelText("Blend Mode")]
        [ValueDropdown(nameof(GetBlendModeOptions))]
        public int blendColor2DBlendMode = 0;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(blendColor2DEnabled))]
        [LabelText("Animated Gradient")]
        [ToggleLeft]
        [Tooltip("BlendColor の基準色を最終色近傍で滑らかに揺らします")]
        public bool blendColor2DAnimatedGradientEnabled = false;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Pattern Type")]
        [ValueDropdown(nameof(GetAnimatedNoisePatternOptions))]
        public int blendColor2DAnimatedGradientPatternType = 10;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Master Strength")]
        [Min(0f)]
        public float blendColor2DAnimatedGradientMasterStrength = 1f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Noise Scale")]
        [Min(0.0001f)]
        public float blendColor2DAnimatedGradientNoiseScale = 6f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Direction")]
        public Vector2 blendColor2DAnimatedGradientNoiseDirection = new Vector2(1f, 0f);

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Speed")]
        public float blendColor2DAnimatedGradientNoiseSpeed = 0.2f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Offset")]
        public Vector2 blendColor2DAnimatedGradientNoiseOffset = Vector2.zero;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Rotation Speed")]
        public float blendColor2DAnimatedGradientRotationSpeed = 0f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Pulse Amplitude")]
        [Min(0f)]
        public float blendColor2DAnimatedGradientPulseAmplitude = 0f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Pulse Speed")]
        public float blendColor2DAnimatedGradientPulseSpeed = 1f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Warp Pattern")]
        [ValueDropdown(nameof(GetAnimatedNoisePatternOptions))]
        [HideInInspector]
        public int blendColor2DAnimatedGradientWarpPatternType = 10;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Warp Scale")]
        [Min(0.0001f)]
        public float blendColor2DAnimatedGradientWarpScale = 2f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Warp Strength")]
        [Min(0f)]
        public float blendColor2DAnimatedGradientWarpStrength = 0.1f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Warp Direction")]
        public Vector2 blendColor2DAnimatedGradientWarpDirection = new Vector2(0.71f, 0.43f);

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Warp Speed")]
        public float blendColor2DAnimatedGradientWarpSpeed = 0.35f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Loop Seconds")]
        [Min(0f)]
        public float blendColor2DAnimatedGradientLoopSeconds = 0f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Octaves")]
        [Min(1f)]
        public float blendColor2DAnimatedGradientOctaves = 4f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Lacunarity")]
        [Min(1f)]
        public float blendColor2DAnimatedGradientLacunarity = 2f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Gain")]
        [Range(0f, 1f)]
        public float blendColor2DAnimatedGradientGain = 0.5f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Cell Sharpness")]
        [Min(0.01f)]
        public float blendColor2DAnimatedGradientCellSharpness = 1.5f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Pattern Contrast")]
        [Min(0f)]
        public float blendColor2DAnimatedGradientPatternContrast = 1f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Hue Amp")]
        [Min(0f)]
        public float blendColor2DAnimatedGradientHueAmplitude = 0.0025f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Sat Amp")]
        [Min(0f)]
        public float blendColor2DAnimatedGradientSaturationAmplitude = 0.008f;

        [TitleGroup("Blend Color")]
        [ShowIf(nameof(ShowBlendColorAnimatedGradientSettings))]
        [LabelText("Light Amp")]
        [Min(0f)]
        public float blendColor2DAnimatedGradientLightnessAmplitude = 0.015f;

        // --- AdvancedFade2D ---
        [TitleGroup("AdvancedFade", "ワイプ / 境界グロー / Burn")]
        [ToggleLeft]
        [Tooltip("AdvancedFade2D のフェード本体を有効にする")]
        public bool advancedFadeEnabled = false;

        [TitleGroup("AdvancedFade")]
        [ShowIf(nameof(advancedFadeEnabled))]
        [LabelText("Fade Direction")]
        [ValueDropdown(nameof(GetAdvancedFadeDirectionOptions))]
        public int advancedFadeFadeDirection = 0;

        [TitleGroup("AdvancedFade")]
        [ShowIf(nameof(advancedFadeEnabled))]
        [Range(0f, 1f)]
        [LabelText("Fade Amount")]
        public float advancedFadeFadeAmount = 0f;

        [TitleGroup("AdvancedFade")]
        [ShowIf(nameof(advancedFadeEnabled))]
        [Range(0f, 1f)]
        [LabelText("Softness")]
        public float advancedFadeSoft = 0.1f;

        [TitleGroup("AdvancedFade")]
        [ShowIf(nameof(advancedFadeEnabled))]
        [Min(0f)]
        [LabelText("Glow Intensity")]
        public float advancedFadeGlowIntensity = 0f;

        [TitleGroup("AdvancedFade")]
        [ShowIf(nameof(advancedFadeEnabled))]
        [Min(0f)]
        [LabelText("Glow Range")]
        public float advancedFadeGlowRange = 0.05f;

        [TitleGroup("AdvancedFade")]
        [ShowIf(nameof(advancedFadeEnabled))]
        [LabelText("Glow Blend")]
        [ValueDropdown(nameof(GetRainbowBlendModeOptions))]
        public int advancedFadeGlowBlendMode = 0;

        [TitleGroup("AdvancedFade")]
        [ShowIf(nameof(advancedFadeEnabled))]
        [LabelText("Wave Params A")]
        public Vector4 advancedFadeWaveParamsA = Vector4.zero;

        [TitleGroup("AdvancedFade")]
        [ShowIf(nameof(advancedFadeEnabled))]
        [LabelText("Wave Params B")]
        public Vector4 advancedFadeWaveParamsB = Vector4.zero;

        [TitleGroup("AdvancedFade")]
        [ShowIf(nameof(ShowAdvancedFadeCircleSettings))]
        [LabelText("Circle Start Angle")]
        public float advancedFadeCircleStartAngleDeg = 90f;

        [TitleGroup("AdvancedFade")]
        [ShowIf(nameof(ShowAdvancedFadeCircleSettings))]
        [LabelText("Circle Clockwise")]
        [ToggleLeft]
        public bool advancedFadeCircleClockwise = true;

        // --- Rainbow2D ---
        [TitleGroup("Rainbow", "虹色演出")]
        [ToggleLeft]
        public bool rainbowEnabled = false;

        [TitleGroup("Rainbow")]
        [ShowIf(nameof(rainbowEnabled))]
        [ValueDropdown(nameof(GetRainbowModeOptions))]
        public int rainbowMode = 0;

        [TitleGroup("Rainbow")]
        [ShowIf(nameof(rainbowEnabled))]
        [ValueDropdown(nameof(GetRainbowPatternOptions))]
        public int rainbowPattern = 0;

        [TitleGroup("Rainbow")]
        [ShowIf(nameof(rainbowEnabled))]
        public Vector2 rainbowDirection = new Vector2(1f, 0f);

        [TitleGroup("Rainbow")]
        [ShowIf(nameof(rainbowEnabled))]
        public float rainbowScale = 1f;

        [TitleGroup("Rainbow")]
        [ShowIf(nameof(rainbowEnabled))]
        public float rainbowOffset = 0f;

        [TitleGroup("Rainbow")]
        [ShowIf(nameof(rainbowEnabled))]
        public float rainbowSpeed = 0.5f;

        [TitleGroup("Rainbow")]
        [ShowIf(nameof(rainbowEnabled))]
        public float rainbowPixelSize = 2f;

        [TitleGroup("Rainbow")]
        [ShowIf(nameof(rainbowEnabled))]
        [Range(0f, 1f)]
        public float rainbowIntensity = 0.5f;

        [TitleGroup("Rainbow")]
        [ShowIf(nameof(rainbowEnabled))]
        [ValueDropdown(nameof(GetRainbowBlendModeOptions))]
        public int rainbowBlendMode = 1;

        // --- AdvancedFade2D Burn ---
        [TitleGroup("AdvancedFade", "焼け消え(Noise)")]
        [TitleGroup("AdvancedFade/Burn")]
        [ToggleLeft]
        public bool advancedFadeBurnEnabled = false;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(advancedFadeBurnEnabled))]
        [Range(0f, 1f)]
        public float advancedFadeBurnProgress = 0f;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(advancedFadeBurnEnabled))]
        [Range(0f, 0.5f)]
        public float advancedFadeBurnEdgeWidth = 0.1f;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(advancedFadeBurnEnabled))]
        public float advancedFadeBurnNoiseScale = 4f;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(advancedFadeBurnEnabled))]
        [Range(0f, 1f)]
        public float advancedFadeBurnNoiseStrength = 0.5f;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(advancedFadeBurnEnabled))]
        [ValueDropdown(nameof(GetBurnNoiseTypeOptions))]
        public int advancedFadeBurnNoiseType = 10;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(advancedFadeBurnEnabled))]
        public Vector2 advancedFadeBurnDirection = new Vector2(0f, 1f);

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(advancedFadeBurnEnabled))]
        public Color advancedFadeBurnEdgeColor = new Color(1f, 0.5f, 0.1f, 1f);

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(advancedFadeBurnEnabled))]
        [ValueDropdown(nameof(GetRainbowBlendModeOptions))]
        public int advancedFadeBurnBlendMode = 0;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(advancedFadeBurnEnabled))]
        public bool advancedFadeBurnInvert = false;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(advancedFadeBurnEnabled))]
        [LabelText("Animated Noise")]
        [ToggleLeft]
        public bool advancedFadeBurnAnimatedNoiseEnabled = false;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(ShowAdvancedFadeBurnAnimatedNoiseSettings))]
        [LabelText("Pattern Type")]
        [ValueDropdown(nameof(GetAnimatedNoisePatternOptions))]
        public int advancedFadeBurnAnimatedNoisePatternType = 10;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(ShowAdvancedFadeBurnAnimatedNoiseSettings))]
        [LabelText("Direction")]
        public Vector2 advancedFadeBurnAnimatedNoiseDirection = new Vector2(1f, 0f);

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(ShowAdvancedFadeBurnAnimatedNoiseSettings))]
        [LabelText("Speed")]
        public float advancedFadeBurnAnimatedNoiseSpeed = 0.2f;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(ShowAdvancedFadeBurnAnimatedNoiseSettings))]
        [LabelText("Offset")]
        public Vector2 advancedFadeBurnAnimatedNoiseOffset = Vector2.zero;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(ShowAdvancedFadeBurnAnimatedNoiseSettings))]
        [LabelText("Rotation Speed")]
        public float advancedFadeBurnAnimatedNoiseRotationSpeed = 0f;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(ShowAdvancedFadeBurnAnimatedNoiseSettings))]
        [LabelText("Pulse Amplitude")]
        [Min(0f)]
        public float advancedFadeBurnAnimatedNoisePulseAmplitude = 0f;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(ShowAdvancedFadeBurnAnimatedNoiseSettings))]
        [LabelText("Pulse Speed")]
        public float advancedFadeBurnAnimatedNoisePulseSpeed = 1f;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(ShowAdvancedFadeBurnAnimatedNoiseSettings))]
        [LabelText("Warp Pattern")]
        [ValueDropdown(nameof(GetAnimatedNoisePatternOptions))]
        [HideInInspector]
        public int advancedFadeBurnAnimatedNoiseWarpPatternType = 10;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(ShowAdvancedFadeBurnAnimatedNoiseSettings))]
        [LabelText("Warp Scale")]
        [Min(0.0001f)]
        public float advancedFadeBurnAnimatedNoiseWarpScale = 2f;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(ShowAdvancedFadeBurnAnimatedNoiseSettings))]
        [LabelText("Warp Strength")]
        [Min(0f)]
        public float advancedFadeBurnAnimatedNoiseWarpStrength = 0.2f;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(ShowAdvancedFadeBurnAnimatedNoiseSettings))]
        [LabelText("Warp Direction")]
        public Vector2 advancedFadeBurnAnimatedNoiseWarpDirection = new Vector2(0.71f, 0.43f);

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(ShowAdvancedFadeBurnAnimatedNoiseSettings))]
        [LabelText("Warp Speed")]
        public float advancedFadeBurnAnimatedNoiseWarpSpeed = 0.35f;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(ShowAdvancedFadeBurnAnimatedNoiseSettings))]
        [LabelText("Loop Seconds")]
        [Min(0f)]
        public float advancedFadeBurnAnimatedNoiseLoopSeconds = 0f;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(ShowAdvancedFadeBurnAnimatedNoiseSettings))]
        [LabelText("Octaves")]
        [Min(1f)]
        public float advancedFadeBurnAnimatedNoiseOctaves = 4f;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(ShowAdvancedFadeBurnAnimatedNoiseSettings))]
        [LabelText("Lacunarity")]
        [Min(1f)]
        public float advancedFadeBurnAnimatedNoiseLacunarity = 2f;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(ShowAdvancedFadeBurnAnimatedNoiseSettings))]
        [LabelText("Gain")]
        [Range(0f, 1f)]
        public float advancedFadeBurnAnimatedNoiseGain = 0.5f;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(ShowAdvancedFadeBurnAnimatedNoiseSettings))]
        [LabelText("Cell Sharpness")]
        [Min(0.01f)]
        public float advancedFadeBurnAnimatedNoiseCellSharpness = 1.5f;

        [TitleGroup("AdvancedFade/Burn")]
        [ShowIf(nameof(ShowAdvancedFadeBurnAnimatedNoiseSettings))]
        [LabelText("Pattern Contrast")]
        [Min(0f)]
        public float advancedFadeBurnAnimatedNoisePatternContrast = 1f;

        // ═══════════════════════════════════════════════════════════════════════════
        // Render State (MeshRenderer/SpriteRenderer 共通)
        // ═══════════════════════════════════════════════════════════════════════════

        [TitleGroup("Render State", "描画ブレンド/カリング/ZWrite/Queueを制御。MeshRendererの加算演出に利用")]
        [Tooltip("全体の合成方式プリセット。通常表示は Alpha、光線や発光演出は Additive 系を選択します。")]
        [ValueDropdown(nameof(GetRenderStateBlendPresetOptions))]
        public BaseShaderRenderBlendPreset renderStateBlendPreset = BaseShaderRenderBlendPreset.Alpha;

        [TitleGroup("Render State")]
        [ToggleLeft]
        [Tooltip("有効にすると Src/Dst Blend を直接指定します。無効時は Preset から自動決定します。")]
        public bool renderStateUseCustomBlendFactors = false;

        [TitleGroup("Render State")]
        [ShowIf(nameof(renderStateUseCustomBlendFactors))]
        [LabelText("Src Blend")]
        [Tooltip("ソース側係数。通常は SrcAlpha、純加算なら One を使います。")]
        [ValueDropdown(nameof(GetRenderStateBlendFactorOptions))]
        public int renderStateSrcBlend = 5; // SrcAlpha

        [TitleGroup("Render State")]
        [ShowIf(nameof(renderStateUseCustomBlendFactors))]
        [LabelText("Dst Blend")]
        [Tooltip("デスティネーション側係数。通常は OneMinusSrcAlpha、純加算なら One を使います。")]
        [ValueDropdown(nameof(GetRenderStateBlendFactorOptions))]
        public int renderStateDstBlend = 10; // OneMinusSrcAlpha

        [TitleGroup("Render State")]
        [ToggleLeft]
        [Tooltip("通常はオフ。体積表現や遮蔽が必要な場合のみオン。")]
        public bool renderStateZWrite = false;

        [TitleGroup("Render State")]
        [ValueDropdown(nameof(GetRenderStateCullOptions))]
        [Tooltip("ポリゴン面の描画対象。板ポリのビーム演出では Off 推奨、片面メッシュでは Back が一般的です。")]
        public int renderStateCull = 0; // Off

        [TitleGroup("Render State")]
        [MinValue(-200)]
        [MaxValue(200)]
        [Tooltip("Transparent キュー基準の相対値。大きいほど後描画。重なり順を微調整します。")]
        public int renderStateQueueOffset = 0;

        [TitleGroup("Render State")]
        [Range(0f, 1f)]
        [Tooltip("最終出力の強度。加算が強すぎる場合に 0-1 で抑えます。")]
        public float renderStateBlendIntensity = 1f;

        // ═══════════════════════════════════════════════════════════════════════════
        // Auto Entries Generation
        // ═══════════════════════════════════════════════════════════════════════════

        protected override void OnRefreshAutoEntries()
        {
            ClearAutoEntries();

            var (presetSrcBlend, presetDstBlend) = ResolveRenderStateBlendFactors(renderStateBlendPreset);
            var srcBlend = renderStateUseCustomBlendFactors ? renderStateSrcBlend : presetSrcBlend;
            var dstBlend = renderStateUseCustomBlendFactors ? renderStateDstBlend : presetDstBlend;

            SetAutoEntry(MaterialFxKeys.BaseShader.RenderState.BlendPreset, MakeInt((int)renderStateBlendPreset));
            SetAutoEntry(MaterialFxKeys.BaseShader.RenderState.SrcBlend, MakeInt(srcBlend));
            SetAutoEntry(MaterialFxKeys.BaseShader.RenderState.DstBlend, MakeInt(dstBlend));
            SetAutoEntry(MaterialFxKeys.BaseShader.RenderState.ZWrite, MakeBool(renderStateZWrite));
            SetAutoEntry(MaterialFxKeys.BaseShader.RenderState.Cull, MakeInt(renderStateCull));
            SetAutoEntry(MaterialFxKeys.BaseShader.RenderState.QueueOffset, MakeInt(renderStateQueueOffset));
            SetAutoEntry(MaterialFxKeys.BaseShader.RenderState.BlendIntensity, MakeFloat(renderStateBlendIntensity));

            // --- Dissolve ---
            SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Dissolve.Enabled, MakeBool(dissolveEnabled));
            if (dissolveEnabled)
            {
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Dissolve.Threshold, MakeFloat(dissolveThreshold));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Dissolve.EdgeWidth, MakeFloat(dissolveEdgeWidth));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Dissolve.EdgeColor, MakeColor(dissolveEdgeColor));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Dissolve.Source.SlotType, MakeInt(dissolveSourceSlotType));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Dissolve.Source.Channel, MakeInt(dissolveSourceChannel));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Dissolve.Source.UVSpace, MakeInt(dissolveSourceUVSpace));
            }

            // --- Flow Warp ---
            SetAutoEntry(MaterialFxKeys.BaseShader.FlowWarp.Enabled, MakeBool(flowWarpEnabled));
            if (flowWarpEnabled)
            {
                SetAutoEntry(MaterialFxKeys.BaseShader.FlowWarp.Strength, MakeFloat2(flowWarpStrength));
                SetAutoEntry(MaterialFxKeys.BaseShader.FlowWarp.Speed, MakeFloat(flowWarpSpeed));
                SetAutoEntry(MaterialFxKeys.BaseShader.FlowWarp.Source.SlotType, MakeInt(flowWarpSourceSlotType));
                SetAutoEntry(MaterialFxKeys.BaseShader.FlowWarp.Source.Channel, MakeInt(flowWarpSourceChannel));
                SetAutoEntry(MaterialFxKeys.BaseShader.FlowWarp.Source.UVSpace, MakeInt(flowWarpSourceUVSpace));
            }

            // --- Color Overlay ---
            SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.ColorOverlay.Enabled, MakeBool(colorOverlayEnabled));
            if (colorOverlayEnabled)
            {
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.ColorOverlay.Color, MakeColor(colorOverlayColor));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.ColorOverlay.BlendMode, MakeInt(colorOverlayBlendMode));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.ColorOverlay.Intensity, MakeFloat(colorOverlayIntensity));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.ColorOverlay.Source.SlotType, MakeInt(colorOverlaySourceSlotType));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.ColorOverlay.Source.Channel, MakeInt(colorOverlaySourceChannel));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.ColorOverlay.Source.UVSpace, MakeInt(colorOverlaySourceUVSpace));
            }

            // --- Color Ramp ---
            SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.ColorRamp.Enabled, MakeBool(colorRampEnabled));
            if (colorRampEnabled)
            {
                // Guard against unassigned texture — avoid sending null textures that may be invalid for this feature.
                if (colorRampTexture == null)
                {
                }
                else
                {
                    SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.ColorRamp.Texture, MakeTexture(colorRampTexture));
                }

                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.ColorRamp.Intensity, MakeFloat(colorRampIntensity));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.ColorRamp.PreserveAlpha, MakeBool(colorRampPreserveAlpha));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.ColorRamp.Source.SlotType, MakeInt(colorRampSourceSlotType));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.ColorRamp.Source.Channel, MakeInt(colorRampSourceChannel));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.ColorRamp.Source.UVSpace, MakeInt(colorRampSourceUVSpace));
            }

            // --- Refraction ---
            SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Refraction.Enabled, MakeBool(refractionEnabled));
            if (refractionEnabled)
            {
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Refraction.Strength, MakeFloat2(refractionStrength));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Refraction.ChromaticAberration, MakeFloat(refractionChromaticAberration));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Refraction.Source.SlotType, MakeInt(refractionSourceSlotType));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Refraction.Source.Channel, MakeInt(refractionSourceChannel));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Refraction.Source.UVSpace, MakeInt(refractionSourceUVSpace));
            }

            // --- Caustics ---
            SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Caustics.Enabled, MakeBool(causticsEnabled));
            if (causticsEnabled)
            {
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Caustics.Color, MakeColor(causticsColor));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Caustics.Intensity, MakeFloat(causticsIntensity));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Caustics.Threshold, MakeFloat(causticsThreshold));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Caustics.Softness, MakeFloat(causticsSoftness));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Caustics.ScrollA, MakeFloat2(causticsScrollA));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Caustics.ScrollB, MakeFloat2(causticsScrollB));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Caustics.Source.A.SlotType, MakeInt(causticsSourceASlotType));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Caustics.Source.A.Channel, MakeInt(causticsSourceAChannel));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Caustics.Source.A.UVSpace, MakeInt(causticsSourceAUVSpace));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Caustics.Source.B.SlotType, MakeInt(causticsSourceBSlotType));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Caustics.Source.B.Channel, MakeInt(causticsSourceBChannel));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Caustics.Source.B.UVSpace, MakeInt(causticsSourceBUVSpace));
            }

            // --- Ripple ---
            SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Ripple.Enabled, MakeBool(rippleEnabled));
            if (rippleEnabled)
            {
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Ripple.Center, MakeFloat2(rippleCenter));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Ripple.WaveParams, MakeFloat4(rippleWaveParams));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Ripple.Amplitude, MakeFloat(rippleAmplitude));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Ripple.Phase, MakeFloat(ripplePhase));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Ripple.DistortUV, MakeBool(rippleDistortUV));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Ripple.ColorBlend, MakeFloat(rippleColorBlend));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.Ripple.Color, MakeColor(rippleColor));
            }

            // --- Hue Shift ---
            SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.HueShift.Enabled, MakeBool(hueShiftEnabled));
            if (hueShiftEnabled)
            {
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.HueShift.Amount, MakeFloat(hueShiftAmount));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.HueShift.SaturationMod, MakeFloat(hueSaturationMod));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.HueShift.ValueMod, MakeFloat(hueValueMod));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.HueShift.Mask.Source.SlotType, MakeInt(hueShiftMaskSlotType));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.HueShift.Mask.Source.Channel, MakeInt(hueShiftMaskChannel));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.HueShift.Mask.Source.UVSpace, MakeInt(hueShiftMaskUVSpace));
            }

            // --- Normal Map ---
            SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.NormalMap.Enabled, MakeBool(normalMapEnabled));
            if (normalMapEnabled)
            {
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.NormalMap.Strength, MakeFloat(normalMapStrength));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.NormalMap.LightDir, MakeFloat3(normalMapLightDir));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.NormalMap.Source.SlotType, MakeInt(normalMapSourceSlotType));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.NormalMap.Source.Channel, MakeInt(normalMapSourceChannel));
                SetAutoEntry(MaterialFxKeys.BaseShader.CompositeSystems.NormalMap.Source.UVSpace, MakeInt(normalMapSourceUVSpace));
            }

            // --- Emission ---
            SetAutoEntry(MaterialFxKeys.BaseShader.Emission.Enabled, MakeBool(emissionEnabled));
            if (emissionEnabled)
            {
                // ★修正: emissionColorのRGBとemissionIntensityを結合してColor(r,g,b,intensity)として設定
                // シェーダー側の_EmissionColorは rgb=color, a=intensity として扱われる
                var combinedEmissionColor = new Color(emissionColor.r, emissionColor.g, emissionColor.b, emissionIntensity);
                SetAutoEntry(MaterialFxKeys.BaseShader.Emission.Color, MakeColor(combinedEmissionColor));
            }

            // --- Mask ---
            SetAutoEntry(MaterialFxKeys.BaseShader.Mask.Enabled, MakeBool(maskEnabled));
            if (maskEnabled)
            {
                SetAutoEntry(MaterialFxKeys.BaseShader.Mask.Threshold, MakeFloat(maskThreshold));
                SetAutoEntry(MaterialFxKeys.BaseShader.Mask.Softness, MakeFloat(maskSoftness));
                SetAutoEntry(MaterialFxKeys.BaseShader.Mask.Source.SlotType, MakeInt(maskSourceSlotType));
                SetAutoEntry(MaterialFxKeys.BaseShader.Mask.Source.Channel, MakeInt(maskSourceChannel));
                SetAutoEntry(MaterialFxKeys.BaseShader.Mask.Source.UVSpace, MakeInt(maskSourceUVSpace));
            }

            // --- BlendColor2D ---
            SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.Enabled, MakeBool(blendColor2DEnabled));
            if (blendColor2DEnabled)
            {
                SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.Color, MakeColor(blendColor2DColor));
                SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.BlendIntensity, MakeFloat(blendColor2DBlendIntensity));
                SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.BlendGradDirection, MakeInt(blendColor2DBlendGradDirection));
                SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.BlendGradationAmount, MakeFloat(blendColor2DBlendGradationAmount));
                SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.BlendSoftness, MakeFloat(blendColor2DBlendSoftness));
                SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.BlendMode, MakeInt(blendColor2DBlendMode));
                SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.Enabled, MakeBool(blendColor2DAnimatedGradientEnabled));
                if (blendColor2DAnimatedGradientEnabled)
                {
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.PatternType, MakeInt(blendColor2DAnimatedGradientPatternType));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.MasterStrength, MakeFloat(blendColor2DAnimatedGradientMasterStrength));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.NoiseScale, MakeFloat(blendColor2DAnimatedGradientNoiseScale));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.NoiseDirection, MakeFloat2(blendColor2DAnimatedGradientNoiseDirection));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.NoiseSpeed, MakeFloat(blendColor2DAnimatedGradientNoiseSpeed));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.NoiseOffset, MakeFloat2(blendColor2DAnimatedGradientNoiseOffset));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.RotationSpeed, MakeFloat(blendColor2DAnimatedGradientRotationSpeed));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.PulseAmplitude, MakeFloat(blendColor2DAnimatedGradientPulseAmplitude));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.PulseSpeed, MakeFloat(blendColor2DAnimatedGradientPulseSpeed));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.WarpPatternType, MakeInt(0));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.WarpScale, MakeFloat(blendColor2DAnimatedGradientWarpScale));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.WarpStrength, MakeFloat(blendColor2DAnimatedGradientWarpStrength));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.WarpDirection, MakeFloat2(blendColor2DAnimatedGradientWarpDirection));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.WarpSpeed, MakeFloat(blendColor2DAnimatedGradientWarpSpeed));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.LoopSeconds, MakeFloat(blendColor2DAnimatedGradientLoopSeconds));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.Octaves, MakeFloat(blendColor2DAnimatedGradientOctaves));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.Lacunarity, MakeFloat(blendColor2DAnimatedGradientLacunarity));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.Gain, MakeFloat(blendColor2DAnimatedGradientGain));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.CellSharpness, MakeFloat(blendColor2DAnimatedGradientCellSharpness));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.PatternContrast, MakeFloat(blendColor2DAnimatedGradientPatternContrast));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.HueAmplitude, MakeFloat(blendColor2DAnimatedGradientHueAmplitude));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.SaturationAmplitude, MakeFloat(blendColor2DAnimatedGradientSaturationAmplitude));
                    SetAutoEntry(MaterialFxKeys.BaseShader.BlendColor2D.AnimatedGradient.LightnessAmplitude, MakeFloat(blendColor2DAnimatedGradientLightnessAmplitude));
                }
            }

            // --- Outline ---
            SetAutoEntry(MaterialFxKeys.BaseShader.Outline.Enabled, MakeBool(outlineEnabled));
            if (outlineEnabled)
            {
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.Mode, MakeInt(outlineMode));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.Color, MakeColor(outlineColor));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.DirectionMask, MakeInt((int)outlineDirectionMask));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AutoColorEnabled, MakeBool(outlineAutoColorEnabled));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AutoColorMode, MakeInt((int)outlineAutoColorMode));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AutoHue, MakeFloat(outlineAutoHue));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AutoSaturation, MakeFloat(outlineAutoSaturation));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AutoLightness, MakeFloat(outlineAutoLightness));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.Enabled, MakeBool(outlineAnimatedGradientEnabled));
                if (outlineAnimatedGradientEnabled)
                {
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.PatternType, MakeInt(outlineAnimatedGradientPatternType));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.MasterStrength, MakeFloat(outlineAnimatedGradientMasterStrength));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.NoiseScale, MakeFloat(outlineAnimatedGradientNoiseScale));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.NoiseDirection, MakeFloat2(outlineAnimatedGradientNoiseDirection));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.NoiseSpeed, MakeFloat(outlineAnimatedGradientNoiseSpeed));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.NoiseOffset, MakeFloat2(outlineAnimatedGradientNoiseOffset));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.RotationSpeed, MakeFloat(outlineAnimatedGradientRotationSpeed));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.PulseAmplitude, MakeFloat(outlineAnimatedGradientPulseAmplitude));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.PulseSpeed, MakeFloat(outlineAnimatedGradientPulseSpeed));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.WarpPatternType, MakeInt(0));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.WarpScale, MakeFloat(outlineAnimatedGradientWarpScale));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.WarpStrength, MakeFloat(outlineAnimatedGradientWarpStrength));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.WarpDirection, MakeFloat2(outlineAnimatedGradientWarpDirection));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.WarpSpeed, MakeFloat(outlineAnimatedGradientWarpSpeed));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.LoopSeconds, MakeFloat(outlineAnimatedGradientLoopSeconds));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.Octaves, MakeFloat(outlineAnimatedGradientOctaves));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.Lacunarity, MakeFloat(outlineAnimatedGradientLacunarity));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.Gain, MakeFloat(outlineAnimatedGradientGain));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.CellSharpness, MakeFloat(outlineAnimatedGradientCellSharpness));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.PatternContrast, MakeFloat(outlineAnimatedGradientPatternContrast));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.HueAmplitude, MakeFloat(outlineAnimatedGradientHueAmplitude));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.SaturationAmplitude, MakeFloat(outlineAnimatedGradientSaturationAmplitude));
                    SetAutoEntry(MaterialFxKeys.BaseShader.Outline.AnimatedGradient.LightnessAmplitude, MakeFloat(outlineAnimatedGradientLightnessAmplitude));
                }
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.Width, MakeFloat(outlineWidth));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.Opacity, MakeFloat(outlineOpacity));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.Softness, MakeFloat(outlineSoftness));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.BlendMode, MakeInt(outlineBlendMode));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.PixelPerfect, MakeBool(outlinePixelPerfect));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.WidthUnit, MakeInt(outlineWidthUnit));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.PixelStep, MakeFloat(outlinePixelStep));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.SamplePattern, MakeInt(outlineSamplePattern));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.MaskRespect, MakeBool(outlineMaskRespect));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.UseVertexColor, MakeBool(outlineUseVertexColor));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.UVClampEnabled, MakeBool(outlineUvClampEnabled));
                SetAutoEntry(MaterialFxKeys.BaseShader.Outline.ZTestMode, MakeInt(outlineZTestMode));
            }

            // --- Text Fx ---
            SetAutoEntry(MaterialFxKeys.BaseShader.TextFx.Outline.Enabled, MakeBool(textOutlineEnabled));
            if (textOutlineEnabled)
            {
                SetAutoEntry(MaterialFxKeys.BaseShader.TextFx.Outline.DirectionMask, MakeInt((int)textOutlineDirectionMask));
                SetAutoEntry(MaterialFxKeys.BaseShader.TextFx.Outline.AutoColorEnabled, MakeBool(textOutlineAutoColorEnabled));
                SetAutoEntry(MaterialFxKeys.BaseShader.TextFx.Outline.AutoColorMode, MakeInt((int)textOutlineAutoColorMode));
                SetAutoEntry(MaterialFxKeys.BaseShader.TextFx.Outline.AutoHue, MakeFloat(textOutlineAutoHue));
                SetAutoEntry(MaterialFxKeys.BaseShader.TextFx.Outline.AutoSaturation, MakeFloat(textOutlineAutoSaturation));
                SetAutoEntry(MaterialFxKeys.BaseShader.TextFx.Outline.AutoLightness, MakeFloat(textOutlineAutoLightness));
                SetAutoEntry(MaterialFxKeys.BaseShader.TextFx.Outline.Color, MakeColor(textOutlineColor));
                SetAutoEntry(MaterialFxKeys.BaseShader.TextFx.Outline.Thickness, MakeFloat(textOutlineThickness));
                SetAutoEntry(MaterialFxKeys.BaseShader.TextFx.Outline.Softness, MakeFloat(textOutlineSoftness));
            }

            SetAutoEntry(MaterialFxKeys.BaseShader.TextFx.Shadow.Enabled, MakeBool(textShadowEnabled));
            if (textShadowEnabled)
            {
                SetAutoEntry(MaterialFxKeys.BaseShader.TextFx.Shadow.Color, MakeColor(textShadowColor));
                SetAutoEntry(MaterialFxKeys.BaseShader.TextFx.Shadow.Offset, MakeFloat2(textShadowOffset));
                SetAutoEntry(MaterialFxKeys.BaseShader.TextFx.Shadow.Softness, MakeFloat(textShadowSoftness));
            }

            SetAutoEntry(MaterialFxKeys.BaseShader.TextFx.Glow.Enabled, MakeBool(textGlowEnabled));
            if (textGlowEnabled)
            {
                SetAutoEntry(MaterialFxKeys.BaseShader.TextFx.Glow.Color, MakeColor(textGlowColor));
                SetAutoEntry(MaterialFxKeys.BaseShader.TextFx.Glow.Thickness, MakeFloat(textGlowThickness));
                SetAutoEntry(MaterialFxKeys.BaseShader.TextFx.Glow.Softness, MakeFloat(textGlowSoftness));
            }

            // --- Rainbow2D ---
            SetAutoEntry(MaterialFxKeys.BaseShader.Rainbow2D.Enabled, MakeBool(rainbowEnabled));
            if (rainbowEnabled)
            {
                SetAutoEntry(MaterialFxKeys.BaseShader.Rainbow2D.Mode, MakeInt(rainbowMode));
                SetAutoEntry(MaterialFxKeys.BaseShader.Rainbow2D.Pattern, MakeInt(rainbowPattern));
                SetAutoEntry(MaterialFxKeys.BaseShader.Rainbow2D.Direction, MakeFloat2(rainbowDirection));
                SetAutoEntry(MaterialFxKeys.BaseShader.Rainbow2D.Scale, MakeFloat(rainbowScale));
                SetAutoEntry(MaterialFxKeys.BaseShader.Rainbow2D.Offset, MakeFloat(rainbowOffset));
                SetAutoEntry(MaterialFxKeys.BaseShader.Rainbow2D.Speed, MakeFloat(rainbowSpeed));
                SetAutoEntry(MaterialFxKeys.BaseShader.Rainbow2D.PixelSize, MakeFloat(rainbowPixelSize));
                SetAutoEntry(MaterialFxKeys.BaseShader.Rainbow2D.Intensity, MakeFloat(rainbowIntensity));
                SetAutoEntry(MaterialFxKeys.BaseShader.Rainbow2D.BlendMode, MakeInt(rainbowBlendMode));
            }

            // --- AdvancedFade2D ---
            SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Enabled, MakeBool(advancedFadeEnabled));
            if (advancedFadeEnabled)
            {
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.FadeDirection, MakeInt(advancedFadeFadeDirection));
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.FadeAmount, MakeFloat(advancedFadeFadeAmount));
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Soft, MakeFloat(advancedFadeSoft));
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.GlowIntensity, MakeFloat(advancedFadeGlowIntensity));
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.GlowRange, MakeFloat(advancedFadeGlowRange));
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.GlowBlendMode, MakeInt(advancedFadeGlowBlendMode));
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.WaveParamsA, MakeFloat4(advancedFadeWaveParamsA));
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.WaveParamsB, MakeFloat4(advancedFadeWaveParamsB));
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Circle.StartAngleDeg, MakeFloat(advancedFadeCircleStartAngleDeg));
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Circle.Clockwise, MakeBool(advancedFadeCircleClockwise));
            }

            // --- AdvancedFade2D Burn ---
            SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.Enabled, MakeBool(advancedFadeBurnEnabled));
            if (advancedFadeBurnEnabled)
            {
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.Progress, MakeFloat(advancedFadeBurnProgress));
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.EdgeWidth, MakeFloat(advancedFadeBurnEdgeWidth));
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.NoiseScale, MakeFloat(advancedFadeBurnNoiseScale));
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.NoiseStrength, MakeFloat(advancedFadeBurnNoiseStrength));
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.NoiseType, MakeInt(advancedFadeBurnNoiseType));
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.Direction, MakeFloat2(advancedFadeBurnDirection));
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.EdgeColor, MakeColor(advancedFadeBurnEdgeColor));
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.BlendMode, MakeInt(advancedFadeBurnBlendMode));
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.Invert, MakeBool(advancedFadeBurnInvert));
                SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.Enabled, MakeBool(advancedFadeBurnAnimatedNoiseEnabled));
                if (advancedFadeBurnAnimatedNoiseEnabled)
                {
                    SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.PatternType, MakeInt(advancedFadeBurnAnimatedNoisePatternType));
                    SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.Direction, MakeFloat2(advancedFadeBurnAnimatedNoiseDirection));
                    SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.Speed, MakeFloat(advancedFadeBurnAnimatedNoiseSpeed));
                    SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.Offset, MakeFloat2(advancedFadeBurnAnimatedNoiseOffset));
                    SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.RotationSpeed, MakeFloat(advancedFadeBurnAnimatedNoiseRotationSpeed));
                    SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.PulseAmplitude, MakeFloat(advancedFadeBurnAnimatedNoisePulseAmplitude));
                    SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.PulseSpeed, MakeFloat(advancedFadeBurnAnimatedNoisePulseSpeed));
                    SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.WarpPatternType, MakeInt(0));
                    SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.WarpScale, MakeFloat(advancedFadeBurnAnimatedNoiseWarpScale));
                    SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.WarpStrength, MakeFloat(advancedFadeBurnAnimatedNoiseWarpStrength));
                    SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.WarpDirection, MakeFloat2(advancedFadeBurnAnimatedNoiseWarpDirection));
                    SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.WarpSpeed, MakeFloat(advancedFadeBurnAnimatedNoiseWarpSpeed));
                    SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.LoopSeconds, MakeFloat(advancedFadeBurnAnimatedNoiseLoopSeconds));
                    SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.Octaves, MakeFloat(advancedFadeBurnAnimatedNoiseOctaves));
                    SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.Lacunarity, MakeFloat(advancedFadeBurnAnimatedNoiseLacunarity));
                    SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.Gain, MakeFloat(advancedFadeBurnAnimatedNoiseGain));
                    SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.CellSharpness, MakeFloat(advancedFadeBurnAnimatedNoiseCellSharpness));
                    SetAutoEntry(MaterialFxKeys.BaseShader.AdvancedFade2D.Burn.AnimatedNoise.PatternContrast, MakeFloat(advancedFadeBurnAnimatedNoisePatternContrast));
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // ValueDropdown Options (Odin Inspector用)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// テクスチャスロットタイプのドロップダウンオプション
        /// </summary>
        static ValueDropdownList<int> GetSlotTypeOptions()
        {
            return new ValueDropdownList<int>
            {
                { "External A", 5 },
                { "External B", 6 },
                { "Custom RT", 7 },
            };
        }

        /// <summary>
        /// チャンネル選択のドロップダウンオプション
        /// ★修正: シェーダー側の CHANNEL_* 定義はビットマスク (R=1, G=2, B=4, A=8)
        /// </summary>
        static ValueDropdownList<int> GetChannelOptions()
        {
            return new ValueDropdownList<int>
            {
                { "R (Red)", 1 },
                { "G (Green)", 2 },
                { "B (Blue)", 4 },
                { "A (Alpha)", 8 },
            };
        }

        static ValueDropdownList<int> GetRainbowModeOptions()
        {
            return new ValueDropdownList<int>
            {
                { "Gradient", 0 },
                { "Pixel", 1 },
            };
        }

        static ValueDropdownList<int> GetRainbowPatternOptions()
        {
            return new ValueDropdownList<int>
            {
                { "Horizontal", 0 },
                { "Vertical", 1 },
                { "Checker", 2 },
            };
        }

        static ValueDropdownList<int> GetRainbowBlendModeOptions()
        {
            return new ValueDropdownList<int>
            {
                { "Add", 0 },
                { "Screen", 1 },
                { "Overlay", 2 },
                { "Lerp", 3 },
            };
        }

        static ValueDropdownList<int> GetAnimatedNoisePatternOptions()
        {
            return new ValueDropdownList<int>
            {
                { "Smooth Value", 10 },
                { "Perlin", 20 },
                { "FBM", 30 },
                { "Ridged FBM", 40 },
                { "Cellular", 50 },
                { "Hex Cell", 60 },
                { "Turtle Shell", 70 },
                { "Checker", 80 },
                { "Stripes", 90 },
                { "Diamond", 100 },
                { "Truchet", 110 },
                { "Interference", 120 },
                { "Swirl", 130 },
            };
        }

        static ValueDropdownList<int> GetBurnNoiseTypeOptions() => GetAnimatedNoisePatternOptions();

        static ValueDropdownList<int> GetBlendColorGradientDirectionOptions()
        {
            return new ValueDropdownList<int>
            {
                { "None", 0 },
                { "Horizontal", 1 },
                { "Vertical", 2 },
                { "Radial", 3 },
            };
        }

        static ValueDropdownList<int> GetAdvancedFadeDirectionOptions()
        {
            return new ValueDropdownList<int>
            {
                { "Left To Right", 0 },
                { "Right To Left", 1 },
                { "Bottom To Top", 2 },
                { "Top To Bottom", 3 },
                { "Radial In", 4 },
                { "Radial Out", 5 },
                { "Circle", 6 },
            };
        }

        static (int src, int dst) ResolveRenderStateBlendFactors(BaseShaderRenderBlendPreset preset)
        {
            return preset switch
            {
                BaseShaderRenderBlendPreset.Additive => (1, 1),         // One, One
                BaseShaderRenderBlendPreset.AdditiveAlpha => (5, 1),    // SrcAlpha, One
                BaseShaderRenderBlendPreset.Premultiply => (1, 10),     // One, OneMinusSrcAlpha
                _ => (5, 10),                                           // SrcAlpha, OneMinusSrcAlpha
            };
        }

        static ValueDropdownList<BaseShaderRenderBlendPreset> GetRenderStateBlendPresetOptions()
        {
            return new ValueDropdownList<BaseShaderRenderBlendPreset>
            {
                { "Alpha", BaseShaderRenderBlendPreset.Alpha },
                { "Additive (One One)", BaseShaderRenderBlendPreset.Additive },
                { "Additive Alpha (SrcAlpha One)", BaseShaderRenderBlendPreset.AdditiveAlpha },
                { "Premultiply", BaseShaderRenderBlendPreset.Premultiply },
            };
        }

        static ValueDropdownList<int> GetRenderStateBlendFactorOptions()
        {
            return new ValueDropdownList<int>
            {
                { "Zero", 0 },
                { "One", 1 },
                { "DstColor", 2 },
                { "SrcColor", 3 },
                { "OneMinusDstColor", 4 },
                { "SrcAlpha", 5 },
                { "OneMinusSrcColor", 6 },
                { "DstAlpha", 7 },
                { "OneMinusDstAlpha", 8 },
                { "SrcAlphaSaturate", 9 },
                { "OneMinusSrcAlpha", 10 },
            };
        }

        static ValueDropdownList<int> GetRenderStateCullOptions()
        {
            return new ValueDropdownList<int>
            {
                { "Off", 0 },
                { "Front", 1 },
                { "Back", 2 },
            };
        }

        /// <summary>
        /// UV空間のドロップダウンオプション
        /// </summary>
        static ValueDropdownList<int> GetUVSpaceOptions()
        {
            return new ValueDropdownList<int>
            {
                { "Sprite Local", 0 },
                { "Screen", 1 },
                { "Texture Raw", 2 },
                { "World XY", 3 },
            };
        }

        /// <summary>
        /// ブレンドモードのドロップダウンオプション
        /// </summary>
        static ValueDropdownList<int> GetBlendModeOptions()
        {
            return new ValueDropdownList<int>
            {
                { "Normal", 0 },
                { "Multiply", 1 },
                { "Screen", 2 },
                { "Overlay", 3 },
                { "Add", 4 },
                { "Soft Light", 5 },
                { "Color Dodge", 6 },
                { "Color Burn", 7 },
                { "Darken", 8 },
                { "Lighten", 9 },
                { "Difference", 10 },
            };
        }

        static ValueDropdownList<int> GetOutlineModeOptions()
        {
            return new ValueDropdownList<int>
            {
                { "Outside", 10 },
                { "Inside", 20 },
            };
        }

        static ValueDropdownList<int> GetOutlineBlendModeOptions()
        {
            return new ValueDropdownList<int>
            {
                { "Alpha", 10 },
                { "Add", 20 },
                { "Screen", 30 },
            };
        }

        static ValueDropdownList<int> GetOutlineWidthUnitOptions()
        {
            return new ValueDropdownList<int>
            {
                { "Texel", 10 },
                { "Screen Pixel", 20 },
            };
        }

        static ValueDropdownList<int> GetOutlineSamplePatternOptions()
        {
            return new ValueDropdownList<int>
            {
                { "Diamond4", 10 },
                { "Box8", 20 },
                { "Circle12", 30 },
            };
        }

        static ValueDropdownList<int> GetOutlineZTestModeOptions()
        {
            return new ValueDropdownList<int>
            {
                { "LessEqual", 10 },
                { "Always", 20 },
            };
        }

        bool ShowOutlineAutoColorSettings => outlineEnabled && outlineAutoColorEnabled;
        bool ShowOutlineAnimatedGradientSettings => outlineEnabled && outlineAnimatedGradientEnabled;
        bool ShowBlendColorAnimatedGradientSettings => blendColor2DEnabled && blendColor2DAnimatedGradientEnabled;
        bool ShowTextOutlineAutoColorSettings => textOutlineEnabled && textOutlineAutoColorEnabled;
        bool ShowAdvancedFadeCircleSettings => advancedFadeEnabled && advancedFadeFadeDirection == 6;
        bool ShowAdvancedFadeBurnAnimatedNoiseSettings => advancedFadeBurnEnabled && advancedFadeBurnAnimatedNoiseEnabled;
    }

    [CreateAssetMenu(fileName = "BaseShaderFxPreset", menuName = "Game/MaterialFx/BaseShaderFxPreset")]
    public sealed class BaseShaderFxPresetSO : ScriptableObject
    {
        [SerializeReference, InlineProperty, HideLabel]
        [Tooltip("SO wrapper for BaseShaderFxPreset data.")]
        BaseShaderFxPreset preset = new();

        public BaseShaderFxPreset Preset => preset;

        public IReadOnlyList<MaterialFxPresetEntry> Entries
            => preset?.Entries ?? Array.Empty<MaterialFxPresetEntry>();

        public void RefreshEntries()
        {
            preset?.RefreshEntries();
        }

        void OnEnable()
        {
            preset?.MarkEntriesDirty();
        }

        void OnValidate()
        {
            preset?.MarkEntriesDirty();
        }
    }
}

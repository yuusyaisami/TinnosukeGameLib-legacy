#ifndef GAME_HUE_SHIFT_2D_INCLUDED
#define GAME_HUE_SHIFT_2D_INCLUDED

// ═══════════════════════════════════════════════════════════════════════════
// HueShift2D.hlsl - 色相シフトエフェクト (v3.0)
// ═══════════════════════════════════════════════════════════════════════════
//
// 仕様書 BaseShader-CompositeSystem-v3.0 準拠
//
// 機能:
//   - ノイズ値を使って色相を回転
//   - サイケデリック、虹色、オーラ変化などに使用
//   - 彩度・明度の調整も可能
//
// ═══════════════════════════════════════════════════════════════════════════

// TextureSlot2D.hlsl は Surface2D.hlsl 経由でインクルード想定
#include "Assets/GameLib/Script/Shader/Core/BaseShader/Features/ColorSpaceUtils.hlsl"

// ---------------------------------------------------------------------------
// HueShift パラメータ構造体
// ★ BaseShader.shader: HueShiftMaskSource (マスク付きシフト)
// ---------------------------------------------------------------------------
struct HueShift2DParams
{
    float          enabled;
    TextureSlotRef maskSource;    // マスクソース (slotType=255で無効=全面適用)
    
    // シフト量
    half           shiftAmount;     // シフト量（0-1 で 360度）
    half           saturationMod;   // 彩度調整（0=変化なし、+で増加、-で減少）
    half           valueMod;        // 明度調整（0=変化なし、+で増加、-で減少）
};

// ---------------------------------------------------------------------------
// CBUFFER から HueShift2DParams を生成（旧式互換）
// ---------------------------------------------------------------------------
inline HueShift2DParams MakeHueShift2DParams(
    float enabled,
    float sourceSlotType,
    float sourceChannel,
    float sourceUVSpace,
    float4 sourceTilingOffset,
    float4 sourceRemap,
    float shiftRange,
    float saturationMod,
    float valueMod,
    float timeSpeed)
{
    HueShift2DParams p = (HueShift2DParams)0;
    p.enabled = enabled;
    p.maskSource = MakeTextureSlotRef(
        sourceSlotType,
        sourceChannel,
        sourceUVSpace,
        sourceTilingOffset,
        sourceRemap
    );
    p.shiftAmount = shiftRange;
    p.saturationMod = saturationMod;
    p.valueMod = valueMod;
    return p;
}

// ---------------------------------------------------------------------------
// ★ Simplified: BaseShader.shader の実際のプロパティに合わせた簡易版
// HueShiftMaskSource: SlotType=255 は「マスクなし（全面適用）」を意味する
// ---------------------------------------------------------------------------
inline HueShift2DParams MakeHueShift2DParamsSimple(
    float enabled,
    float maskSlotType,
    float maskChannel,
    float maskUVSpace,
    float4 maskTilingOffset,
    float4 maskRemap,
    float shiftAmount,         // _HueShiftAmount
    float saturationMod,       // _HueSaturationMod (0=変化なし)
    float valueMod)            // _HueValueMod (0=変化なし)
{
    HueShift2DParams p = (HueShift2DParams)0;
    p.enabled = enabled;
    p.maskSource = MakeTextureSlotRef(
        maskSlotType,
        maskChannel,
        maskUVSpace,
        maskTilingOffset,
        maskRemap
    );
    p.shiftAmount = shiftAmount;
    p.saturationMod = saturationMod;
    p.valueMod = valueMod;
    return p;
}

// デフォルト値で初期化（無効状態）
inline HueShift2DParams MakeDefaultHueShift2DParams()
{
    HueShift2DParams p = (HueShift2DParams)0;
    p.enabled = 0;
    p.maskSource = MakeDefaultTextureSlotRef();
    p.maskSource.slotType = 255;  // マスクなし（全面適用）
    p.shiftAmount = 0.0h;
    p.saturationMod = 0.0h;
    p.valueMod = 0.0h;
    return p;
}

// ---------------------------------------------------------------------------
// HueShift 適用
// ★ 修正: マスクベースの適用ロジック
// ---------------------------------------------------------------------------
inline Surface2D Surface2D_ApplyHueShift(Surface2D s, HueShift2DParams p, float time)
{
    if (p.enabled < 0.5h)
        return s;
    
    // マスク値を取得（SlotType=255 は無効=全面適用）
    half maskValue = 1.0h;
    if (p.maskSource.slotType != 255 && p.maskSource.slotType != TEXTURE_SLOT_NONE)
    {
        maskValue = SampleSlotScalar(s, p.maskSource);
    }
    
    // RGB → HSV
    half3 hsv = RGBtoHSV(s.color);
    
    // 色相シフト（マスク値で強度を制御）
    hsv.x = frac(hsv.x + p.shiftAmount * maskValue);
    
    // 彩度・明度調整（加算方式：0=変化なし）
    hsv.y = saturate(hsv.y + p.saturationMod * maskValue);
    hsv.z = saturate(hsv.z + p.valueMod * maskValue);
    
    // HSV → RGB
    s.color = HSVtoRGB(hsv);
    
    return s;
}

// ---------------------------------------------------------------------------
// 固定シフト量での色相シフト（ノイズなし）
// ---------------------------------------------------------------------------
inline Surface2D Surface2D_ApplyHueShiftFixed(Surface2D s, half shiftAmount, half satMod, half valMod)
{
    half3 hsv = RGBtoHSV(s.color);
    hsv.x = frac(hsv.x + shiftAmount);
    hsv.y = saturate(hsv.y * satMod);
    hsv.z = saturate(hsv.z * valMod);
    s.color = HSVtoRGB(hsv);
    return s;
}

#endif // GAME_HUE_SHIFT_2D_INCLUDED

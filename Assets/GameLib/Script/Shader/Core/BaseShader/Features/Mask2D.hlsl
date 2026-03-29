#ifndef GAME_MASK_2D_INCLUDED
#define GAME_MASK_2D_INCLUDED

// ═══════════════════════════════════════════════════════════════════════════
// Mask2D.hlsl - アルファマスクエフェクト (v2.0)
// ═══════════════════════════════════════════════════════════════════════════
//
// 仕様書 BaseShader-CompositeSystem-v2.0 準拠
//
// 機能:
//   - TextureSlot からマスク値を読み取り
//   - Threshold を境界として、Softness でグラデーション
//   - アルファにマスクを乗算
//
// v2.0 変更点:
//   - binding 引数を削除
//   - TextureSlotRef から直接 external texture を解決
//   - 旧互換ラッパーを削除
//
// ═══════════════════════════════════════════════════════════════════════════

// TextureSlot2D.hlsl は Surface2D.hlsl 経由でインクルード想定

// ---------------------------------------------------------------------------
// Mask パラメータ構造体
// ---------------------------------------------------------------------------
struct Mask2DParams
{
    float          enabled;
    TextureSlotRef source;
    float          threshold;
    float          softness;
};

// ---------------------------------------------------------------------------
// CBUFFER から Mask2DParams を生成
// ---------------------------------------------------------------------------
inline Mask2DParams MakeMask2DParams(
    float enabled,
    float sourceSlotType,
    float sourceChannel,
    float sourceUVSpace,
    float4 sourceTilingOffset,
    float4 sourceRemap,
    float threshold,
    float softness)
{
    Mask2DParams p;
    p.enabled = enabled;
    p.source = MakeTextureSlotRef(
        sourceSlotType,
        sourceChannel,
        sourceUVSpace,
        sourceTilingOffset,
        sourceRemap
    );
    p.threshold = threshold;
    p.softness = max(softness, 0.001);  // ゼロ除算防止
    return p;
}

// デフォルト値で初期化（無効状態）
inline Mask2DParams MakeDefaultMask2DParams()
{
    Mask2DParams p;
    p.enabled = 0;
    p.source = MakeDefaultTextureSlotRef();
    p.threshold = 0.5;
    p.softness = 0.1;
    return p;
}

// ---------------------------------------------------------------------------
// Mask 適用
// ★v2.0: binding 引数なし
// ★修正: ノイズ値の全範囲でソフトマスクが機能するように
// ---------------------------------------------------------------------------
inline Surface2D Surface2D_ApplyMask(Surface2D s, Mask2DParams p)
{
    Surface2D result = s;
    if (p.enabled >= 0.5 && p.source.slotType != TEXTURE_SLOT_NONE)
    {
        half mask = SampleSlotScalar(result, p.source);
        half softHalf = p.softness * 0.5h;
        half alpha = smoothstep(p.threshold - softHalf, p.threshold + softHalf, mask);
        result.alpha *= alpha;
        result.alphaFactor *= alpha;
    }

    return result;
}

// ---------------------------------------------------------------------------
// Mask のみを適用（逆マスク版）
// マスク値が低い部分を残す
// ---------------------------------------------------------------------------
inline Surface2D Surface2D_ApplyInverseMask(Surface2D s, Mask2DParams p)
{
    if (p.enabled < 0.5)
        return s;
    if (p.source.slotType == TEXTURE_SLOT_NONE)
        return s;
    
    // ソースからマスク値を取得
    half mask = SampleSlotScalar(s, p.source);
    
    // 逆ソフトマスク
    half alpha = 1.0 - smoothstep(p.threshold - p.softness, p.threshold + p.softness, mask);
    s.alpha *= alpha;
    s.alphaFactor *= alpha;
    
    return s;
}

#endif // GAME_MASK_2D_INCLUDED

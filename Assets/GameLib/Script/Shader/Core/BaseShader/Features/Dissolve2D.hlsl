#ifndef GAME_DISSOLVE_2D_INCLUDED
#define GAME_DISSOLVE_2D_INCLUDED

// ═══════════════════════════════════════════════════════════════════════════
// Dissolve2D.hlsl - 溶解・消滅エフェクト (v2.0)
// ═══════════════════════════════════════════════════════════════════════════
//
// 仕様書 BaseShader-CompositeSystem-v2.0 準拠
//
// 機能:
//   - TextureSlot からノイズを読み、Threshold で溶解
//   - エッジに発光色を乗せる
//   - EdgeWidth でエッジの幅を制御
//
// v2.0 変更点:
//   - binding 引数を削除
//   - TextureSlotRef から直接 external texture を解決
//   - 旧互換ラッパーを削除
//
// ═══════════════════════════════════════════════════════════════════════════

// TextureSlot2D.hlsl は Surface2D.hlsl 経由でインクルード想定

// ---------------------------------------------------------------------------
// Dissolve パラメータ構造体
// ---------------------------------------------------------------------------
struct Dissolve2DParams
{
    float          enabled;
    TextureSlotRef source;
    float          threshold;
    float          edgeWidth;
    half3          edgeColor;
    float          edgeEmission;
};

// ---------------------------------------------------------------------------
// CBUFFER から Dissolve2DParams を生成
// (CBUFFER 変数は Surface2D.hlsl で宣言)
// ---------------------------------------------------------------------------
inline Dissolve2DParams MakeDissolve2DParams(
    float enabled,
    float sourceSlotType,
    float sourceChannel,
    float sourceUVSpace,
    float4 sourceTilingOffset,
    float4 sourceRemap,
    float threshold,
    float edgeWidth,
    float4 edgeColor)
{
    Dissolve2DParams p;
    p.enabled = enabled;
    p.source = MakeTextureSlotRef(
        sourceSlotType,
        sourceChannel,
        sourceUVSpace,
        sourceTilingOffset,
        sourceRemap
    );
    p.threshold = threshold;
    p.edgeWidth = max(edgeWidth, 0.001);  // ゼロ除算防止
    p.edgeColor = edgeColor.rgb;
    p.edgeEmission = edgeColor.a;
    return p;
}

// デフォルト値で初期化（無効状態）
inline Dissolve2DParams MakeDefaultDissolve2DParams()
{
    Dissolve2DParams p;
    p.enabled = 0;
    p.source = MakeDefaultTextureSlotRef();
    p.threshold = 0;
    p.edgeWidth = 0.05;
    p.edgeColor = half3(1, 0.5, 0);
    p.edgeEmission = 2;
    return p;
}

// ---------------------------------------------------------------------------
// Dissolve 適用
// ★v2.0: binding 引数なし
// ★修正: threshold=0 で完全表示、threshold=1 で完全透明に
// 
// 動作仕様:
//   - threshold=0: 全ピクセルが完全に表示される（dissolve=1）
//   - threshold=1: 全ピクセルが完全に透明になる（dissolve=0）
//   - mask値がthresholdより大きい部分が表示される
//   - edgeWidth: 表示/透明の境界のソフトエッジ幅
// ---------------------------------------------------------------------------
inline Surface2D Surface2D_ApplyDissolve(Surface2D s, Dissolve2DParams p)
{
    Surface2D result = s;
    if (p.enabled >= 0.5 && p.source.slotType != TEXTURE_SLOT_NONE)
    {
        half mask = SampleSlotScalar(result, p.source);
        float halfWidth = p.edgeWidth * 0.5;
        float adjustedThreshold = p.threshold * (1.0 + p.edgeWidth) - halfWidth;
        float tMin = adjustedThreshold - halfWidth;
        float tMax = adjustedThreshold + halfWidth;
        half dissolve = smoothstep(tMin, tMax, mask);
        half edgeIntensity = dissolve * (1.0h - dissolve) * 4.0h;

        result.alpha *= dissolve;
        result.alphaFactor *= dissolve;

        if (result.alpha > 0.001)
        {
            result.color += p.edgeColor * edgeIntensity * p.edgeEmission;
        }
    }

    return result;
}

// ---------------------------------------------------------------------------
// Dissolve のみを適用（クリップ版）
// アルファが十分小さい場合は clip() でピクセルを破棄
// ---------------------------------------------------------------------------
inline Surface2D Surface2D_ApplyDissolveWithClip(Surface2D s, Dissolve2DParams p, float clipThreshold)
{
    s = Surface2D_ApplyDissolve(s, p);
    clip(s.alpha - clipThreshold);
    return s;
}

#endif // GAME_DISSOLVE_2D_INCLUDED

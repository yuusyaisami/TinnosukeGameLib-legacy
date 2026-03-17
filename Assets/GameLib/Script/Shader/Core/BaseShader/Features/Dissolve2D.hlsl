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
    Dissolve2DParams p = (Dissolve2DParams)0;
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
    Dissolve2DParams p = (Dissolve2DParams)0;
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
    if (p.enabled < 0.5)
        return s;
    if (p.source.slotType == TEXTURE_SLOT_NONE)
        return s;
    
    // ソースからマスク値を取得
    half mask = SampleSlotScalar(s, p.source);
    
    // ★修正: thresholdを拡張して0で完全表示、1で完全透明を保証
    // threshold=0 のとき、全ての mask 値 (0-1) が表示されるべき
    // threshold=1 のとき、全ての mask 値 (0-1) が透明になるべき
    // 
    // 解決策: threshold を少し拡張して、0/1で確実に全範囲をカバー
    // 内部threshold = threshold * (1 + edgeWidth) - edgeWidth/2
    // これにより threshold=0 → 内部=-edgeWidth/2, threshold=1 → 内部=1+edgeWidth/2
    
    float halfWidth = p.edgeWidth * 0.5;
    
    // ★重要: スムーズステップの範囲を調整
    // threshold=0: tMin=-halfWidth-eps, tMax=halfWidth-eps → mask>=0 で dissolve≒1
    // threshold=1: tMin=1-halfWidth+eps, tMax=1+halfWidth+eps → mask<=1 で dissolve≒0
    float adjustedThreshold = p.threshold * (1.0 + p.edgeWidth) - halfWidth;
    float tMin = adjustedThreshold - halfWidth;
    float tMax = adjustedThreshold + halfWidth;
    
    // dissolve: mask値がthresholdより大きい部分を表示（表示=1, 透明=0）
    half dissolve = smoothstep(tMin, tMax, mask);
    
    // edge: エッジ部分のみを抽出（threshold付近でマスク値が境界にある部分）
    // エッジは dissolve の傾斜部分のみで発光
    half edgeIntensity = dissolve * (1.0h - dissolve) * 4.0h;  // 0-1-0 のベルカーブ
    
    // アルファを削る
    s.alpha *= dissolve;
    s.alphaFactor *= dissolve;
    
    // エッジに発光を乗せる（アルファが残っている部分のみ）
    if (s.alpha > 0.001)
    {
        s.color += p.edgeColor * edgeIntensity * p.edgeEmission;
    }
    
    return s;
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

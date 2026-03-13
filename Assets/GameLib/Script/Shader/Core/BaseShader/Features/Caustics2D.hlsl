#ifndef GAME_CAUSTICS_2D_INCLUDED
#define GAME_CAUSTICS_2D_INCLUDED

// ═══════════════════════════════════════════════════════════════════════════
// Caustics2D.hlsl - コースティクス（集光）パターン合成 (v3.0)
// ═══════════════════════════════════════════════════════════════════════════
//
// 仕様書 BaseShader-CompositeSystem-v3.0 準拠
//
// 機能:
//   - 水中の光の集光パターン（コースティクス）を合成
//   - 2層のノイズを異なる速度・角度で合成し、リアルな光の揺らぎを表現
//   - 深度フェード機能で水深に応じた減衰
//
// ═══════════════════════════════════════════════════════════════════════════

// TextureSlot2D.hlsl は Surface2D.hlsl 経由でインクルード想定

// ---------------------------------------------------------------------------
// Caustics パラメータ構造体
// ★ 修正: BaseShader.shader のプロパティ構造に合わせる
// ---------------------------------------------------------------------------
struct Caustics2DParams
{
    float          enabled;
    
    // レイヤーA (TextureSlotRef)
    TextureSlotRef sourceA;
    
    // レイヤーB (TextureSlotRef)
    TextureSlotRef sourceB;
    
    // 合成
    half3          tintColor;       // 水の色
    half           tintAlpha;       // 不透明度
    half           intensity;       // 全体強度
    half           threshold;       // しきい値
    half           softness;        // ソフトネス
    
    // スクロール
    half2          scrollA;         // レイヤーAスクロール速度
    half2          scrollB;         // レイヤーBスクロール速度
};

// ---------------------------------------------------------------------------
// CBUFFER から Caustics2DParams を生成（旧式：互換性維持）
// ---------------------------------------------------------------------------
inline Caustics2DParams MakeCaustics2DParams(
    float enabled,
    float slot1Type,
    float2 scroll1Speed,
    float scale1,
    float slot2Type,
    float2 scroll2Speed,
    float scale2,
    float4 tintColor,
    float intensity,
    float blendMode,
    float depthFadeEnabled,
    float depthFadeStart,
    float depthFadeEnd)
{
    Caustics2DParams p = (Caustics2DParams)0;
    p.enabled = enabled;
    p.sourceA = MakeTextureSlotRef(slot1Type, CHANNEL_R, NOISE_UV_SPACE_SPRITE_LOCAL, 
                                    float4(scale1, scale1, 0, 0), float4(0.5, 0.5, 1, 0));
    p.sourceB = MakeTextureSlotRef(slot2Type, CHANNEL_R, NOISE_UV_SPACE_SPRITE_LOCAL, 
                                    float4(scale2, scale2, 0, 0), float4(0.5, 0.5, 1, 0));
    p.tintColor = tintColor.rgb;
    p.tintAlpha = tintColor.a;
    p.intensity = intensity;
    p.threshold = 0.5h;
    p.softness = 0.1h;
    p.scrollA = scroll1Speed;
    p.scrollB = scroll2Speed;
    return p;
}

// ---------------------------------------------------------------------------
// ★ Simplified: BaseShader.shader の実際のプロパティに合わせた簡易版
// ---------------------------------------------------------------------------
inline Caustics2DParams MakeCaustics2DParamsSimple(
    float enabled,
    // Source A
    float sourceASlotType,
    float sourceAChannel,
    float sourceAUVSpace,
    float4 sourceATilingOffset,
    float4 sourceARemap,
    // Source B
    float sourceBSlotType,
    float sourceBChannel,
    float sourceBUVSpace,
    float4 sourceBTilingOffset,
    float4 sourceBRemap,
    // Caustics params
    float4 causticsColor,
    float intensity,
    float threshold,
    float softness,
    float2 scrollA,
    float2 scrollB)
{
    Caustics2DParams p = (Caustics2DParams)0;
    p.enabled = enabled;
    p.sourceA = MakeTextureSlotRef(sourceASlotType, sourceAChannel, sourceAUVSpace, 
                                    sourceATilingOffset, sourceARemap);
    p.sourceB = MakeTextureSlotRef(sourceBSlotType, sourceBChannel, sourceBUVSpace, 
                                    sourceBTilingOffset, sourceBRemap);
    p.tintColor = causticsColor.rgb;
    p.tintAlpha = causticsColor.a;
    p.intensity = intensity;
    p.threshold = threshold;
    p.softness = max(softness, 0.001h);  // ゼロ除算防止
    p.scrollA = scrollA;
    p.scrollB = scrollB;
    return p;
}

// デフォルト値で初期化（無効状態）
inline Caustics2DParams MakeDefaultCaustics2DParams()
{
    Caustics2DParams p = (Caustics2DParams)0;
    p.enabled = 0;
    p.sourceA = MakeDefaultTextureSlotRef();
    p.sourceB = MakeDefaultTextureSlotRef();
    p.tintColor = half3(0.7, 0.9, 1.0);
    p.tintAlpha = 0.5;
    p.intensity = 0.6;
    p.threshold = 0.5h;
    p.softness = 0.1h;
    p.scrollA = half2(0.1, 0.08);
    p.scrollB = half2(-0.08, 0.12);
    return p;
}

// ---------------------------------------------------------------------------
// コースティクスパターン計算
// ★ 修正: TextureSlotRef を使用
// ---------------------------------------------------------------------------
inline half ComputeCaustics(Surface2D s, float time, Caustics2DParams p)
{
    // レイヤーA: スクロール + サンプル
    Surface2D sA = s;
    sA.uvLocal = s.uvLocal + p.scrollA * time;
    half nA = SampleSlotScalar(sA, p.sourceA);
    
    // レイヤーB: スクロール + サンプル
    Surface2D sB = s;
    sB.uvLocal = s.uvLocal + p.scrollB * time;
    half nB = SampleSlotScalar(sB, p.sourceB);
    
    // 乗算で交差パターンを生成、しきい値適用
    half raw = nA * nB;
    half caustic = smoothstep(p.threshold - p.softness, p.threshold + p.softness, raw);
    
    return caustic;
}

// ---------------------------------------------------------------------------
// Caustics 適用
// ★ 修正: 新しい構造に対応
// ---------------------------------------------------------------------------
inline Surface2D Surface2D_ApplyCaustics(Surface2D s, Caustics2DParams p, float time)
{
    if (p.enabled < 0.5h)
        return s;
    
    half caustic = ComputeCaustics(s, time, p);
    
    // Add ブレンド（コースティクスの基本ブレンドモード）
    half3 causticsColor = p.tintColor * caustic * p.intensity;
    s.color = s.color + causticsColor * p.tintAlpha;
    
    return s;
}

#endif // GAME_CAUSTICS_2D_INCLUDED

#ifndef GAME_REFRACTION_2D_INCLUDED
#define GAME_REFRACTION_2D_INCLUDED

// ═══════════════════════════════════════════════════════════════════════════
// Refraction2D.hlsl - 屈折/歪みエフェクト (v3.0)
// ═══════════════════════════════════════════════════════════════════════════
//
// 仕様書 BaseShader-CompositeSystem-v3.0 準拠
//
// 機能:
//   - ノイズの勾配チャンネルを使って MainTex の UV を歪ませる
//   - 水面、熱気揺らぎ、ガラス越しの歪みなどを実現
//   - クロマティックアベレーション（色収差）をサポート
//   - 時間変調で動的な歪みが可能
//
// FlowWarp との違い:
//   - FlowWarp: ノイズ値自体で歪み方向を決定
//   - Refraction: ノイズの勾配（微分）で物理的に正しい屈折方向を計算
//
// ═══════════════════════════════════════════════════════════════════════════

// TextureSlot2D.hlsl は Surface2D.hlsl 経由でインクルード想定

// ---------------------------------------------------------------------------
// Refraction パラメータ構造体
// ---------------------------------------------------------------------------
struct Refraction2DParams
{
    float          enabled;
    TextureSlotRef source;           // 勾配ソース (OUTPUT_MODE_GRADIENT 推奨)
    half           strength;         // 全体歪み強度
    half2          strengthXY;       // X/Y 個別強度（異方性歪み用）
    int            chromaticEnabled; // クロマティックアベレーション有効
    half           chromaticOffset;  // RGB チャンネル間のずれ量
    half           timeSpeed;        // 歪みの時間変化速度
    half           timeAmplitude;    // 歪みの時間変化振幅
};

// ---------------------------------------------------------------------------
// CBUFFER から Refraction2DParams を生成
// ---------------------------------------------------------------------------
inline Refraction2DParams MakeRefraction2DParams(
    float enabled,
    float sourceSlotType,
    float sourceChannel,
    float sourceUVSpace,
    float4 sourceTilingOffset,
    float4 sourceRemap,
    float strength,
    float2 strengthXY,
    float chromaticEnabled,
    float chromaticOffset,
    float timeSpeed,
    float timeAmplitude)
{
    Refraction2DParams p;
    p.enabled = enabled;
    p.source = MakeTextureSlotRef(
        sourceSlotType,
        sourceChannel,
        sourceUVSpace,
        sourceTilingOffset,
        sourceRemap
    );
    p.strength = strength;
    p.strengthXY = strengthXY;
    p.chromaticEnabled = (int)round(chromaticEnabled);
    p.chromaticOffset = chromaticOffset;
    p.timeSpeed = timeSpeed;
    p.timeAmplitude = timeAmplitude;
    return p;
}

// ---------------------------------------------------------------------------
// ★ Simplified: BaseShader.shader の実際のプロパティに合わせた簡易版
// ---------------------------------------------------------------------------
inline Refraction2DParams MakeRefraction2DParamsSimple(
    float enabled,
    float sourceSlotType,
    float sourceChannel,
    float sourceUVSpace,
    float4 sourceTilingOffset,
    float4 sourceRemap,
    float2 strengthXY,           // _RefractionStrength.xy
    float chromaticAberration)   // _RefractionChromaticAberration
{
    Refraction2DParams p;
    p.enabled = enabled;
    p.source = MakeTextureSlotRef(
        sourceSlotType,
        sourceChannel,
        sourceUVSpace,
        sourceTilingOffset,
        sourceRemap
    );
    p.strength = max(strengthXY.x, strengthXY.y);  // 全体強度
    p.strengthXY = strengthXY;
    p.chromaticEnabled = (chromaticAberration > 0.001h) ? 1 : 0;
    p.chromaticOffset = chromaticAberration;
    p.timeSpeed = 0;
    p.timeAmplitude = 0;
    return p;
}

// デフォルト値で初期化（無効状態）
inline Refraction2DParams MakeDefaultRefraction2DParams()
{
    Refraction2DParams p;
    p.enabled = 0;
    p.source = MakeDefaultTextureSlotRef();
    p.strength = 0.02;
    p.strengthXY = half2(1, 1);
    p.chromaticEnabled = 0;
    p.chromaticOffset = 0.002;
    p.timeSpeed = 0;
    p.timeAmplitude = 0;
    return p;
}

// ---------------------------------------------------------------------------
// 屈折オフセット計算
// ---------------------------------------------------------------------------
inline float2 ComputeRefractionOffset(half2 gradient, Refraction2DParams p, float time)
{
    // gradient は [0,1] から [-1,1] に変換済みと仮定 (SampleSlotVector2 で変換済み)
    
    // 時間変調
    half timeMod = 1.0h;
    if (p.timeAmplitude > 0.001h)
    {
        timeMod = 1.0h + sin(time * p.timeSpeed) * p.timeAmplitude;
    }
    
    return gradient * p.strength * p.strengthXY * timeMod;
}

// ---------------------------------------------------------------------------
// Refraction UV 歪み適用
// MainTex サンプリング前に UV を歪ませる
// ---------------------------------------------------------------------------
inline float2 Refraction2D_WarpUV(float2 uvMain, Surface2D s, Refraction2DParams p, float time)
{
    if (p.enabled < 0.5h)
        return uvMain;
    if (p.source.slotType == TEXTURE_SLOT_NONE)
        return uvMain;
    
    // 勾配サンプル（GB チャンネルに格納されている想定）
    // source.channelMask を CHANNEL_GB に設定することを推奨
    half2 gradient = SampleSlotVector2(s, p.source);
    
    float2 offset = ComputeRefractionOffset(gradient, p, time);
    
    return uvMain + offset;
}

// ---------------------------------------------------------------------------
// Refraction with Chromatic Aberration
// MainTex をクロマティックアベレーション付きでサンプリング
// ---------------------------------------------------------------------------
inline half4 Refraction2D_SampleWithChromatic(
    TEXTURE2D_PARAM(mainTex, samp),
    float2 uvMain,
    Surface2D s,
    Refraction2DParams p,
    float time)
{
    if (p.enabled < 0.5h)
        return SAMPLE_TEXTURE2D(mainTex, samp, uvMain);
    if (p.source.slotType == TEXTURE_SLOT_NONE)
        return SAMPLE_TEXTURE2D(mainTex, samp, uvMain);
    
    // 勾配サンプル
    half2 gradient = SampleSlotVector2(s, p.source);
    float2 offset = ComputeRefractionOffset(gradient, p, time);
    
    if (p.chromaticEnabled > 0)
    {
        // クロマティックアベレーション
        float2 offsetR = offset * (1.0 - p.chromaticOffset);
        float2 offsetG = offset;
        float2 offsetB = offset * (1.0 + p.chromaticOffset);
        
        half r = SAMPLE_TEXTURE2D(mainTex, samp, uvMain + offsetR).r;
        half g = SAMPLE_TEXTURE2D(mainTex, samp, uvMain + offsetG).g;
        half b = SAMPLE_TEXTURE2D(mainTex, samp, uvMain + offsetB).b;
        half a = SAMPLE_TEXTURE2D(mainTex, samp, uvMain + offsetG).a;
        
        return half4(r, g, b, a);
    }
    else
    {
        return SAMPLE_TEXTURE2D(mainTex, samp, uvMain + offset);
    }
}

// ---------------------------------------------------------------------------
// Surface2D 後処理版 (MainTex サンプリング後に色を置き換え)
// 注意: この関数は MainTex の再サンプリングが必要なため、
//       Surface2D_BeforeSample_Apply で UV を歪ませる方が効率的
// ---------------------------------------------------------------------------
inline Surface2D Surface2D_ApplyRefraction(
    Surface2D s,
    TEXTURE2D_PARAM(mainTex, samp),
    Refraction2DParams p,
    float time)
{
    if (p.enabled < 0.5h)
        return s;
    if (p.source.slotType == TEXTURE_SLOT_NONE)
        return s;
    
    half4 refracted = Refraction2D_SampleWithChromatic(
        TEXTURE2D_ARGS(mainTex, samp),
        s.uvMain,
        s,
        p,
        time);
    
    s.color = refracted.rgb;
    // アルファは維持（歪みはアルファに影響しない）
    
    return s;
}

#endif // GAME_REFRACTION_2D_INCLUDED

// ============================================================================
// Transition2D.hlsl - BaseShader Transition System v1.0
// ============================================================================
// 画像間トランジション（クロスフェード/ディゾルブ/ワイプ）を実現する機能。
// AnimationSpriteChannelPlayer からのみ制御される。
// ============================================================================

#ifndef GAME_TRANSITION2D_INCLUDED
#define GAME_TRANSITION2D_INCLUDED

// ============================================================================
// External Texture for Transition Source (from sprite)
// NOTE: _ExtTexA and sampler_ExtTexA are declared in TextureSlot2D.hlsl
//       to avoid redefinition errors. This file assumes they are available.
// ============================================================================

// ============================================================================
// Constants
// ============================================================================

#define TRANSITION_MODE_CROSSFADE       0
#define TRANSITION_MODE_DISSOLVE        1
#define TRANSITION_MODE_WIPE_HORIZONTAL 2
#define TRANSITION_MODE_WIPE_VERTICAL   3

// ============================================================================
// Parameter Struct
// ============================================================================

struct Transition2DParams
{
    float enabled;       // 有効フラグ (0 or 1)
    float blendMode;     // TransitionBlendMode enum
    float progress;      // 進行度 0.0 - 1.0
    float edgeWidth;     // ディゾルブ等のエッジ幅
    float softness;      // エッジのソフトネス
    float direction;     // ワイプ方向 (-1 or 1)
    float4 fromUVRect;   // from側スプライトのUV矩形 (minU, minV, maxU, maxV)
};

// ============================================================================
// Parameter Factory
// ============================================================================

/// <summary>
/// CBUFFER から Transition2DParams を生成する。
/// </summary>
/// <param name="enabled">_TransitionEnabled</param>
/// <param name="blendMode">_TransitionBlendMode</param>
/// <param name="progress">_TransitionProgress</param>
/// <param name="params">_TransitionParams (x=edgeWidth, y=softness, z=direction, w=reserved)</param>
inline Transition2DParams MakeTransition2DParams(
    float enabled, 
    float blendMode, 
    float progress, 
    float4 params,
    float4 fromUVRect)
{
    Transition2DParams p;
    p.enabled = enabled;
    p.blendMode = blendMode;
    p.progress = progress;
    p.edgeWidth = params.x;
    p.softness = max(params.y, 0.001); // ゼロ除算防止
    p.direction = params.z;
    p.fromUVRect = fromUVRect;
    return p;
}

/// <summary>
/// 簡易ファクトリ（デフォルトパラメータ）
/// </summary>
inline Transition2DParams MakeTransition2DParamsSimple(
    float enabled,
    float blendMode,
    float progress)
{
    return MakeTransition2DParams(enabled, blendMode, progress, float4(0.1, 0.05, 1.0, 0.0), float4(0, 0, 1, 1));
}

// ============================================================================
// Internal Blend Functions
// ============================================================================

/// <summary>
/// シンプルなクロスフェード
/// </summary>
inline float4 Transition_CrossFade(float4 from, float4 to, float t)
{
    return lerp(from, to, t);
}

/// <summary>
/// ノイズベースディゾルブ
/// </summary>
inline float4 Transition_Dissolve(float4 from, float4 to, float2 uvLocal, float t, float edgeWidth, float softness)
{
    // 簡易ノイズ（将来的にはテクスチャスロット対応可能）
    float noise = frac(sin(dot(uvLocal * 100.0, float2(12.9898, 78.233))) * 43758.5453);
    
    // エッジのスムーズステップ
    float edge = smoothstep(t - edgeWidth * 0.5, t + softness, noise);
    
    return lerp(from, to, edge);
}

/// <summary>
/// 横方向ワイプ
/// </summary>
inline float4 Transition_WipeHorizontal(float4 from, float4 to, float2 uvLocal, float t, float softness, float direction)
{
    float wipePos = (direction > 0.0) ? uvLocal.x : (1.0 - uvLocal.x);
    float edge = smoothstep(t - softness, t, wipePos);
    return lerp(from, to, edge);
}

/// <summary>
/// 縦方向ワイプ
/// </summary>
inline float4 Transition_WipeVertical(float4 from, float4 to, float2 uvLocal, float t, float softness, float direction)
{
    float wipePos = (direction > 0.0) ? uvLocal.y : (1.0 - uvLocal.y);
    float edge = smoothstep(t - softness, t, wipePos);
    return lerp(from, to, edge);
}

// ============================================================================
// Main Apply Function
// ============================================================================

/// <summary>
/// Surface2D にトランジション効果を適用する。
/// </summary>
/// <param name="s">現在の Surface2D（トランジション先 = 新しいスプライト）</param>
/// <param name="fromSample">トランジション元からサンプルした色（_ExtTexA 等）</param>
/// <param name="p">トランジションパラメータ</param>
/// <returns>トランジション適用後の Surface2D</returns>
/// <remarks>
/// Progress の意味:
/// - 0.0 = トランジション開始（fromSample が 100%）
/// - 1.0 = トランジション完了（Surface2D が 100%）
/// </remarks>
inline Surface2D Surface2D_ApplyTransition(
    Surface2D s,
    float4 fromSample,
    Transition2DParams p)
{
    Surface2D output = s;
    if (p.enabled > 0.5 && p.progress < 1.0)
    {
        if (p.progress <= 0.0)
        {
            output.color = fromSample.rgb;
            output.alpha = fromSample.a;
        }
        else
        {
            float t = saturate(p.progress);
            float4 current = float4(output.color, output.alpha);
            float4 from = fromSample;
            int mode = (int)p.blendMode;
            float4 transitionResult = current;

            if (mode == TRANSITION_MODE_CROSSFADE)
            {
                transitionResult = Transition_CrossFade(from, current, t);
            }
            else if (mode == TRANSITION_MODE_DISSOLVE)
            {
                transitionResult = Transition_Dissolve(from, current, output.uvLocal, t, p.edgeWidth, p.softness);
            }
            else if (mode == TRANSITION_MODE_WIPE_HORIZONTAL)
            {
                transitionResult = Transition_WipeHorizontal(from, current, output.uvLocal, t, p.softness, p.direction);
            }
            else if (mode == TRANSITION_MODE_WIPE_VERTICAL)
            {
                transitionResult = Transition_WipeVertical(from, current, output.uvLocal, t, p.softness, p.direction);
            }
            else
            {
                transitionResult = Transition_CrossFade(from, current, t);
            }

            output.color = transitionResult.rgb;
            output.alpha = transitionResult.a;
        }
    }

    return output;
}

// ============================================================================
// UV Mapping for From Sprite (Atlas)
// ============================================================================

/// <summary>
/// uvLocal(0..1) を from 側のアトラスUVへ変換する。
/// _TransitionFromSpriteUVRect = (minU, minV, maxU, maxV)
/// </summary>
inline float2 Transition_MapLocalToFromUV(float2 uvLocal, float4 fromUVRect)
{
    return fromUVRect.xy + uvLocal * (fromUVRect.zw - fromUVRect.xy);
}

/// <summary>
/// from スプライトを _ExtTexA からサンプルする。
/// </summary>
inline float4 Transition_SampleFrom(float2 uvLocal, float4 fromUVRect)
{
    float2 fromUV = Transition_MapLocalToFromUV(uvLocal, fromUVRect);
    return SAMPLE_TEXTURE2D(_ExtTexA, sampler_ExtTexA, fromUV);
}

// ============================================================================
// 2-Argument Overload (for Surface2D.hlsl pipeline call)
// ============================================================================

/// <summary>
/// Surface2D にトランジション効果を適用する（2引数版）。
/// Surface2D.hlsl の SURFACE2D_PIPELINE マクロから呼び出される。
/// 内部で _ExtTexA をサンプルして3引数版に委譲する。
/// </summary>
/// <param name="s">現在の Surface2D（トランジション先 = 新しいスプライト）</param>
/// <param name="p">トランジションパラメータ（_TransitionFromSpriteUVRect を含む）</param>
inline Surface2D Surface2D_ApplyTransition(Surface2D s, Transition2DParams p)
{
    Surface2D output = s;
    if (p.enabled > 0.5)
    {
        float4 fromSample = Transition_SampleFrom(output.uvLocal, p.fromUVRect);
        output = Surface2D_ApplyTransition(output, fromSample, p);
    }

    return output;
}

#endif // GAME_TRANSITION2D_INCLUDED

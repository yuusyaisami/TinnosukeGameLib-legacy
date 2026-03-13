#ifndef GAME_PIXELATION2D_INCLUDED
#define GAME_PIXELATION2D_INCLUDED

// ---- stage detection -------------------------------------------------------
#if defined(SHADER_STAGE_FRAGMENT)
    #define GAME_IS_FRAGMENT 1
#else
    #define GAME_IS_FRAGMENT 0
#endif

struct Pixelation2DParams
{
    float enabled;
    int   mode; // 0:Off, 1:Screen, 2:Texel, 3:ColorOnly
    float colorSteps;
    float alphaSteps;
    float2 blockScreenSize;
};

inline Pixelation2DParams MakePixelation2DParams()
{
    Pixelation2DParams p = (Pixelation2DParams)0;
    p.enabled = _PixelationEnabled;
    p.mode = (int)round(_PixelateMode);
    p.colorSteps = _PixelColorSteps;
    p.alphaSteps = _PixelAlphaSteps;
    p.blockScreenSize = _PixelBlockScreenSize.xy;
    return p;
}

// UV をスクリーン基準のブロックでスナップ
inline float2 Pixelation2D_ApplyUV_Screen(float2 uv, float2 screenUV, float2 blockPixels)
{
#if GAME_IS_FRAGMENT
    float2 block = max(blockPixels, float2(1.0, 1.0));

    float2 pixel = screenUV * _ScreenParams.xy;
    float2 snapped = (floor(pixel / block) + 0.5) * block;
    float2 dp = snapped - pixel;

    float2 duvdx = ddx(uv);
    float2 duvdy = ddy(uv);

    return uv + duvdx * dp.x + duvdy * dp.y;
#else
    return uv;
#endif
}

// UV をテクセル単位でスナップ (blockTexels = number of texels per block)
inline float2 Pixelation2D_ApplyUV_Texel(float2 uv, float2 blockTexels)
{
#if GAME_IS_FRAGMENT
    float2 texelSizeUV = float2(_MainTex_TexelSize.x, _MainTex_TexelSize.y);
    float2 block = max(blockTexels, float2(1.0f, 1.0f));
    float2 snapSize = texelSizeUV * block;
    snapSize = max(snapSize, float2(1e-4f, 1e-4f));
    return floor(uv / snapSize) * snapSize + snapSize * 0.5f;
#else
    return uv;
#endif
}

// Sprite Atlas 対応版（スプライト局所0..1でスナップ後に戻す）
inline float2 Pixelation2D_ApplyUV_Texel_SpriteLocal(float2 uv, float2 blockTexels)
{
#if GAME_IS_FRAGMENT
    float2 uvLocal = AtlasUVToSpriteLocalUV(uv); // 0..1 (SpriteMode Multiple / Atlas 対応)

    // ローカル(0..1)空間でのテクセルサイズは「スプライトのピクセルサイズ」に依存する。
    // _SpriteUVRect が有効ならそれを使用し、無い場合は _MainTex_ST をフレーム矩形とみなして推定する。
    float2 spritePxSize;
    {
        float4 rect = _SpriteUVRect;
        float2 rectMin = rect.xy;
        float2 rectMax = rect.zw;
        float2 rectSize = rectMax - rectMin;
        bool hasSpriteRect =
            (abs(rectMin.x) > 1e-5) || (abs(rectMin.y) > 1e-5) ||
            (abs(rectMax.x - 1.0) > 1e-5) || (abs(rectMax.y - 1.0) > 1e-5);

        if (hasSpriteRect)
        {
            spritePxSize = max(rectSize * _MainTex_TexelSize.zw, float2(1.0, 1.0));
        }
        else
        {
            float2 stScale  = _MainTex_ST.xy;
            float2 stOffset = _MainTex_ST.zw;
            bool hasST =
                (abs(stScale.x - 1.0) > 1e-5) || (abs(stScale.y - 1.0) > 1e-5) ||
                (abs(stOffset.x) > 1e-5) || (abs(stOffset.y) > 1e-5);

            spritePxSize = hasST
                ? max(stScale * _MainTex_TexelSize.zw, float2(1.0, 1.0))
                : max(_MainTex_TexelSize.zw, float2(1.0, 1.0));
        }
    }

    float2 texelSizeLocal01 = 1.0 / spritePxSize; // 0..1 空間での 1 texel
    float2 snapSizeLocal = texelSizeLocal01 * max(blockTexels, float2(1.0, 1.0));
    uvLocal = floor(uvLocal / snapSizeLocal) * snapSizeLocal + snapSizeLocal * 0.5f;
    return SpriteLocalUVToAtlasUV(uvLocal);
#else
    return uv;
#endif
}

// 色の階調量子化（levels＝段階数として扱う）
inline float3 Pixelation2D_QuantizeColor(float3 color, float pixelateLevels)
{
    if (pixelateLevels <= 1.0f)
        return color;

    float levels = max(pixelateLevels, 2.0f);
    float s = levels - 1.0f;
    return clamp(floor(color * s + 0.5f) / s, 0.0f, 1.0f);
}

// α の階調量子化（levels＝段階数）
inline float Pixelation2D_QuantizeAlpha(float alpha, float pixelateLevels)
{
    if (pixelateLevels <= 1.0f)
        return alpha;

    float levels = max(pixelateLevels, 2.0f);
    float s = levels - 1.0f;
    return saturate(floor(alpha * s + 0.5f) / s);
}

inline Surface2D Surface2D_ApplyPixelation(Surface2D s, Pixelation2DParams p)
{
    if (p.enabled <= 0.5f || p.mode == 0)
        return s;

    if (p.colorSteps > 1.0f)
        s.color = Pixelation2D_QuantizeColor(s.color, p.colorSteps);

    if (p.mode != 3 && p.alphaSteps > 1.0f)
        s.alpha = Pixelation2D_QuantizeAlpha(s.alpha, p.alphaSteps);

    return s;
}

#endif // GAME_PIXELATION2D_INCLUDED

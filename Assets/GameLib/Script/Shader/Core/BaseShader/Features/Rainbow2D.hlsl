#ifndef GAME_RAINBOW_2D_INCLUDED
#define GAME_RAINBOW_2D_INCLUDED

// ============================================================================
// Rainbow2D.hlsl - Rainbow gradient/pixel effect (BaseShader extension)
// ============================================================================

// Modes
#define RAINBOW_MODE_GRADIENT 0
#define RAINBOW_MODE_PIXEL    1

// Patterns
#define RAINBOW_PATTERN_HORIZONTAL 0
#define RAINBOW_PATTERN_VERTICAL   1
#define RAINBOW_PATTERN_CHECKER    2

// Blend modes
#define RAINBOW_BLEND_ADD      0
#define RAINBOW_BLEND_SCREEN   1
#define RAINBOW_BLEND_OVERLAY  2
#define RAINBOW_BLEND_LERP     3

struct Rainbow2DParams
{
    float enabled;
    int   mode;
    int   pattern;
    float2 direction;
    float  scale;
    float  offset;
    float  speed;
    float  pixelSize;
    float  intensity;
    int    blendMode;
};

inline Rainbow2DParams MakeRainbow2DParams(
    float enabled,
    float mode,
    float pattern,
    float2 direction,
    float scale,
    float offset,
    float speed,
    float pixelSize,
    float intensity,
    float blendMode)
{
    Rainbow2DParams p;
    p.enabled = enabled;
    p.mode = (int)round(mode);
    p.pattern = (int)round(pattern);
    p.direction = direction;
    p.scale = max(scale, 0.0001);
    p.offset = offset;
    p.speed = speed;
    p.pixelSize = max(pixelSize, 1.0);
    p.intensity = saturate(intensity);
    p.blendMode = (int)round(blendMode);
    return p;
}

inline float3 HSVToRGB(float3 hsv)
{
    float3 rgb = saturate(abs(frac(hsv.x + float3(0.0, 2.0/3.0, 1.0/3.0)) * 6.0 - 3.0) - 1.0);
    return hsv.z * lerp(float3(1,1,1), rgb, hsv.y);
}

inline float RainbowPatternCoord(float2 uv, int pattern)
{
    if (pattern == RAINBOW_PATTERN_VERTICAL)
        return uv.x;
    if (pattern == RAINBOW_PATTERN_CHECKER)
    {
        float2 c = floor(uv * 8.0);
        return fmod(c.x + c.y, 2.0) * 0.5;
    }
    return uv.y; // horizontal
}

inline float3 RainbowBlend(float3 baseColor, float3 fxColor, float amount, int blendMode)
{
    amount = saturate(amount);
    if (blendMode == RAINBOW_BLEND_ADD)
        return baseColor + fxColor * amount;
    if (blendMode == RAINBOW_BLEND_SCREEN)
    {
        float3 screened = 1.0 - (1.0 - baseColor) * (1.0 - fxColor);
        return lerp(baseColor, screened, amount);
    }
    if (blendMode == RAINBOW_BLEND_OVERLAY)
    {
        float3 overlay;
        overlay.r = baseColor.r < 0.5 ? 2.0 * baseColor.r * fxColor.r : 1.0 - 2.0 * (1.0 - baseColor.r) * (1.0 - fxColor.r);
        overlay.g = baseColor.g < 0.5 ? 2.0 * baseColor.g * fxColor.g : 1.0 - 2.0 * (1.0 - baseColor.g) * (1.0 - fxColor.g);
        overlay.b = baseColor.b < 0.5 ? 2.0 * baseColor.b * fxColor.b : 1.0 - 2.0 * (1.0 - baseColor.b) * (1.0 - fxColor.b);
        return lerp(baseColor, overlay, amount);
    }
    return lerp(baseColor, fxColor, amount);
}

inline Surface2D Surface2D_ApplyRainbow(Surface2D s, Rainbow2DParams p, float time)
{
    Surface2D result = s;
    if (p.enabled >= 0.5)
    {
        float2 uv = result.uvLocal;

        if (p.mode == RAINBOW_MODE_PIXEL)
        {
            float2 texel = max(_MainTex_TexelSize.xy, float2(1.0/256.0, 1.0/256.0));
            float2 step = texel * p.pixelSize;
            uv = floor(uv / step) * step;
        }

        float baseCoord = RainbowPatternCoord(uv, p.pattern);
        float2 dir = p.direction;
        float len = max(length(dir), 1e-4);
        dir /= len;

        float scroll = dot(uv, dir) * p.scale + p.offset + time * p.speed;
        float hue = frac(baseCoord * p.scale + scroll);
        float3 rainbow = HSVToRGB(float3(hue, 1.0, 1.0));

        result.color = RainbowBlend(result.color, rainbow, p.intensity, p.blendMode);
    }

    return result;
}

#endif // GAME_RAINBOW_2D_INCLUDED

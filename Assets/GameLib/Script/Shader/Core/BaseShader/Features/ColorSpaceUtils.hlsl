#ifndef GAME_COLOR_SPACE_UTILS_INCLUDED
#define GAME_COLOR_SPACE_UTILS_INCLUDED

// ═══════════════════════════════════════════════════════════════════════════
// ColorSpaceUtils.hlsl - 色空間変換ユーティリティ
// ═══════════════════════════════════════════════════════════════════════════

inline half3 RGBtoHSV(half3 rgb)
{
    half4 K = half4(0.0h, -1.0h/3.0h, 2.0h/3.0h, -1.0h);
    half4 p = lerp(half4(rgb.bg, K.wz), half4(rgb.gb, K.xy), step(rgb.b, rgb.g));
    half4 q = lerp(half4(p.xyw, rgb.r), half4(rgb.r, p.yzx), step(p.x, rgb.r));
    
    half d = q.x - min(q.w, q.y);
    half e = 1.0e-10h;
    return half3(abs(q.z + (q.w - q.y) / (6.0h * d + e)), d / (q.x + e), q.x);
}

inline half3 HSVtoRGB(half3 hsv)
{
    half4 K = half4(1.0h, 2.0h/3.0h, 1.0h/3.0h, 3.0h);
    half3 p = abs(frac(hsv.xxx + K.xyz) * 6.0h - K.www);
    return hsv.z * lerp(K.xxx, saturate(p - K.xxx), hsv.y);
}

// Set Lum/Sat according to Photoshop/W3C specs for advanced blend modes
inline half GetLuminosity(half3 c)
{
    return dot(c, half3(0.3h, 0.59h, 0.11h));
}

inline half3 SetLuminosity(half3 c, half lum)
{
    half d = lum - GetLuminosity(c);
    c += d;
    
    // Clip color
    half l = GetLuminosity(c);
    half n = min(min(c.r, c.g), c.b);
    half x = max(max(c.r, c.g), c.b);
    
    if (n < 0.0h) c = l + (((c - l) * l) / (l - n + 1e-5h));
    if (x > 1.0h) c = l + (((c - l) * (1.0h - l)) / (x - l + 1e-5h));
    
    return c;
}

inline half GetSaturation(half3 c)
{
    return max(max(c.r, c.g), c.b) - min(min(c.r, c.g), c.b);
}

// This is a simpler version of SetSat for blending purposes
inline half3 SetSaturation(half3 c, half sat)
{
    // Sorting components to apply saturation correctly is complex in HLSL without branches/sort
    // Using a simplified approach common in shaders:
    half3 result;
    half minC = min(min(c.r, c.g), c.b);
    half maxC = max(max(c.r, c.g), c.b);
    half midC;
    
    // Manual mid calculation
    if (c.r != minC && c.r != maxC) midC = c.r;
    else if (c.g != minC && c.g != maxC) midC = c.g;
    else midC = c.b;

    if (maxC > minC)
    {
        midC = ((midC - minC) * sat) / (maxC - minC);
        maxC = sat;
    }
    else
    {
        midC = maxC = 0.0h;
    }
    minC = 0.0h;
    
    // We lost the original Hue order, but for Saturation blend mode we use base Hue.
    // So usually you convert to HSV, swap S, and convert back.
    // Let's use RGB->HSV->RGB for better accuracy in complex blends.
    half3 hsv = RGBtoHSV(c);
    hsv.y = sat;
    return HSVtoRGB(hsv);
}

#endif // GAME_COLOR_SPACE_UTILS_INCLUDED

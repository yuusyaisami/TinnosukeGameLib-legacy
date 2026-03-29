#ifndef GAME_ANIMATED_NOISE_2D_INCLUDED
#define GAME_ANIMATED_NOISE_2D_INCLUDED

#define ANIMATED_NOISE_PATTERN_SMOOTH_VALUE 0
#define ANIMATED_NOISE_PATTERN_PERLIN 1
#define ANIMATED_NOISE_PATTERN_FBM 2
#define ANIMATED_NOISE_PATTERN_RIDGED_FBM 3
#define ANIMATED_NOISE_PATTERN_CELLULAR 4
#define ANIMATED_NOISE_PATTERN_HEX_CELL 5
#define ANIMATED_NOISE_PATTERN_TURTLE_SHELL 6
#define ANIMATED_NOISE_PATTERN_CHECKER 7
#define ANIMATED_NOISE_PATTERN_STRIPES 8
#define ANIMATED_NOISE_PATTERN_DIAMOND 9
#define ANIMATED_NOISE_PATTERN_TRUCHET 10
#define ANIMATED_NOISE_PATTERN_INTERFERENCE 11
#define ANIMATED_NOISE_PATTERN_SWIRL 12

#define ANIMATED_NOISE_TWO_PI 6.28318530718

struct AnimatedNoise2DMotionParams
{
    float enabled;
    int patternType;
    float scale;
    float2 direction;
    float speed;
    float2 offset;
    float rotationSpeed;
    float pulseAmplitude;
    float pulseSpeed;
    int warpPatternType;
    float warpScale;
    float warpStrength;
    float2 warpDirection;
    float warpSpeed;
    float loopSeconds;
    int octaves;
    float lacunarity;
    float gain;
    float cellSharpness;
    float patternContrast;
};

inline AnimatedNoise2DMotionParams MakeAnimatedNoise2DMotionParamsSimple(
    float enabled,
    float scale,
    float2 direction,
    float speed,
    float2 offset,
    float warpScale,
    float warpStrength,
    float loopSeconds)
{
    AnimatedNoise2DMotionParams p = (AnimatedNoise2DMotionParams)0;
    p.enabled = enabled;
    p.patternType = ANIMATED_NOISE_PATTERN_SMOOTH_VALUE;
    p.scale = max(scale, 0.0001);
    p.direction = direction;
    p.speed = speed;
    p.offset = offset;
    p.rotationSpeed = 0.0;
    p.pulseAmplitude = 0.0;
    p.pulseSpeed = 0.0;
    p.warpPatternType = ANIMATED_NOISE_PATTERN_SMOOTH_VALUE;
    p.warpScale = max(warpScale, 0.0001);
    p.warpStrength = max(warpStrength, 0.0);
    p.warpDirection = float2(1.0, 0.0);
    p.warpSpeed = speed;
    p.loopSeconds = max(loopSeconds, 0.0);
    p.octaves = 3;
    p.lacunarity = 2.0;
    p.gain = 0.5;
    p.cellSharpness = 1.0;
    p.patternContrast = 1.0;
    return p;
}

inline AnimatedNoise2DMotionParams MakeAnimatedNoise2DMotionParamsFull(
    float enabled,
    float patternType,
    float scale,
    float2 direction,
    float speed,
    float2 offset,
    float rotationSpeed,
    float pulseAmplitude,
    float pulseSpeed,
    float warpPatternType,
    float warpScale,
    float warpStrength,
    float2 warpDirection,
    float warpSpeed,
    float loopSeconds,
    float octaves,
    float lacunarity,
    float gain,
    float cellSharpness,
    float patternContrast)
{
    AnimatedNoise2DMotionParams p = (AnimatedNoise2DMotionParams)0;
    p.enabled = enabled;
    p.patternType = (int)round(patternType);
    p.scale = max(scale, 0.0001);
    p.direction = direction;
    p.speed = speed;
    p.offset = offset;
    p.rotationSpeed = rotationSpeed;
    p.pulseAmplitude = max(pulseAmplitude, 0.0);
    p.pulseSpeed = pulseSpeed;
    p.warpPatternType = ANIMATED_NOISE_PATTERN_SMOOTH_VALUE;
    p.warpScale = max(warpScale, 0.0001);
    p.warpStrength = max(warpStrength, 0.0);
    p.warpDirection = warpDirection;
    p.warpSpeed = warpSpeed;
    p.loopSeconds = max(loopSeconds, 0.0);
    p.octaves = clamp((int)round(octaves), 1, 4);
    p.lacunarity = max(lacunarity, 1.0);
    p.gain = saturate(gain);
    p.cellSharpness = max(cellSharpness, 0.01);
    p.patternContrast = max(patternContrast, 0.01);
    return p;
}

inline float2 AnimatedNoise2D_NormalizeDirection(float2 dir)
{
    float len = length(dir);
    return len > 0.0001 ? dir / len : float2(1.0, 0.0);
}

inline float2 AnimatedNoise2D_GetSpriteAspectScale()
{
    float2 uvSize = max(_SpriteUVRect.zw - _SpriteUVRect.xy, float2(1e-4, 1e-4));
    float2 texWH = max(_MainTex_TexelSize.zw, float2(1.0, 1.0));
    float2 pxSize = uvSize * texWH;
    float aspect = pxSize.x / max(pxSize.y, 1e-4);
    if (aspect >= 1.0)
        return float2(aspect, 1.0);
    return float2(1.0, 1.0 / max(aspect, 1e-4));
}

inline float2 AnimatedNoise2D_GetAspectCorrectedCenteredUV(float2 uvLocal)
{
    return (uvLocal - 0.5) * AnimatedNoise2D_GetSpriteAspectScale();
}

inline float AnimatedNoise2D_Hash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

inline float AnimatedNoise2D_Hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

inline float2 AnimatedNoise2D_Hash22(float2 p)
{
    float n = AnimatedNoise2D_Hash21(p);
    float m = AnimatedNoise2D_Hash21(p + 17.0);
    return float2(n, m);
}

inline float AnimatedNoise2D_SmoothValue01(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = AnimatedNoise2D_Hash21(i);
    float b = AnimatedNoise2D_Hash21(i + float2(1.0, 0.0));
    float c = AnimatedNoise2D_Hash21(i + float2(0.0, 1.0));
    float d = AnimatedNoise2D_Hash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

inline float AnimatedNoise2D_Perlin01(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float2 g00 = normalize(AnimatedNoise2D_Hash22(i) * 2.0 - 1.0 + 1e-4);
    float2 g10 = normalize(AnimatedNoise2D_Hash22(i + float2(1.0, 0.0)) * 2.0 - 1.0 + 1e-4);
    float2 g01 = normalize(AnimatedNoise2D_Hash22(i + float2(0.0, 1.0)) * 2.0 - 1.0 + 1e-4);
    float2 g11 = normalize(AnimatedNoise2D_Hash22(i + float2(1.0, 1.0)) * 2.0 - 1.0 + 1e-4);
    float n00 = dot(g00, f - float2(0.0, 0.0));
    float n10 = dot(g10, f - float2(1.0, 0.0));
    float n01 = dot(g01, f - float2(0.0, 1.0));
    float n11 = dot(g11, f - float2(1.0, 1.0));
    float n = lerp(lerp(n00, n10, u.x), lerp(n01, n11, u.x), u.y);
    return n * 0.5 + 0.5;
}

inline float AnimatedNoise2D_FBM01(float2 p, float lacunarity, float gain, int octaves)
{
    float2 p2 = p * lacunarity + float2(11.7, 6.3);
    float2 p3 = p2 * lacunarity + float2(17.6, 29.3);
    float2 p4 = p3 * lacunarity + float2(23.9, 47.1);
    float o1 = AnimatedNoise2D_Perlin01(p);
    float o2 = AnimatedNoise2D_Perlin01(p2);
    float o3 = AnimatedNoise2D_Perlin01(p3);
    float o4 = AnimatedNoise2D_Perlin01(p4);

    float w1 = 1.0;
    float w2 = gain;
    float w3 = gain * gain;
    float w4 = gain * gain * gain;

    if (octaves <= 1) return o1;
    if (octaves == 2) return (o1 * w1 + o2 * w2) / max(w1 + w2, 1e-4);
    if (octaves == 3) return (o1 * w1 + o2 * w2 + o3 * w3) / max(w1 + w2 + w3, 1e-4);
    return (o1 * w1 + o2 * w2 + o3 * w3 + o4 * w4) / max(w1 + w2 + w3 + w4, 1e-4);
}

inline float AnimatedNoise2D_RidgedFBM01(float2 p, float lacunarity, float gain, int octaves)
{
    float2 p2 = p * lacunarity + float2(11.7, 6.3);
    float2 p3 = p2 * lacunarity + float2(17.6, 29.3);
    float2 p4 = p3 * lacunarity + float2(23.9, 47.1);
    float o1 = 1.0 - abs(AnimatedNoise2D_Perlin01(p) * 2.0 - 1.0);
    float o2 = 1.0 - abs(AnimatedNoise2D_Perlin01(p2) * 2.0 - 1.0);
    float o3 = 1.0 - abs(AnimatedNoise2D_Perlin01(p3) * 2.0 - 1.0);
    float o4 = 1.0 - abs(AnimatedNoise2D_Perlin01(p4) * 2.0 - 1.0);
    float w1 = 1.0;
    float w2 = gain;
    float w3 = gain * gain;
    float w4 = gain * gain * gain;
    if (octaves <= 1) return o1;
    if (octaves == 2) return (o1 * w1 + o2 * w2) / max(w1 + w2, 1e-4);
    if (octaves == 3) return (o1 * w1 + o2 * w2 + o3 * w3) / max(w1 + w2 + w3, 1e-4);
    return (o1 * w1 + o2 * w2 + o3 * w3 + o4 * w4) / max(w1 + w2 + w3 + w4, 1e-4);
}

inline float AnimatedNoise2D_Cellular01(float2 p, float sharpness)
{
    float2 cell = floor(p);
    float2 fracP = frac(p);
    float minDist = 8.0;
    float secondDist = 8.0;

    float2 feature;
    float2 diff;
    float dist;

#define ANIMATED_NOISE2D_ACCUMULATE_CELL(_offset) \
    feature = AnimatedNoise2D_Hash22(cell + (_offset)); \
    diff = (_offset) + feature - fracP; \
    dist = dot(diff, diff); \
    if (dist < minDist) { secondDist = minDist; minDist = dist; } \
    else if (dist < secondDist) { secondDist = dist; }

    ANIMATED_NOISE2D_ACCUMULATE_CELL(float2(-1.0, -1.0))
    ANIMATED_NOISE2D_ACCUMULATE_CELL(float2(0.0, -1.0))
    ANIMATED_NOISE2D_ACCUMULATE_CELL(float2(1.0, -1.0))
    ANIMATED_NOISE2D_ACCUMULATE_CELL(float2(-1.0, 0.0))
    ANIMATED_NOISE2D_ACCUMULATE_CELL(float2(0.0, 0.0))
    ANIMATED_NOISE2D_ACCUMULATE_CELL(float2(1.0, 0.0))
    ANIMATED_NOISE2D_ACCUMULATE_CELL(float2(-1.0, 1.0))
    ANIMATED_NOISE2D_ACCUMULATE_CELL(float2(0.0, 1.0))
    ANIMATED_NOISE2D_ACCUMULATE_CELL(float2(1.0, 1.0))

#undef ANIMATED_NOISE2D_ACCUMULATE_CELL

    float f1 = sqrt(minDist);
    float f2 = sqrt(secondDist);
    float cells = saturate(1.0 - f1);
    float edges = saturate(1.0 - (f2 - f1) * 2.5);
    return pow(saturate(lerp(cells, edges, 0.55)), max(sharpness, 0.01));
}

inline float AnimatedNoise2D_HexCell01(float2 p, float sharpness)
{
    float2 q = float2(p.x * 1.1547005, p.y + p.x * 0.5);
    return AnimatedNoise2D_Cellular01(q, sharpness);
}

inline float AnimatedNoise2D_TurtleShell01(float2 p, float sharpness)
{
    float a = AnimatedNoise2D_HexCell01(p, sharpness);
    float b = AnimatedNoise2D_HexCell01(p * 0.5 + 7.13, sharpness);
    float ring = abs(a - b);
    return saturate(a * 0.55 + b * 0.25 + ring * 0.8);
}

inline float AnimatedNoise2D_Checker01(float2 p)
{
    float2 c = floor(p);
    return fmod(c.x + c.y, 2.0) == 0.0 ? 0.0 : 1.0;
}

inline float AnimatedNoise2D_Stripes01(float2 p)
{
    return sin(p.x * 3.14159265) * 0.5 + 0.5;
}

inline float AnimatedNoise2D_Diamond01(float2 p)
{
    float2 f = abs(frac(p) - 0.5);
    return saturate(1.0 - (f.x + f.y) * 2.0);
}

inline float AnimatedNoise2D_Truchet01(float2 p)
{
    float2 cell = floor(p);
    float2 f = frac(p);
    float flip = AnimatedNoise2D_Hash21(cell) > 0.5 ? 1.0 : 0.0;
    float d0 = length(f - float2(0.0, 0.0));
    float d1 = length(f - float2(1.0, 1.0));
    float d2 = length(f - float2(1.0, 0.0));
    float d3 = length(f - float2(0.0, 1.0));
    float arcA = smoothstep(0.8, 0.0, min(d0, d1));
    float arcB = smoothstep(0.8, 0.0, min(d2, d3));
    return lerp(arcA, arcB, flip);
}

inline float AnimatedNoise2D_Interference01(float2 p)
{
    float v = sin(p.x * 4.0 + sin(p.y * 1.7)) + cos(p.y * 4.3 + sin(p.x * 1.3));
    return saturate(v * 0.25 + 0.5);
}

inline float AnimatedNoise2D_Swirl01(float2 p)
{
    float2 c = p;
    float r = length(c);
    float a = atan2(c.y, c.x);
    float spiral = sin(a * 4.0 + r * 12.0);
    float ring = cos(r * 18.0 - a * 2.0);
    return saturate((spiral * 0.65 + ring * 0.35) * 0.5 + 0.5);
}

inline float AnimatedNoise2D_ApplyContrast(float v, float contrast)
{
    return saturate((v - 0.5) * contrast + 0.5);
}

inline float AnimatedNoise2D_EvaluatePatternRaw01(float2 uv, AnimatedNoise2DMotionParams p, int patternType)
{
    if (patternType == ANIMATED_NOISE_PATTERN_SMOOTH_VALUE)
        return AnimatedNoise2D_SmoothValue01(uv);
    if (patternType == ANIMATED_NOISE_PATTERN_PERLIN)
        return AnimatedNoise2D_Perlin01(uv);
    if (patternType == ANIMATED_NOISE_PATTERN_FBM)
        return AnimatedNoise2D_FBM01(uv, p.lacunarity, p.gain, p.octaves);
    if (patternType == ANIMATED_NOISE_PATTERN_RIDGED_FBM)
        return AnimatedNoise2D_RidgedFBM01(uv, p.lacunarity, p.gain, p.octaves);
    if (patternType == ANIMATED_NOISE_PATTERN_CELLULAR)
        return AnimatedNoise2D_Cellular01(uv, p.cellSharpness);
    if (patternType == ANIMATED_NOISE_PATTERN_HEX_CELL)
        return AnimatedNoise2D_HexCell01(uv, p.cellSharpness);
    if (patternType == ANIMATED_NOISE_PATTERN_TURTLE_SHELL)
        return AnimatedNoise2D_TurtleShell01(uv, p.cellSharpness);
    if (patternType == ANIMATED_NOISE_PATTERN_CHECKER)
        return AnimatedNoise2D_Checker01(uv);
    if (patternType == ANIMATED_NOISE_PATTERN_STRIPES)
        return AnimatedNoise2D_Stripes01(uv);
    if (patternType == ANIMATED_NOISE_PATTERN_DIAMOND)
        return AnimatedNoise2D_Diamond01(uv);
    if (patternType == ANIMATED_NOISE_PATTERN_TRUCHET)
        return AnimatedNoise2D_Truchet01(uv);
    if (patternType == ANIMATED_NOISE_PATTERN_INTERFERENCE)
        return AnimatedNoise2D_Interference01(uv);
    if (patternType == ANIMATED_NOISE_PATTERN_SWIRL)
        return AnimatedNoise2D_Swirl01(uv);
    return AnimatedNoise2D_SmoothValue01(uv);
}

inline float AnimatedNoise2D_GetMotionTime(AnimatedNoise2DMotionParams p, float time)
{
    if (p.loopSeconds > 0.0001)
        return frac(time / p.loopSeconds) * p.loopSeconds;
    return time;
}

inline float2 AnimatedNoise2D_Rotate(float2 v, float radians)
{
    float s = sin(radians);
    float c = cos(radians);
    return float2(v.x * c - v.y * s, v.x * s + v.y * c);
}

inline float2 AnimatedNoise2D_ApplyWarp(float2 uv, AnimatedNoise2DMotionParams p, float time)
{
    if (p.warpStrength <= 0.0001)
        return uv;

    float2 warpDir = AnimatedNoise2D_NormalizeDirection(p.warpDirection);
    float warpTime = time * p.warpSpeed;
    float2 baseUV = uv * p.warpScale + p.offset + warpDir * warpTime;
    float warpNoise = AnimatedNoise2D_SmoothValue01(baseUV + float2(19.1, 7.3));
    float warpPhase = warpNoise * ANIMATED_NOISE_TWO_PI + dot(baseUV, float2(0.73, -1.13));
    float2 warp = float2(cos(warpPhase), sin(warpPhase)) * p.warpStrength;
    return uv + warp;
}

inline float2 AnimatedNoise2D_BuildAnimatedUV(float2 uvLocal, AnimatedNoise2DMotionParams p, float2 extraOffset, float time)
{
    float t = AnimatedNoise2D_GetMotionTime(p, time);
    float2 dir = AnimatedNoise2D_NormalizeDirection(p.direction);
    float pulsePhase = t * p.pulseSpeed;
    float pulseMain = sin(pulsePhase);
    float pulseCross = cos(pulsePhase * 0.73 + 1.17);
    float pulse = 1.0 + pulseMain * p.pulseAmplitude;
    float2 pulseScale = float2(
        pulse + pulseCross * p.pulseAmplitude * 0.35,
        pulse - pulseCross * p.pulseAmplitude * 0.35);
    float2 orbit = float2(
        cos(pulsePhase * 0.91 + 0.43),
        sin(pulsePhase * 1.13 + 1.07)) * (p.pulseAmplitude * 0.25);
    float2 uv = AnimatedNoise2D_GetAspectCorrectedCenteredUV(uvLocal);
    uv += orbit;
    uv *= p.scale;
    uv *= pulseScale;
    uv = AnimatedNoise2D_Rotate(uv, t * p.rotationSpeed);
    uv += p.offset + extraOffset + dir * (t * p.speed);
    uv = AnimatedNoise2D_ApplyWarp(uv, p, t);
    return uv;
}

inline float AnimatedNoise2D_Sample01(float2 uvLocal, AnimatedNoise2DMotionParams p, float2 extraOffset, float time)
{
    if (p.enabled <= 0.5)
        return 0.5;

    float2 uv = AnimatedNoise2D_BuildAnimatedUV(uvLocal, p, extraOffset, time);
    float v = AnimatedNoise2D_EvaluatePatternRaw01(uv, p, p.patternType);
    return AnimatedNoise2D_ApplyContrast(v, p.patternContrast);
}

inline float AnimatedNoise2D_SampleSigned(float2 uvLocal, AnimatedNoise2DMotionParams p, float2 extraOffset, float time)
{
    return AnimatedNoise2D_Sample01(uvLocal, p, extraOffset, time) * 2.0 - 1.0;
}

inline void AnimatedNoise2D_SampleSignedTriplet(
    float2 uvLocal,
    AnimatedNoise2DMotionParams p,
    float time,
    out float primary,
    out float secondary,
    out float tertiary)
{
    primary = AnimatedNoise2D_SampleSigned(uvLocal, p, float2(0.0, 0.0), time);

    float2 centered = AnimatedNoise2D_GetAspectCorrectedCenteredUV(uvLocal);
    float phase = primary * 2.713
        + dot(centered, float2(1.618, -2.236))
        + time * (p.speed * 0.73 + p.rotationSpeed * 0.11 + p.pulseSpeed * 0.07);

    secondary = sin(phase);
    tertiary = cos(phase * 1.173 + primary * 1.917);
}

#endif

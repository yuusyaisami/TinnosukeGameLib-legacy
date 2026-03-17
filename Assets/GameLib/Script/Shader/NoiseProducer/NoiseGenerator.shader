Shader "Hidden/GameLib/NoiseGenerator"
{
    Properties
    {
        _NoiseInputTex ("Input", 2D) = "black" {}
        _NoiseSecondaryTex ("Secondary", 2D) = "black" {}
        _NoiseTime ("Time", Float) = 0
        _NoiseSeed ("Seed", Float) = 0
        _NoiseScale ("Scale", Vector) = (1, 1, 0, 0)
        _NoiseOffset ("Offset", Vector) = (0, 0, 0, 0)
        _NoiseScroll ("Scroll", Vector) = (0, 0, 0, 0)
        _NoiseRotation ("Rotation", Float) = 0
        _NoiseCenter ("Center", Vector) = (0.5, 0.5, 0, 0)
        _NoiseStrength ("Strength", Float) = 1
        _NoiseGradientA ("GradientA", Color) = (0, 0, 0, 1)
        _NoiseGradientB ("GradientB", Color) = (1, 1, 1, 1)
        _NoiseThreshold ("Threshold", Float) = 0.5
        _NoiseSoftness ("Softness", Float) = 0.1
        _NoiseBlend ("Blend", Float) = 0.5
        _NoiseOpacity ("Opacity", Float) = 1
        _NoiseClearColor ("ClearColor", Color) = (0, 0, 0, 0)
        _NoiseOctaves ("Octaves", Int) = 4
        _NoiseLacunarity ("Lacunarity", Float) = 2.0
        _NoiseGain ("Gain", Float) = 0.5
        _NoiseMaskTex ("Mask", 2D) = "black" {}
        _NoiseStageOp ("StageOp", Int) = 0
    }

    HLSLINCLUDE
    #include "UnityCG.cginc"

    sampler2D _NoiseInputTex;
    sampler2D _NoiseSecondaryTex;
    sampler2D _NoiseMaskTex;

    float _NoiseTime;
    float _NoiseSeed;
    float2 _NoiseScale;
    float2 _NoiseOffset;
    float2 _NoiseScroll;
    float _NoiseRotation;
    float2 _NoiseCenter;
    float _NoiseStrength;
    float4 _NoiseGradientA;
    float4 _NoiseGradientB;
    float _NoiseThreshold;
    float _NoiseSoftness;
    float _NoiseBlend;
    float _NoiseOpacity;
    float4 _NoiseClearColor;
    int _NoiseOctaves;
    float _NoiseLacunarity;
    float _NoiseGain;
    int _NoiseStageOp;

    struct appdata
    {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
    };

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    v2f vert(appdata v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.uv;
        return o;
    }

    // ── Noise Functions ─────────────────────────────────────

    float2 hash22(float2 p)
    {
        float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
        p3 += dot(p3, p3.yzx + 33.33);
        return frac((p3.xx + p3.yz) * p3.zy);
    }

    float hash21(float2 p)
    {
        float3 p3 = frac(float3(p.xyx) * 0.1031);
        p3 += dot(p3, p3.yzx + 33.33);
        return frac((p3.x + p3.y) * p3.z);
    }

    float valueNoise(float2 uv, float seed)
    {
        float2 i = floor(uv);
        float2 f = frac(uv);
        float2 u = f * f * (3.0 - 2.0 * f);

        float a = hash21(i + float2(0, 0) + seed);
        float b = hash21(i + float2(1, 0) + seed);
        float c = hash21(i + float2(0, 1) + seed);
        float d = hash21(i + float2(1, 1) + seed);

        return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
    }

    float fbm(float2 uv, float seed, int octaves, float lacunarity, float gain)
    {
        float value = 0.0;
        float amplitude = 0.5;
        float frequency = 1.0;

        for (int i = 0; i < octaves; i++)
        {
            value += amplitude * valueNoise(uv * frequency + seed, seed + (float)i * 13.37);
            frequency *= lacunarity;
            amplitude *= gain;
        }
        return value;
    }

    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull Off
        ZWrite Off
        ZTest Always

        // ── Pass 0: Generator ───────────────────────────────
        Pass
        {
            Name "Generator"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragGenerator

            float4 fragGenerator(v2f i) : SV_Target
            {
                float2 uv = i.uv * _NoiseScale + _NoiseOffset;

                // SolidColor = 10
                if (_NoiseStageOp == 10)
                    return _NoiseGradientA;

                // GradientLinear = 20
                if (_NoiseStageOp == 20)
                    return lerp(_NoiseGradientA, _NoiseGradientB, i.uv.x);

                // GradientRadial = 30
                if (_NoiseStageOp == 30)
                {
                    float dist = length(i.uv - 0.5) * 2.0;
                    return lerp(_NoiseGradientA, _NoiseGradientB, saturate(dist));
                }

                // ValueNoise = 40
                if (_NoiseStageOp == 40)
                {
                    float n = valueNoise(uv, _NoiseSeed);
                    return float4(n, n, n, 1);
                }

                // PerlinLike = 50 (use smoothed value noise as approximation)
                if (_NoiseStageOp == 50)
                {
                    float n = valueNoise(uv * 1.17, _NoiseSeed + 7.31);
                    return float4(n, n, n, 1);
                }

                // SimplexLike = 60 (use variant hash for different pattern)
                if (_NoiseStageOp == 60)
                {
                    float n = valueNoise(uv * 0.87 + float2(3.7, 1.3), _NoiseSeed + 17.0);
                    return float4(n, n, n, 1);
                }

                // Fbm = 70
                if (_NoiseStageOp == 70)
                {
                    float n = fbm(uv, _NoiseSeed, _NoiseOctaves, _NoiseLacunarity, _NoiseGain);
                    return float4(n, n, n, 1);
                }

                return float4(0, 0, 0, 1);
            }
            ENDHLSL
        }

        // ── Pass 1: UV ──────────────────────────────────────
        Pass
        {
            Name "UV"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragUv

            float4 fragUv(v2f i) : SV_Target
            {
                float2 uv = i.uv;

                // Scroll = 10
                if (_NoiseStageOp == 10)
                    uv += _NoiseScroll * _NoiseTime;

                // Flow = 20
                if (_NoiseStageOp == 20)
                {
                    float2 flowDir = _NoiseScroll;
                    uv += flowDir * sin(_NoiseTime) * (0.5 * _NoiseStrength);
                }

                // Rotate = 30
                if (_NoiseStageOp == 30)
                {
                    float angle = _NoiseTime * _NoiseRotation;
                    float2 center = _NoiseCenter;
                    float s, c;
                    sincos(angle, s, c);
                    float2 p = uv - center;
                    uv = float2(p.x * c - p.y * s, p.x * s + p.y * c) + center;
                }

                // Polar = 40
                if (_NoiseStageOp == 40)
                {
                    float2 center = _NoiseCenter;
                    float2 delta = uv - center;
                    float r = length(delta);
                    float theta = atan2(delta.y, delta.x);
                    uv = float2(theta / 6.28318 + 0.5, r * 2.0);
                }

                // Encode UV as RG for downstream stages
                return float4(uv, 0, 1);
            }
            ENDHLSL
        }

        // ── Pass 2: Filter ──────────────────────────────────
        Pass
        {
            Name "Filter"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragFilter

            float4 fragFilter(v2f i) : SV_Target
            {
                float4 col = tex2D(_NoiseInputTex, i.uv);
                float4 filtered = col;

                // Levels = 20
                if (_NoiseStageOp == 20)
                {
                    float v = col.r;
                    v = smoothstep(_NoiseThreshold - _NoiseSoftness, _NoiseThreshold + _NoiseSoftness, v);
                    filtered = float4(v, v, v, col.a);
                }

                // Clamp = 30
                else if (_NoiseStageOp == 30)
                {
                    float v = saturate((col.r - _NoiseThreshold) / max(_NoiseSoftness, 0.001));
                    filtered = float4(v, v, v, col.a);
                }

                // Invert = 40
                else if (_NoiseStageOp == 40)
                {
                    filtered = float4(1 - col.rgb, col.a);
                }

                return lerp(col, filtered, saturate(_NoiseStrength));
            }
            ENDHLSL
        }

        // ── Pass 3: Composite ───────────────────────────────
        Pass
        {
            Name "Composite"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragComposite

            float4 fragComposite(v2f i) : SV_Target
            {
                float4 primary = tex2D(_NoiseInputTex, i.uv);
                float4 secondary = tex2D(_NoiseSecondaryTex, i.uv);
                float4 outputColor = primary;

                // Blend = 10
                if (_NoiseStageOp == 10)
                    outputColor = lerp(primary, secondary, _NoiseBlend);

                // Add = 20
                else if (_NoiseStageOp == 20)
                    outputColor = saturate(primary + secondary * _NoiseBlend);

                // Multiply = 30
                else if (_NoiseStageOp == 30)
                    outputColor = lerp(primary, primary * secondary, _NoiseBlend);

                // Min = 40
                else if (_NoiseStageOp == 40)
                    outputColor = min(primary, secondary);

                // Max = 50
                else if (_NoiseStageOp == 50)
                    outputColor = max(primary, secondary);

                else if (_NoiseStageOp == 60)
                {
                    float mask = tex2D(_NoiseMaskTex, i.uv).r;
                    outputColor = lerp(primary, secondary, saturate(mask * _NoiseBlend));
                }

                return lerp(primary, outputColor, saturate(_NoiseOpacity));
            }
            ENDHLSL
        }
    }

    Fallback Off
}

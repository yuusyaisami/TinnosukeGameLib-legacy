Shader "Hidden/TextureEffect/ColorShift"
{
    Properties
    {
        _MainTex ("Main Tex", 2D) = "white" {}
        _SourceTex ("Source Tex", 2D) = "white" {}
        _MaskTex ("Mask Tex", 2D) = "white" {}
        _HueShift ("Hue Shift", Float) = 0.0
        _SaturationMultiplier ("Saturation Mul", Float) = 1.0
        _ColorMultiply ("Color Multiply", Color) = (1,1,1,1)
        _ColorAdd ("Color Add", Color) = (0,0,0,0)
        _UseMask ("Use Mask", Float) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        ZWrite Off ZTest Always Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            sampler2D _MainTex;
            sampler2D _SourceTex;
            sampler2D _MaskTex;
            float _HueShift;
            float _SaturationMultiplier;
            fixed4 _ColorMultiply;
            fixed4 _ColorAdd;
            float _UseMask;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float3 RGBtoHSV(float3 c)
            {
                float4 K = float4(0.0, -1.0/3.0, 2.0/3.0, -1.0);
                float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                float d = q.x - min(q.w, q.y);
                float e = 1.0e-10;
                return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }

            float3 HSVtoRGB(float3 c)
            {
                float4 K = float4(1.0, 2.0/3.0, 1.0/3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);

                float3 hsv = RGBtoHSV(col.rgb);
                hsv.x = frac(hsv.x + _HueShift);
                hsv.y *= _SaturationMultiplier;
                col.rgb = HSVtoRGB(hsv);

                col *= _ColorMultiply;
                col += _ColorAdd;
                col = saturate(col);

                if (_UseMask < 0.5)
                    return col;

                fixed4 source = tex2D(_MainTex, i.uv);
                fixed mask = tex2D(_MaskTex, i.uv).r;
                return lerp(source, col, mask);
            }
            ENDCG
        }
    }
}

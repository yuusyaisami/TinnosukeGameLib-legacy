Shader "Hidden/TextureEffect/Blur"
{
    Properties
    {
        _MainTex ("Main Tex", 2D) = "white" {}
        _SourceTex ("Source Tex", 2D) = "white" {}
        _MaskTex ("Mask Tex", 2D) = "white" {}
        _BlurSize ("Blur Size", Float) = 1.0
        _UseMask ("Use Mask", Float) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        ZWrite Off ZTest Always Cull Off

        // Pass 0: Separable blur
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _BlurSize;
            float4 _BlurDirection;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 dir = _BlurDirection.xy * _BlurSize;
                fixed4 col = fixed4(0,0,0,0);

                // 9-tap gaussian approximation
                col += tex2D(_MainTex, i.uv - dir * 4.0) * 0.01621622;
                col += tex2D(_MainTex, i.uv - dir * 3.0) * 0.05405405;
                col += tex2D(_MainTex, i.uv - dir * 2.0) * 0.12162162;
                col += tex2D(_MainTex, i.uv - dir * 1.0) * 0.19459459;
                col += tex2D(_MainTex, i.uv)              * 0.22702703;
                col += tex2D(_MainTex, i.uv + dir * 1.0) * 0.19459459;
                col += tex2D(_MainTex, i.uv + dir * 2.0) * 0.12162162;
                col += tex2D(_MainTex, i.uv + dir * 3.0) * 0.05405405;
                col += tex2D(_MainTex, i.uv + dir * 4.0) * 0.01621622;

                return col;
            }
            ENDCG
        }

        // Pass 1: Mask composite (blur result + original via mask)
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            sampler2D _MainTex;
            sampler2D _SourceTex;
            sampler2D _MaskTex;
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

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 blurred = tex2D(_MainTex, i.uv);
                if (_UseMask < 0.5)
                    return blurred;

                fixed4 source = tex2D(_SourceTex, i.uv);
                fixed mask = tex2D(_MaskTex, i.uv).r;
                return lerp(source, blurred, mask);
            }
            ENDCG
        }
    }
}

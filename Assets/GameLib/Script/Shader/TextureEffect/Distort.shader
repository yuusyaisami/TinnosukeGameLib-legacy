Shader "Hidden/TextureEffect/Distort"
{
    Properties
    {
        _MainTex ("Main Tex", 2D) = "white" {}
        _SourceTex ("Source Tex", 2D) = "white" {}
        _MaskTex ("Mask Tex", 2D) = "white" {}
        _NoiseTex ("Noise Tex", 2D) = "gray" {}
        _DistortStrength ("Distort Strength", Float) = 0.1
        _TimeParam ("Time", Float) = 0.0
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
            sampler2D _NoiseTex;
            float _DistortStrength;
            float _TimeParam;
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
                // Sample noise for UV distortion
                float2 noiseUV = i.uv + float2(_TimeParam * 0.03, _TimeParam * 0.02);
                fixed4 noise = tex2D(_NoiseTex, noiseUV);
                float2 offset = (noise.rg - 0.5) * 2.0 * _DistortStrength;

                fixed4 distorted = tex2D(_MainTex, i.uv + offset);

                if (_UseMask < 0.5)
                    return distorted;

                fixed4 source = tex2D(_MainTex, i.uv);
                fixed mask = tex2D(_MaskTex, i.uv).r;
                return lerp(source, distorted, mask);
            }
            ENDCG
        }
    }
}

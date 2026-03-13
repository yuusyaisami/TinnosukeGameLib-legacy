Shader "Hidden/TextureEffect/Mosaic"
{
    Properties
    {
        _MainTex ("Main Tex", 2D) = "white" {}
        _SourceTex ("Source Tex", 2D) = "white" {}
        _MaskTex ("Mask Tex", 2D) = "white" {}
        _BlockSize ("Block Size", Float) = 16.0
        _TexSize ("Tex Size", Vector) = (1920, 1080, 0, 0)
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
            float _BlockSize;
            float4 _TexSize;
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
                float2 blockCount = _TexSize.xy / max(_BlockSize, 1.0);
                float2 mosaicUV = floor(i.uv * blockCount) / blockCount;
                fixed4 mosaic = tex2D(_MainTex, mosaicUV);

                if (_UseMask < 0.5)
                    return mosaic;

                fixed4 source = tex2D(_MainTex, i.uv);
                fixed mask = tex2D(_MaskTex, i.uv).r;
                return lerp(source, mosaic, mask);
            }
            ENDCG
        }
    }
}

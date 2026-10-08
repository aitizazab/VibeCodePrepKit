// The DOOM-like "pixel look": draws the low-resolution world picture (a point-filtered
// RenderTexture) over the whole screen, optionally reduced to _Levels colour steps per channel
// with a 4x4 ordered dither (the palette lock that makes mixed art read as one set).
// The steps are taken in gamma space so they look even.
Shader "JamKit/Doomlike/Screen"
{
    Properties
    {
        _MainTex ("Low-res picture", 2D) = "black" {}
        _Levels ("Colour steps per channel (0 = off)", Float) = 12
        _Dither ("Dither", Range(0, 1)) = 0.6
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "DoomlikeScreen"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                float _Levels;
                half _Dither;
            CBUFFER_END

            static const float Bayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb;
                if (_Levels >= 2.0)
                {
                    float2 px = floor(input.uv * _MainTex_TexelSize.zw);
                    int i = (int)(fmod(px.x, 4.0) + fmod(px.y, 4.0) * 4.0);
                    float t = (Bayer[i] + 0.5) / 16.0 - 0.5;
                    float steps = _Levels - 1.0;
                    float3 g = pow(saturate((float3)c), 1.0 / 2.2);
                    g = floor(g * steps + 0.5 + t * _Dither) / steps;
                    c = (half3)pow(saturate(g), 2.2);
                }
                return half4(c, 1.0h);
            }
            ENDHLSL
        }
    }
}

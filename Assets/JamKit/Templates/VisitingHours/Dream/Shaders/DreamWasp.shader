// A static "wasp": the dream's immune system up close. The texture is a mask (R body, G wings, B stripes);
// the body fills with flickering TV snow, the stripes flicker darker, the wings shimmer pale. Each wasp gets
// its own flicker from its world position. Transparent, faces the camera (the script turns it).
Shader "JamKit/Dream/Wasp"
{
    Properties
    {
        _MainTex ("Mask (R body, G wings, B stripes)", 2D) = "white" {}
        _Color ("Colour", Color) = (0.95, 0.93, 1, 1)
        _StripeColor ("Stripes", Color) = (0.45, 0.38, 0.6, 1)
        _HotColor ("Close colour", Color) = (1, 0.55, 0.68, 1)
        _Threat ("Threat", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "DreamWasp"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half4 _StripeColor;
                half4 _HotColor;
                half _Threat;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float fog : TEXCOORD1; float seed : TEXCOORD2; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.fog = ComputeFogFactor(o.positionCS.z);
                float3 c = UNITY_MATRIX_M._m03_m13_m23;
                o.seed = frac(dot(floor(c * 3.0), float3(0.137, 0.271, 0.419)));
                return o;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 m = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                float t = floor(_Time.y * 24.0 + i.seed * 50.0);
                float n = Hash(floor(i.uv * 22.0) + t + i.seed * 91.0);
                half3 body = lerp(_Color.rgb, _HotColor.rgb, _Threat * 0.8) * (0.5 + 0.6 * n);
                half3 stripes = _StripeColor.rgb * (0.6 + 0.5 * Hash(float2(t, i.seed)));
                half3 col = lerp(body, stripes, m.b);
                half wing = m.g * (0.35 + 0.25 * sin(_Time.y * 60.0 + i.seed * 20.0));
                half3 wingCol = half3(0.96, 0.97, 1.0);
                half bodyA = m.r;
                half a = saturate(bodyA + wing);
                col = lerp(wingCol, col, bodyA / max(a, 1e-3));
                col = MixFog(col, i.fog);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}

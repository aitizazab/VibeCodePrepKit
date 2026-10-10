// The dream's cast as flat cut-outs (billboards exist only in the dream): the picture as drawn, alpha-clipped,
// a touch warmed by the golden hour, URP fog. Two-sided so a turning cut-out never vanishes edge-on.
Shader "JamKit/Dream/Cutout"
{
    Properties
    {
        _MainTex ("Picture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 0.98, 0.95, 1)
        _Cutoff ("Alpha cut-off", Range(0, 1)) = 0.5
        _BackShade ("Back side shade", Range(0, 1)) = 0.7
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "DreamCutout"
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
                half _Cutoff;
                half _BackShade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float fog : TEXCOORD1; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                clip(c.a - _Cutoff);
                half3 col = c.rgb * _Color.rgb * IS_FRONT_VFACE(face, 1.0h, _BackShade);
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}

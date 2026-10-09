// Painted surfaces for Visiting Hours: walls, floors, the sky and the painted flats and people
// (billboards). Unlit, so the paintings keep their own light; a tint per area (cool for the
// hospital, warm for the dream) and a distance haze from globals the game sets
// (_VHFogColor, _VHFogParams: start, end, strength). Soft alpha edges blend (cut-outs keep their
// painted edges); the opaque material sets _SrcBlend One, _DstBlend Zero. Compatible with the
// DOOM-like's Billboard component (_Frame, _Color, _FlashColor, _Glow through a property block).
Shader "JamKit/VisitingHours/Painted"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Frame ("Frame (offset xy, size zw)", Vector) = (0, 0, 1, 1)
        _FlashColor ("Flash (a = amount)", Color) = (1, 1, 1, 0)
        _Glow ("Glow (ignores haze)", Range(0, 1)) = 0
        _FogScale ("Haze", Range(0, 1)) = 1
        _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.03
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst blend", Float) = 10
        [Enum(Off, 0, On, 1)] _ZWrite ("Depth write", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "VisitingHoursPainted"
            Cull Off
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float4 _Frame;
                half4 _FlashColor;
                half _Glow;
                half _FogScale;
                half _Cutoff;
                float _SrcBlend;
                float _DstBlend;
                float _ZWrite;
            CBUFFER_END

            half4 _VHFogColor;
            float4 _VHFogParams;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fog : TEXCOORD1;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.uv = _Frame.xy + input.uv * _Frame.zw;
                o.color = input.color;
                float d = distance(positionWS, _WorldSpaceCameraPos);
                o.fog = (half)(saturate((d - _VHFogParams.x) / max(_VHFogParams.y - _VHFogParams.x, 0.001)) * _VHFogParams.z * _FogScale);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                clip(t.a - _Cutoff);
                half3 c = t.rgb * _Color.rgb * input.color.rgb;
                c = lerp(c, _FlashColor.rgb, _FlashColor.a);
                c = lerp(c, _VHFogColor.rgb, input.fog * (1.0h - _Glow));
                return half4(c, t.a * _Color.a * input.color.a);
            }
            ENDHLSL
        }
    }
}

// Level geometry and doors for the DOOM-like template: unlit, texture x vertex colour x tint.
// The builder bakes light into the vertex colours (walls facing north/south are darker, the map's
// "light" and the theme's tints), so no lights are needed. Distance fog and the muzzle flash are
// global values the game sets every frame (_DLFogColor, _DLFogParams, _DLFlash).
// _Scroll animates the texture (slime), _Glow ignores darkness and fog (the exit pad, lamps).
Shader "JamKit/Doomlike/World"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Scroll ("Scroll (UV per second)", Vector) = (0, 0, 0, 0)
        _Glow ("Glow", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "DoomlikeWorld"
            Cull Back
            ZWrite On

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
                float4 _Scroll;
                half _Glow;
            CBUFFER_END

            // Set by DoomGame each frame (all zero = no fog, no flash).
            half4 _DLFogColor;
            float4 _DLFogParams; // x: start, y: end, z: strength
            half _DLFlash;

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
                half4 color : COLOR;
                half fog : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.uv = TRANSFORM_TEX(input.uv, _MainTex) + _Scroll.xy * _Time.y;
                o.color = input.color;
                float d = distance(positionWS, _WorldSpaceCameraPos);
                o.fog = (half)(saturate((d - _DLFogParams.x) / max(_DLFogParams.y - _DLFogParams.x, 0.001)) * _DLFogParams.z);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half3 lit = t.rgb * input.color.rgb * _Color.rgb;
                half3 c = lerp(lit, t.rgb * _Color.rgb, _Glow);
                c += c * _DLFlash * 0.55h;
                c = lerp(c, _DLFogColor.rgb, input.fog * (1.0h - _Glow * 0.85h));
                return half4(c, 1.0h);
            }
            ENDHLSL
        }
    }
}

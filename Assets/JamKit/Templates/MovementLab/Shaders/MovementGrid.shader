// Grey-box surfaces for the Movement Lab: a flat colour with a world-space grid (1 m lines, a
// brighter line every 5 m) so speed reads at a glance, a fixed soft light from the face's normal, and
// URP fog for depth. Opaque and unlit; it receives the sun's shadow (only the player's body casts one).
Shader "JamKit/MovementLab/Grid"
{
    Properties
    {
        _Color ("Colour", Color) = (0.58, 0.6, 0.64, 1)
        _LineColor ("Line colour", Color) = (0.82, 0.84, 0.88, 1)
        _LineWidth ("Line width (m)", Range(0.005, 0.1)) = 0.03
        _Glow ("Glow (ignores the light)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "MovementLabGrid"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _LineColor;
                float _LineWidth;
                half _Glow;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fog : TEXCOORD2;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float GridLine(float2 p, float w)
            {
                float2 g = abs(frac(p - 0.5) - 0.5);
                float2 aa = fwidth(p) * 1.2;
                float2 l = 1.0 - smoothstep(w - aa, w + aa, g);
                return max(l.x, l.y);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 an = abs(n);
                // Pick the plane the face lies in (triplanar without blending: boxes are axis-aligned).
                float2 p = an.y > 0.5 ? i.positionWS.xz : (an.x > an.z ? i.positionWS.zy : i.positionWS.xy);
                float minor = GridLine(p, _LineWidth);
                float major = GridLine(p / 5.0, _LineWidth / 5.0 * 1.6);
                half3 col = lerp(_Color.rgb, _LineColor.rgb, saturate(minor * 0.55 + major));
                float3 L = normalize(float3(0.35, 0.85, 0.4));
                half light = 0.62 + 0.38 * saturate(dot(n, L)) + 0.08 * n.y;
                col *= lerp(light, 1.0, _Glow);
                // The player's shadow (the sun's shadow map; 1 where lit).
                half shadow = MainLightRealtimeShadow(TransformWorldToShadowCoord(i.positionWS));
                col *= lerp(0.62h, 1.0h, lerp(shadow, 1.0h, _Glow));
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}

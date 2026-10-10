// Speed lines for first-person movement (Runtime/Unity/Movement/SpeedLines.cs): thin streaks around the
// edge of the view that race outward. One static mesh; the motion is all here. Each streak's quad has
// uv.x along it (0 inner .. 1 outer), uv.y across (-1 .. 1) and a random seed in the vertex colour's
// alpha. Drawn over everything, never inside the centre of the screen (the mesh starts outside it).
// Contrast-proof (docs/SPEED-FEEL.md V3): each streak has a dark core (_CoreColor) and a light rim (_Color), blended
// with premultiplied alpha instead of added, so it shows on bright pastel skies and on dark scenes alike.
Shader "JamKit/Movement/SpeedLines"
{
    Properties
    {
        _Color ("Rim (light)", Color) = (0.95, 0.97, 1, 1)
        _CoreColor ("Core (dark)", Color) = (0.12, 0.13, 0.18, 1)
        _CoreWidth ("Core width (share of the streak)", Range(0, 1)) = 0.45
        _Intensity ("Intensity", Range(0, 1)) = 0
        _Speed ("Streak speed", Float) = 2.2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+100" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "SpeedLines"
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _CoreColor;
                half _CoreWidth;
                half _Intensity;
                float _Speed;
            CBUFFER_END

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
                half seed : TEXCOORD1;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.seed = v.color.a;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float t = frac(_Time.y * _Speed * (0.6 + i.seed) + i.seed * 7.31);
                // A short dash racing outward along the streak.
                float d = i.uv.x - t;
                half dash = saturate(1.0 - abs(d) / 0.22);
                // Across the streak: a dark core in the middle, a light rim either side, soft outer edge.
                half y = abs(i.uv.y);
                half body = 1.0 - smoothstep(0.7, 1.0, y);
                half core = 1.0 - smoothstep(_CoreWidth * 0.7, _CoreWidth, y);
                half3 col = lerp(_Color.rgb, _CoreColor.rgb, core);
                half inner = saturate(i.uv.x * 3.0);
                half a = saturate(dash * body * inner * _Intensity * (0.45 + 0.55 * frac(i.seed * 13.7)) * 1.6);
                return half4(col * a, a);
            }
            ENDHLSL
        }
    }
}

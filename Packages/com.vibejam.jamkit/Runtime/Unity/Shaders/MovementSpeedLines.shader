// Speed lines for first-person movement (Runtime/Unity/Movement/SpeedLines.cs): thin streaks around the
// edge of the view that race outward. One static mesh; the motion is all here. Each streak's quad has
// uv.x along it (0 inner .. 1 outer), uv.y across (-1 .. 1) and a random seed in the vertex colour's
// alpha. Additive, drawn over everything, never inside the centre of the screen (the mesh starts outside it).
Shader "JamKit/Movement/SpeedLines"
{
    Properties
    {
        _Color ("Tint", Color) = (0.9, 0.95, 1, 1)
        _Intensity ("Intensity", Range(0, 1)) = 0
        _Speed ("Streak speed", Float) = 2.2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+100" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "SpeedLines"
            Blend One One
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
                half across = saturate(1.0 - abs(i.uv.y));
                half inner = saturate(i.uv.x * 3.0);
                half a = dash * across * inner * _Intensity * (0.45 + 0.55 * frac(i.seed * 13.7));
                return half4(_Color.rgb * a, 0);
            }
            ENDHLSL
        }
    }
}

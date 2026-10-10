// The Movement Lab's pursuer: a wall of dream-static. Flickering blocky noise that scrolls, brighter at
// the leading edge, fading out at its top and sides. Transparent; _Threat (0..1, set by script) makes it
// hotter and denser as it closes in. PLACEHOLDER for the dream sequences' real look.
Shader "JamKit/MovementLab/DreamStatic"
{
    Properties
    {
        _Color ("Colour", Color) = (0.78, 0.74, 0.98, 1)
        _HotColor ("Close colour", Color) = (1, 0.45, 0.55, 1)
        _Threat ("Threat", Range(0, 1)) = 0
        _Cells ("Noise cells", Float) = 48
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "DreamStatic"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _HotColor;
                half _Threat;
                float _Cells;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
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
                float t = floor(_Time.y * 18.0);
                float2 cell = floor(float2(i.uv.x * _Cells, i.uv.y * _Cells * 0.6 + _Time.y * 3.0));
                float n = Hash(cell + t);
                float band = step(0.5, frac(i.uv.y * 9.0 + _Time.y * 0.7 + Hash(float2(t, 1)) * 0.2));
                float edge = smoothstep(0.0, 0.12, i.uv.x) * smoothstep(1.0, 0.88, i.uv.x) * smoothstep(1.0, 0.7, i.uv.y);
                half3 col = lerp(_Color.rgb, _HotColor.rgb, _Threat) * (0.55 + 0.6 * n);
                half a = edge * saturate(0.35 + 0.45 * n + 0.15 * band + 0.25 * _Threat);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}

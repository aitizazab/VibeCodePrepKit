// The dream's immune system as a wall: pastel TV static (flickering blocky snow with colour fringes and rolling
// scan bands) that frays into the air at its top and sides. _Threat (0..1, set by script) makes it denser and
// hotter (pink) as it closes in. Transparent, two-sided.
Shader "JamKit/Dream/Static"
{
    Properties
    {
        _Color ("Colour", Color) = (0.92, 0.9, 1, 1)
        _HotColor ("Close colour", Color) = (1, 0.55, 0.68, 1)
        _Threat ("Threat", Range(0, 1)) = 0
        _Cells ("Noise cells", Float) = 64
        _Alpha ("Opacity", Range(0, 1)) = 0.85
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
            #pragma multi_compile_fog
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _HotColor;
                half _Threat;
                float _Cells;
                half _Alpha;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float fog : TEXCOORD1; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
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
                float t = floor(_Time.y * 20.0);
                float cells = _Cells * (1.0 + _Threat * 0.6);
                float2 cell = floor(float2(i.uv.x * cells, i.uv.y * cells * 0.7 + _Time.y * 4.0));
                float n = Hash(cell + t);
                float nr = Hash(cell + t + 17.0), nb = Hash(cell + t + 41.0);
                float band = smoothstep(0.35, 0.65, frac(i.uv.y * 7.0 - _Time.y * 0.9 + Hash(float2(t, 3.0)) * 0.15));
                float fray = Hash(float2(floor(i.uv.x * 40.0), t)) * 0.25;
                float edge = smoothstep(0.0, 0.1, i.uv.x) * smoothstep(1.0, 0.9, i.uv.x) * smoothstep(1.0, 0.62 - fray, i.uv.y);
                half3 tint = lerp(_Color.rgb, _HotColor.rgb, _Threat);
                half3 col = tint * (0.55 + 0.55 * n) + half3(nr - 0.5, 0.0, nb - 0.5) * 0.18;
                col *= 0.9 + 0.2 * band;
                half a = edge * saturate(0.4 + 0.45 * n + 0.15 * band + 0.25 * _Threat) * _Alpha;
                col = MixFog(col, i.fog);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}

// Soft additive light for the dream: the warm pools that mark the route on the ground, the exit's pillar of
// light, the glow under the garage door. A radial falloff on a quad (_Mode 0) or a vertical fade on a
// cylinder (_Mode 1), a slow breathing pulse, fading with fog. No depth write.
Shader "JamKit/Dream/Glow"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0.8, 0.45, 1)
        _Intensity ("Intensity", Range(0, 4)) = 0.6
        _Mode ("Mode (0 disc, 1 pillar)", Float) = 0
        _Pulse ("Pulse", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "DreamGlow"
            Blend One One
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
                half _Intensity;
                float _Mode;
                half _Pulse;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float fog : TEXCOORD1; float phase : TEXCOORD2; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                o.phase = dot(UNITY_MATRIX_M._m03_m13_m23, float3(0.21, 0.0, 0.17));
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float k;
                if (_Mode < 0.5)
                {
                    float r = length(i.uv - 0.5) * 2.0;
                    k = saturate(1.0 - r);
                    k = k * k;
                }
                else
                {
                    k = smoothstep(1.0, 0.0, i.uv.y) * (0.55 + 0.45 * sin(i.uv.x * 6.2831 * 3.0 + _Time.y * 0.7));
                }
                float pulse = 1.0 - _Pulse + _Pulse * (0.5 + 0.5 * sin(_Time.y * 1.6 + i.phase));
                half3 col = _Color.rgb * k * _Intensity * pulse;
                col = MixFogColor(col, half3(0, 0, 0), i.fog); // additive light fades out into the fog
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}

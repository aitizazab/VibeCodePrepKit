// The stealth kit's readable overlays (Runtime/Unity/Stealth): a seeker's vision cone on the ground and the detection
// meter above its head. Unlit, transparent, colour = _Color x vertex colour (alpha too), no lighting, both faces.
// _ZTestMode 4 (LessEqual) for the cone on the ground, 8 (Always) for the meter so walls never hide it.
Shader "JamKit/Stealth/Overlay"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 1, 1, 0.35)
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTestMode ("Z test", Float) = 4
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "StealthOverlay"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTestMode]
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _ZTestMode;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                return _Color * i.color;
            }
            ENDHLSL
        }
    }
}

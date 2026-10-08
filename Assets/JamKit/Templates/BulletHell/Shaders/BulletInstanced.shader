// Bullets, coins, sparks and stars for the bullet-hell template, drawn with GPU instancing
// (Graphics.RenderMeshInstanced). Each instance has its own colour (_BulletColor, set per instance
// through a MaterialPropertyBlock array). The texture's red channel marks the bright core and its
// alpha the shape, so one white-cored circle texture serves every bullet colour.
// Unlit, alpha blended, URP (and works with any SRP that renders the default unlit pass).
Shader "JamKit/BulletInstanced"
{
    Properties
    {
        _MainTex ("Shape (R = core, A = shape)", 2D) = "white" {}
        _Core ("Core brightness", Range(0, 1)) = 0.85
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "BulletInstanced"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _Core;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(BulletProps)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BulletColor)
            UNITY_INSTANCING_BUFFER_END(BulletProps)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                o.color = UNITY_ACCESS_INSTANCED_PROP(BulletProps, _BulletColor);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half3 rgb = lerp((half3)input.color.rgb, half3(1, 1, 1), t.r * _Core);
                return half4(rgb, t.a * (half)input.color.a);
            }
            ENDHLSL
        }
    }
}

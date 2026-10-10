// The Gauntlet's ghost of your best run: a translucent figure with a bright rim, flickering faintly, so it reads
// against any surface without hiding the route. Transparent, no depth write; URP fog.
Shader "JamKit/MovementLab/Ghost"
{
    Properties
    {
        _Color ("Colour", Color) = (0.55, 0.85, 1, 0.28)
        _RimColor ("Rim", Color) = (0.8, 0.95, 1, 0.9)
        _RimPower ("Rim power", Range(0.5, 8)) = 2.5
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "GauntletGhost"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _RimColor;
                half _RimPower;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 viewWS : TEXCOORD1; float fog : TEXCOORD2; float y : TEXCOORD3; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(ws);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.viewWS = GetWorldSpaceViewDir(ws);
                o.fog = ComputeFogFactor(o.positionCS.z);
                o.y = ws.y;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 v = normalize(i.viewWS);
                half rim = pow(1.0 - saturate(dot(n, v)), _RimPower);
                half scan = 0.85 + 0.15 * sin(i.y * 18.0 - _Time.y * 6.0);
                half3 col = lerp(_Color.rgb, _RimColor.rgb, rim);
                half a = saturate(_Color.a + rim * _RimColor.a) * scan;
                col = MixFog(col, i.fog);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}

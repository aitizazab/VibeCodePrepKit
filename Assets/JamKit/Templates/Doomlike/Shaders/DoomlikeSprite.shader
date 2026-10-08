// Billboards for the DOOM-like template (enemies, pickups, decorations, effects, the weapon in
// your hands): one cell of a sprite sheet (_Frame: UV offset xy, size zw; a negative width mirrors
// it), alpha cut out, tinted, with a hit flash (_FlashColor.a = amount). Per-object values come
// from a MaterialPropertyBlock. Fog and the muzzle flash use the same globals as the world shader.
// Vertex colours tint too (the enemies' aim lines are LineRenderers with this shader).
// The weapon material sets _ZTest to Always, _ZWrite off and _FogScale 0 so the gun never clips.
Shader "JamKit/Doomlike/Sprite"
{
    Properties
    {
        _MainTex ("Sprite sheet", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Frame ("Frame (offset xy, size zw)", Vector) = (0, 0, 1, 1)
        _FlashColor ("Flash (a = amount)", Color) = (1, 1, 1, 0)
        _Glow ("Glow", Range(0, 1)) = 0
        _FogScale ("Fog", Range(0, 1)) = 1
        _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth test", Float) = 4
        [Enum(Off, 0, On, 1)] _ZWrite ("Depth write", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "DoomlikeSprite"
            Cull Off
            ZWrite [_ZWrite]
            ZTest [_ZTest]

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
                float4 _Frame;
                half4 _FlashColor;
                half _Glow;
                half _FogScale;
                half _Cutoff;
                float _ZTest;
                float _ZWrite;
            CBUFFER_END

            half4 _DLFogColor;
            float4 _DLFogParams;
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
                half fog : TEXCOORD1;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.uv = _Frame.xy + input.uv * _Frame.zw;
                o.color = input.color;
                float d = distance(positionWS, _WorldSpaceCameraPos);
                o.fog = (half)(saturate((d - _DLFogParams.x) / max(_DLFogParams.y - _DLFogParams.x, 0.001)) * _DLFogParams.z * _FogScale);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                clip(t.a - _Cutoff);
                half3 c = t.rgb * _Color.rgb * input.color.rgb;
                c = lerp(c, _FlashColor.rgb, _FlashColor.a);
                c += c * _DLFlash * 0.45h * (1.0h - _Glow);
                c = lerp(c, _DLFogColor.rgb, input.fog * (1.0h - _Glow * 0.85h));
                return half4(c, 1.0h);
            }
            ENDHLSL
        }
    }
}

// The waking world of Visiting Hours (art style E): muted, stylised, soft. Lit by URP's real lights
// (the main light with soft shadows, spot and point lights, light cookies, Forward and Forward+), but
// shaded in soft steps: a lit side and a cool shade side with a soft band between them, a gentle rim,
// a hemisphere ambient the scene sets (_VHWakeSky, _VHWakeGround), a distance haze (_VHWakeFog: rgb,
// a = metres to full haze; 0 = none), a little of Unity's own ambient (_VHWakeSH) and a thin dark outline (a second, inverted-hull pass).
// Per object: an emission map (screens), a hover highlight (_Highlight) and a dithered fade (_Fade).
// URP's screen-space ambient occlusion (the PC renderer's SSAO feature) darkens the ambient and, a little, the direct
// light in creases and contact points; its DepthNormals pass feeds it. Without the feature (Mobile, WebGL) nothing changes.
// Reusable for every waking-world scene: make one material per colour (VhWakingStyle does this).
Shader "JamKit/VisitingHours/Waking"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Colour", Color) = (0.7, 0.7, 0.7, 1)
        _VertexColorWeight ("Use vertex colours", Range(0, 1)) = 0
        [Header(Shading)]
        _ShadeColor ("Shade tint", Color) = (0.62, 0.66, 0.78, 1)
        _ShadeStep ("Shade step", Range(0, 1)) = 0.52
        _ShadeSoftness ("Shade softness", Range(0.001, 0.5)) = 0.14
        _RimColor ("Rim colour", Color) = (0.75, 0.8, 0.9, 1)
        _RimPower ("Rim power", Range(0.5, 8)) = 3.5
        _RimStrength ("Rim strength", Range(0, 1)) = 0.18
        _AmbientScale ("Ambient", Range(0, 2)) = 1
        [Header(Own ambient (scenes that do not set the globals))]
        _SkyAmbient ("Sky ambient", Color) = (0, 0, 0, 1)
        _GroundAmbient ("Ground ambient", Color) = (0, 0, 0, 1)
        _SHAmbient ("Unity ambient", Range(0, 1)) = 0
        [Header(First person)]
        _CameraCutout ("Cut away around (xyz, radius w; set per frame)", Vector) = (0, 0, 0, 0)
        [Header(Emission)]
        _EmissionMap ("Emission map", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        [Header(Highlight and fade)]
        [HDR] _HighlightColor ("Highlight colour", Color) = (1.0, 0.86, 0.62, 1)
        _Highlight ("Highlight", Range(0, 1)) = 0
        _Fade ("Fade (dithered)", Range(0, 1)) = 1
        [Header(Outline)]
        _OutlineColor ("Outline colour", Color) = (0.13, 0.13, 0.16, 1)
        _OutlineWidth ("Outline width (pixels)", Range(0, 4)) = 1.1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_EmissionMap);
        SAMPLER(sampler_EmissionMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _VertexColorWeight;
            half4 _ShadeColor;
            half _ShadeStep;
            half _ShadeSoftness;
            half4 _RimColor;
            half _RimPower;
            half _RimStrength;
            half _AmbientScale;
            half4 _SkyAmbient;
            half4 _GroundAmbient;
            half _SHAmbient;
            float4 _CameraCutout;
            float4 _EmissionMap_ST;
            half4 _EmissionColor;
            half4 _HighlightColor;
            half _Highlight;
            half _Fade;
            half4 _OutlineColor;
            half _OutlineWidth;
        CBUFFER_END

        // A 4x4 ordered dither: _Fade 1 keeps every pixel, 0 none.
        static const float kBayer4[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        void DitherClip(float2 pixel)
        {
            if (_Fade >= 0.999h) return;
            uint2 p = (uint2)pixel & 3u;
            float threshold = (kBayer4[p.y * 4u + p.x] + 0.5) / 16.0;
            clip(_Fade - threshold);
        }

        // A first-person body's own head and hair: cut away within _CameraCutout.w of the camera (its shadow keeps them).
        void CutoutClip(float3 positionWS)
        {
            if (_CameraCutout.w <= 0.0) return;
            float3 d = positionWS - _CameraCutout.xyz;
            clip(dot(d, d) - _CameraCutout.w * _CameraCutout.w);
        }
        ENDHLSL

        Pass
        {
            Name "WakingLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            half4 _VHWakeSky;
            half4 _VHWakeGround;
            half4 _VHWakeFog;
            half _VHWakeSH;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.color = input.color;
                return o;
            }

            half Ramp(half ndl)
            {
                half h = ndl * 0.5h + 0.5h;
                return smoothstep(_ShadeStep - _ShadeSoftness, _ShadeStep + _ShadeSoftness, h);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                DitherClip(input.positionCS.xy);
                CutoutClip(input.positionWS);

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 albedo = tex.rgb * _BaseColor.rgb * lerp(half3(1, 1, 1), input.color.rgb, _VertexColorWeight);
                float3 n = normalize(input.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));

                // Ambient: the scene's sky and ground colours, plus a little of Unity's own ambient.
                half up = saturate(n.y * 0.5h + 0.5h);
                half3 ambient = lerp(_VHWakeGround.rgb + _GroundAmbient.rgb, _VHWakeSky.rgb + _SkyAmbient.rgb, up) + SampleSH(n) * (_VHWakeSH + _SHAmbient);
                ambient *= _AmbientScale;

                half3 direct = 0;
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord, input.positionWS, half4(1, 1, 1, 1));
                direct += mainLight.color * Ramp(dot(n, mainLight.direction)) * mainLight.shadowAttenuation * mainLight.distanceAttenuation;

                #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = n;
                inputData.viewDirectionWS = v;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                    direct += light.color * Ramp(dot(n, light.direction)) * light.distanceAttenuation * light.shadowAttenuation;
                LIGHT_LOOP_END
                #endif

                #if defined(_SCREEN_SPACE_OCCLUSION)
                // Contact shading: occlusion from URP's SSAO pass (fed by the DepthNormals pass below).
                AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(input.positionCS));
                ambient *= ao.indirectAmbientOcclusion;
                direct *= ao.directAmbientOcclusion;
                #endif

                half3 c = albedo * (ambient * _ShadeColor.rgb + direct);

                // A gentle rim, brighter where light falls.
                half rim = pow(saturate(1.0h - saturate(dot(n, v))), _RimPower);
                half lum = saturate(dot(direct + ambient, half3(0.3h, 0.5h, 0.2h)));
                c += _RimColor.rgb * rim * _RimStrength * (0.35h + 0.65h * lum);

                c += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv * _EmissionMap_ST.xy + _EmissionMap_ST.zw).rgb * _EmissionColor.rgb;
                c += _HighlightColor.rgb * _Highlight * (0.12h + rim * 0.9h);

                if (_VHWakeFog.a > 0.0h)
                {
                    float d = distance(input.positionWS, _WorldSpaceCameraPos);
                    c = lerp(c, _VHWakeFog.rgb, saturate(d / _VHWakeFog.a) * 0.85h);
                }
                return half4(c, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "WakingOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #pragma target 3.0
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings OutlineVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float4 cs = TransformObjectToHClip(input.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(input.normalOS);
                float2 nCS = mul((float3x3)GetWorldToHClipMatrix(), nWS).xy;
                float len = max(length(nCS), 1e-4);
                // _OutlineWidth pixels, whatever the distance (cs.w undoes the perspective divide).
                cs.xy += (nCS / len) * (_OutlineWidth * 2.0 / _ScreenParams.xy) * cs.w;
                o.positionCS = _OutlineWidth > 0.001 ? cs : float4(0, 0, -2, 1);
                return o;
            }

            half4 OutlineFrag(Varyings input) : SV_Target
            {
                DitherClip(input.positionCS.xy);
                CutoutClip(input.positionWS);
                return half4(_OutlineColor.rgb, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment WakingShadowFrag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"

            half4 WakingShadowFrag(Varyings input) : SV_TARGET
            {
                DitherClip(input.positionCS.xy);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma target 3.0
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }

            half DepthFrag(Varyings input) : SV_Target
            {
                DitherClip(input.positionCS.xy);
                CutoutClip(input.positionWS);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings DepthNormalsVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            half4 DepthNormalsFrag(Varyings input) : SV_Target
            {
                DitherClip(input.positionCS.xy);
                CutoutClip(input.positionWS);
                float3 n = normalize(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct = saturate(PackNormalOctQuadEncode(n) * 0.5 + 0.5);
                return half4(PackFloat2To888(oct), 0.0);
                #else
                return half4(n, 0.0);
                #endif
            }
            ENDHLSL
        }
    }

    FallBack Off
}

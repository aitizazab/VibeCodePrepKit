// Skyline/InkLit: the comic-ink surface (docs/SKYLINE-STYLE.md rules 1, 3, 4). Flat painted colour, the sun lighting in three
// crisp anti-aliased bands (lit, half, shade; shade takes the ambient's cool tint), light hatching in the deepest band, banded
// hard-edged pools for every additional light (a strong local light takes a room over), a rim, and a mask pass that draws
// the thick hued outline's silhouette for the SkylineInkFeature.
//
// Written against URP 17.3 (Forward+: the keyword in THIS package version is _CLUSTER_LIGHT_LOOP; `_FORWARD_PLUS` is only a
// deprecated alias). The keyword lists in ForwardLit are copied from Shaders/Lit.shader, the light loop from
// ShaderLibrary/Lighting.hlsl (UniversalFragmentBlinnPhong), and ShadowCaster / DepthOnly / DepthNormals include URP's own
// pass files, so their bias, clamping and normal packing are URP's.
//
// Roles (_InkRole): 0 none, 1 hostile, 2 objective, 3 scrap, 4 viewmodel (thin ink), 5 weak spot. Pass SkylineInkMask draws
// only when the material has the role set and the pass enabled (Material.SetShaderPassEnabled).
Shader "Skyline/InkLit"
{
    Properties
    {
        [MainTexture] _BaseMap("Painted map (optional)", 2D) = "white" {}
        [MainColor] _BaseColor("Colour", Color) = (1, 1, 1, 1)
        _BumpMap("Normal map", 2D) = "bump" {}
        _BumpScale("Normal scale", Float) = 1.0
        [HDR] _EmissionColor("Emission colour", Color) = (0, 0, 0)
        _EmissionMap("Emission map", 2D) = "white" {}

        _GradBottom("Gradient: brightness at the floor", Range(0, 1)) = 0.78
        _GradFloorY("Gradient: floor height (m)", Float) = 0
        _GradHeight("Gradient: height to full (m)", Float) = 6
        _Desaturate("Desaturate (colour-free zone)", Range(0, 1)) = 0
        _HatchStrength("Deep-shade hatching", Range(0, 1)) = 0.15
        _InkRole("Ink role (0 none, 1 hostile, 2 objective, 3 scrap, 4 viewmodel, 5 weak spot)", Float) = 0
        _RimStrength("Rim", Range(0, 1)) = 0.25

        [HideInInspector] _Cull("__cull", Float) = 2.0
        [HideInInspector] _QueueOffset("Queue offset", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
            "Queue" = "Geometry"
        }
        LOD 300

        // ------------------------------------------------------------------ ForwardLit
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            #pragma vertex InkVertex
            #pragma fragment InkFragment

            // Material keywords: the two a twin must keep from its Lit original.
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _EMISSION

            // Universal Pipeline keywords, copied from Lit's ForwardLit pass (the ones this shader uses: main-light shadows,
            // additional lights and their shadows, soft shadows, cookies, light layers, Forward+ and fog).
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            #include "Assets/JamKit/Templates/SkylineRepo/Look/Ink/InkLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #if defined(LOD_FADE_CROSSFADE)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 texcoord   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
            #if defined(_NORMALMAP)
                half4 tangentWS   : TEXCOORD3;     // xyz: tangent, w: sign
            #endif
                half fogFactor    : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings InkVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                half fogFactor = 0;
                #if !defined(_FOG_FRAGMENT)
                    fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                #endif

                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
            #if defined(_NORMALMAP)
                real sign = input.tangentOS.w * GetOddNegativeScale();
                output.tangentWS = half4(normalInput.tangentWS.xyz, sign);
            #endif
                output.fogFactor = fogFactor;
                output.positionCS = vertexInput.positionCS;
                return output;
            }

            /// One additional light as a hard-edged pool: a = how much of the light reaches the surface (distance falloff,
            /// shadow, facing), then two crisp steps. A strong light paints its colour across the room instead of a soft glow.
            half3 InkAdditionalLight(Light light, half3 normalWS, uint meshRenderingLayers)
            {
            #ifdef _LIGHT_LAYERS
                if (!IsMatchingLightLayer(light.layerMask, meshRenderingLayers)) return half3(0, 0, 0);
            #endif
                half a = light.distanceAttenuation * light.shadowAttenuation * saturate(dot(normalWS, light.direction));
                half band = smoothstep(0.03h, 0.06h, a) * 0.55h + smoothstep(0.25h, 0.3h, a) * 0.85h;
                return light.color * band;
            }

            void InkFragment(
                Varyings input
                , out half4 outColor : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            #ifdef LOD_FADE_CROSSFADE
                LODFadeCrossFade(input.positionCS);
            #endif

                float3 positionWS = input.positionWS;
                half3 albedo = InkAlbedo(input.uv, positionWS);

                // The normal (Lit's recipe: the interpolated normal, or the normal map through the tangent frame).
                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
            #if defined(_NORMALMAP)
                half3 normalTS = SampleNormal(input.uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
                float sgn = input.tangentWS.w;      // +1 or -1
                float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
                half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);
                normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, tangentToWorld));
            #endif
                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);

                // The variable must be called inputData: Forward+'s LIGHT_LOOP_BEGIN reads inputData.positionWS and
                // inputData.normalizedScreenSpaceUV to find the cluster.
                InputData inputData = (InputData)0;
                inputData.positionWS = positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewDirWS;
            #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                inputData.shadowCoord = TransformWorldToShadowCoord(positionWS);
            #else
                inputData.shadowCoord = float4(0, 0, 0, 0);
            #endif
                inputData.fogCoord = InitializeInputDataFog(float4(positionWS, 1.0), input.fogFactor);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                half4 shadowMask = CalculateShadowMask(inputData);
                uint meshRenderingLayers = GetMeshRenderingLayer();

                // ---- the sun, in three bands
                Light mainLight = GetMainLight(inputData.shadowCoord, positionWS, shadowMask);
            #ifdef _LIGHT_LAYERS
                if (!IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers)) mainLight.color = 0;
            #endif
                half x = saturate(dot(normalWS, mainLight.direction)) * mainLight.shadowAttenuation;
                half w = max(fwidth(x), 0.002h);
                half bandLit = smoothstep(0.45h - w, 0.45h + w, x);
                half bandHalf = smoothstep(0.08h - w, 0.08h + w, x);
                half sunIntensity = 0.28h + 0.32h * bandHalf + 0.4h * bandLit;

                // The shade colour is the ambient (SH evaluated per pixel: SampleSHPixel with neither EVALUATE_SH_* keyword).
                // The Trilight ambient is cool above, so shade takes the sky's tint.
                half3 ambient = SampleSHPixel(half3(0, 0, 0), normalWS);
                half3 color = albedo * (ambient * 0.9h + mainLight.color * sunIntensity);

                // ---- hatching in the deepest band only: world-space diagonal lines, a triangle wave thresholded with an
                // anti-aliased edge, faded out where the lines would be thinner than a pixel.
                float u = dot(positionWS, float3(0.70711, 0.70711, 0.0)) * 6.0;
                float aa = max(fwidth(u) * 1.5, 0.0001);
                float tri = abs(frac(u) - 0.5) * 2.0;
                half hatch = (half)smoothstep(0.5 - aa, 0.5 + aa, tri) * (half)saturate(1.0 - (aa - 0.3) * 2.5);
                color *= 1.0h - _HatchStrength * hatch * (1.0h - bandHalf);

                // ---- additional lights (Forward+ and the per-object path)
                half3 additional = 0;
            #if defined(_ADDITIONAL_LIGHTS)
                #if USE_CLUSTER_LIGHT_LOOP
                // Forward+ keeps the directional lights beyond the main one outside the cluster iterator.
                [loop] for (uint dirIndex = 0; dirIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); dirIndex++)
                {
                    uint lightIndex = dirIndex;
                    CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                    Light light = GetAdditionalLight(lightIndex, positionWS, shadowMask);
                    additional += InkAdditionalLight(light, normalWS, meshRenderingLayers);
                }
                #endif
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, positionWS, shadowMask);
                    additional += InkAdditionalLight(light, normalWS, meshRenderingLayers);
                LIGHT_LOOP_END
            #endif
                color += albedo * additional;

                // ---- rim, emission, fog
                half rim = pow(saturate(1.0h - saturate(dot(normalWS, viewDirWS))), 4.0h) * _RimStrength;
                color += mainLight.color * rim;
                color += SampleEmission(input.uv, _EmissionColor.rgb, TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap));
                color = MixFog(color, inputData.fogCoord);

                outColor = half4(color, 1.0h);
            #ifdef _WRITE_RENDERING_LAYERS
                outRenderingLayers = EncodeMeshRenderingLayer();
            #endif
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ ShadowCaster (URP's own pass file)
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            // Directional versus punctual shadow maps use different normal-bias formulas.
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Assets/JamKit/Templates/SkylineRepo/Look/Ink/InkLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        // ------------------------------------------------------------------ DepthOnly (URP's own pass file)
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing

            #include "Assets/JamKit/Templates/SkylineRepo/Look/Ink/InkLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        // ------------------------------------------------------------------ DepthNormals (URP's generic pass file)
        // Writes the interpolated vertex normal (NormalizeNormalPerPixel), not the normal map's: the ink lines follow the
        // shapes, not the surface texture.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing

            #include "Assets/JamKit/Templates/SkylineRepo/Look/Ink/InkLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }

        // ------------------------------------------------------------------ SkylineInkMask
        // Drawn by SkylineInkFeature's renderer list against the camera depth (LEqual, no depth write): only the visible
        // surface of a role > 0 object lands in the mask. rgb = the role's hue (sRGB, converted by the composite); alpha is the
        // class: 1.0 hued outline, 0.6 thin ink (the viewmodel), 0.8 weak spot (yellow, wins over the others).
        Pass
        {
            Name "SkylineInkMask"
            Tags { "LightMode" = "SkylineInkMask" }

            ZTest LEqual
            ZWrite Off
            Cull Back
            Blend Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex MaskVertex
            #pragma fragment MaskFragment
            #pragma multi_compile_instancing

            #include "Assets/JamKit/Templates/SkylineRepo/Look/Ink/InkLitInput.hlsl"

            struct MaskAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct MaskVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            MaskVaryings MaskVertex(MaskAttributes input)
            {
                MaskVaryings output = (MaskVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 MaskFragment(MaskVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                int role = (int)(_InkRole + 0.5h);
                if (role == 1) return half4(1.0h, 0.1804h, 0.3882h, 1.0h);        // #FF2E63 hostile
                if (role == 2) return half4(0.1804h, 0.9020h, 1.0h, 1.0h);        // #2EE6FF objective
                if (role == 3) return half4(0.3608h, 1.0h, 0.5412h, 1.0h);        // #5CFF8A scrap
                if (role == 4) return half4(0.0863h, 0.0784h, 0.1216h, 0.6h);     // #16141F ink: thin outline
                if (role == 5) return half4(1.0h, 0.8392h, 0.0392h, 0.8h);        // #FFD60A weak spot
                discard;
                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

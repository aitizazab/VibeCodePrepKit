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
// Roles (_InkRole): 0 none, 1 hostile #FF2E63, 2 objective violet #B36BFF, 3 strippable wreck light blue #8FD3FF (its width in
// _InkWidth), 4 viewmodel (thin ink), 5 weak spot (Writ) #FFD60A, 6 bot weak point, 7 objective x-ray only. Pass SkylineInkMask
// draws only when the material has the role set and the pass enabled (Material.SetShaderPassEnabled); pass SkylineInkXray
// (ZTest Greater: only where a nearer surface hides the object) draws roles 2 and 7, so the objective shows through walls.
// Mask alpha classes (read by InkComposite.shader): 1.0 hued (hostile, objective), 0.9 bot weak point, 0.8 weak spot,
// 0.6 thin ink, 0.4 objective seen through a wall, 0.11..0.30 strippable wreck (0.11 + 0.19 * width fraction).
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
        _HatchStrength("Deep-shade hatching", Range(0, 1)) = 0.08
        _InkRole("Ink role (0 none, 1 hostile, 2 objective, 3 strippable wreck, 4 viewmodel, 5 weak spot, 6 bot weak point, 7 objective x-ray only)", Float) = 0
        _InkWidth("Wreck outline width (fraction of full, role 3)", Range(0, 1)) = 1
        [Toggle] _Unlit("Unlit glow (no lighting)", Float) = 0
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

            // Optional stylization, set by SkylineInkFeature as a global each camera (0 = off; never a keyword, so a build keeps it):
            // x: halftone dots in the deepest shade (0..1), y: posterize the light bands to two (0..1).
            float4 _SkInkLitStyle;

            /// One additional light as a hard-edged pool: a = how much of the light reaches the surface (distance falloff,
            /// shadow, facing), then two crisp steps. A strong light paints its colour across the room instead of a soft glow.
            half3 InkAdditionalLight(Light light, half3 normalWS, uint meshRenderingLayers)
            {
            #ifdef _LIGHT_LAYERS
                if (!IsMatchingLightLayer(light.layerMask, meshRenderingLayers)) return half3(0, 0, 0);
            #endif
                half a = light.distanceAttenuation * light.shadowAttenuation * saturate(dot(normalWS, light.direction));
                half band = smoothstep(0.03h, 0.06h, a) * 0.55h + smoothstep(0.25h, 0.3h, a) * 0.85h;
                if (_SkInkLitStyle.y > 0.001) band = lerp(band, smoothstep(0.08h, 0.11h, a) * 0.8h, (half)_SkInkLitStyle.y);   // posterize: one crisp step
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
                // Fix round 1 (washed out): the shade band must read as its own painted tone, about half the lit value on screen
                // (about a fifth in linear light): a floor of 0.08 of the sun, 0.30 more in the half band, 0.62 in the lit band.
                half sunIntensity = 0.08h + 0.30h * bandHalf + 0.62h * bandLit;
                if (_SkInkLitStyle.y > 0.001)
                {
                    // posterize: two crisp tones instead of three (shade and lit), blended in by the strength
                    half bandMid = smoothstep(0.30h - w, 0.30h + w, x);
                    sunIntensity = lerp(sunIntensity, 0.10h + 0.60h * bandMid, (half)_SkInkLitStyle.y);
                }

                // The shade colour is the ambient (SH evaluated per pixel: SampleSHPixel with neither EVALUATE_SH_* keyword).
                // The Trilight ambient is cool above, so shade takes the sky's tint.
                half3 ambient = SampleSHPixel(half3(0, 0, 0), normalWS);
                // fix round 2: the shade takes a cool blue (sky) tint so lit/shade reads as warm sun against cool sky
                half3 color = albedo * (ambient * half3(0.86h, 0.96h, 1.14h) * 0.55h + mainLight.color * sunIntensity);

                // ---- hatching in the deepest band only: world-space diagonal lines, a triangle wave thresholded with an
                // anti-aliased edge, faded out where the lines would be thinner than a pixel.
                // Fix round 1 (moire on big walls and floors): half the density (3 lines a metre), only in the deepest shade, and
                // gone by 15 m.
                float u = dot(positionWS, float3(0.70711, 0.70711, 0.0)) * 3.0;
                float aa = max(fwidth(u) * 1.5, 0.0001);
                float tri = abs(frac(u) - 0.5) * 2.0;
                half hatch = (half)smoothstep(0.5 - aa, 0.5 + aa, tri) * (half)saturate(1.0 - (aa - 0.3) * 2.5)
                    * (half)(1.0 - smoothstep(8.0, 15.0, distance(positionWS, _WorldSpaceCameraPos)));
                half deepShade = 1.0h - smoothstep(0.0h, 0.3h, bandHalf);
                half halftone = (half)_SkInkLitStyle.x;
                color *= 1.0h - _HatchStrength * (1.0h - halftone) * hatch * deepShade;

                // ---- optional Ben-Day dots in the deepest band (halftone > 0): a 45-degree dot grid, 5 dots a metre, projected on the
                // plane the surface faces most (like the hatching it is world-space, so it does not swim with the camera), fading
                // out where the dots would be under about 3 pixels.
                if (halftone > 0.001h)
                {
                    float3 an = abs(normalWS);
                    float2 hp = (an.y >= an.x && an.y >= an.z) ? positionWS.xz : ((an.x >= an.z) ? positionWS.zy : positionWS.xy);
                    float2 g = float2(hp.x + hp.y, hp.x - hp.y) * (0.70711 * 5.0);
                    float dd = length(frac(g) - 0.5);
                    float aaH = max(fwidth(g.x) + fwidth(g.y), 0.0001) * 0.75;
                    float dots = (1.0 - smoothstep(0.34 - aaH, 0.34 + aaH, dd)) * saturate(1.0 - (aaH - 0.12) * 4.0)
                        * (1.0 - smoothstep(14.0, 26.0, distance(positionWS, _WorldSpaceCameraPos)));
                    color *= 1.0h - halftone * 0.55h * (half)dots * deepShade;
                }

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
                // an Unlit material's twin: the colour and map as they are (HDR colours stay hot for the bloom), no lighting
                if (_Unlit > 0.5h)
                    color = SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).rgb * _BaseColor.rgb;
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
        // class: 1.0 hued outline, 0.9 bot weak point (pulsing red-yellow), 0.8 weak spot (yellow), 0.6 thin ink (the viewmodel),
        // 0.11..0.30 strippable wreck (the width fraction in the alpha). Role 7 draws nothing here (the x-ray pass only).
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
                if (role == 2) return half4(0.7020h, 0.4196h, 1.0h, 1.0h);        // #B36BFF objective
                // #8FD3FF strippable wreck: its own alpha class, carrying the outline width fraction (0.4..1) for the composite
                if (role == 3) return half4(0.5608h, 0.8275h, 1.0h, 0.11h + 0.19h * saturate(_InkWidth));
                if (role == 4) return half4(0.0863h, 0.0784h, 0.1216h, 0.6h);     // #16141F ink: thin outline
                if (role == 5) return half4(1.0h, 0.8392h, 0.0392h, 0.8h);        // #FFD60A weak spot (the Writ's)
                if (role == 6) return half4(1.0h, 0.1804h, 0.3882h, 0.9h);        // a bot's own weak point: hostile hue in the mask, pulsed towards yellow by the composite
                discard;
                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ SkylineInkXray
        // The objective seen through walls (D4). Drawn by SkylineInkFeature FIRST, from the renderers in SkylineInkRoles.ObjectiveQueue
        // only, into the same mask: ZTest Greater keeps just the fragments a nearer surface hides (Unity flips the comparison itself
        // on reversed-Z platforms). The ordinary mask pass draws after it, so wherever the objective is visible its full-strength
        // outline overwrites this. Alpha 0.4 is the "seen through a wall" class; the composite draws it at 45%.
        Pass
        {
            Name "SkylineInkXray"
            Tags { "LightMode" = "SkylineInkXray" }

            ZTest Greater
            ZWrite Off
            Cull Back
            Blend Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex XrayVertex
            #pragma fragment XrayFragment
            #pragma multi_compile_instancing

            #include "Assets/JamKit/Templates/SkylineRepo/Look/Ink/InkLitInput.hlsl"

            struct XrayAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct XrayVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            XrayVaryings XrayVertex(XrayAttributes input)
            {
                XrayVaryings output = (XrayVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 XrayFragment(XrayVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                int role = (int)(_InkRole + 0.5h);
                if (role == 2 || role == 7) return half4(0.7020h, 0.4196h, 1.0h, 0.4h);   // #B36BFF objective, x-ray class
                discard;
                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

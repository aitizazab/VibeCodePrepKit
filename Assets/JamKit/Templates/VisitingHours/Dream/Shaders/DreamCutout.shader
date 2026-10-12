// The dream's memories as paper cut-outs (docs/redesign/people.md "Billboards"; DreamCutoutView builds the paper).
// The mesh carries its part in uv2.x: 0 = the picture (front), 1 = the back (card stock, the print faintly showing
// through), 2 = the cut edge (the card's core). A plain quad (no uv2) reads as all picture, two-sided, as before.
// Lit like the world: the main light with wrap diffuse and its shadows, the additional lights (Forward+ cluster loop:
// the house's lamps), ambient from the probes/sky, SSAO, fog; warm light through the paper when the sun is behind it;
// a faint fibre grain; a hand-drawn boil (the picture shifts by a hair, 8 times a second, per cut-out phase).
// Casts and receives shadows (alpha-tested). Passes: UniversalForward, ShadowCaster, DepthOnly, DepthNormals.
Shader "JamKit/Dream/Cutout"
{
    Properties
    {
        _MainTex ("Picture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 0.98, 0.95, 1)
        _Cutoff ("Alpha cut-off", Range(0, 1)) = 0.5
        _BackShade ("Back side shade (plain quads)", Range(0, 1)) = 0.7
        _BackColor ("Card back (mid-grey board)", Color) = (0.5, 0.49, 0.47, 1)
        _EdgeColor ("Card edge (core)", Color) = (0.86, 0.82, 0.74, 1)
        _ShowThrough ("Print showing through the back", Range(0, 1)) = 0.06
        _Wrap ("Wrap diffuse", Range(0, 1)) = 0.35
        _Translucency ("Light through the paper (backlit)", Range(0, 2)) = 0.3
        _Overlay ("Overlay (same UVs as the picture; alpha over it)", 2D) = "white" {}
        _OverlayOn ("Overlay on", Range(0, 1)) = 0
        _Grain ("Paper grain", Range(0, 0.2)) = 0.045
        _Boil ("Boil (UV shift)", Range(0, 0.01)) = 0.0012
        _BoilRate ("Boil frames per second", Range(0, 24)) = 8
        _Phase ("Phase (per cut-out)", Float) = 0
        _Sheen ("Sheen", Range(0, 1)) = 0.08
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        TEXTURE2D(_Overlay);
        SAMPLER(sampler_Overlay);

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _Color;
            half _Cutoff;
            half _BackShade;
            half4 _BackColor;
            half4 _EdgeColor;
            half _ShowThrough;
            half _Wrap;
            half _Translucency;
            half _Grain;
            float _Boil;
            float _BoilRate;
            float _Phase;
            half _Sheen;
            half _OverlayOn;
        CBUFFER_END

        float VhHash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

        float VhNoise(float2 p)
        {
            float2 i = floor(p), f = frac(p);
            float2 u = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(VhHash(i), VhHash(i + float2(1, 0)), u.x), lerp(VhHash(i + float2(0, 1)), VhHash(i + float2(1, 1)), u.x), u.y);
        }

        // The boil: a tiny, stepped wobble of the picture's UVs (hand-drawn animation's line boil).
        float2 VhBoil(float2 uv)
        {
            float frame = floor(_Time.y * _BoilRate + _Phase * 17.0);
            float2 o = float2(VhNoise(uv * 9.0 + frame * 3.1), VhNoise(uv * 9.0 + 41.7 + frame * 2.3)) - 0.5;
            return uv + o * _Boil;
        }

        // The alpha that decides the card's outline: the front and the back use the same (unboiled) test, so the faces
        // meet the cut edge exactly; the boil only moves the colour.
        half VhAlpha(float2 uv) { return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a; }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 part : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float part : TEXCOORD3;
                float fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.part = v.part.x;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half3 Diffuse(Light light, float3 n, float3 v, half3 albedo, half wrap)
            {
                half atten = light.shadowAttenuation * light.distanceAttenuation;
                half ndl = dot(n, light.direction);
                half d = saturate((ndl + wrap) / (1.0 + wrap));
                // Light through the paper: from behind the card, softened, warm.
                // About a third of the light on the far side comes through (the board is thick card, not tracing paper),
                // a touch warm from the fibres.
                half through = saturate(-ndl) * _Translucency * lerp(0.35, 1.0, light.shadowAttenuation) * light.distanceAttenuation;
                half3 h = normalize(light.direction + v);
                half spec = pow(saturate(dot(n, h)), 24.0) * _Sheen;
                return light.color * (albedo * d * atten + albedo * through * half3(1.0, 0.95, 0.88) + spec * atten);
            }

            half4 Frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float part = i.part;
                bool isEdge = part > 1.5;
                bool isBack = part > 0.5 && !isEdge;
                half alpha = isEdge ? 1.0 : VhAlpha(isBack ? i.uv : i.uv);
                clip(alpha - _Cutoff);

                float3 n = normalize(i.normalWS);
                // Plain quads (no part data) are two-sided: their back shows the picture, shaded.
                half frontFacing = IS_FRONT_VFACE(face, 1.0, 0.0);
                if (!isEdge) n = frontFacing > 0.5 ? n : -n;

                float2 buv = VhBoil(i.uv);
                half4 pic = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, buv);
                // A separate overlay printed over the picture (Lena's star watch over the round one she was drawn with).
                if (_OverlayOn > 0.5 && !isBack && !isEdge)
                {
                    half4 ov = SAMPLE_TEXTURE2D(_Overlay, sampler_Overlay, buv);
                    pic.rgb = lerp(pic.rgb, ov.rgb, ov.a);
                }
                half grain = (VhNoise(i.uv * float2(380.0, 380.0)) - 0.5) * 0.6 + (VhNoise(i.uv * float2(90.0, 1400.0)) - 0.5) * 0.4;
                half3 albedo;
                if (isEdge) albedo = _EdgeColor.rgb * (1.0 + grain * 0.6);
                else if (isBack) albedo = lerp(_BackColor.rgb, _BackColor.rgb * pic.rgb, _ShowThrough) * (1.0 + grain * _Grain * 10.0 * 0.5);
                else
                {
                    albedo = pic.rgb * _Color.rgb * (1.0 + grain * _Grain);
                    // A plain quad's back: the old two-sided shade.
                    if (part < 0.5 && frontFacing < 0.5) albedo *= _BackShade;
                }

                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 col = Diffuse(mainLight, n, v, albedo, _Wrap);

            #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = n;
                inputData.viewDirectionWS = v;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                half4 shadowMask = half4(1, 1, 1, 1);
                uint pixelLightCount = GetAdditionalLightsCount();
                #if USE_CLUSTER_LIGHT_LOOP
                [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    Light dl = GetAdditionalLight(lightIndex, i.positionWS, shadowMask);
                    col += Diffuse(dl, n, v, albedo, _Wrap);
                }
                #endif
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light al = GetAdditionalLight(lightIndex, i.positionWS, shadowMask);
                    col += Diffuse(al, n, v, albedo, _Wrap);
                LIGHT_LOOP_END
            #endif

                half3 ambient = SampleSH(n);
            #if defined(_SCREEN_SPACE_OCCLUSION)
                AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.positionCS));
                ambient *= ao.indirectAmbientOcclusion;
                col *= ao.directAmbientOcclusion;
            #endif
                col += albedo * ambient;
                col = MixFog(col, i.fogFactor);
                return half4(col, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 part : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float part : TEXCOORD1;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                float3 nws = TransformObjectToWorldNormal(v.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 l = normalize(_LightPosition - ws);
            #else
                float3 l = _LightDirection;
            #endif
                o.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(ws, nws, l)));
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.part = v.part.x;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                if (i.part < 1.5) clip(VhAlpha(i.uv) - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 part : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float part : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.part = v.part.x;
                return o;
            }

            half Frag(Varyings i) : SV_Target
            {
                if (i.part < 1.5) clip(VhAlpha(i.uv) - _Cutoff);
                return i.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 part : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float part : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.part = v.part.x;
                return o;
            }

            half4 Frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                if (i.part < 1.5) clip(VhAlpha(i.uv) - _Cutoff);
                float3 n = NormalizeNormalPerPixel(i.normalWS);
                if (i.part < 1.5) n = IS_FRONT_VFACE(face, 1.0, 0.0) > 0.5 ? n : -n;
                return half4(n, 0.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}

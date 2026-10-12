// Visiting Hours' stylized foliage (the Look library's trees and hedges: VhLookTrees). URP-standard HLSL.
// Leaf-cluster cards, alpha-tested, double-sided. The mesh carries canopy normals (pointing out from the crown's centre),
// so a whole crown shades as one soft volume, the stylized-AAA way, instead of as hundreds of flat cards.
// Lighting: the main light with wrap diffuse and its shadows (cascades, soft), sun translucency (the crown glows gold
// when the sun is behind it, most at the crown's rim, where light passes through one or two leaves), ambient from the
// probes/sky (SH), SSAO, fog. Vertex colour: rgb = per-cluster tint, a = sway weight (0 at the trunk, 1 at the crown's
// edge) for a gentle wind. UV1.x = rim (0 deep inside the crown, 1 on its outer shell): translucency and a painted
// occlusion. LOD cross-fade (dithered) so the trees' LODGroups don't pop. Also used for grass and flower cards.
// Passes: UniversalForward, ShadowCaster, DepthOnly, DepthNormals (for SSAO and the depth prepass).
// Pass 3: _LightTint (the material sets a slightly green-cool tint on the direct sun so summer leaves don't turn
// autumn-gold under the warm key light; the grade still warms them) and a yellow-green translucency (pass 2's gold
// translucency was half of round 2's "autumn" crowns).
Shader "JamKit/Look/Foliage"
{
    Properties
    {
        [MainTexture] _BaseMap ("Leaf atlas (RGBA)", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.45
        _Wrap ("Wrap diffuse", Range(0, 1)) = 0.45
        _ShadowFloor ("Light kept in self-shadow", Range(0, 1)) = 0.12
        _TranslucencyColor ("Translucency colour", Color) = (0.72, 0.95, 0.34, 1)
        _LightTint ("Direct light tint (keeps leaves summer-green under the warm sun)", Color) = (1, 1, 1, 1)
        _Translucency ("Translucency", Range(0, 4)) = 1.9
        _TranslucencyFocus ("Translucency focus", Range(1, 16)) = 3
        _RimOcclusion ("Darkening deep inside the crown", Range(0, 1)) = 0.35
        _AmbientBoost ("Ambient boost", Range(0, 2)) = 1.1
        _WindStrength ("Wind strength (m)", Range(0, 0.3)) = 0.05
        _WindSpeed ("Wind speed", Range(0, 4)) = 1.2
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        float4 _BaseColor;
        float _Cutoff;
        float _Wrap;
        float _ShadowFloor;
        float4 _TranslucencyColor;
        float4 _LightTint;
        float _Translucency;
        float _TranslucencyFocus;
        float _RimOcclusion;
        float _AmbientBoost;
        float _WindStrength;
        float _WindSpeed;
        CBUFFER_END

        float3 VhWind(float3 positionWS, float weight)
        {
            float t = _Time.y * _WindSpeed;
            float phase = dot(positionWS, float3(0.37, 0.11, 0.29));
            float3 sway = float3(sin(t + phase), sin(t * 1.7 + phase * 1.3) * 0.35, cos(t * 0.8 + phase * 0.7));
            return positionWS + sway * _WindStrength * weight;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

            // Pass 4: a sky/horizon/ground ambient from the rig's globals (_VhAmbientSky etc., linear; a = 1 when set). The
            // project uses Adaptive Probe Volumes, which this shader doesn't sample, and the per-object SH it falls back to came
            // out black (round 3: "sunward far hills pitch black"). Hemispherical, so shade reads cool from the sky above.
            float4 _VhAmbientSky;
            float4 _VhAmbientEquator;
            float4 _VhAmbientGround;
            float3 VhAmbient(float3 n)
            {
                if (_VhAmbientSky.a < 0.5) return SampleSH(n);
                float up = n.y;
                float3 top = lerp(_VhAmbientEquator.rgb, _VhAmbientSky.rgb, saturate(up));
                float3 bottom = lerp(_VhAmbientEquator.rgb, _VhAmbientGround.rgb, saturate(-up));
                return up >= 0.0 ? top : bottom;
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float rim : TEXCOORD5;
                float3 normalWS : TEXCOORD2;
                float4 color : TEXCOORD3;
                float fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 ws = VhWind(TransformObjectToWorld(v.positionOS.xyz), v.color.a);
                o.positionWS = ws;
                o.positionCS = TransformWorldToHClip(ws);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.color = v.color;
                o.rim = v.uv1.x;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                clip(tex.a - _Cutoff);
            #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(i.positionCS);
            #endif
                float rim = saturate(i.rim);
                float3 albedo = tex.rgb * _BaseColor.rgb * i.color.rgb * lerp(1.0 - _RimOcclusion, 1.0, rim);
                float3 n = normalize(i.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light light = GetMainLight(shadowCoord);
                float atten = light.shadowAttenuation * light.distanceAttenuation;
                float ndl = dot(n, light.direction);
                float wrap = saturate((ndl + _Wrap) / (1.0 + _Wrap));
                float lit = lerp(_ShadowFloor, 1.0, atten);
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                float back = pow(saturate(dot(v, -light.direction)), _TranslucencyFocus);
                // Light through the leaves: strongest looking into the sun, at the crown's rim (one or two leaves thick), and
                // not fully killed by the crown's own shadow (that light is what passes through it).
                float3 lightCol = light.color * _LightTint.rgb;
                float3 trans = _TranslucencyColor.rgb * lightCol * _Translucency *
                               (back * 0.9 + 0.12 * saturate(-ndl)) * lerp(0.3, 1.0, rim) * lerp(0.45, 1.0, atten);
                float3 direct = lightCol * wrap * lit;
                float3 ambient = VhAmbient(n) * _AmbientBoost;
            #if defined(_SCREEN_SPACE_OCCLUSION)
                AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.positionCS));
                ambient *= ao.indirectAmbientOcclusion;
                direct *= ao.directAmbientOcclusion;
            #endif
                float3 col = albedo * (direct + ambient) + albedo * trans;
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
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 ws = VhWind(TransformObjectToWorld(v.positionOS.xyz), v.color.a);
                float3 nws = TransformObjectToWorldNormal(v.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 l = normalize(_LightPosition - ws);
            #else
                float3 l = _LightDirection;
            #endif
                o.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(ws, nws, l)));
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff);
            #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(i.positionCS);
            #endif
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
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformWorldToHClip(VhWind(TransformObjectToWorld(v.positionOS.xyz), v.color.a));
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half frag(Varyings i) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff);
            #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(i.positionCS);
            #endif
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
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformWorldToHClip(VhWind(TransformObjectToWorld(v.positionOS.xyz), v.color.a));
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff);
            #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(i.positionCS);
            #endif
                return half4(NormalizeNormalPerPixel(i.normalWS), 0.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}

// The dream's look on real meshes (furniture from Kenney's Furniture Kit in Night 2's house): the same flat pastel
// colour, three soft sun steps, lilac shade, crayon grain, shadow, void sink and fog as JamKit/Dream/Pastel, but the
// doodle outline is an inverted hull (the mesh pushed out along its normals and drawn back-faces only, in the ink
// colour), because Pastel's outline is worked out from the edges of Unity's cube and quad. Where a mesh has split
// normals the hull breaks at the corners, which reads as a sketchy, lifted pencil line.
Shader "JamKit/Dream/PastelMesh"
{
    Properties
    {
        _Color ("Colour", Color) = (0.98, 0.9, 0.62, 1)
        _ShadeColor ("Shade tint", Color) = (0.66, 0.62, 0.9, 1)
        _OutlineColor ("Outline", Color) = (0.38, 0.29, 0.47, 1)
        _OutlineWidth ("Outline width (m)", Range(0, 0.1)) = 0.012
        _Crayon ("Crayon grain", Range(0, 0.3)) = 0.07
        _SunDir ("Direction to the sun", Vector) = (-0.35, 0.42, 0.84, 0)
        _VoidColor ("Void colour", Color) = (0.62, 0.58, 0.88, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half4 _ShadeColor;
            half4 _OutlineColor;
            float _OutlineWidth;
            half _Crayon;
            float4 _SunDir;
            half4 _VoidColor;
        CBUFFER_END

        float Hash(float3 p)
        {
            p = frac(p * 0.3183099 + 0.1);
            p *= 17.0;
            return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
        }

        float Noise(float3 x)
        {
            float3 i = floor(x);
            float3 f = frac(x);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x), lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                        lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x), lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
        }
        ENDHLSL

        Pass
        {
            Name "DreamPastelMesh"
            Tags { "LightMode" = "UniversalForward" }   // the outline pass is SRPDefaultUnlit: URP draws both
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma target 3.0

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fog : TEXCOORD2;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 L = normalize(_SunDir.xyz);
                float l = saturate(dot(n, L) * 0.5 + 0.5);
                float steps = 3.0;
                float band = l * steps;
                float lq = (floor(band) + smoothstep(0.42, 0.58, frac(band))) / steps;
                half3 baseCol = _Color.rgb;
                half3 shade = baseCol * _ShadeColor.rgb;
                half3 col = lerp(shade, baseCol * half3(1.04, 1.0, 0.94), lq);
                col += half3(0.05, 0.035, 0.0) * saturate(n.y);
                half shadow = MainLightRealtimeShadow(TransformWorldToShadowCoord(i.positionWS));
                col = lerp(shade * 0.92, col, shadow);

                float grain = Noise(i.positionWS * float3(5.1, 9.3, 5.7) + float3(i.positionWS.y * 3.0, 0, 0));
                float streak = Noise(float3((i.positionWS.x + i.positionWS.z) * 2.3, i.positionWS.y * 14.0, 0.5));
                col *= 1.0 + (grain * 0.6 + streak * 0.4 - 0.5) * _Crayon * 2.0;

                float sink = saturate((-i.positionWS.y - 3.0) / 14.0);
                col = lerp(col, _VoidColor.rgb, sink * 0.85);
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DreamPastelMeshOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float fog : TEXCOORD0; float3 positionWS : TEXCOORD1; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 wp = TransformObjectToWorld(v.positionOS.xyz);
                float3 wn = TransformObjectToWorldNormal(v.normalOS);
                float wobble = 0.7 + 0.6 * Noise(wp * 1.7);
                wp += normalize(wn) * _OutlineWidth * wobble;
                o.positionWS = wp;
                o.positionCS = TransformWorldToHClip(wp);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half3 col = _OutlineColor.rgb;
                float sink = saturate((-i.positionWS.y - 3.0) / 14.0);
                col = lerp(col, _VoidColor.rgb, sink * 0.85);
                return half4(MixFog(col, i.fog), 1);
            }
            ENDHLSL
        }

        // Depth passes (the Look agent, pass 3, from the Perf agent's list): without them these surfaces were missing from
        // URP's depth texture, so the haze and SSAO treated them as sky, and depth priming couldn't be turned on.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct DepthAttributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct DepthVaryings { float4 positionCS : SV_POSITION; };
            DepthVaryings DepthVert(DepthAttributes v)
            {
                DepthVaryings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }
            half DepthFrag(DepthVaryings i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex NormalsVert
            #pragma fragment NormalsFrag
            #pragma multi_compile_instancing

            struct NormalsAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct NormalsVaryings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            NormalsVaryings NormalsVert(NormalsAttributes v)
            {
                NormalsVaryings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 NormalsFrag(NormalsVaryings i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS), 0.0); }
            ENDHLSL
        }
    }
}

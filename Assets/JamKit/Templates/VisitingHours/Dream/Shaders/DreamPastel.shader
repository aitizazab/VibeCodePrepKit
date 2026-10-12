// The dream street's surfaces (Visiting Hours, art style E: "cutesy pastel doodle with dread underneath").
// A flat pastel colour lit in three soft steps by a low golden-hour sun, a cool lilac shade, a crayon grain,
// and a wobbly doodle outline drawn along the edges of each box (from the object-space position of Unity's
// cube or quad, so a scaled box keeps 5 cm lines). Below the street the colour sinks into the lilac void.
// Unlit (its own sun), opaque, URP fog; it receives the sun's shadow map, so Nadia (the only thing that casts one)
// leaves a lilac shadow on the street. _Shape 1 is the gable prism (ridge and gable lines). An optional pattern
// (_MainTex, projected from world space, _TexStrength 0 by default) adds wallpaper or floorboards as brightness.
Shader "JamKit/Dream/Pastel"
{
    Properties
    {
        _Color ("Colour", Color) = (0.98, 0.9, 0.62, 1)
        _ShadeColor ("Shade tint", Color) = (0.66, 0.62, 0.9, 1)
        _OutlineColor ("Outline", Color) = (0.38, 0.29, 0.47, 1)
        _OutlineWidth ("Outline width (m)", Range(0, 0.2)) = 0.05
        _Crayon ("Crayon grain", Range(0, 0.3)) = 0.07
        _Emission ("Glow (ignores the light)", Range(0, 1)) = 0
        _SunDir ("Direction to the sun", Vector) = (-0.35, 0.42, 0.84, 0)
        _VoidColor ("Void colour", Color) = (0.62, 0.58, 0.88, 1)
        _Shape ("Shape (0 box or quad, 1 gable prism)", Float) = 0
        _MainTex ("Pattern (wallpaper, floorboards; world-projected)", 2D) = "white" {}
        _TexScale ("Pattern tile size (m)", Float) = 1
        _TexMean ("Pattern mean brightness", Float) = 1
        _TexStrength ("Pattern strength (0 = plain colour)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "DreamPastel"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _ShadeColor;
                half4 _OutlineColor;
                float _OutlineWidth;
                half _Crayon;
                half _Emission;
                float4 _SunDir;
                half4 _VoidColor;
                float _Shape;
                float4 _MainTex_ST;
                float _TexScale;
                float _TexMean;
                half _TexStrength;
            CBUFFER_END
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                float3 normalOS : TEXCOORD3;
                float fog : TEXCOORD4;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionOS = v.positionOS.xyz;
                o.normalOS = v.normalOS;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

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

            half4 Frag(Varyings i) : SV_Target
            {
                float3 scale = float3(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21), length(UNITY_MATRIX_M._m02_m12_m22));
                float3 p = i.positionOS;
                float3 an = abs(normalize(i.normalOS));
                float2 d;
                if (an.x > 0.6) d = (0.5 - abs(p.yz)) * scale.yz;
                else if (an.y > 0.6) d = (0.5 - abs(p.xz)) * scale.xz;
                else d = (0.5 - abs(p.xy)) * scale.xy;
                float edge = min(d.x, d.y);
                if (_Shape > 0.5)
                {
                    // Gable prism (y -0.5..0.5, ridge along z): the ridge line and the slanted gable edges.
                    if (an.z > 0.6) edge = min((0.5 - p.y) * scale.y, (0.5 * (0.5 - p.y) - abs(p.x)) * scale.x);
                    else if (an.x < 0.99 && an.y > 0.2) edge = min(edge, (0.5 - p.y) * scale.y * 0.7);
                }

                float3 n = normalize(i.normalWS);
                float3 L = normalize(_SunDir.xyz);
                float l = saturate(dot(n, L) * 0.5 + 0.5);
                float steps = 3.0;
                float band = l * steps;
                float lq = (floor(band) + smoothstep(0.42, 0.58, frac(band))) / steps;
                half3 baseCol = _Color.rgb;
                if (_TexStrength > 0.001)
                {
                    // A pattern projected from world space along the face's main axis (boxes of any size keep the
                    // same tile size), as a brightness variation on the pastel colour, so the palette stays.
                    float3 wn = abs(normalize(i.normalWS));
                    float2 uv = wn.y > 0.6 ? i.positionWS.xz : (wn.x > 0.6 ? i.positionWS.zy : i.positionWS.xy);
                    half3 pat = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv / max(_TexScale, 0.01)).rgb;
                    baseCol *= lerp(half3(1, 1, 1), pat / max(_TexMean, 0.05), _TexStrength);
                }
                half3 shade = baseCol * _ShadeColor.rgb;
                half3 col = lerp(shade, baseCol * half3(1.04, 1.0, 0.94), lq);
                col += half3(0.05, 0.035, 0.0) * saturate(n.y);                  // warm sky on top faces
                // Nadia's shadow: towards the lilac shade where the sun's shadow map says she blocks it.
                half shadow = MainLightRealtimeShadow(TransformWorldToShadowCoord(i.positionWS));
                col = lerp(shade * 0.92, col, lerp(shadow, 1.0h, _Emission));

                float grain = Noise(i.positionWS * float3(5.1, 9.3, 5.7) + float3(i.positionWS.y * 3.0, 0, 0));
                float streak = Noise(float3((i.positionWS.x + i.positionWS.z) * 2.3, i.positionWS.y * 14.0, 0.5));
                col *= 1.0 + (grain * 0.6 + streak * 0.4 - 0.5) * _Crayon * 2.0;

                col = lerp(col, baseCol, _Emission) + baseCol * _Emission * 0.25;

                float wobble = 0.7 + 0.6 * Noise(i.positionWS * 1.7);
                float w = _OutlineWidth * wobble;
                float aa = fwidth(edge) * 1.2;
                float ink = 1.0 - smoothstep(w - aa, w + aa, edge);
                col = lerp(col, _OutlineColor.rgb, ink * step(0.001, _OutlineWidth));

                float sink = saturate((-i.positionWS.y - 3.0) / 14.0);
                col = lerp(col, _VoidColor.rgb, sink * 0.85);
                col = MixFog(col, i.fog);
                return half4(col, 1);
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _ShadeColor;
                half4 _OutlineColor;
                float _OutlineWidth;
                half _Crayon;
                half _Emission;
                float4 _SunDir;
                half4 _VoidColor;
                float _Shape;
                float4 _MainTex_ST;
                float _TexScale;
                float _TexMean;
                half _TexStrength;
            CBUFFER_END
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _ShadeColor;
                half4 _OutlineColor;
                float _OutlineWidth;
                half _Crayon;
                half _Emission;
                float4 _SunDir;
                half4 _VoidColor;
                float _Shape;
                float4 _MainTex_ST;
                float _TexScale;
                float _TexMean;
                half _TexStrength;
            CBUFFER_END
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

// Visiting Hours' far scenery (VhLookBackdrop: the terrain ring's fields, its woods and hedgerows). URP-standard HLSL.
// Cheap on purpose (seen at 300 m - 3 km through haze): one texture, wrap-diffuse main light without shadows, ambient from
// the probes/sky (SH), fog. No specular, no normal map: pass 2's far ground was URP Simple Lit; this adds what Simple Lit
// can't do, the vertex colour: rgb multiplies the albedo (darker under hedges, lighter crowns), alpha blends from
// _NearColor (the scene's own lawn, at the ring's inner edge: round 2's "hard seam where the terrain meets the backdrop")
// to the texture.
// Passes: UniversalForward, DepthOnly, DepthNormals (so the haze and SSAO see it). Casts no shadows.
Shader "JamKit/Look/FarLand"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _NearColor ("Colour at the scene's edge (vertex alpha 0)", Color) = (0.36, 0.46, 0.2, 1)
        _Wrap ("Wrap diffuse", Range(0, 1)) = 0.3
        _AmbientBoost ("Ambient boost", Range(0, 2)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        float4 _BaseColor;
        float4 _NearColor;
        float _Wrap;
        float _AmbientBoost;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 color : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.color = v.color;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                float3 albedo = lerp(_NearColor.rgb, tex, saturate(i.color.a)) * i.color.rgb;
                float3 n = normalize(i.normalWS);
                Light light = GetMainLight();
                float ndl = dot(n, light.direction);
                float wrap = saturate((ndl + _Wrap) / (1.0 + _Wrap));
                float3 col = albedo * (light.color * wrap + VhAmbient(n) * _AmbientBoost);
                col = MixFog(col, i.fogFactor);
                return half4(col, 1.0);
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

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half frag(Varyings i) : SV_Target { return i.positionCS.z; }
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

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS), 0.0); }
            ENDHLSL
        }
    }
    Fallback Off
}

// The dream's sky dome: a too-perfect golden hour. Lilac overhead, a peach band at the horizon, a big soft
// sun ahead (where the exit is), a few crayon-stroke clouds; below the horizon the lilac void the street hangs
// in, with slow doodle stars. Drawn on the inside of a sphere that follows the camera (no depth, no fog).
Shader "JamKit/Dream/Sky"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.7, 0.68, 0.95, 1)
        _HorizonColor ("Horizon", Color) = (1, 0.8, 0.66, 1)
        _SunColor ("Sun", Color) = (1, 0.93, 0.72, 1)
        _VoidColor ("Void", Color) = (0.56, 0.52, 0.86, 1)
        _DeepColor ("Deep void", Color) = (0.36, 0.32, 0.62, 1)
        _SunDir ("Direction to the sun", Vector) = (-0.35, 0.42, 0.84, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Background" "Queue" = "Background" "RenderPipeline" = "UniversalPipeline" "PreviewType" = "Skybox" }

        Pass
        {
            Name "DreamSky"
            Cull Front
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _HorizonColor;
                half4 _SunColor;
                half4 _VoidColor;
                half4 _DeepColor;
                float4 _SunDir;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 x)
            {
                float2 i = floor(x), f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float3 sun = normalize(_SunDir.xyz);
                float y = d.y;
                half3 col;
                if (y >= 0.0)
                {
                    col = lerp(_HorizonColor.rgb, _TopColor.rgb, smoothstep(0.0, 0.55, y));
                    // Crayon clouds: soft horizontal strokes.
                    float2 uv = d.xz / max(0.15, y + 0.15);
                    float c = Noise(uv * float2(0.9, 2.6) + _Time.y * float2(0.004, 0.0)) * Noise(uv * float2(2.1, 5.3));
                    col = lerp(col, half3(1.0, 0.95, 0.96), smoothstep(0.32, 0.5, c) * 0.55 * smoothstep(0.02, 0.2, y));
                }
                else
                {
                    col = lerp(_VoidColor.rgb, _DeepColor.rgb, smoothstep(0.0, 0.7, -y));
                    float2 uv = d.xz / max(0.1, -y) * 6.0;
                    float2 cell = floor(uv);
                    float h = Hash(cell);
                    float2 f = frac(uv) - 0.5;
                    float twinkle = 0.6 + 0.4 * sin(_Time.y * (1.0 + h * 2.0) + h * 30.0);
                    float star = step(0.93, h) * smoothstep(0.12, 0.0, length(f)) * twinkle;
                    col += half3(1.0, 0.95, 0.8) * star * 0.6;
                    col = lerp(col, _HorizonColor.rgb, smoothstep(-0.12, 0.0, y) * 0.7);
                }
                float s = saturate(dot(d, sun));
                col += _SunColor.rgb * (pow(s, 600.0) * 1.2 + pow(s, 24.0) * 0.35 + pow(s, 4.0) * 0.12);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}

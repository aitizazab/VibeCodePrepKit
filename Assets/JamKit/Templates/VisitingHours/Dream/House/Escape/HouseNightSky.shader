// The escape's attic, open to the stars (House agent, round 2): a night sky on a big inverted dome round the attic (its
// own frame, not the camera's): a deep blue gradient to a violet horizon, a Milky Way band with dust lanes, three layers of
// hashed stars (a few bright ones with a soft halo). No textures. ZWrite Off and no depth passes, drawn in the transparent
// queue's front (after URP's skybox), so the house's golden-hour sky elsewhere is untouched and anything nearer hides it.
Shader "JamKit/House/NightSky"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (0.015, 0.022, 0.07, 1)
        _Horizon ("Horizon", Color) = (0.12, 0.1, 0.26, 1)
        _Below ("Below the horizon", Color) = (0.03, 0.03, 0.08, 1)
        _StarDensity ("Star density", Float) = 120
        _StarBrightness ("Star brightness", Float) = 4
        _BandColor ("Milky Way", Color) = (0.42, 0.4, 0.62, 1)
        _BandStrength ("Milky Way strength", Float) = 0.55
        _BandPole ("Milky Way pole (object space)", Vector) = (0.42, 0.62, 0.66, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-400" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "NightSky"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            ZTest LEqual
            Cull Front
            Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Zenith;
                half4 _Horizon;
                half4 _Below;
                half4 _BandColor;
                float _StarDensity;
                float _StarBrightness;
                float _BandStrength;
                float4 _BandPole;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dirOS : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.dirOS = input.positionOS.xyz;
                return o;
            }

            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float3 Hash33(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.xxy + p.yxx) * p.zyx);
            }

            float ValueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash13(i), b = Hash13(i + float3(1, 0, 0)), c = Hash13(i + float3(0, 1, 0)), d = Hash13(i + float3(1, 1, 0));
                float e = Hash13(i + float3(0, 0, 1)), g = Hash13(i + float3(1, 0, 1)), h = Hash13(i + float3(0, 1, 1)), k = Hash13(i + float3(1, 1, 1));
                float x0 = lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
                float x1 = lerp(lerp(e, g, f.x), lerp(h, k, f.x), f.y);
                return lerp(x0, x1, f.z);
            }

            // One layer of stars: a point jittered in each cell of a grid of directions; most cells empty.
            float Stars(float3 d, float density, float keep, float size)
            {
                float3 p = d * density;
                float3 c = floor(p);
                float h = Hash13(c);
                float3 star = c + 0.2 + Hash33(c) * 0.6;
                float dist = length(p - star);
                float s = saturate(1.0 - dist / size);
                float lit = step(keep, h);
                return lit * s * s * (0.35 + 1.6 * frac(h * 91.7));
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dirOS);
                float up = d.y;
                float3 col = lerp(_Horizon.rgb, _Zenith.rgb, saturate(pow(saturate(up), 0.6)));
                col = lerp(_Below.rgb, col, saturate(up * 8.0 + 1.0));

                // The Milky Way: a band round a great circle, broken up by dust lanes.
                float3 pole = normalize(_BandPole.xyz);
                float b = dot(d, pole);
                float band = exp(-b * b * 14.0);
                float n = ValueNoise(d * 7.0) * 0.6 + ValueNoise(d * 19.0) * 0.4;
                float lanes = saturate(1.0 - exp(-b * b * 220.0) * smoothstep(0.35, 0.75, ValueNoise(d * 11.0 + 4.0)));
                col += _BandColor.rgb * band * _BandStrength * (0.45 + 0.55 * n) * lanes * saturate(up * 3.0 + 0.4);

                // Stars: dim and many, fewer and brighter, a few bright ones with a halo; more of them in the band.
                float s = Stars(d, _StarDensity * 2.2, 0.82 - band * 0.12, 0.22) * 0.5;
                s += Stars(d + 3.1, _StarDensity, 0.9, 0.2);
                float bright = Stars(d + 7.7, _StarDensity * 0.45, 0.965, 0.16);
                s += bright * 2.0;
                float halo = Stars(d + 7.7, _StarDensity * 0.45, 0.965, 0.6);
                s *= saturate(up * 6.0 + 0.3);
                col += s * _StarBrightness * float3(1.0, 0.95, 0.86);
                col += halo * 0.12 * float3(0.75, 0.8, 1.0) * saturate(up * 6.0 + 0.3);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}

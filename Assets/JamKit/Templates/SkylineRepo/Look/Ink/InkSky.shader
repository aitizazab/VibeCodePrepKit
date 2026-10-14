// Skyline's painted sky (docs/SKYLINE-STYLE.md rule 1): a gradient from the zenith to the horizon, a drawn sun disc with a
// soft ring, and simple posterised cloud shapes in two tones from value noise, drifting slowly. No photograph.
// URP-standard HLSL (Core.hlsl); a skybox material (Queue Background). The sun direction is Visiting Hours' rig global
// _VhSunDiscDir (VhLookRig.Apply sets it, as for the HDRI sky) and the drift clock is _VhSkyTime, so the disc sits exactly
// on the scene's light and the clouds creep with the rig's cloudDrift.
// Colour properties are sRGB, as everywhere in this project (Unity converts them).
Shader "Skyline/InkSky"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (0.3686, 0.6078, 0.8784, 1)           // #5E9BE0
        _Horizon ("Horizon", Color) = (0.8118, 0.8784, 0.9333, 1)          // #CFE0EE
        _Below ("Below the horizon", Color) = (0.7255, 0.7020, 0.6588, 1)  // #B9B3A8
        _CloudLit ("Cloud, lit side", Color) = (1, 0.99, 0.96, 1)
        _CloudShade ("Cloud, shade side", Color) = (0.79, 0.84, 0.91, 1)
        _SunColor ("Sun disc", Color) = (3.2, 2.9, 2.2, 1)                 // HDR: the bloom and the lens flare catch it
        _RingColor ("Sun ring", Color) = (1, 0.95, 0.82, 1)
        _SunRadius ("Sun disc radius (deg)", Range(0.5, 6)) = 2.4
        _CloudScale ("Cloud scale", Float) = 1.6
        _CloudCover ("Cloud cover (higher = fewer)", Range(0.3, 0.8)) = 0.56
        _DriftSpeed ("Cloud drift (uv per second)", Float) = 0.004
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off

        Pass
        {
            Name "InkSky"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Zenith;
            float4 _Horizon;
            float4 _Below;
            float4 _CloudLit;
            float4 _CloudShade;
            float4 _SunColor;
            float4 _RingColor;
            float _SunRadius;
            float _CloudScale;
            float _CloudCover;
            float _DriftSpeed;
            CBUFFER_END

            float4 _VhSunDiscDir;   // world direction of the disc (the rig sets it); zero before a rig exists
            float _VhSkyTime;       // seconds x the rig's cloud drift

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
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

            // Value noise on a grid, smooth between cells.
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            // Two octaves: big shapes, a little breakup on their edges.
            float Cloud(float2 p)
            {
                return Noise(p) * 0.65 + Noise(p * 2.3 + 7.1) * 0.35;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                // the gradient: zenith to horizon by elevation (a square-root ease keeps the blue high), the ground band below
                float up = saturate(d.y);
                float3 sky = lerp(_Horizon.rgb, _Zenith.rgb, pow(up, 0.55));
                float below = saturate(-d.y * 6.0);
                sky = lerp(sky, _Below.rgb, below);

                float3 sunDir = _VhSunDiscDir.xyz;
                bool hasSun = dot(sunDir, sunDir) > 0.25;
                sunDir = hasSun ? normalize(sunDir) : float3(0.4, 0.6, 0.7);

                // simple cloud shapes: the sky above projected onto a plane, two tones, posterised (hard, anti-aliased edges)
                float fadeIn = smoothstep(0.02, 0.2, d.y);
                if (fadeIn > 0.001)
                {
                    float2 uv = d.xz / (d.y + 0.22) * _CloudScale + float2(_VhSkyTime * _DriftSpeed, _VhSkyTime * _DriftSpeed * 0.3);
                    float2 toSun = normalize(sunDir.xz + 1e-5) * 0.09;
                    float n0 = Cloud(uv);
                    float n1 = Cloud(uv + toSun);               // the same sky a step towards the sun
                    float w = max(fwidth(n0) * 1.5, 0.004);
                    float body = smoothstep(_CloudCover - w, _CloudCover + w, n0);
                    float lit = smoothstep(_CloudCover - w, _CloudCover + w, n1);
                    float3 cloud = lerp(_CloudShade.rgb, _CloudLit.rgb, lit);
                    sky = lerp(sky, cloud, body * fadeIn * 0.95);
                }

                // the drawn sun: a hard disc (HDR so it glows), a soft ring round it
                float ang = acos(clamp(dot(d, sunDir), -1.0, 1.0));
                float r = radians(_SunRadius);
                float aa = max(fwidth(ang) * 1.2, 0.0006);
                float ring = smoothstep(r * 2.1 + aa, r * 2.1 - aa, ang) - smoothstep(r * 1.55 + aa, r * 1.55 - aa, ang);
                float halo = smoothstep(r * 5.0, r * 1.2, ang);
                sky = lerp(sky, _RingColor.rgb, halo * 0.18 + ring * 0.35);
                float disc = smoothstep(r + aa, r - aa, ang);
                sky = lerp(sky, _SunColor.rgb, disc);
                return half4(sky, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

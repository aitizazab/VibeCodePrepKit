// Visiting Hours' golden-hour sky (VhLookRig drives its globals). URP-standard HLSL (Core.hlsl).
// - The HDRI (equirectangular, its sun clamped out by tools/look/hdri-prep.py), turned (and tilted a few degrees) so its
//   sun sits exactly on the rig's light: the disc, the HDRI's own glow and rays, the lens flare and the sun shafts all
//   line up (pass 1 drew the disc 3 degrees below the light, so the flare and shafts came from empty sky).
// - A slow cloud drift: two copies of the upper sky, offset along the bearing and cross-faded (a flow-map style loop), so
//   the clouds creep without ever sliding off. Faded out round the sun and at the horizon.
// - The sun disc (limb-darkened, HDR, so bloom and the lens flare catch it) and a warm glow round it.
// - A light warm lift on the sun's side of the sky, a narrow haze band on the horizon in the atmosphere's own haze colours
//   (so far hills hazed by the atmosphere pass dissolve into the same colour), and a soft ground colour below it (the
//   HDRI's lower half is a blurred reflection, not ground). Pass 2 cut the lift and the band ~40% so the HDRI's gold-lit
//   clouds and rays show on the sun's side (round 1: "a flat cream wash over 60% of the frame").
// Globals: _VhSkyMatrix (world dir -> the HDRI's frame), _VhSunDiscDir (world dir of the disc = the light), _VhSunDir,
// _VhSunColor, _VhSkyTime (s), _VhHorizonAway / _VhHorizonSun / _VhHorizonFocus (the haze colours; set by the atmosphere).
Shader "JamKit/Look/Sky"
{
    Properties
    {
        [NoScaleOffset] _SkyTex ("Sky (equirect HDR)", 2D) = "grey" {}
        _Exposure ("Exposure", Float) = 1
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Saturation ("Saturation", Range(0, 2)) = 1.05
        _WarmSide ("Warm lift on the sun's side", Color) = (1.06, 0.99, 0.92, 1)
        _WarmFocus ("Warm lift focus", Range(0.5, 16)) = 4
        _SunSideDim ("Sun-side sky dimming (pass 3: lets the lit clouds read)", Range(0.5, 1)) = 1
        _HorizonColor ("Horizon haze colour (when the atmosphere sets none)", Color) = (1.05, 0.9, 0.78, 1)
        _HorizonHaze ("Horizon haze amount", Range(0, 1)) = 0.22
        _HorizonHeight ("Horizon haze height (sin el)", Range(0.005, 0.3)) = 0.035
        _GroundColor ("Ground colour (below the horizon)", Color) = (0.42, 0.42, 0.44, 1)
        _SunDiscColor ("Sun disc colour", Color) = (1, 0.82, 0.55, 1)
        _SunDiscIntensity ("Sun disc intensity", Float) = 110
        _SunDiscSize ("Sun disc radius (deg)", Range(0.1, 3)) = 0.6
        _SunGlowColor ("Sun glow colour", Color) = (1, 0.74, 0.44, 1)
        _SunGlow ("Sun glow strength", Float) = 2.8
        _SunGlowFalloff ("Sun glow falloff", Range(2, 4000)) = 320
        _SunHaloFalloff ("Sun wide halo falloff", Range(1, 64)) = 12
        _SunHalo ("Sun wide halo strength", Float) = 0.18
        _DriftDegrees ("Cloud drift per loop (deg)", Range(0, 6)) = 1.6
        _DriftPeriod ("Cloud drift loop (s)", Float) = 70
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_SkyTex);
            SAMPLER(sampler_SkyTex);

            CBUFFER_START(UnityPerMaterial)
            float _Exposure;
            float4 _Tint;
            float _Saturation;
            float4 _WarmSide;
            float _WarmFocus;
            float _SunSideDim;
            float4 _HorizonColor;
            float _HorizonHaze;
            float _HorizonHeight;
            float4 _GroundColor;
            float4 _SunDiscColor;
            float _SunDiscIntensity;
            float _SunDiscSize;
            float4 _SunGlowColor;
            float _SunGlow;
            float _SunGlowFalloff;
            float _SunHaloFalloff;
            float _SunHalo;
            float _DriftDegrees;
            float _DriftPeriod;
            CBUFFER_END

            float4 _VhSunDiscDir;
            float4 _VhSunDir;
            float4 _VhSunColor;
            float4x4 _VhSkyMatrix;
            float _VhSkyTime;
            float4 _VhHorizonAway;    // rgb, a = 1 when set
            float4 _VhHorizonSun;
            float _VhHorizonFocus;

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

            // World direction -> the HDRI's equirect uv: into the HDRI's own frame (_VhSkyMatrix: a yaw and a small tilt that
            // put its sun on the light), then an extra yaw for the cloud drift.
            float2 SkyUV(float3 d, float extraYawDeg)
            {
                float3 r = mul((float3x3)_VhSkyMatrix, d);
                if (dot(r, r) < 0.25) r = d;   // no rig yet: the HDRI as authored
                float a = radians(-extraYawDeg);
                float s = sin(a), c = cos(a);
                r = float3(r.x * c + r.z * s, r.y, -r.x * s + r.z * c);
                float u = atan2(r.x, r.z) / (2.0 * PI) + 0.5;
                float v = asin(clamp(r.y, -1.0, 1.0)) / PI + 0.5;
                return float2(u, v);
            }

            float3 SampleSky(float3 d, float yaw)
            {
                return SAMPLE_TEXTURE2D_LOD(_SkyTex, sampler_SkyTex, SkyUV(d, yaw), 0).rgb;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float3 sunDisc = normalize(_VhSunDiscDir.xyz + float3(0, 1e-5, 0));
                float mu = dot(d, sunDisc);

                // Cloud drift: two phases half a loop apart, each sliding the upper sky round the vertical axis by up to
                // _DriftDegrees, cross-faded so the loop never jumps. Higher clouds drift less (parallax).
                float t = _VhSkyTime / max(_DriftPeriod, 1.0);
                float p0 = frac(t), p1 = frac(t + 0.5);
                float w0 = 1.0 - abs(2.0 * p0 - 1.0);
                float elevMask = saturate((d.y - 0.03) / 0.08) * (1.0 - 0.6 * saturate(d.y));
                float nearSun = saturate((mu - 0.94) / 0.05);
                float amt = _DriftDegrees * elevMask * (1.0 - nearSun);
                float3 col;
                if (amt > 1e-3)
                {
                    float3 c0 = SampleSky(d, (p0 - 0.5) * amt);
                    float3 c1 = SampleSky(d, (p1 - 0.5) * amt);
                    col = lerp(c1, c0, w0);
                }
                else
                {
                    col = SampleSky(d, 0);
                }
                col *= _Exposure * _Tint.rgb;

                // Saturation (around luminance).
                float lum = dot(col, float3(0.2126, 0.7152, 0.0722));
                col = lerp(lum.xxx, col, _Saturation);

                // The warm lift on the sun's side (horizontal bearing), strongest low in the sky.
                float3 dh = normalize(float3(d.x, 0, d.z) + 1e-5);
                float3 sh = normalize(float3(sunDisc.x, 0, sunDisc.z) + 1e-5);
                float side = pow(saturate(dot(dh, sh) * 0.5 + 0.5), _WarmFocus);
                float low = 1.0 - saturate(d.y * 1.6);
                col *= lerp(float3(1, 1, 1), _WarmSide.rgb, side * low);
                // Pass 3 (round 2: "a cream wash", no lit clouds sunward): the HDRI's bright haze round the low sun is dimmed
                // on the sun's side, so less of it crosses the bloom threshold and the gold-rimmed clouds stand out of it.
                col *= lerp(1.0, _SunSideDim, side * (1.0 - saturate(d.y * 2.5)));

                // Horizon haze band in the atmosphere's haze colours (far hills fade into exactly this), and below the horizon
                // a soft ground fade.
                float band = exp(-abs(d.y) / _HorizonHeight);
                float3 hazeCol = _HorizonColor.rgb * lerp(1.0, _WarmSide.rgb * 1.15, side);
                if (_VhHorizonAway.a > 0.5)
                {
                    float muH = saturate(dot(d, sunDisc));
                    hazeCol = lerp(_VhHorizonAway.rgb, _VhHorizonSun.rgb, pow(muH, max(_VhHorizonFocus, 1.0)));
                }
                col = lerp(col, hazeCol, band * _HorizonHaze);
                float below = saturate(-d.y / 0.06);
                col = lerp(col, lerp(hazeCol, _GroundColor.rgb, saturate(-d.y * 3.0)), below);

                // The sun: a limb-darkened disc, a tight glow and a wide halo.
                float ang = degrees(acos(clamp(mu, -1.0, 1.0)));
                float disc = saturate((_SunDiscSize - ang) / (_SunDiscSize * 0.25));
                float limb = sqrt(saturate(1.0 - pow(ang / _SunDiscSize, 2.0)));
                float aboveHorizon = saturate((d.y + 0.004) / 0.008);
                col += _SunDiscColor.rgb * _SunDiscIntensity * disc * (0.55 + 0.45 * limb) * aboveHorizon;
                float m = saturate(mu);
                col += _SunGlowColor.rgb * (_SunGlow * pow(m, _SunGlowFalloff) + _SunHalo * pow(m, _SunHaloFalloff));

                return half4(max(col, 0.0), 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}

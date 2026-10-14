// Skyline's comic-ink composite (SkylineInkFeature + the SkylineInk volume override; docs/SKYLINE-STYLE.md rules 1 and 2).
// One full-screen pass over the camera colour:
//   1. thin ink from the depth and normal edges (a Roberts-style cross of four diagonal neighbours), fading with distance;
//      sky pixels take a line only where a neighbour is geometry;
//   2. the thick hued outline from the mask SkylineInkMask drew: 12 taps on a circle, 8 on an inner circle and 4 on a tiny one,
//      outside the silhouette only; the weak-spot yellow wins over every other hue, then a bot's own weak point (red-magenta
//      pulsing towards yellow), and both draw on hostile bodies too. Then hostile red-magenta / objective violet, then the
//      strippable wreck's light blue (its alpha carries its width fraction: a tap counts only when its circle is inside that
//      width, so the outline thins as the strip window runs out), then the objective seen through a wall (violet at 45%).
// URP-standard HLSL: Core.hlsl and core's Blit.hlsl (full-screen triangle, _BlitTexture), as Visiting Hours' atmosphere pass.
// Texture coordinates follow Unity's render-texture layout, the same for colour, depth, normals and the mask.
Shader "Hidden/Skyline/InkComposite"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "Skyline Ink Composite"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // The feature binds these itself with a property block (the atmosphere pass's lesson: a global is not guaranteed
            // inside a raster pass that doesn't declare it).
            TEXTURE2D_X_FLOAT(_SkInkDepth);
            TEXTURE2D_X_FLOAT(_SkInkNormals);
            TEXTURE2D_X(_SkInkMask);

            float4 _SkInkColor;     // rgb: the ink, linear
            float4 _SkInkLine;      // x: thin line width (px), y: outline width (px), z: depth threshold, w: normal threshold
            float4 _SkInkFade;      // x: fade start (m), y: fade end (m), z: ink strength at the end, w: 1 when the normals texture is bound
            float4 _SkInkScreen;    // x,y: texel size, z,w: size in pixels
            float4 _SkInkZParams;   // the depth buffer's linearisation, Unity's _ZBufferParams layout, from the camera itself
            float4 _SkInkTime;      // x: time in seconds (the bot weak point's pulse)

            float SkRaw(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_SkInkDepth, sampler_PointClamp, uv, 0).r;
            }

            bool SkIsSky(float raw)
            {
            #if UNITY_REVERSED_Z
                return raw <= 1e-6;
            #else
                return raw >= 1.0 - 1e-6;
            #endif
            }

            float SkEye(float raw)
            {
                return 1.0 / (_SkInkZParams.z * raw + _SkInkZParams.w);
            }

            float3 SkNormal(float2 uv)
            {
                float3 n = SAMPLE_TEXTURE2D_X_LOD(_SkInkNormals, sampler_PointClamp, uv, 0).xyz;
                return n / max(length(n), 1e-4);
            }

            float SkFade(float eyeDepth)
            {
                float t = saturate((eyeDepth - _SkInkFade.x) / max(_SkInkFade.y - _SkInkFade.x, 1e-3));
                return lerp(1.0, _SkInkFade.z, t);
            }

            float4 SkMask(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_SkInkMask, sampler_PointClamp, uv, 0);
            }

            // Mask alpha classes (see SkylineInkMask / SkylineInkXray in InkLit.shader): 1.0 hued (hostile, objective), 0.9 bot weak
            // point, 0.8 weak spot, 0.6 thin ink, 0.4 objective seen through a wall, 0.11 + 0.19 * width fraction strippable wreck.
            bool SkIsHued(float a) { return a > 0.95; }
            bool SkIsBotWeak(float a) { return a > 0.85 && a <= 0.95; }
            bool SkIsWeak(float a) { return a > 0.7 && a <= 0.85; }
            bool SkIsThin(float a) { return a > 0.5 && a <= 0.7; }
            bool SkIsXray(float a) { return a > 0.32 && a <= 0.5; }
            bool SkIsWreck(float a) { return a > 0.1 && a <= 0.32; }
            float SkWreckWidth(float a) { return saturate((a - 0.11) / 0.19); }

            // The role hues, sRGB (converted with SRGBToLinear where they are drawn).
            static const float3 kObjectiveHue = float3(0.7020, 0.4196, 1.0);   // #B36BFF
            static const float3 kWreckHue = float3(0.5608, 0.8275, 1.0);       // #8FD3FF
            static const float kXrayStrength = 0.45;

            // Hostile red-magenta (r high) beats the objective violet.
            float SkHueRank(float3 rgb)
            {
                return rgb.r > 0.9 ? 3.0 : 2.0;
            }

            struct SkHits
            {
                float weak, botWeak, hue, thin, wreck, xray, bestRank;
                float3 bestHue;
            };

            // Counts one mask tap, taken on a circle of the given fraction of the outline radius (1, 0.6, 0.35), into its class.
            // Thin ink counts only from the inner circle (0.6). A wreck's tap counts in full when its circle lies inside the
            // wreck's current width fraction (a short ramp, so the thinning is smooth rather than stepped).
            void SkTap(float4 m, float fraction, inout SkHits h)
            {
                if (SkIsWeak(m.a)) h.weak += 1.0;
                else if (SkIsBotWeak(m.a)) h.botWeak += 1.0;
                else if (SkIsHued(m.a))
                {
                    h.hue += 1.0;
                    float rank = SkHueRank(m.rgb);
                    if (rank > h.bestRank) { h.bestRank = rank; h.bestHue = m.rgb; }
                }
                else if (SkIsWreck(m.a)) h.wreck += saturate((SkWreckWidth(m.a) - fraction) / 0.08 + 1.0);
                else if (SkIsXray(m.a)) h.xray += 1.0;
                else if (fraction > 0.5 && fraction < 0.7 && SkIsThin(m.a)) h.thin += 1.0;
            }

            half4 FragComposite(Varyings i) : SV_Target
            {
                float2 uv = i.texcoord;
                float2 texel = _SkInkScreen.xy;
                float4 scene = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0);
                float3 col = scene.rgb;

                // ---------------------------------------------------------------- 1. thin ink
                float rawC = SkRaw(uv);
                bool skyC = SkIsSky(rawC);
                float zC = SkEye(rawC);
                bool useNormals = _SkInkFade.w > 0.5;
                float3 nC = (useNormals && !skyC) ? SkNormal(uv) : float3(0, 0, 1);

                // lines thin out with distance: the offset shrinks and the strength fades
                float widthPx = _SkInkLine.x * 0.85 * lerp(1.0, 0.6, saturate((zC - _SkInkFade.x) / max(_SkInkFade.y - _SkInkFade.x, 1e-3)));
                float depthT = _SkInkLine.z;
                float normalT = _SkInkLine.w;
                float edge = 0.0;
                static const float2 kDiag[4] = { float2(1, 1), float2(-1, 1), float2(1, -1), float2(-1, -1) };
                [unroll]
                for (int k = 0; k < 4; k++)
                {
                    float2 uvN = uv + kDiag[k] * widthPx * texel;
                    float rawN = SkRaw(uvN);
                    bool skyN = SkIsSky(rawN);
                    if (skyC && skyN) continue;               // open sky: no ink
                    if (skyN) continue;                       // this pixel is the nearer one: the line goes on the far side
                    float zN = SkEye(rawN);
                    // The line sits on the FARTHER pixel: its neighbour is clearly nearer. A sky pixel next to geometry counts
                    // as the farther one (zC is the far plane), so a silhouette against the sky gets its line.
                    float nearer = (zC - zN) / max(zC, 1e-3);
                    float e = smoothstep(depthT, depthT * 1.5, nearer) * SkFade(zN);
                    if (useNormals && !skyC && abs(zN - zC) < zC * depthT * 2.0)
                    {
                        // a crease: the surface turns without a jump in depth
                        float3 nN = SkNormal(uvN);
                        e = max(e, smoothstep(normalT, normalT * 1.3, 1.0 - dot(nC, nN)) * SkFade(zC));
                    }
                    edge = max(edge, e);
                }
                col = lerp(col, _SkInkColor.rgb, saturate(edge));

                // ---------------------------------------------------------------- 2. thick hued outline
                float4 m0 = SkMask(uv);
                bool centreHas = m0.a > 0.1;
                bool centreWeak = centreHas && SkIsWeak(m0.a);
                if (!centreWeak)
                {
                    float2 R = _SkInkLine.y * texel;
                    SkHits h = (SkHits)0;

                    // outer circle: 12 taps at the full radius
                    [unroll]
                    for (int a = 0; a < 12; a++)
                    {
                        float s, c;
                        sincos((a + 0.5) * 0.5235988, s, c);              // 2*pi/12, offset half a step
                        SkTap(SkMask(uv + float2(c, s) * R), 1.0, h);
                    }
                    // inner circle: 8 taps at 0.6 of the radius (a viewmodel's thin ink reaches only this far)
                    [unroll]
                    for (int b = 0; b < 8; b++)
                    {
                        float s, c;
                        sincos(b * 0.7853982, s, c);                      // 2*pi/8
                        SkTap(SkMask(uv + float2(c, s) * R * 0.6), 0.6, h);
                    }
                    // tiny circle: 4 taps at 0.35 of the radius (a strippable wreck's outline, thinned to 40%, lives only here)
                    [unroll]
                    for (int d = 0; d < 4; d++)
                    {
                        float s, c;
                        sincos(d * 1.5707963 + 0.7853982, s, c);          // 2*pi/4, offset an eighth turn
                        SkTap(SkMask(uv + float2(c, s) * R * 0.35), 0.35, h);
                    }

                    float3 outlineRgb = float3(0, 0, 0);
                    float cover = 0.0;
                    if (h.weak > 0.0)
                    {
                        // priority 5 over 6 over 1: the Writ's yellow wins over everything, and also draws on a hostile body
                        // (the centre may be hostile)
                        outlineRgb = SRGBToLinear(float3(1.0, 0.8392, 0.0392));
                        cover = smoothstep(0.0, 4.0, h.weak);
                    }
                    else if (h.botWeak > 0.0 && !SkIsBotWeak(m0.a))
                    {
                        // a bot's own weak point: the hostile red-magenta tinted towards yellow, t pulsing 0.35..0.6 at 1.2 Hz
                        float t = lerp(0.35, 0.6, 0.5 + 0.5 * sin(_SkInkTime.x * 7.5398));   // 2*pi*1.2
                        outlineRgb = SRGBToLinear(lerp(float3(1.0, 0.1804, 0.3882), float3(1.0, 0.8392, 0.0392), t));
                        cover = smoothstep(0.0, 4.0, h.botWeak);
                    }
                    else if (!centreHas && h.hue > 0.0)
                    {
                        outlineRgb = SRGBToLinear(h.bestHue);
                        cover = smoothstep(0.0, 4.0, h.hue);
                    }
                    else if (!centreHas && h.wreck > 0.0)
                    {
                        // a strippable wreck: light blue, thinner as its strip window runs out (the width is in the mask alpha)
                        outlineRgb = SRGBToLinear(kWreckHue);
                        cover = smoothstep(0.0, 2.5, h.wreck);
                    }
                    else if (!centreHas && h.xray > 0.0)
                    {
                        // the objective behind a wall: the same violet, dimmer
                        outlineRgb = SRGBToLinear(kObjectiveHue);
                        cover = smoothstep(0.0, 4.0, h.xray) * kXrayStrength;
                    }
                    else if (!centreHas && h.thin > 0.0)
                    {
                        outlineRgb = _SkInkColor.rgb;
                        cover = smoothstep(0.0, 3.0, h.thin);
                    }
                    col = lerp(col, outlineRgb, cover);
                }

                return half4(col, scene.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

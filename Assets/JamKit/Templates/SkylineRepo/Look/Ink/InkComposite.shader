// Skyline's comic-ink composite (SkylineInkFeature + the SkylineInk volume override; docs/SKYLINE-STYLE.md rules 1 and 2).
// Three passes, all full-screen triangles (URP's core Blit.hlsl vertex stage):
//   0  Composite: draws over the camera colour with a premultiplied blend (Blend One SrcAlpha: result = src.rgb + dst.rgb * src.a),
//      so it never reads the colour and the feature needs no copy of it. Pixels with no ink and no outline are discarded.
//        1. thin ink from the depth edges (a Roberts-style cross of four diagonal neighbours) and the creases (the normals'
//           difference, or the depth plane's curvature when the volume asks for it), fading with distance; sky pixels take a
//           line only where a neighbour is geometry;
//        2. the thick hued outline from the mask SkylineInkMask drew: 12 taps on a circle, 8 on an inner circle and 4 on a tiny
//           one, outside the silhouette only, and only where the occupancy texture says the mask is within reach (the 24 taps
//           are skipped everywhere else); the weak-spot yellow wins over every other hue, then a bot's own weak point
//           (red-magenta pulsing towards yellow), and both draw on hostile bodies too. Then hostile red-magenta / objective
//           violet, then the strippable wreck's light blue (its alpha carries its width fraction: a tap counts only when its
//           circle is inside that width, so the outline thins as the strip window runs out), then the objective seen through
//           a wall (violet at 45%);
//        3. the optional stylization that lives here (line-weight variation, hand wobble on the thin ink only, paper grain on
//           the scene colour only): never on a hued outline.
//   1  Occupancy: one texel per 4x4 block of the mask, 1 where any mask texel is set (four bilinear taps, each an average of 2x2).
//   2  Dilate: the occupancy grown by k blocks on every side (k = ceil(outline radius / 4)), so a 0 means no mask in reach.
// Texture coordinates follow Unity's render-texture layout, the same for colour, depth, normals, the mask and the occupancy.
Shader "Hidden/Skyline/InkComposite"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        // The feature binds these itself with a property block (the atmosphere pass's lesson: a global is not guaranteed
        // inside a raster pass that doesn't declare it).
        TEXTURE2D_X_FLOAT(_SkInkDepth);
        TEXTURE2D_X_FLOAT(_SkInkNormals);
        TEXTURE2D_X(_SkInkMask);
        TEXTURE2D_X(_SkInkOcc);       // the dilated occupancy (composite)
        TEXTURE2D_X(_SkInkOccSrc);    // the raw occupancy (dilate pass)

        float4 _SkInkColor;     // rgb: the ink, linear
        float4 _SkInkLine;      // x: thin line width (px), y: outline width (px), z: depth threshold, w: normal threshold
        float4 _SkInkFade;      // x: fade start (m), y: fade end (m), z: ink strength at the end, w: 1 when the normals texture is bound
        float4 _SkInkScreen;    // x,y: texel size, z,w: size in pixels
        float4 _SkInkZParams;   // the depth buffer's linearisation, Unity's _ZBufferParams layout, from the camera itself
        float4 _SkInkTime;      // x: time in seconds (the bot weak point's pulse, the wobble's boil)
        float4 _SkInkMode;      // x: 1 = creases from the depth curvature, y: crease slope threshold, z: one pixel's angle (rad)
        float4 _SkInkStyle;     // x: line-weight variation, y: wobble amplitude (px), z: paper grain (all 0..1 / px, 0 = off)
        float4 _SkInkOccParams;     // x: dilation blocks k, z,w: occupancy size in texels
        ENDHLSL

        Pass
        {
            Name "Skyline Ink Composite"
            // result = src.rgb + dst.rgb * src.a: the feature's output is "ink premultiplied" and alpha is what is left of the scene.
            Blend One SrcAlpha
            ColorMask RGB

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite

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

            float SkInvEye(float raw)
            {
                return _SkInkZParams.z * raw + _SkInkZParams.w;     // 1 / eye depth, affine in the raw depth
            }

            float SkEye(float raw)
            {
                return 1.0 / SkInvEye(raw);
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

            // ---- noise for the optional stylization (no textures: a hash)
            float2 SkHash2(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy) * 2.0 - 1.0;
            }

            float SkHash1(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // Smooth value noise of a 2D vector, -1..1 per component; two octaves, cells of 23 and 7 pixels.
            float2 SkWobble(float2 pix, float seed)
            {
                float2 g = pix / 23.0 + seed * 7.31;
                float2 c = floor(g);
                float2 f = frac(g);
                f = f * f * (3.0 - 2.0 * f);
                float2 a = lerp(SkHash2(c), SkHash2(c + float2(1, 0)), f.x);
                float2 b = lerp(SkHash2(c + float2(0, 1)), SkHash2(c + float2(1, 1)), f.x);
                float2 big = lerp(a, b, f.y);
                // a finer octave at a third of the strength: the line shivers rather than only drifts
                float2 g2 = pix / 7.0 + seed * 3.17;
                float2 c2 = floor(g2);
                float2 f2 = frac(g2);
                f2 = f2 * f2 * (3.0 - 2.0 * f2);
                float2 a2 = lerp(SkHash2(c2), SkHash2(c2 + float2(1, 0)), f2.x);
                float2 b2 = lerp(SkHash2(c2 + float2(0, 1)), SkHash2(c2 + float2(1, 1)), f2.x);
                return big * 0.7 + lerp(a2, b2, f2.y) * 0.3;
            }

            half4 FragComposite(Varyings i) : SV_Target
            {
                float2 uv = i.texcoord;
                float2 texel = _SkInkScreen.xy;
                float2 pix = uv * _SkInkScreen.zw;
                float lineVar = _SkInkStyle.x;
                float wobblePx = _SkInkStyle.y;
                float grainAmt = _SkInkStyle.z;

                // ---------------------------------------------------------------- 1. thin ink
                // The hand wobble moves where the lines are looked up (the whole thin-ink image shifts by a smooth noise); the
                // thick outline below keeps the true uv, so a hued outline never wobbles.
                float2 uvT = uv;
                if (wobblePx > 0.0) uvT = uv + SkWobble(pix, floor(_SkInkTime.x * 6.0)) * wobblePx * texel;

                float rawC = SkRaw(uvT);
                bool skyC = SkIsSky(rawC);
                float zC = SkEye(rawC);
                bool curve = _SkInkMode.x > 0.5;
                bool useNormals = !curve && _SkInkFade.w > 0.5;
                float3 nC = (useNormals && !skyC) ? SkNormal(uvT) : float3(0, 0, 1);

                // lines thin out with distance: the offset shrinks and the strength fades
                float farT = saturate((zC - _SkInkFade.x) / max(_SkInkFade.y - _SkInkFade.x, 1e-3));
                float widthPx = _SkInkLine.x * 0.85 * lerp(1.0, 0.6 - 0.25 * lineVar, farT);
                float creaseKeep = 1.0;
                if (lineVar > 0.0)
                {
                    // line-weight variation: near objects draw heavier lines (up to +90% close up), the far taper is steeper, and
                    // creases inside a surface are lighter than silhouettes
                    float nearT = 1.0 - saturate(zC / max(_SkInkFade.y * 0.5, 1.0));
                    widthPx *= 1.0 + 0.9 * lineVar * nearT * nearT;
                    creaseKeep = 1.0 - 0.45 * lineVar;
                }
                float depthT = _SkInkLine.z;
                float normalT = _SkInkLine.w;
                float edge = 0.0;
                static const float2 kDiag[4] = { float2(1, 1), float2(-1, 1), float2(1, -1), float2(-1, -1) };
                float zN[4];
                bool skyN[4];
                [unroll]
                for (int k = 0; k < 4; k++)
                {
                    float2 uvN = uvT + kDiag[k] * widthPx * texel;
                    float rawN = SkRaw(uvN);
                    skyN[k] = SkIsSky(rawN);
                    zN[k] = SkEye(rawN);
                    if (skyN[k]) continue;                    // this pixel is the nearer one (or open sky): the line goes on the far side
                    // The line sits on the FARTHER pixel: its neighbour is clearly nearer. A sky pixel next to geometry counts
                    // as the farther one (zC is the far plane), so a silhouette against the sky gets its line.
                    float nearer = (zC - zN[k]) / max(zC, 1e-3);
                    float e = smoothstep(depthT, depthT * 1.5, nearer) * SkFade(zN[k]);
                    if (useNormals && !skyC && abs(zN[k] - zC) < zC * depthT * 2.0)
                    {
                        // a crease: the surface turns without a jump in depth
                        float3 nN = SkNormal(uvN);
                        e = max(e, smoothstep(normalT, normalT * 1.3, 1.0 - dot(nC, nN)) * SkFade(zC) * creaseKeep);
                    }
                    edge = max(edge, e);
                }
                if (curve && !skyC)
                {
                    // a crease from the depth plane's bend: 1 / eye depth is affine in screen space on a flat surface, so the second
                    // difference across the pixel vanishes on a plane and jumps where two planes meet. Measured in one pixel's
                    // slope change (relative to 1 / depth and the pixel's angle) so it does not depend on distance or grazing angle.
                    float invC = 1.0 / zC;
                    float lenPx = widthPx * 1.41421;
                    float denom = max(invC * lenPx * _SkInkMode.z, 1e-9);
                    float thr = _SkInkMode.y;
                    float band = zC * depthT * 2.0;
                    if (!skyN[0] && !skyN[3] && abs(zN[0] - zC) < band && abs(zN[3] - zC) < band)
                    {
                        float sc = abs(1.0 / zN[0] + 1.0 / zN[3] - 2.0 * invC) / denom;
                        edge = max(edge, smoothstep(thr, thr * 1.3, sc) * SkFade(zC) * creaseKeep);
                    }
                    if (!skyN[1] && !skyN[2] && abs(zN[1] - zC) < band && abs(zN[2] - zC) < band)
                    {
                        float sc = abs(1.0 / zN[1] + 1.0 / zN[2] - 2.0 * invC) / denom;
                        edge = max(edge, smoothstep(thr, thr * 1.3, sc) * SkFade(zC) * creaseKeep);
                    }
                }
                float ink = saturate(edge);

                // ---------------------------------------------------------------- 2. thick hued outline
                float3 outlineRgb = float3(0, 0, 0);
                float cover = 0.0;
                // Only where the occupancy says the mask lies within reach of this pixel's outline radius; everywhere else the
                // 24 taps below would find nothing.
                int2 occCell = int2(floor(pix * 0.25));
                if (LOAD_TEXTURE2D_X(_SkInkOcc, occCell).r > 0.5)
                {
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
                    }
                }

                // ---------------------------------------------------------------- 3. blend: ink under the outline, then the scene
                // Same as lerp(lerp(scene, ink, e), outline, c): rgb = ink * e * (1 - c) + outline * c; the scene keeps (1 - e) * (1 - c).
                float keep = (1.0 - ink) * (1.0 - cover);
                float3 rgb = _SkInkColor.rgb * (ink * (1.0 - cover)) + outlineRgb * cover;
                if (grainAmt > 0.0)
                {
                    // paper grain: sparse dark flecks on the scene colour (screen-fixed like paper), never on an outline
                    float n = SkHash1(floor(pix * 0.6)) * 0.75 + SkHash1(floor(pix / 5.0) + 11.0) * 0.4;
                    float fleck = smoothstep(0.55, 1.0, n);
                    keep *= 1.0 - grainAmt * 0.14 * fleck * (1.0 - cover);
                }
                if (keep > 0.9995) discard;
                return half4(rgb, keep);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Skyline Ink Occupancy"
            Blend Off
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragOccupancy

            // One texel = one 4x4 block of the mask. Four bilinear taps, each at the corner of a 2x2 group of mask texels (so each
            // averages exactly those four), tile the block; any set texel gives an average above 0.004 (the faintest class is
            // 0.11 / 4).
            float FragOccupancy(Varyings i) : SV_Target
            {
                float2 cell = floor(i.texcoord * _SkInkOccParams.zw);
                float2 centre = (cell * 4.0 + 2.0) * _SkInkScreen.xy;       // between the block's middle four pixels
                float2 o = _SkInkScreen.xy;
                float m = 0.0;
                m = max(m, SAMPLE_TEXTURE2D_X_LOD(_SkInkMask, sampler_LinearClamp, centre + float2(-1, -1) * o, 0).a);
                m = max(m, SAMPLE_TEXTURE2D_X_LOD(_SkInkMask, sampler_LinearClamp, centre + float2(1, -1) * o, 0).a);
                m = max(m, SAMPLE_TEXTURE2D_X_LOD(_SkInkMask, sampler_LinearClamp, centre + float2(-1, 1) * o, 0).a);
                m = max(m, SAMPLE_TEXTURE2D_X_LOD(_SkInkMask, sampler_LinearClamp, centre + float2(1, 1) * o, 0).a);
                return m > 0.004 ? 1.0 : 0.0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Skyline Ink Occupancy Dilate"
            Blend Off
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDilate

            float FragDilate(Varyings i) : SV_Target
            {
                int2 c = int2(floor(i.texcoord * _SkInkOccParams.zw));
                int2 hi = int2(_SkInkOccParams.zw) - 1;
                int k = (int)(_SkInkOccParams.x + 0.5);
                float m = 0.0;
                for (int y = -k; y <= k; y++)
                {
                    for (int x = -k; x <= k; x++)
                    {
                        int2 p = clamp(c + int2(x, y), int2(0, 0), hi);
                        m = max(m, LOAD_TEXTURE2D_X(_SkInkOccSrc, p).r);
                    }
                }
                return m;
            }
            ENDHLSL
        }
    }
    FallBack Off
}

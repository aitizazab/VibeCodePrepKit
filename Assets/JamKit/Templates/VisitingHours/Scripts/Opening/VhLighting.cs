using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// The opening's and the title table's lighting and grade, applied once both sets are built
    /// (<see cref="VhStageBuilder.Build"/>). The lighting overhaul (the creator, 2026-10-11; docs/OPENING-AND-TITLE.md "Lighting").
    /// <para><b>The bedroom at night:</b> a dark, cool room cut by one warm motivated key, the sodium streetlight through the
    /// blinds (stripes across the ceiling, the wall and the bed), cold moonlight through the window as the main light (the
    /// blinds' shadow on the bed and floor), a faint cool spill inside the window, and the phone as the only light near her:
    /// a cool glow on the nightstand and, in her hand, on her fingers and the blanket. Graded teal shadows and warm highlights,
    /// dark but not black, a vignette, bloom on the practicals, a little grain; the background falls out of focus behind
    /// the phone.</para>
    /// <para><b>The table at night</b> (the creator's sketch, 2026-10-12: the same night as the opening, real light): seen from
    /// straight above, lit by one warm lamp off the frame's upper right (a soft-shadowed spot, its pool falling off towards the
    /// lower left), a faint cold moonlight fill, a dark cool room, and the tablet's screen on the table (red and blue from the
    /// report's emergency lights while it plays). Graded like the bedroom's night: teal shadows, warm highlights.</para>
    /// <para><b>The streetlight's stripes</b> come from a cookie traced from the real window and slats, on every platform: the
    /// spot's own soft shadows (PC) thicken the 4 mm slats until they close the gaps (round 2's PC captures had no stripes), and
    /// WebGL (the Mobile quality level) has no spot-light shadows at all. Everything used (cookies, Gaussian depth of field,
    /// bloom, tonemapping, SMAA) works on WebGL; SSAO is PC only.</para>
    /// Per-moment changes run in <see cref="VhLightingRig"/>.
    /// </summary>
    public static class VhLighting
    {
        // ---- The bedroom ----
        /// <summary>The hemisphere ambient at night: low and cool, so the motivated lights shape the room.</summary>
        public static readonly Color NightSky = new Color(0.030f, 0.038f, 0.062f), NightGround = new Color(0.012f, 0.013f, 0.020f);
        /// <summary>The streetlight: warm sodium-amber, the room's key.</summary>
        public static readonly Color StreetColor = new Color(1f, 0.70f, 0.42f);
        public const float StreetIntensity = 7f;
        /// <summary>The moon through the window: cold blue, the main light (its shadows work everywhere, WebGL too).</summary>
        public static readonly Color MoonColor = new Color(0.42f, 0.54f, 0.88f);
        public const float MoonIntensity = 0.32f;
        /// <summary>The phone's screen: cool white-blue. URP's point and spot lights fall off as 1/d^2, so the screen's
        /// lights sit 12-14 cm off the glass at small intensities (0.012 lights the nightstand top to about 0.8; a light
        /// a few centimetres from a surface blows it out).</summary>
        public static readonly Color ScreenColor = new Color(0.66f, 0.78f, 1f);
        public const float PhoneGlowIntensity = 0.012f, PhoneGlowRange = 1.5f, PhoneGlowHeight = 0.12f;

        // ---- The table (at night, under a lamp) ----
        /// <summary>The room's ambient at the table at night: dim and cool, so the lamp shapes the table.</summary>
        public static readonly Color TableNightSky = new Color(0.050f, 0.058f, 0.085f), TableNightGround = new Color(0.026f, 0.024f, 0.026f);
        /// <summary>The lamp: warm tungsten, off the frame's upper right, about half a metre above the table, aimed across it.</summary>
        public static readonly Color LampColor = new Color(1f, 0.74f, 0.46f);
        public const float LampIntensity = 1.0f;
        public static readonly Vector3 LampPosition = new Vector3(0.42f, 1.25f, 0.32f), LampTarget = new Vector3(0.08f, 0.75f, 0f);
        /// <summary>A faint cold moonlight from the other side, so the shadows aren't black.</summary>
        public const float MoonFillIntensity = 0.07f;
        // (The morning set-up, kept for reference: the title was a morning table before the creator's sketch.)
        public static readonly Color MorningSky = new Color(0.24f, 0.26f, 0.32f), MorningGround = new Color(0.17f, 0.145f, 0.12f);
        /// <summary>The sun: warm, about 35 degrees up, from the camera's left and a little behind it.</summary>
        public static readonly Color SunColor = new Color(1f, 0.90f, 0.76f);
        public const float SunIntensity = 2.2f;
        public static readonly Vector3 SunDirection = new Vector3(0.70f, -0.58f, 0.42f);
        /// <summary>Where the window patch is centred (table space): among the mug, the badge and the tablet, so the pieces sit in
        /// the sun and the cross of the mullions lands on the table between them.</summary>
        public static readonly Vector3 SunPatchCentre = new Vector3(0.08f, 0.75f, -0.02f);
        /// <summary>The sky fill from the room side: cool, soft, no shadows.</summary>
        public static readonly Color FillColor = new Color(0.55f, 0.64f, 0.86f);
        public const float FillIntensity = 0.26f;
        /// <summary>How much of the world the sun's window cookie covers (m): the window patch is about 0.85 m across (and
        /// 1.7 m long on the table, stretched by the sun's angle), so its edges and the cross of its mullions land on the table.</summary>
        public const float WindowCookieSize = 1.2f;
        /// <summary>A warm glow of morning on the wallpaper behind the table (a soft spot, no shadows).</summary>
        public static readonly Color WallGlowColor = new Color(1f, 0.80f, 0.58f);
        public const float WallGlowIntensity = 0.7f;

        public static void Apply(VhStage s)
        {
            if (s == null) return;
            var rig = s.gameObject.AddComponent<VhLightingRig>();
            rig.stage = s;
            LightBedroom(s, rig);
            LightTable(s, rig);
            Grade(s, rig);
            rig.Init();
        }

        // ---------- The bedroom ----------

        static void LightBedroom(VhStage s, VhLightingRig rig)
        {
            s.bedSky = NightSky;
            s.bedGround = NightGround;
            s.bedBackground = new Color(0.006f, 0.008f, 0.014f);
            var b = s.bedroom;

            if (s.street != null)
            {
                var l = s.street;
                s.streetIntensity = StreetIntensity;
                l.color = StreetColor;
                l.intensity = StreetIntensity;
                l.range = 10f;
                l.spotAngle = 70f;
                l.innerSpotAngle = 40f;
                // The window and the blinds come from the traced cookie (see the class notes), not from shadows.
                l.shadows = LightShadows.None;
                if (b != null)
                {
                    var cookie = BlindsCookie(l, b);
                    rig.owned.Add(cookie);
                    l.cookie = cookie;
                }
            }

            if (s.moon != null)
            {
                var m = s.moon;
                s.moonIntensity = MoonIntensity;
                m.color = MoonColor;
                m.intensity = MoonIntensity;
                m.shadows = LightShadows.Soft;
                m.shadowStrength = 0.9f;
                Shadows(m, 0.03f, 0.25f, SoftShadowQuality.High);
            }

            if (s.phoneGlow != null)
            {
                var g = s.phoneGlow;
                s.phoneGlowIntensity = PhoneGlowIntensity;
                g.color = ScreenColor;
                g.intensity = PhoneGlowIntensity;
                g.range = PhoneGlowRange;
                g.shadows = LightShadows.None;
                g.transform.localPosition = new Vector3(0f, PhoneGlowHeight, 0f);
            }

            if (s.phone != null)
            {
                // The screen's light thrown forward from its face (in her hand): her fingers, the pillow, the blanket's edge.
                // It sits 14 cm behind the glass so the falloff is gentle across her hand.
                var faceGo = new GameObject("Screen face light (light)");
                faceGo.transform.SetParent(s.phone, false);
                faceGo.transform.localPosition = new Vector3(0f, -0.14f, 0f);
                faceGo.transform.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
                var f = faceGo.AddComponent<Light>();
                f.type = LightType.Spot;
                f.color = ScreenColor;
                f.range = 1.6f;
                f.spotAngle = 130f;
                f.innerSpotAngle = 60f;
                f.intensity = 0f;
                f.shadows = LightShadows.None;
                rig.phoneFace = f;
            }

            if (b != null)
            {
                // Cool moonlight spilling in through the window: a wide, faint spot from the window towards the bed.
                var spillGo = new GameObject("Window spill (light)");
                spillGo.transform.SetParent(b, false);
                spillGo.transform.localPosition = new Vector3(-1.7f, 1.6f, 0.15f);
                spillGo.transform.localRotation = Quaternion.LookRotation(new Vector3(1f, -0.45f, 0.1f), Vector3.up);
                var sp = spillGo.AddComponent<Light>();
                sp.type = LightType.Spot;
                sp.spotAngle = 110f;
                sp.innerSpotAngle = 40f;
                sp.color = new Color(0.45f, 0.56f, 0.86f);
                sp.range = 4f;
                sp.intensity = rig.windowSpillIntensity;
                sp.shadows = LightShadows.None;
                rig.windowSpill = sp;

                // The practicals glow (bloom picks them up): the streetlight's lamp and the windows across the street.
                foreach (var r in b.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.name == "Streetlight lamp") VhWakingStyle.SetEmission(r.sharedMaterial, StreetColor * 4.5f);
                    else if (r.name == "Lit window") VhWakingStyle.SetEmission(r.sharedMaterial, new Color(0.95f, 0.68f, 0.38f) * 1.1f);
                }
            }
        }

        /// <summary>The window and the half-open blinds as the streetlight sees them, for a spot cookie (white = light):
        /// each texel's ray from the light is traced to the window opening and through the slats.</summary>
        static Texture2D BlindsCookie(Light l, Transform bedroom)
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true) { name = "VH blinds cookie", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            float half = Mathf.Tan(l.spotAngle * 0.5f * Mathf.Deg2Rad);
            Vector3 origin = bedroom.InverseTransformPoint(l.transform.position);
            const float windowX = -1.84f, slatX = -1.75f;
            const float zMin = -0.45f, zMax = 0.75f, yMin = 0.95f, yMax = 2.15f;
            const float slatY0 = 1.0f, slatPitch = 0.051f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    Vector3 dirWorld = l.transform.TransformDirection(new Vector3(u * half, v * half, 1f));
                    Vector3 d = bedroom.InverseTransformDirection(dirWorld);
                    float value = 0f;
                    if (d.x > 1e-4f)
                    {
                        float t = (windowX - origin.x) / d.x;
                        Vector3 p = origin + d * t;
                        if (p.z > zMin && p.z < zMax && p.y > yMin && p.y < yMax)
                        {
                            // Through the slats' plane: half-open slats (tilted 38 degrees) leave about half of each pitch open,
                            // with a soft edge.
                            float ts = (slatX - origin.x) / d.x;
                            float ys = origin.y + d.y * ts;
                            float k = Mathf.Repeat((ys - slatY0) / slatPitch, 1f);
                            float open = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.40f, 0.50f, k)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.92f, 1f, k)));
                            float edge = Mathf.Min(Mathf.Min(p.z - zMin, zMax - p.z), Mathf.Min(p.y - yMin, yMax - p.y));
                            value = open * Mathf.Clamp01(edge / 0.03f);
                        }
                    }
                    byte c = (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
                    px[y * size + x] = new Color32(c, c, c, c);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        // ---------- The table ----------

        static void LightTable(VhStage s, VhLightingRig rig)
        {
            s.tableSky = TableNightSky;
            s.tableGround = TableNightGround;
            s.tableBackground = new Color(0.012f, 0.012f, 0.018f);
            var t = s.tableRoot;
            if (s.tableKey != null && t != null)
            {
                // The lamp: a warm spot off the frame's upper right, soft shadows falling to the lower left.
                var k = s.tableKey;
                k.type = LightType.Spot;
                k.color = LampColor;
                k.intensity = LampIntensity;
                k.range = 4f;
                k.spotAngle = 120f;
                k.innerSpotAngle = 50f;
                k.transform.position = t.TransformPoint(LampPosition);
                k.transform.rotation = Quaternion.LookRotation((LampTarget - LampPosition).normalized, Vector3.up);
                k.shadows = LightShadows.Soft;
                k.shadowStrength = 0.8f;
                Shadows(k, 0.01f, 0.15f, SoftShadowQuality.High);
            }
            if (t != null)
            {
                // A faint cold moonlight from the room's other side (with the table root, so it's only on at the table).
                var fillGo = new GameObject("Moonlight at the table (light)");
                fillGo.transform.SetParent(t, false);
                fillGo.transform.rotation = Quaternion.LookRotation(new Vector3(0.5f, -0.8f, -0.3f), Vector3.up);
                var f = fillGo.AddComponent<Light>();
                f.type = LightType.Directional;
                f.color = MoonColor;
                f.intensity = MoonFillIntensity;
                f.shadows = LightShadows.None;

                var glowGo = new GameObject("Tablet glow (light)");
                glowGo.transform.SetParent(t, false);
                var g = glowGo.AddComponent<Light>();
                g.type = LightType.Point;
                g.color = new Color(0.78f, 0.86f, 1f);
                g.range = 0.9f;
                g.intensity = rig.tabletGlowIntensity;
                g.shadows = LightShadows.None;
                rig.tabletGlow = g;
            }
        }

        /// <summary>A window for the sun's cookie: a frame of shadow around four panes with soft mullions (white = sun).</summary>
        static Texture2D WindowCookie()
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true) { name = "VH window cookie", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            // The window covers the middle 70% x 80%: two columns and two rows of panes, soft mullions, a soft frame.
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                    float value = 0f;
                    if (u > 0.15f && u < 0.85f && v > 0.1f && v < 0.9f)
                    {
                        float wu = Mathf.InverseLerp(0.15f, 0.85f, u), wv = Mathf.InverseLerp(0.1f, 0.9f, v);
                        float edge = Mathf.Min(Mathf.Min(wu, 1f - wu), Mathf.Min(wv, 1f - wv));
                        float mu = Mathf.Abs(wu - 0.5f), mv = Mathf.Abs(wv - 0.5f);
                        float mullion = Mathf.Min(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.016f, 0.034f, mu)), Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.013f, 0.029f, mv)));
                        value = mullion * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / 0.035f));
                    }
                    byte c = (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
                    px[y * size + x] = new Color32(c, c, c, c);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        static void Shadows(Light l, float bias, float normalBias, SoftShadowQuality quality)
        {
            var d = l.GetUniversalAdditionalLightData();
            if (d == null) return;
            d.usePipelineSettings = false;
            l.shadowBias = bias;
            l.shadowNormalBias = normalBias;
            d.softShadowQuality = quality;
        }

        // ---------- The grade ----------

        static void Grade(VhStage s, VhLightingRig rig)
        {
            if (s.cam != null)
            {
                s.cam.allowHDR = true;
                var cd = s.cam.GetUniversalAdditionalCameraData();
                cd.renderPostProcessing = true;
                cd.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                cd.antialiasingQuality = AntialiasingQuality.High;
                cd.renderShadows = true;
                // A blown pixel (NaN or infinity) must never spread through bloom.
                cd.stopNaN = true;
            }

            // Night: teal shadows, warm highlights, contrast with the blacks just above black; the practicals bloom.
            var night = Profile(rig, "VH night grade");
            night.Add<Tonemapping>().mode.Override(TonemappingMode.ACES);
            var nc = night.Add<ColorAdjustments>();
            nc.postExposure.Override(0.15f);
            nc.contrast.Override(14f);
            nc.saturation.Override(-8f);
            night.Add<WhiteBalance>().temperature.Override(-6f);
            var nsmh = night.Add<ShadowsMidtonesHighlights>();
            nsmh.shadows.Override(new Vector4(0.86f, 0.98f, 1.12f, 0f));
            nsmh.midtones.Override(new Vector4(0.96f, 1f, 1.04f, 0f));
            nsmh.highlights.Override(new Vector4(1.1f, 1.0f, 0.88f, 0f));
            night.Add<LiftGammaGain>().lift.Override(new Vector4(0.98f, 1f, 1.04f, 0.012f));
            var nb = night.Add<Bloom>();
            nb.threshold.Override(0.85f);
            nb.intensity.Override(0.7f);
            nb.scatter.Override(0.72f);
            nb.tint.Override(new Color(1f, 0.9f, 0.8f));
            nb.highQualityFiltering.Override(true);
            nb.clamp.Override(20f);
            var nv = night.Add<Vignette>();
            nv.intensity.Override(0.4f);
            nv.smoothness.Override(0.45f);
            nv.color.Override(new Color(0.01f, 0.015f, 0.03f));
            var ng = night.Add<FilmGrain>();
            ng.type.Override(FilmGrainLookup.Thin2);
            ng.intensity.Override(0.22f);
            ng.response.Override(0.85f);
            var nd = night.Add<DepthOfField>();
            nd.mode.Override(DepthOfFieldMode.Gaussian);
            nd.gaussianStart.Override(0.6f);
            nd.gaussianEnd.Override(2.5f);
            nd.gaussianMaxRadius.Override(1.2f);
            nd.highQualitySampling.Override(true);
            rig.night = Volume(s, "Night grade (volume)", night);
            rig.nightDof = nd;
            rig.nightColor = nc;

            // The table at night (the rig's "morning" volume, kept by that name): like the bedroom's night, a touch brighter
            // for the menu: teal shadows, warm lamp highlights, soft bloom, a vignette.
            var morning = Profile(rig, "VH table night grade");
            morning.Add<Tonemapping>().mode.Override(TonemappingMode.ACES);
            var mc = morning.Add<ColorAdjustments>();
            mc.postExposure.Override(0.3f);
            mc.contrast.Override(12f);
            mc.saturation.Override(-8f);
            var mw = morning.Add<WhiteBalance>();
            mw.temperature.Override(-3f);
            mw.tint.Override(0f);
            var msmh = morning.Add<ShadowsMidtonesHighlights>();
            msmh.shadows.Override(new Vector4(0.88f, 0.98f, 1.10f, 0f));
            msmh.highlights.Override(new Vector4(1.08f, 1.0f, 0.90f, 0f));
            morning.Add<LiftGammaGain>().lift.Override(new Vector4(0.98f, 1f, 1.04f, 0.014f));
            var mb = morning.Add<Bloom>();
            mb.threshold.Override(0.9f);
            mb.intensity.Override(0.45f);
            mb.scatter.Override(0.75f);
            mb.tint.Override(new Color(1f, 0.92f, 0.82f));
            mb.highQualityFiltering.Override(true);
            mb.clamp.Override(20f);
            var mv = morning.Add<Vignette>();
            mv.intensity.Override(0.38f);
            mv.smoothness.Override(0.5f);
            var mg = morning.Add<FilmGrain>();
            mg.type.Override(FilmGrainLookup.Thin1);
            mg.intensity.Override(0.12f);
            mg.response.Override(0.8f);
            var md = morning.Add<DepthOfField>();
            md.mode.Override(DepthOfFieldMode.Gaussian);
            md.gaussianStart.Override(1.2f);
            md.gaussianEnd.Override(3.5f);
            md.gaussianMaxRadius.Override(1f);
            md.highQualitySampling.Override(true);
            rig.morning = Volume(s, "Morning grade (volume)", morning);
            rig.morningDof = md;
        }

        static VolumeProfile Profile(VhLightingRig rig, string name)
        {
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            p.name = name;
            rig.owned.Add(p);
            return p;
        }

        static Volume Volume(VhStage s, string name, VolumeProfile profile)
        {
            var go = new GameObject(name);
            go.transform.SetParent(s.transform, false);
            var v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.priority = 50f;
            v.sharedProfile = profile;
            v.weight = 0f;
            return v;
        }
    }
}

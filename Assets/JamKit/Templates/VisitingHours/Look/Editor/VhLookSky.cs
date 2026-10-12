using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace JamKit.Look.EditorTools
{
    /// <summary>
    /// The golden-hour skies (Poly Haven HDRIs, CC0, prepared by tools/look/hdri-prep.py: the sun found and clamped out,
    /// the sky's colours measured, ThirdParty/LookSky/skies.json). Each gets a JamKit/Look/Sky material
    /// (Look/Library/VH_Sky_&lt;name&gt;.mat). The default is Kloppenheim 06: scattered cumulus lit gold from below by a sun
    /// 7 degrees up, crepuscular rays, a clear blue zenith; the most "late-summer Saturday evening" of the candidates.
    /// </summary>
    public static class VhLookSky
    {
        public const string Default = "Sky_Kloppenheim06_4k";

        public struct Sky
        {
            public string name;
            public float sunElevation, sunAzimuth;
            public Color skyAverage, horizon, sunSideHorizon, zenith, ground;
            public int maxSize;
            public string why;
        }

        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        /// <summary>From ThirdParty/LookSky/skies.json (sun elevation/azimuth in degrees, colours linear, sky exposure 1).</summary>
        public static readonly Sky[] Skies =
        {
            new Sky { name = "Sky_Kloppenheim06_4k", sunElevation = 6.98f, sunAzimuth = 39.36f, maxSize = 4096,
                skyAverage = C(0.81f, 0.951f, 1.178f), horizon = C(1.054f, 0.881f, 0.773f), sunSideHorizon = C(2.172f, 1.517f, 1.031f),
                zenith = C(0.263f, 0.403f, 0.667f), ground = C(0.448f, 0.5f, 0.589f),
                why = "The pick: gold-rimmed scattered cumulus and rays round a low sun, blue overhead." },
            new Sky { name = "Sky_TableMountain2_2k", sunElevation = 7.27f, sunAzimuth = 49.8f, maxSize = 2048,
                skyAverage = C(0.272f, 0.304f, 0.342f), horizon = C(0.686f, 0.753f, 0.633f), sunSideHorizon = C(2.056f, 1.994f, 1.446f),
                zenith = C(0.095f, 0.108f, 0.147f), ground = C(0.172f, 0.191f, 0.201f),
                why = "Alternate: big summer cumulus towers (darker sky; raise its exposure about 3x)." },
            new Sky { name = "Sky_QwantaniSunset_2k", sunElevation = 6.06f, sunAzimuth = 36.04f, maxSize = 2048,
                skyAverage = C(0.955f, 1.072f, 1.28f), horizon = C(2.334f, 1.869f, 1.274f), sunSideHorizon = C(6.123f, 4.354f, 2.212f),
                zenith = C(0.246f, 0.39f, 0.661f), ground = C(0.626f, 0.636f, 0.68f),
                why = "Alternate: hazier, warmer horizon, few clouds (a calmer evening)." },
            new Sky { name = "Sky_EveningRoad01_2k", sunElevation = 14.57f, sunAzimuth = 39.31f, maxSize = 2048,
                skyAverage = C(1.084f, 1.268f, 1.391f), horizon = C(0.953f, 1.045f, 1.012f), sunSideHorizon = C(1.913f, 1.895f, 1.591f),
                zenith = C(0.338f, 0.557f, 0.843f), ground = C(0.6f, 0.679f, 0.712f),
                why = "Alternate: an hour earlier (sun 14 degrees), a full sky of altocumulus." },
        };

        public static bool TryFind(string name, out Sky sky)
        {
            foreach (var s in Skies)
                if (s.name == name) { sky = s; return true; }
            sky = Skies[0];
            return false;
        }

        public static string HdrPath(string name) => VhLookLibrary.SkyRoot + name + ".hdr";
        public static string MaterialPath(string name) => VhLookLibrary.MatRoot + "VH_" + name + ".mat";

        public static void ConfigureImporters(List<string> changed)
        {
            foreach (var s in Skies)
            {
                string path = HdrPath(s.name);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) { Debug.LogWarning("JAMKIT_LOOK missing sky: " + path); continue; }
                if (ti.textureShape == TextureImporterShape.Texture2D && !ti.mipmapEnabled && ti.maxTextureSize == s.maxSize &&
                    ti.textureCompression == TextureImporterCompression.CompressedHQ && ti.wrapModeV == TextureWrapMode.Clamp) continue;
                ti.textureType = TextureImporterType.Default;
                ti.textureShape = TextureImporterShape.Texture2D;
                ti.mipmapEnabled = false;
                ti.wrapModeU = TextureWrapMode.Repeat;
                ti.wrapModeV = TextureWrapMode.Clamp;
                ti.filterMode = FilterMode.Bilinear;
                ti.maxTextureSize = s.maxSize;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.SaveAndReimport();
                changed.Add(path);
            }
        }

        public static void BuildAll()
        {
            foreach (var s in Skies) Material(s.name);
        }

        /// <summary>The sky's material (built on first use).</summary>
        public static Material Material(string name)
        {
            TryFind(name, out var s);
            string path = MaterialPath(s.name);
            var shader = Shader.Find("JamKit/Look/Sky");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool fresh = m == null;
            if (fresh) m = new Material(shader) { name = "VH_" + s.name };
            else if (m.shader != shader) m.shader = shader;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(HdrPath(s.name));
            if (tex == null)
            {
                VhLookLibrary.ConfigureImporters();
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(HdrPath(s.name));
            }
            m.SetTexture("_SkyTex", tex);
            // Normalise the candidates to about the same brightness (the pick's sky average, luminance ~0.94).
            float lum = s.skyAverage.r * 0.2126f + s.skyAverage.g * 0.7152f + s.skyAverage.b * 0.0722f;
            float exposure = Mathf.Clamp(0.94f / Mathf.Max(lum, 1e-3f), 0.5f, 4f);
            m.SetFloat("_Exposure", exposure);
            m.SetColor("_HorizonColor", s.horizon * exposure);
            m.SetColor("_GroundColor", Color.Lerp(s.ground, new Color(0.46f, 0.43f, 0.4f), 0.5f) * exposure * 0.85f);
            // Pass 2 (round 1: the sun's side was "a flat cream wash", no disc, no glow): a lighter warm lift and a narrower
            // horizon band so the HDRI's gold-lit clouds and rays show, a brighter disc with a tight glow that blooms round
            // foliage. Set here (not only as shader defaults) so existing materials pick them up.
            m.SetColor("_WarmSide", new Color(1.0f, 0.99f, 0.97f, 1f));
            // Pass 4 (round 3: the whole sunward sky sat at the tonemapper's ceiling, so the disc couldn't show, and the zenith read
            // grey): the sky a touch darker and bluer overall (the grade now lifts exposure), and the sun side much dimmer.
            m.SetColor("_Tint", new Color(0.8f, 0.83f, 0.88f, 1f));
            m.SetFloat("_WarmFocus", 4f);
            // Pass 3 (round 2: no sun disc, a cream wash, the clouds sunward lost): the sun side dimmed 18%, a thinner
            // horizon band, a brighter, crisper disc, a tight glow and almost no wide halo (the wash), richer colour.
            m.SetFloat("_SunSideDim", 0.55f);
            m.SetFloat("_HorizonHaze", 0.16f);
            m.SetFloat("_HorizonHeight", 0.03f);
            m.SetFloat("_Saturation", 1.18f);
            m.SetColor("_SunDiscColor", new Color(1f, 0.86f, 0.62f, 1f));
            m.SetFloat("_SunDiscIntensity", 160f);
            m.SetFloat("_SunDiscSize", 0.55f);
            m.SetColor("_SunGlowColor", new Color(1f, 0.76f, 0.46f, 1f));
            m.SetFloat("_SunGlow", 1.4f);
            m.SetFloat("_SunGlowFalloff", 420f);
            m.SetFloat("_SunHalo", 0.04f);
            m.SetFloat("_SunHaloFalloff", 14f);
            if (fresh) { VhLookLibrary.EnsureAssetFolder(path); AssetDatabase.CreateAsset(m, path); }
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>The horizon colour looking away from the sun and towards it, at the sky material's exposure (haze colours).</summary>
        public static void HazeColors(string name, out Color away, out Color towards)
        {
            TryFind(name, out var s);
            float lum = s.skyAverage.r * 0.2126f + s.skyAverage.g * 0.7152f + s.skyAverage.b * 0.0722f;
            float exposure = Mathf.Clamp(0.94f / Mathf.Max(lum, 1e-3f), 0.5f, 4f);
            // Away from the sun the horizon band is cooler than the average horizon: mix it towards the low sky's blue.
            // Pass 3: a little bluer away from the sun (aerial perspective reads cool), the sunward colour a touch dimmer.
            // Pass 4: x the sky material's tint and, sunward, its sun-side dimming, so hazed hills match the sky behind them.
            var tint = new Color(0.8f, 0.83f, 0.88f, 1f);
            away = Color.Lerp(s.horizon, s.skyAverage, 0.6f) * exposure * 0.86f * tint;
            towards = s.sunSideHorizon * exposure * 0.72f * 0.6f * tint;
        }
    }
}

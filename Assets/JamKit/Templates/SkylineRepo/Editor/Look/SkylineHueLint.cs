using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JamKit.SkylineRepo.EditorTools
{
    /// <summary>
    /// The hostile-hue lint (docs/SKYLINE-STYLE.md rule 2: "no scenery ever uses this hue"; docs/SKYLINE-PLAN-FLOOR1.md Task S0,
    /// review focus 11). The hot red-magenta <c>#FF2E63</c> belongs to hostile bots, mines, acid and bolts, so a shop sign, a
    /// wall, a neon or a light in that band would read as an enemy outline. <see cref="Run"/> walks a scene and returns one
    /// problem line per scenery material whose colour or emission hue is within <see cref="BandDegrees"/> of the hostile hue
    /// with saturation above <see cref="MinSaturation"/>, and per light of such a hue. Exempt: the role materials in
    /// <c>Resources/SkylineFx</c>, any material with an ink role above zero (the bots' paint, the weak-spot parts), and
    /// everything under a <see cref="SkylineBot"/> or <see cref="SkylineWreck"/> (their own hostile colour). Called at the end of
    /// <see cref="SkylineInkConvert.ConvertScene"/> as warnings; the pass and the stills judge them.
    /// </summary>
    public static class SkylineHueLint
    {
        public const float BandDegrees = 20f, MinSaturation = 0.4f;
        /// <summary>The hue of <c>#FF2E63</c> in degrees (about 345).</summary>
        public static readonly float HostileHue = HueDegrees(new Color32(0xFF, 0x2E, 0x63, 255));

        /// <summary>The hue of a colour (as entered, sRGB numbers) in degrees, 0..360.</summary>
        public static float HueDegrees(Color c)
        {
            Color.RGBToHSV(c, out float h, out _, out _);
            return h * 360f;
        }

        /// <summary>True when the colour is within <see cref="BandDegrees"/> of the hostile hue and more saturated than
        /// <see cref="MinSaturation"/>. HDR colours (emission above 1) are judged by their hue, so they are scaled down first.</summary>
        public static bool InHostileBand(Color c)
        {
            float peak = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (peak <= 0f) return false;
            if (peak > 1f) c = new Color(c.r / peak, c.g / peak, c.b / peak, c.a);
            Color.RGBToHSV(c, out float h, out float s, out _);
            if (s <= MinSaturation) return false;
            float d = Mathf.Abs(Mathf.DeltaAngle(h * 360f, HostileHue));
            return d <= BandDegrees;
        }

        /// <summary>The problem lines of one material (empty when fine). <paramref name="where"/> names where it is used.</summary>
        public static List<string> CheckMaterial(Material m, string where)
        {
            var lines = new List<string>();
            if (m == null || IsRoleMaterial(m)) return lines;
            string path = AssetDatabase.GetAssetPath(m);
            string id = m.name + (string.IsNullOrEmpty(path) ? "" : " (" + path + ")");
            if (m.HasProperty("_BaseColor") && InHostileBand(m.GetColor("_BaseColor")))
                lines.Add("scenery material " + id + " has a colour in the hostile red-magenta band (" + ColorUtility.ToHtmlStringRGB(m.GetColor("_BaseColor")) + ") on " + where);
            else if (!m.HasProperty("_BaseColor") && m.HasProperty("_Color") && InHostileBand(m.GetColor("_Color")))
                lines.Add("scenery material " + id + " has a colour in the hostile red-magenta band (" + ColorUtility.ToHtmlStringRGB(m.GetColor("_Color")) + ") on " + where);
            if (m.HasProperty("_EmissionColor") && m.IsKeywordEnabled("_EMISSION") && InHostileBand(m.GetColor("_EmissionColor")))
                lines.Add("scenery material " + id + " glows in the hostile red-magenta band (" + ColorUtility.ToHtmlStringRGB(m.GetColor("_EmissionColor")) + ") on " + where);
            return lines;
        }

        /// <summary>The problem lines of one light (empty when fine).</summary>
        public static List<string> CheckLight(Light l, string where)
        {
            var lines = new List<string>();
            if (l != null && InHostileBand(l.color))
                lines.Add("light " + where + " has a colour in the hostile red-magenta band (" + ColorUtility.ToHtmlStringRGB(l.color) + ")");
            return lines;
        }

        /// <summary>True for materials that carry a role on purpose: the build-included role materials and anything with an ink role.</summary>
        public static bool IsRoleMaterial(Material m)
        {
            if (m == null) return false;
            string path = AssetDatabase.GetAssetPath(m);
            if (!string.IsNullOrEmpty(path) && path.Replace('\\', '/').Contains("Resources/SkylineFx/")) return true;
            return SkylineInkRoles.GetRole(m) > SkylineInkRoles.None;
        }

        /// <summary>Walks the scene's renderers and lights and returns the problem lines (empty = clean). Bots, wrecks and what is
        /// under them are skipped.</summary>
        public static List<string> Run(Scene scene)
        {
            var lines = new List<string>();
            if (!scene.IsValid() || !scene.isLoaded) return lines;
            var seen = new HashSet<Material>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (IsHostileOwned(r.transform)) continue;
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null || !seen.Add(m)) continue;     // one line per material, naming the first renderer
                        lines.AddRange(CheckMaterial(m, Describe(r.transform)));
                    }
                }
                foreach (var l in root.GetComponentsInChildren<Light>(true))
                {
                    if (IsHostileOwned(l.transform)) continue;
                    lines.AddRange(CheckLight(l, Describe(l.transform)));
                }
            }
            return lines;
        }

        static bool IsHostileOwned(Transform t) =>
            t.GetComponentInParent<SkylineBot>(true) != null || t.GetComponentInParent<SkylineWreck>(true) != null;

        static string Describe(Transform t)
        {
            string s = t.name;
            for (var p = t.parent; p != null; p = p.parent) s = p.name + "/" + s;
            return s;
        }
    }
}

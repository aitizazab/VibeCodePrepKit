using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.Perf.EditorTools
{
    /// <summary>
    /// Shadow-caster rules (docs/redesign/perf.md "Shadows"). Every caster is drawn once per cascade (4 on the hill) and six
    /// times per shadowed point light, so small things and far things casting is the cheapest frame time to win back.
    /// <code>
    /// VhPerfShadows.Apply(sceneRoot, VhPerfShadows.Rules.Outdoor);     // the hill
    /// VhPerfShadows.Apply(houseRoot, VhPerfShadows.Rules.Indoor);      // rooms, the ward
    /// </code>
    /// </summary>
    public static class VhPerfShadows
    {
        public sealed class Rules
        {
            /// <summary>Renderers whose largest side is under this (m) cast no shadow (cups, books, petals, kerb litter).</summary>
            public float minCasterSize = 0.35f;
            /// <summary>Renderers whose nearest point is farther than this from every play point cast no shadow (0: no
            /// distance rule). Use the shadow distance plus the length of a low sun's shadow.</summary>
            public float maxCasterDistance;
            /// <summary>Cards (grass, flowers) never cast: their shadows are noise at 1080p.</summary>
            public bool cardsCast;
            /// <summary>Names containing this (backdrop scenery) never cast.</summary>
            public string backdropTag = "(backdrop)";

            public static Rules Outdoor => new Rules { minCasterSize = 0.5f, maxCasterDistance = 0f };
            public static Rules Indoor => new Rules { minCasterSize = 0.25f, maxCasterDistance = 0f };
        }

        /// <summary>Applies the rules under <paramref name="root"/>; <paramref name="playPoints"/> (the routes, the rooms)
        /// for the distance rule. Returns how many renderers stopped casting.</summary>
        public static int Apply(Transform root, Rules rules, Vector3[] playPoints = null)
        {
            if (root == null) return 0;
            rules = rules ?? Rules.Outdoor;
            int off = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r.shadowCastingMode == ShadowCastingMode.Off || r.shadowCastingMode == ShadowCastingMode.ShadowsOnly) continue;
                if (ShouldNotCast(r, rules, playPoints)) { r.shadowCastingMode = ShadowCastingMode.Off; off++; }
            }
            return off;
        }

        public static bool ShouldNotCast(Renderer r, Rules rules, Vector3[] playPoints)
        {
            var b = r.bounds;
            float big = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (big < rules.minCasterSize) return true;
            if (!string.IsNullOrEmpty(rules.backdropTag) && InBackdrop(r.transform, rules.backdropTag)) return true;
            if (!rules.cardsCast && r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null && mf.sharedMesh.vertexCount < 400000 && VhPerfMesh.IsCards(mf.sharedMesh)) return true;
            if (rules.maxCasterDistance > 0f && playPoints != null && playPoints.Length > 0)
            {
                float near = float.MaxValue;
                foreach (var p in playPoints) near = Mathf.Min(near, (b.ClosestPoint(p) - p).sqrMagnitude);
                if (near > rules.maxCasterDistance * rules.maxCasterDistance) return true;
            }
            return false;
        }

        static bool InBackdrop(Transform t, string tag)
        {
            for (; t != null; t = t.parent) if (t.name.Contains(tag)) return true;
            return false;
        }

        /// <summary>
        /// Lights: at most <paramref name="maxShadowed"/> point/spot lights under <paramref name="root"/> keep shadows (the
        /// brightest first); the rest go shadowless. Point-light shadows cost six shadow maps each. Returns how many changed.
        /// </summary>
        public static int LimitShadowedLights(Transform root, int maxShadowed = 2)
        {
            if (root == null) return 0;
            var lights = root.GetComponentsInChildren<Light>(true);
            System.Array.Sort(lights, (a, b) => (b.intensity * b.range).CompareTo(a.intensity * a.range));
            int kept = 0, changed = 0;
            foreach (var l in lights)
            {
                if (l.type == LightType.Directional || l.shadows == LightShadows.None) continue;
                if (kept < maxShadowed) { kept++; continue; }
                l.shadows = LightShadows.None;
                changed++;
            }
            return changed;
        }
    }
}

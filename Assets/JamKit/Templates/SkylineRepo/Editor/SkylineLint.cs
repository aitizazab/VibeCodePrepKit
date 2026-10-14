using System;
using System.Collections.Generic;
using JamKit.Movement;
using JamKit.Skyline;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JamKit.SkylineRepo.EditorTools
{
    /// <summary>
    /// The scene lint (docs/SKYLINE-PLAN-M1.md Task 16), run by stage A on the arena. Opens the scene and returns its
    /// problems, one line each naming the object (its hierarchy path):
    /// <list type="number">
    /// <item><c>no collider: &lt;object&gt;</c>: a MeshRenderer with no enabled Collider on it or a parent, unless it or a parent is
    /// named "... (fx)" or "... (backdrop)" or carries <see cref="SkylineNoCollider"/>.</item>
    /// <item><c>static overlap: &lt;a&gt; x &lt;b&gt; (&lt;depth&gt; m)</c>: two static colliders (no rigidbody, not triggers,
    /// not the player's CharacterController, neither the other's parent or child) whose
    /// <see cref="Physics.ComputePenetration"/> depth exceeds 0.02 m. Boxes that only touch (a cover box standing on the floor,
    /// walls meeting edge to edge) have no depth and pass.</item>
    /// <item><c>unmarked wall: runnable or not? &lt;object&gt;</c>: a collider whose world bounds are at least 2 m tall and
    /// 3 m long with no <see cref="MovementSurface"/> (a box collider only when one of its axes is within about 10 degrees
    /// of vertical, so its sides are nearly vertical faces). The marker must be on the collider's own object,
    /// which is where MovementController reads it; one only on a parent is reported as
    /// <c>wall marked on a parent only (the engine reads the collider's own): &lt;object&gt;</c>.</item>
    /// <item><c>untextured: &lt;object&gt; (&lt;material&gt;)</c>: a renderer whose material has no _BaseMap texture, unless the
    /// material's name starts with SK_Emissive or SK_Glass, or is a comic-ink twin (INK_...: flat colour is the style). (Every stand-in surface is flagged until Task 21; expected.)</item>
    /// </list>
    /// </summary>
    public static class SkylineLint
    {
        public const float MaxStaticPenetration = 0.02f;
        const float WallMinHeight = 2f, WallMinLength = 3f;

        public static List<string> Run(string scenePath)
        {
            var problems = new List<string>();
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var renderers = new List<Renderer>();
            var colliders = new List<Collider>();
            foreach (var r in roots)
            {
                renderers.AddRange(r.GetComponentsInChildren<Renderer>(false));
                colliders.AddRange(r.GetComponentsInChildren<Collider>(false));
            }
            Physics.SyncTransforms();

            // 1. renderers without colliders
            foreach (var r in renderers)
            {
                if (!(r is MeshRenderer)) continue;
                if (HasEnabledCollider(r.transform)) continue;
                if (Exempt(r.transform)) continue;
                problems.Add("no collider: " + PathOf(r.transform));
            }

            // 2. static colliders that sink into each other
            var statics = new List<Collider>();
            foreach (var c in colliders)
            {
                if (c == null || !c.enabled || c.isTrigger || c is CharacterController) continue;
                if (c.GetComponentInParent<Rigidbody>() != null) continue;
                if (c is MeshCollider mc && !mc.convex) continue;     // ComputePenetration needs a primitive or convex side
                statics.Add(c);
            }
            for (int i = 0; i < statics.Count; i++)
            {
                var a = statics[i];
                var ba = a.bounds;
                for (int j = i + 1; j < statics.Count; j++)
                {
                    var b = statics[j];
                    if (!ba.Intersects(b.bounds)) continue;
                    if (a.transform.IsChildOf(b.transform) || b.transform.IsChildOf(a.transform)) continue;
                    bool hit;
                    float depth;
                    try
                    {
                        hit = Physics.ComputePenetration(a, a.transform.position, a.transform.rotation,
                            b, b.transform.position, b.transform.rotation, out _, out depth);
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                    if (hit && depth > MaxStaticPenetration)
                        problems.Add("static overlap: " + PathOf(a.transform) + " x " + PathOf(b.transform) + " (" +
                            depth.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " m)");
                }
            }

            // 3. walls with no say on wall-running
            foreach (var c in colliders)
            {
                if (c == null || !c.enabled || c.isTrigger || c is CharacterController) continue;
                var s = c.bounds.size;
                if (s.y < WallMinHeight || Mathf.Max(s.x, s.z) < WallMinLength) continue;
                if (c is BoxCollider && !Upright(c.transform)) continue;   // a tilted box (a ramp, a slab) has no vertical face
                if (c.GetComponent<MovementSurface>() != null) continue;
                if (c.GetComponentInParent<MovementSurface>() != null)
                    problems.Add("wall marked on a parent only (the engine reads the collider's own): " + PathOf(c.transform));
                else problems.Add("unmarked wall: runnable or not? " + PathOf(c.transform));
            }

            // 4. untextured surfaces
            foreach (var r in renderers)
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                var mats = r.sharedMaterials;
                if (mats == null || mats.Length == 0)
                {
                    problems.Add("untextured: " + PathOf(r.transform) + " (none)");
                    continue;
                }
                foreach (var m in mats)
                {
                    string name = m != null ? m.name : "none";
                    if (name.StartsWith("SK_Emissive", StringComparison.Ordinal) || name.StartsWith("SK_Glass", StringComparison.Ordinal)) continue;
                    // comic ink (docs/SKYLINE-STYLE.md): flat painted colour is the style, so an InkLit twin is untextured on purpose
                    if (name.StartsWith("INK_", StringComparison.Ordinal)) continue;
                    bool textured = m != null && m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null;
                    if (!textured) problems.Add("untextured: " + PathOf(r.transform) + " (" + name + ")");
                }
            }

            // 5. floor 1 only: the scene is exactly the floor data (Task B1)
            if (string.Equals(scenePath.Replace('\\', '/'), SkylineScenes.Floor1, StringComparison.Ordinal))
                problems.AddRange(FloorDataMatch(colliders));
            return problems;
        }

        /// <summary>
        /// The <c>FloorDataMatch</c> rule (docs/SKYLINE-PLAN-FLOOR1.md Task B1): every <see cref="FloorSolid"/> of
        /// <see cref="RepossessArena"/> (and of <see cref="HotelLobby"/> once any collider stands south of the Food Court, i.e.
        /// B2's lobby is built) has exactly one enabled solid collider with equal bounds (to a centimetre), an equal
        /// <see cref="MovementSurface"/> runnable flag, and glass on the glass layer; and every other enabled solid collider is
        /// reported as <c>no solid for collider</c>. Not counted: triggers, the player's, rigidbodies' (bots, debris), anything
        /// under an object named "... (fx)", "... (backdrop)", "... (extra)" or carrying <see cref="SkylineNoCollider"/>. A collider
        /// a later task adds on purpose (a door's holding collider) is named "... (extra)". Lines start with <c>FloorDataMatch:</c>.
        /// </summary>
        public static List<string> FloorDataMatch(IEnumerable<Collider> colliders)
        {
            int glassLayer = LayerMask.NameToLayer(SkylineArenaBuilder.FloorGlassLayer);
            var placed = new List<PlacedCollider>();
            foreach (var c in colliders)
            {
                if (c == null || !c.enabled || c.isTrigger || c is CharacterController) continue;
                if (c.GetComponentInParent<Rigidbody>() != null || c.GetComponentInParent<SkylinePlayer>() != null) continue;
                if (Exempt(c.transform) || ExtraByName(c.transform)) continue;
                var b = c.bounds;
                var ms = c.GetComponent<MovementSurface>();
                placed.Add(new PlacedCollider
                {
                    path = PathOf(c.transform),
                    min = new System.Numerics.Vector3(b.min.x, b.min.y, b.min.z),
                    max = new System.Numerics.Vector3(b.max.x, b.max.y, b.max.z),
                    runnable = ms != null && ms.runnable,
                    glass = glassLayer >= 0 && c.gameObject.layer == glassLayer,
                });
            }
            var solids = new List<FloorSolid>(RepossessArena.Solids());
            bool lobby = false;
            foreach (var p in placed) if ((p.min.Z + p.max.Z) * 0.5f < HotelLobby.MaxZ) { lobby = true; break; }
            if (lobby) solids.AddRange(HotelLobby.Solids());
            else Debug.Log("SKYLINE_LINT FloorDataMatch: no collider south of the Food Court, so the hotel lobby (B2) is not checked");
            return JamKit.Skyline.FloorDataMatch.Match(solids, placed);
        }

        static bool ExtraByName(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
                if (p.name.EndsWith("(extra)", StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>An enabled collider on <paramref name="t"/> or a parent (a disabled one doesn't count).</summary>
        static bool HasEnabledCollider(Transform t)
        {
            foreach (var c in t.GetComponentsInParent<Collider>(false)) if (c.enabled) return true;
            return false;
        }

        /// <summary>One of the box's own axes is within about 10 degrees of world up, so its side faces are nearly vertical.</summary>
        static bool Upright(Transform t)
        {
            const float Cos10 = 0.985f;
            return Mathf.Abs(Vector3.Dot(t.up, Vector3.up)) > Cos10 || Mathf.Abs(Vector3.Dot(t.right, Vector3.up)) > Cos10
                || Mathf.Abs(Vector3.Dot(t.forward, Vector3.up)) > Cos10;
        }

        static bool Exempt(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
            {
                if (p.GetComponent<SkylineNoCollider>() != null) return true;
                string n = p.name;
                if (n.EndsWith("(fx)", StringComparison.Ordinal) || n.EndsWith("(backdrop)", StringComparison.Ordinal)) return true;
            }
            return false;
        }

        static string PathOf(Transform t)
        {
            string s = t.name;
            for (var p = t.parent; p != null; p = p.parent) s = p.name + "/" + s;
            return s;
        }
    }
}

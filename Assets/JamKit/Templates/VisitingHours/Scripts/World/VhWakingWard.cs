using System.Collections.Generic;
using UnityEngine;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// St. Brigid's, Ward 4 West, and the 11 PM booth (docs/redesign/day.md): built in code when a day starts. This file is
    /// the kit (the palette, PBR surfaces from <see cref="VhWardLibrary"/>, walls with doorways and windows, lights, the
    /// shared props) and the booth; <c>VhWardFloor.cs</c> lays out the floor: the lift lobby, the family room, the corridor
    /// with rooms 11 to 16 and its sunlit west window, the nurses' station, the staff room, the quiet room, stairwell B.
    /// Every surface is a textured PBR material (URP/Lit: albedo, normal, mask) when the library is built; without it the
    /// style-E colours stand in (and it's logged). People are <see cref="VhWardPeople"/>: the People agent's prefabs when
    /// they exist, stand-ins until then.
    /// </summary>
    public static partial class VhWakingWard
    {
        // The ward's palette (the colours each surface comes out at, on average; the textures carry the detail).
        static readonly Color WallPaint = new Color(0.78f, 0.80f, 0.76f);
        static readonly Color WallSage = new Color(0.66f, 0.72f, 0.66f);
        static readonly Color WallRoom = new Color(0.80f, 0.77f, 0.70f);
        static readonly Color WallWarm = new Color(0.82f, 0.74f, 0.62f);
        static readonly Color Skirting = new Color(0.36f, 0.40f, 0.42f);
        static readonly Color Vinyl = new Color(0.66f, 0.68f, 0.66f);
        static readonly Color VinylRoom = new Color(0.62f, 0.60f, 0.55f);
        static readonly Color CeilingTile = new Color(0.86f, 0.86f, 0.84f);
        static readonly Color CeilingGrid = new Color(0.78f, 0.78f, 0.76f);
        static readonly Color DoorBlue = new Color(0.42f, 0.52f, 0.58f);
        static readonly Color DoorFrame = new Color(0.80f, 0.80f, 0.78f);
        static readonly Color Glass = new Color(0.16f, 0.2f, 0.22f);
        static readonly Color Laminate = new Color(0.62f, 0.50f, 0.38f);
        static readonly Color Worktop = new Color(0.82f, 0.81f, 0.77f);
        static readonly Color Plastic = new Color(0.86f, 0.86f, 0.84f);
        static readonly Color PlasticGrey = new Color(0.55f, 0.57f, 0.59f);
        static readonly Color BedFrame = new Color(0.82f, 0.81f, 0.77f);
        static readonly Color HospitalBlanket = new Color(0.52f, 0.62f, 0.72f);
        static readonly Color Gown = new Color(0.66f, 0.74f, 0.80f);
        static readonly Color Lily = new Color(0.94f, 0.93f, 0.88f);
        static readonly Color Stem = new Color(0.30f, 0.45f, 0.30f);
        static readonly Color SharpsYellow = new Color(0.86f, 0.74f, 0.22f);
        static readonly Color ExtinguisherRed = new Color(0.68f, 0.14f, 0.12f);
        static readonly Color Cork = new Color(0.62f, 0.48f, 0.34f);
        static readonly Color LinenBlue = new Color(0.32f, 0.44f, 0.62f);
        static readonly Color ChairBlue = new Color(0.30f, 0.42f, 0.50f);
        static readonly Color ChairTeal = new Color(0.24f, 0.46f, 0.46f);
        static readonly Color CarpetBlue = new Color(0.30f, 0.34f, 0.42f);
        static readonly Color CarpetWarm = new Color(0.44f, 0.34f, 0.30f);
        static readonly Color Steel = new Color(0.62f, 0.64f, 0.66f);
        static readonly Color Shoe = new Color(0.16f, 0.16f, 0.17f);

        /// <summary>Which PBR surface each palette colour is (nearest colour wins; unknown colours are painted plastic).</summary>
        static readonly KeyValuePair<Color, string>[] Surfaces =
        {
            new KeyValuePair<Color, string>(WallPaint, "wall"), new KeyValuePair<Color, string>(WallSage, "wall"),
            new KeyValuePair<Color, string>(WallRoom, "wall"), new KeyValuePair<Color, string>(WallWarm, "wall"),
            new KeyValuePair<Color, string>(Vinyl, "floor"), new KeyValuePair<Color, string>(VinylRoom, "floor"),
            new KeyValuePair<Color, string>(CeilingTile, "ceiling"), new KeyValuePair<Color, string>(Glass, "glass"),
            new KeyValuePair<Color, string>(Laminate, "wood"), new KeyValuePair<Color, string>(VhWakingStyle.WoodDark, "wood"),
            new KeyValuePair<Color, string>(VhWakingStyle.Wood, "wood"), new KeyValuePair<Color, string>(HospitalBlanket, "fabric"),
            new KeyValuePair<Color, string>(Gown, "linen"), new KeyValuePair<Color, string>(VhWakingStyle.Sheet, "linen"),
            new KeyValuePair<Color, string>(VhWakingStyle.Pillow, "linen"), new KeyValuePair<Color, string>(VhWakingStyle.Paper, "linen"),
            new KeyValuePair<Color, string>(Lily, "linen"), new KeyValuePair<Color, string>(Stem, "fabric"),
            new KeyValuePair<Color, string>(Cork, "carpet"), new KeyValuePair<Color, string>(LinenBlue, "fabric"),
            new KeyValuePair<Color, string>(ChairBlue, "fabric"), new KeyValuePair<Color, string>(ChairTeal, "fabric"),
            new KeyValuePair<Color, string>(CarpetBlue, "carpet"), new KeyValuePair<Color, string>(CarpetWarm, "carpet"),
            new KeyValuePair<Color, string>(VhWakingStyle.Metal, "metal"), new KeyValuePair<Color, string>(VhWakingStyle.MetalDark, "metal"),
            new KeyValuePair<Color, string>(Steel, "metal"), new KeyValuePair<Color, string>(VhWakingStyle.Skin, "skin"),
            new KeyValuePair<Color, string>(VhWakingStyle.Lanyard, "fabric"), new KeyValuePair<Color, string>(VhWakingStyle.Lampshade, "linen"),
            new KeyValuePair<Color, string>(VhWakingStyle.Plant, "fabric"),
        };

        static string SurfaceFor(Color c)
        {
            string best = "plastic";
            float bestD = 0.06f;
            foreach (var kv in Surfaces)
            {
                float dr = c.r - kv.Key.r, dg = c.g - kv.Key.g, db = c.b - kv.Key.b;
                float d = dr * dr + dg * dg + db * db;
                if (d < bestD) { bestD = d; best = kv.Value; }
            }
            return best;
        }

        sealed class Kit
        {
            public VhWakingStyle style;
            public VhMeshKit meshes;
            public VhWardLibrary lib;
            public readonly List<Material> mats = new List<Material>();
            /// <summary>Where the parts being built go: the place's root, or the room being built (VhWardFloor.Room).</summary>
            public Transform root;
            /// <summary>The place's root (it owns the materials and meshes).</summary>
            public Transform place;
            public VhWardRooms rooms;
            public VisitingHoursConfig config;
            public VhArea area;
            public VisitingHoursBoot boot;
            public bool day2;
        }

        /// <summary>Frees the place's materials, meshes and pictures when it's taken down.</summary>
        sealed class Owned : MonoBehaviour
        {
            public VhWakingStyle style;
            public VhMeshKit meshes;
            public List<Material> mats;
            public VhWardLibrary lib;
            public readonly List<Object> extra = new List<Object>();

            void OnDestroy()
            {
                style?.Dispose();
                meshes?.Dispose();
                if (mats != null) foreach (var m in mats) VhUtil.Kill(m);
                foreach (var o in extra) VhUtil.Kill(o);
                if (lib != null) lib.ForgetTints();
            }
        }

        static Kit Begin(VhArea a, VisitingHoursConfig c, bool day2)
        {
            var boot = VhBoot.Config;
            var k = new Kit
            {
                style = new VhWakingStyle(boot != null ? boot.wakingMaterial : null), meshes = new VhMeshKit(), root = a.root,
                place = a.root, config = c, area = a, boot = boot, lib = VhWardLibrary.Load(), day2 = day2,
            };
            var owned = a.root.gameObject.AddComponent<Owned>();
            owned.style = k.style;
            owned.meshes = k.meshes;
            owned.mats = k.mats;
            owned.lib = k.lib;
            return k;
        }

        static void Own(Kit k, Object o)
        {
            var owned = k.place.GetComponent<Owned>();
            if (owned != null) owned.extra.Add(o);
        }

        // ---------- Materials ----------

        /// <summary>The surface for a colour: the PBR material (its texture, coming out about this colour) or the style-E colour.</summary>
        static Material M(Kit k, Color c, string surface = null)
        {
            if (k.lib != null)
            {
                var m = k.lib.Colour(surface ?? SurfaceFor(c), c, k.mats);
                if (m != null) return m;
            }
            return k.style.Mat(c);
        }

        // ---------- Building blocks ----------

        static MeshRenderer Box(Kit k, string name, Vector3 center, Vector3 size, Color color, bool solid = false, float round = 0f, Quaternion? rot = null, string surface = null)
        {
            var mesh = round > 0f ? k.meshes.RoundedBox(size, round, 3) : k.meshes.Box(size);
            var r = VhMeshKit.Show(k.root, name, mesh, M(k, color, surface), center, rot ?? Quaternion.identity);
            // The judges' "mismatched seams" (judge/look-2.md): walls, floors and the ceiling are built from many boxes, each
            // with its texture starting at its own corner. Their UVs come from the place's own frame instead, so the plaster,
            // the floor and the tiles run on across every joint.
            if (round <= 0f && IsShell(surface ?? SurfaceFor(color))) VhMeshKit.PlaceUv(mesh, r.transform, k.place);
            if (solid) r.gameObject.AddComponent<BoxCollider>().size = size;
            return r;
        }

        static bool IsShell(string surface) =>
            surface == "wall" || surface == "floor" || surface == "ceiling" || surface == "carpet" || surface == "terrazzo";

        /// <summary>A picture on a quad (the chart's page, a photo, a notice): the art's own colours.</summary>
        static MeshRenderer Picture(Kit k, Transform parent, string name, Texture tex, Color fallback, float w, float h, Vector3 at, Quaternion rot)
        {
            var mat = tex != null ? k.style.Mat(Color.white, tex, 0f) : M(k, fallback, "linen");
            return VhMeshKit.Show(parent != null ? parent : k.root, name, k.meshes.Quad(w, h), mat, at, rot, false);
        }

        static MeshRenderer Part(Kit k, Transform parent, string name, Mesh mesh, Color color, Vector3 at, Quaternion rot, bool shadows = true, string surface = null) =>
            VhMeshKit.Show(parent, name, mesh, M(k, color, surface), at, rot, shadows);

        static MeshRenderer Glow(Kit k, string name, Mesh mesh, Color body, Color emission, Vector3 at, Quaternion rot, Transform parent = null)
        {
            var m = k.style.Unique(body, null, "VH " + name);
            VhWakingStyle.SetEmission(m, emission);
            return VhMeshKit.Show(parent != null ? parent : k.root, name, mesh, m, at, rot, false);
        }

        static Quaternion Facing(Vector3 dir) => Quaternion.LookRotation(dir, Vector3.up);

        /// <summary>A plain wall from (x0,z0) to (x1,z1): the paint, a vinyl coving at the foot (hospital floors turn up the
        /// wall) and a collider.</summary>
        static void Wall(Kit k, float x0, float z0, float x1, float z1, float h, Color paint, float thick = 0.12f)
        {
            var a = new Vector3(x0, 0f, z0);
            var b = new Vector3(x1, 0f, z1);
            float len = Vector3.Distance(a, b);
            if (len < 0.01f) return;
            var mid = (a + b) * 0.5f;
            var rot = Facing((b - a).normalized);
            Box(k, "Wall", mid + Vector3.up * (h * 0.5f), new Vector3(thick, h, len), paint, true, 0f, rot, "wall");
            Box(k, "Coving", mid + Vector3.up * 0.06f, new Vector3(thick + 0.03f, 0.12f, len + 0.02f), Skirting, false, 0f, rot, "plastic");
        }

        /// <summary>An opening in a wall run, along the wall's length from its start (metres): from <c>a</c> to <c>b</c>, its
        /// bottom at <c>sill</c> and top at <c>head</c> (a doorway: sill 0, head 2.1).</summary>
        struct Gap
        {
            public float a, b, sill, head;
            public Gap(float a, float b, float sill, float head) { this.a = a; this.b = b; this.sill = sill; this.head = head; }
        }

        static Gap Doorway(float a, float width) => new Gap(a, a + width, 0f, 2.1f);
        static Gap Window(float a, float width, float sill = 0.9f, float head = 2.3f) => new Gap(a, a + width, sill, head);

        /// <summary>A wall from (x0,z0) to (x1,z1) with openings (doorways and windows): solid pieces round each opening, a
        /// frame on each, a sill, mullions and a transom on the windows (their glazing is open air: the sky shows through and
        /// the sun comes in; an invisible pane keeps her inside).</summary>
        static void WallRun(Kit k, float x0, float z0, float x1, float z1, float h, Color paint, params Gap[] gaps)
        {
            var a = new Vector3(x0, 0f, z0);
            var b = new Vector3(x1, 0f, z1);
            float len = Vector3.Distance(a, b);
            var dir = (b - a) / Mathf.Max(0.001f, len);
            System.Array.Sort(gaps, (p, q) => p.a.CompareTo(q.a));
            float at = 0f;
            var rot = Facing(dir);
            foreach (var g in gaps)
            {
                if (g.a > at + 0.01f) Segment(k, a + dir * at, a + dir * g.a, 0f, h, paint, true);
                if (g.sill > 0.01f) Segment(k, a + dir * g.a, a + dir * g.b, 0f, g.sill, paint, true);
                if (g.head < h - 0.01f) Segment(k, a + dir * g.a, a + dir * g.b, g.head, h, paint, false);
                bool window = g.sill > 0.05f;
                var c0 = a + dir * g.a;
                var c1 = a + dir * g.b;
                float w = g.b - g.a;
                var mid = (c0 + c1) * 0.5f;
                Box(k, "Frame", c0 + dir * 0.03f + Vector3.up * ((g.sill + g.head) * 0.5f), new Vector3(0.16f, g.head - g.sill, 0.06f), DoorFrame, false, 0f, rot, "plastic");
                Box(k, "Frame", c1 - dir * 0.03f + Vector3.up * ((g.sill + g.head) * 0.5f), new Vector3(0.16f, g.head - g.sill, 0.06f), DoorFrame, false, 0f, rot, "plastic");
                Box(k, "Frame (head)", mid + Vector3.up * (g.head - 0.03f), new Vector3(0.16f, 0.06f, w), DoorFrame, false, 0f, rot, "plastic");
                if (window)
                {
                    Box(k, "Sill", mid + Vector3.up * (g.sill + 0.02f), new Vector3(0.3f, 0.04f, w + 0.08f), Worktop, false, 0.008f, rot, "plastic");
                    int n = Mathf.Max(1, Mathf.RoundToInt(w / 1.1f));
                    for (int i = 1; i < n; i++)
                        Box(k, "Mullion", c0 + dir * (w * i / n) + Vector3.up * ((g.sill + g.head) * 0.5f), new Vector3(0.08f, g.head - g.sill, 0.05f), PlasticGrey, false, 0f, rot, "metal");
                    Box(k, "Transom", mid + Vector3.up * (g.head - 0.35f), new Vector3(0.08f, 0.05f, w), PlasticGrey, false, 0f, rot, "metal");
                    var pane = new GameObject("Window pane (collider)");
                    pane.transform.SetParent(k.root, false);
                    pane.transform.localPosition = mid + Vector3.up * ((g.sill + g.head) * 0.5f);
                    pane.transform.localRotation = rot;
                    pane.AddComponent<BoxCollider>().size = new Vector3(0.1f, g.head - g.sill, w);
                }
                at = g.b;
            }
            if (at < len - 0.01f) Segment(k, a + dir * at, b, 0f, h, paint, true);
        }

        static void Segment(Kit k, Vector3 from, Vector3 to, float y0, float y1, Color paint, bool coving)
        {
            float len = Vector3.Distance(from, to);
            if (len < 0.01f || y1 - y0 < 0.01f) return;
            var mid = (from + to) * 0.5f;
            var rot = Facing((to - from).normalized);
            Box(k, "Wall", mid + Vector3.up * ((y0 + y1) * 0.5f), new Vector3(0.12f, y1 - y0, len), paint, true, 0f, rot, "wall");
            if (coving && y0 < 0.01f) Box(k, "Coving", mid + Vector3.up * 0.06f, new Vector3(0.15f, 0.12f, len + 0.02f), Skirting, false, 0f, rot, "plastic");
        }

        /// <summary>A handrail along a wall (on brackets, a hand off the wall), as hospital corridors have.</summary>
        static void Handrail(Kit k, Vector3 from, Vector3 to, Vector3 off)
        {
            var path = new List<Vector3> { from + off + Vector3.up * 0.9f, to + off + Vector3.up * 0.9f };
            VhMeshKit.Show(k.root, "Handrail", k.meshes.Tube(path, 0.025f, 10), M(k, Laminate, "wood"), Vector3.zero, Quaternion.identity);
            float len = Vector3.Distance(from, to);
            int n = Mathf.Max(2, Mathf.CeilToInt(len / 1.5f) + 1);
            var size = new Vector3(Mathf.Abs(off.x) > 0f ? Mathf.Abs(off.x) : 0.03f, 0.04f, Mathf.Abs(off.z) > 0f ? Mathf.Abs(off.z) : 0.03f);
            for (int i = 0; i < n; i++)
                Box(k, "Rail bracket", Vector3.Lerp(from, to, i / (float)(n - 1)) + Vector3.up * 0.87f + off * 0.5f, size, Steel, false, 0f, null, "metal");
        }

        /// <summary>A shut door on a wall's face (<paramref name="center"/> on the face at the floor, <paramref name="normal"/>
        /// out of the wall towards the viewer): the frame, the leaf, a vision panel, the lever, a kick plate, a number plate,
        /// and hand sanitiser beside it.</summary>
        static void Door(Kit k, Vector3 center, Vector3 normal, float width, Color leaf, string number = null)
        {
            var right = Vector3.Cross(Vector3.up, -normal).normalized;
            var g = new GameObject("Door" + (number != null ? " " + number : "")).transform;
            g.SetParent(k.root, false);
            g.localPosition = center + normal * 0.065f;
            g.localRotation = Facing(-normal);
            float w = width, h = 2.1f;
            Part(k, g, "Frame (left)", k.meshes.Box(new Vector3(0.06f, h + 0.06f, 0.04f)), DoorFrame, new Vector3(-w * 0.5f - 0.03f, (h + 0.06f) * 0.5f, 0f), Quaternion.identity, true, "plastic");
            Part(k, g, "Frame (right)", k.meshes.Box(new Vector3(0.06f, h + 0.06f, 0.04f)), DoorFrame, new Vector3(w * 0.5f + 0.03f, (h + 0.06f) * 0.5f, 0f), Quaternion.identity, true, "plastic");
            Part(k, g, "Frame (top)", k.meshes.Box(new Vector3(w + 0.12f, 0.06f, 0.04f)), DoorFrame, new Vector3(0f, h + 0.03f, 0f), Quaternion.identity, true, "plastic");
            Part(k, g, "Leaf", k.meshes.Box(new Vector3(w, h, 0.03f)), leaf, new Vector3(0f, h * 0.5f, 0.005f), Quaternion.identity, true, "wood");
            Part(k, g, "Vision panel", k.meshes.Box(new Vector3(0.16f, 0.6f, 0.035f)), Glass, new Vector3(w * 0.22f, 1.45f, 0f), Quaternion.identity, false, "glass");
            Part(k, g, "Kick plate", k.meshes.Box(new Vector3(w - 0.04f, 0.22f, 0.036f)), Steel, new Vector3(0f, 0.13f, 0f), Quaternion.identity, false, "metal");
            Part(k, g, "Lever", k.meshes.Box(new Vector3(0.13f, 0.022f, 0.022f)), Steel, new Vector3(-w * 0.5f + 0.13f, 1.02f, -0.045f), Quaternion.identity, false, "metal");
            Part(k, g, "Lever rose", k.meshes.Cylinder(0.025f, 0.025f, 0.012f, 12), Steel, new Vector3(-w * 0.5f + 0.08f, 1.02f, -0.024f), Quaternion.Euler(90f, 0f, 0f), false, "metal");
            if (number != null)
            {
                Part(k, g, "Number plate", k.meshes.Box(new Vector3(0.2f, 0.12f, 0.015f)), Plastic, new Vector3(w * 0.5f + 0.24f, 1.6f, -0.01f), Quaternion.identity, false, "plastic");
                Sign(k, g, "sign_" + number, new Vector3(w * 0.5f + 0.24f, 1.6f, -0.019f), Quaternion.identity, 0.17f, 0.1f, Plastic);
            }
            Sanitiser(k, center + right * (w * 0.5f + 0.3f) + Vector3.up * 1.25f, normal);
        }

        /// <summary>A door leaf standing open against the wall beside a doorway (hinged at <paramref name="hinge"/>, lying along
        /// <paramref name="alongWall"/>, a hand off the wall towards <paramref name="intoRoom"/>).</summary>
        static void OpenLeaf(Kit k, Vector3 hinge, Vector3 alongWall, Vector3 intoRoom, float width, Color leaf)
        {
            var center = hinge + alongWall * (width * 0.5f) + intoRoom * 0.08f + Vector3.up * 1.05f;
            Box(k, "Door (open)", center, new Vector3(0.04f, 2.1f, width), leaf, true, 0f, Facing(alongWall), "wood");
            Box(k, "Kick plate", center + intoRoom * 0.022f - Vector3.up * 0.92f, new Vector3(0.006f, 0.22f, width - 0.04f), Steel, false, 0f, Facing(alongWall), "metal");
        }

        static void Sanitiser(Kit k, Vector3 wallPoint, Vector3 normal)
        {
            var rot = Facing(-normal);
            Box(k, "Hand sanitiser", wallPoint + normal * 0.11f, new Vector3(0.12f, 0.24f, 0.09f), Plastic, false, 0.02f, rot, "plastic");
            Box(k, "Sanitiser window", wallPoint + normal * 0.157f + Vector3.up * 0.04f, new Vector3(0.05f, 0.08f, 0.004f), new Color(0.6f, 0.75f, 0.8f), false, 0f, rot, "glass");
            Box(k, "Sanitiser lever", wallPoint + normal * 0.13f - Vector3.up * 0.1f, new Vector3(0.09f, 0.03f, 0.06f), PlasticGrey, false, 0.01f, rot, "plastic");
        }

        /// <summary>A printed picture from the library (a sign, a notice, the board), on a quad facing <paramref name="rot"/>'s
        /// -Z. Without the library: a plain plate (logged once by the library).</summary>
        static void Sign(Kit k, Transform parent, string picture, Vector3 at, Quaternion rot, float w, float h, Color plate)
        {
            var tex = k.lib != null ? k.lib.Picture(picture) : null;
            var mat = tex != null ? k.style.Mat(Color.white, tex, 0f) : M(k, plate, "plastic");
            VhMeshKit.Show(parent != null ? parent : k.root, "Picture: " + picture, k.meshes.Quad(w, h), mat, at, rot, false);
        }

        /// <summary>A sign on a wall: a plate and its picture, on the wall's face at <paramref name="at"/> looking along <paramref name="normal"/>.</summary>
        static void WallSign(Kit k, string picture, Vector3 at, Vector3 normal, float w, float h, Color plate)
        {
            Box(k, "Sign plate", at + normal * 0.008f, new Vector3(w + 0.02f, h + 0.02f, 0.012f), plate, false, 0.003f, Facing(-normal), "plastic");
            Sign(k, null, picture, at + normal * 0.016f, Facing(-normal), w, h, plate);
        }

        static void AddLight(Kit k, string name, Vector3 at, Color color, float intensity, float range, bool shadows = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(k.root, false);
            go.transform.localPosition = at;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            // On the ward the lights that may cast shadows take turns: only the nearest two do (perf.md, Day item 1).
            if (shadows && k.rooms != null) k.rooms.AddShadowCandidate(l);
        }

        /// <summary>A recessed ceiling panel: the diffuser glowing, and the light it gives.</summary>
        static void CeilingLight(Kit k, Vector3 at, Vector2 size, Color light, float intensity, float range, bool shadows, float glow = 1.1f)
        {
            Box(k, "Light housing", at + Vector3.up * 0.01f, new Vector3(size.x + 0.06f, 0.03f, size.y + 0.06f), CeilingGrid, false, 0f, null, "metal");
            Glow(k, "Light diffuser", k.meshes.Box(new Vector3(size.x, 0.02f, size.y)), new Color(0.9f, 0.92f, 0.9f), new Color(0.96f, 0.97f, 0.92f) * glow, at - Vector3.up * 0.005f, Quaternion.identity);
            AddLight(k, "Ceiling light", at - Vector3.up * 0.35f, light, intensity, range, shadows);
        }

        /// <summary>A suspended ceiling: the tiles (their texture carries the grid).</summary>
        static void SuspendedCeiling(Kit k, float x0, float x1, float z0, float z1, float y)
        {
            Box(k, "Ceiling tiles", new Vector3((x0 + x1) * 0.5f, y + 0.02f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 0.04f, z1 - z0), CeilingTile, false, 0f, null, "ceiling");
        }

        static void Floor(Kit k, string name, float x0, float x1, float z0, float z1, Color c, string surface = "floor")
        {
            Box(k, name, new Vector3((x0 + x1) * 0.5f, -0.05f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 0.1f, z1 - z0), c, true, 0f, null, surface);
        }

        static VhSpot Spot(Kit k, string id, Vector3 at, float radius)
        {
            var def = k.config.FindExaminable(id);
            if (def == null) return null;
            var s = new VhSpot { def = def, position = at, radius = radius };
            k.area.spots.Add(s);
            return s;
        }

        /// <summary>
        /// One of the boot asset's models (Kenney's furniture), on PBR surfaces by its material names: scaled so its height
        /// (or with <paramref name="byWidth"/> its widest side) is <paramref name="size"/>, standing on <paramref name="floor"/>,
        /// its front (+Z in the kit) turned <paramref name="yaw"/> degrees. A box collider round it when <paramref name="solid"/>.
        /// Null when the model isn't in the build (the caller builds its own).
        /// </summary>
        static GameObject Model(Kit k, string name, Vector3 floor, float yaw, float size, bool byWidth = false, bool solid = true)
        {
            var prefab = k.boot != null ? k.boot.Model(name) : null;
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, k.root);
            go.name = name;
            foreach (var an in go.GetComponentsInChildren<Animator>(true)) an.enabled = false;
            Restyle(k, go);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            var b = Bounds(go);
            float have = byWidth ? Mathf.Max(b.size.x, b.size.z) : b.size.y;
            if (have > 1e-4f) go.transform.localScale = Vector3.one * (size / have);
            b = Bounds(go);
            var target = k.root.TransformPoint(floor);
            go.transform.position += target - new Vector3(b.center.x, b.min.y, b.center.z);
            go.transform.RotateAround(target, Vector3.up, yaw);
            if (solid)
            {
                b = Bounds(go);
                var col = new GameObject(name + " (collider)");
                col.transform.SetParent(k.root, false);
                col.transform.position = b.center;
                col.AddComponent<BoxCollider>().size = b.size;
            }
            return go;
        }

        /// <summary>A Kenney model onto the ward's PBR surfaces, by its material names (wood, metal, carpet...).</summary>
        static void Restyle(Kit k, GameObject root)
        {
            if (k.lib == null)
            {
                k.style.Restyle(root);
                return;
            }
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var shared = r.sharedMaterials;
                var next = new Material[shared.Length];
                for (int i = 0; i < shared.Length; i++)
                {
                    string n = shared[i] != null ? shared[i].name : "";
                    string surface = n.StartsWith("metal") ? "metal" : n.StartsWith("carpet") ? "fabric" : n.StartsWith("lamp") ? "linen" : n.StartsWith("plant") ? "fabric" : "wood";
                    if (!VhWakingStyle.PaletteFor(n, out Color c)) c = VhWakingStyle.WoodPale;
                    next[i] = M(k, c, surface);
                }
                r.sharedMaterials = next;
            }
        }

        static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        /// <summary>A wall clock: the rim, the face, the ticks, the hands at <paramref name="hours"/> (12-hour, decimal).</summary>
        static void Clock(Kit k, Vector3 wallPoint, Vector3 normal, float hours)
        {
            var g = new GameObject("Wall clock").transform;
            g.SetParent(k.root, false);
            g.localPosition = wallPoint + normal * 0.03f;
            g.localRotation = Facing(-normal);
            Part(k, g, "Rim", k.meshes.Cylinder(0.16f, 0.16f, 0.04f, 32), new Color(0.2f, 0.21f, 0.23f), new Vector3(0f, 0f, -0.02f), Quaternion.Euler(90f, 0f, 0f), true, "plastic");
            Part(k, g, "Face", k.meshes.Disc(0.145f, 32), new Color(0.94f, 0.93f, 0.9f), new Vector3(0f, 0f, -0.021f), Quaternion.Euler(-90f, 0f, 0f), false, "plastic");
            for (int i = 0; i < 12; i++)
            {
                var r = Quaternion.Euler(0f, 0f, i * 30f);
                Part(k, g, "Tick", k.meshes.Box(new Vector3(0.008f, 0.022f, 0.004f)), Shoe, r * new Vector3(0f, 0.125f, 0f) + new Vector3(0f, 0f, -0.024f), r, false, "plastic_dark");
            }
            float hourA = (hours % 12f) * 30f, minA = (hours * 60f % 60f) * 6f;
            var hr = Quaternion.Euler(0f, 0f, hourA);
            var mr = Quaternion.Euler(0f, 0f, minA);
            Part(k, g, "Hour hand", k.meshes.Box(new Vector3(0.012f, 0.075f, 0.004f)), Shoe, hr * new Vector3(0f, 0.032f, 0f) + new Vector3(0f, 0f, -0.028f), hr, false, "plastic_dark");
            Part(k, g, "Minute hand", k.meshes.Box(new Vector3(0.008f, 0.11f, 0.004f)), Shoe, mr * new Vector3(0f, 0.05f, 0f) + new Vector3(0f, 0f, -0.031f), mr, false, "plastic_dark");
        }

        // ---------- Shared props ----------

        /// <summary>A hospital bed centred at <paramref name="at"/>, its head towards local +Z turned <paramref name="yaw"/>:
        /// castors, the frame, the head and foot ends, the side rails, the mattress, the pillow. <paramref name="raised"/>:
        /// the backrest up (a patient sitting up).</summary>
        static Transform Bed(Kit k, Vector3 at, float yaw = 0f, bool raised = false)
        {
            var g = new GameObject("Bed").transform;
            g.SetParent(k.root, false);
            g.localPosition = at;
            g.localRotation = Quaternion.Euler(0f, yaw, 0f);
            float hw = 0.48f, hl = 1.05f;
            foreach (var o in new[] { new Vector3(-hw + 0.06f, 0f, -hl + 0.08f), new Vector3(hw - 0.06f, 0f, -hl + 0.08f), new Vector3(-hw + 0.06f, 0f, hl - 0.08f), new Vector3(hw - 0.06f, 0f, hl - 0.08f) })
            {
                Part(k, g, "Castor", k.meshes.Cylinder(0.05f, 0.05f, 0.04f, 12), Shoe, o + new Vector3(-0.02f, 0.05f, 0f), Quaternion.Euler(0f, 0f, 90f), true, "plastic_dark");
                Part(k, g, "Bed leg", k.meshes.Cylinder(0.025f, 0.025f, 0.3f, 8), Steel, o + Vector3.up * 0.09f, Quaternion.identity, true, "metal");
            }
            Part(k, g, "Bed chassis", k.meshes.RoundedBox(new Vector3(hw * 2f, 0.12f, hl * 2f), 0.02f, 3), VhWakingStyle.MetalDark, Vector3.up * 0.42f, Quaternion.identity, true, "metal");
            Part(k, g, "Bed deck", k.meshes.RoundedBox(new Vector3(hw * 2f - 0.04f, 0.04f, hl * 2f - 0.04f), 0.01f, 3), BedFrame, Vector3.up * 0.5f, Quaternion.identity, true, "plastic");
            if (raised)
            {
                Part(k, g, "Mattress (foot)", k.meshes.RoundedBox(new Vector3(0.88f, 0.14f, 1.3f), 0.05f, 4), VhWakingStyle.Sheet, new Vector3(0f, 0.59f, -0.34f), Quaternion.identity, true, "linen");
                Part(k, g, "Mattress (back, raised)", k.meshes.RoundedBox(new Vector3(0.88f, 0.14f, 0.8f), 0.05f, 4), VhWakingStyle.Sheet, new Vector3(0f, 0.8f, 0.6f), Quaternion.Euler(-40f, 0f, 0f), true, "linen");
                Part(k, g, "Pillow", k.meshes.RoundedBox(new Vector3(0.62f, 0.13f, 0.38f), 0.06f, 4, 0.02f), VhWakingStyle.Pillow, new Vector3(0f, 1.06f, 0.86f), Quaternion.Euler(-48f, 0f, 0f), true, "linen");
            }
            else
            {
                Part(k, g, "Mattress", k.meshes.RoundedBox(new Vector3(0.88f, 0.14f, 1.98f), 0.05f, 4), VhWakingStyle.Sheet, Vector3.up * 0.59f, Quaternion.identity, true, "linen");
                Part(k, g, "Pillow", k.meshes.RoundedBox(new Vector3(0.62f, 0.13f, 0.38f), 0.06f, 4, 0.02f), VhWakingStyle.Pillow, new Vector3(0f, 0.72f, 0.8f), Quaternion.Euler(-12f, 0f, 0f), true, "linen");
            }
            Part(k, g, "Head end", k.meshes.RoundedBox(new Vector3(hw * 2f + 0.04f, 0.58f, 0.05f), 0.02f, 3), BedFrame, new Vector3(0f, 0.75f, hl + 0.02f), Quaternion.identity, true, "plastic");
            Part(k, g, "Foot end", k.meshes.RoundedBox(new Vector3(hw * 2f + 0.04f, 0.42f, 0.05f), 0.02f, 3), BedFrame, new Vector3(0f, 0.63f, -hl - 0.02f), Quaternion.identity, true, "plastic");
            foreach (float x in new[] { -hw - 0.02f, hw + 0.02f })
            {
                Part(k, g, "Side rail", k.meshes.RoundedBox(new Vector3(0.03f, 0.24f, 0.8f), 0.012f, 3), BedFrame, new Vector3(x, 0.8f, 0.45f), Quaternion.identity, true, "plastic");
                Part(k, g, "Side rail window", k.meshes.Box(new Vector3(0.032f, 0.12f, 0.6f)), Glass, new Vector3(x, 0.8f, 0.45f), Quaternion.identity, false, "glass");
            }
            var col = g.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.45f, 0f);
            col.size = new Vector3(hw * 2f + 0.1f, 0.9f, hl * 2f + 0.1f);
            return g;
        }

        static void Wheelchair(Kit k, Vector3 at, float yaw)
        {
            var w = new GameObject("Wheelchair").transform;
            w.SetParent(k.root, false);
            w.localPosition = at;
            w.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var ring = new List<Vector3>();
            for (int i = 0; i <= 28; i++)
            {
                float a = i / 28f * Mathf.PI * 2f;
                ring.Add(new Vector3(0f, Mathf.Sin(a) * 0.29f, Mathf.Cos(a) * 0.29f));
            }
            foreach (float x in new[] { -0.3f, 0.3f })
            {
                Part(k, w, "Wheel", k.meshes.Tube(ring, 0.018f, 8), Shoe, new Vector3(x, 0.3f, -0.08f), Quaternion.identity, true, "plastic_dark");
                Part(k, w, "Push rim", k.meshes.Tube(ring, 0.007f, 6), Steel, new Vector3(x * 1.1f, 0.3f, -0.08f), Quaternion.identity, false, "metal");
                Part(k, w, "Hub", k.meshes.Cylinder(0.03f, 0.03f, 0.05f, 10), Steel, new Vector3(x, 0.3f, -0.08f), Quaternion.Euler(0f, 0f, 90f), false, "metal");
                Part(k, w, "Castor", k.meshes.Cylinder(0.06f, 0.06f, 0.025f, 12), Shoe, new Vector3(x * 0.8f, 0.06f, 0.36f), Quaternion.Euler(0f, 0f, 90f), true, "plastic_dark");
                Part(k, w, "Frame", k.meshes.Tube(new List<Vector3> { new Vector3(x * 0.85f, 0.08f, 0.36f), new Vector3(x * 0.85f, 0.5f, 0.3f), new Vector3(x * 0.85f, 0.5f, -0.2f), new Vector3(x * 0.85f, 0.95f, -0.26f) }, 0.012f, 6), Steel, Vector3.zero, Quaternion.identity, true, "metal");
            }
            Part(k, w, "Seat", k.meshes.RoundedBox(new Vector3(0.46f, 0.04f, 0.44f), 0.015f, 2), Shoe, new Vector3(0f, 0.5f, 0.06f), Quaternion.identity, true, "fabric");
            Part(k, w, "Back", k.meshes.RoundedBox(new Vector3(0.46f, 0.4f, 0.03f), 0.015f, 2), Shoe, new Vector3(0f, 0.74f, -0.23f), Quaternion.Euler(-8f, 0f, 0f), true, "fabric");
            Part(k, w, "Footplate", k.meshes.Box(new Vector3(0.36f, 0.015f, 0.14f)), VhWakingStyle.MetalDark, new Vector3(0f, 0.12f, 0.4f), Quaternion.identity, true, "metal");
            var col = w.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.45f, 0.05f);
            col.size = new Vector3(0.66f, 0.9f, 0.9f);
        }

        /// <summary>A padded chair (the visitors' and the family room's): a steel frame, a fabric seat and back, wooden arms.</summary>
        static Transform Chair(Kit k, Vector3 at, float yaw, Color upholstery, bool arms = true)
        {
            var c = new GameObject("Chair").transform;
            c.SetParent(k.root, false);
            c.localPosition = at;
            c.localRotation = Quaternion.Euler(0f, yaw, 0f);
            foreach (var o in new[] { new Vector3(-0.22f, 0f, -0.2f), new Vector3(0.22f, 0f, -0.2f), new Vector3(-0.22f, 0f, 0.2f), new Vector3(0.22f, 0f, 0.2f) })
                Part(k, c, "Leg", k.meshes.Cylinder(0.014f, 0.014f, 0.42f, 8), Steel, o, Quaternion.identity, true, "metal");
            Part(k, c, "Seat", k.meshes.RoundedBox(new Vector3(0.5f, 0.09f, 0.48f), 0.035f, 4, 0.01f), upholstery, new Vector3(0f, 0.46f, 0f), Quaternion.identity, true, "fabric");
            Part(k, c, "Back", k.meshes.RoundedBox(new Vector3(0.5f, 0.46f, 0.08f), 0.035f, 4, 0.01f), upholstery, new Vector3(0f, 0.76f, -0.24f), Quaternion.Euler(-8f, 0f, 0f), true, "fabric");
            if (arms)
                foreach (float x in new[] { -0.27f, 0.27f })
                {
                    Part(k, c, "Arm", k.meshes.RoundedBox(new Vector3(0.05f, 0.04f, 0.42f), 0.015f, 3), Laminate, new Vector3(x, 0.64f, -0.02f), Quaternion.identity, true, "wood");
                    Part(k, c, "Arm post", k.meshes.Cylinder(0.012f, 0.012f, 0.2f, 8), Steel, new Vector3(x, 0.44f, 0.12f), Quaternion.identity, false, "metal");
                }
            var col = c.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.45f, 0f);
            col.size = new Vector3(0.6f, 0.9f, 0.56f);
            return c;
        }

        /// <summary>One trace on a monitor (0 the heart's spikes, 1 the pleth's swell, 2 the breathing's slow wave).</summary>
        static void Trace(Kit k, Transform parent, Vector3 at, Color glow, int kind)
        {
            var wave = new List<Vector3>();
            for (int i = 0; i <= 40; i++)
            {
                float u = i / 40f;
                float y;
                if (kind == 0) { int m = i % 13; y = m == 5 ? 0.035f : m == 6 ? -0.02f : m == 7 ? 0.008f : 0f; }
                else if (kind == 1) y = Mathf.Max(0f, Mathf.Sin(u * Mathf.PI * 6f)) * 0.02f;
                else y = Mathf.Sin(u * Mathf.PI * 2f) * 0.012f;
                wave.Add(at + new Vector3(-0.14f + u * 0.28f, y, 0f));
            }
            var mat = k.style.Unique(Color.black, null, "VH monitor trace");
            VhWakingStyle.SetEmission(mat, glow);
            VhMeshKit.Show(parent, "Trace", k.meshes.Tube(wave, 0.0028f, 4), mat, Vector3.zero, Quaternion.identity, false);
        }

        static void Bouquet(Kit k, Vector3 at)
        {
            Part(k, k.root, "Vase", k.meshes.Cylinder(0.06f, 0.075f, 0.22f, 16), new Color(0.55f, 0.64f, 0.68f), at, Quaternion.identity, true, "glass");
            var stem = M(k, Stem, "fabric");
            var lily = M(k, Lily, "linen");
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * Mathf.PI * 2f;
                var top = at + new Vector3(Mathf.Cos(a) * 0.1f, 0.45f + (i % 3) * 0.05f, Mathf.Sin(a) * 0.16f);
                var mid = Vector3.Lerp(at + Vector3.up * 0.18f, top, 0.5f) + new Vector3(Mathf.Cos(a) * 0.02f, 0f, Mathf.Sin(a) * 0.03f);
                VhMeshKit.Show(k.root, "Stem", k.meshes.Tube(new List<Vector3> { at + Vector3.up * 0.18f, mid, top }, 0.005f, 5), stem, Vector3.zero, Quaternion.identity, false);
                VhMeshKit.Show(k.root, "Leaf", k.meshes.RoundedBox(new Vector3(0.012f, 0.1f, 0.03f), 0.005f, 2), stem, mid, Quaternion.Euler(0f, a * Mathf.Rad2Deg, 30f), false);
                var bloom = new GameObject("Lily").transform;
                bloom.SetParent(k.root, false);
                bloom.localPosition = top;
                bloom.localRotation = Quaternion.Euler(25f * Mathf.Cos(a), a * Mathf.Rad2Deg, 25f * Mathf.Sin(a));
                for (int p = 0; p < 3; p++)
                    VhMeshKit.Show(bloom, "Petal", k.meshes.RoundedBox(new Vector3(0.03f, 0.008f, 0.08f), 0.004f, 2, 0.003f), lily, Quaternion.Euler(0f, p * 120f, 0f) * new Vector3(0f, 0f, 0.03f), Quaternion.Euler(-25f, p * 120f, 0f), false);
            }
            Box(k, "Card stick", at + new Vector3(-0.07f, 0.28f, 0.02f), new Vector3(0.004f, 0.18f, 0.004f), Stem, false, 0f, null, "fabric");
            Box(k, "Card", at + new Vector3(-0.07f, 0.38f, 0.02f), new Vector3(0.006f, 0.06f, 0.09f), new Color(0.95f, 0.94f, 0.9f), false, 0f, null, "linen");
        }

        /// <summary>A potted plant (Kenney's when the build has it, a ward's fig built here otherwise).</summary>
        static void Plant(Kit k, Vector3 at, float height)
        {
            if (Model(k, "plant", at, 30f, height) != null) return;
            Part(k, k.root, "Pot", k.meshes.Cylinder(0.16f, 0.2f, 0.36f, 16), new Color(0.82f, 0.8f, 0.76f), at, Quaternion.identity, true, "plastic");
            var leaf = M(k, VhWakingStyle.Plant, "fabric");
            for (int i = 0; i < 14; i++)
            {
                float a = i * 2.4f, r = 0.05f + (i % 4) * 0.04f, y = 0.4f + i * (height - 0.5f) / 14f;
                VhMeshKit.Show(k.root, "Leaf", k.meshes.RoundedBox(new Vector3(0.12f, 0.012f, 0.22f), 0.006f, 2, 0.01f), leaf, at + new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r), Quaternion.Euler(-30f, a * Mathf.Rad2Deg, 0f), true);
            }
        }

        // ---------- 11 PM: the booth in the closed sleep-research wing ----------

        public static void Booth(VhArea a, VisitingHoursConfig c, bool day2)
        {
            var k = Begin(a, c, day2);
            const float h = 2.7f;
            var wall = new Color(0.66f, 0.66f, 0.62f);
            Floor(k, "Floor", -3f, 3f, 0f, 6f, new Color(0.5f, 0.5f, 0.48f));
            SuspendedCeiling(k, -3f, 3f, 0f, 6f, h);
            Wall(k, -3f, 0f, -3f, 6f, h, wall);
            Wall(k, 3f, 0f, 3f, 6f, h, wall);
            Wall(k, -3f, 0f, 3f, 0f, h, wall);
            Wall(k, -3f, 6f, 3f, 6f, h, wall);
            // The closed wing's ceiling lights are off; the desk lamp is the room's warm light.
            Box(k, "Light (off)", new Vector3(-1.2f, h - 0.01f, 3f), new Vector3(0.6f, 0.02f, 1.2f), new Color(0.55f, 0.56f, 0.55f), false, 0f, null, "plastic");
            Box(k, "Light (off)", new Vector3(1.2f, h - 0.01f, 3f), new Vector3(0.6f, 0.02f, 1.2f), new Color(0.55f, 0.56f, 0.55f), false, 0f, null, "plastic");
            Door(k, new Vector3(-0.6f, 0f, 0.06f), Vector3.forward, 0.95f, DoorBlue);

            // The night window, the observation glass onto the dark lab.
            var night = k.style.Unique(new Color(0.04f, 0.06f, 0.1f), null, "VH booth window");
            VhWakingStyle.SetEmission(night, new Color(0.06f, 0.09f, 0.18f) * 0.8f);
            VhMeshKit.Show(k.root, "Window", k.meshes.Quad(2.4f, 1.1f), night, new Vector3(0f, 1.6f, 5.93f), Quaternion.identity, false);
            Box(k, "Window frame", new Vector3(0f, 2.16f, 5.92f), new Vector3(2.5f, 0.05f, 0.06f), Plastic, false, 0f, null, "plastic");
            Box(k, "Window frame", new Vector3(0f, 1.04f, 5.92f), new Vector3(2.5f, 0.05f, 0.06f), Plastic, false, 0f, null, "plastic");
            Box(k, "Window frame", new Vector3(0f, 1.6f, 5.92f), new Vector3(0.05f, 1.1f, 0.06f), Plastic, false, 0f, null, "plastic");
            Box(k, "Sill", new Vector3(0f, 1.0f, 5.85f), new Vector3(2.5f, 0.04f, 0.2f), Plastic, false, 0f, null, "plastic");
            Box(k, "Observation glass", new Vector3(2.935f, 1.55f, 3.9f), new Vector3(0.01f, 1.0f, 2.0f), Glass, false, 0f, null, "glass");
            Box(k, "Glass frame", new Vector3(2.92f, 1.55f, 3.9f), new Vector3(0.06f, 1.1f, 2.1f), new Color(0.3f, 0.31f, 0.33f), false, 0f, null, "metal");

            // The desk: papers, Mags' mug, the lamp, a radio; her coat on the stand; the closed wing's boxes.
            Box(k, "Desk", new Vector3(2.4f, 0.73f, 2.1f), new Vector3(0.8f, 0.04f, 1.6f), Laminate * 0.85f, true, 0.01f, null, "wood");
            Box(k, "Desk pedestal", new Vector3(2.4f, 0.36f, 1.5f), new Vector3(0.7f, 0.7f, 0.42f), Laminate * 0.75f, true, 0.01f, null, "wood");
            Part(k, k.root, "Desk leg", k.meshes.Cylinder(0.025f, 0.025f, 0.72f, 8), VhWakingStyle.MetalDark, new Vector3(2.4f, 0f, 2.8f), Quaternion.identity, true, "metal");
            if (Model(k, "lamp", new Vector3(2.55f, 0.75f, 2.65f), -120f, 0.42f, false, false) == null)
                Part(k, k.root, "Lamp", k.meshes.Cylinder(0.1f, 0.05f, 0.14f, 14), VhWakingStyle.Lampshade, new Vector3(2.55f, 1.05f, 2.65f), Quaternion.identity, true, "linen");
            AddLight(k, "Desk lamp", new Vector3(2.45f, 1.15f, 2.55f), new Color(1f, 0.8f, 0.55f), 1.2f, 5.5f, true);
            Box(k, "Papers", new Vector3(2.3f, 0.76f, 2.0f), new Vector3(0.3f, 0.02f, 0.4f), VhWakingStyle.Paper, false, 0f, Quaternion.Euler(0f, 12f, 0f), "linen");
            Box(k, "Papers", new Vector3(2.45f, 0.77f, 1.75f), new Vector3(0.24f, 0.015f, 0.32f), new Color(0.84f, 0.82f, 0.74f), false, 0f, Quaternion.Euler(0f, -8f, 0f), "linen");
            Part(k, k.root, "Mags' mug", k.meshes.Cylinder(0.042f, 0.042f, 0.1f, 14), new Color(0.6f, 0.32f, 0.28f), new Vector3(2.2f, 0.75f, 2.5f), Quaternion.identity, true, "plastic");
            Model(k, "radio", new Vector3(2.55f, 0.75f, 1.45f), -90f, 0.16f, false, false);
            Model(k, "coat_rack", new Vector3(2.55f, 0f, 0.45f), 0f, 1.7f);
            Model(k, "box", new Vector3(1.5f, 0f, 5.45f), 10f, 0.4f);
            Model(k, "box", new Vector3(1.55f, 0.4f, 5.5f), -5f, 0.32f, false, false);
            Model(k, "box", new Vector3(2.1f, 0f, 5.4f), 30f, 0.38f);
            Clock(k, new Vector3(0.9f, 2.25f, 0.06f), Vector3.forward, 11f);

            // The chair and its rig: a reclined seat, the halo over the head, cables to the console's screens.
            var chairCol = new Color(0.32f, 0.36f, 0.42f);
            var chair = new GameObject("The chair").transform;
            chair.SetParent(k.root, false);
            chair.localPosition = new Vector3(-1.1f, 0f, 4.1f);
            chair.localRotation = Quaternion.Euler(0f, 160f, 0f);
            Part(k, chair, "Base", k.meshes.Cylinder(0.28f, 0.2f, 0.32f, 18), VhWakingStyle.MetalDark, Vector3.zero, Quaternion.identity, true, "metal");
            Part(k, chair, "Seat", k.meshes.RoundedBox(new Vector3(0.62f, 0.14f, 0.62f), 0.06f, 4, 0.01f), chairCol, new Vector3(0f, 0.45f, 0f), Quaternion.identity, true, "fabric");
            Part(k, chair, "Back", k.meshes.RoundedBox(new Vector3(0.62f, 0.12f, 0.88f), 0.06f, 4, 0.01f), chairCol, new Vector3(0f, 0.76f, -0.62f), Quaternion.Euler(-35f, 0f, 0f), true, "fabric");
            Part(k, chair, "Headrest", k.meshes.RoundedBox(new Vector3(0.34f, 0.1f, 0.24f), 0.05f, 3), chairCol, new Vector3(0f, 1.08f, -1.02f), Quaternion.Euler(-35f, 0f, 0f), true, "fabric");
            Part(k, chair, "Legrest", k.meshes.RoundedBox(new Vector3(0.56f, 0.12f, 0.62f), 0.05f, 3), chairCol, new Vector3(0f, 0.4f, 0.56f), Quaternion.Euler(15f, 0f, 0f), true, "fabric");
            foreach (float x in new[] { -0.36f, 0.36f })
                Part(k, chair, "Armrest", k.meshes.RoundedBox(new Vector3(0.1f, 0.08f, 0.55f), 0.035f, 3), chairCol * 0.85f, new Vector3(x, 0.62f, -0.05f), Quaternion.identity, true, "fabric");
            var halo = new List<Vector3>();
            for (int i = 0; i <= 32; i++)
            {
                float t = i / 32f * Mathf.PI * 2f;
                halo.Add(new Vector3(Mathf.Cos(t) * 0.18f, Mathf.Sin(t) * 0.18f, 0f));
            }
            var rig = new GameObject("Rig").transform;
            rig.SetParent(chair, false);
            rig.localPosition = new Vector3(0f, 1.3f, -1.05f);
            rig.localRotation = Quaternion.Euler(55f, 0f, 0f);
            Part(k, rig, "Halo", k.meshes.Tube(halo, 0.018f, 8), Steel, Vector3.zero, Quaternion.identity, true, "metal");
            Glow(k, "Halo lights", k.meshes.Tube(halo, 0.006f, 6), Color.black, new Color(0.45f, 0.65f, 1f) * 1.2f, new Vector3(0f, 0f, 0.012f), Quaternion.identity, rig);
            Part(k, chair, "Rig arm", k.meshes.Tube(new List<Vector3> { new Vector3(0.3f, 0.6f, -0.9f), new Vector3(0.3f, 1.45f, -1.25f), new Vector3(0.12f, 1.42f, -1.1f) }, 0.02f, 8), VhWakingStyle.MetalDark, Vector3.zero, Quaternion.identity, true, "metal");
            var chairBox = chair.gameObject.AddComponent<BoxCollider>();
            chairBox.center = new Vector3(0f, 0.5f, -0.2f);
            chairBox.size = new Vector3(0.8f, 1f, 1.8f);
            AddLight(k, "Rig glow", new Vector3(-1.0f, 1.4f, 5.0f), new Color(0.5f, 0.65f, 1f), 0.25f, 2f);
            AddLight(k, "Lab fill", new Vector3(-1.0f, 2.3f, 3.4f), new Color(0.6f, 0.7f, 0.95f), 0.35f, 4.5f);

            Box(k, "Console", new Vector3(-2.5f, 0.55f, 5.25f), new Vector3(0.75f, 1.1f, 0.55f), VhWakingStyle.MetalDark, true, 0.02f, null, "metal");
            Box(k, "Console top", new Vector3(-2.5f, 1.12f, 5.2f), new Vector3(0.8f, 0.04f, 0.62f), new Color(0.3f, 0.31f, 0.33f), false, 0.01f, null, "plastic");
            for (int i = 0; i < 2; i++)
            {
                float x = -2.7f + i * 0.4f;
                Box(k, "Console screen", new Vector3(x, 1.38f, 5.35f), new Vector3(0.36f, 0.26f, 0.04f), VhWakingStyle.MetalDark, false, 0.01f, null, "plastic");
                Box(k, "Console glass", new Vector3(x, 1.38f, 5.328f), new Vector3(0.32f, 0.22f, 0.002f), new Color(0.03f, 0.04f, 0.05f), false, 0f, null, "glass");
                var scr = new GameObject("Console traces").transform;
                scr.SetParent(k.root, false);
                scr.localPosition = new Vector3(x, 1.38f, 5.322f);
                scr.localRotation = Quaternion.Euler(0f, 180f, 0f);
                Trace(k, scr, new Vector3(0f, 0.04f, 0f), new Color(0.4f, 0.6f, 1f) * 1.3f, i == 0 ? 2 : 1);
                Trace(k, scr, new Vector3(0f, -0.04f, 0f), new Color(0.6f, 0.5f, 1f) * 1.1f, 2);
            }
            for (int i = 0; i < 8; i++)
                Glow(k, "Console lamp", k.meshes.Box(new Vector3(0.025f, 0.012f, 0.025f)), Color.black, (i % 3 == 0 ? new Color(1f, 0.6f, 0.2f) : new Color(0.3f, 1f, 0.5f)) * 1.3f, new Vector3(-2.8f + i * 0.08f, 1.145f, 5.05f), Quaternion.identity);
            Part(k, k.root, "Cable", k.meshes.Tube(new List<Vector3> { new Vector3(-2.2f, 0.9f, 5.05f), new Vector3(-1.9f, 0.04f, 4.9f), new Vector3(-1.4f, 0.04f, 4.9f), new Vector3(-1.2f, 0.5f, 4.95f) }, 0.016f, 6), Shoe, Vector3.zero, Quaternion.identity, false, "plastic_dark");
            Part(k, k.root, "Cable", k.meshes.Tube(new List<Vector3> { new Vector3(-2.25f, 0.6f, 5.0f), new Vector3(-1.95f, 0.03f, 5.1f), new Vector3(-1.3f, 0.03f, 5.15f), new Vector3(-1.15f, 0.4f, 5.0f) }, 0.012f, 6), new Color(0.3f, 0.3f, 0.34f), Vector3.zero, Quaternion.identity, false, "plastic_dark");

            // The curtained bed in the corner, its curtain drawn all the way round.
            Box(k, "Curtained bed", new Vector3(-2.3f, 0.4f, 1.6f), new Vector3(0.9f, 0.5f, 1.9f), VhWakingStyle.Sheet * 0.8f, true, 0.04f, null, "linen");
            var trackB = new List<Vector3> { new Vector3(-2.95f, 2.5f, 2.75f), new Vector3(-1.65f, 2.5f, 2.75f), new Vector3(-1.65f, 2.5f, 0.5f), new Vector3(-2.95f, 2.5f, 0.5f) };
            Part(k, k.root, "Curtain track", k.meshes.Tube(trackB, 0.012f, 6), Steel, Vector3.zero, Quaternion.identity, false, "metal");
            var curtain = new Color(0.52f, 0.58f, 0.56f);
            for (int i = 0; i < 14; i++)
                Box(k, "Curtain", new Vector3(-2.9f + i * 0.095f, 1.33f, 2.75f + (i % 2 == 0 ? 0.03f : -0.03f)), new Vector3(0.11f, 2.3f, 0.025f), curtain, false, 0f, Quaternion.Euler(0f, i % 2 == 0 ? 30f : -30f, 0f), "fabric");
            for (int i = 0; i < 22; i++)
                Box(k, "Curtain", new Vector3(-1.65f + (i % 2 == 0 ? 0.03f : -0.03f), 1.33f, 2.7f - i * 0.1f), new Vector3(0.025f, 2.3f, 0.12f), curtain, false, 0f, Quaternion.Euler(0f, i % 2 == 0 ? 30f : -30f, 0f), "fabric");
            var curtainWall = new GameObject("Curtain (collider)");
            curtainWall.transform.SetParent(k.root, false);
            curtainWall.transform.localPosition = new Vector3(-2.3f, 1.2f, 1.6f);
            curtainWall.AddComponent<BoxCollider>().size = new Vector3(1.4f, 2.4f, 2.4f);

            // Mags, by the desk.
            People(k).Place(VhWardPeople.Mags, new Vector3(1.6f, 0f, 2.3f), 230f, VhWardPeople.Pose.Standing);

            VhWardLight.Night(k.root);
            VhWakingStyle.SetAmbient(new Color(0.12f, 0.14f, 0.19f), new Color(0.06f, 0.06f, 0.07f), new Color(0.05f, 0.06f, 0.08f), 0f, 0.15f);
            a.spawn = new Vector3(-0.8f, 0.05f, 1.2f);
            a.spawnYaw = 45f;
            // Where she wakes when the house threw her out (the chair; she looks across at Mags).
            a.chairEye = new Vector3(-1.4f, 1.12f, 4.93f);
            a.chairYaw = 131f;
            JamKit.Perf.VhPerfTiers.CapNow();
        }

        static VhWardPeople People(Kit k) => new VhWardPeople(k.root, k.meshes, k.style, k.lib, k.mats, k.area.cast);
    }
}

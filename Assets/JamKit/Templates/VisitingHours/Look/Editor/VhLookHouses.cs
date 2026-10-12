using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.Look.EditorTools
{
    /// <summary>
    /// Houses for the Look's scenes (LookLab's clapboard house and brick bungalow, the hill behind them), at two levels of
    /// detail, built from the Look library. Pass 2 (the judge's round 1): the roof is a real slab (shingles on top, a painted
    /// soffit underneath, fascia and rake boards on its edges) so it reads from below; the gable ends rise to meet the
    /// roof's underside (no sky gap); gutters run only along the horizontal eaves and the downspouts drop straight down the
    /// corners; siding and brick courses run continuously across every wall piece and the gables (UVs in the house's own
    /// frame); furniture silhouettes and a dividing wall behind the windows.
    /// <list type="bullet">
    /// <item><see cref="Lod.Full"/>: real window openings (trim, sill, glass, muntins, curtains, a lit room with furniture),
    /// a panelled door, a porch with steps and balusters, colliders on every solid.</item>
    /// <item><see cref="Lod.Far"/>: solid walls with framed windows (trim, sill, dark reflective glass), a door, the same
    /// roof, porch posts, chimney, gutters; merged into one mesh per material; one collider.</item>
    /// </list>
    /// <code>
    /// var spec = VhLookHouses.Spec.Clapboard(new Color(0.72f, 0.8f, 0.66f));     // or Spec.Brick(), Spec.Random(seed)
    /// VhLookHouses.Build(parent, frontLeftCorner, yawDegrees, spec, VhLookHouses.Lod.Full, groundHeightAt);
    /// </code>
    /// The house's frame: x along the front (to the right), z back, y up; the front faces -z. Foundations reach 0.3 m into the
    /// ground at the lowest corner, and the floor sits 0.4 m above the highest one (a slope gets a taller foundation downhill).
    /// </summary>
    public static class VhLookHouses
    {
        public enum Lod { Full, Far }

        /// <summary>Screen-relative height below which a far house isn't drawn (tunable by the Perf agent).</summary>
        public static float FarCull = 0.006f;

        public struct Opening
        {
            public float u0, v0, u1, v1;
            public bool door;
            public Opening(float u0, float v0, float u1, float v1, bool door = false) { this.u0 = u0; this.v0 = v0; this.u1 = u1; this.v1 = v1; this.door = door; }
        }

        public sealed class Spec
        {
            public float width = 14f, depth = 10f;
            public int storeys = 2;
            public float storeyHeight = 2.8f;
            public float roofRise = 3.3f, overhang = 0.45f, roofThickness = 0.2f;
            public Material siding, gable, trim, roof, door, foundation;
            public bool porch = true, chimney = true, shutters;
            /// <summary>Pass 3 (round 2: "every house faces the same way; identical rows"): the gable end faces the street,
            /// with the door, the porch and the front windows on it (the ridge runs back from the street). The caller's
            /// corner, yaw, width (frontage) and depth mean the same as for a side-gabled house.</summary>
            public bool gableFront;
            public Color shutterColor = new Color(0.2f, 0.28f, 0.24f);
            /// <summary>Openings per wall (u along the wall from its left end seen from outside, v up from the floor). Null =
            /// laid out automatically (bays on the front, two windows a floor on the sides).</summary>
            public List<Opening> front, left, right, back;
            public int seed = 1;

            public float WallHeight => storeys * storeyHeight;

            /// <summary>A shallow copy (the builder's own frame for a gable-front house).</summary>
            public Spec MemberwiseCloneSpec() => (Spec)MemberwiseClone();

            public static Spec Clapboard(Color paint, int seed = 1)
            {
                var siding = VhLookLibrary.Tinted("Clapboard_Painted", "Paint" + ColorKey(paint), paint);
                return new Spec
                {
                    siding = siding, gable = siding, trim = VhLookLibrary.Paint("TrimWhite", new Color(0.95f, 0.94f, 0.9f)),
                    roof = VhLookLibrary.Get(seed % 3 == 0 ? "Roof_ShinglesSlate" : "Roof_ShinglesAsphalt"),
                    door = VhLookLibrary.Paint("Door" + (seed % 4), DoorColors[seed % 4], 1.4f),
                    foundation = VhLookLibrary.Get("Concrete_Kerb"), seed = seed,
                };
            }

            public static Spec Brick(int seed = 1)
            {
                return new Spec
                {
                    storeys = 1, storeyHeight = 3.2f, roofRise = 2.4f, overhang = 0.5f, porch = false, chimney = true,
                    siding = VhLookLibrary.Get("Brick_Red"), gable = VhLookLibrary.Get("Siding_WoodBrown"),
                    trim = VhLookLibrary.Paint("TrimWhite", new Color(0.95f, 0.94f, 0.9f)), roof = VhLookLibrary.Get("Roof_ShinglesSlate"),
                    door = VhLookLibrary.Paint("DoorGreen", new Color(0.16f, 0.3f, 0.22f), 1.4f), foundation = VhLookLibrary.Get("Concrete_Kerb"),
                    seed = seed,
                };
            }

            /// <summary>A plausible house for a 1990s hill street: one or two storeys, a painted or brick front, a porch
            /// sometimes, shutters sometimes.</summary>
            public static Spec Random(int seed)
            {
                var rng = new System.Random(seed * 977 + 3);
                float R() => (float)rng.NextDouble();
                Spec s = R() < 0.22f ? Brick(seed) : Clapboard(HousePaints[rng.Next(HousePaints.Length)], seed);
                s.width = Mathf.Round(Mathf.Lerp(9f, 14f, R()) * 2f) / 2f;
                s.depth = Mathf.Round(Mathf.Lerp(7.5f, 10f, R()) * 2f) / 2f;
                if (s.storeys == 2 || R() < 0.55f) { s.storeys = R() < 0.6f ? 2 : 1; }
                s.storeyHeight = s.storeys == 2 ? 2.8f : 3.0f;
                s.roofRise = s.depth * Mathf.Lerp(0.26f, 0.36f, R());
                s.porch = R() < 0.55f;
                s.chimney = R() < 0.6f;
                s.shutters = s.siding != null && s.siding.name.Contains("Clapboard") && R() < 0.5f;
                s.shutterColor = ShutterColors[rng.Next(ShutterColors.Length)];
                // A third of them gable-front (narrower and deeper, steeper roofs): the street isn't one row of the same box.
                if (R() < 0.34f)
                {
                    s.gableFront = true;
                    s.width = Mathf.Round(Mathf.Lerp(7.5f, 9.5f, R()) * 2f) / 2f;
                    s.depth = Mathf.Round(Mathf.Lerp(10f, 13f, R()) * 2f) / 2f;
                    s.roofRise = s.width * Mathf.Lerp(0.38f, 0.5f, R());
                }
                return s;
            }
        }

        static readonly Color[] DoorColors = { new Color(0.55f, 0.13f, 0.1f), new Color(0.14f, 0.24f, 0.38f), new Color(0.16f, 0.3f, 0.22f), new Color(0.32f, 0.2f, 0.12f) };
        static readonly Color[] ShutterColors = { new Color(0.2f, 0.28f, 0.24f), new Color(0.16f, 0.2f, 0.3f), new Color(0.12f, 0.12f, 0.12f), new Color(0.42f, 0.16f, 0.12f) };
        /// <summary>Faded 1990s paints: cream, sky blue, rose, sage, white, butter, grey-green, dusty blue.</summary>
        public static readonly Color[] HousePaints =
        {
            new Color(0.95f, 0.88f, 0.72f), new Color(0.74f, 0.82f, 0.88f), new Color(0.92f, 0.8f, 0.74f), new Color(0.78f, 0.84f, 0.7f),
            new Color(0.97f, 0.96f, 0.92f), new Color(0.96f, 0.9f, 0.64f), new Color(0.7f, 0.76f, 0.7f), new Color(0.66f, 0.74f, 0.84f),
        };

        static string ColorKey(Color c) => Mathf.RoundToInt(c.r * 99) + "_" + Mathf.RoundToInt(c.g * 99) + "_" + Mathf.RoundToInt(c.b * 99);

        /// <summary>
        /// Builds a house with its front-left foundation corner at <paramref name="corner"/> (x, z; y is taken from the ground),
        /// turned <paramref name="yaw"/> degrees. <paramref name="ground"/> gives the ground height at (x, z); null = corner.y.
        /// </summary>
        public static GameObject Build(Transform parent, Vector3 corner, float yaw, Spec s, Lod lod, Func<float, float, float> ground = null, string name = null)
        {
            var root = new GameObject(name ?? ("House (" + (lod == Lod.Full ? "full" : "far") + ")"));
            if (parent != null) root.transform.SetParent(parent, false);
            var rot = Quaternion.Euler(0f, yaw, 0f);
            // A gable-front house is built side-gabled in its own frame (W along the ridge = the caller's depth, D = the
            // frontage), turned so its right wall (a gable end) faces the street: frame +x = the street direction (-z of
            // the caller's frame), and its right wall's u = 0 end sits on the caller's front-left corner.
            var b0 = s;
            if (s.gableFront)
            {
                b0 = (Spec)s.MemberwiseCloneSpec();
                b0.width = s.depth;
                b0.depth = s.width;
                rot = rot * Quaternion.Euler(0f, 90f, 0f);
            }
            var origin = s.gableFront ? corner - rot * new Vector3(b0.width, 0f, 0f) : corner;
            // Ground under the footprint (corners and middle).
            float gMin = float.MaxValue, gMax = float.MinValue;
            for (int i = 0; i <= 2; i++)
                for (int k = 0; k <= 2; k++)
                {
                    var p = origin + rot * new Vector3(b0.width * i * 0.5f, 0f, b0.depth * k * 0.5f);
                    float g = ground != null ? ground(p.x, p.z) : corner.y;
                    gMin = Mathf.Min(gMin, g); gMax = Mathf.Max(gMax, g);
                }
            float floorY = gMax + (lod == Lod.Full ? 0.7f : 0.45f);
            root.transform.position = new Vector3(origin.x, floorY, origin.z);
            root.transform.rotation = rot;
            // The ground's height in the house frame at a house-frame (x, z).
            Func<float, float, float> local = (x, z) =>
            {
                var w = root.transform.TransformPoint(new Vector3(x, 0f, z));
                return (ground != null ? ground(w.x, w.z) : corner.y) - floorY;
            };
            var b = new Builder(root.transform, b0, lod, gMin - 0.3f - floorY, gMax - floorY, gMin - floorY, local);
            b.BuildAll();
            if (lod == Lod.Far) MergeByMaterial(root, b0);
            return root;
        }

        // ------------------------------------------------------------------------------------------------------------

        sealed class Builder
        {
            readonly Transform root;
            readonly Spec s;
            readonly Lod lod;
            readonly float footY;   // the foundation's bottom, in the house frame (negative)
            readonly float groundTop;   // the highest ground under the house, in the house frame (negative)
            readonly float groundLow;   // the lowest ground under the house, in the house frame (negative)
            readonly Func<float, float, float> groundAt;   // the ground's height in the house frame at (x, z)
            readonly List<GameObject> framed = new List<GameObject>();
            readonly Material glass, sheer, dark, curtain, interior, gutter, brass;
            readonly System.Random rng;
            float W => s.width;
            float D => s.depth;
            float H => s.WallHeight;
            const float Th = 0.22f;

            public Builder(Transform root, Spec s, Lod lod, float footY, float groundTop, float groundLow, Func<float, float, float> groundAt)
            {
                this.root = root; this.s = s; this.lod = lod; this.footY = footY; this.groundTop = groundTop;
                this.groundLow = groundLow; this.groundAt = groundAt;
                rng = new System.Random(s.seed * 31 + 7);
                glass = VhLookLibrary.Glass();
                sheer = VhLookLibrary.Sheer();
                dark = VhLookLibrary.Paint("WindowDark", new Color(0.11f, 0.1f, 0.09f), 1.9f);
                curtain = VhLookLibrary.Tinted("Plaster_Interior", "Curtain", new Color(0.95f, 0.86f, 0.74f));
                // Pass 3 (round 2: "brown blocks" in the windows): a dimmer, less orange room behind a sheer curtain.
                interior = VhLookLibrary.Tinted("Plaster_Interior", "SoftRoom", new Color(0.78f, 0.74f, 0.68f));
                gutter = VhLookLibrary.Paint("GutterWhite", new Color(0.92f, 0.92f, 0.9f), 1.5f);
                brass = VhLookLibrary.Paint("Brass", new Color(0.85f, 0.65f, 0.3f), 1.6f);
            }

            public void BuildAll()
            {
                bool full = lod == Lod.Full;
                // The foundation (pass 3; round 2: "stone foundations a third of the wall high"): concrete shows at most
                // ~0.35 m above the lowest ground; on a slope the walls carry on down to there in their own siding (a
                // walk-out basement), instead of a tall concrete plinth all round.
                float skirtBottom = Mathf.Min(-0.3f, groundLow + 0.35f);
                Box("Foundation", new Vector3(-0.06f, footY, -0.06f), new Vector3(W + 0.06f, skirtBottom + 0.02f, D + 0.06f), s.foundation, 0.03f, true);
                if (skirtBottom < -0.05f)
                    framed.Add(Box("Basement wall", new Vector3(-0.03f, skirtBottom, -0.03f), new Vector3(W + 0.03f, 0.001f, D + 0.03f), s.siding, 0.004f, true));
                var side = AutoSide();
                List<Opening> front, right;
                if (s.gableFront)
                {
                    // The street-facing gable end is the right wall: door and windows there; the long front wall gets side windows.
                    front = s.front ?? AutoLong(W);
                    right = s.right ?? AutoGableFront(D - 2f * Th);
                }
                else
                {
                    front = s.front ?? AutoFront();
                    right = s.right ?? side;
                }
                // Walls: front (faces -z), right (faces +x), back (faces +z), left (faces -x). Origins at each wall's left end
                // seen from outside; "inward" into the house.
                Wall("Wall front", new Vector3(0f, 0f, 0f), Vector3.forward, W, front);
                Wall("Wall right", new Vector3(W, 0f, Th), Vector3.left, D - 2f * Th, right);
                Wall("Wall back", new Vector3(W, 0f, D), Vector3.back, W, s.back ?? AutoBack());
                Wall("Wall left", new Vector3(0f, 0f, D - Th), Vector3.right, D - 2f * Th, s.left ?? side);
                if (full) Interior(front);
                // Corner boards (clapboard) and the frieze under the soffit.
                if (s.siding != null && !s.siding.name.Contains("Brick"))
                    foreach (var cx in new[] { 0f, W })
                        foreach (var cz in new[] { 0f, D })
                            Box("Corner board", new Vector3(cx - 0.09f, 0f, cz - 0.09f), new Vector3(cx + 0.09f, H, cz + 0.09f), s.trim, 0.015f, false);
                // The frieze rises into the soffit (the roof's underside is higher at the wall line than at the eave).
                float soffitAtWall = H + Mathf.Max(0f, s.overhang * s.roofRise / (D * 0.5f + s.overhang) - s.roofThickness) + 0.04f;
                Box("Frieze front", new Vector3(-0.05f, H - 0.28f, -0.05f), new Vector3(W + 0.05f, soffitAtWall, 0.02f), s.trim, 0.01f, false);
                Box("Frieze back", new Vector3(-0.05f, H - 0.28f, D - 0.02f), new Vector3(W + 0.05f, soffitAtWall, D + 0.05f), s.trim, 0.01f, false);
                Box("Frieze left", new Vector3(-0.05f, H - 0.2f, 0f), new Vector3(0.02f, H, D), s.trim, 0.01f, false);
                Box("Frieze right", new Vector3(W - 0.02f, H - 0.2f, 0f), new Vector3(W + 0.05f, H, D), s.trim, 0.01f, false);
                Roof();
                if (s.chimney) Chimney();
                if (s.gableFront) Porch(right, WallFrame("Porch frame (gable end)", new Vector3(W, 0f, Th), Vector3.left));
                else Porch(front, null);
                // Siding and brick: one continuous layout over every piece of every wall and the gables. Painted clapboard
                // at full detail is also cut into its courses, each shifted along the wall (round 2: "vertical siding
                // joints line up": the texture's one butt joint ran up every course at the same place).
                bool clap = s.siding != null && s.siding.name.Contains("Clapboard");
                foreach (var go in framed)
                {
                    if (clap && full) SplitCourses(go, root, CourseHeight, CoursePhase, s.seed);
                    else FrameUVs(go, root);
                }
            }

            /// <summary>A child frame on a wall: its origin at the wall's u = 0 end, local x along the wall, -z outward.</summary>
            Transform WallFrame(string name, Vector3 origin, Vector3 inward)
            {
                var t = new GameObject(name).transform;
                t.SetParent(root, false);
                t.localPosition = origin;
                t.localRotation = Quaternion.LookRotation(inward, Vector3.up);
                return t;
            }

            /// <summary>The street gable end of a gable-front house: the door off-centre, a window beside it, the upper
            /// floor's windows centred.</summary>
            List<Opening> AutoGableFront(float len)
            {
                var list = new List<Opening>();
                bool doorLeft = (s.seed & 1) == 0;
                float dc = doorLeft ? len * 0.3f : len * 0.7f, wc = doorLeft ? len * 0.72f : len * 0.28f;
                list.Add(new Opening(dc - 0.5f, 0f, dc + 0.5f, 2.2f, true));
                list.Add(new Opening(wc - 0.65f, 0.95f, wc + 0.65f, Mathf.Min(2.55f, s.storeyHeight - 0.3f)));
                for (int f = 1; f < s.storeys; f++)
                {
                    float y0 = f * s.storeyHeight;
                    foreach (var c in new[] { len * 0.32f, len * 0.68f })
                        list.Add(new Opening(c - 0.55f, y0 + 0.95f, c + 0.55f, y0 + Mathf.Min(2.45f, s.storeyHeight - 0.3f)));
                }
                return list;
            }

            /// <summary>A long side wall: a window every ~3.5 m on each floor.</summary>
            List<Opening> AutoLong(float len)
            {
                var list = new List<Opening>();
                int n = Mathf.Max(2, Mathf.FloorToInt(len / 3.5f));
                for (int f = 0; f < s.storeys; f++)
                {
                    float y0 = f * s.storeyHeight;
                    for (int i = 0; i < n; i++)
                    {
                        float c = (i + 0.5f) * len / n;
                        list.Add(new Opening(c - 0.6f, y0 + 1.0f, c + 0.6f, y0 + Mathf.Min(2.5f, s.storeyHeight - 0.3f)));
                    }
                }
                return list;
            }

            // ---------- Openings ----------

            List<Opening> AutoFront()
            {
                var list = new List<Opening>();
                int bays = Mathf.Max(2, Mathf.FloorToInt(W / 2.7f));
                float bay = W / bays;
                int doorBay = bays / 2 - (bays % 2 == 0 ? 1 : 0);
                if (s.porch) doorBay = Mathf.Clamp(doorBay, 0, bays - 1);
                for (int f = 0; f < s.storeys; f++)
                    for (int i = 0; i < bays; i++)
                    {
                        float c = (i + 0.5f) * bay;
                        float y0 = f * s.storeyHeight;
                        if (f == 0 && i == doorBay) { list.Add(new Opening(c - 0.5f, 0f, c + 0.5f, 2.2f, true)); continue; }
                        float w = Mathf.Min(1.4f, bay * 0.55f);
                        list.Add(new Opening(c - w * 0.5f, y0 + 0.95f, c + w * 0.5f, y0 + Mathf.Min(2.55f, s.storeyHeight - 0.3f)));
                    }
                return list;
            }

            List<Opening> AutoSide()
            {
                var list = new List<Opening>();
                float len = D - 2f * Th;
                for (int f = 0; f < s.storeys; f++)
                {
                    float y0 = f * s.storeyHeight;
                    foreach (var c in new[] { len * 0.28f, len * 0.72f })
                        list.Add(new Opening(c - 0.6f, y0 + 1.0f, c + 0.6f, y0 + Mathf.Min(2.5f, s.storeyHeight - 0.3f)));
                }
                return list;
            }

            List<Opening> AutoBack()
            {
                var list = new List<Opening>();
                for (int f = 0; f < s.storeys; f++)
                {
                    float y0 = f * s.storeyHeight;
                    foreach (var c in new[] { W * 0.25f, W * 0.7f })
                        list.Add(new Opening(c - 0.6f, y0 + 1.0f, c + 0.6f, y0 + Mathf.Min(2.4f, s.storeyHeight - 0.35f)));
                }
                return list;
            }

            // ---------- Walls ----------

            void Wall(string name, Vector3 origin, Vector3 inward, float width, List<Opening> openings)
            {
                var rot = Quaternion.LookRotation(inward, Vector3.up);
                var wall = new GameObject(name).transform;
                wall.SetParent(root, false);
                if (lod == Lod.Far)
                {
                    framed.Add(WallBox("Wall", wall, origin, rot, 0f, width, 0f, H, 0f, Th, s.siding, 0.004f, false));
                }
                else
                {
                    var us = new List<float> { 0f, width };
                    foreach (var o in openings) { us.Add(o.u0); us.Add(o.u1); }
                    us.Sort();
                    for (int i = 0; i < us.Count - 1; i++)
                    {
                        float a = us[i], c = us[i + 1];
                        if (c - a < 1e-3f) continue;
                        float mid = (a + c) * 0.5f;
                        var vs = new List<float> { 0f, H };
                        // Every column is cut at every opening's heights (pass 4; round 3: "vertical dashed seams": a full-height column
                        // beside a column cut at a window made T-junctions, and light leaked through the cracks down the wall).
                        foreach (var o in openings) { vs.Add(o.v0); vs.Add(o.v1); }
                        vs.Sort();
                        for (int k = 0; k < vs.Count - 1; k++)
                        {
                            float v0 = vs[k], v1 = vs[k + 1];
                            if (v1 - v0 < 1e-3f) continue;
                            float vm = (v0 + v1) * 0.5f;
                            bool hole = false;
                            foreach (var o in openings) if (mid > o.u0 && mid < o.u1 && vm > o.v0 && vm < o.v1) hole = true;
                            // Pieces butt edge to edge (no bevel on the wall's own pieces, so the joints don't show as seams).
                            if (!hole) framed.Add(WallBox("Wall piece", wall, origin, rot, a, c, v0, v1, 0f, Th, s.siding, 0.0005f, true));
                        }
                    }
                }
                foreach (var o in openings) Dress(wall, origin, rot, o);
            }

            void Dress(Transform wall, Vector3 origin, Quaternion rot, Opening o)
            {
                bool full = lod == Lod.Full;
                const float t = 0.1f, out_ = -0.05f;
                WallBox("Trim L", wall, origin, rot, o.u0 - t, o.u0, o.v0 - (o.door ? 0f : t), o.v1 + t, out_, 0.06f, s.trim, 0.01f, false);
                WallBox("Trim R", wall, origin, rot, o.u1, o.u1 + t, o.v0 - (o.door ? 0f : t), o.v1 + t, out_, 0.06f, s.trim, 0.01f, false);
                WallBox("Trim head", wall, origin, rot, o.u0 - t - 0.04f, o.u1 + t + 0.04f, o.v1 + t, o.v1 + t + 0.08f, out_ - 0.03f, 0.06f, s.trim, 0.012f, false);
                if (s.shutters && !o.door)
                {
                    float sw = (o.u1 - o.u0) * 0.5f;
                    var sh = VhLookLibrary.Paint("Shutter" + ColorKey(s.shutterColor), s.shutterColor, 1.3f);
                    WallBox("Shutter L", wall, origin, rot, o.u0 - t - sw, o.u0 - t - 0.02f, o.v0, o.v1, -0.06f, -0.02f, sh, 0.01f, false);
                    WallBox("Shutter R", wall, origin, rot, o.u1 + t + 0.02f, o.u1 + t + sw, o.v0, o.v1, -0.06f, -0.02f, sh, 0.01f, false);
                }
                if (o.door)
                {
                    float depth = full ? 0.1f : -0.02f;
                    WallBox("Door", wall, origin, rot, o.u0 + 0.02f, o.u1 - 0.02f, o.v0, o.v1 - 0.02f, depth, depth + 0.05f, s.door, 0.01f, full);
                    float pw = (o.u1 - o.u0 - 0.3f) * 0.5f;
                    for (int r = 0; r < 2; r++)
                        for (int c = 0; c < 2; c++)
                        {
                            float pu = o.u0 + 0.1f + c * (pw + 0.1f);
                            float pv = o.v0 + 0.15f + r * 1.0f;
                            WallBox("Door panel", wall, origin, rot, pu, pu + pw, pv, pv + 0.8f, depth - 0.015f, depth, s.door, 0.015f, false);
                        }
                    WallBox("Door knob", wall, origin, rot, o.u1 - 0.16f, o.u1 - 0.1f, o.v0 + 0.95f, o.v0 + 1.01f, depth - 0.07f, depth, brass, 0.02f, false);
                    if (full)
                    {
                        WallBox("Reveal L", wall, origin, rot, o.u0, o.u0 + 0.02f, o.v0, o.v1, 0f, Th, s.trim, 0.002f, false);
                        WallBox("Reveal R", wall, origin, rot, o.u1 - 0.02f, o.u1, o.v0, o.v1, 0f, Th, s.trim, 0.002f, false);
                        WallBox("Reveal top", wall, origin, rot, o.u0, o.u1, o.v1 - 0.02f, o.v1, 0f, Th, s.trim, 0.002f, false);
                    }
                    return;
                }
                WallBox("Sill", wall, origin, rot, o.u0 - 0.14f, o.u1 + 0.14f, o.v0 - 0.07f, o.v0, -0.12f, 0.08f, s.trim, 0.015f, false);
                float uc = (o.u0 + o.u1) * 0.5f, vc = (o.v0 + o.v1) * 0.5f;
                if (!full)
                {
                    // Far: the pane sits just proud of the solid wall, dark and glossy (it reflects the sky), with its sashes.
                    WallBox("Pane", wall, origin, rot, o.u0, o.u1, o.v0, o.v1, -0.025f, -0.005f, dark, 0.002f, false);
                    WallBox("Meeting rail", wall, origin, rot, o.u0, o.u1, vc - 0.03f, vc + 0.03f, -0.045f, -0.02f, s.trim, 0.005f, false);
                    WallBox("Muntin", wall, origin, rot, uc - 0.02f, uc + 0.02f, o.v0, o.v1, -0.04f, -0.02f, s.trim, 0.004f, false);
                    return;
                }
                WallBox("Reveal L", wall, origin, rot, o.u0, o.u0 + 0.02f, o.v0, o.v1, 0f, Th, s.trim, 0.002f, false);
                WallBox("Reveal R", wall, origin, rot, o.u1 - 0.02f, o.u1, o.v0, o.v1, 0f, Th, s.trim, 0.002f, false);
                WallBox("Reveal top", wall, origin, rot, o.u0, o.u1, o.v1 - 0.02f, o.v1, 0f, Th, s.trim, 0.002f, false);
                WallBox("Reveal bottom", wall, origin, rot, o.u0, o.u1, o.v0, o.v0 + 0.02f, 0f, Th, s.trim, 0.002f, false);
                WallBox("Glass", wall, origin, rot, o.u0, o.u1, o.v0, o.v1, 0.11f, 0.12f, glass, 0.001f, false);
                // A sheer curtain over both sashes (pass 4: round 3 still saw blocks in the upper sash): the room is a soft glow.
                WallBox("Sheer", wall, origin, rot, o.u0 + 0.02f, o.u1 - 0.02f, o.v0 + 0.04f, o.v1 - 0.04f, 0.16f, 0.165f, sheer, 0.001f, false);
                WallBox("Meeting rail", wall, origin, rot, o.u0, o.u1, vc - 0.03f, vc + 0.03f, 0.07f, 0.11f, s.trim, 0.005f, false);
                WallBox("Muntin v", wall, origin, rot, uc - 0.015f, uc + 0.015f, o.v0, o.v1, 0.085f, 0.11f, s.trim, 0.004f, false);
                for (int m = 1; m <= 2; m++)
                {
                    float vv = Mathf.Lerp(o.v0, vc, m / 3f), vv2 = Mathf.Lerp(vc, o.v1, m / 3f);
                    WallBox("Muntin h", wall, origin, rot, o.u0, o.u1, vv - 0.012f, vv + 0.012f, 0.09f, 0.11f, s.trim, 0.003f, false);
                    WallBox("Muntin h", wall, origin, rot, o.u0, o.u1, vv2 - 0.012f, vv2 + 0.012f, 0.09f, 0.11f, s.trim, 0.003f, false);
                }
                WallBox("Sash", wall, origin, rot, o.u0, o.u1, o.v0, o.v0 + 0.06f, 0.07f, 0.11f, s.trim, 0.005f, false);
                float cw = (o.u1 - o.u0) * 0.26f;
                WallBox("Curtain L", wall, origin, rot, o.u0 + 0.02f, o.u0 + cw, o.v0 + 0.05f, o.v1, Th + 0.05f, Th + 0.08f, curtain, 0.01f, false);
                WallBox("Curtain R", wall, origin, rot, o.u1 - cw, o.u1 - 0.02f, o.v0 + 0.05f, o.v1, Th + 0.05f, Th + 0.08f, curtain, 0.01f, false);
            }

            // ---------- The inside (Full): shell, floors, a dividing wall, furniture silhouettes ----------

            void Interior(List<Opening> front)
            {
                var inside = new GameObject("Interior").transform;
                inside.SetParent(root, false);
                var shellMesh = VhLookUv.BevelBox(new Vector3(W - 2f * Th - 0.04f, H - 0.04f, D - 2f * Th - 0.04f), 0.01f);
                Invert(shellMesh);
                var shell = new GameObject("Interior shell");
                shell.transform.SetParent(inside, false);
                shell.transform.localPosition = new Vector3(W * 0.5f, H * 0.5f, D * 0.5f);
                shell.AddComponent<MeshFilter>().sharedMesh = shellMesh;
                shell.AddComponent<MeshRenderer>().sharedMaterial = interior;
                for (int f = 1; f < s.storeys; f++)
                    Box("Floor " + f, new Vector3(Th, f * s.storeyHeight - 0.1f, Th), new Vector3(W - Th, f * s.storeyHeight + 0.1f, D - Th), interior, 0.01f, false, inside);
                // A wall dividing the front rooms from the back ones (the rooms read as rooms, not one empty box).
                Box("Dividing wall", new Vector3(Th, 0f, D * 0.5f - 0.06f), new Vector3(W - Th, H, D * 0.5f + 0.06f), interior, 0.01f, false, inside);
                var wood = VhLookLibrary.Paint("FurnitureWood", new Color(0.36f, 0.24f, 0.16f), 1.3f);
                var fabric = VhLookLibrary.Paint("FurnitureFabric", new Color(0.46f, 0.36f, 0.3f), 0.6f);
                var fabric2 = VhLookLibrary.Paint("FurnitureFabricBlue", new Color(0.3f, 0.34f, 0.42f), 0.6f);
                var shade = VhLookLibrary.Paint("LampShade", new Color(1f, 0.86f, 0.62f), 0.5f);
                int k = 0;
                foreach (var o in front)
                {
                    if (o.door) continue;
                    float uc = (o.u0 + o.u1) * 0.5f;
                    int floor = Mathf.FloorToInt((o.v0 + 0.01f) / s.storeyHeight);
                    float y = floor * s.storeyHeight + (floor > 0 ? 0.1f : 0f);
                    float z = Th + 1.1f + (k % 2) * 0.5f;
                    switch ((k + s.seed) % 4)
                    {
                        case 0:   // a sofa, back to the window
                            Box("Sofa", new Vector3(uc - 1f, y, z), new Vector3(uc + 1f, y + 0.45f, z + 0.85f), fabric, 0.08f, false, inside);
                            Box("Sofa back", new Vector3(uc - 1f, y, z + 0.65f), new Vector3(uc + 1f, y + 0.85f, z + 0.9f), fabric, 0.08f, false, inside);
                            break;
                        case 1:   // a floor lamp and an armchair
                            Box("Lamp pole", new Vector3(uc + 0.55f, y, z - 0.02f), new Vector3(uc + 0.59f, y + 1.45f, z + 0.02f), brass, 0.01f, false, inside);
                            Box("Lamp shade", new Vector3(uc + 0.37f, y + 1.45f, z - 0.2f), new Vector3(uc + 0.77f, y + 1.75f, z + 0.2f), shade, 0.05f, false, inside);
                            Box("Armchair", new Vector3(uc - 0.7f, y, z + 0.2f), new Vector3(uc + 0.1f, y + 0.9f, z + 1.0f), fabric2, 0.1f, false, inside);
                            break;
                        case 2:   // a bookcase on the dividing wall, a table
                            Box("Bookcase", new Vector3(uc - 0.6f, y, D * 0.5f - 0.42f), new Vector3(uc + 0.6f, y + 2.0f, D * 0.5f - 0.08f), wood, 0.02f, false, inside);
                            for (int sh = 1; sh <= 4; sh++)
                                Box("Books", new Vector3(uc - 0.55f, y + sh * 0.42f - 0.3f, D * 0.5f - 0.38f), new Vector3(uc + 0.5f, y + sh * 0.42f - 0.05f, D * 0.5f - 0.12f),
                                    sh % 2 == 0 ? fabric2 : fabric, 0.01f, false, inside);
                            Box("Table", new Vector3(uc - 0.6f, y + 0.72f, z + 0.3f), new Vector3(uc + 0.6f, y + 0.77f, z + 1.1f), wood, 0.01f, false, inside);
                            Box("Table legs", new Vector3(uc - 0.05f, y, z + 0.65f), new Vector3(uc + 0.05f, y + 0.72f, z + 0.75f), wood, 0.01f, false, inside);
                            break;
                        default:  // a bed headboard and a dresser (upstairs), or a sideboard (downstairs)
                            Box("Dresser", new Vector3(uc - 0.7f, y, D * 0.5f - 0.6f), new Vector3(uc + 0.7f, y + 1.0f, D * 0.5f - 0.08f), wood, 0.02f, false, inside);
                            Box("Picture", new Vector3(uc - 0.35f, y + 1.4f, D * 0.5f - 0.1f), new Vector3(uc + 0.35f, y + 1.9f, D * 0.5f - 0.07f), fabric2, 0.01f, false, inside);
                            break;
                    }
                    k++;
                }
            }

            // ---------- The roof: a slab with thickness, shingles on top, painted soffit underneath ----------

            void Roof()
            {
                float over = s.overhang, rise = s.roofRise, t = s.roofThickness;
                float hw = W * 0.5f + over, hd = D * 0.5f + over;
                var mesh = GableSlab(hw, hd, rise, t);
                var go = new GameObject("Roof");
                go.transform.SetParent(root, false);
                go.transform.localPosition = new Vector3(W * 0.5f, H, D * 0.5f);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = new[] { s.roof, s.trim };
                if (lod == Lod.Full) go.AddComponent<MeshCollider>().sharedMesh = mesh;
                // Ridge cap.
                Box("Ridge cap", new Vector3(-over, H + rise - 0.02f, D * 0.5f - 0.12f), new Vector3(W + over, H + rise + 0.07f, D * 0.5f + 0.12f), s.roof, 0.03f, false);
                // Gable ends: up to the roof's underside at the wall line (no gap), in the siding (or the gable material).
                float slope = rise / hd;
                float underAtWall = over * slope - t + 0.02f;
                foreach (var x in new[] { 0f, W })
                {
                    var g = GablePrism(D, underAtWall, rise - t + 0.02f, Th, x == 0f);
                    var gg = new GameObject("Gable");
                    gg.transform.SetParent(root, false);
                    gg.transform.localPosition = new Vector3(x, H, D * 0.5f);
                    gg.AddComponent<MeshFilter>().sharedMesh = g;
                    gg.AddComponent<MeshRenderer>().sharedMaterial = s.gable ?? s.siding;
                    framed.Add(gg);
                }
                // Gutters along the two eaves only (horizontal), just under the roof's edge, outside the fascia.
                foreach (var sz in new[] { -1f, 1f })
                {
                    float ez = D * 0.5f + sz * hd;
                    float z0 = sz < 0 ? ez - 0.14f : ez, z1 = sz < 0 ? ez : ez + 0.14f;
                    Box("Gutter", new Vector3(-over, H - t - 0.13f, z0), new Vector3(W + over, H - t + 0.03f, z1), gutter, 0.035f, false);
                    // A downspout at one end of each gutter: an elbow back to the wall, then straight down the corner.
                    // Just inside the corner board (front-left and back-right corners).
                    float dx = sz < 0 ? 0.14f : W - 0.22f;
                    float wallZ = sz < 0 ? -0.1f : D + 0.1f;
                    float zOut = (z0 + z1) * 0.5f;
                    Box("Downspout elbow", new Vector3(dx, H - t - 0.24f, Mathf.Min(zOut, wallZ) - 0.04f), new Vector3(dx + 0.08f, H - t - 0.14f, Mathf.Max(zOut, wallZ) + 0.04f), gutter, 0.02f, false);
                    float gd = groundAt != null ? groundAt(dx, wallZ) : groundTop;
                    Box("Downspout", new Vector3(dx, gd + 0.14f, wallZ - 0.04f), new Vector3(dx + 0.08f, H - t - 0.16f, wallZ + 0.04f), gutter, 0.02f, false);
                    Box("Downspout shoe", new Vector3(dx, gd + 0.06f, wallZ + (sz < 0 ? -0.36f : -0.04f)), new Vector3(dx + 0.08f, gd + 0.16f, wallZ + (sz < 0 ? 0.04f : 0.36f)), gutter, 0.02f, false);
                }
            }

            void Chimney()
            {
                var brick = VhLookLibrary.Get("Brick_Red");
                float cx = W - Mathf.Min(2.6f, W * 0.22f), cz = D * 0.5f + 0.7f;
                float top = H + s.roofRise + 1.1f;
                Box("Chimney", new Vector3(cx - 0.5f, 0.5f, cz - 0.45f), new Vector3(cx + 0.5f, top, cz + 0.45f), brick, 0.02f, lod == Lod.Full);
                Box("Chimney cap", new Vector3(cx - 0.6f, top, cz - 0.55f), new Vector3(cx + 0.6f, top + 0.12f, cz + 0.55f), s.foundation, 0.02f, false);
                // Flashing where it comes through the roof (the roof's height at the chimney's downhill face).
                float hd = D * 0.5f + s.overhang;
                float roofY = H + s.roofRise * (1f - (cz - 0.45f - D * 0.5f) / hd);
                Box("Flashing", new Vector3(cx - 0.55f, roofY - 0.1f, cz - 0.5f), new Vector3(cx + 0.55f, roofY + 0.08f, cz + 0.5f), VhLookLibrary.Get("Galvanized_Corrugated"), 0.005f, false);
            }

            /// <summary>
            /// The porch (or a stoop) at the door of a wall: <paramref name="frame"/> is the wall's frame (null = the front
            /// wall: x along it, -z outward). Pass 3 (round 2: "a floating porch frame: posts and rail, no deck, no ground"): the
            /// skirt and the steps go down to the real ground in front of the door (pass 2 stopped them at the highest
            /// ground under the house, so a porch on the downhill side hung in the air), and the posts stand on the deck.
            /// </summary>
            void Porch(List<Opening> openings, Transform frame)
            {
                Opening door = default;
                bool found = false;
                foreach (var o in openings) if (o.door) { door = o; found = true; break; }
                if (!found) return;
                bool full = lod == Lod.Full;
                var f = frame != null ? frame : root;
                // The ground in this wall's frame (frame-local x, z -> house-frame height).
                float G(float x, float z)
                {
                    var h = root.InverseTransformPoint(f.TransformPoint(new Vector3(x, 0f, z)));
                    return groundAt != null ? groundAt(h.x, h.z) : groundTop;
                }
                GameObject B(string n, Vector3 min, Vector3 max, Material m, float bevel, bool col) => Box(n, min, max, m, bevel, col, f);
                if (!s.porch)
                {
                    // A concrete stoop and steps down to the ground in front of it.
                    float gS = Mathf.Min(G(door.u0, -1.1f), G(door.u1, -1.1f), G((door.u0 + door.u1) * 0.5f, -0.3f));
                    float rise = Mathf.Max(0.15f, -gS);
                    int n = Mathf.Max(1, Mathf.RoundToInt(rise / 0.18f) - 1);
                    float sh = rise / (n + 1);
                    B("Stoop", new Vector3(door.u0 - 0.35f, gS - 0.15f, -1.1f), new Vector3(door.u1 + 0.35f, 0f, 0f), s.foundation, 0.02f, true);
                    for (int st = 0; st < n; st++)
                        B("Step " + (st + 1), new Vector3(door.u0 - 0.2f, gS - 0.15f, -1.1f - 0.3f * (st + 1)), new Vector3(door.u1 + 0.2f, -sh * (st + 1), -1.1f - 0.3f * st), s.foundation, 0.02f, true);
                    return;
                }
                var deck = VhLookLibrary.Get("Deck_Planks");
                float px0 = door.u0 - 1.3f, px1 = door.u1 + 1.3f, pz0 = -2.4f;
                float gDeck = Mathf.Min(Mathf.Min(G(px0, pz0), G(px1, pz0)), Mathf.Min(G(px0, -0.1f), G(px1, -0.1f)));
                float stairZ = pz0 - 0.32f;
                float gStair = Mathf.Min(G(door.u0, stairZ), G(door.u1, stairZ));
                float riseToDoor = Mathf.Max(0.2f, -gStair);
                int steps = Mathf.Max(1, Mathf.RoundToInt(riseToDoor / 0.18f) - 1);
                float stepH = riseToDoor / (steps + 1);
                B("Porch deck", new Vector3(px0, -0.12f, pz0), new Vector3(px1, 0f, 0f), deck, 0.02f, true);
                B("Porch skirt", new Vector3(px0 + 0.05f, gDeck - 0.2f, pz0 + 0.05f), new Vector3(px1 - 0.05f, -0.12f, 0f), s.trim, 0.01f, false);
                for (int st = 0; st < steps; st++)
                {
                    float top = -stepH * (st + 1);
                    B("Step " + (st + 1), new Vector3(door.u0 - 0.4f, gStair - 0.2f, pz0 - 0.32f * (st + 1)), new Vector3(door.u1 + 0.4f, top, pz0 - 0.32f * st), s.foundation, 0.02f, true);
                }
                foreach (var postX in new[] { px0 + 0.1f, px1 - 0.1f })
                    B("Porch post", new Vector3(postX - 0.08f, 0f, pz0 + 0.02f), new Vector3(postX + 0.08f, 2.7f, pz0 + 0.18f), s.trim, 0.02f, full);
                B("Porch beam", new Vector3(px0, 2.7f, pz0), new Vector3(px1, 2.9f, pz0 + 0.2f), s.trim, 0.015f, false);
                var pr = GableSlab((px1 - px0) * 0.5f + 0.25f, 1.45f, 0.6f, 0.12f);
                var prg = new GameObject("Porch roof");
                prg.transform.SetParent(f, false);
                prg.transform.localPosition = new Vector3((px0 + px1) * 0.5f, 2.9f, -1.2f);
                prg.AddComponent<MeshFilter>().sharedMesh = pr;
                prg.AddComponent<MeshRenderer>().sharedMaterials = new[] { s.roof, s.trim };
                if (!full) return;
                foreach (var rx in new[] { (px0, door.u0 - 0.4f), (door.u1 + 0.4f, px1) })
                {
                    B("Rail", new Vector3(rx.Item1, 0.85f, pz0 + 0.05f), new Vector3(rx.Item2, 0.92f, pz0 + 0.12f), s.trim, 0.01f, false);
                    for (float bx = rx.Item1 + 0.1f; bx < rx.Item2 - 0.05f; bx += 0.14f)
                        B("Baluster", new Vector3(bx - 0.02f, 0f, pz0 + 0.065f), new Vector3(bx + 0.02f, 0.85f, pz0 + 0.105f), s.trim, 0.004f, false);
                }
            }

            // ---------- Primitives in the house's frame ----------

            GameObject Box(string name, Vector3 min, Vector3 max, Material m, float bevel, bool collider, Transform parent = null)
            {
                var size = max - min;
                size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
                var go = new GameObject(name);
                go.transform.SetParent(parent != null ? parent : root, false);
                go.transform.localPosition = (min + max) * 0.5f;
                go.AddComponent<MeshFilter>().sharedMesh = VhLookUv.BevelBox(size, bevel);
                go.AddComponent<MeshRenderer>().sharedMaterial = m;
                if (collider && lod == Lod.Full) go.AddComponent<BoxCollider>().size = size;
                return go;
            }

            GameObject WallBox(string name, Transform parent, Vector3 origin, Quaternion rot, float u0, float u1, float v0, float v1,
                float w0, float w1, Material m, float bevel, bool collider)
            {
                var size = new Vector3(Mathf.Abs(u1 - u0), Mathf.Abs(v1 - v0), Mathf.Abs(w1 - w0));
                var c = new Vector3((u0 + u1) * 0.5f, (v0 + v1) * 0.5f, (w0 + w1) * 0.5f);
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = origin + rot * c;
                go.transform.localRotation = rot;
                go.AddComponent<MeshFilter>().sharedMesh = VhLookUv.BevelBox(size, bevel);
                go.AddComponent<MeshRenderer>().sharedMaterial = m;
                if (collider && lod == Lod.Full) go.AddComponent<BoxCollider>().size = size;
                return go;
            }
        }

        // ------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// A gable roof slab, ridge along x, centred on the origin at its eaves' top edge: top surfaces (submesh 0, shingles,
        /// v up the slope), underside, eave and rake edges (submesh 1, painted). Every face is wound to its own outward normal.
        /// </summary>
        public static Mesh GableSlab(float hw, float hd, float rise, float t)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>();
            var top = new List<int>(); var rest = new List<int>();
            float slope = Mathf.Sqrt(hd * hd + rise * rise);
            void Quad(List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
            {
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                for (int k = 0; k < 4; k++) n.Add(normal);
                uv.Add(ua); uv.Add(ub); uv.Add(uc); uv.Add(ud);
                bool flip = Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f;
                if (!flip) { tris.Add(i); tris.Add(i + 1); tris.Add(i + 2); tris.Add(i); tris.Add(i + 2); tris.Add(i + 3); }
                else { tris.Add(i); tris.Add(i + 2); tris.Add(i + 1); tris.Add(i); tris.Add(i + 3); tris.Add(i + 2); }
            }
            var nS = new Vector3(0f, hd, -rise).normalized;   // the south (front, -z) slope's outward normal
            var nN = new Vector3(0f, hd, rise).normalized;
            // Tops.
            Quad(top, new Vector3(-hw, 0f, -hd), new Vector3(-hw, rise, 0f), new Vector3(hw, rise, 0f), new Vector3(hw, 0f, -hd), nS,
                new Vector2(-hw, 0f), new Vector2(-hw, slope), new Vector2(hw, slope), new Vector2(hw, 0f));
            Quad(top, new Vector3(-hw, 0f, hd), new Vector3(-hw, rise, 0f), new Vector3(hw, rise, 0f), new Vector3(hw, 0f, hd), nN,
                new Vector2(hw, 0f), new Vector2(hw, slope), new Vector2(-hw, slope), new Vector2(-hw, 0f));
            // Undersides (the soffit seen from the street), a thickness below.
            Quad(rest, new Vector3(-hw, -t, -hd), new Vector3(-hw, rise - t, 0f), new Vector3(hw, rise - t, 0f), new Vector3(hw, -t, -hd), -nS,
                new Vector2(-hw, 0f), new Vector2(-hw, slope), new Vector2(hw, slope), new Vector2(hw, 0f));
            Quad(rest, new Vector3(-hw, -t, hd), new Vector3(-hw, rise - t, 0f), new Vector3(hw, rise - t, 0f), new Vector3(hw, -t, hd), -nN,
                new Vector2(-hw, 0f), new Vector2(-hw, slope), new Vector2(hw, slope), new Vector2(hw, 0f));
            // Eave edges (fascia).
            Quad(rest, new Vector3(-hw, -t, -hd), new Vector3(-hw, 0f, -hd), new Vector3(hw, 0f, -hd), new Vector3(hw, -t, -hd), Vector3.back,
                new Vector2(-hw, 0f), new Vector2(-hw, t), new Vector2(hw, t), new Vector2(hw, 0f));
            Quad(rest, new Vector3(-hw, -t, hd), new Vector3(-hw, 0f, hd), new Vector3(hw, 0f, hd), new Vector3(hw, -t, hd), Vector3.forward,
                new Vector2(-hw, 0f), new Vector2(-hw, t), new Vector2(hw, t), new Vector2(hw, 0f));
            // Rake edges (the slab's ends over the gables).
            foreach (var sx in new[] { -1f, 1f })
            {
                float x = sx * hw;
                var nx = new Vector3(sx, 0f, 0f);
                Quad(rest, new Vector3(x, -t, -hd), new Vector3(x, 0f, -hd), new Vector3(x, rise, 0f), new Vector3(x, rise - t, 0f), nx,
                    new Vector2(0f, 0f), new Vector2(0f, t), new Vector2(slope, t), new Vector2(slope, 0f));
                Quad(rest, new Vector3(x, -t, hd), new Vector3(x, 0f, hd), new Vector3(x, rise, 0f), new Vector3(x, rise - t, 0f), nx,
                    new Vector2(0f, 0f), new Vector2(0f, t), new Vector2(slope, t), new Vector2(slope, 0f));
            }
            var mesh = new Mesh { name = "Roof slab " + (hw * 2f).ToString("0.#") + "x" + (hd * 2f).ToString("0.#") };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(top, 0);
            mesh.SetTriangles(rest, 1);
            mesh.RecalculateTangents(); mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A gable end as a solid pentagonal prism in the YZ plane: base width d (z from -d/2 to d/2) at y = 0, its
        /// sides rising to <paramref name="shoulder"/> at the wall line and the apex at <paramref name="apex"/>, th thick
        /// (inward along +x for the left gable, -x for the right).</summary>
        static Mesh GablePrism(float d, float shoulder, float apex, float th, bool left)
        {
            float hd = d * 0.5f;
            var outline = new[] { new Vector2(-hd, 0f), new Vector2(-hd, shoulder), new Vector2(0f, apex), new Vector2(hd, shoulder), new Vector2(hd, 0f) };
            float xo = 0f, xi = left ? th : -th;
            var outN = left ? Vector3.left : Vector3.right;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tris = new List<int>();
            void Face(float x, Vector3 normal)
            {
                int b = v.Count;
                foreach (var p in outline) { v.Add(new Vector3(x, p.y, p.x)); n.Add(normal); uv.Add(new Vector2(p.x, p.y)); }
                int[] fan = { 0, 1, 2, 0, 2, 4, 2, 3, 4 };
                for (int i = 0; i < fan.Length; i += 3)
                {
                    var a = v[b + fan[i]]; var c1 = v[b + fan[i + 1]]; var c2 = v[b + fan[i + 2]];
                    bool flip = Vector3.Dot(Vector3.Cross(c1 - a, c2 - a), normal) < 0f;
                    tris.Add(b + fan[i]);
                    tris.Add(b + (flip ? fan[i + 2] : fan[i + 1]));
                    tris.Add(b + (flip ? fan[i + 1] : fan[i + 2]));
                }
            }
            Face(xo, outN);
            Face(xi, -outN);
            var m = new Mesh { name = "Gable " + d.ToString("0.#") };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tris, 0);
            m.RecalculateTangents(); m.RecalculateBounds();
            return m;
        }

        /// <summary>Rewrites a piece's UVs in metres in the house's own frame (u along the wall, v = height), so siding and
        /// brick courses line up across every piece, corner to corner and up into the gables.</summary>
        public static void FrameUVs(GameObject go, Transform frame)
        {
            var mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            var mesh = mf.sharedMesh;
            var verts = mesh.vertices;
            var normals = mesh.normals;
            var uvs = new Vector2[verts.Length];
            var t = go.transform;
            for (int i = 0; i < verts.Length; i++)
            {
                var p = frame.InverseTransformPoint(t.TransformPoint(verts[i]));
                var nn = frame.InverseTransformDirection(t.TransformDirection(normals[i]));
                var an = new Vector3(Mathf.Abs(nn.x), Mathf.Abs(nn.y), Mathf.Abs(nn.z));
                if (an.y >= an.x && an.y >= an.z) uvs[i] = new Vector2(p.x, p.z);
                else if (an.x >= an.z) uvs[i] = new Vector2(p.z, p.y);
                else uvs[i] = new Vector2(p.x, p.y);
            }
            mesh.uv = uvs;
            mesh.RecalculateTangents();
        }

        /// <summary>Clapboard (Clapboard_Painted, 2 m a repeat): 16 courses a repeat, the first lap line 0.115 m up.</summary>
        public const float CourseHeight = 0.125f, CoursePhase = 0.115f;

        /// <summary>
        /// Rewrites a siding piece's mesh in the house's frame, cut into horizontal bands one course high, each band's u
        /// shifted by its own amount (the same for that course all round the house, so the pieces of a wall still meet), so
        /// the texture's butt joints are staggered course by course instead of lining up. Up/down faces get plan UVs, as
        /// <see cref="FrameUVs"/> gives them.
        /// </summary>
        public static void SplitCourses(GameObject go, Transform frame, float courseH, float phase, int seed)
        {
            var mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            var src = mf.sharedMesh;
            var t = go.transform;
            var sv = src.vertices; var sn = src.normals; var st = src.triangles;
            var pv = new Vector3[sv.Length]; var pn = new Vector3[sv.Length];
            for (int i = 0; i < sv.Length; i++)
            {
                pv[i] = frame.InverseTransformPoint(t.TransformPoint(sv[i]));
                pn[i] = frame.InverseTransformDirection(t.TransformDirection(sn[i])).normalized;
            }
            var ov = new List<Vector3>(); var on = new List<Vector3>(); var ouv = new List<Vector2>(); var ot = new List<int>();
            var poly = new List<Vector3>(8); var polyN = new List<Vector3>(8);
            var tmp = new List<Vector3>(8); var tmpN = new List<Vector3>(8);
            float Shift(int k) => Mathf.Repeat(Mathf.Sin(k * 78.233f + seed * 12.9898f) * 43758.5453f, 1f) * 2f;
            Vector2 Uv(Vector3 p, Vector3 n, float shift)
            {
                var an = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
                if (an.y >= an.x && an.y >= an.z) return new Vector2(p.x, p.z);
                if (an.x >= an.z) return new Vector2(p.z + shift, p.y);
                return new Vector2(p.x + shift, p.y);
            }
            void Emit(List<Vector3> ps, List<Vector3> ns, float shift)
            {
                if (ps.Count < 3) return;
                int b = ov.Count;
                for (int i = 0; i < ps.Count; i++)
                {
                    ov.Add(t.InverseTransformPoint(frame.TransformPoint(ps[i])));
                    on.Add(t.InverseTransformDirection(frame.TransformDirection(ns[i])).normalized);
                    ouv.Add(Uv(ps[i], ns[i], shift));
                }
                for (int i = 1; i < ps.Count - 1; i++) { ot.Add(b); ot.Add(b + i); ot.Add(b + i + 1); }
            }
            // Keeps the part of a polygon on one side of the plane y = h (above if keepAbove).
            void Clip(List<Vector3> ps, List<Vector3> ns, float h, bool keepAbove, List<Vector3> outP, List<Vector3> outN)
            {
                outP.Clear(); outN.Clear();
                for (int i = 0; i < ps.Count; i++)
                {
                    var a = ps[i]; var b = ps[(i + 1) % ps.Count];
                    bool ina = keepAbove ? a.y >= h : a.y <= h, inb = keepAbove ? b.y >= h : b.y <= h;
                    if (ina) { outP.Add(a); outN.Add(ns[i]); }
                    if (ina != inb)
                    {
                        float k = (h - a.y) / (b.y - a.y);
                        outP.Add(Vector3.Lerp(a, b, k));
                        outN.Add(Vector3.Lerp(ns[i], ns[(i + 1) % ps.Count], k).normalized);
                    }
                }
            }
            for (int i = 0; i < st.Length; i += 3)
            {
                int a = st[i], b = st[i + 1], c = st[i + 2];
                var fn = (pn[a] + pn[b] + pn[c]).normalized;
                poly.Clear(); polyN.Clear();
                poly.Add(pv[a]); poly.Add(pv[b]); poly.Add(pv[c]);
                polyN.Add(pn[a]); polyN.Add(pn[b]); polyN.Add(pn[c]);
                if (Mathf.Abs(fn.y) > 0.5f) { Emit(poly, polyN, 0f); continue; }
                float y0 = Mathf.Min(pv[a].y, Mathf.Min(pv[b].y, pv[c].y)), y1 = Mathf.Max(pv[a].y, Mathf.Max(pv[b].y, pv[c].y));
                int k0 = Mathf.FloorToInt((y0 - phase) / courseH), k1 = Mathf.FloorToInt((y1 - phase) / courseH);
                if (k1 - k0 > 400) { Emit(poly, polyN, 0f); continue; }
                for (int k = k0; k <= k1; k++)
                {
                    float lo = phase + k * courseH, hi = lo + courseH;
                    Clip(poly, polyN, lo, true, tmp, tmpN);
                    var bandP = new List<Vector3>(tmp); var bandN = new List<Vector3>(tmpN);
                    Clip(bandP, bandN, hi, false, tmp, tmpN);
                    Emit(tmp, tmpN, Shift(k));
                }
            }
            var mesh = new Mesh { name = src.name + " (courses)" };
            if (ov.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(ov); mesh.SetNormals(on); mesh.SetUVs(0, ouv); mesh.SetTriangles(ot, 0);
            mesh.RecalculateTangents(); mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
        }

        static void Invert(Mesh m)
        {
            var t = m.triangles;
            for (int i = 0; i < t.Length; i += 3) { int k = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = k; }
            m.triangles = t;
            var n = m.normals;
            for (int i = 0; i < n.Length; i++) n[i] = -n[i];
            m.normals = n;
            m.RecalculateTangents();
        }

        /// <summary>Far houses: every renderer merged into one mesh per material (a hill of them is a few hundred draws, not
        /// thousands), and one box collider round the body.</summary>
        static void MergeByMaterial(GameObject root, Spec s)
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            var toDelete = new List<GameObject>();
            var inv = root.transform.worldToLocalMatrix;
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var mesh = mf.sharedMesh;
                var mats = mr.sharedMaterials;
                for (int sub = 0; sub < mesh.subMeshCount && sub < mats.Length; sub++)
                {
                    if (mats[sub] == null) continue;
                    if (!groups.TryGetValue(mats[sub], out var list)) groups[mats[sub]] = list = new List<CombineInstance>();
                    list.Add(new CombineInstance { mesh = mesh, subMeshIndex = sub, transform = inv * mr.transform.localToWorldMatrix });
                }
                toDelete.Add(mr.gameObject);
            }
            foreach (var go in toDelete) if (go != null && go != root) UnityEngine.Object.DestroyImmediate(go);
            // Drop the now-empty grouping objects.
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                var c = root.transform.GetChild(i);
                if (c.GetComponentsInChildren<Renderer>(true).Length == 0) UnityEngine.Object.DestroyImmediate(c.gameObject);
            }
            foreach (var kv in groups)
            {
                var merged = new Mesh { name = root.name + " " + kv.Key.name, indexFormat = IndexFormat.UInt32 };
                merged.CombineMeshes(kv.Value.ToArray(), true, true);
                merged.RecalculateBounds();
                var go = new GameObject("Merged " + kv.Key.name);
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = merged;
                go.AddComponent<MeshRenderer>().sharedMaterial = kv.Key;
            }
            // Far houses drop out past ~1.5 km (well into the haze).
            var lod = root.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(FarCull, root.GetComponentsInChildren<Renderer>(true)) });
            lod.RecalculateBounds();
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(s.width * 0.5f, s.WallHeight * 0.5f, s.depth * 0.5f);
            col.size = new Vector3(s.width, s.WallHeight, s.depth);
        }
    }
}

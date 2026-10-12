using System;
using System.Collections.Generic;
using JamKit.DeepBlock;
using UnityEngine;
using static JamKit.DreamEscape.EditorTools.HillKit;
using Random = System.Random;
using Side = JamKit.DreamEscape.EditorTools.HillMesh.Side;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// The hill's big buildings, drawn on the colliders <see cref="HillColliders.Building"/> makes: every wall's outer
    /// face on the body box, the church's nave roof on its two slopes and its bell tower in the tower box (the spire
    /// above it is drawn only, out of reach), the hospital's tower and entrance canopy and columns in theirs, shop
    /// awnings in their boxes, parapets on the parapet rings. Trim stands at most 0.12 m off a face.
    /// <para>Local frame: x along the front (to the right as you face it), z into the building from the front face
    /// (z = 0), y up from the floor.</para>
    /// </summary>
    public static class HillLandmarks
    {
        public static void Draw(HillMesh m, HillBuilding b)
        {
            if (b == null) return;
            var bl = new Builder(m, b);
            if (bl.FL < 1f || bl.D < 1f) return;
            m.Push(bl.FrameMatrix);
            try { bl.Draw(); }
            finally { m.Pop(); }
        }

        sealed class FaceJob
        {
            public int face;
            public float len;
            public string mat;
            public List<Vector2> outline;
            public readonly List<Rect> holes = new List<Rect>();
            public readonly List<Action> after = new List<Action>();
        }

        sealed class Builder
        {
            readonly HillMesh m;
            readonly HillBuilding b;
            readonly Random r;
            public readonly float FL, D, H;
            /// <summary>+1 when local x runs the same way as the world's along-the-front axis, -1 when it runs against it.</summary>
            readonly float sgn;
            public readonly Matrix4x4 FrameMatrix;

            public Builder(HillMesh m, HillBuilding b)
            {
                this.m = m;
                this.b = b;
                r = new Random(b.seed * 3301 + (int)b.kind * 97 + 5);
                var mn = U(b.min);
                var mx = U(b.max);
                bool alongX = b.facing == HillFacing.South || b.facing == HillFacing.North;
                FL = alongX ? mx.x - mn.x : mx.y - mn.y;
                D = alongX ? mx.y - mn.y : mx.x - mn.x;
                H = b.height;
                Vector3 origin;
                float yaw;
                switch (b.facing)
                {
                    case HillFacing.North: origin = new Vector3((mn.x + mx.x) * 0.5f, b.floorY, mx.y); yaw = 180f; sgn = -1f; break;
                    case HillFacing.East: origin = new Vector3(mx.x, b.floorY, (mn.y + mx.y) * 0.5f); yaw = -90f; sgn = 1f; break;
                    case HillFacing.West: origin = new Vector3(mn.x, b.floorY, (mn.y + mx.y) * 0.5f); yaw = 90f; sgn = -1f; break;
                    default: origin = new Vector3((mn.x + mx.x) * 0.5f, b.floorY, mn.y); yaw = 0f; sgn = 1f; break;
                }
                FrameMatrix = Matrix4x4.TRS(origin, Quaternion.Euler(0f, yaw, 0f), Vector3.one);
            }

            static string Or(string a, string fallback) => string.IsNullOrEmpty(a) ? fallback : a;

            public void Draw()
            {
                switch (b.kind)
                {
                    case HillBuildingKind.Church: Church(); break;
                    case HillBuildingKind.Hospital: Hospital(); break;
                    case HillBuildingKind.School: School(); break;
                    case HillBuildingKind.ShopRow: ShopRow(); break;
                    case HillBuildingKind.CornerShop: CornerShop(false); break;
                    case HillBuildingKind.BikeShop: CornerShop(true); break;
                    case HillBuildingKind.Garage: Garage(); break;
                    case HillBuildingKind.Boathouse: Boathouse(); break;
                    case HillBuildingKind.WaterTower: WaterTower(); break;
                    case HillBuildingKind.Kiosk: Kiosk(); break;
                    case HillBuildingKind.Treehouse: Treehouse(); break;
                    default: Terrace(); break;
                }
            }

            // ---------- Faces ----------

            // 0 front (z = 0, out -z), 1 right (x = FL/2, out +x), 2 back (z = D, out +z), 3 left (x = -FL/2, out -x).
            Vector3 FO(int f) => f == 0 ? new Vector3(-FL * 0.5f, 0f, 0f) : f == 1 ? new Vector3(FL * 0.5f, 0f, 0f) : f == 2 ? new Vector3(FL * 0.5f, 0f, D) : new Vector3(-FL * 0.5f, 0f, D);
            static Vector3 FA(int f) => f == 0 ? Vector3.right : f == 1 ? Vector3.forward : f == 2 ? Vector3.left : Vector3.back;
            static Vector3 FN(int f) => f == 0 ? Vector3.back : f == 1 ? Vector3.right : f == 2 ? Vector3.forward : Vector3.left;
            float FLen(int f) => f % 2 == 0 ? FL : D;

            FaceJob Job(int f, string mat, float y0, float y1)
            {
                float len = FLen(f);
                return new FaceJob { face = f, len = len, mat = mat, outline = new List<Vector2> { new Vector2(0f, y0), new Vector2(len, y0), new Vector2(len, y1), new Vector2(0f, y1) } };
            }

            void Run(FaceJob j)
            {
                m.Push(WallFrame(FO(j.face), FA(j.face), FN(j.face)));
                try
                {
                    WallFace(m, j.mat, j.outline, j.holes);
                    foreach (var a in j.after) a();
                }
                finally { m.Pop(); }
            }

            void AddWin(FaceJob j, Win w)
            {
                j.holes.Add(w.Hole);
                j.after.Add(() => Window(m, w, r));
            }

            void AddDoor(FaceJob j, DoorSpec d)
            {
                j.holes.Add(d.Hole);
                j.after.Add(() => Door(m, d, r));
            }

            Win Sash(float cx, float y0, float w, float h, string wall, int casing, string trim, int muntins, bool lit)
            {
                return new Win
                {
                    x0 = cx - w * 0.5f, x1 = cx + w * 0.5f, y0 = y0, y1 = y0 + h, depth = 0.14f, muntins = muntins, casing = casing,
                    wall = wall, reveal = wall, trim = trim, frame = trim, roomUV = HillAtlas.Interior(lit ? r.Next(7) : 7), roomFlip = Chance(r, 0.5f),
                    curtains = Chance(r, 0.4f), curtain = Chance(r, 0.5f) ? HillMat.Canvas : HillMat.TrimCream, roomDepth = R(r, 0.8f, 1.2f), headCap = true,
                };
            }

            /// <summary>A flat roof at the body's top, and a parapet ring (<paramref name="parapet"/> high,
            /// <paramref name="ring"/> thick) with its coping 4 cm over the outer faces (0: a metal flashing at the edge).</summary>
            void FlatTop(float parapet, float ring, string coping, string inner)
            {
                float y = H;
                m.Quad(HillMat.RoofFlat, new Vector3(-FL * 0.5f + ring, y, ring), new Vector3(FL * 0.5f - ring, y, ring), new Vector3(FL * 0.5f - ring, y, D - ring), new Vector3(-FL * 0.5f + ring, y, D - ring), Vector3.up);
                if (parapet <= 0f)
                {
                    m.Box(HillMat.Gutter, new Vector3(-FL * 0.5f - 0.04f, y - 0.12f, -0.04f), new Vector3(FL * 0.5f + 0.04f, y, 0.2f), 0.01f);
                    m.Box(HillMat.Gutter, new Vector3(-FL * 0.5f - 0.04f, y - 0.12f, D - 0.2f), new Vector3(FL * 0.5f + 0.04f, y, D + 0.04f), 0.01f);
                    m.Box(HillMat.Gutter, new Vector3(-FL * 0.5f - 0.04f, y - 0.12f, 0.2f), new Vector3(-FL * 0.5f + 0.2f, y, D - 0.2f), 0.01f);
                    m.Box(HillMat.Gutter, new Vector3(FL * 0.5f - 0.2f, y - 0.12f, 0.2f), new Vector3(FL * 0.5f + 0.04f, y, D - 0.2f), 0.01f);
                    return;
                }
                float top = y + parapet, cop = 0.08f, hx = FL * 0.5f;
                m.Quad(inner, new Vector3(-hx + ring, y, ring), new Vector3(hx - ring, y, ring), new Vector3(hx - ring, top - cop, ring), new Vector3(-hx + ring, top - cop, ring), Vector3.forward);
                m.Quad(inner, new Vector3(-hx + ring, y, D - ring), new Vector3(hx - ring, y, D - ring), new Vector3(hx - ring, top - cop, D - ring), new Vector3(-hx + ring, top - cop, D - ring), Vector3.back);
                m.Quad(inner, new Vector3(-hx + ring, y, ring), new Vector3(-hx + ring, y, D - ring), new Vector3(-hx + ring, top - cop, D - ring), new Vector3(-hx + ring, top - cop, ring), Vector3.right);
                m.Quad(inner, new Vector3(hx - ring, y, ring), new Vector3(hx - ring, y, D - ring), new Vector3(hx - ring, top - cop, D - ring), new Vector3(hx - ring, top - cop, ring), Vector3.left);
                m.Box(coping, new Vector3(-hx - 0.04f, top - cop, -0.04f), new Vector3(hx + 0.04f, top, ring + 0.03f), 0.012f);
                m.Box(coping, new Vector3(-hx - 0.04f, top - cop, D - ring - 0.03f), new Vector3(hx + 0.04f, top, D + 0.04f), 0.012f);
                m.Box(coping, new Vector3(-hx - 0.04f, top - cop, ring + 0.03f), new Vector3(-hx + ring + 0.03f, top, D - ring - 0.03f), 0.012f);
                m.Box(coping, new Vector3(hx - ring - 0.03f, top - cop, ring + 0.03f), new Vector3(hx + 0.04f, top, D - ring - 0.03f), 0.012f);
            }

            /// <summary>A plinth band along a face (call inside the face's frame).</summary>
            void PlinthBand(float len, float top = 0.35f, string mat = HillMat.Plinth) =>
                m.Box(mat, new Vector3(-0.05f, -1.4f, 0f), new Vector3(len, top, 0.05f), 0.012f, Side.ZNeg);

            void Downpipe(float s, float yTop)
            {
                m.Cylinder(HillMat.Gutter, new Vector3(s, -1f, 0.065f), new Vector3(s, yTop - 0.25f, 0.065f), 0.045f, 6, false);
                m.Box(HillMat.Gutter, new Vector3(s - 0.12f, yTop - 0.3f, 0f), new Vector3(s + 0.12f, yTop - 0.08f, 0.115f), 0.01f, Side.ZNeg);
                for (float y = 1.4f; y < yTop - 0.6f; y += 2.4f)
                    m.Box(HillMat.Gutter, new Vector3(s - 0.06f, y, 0f), new Vector3(s + 0.06f, y + 0.04f, 0.115f), 0f, Side.ZNeg);
            }

            /// <summary>A recessed band of glazing (ribbon windows, curtain walling) in a face frame: reveals, glass,
            /// mullions every <paramref name="mull"/> m, a transom, and the rooms behind it bay by bay.</summary>
            void Band(float x0, float x1, float y0, float y1, float recess, string glass, string frame, string reveal, float mull, string roomMat, Func<int, Rect> roomUV, float roomDepth, float transomAt = -1f)
            {
                m.Quad(reveal, new Vector3(x0, y1, 0f), new Vector3(x1, y1, 0f), new Vector3(x1, y1, -recess), new Vector3(x0, y1, -recess), Vector3.down);
                m.Quad(reveal, new Vector3(x0, y0, 0f), new Vector3(x1, y0, 0f), new Vector3(x1, y0, -recess), new Vector3(x0, y0, -recess), Vector3.up);
                m.Quad(reveal, new Vector3(x0, y0, 0f), new Vector3(x0, y1, 0f), new Vector3(x0, y1, -recess), new Vector3(x0, y0, -recess), Vector3.right);
                m.Quad(reveal, new Vector3(x1, y0, 0f), new Vector3(x1, y1, 0f), new Vector3(x1, y1, -recess), new Vector3(x1, y0, -recess), Vector3.left);
                float gz = -recess + 0.02f;
                m.Quad(glass, new Vector3(x0, y0, gz), new Vector3(x0, y1, gz), new Vector3(x1, y1, gz), new Vector3(x1, y0, gz), Vector3.forward);
                int n = Mathf.Max(1, Mathf.RoundToInt((x1 - x0) / mull));
                for (int i = 0; i <= n; i++)
                {
                    float x = Mathf.Lerp(x0 + 0.03f, x1 - 0.03f, i / (float)n);
                    m.Box(frame, new Vector3(x - 0.03f, y0, -recess - 0.01f), new Vector3(x + 0.03f, y1, -recess + 0.06f), 0f, Side.ZNeg | Side.YNeg | Side.YPos);
                }
                m.Box(frame, new Vector3(x0, y0, -recess - 0.01f), new Vector3(x1, y0 + 0.05f, -recess + 0.06f), 0f, Side.ZNeg | Side.XNeg | Side.XPos);
                m.Box(frame, new Vector3(x0, y1 - 0.05f, -recess - 0.01f), new Vector3(x1, y1, -recess + 0.06f), 0f, Side.ZNeg | Side.XNeg | Side.XPos);
                if (transomAt > 0f && transomAt < y1 - 0.2f)
                    m.Box(frame, new Vector3(x0, transomAt - 0.025f, -recess - 0.01f), new Vector3(x1, transomAt + 0.025f, -recess + 0.05f), 0f, Side.ZNeg | Side.XNeg | Side.XPos);
                if (roomMat != null) RoomStrip(roomMat, roomUV, x0, x1, y0, y1, -recess - 0.015f, roomDepth);
            }

            /// <summary>Rooms behind a long opening, bay by bay (no overlaps: each bay's back wall abuts the next).</summary>
            void RoomStrip(string mat, Func<int, Rect> uvOf, float x0, float x1, float y0, float y1, float zNear, float depth)
            {
                float bayW = Mathf.Max(1.2f, (y1 - y0) * 1.6f);
                int n = Mathf.Max(1, Mathf.RoundToInt((x1 - x0) / bayW));
                float zf = zNear - depth, ey = Mathf.Min(depth * 0.25f, (y1 - y0) * 0.3f);
                for (int i = 0; i < n; i++)
                {
                    float a = Mathf.Lerp(x0, x1, i / (float)n), c = Mathf.Lerp(x0, x1, (i + 1) / (float)n);
                    var uv = uvOf(i);
                    bool flip = i % 2 == 1;
                    float u0 = flip ? uv.xMax : uv.xMin, u1 = flip ? uv.xMin : uv.xMax, sv = uv.height * 0.07f;
                    m.PolyUV(mat, new[] { new Vector3(a, y0 - ey, zf), new Vector3(a, y1 + ey, zf), new Vector3(c, y1 + ey, zf), new Vector3(c, y0 - ey, zf) },
                        new[] { new Vector2(u0, uv.yMin), new Vector2(u0, uv.yMax), new Vector2(u1, uv.yMax), new Vector2(u1, uv.yMin) }, Vector3.forward);
                    m.PolyUV(mat, new[] { new Vector3(a, y1, zNear), new Vector3(c, y1, zNear), new Vector3(c, y1 + ey, zf), new Vector3(a, y1 + ey, zf) },
                        new[] { new Vector2(u0, uv.yMax - sv), new Vector2(u1, uv.yMax - sv), new Vector2(u1, uv.yMax), new Vector2(u0, uv.yMax) }, Vector3.down);
                    m.PolyUV(mat, new[] { new Vector3(a, y0, zNear), new Vector3(c, y0, zNear), new Vector3(c, y0 - ey, zf), new Vector3(a, y0 - ey, zf) },
                        new[] { new Vector2(u0, uv.yMin + sv), new Vector2(u1, uv.yMin + sv), new Vector2(u1, uv.yMin), new Vector2(u0, uv.yMin) }, Vector3.up);
                    if (i == 0 || i == n - 1)
                    {
                        float ex = i == 0 ? a : c;
                        float uu = i == 0 ? u0 : u1;
                        m.PolyUV(mat, new[] { new Vector3(ex, y0, zNear), new Vector3(ex, y1, zNear), new Vector3(ex, y1 + ey, zf), new Vector3(ex, y0 - ey, zf) },
                            new[] { new Vector2(uu, uv.yMin), new Vector2(uu, uv.yMax), new Vector2(uu, uv.yMax), new Vector2(uu, uv.yMin) }, i == 0 ? Vector3.right : Vector3.left);
                    }
                }
            }

            // ---------- Church ----------

            void Church()
            {
                string stone = Or(b.wall, HillMat.ChurchStone), slate = Or(b.roofMat, HillMat.RoofSlate);
                float hx = FL * 0.5f, ridge = H + FL * 0.5f * 0.9f, eaveX = hx + 0.4f, low = H - 0.35f, t = 0.28f;
                float G = (ridge - low) / eaveX;
                float RoofAt(float x) => low + G * (eaveX - Mathf.Abs(x));
                // The nave's sides: lancets between buttresses.
                int bays = Mathf.Max(2, Mathf.FloorToInt((D - 1.5f) / 3.6f));
                foreach (int f in new[] { 1, 3 })
                {
                    var j = Job(f, stone, -1.2f, H);
                    float bayW = (D - 1.5f) / bays;
                    float spring = Mathf.Min(H - 2.0f, 2.1f + 3.2f), w = Mathf.Min(1.1f, bayW * 0.4f), sill = Mathf.Min(2.1f, spring - 1.5f);
                    for (int i = 0; i < bays; i++)
                    {
                        float s = 0.75f + bayW * (i + 0.5f);
                        float x0 = s - w * 0.5f, x1 = s + w * 0.5f, apex = spring + w * 0.866f;
                        j.holes.Add(new Rect(x0, sill, w, apex - sill));
                        j.after.Add(() =>
                        {
                            Lancet(m, x0, x1, sill, spring, 0.32f, stone, stone, HillMat.Glass, HillMat.Interior, HillAtlas.Interior(7));
                            Tracery(x0, x1, sill, spring, 0.32f);
                        });
                    }
                    j.after.Add(() =>
                    {
                        for (int k = 0; k <= bays; k++)
                        {
                            float s = Mathf.Clamp(0.75f + bayW * k, 0.3f, D - 0.3f);
                            Buttress(s, H - 1.7f);
                        }
                        PlinthBand(D, 0.45f, HillMat.StoneCoping);
                        m.Box(stone, new Vector3(-0.05f, H - 0.5f, 0f), new Vector3(D + 0.05f, H - 0.3f, 0.1f), 0.02f, Side.ZNeg);
                        Downpipe(0.4f, low);
                        Downpipe(D - 0.4f, low);
                    });
                    Run(j);
                }
                // The gables: the front either side of the tower, the back with a triple lancet and an oculus.
                foreach (int f in new[] { 0, 2 })
                {
                    var j = new FaceJob { face = f, len = FL, mat = stone };
                    j.outline = new List<Vector2> { new Vector2(0f, -1.2f), new Vector2(FL, -1.2f), new Vector2(FL, RoofAt(hx) - t * 0.5f), new Vector2(hx, ridge - t * 0.5f), new Vector2(0f, RoofAt(hx) - t * 0.5f) };
                    if (f == 0)
                    {
                        float side = (hx - 3f) * 0.5f;
                        if (side > 1.2f)
                            foreach (float s in new[] { side, FL - side })
                            {
                                float x0 = s - 0.4f, x1 = s + 0.4f, sill = 2.4f, spring = Mathf.Min(H - 1.2f, 4.6f);
                                j.holes.Add(new Rect(x0, sill, 0.8f, spring + 0.8f * 0.866f - sill));
                                j.after.Add(() => Lancet(m, x0, x1, sill, spring, 0.3f, stone, stone, HillMat.Glass, HillMat.Interior, HillAtlas.Interior(7)));
                            }
                    }
                    else
                    {
                        float[] cs = { hx - 1.55f, hx, hx + 1.55f };
                        for (int i = 0; i < 3; i++)
                        {
                            float w = i == 1 ? 1.3f : 0.95f, x0 = cs[i] - w * 0.5f, x1 = cs[i] + w * 0.5f;
                            float spring = i == 1 ? H - 0.9f : H - 1.9f, sill = 2.2f;
                            j.holes.Add(new Rect(x0, sill, w, spring + w * 0.866f - sill));
                            j.after.Add(() =>
                            {
                                Lancet(m, x0, x1, sill, spring, 0.32f, stone, stone, HillMat.Glass, HillMat.Interior, HillAtlas.Interior(7));
                                Tracery(x0, x1, sill, spring, 0.32f);
                            });
                        }
                        float oy = H + (ridge - H) * 0.42f, orr = Mathf.Min(0.7f, (ridge - H) * 0.2f);
                        if (orr > 0.3f)
                        {
                            j.holes.Add(new Rect(hx - orr, oy - orr, orr * 2f, orr * 2f));
                            j.after.Add(() => Rose(hx, oy, orr, stone, 0.25f, false));
                        }
                    }
                    j.after.Add(() =>
                    {
                        PlinthBand(FL, 0.45f, HillMat.StoneCoping);
                        Buttress(0.35f, H - 1.7f);
                        Buttress(FL - 0.35f, H - 1.7f);
                    });
                    Run(j);
                }
                // The nave roof on the collider's two slopes; coped gables and a ridge.
                var left = new[] { new Vector3(-eaveX, low, 0f), new Vector3(0f, ridge, 0f), new Vector3(0f, ridge, D), new Vector3(-eaveX, low, D) };
                var right = new[] { new Vector3(eaveX, low, D), new Vector3(0f, ridge, D), new Vector3(0f, ridge, 0f), new Vector3(eaveX, low, 0f) };
                Slab(m, left, new[] { true, false, true, true }, slate, HillMat.FenceWood, HillMat.StoneCoping, t);
                Slab(m, right, new[] { true, false, true, true }, slate, HillMat.FenceWood, HillMat.StoneCoping, t);
                m.Beam(HillMat.StoneCoping, new Vector3(0f, ridge + 0.01f, -0.05f), new Vector3(0f, ridge + 0.01f, D + 0.05f), 0.3f, 0.1f, 0.015f);
                foreach (float z in new[] { 0.12f, D - 0.12f })
                    foreach (int sx in new[] { -1, 1 })
                        m.Beam(HillMat.StoneCoping, new Vector3(sx * eaveX, low, z), new Vector3(0f, ridge, z), 0.4f, 0.18f, 0.02f);
                foreach (int sx in new[] { -1, 1 })
                {
                    m.Cylinder(HillMat.Gutter, new Vector3(sx * (eaveX + 0.06f), low - 0.07f, -0.02f), new Vector3(sx * (eaveX + 0.06f), low - 0.07f, D + 0.02f), 0.06f, 6, true);
                    // Corbels under the eaves.
                    for (float z = 0.5f; z < D - 0.3f; z += 0.9f)
                        m.Box(stone, new Vector3(sx > 0 ? hx : -hx - 0.1f, H - 0.62f, z - 0.09f), new Vector3(sx > 0 ? hx + 0.1f : -hx, H - 0.5f, z + 0.09f), 0.01f);
                }
                // A cross on the back gable's apex.
                m.Box(HillMat.StoneCoping, new Vector3(-0.07f, ridge, D - 0.25f), new Vector3(0.07f, ridge + 1.2f, D - 0.1f), 0.01f);
                m.Box(HillMat.StoneCoping, new Vector3(-0.4f, ridge + 0.7f, D - 0.25f), new Vector3(0.4f, ridge + 0.84f, D - 0.1f), 0.01f);
                ChurchTower(stone, ridge);
            }

            /// <summary>A shallow buttress (0.12 proud) with a sloped set-off, in a face frame.</summary>
            void Buttress(float s, float top)
            {
                string stone = Or(b.wall, HillMat.ChurchStone);
                m.Box(stone, new Vector3(s - 0.32f, -1.2f, 0f), new Vector3(s + 0.32f, top, 0.12f), 0.02f, Side.ZNeg | Side.YPos);
                Extrude(m, HillMat.StoneCoping, new[] { new Vector2(0f, top), new Vector2(0.125f, top), new Vector2(0f, top + 0.5f) }, Vector3.forward, Vector3.up, Vector3.right, s - 0.33f, s + 0.33f);
                m.Box(HillMat.StoneCoping, new Vector3(s - 0.34f, 1.7f, 0f), new Vector3(s + 0.34f, 1.82f, 0.12f), 0.015f, Side.ZNeg);
            }

            /// <summary>Y tracery and leads in a lancet (face frame).</summary>
            void Tracery(float x0, float x1, float sill, float spring, float depth)
            {
                float w = x1 - x0, cx = (x0 + x1) * 0.5f, z = -depth + 0.05f;
                m.Beam(HillMat.StoneCoping, new Vector3(cx, sill, z), new Vector3(cx, spring, z), 0.07f, 0.07f);
                m.Beam(HillMat.StoneCoping, new Vector3(cx, spring, z), new Vector3(x0 + w * 0.17f, spring + w * 0.55f, z), 0.06f, 0.06f);
                m.Beam(HillMat.StoneCoping, new Vector3(cx, spring, z), new Vector3(x1 - w * 0.17f, spring + w * 0.55f, z), 0.06f, 0.06f);
                for (int k = 1; k < 4; k++)
                {
                    float y = Mathf.Lerp(sill, spring, k / 4f);
                    m.Box(HillMat.MetalDark, new Vector3(x0, y - 0.008f, z - 0.03f), new Vector3(x1, y + 0.008f, z - 0.015f));
                }
            }

            /// <summary>A round window (rose or oculus) in a face frame: the wall filled back round the circle, its
            /// reveal, glass, the room behind, and wheel tracery.</summary>
            void Rose(float cx, float cy, float rad, string stone, float depth, bool rose)
            {
                const int seg = 16;
                var pts = new Vector3[seg];
                for (int i = 0; i < seg; i++)
                {
                    float a = i * Mathf.PI * 2f / seg;
                    pts[i] = new Vector3(cx + Mathf.Cos(a) * rad, cy + Mathf.Sin(a) * rad, 0f);
                }
                for (int q = 0; q < 4; q++)
                {
                    var corner = new Vector3(cx + (q == 0 || q == 3 ? rad : -rad), cy + (q < 2 ? rad : -rad), 0f);
                    for (int k = 0; k < seg / 4; k++)
                        m.Tri(stone, corner, pts[(q * seg / 4 + k) % seg], pts[(q * seg / 4 + k + 1) % seg], Vector3.forward);
                }
                for (int i = 0; i < seg; i++)
                {
                    var a = pts[i];
                    var c = pts[(i + 1) % seg];
                    var mid = (a + c) * 0.5f;
                    m.Quad(stone, a, c, c - Vector3.forward * depth, a - Vector3.forward * depth, new Vector3(cx - mid.x, cy - mid.y, 0f));
                }
                Room(m, HillMat.Interior, HillAtlas.Interior(7), false, cx - rad, cx + rad, cy - rad, cy + rad, -depth - 0.03f, 1.2f);
                var g = new Vector3[seg];
                for (int i = 0; i < seg; i++) g[i] = pts[i] - Vector3.forward * (depth - 0.02f);
                m.Poly(HillMat.Glass, g, Vector3.forward);
                var cz = new Vector3(cx, cy, -depth + 0.06f);
                Torus(m, HillMat.StoneCoping, cz, Vector3.forward, rad * 0.25f, Mathf.Max(0.035f, rad * 0.04f), 10, 4);
                int spokes = rose ? 8 : 4;
                for (int i = 0; i < spokes; i++)
                {
                    float a = i * Mathf.PI * 2f / spokes;
                    var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                    m.Beam(HillMat.StoneCoping, cz + d * rad * 0.27f, cz + d * rad * 0.98f, 0.07f, 0.07f);
                    if (rose)
                    {
                        float a2 = a + Mathf.PI / spokes;
                        Torus(m, HillMat.StoneCoping, cz + new Vector3(Mathf.Cos(a2), Mathf.Sin(a2), 0f) * rad * 0.64f, Vector3.forward, rad * 0.17f, 0.03f, 8, 4);
                    }
                }
            }

            void ChurchTower(string stone, float ridge)
            {
                float TT = H + 12f, sb = Mathf.Min(H + 1f, TT * 0.45f), belfry = TT - 5f;
                // Faces: front (z = -6), right (x = 3), back (z = 0, above the nave), left (x = -3).
                var faces = new[]
                {
                    (o: new Vector3(-3f, 0f, -6f), a: Vector3.right, n: Vector3.back, y0: -1.2f),
                    (o: new Vector3(3f, 0f, -6f), a: Vector3.forward, n: Vector3.right, y0: -1.2f),
                    (o: new Vector3(3f, 0f, 0f), a: Vector3.left, n: Vector3.forward, y0: H),
                    (o: new Vector3(-3f, 0f, 0f), a: Vector3.back, n: Vector3.left, y0: -1.2f),
                };
                for (int f = 0; f < 4; f++)
                {
                    var fc = faces[f];
                    var holes = new List<Rect>();
                    var after = new List<Action>();
                    // The belfry: two louvred lancets on every face.
                    foreach (float c in new[] { 2.05f, 3.95f })
                    {
                        float x0 = c - 0.45f, x1 = c + 0.45f, sill = belfry + 0.5f, spring = TT - 2.3f;
                        holes.Add(new Rect(x0, sill, 0.9f, spring + 0.9f * 0.866f - sill));
                        after.Add(() =>
                        {
                            var o = Lancet(m, x0, x1, sill, spring, 0.42f, stone, stone, null, null, default);
                            m.Quad(HillMat.Rubber, new Vector3(x0, sill, -0.4f), new Vector3(x0, spring + 0.8f, -0.4f), new Vector3(x1, spring + 0.8f, -0.4f), new Vector3(x1, sill, -0.4f), Vector3.forward);
                            for (float y = sill + 0.12f; y < spring + 0.6f; y += 0.2f)
                            {
                                var sp = Span(o, y + 0.04f);
                                if (sp.y - sp.x < 0.1f) continue;
                                m.Beam(HillMat.TrimDark, new Vector3(sp.x + 0.01f, y + 0.06f, -0.32f), new Vector3(sp.y - 0.01f, y + 0.06f, -0.32f), 0.16f, 0.02f);
                            }
                        });
                    }
                    if (f == 0)
                    {
                        // The portal (a pointed doorway, its oak doors deep in the wall), the rose window over it.
                        float px0 = 1.75f, px1 = 4.25f, pspring = Mathf.Min(2.9f, sb - 2.6f);
                        holes.Add(new Rect(px0, 0f, px1 - px0, pspring + (px1 - px0) * 0.866f));
                        after.Add(() => Portal(px0, px1, pspring, stone));
                        float rTop = belfry - 2.4f, rBottom = sb + 0.4f, rr = Mathf.Min(1.45f, (rTop - rBottom) * 0.5f);
                        if (rr > 0.5f)
                        {
                            float ry = rBottom + rr;
                            holes.Add(new Rect(3f - rr, ry - rr, rr * 2f, rr * 2f));
                            after.Add(() => Rose(3f, ry, rr, stone, 0.3f, true));
                        }
                    }
                    else if (f != 2)
                    {
                        float x0 = 2.65f, x1 = 3.35f, sill = sb + 0.6f, spring = Mathf.Min(sill + 1.8f, belfry - 2.6f);
                        if (spring > sill + 0.5f)
                        {
                            holes.Add(new Rect(x0, sill, 0.7f, spring + 0.7f * 0.866f - sill));
                            after.Add(() => Lancet(m, x0, x1, sill, spring, 0.3f, stone, stone, HillMat.Glass, HillMat.Interior, HillAtlas.Interior(7)));
                        }
                    }
                    m.Push(WallFrame(fc.o, fc.a, fc.n));
                    try
                    {
                        WallFace(m, stone, new List<Vector2> { new Vector2(0f, fc.y0), new Vector2(6f, fc.y0), new Vector2(6f, TT), new Vector2(0f, TT) }, holes);
                        foreach (var a in after) a();
                        if (f != 2) Clock(m, 3f, belfry - 1.05f, 0.85f);
                        // String courses, clasping buttresses, the cornice and the plinth.
                        foreach (float y in new[] { sb, belfry })
                            if (y > fc.y0 + 0.2f) m.Box(HillMat.StoneCoping, new Vector3(-0.1f, y, 0f), new Vector3(6.1f, y + 0.16f, 0.1f), 0.02f, Side.ZNeg);
                        if (f != 2)
                        {
                            m.Box(stone, new Vector3(-0.12f, -1.2f, 0f), new Vector3(0.45f, belfry, 0.12f), 0.02f, Side.ZNeg);
                            m.Box(stone, new Vector3(5.55f, -1.2f, 0f), new Vector3(6.12f, belfry, 0.12f), 0.02f, Side.ZNeg);
                            PlinthBand(6f, 0.45f, HillMat.StoneCoping);
                        }
                        m.Box(HillMat.StoneCoping, new Vector3(-0.08f, TT - 0.4f, 0f), new Vector3(6.08f, TT, 0.09f), 0.02f, Side.ZNeg);
                    }
                    finally { m.Pop(); }
                }
                // The tower's top, its pinnacles and the copper spire (above the collider: out of reach).
                m.Quad(HillMat.StoneCoping, new Vector3(-3f, TT, -6f), new Vector3(3f, TT, -6f), new Vector3(3f, TT, 0f), new Vector3(-3f, TT, 0f), Vector3.up);
                var sc = new Vector3(0f, TT, -3f);
                float ap = 2.5f, rv = ap / Mathf.Cos(22.5f * Mathf.Deg2Rad);
                var ring = new List<Vector2>();
                for (int k = 0; k < 8; k++)
                {
                    float a = (k * 45f + 22.5f) * Mathf.Deg2Rad;
                    ring.Add(new Vector2(Mathf.Cos(a) * rv, Mathf.Sin(a) * rv - 3f));
                }
                m.Prism(HillMat.StoneCoping, ring, TT, TT + 0.35f, false);
                var apex = sc + Vector3.up * 11.5f;
                for (int k = 0; k < 8; k++)
                {
                    var p0 = new Vector3(ring[k].x, TT + 0.35f, ring[k].y);
                    var p1 = new Vector3(ring[(k + 1) % 8].x, TT + 0.35f, ring[(k + 1) % 8].y);
                    var mid = (p0 + p1) * 0.5f - sc;
                    m.Tri(HillMat.Copper, p0, p1, apex, new Vector3(mid.x, 0.2f, mid.z));
                }
                m.Box(HillMat.MetalDark, apex + new Vector3(-0.04f, -0.1f, -0.04f), apex + new Vector3(0.04f, 1.3f, 0.04f));
                m.Box(HillMat.MetalDark, apex + new Vector3(-0.35f, 0.75f, -0.04f), apex + new Vector3(0.35f, 0.85f, 0.04f));
                foreach (int sx in new[] { -1, 1 })
                    foreach (float z in new[] { -5.7f, -0.3f })
                    {
                        var c = new Vector3(sx * 2.7f, TT, z);
                        m.Box(HillMat.StoneCoping, c + new Vector3(-0.28f, 0f, -0.28f), c + new Vector3(0.28f, 0.5f, 0.28f), 0.02f, Side.YNeg);
                        var t0 = c + new Vector3(-0.25f, 0.5f, -0.25f); var t1 = c + new Vector3(0.25f, 0.5f, -0.25f);
                        var t2 = c + new Vector3(0.25f, 0.5f, 0.25f); var t3 = c + new Vector3(-0.25f, 0.5f, 0.25f);
                        var tip = c + Vector3.up * 2.1f;
                        m.Tri(HillMat.StoneCoping, t0, t1, tip, Vector3.back);
                        m.Tri(HillMat.StoneCoping, t1, t2, tip, Vector3.right);
                        m.Tri(HillMat.StoneCoping, t2, t3, tip, Vector3.forward);
                        m.Tri(HillMat.StoneCoping, t3, t0, tip, Vector3.left);
                    }
            }

            /// <summary>The church's west doorway in the tower's front (face frame): a deep pointed recess, a hood mould,
            /// oak doors with iron straps and ring handles, a stone threshold.</summary>
            void Portal(float x0, float x1, float spring, string stone)
            {
                const float depth = 0.5f;
                var o = Lancet(m, x0, x1, 0f, spring, depth, stone, stone, null, null, default, false);
                var door = new Vector3[o.Count];
                for (int i = 0; i < o.Count; i++) door[i] = new Vector3(o[i].x, o[i].y, -depth + 0.01f);
                m.Poly(HillMat.FenceWood, door, Vector3.forward);
                float cx = (x0 + x1) * 0.5f, w = x1 - x0, apex = spring + w * 0.866f;
                m.Quad(HillMat.Rubber, new Vector3(cx - 0.012f, 0f, -depth + 0.016f), new Vector3(cx - 0.012f, apex, -depth + 0.016f), new Vector3(cx + 0.012f, apex, -depth + 0.016f), new Vector3(cx + 0.012f, 0f, -depth + 0.016f), Vector3.forward);
                foreach (float y in new[] { 0.5f, 1.5f, 2.5f })
                    if (y < spring)
                    {
                        m.Box(HillMat.MetalDark, new Vector3(x0 + 0.05f, y, -depth + 0.01f), new Vector3(cx - 0.06f, y + 0.07f, -depth + 0.03f), 0.004f);
                        m.Box(HillMat.MetalDark, new Vector3(cx + 0.06f, y, -depth + 0.01f), new Vector3(x1 - 0.05f, y + 0.07f, -depth + 0.03f), 0.004f);
                    }
                foreach (int sx in new[] { -1, 1 })
                    Torus(m, HillMat.MetalDark, new Vector3(cx + sx * 0.18f, 1.05f, -depth + 0.04f), Vector3.forward, 0.08f, 0.012f, 10, 4);
                m.Box(HillMat.StoneCoping, new Vector3(x0, -0.2f, -depth), new Vector3(x1, 0.02f, 0f), 0.012f);
                // The hood mould over the arch.
                for (int i = 2; i < o.Count - 1; i++)
                {
                    var a = new Vector3(cx + (o[i].x - cx) * 1.12f, spring + (o[i].y - spring) * 1.08f + 0.1f, 0.05f);
                    var c = new Vector3(cx + (o[i + 1].x - cx) * 1.12f, spring + (o[i + 1].y - spring) * 1.08f + 0.1f, 0.05f);
                    m.Beam(HillMat.StoneCoping, a, c, 0.1f, 0.1f);
                }
            }

            // ---------- Hospital ----------

            void Hospital()
            {
                string panel = Or(b.wall, HillMat.ConcretePanel);
                float hx = FL * 0.5f, TT = H + 12f, tz0 = D * 0.3f;
                // Floor levels: a tall ground floor, then 3.7 m storeys up through the tower.
                var levels = new List<float> { 0f, 4.6f };
                while (levels[levels.Count - 1] + 3.7f < TT + 0.1f) levels.Add(levels[levels.Count - 1] + 3.7f);
                Rect Ward(int i) => HillAtlas.WardInterior((i % 3 == 2) ? 1 : 0);
                // The main block's four faces.
                for (int f = 0; f < 4; f++)
                {
                    float len = FLen(f);
                    var j = Job(f, panel, -1.2f, H + 1.1f - 0.08f);
                    for (int k = 0; k + 1 < levels.Count && levels[k] < H - 2f; k++)
                    {
                        float y0 = levels[k] + (k == 0 ? 0.35f : 1.0f), y1 = Mathf.Min(levels[k + 1] - (k == 0 ? 0.7f : 0.45f), H - 0.5f);
                        if (y1 - y0 < 0.8f) continue;
                        var spans = new List<Vector2> { new Vector2(0.9f, len - 0.9f) };
                        if (f == 0 && k == 0)
                        {
                            spans = Cut(spans, hx - 4.6f, hx + 4.6f);
                            if (FL > 40f) spans = Cut(spans, FL - 13.4f, FL - 1.6f);
                            // The entrance: a deep glazed lobby with sliding doors and a revolving door.
                            float ex0 = hx - 4.2f, ex1 = hx + 4.2f, ey1 = 3.9f;
                            j.holes.Add(new Rect(ex0, 0f, ex1 - ex0, ey1));
                            j.after.Add(() => Entrance(ex0, ex1, ey1));
                            if (FL > 40f)
                            {
                                float ax0 = FL - 13f, ax1 = FL - 2f;
                                j.holes.Add(new Rect(ax0, 0f, ax1 - ax0, 3.6f));
                                j.after.Add(() => AmbulanceBay(ax0, ax1));
                            }
                        }
                        foreach (var sp in spans)
                        {
                            float a = sp.x, c = sp.y;
                            if (c - a < 1.2f) continue;
                            int kk = k;
                            j.holes.Add(new Rect(a, y0, c - a, y1 - y0));
                            j.after.Add(() => Band(a, c, y0, y1, 0.2f, HillMat.CurtainWall, HillMat.MetalDark, panel, 1.5f, HillMat.InteriorWard, i => Ward(i + kk), 2.4f, y0 + (y1 - y0) * 0.72f));
                        }
                    }
                    int ff = f;
                    j.after.Add(() =>
                    {
                        PlinthBand(len, 0.3f, HillMat.Concrete);
                        // Vertical fins at the ends and a cornice band at the roof line.
                        m.Box(HillMat.Concrete, new Vector3(-0.05f, -1.2f, 0f), new Vector3(0.9f, H + 1.02f, 0.1f), 0.02f, Side.ZNeg);
                        m.Box(HillMat.Concrete, new Vector3(len - 0.9f, -1.2f, 0f), new Vector3(len + 0.05f, H + 1.02f, 0.1f), 0.02f, Side.ZNeg);
                        if (ff != 0) { Downpipe(1.1f, H); Downpipe(len - 1.1f, H); }
                    });
                    Run(j);
                }
                FlatTop(1.1f, 0.3f, HillMat.Concrete, panel);
                // The tower above the main block: bands on three faces and the back, the sign near its top on the front.
                var tf = new[]
                {
                    (o: new Vector3(-17f, 0f, tz0), a: Vector3.right, n: Vector3.back, len: 34f),
                    (o: new Vector3(17f, 0f, tz0), a: Vector3.forward, n: Vector3.right, len: D - tz0),
                    (o: new Vector3(17f, 0f, D), a: Vector3.left, n: Vector3.forward, len: 34f),
                    (o: new Vector3(-17f, 0f, D), a: Vector3.back, n: Vector3.left, len: D - tz0),
                };
                for (int f = 0; f < 4; f++)
                {
                    var fc = tf[f];
                    float yb = f == 2 ? H + 1.1f : H;
                    var holes = new List<Rect>();
                    var after = new List<Action>();
                    for (int k = 0; k + 1 < levels.Count; k++)
                    {
                        float y0 = levels[k] + 1.0f, y1 = levels[k + 1] - 0.45f;
                        if (y0 < yb + 0.3f || y1 > TT - 0.5f) continue;
                        if (f == 0 && y1 > TT - 3.6f) continue;
                        int kk = k;
                        holes.Add(new Rect(0.9f, y0, fc.len - 1.8f, y1 - y0));
                        after.Add(() => Band(0.9f, fc.len - 0.9f, y0, y1, 0.2f, HillMat.CurtainWall, HillMat.MetalDark, panel, 1.5f, HillMat.InteriorWard, i => Ward(i + kk + 1), 2.4f, y0 + (y1 - y0) * 0.72f));
                    }
                    m.Push(WallFrame(fc.o, fc.a, fc.n));
                    try
                    {
                        WallFace(m, panel, new List<Vector2> { new Vector2(0f, yb), new Vector2(fc.len, yb), new Vector2(fc.len, TT), new Vector2(0f, TT) }, holes);
                        foreach (var a in after) a();
                        m.Box(HillMat.Concrete, new Vector3(-0.06f, TT - 0.3f, 0f), new Vector3(fc.len + 0.06f, TT, 0.08f), 0.02f, Side.ZNeg);
                        m.Box(HillMat.Concrete, new Vector3(-0.05f, yb, 0f), new Vector3(0.9f, TT - 0.3f, 0.1f), 0.02f, Side.ZNeg);
                        m.Box(HillMat.Concrete, new Vector3(fc.len - 0.9f, yb, 0f), new Vector3(fc.len + 0.05f, TT - 0.3f, 0.1f), 0.02f, Side.ZNeg);
                        if (f == 0)
                        {
                            // The big sign: lettering on a dark frame, lit from below.
                            float sw = Mathf.Min(24f, fc.len - 3f), sh = sw / 8f, sy = TT - 0.55f - sh * 0.5f - 0.1f;
                            if (sh > 2.6f) { sh = 2.6f; sw = sh * 8f; }
                            m.Box(HillMat.MetalDark, new Vector3(fc.len * 0.5f - sw * 0.5f - 0.15f, sy - sh * 0.5f - 0.15f, 0f), new Vector3(fc.len * 0.5f + sw * 0.5f + 0.15f, sy + sh * 0.5f + 0.15f, 0.09f), 0.02f, Side.ZNeg);
                            Panel(m, HillMat.SignBoard, new Vector3(fc.len * 0.5f, sy, 0.1f), Vector3.right * sw, Vector3.up * sh, Vector3.forward, HillAtlas.Sign(HillAtlas.SignHospital));
                            for (int i = 0; i < 6; i++)
                            {
                                float x = fc.len * 0.5f - sw * 0.5f + sw * (i + 0.5f) / 6f;
                                m.Box(HillMat.LampGlow, new Vector3(x - 0.2f, sy - sh * 0.5f - 0.3f, 0.02f), new Vector3(x + 0.2f, sy - sh * 0.5f - 0.2f, 0.11f), 0.01f);
                            }
                        }
                    }
                    finally { m.Pop(); }
                }
                // The tower roof: the helipad and its markings, the plant rooms.
                m.Quad(HillMat.RoofFlat, new Vector3(-17f, TT, tz0), new Vector3(17f, TT, tz0), new Vector3(17f, TT, D), new Vector3(-17f, TT, D), Vector3.up);
                var hc = new Vector3(-4f, TT + 0.016f, tz0 + (D - tz0) * 0.5f);
                float hr = Mathf.Min(6f, (D - tz0) * 0.4f);
                for (int i = 0; i < 32; i++)
                {
                    float a0 = i * Mathf.PI / 16f, a1 = (i + 1) * Mathf.PI / 16f;
                    Strip(m, HillMat.DoorYellow, hc + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * hr, hc + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * hr, 0.35f, Vector3.up);
                }
                float hh = hr * 0.45f;
                Strip(m, HillMat.TrimWhite, hc + new Vector3(-hh * 0.6f, 0f, -hh), hc + new Vector3(-hh * 0.6f, 0f, hh), 0.5f, Vector3.up);
                Strip(m, HillMat.TrimWhite, hc + new Vector3(hh * 0.6f, 0f, -hh), hc + new Vector3(hh * 0.6f, 0f, hh), 0.5f, Vector3.up);
                Strip(m, HillMat.TrimWhite, hc + new Vector3(-hh * 0.6f, 0.001f, 0f), hc + new Vector3(hh * 0.6f, 0.001f, 0f), 0.5f, Vector3.up);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4f;
                    var p = hc + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (hr + 0.6f);
                    m.Cylinder(HillMat.LampGlow, p, p + Vector3.up * 0.12f, 0.08f, 8, true);
                }
                foreach (float x in new[] { 9.5f })
                {
                    m.Box(panel, new Vector3(x - 4.5f, TT, D - 7f), new Vector3(x + 4.5f, TT + 3.4f, D - 0.6f), 0.03f, Side.YNeg);
                    m.Box(HillMat.Concrete, new Vector3(x - 4.6f, TT + 3.4f, D - 7.1f), new Vector3(x + 4.6f, TT + 3.55f, D - 0.5f), 0.02f);
                    for (int i = 0; i < 4; i++)
                        m.Quad(HillMat.Corrugated, new Vector3(x - 4f + i * 2.1f, TT + 0.8f, D - 7.012f), new Vector3(x - 4f + i * 2.1f + 1.7f, TT + 0.8f, D - 7.012f), new Vector3(x - 4f + i * 2.1f + 1.7f, TT + 2.8f, D - 7.012f), new Vector3(x - 4f + i * 2.1f, TT + 2.8f, D - 7.012f), Vector3.back);
                    for (int i = 0; i < 3; i++)
                        m.Cylinder(HillMat.Steel, new Vector3(x - 3f + i * 3f, TT + 3.55f, D - 3.8f), new Vector3(x - 3f + i * 3f, TT + 4.3f, D - 3.8f), 0.45f, 12, true);
                }
                m.Box(HillMat.Concrete, new Vector3(-14f, TT, D - 4f), new Vector3(-10.5f, TT + 2.8f, D - 0.8f), 0.03f, Side.YNeg);
                // The entrance canopy on its columns, its soffit lit.
                m.Box(HillMat.Concrete, new Vector3(-8f, 4.2f, -7f), new Vector3(8f, 4.6f, 0f), 0.03f, Side.ZPos);
                for (int i = 0; i < 3; i++)
                {
                    float z = -6.2f + i * 2.2f;
                    m.Quad(HillMat.LampGlow, new Vector3(-7.2f, 4.19f, z), new Vector3(7.2f, 4.19f, z), new Vector3(7.2f, 4.19f, z + 0.35f), new Vector3(-7.2f, 4.19f, z + 0.35f), Vector3.down);
                }
                Panel(m, HillMat.SignBoard, new Vector3(0f, 4.4f, -7.008f), Vector3.right * 3.0f, Vector3.up * 0.375f, Vector3.back, HillAtlas.Sign(HillAtlas.SignHospital));
                for (int i = 0; i < 4; i++)
                {
                    float x = -7f + i * (14f / 3f);
                    m.Cylinder(HillMat.Concrete, new Vector3(x, 0f, -6.35f), new Vector3(x, 4.2f, -6.35f), 0.22f, 12, false);
                    m.Cylinder(HillMat.Steel, new Vector3(x, 0f, -6.35f), new Vector3(x, 0.3f, -6.35f), 0.245f, 12, false);
                    m.Cylinder(HillMat.Steel, new Vector3(x, 3.95f, -6.35f), new Vector3(x, 4.2f, -6.35f), 0.245f, 12, false);
                }
            }

            static List<Vector2> Cut(List<Vector2> spans, float a, float c)
            {
                var res = new List<Vector2>();
                foreach (var s in spans)
                {
                    if (c <= s.x || a >= s.y) { res.Add(s); continue; }
                    if (a > s.x) res.Add(new Vector2(s.x, a));
                    if (c < s.y) res.Add(new Vector2(c, s.y));
                }
                return res;
            }

            /// <summary>The hospital's entrance (face frame): a lobby behind glass, two pairs of sliding doors and a
            /// revolving door between them.</summary>
            void Entrance(float x0, float x1, float y1)
            {
                const float recess = 0.2f;
                m.Quad(HillMat.Concrete, new Vector3(x0, y1, 0f), new Vector3(x1, y1, 0f), new Vector3(x1, y1, -recess), new Vector3(x0, y1, -recess), Vector3.down);
                m.Quad(HillMat.Concrete, new Vector3(x0, 0f, 0f), new Vector3(x0, y1, 0f), new Vector3(x0, y1, -recess), new Vector3(x0, 0f, -recess), Vector3.right);
                m.Quad(HillMat.Concrete, new Vector3(x1, 0f, 0f), new Vector3(x1, y1, 0f), new Vector3(x1, y1, -recess), new Vector3(x1, 0f, -recess), Vector3.left);
                Room(m, HillMat.InteriorWard, HillAtlas.WardInterior(1), false, x0, x1, 0f, y1, -recess - 0.02f, 6f);
                float cx = (x0 + x1) * 0.5f;
                // The glazed screen with its doors.
                m.Quad(HillMat.Glass, new Vector3(x0, 0f, -recess + 0.02f), new Vector3(x0, y1, -recess + 0.02f), new Vector3(x1, y1, -recess + 0.02f), new Vector3(x1, 0f, -recess + 0.02f), Vector3.forward);
                foreach (float x in new[] { x0 + 0.05f, x0 + 1.4f, cx - 1.3f, cx + 1.3f, x1 - 1.4f, x1 - 0.05f })
                    m.Box(HillMat.MetalDark, new Vector3(x - 0.04f, 0f, -recess - 0.02f), new Vector3(x + 0.04f, y1, -recess + 0.07f), 0f, Side.ZNeg);
                m.Box(HillMat.MetalDark, new Vector3(x0, 2.6f, -recess - 0.02f), new Vector3(x1, 2.75f, -recess + 0.07f), 0f, Side.ZNeg);
                foreach (float x in new[] { x0 + 2.75f, x1 - 2.75f })
                    m.Quad(HillMat.Rubber, new Vector3(x - 0.01f, 0.05f, -recess + 0.075f), new Vector3(x - 0.01f, 2.6f, -recess + 0.075f), new Vector3(x + 0.01f, 2.6f, -recess + 0.075f), new Vector3(x + 0.01f, 0.05f, -recess + 0.075f), Vector3.forward);
                // The revolving door just inside: a glass drum, its canopy disc and four wings.
                var dc = new Vector3(cx, 0f, -recess - 1.3f);
                const float dr = 1.1f;
                for (int i = 0; i < 16; i++)
                {
                    float a0 = i * Mathf.PI / 8f, a1 = (i + 1) * Mathf.PI / 8f;
                    var p0 = dc + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * dr;
                    var p1 = dc + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * dr;
                    if (Mathf.Sin((a0 + a1) * 0.5f) > 0.3f) continue;
                    m.Quad(HillMat.Glass, p0, p1, p1 + Vector3.up * 2.4f, p0 + Vector3.up * 2.4f, (p0 + p1) * 0.5f - dc);
                }
                m.Cylinder(HillMat.MetalDark, dc + Vector3.up * 2.4f, dc + Vector3.up * 2.65f, dr + 0.04f, 16, true);
                for (int k = 0; k < 4; k++)
                {
                    float a = k * Mathf.PI * 0.5f + 0.4f;
                    var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    m.Quad(HillMat.Glass, dc + d * 0.05f, dc + d * (dr - 0.05f), dc + d * (dr - 0.05f) + Vector3.up * 2.3f, dc + d * 0.05f + Vector3.up * 2.3f, new Vector3(-d.z, 0f, d.x));
                    m.Quad(HillMat.Glass, dc + d * 0.05f, dc + d * (dr - 0.05f), dc + d * (dr - 0.05f) + Vector3.up * 2.3f, dc + d * 0.05f + Vector3.up * 2.3f, new Vector3(d.z, 0f, -d.x));
                    m.Beam(HillMat.Steel, dc + d * (dr - 0.05f), dc + d * (dr - 0.05f) + Vector3.up * 2.3f, 0.04f, 0.04f);
                }
                m.Cylinder(HillMat.Steel, dc, dc + Vector3.up * 2.4f, 0.06f, 8, false);
                // Mat and threshold.
                m.Quad(HillMat.Rubber, new Vector3(cx - 2f, 0.012f, -2.5f), new Vector3(cx + 2f, 0.012f, -2.5f), new Vector3(cx + 2f, 0.012f, -0.02f), new Vector3(cx - 2f, 0.012f, -0.02f), Vector3.up);
            }

            /// <summary>The ambulance bay (face frame): two roller doors under a lit red EMERGENCY sign, yellow hatching
            /// on the apron.</summary>
            void AmbulanceBay(float x0, float x1)
            {
                const float y1 = 3.6f, recess = 0.22f;
                m.Quad(HillMat.Concrete, new Vector3(x0, y1, 0f), new Vector3(x1, y1, 0f), new Vector3(x1, y1, -recess), new Vector3(x0, y1, -recess), Vector3.down);
                m.Quad(HillMat.Concrete, new Vector3(x0, 0f, 0f), new Vector3(x0, y1, 0f), new Vector3(x0, y1, -recess), new Vector3(x0, 0f, -recess), Vector3.right);
                m.Quad(HillMat.Concrete, new Vector3(x1, 0f, 0f), new Vector3(x1, y1, 0f), new Vector3(x1, y1, -recess), new Vector3(x1, 0f, -recess), Vector3.left);
                float mid = (x0 + x1) * 0.5f;
                foreach (var (a, c) in new[] { (x0, mid - 0.2f), (mid + 0.2f, x1) })
                {
                    m.Quad(HillMat.Corrugated, new Vector3(a, 0f, -recess), new Vector3(a, y1 - 0.4f, -recess), new Vector3(c, y1 - 0.4f, -recess), new Vector3(c, 0f, -recess), Vector3.forward);
                    m.Box(HillMat.MetalDark, new Vector3(a, y1 - 0.4f, -recess - 0.02f), new Vector3(c, y1, -recess + 0.1f), 0.01f, Side.ZNeg);
                    m.Box(HillMat.Steel, new Vector3(a, 0f, -recess - 0.02f), new Vector3(c, 0.1f, -recess + 0.03f), 0f, Side.ZNeg);
                }
                m.Box(HillMat.Concrete, new Vector3(mid - 0.2f, 0f, -recess), new Vector3(mid + 0.2f, y1, 0f), 0.01f, Side.ZNeg);
                // The sign: a lit box with the red EMERGENCY board on it.
                float sw = Mathf.Min(6f, x1 - x0 - 1f), sh = sw / 8f, sy = 4.25f + sh * 0.5f;
                m.Box(HillMat.LampGlow, new Vector3(mid - sw * 0.5f - 0.1f, sy - sh * 0.5f - 0.1f, 0f), new Vector3(mid + sw * 0.5f + 0.1f, sy + sh * 0.5f + 0.1f, 0.09f), 0.015f, Side.ZNeg);
                Panel(m, HillMat.SignBoard, new Vector3(mid, sy, 0.096f), Vector3.right * sw, Vector3.up * sh, Vector3.forward, HillAtlas.Sign(HillAtlas.SignEmergency));
                // Hatching on the ground in front (decal strips 1.5 cm up), and a border.
                const float gy = 0.015f;
                for (float x = x0 + 0.5f; x < x1 - 0.5f; x += 1.2f)
                    Strip(m, HillMat.DoorYellow, new Vector3(x, gy, 0.6f), new Vector3(x + 1.6f > x1 ? x1 - 0.1f : x + 1.6f, gy, 5.6f), 0.18f, Vector3.forward);
                Strip(m, HillMat.DoorYellow, new Vector3(x0, gy, 0.4f), new Vector3(x1, gy, 0.4f), 0.15f, Vector3.forward);
                Strip(m, HillMat.DoorYellow, new Vector3(x0, gy, 6f), new Vector3(x1, gy, 6f), 0.15f, Vector3.forward);
            }

            // ---------- School ----------

            void School()
            {
                string brick = Or(b.wall, Chance(r, 0.5f) ? HillMat.BrickRed : HillMat.BrickCream);
                float hx = FL * 0.5f, L1 = Mathf.Max(3.4f, Mathf.Min(4f, H * 0.5f));
                var floors = new List<float> { 0f };
                if (H > L1 + 2.8f) floors.Add(L1);
                Rect Class(int i) => HillAtlas.Interior(i % 3 == 0 ? 6 : 5);
                for (int f = 0; f < 4; f++)
                {
                    float len = FLen(f);
                    var j = Job(f, brick, -1.2f, H + 0.6f - 0.08f);
                    foreach (float fy in floors)
                    {
                        float y0 = fy + 0.9f, y1 = fy + Mathf.Min(2.75f, (fy == 0f ? L1 : H - fy) - 0.6f);
                        if (y1 - y0 < 0.8f) continue;
                        var spans = new List<Vector2> { new Vector2(1.2f, f == 1 ? len * 0.55f : len - 1.2f) };
                        if (f == 0 && fy == 0f) spans = Cut(spans, hx - 2.2f, hx + 2.2f);
                        foreach (var sp in spans)
                        {
                            float a = sp.x, c = sp.y;
                            if (c - a < 1.5f) continue;
                            j.holes.Add(new Rect(a, y0, c - a, y1 - y0));
                            j.after.Add(() =>
                            {
                                Band(a, c, y0, y1, 0.16f, HillMat.Glass, HillMat.TrimWhite, brick, 1.25f, HillMat.Interior, Class, 2.2f, y0 + (y1 - y0) * 0.7f);
                                m.Box(HillMat.Concrete, new Vector3(a - 0.1f, y0 - 0.1f, -0.16f), new Vector3(c + 0.1f, y0, 0.06f), 0.012f);
                                m.Box(HillMat.Concrete, new Vector3(a - 0.1f, y1, 0f), new Vector3(c + 0.1f, y1 + 0.25f, 0.03f), 0.008f, Side.ZNeg);
                            });
                        }
                    }
                    if (f == 0)
                    {
                        // The entrance: two glazed doors under a sign, the clock high on the side.
                        foreach (float x in new[] { hx - 0.95f, hx + 0.05f })
                            AddDoor(j, new DoorSpec { x0 = x, x1 = x + 0.9f, height = 2.3f, depth = 0.35f, style = 4, casing = 3, wall = brick, reveal = HillMat.Concrete, trim = HillMat.MetalDark, leaf = HillMat.DoorBlue, room = HillMat.Interior, hallUV = HillAtlas.Interior(3) });
                        j.after.Add(() =>
                        {
                            m.Box(HillMat.Concrete, new Vector3(hx - 1.4f, 2.3f, 0f), new Vector3(hx + 1.4f, 2.6f, 0.08f), 0.015f, Side.ZNeg);
                            float sw = Mathf.Min(6f, FL * 0.3f), sh = sw / 8f, sy = Mathf.Min(L1 + 0.45f, 2.7f + sh * 0.5f + 0.25f);
                            m.Box(HillMat.TrimWhite, new Vector3(hx - sw * 0.5f - 0.1f, sy - sh * 0.5f - 0.08f, 0f), new Vector3(hx + sw * 0.5f + 0.1f, sy + sh * 0.5f + 0.08f, 0.07f), 0.01f, Side.ZNeg);
                            Panel(m, HillMat.SignBoard, new Vector3(hx, sy, 0.076f), Vector3.right * sw, Vector3.up * sh, Vector3.forward, HillAtlas.Sign(HillAtlas.SignSchool));
                        });
                    }
                    int ff = f;
                    j.after.Add(() =>
                    {
                        PlinthBand(len, 0.4f, HillMat.Concrete);
                        if (ff == 1) Clock(m, len * 0.78f, Mathf.Min(H - 0.7f, H * 0.62f), 0.55f);
                        if (ff == 0 || ff == 2) { Downpipe(0.5f, H); Downpipe(len - 0.5f, H); }
                        m.Box(HillMat.Concrete, new Vector3(-0.04f, H - 0.3f, 0f), new Vector3(len + 0.04f, H - 0.15f, 0.06f), 0.01f, Side.ZNeg);
                    });
                    Run(j);
                }
                FlatTop(0.6f, 0.3f, HillMat.StoneCoping, brick);
            }

            // ---------- Shops ----------

            static int ShopInteriorFor(int sign, int fallback)
            {
                if (sign == HillAtlas.SignAbernathy) return 7;
                if (sign >= HillAtlas.SignBakery && sign <= HillAtlas.SignHardware) return sign - 1;
                return ((fallback % 8) + 8) % 8;
            }

            /// <summary>One shopfront on a face job between s0 and s1: pilasters with corbels, a fascia with its sign, a
            /// cornice, a display window over a panelled stall riser with the shop's interior behind, a glazed door.</summary>
            void Shopfront(FaceJob j, float s0, float s1, int sign, string paint, string wall, bool doorRight)
            {
                float w = s1 - s0;
                int interior = ShopInteriorFor(sign, r.Next(8));
                float dw = 1.0f, dx0 = doorRight ? s1 - 0.45f - dw : s0 + 0.45f, dx1 = dx0 + dw;
                float wx0 = doorRight ? s0 + 0.45f : dx1 + 0.15f, wx1 = doorRight ? dx0 - 0.15f : s1 - 0.45f;
                if (wx1 - wx0 > 0.8f)
                {
                    var win = new Win
                    {
                        x0 = wx0, x1 = wx1, y0 = 0.55f, y1 = 2.55f, depth = 0.15f, muntins = wx1 - wx0 > 2.6f ? 6 : 3, casing = 4, sill = false,
                        wall = wall, reveal = paint, trim = paint, frame = paint, glass = HillMat.GlassShop, room = HillMat.InteriorShop,
                        roomUV = HillAtlas.ShopInterior(interior), roomDepth = 2.2f,
                    };
                    AddWin(j, win);
                    j.after.Add(() =>
                    {
                        m.Box(paint, new Vector3(wx0, 0f, 0f), new Vector3(wx1, 0.55f, 0.05f), 0.008f, Side.ZNeg);
                        int panels = Mathf.Max(1, Mathf.RoundToInt((wx1 - wx0) / 1.1f));
                        for (int i = 0; i < panels; i++)
                        {
                            float a = Mathf.Lerp(wx0 + 0.08f, wx1 - 0.08f, i / (float)panels) + 0.03f, c = Mathf.Lerp(wx0 + 0.08f, wx1 - 0.08f, (i + 1) / (float)panels) - 0.03f;
                            m.Box(paint, new Vector3(a, 0.1f, 0.05f), new Vector3(c, 0.45f, 0.065f), 0.008f, Side.ZNeg);
                        }
                        m.Box(paint, new Vector3(wx0 - 0.02f, 0.53f, -0.15f), new Vector3(wx1 + 0.02f, 0.58f, 0.07f), 0.01f);
                    });
                }
                AddDoor(j, new DoorSpec { x0 = dx0, x1 = dx1, height = 2.3f, depth = 0.2f, style = 4, casing = 3, wall = wall, reveal = paint, trim = paint, leaf = paint, room = HillMat.InteriorShop, hallUV = HillAtlas.ShopInterior(interior) });
                j.after.Add(() =>
                {
                    foreach (float x in new[] { s0, s1 - 0.32f })
                    {
                        m.Box(paint, new Vector3(x, 0f, 0f), new Vector3(x + 0.32f, 2.95f, 0.09f), 0.012f, Side.ZNeg);
                        m.Box(paint, new Vector3(x - 0.03f, 2.95f, 0f), new Vector3(x + 0.35f, 3.62f, 0.12f), 0.015f, Side.ZNeg);
                    }
                    m.Box(paint, new Vector3(s0 + 0.32f, 2.95f, 0f), new Vector3(s1 - 0.32f, 3.6f, 0.08f), 0.012f, Side.ZNeg);
                    float sw = Mathf.Min(w - 1f, 4.8f), sh = Mathf.Min(0.55f, sw / 8f);
                    sw = sh * 8f;
                    if (sign >= 0) Panel(m, HillMat.SignBoard, new Vector3((s0 + s1) * 0.5f, 3.275f, 0.086f), Vector3.right * sw, Vector3.up * sh, Vector3.forward, HillAtlas.Sign(sign));
                    m.Box(HillMat.TrimCream, new Vector3(s0 - 0.02f, 3.62f, 0f), new Vector3(s1 + 0.02f, 3.74f, 0.12f), 0.012f, Side.ZNeg);
                });
            }

            /// <summary>A striped canvas awning filling its collider box (building frame): a walkable top, its valance
            /// 0.12 below the box, cheeks, a plain underside and two arms.</summary>
            void Awning(float x0, float x1, string canvas)
            {
                const float y0 = 2.7f, y1 = 2.85f, z0 = -1.6f;
                m.PolyUV(canvas, new[] { new Vector3(x0, y1, z0), new Vector3(x0, y1, 0f), new Vector3(x1, y1, 0f), new Vector3(x1, y1, z0) },
                    new[] { new Vector2(x0, z0), new Vector2(x0, 0f), new Vector2(x1, 0f), new Vector2(x1, z0) }, Vector3.up);
                m.Quad(HillMat.Canvas, new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y0, 0f), new Vector3(x0, y0, 0f), Vector3.down);
                var val = new[] { new Vector3(x0, y0 - 0.12f, z0), new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y0 - 0.12f, z0) };
                var vuv = new[] { new Vector2(x0, 0f), new Vector2(x0, 0.27f), new Vector2(x1, 0.27f), new Vector2(x1, 0f) };
                m.PolyUV(canvas, val, vuv, Vector3.back);
                m.PolyUV(canvas, new[] { val[0] + Vector3.forward * 0.006f, val[1] + Vector3.forward * 0.006f, val[2] + Vector3.forward * 0.006f, val[3] + Vector3.forward * 0.006f }, vuv, Vector3.forward);
                m.Quad(canvas, new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x0, y1, 0f), new Vector3(x0, y0, 0f), Vector3.left);
                m.Quad(canvas, new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, 0f), new Vector3(x1, y0, 0f), Vector3.right);
                foreach (float x in new[] { x0 + 0.12f, x1 - 0.12f })
                    m.Box(HillMat.MetalDark, new Vector3(x - 0.015f, y0 - 0.025f, z0 + 0.05f), new Vector3(x + 0.015f, y0 - 0.005f, 0f));
            }

            void ShopRow()
            {
                string wall = Or(b.wall, Pick(r, new[] { HillMat.BrickRed, HillMat.BrickCream, HillMat.Stucco, HillMat.BrickRed }));
                bool brick = wall.StartsWith("brick");
                int shops = Mathf.Max(1, b.signs.Count);
                float w = FL / shops, hx = FL * 0.5f;
                string[] paints = { HillMat.DoorGreen, HillMat.DoorBlue, HillMat.TrimDark, HillMat.DoorRed, HillMat.TrimCream, HillMat.DoorYellow };
                string[] canvases = { HillMat.CanvasRed, HillMat.CanvasGreen, HillMat.CanvasBlue };
                var front = Job(0, wall, -1.2f, H + 0.9f - 0.08f);
                for (int i = 0; i < shops; i++)
                {
                    // Shop i sits at the world's i-th place along the front; local x may run the other way.
                    float c = sgn * (-hx + w * (i + 0.5f));
                    float s0 = c + hx - w * 0.5f, s1 = c + hx + w * 0.5f;
                    int sign = i < b.signs.Count ? b.signs[i] : -1;
                    Shopfront(front, s0 + 0.02f, s1 - 0.02f, sign, paints[(i + b.seed) % paints.Length], wall, i % 2 == 1);
                    float aw = w - 0.6f;
                    Awning(c - aw * 0.5f, c + aw * 0.5f, canvases[(i + b.seed) % canvases.Length]);
                }
                UpperFloors(front, 3.95f, brick, wall, true);
                front.after.Add(() =>
                {
                    PlinthBand(FL, 0f, HillMat.Plinth);
                    m.Box(HillMat.TrimCream, new Vector3(-0.05f, H - 0.35f, 0f), new Vector3(FL + 0.05f, H - 0.1f, 0.12f), 0.02f, Side.ZNeg);
                    for (int i = 1; i < shops; i++) Downpipe(w * i, H);
                });
                Run(front);
                for (int f = 1; f < 4; f++)
                {
                    var j = Job(f, wall, -1.2f, H + 0.9f - 0.08f);
                    if (f == 2) { UpperFloors(j, 0f, brick, wall, false); int n = Mathf.Max(1, Mathf.RoundToInt(FL / 5f)); j.after.Add(() => { for (int i = 0; i <= n; i++) Downpipe(Mathf.Lerp(0.5f, FL - 0.5f, i / (float)n), H); }); }
                    else UpperFloors(j, 3.95f, brick, wall, false);
                    j.after.Add(() => PlinthBand(FLen(j.face), 0.3f));
                    Run(j);
                }
                FlatTop(0.9f, 0.3f, brick ? HillMat.StoneCoping : HillMat.TrimCream, wall);
            }

            /// <summary>Flats over the shops: sash windows with interiors, storey by storey, from <paramref name="from"/>.</summary>
            void UpperFloors(FaceJob j, float from, bool brick, string wall, bool front)
            {
                float len = j.len, sh = 2.9f;
                int perBay = front ? 2 : 1;
                float spacing = front ? Mathf.Max(1.8f, FL / Mathf.Max(1, b.signs.Count) / perBay) : 3.2f;
                int n = Mathf.Max(1, Mathf.FloorToInt((len - 1f) / spacing));
                for (float y = from; y + 2.2f < H; y += sh)
                    for (int i = 0; i < n; i++)
                    {
                        float c = 0.5f + (len - 1f) * (i + 0.5f) / n;
                        if (!front && Chance(r, 0.3f)) continue;
                        var win = Sash(c, y + 0.85f, 0.9f, Mathf.Min(1.5f, sh - 1.3f), wall, brick ? 1 : 2, HillMat.TrimWhite, 2, Chance(r, 0.3f));
                        AddWin(j, win);
                    }
            }

            void CornerShop(bool bike)
            {
                string wall = Or(b.wall, bike ? HillMat.BrickRed : Pick(r, new[] { HillMat.StuccoWarm, HillMat.BrickCream, HillMat.BrickRed }));
                bool brick = wall.StartsWith("brick");
                int sign = b.signs.Count > 0 ? b.signs[0] : bike ? HillAtlas.SignAbernathy : HillAtlas.SignNews;
                string paint = bike ? HillMat.DoorBlue : Pick(r, new[] { HillMat.DoorGreen, HillMat.TrimDark, HillMat.DoorRed });
                bool parapet = !bike;
                float top = H + (parapet ? 0.6f - 0.08f : 0f);
                for (int f = 0; f < 4; f++)
                {
                    var j = Job(f, wall, -1.2f, top);
                    float len = FLen(f);
                    if (f == 0) Shopfront(j, 0.1f, len - 0.1f, sign, paint, wall, !bike);
                    else if (f == 1 && !bike && len > 3.5f) Shopfront(j, 0.1f, Mathf.Min(len - 0.3f, 6f), sign, paint, wall, false);
                    if (H > 6f) UpperFloors(j, 3.95f, brick, wall, f == 0);
                    j.after.Add(() =>
                    {
                        PlinthBand(len, 0f);
                        if (!parapet) m.Box(HillMat.TrimCream, new Vector3(-0.05f, H - 0.4f, 0f), new Vector3(len + 0.05f, H - 0.12f, 0.12f), 0.02f, Side.ZNeg);
                    });
                    Run(j);
                }
                FlatTop(parapet ? 0.6f : 0f, 0.3f, brick ? HillMat.StoneCoping : HillMat.TrimCream, wall);
            }

            // ---------- Small buildings ----------

            void Garage()
            {
                string wall = Or(b.wall, HillMat.BrickRed);
                int n = Mathf.Max(1, Mathf.RoundToInt(FL / 2.9f));
                float bw = FL / n;
                var j = Job(0, wall, -1.2f, H);
                string[] doors = { HillMat.Corrugated, HillMat.DoorGreen, HillMat.DoorBlue, HillMat.MetalPaint, HillMat.DoorRed, HillMat.Corrugated };
                for (int i = 0; i < n; i++)
                {
                    float x0 = bw * i + 0.3f, x1 = bw * (i + 1) - 0.3f, y1 = Mathf.Min(2.1f, H - 0.4f);
                    string dm = doors[(i * 7 + b.seed) % doors.Length];
                    int num = i + 1;
                    j.holes.Add(new Rect(x0, 0f, x1 - x0, y1));
                    j.after.Add(() =>
                    {
                        const float rc = 0.12f;
                        m.Quad(HillMat.Concrete, new Vector3(x0, y1, 0f), new Vector3(x1, y1, 0f), new Vector3(x1, y1, -rc), new Vector3(x0, y1, -rc), Vector3.down);
                        m.Quad(wall, new Vector3(x0, 0f, 0f), new Vector3(x0, y1, 0f), new Vector3(x0, y1, -rc), new Vector3(x0, 0f, -rc), Vector3.right);
                        m.Quad(wall, new Vector3(x1, 0f, 0f), new Vector3(x1, y1, 0f), new Vector3(x1, y1, -rc), new Vector3(x1, 0f, -rc), Vector3.left);
                        m.Box(HillMat.MetalDark, new Vector3(x0, 0f, -rc - 0.02f), new Vector3(x1, y1, -rc + 0.01f), 0f, Side.ZNeg);
                        m.Box(dm, new Vector3(x0 + 0.06f, 0.02f, -rc + 0.01f), new Vector3(x1 - 0.06f, y1 - 0.05f, -rc + 0.04f), 0.008f, Side.ZNeg);
                        for (int k = 1; k < 4; k++)
                            m.Box(dm, new Vector3(x0 + 0.1f, k * (y1 / 4f) - 0.015f, -rc + 0.04f), new Vector3(x1 - 0.1f, k * (y1 / 4f) + 0.015f, -rc + 0.05f), 0f, Side.ZNeg);
                        float cx = (x0 + x1) * 0.5f;
                        m.Box(HillMat.Steel, new Vector3(cx - 0.12f, 0.95f, -rc + 0.04f), new Vector3(cx + 0.12f, 1.0f, -rc + 0.075f), 0.008f);
                        Digits(m, HillMat.TrimWhite, num.ToString(), new Vector3(cx - 0.05f, y1 - 0.35f, -rc + 0.045f), 0.16f, 0.006f);
                        m.Rect(HillMat.Stains, new Vector3(cx, 0.012f, 1.2f), Vector3.right * 1.4f, Vector3.forward * 1.4f, Vector3.up, HillAtlas.Decal((num + b.seed) % 4));
                    });
                }
                j.after.Add(() =>
                {
                    m.Box(HillMat.Concrete, new Vector3(-0.04f, Mathf.Min(2.1f, H - 0.4f), 0f), new Vector3(FL + 0.04f, Mathf.Min(2.1f, H - 0.4f) + 0.2f, 0.05f), 0.01f, Side.ZNeg);
                    PlinthBand(FL, 0f, HillMat.Concrete);
                });
                Run(j);
                for (int f = 1; f < 4; f++)
                {
                    var jj = Job(f, wall, -1.2f, H);
                    jj.after.Add(() => PlinthBand(FLen(jj.face), 0f, HillMat.Concrete));
                    Run(jj);
                }
                FlatTop(0f, 0f, HillMat.Concrete, wall);
            }

            void Kiosk()
            {
                string wall = Or(b.wall, Pick(r, new[] { HillMat.DoorGreen, HillMat.SidingSage, HillMat.DoorBlue }));
                int sign = b.signs.Count > 0 ? b.signs[0] : HillAtlas.SignNews;
                for (int f = 0; f < 4; f++)
                {
                    var j = Job(f, wall, -0.6f, H + 0.6f - 0.08f);
                    float len = FLen(f);
                    if (f == 0)
                    {
                        float x0 = 0.35f, x1 = len - 0.35f, y0 = 0.95f, y1 = Mathf.Min(2.2f, H - 0.7f);
                        var w = new Win
                        {
                            x0 = x0, x1 = x1, y0 = y0, y1 = y1, depth = 0.25f, muntins = 3, casing = 4, sill = false, wall = wall, reveal = HillMat.TrimWhite,
                            trim = HillMat.TrimWhite, frame = HillMat.TrimWhite, glass = HillMat.GlassShop, room = HillMat.InteriorShop, roomUV = HillAtlas.ShopInterior(ShopInteriorFor(sign, 5)), roomDepth = 1.4f,
                        };
                        AddWin(j, w);
                        j.after.Add(() =>
                        {
                            m.Box(HillMat.FenceWood, new Vector3(x0 - 0.05f, y0 - 0.06f, -0.25f), new Vector3(x1 + 0.05f, y0, 0.12f), 0.01f);
                            float sw = Mathf.Min(len - 0.4f, 3.2f), sh = sw / 8f;
                            m.Box(HillMat.TrimWhite, new Vector3(len * 0.5f - sw * 0.5f - 0.06f, H - sh - 0.2f, 0f), new Vector3(len * 0.5f + sw * 0.5f + 0.06f, H - 0.08f, 0.07f), 0.01f, Side.ZNeg);
                            Panel(m, HillMat.SignBoard, new Vector3(len * 0.5f, H - 0.14f - sh * 0.5f, 0.076f), Vector3.right * sw, Vector3.up * sh, Vector3.forward, HillAtlas.Sign(sign));
                            // Newspaper boards leaning on the front.
                            m.Box(HillMat.TrimWhite, new Vector3(0.2f, 0f, 0.02f), new Vector3(0.75f, 0.85f, 0.1f), 0.01f);
                        });
                    }
                    else if (f == 1)
                        AddDoor(j, new DoorSpec { x0 = len * 0.5f - 0.4f, x1 = len * 0.5f + 0.4f, height = Mathf.Min(2.05f, H - 0.5f), depth = 0.08f, style = 2, casing = 0, wall = wall, reveal = HillMat.TrimWhite, trim = HillMat.TrimWhite, leaf = HillMat.TrimDark, hallUV = HillAtlas.Interior(7) });
                    j.after.Add(() =>
                    {
                        m.Box(HillMat.TrimWhite, new Vector3(-0.03f, -0.6f, 0f), new Vector3(0.1f, H, 0.03f), 0f, Side.ZNeg);
                        m.Box(HillMat.TrimWhite, new Vector3(len - 0.1f, -0.6f, 0f), new Vector3(len + 0.03f, H, 0.03f), 0f, Side.ZNeg);
                        m.Box(HillMat.TrimWhite, new Vector3(-0.03f, H - 0.12f, 0f), new Vector3(len + 0.03f, H + 0.05f, 0.08f), 0.01f, Side.ZNeg);
                    });
                    Run(j);
                }
                FlatTop(0.6f, 0.25f, HillMat.TrimWhite, wall);
            }

            void Boathouse()
            {
                string wall = Or(b.wall, Chance(r, 0.5f) ? HillMat.FenceWood : HillMat.ShingleWall);
                const string trim = HillMat.TrimWhite;
                for (int f = 0; f < 4; f++)
                {
                    var j = Job(f, wall, -1.2f, H - 0.1f);
                    float len = FLen(f);
                    if (f == 0)
                    {
                        float dw = Mathf.Min(4f, len - 1.2f), x0 = len * 0.5f - dw * 0.5f, x1 = x0 + dw, dh = Mathf.Min(3f, H - 1.4f);
                        j.holes.Add(new Rect(x0, 0f, dw, dh));
                        j.after.Add(() =>
                        {
                            const float rc = 0.15f;
                            m.Quad(trim, new Vector3(x0, dh, 0f), new Vector3(x1, dh, 0f), new Vector3(x1, dh, -rc), new Vector3(x0, dh, -rc), Vector3.down);
                            m.Quad(trim, new Vector3(x0, 0f, 0f), new Vector3(x0, dh, 0f), new Vector3(x0, dh, -rc), new Vector3(x0, 0f, -rc), Vector3.right);
                            m.Quad(trim, new Vector3(x1, 0f, 0f), new Vector3(x1, dh, 0f), new Vector3(x1, dh, -rc), new Vector3(x1, 0f, -rc), Vector3.left);
                            foreach (var (a, c) in new[] { (x0, x0 + dw * 0.5f - 0.01f), (x0 + dw * 0.5f + 0.01f, x1) })
                            {
                                m.Box(HillMat.FenceWood, new Vector3(a, 0f, -rc), new Vector3(c, dh, -rc + 0.04f), 0f, Side.ZNeg);
                                m.Beam(trim, new Vector3(a + 0.1f, 0.2f, -rc + 0.06f), new Vector3(c - 0.1f, dh - 0.2f, -rc + 0.06f), 0.04f, 0.1f);
                                foreach (float y in new[] { 0.2f, dh - 0.2f })
                                    m.Box(trim, new Vector3(a, y - 0.06f, -rc + 0.04f), new Vector3(c, y + 0.06f, -rc + 0.08f), 0f, Side.ZNeg);
                            }
                            // The false gable: bargeboards over the doors and a round window between them.
                            float gy = dh + 0.2f, apexY = H - 0.15f;
                            m.Beam(trim, new Vector3(0.1f, gy, 0.04f), new Vector3(len * 0.5f, apexY, 0.04f), 0.05f, 0.22f, 0.006f);
                            m.Beam(trim, new Vector3(len - 0.1f, gy, 0.04f), new Vector3(len * 0.5f, apexY, 0.04f), 0.05f, 0.22f, 0.006f);
                            float rr = Mathf.Min(0.35f, (apexY - gy) * 0.3f);
                            if (rr > 0.15f)
                            {
                                Torus(m, trim, new Vector3(len * 0.5f, (gy + apexY) * 0.5f - 0.1f, 0.03f), Vector3.forward, rr, 0.04f, 14, 4);
                                Disc(m, HillMat.Glass, new Vector3(len * 0.5f, (gy + apexY) * 0.5f - 0.1f, 0.02f), Vector3.forward, rr - 0.02f, 14);
                                Disc(m, HillMat.Rubber, new Vector3(len * 0.5f, (gy + apexY) * 0.5f - 0.1f, 0.012f), Vector3.forward, rr - 0.02f, 14);
                            }
                        });
                    }
                    else if (f == 1)
                    {
                        AddDoor(j, new DoorSpec { x0 = 1f, x1 = 1.9f, height = 2f, depth = 0.08f, style = 1, casing = 0, wall = wall, reveal = trim, trim = trim, leaf = HillMat.DoorGreen, hallUV = HillAtlas.Interior(7) });
                        var w = new Win { x0 = len * 0.6f, x1 = len * 0.6f + 1.1f, y0 = 1.1f, y1 = 2.1f, muntins = 5, casing = 0, wall = wall, reveal = trim, trim = trim, frame = trim, roomUV = HillAtlas.Interior(5), roomDepth = 1.2f };
                        if (w.x1 + 0.3f < len) AddWin(j, w);
                    }
                    j.after.Add(() =>
                    {
                        m.Box(trim, new Vector3(-0.03f, -1.2f, 0f), new Vector3(0.12f, H - 0.1f, 0.03f), 0f, Side.ZNeg);
                        m.Box(trim, new Vector3(len - 0.12f, -1.2f, 0f), new Vector3(len + 0.03f, H - 0.1f, 0.03f), 0f, Side.ZNeg);
                        m.Box(trim, new Vector3(-0.04f, H - 0.4f, 0f), new Vector3(len + 0.04f, H, 0.1f), 0.012f, Side.ZNeg);
                        m.Box(HillMat.StoneWall, new Vector3(-0.05f, -1.4f, 0f), new Vector3(len, -0.05f, 0.05f), 0.02f, Side.ZNeg);
                    });
                    Run(j);
                }
                FlatTop(0f, 0.2f, HillMat.Gutter, wall);
            }

            void WaterTower()
            {
                float rad = Mathf.Min(FL, D) * 0.5f - 0.45f, cz = D * 0.5f, legTop = H * 0.55f, tankTop = H - Mathf.Min(1.6f, rad * 0.6f);
                var c = new Vector3(0f, 0f, cz);
                string metal = Or(b.wall, HillMat.MetalPaint);
                for (int i = 0; i < 4; i++)
                {
                    float a = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                    var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    m.Cylinder(HillMat.MetalDark, c + d * rad * 0.95f + Vector3.down * 0.3f, c + d * rad * 0.8f + Vector3.up * legTop, 0.12f, 8, false);
                    m.Box(HillMat.Concrete, c + d * rad * 0.95f + new Vector3(-0.3f, -0.6f, -0.3f), c + d * rad * 0.95f + new Vector3(0.3f, 0.25f, 0.3f), 0.03f);
                    var e = new Vector3(Mathf.Cos(a + Mathf.PI * 0.5f), 0f, Mathf.Sin(a + Mathf.PI * 0.5f));
                    foreach (float y in new[] { legTop * 0.3f, legTop * 0.7f })
                    {
                        m.Beam(HillMat.MetalDark, c + d * rad * 0.9f + Vector3.up * y, c + e * rad * 0.9f + Vector3.up * (y + legTop * 0.35f), 0.05f, 0.05f);
                        m.Beam(HillMat.MetalDark, c + d * rad * 0.9f + Vector3.up * (y + legTop * 0.35f), c + e * rad * 0.9f + Vector3.up * y, 0.05f, 0.05f);
                    }
                }
                m.Cylinder(HillMat.MetalDark, c + Vector3.down * 0.3f, c + Vector3.up * legTop, 0.25f, 10, false);
                m.Tube(metal, new[] { c + Vector3.up * (legTop - 0.4f), c + Vector3.up * legTop, c + Vector3.up * (legTop + 0.1f), c + Vector3.up * tankTop }, new[] { rad * 0.4f, rad * 0.92f, rad, rad }, 20, false, 2f);
                Disc(m, metal, c + Vector3.up * (legTop - 0.4f), Vector3.down, rad * 0.4f, 12);
                foreach (float k in new[] { 0.33f, 0.66f })
                    m.Cylinder(HillMat.MetalDark, c + Vector3.up * Mathf.Lerp(legTop, tankTop, k), c + Vector3.up * (Mathf.Lerp(legTop, tankTop, k) + 0.08f), rad + 0.02f, 20, false);
                // The catwalk with its railing, inside the box.
                float cw = rad + 0.35f;
                var deck = new List<Vector2>();
                for (int i = 0; i < 20; i++)
                {
                    float a = i * Mathf.PI * 2f / 20f;
                    deck.Add(new Vector2(Mathf.Cos(a) * cw, Mathf.Sin(a) * cw + cz));
                }
                m.Prism(HillMat.Steel, deck, legTop - 0.05f, legTop, true, true);
                Torus(m, HillMat.Steel, c + Vector3.up * (legTop + 1.0f), Vector3.up, cw - 0.03f, 0.025f, 20, 4);
                for (int i = 0; i < 20; i++)
                {
                    float a = i * Mathf.PI * 2f / 20f;
                    var p = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (cw - 0.03f);
                    m.Cylinder(HillMat.Steel, p + Vector3.up * legTop, p + Vector3.up * (legTop + 1.0f), 0.015f, 4, false);
                }
                // The conical roof, its point at the box's top.
                var apex = c + Vector3.up * H;
                for (int i = 0; i < 20; i++)
                {
                    float a0 = i * Mathf.PI * 2f / 20f, a1 = (i + 1) * Mathf.PI * 2f / 20f;
                    var p0 = c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * (rad + 0.15f) + Vector3.up * tankTop;
                    var p1 = c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * (rad + 0.15f) + Vector3.up * tankTop;
                    m.Tri(HillMat.RoofMetal, p0, p1, apex, (p0 + p1) * 0.5f - c + Vector3.up);
                }
                // A ladder up a leg.
                var l0 = c + new Vector3(rad * 0.7f, 0f, 0f);
                for (float y = 0.4f; y < legTop - 0.2f; y += 0.3f)
                    m.Cylinder(HillMat.Steel, l0 + new Vector3(0f, y, -0.22f), l0 + new Vector3(0f, y, 0.22f), 0.015f, 4, false);
                foreach (float z in new[] { -0.22f, 0.22f })
                    m.Box(HillMat.Steel, l0 + new Vector3(-0.02f, 0f, z - 0.02f), l0 + new Vector3(0.02f, legTop, z + 0.02f));
            }

            void Treehouse()
            {
                const string wood = HillMat.FenceWood;
                float hx = FL * 0.5f, deckY = 0f, wallTop = Mathf.Max(1.4f, H * 0.6f);
                // Posts and braces down to the box's bottom, the deck.
                foreach (int sx in new[] { -1, 1 })
                    foreach (float z in new[] { 0.15f, D - 0.15f })
                    {
                        m.Box(wood, new Vector3(sx * (hx - 0.15f) - 0.08f, -1.5f, z - 0.08f), new Vector3(sx * (hx - 0.15f) + 0.08f, deckY - 0.15f, z + 0.08f), 0.01f);
                        m.Beam(wood, new Vector3(sx * (hx - 0.15f), -0.9f, z), new Vector3(sx * (hx - 0.15f) * 0.5f, deckY - 0.2f, z), 0.06f, 0.06f);
                    }
                m.Box(HillMat.Deck, new Vector3(-hx, deckY - 0.15f, 0f), new Vector3(hx, deckY, D), 0.01f);
                // The plank house, inset from the deck's edge, with a door, a window and a gable roof inside the box.
                float ix = hx - 0.35f, iz0 = 0.6f, iz1 = D - 0.2f;
                if (iz1 - iz0 < 1f) iz0 = 0.2f;
                var faces = new[]
                {
                    (o: new Vector3(-ix, 0f, iz0), a: Vector3.right, n: Vector3.back, len: ix * 2f),
                    (o: new Vector3(ix, 0f, iz0), a: Vector3.forward, n: Vector3.right, len: iz1 - iz0),
                    (o: new Vector3(ix, 0f, iz1), a: Vector3.left, n: Vector3.forward, len: ix * 2f),
                    (o: new Vector3(-ix, 0f, iz1), a: Vector3.back, n: Vector3.left, len: iz1 - iz0),
                };
                float ridge = H - 0.02f;
                // The walls reach just into the roof slab (whose top runs from the deck's edge up to the ridge).
                float wallEdge = wallTop - 0.1f + (ridge - wallTop + 0.1f) * (hx - ix) / hx - 0.03f;
                for (int f = 0; f < 4; f++)
                {
                    var fc = faces[f];
                    float edgeA = wallEdge, edgeB = wallEdge;
                    var outline = new List<Vector2> { new Vector2(0f, deckY), new Vector2(fc.len, deckY), new Vector2(fc.len, edgeB) };
                    if (f == 0 || f == 2) outline.Add(new Vector2(fc.len * 0.5f, ridge - 0.03f));
                    outline.Add(new Vector2(0f, edgeA));
                    var holes = new List<Rect>();
                    Win w = null;
                    Rect door = default;
                    if (f == 0)
                    {
                        door = new Rect(fc.len * 0.3f - 0.3f, deckY, 0.6f, Mathf.Min(1.2f, wallEdge - 0.2f));
                        holes.Add(door);
                        w = new Win { x0 = fc.len * 0.68f - 0.25f, x1 = fc.len * 0.68f + 0.25f, y0 = 0.5f, y1 = Mathf.Min(1.0f, wallEdge - 0.25f), depth = 0.04f, muntins = 0, casing = 0, wall = wood, reveal = HillMat.TrimWhite, trim = HillMat.TrimWhite, frame = HillMat.TrimWhite, roomUV = HillAtlas.Interior(6), roomDepth = 0.6f, headCap = false };
                        if (w.x0 > door.xMax + 0.25f && w.y1 > w.y0 + 0.2f) holes.Add(w.Hole); else w = null;
                    }
                    m.Push(WallFrame(fc.o, fc.a, fc.n));
                    try
                    {
                        WallFace(m, wood, outline, holes);
                        if (f == 0)
                        {
                            Room(m, HillMat.Interior, HillAtlas.Interior(6), false, door.xMin, door.xMax, door.yMin, door.yMax, 0f, 1.0f);
                            m.Box(HillMat.TrimWhite, new Vector3(door.xMin - 0.05f, door.yMax, 0f), new Vector3(door.xMax + 0.05f, door.yMax + 0.06f, 0.03f), 0f, Side.ZNeg);
                            if (w != null) Window(m, w, r);
                        }
                        for (float y = 0.25f; f != 0 && y < wallEdge; y += 0.25f)
                        {
                            var sp = Span(outline, y);
                            m.Box(wood, new Vector3(sp.x, y - 0.01f, 0f), new Vector3(sp.y, y + 0.01f, 0.012f), 0f, Side.ZNeg);
                        }
                    }
                    finally { m.Pop(); }
                }
                foreach (int sx in new[] { -1, 1 })
                {
                    var pts = new[] { new Vector3(sx * hx, wallTop - 0.1f, iz0 - 0.15f), new Vector3(0f, ridge, iz0 - 0.15f), new Vector3(0f, ridge, iz1 + 0.15f), new Vector3(sx * hx, wallTop - 0.1f, iz1 + 0.15f) };
                    if (sx > 0) System.Array.Reverse(pts);
                    Slab(m, pts, new[] { true, false, true, true }, Chance(r, 0.5f) ? HillMat.RoofGreen : HillMat.RoofRed, wood, HillMat.TrimWhite, 0.06f);
                }
                // A low railing round the deck's open front and a rope down to the ground.
                foreach (float x in new[] { -hx + 0.05f, hx - 0.05f })
                    m.Box(wood, new Vector3(x - 0.04f, deckY, 0.02f), new Vector3(x + 0.04f, deckY + 0.7f, 0.1f), 0.008f);
                m.Box(wood, new Vector3(-hx + 0.05f, deckY + 0.62f, 0.03f), new Vector3(hx - 0.05f, deckY + 0.7f, 0.09f), 0.008f);
                var ropeTop = new Vector3(hx * 0.5f, deckY - 0.15f, 0.08f);
                m.Tube(HillMat.Canvas, new[] { ropeTop, ropeTop + new Vector3(0.03f, -0.7f, -0.02f), ropeTop + new Vector3(-0.02f, -1.3f, 0.01f) }, new[] { 0.02f }, 5, false, 1f);
                for (int k = 1; k <= 2; k++) m.Ellipsoid(HillMat.Canvas, ropeTop + new Vector3(0f, -0.45f * k, 0f), new Vector3(0.04f, 0.05f, 0.04f), 6);
            }

            void Terrace()
            {
                string wall = Or(b.wall, HillMat.BrickRed);
                bool brick = wall.StartsWith("brick");
                int n = Mathf.Max(1, Mathf.RoundToInt(FL / 5f));
                float bw = FL / n;
                string[] doors = { HillMat.DoorRed, HillMat.DoorBlue, HillMat.DoorGreen, HillMat.DoorYellow, HillMat.TrimDark };
                for (int f = 0; f < 4; f++)
                {
                    var j = Job(f, wall, -1.2f, H);
                    float len = FLen(f);
                    if (f == 0 || f == 2)
                        for (int i = 0; i < n; i++)
                        {
                            float s0 = bw * i;
                            if (f == 0) AddDoor(j, new DoorSpec { x0 = s0 + 0.5f, x1 = s0 + 1.45f, height = 2.1f, depth = 0.16f, style = (i + b.seed) % 4, transom = true, casing = brick ? 1 : 2, wall = wall, reveal = wall, trim = HillMat.TrimWhite, leaf = doors[(i + b.seed) % doors.Length], hallUV = HillAtlas.Interior(3) });
                            for (float y = 0f; y + 2.4f < H; y += 2.9f)
                            {
                                if (f == 0 && y == 0f) AddWin(j, Sash(s0 + bw * 0.68f, 0.85f, 1.3f, 1.5f, wall, brick ? 1 : 2, HillMat.TrimWhite, 2, Chance(r, 0.3f)));
                                else
                                {
                                    AddWin(j, Sash(s0 + bw * 0.3f, y + 0.85f, 0.9f, 1.4f, wall, brick ? 1 : 2, HillMat.TrimWhite, 2, Chance(r, 0.3f)));
                                    AddWin(j, Sash(s0 + bw * 0.7f, y + 0.85f, 0.9f, 1.4f, wall, brick ? 1 : 2, HillMat.TrimWhite, 2, Chance(r, 0.3f)));
                                }
                            }
                        }
                    j.after.Add(() =>
                    {
                        PlinthBand(len, 0f);
                        m.Box(HillMat.TrimCream, new Vector3(-0.05f, H - 0.4f, 0f), new Vector3(len + 0.05f, H - 0.12f, 0.12f), 0.02f, Side.ZNeg);
                    });
                    Run(j);
                }
                FlatTop(0f, 0.3f, HillMat.Gutter, wall);
            }
        }
    }
}

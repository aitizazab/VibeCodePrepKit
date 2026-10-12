using System.Collections.Generic;
using JamKit.DeepBlock;
using UnityEngine;
using static JamKit.DreamEscape.EditorTools.HillKit;
using Random = System.Random;
using Side = JamKit.DreamEscape.EditorTools.HillMesh.Side;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// The hill's props, each drawn at real-world scale inside its <see cref="HillProp.Size"/> box (the collider
    /// <see cref="HillColliders.Prop"/> makes), turned by its yaw to the nearest quarter like the collider. Local frame:
    /// x across, y up from the foot, z along (a car's nose at +z). Variants (<see cref="HillProp.variant"/>) pick
    /// colours, body styles and contents.
    /// </summary>
    public static class HillProps
    {
        static readonly string[] Paints =
        {
            HillMat.DoorRed, HillMat.DoorBlue, HillMat.DoorGreen, HillMat.DoorYellow, HillMat.TrimCream, HillMat.TrimWhite,
            HillMat.TrimDark, HillMat.MetalPaint, HillMat.Copper,
        };

        public static void Draw(HillMesh m, HillProp p)
        {
            if (p == null) return;
            var s = U(p.Size);
            int q = ((Mathf.RoundToInt(p.yaw / 90f) % 4) + 4) % 4;
            var pos = U(p.position);
            var r = new Random(p.variant * 7919 + Mathf.RoundToInt(pos.x * 13f) * 31 + Mathf.RoundToInt(pos.z * 17f) * 7 + (int)p.kind);
            m.Push(pos, q * 90f);
            try { new Prop(m, r, s, p.variant, p.kind).Draw(); }
            finally { m.Pop(); }
        }

        /// <summary>The world positions of a power pole's insulator tops (where the builder hangs its wires).</summary>
        public static Vector3[] PowerPoleInsulators(HillProp p)
        {
            var s = U(p.Size);
            int q = ((Mathf.RoundToInt(p.yaw / 90f) % 4) + 4) % 4;
            var rot = Quaternion.Euler(0f, q * 90f, 0f);
            float y = s.y - 0.6f + 0.22f;
            var local = new[] { new Vector3(-1.05f, y, 0f), new Vector3(-0.45f, y, 0f), new Vector3(0.45f, y, 0f), new Vector3(1.05f, y, 0f) };
            var res = new Vector3[local.Length];
            for (int i = 0; i < local.Length; i++) res[i] = U(p.position) + rot * local[i];
            return res;
        }

        sealed class Prop
        {
            readonly HillMesh m;
            readonly Random r;
            readonly Vector3 s;
            readonly int v;
            readonly HillPropKind kind;

            public Prop(HillMesh m, Random r, Vector3 s, int v, HillPropKind kind) { this.m = m; this.r = r; this.s = s; this.v = v; this.kind = kind; }

            string Paint(int offset = 0) => Paints[(((v + offset) % Paints.Length) + Paints.Length) % Paints.Length];

            // Shorthands: a box by centre and size; a cylinder between two points.
            void B(string mat, float cx, float cy, float cz, float sx, float sy, float sz, float bevel = 0f, Side skip = Side.None) =>
                m.BoxC(mat, new Vector3(cx, cy, cz), new Vector3(sx, sy, sz), bevel, skip);
            void Cyl(string mat, Vector3 a, Vector3 b, float rad, int sides = 8, bool caps = true) => m.Cylinder(mat, a, b, rad, sides, caps);
            static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
            /// <summary>A box between two corners in any order (mirrored sides pass their corners flipped).</summary>
            void Bx(string mat, Vector3 a, Vector3 b, float bevel = 0f, Side skip = Side.None) => m.Box(mat, Vector3.Min(a, b), Vector3.Max(a, b), bevel, skip);

            /// <summary>A lathe along y at (x, z): rings at heights <paramref name="ys"/> with radii <paramref name="rs"/>.</summary>
            void Lathe(string mat, float x, float z, float[] ys, float[] rs, int sides = 10, bool cap = true)
            {
                var path = new Vector3[ys.Length];
                for (int i = 0; i < ys.Length; i++) path[i] = new Vector3(x, ys[i], z);
                m.Tube(mat, path, rs, sides, cap, 1f);
            }

            /// <summary>A frustum between two horizontal rectangles (y0 and y1); optionally faced inward too (open tubs).</summary>
            void Loft(string mat, float y0, float ax0, float ax1, float az0, float az1, float y1, float bx0, float bx1, float bz0, float bz1, bool top, bool bottom, bool inside = false)
            {
                var a = new[] { V(ax0, y0, az0), V(ax1, y0, az0), V(ax1, y0, az1), V(ax0, y0, az1) };
                var b = new[] { V(bx0, y1, bz0), V(bx1, y1, bz0), V(bx1, y1, bz1), V(bx0, y1, bz1) };
                var c = (a[0] + a[2] + b[0] + b[2]) * 0.25f;
                for (int i = 0; i < 4; i++)
                {
                    int j = (i + 1) % 4;
                    var mid = (a[i] + a[j] + b[i] + b[j]) * 0.25f - c;
                    mid.y = 0f;
                    m.Quad(mat, a[i], a[j], b[j], b[i], mid);
                    if (inside) m.Quad(mat, a[i], a[j], b[j], b[i], -mid);
                }
                if (top) m.Poly(mat, b, Vector3.up);
                if (bottom) m.Poly(mat, a, Vector3.down);
                if (inside) m.Poly(mat, a, Vector3.up);
            }

            public void Draw()
            {
                switch (kind)
                {
                    case HillPropKind.Car: Car(false); break;
                    case HillPropKind.Van: Car(true); break;
                    case HillPropKind.Bike: Bike(); break;
                    case HillPropKind.BikeRack: BikeRack(); break;
                    case HillPropKind.Bin: Bin(); break;
                    case HillPropKind.Wheelie: Wheelie(); break;
                    case HillPropKind.Mailbox: Mailbox(); break;
                    case HillPropKind.Hydrant: Hydrant(); break;
                    case HillPropKind.Streetlight: Streetlight(); break;
                    case HillPropKind.PowerPole: PowerPole(); break;
                    case HillPropKind.Bench: Bench(); break;
                    case HillPropKind.Planter: Planter(); break;
                    case HillPropKind.BusShelter: BusShelter(); break;
                    case HillPropKind.BusStopSign: BusStopSign(); break;
                    case HillPropKind.PhoneBox: PhoneBox(); break;
                    case HillPropKind.Fountain: Fountain(); break;
                    case HillPropKind.Swing: Swing(); break;
                    case HillPropKind.Slide: Slide(); break;
                    case HillPropKind.Trampoline: Trampoline(); break;
                    case HillPropKind.Shed: Shed(); break;
                    case HillPropKind.Greenhouse: Greenhouse(); break;
                    case HillPropKind.WashingLine: WashingLine(); break;
                    case HillPropKind.Grill: Grill(); break;
                    case HillPropKind.Sprinkler: Sprinkler(); break;
                    case HillPropKind.Mower: Mower(); break;
                    case HillPropKind.YardSaleTable: YardSale(); break;
                    case HillPropKind.PicnicTable: PicnicTable(); break;
                    case HillPropKind.Sandbox: Sandbox(); break;
                    case HillPropKind.Crates: Crates(); break;
                    case HillPropKind.Pallets: Pallets(); break;
                    case HillPropKind.Barrel: Barrel(); break;
                    case HillPropKind.TyreStack: TyreStack(); break;
                    case HillPropKind.Workbench: Workbench(); break;
                    case HillPropKind.Ladder: Ladder(); break;
                    case HillPropKind.Wheelbarrow: Wheelbarrow(); break;
                    case HillPropKind.Kennel: Kennel(); break;
                    case HillPropKind.BirdBath: BirdBath(); break;
                    case HillPropKind.Gnome: Gnome(); break;
                    case HillPropKind.StreetSign: StreetSign(); break;
                    case HillPropKind.Bollard: Bollard(); break;
                    case HillPropKind.Railing: Railing(); break;
                    case HillPropKind.FlowerBed: FlowerBed(); break;
                    case HillPropKind.Bush: HillTrees.DrawBush(m, Vector3.zero, s, v * 31 + r.Next(1000)); break;
                    case HillPropKind.Rock: Rock(); break;
                    case HillPropKind.AcUnit: AcUnit(); break;
                    case HillPropKind.SatelliteDish: Dish(); break;
                    case HillPropKind.Hose: Hose(); break;
                    case HillPropKind.PaddlingPool: PaddlingPool(); break;
                    case HillPropKind.Chalk: Chalk(); break;
                    case HillPropKind.Ball: Ball(); break;
                    default: B(HillMat.FenceWood, 0f, s.y * 0.5f, 0f, s.x, s.y, s.z, 0.02f); break;
                }
            }

            // ---------- Vehicles ----------

            /// <summary>A stylized 1970s-90s family car (saloon, estate or hatchback by variant) or a van: a body
            /// extruded from its side profile with square wheel arches cut in the sides, a glass cabin with painted
            /// pillars and seats behind, wheels with hubcaps, bumpers, lights, grille, plates, mirrors, door seams.</summary>
            void Car(bool van)
            {
                float W = s.x, H = s.y, L = s.z, Lb = L - 0.08f, hb = Lb * 0.5f;
                string paint = Paint(v / 3);
                if (paint == HillMat.TrimDark && Chance(r, 0.5f)) paint = HillMat.DoorBlue;
                int body = van ? 3 : ((v % 3) + 3) % 3;
                bool chrome = Chance(r, 0.6f);
                float wr = van ? H * 0.16f : H * 0.21f;
                float wy = wr, wzF = van ? hb - 0.85f : 0.32f * L, wzB = van ? -hb + 0.95f : -0.3f * L;
                float sideX = W * 0.5f - 0.025f;
                float sill = van ? H * 0.17f : H * 0.2f, belt = van ? H * 0.5f : H * 0.64f, nose = van ? H * 0.42f : H * 0.52f;
                // The side profile (z, y), convex.
                var profile = van
                    ? new List<Vector2> { new Vector2(-hb, sill), new Vector2(hb, sill), new Vector2(hb, nose), new Vector2(hb - 0.28f, belt), new Vector2(-hb, belt) }
                    : new List<Vector2>
                    {
                        new Vector2(-hb, sill), new Vector2(hb, sill), new Vector2(hb, nose), new Vector2(hb - 0.1f, H * 0.575f),
                        new Vector2(0.16f * L, belt), new Vector2(body == 0 ? -0.36f * L : -0.47f * L, belt + 0.005f),
                        new Vector2(-hb + 0.06f, body == 0 ? H * 0.6f : belt), new Vector2(-hb, nose),
                    };
                Extrude(m, paint, profile, Vector3.forward, Vector3.up, Vector3.right, -sideX, sideX, false, false);
                // The sides, with the wheel arches cut out.
                float archW = wr + 0.06f, archTop = wy + wr + 0.06f;
                foreach (int side in new[] { -1, 1 })
                {
                    var origin = side > 0 ? V(sideX, 0f, -hb) : V(-sideX, 0f, hb);
                    m.Push(WallFrame(origin, side > 0 ? Vector3.forward : Vector3.back, side > 0 ? Vector3.right : Vector3.left));
                    var outline = new List<Vector2>();
                    foreach (var pt in profile) outline.Add(new Vector2(side > 0 ? pt.x + hb : hb - pt.x, pt.y));
                    if (side < 0) outline.Reverse();
                    var holes = new List<Rect>();
                    foreach (float wz in new[] { wzF, wzB })
                    {
                        float u = side > 0 ? wz + hb : hb - wz;
                        holes.Add(new Rect(u - archW, sill, archW * 2f, archTop - sill));
                    }
                    WallFace(m, paint, outline, holes);
                    foreach (var h in holes)
                    {
                        // The wheel well: dark faces behind the cut.
                        float d = 0.32f;
                        m.Quad(HillMat.Rubber, V(h.xMin, h.yMax, 0f), V(h.xMax, h.yMax, 0f), V(h.xMax, h.yMax, -d), V(h.xMin, h.yMax, -d), Vector3.down);
                        m.Quad(HillMat.Rubber, V(h.xMin, h.yMin, 0f), V(h.xMin, h.yMax, 0f), V(h.xMin, h.yMax, -d), V(h.xMin, h.yMin, -d), Vector3.right);
                        m.Quad(HillMat.Rubber, V(h.xMax, h.yMin, 0f), V(h.xMax, h.yMax, 0f), V(h.xMax, h.yMax, -d), V(h.xMax, h.yMin, -d), Vector3.left);
                        m.Quad(HillMat.Rubber, V(h.xMin, h.yMin, -d), V(h.xMax, h.yMin, -d), V(h.xMax, h.yMax, -d), V(h.xMin, h.yMax, -d), Vector3.forward);
                        // An arch lip.
                        Bx(HillMat.Rubber, V(h.xMin - 0.03f, h.yMax, 0f), V(h.xMax + 0.03f, h.yMax + 0.035f, 0.012f), 0.005f, Side.ZNeg);
                    }
                    // A rubbing strip and the door seams and handles.
                    float stripY = van ? H * 0.36f : H * 0.43f;
                    float fz = side > 0 ? 1f : -1f;
                    Bx(chrome ? HillMat.Steel : HillMat.Rubber, V(0.25f, stripY - 0.02f, 0f), V(Lb - 0.25f, stripY + 0.02f, 0.014f), 0.005f, Side.ZNeg);
                    float[] seams = van ? new[] { 0.42f * L, 0.12f * L, -0.25f * L } : new[] { 0.13f * L, -0.07f * L, body == 0 ? -0.27f * L : -0.28f * L };
                    foreach (float sz in seams)
                    {
                        float u = side > 0 ? sz + hb : hb - sz;
                        m.Quad(HillMat.Rubber, V(u - 0.006f, sill + 0.03f, 0.007f), V(u - 0.006f, belt - 0.02f, 0.007f), V(u + 0.006f, belt - 0.02f, 0.007f), V(u + 0.006f, sill + 0.03f, 0.007f), Vector3.forward);
                    }
                    for (int i = 0; i + 1 < seams.Length && i < 2; i++)
                    {
                        float u = (side > 0 ? seams[i] + hb : hb - seams[i]) - fz * 0.0f - 0.18f * (side > 0 ? 1f : -1f);
                        Bx(HillMat.Steel, V(u - 0.06f, belt - 0.1f, 0f), V(u + 0.06f, belt - 0.07f, 0.016f), 0.004f, Side.ZNeg);
                    }
                    m.Pop();
                }
                // Wheels: tyres inside the arches, hubcaps, centre caps.
                foreach (float wz in new[] { wzF, wzB })
                    foreach (int side in new[] { -1, 1 })
                    {
                        float x1 = side * (W * 0.5f - 0.035f), x0 = side * (W * 0.5f - 0.235f);
                        Cyl(HillMat.Rubber, V(x0, wy, wz), V(x1, wy, wz), wr, 14);
                        Cyl(HillMat.Steel, V(x1 - side * 0.01f, wy, wz), V(x1 + side * 0.008f, wy, wz), wr * 0.62f, 12);
                        Cyl(chrome ? HillMat.Steel : HillMat.MetalDark, V(x1, wy, wz), V(x1 + side * 0.016f, wy, wz), wr * 0.18f, 8);
                        for (int k = 0; k < 4; k++)
                        {
                            float a = k * Mathf.PI * 0.5f + 0.4f;
                            Bx(HillMat.MetalDark, V(x1 + side * 0.008f - 0.004f, wy + Mathf.Sin(a) * wr * 0.38f - 0.012f, wz + Mathf.Cos(a) * wr * 0.38f - 0.012f),
                                V(x1 + side * 0.008f + 0.004f, wy + Mathf.Sin(a) * wr * 0.38f + 0.012f, wz + Mathf.Cos(a) * wr * 0.38f + 0.012f));
                        }
                    }
                // The cabin.
                float roofY = H - 0.012f;
                if (van)
                {
                    // The van's cargo box up to its walkable roof, the cab's glass in front of it.
                    float boxFront = hb - 1.15f;
                    Bx(paint, V(-sideX, belt, -hb), V(sideX, H, boxFront), 0.05f, Side.YNeg);
                    Cabin(paint, belt, sideX - 0.03f, boxFront, hb - 0.3f, roofY, sideX - 0.12f, boxFront, boxFront + 0.18f, true, false, 0f);
                    Bx(paint, V(-sideX + 0.12f, roofY - 0.06f, boxFront - 0.02f), V(sideX - 0.12f, roofY, boxFront + 0.2f), 0f, Side.YNeg);
                    // Rear doors and a sliding door rail.
                    m.Quad(HillMat.Rubber, V(-0.006f, sill + 0.05f, -hb - 0.007f), V(-0.006f, H - 0.08f, -hb - 0.007f), V(0.006f, H - 0.08f, -hb - 0.007f), V(0.006f, sill + 0.05f, -hb - 0.007f), Vector3.back);
                    foreach (int side in new[] { -1, 1 })
                        Bx(HillMat.Steel, V(side * sideX - 0.008f, belt + 0.1f, -0.3f * L), V(side * sideX + 0.008f, belt + 0.13f, boxFront - 0.1f), 0f);
                    foreach (int side in new[] { -1, 1 })
                    {
                        Bx(HillMat.Glass, V(side * 0.45f - 0.35f, H * 0.62f, -hb - 0.01f), V(side * 0.45f + 0.35f, H * 0.85f, -hb), 0.01f);
                    }
                    // A stripe along the cargo box.
                    foreach (int side in new[] { -1, 1 })
                        Bx(Paint(v + 4), V(side * sideX - 0.006f, belt + 0.28f, -hb + 0.1f), V(side * sideX + 0.006f, belt + 0.38f, boxFront - 0.1f), 0f);
                }
                else
                {
                    float z0 = body == 0 ? -0.3f * L : -0.46f * L, z1 = 0.16f * L;
                    float t0 = body == 0 ? -0.18f * L : body == 1 ? -0.45f * L : -0.3f * L, t1 = 0.03f * L;
                    Cabin(paint, belt, sideX - 0.03f, z0, z1, roofY, sideX - 0.17f, t0, t1, true, true, -0.07f * L);
                    if (Chance(r, 0.35f))
                    {
                        // Roof bars (an estate's rack).
                        foreach (int side in new[] { -1, 1 })
                            Bx(HillMat.MetalDark, V(side * (sideX - 0.25f) - 0.02f, roofY, t0 + 0.1f), V(side * (sideX - 0.25f) + 0.02f, H, t1 - 0.05f), 0.005f);
                    }
                }
                // Bumpers, grille, lights, plates, mirrors, exhaust.
                string bumper = chrome ? HillMat.Steel : HillMat.Rubber;
                float by = sill + 0.08f;
                Bx(bumper, V(-W * 0.5f + 0.03f, by - 0.06f, hb - 0.02f), V(W * 0.5f - 0.03f, by + 0.06f, L * 0.5f), 0.025f);
                Bx(bumper, V(-W * 0.5f + 0.03f, by - 0.06f, -L * 0.5f), V(W * 0.5f - 0.03f, by + 0.06f, -hb + 0.02f), 0.025f);
                float ly = (by + 0.06f + nose) * 0.5f + 0.02f;
                Bx(HillMat.MetalDark, V(-W * 0.22f, ly - 0.07f, hb - 0.01f), V(W * 0.22f, ly + 0.07f, hb + 0.01f), 0.01f);
                for (int i = 0; i < 3; i++) Bx(HillMat.Steel, V(-W * 0.21f, ly - 0.05f + i * 0.05f - 0.008f, hb + 0.005f), V(W * 0.21f, ly - 0.05f + i * 0.05f + 0.008f, hb + 0.016f));
                bool round = (v / 2) % 2 == 0;
                foreach (int side in new[] { -1, 1 })
                {
                    float lx = side * (W * 0.5f - 0.24f);
                    if (round)
                    {
                        Cyl(HillMat.Steel, V(lx, ly, hb - 0.01f), V(lx, ly, hb + 0.012f), 0.085f, 12);
                        Disc(m, HillMat.Glass, V(lx, ly, hb + 0.014f), Vector3.forward, 0.072f, 12);
                        Disc(m, HillMat.LampGlow, V(lx, ly, hb + 0.0125f), Vector3.forward, 0.03f, 8);
                    }
                    else
                    {
                        Bx(HillMat.Steel, V(lx - 0.13f, ly - 0.06f, hb - 0.01f), V(lx + 0.13f, ly + 0.06f, hb + 0.012f), 0.008f);
                        m.Quad(HillMat.Glass, V(lx - 0.12f, ly - 0.05f, hb + 0.0135f), V(lx - 0.12f, ly + 0.05f, hb + 0.0135f), V(lx + 0.12f, ly + 0.05f, hb + 0.0135f), V(lx + 0.12f, ly - 0.05f, hb + 0.0135f), Vector3.forward);
                    }
                    Bx(HillMat.DoorYellow, V(side * (W * 0.5f - 0.1f) - 0.05f, by + 0.07f, hb - 0.01f), V(side * (W * 0.5f - 0.1f) + 0.05f, by + 0.11f, hb + 0.01f), 0.005f);
                    // Tail lights.
                    Bx(HillMat.DoorRed, V(side * (W * 0.5f - 0.2f) - 0.15f, ly - 0.06f, -hb - 0.012f), V(side * (W * 0.5f - 0.2f) + 0.15f, ly + 0.06f, -hb + 0.01f), 0.01f);
                    Bx(HillMat.DoorYellow, V(side * (W * 0.5f - 0.2f) - 0.15f, ly - 0.1f, -hb - 0.01f), V(side * (W * 0.5f - 0.2f) + 0.15f, ly - 0.06f, -hb + 0.01f), 0.005f);
                    // Mirrors.
                    float mz = van ? hb - 0.42f : 0.15f * L;
                    Bx(paint, V(side * (W * 0.5f - 0.11f), belt + 0.03f, mz - 0.03f), V(side * (W * 0.5f - 0.005f), belt + 0.06f, mz + 0.03f), 0.006f);
                    Bx(paint, V(side * (W * 0.5f - 0.06f) - 0.05f, belt + 0.06f, mz - 0.05f), V(side * (W * 0.5f - 0.06f) + 0.05f, belt + 0.16f, mz + 0.03f), 0.015f);
                }
                Bx(HillMat.TrimWhite, V(-0.26f, by - 0.055f, L * 0.5f), V(0.26f, by + 0.055f, L * 0.5f + 0.008f), 0.004f);
                Bx(HillMat.DoorYellow, V(-0.26f, ly - 0.06f, -hb - 0.014f), V(0.26f, ly + 0.06f, -hb - 0.004f), 0.004f);
                Cyl(HillMat.Steel, V(W * 0.3f, sill - 0.02f, -hb + 0.1f), V(W * 0.3f, sill - 0.02f, -L * 0.5f + 0.005f), 0.025f, 6);
                Cyl(HillMat.MetalDark, V(-W * 0.5f + 0.2f, nose, hb - 0.4f), V(-W * 0.5f + 0.2f, H * 0.95f, hb - 0.55f), 0.004f, 4, false);
            }

            /// <summary>A glass cabin lofted from a belt-line rectangle (y0; x half-width bx; z from bz0 to bz1) to a
            /// roof rectangle (y1; tx; tz0..tz1): each face a glass pane framed by painted pillars, a painted roof, seats,
            /// dash and wheel inside.</summary>
            void Cabin(string paint, float y0, float bx, float bz0, float bz1, float y1, float tx, float tz0, float tz1, bool glazeFront, bool glazeBack, float bPillarZ)
            {
                var a = new[] { V(-bx, y0, bz0), V(bx, y0, bz0), V(bx, y0, bz1), V(-bx, y0, bz1) };
                var b = new[] { V(-tx, y1, tz0), V(tx, y1, tz0), V(tx, y1, tz1), V(-tx, y1, tz1) };
                // Faces: back (0-1), right (1-2), front (2-3), left (3-0).
                for (int i = 0; i < 4; i++)
                {
                    int j = (i + 1) % 4;
                    var A = a[i]; var Bv = a[j]; var C = b[j]; var D = b[i];
                    var centre = (A + Bv + C + D) * 0.25f;
                    var outward = centre - new Vector3(0f, centre.y, (bz0 + bz1 + tz0 + tz1) * 0.25f);
                    outward.y = 0f;
                    bool glazed = i == 0 ? glazeBack : i == 2 ? glazeFront : true;
                    if (!glazed) { m.Quad(paint, A, Bv, C, D, outward); continue; }
                    float pu = i % 2 == 0 ? 0.09f : 0.05f, pv = 0.07f;
                    Vector3 Q(float u, float w) => Vector3.Lerp(Vector3.Lerp(A, Bv, u), Vector3.Lerp(D, C, u), w);
                    float baseW = (Bv - A).magnitude;
                    float uu = Mathf.Clamp01(pu / Mathf.Max(0.1f, baseW) * 1.6f);
                    var gA = Q(uu, pv); var gB = Q(1f - uu, pv); var gC = Q(1f - uu, 1f - pv); var gD = Q(uu, 1f - pv);
                    m.Quad(paint, A, Bv, gB, gA, outward);
                    m.Quad(paint, Bv, C, gC, gB, outward);
                    m.Quad(paint, C, D, gD, gC, outward);
                    m.Quad(paint, D, A, gA, gD, outward);
                    var n = outward.normalized;
                    var inset = -Vector3.Cross(gB - gA, gD - gA).normalized * 0.012f;
                    if (Vector3.Dot(inset, n) > 0f) inset = -inset;
                    m.Quad(HillMat.Glass, gA + inset, gB + inset, gC + inset, gD + inset, outward);
                    if ((i == 1 || i == 3) && bPillarZ != 0f)
                    {
                        // The B pillar: a painted strip across the side glass.
                        float u0 = Mathf.InverseLerp(A.z, Bv.z, bPillarZ - 0.04f), u1 = Mathf.InverseLerp(A.z, Bv.z, bPillarZ + 0.04f);
                        var p0 = Q(u0, 0f); var p1 = Q(u1, 0f); var p2 = Q(u1, 1f); var p3 = Q(u0, 1f);
                        m.Quad(paint, p0 + n * 0.002f, p1 + n * 0.002f, p2 + n * 0.002f, p3 + n * 0.002f, outward);
                    }
                }
                m.Poly(paint, b, Vector3.up);
                // Inside: a dark floor at the belt, seats, the dash and the wheel.
                m.Quad(HillMat.Rubber, V(-bx + 0.02f, y0 + 0.006f, bz0 + 0.02f), V(bx - 0.02f, y0 + 0.006f, bz0 + 0.02f), V(bx - 0.02f, y0 + 0.006f, bz1 - 0.02f), V(-bx + 0.02f, y0 + 0.006f, bz1 - 0.02f), Vector3.up);
                string seat = Pick(r, new[] { HillMat.TrimDark, HillMat.Rubber, HillMat.Canvas, HillMat.DoorRed });
                float sy = y0 - 0.1f, front = bz1 - 0.55f;
                foreach (float x in new[] { -bx * 0.5f, bx * 0.5f })
                {
                    B(seat, x, sy + 0.12f, front, bx * 0.8f, 0.12f, 0.45f, 0.03f);
                    Bx(seat, V(x - bx * 0.38f, sy + 0.15f, front - 0.3f), V(x + bx * 0.38f, y1 - 0.2f, front - 0.2f), 0.03f);
                    B(seat, x, y1 - 0.15f, front - 0.25f, 0.24f, 0.13f, 0.09f, 0.03f);
                }
                if (bz1 - bz0 > 1.6f)
                {
                    float rear = front - 0.8f;
                    B(seat, 0f, sy + 0.12f, rear, bx * 1.8f, 0.12f, 0.45f, 0.03f);
                    Bx(seat, V(-bx * 0.9f, sy + 0.15f, rear - 0.3f), V(bx * 0.9f, y1 - 0.25f, rear - 0.2f), 0.03f);
                }
                Bx(HillMat.MetalDark, V(-bx + 0.03f, y0 - 0.05f, bz1 - 0.2f), V(bx - 0.03f, y0 + 0.08f, bz1 - 0.02f), 0.02f);
                Torus(m, HillMat.MetalDark, V(-bx * 0.5f, y0 + 0.12f, bz1 - 0.3f), new Vector3(0f, 0.5f, 1f), 0.17f, 0.015f, 12, 4);
            }

            void Bike()
            {
                string paint = Paint(v);
                float wr = Mathf.Min(0.34f, s.y * 0.34f), wz = s.z * 0.5f - wr - 0.01f, y = wr;
                foreach (float z in new[] { -wz, wz })
                {
                    var c = V(0f, y, z);
                    Torus(m, HillMat.Rubber, c, Vector3.right, wr - 0.022f, 0.022f, 18, 5);
                    Torus(m, HillMat.Steel, c, Vector3.right, wr - 0.05f, 0.01f, 18, 4);
                    Cyl(HillMat.Steel, c - V(0.04f, 0f, 0f), c + V(0.04f, 0f, 0f), 0.02f, 6);
                    for (int k = 0; k < 12; k++)
                    {
                        float a = k * Mathf.PI / 6f;
                        var e = c + new Vector3(0f, Mathf.Sin(a), Mathf.Cos(a)) * (wr - 0.055f);
                        Strip(m, HillMat.Steel, c + V(k % 2 == 0 ? 0.03f : -0.03f, 0f, 0f), e, 0.004f, Vector3.right);
                    }
                }
                var ra = V(0f, y, -wz); var fa = V(0f, y, wz);
                var bb = V(0f, y - 0.04f, -0.05f); var st = V(0f, y + 0.5f, -0.19f); var ht = V(0f, y + 0.53f, wz - 0.17f); var hbm = V(0f, y + 0.37f, wz - 0.12f);
                void T(Vector3 a, Vector3 b, float rr = 0.017f) => Cyl(paint, a, b, rr, 6, false);
                T(bb, st, 0.019f); T(st, ht); T(hbm, bb, 0.021f); T(hbm, ht, 0.022f);
                foreach (float x in new[] { -0.045f, 0.045f })
                {
                    T(bb + V(x * 0.5f, 0f, 0f), ra + V(x, 0f, 0f), 0.011f);
                    T(st + V(x * 0.3f, 0f, 0f), ra + V(x, 0f, 0f), 0.011f);
                    T(hbm + V(x * 0.4f, 0f, 0f), fa + V(x, 0f, 0f), 0.013f);
                }
                Cyl(HillMat.Steel, st, st + V(0f, 0.12f, -0.03f), 0.012f, 6, false);
                B(HillMat.Rubber, 0f, st.y + 0.15f, st.z - 0.02f, 0.13f, 0.05f, 0.25f, 0.02f);
                var stem = ht + V(0f, 0.1f, -0.04f);
                Cyl(HillMat.Steel, ht, stem, 0.012f, 6, false);
                float bar = Mathf.Min(0.24f, s.x * 0.48f);
                Cyl(HillMat.Steel, V(-bar, stem.y, stem.z), V(bar, stem.y, stem.z), 0.011f, 6, false);
                foreach (float x in new[] { -bar, bar }) Cyl(HillMat.Rubber, V(x * 0.7f, stem.y, stem.z), V(x, stem.y, stem.z), 0.016f, 6);
                Disc(m, HillMat.MetalDark, bb + V(0.05f, 0f, 0f), Vector3.right, 0.09f, 12);
                Disc(m, HillMat.MetalDark, bb + V(0.049f, 0f, 0f), Vector3.left, 0.09f, 12);
                m.Beam(HillMat.Steel, bb + V(0.06f, 0f, 0f), bb + V(0.06f, -0.14f, 0.06f), 0.012f, 0.02f);
                m.Beam(HillMat.Steel, bb - V(0.06f, 0f, 0f), bb + V(-0.06f, 0.14f, -0.06f), 0.012f, 0.02f);
                B(HillMat.Rubber, 0.09f, bb.y - 0.14f, bb.z + 0.06f, 0.08f, 0.02f, 0.1f);
                B(HillMat.Rubber, -0.09f, bb.y + 0.14f, bb.z - 0.06f, 0.08f, 0.02f, 0.1f);
                // A chain line and the kickstand.
                Strip(m, HillMat.MetalDark, bb + V(0.05f, 0.09f, 0f), ra + V(0.05f, 0.04f, 0f), 0.012f, Vector3.right);
                Strip(m, HillMat.MetalDark, bb + V(0.05f, -0.09f, 0f), ra + V(0.05f, -0.04f, 0f), 0.012f, Vector3.right);
                Cyl(HillMat.Steel, bb + V(-0.04f, 0f, -0.08f), V(-0.17f, 0.01f, -0.2f), 0.009f, 5, false);
                // A front basket on some, mudguards on others.
                if (v % 2 == 0)
                {
                    var bc = fa + V(0f, wr + 0.12f, -0.02f);
                    Bx(HillMat.MetalPaint, bc - V(0.17f, 0.11f, 0.13f), bc + V(0.17f, 0.11f, 0.13f), 0.01f, Side.YPos);
                    Bx(HillMat.MetalDark, bc - V(0.155f, 0.095f, 0.115f), bc + V(0.155f, 0.115f, 0.115f), 0f, Side.YNeg);
                }
                else
                    foreach (float z in new[] { -wz, wz })
                    {
                        var path = new Vector3[6];
                        for (int k = 0; k < 6; k++)
                        {
                            float a = Mathf.Lerp(0.3f, 2.6f, k / 5f);
                            path[k] = V(0f, y + Mathf.Sin(a) * (wr + 0.04f), z + Mathf.Cos(a) * (wr + 0.04f) * (z < 0f ? -1f : 1f));
                        }
                        m.Tube(paint, path, new[] { 0.025f }, 5, false, 1f);
                    }
            }

            void BikeRack()
            {
                int n = Mathf.Max(1, Mathf.RoundToInt(s.z / 0.75f));
                for (int i = 0; i < n; i++)
                {
                    float z = -s.z * 0.5f + (i + 0.5f) * s.z / n;
                    float hw = Mathf.Min(0.3f, s.z / n * 0.42f), h = s.y - 0.03f;
                    var path = new List<Vector3> { V(0f, -0.1f, z - hw), V(0f, h - hw, z - hw) };
                    for (int k = 1; k < 6; k++)
                    {
                        float a = Mathf.PI * k / 6f;
                        path.Add(V(0f, h - hw + Mathf.Sin(a) * hw, z - Mathf.Cos(a) * hw));
                    }
                    path.Add(V(0f, h - hw, z + hw));
                    path.Add(V(0f, -0.1f, z + hw));
                    m.Tube(HillMat.Steel, path, new[] { 0.025f }, 8, false, 1f);
                }
            }

            // ---------- Street furniture ----------

            void Bin()
            {
                float rr = Mathf.Min(s.x, s.z) * 0.45f, h = s.y;
                string body = v % 3 == 0 ? HillMat.DoorGreen : v % 3 == 1 ? HillMat.MetalDark : HillMat.Concrete;
                Lathe(body, 0f, 0f, new[] { 0f, 0.06f, 0.08f, h - 0.14f, h - 0.12f }, new[] { rr * 0.9f, rr * 0.9f, rr, rr, rr * 1.04f }, 12, false);
                for (int i = 0; i < 12; i++)
                {
                    float a = i * Mathf.PI / 6f;
                    var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    m.Beam(body, d * (rr + 0.004f) + V(0f, 0.12f, 0f), d * (rr + 0.004f) + V(0f, h - 0.2f, 0f), 0.03f, 0.03f);
                }
                Cyl(body == HillMat.Concrete ? HillMat.MetalDark : body, V(0f, h - 0.12f, 0f), V(0f, h - 0.05f, 0f), rr * 1.05f, 12);
                m.Ellipsoid(body == HillMat.Concrete ? HillMat.MetalDark : body, V(0f, h - 0.05f, 0f), new Vector3(rr * 0.95f, 0.05f, rr * 0.95f), 10);
                // The mouth: a dark slot on the side of the hood.
                Bx(HillMat.Rubber, V(-rr * 0.5f, h - 0.11f, rr * 0.9f), V(rr * 0.5f, h - 0.06f, rr * 1.06f));
            }

            void Wheelie()
            {
                string body = v % 3 == 0 ? HillMat.DoorGreen : v % 3 == 1 ? HillMat.DoorBlue : HillMat.TrimDark;
                float hx = s.x * 0.5f, hz = s.z * 0.5f, h = s.y;
                Loft(body, 0.08f, -hx + 0.06f, hx - 0.06f, -hz + 0.08f, hz - 0.07f, h - 0.08f, -hx + 0.02f, hx - 0.02f, -hz + 0.04f, hz - 0.02f, false, true);
                Bx(body, V(-hx, h - 0.09f, -hz + 0.02f), V(hx, h, hz), 0.012f);
                Bx(body, V(-hx + 0.04f, h - 0.17f, -hz + 0.01f), V(hx - 0.04f, h - 0.09f, -hz + 0.06f), 0.01f);
                Cyl(HillMat.MetalDark, V(-hx + 0.05f, h - 0.13f, -hz + 0.02f), V(hx - 0.05f, h - 0.13f, -hz + 0.02f), 0.018f, 6);
                foreach (int sx in new[] { -1, 1 })
                {
                    Cyl(HillMat.Rubber, V(sx * (hx - 0.06f), 0.1f, -hz + 0.11f), V(sx * (hx - 0.005f), 0.1f, -hz + 0.11f), 0.1f, 10);
                    Bx(body, V(sx * 0.1f - 0.025f, 0.15f, hz - 0.03f), V(sx * 0.1f + 0.025f, h - 0.15f, hz - 0.012f), 0.008f);
                }
                Cyl(HillMat.Steel, V(-hx + 0.06f, 0.1f, -hz + 0.11f), V(hx - 0.06f, 0.1f, -hz + 0.11f), 0.015f, 6, false);
                m.Push(WallFrame(V(-0.1f, 0f, hz - 0.045f), Vector3.right, Vector3.forward));
                Digits(m, HillMat.TrimWhite, (10 + ((v * 7) % 80)).ToString(), V(0.02f, h * 0.55f, 0f), 0.13f, 0.006f);
                m.Pop();
            }

            void Mailbox()
            {
                float h = s.y, top = h - 0.02f, bodyH = 0.24f, bw = Mathf.Min(0.22f, s.x * 0.55f), hz = s.z * 0.5f - 0.01f;
                string post = v % 2 == 0 ? HillMat.FenceWood : HillMat.FencePaint;
                Bx(post, V(-0.045f, -0.1f, -0.045f), V(0.045f, top - bodyH, 0.045f), 0.008f);
                Bx(post, V(-0.04f, top - bodyH - 0.06f, -hz + 0.05f), V(0.04f, top - bodyH, hz - 0.08f), 0.006f);
                m.Beam(post, V(0f, top - bodyH - 0.3f, -0.03f), V(0f, top - bodyH - 0.04f, -hz + 0.12f), 0.035f, 0.035f);
                string box = Pick(r, new[] { HillMat.MetalDark, HillMat.MetalPaint, HillMat.DoorBlue, HillMat.DoorRed, HillMat.DoorGreen });
                var outline = new List<Vector2> { new Vector2(-bw * 0.5f, top - bodyH), new Vector2(bw * 0.5f, top - bodyH), new Vector2(bw * 0.5f, top - bw * 0.5f) };
                for (int k = 1; k < 6; k++)
                {
                    float a = Mathf.PI * k / 6f;
                    outline.Add(new Vector2(Mathf.Cos(a) * bw * 0.5f, top - bw * 0.5f + Mathf.Sin(a) * bw * 0.5f));
                }
                outline.Add(new Vector2(-bw * 0.5f, top - bw * 0.5f));
                Extrude(m, box, outline, Vector3.right, Vector3.up, Vector3.forward, -hz + 0.02f, hz);
                Cyl(HillMat.Steel, V(0f, top - bodyH * 0.55f, hz), V(0f, top - bodyH * 0.55f, hz + 0.01f), 0.02f, 6);
                bool up = v % 3 != 0;
                Bx(HillMat.DoorRed, V(bw * 0.5f, top - bodyH + 0.02f, -0.05f), V(bw * 0.5f + 0.012f, top - bodyH + 0.04f, 0.1f), 0.003f);
                if (up) Bx(HillMat.DoorRed, V(bw * 0.5f, top - bodyH + 0.04f, -0.05f), V(bw * 0.5f + 0.012f, top + 0.0f, 0.02f), 0.003f);
                else Bx(HillMat.DoorRed, V(bw * 0.5f, top - bodyH + 0.02f, 0.1f), V(bw * 0.5f + 0.012f, top - bodyH + 0.1f, 0.18f), 0.003f);
                m.Push(WallFrame(V(-bw * 0.5f - 0.001f, 0f, hz - 0.08f), Vector3.back, Vector3.left));
                Digits(m, HillMat.TrimWhite, (2 + v % 90).ToString(), V(0.04f, top - bodyH + 0.06f, 0f), 0.08f, 0.004f);
                m.Pop();
            }

            void Hydrant()
            {
                string paint = v % 3 == 0 ? HillMat.DoorYellow : HillMat.DoorRed;
                float rr = Mathf.Min(s.x, s.z) * 0.32f, h = s.y;
                Lathe(paint, 0f, 0f, new[] { -0.05f, 0.06f, 0.08f, 0.12f, h * 0.76f, h * 0.8f }, new[] { rr * 1.4f, rr * 1.4f, rr * 1.15f, rr, rr, rr * 1.1f }, 12, false);
                m.Ellipsoid(paint, V(0f, h * 0.8f, 0f), new Vector3(rr * 1.05f, h * 0.12f, rr * 1.05f), 10);
                Cyl(HillMat.Steel, V(0f, h * 0.88f, 0f), V(0f, h * 0.98f, 0f), rr * 0.32f, 5);
                foreach (int sx in new[] { -1, 1 })
                {
                    Cyl(paint, V(sx * rr * 0.8f, h * 0.6f, 0f), V(sx * (Mathf.Min(s.x * 0.5f, rr * 1.6f) - 0.02f), h * 0.6f, 0f), rr * 0.36f, 8);
                    Cyl(HillMat.Steel, V(sx * (Mathf.Min(s.x * 0.5f, rr * 1.6f) - 0.02f), h * 0.6f, 0f), V(sx * Mathf.Min(s.x * 0.5f, rr * 1.6f), h * 0.6f, 0f), rr * 0.3f, 5);
                }
                Cyl(paint, V(0f, h * 0.5f, rr * 0.8f), V(0f, h * 0.5f, Mathf.Min(s.z * 0.5f, rr * 1.6f) - 0.02f), rr * 0.5f, 8);
                Cyl(HillMat.Steel, V(0f, h * 0.5f, Mathf.Min(s.z * 0.5f, rr * 1.6f) - 0.02f), V(0f, h * 0.5f, Mathf.Min(s.z * 0.5f, rr * 1.6f)), rr * 0.42f, 6);
            }

            void Streetlight()
            {
                float h = s.y;
                Lathe(HillMat.MetalDark, 0f, 0f, new[] { -0.1f, 0.35f, 0.4f, 0.55f, 0.6f }, new[] { 0.14f, 0.14f, 0.11f, 0.09f, 0.075f }, 10, false);
                m.Tube(HillMat.MetalDark, new[] { V(0f, 0.6f, 0f), V(0f, h * 0.5f, 0f), V(0f, h - 0.75f, 0f) }, new[] { 0.075f, 0.062f, 0.05f }, 10, false, 1f);
                foreach (float y in new[] { 1.2f, h - 0.85f }) Cyl(HillMat.MetalDark, V(0f, y, 0f), V(0f, y + 0.06f, 0f), 0.085f, 10);
                if (v % 2 == 0)
                {
                    // A post-top lantern.
                    float y0 = h - 0.75f;
                    Lathe(HillMat.MetalDark, 0f, 0f, new[] { y0, y0 + 0.1f, y0 + 0.15f }, new[] { 0.05f, 0.1f, 0.14f }, 8, false);
                    Cyl(HillMat.LampGlow, V(0f, y0 + 0.17f, 0f), V(0f, y0 + 0.45f, 0f), 0.07f, 8);
                    for (int k = 0; k < 6; k++)
                    {
                        float a0 = k * Mathf.PI / 3f, a1 = (k + 1) * Mathf.PI / 3f;
                        var p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                        var p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                        m.Quad(HillMat.Glass, p0 * 0.14f + V(0f, y0 + 0.15f, 0f), p1 * 0.14f + V(0f, y0 + 0.15f, 0f), p1 * 0.17f + V(0f, y0 + 0.5f, 0f), p0 * 0.17f + V(0f, y0 + 0.5f, 0f), (p0 + p1));
                        m.Beam(HillMat.MetalDark, p0 * 0.14f + V(0f, y0 + 0.15f, 0f), p0 * 0.17f + V(0f, y0 + 0.5f, 0f), 0.012f, 0.012f);
                    }
                    Lathe(HillMat.MetalDark, 0f, 0f, new[] { y0 + 0.5f, y0 + 0.53f, y0 + 0.66f, y0 + 0.72f }, new[] { 0.2f, 0.19f, 0.06f, 0.015f }, 8, true);
                }
                else
                {
                    // A swan-neck arm with a hanging lantern.
                    float y0 = h - 0.75f;
                    var arm = new[] { V(0f, y0, 0f), V(0f, y0 + 0.4f, 0f), V(0.12f, y0 + 0.66f, 0f), V(0.4f, y0 + 0.72f, 0f), V(0.62f, y0 + 0.64f, 0f) };
                    m.Tube(HillMat.MetalDark, arm, new[] { 0.045f, 0.04f, 0.035f, 0.03f, 0.025f }, 6, false, 1f);
                    float lx = 0.62f, ly = y0 + 0.6f;
                    Lathe(HillMat.MetalDark, lx, 0f, new[] { ly - 0.04f, ly, ly + 0.02f }, new[] { 0.22f, 0.21f, 0.04f }, 10, true);
                    Lathe(HillMat.Glass, lx, 0f, new[] { ly - 0.3f, ly - 0.04f }, new[] { 0.1f, 0.18f }, 10, false);
                    Cyl(HillMat.LampGlow, V(lx, ly - 0.25f, 0f), V(lx, ly - 0.06f, 0f), 0.06f, 8);
                    Cyl(HillMat.MetalDark, V(lx, ly - 0.33f, 0f), V(lx, ly - 0.29f, 0f), 0.1f, 8);
                }
            }

            void PowerPole()
            {
                float h = s.y;
                m.Tube(HillMat.FenceWood, new[] { V(0f, -0.4f, 0f), V(0f, h * 0.5f, 0f), V(0f, h, 0f) }, new[] { 0.16f, 0.14f, 0.11f }, 9, true, 2f);
                float ay = h - 0.6f;
                Bx(HillMat.FenceWood, V(-1.25f, ay - 0.05f, -0.06f), V(1.25f, ay + 0.06f, 0.06f), 0.012f);
                foreach (int sx in new[] { -1, 1 })
                    m.Beam(HillMat.MetalDark, V(0f, ay - 0.55f, 0.08f), V(sx * 0.65f, ay - 0.05f, 0.06f), 0.02f, 0.04f);
                foreach (float x in new[] { -1.05f, -0.45f, 0.45f, 1.05f })
                {
                    Cyl(HillMat.Steel, V(x, ay + 0.06f, 0f), V(x, ay + 0.1f, 0f), 0.015f, 5, false);
                    Lathe(HillMat.TrimWhite, x, 0f, new[] { ay + 0.08f, ay + 0.1f, ay + 0.13f, ay + 0.15f, ay + 0.18f, ay + 0.2f, ay + 0.22f }, new[] { 0.03f, 0.055f, 0.03f, 0.05f, 0.028f, 0.04f, 0.02f }, 8, true);
                }
                // Climbing steps and, on some, a transformer can.
                for (float y = 2.6f; y < h - 1.2f; y += 0.45f)
                {
                    float a = ((int)(y / 0.45f) % 2 == 0) ? 0f : Mathf.PI;
                    var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Cyl(HillMat.Steel, d * 0.1f + V(0f, y, 0f), d * 0.32f + V(0f, y, 0f), 0.012f, 4, false);
                }
                if (v % 3 == 0)
                {
                    float ty = h - 2.4f;
                    Bx(HillMat.MetalDark, V(-0.06f, ty + 0.2f, 0.13f), V(0.06f, ty + 0.9f, 0.2f));
                    Lathe(HillMat.MetalPaint, 0f, 0.42f, new[] { ty, ty + 0.75f, ty + 0.8f }, new[] { 0.24f, 0.24f, 0.1f }, 12, true);
                    m.Ellipsoid(HillMat.MetalPaint, V(0f, ty, 0.42f), new Vector3(0.24f, 0.04f, 0.24f), 10);
                }
            }

            void Bench()
            {
                float hx = s.x * 0.5f, hz = s.z * 0.5f;
                string iron = HillMat.MetalDark, wood = v % 2 == 0 ? HillMat.FenceWood : HillMat.Deck;
                foreach (float x in new[] { -hx + 0.06f, hx - 0.06f })
                {
                    m.Beam(iron, V(x, 0f, hz - 0.06f), V(x, 0.44f, hz - 0.1f), 0.05f, 0.05f, 0.008f);
                    m.Beam(iron, V(x, 0f, -hz + 0.07f), V(x, s.y - 0.02f, -hz + 0.02f), 0.05f, 0.05f, 0.008f);
                    m.Beam(iron, V(x, 0.42f, hz - 0.06f), V(x, 0.42f, -hz + 0.06f), 0.045f, 0.045f);
                    m.Beam(iron, V(x, 0.65f, hz - 0.02f), V(x, 0.65f, -hz + 0.1f), 0.05f, 0.04f, 0.008f);
                    m.Beam(iron, V(x, 0.44f, hz - 0.08f), V(x, 0.65f, hz - 0.06f), 0.035f, 0.035f);
                    B(iron, x, 0.02f, hz - 0.06f, 0.08f, 0.04f, 0.1f);
                    B(iron, x, 0.02f, -hz + 0.07f, 0.08f, 0.04f, 0.1f);
                }
                for (int i = 0; i < 5; i++)
                {
                    float z = Mathf.Lerp(hz - 0.07f, -hz + 0.15f, i / 4f);
                    B(wood, 0f, 0.455f, z, s.x - 0.04f, 0.035f, 0.07f, 0.008f);
                }
                for (int i = 0; i < 3; i++)
                {
                    float y = 0.56f + i * 0.1f;
                    float z = -hz + 0.07f - (y - 0.44f) / (s.y - 0.44f) * 0.05f;
                    Bx(wood, V(-hx + 0.03f, y - 0.04f, z - 0.015f), V(hx - 0.03f, y + 0.04f, z + 0.015f), 0.008f);
                }
                if (v % 3 == 0) B(HillMat.Steel, 0f, 0.66f, -hz + 0.07f - 0.04f + 0.02f, 0.18f, 0.05f, 0.006f, 0.002f);
            }

            void Planter()
            {
                float hx = s.x * 0.5f, hz = s.z * 0.5f, h = s.y;
                bool timber = v % 2 == 0;
                string mat = timber ? HillMat.FenceWood : HillMat.Concrete;
                float t = timber ? 0.05f : 0.07f;
                Bx(mat, V(-hx, 0f, -hz), V(hx, h, -hz + t), 0.01f);
                Bx(mat, V(-hx, 0f, hz - t), V(hx, h, hz), 0.01f);
                Bx(mat, V(-hx, 0f, -hz + t), V(-hx + t, h, hz - t), 0.01f);
                Bx(mat, V(hx - t, 0f, -hz + t), V(hx, h, hz - t), 0.01f);
                if (timber)
                    foreach (float y in new[] { h * 0.33f, h * 0.66f })
                    {
                        Bx(HillMat.FenceWood, V(-hx - 0.004f, y - 0.004f, -hz - 0.004f), V(hx + 0.004f, y + 0.004f, -hz + 0.01f));
                        Bx(HillMat.FenceWood, V(-hx - 0.004f, y - 0.004f, hz - 0.01f), V(hx + 0.004f, y + 0.004f, hz + 0.004f));
                    }
                m.Quad(HillMat.Dirt, V(-hx + t, h - 0.06f, -hz + t), V(hx - t, h - 0.06f, -hz + t), V(hx - t, h - 0.06f, hz - t), V(-hx + t, h - 0.06f, hz - t), Vector3.up);
                HillTrees.DrawFlowers(m, V(0f, h - 0.06f, 0f), new Vector2(s.x - 2f * t - 0.06f, s.z - 2f * t - 0.06f), 26f, v * 17 + 3);
                if (s.x > 0.9f) HillTrees.DrawBush(m, V(-hx * 0.4f, h - 0.08f, 0f), new Vector3(0.45f, 0.4f, 0.4f), v * 3 + 1);
            }

            void BusShelter()
            {
                float hx = s.x * 0.5f, hz = s.z * 0.5f, h = s.y, roofT = 0.15f;
                string frame = v % 2 == 0 ? HillMat.Steel : HillMat.MetalDark;
                var posts = new[] { V(-hx + 0.05f, 0f, -hz + 0.05f), V(hx - 0.05f, 0f, -hz + 0.05f), V(-hx + 0.05f, 0f, hz - 0.12f), V(hx - 0.05f, 0f, hz - 0.12f), V(-hx * 0.33f, 0f, -hz + 0.05f), V(hx * 0.33f, 0f, -hz + 0.05f) };
                foreach (var p in posts) Bx(frame, p - V(0.04f, 0f, 0.04f), p + V(0.04f, h - roofT, 0.04f), 0.006f);
                // The roof: walkable top at the box's top, fascia with the route sign, a lit strip under it.
                Bx(HillMat.MetalPaint, V(-hx, h - roofT, -hz), V(hx, h, hz), 0f, Side.YPos);
                m.Quad(HillMat.Corrugated, V(-hx, h, -hz), V(hx, h, -hz), V(hx, h, hz), V(-hx, h, hz), Vector3.up);
                Panel(m, HillMat.SignBoard, V(0f, h - roofT * 0.5f - 0.005f, hz + 0.004f), Vector3.right * Mathf.Min(1.2f, s.x * 0.4f), Vector3.up * Mathf.Min(0.14f, roofT - 0.01f), Vector3.forward, HillAtlas.Sign(HillAtlas.SignBusStop));
                Bx(HillMat.LampGlow, V(-hx + 0.2f, h - roofT - 0.015f, -hz + 0.3f), V(hx - 0.2f, h - roofT, -hz + 0.42f));
                // Glass: the back in three panes, one end glazed, the other an advert.
                for (int i = 0; i < 3; i++)
                {
                    float x0 = Mathf.Lerp(-hx + 0.09f, hx - 0.09f, i / 3f), x1 = Mathf.Lerp(-hx + 0.09f, hx - 0.09f, (i + 1) / 3f);
                    GlassPane(V(x0 + 0.03f, 0.12f, -hz + 0.05f), V(x1 - 0.03f, h - roofT - 0.08f, -hz + 0.05f), Vector3.forward);
                    Bx(frame, V(x0, 0.08f, -hz + 0.03f), V(x1, 0.12f, -hz + 0.07f));
                    Bx(frame, V(x0, h - roofT - 0.08f, -hz + 0.03f), V(x1, h - roofT - 0.04f, -hz + 0.07f));
                }
                GlassPane(V(-hx + 0.05f, 0.12f, -hz + 0.12f), V(-hx + 0.05f, h - roofT - 0.08f, hz * 0.4f), Vector3.right);
                Bx(frame, V(-hx + 0.02f, 0.08f, -hz + 0.09f), V(-hx + 0.08f, 0.12f, hz * 0.4f));
                Bx(HillMat.MetalDark, V(hx - 0.12f, 0.35f, -hz + 0.12f), V(hx - 0.01f, h - roofT - 0.2f, hz * 0.45f), 0.01f);
                float ay0 = 0.45f, ay1 = h - roofT - 0.3f, az0 = -hz + 0.18f, az1 = hz * 0.45f - 0.06f;
                var ad = HillAtlas.ShopInterior(((v % 8) + 8) % 8);
                Panel(m, HillMat.InteriorShop, V(hx - 0.005f, (ay0 + ay1) * 0.5f, (az0 + az1) * 0.5f), Vector3.forward * (az1 - az0), Vector3.up * (ay1 - ay0), Vector3.right, ad);
                Panel(m, HillMat.InteriorShop, V(hx - 0.125f, (ay0 + ay1) * 0.5f, (az0 + az1) * 0.5f), Vector3.back * (az1 - az0), Vector3.up * (ay1 - ay0), Vector3.left, HillAtlas.ShopInterior((v + 3) % 8));
                // The perch bench.
                Bx(frame, V(-hx * 0.6f, 0.46f, -hz + 0.1f), V(hx * 0.6f, 0.5f, -hz + 0.38f), 0.01f);
                foreach (float x in new[] { -hx * 0.5f, 0f, hx * 0.5f }) Bx(frame, V(x - 0.02f, 0f, -hz + 0.2f), V(x + 0.02f, 0.46f, -hz + 0.26f));
                // A timetable case.
                Bx(HillMat.TrimWhite, V(-hx * 0.33f - 0.25f, 1.1f, -hz + 0.09f), V(-hx * 0.33f + 0.25f, 1.7f, -hz + 0.12f), 0.006f);
                m.Quad(HillMat.Glass, V(-hx * 0.33f - 0.23f, 1.12f, -hz + 0.126f), V(-hx * 0.33f + 0.23f, 1.12f, -hz + 0.126f), V(-hx * 0.33f + 0.23f, 1.68f, -hz + 0.126f), V(-hx * 0.33f - 0.23f, 1.68f, -hz + 0.126f), Vector3.forward);
            }

            /// <summary>A two-sided glass pane between two corners (a vertical rectangle facing ±<paramref name="facing"/>).</summary>
            void GlassPane(Vector3 a, Vector3 c, Vector3 facing)
            {
                Vector3 b, d;
                if (Mathf.Abs(facing.z) > 0.5f) { b = V(c.x, a.y, a.z); d = V(a.x, c.y, a.z); }
                else { b = V(a.x, a.y, c.z); d = V(a.x, c.y, a.z); }
                m.Quad(HillMat.Glass, a, b, c, d, facing);
                m.Quad(HillMat.Glass, a, b, c, d, -facing);
            }

            void BusStopSign()
            {
                float h = s.y, hz = s.z * 0.5f;
                Cyl(HillMat.MetalPaint, V(0f, -0.2f, 0f), V(0f, h - 0.04f, 0f), 0.035f, 8);
                Cyl(HillMat.MetalPaint, V(0f, h - 0.04f, 0f), V(0f, h, 0f), 0.045f, 8);
                float y0 = h - 0.72f, y1 = h - 0.08f;
                Bx(HillMat.DoorRed, V(-0.008f, y0, -hz + 0.01f), V(0.008f, y1, hz - 0.01f), 0.006f);
                foreach (int side in new[] { -1, 1 })
                {
                    // Layers 6 mm apart: the white field, the route strip and the bus pictogram on it.
                    var facing = new Vector3(side, 0f, 0f);
                    var right = new Vector3(0f, 0f, -side);
                    m.Rect(HillMat.TrimWhite, V(side * 0.014f, (y0 + y1) * 0.5f + 0.06f, 0f), Vector3.forward * (s.z - 0.08f), Vector3.up * 0.36f, facing);
                    Panel(m, HillMat.SignBoard, V(side * 0.02f, y0 + 0.09f, 0f), right * (s.z - 0.06f), Vector3.up * (s.z - 0.06f) / 8f, facing, HillAtlas.Sign(HillAtlas.SignBusStop));
                    float cy = (y0 + y1) * 0.5f + 0.08f;
                    Bx(HillMat.DoorRed, V(side * 0.014f, cy - 0.07f, -0.15f), V(side * 0.02f, cy + 0.07f, 0.15f));
                    for (int k = 0; k < 4; k++)
                        Bx(HillMat.TrimWhite, V(side * 0.02f, cy, -0.12f + k * 0.065f), V(side * 0.026f, cy + 0.045f, -0.08f + k * 0.065f));
                    foreach (float wz in new[] { -0.09f, 0.09f })
                        Disc(m, HillMat.MetalDark, V(side * 0.026f, cy - 0.07f, wz), facing, 0.03f, 8);
                }
                Bx(HillMat.TrimWhite, V(-0.05f, 1.35f, -0.12f), V(-0.035f, 1.8f, 0.12f), 0.004f);
                m.Quad(HillMat.Glass, V(-0.052f, 1.37f, -0.11f), V(-0.052f, 1.78f, -0.11f), V(-0.052f, 1.78f, 0.11f), V(-0.052f, 1.37f, 0.11f), Vector3.left);
            }

            void PhoneBox()
            {
                float hx = s.x * 0.5f, hz = s.z * 0.5f, h = s.y;
                string red = HillMat.DoorRed;
                Bx(HillMat.Concrete, V(-hx, -0.05f, -hz), V(hx, 0.1f, hz), 0.015f);
                float body = h - 0.45f;
                foreach (int sx in new[] { -1, 1 })
                    foreach (int sz in new[] { -1, 1 })
                        Bx(red, V(sx * (hx - 0.03f) - 0.07f, 0.1f, sz * (hz - 0.03f) - 0.07f), V(sx * (hx - 0.03f) + 0.07f, body, sz * (hz - 0.03f) + 0.07f), 0.01f);
                // Three glazed sides and the door; the back glazed too.
                for (int f = 0; f < 4; f++)
                {
                    float yaw = f * 90f;
                    m.Push(V(0f, 0f, 0f), yaw);
                    float z = hz - 0.06f;
                    Bx(red, V(-hx + 0.08f, 0.1f, z - 0.03f), V(hx - 0.08f, 0.85f, z + 0.03f), 0.008f);
                    Bx(red, V(-hx + 0.08f, body - 0.12f, z - 0.03f), V(hx - 0.08f, body, z + 0.03f), 0.008f);
                    float gy0 = 0.85f, gy1 = body - 0.12f;
                    m.Quad(HillMat.Glass, V(-hx + 0.1f, gy0, z), V(hx - 0.1f, gy0, z), V(hx - 0.1f, gy1, z), V(-hx + 0.1f, gy1, z), Vector3.forward);
                    m.Quad(HillMat.Glass, V(-hx + 0.1f, gy0, z - 0.004f), V(hx - 0.1f, gy0, z - 0.004f), V(hx - 0.1f, gy1, z - 0.004f), V(-hx + 0.1f, gy1, z - 0.004f), Vector3.back);
                    for (int c = 1; c < 3; c++)
                    {
                        float x = Mathf.Lerp(-hx + 0.1f, hx - 0.1f, c / 3f);
                        Bx(red, V(x - 0.015f, gy0, z - 0.02f), V(x + 0.015f, gy1, z + 0.02f));
                    }
                    for (int k = 1; k < 6; k++)
                    {
                        float y = Mathf.Lerp(gy0, gy1, k / 6f);
                        Bx(red, V(-hx + 0.1f, y - 0.015f, z - 0.02f), V(hx - 0.1f, y + 0.015f, z + 0.02f));
                    }
                    // The lit header panel.
                    Bx(HillMat.LampGlow, V(-hx + 0.15f, body + 0.04f, hz - 0.02f), V(hx - 0.15f, body + 0.16f, hz - 0.005f));
                    if (f == 0) Bx(HillMat.Steel, V(hx - 0.2f, 1.0f, hz - 0.02f), V(hx - 0.14f, 1.25f, hz + 0.01f), 0.01f);
                    m.Pop();
                }
                Bx(red, V(-hx + 0.02f, body, -hz + 0.02f), V(hx - 0.02f, body + 0.22f, hz - 0.02f), 0.015f);
                Bx(red, V(-hx, body + 0.22f, -hz), V(hx, body + 0.28f, hz), 0.02f);
                m.Ellipsoid(red, V(0f, body + 0.28f, 0f), new Vector3(hx * 0.92f, 0.13f, hz * 0.92f), 10);
                B(red, 0f, h - 0.04f, 0f, 0.12f, 0.08f, 0.12f, 0.02f);
                B(HillMat.MetalDark, 0f, 1.25f, -hz + 0.15f, 0.28f, 0.36f, 0.14f, 0.02f);
            }

            void Fountain()
            {
                float a = Mathf.Min(s.x, s.z) * 0.5f, h = s.y, wallTop = Mathf.Min(0.55f, h * 0.4f);
                string stone = v % 2 == 0 ? HillMat.StoneCoping : HillMat.Concrete;
                var outer = Oct(a);
                m.Prism(stone, outer, -0.1f, wallTop - 0.08f, false);
                var inner = Oct(a - 0.16f);
                // The coping ring, the inner faces, the water.
                for (int i = 0; i < 8; i++)
                {
                    var o0 = Oct(a + 0.02f)[i]; var o1 = Oct(a + 0.02f)[(i + 1) % 8];
                    var i0 = inner[i]; var i1 = inner[(i + 1) % 8];
                    m.Quad(stone, V(o0.x, wallTop, o0.y), V(o1.x, wallTop, o1.y), V(i1.x, wallTop, i1.y), V(i0.x, wallTop, i0.y), Vector3.up);
                    m.Quad(stone, V(o0.x, wallTop - 0.08f, o0.y), V(o1.x, wallTop - 0.08f, o1.y), V(o1.x, wallTop, o1.y), V(o0.x, wallTop, o0.y), V(o0.x + o1.x, 0f, o0.y + o1.y));
                    var mid = (i0 + i1) * 0.5f;
                    m.Quad(stone, V(i0.x, 0.1f, i0.y), V(i1.x, 0.1f, i1.y), V(i1.x, wallTop, i1.y), V(i0.x, wallTop, i0.y), V(-mid.x, 0f, -mid.y));
                }
                var floor = new Vector3[8];
                var water = new Vector3[8];
                for (int i = 0; i < 8; i++) { floor[i] = V(inner[i].x, 0.1f, inner[i].y); water[i] = V(inner[i].x, wallTop - 0.1f, inner[i].y); }
                m.Poly(HillMat.CreekBed, floor, Vector3.up);
                m.Poly(HillMat.Water, water, Vector3.up);
                // The column, its bowl and the spout.
                Lathe(stone, 0f, 0f, new[] { 0.1f, 0.3f, h * 0.55f, h * 0.62f }, new[] { 0.2f, 0.13f, 0.11f, 0.13f }, 10, false);
                float by = h * 0.62f, bowlR = Mathf.Min(0.5f, a * 0.42f);
                m.Tube(stone, new[] { V(0f, by, 0f), V(0f, by + 0.08f, 0f), V(0f, by + 0.15f, 0f), V(0f, by + 0.17f, 0f) }, new[] { 0.13f, bowlR * 0.75f, bowlR, bowlR * 1.02f }, 12, false, 1f);
                Disc(m, HillMat.Water, V(0f, by + 0.14f, 0f), Vector3.up, bowlR * 0.94f, 12);
                Lathe(stone, 0f, 0f, new[] { by + 0.1f, by + 0.3f, by + 0.34f, Mathf.Min(h - 0.06f, by + 0.5f) }, new[] { 0.06f, 0.05f, 0.08f, 0.02f }, 8, true);
                m.Ellipsoid(HillMat.Water, V(0f, Mathf.Min(h - 0.08f, by + 0.52f), 0f), new Vector3(0.05f, 0.06f, 0.05f), 6);
            }

            static List<Vector2> Oct(float apothem)
            {
                float rv = apothem / Mathf.Cos(22.5f * Mathf.Deg2Rad);
                var l = new List<Vector2>();
                for (int i = 0; i < 8; i++)
                {
                    float a = (i * 45f + 22.5f) * Mathf.Deg2Rad;
                    l.Add(new Vector2(Mathf.Cos(a) * rv, Mathf.Sin(a) * rv));
                }
                return l;
            }

            // ---------- Play ----------

            void Swing()
            {
                float hx = s.x * 0.5f, hz = s.z * 0.5f, h = s.y;
                string paint = Paint(v + 1);
                foreach (int sx in new[] { -1, 1 })
                {
                    float x = sx * (hx - 0.06f);
                    foreach (int sz in new[] { -1, 1 })
                        Cyl(paint, V(x, -0.15f, sz * (hz - 0.05f)), V(x, h - 0.05f, 0f), 0.04f, 8, false);
                    Cyl(paint, V(x, 0.7f, -(hz - 0.05f) * 0.69f), V(x, 0.7f, (hz - 0.05f) * 0.69f), 0.025f, 6, false);
                }
                Cyl(paint, V(-hx, h - 0.05f, 0f), V(hx, h - 0.05f, 0f), 0.045f, 10, true);
                int seats = s.x > 2.4f ? 2 : 1;
                for (int i = 0; i < seats; i++)
                {
                    float x = seats == 1 ? 0f : (i == 0 ? -hx * 0.38f : hx * 0.38f);
                    float sy = 0.42f + (i == 1 ? 0.0f : 0.03f);
                    foreach (float dx in new[] { -0.2f, 0.2f })
                        Cyl(HillMat.Steel, V(x + dx, h - 0.08f, 0f), V(x + dx, sy + 0.02f, 0f), 0.008f, 4, false);
                    B(HillMat.Rubber, x, sy, 0f, 0.46f, 0.03f, 0.17f, 0.01f);
                }
            }

            void Slide()
            {
                float hx = s.x * 0.5f, hz = s.z * 0.5f, h = s.y;
                string paint = Paint(v + 2);
                float py = Mathf.Min(1.5f, h - 0.55f), pz0 = -hz + 0.35f, pz1 = -hz + 0.85f;
                // Ladder.
                foreach (int sx in new[] { -1, 1 })
                    Cyl(paint, V(sx * (hx - 0.06f), -0.1f, -hz + 0.05f), V(sx * (hx - 0.06f), h - 0.05f, pz0), 0.03f, 8, false);
                for (int k = 1; k < 6; k++)
                {
                    float t = k / 6f;
                    var p = Vector3.Lerp(V(0f, 0f, -hz + 0.05f), V(0f, py, pz0), t);
                    Cyl(HillMat.Steel, V(-hx + 0.06f, p.y, p.z), V(hx - 0.06f, p.y, p.z), 0.016f, 6, false);
                }
                // Platform and its guard hoops, the back legs.
                Bx(HillMat.Deck, V(-hx + 0.05f, py - 0.05f, pz0), V(hx - 0.05f, py, pz1), 0.01f);
                foreach (int sx in new[] { -1, 1 })
                {
                    Cyl(paint, V(sx * (hx - 0.06f), -0.1f, pz1), V(sx * (hx - 0.06f), h - 0.05f, pz1), 0.03f, 8, false);
                    Cyl(paint, V(sx * (hx - 0.06f), h - 0.12f, pz0), V(sx * (hx - 0.06f), h - 0.12f, pz1), 0.022f, 6, false);
                    Cyl(paint, V(sx * (hx - 0.06f), py + 0.3f, pz0), V(sx * (hx - 0.06f), py + 0.3f, pz1), 0.018f, 6, false);
                }
                // The chute: a bed and two sides down to a run-out.
                var top = V(0f, py, pz1);
                var low = V(0f, 0.35f, hz - 0.45f);
                var end = V(0f, 0.3f, hz - 0.03f);
                float cw = s.x - 0.24f;
                m.Beam(HillMat.Steel, top, low, cw, 0.03f);
                m.Beam(HillMat.Steel, low, end, cw, 0.03f);
                foreach (int sx in new[] { -1, 1 })
                {
                    var off = V(sx * (cw * 0.5f + 0.02f), 0.08f, 0f);
                    m.Beam(paint, top + off, low + off, 0.035f, 0.2f, 0.006f);
                    m.Beam(paint, low + off, end + off, 0.035f, 0.2f, 0.006f);
                    Cyl(paint, V(sx * (hx - 0.1f), -0.1f, hz - 0.3f), V(sx * (hx - 0.1f), 0.33f, hz - 0.3f), 0.025f, 6, false);
                }
            }

            void Trampoline()
            {
                float R0 = Mathf.Min(s.x, s.z) * 0.5f - 0.05f, h = s.y - 0.12f;
                Torus(m, HillMat.Steel, V(0f, h, 0f), Vector3.up, R0, 0.03f, 24, 5);
                string pad = Paint(v + 1);
                int seg = 24;
                for (int i = 0; i < seg; i++)
                {
                    float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                    var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)); var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                    float ri = R0 * 0.8f, ro = R0 + 0.04f;
                    m.Quad(pad, d0 * ri + V(0f, h + 0.05f, 0f), d1 * ri + V(0f, h + 0.05f, 0f), d1 * ro + V(0f, h + 0.05f, 0f), d0 * ro + V(0f, h + 0.05f, 0f), Vector3.up);
                    m.Quad(pad, d0 * ro + V(0f, h + 0.05f, 0f), d1 * ro + V(0f, h + 0.05f, 0f), d1 * ro + V(0f, h - 0.02f, 0f), d0 * ro + V(0f, h - 0.02f, 0f), d0 + d1);
                    m.Quad(HillMat.Rubber, d0 * ri + V(0f, h + 0.05f, 0f), d1 * ri + V(0f, h + 0.05f, 0f), d1 * ri + V(0f, h + 0.02f, 0f), d0 * ri + V(0f, h + 0.02f, 0f), -(d0 + d1));
                }
                Disc(m, HillMat.Rubber, V(0f, h + 0.02f, 0f), Vector3.up, R0 * 0.8f, 24);
                Disc(m, HillMat.Rubber, V(0f, h + 0.015f, 0f), Vector3.down, R0 * 0.8f, 24);
                for (int i = 0; i < 6; i++)
                {
                    float a = i * Mathf.PI / 3f;
                    var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    var t = new Vector3(-d.z, 0f, d.x);
                    var top = d * R0 + V(0f, h, 0f);
                    Cyl(HillMat.Steel, top + t * 0.12f, d * R0 + t * 0.2f + V(0f, -0.05f, 0f), 0.022f, 6, false);
                    Cyl(HillMat.Steel, top - t * 0.12f, d * R0 - t * 0.2f + V(0f, -0.05f, 0f), 0.022f, 6, false);
                }
            }

            void Sandbox()
            {
                float hx = s.x * 0.5f, hz = s.z * 0.5f, h = s.y, t = 0.12f;
                Bx(HillMat.FenceWood, V(-hx, -0.05f, -hz), V(hx, h, -hz + t), 0.01f);
                Bx(HillMat.FenceWood, V(-hx, -0.05f, hz - t), V(hx, h, hz), 0.01f);
                Bx(HillMat.FenceWood, V(-hx, -0.05f, -hz + t), V(-hx + t, h, hz - t), 0.01f);
                Bx(HillMat.FenceWood, V(hx - t, -0.05f, -hz + t), V(hx, h, hz - t), 0.01f);
                foreach (int sx in new[] { -1, 1 })
                    foreach (int sz in new[] { -1, 1 })
                        m.Poly(HillMat.FenceWood, new[] { V(sx * hx, h + 0.008f, sz * hz), V(sx * (hx - 0.4f), h + 0.008f, sz * hz), V(sx * hx, h + 0.008f, sz * (hz - 0.4f)) }, Vector3.up);
                m.Quad(HillMat.Gravel, V(-hx + t, h - 0.1f, -hz + t), V(hx - t, h - 0.1f, -hz + t), V(hx - t, h - 0.1f, hz - t), V(-hx + t, h - 0.1f, hz - t), Vector3.up);
                m.Ellipsoid(HillMat.Gravel, V(hx * 0.3f, h - 0.12f, -hz * 0.2f), new Vector3(0.35f, 0.08f, 0.3f), 8);
                Lathe(Paint(v), -hx * 0.4f, hz * 0.3f, new[] { h - 0.1f, h + 0.06f }, new[] { 0.07f, 0.09f }, 8, false);
                m.Beam(HillMat.DoorYellow, V(-hx * 0.1f, h - 0.08f, hz * 0.4f), V(hx * 0.2f, h - 0.02f, hz * 0.55f), 0.06f, 0.012f);
            }

            // ---------- Garden and yard ----------

            void Shed()
            {
                float hx = s.x * 0.5f, hz = s.z * 0.5f, h = s.y, roofT = 0.14f, ov = 0.07f;
                string wall = Pick(r, new[] { HillMat.FenceWood, HillMat.SidingSage, HillMat.SidingButter, HillMat.SidingBlue, HillMat.FenceWood });
                string trim = Chance(r, 0.6f) ? HillMat.TrimWhite : HillMat.TrimCream;
                float wx = hx - ov, wz = hz - ov, top = h - roofT;
                // Walls as faces with the door and a window cut out.
                var faces = new[]
                {
                    (V(-wx, 0f, wz), Vector3.right, Vector3.forward, wx * 2f),
                    (V(wx, 0f, wz), Vector3.back, Vector3.right, wz * 2f),
                    (V(wx, 0f, -wz), Vector3.left, Vector3.back, wx * 2f),
                    (V(-wx, 0f, -wz), Vector3.forward, Vector3.left, wz * 2f),
                };
                for (int f = 0; f < 4; f++)
                {
                    var (o, along, outward, len) = faces[f];
                    m.Push(WallFrame(o, along, outward));
                    var outline = new List<Vector2> { new Vector2(0f, 0.08f), new Vector2(len, 0.08f), new Vector2(len, top), new Vector2(0f, top) };
                    var holes = new List<Rect>();
                    DoorSpec door = null;
                    Win win = null;
                    if (f == 0)
                    {
                        float dw = 0.8f;
                        door = new DoorSpec { x0 = len * 0.5f - dw * 0.5f + 0.25f, x1 = len * 0.5f + dw * 0.5f + 0.25f, height = Mathf.Min(1.9f, top - 0.2f), depth = 0.05f, style = 0, casing = 0, wall = wall, reveal = trim, trim = trim, leaf = Pick(r, new[] { HillMat.DoorGreen, HillMat.DoorBlue, HillMat.DoorRed, HillMat.FenceWood }), hallUV = HillAtlas.Interior(7) };
                        holes.Add(new Rect(door.Hole.x, 0.08f, door.Hole.width, door.Hole.height));
                    }
                    if (f == 1)
                    {
                        win = new Win { x0 = len * 0.5f - 0.35f, x1 = len * 0.5f + 0.35f, y0 = 1.0f, y1 = 1.55f, depth = 0.05f, muntins = 5, casing = 0, wall = wall, reveal = trim, trim = trim, frame = trim, roomUV = HillAtlas.Interior(7), roomDepth = 0.6f, headCap = false, flowerBox = Chance(r, 0.4f) };
                        holes.Add(win.Hole);
                    }
                    WallFace(m, wall, outline, holes);
                    if (door != null) m.Push(V(0f, 0.08f, 0f), Quaternion.identity);
                    if (door != null) { Door(m, door, r); m.Pop(); }
                    if (win != null) Window(m, win, r);
                    Bx(trim, V(-0.025f, 0.08f, 0f), V(0.08f, top, 0.025f), 0f, Side.ZNeg);
                    Bx(trim, V(len - 0.08f, 0.08f, 0f), V(len, top, 0.025f), 0f, Side.ZNeg);
                    m.Pop();
                }
                Bx(HillMat.FenceWood, V(-wx - 0.02f, -0.1f, -wz - 0.02f), V(wx + 0.02f, 0.08f, wz + 0.02f), 0.01f);
                // The roof slab: felt on top at the box's top, fascia round it.
                Bx(trim, V(-hx, top, -hz), V(hx, h, hz), 0f, Side.YPos);
                m.Quad(Chance(r, 0.5f) ? HillMat.RoofGreen : HillMat.RoofGrey, V(-hx, h, -hz), V(hx, h, -hz), V(hx, h, hz), V(-hx, h, hz), Vector3.up);
                Bx(HillMat.Gutter, V(-hx, h - 0.03f, hz - 0.02f), V(hx, h + 0.006f, hz), 0f, Side.YNeg);
            }

            void Greenhouse()
            {
                float hx = s.x * 0.5f, hz = s.z * 0.5f, h = s.y, eave = h * 0.68f;
                string frame = HillMat.Steel;
                Bx(HillMat.BrickRed, V(-hx, -0.1f, -hz), V(hx, 0.35f, hz), 0.01f, Side.YPos);
                Bx(HillMat.StoneCoping, V(-hx, 0.35f, -hz), V(hx, 0.39f, hz), 0.01f);
                var c = new[] { V(-hx + 0.03f, 0f, -hz + 0.03f), V(hx - 0.03f, 0f, -hz + 0.03f), V(hx - 0.03f, 0f, hz - 0.03f), V(-hx + 0.03f, 0f, hz - 0.03f) };
                foreach (var p in c) Bx(frame, p + V(-0.025f, 0.39f, -0.025f), p + V(0.025f, eave, 0.025f));
                // Side walls: glass between mullions.
                foreach (int sx in new[] { -1, 1 })
                {
                    float x = sx * (hx - 0.03f);
                    int n = Mathf.Max(2, Mathf.RoundToInt(s.z / 0.6f));
                    for (int i = 0; i <= n; i++)
                    {
                        float z = Mathf.Lerp(-hz + 0.03f, hz - 0.03f, i / (float)n);
                        Bx(frame, V(x - 0.015f, 0.39f, z - 0.015f), V(x + 0.015f, eave, z + 0.015f));
                    }
                    GlassPane(V(x, 0.39f, -hz + 0.05f), V(x, eave, hz - 0.05f), Vector3.right);
                    Bx(frame, V(x - 0.02f, eave - 0.03f, -hz), V(x + 0.02f, eave + 0.01f, hz));
                }
                // Gable ends: glass below the eave, a triangle above, the door in front.
                foreach (int sz in new[] { -1, 1 })
                {
                    float z = sz * (hz - 0.03f);
                    GlassPane(V(-hx + 0.05f, 0.39f, z), V(hx - 0.05f, eave, z), Vector3.forward);
                    m.Quad(HillMat.Glass, V(-hx + 0.03f, eave, z), V(hx - 0.03f, eave, z), V(0f, h - 0.03f, z), V(0f, h - 0.03f, z), Vector3.forward * sz);
                    m.Tri(HillMat.Glass, V(-hx + 0.03f, eave, z), V(hx - 0.03f, eave, z), V(0f, h - 0.03f, z), Vector3.forward * -sz);
                    Bx(frame, V(-0.015f, 0.39f, z - 0.015f), V(0.015f, h - 0.03f, z + 0.015f));
                    if (sz > 0)
                    {
                        Bx(frame, V(-0.4f, 0.39f, z - 0.02f), V(-0.37f, 1.95f, z + 0.02f));
                        Bx(frame, V(0.37f, 0.39f, z - 0.02f), V(0.4f, 1.95f, z + 0.02f));
                        Bx(frame, V(-0.4f, 1.92f, z - 0.02f), V(0.4f, 1.95f, z + 0.02f));
                    }
                }
                // The roof: glass slopes with glazing bars, a ridge.
                foreach (int sx in new[] { -1, 1 })
                {
                    var a0 = V(sx * (hx - 0.03f), eave, -hz + 0.03f); var a1 = V(sx * (hx - 0.03f), eave, hz - 0.03f);
                    var b0 = V(0f, h - 0.03f, -hz + 0.03f); var b1 = V(0f, h - 0.03f, hz - 0.03f);
                    m.Quad(HillMat.Glass, a0, a1, b1, b0, V(sx, 1f, 0f));
                    m.Quad(HillMat.Glass, a0, a1, b1, b0, V(-sx, -1f, 0f));
                    int n = Mathf.Max(2, Mathf.RoundToInt(s.z / 0.6f));
                    for (int i = 0; i <= n; i++)
                    {
                        float z = Mathf.Lerp(-hz + 0.03f, hz - 0.03f, i / (float)n);
                        m.Beam(frame, V(sx * (hx - 0.03f), eave + 0.01f, z), V(0f, h - 0.02f, z), 0.025f, 0.025f);
                    }
                }
                Bx(frame, V(-0.03f, h - 0.05f, -hz), V(0.03f, h, hz));
                // Inside: staging with pots and tomato plants.
                Bx(HillMat.FenceWood, V(-hx + 0.08f, 0.75f, -hz + 0.2f), V(-hx + 0.6f, 0.8f, hz - 0.3f), 0.006f);
                for (float z = -hz + 0.35f; z < hz - 0.4f; z += 0.32f)
                {
                    Lathe(HillMat.RoofClay, -hx + 0.34f, z, new[] { 0.8f, 0.94f }, new[] { 0.06f, 0.08f }, 8, true);
                    HillTrees.DrawBush(m, V(-hx + 0.34f, 0.92f, z), new Vector3(0.24f, 0.22f, 0.24f), v * 13 + Mathf.RoundToInt(z * 10f));
                }
                for (float z = -hz + 0.4f; z < hz - 0.5f; z += 0.55f)
                {
                    Cyl(HillMat.FenceWood, V(hx - 0.4f, 0.35f, z), V(hx - 0.4f, 1.8f, z), 0.012f, 4, false);
                    HillTrees.DrawBush(m, V(hx - 0.4f, 0.35f, z), new Vector3(0.4f, 1.3f, 0.4f), v * 7 + Mathf.RoundToInt(z * 10f));
                }
            }

            void WashingLine()
            {
                float hz = s.z * 0.5f - 0.05f, h = s.y;
                foreach (int sz in new[] { -1, 1 })
                {
                    Cyl(HillMat.MetalPaint, V(0f, -0.3f, sz * hz), V(0f, h, sz * hz), 0.03f, 8, true);
                    Cyl(HillMat.MetalPaint, V(-0.35f, h - 0.06f, sz * hz), V(0.35f, h - 0.06f, sz * hz), 0.02f, 6, true);
                }
                string[] cloth = { HillMat.Canvas, HillMat.CanvasBlue, HillMat.CanvasRed, HillMat.TrimWhite, HillMat.CanvasGreen, HillMat.DoorYellow, HillMat.TrimCream };
                foreach (float x in new[] { -0.3f, 0f, 0.3f })
                {
                    var path = new Vector3[9];
                    for (int k = 0; k < 9; k++)
                    {
                        float t = k / 8f;
                        path[k] = V(x, h - 0.07f - Mathf.Sin(t * Mathf.PI) * 0.14f, Mathf.Lerp(-hz, hz, t));
                    }
                    m.Tube(HillMat.Steel, path, new[] { 0.004f }, 3, false, 1f);
                    if (x > 0.2f && r.Next(2) == 0) continue;
                    float z = -hz + R(r, 0.3f, 0.7f);
                    while (z < hz - 0.5f)
                    {
                        float w = R(r, 0.3f, 0.75f), len = R(r, 0.35f, 0.75f);
                        float t = (z + w * 0.5f + hz) / (2f * hz);
                        float ly = h - 0.07f - Mathf.Sin(t * Mathf.PI) * 0.14f;
                        string mat = cloth[r.Next(cloth.Length)];
                        Bx(mat, V(x - 0.004f, ly - len, z), V(x + 0.004f, ly, z + w), 0f);
                        foreach (float pz in new[] { z + 0.04f, z + w - 0.04f })
                            Bx(HillMat.FenceWood, V(x - 0.008f, ly - 0.03f, pz - 0.006f), V(x + 0.008f, ly + 0.025f, pz + 0.006f));
                        z += w + R(r, 0.08f, 0.5f);
                    }
                }
            }

            void Grill()
            {
                float rr = Mathf.Min(s.x, s.z) * 0.42f, h = s.y, cy = h - rr * 0.95f;
                string body = v % 2 == 0 ? HillMat.MetalDark : HillMat.DoorRed;
                m.Ellipsoid(body, V(0f, cy, 0f), new Vector3(rr, rr * 0.9f, rr), 14);
                Torus(m, HillMat.Steel, V(0f, cy + 0.02f, 0f), Vector3.up, rr * 1.0f, 0.012f, 16, 4);
                B(HillMat.FenceWood, 0f, cy + rr * 0.9f + 0.03f, 0f, 0.18f, 0.035f, 0.04f, 0.01f);
                foreach (float x in new[] { -0.07f, 0.07f }) Cyl(HillMat.Steel, V(x, cy + rr * 0.88f, 0f), V(x, cy + rr * 0.9f + 0.015f, 0f), 0.008f, 4, false);
                Disc(m, HillMat.Steel, V(0.0f, cy + rr * 0.7f, rr * 0.7f), V(0f, 0.7f, 0.7f), 0.05f, 8);
                for (int i = 0; i < 3; i++)
                {
                    float a = i * Mathf.PI * 2f / 3f + 0.3f;
                    var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Cyl(body == HillMat.DoorRed ? HillMat.MetalDark : body, d * rr * 0.55f + V(0f, cy - rr * 0.6f, 0f), d * (rr * 0.95f) + V(0f, 0f, 0f), 0.016f, 6, false);
                    if (i < 2) Cyl(HillMat.Rubber, d * rr * 0.95f + V(0f, 0.06f, 0f) - new Vector3(-d.z, 0f, d.x) * 0.02f, d * rr * 0.95f + V(0f, 0.06f, 0f) + new Vector3(-d.z, 0f, d.x) * 0.02f, 0.06f, 8);
                }
                Disc(m, HillMat.Steel, V(0f, 0.25f, 0f), Vector3.up, rr * 0.55f, 10);
                Disc(m, HillMat.Steel, V(0f, 0.25f, 0f), Vector3.down, rr * 0.55f, 10);
                foreach (int sx in new[] { -1, 1 })
                    B(HillMat.FenceWood, sx * (rr + 0.04f), cy + 0.01f, 0f, 0.04f, 0.03f, 0.14f, 0.008f);
            }

            void Sprinkler()
            {
                B(HillMat.DoorGreen, 0f, 0.025f, 0f, s.x * 0.9f, 0.05f, s.z * 0.5f, 0.01f);
                Cyl(HillMat.Steel, V(0f, 0.05f, 0f), V(0f, s.y - 0.03f, 0f), 0.01f, 5, false);
                Cyl(HillMat.Steel, V(-s.x * 0.45f, s.y - 0.03f, 0f), V(s.x * 0.45f, s.y - 0.03f, 0f), 0.012f, 5, true);
                m.Tube(HillMat.DoorGreen, new[] { V(0f, 0.02f, -s.z * 0.25f), V(0.1f, 0.015f, -0.6f), V(-0.3f, 0.015f, -1.2f), V(0.2f, 0.015f, -1.9f) }, new[] { 0.012f }, 5, false, 1f);
            }

            void Mower()
            {
                float hx = s.x * 0.5f, hz = s.z * 0.5f, h = s.y;
                string paint = v % 3 == 0 ? HillMat.DoorRed : v % 3 == 1 ? HillMat.DoorGreen : HillMat.DoorYellow;
                float dz0 = hz - 0.62f, dz1 = hz - 0.02f;
                Bx(paint, V(-hx + 0.05f, 0.08f, dz0), V(hx - 0.05f, 0.27f, dz1), 0.05f);
                Cyl(HillMat.MetalDark, V(0f, 0.27f, (dz0 + dz1) * 0.5f), V(0f, 0.42f, (dz0 + dz1) * 0.5f), 0.12f, 10);
                B(paint, 0.06f, 0.46f, (dz0 + dz1) * 0.5f + 0.02f, 0.14f, 0.08f, 0.14f, 0.02f);
                Cyl(HillMat.MetalDark, V(-0.08f, 0.43f, (dz0 + dz1) * 0.5f - 0.05f), V(-0.08f, 0.47f, (dz0 + dz1) * 0.5f - 0.05f), 0.045f, 8);
                foreach (int sx in new[] { -1, 1 })
                    foreach (float z in new[] { dz0 + 0.08f, dz1 - 0.08f })
                    {
                        Cyl(HillMat.Rubber, V(sx * (hx - 0.05f), 0.09f, z), V(sx * (hx - 0.005f), 0.09f, z), 0.09f, 10);
                        Cyl(HillMat.Steel, V(sx * (hx - 0.004f), 0.09f, z), V(sx * (hx - 0.001f), 0.09f, z), 0.04f, 6);
                    }
                foreach (int sx in new[] { -1, 1 })
                    Cyl(HillMat.MetalDark, V(sx * 0.2f, 0.25f, dz0 + 0.05f), V(sx * 0.21f, h - 0.03f, -hz + 0.05f), 0.014f, 6, false);
                Cyl(HillMat.Rubber, V(-0.22f, h - 0.03f, -hz + 0.05f), V(0.22f, h - 0.03f, -hz + 0.05f), 0.02f, 6, true);
                Loft(HillMat.Canvas, 0.15f, -0.17f, 0.17f, dz0 - 0.35f, dz0 - 0.02f, 0.5f, -0.18f, 0.18f, dz0 - 0.4f, dz0 - 0.02f, true, true);
            }

            void YardSale()
            {
                float hx = s.x * 0.5f, hz = s.z * 0.5f, h = s.y;
                Bx(v % 2 == 0 ? HillMat.TrimWhite : HillMat.FenceWood, V(-hx, h - 0.04f, -hz), V(hx, h, hz), 0.01f);
                foreach (int sx in new[] { -1, 1 })
                {
                    m.Beam(HillMat.MetalDark, V(sx * (hx - 0.15f), 0f, -hz + 0.05f), V(sx * (hx - 0.15f), h - 0.04f, hz - 0.05f), 0.025f, 0.025f);
                    m.Beam(HillMat.MetalDark, V(sx * (hx - 0.15f), 0f, hz - 0.05f), V(sx * (hx - 0.15f), h - 0.04f, -hz + 0.05f), 0.025f, 0.025f);
                }
                if (v % 3 == 0)
                {
                    Bx(HillMat.CanvasRed, V(-hx - 0.01f, h - 0.3f, hz), V(hx + 0.01f, h + 0.004f, hz + 0.008f), 0f);
                    m.Quad(HillMat.CanvasRed, V(-hx - 0.01f, h + 0.006f, -hz), V(hx + 0.01f, h + 0.006f, -hz), V(hx + 0.01f, h + 0.006f, hz), V(-hx - 0.01f, h + 0.006f, hz), Vector3.up);
                }
                // Bric-a-brac along the top.
                float x = -hx + 0.1f, top = h + 0.008f;
                while (x < hx - 0.15f)
                {
                    int item = r.Next(8);
                    float z = R(r, -hz + 0.15f, hz - 0.15f);
                    float w = 0.15f;
                    switch (item)
                    {
                        case 0:   // a stack of books
                        {
                            int n = 2 + r.Next(4);
                            float y = top;
                            for (int i = 0; i < n; i++)
                            {
                                float bh = R(r, 0.03f, 0.05f);
                                B(Paint(r.Next(9)), x + 0.1f + R(r, -0.01f, 0.01f), y + bh * 0.5f, z, R(r, 0.15f, 0.2f), bh, R(r, 0.2f, 0.25f), 0.004f);
                                y += bh;
                            }
                            w = 0.22f;
                            break;
                        }
                        case 1:   // a lamp
                            Lathe(HillMat.Copper, x + 0.08f, z, new[] { top, top + 0.03f, top + 0.05f, top + 0.3f }, new[] { 0.07f, 0.06f, 0.015f, 0.012f }, 8, false);
                            Lathe(HillMat.Canvas, x + 0.08f, z, new[] { top + 0.24f, top + 0.4f }, new[] { 0.13f, 0.08f }, 10, true);
                            w = 0.28f;
                            break;
                        case 2:   // a vase
                            Lathe(Pick(r, new[] { HillMat.Copper, HillMat.TrimCream, HillMat.DoorBlue, HillMat.RoofClay }), x + 0.06f, z, new[] { top, top + 0.06f, top + 0.16f, top + 0.22f, top + 0.24f }, new[] { 0.04f, 0.07f, 0.05f, 0.03f, 0.04f }, 10, false);
                            w = 0.14f;
                            break;
                        case 3:   // a toaster
                            B(HillMat.Steel, x + 0.1f, top + 0.09f, z, 0.2f, 0.18f, 0.13f, 0.03f);
                            B(HillMat.MetalDark, x + 0.1f, top + 0.18f, z, 0.14f, 0.005f, 0.03f);
                            w = 0.24f;
                            break;
                        case 4:   // a crate of records
                            B(HillMat.FenceWood, x + 0.18f, top + 0.08f, z, 0.34f, 0.16f, 0.34f, 0.006f, Side.YPos);
                            for (int i = 0; i < 8; i++) B(Chance(r, 0.5f) ? HillMat.MetalDark : Paint(i), x + 0.03f + i * 0.04f, top + 0.15f, z, 0.008f, 0.3f, 0.3f);
                            w = 0.38f;
                            break;
                        case 5:   // a picture in a frame
                            Bx(HillMat.FenceWood, V(x, top, z - 0.02f), V(x + 0.3f, top + 0.24f, z + 0.02f), 0.006f);
                            Panel(m, HillMat.Interior, V(x + 0.15f, top + 0.12f, z + 0.021f), Vector3.right * 0.24f, Vector3.up * 0.18f, Vector3.forward, Sub(HillAtlas.Interior(r.Next(7)), 0.2f, 0.3f, 0.8f, 0.8f));
                            w = 0.34f;
                            break;
                        case 6:   // mugs
                            for (int i = 0; i < 3; i++) Lathe(Paint(i + 2), x + 0.05f + i * 0.09f, z, new[] { top, top + 0.1f }, new[] { 0.04f, 0.042f }, 8, true);
                            w = 0.3f;
                            break;
                        default:  // a toy ball and a box
                            m.Ellipsoid(Paint(r.Next(9)), V(x + 0.1f, top + 0.1f, z), new Vector3(0.1f, 0.1f, 0.1f), 10);
                            B(HillMat.Canvas, x + 0.3f, top + 0.08f, z, 0.18f, 0.16f, 0.2f, 0.01f);
                            w = 0.42f;
                            break;
                    }
                    // A price tag.
                    B(HillMat.TrimWhite, x + 0.02f, top + 0.003f, Mathf.Min(hz - 0.04f, z + 0.15f), 0.05f, 0.004f, 0.035f);
                    x += w + R(r, 0.05f, 0.15f);
                }
            }

            void PicnicTable()
            {
                float hx = s.x * 0.5f, hz = s.z * 0.5f, h = s.y;
                string wood = v % 2 == 0 ? HillMat.FenceWood : HillMat.Deck;
                for (int i = 0; i < 5; i++)
                {
                    float z = Mathf.Lerp(-0.34f, 0.34f, i / 4f);
                    B(wood, 0f, h - 0.02f, z, s.x, 0.04f, 0.14f, 0.008f);
                }
                foreach (int sz in new[] { -1, 1 })
                    for (int i = 0; i < 2; i++)
                        B(wood, 0f, 0.44f, sz * (hz - 0.07f - i * 0.15f), s.x - 0.1f, 0.04f, 0.13f, 0.008f);
                foreach (int sx in new[] { -1, 1 })
                {
                    float x = sx * (hx - 0.3f);
                    m.Beam(wood, V(x, 0f, -hz + 0.25f), V(x, h - 0.04f, 0.12f), 0.05f, 0.1f, 0.006f);
                    m.Beam(wood, V(x, 0f, hz - 0.25f), V(x, h - 0.04f, -0.12f), 0.05f, 0.1f, 0.006f);
                    Bx(wood, V(x - 0.025f, 0.38f, -hz + 0.02f), V(x + 0.025f, 0.42f, hz - 0.02f), 0.005f);
                    Bx(wood, V(x - 0.025f, h - 0.1f, -0.38f), V(x + 0.025f, h - 0.04f, 0.38f), 0.005f);
                }
            }

            void Crates()
            {
                float hx = s.x * 0.5f, hz = s.z * 0.5f, h = s.y;
                float c = Mathf.Min(hx, hz) - 0.02f, ch = h * 0.5f - 0.01f;
                Crate(V(-hx + c * 0.5f + 0.01f, 0f, -hz + c * 0.5f + 0.02f + R(r, 0f, 0.05f)), c, ch, false);
                Crate(V(hx - c * 0.5f - 0.01f, 0f, hz - c * 0.5f - 0.02f - R(r, 0f, 0.05f)), c, ch, true);
                if (v % 2 == 0) Crate(V(R(r, -0.05f, 0.05f), ch + 0.01f, 0f), c * 0.95f, ch, true);
            }

            void Crate(Vector3 at, float w, float h, bool apples)
            {
                float hw = w * 0.5f;
                foreach (int sx in new[] { -1, 1 })
                    foreach (int sz in new[] { -1, 1 })
                        Bx(HillMat.FenceWood, at + V(sx * hw - (sx > 0 ? 0.05f : 0f), 0f, sz * hw - (sz > 0 ? 0.05f : 0f)), at + V(sx * hw + (sx < 0 ? 0.05f : 0f), h, sz * hw + (sz < 0 ? 0.05f : 0f)), 0.006f);
                for (int k = 0; k < 3; k++)
                {
                    float y = 0.03f + k * (h - 0.04f) / 3f, sh = (h - 0.04f) / 3f - 0.03f;
                    Bx(HillMat.FenceWood, at + V(-hw + 0.05f, y, -hw), at + V(hw - 0.05f, y + sh, -hw + 0.018f), 0.004f);
                    Bx(HillMat.FenceWood, at + V(-hw + 0.05f, y, hw - 0.018f), at + V(hw - 0.05f, y + sh, hw), 0.004f);
                    Bx(HillMat.FenceWood, at + V(-hw, y, -hw + 0.05f), at + V(-hw + 0.018f, y + sh, hw - 0.05f), 0.004f);
                    Bx(HillMat.FenceWood, at + V(hw - 0.018f, y, -hw + 0.05f), at + V(hw, y + sh, hw - 0.05f), 0.004f);
                }
                Bx(HillMat.FenceWood, at + V(-hw + 0.02f, 0.01f, -hw + 0.02f), at + V(hw - 0.02f, 0.03f, hw - 0.02f));
                if (apples)
                    for (int i = 0; i < 9; i++)
                        m.Ellipsoid(Chance(r, 0.6f) ? HillMat.DoorRed : HillMat.DoorYellow, at + V(R(r, -hw + 0.08f, hw - 0.08f), h - 0.07f, R(r, -hw + 0.08f, hw - 0.08f)), new Vector3(0.045f, 0.042f, 0.045f), 6);
            }

            void Pallets()
            {
                float hx = s.x * 0.5f, hz = s.z * 0.5f;
                int n = Mathf.Max(1, Mathf.FloorToInt(s.y / 0.144f));
                for (int k = 0; k < n; k++)
                {
                    float y0 = k * 0.144f;
                    var o = V(R(r, -0.02f, 0.02f), y0, R(r, -0.02f, 0.02f));
                    foreach (float z in new[] { -hz + 0.05f, 0f, hz - 0.05f })
                        Bx(HillMat.Deck, o + V(-hx + 0.02f, 0f, z - 0.05f), o + V(hx - 0.02f, 0.022f, z + 0.05f), 0.003f);
                    foreach (float x in new[] { -hx + 0.07f, 0f, hx - 0.07f })
                        Bx(HillMat.FenceWood, o + V(x - 0.05f, 0.022f, -hz + 0.02f), o + V(x + 0.05f, 0.122f, hz - 0.02f), 0.004f);
                    for (int i = 0; i < 7; i++)
                    {
                        float z = Mathf.Lerp(-hz + 0.07f, hz - 0.07f, i / 6f);
                        Bx(HillMat.Deck, o + V(-hx + 0.02f, 0.122f, z - 0.05f), o + V(hx - 0.02f, 0.144f, z + 0.05f), 0.003f);
                    }
                }
            }

            void Barrel()
            {
                float rr = Mathf.Min(s.x, s.z) * 0.47f, h = s.y;
                if (v % 2 == 0)
                {
                    string paint = Paint(v / 2);
                    Cyl(paint, V(0f, 0f, 0f), V(0f, h - 0.01f, 0f), rr, 14, false);
                    foreach (float y in new[] { h * 0.33f, h * 0.66f }) Torus(m, paint, V(0f, y, 0f), Vector3.up, rr, 0.012f, 16, 4);
                    Torus(m, paint, V(0f, h - 0.012f, 0f), Vector3.up, rr - 0.005f, 0.012f, 16, 4);
                    Disc(m, HillMat.MetalPaint, V(0f, h - 0.02f, 0f), Vector3.up, rr - 0.01f, 14);
                    Cyl(HillMat.Steel, V(rr * 0.5f, h - 0.02f, 0f), V(rr * 0.5f, h - 0.005f, 0f), 0.03f, 6);
                }
                else
                {
                    Lathe(HillMat.FenceWood, 0f, 0f, new[] { 0f, h * 0.25f, h * 0.5f, h * 0.75f, h - 0.01f }, new[] { rr * 0.86f, rr * 0.96f, rr, rr * 0.96f, rr * 0.86f }, 12, false);
                    foreach (float y in new[] { 0.06f, h * 0.3f, h * 0.7f, h - 0.07f })
                    {
                        float k = 1f - Mathf.Pow((y - h * 0.5f) / (h * 0.5f), 2f) * 0.14f;
                        Cyl(HillMat.MetalDark, V(0f, y - 0.02f, 0f), V(0f, y + 0.02f, 0f), rr * k + 0.006f, 12, false);
                    }
                    Disc(m, HillMat.Water, V(0f, h - 0.06f, 0f), Vector3.up, rr * 0.84f, 12);
                    Disc(m, HillMat.FenceWood, V(0f, h * 0.4f, 0f), Vector3.up, rr * 0.9f, 12);
                }
            }

            void TyreStack()
            {
                int n = Mathf.Max(1, Mathf.FloorToInt(s.y / 0.2f));
                float R0 = Mathf.Min(s.x, s.z) * 0.5f - 0.11f;
                for (int i = 0; i < n; i++)
                    Torus(m, HillMat.Rubber, V(R(r, -0.02f, 0.02f), 0.1f + i * 0.195f, R(r, -0.02f, 0.02f)), Vector3.up, R0, 0.095f, 12, 6);
            }

            void Workbench()
            {
                float hx = s.x * 0.5f, hz = s.z * 0.5f, h = s.y;
                Bx(HillMat.Deck, V(-hx, h - 0.06f, -hz), V(hx, h, hz), 0.01f);
                foreach (int sx in new[] { -1, 1 })
                    foreach (int sz in new[] { -1, 1 })
                        Bx(HillMat.FenceWood, V(sx * (hx - 0.08f) - 0.04f, 0f, sz * (hz - 0.08f) - 0.04f), V(sx * (hx - 0.08f) + 0.04f, h - 0.06f, sz * (hz - 0.08f) + 0.04f), 0.006f);
                Bx(HillMat.FenceWood, V(-hx + 0.05f, 0.18f, -hz + 0.06f), V(hx - 0.05f, 0.21f, hz - 0.06f), 0.005f);
                Bx(HillMat.FenceWood, V(-hx + 0.1f, h - 0.16f, hz - 0.1f), V(hx - 0.1f, h - 0.06f, hz - 0.06f));
                // The vice.
                float vx = hx - 0.25f;
                B(HillMat.MetalDark, vx, h + 0.06f, hz - 0.12f, 0.16f, 0.12f, 0.2f, 0.01f);
                B(HillMat.MetalDark, vx, h + 0.06f, hz + 0.0f, 0.16f, 0.1f, 0.05f, 0.008f);
                Cyl(HillMat.Steel, V(vx - 0.12f, h + 0.04f, hz + 0.02f), V(vx + 0.12f, h + 0.04f, hz + 0.02f), 0.008f, 5);
                // Tools and tins on the top.
                m.Beam(HillMat.FenceWood, V(-hx + 0.3f, h + 0.015f, -0.1f), V(-hx + 0.6f, h + 0.015f, 0.05f), 0.028f, 0.028f);
                B(HillMat.MetalDark, -hx + 0.62f, h + 0.03f, 0.06f, 0.1f, 0.04f, 0.04f, 0.005f);
                Bx(HillMat.Steel, V(-0.3f, h, -hz + 0.12f), V(0.25f, h + 0.004f, -hz + 0.26f));
                B(HillMat.FenceWood, -0.36f, h + 0.03f, -hz + 0.19f, 0.12f, 0.06f, 0.1f, 0.01f);
                for (int i = 0; i < 3; i++)
                {
                    float x = 0.2f + i * 0.13f, z = R(r, -0.05f, 0.1f);
                    Lathe(HillMat.MetalPaint, x, z, new[] { h, h + 0.12f }, new[] { 0.055f, 0.055f }, 10, false);
                    Disc(m, Paint(i * 2 + v), V(x, h + 0.121f, z), Vector3.up, 0.052f, 10);
                }
                B(HillMat.DoorRed, -0.1f, 0.3f, 0f, 0.45f, 0.18f, 0.22f, 0.015f);
                Cyl(HillMat.MetalDark, V(-0.25f, 0.42f, 0f), V(0.05f, 0.42f, 0f), 0.01f, 5);
                for (int i = 0; i < 4; i++) B(HillMat.FenceWood, hx - 0.4f + R(r, -0.1f, 0.1f), 0.24f + i * 0.04f, R(r, -0.1f, 0.1f), R(r, 0.3f, 0.6f), 0.035f, 0.09f, 0.004f);
                Lathe(HillMat.TrimCream, hx - 0.5f, -hz + 0.15f, new[] { h, h + 0.1f }, new[] { 0.04f, 0.043f }, 8, true);
            }

            void Ladder()
            {
                float hx = s.x * 0.5f, hz = s.z * 0.5f, h = s.y;
                string mat = v % 2 == 0 ? HillMat.Steel : HillMat.FenceWood;
                var b0 = V(0f, 0f, -hz + 0.05f);
                var t0 = V(0f, h - 0.02f, hz - 0.05f);
                foreach (int sx in new[] { -1, 1 })
                {
                    m.Beam(mat, b0 + V(sx * (hx - 0.04f), 0f, 0f), t0 + V(sx * (hx - 0.04f), 0f, 0f), 0.03f, 0.07f, 0.006f);
                    B(HillMat.Rubber, sx * (hx - 0.04f), 0.02f, -hz + 0.06f, 0.04f, 0.04f, 0.09f, 0.008f);
                }
                int n = Mathf.FloorToInt(h / 0.28f);
                for (int k = 1; k < n; k++)
                {
                    var p = Vector3.Lerp(b0, t0, k / (float)n);
                    Cyl(mat, V(-hx + 0.05f, p.y, p.z), V(hx - 0.05f, p.y, p.z), 0.016f, 6, false);
                }
            }

            void Wheelbarrow()
            {
                float hx = s.x * 0.5f, hz = s.z * 0.5f, h = s.y;
                string tray = v % 2 == 0 ? HillMat.DoorGreen : HillMat.MetalPaint;
                float ty0 = 0.32f, ty1 = h - 0.04f;
                Loft(tray, ty0, -0.2f, 0.2f, -0.3f, 0.2f, ty1, -hx + 0.03f, hx - 0.03f, -0.5f, hz - 0.28f, false, true, true);
                m.Quad(HillMat.Dirt, V(-0.25f, ty0 + 0.14f, -0.36f), V(0.25f, ty0 + 0.14f, -0.36f), V(0.25f, ty0 + 0.14f, 0.26f), V(-0.25f, ty0 + 0.14f, 0.26f), Vector3.up);
                float wz = hz - 0.2f;
                Cyl(HillMat.Rubber, V(-0.05f, 0.19f, wz), V(0.05f, 0.19f, wz), 0.19f, 14);
                Cyl(HillMat.Steel, V(-0.06f, 0.19f, wz), V(0.06f, 0.19f, wz), 0.06f, 8);
                foreach (int sx in new[] { -1, 1 })
                {
                    m.Beam(HillMat.FenceWood, V(sx * 0.07f, 0.19f, wz), V(sx * (hx - 0.1f), 0.5f, -hz + 0.05f), 0.035f, 0.045f, 0.005f);
                    Cyl(HillMat.Rubber, V(sx * (hx - 0.1f), 0.48f, -hz + 0.12f), V(sx * (hx - 0.1f), 0.5f, -hz + 0.02f), 0.022f, 6);
                    m.Beam(HillMat.MetalDark, V(sx * 0.18f, 0.33f, -0.25f), V(sx * 0.2f, 0f, -0.32f), 0.025f, 0.025f);
                }
            }

            void Kennel()
            {
                float hx = s.x * 0.5f, hz = s.z * 0.5f, h = s.y, eave = h * 0.62f, ov = 0.06f;
                string wall = Pick(r, new[] { HillMat.FenceWood, HillMat.SidingRose, HillMat.SidingBlue, HillMat.SidingButter });
                string roofM = Chance(r, 0.5f) ? HillMat.RoofRed : HillMat.RoofGreen;
                float wx = hx - ov, wz = hz - ov;
                m.Push(WallFrame(V(-wx, 0f, wz), Vector3.right, Vector3.forward));
                var outline = new List<Vector2> { new Vector2(0f, 0.05f), new Vector2(wx * 2f, 0.05f), new Vector2(wx * 2f, eave), new Vector2(wx, h - 0.02f), new Vector2(0f, eave) };
                var hole = new Rect(wx - 0.18f, 0.08f, 0.36f, 0.45f);
                WallFace(m, wall, outline, new[] { hole });
                Room(m, HillMat.Rubber, new Rect(0f, 0f, 1f, 1f), false, hole.xMin, hole.xMax, hole.yMin, hole.yMax, 0f, wz * 1.6f);
                Bx(HillMat.TrimWhite, V(hole.xMin - 0.04f, hole.yMax, 0f), V(hole.xMax + 0.04f, hole.yMax + 0.05f, 0.02f), 0.005f, Side.ZNeg);
                Bx(HillMat.TrimCream, V(wx - 0.12f, hole.yMax + 0.1f, 0f), V(wx + 0.12f, hole.yMax + 0.17f, 0.012f), 0.004f, Side.ZNeg);
                m.Pop();
                Bx(wall, V(-wx, 0.05f, -wz), V(wx, eave, wz - 0.001f), 0f, Side.ZPos | Side.YPos | Side.YNeg);
                m.Tri(wall, V(-wx, eave, -wz), V(wx, eave, -wz), V(0f, h - 0.02f, -wz), Vector3.back);
                Bx(HillMat.FenceWood, V(-wx - 0.02f, 0f, -wz - 0.02f), V(wx + 0.02f, 0.05f, wz + 0.02f), 0.005f);
                foreach (int sx in new[] { -1, 1 })
                {
                    var a0 = V(sx * hx, eave - 0.05f, -hz); var a1 = V(sx * hx, eave - 0.05f, hz);
                    var b0 = V(0f, h, -hz); var b1 = V(0f, h, hz);
                    m.Quad(roofM, a0, a1, b1, b0, V(sx, 1f, 0f));
                    m.Quad(HillMat.FenceWood, a0 + V(0f, -0.04f, 0f), a1 + V(0f, -0.04f, 0f), b1 + V(0f, -0.04f, 0f), b0 + V(0f, -0.04f, 0f), V(-sx, -1f, 0f));
                    m.Quad(HillMat.TrimWhite, a0, a0 + V(0f, -0.04f, 0f), b0 + V(0f, -0.04f, 0f), b0, Vector3.back);
                    m.Quad(HillMat.TrimWhite, a1, a1 + V(0f, -0.04f, 0f), b1 + V(0f, -0.04f, 0f), b1, Vector3.forward);
                    m.Quad(HillMat.TrimWhite, a0, a1, a1 + V(0f, -0.04f, 0f), a0 + V(0f, -0.04f, 0f), V(sx, 0f, 0f));
                }
            }

            void BirdBath()
            {
                float h = s.y, br = Mathf.Min(s.x, s.z) * 0.48f;
                string stone = v % 2 == 0 ? HillMat.StoneCoping : HillMat.Concrete;
                Lathe(stone, 0f, 0f, new[] { -0.05f, 0.08f, 0.12f, h * 0.3f, h * 0.7f, h - 0.16f }, new[] { br * 0.62f, br * 0.62f, br * 0.4f, br * 0.25f, br * 0.22f, br * 0.3f }, 10, false);
                m.Tube(stone, new[] { V(0f, h - 0.17f, 0f), V(0f, h - 0.1f, 0f), V(0f, h - 0.03f, 0f), V(0f, h - 0.01f, 0f) }, new[] { br * 0.3f, br * 0.75f, br * 0.98f, br }, 14, false, 1f);
                Disc(m, HillMat.Water, V(0f, h - 0.04f, 0f), Vector3.up, br * 0.93f, 14);
                Disc(m, stone, V(0f, h - 0.1f, 0f), Vector3.up, br * 0.7f, 12);
                if (v % 3 == 0)
                {
                    var b = V(br * 0.9f, h + 0.03f, 0f);
                    m.Ellipsoid(HillMat.TrimDark, b, new Vector3(0.045f, 0.03f, 0.03f), 8);
                    m.Ellipsoid(HillMat.TrimDark, b + V(0.04f, 0.03f, 0f), new Vector3(0.02f, 0.02f, 0.02f), 6);
                    m.Beam(HillMat.DoorYellow, b + V(0.055f, 0.03f, 0f), b + V(0.075f, 0.028f, 0f), 0.008f, 0.008f);
                }
            }

            void Gnome()
            {
                float h = s.y, k = h / 0.4f;
                string coat = Pick(r, new[] { HillMat.DoorBlue, HillMat.DoorGreen, HillMat.DoorYellow });
                Lathe(coat, 0f, 0f, new[] { 0.02f * k, 0.12f * k, 0.2f * k }, new[] { 0.09f * k, 0.085f * k, 0.06f * k }, 10, false);
                Cyl(HillMat.MetalDark, V(0f, 0.11f * k, 0f), V(0f, 0.135f * k, 0f), 0.088f * k, 10, false);
                foreach (int sx in new[] { -1, 1 }) m.Ellipsoid(HillMat.MetalDark, V(sx * 0.04f * k, 0.02f * k, 0.03f * k), new Vector3(0.035f, 0.022f, 0.05f) * k, 6);
                m.Ellipsoid(HillMat.TrimCream, V(0f, 0.25f * k, 0.01f * k), new Vector3(0.055f, 0.05f, 0.05f) * k, 8);
                m.Ellipsoid(HillMat.DoorRed, V(0f, 0.255f * k, 0.06f * k), new Vector3(0.018f, 0.016f, 0.016f) * k, 6);
                Lathe(HillMat.TrimWhite, 0f, 0.035f * k, new[] { 0.14f * k, 0.2f * k, 0.25f * k }, new[] { 0.005f * k, 0.045f * k, 0.04f * k }, 8, false);
                Lathe(HillMat.DoorRed, 0f, -0.005f * k, new[] { 0.27f * k, 0.33f * k, h - 0.005f }, new[] { 0.062f * k, 0.035f * k, 0.004f }, 10, false);
                foreach (int sx in new[] { -1, 1 }) Cyl(coat, V(sx * 0.07f * k, 0.18f * k, 0f), V(sx * 0.05f * k, 0.11f * k, 0.05f * k), 0.022f * k, 6);
            }

            void StreetSign()
            {
                float h = s.y, len = Mathf.Max(0.5f, s.z - 0.02f);
                Cyl(HillMat.MetalDark, V(0f, -0.2f, 0f), V(0f, h - 0.05f, 0f), 0.03f, 8);
                m.Ellipsoid(HillMat.MetalDark, V(0f, h - 0.04f, 0f), new Vector3(0.04f, 0.04f, 0.04f), 6);
                int a = ((v % HillAtlas.SignRows) + HillAtlas.SignRows) % HillAtlas.SignRows;
                if (a < HillAtlas.SignMillStreet || a > HillAtlas.SignLarchStreet) a = HillAtlas.SignMillStreet + ((v % 3) + 3) % 3;
                int b = HillAtlas.SignMillStreet + ((a - HillAtlas.SignMillStreet + 1) % 3);
                float bh = len / 8f;
                SignBlade(V(0f, h - 0.2f, 0f), Vector3.forward, len, bh, a);
                SignBlade(V(0f, h - 0.2f - bh - 0.02f, 0f), Vector3.right, len, bh, b);
            }

            void SignBlade(Vector3 c, Vector3 along, float len, float h, int sign)
            {
                var across = Vector3.Cross(Vector3.up, along).normalized;
                var size = along * len + across * 0.016f + Vector3.up * (h + 0.02f);
                m.BoxC(HillMat.MetalPaint, c, new Vector3(Mathf.Abs(size.x), size.y, Mathf.Abs(size.z)), 0.004f);
                foreach (int side in new[] { -1, 1 })
                {
                    var facing = across * side;
                    Panel(m, HillMat.SignBoard, c + facing * 0.0095f, Vector3.Cross(facing, Vector3.up) * -len, Vector3.up * h, facing, HillAtlas.Sign(sign));
                }
            }

            void Bollard()
            {
                float h = s.y, rr = Mathf.Min(s.x, s.z) * 0.4f;
                string body = v % 3 == 0 ? HillMat.Concrete : v % 3 == 1 ? HillMat.MetalDark : HillMat.TrimWhite;
                Lathe(body, 0f, 0f, new[] { -0.05f, 0.06f, 0.08f, h - 0.08f }, new[] { rr * 1.2f, rr * 1.2f, rr, rr }, 10, false);
                m.Ellipsoid(body, V(0f, h - 0.08f, 0f), new Vector3(rr, 0.08f, rr), 10);
                Cyl(body == HillMat.TrimWhite ? HillMat.DoorRed : HillMat.TrimWhite, V(0f, h - 0.25f, 0f), V(0f, h - 0.18f, 0f), rr + 0.004f, 10, false);
            }

            void Railing()
            {
                float hz = s.z * 0.5f, h = s.y;
                string mat = v % 2 == 0 ? HillMat.MetalDark : HillMat.MetalPaint;
                int posts = Mathf.Max(2, Mathf.RoundToInt(s.z / 1.5f) + 1);
                for (int i = 0; i < posts; i++)
                {
                    float z = Mathf.Lerp(-hz + 0.03f, hz - 0.03f, i / (float)(posts - 1));
                    Bx(mat, V(-0.03f, -0.1f, z - 0.03f), V(0.03f, h - 0.04f, z + 0.03f), 0.006f);
                }
                Cyl(mat, V(0f, h - 0.03f, -hz), V(0f, h - 0.03f, hz), 0.03f, 8, true);
                Cyl(mat, V(0f, 0.12f, -hz + 0.03f), V(0f, 0.12f, hz - 0.03f), 0.015f, 6, false);
                for (float z = -hz + 0.12f; z < hz - 0.06f; z += 0.12f)
                    Bx(mat, V(-0.008f, 0.12f, z - 0.008f), V(0.008f, h - 0.05f, z + 0.008f), 0f, Side.YNeg | Side.YPos);
            }

            void FlowerBed()
            {
                float hx = s.x * 0.5f, hz = s.z * 0.5f, h = Mathf.Max(0.15f, s.y - 0.05f);
                bool brick = v % 2 == 0;
                string edge = brick ? HillMat.BrickRed : HillMat.FenceWood;
                float t = brick ? 0.1f : 0.08f;
                Bx(edge, V(-hx, -0.05f, -hz), V(hx, h, -hz + t), 0.01f);
                Bx(edge, V(-hx, -0.05f, hz - t), V(hx, h, hz), 0.01f);
                Bx(edge, V(-hx, -0.05f, -hz + t), V(-hx + t, h, hz - t), 0.01f);
                Bx(edge, V(hx - t, -0.05f, -hz + t), V(hx, h, hz - t), 0.01f);
                if (brick)
                {
                    Bx(HillMat.StoneCoping, V(-hx - 0.01f, h, -hz - 0.01f), V(hx + 0.01f, h + 0.03f, -hz + t + 0.01f), 0.006f);
                    Bx(HillMat.StoneCoping, V(-hx - 0.01f, h, hz - t - 0.01f), V(hx + 0.01f, h + 0.03f, hz + 0.01f), 0.006f);
                }
                m.Quad(HillMat.Dirt, V(-hx + t, h - 0.05f, -hz + t), V(hx - t, h - 0.05f, -hz + t), V(hx - t, h - 0.05f, hz - t), V(-hx + t, h - 0.05f, hz - t), Vector3.up);
                HillTrees.DrawFlowers(m, V(0f, h - 0.05f, 0f), new Vector2(s.x - 2f * t - 0.04f, s.z - 2f * t - 0.04f), 22f, v * 11 + 5);
                HillTrees.DrawGrass(m, V(0f, h - 0.05f, 0f), new Vector2(s.x - 2f * t - 0.04f, s.z - 2f * t - 0.04f), 6f, v * 7 + 1);
            }

            void Rock()
            {
                float sd = R(r, 0f, 100f);
                m.Ellipsoid(HillMat.StoneWall, V(0f, s.y * 0.38f, 0f), new Vector3(s.x * 0.48f, s.y * 0.62f, s.z * 0.48f), 12, u => 1f + 0.18f * Noise(u * 2.2f, sd) - (u.y < -0.3f ? 0.1f : 0f));
                if (s.x > 0.6f) m.Ellipsoid(HillMat.StoneWall, V(s.x * 0.32f, 0.08f, s.z * 0.3f), new Vector3(0.18f, 0.13f, 0.15f), 8, u => 1f + 0.2f * Noise(u * 3f, sd + 5f));
                for (int i = 0; i < 10; i++)
                {
                    float a = R(r, 0f, 6.283f);
                    Tuft(m, HillMat.GrassCards, V(Mathf.Cos(a) * s.x * 0.48f, 0f, Mathf.Sin(a) * s.z * 0.48f), R(r, 0.25f, 0.4f), R(r, 0.2f, 0.35f), R(r, 0f, 3.14f), HillAtlas.Tuft(r.Next(4)), 0.1f);
                }
            }

            void AcUnit()
            {
                float hx = s.x * 0.5f, hz = s.z * 0.5f, h = s.y;
                Bx(HillMat.MetalPaint, V(-hx, 0.06f, -hz), V(hx, h, hz), 0.02f);
                foreach (int sx in new[] { -1, 1 }) B(HillMat.Rubber, sx * (hx - 0.1f), 0.03f, 0f, 0.1f, 0.06f, s.z - 0.04f, 0.01f);
                float fx = -hx * 0.25f, fy = (h + 0.06f) * 0.5f, fr = Mathf.Min(h * 0.38f, hx * 0.6f);
                Disc(m, HillMat.MetalDark, V(fx, fy, hz + 0.002f), Vector3.forward, fr, 16);
                for (int k = 0; k < 3; k++)
                {
                    float a = k * Mathf.PI * 2f / 3f;
                    Bx(HillMat.MetalPaint, V(fx - 0.02f, fy - 0.02f, hz + 0.004f) + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * fr * 0.4f, V(fx + 0.02f, fy + 0.02f, hz + 0.008f) + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * fr * 0.4f);
                }
                foreach (float k in new[] { 0.95f, 0.65f, 0.35f }) Torus(m, HillMat.Steel, V(fx, fy, hz + 0.012f), Vector3.forward, fr * k, 0.005f, 16, 3);
                for (float y = 0.12f; y < h - 0.05f; y += 0.045f)
                    Bx(HillMat.Steel, V(hx * 0.45f, y, hz - 0.002f), V(hx - 0.05f, y + 0.012f, hz + 0.006f));
                Cyl(HillMat.Copper, V(hx, 0.2f, -hz + 0.1f), V(hx - 0.02f, 0.2f, -hz + 0.1f), 0.012f, 6);
            }

            void Dish()
            {
                float rr = Mathf.Min(s.x, s.y) * 0.45f, h = s.y;
                var c = V(0f, h * 0.55f, s.z * 0.5f - 0.1f);
                var axis = new Vector3(0f, 0.45f, 1f).normalized;
                var u = Vector3.Cross(axis, Vector3.up).normalized;
                var w = Vector3.Cross(u, axis);
                int seg = 14;
                for (int i = 0; i < seg; i++)
                {
                    float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                    Vector3 Ring(float a, float rad, float depth) => c + (u * Mathf.Cos(a) + w * Mathf.Sin(a)) * rad - axis * depth;
                    var p00 = Ring(a0, 0.02f, 0.08f); var p01 = Ring(a1, 0.02f, 0.08f);
                    var p10 = Ring(a0, rr * 0.55f, 0.05f); var p11 = Ring(a1, rr * 0.55f, 0.05f);
                    var p20 = Ring(a0, rr, 0f); var p21 = Ring(a1, rr, 0f);
                    m.Quad(HillMat.TrimWhite, p00, p01, p11, p10, axis);
                    m.Quad(HillMat.TrimWhite, p10, p11, p21, p20, axis);
                    m.Quad(HillMat.MetalPaint, p00, p01, p11, p10, -axis);
                    m.Quad(HillMat.MetalPaint, p10, p11, p21, p20, -axis);
                }
                var focus = c + axis * rr * 0.9f;
                m.Beam(HillMat.Steel, c - w * rr * 0.9f, focus, 0.015f, 0.015f);
                m.BoxC(HillMat.MetalDark, focus, new Vector3(0.05f, 0.05f, 0.08f), 0.01f);
                Bx(HillMat.MetalDark, V(-0.06f, 0f, -s.z * 0.5f), V(0.06f, h * 0.7f, -s.z * 0.5f + 0.02f), 0.005f);
                m.Beam(HillMat.Steel, V(0f, h * 0.5f, -s.z * 0.5f + 0.02f), c - axis * 0.08f, 0.03f, 0.03f);
            }

            void Hose()
            {
                float hx = s.x * 0.5f, h = s.y, cy = h * 0.55f, rr = Mathf.Min(h * 0.42f, s.z * 0.45f);
                string reel = v % 2 == 0 ? HillMat.DoorGreen : HillMat.DoorYellow;
                foreach (int sx in new[] { -1, 1 })
                {
                    Cyl(reel, V(sx * (hx * 0.5f), cy, 0f), V(sx * (hx * 0.5f + 0.02f), cy, 0f), rr, 14);
                    m.Beam(HillMat.MetalPaint, V(sx * (hx * 0.5f + 0.03f), cy, 0f), V(sx * (hx * 0.5f + 0.05f), 0f, -s.z * 0.4f), 0.025f, 0.025f);
                    m.Beam(HillMat.MetalPaint, V(sx * (hx * 0.5f + 0.03f), cy, 0f), V(sx * (hx * 0.5f + 0.05f), 0f, s.z * 0.4f), 0.025f, 0.025f);
                }
                Cyl(reel, V(-hx * 0.5f, cy, 0f), V(hx * 0.5f, cy, 0f), rr * 0.35f, 10, false);
                for (int k = 0; k < 4; k++) Torus(m, HillMat.DoorGreen, V(-hx * 0.36f + k * hx * 0.24f, cy, 0f), Vector3.right, rr * 0.55f, rr * 0.18f, 12, 5);
                Cyl(HillMat.Steel, V(hx * 0.5f + 0.02f, cy, 0f), V(hx * 0.5f + 0.1f, cy, 0f), 0.012f, 5);
                B(HillMat.Rubber, hx * 0.5f + 0.12f, cy, 0f, 0.04f, 0.1f, 0.03f, 0.008f);
                m.Tube(HillMat.DoorGreen, new[] { V(0f, cy - rr * 0.6f, rr * 0.3f), V(0.1f, 0.02f, s.z * 0.5f + 0.2f), V(-0.4f, 0.015f, s.z * 0.5f + 0.8f), V(0.3f, 0.015f, s.z * 0.5f + 1.3f) }, new[] { 0.014f }, 5, false, 1f);
                Cyl(HillMat.DoorYellow, V(0.3f, 0.02f, s.z * 0.5f + 1.3f), V(0.38f, 0.02f, s.z * 0.5f + 1.42f), 0.02f, 6);
            }

            void PaddlingPool()
            {
                float R0 = Mathf.Min(s.x, s.z) * 0.5f - 0.11f;
                string a = v % 2 == 0 ? HillMat.DoorBlue : HillMat.DoorGreen;
                Torus(m, a, V(0f, 0.1f, 0f), Vector3.up, R0, 0.1f, 24, 8);
                Torus(m, HillMat.TrimWhite, V(0f, 0.22f, 0f), Vector3.up, R0, 0.065f, 24, 6);
                Disc(m, a, V(0f, 0.012f, 0f), Vector3.up, R0, 24);
                Disc(m, HillMat.Water, V(0f, 0.2f, 0f), Vector3.up, R0 - 0.02f, 24);
                var duck = V(R(r, -0.3f, 0.3f), 0.22f, R(r, -0.3f, 0.3f));
                m.Ellipsoid(HillMat.DoorYellow, duck, new Vector3(0.07f, 0.045f, 0.09f), 8);
                m.Ellipsoid(HillMat.DoorYellow, duck + V(0f, 0.06f, 0.05f), new Vector3(0.04f, 0.04f, 0.04f), 8);
                m.Beam(HillMat.DoorRed, duck + V(0f, 0.06f, 0.09f), duck + V(0f, 0.055f, 0.12f), 0.025f, 0.012f);
            }

            void Chalk()
            {
                float y = 0.012f;
                string chalk = HillMat.RoadPaint;
                float sq = Mathf.Min(0.45f, Mathf.Min(s.x, s.z) / 4.4f), lw = 0.025f;
                // Hopscotch along z: single, double, single, double, single, double, then the round top.
                var cells = new List<Rect>();
                float z = -s.z * 0.5f + 0.1f;
                int[] row = { 1, 2, 1, 2, 1, 2 };
                foreach (int n in row)
                {
                    for (int i = 0; i < n; i++) cells.Add(new Rect(n == 1 ? -sq * 0.5f : (i == 0 ? -sq : 0f), z, sq, sq));
                    z += sq;
                }
                void Line(Vector3 a, Vector3 b) => Strip(m, chalk, a, b, lw, Vector3.up);
                foreach (var c in cells)
                {
                    Line(V(c.xMin, y, c.yMin), V(c.xMax, y, c.yMin));
                    Line(V(c.xMax, y, c.yMin), V(c.xMax, y, c.yMax));
                    Line(V(c.xMax, y, c.yMax), V(c.xMin, y, c.yMax));
                    Line(V(c.xMin, y, c.yMax), V(c.xMin, y, c.yMin));
                }
                for (int k = 0; k < 8; k++)
                {
                    float a0 = Mathf.PI * k / 8f, a1 = Mathf.PI * (k + 1) / 8f;
                    Line(V(-Mathf.Cos(a0) * sq * 0.5f, y, z + Mathf.Sin(a0) * sq * 0.5f), V(-Mathf.Cos(a1) * sq * 0.5f, y, z + Mathf.Sin(a1) * sq * 0.5f));
                }
                // Numbers lying on the ground.
                m.Push(Frame(V(0f, y + 0.001f, 0f), Vector3.right, Vector3.forward, Vector3.up));
                int num = 1;
                foreach (var c in cells)
                {
                    Digits(m, chalk, num.ToString(), V(c.center.x - sq * 0.12f, c.center.y - sq * 0.18f, 0f), sq * 0.36f, 0.002f);
                    num++;
                }
                m.Pop();
                // A sun in the corner.
                var sc = V(s.x * 0.5f - 0.35f, y, s.z * 0.5f - 0.35f);
                for (int k = 0; k < 10; k++)
                {
                    float a0 = k * Mathf.PI * 0.2f, a1 = (k + 1) * Mathf.PI * 0.2f;
                    Strip(m, HillMat.DoorYellow, sc + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * 0.16f, sc + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * 0.16f, 0.03f, Vector3.up);
                    Strip(m, HillMat.DoorYellow, sc + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * 0.21f, sc + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * 0.3f, 0.025f, Vector3.up);
                }
            }

            void Ball()
            {
                float rr = Mathf.Min(s.x, Mathf.Min(s.y, s.z)) * 0.5f;
                m.Ellipsoid(Paint(v), V(0f, rr, 0f), new Vector3(rr, rr, rr), 12);
                Torus(m, HillMat.TrimWhite, V(0f, rr, 0f), new Vector3(0.3f, 1f, 0.2f), rr * 1.0f, rr * 0.08f, 16, 4);
            }
        }
    }
}

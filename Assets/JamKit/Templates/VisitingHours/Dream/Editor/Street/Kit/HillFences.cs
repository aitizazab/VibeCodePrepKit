using System.Collections.Generic;
using JamKit.DeepBlock;
using UnityEngine;
using static JamKit.DreamEscape.EditorTools.HillKit;
using Random = System.Random;
using Side = JamKit.DreamEscape.EditorTools.HillMesh.Side;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// The hill's fences and garden walls, each filling its collider box exactly (posts, rails and copings inside it),
    /// running along the box's longer horizontal axis, styled by <see cref="BlockPiece.style"/> (falling back on its
    /// kind): picket, board, lattice, chainlink, railing, brick_low, garden_wall, stone_low, stone_retaining,
    /// concrete_retaining, hedge, gate.
    /// </summary>
    public static class HillFences
    {
        public static readonly string[] Styles =
        {
            "picket", "board", "lattice", "chainlink", "railing", "brick_low", "garden_wall", "stone_low", "stone_retaining",
            "concrete_retaining", "hedge", "gate",
        };

        public static void Draw(HillMesh m, BlockPiece p)
        {
            if (p == null) return;
            string style = string.IsNullOrEmpty(p.style) ? StyleOf(p.kind) : p.style;
            if (style == "hedge") { HillTrees.DrawHedge(m, p); return; }
            var c = U(p.center);
            var s = U(p.size);
            bool alongX = s.x >= s.z;
            float L = alongX ? s.x : s.z, T = alongX ? s.z : s.x, H = s.y;
            if (L < 0.05f || T < 0.01f || H < 0.05f) return;
            var r = new Random(Mathf.RoundToInt(c.x * 31f + c.z * 17f + c.y * 7f) ^ style.GetHashCode());
            m.Push(new Vector3(c.x, c.y - H * 0.5f, c.z), alongX ? 0f : -90f);
            try
            {
                var f = new Fence { m = m, r = r, L = L, T = T, H = H };
                switch (style)
                {
                    case "picket": f.Picket(); break;
                    case "lattice": f.Board(true); break;
                    case "chainlink": f.Chainlink(); break;
                    case "railing":
                    case "railing_tall": f.Railing(); break;
                    case "brick_low": f.Masonry(Chance(r, 0.7f) ? HillMat.BrickRed : HillMat.BrickDark, HillMat.StoneCoping, true); break;
                    case "garden_wall": f.Masonry(Chance(r, 0.6f) ? HillMat.Stucco : HillMat.StuccoWarm, HillMat.StoneCoping, false); break;
                    case "stone_low": f.DryStone(); break;
                    case "stone_retaining": f.Retaining(false); break;
                    case "concrete_retaining": f.Retaining(true); break;
                    case "gate": f.Gate(); break;
                    default: f.Board(false); break;
                }
            }
            finally { m.Pop(); }
        }

        static string StyleOf(BlockKind k)
        {
            switch (k)
            {
                case BlockKind.Picket: return "picket";
                case BlockKind.Hedge: return "hedge";
                case BlockKind.BrickWall: return "brick_low";
                case BlockKind.BlockWall: return "garden_wall";
                case BlockKind.Gate: return "gate";
                case BlockKind.Ledge: return "stone_low";
                default: return "board";
            }
        }

        /// <summary>One fence in its own frame: x along it (-L/2..L/2), z across (-T/2..T/2), y up from its foot (0..H).</summary>
        sealed class Fence
        {
            public HillMesh m;
            public Random r;
            public float L, T, H;

            List<float> PostXs(float spacing, float post)
            {
                int n = Mathf.Max(2, Mathf.RoundToInt(L / spacing) + 1);
                var xs = new List<float>();
                for (int i = 0; i < n; i++) xs.Add(Mathf.Lerp(-L * 0.5f + post * 0.5f, L * 0.5f - post * 0.5f, i / (float)(n - 1)));
                return xs;
            }

            void CapPyramid(string mat, float x, float z, float half, float y0, float y1)
            {
                var a = new Vector3(x - half, y0, z - half); var b = new Vector3(x + half, y0, z - half);
                var c = new Vector3(x + half, y0, z + half); var d = new Vector3(x - half, y0, z + half);
                var apex = new Vector3(x, y1, z);
                m.Tri(mat, a, b, apex, new Vector3(0f, 0.5f, -1f));
                m.Tri(mat, b, c, apex, new Vector3(1f, 0.5f, 0f));
                m.Tri(mat, c, d, apex, new Vector3(0f, 0.5f, 1f));
                m.Tri(mat, d, a, apex, new Vector3(-1f, 0.5f, 0f));
            }

            public void Picket()
            {
                string mat = Chance(r, 0.8f) ? HillMat.FencePaint : HillMat.FenceWood;
                float ps = Mathf.Min(0.09f, T), cap = 0.07f;
                var posts = PostXs(2f, ps);
                foreach (float x in posts)
                {
                    m.Box(mat, new Vector3(x - ps * 0.5f, 0f, -ps * 0.5f), new Vector3(x + ps * 0.5f, H - cap, ps * 0.5f), 0.008f, Side.YNeg);
                    CapPyramid(mat, x, 0f, ps * 0.5f, H - cap, H);
                }
                float zr0 = -Mathf.Min(T * 0.5f, 0.05f), zr1 = zr0 + Mathf.Min(0.035f, T * 0.45f), zp1 = Mathf.Min(zr1 + 0.022f, T * 0.5f);
                foreach (float y in new[] { H * 0.22f, H * 0.72f })
                    m.Box(mat, new Vector3(-L * 0.5f + ps, y - 0.035f, zr0), new Vector3(L * 0.5f - ps, y + 0.035f, zr1), 0.005f);
                float pitch = R(r, 0.11f, 0.13f), pw = 0.07f;
                var outline = new Vector2[5];
                for (float x = -L * 0.5f + ps + 0.03f; x + pw < L * 0.5f - ps - 0.02f; x += pitch)
                {
                    bool atPost = false;
                    foreach (float px in posts) if (x + pw > px - ps * 0.5f - 0.01f && x < px + ps * 0.5f + 0.01f) atPost = true;
                    if (atPost) continue;
                    float top = H - 0.03f - R(r, 0f, 0.015f);
                    outline[0] = new Vector2(x, 0.04f); outline[1] = new Vector2(x + pw, 0.04f); outline[2] = new Vector2(x + pw, top - 0.05f);
                    outline[3] = new Vector2(x + pw * 0.5f, top); outline[4] = new Vector2(x, top - 0.05f);
                    // Its two faces and two edges (18 vertices, not an extrusion's 30: pickets were a tenth of the hill's
                    // vertices; the slanted tops are 2 cm wide and never show).
                    var front = new Vector3[5];
                    var back = new Vector3[5];
                    for (int k = 0; k < 5; k++) { front[k] = new Vector3(outline[k].x, outline[k].y, zr1); back[k] = new Vector3(outline[k].x, outline[k].y, zp1); }
                    m.Poly(mat, front, Vector3.back);
                    m.Poly(mat, back, Vector3.forward);
                    m.Quad(mat, new Vector3(x, 0.04f, zr1), new Vector3(x, top - 0.05f, zr1), new Vector3(x, top - 0.05f, zp1), new Vector3(x, 0.04f, zp1), Vector3.left);
                    m.Quad(mat, new Vector3(x + pw, 0.04f, zr1), new Vector3(x + pw, top - 0.05f, zr1), new Vector3(x + pw, top - 0.05f, zp1), new Vector3(x + pw, 0.04f, zp1), Vector3.right);
                }
            }

            public void Board(bool latticeTop)
            {
                bool painted = Chance(r, 0.35f);
                string wood = painted ? HillMat.FencePaint : HillMat.FenceWood;
                string postMat = painted ? HillMat.FencePaint : HillMat.FenceWood;
                // Posts and rails behind, boards in front of them, a cap rail over the whole thickness.
                float ps = 0.09f, capH = 0.045f;
                float zBack = -T * 0.5f, pdp = Mathf.Max(0.01f, Mathf.Min(0.09f, T - 0.03f));
                float zBoard0 = zBack + pdp, zBoard1 = Mathf.Min(zBoard0 + 0.02f, T * 0.5f);
                var posts = PostXs(2.4f, ps);
                foreach (float x in posts)
                    m.Box(postMat, new Vector3(x - ps * 0.5f, 0f, zBack), new Vector3(x + ps * 0.5f, H - capH, zBoard0), 0.01f, Side.YNeg | Side.YPos);
                float boardTop = latticeTop ? H - 0.38f : H - capH;
                foreach (float y in latticeTop ? new[] { 0.25f, boardTop - 0.25f } : new[] { 0.25f, H * 0.5f, H - 0.3f })
                    m.Box(postMat, new Vector3(-L * 0.5f + ps, y - 0.045f, zBack + pdp * 0.3f), new Vector3(L * 0.5f - ps, y + 0.045f, zBoard0), 0f);
                float bw = R(r, 0.12f, 0.15f), gap = 0.008f;
                int n = Mathf.Max(1, Mathf.FloorToInt(L / (bw + gap)));
                float x0 = -L * 0.5f + (L - n * (bw + gap)) * 0.5f;
                for (int i = 0; i < n; i++)
                {
                    float x = x0 + i * (bw + gap);
                    m.Box(wood, new Vector3(x, R(r, 0.02f, 0.05f), zBoard0), new Vector3(x + bw, boardTop - R(r, 0f, 0.01f), zBoard1), 0f, Side.YPos);
                }
                // The cap rail along the top (over the posts), its front edge flush with the box.
                m.Box(postMat, new Vector3(-L * 0.5f, H - capH, zBack), new Vector3(L * 0.5f, H, Mathf.Min(zBoard1 + 0.02f, T * 0.5f)), 0.008f);
                if (latticeTop)
                {
                    float y0 = boardTop, y1 = H - capH;
                    m.Box(postMat, new Vector3(-L * 0.5f, y0, zBack + 0.01f), new Vector3(L * 0.5f, y0 + 0.04f, Mathf.Min(zBoard1 + 0.02f, T * 0.5f)), 0.006f);
                    float lz = (zBoard0 + zBoard1) * 0.5f;
                    m.Push(Frame(new Vector3(0f, 0f, lz), Vector3.right, Vector3.up, Vector3.forward));
                    Lattice(m, wood, -L * 0.5f + ps, L * 0.5f - ps, y0 + 0.04f, y1, 0f, 0.13f, 0.03f);
                    m.Pop();
                    m.Push(Frame(new Vector3(0f, 0f, lz - 0.001f), Vector3.left, Vector3.up, Vector3.back));
                    Lattice(m, wood, -L * 0.5f + ps, L * 0.5f - ps, y0 + 0.04f, y1, 0f, 0.13f, 0.03f);
                    m.Pop();
                }
            }

            public void Chainlink()
            {
                var posts = PostXs(2.5f, 0.06f);
                foreach (float x in posts)
                {
                    m.Cylinder(HillMat.Steel, new Vector3(x, 0f, 0f), new Vector3(x, H - 0.02f, 0f), 0.03f, 8, false);
                    m.Ellipsoid(HillMat.Steel, new Vector3(x, H - 0.03f, 0f), new Vector3(0.036f, 0.03f, 0.036f), 6);
                }
                m.Cylinder(HillMat.Steel, new Vector3(-L * 0.5f, H - 0.06f, 0f), new Vector3(L * 0.5f, H - 0.06f, 0f), 0.02f, 6, true);
                m.Cylinder(HillMat.Steel, new Vector3(-L * 0.5f, 0.06f, 0f), new Vector3(L * 0.5f, 0.06f, 0f), 0.006f, 4, false);
                // The diamond mesh: thin strands both ways, two-sided.
                float sp = 0.14f, x0 = -L * 0.5f + 0.03f, x1 = L * 0.5f - 0.03f, y0 = 0.05f, y1 = H - 0.08f;
                for (int dir = 0; dir < 2; dir++)
                {
                    float zz = dir == 0 ? -0.004f : 0.004f;
                    float cMin = dir == 0 ? x0 - y1 : x0 + y0, cMax = dir == 0 ? x1 - y0 : x1 + y1;
                    for (float cc = cMin + sp * 0.5f; cc < cMax; cc += sp)
                    {
                        float xa, xb;
                        if (dir == 0) { xa = Mathf.Max(x0, y0 + cc); xb = Mathf.Min(x1, y1 + cc); }
                        else { xa = Mathf.Max(x0, cc - y1); xb = Mathf.Min(x1, cc - y0); }
                        if (xb - xa < 0.01f) continue;
                        float ya = dir == 0 ? xa - cc : cc - xa, yb = dir == 0 ? xb - cc : cc - xb;
                        Strip(m, HillMat.Steel, new Vector3(xa, ya, zz), new Vector3(xb, yb, zz), 0.01f, Vector3.forward);
                    }
                }
            }

            public void Railing()
            {
                string mat = Chance(r, 0.6f) ? HillMat.MetalDark : HillMat.MetalPaint;
                float ps = Mathf.Min(0.055f, T);
                var posts = PostXs(2f, ps);
                foreach (float x in posts)
                {
                    m.Box(mat, new Vector3(x - ps * 0.5f, 0f, -ps * 0.5f), new Vector3(x + ps * 0.5f, H - 0.05f, ps * 0.5f), 0.006f, Side.YNeg);
                    m.Ellipsoid(mat, new Vector3(x, H - 0.035f, 0f), new Vector3(ps * 0.55f, 0.035f, ps * 0.55f), 6);
                    m.Box(mat, new Vector3(x - ps, 0f, -ps), new Vector3(x + ps, 0.03f, ps), 0.006f, Side.YNeg);
                }
                float rw = Mathf.Min(0.045f, T);
                m.Box(mat, new Vector3(-L * 0.5f, H - 0.1f, -rw * 0.5f), new Vector3(L * 0.5f, H - 0.06f, rw * 0.5f), 0.008f);
                m.Box(mat, new Vector3(-L * 0.5f + ps, 0.09f, -0.012f), new Vector3(L * 0.5f - ps, 0.12f, 0.012f), 0.004f);
                for (float x = -L * 0.5f + ps + 0.06f; x < L * 0.5f - ps - 0.03f; x += 0.12f)
                    m.Box(mat, new Vector3(x - 0.008f, 0.12f, -0.008f), new Vector3(x + 0.008f, H - 0.1f, 0.008f), 0f, Side.YNeg | Side.YPos);
            }

            /// <summary>A brick or rendered garden wall: the body 3 cm in from the box, piers, a coping filling the box.</summary>
            public void Masonry(string body, string coping, bool brickWall)
            {
                float copeH = Mathf.Min(0.09f, H * 0.2f), inset = Mathf.Min(0.03f, T * 0.15f);
                m.Box(body, new Vector3(-L * 0.5f, 0f, -T * 0.5f + inset), new Vector3(L * 0.5f, H - copeH, T * 0.5f - inset), 0.01f, Side.YNeg | Side.YPos);
                if (!brickWall)
                    m.Box(HillMat.Plinth, new Vector3(-L * 0.5f, 0f, -T * 0.5f + inset * 0.4f), new Vector3(L * 0.5f, Mathf.Min(0.22f, H * 0.3f), T * 0.5f - inset * 0.4f), 0.008f, Side.YNeg);
                int piers = Mathf.Max(2, Mathf.RoundToInt(L / 2.8f) + 1);
                float pw = 0.34f;
                for (int i = 0; i < piers; i++)
                {
                    float x = Mathf.Lerp(-L * 0.5f + pw * 0.5f, L * 0.5f - pw * 0.5f, i / (float)(piers - 1));
                    m.Box(body, new Vector3(x - pw * 0.5f, 0f, -T * 0.5f), new Vector3(x + pw * 0.5f, H - copeH, T * 0.5f), 0.012f, Side.YNeg | Side.YPos);
                }
                m.Box(coping, new Vector3(-L * 0.5f, H - copeH, -T * 0.5f), new Vector3(L * 0.5f, H, T * 0.5f), 0.015f);
                if (Chance(r, 0.5f)) Stains(-T * 0.5f + inset, H - copeH);
            }

            public void DryStone()
            {
                float topH = Mathf.Min(0.18f, H * 0.3f);
                m.Box(HillMat.StoneWall, new Vector3(-L * 0.5f, 0f, -T * 0.5f + 0.03f), new Vector3(L * 0.5f, H - topH, T * 0.5f - 0.03f), Mathf.Min(0.05f, T * 0.2f), Side.YNeg);
                // Cock-and-hen coping: upright stones along the top, their heights varying.
                float x = -L * 0.5f;
                while (x < L * 0.5f - 0.02f)
                {
                    float w = Mathf.Min(R(r, 0.06f, 0.11f), L * 0.5f - x);
                    float h = topH * R(r, 0.75f, 1f);
                    float z = R(r, 0f, 0.02f);
                    m.Box(Chance(r, 0.5f) ? HillMat.StoneWall : HillMat.StoneCoping, new Vector3(x, H - topH - 0.01f, -T * 0.5f + 0.02f + z), new Vector3(x + w - 0.008f, H - topH + h, T * 0.5f - 0.02f - z), 0.01f, Side.YNeg);
                    x += w;
                }
                // Moss and grass at its foot.
                for (int k = 0; k < Mathf.RoundToInt(L * 1.5f); k++)
                    foreach (float side in new[] { -1f, 1f })
                        Tuft(m, HillMat.GrassCards, new Vector3(R(r, -0.5f, 0.5f) * L, 0f, side * (T * 0.5f + R(r, -0.01f, 0.06f))), R(r, 0.2f, 0.35f), R(r, 0.15f, 0.3f), R(r, 0f, 3.14f), HillAtlas.Tuft(r.Next(4)), 0.1f);
            }

            /// <summary>A tall retaining wall: battered faces (4 degrees) on both sides (the buried one costs two
            /// triangles), a coping filling the top, weep pipes with stains below them, a few ivy patches. Concrete
            /// ones are board-marked with an expansion joint every 6 m.</summary>
            public void Retaining(bool concrete)
            {
                string face = concrete ? HillMat.Concrete : HillMat.StoneWall;
                float copeH = concrete ? 0.12f : 0.16f;
                float yTop = H - copeH;
                float bat = Mathf.Min(yTop * Mathf.Tan(4f * Mathf.Deg2Rad), T * 0.3f);
                float hz = T * 0.5f;
                var fb0 = new Vector3(-L * 0.5f, 0f, -hz); var fb1 = new Vector3(L * 0.5f, 0f, -hz);
                var ft0 = new Vector3(-L * 0.5f, yTop, -hz + bat); var ft1 = new Vector3(L * 0.5f, yTop, -hz + bat);
                var bb0 = new Vector3(-L * 0.5f, 0f, hz); var bb1 = new Vector3(L * 0.5f, 0f, hz);
                var bt0 = new Vector3(-L * 0.5f, yTop, hz - bat); var bt1 = new Vector3(L * 0.5f, yTop, hz - bat);
                m.Quad(face, fb0, fb1, ft1, ft0, new Vector3(0f, bat, -yTop));
                m.Quad(face, bb0, bb1, bt1, bt0, new Vector3(0f, bat, yTop));
                m.Poly(face, new[] { fb0, ft0, bt0, bb0 }, Vector3.left);
                m.Poly(face, new[] { fb1, ft1, bt1, bb1 }, Vector3.right);
                // The coping: over the battered tops, its outer edges 4 cm proud of the faces, inside the box.
                m.Box(concrete ? HillMat.Concrete : HillMat.StoneCoping, new Vector3(-L * 0.5f, yTop, -hz + Mathf.Max(0f, bat - 0.04f)), new Vector3(L * 0.5f, H, hz - Mathf.Max(0f, bat - 0.04f)), concrete ? 0.02f : 0.03f);
                foreach (float side in new[] { -1f, 1f })
                {
                    // A frame on the face: x along, y up the face, z out.
                    var outward = new Vector3(0f, bat, yTop * side).normalized;
                    var upFace = new Vector3(0f, yTop, -bat * side).normalized;
                    float faceLen = Mathf.Sqrt(yTop * yTop + bat * bat);
                    m.Push(Frame(new Vector3(-L * 0.5f, 0f, hz * side), Vector3.right, upFace, outward));
                    try
                    {
                        if (concrete)
                        {
                            for (float y = 0.3f; y < faceLen - 0.1f; y += 0.3f)
                                m.Box(face, new Vector3(0.02f, y - 0.006f, 0f), new Vector3(L - 0.02f, y + 0.006f, 0.006f), 0f, Side.ZNeg);
                            for (float x = 6f; x < L - 0.5f; x += 6f)
                                m.Quad(HillMat.Rubber, new Vector3(x - 0.01f, 0.02f, 0.004f), new Vector3(x - 0.01f, faceLen, 0.004f), new Vector3(x + 0.01f, faceLen, 0.004f), new Vector3(x + 0.01f, 0.02f, 0.004f), Vector3.forward);
                        }
                        // Weep pipes along the bottom, each with a stain running down from it.
                        for (float x = R(r, 0.8f, 1.6f); x < L - 0.4f; x += R(r, 1.8f, 2.6f))
                        {
                            float y = Mathf.Min(0.45f, faceLen * 0.4f);
                            m.Cylinder(HillMat.Gutter, new Vector3(x, y, -0.02f), new Vector3(x, y, 0.035f), 0.045f, 8, false);
                            Disc(m, HillMat.Rubber, new Vector3(x, y, 0.03f), Vector3.forward, 0.036f, 8);
                            m.Rect(HillMat.Stains, new Vector3(x, y * 0.5f, 0.012f), Vector3.right * 0.35f, Vector3.up * (y + 0.1f), Vector3.forward, HillAtlas.Decal(r.Next(4)));
                        }
                        // Stains and cracks, and ivy hanging from the top on the stone ones.
                        int marks = Mathf.Max(1, Mathf.RoundToInt(L / 4f));
                        for (int i = 0; i < marks; i++)
                        {
                            float x = R(r, 0.6f, L - 0.6f), y = R(r, faceLen * 0.3f, faceLen * 0.8f), sz = R(r, 0.6f, 1.4f);
                            m.Rect(Chance(r, 0.5f) ? HillMat.Stains : HillMat.Cracks, new Vector3(x, y, 0.014f + i * 0.001f), Vector3.right * sz, Vector3.up * sz, Vector3.forward, HillAtlas.Decal(r.Next(4)));
                        }
                        if (!concrete || Chance(r, 0.3f))
                        {
                            int patches = Mathf.Max(1, Mathf.RoundToInt(L / 7f));
                            for (int i = 0; i < patches; i++)
                            {
                                float ih = Mathf.Min(faceLen * R(r, 0.4f, 0.8f), 3f);
                                Ivy(m, r, R(r, 1f, L - 1f), faceLen - ih * 0.5f, R(r, 1f, 2.2f), ih, 5f);
                            }
                        }
                    }
                    finally { m.Pop(); }
                }
                if (!concrete)
                    for (int k = 0; k < Mathf.RoundToInt(L * 1.2f); k++)
                        foreach (float side in new[] { -1f, 1f })
                            Tuft(m, HillMat.GrassCards, new Vector3(R(r, -0.5f, 0.5f) * L, 0f, side * (hz + R(r, -0.01f, 0.08f))), R(r, 0.25f, 0.4f), R(r, 0.2f, 0.4f), R(r, 0f, 3.14f), HillAtlas.Tuft(r.Next(4)), 0.1f);
            }

            void Stains(float zFace, float top)
            {
                int n = Mathf.Max(1, Mathf.RoundToInt(L / 3f));
                for (int i = 0; i < n; i++)
                {
                    float x = R(r, -L * 0.5f + 0.4f, L * 0.5f - 0.4f), sz = Mathf.Min(top * 0.8f, R(r, 0.4f, 0.8f));
                    m.Rect(HillMat.Stains, new Vector3(x, top - sz * 0.5f - 0.02f, zFace - 0.012f - i * 0.001f), Vector3.right * sz, Vector3.up * sz, Vector3.back, HillAtlas.Decal(r.Next(4)));
                }
            }

            /// <summary>A garden gate leaf filling the box: a frame with a Z brace, boards or pickets, strap hinges on
            /// the -x end and a latch on the +x end.</summary>
            public void Gate()
            {
                bool painted = Chance(r, 0.6f);
                string mat = painted ? HillMat.FencePaint : HillMat.FenceWood;
                float th = Mathf.Min(0.04f, T * 0.5f), z0 = -T * 0.5f + th;
                float x0 = -L * 0.5f, x1 = L * 0.5f, sw = 0.07f;
                m.Box(mat, new Vector3(x0, 0.05f, z0 - th), new Vector3(x0 + sw, H - 0.04f, z0), 0.006f);
                m.Box(mat, new Vector3(x1 - sw, 0.05f, z0 - th), new Vector3(x1, H - 0.04f, z0), 0.006f);
                foreach (float y in new[] { 0.18f, H - 0.3f })
                    m.Box(mat, new Vector3(x0 + sw, y - 0.045f, z0 - th), new Vector3(x1 - sw, y + 0.045f, z0), 0.005f);
                m.Beam(mat, new Vector3(x0 + sw + 0.04f, 0.22f, z0 - th * 0.5f), new Vector3(x1 - sw - 0.04f, H - 0.34f, z0 - th * 0.5f), th, 0.08f);
                // Pickets on the front.
                float pw = 0.075f, pitch = 0.11f;
                var outline = new Vector2[5];
                for (float x = x0 + 0.02f; x + pw <= x1 - 0.02f; x += pitch)
                {
                    float top = H - R(r, 0f, 0.01f);
                    outline[0] = new Vector2(x, 0.04f); outline[1] = new Vector2(x + pw, 0.04f); outline[2] = new Vector2(x + pw, top - 0.05f);
                    outline[3] = new Vector2(x + pw * 0.5f, top); outline[4] = new Vector2(x, top - 0.05f);
                    Extrude(m, mat, outline, Vector3.right, Vector3.up, Vector3.forward, z0, Mathf.Min(z0 + 0.022f, T * 0.5f));
                }
                // Strap hinges and the latch.
                foreach (float y in new[] { 0.18f, H - 0.3f })
                {
                    m.Box(HillMat.MetalDark, new Vector3(x0, y - 0.02f, z0 - th - 0.006f), new Vector3(x0 + 0.38f, y + 0.02f, z0 - th), 0.002f);
                    m.Cylinder(HillMat.MetalDark, new Vector3(x0 + 0.01f, y - 0.06f, z0 - th - 0.01f), new Vector3(x0 + 0.01f, y + 0.06f, z0 - th - 0.01f), 0.012f, 6, true);
                }
                m.Box(HillMat.MetalDark, new Vector3(x1 - 0.2f, H * 0.62f - 0.015f, z0 - th - 0.008f), new Vector3(x1, H * 0.62f + 0.015f, z0 - th), 0.003f);
                m.Box(HillMat.MetalDark, new Vector3(x1 - 0.14f, H * 0.62f - 0.06f, z0 - th - 0.02f), new Vector3(x1 - 0.11f, H * 0.62f + 0.03f, z0 - th - 0.008f), 0.003f);
            }
        }
    }
}

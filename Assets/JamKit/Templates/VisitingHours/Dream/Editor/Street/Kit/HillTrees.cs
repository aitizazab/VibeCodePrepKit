using System.Collections.Generic;
using JamKit.DeepBlock;
using UnityEngine;
using static JamKit.DreamEscape.EditorTools.HillKit;
using Random = System.Random;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// The hill's trees, bushes, hedges, grass and flowers: stylized forms that read as trees from 5 m and from 150 m.
    /// A tree is a tapered bark trunk with a root flare (inside <see cref="HillColliders.Trunk"/>'s box up to its top),
    /// main branches forking toward the canopy's clumps, and each clump a lumpy dark mass covered in leaf-cluster cards
    /// whose normals bend outward from the canopy's centre (soft, painterly shading).
    /// </summary>
    public static class HillTrees
    {
        /// <summary>Draws a tree (kind 0 broadleaf, 1 tall, 2 small blossom, 3 conifer, 4 the big oak) in world space.
        /// The big oak's climbable limbs are drawn separately by <see cref="DrawLimb"/>.</summary>
        public static void Draw(HillMesh m, HillTree t)
        {
            if (t == null) return;
            var r = new Random(t.seed * 4567 + 89 + t.kind * 7);
            var foot = U(t.foot);
            float H = Mathf.Max(2f, t.height), C = Mathf.Max(0.6f, t.canopy), tr = Mathf.Max(0.05f, t.trunk);
            if (t.kind == 3) { Conifer(m, r, foot, H, C, tr); return; }
            float crownBase, cy, ry;
            int branches, clumps, cards;
            string leaf = HillMat.Leaves;
            switch (t.kind)
            {
                case 1: crownBase = 0.24f; cy = H * 0.6f; ry = H * 0.38f; branches = 6; clumps = 9; cards = 30; C *= 0.75f; break;
                case 2: crownBase = 0.38f; cy = H - C * 0.62f; ry = C * 0.62f; branches = 4; clumps = 6; cards = 30; leaf = HillMat.LeavesBlossom; break;
                case 4: crownBase = 0.32f; cy = H - C * 0.72f; ry = C * 0.72f; branches = 7; clumps = 12; cards = 44; break;
                default: crownBase = 0.36f; cy = H - C * 0.8f; ry = C * 0.8f; branches = 5; clumps = 8; cards = 34; break;
            }
            branches += r.Next(-1, 2);
            clumps += r.Next(-1, 2);
            var cc = foot + Vector3.up * cy;
            var radii = new Vector3(C * R(r, 0.9f, 1.1f), ry, C * R(r, 0.9f, 1.1f));
            // The trunk: straight and inside its collider (radius <= trunk) up to 55 % of the height, then free.
            float colTop = H * 0.55f, forkY = Mathf.Min(H * crownBase, cy - ry * 0.3f);
            var lean = new Vector3(R(r, -1f, 1f), 0f, R(r, -1f, 1f)) * (tr * 0.6f);
            var trunk = new List<Vector3>();
            var trunkR = new List<float>();
            trunk.Add(foot + Vector3.down * 0.3f); trunkR.Add(tr * 1.1f);
            trunk.Add(foot + Vector3.up * 0.05f); trunkR.Add(tr * 1.04f);
            trunk.Add(foot + Vector3.up * 0.45f); trunkR.Add(tr * 0.95f);
            trunk.Add(foot + Vector3.up * Mathf.Min(forkY, colTop) * 0.6f); trunkR.Add(tr * 0.88f);
            trunk.Add(foot + Vector3.up * Mathf.Min(forkY, colTop)); trunkR.Add(tr * 0.82f);
            if (forkY > colTop + 0.3f) { trunk.Add(foot + Vector3.up * forkY + lean * 0.5f); trunkR.Add(tr * 0.75f); }
            var top = cc + lean + Vector3.up * ry * 0.35f;
            trunk.Add(Vector3.Lerp(trunk[trunk.Count - 1], top, 0.5f) + lean * 0.3f); trunkR.Add(tr * 0.5f);
            trunk.Add(top); trunkR.Add(tr * 0.16f);
            m.Tube(HillMat.Bark, trunk, trunkR, t.kind == 4 ? 10 : 8, false, 1.6f);
            Roots(m, r, foot, tr, t.kind == 4 ? 6 : 4);
            // The clumps of the canopy.
            var centres = new List<Vector3>();
            var sizes = new List<float>();
            for (int i = 0; i < clumps; i++)
            {
                var d = OnSphere(r);
                d.y = d.y * 0.75f + 0.2f;
                if (t.kind == 1) d.y = (i / (float)(clumps - 1)) * 1.6f - 0.75f + R(r, -0.1f, 0.1f);
                var p = cc + Vector3.Scale(d.normalized * R(r, 0.45f, 0.7f), radii);
                float s = (t.kind == 1 ? C * 0.65f : C * 0.55f) * R(r, 0.75f, 1.1f);
                centres.Add(p);
                sizes.Add(s);
            }
            // A central clump fills the middle.
            centres.Add(cc + Vector3.up * ry * 0.1f);
            sizes.Add(Mathf.Min(radii.x, ry) * 0.75f);
            // Main branches from the fork to the clumps (the rest branch off them).
            var trunkTopBase = foot + Vector3.up * forkY + lean * 0.5f;
            var mains = new List<Vector3[]>();
            for (int i = 0; i < clumps; i++)
            {
                Vector3 start;
                float r0;
                if (i < branches)
                {
                    start = trunkTopBase + Vector3.up * R(r, 0f, Mathf.Max(0.2f, (top.y - trunkTopBase.y) * 0.5f));
                    r0 = tr * R(r, 0.42f, 0.55f);
                }
                else
                {
                    var host = mains[r.Next(mains.Count)];
                    start = Vector3.Lerp(host[1], host[2], R(r, 0.2f, 0.7f));
                    r0 = tr * 0.18f;
                }
                var end = centres[i];
                var dir = end - start;
                var mid1 = start + new Vector3(dir.x * 0.35f, dir.y * 0.15f + dir.magnitude * 0.15f, dir.z * 0.35f);
                var mid2 = start + new Vector3(dir.x * 0.75f, dir.y * 0.7f, dir.z * 0.75f) + OnSphere(r) * 0.15f * dir.magnitude;
                var path = new[] { start, mid1, mid2, end };
                if (i < branches) mains.Add(path);
                m.Tube(HillMat.Bark, path, new[] { r0, r0 * 0.72f, r0 * 0.45f, r0 * 0.18f }, i < branches ? 6 : 5, false, 1.2f);
            }
            if (mains.Count == 0) mains.Add(new[] { trunkTopBase, trunkTopBase, top, top });
            // Twigs that break the silhouette between clumps.
            int twigs = t.kind == 4 ? 10 : 6;
            for (int i = 0; i < twigs; i++)
            {
                var host = mains[r.Next(mains.Count)];
                var s0 = Vector3.Lerp(host[1], host[2], R(r, 0.3f, 0.9f));
                var outDir = (s0 - cc);
                outDir.y = Mathf.Abs(outDir.y) * 0.3f + 0.4f;
                outDir = (outDir.normalized + OnSphere(r) * 0.3f).normalized;
                float len = C * R(r, 0.6f, 1.0f);
                var e = s0 + outDir * len;
                m.Tube(HillMat.Bark, new[] { s0, s0 + outDir * len * 0.5f + Vector3.up * 0.15f, e }, new[] { tr * 0.1f, tr * 0.06f, 0.012f }, 4, false, 1f);
                Clump(m, r, e, new Vector3(0.45f, 0.4f, 0.45f) * C * 0.35f, cc, 5, Mathf.Clamp(C * 0.35f, 0.5f, 1.3f), leaf, false);
            }
            float cardSize = Mathf.Clamp(C * (t.kind == 2 ? 0.42f : 0.46f), 0.7f, 2.2f);
            for (int i = 0; i < centres.Count; i++)
            {
                float s = sizes[i];
                Clump(m, r, centres[i], new Vector3(s, s * 0.82f, s), cc, i == centres.Count - 1 ? cards / 2 : cards, cardSize * R(r, 0.85f, 1.1f), leaf, true);
            }
        }

        /// <summary>A canopy clump: a lumpy dark mass and leaf cards over its surface, facing out, normals bent out
        /// from the canopy centre <paramref name="canopyCentre"/>.</summary>
        static void Clump(HillMesh m, Random r, Vector3 c, Vector3 radii, Vector3 canopyCentre, int cards, float cardSize, string leaf, bool mass)
        {
            if (mass)
            {
                float seed = R(r, 0f, 100f);
                m.Ellipsoid(HillMat.LeavesDark, c, radii * 0.78f, 10, u => 1f + 0.16f * Noise(u * 2.2f, seed));
            }
            for (int k = 0; k < cards; k++)
            {
                var d = OnSphere(r);
                d.y = d.y * 0.8f + 0.18f;
                d.Normalize();
                var p = c + Vector3.Scale(d, radii) * R(r, 0.78f, 1.02f);
                var fromCanopy = (p - canopyCentre).normalized;
                var facing = (d + fromCanopy * 0.6f + OnSphere(r) * 0.35f).normalized;
                var normal = (fromCanopy * 0.75f + Vector3.up * 0.35f + d * 0.2f).normalized;
                string mat = leaf == HillMat.LeavesBlossom && Chance(r, 0.25f) ? HillMat.Leaves : leaf;
                LeafCard(m, mat, p, facing, cardSize * R(r, 0.7f, 1.15f), R(r, 0f, 6.283f), HillAtlas.Leaf(r.Next(4)), normal);
            }
        }

        static void Roots(HillMesh m, Random r, Vector3 foot, float tr, int n)
        {
            float a0 = R(r, 0f, 6.283f);
            for (int i = 0; i < n; i++)
            {
                float a = a0 + i * 6.283f / n + R(r, -0.3f, 0.3f);
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var path = new[]
                {
                    foot + d * tr * 0.5f + Vector3.up * 0.4f,
                    foot + d * (tr * 0.95f) + Vector3.up * 0.12f,
                    foot + d * (tr + 0.25f) + Vector3.up * 0.02f,
                    foot + d * (tr + R(r, 0.5f, 0.9f)) + Vector3.down * 0.05f,
                };
                m.Tube(HillMat.Bark, path, new[] { tr * 0.38f, Mathf.Min(tr * 0.25f, 0.1f), 0.05f, 0.015f }, 5, false, 1f);
            }
        }

        static void Conifer(HillMesh m, Random r, Vector3 foot, float H, float C, float tr)
        {
            var path = new[] { foot + Vector3.down * 0.3f, foot + Vector3.up * 0.05f, foot + Vector3.up * 0.5f, foot + Vector3.up * H * 0.5f, foot + Vector3.up * H };
            m.Tube(HillMat.Bark, path, new[] { tr * 1.08f, tr * 1.02f, tr * 0.92f, tr * 0.55f, 0.03f }, 7, false, 1.6f);
            Roots(m, r, foot, tr, 3);
            float y0 = Mathf.Max(H * 0.14f, Mathf.Min(1.8f, H * 0.55f));
            int tiers = Mathf.Clamp(Mathf.RoundToInt((H - y0) / 1.1f), 4, 10);
            float tierH = (H - y0) / tiers;
            // The dark core: a stack of cones (opaque hedge foliage, no holes).
            var core = new List<Vector3>();
            var coreR = new List<float>();
            for (int i = 0; i < tiers; i++)
            {
                float y = y0 + i * tierH;
                float f = 1f - (y - y0) / (H - y0);
                float rad = C * Mathf.Pow(f, 0.9f) * 0.85f + 0.12f;
                core.Add(foot + Vector3.up * (y - tierH * 0.15f)); coreR.Add(rad * 0.25f);
                core.Add(foot + Vector3.up * y); coreR.Add(rad);
            }
            core.Add(foot + Vector3.up * (H + 0.2f)); coreR.Add(0.02f);
            m.Tube(HillMat.Hedge, core, coreR, 8, false, 1.5f);
            // Branch layers of cards: lying outward and drooping, then upright ones round the rim for the silhouette.
            for (int i = 0; i < tiers; i++)
            {
                float y = y0 + i * tierH;
                float f = 1f - (y - y0) / (H - y0);
                float rad = C * Mathf.Pow(f, 0.9f) + 0.25f;
                int n = Mathf.Clamp(Mathf.RoundToInt(rad * 7f), 5, 16);
                float a0 = R(r, 0f, 6.283f);
                for (int k = 0; k < n; k++)
                {
                    float a = a0 + k * 6.283f / n + R(r, -0.2f, 0.2f);
                    var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    var p = foot + Vector3.up * (y - 0.05f) + d * rad * R(r, 0.45f, 0.65f);
                    var facing = (d * 0.45f + Vector3.up * 0.9f).normalized;
                    var normal = (d * 0.7f + Vector3.up * 0.55f).normalized;
                    LeafCard(m, HillMat.LeavesDark, p, facing, rad * R(r, 0.8f, 1.05f), R(r, 0f, 6.283f), HillAtlas.Leaf(r.Next(4)), normal);
                    if (k % 2 == 0)
                    {
                        var q = foot + Vector3.up * (y - tierH * 0.25f) + d * rad * R(r, 0.75f, 0.95f);
                        LeafCard(m, HillMat.Leaves, q, (d + Vector3.up * 0.25f).normalized, rad * R(r, 0.55f, 0.75f), R(r, 0f, 6.283f), HillAtlas.Leaf(r.Next(4)), normal);
                    }
                }
            }
            LeafCard(m, HillMat.LeavesDark, foot + Vector3.up * (H - 0.25f), Vector3.right, 0.7f, 0f, HillAtlas.Leaf(r.Next(4)), Vector3.up);
            LeafCard(m, HillMat.LeavesDark, foot + Vector3.up * (H - 0.25f), Vector3.forward, 0.7f, 0f, HillAtlas.Leaf(r.Next(4)), Vector3.up);
        }

        /// <summary>
        /// One of the big oak's climbable limbs: a thick bark branch from the trunk's axis to the box's far end, its
        /// walkable top flush with the box's top (a tube flattened a little), small side branches and leafy ends; the tip
        /// droops away below the walkable top so it never blocks the way off the end.
        /// </summary>
        public static void DrawLimb(HillMesh m, BlockPiece limb, Vector3 trunkAxisFoot)
        {
            if (limb == null) return;
            var c = U(limb.center);
            var s = U(limb.size);
            var mn = c - s * 0.5f;
            var mx = c + s * 0.5f;
            var r = new Random(Mathf.RoundToInt(c.x * 37f + c.z * 101f + c.y * 13f));
            bool alongX = s.x >= s.z;
            float top = mx.y;
            float nearEnd, farEnd;
            if (alongX)
            {
                bool minNear = Mathf.Abs(mn.x - trunkAxisFoot.x) < Mathf.Abs(mx.x - trunkAxisFoot.x);
                nearEnd = minNear ? mn.x : mx.x; farEnd = minNear ? mx.x : mn.x;
            }
            else
            {
                bool minNear = Mathf.Abs(mn.z - trunkAxisFoot.z) < Mathf.Abs(mx.z - trunkAxisFoot.z);
                nearEnd = minNear ? mn.z : mx.z; farEnd = minNear ? mx.z : mn.z;
            }
            float sign = Mathf.Sign(farEnd - nearEnd);
            float yaw = alongX ? (sign > 0f ? 0f : 180f) : (sign > 0f ? -90f : 90f);
            float width = alongX ? s.z : s.x;
            float rad = Mathf.Min(width * 0.5f, s.y * 0.6f);
            // Local frame: x along the limb (from the trunk's axis outward), y up from the walkable top, z across.
            float axisAlong = alongX ? trunkAxisFoot.x : trunkAxisFoot.z;
            float startX = (axisAlong - nearEnd) * sign;   // the axis in the limb's own x (<= 0)
            float len = Mathf.Abs(farEnd - nearEnd);
            var origin = alongX ? new Vector3(nearEnd, top, c.z) : new Vector3(c.x, top, nearEnd);
            const float flat = 0.82f;
            m.Push(Matrix4x4.TRS(origin, Quaternion.Euler(0f, yaw, 0f), new Vector3(1f, flat, 1f)));
            try
            {
                float rRoot = Mathf.Max(rad * 1.25f, Mathf.Min(rad * 1.5f, 0.55f));
                var xs = new[] { Mathf.Min(startX, -0.05f), 0f, len * 0.35f, len * 0.7f, len };
                var rs = new[] { rRoot, rad, rad * 0.92f, rad * 0.82f, rad * 0.72f };
                var path = new Vector3[xs.Length];
                for (int i = 0; i < xs.Length; i++) path[i] = new Vector3(xs[i], -rs[i], R(r, -0.02f, 0.02f) * (i > 0 && i < xs.Length - 1 ? 1f : 0f));
                m.Tube(HillMat.Bark, path, rs, 10, false, 1.4f);
            }
            finally { m.Pop(); }
            // The rest is in world space through the same (unscaled) frame.
            m.Push(Matrix4x4.TRS(origin, Quaternion.Euler(0f, yaw, 0f), Vector3.one));
            try
            {
                float endY = -rad * 0.72f * flat;
                var tip0 = new Vector3(len - 0.05f, endY, 0f);
                var tip1 = new Vector3(len + 0.8f, endY - 0.35f, R(r, -0.3f, 0.3f));
                var tip2 = new Vector3(len + 1.6f, endY - 0.45f + R(r, -0.2f, 0.3f), R(r, -0.6f, 0.6f));
                m.Tube(HillMat.Bark, new[] { tip0, tip1, tip2 }, new[] { rad * 0.6f, rad * 0.35f, 0.04f }, 6, false, 1f);
                var leafCentre = tip2 + Vector3.up * 0.4f;
                Clump(m, r, leafCentre, new Vector3(1.1f, 0.8f, 1.1f), leafCentre - Vector3.up * 0.5f, 22, 1.1f, HillMat.Leaves, true);
                // Side branches: out sideways first, then up, clear of the walking line.
                int sides = 2 + r.Next(2);
                for (int i = 0; i < sides; i++)
                {
                    float x = len * R(r, 0.25f, 0.85f);
                    float z = (i % 2 == 0 ? 1f : -1f) * (rad * 0.9f);
                    float y = -rad * flat * 0.9f;
                    var a = new Vector3(x, y, z);
                    var b = new Vector3(x + R(r, 0.1f, 0.4f), y + 0.05f, z * 2.6f + Mathf.Sign(z) * 0.6f);
                    var e = new Vector3(x + R(r, 0.3f, 0.8f), y + R(r, 1.2f, 2.0f), z * 3f + Mathf.Sign(z) * 1.0f);
                    m.Tube(HillMat.Bark, new[] { a, b, e }, new[] { Mathf.Min(0.09f, rad * 0.3f), 0.05f, 0.015f }, 5, false, 1f);
                    Clump(m, r, e, new Vector3(0.8f, 0.6f, 0.8f), e - Vector3.up * 0.6f, 14, 0.9f, HillMat.Leaves, false);
                }
            }
            finally { m.Pop(); }
        }

        /// <summary>A bush: a lumpy hedge-foliage mass with leaf cards over it, standing on <paramref name="foot"/>,
        /// about <paramref name="size"/> (its collider box when it's a prop).</summary>
        public static void DrawBush(HillMesh m, Vector3 foot, Vector3 size, int seed)
        {
            var r = new Random(seed * 131 + 7);
            size = new Vector3(Mathf.Max(0.2f, size.x), Mathf.Max(0.2f, size.y), Mathf.Max(0.2f, size.z));
            var half = size * 0.5f;
            float s0 = R(r, 0f, 100f);
            m.Ellipsoid(HillMat.Hedge, foot + Vector3.up * (half.y * 0.92f), half * 0.86f, 10, u => 1f + 0.14f * Noise(u * 2.5f, s0));
            int lumps = size.x * size.z > 1.2f ? 2 + r.Next(2) : 1;
            for (int i = 0; i < lumps; i++)
            {
                var off = new Vector3(R(r, -0.35f, 0.35f) * half.x, R(r, 0.1f, 0.25f) * half.y, R(r, -0.35f, 0.35f) * half.z);
                m.Ellipsoid(HillMat.Hedge, foot + Vector3.up * half.y + off, half * R(r, 0.45f, 0.6f), 8, u => 1f + 0.18f * Noise(u * 3f, s0 + i));
            }
            bool flowering = r.Next(3) == 0;
            int cards = Mathf.Clamp(Mathf.RoundToInt((size.x * size.z + size.y * (size.x + size.z)) * 9f), 12, 60);
            var centre = foot + Vector3.up * (half.y * 0.85f);
            float cs = Mathf.Clamp(Mathf.Min(size.x, size.z) * 0.42f, 0.3f, 0.9f);
            for (int k = 0; k < cards; k++)
            {
                var d = OnSphere(r);
                d.y = Mathf.Abs(d.y) * 0.9f - 0.1f;
                d.Normalize();
                var p = centre + Vector3.Scale(d, half) * R(r, 0.82f, 1.0f);
                if (p.y < foot.y + 0.08f) p.y = foot.y + 0.08f;
                var facing = (d + OnSphere(r) * 0.3f).normalized;
                var normal = (d * 0.75f + Vector3.up * 0.35f).normalized;
                string mat = flowering && Chance(r, 0.3f) ? HillMat.LeavesBlossom : Chance(r, 0.3f) ? HillMat.LeavesDark : HillMat.Leaves;
                LeafCard(m, mat, p, facing, cs * R(r, 0.75f, 1.2f), R(r, 0f, 6.283f), HillAtlas.Leaf(r.Next(4)), normal);
            }
        }

        /// <summary>A clipped hedge filling its box: a rounded lumpy mass with leaf cards over its top and sides (within a
        /// few centimetres of the box) so its silhouette isn't a box.</summary>
        public static void DrawHedge(HillMesh m, BlockPiece p)
        {
            if (p == null) return;
            var c = U(p.center);
            var s = U(p.size);
            var mn = c - s * 0.5f;
            var mx = c + s * 0.5f;
            var r = new Random(Mathf.RoundToInt(c.x * 17f + c.z * 53f));
            bool alongX = s.x >= s.z;
            float thick = Mathf.Min(s.x, s.z), len = Mathf.Max(s.x, s.z);
            float bevel = Mathf.Min(0.16f, thick * 0.3f);
            m.Box(HillMat.Hedge, mn + new Vector3(0.04f, 0f, 0.04f), mx - new Vector3(0.04f, 0.04f, 0.04f), bevel, HillMesh.Side.YNeg);
            Vector3 Along(float a, float across, float y) => alongX ? new Vector3(mn.x + a, y, c.z + across) : new Vector3(c.x + across, y, mn.z + a);
            Vector3 Out(float sign) => alongX ? new Vector3(0f, 0f, sign) : new Vector3(sign, 0f, 0f);
            var AlongDir = alongX ? Vector3.right : Vector3.forward;
            // Lumps along the top.
            float s0 = R(r, 0f, 100f);
            int lumps = Mathf.Max(1, Mathf.RoundToInt(len / 0.75f));
            for (int i = 0; i < lumps; i++)
            {
                float a = (i + 0.5f) / lumps * len + R(r, -0.15f, 0.15f);
                var lc = Along(a, R(r, -0.08f, 0.08f) * thick, mx.y - 0.1f);
                var lr = alongX ? new Vector3(R(r, 0.4f, 0.55f), 0.13f, thick * 0.42f) : new Vector3(thick * 0.42f, 0.13f, R(r, 0.4f, 0.55f));
                m.Ellipsoid(HillMat.Hedge, lc, lr, 6, u => 1f + 0.15f * Noise(u * 3f, s0 + i));
            }
            // Cards: sides, then the top.
            float H = s.y;
            int perSide = Mathf.Clamp(Mathf.RoundToInt(len * (H - 0.1f) * 3f), 4, 160);
            foreach (float sign in new[] { -1f, 1f })
                for (int k = 0; k < perSide; k++)
                {
                    float a = R(r, 0.05f, len - 0.05f), y = R(r, mn.y + 0.12f, mx.y - 0.05f);
                    var o = Out(sign);
                    var pt = Along(a, sign * (thick * 0.5f - R(r, 0.02f, 0.06f)), y);
                    float up = (y - mn.y) / Mathf.Max(0.1f, H);
                    var facing = (o + Vector3.up * (up * 0.5f) + OnSphere(r) * 0.3f).normalized;
                    LeafCard(m, Chance(r, 0.35f) ? HillMat.LeavesDark : HillMat.Leaves, pt, facing, R(r, 0.32f, 0.5f), R(r, 0f, 6.283f), HillAtlas.Leaf(r.Next(4)),
                        (o * 0.7f + Vector3.up * 0.45f).normalized);
                }
            foreach (float sign in new[] { -1f, 1f })
                for (int k = 0; k < Mathf.Max(2, Mathf.RoundToInt(thick * H * 3f)); k++)
                {
                    var pt = Along(sign < 0f ? R(r, 0.02f, 0.06f) : len - R(r, 0.02f, 0.06f), R(r, -0.4f, 0.4f) * thick, R(r, mn.y + 0.12f, mx.y - 0.05f));
                    LeafCard(m, HillMat.Leaves, pt, (AlongDir * sign + OnSphere(r) * 0.3f).normalized, R(r, 0.32f, 0.5f), R(r, 0f, 6.283f), HillAtlas.Leaf(r.Next(4)), (AlongDir * sign * 0.7f + Vector3.up * 0.45f).normalized);
                }
            int onTop = Mathf.Clamp(Mathf.RoundToInt(len * thick * 5f), 3, 120);
            for (int k = 0; k < onTop; k++)
            {
                var pt = Along(R(r, 0.05f, len - 0.05f), R(r, -0.45f, 0.45f) * thick, mx.y - R(r, 0.0f, 0.05f));
                var facing = (Vector3.up + OnSphere(r) * 0.45f).normalized;
                LeafCard(m, Chance(r, 0.25f) ? HillMat.LeavesDark : HillMat.Leaves, pt, facing, R(r, 0.35f, 0.55f), R(r, 0f, 6.283f), HillAtlas.Leaf(r.Next(4)), Vector3.up);
            }
            // A fringe of grass at its foot.
            foreach (float sign in new[] { -1f, 1f })
                for (int k = 0; k < Mathf.RoundToInt(len * 2f); k++)
                    Tuft(m, HillMat.GrassCards, Along(R(r, 0f, len), sign * (thick * 0.5f + R(r, -0.02f, 0.08f)), mn.y), R(r, 0.25f, 0.4f), R(r, 0.18f, 0.32f), R(r, 0f, 3.14f), HillAtlas.Tuft(r.Next(4)), 0.1f);
        }

        /// <summary>Grass tufts (crossed pairs of cards) over a rectangle <paramref name="area"/> (x by z) centred on
        /// <paramref name="center"/>, rooted at its height; <paramref name="density"/> tufts per square metre.</summary>
        public static void DrawGrass(HillMesh m, Vector3 center, Vector2 area, float density, int seed)
        {
            var r = new Random(seed * 977 + 3);
            int n = Mathf.Clamp(Mathf.RoundToInt(area.x * area.y * density), 0, 4000);
            for (int i = 0; i < n; i++)
            {
                var p = center + new Vector3(R(r, -0.5f, 0.5f) * area.x, 0f, R(r, -0.5f, 0.5f) * area.y);
                Tuft(m, HillMat.GrassCards, p, R(r, 0.25f, 0.45f), R(r, 0.2f, 0.45f), R(r, 0f, 3.14f), HillAtlas.Tuft(r.Next(4)), R(r, -0.15f, 0.15f));
            }
        }

        /// <summary>Flowers (crossed cards) over a rectangle, like <see cref="DrawGrass"/>.</summary>
        public static void DrawFlowers(HillMesh m, Vector3 center, Vector2 area, float density, int seed)
        {
            var r = new Random(seed * 613 + 11);
            int n = Mathf.Clamp(Mathf.RoundToInt(area.x * area.y * density), 0, 3000);
            for (int i = 0; i < n; i++)
            {
                var p = center + new Vector3(R(r, -0.5f, 0.5f) * area.x, 0f, R(r, -0.5f, 0.5f) * area.y);
                Tuft(m, HillMat.Flowers, p, R(r, 0.2f, 0.32f), R(r, 0.2f, 0.38f), R(r, 0f, 3.14f), HillAtlas.Tuft(r.Next(4)), R(r, -0.1f, 0.1f));
            }
        }
    }
}

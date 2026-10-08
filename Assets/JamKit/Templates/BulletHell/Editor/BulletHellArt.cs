using System;
using JamKit.EditorTools;
using UnityEngine;

namespace JamKit.BulletHell.EditorTools
{
    /// <summary>
    /// Placeholder art for the bullet-hell template, drawn from signed distance functions: the ship,
    /// enemy and boss silhouettes (white, tinted by their sprite colour, with a darker rim so they read
    /// on any background), the hitbox dot, rings and the bullet texture (red channel = bright core).
    /// Replace any PNG in Templates/BulletHell/Art/Generated with real art of the same name, or point
    /// the data assets at new sprites.
    /// </summary>
    public static class BulletHellArt
    {
        public const string Dir = BulletHellBuilder.Root + "/Art/Generated";
        const int Size = 128;

        public sealed class Set
        {
            public Sprite ship, hitbox, ring, aura, bullet, ringFx;
            public Sprite orb, triangle, square, diamond, hexagon, star, wide, burst;
            public Sprite bossGate, bossForge, bossLight;
            public Sprite howMove, howDodge, howBomb, howRun;
        }

        public static Set MakeAll()
        {
            var s = new Set();
            s.ship = Shape("bh_ship", (x, y) => Polygon(x, y, new[] { 0f, 0.95f, 0.72f, -0.78f, 0f, -0.38f, -0.72f, -0.78f }));
            s.hitbox = Make("bh_hitbox", Size, (x, y) =>
            {
                float d = Len(x, y);
                float body = Cov((d - 0.62f) * Size * 0.5f);
                float rim = Cov((d - 0.9f) * Size * 0.5f) - body;
                return new Color(1f, 1f, 1f, 1f) * body + new Color(0.9f, 0.1f, 0.2f, 1f) * rim;
            });
            s.ring = Make("bh_ring", Size, (x, y) =>
            {
                float d = Mathf.Abs(Len(x, y) - 0.95f) - 0.035f;
                return new Color(1f, 1f, 1f, Cov(d * Size * 0.5f));
            });
            s.aura = Make("bh_aura", Size, (x, y) =>
            {
                float d = Len(x, y);
                float a = Mathf.Clamp01(1f - d);
                return new Color(1f, 1f, 1f, a * a * 0.8f);
            });
            // Bullet texture: alpha = disc, red = how much of the white core shows.
            s.bullet = Make("bh_bullet", 64, (x, y) =>
            {
                float d = Len(x, y);
                float a = Cov((d - 0.94f) * 32f);
                float core = 1f - Mathf.SmoothStep(0.25f, 0.62f, d);
                return new Color(core, core, core, a);
            });
            s.ringFx = Make("bh_ringfx", Size, (x, y) =>
            {
                float d = Len(x, y);
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.9f) / 0.1f) * Cov((d - 1f) * Size * 0.5f);
                return new Color(0f, 0f, 0f, a);
            });

            s.orb = Shape("bh_orb", (x, y) => Mathf.Min(Len(x, y) - 0.9f, Mathf.Max(Len(x, y) - 0.9f, -(Mathf.Abs(Len(x, y) - 0.45f) - 0.1f))));
            s.triangle = Shape("bh_triangle", (x, y) => Polygon(x, y, new[] { 0f, -0.95f, 0.85f, 0.7f, -0.85f, 0.7f }));
            s.square = Shape("bh_square", (x, y) => RoundBox(x, y, 0.82f, 0.82f, 0.2f));
            s.diamond = Shape("bh_diamond", (x, y) => Polygon(x, y, new[] { 0f, 0.95f, 0.8f, 0f, 0f, -0.95f, -0.8f, 0f }));
            s.hexagon = Shape("bh_hexagon", (x, y) => Regular(x, y, 6, 0.92f, 0f));
            s.star = Shape("bh_star", (x, y) => Star(x, y, 5, 0.95f, 0.45f, 90f));
            s.wide = Shape("bh_wide", (x, y) => Mathf.Min(RoundBox(x, y, 0.95f, 0.42f, 0.2f), Len(x, y + 0.1f) - 0.5f));
            s.burst = Shape("bh_burst", (x, y) => Star(x, y, 8, 0.95f, 0.6f, 0f));
            s.bossGate = Shape("bh_boss_gate", (x, y) => Mathf.Max(Regular(x, y, 8, 0.95f, 22.5f), -(Regular(x, y, 6, 0.42f, 0f))), 256);
            s.bossForge = Shape("bh_boss_forge", (x, y) => Mathf.Min(Star(x, y, 8, 0.97f, 0.62f, 0f), Len(x, y) - 0.55f), 256);
            s.bossLight = Shape("bh_boss_light", (x, y) => Mathf.Min(Star(x, y, 12, 0.97f, 0.7f, 0f), Mathf.Max(Len(x, y) - 0.62f, -(Len(x, y) - 0.3f))), 256);

            s.howMove = HowTo("bh_howto_move", (x, y) => Polygon(x, y, new[] { 0f, 0.95f, 0.72f, -0.78f, 0f, -0.38f, -0.72f, -0.78f }), new Color(0.55f, 0.95f, 1f));
            s.howDodge = HowTo("bh_howto_dodge", (x, y) => Mathf.Min(Mathf.Abs(Len(x, y) - 0.8f) - 0.05f, Len(x, y) - 0.18f), new Color(1f, 0.4f, 0.5f));
            s.howBomb = HowTo("bh_howto_bomb", (x, y) => Mathf.Min(Mathf.Abs(Len(x, y) - 0.85f) - 0.08f, Mathf.Abs(Len(x, y) - 0.5f) - 0.06f), new Color(1f, 0.85f, 0.4f));
            s.howRun = HowTo("bh_howto_run", (x, y) => Mathf.Min(Mathf.Min(RoundBox(x + 0.6f, y, 0.22f, 0.22f, 0.06f), RoundBox(x, y, 0.22f, 0.22f, 0.06f)), RoundBox(x - 0.6f, y, 0.22f, 0.22f, 0.06f)), new Color(0.8f, 0.55f, 1f));
            return s;
        }

        // ---------- Rendering ----------

        delegate Color Pixel(float x, float y);

        /// <summary>A white silhouette with a soft inner shade and a darker rim, from an SDF in [-1, 1] space.</summary>
        static Sprite Shape(string name, Func<float, float, float> sdf, int size = Size)
        {
            float px = 2f / size;
            return Make(name, size, (x, y) =>
            {
                float d = sdf(x, y);
                float a = Cov(d / px);
                if (a <= 0f) return new Color(1f, 1f, 1f, 0f);
                float rim = Mathf.Clamp01(1f + d / (px * 5f)); // 1 at the edge, 0 deeper in
                float shade = 1f - 0.25f * Len(x, y);
                float v = Mathf.Lerp(shade, 0.35f, rim * 0.9f);
                return new Color(v, v, v, a);
            });
        }

        static Sprite HowTo(string name, Func<float, float, float> sdf, Color color)
        {
            float px = 2f / Size;
            return Make(name, Size, (x, y) =>
            {
                float a = Cov(sdf(x * 1.15f, y * 1.15f) / px);
                return new Color(color.r, color.g, color.b, a);
            });
        }

        static Sprite Make(string name, int size, Pixel f)
        {
            var px = new Color[size * size];
            for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                float x = (i + 0.5f) / size * 2f - 1f;
                float y = (j + 0.5f) / size * 2f - 1f;
                px[j * size + i] = f(x, y);
            }
            return SceneBuilder.MakeSprite(name, size, size, px, size, Dir);
        }

        // ---------- Distance functions ([-1, 1] space; negative inside) ----------

        static float Cov(float d) => Mathf.Clamp01(0.5f - d);

        static float Len(float x, float y) => Mathf.Sqrt(x * x + y * y);

        static float RoundBox(float x, float y, float hw, float hh, float r)
        {
            float qx = Mathf.Abs(x) - hw + r, qy = Mathf.Abs(y) - hh + r;
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        /// <summary>Exact distance to a polygon given as x0,y0,x1,y1,... (any winding).</summary>
        static float Polygon(float x, float y, float[] v)
        {
            int n = v.Length / 2;
            float d = float.MaxValue;
            float s = 1f;
            for (int i = 0, j = n - 1; i < n; j = i, i++)
            {
                float ax = v[2 * i], ay = v[2 * i + 1], bx = v[2 * j], by = v[2 * j + 1];
                float ex = bx - ax, ey = by - ay, wx = x - ax, wy = y - ay;
                float t = Mathf.Clamp01((wx * ex + wy * ey) / (ex * ex + ey * ey));
                float dx = wx - ex * t, dy = wy - ey * t;
                d = Mathf.Min(d, dx * dx + dy * dy);
                bool c1 = y >= ay, c2 = y < by, c3 = ex * wy > ey * wx;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }

        static float Regular(float x, float y, int sides, float r, float rotationDeg)
        {
            var v = new float[sides * 2];
            for (int i = 0; i < sides; i++)
            {
                float a = (rotationDeg + 90f + 360f * i / sides) * Mathf.Deg2Rad;
                v[2 * i] = Mathf.Cos(a) * r;
                v[2 * i + 1] = Mathf.Sin(a) * r;
            }
            return Polygon(x, y, v);
        }

        static float Star(float x, float y, int points, float outer, float inner, float rotationDeg)
        {
            var v = new float[points * 4];
            for (int i = 0; i < points * 2; i++)
            {
                float a = (rotationDeg + 90f + 180f * i / points) * Mathf.Deg2Rad;
                float r = (i & 1) == 0 ? outer : inner;
                v[2 * i] = Mathf.Cos(a) * r;
                v[2 * i + 1] = Mathf.Sin(a) * r;
            }
            return Polygon(x, y, v);
        }
    }
}

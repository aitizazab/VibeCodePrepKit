using System.Collections.Generic;
using UnityEngine;

namespace JamKit.Argument
{
    /// <summary>
    /// The battle's backdrops, drawn once in code in the sprites' own style (docs/redesign/battle.md, "Art direction"):
    /// Lena's and Walt's drawings are ink on paper with manga screentone, so the table is too. Halftone dots for light
    /// and shade, ink lines for the planks and the place settings, one warm accent. A drop-in file wins over the drawn
    /// one: Resources/ArgumentArt/stage_&lt;name&gt;.png (e.g. stage_kitchen.png, 1200 x 640).
    /// </summary>
    public static class StageArt
    {
        public const int TableWidth = 1200, TableHeight = 640;

        static readonly Dictionary<string, Texture2D> _made = new Dictionary<string, Texture2D>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _made.Clear();

        /// <summary>The table for an arena ("kitchen_table" Lena's, "larch_street_foot" Walt's step).</summary>
        public static Texture2D Table(string arena)
        {
            string key = string.IsNullOrEmpty(arena) ? "kitchen_table" : arena;
            if (_made.TryGetValue(key, out var t) && t != null) return t;
            t = Resources.Load<Texture2D>(ArgumentArt.ArtFolder + "stage_" + key);
            if (t == null) t = key == "larch_street_foot" ? DrawStep() : DrawKitchen();
            _made[key] = t;
            return t;
        }

        /// <summary>A screentone dot: 1 inside, 0 outside, soft at the edge. <paramref name="tone"/> 0..1 is how much
        /// ink (dot size); the grid is rotated 45 degrees like a printed tone.</summary>
        static float Dot(int x, int y, float tone, float cell)
        {
            if (tone <= 0.01f) return 0f;
            float u = (x + y) * 0.70710678f / cell, v = (x - y) * 0.70710678f / cell;
            float fu = u - Mathf.Floor(u) - 0.5f, fv = v - Mathf.Floor(v) - 0.5f;
            float d = Mathf.Sqrt(fu * fu + fv * fv);
            float r = Mathf.Sqrt(Mathf.Clamp01(tone)) * 0.62f;
            return Mathf.Clamp01((r - d) * cell * 0.9f + 0.5f);
        }

        static float Hash(int x, int y)
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) / 4294967295f;
        }

        /// <summary>Lena's kitchen table, seen from Nadia's seat: dark planks under the morning light from the window
        /// (a pool of halftone), four place settings at the edges (the fourth, Nadia's, nearest), a coffee ring.</summary>
        static Texture2D DrawKitchen()
        {
            int w = TableWidth, h = TableHeight;
            var px = new Color32[w * h];
            var ink = new Color(0.055f, 0.045f, 0.06f);
            var paper = new Color(0.93f, 0.89f, 0.82f);
            var warm = new Color(0.98f, 0.82f, 0.55f);
            for (int y = 0; y < h; y++)
            {
                float fy = (float)y / h;
                for (int x = 0; x < w; x++)
                {
                    float fx = (float)x / w;
                    // The light from the window: upper left, falling off across the table.
                    float lx = fx - 0.18f, ly = fy - 0.78f;
                    float light = Mathf.Clamp01(1f - Mathf.Sqrt(lx * lx * 0.9f + ly * ly * 2.2f) * 1.25f);
                    // Planks: long boards across, a seam every 80 px with a slight wander, grain as faint streaks.
                    float plank = (y + Mathf.Sin(x * 0.004f + y * 0.01f) * 3f) % 80f;
                    float seam = plank < 2.2f ? 1f : 0f;
                    float grain = Mathf.PerlinNoise(x * 0.012f, y * 0.18f + Mathf.Floor(y / 80f) * 7.3f);
                    float tone = light * (0.42f + grain * 0.35f);
                    float dot = Dot(x, y, tone, 7f);
                    var c = Color.Lerp(ink, Color.Lerp(paper, warm, 0.35f), dot * 0.55f);
                    // Hatching in the far shade (lower right): the table edge away from the light.
                    float shade = Mathf.Clamp01((fx - 0.62f) * 2.2f) * Mathf.Clamp01((0.45f - fy) * 2.4f);
                    if (shade > 0f && ((x + y) % 9) < 1) c = Color.Lerp(c, ink * 0.6f, shade * 0.7f);
                    if (seam > 0f) c = Color.Lerp(c, ink * 0.5f, 0.85f);
                    // Paper grain over everything.
                    float n = Hash(x, y) * 0.035f;
                    c = new Color(c.r + n, c.g + n, c.b + n);
                    px[y * w + x] = c;
                }
            }
            // Place settings: an inked plate outline at each edge; Nadia's (bottom, nearest) with her place card.
            Plate(px, w, h, 600, 70, 92, paper);   // Nadia's, nearest (bottom)
            Plate(px, w, h, 600, 575, 80, paper);  // Elias's, across
            Plate(px, w, h, 110, 330, 78, paper);  // Theo's
            Plate(px, w, h, 1090, 330, 78, paper); // Lena's
            Rect(px, w, h, 560, 175, 80, 26, paper); // the place card
            Ring(px, w, h, 860, 150, 34, 3f, new Color(0.45f, 0.27f, 0.16f)); // a coffee ring
            return Make(px, w, h, "stage_kitchen_drawn");
        }

        /// <summary>Walt's: the step outside the shop, worn concrete and the shop's shadow, a tyre and a patch kit.</summary>
        static Texture2D DrawStep()
        {
            int w = TableWidth, h = TableHeight;
            var px = new Color32[w * h];
            var ink = new Color(0.06f, 0.055f, 0.05f);
            var paper = new Color(0.93f, 0.9f, 0.83f);
            var warm = new Color(1f, 0.84f, 0.55f);
            for (int y = 0; y < h; y++)
            {
                float fy = (float)y / h;
                for (int x = 0; x < w; x++)
                {
                    float fx = (float)x / w;
                    // Golden hour from the right; the shop's shadow on the left.
                    float light = Mathf.Clamp01(fx * 1.2f - 0.15f) * (0.75f + 0.25f * fy);
                    float slab = (x % 300 < 2 || y % 160 < 2) ? 1f : 0f;
                    float grit = Mathf.PerlinNoise(x * 0.03f, y * 0.03f);
                    float dot = Dot(x, y, light * (0.45f + grit * 0.3f), 7f);
                    var c = Color.Lerp(ink, Color.Lerp(paper, warm, 0.4f), dot * 0.55f);
                    if (slab > 0f) c = Color.Lerp(c, ink * 0.5f, 0.8f);
                    float n = Hash(x, y) * 0.035f;
                    px[y * w + x] = new Color(c.r + n, c.g + n, c.b + n);
                }
            }
            Ring(px, w, h, 200, 160, 120, 14f, paper);  // the tyre
            Ring(px, w, h, 200, 160, 60, 3f, paper);
            Rect(px, w, h, 930, 90, 120, 60, paper);    // the patch kit
            return Make(px, w, h, "stage_step_drawn");
        }

        static void Plate(Color32[] px, int w, int h, int cx, int cy, int r, Color c)
        {
            Ring(px, w, h, cx, cy, r, 3f, c);
            Ring(px, w, h, cx, cy, Mathf.RoundToInt(r * 0.68f), 2f, c);
        }

        static void Ring(Color32[] px, int w, int h, int cx, int cy, int r, float thick, Color c)
        {
            int r2 = r + (int)thick + 2;
            for (int y = Mathf.Max(0, cy - r2); y < Mathf.Min(h, cy + r2); y++)
                for (int x = Mathf.Max(0, cx - r2); x < Mathf.Min(w, cx + r2); x++)
                {
                    float d = Mathf.Abs(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r);
                    float a = Mathf.Clamp01(thick * 0.5f - d + 0.5f) * 0.8f;
                    if (a <= 0f) continue;
                    var o = (Color)px[y * w + x];
                    px[y * w + x] = Color.Lerp(o, c, a);
                }
        }

        static void Rect(Color32[] px, int w, int h, int x0, int y0, int rw, int rh, Color c)
        {
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(h, y0 + rh); y++)
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(w, x0 + rw); x++)
                {
                    bool edge = y < y0 + 2 || y >= y0 + rh - 2 || x < x0 + 2 || x >= x0 + rw - 2;
                    var o = (Color)px[y * w + x];
                    px[y * w + x] = Color.Lerp(o, c, edge ? 0.85f : 0.35f);
                }
        }

        static Texture2D Make(Color32[] px, int w, int h, string name)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }

        /// <summary>A soft dark edge, clear in the middle (the room closing in: composure, the end beats).</summary>
        public static Texture2D Vignette()
        {
            if (_made.TryGetValue("vignette", out var t) && t != null) return t;
            const int s = 256;
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f) / s - 0.5f, dy = (y + 0.5f) / s - 0.5f;
                    float d = Mathf.Sqrt(dx * dx * 1.2f + dy * dy * 1.9f) * 2f;
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.35f) / 0.75f));
                    px[y * s + x] = new Color32(0, 0, 0, (byte)(a * 255f));
                }
            t = new Texture2D(s, s, TextureFormat.RGBA32, false) { name = "stage_vignette", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            t.SetPixels32(px);
            t.Apply(false, true);
            _made["vignette"] = t;
            return t;
        }

        /// <summary>Her sky (SELECTION.md 7: the ceiling lifts after "He called you first"), in the same ink-and-screentone
        /// hand as the table: deep night ink with a halftone fading up from a faint wine glow at the bottom edge (the kitchen,
        /// below), and stars, a few larger with a printed cross. A drop-in file wins: Resources/ArgumentArt/stage_sky.png.</summary>
        public static Texture2D Sky()
        {
            if (_made.TryGetValue("sky", out var t) && t != null) return t;
            t = Resources.Load<Texture2D>(ArgumentArt.ArtFolder + "stage_sky");
            if (t != null) { _made["sky"] = t; return t; }
            const int w = TableWidth, h = TableHeight;
            var px = new Color32[w * h];
            var night = new Color(0.035f, 0.04f, 0.085f);
            var deep = new Color(0.075f, 0.065f, 0.14f);
            var glow = new Color(0.42f, 0.1f, 0.16f);
            for (int y = 0; y < h; y++)
            {
                // y = 0 is the bottom row of the texture.
                float up = (float)y / (h - 1);
                var c = Color.Lerp(deep, night, up);
                float tone = Mathf.Clamp01(0.55f - up * 0.9f);
                for (int x = 0; x < w; x++)
                {
                    float d = Dot(x, y, tone, 7f);
                    px[y * w + x] = (Color32)Color.Lerp(c, glow, d * 0.55f);
                }
            }
            // Stars: many faint, a few bright with a cross.
            for (int i = 0; i < 520; i++)
            {
                int x = (int)(Hash(i * 7 + 1, 3) * w), y = (int)(Hash(i * 13 + 5, 11) * h);
                float b = Hash(i, 29);
                int r = b > 0.97f ? 3 : b > 0.85f ? 2 : 1;
                float bright = 0.35f + 0.65f * b;
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int xx = x + dx, yy = y + dy;
                        if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;
                        float fall = 1f - Mathf.Sqrt(dx * dx + dy * dy) / (r + 0.5f);
                        if (fall <= 0f) continue;
                        var was = (Color)px[yy * w + xx];
                        px[yy * w + xx] = (Color32)Color.Lerp(was, new Color(1f, 0.96f, 0.86f), Mathf.Clamp01(fall * bright));
                    }
                if (r < 3) continue;
                for (int k = 4; k <= 9; k++)
                    foreach (var (dx, dy) in new[] { (k, 0), (-k, 0), (0, k), (0, -k) })
                    {
                        int xx = x + dx, yy = y + dy;
                        if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;
                        var was = (Color)px[yy * w + xx];
                        px[yy * w + xx] = (Color32)Color.Lerp(was, new Color(1f, 0.95f, 0.8f), 0.5f * (1f - (k - 4) / 6f));
                    }
            }
            t = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "stage_sky", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            t.SetPixels32(px);
            t.Apply(false, true);
            _made["sky"] = t;
            return t;
        }

        /// <summary>A paper grain to lay over the whole stage (tileable, faint).</summary>
        public static Texture2D Grain()
        {
            if (_made.TryGetValue("grain", out var t) && t != null) return t;
            const int s = 256;
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float n = Hash(x, y);
                    byte a = (byte)(n > 0.82f ? 22 : n < 0.1f ? 14 : 0);
                    byte v = (byte)(n > 0.5f ? 255 : 0);
                    px[y * s + x] = new Color32(v, v, v, a);
                }
            t = new Texture2D(s, s, TextureFormat.RGBA32, false) { name = "stage_grain", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            t.SetPixels32(px);
            t.Apply(false, true);
            _made["grain"] = t;
            return t;
        }
    }
}

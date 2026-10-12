using System.Collections.Generic;
using UnityEngine;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// Stand-in pictures drawn in code, in style E's soft flat colours (never grey boxes), for every
    /// picture the opening and the title use until the real art is in Art/ (the builder fills the boot
    /// asset's art list; anything missing comes from here and is logged once). Includes the coffee with
    /// "Quit" written in its milk and the notepad with "Credits" at the top, in a little hand-drawn
    /// stroke alphabet. <see cref="Dispose"/> frees what it made.
    /// </summary>
    public sealed class VhTextures
    {
        readonly List<Texture2D> _made = new List<Texture2D>();

        public void Dispose()
        {
            foreach (var t in _made) VhUtil.Kill(t);
            _made.Clear();
        }

        /// <summary>
        /// Compresses every picture made so far and drops its CPU copy (docs/redesign/perf.md, Day item 7: readable RGBA32
        /// stand-ins hold their pixels twice). Call once the set is built: a sealed picture can't be read back (the drawing
        /// code checks <c>isReadable</c> and draws afresh instead). Pictures made later stay readable until the next call.
        /// </summary>
        public void Seal()
        {
            foreach (var t in _made)
            {
                if (t == null || !t.isReadable) continue;
                if (t.width % 4 == 0 && t.height % 4 == 0) t.Compress(true);
                t.Apply(false, true);
            }
        }

        Texture2D Make(string name, int w, int h, Color[] px, bool repeat)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "vh_" + name + " (stand-in)" };
            t.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            t.anisoLevel = 4;
            t.SetPixels(px);
            t.Apply(true, false);
            _made.Add(t);
            return t;
        }

        // ---------- Noise ----------

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        /// <summary>Tileable value noise (period in cells).</summary>
        static float Noise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int Wrap(int v) => ((v % period) + period) % period;
            float a = Hash(Wrap(x0), Wrap(y0), seed), b = Hash(Wrap(x0 + 1), Wrap(y0), seed);
            float c = Hash(Wrap(x0), Wrap(y0 + 1), seed), d = Hash(Wrap(x0 + 1), Wrap(y0 + 1), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        // ---------- Tiles ----------

        /// <summary>Soft wood grain (tiles; grain runs along V).</summary>
        public Texture2D Wood(Color baseColor, int seed = 3)
        {
            const int S = 256;
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (float)x / S * 8f, v = (float)y / S * 2f;
                    float warp = Noise(u * 0.5f, v * 2f, 4, seed) * 1.6f;
                    float grain = Mathf.Sin((u + warp) * Mathf.PI * 2f) * 0.5f + 0.5f;
                    float fine = Noise(u * 4f, v * 16f, 32, seed + 1);
                    float k = 0.92f + grain * 0.07f + fine * 0.04f;
                    px[y * S + x] = new Color(k, k * 0.995f, k * 0.985f, 1f);
                }
            return Make("wood", S, S, px, true);
        }

        /// <summary>Matte painted plaster: a faint mottle (tiles).</summary>
        public Texture2D Plaster(int seed = 5)
        {
            const int S = 128;
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float n = Noise(x / 16f, y / 16f, 8, seed) * 0.6f + Noise(x / 4f, y / 4f, 32, seed + 7) * 0.4f;
                    float k = 0.95f + n * 0.05f;
                    px[y * S + x] = new Color(k, k, k, 1f);
                }
            return Make("plaster", S, S, px, true);
        }

        /// <summary>A soft knit (the blanket): faint crossing ribs (tiles).</summary>
        public Texture2D Knit(int seed = 9)
        {
            const int S = 128;
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float rib = Mathf.Abs(Mathf.Sin((x / (float)S) * Mathf.PI * 32f)) * 0.5f + Mathf.Abs(Mathf.Sin((y / (float)S) * Mathf.PI * 16f + x * 0.05f)) * 0.5f;
                    float n = Noise(x / 8f, y / 8f, 16, seed);
                    float k = 0.9f + rib * 0.07f + n * 0.05f;
                    px[y * S + x] = new Color(k, k, k, 1f);
                }
            return Make("knit", S, S, px, true);
        }

        /// <summary>A white-ish texture (multiplied by the material colour: no change).</summary>
        public Texture2D Flat(Color c) => Make("flat", 4, 4, Fill(16, c), false);

        static Color[] Fill(int n, Color c)
        {
            var px = new Color[n];
            for (int i = 0; i < n; i++) px[i] = c;
            return px;
        }

        /// <summary>A soft round blob (an emergency light's glow, a streetlight).</summary>
        public Texture2D Blob()
        {
            const int S = 64;
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = (x + 0.5f) / S * 2f - 1f, dy = (y + 0.5f) / S * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);
                    px[y * S + x] = new Color(1f, 1f, 1f, a);
                }
            return Make("blob", S, S, px, false);
        }

        // ---------- Pictures ----------

        /// <summary>The coffee seen from above: dark crema, a lighter rim, a milk swirl, and "Quit" written in the milk.
        /// With <paramref name="swirl"/> (a plain coffee picture, readable), "Quit" is written over it instead.</summary>
        public Texture2D CoffeeQuit(Texture2D swirl = null)
        {
            bool baseArt = swirl != null && swirl.isReadable;
            const int S = 512;
            var px = new Color[S * S];
            Color coffee = new Color(0.33f, 0.22f, 0.15f), crema = new Color(0.55f, 0.40f, 0.28f), milk = new Color(0.90f, 0.84f, 0.74f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = (x + 0.5f) / S - 0.5f, dy = (y + 0.5f) / S - 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                    float n = Noise(x / 24f, y / 24f, 22, 11);
                    Color c = baseArt ? swirl.GetPixelBilinear((x + 0.5f) / S, (y + 0.5f) / S) : Color.Lerp(coffee, crema, Mathf.SmoothStep(0.62f, 1f, r) * 0.8f + n * 0.12f);
                    px[y * S + x] = c;
                }
            var brush = new Brush(px, S, S);
            // A loose swirl of milk around the word.
            var swirlPath = new List<Vector2>();
            for (int i = 0; i <= 160; i++)
            {
                float t = i / 160f;
                float a = t * Mathf.PI * 3.4f + 0.6f;
                float rr = 0.44f - t * 0.1f;
                swirlPath.Add(new Vector2(0.5f + Mathf.Cos(a) * rr, 0.5f + Mathf.Sin(a) * rr * 0.92f));
            }
            if (!baseArt) brush.Stroke(swirlPath, S, 5f, milk, 0.55f);
            StrokeText.Draw(brush, "Quit", new Vector2(0.5f, 0.47f), 0.205f, -8f, S, 10f, milk, 0.95f);
            return Make("coffee_quit", S, S, px, false);
        }

        /// <summary>A lined notepad page with "Credits" at the top and lines of handwriting.</summary>
        public Texture2D NotepadPage()
        {
            const int W = 512, H = 704;
            var px = new Color[W * H];
            Color paper = new Color(0.93f, 0.91f, 0.85f), line = new Color(0.62f, 0.70f, 0.80f), margin = new Color(0.80f, 0.55f, 0.55f), ink = new Color(0.18f, 0.22f, 0.36f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float n = Noise(x / 32f, y / 32f, 16, 21) * 0.03f;
                    Color c = new Color(paper.r - n, paper.g - n, paper.b - n);
                    int fromTop = H - 1 - y;
                    if (fromTop > 120 && (fromTop - 120) % 38 == 0) c = Color.Lerp(c, line, 0.7f);
                    if (x == 70 || x == 71) c = Color.Lerp(c, margin, 0.7f);
                    px[y * W + x] = c;
                }
            var brush = new Brush(px, W, H);
            StrokeText.Draw(brush, "Credits", new Vector2(0.52f, 1f - 80f / H), 0.085f, -2f, W, 3.2f, ink, 0.95f, (float)H / W);
            // Lines of handwriting: loops along each ruled line, in words.
            var path = new List<Vector2>();
            for (int row = 0; row < 13; row++)
            {
                float baseY = 1f - (120f + 38f * (row + 1) - 8f) / H;
                float x = 90f / W;
                float end = (row % 4 == 3 ? 0.55f : 0.9f) - Hash(row, 3, 5) * 0.12f;
                int word = 0;
                while (x < end)
                {
                    path.Clear();
                    float wordLen = 0.06f + Hash(row, word, 7) * 0.1f;
                    for (float t = 0f; t <= 1f; t += 0.02f)
                    {
                        float xx = x + t * wordLen;
                        float loop = Mathf.Sin(t * wordLen * W * 0.45f) * 6f + Mathf.Sin(t * wordLen * W * 0.13f + word) * 3f;
                        path.Add(new Vector2(xx + Mathf.Cos(t * wordLen * W * 0.45f) * 2.5f / W, baseY + (loop + 6f) / H));
                    }
                    brush.Stroke(path, W, 1.6f, ink, 0.8f, (float)H / W);
                    x += wordLen + 0.025f;
                    word++;
                }
            }
            return Make("notepad_page", W, H, px, false);
        }

        /// <summary>Nadia's hospital ID: a white card, a teal band, her photo (a soft portrait shape), her name and a barcode.</summary>
        public Texture2D BadgeFace(Texture2D photo)
        {
            const int W = 256, H = 400;
            var px = new Color[W * H];
            Color card = new Color(0.92f, 0.92f, 0.90f), band = new Color(0.28f, 0.47f, 0.52f), ink = new Color(0.2f, 0.22f, 0.26f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    Color c = card;
                    if (y > H - 70) c = band;
                    if (y < 60 && y > 30 && x > 40 && x < W - 40 && (x / 3) % 3 != 0 && Hash(x / 3, 0, 4) > 0.3f) c = ink;
                    px[y * W + x] = c;
                }
            // The photo: her portrait if there is one, else a soft head-and-shoulders in her colours.
            int px0 = 58, py0 = 140, pw = 140, ph = 170;
            for (int y = 0; y < ph; y++)
                for (int x = 0; x < pw; x++)
                {
                    Color c;
                    if (photo != null && photo.isReadable)
                    {
                        // The centre of the photo at the slot's shape (the art is 16:9).
                        float slot = (float)pw / ph, have = (float)photo.width / photo.height;
                        float su = have > slot ? slot / have : 1f, sv = have > slot ? 1f : have / slot;
                        c = photo.GetPixelBilinear(0.5f + ((float)x / pw - 0.5f) * su, 0.5f + ((float)y / ph - 0.5f) * sv);
                    }
                    else
                    {
                        float u = (float)x / pw - 0.5f, v = (float)y / ph;
                        c = new Color(0.70f, 0.74f, 0.78f);
                        float head = Mathf.Sqrt(u * u / 0.04f + (v - 0.62f) * (v - 0.62f) / 0.06f);
                        float hair = Mathf.Sqrt(u * u / 0.06f + (v - 0.68f) * (v - 0.68f) / 0.075f);
                        if (v < 0.36f && Mathf.Abs(u) < 0.42f - (0.36f - v) * 0.4f) c = VhWakingStyle.Scrubs;
                        if (hair < 1f) c = VhWakingStyle.Hair;
                        if (head < 1f && v < 0.78f) c = VhWakingStyle.Skin;
                    }
                    px[(py0 + y) * W + px0 + x] = c;
                }
            var brush = new Brush(px, W, H);
            var path = new List<Vector2>();
            for (int row = 0; row < 2; row++)
            {
                path.Clear();
                float y = (110f - row * 26f) / H;
                for (float t = 0f; t <= 1f; t += 0.02f) path.Add(new Vector2(0.22f + t * (row == 0 ? 0.56f : 0.4f), y + Mathf.Sin(t * 40f) * 2f / H));
                brush.Stroke(path, W, row == 0 ? 3f : 2f, ink, 0.9f, (float)H / W);
            }
            return Make("badge_face", W, H, px, false);
        }

        /// <summary>A small framed photo: two people side by side on a muted beach-blue background (Nadia and Elias, years ago).</summary>
        public Texture2D FramedPhoto()
        {
            const int W = 200, H = 256;
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float v = (float)y / H;
                    Color c = Color.Lerp(new Color(0.55f, 0.58f, 0.52f), new Color(0.68f, 0.75f, 0.80f), v);
                    for (int p = 0; p < 2; p++)
                    {
                        float cx = p == 0 ? 0.36f : 0.64f, top = p == 0 ? 0.62f : 0.70f;
                        float u = (float)x / W - cx;
                        float head = Mathf.Sqrt(u * u / 0.006f + (v - top) * (v - top) / 0.008f);
                        if (v < top - 0.06f && Mathf.Abs(u) < 0.13f - (top - 0.06f - v) * -0.1f) c = p == 0 ? VhWakingStyle.Scrubs : new Color(0.45f, 0.40f, 0.36f);
                        if (head < 1f) c = VhWakingStyle.Skin;
                        if (head < 1.15f && v > top + 0.02f) c = p == 0 ? VhWakingStyle.Hair : new Color(0.28f, 0.22f, 0.18f);
                    }
                    px[y * W + x] = c;
                }
            return Make("framed_photo", W, H, px, false);
        }

        /// <summary>A phone wallpaper: a soft dusk gradient.</summary>
        public Texture2D Wallpaper()
        {
            const int W = 64, H = 128;
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float v = (float)y / H;
                    px[y * W + x] = Color.Lerp(new Color(0.20f, 0.22f, 0.32f), new Color(0.42f, 0.36f, 0.42f), v * v);
                }
            return Make("wallpaper", W, H, px, false);
        }

        /// <summary>A news still when there are no frames: a night road, wet asphalt, a barrier, dark trees.</summary>
        public Texture2D NewsStill(int seed)
        {
            const int W = 512, H = 288;
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (float)x / W, v = (float)y / H;
                    Color sky = Color.Lerp(new Color(0.10f, 0.11f, 0.16f), new Color(0.05f, 0.06f, 0.09f), v);
                    float horizon = 0.46f + Mathf.Sin(u * 9f + seed) * 0.03f + Noise(u * 10f, 0.5f, 10, seed) * 0.06f;
                    Color c = v > horizon ? sky : new Color(0.07f, 0.08f, 0.09f);
                    // The road narrows to the horizon.
                    float half = (horizon - v) * 1.1f;
                    if (v < horizon && Mathf.Abs(u - 0.5f - (seed % 2 == 0 ? 0.05f : -0.08f) * (horizon - v)) < half)
                    {
                        float wet = Noise(u * 40f, v * 20f, 40, seed) * 0.05f;
                        c = new Color(0.16f + wet, 0.17f + wet, 0.19f + wet);
                        if (Mathf.Abs(u - 0.5f) < 0.004f + (horizon - v) * 0.01f && ((int)(v * 40f) % 2 == 0)) c = new Color(0.55f, 0.52f, 0.42f);
                    }
                    px[y * W + x] = c;
                }
            return Make("news_still_" + seed, W, H, px, false);
        }

        // ---------- Drawing ----------

        /// <summary>Stamps soft round dabs into a pixel buffer (alpha blended).</summary>
        public sealed class Brush
        {
            readonly Color[] _px;
            readonly int _w, _h;

            public Brush(Color[] px, int w, int h)
            {
                _px = px;
                _w = w;
                _h = h;
            }

            public void Dab(float cx, float cy, float radius, Color c, float alpha)
            {
                int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - radius - 1)), x1 = Mathf.Min(_w - 1, Mathf.CeilToInt(cx + radius + 1));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - radius - 1)), y1 = Mathf.Min(_h - 1, Mathf.CeilToInt(cy + radius + 1));
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                        float a = Mathf.Clamp01(radius + 0.5f - d) * alpha;
                        if (a <= 0f) continue;
                        int i = y * _w + x;
                        _px[i] = Color.Lerp(_px[i], c, a);
                    }
            }

            /// <summary>A stroke through points in 0..1 texture space (x scaled by the width; y by width * aspect).</summary>
            public void Stroke(IList<Vector2> pts, int width, float radius, Color c, float alpha, float aspect = 1f)
            {
                for (int i = 0; i + 1 < pts.Count; i++)
                {
                    Vector2 a = new Vector2(pts[i].x * _w, pts[i].y * _h), b = new Vector2(pts[i + 1].x * _w, pts[i + 1].y * _h);
                    float len = Vector2.Distance(a, b);
                    int steps = Mathf.Max(1, Mathf.CeilToInt(len / Mathf.Max(0.5f, radius * 0.35f)));
                    for (int s = 0; s <= steps; s++)
                    {
                        Vector2 p = Vector2.Lerp(a, b, (float)s / steps);
                        Dab(p.x, p.y, radius, c, alpha);
                    }
                }
            }
        }

        /// <summary>A gear (eight teeth, a hole) filled into a pixel buffer, anti-aliased: the "Settings" icon.</summary>
        public static void Gear(Color[] px, int w, int h, float cx, float cy, float radius, Color c)
        {
            float inner = radius * 0.78f, hole = radius * 0.34f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - radius - 2)), x1 = Mathf.Min(w - 1, Mathf.CeilToInt(cx + radius + 2));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - radius - 2)), y1 = Mathf.Min(h - 1, Mathf.CeilToInt(cy + radius + 2));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    // Teeth: a rounded square wave round the rim (eight of them).
                    float a = Mathf.Atan2(dy, dx) * 8f / (Mathf.PI * 2f);
                    float f = Mathf.Abs(a - Mathf.Round(a));
                    float tooth = Mathf.Clamp01((0.3f - f) * 8f);
                    float outer = Mathf.Lerp(inner, radius, tooth);
                    float cover = Mathf.Clamp01(outer + 0.5f - r) * Mathf.Clamp01(r - hole + 0.5f);
                    if (cover <= 0f) continue;
                    int i = y * w + x;
                    px[i] = Color.Lerp(px[i], c, cover);
                }
        }

        /// <summary>
        /// Nadia's badge with the Settings label: the badge art (or the drawn badge) with a gear and "Settings" written
        /// on the card's lower band (the art's empty grey bar; on the drawn card, a bar painted over the name lines).
        /// </summary>
        public Texture2D BadgeSettings(Texture2D art, Texture2D photo)
        {
            bool fromArt = art != null && art.isReadable;
            int W, H;
            Color[] px;
            float barV;
            if (fromArt)
            {
                W = 384;
                H = Mathf.Max(1, Mathf.RoundToInt(W * (float)art.height / art.width));
                px = new Color[W * H];
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++) px[y * W + x] = art.GetPixelBilinear((x + 0.5f) / W, (y + 0.5f) / H);
                barV = 0.364f; // the badge art's wide grey bar under the photo
            }
            else
            {
                var drawn = BadgeFace(photo);
                W = drawn.width;
                H = drawn.height;
                px = drawn.GetPixels();
                barV = 0.25f;
                Color bar = new Color(0.84f, 0.87f, 0.87f);
                for (int y = Mathf.RoundToInt((barV - 0.05f) * H); y < Mathf.RoundToInt((barV + 0.05f) * H); y++)
                    for (int x = Mathf.RoundToInt(0.12f * W); x < Mathf.RoundToInt(0.88f * W); x++) px[y * W + x] = bar;
            }
            Color ink = new Color(0.17f, 0.30f, 0.34f);
            // The gear and the word together inside the card face with a margin: on the card only about u 0.2 to 0.8 of
            // this picture shows (the art's own border and the crop), so they sit between 0.26 and 0.72.
            Gear(px, W, H, 0.30f * W, barV * H, 0.04f * W, ink);
            var brush = new Brush(px, W, H);
            // Narrow enough that the final "s" stays on the card face (the round-2 title shot showed "Setting" cut at its edge;
            // the word came out about a third wider on the card than its advances add up to, so this leaves room).
            StrokeText.Draw(brush, "Settings", new Vector2(0.545f, barV - 0.006f), 0.08f, 0f, W, W / 150f, ink, 0.95f, (float)H / W);
            return Make("badge_settings", W, H, px, false);
        }

        /// <summary>
        /// The notepad page with the credits on it: the page art (or the drawn page, which already has them) with
        /// "Credits" written on its top lines, underlined, and a few lines of handwritten names under it.
        /// </summary>
        /// <summary>The pill bottle's label (the Despair route's quit piece): pharmacy white paper, a coloured band, "Give Up"
        /// in dark ink, a few printed lines.</summary>
        public Texture2D PillLabel()
        {
            const int W = 512, H = 256;
            var px = new Color[W * H];
            Color paper = new Color(0.95f, 0.94f, 0.90f), band = new Color(0.62f, 0.22f, 0.22f), ink = new Color(0.13f, 0.14f, 0.18f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float n = Noise(x / 32f, y / 32f, 16, 31) * 0.025f;
                    Color c = new Color(paper.r - n, paper.g - n, paper.b - n);
                    if (y > H - 30) c = band;
                    px[y * W + x] = c;
                }
            var brush = new Brush(px, W, H);
            StrokeText.Draw(brush, "Give Up", new Vector2(0.5f, 0.52f), 0.17f, 0f, W, 6f, ink, 0.97f, (float)H / W);
            var path = new List<Vector2>();
            for (int row = 0; row < 2; row++)
            {
                path.Clear();
                float y = (40f - row * 16f) / H;
                path.Add(new Vector2(0.12f, y));
                path.Add(new Vector2(row == 0 ? 0.8f : 0.55f, y));
                brush.Stroke(path, W, 2.2f, new Color(0.45f, 0.46f, 0.5f), 0.9f, (float)H / W);
            }
            return Make("pill_label", W, H, px, false);
        }

        public Texture2D NotepadCredits(Texture2D art)
        {
            if (art == null || !art.isReadable) return NotepadPage();
            const int W = 512;
            int H = Mathf.Max(1, Mathf.RoundToInt(W * (float)art.height / art.width));
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++) px[y * W + x] = art.GetPixelBilinear((x + 0.5f) / W, (y + 0.5f) / H);
            float aspect = (float)H / W;
            Color ink = new Color(0.18f, 0.22f, 0.36f);
            var brush = new Brush(px, W, H);
            // The art's ruled lines: the first 0.115 from the top, then every 0.0557 (the notepad shows the middle 72%).
            const float first = 0.115f, step = 0.0557f;
            float headBase = 1f - (first + step);
            StrokeText.Draw(brush, "Credits", new Vector2(0.5f, headBase + 0.042f / aspect), 0.1f, -2f, W, 3.4f, ink, 0.95f, aspect);
            var path = new List<Vector2>();
            path.Clear();
            for (float t = 0f; t <= 1f; t += 0.05f) path.Add(new Vector2(0.31f + t * 0.38f, headBase - 0.012f + Mathf.Sin(t * 5f) * 0.002f));
            brush.Stroke(path, W, 1.8f, ink, 0.85f, aspect);
            // Names: handwriting loops along the next lines, short and long.
            for (int row = 0; row < 8; row++)
            {
                float baseY = 1f - (first + step * (row + 3)) + 0.004f;
                float x = 0.2f;
                float end = (row % 3 == 2 ? 0.55f : 0.78f) - Hash(row, 3, 5) * 0.1f;
                int word = 0;
                while (x < end)
                {
                    path.Clear();
                    float wordLen = 0.06f + Hash(row, word, 7) * 0.09f;
                    for (float t = 0f; t <= 1f; t += 0.02f)
                    {
                        float xx = x + t * wordLen;
                        float loop = Mathf.Sin(t * wordLen * W * 0.45f) * 6f + Mathf.Sin(t * wordLen * W * 0.13f + word) * 3f;
                        path.Add(new Vector2(xx + Mathf.Cos(t * wordLen * W * 0.45f) * 2.5f / W, baseY + (loop + 7f) / H));
                    }
                    brush.Stroke(path, W, 1.5f, ink, 0.8f, aspect);
                    x += wordLen + 0.025f;
                    word++;
                }
            }
            return Make("notepad_credits", W, H, px, false);
        }

        /// <summary>A tiny hand-drawn stroke alphabet (only the letters the title needs: "Quit", "Credits", "Settings").</summary>
        public static class StrokeText
        {
            static readonly List<Vector2> Pts = new List<Vector2>();

            static void Arc(float cx, float cy, float rx, float ry, float a0, float a1, int n)
            {
                for (int i = 0; i <= n; i++)
                {
                    float a = Mathf.Lerp(a0, a1, (float)i / n) * Mathf.Deg2Rad;
                    Pts.Add(new Vector2(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry));
                }
            }

            /// <summary>The strokes of one letter in a 1-unit-high box (baseline 0, x-height 0.55); returns its advance.</summary>
            static float Letter(char ch, List<List<Vector2>> strokes)
            {
                strokes.Clear();
                void Begin() { Pts.Clear(); }
                void End() { strokes.Add(new List<Vector2>(Pts)); }
                switch (ch)
                {
                    case 'Q':
                        Begin(); Arc(0.42f, 0.5f, 0.38f, 0.48f, 80f, 440f, 40); End();
                        Begin(); Pts.Add(new Vector2(0.48f, 0.18f)); Pts.Add(new Vector2(0.64f, 0.02f)); Pts.Add(new Vector2(0.86f, -0.08f)); End();
                        return 0.95f;
                    case 'C':
                        Begin(); Arc(0.42f, 0.5f, 0.38f, 0.48f, 50f, 315f, 32); End();
                        return 0.82f;
                    case 'u':
                        Begin(); Pts.Add(new Vector2(0.08f, 0.55f)); Pts.Add(new Vector2(0.08f, 0.2f)); Arc(0.28f, 0.2f, 0.2f, 0.2f, 180f, 360f, 10); Pts.Add(new Vector2(0.48f, 0.55f)); End();
                        Begin(); Pts.Add(new Vector2(0.48f, 0.55f)); Pts.Add(new Vector2(0.5f, 0.0f)); End();
                        return 0.68f;
                    case 'i':
                        Begin(); Pts.Add(new Vector2(0.12f, 0.52f)); Pts.Add(new Vector2(0.12f, 0.02f)); End();
                        Begin(); Arc(0.12f, 0.74f, 0.035f, 0.035f, 0f, 360f, 8); End();
                        return 0.34f;
                    case 't':
                        Begin(); Pts.Add(new Vector2(0.2f, 0.88f)); Pts.Add(new Vector2(0.2f, 0.12f)); Arc(0.34f, 0.12f, 0.14f, 0.12f, 180f, 300f, 6); End();
                        Begin(); Pts.Add(new Vector2(0.02f, 0.56f)); Pts.Add(new Vector2(0.44f, 0.58f)); End();
                        return 0.5f;
                    case 'r':
                        Begin(); Pts.Add(new Vector2(0.08f, 0.55f)); Pts.Add(new Vector2(0.08f, 0.0f)); End();
                        Begin(); Pts.Add(new Vector2(0.08f, 0.32f)); Arc(0.3f, 0.32f, 0.22f, 0.22f, 180f, 70f, 8); End();
                        return 0.46f;
                    case 'e':
                        Begin(); Pts.Add(new Vector2(0.06f, 0.28f)); Pts.Add(new Vector2(0.46f, 0.3f)); Arc(0.26f, 0.28f, 0.21f, 0.27f, 10f, 320f, 22); End();
                        return 0.56f;
                    case 'd':
                        Begin(); Arc(0.26f, 0.27f, 0.21f, 0.27f, 90f, 450f, 24); End();
                        Begin(); Pts.Add(new Vector2(0.47f, 0.95f)); Pts.Add(new Vector2(0.47f, 0.0f)); End();
                        return 0.6f;
                    case 's':
                        Begin(); Arc(0.24f, 0.41f, 0.17f, 0.13f, 20f, 270f, 10); Arc(0.24f, 0.14f, 0.18f, 0.14f, 90f, -160f, 10); End();
                        return 0.5f;
                    case 'S':
                        Begin(); Arc(0.36f, 0.72f, 0.26f, 0.23f, 25f, 270f, 16); Arc(0.36f, 0.25f, 0.29f, 0.25f, 90f, -165f, 16); End();
                        return 0.74f;
                    case 'G':
                        Begin(); Arc(0.42f, 0.5f, 0.38f, 0.48f, 50f, 330f, 32); Pts.Add(new Vector2(0.79f, 0.44f)); Pts.Add(new Vector2(0.52f, 0.44f)); End();
                        return 0.86f;
                    case 'v':
                        Begin(); Pts.Add(new Vector2(0.04f, 0.55f)); Pts.Add(new Vector2(0.26f, 0.0f)); Pts.Add(new Vector2(0.48f, 0.55f)); End();
                        return 0.54f;
                    case 'U':
                        Begin(); Pts.Add(new Vector2(0.08f, 0.95f)); Pts.Add(new Vector2(0.08f, 0.3f)); Arc(0.38f, 0.3f, 0.3f, 0.3f, 180f, 360f, 14); Pts.Add(new Vector2(0.68f, 0.95f)); End();
                        return 0.8f;
                    case 'p':
                        Begin(); Pts.Add(new Vector2(0.08f, 0.55f)); Pts.Add(new Vector2(0.08f, -0.35f)); End();
                        Begin(); Arc(0.29f, 0.27f, 0.21f, 0.27f, 180f, 540f, 24); End();
                        return 0.58f;
                    case 'n':
                        Begin(); Pts.Add(new Vector2(0.08f, 0.55f)); Pts.Add(new Vector2(0.08f, 0.0f)); End();
                        Begin(); Pts.Add(new Vector2(0.08f, 0.3f)); Arc(0.28f, 0.32f, 0.2f, 0.22f, 175f, 0f, 10); Pts.Add(new Vector2(0.48f, 0.0f)); End();
                        return 0.62f;
                    case 'g':
                        Begin(); Arc(0.26f, 0.3f, 0.2f, 0.25f, 90f, 450f, 24); End();
                        Begin(); Pts.Add(new Vector2(0.46f, 0.55f)); Pts.Add(new Vector2(0.46f, -0.1f)); Arc(0.27f, -0.1f, 0.19f, 0.18f, 0f, -165f, 10); End();
                        return 0.6f;
                    default:
                        return 0.4f;
                }
            }

            /// <summary>Writes <paramref name="text"/> centred at <paramref name="center"/> (0..1 texture space),
            /// <paramref name="height"/> tall (fraction of the width), slanted and turned by <paramref name="degrees"/>.</summary>
            public static void Draw(Brush brush, string text, Vector2 center, float height, float degrees, int width, float radius, Color c, float alpha, float aspect = 1f)
            {
                var strokes = new List<List<Vector2>>();
                float total = 0f;
                foreach (char ch in text) total += Letter(ch, strokes);
                float rot = degrees * Mathf.Deg2Rad, cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
                float x = -total * 0.5f;
                var path = new List<Vector2>();
                foreach (char ch in text)
                {
                    float adv = Letter(ch, strokes);
                    foreach (var s in strokes)
                    {
                        path.Clear();
                        foreach (var p in s)
                        {
                            float lx = (x + p.x + p.y * 0.18f) * height, ly = (p.y - 0.4f) * height;
                            float rx = lx * cs - ly * sn, ry = lx * sn + ly * cs;
                            path.Add(new Vector2(center.x + rx, center.y + ry / aspect));
                        }
                        brush.Stroke(path, width, radius, c, alpha, aspect);
                    }
                    x += adv;
                }
            }
        }
    }
}

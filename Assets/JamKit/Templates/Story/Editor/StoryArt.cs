using System;
using System.Collections.Generic;
using JamKit.EditorTools;
using UnityEngine;

namespace JamKit.Narrative.EditorTools
{
    /// <summary>
    /// The sample's placeholder art, drawn in code: gradient backgrounds with simple shapes, character
    /// silhouettes with expression layers, coloured CGs and item icons. Written as PNGs into
    /// Templates/Story/Art/Generated (only when the pixels change). Replace any of them by pointing the
    /// story config at your own sprite; the generator never overwrites a reference you changed.
    /// </summary>
    public static class StoryArt
    {
        public const string Dir = StoryBuilder.Root + "/Art/Generated";
        const int BgW = 640, BgH = 360;

        public sealed class Set
        {
            public readonly Dictionary<string, Sprite> backgrounds = new Dictionary<string, Sprite>();
            public readonly Dictionary<string, Sprite> cgs = new Dictionary<string, Sprite>();
            public readonly Dictionary<string, Sprite> items = new Dictionary<string, Sprite>();
            public readonly Dictionary<string, Sprite> portraitBases = new Dictionary<string, Sprite>();
            public readonly Dictionary<string, Dictionary<string, Sprite>> expressions = new Dictionary<string, Dictionary<string, Sprite>>();
        }

        public static Set MakeAll()
        {
            var s = new Set();
            s.backgrounds["platform_night"] = Save("bg_platform_night", PlatformNight());
            s.backgrounds["waiting_room"] = Save("bg_waiting_room", WaitingRoom());
            s.backgrounds["office"] = Save("bg_office", Office());
            s.backgrounds["platform_dawn"] = Save("bg_platform_dawn", PlatformDawn());
            s.backgrounds["train_interior"] = Save("bg_train_interior", TrainInterior());
            s.backgrounds["street_rain"] = Save("bg_street_rain", StreetRain());
            s.cgs["cg_photo"] = Save("cg_photo", Photo());
            s.cgs["cg_train"] = Save("cg_train", TrainLights());
            s.cgs["cg_umbrella"] = Save("cg_umbrella", Umbrella());
            s.cgs["cg_ticket"] = Save("cg_ticket", TicketCg());
            s.items["brass_key"] = Save("item_brass_key", KeyIcon(), 128f);
            s.items["ticket"] = Save("item_ticket", TicketIcon(), 128f);
            s.items["photograph"] = Save("item_photograph", PhotoIcon(), 128f);
            Character(s, "iris", new Color(0.93f, 0.76f, 0.22f), new Color(0.22f, 0.16f, 0.14f), new Color(0.96f, 0.84f, 0.74f), false,
                "neutral", "smile", "sad", "surprised", "knowing");
            Character(s, "bram", new Color(0.16f, 0.22f, 0.38f), new Color(0.55f, 0.55f, 0.58f), new Color(0.88f, 0.70f, 0.58f), true,
                "neutral", "stern", "laugh");
            return s;
        }

        static Sprite Save(string name, Canvas c, float ppu = 100f) =>
            SceneBuilder.MakeSprite(name, c.w, c.h, c.ToUnity(), ppu, Dir);

        // ---------- A tiny painter (top-left origin) ----------

        sealed class Canvas
        {
            public readonly int w, h;
            readonly Color[] _px;

            public Canvas(int w, int h, Color fill)
            {
                this.w = w; this.h = h;
                _px = new Color[w * h];
                for (int i = 0; i < _px.Length; i++) _px[i] = fill;
            }

            public void Blend(int x, int y, Color c)
            {
                if (x < 0 || y < 0 || x >= w || y >= h || c.a <= 0f) return;
                int i = y * w + x;
                var d = _px[i];
                float a = c.a + d.a * (1f - c.a);
                if (a <= 0f) { _px[i] = Color.clear; return; }
                _px[i] = new Color((c.r * c.a + d.r * d.a * (1f - c.a)) / a, (c.g * c.a + d.g * d.a * (1f - c.a)) / a, (c.b * c.a + d.b * d.a * (1f - c.a)) / a, a);
            }

            public void VGradient(float y0, float y1, Color top, Color bottom)
            {
                int a = (int)(y0 * h), b = (int)(y1 * h);
                for (int y = Math.Max(0, a); y < Math.Min(h, b); y++)
                {
                    var c = Color.Lerp(top, bottom, (y - a) / (float)Math.Max(1, b - a));
                    for (int x = 0; x < w; x++) Blend(x, y, c);
                }
            }

            public void Rect(float x0, float y0, float x1, float y1, Color c)
            {
                for (int y = (int)(y0 * h); y < (int)(y1 * h); y++)
                    for (int x = (int)(x0 * w); x < (int)(x1 * w); x++) Blend(x, y, c);
            }

            /// <summary>An ellipse in pixels (centre, radii).</summary>
            public void Ellipse(float cx, float cy, float rx, float ry, Color c, float soft = 1.2f)
            {
                for (int y = (int)(cy - ry - 2); y <= (int)(cy + ry + 2); y++)
                    for (int x = (int)(cx - rx - 2); x <= (int)(cx + rx + 2); x++)
                    {
                        float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                        float d = (float)Math.Sqrt(dx * dx + dy * dy);
                        float edge = Mathf.Clamp01((1f - d) * Math.Min(rx, ry) / soft);
                        if (edge > 0f) Blend(x, y, new Color(c.r, c.g, c.b, c.a * edge));
                    }
            }

            /// <summary>A soft radial glow (fraction coordinates, radius as a fraction of the width).</summary>
            public void Glow(float fx, float fy, float fr, Color c)
            {
                float cx = fx * w, cy = fy * h, r = fr * w;
                for (int y = (int)(cy - r); y <= (int)(cy + r); y++)
                    for (int x = (int)(cx - r); x <= (int)(cx + r); x++)
                    {
                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / r;
                        if (d >= 1f) continue;
                        float k = (1f - d) * (1f - d);
                        Blend(x, y, new Color(c.r, c.g, c.b, c.a * k));
                    }
            }

            /// <summary>A filled polygon (fraction coordinates).</summary>
            public void Poly(Color c, params float[] xy)
            {
                int n = xy.Length / 2;
                float minY = 1f, maxY = 0f;
                for (int i = 0; i < n; i++) { minY = Math.Min(minY, xy[i * 2 + 1]); maxY = Math.Max(maxY, xy[i * 2 + 1]); }
                var xs = new List<float>();
                for (int y = (int)(minY * h); y <= (int)(maxY * h); y++)
                {
                    float py = (y + 0.5f) / h;
                    xs.Clear();
                    for (int i = 0, j = n - 1; i < n; j = i++)
                    {
                        float yi = xy[i * 2 + 1], yj = xy[j * 2 + 1];
                        if ((yi > py) != (yj > py)) xs.Add(xy[i * 2] + (py - yi) / (yj - yi) * (xy[j * 2] - xy[i * 2]));
                    }
                    xs.Sort();
                    for (int k = 0; k + 1 < xs.Count; k += 2)
                        for (int x = (int)(xs[k] * w); x < (int)(xs[k + 1] * w); x++) Blend(x, y, c);
                }
            }

            public void Line(float x0, float y0, float x1, float y1, Color c, float width = 1f)
            {
                float ax = x0 * w, ay = y0 * h, bx = x1 * w, by = y1 * h;
                int steps = (int)Math.Max(Math.Abs(bx - ax), Math.Abs(by - ay)) + 1;
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    float x = Mathf.Lerp(ax, bx, t), y = Mathf.Lerp(ay, by, t);
                    if (width <= 1f) Blend((int)x, (int)y, c);
                    else Ellipse(x, y, width * 0.5f, width * 0.5f, c, 0.8f);
                }
            }

            public void Rain(int count, Color c, int seed, float length = 0.05f)
            {
                var rng = new System.Random(seed);
                for (int i = 0; i < count; i++)
                {
                    float x = (float)rng.NextDouble(), y = (float)rng.NextDouble();
                    Line(x, y, x - length * 0.25f, y + length, c);
                }
            }

            public void Noise(float amount, int seed)
            {
                var rng = new System.Random(seed);
                for (int i = 0; i < _px.Length; i++)
                {
                    float n = ((float)rng.NextDouble() - 0.5f) * amount;
                    var p = _px[i];
                    _px[i] = new Color(Mathf.Clamp01(p.r + n), Mathf.Clamp01(p.g + n), Mathf.Clamp01(p.b + n), p.a);
                }
            }

            public void Tint(Color mul)
            {
                for (int i = 0; i < _px.Length; i++) _px[i] = new Color(_px[i].r * mul.r, _px[i].g * mul.g, _px[i].b * mul.b, _px[i].a);
            }

            /// <summary>Unity's textures start at the bottom row.</summary>
            public Color[] ToUnity()
            {
                var o = new Color[_px.Length];
                for (int y = 0; y < h; y++) Array.Copy(_px, y * w, o, (h - 1 - y) * w, w);
                return o;
            }
        }

        static Color C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
        static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);

        // ---------- Backgrounds ----------

        static Canvas PlatformNight()
        {
            var c = new Canvas(BgW, BgH, C("#0a0f22"));
            c.VGradient(0f, 0.62f, C("#070b1c"), C("#1d2b4f"));
            // Town far away.
            c.Poly(C("#0d1430"), 0f, 0.5f, 0.08f, 0.44f, 0.12f, 0.47f, 0.2f, 0.4f, 0.26f, 0.46f, 0.36f, 0.43f, 0.45f, 0.48f, 0.6f, 0.42f, 0.72f, 0.47f, 0.85f, 0.41f, 1f, 0.46f, 1f, 0.62f, 0f, 0.62f);
            for (int i = 0; i < 14; i++) c.Rect(0.05f + i * 0.068f, 0.5f, 0.058f + i * 0.068f, 0.512f, A(C("#f6c869"), i % 3 == 0 ? 0.8f : 0.25f));
            // Canopy.
            c.Rect(0f, 0f, 1f, 0.09f, C("#05070f"));
            c.Rect(0f, 0.09f, 1f, 0.105f, C("#1a2133"));
            // Platform and edge.
            c.VGradient(0.62f, 1f, C("#2b3346"), C("#151a26"));
            c.Rect(0f, 0.66f, 1f, 0.68f, C("#d8b13f"));
            c.Rect(0f, 0.62f, 1f, 0.66f, C("#3a4258"));
            // Lamp post with glow.
            c.Rect(0.71f, 0.16f, 0.718f, 0.86f, C("#0b0d14"));
            c.Glow(0.714f, 0.17f, 0.22f, A(C("#ffd27a"), 0.45f));
            c.Ellipse(0.714f * BgW, 0.17f * BgH, 9f, 6f, C("#fff1c4"));
            c.Glow(0.714f, 0.86f, 0.12f, A(C("#ffd27a"), 0.18f));
            // Departure board.
            c.Rect(0.32f, 0.13f, 0.56f, 0.24f, C("#0b0c10"));
            for (int i = 0; i < 9; i++) c.Rect(0.335f + i * 0.024f, 0.165f, 0.352f + i * 0.024f, 0.205f, A(C("#ffb347"), 0.9f));
            // Bench.
            c.Rect(0.12f, 0.7f, 0.3f, 0.725f, C("#0c0f17"));
            c.Rect(0.13f, 0.725f, 0.14f, 0.8f, C("#0c0f17"));
            c.Rect(0.28f, 0.725f, 0.29f, 0.8f, C("#0c0f17"));
            c.Rain(420, A(C("#9fb4ff"), 0.22f), 11);
            c.Noise(0.025f, 3);
            return c;
        }

        static Canvas WaitingRoom()
        {
            var c = new Canvas(BgW, BgH, C("#3a2516"));
            c.VGradient(0f, 0.7f, C("#6b4426"), C("#9a6a3a"));
            c.Rect(0f, 0.55f, 1f, 0.7f, C("#4a2d19"));
            c.Rect(0f, 0.55f, 1f, 0.565f, C("#2c1a0e"));
            c.VGradient(0.7f, 1f, C("#3b2a1f"), C("#21160f"));
            // Window with rain.
            c.Rect(0.08f, 0.14f, 0.3f, 0.48f, C("#2b1a0e"));
            c.Rect(0.095f, 0.16f, 0.285f, 0.46f, C("#16203b"));
            c.Rect(0.187f, 0.16f, 0.193f, 0.46f, C("#2b1a0e"));
            c.Rect(0.095f, 0.305f, 0.285f, 0.312f, C("#2b1a0e"));
            // Door with frosted glass (LOST PROPERTY).
            c.Rect(0.62f, 0.18f, 0.8f, 0.9f, C("#4d3220"));
            c.Rect(0.65f, 0.24f, 0.77f, 0.48f, A(C("#e9e2cf"), 0.85f));
            c.Rect(0.665f, 0.33f, 0.755f, 0.36f, A(C("#b08a3a"), 0.9f));
            c.Ellipse(0.785f * BgW, 0.58f * BgH, 4f, 4f, C("#d8b13f"));
            // Bench and radiator.
            c.Rect(0.33f, 0.68f, 0.58f, 0.72f, C("#2a180c"));
            c.Rect(0.33f, 0.58f, 0.58f, 0.6f, C("#2a180c"));
            c.Rect(0.35f, 0.72f, 0.36f, 0.82f, C("#1c1008"));
            c.Rect(0.55f, 0.72f, 0.56f, 0.82f, C("#1c1008"));
            for (int i = 0; i < 7; i++) c.Rect(0.85f + i * 0.018f, 0.6f, 0.86f + i * 0.018f, 0.78f, C("#8a8c8f"));
            c.Glow(0.45f, 0.05f, 0.35f, A(C("#ffcf7a"), 0.35f));
            c.Rain(60, A(C("#9fb4ff"), 0.25f), 5, 0.03f);
            c.Noise(0.02f, 9);
            return c;
        }

        /// <summary>The office; hotspot shapes in Data/Hotspots/Office.asset match these areas.</summary>
        static Canvas Office()
        {
            var c = new Canvas(BgW, BgH, C("#273228"));
            c.VGradient(0f, 0.62f, C("#33433a"), C("#3f5244"));
            c.VGradient(0.62f, 1f, C("#3a2a1c"), C("#22180f"));
            c.Rect(0f, 0.6f, 1f, 0.62f, C("#1d241f"));
            // Door (0.03-0.17).
            c.Rect(0.03f, 0.18f, 0.17f, 0.95f, C("#4a3322"));
            c.Rect(0.05f, 0.24f, 0.15f, 0.46f, A(C("#e9e2cf"), 0.75f));
            c.Ellipse(0.155f * BgW, 0.6f * BgH, 4f, 4f, C("#d8b13f"));
            // Coat stand with a grey coat (0.20-0.30).
            c.Rect(0.248f, 0.2f, 0.256f, 0.88f, C("#2a1c12"));
            c.Rect(0.22f, 0.86f, 0.285f, 0.88f, C("#2a1c12"));
            c.Poly(C("#6f7276"), 0.215f, 0.3f, 0.252f, 0.24f, 0.29f, 0.3f, 0.3f, 0.74f, 0.205f, 0.74f);
            c.Rect(0.248f, 0.3f, 0.256f, 0.72f, A(C("#4c4f53"), 0.8f));
            // Notice board (0.36-0.56, 0.14-0.40).
            c.Rect(0.36f, 0.14f, 0.56f, 0.4f, C("#8a5d33"));
            c.Rect(0.37f, 0.16f, 0.55f, 0.38f, C("#b0794a"));
            c.Rect(0.385f, 0.18f, 0.44f, 0.26f, C("#efe6d2"));
            c.Rect(0.45f, 0.19f, 0.53f, 0.3f, C("#f3edd8"));
            c.Rect(0.39f, 0.28f, 0.47f, 0.36f, C("#d7e3ef"));
            c.Line(0.455f, 0.25f, 0.525f, 0.25f, C("#c0392b"), 2f);
            // Window (0.62-0.80, 0.12-0.42).
            c.Rect(0.62f, 0.12f, 0.8f, 0.42f, C("#1c1a14"));
            c.Rect(0.635f, 0.14f, 0.785f, 0.4f, C("#141c33"));
            c.Glow(0.71f, 0.3f, 0.05f, A(C("#ffd27a"), 0.6f));
            // Shelves (0.82-0.98, 0.10-0.92).
            c.Rect(0.82f, 0.1f, 0.98f, 0.92f, C("#3d2a1b"));
            var rng = new System.Random(4);
            for (int s = 0; s < 5; s++)
            {
                float y = 0.12f + s * 0.16f;
                c.Rect(0.82f, y + 0.13f, 0.98f, y + 0.145f, C("#26190f"));
                for (int b = 0; b < 4; b++)
                {
                    float x = 0.83f + b * 0.037f;
                    var col = Color.HSVToRGB((float)rng.NextDouble(), 0.4f, 0.55f);
                    c.Rect(x, y + 0.05f + (float)rng.NextDouble() * 0.03f, x + 0.03f, y + 0.13f, col);
                }
            }
            c.Rect(0.89f, 0.6f, 0.93f, 0.68f, C("#efe6d2"));
            // Desk (0.34-0.74, 0.56-0.92) with a drawer (0.48-0.60, 0.66-0.74).
            c.Poly(C("#5a3b22"), 0.34f, 0.6f, 0.74f, 0.6f, 0.76f, 0.66f, 0.32f, 0.66f);
            c.Rect(0.34f, 0.66f, 0.74f, 0.92f, C("#4a301b"));
            c.Rect(0.48f, 0.66f, 0.6f, 0.74f, C("#3a2413"));
            c.Rect(0.49f, 0.67f, 0.59f, 0.73f, C("#5c3d24"));
            c.Ellipse(0.54f * BgW, 0.7f * BgH, 3f, 4f, C("#d8b13f"));
            c.Rect(0.42f, 0.555f, 0.5f, 0.6f, C("#e9e2cf"));
            c.Ellipse(0.62f * BgW, 0.58f * BgH, 10f, 6f, C("#d9d4c7"));
            c.Glow(0.5f, 0.0f, 0.4f, A(C("#fff1c4"), 0.25f));
            c.Noise(0.02f, 21);
            return c;
        }

        static Canvas PlatformDawn()
        {
            var c = new Canvas(BgW, BgH, C("#f2a46b"));
            c.VGradient(0f, 0.62f, C("#5b6fb5"), C("#f6b37f"));
            c.Ellipse(0.66f * BgW, 0.52f * BgH, 38f, 38f, A(C("#fff1c4"), 0.95f));
            c.Glow(0.66f, 0.52f, 0.3f, A(C("#ffd9a0"), 0.5f));
            c.Poly(C("#3c3354"), 0f, 0.55f, 0.1f, 0.5f, 0.2f, 0.54f, 0.32f, 0.47f, 0.42f, 0.53f, 0.55f, 0.5f, 0.7f, 0.56f, 0.85f, 0.49f, 1f, 0.53f, 1f, 0.62f, 0f, 0.62f);
            c.VGradient(0.62f, 1f, C("#7a6a78"), C("#3e3546"));
            c.Rect(0f, 0.66f, 1f, 0.68f, C("#e7c25a"));
            c.Noise(0.02f, 7);
            return c;
        }

        static Canvas TrainInterior()
        {
            var c = new Canvas(BgW, BgH, C("#3b2c1e"));
            c.VGradient(0f, 1f, C("#5b452c"), C("#2a1e14"));
            for (int i = 0; i < 4; i++)
            {
                float x = 0.06f + i * 0.24f;
                c.Rect(x, 0.16f, x + 0.18f, 0.48f, C("#0d1428"));
                c.Line(x + 0.02f, 0.3f, x + 0.16f, 0.27f, A(C("#ffd27a"), 0.6f), 2f);
            }
            for (int i = 0; i < 4; i++)
            {
                float x = 0.04f + i * 0.25f;
                c.Rect(x, 0.56f, x + 0.2f, 0.88f, C("#7a2e2e"));
                c.Rect(x, 0.52f, x + 0.2f, 0.58f, C("#5e2222"));
            }
            c.Rect(0f, 0f, 1f, 0.08f, C("#e9dcc0"));
            c.Glow(0.5f, 0.04f, 0.6f, A(C("#fff1c4"), 0.3f));
            c.Noise(0.02f, 13);
            return c;
        }

        static Canvas StreetRain()
        {
            var c = new Canvas(BgW, BgH, C("#0d1226"));
            c.VGradient(0f, 0.55f, C("#0a0e20"), C("#26304f"));
            for (int i = 0; i < 6; i++)
            {
                float x = i * 0.17f, top = 0.18f + (i % 3) * 0.06f;
                c.Poly(C("#111629"), x, 0.6f, x, top, x + 0.075f, top - 0.08f, x + 0.15f, top, x + 0.15f, 0.6f);
                c.Rect(x + 0.03f, top + 0.08f, x + 0.06f, top + 0.14f, A(C("#f6c869"), i % 2 == 0 ? 0.85f : 0.3f));
                c.Rect(x + 0.09f, top + 0.08f, x + 0.12f, top + 0.14f, A(C("#f6c869"), i % 2 == 1 ? 0.85f : 0.3f));
            }
            c.VGradient(0.6f, 1f, C("#1d2235"), C("#0b0d16"));
            for (int i = 0; i < 3; i++)
            {
                float x = 0.15f + i * 0.35f;
                c.Rect(x, 0.3f, x + 0.006f, 0.82f, C("#06070c"));
                c.Glow(x + 0.003f, 0.3f, 0.12f, A(C("#ffd27a"), 0.4f));
                c.Glow(x + 0.003f, 0.88f, 0.08f, A(C("#ffd27a"), 0.2f));
            }
            c.Rain(380, A(C("#9fb4ff"), 0.2f), 17);
            c.Noise(0.025f, 19);
            return c;
        }

        // ---------- CGs ----------

        static Canvas Photo()
        {
            var c = new Canvas(BgW, BgH, C("#1b130c"));
            c.Rect(0.22f, 0.08f, 0.78f, 0.92f, C("#efe3c8"));
            c.Rect(0.25f, 0.12f, 0.75f, 0.8f, C("#b4935f"));
            c.VGradient(0.12f, 0.5f, C("#d9bf8c"), C("#b4935f"));
            c.Rect(0.25f, 0.6f, 0.75f, 0.8f, C("#8a6c45"));
            c.Rect(0.25f, 0.62f, 0.75f, 0.635f, C("#e2c47a"));
            Figure(c, 0.42f, 0.62f, 0.13f, C("#3d2b1a"), C("#d4b06a"));
            Figure(c, 0.56f, 0.62f, 0.14f, C("#3d2b1a"), C("#4a3a2a"));
            c.Glow(0.6f, 0.2f, 0.2f, A(C("#fff1c4"), 0.3f));
            c.Noise(0.05f, 27);
            return c;
        }

        /// <summary>A standing person, seen small (for CGs): head, body, in fractions.</summary>
        static void Figure(Canvas c, float x, float feetY, float height, Color hair, Color coat)
        {
            float headR = height * 0.12f;
            c.Poly(coat, x - height * 0.13f, feetY, x - height * 0.1f, feetY - height * 0.72f, x + height * 0.1f, feetY - height * 0.72f, x + height * 0.13f, feetY);
            c.Ellipse(x * c.w, (feetY - height * 0.82f) * c.h, headR * c.h, headR * c.h, C("#e7c9a5"));
            c.Ellipse(x * c.w, (feetY - height * 0.86f) * c.h, headR * c.h * 1.05f, headR * c.h * 0.7f, hair);
        }

        static Canvas TrainLights()
        {
            var c = new Canvas(BgW, BgH, C("#04050a"));
            c.VGradient(0f, 1f, C("#05060d"), C("#10131f"));
            c.Poly(C("#1a1d2a"), 0.3f, 1f, 0.47f, 0.5f, 0.53f, 0.5f, 0.7f, 1f);
            c.Line(0.38f, 1f, 0.49f, 0.5f, C("#5b6070"), 2f);
            c.Line(0.62f, 1f, 0.51f, 0.5f, C("#5b6070"), 2f);
            c.Glow(0.46f, 0.47f, 0.25f, A(C("#fff3c0"), 0.7f));
            c.Glow(0.54f, 0.47f, 0.25f, A(C("#fff3c0"), 0.7f));
            c.Ellipse(0.46f * BgW, 0.47f * BgH, 9f, 9f, Color.white);
            c.Ellipse(0.54f * BgW, 0.47f * BgH, 9f, 9f, Color.white);
            c.Rain(300, A(C("#c8d4ff"), 0.25f), 31);
            return c;
        }

        static Canvas Umbrella()
        {
            var c = new Canvas(BgW, BgH, C("#3d3a5a"));
            c.VGradient(0f, 0.7f, C("#53588a"), C("#e6a77a"));
            c.VGradient(0.7f, 1f, C("#3a3550"), C("#1f1c2c"));
            Figure(c, 0.45f, 0.86f, 0.42f, C("#2a1e16"), C("#e8c547"));
            Figure(c, 0.56f, 0.86f, 0.44f, C("#1c1612"), C("#3b4a5e"));
            c.Poly(C("#1e2433"), 0.34f, 0.36f, 0.5f, 0.22f, 0.66f, 0.36f);
            c.Line(0.5f, 0.24f, 0.505f, 0.6f, C("#111"), 2f);
            c.Rain(250, A(C("#e8ecff"), 0.25f), 37);
            c.Noise(0.02f, 41);
            return c;
        }

        static Canvas TicketCg()
        {
            var c = new Canvas(BgW, BgH, C("#2a1c12"));
            c.Glow(0.5f, 0.45f, 0.6f, A(C("#ffd27a"), 0.25f));
            c.Rect(0.3f, 0.3f, 0.7f, 0.66f, C("#b98e5a"));
            c.Rect(0.32f, 0.33f, 0.68f, 0.63f, C("#c9a06a"));
            c.Rect(0.35f, 0.38f, 0.62f, 0.43f, C("#4a3020"));
            c.Rect(0.35f, 0.47f, 0.55f, 0.5f, C("#6b4a30"));
            c.Rect(0.35f, 0.53f, 0.5f, 0.56f, C("#6b4a30"));
            c.Ellipse(0.62f * BgW, 0.55f * BgH, 14f, 14f, A(C("#8a2a2a"), 0.7f));
            c.Noise(0.03f, 43);
            return c;
        }

        // ---------- Item icons ----------

        static Canvas KeyIcon()
        {
            var c = new Canvas(128, 128, Color.clear);
            c.Ellipse(40f, 64f, 24f, 24f, C("#d8b13f"));
            c.Rect(0.45f, 0.46f, 0.92f, 0.54f, C("#d8b13f"));
            c.Rect(0.78f, 0.54f, 0.84f, 0.68f, C("#d8b13f"));
            c.Rect(0.66f, 0.54f, 0.71f, 0.64f, C("#d8b13f"));
            c.Ellipse(40f, 64f, 10f, 10f, C("#1b1b22"));
            return c;
        }

        static Canvas TicketIcon()
        {
            var c = new Canvas(128, 128, Color.clear);
            c.Rect(0.12f, 0.28f, 0.88f, 0.72f, C("#b98e5a"));
            c.Rect(0.16f, 0.32f, 0.84f, 0.68f, C("#d3ab73"));
            c.Rect(0.22f, 0.4f, 0.7f, 0.47f, C("#4a3020"));
            c.Rect(0.22f, 0.53f, 0.55f, 0.58f, C("#6b4a30"));
            return c;
        }

        static Canvas PhotoIcon()
        {
            var c = new Canvas(128, 128, Color.clear);
            c.Rect(0.16f, 0.12f, 0.84f, 0.88f, C("#efe3c8"));
            c.Rect(0.22f, 0.18f, 0.78f, 0.7f, C("#b4935f"));
            c.Ellipse(0.42f * 128, 0.42f * 128, 8f, 8f, C("#3d2b1a"));
            c.Ellipse(0.6f * 128, 0.42f * 128, 8f, 8f, C("#3d2b1a"));
            c.Rect(0.36f, 0.48f, 0.48f, 0.7f, C("#d4b06a"));
            c.Rect(0.54f, 0.48f, 0.66f, 0.7f, C("#4a3a2a"));
            return c;
        }

        // ---------- Characters ----------

        const int CharW = 360, CharH = 600;

        /// <summary>A waist-up silhouette (base) plus one transparent layer per expression (eyes, brows, mouth).</summary>
        static void Character(Set s, string id, Color coat, Color hair, Color skin, bool cap, params string[] expressions)
        {
            var b = new Canvas(CharW, CharH, Color.clear);
            float cx = CharW * 0.5f;
            // Body: shoulders to the bottom edge.
            b.Poly(coat, 0.08f, 1f, 0.12f, 0.58f, 0.3f, 0.47f, 0.7f, 0.47f, 0.88f, 0.58f, 0.92f, 1f);
            b.Poly(new Color(coat.r * 0.8f, coat.g * 0.8f, coat.b * 0.8f), 0.46f, 0.5f, 0.54f, 0.5f, 0.52f, 1f, 0.48f, 1f);
            // Neck and head.
            b.Rect(0.44f, 0.38f, 0.56f, 0.5f, skin * 0.92f);
            b.Ellipse(cx, CharH * 0.29f, CharW * 0.17f, CharH * 0.12f, skin);
            if (cap)
            {
                b.Ellipse(cx, CharH * 0.2f, CharW * 0.19f, CharH * 0.06f, coat);
                b.Rect(0.3f, 0.2f, 0.7f, 0.215f, new Color(0.08f, 0.08f, 0.1f));
                b.Ellipse(cx, CharH * 0.36f, CharW * 0.13f, CharH * 0.05f, hair);
            }
            else
            {
                b.Ellipse(cx, CharH * 0.22f, CharW * 0.19f, CharH * 0.09f, hair);
                b.Rect(0.31f, 0.22f, 0.36f, 0.42f, hair);
                b.Rect(0.64f, 0.22f, 0.69f, 0.42f, hair);
                // Raincoat hood resting on the shoulders.
                b.Poly(coat * 0.85f, 0.26f, 0.5f, 0.34f, 0.42f, 0.66f, 0.42f, 0.74f, 0.5f);
            }
            s.portraitBases[id] = Save("char_" + id + "_base", b);
            var dict = new Dictionary<string, Sprite>();
            foreach (var e in expressions) dict[e] = Save("char_" + id + "_" + e, Face(e));
            s.expressions[id] = dict;
        }

        static Canvas Face(string mood)
        {
            var f = new Canvas(CharW, CharH, Color.clear);
            var ink = new Color(0.12f, 0.08f, 0.08f);
            float ey = 0.28f, lx = 0.43f, rx = 0.57f;
            void Eye(float x, float ry) => f.Ellipse(x * CharW, ey * CharH, 6f, ry, ink);
            void Brow(float x, float tilt) => f.Line(x - 0.035f, ey - 0.035f + tilt, x + 0.035f, ey - 0.035f - tilt, ink, 3f);
            switch (mood)
            {
                case "smile":
                    Eye(lx, 6f); Eye(rx, 6f);
                    f.Line(0.45f, 0.335f, 0.5f, 0.35f, ink, 3f); f.Line(0.5f, 0.35f, 0.55f, 0.335f, ink, 3f);
                    break;
                case "sad":
                    Eye(lx, 5f); Eye(rx, 5f);
                    Brow(lx, -0.012f); Brow(rx, 0.012f);
                    f.Line(0.46f, 0.355f, 0.5f, 0.343f, ink, 3f); f.Line(0.5f, 0.343f, 0.54f, 0.355f, ink, 3f);
                    break;
                case "surprised":
                    f.Ellipse(lx * CharW, ey * CharH, 8f, 9f, ink); f.Ellipse(rx * CharW, ey * CharH, 8f, 9f, ink);
                    Brow(lx, 0f); Brow(rx, 0f);
                    f.Ellipse(0.5f * CharW, 0.35f * CharH, 9f, 11f, ink);
                    break;
                case "knowing":
                    f.Line(lx - 0.025f, ey, lx + 0.025f, ey, ink, 4f); f.Line(rx - 0.025f, ey, rx + 0.025f, ey, ink, 4f);
                    f.Line(0.46f, 0.345f, 0.53f, 0.34f, ink, 3f); f.Line(0.53f, 0.34f, 0.555f, 0.33f, ink, 3f);
                    break;
                case "stern":
                    Eye(lx, 5f); Eye(rx, 5f);
                    Brow(lx, 0.012f); Brow(rx, -0.012f);
                    f.Line(0.46f, 0.348f, 0.54f, 0.348f, ink, 4f);
                    break;
                case "laugh":
                    f.Line(lx - 0.025f, ey + 0.005f, lx, ey - 0.008f, ink, 3f); f.Line(lx, ey - 0.008f, lx + 0.025f, ey + 0.005f, ink, 3f);
                    f.Line(rx - 0.025f, ey + 0.005f, rx, ey - 0.008f, ink, 3f); f.Line(rx, ey - 0.008f, rx + 0.025f, ey + 0.005f, ink, 3f);
                    f.Poly(ink, 0.45f, 0.335f, 0.55f, 0.335f, 0.5f, 0.37f);
                    break;
                default: // neutral
                    Eye(lx, 6f); Eye(rx, 6f);
                    f.Line(0.47f, 0.345f, 0.53f, 0.345f, ink, 3f);
                    break;
            }
            return f;
        }
    }
}

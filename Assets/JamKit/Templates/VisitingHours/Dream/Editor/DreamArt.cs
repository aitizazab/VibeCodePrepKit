using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// The dream escape's pictures: the cast as cut-out billboards and the static wasp's mask. Style-E art is used
    /// when it exists (another agent and the creator make it; this code never writes there):
    /// <c>Templates/VisitingHours/Art/Dream/&lt;name&gt;.png</c>, then <c>Templates/VisitingHours/Dream/Art/&lt;name&gt;.png</c>.
    /// Until then a PLACEHOLDER is drawn in code into <c>Dream/Generated/</c>: a cutesy doodle (big round head,
    /// dot eyes, tiny mouth, wobbly plum outline, a white paper border like a cut-out sticker). The build logs
    /// "JAMKIT_DREAM art: ..." with which is which.
    /// </summary>
    public static class DreamArt
    {
        public const string ArtDir = "Assets/JamKit/Templates/VisitingHours/Art/Dream";
        public const string LocalArtDir = DreamEscapeBuilder.Root + "/Art";
        public const string GeneratedDir = DreamEscapeBuilder.Root + "/Generated";

        public const string Walt = "dream_walt";
        public const string NeighbourRoses = "dream_neighbour_roses";
        public const string NeighbourGrill = "dream_neighbour_grill";
        public const string Dog = "dream_dog";
        /// <summary>Night 2's house: Lena and Theo (PLACEHOLDER doodles until style-E dream art is dropped in under these names).</summary>
        public const string Lena = "dream_lena";
        public const string Theo = "dream_theo";
        public const string WaspMask = "dream_wasp_mask";
        /// <summary>Night 1's Elias (at the barbecue, at the stove in the window): the cast's sticker style, a blue apron and tongs.</summary>
        public const string Elias = "dream_elias";
        /// <summary>Night 2's story pictures (style-E files only; no placeholders are drawn: the plain cards stay):
        /// the photo wall <c>dream_pic_photo_1</c> to <c>_6</c>, the fridge calendar, Theo's drawing.</summary>
        public const string PicPhoto = "dream_pic_photo_";
        public const string PicCalendar = "dream_pic_calendar";
        public const string PicTheoDrawing = "dream_pic_theo_drawing";
        /// <summary>Night 2's re-plot (docs/NIGHTS-1-2.md; PLACEHOLDER art from tools/art/vh_night2_props.py): the calendar
        /// with "Pancakes!" on every Sunday and stars on Lena's Thursdays, Theo's "THE BAD DREAM", Nadia's place card.</summary>
        public const string PicCalendarN2 = "dream_pic_calendar_n2", PicBadDream = "dream_pic_bad_dream", PicPlaceCard = "dream_pic_place_card";

        public static string NameOf(DreamCast who)
        {
            switch (who)
            {
                case DreamCast.Walt: return Walt;
                case DreamCast.NeighbourRoses: return NeighbourRoses;
                case DreamCast.NeighbourGrill: return NeighbourGrill;
                default: return Dog;
            }
        }

        /// <summary>The real picture's path if it exists, else null.</summary>
        public static string RealPath(string name)
        {
            foreach (var dir in new[] { ArtDir, LocalArtDir })
            {
                string p = dir + "/" + name + ".png";
                if (File.Exists(p)) return p;
            }
            return null;
        }

        /// <summary>The texture for <paramref name="name"/>: the real art, or a generated placeholder.</summary>
        public static Texture2D Get(string name, out bool placeholder)
        {
            string real = RealPath(name);
            placeholder = real == null;
            string path = real ?? GeneratedDir + "/" + name + ".png";
            if (placeholder)
            {
                DreamEscapeBuilder.EnsureFolder(GeneratedDir);
                var canvas = Draw(name);
                File.WriteAllBytes(path, canvas.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(canvas);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null)
            {
                bool mask = name == WaspMask;
                bool changed = imp.textureType != TextureImporterType.Default || imp.alphaIsTransparency == mask || imp.sRGBTexture == mask
                    || imp.wrapMode != TextureWrapMode.Clamp || imp.maxTextureSize != 1024 || !imp.mipmapEnabled;
                if (changed)
                {
                    imp.textureType = TextureImporterType.Default;
                    imp.alphaIsTransparency = !mask;
                    imp.sRGBTexture = !mask;
                    imp.wrapMode = TextureWrapMode.Clamp;
                    imp.mipmapEnabled = true;
                    imp.maxTextureSize = 1024;
                    imp.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---------- The placeholders ----------

        static readonly Color Ink = new Color(0.36f, 0.27f, 0.45f, 1f);
        static readonly Color Skin = new Color(0.99f, 0.84f, 0.74f, 1f);
        static readonly Color Paper = new Color(1f, 0.99f, 0.96f, 1f);

        static Texture2D Draw(string name)
        {
            switch (name)
            {
                case Walt: return Finish(DrawWalt());
                case NeighbourRoses: return Finish(DrawRoses());
                case NeighbourGrill: return Finish(DrawGrill());
                case Dog: return Finish(DrawDog());
                case Lena: return Finish(DrawLena());
                case Theo: return Finish(DrawTheo());
                default: return DrawWasp();
            }
        }

        sealed class Canvas
        {
            public readonly int w, h;
            public readonly Color[] px;
            public Canvas(int w, int h) { this.w = w; this.h = h; px = new Color[w * h]; }

            float Wobble(float x, float y) => 0.9f * Mathf.Sin(x * 0.11f + y * 0.07f) + 0.6f * Mathf.Sin(y * 0.19f - x * 0.05f);

            void Shape(float x0, float y0, float x1, float y1, Func<float, float, float> sd, Color fill, float line)
            {
                int ix0 = Mathf.Max(0, (int)(x0 - line - 3)), ix1 = Mathf.Min(w - 1, (int)(x1 + line + 3));
                int iy0 = Mathf.Max(0, (int)(y0 - line - 3)), iy1 = Mathf.Min(h - 1, (int)(y1 + line + 3));
                for (int y = iy0; y <= iy1; y++)
                    for (int x = ix0; x <= ix1; x++)
                    {
                        float d = sd(x + 0.5f, y + 0.5f) + Wobble(x, y);
                        if (d > line) continue;
                        Color c = d < 0f ? fill : Ink;
                        float a = Mathf.Clamp01(line - d);
                        int i = y * w + x;
                        px[i] = Color.Lerp(px[i], c, a);
                        if (a > 0f) px[i].a = Mathf.Max(px[i].a, a);
                    }
            }

            public void Ellipse(float cx, float cy, float rx, float ry, Color fill, float line = 3f) =>
                Shape(cx - rx, cy - ry, cx + rx, cy + ry, (x, y) =>
                {
                    float dx = (x - cx) / rx, dy = (y - cy) / ry;
                    return (Mathf.Sqrt(dx * dx + dy * dy) - 1f) * Mathf.Min(rx, ry);
                }, fill, line);

            public void Box(float x0, float y0, float x1, float y1, float r, Color fill, float line = 3f) =>
                Shape(x0, y0, x1, y1, (x, y) =>
                {
                    float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f;
                    float qx = Mathf.Abs(x - cx) - ((x1 - x0) * 0.5f - r), qy = Mathf.Abs(y - cy) - ((y1 - y0) * 0.5f - r);
                    float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
                    return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
                }, fill, line);

            /// <summary>A trapezoid-ish body: wider at the bottom.</summary>
            public void Dress(float cx, float yTop, float yBottom, float topHalf, float bottomHalf, Color fill, float line = 3f) =>
                Shape(cx - bottomHalf, yBottom, cx + bottomHalf, yTop, (x, y) =>
                {
                    float t = Mathf.InverseLerp(yBottom, yTop, y);
                    float half = Mathf.Lerp(bottomHalf, topHalf, t) - 6f;
                    float dx = Mathf.Abs(x - cx) - half, dy = Mathf.Max(y - yTop, yBottom - y);
                    return Mathf.Max(dx, dy) - 6f;
                }, fill, line);

            public void Capsule(float ax, float ay, float bx, float by, float r, Color fill, float line = 3f) =>
                Shape(Mathf.Min(ax, bx) - r, Mathf.Min(ay, by) - r, Mathf.Max(ax, bx) + r, Mathf.Max(ay, by) + r, (x, y) =>
                {
                    float px0 = x - ax, py0 = y - ay, bax = bx - ax, bay = by - ay;
                    float t = Mathf.Clamp01((px0 * bax + py0 * bay) / Mathf.Max(1e-4f, bax * bax + bay * bay));
                    float dx = px0 - bax * t, dy = py0 - bay * t;
                    return Mathf.Sqrt(dx * dx + dy * dy) - r;
                }, fill, line);

            public void Dot(float cx, float cy, float r, Color c) => Ellipse(cx, cy, r, r, c, 0.6f);

            /// <summary>Eyes (round, a touch too round), a tiny mouth and blush.</summary>
            public void Face(float cx, float cy, float s, bool smile = true)
            {
                Dot(cx - 0.32f * s, cy + 0.05f * s, 0.09f * s, Ink);
                Dot(cx + 0.32f * s, cy + 0.05f * s, 0.09f * s, Ink);
                Dot(cx - 0.29f * s, cy + 0.09f * s, 0.025f * s, Color.white);
                Dot(cx + 0.35f * s, cy + 0.09f * s, 0.025f * s, Color.white);
                Ellipse(cx - 0.5f * s, cy - 0.2f * s, 0.12f * s, 0.07f * s, new Color(1f, 0.66f, 0.7f, 1f), 0f);
                Ellipse(cx + 0.5f * s, cy - 0.2f * s, 0.12f * s, 0.07f * s, new Color(1f, 0.66f, 0.7f, 1f), 0f);
                if (smile) Capsule(cx - 0.07f * s, cy - 0.27f * s, cx + 0.07f * s, cy - 0.27f * s, 0.022f * s, Ink, 0f);
                else Dot(cx, cy - 0.27f * s, 0.035f * s, Ink);
            }

            public Texture2D ToTexture()
            {
                var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
                t.SetPixels(px);
                t.Apply();
                return t;
            }
        }

        /// <summary>A white paper border round the figure, like a cut-out.</summary>
        static Texture2D Finish(Canvas c)
        {
            const int border = 7;
            var src = (Color[])c.px.Clone();
            for (int y = 0; y < c.h; y++)
                for (int x = 0; x < c.w; x++)
                {
                    int i = y * c.w + x;
                    if (src[i].a > 0.5f) continue;
                    float near = 0f;
                    for (int dy = -border; dy <= border && near < 1f; dy++)
                        for (int dx = -border; dx <= border; dx++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx < 0 || yy < 0 || xx >= c.w || yy >= c.h) continue;
                            float r = Mathf.Sqrt(dx * dx + dy * dy);
                            if (r > border) continue;
                            if (src[yy * c.w + xx].a > 0.5f) { near = Mathf.Max(near, Mathf.Clamp01(border + 0.5f - r)); if (near >= 1f) break; }
                        }
                    if (near <= 0f) continue;
                    var p = Color.Lerp(Paper, src[i], src[i].a);
                    p.a = Mathf.Max(src[i].a, near);
                    c.px[i] = p;
                }
            return c.ToTexture();
        }

        static Canvas DrawWalt()
        {
            var c = new Canvas(256, 560);
            var shirt = new Color(0.62f, 0.72f, 0.88f, 1f);
            var apron = new Color(0.78f, 0.72f, 0.62f, 1f);
            var trousers = new Color(0.45f, 0.42f, 0.58f, 1f);
            var cap = new Color(0.66f, 0.52f, 0.46f, 1f);
            c.Capsule(108, 30, 108, 150, 15, trousers);
            c.Capsule(148, 30, 148, 150, 15, trousers);
            c.Ellipse(104, 26, 24, 12, Ink, 2f);
            c.Ellipse(152, 26, 24, 12, Ink, 2f);
            c.Box(78, 140, 178, 330, 24, shirt);
            c.Box(92, 150, 164, 300, 12, apron);
            c.Capsule(80, 300, 62, 200, 13, shirt);
            c.Capsule(176, 300, 196, 200, 13, shirt);
            c.Ellipse(62, 192, 13, 13, Skin);
            c.Ellipse(198, 192, 13, 13, Skin);
            c.Box(186, 150, 214, 196, 6, new Color(0.93f, 0.36f, 0.38f, 1f)); // the red rag
            c.Ellipse(128, 410, 78, 80, Skin);
            c.Face(128, 410, 80, false);
            c.Ellipse(128, 372, 34, 12, new Color(0.98f, 0.98f, 0.98f, 1f), 2.5f); // moustache
            c.Box(54, 452, 190, 488, 16, cap);
            c.Box(150, 452, 214, 468, 8, cap);
            return c;
        }

        static Canvas DrawRoses()
        {
            var c = new Canvas(256, 520);
            var dress = new Color(0.98f, 0.7f, 0.78f, 1f);
            var hair = new Color(0.62f, 0.45f, 0.4f, 1f);
            var hat = new Color(0.99f, 0.9f, 0.62f, 1f);
            c.Capsule(112, 26, 112, 110, 12, Skin);
            c.Capsule(144, 26, 144, 110, 12, Skin);
            c.Dress(128, 290, 90, 46, 92, dress);
            c.Capsule(86, 270, 70, 190, 12, dress);
            c.Capsule(170, 270, 186, 190, 12, dress);
            c.Ellipse(128, 372, 78, 80, hair);
            c.Ellipse(128, 368, 66, 68, Skin);
            c.Face(128, 362, 70, true);
            c.Ellipse(128, 430, 118, 22, hat);
            c.Ellipse(128, 450, 60, 34, hat);
            c.Capsule(80, 452, 176, 452, 6, new Color(0.98f, 0.55f, 0.62f, 1f), 1.5f);
            // A bunch of roses held in front.
            c.Capsule(128, 160, 128, 230, 4, new Color(0.45f, 0.72f, 0.45f, 1f), 1.5f);
            c.Ellipse(114, 236, 16, 16, new Color(0.93f, 0.33f, 0.42f, 1f));
            c.Ellipse(142, 240, 16, 16, new Color(0.96f, 0.45f, 0.52f, 1f));
            c.Ellipse(128, 256, 16, 16, new Color(0.9f, 0.3f, 0.4f, 1f));
            c.Ellipse(128, 186, 20, 12, Skin);
            return c;
        }

        /// <summary>Lena (PLACEHOLDER, only drawn if Art/Dream/dream_lena.png is missing; the real file is her approved
        /// design, CHARACTERS.md "4-a"): blonde, long loose waves, a cream turtleneck, slim wine-red trousers, dark boots, a
        /// notebook held loosely in one hand. No apron (2026-10-12: the old placeholder had one).</summary>
        static Canvas DrawLena()
        {
            var c = new Canvas(256, 540);
            var trousers = new Color(0.55f, 0.16f, 0.22f, 1f);
            var top = new Color(0.98f, 0.94f, 0.86f, 1f);
            var boots = new Color(0.36f, 0.24f, 0.2f, 1f);
            var notebook = new Color(0.3f, 0.22f, 0.2f, 1f);
            var hair = new Color(0.98f, 0.86f, 0.55f, 1f);
            c.Capsule(112, 30, 112, 160, 15, trousers);
            c.Capsule(144, 30, 144, 160, 15, trousers);
            c.Ellipse(110, 24, 18, 12, boots);
            c.Ellipse(146, 24, 18, 12, boots);
            c.Box(90, 150, 166, 310, 18, top);
            c.Capsule(86, 300, 74, 190, 12, top);
            c.Capsule(170, 300, 182, 190, 12, top);
            c.Ellipse(74, 184, 12, 12, Skin);
            c.Ellipse(182, 184, 12, 12, Skin);
            c.Box(170, 168, 204, 214, 4, notebook);
            // Long loose waves, falling past the shoulders.
            c.Ellipse(128, 380, 86, 92, hair);
            c.Capsule(62, 380, 70, 270, 22, hair);
            c.Capsule(194, 380, 186, 270, 22, hair);
            c.Ellipse(128, 372, 64, 68, Skin);
            c.Face(128, 366, 68, true);
            return c;
        }

        /// <summary>Theo (PLACEHOLDER): ten, a striped T-shirt, shorts, scruffy brown hair; smaller than the grown-ups.</summary>
        static Canvas DrawTheo()
        {
            var c = new Canvas(256, 420);
            var shirt = new Color(0.62f, 0.8f, 0.98f, 1f);
            var stripe = new Color(0.98f, 0.98f, 1f, 1f);
            var shorts = new Color(0.98f, 0.72f, 0.5f, 1f);
            var hair = new Color(0.5f, 0.36f, 0.3f, 1f);
            c.Capsule(108, 24, 108, 100, 13, Skin);
            c.Capsule(148, 24, 148, 100, 13, Skin);
            c.Box(84, 96, 172, 150, 18, shorts);
            c.Box(78, 140, 178, 250, 26, shirt);
            c.Box(80, 176, 176, 190, 2, stripe);
            c.Box(80, 212, 176, 226, 2, stripe);
            c.Capsule(80, 236, 58, 160, 12, shirt);
            c.Capsule(176, 236, 198, 160, 12, shirt);
            c.Ellipse(58, 154, 12, 12, Skin);
            c.Ellipse(198, 154, 12, 12, Skin);
            c.Ellipse(128, 316, 74, 72, hair);
            c.Ellipse(128, 306, 62, 62, Skin);
            c.Face(128, 300, 64, false);
            return c;
        }

        static Canvas DrawGrill()
        {
            var c = new Canvas(256, 540);
            var shirt = new Color(0.62f, 0.86f, 0.8f, 1f);
            var apron = new Color(1f, 0.99f, 0.97f, 1f);
            var trousers = new Color(0.55f, 0.55f, 0.72f, 1f);
            var hair = new Color(0.48f, 0.38f, 0.36f, 1f);
            c.Capsule(108, 30, 108, 140, 16, trousers);
            c.Capsule(148, 30, 148, 140, 16, trousers);
            c.Box(70, 130, 186, 320, 30, shirt);
            c.Box(90, 140, 166, 300, 14, apron);
            c.Capsule(72, 290, 52, 200, 14, shirt);
            c.Capsule(184, 290, 206, 220, 14, shirt);
            c.Ellipse(52, 192, 14, 14, Skin);
            // The spatula, raised.
            c.Capsule(206, 222, 224, 300, 4, new Color(0.6f, 0.58f, 0.66f, 1f), 1.5f);
            c.Box(210, 296, 240, 330, 4, new Color(0.82f, 0.82f, 0.88f, 1f), 2f);
            c.Ellipse(206, 222, 14, 14, Skin);
            c.Ellipse(128, 400, 82, 80, Skin);
            c.Face(128, 398, 80, true);
            c.Box(60, 440, 196, 478, 18, hair);
            return c;
        }

        static Canvas DrawDog()
        {
            var c = new Canvas(384, 300);
            var fur = new Color(0.99f, 0.86f, 0.62f, 1f);
            var ear = new Color(0.86f, 0.64f, 0.48f, 1f);
            c.Capsule(300, 120, 340, 170, 10, fur);           // tail
            c.Capsule(150, 30, 150, 90, 15, fur);
            c.Capsule(250, 30, 250, 90, 15, fur);
            c.Ellipse(205, 110, 105, 62, fur);                 // body
            c.Ellipse(118, 186, 78, 72, fur);                  // head
            c.Ellipse(60, 186, 22, 50, ear);
            c.Ellipse(176, 186, 22, 50, ear);
            c.Face(118, 194, 70, true);
            c.Ellipse(118, 166, 13, 10, Ink, 1f);              // nose
            c.Box(150, 120, 260, 134, 6, new Color(0.62f, 0.78f, 0.98f, 1f), 2f); // collar
            return c;
        }

        static Texture2D DrawWasp()
        {
            const int s = 128;
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false, true);
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float u = (x + 0.5f) / s * 2f - 1f, v = (y + 0.5f) / s * 2f - 1f;
                    float body = 0f;
                    body = Mathf.Max(body, Blob(u, v, -0.42f, 0.05f, 0.17f, 0.17f));
                    body = Mathf.Max(body, Blob(u, v, -0.14f, 0.05f, 0.2f, 0.18f));
                    body = Mathf.Max(body, Blob(u, v, 0.32f, 0.0f, 0.38f, 0.24f));
                    float stripe = body > 0.5f && u > 0.05f && Mathf.Repeat((u - 0.05f) * 6f, 1f) < 0.45f ? 1f : 0f;
                    float wings = Mathf.Max(Blob(u, v, -0.05f, 0.42f, 0.22f, 0.36f), Blob(u, v, 0.18f, 0.38f, 0.18f, 0.3f));
                    px[y * s + x] = new Color(body, wings * (1f - body), stripe, 1f);
                }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        static float Blob(float u, float v, float cx, float cy, float rx, float ry)
        {
            float dx = (u - cx) / rx, dy = (v - cy) / ry;
            return Mathf.Clamp01((1f - Mathf.Sqrt(dx * dx + dy * dy)) * 6f);
        }
    }
}

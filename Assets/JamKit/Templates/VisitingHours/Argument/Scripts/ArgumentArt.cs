using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.Argument
{
    /// <summary>
    /// The opponent's sprite and the screen's fonts (docs/ARGUMENT-PROTOTYPE.md, "Art").
    ///
    /// SPRITES (a drop-in): Assets/JamKit/Templates/VisitingHours/Argument/Resources/ArgumentArt/&lt;who&gt;_&lt;expression&gt;.png,
    /// for Lena: lena_antagonistic (the pose she starts the battle in, <see cref="Opening"/>), lena_neutral, lena_angry,
    /// lena_rattled, lena_softened, lena_hurt, lena_pleading. Portrait 2:3, 1024 x 1536 px,
    /// transparent background, the figure from the waist (or hips) up filling most of the height, facing slightly
    /// right (towards the claims). The import settings are set for you (ArgumentArtImport: Default texture, no
    /// mipmaps, no power-of-two rescale, alpha is transparency); a GUID-only .meta is fine. A missing expression falls back to
    /// &lt;who&gt;_neutral; with no file at all, a sketch-style silhouette is drawn in code, a different face per
    /// expression.
    ///
    /// THE BOIL (<see cref="ArgumentBoil"/>): the view distorts whatever <see cref="Sprite"/> returns with Lily's
    /// hand-drawn boil, so it stacks with a treatment. F10 in the battle cycles its presets.
    ///
    /// TREATMENTS (a hook, no picker yet): with <see cref="SetTreatment"/>("bw") the slot first looks for
    /// &lt;who&gt;_bw_&lt;expression&gt;.png and falls back to the plain files above.
    ///
    /// FONTS (open licence, OFL; licences in Argument/Licenses): DM Serif Display for the claim, headings and names;
    /// Crimson Text (regular, italic, bold) for everything else. In Resources/ArgumentFonts/.
    /// </summary>
    public static class ArgumentArt
    {
        public const string ArtFolder = "ArgumentArt/";
        public const string FontFolder = "ArgumentFonts/";
        /// <summary>Every mood a sprite can be drawn in. Lena: the first seven. Walt adds kind and sad (no angry, hurt or
        /// pleading: his battle file maps those). A missing drawing falls back to that opponent's neutral.</summary>
        public static readonly string[] Expressions = { "antagonistic", "neutral", "angry", "rattled", "softened", "hurt", "pleading", "kind", "sad" };
        /// <summary>Every opponent starts the battle in this pose, until a reply changes her expression (no file: neutral).</summary>
        public const string Opening = "antagonistic";

        static string _treatment = "";
        /// <summary>The sprite treatment in use ("" for the plain files).</summary>
        public static string Treatment => _treatment;
        /// <summary>Uses &lt;who&gt;_&lt;treatment&gt;_&lt;expression&gt; files where they exist ("" or null: the plain ones).</summary>
        public static void SetTreatment(string treatment)
        {
            treatment = (treatment ?? "").ToLowerInvariant();
            if (treatment == _treatment) return;
            _treatment = treatment;
            _sprites.Clear();
        }
        public const int PlaceholderWidth = 384, PlaceholderHeight = 576;

        static readonly Dictionary<string, Texture2D> _sprites = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Font> _fonts = new Dictionary<string, Font>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _sprites.Clear();
            _fonts.Clear();
            _treatment = "";
        }

        /// <summary>The sprite for <paramref name="who"/> in <paramref name="expression"/> (a file, else neutral's file, else the placeholder).</summary>
        public static Texture2D Sprite(string who, string expression, out bool placeholder)
        {
            who = (who ?? "").ToLowerInvariant();
            expression = Known(expression);
            string key = who + "_" + expression;
            placeholder = false;
            if (_sprites.TryGetValue(key, out var t) && t != null)
            {
                placeholder = t.name.StartsWith("placeholder", StringComparison.Ordinal);
                return t;
            }
            if (_treatment.Length > 0) t = Load(who + "_" + _treatment + "_" + expression);
            if (t == null) t = Load(key);
            if (t == null && _treatment.Length > 0) t = Load(who + "_" + _treatment + "_neutral");
            if (t == null) t = Load(who + "_neutral");
            if (t == null)
            {
                t = Placeholder(expression);
                placeholder = true;
            }
            _sprites[key] = t;
            return t;
        }

        /// <summary>One file from Resources/ArgumentArt: the texture, or (if someone re-imports it as a Sprite) the sprite's texture.</summary>
        static Texture2D Load(string name)
        {
            var t = Resources.Load<Texture2D>(ArtFolder + name);
            if (t != null) return t;
            var s = Resources.Load<Sprite>(ArtFolder + name);
            return s != null ? s.texture : null;
        }

        /// <summary>Drops the loaded sprites (the next call loads again; tests, and after new files land).</summary>
        public static void Forget() => _sprites.Clear();

        /// <summary>The real art for an expression is in Resources (no fallback, no placeholder; tests).</summary>
        public static bool HasFile(string who, string expression) => Load((who ?? "").ToLowerInvariant() + "_" + expression) != null;

        static string Known(string expression)
        {
            foreach (var e in Expressions) if (e == expression) return e;
            return "neutral";
        }

        // ---------- Fonts ----------

        public static Font Display => Font("DMSerifDisplay-Regular");
        public static Font Body => Font("CrimsonText-Regular");
        public static Font BodyItalic => Font("CrimsonText-Italic");
        public static Font BodyBold => Font("CrimsonText-Bold");

        static Font Font(string name)
        {
            if (_fonts.TryGetValue(name, out var f) && f != null) return f;
            f = Resources.Load<Font>(FontFolder + name);
            _fonts[name] = f;
            return f;
        }

        /// <summary>Sets an element's font (no-op if the font file is missing: the theme's font stays).</summary>
        public static void Use(VisualElement e, Font font)
        {
            if (e == null || font == null) return;
            e.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(font));
        }

        // ---------- The placeholder: a pencil silhouette with a face per expression ----------

        static Texture2D Placeholder(string expression)
        {
            int w = PlaceholderWidth, h = PlaceholderHeight;
            var px = new Color32[w * h];
            var ink = new Color32(214, 206, 230, 255);
            var fill = new Color32(42, 36, 54, 255);
            var hatch = new Color32(78, 70, 98, 255);
            // The figure: shoulders and torso (a rounded trapezoid), neck, head, hair gathered in a bun.
            float cx = w * 0.5f, headY = h * 0.70f, headR = w * 0.16f;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float fx = x, fy = y;
                    bool inside = false;
                    // torso: from the bottom up to the shoulders (y < 0.47 h), wider at the bottom
                    float shoulderY = h * 0.47f;
                    if (fy < shoulderY)
                    {
                        float half = Mathf.Lerp(w * 0.45f, w * 0.34f, fy / shoulderY);
                        float round = fy > shoulderY - w * 0.08f ? Mathf.Sqrt(Mathf.Max(0f, 1f - Sq((fy - (shoulderY - w * 0.08f)) / (w * 0.08f)))) : 1f;
                        inside = Mathf.Abs(fx - cx) < half * Mathf.Lerp(0.55f, 1f, round);
                    }
                    // neck
                    if (!inside && fy >= shoulderY - 4 && fy < headY - headR * 0.6f) inside = Mathf.Abs(fx - cx) < w * 0.07f;
                    // head
                    if (!inside) inside = Sq((fx - cx) / (headR * 0.9f)) + Sq((fy - headY) / headR) < 1f;
                    // hair: a bun at the back of the head and a fringe line
                    if (!inside) inside = Sq((fx - (cx + headR * 0.75f)) / (headR * 0.45f)) + Sq((fy - (headY + headR * 0.55f)) / (headR * 0.42f)) < 1f;
                    if (!inside) { px[y * w + x] = new Color32(0, 0, 0, 0); continue; }
                    // pencil hatching across the fill
                    bool line = ((x + y * 2) % 9) == 0 || ((x * 3 - y) % 23) == 0;
                    px[y * w + x] = line ? hatch : fill;
                }
            }
            // A sketchy outline: brighten pixels at the silhouette's edge.
            var outline = new List<int>();
            for (int y = 1; y < h - 1; y++)
                for (int x = 1; x < w - 1; x++)
                {
                    int i = y * w + x;
                    if (px[i].a == 0) continue;
                    if (px[i - 1].a == 0 || px[i + 1].a == 0 || px[i - w].a == 0 || px[i + w].a == 0) outline.Add(i);
                }
            foreach (var i in outline) { px[i] = ink; if (i + 1 < px.Length && px[i + 1].a != 0) px[i + 1] = ink; }

            // The face, one per expression (brows, eyes, mouth), in light pencil.
            float eyeY = headY + headR * 0.1f, eyeDx = headR * 0.38f, mouthY = headY - headR * 0.45f;
            float browTilt, browLift, mouthCurve, eyeOpen;
            switch (expression)
            {
                case "antagonistic": browTilt = 0.25f; browLift = -0.02f; mouthCurve = 0.2f; eyeOpen = 0.55f; break;
                case "angry": browTilt = 0.35f; browLift = 0.0f; mouthCurve = -0.5f; eyeOpen = 0.5f; break;
                case "rattled": browTilt = -0.3f; browLift = 0.12f; mouthCurve = -0.15f; eyeOpen = 1.2f; break;
                case "softened": browTilt = -0.12f; browLift = 0.05f; mouthCurve = 0.45f; eyeOpen = 0.6f; break;
                case "hurt": browTilt = -0.45f; browLift = 0.08f; mouthCurve = -0.35f; eyeOpen = 0.7f; break;
                case "kind": browTilt = -0.08f; browLift = 0.06f; mouthCurve = 0.35f; eyeOpen = 0.75f; break;
                case "sad": browTilt = -0.4f; browLift = 0.1f; mouthCurve = -0.25f; eyeOpen = 0.65f; break;
                case "pleading": browTilt = -0.5f; browLift = 0.15f; mouthCurve = -0.1f; eyeOpen = 1.05f; break;
                default: browTilt = 0f; browLift = 0.04f; mouthCurve = 0.05f; eyeOpen = 0.9f; break;
            }
            for (int side = -1; side <= 1; side += 2)
            {
                float ex = cx + side * eyeDx;
                // brow: a short line, inner end lower when angry (tilt > 0), higher when worried (tilt < 0)
                for (float t = -1f; t <= 1f; t += 0.02f)
                {
                    float bx = ex + t * headR * 0.24f;
                    float inner = -side * t; // +1 at the inner end
                    float by = eyeY + headR * (0.28f + browLift) - inner * browTilt * headR * 0.16f;
                    Dot(px, w, h, bx, by, 2, ink);
                }
                // eye: a small ellipse, squinted or wide
                for (float a = 0f; a < Mathf.PI * 2f; a += 0.08f)
                    Dot(px, w, h, ex + Mathf.Cos(a) * headR * 0.11f, eyeY + Mathf.Sin(a) * headR * 0.06f * eyeOpen, 1, ink);
            }
            // mouth: a curve, up for a smile, down for anger and hurt
            for (float t = -1f; t <= 1f; t += 0.02f)
                Dot(px, w, h, cx + t * headR * 0.3f, mouthY + mouthCurve * headR * 0.18f * (1f - t * t) - mouthCurve * headR * 0.09f, 2, ink);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "placeholder_" + expression, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        static float Sq(float v) => v * v;

        static void Dot(Color32[] px, int w, int h, float x, float y, int r, Color32 c)
        {
            int ix = Mathf.RoundToInt(x), iy = Mathf.RoundToInt(y);
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int X = ix + dx, Y = iy + dy;
                    if (X < 0 || Y < 0 || X >= w || Y >= h || dx * dx + dy * dy > r * r) continue;
                    px[Y * w + X] = c;
                }
        }
    }
}

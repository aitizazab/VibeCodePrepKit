using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The HUD's shared style (docs/SKYLINE-STYLE.md, docs/SKYLINE-HUD.md): the ink keyline, the palette, and the display-font
    /// loader. Nothing here is a rounded box; the HUD is slanted hard-edged shapes with ink keylines and a display face.
    /// <para><b>The ammo font is one setting</b>: <see cref="AmmoFontName"/> ("LuckiestGuy", the creator's pick on 2026-10-14;
    /// "Anton" and "Bangers" are also on disk; Anton is kept for a later "Condensed" UI option, digits set closer together). Change <see cref="AmmoFontDefault"/> to swap it for good, or launch with
    /// <c>-skyline-ammo-font Bangers</c> to try one in a still. Faces live under <c>ThirdParty/Fonts/Resources/SkylineFonts/</c>,
    /// each beside its licence; a face that fails to load logs <c>SKYLINE_HUD font missing</c> once and the project font is used.</para>
    /// </summary>
    public static class SkylineHudStyle
    {
        // ---- the ink and the palette (hex from docs/SKYLINE-STYLE.md and the plan's H2)
        public const float KeylinePx = 3f;
        public static readonly Color Ink = new Color32(0x16, 0x14, 0x1F, 255);
        public static readonly Color Chassis = new Color32(0xFF, 0x9E, 0x3F, 255);        // orange
        public static readonly Color ChassisHot = new Color32(0xFF, 0xE2, 0xB8, 255);     // the low-health pulse's peak
        public static readonly Color Plating = new Color32(0x8F, 0xD3, 0xFF, 255);        // light blue (the plating hue)
        public static readonly Color Green = new Color32(0x5C, 0xFF, 0x8A, 255);          // special ammo gained
        public static readonly Color Hostile = new Color32(0xFF, 0x2E, 0x63, 255);        // reserved for hostiles; the edge tint only
        public static readonly Color Paper = new Color32(0xF7, 0xF3, 0xE8, 255);          // warm white for digits and text
        public static readonly Color Track = new Color(0.086f, 0.078f, 0.122f, 0.80f);    // the empty part of a bar: ink, see-through

        // ---- the display font (one swappable setting)
        public const string AmmoFontDefault = "LuckiestGuy";
        /// <summary>The faces on disk: name to Resources path (no extension).</summary>
        public static readonly Dictionary<string, string> Faces = new Dictionary<string, string>
        {
            { "Anton", "SkylineFonts/Anton/Anton-Regular" },
            { "Bangers", "SkylineFonts/Bangers/Bangers-Regular" },
            { "LuckiestGuy", "SkylineFonts/LuckiestGuy/LuckiestGuy-Regular" },
        };

        static string _ammoFont;
        static readonly Dictionary<string, Font> Loaded = new Dictionary<string, Font>();
        static readonly HashSet<string> Warned = new HashSet<string>();

        /// <summary>The ammo digits' face: <c>-skyline-ammo-font NAME</c> if given, else <see cref="AmmoFontDefault"/>.</summary>
        public static string AmmoFontName
        {
            get => _ammoFont ?? (_ammoFont = SkylineRunArgs.Value("-skyline-ammo-font") ?? AmmoFontDefault);
            set => _ammoFont = value;
        }

        /// <summary>The named face, or null (logged once as <c>SKYLINE_HUD font missing</c>) when it isn't there.</summary>
        public static Font LoadFace(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (Loaded.TryGetValue(name, out var f) && f != null) return f;
            Font font = null;
            if (Faces.TryGetValue(name, out var path)) font = Resources.Load<Font>(path);
            if (font == null)
            {
                if (Warned.Add(name)) Debug.LogWarning("SKYLINE_HUD font missing: " + name + "; using the project font");
                return null;
            }
            Loaded[name] = font;
            return font;
        }

        /// <summary>Sets a label's face to the named display font (regular weight: these faces are already heavy). A missing
        /// face leaves the project font.</summary>
        public static void ApplyFace(IStyle style, string name)
        {
            var font = LoadFace(name);
            if (font == null) return;
            style.unityFontDefinition = FontDefinition.FromFont(font);
            style.unityFontStyleAndWeight = FontStyle.Normal;
        }

        public static void ZeroBox(IStyle s)
        {
            s.marginLeft = s.marginRight = s.marginTop = s.marginBottom = 0;
            s.paddingLeft = s.paddingRight = s.paddingTop = s.paddingBottom = 0;
        }

        /// <summary>A transparent, click-through, absolutely placed element.</summary>
        public static VisualElement Layer(VisualElement parent, string name = null)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            if (name != null) e.name = name;
            e.style.position = Position.Absolute;
            parent.Add(e);
            return e;
        }

        // ---- painting helpers (Painter2D)

        /// <summary>The slanted bar shape: the top edge leans right by <paramref name="skew"/> ("/" ends), hard corners.</summary>
        public static void Parallelogram(Painter2D p, float x, float y, float w, float h, float skew)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(x, y + h));
            p.LineTo(new Vector2(x + skew, y));
            p.LineTo(new Vector2(x + skew + w, y));
            p.LineTo(new Vector2(x + w, y + h));
            p.ClosePath();
        }

        public static void FillStroke(Painter2D p, Color fill, Color ink, float line)
        {
            p.fillColor = fill;
            p.Fill();
            if (line > 0f && ink.a > 0f)
            {
                p.strokeColor = ink;
                p.lineWidth = line;
                p.lineJoin = LineJoin.Miter;
                p.Stroke();
            }
        }
    }

    /// <summary>
    /// Text with a hard ink keyline round every glyph, made without the font's outline support (which a dynamic font may not
    /// have): the same string in the ink colour at eight offsets behind the front label. Optional rich text on the front
    /// label only (the back copies take the plain string), so a coloured speaker prefix keeps a dark edge.
    /// </summary>
    public sealed class KeylinedLabel : VisualElement
    {
        static readonly Vector2[] Dirs =
        {
            new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1),
            new Vector2(0.7071f, 0.7071f), new Vector2(-0.7071f, 0.7071f), new Vector2(0.7071f, -0.7071f), new Vector2(-0.7071f, -0.7071f),
        };

        readonly Label[] _back = new Label[Dirs.Length];
        public Label Front { get; }

        /// <param name="face">A display face name (<see cref="SkylineHudStyle.Faces"/>) or null for the project font.</param>
        public KeylinedLabel(float sizePx, float keylinePx, string face, bool bold = false)
        {
            pickingMode = PickingMode.Ignore;
            style.flexShrink = 0;
            for (int i = 0; i < _back.Length; i++)
            {
                var b = _back[i] = MakeLabel(sizePx, face, bold);
                b.style.color = SkylineHudStyle.Ink;
                b.style.position = Position.Absolute;
                b.style.left = Dirs[i].x * keylinePx;
                b.style.right = -Dirs[i].x * keylinePx;
                b.style.top = -Dirs[i].y * keylinePx;
                Add(b);
            }
            Front = MakeLabel(sizePx, face, bold);
            Front.enableRichText = true;
            Front.style.color = SkylineHudStyle.Paper;
            Add(Front);
        }

        static Label MakeLabel(float size, string face, bool bold)
        {
            var l = new Label { pickingMode = PickingMode.Ignore, enableRichText = false };
            SkylineHudStyle.ZeroBox(l.style);
            l.style.fontSize = size;
            l.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
            l.style.whiteSpace = WhiteSpace.NoWrap;
            if (face != null) SkylineHudStyle.ApplyFace(l.style, face);
            return l;
        }

        /// <summary>Sets the text. <paramref name="plain"/> (for the keyline copies) defaults to the same string.</summary>
        public void SetText(string rich, string plain = null)
        {
            Front.text = rich ?? "";
            string back = plain ?? rich ?? "";
            for (int i = 0; i < _back.Length; i++) _back[i].text = back;
        }

        public string Text => Front.text;

        public void SetColor(Color c) => Front.style.color = c;

        public void SetWrap(bool wrap, TextAnchor align)
        {
            var ws = wrap ? WhiteSpace.Normal : WhiteSpace.NoWrap;
            Front.style.whiteSpace = ws;
            Front.style.unityTextAlign = align;
            foreach (var b in _back)
            {
                b.style.whiteSpace = ws;
                b.style.unityTextAlign = align;
            }
        }

        public void SetItalic(bool italic)
        {
            var fs = italic ? FontStyle.Italic : FontStyle.Normal;
            Front.style.unityFontStyleAndWeight = fs;
            foreach (var b in _back) b.style.unityFontStyleAndWeight = fs;
        }
    }
}

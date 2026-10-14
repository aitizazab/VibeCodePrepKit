using System;
using System.Collections.Generic;
using JamKit.Skyline;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The movie-style subtitles (docs/SKYLINE-HUD.md, plan task H4): bottom centre, white text with a dark ink outline, no box,
    /// at most two lines, the speaker's name a coloured prefix ("DOT:"), CONCIERGE's PA lines in italics. What shows is the
    /// pure <see cref="SubtitleBuffer"/> (two lines, newest last, lines expire); this class draws it. The setting (Off /
    /// Dialogue / Dialogue + bot barks, and the size 28 / 34 / 42 px at the 1080p reference) is read from
    /// <see cref="SkylineSettings"/> every frame, so the pause menu changes it live. Lines fade in and out and slide a few
    /// pixels; a text wraps at 70% of the safe width and is never clipped. If a wrapped line would make more than two text
    /// rows, the older line leaves first. This replaces H2's interim subtitle line and the bubbles above the bots.
    /// </summary>
    public sealed class SkylineHudSubtitles
    {
        public const string BoxName = "hud-say", LineName = "hud-say-line";
        /// <summary>The block's gap above the bottom edge of the safe area, the slide of a fading line, and how wide the text may
        /// run (a fraction of the safe width).</summary>
        public const float BottomPx = 120f, SlidePx = 12f, WidthFraction = 0.70f;
        /// <summary>At most this many rows of text at once (a line that wraps to two rows fills the block alone).</summary>
        public const int MaxRows = 2;
        public const float FadeInSeconds = 0.18f, FadeOutSeconds = 0.32f, DefaultSeconds = 4f;
        /// <summary>A bot's bark is subtitled only while its bot is within this many metres of the player's camera (a bark from
        /// the far side of the hall is not worth reading).</summary>
        public const float BarkRange = 40f;
        /// <summary>An average glyph is about this fraction of the font size wide (a deliberate over-estimate, so the row count
        /// errs toward dropping the older line rather than clipping).</summary>
        const float GlyphWidth = 0.52f;
        /// <summary>The panel's reference width, used for the wrap width until a layout pass has measured the block.</summary>
        const float ReferenceWidth = 1920f;
        static readonly Color BarkPrefix = new Color(0.75f, 0.82f, 0.88f, 1f);

        sealed class Slot
        {
            public VisualElement el;
            public KeylinedLabel label;
            public SubtitleLine line;
            public float alpha;
            public int seq;
        }

        readonly SubtitleBuffer _buffer = new SubtitleBuffer();
        readonly HashSet<SubtitleLine> _barks = new HashSet<SubtitleLine>();
        readonly List<Slot> _slots = new List<Slot>();
        readonly List<SubtitleLine> _shown = new List<SubtitleLine>();
        VisualElement _box;
        float _now;
        int _seq;
        SubtitleMode _mode = SubtitleMode.Dialogue;
        SubtitleSize _size = SubtitleSize.Medium;
        bool _sizeDirty = true;

        /// <summary>The block holding the lines (named <see cref="BoxName"/>), or null before <see cref="Build"/>.</summary>
        public VisualElement Box => _box;

        /// <summary>How many lines are on screen or fading out right now.</summary>
        public int OnScreen
        {
            get
            {
                int n = 0;
                foreach (var s in _slots) if (s.line != null) n++;
                return n;
            }
        }

        /// <summary>The lines that are meant to be showing now (not fading out), oldest first.</summary>
        public IReadOnlyList<SubtitleLine> Lines => _shown;

        /// <summary>The text of the label on screen for <paramref name="line"/> (with its colour tag), or null.</summary>
        public string TextOf(SubtitleLine line)
        {
            foreach (var s in _slots) if (s.line == line) return s.label.Text;
            return null;
        }

        /// <summary>The element a line is drawn in, or null.</summary>
        public VisualElement ElementOf(SubtitleLine line)
        {
            foreach (var s in _slots) if (s.line == line) return s.el;
            return null;
        }

        public void Build(VisualElement safe)
        {
            _box = SkylineHudStyle.Layer(safe, BoxName);
            _box.style.left = Length.Percent(100f * (1f - WidthFraction) * 0.5f);
            _box.style.right = Length.Percent(100f * (1f - WidthFraction) * 0.5f);
            _box.style.bottom = BottomPx;
            _box.style.flexDirection = FlexDirection.Column;
            _box.style.alignItems = Align.Center;
            _box.style.justifyContent = Justify.FlexEnd;
            for (int i = 0; i < SubtitleBuffer.MaxLines + 1; i++)   // two lines, plus one still fading out
            {
                var el = new VisualElement { pickingMode = PickingMode.Ignore, name = LineName };
                el.style.width = Length.Percent(100);
                el.style.alignItems = Align.Center;
                el.style.display = DisplayStyle.None;
                el.style.opacity = 0f;
                _box.Add(el);
                _slots.Add(new Slot { el = el });
            }
            _sizeDirty = true;
        }

        // ------------------------------------------------------------------ saying

        /// <summary>A line of dialogue: the crew's teaching lines and CONCIERGE's PA lines (<paramref name="pa"/>, italics).</summary>
        public void Say(string speaker, string text, bool pa, float seconds) =>
            Add(speaker, text, pa, false, seconds > 0f ? seconds : DefaultSeconds);

        /// <summary>A bot's bark, kept only in "Dialogue + bot barks".</summary>
        public void Bark(string botName, string text, float seconds) =>
            Add(botName, text, false, true, seconds > 0f ? seconds : BarkSeconds(text));

        /// <summary>How long a bark is read (a floor, then the time to read its characters).</summary>
        public static float BarkSeconds(string text) => Mathf.Max(1.5f, 0.6f + (text?.Length ?? 0) / 14f);

        void Add(string speaker, string text, bool pa, bool bark, float seconds)
        {
            var mode = SkylineSettings.Options.subtitleMode;
            string name = (speaker ?? "").Replace("<", "").Trim().ToUpperInvariant();
            string body = (text ?? "").Replace("<", "").Trim();
            // The same words from the same speaker still on screen are kept on screen, not repeated.
            if (mode != SubtitleMode.Off)
                foreach (var l in _buffer.Visible(_now))
                    if (l.speaker == name && l.text == body)
                    {
                        if (l.expires < _now + seconds) l.expires = _now + seconds;
                        return;
                    }
            _buffer.Say(name, body, pa, bark, _now, seconds, mode);
            var vis = _buffer.Visible(_now);
            if (bark && vis.Count > 0)
            {
                var last = vis[vis.Count - 1];
                if (last.speaker == name && last.text == body) _barks.Add(last);
            }
        }

        /// <summary>Takes the dialogue down now (its line ended early); barks run their course.</summary>
        public void EndDialogue()
        {
            foreach (var l in _buffer.Visible(_now))
                if (!_barks.Contains(l)) l.expires = Math.Min(l.expires, _now);
        }

        /// <summary>Every line goes now (a restart).</summary>
        public void Clear()
        {
            _buffer.Clear();
            _barks.Clear();
        }

        // ------------------------------------------------------------------ the frame

        /// <summary>Advances the clock by <paramref name="dt"/> (unscaled seconds), follows the settings, and fades the lines.</summary>
        public void Tick(float dt)
        {
            if (_box == null) return;
            _now += Mathf.Max(0f, dt);
            var o = SkylineSettings.Options;
            if (o.subtitleMode != _mode)
            {
                _mode = o.subtitleMode;
                if (_mode == SubtitleMode.Off) Clear();
            }
            if (o.subtitleSize != _size) { _size = o.subtitleSize; _sizeDirty = true; }
            if (_sizeDirty) Rebuild();

            var vis = _buffer.Visible(_now);
            _barks.RemoveWhere(l => !Has(vis, l));

            // A newest line that wraps to two rows leaves room for no other: the older ones go now.
            float px = SubtitleBuffer.FontPx(_size), width = WrapWidth();
            int rows = 0;
            for (int i = vis.Count - 1; i >= 0; i--)
            {
                rows += RowsFor(vis[i].speaker, vis[i].text, px, width);
                if (rows > MaxRows && i < vis.Count - 1) vis[i].expires = Math.Min(vis[i].expires, _now);
            }
            vis = _buffer.Visible(_now);

            // Give each new line a slot; order the slots by arrival.
            bool added = false;
            foreach (var l in vis)
            {
                if (SlotOf(l) != null) continue;
                var slot = FreeSlot(vis);
                if (slot == null) continue;
                Bind(slot, l);
                added = true;
            }
            if (added) _box.Sort((a, b) => SlotAt(a).seq.CompareTo(SlotAt(b).seq));

            _shown.Clear();
            foreach (var l in vis) _shown.Add(l);
            foreach (var s in _slots)
            {
                if (s.line == null) continue;
                bool on = Has(vis, s.line);
                s.alpha = on ? Mathf.Min(1f, s.alpha + dt / FadeInSeconds) : Mathf.Max(0f, s.alpha - dt / FadeOutSeconds);
                float eased = s.alpha * s.alpha * (3f - 2f * s.alpha);
                s.el.style.opacity = eased;
                s.el.style.translate = new Translate(0f, (1f - eased) * SlidePx);
                if (!on && s.alpha <= 0f)
                {
                    s.line = null;
                    s.el.style.display = DisplayStyle.None;
                }
            }
        }

        static bool Has(IReadOnlyList<SubtitleLine> list, SubtitleLine l)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == l) return true;
            return false;
        }

        Slot SlotOf(SubtitleLine l)
        {
            foreach (var s in _slots) if (s.line == l) return s;
            return null;
        }

        Slot SlotAt(VisualElement el)
        {
            foreach (var s in _slots) if (s.el == el) return s;
            return _slots[0];
        }

        /// <summary>An empty slot, else the dimmest one that is fading out; null when all hold live lines.</summary>
        Slot FreeSlot(IReadOnlyList<SubtitleLine> live)
        {
            Slot best = null;
            foreach (var s in _slots)
            {
                if (s.line == null) return s;
                if (Has(live, s.line)) continue;
                if (best == null || s.alpha < best.alpha) best = s;
            }
            return best;
        }

        void Bind(Slot s, SubtitleLine l)
        {
            bool bark = _barks.Contains(l);
            var (r, g, b) = SpeakerColours.Of(l.speaker);
            var prefix = bark ? BarkPrefix : new Color(r, g, b);
            s.line = l;
            s.alpha = 0f;
            s.seq = ++_seq;
            s.label.SetItalic(l.pa);
            s.label.SetText("<color=#" + ColorUtility.ToHtmlStringRGB(prefix) + ">" + l.speaker + ":</color> " + l.text, l.speaker + ": " + l.text);
            s.el.style.display = DisplayStyle.Flex;
            s.el.style.opacity = 0f;
        }

        /// <summary>(Re)makes the labels at the current size; lines on screen keep their words.</summary>
        void Rebuild()
        {
            _sizeDirty = false;
            float px = SubtitleBuffer.FontPx(_size);
            foreach (var s in _slots)
            {
                if (s.label != null) s.label.RemoveFromHierarchy();
                s.label = new KeylinedLabel(px, Mathf.Max(2f, Mathf.Round(px / 17f)), null, true);
                s.label.style.width = Length.Percent(100);
                s.label.SetWrap(true, TextAnchor.LowerCenter);
                s.el.Add(s.label);
                if (s.line != null)
                {
                    var l = s.line;
                    int seq = s.seq;
                    float alpha = s.alpha;
                    Bind(s, l);
                    s.seq = seq;   // keep the arrival order
                    s.alpha = alpha;
                }
            }
        }

        float WrapWidth()
        {
            float w = _box != null ? _box.resolvedStyle.width : 0f;
            return w > 1f ? w : ReferenceWidth * WidthFraction;
        }

        /// <summary>How many rows the line "SPEAKER: text" needs at <paramref name="fontPx"/> in a block <paramref name="widthPx"/>
        /// wide (at least 1).</summary>
        public static int RowsFor(string speaker, string text, float fontPx, float widthPx)
        {
            int chars = (speaker?.Length ?? 0) + 2 + (text?.Length ?? 0);
            float perRow = Mathf.Max(8f, widthPx / (Mathf.Max(1f, fontPx) * GlyphWidth));
            return Mathf.Max(1, Mathf.CeilToInt(chars / perRow));
        }
    }
}

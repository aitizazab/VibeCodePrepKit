using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace JamKit.Dialogue
{
    /// <summary>
    /// One line of dialogue split into visible characters and inline markup, so a typewriter can
    /// reveal it a character at a time without ever showing half a tag.
    /// <para>Pass-through markup (UI Toolkit rich text): &lt;b&gt; &lt;i&gt; &lt;u&gt; &lt;s&gt;
    /// &lt;color=red&gt; &lt;size=120%&gt; &lt;mark=...&gt; &lt;br&gt; and the rest of TextCore's tags.
    /// Typewriter-only markup, removed from what is drawn:</para>
    /// <list type="bullet">
    /// <item>&lt;pause=0.5&gt; (or &lt;p=0.5&gt;): stop for half a second at this point.</item>
    /// <item>&lt;speed=0.5&gt;...&lt;/speed&gt;: type this part at half speed (2 = twice as fast).</item>
    /// <item>&lt;c=name&gt;...&lt;/c&gt;: a named colour from the game's palette (ink treats # as a tag,
    /// so a hex colour can't be written in a line).</item>
    /// </list>
    /// A "&lt;" that doesn't start a well-formed tag is ordinary text.
    /// </summary>
    public sealed class RichLine
    {
        struct Token
        {
            public bool isTag;
            public char ch;
            /// <summary>Drawn text of a tag ("&lt;b&gt;", "&lt;color=#ff0000&gt;").</summary>
            public string tag;
            /// <summary>Tag name ("b", "color"); for closers, the name it closes.</summary>
            public string name;
            public bool closing;
            /// <summary>Colour tags are left out of the hidden remainder (they'd override its alpha).</summary>
            public bool colour;
        }

        static readonly HashSet<string> Unpaired = new HashSet<string> { "br", "sprite", "space", "pos", "page", "margin", "indent", "line-height", "line-indent", "voffset", "cspace", "mspace", "width", "align", "rotate" };

        readonly List<Token> _tokens = new List<Token>();
        readonly List<float> _speed = new List<float>();
        readonly List<float> _pauseBefore = new List<float>();
        string _plain = "", _markup = "", _raw = "";

        public static readonly RichLine Empty = new RichLine();

        /// <summary>The text as written (with typewriter tags).</summary>
        public string Raw => _raw;
        /// <summary>Only the visible characters (for the backlog's search, read-text tracking, tests).</summary>
        public string Plain => _plain;
        /// <summary>What a rich-text label draws for the whole line (typewriter tags removed, palette colours resolved).</summary>
        public string Markup => _markup;
        /// <summary>Number of visible characters.</summary>
        public int Length => _plain.Length;
        bool _hasMarkup;
        /// <summary>True if the line has any markup at all (drawn or typewriter-only).</summary>
        public bool HasMarkup => _hasMarkup;

        public char CharAt(int index) => _plain[index];

        /// <summary>Speed multiplier of character <paramref name="index"/> (1 = normal, 2 = twice as fast).</summary>
        public float SpeedAt(int index) => index >= 0 && index < _speed.Count ? _speed[index] : 1f;

        /// <summary>Seconds of &lt;pause&gt; just before character <paramref name="index"/> (index == Length: after the last one).</summary>
        public float PauseBefore(int index) => index >= 0 && index < _pauseBefore.Count ? _pauseBefore[index] : 0f;

        /// <summary>Parses a line. <paramref name="palette"/> turns a &lt;c=name&gt; into a colour value
        /// ("#ffcc00"); null or an unknown name keeps the name ("&lt;color=name&gt;").</summary>
        public static RichLine Parse(string raw, Func<string, string> palette = null)
        {
            var line = new RichLine();
            line.Build(raw ?? "", palette);
            return line;
        }

        void Build(string raw, Func<string, string> palette)
        {
            _raw = raw;
            var plain = new StringBuilder(raw.Length);
            var markup = new StringBuilder(raw.Length + 16);
            var speeds = new List<float> { 1f };
            float pending = 0f;
            int i = 0;
            while (i < raw.Length)
            {
                char c = raw[i];
                if (c == '<' && TryReadTag(raw, i, out int end, out string name, out string value, out bool closing))
                {
                    _hasMarkup = true;
                    string lower = name.ToLowerInvariant();
                    if (lower == "pause" || lower == "p")
                    {
                        if (!closing) pending += ParseFloat(value, 0.5f);
                    }
                    else if (lower == "speed")
                    {
                        if (closing) { if (speeds.Count > 1) speeds.RemoveAt(speeds.Count - 1); }
                        else
                        {
                            float s = ParseFloat(value, 1f);
                            if (!(s > 0.01f)) s = 0.01f;
                            speeds.Add(speeds[speeds.Count - 1] * s);
                        }
                    }
                    else
                    {
                        string drawnName = lower == "c" ? "color" : lower;
                        string drawn;
                        if (closing) drawn = "</" + drawnName + ">";
                        else if (lower == "c")
                        {
                            string resolved = palette != null ? palette(value) : null;
                            drawn = "<color=" + (string.IsNullOrEmpty(resolved) ? value : resolved) + ">";
                        }
                        else drawn = raw.Substring(i, end - i + 1);
                        _tokens.Add(new Token { isTag = true, tag = drawn, name = drawnName, closing = closing, colour = drawnName == "color" || drawnName == "alpha" });
                        markup.Append(drawn);
                    }
                    i = end + 1;
                    continue;
                }
                _tokens.Add(new Token { ch = c });
                _speed.Add(speeds[speeds.Count - 1]);
                _pauseBefore.Add(pending);
                pending = 0f;
                plain.Append(c);
                markup.Append(c);
                i++;
            }
            _pauseBefore.Add(pending);
            _plain = plain.ToString();
            _markup = markup.ToString();
        }

        static float ParseFloat(string s, float fallback)
        {
            if (string.IsNullOrEmpty(s)) return fallback;
            return float.TryParse(s.Trim().Trim('"'), NumberStyles.Float, CultureInfo.InvariantCulture, out float f) && !float.IsNaN(f) && !float.IsInfinity(f) ? f : fallback;
        }

        /// <summary>A tag is &lt;name&gt;, &lt;name=value&gt;, &lt;name value&gt; or &lt;/name&gt;; the name starts with a letter.</summary>
        static bool TryReadTag(string s, int start, out int end, out string name, out string value, out bool closing)
        {
            end = -1; name = ""; value = ""; closing = false;
            int i = start + 1;
            if (i < s.Length && s[i] == '/') { closing = true; i++; }
            int nameStart = i;
            if (i >= s.Length || !char.IsLetter(s[i])) return false;
            while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '-' || s[i] == '_')) i++;
            name = s.Substring(nameStart, i - nameStart);
            int close = s.IndexOf('>', i);
            if (close < 0) return false;
            string rest = s.Substring(i, close - i);
            if (rest.IndexOf('<') >= 0) return false;
            if (closing)
            {
                if (rest.Trim().Length > 0) return false;
            }
            else if (rest.Length > 0)
            {
                if (rest[0] != '=' && rest[0] != ' ') return false;
                value = rest.Substring(1).Trim();
            }
            end = close;
            return true;
        }

        /// <summary>The markup for the first <paramref name="visible"/> characters with every open
        /// tag closed. <paramref name="hideRest"/> appends the rest invisibly (&lt;alpha=#00&gt;), so the
        /// words keep their final places while typing instead of jumping to the next line.</summary>
        public string Build(int visible, bool hideRest = false)
        {
            if (visible >= Length && !hideRest) return _markup;
            if (visible < 0) visible = 0;
            var sb = new StringBuilder(_markup.Length + 24);
            var open = new List<string>();
            int shown = 0, t = 0;
            for (; t < _tokens.Count; t++)
            {
                var tok = _tokens[t];
                if (!tok.isTag)
                {
                    if (shown >= visible) break;
                    sb.Append(tok.ch);
                    shown++;
                    continue;
                }
                sb.Append(tok.tag);
                Track(open, tok);
            }
            if (!hideRest || t >= _tokens.Count)
            {
                for (int k = open.Count - 1; k >= 0; k--) sb.Append("</").Append(open[k]).Append('>');
                return sb.ToString();
            }
            for (int k = open.Count - 1; k >= 0; k--)
                if (open[k] == "color" || open[k] == "alpha") { sb.Append("</").Append(open[k]).Append('>'); open.RemoveAt(k); }
            sb.Append("<alpha=#00>");
            for (; t < _tokens.Count; t++)
            {
                var tok = _tokens[t];
                if (!tok.isTag) { sb.Append(tok.ch); continue; }
                if (tok.colour) continue;
                sb.Append(tok.tag);
                Track(open, tok);
            }
            sb.Append("</alpha>");
            for (int k = open.Count - 1; k >= 0; k--) sb.Append("</").Append(open[k]).Append('>');
            return sb.ToString();
        }

        static void Track(List<string> open, Token tok)
        {
            if (Unpaired.Contains(tok.name)) return;
            if (!tok.closing) { open.Add(tok.name); return; }
            for (int k = open.Count - 1; k >= 0; k--)
                if (open[k] == tok.name) { open.RemoveAt(k); return; }
        }

        /// <summary>Removes every tag (drawn and typewriter-only): what a line says, for logs and search.</summary>
        public static string StripTags(string raw) => Parse(raw).Plain;
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace JamKit.Argument
{
    /// <summary>
    /// One spoken line, typed out a letter at a time (docs/redesign/battle.md, "Delivery"). The creator, 2026-10-12: "the
    /// changed text just pops up, instead of each line popping up at a time, giving the model space to react to it".
    ///
    /// The line is the prose file's text after <see cref="ArgumentText"/> (bold already turned into rich text tags). It
    /// paces itself like speech: a beat after commas, a longer one after a full stop, a slow trail on "...". The
    /// creator's drawl mark <c>~</c> after a letter ("ju~st dandy") stretches that letter: it types out three times
    /// ("juuust"), slowly, with slowed voice blips; each extra <c>~</c> stretches it by two more.
    ///
    /// <see cref="Visible"/> is the whole line with the part not yet typed made transparent (so the words never jump to
    /// the next line as they appear, and the box can be sized for the whole line from the start). Tags are kept
    /// balanced on both sides of the split. Nothing is allocated per tick except when the visible text changes.
    /// </summary>
    public sealed class Typewriter
    {
        /// <summary>Seconds per letter at speed 1 (about 45 letters a second).</summary>
        public const float LetterSeconds = 0.022f;
        public const float CommaPause = 0.14f, StopPause = 0.3f, DotPause = 0.13f, DashPause = 0.18f, BreakPause = 0.25f;
        /// <summary>Seconds per letter of a drawl (the stretched letter).</summary>
        public const float DrawlSeconds = 0.15f;
        /// <summary>A drawled letter is typed this many times (each extra ~ adds two).</summary>
        public const int DrawlRepeat = 3;
        /// <summary>The colour the untyped part is drawn in (fully transparent).</summary>
        public const string HiddenOpen = "<color=#00000000>", HiddenClose = "</color>";

        /// <summary>One piece of the line: a tag (no time) or a character (its time before it shows).</summary>
        struct Piece
        {
            public string tag;      // non-null: a tag ("<b>", "</b>")
            public char ch;
            public float delay;     // seconds after the previous character
            public bool blip;       // a voice blip as it appears
            public bool slow;       // a drawled letter (slow blip)
        }

        readonly List<Piece> _pieces = new List<Piece>(128);
        readonly StringBuilder _sb = new StringBuilder(256);
        readonly List<string> _open = new List<string>(4);
        string _source = "", _full = "", _plain = "", _visible = "";
        int _chars, _shown, _visibleFor = -1;
        float _time, _next;
        int _blips, _slowBlips;

        /// <summary>The line as written (rich text, drawl marks in).</summary>
        public string Source => _source;
        /// <summary>The whole line as it will read when typed (drawls spelled out, tags kept): what the box is sized for.</summary>
        public string Full => _full;
        /// <summary>The whole line without tags (drawls spelled out).</summary>
        public string Plain => _plain;
        /// <summary>Characters in the line (tags not counted).</summary>
        public int Length => _chars;
        /// <summary>Characters typed so far.</summary>
        public int Shown => _shown;
        public bool Done => _shown >= _chars;
        /// <summary>0..1 through the line.</summary>
        public float Progress => _chars == 0 ? 1f : (float)_shown / _chars;
        /// <summary>Seconds the whole line takes to type at speed 1.</summary>
        public float Duration => _duration;
        float _duration;

        /// <summary>Starts a new line (or clears it with null / "").</summary>
        public void Set(string text)
        {
            _source = text ?? "";
            _pieces.Clear();
            _chars = 0;
            _shown = 0;
            _time = 0f;
            _visibleFor = -1;
            _blips = _slowBlips = 0;
            Parse(_source, _pieces);
            float total = 0f;
            _sb.Clear();
            var plain = new StringBuilder(_source.Length);
            foreach (var p in _pieces)
            {
                if (p.tag != null) { _sb.Append(p.tag); continue; }
                _sb.Append(p.ch);
                plain.Append(p.ch);
                _chars++;
                total += p.delay;
            }
            _full = _sb.ToString();
            _plain = plain.ToString();
            _duration = total;
            _next = FirstDelay();
        }

        float FirstDelay()
        {
            foreach (var p in _pieces) if (p.tag == null) return p.delay;
            return 0f;
        }

        /// <summary>Types for <paramref name="dt"/> seconds at <paramref name="speed"/> (1 = normal). Returns the voice
        /// blips due this tick (letters, about every other one); <paramref name="slowBlips"/> gets the drawled ones.</summary>
        public int Tick(float dt, float speed, out int slowBlips)
        {
            slowBlips = 0;
            if (Done || dt <= 0f) return 0;
            if (speed <= 0f) speed = 1f;
            _time += dt * speed;
            int blips = 0;
            int index = PieceIndexOf(_shown);
            while (!Done && _time >= _next)
            {
                // Show the next character (and any tags before it).
                while (index < _pieces.Count && _pieces[index].tag != null) index++;
                if (index >= _pieces.Count) { _shown = _chars; break; }
                var p = _pieces[index];
                _shown++;
                if (p.slow) slowBlips++;
                else if (p.blip) { _blips++; if ((_blips & 1) == 1) blips++; }
                index++;
                // The next character's delay.
                int k = index;
                while (k < _pieces.Count && _pieces[k].tag != null) k++;
                _next += k < _pieces.Count ? _pieces[k].delay : 0f;
            }
            return blips;
        }

        /// <summary>Types the rest at once (a press to hurry the line).</summary>
        public void Finish()
        {
            _shown = _chars;
            _time = _next = _duration;
        }

        int PieceIndexOf(int shownChars)
        {
            int seen = 0;
            for (int i = 0; i < _pieces.Count; i++)
            {
                if (_pieces[i].tag != null) continue;
                if (seen == shownChars) return i;
                seen++;
            }
            return _pieces.Count;
        }

        /// <summary>The line with the untyped part transparent (rich text). Rebuilt only when more has been typed.</summary>
        public string Visible
        {
            get
            {
                if (_visibleFor == _shown) return _visible;
                _visibleFor = _shown;
                _visible = Build(_shown);
                return _visible;
            }
        }

        /// <summary>Only the typed part (no hidden tail): tests and plain logs.</summary>
        public string Typed
        {
            get
            {
                var sb = new StringBuilder();
                int seen = 0;
                foreach (var p in _pieces)
                {
                    if (p.tag != null) continue;
                    if (seen++ >= _shown) break;
                    sb.Append(p.ch);
                }
                return sb.ToString();
            }
        }

        string Build(int shown)
        {
            if (shown >= _chars) return _full;
            _sb.Clear();
            _open.Clear();
            int seen = 0;
            int i = 0;
            for (; i < _pieces.Count; i++)
            {
                var p = _pieces[i];
                if (p.tag != null)
                {
                    if (seen >= shown) break;
                    Track(p.tag);
                    _sb.Append(p.tag);
                    continue;
                }
                if (seen >= shown) break;
                _sb.Append(p.ch);
                seen++;
            }
            // Close what's open, hide the rest, and reopen inside the hidden part so tags stay nested.
            for (int k = _open.Count - 1; k >= 0; k--) _sb.Append("</").Append(Name(_open[k])).Append('>');
            _sb.Append(HiddenOpen);
            foreach (var t in _open) _sb.Append(t);
            for (; i < _pieces.Count; i++)
            {
                var p = _pieces[i];
                if (p.tag != null) _sb.Append(p.tag);
                else _sb.Append(p.ch);
            }
            _sb.Append(HiddenClose);
            return _sb.ToString();
        }

        void Track(string tag)
        {
            if (tag.StartsWith("</", StringComparison.Ordinal))
            {
                string n = Name(tag);
                for (int k = _open.Count - 1; k >= 0; k--)
                    if (Name(_open[k]) == n) { _open.RemoveAt(k); break; }
            }
            else if (!tag.EndsWith("/>", StringComparison.Ordinal)) _open.Add(tag);
        }

        /// <summary>A tag's name: "&lt;b&gt;" and "&lt;/b&gt;" are "b", "&lt;color=#fff&gt;" is "color".</summary>
        static string Name(string tag)
        {
            int start = tag.StartsWith("</", StringComparison.Ordinal) ? 2 : 1;
            int end = start;
            while (end < tag.Length && tag[end] != '>' && tag[end] != '=' && tag[end] != ' ') end++;
            return tag.Substring(start, end - start);
        }

        // ---------- Parsing: tags, pauses, the drawl ----------

        static void Parse(string s, List<Piece> into)
        {
            int n = s.Length;
            float pending = 0f; // a pause after a mark, added to the next character's delay
            for (int i = 0; i < n; i++)
            {
                char c = s[i];
                if (c == '<')
                {
                    int close = s.IndexOf('>', i + 1);
                    if (close > i + 1 && IsTagName(s, i + 1))
                    {
                        into.Add(new Piece { tag = s.Substring(i, close - i + 1) });
                        i = close;
                        continue;
                    }
                }
                if (c == '~')
                {
                    // A drawl mark: it stretches the letter before it (handled there); a stray one is a short pause.
                    if (!(i > 0 && char.IsLetter(s[i - 1]))) pending += DashPause;
                    continue;
                }
                int drawl = 0;
                for (int k = i + 1; k < n && s[k] == '~'; k++) drawl++;
                if (drawl > 0 && char.IsLetter(c))
                {
                    int times = DrawlRepeat + 2 * (drawl - 1);
                    for (int r = 0; r < times; r++) into.Add(new Piece { ch = c, delay = (r == 0 ? LetterSeconds + pending : DrawlSeconds), slow = true });
                    pending = 0f;
                    continue;
                }
                into.Add(new Piece { ch = c, delay = LetterSeconds + pending, blip = char.IsLetterOrDigit(c) });
                pending = PauseAfter(s, i);
            }
        }

        static float PauseAfter(string s, int i)
        {
            char c = s[i];
            char next = NextVisible(s, i + 1);
            switch (c)
            {
                case ',':
                case ';':
                case ':':
                    return CommaPause;
                case '.':
                    if (next == '.') return DotPause; // an ellipsis: each dot
                    return next == '\0' || char.IsWhiteSpace(next) || next == '"' || next == '”' ? StopPause : 0f;
                case '…':
                    return StopPause;
                case '!':
                case '?':
                    return next == '!' || next == '?' ? 0f : StopPause;
                case '-':
                    return next == '\0' || char.IsWhiteSpace(next) ? DashPause : 0f;
                case '—':
                case '–':
                    return DashPause;
                case '\n':
                    return BreakPause;
            }
            return 0f;
        }

        static char NextVisible(string s, int i)
        {
            while (i < s.Length)
            {
                if (s[i] == '<')
                {
                    int close = s.IndexOf('>', i + 1);
                    if (close > i + 1 && IsTagName(s, i + 1)) { i = close + 1; continue; }
                }
                if (s[i] == '~') { i++; continue; }
                return s[i];
            }
            return '\0';
        }

        static bool IsTagName(string s, int i)
        {
            if (i < s.Length && s[i] == '/') i++;
            return i < s.Length && char.IsLetter(s[i]);
        }

        /// <summary>The line as it reads once typed (drawls spelled out, tags kept), without a typewriter.</summary>
        public static string Spelled(string text)
        {
            var t = new Typewriter();
            t.Set(text);
            return t.Full;
        }

        /// <summary>Tags taken out ("&lt;b&gt;him&lt;/b&gt;" reads "him"); drawl marks dropped (not spelled out).</summary>
        public static string StripTags(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '<')
                {
                    int close = s.IndexOf('>', i + 1);
                    if (close > i + 1 && IsTagName(s, i + 1)) { i = close; continue; }
                }
                if (s[i] == '~') continue;
                sb.Append(s[i]);
            }
            return sb.ToString();
        }

        /// <summary>How long a typed line stays up after it's done, for reading (seconds at speed 1).</summary>
        public static float ReadingHold(int chars) => Math.Max(1.1f, Math.Min(3.6f, 0.7f + 0.026f * chars));
    }
}

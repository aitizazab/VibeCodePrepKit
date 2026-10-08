using System;

namespace JamKit.Dialogue
{
    /// <summary>
    /// Reveals a line a few characters at a time. Tick with real (unscaled) time: dialogue runs while
    /// gameplay time is frozen. Short pauses after sentence and clause punctuation make it read
    /// naturally, and the line's own markup can slow down, speed up or pause the typing
    /// (&lt;speed=0.5&gt;...&lt;/speed&gt;, &lt;pause=0.4&gt;; see <see cref="RichLine"/>).
    /// "Press to complete, press again to advance" is <see cref="Complete"/> then the caller's
    /// own advance once <see cref="IsComplete"/>.
    /// </summary>
    [Serializable]
    public sealed class Typewriter
    {
        public float charsPerSecond = 45f;
        /// <summary>Extra pause after . ! ? (seconds).</summary>
        public float sentencePause = 0.25f;
        /// <summary>Extra pause after , ; : (seconds).</summary>
        public float clausePause = 0.08f;
        /// <summary>Resolves &lt;c=name&gt; palette colours (optional).</summary>
        [NonSerialized] public Func<string, string> palette;

        RichLine _line = RichLine.Empty;
        int _visible;
        float _budget;
        float _delay;

        /// <summary>The whole line as drawn (markup included, typewriter tags removed).</summary>
        public string Text => _line.Markup;
        /// <summary>The parsed line.</summary>
        public RichLine Line => _line;
        /// <summary>Visible characters so far (markup doesn't count).</summary>
        public int Visible => _visible;
        /// <summary>Visible characters in the whole line.</summary>
        public int Length => _line.Length;
        public bool IsComplete => _visible >= _line.Length && _delay <= 0f;
        /// <summary>True while a #wait (or <see cref="Delay"/>) holds the line back.</summary>
        public bool IsWaiting => _delay > 0f;

        /// <summary>Starts a new line from nothing.</summary>
        public void Begin(string text) => BeginParsed(RichLine.Parse(text, palette));

        /// <summary>Starts a line parsed already (to share one parse with the view).</summary>
        public void BeginParsed(RichLine line)
        {
            _line = line ?? RichLine.Empty;
            _visible = 0;
            _budget = -_line.PauseBefore(0);
            _delay = 0f;
        }

        /// <summary>Holds the line back for this many seconds before the first character.</summary>
        public void Delay(float seconds)
        {
            if (seconds > _delay) _delay = seconds;
        }

        /// <summary>Shows the whole line at once (skips any delay too).</summary>
        public void Complete()
        {
            _visible = _line.Length;
            _delay = 0f;
            _budget = 0f;
        }

        /// <summary>Advances by real seconds. Returns true if more characters became visible.</summary>
        public bool Tick(float dt)
        {
            if (!(dt > 0f) || IsComplete) return false;
            if (_delay > 0f)
            {
                _delay -= dt;
                if (_delay > 0f) return false;
                dt = -_delay;
                _delay = 0f;
            }
            if (charsPerSecond <= 0f) { bool any = _visible < _line.Length; Complete(); return any; }
            _budget += dt;
            int before = _visible;
            float basePerChar = 1f / charsPerSecond;
            int length = _line.Length;
            while (_visible < length)
            {
                float perChar = basePerChar / _line.SpeedAt(_visible);
                if (_budget < perChar) break;
                _budget -= perChar;
                char c = _line.CharAt(_visible);
                _visible++;
                if (_visible >= length) break;
                _budget -= _line.PauseBefore(_visible);
                // Pause after punctuation, but not inside "..." or before the end of the line.
                if (_line.CharAt(_visible) != c)
                {
                    if (c == '.' || c == '!' || c == '?') _budget -= sentencePause;
                    else if (c == ',' || c == ';' || c == ':') _budget -= clausePause;
                }
            }
            return _visible != before;
        }
    }
}

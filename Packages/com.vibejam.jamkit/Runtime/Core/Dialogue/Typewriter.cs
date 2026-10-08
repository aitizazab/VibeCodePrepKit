using System;

namespace JamKit.Dialogue
{
    /// <summary>
    /// Reveals a line a few characters at a time. Tick with real (unscaled) time: dialogue runs while
    /// gameplay time is frozen. Short pauses after sentence and clause punctuation make it read
    /// naturally. "Press to complete, press again to advance" is <see cref="Complete"/> then the caller's
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

        string _text = "";
        int _visible;
        float _budget;
        float _delay;

        public string Text => _text;
        public int Visible => _visible;
        public bool IsComplete => _visible >= _text.Length && _delay <= 0f;
        /// <summary>True while a #wait (or <see cref="Delay"/>) holds the line back.</summary>
        public bool IsWaiting => _delay > 0f;

        /// <summary>Starts a new line from nothing.</summary>
        public void Begin(string text)
        {
            _text = text ?? "";
            _visible = 0;
            _budget = 0f;
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
            _visible = _text.Length;
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
            if (charsPerSecond <= 0f) { bool any = _visible < _text.Length; Complete(); return any; }
            _budget += dt;
            int before = _visible;
            float perChar = 1f / charsPerSecond;
            while (_visible < _text.Length && _budget >= perChar)
            {
                _budget -= perChar;
                char c = _text[_visible];
                _visible++;
                // Pause after punctuation, but not inside "..." or before the end of the line.
                bool nextIsSame = _visible < _text.Length && _text[_visible] == c;
                if (!nextIsSame && _visible < _text.Length)
                {
                    if (c == '.' || c == '!' || c == '?') _budget -= sentencePause;
                    else if (c == ',' || c == ';' || c == ':') _budget -= clausePause;
                }
            }
            return _visible != before;
        }
    }
}

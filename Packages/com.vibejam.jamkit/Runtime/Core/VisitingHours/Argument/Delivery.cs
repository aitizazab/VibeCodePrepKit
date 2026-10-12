using System;
using System.Collections.Generic;

namespace JamKit.Argument
{
    /// <summary>
    /// The spoken lines, one at a time (docs/redesign/battle.md, "Delivery"): each is typed out (<see cref="Typewriter"/>)
    /// with voice blips, stays up long enough to read, then the next comes. A press hurries a line (the rest appears) or,
    /// once it's all there, moves on. Lines from an exchange are <i>blocking</i>: the debate waits while they're typed
    /// and for a beat after (<see cref="BlockHold"/>), then the last of them stays up to be read while play resumes (its
    /// tail). A statement said as it flies, or a short bark, isn't blocking: it plays over the debate, gives way to a
    /// blocking line, and waits for an exchange's tail to finish.
    ///
    /// The opponent's face follows the lines (<see cref="Portrait"/>): each line of theirs shows its expression as it
    /// starts, and a moment after it's finished the face eases back to a listening idle, so nothing extreme is held
    /// while Nadia thinks.
    /// </summary>
    public sealed class Delivery
    {
        public struct Line
        {
            public Speaker speaker;
            public string who, text, expression;
            public bool blocking;
            /// <summary>A statement flying in the debate (its serial), or 0.</summary>
            public int statement;
            /// <summary>Said before (quieter blips).</summary>
            public bool repeat;
            /// <summary>Typing speed for this line (Nadia's cut-ins are quicker); 0 = 1.</summary>
            public float speed;
        }

        /// <summary>What happened this tick (for the view's sound).</summary>
        public struct Tock
        {
            public bool started;
            public int blips, slowBlips;
        }

        /// <summary>Seconds a blocking line holds the debate after it's typed (the rest of its reading time runs over play).</summary>
        public const float BlockHold = 0.8f;

        readonly List<Line> _queue = new List<Line>(16);
        readonly Typewriter _typewriter = new Typewriter();
        Line _current, _last;
        bool _has, _hadAny;
        float _held, _holdFor;

        public Typewriter Typewriter => _typewriter;
        /// <summary>A line is up (typing, or held for reading).</summary>
        public bool HasLine => _has;
        public Line Current => _current;
        /// <summary>The last line shown (kept on screen, dimmed, until the next).</summary>
        public Line Last => _last;
        public bool HadAny => _hadAny;
        public bool LineDone => _has && _typewriter.Done;
        public int Queued => _queue.Count;
        public bool Idle => !_has && _queue.Count == 0;

        /// <summary>A blocking line is being said (or waiting): the debate holds still for it. Its tail doesn't hold it.</summary>
        public bool Blocking
        {
            get
            {
                if (_has && _current.blocking && !InTail) return true;
                foreach (var l in _queue) if (l.blocking) return true;
                return false;
            }
        }

        /// <summary>The line up is an exchange's last line, typed and held a beat: it stays to be read over play.</summary>
        public bool InTail => _has && _current.blocking && _typewriter.Done && _held >= BlockHold;

        /// <summary>A line longer than this (letters) is said in pages, split at a sentence (the box never has to hold more
        /// than a page).</summary>
        public const int MaxPage = 300;

        public void Add(Line line)
        {
            if (string.IsNullOrEmpty(line.text)) return;
            if (Typewriter.StripTags(line.text).Length > MaxPage && SplitPage(line.text, out string first, out string rest))
            {
                var a = line;
                a.text = first;
                Add(a);
                var b = line;
                b.text = rest;
                b.expression = "";
                Add(b);
                return;
            }
            if (line.blocking)
            {
                // A statement or bark in progress gives way to what's said in an exchange, and so does a queued one.
                for (int i = _queue.Count - 1; i >= 0; i--) if (!_queue[i].blocking) _queue.RemoveAt(i);
                if (_has && !_current.blocking) End();
            }
            else
            {
                // A newer statement replaces the one still being said (or waiting); an exchange's tail is left to finish.
                if (_has && !_current.blocking) End();
                for (int i = _queue.Count - 1; i >= 0; i--) if (!_queue[i].blocking) _queue.RemoveAt(i);
            }
            _queue.Add(line);
        }

        /// <summary>Splits a long line at the sentence end nearest its middle (outside any tag); false if there's none.</summary>
        public static bool SplitPage(string text, out string first, out string rest)
        {
            first = text;
            rest = "";
            int mid = text.Length / 2, best = -1, depth = 0;
            for (int i = 0; i < text.Length - 2; i++)
            {
                char c = text[i];
                if (c == '<') depth++;
                else if (c == '>') depth = Math.Max(0, depth - 1);
                if (depth > 0 || (c != '.' && c != '?' && c != '!') || text[i + 1] != ' ') continue;
                if (best < 0 || Math.Abs(i - mid) < Math.Abs(best - mid)) best = i;
            }
            if (best < 0) return false;
            first = text.Substring(0, best + 1);
            rest = text.Substring(best + 2);
            return rest.Length > 0;
        }

        /// <summary>Drops whatever's waiting (and the line up): the battle is closing, or a test.</summary>
        public void Clear()
        {
            _queue.Clear();
            if (_has) End();
        }

        /// <summary>Drops the non-blocking lines (a statement's subtitle when its flight is cut short).</summary>
        public void DropPassing()
        {
            for (int i = _queue.Count - 1; i >= 0; i--) if (!_queue[i].blocking) _queue.RemoveAt(i);
            if (_has && !_current.blocking) End();
        }

        /// <summary>Types and paces the lines. <paramref name="advance"/>: the player pressed to hurry or move on.</summary>
        public Tock Tick(float dt, float speed, bool advance)
        {
            var t = new Tock();
            if (speed <= 0f) speed = 1f;
            if (!_has && _queue.Count > 0)
            {
                _current = _queue[0];
                _queue.RemoveAt(0);
                _has = _hadAny = true;
                _last = _current;
                _typewriter.Set(_current.text);
                _held = 0f;
                _holdFor = _current.blocking ? Typewriter.ReadingHold(_typewriter.Length) : 2.2f;
                t.started = true;
                advance = false; // the press that ended the last line doesn't skip this one
            }
            if (!_has) return t;
            if (!_typewriter.Done)
            {
                if (advance) _typewriter.Finish();
                else t.blips = _typewriter.Tick(dt, speed * (_current.speed > 0f ? _current.speed : 1f), out t.slowBlips);
                return t;
            }
            _held += dt * speed;
            // A blocking line holds a beat, then the next line of the exchange comes, or it stays as the tail (read over
            // play) until its reading time runs out or something else is said; a press moves on. A passing line stays
            // until replaced or it runs out.
            if (_current.blocking)
            {
                if (advance) End();
                else if (_held >= BlockHold && _queue.Count > 0 && _queue[0].blocking) End();
                else if (_held >= _holdFor) End();
            }
            else if (_held >= _holdFor) End();
            return t;
        }

        void End()
        {
            _has = false;
            _held = 0f;
        }
    }

    /// <summary>
    /// The opponent's face through the lines: a line's expression as it starts (a little lift as they speak), held while
    /// it's typed and a moment after, then back to their idle (listening): neutral, or arms folded once they're angry.
    /// Their opening pose holds until their first expressive line.
    /// </summary>
    public sealed class Portrait
    {
        /// <summary>Seconds a line's expression stays after it's finished before the face eases back.</summary>
        public const float Linger = 1.1f;

        string _shown, _idle;
        bool _reacting, _speaking, _pinned;
        float _since;
        int _changes;

        public Portrait(string opening)
        {
            _shown = _idle = opening ?? "";
        }

        public string Shown => _shown;
        public string Idle => _idle;
        public bool Speaking => _speaking;
        public bool Reacting => _reacting;
        /// <summary>Counts every change of face (the view cross-fades on a change).</summary>
        public int Changes => _changes;
        public float Since => _since;

        public void SetIdle(string idle)
        {
            if (string.IsNullOrEmpty(idle) || idle == _idle) return;
            _idle = idle;
            if (!_reacting && !_speaking) Show(_idle);
        }

        /// <summary>A line of theirs starts: its expression ("" keeps the face, but they're speaking).</summary>
        public void Speak(string expression)
        {
            if (_pinned) return;
            _speaking = true;
            _since = 0f;
            if (!string.IsNullOrEmpty(expression))
            {
                _reacting = true;
                Show(expression);
            }
        }

        /// <summary>Their line is done (typed out).</summary>
        public void Quiet() => _speaking = false;

        /// <summary>Holds a face until <see cref="Release"/> (the end beats, a capture).</summary>
        public void Hold(string expression)
        {
            _pinned = true;
            Show(expression);
        }

        public void Release()
        {
            _pinned = false;
            _reacting = true;
            _since = 0f;
        }

        public bool Pinned => _pinned;

        public void Tick(float dt)
        {
            if (_speaking || _pinned) return;
            _since += dt;
            if (_reacting && _since >= Linger)
            {
                _reacting = false;
                Show(_idle);
            }
        }

        void Show(string e)
        {
            if (e == _shown) return;
            _shown = e;
            _changes++;
        }
    }
}

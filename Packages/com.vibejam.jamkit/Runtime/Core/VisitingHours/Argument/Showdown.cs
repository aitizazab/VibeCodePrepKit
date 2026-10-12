using System;
using System.Collections.Generic;

namespace JamKit.Argument
{
    /// <summary>
    /// Pressing, played (docs/redesign/battle.md, "The showdown"; after Danganronpa's Rebuttal Showdown): Nadia forces a
    /// premise down, and the opponent cuts back. Their words come at her one at a time across the table; she cuts each
    /// as it reaches her mark (A), and a word she misses lands (a heartbeat). Swinging at nothing staggers her for a
    /// moment. When every word is cut or has landed, the premise is forced: pressing always gets there, unless her
    /// composure gives out first. Angrier opponents throw more words, faster and closer together.
    ///
    /// Units are the debate's (x -3..3): words travel right to left along the middle, the mark is at <see cref="Mark"/>.
    /// </summary>
    public sealed class Showdown
    {
        public const float Mark = -1.7f;
        /// <summary>A word is cuttable while its front edge is this close to the mark.</summary>
        public const float Window = 0.45f;
        public const float Stagger = 0.35f;
        public const float CharWidth = 0.11f;

        public struct Word
        {
            public string text;
            public float x, width, speed, age;
            public bool active, cut, landed;
            public float doneAge;
        }

        readonly List<Word> _words = new List<Word>(12);
        readonly List<string> _queue = new List<string>(12);
        readonly Rng _rng;
        readonly float _speed, _gap;
        readonly bool _canHurt;
        float _time, _nextAt, _stagger;
        int _cuts, _landed, _whiffs, _total;
        int _hitsTaken, _hits;

        /// <summary>A showdown with these words (in order), at this anger; the claim throws two more.</summary>
        public Showdown(IList<string> words, int anger, bool claim, ArgumentDifficulty difficulty, float pace, ulong seed)
        {
            _rng = new Rng(seed == 0 ? 1UL : seed);
            anger = Math.Max(0, Math.Min(3, anger));
            int count = 4 + anger + (claim ? 2 : 0);
            var pool = new List<string>();
            if (words != null) foreach (var w in words) if (!string.IsNullOrEmpty(w)) pool.Add(w);
            if (pool.Count == 0) pool.Add("...");
            int start = _rng.Range(0, pool.Count);
            for (int i = 0; i < count; i++) _queue.Add(pool[(start + i) % pool.Count]);
            _total = _queue.Count;
            pace = pace <= 0f ? 1f : pace;
            _speed = (difficulty == ArgumentDifficulty.Story ? 1.7f : 2.3f + 0.45f * anger) * pace;
            _gap = Math.Max(0.55f, 1.2f - 0.15f * anger) / pace;
            _canHurt = difficulty != ArgumentDifficulty.Story;
            _nextAt = 0.9f;
        }

        public IReadOnlyList<Word> Words => _words;
        public float Time => _time;
        public int Cuts => _cuts;
        public int Landed => _landed;
        public int Whiffs => _whiffs;
        public int Total => _total;
        public bool Staggered => _stagger > 0f;
        public bool Finished => _queue.Count == 0 && !AnyLive();
        /// <summary>Hits on Nadia since the last call (each a heartbeat).</summary>
        public int TakeHits()
        {
            int n = _hits - _hitsTaken;
            _hitsTaken = _hits;
            return n;
        }

        /// <summary>The front-most word not yet cut or landed (its index, or -1): the one a cut would take.</summary>
        public int Front
        {
            get
            {
                int best = -1;
                float bx = float.MaxValue;
                for (int i = 0; i < _words.Count; i++)
                {
                    var w = _words[i];
                    if (!w.active || w.cut || w.landed) continue;
                    if (w.x < bx) { bx = w.x; best = i; }
                }
                return best;
            }
        }

        /// <summary>True if the front word is in the window now (a cut would land).</summary>
        public bool InWindow
        {
            get
            {
                int f = Front;
                return f >= 0 && Math.Abs(FrontEdge(_words[f]) - Mark) <= Window;
            }
        }

        static float FrontEdge(in Word w) => w.x - w.width * 0.5f;

        /// <summary>Plays on: <paramref name="cut"/> is the cut button pressed this tick. Returns 1 for a cut, -1 for a
        /// swing at nothing, 0 otherwise.</summary>
        public int Tick(float dt, bool cut)
        {
            if (dt <= 0f) return 0;
            _time += dt;
            if (_stagger > 0f) _stagger -= dt;
            if (_queue.Count > 0 && _time >= _nextAt)
            {
                string text = _queue[0];
                _queue.RemoveAt(0);
                float width = Math.Max(0.6f, text.Length * CharWidth + 0.3f);
                _words.Add(new Word { text = text, width = width, x = PressurePhase.HalfWidth + width * 0.5f, speed = _speed * _rng.Range(0.92f, 1.1f), active = true });
                _nextAt = _time + _gap * _rng.Range(0.8f, 1.25f);
            }
            for (int i = 0; i < _words.Count; i++)
            {
                var w = _words[i];
                if (!w.active) continue;
                w.age += dt;
                if (w.cut || w.landed)
                {
                    w.doneAge += dt;
                    if (w.doneAge > 0.6f) w.active = false;
                }
                else
                {
                    w.x -= w.speed * dt;
                    if (FrontEdge(w) < Mark - Window)
                    {
                        w.landed = true;
                        _landed++;
                        if (_canHurt) _hits++;
                    }
                }
                _words[i] = w;
            }
            if (!cut) return 0;
            if (_stagger > 0f) return 0;
            int f = Front;
            if (f >= 0 && Math.Abs(FrontEdge(_words[f]) - Mark) <= Window)
            {
                var w = _words[f];
                w.cut = true;
                _words[f] = w;
                _cuts++;
                return 1;
            }
            _whiffs++;
            _stagger = Stagger;
            return -1;
        }

        bool AnyLive()
        {
            foreach (var w in _words) if (w.active && !w.cut && !w.landed) return true;
            return false;
        }

        /// <summary>Seconds until the front word reaches the middle of the window (the pilot's timing), or -1.</summary>
        public float UntilWindow
        {
            get
            {
                int f = Front;
                if (f < 0) return -1f;
                var w = _words[f];
                return (FrontEdge(w) - Mark) / Math.Max(0.01f, w.speed);
            }
        }
    }
}

using System;
using System.Collections.Generic;

namespace JamKit.Skyline
{
    /// <summary>
    /// When the teaching beats (6.8) are said (Task 16 fix round 1, the controller's ruling): one line at a time, each for its
    /// reading time (<see cref="LineSeconds"/>: about 15 characters a second, 1.5 to 4 s; the babble is cut to it and the
    /// subtitle stays up for it), then <see cref="Gap"/> before the next. Combat notices (<see cref="IsCombat"/>: FirstSeen,
    /// FirstPlatingBreak, NextFightArmed) go ahead of pending warm-up hints; within each kind the queue keeps its order, so
    /// beats that share a trigger all play, in the data's order. A hint that has waited more than <see cref="StaleSeconds"/>
    /// and whose moment has passed (the caller says) is dropped. Pure; the arena director feeds it unscaled time.
    /// </summary>
    public sealed class TeachingQueue
    {
        public const float CharsPerSecond = 15f, MinLineSeconds = 1.5f, MaxLineSeconds = 4f, Gap = 0.5f, StaleSeconds = 20f;

        struct Entry
        {
            public TeachingBeat beat;
            public float queuedAt;
        }

        readonly List<Entry> _pending = new List<Entry>();
        float _nextAt = float.NegativeInfinity;

        public int Count => _pending.Count;

        /// <summary>The time the next line may start (after the current line and the gap).</summary>
        public float NextAt => _nextAt;

        /// <summary>Seconds a line is shown and spoken: its reading time at <see cref="CharsPerSecond"/>, clamped to
        /// <see cref="MinLineSeconds"/>..<see cref="MaxLineSeconds"/>.</summary>
        public static float LineSeconds(string text)
        {
            float s = (text?.Length ?? 0) / CharsPerSecond;
            return s < MinLineSeconds ? MinLineSeconds : s > MaxLineSeconds ? MaxLineSeconds : s;
        }

        /// <summary>Combat notices jump ahead of warm-up hints and are never dropped.</summary>
        public static bool IsCombat(BeatTrigger t) => t == BeatTrigger.FirstSeen || t == BeatTrigger.FirstPlatingBreak || t == BeatTrigger.NextFightArmed;

        public void Enqueue(TeachingBeat beat, float now)
        {
            if (beat == null) return;
            _pending.Add(new Entry { beat = beat, queuedAt = now });
        }

        public bool Contains(string id)
        {
            foreach (var e in _pending) if (e.beat.id == id) return true;
            return false;
        }

        /// <summary>Drops stale hints (into <paramref name="dropped"/>, if given), then returns the beat to start now, or null
        /// while a line is still being said. <paramref name="momentPassed"/> says whether a hint's moment is over (null: never).</summary>
        public TeachingBeat Next(float now, Func<TeachingBeat, bool> momentPassed, List<TeachingBeat> dropped)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var e = _pending[i];
                if (IsCombat(e.beat.trigger) || now - e.queuedAt <= StaleSeconds) continue;
                if (momentPassed == null || !momentPassed(e.beat)) continue;
                _pending.RemoveAt(i);
                dropped?.Insert(0, e.beat);
            }
            if (now < _nextAt || _pending.Count == 0) return null;
            int pick = 0;
            for (int i = 0; i < _pending.Count; i++)
                if (IsCombat(_pending[i].beat.trigger)) { pick = i; break; }
            var b = _pending[pick].beat;
            _pending.RemoveAt(pick);
            _nextAt = now + LineSeconds(b.text) + Gap;
            return b;
        }
    }
}

using System;
using System.Collections.Generic;

namespace JamKit.Skyline
{
    /// <summary>How soon a teaching line must be said (judge rounds 1 and 2: every beat fires at its moment, or not at all).</summary>
    public enum LineUrgency
    {
        /// <summary>Nan's opener: flavour, said when nothing more urgent is waiting.</summary>
        Hint = 0,
        /// <summary>A warm-up lesson tied to a place (Dot's practice bot and kiosk lessons, Nan's counter): cuts the opener.</summary>
        Lesson = 1,
        /// <summary>A notice (CONCIERGE's safety notices, the next-fight notice, the lift opening): cuts hints and lessons.</summary>
        Notice = 2,
        /// <summary>A reaction to what the player just did (a spot formed, the ledge reached, the plating broke): cuts
        /// everything but another reaction.</summary>
        Reaction = 3,
    }

    /// <summary>
    /// When the teaching beats (6.8) are said (judge rounds 1 and 2; first written in Task 16 fix round 1). One line at a
    /// time, each for its reading time (<see cref="LineSeconds"/>: a 0.5 s lead plus 15 characters a second, 2 to 10 s; the
    /// babble and the subtitle last that long), then <see cref="Gap"/> before the next.
    /// <list type="bullet">
    /// <item>Order: reactions, then notices, then lessons, then the opener (<see cref="UrgencyOf"/>); first come, first said
    /// within each.</item>
    /// <item>Moments: a pending line whose moment has passed (the caller says) is dropped at once, and so is one that has
    /// waited longer than its kind allows (<see cref="MaxWait"/>: a reaction 3 s, a lesson or the opener 20 s, a notice
    /// 30 s). A line late for its moment is never said.</item>
    /// <item>A line whose moment passes while it is being said (the spotlight wrecked mid-notice) ends there, once it has
    /// been up <see cref="MinShownBeforeCut"/>; the next line starts at once.</item>
    /// <item>Cuts: any more urgent pending line cuts the line being said once that has been up
    /// <see cref="MinShownBeforeCut"/>. The cut line goes back to the front of the queue (waiting from now), so it is said
    /// again after if its moment still holds.</item>
    /// </list>
    /// Pure; the arena director feeds it the game clock.
    /// </summary>
    public sealed class TeachingQueue
    {
        public const float CharsPerSecond = 15f, LeadSeconds = 0.5f, MinLineSeconds = 2f, MaxLineSeconds = 10f, Gap = 0.4f;
        public const float HintMaxWait = 20f, NoticeMaxWait = 30f, ReactionMaxWait = 3f, MinShownBeforeCut = 1.2f;

        struct Entry
        {
            public TeachingBeat beat;
            public float queuedAt;
        }

        readonly List<Entry> _pending = new List<Entry>();
        float _nextAt = float.NegativeInfinity, _currentStart, _currentEnd;
        TeachingBeat _current;

        public int Count => _pending.Count;

        /// <summary>The time the next line may start (after the current line and the gap), unless a cut comes first.</summary>
        public float NextAt => _nextAt;

        /// <summary>The line being said (null once it has ended).</summary>
        public TeachingBeat Current(float now) => _current != null && now < _currentEnd ? _current : null;

        /// <summary>Seconds a line is shown and spoken: <see cref="LeadSeconds"/> plus its length at
        /// <see cref="CharsPerSecond"/>, clamped to <see cref="MinLineSeconds"/>..<see cref="MaxLineSeconds"/>.</summary>
        public static float LineSeconds(string text)
        {
            float s = LeadSeconds + (text?.Length ?? 0) / CharsPerSecond;
            return s < MinLineSeconds ? MinLineSeconds : s > MaxLineSeconds ? MaxLineSeconds : s;
        }

        public static LineUrgency UrgencyOf(BeatTrigger t)
        {
            switch (t)
            {
                case BeatTrigger.FirstSpotCreated:
                case BeatTrigger.BoostLedgeReached:
                case BeatTrigger.FirstPlatingBreak:
                    return LineUrgency.Reaction;
                case BeatTrigger.FirstSeen:
                case BeatTrigger.NextFightArmed:
                case BeatTrigger.LiftOpened:
                    return LineUrgency.Notice;
                case BeatTrigger.PracticeBotSeen:
                case BeatTrigger.NearBoostLedge:
                case BeatTrigger.NearPracticeLedge:
                    return LineUrgency.Lesson;
                default:
                    return LineUrgency.Hint;
            }
        }

        public static float MaxWait(LineUrgency u) =>
            u == LineUrgency.Reaction ? ReactionMaxWait : u == LineUrgency.Notice ? NoticeMaxWait : HintMaxWait;

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

        /// <summary>Forgets every pending line and the line being said.</summary>
        public void Clear()
        {
            _pending.Clear();
            _current = null;
            _nextAt = float.NegativeInfinity;
        }

        /// <summary>Drops pending lines whose moment has passed or that waited too long (into <paramref name="dropped"/>), ends
        /// the line being said if its own moment has passed (into <paramref name="cut"/> and <paramref name="dropped"/>), then
        /// returns the line to start now, or null. A line cut for a more urgent one is added to <paramref name="cut"/> and put
        /// back at the front of the queue. When <paramref name="cut"/> gains a line and null is returned, the caller takes the
        /// subtitle down. <paramref name="momentPassed"/> says whether a line's moment is over (null: never).</summary>
        public TeachingBeat Next(float now, Func<TeachingBeat, bool> momentPassed, List<TeachingBeat> dropped, List<TeachingBeat> cut = null)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var e = _pending[i];
                bool late = now - e.queuedAt > MaxWait(UrgencyOf(e.beat.trigger));
                if (!late && (momentPassed == null || !momentPassed(e.beat))) continue;
                _pending.RemoveAt(i);
                dropped?.Insert(0, e.beat);
            }

            var cur = Current(now);
            bool shownEnough = cur != null && now - _currentStart >= MinShownBeforeCut;
            if (shownEnough && momentPassed != null && momentPassed(cur))
            {
                // its moment is over: end it now; the next line, if any, starts at once
                cut?.Add(cur);
                dropped?.Add(cur);
                _current = null;
                _currentEnd = now;
                _nextAt = _pending.Count > 0 ? now : now + Gap;
                cur = null;
            }
            if (_pending.Count == 0) return null;

            int pick = 0;
            var best = UrgencyOf(_pending[0].beat.trigger);
            for (int i = 1; i < _pending.Count; i++)
            {
                var u = UrgencyOf(_pending[i].beat.trigger);
                if (u > best) { best = u; pick = i; }
            }

            if (cur != null)
            {
                if (!shownEnough || best <= UrgencyOf(cur.trigger)) return null;
                cut?.Add(cur);
                _pending.Insert(0, new Entry { beat = cur, queuedAt = now });
                pick++;
            }
            else if (now < _nextAt) return null;

            var b = _pending[pick].beat;
            _pending.RemoveAt(pick);
            _current = b;
            _currentStart = now;
            _currentEnd = now + LineSeconds(b.text);
            _nextAt = _currentEnd + Gap;
            return b;
        }
    }
}

using System;
using System.Collections.Generic;

namespace JamKit.Skyline
{
    /// <summary>How soon a teaching line must be said (judge rounds 1-3: every beat fires at its moment, or not at all).</summary>
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
    /// When the teaching beats (6.8) are said (judge rounds 1-3; first written in Task 16 fix round 1). One line at a time,
    /// each for its reading time (<see cref="LineSeconds"/>: a 0.5 s lead plus 15 characters a second, 2 to 10 s; the babble
    /// and the subtitle last that long), then <see cref="Gap"/> before the next.
    /// <list type="bullet">
    /// <item>Order: reactions, then notices, then lessons, then the opener (<see cref="UrgencyOf"/>). Within a kind, the
    /// caller's order (smaller first: for a notice, how far its nearest bot is), then first come, first said.</item>
    /// <item>Said once: a line that has started is never queued again (<see cref="WasSaid"/>), except a repeating one (the
    /// next-fight notice). A learned lesson never repeats.</item>
    /// <item>Moments: a pending line whose moment has passed (the caller says) is dropped at once, and so is one that has
    /// waited longer than its kind allows (<see cref="MaxWait"/>: a reaction 3 s, a lesson or the opener 20 s, a notice
    /// 30 s).</item>
    /// <item>A line whose moment passes while it is being said ends there, once it has been up
    /// <see cref="MinShownBeforeCut"/>; the next line starts at once. A safety notice always finishes: it teaches the kind of bot,
    /// not the one that just died.</item>
    /// <item>Cuts: after <see cref="MinShownBeforeCut"/>, a more urgent line cuts the one being said, and a notice whose
    /// order is <see cref="NoticeCutMargin"/> smaller cuts another notice (the floor-care unit at your ankles before the
    /// drones across the hall). Nothing less urgent ever cuts. A cut line resumes where it was cut
    /// (<see cref="StartOffset"/>, with <see cref="ResumeLead"/> to find the place again), unless it had been up 80% of its
    /// time (then it counts as said) or it had been cut before (then it is dropped for good).</item>
    /// </list>
    /// Pure; the arena director feeds it the game clock.
    /// </summary>
    public sealed class TeachingQueue
    {
        public const float CharsPerSecond = 15f, LeadSeconds = 0.5f, MinLineSeconds = 2f, MaxLineSeconds = 10f, Gap = 0.4f;
        public const float HintMaxWait = 20f, NoticeMaxWait = 30f, ReactionMaxWait = 3f, MinShownBeforeCut = 1.2f;
        public const float ResumeLead = 0.8f, MinResumeSeconds = 1.5f, SaidShare = 0.8f, NoticeCutMargin = 8f;

        struct Entry
        {
            public TeachingBeat beat;
            public float queuedAt, offset;
            public int cuts;
        }

        readonly List<Entry> _pending = new List<Entry>();
        readonly HashSet<string> _said = new HashSet<string>();
        float _nextAt = float.NegativeInfinity, _currentStart, _currentEnd, _currentOffset;
        Entry _current;
        bool _speaking;

        public int Count => _pending.Count;

        /// <summary>The time the next line may start (after the current line and the gap), unless a cut comes first.</summary>
        public float NextAt => _nextAt;

        /// <summary>Seconds into its line the line just started begins (0, or where it was cut when it resumes).</summary>
        public float StartOffset => _currentOffset;

        /// <summary>How long the line just started stays up (its reading time, or what was left of it plus
        /// <see cref="ResumeLead"/>).</summary>
        public float CurrentSeconds => _currentEnd - _currentStart;

        /// <summary>The line being said (null once it has ended).</summary>
        public TeachingBeat Current(float now) => _speaking && now < _currentEnd ? _current.beat : null;

        /// <summary>Whether the line <paramref name="id"/> has started at least once.</summary>
        public bool WasSaid(string id) => _said.Contains(id);

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
                case BeatTrigger.AssetTowed:
                case BeatTrigger.MeterStarted:
                case BeatTrigger.FirstSpecialAmmo:
                case BeatTrigger.LeftCircleMidTow:
                    return LineUrgency.Reaction;
                case BeatTrigger.FirstSeen:
                case BeatTrigger.NextFightArmed:
                case BeatTrigger.LiftOpened:
                case BeatTrigger.WaveAnnounce:
                case BeatTrigger.AlarmRaised:
                case BeatTrigger.AllTowed:
                case BeatTrigger.FloorStart:
                    return LineUrgency.Notice;
                case BeatTrigger.PracticeBotSeen:
                case BeatTrigger.NearBoostLedge:
                case BeatTrigger.NearPracticeLedge:
                case BeatTrigger.CircleNear:
                case BeatTrigger.LobbyDesk:
                case BeatTrigger.LobbyLiftNear:
                    return LineUrgency.Lesson;
                default:
                    return LineUrgency.Hint;
            }
        }

        public static float MaxWait(LineUrgency u) =>
            u == LineUrgency.Reaction ? ReactionMaxWait : u == LineUrgency.Notice ? NoticeMaxWait : HintMaxWait;

        /// <summary>Queues <paramref name="beat"/>, unless it has been said before (and doesn't repeat) or is already
        /// waiting. Returns whether it was queued.</summary>
        public bool Enqueue(TeachingBeat beat, float now)
        {
            if (beat == null || (!beat.repeats && _said.Contains(beat.id)) || Contains(beat.id)) return false;
            _pending.Add(new Entry { beat = beat, queuedAt = now });
            return true;
        }

        public bool Contains(string id)
        {
            foreach (var e in _pending) if (e.beat.id == id) return true;
            return false;
        }

        /// <summary>Forgets every pending line and the line being said (what has been said stays said).</summary>
        public void Clear()
        {
            _pending.Clear();
            _speaking = false;
            _nextAt = float.NegativeInfinity;
        }

        /// <summary>Drops pending lines whose moment has passed or that waited too long (into <paramref name="dropped"/>), ends
        /// the line being said if its moment has passed and it isn't a safety notice (into <paramref name="cut"/> and
        /// <paramref name="dropped"/>), then returns the line to start now, or null. A line cut for another is added to
        /// <paramref name="cut"/> (and queued to resume, if it may). When <paramref name="cut"/> gains a line and null is
        /// returned, the caller takes the subtitle down. <paramref name="momentPassed"/> says whether a line's moment is over
        /// (null: never); <paramref name="order"/> ranks lines of one kind (smaller first; null: first come, first said).</summary>
        public TeachingBeat Next(float now, Func<TeachingBeat, bool> momentPassed, List<TeachingBeat> dropped,
            List<TeachingBeat> cut = null, Func<TeachingBeat, float> order = null)
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
            if (shownEnough && cur.trigger != BeatTrigger.FirstSeen && momentPassed != null && momentPassed(cur))
            {
                cut?.Add(cur);
                dropped?.Add(cur);
                _speaking = false;
                _currentEnd = now;
                _nextAt = _pending.Count > 0 ? now : now + Gap;
                cur = null;
            }
            if (_pending.Count == 0) return null;

            int pick = 0;
            var best = UrgencyOf(_pending[0].beat.trigger);
            float bestOrder = order != null ? order(_pending[0].beat) : 0f;
            for (int i = 1; i < _pending.Count; i++)
            {
                var u = UrgencyOf(_pending[i].beat.trigger);
                float o = order != null ? order(_pending[i].beat) : 0f;
                if (u > best || (u == best && o < bestOrder)) { best = u; bestOrder = o; pick = i; }
            }

            if (cur != null)
            {
                if (!shownEnough) return null;
                var cu = UrgencyOf(cur.trigger);
                bool closerNotice = best == LineUrgency.Notice && cu == LineUrgency.Notice && order != null
                    && bestOrder + NoticeCutMargin < order(cur);
                if (!(best > cu || closerNotice)) return null;
                cut?.Add(cur);
                float shown = _currentOffset + (now - _currentStart);
                bool mostlySaid = shown >= SaidShare * LineSeconds(cur.text);
                if (!mostlySaid && _current.cuts == 0)
                {
                    _pending.Insert(0, new Entry { beat = cur, queuedAt = now, offset = shown, cuts = 1 });
                    pick++;
                }
            }
            else if (now < _nextAt) return null;

            var next = _pending[pick];
            _pending.RemoveAt(pick);
            _current = next;
            _speaking = true;
            _said.Add(next.beat.id);
            _currentStart = now;
            _currentOffset = next.offset;
            float full = LineSeconds(next.beat.text);
            float seconds = next.offset > 0f ? Math.Max(MinResumeSeconds, full - next.offset) + ResumeLead : full;
            _currentEnd = now + seconds;
            _nextAt = _currentEnd + Gap;
            return next.beat;
        }
    }
}

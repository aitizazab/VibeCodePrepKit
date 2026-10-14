using System;
using System.Collections.Generic;

namespace JamKit.Skyline
{
    /// <summary>
    /// The pure half of Skyline's mix pass (the creator, 2026-10-14: "the sounds just bunch together into a cacophony of harsh
    /// sounds"): who gets to play when many bots make noise at once. No Unity types, so the unit tests can run a scripted
    /// fight through it. <see cref="SkylineVoiceGate"/> decides which sounds of a group play (at most N at once, a minimum gap
    /// between starts, the most audible wins), <see cref="SkylineLoopBudget"/> keeps only the nearest few idle loops audible,
    /// and <see cref="SkylineBiquad"/> is the gentle EQ the bot sounds go through. The numbers live in SkylineMix (Unity side).
    /// </summary>
    public sealed class SkylineVoiceGate
    {
        /// <summary>One group's limits.</summary>
        public struct Rule
        {
            /// <summary>At most this many of the group playing at once (0 = no limit).</summary>
            public int maxConcurrent;
            /// <summary>A new start must wait this long after the group's last start (seconds), unless it is much more audible.</summary>
            public float minGap;
            /// <summary>A sound quieter than this (its audibility where the listener stands) is not worth a voice.</summary>
            public float floor;
            /// <summary>A newcomer displaces the weakest playing sound (or the one inside the gap) when it is at least this many
            /// times more audible (&gt; 1: a tie keeps what already plays).</summary>
            public float stealFactor;
            /// <summary>The family (a wider group, e.g. "bot") whose own limit also applies; null for none.</summary>
            public string family;
        }

        /// <summary>The verdict for one request.</summary>
        public struct Verdict
        {
            /// <summary>True when the sound may play.</summary>
            public bool admit;
            /// <summary>The id the caller got for the admitted sound (give it back to <see cref="End"/> if it stops early).</summary>
            public int id;
            /// <summary>Playing sounds this one displaced: the caller fades them out (ids from earlier verdicts).</summary>
            public List<int> displaced;
        }

        sealed class Entry
        {
            public int id;
            public string group, family;
            public float start, end, audible;
        }

        readonly Dictionary<string, Rule> _rules = new Dictionary<string, Rule>();
        readonly Dictionary<string, int> _familyMax = new Dictionary<string, int>();
        readonly List<Entry> _active = new List<Entry>();
        readonly Dictionary<string, float> _lastStart = new Dictionary<string, float>();
        int _nextId = 1;

        /// <summary>Counts for the pass's log.</summary>
        public int Admitted, DroppedQuiet, DroppedFull, DroppedGap, Displaced;

        public void SetRule(string group, Rule rule) => _rules[group] = rule;

        /// <summary>The limit on a whole family (all groups whose rule names it), e.g. six bot one-shots at once.</summary>
        public void SetFamilyMax(string family, int max) => _familyMax[family] = max;

        public void Clear()
        {
            _active.Clear();
            _lastStart.Clear();
            Admitted = DroppedQuiet = DroppedFull = DroppedGap = Displaced = 0;
        }

        /// <summary>How many sounds of <paramref name="group"/> are playing at <paramref name="now"/>.</summary>
        public int ActiveIn(string group, float now)
        {
            Prune(now);
            int n = 0;
            foreach (var e in _active) if (e.group == group) n++;
            return n;
        }

        /// <summary>Asks to start a sound of <paramref name="group"/> lasting <paramref name="duration"/> seconds, with the given
        /// audibility (its linear level where the listener stands). The verdict names what it displaces.</summary>
        public Verdict Request(string group, float now, float duration, float audible)
        {
            var v = new Verdict { admit = false, id = -1, displaced = null };
            if (string.IsNullOrEmpty(group) || !_rules.TryGetValue(group, out var rule))
            {
                v.admit = true;   // an unruled group is not the gate's business
                return v;
            }
            Prune(now);
            if (audible < rule.floor) { DroppedQuiet++; return v; }

            float steal = rule.stealFactor < 1f ? 1f : rule.stealFactor;
            List<int> out_ = null;

            // 1. the gap since the group's last start: a louder newcomer may replace the sound that is inside it
            if (rule.minGap > 0f && _lastStart.TryGetValue(group, out float last) && now - last < rule.minGap)
            {
                Entry recent = null;
                foreach (var e in _active)
                    if (e.group == group && now - e.start < rule.minGap && (recent == null || e.audible > recent.audible)) recent = e;
                if (recent != null)
                {
                    if (audible < recent.audible * steal) { DroppedGap++; return v; }
                    Displace(recent, ref out_);
                }
            }
            // 2. the group's own limit, then its family's: the newcomer must beat the weakest playing sound
            if (rule.maxConcurrent > 0 && !Fit(e => e.group == group, rule.maxConcurrent, audible, steal, ref out_)) { DroppedFull++; Undo(); return v; }
            if (rule.family != null && _familyMax.TryGetValue(rule.family, out int fmax) && fmax > 0
                && !Fit(e => e.family == rule.family, fmax, audible, steal, ref out_)) { DroppedFull++; Undo(); return v; }

            v.admit = true;
            v.id = _nextId++;
            v.displaced = out_;
            _active.Add(new Entry { id = v.id, group = group, family = rule.family, start = now, end = now + Math.Max(0.01f, duration), audible = audible });
            _lastStart[group] = now;
            Admitted++;
            if (out_ != null) Displaced += out_.Count;
            return v;
        }

        /// <summary>The caller stopped a sound early (or it was displaced and has faded).</summary>
        public void End(int id)
        {
            for (int i = 0; i < _active.Count; i++) if (_active[i].id == id) { _active.RemoveAt(i); return; }
        }

        readonly List<Entry> _pendingUndo = new List<Entry>();

        bool Fit(Func<Entry, bool> inSet, int max, float audible, float steal, ref List<int> displaced)
        {
            int count = 0;
            Entry weakest = null;
            foreach (var e in _active)
            {
                if (!inSet(e)) continue;
                count++;
                if (weakest == null || e.audible < weakest.audible) weakest = e;
            }
            if (count < max) return true;
            if (weakest == null || audible < weakest.audible * steal) return false;
            Displace(weakest, ref displaced);
            return true;
        }

        void Displace(Entry e, ref List<int> displaced)
        {
            if (displaced == null) displaced = new List<int>();
            if (!displaced.Contains(e.id)) displaced.Add(e.id);
            _pendingUndo.Add(e);
            _active.Remove(e);
        }

        // A failed request puts back what it had already displaced.
        void Undo()
        {
            foreach (var e in _pendingUndo) _active.Add(e);
            _pendingUndo.Clear();
        }

        void Prune(float now)
        {
            _pendingUndo.Clear();
            for (int i = _active.Count - 1; i >= 0; i--) if (_active[i].end <= now) _active.RemoveAt(i);
        }
    }

    /// <summary>The voice gate's limits for Skyline (the mix pass, 2026-10-14): per group at most N sounds at once and a minimum gap
    /// between starts; the nearest (most audible) bot wins; six bot one-shots at most in all. The player's own sounds are never gated.
    /// The groups: "bot.alert", "bot.telegraph", "bot.attack", "bot.hurt", "bot.break" (plate shed, wreck, mine blast, acid sweep),
    /// "bot.minor", and "bark" (a bot's babble).</summary>
    public static class SkylineMixRules
    {
        public const string FamilyBot = "bot";

        public static void Configure(SkylineVoiceGate g)
        {
            const float floor = 0.006f, steal = 1.5f;
            g.SetFamilyMax(FamilyBot, 6);
            g.SetRule("bot.alert", new SkylineVoiceGate.Rule { maxConcurrent = 2, minGap = 0.20f, floor = floor, stealFactor = steal, family = FamilyBot });
            g.SetRule("bot.telegraph", new SkylineVoiceGate.Rule { maxConcurrent = 3, minGap = 0.06f, floor = floor, stealFactor = steal, family = FamilyBot });
            g.SetRule("bot.attack", new SkylineVoiceGate.Rule { maxConcurrent = 3, minGap = 0.06f, floor = floor, stealFactor = steal, family = FamilyBot });
            g.SetRule("bot.hurt", new SkylineVoiceGate.Rule { maxConcurrent = 2, minGap = 0.10f, floor = floor, stealFactor = steal, family = FamilyBot });
            g.SetRule("bot.break", new SkylineVoiceGate.Rule { maxConcurrent = 2, minGap = 0.12f, floor = floor, stealFactor = steal, family = FamilyBot });
            g.SetRule("bot.minor", new SkylineVoiceGate.Rule { maxConcurrent = 2, minGap = 0.10f, floor = floor, stealFactor = steal, family = FamilyBot });
            // a bark: one at a time, nearest wins; a newcomer within 0.5 s of the last needs to be 1.5 times louder
            g.SetRule("bark", new SkylineVoiceGate.Rule { maxConcurrent = 1, minGap = 0.5f, floor = 0.02f, stealFactor = steal, family = null });
        }
    }

    /// <summary>Which of the scene's looping beds may be heard: the nearest few (by audibility), with a margin so two loops of
    /// about the same loudness do not trade places every frame, and gains that fade instead of cutting.</summary>
    public static class SkylineLoopBudget
    {
        /// <summary>Moves the first <paramref name="count"/> of <paramref name="gains"/> (one per loop, 0..1) toward 1 for the <paramref name="max"/> most audible loops and 0
        /// for the rest, at a rate of 1/<paramref name="fadeSeconds"/> a second. A loop already on counts
        /// <paramref name="stickiness"/> times its audibility when ranked. Returns how many loops are on (gain target 1).</summary>
        public static int Step(float[] audible, float[] gains, int count, int max, float dt, float fadeSeconds, float stickiness = 1.25f)
        {
            int n = Math.Min(count, Math.Min(audible.Length, gains.Length));
            int on = 0;
            for (int i = 0; i < n; i++)
            {
                float mine = audible[i] * (gains[i] > 0.5f ? stickiness : 1f);
                int better = 0;
                for (int j = 0; j < n && better < max; j++)
                {
                    if (j == i) continue;
                    float other = audible[j] * (gains[j] > 0.5f ? stickiness : 1f);
                    if (other > mine || (other == mine && j < i)) better++;
                }
                bool wanted = better < max && audible[i] > 0f;
                if (wanted) on++;
                float step = fadeSeconds > 0f ? dt / fadeSeconds : 1f;
                gains[i] = wanted ? Math.Min(1f, gains[i] + step) : Math.Max(0f, gains[i] - step);
            }
            return on;
        }
    }

    /// <summary>A second-order filter (RBJ cookbook) for the mix's gentle EQ: a peaking cut over the harsh 2-5 kHz band and a
    /// low-pass for the synthesized babble. One channel; the audio thread owns one per voice.</summary>
    public sealed class SkylineBiquad
    {
        float _b0 = 1f, _b1, _b2, _a1, _a2, _z1, _z2;

        public bool IsFlat { get; private set; } = true;

        public void Reset() { _z1 = _z2 = 0f; }

        /// <summary>Peaking EQ: <paramref name="gainDb"/> at <paramref name="centerHz"/>, <paramref name="q"/> wide.</summary>
        public void SetPeaking(float sampleRate, float centerHz, float q, float gainDb)
        {
            if (Math.Abs(gainDb) < 0.05f) { SetFlat(); return; }
            double A = Math.Pow(10.0, gainDb / 40.0);
            double w0 = 2.0 * Math.PI * Math.Min(centerHz, sampleRate * 0.45f) / sampleRate;
            double alpha = Math.Sin(w0) / (2.0 * q), cw = Math.Cos(w0);
            double a0 = 1.0 + alpha / A;
            Set((1.0 + alpha * A) / a0, -2.0 * cw / a0, (1.0 - alpha * A) / a0, -2.0 * cw / a0, (1.0 - alpha / A) / a0);
        }

        /// <summary>Low-pass at <paramref name="cutoffHz"/> (q 0.707 = Butterworth, 12 dB an octave).</summary>
        public void SetLowPass(float sampleRate, float cutoffHz, float q = 0.7071f)
        {
            double w0 = 2.0 * Math.PI * Math.Min(cutoffHz, sampleRate * 0.45f) / sampleRate;
            double alpha = Math.Sin(w0) / (2.0 * q), cw = Math.Cos(w0);
            double a0 = 1.0 + alpha;
            Set((1.0 - cw) / 2.0 / a0, (1.0 - cw) / a0, (1.0 - cw) / 2.0 / a0, -2.0 * cw / a0, (1.0 - alpha) / a0);
        }

        public void SetFlat()
        {
            _b0 = 1f; _b1 = _b2 = _a1 = _a2 = 0f;
            IsFlat = true;
        }

        void Set(double b0, double b1, double b2, double a1, double a2)
        {
            _b0 = (float)b0; _b1 = (float)b1; _b2 = (float)b2; _a1 = (float)a1; _a2 = (float)a2;
            IsFlat = false;
        }

        public float Process(float x)
        {
            float y = _b0 * x + _z1;
            _z1 = _b1 * x - _a1 * y + _z2;
            _z2 = _b2 * x - _a2 * y;
            return y;
        }

        /// <summary>Filters an interleaved block in place (the same filter state runs through every channel's samples in
        /// turn, so use it on mono clips; the voice EQ keeps one biquad per channel).</summary>
        public void ProcessMono(float[] data)
        {
            if (IsFlat) return;
            for (int i = 0; i < data.Length; i++) data[i] = Process(data[i]);
        }
    }
}

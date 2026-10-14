using System;
using System.Collections.Generic;

namespace JamKit.Skyline
{
    /// <summary>One section of the music as data (docs/SKYLINE-MUSIC.md): a tempo, a length and where it goes next. A
    /// section's length is whole bars plus <see cref="extraBeats"/> (the pre-drop is 6 beats, not a whole bar). Sections
    /// change only on bar lines (counted from the section's own start) or when a non-looping one ends. The composition is
    /// still being worked on with the creator: these are the numbers the sequencer runs on, never constants in code.</summary>
    public sealed class MusicSectionDef
    {
        public string id = "";
        public double bpm = MusicSections.Floor1Bpm;
        public int beatsPerBar = 4, bars;
        /// <summary>Beats on top of <see cref="bars"/> whole bars (0 when the section is whole bars).</summary>
        public double extraBeats;
        /// <summary>True: the section repeats until a request changes it (on a bar line). False: it plays once, then
        /// goes to <see cref="next"/> (or goes quiet when that is null).</summary>
        public bool loops;
        /// <summary>The id of the section a non-looping one hands over to when it ends (the set-piece chain); null for none.</summary>
        public string next;

        public double BeatSeconds => 60.0 / bpm;
        public double BarSeconds => beatsPerBar * BeatSeconds;
        public double Beats => bars * beatsPerBar + extraBeats;
        public double Seconds => Beats * BeatSeconds;
    }

    /// <summary>One musical event inside a section, in beats from the section's start: what the stems of the composed
    /// version must hit (a note of the riser's walk-up, a held wub of the pre-drop, its dive, its gap). Pitches are MIDI
    /// note numbers (60 = middle C); -1 for silence.</summary>
    public sealed class MusicCue
    {
        public string kind = "";
        public double beat, lengthBeats;
        public int midi = -1;
    }

    /// <summary>The opening of floor 1 as a section table (the creator's decisions, 2026-10-14): in C at 122.5 BPM. Lobby
    /// muzak loops; stepping into the lift starts the riser on the muzak's next bar line: ONE bar of the six-step walk-up
    /// C-E-G-A-Bb landing on C (on sixteenths 0, 4, 8, 11, 14, 16), then the pre-drop, 6 beats from that landing (four
    /// dotted quarters: three held wub notes and the fourth diving, the last half-beat empty, the gap), then the drop on
    /// the beat after, when the doors open and the wave clock starts. The drop therefore lands 1 bar + 6 beats = 10 beats
    /// after the riser starts, NOT on a downbeat of the riser's grid: the main song re-bars from the drop. Placeholder
    /// numbers where marked; the composed stems replace them, with the creator.</summary>
    public static class MusicSections
    {
        public const string Muzak = "muzak", Riser = "riser", PreDrop = "pre-drop", Drop = "drop", MainLoop = "main", CalmLoop = "calm";

        /// <summary>The floor 1 opening's tempo (the Subway Surfers theme measured about 122.5 BPM).</summary>
        public const double Floor1Bpm = 122.5;

        /// <summary>The riser's six steps on the sixteenth grid of its one bar (the last is the landing, on the next
        /// section's first beat) and their pitches: C E G A Bb C.</summary>
        public static readonly int[] RiserSixteenths = { 0, 4, 8, 11, 14, 16 };
        public static readonly int[] RiserMidi = { 60, 64, 67, 69, 70, 72 };

        /// <summary>The pre-drop's four dotted quarters, in beats from its start (the landing).</summary>
        public static readonly double[] PreDropHitBeats = { 0.0, 1.5, 3.0, 4.5 };
        public const double DottedQuarterBeats = 1.5, PreDropBeats = 6.0, PreDropGapBeats = 0.5;
        /// <summary>The wub's root (C2) and how far the fourth hit dives, in semitones (placeholder).</summary>
        public const int PreDropWubMidi = 36, PreDropDiveSemitones = 12;

        /// <summary>The section table, in the order the opening plays them (placeholder numbers for the lengths of the
        /// loops: muzak 8 bars, main 16, calm 8). Replaced by the composed stems' real values.</summary>
        public static MusicSectionDef[] Placeholders()
        {
            return new[]
            {
                new MusicSectionDef { id = Muzak, bars = 8, loops = true },
                new MusicSectionDef { id = Riser, bars = 1, next = PreDrop },
                new MusicSectionDef { id = PreDrop, extraBeats = PreDropBeats, next = Drop },
                new MusicSectionDef { id = Drop, bars = 1, next = MainLoop },
                new MusicSectionDef { id = MainLoop, bars = 16, loops = true },
                new MusicSectionDef { id = CalmLoop, bars = 8, loops = true },
            };
        }

        /// <summary>The musical events of a section that has any (the riser and the pre-drop); empty for the others.</summary>
        public static MusicCue[] Cues(string sectionId)
        {
            var cues = new List<MusicCue>();
            if (sectionId == Riser)
            {
                for (int i = 0; i < RiserSixteenths.Length; i++)
                    cues.Add(new MusicCue
                    {
                        kind = i == RiserSixteenths.Length - 1 ? "landing" : "step",
                        beat = RiserSixteenths[i] / 4.0,
                        lengthBeats = i == RiserSixteenths.Length - 1 ? DottedQuarterBeats : 0.0,
                        midi = RiserMidi[i],
                    });
            }
            else if (sectionId == PreDrop)
            {
                for (int i = 0; i < 3; i++)
                    cues.Add(new MusicCue { kind = "wub", beat = PreDropHitBeats[i], lengthBeats = DottedQuarterBeats, midi = PreDropWubMidi });
                // the fourth dives and stops half a beat short of the drop: the gap
                cues.Add(new MusicCue { kind = "dive", beat = PreDropHitBeats[3], lengthBeats = DottedQuarterBeats - PreDropGapBeats,
                    midi = PreDropWubMidi - PreDropDiveSemitones });
                cues.Add(new MusicCue { kind = "gap", beat = PreDropBeats - PreDropGapBeats, lengthBeats = PreDropGapBeats, midi = -1 });
            }
            return cues.ToArray();
        }
    }

    /// <summary>The lift ride's timing on the audio DSP clock (seconds): the riser on the muzak's next bar line, the
    /// pre-drop where the riser lands, the drop 1 bar + 6 beats after the riser starts.</summary>
    public struct LiftRidePlan
    {
        /// <summary>The riser starts and the lift starts to rise (the doors must be shut by now).</summary>
        public double riserAt;
        /// <summary>The riser lands on C and the pre-drop starts.</summary>
        public double preDropAt;
        /// <summary>The drop: the doors are open, the wave clock starts, the main song re-bars from here.</summary>
        public double dropAt;
        /// <summary>The doors start to open <see cref="MusicSequencer.DoorLead"/> before the drop so they are open on it.</summary>
        public double doorsStartOpeningAt;
        /// <summary>The doors are shut by this time (the riser starts).</summary>
        public double doorsClosedBy;
        /// <summary>The drop section ends and the main loop starts.</summary>
        public double mainAt;
    }

    /// <summary>A section change: from one section to another at a DSP time.</summary>
    public struct MusicChange
    {
        public string from, to;
        public double at;
    }

    /// <summary>The music's one clock (docs/SKYLINE-MUSIC.md, docs/SKYLINE-PLAN-FLOOR1.md Task M1): pure, driven by the
    /// DSP time the caller reads. A section loops until its trigger; a request changes it on the current section's next bar
    /// line (a set-piece, being non-looping, changes only where it ends). Times are computed from the section's start by
    /// multiplication, never accumulated, so hours of looping do not drift.</summary>
    public sealed class MusicSequencer
    {
        /// <summary>The least lead (s) a request gets before its change, so the audio can schedule it.</summary>
        public const double MinLead = 0.25;
        /// <summary>The lift doors start to open this long before the drop so they are open on it.</summary>
        public const double DoorLead = 1.2;
        const double Eps = 1e-9;

        readonly Dictionary<string, MusicSectionDef> _defs = new Dictionary<string, MusicSectionDef>();
        MusicSectionDef _cur;
        double _start;
        long _pass;
        string _pending;
        double _pendingAt;

        public MusicSequencer(IReadOnlyList<MusicSectionDef> defs)
        {
            if (defs == null) throw new ArgumentNullException(nameof(defs));
            foreach (var d in defs)
            {
                if (d == null || string.IsNullOrEmpty(d.id)) throw new ArgumentException("a section needs an id", nameof(defs));
                if (_defs.ContainsKey(d.id)) throw new ArgumentException("duplicate section " + d.id, nameof(defs));
                if (d.bpm <= 0 || d.beatsPerBar <= 0 || d.Beats <= 0) throw new ArgumentException("section " + d.id + " has no length", nameof(defs));
                _defs[d.id] = d;
            }
            foreach (var d in defs)
                if (d.next != null && !_defs.ContainsKey(d.next)) throw new ArgumentException("section " + d.id + " goes to unknown " + d.next, nameof(defs));
        }

        /// <summary>The section playing (null before <see cref="Start"/>).</summary>
        public string Current => _cur?.id;
        /// <summary>When the current section first started (a loop's later passes do not move it).</summary>
        public double SectionStart => _start;
        /// <summary>When the current pass of the current section started (the latest <see cref="Tick"/> sets the pass).</summary>
        public double CurrentStart => _cur == null ? 0 : _start + _pass * _cur.Seconds;
        /// <summary>The section a request scheduled (null for none) and its DSP time.</summary>
        public string Pending => _pending;
        public double PendingAt => _pending == null ? 0 : _pendingAt;

        public MusicSectionDef Def(string id) => _defs[id];

        /// <summary>Starts <paramref name="id"/> at <paramref name="dspTime"/> (also a hard restart: a wreck calls this with
        /// the muzak). Drops any pending change.</summary>
        public void Start(string id, double dspTime)
        {
            _cur = Need(id);
            _start = dspTime;
            _pass = 0;
            _pending = null;
        }

        /// <summary>Drops a scheduled change; the current section carries on.</summary>
        public void CancelPending() { _pending = null; }

        /// <summary>Asks for <paramref name="id"/> on the current section's next bar line at least <see cref="MinLead"/> after
        /// <paramref name="now"/> (a non-looping section's only change point is its end; one that ends inside the lead hands
        /// over to its next first and the request lands on that one's grid). Replaces a pending request (the later wins).
        /// Returns the DSP time of the change.</summary>
        public double Request(string id, double now)
        {
            Need(id);
            NeedStarted();
            double at = ChangePoint(now);
            _pending = id;
            _pendingAt = at;
            return at;
        }

        /// <summary>The lift ride's timing if the player stepped into the lift now; changes nothing.</summary>
        public LiftRidePlan PlanLiftRide(double now)
        {
            NeedStarted();
            var riser = Need(MusicSections.Riser);
            var pre = Need(MusicSections.PreDrop);
            var drop = Need(MusicSections.Drop);
            return PlanAt(ChangePoint(now), riser, pre, drop);
        }

        /// <summary>Plans the lift ride and schedules it: the riser is requested for its time and the set-piece chain
        /// (riser, pre-drop, drop, main) follows by itself. Returns the plan.</summary>
        public LiftRidePlan StartLiftRide(double now)
        {
            var plan = PlanLiftRide(now);
            _pending = MusicSections.Riser;
            _pendingAt = plan.riserAt;
            return plan;
        }

        static LiftRidePlan PlanAt(double riserAt, MusicSectionDef riser, MusicSectionDef pre, MusicSectionDef drop)
        {
            double preAt = riserAt + riser.Seconds;
            double dropAt = preAt + pre.Seconds;
            return new LiftRidePlan
            {
                riserAt = riserAt,
                preDropAt = preAt,
                dropAt = dropAt,
                doorsStartOpeningAt = dropAt - DoorLead,
                doorsClosedBy = riserAt,
                mainAt = dropAt + drop.Seconds,
            };
        }

        /// <summary>Whole bars the current section has played since it first started (loops counted); 0 before it starts.</summary>
        public double BarIndex(double now)
        {
            if (_cur == null) return 0;
            double b = Math.Floor((now - _start) / _cur.BarSeconds + Eps);
            return b < 0 ? 0 : b;
        }

        /// <summary>The next change if nothing else is requested (the pending request, or a non-looping section handing over);
        /// the director schedules the next section's audio from it. False when there is none.</summary>
        public bool Upcoming(out MusicChange change)
        {
            change = default;
            string to;
            double at;
            if (!NextEvent(out to, out at)) return false;
            change = new MusicChange { from = _cur.id, to = to, at = at };
            return true;
        }

        /// <summary>Reports (once each, in order) the changes whose time is at or before <paramref name="now"/>: true with the
        /// change when one has started, false when none is due. After a long hitch call it in a loop until it returns false.</summary>
        public bool Tick(double now, out MusicChange change)
        {
            change = default;
            if (_cur == null) return false;
            string to;
            double at;
            if (NextEvent(out to, out at) && at <= now + Eps)
            {
                change = new MusicChange { from = _cur.id, to = to, at = at };
                bool wasPending = _pending != null && _pending == to && Math.Abs(_pendingAt - at) <= Eps;
                _cur = _defs[to];
                _start = at;
                _pass = 0;
                if (wasPending) _pending = null;
                return true;
            }
            if (_cur.loops && now > _start)
            {
                long pass = (long)Math.Floor((now - _start) / _cur.Seconds + Eps);
                if (pass > _pass) _pass = pass;
            }
            return false;
        }

        // ---- internals ----

        MusicSectionDef Need(string id)
        {
            MusicSectionDef d;
            if (id == null || !_defs.TryGetValue(id, out d)) throw new ArgumentException("unknown section " + id);
            return d;
        }

        void NeedStarted()
        {
            if (_cur == null) throw new InvalidOperationException("the sequencer has not been started");
        }

        /// <summary>The earliest of the pending request and a non-looping section's hand-over; the request wins a tie
        /// (it replaces the chain).</summary>
        bool NextEvent(out string to, out double at)
        {
            to = null;
            at = 0;
            bool chain = !_cur.loops && _cur.next != null;
            double chainAt = _start + _cur.Seconds;
            if (_pending != null && (!chain || _pendingAt <= chainAt + Eps))
            {
                to = _pending;
                at = _pendingAt;
                return true;
            }
            if (chain)
            {
                to = _cur.next;
                at = chainAt;
                return true;
            }
            return false;
        }

        /// <summary>The first legal change point at least <see cref="MinLead"/> after <paramref name="now"/>.</summary>
        double ChangePoint(double now)
        {
            double earliest = now + MinLead - Eps;
            var d = _cur;
            double start = _start;
            long pass = 0;
            // the current section's pass in progress (loops): the grid restarts at each pass
            if (d.loops && now > start) pass = (long)Math.Floor((now - start) / d.Seconds + Eps);
            for (int guard = 0; guard < 64; guard++)
            {
                double passStart = start + pass * d.Seconds;
                if (d.loops)
                {
                    double end = passStart + d.Seconds;
                    if (end < earliest - Eps) { pass++; continue; }   // too close to the loop's end: the next pass's grid
                    double k = Math.Ceiling((earliest - passStart) / d.BarSeconds - Eps);
                    if (k < 0) k = 0;
                    double t = passStart + k * d.BarSeconds;
                    return t < end ? t : end;
                }
                double over = start + d.Seconds;
                if (over >= earliest) return over;
                if (d.next == null) return earliest;
                start = over;
                pass = 0;
                d = _defs[d.next];
            }
            return earliest;
        }
    }
}

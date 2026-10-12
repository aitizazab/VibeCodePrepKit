using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Argument
{
    /// <summary>
    /// The debate (docs/redesign/battle.md): the argument as real-time play. The opponent says their case one statement
    /// at a time, round after round, and each statement flies across the table as it's said, its weak words glowing.
    /// Nadia's light moves among them while their pressure comes at her (the same fair attacks as a pressure phase,
    /// sparser: <see cref="PressureSpec.ForDebate"/>). To act on a statement she puts her light on it (and, to show
    /// evidence, on its glowing words); <see cref="BattleFlow"/> turns that into a verb.
    ///
    /// A round is one pass of everything still standing, then the claim. As the claim goes by, the pressure drains (the
    /// table clears, a breath), and the next round starts with the heat and anger as they are then, so a wrong move
    /// sharpens the next round. Hidden things a slip would reveal drift through from the second round on, once a round.
    ///
    /// Surface units are a pressure phase's (x -3..3, y -1.6..1.6, y up). The view tells it each statement's measured
    /// size (<see cref="Measure"/>); until then it estimates. Nothing allocated per tick.
    /// </summary>
    public sealed class Debate
    {
        /// <summary>How close the light has to be to touch a statement (it aims with a ring a little wider than it is).</summary>
        public const float AimRadius = 0.2f;
        /// <summary>Estimated size of a character and a line before the view measures (surface units).</summary>
        public const float CharWidth = 0.074f, LineHeight = 0.27f, Padding = 0.22f;
        /// <summary>The next statement comes once the last is this far through its flight.</summary>
        public const float Overlap = 0.8f;
        /// <summary>The longest the table may stay draining before the next round starts anyway.</summary>
        public const float DrainLimit = 3.5f;
        public const float SlipRadius = 0.2f;
        /// <summary>Seconds between a statement's guard rings.</summary>
        public const float GuardEvery = 1.7f;
        public const float SlipSpeed = 0.55f;

        public enum Flight { FromRight, FromLeft, Drift }
        public enum Fate { Flying, Shattered, Settled }

        public struct Statement
        {
            public int serial;
            /// <summary>The premise it states (or <see cref="ArgumentEngine.ClaimId"/>).</summary>
            public string premise;
            /// <summary>Their question about that premise (a retort answers it).</summary>
            public bool question;
            public bool claim;
            /// <summary>The words, plain (drawls spelled out), split around the weak words.</summary>
            public string pre, weak, post;
            /// <summary>Said before, in an earlier round.</summary>
            public bool repeat;
            public Flight flight;
            public Vector2 position, velocity;
            public float age, life, tilt;
            /// <summary>Size (surface units) and where the weak words run, from the statement's left edge.</summary>
            public float width, height, weak0, weak1;
            public bool measured;
            public Fate fate;
            public float fateAge;
            public bool active;
            /// <summary>Guard rings still to bloom from its weak words, and when the next one does (its age).</summary>
            public int guards;
            public float nextGuard;

            public string Plain => pre + weak + post;
            public float Left => position.X - width * 0.5f;
            /// <summary>0..1: faded in at the start, out at the end (and as it shatters or settles).</summary>
            public float Alpha
            {
                get
                {
                    float a = Math.Min(1f, age / 0.25f);
                    if (life - age < 0.35f) a = Math.Min(a, Math.Max(0f, (life - age) / 0.35f));
                    if (fate != Fate.Flying) a = Math.Min(a, Math.Max(0f, 1f - fateAge / 0.6f));
                    return a;
                }
            }
        }

        public struct DebateSlip
        {
            public string premise, name;
            public Vector2 position, velocity;
            public float age, life;
            public bool active;
        }

        struct Pending
        {
            public string premise;
            public bool question;
        }

        readonly ArgumentEngine _engine;
        readonly Rng _rng;
        readonly List<Statement> _statements = new List<Statement>(4);
        readonly List<Pending> _lineup = new List<Pending>(16);
        readonly List<DebateSlip> _slips = new List<DebateSlip>(2);
        readonly List<SlipSeed> _slipSeeds = new List<SlipSeed>(2);
        readonly HashSet<string> _said = new HashSet<string>();
        PressurePhase _hazards;
        int _round, _serial, _lastLane = -1, _patternCycle;
        float _roundTime, _drainTime, _nextAt;
        bool _draining, _slipThisRound, _started;
        Vector2 _light;
        ulong _seed;
        string _caught;

        public Debate(ArgumentEngine engine, ulong seed)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _seed = seed == 0 ? 1UL : seed;
            _rng = new Rng(_seed ^ 0x9E3779B97F4A7C15UL);
            _light = new Vector2(-1.2f, -0.6f);
        }

        // ---------- State ----------

        public int Round => _round;
        public float RoundTime => _roundTime;
        public bool Draining => _draining;
        public PressurePhase Hazards => _hazards;
        public IReadOnlyList<Statement> Statements => _statements;
        public IReadOnlyList<DebateSlip> Slips => _slips;
        public Vector2 Light => _hazards != null ? _hazards.Light : _light;
        /// <summary>Statements still to come this round (premise ids; tests and the pilot).</summary>
        public int Waiting => _lineup.Count;
        public bool HasWaiting(string premise)
        {
            foreach (var p in _lineup) if (p.premise == premise) return true;
            return false;
        }

        /// <summary>Starts (or restarts after a pause elsewhere) with the light at <paramref name="light"/>.</summary>
        public void Begin(Vector2 light)
        {
            _light = light;
            if (!_started)
            {
                _started = true;
                NewRound();
            }
            else _hazards?.PlaceLight(light);
        }

        /// <summary>Moves the light (and the pressure) on: <paramref name="dt"/> of real-time play. Spawns statements on
        /// cue; returns the serial of a statement that just started (0 if none), for its subtitle.</summary>
        public int Tick(float dt, Vector2 move)
        {
            if (!_started || dt <= 0f) return 0;
            int spoken = 0;
            _roundTime += dt;
            if (_hazards != null)
            {
                _hazards.Tick(dt, move);
                _light = _hazards.Light;
            }
            for (int i = 0; i < _statements.Count; i++)
            {
                var s = _statements[i];
                if (!s.active) continue;
                s.age += dt;
                if (s.fate == Fate.Flying)
                {
                    s.position += s.velocity * dt;
                    // Her words push back as she says them: a ring blooms from the weak words.
                    if (s.guards > 0 && s.age >= s.nextGuard && _hazards != null && !_draining)
                    {
                        var at = WeakCentre(s);
                        if (Math.Abs(at.X) < PressurePhase.HalfWidth - 0.2f) _hazards.Guard(at);
                        s.guards--;
                        s.nextGuard = s.age + GuardEvery;
                    }
                }
                else
                {
                    s.fateAge += dt;
                    s.position += s.velocity * (dt * 0.25f);
                }
                if (s.age >= s.life || (s.fate != Fate.Flying && s.fateAge >= 0.6f)) s.active = false;
                _statements[i] = s;
            }
            TickSlips(dt);

            // The next statement, once the last is far enough along.
            if (!_draining && _roundTime >= _nextAt && Ready())
            {
                if (_lineup.Count > 0)
                {
                    spoken = Spawn(_lineup[0]);
                    _lineup.RemoveAt(0);
                }
                else if (!AnyActive())
                {
                    // The round's last word (the claim) has gone: the table clears for a breath.
                    _draining = true;
                    _drainTime = 0f;
                    _hazards?.Drain();
                }
            }
            if (_draining)
            {
                _drainTime += dt;
                if ((_hazards == null || _hazards.Quiet) || _drainTime >= DrainLimit) NewRound();
            }
            if (!_slipThisRound && _round >= 2 && _roundTime > 4f) SpawnSlip();
            return spoken;
        }

        bool Ready()
        {
            for (int i = 0; i < _statements.Count; i++)
            {
                var s = _statements[i];
                if (s.active && s.fate == Fate.Flying && s.age < s.life * Overlap) return false;
            }
            return true;
        }

        bool AnyActive()
        {
            for (int i = 0; i < _statements.Count; i++) if (_statements[i].active) return true;
            return false;
        }

        // ---------- Rounds ----------

        void NewRound()
        {
            _round++;
            _roundTime = 0f;
            _draining = false;
            _slipThisRound = false;
            _nextAt = 0.5f;
            if (_hazards != null) _light = _hazards.Light;
            _seed = _seed * 6364136223846793005UL + 1442695040888963407UL;
            _hazards = new PressurePhase(_engine.DebateSpec(), _seed, null);
            _hazards.PlaceLight(_light);
            BuildLineup();
        }

        /// <summary>The round's statements: every premise still standing (with its question after it, while it can be
        /// answered back), in the case's order, then the claim.</summary>
        void BuildLineup()
        {
            _lineup.Clear();
            foreach (var p in _engine.Def.premises)
            {
                if (p == null || _engine.Status(p.id) != PremiseStatus.Standing) continue;
                _lineup.Add(new Pending { premise = p.id });
                if (QuestionOpen(p.id)) _lineup.Add(new Pending { premise = p.id, question = true });
            }
            _lineup.Add(new Pending { premise = ArgumentEngine.ClaimId });
        }

        /// <summary>After an exchange: what fell leaves (in flight it shatters or settles), what came out joins the round
        /// next.</summary>
        public void Refresh()
        {
            for (int i = _lineup.Count - 1; i >= 0; i--)
            {
                var l = _lineup[i];
                if (l.premise == ArgumentEngine.ClaimId) continue;
                if (_engine.Status(l.premise) != PremiseStatus.Standing || (l.question && !QuestionOpen(l.premise))) _lineup.RemoveAt(i);
            }
            for (int i = 0; i < _statements.Count; i++)
            {
                var s = _statements[i];
                if (!s.active || s.fate != Fate.Flying || s.claim) continue;
                var st = _engine.Status(s.premise);
                if (st == PremiseStatus.Broken || st == PremiseStatus.Forced) { s.fate = Fate.Shattered; s.fateAge = 0f; }
                else if (st != PremiseStatus.Standing || (s.question && !QuestionOpen(s.premise))) { s.fate = Fate.Settled; s.fateAge = 0f; }
                _statements[i] = s;
            }
            // Newly standing premises go to the front of what's left of the round.
            int insert = 0;
            foreach (var p in _engine.Def.premises)
            {
                if (p == null || _engine.Status(p.id) != PremiseStatus.Standing) continue;
                if (InLineup(p.id, false) || InFlight(p.id) || _said.Contains(p.id)) continue;
                _lineup.Insert(insert++, new Pending { premise = p.id });
                if (QuestionOpen(p.id)) _lineup.Insert(insert++, new Pending { premise = p.id, question = true });
            }
            if (_engine.Status(_caught ?? "") != PremiseStatus.Hidden) _caught = null;
        }

        /// <summary>Their question about a premise is still there to answer back (it stands, and has a retort).</summary>
        bool QuestionOpen(string id)
        {
            var p = _engine.Def.Premise(id);
            return p != null && p.retort != null && !string.IsNullOrEmpty(p.question) && _engine.Status(id) == PremiseStatus.Standing;
        }

        bool InLineup(string id, bool question)
        {
            foreach (var l in _lineup) if (l.premise == id && l.question == question) return true;
            return false;
        }

        bool InFlight(string id)
        {
            foreach (var s in _statements) if (s.active && s.premise == id && !s.question) return true;
            return false;
        }

        /// <summary>The light's position, kept across pauses (a phase elsewhere, a talk).</summary>
        public void PlaceLight(Vector2 p)
        {
            _light = p;
            _hazards?.PlaceLight(p);
        }

        // ---------- Statements ----------

        int Spawn(Pending p)
        {
            var def = _engine.Def;
            string text;
            string weak = "";
            bool claim = p.premise == ArgumentEngine.ClaimId;
            if (claim) text = def.claim;
            else
            {
                var pd = def.Premise(p.premise);
                if (pd == null) return 0;
                text = p.question ? pd.question : pd.text;
                if (!p.question) weak = pd.weak;
            }
            string plain = Typewriter.StripTags(Typewriter.Spelled(text ?? ""));
            Split(plain, weak, out string pre, out string w, out string post);
            string key = p.premise + (p.question ? "?" : "");
            bool repeat = !_said.Add(key);
            float width = plain.Length * CharWidth + Padding;
            float maxW = 2f * PressurePhase.HalfWidth - 0.3f;
            float height = LineHeight;
            if (width > maxW) { height = LineHeight * 2f; width = Math.Min(maxW, width * 0.55f + Padding); }
            float life = Math.Max(6f, Math.Min(9.5f, 4.6f + plain.Length * 0.05f));
            // Patterns: in from the right, in from the left, then one that settles in place and drifts (Danganronpa's
            // mix), never twice the same lane running.
            var flight = (Flight)(_patternCycle++ % 3);
            if (claim) flight = Flight.Drift;
            int lane = PickLane();
            float y = LaneY(lane, height);
            Vector2 pos, vel;
            float half = width * 0.5f;
            switch (flight)
            {
                case Flight.FromRight:
                    pos = new Vector2(PressurePhase.HalfWidth + half - 0.2f, y);
                    vel = new Vector2(-(2f * PressurePhase.HalfWidth + width - 0.4f) / life, _rng.Range(-0.05f, 0.05f));
                    break;
                case Flight.FromLeft:
                    pos = new Vector2(-PressurePhase.HalfWidth - half + 0.2f, y);
                    vel = new Vector2((2f * PressurePhase.HalfWidth + width - 0.4f) / life, _rng.Range(-0.05f, 0.05f));
                    break;
                default:
                    float room = Math.Max(0f, PressurePhase.HalfWidth - half - 0.25f);
                    pos = new Vector2(_rng.Range(-room, room), y);
                    vel = new Vector2(_rng.Range(-0.12f, 0.12f), _rng.Range(-0.04f, 0.04f));
                    life = Math.Max(4.5f, life * 0.85f);
                    break;
            }
            var s = new Statement
            {
                serial = ++_serial, premise = p.premise, question = p.question, claim = claim,
                pre = pre, weak = w, post = post, repeat = repeat,
                flight = flight, position = pos, velocity = vel, life = life, tilt = _rng.Range(-3f, 3f),
                width = width, height = height, active = true,
            };
            EstimateWeak(ref s);
            // Guards: the claim and her questions say themselves plainly; a premise pushes back once, a key one (or any,
            // once she's heated) more.
            if (!claim && !p.question)
            {
                var pd = def.Premise(p.premise);
                s.guards = 1 + (pd != null && pd.key ? 1 : 0) + (_engine.Heat >= 2 ? 1 : 0);
                s.nextGuard = 0.7f;
            }
            int slot = -1;
            for (int i = 0; i < _statements.Count; i++) if (!_statements[i].active) { slot = i; break; }
            if (slot < 0) { _statements.Add(s); slot = _statements.Count - 1; }
            else _statements[slot] = s;
            _nextAt = _roundTime;
            return s.serial;
        }

        int PickLane()
        {
            int lane = _rng.Range(0, 4);
            if (lane == _lastLane) lane = (lane + 1 + _rng.Range(0, 3)) % 4;
            _lastLane = lane;
            return lane;
        }

        static float LaneY(int lane, float height)
        {
            float top = PressurePhase.HalfHeight - 0.2f - height * 0.5f;
            float bottom = -PressurePhase.HalfHeight + 0.2f + height * 0.5f;
            return top + (bottom - top) * (lane / 3f);
        }

        /// <summary>Splits <paramref name="plain"/> around <paramref name="weak"/> (case-insensitive; the whole line if it
        /// isn't there, or there's none).</summary>
        public static void Split(string plain, string weak, out string pre, out string w, out string post)
        {
            plain = plain ?? "";
            int at = string.IsNullOrEmpty(weak) ? -1 : plain.IndexOf(weak, StringComparison.OrdinalIgnoreCase);
            if (at < 0) { pre = ""; w = plain; post = ""; return; }
            pre = plain.Substring(0, at);
            w = plain.Substring(at, weak.Length);
            post = plain.Substring(at + weak.Length);
        }

        static void EstimateWeak(ref Statement s)
        {
            float total = Math.Max(1, s.Plain.Length);
            float inner = s.width - Padding;
            s.weak0 = Padding * 0.5f + inner * (s.pre.Length / total);
            s.weak1 = Padding * 0.5f + inner * ((s.pre.Length + s.weak.Length) / total);
        }

        /// <summary>The view's measure of statement <paramref name="serial"/>: its size and where its weak words run
        /// (surface units, from its left edge). The statement keeps its centre.</summary>
        public void Measure(int serial, float width, float height, float weak0, float weak1)
        {
            for (int i = 0; i < _statements.Count; i++)
            {
                var s = _statements[i];
                if (s.serial != serial || !s.active) continue;
                if (width > 0.05f) s.width = width;
                if (height > 0.05f) s.height = height;
                s.weak0 = Math.Max(0f, weak0);
                s.weak1 = Math.Max(s.weak0, Math.Min(s.width, weak1));
                s.measured = true;
                _statements[i] = s;
                return;
            }
        }

        /// <summary>The statement under the light (its index, or -1), and whether the light is on its weak words.
        /// <paramref name="preferWeak"/>: of two under it, the one whose weak words it touches.</summary>
        public int Under(Vector2 light, bool preferWeak, out bool onWeak)
        {
            onWeak = false;
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < _statements.Count; i++)
            {
                var s = _statements[i];
                if (!s.active || s.fate != Fate.Flying || s.Alpha < 0.35f) continue;
                if (!Touches(s, light, 0f, s.width, out float d)) continue;
                bool weak = Touches(s, light, s.weak0, s.weak1, out _);
                float score = d - (preferWeak && weak ? 10f : 0f);
                if (score < bestScore) { bestScore = score; best = i; onWeak = weak; }
            }
            return best;
        }

        /// <summary>True if the light's aim ring overlaps the strip [from..to] (from the left edge) of a statement.</summary>
        static bool Touches(in Statement s, Vector2 light, float from, float to, out float distance)
        {
            float x0 = s.Left + from, x1 = s.Left + to;
            float y0 = s.position.Y - s.height * 0.5f, y1 = s.position.Y + s.height * 0.5f;
            float nx = Math.Max(x0, Math.Min(x1, light.X));
            float ny = Math.Max(y0, Math.Min(y1, light.Y));
            distance = Vector2.Distance(new Vector2(nx, ny), light);
            return distance <= AimRadius;
        }

        /// <summary>The middle of a statement's weak words (where to aim evidence).</summary>
        public static Vector2 WeakCentre(in Statement s) => new Vector2(s.Left + (s.weak0 + s.weak1) * 0.5f, s.position.Y);

        public Statement this[int i] => _statements[i];

        /// <summary>The statement in flight for a premise (its index, or -1). <paramref name="question"/>: its question.</summary>
        public int Find(string premise, bool question)
        {
            for (int i = 0; i < _statements.Count; i++)
            {
                var s = _statements[i];
                if (s.active && s.fate == Fate.Flying && s.premise == premise && s.question == question) return i;
            }
            return -1;
        }

        /// <summary>Marks a statement struck by a shot (it shudders: the view reads <see cref="Statement.fate"/>).</summary>
        public void Shatter(int index)
        {
            if (index < 0 || index >= _statements.Count) return;
            var s = _statements[index];
            s.fate = Fate.Shattered;
            s.fateAge = 0f;
            _statements[index] = s;
        }

        // ---------- Slips: what they aren't saying, drifting through ----------

        void SpawnSlip()
        {
            _slipThisRound = true;
            _engine.SlipsWaiting(_slipSeeds);
            if (_slipSeeds.Count == 0) return;
            foreach (var sl in _slips) if (sl.active) return;
            var seed = _slipSeeds[_rng.Range(0, _slipSeeds.Count)];
            bool fromLeft = _rng.Chance(0.5f);
            float y = _rng.Range(-PressurePhase.HalfHeight * 0.6f, PressurePhase.HalfHeight * 0.6f);
            var pos = new Vector2(fromLeft ? -PressurePhase.HalfWidth - 0.2f : PressurePhase.HalfWidth + 0.2f, y);
            var vel = new Vector2(fromLeft ? SlipSpeed : -SlipSpeed, _rng.Range(-0.12f, 0.12f));
            var slip = new DebateSlip { premise = seed.premise, name = seed.name, position = pos, velocity = vel, life = (2f * PressurePhase.HalfWidth + 0.8f) / SlipSpeed, active = true };
            for (int i = 0; i < _slips.Count; i++) if (!_slips[i].active) { _slips[i] = slip; return; }
            _slips.Add(slip);
        }

        void TickSlips(float dt)
        {
            for (int i = 0; i < _slips.Count; i++)
            {
                var s = _slips[i];
                if (!s.active) continue;
                s.age += dt;
                s.position += s.velocity * dt;
                // A gentle bob, like paper on air.
                s.position.Y += (float)Math.Sin(s.age * 2.3f) * 0.12f * dt;
                if (s.age >= s.life) s.active = false;
                else if (Vector2.DistanceSquared(s.position, Light) < (SlipRadius + PressurePhase.LightRadius) * (SlipRadius + PressurePhase.LightRadius))
                {
                    s.active = false;
                    if (_caught == null) _caught = s.premise;
                }
                _slips[i] = s;
            }
        }

        /// <summary>A slip the light caught since the last call (its premise), or null.</summary>
        public string TakeCaughtSlip()
        {
            string c = _caught;
            _caught = null;
            return c;
        }

        /// <summary>A hit from the debate's pressure since the last call.</summary>
        public int TakeHits() => _hazards != null ? _hazards.TakeHits() : 0;
    }
}

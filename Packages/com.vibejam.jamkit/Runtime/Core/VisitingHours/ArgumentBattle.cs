using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.BulletHell;
using JamKit.Patterns;

namespace JamKit.VisitingHours
{
    /// <summary>One emitter of a statement: a core pattern fired from a point above (or in) the box,
    /// optionally sweeping side to side. Box units: x -halfWidth..halfWidth, y -halfHeight..halfHeight.</summary>
    [Serializable]
    public class VolleyDef
    {
        public PatternDef pattern = new PatternDef();
        public float originX;
        public float originY = 2.6f;
        /// <summary>The origin sweeps this far either side (0 = still).</summary>
        public float sweepX;
        public float sweepHz = 0.3f;
    }

    /// <summary>Where Nadia stood when a statement ended: the lower zone listens, the upper presses.
    /// Spoke = she answered it with an object (cracked it).</summary>
    public enum Stance { None, Listen, Press, Spoke }

    /// <summary>One line of the speaker's argument: its words, its volley, and the deduction that answers it.</summary>
    [Serializable]
    public class StatementDef
    {
        public string text = "";
        /// <summary>The speaker's portrait expression while it plays.</summary>
        public string expression = "neutral";
        public float duration = 9f;
        public List<VolleyDef> volleys = new List<VolleyDef>();
        /// <summary>The entry that cracks it (empty: nothing does).</summary>
        public string key = "";
        /// <summary>Cracking it is a bonus: Convinced only needs the non-optional keys.</summary>
        public bool optional;
        /// <summary>Entries that drift into the box during it (only the ones Nadia knows).</summary>
        public List<string> offers = new List<string>();
        /// <summary>The speaker's reply when it cracks.</summary>
        public string crackLine = "";
        public string crackExpression = "sad";
        /// <summary>The reply to a wrong object (empty: the battle's default).</summary>
        public string wrongLine = "";
    }

    /// <summary>An argument battle, as data: the box, Nadia's composure, the practice volley,
    /// the statements and what the outcomes give.</summary>
    [Serializable]
    public class BattleDef
    {
        public string id = "walt";
        public string speaker = "Walt";
        public int composure = 5;
        public float halfWidth = 4f;
        public float halfHeight = 2.5f;
        /// <summary>Above this height is the Pressing zone (box units; 0 = the middle).</summary>
        public float pressLine;
        public float playerSpeed = 3.2f;
        public float playerRadius = 0.09f;
        /// <summary>Seconds after a hit when bullets pass through.</summary>
        public float hitInvulnerable = 1f;

        public string practiceText = "";
        public float practiceDuration = 5.5f;
        public List<VolleyDef> practice = new List<VolleyDef>();

        public List<StatementDef> statements = new List<StatementDef>();
        /// <summary>The pause between statements (bullets cleared, the reply shows).</summary>
        public float interlude = 1.4f;
        /// <summary>The pause after a crack (time to read both lines).</summary>
        public float crackInterlude = 3.2f;

        /// <summary>Pressing: bullets this much faster and denser while Nadia is in the upper zone.</summary>
        public float pressSpeedScale = 1.3f;
        public float pressDensityScale = 1.5f;
        /// <summary>The volley after a crack is gentler; the one after a wrong object harsher.</summary>
        public float softerScale = 0.7f;
        public float harsherScale = 1.25f;

        public float objectRadius = 0.32f;
        public float objectSpeed = 0.75f;
        public float objectFirstDelay = 1.6f;
        public float objectInterval = 2.4f;
        public int maxObjects = 2;

        public string defaultWrongLine = "That's not what this is about.";
        /// <summary>Convinced writes this truth; Heard out gives this personal entry.</summary>
        public string convincedEntry = "";
        public string heardOutEntry = "";
        /// <summary>Collision radius per bullet style (style = PatternDef.styleIndex).</summary>
        public List<float> styleRadii = new List<float> { 0.08f, 0.1f, 0.085f, 0.08f };
    }

    public enum BattlePhase { NotStarted, Practice, Statement, Interlude, Done }

    public enum BattleOutcome { None, Convinced, HeardOut, Pressed, ComposureLost }

    public enum BattleEventKind { PracticeStart, StatementStart, StatementEnd, Hit, Absorbed, ObjectAppeared, Crack, Wrong, Outcome }

    public struct BattleEvent
    {
        public BattleEventKind kind;
        public int statement;
        public string entry;
        public Stance stance;
    }

    /// <summary>An object in the box: a deduction or memory Nadia can say by touching it.</summary>
    public struct BoxObject
    {
        public string entry;
        public Vector2 position;
        public float baseY;
        public float velocityX;
        public float age;
        public float phase;
        public bool active;
    }

    /// <summary>
    /// An Undertale-style argument, pure logic (no Unity): the speaker's statements are volleys
    /// (core <see cref="Emitter"/>s into a core <see cref="BulletPool"/>), Nadia's composure is a small
    /// light in the box with a few hits, and there are no menus. Where she stands is her stance:
    /// the lower zone listens, the upper (nearer the speaker) presses, with denser, faster volleys;
    /// her zone when a statement ends counts as her choice. Deductions she has actually made drift
    /// into the box during the statements they answer: touching the right one cracks the statement
    /// (a softer next volley), a wrong one gets a line and a harsher next volley. Objects she
    /// hasn't found never appear. Outcomes: see <see cref="Resolve"/>.
    /// Tick it with the frame's dt and the move input; read <see cref="Events"/> after each tick.
    /// </summary>
    public sealed class ArgumentBattle
    {
        readonly BattleDef _def;
        readonly Journal _journal;
        readonly Rng _rng;
        readonly BulletPool _bullets = new BulletPool(1024);
        readonly List<Emitter> _emitters = new List<Emitter>();
        readonly List<VolleyDef> _volleys = new List<VolleyDef>();
        readonly List<BulletSpawn> _spawns = new List<BulletSpawn>(256);
        readonly List<BattleEvent> _events = new List<BattleEvent>(16);
        readonly HashSet<string> _spent = new HashSet<string>(StringComparer.Ordinal);
        readonly BoxObject[] _objects;
        readonly Stance[] _stances;
        readonly bool[] _cracked;

        BattlePhase _phase = BattlePhase.NotStarted;
        BattleOutcome _outcome;
        int _statement = -1;
        float _phaseTime, _time, _interludeLength, _iframes, _objectTimer;
        float _currentScale = 1f, _nextScale = 1f;
        int _composure, _wrongs, _offerCursor, _spawnSide = 1, _lineSerial, _fired;
        string _lastSay = "", _lastReply = "", _lastReplyExpression = "";
        Vector2 _player;
        bool _invulnerable;
        float _speedScale = 1f, _densityScale = 1f;

        public ArgumentBattle(BattleDef def, Journal journal, ulong seed = 1)
        {
            _def = def ?? new BattleDef();
            _journal = journal;
            _rng = new Rng(seed == 0 ? 1UL : seed);
            _objects = new BoxObject[Math.Max(1, _def.maxObjects)];
            int n = _def.statements != null ? _def.statements.Count : 0;
            _stances = new Stance[n];
            _cracked = new bool[n];
            _composure = Math.Max(1, _def.composure);
            _player = new Vector2(0f, -_def.halfHeight * 0.55f);
        }

        public BattleDef Def => _def;
        public BattlePhase Phase => _phase;
        public BattleOutcome Outcome => _outcome;
        /// <summary>The statement playing (or just played, during an interlude); -1 during practice.</summary>
        public int StatementIndex => _statement;
        public StatementDef Statement => _statement >= 0 && _statement < StatementCount ? _def.statements[_statement] : null;
        public int StatementCount => _stances.Length;
        public float PhaseTime => _phaseTime;
        public float Time => _time;
        /// <summary>Seconds until the statement ends and the zone counts (0 outside statements).</summary>
        public float StatementTimeLeft => _phase == BattlePhase.Statement && Statement != null ? Math.Max(0f, Statement.duration - _phaseTime) : 0f;
        /// <summary>This statement's volley strength: below 1 after a crack, above 1 after a wrong object.</summary>
        public float VolleyScale => _currentScale;
        public int Composure => _composure;
        public int MaxComposure => Math.Max(1, _def.composure);
        public int WrongCount => _wrongs;
        public bool IsCracked(int statement) => statement >= 0 && statement < _cracked.Length && _cracked[statement];
        public Stance StanceAt(int statement) => statement >= 0 && statement < _stances.Length ? _stances[statement] : Stance.None;
        public int Presses => Count(Stance.Press);
        public int Listens => Count(Stance.Listen);
        /// <summary>The Force this battle adds: each pressed statement, plus one for a Pressed outcome.</summary>
        public int ForceAdded => Presses + (_outcome == BattleOutcome.Pressed ? 1 : 0);
        /// <summary>Heard out the clean way: never pressed and no wrong object.</summary>
        public bool HeardOutClean => _outcome == BattleOutcome.HeardOut && Presses == 0 && _wrongs == 0;

        public BulletPool Bullets => _bullets;
        /// <summary>Bullets fired since the start (all volleys).</summary>
        public int BulletsFired => _fired;
        public Vector2 Player => _player;
        public bool InPressZone => _player.Y > _def.pressLine;
        /// <summary>Seconds of hit invulnerability left (the light blinks).</summary>
        public float Blink => Math.Max(0f, _iframes);
        public BoxObject[] Objects => _objects;
        public IReadOnlyList<BattleEvent> Events => _events;

        /// <summary>What Nadia said last (an object's say line) and the speaker's reply; the serial goes up with each new pair.</summary>
        public string LastSay => _lastSay;
        public string LastReply => _lastReply;
        public string LastReplyExpression => _lastReplyExpression;
        public int LineSerial => _lineSerial;

        /// <summary>No hits land (practice is always like this; god mode and the smoke run set it).</summary>
        public bool Invulnerable
        {
            get => _invulnerable;
            set => _invulnerable = value;
        }

        /// <summary>Difficulty assists (Settings' enemy bullet speed and density).</summary>
        public float SpeedScale
        {
            get => _speedScale;
            set => _speedScale = Math.Max(0.1f, value);
        }

        public float DensityScale
        {
            get => _densityScale;
            set => _densityScale = Math.Max(0.1f, value);
        }

        int Count(Stance s)
        {
            int n = 0;
            for (int i = 0; i < _stances.Length; i++) if (_stances[i] == s) n++;
            return n;
        }

        // ---------- Flow ----------

        /// <summary>Starts with the practice volley (or the first statement when there is none).</summary>
        public void Start()
        {
            if (_phase != BattlePhase.NotStarted) return;
            _events.Clear();
            if (_def.practice != null && _def.practice.Count > 0 && _def.practiceDuration > 0f)
            {
                _phase = BattlePhase.Practice;
                _phaseTime = 0f;
                SetVolleys(_def.practice);
                Emit(BattleEventKind.PracticeStart, -1, "", Stance.None);
            }
            else BeginStatement(0);
        }

        /// <summary>Advances by <paramref name="dt"/> with the move input (length 0..1, y up).</summary>
        public void Tick(float dt, Vector2 move)
        {
            _events.Clear();
            if (_phase == BattlePhase.NotStarted || _phase == BattlePhase.Done || !(dt > 0f)) return;
            _phaseTime += dt;
            _time += dt;

            float len = move.Length();
            if (len > 1f) move /= len;
            _player += move * (_def.playerSpeed * dt);
            float r = _def.playerRadius;
            _player = new Vector2(Clamp(_player.X, -_def.halfWidth + r, _def.halfWidth - r), Clamp(_player.Y, -_def.halfHeight + r, _def.halfHeight - r));

            if (_phase == BattlePhase.Practice || _phase == BattlePhase.Statement) Fire(dt);
            _bullets.Step(dt, new PlayfieldBounds(-_def.halfWidth, -_def.halfHeight, _def.halfWidth, _def.halfHeight + 1.5f), 1.2f);

            if (_iframes > 0f) _iframes -= dt;
            if (Collide()) return;

            if (_phase == BattlePhase.Statement)
            {
                TickObjects(dt);
                if (_phase != BattlePhase.Statement) return;
                if (_phaseTime >= Statement.duration) EndStatement(InPressZone ? Stance.Press : Stance.Listen, _def.interlude);
            }
            else if (_phase == BattlePhase.Practice)
            {
                if (_phaseTime >= _def.practiceDuration)
                {
                    _bullets.Clear();
                    _emitters.Clear();
                    BeginInterlude(_def.interlude);
                }
            }
            else if (_phase == BattlePhase.Interlude && _phaseTime >= _interludeLength)
            {
                int next = _statement + 1;
                if (next < StatementCount) BeginStatement(next);
                else Finish(Resolve(_def, _stances, _cracked, _composure));
            }
        }

        /// <summary>Ends the battle now with an outcome (debug skip).</summary>
        public void ForceOutcome(BattleOutcome outcome)
        {
            if (_phase == BattlePhase.Done) return;
            _events.Clear();
            Finish(outcome);
        }

        void BeginStatement(int index)
        {
            _statement = index;
            _phase = BattlePhase.Statement;
            _phaseTime = 0f;
            _currentScale = _nextScale;
            _nextScale = 1f;
            _spent.Clear();
            _offerCursor = 0;
            _objectTimer = _def.objectFirstDelay;
            ClearObjects();
            SetVolleys(Statement.volleys);
            Emit(BattleEventKind.StatementStart, index, "", Stance.None);
        }

        void EndStatement(Stance stance, float pause)
        {
            _stances[_statement] = stance;
            _bullets.Clear();
            _emitters.Clear();
            ClearObjects();
            Emit(BattleEventKind.StatementEnd, _statement, "", stance);
            BeginInterlude(pause);
        }

        void BeginInterlude(float seconds)
        {
            _phase = BattlePhase.Interlude;
            _phaseTime = 0f;
            _interludeLength = Math.Max(0.05f, seconds);
        }

        void Finish(BattleOutcome outcome)
        {
            _outcome = outcome;
            _phase = BattlePhase.Done;
            _bullets.Clear();
            _emitters.Clear();
            ClearObjects();
            Emit(BattleEventKind.Outcome, _statement, "", Stance.None);
        }

        /// <summary>
        /// The outcome rules. Composure at 0: lost. Every key (non-optional) statement cracked:
        /// Convinced. Otherwise mixed play falls through by what was done most: pressed at least
        /// half the statements is Pressed, anything else Heard out (strictly, Heard out is listening
        /// to every statement with no wrong object; see <see cref="HeardOutClean"/>).
        /// A battle with no key statements can't be won by Convinced.
        /// </summary>
        public static BattleOutcome Resolve(BattleDef def, Stance[] stances, bool[] cracked, int composure)
        {
            if (composure <= 0) return BattleOutcome.ComposureLost;
            int keys = 0, keysCracked = 0, presses = 0;
            int n = def != null && def.statements != null ? def.statements.Count : 0;
            for (int i = 0; i < n; i++)
            {
                var s = def.statements[i];
                if (s != null && !string.IsNullOrEmpty(s.key) && !s.optional)
                {
                    keys++;
                    if (cracked != null && i < cracked.Length && cracked[i]) keysCracked++;
                }
                if (stances != null && i < stances.Length && stances[i] == Stance.Press) presses++;
            }
            if (keys > 0 && keysCracked == keys) return BattleOutcome.Convinced;
            if (presses > 0 && presses * 2 >= n) return BattleOutcome.Pressed;
            return BattleOutcome.HeardOut;
        }

        // ---------- Volleys and hits ----------

        void SetVolleys(List<VolleyDef> volleys)
        {
            _emitters.Clear();
            _volleys.Clear();
            if (volleys == null) return;
            for (int i = 0; i < volleys.Count; i++)
            {
                var v = volleys[i];
                if (v == null || v.pattern == null) continue;
                _volleys.Add(v);
                _emitters.Add(new Emitter(v.pattern, _rng));
            }
        }

        void Fire(float dt)
        {
            bool pressing = _phase == BattlePhase.Statement && InPressZone;
            float speed = _speedScale * _currentScale * (pressing ? _def.pressSpeedScale : 1f);
            float density = _densityScale * _currentScale * (pressing ? _def.pressDensityScale : 1f);
            if (_phase == BattlePhase.Practice) { speed = _speedScale; density = 1f; }
            _spawns.Clear();
            for (int i = 0; i < _emitters.Count; i++)
            {
                var e = _emitters[i];
                var v = _volleys[i];
                e.SpeedScale = speed;
                e.DensityScale = density;
                float x = v.originX;
                if (v.sweepX != 0f) x += v.sweepX * (float)Math.Sin(_time * v.sweepHz * Math.PI * 2.0);
                e.Tick(dt, new Vector2(x, v.originY), _player, _spawns);
            }
            if (_spawns.Count > 0) _fired += _bullets.AddSpawns(_spawns, _def.styleRadii, 1f, 0.09f);
            _spawns.Clear();
        }

        /// <summary>True when the battle ended (composure lost).</summary>
        bool Collide()
        {
            if (_iframes > 0f && _phase != BattlePhase.Practice) return false;
            int hit = _bullets.FirstOverlap(_player, _def.playerRadius);
            if (hit < 0) return false;
            _bullets.RemoveAt(hit);
            if (_phase == BattlePhase.Practice || _invulnerable)
            {
                Emit(BattleEventKind.Absorbed, _statement, "", Stance.None);
                return false;
            }
            _composure--;
            _iframes = _def.hitInvulnerable;
            Emit(BattleEventKind.Hit, _statement, "", Stance.None);
            if (_composure > 0) return false;
            Finish(BattleOutcome.ComposureLost);
            return true;
        }

        // ---------- Objects ----------

        void ClearObjects()
        {
            for (int i = 0; i < _objects.Length; i++) _objects[i].active = false;
        }

        int ActiveObjects()
        {
            int n = 0;
            for (int i = 0; i < _objects.Length; i++) if (_objects[i].active) n++;
            return n;
        }

        bool OnScreen(string entry)
        {
            for (int i = 0; i < _objects.Length; i++) if (_objects[i].active && _objects[i].entry == entry) return true;
            return false;
        }

        void TickObjects(float dt)
        {
            _objectTimer -= dt;
            if (_objectTimer <= 0f)
            {
                _objectTimer += Math.Max(0.2f, _def.objectInterval);
                TrySpawnObject();
            }
            float edge = _def.halfWidth + 0.6f;
            float reach = _def.objectRadius + _def.playerRadius;
            for (int i = 0; i < _objects.Length; i++)
            {
                ref var o = ref _objects[i];
                if (!o.active) continue;
                o.age += dt;
                float x = o.position.X + o.velocityX * dt;
                float y = o.baseY + 0.18f * (float)Math.Sin(o.age * 1.6f + o.phase);
                o.position = new Vector2(x, y);
                if ((x > edge && o.velocityX > 0f) || (x < -edge && o.velocityX < 0f)) { o.active = false; continue; }
                if (Math.Abs(x) > _def.halfWidth) continue; // not in the box yet
                if (Vector2.DistanceSquared(o.position, _player) > reach * reach) continue;
                string entry = o.entry;
                o.active = false;
                Touch(entry);
                if (_phase != BattlePhase.Statement) return;
            }
        }

        void TrySpawnObject()
        {
            var s = Statement;
            if (s == null || s.offers == null || s.offers.Count == 0 || _journal == null) return;
            if (ActiveObjects() >= _objects.Length) return;
            for (int k = 0; k < s.offers.Count; k++)
            {
                int idx = (_offerCursor + k) % s.offers.Count;
                string entry = s.offers[idx];
                if (string.IsNullOrEmpty(entry) || !_journal.Knows(entry) || _spent.Contains(entry) || OnScreen(entry)) continue;
                _offerCursor = idx + 1;
                for (int i = 0; i < _objects.Length; i++)
                {
                    if (_objects[i].active) continue;
                    _spawnSide = -_spawnSide;
                    float baseY = _rng.Range(-_def.halfHeight * 0.7f, _def.halfHeight * 0.7f);
                    _objects[i] = new BoxObject
                    {
                        entry = entry,
                        position = new Vector2(_spawnSide * (_def.halfWidth + 0.4f), baseY),
                        baseY = baseY,
                        velocityX = -_spawnSide * _def.objectSpeed,
                        age = 0f,
                        phase = _rng.Range(0f, 6.28f),
                        active = true,
                    };
                    Emit(BattleEventKind.ObjectAppeared, _statement, entry, Stance.None);
                    return;
                }
                return;
            }
        }

        void Touch(string entry)
        {
            var s = Statement;
            var def = _journal != null ? _journal.Book.Find(entry) : null;
            _lastSay = def != null && !string.IsNullOrEmpty(def.sayLine) ? def.sayLine : (def != null ? def.title : entry);
            _lineSerial++;
            if (s != null && !string.IsNullOrEmpty(s.key) && s.key == entry)
            {
                _cracked[_statement] = true;
                _lastReply = s.crackLine ?? "";
                _lastReplyExpression = string.IsNullOrEmpty(s.crackExpression) ? s.expression : s.crackExpression;
                _nextScale *= _def.softerScale;
                Emit(BattleEventKind.Crack, _statement, entry, Stance.Spoke);
                EndStatement(Stance.Spoke, _def.crackInterlude);
                return;
            }
            _wrongs++;
            _spent.Add(entry);
            _lastReply = s != null && !string.IsNullOrEmpty(s.wrongLine) ? s.wrongLine : _def.defaultWrongLine;
            _lastReplyExpression = "firm";
            _nextScale = Math.Min(1.8f, _nextScale * _def.harsherScale);
            Emit(BattleEventKind.Wrong, _statement, entry, Stance.None);
        }

        /// <summary>The active object showing <paramref name="entry"/>, if any (the pilot goes for it).</summary>
        public bool TryFindObject(string entry, out Vector2 position)
        {
            for (int i = 0; i < _objects.Length; i++)
                if (_objects[i].active && _objects[i].entry == entry && Math.Abs(_objects[i].position.X) <= _def.halfWidth)
                {
                    position = _objects[i].position;
                    return true;
                }
            position = Vector2.Zero;
            return false;
        }

        void Emit(BattleEventKind kind, int statement, string entry, Stance stance)
        {
            _events.Add(new BattleEvent { kind = kind, statement = statement, entry = entry, stance = stance });
        }

        static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
    }

    /// <summary>What a scripted player (tests, the smoke run) does during one statement.</summary>
    public enum PilotAction { Listen, Press, Touch }

    /// <summary>
    /// A simple autopilot for the box: head for a goal (the listening spot, the pressing spot, or an
    /// object to say) while sliding away from nearby bullets. Allocation-free.
    /// </summary>
    public static class BattlePilot
    {
        public static Vector2 Goal(ArgumentBattle b, PilotAction action, string entry)
        {
            var d = b.Def;
            if (action == PilotAction.Touch && b.TryFindObject(entry, out var at)) return at;
            if (action == PilotAction.Press) return new Vector2(0f, d.halfHeight * 0.55f);
            return new Vector2(0f, -d.halfHeight * 0.55f);
        }

        public static Vector2 Steer(ArgumentBattle b, Vector2 goal, float dodgeRadius = 0.8f)
        {
            Vector2 p = b.Player;
            Vector2 to = goal - p;
            float dist = to.Length();
            Vector2 want = dist > 0.04f ? to / Math.Max(dist, 0.25f) : Vector2.Zero;
            if (want.Length() > 1f) want = Vector2.Normalize(want);
            Vector2 away = Vector2.Zero;
            var items = b.Bullets.Items;
            float r2 = dodgeRadius * dodgeRadius;
            for (int i = 0; i < b.Bullets.Count; i++)
            {
                Vector2 off = p - items[i].position;
                float d2 = off.LengthSquared();
                if (d2 > r2 || d2 < 1e-6f) continue;
                away += off / d2 * 0.12f;
            }
            Vector2 m = want + away;
            float len = m.Length();
            return len > 1f ? m / len : m;
        }
    }
}

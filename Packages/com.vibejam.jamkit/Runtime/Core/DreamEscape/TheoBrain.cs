using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Stealth;

namespace JamKit.DreamEscape
{
    /// <summary>What Theo is doing.</summary>
    public enum TheoState { Idle, Counting, Seeking, Investigating, Coming, Celebrating }

    /// <summary>What Theo says this tick (the presenter turns it into a line and a sound; <see cref="DreamHouseLines"/>).</summary>
    public enum TheoSay { None, Count, CountDone, Peeked, SawYouGo, RoomCall, Heard, Suspicious, CheckedEmpty, Giggle, LostYou, AsksMum, Found, Coming }

    /// <summary>One tick's events (a struct: no allocation).</summary>
    public struct TheoEvents
    {
        public TheoSay say;
        /// <summary>The count's number, the storey he saw her go to, a line's variant.</summary>
        public int arg;
        public string room;
        /// <summary>He found her this tick (saw her for long enough, or opened the spot she's in).</summary>
        public bool found;
        public int foundInSpot;
        /// <summary>A hiding spot he checked this tick (its index), -1 for none.</summary>
        public int checkedSpot;
        public static TheoEvents None => new TheoEvents { foundInSpot = -1, checkedSpot = -1 };
    }

    /// <summary>
    /// Theo seeking (docs/redesign/house.md "Theo"), engine-free and multi-floor. He counts at the table (his face in his
    /// arms) and peeks once, like a kid; then he searches the house room by room, upstairs and down, out into the yard,
    /// the way a ten-year-old would: rooms he hasn't tried yet first, the floor he glimpsed her on, the room a noise came
    /// from, his favourites; in each room he looks round and checks the hiding places (not all of them: he's ten), opening
    /// cupboards and pulling back curtains. He hears her (running, landing, sliding, a wall run: through walls less, through
    /// floors less again) and goes to look. Seeing her fills his meter (crouching, the dark and cloth slow it); full means
    /// "found you": in hide-and-seek, being seen is being found. When she's found every memory he comes straight for her.
    /// Sight and the meter are the stealth kit's (<see cref="StealthSight"/>, <see cref="DetectionMeter"/>). He walks the
    /// house's graph (<see cref="DreamHouse.Graph"/>); stairs are links he walks up and down. No allocation per tick.
    /// </summary>
    public sealed class TheoBrain
    {
        enum Act { None, Walk, Look, Check, Wait }

        readonly DreamHouse _house;
        readonly IStealthWorld _world;
        readonly WaypointGraph _graph;
        readonly StealthDef _def;
        readonly DetectionMeter _meter = new DetectionMeter();
        readonly Random _rng;
        readonly List<int> _path = new List<int>(64);
        readonly List<string> _roomIds = new List<string>();
        readonly bool[] _roomDone;
        readonly int[] _roomNode;      // a node in each room (its most central)
        readonly List<int> _checks = new List<int>(8);

        TheoState _state = TheoState.Idle;
        Act _act;
        Vector3 _pos, _goal;
        float _yaw, _actTime, _actLen, _speed;
        int _pathIndex;
        bool _walkStraight;
        float _countLeft, _countTotal, _peekAt, _peekLeft;
        int _lastCountSaid;
        bool _peeked;
        int _storeyHint = -1;
        string _noiseRoom;
        float _noiseAge = 99f, _heardCooldown, _giggleCooldown = 6f, _callCooldown, _suspiciousCooldown;
        int _room = -1;           // the room he's searching (index into _roomIds)
        int _checkIndex;
        Vector3 _investigate;
        float _celebrate;
        int _level;
        int _sweeps;
        bool _askedThisSweep;
        float _visibility;
        Vector3 _target;
        TheoEvents _ev = TheoEvents.None;

        /// <summary>Theo's senses and pace: a ten-year-old's (eyes at 1.2 m; quicker to see you the longer the game goes on).</summary>
        public static StealthDef Def()
        {
            return new StealthDef
            {
                eyeHeight = 1.2f, sightRange = 13f, coneHalfAngle = 42f, peripheralHalfAngle = 80f, peripheralScale = 0.3f,
                closeRange = 1.0f, nearRange = 4f, farScale = 0.35f, crouchScale = 0.5f, shadowScale = 0.25f, bushScale = 0.1f,
                bushRevealRange = 1.2f, fillRate = 0.95f, drainDelay = 1.0f, drainRate = 0.35f, lookAroundTime = 1.8f,
                lookAroundAngle = 70f, checkSpotTime = 1.2f, catchRange = 1.1f, searchSpeed = 2.4f, investigateSpeed = 2.8f,
                turnRate = 240f, hearingScale = 1f, occludedHearing = 0.5f,
            };
        }

        /// <summary>Per catch (0, 1, 2): how fast he walks, how many hiding places he checks, how fast he sees.</summary>
        static readonly float[] LevelSpeed = { 2.3f, 2.6f, 2.9f }, LevelCheck = { 0.7f, 0.82f, 0.92f }, LevelFill = { 1f, 1.15f, 1.3f };
        /// <summary>Through a floor he hears this much of a noise.</summary>
        public const float ThroughFloor = 0.35f;

        public TheoBrain(DreamHouse house, IStealthWorld world, WaypointGraph graph, StealthDef def = null, int seed = 10)
        {
            _house = house;
            _world = world;
            _graph = graph;
            _def = def ?? Def();
            _rng = new Random(seed);
            foreach (var r in house.rooms)
                if (r.id != "street" && r.id != "understairs" && r.id != "atticstairs" && r.id != "frontgarden") _roomIds.Add(r.id);
            _roomDone = new bool[_roomIds.Count];
            _roomNode = new int[_roomIds.Count];
            for (int k = 0; k < _roomIds.Count; k++)
            {
                var room = house.Room(_roomIds[k]);
                int best = -1;
                float bd = float.MaxValue;
                for (int n = 0; n < house.nodes.Count; n++)
                {
                    if (house.nodes[n].room != room.id) continue;
                    var c = room.Center;
                    float d = Vector3.DistanceSquared(house.nodes[n].p, c);
                    if (d < bd) { bd = d; best = n; }
                }
                _roomNode[k] = best;
            }
        }

        public TheoState State => _state;
        public Vector3 Position => _pos;
        public float Yaw => _yaw;
        public Vector3 Eye => _pos + new Vector3(0f, _def.eyeHeight, 0f);
        public float Meter => _meter.Value;
        public float Visibility => _visibility;
        public bool Counting => _state == TheoState.Counting;
        public bool Peeking => _peekLeft > 0f;
        public float CountLeft => _state == TheoState.Counting ? Math.Max(0f, _countLeft) : 0f;
        public string SearchingRoom => _room >= 0 ? _roomIds[_room] : null;
        public int CheckingSpot => _act == Act.Check && _checkIndex < _checks.Count ? _checks[_checkIndex] : -1;
        public bool Moving => _act == Act.Walk;
        public int Sweeps => _sweeps;
        public TheoEvents Events => _ev;
        public StealthDef Definition => _def;

        /// <summary>Counts at <paramref name="at"/> facing <paramref name="yaw"/> for <paramref name="seconds"/>; the game
        /// gets harder with each catch (<paramref name="level"/> 0..2).</summary>
        public void StartCounting(Vector3 at, float yaw, float seconds, int level)
        {
            _pos = at;
            _yaw = yaw;
            _level = Math.Max(0, Math.Min(2, level));
            _state = TheoState.Counting;
            _countTotal = _countLeft = seconds;
            _lastCountSaid = 0;
            _peeked = false;
            _peekAt = seconds * (0.35f + 0.35f * (float)_rng.NextDouble());
            _peekLeft = 0f;
            _storeyHint = -1;
            _meter.Reset();
            _act = Act.None;
            _room = -1;
            for (int k = 0; k < _roomDone.Length; k++) _roomDone[k] = false;
            _askedThisSweep = false;
        }

        /// <summary>Every memory is found: he comes straight to her (she's letting him).</summary>
        public void ComeFind(Vector3 her)
        {
            _state = TheoState.Coming;
            _target = her;
            WalkTo(her, _def.investigateSpeed);
        }

        /// <summary>Stops (a cutscene, the round's end).</summary>
        public void Stop()
        {
            _state = TheoState.Idle;
            _act = Act.None;
        }

        public void Place(Vector3 at, float yaw)
        {
            _pos = at;
            _yaw = yaw;
        }

        // =====================================================================================================

        public void Tick(float dt, in StealthTarget target, in StealthNoise noise)
        {
            _ev = TheoEvents.None;
            if (dt <= 0f) return;
            _heardCooldown -= dt; _giggleCooldown -= dt; _callCooldown -= dt; _suspiciousCooldown -= dt; _noiseAge += dt;
            if (target.present) _target = target.feet;
            switch (_state)
            {
                case TheoState.Counting: CountTick(dt, target); return;
                case TheoState.Celebrating:
                    _celebrate -= dt;
                    if (_celebrate <= 0f) _state = TheoState.Idle;
                    return;
                case TheoState.Idle: return;
            }
            // Seeing: his meter fills while he sees her; full is "found you".
            See(dt, target);
            if (_ev.found) return;
            Hear(noise, target);
            switch (_state)
            {
                case TheoState.Coming:
                    _target = target.present ? target.feet : _target;
                    if (Vector3.Distance(_pos, _target) <= _def.catchRange + 0.4f && Math.Abs(_pos.Y - _target.Y) < 1.2f) { Found(-1); return; }
                    _replan -= dt;
                    // Not while he's on a flight of stairs: from halfway up, the nearest point is the one he just left,
                    // and replanning walked him back down for ever (the round never ended).
                    if ((_replan <= 0f || _act != Act.Walk) && !(_act == Act.Walk && OnStairs)) { _replan = 0.6f; WalkTo(_target, _def.investigateSpeed); }
                    Step(dt);
                    if (_callCooldown <= 0f) { _callCooldown = 6f; Say(TheoSay.Coming); }
                    return;
                case TheoState.Investigating: InvestigateTick(dt); break;
                case TheoState.Seeking: SeekTick(dt); break;
            }
            // A giggle now and then when he's near her and hasn't seen her (the tension's cue).
            if (_giggleCooldown <= 0f && target.present && Vector3.Distance(_pos, target.feet) < 6f && _meter.Value < 0.2f)
            {
                _giggleCooldown = 9f + (float)_rng.NextDouble() * 6f;
                Say(TheoSay.Giggle, _rng.Next(3));
            }
        }

        float _replan;

        /// <summary>He's walking a link between floors (a flight, the ladder).</summary>
        bool OnStairs => _pathIndex > 0 && _pathIndex < _path.Count && Math.Abs(_graph[_path[_pathIndex]].Y - _graph[_path[_pathIndex - 1]].Y) > 0.3f;

        void CountTick(float dt, in StealthTarget target)
        {
            _countLeft -= dt;
            int said = (int)Math.Ceiling(_countTotal - _countLeft);
            // He counts aloud to twenty however long he counts for.
            int n = Math.Min(20, Math.Max(1, (int)Math.Ceiling(20f * (_countTotal - _countLeft) / Math.Max(0.1f, _countTotal))));
            if (n != _lastCountSaid && _countLeft > 0f) { _lastCountSaid = n; Say(TheoSay.Count, n); }
            // The peek: he lifts his head and looks round for a second.
            if (!_peeked && _countTotal - _countLeft >= _peekAt)
            {
                _peeked = true;
                _peekLeft = 1.1f;
                _yaw += (float)(_rng.NextDouble() * 120.0 - 60.0);
                Say(TheoSay.Peeked, _rng.Next(DreamHouseLines.Peeked.Length));
            }
            if (_peekLeft > 0f)
            {
                _peekLeft -= dt;
                float v = Visible(target);
                if (v > 0.12f && _storeyHint < 0)
                {
                    _storeyHint = StoreyHintOf(target.feet);
                    Say(TheoSay.SawYouGo, _storeyHint);
                }
            }
            if (_countLeft > 0f) return;
            _state = TheoState.Seeking;
            _meter.Reset();
            Say(TheoSay.CountDone);
            NextRoom();
        }

        int StoreyHintOf(Vector3 p)
        {
            var room = _house.RoomAt(p + new Vector3(0f, 0.1f, 0f));
            if (room != null && room.outside) return 4;
            return DreamHouse.StoreyOf(p.Y);
        }

        /// <summary>How well he'd see her from where he stands now, facing his way (what his meter fills by; tests).</summary>
        public float VisibilityOf(in StealthTarget target) => Visible(target);

        float Visible(in StealthTarget target)
        {
            if (!target.present) return 0f;
            HideKind? kind = target.hidingSpot >= 0 && target.hidingSpot < _house.spots.Count ? _house.spots[target.hidingSpot].kind : (HideKind?)null;
            return StealthSight.Visibility(_def, _world, Eye, _yaw, target, kind, out _);
        }

        void See(float dt, in StealthTarget target)
        {
            _visibility = Visible(target);
            _meter.Tick(_def, _visibility * _def.fillRate * LevelFill[_level], dt);
            if (_meter.Full) { Found(-1); return; }
            if (_meter.Value > 0.35f && _state == TheoState.Seeking)
            {
                // A glimpse: he turns to it and goes to look.
                if (_suspiciousCooldown <= 0f) { _suspiciousCooldown = 4f; Say(TheoSay.Suspicious, _rng.Next(DreamHouseLines.Suspicious.Length)); }
                Investigate(target.feet);
            }
            else if (_visibility > 0f && _state == TheoState.Investigating) _investigate = target.feet;
        }

        void Hear(in StealthNoise noise, in StealthTarget target)
        {
            if (!noise.Any || _heardCooldown > 0f) return;
            float r = noise.radius * _def.hearingScale;
            if (Math.Abs(noise.position.Y - _pos.Y) > 1.8f) r *= ThroughFloor;
            float d = Vector3.Distance(Eye, noise.position);
            if (d > r) return;
            if (_world != null && !_world.LineClear(Eye, noise.position + new Vector3(0f, 0.3f, 0f))) r *= _def.occludedHearing;
            if (d > r) return;
            _heardCooldown = 4f;
            var room = _house.RoomAt(noise.position + new Vector3(0f, 0.2f, 0f));
            _noiseRoom = room != null ? room.id : null;
            _noiseAge = 0f;
            Say(TheoSay.Heard, _rng.Next(DreamHouseLines.Heard.Length));
            if (_state == TheoState.Seeking || _state == TheoState.Investigating) Investigate(noise.position);
        }

        void Found(int spot)
        {
            _ev.found = true;
            _ev.foundInSpot = spot;
            Say(TheoSay.Found);
            _state = TheoState.Celebrating;
            _celebrate = 2.5f;
            _act = Act.None;
            _meter.Set(1f);
        }

        void Say(TheoSay what, int arg = 0, string room = null)
        {
            _ev.say = what;
            _ev.arg = arg;
            _ev.room = room;
        }

        // ---------- Searching ----------

        void Investigate(Vector3 at)
        {
            _state = TheoState.Investigating;
            _investigate = at;
            WalkTo(at, _def.investigateSpeed);
            _act = Act.Walk;
            _checks.Clear();
            _checkIndex = 0;
        }

        void InvestigateTick(float dt)
        {
            switch (_act)
            {
                case Act.Walk:
                    Step(dt);
                    if (_act == Act.Walk || _act == Act.Check) return;
                    // There: look round, then check every hiding place close by.
                    _act = Act.Look; _actTime = 0f; _actLen = _def.lookAroundTime; _lookCenter = _yaw;
                    _checks.Clear();
                    for (int s = 0; s < _house.spots.Count; s++)
                    {
                        var sp = _house.spots[s];
                        if (Math.Abs(sp.Approach.Y - _investigate.Y) < 1.5f && Vector3.Distance(sp.Center, _investigate) < 5.5f) _checks.Add(s);
                    }
                    _checkIndex = 0;
                    return;
                case Act.Look:
                    LookRound(dt);
                    if (_act == Act.Look) return;
                    if (!NextCheck()) { _state = TheoState.Seeking; Say(TheoSay.LostYou, _rng.Next(DreamHouseLines.LostYou.Length)); NextRoom(); }
                    return;
                case Act.Check:
                    CheckTick(dt);
                    if (_act == Act.None && !NextCheck()) { _state = TheoState.Seeking; NextRoom(); }
                    return;
                default:
                    _state = TheoState.Seeking;
                    NextRoom();
                    return;
            }
        }

        float _lookCenter;

        void SeekTick(float dt)
        {
            switch (_act)
            {
                case Act.Walk:
                    Step(dt);
                    if (_act == Act.Walk || _act == Act.Check) return;
                    // In the room: a look round, then its hiding places.
                    if (_room >= 0 && _callCooldown <= 0f && _rng.NextDouble() < 0.55)
                    {
                        string call = DreamHouseLines.RoomCall(_roomIds[_room]);
                        if (call != null) { _callCooldown = 7f; Say(TheoSay.RoomCall, 0, _roomIds[_room]); }
                    }
                    _act = Act.Look; _actTime = 0f; _actLen = _def.lookAroundTime; _lookCenter = _yaw;
                    PlanChecks();
                    return;
                case Act.Look:
                    LookRound(dt);
                    if (_act == Act.Look) return;
                    if (!NextCheck()) NextRoom();
                    return;
                case Act.Check:
                    CheckTick(dt);
                    if (_act == Act.None && !NextCheck()) NextRoom();
                    return;
                default:
                    NextRoom();
                    return;
            }
        }

        void PlanChecks()
        {
            _checks.Clear();
            _checkIndex = 0;
            if (_room < 0) return;
            string id = _roomIds[_room];
            for (int s = 0; s < _house.spots.Count; s++)
                if (_house.spotRooms[s] == id && _rng.NextDouble() < LevelCheck[_level]) _checks.Add(s);
        }

        /// <summary>Starts walking to the next hiding place to check; false when there are none left.</summary>
        bool NextCheck()
        {
            if (_checkIndex >= _checks.Count) { _act = Act.None; return false; }
            var sp = _house.spots[_checks[_checkIndex]];
            WalkTo(sp.Approach, _speed > 0f ? _speed : LevelSpeed[_level]);
            _checkPending = true;
            return true;
        }

        bool _checkPending;

        void CheckTick(float dt)
        {
            int s = _checks[_checkIndex];
            var sp = _house.spots[s];
            Face(YawTo(sp.Center), dt);
            _actTime += dt;
            if (_actTime < _def.checkSpotTime) return;
            _ev.checkedSpot = s;
            // Is she in it?
            if (_herSpot == s) { Found(s); return; }
            Say(TheoSay.CheckedEmpty, _rng.Next(DreamHouseLines.CheckedEmpty.Length));
            _checkIndex++;
            _act = Act.None;
        }

        int _herSpot = -1;

        readonly HashSet<string> _shut = new HashSet<string>();

        /// <summary>A room he won't search (his own room while it's the base: its door is shut).</summary>
        public void SetRoomOpen(string room, bool open)
        {
            if (open) _shut.Remove(room); else _shut.Add(room);
        }

        /// <summary>Which hiding spot she's in (the game tells him each tick; he can only use it by opening that spot).</summary>
        public void SetHerSpot(int spot) => _herSpot = spot;

        void LookRound(float dt)
        {
            _actTime += dt;
            float k = _actTime / Math.Max(0.1f, _actLen);
            Face(_lookCenter + _def.lookAroundAngle * (float)Math.Sin(k * Math.PI * 2.0), dt);
            if (_actTime >= _actLen) _act = Act.None;
        }

        /// <summary>Picks the next room: rooms he hasn't tried this sweep, nearest first, pulled toward the floor he saw
        /// her go to and a room he heard something in; a kid's favourites now and then.</summary>
        void NextRoom()
        {
            int start = _graph.Count > 0 ? NearestNode(_pos) : -1;
            int best = -1;
            float bestScore = float.MaxValue;
            for (int k = 0; k < _roomIds.Count; k++)
            {
                if (_roomDone[k] || _roomNode[k] < 0 || _shut.Contains(_roomIds[k])) continue;
                var node = _house.nodes[_roomNode[k]].p;
                float d = Vector3.Distance(node, _pos) + Math.Abs(node.Y - _pos.Y) * 4f;   // stairs cost more
                float score = d;
                var room = _house.Room(_roomIds[k]);
                int storey = room.outside ? 4 : room.storey;
                if (_storeyHint >= 0 && storey == _storeyHint) score -= 14f;
                if (_noiseRoom == _roomIds[k] && _noiseAge < 30f) score -= 20f;
                score += (float)_rng.NextDouble() * 6f;
                if (score < bestScore) { bestScore = score; best = k; }
            }
            if (best < 0)
            {
                // Everywhere tried: ask Mum (once a sweep), and start again.
                _sweeps++;
                for (int k = 0; k < _roomDone.Length; k++) _roomDone[k] = false;
                _storeyHint = -1;
                if (!_askedThisSweep) { _askedThisSweep = true; Say(TheoSay.AsksMum); }
                _room = -1;
                _act = Act.None;
                return;
            }
            _roomDone[best] = true;
            _room = best;
            _speed = LevelSpeed[_level];
            WalkTo(_house.nodes[_roomNode[best]].p, _speed);
            _checkPending = false;
        }

        // ---------- Walking ----------

        int NearestNode(Vector3 p)
        {
            int best = -1, any = -1;
            float bd = float.MaxValue, ad = float.MaxValue;
            for (int i = 0; i < _graph.Count; i++)
            {
                var n = _graph[i];
                float dy = Math.Abs(n.Y - p.Y);
                float d = (n.X - p.X) * (n.X - p.X) + (n.Z - p.Z) * (n.Z - p.Z) + dy * dy * 16f;
                if (d < ad) { ad = d; any = i; }
                if (dy > 0.9f || d >= bd) continue;
                if (_world != null && !WaypointGraph.WalkClear(_world, new Vector3(p.X, n.Y, p.Z), n)) continue;
                bd = d; best = i;
            }
            return best >= 0 ? best : any;
        }

        void WalkTo(Vector3 goal, float speed)
        {
            _goal = goal;
            _speed = speed;
            _act = Act.Walk;
            _path.Clear();
            _pathIndex = 0;
            _walkStraight = Math.Abs(goal.Y - _pos.Y) < 0.3f && DreamHouse.WalkClear(_world, _pos, goal);
            if (_walkStraight || _graph.Count == 0) return;
            int a = NearestNode(_pos), b = NearestNode(goal);
            if (a < 0 || b < 0 || !_graph.FindPath(a, b, _path)) { _path.Clear(); _walkStraight = true; return; }
            // Skip nodes he can already walk to straight (on his floor).
            for (int k = _path.Count - 1; k > 0; k--)
            {
                var n = _graph[_path[k]];
                if (Math.Abs(n.Y - _pos.Y) < 0.3f && DreamHouse.WalkClear(_world, _pos, n)) { _pathIndex = k; break; }
            }
        }

        void Step(float dt)
        {
            Vector3 next = _pathIndex < _path.Count ? _graph[_path[_pathIndex]] : _goal;
            var d = next - _pos;
            float len = d.Length();
            float step = _speed * dt;
            var flat = new Vector3(d.X, 0f, d.Z);
            if (flat.LengthSquared() > 1e-4f) Face(StealthYaw(flat), dt);
            if (len <= Math.Max(step, 0.05f))
            {
                _pos = next;
                if (_pathIndex < _path.Count)
                {
                    _pathIndex++;
                    if (_pathIndex >= _path.Count) return;
                    // Straight to the goal once he can.
                    if (Math.Abs(_goal.Y - _pos.Y) < 0.3f && DreamHouse.WalkClear(_world, _pos, _goal)) _pathIndex = _path.Count;
                }
                else
                {
                    _act = _checkPending ? Act.Check : Act.None;
                    _actTime = 0f;
                    if (_checkPending) _checkPending = false;
                }
                return;
            }
            _pos += d / len * step;
        }

        void Face(float yaw, float dt)
        {
            float delta = (yaw - _yaw) % 360f;
            if (delta > 180f) delta -= 360f;
            if (delta < -180f) delta += 360f;
            float max = _def.turnRate * dt;
            _yaw += Math.Abs(delta) <= max ? delta : Math.Sign(delta) * max;
        }

        float YawTo(Vector3 p) => StealthYaw(new Vector3(p.X - _pos.X, 0f, p.Z - _pos.Z));

        static float StealthYaw(Vector3 d) => (float)(Math.Atan2(d.X, d.Z) * 180.0 / Math.PI);
    }
}

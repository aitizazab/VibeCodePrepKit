using System;
using System.Collections.Generic;
using System.Numerics;
using static JamKit.Stealth.StealthMath;

namespace JamKit.Stealth
{
    /// <summary>
    /// One seeker's senses and decisions (docs/STEALTH.md), engine-free: sight (<see cref="StealthSight"/>) fills a
    /// <see cref="DetectionMeter"/>; hearing reacts to <see cref="StealthNoise"/>; the alert states run
    /// unaware → suspicious → searching → found and back to unaware; the search goes to the last known position, looks
    /// around, then checks the hiding spots near it (nearest first) and opens the one you're in. Two modes:
    /// <see cref="SeekerMode.Patrol"/> walks a loop of graph nodes and returns to it after a search;
    /// <see cref="SeekerMode.HideAndSeek"/> counts, then sweeps every spot and corner until it finds you.
    /// It walks on a flat floor (its start height) over a <see cref="WaypointGraph"/>, straight when the way is clear.
    /// The Unity side moves a body to <see cref="Position"/> / <see cref="Yaw"/> each frame. No allocation per tick.
    /// </summary>
    public sealed class SeekerBrain
    {
        enum Task { Idle, Patrol, Turn, GoTo, LookAround, CheckSpot, Chase, Lookout }

        const int LookoutStop = -2;

        struct Stop
        {
            public Vector3 point;
            public int spot; // -1: look around here; -2: the lookout
        }

        public readonly StealthDef def;
        readonly IStealthWorld _world;
        readonly WaypointGraph _graph;
        readonly List<HidingSpot> _spots;
        readonly List<int> _patrol = new List<int>();
        readonly DetectionMeter _meter = new DetectionMeter();
        readonly List<int> _path = new List<int>(32);
        readonly List<Stop> _stops = new List<Stop>(96);
        bool[] _spotDone;
        bool[] _nodeDone;

        SeekerMode _mode = SeekerMode.Patrol;
        AlertState _state = AlertState.Unaware;
        Vector3 _pos;
        float _yaw, _floorY;
        float _stateTime, _sinceSeen = 999f;
        float _visibility;
        SightZone _zone;
        bool _counting;
        float _countLeft, _countYaw;
        Vector3 _lastKnown, _stimulus;
        bool _hasLastKnown;
        SeekerEvents _events = SeekerEvents.None;

        // Walking and the current task
        Task _task = Task.Idle;
        float _taskTime, _speed;
        Vector3 _goal;
        int _pathIndex;
        bool _arrived = true;
        float _lookCenter;
        int _patrolIndex, _stopIndex, _checking = -1;
        float _searchLeft, _replan;
        int _sweeps;
        int _targetSpot = -1;
        float _bounceT;

        // A lookout (a trampoline): counted on, and visited during hide-and-seek sweeps; its eye rises with each bounce.
        bool _hasLookout;
        Vector3 _lookout;
        float _lookoutBase, _lookoutBounce, _lookoutPeriod, _lookoutTime;
        int _lookoutEvery;
        float _eyeLift;

        // Wariness, escalation and calls (StealthRules; docs/STEALTH.md "When the movement outruns the guards").
        float _waryLeft;
        readonly float[] _noiseTimes = new float[8];
        int _noiseHead;
        float _clock;
        bool _suppressCall;
        Vector3 _callSpot;

        /// <summary>The answers in play (null: none, the original kit). The Unity seeker sets <see cref="StealthRules.Shared"/>.</summary>
        public StealthRules rules;

        public SeekerBrain(StealthDef def, IStealthWorld world, WaypointGraph graph, List<HidingSpot> spots, Vector3 feet, float yaw)
        {
            this.def = def ?? new StealthDef();
            _world = world;
            _graph = graph ?? new WaypointGraph();
            _spots = spots ?? new List<HidingSpot>();
            _spotDone = new bool[_spots.Count];
            Place(feet, yaw);
        }

        public SeekerMode Mode => _mode;
        public AlertState State => _state;
        public Vector3 Position => _pos;
        public float Yaw => _yaw;
        public Vector3 Eye => _pos + new Vector3(0f, def.eyeHeight + _eyeLift, 0f);
        /// <summary>Wary: it heard something (or was called) and for <see cref="StealthDef.waryTime"/> s looks wider, turns
        /// quicker and fills its meter faster, even back on its patrol.</summary>
        public bool Wary => _waryLeft > 0f;
        public float WaryLeft => Math.Max(0f, _waryLeft);
        /// <summary>Where its last call pointed (answer B).</summary>
        public Vector3 CallSpot => _callSpot;
        float TurnRate => def.turnRate * (Wary ? def.waryTurnScale : 1f);
        /// <summary>Wariness and escalation are part of answers A and B (off: the original kit).</summary>
        bool WaryOn => rules != null && (rules.loudMovement || rules.dreamPushesBack);

        /// <summary>Another guard's call (answer B): turn wary and, if calm, come to <paramref name="spot"/>. True if it reacted.</summary>
        public bool HearCall(Vector3 spot)
        {
            if (_counting || _state == AlertState.Found) return false;
            _waryLeft = def.waryTime;
            if (_state != AlertState.Unaware) return false;
            _suppressCall = true;
            BecomeSuspicious(spot);
            _suppressCall = false;
            return true;
        }

        /// <summary>How far the seeker stands above its floor right now (on the lookout, bouncing); the body follows it.</summary>
        public float EyeLift => _eyeLift;
        /// <summary>On the lookout (bouncing, looking over things).</summary>
        public bool OnLookout => _task == Task.Lookout;
        /// <summary>The detection meter, 0..1 (full: found).</summary>
        public float Meter => _meter.Value;
        /// <summary>Last tick's visibility of the target (0: unseen).</summary>
        public float Visibility => _visibility;
        public SightZone Zone => _zone;
        public bool Counting => _counting;
        public float CountLeft => _counting ? Math.Max(0f, _countLeft) : 0f;
        public bool HasLastKnown => _hasLastKnown;
        public Vector3 LastKnown => _lastKnown;
        /// <summary>The hiding spot being checked right now, -1 for none.</summary>
        public int CheckingSpot => _task == Task.CheckSpot ? _checking : -1;
        public SeekerEvents Events => _events;
        public float TimeInState => _stateTime;
        public bool Moving => _task != Task.Idle && !_arrived;
        public int Sweeps => _sweeps;
        public IReadOnlyList<HidingSpot> Spots => _spots;
        public WaypointGraph Graph => _graph;

        /// <summary>The loop a patrol walks (graph node indices).</summary>
        public void SetPatrol(IList<int> nodes)
        {
            _patrol.Clear();
            if (nodes != null) for (int i = 0; i < nodes.Count; i++) _patrol.Add(nodes[i]);
            _patrolIndex = 0;
        }

        /// <summary>Puts the seeker somewhere, unaware, meter empty, patrolling (or idle with no patrol).</summary>
        public void Place(Vector3 feet, float yaw)
        {
            _waryLeft = 0f;
            for (int i = 0; i < _noiseTimes.Length; i++) _noiseTimes[i] = -999f;
            _pos = feet;
            _floorY = feet.Y;
            _yaw = yaw;
            _mode = SeekerMode.Patrol;
            _counting = false;
            _meter.Reset();
            _hasLastKnown = false;
            _sinceSeen = 999f;
            _checking = -1;
            SetState(AlertState.Unaware);
            _events = SeekerEvents.None;
            _task = Task.Idle;
            _arrived = true;
            _patrolIndex = NearestPatrolIndex();
        }

        /// <summary>Hide-and-seek: counts (eyes closed, facing <paramref name="countYaw"/>) for <see cref="StealthDef.countTime"/>
        /// seconds, then seeks until it finds you.</summary>
        public void StartHideAndSeek(Vector3 feet, float countYaw, float countSeconds = -1f)
        {
            Place(feet, countYaw);
            _mode = SeekerMode.HideAndSeek;
            _counting = true;
            _countYaw = countYaw;
            _countLeft = countSeconds >= 0f ? countSeconds : def.countTime;
            _sweeps = 0;
        }

        /// <summary>A lookout for hide-and-seek (docs/DENSE-STREET.md 9, rule 4: Theo's trampoline): a point on the floor
        /// with a platform <paramref name="platform"/> m high that it bounces on, its eye rising up to
        /// <paramref name="bounce"/> m more at the top of each bounce (every <paramref name="period"/> s), so it sees over
        /// fences only then. It counts there if it starts there, and its sweeps come back to it every
        /// <paramref name="everyStops"/> stops for <paramref name="seconds"/> s.</summary>
        public void SetLookout(Vector3 point, float platform, float bounce, float period, int everyStops, float seconds)
        {
            _hasLookout = true;
            _lookout = point;
            _lookoutBase = Math.Max(0f, platform);
            _lookoutBounce = Math.Max(0f, bounce);
            _lookoutPeriod = Math.Max(0.2f, period);
            _lookoutEvery = Math.Max(1, everyStops);
            _lookoutTime = Math.Max(0.5f, seconds);
        }

        public void ClearLookout() { _hasLookout = false; _eyeLift = 0f; }

        bool AtLookout => _hasLookout && FlatDistance(_pos, _lookout) < 0.6f;

        /// <summary>The eye's lift <paramref name="t"/> s into bouncing: the platform plus |sin| of the bounce.</summary>
        float Bounce(float t) => _lookoutBase + _lookoutBounce * (float)Math.Abs(Math.Sin(t * Math.PI / _lookoutPeriod));

        /// <summary>A scripted alarm: suspicious of a place (a thrown can, a door slam).</summary>
        public void Alert(Vector3 at)
        {
            if (_counting || _state == AlertState.Found) return;
            _meter.Bump(def.suspiciousAt);
            BecomeSuspicious(at);
        }

        // ---------- The tick ----------

        public void Tick(float dt, in StealthTarget target, in StealthNoise noise)
        {
            _events = SeekerEvents.None;
            if (!(dt > 0f)) return;
            _stateTime += dt;
            _targetSpot = target.present ? target.hidingSpot : -1;
            _clock += dt;
            if (_waryLeft > 0f) _waryLeft -= dt;
            _bounceT += dt;
            _eyeLift = _task == Task.Lookout || (_counting && AtLookout) ? Bounce(_bounceT) : (AtLookout && _hasLookout ? _lookoutBase : 0f);

            // Senses.
            HideKind? hide = null;
            if (target.present && target.hidingSpot >= 0 && target.hidingSpot < _spots.Count) hide = _spots[target.hidingSpot].kind;
            float notice = rules != null && target.present ? rules.NoticeScale(def, target.speed) : 1f;
            _visibility = _counting ? 0f : StealthSight.Visibility(def, _world, Eye, _yaw, target, hide, out _zone, Wary ? def.waryConeScale : 1f, notice);
            if (_counting) _zone = SightZone.None;
            float rate = _visibility * def.fillRate * notice * (Wary ? def.waryFillScale : 1f)
                * (_state == AlertState.Suspicious || _state == AlertState.Searching ? def.alertFillScale : 1f);
            _meter.Tick(def, rate, dt);
            if (_visibility > 0f)
            {
                _lastKnown = new Vector3(target.feet.X, _floorY, target.feet.Z);
                _hasLastKnown = true;
                _sinceSeen = 0f;
            }
            else _sinceSeen += dt;
            bool heard = !_counting && _state != AlertState.Found && Hears(noise);
            if (heard) _events.heard = true;
            if (heard && WaryOn)
            {
                if (!Wary) _events.becameWary = true;
                _waryLeft = def.waryTime;
                _noiseTimes[_noiseHead] = _clock;
                _noiseHead = (_noiseHead + 1) % _noiseTimes.Length;
                if (HeardRecently() >= Math.Max(1, def.escalateNoises) && _state != AlertState.Searching)
                {
                    // Repeated noise: alert. Search the spot at once, the meter part full.
                    _events.escalated = true;
                    _meter.Bump(def.escalateMeter);
                    for (int i = 0; i < _noiseTimes.Length; i++) _noiseTimes[i] = -999f;
                    BeginSearch(Flat(noise.position), _mode == SeekerMode.HideAndSeek);
                    heard = false; // handled
                }
            }

            if (_counting)
            {
                _countLeft -= dt;
                _yaw = MoveTowardsAngle(_yaw, _countYaw, TurnRate * dt);
                if (_countLeft <= 0f)
                {
                    _counting = false;
                    _events.countingDone = true;
                    BeginSearch(_pos, true);
                }
                return;
            }

            if (_state != AlertState.Found && _meter.Full)
            {
                BecomeFound(-1);
            }

            switch (_state)
            {
                case AlertState.Unaware:
                    if (_visibility > 0f && _meter.Value >= def.suspiciousAt) BecomeSuspicious(_lastKnown);
                    else if (heard) { _meter.Bump(def.noiseBump); BecomeSuspicious(Flat(noise.position)); }
                    else if (_mode == SeekerMode.HideAndSeek) BeginSearch(_pos, true);
                    else PatrolTick(dt);
                    break;

                case AlertState.Suspicious:
                    if (_visibility > 0f) _stimulus = _lastKnown;
                    if (heard) _stimulus = Flat(noise.position);
                    if (_stateTime < def.reactTime)
                    {
                        Face(YawTo(_stimulus), dt);
                    }
                    else
                    {
                        if (_task != Task.GoTo || Vector3.DistanceSquared(_goal, _stimulus) > 1f) GoTo(_stimulus, def.investigateSpeed);
                        Walk(dt);
                        if (_arrived) BeginSearch(_stimulus, _mode == SeekerMode.HideAndSeek);
                    }
                    break;

                case AlertState.Searching:
                    if (heard) BeginSearch(Flat(noise.position), _mode == SeekerMode.HideAndSeek);
                    else if (_visibility > 0f && _task != Task.Chase)
                    {
                        // A glimpse while searching: go to it (the meter fills fast meanwhile).
                        _task = Task.Chase;
                        _taskTime = 0f;
                    }
                    SearchTick(dt);
                    if (_mode == SeekerMode.Patrol)
                    {
                        _searchLeft -= dt;
                        if (_searchLeft <= 0f) BecomeUnaware();
                    }
                    break;

                case AlertState.Found:
                    if (_mode == SeekerMode.HideAndSeek)
                    {
                        // The round is over: face the one found.
                        if (target.present) Face(YawTo(target.feet), dt);
                        break;
                    }
                    if (_sinceSeen < def.loseSightTime)
                    {
                        ChaseTick(_lastKnown, def.chaseSpeed, dt);
                    }
                    else
                    {
                        _meter.Set(Math.Min(_meter.Value, 0.7f));
                        BeginSearch(_lastKnown, false);
                    }
                    break;
            }
        }

        bool Hears(in StealthNoise noise)
        {
            if (!noise.Any) return false;
            float r = noise.radius * def.hearingScale;
            float d = Vector3.Distance(Eye, noise.position);
            if (d > r) return false;
            if (_world != null && !_world.LineClear(Eye, noise.position + new Vector3(0f, 0.3f, 0f))) r *= def.occludedHearing;
            return d <= r;
        }

        // ---------- States ----------

        void SetState(AlertState s)
        {
            if (s == _state) { _stateTime = 0f; return; }
            _events.stateChanged = true;
            _events.from = _state;
            _events.to = s;
            // A guard who grows more alert calls out (answer B; SeekerSquad passes it on).
            if (!_suppressCall && (int)s > (int)_state && s != AlertState.Unaware)
            {
                _events.called = true;
                _callSpot = _hasLastKnown ? _lastKnown : _pos;
            }
            _state = s;
            _stateTime = 0f;
        }

        void BecomeSuspicious(Vector3 at)
        {
            _stimulus = Flat(at);
            SetState(AlertState.Suspicious);
            _task = Task.Turn;
            _arrived = true;
        }

        void BecomeFound(int spot)
        {
            _meter.Set(1f);
            SetState(AlertState.Found);
            _events.found = true;
            _events.foundInSpot = spot;
            _task = Task.Chase;
            _arrived = true;
        }

        void BecomeUnaware()
        {
            SetState(AlertState.Unaware);
            _task = Task.Idle;
            _arrived = true;
            _patrolIndex = NearestPatrolIndex();
        }

        // ---------- Patrol ----------

        int NearestPatrolIndex()
        {
            int best = 0;
            float bd = float.MaxValue;
            for (int i = 0; i < _patrol.Count; i++)
            {
                float d = Vector3.DistanceSquared(_graph[_patrol[i]], _pos);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        void PatrolTick(float dt)
        {
            if (_patrol.Count == 0) return;
            if (_task != Task.Patrol)
            {
                _task = Task.Patrol;
                GoToKeepTask(_graph[_patrol[_patrolIndex]], def.patrolSpeed);
            }
            if (!_arrived) { Walk(dt); return; }
            _taskTime += dt;
            if (_taskTime >= def.patrolPause)
            {
                _patrolIndex = (_patrolIndex + 1) % _patrol.Count;
                GoToKeepTask(_graph[_patrol[_patrolIndex]], def.patrolSpeed);
            }
        }

        // ---------- Search ----------

        /// <summary>Plans a search around <paramref name="center"/>: look around there, check the hiding spots within
        /// <see cref="StealthDef.spotSearchRadius"/> nearest first, then (patrol) a few nearby nodes, or (hide-and-seek,
        /// <paramref name="sweep"/>) every other spot and the patrol points too.</summary>
        void BeginSearch(Vector3 center, bool sweep)
        {
            center = Flat(center);
            SetState(AlertState.Searching);
            _searchLeft = def.searchTime;
            _stops.Clear();
            if (_spotDone.Length != _spots.Count) _spotDone = new bool[_spots.Count]; // spots added since (a scene loaded)
            if (sweep) { for (int i = 0; i < _spotDone.Length; i++) _spotDone[i] = false; _sweeps++; }
            _stops.Add(new Stop { point = center, spot = -1 });
            // Spots near the centre, nearest first (insertion into the list; a handful of spots).
            int first = _stops.Count;
            for (int i = 0; i < _spots.Count; i++)
            {
                if (_spotDone[i] && sweep) continue;
                float d = FlatDistance(_spots[i].Center, center);
                if (d > def.spotSearchRadius) continue;
                InsertByDistance(first, new Stop { point = Flat(_spots[i].Approach), spot = i }, center);
            }
            if (sweep)
            {
                // Then every other hiding spot and every walking point (to look around: corners, the alley), as a
                // greedy nearest-next tour from the last stop.
                if (_nodeDone == null || _nodeDone.Length != _graph.Count) _nodeDone = new bool[_graph.Count];
                for (int i = 0; i < _nodeDone.Length; i++) _nodeDone[i] = FlatDistance(_graph[i], center) < 2f;
                Vector3 from = _stops[_stops.Count - 1].point;
                while (true)
                {
                    int spot = -1, node = -1;
                    float bd = float.MaxValue;
                    for (int i = 0; i < _spots.Count; i++)
                    {
                        if (_spotDone[i] || ContainsSpot(i)) continue;
                        float d = FlatDistance(_spots[i].Approach, from);
                        if (d < bd) { bd = d; spot = i; }
                    }
                    for (int i = 0; i < _nodeDone.Length; i++)
                    {
                        if (_nodeDone[i]) continue;
                        float d = FlatDistance(_graph[i], from);
                        if (d < bd) { bd = d; node = i; spot = -1; }
                    }
                    if (node >= 0)
                    {
                        _nodeDone[node] = true;
                        _stops.Add(new Stop { point = Flat(_graph[node]), spot = -1 });
                        from = _graph[node];
                    }
                    else if (spot >= 0)
                    {
                        _stops.Add(new Stop { point = Flat(_spots[spot].Approach), spot = spot });
                        from = _spots[spot].Approach;
                    }
                    else break;
                }
            }
            else
            {
                // A patrol also walks to the two nearest graph points around the centre and looks there.
                int a = -1, b = -1;
                float da = float.MaxValue, db = float.MaxValue;
                for (int i = 0; i < _graph.Count; i++)
                {
                    float d = FlatDistance(_graph[i], center);
                    if (d < 2f) continue;
                    if (d < da) { db = da; b = a; da = d; a = i; }
                    else if (d < db) { db = d; b = i; }
                }
                if (a >= 0 && da < def.spotSearchRadius * 1.5f) _stops.Add(new Stop { point = Flat(_graph[a]), spot = -1 });
                if (b >= 0 && db < def.spotSearchRadius * 1.5f) _stops.Add(new Stop { point = Flat(_graph[b]), spot = -1 });
            }
            if (sweep && _hasLookout)
            {
                // Back to the lookout first and every few stops: bounce and look over everything.
                var look = new Stop { point = Flat(_lookout), spot = LookoutStop };
                for (int i = _stops.Count; i > 0; i--)
                    if (i % _lookoutEvery == 0 && i < _stops.Count) _stops.Insert(i, look);
                _stops.Insert(0, look);
            }
            _stopIndex = 0;
            StartStop();
        }

        bool ContainsSpot(int spot)
        {
            for (int i = 0; i < _stops.Count; i++) if (_stops[i].spot == spot) return true;
            return false;
        }

        void InsertByDistance(int first, Stop s, Vector3 center)
        {
            float d = FlatDistance(_spots[s.spot].Center, center);
            int at = _stops.Count;
            for (int i = first; i < _stops.Count; i++)
                if (FlatDistance(_spots[_stops[i].spot].Center, center) > d) { at = i; break; }
            _stops.Insert(at, s);
        }

        void StartStop()
        {
            if (_stopIndex >= _stops.Count)
            {
                if (_mode == SeekerMode.HideAndSeek) BeginSearch(_pos, true);
                else BecomeUnaware();
                return;
            }
            GoTo(_stops[_stopIndex].point, def.searchSpeed);
        }

        void SearchTick(float dt)
        {
            if (_task == Task.Chase)
            {
                // Going to where the target was glimpsed; there, look around and search near it.
                ChaseTick(_lastKnown, def.searchSpeed, dt);
                if (_arrived && _sinceSeen > 0.3f)
                {
                    _stops.Insert(Math.Min(_stopIndex, _stops.Count), new Stop { point = _lastKnown, spot = -1 });
                    StartStop();
                }
                return;
            }
            if (_stopIndex >= _stops.Count) { StartStop(); return; }
            var stop = _stops[_stopIndex];
            switch (_task)
            {
                case Task.GoTo:
                    Walk(dt);
                    if (!_arrived) return;
                    _taskTime = 0f;
                    if (stop.spot == LookoutStop)
                    {
                        _task = Task.Lookout;
                        _lookCenter = _yaw;
                        _bounceT = 0f;
                    }
                    else if (stop.spot >= 0)
                    {
                        _task = Task.CheckSpot;
                        _checking = stop.spot;
                    }
                    else
                    {
                        _task = Task.LookAround;
                        _lookCenter = _yaw;
                    }
                    return;
                case Task.LookAround:
                    _taskTime += dt;
                    float k = _taskTime / Math.Max(0.1f, def.lookAroundTime);
                    _yaw = MoveTowardsAngle(_yaw, _lookCenter + def.lookAroundAngle * (float)Math.Sin(k * Math.PI * 2.0), TurnRate * dt);
                    if (_taskTime >= def.lookAroundTime) { _stopIndex++; StartStop(); }
                    return;
                case Task.Lookout:
                    // Bouncing: a slow full turn, looking over the fences at the top of each bounce.
                    _taskTime += dt;
                    _yaw += 360f / _lookoutTime * dt;
                    if (_taskTime >= _lookoutTime) { _stopIndex++; StartStop(); }
                    return;
                case Task.CheckSpot:
                    _taskTime += dt;
                    Face(YawTo(_spots[_checking].Center), dt);
                    if (_taskTime >= def.checkSpotTime)
                    {
                        _events.checkedSpot = _checking;
                        _spotDone[_checking] = true;
                        if (_targetSpot == _checking) { BecomeFound(_checking); return; }
                        _checking = -1;
                        _stopIndex++;
                        StartStop();
                    }
                    return;
                default:
                    StartStop();
                    return;
            }
        }

        // ---------- Walking ----------

        void ChaseTick(Vector3 to, float speed, float dt)
        {
            float d = FlatDistance(_pos, to);
            _replan -= dt;
            if (d <= def.catchRange)
            {
                _arrived = true;
                Face(YawTo(to), dt);
                return;
            }
            if (_replan <= 0f || _arrived)
            {
                _replan = 0.25f;
                GoToKeepTask(to, speed);
            }
            Walk(dt);
        }

        void GoTo(Vector3 point, float speed)
        {
            _task = Task.GoTo;
            GoToKeepTask(point, speed);
        }

        void GoToKeepTask(Vector3 point, float speed)
        {
            _goal = Flat(point);
            _speed = speed;
            _taskTime = 0f;
            _arrived = FlatDistance(_pos, _goal) <= def.arriveRadius;
            _path.Clear();
            _pathIndex = 0;
            if (_arrived || WaypointGraph.WalkClear(_world, _pos, _goal) || _graph.Count == 0) return;
            int a = _graph.NearestReachable(_world, _pos), b = _graph.NearestReachable(_world, _goal);
            if (!_graph.FindPath(a, b, _path)) _path.Clear();
            SkipAhead();
        }

        /// <summary>Heads for the farthest point along the path it can already walk to straight (no doubling back to the
        /// nearest node when a plan is made mid-walk).</summary>
        void SkipAhead()
        {
            for (int k = _path.Count - 1; k > _pathIndex; k--)
                if (WaypointGraph.WalkClear(_world, _pos, Flat(_graph[_path[k]]))) { _pathIndex = k; return; }
        }

        void Walk(float dt)
        {
            if (_arrived) return;
            Vector3 next = _pathIndex < _path.Count ? Flat(_graph[_path[_pathIndex]]) : _goal;
            Vector3 d = next - _pos;
            d.Y = 0f;
            float len = d.Length();
            float step = _speed * dt;
            if (len > 1e-4f) Face(YawOf(d), dt);
            if (len <= Math.Max(step, def.arriveRadius))
            {
                _pos = next;
                if (_pathIndex < _path.Count)
                {
                    _pathIndex++;
                    // Straight to the goal once the way is clear, else to the farthest node in sight.
                    if (WaypointGraph.WalkClear(_world, _pos, _goal)) _pathIndex = _path.Count;
                    else SkipAhead();
                }
                else _arrived = true;
                return;
            }
            _pos += d / len * step;
        }

        void Face(float yaw, float dt) => _yaw = MoveTowardsAngle(_yaw, yaw, TurnRate * dt);

        int HeardRecently()
        {
            int n = 0;
            for (int i = 0; i < _noiseTimes.Length; i++)
                if (_clock - _noiseTimes[i] <= def.escalateWindow) n++;
            return n;
        }

        float YawTo(Vector3 p)
        {
            var d = new Vector3(p.X - _pos.X, 0f, p.Z - _pos.Z);
            return d.LengthSquared() < 1e-6f ? _yaw : YawOf(d);
        }

        Vector3 Flat(Vector3 p) => new Vector3(p.X, _floorY, p.Z);
    }
}

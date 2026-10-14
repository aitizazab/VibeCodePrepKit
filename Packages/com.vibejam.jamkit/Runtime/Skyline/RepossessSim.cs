using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Movement;

namespace JamKit.Skyline
{
    /// <summary>Drives the Unit in a <see cref="RepossessSim"/>: like a player's hands, it may read the whole floor (the
    /// objective, the clock, the bots, the telegraphs). Movement and the optional hold-fire/reload are returned; aiming is the
    /// fight's (the Writ shoots the nearest awake bot it can see).</summary>
    public interface IFloorBot
    {
        FightInput Next(RepossessSim sim, float h);
    }

    /// <summary>The knobs of a headless floor run (docs/SKYLINE-PLAN-FLOOR1.md Task W3). All optional.</summary>
    public sealed class FloorOptions
    {
        /// <summary>True: a wreck ends the run (after the floor has been reset as a wreck resets it). False: the run goes on from
        /// the restart, as the game does (the lobby and the lift ride are skipped: the Unit stands in the arrival alcove at the drop).</summary>
        public bool stopOnWreck = true;
        /// <summary>The Unit falling below this y (out through a broken façade pane) is wrecked.</summary>
        public float fallDeathY = -12f;
        /// <summary>Hits are counted but do no damage (the band measurements).</summary>
        public bool invulnerable;
        public FightPlayerDef player;
        public DifficultyDef difficulty;
        public GunDef gun;
        /// <summary>Where the Unit stands at the drop (default <see cref="RepossessArena.ArrivalPoint"/>) and which way it faces.</summary>
        public Vector3? start;
        public float startYaw;
        /// <summary>The movement world stops or starts colliding with a solid: called with (solid id, enabled) when a shot breaks a
        /// façade pane (false), when the floor restarts and mends them (true), and for the west service lift's shutter
        /// (<see cref="RepossessArena.LiftShutterId"/>: false when the third asset is towed, true on a restart). The tests map it
        /// onto <c>BoxWorld.Box.enabled</c> (<c>FloorWorld.Build(boxes)</c>).</summary>
        public Action<string, bool> solidEnabled;
        /// <summary>The wave clock's cap and interval (default: the floor's numbers).</summary>
        public int waveCap = FloorRules.WaveCap;
        public float waveSeconds = FloorRules.WaveSeconds;
        /// <summary>True (the game): door-spawned drones and floor-care units come to their working point through a <see cref="BotArrival"/>
        /// and neither see nor attack on the way. False: they start at their door and work from there (the W3 stand-in).</summary>
        public bool arrivals = true;
    }

    /// <summary>What a floor run did (see <see cref="RepossessSim.Result"/>). Counters carry across restarts after a wreck.</summary>
    public struct FloorResult
    {
        /// <summary>Assets towed (since the last restart), and when each was (sim seconds, in the order of the job sheet; -1 = not towed).</summary>
        public int towed;
        public float[] towSeconds;
        /// <summary>The Unit was wrecked at least once / reached the west service lift.</summary>
        public bool wrecked, reachedExit;
        /// <summary>Waves handed to the spawner, and intervals skipped because a wave still waited for room (D9), over the run.</summary>
        public int wavesReleased, wavesSkipped;
        /// <summary>Hits the Unit took (bolts, beams, acid, mine blasts) and the lowest plating seen.</summary>
        public int hits;
        public float minPlating;
        public float secondsRun;
        /// <summary>The first wave number at whose time two bots 90 degrees or more apart (seen from the Unit) both had the Unit in
        /// sight, or -1; and when.</summary>
        public float firstFlankedWave, firstFlankedSeconds;
        /// <summary>When the third asset was towed, or -1.</summary>
        public float timeToThird;
        // ---- beyond the plan's list
        public int wrecks;
        /// <summary>Living bots (and the wave number last released) when the Unit was last wrecked, or -1.</summary>
        public int aliveAtWreck, waveAtWreck;
        /// <summary>The most bots alive together, and the number of waves that could not find a legal spawn for over 5 s (X0 logs these).</summary>
        public int peakAlive, wavesStuck;
        public int panesBroken, alarms;
        public int botsSpawned;
    }

    /// <summary>Bots alive at a moment of the run (<see cref="RepossessSim.AliveHistory"/>, one sample a second).</summary>
    public struct AliveSample
    {
        public float t;
        public int alive, wave;
    }

    /// <summary>
    /// The headless floor (docs/SKYLINE-PLAN-FLOOR1.md Task W3): <see cref="FightSim"/> generalised to the Food Court. The real
    /// <c>MoveSim</c> walks the arena's data (the caller passes the <see cref="IMoveWorld"/>: the tests pass
    /// <c>FloorWorld.Build()</c>), the wave clock (W1) releases a wave every 30 s, <see cref="SpawnBearings"/> (W2) picks two doors
    /// at least 90 degrees apart, the <see cref="WaveSpawner"/> telegraphs them for 2 s, and the bots come through as awake
    /// <see cref="FightEnemy"/>s (<see cref="FightSim.AddEnemy"/>); the tow meters (R1) fill while the Unit stands in a circle; the
    /// third tow stops the clock and opens the west lift; the Unit's feet in <see cref="RepossessArena.ExitLiftZone"/> end the floor.
    /// A wreck resets the floor as the game does (clock, meters, bots, telegraphs, panes; the same sockets).
    /// <para>Stand-ins, stated so nobody mistakes them for the game:</para>
    /// <list type="bullet">
    /// <item>The lobby and the lift ride are skipped: the run starts at the drop, the Unit in the arrival alcove.</item>
    /// <item>A drone or floor-care unit leaves its door through a <see cref="BotArrival"/> (W4; <see cref="FloorOptions.arrivals"/>) and
    /// neither sees nor attacks until it has arrived: a drone flies a swept route to a hover point 12..28 m from the Unit, a floor-care
    /// unit paths on <see cref="ArenaNav"/> (ramps and escalators included) until it is within 4 m on the Unit's level, then the fight's
    /// own brain takes over. That brain drives in 2D round the floor-level cover, so a unit that has arrived can stall at a concave
    /// corner, and it cannot hurt a Unit more than 1.5 m below its floor.</item>
    /// <item>The gondola rides outside the glass and never shoots: it lays its mines along the inside face of the façade
    /// (<see cref="FightArea.mineRailOffset"/>). A Writ shot whose line passes a façade pane breaks it (the pane is gone from the
    /// wall-run); glass balustrades stop shots but not sight.</item>
    /// </list>
    /// </summary>
    public sealed class RepossessSim
    {
        /// <summary>How far in front of its door a bot appears (m), so it never starts inside the shop block or the hatch frame.</summary>
        public const float DoorInset = 0.8f;
        /// <summary>The height a drone leaves a ground-level shutter at (m).</summary>
        public const float DroneDoorHeight = 2.2f;
        /// <summary>A wave that finds no legal spawn for this long is counted in <see cref="FloorResult.wavesStuck"/>.</summary>
        public const float StuckSeconds = 5f;

        readonly IFloorBot _bot;
        readonly FloorOptions _o;
        readonly ulong _seed;
        readonly Vector3 _start;
        readonly List<CoverBox> _balustrades = new List<CoverBox>();
        readonly List<WaveDef> _released = new List<WaveDef>();
        readonly Queue<WaveDef> _queue = new Queue<WaveDef>();
        readonly List<SpawnAssignment> _due = new List<SpawnAssignment>(), _started = new List<SpawnAssignment>();
        readonly Dictionary<SpawnAssignment, int> _groupOf = new Dictionary<SpawnAssignment, int>();
        readonly SpawnDoor[] _doors = RepossessSites.Doors();
        readonly List<AliveSample> _history = new List<AliveSample>();
        readonly Dictionary<FightEnemy, BotArrival> _arrivals = new Dictionary<FightEnemy, BotArrival>();
        readonly List<FightEnemy> _arrivalList = new List<FightEnemy>();
        static ArenaNav _nav;
        static readonly object _navLock = new object();
        float? _lastMean;
        Vector3? _alarm;
        float _stuckFor;
        bool _stuckCounted;
        int _lastWave, _spawnIndex;
        int _releasedBase, _skippedBase;
        float _nextSample;
        bool _finished;
        FloorResult _r;

        /// <param name="bot">Drives the Unit.</param>
        /// <param name="seed">Everything random (the sockets, the door choices, the enemies' aim, the hit rolls).</param>
        /// <param name="world">The movement world over <see cref="RepossessArena.Solids"/> (the tests: <c>FloorWorld.Build()</c>).</param>
        public RepossessSim(IFloorBot bot, ulong seed, IMoveWorld world, FloorOptions options = null)
        {
            _bot = bot ?? throw new ArgumentNullException(nameof(bot));
            if (world == null) throw new ArgumentNullException(nameof(world));
            _o = options ?? new FloorOptions();
            _seed = seed;
            _start = _o.start ?? RepossessArena.ArrivalPoint;
            Cover = RepossessArena.SightBlockers();
            var panes = new List<BreakablePane>();
            foreach (var s in RepossessArena.Solids())
            {
                if (RepossessArena.IsPane(s.id)) panes.Add(new BreakablePane { id = s.id, box = s.box });
                else if (s.glass && s.role == "balustrade") _balustrades.Add(s.box);
            }
            Fight = new FightSim(new EncounterDef { id = "floor1", spawns = new Spawn[0] }, world, new Adapter(this), Cover, _start, seed,
                _o.difficulty, null, _o.player, _o.gun, _o.startYaw, FightArea.Floor1);
            Fight.EndsOnClear = false;
            Fight.Invulnerable = _o.invulnerable;
            Fight.SetShotBlockers(_balustrades);
            Fight.SetPanes(panes);
            Fight.PaneBroken += p =>
            {
                _r.panesBroken++;
                _o.solidEnabled?.Invoke(p.id, false);
            };

            Clock = new WaveClock(_o.waveCap, _o.waveSeconds);
            Objective = new Repossession(RepossessSites.Assets(), seed);
            Flow = new FloorFlow();
            Spawner = new WaveSpawner();

            Flow.Entered += phase =>
            {
                if (phase == FloorPhase.Floor) Clock.Start();
                else if (phase == FloorPhase.Finale)
                {
                    Clock.Stop();
                    _queue.Clear();
                    _o.solidEnabled?.Invoke(RepossessArena.LiftShutterId, false);
                }
            };
            Objective.MeterStarted += j =>
            {
                _alarm = j.socket.position;
                _r.alarms++;
                AlarmRaised?.Invoke(j);
            };
            Objective.Towed += j =>
            {
                _r.towSeconds[IndexOf(j)] = Fight.Time;
                _r.towed = Objective.TowedCount;
                AssetTowed?.Invoke(j);
            };
            Objective.AllTowedEvent += () =>
            {
                _r.timeToThird = Fight.Time;
                Flow.AllTowed();
            };

            _r = NewResult();
            Flow.SteppedIntoLift();
            Flow.DoorsOpened();
        }

        sealed class Adapter : IFightBot
        {
            readonly RepossessSim _sim;
            public Adapter(RepossessSim sim) { _sim = sim; }
            public FightInput Next(FightSim fight, float h) => _sim._bot.Next(_sim, h);
        }

        FloorResult NewResult()
        {
            var ts = new float[Objective.Sheet.Count];
            for (int i = 0; i < ts.Length; i++) ts[i] = -1f;
            return new FloorResult
            {
                towSeconds = ts, firstFlankedWave = -1f, firstFlankedSeconds = -1f, timeToThird = -1f, aliveAtWreck = -1, waveAtWreck = -1,
                minPlating = float.MaxValue,
            };
        }

        int IndexOf(JobEntry j)
        {
            for (int i = 0; i < Objective.Sheet.Count; i++) if (Objective.Sheet[i] == j) return i;
            return 0;
        }

        // ---- what a driver may read

        public FightSim Fight { get; }
        public WaveClock Clock { get; }
        public Repossession Objective { get; }
        public FloorFlow Flow { get; }
        /// <summary>The 2 s door telegraphs (W2).</summary>
        public WaveSpawner Spawner { get; }
        /// <summary>The sight blockers (<see cref="RepossessArena.SightBlockers"/>).</summary>
        public IReadOnlyList<CoverBox> Cover { get; }
        public bool Finished => _finished;
        /// <summary>The number of the last wave handed to the spawner (since the last restart), 0 before wave 1.</summary>
        public int LastWave => _lastWave;
        /// <summary>The waves released but not yet through a door (waiting for a legal choice).</summary>
        public int QueuedWaves => _queue.Count;
        /// <summary>The alarm is up for the next wave (an asset's meter started and no wave has been chosen since).</summary>
        public Vector3? PendingAlarm => _alarm;
        /// <summary>One sample a second of the bots alive.</summary>
        public IReadOnlyList<AliveSample> AliveHistory => _history;
        public float Time => Fight.Time;

        /// <summary>The floor-care units' walkable graph (built once from <see cref="RepossessArena.Solids"/>).</summary>
        public static ArenaNav Nav
        {
            get
            {
                lock (_navLock) return _nav ?? (_nav = ArenaNav.Build(RepossessArena.Solids()));
            }
        }

        /// <summary>The arrival of a door-spawned bot while it is still being moved (null once it has arrived and does not need it, and for
        /// bots staged without one).</summary>
        public BotArrival ArrivalOf(FightEnemy e)
        {
            BotArrival a;
            return _arrivals.TryGetValue(e, out a) ? a : null;
        }

        /// <summary>A door's telegraph started (a sound from its direction, a light, the door moving).</summary>
        public event Action<SpawnAssignment> TelegraphStarted;
        /// <summary>A bot came through its door.</summary>
        public event Action<FightEnemy, SpawnAssignment> BotSpawned;
        /// <summary>A door-spawned bot reached its working point and now sees and attacks (an arrival that gave up counts too).</summary>
        public event Action<FightEnemy, BotArrival> ArrivalDone;
        /// <summary>An asset's meter started: CONCIERGE's alarm.</summary>
        public event Action<JobEntry> AlarmRaised;
        public event Action<JobEntry> AssetTowed;
        /// <summary>The Unit was wrecked and the floor has been reset (the run ends after this when <see cref="FloorOptions.stopOnWreck"/>).</summary>
        public event Action Wrecked;

        /// <summary>Bots alive (not wrecked).</summary>
        public int Alive
        {
            get
            {
                int n = 0;
                foreach (var e in Fight.Enemies) if (!e.Wrecked) n++;
                return n;
            }
        }

        public bool GondolaAlive
        {
            get
            {
                foreach (var e in Fight.Enemies) if (!e.Wrecked && e.Def.id == SkylineEnemies.GondolaId) return true;
                return false;
            }
        }

        /// <summary>Alive plus the bots still behind a telegraph or waiting for a legal door: what counts against the cap.</summary>
        public int Committed
        {
            get
            {
                int n = Alive + Spawner.PendingCount;
                foreach (var w in _queue) n += w.enemies.Length;
                return n;
            }
        }

        /// <summary>Whether the Unit's feet are inside the west service lift (the floor's end, once the shutter is up).</summary>
        public bool InExitLift
        {
            get
            {
                var p = Fight.Player.Position;
                var z = RepossessArena.ExitLiftZone;
                return p.X >= z.min.X && p.X <= z.max.X && p.Y >= z.min.Y - 0.1f && p.Y <= z.max.Y && p.Z >= z.min.Z && p.Z <= z.max.Z;
            }
        }

        // ---- the run

        public FloorResult Run(float maxSeconds)
        {
            int steps = (int)Math.Ceiling(maxSeconds / FightSim.Step);
            for (int i = 0; i < steps && !_finished; i++) Tick();
            return Result();
        }

        public FloorResult Result()
        {
            var r = _r;
            var fr = Fight.Result();
            r.secondsRun = Fight.Time;
            r.hits = fr.hitsTaken;
            r.minPlating = fr.minPlating;
            r.wavesReleased = _releasedBase + Clock.Released;
            r.wavesSkipped = _skippedBase + Clock.Skipped;
            r.towed = Objective.TowedCount;
            r.towSeconds = (float[])_r.towSeconds.Clone();
            r.reachedExit = Flow.Phase == FloorPhase.Done;
            return r;
        }

        /// <summary>One 1/60 s step: movement and fight, then the tow meters, the wave clock, the doors and the end.</summary>
        public void Tick()
        {
            if (_finished) return;
            float h = FightSim.Step;
            StepArrivals(h);
            Fight.Tick();
            if (!Fight.Armor.Wrecked && Fight.Player.Position.Y < _o.fallDeathY) Fight.Armor.Damage(1e6f);
            if (Fight.Armor.Wrecked)
            {
                OnWrecked();
                return;
            }
            if (Flow.Phase == FloorPhase.Floor) Objective.Tick(h, Fight.Player.Position);
            if (Flow.Phase == FloorPhase.Floor || Flow.Phase == FloorPhase.Finale) TickWaves(h);
            Watch();
            if (Flow.Phase == FloorPhase.Finale && InExitLift)
            {
                Flow.ReachedExit();
                _finished = true;
            }
        }

        void TickWaves(float h)
        {
            if (Clock.Running)
            {
                _released.Clear();
                Clock.Tick(h, Committed, GondolaAlive, _released);
                foreach (var w in _released)
                {
                    _lastWave = w.number;
                    _queue.Enqueue(w);
                }
            }
            // the oldest released wave asks for its doors (retried every step until one is legal)
            if (_queue.Count > 0)
            {
                var w = _queue.Peek();
                var q = new SpawnQuery
                {
                    player = Fight.Player.Position, viewYawDeg = Fight.Player.Yaw, lastMeanBearing = _lastMean, alarmAsset = _alarm,
                    wave = w, seed = _seed ^ ((ulong)w.number * 0x9E3779B97F4A7C15UL), busyDoors = Spawner.BusyDoors(),
                };
                var choice = SpawnBearings.Choose(q, _doors);
                if (choice.ok)
                {
                    _queue.Dequeue();
                    _lastMean = choice.meanBearing;
                    _alarm = null;
                    _stuckFor = 0f;
                    _stuckCounted = false;
                    var first = choice.assignments[0].door.id;
                    foreach (var a in choice.assignments) _groupOf[a] = w.number * 10 + (a.door.id == first ? 0 : 1);
                    Spawner.Order(choice, Fight.Time);
                }
                else
                {
                    _stuckFor += h;
                    if (_stuckFor > StuckSeconds && !_stuckCounted)
                    {
                        _stuckCounted = true;
                        _r.wavesStuck++;
                    }
                }
            }
            _due.Clear();
            _started.Clear();
            Spawner.Tick(Fight.Time, _due, _started);
            foreach (var a in _started) TelegraphStarted?.Invoke(a);
            foreach (var a in _due) SpawnBot(a);
        }

        void SpawnBot(SpawnAssignment a)
        {
            int group;
            if (!_groupOf.TryGetValue(a, out group)) group = _lastWave * 10;
            _groupOf.Remove(a);
            var e = SpawnNow(a.enemy, a.door, group);
            BotSpawned?.Invoke(e, a);
        }

        /// <summary>Puts an awake bot through <paramref name="door"/> right now (the wave path uses it when the telegraph is up;
        /// a test or a pilot can stage a bot with it). The gondola rides from this rail end to the other.</summary>
        public FightEnemy SpawnNow(string enemyId, SpawnDoor door, int group)
        {
            var def = SkylineEnemies.Find(enemyId);
            if (def == null) throw new ArgumentException("unknown enemy " + enemyId);
            BotArrival arrival = _o.arrivals && door.kind != DoorKind.RailEnd
                ? new BotArrival(def, door, _seed ^ ((ulong)(_spawnIndex + 1) * 0xD1B54A32D192ED03UL), Nav, Cover) : null;
            bool arrives = arrival != null && BotArrival.Applies(def);
            var s = new Spawn(enemyId, arrives ? arrival.Start : SpawnPosition(door, def), group);
            if (def.layer == ThreatLayer.Walls)
            {
                s.position = door.position;
                s.railEnd = door.position;
                foreach (var d in _doors)
                    if (d.kind == DoorKind.RailEnd && d.id != door.id) s.railEnd = d.position;
            }
            var e = Fight.AddEnemy(s, _spawnIndex++);
            e.Awake = true;
            if (arrives)
            {
                e.Arriving = true;
                _arrivals[e] = arrival;
            }
            _r.botsSpawned++;
            return e;
        }

        /// <summary>Moves every door-spawned bot that is still arriving (and the drones that have arrived, which re-pick their hover point
        /// every 6..9 s) one step, before the fight's own step. A bot does not see or attack until it has arrived. A drone that is
        /// searching or going home (its brain moves it) is left to the brain until it has a line again.</summary>
        void StepArrivals(float h)
        {
            if (_arrivals.Count == 0) return;
            _arrivalList.Clear();
            _arrivalList.AddRange(_arrivals.Keys);
            var player = Fight.Player.Position;
            foreach (var e in _arrivalList)
            {
                var a = _arrivals[e];
                if (e.Wrecked) { _arrivals.Remove(e); continue; }
                bool flies = EngagePoints.Flies(e.Def);
                if (a.Engaged && !flies) { _arrivals.Remove(e); continue; }
                if (a.Engaged && (e.HasSearchGoal || e.Returning)) continue;
                e.Position = a.Step(e.Position, player, Cover, h, Fight.Area);
                if (a.Engaged && e.Arriving)
                {
                    e.Arriving = false;
                    e.Home = e.Position;
                    ArrivalDone?.Invoke(e, a);
                }
            }
        }

        static CoverBox[] _spawnCover;

        /// <summary>Where a bot appears for <paramref name="door"/>: in front of the door on the way it faces (the first step of
        /// <see cref="DoorInset"/> m, then sideways, that leaves its body clear of every piece: never inside a shop block, a column or an escalator), a
        /// drone from a ground-level shutter at head height; a rail bot on its rail end.</summary>
        public static Vector3 SpawnPosition(SpawnDoor door, EnemyDef def)
        {
            if (door.kind == DoorKind.RailEnd) return door.position;
            if (_spawnCover == null) _spawnCover = RepossessArena.SightBlockers();
            float yaw = door.yawDeg * (MathF.PI / 180f);
            var dir = new Vector3(MathF.Sin(yaw), 0f, MathF.Cos(yaw));
            var side = new Vector3(MathF.Cos(yaw), 0f, -MathF.Sin(yaw));
            var p = door.position;
            for (int k = 1; k <= 8; k++)
                foreach (float lateral in new[] { 0f, DoorInset, -DoorInset, 2f * DoorInset, -2f * DoorInset, 3f * DoorInset, -3f * DoorInset })
                {
                    p = door.position + dir * (DoorInset * k) + side * lateral;
                    if (def.layer == ThreatLayer.Air && p.Y < 1.5f) p.Y = DroneDoorHeight;
                    if (!CoverGeometry.BlockedThick(p, p, _spawnCover, def.bodyRadius)) return p;
                }
            return p;
        }

        void Watch()
        {
            int alive = Alive;
            if (alive > _r.peakAlive) _r.peakAlive = alive;
            if (Fight.Time + 1e-4f >= _nextSample)
            {
                _history.Add(new AliveSample { t = Fight.Time, alive = alive, wave = _lastWave });
                _nextSample += 1f;
            }
            if (_r.firstFlankedSeconds < 0f && Flanked()) { _r.firstFlankedSeconds = Fight.Time; _r.firstFlankedWave = _lastWave; }
        }

        // Two bots that both have the Unit in sight, 90 degrees or more apart as seen from it.
        bool Flanked()
        {
            var p = Fight.Player.Position;
            var seen = new List<float>();
            foreach (var e in Fight.Enemies)
            {
                if (e.Wrecked || !e.SeesPlayer) continue;
                if (e.Def.layer == ThreatLayer.Walls) continue;   // the gondola only lays mines: it does not flank
                seen.Add(SpawnBearings.Bearing(p, e.Position));
            }
            for (int i = 0; i < seen.Count; i++)
                for (int j = i + 1; j < seen.Count; j++)
                    if (SpawnBearings.AngleBetween(seen[i], seen[j]) >= FloorRules.MinBearingSpread) return true;
            return false;
        }

        void OnWrecked()
        {
            _r.wrecked = true;
            _r.wrecks++;
            _r.aliveAtWreck = Alive;
            _r.waveAtWreck = _lastWave;
            // the floor resets: clock, meters (same sockets), telegraphs, bots, panes, the lift shutter, the music is the caller's
            Flow.Wrecked();
            _releasedBase += Clock.Released;
            _skippedBase += Clock.Skipped;
            Clock.Reset();
            Objective.Reset();
            Spawner.Cancel();
            _arrivals.Clear();
            _queue.Clear();
            _groupOf.Clear();
            _lastMean = null;
            _alarm = null;
            _stuckFor = 0f;
            _stuckCounted = false;
            _lastWave = 0;
            foreach (var p in Fight.Panes)
                if (p.broken) _o.solidEnabled?.Invoke(p.id, true);
            Fight.RestorePanes();
            _o.solidEnabled?.Invoke(RepossessArena.LiftShutterId, true);
            Fight.Restart(_start, _o.startYaw);
            Wrecked?.Invoke();
            if (_o.stopOnWreck)
            {
                _finished = true;
                return;
            }
            // the lift ride is skipped: back at the drop
            for (int i = 0; i < _r.towSeconds.Length; i++) _r.towSeconds[i] = -1f;
            _r.timeToThird = -1f;
            Flow.SteppedIntoLift();
            Flow.DoorsOpened();
        }
    }
}

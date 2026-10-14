using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Movement;

namespace JamKit.Skyline
{
    /// <summary>The player's side of a headless fight (Task 11b): armour, how well the bot shoots, and stripping. Data, so the
    /// creator can tune the stand-in player without touching the Writ (whose numbers are fixed by Task 5).</summary>
    public sealed class FightPlayerDef
    {
        public float platingMax = 100f, chassisMax = 100f;
        /// <summary>The chance one Writ shot hits the body it's aimed at: <see cref="hitChanceNear"/> within
        /// <see cref="nearDistance"/> m, falling linearly to <see cref="hitChanceFar"/> at <see cref="farDistance"/> m and beyond.
        /// Weak spots are ignored here (every hit is a body hit).</summary>
        public float hitChanceNear = 0.85f, nearDistance = 15f, hitChanceFar = 0.5f, farDistance = 40f;
        /// <summary>Seconds to swing onto a new target before the first shot at it: <see cref="retargetBaseSeconds"/> plus
        /// <see cref="retargetSecondsPer90"/> for every 90 degrees the aim has to turn.</summary>
        public float retargetBaseSeconds = 0.08f, retargetSecondsPer90 = 0.15f;
        /// <summary>Moving faster than <see cref="movingFastSpeed"/> m/s (a sprint or a fast strafe) takes
        /// <see cref="movingFastPenalty"/> off the hit chance.</summary>
        public float movingFastSpeed = 6f, movingFastPenalty = 0.15f;
        /// <summary>A fresh wreck is stripped by passing within this many metres of it (on the floor plan).</summary>
        public float stripRadius = 1.5f;

        public float HitChance(float distance)
        {
            if (!(distance > nearDistance)) return hitChanceNear;
            if (distance >= farDistance || farDistance <= nearDistance) return hitChanceFar;
            float t = (distance - nearDistance) / (farDistance - nearDistance);
            return hitChanceNear + (hitChanceFar - hitChanceNear) * t;
        }

        /// <summary>The hit chance at <paramref name="distance"/> for a shooter moving at <paramref name="speed"/> m/s.</summary>
        public float HitChance(float distance, float speed) =>
            Math.Max(0f, HitChance(distance) - (speed > movingFastSpeed ? movingFastPenalty : 0f));

        /// <summary>Seconds to swing the aim through <paramref name="degrees"/>.</summary>
        public float RetargetSeconds(float degrees) =>
            retargetBaseSeconds + retargetSecondsPer90 * Math.Max(0f, Math.Min(180f, degrees)) / 90f;
    }

    /// <summary>What the player bot does this step: move, and optionally hold its fire or reload. Aiming is the sim's: it
    /// shoots the nearest awake enemy it can see within the Writ's range.</summary>
    public struct FightInput
    {
        public MoveInput move;
        public bool holdFire;
        public bool reload;
    }

    /// <summary>Drives the player in a <see cref="FightSim"/>, like a player's hands; it may read the whole fight (a player
    /// sees the telegraphs, the beams and the acid).</summary>
    public interface IFightBot
    {
        FightInput Next(FightSim fight, float h);
    }

    /// <summary>The ground a fight happens on (docs/SKYLINE-PLAN-FLOOR1.md Task W3): the box (x, z) the floor units and the
    /// patrols stay inside, and the furthest +Z the drone search may go. <see cref="Galleria"/> is the old hall (the default,
    /// so the M1 fights behave exactly as before); <see cref="Floor1"/> is the Food Court.</summary>
    public struct FightArea
    {
        public float minX, maxX, minZ, maxZ;
        /// <summary>The furthest +Z a searching drone may go (the old hall stops south of the mezzanine deck's edge).</summary>
        public float searchMaxZ;
        /// <summary>Added to a rail bot's rail ends to get the rail its MINES are laid along (zero: the rail itself). On the
        /// Food Court the gondola rides outside the glass but lays its mines on the inside of the wall-run.</summary>
        public Vector3 mineRailOffset;

        /// <summary>The M1 warm-up hall (<see cref="GalleriaArena"/>'s hall box).</summary>
        public static readonly FightArea Galleria = new FightArea
        {
            minX = GalleriaArena.HallMinX, maxX = GalleriaArena.HallMaxX, minZ = GalleriaArena.HallMinZ, maxZ = GalleriaArena.HallMaxZ, searchMaxZ = 11f,
        };

        /// <summary>Floor 1: the Food Court (interior x -35..35, z -28..28). The gondola's mines go on the inside face of the
        /// façade (x 34.9) at the hatches' height (2.5 m), docs/SKYLINE-FLOOR-REPOSSESS.md section 10.</summary>
        public static readonly FightArea Floor1 = new FightArea
        {
            minX = -35f, maxX = 35f, minZ = -28f, maxZ = 28f, searchMaxZ = 28f, mineRailOffset = new Vector3(-1.6f, -0.5f, 0f),
        };
    }

    /// <summary>A glass pane a shot through breaks (the Food Court's façade, docs/SKYLINE-FLOOR-REPOSSESS.md section 10).</summary>
    public sealed class BreakablePane
    {
        public string id = "";
        public CoverBox box;
        public bool broken;
    }

    /// <summary>One bot in a fight: its body, its behaviour clock and what its kind does.</summary>
    public sealed class FightEnemy
    {
        public int Index { get; internal set; }
        public EnemyDef Def { get; internal set; }
        public int Group { get; internal set; }
        public EnemyBody Body { get; internal set; }
        public BotBrain Brain { get; internal set; }
        /// <summary>World metres. A floor-care unit's is on the floor; the others' is the body's centre.</summary>
        public Vector3 Position { get; internal set; }
        public bool Awake { get; internal set; }
        /// <summary>It is still coming through its door to its working point (<see cref="BotArrival"/>): it neither sees nor attacks
        /// and its brain does not run; whoever moves it (RepossessSim) clears this when it arrives.</summary>
        public bool Arriving { get; internal set; }
        public bool Wrecked => Body.Wrecked;
        /// <summary>It had the player in sight (and in range) at the end of the last step.</summary>
        public bool SeesPlayer { get; internal set; }
        // the floor-care unit's sweep
        public bool Sweeping { get; internal set; }
        public float SweepTime { get; internal set; }
        public float SweepYaw { get; internal set; }
        internal bool sweepHit;
        internal int detourBox = -1, detourCorner;
        /// <summary>Where the player was on the step it lost sight of it (the search's target), if it has lost sight of it.</summary>
        public Vector3 LastKnown { get; internal set; }
        public bool HasLastKnown { get; internal set; }
        internal Vector3 searchGoal;
        internal bool hasSearchGoal, sawLastStep;
        internal float lastSeenAt = float.NegativeInfinity;
        internal readonly List<Vector3> trail = new List<Vector3>();
        /// <summary>Its search goal while it has one (see FightSim's search rule).</summary>
        public bool HasSearchGoal => hasSearchGoal;
        public Vector3 SearchGoal => searchGoal;
        /// <summary>Its memory ran out: it is going home the way it came.</summary>
        public bool Returning { get; internal set; }
        /// <summary>Where it spawned.</summary>
        public Vector3 Home { get; internal set; }
        // the spotlight's beam
        public TelegraphedShot Beam { get; internal set; }
        // the gondola
        public GondolaRail Rail { get; internal set; }
        /// <summary>The rail its mines are laid along when that differs from <see cref="Rail"/> (<see cref="FightArea.mineRailOffset"/>), or null.</summary>
        public GondolaRail MineRail { get; internal set; }
        public MineLayer Mines { get; internal set; }
        /// <summary>The drone's hover patrol round <see cref="Position"/> (its post), or null for bots that hold still.</summary>
        public HoverPatrol Patrol { get; internal set; }
        /// <summary>The floor-care unit's idle cleaning round <see cref="Home"/> until it wakes (<see cref="FloorCareIdle"/>), or null.</summary>
        public FloorCareIdle Cleaning { get; internal set; }
        /// <summary>The gondola's idle washing along the start of its rail until it wakes (<see cref="GondolaIdle"/>), or null.</summary>
        public GondolaIdle Washing { get; internal set; }
        internal bool railStarted;

        /// <summary>Where it is shot at and sees from: a floor-care unit's body sits 0.4 m above the floor; a patrolling drone
        /// is at its post plus the patrol's offset.</summary>
        public Vector3 Center => Def.moveSpeed > 0f ? Position + new Vector3(0f, FightSim.FloorUnitCenter, 0f)
            : Patrol != null ? Position + Patrol.Offset : Position;
    }

    /// <summary>A wreck lying in the fight, strippable while fresh.</summary>
    public sealed class FightWreck
    {
        public Wreck Wreck;
        /// <summary>On the floor under where the bot was wrecked.</summary>
        public Vector3 Position;
        public int EnemyIndex, Group;
    }

    /// <summary>One headless fight's outcome (Task 11b).</summary>
    public struct FightResult
    {
        public bool cleared, playerWrecked;
        /// <summary>When the last enemy was wrecked, or -1.</summary>
        public float clearSeconds;
        /// <summary>When the Unit was wrecked, or -1.</summary>
        public float wreckedAt;
        /// <summary>The lowest plating seen during the fight.</summary>
        public float minPlating;
        /// <summary>Chassis lost (it never regenerates).</summary>
        public float chassisLost;
        /// <summary>Everything that landed on the Unit (plating plus chassis).</summary>
        public float damageTaken;
        /// <summary>The Writ's shots; the enemies' shots at the player (bolts fired, beams fired, acid sweeps started);
        /// the hits the Unit took (bolts, beams, acid, mine blasts).</summary>
        public int shotsFired, shotsAtPlayer, hitsTaken;
        /// <summary>The fight's length: the clear, the wreck, or the cap.</summary>
        public float seconds;
        public int enemiesWrecked, enemies;
        public float recoveredScrap;
    }

    /// <summary>
    /// A headless fight on the real cover (Task 11b), separate from <see cref="CombatSim"/> (the dodge harness): the movement
    /// engine moves the player, who fires the Writ back; every spawn is an <see cref="EnemyBody"/> with a
    /// <see cref="BotBrain"/>, woken by groups. Fixed step (<see cref="Step"/>), seeded, deterministic.
    /// <para>Simplifications (each a stand-in for the Unity fight, stated so nobody mistakes them for the game):</para>
    /// <list type="bullet">
    /// <item>The Writ's shot is a roll: the bot aims at the nearest awake enemy it can see (eye to body centre, through the
    /// cover), and hits with <see cref="FightPlayerDef.HitChance"/>; damage is the Writ's with its falloff, and lands on the
    /// first plate still on, then the chassis (no weak spots, no Writ spots).</item>
    /// <item>Sight is a segment test against the cover boxes (and the mezzanine deck) from the bot's centre to the player's
    /// chest or eye; the player sees from its eye. Bolts are spheres of the projectile's radius flying straight; a bolt is
    /// stopped by a box its edge touches (the box grown by the radius) and hits the player's capsule.</item>
    /// <item>Spotlights hold still. Drones fly a bounded hover patrol round their post (<see cref="HoverPatrol"/>: a bob, and
    /// while awake sideways strafes of up to <see cref="EnemyDef.patrolLeash"/> across the line to the player, a new leg after
    /// each volley, a dash when hit), and they see and shoot from where the patrol has them. The post itself holds until
    /// they lose sight of the player; then (awake, with a last-known
    /// position) they drift sideways at their own height, at <see cref="EnemyDef.searchSpeed"/>, to the nearest point within
    /// 10 m that sees where they last saw the player and that they can reach in a straight line without touching cover
    /// (they pick again every half second; they never pass through cover or under the mezzanine deck). A drone that re-sights
    /// the player within <see cref="EnemyDef.rememberSeconds"/> skips its alert (BotBrain).</item>
    /// <item>Floor-care units head for the player round the cover: when a box stands on the straight line they make for the
    /// corner of it that gives the shortest way round, and slide along a face they still run into (no path finding over the
    /// whole hall: a unit can stall in a concave corner between two boxes, and units pass through each other). Gondolas ride
    /// their rails.</item>
    /// <item>Before they wake, floor-care units clean round their post (<see cref="FloorCareIdle"/>: short runs, gentle turns, a
    /// bump-and-turn at cover, within 1.5 m) and gondolas wash a short stretch of their rail from the spawn
    /// (<see cref="GondolaIdle"/>); they see from where the idle work has them, and once awake they go on from there.</item>
    /// <item>The Writ's hit chance drops by <see cref="FightPlayerDef.movingFastPenalty"/> while the player moves faster than
    /// <see cref="FightPlayerDef.movingFastSpeed"/>, and swinging onto a new target costs time that grows with the turn.</item>
    /// <item>Two random streams: the enemies' aim error, and the player's hit rolls, so runs with one seed draw the same enemy
    /// aim errors in the same order. <see cref="Invulnerable"/> (the dodge band) counts hits without applying them.</item>
    /// <item>A floor-care sweep turns to face the player when it starts, damages once per sweep, and is stopped by a box
    /// between the nozzle and the player's ankles; feet above the def's ankle height clear it.</item>
    /// <item>Wrecks lie on the floor under the bot; passing within <see cref="FightPlayerDef.stripRadius"/> strips a fresh one.</item>
    /// </list>
    /// </summary>
    public sealed class FightSim
    {
        /// <summary>The fight's fixed step (s).</summary>
        public const float Step = 1f / 60f;
        /// <summary>A floor-care unit's body centre above the floor (m).</summary>
        public const float FloorUnitCenter = 0.4f;
        /// <summary>The acid jet's height above the floor for its cover test (m).</summary>
        const float JetHeight = 0.2f;
        const float MaxBoltAge = 4f;
        /// <summary>A floor unit does not reach a player standing more than this far below its own floor (the pit, seen from the rim).</summary>
        const float FloorUnitDropReach = 1.5f;

        struct Bolt
        {
            public Vector3 pos, vel;
            public float radius, age, damage;
        }

        readonly MoveSim _player;
        readonly IFightBot _bot;
        readonly List<FightEnemy> _enemies = new List<FightEnemy>();
        readonly List<FightWreck> _wrecks = new List<FightWreck>();
        readonly List<Bolt> _bolts = new List<Bolt>();
        /// <summary>Where the Unit's chest has been lately: the drones aim at where it was <see cref="EnemyDef.aimLagSeconds"/> ago.</summary>
        readonly PositionTrail _trail = new PositionTrail();
        readonly CoverBox[] _cover;
        readonly CoverBox[] _floorCover;
        /// <summary>What stops a Writ shot or a bolt: <see cref="_cover"/>, plus any shot-only blockers (glass balustrades).</summary>
        CoverBox[] _shotCover;
        readonly List<BreakablePane> _panes = new List<BreakablePane>();
        readonly DifficultyDef _difficulty;
        readonly FightPlayerDef _pd;
        readonly FightArea _area;
        readonly ulong _seed;
        readonly Func<string, EnemyDef> _lookup;
        readonly GunDef _gunDef;
        SkylineGun _gun;
        /// <summary>Two streams: the enemies' aim error, and the player's hit rolls, so two runs with one seed draw the same
        /// enemy aim errors in the same order however differently the player shoots.</summary>
        readonly Rng _rng, _playerRng;
        readonly List<BotCue> _cues = new List<BotCue>();
        readonly List<GunCue> _gunCues = new List<GunCue>();
        readonly List<float> _blasts = new List<float>();
        readonly Dictionary<int, int> _shotsByGroup = new Dictionary<int, int>();
        FightEnemy _target;
        Vector3 _aimDir = new Vector3(0f, 0f, 1f);
        float _readyAt;
        FightResult _r;
        bool _over;

        /// <summary>The band measurement's mode: hits are counted but do no damage, so a runner can't be wrecked mid-measure.</summary>
        public bool Invulnerable { get; set; }
        /// <summary>Seconds of live aimed shooters (drones, spotlights) with the player in sight, summed over the shooters: the
        /// dodge band's exposure.</summary>
        public float ShooterSecondsInSight { get; private set; }
        /// <summary>Shots at the player (bolts, beams, sweeps) by the bots of <paramref name="group"/>.</summary>
        public int ShotsAtPlayerByGroup(int group) => _shotsByGroup.TryGetValue(group, out int n) ? n : 0;

        public FightArea Area => _area;
        /// <summary>Whether the fight ends once every enemy is wrecked (there must have been one). The floor sim turns this off, since waves keep adding bots.</summary>
        public bool EndsOnClear { get => _endsOnClear; set => _endsOnClear = value; }
        bool _endsOnClear = true;
        /// <summary>The glass panes that shots break (see SetPanes); none on the M1 fights.</summary>
        public IReadOnlyList<BreakablePane> Panes => _panes;
        /// <summary>A shot passed through a pane and broke it: the world should stop colliding with it.</summary>
        public event Action<BreakablePane> PaneBroken;

        public MoveSim Player => _player;
        public PlayerArmor Armor { get; private set; }
        public SkylineGun Gun => _gun;
        public IReadOnlyList<FightEnemy> Enemies => _enemies;
        public IReadOnlyList<FightWreck> Wrecks => _wrecks;
        public IReadOnlyList<CoverBox> Cover => _cover;
        public FightPlayerDef PlayerDef => _pd;
        public float Time { get; private set; }
        /// <summary>The enemy the Writ is on, or null.</summary>
        public FightEnemy Target => _target;
        public bool Over => _over;

        /// <param name="encounter">Spawns in world metres (the arena's frame; no anchor translation).</param>
        /// <param name="world">The movement world; every box in <paramref name="cover"/> must be solid in it (checked).</param>
        /// <param name="cover">What blocks sight and shots (e.g. <see cref="GalleriaArena.FightCover"/>).</param>
        public FightSim(EncounterDef encounter, IMoveWorld world, IFightBot bot, IReadOnlyList<CoverBox> cover, Vector3 start,
            ulong seed, DifficultyDef difficulty = null, Func<string, EnemyDef> lookup = null, FightPlayerDef player = null, GunDef gun = null,
            float startYaw = 0f, FightArea? area = null)
        {
            if (encounter?.spawns == null) throw new ArgumentException("an encounter with spawns is needed", nameof(encounter));
            if (world == null) throw new ArgumentNullException(nameof(world));
            _bot = bot ?? throw new ArgumentNullException(nameof(bot));
            _difficulty = difficulty ?? Difficulties.Normal();
            _pd = player ?? new FightPlayerDef();
            _area = area ?? FightArea.Galleria;
            _seed = seed;
            _gunDef = gun ?? SkylineGuns.Writ();
            lookup = lookup ?? SkylineEnemies.Find;
            _lookup = lookup;
            _cover = cover != null ? new List<CoverBox>(cover).ToArray() : new CoverBox[0];
            _shotCover = _cover;
            var floor = new List<CoverBox>();
            foreach (var b in _cover)
                if (b.min.Y <= 0.5f) floor.Add(b);
            _floorCover = floor.ToArray();
            for (int i = 0; i < _cover.Length; i++)
            {
                // probe the centre and four corners (just inside), low and high: every probe must be solid
                var b = _cover[i];
                float ix = Math.Min(0.1f, (b.max.X - b.min.X) * 0.25f), iz = Math.Min(0.1f, (b.max.Z - b.min.Z) * 0.25f);
                float probeH = Math.Min(0.5f, (b.max.Y - b.min.Y) * 0.5f);
                float cx = (b.min.X + b.max.X) * 0.5f, cz = (b.min.Z + b.max.Z) * 0.5f;
                var xs = new[] { cx, b.min.X + ix, b.max.X - ix, b.min.X + ix, b.max.X - ix };
                var zs = new[] { cz, b.min.Z + iz, b.min.Z + iz, b.max.Z - iz, b.max.Z - iz };
                foreach (float y in new[] { b.min.Y + 0.01f, Math.Max(b.min.Y + 0.01f, b.max.Y - probeH - 0.01f) })
                    for (int k = 0; k < xs.Length; k++)
                        if (world.CapsuleFits(new Vector3(xs[k], y, zs[k]), 0.05f, probeH))
                            throw new ArgumentException($"cover box {i} ({b.min} to {b.max}) is not solid in the move world at ({xs[k]}, {y}, {zs[k]}): the player would run through it");
            }
            _rng = new Rng(seed);
            _playerRng = new Rng(seed, 0x5DEECE66DUL);
            _player = new MoveSim(world, new MoveDef());
            _player.Teleport(start, startYaw, Vector3.Zero);
            Armor = new PlayerArmor(_pd.platingMax, _pd.chassisMax);
            _gun = new SkylineGun(_gunDef);
            _gun.Equip(null);
            _gun.Tick(1f, false, false, null);   // the gun is out before the fight starts
            for (int i = 0; i < encounter.spawns.Length; i++) AddEnemy(encounter.spawns[i], i);
            _r = new FightResult { clearSeconds = -1f, wreckedAt = -1f, minPlating = Armor.Plating, enemies = _enemies.Count };
            _cleaningBlocked = (a, b, r) => FloorCareIdle.FloorBlocked(a, b, r, _floorCover, _area);
            _patrolBlocked = (a, b, r) => HoverPatrol.LeavesArea(a, b, _area.minX + 1f, _area.maxX - 1f, _area.minZ + 1f, _area.searchMaxZ)
                || CoverGeometry.BlockedThick(a, b, _cover, r);
        }

        /// <summary>Adds one enemy to the fight, as the constructor does for each of its spawns: <paramref name="index"/> seeds its
        /// brain and its patrol or idle work (the constructor passes the spawn's place in the list), so the same spawn and index give
        /// the same enemy whenever it is added. The enemy starts asleep; <see cref="FightEnemy.Awake"/> or <see cref="Wake"/> wakes it.</summary>
        public FightEnemy AddEnemy(Spawn s, int index)
        {
            var def = _lookup(s.enemy);
            if (def == null) throw new ArgumentException("unknown enemy " + s.enemy);
            ulong seed = _seed;
            int i = index;
            var e = new FightEnemy
            {
                Index = i, Def = def, Group = s.group, Body = new EnemyBody(def),
                Brain = new BotBrain(def, seed * 7919UL + (ulong)i * 104729UL + 1UL), Position = s.position, Home = s.position,
            };
            if (def.aim == AimModel.FiresAtWhereYouAre && def.projectileSpeed <= 0f)
                e.Beam = new TelegraphedShot
                {
                    telegraph = def.telegraphSeconds, lockBefore = def.beamLockBefore, followRate = def.beamFollowRate, hitRadius = def.beamHitRadius,
                };
            if (def.layer == ThreatLayer.Walls)
            {
                e.Rail = new GondolaRail(s.position, s.railEnd, def.railSpeed);
                if (_area.mineRailOffset != Vector3.Zero)
                    e.MineRail = new GondolaRail(s.position + _area.mineRailOffset, s.railEnd + _area.mineRailOffset, def.railSpeed);
                e.Mines = new MineLayer(def);
            }
            // its own stream, so the patrol never shifts the enemies' aim errors
            if (e.Beam == null && e.Rail == null && !(def.moveSpeed > 0f) && HoverPatrol.Moves(def))
                e.Patrol = new HoverPatrol(def, PatrolSeed(seed, i));
            // the idle work before waking (its own stream too)
            if (FloorCareIdle.Works(def)) e.Cleaning = new FloorCareIdle(def, PatrolSeed(seed, i));
            if (e.Rail != null && GondolaIdle.Works(def)) e.Washing = new GondolaIdle(def, e.Rail.Length, PatrolSeed(seed, i));
            _enemies.Add(e);
            if (_enemies.Count > _r.enemies) _r.enemies = _enemies.Count;
            return e;
        }

        /// <summary>The seed of spawn <paramref name="index"/>'s hover patrol in a fight seeded <paramref name="seed"/> (the Unity
        /// arena seeds its own the same way from its spawn seed).</summary>
        public static ulong PatrolSeed(ulong seed, int index) => seed * 0x2545F4914F6CDD1DUL + (ulong)index * 0x9E3779B97F4A7C15UL + 0x5A17UL;

        readonly Func<Vector3, Vector3, float, bool> _patrolBlocked, _cleaningBlocked;

        // ---- geometry the bots may use too

        public Vector3 Chest => _player.Position + new Vector3(0f, _player.Height * 0.5f, 0f);
        public Vector3 Eye => EyeAt(_player.Position);
        public Vector3 EyeAt(Vector3 feet) => feet + new Vector3(0f, _player.Height * (_player.def.eyeHeight / _player.def.standHeight), 0f);

        public bool Clear(Vector3 a, Vector3 b) => !CoverGeometry.Blocked(a, b, _cover);

        /// <summary>Whether a Writ shot (or a bolt's line) from <paramref name="a"/> to <paramref name="b"/> is unblocked: the
        /// cover, plus the shot-only blockers (<see cref="SetShotBlockers"/>).</summary>
        public bool ShotClear(Vector3 a, Vector3 b) => !CoverGeometry.Blocked(a, b, _shotCover);

        /// <summary>Boxes that stop shots and bolts but not sight (the Food Court's glass balustrades: glass is never free to
        /// shoot through, docs/SKYLINE-FLOOR-REPOSSESS.md section 10). Call before the run; the M1 fights have none.</summary>
        public void SetShotBlockers(IEnumerable<CoverBox> extra)
        {
            var l = new List<CoverBox>(_cover);
            if (extra != null) l.AddRange(extra);
            _shotCover = l.ToArray();
        }

        /// <summary>The panes a Writ shot breaks when its line passes through one (the Food Court's façade). A pane is neither
        /// sight nor shot cover; breaking it raises <see cref="PaneBroken"/> so the world can stop colliding with it. Call before the run.</summary>
        public void SetPanes(IEnumerable<BreakablePane> panes)
        {
            _panes.Clear();
            if (panes != null) _panes.AddRange(panes);
        }

        /// <summary>Every pane whole again (the floor restarts).</summary>
        public void RestorePanes()
        {
            foreach (var p in _panes) p.broken = false;
        }

        void BreakPanes(Vector3 from, Vector3 to)
        {
            foreach (var p in _panes)
            {
                if (p.broken || !CoverGeometry.SegmentHitsBox(from, to, p.box)) continue;
                p.broken = true;
                PaneBroken?.Invoke(p);
            }
        }

        /// <summary>A wreck on a restarting fight: every enemy, wreck and bolt gone, the Unit whole again (new armour, a full
        /// Writ) and standing at <paramref name="start"/> facing <paramref name="startYaw"/>; the clock, the counters, the random
        /// streams and the panes are kept (call <see cref="RestorePanes"/> to mend them).</summary>
        public void Restart(Vector3 start, float startYaw)
        {
            _enemies.Clear();
            _wrecks.Clear();
            _bolts.Clear();
            _cues.Clear();
            _gunCues.Clear();
            _blasts.Clear();
            _target = null;
            _aimDir = new Vector3(0f, 0f, 1f);
            _readyAt = 0f;
            Armor = new PlayerArmor(_pd.platingMax, _pd.chassisMax);
            _player.Teleport(start, startYaw, Vector3.Zero);
            _trail.Clear();
            _gun = new SkylineGun(_gunDef);
            _gun.Equip(null);
            _gun.Tick(1f, false, false, null);
            _r.playerWrecked = false;
            _over = false;
        }

        /// <summary>Whether <paramref name="e"/> would see a player standing with its feet at <paramref name="feet"/> (its chest or
        /// eye, within its range, nothing between).</summary>
        public bool CanSee(FightEnemy e, Vector3 feet) => CanSee(e, feet, 0f);

        /// <summary>As <see cref="CanSee(FightEnemy, Vector3)"/>, and also the eye raised by <paramref name="eyeLift"/> m (a jump:
        /// a double jump lifts it about 2 m).</summary>
        public bool CanSee(FightEnemy e, Vector3 feet, float eyeLift)
        {
            Vector3 c = e.Center;
            Vector3 chest = feet + new Vector3(0f, _player.def.standHeight * 0.5f, 0f), eye = feet + new Vector3(0f, _player.def.eyeHeight, 0f);
            if ((Vector3.Distance(c, chest) <= e.Def.range && Clear(c, chest)) || (Vector3.Distance(c, eye) <= e.Def.range && Clear(c, eye))) return true;
            if (!(eyeLift > 0f)) return false;
            Vector3 up = eye + new Vector3(0f, eyeLift, 0f);
            return Vector3.Distance(c, up) <= e.Def.range && Clear(c, up);
        }

        /// <summary>Whether a bot of <paramref name="e"/>'s range, standing at <paramref name="from"/>, would see a player standing at
        /// <paramref name="feet"/> (chest, eye, or the eye raised by <paramref name="eyeLift"/>).</summary>
        public bool CanSeeFrom(FightEnemy e, Vector3 from, Vector3 feet, float eyeLift)
        {
            Vector3 chest = feet + new Vector3(0f, _player.def.standHeight * 0.5f, 0f), eye = feet + new Vector3(0f, _player.def.eyeHeight, 0f);
            if ((Vector3.Distance(from, chest) <= e.Def.range && Clear(from, chest)) || (Vector3.Distance(from, eye) <= e.Def.range && Clear(from, eye))) return true;
            if (!(eyeLift > 0f)) return false;
            Vector3 up = eye + new Vector3(0f, eyeLift, 0f);
            return Vector3.Distance(from, up) <= e.Def.range && Clear(from, up);
        }

        /// <summary>A live drone or spotlight: the bots that shoot at the player (not the floor-care units or the gondolas).</summary>
        public static bool IsShooter(FightEnemy e) => !e.Wrecked && (e.Def.aim == AimModel.LeadsYou || e.Def.aim == AimModel.FiresAtWhereYouAre);

        /// <summary>Whether a player standing at <paramref name="feet"/> could shoot <paramref name="e"/> (eye to centre, within the gun's range).</summary>
        public bool CanShoot(Vector3 feet, FightEnemy e)
        {
            Vector3 eye = EyeAt(feet);
            return Vector3.Distance(eye, e.Center) <= _gun.Def.range && ShotClear(eye, e.Center);
        }

        bool SeesNow(FightEnemy e, out Vector3 aimAt)
        {
            Vector3 c = e.Center, chest = Chest, eye = Eye;
            aimAt = chest;
            if (Vector3.Distance(c, chest) <= e.Def.range && Clear(c, chest)) return true;
            aimAt = eye;
            return Vector3.Distance(c, eye) <= e.Def.range && Clear(c, eye);
        }

        // ---- the run

        public FightResult Run(float maxSeconds)
        {
            int steps = (int)MathF.Ceiling(maxSeconds / Step);
            for (int i = 0; i < steps && !_over; i++) Tick();
            return Result();
        }

        public FightResult Result()
        {
            var r = _r;
            r.seconds = Time;
            r.chassisLost = Armor.ChassisMax - Armor.Chassis;
            r.recoveredScrap = Armor.RecoveredScrap;
            int wrecked = 0;
            foreach (var e in _enemies) if (e.Wrecked) wrecked++;
            r.enemiesWrecked = wrecked;
            return r;
        }

        public void Tick()
        {
            if (_over) return;
            float h = Step;
            var input = _bot.Next(this, h);
            _player.Step(in input.move, h);
            Time += h;
            _trail.Record(Time, Chest);

            Shoot(input, h);
            foreach (var e in _enemies) Act(e, h);
            FlyBolts(h);
            StripWrecks();

            if (Armor.Plating < _r.minPlating) _r.minPlating = Armor.Plating;
            if (Armor.Wrecked)
            {
                _r.playerWrecked = true;
                _r.wreckedAt = Time;
                _over = true;
                return;
            }
            bool all = _enemies.Count > 0 && _endsOnClear;
            foreach (var e in _enemies)
                if (!e.Wrecked) { all = false; break; }
            if (all)
            {
                _r.cleared = true;
                _r.clearSeconds = Time;
                _over = true;
            }
        }

        void Shoot(FightInput input, float h)
        {
            Vector3 eye = Eye;
            FightEnemy best = null;
            float bestD = float.PositiveInfinity;
            foreach (var e in _enemies)
            {
                if (e.Wrecked || !e.Awake) continue;
                float d = Vector3.Distance(eye, e.Center);
                if (d >= bestD || d > _gun.Def.range || !ShotClear(eye, e.Center)) continue;
                best = e;
                bestD = d;
            }
            if (best != _target)
            {
                _target = best;
                if (best != null)
                {
                    // the swing onto it costs more the further the aim has to turn
                    Vector3 to = best.Center - eye;
                    float len = to.Length();
                    Vector3 dir = len > 1e-4f ? to / len : _aimDir;
                    float deg = MathF.Acos(Math.Max(-1f, Math.Min(1f, Vector3.Dot(dir, _aimDir)))) * (180f / MathF.PI);
                    _readyAt = Time + _pd.RetargetSeconds(deg);
                    _aimDir = dir;
                }
            }
            else if (_target != null)
            {
                Vector3 to = _target.Center - eye;
                if (to.LengthSquared() > 1e-8f) _aimDir = Vector3.Normalize(to);
            }
            _gunCues.Clear();
            bool reloading = _gun.State.IsReloading;
            if (!reloading && (_gun.Ammo == 0 || (input.reload && _gun.Ammo < _gun.Def.weapon.magazineSize))) _gun.Reload(_gunCues);
            bool pull = _target != null && !input.holdFire && Time >= _readyAt && _gun.Ammo > 0 && !_gun.State.IsReloading;
            _gun.Tick(h, pull, pull, _gunCues);
            foreach (var c in _gunCues)
            {
                if (c.kind != GunCueKind.Shot) continue;
                _r.shotsFired++;
                if (_target != null && _panes.Count > 0) BreakPanes(eye, _target.Center);
                if (_target == null || _target.Wrecked) continue;
                float dist = Vector3.Distance(eye, _target.Center);
                if (!(_playerRng.NextFloat() < _pd.HitChance(dist, _player.HorizontalSpeed))) continue;
                Damage(_target, _gun.Def.damage * _gun.Def.FalloffScale(dist));
            }
        }

        /// <summary>Damages <paramref name="e"/> (the Writ's hits come through here): the first plate still on, then the chassis;
        /// its group wakes; a wrecking blow leaves a wreck on the floor under it.</summary>
        public void Damage(FightEnemy e, float amount)
        {
            int plate = -1;
            for (int i = 0; i < e.Body.PlateCount; i++)
                if (e.Body.PlateOn(i)) { plate = i; break; }
            var d = e.Body.Damage(amount, plate);
            Wake(e.Group);
            e.Brain.OnDamaged(null);
            e.Patrol?.OnHit();
            if (d.wrecked)
            {
                e.Brain.OnWrecked(null);
                e.Sweeping = false;
                Vector3 at = e.Center;
                _wrecks.Add(new FightWreck
                {
                    Wreck = new Wreck(e.Def, Time), Position = new Vector3(at.X, 0f, at.Z), EnemyIndex = e.Index, Group = e.Group,
                });
            }
        }

        /// <summary>Wakes every live bot of <paramref name="group"/>.</summary>
        /// <summary>Puts a wreck of <paramref name="def"/> on the floor at <paramref name="at"/>, wrecked at <paramref name="wreckedAt"/> (s).</summary>
        public FightWreck AddWreck(EnemyDef def, Vector3 at, float wreckedAt)
        {
            var w = new FightWreck { Wreck = new Wreck(def, wreckedAt), Position = new Vector3(at.X, 0f, at.Z), EnemyIndex = -1, Group = -1 };
            _wrecks.Add(w);
            return w;
        }

        public void Wake(int group)
        {
            foreach (var o in _enemies)
                if (o.Group == group && !o.Wrecked) o.Awake = true;
        }

        void Act(FightEnemy e, float h)
        {
            if (e.Wrecked)
            {
                // a wrecked gondola's mines stay on the wall
                if (e.Mines != null) TickMines(e, h, false);
                e.SeesPlayer = false;
                return;
            }
            if (e.Arriving)
            {
                e.SeesPlayer = false;
                return;
            }
            bool sees = SeesNow(e, out Vector3 aimAt);
            e.SeesPlayer = sees;
            if (!e.Awake && sees) Wake(e.Group);
            _cues.Clear();
            if (e.Def.layer == ThreatLayer.Walls)
            {
                e.Brain.Tick(h, e.Awake && sees, _cues);
                if (e.Awake)
                {
                    // woken: on along the rail at full speed from where the washing left it
                    if (!e.railStarted && e.Washing != null) { e.Rail.StartAt(e.Washing.Along); e.MineRail?.StartAt(e.Washing.Along); }
                    e.railStarted = true;
                    e.Rail.Tick(h);
                    e.MineRail?.Tick(h);
                    e.Position = e.Rail.Position;
                }
                else if (e.Washing != null)
                {
                    e.Washing.Tick(h);
                    e.Position = e.Rail.PositionAt(e.Washing.Along);
                }
                TickMines(e, h, e.Awake);
                return;
            }
            if (e.Def.moveSpeed > 0f)
            {
                FloorUnit(e, h);
                return;
            }
            e.Brain.Tick(h, e.Awake && sees, _cues);
            if (e.Awake && sees) ShooterSecondsInSight += h;
            if (sees)
            {
                e.lastSeenAt = Time;
                e.hasSearchGoal = false;
                e.Returning = false;
            }
            else if (e.Awake && e.Def.searchSpeed > 0f && e.lastSeenAt > float.NegativeInfinity) Search(e, h, e.sawLastStep);
            e.sawLastStep = sees;
            foreach (var c in _cues)
            {
                if (e.Beam != null)
                {
                    if (c.kind == BotCueKind.State && c.state == BotState.Telegraph && sees) e.Beam.Start(Chest);
                }
                else if (c.kind == BotCueKind.Attack && sees) FireBolt(e, aimAt);
            }
            // the hover patrol, round the post the logic above left it at (it strafes across the line to the player)
            if (e.Patrol != null) e.Patrol.Tick(h, e.Position, e.Awake, e.Brain.State == BotState.Telegraph, Chest - e.Center, _patrolBlocked);
            if (e.Beam != null && e.Beam.Active && e.Beam.Step(h, Chest, out bool hit))
            {
                CountShot(e);
                if (hit && SeesNow(e, out _)) HitPlayer(e.Def.attackDamage);
            }
        }

        void TickMines(FightEnemy e, float h, bool laying)
        {
            _blasts.Clear();
            e.Mines.Tick(h, e.MineRail ?? e.Rail, laying, Chest, _blasts);
            foreach (float d in _blasts) HitPlayer(d);
        }

        void FireBolt(FightEnemy e, Vector3 target)
        {
            var d = e.Def;
            Vector3 from = e.Center;
            Vector3 aim;
            if (d.aimLagSeconds > 0f) aim = Aim.LaggedPoint(_trail, Time, d.aimLagSeconds, Chest) + (target - Chest) - from;   // where it was, no lead
            else
            {
                float lead = d.aim == AimModel.LeadsYou ? _difficulty.leadFactor : 0f;
                aim = Aim.LeadPoint(from, target, _player.Velocity, d.projectileSpeed, lead) - from;
            }
            float len = aim.Length();
            if (!(len > 1e-4f)) return;
            CountShot(e);
            _bolts.Add(new Bolt { pos = from, vel = Spread(aim / len, d.aimSpreadDeg) * d.projectileSpeed, radius = d.projectileRadius, damage = d.attackDamage });
            e.Patrol?.OnVolley();
        }

        void FloorUnit(FightEnemy e, float h)
        {
            var d = e.Def;
            Vector3 feet = _player.Position;
            float dx = feet.X - e.Position.X, dz = feet.Z - e.Position.Z;
            float flat = MathF.Sqrt(dx * dx + dz * dz);
            Vector3 nozzle = e.Position + new Vector3(0f, JetHeight, 0f), ankle = new Vector3(feet.X, JetHeight, feet.Z);
            bool reach = e.Awake && flat <= d.sweepRadius && feet.Y > e.Position.Y - FloorUnitDropReach && Clear(nozzle, ankle);
            e.Brain.Tick(h, reach, _cues);
            foreach (var c in _cues)
                if (c.kind == BotCueKind.Attack && reach)
                {
                    e.Sweeping = true;
                    e.SweepTime = 0f;
                    e.SweepYaw = MathF.Atan2(dx, dz) * (180f / MathF.PI);
                    e.sweepHit = false;
                    CountShot(e);
                }
            if (e.Sweeping)
            {
                if (!e.sweepHit && feet.Y > e.Position.Y - FloorUnitDropReach && AcidSweep.Touches(e.Position, e.SweepYaw, e.SweepTime, feet, e.Position.Y, d) && Clear(nozzle, ankle))
                {
                    e.sweepHit = true;
                    HitPlayer(d.attackDamage);
                }
                e.SweepTime += h;
                if (e.SweepTime > d.sweepSeconds) e.Sweeping = false;
                return;
            }
            if (!e.Awake)
            {
                // asleep: it cleans round its post
                if (e.Cleaning != null)
                {
                    e.Cleaning.Tick(h, e.Home, _cleaningBlocked);
                    e.Position = e.Home + e.Cleaning.Offset;
                }
                return;
            }
            if (e.Brain.State == BotState.Telegraph) return;
            // close enough (half its sweep radius): it wants the player inside its arc, not under it, so it stops driving
            if (flat >= d.sweepRadius * 0.5f) Drive(e, new Vector2(feet.X, feet.Z), d.moveSpeed * h);
            Separate(e, h);
        }

        readonly List<Vector2> _sepAt = new List<Vector2>();
        readonly List<float> _sepR = new List<float>();

        /// <summary>The floor-care units keep apart (<see cref="FloorSeparation"/>): an awake unit free to move steps out of any
        /// other live unit it overlaps, never into a box or out of the hall (a blocked push is tried along each axis alone).</summary>
        void Separate(FightEnemy e, float h)
        {
            _sepAt.Clear();
            _sepR.Clear();
            foreach (var o in _enemies)
            {
                if (o == e || o.Wrecked || !(o.Def.moveSpeed > 0f)) continue;
                _sepAt.Add(new Vector2(o.Position.X, o.Position.Z));
                _sepR.Add(o.Def.bodyRadius);
            }
            var p = new Vector2(e.Position.X, e.Position.Z);
            float r = e.Def.bodyRadius;
            Vector2 push = FloorSeparation.Push(p, r, _sepAt, _sepR, e.Def.moveSpeed * h, e.Index);
            if (push.LengthSquared() < 1e-10f) return;
            if (!FloorFree(p + push, r))
            {
                if (FloorFree(p + new Vector2(push.X, 0f), r)) push = new Vector2(push.X, 0f);
                else if (FloorFree(p + new Vector2(0f, push.Y), r)) push = new Vector2(0f, push.Y);
                else return;
            }
            Vector2 q = p + push;
            e.Position = new Vector3(q.X, e.Position.Y, q.Y);
        }

        /// <summary>A floor unit of radius <paramref name="r"/> at <paramref name="q"/> stands inside the hall and clear of every box.</summary>
        bool FloorFree(Vector2 q, float r)
        {
            float m = r + 0.05f;
            if (q.X < _area.minX + m || q.X > _area.maxX - m || q.Y < _area.minZ + m || q.Y > _area.maxZ - m) return false;
            foreach (var b in _floorCover)
                if (q.X > b.min.X - r && q.X < b.max.X + r && q.Y > b.min.Z - r && q.Y < b.max.Z + r) return false;
            return true;
        }

        /// <summary>Toward the target, round the cover: when a box stands on the straight line, it heads for the corner of that
        /// box (just outside it) that makes the shortest way round with a clear line on to the target, and keeps that choice
        /// until the line is clear; a box it still runs into is slid along. Kept inside the hall.</summary>
        void Drive(FightEnemy e, Vector2 target, float stepLen)
        {
            var p = new Vector2(e.Position.X, e.Position.Z);
            float r = e.Def.bodyRadius;
            int bi = FirstBlocking(p, target, r);
            Vector2 aim = target;
            if (bi >= 0)
            {
                if (e.detourBox != bi)
                {
                    e.detourBox = bi;
                    e.detourCorner = BestCorner(bi, p, target, r);
                }
                aim = Corner(_floorCover[bi], e.detourCorner, r + DetourMargin);
            }
            else e.detourBox = -1;
            Steer(e, p, aim, target, stepLen);
        }

        const float DetourMargin = 0.4f;

        static Vector2 Corner(CoverBox b, int i, float m) =>
            new Vector2((i & 1) == 0 ? b.min.X - m : b.max.X + m, (i & 2) == 0 ? b.min.Z - m : b.max.Z + m);

        int BestCorner(int bi, Vector2 p, Vector2 target, float r)
        {
            var b = _floorCover[bi];
            int best = 0;
            float bestLen = float.PositiveInfinity;
            for (int i = 0; i < 4; i++)
            {
                Vector2 c = Corner(b, i, r + DetourMargin);
                float len = Vector2.Distance(p, c) + Vector2.Distance(c, target);
                if (SegmentHitsRect(c, target, b, r)) len += 1000f;   // still behind the box from that corner
                if (SegmentHitsRect(p, c, b, r)) len += Math.Max(b.max.X - b.min.X, b.max.Z - b.min.Z);   // the far side: round an end first
                if (len < bestLen) { bestLen = len; best = i; }
            }
            return best;
        }

        int FirstBlocking(Vector2 p, Vector2 target, float r)
        {
            int best = -1;
            float bestD = float.PositiveInfinity;
            for (int i = 0; i < _floorCover.Length; i++)
            {
                if (!SegmentHitsRect(p, target, _floorCover[i], r)) continue;
                var b = _floorCover[i];
                float d = Vector2.Distance(p, new Vector2((b.min.X + b.max.X) * 0.5f, (b.min.Z + b.max.Z) * 0.5f));
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        /// <summary>The 2D segment against the box's footprint grown by <paramref name="r"/>.</summary>
        static bool SegmentHitsRect(Vector2 a, Vector2 b, CoverBox box, float r) =>
            CoverGeometry.SegmentHitsBox(new Vector3(a.X, 0f, a.Y), new Vector3(b.X, 0f, b.Y),
                new CoverBox { min = new Vector3(box.min.X - r, -1f, box.min.Z - r), max = new Vector3(box.max.X + r, 1f, box.max.Z + r) });

        /// <summary>One step toward <paramref name="aim"/>; a box in the way is slid along (the step loses its part into the face
        /// and keeps its speed along it; head-on, it turns toward the end that's shorter on the way to the target).</summary>
        void Steer(FightEnemy e, Vector2 p, Vector2 aim, Vector2 target, float stepLen)
        {
            Vector2 to = aim - p;
            float len = to.Length();
            if (!(len > 1e-4f)) return;
            Vector2 step = to / len * Math.Min(stepLen, len);
            float r = e.Def.bodyRadius;
            for (int pass = 0; pass < 2; pass++)
            {
                bool moved = false;
                foreach (var b in _floorCover)
                {
                    float x0 = b.min.X - r, x1 = b.max.X + r, z0 = b.min.Z - r, z1 = b.max.Z + r;
                    Vector2 n = p + step;
                    if (n.X <= x0 || n.X >= x1 || n.Y <= z0 || n.Y >= z1) continue;
                    bool xFace = p.X <= x0 || p.X >= x1;
                    Vector2 slid = xFace ? new Vector2(0f, step.Y) : new Vector2(step.X, 0f);
                    if (slid.Length() < 0.3f * stepLen)
                    {
                        // head-on: go round the end that's shorter on the way to the target
                        if (xFace)
                        {
                            float viaLo = MathF.Abs(p.Y - z0) + MathF.Abs(target.Y - z0), viaHi = MathF.Abs(p.Y - z1) + MathF.Abs(target.Y - z1);
                            slid = new Vector2(0f, viaHi < viaLo ? 1f : -1f);
                        }
                        else
                        {
                            float viaLo = MathF.Abs(p.X - x0) + MathF.Abs(target.X - x0), viaHi = MathF.Abs(p.X - x1) + MathF.Abs(target.X - x1);
                            slid = new Vector2(viaHi < viaLo ? 1f : -1f, 0f);
                        }
                    }
                    step = Vector2.Normalize(slid) * stepLen;
                    moved = true;
                }
                if (!moved) break;
            }
            Vector2 q = p + step;
            float m = r + 0.05f;
            q.X = Math.Max(_area.minX + m, Math.Min(_area.maxX - m, q.X));
            q.Y = Math.Max(_area.minZ + m, Math.Min(_area.maxZ - m, q.Y));
            foreach (var b in _floorCover)
                if (q.X > b.min.X - r && q.X < b.max.X + r && q.Y > b.min.Z - r && q.Y < b.max.Z + r) return;   // a corner it can't slide out of: it stalls this step
            e.Position = new Vector3(q.X, e.Position.Y, q.Y);
        }

        void FlyBolts(float h)
        {
            float r = _player.def.radius;
            Vector3 a = _player.Position + new Vector3(0f, r, 0f);
            Vector3 b = _player.Position + new Vector3(0f, MathF.Max(r, _player.Height - r), 0f);
            for (int i = _bolts.Count - 1; i >= 0; i--)
            {
                var p = _bolts[i];
                Vector3 from = p.pos;
                p.pos += p.vel * h;
                p.age += h;
                if (SegmentsCloserThan(from, p.pos, a, b, r + p.radius))
                {
                    // the player is hit unless cover stood between it and the point of contact
                    if (!CoverGeometry.BlockedThick(from, ClosestOn(from, p.pos, a, b), _shotCover, p.radius)) HitPlayer(p.damage);
                    _bolts.RemoveAt(i);
                    continue;
                }
                if (CoverGeometry.BlockedThick(from, p.pos, _shotCover, p.radius) || p.age > MaxBoltAge)
                {
                    _bolts.RemoveAt(i);
                    continue;
                }
                _bolts[i] = p;
            }
        }

        void CountShot(FightEnemy e)
        {
            _r.shotsAtPlayer++;
            _shotsByGroup[e.Group] = ShotsAtPlayerByGroup(e.Group) + 1;
        }

        /// <summary>
        /// The drone search (the reference rule; the Unity drone does exactly this). Only awake bots with a
        /// <see cref="EnemyDef.searchSpeed"/> search; they move at their own height, at that speed, and never through cover.
        /// <list type="number">
        /// <item>On the step it loses sight of the player, it records where the player is then as <see cref="FightEnemy.LastKnown"/>
        /// (the chest; the player has just stepped out of its sight). That is a step of perfect information: a few centimetres
        /// past the last seen point when sight breaks at cover, but the whole jump when it is lost to range or a teleport.</item>
        /// <item>If it has a clear line to that point from where it is (it lost the player to distance, not to cover), it holds
        /// and watches it.</item>
        /// <item>Otherwise it scans ring points around itself at 1.5, 3, 4.5, 6, 8 and 10 m (16 directions each) and takes, from the
        /// first ring that has any, the point that sees the last known position, can be reached in a straight line without
        /// touching cover, lies inside the hall and south of the mezzanine deck's edge, and is nearest the last known position.
        /// It commits to that point: no re-picking until it gets there (then it holds), sees the player again, or its memory
        /// runs out. No such point within 10 m: it holds.</item>
        /// <item>When <see cref="EnemyDef.rememberSeconds"/> have passed since it last saw the player, it goes home the way it came:
        /// back through each point it set out from (a leg is struck off only when reached, so a re-sight on the way loses
        /// nothing), then to its spawn, which is fixed (<see cref="FightEnemy.Home"/>); it holds there.</item>
        /// <item>Seeing the player again ends the search at once (the trail home is kept).</item>
        /// </list>
        /// </summary>
        void Search(FightEnemy e, float h, bool justLost)
        {
            if (justLost)
            {
                e.LastKnown = Chest;
                e.HasLastKnown = true;
                e.hasSearchGoal = false;
                e.Returning = false;
                if (!Clear(e.Position, e.LastKnown) && FindVantage(e, out e.searchGoal))
                {
                    e.hasSearchGoal = true;
                    e.trail.Add(e.Position);   // the way back
                }
            }
            if (!e.Returning && Time - e.lastSeenAt > e.Def.rememberSeconds)
            {
                e.Returning = true;
                e.hasSearchGoal = false;
            }
            if (e.Returning && !e.hasSearchGoal)
            {
                // the next leg home: the last point it set out from (taken off the trail only once reached), then home itself
                if (e.trail.Count > 0) { e.searchGoal = e.trail[e.trail.Count - 1]; e.hasSearchGoal = true; }
                else if (Vector3.Distance(e.Position, e.Home) > 1e-3f) { e.searchGoal = e.Home; e.hasSearchGoal = true; }
            }
            if (!e.hasSearchGoal) return;
            Vector3 to = e.searchGoal - e.Position;
            float len = to.Length();
            if (len < 1e-3f)
            {
                // there: hold, or, returning, this leg is done
                if (e.Returning && e.trail.Count > 0 && Vector3.Distance(e.searchGoal, e.trail[e.trail.Count - 1]) < 1e-3f) e.trail.RemoveAt(e.trail.Count - 1);
                e.hasSearchGoal = false;
                return;
            }
            Vector3 next = e.Position + to / len * Math.Min(len, e.Def.searchSpeed * h);
            // a blocked step (only possible if cover moved): it holds and tries the same leg again; no leg is ever skipped
            if (CoverGeometry.BlockedThick(e.Position, next, _cover, e.Def.bodyRadius)) { e.hasSearchGoal = false; return; }
            e.Position = next;
        }

        bool FindVantage(FightEnemy e, out Vector3 goal)
        {
            goal = e.Position;
            float r = e.Def.bodyRadius;
            foreach (float dist in SearchRings)
            {
                float best = float.PositiveInfinity;
                for (int k = 0; k < 16; k++)
                {
                    float a = k * (MathF.PI / 8f);
                    var p = e.Position + new Vector3(MathF.Sin(a) * dist, 0f, MathF.Cos(a) * dist);
                    if (p.X < _area.minX + 1f || p.X > _area.maxX - 1f || p.Z < _area.minZ + 1f || p.Z > _area.searchMaxZ) continue;
                    if (CoverGeometry.BlockedThick(p, p, _cover, r)) continue;   // inside a piece
                    float toLast = Vector3.Distance(p, e.LastKnown);
                    if (toLast >= best || toLast > e.Def.range || !Clear(p, e.LastKnown)) continue;
                    if (CoverGeometry.BlockedThick(e.Position, p, _cover, r)) continue;   // no straight way there
                    best = toLast;
                    goal = p;
                }
                if (!float.IsPositiveInfinity(best)) return true;
            }
            return false;
        }

        /// <summary>The search's ring radii (m).</summary>
        public static readonly float[] SearchRings = { 1.5f, 3f, 4.5f, 6f, 8f, 10f };

        void HitPlayer(float damage)
        {
            if (Armor.Wrecked || !(damage > 0f)) return;
            if (Invulnerable)
            {
                _r.hitsTaken++;
                return;
            }
            var hit = Armor.Damage(damage);
            _r.hitsTaken++;
            _r.damageTaken += hit.toPlating + hit.toChassis;
        }

        void StripWrecks()
        {
            Vector3 p = _player.Position;
            float r2 = _pd.stripRadius * _pd.stripRadius;
            foreach (var w in _wrecks)
            {
                if (!w.Wreck.Fresh(Time)) continue;
                float dx = w.Position.X - p.X, dz = w.Position.Z - p.Z;
                if (dx * dx + dz * dz <= r2 && p.Y - w.Position.Y < 2.5f) Armor.Strip(w.Wreck, Time, _difficulty.scrapScale);
            }
        }

        /// <summary>The direction tilted by a random error uniform over a cone of <paramref name="deg"/> degrees (as <see cref="CombatSim"/>).</summary>
        Vector3 Spread(Vector3 dir, float deg)
        {
            if (!(deg > 0f)) return dir;
            Vector3 side = Vector3.Normalize(Vector3.Cross(dir, MathF.Abs(dir.Y) < 0.99f ? Vector3.UnitY : Vector3.UnitX));
            Vector3 up = Vector3.Cross(side, dir);
            float angle = _rng.Range(0f, 2f * MathF.PI);
            float rr = MathF.Tan(deg * MathF.PI / 180f) * MathF.Sqrt(_rng.NextFloat());
            return Vector3.Normalize(dir + (side * MathF.Cos(angle) + up * MathF.Sin(angle)) * rr);
        }

        /// <summary>Whether segments p0-p1 and q0-q1 come within <paramref name="dist"/> of each other.</summary>
        public static bool SegmentsCloserThan(Vector3 p0, Vector3 p1, Vector3 q0, Vector3 q1, float dist)
        {
            ClosestPoints(p0, p1, q0, q1, out Vector3 a, out Vector3 b);
            return Vector3.DistanceSquared(a, b) <= dist * dist;
        }

        static Vector3 ClosestOn(Vector3 p0, Vector3 p1, Vector3 q0, Vector3 q1)
        {
            ClosestPoints(p0, p1, q0, q1, out Vector3 a, out _);
            return a;
        }

        static void ClosestPoints(Vector3 p0, Vector3 p1, Vector3 q0, Vector3 q1, out Vector3 onP, out Vector3 onQ)
        {
            Vector3 d1 = p1 - p0, d2 = q1 - q0, r = p0 - q0;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
            float s, t;
            if (a <= 1e-9f && e <= 1e-9f) { onP = p0; onQ = q0; return; }
            if (a <= 1e-9f) { s = 0f; t = Clamp01(f / e); }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= 1e-9f) { t = 0f; s = Clamp01(-c / a); }
                else
                {
                    float b = Vector3.Dot(d1, d2), denom = a * e - b * b;
                    s = denom > 1e-9f ? Clamp01((b * f - c * e) / denom) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f) { t = 0f; s = Clamp01(-c / a); }
                    else if (t > 1f) { t = 1f; s = Clamp01((b - c) / a); }
                }
            }
            onP = p0 + d1 * s;
            onQ = q0 + d2 * t;
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    /// <summary>
    /// Floor-care units keep apart (judge round 1: three units drove into one pile at the player's feet, faces and canisters
    /// inside each other). Each step, a unit free to move (awake, not telegraphing or sweeping) steps half-way out of every
    /// other live unit it overlaps: closer than the two radii plus <see cref="Gap"/> (flat), no faster than its own drive.
    /// Both units do the same in their turn, so a pair parts evenly; units bunched on the ring round the player spread along
    /// it. FightSim and Unity's SkylineBot run this same code (the cover check is each side's own).
    /// </summary>
    public static class FloorSeparation
    {
        /// <summary>The clear gap kept between two units' bodies (m).</summary>
        public const float Gap = 0.25f;

        /// <summary>The flat push (x, z) for a unit at <paramref name="p"/> of radius <paramref name="r"/> from the others at
        /// <paramref name="others"/> (radii <paramref name="radii"/>), at most <paramref name="maxStep"/> long. Two units on
        /// the same spot part along a direction set by <paramref name="salt"/> (deterministic).</summary>
        public static Vector2 Push(Vector2 p, float r, IReadOnlyList<Vector2> others, IReadOnlyList<float> radii, float maxStep, int salt)
        {
            if (others == null || !(maxStep > 0f)) return Vector2.Zero;
            Vector2 sum = Vector2.Zero;
            for (int i = 0; i < others.Count; i++)
            {
                float want = r + (radii != null && i < radii.Count ? radii[i] : r) + Gap;
                Vector2 d = p - others[i];
                float dist = d.Length();
                if (dist >= want) continue;
                Vector2 dir;
                if (dist > 1e-4f) dir = d / dist;
                else
                {
                    float a = (salt * 2.399963f + i * 0.7f) % (2f * MathF.PI);
                    dir = new Vector2(MathF.Sin(a), MathF.Cos(a));
                }
                sum += dir * ((want - dist) * 0.5f);
            }
            float len = sum.Length();
            if (len > maxStep) sum *= maxStep / len;
            return sum;
        }
    }
}

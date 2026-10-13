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
        /// <summary>Seconds to swing onto a new target before the first shot at it.</summary>
        public float retargetSeconds = 0.2f;
        /// <summary>A fresh wreck is stripped by passing within this many metres of it (on the floor plan).</summary>
        public float stripRadius = 1.5f;

        public float HitChance(float distance)
        {
            if (!(distance > nearDistance)) return hitChanceNear;
            if (distance >= farDistance || farDistance <= nearDistance) return hitChanceFar;
            float t = (distance - nearDistance) / (farDistance - nearDistance);
            return hitChanceNear + (hitChanceFar - hitChanceNear) * t;
        }
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
        public bool Wrecked => Body.Wrecked;
        /// <summary>It had the player in sight (and in range) at the end of the last step.</summary>
        public bool SeesPlayer { get; internal set; }
        // the floor-care unit's sweep
        public bool Sweeping { get; internal set; }
        public float SweepTime { get; internal set; }
        public float SweepYaw { get; internal set; }
        internal bool sweepHit;
        internal int detourBox = -1, detourCorner;
        // the spotlight's beam
        public TelegraphedShot Beam { get; internal set; }
        // the gondola
        public GondolaRail Rail { get; internal set; }
        public MineLayer Mines { get; internal set; }

        /// <summary>Where it is shot at and sees from: a floor-care unit's body sits 0.4 m above the floor.</summary>
        public Vector3 Center => Def.moveSpeed > 0f ? Position + new Vector3(0f, FightSim.FloorUnitCenter, 0f) : Position;
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
    /// <item>Drones and spotlights hold still; floor-care units drive straight at the player at their move speed and slide along
    /// a box face they run into (no path finding: a unit can stall in a concave corner between two boxes, and units pass
    /// through each other); gondolas ride their rails.</item>
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
        readonly CoverBox[] _cover;
        readonly CoverBox[] _floorCover;
        readonly DifficultyDef _difficulty;
        readonly FightPlayerDef _pd;
        readonly SkylineGun _gun;
        readonly Rng _rng;
        readonly List<BotCue> _cues = new List<BotCue>();
        readonly List<GunCue> _gunCues = new List<GunCue>();
        readonly List<float> _blasts = new List<float>();
        FightEnemy _target;
        float _readyAt;
        FightResult _r;
        bool _over;

        public MoveSim Player => _player;
        public PlayerArmor Armor { get; }
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
            float startYaw = 0f)
        {
            if (encounter?.spawns == null) throw new ArgumentException("an encounter with spawns is needed", nameof(encounter));
            if (world == null) throw new ArgumentNullException(nameof(world));
            _bot = bot ?? throw new ArgumentNullException(nameof(bot));
            _difficulty = difficulty ?? Difficulties.Normal();
            _pd = player ?? new FightPlayerDef();
            lookup = lookup ?? SkylineEnemies.Find;
            _cover = cover != null ? new List<CoverBox>(cover).ToArray() : new CoverBox[0];
            var floor = new List<CoverBox>();
            foreach (var b in _cover)
                if (b.min.Y <= 0.5f) floor.Add(b);
            _floorCover = floor.ToArray();
            for (int i = 0; i < _cover.Length; i++)
            {
                var b = _cover[i];
                var feet = new Vector3((b.min.X + b.max.X) * 0.5f, b.min.Y + 0.01f, (b.min.Z + b.max.Z) * 0.5f);
                if (world.CapsuleFits(feet, 0.3f, 1f))
                    throw new ArgumentException($"cover box {i} ({b.min} to {b.max}) is not solid in the move world: the player would run through it");
            }
            _rng = new Rng(seed);
            _player = new MoveSim(world, new MoveDef());
            _player.Teleport(start, startYaw, Vector3.Zero);
            Armor = new PlayerArmor(_pd.platingMax, _pd.chassisMax);
            _gun = new SkylineGun(gun ?? SkylineGuns.Writ());
            _gun.Equip(null);
            _gun.Tick(1f, false, false, null);   // the gun is out before the fight starts
            for (int i = 0; i < encounter.spawns.Length; i++)
            {
                var s = encounter.spawns[i];
                var def = lookup(s.enemy);
                if (def == null) throw new ArgumentException("unknown enemy " + s.enemy);
                var e = new FightEnemy
                {
                    Index = i, Def = def, Group = s.group, Body = new EnemyBody(def),
                    Brain = new BotBrain(def, seed * 7919UL + (ulong)i * 104729UL + 1UL), Position = s.position,
                };
                if (def.aim == AimModel.FiresAtWhereYouAre && def.projectileSpeed <= 0f)
                    e.Beam = new TelegraphedShot
                    {
                        telegraph = def.telegraphSeconds, lockBefore = def.beamLockBefore, followRate = def.beamFollowRate, hitRadius = def.beamHitRadius,
                    };
                if (def.layer == ThreatLayer.Walls)
                {
                    e.Rail = new GondolaRail(s.position, s.railEnd, def.railSpeed);
                    e.Mines = new MineLayer(def);
                }
                _enemies.Add(e);
            }
            _r = new FightResult { clearSeconds = -1f, wreckedAt = -1f, minPlating = Armor.Plating, enemies = _enemies.Count };
        }

        // ---- geometry the bots may use too

        public Vector3 Chest => _player.Position + new Vector3(0f, _player.Height * 0.5f, 0f);
        public Vector3 Eye => EyeAt(_player.Position);
        public Vector3 EyeAt(Vector3 feet) => feet + new Vector3(0f, _player.Height * (_player.def.eyeHeight / _player.def.standHeight), 0f);

        public bool Clear(Vector3 a, Vector3 b) => !CoverGeometry.Blocked(a, b, _cover);

        /// <summary>Whether <paramref name="e"/> would see a player standing with its feet at <paramref name="feet"/> (its chest or
        /// eye, within its range, nothing between).</summary>
        public bool CanSee(FightEnemy e, Vector3 feet)
        {
            Vector3 c = e.Center;
            Vector3 chest = feet + new Vector3(0f, _player.def.standHeight * 0.5f, 0f), eye = EyeAt(feet);
            return (Vector3.Distance(c, chest) <= e.Def.range && Clear(c, chest)) || (Vector3.Distance(c, eye) <= e.Def.range && Clear(c, eye));
        }

        /// <summary>Whether a player standing at <paramref name="feet"/> could shoot <paramref name="e"/> (eye to centre, within the gun's range).</summary>
        public bool CanShoot(Vector3 feet, FightEnemy e)
        {
            Vector3 eye = EyeAt(feet);
            return Vector3.Distance(eye, e.Center) <= _gun.Def.range && Clear(eye, e.Center);
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
            bool all = true;
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
                if (d >= bestD || d > _gun.Def.range || !Clear(eye, e.Center)) continue;
                best = e;
                bestD = d;
            }
            if (best != _target)
            {
                _target = best;
                _readyAt = Time + _pd.retargetSeconds;
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
                if (_target == null || _target.Wrecked) continue;
                float dist = Vector3.Distance(eye, _target.Center);
                if (!(_rng.NextFloat() < _pd.HitChance(dist))) continue;
                Hurt(_target, _gun.Def.damage * _gun.Def.FalloffScale(dist));
            }
        }

        void Hurt(FightEnemy e, float amount)
        {
            int plate = -1;
            for (int i = 0; i < e.Body.PlateCount; i++)
                if (e.Body.PlateOn(i)) { plate = i; break; }
            var d = e.Body.Damage(amount, plate);
            WakeGroup(e.Group);
            e.Brain.OnDamaged(null);
            if (d.wrecked)
            {
                e.Brain.OnWrecked(null);
                e.Sweeping = false;
                _wrecks.Add(new FightWreck
                {
                    Wreck = new Wreck(e.Def, Time), Position = new Vector3(e.Position.X, 0f, e.Position.Z), EnemyIndex = e.Index, Group = e.Group,
                });
            }
        }

        void WakeGroup(int group)
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
            bool sees = SeesNow(e, out Vector3 aimAt);
            e.SeesPlayer = sees;
            if (!e.Awake && sees) WakeGroup(e.Group);
            _cues.Clear();
            if (e.Def.layer == ThreatLayer.Walls)
            {
                e.Brain.Tick(h, e.Awake && sees, _cues);
                if (e.Awake)
                {
                    e.Rail.Tick(h);
                    e.Position = e.Rail.Position;
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
            foreach (var c in _cues)
            {
                if (e.Beam != null)
                {
                    if (c.kind == BotCueKind.State && c.state == BotState.Telegraph && sees) e.Beam.Start(Chest);
                }
                else if (c.kind == BotCueKind.Attack && sees) FireBolt(e, aimAt);
            }
            if (e.Beam != null && e.Beam.Active && e.Beam.Step(h, Chest, out bool hit))
            {
                _r.shotsAtPlayer++;
                if (hit && SeesNow(e, out _)) HitPlayer(e.Def.attackDamage);
            }
        }

        void TickMines(FightEnemy e, float h, bool laying)
        {
            _blasts.Clear();
            e.Mines.Tick(h, e.Rail, laying, Chest, _blasts);
            foreach (float d in _blasts) HitPlayer(d);
        }

        void FireBolt(FightEnemy e, Vector3 target)
        {
            var d = e.Def;
            Vector3 from = e.Center;
            float lead = d.aim == AimModel.LeadsYou ? _difficulty.leadFactor : 0f;
            Vector3 aim = Aim.LeadPoint(from, target, _player.Velocity, d.projectileSpeed, lead) - from;
            float len = aim.Length();
            if (!(len > 1e-4f)) return;
            _r.shotsAtPlayer++;
            _bolts.Add(new Bolt { pos = from, vel = Spread(aim / len, d.aimSpreadDeg) * d.projectileSpeed, radius = d.projectileRadius, damage = d.attackDamage });
        }

        void FloorUnit(FightEnemy e, float h)
        {
            var d = e.Def;
            Vector3 feet = _player.Position;
            float dx = feet.X - e.Position.X, dz = feet.Z - e.Position.Z;
            float flat = MathF.Sqrt(dx * dx + dz * dz);
            Vector3 nozzle = e.Position + new Vector3(0f, JetHeight, 0f), ankle = new Vector3(feet.X, JetHeight, feet.Z);
            bool reach = e.Awake && flat <= d.sweepRadius && Clear(nozzle, ankle);
            e.Brain.Tick(h, reach, _cues);
            foreach (var c in _cues)
                if (c.kind == BotCueKind.Attack && reach)
                {
                    e.Sweeping = true;
                    e.SweepTime = 0f;
                    e.SweepYaw = MathF.Atan2(dx, dz) * (180f / MathF.PI);
                    e.sweepHit = false;
                    _r.shotsAtPlayer++;
                }
            if (e.Sweeping)
            {
                if (!e.sweepHit && AcidSweep.Touches(e.Position, e.SweepYaw, e.SweepTime, feet, e.Position.Y, d) && Clear(nozzle, ankle))
                {
                    e.sweepHit = true;
                    HitPlayer(d.attackDamage);
                }
                e.SweepTime += h;
                if (e.SweepTime > d.sweepSeconds) e.Sweeping = false;
                return;
            }
            if (!e.Awake || e.Brain.State == BotState.Telegraph) return;
            if (flat < d.sweepRadius * 0.5f) return;   // close enough: it wants the player inside its arc, not under it
            Drive(e, new Vector2(feet.X, feet.Z), d.moveSpeed * h);
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
            q.X = Math.Max(GalleriaArena.HallMinX + m, Math.Min(GalleriaArena.HallMaxX - m, q.X));
            q.Y = Math.Max(GalleriaArena.HallMinZ + m, Math.Min(GalleriaArena.HallMaxZ - m, q.Y));
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
                    if (!CoverGeometry.BlockedThick(from, ClosestOn(from, p.pos, a, b), _cover, p.radius)) HitPlayer(p.damage);
                    _bolts.RemoveAt(i);
                    continue;
                }
                if (CoverGeometry.BlockedThick(from, p.pos, _cover, p.radius) || p.age > MaxBoltAge)
                {
                    _bolts.RemoveAt(i);
                    continue;
                }
                _bolts[i] = p;
            }
        }

        void HitPlayer(float damage)
        {
            if (Armor.Wrecked || !(damage > 0f)) return;
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

    /// <summary>Stands where it is and shoots back (the sim aims for it): the "standing still" check.</summary>
    public sealed class StandAndShootBot : IFightBot
    {
        public FightInput Next(FightSim fight, float h) => new FightInput { move = new MoveInput { yaw = fight.Player.Yaw } };
    }

    /// <summary>One stop of a skilled line: walk the approach, then hide and peek until the named groups are wrecked, then
    /// strip their fresh wrecks. Points are (x, z) in world metres.</summary>
    public sealed class FightStation
    {
        public string name = "";
        public Vector2[] approach = new Vector2[0];
        /// <summary>Where it stands out of the groups' sight between peeks.</summary>
        public Vector2 hide;
        /// <summary>Where it steps to shoot; the first one that has a shot at a living member of the groups is used. On a
        /// strafe station, the two ends of the lane it runs.</summary>
        public Vector2[] peeks = new Vector2[0];
        public int[] groups = new int[0];
        /// <summary>A strafe station (against beams): no hiding; it runs back and forth between the first two peek points,
        /// shooting, and turns round whenever a beam locks on it, and grabs a fresh wreck that falls near its lane.</summary>
        public bool strafe;
    }

    /// <summary>A skilled line through one encounter (Task 11b): the stations in order, and how long it peeks and hides.</summary>
    public sealed class FightPlan
    {
        public string encounter = "";
        public FightStation[] stations = new FightStation[0];
        /// <summary>Seconds in an enemy's sight per peek before ducking back (a drone's alert plus its telegraph is about 1 s).</summary>
        public float peekSeconds = 1f;
        /// <summary>Seconds behind cover between peeks: long enough for the drones to lose track (their 3 s) and to reload.</summary>
        public float hideSeconds = 3.2f;
        /// <summary>Plating lost in one peek that sends it back to cover early.</summary>
        public float duckDamage = 10f;
    }

    /// <summary>
    /// The skilled line (Task 11b, the creator's description): one group at a time from behind cover. At each station it
    /// waits behind cover, steps out to a peek point, shoots while the drones are still waking (alert plus telegraph), ducks
    /// back before they settle, reloads in cover, and repeats; when the station's groups are wrecked it strips their fresh
    /// wrecks and moves on. On a strafe station (the spotlights) it runs a lane instead and turns whenever a beam locks.
    /// Anywhere, a beam locking on it makes it change direction for a moment, and it jumps every acid sweep it sees coming
    /// (it reads the floor-care unit's telegraph and the jet's angle, as a player does). After the last station it walks
    /// toward anything still standing.
    /// <para>With <c>useCover = false</c> it is the negative control: the same route and peek points, but it never hides,
    /// never strafes and never dodges a beam (it still jumps the acid).</para>
    /// </summary>
    public sealed class CoverFighterBot : IFightBot
    {
        public enum Phase { Approach, Hide, Peek, Strip, Hunt }

        /// <summary>Seconds it changes direction for when a beam locks on it.</summary>
        public const float DodgeSeconds = 0.3f;

        readonly FightPlan _plan;
        readonly bool _useCover;
        int _station, _wp, _peek, _lane;
        float _timer, _exposed, _platingAtPeek, _idle, _dodgeUntil = -1f;
        Vector2 _dodgeDir;
        bool _started;
        bool[] _locked = new bool[0];

        public Phase Current { get; private set; }
        public int Station => _station;

        public CoverFighterBot(FightPlan plan, bool useCover = true)
        {
            _plan = plan ?? throw new ArgumentNullException(nameof(plan));
            _useCover = useCover;
        }

        FightStation St => _station < _plan.stations.Length ? _plan.stations[_station] : null;

        bool InGroups(int group)
        {
            var s = St;
            if (s?.groups == null) return false;
            foreach (int g in s.groups) if (g == group) return true;
            return false;
        }

        bool StationClear(FightSim f)
        {
            foreach (var e in f.Enemies) if (!e.Wrecked && InGroups(e.Group)) return false;
            return true;
        }

        void Begin(int station)
        {
            _station = station;
            _wp = 0;
            Current = St != null ? Phase.Approach : Phase.Hunt;
        }

        /// <summary>True on the step a beam that sees the player locks.</summary>
        bool BeamLocked(FightSim f)
        {
            if (_locked.Length != f.Enemies.Count) _locked = new bool[f.Enemies.Count];
            bool any = false;
            for (int i = 0; i < f.Enemies.Count; i++)
            {
                var e = f.Enemies[i];
                bool now = e.Beam != null && !e.Wrecked && e.Beam.Active && e.Beam.Locked;
                if (now && !_locked[i] && e.SeesPlayer) any = true;
                _locked[i] = now;
            }
            return any;
        }

        public FightInput Next(FightSim f, float h)
        {
            if (!_started)
            {
                _started = true;
                Begin(0);
            }
            var input = new FightInput();
            var p = f.Player.Position;
            var me = new Vector2(p.X, p.Z);
            Vector2 goal = me;
            int mag = f.Gun.Def.weapon.magazineSize;
            bool locked = BeamLocked(f) && _useCover;
            bool strafing = false;
            for (int guard = 0; guard < 4; guard++)
            {
                var st = St;
                if (Current == Phase.Approach)
                {
                    if (st == null) { Current = Phase.Hunt; continue; }
                    if (_wp < st.approach.Length)
                    {
                        goal = st.approach[_wp];
                        if (Vector2.Distance(me, goal) < 0.8f) { _wp++; continue; }
                        break;
                    }
                    goal = st.hide;
                    if (Vector2.Distance(me, goal) < 0.5f || !_useCover || st.strafe)
                    {
                        Current = Phase.Hide;
                        _timer = _plan.hideSeconds;   // arriving unseen: it may peek at once
                        continue;
                    }
                    break;
                }
                if (Current == Phase.Hide)
                {
                    if (StationClear(f)) { Current = Phase.Strip; continue; }
                    goal = st.hide;
                    _timer += h;
                    input.reload = f.Gun.Ammo < mag && f.Target == null;
                    bool ready = !_useCover || st.strafe
                        || (_timer >= _plan.hideSeconds && Vector2.Distance(me, goal) < 0.6f && f.Gun.Ammo == mag && !f.Gun.State.IsReloading);
                    if (ready)
                    {
                        int pick = st.strafe ? 0 : PickPeek(f, st, -1);
                        if (pick >= 0)
                        {
                            _peek = pick;
                            _lane = 0;
                            Current = Phase.Peek;
                            _exposed = 0f;
                            _idle = 0f;
                            _platingAtPeek = f.Armor.Plating;
                            continue;
                        }
                    }
                    break;
                }
                if (Current == Phase.Peek)
                {
                    if (StationClear(f)) { Current = Phase.Strip; continue; }
                    if (st.strafe && _useCover && st.peeks.Length >= 2)
                    {
                        strafing = true;
                        goal = st.peeks[_lane];
                        if (Vector2.Distance(me, goal) < 0.6f || locked) _lane = 1 - _lane;
                        goal = st.peeks[_lane];
                        // a fresh wreck near the lane is worth a detour while the plating has room
                        var w = NearestFresh(f, me, false);
                        if (w != null && f.Armor.Plating < f.Armor.PlatingMax - 5f && Vector2.Distance(me, Flat(w.Position)) < 6f) goal = Flat(w.Position);
                        break;
                    }
                    goal = st.peeks[_peek];
                    bool seen = false;
                    foreach (var e in f.Enemies) if (!e.Wrecked && e.SeesPlayer && e.Def.moveSpeed <= 0f) { seen = true; break; }
                    if (seen) _exposed += h;
                    bool there = Vector2.Distance(me, goal) < 0.6f;
                    _idle = there && f.Target == null ? _idle + h : 0f;
                    if (_useCover && (_exposed >= _plan.peekSeconds || f.Gun.Ammo == 0 || _platingAtPeek - f.Armor.Plating >= _plan.duckDamage))
                    {
                        Current = Phase.Hide;
                        _timer = 0f;
                        continue;
                    }
                    if (_idle >= 0.4f)
                    {
                        // nothing to shoot from here: the next peek point that has a shot, else back to cover
                        int pick = PickPeek(f, st, _peek);
                        _idle = 0f;
                        if (pick >= 0) _peek = pick;
                        else if (_useCover)
                        {
                            Current = Phase.Hide;
                            _timer = _plan.hideSeconds - 0.5f;
                            continue;
                        }
                    }
                    break;
                }
                if (Current == Phase.Strip)
                {
                    var best = NearestFresh(f, me, true);
                    if (best != null) { goal = Flat(best.Position); break; }
                    Begin(_station + 1);
                    continue;
                }
                // Hunt: walk toward the nearest bot still standing until there's something to shoot
                FightEnemy near = null;
                float nd = float.PositiveInfinity;
                foreach (var e in f.Enemies)
                {
                    if (e.Wrecked) continue;
                    float d = Vector2.Distance(me, Flat(e.Position));
                    if (d < nd) { nd = d; near = e; }
                }
                if (near != null && f.Target == null) goal = Flat(near.Position);
                break;
            }
            if (locked && !strafing)
            {
                // a beam has locked: change direction for a moment (reverse the run; from a standstill, step sideways)
                var v = new Vector2(f.Player.Velocity.X, f.Player.Velocity.Z);
                _dodgeDir = v.Length() > 1f ? -Vector2.Normalize(v) : new Vector2(1f, 0f);
                _dodgeUntil = f.Time + DodgeSeconds;
            }
            input.move = f.Time < _dodgeUntil && !strafing ? MoveTo(f, me, me + _dodgeDir * 5f) : MoveTo(f, me, goal);
            JumpTheAcid(f, ref input.move);
            return input;
        }

        static Vector2 Flat(Vector3 v) => new Vector2(v.X, v.Z);

        FightWreck NearestFresh(FightSim f, Vector2 me, bool stationOnly)
        {
            FightWreck best = null;
            float bestD = float.PositiveInfinity;
            foreach (var w in f.Wrecks)
            {
                if (!w.Wreck.Fresh(f.Time) || (stationOnly && !InGroups(w.Group))) continue;
                float d = Vector2.Distance(me, Flat(w.Position));
                if (d < bestD) { bestD = d; best = w; }
            }
            return best;
        }

        /// <summary>The first peek point after <paramref name="after"/> (wrapping) with a shot at a living member of the station's groups.</summary>
        int PickPeek(FightSim f, FightStation st, int after)
        {
            int n = st.peeks.Length;
            for (int k = 1; k <= n; k++)
            {
                int i = ((after < 0 ? -1 : after) + k + n) % n;
                if (after >= 0 && i == after) continue;
                var feet = new Vector3(st.peeks[i].X, 0f, st.peeks[i].Y);
                foreach (var e in f.Enemies)
                    if (!e.Wrecked && InGroups(e.Group) && f.CanShoot(feet, e)) return i;
            }
            return -1;
        }

        static MoveInput MoveTo(FightSim f, Vector2 me, Vector2 goal)
        {
            Vector2 d = goal - me;
            float dist = d.Length();
            if (dist < 0.15f) return new MoveInput { yaw = f.Player.Yaw };
            float yaw = MathF.Atan2(d.X, d.Y) * (180f / MathF.PI);
            return new MoveInput { move = new Vector2(0f, Math.Min(1f, dist / 1.2f)), yaw = yaw, sprintHeld = dist > 2.5f };
        }

        /// <summary>Jumps so its feet are above the jet as it passes: from each sweeping or telegraphing floor-care unit in reach,
        /// when the jet will cross its bearing; a double jump bridges two sweeps in a row.</summary>
        static void JumpTheAcid(FightSim f, ref MoveInput m)
        {
            var p = f.Player.Position;
            float now = f.Time, soonest = float.PositiveInfinity, latest = float.NegativeInfinity;
            foreach (var e in f.Enemies)
            {
                var d = e.Def;
                if (e.Wrecked || d.moveSpeed <= 0f) continue;
                float dx = p.X - e.Position.X, dz = p.Z - e.Position.Z;
                float flat = MathF.Sqrt(dx * dx + dz * dz);
                if (flat > d.sweepRadius + 1.5f) continue;
                float bearing = MathF.Atan2(dx, dz) * (180f / MathF.PI);
                float start, yaw;
                if (e.Sweeping) { start = now - e.SweepTime; yaw = e.SweepYaw; }
                else if (e.Brain.State == BotState.Telegraph) { start = now + Math.Max(0f, d.telegraphSeconds - e.Brain.StateTime); yaw = bearing; }
                else continue;
                float ws, we;
                if (flat < 0.8f) { ws = start; we = start + d.sweepSeconds; }
                else
                {
                    float rel = (bearing - (yaw - d.sweepArcDeg * 0.5f)) % 360f;
                    if (rel < 0f) rel += 360f;
                    float half = d.sweepWidthDeg * 0.5f + 10f;
                    if (rel > d.sweepArcDeg + half && rel < 360f - half) continue;   // the jet never passes this bearing
                    if (rel > 180f) rel -= 360f;
                    float tc = start + rel / d.sweepArcDeg * d.sweepSeconds, w = half / d.sweepArcDeg * d.sweepSeconds;
                    ws = tc - w;
                    we = tc + w;
                }
                if (we < now) continue;
                soonest = Math.Min(soonest, ws);
                latest = Math.Max(latest, we);
            }
            if (float.IsPositiveInfinity(soonest)) return;
            if (f.Player.Grounded)
            {
                if (soonest - now <= 0.1f) { m.jumpPressed = true; m.jumpHeld = true; }
            }
            else if (f.Player.DoubleJumpAvailable && f.Player.Velocity.Y < 0f && p.Y < 0.75f && latest > now + 0.05f)
            {
                m.jumpPressed = true;
                m.jumpHeld = true;
            }
        }
    }

    /// <summary>The skilled lines through M1's three main-hall fights (world metres; see <see cref="CoverFighterBot"/>).</summary>
    public static class FightPlans
    {
        static Vector2 P(float x, float z) => new Vector2(x, z);

        public static FightPlan For(string encounterId)
        {
            switch (encounterId)
            {
                case "food-court": return FoodCourt();
                case "skylight": return Skylight();
                case "facade": return Facade();
                default: return null;
            }
        }

        /// <summary>Centre group first from behind the south kiosk; then the west and east rooms, each peeked from the centre
        /// room around a counter run's north end (the centre group is wrecked by then, and the other room's drones can't see
        /// past the far counter).</summary>
        public static FightPlan FoodCourt() => new FightPlan
        {
            encounter = "food-court",
            stations = new[]
            {
                new FightStation { name = "centre group, from behind the south kiosk", hide = P(0f, -10.6f),
                    peeks = new[] { P(3f, -10.6f), P(-3f, -10.6f), P(3.2f, -12f), P(-3.2f, -12f) }, groups = new[] { 2 } },
                new FightStation { name = "west group, round the west counter's north end", approach = new[] { P(-6f, -3f) }, hide = P(-9.4f, 1.5f),
                    peeks = new[] { P(-9.6f, 5.4f), P(-11f, 5.6f) }, groups = new[] { 0 } },
                new FightStation { name = "east group, round the east counter's north end", approach = new[] { P(-12.5f, -11.5f), P(0f, -11.5f), P(6f, -3f) }, hide = P(9.4f, 1.5f),
                    peeks = new[] { P(9.6f, 5.4f), P(11f, 5.6f) }, groups = new[] { 1 } },
            },
        };

        /// <summary>Each spotlight from the room under it, running a lane and turning whenever its beam locks; the floor-care
        /// units come to the lane and are shot and jumped there.</summary>
        public static FightPlan Skylight() => new FightPlan
        {
            encounter = "skylight",
            stations = new[]
            {
                new FightStation { name = "west room lane", approach = new[] { P(-7f, -14f), P(-15f, -11f) }, strafe = true,
                    peeks = new[] { P(-18f, -9f), P(-18f, -2f) }, groups = new[] { 0 } },
                new FightStation { name = "east room lane", approach = new[] { P(-7f, -14f), P(7f, -14f), P(15f, -11f) }, strafe = true,
                    peeks = new[] { P(18f, -9f), P(18f, -2f) }, groups = new[] { 1 } },
            },
        };

        /// <summary>From the centre room (out of both groups' sight), each room's drones are peeked round its counter run's south
        /// end; then the gondolas are shot from the floor (it never touches a wall).</summary>
        public static FightPlan Facade() => new FightPlan
        {
            encounter = "facade",
            stations = new[]
            {
                new FightStation { name = "west group, round the west counter's south end", approach = new[] { P(-4f, -11f) }, hide = P(-9.4f, -8.5f),
                    peeks = new[] { P(-10.2f, -11.6f), P(-11.6f, -11.8f) }, groups = new[] { 0 } },
                new FightStation { name = "east group, round the east counter's south end", approach = new[] { P(0f, -11f) }, hide = P(9.4f, -8.5f),
                    peeks = new[] { P(10.2f, -11.6f), P(11.6f, -11.8f) }, groups = new[] { 1 } },
            },
        };
    }

    /// <summary>Drives a <see cref="FightSim"/> with one of the dodge harness's lane runners (<see cref="StraightLineBot"/>,
    /// <see cref="JinkingBot"/>); the sim shoots back for it as for any bot.</summary>
    public sealed class LaneRunnerBot : IFightBot
    {
        readonly IPlayerBot _runner;

        public LaneRunnerBot(IPlayerBot runner) { _runner = runner ?? throw new ArgumentNullException(nameof(runner)); }

        public FightInput Next(FightSim fight, float h) => new FightInput { move = _runner.Next(fight.Player, fight.Time, h) };
    }

    /// <summary>The dodge band measured in a real fight (the creator, 2026-10-13: every balance check simulates the player
    /// shooting back): the straight-line and jinking runners each fight the same awake shooters in the open, firing the Writ,
    /// until the shooters are wrecked, the Unit is, or the span ends.</summary>
    public struct FightDodgeResult
    {
        /// <summary>Hits taken per second of exposure (the fight's length: while any shooter stands), pooled over the seeds.</summary>
        public float straightRate, jinkRate;
        public float straightHitsMean, jinkHitsMean, straightSecondsMean, jinkSecondsMean;
        public int straightZeroSeeds, jinkZeroSeeds, seeds;
        public float Ratio => jinkRate > 0f ? straightRate / jinkRate : float.PositiveInfinity;
    }

    public static class FightDodge
    {
        /// <summary>The distance (m, on the floor plan) from one group's centre to the middle of the runners' lane.</summary>
        public const float GroupStandOff = 12f;

        /// <summary>The encounter's aimed shooters (drones and spotlights; optionally only one group's) moved into the fight's
        /// frame, where the lane runners run (their lanes go 15 m either way along z about the origin). For the whole encounter the
        /// origin is its anchor (the hall's entry); for one group it is a point <see cref="GroupStandOff"/> m from the group's centre
        /// toward the hall's middle, so the group is in the runners' face (from the entry a side group is 20-38 m away and lands
        /// one or two hits a fight: too few to measure).</summary>
        public static EncounterDef Aimed(EncounterDef e, Func<string, EnemyDef> lookup = null, int group = -1)
        {
            lookup = lookup ?? SkylineEnemies.Find;
            Vector3 origin = e.anchor;
            if (group >= 0)
            {
                Vector3 sum = Vector3.Zero;
                int n = 0;
                foreach (var s in e.spawns) if (s.group == group) { sum += s.position; n++; }
                if (n > 0)
                {
                    var c = new Vector2(sum.X / n, sum.Z / n);
                    var toMiddle = c.Length() > 1e-3f ? -Vector2.Normalize(c) : new Vector2(0f, -1f);
                    var o = c + toMiddle * GroupStandOff;
                    origin = new Vector3(o.X, 0f, o.Y);
                }
            }
            var list = new List<Spawn>();
            foreach (var s in e.spawns)
            {
                var d = lookup(s.enemy);
                if (d == null || (d.aim != AimModel.LeadsYou && d.aim != AimModel.FiresAtWhereYouAre)) continue;
                if (group >= 0 && s.group != group) continue;
                list.Add(new Spawn(s.enemy, s.position - origin, 0));   // one group: awake together from the start
            }
            return new EncounterDef { id = e.id + (group >= 0 ? "/group " + group : "/aimed"), anchor = Vector3.Zero, spawns = list.ToArray() };
        }

        /// <summary>Both runners against <paramref name="aimed"/> (in the fight's frame) over seeds <paramref name="seed"/>..+<paramref name="seeds"/>-1,
        /// in an open <paramref name="makeWorld"/> with no cover. The seed drives the jinker's reversals, the shooters' aim error
        /// and the Writ's hit rolls.</summary>
        public static FightDodgeResult Run(EncounterDef aimed, Func<IMoveWorld> makeWorld, float span, ulong seed, int seeds,
            DifficultyDef difficulty = null, Func<string, EnemyDef> lookup = null)
        {
            if (seeds < 1) seeds = 1;
            var r = new FightDodgeResult { seeds = seeds };
            float sHits = 0f, jHits = 0f, sSec = 0f, jSec = 0f;
            for (int i = 0; i < seeds; i++)
            {
                ulong s = seed + (ulong)i;
                var a = new FightSim(aimed, makeWorld(), new LaneRunnerBot(new StraightLineBot()), null, Vector3.Zero, s, difficulty, lookup).Run(span);
                var b = new FightSim(aimed, makeWorld(), new LaneRunnerBot(new JinkingBot(s)), null, Vector3.Zero, s, difficulty, lookup).Run(span);
                sHits += a.hitsTaken; sSec += a.seconds; if (a.hitsTaken == 0) r.straightZeroSeeds++;
                jHits += b.hitsTaken; jSec += b.seconds; if (b.hitsTaken == 0) r.jinkZeroSeeds++;
            }
            r.straightHitsMean = sHits / seeds; r.jinkHitsMean = jHits / seeds;
            r.straightSecondsMean = sSec / seeds; r.jinkSecondsMean = jSec / seeds;
            r.straightRate = sSec > 0f ? sHits / sSec : 0f;
            r.jinkRate = jSec > 0f ? jHits / jSec : 0f;
            return r;
        }
    }
}

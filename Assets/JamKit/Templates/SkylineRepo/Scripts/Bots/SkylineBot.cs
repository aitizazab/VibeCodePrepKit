using System;
using System.Collections.Generic;
using JamKit.Skyline;
using UnityEngine;
using NVec3 = System.Numerics.Vector3;
using Object = UnityEngine.Object;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// One of CONCIERGE's bots in Unity (docs/SKYLINE-PLAN-M1.md Task 15). The pure code decides; this draws, sounds and
    /// moves it. Task 11b's headless <see cref="FightSim"/> is the behaviour reference, and this follows it step for step on
    /// one clock (<c>Time.deltaTime</c>, shared by the <see cref="BotBrain"/>, the spotlight's <see cref="TelegraphedShot"/>,
    /// the acid sweep, the rail and the mines):
    /// <list type="bullet">
    /// <item><b>Sight.</b> Within the def's range and a clear line from the bot's centre to the target's chest, or to a point
    /// 0.35 m above it (the eye; FightSim tries the chest, then the eye). Only cover blocks sight: bots, shed parts and
    /// triggers don't. An inactive target is never seen.</item>
    /// <item><b>Groups.</b> Every bot has an <see cref="group"/>; a group wakes when any member sees its target or is hurt
    /// (<see cref="Wake"/>, logged <c>SKYLINE_BOT group &lt;n&gt; woke</c>). A negative group is a bot alone. The brain ticks
    /// with "sees" only while awake, as FightSim. Groups are scene-wide numbers: give each encounter its own.</item>
    /// <item><b>Memory and jitter</b> live in BotBrain (the def's <c>rememberSeconds</c>: a re-sight inside it skips the alert
    /// after a seeded 0-0.15 s beat; a fresh alert waits the def's alert time plus a seeded 0-0.4 s), so a group never fires in
    /// lockstep, exactly as in FightSim.</item>
    /// <item><b>Drone.</b> Holds its spawn point at the spawn height (the encounter's), drifting in a gentle 1.5 m
    /// figure-eight only while idle or holding. Awake and out of sight, it searches by FightSim's documented rule
    /// (<see cref="Search"/>: last-known point; hold if it has a clear line to it; else the nearest vantage on the rings within
    /// 10 m, committed to; home the way it came when its memory runs out, a leg struck off only on arrival; Home fixed) at the
    /// def's <c>searchSpeed</c>. Fires a <see cref="SkylineBolt"/> at <see cref="Aim.LeadPoint"/> with Normal's lead factor,
    /// tilted by a seeded error over a cone of the def's <c>aimSpreadDeg</c>.</item>
    /// <item><b>Floor-care unit.</b> Drives at the def's <c>moveSpeed</c> toward the target round the cover (FightSim's
    /// detour-to-the-best-corner of a blocking collider's bounds, and a slide along a face it still runs into), stops half its
    /// sweep radius short, never moves while telegraphing or sweeping; sweeps (<see cref="SkylineAcid"/>) when the target's
    /// feet are within the sweep radius with a clear jet, damaging once per sweep; feet above <c>ankleHeight</c> are safe.</item>
    /// <item><b>Spotlight.</b> Turns only. Starts its beam on entering Telegraph (if it sees), steps it on the brain's clock (the
    /// first step is the state time the brain carried in, so the lock and the shot line up with the brain's states); the
    /// shot lands with the beam's hit and a clear sight now.</item>
    /// <item><b>Gondola.</b> Rides its rail (<see cref="GondolaRail"/>, start to <c>railEnd</c>) while awake and lays wall
    /// mines (<see cref="MineLayer"/>, <see cref="WallMine"/>) by the pure rules; the mines live on after a wreck. Its brain
    /// still telegraphs and "attacks" (a squeegee slap, no damage: FightSim's gondola ignores its attack cues).</item>
    /// </list>
    /// Damage feedback follows the creator's rules: no numbers, no bars; plates and body flash white (the shot's chip comes
    /// from the shooter); only a Writ spot (<see cref="SkylineSpotMarker"/>) glows and sparks on a live bot; a fresh wreck
    /// sparks (<see cref="SkylineWreck"/>).
    /// </summary>
    public sealed class SkylineBot : MonoBehaviour, ISkylineHittable, ISkylineAimable
    {
        // ------------------------------------------------------------------ statics

        /// <summary>The bots not yet wrecked, in spawn order.</summary>
        public static readonly List<SkylineBot> Live = new List<SkylineBot>();
        /// <summary>A Writ hit made a spot on this bot.</summary>
        public static event Action<SkylineBot> SpotCreated;
        /// <summary>This bot saw its target for the first time (raised once per bot; the first raise is the first sighting of all).</summary>
        public static event Action<SkylineBot> FirstSeenAny;
        public static event Action<SkylineBot> Wrecked;
        /// <summary>A plate (index into the def's plates) came off.</summary>
        public static event Action<SkylineBot, int> PlateShed;
        /// <summary>A Writ hit landed on a spot (and consumed it, by default).</summary>
        public static event Action<SkylineBot> SpotHit;
        /// <summary>The brain entered a state.</summary>
        public static event Action<SkylineBot, BotState> StateEntered;
        /// <summary>The bot attacked: a bolt fired, a beam shot, a sweep started, a squeegee slap.</summary>
        public static event Action<SkylineBot> Attacked;
        /// <summary>A gondola's mine changed (laid, armed, triggered, blast, expired), at its logical position.</summary>
        public static event Action<SkylineBot, SkylineMineEvent, Vector3> MineEvent;
        /// <summary>A bark: its id ("spotted", "hurt", "lost", "wrecked") and its line.</summary>
        public static event Action<SkylineBot, string, string> Barked;

        /// <summary>Where a searching drone may go (x, z): FightSim's hall less 1 m, south of the mezzanine's edge (z 11). The
        /// arena or the gallery sets its own.</summary>
        public static Rect SearchArea = DefaultSearchArea;
        static Rect DefaultSearchArea => Rect.MinMaxRect(GalleriaArena.HallMinX + 1f, GalleriaArena.HallMinZ + 1f, GalleriaArena.HallMaxX - 1f, 11f);

        static readonly Dictionary<string, int> _numbers = new Dictionary<string, int>();
        static readonly RaycastHit[] _hits = new RaycastHit[32];
        static readonly Collider[] _overlaps = new Collider[32];
        /// <summary>The eye above the chest (m): the player's eye is 1.6 m up, its chest 1.25 m.</summary>
        const float EyeAboveChest = 0.35f;
        const float RegrowSeconds = 3f, FlashSeconds = 0.07f, HurtSoundGap = 0.15f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Live.Clear();
            _numbers.Clear();
            SpotCreated = null; FirstSeenAny = null; Wrecked = null; PlateShed = null; SpotHit = null;
            StateEntered = null; Attacked = null; MineEvent = null; Barked = null;
            SearchArea = DefaultSearchArea;
        }

        /// <summary>Makes a bot of <paramref name="enemyId"/> at <paramref name="position"/> (a floor-care unit's on the floor,
        /// the others' at their centre: the drone hovers at this height) facing <paramref name="yaw"/>, alone (no group).</summary>
        public static SkylineBot Spawn(string enemyId, Vector3 position, float yaw, ulong seed, ISkylineTarget target, WritTuning writ, Transform parent) =>
            Spawn(enemyId, position, yaw, seed, target, writ, parent, -1, position);

        /// <summary>As the other, in <paramref name="group"/>, with a rail to <paramref name="railEnd"/> (the gondola; ignored by
        /// the others). Encounters' <c>Spawn</c> data maps on directly: (enemy, position, group, railEnd). A gondola faces
        /// away from its wall (<paramref name="yaw"/>).</summary>
        public static SkylineBot Spawn(string enemyId, Vector3 position, float yaw, ulong seed, ISkylineTarget target, WritTuning writ, Transform parent,
            int group, Vector3 railEnd)
        {
            var def = SkylineEnemies.Find(enemyId);
            if (def == null)
            {
                Debug.LogWarning("SKYLINE_BOT unknown enemy " + enemyId);
                return null;
            }
            _numbers.TryGetValue(def.id, out int n);
            n++;
            _numbers[def.id] = n;
            var go = new GameObject(def.id + "#" + n);
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var bot = go.AddComponent<SkylineBot>();
            bot.Init(def, n, position, yaw, seed, target, writ, group, railEnd);
            return bot;
        }

        /// <summary>Wakes every live bot of <paramref name="group"/> (a negative group wakes only <paramref name="who"/>).</summary>
        public static void Wake(int group, SkylineBot who = null)
        {
            if (group < 0)
            {
                if (who != null && !who.IsAwake && !who.Body.Wrecked) who.IsAwake = true;
                return;
            }
            bool any = false;
            for (int i = 0; i < Live.Count; i++)
            {
                var b = Live[i];
                if (b == null || b.group != group || b.Body.Wrecked || b.IsAwake) continue;
                b.IsAwake = true;
                any = true;
            }
            if (any) Debug.Log("SKYLINE_BOT group " + group + " woke");
        }

        /// <summary>A target that exists (not null, not a destroyed component).</summary>
        public static bool TargetLive(ISkylineTarget t)
        {
            if (t == null) return false;
            var o = t as Object;
            return (object)o == null || o != null;
        }

        /// <summary>Colliders that never block sight, bolts or moves: triggers, bots' hit zones, shed parts, and
        /// <paramref name="self"/>'s own.</summary>
        public static bool IgnoredBySight(Collider c, Transform self)
        {
            if (c == null || c.isTrigger) return true;
            if (c.GetComponent<SkylineHitZone>() != null || c.GetComponent<SkylineDebris>() != null) return true;
            return self != null && c.transform.IsChildOf(self);
        }

        /// <summary>The first thing that blocks along a ray (cover only), within <paramref name="dist"/>.</summary>
        public static bool CastClear(Vector3 from, Vector3 dir, float dist, Transform self, out RaycastHit hit)
        {
            hit = default(RaycastHit);
            int n = Physics.RaycastNonAlloc(from, dir, _hits, dist, ~0, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            for (int i = 0; i < n; i++)
            {
                if (IgnoredBySight(_hits[i].collider, self)) continue;
                if (_hits[i].distance < best) { best = _hits[i].distance; hit = _hits[i]; }
            }
            return best < float.PositiveInfinity;
        }

        // ------------------------------------------------------------------ instance

        public EnemyDef Def;
        public EnemyBody Body;
        public BotBrain Brain;
        public WeakSpots Spots;
        /// <summary>Dot's practice bot: never attacks, regrows a shed plate after 3 s and refills its chassis instead of wrecking.</summary>
        public bool practice;
        public ISkylineTarget target;
        /// <summary>The bots that wake together; negative: alone.</summary>
        public int group = -1;

        public string Id { get; private set; } = "";
        public SkylineBotParts Parts { get; private set; }
        public Transform Rig => Parts != null ? Parts.rig : transform;
        public bool IsAwake { get; private set; }
        public bool SeesTarget { get; private set; }
        public TelegraphedShot Beam { get; private set; }
        public GondolaRail Rail { get; private set; }
        public MineLayer Mines { get; private set; }
        public SkylineWreck WreckObject { get; private set; }
        public WritTuning WritTuning => _writ;

        // ISkylineAimable
        public Vector3 Center => Parts != null ? Rig.TransformPoint(Parts.centerLocal) : transform.position;
        public float Radius => Def != null ? Def.bodyRadius : 0.5f;
        public bool Alive => _initialized && Body != null && !Body.Wrecked;

        bool _initialized, _everSeen, _sawLastStep, _beamStartedThisFrame, _lockBeeped;
        ulong _seed;
        Rng _rng;
        WritTuning _writ;
        readonly List<BotCue> _cues = new List<BotCue>();
        readonly List<BotCue> _hitCues = new List<BotCue>();
        float _clock;
        Vector3 _pos, _home, _kick;
        float _yaw, _pitch, _spawnYaw, _shake, _sway, _swayVel;
        // the drone's search
        Vector3 _lastKnown, _goal;
        bool _hasGoal, _returning;
        float _lastSeenAt = float.NegativeInfinity;
        readonly List<Vector3> _trail = new List<Vector3>();
        float _eightPhase, _eightWeight = 1f;
        // the floor-care unit
        SkylineAcid _acid;
        Collider _detour;
        int _detourCorner;
        // the spotlight
        SkylineBeam _beamView;
        // the gondola
        readonly Dictionary<WallMine, SkylineMineView> _mineViews = new Dictionary<WallMine, SkylineMineView>();
        readonly List<WallMine> _mineDone = new List<WallMine>();
        readonly List<float> _blasts = new List<float>();
        readonly List<Vector3> _blastAt = new List<Vector3>();
        GameObject _railBar;
        float _squeegee, _squeegeeTarget;
        // look and sound
        SkylineBotFace _face;
        AudioSource _voice, _idle;
        SkylineBarkBubble _bubble;
        readonly Dictionary<int, SkylineSpotMarker> _markers = new Dictionary<int, SkylineSpotMarker>();
        readonly List<int> _markerGone = new List<int>();
        readonly List<Renderer> _flashing = new List<Renderer>();
        float _flashLeft, _lastHurtSound = -1f, _spin, _charge;
        bool _charging;
        GameObject _chargeBall;
        float[] _regrowAt;
        static MaterialPropertyBlock _flashBlock;

        void Init(EnemyDef def, int number, Vector3 position, float yaw, ulong seed, ISkylineTarget tgt, WritTuning writ, int grp, Vector3 railEnd)
        {
            Def = def;
            Id = def.id + "#" + number;
            _seed = seed;
            target = tgt;
            _writ = writ ?? new WritTuning();
            group = grp;
            Body = new EnemyBody(def);
            Brain = new BotBrain(def, seed);
            Spots = new WeakSpots();
            _rng = new Rng(seed ^ 0x9E3779B97F4A7C15UL, 0xB0B5UL);
            _pos = _home = position;
            _yaw = _spawnYaw = yaw;
            _eightPhase = (float)(seed % 1000UL) * 0.0063f;
            _regrowAt = new float[def.plates != null ? def.plates.Length : 0];
            for (int i = 0; i < _regrowAt.Length; i++) _regrowAt[i] = -1f;

            Parts = SkylineBotBodies.Build(def, transform, this);
            _face = gameObject.AddComponent<SkylineBotFace>();
            _face.Init(def.id, Parts.face != null ? Parts.face.GetComponent<Renderer>() : null);

            _voice = gameObject.AddComponent<AudioSource>();
            _voice.playOnAwake = false;
            _voice.spatialBlend = 1f;
            _voice.rolloffMode = AudioRolloffMode.Linear;
            _voice.minDistance = 2f;
            _voice.maxDistance = 45f;
            var idleClip = SkylineSounds.Get(def.id + "/idle_loop");
            if (idleClip != null)
            {
                _idle = gameObject.AddComponent<AudioSource>();
                _idle.clip = idleClip;
                _idle.loop = true;
                _idle.spatialBlend = 1f;
                _idle.rolloffMode = AudioRolloffMode.Linear;
                _idle.minDistance = 1.5f;
                _idle.maxDistance = 25f;
                _idle.volume = 0.35f;
                _idle.Play();
            }

            if (def.aim == AimModel.FiresAtWhereYouAre && def.projectileSpeed <= 0f)
            {
                Beam = new TelegraphedShot
                {
                    telegraph = def.telegraphSeconds, lockBefore = def.beamLockBefore, followRate = def.beamFollowRate, hitRadius = def.beamHitRadius,
                };
                _beamView = SkylineBeam.Create(transform);
            }
            if (def.moveSpeed > 0f) _acid = SkylineAcid.Create(transform, def);
            if (def.layer == ThreatLayer.Walls)
            {
                Rail = new GondolaRail(N(position), N(railEnd), def.railSpeed);
                Mines = new MineLayer(def);
                BuildRailBar(position, railEnd);
            }

            _initialized = true;
            Live.Add(this);
            if (isActiveAndEnabled && !SkylineTargets.Aimables.Contains(this)) SkylineTargets.Aimables.Add(this);
            Pose(0f);
        }

        void OnEnable()
        {
            if (_initialized && Alive && !SkylineTargets.Aimables.Contains(this)) SkylineTargets.Aimables.Add(this);
        }

        void OnDisable() => SkylineTargets.Aimables.Remove(this);

        void OnDestroy()
        {
            Live.Remove(this);
            SkylineTargets.Aimables.Remove(this);
            foreach (var v in _mineViews.Values) v.Destroy();
            _mineViews.Clear();
            if (_railBar != null) Destroy(_railBar);
            if (_bubble != null) Destroy(_bubble.gameObject);
            if (_chargeBall != null) Destroy(_chargeBall);
        }

        // ------------------------------------------------------------------ the frame

        void Update()
        {
            if (!_initialized) return;
            float dt = Time.deltaTime;
            if (!(dt > 0f)) return;
            _clock += dt;
            if (Body.Wrecked)
            {
                if (Mines != null) TickMines(dt, false);     // a wrecked gondola's mines stay on the wall
                return;
            }
            if (practice) Regrow();
            float now = Time.time;
            Spots.Expire(now, _writ);
            SyncMarkers();

            bool live = TargetLive(target) && target.Root != null && target.Root.gameObject.activeInHierarchy;
            Vector3 aimAt = Vector3.zero;
            bool sees = live && Sees(out aimAt);
            SeesTarget = sees;
            if (sees && !_everSeen)
            {
                _everSeen = true;
                FirstSeenAny?.Invoke(this);
            }
            if (!IsAwake && sees) Wake(group, this);
            _cues.Clear();
            _beamStartedThisFrame = false;

            if (Def.layer == ThreatLayer.Walls)
            {
                Brain.Tick(dt, IsAwake && sees, _cues);
                if (IsAwake)
                {
                    Rail.Tick(dt);
                    _pos = U(Rail.Position);
                }
                HandleCues(_cues, sees, aimAt, false);
                TickMines(dt, IsAwake);
            }
            else if (Def.moveSpeed > 0f)
            {
                FloorUnit(dt, live, sees, aimAt);
            }
            else
            {
                Brain.Tick(dt, IsAwake && sees, _cues);
                if (sees)
                {
                    _lastSeenAt = _clock;
                    _hasGoal = false;
                    _returning = false;
                }
                else if (IsAwake && Def.searchSpeed > 0f && _lastSeenAt > float.NegativeInfinity && live) Search(dt, _sawLastStep);
                _sawLastStep = sees;
                HandleCues(_cues, sees, aimAt, false);
                StepBeam(dt, live);
            }

            Knockback(dt);
            Face(dt, sees, live);
            Pose(dt);
            TickFlash(dt);
        }

        bool Sees(out Vector3 aimAt)
        {
            Vector3 c = Center, chest = target.Chest, eye = chest + Vector3.up * EyeAboveChest;
            aimAt = chest;
            if (Vector3.Distance(c, chest) <= Def.range && Clear(c, chest)) return true;
            aimAt = eye;
            return Vector3.Distance(c, eye) <= Def.range && Clear(c, eye);
        }

        /// <summary>Nothing but the target (or things that never block) between <paramref name="a"/> and <paramref name="b"/>.</summary>
        bool Clear(Vector3 a, Vector3 b)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (!(len > 1e-4f)) return true;
            int n = Physics.RaycastNonAlloc(a, d / len, _hits, len, ~0, QueryTriggerInteraction.Ignore);
            Transform tr = TargetLive(target) ? target.Root : null;
            for (int i = 0; i < n; i++)
            {
                var col = _hits[i].collider;
                if (IgnoredBySight(col, transform)) continue;
                if (tr != null && col.transform.IsChildOf(tr)) continue;
                return false;
            }
            return true;
        }

        /// <summary>A sphere of <paramref name="r"/> moved from <paramref name="a"/> to <paramref name="b"/> touches cover.</summary>
        bool BlockedThick(Vector3 a, Vector3 b, float r)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            Transform tr = TargetLive(target) ? target.Root : null;
            if (!(len > 1e-4f))
            {
                int m = Physics.OverlapSphereNonAlloc(a, r, _overlaps, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < m; i++)
                    if (!IgnoredBySight(_overlaps[i], transform) && (tr == null || !_overlaps[i].transform.IsChildOf(tr))) return true;
                return false;
            }
            int n = Physics.SphereCastNonAlloc(a, r, d / len, _hits, len, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var col = _hits[i].collider;
                if (IgnoredBySight(col, transform) || (tr != null && col.transform.IsChildOf(tr))) continue;
                return true;
            }
            // a sphere that starts inside something isn't reported by the cast
            return BlockedThick(a, a, r * 0.98f);
        }

        // ------------------------------------------------------------------ cues

        void HandleCues(List<BotCue> cues, bool sees, Vector3 aimAt, bool floorReach)
        {
            for (int i = 0; i < cues.Count; i++)
            {
                var c = cues[i];
                switch (c.kind)
                {
                    case BotCueKind.State:
                        StateEntered?.Invoke(this, c.state);
                        if (c.state == BotState.Alert) Sound("alert");
                        else if (c.state == BotState.Telegraph) { Sound("telegraph"); OnTelegraph(sees); }
                        else if (c.state == BotState.Recover || c.state == BotState.Idle) { _charging = false; _squeegeeTarget = 0f; }
                        break;
                    case BotCueKind.Attack:
                        OnAttack(sees, aimAt, floorReach);
                        break;
                    case BotCueKind.Bark:
                        Bark(c.bark);
                        break;
                }
            }
        }

        void OnTelegraph(bool sees)
        {
            if (Beam != null)
            {
                if (sees && TargetLive(target))
                {
                    Beam.Start(N(target.Chest));
                    _beamStartedThisFrame = true;
                    _lockBeeped = false;
                }
            }
            else if (Def.id == SkylineEnemies.DroneId) { _charging = true; _charge = 0f; }
            else if (Def.layer == ThreatLayer.Walls) _squeegeeTarget = -70f;
        }

        void OnAttack(bool sees, Vector3 aimAt, bool floorReach)
        {
            if (practice || Beam != null) return;       // the spotlight's shot is the beam's
            if (Def.layer == ThreatLayer.Walls)
            {
                _squeegeeTarget = 35f;
                Sound("attack");
                Attacked?.Invoke(this);
                return;
            }
            if (Def.moveSpeed > 0f)
            {
                if (!floorReach || _acid == null || !TargetLive(target)) return;
                Vector3 feet = target.Root.position;
                float bearing = Mathf.Atan2(feet.x - _pos.x, feet.z - _pos.z) * Mathf.Rad2Deg;
                _acid.Begin(bearing);
                _yaw = bearing;
                Sound("attack");
                Sound("acid_sweep");
                Attacked?.Invoke(this);
                return;
            }
            _charging = false;
            if (sees && TargetLive(target)) FireBolt(aimAt);
        }

        void FireBolt(Vector3 aimAt)
        {
            var d = Def;
            Vector3 from = Parts.muzzle != null ? Parts.muzzle.position : Center;
            float lead = d.aim == AimModel.LeadsYou ? Difficulties.Normal().leadFactor : 0f;
            Vector3 aim = U(Aim.LeadPoint(N(from), N(aimAt), N(target.Velocity), d.projectileSpeed, lead)) - from;
            float len = aim.magnitude;
            if (!(len > 1e-4f)) return;
            Vector3 dir = Spread(aim / len, d.aimSpreadDeg);
            SkylineBolt.Fire(from, dir * d.projectileSpeed, d.projectileRadius, d.attackDamage, d.id, target, transform);
            Sound("attack");
            Attacked?.Invoke(this);
        }

        /// <summary>The direction tilted by a random error uniform over a cone of <paramref name="deg"/> degrees (FightSim's).</summary>
        Vector3 Spread(Vector3 dir, float deg)
        {
            if (!(deg > 0f)) return dir;
            Vector3 side = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.99f ? Vector3.up : Vector3.right).normalized;
            Vector3 up = Vector3.Cross(side, dir);
            float angle = _rng.Range(0f, 2f * Mathf.PI);
            float rr = Mathf.Tan(deg * Mathf.Deg2Rad) * Mathf.Sqrt(_rng.NextFloat());
            return (dir + (side * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * rr).normalized;
        }

        void StepBeam(float dt, bool live)
        {
            if (Beam == null) return;
            if (!Beam.Active)
            {
                _beamView.Hide();
                return;
            }
            // one clock: on the frame it starts, the beam catches up the time the brain carried into Telegraph
            float step = _beamStartedThisFrame ? Brain.StateTime : dt;
            Vector3 lamp = Parts.lamp != null ? Parts.lamp.position : Center;
            NVec3 chest = live ? N(target.Chest) : new NVec3(float.NaN);
            if (step > 0f && Beam.Step(step, chest, out bool hit))
            {
                Vector3 to = U(Beam.AimPoint);
                Sound("attack");
                Attacked?.Invoke(this);
                if (!practice && hit && live && Sees(out _)) target.TakeAttack(Def.attackDamage, Def.id, lamp);
                _beamView.Fire(lamp, to);
                if (Parts.muzzle != null) SkylineTracer.Spawn(Parts.muzzle.position, to, new Color(1f, 0.95f, 0.85f));
                return;
            }
            if (Beam.Locked && !_lockBeeped)
            {
                _lockBeeped = true;
                Sound("lock_beep");
            }
            _beamView.Draw(lamp, U(Beam.AimPoint), Beam.Locked);
        }

        void Bark(string bark)
        {
            string line = SkylineBarks.Line(Def.id, bark);
            if (string.IsNullOrEmpty(line)) return;
            Barked?.Invoke(this, bark, line);
            Vector3 offset = Rig.TransformPoint(Parts.centerLocal) - transform.position + Vector3.up * (Def.bodyRadius + 0.55f);
            _bubble = SkylineBarks.Say(transform, offset, line, Def.id, (int)(_seed & 0x7FFFFFFFUL), _voice, _bubble);
        }

        void Sound(string name) => SkylineSounds.PlayAt(Def.id + "/" + name, Center);

        // ------------------------------------------------------------------ the floor-care unit

        void FloorUnit(float dt, bool live, bool sees, Vector3 aimAt)
        {
            var d = Def;
            Vector3 feet = live ? target.Root.position : _pos;
            float dx = feet.x - _pos.x, dz = feet.z - _pos.z;
            float flat = Mathf.Sqrt(dx * dx + dz * dz);
            Vector3 nozzle = _pos + new Vector3(0f, 0.2f, 0f), ankle = new Vector3(feet.x, _pos.y + 0.2f, feet.z);
            bool reach = live && IsAwake && flat <= d.sweepRadius && Clear(nozzle, ankle);
            Brain.Tick(dt, reach, _cues);
            HandleCues(_cues, sees, aimAt, reach);
            if (_acid != null && _acid.Running)
            {
                if (!_acid.Hit && live && !practice && AcidSweep.Touches(N(_pos), _acid.Yaw, _acid.T, N(feet), _pos.y, d) && Clear(nozzle, ankle))
                {
                    _acid.Hit = true;
                    target.TakeAttack(d.attackDamage, d.id, nozzle);
                }
                Vector3 noz = Parts.nozzle != null ? Parts.nozzle.position : nozzle;
                _acid.Advance(dt, noz, _pos.y);
                _yaw = Mathf.MoveTowardsAngle(_yaw, _acid.JetBearing, 720f * dt);
                return;
            }
            if (!IsAwake || !live) return;
            if (flat > 1e-3f) _yaw = Mathf.MoveTowardsAngle(_yaw, Mathf.Atan2(dx, dz) * Mathf.Rad2Deg, 360f * dt);
            if (Brain.State == BotState.Telegraph) return;
            if (flat < d.sweepRadius * 0.5f) return;   // close enough: it wants the target inside its arc, not under it
            Drive(feet, d.moveSpeed * dt);
        }

        const float DetourMargin = 0.4f;

        /// <summary>FightSim's Drive on Unity's colliders: when a collider stands on the straight line it makes for the corner of
        /// that collider's bounds (just outside) that gives the shortest way round with a clear line on, kept until the line is
        /// clear; a face it still runs into is slid along.</summary>
        void Drive(Vector3 target3, float stepLen)
        {
            float r = Def.bodyRadius;
            Vector3 p = _pos, tgt = new Vector3(target3.x, _pos.y, target3.z);
            var blocker = FirstBlocking(p, tgt, r);
            Vector3 aim = tgt;
            if (blocker != null)
            {
                if (blocker != _detour)
                {
                    _detour = blocker;
                    _detourCorner = BestCorner(blocker.bounds, p, tgt, r);
                }
                aim = Corner(blocker.bounds, _detourCorner, r + DetourMargin, p.y);
            }
            else _detour = null;
            Steer(p, aim, tgt, stepLen);
        }

        float BodyY => Def.bodyRadius + 0.05f;

        Collider FirstBlocking(Vector3 p, Vector3 tgt, float r)
        {
            Vector3 a = p + Vector3.up * BodyY, b = tgt + Vector3.up * BodyY;
            Vector3 d = b - a;
            float len = d.magnitude;
            if (!(len > 1e-3f)) return null;
            int n = Physics.SphereCastNonAlloc(a, r, d / len, _hits, len, ~0, QueryTriggerInteraction.Ignore);
            Transform tr = TargetLive(target) ? target.Root : null;
            Collider best = null;
            float bestD = float.PositiveInfinity;
            for (int i = 0; i < n; i++)
            {
                var c = _hits[i].collider;
                if (IgnoredBySight(c, transform) || (tr != null && c.transform.IsChildOf(tr))) continue;
                if (_hits[i].distance < bestD) { bestD = _hits[i].distance; best = c; }
            }
            return best;
        }

        static Vector3 Corner(Bounds b, int i, float m, float y) =>
            new Vector3((i & 1) == 0 ? b.min.x - m : b.max.x + m, y, (i & 2) == 0 ? b.min.z - m : b.max.z + m);

        static int BestCorner(Bounds b, Vector3 p, Vector3 tgt, float r)
        {
            int best = 0;
            float bestLen = float.PositiveInfinity;
            for (int i = 0; i < 4; i++)
            {
                Vector3 c = Corner(b, i, r + DetourMargin, p.y);
                float len = Flat(p, c) + Flat(c, tgt);
                if (SegmentHitsRect(c, tgt, b, r)) len += 1000f;   // still behind it from that corner
                if (SegmentHitsRect(p, c, b, r)) len += Mathf.Max(b.size.x, b.size.z);   // the far side: round an end first
                if (len < bestLen) { bestLen = len; best = i; }
            }
            return best;
        }

        static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        static bool SegmentHitsRect(Vector3 a, Vector3 b, Bounds box, float r) =>
            CoverGeometry.SegmentHitsBox(new NVec3(a.x, 0f, a.z), new NVec3(b.x, 0f, b.z),
                new CoverBox { min = new NVec3(box.min.x - r, -1f, box.min.z - r), max = new NVec3(box.max.x + r, 1f, box.max.z + r) });

        /// <summary>One step toward <paramref name="aim"/>; a face in the way is slid along (head-on, it turns toward the end of
        /// the collider that's shorter on the way to the target); a step that's still blocked stalls.</summary>
        void Steer(Vector3 p, Vector3 aim, Vector3 tgt, float stepLen)
        {
            Vector3 to = aim - p;
            to.y = 0f;
            float len = to.magnitude;
            if (!(len > 1e-4f)) return;
            Vector3 step = to / len * Mathf.Min(stepLen, len);
            for (int pass = 0; pass < 2; pass++)
            {
                if (!CastStep(p, step, out var hit)) break;
                Vector3 n = hit.normal;
                n.y = 0f;
                if (n.sqrMagnitude < 1e-6f) break;
                n.Normalize();
                Vector3 slid = step - n * Vector3.Dot(step, n);
                if (slid.magnitude < 0.3f * stepLen)
                {
                    Vector3 side = new Vector3(-n.z, 0f, n.x);
                    var bb = hit.collider.bounds;
                    float ext = Mathf.Abs(side.x) * bb.extents.x + Mathf.Abs(side.z) * bb.extents.z;
                    Vector3 c = new Vector3(bb.center.x, p.y, bb.center.z);
                    Vector3 e1 = c + side * ext, e2 = c - side * ext;
                    slid = Flat(p, e1) + Flat(e1, tgt) <= Flat(p, e2) + Flat(e2, tgt) ? side : -side;
                }
                if (slid.sqrMagnitude < 1e-8f) return;
                step = slid.normalized * stepLen;
            }
            if (CastStep(p, step, out _)) return;     // a corner it can't slide out of: it stalls this step
            _pos = p + step;
        }

        bool CastStep(Vector3 p, Vector3 step, out RaycastHit hit)
        {
            hit = default(RaycastHit);
            float len = step.magnitude;
            if (!(len > 1e-5f)) return false;
            int n = Physics.SphereCastNonAlloc(p + Vector3.up * BodyY, Def.bodyRadius * 0.98f, step / len, _hits, len + 0.02f, ~0, QueryTriggerInteraction.Ignore);
            Transform tr = TargetLive(target) ? target.Root : null;
            float best = float.PositiveInfinity;
            for (int i = 0; i < n; i++)
            {
                var c = _hits[i].collider;
                if (IgnoredBySight(c, transform) || (tr != null && c.transform.IsChildOf(tr))) continue;
                if (_hits[i].distance < best) { best = _hits[i].distance; hit = _hits[i]; }
            }
            return best < float.PositiveInfinity;
        }

        // ------------------------------------------------------------------ the drone's search (FightSim.Search, documented rule)

        void Search(float dt, bool justLost)
        {
            if (justLost)
            {
                _lastKnown = target.Chest;
                _hasGoal = false;
                _returning = false;
                if (!Clear(_pos, _lastKnown) && FindVantage(out _goal))
                {
                    _hasGoal = true;
                    _trail.Add(_pos);   // the way back
                }
            }
            if (!_returning && _clock - _lastSeenAt > Def.rememberSeconds)
            {
                _returning = true;
                _hasGoal = false;
            }
            if (_returning && !_hasGoal)
            {
                // the next leg home: the last point it set out from (struck off only once reached), then home itself
                if (_trail.Count > 0) { _goal = _trail[_trail.Count - 1]; _hasGoal = true; }
                else if (Vector3.Distance(_pos, _home) > 1e-3f) { _goal = _home; _hasGoal = true; }
            }
            if (!_hasGoal) return;
            Vector3 to = _goal - _pos;
            float len = to.magnitude;
            if (len < 1e-3f)
            {
                if (_returning && _trail.Count > 0 && Vector3.Distance(_goal, _trail[_trail.Count - 1]) < 1e-3f) _trail.RemoveAt(_trail.Count - 1);
                _hasGoal = false;
                return;
            }
            Vector3 next = _pos + to / len * Mathf.Min(len, Def.searchSpeed * dt);
            // a blocked step (only if cover moved): hold and try the same leg again; no leg is ever skipped
            if (BlockedThick(_pos, next, Def.bodyRadius)) { _hasGoal = false; return; }
            _pos = next;
        }

        bool FindVantage(out Vector3 goal)
        {
            goal = _pos;
            float r = Def.bodyRadius;
            foreach (float dist in FightSim.SearchRings)
            {
                float best = float.PositiveInfinity;
                for (int k = 0; k < 16; k++)
                {
                    float a = k * (Mathf.PI / 8f);
                    var p = _pos + new Vector3(Mathf.Sin(a) * dist, 0f, Mathf.Cos(a) * dist);
                    if (p.x < SearchArea.xMin || p.x > SearchArea.xMax || p.z < SearchArea.yMin || p.z > SearchArea.yMax) continue;
                    if (BlockedThick(p, p, r)) continue;   // inside a piece
                    float toLast = Vector3.Distance(p, _lastKnown);
                    if (toLast >= best || toLast > Def.range || !Clear(p, _lastKnown)) continue;
                    if (BlockedThick(_pos, p, r)) continue;   // no straight way there
                    best = toLast;
                    goal = p;
                }
                if (!float.IsPositiveInfinity(best)) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ the gondola's mines

        void TickMines(float dt, bool laying)
        {
            bool live = TargetLive(target) && target.Root != null && target.Root.gameObject.activeInHierarchy;
            NVec3 chest = live ? N(target.Chest) : new NVec3(float.PositiveInfinity);
            _blasts.Clear();
            Mines.Tick(dt, Rail, laying && !Body.Wrecked, chest, _blasts);
            var mines = Mines.Mines;
            for (int i = 0; i < mines.Count; i++)
            {
                var m = mines[i];
                if (_mineViews.ContainsKey(m)) continue;
                Vector3 at = U(m.Position);
                Vector3 drop = Parts.mineDrop != null && Parts.rig.gameObject.activeInHierarchy ? Parts.mineDrop.position : at;
                var v = new SkylineMineView(m, at, drop, -(Quaternion.Euler(0f, _spawnYaw, 0f) * Vector3.forward), Def.mineTriggerRadius, transform);
                v.lastState = MineState.Arming;
                _mineViews[m] = v;
                SkylineSounds.PlayAt(Def.id + "/mine_lay", at);
                MineEvent?.Invoke(this, SkylineMineEvent.Laid, at);
            }
            _mineDone.Clear();
            _blastAt.Clear();
            foreach (var kv in _mineViews)
            {
                var m = kv.Key;
                var v = kv.Value;
                if (m.State != v.lastState)
                {
                    v.lastState = m.State;
                    switch (m.State)
                    {
                        case MineState.Armed:
                            SkylineSounds.PlayAt(Def.id + "/mine_arm", v.Logical);
                            MineEvent?.Invoke(this, SkylineMineEvent.Armed, v.Logical);
                            break;
                        case MineState.Fusing:
                            SkylineSounds.PlayAt(Def.id + "/telegraph", v.Logical);
                            MineEvent?.Invoke(this, SkylineMineEvent.Triggered, v.Logical);
                            break;
                        case MineState.Exploded:
                            SkylineSounds.PlayAt(Def.id + "/mine_blast", v.Logical);
                            MineEvent?.Invoke(this, SkylineMineEvent.Blast, v.Logical);
                            v.Blast(Def.mineBlastRadius);
                            _blastAt.Add(v.Logical);
                            _mineDone.Add(m);
                            continue;
                        case MineState.Expired:
                            MineEvent?.Invoke(this, SkylineMineEvent.Expired, v.Logical);
                            v.Expire();
                            break;
                    }
                }
                // a mine the layer dropped as the oldest (over mineMax) is no longer listed: it goes quietly
                if (m.State != MineState.Expired && !Contains(mines, m)) v.Expire();
                if (!v.Tick(dt, m.State)) _mineDone.Add(m);
            }
            for (int i = 0; i < _mineDone.Count; i++) _mineViews.Remove(_mineDone[i]);
            if (_blasts.Count > 0 && live)
            {
                Vector3 c = target.Chest;
                for (int i = 0; i < _blasts.Count; i++)
                {
                    Vector3 from = NearestTo(_blastAt, c, Center);
                    target.TakeAttack(_blasts[i], Def.id, from);
                }
            }
        }

        static bool Contains(IReadOnlyList<WallMine> list, WallMine m)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == m) return true;
            return false;
        }

        static Vector3 NearestTo(List<Vector3> pts, Vector3 c, Vector3 fallback)
        {
            float best = float.PositiveInfinity;
            Vector3 r = fallback;
            for (int i = 0; i < pts.Count; i++)
            {
                float d = (pts[i] - c).sqrMagnitude;
                if (d < best) { best = d; r = pts[i]; }
            }
            return r;
        }

        void BuildRailBar(Vector3 a, Vector3 b)
        {
            Vector3 mount = Parts.rail != null ? Parts.rail.position - transform.position : new Vector3(0f, 1.3f, 0f);
            Vector3 from = a + mount, to = b + mount;
            Vector3 d = to - from;
            float len = d.magnitude;
            Vector3 along = len > 1e-3f ? d / len : Quaternion.Euler(0f, _spawnYaw, 0f) * Vector3.right;
            _railBar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _railBar.name = Id + " Rail";
            Destroy(_railBar.GetComponent<Collider>());
            if (transform.parent != null) _railBar.transform.SetParent(transform.parent, true);
            _railBar.transform.SetPositionAndRotation((from + to) * 0.5f + Vector3.up * 0.1f, Quaternion.LookRotation(along));
            _railBar.transform.localScale = new Vector3(0.1f, 0.06f, len + 1.2f);
            _railBar.GetComponent<Renderer>().sharedMaterial = SkylineBotLook.Lit(SkylineBotLook.Grey);
        }

        // ------------------------------------------------------------------ hits

        public SkylineHitResult TakeHit(in SkylineHitInfo hit)
        {
            var r = new SkylineHitResult();
            if (!_initialized || Body.Wrecked) return r;
            r.hit = true;
            float now = Time.time;
            Vector3 local = Rig.InverseTransformPoint(hit.point);
            r.writ = Writ.Resolve(Spots, hit.zone, N(local), now, hit.fromWrit, hit.gun != null ? hit.gun.weakPointMultiplier : 1f, _writ);
            int plate = hit.zone == HitZone.Plate ? hit.plate : -1;
            bool plateWasOn = plate >= 0 && Body.PlateOn(plate);
            r.damage = Body.Damage(hit.damage * r.writ.multiplier, plate);
            Wake(group, this);
            _hitCues.Clear();
            Brain.OnDamaged(_hitCues);
            HandleCues(_hitCues, SeesTarget, TargetLive(target) ? target.Chest : Center, false);

            if (Time.time - _lastHurtSound >= HurtSoundGap || _lastHurtSound < 0f)
            {
                _lastHurtSound = Time.time;
                Sound("hurt");
            }
            if (r.writ.spotHit)
            {
                if (_markers.TryGetValue(r.writ.spot.id, out var mk) && mk != null) mk.BreakThrough();
                _markers.Remove(r.writ.spot.id);
                SpotHit?.Invoke(this);
            }
            if (r.writ.spotCreated)
            {
                var s = r.writ.spot;
                _markers[s.id] = SkylineSpotMarker.Create(Rig, s.id, new Vector3(s.local.X, s.local.Y, s.local.Z), hit.normal, (int)(_seed & 0xFFFFUL));
                SpotCreated?.Invoke(this);
            }
            if (plateWasOn && Parts.plates[plate] != null) Flash(Parts.plates[plate].GetComponentsInChildren<Renderer>());
            else if (hit.zone == HitZone.Body || hit.zone == HitZone.Plate) Flash(Parts.bodyRenderers);
            if (r.damage.plateShed >= 0) ShedPlate(r.damage.plateShed, hit.normal);
            Knock(hit.direction, hit.knockback);
            if (r.damage.wrecked)
            {
                if (practice) Refill();
                else
                {
                    r.killed = true;
                    WreckNow(hit.normal);
                }
            }
            return r;
        }

        void Knock(Vector3 dir, float amount)
        {
            if (!(amount > 0f)) return;
            if (Def.layer == ThreatLayer.Walls) { _swayVel += amount * 60f; return; }
            if (Beam != null) { _shake = Mathf.Min(1f, _shake + amount * 0.5f); return; }
            Vector3 d = dir;
            if (Def.moveSpeed > 0f) d.y = 0f;
            if (d.sqrMagnitude < 1e-6f) return;
            _kick += d.normalized * amount;
        }

        void Knockback(float dt)
        {
            _shake = Mathf.Max(0f, _shake - dt * 3f);
            _swayVel += -_sway * 40f * dt;
            _swayVel *= Mathf.Exp(-3f * dt);
            _sway += _swayVel * dt;
            if (_kick.sqrMagnitude < 1e-6f) { _kick = Vector3.zero; return; }
            Vector3 step = _kick * dt;
            if (Def.moveSpeed > 0f)
            {
                if (!CastStep(_pos, step, out _)) _pos += step;
                else _kick = Vector3.zero;
            }
            else if (Def.layer != ThreatLayer.Walls && Beam == null)
            {
                if (!BlockedThick(_pos, _pos + step, Def.bodyRadius * 0.9f)) _pos += step;
                else _kick = Vector3.zero;
            }
            _kick *= Mathf.Exp(-5f * dt);
        }

        void Flash(IList<Renderer> renderers)
        {
            if (_flashBlock == null)
            {
                _flashBlock = new MaterialPropertyBlock();
                _flashBlock.SetColor("_BaseColor", Color.white);
                _flashBlock.SetColor("_Color", Color.white);
            }
            ClearFlash();
            for (int i = 0; i < renderers.Count; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                r.SetPropertyBlock(_flashBlock);
                _flashing.Add(r);
            }
            _flashLeft = FlashSeconds;
        }

        void TickFlash(float dt)
        {
            if (_flashLeft <= 0f) return;
            _flashLeft -= dt;
            if (_flashLeft <= 0f) ClearFlash();
        }

        void ClearFlash()
        {
            for (int i = 0; i < _flashing.Count; i++) if (_flashing[i] != null) _flashing[i].SetPropertyBlock(null);
            _flashing.Clear();
        }

        void ShedPlate(int i, Vector3 normal)
        {
            if (Parts.plates == null || i < 0 || i >= Parts.plates.Length) return;
            var t = Parts.plates[i];
            if (t != null && t.gameObject.activeSelf)
            {
                Fling(t.gameObject, normal, 1f);
                t.gameObject.SetActive(false);
            }
            if (practice && i < _regrowAt.Length) _regrowAt[i] = _clock + RegrowSeconds;
            PlateShed?.Invoke(this, i);
            Sound("plate_shed");
        }

        /// <summary>A copy of <paramref name="part"/> thrown off along <paramref name="normal"/> and up: a Rigidbody that ignores the
        /// player's controller, shrinking away over its last second and gone after 6 s.</summary>
        GameObject Fling(GameObject part, Vector3 normal, float strength)
        {
            var clone = Instantiate(part, part.transform.position, part.transform.rotation);
            clone.name = part.name + " (shed)";
            clone.transform.localScale = part.transform.lossyScale;
            foreach (var z in clone.GetComponentsInChildren<SkylineHitZone>(true)) Destroy(z);
            foreach (var m in clone.GetComponentsInChildren<SkylineSpotMarker>(true)) Destroy(m.gameObject);
            var cc = PlayerController();
            foreach (var col in clone.GetComponentsInChildren<Collider>(true))
            {
                var mc = col as MeshCollider;
                if (mc != null) mc.convex = true;
                if (col.GetComponent<SkylineDebris>() == null) col.gameObject.AddComponent<SkylineDebris>().life = 6f;
                if (cc != null) Physics.IgnoreCollision(col, cc, true);
            }
            var debris = clone.GetComponent<SkylineDebris>();
            if (debris == null) debris = clone.AddComponent<SkylineDebris>();
            debris.life = 6f;
            var rb = clone.AddComponent<Rigidbody>();
            rb.mass = 2f;
            Vector3 n = normal.sqrMagnitude > 1e-6f ? normal.normalized : (clone.transform.position - Center).normalized;
            Vector3 jitter = new Vector3(_rng.Range(-1f, 1f), _rng.Range(0f, 0.5f), _rng.Range(-1f, 1f)) * 0.8f;
            rb.AddForce((n * 2.5f + Vector3.up * 3f + jitter) * strength * rb.mass, ForceMode.Impulse);
            rb.AddTorque(new Vector3(_rng.Range(-4f, 4f), _rng.Range(-4f, 4f), _rng.Range(-4f, 4f)), ForceMode.Impulse);
            clone.SetActive(true);
            return clone;
        }

        Collider PlayerController()
        {
            if (!TargetLive(target)) return null;
            var p = target as SkylinePlayer;
            if (p != null)
            {
                if (p.movement != null && p.movement.controller != null) return p.movement.controller;
                return p.GetComponent<CharacterController>();
            }
            return null;
        }

        void Regrow()
        {
            for (int i = 0; i < _regrowAt.Length; i++)
            {
                if (_regrowAt[i] < 0f || _clock < _regrowAt[i]) continue;
                _regrowAt[i] = -1f;
                var hp = new float[_regrowAt.Length];
                for (int j = 0; j < hp.Length; j++) hp[j] = j == i ? Def.plates[j].hp : Body.PlateHp(j);
                Body = Rebuilt(hp, Body.Chassis);
                if (Parts.plates[i] != null) Parts.plates[i].gameObject.SetActive(true);
            }
        }

        /// <summary>A body with these plate hp and chassis, made by damaging a fresh one (EnemyBody has no setter).</summary>
        EnemyBody Rebuilt(float[] plateHp, float chassis)
        {
            var b = new EnemyBody(Def);
            for (int j = 0; j < plateHp.Length; j++)
            {
                float lost = b.PlateHp(j) - plateHp[j];
                if (lost > 0f) b.Damage(lost, j);
            }
            float chassisLost = b.Chassis - chassis;
            if (chassisLost > 0f && chassisLost < b.Chassis) b.Damage(chassisLost, -1);
            return b;
        }

        /// <summary>The practice bot's "wreck": a fresh body, every plate back on.</summary>
        void Refill()
        {
            Body = new EnemyBody(Def);
            for (int i = 0; i < _regrowAt.Length; i++)
            {
                _regrowAt[i] = -1f;
                if (Parts.plates[i] != null) Parts.plates[i].gameObject.SetActive(true);
            }
            Debug.Log("SKYLINE_BOT " + Id + " practice refill");
        }

        void WreckNow(Vector3 normal)
        {
            _hitCues.Clear();
            Brain.OnWrecked(_hitCues);
            HandleCues(_hitCues, false, Center, false);
            _face.Show(BotFace.Wrecked);
            Sound("wreck");
            if (_idle != null) _idle.Stop();
            if (_acid != null) _acid.Stop();
            if (_beamView != null) _beamView.Hide();
            _charging = false;
            ClearFlash();
            Vector3 c = Center;
            // the burst: the plates still on, and 4-6 parts of the body
            for (int i = 0; i < Parts.plates.Length; i++)
            {
                var t = Parts.plates[i];
                if (t == null || !t.gameObject.activeSelf) continue;
                Fling(t.gameObject, (t.position - c).sqrMagnitude > 1e-4f ? (t.position - c).normalized : Vector3.up, 1.3f);
                t.gameObject.SetActive(false);
            }
            int parts = 4 + _rng.Range(0, 3);
            for (int i = 0; i < parts; i++)
            {
                var bit = SkylineBotLook.Part(PrimitiveType.Cube, "Part", null, Vector3.zero, Vector3.one * _rng.Range(0.1f, 0.22f), i % 2 == 0 ? SkylineBotLook.White : SkylineBotLook.Grey);
                bit.transform.position = c + new Vector3(_rng.Range(-0.2f, 0.2f), _rng.Range(0f, 0.2f), _rng.Range(-0.2f, 0.2f));
                bit.SetActive(false);
                var dir = new Vector3(_rng.Range(-1f, 1f), _rng.Range(0.2f, 1f), _rng.Range(-1f, 1f)).normalized;
                var flung = Fling(bit, dir, 1.4f);
                Destroy(bit);
                flung.name = "Wreck part";
            }
            foreach (var m in _markers.Values) if (m != null) m.Close();
            _markers.Clear();
            Spots.Clear();
            Rig.gameObject.SetActive(false);
            if (Parts.mount != null) Parts.mount.gameObject.SetActive(false);
            Live.Remove(this);
            SkylineTargets.Aimables.Remove(this);
            Vector3 floor = new Vector3(c.x, _pos.y - (Def.moveSpeed > 0f ? 0f : 1f), c.z);
            if (CastClear(c, Vector3.down, 60f, transform, out var down)) floor = down.point;
            else if (Def.moveSpeed > 0f) floor = _pos;
            WreckObject = SkylineWreck.Create(Def, Id, floor, _yaw, target, transform.parent);
            Wrecked?.Invoke(this);
        }

        void SyncMarkers()
        {
            if (_markers.Count == 0) return;
            _markerGone.Clear();
            foreach (var kv in _markers)
            {
                bool still = false;
                var all = Spots.All;
                for (int i = 0; i < all.Count; i++) if (all[i].id == kv.Key) { still = true; break; }
                if (!still) _markerGone.Add(kv.Key);
            }
            for (int i = 0; i < _markerGone.Count; i++)
            {
                if (_markers.TryGetValue(_markerGone[i], out var m) && m != null) m.Close();
                _markers.Remove(_markerGone[i]);
            }
        }

        // ------------------------------------------------------------------ looks

        void Face(float dt, bool sees, bool live)
        {
            _face.Show(Brain.Face);
            if (Beam != null)
            {
                // the spotlight turns: onto the beam's point, else the target, else a slow idle scan
                Vector3 look;
                if (Beam.Active) look = U(Beam.AimPoint);
                else if (sees && live) look = target.Chest;
                else if (!IsAwake || Brain.State == BotState.Idle)
                    look = Center + Quaternion.Euler(10f, _spawnYaw + Mathf.Sin(_clock * 0.4f) * 25f, 0f) * Vector3.forward * 10f;
                else look = Center + Quaternion.Euler(_pitch, _yaw, 0f) * Vector3.forward * 10f;
                Vector3 d = look - Center;
                if (d.sqrMagnitude > 1e-4f)
                {
                    float wantYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                    float wantPitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
                    _yaw = Mathf.MoveTowardsAngle(_yaw, wantYaw, 240f * dt);
                    _pitch = Mathf.MoveTowardsAngle(_pitch, Mathf.Clamp(wantPitch, -60f, 70f), 240f * dt);
                }
            }
            else if (Def.moveSpeed <= 0f && Def.layer != ThreatLayer.Walls)
            {
                // the drone faces the target it sees, else where it's going
                Vector3 d = Vector3.zero;
                if (sees && live) d = target.Chest - Center;
                else if (_hasGoal) d = _goal - _pos;
                d.y = 0f;
                if (d.sqrMagnitude > 1e-4f) _yaw = Mathf.MoveTowardsAngle(_yaw, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 270f * dt);
            }
        }

        void Pose(float dt)
        {
            if (Parts == null) return;
            var rig = Parts.rig;
            if (Def.layer == ThreatLayer.Walls)
            {
                transform.position = _pos;
                rig.rotation = Quaternion.Euler(0f, _spawnYaw, _sway);
                _squeegee = Mathf.MoveTowards(_squeegee, _squeegeeTarget, 400f * dt);
                if (Parts.squeegee != null) Parts.squeegee.localRotation = Quaternion.Euler(_squeegee, 0f, 0f);
                if (Mathf.Abs(_squeegee - 35f) < 0.5f && _squeegeeTarget > 0f) _squeegeeTarget = 0f;
            }
            else if (Beam != null)
            {
                var shake = _shake > 0f ? Quaternion.Euler(_rng.Range(-1f, 1f) * _shake * 6f, _rng.Range(-1f, 1f) * _shake * 6f, 0f) : Quaternion.identity;
                rig.rotation = Quaternion.Euler(_pitch, _yaw, 0f) * shake;
            }
            else if (Def.moveSpeed > 0f)
            {
                transform.position = _pos;
                rig.rotation = Quaternion.Euler(0f, _yaw, 0f);
                bool fast = Brain.State == BotState.Telegraph || (_acid != null && _acid.Running);
                _spin += (fast ? 1440f : 120f) * dt;
                if (Parts.spinner != null) Parts.spinner.localRotation = Quaternion.Euler(0f, _spin, 0f);
            }
            else
            {
                // the drone: the figure-eight only while idle or holding (not on its way to a search point)
                float want = _hasGoal ? 0f : 1f;
                _eightWeight = Mathf.MoveTowards(_eightWeight, want, 2f * dt);
                _eightPhase += dt * (Mathf.PI * 2f / 7f);
                Vector3 eight = new Vector3(Mathf.Sin(_eightPhase) * 0.75f, Mathf.Sin(_eightPhase * 2f) * 0.08f, Mathf.Sin(_eightPhase * 2f) * 0.375f);
                transform.position = _pos + Quaternion.Euler(0f, _spawnYaw, 0f) * eight * _eightWeight;
                Vector3 lean = _kick * 6f;
                rig.rotation = Quaternion.Euler(Mathf.Clamp(lean.z, -25f, 25f), _yaw, Mathf.Clamp(-lean.x, -25f, 25f));
                _spin += 1800f * dt;
                if (Parts.spinner != null)
                    for (int i = 0; i < Parts.spinner.childCount; i++) Parts.spinner.GetChild(i).localRotation = Quaternion.Euler(0f, _spin * (i % 2 == 0 ? 1f : -1f), 0f);
                ChargeBall(dt);
            }
        }

        /// <summary>The drone's telegraph: a red ball swelling at the muzzle until it fires (lit, not glowing).</summary>
        void ChargeBall(float dt)
        {
            if (Parts.muzzle == null) return;
            if (_charging)
            {
                if (_chargeBall == null)
                {
                    _chargeBall = SkylineBotLook.Part(PrimitiveType.Sphere, "Charge", Parts.muzzle, Vector3.zero, Vector3.zero, new Color(0.95f, 0.15f, 0.1f), false);
                    _chargeBall.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                _charge += dt;
                float k = Def.telegraphSeconds > 0f ? Mathf.Clamp01(_charge / Def.telegraphSeconds) : 1f;
                _chargeBall.SetActive(true);
                _chargeBall.transform.localScale = Vector3.one * (0.04f + 0.12f * k);
            }
            else if (_chargeBall != null) _chargeBall.SetActive(false);
        }

        // ------------------------------------------------------------------ for directors

        public Collider PlateCollider(int i) =>
            Parts.plates != null && i >= 0 && i < Parts.plates.Length && Parts.plates[i] != null ? Parts.plates[i].GetComponentInChildren<Collider>() : null;

        public Collider WeakPointCollider => Parts.weakPoint != null ? Parts.weakPoint.GetComponentInChildren<Collider>() : null;

        /// <summary>The largest body-zone collider (the chassis).</summary>
        public Collider BodyCollider
        {
            get
            {
                Collider best = null;
                float size = -1f;
                foreach (var z in Rig.GetComponentsInChildren<SkylineHitZone>())
                {
                    if (z.zone != HitZone.Body) continue;
                    var c = z.GetComponent<Collider>();
                    if (c == null) continue;
                    float s = c.bounds.size.sqrMagnitude;
                    if (s > size) { size = s; best = c; }
                }
                return best;
            }
        }

        /// <summary>Where a spot is in the world now.</summary>
        public Vector3 SpotWorld(WeakSpot s) => Rig.TransformPoint(new Vector3(s.local.X, s.local.Y, s.local.Z));

        static NVec3 N(Vector3 v) => new NVec3(v.x, v.y, v.z);
        static Vector3 U(NVec3 v) => new Vector3(v.X, v.Y, v.Z);
    }

    /// <summary>A shed plate or a wreck's part: never blocks a bot's sight, shrinks away over its last second, gone after
    /// <see cref="life"/> seconds.</summary>
    public sealed class SkylineDebris : MonoBehaviour
    {
        public float life = 6f;
        float _age;
        Vector3 _scale;
        bool _root;

        void Start()
        {
            _scale = transform.localScale;
            _root = transform.parent == null || transform.parent.GetComponentInParent<SkylineDebris>() == null;
        }

        void Update()
        {
            if (!_root) return;
            _age += Time.deltaTime;
            float left = life - _age;
            if (left <= 0f) { Destroy(gameObject); return; }
            if (left < 1f) transform.localScale = _scale * left;
        }
    }
}

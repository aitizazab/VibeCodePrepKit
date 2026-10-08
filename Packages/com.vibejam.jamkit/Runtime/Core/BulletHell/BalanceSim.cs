using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using JamKit.Patterns;
using JamKit.Roguelite;

namespace JamKit.BulletHell
{
    /// <summary>One enemy of a simulated room: when it arrives, where from and to, and what it does.</summary>
    public sealed class SimEnemy
    {
        public float time;
        public Vector2 from, anchor;
        public EnemyMoveDef move = new EnemyMoveDef();
        public float hp = 10f;
        public float radius = 0.4f;
        public PatternSequence attack;
        public float attackDelay = 0.4f;
        public bool contact = true;
    }

    /// <summary>A room for the balance report: a list of enemies (the editor builds them from the real waves).</summary>
    public sealed class SimRoom
    {
        public string name = "room";
        public readonly List<SimEnemy> enemies = new List<SimEnemy>();
    }

    /// <summary>The ship's numbers for the simulation (the editor copies them from PlayerDef).</summary>
    public sealed class SimPlayer
    {
        public float moveSpeed = 7.5f;
        public float hitboxRadius = 0.07f;
        public float grazeRadius = 0.6f;
        public float shotSpeed = 20f;
        public float shotDamage = 1f;
        public float fireInterval = 0.085f;
        public float shotLifetime = 1.5f;
        public float shotRadius = 0.12f;
        public float homingTurnRate = 7f;
        public float invulnerableAfterHit = 2f;
        public int lives = 3;
        public SprintDef sprint = new SprintDef();
        public int kitBombs;
        public int bayBombs = 2;
        public int maxBombs = 6;
        public float bombRechargeSeconds = 20f;
        public float bombRadius = 5f;
        public float bombDamage = 40f;
        public float bombInvulnerable = 2.5f;
    }

    /// <summary>What one simulated room produced.</summary>
    public struct SimResult
    {
        public float damage;
        /// <summary>Damage by <see cref="ShotSource"/>.</summary>
        public float[] damageBySource;
        public int hits;
        public int grazes;
        /// <summary>Seconds the bot spent sprinting, and holding still.</summary>
        public float sprintSeconds, stillSeconds;
        /// <summary>The bot's height summed over time (divide by clearSeconds for its mean height: an
        /// aggressive bot plays higher, closer to its targets).</summary>
        public float heightSeconds;
        public float clearSeconds;
        public bool cleared;
        /// <summary>Seconds with at least one enemy on screen (DPS is damage over this).</summary>
        public float engagedSeconds;
    }

    /// <summary>
    /// A headless bullet-hell room with a scripted dodge bot (docs/DESIGN-ROGUELITE.md, section 2,
    /// "Checking it"). It uses the game's own pure rules: <see cref="BulletPool"/>, <see cref="Emitter"/>,
    /// <see cref="EnemyMover"/>, <see cref="ShmupLoadout"/> streams and stats, <see cref="SprintMeter"/>,
    /// <see cref="BombBay"/>, <see cref="PowerBoosts"/> and the companion. The bot
    /// sits low on the screen (where players are), lines up under enemies and dodges the bullets near it.
    /// Deterministic for a seed.
    /// </summary>
    public sealed class BalanceSim
    {
        public float dt = 1f / 60f;
        public float roomTimeLimit = 70f;
        public PlayfieldBounds bounds = PlayfieldBounds.Centered(6f, 5.3f);
        /// <summary>Enemy bullet hit radii by pattern style (index = styleIndex). Missing styles use 0.08.</summary>
        public IReadOnlyList<float> styleRadii;
        public CompanionRules companion;
        public PowerBoostRules boosts = new PowerBoostRules();
        /// <summary>The gun's numbers (BulletHellConfig > Shots); null = the loadout's own.</summary>
        public ShotRules shots;
        /// <summary>The bot's resting height (players sit low, where it's safest).</summary>
        public float homeY = -3.6f;
        /// <summary>Seconds between the bot's decisions (a human's reaction time; 0 = every frame, superhuman).</summary>
        public float reactionSeconds = 0.18f;
        /// <summary>Start every room on the last life (the report sets it for Red Line-style upgrades).</summary>
        public bool startOnLastLife;

        readonly BulletPool _enemyBullets = new BulletPool(4096);
        readonly BulletPool _shots = new BulletPool(1024);
        readonly GhostPhase _ghostPhase = new GhostPhase();
        readonly GuardianAngel _angel = new GuardianAngel();
        ShmupLoadout _loadoutNow;
        bool _gambitOn;
        float _bladeClock;
        int _volley;
        readonly List<BulletSpawn> _spawns = new List<BulletSpawn>(256);
        readonly List<BulletHit> _hits = new List<BulletHit>(128);
        readonly List<Live> _live = new List<Live>(64);
        HitTarget[] _targets = new HitTarget[64];
        readonly int[] _near = new int[512];
        readonly SprintMeter _sprint = new SprintMeter();
        readonly BombBay _bay = new BombBay();
        readonly PowerBoosts _boost = new PowerBoosts();
        readonly CompanionState _buddy = new CompanionState();
        readonly WeaponClock _gun = new WeaponClock();

        sealed class Live
        {
            public SimEnemy def;
            public readonly EnemyMover mover = new EnemyMover();
            public Emitter emitter;
            public float hp, actTime;
            public int id;
            public bool alive;
        }

        sealed class WeaponClock
        {
            float _t;
            public int Tick(float dt, float interval)
            {
                _t += dt;
                int n = 0;
                while (_t >= interval && n < 8) { _t -= interval; n++; }
                return n;
            }
            public void Reset() => _t = 0f;
        }

        public SimResult Play(SimRoom room, ShmupLoadout loadout, SimPlayer player, ulong seed)
        {
            var rng = new Rng(seed);
            var result = new SimResult { damageBySource = new float[SourceCount] };
            _enemyBullets.Clear();
            _shots.Clear();
            if (shots != null && !ReferenceEquals(loadout.Shots, shots)) loadout.Shots = shots;
            _shots.bounceDamageScale = loadout.Shots.reboundAfterBounce;
            _loadoutNow = loadout;
            _ghostPhase.RoomStarted(boosts, loadout.Effect(ShmupEffects.GhostStep));
            _angel.Reset();
            _bladeClock = 0f;
            _volley = 0;
            _live.Clear();
            _boost.Reset();
            _gun.Reset();
            _sprint.Configure(player.sprint, loadout.StaminaMaxScale, loadout.StaminaRegenScale, loadout.RoomSprintBudgetScale);
            _sprint.Fill();
            int capacity = loadout.BombCapacity(player.kitBombs, player.bayBombs, player.maxBombs);
            _bay.Configure(capacity, loadout.BombRechargeSeconds(player.bombRechargeSeconds));
            _bay.Fill();
            bool buddyOn = companion != null;
            if (buddyOn) _buddy.Begin(companion);

            // Each seed starts the bot somewhere else and shifts its reaction phase: different dodges.
            var pos = new Vector2(rng.Range(-2.5f, 2.5f), homeY);
            float invuln = 0f, t = 0f, decide = rng.Range(0f, reactionSeconds);
            int nextSpawn = 0, nextId = 1, livesLost = startOnLastLife ? Math.Max(0, player.lives - 1) : 0;
            var move = Vector2.Zero;
            bool danger = false;
            var order = new List<SimEnemy>(room.enemies);
            order.Sort((a, b) => a.time.CompareTo(b.time));
            int ghost = loadout.Effect(ShmupEffects.GhostStep), slow = loadout.Effect(ShmupEffects.FocusSlow);
            int autoBomb = loadout.Effect(ShmupEffects.AutoBomb);


            while (t < roomTimeLimit)
            {
                t += dt;
                // Arrivals.
                while (nextSpawn < order.Count && order[nextSpawn].time <= t)
                {
                    var d = order[nextSpawn++];
                    var l = new Live { def = d, hp = d.hp, id = nextId++, alive = true };
                    l.mover.Begin(d.move, d.from, d.anchor);
                    if (d.attack != null && d.attack.steps.Count > 0) l.emitter = new Emitter(d.attack, rng);
                    _live.Add(l);
                }

                // The bot moves (and maybe sprints or bombs).
                decide -= dt;
                if (decide <= 0f)
                {
                    decide += Math.Max(dt, reactionSeconds);
                    int nearCount = GatherNear(pos, 2.6f);
                    move = ChooseMove(pos, player, loadout, nearCount, out danger);
                }
                bool wantSprint = danger && _sprint.CanSprint;
                float sprintFraction = _sprint.Tick(dt, wantSprint, move.LengthSquared() > 0.01f);
                bool sprinting = _sprint.IsSprinting;
                float speed = player.moveSpeed * loadout.MoveSpeedScale * (1f + (loadout.SprintMultiplier(player.sprint.speedMultiplier) - 1f) * sprintFraction);
                pos = bounds.Clamp(pos + move * (speed * dt), 0.35f);

                // Boosts (Ember Hunger, Still Point, Red Line).
                int grazed = _enemyBullets.Graze(pos, player.grazeRadius * loadout.GrazeRadiusScale, player.hitboxRadius * loadout.HitboxScale);
                result.grazes += grazed;
                if (sprinting) result.sprintSeconds += dt;
                if (move.LengthSquared() <= 0.01f) result.stillSeconds += dt;
                result.heightSeconds += pos.Y * dt;
                _boost.Tick(dt, boosts, loadout.Effect(ShmupEffects.GrazePower), loadout.Effect(ShmupEffects.LastStand), grazed, livesLost >= player.lives - 1,
                    loadout.Effect(ShmupEffects.StillPoint), move.LengthSquared() > 0.01f);
                _gambitOn = loadout.Effect(ShmupEffects.LastStand) > 0 && livesLost >= player.lives - 1;
                _ghostPhase.Tick(dt, sprinting, boosts);
                _angel.Tick(dt);

                loadout.DamageBoost = _boost.DamageMultiplier;
                loadout.FireRateBoost = _boost.FireRateMultiplier;

                // The ship's guns.
                int volleys = _gun.Tick(dt, loadout.FireInterval(player.fireInterval));
                if (volleys > 0)
                {
                    var streams = loadout.Streams(false);
                    float damage = player.shotDamage * loadout.ShotDamageScale;
                    float shotSpeed = player.shotSpeed * loadout.ShotSpeedScale;
                    for (int v = 0; v < volleys; v++, _volley++)
                        for (int i = 0; i < streams.Count; i++)
                        {
                            var st = streams[i];
                            if (!st.FiresOn(_volley)) continue;
                            _shots.Spawn(new Vector2(pos.X + st.offsetX, pos.Y + st.offsetY), st.angleDegrees * (float)(Math.PI / 180.0), shotSpeed * st.speedScale,
                                (int)st.source, player.shotRadius, st.lifetime > 0f ? st.lifetime : player.shotLifetime, 0f, 0f, damage * st.damageScale, loadout.Pierce, st.Flags);
                        }
                }

                // The companion.
                if (buddyOn)
                {
                    int shots = _buddy.Tick(dt, true, loadout.CompanionCooldownScale);
                    var at = pos + _buddy.Offset;
                    float aim = AimAngle(at);
                    for (int s = 0; s < shots; s++)
                        _shots.Spawn(at, aim, companion.shotSpeed, (int)ShotSource.Companion, player.shotRadius, 1.5f, 0f, 0f,
                            player.shotDamage * companion.damageScale * loadout.CompanionDamageScale, 0);
                }

                // Enemies move and shoot.
                for (int i = 0; i < _live.Count; i++)
                {
                    var l = _live[i];
                    if (!l.alive) continue;
                    l.mover.Tick(dt, pos, bounds);
                    if (l.mover.IsGone) { l.alive = false; continue; }
                    var p = l.mover.Position;
                    bool canFire = l.mover.Phase == MoverPhase.Acting
                        || (l.mover.Phase == MoverPhase.Leaving && l.def.move.kind != EnemyMoveKind.Hover && l.def.move.kind != EnemyMoveKind.Strafe);
                    if (l.emitter == null || !canFire || !bounds.Contains(p, -0.2f)) continue;
                    l.actTime += dt;
                    if (l.actTime < l.def.attackDelay) continue;
                    _spawns.Clear();
                    l.emitter.Tick(dt, p, pos, _spawns);
                    if (_spawns.Count > 0) _enemyBullets.AddSpawns(_spawns, styleRadii, 1f, 0.08f);
                }

                // Shots hit enemies.
                int n = FillTargets();
                if (AnyActive(n)) result.engagedSeconds += dt;
                _shots.SteerHoming(_targets, n, player.homingTurnRate, dt);
                _shots.Step(dt, bounds, 0.8f);
                _enemyBullets.Step(dt, bounds, 0.8f);
                if (slow > 0) _enemyBullets.SlowNear(pos, boosts.slowRadius, boosts.slowScale, dt);
                _shots.CollideTargets(_targets, n, _hits);
                for (int i = 0; i < _hits.Count; i++)
                {
                    var h = _hits[i];
                    float dmg = (h.flags & BulletFlags.Falloff) != 0 ? h.damage * loadout.Shots.ScatterFalloffAt(h.age) : h.damage;
                    DamageTarget(h.targetIndex, dmg, h.style, ref result);
                    // Last Gambit: every hit ricochets once to the nearest other enemy.
                    if (_gambitOn && (h.flags & BulletFlags.Ricocheted) == 0)
                    {
                        int r = LastGambit.RicochetTarget(_targets, n, h.targetId, h.position, boosts.ricochetRange);
                        if (r >= 0)
                        {
                            var to = _targets[r].position - h.position;
                            _shots.Spawn(h.position, (float)Math.Atan2(to.Y, to.X), player.shotSpeed, (int)ShotSource.Ricochet, player.shotRadius, 0.6f, 0f, 0f,
                                dmg * boosts.ricochetDamage, 0, BulletFlags.Ricocheted);
                        }
                    }
                }

                // The Blade: an arc slash in front, instead of the main guns.
                if (loadout.Effect(ShmupEffects.Blade) > 0)
                {
                    _bladeClock += dt;
                    var shots = loadout.Shots;
                    if (_bladeClock >= shots.bladeInterval)
                    {
                        _bladeClock -= shots.bladeInterval;
                        float slash = BladeArc.SlashDamage(shots, player.shotDamage * loadout.ShotDamageScale, loadout.FireInterval(player.fireInterval));
                        for (int i = 0; i < n; i++)
                            if (_targets[i].active && BladeArc.Touches(pos, _targets[i].position, _targets[i].radius, shots.bladeRange, shots.bladeHalfAngle))
                                DamageTarget(i, slash, (int)ShotSource.Blade, ref result);
                    }
                }

                // The Bomb Bay: the bot bombs when bullets crowd it.
                _bay.Tick(dt, true);
                if (_bay.Bombs > 0 && invuln <= 0f && danger && CountWithin(pos, 1.2f) >= 4 && _bay.TryUse())
                {
                    Bomb(pos, player, loadout, ref result, 1f);
                    invuln = player.bombInvulnerable;
                }

                // The player gets hit.
                if (invuln > 0f) invuln -= dt;
                else
                {
                    bool bulletHit = !(ghost > 0 && _ghostPhase.Phasing) && _enemyBullets.FirstOverlap(pos, player.hitboxRadius * loadout.HitboxScale) >= 0;
                    bool bodyHit = false;
                    for (int i = 0; i < _live.Count && !bodyHit; i++)
                    {
                        var l = _live[i];
                        if (!l.alive || !l.def.contact) continue;
                        float r = l.def.radius * 0.8f + player.hitboxRadius * loadout.HitboxScale;
                        if (Vector2.DistanceSquared(l.mover.Position, pos) < r * r) bodyHit = true;
                    }
                    if (bulletHit || bodyHit)
                    {
                        if (buddyOn && _buddy.TryGuard())
                        {
                            _enemyBullets.ClearInCircle(pos, 0.6f);
                            invuln = 0.5f;
                        }
                        else if (autoBomb > 0 && _angel.Ready && _bay.Bombs > 0)
                        {
                            int spent = _angel.TryTrigger(_bay, boosts);
                            Bomb(pos, player, loadout, ref result, GuardianAngel.PowerFor(spent, boosts));
                            invuln = player.bombInvulnerable;
                        }
                        else
                        {
                            result.hits++;
                            livesLost++;
                            invuln = player.invulnerableAfterHit;
                            _enemyBullets.ClearInCircle(pos, 3f);
                        }
                    }
                }

                // Cleared?
                if (nextSpawn >= order.Count && !AnyAlive())
                {
                    result.cleared = true;
                    break;
                }
            }
            result.clearSeconds = Math.Min(t, roomTimeLimit);
            loadout.DamageBoost = 1f;
            loadout.FireRateBoost = 1f;

            return result;
        }

        /// <summary>Damage sources tracked (ShotSource values).</summary>
        public const int SourceCount = 12;

        void Bomb(Vector2 pos, SimPlayer player, ShmupLoadout loadout, ref SimResult result, float powerScale)
        {
            float power = loadout.BombPower * powerScale;
            float radius = player.bombRadius * power, damage = player.bombDamage * power;
            _enemyBullets.Clear();
            int n = FillTargets();
            for (int i = 0; i < n; i++)
                if (_targets[i].active && Vector2.Distance(_targets[i].position, pos) <= radius + _targets[i].radius)
                    DamageTarget(i, damage, (int)ShotSource.Bomb, ref result);
        }

        void DamageTarget(int index, float damage, int source, ref SimResult result)
        {
            var l = Owner(index);
            if (l == null || !l.alive) return;
            float dealt = Math.Min(damage, l.hp);
            l.hp -= damage;
            result.damage += dealt;
            if (source >= 0 && source < result.damageBySource.Length) result.damageBySource[source] += dealt;
            if (l.hp > 0f) return;
            l.alive = false;
            // Last Gambit: a kill bursts in a small blast (blast kills don't blast again).
            if (_gambitOn && source != (int)ShotSource.Blast && _loadoutNow != null)
            {
                var at = l.mover.Position;
                float blast = boosts.blastDamage * _loadoutNow.ShotDamageScale;
                float rr = boosts.blastRadius;
                for (int i = 0; i < _owners.Count; i++)
                {
                    var o = _owners[i];
                    if (o == null || !o.alive) continue;
                    float reach = rr + o.def.radius;
                    if (Vector2.DistanceSquared(o.mover.Position, at) <= reach * reach) DamageTarget(i, blast, (int)ShotSource.Blast, ref result);
                }
            }
        }

        readonly List<Live> _owners = new List<Live>(64);

        Live Owner(int index) => index >= 0 && index < _owners.Count ? _owners[index] : null;

        int FillTargets()
        {
            if (_targets.Length < _live.Count) Array.Resize(ref _targets, _live.Count * 2);
            _owners.Clear();
            int n = 0;
            for (int i = 0; i < _live.Count; i++)
            {
                var l = _live[i];
                if (!l.alive) continue;
                var p = l.mover.Position;
                _targets[n] = new HitTarget { position = p, radius = l.def.radius, id = l.id, active = bounds.Contains(p, 0.2f) };
                _owners.Add(l);
                n++;
            }
            return n;
        }

        bool AnyActive(int n)
        {
            for (int i = 0; i < n; i++) if (_targets[i].active) return true;
            return false;
        }

        bool AnyAlive()
        {
            for (int i = 0; i < _live.Count; i++) if (_live[i].alive) return true;
            return false;
        }

        float AimAngle(Vector2 from)
        {
            float best = float.PositiveInfinity;
            Vector2 target = default;
            bool found = false;
            for (int i = 0; i < _live.Count; i++)
            {
                var l = _live[i];
                if (!l.alive || !bounds.Contains(l.mover.Position, 0f)) continue;
                float d = Vector2.DistanceSquared(l.mover.Position, from);
                if (d < best) { best = d; target = l.mover.Position; found = true; }
            }
            if (!found) return (float)(Math.PI / 2);
            return (float)Math.Atan2(target.Y - from.Y, target.X - from.X);
        }

        int GatherNear(Vector2 pos, float range)
        {
            int n = 0;
            var items = _enemyBullets.Items;
            float rr = range * range;
            for (int i = 0; i < _enemyBullets.Count && n < _near.Length; i++)
                if (Vector2.DistanceSquared(items[i].position, pos) <= rr) _near[n++] = i;
            return n;
        }

        int CountWithin(Vector2 pos, float range)
        {
            int n = 0;
            var items = _enemyBullets.Items;
            float rr = range * range;
            for (int i = 0; i < _enemyBullets.Count; i++)
                if (Vector2.DistanceSquared(items[i].position, pos) <= rr) n++;
            return n;
        }

        static readonly Vector2[] Moves =
        {
            Vector2.Zero, new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f),
            Vector2.Normalize(new Vector2(1f, 1f)), Vector2.Normalize(new Vector2(-1f, 1f)),
            Vector2.Normalize(new Vector2(1f, -1f)), Vector2.Normalize(new Vector2(-1f, -1f)),
        };

        /// <summary>The bot: the move (unit or zero) with the least danger, nudged toward its spot under the
        /// nearest enemy and its resting height. <paramref name="danger"/>: bullets are close (sprint).</summary>
        Vector2 ChooseMove(Vector2 pos, SimPlayer player, ShmupLoadout loadout, int nearCount, out bool danger)
        {
            float targetX = pos.X;
            float bestY = float.NegativeInfinity;
            for (int i = 0; i < _live.Count; i++)
            {
                var l = _live[i];
                if (!l.alive || !bounds.Contains(l.mover.Position, -0.3f)) continue;
                // Line up under the lowest enemy (the nearest threat), like a player does.
                float y = -l.mover.Position.Y;
                if (y > bestY) { bestY = y; targetX = l.mover.Position.X; }
            }
            float step = player.moveSpeed * loadout.MoveSpeedScale * 0.12f;
            // The bot plays to its upgrade, as a player would: with Ember Hunger it hugs bullets (a tighter
            // margin, so it grazes); with Ghost Step it sprints at the first sign of trouble.
            float avoid = loadout.Effect(ShmupEffects.GrazePower) > 0 ? 0.3f : 0.9f;
            // Still Point: a burst mover. It holds still while it's safe and only moves (sprinting) when it must.
            bool burstMover = loadout.Effect(ShmupEffects.StillPoint) > 0;
            // Scatter and Blade: an aggressive bot that closes in under its target.
            float closeIn = loadout.Effect(ShmupEffects.Blade) > 0 ? loadout.Shots.bladeRange * 0.75f
                : loadout.Effect(ShmupEffects.Scatter) > 0 ? player.shotSpeed * loadout.ShotSpeedScale * loadout.Shots.scatterLifetime * 0.5f : 0f;
            float wantY = homeY;
            if (closeIn > 0f && bestY > float.NegativeInfinity)
                wantY = Math.Max(homeY, Math.Min(bounds.maxY - 1.5f, -bestY - closeIn));
            bool sprintLover = loadout.Effect(ShmupEffects.GhostStep) > 0;
            // It sees bullets as they move: slowed inside a Time Dilation bubble.
            float slowR2 = loadout.Effect(ShmupEffects.FocusSlow) > 0 && boosts != null ? boosts.slowRadius * boosts.slowRadius : -1f;
            float slowK = boosts != null ? boosts.slowScale : 1f;
            var items = _enemyBullets.Items;
            float bestScore = float.PositiveInfinity, here = 0f;
            Vector2 best = Vector2.Zero;
            for (int m = 0; m < Moves.Length; m++)
            {
                var p = bounds.Clamp(pos + Moves[m] * step, 0.35f);
                float risk = 0f;
                for (int k = 0; k < nearCount; k++)
                {
                    ref Bullet b = ref items[_near[k]];
                    float c = (float)Math.Cos(b.angle), s = (float)Math.Sin(b.angle);
                    for (int f = 1; f <= 2; f++)
                    {
                        float tau = 0.06f * f;
                        if (slowR2 > 0f && Vector2.DistanceSquared(b.position, pos) <= slowR2) tau *= slowK;
                        float bx = b.position.X + c * b.speed * tau, by = b.position.Y + s * b.speed * tau;
                        float dx = bx - p.X, dy = by - p.Y;
                        float d2 = dx * dx + dy * dy;
                        float r = b.radius + player.hitboxRadius * loadout.HitboxScale + 0.12f;
                        if (d2 < avoid) risk += 1f / (d2 + 0.01f) * (d2 < r * r ? 8f : 1f);
                    }
                }
                for (int i = 0; i < _live.Count; i++)
                {
                    var l = _live[i];
                    if (!l.alive || !l.def.contact) continue;
                    float d2 = Vector2.DistanceSquared(l.mover.Position, p);
                    if (d2 < 2.5f) risk += 3f / (d2 + 0.05f);
                }
                if (m == 0) here = risk;
                float score = risk * 0.05f + Math.Abs(p.X - targetX) * 0.25f + Math.Abs(p.Y - wantY) * 0.35f;
                if (score < bestScore) { bestScore = score; best = Moves[m]; }
            }
            danger = here > (sprintLover && (_ghostPhase.Ready || loadout.Effect(ShmupEffects.GhostStep) <= 0) ? 8f : 25f);
            if (burstMover)
            {
                // Still while it's safe and lined up (the damage ramps); a sprinting burst when it has to move.
                if (here < 4f && Math.Abs(pos.X - targetX) < 0.7f) return Vector2.Zero;
                danger = here >= 4f;
            }
            // Close enough to the spot: hold still (no jitter).
            if (best != Vector2.Zero && here < 1f && Math.Abs(pos.X - targetX) < 0.12f && Math.Abs(pos.Y - homeY) < 0.12f) best = Vector2.Zero;
            return best;
        }
    }

    public enum BalanceVerdict { Useful, Flagged, NotSimulated }

    /// <summary>One upgrade's line in the balance report.</summary>
    public sealed class BalanceRow
    {
        public string id, name;
        public Rarity rarity;
        public BalanceVerdict verdict;
        /// <summary>DPS with it / DPS of the baseline (no upgrades).</summary>
        public float dpsRatio = 1f;
        /// <summary>Share of all damage dealt by its own source (its streams, wake, bombs, companion).</summary>
        public float damageShare;
        public float hitsPerRoom, baselineHitsPerRoom;
        public float clearSeconds, baselineClearSeconds;
        public float grazesPerRoom, baselineGrazesPerRoom;
        /// <summary>The best of: DPS gain, fewer hits (fraction), faster clears (fraction). Flagged under the threshold.</summary>
        public float usefulness;
        public string note = "";
    }

    /// <summary>
    /// The balance report: a baseline (no upgrades) and one run per upgrade over the same seeded rooms,
    /// then per upgrade its DPS against the baseline, its own damage share, hits taken and time to clear.
    /// Anything whose usefulness is under the threshold is flagged (a rear gun that never hits, a stat
    /// too small to matter). Upgrades that only touch the economy or lives are listed as not simulated.
    /// </summary>
    public static class BalanceReport
    {
        /// <summary>Usefulness under this flags an upgrade (0.05 = it must improve something by 5%).</summary>
        public const float DefaultThreshold = 0.05f;

        static readonly string[] EconomyStats = { ShmupStats.Luck, ShmupStats.Magnet, ShmupStats.CoinGain };
        static readonly string[] EconomyEffects = { ShmupEffects.LifeUp, ShmupEffects.GrazeHeal, ShmupEffects.BombPlus };

        /// <summary>True when the simulation can't see what it does (coins, luck, lives, instant pickups).</summary>
        public static bool IsNotSimulated(UpgradeDef def)
        {
            if (def == null) return true;
            bool any = false;
            if (def.modifiers != null)
                foreach (var m in def.modifiers)
                {
                    if (m == null) continue;
                    if (Array.IndexOf(EconomyStats, m.stat) < 0) return false;
                    any = true;
                }
            if (def.effects != null)
                foreach (var e in def.effects)
                {
                    if (Array.IndexOf(EconomyEffects, e) >= 0 || ShmupEffects.TryParseAmount(e, ShmupEffects.Coins, out _)) { any = true; continue; }
                    return false;
                }
            return any || (def.modifiers == null || def.modifiers.Count == 0);
        }

        /// <summary>True when all it changes is the graze ring: worth score (and Ember Hunger or Close Shave),
        /// which DPS, hits and clear time can't show. Simulated for its grazes, listed as not simulated.</summary>
        public static bool IsGrazeOnly(UpgradeDef def)
        {
            if (def == null || def.modifiers == null || def.modifiers.Count == 0) return false;
            if (def.effects != null && def.effects.Count > 0) return false;
            foreach (var m in def.modifiers)
                if (m == null || m.stat != ShmupStats.GrazeRadius) return false;
            return true;
        }

        /// <summary>
        /// Fewer hits as a fraction of the baseline's, when it's more than noise: the hits saved over the
        /// whole sample must be at least 2 and at least the square root of the baseline's hits (about one
        /// standard deviation of a count). 61 hits down to 50 counts (18%); 6 down to 4 doesn't.
        /// </summary>
        public static float HitGain(int baselineHits, int hits)
        {
            int saved = baselineHits - hits;
            if (baselineHits <= 0 || saved < Math.Max(2.0, Math.Sqrt(baselineHits))) return 0f;
            return saved / (float)baselineHits;
        }

        /// <summary>The damage sources an upgrade adds (its share is credited to it).</summary>
        public static void SourcesOf(UpgradeDef def, List<ShotSource> into)
        {
            into.Clear();
            if (def == null || def.effects == null) return;
            foreach (var e in def.effects)
            {
                if (e == ShmupEffects.Spread) into.Add(ShotSource.Spread);
                else if (e == ShmupEffects.Homing) { into.Add(ShotSource.Homing); into.Add(ShotSource.Spread); }
                else if (e == ShmupEffects.RearShot) into.Add(ShotSource.Rear);
                else if (e == ShmupEffects.Rebound) into.Add(ShotSource.Rebound);
                else if (e == ShmupEffects.BombBay || e == ShmupEffects.BiggerBombs) into.Add(ShotSource.Bomb);
                else if (e == ShmupEffects.Scatter) into.Add(ShotSource.Scatter);
                else if (e == ShmupEffects.Blade) into.Add(ShotSource.Blade);
                else if (e == ShmupEffects.LastStand) { into.Add(ShotSource.Ricochet); into.Add(ShotSource.Blast); }
            }
            if (def.modifiers != null)
                foreach (var m in def.modifiers)
                    if (m != null && m.stat == ShmupStats.CompanionDamage) into.Add(ShotSource.Companion);
        }

        /// <summary>
        /// Plays the baseline and every upgrade of <paramref name="pool"/> over <paramref name="rooms"/> x
        /// <paramref name="seeds"/>. An upgrade's <c>requires</c> are owned for its runs (a Bomb Bay upgrade
        /// is tested with the Bay; its share counts the Bay's bombs only if it adds them).
        /// </summary>
        public static List<BalanceRow> Run(BalanceSim sim, IReadOnlyList<SimRoom> rooms, SimPlayer player, IReadOnlyList<UpgradeDef> pool,
            int seeds, float threshold = DefaultThreshold, Func<string, UpgradeDef> lookup = null)
        {
            var rows = new List<BalanceRow>();
            var loadout = new ShmupLoadout();
            var baseResult = PlayAll(sim, rooms, player, loadout, seeds);
            var sources = new List<ShotSource>();
            foreach (var def in pool)
            {
                if (def == null) continue;
                var row = new BalanceRow { id = def.id, name = def.displayName, rarity = def.rarity };
                rows.Add(row);
                if (IsNotSimulated(def))
                {
                    row.verdict = BalanceVerdict.NotSimulated;
                    row.note = "economy or lives: not measured";
                    continue;
                }
                // Its requirements first, measured against a baseline that has them too.
                loadout.Reset();
                bool hasReq = false;
                if (def.requires != null)
                    foreach (var r in def.requires)
                    {
                        var req = lookup != null ? lookup(r) : null;
                        if (req != null) { loadout.Inventory.ForceTake(req); hasReq = true; }
                    }
                var reference = hasReq ? PlayAll(sim, rooms, player, loadout, seeds) : baseResult;
                loadout.Inventory.ForceTake(def);
                // An upgrade that only works on the last life is measured there.
                bool lastLife = def.effects != null && def.effects.Contains(ShmupEffects.LastStand);
                sim.startOnLastLife = lastLife;
                var with = PlayAll(sim, rooms, player, loadout, seeds);
                sim.startOnLastLife = false;
                if (lastLife) row.note = "measured on the last life";
                SourcesOf(def, sources);
                float own = 0f;
                foreach (var s in sources) own += with.damageBySource[(int)s] - (hasReq ? reference.damageBySource[(int)s] : 0f);
                Fill(row, with, reference, own, threshold, rooms.Count * seeds);
                if (hasReq) row.note = "measured with " + string.Join(", ", def.requires);
                if (IsGrazeOnly(def))
                {
                    row.verdict = BalanceVerdict.NotSimulated;
                    float ratio = row.baselineGrazesPerRoom > 0f ? row.grazesPerRoom / row.baselineGrazesPerRoom : 1f;
                    row.note = "score and graze synergies only (Ember Hunger, Close Shave): grazes x" + F(ratio) + " ("
                        + F(row.grazesPerRoom, "0") + " a room)";
                }
            }
            return rows;
        }

        /// <summary>The baseline line (no upgrades), for the report's header.</summary>
        public static SimResult Baseline(BalanceSim sim, IReadOnlyList<SimRoom> rooms, SimPlayer player, int seeds) =>
            PlayAll(sim, rooms, player, new ShmupLoadout(), seeds);

        static void Fill(BalanceRow row, SimResult with, SimResult reference, float own, float threshold, int count)
        {
            float dps = with.engagedSeconds > 0f ? with.damage / with.engagedSeconds : 0f;
            float baseDps = reference.engagedSeconds > 0f ? reference.damage / reference.engagedSeconds : 0f;
            row.dpsRatio = baseDps > 0f ? dps / baseDps : 1f;
            row.damageShare = with.damage > 0f ? Math.Max(0f, own) / with.damage : 0f;
            row.hitsPerRoom = with.hits / (float)Math.Max(1, count);
            row.baselineHitsPerRoom = reference.hits / (float)Math.Max(1, count);
            row.clearSeconds = with.clearSeconds / Math.Max(1, count);
            row.baselineClearSeconds = reference.clearSeconds / Math.Max(1, count);
            row.grazesPerRoom = with.grazes / (float)Math.Max(1, count);
            row.baselineGrazesPerRoom = reference.grazes / (float)Math.Max(1, count);
            float dpsGain = row.dpsRatio - 1f;
            float hitGain = HitGain(reference.hits, with.hits);
            float timeGain = row.baselineClearSeconds > 0f ? (row.baselineClearSeconds - row.clearSeconds) / row.baselineClearSeconds : 0f;
            row.usefulness = Math.Max(dpsGain, Math.Max(hitGain, timeGain));
            row.verdict = row.usefulness < threshold ? BalanceVerdict.Flagged : BalanceVerdict.Useful;
        }

        static SimResult PlayAll(BalanceSim sim, IReadOnlyList<SimRoom> rooms, SimPlayer player, ShmupLoadout loadout, int seeds)
        {
            var total = new SimResult { damageBySource = new float[BalanceSim.SourceCount] };
            for (int s = 0; s < Math.Max(1, seeds); s++)
                for (int r = 0; r < rooms.Count; r++)
                {
                    var res = sim.Play(rooms[r], loadout, player, 0x5EED0000UL + (ulong)(s * 7919 + r * 104729));
                    total.damage += res.damage;
                    total.hits += res.hits;
                    total.grazes += res.grazes;
                    total.sprintSeconds += res.sprintSeconds;
                    total.clearSeconds += res.clearSeconds;
                    total.engagedSeconds += res.engagedSeconds;
                    if (res.cleared) total.cleared = true;
                    for (int i = 0; i < total.damageBySource.Length; i++) total.damageBySource[i] += res.damageBySource[i];
                }
            return total;
        }

        static string F(float v, string fmt = "0.00") => v.ToString(fmt, CultureInfo.InvariantCulture);

        public static string ToMarkdown(List<BalanceRow> rows, SimResult baseline, int roomCount, int seeds, float threshold)
        {
            var sb = new StringBuilder();
            int n = Math.Max(1, roomCount * seeds);
            sb.AppendLine("# Bullet hell balance report");
            sb.AppendLine();
            sb.AppendLine("A scripted dodge bot played " + roomCount + " rooms x " + seeds + " seeds with each upgrade alone (and a baseline with none).");
            sb.AppendLine("Baseline: " + F(baseline.damage / Math.Max(0.01f, baseline.engagedSeconds), "0.0") + " DPS, " + F(baseline.hits / (float)n)
                + " hits per room, " + F(baseline.clearSeconds / n, "0.0") + " s per room.");
            sb.AppendLine("Usefulness = the best of DPS gain, fewer hits and faster clears. Under " + F(threshold * 100f, "0") + "% it's FLAGGED."
                + " Fewer hits count once the hits saved reach 2 and the square root of the baseline's hits (noise).");
            sb.AppendLine();
            sb.AppendLine("| Upgrade | Rarity | Verdict | DPS vs baseline | Own damage share | Hits/room (base) | Clear s (base) | Usefulness | Note |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
            foreach (var r in rows)
            {
                if (r.verdict == BalanceVerdict.NotSimulated)
                {
                    sb.AppendLine("| " + r.name + " | " + r.rarity + " | not simulated | | | | | | " + r.note + " |");
                    continue;
                }
                // (hits saved count only past the noise margin: see BalanceReport.HitGain)
                sb.AppendLine("| " + r.name + " | " + r.rarity + " | " + (r.verdict == BalanceVerdict.Flagged ? "**FLAGGED**" : "useful") + " | x"
                    + F(r.dpsRatio) + " | " + F(r.damageShare * 100f, "0") + "% | " + F(r.hitsPerRoom) + " (" + F(r.baselineHitsPerRoom) + ") | "
                    + F(r.clearSeconds, "0.0") + " (" + F(r.baselineClearSeconds, "0.0") + ") | " + F(r.usefulness * 100f, "0") + "% | " + r.note + " |");
            }
            return sb.ToString();
        }

        public static string ToCsv(List<BalanceRow> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("id,name,rarity,verdict,dps_ratio,damage_share,hits_per_room,baseline_hits_per_room,clear_seconds,baseline_clear_seconds,usefulness,note");
            foreach (var r in rows)
                sb.AppendLine(r.id + ",\"" + r.name + "\"," + r.rarity + "," + r.verdict + "," + F(r.dpsRatio, "0.000") + "," + F(r.damageShare, "0.000") + ","
                    + F(r.hitsPerRoom, "0.000") + "," + F(r.baselineHitsPerRoom, "0.000") + "," + F(r.clearSeconds, "0.00") + "," + F(r.baselineClearSeconds, "0.00")
                    + "," + F(r.usefulness, "0.000") + ",\"" + r.note + "\"");
            return sb.ToString();
        }

        /// <summary>
        /// A small stand-in for floor 1-2 (no Unity needed): sweeping drones, diving gnats, an aimed turret,
        /// a spinner and a ring bloom, at the template's numbers. The editor tool uses the real rooms instead.
        /// </summary>
        public static List<SimRoom> DefaultRooms()
        {
            var rooms = new List<SimRoom>();
            var aimed = new PatternDef { shape = PatternShape.Aimed, count = 1, speed = 3.6f, shots = 0, shotInterval = 1.6f, startDelay = 0.5f };
            var triple = PatternLibrary.AimedTriple(5f);
            var spiral = new PatternDef { shape = PatternShape.Spiral, count = 3, rotatePerShotDegrees = 13f, shots = 0, shotInterval = 0.13f, speed = 2.8f, lifetime = 9f };
            var ring = new PatternDef { shape = PatternShape.Ring, count = 16, speed = 2.6f, shots = 0, shotInterval = 2.2f, rotatePerShotDegrees = 11f };
            var sweep = new EnemyMoveDef { kind = EnemyMoveKind.Sweep, speed = 3.2f, amplitude = 0.6f, frequency = 0.35f };
            var dive = new EnemyMoveDef { kind = EnemyMoveKind.Dive, enterSeconds = 0.9f, actSeconds = 0.5f, speed = 7f };
            var hover = new EnemyMoveDef { kind = EnemyMoveKind.Hover, enterSeconds = 1.2f, actSeconds = 9f, speed = 4f };
            var strafe = new EnemyMoveDef { kind = EnemyMoveKind.Strafe, enterSeconds = 1.2f, amplitude = 2f, frequency = 0.15f, actSeconds = 12f, speed = 4f };

            var a = new SimRoom { name = "drones and gnats" };
            for (int i = 0; i < 5; i++) a.enemies.Add(Enemy(0.45f * i, new Vector2(-7f, 3.6f), new Vector2(-7f, 3.6f), sweep, 4f, 0.3f, Seq(aimed)));
            for (int i = 0; i < 5; i++) a.enemies.Add(Enemy(1.2f + 0.45f * i, new Vector2(7f, 2.4f), new Vector2(7f, 2.4f), sweep, 4f, 0.3f, Seq(aimed)));
            for (int i = 0; i < 6; i++) a.enemies.Add(Enemy(9f + 0.3f * i, new Vector2(-4.5f + 1.8f * i, 6.3f), new Vector2(-4.5f + 1.8f * i, 3.2f), dive, 3f, 0.25f, null));
            rooms.Add(a);

            var b = new SimRoom { name = "turret pair" };
            b.enemies.Add(Enemy(0f, new Vector2(-3f, 6.3f), new Vector2(-3f, 3.5f), hover, 22f, 0.42f, Seq(triple, true, 1.4f)));
            b.enemies.Add(Enemy(0f, new Vector2(3f, 6.3f), new Vector2(3f, 3.5f), hover, 22f, 0.42f, Seq(triple, true, 1.4f)));
            for (int i = 0; i < 4; i++) b.enemies.Add(Enemy(2f + 0.5f * i, new Vector2(-7f, 2.2f), new Vector2(-7f, 2.2f), sweep, 4f, 0.3f, Seq(aimed)));
            for (int i = 0; i < 5; i++) b.enemies.Add(Enemy(8f + 0.3f * i, new Vector2(-3.6f + 1.8f * i, 6.3f), new Vector2(-3.6f + 1.8f * i, 3f), dive, 3f, 0.25f, null));
            rooms.Add(b);

            var c = new SimRoom { name = "spinner and bloom" };
            c.enemies.Add(Enemy(0f, new Vector2(0f, 6.3f), new Vector2(0f, 3.3f), strafe, 30f, 0.42f, Seq(spiral)));
            for (int i = 0; i < 4; i++) c.enemies.Add(Enemy(3f + 0.35f * i, new Vector2(-3.75f + 2.5f * i, 6.3f), new Vector2(-3.75f + 2.5f * i, 3f), dive, 3f, 0.25f, null));
            c.enemies.Add(Enemy(10f, new Vector2(-2.6f, 6.3f), new Vector2(-2.6f, 3.1f), hover, 40f, 0.48f, Seq(ring)));
            c.enemies.Add(Enemy(10f, new Vector2(2.6f, 6.3f), new Vector2(2.6f, 3.1f), hover, 40f, 0.48f, Seq(ring)));
            rooms.Add(c);

            // A boss-sized target with dense, mixed patterns: where DPS and dodging both matter.
            var d = new SimRoom { name = "boss" };
            var bossAttack = new PatternSequence { loop = true, loopDelay = 0.7f };
            bossAttack.Add(0f, Style(PatternLibrary.RingBurst(20, 2.6f), 2));
            bossAttack.Add(0.8f, PatternLibrary.AimedTriple(5f));
            bossAttack.Add(1.6f, Style(PatternLibrary.RingBurst(20, 2.6f), 2, 9f));
            var spiralFlower = PatternLibrary.SpiralFlower(2, 2.8f);
            spiralFlower.shots = 40;
            spiralFlower.shotInterval = 0.08f;
            bossAttack.Add(2.6f, spiralFlower);
            d.enemies.Add(new SimEnemy
            {
                time = 0f, from = new Vector2(0f, 7f), anchor = new Vector2(0f, 3.1f), hp = 420f, radius = 0.95f, attack = bossAttack, attackDelay = 0.3f,
                move = new EnemyMoveDef { kind = EnemyMoveKind.Strafe, enterSeconds = 1.5f, amplitude = 2.5f, frequency = 0.12f, actSeconds = 0f, speed = 3f },
            });
            rooms.Add(d);

            // A crowded room: two heavy blooms and a curling escort.
            var e = new SimRoom { name = "crowded" };
            var heavy = new PatternSequence { loop = true, loopDelay = 0.8f };
            heavy.Add(0f, Style(PatternLibrary.RingBurst(22, 2.4f), 2));
            heavy.Add(0.9f, PatternLibrary.AimedTriple(5.5f));
            var hoverLong = new EnemyMoveDef { kind = EnemyMoveKind.Hover, enterSeconds = 1.4f, actSeconds = 0f, speed = 3f };
            e.enemies.Add(Enemy(0f, new Vector2(-2.8f, 7f), new Vector2(-2.8f, 3.3f), hoverLong, 120f, 0.75f, heavy));
            e.enemies.Add(Enemy(1.5f, new Vector2(3f, 7f), new Vector2(3f, 3f), hoverLong, 120f, 0.75f, Seq(PatternLibrary.CurlRing(14, 2.4f), true, 1.8f)));
            for (int i = 0; i < 5; i++) e.enemies.Add(Enemy(4f + 0.45f * i, new Vector2(7f, 2.2f), new Vector2(7f, 2.2f), sweep, 4f, 0.3f, Seq(aimed)));
            for (int i = 0; i < 4; i++) e.enemies.Add(Enemy(9f + 0.35f * i, new Vector2(-3.9f + 2.6f * i, 6.3f), new Vector2(-3.9f + 2.6f * i, 3.6f), dive, 3f, 0.25f, null));
            rooms.Add(e);
            return rooms;
        }

        static PatternDef Style(PatternDef p, int style, float baseAngle = 0f)
        {
            p.styleIndex = style;
            if (baseAngle != 0f) p.baseAngleDegrees = baseAngle;
            return p;
        }

        static SimEnemy Enemy(float time, Vector2 from, Vector2 anchor, EnemyMoveDef move, float hp, float radius, PatternSequence attack) =>
            new SimEnemy { time = time, from = from, anchor = anchor, move = move, hp = hp, radius = radius, attack = attack };

        static PatternSequence Seq(PatternDef p, bool loop = false, float loopDelay = 0f)
        {
            var s = new PatternSequence { loop = loop, loopDelay = loopDelay };
            s.Add(0f, p);
            return s;
        }
    }
}

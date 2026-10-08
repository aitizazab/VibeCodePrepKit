using System.Collections.Generic;
using JamKit.Combat;
using JamKit.Patterns;
using UnityEngine;
using UnityEngine.AI;

namespace JamKit.Doomlike
{
    /// <summary>
    /// One enemy in a level: core <see cref="EnemyBrain"/> decisions, core <see cref="Health"/>, a
    /// NavMeshAgent to walk, an 8-angle billboard and its attacks (melee, telegraphed hitscan, or a
    /// core <see cref="Emitter"/> pattern laid on the floor plane). DoomGame ticks it in order;
    /// nothing here runs on its own Update.
    /// </summary>
    public sealed class DoomEnemy : MonoBehaviour
    {
        public EnemyAsset asset;
        public int id;
        public NavMeshAgent agent;
        public CapsuleCollider body;
        public Billboard billboard;
        public LineRenderer aimLine;

        EnemyBrain _brain;
        Health _health;
        Rng _rng;
        Emitter _primary, _secondary, _firing;
        EnemyAttack _firingAttack;
        float _facing;
        float _losTimer, _repathTimer;
        bool _canSee;
        float _hitFlash, _tracer;
        float _walkPhase;
        float _deathTime;
        bool _gibbed;
        bool _dead;
        bool _counted;
        readonly List<BulletSpawn> _spawns = new List<BulletSpawn>(64);

        public EnemyBrain Brain => _brain;
        public Health Health => _health;
        public bool Alive => !_dead;
        public bool Gibbed => _gibbed;
        public bool CanSeePlayer => _canSee;
        public EnemyState State => _brain.State;
        /// <summary>Its facing, a 2D angle in radians.</summary>
        public float Facing => _facing;
        public float TopHeight => asset.hoverHeight + asset.height;
        public Vector3 Center => transform.position + Vector3.up * (asset.hoverHeight + asset.height * 0.55f);
        /// <summary>Times it has landed an attack on the player (god mode still counts), for tests.</summary>
        public int AttacksLanded;
        public int AttacksMade;

        /// <summary>Builds an enemy at its spawn point. Call after the level's NavMesh exists.</summary>
        public static DoomEnemy Create(EnemySpawn spawn, DoomGame game, Transform parent, int id, Rng rng)
        {
            var a = spawn.asset;
            var go = new GameObject(a.displayName + " " + id);
            go.layer = LevelBuilder.ActorLayer;
            go.transform.SetParent(parent, false);
            go.transform.position = spawn.position;

            var e = go.AddComponent<DoomEnemy>();
            e.asset = a;
            e.id = id;
            e._rng = rng;
            e._facing = spawn.facing;
            e._brain = new EnemyBrain(BrainFor(a)) { secondaryChance = a.secondaryChance };
            e._health = new Health(Mathf.Max(1f, a.health));
            e._brain.Alerted += e.OnAlerted;
            e._brain.AttackStarted += e.OnAttackStarted;
            e._brain.AttackFired += e.OnAttackFired;
            e._brain.PainStarted += e.OnPain;
            if (a.attack != null && a.attack.kind == EnemyAttackKind.Projectile && a.attack.pattern != null)
                e._primary = new Emitter(a.attack.pattern, rng);
            if (a.secondaryChance > 0f && a.secondaryAttack != null && a.secondaryAttack.kind == EnemyAttackKind.Projectile && a.secondaryAttack.pattern != null)
                e._secondary = new Emitter(a.secondaryAttack.pattern, rng);

            var cap = go.AddComponent<CapsuleCollider>();
            cap.radius = Mathf.Max(0.15f, a.radius);
            cap.height = Mathf.Max(cap.radius * 2f, a.height);
            cap.center = new Vector3(0f, a.hoverHeight + cap.height * 0.5f, 0f);
            e.body = cap;

            // Only add the agent on the NavMesh (Unity warns otherwise): snap to it, or stand still.
            bool onMesh = NavMesh.SamplePosition(spawn.position, out var navHit, 1.6f, NavMesh.AllAreas);
            if (onMesh) go.transform.position = navHit.position;
            NavMeshAgent agent = null;
            if (onMesh) agent = go.AddComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.radius = Mathf.Clamp(a.radius, 0.2f, 0.9f);
                agent.height = Mathf.Max(1f, a.height);
                agent.speed = a.speed;
                agent.acceleration = 40f;
                agent.angularSpeed = 0f;
                agent.updateRotation = false;
                agent.autoBraking = true;
                agent.stoppingDistance = StopDistance(a, game);
                agent.baseOffset = 0f;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            }
            e.agent = agent;

            float aspect = a.sprite != null ? a.sprite.CellAspect : 1f;
            var tex = a.sprite != null ? a.sprite.texture : null;
            e.billboard = Billboard.Create("Sprite", go.transform, spawn.position + Vector3.up * a.hoverHeight,
                a.height * aspect, a.height, game.config.spriteMaterial, tex, a.tint);
            e.billboard.transform.localPosition = new Vector3(0f, a.hoverHeight, 0f);

            if (UsesAimLine(a.attack) || UsesAimLine(a.secondaryAttack))
            {
                var lineGo = new GameObject("Aim line");
                lineGo.transform.SetParent(go.transform, false);
                var lr = lineGo.AddComponent<LineRenderer>();
                lr.sharedMaterial = game.config.lineMaterial;
                lr.positionCount = 2;
                lr.useWorldSpace = true;
                lr.startWidth = 0.035f;
                lr.endWidth = 0.02f;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.enabled = false;
                e.aimLine = lr;
            }
            e.UpdateSprite(game);
            return e;
        }

        static bool UsesAimLine(EnemyAttack a) => a != null && a.kind == EnemyAttackKind.Hitscan;

        static float StopDistance(EnemyAsset a, DoomGame game)
        {
            if (a.attack != null && a.attack.kind == EnemyAttackKind.Melee)
                return Mathf.Max(0.3f, a.radius + game.config.player.radius + a.attack.meleeRange * 0.5f);
            return 1.5f;
        }

        static readonly Dictionary<EnemyAsset, EnemyBrainDef> BrainCache = new Dictionary<EnemyAsset, EnemyBrainDef>();

        /// <summary>A per-type copy of the brain data with the fire phase stretched to cover the
        /// longest pattern (the asset itself is never changed).</summary>
        static EnemyBrainDef BrainFor(EnemyAsset a)
        {
            if (BrainCache.TryGetValue(a, out var def) && def != null) return def;
            def = JsonUtility.FromJson<EnemyBrainDef>(JsonUtility.ToJson(a.brain ?? new EnemyBrainDef()));
            float longest = 0f;
            if (a.attack != null && a.attack.kind == EnemyAttackKind.Projectile && a.attack.pattern != null)
                longest = Mathf.Max(longest, a.attack.pattern.Duration);
            if (a.secondaryAttack != null && a.secondaryChance > 0f && a.secondaryAttack.kind == EnemyAttackKind.Projectile && a.secondaryAttack.pattern != null)
                longest = Mathf.Max(longest, a.secondaryAttack.pattern.Duration);
            if (!float.IsInfinity(longest)) def.fireSeconds = Mathf.Max(def.fireSeconds, longest + 0.05f);
            BrainCache[a] = def;
            return def;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => BrainCache.Clear();

        /// <summary>Forget cached brain copies (after editing enemy assets in play mode).</summary>
        public static void ClearCache() => BrainCache.Clear();

        // ---------- Per frame ----------

        public void Tick(float dt, DoomGame game)
        {
            var player = game.player;
            if (_dead)
            {
                _deathTime += dt;
                UpdateSprite(game);
                return;
            }
            _health.Tick(dt);
            Vector3 pos = transform.position;
            Vector3 target = player.transform.position;
            Vector3 to = target - pos;
            to.y = 0f;
            float dist = to.magnitude;

            _losTimer -= dt;
            if (_losTimer <= 0f)
            {
                _losTimer = 0.12f + (id % 5) * 0.012f;
                _canSee = player.Alive && dist <= Mathf.Max(asset.brain.sightRange, asset.brain.attackRange) + 2f && game.LineOfSight(EyePosition, player.EyePosition);
            }

            float toAngle = dist > 0.01f ? Mathf.Atan2(to.z, to.x) : _facing;
            var senses = new BrainSenses
            {
                canSee = _canSee,
                distance = dist,
                angleOffFacing = GridMath.WrapSigned(toAngle - _facing),
                heardNoise = game.HeardNoise(pos),
            };
            _brain.Tick(dt, senses, _rng);
            if (_dead) return;

            // Fire phase: a pattern keeps spawning shots.
            if (_firing != null && _brain.State == EnemyState.Attack && _brain.Phase == AttackPhase.Fire)
                TickPattern(dt, game);
            else _firing = null;

            Move(dt, game, target, toAngle);
            if (_hitFlash > 0f) _hitFlash -= dt;
            if (_tracer > 0f) _tracer -= dt;
            UpdateAimLine(game);
            UpdateSprite(game);
        }

        public Vector3 EyePosition => transform.position + Vector3.up * (asset.hoverHeight + asset.height * 0.8f);

        void Move(float dt, DoomGame game, Vector3 target, float toAngle)
        {
            bool canWalk = agent != null && agent.enabled && agent.isOnNavMesh;
            if (canWalk)
            {
                if (_brain.WantsToMove)
                {
                    agent.speed = asset.speed * _brain.SpeedScale;
                    if (agent.isStopped) agent.isStopped = false;
                    _repathTimer -= dt;
                    if (_repathTimer <= 0f || !agent.hasPath)
                    {
                        _repathTimer = 0.25f + (id % 4) * 0.03f;
                        agent.SetDestination(target);
                    }
                }
                else if (!agent.isStopped)
                {
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                }
            }

            float want = _facing;
            Vector3 v = canWalk ? agent.velocity : Vector3.zero;
            v.y = 0f;
            bool moving = v.sqrMagnitude > 0.15f;
            if (_brain.FacesTarget || (_canSee && _brain.State == EnemyState.Chase && !moving)) want = toAngle;
            else if (moving) want = Mathf.Atan2(v.z, v.x);
            float turn = 10f * dt;
            _facing = GridMath.Wrap(_facing + Mathf.Clamp(GridMath.WrapSigned(want - _facing), -turn, turn));
            if (moving) _walkPhase += v.magnitude * dt / Mathf.Max(0.5f, asset.height * 0.6f);
        }

        // ---------- Attacks ----------

        EnemyAttack AttackFor(int index) => index == 1 && asset.secondaryAttack != null && asset.secondaryChance > 0f ? asset.secondaryAttack : asset.attack;

        void OnAlerted()
        {
            var g = DoomGame.Current;
            if (g != null) g.PlayAt(asset.alertSound, transform.position, 0.8f);
        }

        void OnPain()
        {
            _firing = null;
            var g = DoomGame.Current;
            if (g != null) g.PlayAt(asset.painSound, transform.position, 0.7f);
        }

        void OnAttackStarted(int index) => AttacksMade++;

        void OnAttackFired(int index)
        {
            var game = DoomGame.Current;
            if (game == null) return;
            var a = AttackFor(index);
            if (a == null) return;
            var player = game.player;
            game.PlayAt(a.sound, transform.position, 0.9f);
            switch (a.kind)
            {
                case EnemyAttackKind.Melee:
                {
                    Vector3 d = player.transform.position - transform.position;
                    d.y = 0f;
                    if (player.Alive && d.magnitude <= asset.radius + player.Radius + a.meleeRange && _canSee)
                    {
                        AttacksLanded++;
                        game.DamagePlayer(a.damage, transform.position, DamageKind.Melee);
                    }
                    break;
                }
                case EnemyAttackKind.Hitscan:
                {
                    _tracer = 0.07f;
                    if (!player.Alive || !game.LineOfSight(EyePosition, player.EyePosition)) break;
                    float dist = Vector3.Distance(EyePosition, player.EyePosition);
                    float chance = HitscanMath.EnemyHitChance(a.accuracy, dist, player.Speed);
                    float total = 0f;
                    for (int i = 0; i < Mathf.Max(1, a.pellets); i++)
                        if (_rng.Chance(chance)) total += HitscanMath.Roll(a.damage, 0.2f, _rng);
                    if (total > 0f)
                    {
                        AttacksLanded++;
                        game.DamagePlayer(total, transform.position, DamageKind.Bullet);
                    }
                    else game.fx.Spawn(FxField.Fx.Puff, player.EyePosition + Random.insideUnitSphere * 0.8f, 0.4f, Color.white);
                    break;
                }
                case EnemyAttackKind.Projectile:
                {
                    var em = index == 1 && _secondary != null ? _secondary : _primary;
                    if (em == null) break;
                    em.Reset();
                    var s = GameRootSettings();
                    em.SpeedScale = s != null ? s.enemyBulletSpeedScale : 1f;
                    em.DensityScale = s != null ? s.enemyDensityScale : 1f;
                    _firing = em;
                    _firingAttack = a;
                    TickPattern(0f, game);
                    break;
                }
            }
        }

        static JamKit.Flow.Settings GameRootSettings() => JamKit.Shell.GameRoot.Exists ? JamKit.Shell.GameRoot.Settings : null;

        void TickPattern(float dt, DoomGame game)
        {
            var a = _firingAttack;
            var player = game.player;
            Vector3 origin = transform.position + Vector3.up * (asset.hoverHeight + a.muzzleHeight);
            Vector3 target = player.transform.position;
            _spawns.Clear();
            _firing.Tick(dt, new System.Numerics.Vector2(origin.x, origin.z), new System.Numerics.Vector2(target.x, target.z), _spawns);
            float targetY = player.transform.position.y + player.Height * 0.6f;
            float dist = Vector2.Distance(new Vector2(origin.x, origin.z), new Vector2(target.x, target.z));
            // Start each shot just outside the body so it doesn't hit the enemy's own wall corner.
            float pushOut = asset.radius + a.projectileRadius + 0.05f;
            for (int i = 0; i < _spawns.Count; i++)
            {
                var b = _spawns[i];
                var dir = GridMath.Direction(b.angle);
                var p = new Vector3(b.position.X + dir.X * pushOut, origin.y, b.position.Y + dir.Y * pushOut);
                // Shots aimed roughly at the player drop or climb to meet them; the rest fly level.
                float off = Mathf.Abs(GridMath.WrapSigned(b.angle - Mathf.Atan2(target.z - origin.z, target.x - origin.x)));
                float ty = off < 0.6f ? targetY : origin.y;
                game.projectiles.Spawn(p, b.angle, b.speed, b.acceleration, b.angularVelocity, b.lifetime,
                    ty, Mathf.Max(1f, dist - pushOut), a.damage, a.projectileRadius, a.splashRadius, a.projectileColor,
                    false, id, 0f, b.age);
            }
        }

        void UpdateAimLine(DoomGame game)
        {
            if (aimLine == null) return;
            var a = AttackFor(_brain.CurrentAttack);
            bool winding = _brain.State == EnemyState.Attack && _brain.Phase == AttackPhase.Windup && a != null && a.kind == EnemyAttackKind.Hitscan;
            bool show = (winding && _canSee) || _tracer > 0f;
            if (aimLine.enabled != show) aimLine.enabled = show;
            if (!show) return;
            aimLine.SetPosition(0, EyePosition);
            aimLine.SetPosition(1, game.player.EyePosition - Vector3.up * 0.25f);
            Color c = _tracer > 0f ? new Color(1f, 0.95f, 0.6f) : new Color(1f, 0.15f, 0.1f, 1f) * (0.6f + 0.4f * Mathf.PingPong(_brain.PhaseTime * 12f, 1f));
            aimLine.startColor = c;
            aimLine.endColor = c;
        }

        // ---------- Damage ----------

        /// <summary>Applies damage. Returns what happened (killed, overkill for gibbing...).</summary>
        public DamageResult TakeDamage(float amount, DamageKind kind, int sourceId)
        {
            if (_dead) return default;
            var r = _health.Damage(new DamageInfo(amount, kind, sourceId));
            if (r.applied <= 0f) return r;
            _hitFlash = 0.09f;
            if (r.killed) Die(r);
            else _brain.Damaged(_health.Fraction01, _rng);
            return r;
        }

        /// <summary>Wakes it (an ambush trigger, a nearby fight).</summary>
        public void Alert() => _brain.Alert();

        void Die(DamageResult r)
        {
            _dead = true;
            _brain.Kill();
            _gibbed = asset.gibOverkill > 0f && r.overkill >= asset.gibOverkill;
            _deathTime = 0f;
            _firing = null;
            if (agent != null) agent.enabled = false;
            if (body != null) body.enabled = false;
            if (aimLine != null) aimLine.enabled = false;
        }

        /// <summary>True once, the first time it's asked after death (the game counts the kill).</summary>
        public bool TakeKill()
        {
            if (!_dead || _counted) return false;
            _counted = true;
            return true;
        }

        // ---------- Look ----------

        public void UpdateSprite(DoomGame game)
        {
            var sheet = asset.sprite;
            if (sheet == null || billboard == null) return;
            int col, row;
            bool mirror = false;
            if (_dead)
            {
                row = SpriteSheet.DeathRow;
                int f = (int)(_deathTime * Mathf.Max(1f, sheet.deathFramesPerSecond));
                if (_gibbed && sheet.gibFrames > 0) col = Mathf.Min(sheet.deathFrames + sheet.gibFrames - 1, sheet.deathFrames + f);
                else col = Mathf.Min(Mathf.Max(1, sheet.deathFrames) - 1, f);
                col = Mathf.Clamp(col, 0, SpriteSheet.Columns - 1);
            }
            else
            {
                Vector3 cam = game.CameraPosition;
                int frame8 = AngleFrame.Frame(_facing, new System.Numerics.Vector2(transform.position.x, transform.position.z),
                    new System.Numerics.Vector2(cam.x, cam.z), 8);
                col = AngleFrame.SheetColumn(frame8, sheet.directions, out mirror);
                switch (_brain.State)
                {
                    case EnemyState.Pain: row = SpriteSheet.PainRow; break;
                    case EnemyState.Attack: row = SpriteSheet.AttackRow; break;
                    case EnemyState.Chase:
                        row = agent != null && agent.enabled && agent.velocity.sqrMagnitude > 0.15f
                            ? ((int)(_walkPhase * 2f) % 2 == 0 ? SpriteSheet.WalkRowA : SpriteSheet.WalkRowB)
                            : SpriteSheet.WalkRowA;
                        break;
                    default: row = SpriteSheet.WalkRowA; break;
                }
            }
            billboard.SetFrame(SpriteSheet.Cell(col, row, mirror));

            float flash = 0f;
            Color fc = Color.white;
            if (!_dead)
            {
                if (_hitFlash > 0f) { flash = 0.75f; fc = Color.white; }
                else if (_brain.State == EnemyState.Attack && _brain.Phase == AttackPhase.Windup)
                {
                    var a = AttackFor(_brain.CurrentAttack);
                    fc = a != null && a.kind == EnemyAttackKind.Hitscan ? new Color(1f, 0.2f, 0.15f) : new Color(1f, 0.6f, 0.15f);
                    flash = 0.15f + 0.3f * Mathf.PingPong(_brain.PhaseTime * 8f, 1f);
                }
                else if (_brain.State == EnemyState.Attack && _brain.Phase == AttackPhase.Fire) { flash = 0.35f; fc = new Color(1f, 0.95f, 0.7f); }
            }
            billboard.SetFlash(fc, flash);
            billboard.Face(game.CameraYaw);
        }
    }
}

using System;
using System.Collections.Generic;
using JamKit.Combat;
using JamKit.Flow;
using JamKit.Patterns;
using JamKit.Shell;
using UnityEngine;
using SVector2 = System.Numerics.Vector2;

namespace JamKit.BulletHell
{
    /// <summary>
    /// A floor boss from a <see cref="BossDef"/>: flies in (untouchable), then fights through its
    /// phases (core <see cref="BossPhaseTracker"/> on its health fraction; each phase has its own
    /// pattern sequence and movement). A phase change flashes, shakes, clears the bullets and makes it
    /// briefly invulnerable. Death plays a chain of explosions, then raises <see cref="Defeated"/>.
    /// </summary>
    public sealed class ShmupBoss : MonoBehaviour
    {
        public enum State { Inactive, Entering, Fighting, Dying, Dead }

        public SpriteRenderer body;
        public SpriteFlash flash;
        [Tooltip("A decorative ring that spins around the boss.")]
        public SpriteRenderer halo;
        public float enterSeconds = 2.2f;
        public float deathSeconds = 1.8f;

        BossDef _def;
        Health _health;
        BossPhaseTracker _phases;
        Emitter _emitter;
        Rng _rng;
        Settings _settings;
        State _state = State.Inactive;
        float _stateTime, _phaseTime, _invulnerable, _flashCooldown, _boomClock;
        Vector2 _pos, _home, _dashFrom, _dashTo;
        float _dashClock;
        readonly List<float> _thresholds = new List<float>();
        BulletField _field;
        FxField _fx;

        public BossDef Def => _def;
        public Health Health => _health;
        public State Current => _state;
        public int Phase => _phases != null ? _phases.Phase : 0;
        public int PhaseCount => _phases != null ? _phases.PhaseCount : 0;
        public bool IsActive => _state == State.Entering || _state == State.Fighting || _state == State.Dying;
        /// <summary>Takes damage only while fighting and not in a phase-change window.</summary>
        public bool CanBeHurt => _state == State.Fighting && _invulnerable <= 0f;
        public Vector2 Position => _pos;
        public float Radius => _def != null ? _def.radius : 1f;
        public float HealthFraction => _health != null ? _health.Fraction01 : 0f;
        /// <summary>The running phase's attack (null while entering, dying or between phases): the companion
        /// reads it to call out the next pattern.</summary>
        public PatternSequence CurrentAttack => _emitter != null && CurrentPhase != null ? CurrentPhase.attack : null;
        /// <summary>Seconds into the running phase's attack (its emitter's clock).</summary>
        public float EmitterTime => _emitter != null ? _emitter.Time : 0f;

        /// <summary>Phase index (from 0) each time a new phase starts (not the first).</summary>
        public event Action<int> PhaseChanged;
        /// <summary>Death sequence finished.</summary>
        public event Action Defeated;
        /// <summary>The killing blow landed (the explosions start).</summary>
        public event Action Dying;

        void Awake() => gameObject.SetActive(_state != State.Inactive);

        public void Begin(BossDef def, float hpScale, Rng rng, Settings settings, BulletField field, FxField fx)
        {
            _def = def;
            _rng = rng;
            _settings = settings;
            _field = field;
            _fx = fx;
            _health = new Health(Mathf.Max(1f, def.hp * Mathf.Max(0.01f, hpScale)));
            _thresholds.Clear();
            foreach (var p in def.phases) _thresholds.Add(p != null ? p.startsAtHealth : 0.5f);
            if (_thresholds.Count == 0) _thresholds.Add(1f);
            _phases = new BossPhaseTracker(_thresholds);
            _home = def.homePosition;
            _pos = new Vector2(_home.x, _home.y + 5f);
            _state = State.Entering;
            _stateTime = 0f;
            _invulnerable = 0f;
            _emitter = null;
            body.sprite = def.sprite;
            body.color = def.color;
            transform.localScale = Vector3.one * def.size;
            if (halo != null) halo.color = new Color(def.color.r, def.color.g, def.color.b, 0.35f);
            Place();
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            _state = State.Inactive;
            gameObject.SetActive(false);
        }

        BossPhase CurrentPhase => _def != null && _def.phases.Count > 0 ? _def.phases[Mathf.Clamp(Phase, 0, _def.phases.Count - 1)] : null;

        void StartPhase(int index, bool announce)
        {
            var p = CurrentPhase;
            _phaseTime = 0f;
            _dashClock = 0f;
            _dashFrom = _dashTo = _pos;
            _emitter = p != null && p.attack != null && p.attack.steps.Count > 0 ? new Emitter(p.attack, _rng) : null;
            if (_emitter != null && _settings != null)
            {
                _emitter.SpeedScale = _settings.enemyBulletSpeedScale;
                _emitter.DensityScale = _settings.enemyDensityScale;
            }
            if (p != null) body.color = _def.color * p.tint;
            if (!announce) return;
            _invulnerable = _def.phaseChangeInvulnerable;
            if (_field != null) ClearBullets();
            GameRoot.Juice.Flash(new Color(1f, 1f, 1f, 0.55f), 0.3f);
            GameRoot.Juice.Shake(0.6f);
            GameRoot.Audio.Play("boss_phase");
            if (_fx != null) _fx.Ring(_pos, _def.color, 0.5f, 6f, 0.6f);
            PhaseChanged?.Invoke(index);
        }

        void ClearBullets()
        {
            var pool = _field.Enemy;
            if (_fx != null)
            {
                var items = pool.Items;
                for (int i = 0; i < pool.Count; i += 3)
                    _fx.Spark(new Vector2(items[i].position.X, items[i].position.Y), new Color(0.7f, 0.9f, 1f, 0.8f), new Vector2(0f, 1.5f), 0.12f, 0.35f);
            }
            pool.Clear();
        }

        public void Tick(float dt, Vector2 player)
        {
            if (_state == State.Inactive || _state == State.Dead) return;
            _stateTime += dt;
            _flashCooldown -= Time.unscaledDeltaTime;
            if (halo != null) halo.transform.Rotate(0f, 0f, 40f * dt);
            switch (_state)
            {
                case State.Entering:
                {
                    float k = Mathf.Clamp01(_stateTime / enterSeconds);
                    float e = 1f - (1f - k) * (1f - k);
                    _pos = Vector2.Lerp(new Vector2(_home.x, _home.y + 5f), _home, e);
                    if (k >= 1f)
                    {
                        _state = State.Fighting;
                        _stateTime = 0f;
                        StartPhase(0, false);
                    }
                    break;
                }
                case State.Fighting:
                    _phaseTime += dt;
                    if (_invulnerable > 0f) _invulnerable -= dt;
                    Move(dt, player);
                    if (_emitter != null && _invulnerable <= 0f)
                    {
                        var spawns = _field.Spawns;
                        spawns.Clear();
                        _emitter.Tick(dt, new SVector2(_pos.x, _pos.y), new SVector2(player.x, player.y), spawns);
                        if (spawns.Count > 0) _field.EmitEnemy(spawns);
                    }
                    break;
                case State.Dying:
                    _boomClock -= dt;
                    if (_boomClock <= 0f)
                    {
                        _boomClock = 0.18f;
                        Vector2 at = _pos + UnityEngine.Random.insideUnitCircle * (_def.size * 0.45f);
                        if (_fx != null)
                        {
                            _fx.Ring(at, _def.color, 0.2f, 1.6f, 0.4f);
                            _fx.Burst(at, Color.Lerp(_def.color, Color.white, 0.4f), 10, 5f, 0.16f, 0.45f);
                        }
                        GameRoot.Audio.Play("enemy_die");
                        GameRoot.Juice.Shake(0.35f);
                        if (flash != null) flash.Flash(Color.white, 0.08f);
                    }
                    if (_stateTime >= deathSeconds) Die();
                    break;
            }
            Place();
        }

        void Move(float dt, Vector2 player)
        {
            var p = CurrentPhase;
            if (p == null) return;
            float t = _phaseTime;
            float w = Mathf.PI * 2f * Mathf.Max(0f, p.moveSpeed);
            Vector2 target;
            switch (p.movement)
            {
                case BossMoveKind.Sway:
                    target = _home + new Vector2(p.moveWidth * Mathf.Sin(w * t), 0.25f * Mathf.Sin(w * 2f * t));
                    break;
                case BossMoveKind.Figure8:
                    target = _home + new Vector2(p.moveWidth * Mathf.Sin(w * t), 0.8f * Mathf.Sin(2f * w * t));
                    break;
                case BossMoveKind.Chase:
                {
                    float x = Mathf.MoveTowards(_pos.x, Mathf.Clamp(player.x, _home.x - p.moveWidth, _home.x + p.moveWidth), p.moveSpeed * 4f * dt);
                    target = new Vector2(x, _home.y + 0.2f * Mathf.Sin(t * 2f));
                    break;
                }
                case BossMoveKind.Dash:
                {
                    _dashClock -= dt;
                    if (_dashClock <= 0f)
                    {
                        _dashClock = 2.2f;
                        _dashFrom = _pos;
                        _dashTo = new Vector2(_home.x + p.moveWidth * (_rng.NextFloat() * 2f - 1f), _home.y + (_rng.NextFloat() - 0.5f));
                    }
                    float k = Mathf.Clamp01((2.2f - _dashClock) / 0.6f);
                    target = Vector2.Lerp(_dashFrom, _dashTo, k * k * (3f - 2f * k));
                    break;
                }
                default:
                    target = _home + new Vector2(0f, 0.2f * Mathf.Sin(t * 2f));
                    break;
            }
            // Ease toward the target so phase changes never teleport the boss.
            _pos = Vector2.Lerp(_pos, target, 1f - Mathf.Exp(-8f * dt));
        }

        void Place()
        {
            transform.position = new Vector3(_pos.x, _pos.y, transform.position.z);
        }

        /// <summary>A hit from a player shot or bomb. Returns true if it started the death.</summary>
        public bool Damage(float amount)
        {
            if (!CanBeHurt) return false;
            var r = _health.Damage(new DamageInfo(amount, DamageKind.Bullet));
            if (_flashCooldown <= 0f && flash != null)
            {
                flash.Flash(Color.white, 0.05f);
                _flashCooldown = 0.06f;
            }
            if (r.killed) { StartDying(); return true; }
            int before = _phases.Phase;
            if (_phases.Update(_health.Fraction01) && _phases.Phase != before) StartPhase(_phases.Phase, true);
            return false;
        }

        /// <summary>Debug "kill boss": starts the death sequence at once (works while it's still flying in).</summary>
        public void DebugKill()
        {
            if (_state != State.Entering && _state != State.Fighting) return;
            if (!_health.IsDead) _health.Kill();
            StartDying();
        }

        void StartDying()
        {
            if (_state == State.Dying || _state == State.Dead) return;
            _state = State.Dying;
            _stateTime = 0f;
            _boomClock = 0f;
            _emitter = null;
            if (_field != null) ClearBullets();
            GameRoot.Juice.HitStop(0.2f);
            Dying?.Invoke();
        }

        void Die()
        {
            _state = State.Dead;
            if (_field != null) ClearBullets();
            if (_fx != null)
            {
                _fx.Ring(_pos, Color.white, 0.5f, 9f, 0.8f);
                _fx.Burst(_pos, _def.color, 60, 9f, 0.22f, 0.8f);
            }
            GameRoot.Juice.Flash(new Color(1f, 1f, 1f, 0.85f), 0.45f);
            GameRoot.Juice.Shake(1f);
            GameRoot.Juice.HitStop(0.25f);
            GameRoot.Audio.Play("explosion");
            gameObject.SetActive(false);
            Defeated?.Invoke();
        }
    }
}

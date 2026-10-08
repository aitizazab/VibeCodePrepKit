using System.Collections.Generic;
using JamKit.Combat;
using JamKit.Shell;
using UnityEngine;

namespace JamKit.BulletHell
{
    /// <summary>
    /// The player's ship: 8-way movement (Move), Sprint (Shift / LB: faster, on a stamina bar inside a
    /// per-room budget, core <see cref="SprintMeter"/>), optional Focus (PlayerDef.focusEnabled), auto or held fire
    /// through the core <see cref="WeaponState"/> (shot ages keep streams even at any frame rate),
    /// lives as a core <see cref="Health"/> (1 per hit, i-frames, the difficulty's damage scale) and
    /// bombs. The game drives it (<see cref="BulletHellGame"/>): collisions and bombs live there.
    /// </summary>
    public sealed class ShmupPlayer : MonoBehaviour
    {
        [Header("Parts (set by the builder)")]
        public SpriteRenderer body;
        public SpriteFlash flash;
        public SpriteRenderer hitboxDot;
        public SpriteRenderer grazeRing;
        public SpriteRenderer focusAura;

        PlayerDef _def;
        ShmupLoadout _loadout;
        WeaponDef _weaponDef;
        WeaponState _weapon;
        Health _health;
        readonly List<float> _shotAges = new List<float>(8);
        int _bombs, _maxBombs;
        bool _focused, _controls = true;
        float _blink, _shotSound, _sprintFraction;
        Color _baseColor = Color.white;
        readonly SprintMeter _sprint = new SprintMeter();

        public PlayerDef Def => _def;
        public Health Health => _health;
        public int Bombs => _bombs;
        public int MaxBombs => _maxBombs;
        public bool Focused => _focused;
        /// <summary>The sprint meter: stamina and this room's budget (the HUD draws it).</summary>
        public SprintMeter Sprint => _sprint;
        /// <summary>Sprinting this frame.</summary>
        public bool Sprinting => _sprint.IsSprinting;
        /// <summary>The run's speed while sprinting, times normal (data x the sprint_speed stat).</summary>
        public float SprintMultiplier => _def != null && _loadout != null ? _loadout.SprintMultiplier(_def.sprint != null ? _def.sprint.speedMultiplier : 1.6f) : 1f;
        public bool IsInvulnerable => _health != null && _health.IsInvulnerable;
        public Vector2 Position => transform.position;
        public float HitboxRadius => _def != null ? _def.hitboxRadius : 0.07f;
        public float GrazeRadius => (_def != null ? _def.grazeRadius : 0.6f) * (_loadout != null ? _loadout.GrazeRadiusScale : 1f);
        /// <summary>Movement and firing on/off (off in menus, intros and between rooms).</summary>
        public bool ControlsEnabled
        {
            get => _controls;
            set => _controls = value;
        }

        /// <summary>A fresh ship for a new run.</summary>
        public void BeginRun(PlayerDef def, ShmupLoadout loadout, int lives)
        {
            _def = def;
            _loadout = loadout;
            _weaponDef = new WeaponDef
            {
                magazineSize = 0,
                infiniteReserve = true,
                fireMode = def.weapon != null ? def.weapon.fireMode : FireMode.Auto,
                fireInterval = def.weapon != null ? def.weapon.fireInterval : 0.085f,
                burstCount = def.weapon != null ? def.weapon.burstCount : 3,
                burstInterval = def.weapon != null ? def.weapon.burstInterval : 0.06f,
            };
            _weapon = new WeaponState(_weaponDef);
            _health = new Health(Mathf.Max(1, lives), def.invulnerableAfterHit);
            _maxBombs = Mathf.Max(def.bombsPerLife, def.maxBombs);
            _bombs = def.bombsPerLife;
            if (body != null)
            {
                body.sprite = def.sprite != null ? def.sprite : body.sprite;
                body.color = def.color;
                _baseColor = def.color;
                body.transform.localScale = Vector3.one * def.size;
            }
            if (hitboxDot != null) hitboxDot.transform.localScale = Vector3.one * Mathf.Max(0.12f, def.hitboxRadius * 2.6f);
            ConfigureSprint();
            _sprint.Fill();
            _sprintFraction = 0f;
            UpdateRing();
            transform.position = new Vector3(0f, -3.6f, transform.position.z);
        }

        void UpdateRing()
        {
            if (grazeRing != null) grazeRing.transform.localScale = Vector3.one * (GrazeRadius * 2f);
        }

        public void AddBombs(int n) => _bombs = Mathf.Clamp(_bombs + n, 0, _maxBombs);

        public void RaiseMaxBombs(int n)
        {
            _maxBombs += n;
            AddBombs(n);
        }

        /// <summary>After a lost life: bombs back up to the per-life amount.</summary>
        public void RefillBombs() => _bombs = Mathf.Max(_bombs, _def != null ? _def.bombsPerLife : 2);

        public bool TryUseBomb()
        {
            if (_bombs <= 0) return false;
            _bombs--;
            return true;
        }

        /// <summary>Adds lives (raising the max when needed). Returns the amount added.</summary>
        public float AddLives(float n)
        {
            if (_health == null || n <= 0f) return 0f;
            if (_health.Current + n > _health.Max) _health.SetMax(_health.Current + n);
            return _health.Heal(n);
        }

        /// <summary>Heals up to the max only (rests, graze heal). Returns the amount restored.</summary>
        public float Heal(float n) => _health != null ? _health.Heal(n) : 0f;

        public void Teleport(Vector2 p) => transform.position = new Vector3(p.x, p.y, transform.position.z);

        /// <summary>Reads input, moves, fires. Called by the game each frame while playing.</summary>
        public void Tick(float dt, BulletField field, PlayfieldBounds bounds)
        {
            if (_def == null) return;
            var input = GameRoot.Input;
            bool allowed = _controls && input.GameplayAllowed;
            _focused = _def.focusEnabled && allowed && input.Focus.IsPressed();
            _health.Tick(dt);

            Vector2 move = allowed ? input.MoveVector : Vector2.zero;
            ConfigureSprint(); // upgrades can change the stats at any time
            bool wantSprint = allowed && !_focused && input.Sprint.IsPressed();
            _sprintFraction = _sprint.Tick(dt, wantSprint, move.sqrMagnitude > 0.01f);

            if (allowed && dt > 0f)
            {
                float speed = _focused ? _def.focusSpeed : _def.moveSpeed * (1f + (SprintMultiplier - 1f) * _sprintFraction);
                speed *= _loadout.MoveSpeedScale;
                Vector2 p = (Vector2)transform.position + move * (speed * dt);
                var clamped = bounds.Clamp(new System.Numerics.Vector2(p.x, p.y), 0.35f);
                transform.position = new Vector3(clamped.X, clamped.Y, transform.position.z);
            }

            bool held = allowed && (_def.autoFire || input.Fire.IsPressed());
            _weaponDef.fireInterval = _loadout.FireInterval(_def.weapon != null ? _def.weapon.fireInterval : 0.085f);
            int shots = _weapon.Tick(dt, held, false, _shotAges);
            if (shots > 0) Fire(field);

            UpdateRing();
            UpdateLooks();
        }

        void Fire(BulletField field)
        {
            var streams = _loadout.Streams(_focused);
            Vector2 origin = transform.position;
            float speed = _def.shotSpeed * _loadout.ShotSpeedScale;
            float damage = _def.shotDamage * _loadout.Damage;
            int pierce = _loadout.Pierce;
            for (int s = 0; s < _shotAges.Count; s++)
            {
                float age = _shotAges[s];
                for (int i = 0; i < streams.Count; i++)
                {
                    var st = streams[i];
                    int style = st.homing ? _def.homingShotStyle : _def.shotStyle;
                    field.FirePlayer(origin + new Vector2(st.offsetX, st.offsetY), st.angleDegrees * Mathf.Deg2Rad, speed,
                        damage * st.damageScale, style, _def.shotLifetime, pierce, st.homing, age);
                }
            }
            _shotSound -= _shotAges.Count;
            if (_shotSound <= 0f)
            {
                _shotSound = 4f;
                GameRoot.Audio.Play("shoot", 0.12f);
            }
        }

        void ConfigureSprint()
        {
            if (_def == null || _loadout == null) return;
            _sprint.Configure(_def.sprint, _loadout.StaminaMaxScale, _loadout.StaminaRegenScale, _loadout.RoomSprintBudgetScale);
        }

        /// <summary>A fight room starts: the room's sprint budget (and the bar) are full again.</summary>
        public void RefillSprint()
        {
            ConfigureSprint();
            _sprint.RefillRoom();
        }

        void UpdateLooks()
        {
            // Without Focus mode the hitbox is always shown (the How to Play says only the dot can be hit).
            if (hitboxDot != null) hitboxDot.enabled = _focused || (_def != null && !_def.focusEnabled);
            if (focusAura != null) focusAura.enabled = _focused || _sprint.IsSprinting;
            if (grazeRing != null)
            {
                var c = grazeRing.color;
                c.a = _focused ? 0.35f : 0.12f;
                grazeRing.color = c;
            }
            if (body == null) return;
            if (IsInvulnerable)
            {
                _blink += Time.unscaledDeltaTime;
                var c = _baseColor;
                c.a = Mathf.Repeat(_blink, 0.16f) < 0.08f ? 0.25f : 1f;
                body.color = c;
            }
            else if (body.color != _baseColor)
            {
                _blink = 0f;
                body.color = _baseColor;
            }
        }
    }
}

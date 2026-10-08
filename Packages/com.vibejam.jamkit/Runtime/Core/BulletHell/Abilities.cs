using System;

namespace JamKit.BulletHell
{
    /// <summary>
    /// The Bomb Bay: a few bombs that recharge one at a time while fighting. Capacity 0 = no bombs
    /// (the starting kit has none since round 2; the Bomb Bay epic gives them).
    /// </summary>
    public sealed class BombBay
    {
        int _bombs, _capacity;
        float _rechargeSeconds, _clock;

        public int Bombs => _bombs;
        public int Capacity => _capacity;
        public float RechargeSeconds => _rechargeSeconds;
        /// <summary>Progress to the next bomb, 0..1 (0 when full or not recharging).</summary>
        public float RechargeFraction => _rechargeSeconds > 0f && _bombs < _capacity ? Math.Min(1f, _clock / _rechargeSeconds) : 0f;
        public bool Full => _bombs >= _capacity;

        /// <summary>New capacity and recharge time (0 = no recharge). Bombs above the capacity are dropped;
        /// a bigger capacity doesn't add bombs by itself (<see cref="Add"/> does).</summary>
        public void Configure(int capacity, float rechargeSeconds)
        {
            _capacity = Math.Max(0, capacity);
            _rechargeSeconds = Math.Max(0f, rechargeSeconds);
            if (_bombs > _capacity) _bombs = _capacity;
        }

        public void Fill() { _bombs = _capacity; _clock = 0f; }

        public void Empty() { _bombs = 0; _clock = 0f; }

        /// <summary>Adds bombs up to the capacity. Returns how many were added.</summary>
        public int Add(int n)
        {
            int before = _bombs;
            _bombs = Math.Min(_capacity, Math.Max(0, _bombs + n));
            return _bombs - before;
        }

        public bool TryUse()
        {
            if (_bombs <= 0) return false;
            _bombs--;
            return true;
        }

        /// <summary>Recharges while <paramref name="fighting"/>. Returns true when a bomb came back this tick.</summary>
        public bool Tick(float dt, bool fighting)
        {
            if (!fighting || _rechargeSeconds <= 0f || _bombs >= _capacity || !(dt > 0f)) return false;
            _clock += dt;
            if (_clock < _rechargeSeconds) return false;
            _clock -= _rechargeSeconds;
            _bombs++;
            if (_bombs >= _capacity) _clock = 0f;
            return true;
        }
    }

    /// <summary>Numbers for the moment-to-moment epics, as data (BulletHellConfig > Boosts).</summary>
    [Serializable]
    public class PowerBoostRules
    {
        // Ember Hunger (graze_power): each graze adds heat (+damage), which cools over time.
        public float heatPerGraze = 0.12f;
        public float heatMax = 0.6f;
        /// <summary>Heat lost per second.</summary>
        public float heatDecay = 0.15f;
        // Last Gambit (last_stand): on the last life.
        public float lastStandDamage = 0.75f;
        public float lastStandFireRate;
        /// <summary>A ricochet's damage, times the hit that made it.</summary>
        public float ricochetDamage = 0.8f;
        /// <summary>How far a ricochet looks for another enemy.</summary>
        public float ricochetRange = 4f;
        /// <summary>A kill's blast: radius, and damage as a multiple of a main shot (times the damage stat and boost).</summary>
        public float blastRadius = 1.3f;
        public float blastDamage = 6f;
        // Still Point (still_point): holding still ramps damage up; any movement resets it.
        /// <summary>Seconds of holding still to reach the full bonus.</summary>
        public float stillRampSeconds = 0.6f;
        public float stillMax = 1f;
        // Ghost Step (ghost_step): a short phase when a sprint starts, then a cooldown; a few per room.
        public float ghostWindow = 0.5f;
        public float ghostCooldown = 5f;
        public int ghostPhasesPerRoom = 3;
        // Guardian Angel (auto_bomb): spends every stored bomb, then waits.
        public float guardianCooldown = 10f;
        /// <summary>Its bomb's power grows by this per extra bomb it spent.</summary>
        public float guardianPowerPerBomb = 0.25f;
        // Time Dilation (focus_slow): a bubble around the ship where enemy bullets slow down.
        public float slowRadius = 1.6f;
        /// <summary>Enemy bullet speed inside the bubble (0.5 = half).</summary>
        public float slowScale = 0.5f;
    }

    /// <summary>
    /// The damage and fire-rate multipliers from Ember Hunger (grazes heat the guns), Still Point (holding
    /// still) and Last Gambit (the last life). The game feeds it every frame and copies the result into
    /// <see cref="ShmupLoadout.DamageBoost"/> and <see cref="ShmupLoadout.FireRateBoost"/>.
    /// </summary>
    public sealed class PowerBoosts
    {
        float _heat, _still;

        public float Heat => _heat;
        /// <summary>0..1 of Still Point's ramp.</summary>
        public float StillCharge => _still;
        public float DamageMultiplier { get { return _damage; } }
        public float FireRateMultiplier { get { return _fireRate; } }
        float _damage = 1f, _fireRate = 1f;

        public void Reset()
        {
            _heat = _still = 0f;
            _damage = _fireRate = 1f;
        }

        /// <summary>One frame. Stacks are the effect counts (0 = the upgrade is not owned).</summary>
        public void Tick(float dt, PowerBoostRules rules, int grazePower, int lastStand, int grazesThisFrame, bool lastLife,
            int stillPoint = 0, bool moving = true)
        {
            if (rules == null) { _damage = _fireRate = 1f; return; }
            dt = Math.Max(0f, dt);
            if (grazePower > 0)
            {
                _heat += grazesThisFrame * rules.heatPerGraze * grazePower;
                _heat -= rules.heatDecay * dt;
                float cap = rules.heatMax * grazePower;
                _heat = _heat < 0f ? 0f : _heat > cap ? cap : _heat;
            }
            else _heat = 0f;
            if (stillPoint > 0 && !moving) _still = Math.Min(1f, _still + dt / Math.Max(0.02f, rules.stillRampSeconds));
            else _still = 0f;
            float dmg = 1f + _heat + (stillPoint > 0 ? _still * rules.stillMax * stillPoint : 0f);
            float rate = 1f;
            if (lastStand > 0 && lastLife)
            {
                dmg += rules.lastStandDamage * lastStand;
                rate += rules.lastStandFireRate * lastStand;
            }
            _damage = dmg;
            _fireRate = rate;
        }
    }

    /// <summary>
    /// Ghost Step's phases: starting a sprint opens a short window where bullets pass through, then a
    /// cooldown; only a few per fight room (the HUD shows them as pips). Tapping sprint spends one.
    /// </summary>
    public sealed class GhostPhase
    {
        float _window, _cooldown;
        int _left;
        bool _wasSprinting;

        public bool Phasing => _window > 0f;
        public int PhasesLeft => _left;
        public float CooldownLeft => Math.Max(0f, _cooldown);
        public bool Ready => _left > 0 && _cooldown <= 0f;

        /// <summary>A fight room starts: its phases are back (one more per extra Ghost Step stack or forge level).</summary>
        public void RoomStarted(PowerBoostRules rules, int stacks = 1)
        {
            _left = rules != null ? Math.Max(0, rules.ghostPhasesPerRoom + Math.Max(0, stacks - 1)) : 0;
            _window = _cooldown = 0f;
        }

        /// <summary>One frame. Returns true when a phase started.</summary>
        public bool Tick(float dt, bool sprinting, PowerBoostRules rules)
        {
            dt = Math.Max(0f, dt);
            if (_window > 0f) _window -= dt;
            if (_cooldown > 0f) _cooldown -= dt;
            bool started = sprinting && !_wasSprinting;
            _wasSprinting = sprinting;
            if (!started || rules == null || !Ready) return false;
            _left--;
            _window = rules.ghostWindow;
            _cooldown = rules.ghostCooldown;
            return true;
        }
    }

    /// <summary>Guardian Angel: a hit fires a bomb instead of costing a life, spending EVERY stored bomb, then a cooldown.</summary>
    public sealed class GuardianAngel
    {
        float _cooldown;

        public float CooldownLeft => Math.Max(0f, _cooldown);
        public bool Ready => _cooldown <= 0f;

        public void Reset() => _cooldown = 0f;

        public void Tick(float dt)
        {
            if (_cooldown > 0f) _cooldown -= Math.Max(0f, dt);
        }

        /// <summary>Ready and the bay has a bomb: empties it and starts the cooldown. Returns the bombs spent (0 = no save).</summary>
        public int TryTrigger(BombBay bay, PowerBoostRules rules)
        {
            if (!Ready || bay == null || bay.Bombs <= 0) return 0;
            int spent = bay.Bombs;
            bay.Empty();
            _cooldown = rules != null ? rules.guardianCooldown : 10f;
            return spent;
        }

        /// <summary>The bomb's power for <paramref name="spent"/> bombs.</summary>
        public static float PowerFor(int spent, PowerBoostRules rules) =>
            1f + Math.Max(0, spent - 1) * (rules != null ? rules.guardianPowerPerBomb : 0.25f);
    }

    /// <summary>Last Gambit's ricochet: the nearest other active target within range, or -1.</summary>
    public static class LastGambit
    {
        public static int RicochetTarget(HitTarget[] targets, int count, int exceptId, System.Numerics.Vector2 from, float range)
        {
            int best = -1;
            float bestD = range * range;
            for (int i = 0; i < count; i++)
            {
                if (!targets[i].active || targets[i].id == exceptId) continue;
                float d = System.Numerics.Vector2.DistanceSquared(targets[i].position, from);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }
    }
}

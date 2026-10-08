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

    /// <summary>Numbers for the moment-to-moment damage epics, as data (BulletHellConfig holds one).</summary>
    [Serializable]
    public class PowerBoostRules
    {
        // Ember Hunger (graze_power): each graze adds heat (+damage), which cools over time.
        public float heatPerGraze = 0.12f;
        public float heatMax = 0.6f;
        /// <summary>Heat lost per second.</summary>
        public float heatDecay = 0.15f;
        // Red Line (last_stand)
        public float lastStandDamage = 0.5f;
        public float lastStandFireRate = 0.3f;
        // Time Dilation (focus_slow): a bubble around the ship where enemy bullets slow down.
        public float slowRadius = 1.6f;
        /// <summary>Enemy bullet speed inside the bubble (0.5 = half).</summary>
        public float slowScale = 0.5f;
    }

    /// <summary>
    /// The damage and fire-rate multipliers from Ember Hunger (grazes heat the guns) and Red Line (the
    /// last life). The game feeds it every frame and copies the result into
    /// <see cref="ShmupLoadout.DamageBoost"/> and <see cref="ShmupLoadout.FireRateBoost"/>.
    /// </summary>
    public sealed class PowerBoosts
    {
        float _heat;

        public float Heat => _heat;
        public float DamageMultiplier { get { return _damage; } }
        public float FireRateMultiplier { get { return _fireRate; } }
        float _damage = 1f, _fireRate = 1f;

        public void Reset()
        {
            _heat = 0f;
            _damage = _fireRate = 1f;
        }

        /// <summary>One frame. Stacks are the effect counts (0 = the upgrade isn't owned).</summary>
        public void Tick(float dt, PowerBoostRules rules, int grazePower, int lastStand, int grazesThisFrame, bool lastLife)
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
            float dmg = 1f + _heat;
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
}

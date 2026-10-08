using System;

namespace JamKit.Combat
{
    /// <summary>
    /// Hit points for the player, enemies and breakables. Handles the rules every game re-invents:
    /// clamping, overkill, dead things ignoring hits, invulnerability frames after a hit (so one
    /// bullet cluster doesn't drain the player in a frame) and a difficulty multiplier.
    /// </summary>
    public sealed class Health
    {
        /// <summary>Remaining health below this counts as zero, so float sums like 10 x 0.1 still kill.</summary>
        const float DeathEpsilon = 1e-4f;

        float _damageTakenScale = 1f;
        float _invulnerabilitySeconds;

        public float Max { get; private set; }
        public float Current { get; private set; }
        public bool IsDead { get; private set; }
        public float Fraction01 => Current / Max;

        /// <summary>Seconds of invulnerability granted after each damaging hit. 0 for most enemies.</summary>
        public float InvulnerabilitySeconds
        {
            get => _invulnerabilitySeconds;
            set => _invulnerabilitySeconds = value > 0f ? value : 0f;
        }
        public float InvulnerableRemaining { get; private set; }
        public bool IsInvulnerable => InvulnerableRemaining > 0f;

        /// <summary>Multiplies incoming damage: below 1 for assist modes, above 1 for hard mode.</summary>
        public float DamageTakenScale
        {
            get => _damageTakenScale;
            set => _damageTakenScale = value > 0f ? value : 0f;
        }

        /// <summary>Raised for every hit that removed health (and by <see cref="Kill"/>).</summary>
        public event Action<DamageInfo, DamageResult> Damaged;
        /// <summary>Raised with the amount actually restored.</summary>
        public event Action<float> Healed;
        /// <summary>Raised once, with the killing blow.</summary>
        public event Action<DamageInfo> Died;
        public event Action Revived;

        public Health(float max, float invulnerabilitySeconds = 0f)
        {
            if (!(max > 0f)) throw new ArgumentException("max must be positive");
            Max = max;
            Current = max;
            InvulnerabilitySeconds = invulnerabilitySeconds;
        }

        public DamageResult Damage(DamageInfo info)
        {
            if (IsDead) return default;
            if (IsInvulnerable) return new DamageResult(0f, 0f, false, true);
            float amount = info.amount * _damageTakenScale;
            if (!(amount > 0f)) return default;

            float applied = Math.Min(amount, Current);
            float overkill = amount - applied;
            Current -= applied;
            bool killed = Current <= DeathEpsilon;
            if (killed)
            {
                Current = 0f;
                IsDead = true;
                InvulnerableRemaining = 0f;
            }
            else if (_invulnerabilitySeconds > InvulnerableRemaining)
            {
                InvulnerableRemaining = _invulnerabilitySeconds;
            }

            var result = new DamageResult(applied, overkill, killed, false);
            Damaged?.Invoke(info, result);
            if (killed) Died?.Invoke(info);
            return result;
        }

        /// <summary>Restores up to <paramref name="amount"/>, never past Max. Dead entities can't heal.
        /// Returns the amount actually restored.</summary>
        public float Heal(float amount)
        {
            if (IsDead || !(amount > 0f)) return 0f;
            float applied = Math.Min(amount, Max - Current);
            if (applied <= 0f) return 0f;
            Current += applied;
            Healed?.Invoke(applied);
            return applied;
        }

        /// <summary>Instant death that ignores invulnerability and scaling (pits, scripted deaths).
        /// Returns false if already dead.</summary>
        public bool Kill(DamageInfo info = default)
        {
            if (IsDead) return false;
            var result = new DamageResult(Current, 0f, true, false);
            Current = 0f;
            IsDead = true;
            InvulnerableRemaining = 0f;
            Damaged?.Invoke(info, result);
            Died?.Invoke(info);
            return true;
        }

        /// <summary>Back to full health with no invulnerability left over. Returns false if alive.</summary>
        public bool Revive()
        {
            if (!IsDead) return false;
            IsDead = false;
            Current = Max;
            InvulnerableRemaining = 0f;
            Revived?.Invoke();
            return true;
        }

        /// <summary>Changes Max (upgrades, boss phases). Current is clamped down, never raised:
        /// call <see cref="Heal"/> after if the upgrade should also fill the new space.</summary>
        public void SetMax(float max)
        {
            if (!(max > 0f)) throw new ArgumentException("max must be positive");
            Max = max;
            if (Current > max) Current = max;
        }

        /// <summary>Invulnerability not caused by a hit (dodge rolls, respawn). Never shortens an
        /// existing window.</summary>
        public void GrantInvulnerability(float seconds)
        {
            if (!IsDead && seconds > InvulnerableRemaining) InvulnerableRemaining = seconds;
        }

        public void Tick(float dt)
        {
            if (InvulnerableRemaining > 0f)
                InvulnerableRemaining = Math.Max(0f, InvulnerableRemaining - dt);
        }
    }
}

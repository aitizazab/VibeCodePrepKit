using System;
using System.Collections.Generic;
using JamKit.Movement;

namespace JamKit.Skyline
{
    /// <summary>One gun's record for the run.</summary>
    public struct WeaponTally
    {
        public int shots, hits, kills;
        public float damage, rangeSum;
        public float MeanHitRange => hits > 0 ? rangeSum / hits : 0f;
    }

    /// <summary>
    /// What CONCIERGE saw this run (docs/SKYLINE-DESIGN.md 4.1-4.2): seconds per movement state and per-gun records,
    /// counted during combat only. Resets every run. Scoring and counters are built on top later (M2-M3).
    /// </summary>
    public sealed class LedgerRun
    {
        readonly float[] _modeSeconds = new float[Enum.GetValues(typeof(MoveMode)).Length];
        readonly Dictionary<int, WeaponTally> _weapons = new Dictionary<int, WeaponTally>();
        float _totalDamage;

        public bool InCombat { get; private set; }
        public float CombatSeconds { get; private set; }
        /// <summary>Combat seconds when a runnable wall was in reach (or the player was on one).</summary>
        public float WallReachSeconds { get; private set; }

        public void BeginCombat() => InCombat = true;
        public void EndCombat() => InCombat = false;

        /// <summary>Call once per simulation step. <paramref name="wallInReach"/> comes from <see cref="WallReach"/>.</summary>
        public void Tick(float dt, MoveMode mode, bool wallInReach)
        {
            if (!InCombat || dt <= 0f) return;
            CombatSeconds += dt;
            _modeSeconds[(int)mode] += dt;
            if (wallInReach || mode == MoveMode.Wallrun) WallReachSeconds += dt;
        }

        public float SecondsIn(MoveMode mode) => _modeSeconds[(int)mode];
        public float ShareOf(MoveMode mode) => CombatSeconds > 0f ? _modeSeconds[(int)mode] / CombatSeconds : 0f;

        /// <summary>The share of the wall time on offer that was used (4.2: measured by opportunity).</summary>
        public float WallUse => WallReachSeconds > 0f ? SecondsIn(MoveMode.Wallrun) / WallReachSeconds : 0f;

        public void RecordShot(int weapon, bool hit, float damage, float range, bool kill)
        {
            if (!InCombat) return;
            _weapons.TryGetValue(weapon, out var t);
            t.shots++;
            if (hit)
            {
                t.hits++;
                t.damage += damage;
                t.rangeSum += range;
                _totalDamage += damage;
            }
            if (kill) t.kills++;
            _weapons[weapon] = t;
        }

        public WeaponTally Weapon(int weapon) => _weapons.TryGetValue(weapon, out var t) ? t : default;

        /// <summary>This gun's share of all the damage dealt this run (0 when nothing has been hit).</summary>
        public float DamageShare(int weapon) => _totalDamage > 0f ? Weapon(weapon).damage / _totalDamage : 0f;

        public void Reset()
        {
            Array.Clear(_modeSeconds, 0, _modeSeconds.Length);
            _weapons.Clear();
            _totalDamage = 0f;
            CombatSeconds = 0f;
            WallReachSeconds = 0f;
            InCombat = false;
        }
    }
}

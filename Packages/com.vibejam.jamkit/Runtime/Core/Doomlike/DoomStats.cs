using System;
using JamKit.Combat;
using JamKit.Roguelite;

namespace JamKit.Doomlike
{
    /// <summary>
    /// The player stats upgrades can change, with their base values, and the effect ids the game
    /// understands. Upgrade data refers to these by name (lowercase).
    /// </summary>
    public static class DoomStats
    {
        /// <summary>Multiplies all weapon damage (base 1).</summary>
        public const string Damage = "damage";
        /// <summary>Multiplies fire rate: 1.2 = 20% faster (base 1).</summary>
        public const string FireRate = "fire_rate";
        /// <summary>Multiplies run and walk speed (base 1).</summary>
        public const string MoveSpeed = "move_speed";
        /// <summary>Maximum health in points (base from the player tuning, 100).</summary>
        public const string MaxHealth = "max_health";
        /// <summary>Multiplies every ammo maximum (base 1).</summary>
        public const string AmmoCapacity = "ammo_capacity";
        /// <summary>Multiplies melee damage on top of Damage (base 1).</summary>
        public const string MeleeDamage = "melee_damage";
        /// <summary>Multiplies explosion radius (base 1).</summary>
        public const string SplashRadius = "splash_radius";
        /// <summary>Multiplies what health and ammo pickups give (base 1).</summary>
        public const string PickupBonus = "pickup_bonus";
        /// <summary>Fraction of incoming damage ignored, 0..0.75 (base 0).</summary>
        public const string Armor = "armor";

        public static readonly string[] All =
            { Damage, FireRate, MoveSpeed, MaxHealth, AmmoCapacity, MeleeDamage, SplashRadius, PickupBonus, Armor };

        // Effect ids (UpgradeDef.effects).
        /// <summary>Heals to full when taken.</summary>
        public const string HealFull = "heal_full";
        /// <summary>Each kill heals this many points per stack ("lifesteal").</summary>
        public const string Lifesteal = "lifesteal";
        /// <summary>Fills every ammo pool when taken.</summary>
        public const string FillAmmo = "fill_ammo";
        /// <summary>"heal:25" heals 25 when taken.</summary>
        public const string HealPrefix = "heal:";

        public static bool IsKnownStat(string stat) => Array.IndexOf(All, stat) >= 0;

        public static bool IsKnownEffect(string effect)
        {
            if (string.IsNullOrEmpty(effect)) return false;
            if (effect == HealFull || effect == Lifesteal || effect == FillAmmo) return true;
            return effect.StartsWith(HealPrefix, StringComparison.Ordinal)
                && float.TryParse(effect.Substring(HealPrefix.Length), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float v) && v > 0f;
        }

        /// <summary>Points healed by a "heal:N" effect, else 0.</summary>
        public static float HealAmount(string effect)
        {
            if (effect == null || !effect.StartsWith(HealPrefix, StringComparison.Ordinal)) return 0f;
            float.TryParse(effect.Substring(HealPrefix.Length), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float v);
            return v > 0f ? v : 0f;
        }

        /// <summary>Base values and safety clamps for a fresh run.</summary>
        public static void SetBase(StatBlock stats, float maxHealth)
        {
            stats.SetBase(Damage, 1f);
            stats.SetBase(FireRate, 1f);
            stats.SetBase(MoveSpeed, 1f);
            stats.SetBase(MaxHealth, maxHealth);
            stats.SetBase(AmmoCapacity, 1f);
            stats.SetBase(MeleeDamage, 1f);
            stats.SetBase(SplashRadius, 1f);
            stats.SetBase(PickupBonus, 1f);
            stats.SetBase(Armor, 0f);
            stats.SetClamp(FireRate, 0.25f, 4f);
            stats.SetClamp(MoveSpeed, 0.5f, 2f);
            stats.SetClamp(MaxHealth, 1f, 999f);
            stats.SetClamp(Armor, 0f, 0.75f);
            stats.SetClamp(AmmoCapacity, 0.25f, 5f);
        }

        /// <summary>Copies a weapon's timing and ammo rules (the asset stays untouched; the copy is
        /// what the run's upgrades speed up).</summary>
        public static WeaponDef Copy(WeaponDef d)
        {
            if (d == null) return new WeaponDef();
            return new WeaponDef
            {
                magazineSize = d.magazineSize,
                startingReserve = d.startingReserve,
                maxReserve = d.maxReserve,
                infiniteReserve = d.infiniteReserve,
                ammoPerShot = d.ammoPerShot,
                fireMode = d.fireMode,
                fireInterval = d.fireInterval,
                burstCount = d.burstCount,
                burstInterval = d.burstInterval,
                reloadTime = d.reloadTime,
                autoReloadOnEmpty = d.autoReloadOnEmpty,
            };
        }

        /// <summary>Sets the copy's intervals from the asset's and a fire-rate multiplier.</summary>
        public static void ApplyFireRate(WeaponDef asset, WeaponDef live, float fireRate)
        {
            float k = fireRate > 0.01f ? fireRate : 0.01f;
            live.fireInterval = asset.fireInterval / k;
            live.burstInterval = asset.burstInterval / k;
        }
    }
}

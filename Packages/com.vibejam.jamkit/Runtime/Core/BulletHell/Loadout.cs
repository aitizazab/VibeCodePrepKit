using System;
using System.Collections.Generic;
using System.Globalization;
using JamKit.Roguelite;

namespace JamKit.BulletHell
{
    /// <summary>Stat keys the bullet-hell template reads. Upgrades change them through StatModifiers
    /// (e.g. damage PercentAdd 0.2 = +20% damage).</summary>
    public static class ShmupStats
    {
        /// <summary>Damage per player shot (base 1).</summary>
        public const string Damage = "damage";
        /// <summary>Shots per second multiplier (base 1): 1.5 fires 50% faster.</summary>
        public const string FireRate = "fire_rate";
        /// <summary>Move speed multiplier (base 1), for normal and focused movement.</summary>
        public const string MoveSpeed = "move_speed";
        /// <summary>Player shot speed multiplier (base 1).</summary>
        public const string ShotSpeed = "shot_speed";
        /// <summary>Graze ring size multiplier (base 1).</summary>
        public const string GrazeRadius = "graze_radius";
        /// <summary>Draft luck (base 0): rarer cards. See UpgradeDraft.</summary>
        public const string Luck = "luck";
        /// <summary>Coin pickup reach multiplier (base 1).</summary>
        public const string Magnet = "magnet";
        /// <summary>Coins per drop multiplier (base 1).</summary>
        public const string CoinGain = "coin_gain";
        /// <summary>Bomb radius and damage multiplier (base 1).</summary>
        public const string BombPower = "bomb_power";

        public static readonly string[] All = { Damage, FireRate, MoveSpeed, ShotSpeed, GrazeRadius, Luck, Magnet, CoinGain, BombPower };

        /// <summary>Base values and sane clamps, so no upgrade stack can break the game.</summary>
        public static void SetDefaults(StatBlock s)
        {
            s.SetBase(Damage, 1f);
            s.SetBase(FireRate, 1f);
            s.SetBase(MoveSpeed, 1f);
            s.SetBase(ShotSpeed, 1f);
            s.SetBase(GrazeRadius, 1f);
            s.SetBase(Luck, 0f);
            s.SetBase(Magnet, 1f);
            s.SetBase(CoinGain, 1f);
            s.SetBase(BombPower, 1f);
            s.SetClamp(Damage, 0.1f, 100f);
            s.SetClamp(FireRate, 0.25f, 4f);
            s.SetClamp(MoveSpeed, 0.4f, 2.5f);
            s.SetClamp(ShotSpeed, 0.4f, 3f);
            s.SetClamp(GrazeRadius, 0.25f, 4f);
            s.SetClamp(Luck, -1f, 5f);
            s.SetClamp(Magnet, 0f, 10f);
            s.SetClamp(CoinGain, 0f, 10f);
            s.SetClamp(BombPower, 0.25f, 5f);
        }
    }

    /// <summary>
    /// Effect ids for upgrades (UpgradeDef.effects). Game code checks their stack counts; ids with a
    /// number ("coins:15") are instant rewards applied once when taken.
    /// </summary>
    public static class ShmupEffects
    {
        /// <summary>+2 angled shot streams per stack.</summary>
        public const string Spread = "spread";
        /// <summary>Shots pass through one more enemy per stack.</summary>
        public const string Pierce = "pierce";
        /// <summary>Side streams curve toward enemies (adds two if there are none).</summary>
        public const string Homing = "homing";
        /// <summary>Two streams fire backwards.</summary>
        public const string RearShot = "rear_shot";
        /// <summary>Bombs +50% radius and damage per stack.</summary>
        public const string BiggerBombs = "bigger_bombs";
        /// <summary>Every N grazes restore half a life (a bomb when at full lives).</summary>
        public const string GrazeHeal = "graze_heal";
        /// <summary>Instant: +1 bomb capacity and a bomb.</summary>
        public const string BombPlus = "bomb_plus";
        /// <summary>Instant: +1 life (and +1 max).</summary>
        public const string LifeUp = "life_up";
        /// <summary>A hit that would cost a life spends a bomb instead (if you have one).</summary>
        public const string AutoBomb = "auto_bomb";
        /// <summary>While focused, enemy bullets move slower.</summary>
        public const string FocusSlow = "focus_slow";
        /// <summary>Instant: coins, as "coins:15".</summary>
        public const string Coins = "coins";

        /// <summary>"coins:15" with prefix "coins" gives 15.</summary>
        public static bool TryParseAmount(string effect, string prefix, out int amount)
        {
            amount = 0;
            if (effect == null || prefix == null) return false;
            if (!effect.StartsWith(prefix + ":", StringComparison.Ordinal)) return false;
            return int.TryParse(effect.Substring(prefix.Length + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out amount);
        }

        public static readonly string[] Known =
            { Spread, Pierce, Homing, RearShot, BiggerBombs, GrazeHeal, BombPlus, LifeUp, AutoBomb, FocusSlow };

        /// <summary>True for every id the template understands (including "coins:N").</summary>
        public static bool IsKnown(string effect) =>
            Array.IndexOf(Known, effect) >= 0 || TryParseAmount(effect, Coins, out _);
    }

    /// <summary>One stream of player shots: where it starts relative to the ship, which way it flies.</summary>
    public struct ShotStream
    {
        /// <summary>Degrees, 90 = straight up.</summary>
        public float angleDegrees;
        public float offsetX;
        public float offsetY;
        public float damageScale;
        public bool homing;

        public ShotStream(float angleDegrees, float offsetX, float offsetY, float damageScale = 1f, bool homing = false)
        {
            this.angleDegrees = angleDegrees;
            this.offsetX = offsetX;
            this.offsetY = offsetY;
            this.damageScale = damageScale;
            this.homing = homing;
        }
    }

    /// <summary>The player's volley from upgrade stacks. Focus tightens the spread.</summary>
    public static class PlayerShots
    {
        /// <summary>Fills <paramref name="output"/> (cleared first). Base: two parallel streams straight up.</summary>
        public static void Build(int spread, int rear, int homing, bool focused, List<ShotStream> output)
        {
            output.Clear();
            float gap = focused ? 0.09f : 0.16f;
            output.Add(new ShotStream(90f, -gap, 0.3f));
            output.Add(new ShotStream(90f, gap, 0.3f));
            bool home = homing > 0;
            float step = focused ? 4f : 11f;
            for (int s = 1; s <= Math.Max(0, spread); s++)
            {
                float a = step * s;
                output.Add(new ShotStream(90f + a, -0.2f, 0.15f, 0.75f, home));
                output.Add(new ShotStream(90f - a, 0.2f, 0.15f, 0.75f, home));
            }
            if (home && spread <= 0)
            {
                output.Add(new ShotStream(120f, -0.3f, 0.1f, 0.6f, true));
                output.Add(new ShotStream(60f, 0.3f, 0.1f, 0.6f, true));
            }
            if (rear > 0)
            {
                output.Add(new ShotStream(255f, -0.15f, -0.2f, 0.75f));
                output.Add(new ShotStream(285f, 0.15f, -0.2f, 0.75f));
            }
        }
    }

    /// <summary>
    /// The player's build for one run: a StatBlock with the template's stats and an
    /// UpgradeInventory on top of it, with the derived numbers the game reads every frame.
    /// </summary>
    public sealed class ShmupLoadout
    {
        public readonly StatBlock Stats = new StatBlock();
        public readonly UpgradeInventory Inventory;
        readonly List<ShotStream> _streams = new List<ShotStream>(16);
        int _streamKey = -1;

        public ShmupLoadout()
        {
            ShmupStats.SetDefaults(Stats);
            Inventory = new UpgradeInventory(Stats);
        }

        /// <summary>New run: forget every upgrade, keep the base values.</summary>
        public void Reset()
        {
            Inventory.Clear();
            Stats.ClearModifiers();
            _streamKey = -1;
        }

        public float Damage => Stats.Get(ShmupStats.Damage);
        public float FireRate => Stats.Get(ShmupStats.FireRate);
        public float MoveSpeedScale => Stats.Get(ShmupStats.MoveSpeed);
        public float ShotSpeedScale => Stats.Get(ShmupStats.ShotSpeed);
        public float GrazeRadiusScale => Stats.Get(ShmupStats.GrazeRadius);
        public float Luck => Stats.Get(ShmupStats.Luck);
        public float MagnetScale => Stats.Get(ShmupStats.Magnet);
        public float CoinGain => Stats.Get(ShmupStats.CoinGain);

        /// <summary>Bomb radius and damage: the stat times 1.5 per bigger-bombs stack.</summary>
        public float BombPower => Stats.Get(ShmupStats.BombPower) * (1f + 0.5f * Effect(ShmupEffects.BiggerBombs));

        public int Pierce => Effect(ShmupEffects.Pierce);

        public int Effect(string id) => Inventory.EffectStacks(id);

        /// <summary>Seconds between volleys for a weapon whose base interval is <paramref name="baseInterval"/>.</summary>
        public float FireInterval(float baseInterval) => Math.Max(0.02f, baseInterval / Math.Max(0.05f, FireRate));

        /// <summary>The current volley (cached until the spread, rear, homing stacks or focus change).</summary>
        public List<ShotStream> Streams(bool focused)
        {
            int spread = Effect(ShmupEffects.Spread), rear = Effect(ShmupEffects.RearShot), homing = Effect(ShmupEffects.Homing);
            int key = (spread & 0xFF) | ((rear & 0xFF) << 8) | ((homing & 0xFF) << 16) | (focused ? 1 << 24 : 0);
            if (key != _streamKey)
            {
                PlayerShots.Build(spread, rear, homing, focused, _streams);
                _streamKey = key;
            }
            return _streams;
        }
    }
}

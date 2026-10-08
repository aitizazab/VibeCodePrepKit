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
        /// <summary>Sprint speed bonus multiplier (base 1): 1.5 makes the sprint's extra speed 50% bigger.</summary>
        public const string SprintSpeed = "sprint_speed";
        /// <summary>Stamina bar size multiplier (base 1): longer sprints per burst.</summary>
        public const string StaminaMax = "stamina_max";
        /// <summary>Stamina recharge speed multiplier (base 1).</summary>
        public const string StaminaRegen = "stamina_regen";
        /// <summary>Sprint seconds per room multiplier (base 1).</summary>
        public const string RoomSprintBudget = "room_sprint_budget";
        /// <summary>Bomb Bay recharge speed multiplier (base 1): 1.5 recharges 50% faster.</summary>
        public const string BombRecharge = "bomb_recharge";
        /// <summary>The companion's shot damage multiplier (base 1).</summary>
        public const string CompanionDamage = "companion_damage";
        /// <summary>The companion's guard cooldown multiplier (base 1): 0.7 = the guard is back 30% sooner.</summary>
        public const string CompanionShieldCooldown = "companion_shield_cooldown";

        public static readonly string[] All =
        {
            Damage, FireRate, MoveSpeed, ShotSpeed, GrazeRadius, Luck, Magnet, CoinGain, BombPower,
            SprintSpeed, StaminaMax, StaminaRegen, RoomSprintBudget, BombRecharge, CompanionDamage, CompanionShieldCooldown,
        };

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
            s.SetBase(SprintSpeed, 1f);
            s.SetBase(StaminaMax, 1f);
            s.SetBase(StaminaRegen, 1f);
            s.SetBase(RoomSprintBudget, 1f);
            s.SetBase(BombRecharge, 1f);
            s.SetBase(CompanionDamage, 1f);
            s.SetBase(CompanionShieldCooldown, 1f);
            s.SetClamp(BombRecharge, 0.25f, 5f);
            s.SetClamp(CompanionDamage, 0f, 10f);
            s.SetClamp(CompanionShieldCooldown, 0.2f, 5f);
            s.SetClamp(SprintSpeed, 0f, 4f);
            s.SetClamp(StaminaMax, 0.25f, 5f);
            s.SetClamp(StaminaRegen, 0.25f, 5f);
            s.SetClamp(RoomSprintBudget, 0f, 5f);
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
        /// <summary>Two streams fire backwards. Kept for comparison only: the balance report flags it
        /// (enemies never get behind a player who sits low), and it's no longer in the upgrade pool.</summary>
        public const string RearShot = "rear_shot";
        /// <summary>Two streams fire down at an angle, bounce off the bottom edge and climb back up
        /// across the screen (the lower you sit, the sooner they come back). Replaces the rear gun.</summary>
        public const string Rebound = "rebound";
        /// <summary>Bombs: hold a bay of bombs that recharge over time. Without it the ship has no bombs
        /// (unless PlayerDef gives some). Each extra stack holds one more.</summary>
        public const string BombBay = "bomb_bay";
        /// <summary>While sprinting, enemy bullets pass through you (bodies still hurt).</summary>
        public const string GhostStep = "ghost_step";
        /// <summary>Grazing heats your guns: more damage for a few seconds, up to a cap.</summary>
        public const string GrazePower = "graze_power";
        /// <summary>On your last life: more damage and faster fire.</summary>
        public const string LastStand = "last_stand";
        /// <summary>Bombs +50% radius and damage per stack.</summary>
        public const string BiggerBombs = "bigger_bombs";
        /// <summary>Every N grazes restore half a life (a bomb when at full lives).</summary>
        public const string GrazeHeal = "graze_heal";
        /// <summary>+1 bomb capacity (a Bomb Bay upgrade: it needs bomb_bay to matter) and a bomb now.</summary>
        public const string BombPlus = "bomb_plus";
        /// <summary>Instant: +1 life (and +1 max).</summary>
        public const string LifeUp = "life_up";
        /// <summary>A hit that would cost a life spends a bomb instead (if you have one).</summary>
        public const string AutoBomb = "auto_bomb";
        /// <summary>Time Dilation: enemy bullets close to the ship move slower (a bubble around it;
        /// PowerBoostRules.slowRadius and slowScale).</summary>
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
        {
            Spread, Pierce, Homing, RearShot, Rebound, BombBay, GhostStep, GrazePower, LastStand,
            BiggerBombs, GrazeHeal, BombPlus, LifeUp, AutoBomb, FocusSlow,
        };

        /// <summary>True for every id the template understands (including "coins:N").</summary>
        public static bool IsKnown(string effect) =>
            Array.IndexOf(Known, effect) >= 0 || TryParseAmount(effect, Coins, out _);
    }

    /// <summary>Which upgrade a shot stream comes from (the balance report credits damage by it).</summary>
    public enum ShotSource : byte { Main, Spread, Homing, Rear, Rebound, Companion, Bomb }

    /// <summary>One stream of player shots: where it starts relative to the ship, which way it flies.</summary>
    public struct ShotStream
    {
        /// <summary>Degrees, 90 = straight up.</summary>
        public float angleDegrees;
        public float offsetX;
        public float offsetY;
        public float damageScale;
        public bool homing;
        /// <summary>Bounces once off the bottom edge (BulletFlags.Bounce).</summary>
        public bool bounce;
        public ShotSource source;

        public ShotStream(float angleDegrees, float offsetX, float offsetY, float damageScale = 1f, bool homing = false,
            ShotSource source = ShotSource.Main, bool bounce = false)
        {
            this.angleDegrees = angleDegrees;
            this.offsetX = offsetX;
            this.offsetY = offsetY;
            this.damageScale = damageScale;
            this.homing = homing;
            this.source = source;
            this.bounce = bounce;
        }
    }

    /// <summary>The player's volley from upgrade stacks. Focus tightens the spread.</summary>
    public static class PlayerShots
    {
        /// <summary>Damage of each of the two homing streams Seeker Rounds adds when there's no spread
        /// (tuned with the balance report: homing shots almost never miss).</summary>
        public const float HomingStreamDamage = 0.35f;
        /// <summary>Damage of a rebound stream on the way down.</summary>
        public const float ReboundDamage = 1f;
        /// <summary>Rebound shots come back off the floor hotter: their damage times this
        /// (BulletPool.bounceDamageScale on the player's pool).</summary>
        public const float ReboundAfterBounce = 1.6f;

        /// <summary>Fills <paramref name="output"/> (cleared first). Base: two parallel streams straight up.</summary>
        public static void Build(int spread, int rear, int homing, bool focused, List<ShotStream> output) =>
            Build(spread, rear, homing, 0, focused, output);

        /// <summary>As above, plus <paramref name="rebound"/> stacks (two bouncing streams per stack, fanning wider).</summary>
        public static void Build(int spread, int rear, int homing, int rebound, bool focused, List<ShotStream> output)
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
                output.Add(new ShotStream(90f + a, -0.2f, 0.15f, 0.75f, home, ShotSource.Spread));
                output.Add(new ShotStream(90f - a, 0.2f, 0.15f, 0.75f, home, ShotSource.Spread));
            }
            if (home && spread <= 0)
            {
                output.Add(new ShotStream(120f, -0.3f, 0.1f, HomingStreamDamage, true, ShotSource.Homing));
                output.Add(new ShotStream(60f, 0.3f, 0.1f, HomingStreamDamage, true, ShotSource.Homing));
            }
            if (rear > 0)
            {
                output.Add(new ShotStream(255f, -0.15f, -0.2f, 0.75f, false, ShotSource.Rear));
                output.Add(new ShotStream(285f, 0.15f, -0.2f, 0.75f, false, ShotSource.Rear));
            }
            for (int s = 1; s <= Math.Max(0, rebound); s++)
            {
                // Down and out from vertical (wider per stack): off the floor they climb back up at the
                // same angle, sweeping the side lanes the main guns don't reach.
                float a = 26f + 12f * (s - 1);
                output.Add(new ShotStream(270f - a, -0.2f, -0.15f, ReboundDamage, false, ShotSource.Rebound, true));
                output.Add(new ShotStream(270f + a, 0.2f, -0.15f, ReboundDamage, false, ShotSource.Rebound, true));
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
            _damageBoost = 1f;
            _fireRateBoost = 1f;
        }

        public float Damage => Stats.Get(ShmupStats.Damage);
        public float FireRate => Stats.Get(ShmupStats.FireRate);
        public float MoveSpeedScale => Stats.Get(ShmupStats.MoveSpeed);
        public float ShotSpeedScale => Stats.Get(ShmupStats.ShotSpeed);
        public float GrazeRadiusScale => Stats.Get(ShmupStats.GrazeRadius);
        public float Luck => Stats.Get(ShmupStats.Luck);
        public float MagnetScale => Stats.Get(ShmupStats.Magnet);
        public float CoinGain => Stats.Get(ShmupStats.CoinGain);
        public float SprintSpeedScale => Stats.Get(ShmupStats.SprintSpeed);
        public float StaminaMaxScale => Stats.Get(ShmupStats.StaminaMax);
        public float StaminaRegenScale => Stats.Get(ShmupStats.StaminaRegen);
        public float RoomSprintBudgetScale => Stats.Get(ShmupStats.RoomSprintBudget);
        public float BombRechargeScale => Stats.Get(ShmupStats.BombRecharge);
        public float CompanionDamageScale => Stats.Get(ShmupStats.CompanionDamage);
        public float CompanionCooldownScale => Stats.Get(ShmupStats.CompanionShieldCooldown);

        float _damageBoost = 1f, _fireRateBoost = 1f;

        /// <summary>A moment-to-moment damage multiplier on top of the stats (Ember Hunger, Red Line: see
        /// <see cref="PowerBoosts"/>). The game sets it every frame; 1 = none.</summary>
        public float DamageBoost
        {
            get => _damageBoost;
            set => _damageBoost = Math.Max(0f, value);
        }

        /// <summary>A moment-to-moment fire-rate multiplier (Red Line). 1 = none.</summary>
        public float FireRateBoost
        {
            get => _fireRateBoost;
            set => _fireRateBoost = Math.Max(0.05f, value);
        }

        /// <summary>Damage per shot right now, as a multiplier of the ship's base: the stat times the boost.</summary>
        public float ShotDamageScale => Damage * _damageBoost;

        /// <summary>
        /// Bombs the ship can hold: the starting kit (<paramref name="kitBombs"/>, PlayerDef.bombsPerLife, 0 by
        /// default) plus, with a Bomb Bay, <paramref name="bayBombs"/> and one more per extra Bay stack and per
        /// bomb_plus stack. Capped at <paramref name="maxBombs"/>.
        /// </summary>
        public int BombCapacity(int kitBombs, int bayBombs, int maxBombs)
        {
            int bay = Effect(ShmupEffects.BombBay);
            int n = Math.Max(0, kitBombs);
            if (bay > 0) n += Math.Max(0, bayBombs) + (bay - 1) + Effect(ShmupEffects.BombPlus);
            return Math.Min(n, Math.Max(0, maxBombs));
        }

        /// <summary>Seconds per bomb the Bay recharges (0 = no Bay: no recharge).</summary>
        public float BombRechargeSeconds(float baseSeconds) =>
            Effect(ShmupEffects.BombBay) > 0 ? Math.Max(0.5f, baseSeconds / Math.Max(0.05f, BombRechargeScale)) : 0f;

        /// <summary>Move speed multiplier while sprinting: 1 + (the data's bonus) x sprint_speed.</summary>
        public float SprintMultiplier(float dataMultiplier) => 1f + Math.Max(0f, dataMultiplier - 1f) * SprintSpeedScale;

        /// <summary>Bomb radius and damage: the stat times 1.5 per bigger-bombs stack.</summary>
        public float BombPower => Stats.Get(ShmupStats.BombPower) * (1f + 0.5f * Effect(ShmupEffects.BiggerBombs));

        public int Pierce => Effect(ShmupEffects.Pierce);

        public int Effect(string id) => Inventory.EffectStacks(id);

        /// <summary>Seconds between volleys for a weapon whose base interval is <paramref name="baseInterval"/>.</summary>
        public float FireInterval(float baseInterval) => Math.Max(0.02f, baseInterval / Math.Max(0.05f, FireRate * _fireRateBoost));

        /// <summary>The current volley (cached until the spread, rear, homing, rebound stacks or focus change).</summary>
        public List<ShotStream> Streams(bool focused)
        {
            int spread = Effect(ShmupEffects.Spread), rear = Effect(ShmupEffects.RearShot), homing = Effect(ShmupEffects.Homing);
            int rebound = Effect(ShmupEffects.Rebound);
            int key = (spread & 0x3F) | ((rear & 0x3F) << 6) | ((homing & 0x3F) << 12) | ((rebound & 0x3F) << 18) | (focused ? 1 << 24 : 0);
            if (key != _streamKey)
            {
                PlayerShots.Build(spread, rear, homing, rebound, focused, _streams);
                _streamKey = key;
            }
            return _streams;
        }
    }
}

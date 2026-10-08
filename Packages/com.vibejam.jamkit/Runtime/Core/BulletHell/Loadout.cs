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
        /// <summary>The ship's hitbox radius multiplier (base 1): 0.8 = a 20% smaller hitbox.</summary>
        public const string HitboxSize = "hitbox_size";
        /// <summary>Bomb Bay recharge speed multiplier (base 1): 1.5 recharges 50% faster.</summary>
        public const string BombRecharge = "bomb_recharge";
        /// <summary>The companion's shot damage multiplier (base 1).</summary>
        public const string CompanionDamage = "companion_damage";
        /// <summary>The companion's guard cooldown multiplier (base 1): 0.7 = the guard is back 30% sooner.</summary>
        public const string CompanionShieldCooldown = "companion_shield_cooldown";

        public static readonly string[] All =
        {
            Damage, FireRate, MoveSpeed, ShotSpeed, GrazeRadius, Luck, Magnet, CoinGain, BombPower,
            SprintSpeed, StaminaMax, StaminaRegen, RoomSprintBudget, HitboxSize, BombRecharge, CompanionDamage, CompanionShieldCooldown,
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
            s.SetBase(HitboxSize, 1f);
            s.SetClamp(HitboxSize, 0.4f, 2f);
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
        /// <summary>Last Gambit: on your last life, more damage; every hit ricochets once to the nearest other
        /// enemy, and kills burst in a small blast.</summary>
        public const string LastStand = "last_stand";
        /// <summary>Still Point: holding still ramps your damage up; any movement resets it.</summary>
        public const string StillPoint = "still_point";
        /// <summary>Scatter: the main guns become a short-range shotgun cone (damage falls off with distance).</summary>
        public const string Scatter = "scatter";
        /// <summary>Blade: the main guns become an arc slash right in front of the ship.</summary>
        public const string Blade = "blade";
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
            Spread, Pierce, Homing, RearShot, Rebound, BombBay, GhostStep, GrazePower, LastStand, StillPoint, Scatter, Blade,
            BiggerBombs, GrazeHeal, BombPlus, LifeUp, AutoBomb, FocusSlow,
        };

        /// <summary>True for every id the template understands (including "coins:N").</summary>
        public static bool IsKnown(string effect) =>
            Array.IndexOf(Known, effect) >= 0 || TryParseAmount(effect, Coins, out _);
    }

    /// <summary>Which upgrade a shot stream comes from (the balance report credits damage by it).</summary>
    public enum ShotSource : byte { Main, Spread, Homing, Rear, Rebound, Companion, Bomb, Scatter, Blade, Ricochet, Blast }

    /// <summary>
    /// The gun's numbers, as data (BulletHellConfig > Shots): the side streams, Seeker Rounds' homing
    /// streams, Backdraft's rebound streams, and the two close-range epics (Scatter and Blade).
    /// </summary>
    [Serializable]
    public class ShotRules
    {
        /// <summary>Damage of a Twin Spread side stream (times a main shot).</summary>
        public float spreadDamage = 0.75f;
        // Seeker Rounds
        /// <summary>Damage of a side stream when it homes (Seeker Rounds with Twin Spread), times a main shot.</summary>
        public float homingSideDamage = 0.7f;
        /// <summary>Damage of each of the two homing streams Seeker Rounds adds without Twin Spread.</summary>
        public float homingStreamDamage = 0.7f;
        /// <summary>Homing streams fire on every Nth volley only (fewer, heavier seekers).</summary>
        public int homingVolleyEvery = 3;
        /// <summary>Homing shots fly at this fraction of the shot speed (slower: dodging still matters).</summary>
        public float homingSpeedScale = 0.7f;
        // Backdraft (rare since the epic pass)
        public float reboundDamage = 0.7f;
        /// <summary>Damage times this once a rebound shot bounces off the floor.</summary>
        public float reboundAfterBounce = 1.35f;
        public float reboundAngle = 26f;
        // Scatter (close range: a shotgun cone instead of the main guns)
        public int scatterPellets = 6;
        public float scatterConeDegrees = 30f;
        /// <summary>Pellet damage point-blank, times a main shot.</summary>
        public float scatterDamage = 1.4f;
        /// <summary>The scatter fires this much slower than the main guns.</summary>
        public float scatterIntervalScale = 1.4f;
        /// <summary>Seconds a pellet flies (its range = shot speed x this).</summary>
        public float scatterLifetime = 0.25f;
        /// <summary>Damage left at the end of a pellet's range (0.25 = a quarter).</summary>
        public float scatterFalloff = 0.5f;
        // Blade (very close range: an arc slash in front instead of the main guns)
        public float bladeInterval = 0.2f;
        public float bladeRange = 1.7f;
        public float bladeHalfAngle = 70f;
        /// <summary>Damage per second of the slash as a multiple of the main guns' base DPS.</summary>
        public float bladeDpsScale = 3.5f;

        /// <summary>Damage left after <paramref name="age"/> seconds of a scatter pellet's flight (1 point-blank).</summary>
        public float ScatterFalloffAt(float age)
        {
            float k = scatterLifetime > 0f ? Math.Min(1f, Math.Max(0f, age / scatterLifetime)) : 0f;
            return 1f + (scatterFalloff - 1f) * k;
        }
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
        /// <summary>Bounces once off the bottom edge (BulletFlags.Bounce).</summary>
        public bool bounce;
        public ShotSource source;
        /// <summary>Times the shot speed (homing streams fly slower).</summary>
        public float speedScale;
        /// <summary>Seconds it flies; 0 = the player's shot lifetime (scatter pellets are short).</summary>
        public float lifetime;
        /// <summary>Damage falls off with distance (BulletFlags.Falloff: scatter pellets).</summary>
        public bool falloff;
        /// <summary>Fires on every Nth volley only (0 or 1 = every volley).</summary>
        public int everyNth;

        /// <summary>Does it fire on volley number <paramref name="volley"/> (counted from 0)?</summary>
        public bool FiresOn(int volley) => everyNth <= 1 || volley % everyNth == 0;

        public ShotStream(float angleDegrees, float offsetX, float offsetY, float damageScale = 1f, bool homing = false,
            ShotSource source = ShotSource.Main, bool bounce = false, float speedScale = 1f, float lifetime = 0f, bool falloff = false)
        {
            this.angleDegrees = angleDegrees;
            this.offsetX = offsetX;
            this.offsetY = offsetY;
            this.damageScale = damageScale;
            this.homing = homing;
            this.source = source;
            this.bounce = bounce;
            this.speedScale = speedScale;
            this.lifetime = lifetime;
            this.falloff = falloff;
            everyNth = 0;
        }

        /// <summary>The bullet flags this stream's shots carry.</summary>
        public BulletFlags Flags =>
            (homing ? BulletFlags.Homing : BulletFlags.None) | (bounce ? BulletFlags.Bounce : BulletFlags.None) | (falloff ? BulletFlags.Falloff : BulletFlags.None);
    }

    /// <summary>The player's volley from upgrade stacks. Focus tightens the spread.</summary>
    public static class PlayerShots
    {
        static readonly ShotRules Defaults = new ShotRules();

        static ShotStream Every(ShotStream s, int n)
        {
            s.everyNth = n;
            return s;
        }

        /// <summary>Fills <paramref name="output"/> (cleared first). Base: two parallel streams straight up.</summary>
        public static void Build(int spread, int rear, int homing, bool focused, List<ShotStream> output) =>
            Build(spread, rear, homing, 0, 0, 0, focused, Defaults, output);

        /// <summary>As above, plus <paramref name="rebound"/> stacks (two bouncing streams per stack, fanning wider).</summary>
        public static void Build(int spread, int rear, int homing, int rebound, bool focused, List<ShotStream> output) =>
            Build(spread, rear, homing, rebound, 0, 0, focused, Defaults, output);

        /// <summary>
        /// The whole volley. <paramref name="scatter"/>: the two main streams become a short-range shotgun cone;
        /// <paramref name="blade"/>: no main streams at all (the blade slashes instead, see <see cref="BladeArc"/>).
        /// </summary>
        public static void Build(int spread, int rear, int homing, int rebound, int scatter, int blade, bool focused, ShotRules rules, List<ShotStream> output)
        {
            output.Clear();
            rules = rules ?? Defaults;
            float gap = focused ? 0.09f : 0.16f;
            if (blade > 0) { }
            else if (scatter > 0)
            {
                int n = Math.Max(1, rules.scatterPellets);
                for (int i = 0; i < n; i++)
                {
                    float a = n == 1 ? 90f : 90f - rules.scatterConeDegrees * 0.5f + rules.scatterConeDegrees * i / (n - 1);
                    output.Add(new ShotStream(a, 0f, 0.3f, rules.scatterDamage, false, ShotSource.Scatter, false, 1f, rules.scatterLifetime, true));
                }
            }
            else
            {
                output.Add(new ShotStream(90f, -gap, 0.3f));
                output.Add(new ShotStream(90f, gap, 0.3f));
            }
            bool home = homing > 0;
            float homeSpeed = home ? rules.homingSpeedScale : 1f;
            float step = focused ? 4f : 11f;
            for (int s = 1; s <= Math.Max(0, spread); s++)
            {
                float a = step * s;
                float d = home ? rules.homingSideDamage : rules.spreadDamage;
                output.Add(Every(new ShotStream(90f + a, -0.2f, 0.15f, d, home, ShotSource.Spread, false, homeSpeed), home ? rules.homingVolleyEvery : 1));
                output.Add(Every(new ShotStream(90f - a, 0.2f, 0.15f, d, home, ShotSource.Spread, false, homeSpeed), home ? rules.homingVolleyEvery : 1));
            }
            if (home && spread <= 0)
            {
                output.Add(Every(new ShotStream(120f, -0.3f, 0.1f, rules.homingStreamDamage, true, ShotSource.Homing, false, homeSpeed), rules.homingVolleyEvery));
                output.Add(Every(new ShotStream(60f, 0.3f, 0.1f, rules.homingStreamDamage, true, ShotSource.Homing, false, homeSpeed), rules.homingVolleyEvery));
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
                float a = rules.reboundAngle + 12f * (s - 1);
                output.Add(new ShotStream(270f - a, -0.2f, -0.15f, rules.reboundDamage, false, ShotSource.Rebound, true));
                output.Add(new ShotStream(270f + a, 0.2f, -0.15f, rules.reboundDamage, false, ShotSource.Rebound, true));
            }
        }
    }

    /// <summary>The Blade's slash: an arc in front of the ship (pure geometry and its damage per slash).</summary>
    public static class BladeArc
    {
        /// <summary>Does a target circle touch the arc of <paramref name="range"/> within
        /// <paramref name="halfAngleDegrees"/> of straight up from <paramref name="ship"/>?</summary>
        public static bool Touches(System.Numerics.Vector2 ship, System.Numerics.Vector2 target, float targetRadius, float range, float halfAngleDegrees)
        {
            var d = target - ship;
            float dist = d.Length();
            if (dist > range + targetRadius) return false;
            if (dist <= targetRadius) return true;
            double angle = Math.Atan2(d.Y, d.X) * 180.0 / Math.PI; // 90 = straight up
            double off = Math.Abs(angle - 90.0);
            if (off > 180.0) off = 360.0 - off;
            // A big target can be touched at the edge of the arc.
            double slack = Math.Asin(Math.Min(1.0, targetRadius / dist)) * 180.0 / Math.PI;
            return off <= halfAngleDegrees + slack;
        }

        /// <summary>One slash's damage: the main guns' base DPS (two streams of <paramref name="shotDamage"/> every
        /// <paramref name="fireInterval"/>) times the blade's DPS scale, over its interval.</summary>
        public static float SlashDamage(ShotRules rules, float shotDamage, float fireInterval) =>
            rules.bladeDpsScale * (2f * shotDamage / Math.Max(0.01f, fireInterval)) * rules.bladeInterval;
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
        ShotRules _shots = new ShotRules();

        /// <summary>The gun's numbers (BulletHellConfig > Shots). Setting it rebuilds the volley.</summary>
        public ShotRules Shots
        {
            get => _shots;
            set { _shots = value ?? new ShotRules(); _streamKey = -1; }
        }

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
        public float HitboxScale => Stats.Get(ShmupStats.HitboxSize);
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
        public float FireInterval(float baseInterval)
        {
            float interval = baseInterval / Math.Max(0.05f, FireRate * _fireRateBoost);
            if (Effect(ShmupEffects.Scatter) > 0 && Effect(ShmupEffects.Blade) <= 0) interval *= Math.Max(0.1f, _shots.scatterIntervalScale);
            return Math.Max(0.02f, interval);
        }

        /// <summary>The current volley (cached until the spread, rear, homing, rebound stacks or focus change).</summary>
        public List<ShotStream> Streams(bool focused)
        {
            int spread = Effect(ShmupEffects.Spread), rear = Effect(ShmupEffects.RearShot), homing = Effect(ShmupEffects.Homing);
            int rebound = Effect(ShmupEffects.Rebound), scatter = Effect(ShmupEffects.Scatter), blade = Effect(ShmupEffects.Blade);
            int key = (spread & 0x1F) | ((rear & 0x1F) << 5) | ((homing & 0x1F) << 10) | ((rebound & 0x1F) << 15)
                | ((scatter > 0 ? 1 : 0) << 20) | ((blade > 0 ? 1 : 0) << 21) | (focused ? 1 << 22 : 0);
            if (key != _streamKey)
            {
                PlayerShots.Build(spread, rear, homing, rebound, scatter, blade, focused, _shots, _streams);
                _streamKey = key;
            }
            return _streams;
        }
    }
}

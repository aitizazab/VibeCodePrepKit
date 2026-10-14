using System;

namespace JamKit.Skyline
{
    /// <summary>The layer of the player's space a bot threatens (docs/SKYLINE-DESIGN.md 2.5-2.6).</summary>
    public enum ThreatLayer { Floor, Walls, Air }

    /// <summary>A plate of armour over a bot's chassis; it absorbs hits where it sits and sheds when broken (2.3).</summary>
    public sealed class PlateDef
    {
        public string id;
        public float hp;

        public PlateDef(string id, float hp)
        {
            this.id = id;
            this.hp = hp;
        }
    }

    /// <summary>One bot as data (2.5): its tags, its body and its attack. Each bot's identity agent owns and tunes it; the
    /// dodge measurement (Task 11) tunes the aimed attacks on Normal.</summary>
    public sealed class EnemyDef
    {
        public string id = "", displayName = "";
        public AimModel aim;
        public ThreatLayer layer;
        public float chassisHp = 50f;
        public PlateDef[] plates = new PlateDef[0];
        /// <summary>The bot's size for aim assist and line-of-sight checks (m).</summary>
        public float bodyRadius = 0.625f;
        /// <summary>How much larger the body is built than its model or primitives (the creator, 2026-10-15: "It was impossible to land
        /// a headshot on a drone"). The Unity body scales its whole rig by this; <see cref="bodyRadius"/> is already the scaled radius.</summary>
        public float bodyScale = 1f;
        /// <summary>The weak part's extra scale on top of <see cref="bodyScale"/> (the drone's rotors with their ducts, the floor-care unit's
        /// canister, the spotlight's lens), about its own centre. The hit zones are the visible parts' own colliders, so they grow with it.</summary>
        public float weakPointScale = 1f;
        /// <summary>The weak part's visible radius in the unscaled model (m), for <see cref="BotSizes"/>: the drone's rotor duct, the
        /// canister, the lens.</summary>
        public float weakPointRadius = 0.15f;
        // The behaviour clock (BotBrain): alert, then a cycle of telegraph + attack + recovery.
        public float alertSeconds = 0.6f;
        public float telegraphSeconds = 0.4f;
        /// <summary>One full attack cycle (s): the telegraph plus the recovery.</summary>
        public float fireInterval = 1.4f;
        public float loseSightSeconds = 3f;
        // A shot.
        /// <summary>m/s; 0 is a telegraphed hitscan (the spotlight).</summary>
        public float projectileSpeed = 30f;
        public float projectileRadius = 0.15f;
        /// <summary>Degrees of random aim error on a projectile shot (a drone isn't a perfect shot; it also makes the dodge
        /// measurement a dial instead of a cliff). 0 for none.</summary>
        public float aimSpreadDeg;
        /// <summary>The drone's lagged aim (the creator, 2026-10-14): the shot goes to where the target's chest was this many seconds
        /// ago (<see cref="PositionTrail"/>) instead of leading it; a mover is missed by what it covers in the lag plus the bolt's
        /// flight. 0 for the old behaviour (a lead of <see cref="DifficultyDef.leadFactor"/> for <see cref="AimModel.LeadsYou"/>).</summary>
        public float aimLagSeconds;
        public float attackDamage = 10f;
        public float range = 40f;
        /// <summary>m/s the bot drives toward the player once awake (Task 11b's fight); 0 for bots that don't walk.</summary>
        public float moveSpeed;
        /// <summary>Seconds a bot remembers the player after losing sight: re-sighting within it skips the alert and goes straight
        /// to the telegraph (Task 11b fix round). 0 for none.</summary>
        public float rememberSeconds;
        /// <summary>m/s an awake flying bot that has lost sight drifts sideways toward a point that sees where it last saw the
        /// player (it can't pass through cover). 0 for bots that hold their post.</summary>
        public float searchSpeed;
        /// <summary>The flying bot's hover patrol (<see cref="HoverPatrol"/>): m/s of its sideways strafes round its post while
        /// awake, how far from the post it may stray (m), how high it bobs (m), and its dash when hit (m/s). 0 speed or leash:
        /// it holds its post.</summary>
        public float patrolSpeed, patrolLeash, patrolBob, patrolDashSpeed;
        /// <summary>Idle work before it wakes (IdleWork.cs, fix round "idle life"): m/s of the floor-care unit's slow cleaning
        /// round its post, or of the gondola's slow ride along a short stretch of its rail; and how far it may go (m: the
        /// floor-care unit's leash from its post, the gondola's stretch from its spawn along the rail). 0 speed or leash: it
        /// stands still until woken.</summary>
        public float idleSpeed, idleLeash;
        // The spotlight's beam (TelegraphedShot); its telegraph is telegraphSeconds.
        public float beamFollowRate = 10f, beamHitRadius = 0.6f, beamLockBefore = 0.25f;
        // The floor-care unit's acid sweep (AcidSweep).
        public float sweepSeconds = 0.8f, sweepRadius = 4.5f, sweepArcDeg = 120f, sweepWidthDeg = 24f, ankleHeight = 0.4f;
        /// <summary>Plating a fresh wreck gives back when stripped, on Normal (2.4, 7.4).</summary>
        public float scrapPlating = 20f;
        // The window-washer gondola (Gondola.cs): a rail along a wall and the mines it lays. The blast damage is attackDamage.
        /// <summary>m/s along its rail.</summary>
        public float railSpeed = 3f;
        /// <summary>m of rail travel between mines.</summary>
        public float mineSpacing = 4f;
        /// <summary>Live mines per gondola; laying one more removes the oldest.</summary>
        public int mineMax = 6;
        public float mineArmSeconds = 0.6f;
        /// <summary>The beep between trigger and blast: leaving the wall in time dodges it.</summary>
        public float mineFuseSeconds = 0.35f;
        public float mineTriggerRadius = 1.5f, mineBlastRadius = 2.5f;
        /// <summary>How long (s) an untouched mine stays on the wall before it expires.</summary>
        public float mineLifetime = 12f;
    }

    /// <summary>M1's four bots (11.2; the gondola came with Task 9b). Each call returns a new instance.</summary>
    public static class SkylineEnemies
    {
        public const string DroneId = "mall-cop-drone", FloorCareId = "floor-care-unit", SpotlightId = "spotlight",
            GondolaId = "window-washer-gondola";

        /// <summary>Officious; shots at where you were 0.2 s ago (punishes standing still; a mover is missed). Aim tag: leads you
        /// (kept for the never-mix rule; since 2026-10-14 it lags instead of leading). Layer: air.</summary>
        public static EnemyDef MallCopDrone() => new EnemyDef
        {
            id = DroneId, displayName = "Mall-cop drone", aim = AimModel.LeadsYou, layer = ThreatLayer.Air,
            chassisHp = 70f, plates = new[] { new PlateDef("shell", 30f), new PlateDef("lens-guard", 20f) }, bodyRadius = 0.63f,
            bodyScale = 1.4f, weakPointScale = 2f, weakPointRadius = 0.175f,
            alertSeconds = 0.6f, telegraphSeconds = 0.35f, fireInterval = 1.0f, loseSightSeconds = 3f, rememberSeconds = 8f, searchSpeed = 2f,
            patrolSpeed = 2.5f, patrolLeash = 2.5f, patrolBob = 0.25f, patrolDashSpeed = 4.5f,
            projectileSpeed = 30f, projectileRadius = 0.15f, aimLagSeconds = 0.2f, aimSpreadDeg = 0.5f, attackDamage = 24f, range = 45f, scrapPlating = 20f,
        };

        /// <summary>A roomba with a happy face, angry at "stains"; an ankle-height acid sweep (punishes staying on the floor).
        /// Aim: area. Layer: floor.</summary>
        public static EnemyDef FloorCareUnit() => new EnemyDef
        {
            id = FloorCareId, displayName = "Floor-care unit", aim = AimModel.Area, layer = ThreatLayer.Floor,
            chassisHp = 80f, plates = new[] { new PlateDef("lid", 40f), new PlateDef("bumper", 30f) }, bodyRadius = 0.625f,
            bodyScale = 1.25f, weakPointScale = 1.8f, weakPointRadius = 0.13f,
            alertSeconds = 0.8f, telegraphSeconds = 0.6f, fireInterval = 1.2f, loseSightSeconds = 3f,
            projectileSpeed = 0f, attackDamage = 14f, range = 10f, moveSpeed = 5f,
            sweepSeconds = 0.8f, sweepRadius = 4.5f, sweepArcDeg = 120f, sweepWidthDeg = 24f, ankleHeight = 0.4f, scrapPlating = 30f,
            idleSpeed = 0.55f, idleLeash = 1.5f,
        };

        /// <summary>A searchlight with a rifle: a 0.8 s laser telegraph that follows you with a lag and locks 0.25 s before a
        /// hitscan shot (punishes lingering exposed up high). Aim: fires at where you are now. Layer: air.</summary>
        public static EnemyDef Spotlight() => new EnemyDef
        {
            id = SpotlightId, displayName = "Spotlight", aim = AimModel.FiresAtWhereYouAre, layer = ThreatLayer.Air,
            chassisHp = 80f, plates = new[] { new PlateDef("housing-left", 55f), new PlateDef("housing-right", 55f) }, bodyRadius = 0.78f,
            bodyScale = 1.3f, weakPointScale = 1.3f, weakPointRadius = 0.33f,
            alertSeconds = 0.5f, telegraphSeconds = 0.8f, fireInterval = 2.4f, loseSightSeconds = 3f,
            projectileSpeed = 0f, attackDamage = 35f, range = 60f,
            beamFollowRate = 20f, beamHitRadius = 1.4f, beamLockBefore = 0.25f, scrapPlating = 25f,
        };

        /// <summary>A window-washer's gondola riding a rail along a wall, dropping mines as it goes (punishes a long wall-run).
        /// Aim: area. Layer: walls. The mine numbers are the EnemyDef defaults; its fight comes with the facade encounter.</summary>
        public static EnemyDef Gondola() => new EnemyDef
        {
            id = GondolaId, displayName = "Window-washer gondola", aim = AimModel.Area, layer = ThreatLayer.Walls,
            chassisHp = 60f, plates = new[] { new PlateDef("bucket-left", 20f), new PlateDef("bucket-right", 20f) }, bodyRadius = 0.8f,
            attackDamage = 25f, range = 30f, scrapPlating = 25f,
            railSpeed = 3f, mineSpacing = 4f, mineMax = 6, mineArmSeconds = 0.6f, mineFuseSeconds = 0.35f,
            mineTriggerRadius = 1.5f, mineBlastRadius = 2.5f, mineLifetime = 12f,
            idleSpeed = 0.6f, idleLeash = 2f,
        };

        public static EnemyDef[] All() => new[] { MallCopDrone(), FloorCareUnit(), Spotlight(), Gondola() };

        public static EnemyDef Find(string id) =>
            id == DroneId ? MallCopDrone() : id == FloorCareId ? FloorCareUnit() : id == SpotlightId ? Spotlight() : id == GondolaId ? Gondola() : null;
    }

    /// <summary>What the bots' sizes look like from where they are fought (the creator's playtest: the drone was "tiny. I could barely see
    /// it at the ranges I was fighting it", and its weak point could not be hit). Pure maths on the def.</summary>
    public static class BotSizes
    {
        /// <summary>The weak part's visible radius as built (m): the model's, times the body scale and the weak part's own scale.</summary>
        public static float WeakRadius(EnemyDef d) => d.weakPointRadius * d.bodyScale * d.weakPointScale;

        /// <summary>The weak part's visible radius in the old, unscaled bots (m).</summary>
        public static float OldWeakRadius(EnemyDef d) => d.weakPointRadius;

        /// <summary>How wide a disc of <paramref name="radius"/> metres looks from <paramref name="distance"/> metres, in degrees.</summary>
        public static float AngularDegrees(float radius, float distance) =>
            distance > 0f ? 2f * (float)Math.Atan(radius / distance) * (180f / (float)Math.PI) : 180f;
    }

    public struct BotDamage
    {
        public float toPlate, toChassis;
        /// <summary>The plate this hit knocked off, or -1.</summary>
        public int plateShed;
        /// <summary>This hit wrecked the bot.</summary>
        public bool wrecked;
        public float Applied => toPlate + toChassis;
    }

    /// <summary>A bot's plates and chassis. A hit on a plate that's still on is absorbed by it first; what's left carries
    /// into the chassis. A plate sheds once, where it was hit; the bot is wrecked once.</summary>
    public sealed class EnemyBody
    {
        readonly float[] _plates;

        public EnemyDef Def { get; }
        public float Chassis { get; private set; }
        public bool Wrecked { get; private set; }
        public int PlateCount => _plates.Length;

        public EnemyBody(EnemyDef def)
        {
            Def = def ?? throw new ArgumentNullException(nameof(def));
            int n = def.plates != null ? def.plates.Length : 0;
            _plates = new float[n];
            for (int i = 0; i < n; i++) _plates[i] = def.plates[i] != null ? Clamp(def.plates[i].hp, 0f) : 0f;
            Chassis = Clamp(def.chassisHp, 1f); // NaN or below 1: a 1 hp chassis; +infinity: the 1e6 ceiling
        }

        // The ceiling is the cap Damage puts on one hit, so no body is out of reach of a wrecking blow. NaN takes the floor.
        const float HpCeiling = 1e6f;
        static float Clamp(float v, float floor) => v > floor ? (v < HpCeiling ? v : HpCeiling) : floor;

        public float PlateHp(int i) => i >= 0 && i < _plates.Length ? _plates[i] : 0f;
        public bool PlateOn(int i) => PlateHp(i) > 0f;

        /// <summary><paramref name="amount"/> after every multiplier, at <paramref name="plate"/> (an index into the def's
        /// plates; -1 or a shed plate means the chassis). Zero, negative and NaN do nothing; huge amounts are capped.</summary>
        public BotDamage Damage(float amount, int plate)
        {
            var r = new BotDamage { plateShed = -1 };
            if (Wrecked || !(amount > 0f)) return r;
            float left = Math.Min(amount, 1e6f);
            if (PlateOn(plate))
            {
                float take = Math.Min(left, _plates[plate]);
                _plates[plate] -= take;
                left -= take;
                r.toPlate = take;
                if (_plates[plate] <= 0f)
                {
                    _plates[plate] = 0f;
                    r.plateShed = plate;
                }
            }
            if (left > 0f)
            {
                float take = Math.Min(left, Chassis);
                Chassis -= take;
                r.toChassis = take;
                if (Chassis <= 0f)
                {
                    Chassis = 0f;
                    Wrecked = true;
                    r.wrecked = true;
                }
            }
            return r;
        }
    }

    public struct ArmorHit
    {
        public float toPlating, toChassis;
        /// <summary>This hit took the plating to zero (the first time is Dot's teaching beat, 6.8).</summary>
        public bool platingBroke;
        public bool wrecked;
    }

    /// <summary>A wrecked bot's remains: scrap can be stripped once while it's fresh (2.4: "aggression heals").</summary>
    public sealed class Wreck
    {
        public EnemyDef def;
        public float wreckedAt;
        /// <summary>How long (s) after the wreck its scrap can still be stripped (tunable; the creator hasn't set it).</summary>
        public float freshSeconds = 8f;
        public bool stripped;

        public Wreck(EnemyDef def, float wreckedAt)
        {
            this.def = def;
            this.wreckedAt = wreckedAt;
        }

        public bool Fresh(float now) => !stripped && now >= wreckedAt && now - wreckedAt <= freshSeconds;
    }

    /// <summary>The Unit's health (2.4): plating over the chassis. Plating absorbs first and comes back only from scrap
    /// stripped off fresh wrecks; the chassis never regenerates (repair rewards and vending machines come later).</summary>
    public sealed class PlayerArmor
    {
        public float PlatingMax { get; }
        public float Plating { get; private set; }
        public float ChassisMax { get; }
        public float Chassis { get; private set; }
        public bool Wrecked { get; private set; }
        /// <summary>How many times the plating has broken this run.</summary>
        public int PlatingBreaks { get; private set; }

        // Matches the 1e6 cap Damage puts on one hit, so a hit can always wreck the Unit.
        const float HpCeiling = 1e6f;

        public PlayerArmor(float platingMax = 100f, float chassisMax = 100f)
        {
            PlatingMax = platingMax > 0f ? (platingMax < HpCeiling ? platingMax : HpCeiling) : 0f; // NaN or negative: none; +infinity: the 1e6 ceiling
            Plating = PlatingMax;
            ChassisMax = chassisMax > 1f ? (chassisMax < HpCeiling ? chassisMax : HpCeiling) : 1f;
            Chassis = ChassisMax;
        }

        public ArmorHit Damage(float amount)
        {
            var r = new ArmorHit();
            if (Wrecked || !(amount > 0f)) return r;
            float left = Math.Min(amount, 1e6f);
            if (Plating > 0f)
            {
                float take = Math.Min(left, Plating);
                Plating -= take;
                left -= take;
                r.toPlating = take;
                if (Plating <= 0f)
                {
                    Plating = 0f;
                    r.platingBroke = true;
                    PlatingBreaks++;
                }
            }
            if (left > 0f)
            {
                float take = Math.Min(left, Chassis);
                Chassis -= take;
                r.toChassis = take;
                if (Chassis <= 0f)
                {
                    Chassis = 0f;
                    Wrecked = true;
                    r.wrecked = true;
                }
            }
            return r;
        }

        /// <summary>This run's recovered scrap (money, delivered at the end of the run for recovered salvage): the part of
        /// stripped wrecks that plating had no room for. Only ever grows.</summary>
        public float RecoveredScrap { get; private set; }

        /// <summary>Scrap banked per plating point that didn't fit (data; the creator's call, 2026-10-13: excess converts to
        /// money, not stored health for later).</summary>
        public float RecoveredScrapPerPlating = 1f;

        /// <summary>Strips a fresh wreck: its scrap (times <paramref name="scale"/>, the difficulty's) fills the plating
        /// first, up to the max; whatever doesn't fit is banked as <see cref="RecoveredScrap"/>. A wreck is always used up
        /// in full by a strip, also at full plating. Returns the plating gained.</summary>
        public float Strip(Wreck w, float now, float scale = 1f)
        {
            if (Wrecked || w == null || w.def == null || !w.Fresh(now)) return 0f;
            float gain = Math.Min(Math.Max(0f, w.def.scrapPlating * (scale > 0f ? scale : 0f)), HpCeiling); // the ceiling keeps the bank finite
            if (!(gain > 0f)) return 0f; // also catches NaN
            float toPlating = Math.Min(gain, PlatingMax - Plating);
            Plating += toPlating;
            float rate = RecoveredScrapPerPlating;
            if (rate > 0f) RecoveredScrap += (gain - toPlating) * rate; // a NaN or negative rate banks nothing
            w.stripped = true;
            return toPlating;
        }
    }
}

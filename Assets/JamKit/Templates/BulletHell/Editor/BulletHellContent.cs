using System;
using System.Collections.Generic;
using Ink.UnityIntegration;
using JamKit.Patterns;
using JamKit.Roguelite;
using UnityEditor;
using UnityEngine;

namespace JamKit.BulletHell.EditorTools
{
    /// <summary>
    /// The template's starting content, as code, so it can be rebuilt reproducibly: 11 enemies,
    /// 13 waves, 14 rooms over 3 floors, 3 bosses (8 phases), 22 upgrades + a fallback (6 epics and
    /// 2 legendaries that pass the epic rubric), the companion, trials and wagers, the player, the
    /// palette and the config. Normal rebuilds only create what's missing (team edits survive), except
    /// once per <see cref="ContentRevision"/>: a config below it gets the assets that revision changed
    /// rewritten (round 2: the config, the player and the upgrades). "Reset Content to Defaults"
    /// rewrites every asset below to these values.
    /// </summary>
    public static class BulletHellContent
    {
        public const string DataDir = BulletHellBuilder.Root + "/Data";
        public const string ConfigPath = DataDir + "/BulletHellConfig.asset";
        public const string CompanionPath = DataDir + "/Companion.asset";
        /// <summary>Round 2 (2026-10-08): the reward economy, epics, encounters, remnants, companion.</summary>
        public const int ContentRevision = 2;
        /// <summary>Upgrades the template no longer ships (deleted by the round-2 migration).</summary>
        static readonly string[] Retired = { "rear_guard", "overdrive" };

        // Bullet style indices (see Styles()).
        const int Red = 0, Blue = 1, Pink = 2, Orange = 3, Rice = 4, Yellow = 5, PlayerShot = 6, PlayerHoming = 7, Violet = 8, Needle = 9;

        static bool _reset;
        static int _fromRevision;

        public static BulletHellConfig EnsureAll(BulletHellArt.Set art, bool reset)
        {
            _reset = reset;
            var existing = AssetDatabase.LoadAssetAtPath<BulletHellConfig>(ConfigPath);
            // A config from before revisions existed reads 0: it is revision 1 (the first shipped content).
            _fromRevision = existing != null ? Math.Max(1, existing.contentRevision) : ContentRevision;
            bool migrate = existing != null && existing.contentRevision < ContentRevision;
            if (migrate) Debug.Log("JAMKIT_BULLETHELL content revision " + existing.contentRevision + " -> " + ContentRevision + ": rewriting the config, player and upgrades once.");
            foreach (var d in new[] { "Enemies", "Waves", "Rooms", "Bosses", "Upgrades" })
                BulletHellBuilder.EnsureFolder(DataDir + "/" + d);
            if (migrate || reset)
                foreach (var id in Retired)
                {
                    string path = DataDir + "/Upgrades/" + id + ".asset";
                    if (AssetDatabase.LoadAssetAtPath<UpgradeAsset>(path) != null) AssetDatabase.DeleteAsset(path);
                }

            var player = Asset<PlayerDef>(DataDir + "/Player.asset", 2, p =>
            {
                p.sprite = art.ship;
                p.color = new Color(0.55f, 0.95f, 1f);
                p.size = 0.75f;
                p.moveSpeed = 7.5f;
                p.focusSpeed = 3.2f;
                p.focusEnabled = false; // Shift is Sprint; Focus stays available behind this flag
                p.sprint = new SprintDef();
                p.hitboxRadius = 0.07f;
                p.grazeRadius = 0.6f;
                p.lives = 3;
                p.bombsPerLife = 0; // bombs come from the Bomb Bay epic (docs/DESIGN-ROGUELITE.md, section 2)
                p.maxBombs = 6;
                p.bayBombs = 2;
                p.bombRechargeSeconds = 20f;
                p.shotStyle = PlayerShot;
                p.homingShotStyle = PlayerHoming;
                p.baseStats = new List<StatEntry>();
            });

            // ---------- Enemies ----------
            var drone = Enemy("Drone", art.orb, new Color(0.35f, 0.95f, 0.85f), 4f, 0.6f, 0.3f, 100, 1,
                new EnemyMoveDef { kind = JamKit.BulletHell.EnemyMoveKind.Sweep, speed = 3.2f, amplitude = 0.6f, frequency = 0.35f },
                Seq(false, 0f, (0f, P(new PatternDef { shape = PatternShape.Aimed, count = 1, speed = 3.6f, shots = 0, shotInterval = 1.6f, startDelay = 0.5f }, Red))));
            var gnat = Enemy("Gnat", art.triangle, new Color(0.85f, 1f, 0.4f), 3f, 0.5f, 0.25f, 80, 1,
                new EnemyMoveDef { kind = JamKit.BulletHell.EnemyMoveKind.Dive, enterSeconds = 0.9f, actSeconds = 0.5f, speed = 7f },
                new PatternSequence());
            var turret = Enemy("Turret", art.square, new Color(0.5f, 0.6f, 1f), 22f, 0.9f, 0.42f, 300, 3,
                new EnemyMoveDef { kind = JamKit.BulletHell.EnemyMoveKind.Hover, enterSeconds = 1.2f, actSeconds = 9f, speed = 4f },
                Seq(true, 1.4f, (0f, P(PatternLibrary.AimedTriple(5f), Blue))));
            var spinner = Enemy("Spinner", art.diamond, new Color(1f, 0.5f, 0.8f), 30f, 0.9f, 0.42f, 400, 4,
                new EnemyMoveDef { kind = JamKit.BulletHell.EnemyMoveKind.Strafe, enterSeconds = 1.2f, amplitude = 2f, frequency = 0.15f, actSeconds = 12f, speed = 4f },
                Seq(false, 0f, (0f, P(new PatternDef { shape = PatternShape.Spiral, count = 3, rotatePerShotDegrees = 13f, shots = 0, shotInterval = 0.13f, speed = 2.8f, lifetime = 9f }, Red))), spin: 120f);
            var bloom = Enemy("Bloom", art.hexagon, new Color(0.75f, 0.5f, 1f), 40f, 1f, 0.48f, 450, 4,
                new EnemyMoveDef { kind = JamKit.BulletHell.EnemyMoveKind.Hover, enterSeconds = 1.3f, actSeconds = 12f, speed = 4f },
                Seq(false, 0f, (0f, P(new PatternDef { shape = PatternShape.Ring, count = 16, speed = 2.6f, shots = 0, shotInterval = 2.2f, rotatePerShotDegrees = 11f }, Pink))));
            var lancer = Enemy("Lancer", art.triangle, new Color(1f, 0.4f, 0.35f), 16f, 0.7f, 0.33f, 250, 2,
                new EnemyMoveDef { kind = JamKit.BulletHell.EnemyMoveKind.Strafe, enterSeconds = 1f, amplitude = 3f, frequency = 0.25f, actSeconds = 8f, speed = 5f },
                Seq(true, 1.5f, (0f, P(PatternLibrary.AimedStack(4, 3.5f), Rice))));
            var wallmaker = Enemy("Wallmaker", art.wide, new Color(1f, 0.65f, 0.3f), 55f, 1.3f, 0.5f, 500, 5,
                new EnemyMoveDef { kind = JamKit.BulletHell.EnemyMoveKind.Hover, enterSeconds = 1.4f, actSeconds = 12f, speed = 3f },
                Seq(false, 0f, (0f, P(PatternLibrary.Wall(11, 11f, 2.2f), Orange, d => { d.shots = 0; d.shotInterval = 2.8f; d.lifetime = 9f; }))));
            var curler = Enemy("Curler", art.star, new Color(0.4f, 0.9f, 1f), 35f, 0.95f, 0.45f, 450, 4,
                new EnemyMoveDef { kind = JamKit.BulletHell.EnemyMoveKind.Hover, enterSeconds = 1.2f, actSeconds = 11f, speed = 4f },
                Seq(true, 1.8f, (0f, P(PatternLibrary.CurlRing(14, 2.4f), Blue))), spin: -60f);
            var brute = Enemy("Brute", art.square, new Color(1f, 0.45f, 0.25f), 28f, 0.95f, 0.45f, 350, 3,
                new EnemyMoveDef { kind = JamKit.BulletHell.EnemyMoveKind.Drift, speed = 0.8f, amplitude = 1.5f, frequency = 0.2f },
                Seq(false, 0f, (0f, P(PatternLibrary.Shotgun(8, 4.5f), Red, d => { d.shots = 0; d.shotInterval = 1.8f; d.startDelay = 0.8f; }))));
            var heavyBloom = Enemy("HeavyBloom", art.hexagon, new Color(1f, 0.4f, 0.85f), 160f, 1.6f, 0.75f, 1500, 12,
                new EnemyMoveDef { kind = JamKit.BulletHell.EnemyMoveKind.Hover, enterSeconds = 1.6f, actSeconds = 0f, speed = 3f },
                Seq(true, 0.8f,
                    (0f, P(PatternLibrary.RingBurst(22, 2.4f), Pink)),
                    (0.9f, P(PatternLibrary.AimedTriple(5.5f), Blue)),
                    (1.8f, P(PatternLibrary.RingBurst(22, 2.4f), Pink, d => d.baseAngleDegrees = 8f))),
                big: true, display: "Heavy Bloom");
            var twinSpinner = Enemy("TwinSpinner", art.diamond, new Color(1f, 0.82f, 0.3f), 180f, 1.6f, 0.75f, 1600, 12,
                new EnemyMoveDef { kind = JamKit.BulletHell.EnemyMoveKind.Strafe, enterSeconds = 1.6f, amplitude = 2.5f, frequency = 0.1f, actSeconds = 0f, speed = 3f },
                Seq(true, 1f, (0f, P(PatternLibrary.SpiralFlower(3, 2.6f), Yellow, d => { d.shots = 50; d.shotInterval = 0.08f; }))),
                spin: 90f, big: true, display: "Twin Spinner");

            // ---------- Waves ----------
            var T = EnterSide.Top; var L = EnterSide.Left; var R = EnterSide.Right;
            var w1 = Wave("W01_DroneLines", 10f, S(0f, drone, 0f, 3.6f, L, 5, 0f, 0f, 0.45f), S(1.2f, drone, 0f, 2.4f, R, 5, 0f, 0f, 0.45f));
            var w2 = Wave("W02_GnatRain", 9f, S(0f, gnat, 0f, 3.2f, T, 6, 1.8f, 0f, 0.3f), S(3f, gnat, 0.9f, 2.6f, T, 5, 1.8f, 0f, 0.3f));
            var w3 = Wave("W03_TurretPair", 12f, S(0f, turret, -3f, 3.5f, T, 1, 0f, 0f, 0f, true), S(2f, drone, 0f, 2.2f, L, 4, 0f, 0f, 0.5f));
            var w4 = Wave("W04_Spinner", 12f, S(0f, spinner, 0f, 3.3f), S(3f, gnat, 0f, 3f, T, 4, 2.5f, 0f, 0.35f));
            var w5 = Wave("W05_BloomDuo", 13f, S(0f, bloom, -2.6f, 3.1f, T, 1, 0f, 0f, 0f, true), S(4f, drone, 0f, 4.2f, R, 4, 0f, 0f, 0.5f));
            var w6 = Wave("W06_Lancers", 11f, S(0f, lancer, 0f, 4f, T, 3, 2.6f, -0.4f, 0.3f), S(2.5f, drone, 0f, 2.4f, L, 4, 0f, 0f, 0.45f));
            var w7 = Wave("W07_Wall", 14f, S(0f, wallmaker, 0f, 4.2f), S(2f, lancer, -3.5f, 3f, T, 1, 0f, 0f, 0f, true));
            var w8 = Wave("W08_Curlers", 13f, S(0f, curler, -3f, 3.2f, T, 1, 0f, 0f, 0f, true), S(3.5f, gnat, 0f, 3.4f, T, 5, 2f, 0f, 0.3f));
            var w9 = Wave("W09_Brutes", 12f, S(0f, brute, 0f, 0f, T, 3, 3.2f, 0f, 0.6f), S(2f, drone, 0f, 3.8f, R, 5, 0f, 0f, 0.4f));
            var w10 = Wave("W10_Swarm", 12f, S(0f, drone, 0f, 3.8f, L, 4, 0f, 0f, 0.4f), S(0f, drone, 0f, 2.8f, R, 4, 0f, 0f, 0.4f), S(1.5f, turret, 0f, 3.4f));
            var e1 = Wave("E01_HeavyBloom", 0f, S(0f, heavyBloom, 0f, 3.1f), S(3f, drone, 0f, 4.4f, L, 4, 0f, 0f, 0.5f), S(8f, drone, 0f, 4.4f, R, 4, 0f, 0f, 0.5f));
            var e2 = Wave("E02_TwinSpinner", 0f, S(0f, twinSpinner, 0f, 3.2f), S(4f, gnat, 0f, 3.6f, T, 4, 2.6f, 0f, 0.4f), S(9f, gnat, 0f, 3.6f, T, 4, 2.6f, 0f, 0.4f));
            var e3 = Wave("E03_EliteEscort", 0f, S(0f, heavyBloom, -2.8f, 3.3f), S(1.5f, curler, 3f, 3f), S(5f, drone, 0f, 2.2f, R, 5, 0f, 0f, 0.45f));

            // ---------- Rooms ----------
            var floor1Battle = new List<RoomDef>
            {
                Room("F1_BattleA", RoomType.Combat, 5, w1, w2, w3),
                Room("F1_BattleB", RoomType.Combat, 5, w2, w1, w4),
                Room("F1_BattleC", RoomType.Combat, 5, w1, w3, w6),
            };
            var floor1Elite = new List<RoomDef> { Room("F1_EliteA", RoomType.Elite, 10, w1, e1) };
            var floor2Battle = new List<RoomDef>
            {
                Room("F2_BattleA", RoomType.Combat, 6, w4, w5, w6),
                Room("F2_BattleB", RoomType.Combat, 6, w3, w7, w2),
                Room("F2_BattleC", RoomType.Combat, 6, w6, w8, w1),
            };
            var floor2Elite = new List<RoomDef> { Room("F2_EliteA", RoomType.Elite, 12, w2, e2), Room("F2_EliteB", RoomType.Elite, 12, w10, e1) };
            var floor3Battle = new List<RoomDef>
            {
                Room("F3_BattleA", RoomType.Combat, 8, w7, w8, w9),
                Room("F3_BattleB", RoomType.Combat, 8, w5, w9, w10),
                Room("F3_BattleC", RoomType.Combat, 8, w10, w7, w5),
            };
            var floor3Elite = new List<RoomDef> { Room("F3_EliteA", RoomType.Elite, 14, w9, e3), Room("F3_EliteB", RoomType.Elite, 14, w4, e2) };

            // ---------- Bosses ----------
            var gatekeeper = Asset<BossDef>(DataDir + "/Bosses/Gatekeeper.asset", b =>
            {
                b.displayName = "Gatekeeper";
                b.title = "Warden of the Outer Ring";
                b.hp = 700f;
                b.sprite = art.bossGate;
                b.color = new Color(1f, 0.55f, 0.4f);
                b.size = 2.4f;
                b.radius = 0.95f;
                b.homePosition = new Vector2(0f, 3.1f);
                b.score = 10000;
                b.coins = 30;
                b.phaseChangeInvulnerable = 1.5f;
                b.phases = new List<BossPhase>
                {
                    Phase("Opening Salvo", 1f, BossMoveKind.Sway, 3f, 0.12f, Color.white, Seq(true, 0.7f,
                        (0f, P(PatternLibrary.RingBurst(20, 2.6f), Pink)),
                        (0.8f, P(PatternLibrary.AimedTriple(5f), Blue)),
                        (1.6f, P(PatternLibrary.RingBurst(20, 2.6f), Pink, d => d.baseAngleDegrees = 9f)))),
                    Phase("Petal Storm", 0.5f, BossMoveKind.Hold, 0f, 0f, new Color(1f, 0.8f, 0.8f), Seq(true, 0.8f,
                        (0f, P(PatternLibrary.SpiralFlower(2, 2.8f), Red, d => { d.shots = 60; d.shotInterval = 0.08f; })),
                        (2.5f, P(PatternLibrary.AimedStack(5, 3.5f), Rice)))),
                };
            });
            var forgemother = Asset<BossDef>(DataDir + "/Bosses/Forgemother.asset", b =>
            {
                b.displayName = "Forgemother";
                b.title = "Queen of the Furnace";
                b.hp = 1300f;
                b.sprite = art.bossForge;
                b.color = new Color(1f, 0.65f, 0.25f);
                b.size = 2.6f;
                b.radius = 1f;
                b.homePosition = new Vector2(0f, 3.1f);
                b.score = 20000;
                b.coins = 40;
                b.phaseChangeInvulnerable = 1.5f;
                b.phases = new List<BossPhase>
                {
                    Phase("Molten Walls", 1f, BossMoveKind.Sway, 2.5f, 0.1f, Color.white, Seq(true, 0.8f,
                        (0f, P(PatternLibrary.Wall(13, 12f, 2f), Orange)),
                        (1.2f, P(PatternLibrary.Shotgun(9, 4.5f), Red)),
                        (2.4f, P(PatternLibrary.Wall(12, 11f, 2f), Orange)))),
                    Phase("Sparks", 0.66f, BossMoveKind.Figure8, 3f, 0.1f, new Color(1f, 0.9f, 0.7f), Seq(true, 0.6f,
                        (0f, P(PatternLibrary.CurlRing(16, 2.4f), Yellow)),
                        (1.6f, P(PatternLibrary.AimedStack(6, 4f), Rice)))),
                    Phase("Meltdown", 0.33f, BossMoveKind.Hold, 0f, 0f, new Color(1f, 0.6f, 0.5f), Seq(true, 1f,
                        (0f, P(PatternLibrary.SpiralFlower(3, 2.5f), Red, d => { d.shots = 70; d.shotInterval = 0.08f; })),
                        (0f, P(PatternLibrary.RingBurst(12, 1.8f), Orange, d => { d.shots = 4; d.shotInterval = 1.2f; })))),
                };
            });
            var lastLight = Asset<BossDef>(DataDir + "/Bosses/LastLight.asset", b =>
            {
                b.displayName = "The Last Light";
                b.title = "Heart of the Core";
                b.hp = 2000f;
                b.sprite = art.bossLight;
                b.color = new Color(0.85f, 0.6f, 1f);
                b.size = 2.8f;
                b.radius = 1.05f;
                b.homePosition = new Vector2(0f, 3.1f);
                b.score = 40000;
                b.coins = 0;
                b.phaseChangeInvulnerable = 1.8f;
                b.phases = new List<BossPhase>
                {
                    Phase("Halo", 1f, BossMoveKind.Sway, 3.2f, 0.09f, Color.white, Seq(true, 0.6f,
                        (0f, P(new PatternDef { shape = PatternShape.Spread, count = 7, spreadDegrees = 70f, aimAtTarget = true, speed = 4f, shots = 4, shotInterval = 0.25f }, Needle)),
                        (1.4f, P(PatternLibrary.RingBurst(24, 2.4f), Violet, d => { d.shots = 2; d.shotInterval = 0.6f; d.rotatePerShotDegrees = 7f; })))),
                    Phase("Pursuit", 0.7f, BossMoveKind.Chase, 4f, 0.6f, new Color(0.8f, 0.8f, 1f), Seq(true, 0.7f,
                        (0f, P(PatternLibrary.Shotgun(10, 5f), Red, d => { d.shots = 3; d.shotInterval = 0.35f; })),
                        (1.2f, P(new PatternDef { shape = PatternShape.Spiral, count = 4, rotatePerShotDegrees = 15f, shots = 30, shotInterval = 0.08f, speed = 3f }, Pink)))),
                    Phase("Supernova", 0.4f, BossMoveKind.Dash, 3f, 0.5f, new Color(1f, 0.75f, 1f), Seq(true, 0.8f,
                        (0f, P(PatternLibrary.SpiralFlower(4, 2.6f), Blue, d => { d.shots = 80; d.shotInterval = 0.075f; })),
                        (1f, P(PatternLibrary.CurlRing(18, 2.2f), Violet)),
                        (3f, P(PatternLibrary.AimedTriple(6f), Needle)))),
                };
            });

            // ---------- Upgrades ----------
            // Commons and rares: numbers and small verbs (normal battles offer these). Epics and legendaries
            // change a verb and pass the rubric (bosses, trials, sacrifices and wagers offer them). Each epic's
            // rubric and design notes say which situations it's for; the balance report checks they matter.
            const string Gun = "gun", Move = "move", Bomb = "bomb", Defense = "defense", Economy = "economy", Risk = "risk";
            var ups = new List<UpgradeAsset>
            {
                Up("sharp_rounds", "Sharp Rounds", Rarity.Common, 3, "Shots deal 20% more damage.", "Dm", new Color(1f, 0.45f, 0.45f),
                    new[] { M(ShmupStats.Damage, StatOp.PercentAdd, 0.2f) }, Tags(Gun)),
                Up("rapid_trigger", "Rapid Trigger", Rarity.Common, 3, "Fire 15% faster.", "Rt", new Color(1f, 0.75f, 0.35f),
                    new[] { M(ShmupStats.FireRate, StatOp.PercentAdd, 0.15f) }, Tags(Gun)),
                Up("light_frame", "Light Frame", Rarity.Common, 2, "Move 10% faster.", "Sp", new Color(0.5f, 1f, 0.8f),
                    new[] { M(ShmupStats.MoveSpeed, StatOp.PercentAdd, 0.1f) }, Tags(Move)),
                Up("wide_halo", "Wide Halo", Rarity.Common, 2, "A bigger graze ring: more points from near misses.", "Gz", new Color(0.7f, 0.9f, 1f),
                    new[] { M(ShmupStats.GrazeRadius, StatOp.PercentAdd, 0.35f) }, Tags(Risk)),
                Up("coin_magnet", "Coin Magnet", Rarity.Common, 2, "Coins fly to you from further away, and enemies drop 25% more.", "Mg", new Color(1f, 0.85f, 0.3f),
                    new[] { M(ShmupStats.Magnet, StatOp.PercentAdd, 1f), M(ShmupStats.CoinGain, StatOp.PercentAdd, 0.25f) }, Tags(Economy)),
                Up("hot_barrels", "Hot Barrels", Rarity.Common, 2, "Shots fly 25% faster and hit a little harder.", "Hb", new Color(1f, 0.6f, 0.4f),
                    new[] { M(ShmupStats.ShotSpeed, StatOp.PercentAdd, 0.25f), M(ShmupStats.Damage, StatOp.PercentAdd, 0.05f) }, Tags(Gun)),
                Up("spare_bomb", "Spare Bomb", Rarity.Common, 2, "Your Bomb Bay holds one more bomb (and you get it now).", "Bm", new Color(1f, 0.85f, 0.4f),
                    null, Tags(Bomb), Needs(ShmupEffects.BombBay), ShmupEffects.BombPlus),
                Up("quick_fuse", "Quick Fuse", Rarity.Common, 2, "Your Bomb Bay recharges 35% faster.", "QF", new Color(1f, 0.75f, 0.35f),
                    new[] { M(ShmupStats.BombRecharge, StatOp.PercentAdd, 0.35f) }, Tags(Bomb), Needs(ShmupEffects.BombBay)),
                Up("twin_spread", "Twin Spread", Rarity.Rare, 2, "Two more streams fan out to the sides.", "Sd", new Color(0.4f, 0.75f, 1f),
                    null, Tags(Gun), null, ShmupEffects.Spread),
                Up("piercing_shot", "Piercing Shot", Rarity.Rare, 2, "Shots pass through one more enemy and hit 8% harder.", "Pc", new Color(0.6f, 0.8f, 1f),
                    new[] { M(ShmupStats.Damage, StatOp.PercentAdd, 0.08f) }, Tags(Gun), null, ShmupEffects.Pierce),
                Up("big_bang", "Big Bang", Rarity.Rare, 2, "Bombs are 50% bigger and stronger.", "BB", new Color(1f, 0.7f, 0.3f),
                    null, Tags(Bomb), Needs(ShmupEffects.BombBay), ShmupEffects.BiggerBombs),
                Up("lucky_charm", "Lucky Charm", Rarity.Rare, 2, "Rarer upgrades show up more often.", "Lk", new Color(0.5f, 1f, 0.5f),
                    new[] { M(ShmupStats.Luck, StatOp.Flat, 0.6f) }, Tags(Economy)),
                Up("close_shave", "Close Shave", Rarity.Rare, 1, "Every 120 grazes restore half a life (or a bomb, or coins, when you're full).", "CS", new Color(0.5f, 1f, 0.7f),
                    null, Tags(Defense, Risk), null, ShmupEffects.GrazeHeal),
                Up("extra_life", "Extra Life", Rarity.Rare, 1, "+1 life.", "1U", new Color(1f, 0.45f, 0.55f),
                    null, Tags(Defense), null, ShmupEffects.LifeUp),
                Up("kindred_sting", "Kindred Sting", Rarity.Rare, 2, "Your companion's shots hit 60% harder.", "KS", new Color(1f, 0.85f, 0.55f),
                    new[] { M(ShmupStats.CompanionDamage, StatOp.PercentAdd, 0.6f) }, Tags(BulletHellGame.CompanionTag)),
                Up("steady_wings", "Steady Wings", Rarity.Rare, 2, "Your companion's guard comes back 30% sooner.", "SW", new Color(1f, 0.9f, 0.65f),
                    new[] { M(ShmupStats.CompanionShieldCooldown, StatOp.PercentAdd, -0.3f) }, Tags(BulletHellGame.CompanionTag, Defense)),

                Epic(Up("seeker_rounds", "Seeker Rounds", Rarity.Epic, 1, "Your side streams curve toward enemies: dodge where you like, the damage still lands.", "Sk", new Color(0.8f, 0.6f, 1f),
                        null, Tags(Gun), null, ShmupEffects.Homing),
                    UpgradeVerb.Aim, new[] { "waves", "sides", "crowded" }, "Anywhere: it frees you from lining up under enemies.",
                    "Embers that know where they're going.", "+10% damage", new[] { M(ShmupStats.Damage, StatOp.PercentAdd, 0.1f) },
                    "Changes aiming: dodge freely, damage still lands. Strongest damage epic (balance report: about x1.55 DPS); its homing streams were cut from 0.6 to 0.35 damage so it doesn't dwarf the rest."),
                Epic(Up("backdraft", "Backdraft", Rarity.Epic, 2, "Two streams fire down, bounce off the floor and climb back hotter (+60% damage). The lower you sit, the sooner they return.", "BD", new Color(1f, 0.55f, 0.3f),
                        null, Tags(Gun), null, ShmupEffects.Rebound),
                    UpgradeVerb.Aim, new[] { "waves", "sides", "turrets" }, "Low on the screen, where players sit: the rear gun's replacement.",
                    "The forge's bellows: what goes down comes back up hotter.", "+10% damage", new[] { M(ShmupStats.Damage, StatOp.PercentAdd, 0.1f) },
                    "Replaces the rear twin gun, which the playtest and the balance report found useless (players sit low, enemies never get behind). Rebound shots reward sitting low and cover the lanes beside the main guns (about x1.26 DPS, 23% of all damage)."),
                Epic(Up("bomb_bay", "Bomb Bay", Rarity.Epic, 2, "Hold 2 bombs (K / right click / Space) that recharge one every 20 s of fighting. More stacks hold more.", "BY", new Color(1f, 0.85f, 0.4f),
                        null, Tags(Bomb, Defense), null, ShmupEffects.BombBay),
                    UpgradeVerb.Risk, new[] { "boss", "crowded", "low_life" }, "Anywhere: a panic button for the densest moments.",
                    "A furnace hatch full of stored fire.", "+1 bomb, recharge 15% faster", new[] { M(ShmupStats.BombRecharge, StatOp.PercentAdd, 0.15f) },
                    "Bombs left the starting kit (rarely used, odd as free kit). Now an epic with its own upgrades: Spare Bomb, Quick Fuse, Big Bang, Guardian Angel. Balance report: about 40% fewer hits."),
                Epic(Up("guardian_angel", "Guardian Angel", Rarity.Epic, 1, "When you're hit, a bomb goes off instead of losing a life (if your Bay has one).", "GA", new Color(1f, 0.95f, 0.7f),
                        null, Tags(Bomb, Defense), Needs(ShmupEffects.BombBay), ShmupEffects.AutoBomb),
                    UpgradeVerb.Risk, new[] { "boss", "crowded", "low_life" }, "Anywhere; it needs the Bomb Bay.",
                    "A warden's last mercy.", "Bomb Bay recharges 20% faster", new[] { M(ShmupStats.BombRecharge, StatOp.PercentAdd, 0.2f) },
                    "Turns bombs into insurance: you can take risks while a bomb is stocked. Only offered with the Bomb Bay."),
                Epic(Up("ghost_step", "Ghost Step", Rarity.Epic, 1, "While you sprint, enemy bullets pass through you. Sprinting drains 25% faster.", "GS", new Color(0.75f, 0.85f, 1f),
                        new[] { M(ShmupStats.StaminaMax, StatOp.Multiply, 0.75f) }, Tags(Move, Defense), null, ShmupEffects.GhostStep),
                    UpgradeVerb.Dodge, new[] { "boss", "crowded" }, "Anywhere: dash through a wall instead of around it.",
                    "A spark too quick to burn.", "+20% stamina", new[] { M(ShmupStats.StaminaMax, StatOp.PercentAdd, 0.2f) },
                    "Changes the dodge: sprint becomes a dash through bullets, so the per-room sprint budget becomes your shield. Balance report: the biggest cut in hits taken (about 75%). Bodies still hurt."),
                Epic(Up("ember_hunger", "Ember Hunger", Rarity.Epic, 1, "Grazing a bullet heats your guns: up to +60% damage, cooling over a few seconds.", "EH", new Color(1f, 0.45f, 0.3f),
                        null, Tags(Risk, Gun), null, ShmupEffects.GrazePower),
                    UpgradeVerb.Risk, new[] { "boss", "crowded", "long_fights" }, "Wherever the bullets are thickest: it pays you to stay close.",
                    "A spark that feeds on near misses.", "+15% graze ring", new[] { M(ShmupStats.GrazeRadius, StatOp.PercentAdd, 0.15f) },
                    "Changes risk: hugging bullets is rewarded with damage. Balance report: about x1.1 DPS for a bot that grazes on purpose, and more hits taken: the trade is the point."),
                Epic(Up("time_dilation", "Time Dilation", Rarity.Legendary, 1, "Enemy bullets close to you move at half speed.", "TD", new Color(0.6f, 0.9f, 1f),
                        null, Tags(Defense), null, ShmupEffects.FocusSlow),
                    UpgradeVerb.Dodge, new[] { "boss", "crowded", "turrets" }, "Anywhere: a bubble around the ship.",
                    "The forge's heat haze, where time runs thick.", "+10% move speed", new[] { M(ShmupStats.MoveSpeed, StatOp.PercentAdd, 0.1f) },
                    "Changes dodging: micro-dodges get easier in dense patterns. It used to slow bullets only while sprinting, which the balance report found did nothing measurable; the bubble cuts hits by about a third."),
                Epic(Up("red_line", "Red Line", Rarity.Legendary, 1, "On your last life: +50% damage and 30% faster fire.", "RL", new Color(1f, 0.3f, 0.3f),
                        null, Tags(Risk, Gun), null, ShmupEffects.LastStand),
                    UpgradeVerb.Risk, new[] { "boss", "low_life", "long_fights" }, "Anywhere, on your last life.",
                    "A star burning hottest just before it goes out.", "+10% damage", new[] { M(ShmupStats.Damage, StatOp.PercentAdd, 0.1f) },
                    "Replaces Overdrive (only numbers, so it failed the rubric). A risk verb: playing on one life on purpose, which pairs with the Altar's life offering. Balance report: about x1.5 DPS on the last life."),
            };
            var fallback = Up("pocket_change", "Pocket Change", Rarity.Common, 0, "+15 coins.", "$", new Color(1f, 0.83f, 0.25f),
                null, Tags(Economy), null, ShmupEffects.Coins + ":15");

            var companion = Asset<CompanionDef>(CompanionPath, 2, cd =>
            {
                cd.displayName = "Companion";
                cd.sprite = art.star;
                cd.color = new Color(1f, 0.85f, 0.55f);
                cd.size = 0.42f;
                cd.shotStyle = PlayerHoming;
                cd.rules = new CompanionRules();
            });

            // ---------- Config ----------
            var config = Asset<BulletHellConfig>(ConfigPath, 2, c =>
            {
                c.run = new RunConfig
                {
                    floors = 3,
                    roomsPerFloorMin = 5,
                    roomsPerFloorMax = 6,
                    // Encounters take some of the middle rooms; a run stays at ~2-3 fights per floor.
                    combatWeight = 40f,
                    eliteWeight = 11f,
                    treasureWeight = 6f,
                    shopWeight = 8f,
                    restWeight = 3f,
                    eventWeight = 8f,
                    forgeWeight = 6f,
                    altarWeight = 5f,
                    wagerWeight = 4f,
                    trialWeight = 6f,
                    traderWeight = 4f,
                    noBackToBackElites = true,
                    restBeforeBoss = true,
                    branching = true,
                    branchChance = 0.55f,
                };
                c.rewards = new ShmupRewards();
                c.boosts = new PowerBoostRules();
                c.forge = new ForgeRules();
                c.altar = new AltarRules();
                c.trader = new TraderRules();
                c.remnants = new RemnantRules();
                c.trials = Trials();
                c.wagers = Wagers();
                c.companionEnabled = true;
                c.companion = companion;
                c.roomColors = BulletHellConfig.DefaultRoomColors();
                c.floors = new List<FloorDef>
                {
                    new FloorDef
                    {
                        name = "The Outer Ring", background = new Color(0.04f, 0.08f, 0.12f), accent = new Color(0.35f, 0.9f, 0.85f),
                        combatRooms = floor1Battle, eliteRooms = floor1Elite, boss = gatekeeper, hpScale = 1f, coinScale = 1f,
                    },
                    new FloorDef
                    {
                        name = "The Furnace", background = new Color(0.12f, 0.06f, 0.05f), accent = new Color(1f, 0.6f, 0.3f),
                        combatRooms = floor2Battle, eliteRooms = floor2Elite, boss = forgemother, hpScale = 1.35f, coinScale = 1.2f,
                    },
                    new FloorDef
                    {
                        name = "The Core", background = new Color(0.08f, 0.05f, 0.13f), accent = new Color(0.8f, 0.55f, 1f),
                        combatRooms = floor3Battle, eliteRooms = floor3Elite, boss = lastLight, hpScale = 1.75f, coinScale = 1.4f,
                    },
                };
                c.player = player;
                c.upgrades = ups;
                c.fallbackUpgrade = fallback;
                c.economy = new ShmupEconomy();
                c.events = AssetDatabase.LoadAssetAtPath<InkFile>(BulletHellBuilder.Root + "/Dialogue/BulletHellEvents.ink");
                c.eventKnots = new List<string> { "shrine", "gambler", "medic", "cache" };
                c.playfieldHalfSize = new Vector2(6f, 5.3f);
                c.bulletStyles = Styles();
                c.coinColor = new Color(1f, 0.83f, 0.25f);
                c.sideColor = new Color(0.03f, 0.03f, 0.06f);
                c.roomIntroSeconds = 1.4f;
                c.roomClearedSeconds = 1.6f;
                c.waveGapSeconds = 0.6f;
                c.bossMusic = "music_boss";
            });
            if (config.events == null)
            {
                config.events = AssetDatabase.LoadAssetAtPath<InkFile>(BulletHellBuilder.Root + "/Dialogue/BulletHellEvents.ink");
                EditorUtility.SetDirty(config);
            }
            if (config.contentRevision != ContentRevision)
            {
                config.contentRevision = ContentRevision;
                EditorUtility.SetDirty(config);
            }
            return config;
        }

        /// <summary>The Trial board's conditions (it offers up to 3 the player can take).</summary>
        public static List<TrialDef> Trials() => new List<TrialDef>
        {
            new TrialDef { id = "untouched", displayName = "Untouched", description = "Clear the next fight without being hit.", kind = TrialKind.NoHit, rooms = 1 },
            new TrialDef { id = "grounded", displayName = "Feet on the Ground", description = "Clear the next fight without sprinting.", kind = TrialKind.NoSprint, rooms = 1 },
            new TrialDef { id = "swift", displayName = "Swift Hand", description = "Clear the next fight within 45 seconds.", kind = TrialKind.Timer, amount = 45f, rooms = 1 },
            new TrialDef { id = "arsenal", displayName = "Arsenal", description = "Own 3 gun upgrades (stacks count) by the end of the second fight from now.", kind = TrialKind.OwnTagged, amount = 3f, tag = "gun", rooms = 2 },
            new TrialDef { id = "needle", displayName = "Thread the Needle", description = "Graze 100 bullets in the next fight.", kind = TrialKind.Graze, amount = 100f, rooms = 1 },
            new TrialDef { id = "steady_nerves", displayName = "Steady Nerves", description = "Don't bomb in the next two fights.", kind = TrialKind.NoBomb, rooms = 2, requires = ShmupEffects.BombBay },
        };

        /// <summary>The Wager room's deals (it offers up to 2).</summary>
        public static List<WagerDef> Wagers() => new List<WagerDef>
        {
            new WagerDef
            {
                id = "blood_price", displayName = "Blood Price", stake = WagerStake.Life, amount = 1,
                description = "Lose a life now. Clear the next fight without being hit: choose an epic.",
                condition = new TrialDef { id = "blood_price", displayName = "Blood Price: no hits", description = "Clear the next fight without being hit.", kind = TrialKind.NoHit, rooms = 1 },
            },
            new WagerDef
            {
                id = "leaden_wings", displayName = "Leaden Wings", stake = WagerStake.Curse, curse = new StatModifier(ShmupStats.MoveSpeed, StatOp.PercentAdd, -0.2f),
                description = "Move 20% slower until the next fight ends. Clear it within 50 seconds: choose an epic.",
                condition = new TrialDef { id = "leaden_wings", displayName = "Leaden Wings: 50 s", description = "Clear the next fight within 50 seconds.", kind = TrialKind.Timer, amount = 50f, rooms = 1 },
            },
            new WagerDef
            {
                id = "gilded_nerve", displayName = "Gilded Nerve", stake = WagerStake.Coins, amount = 30,
                description = "Pay 30 coins. Graze 80 bullets in the next fight: choose an epic.",
                condition = new TrialDef { id = "gilded_nerve", displayName = "Gilded Nerve: 80 grazes", description = "Graze 80 bullets in the next fight.", kind = TrialKind.Graze, amount = 80f, rooms = 1 },
            },
        };

        public static List<BulletStyle> Styles() => new List<BulletStyle>
        {
            Style("Red pellet", new Color(1f, 0.33f, 0.43f), 0.075f, 0.24f),
            Style("Blue pellet", new Color(0.36f, 0.66f, 1f), 0.075f, 0.24f),
            Style("Pink orb", new Color(1f, 0.45f, 0.85f), 0.13f, 0.4f),
            Style("Orange ball", new Color(1f, 0.62f, 0.25f), 0.22f, 0.62f),
            Style("Green rice", new Color(0.45f, 1f, 0.55f), 0.065f, 0.2f, 2.2f),
            Style("Yellow pellet", new Color(1f, 0.88f, 0.35f), 0.075f, 0.24f),
            Style("Player shot", new Color(0.55f, 0.95f, 1f, 0.7f), 0.12f, 0.2f, 3f),
            Style("Player homing shot", new Color(0.8f, 0.6f, 1f, 0.8f), 0.12f, 0.22f, 2.2f),
            Style("Violet ball", new Color(0.7f, 0.45f, 1f), 0.2f, 0.56f),
            Style("White needle", new Color(0.92f, 0.95f, 1f), 0.06f, 0.16f, 3f),
        };

        static BulletStyle Style(string name, Color c, float hit, float size, float stretch = 1f) =>
            new BulletStyle { name = name, color = c, hitRadius = hit, size = size, stretch = stretch };

        // ---------- Helpers ----------

        static T Asset<T>(string path, Action<T> fill) where T : ScriptableObject => Asset(path, 1, fill);

        /// <summary>Loads or creates the asset; fills it when created, on a reset, or once when the project's
        /// content revision is older than <paramref name="revision"/> (the revision that last changed it).</summary>
        static T Asset<T>(string path, int revision, Action<T> fill) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            bool created = a == null;
            if (created)
            {
                a = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(a, path);
            }
            if (created || _reset || _fromRevision < revision)
            {
                fill(a);
                EditorUtility.SetDirty(a);
            }
            return a;
        }

        static string[] Tags(params string[] t) => t;

        static string[] Needs(params string[] ids) => ids;

        /// <summary>Fills an epic's rubric, its forge bonus and design notes (part of the same asset fill).</summary>
        static UpgradeAsset Epic(UpgradeAsset u, UpgradeVerb verb, string[] situations, string where, string idea, string forgeText,
            StatModifier[] forge, string notes)
        {
            if (!(_reset || _fromRevision < 2 || u.rubric == null || u.rubric.verb == UpgradeVerb.None)) return u;
            u.rubric = new EpicRubric { verb = verb, situations = new List<string>(situations), wherePlayerIs = where, idea = idea };
            u.forgeText = forgeText;
            u.forgeModifiers = new List<StatModifier>(forge);
            u.designNotes = notes;
            EditorUtility.SetDirty(u);
            return u;
        }

        static PatternDef P(PatternDef d, int style, Action<PatternDef> tweak = null)
        {
            d.styleIndex = style;
            tweak?.Invoke(d);
            return d;
        }

        static PatternSequence Seq(bool loop, float loopDelay, params (float time, PatternDef pattern)[] steps)
        {
            var s = new PatternSequence { loop = loop, loopDelay = loopDelay };
            foreach (var (time, pattern) in steps) s.Add(time, pattern);
            return s;
        }

        static EnemyDef Enemy(string file, Sprite sprite, Color color, float hp, float size, float radius, int score, int coins,
            JamKit.BulletHell.EnemyMoveDef move, PatternSequence attack, float spin = 0f, bool big = false, string display = null)
        {
            return Asset<EnemyDef>(DataDir + "/Enemies/" + file + ".asset", e =>
            {
                e.displayName = display ?? file;
                e.sprite = sprite;
                e.color = color;
                e.hp = hp;
                e.size = size;
                e.radius = radius;
                e.score = score;
                e.coins = coins;
                e.movement = move;
                e.attack = attack;
                e.attackDelay = 0.4f;
                e.spin = spin;
                e.big = big;
                e.hurtsOnContact = true;
            });
        }

        static WaveSpawn S(float time, EnemyDef enemy, float x, float y, EnterSide from = EnterSide.Top, int count = 1,
            float dx = 1.5f, float dy = 0f, float interval = 0.25f, bool mirror = false) => new WaveSpawn
        {
            time = time,
            enemy = enemy,
            position = new Vector2(x, y),
            from = from,
            count = count,
            spacing = new Vector2(dx, dy),
            interval = interval,
            mirror = mirror,
        };

        static WaveDef Wave(string file, float maxDuration, params WaveSpawn[] spawns) =>
            Asset<WaveDef>(DataDir + "/Waves/" + file + ".asset", w =>
            {
                w.maxDuration = maxDuration;
                w.spawns = new List<WaveSpawn>(spawns);
            });

        static RoomDef Room(string file, RoomType type, int bonus, params WaveDef[] waves) =>
            Asset<RoomDef>(DataDir + "/Rooms/" + file + ".asset", r =>
            {
                r.type = type;
                r.bonusCoins = bonus;
                r.waves = new List<WaveDef>(waves);
            });

        static BossPhase Phase(string name, float startsAt, BossMoveKind move, float width, float speed, Color tint, PatternSequence attack) =>
            new BossPhase { name = name, startsAtHealth = startsAt, movement = move, moveWidth = width, moveSpeed = speed, tint = tint, attack = attack };

        static StatModifier M(string stat, StatOp op, float value) => new StatModifier(stat, op, value);

        static UpgradeAsset Up(string id, string name, Rarity rarity, int stacks, string description, string iconText, Color iconColor,
            StatModifier[] mods, string[] tags, string[] requires = null, params string[] effects)
        {
            return Asset<UpgradeAsset>(DataDir + "/Upgrades/" + id + ".asset", 2, u =>
            {
                u.def = new UpgradeDef(id, rarity, stacks)
                {
                    displayName = name,
                    description = description,
                };
                if (mods != null) u.def.modifiers.AddRange(mods);
                if (effects != null) u.def.effects.AddRange(effects);
                if (tags != null) u.def.tags.AddRange(tags);
                if (requires != null) u.def.requires.AddRange(requires);
                u.iconText = iconText;
                u.iconColor = iconColor;
                u.forgeModifiers = new List<StatModifier>();
                u.forgeEffects = new List<string>();
                u.forgeText = "";
                u.rubric = new EpicRubric();
                u.designNotes = "";
            });
        }
    }
}

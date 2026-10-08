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
    /// 13 waves, 14 rooms over 3 floors, 3 bosses (8 phases), 18 upgrades + a fallback, the player,
    /// the palette and the config. Normal rebuilds only create what's missing (team edits survive);
    /// "Reset Content to Defaults" rewrites every asset below to these values.
    /// </summary>
    public static class BulletHellContent
    {
        public const string DataDir = BulletHellBuilder.Root + "/Data";
        public const string ConfigPath = DataDir + "/BulletHellConfig.asset";

        // Bullet style indices (see Styles()).
        const int Red = 0, Blue = 1, Pink = 2, Orange = 3, Rice = 4, Yellow = 5, PlayerShot = 6, PlayerHoming = 7, Violet = 8, Needle = 9;

        static bool _reset;

        public static BulletHellConfig EnsureAll(BulletHellArt.Set art, bool reset)
        {
            _reset = reset;
            foreach (var d in new[] { "Enemies", "Waves", "Rooms", "Bosses", "Upgrades" })
                BulletHellBuilder.EnsureFolder(DataDir + "/" + d);

            var player = Asset<PlayerDef>(DataDir + "/Player.asset", p =>
            {
                p.sprite = art.ship;
                p.color = new Color(0.55f, 0.95f, 1f);
                p.size = 0.75f;
                p.moveSpeed = 7.5f;
                p.focusSpeed = 3.2f;
                p.hitboxRadius = 0.07f;
                p.grazeRadius = 0.6f;
                p.lives = 3;
                p.bombsPerLife = 2;
                p.maxBombs = 5;
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
            var ups = new List<UpgradeAsset>
            {
                Up("sharp_rounds", "Sharp Rounds", Rarity.Common, 3, "Shots deal 20% more damage.", "Dm", new Color(1f, 0.45f, 0.45f),
                    new[] { M(ShmupStats.Damage, StatOp.PercentAdd, 0.2f) }),
                Up("rapid_trigger", "Rapid Trigger", Rarity.Common, 3, "Fire 15% faster.", "Rt", new Color(1f, 0.75f, 0.35f),
                    new[] { M(ShmupStats.FireRate, StatOp.PercentAdd, 0.15f) }),
                Up("light_frame", "Light Frame", Rarity.Common, 2, "Move 10% faster (focused too).", "Sp", new Color(0.5f, 1f, 0.8f),
                    new[] { M(ShmupStats.MoveSpeed, StatOp.PercentAdd, 0.1f) }),
                Up("wide_halo", "Wide Halo", Rarity.Common, 2, "A bigger graze ring: more points from near misses.", "Gz", new Color(0.7f, 0.9f, 1f),
                    new[] { M(ShmupStats.GrazeRadius, StatOp.PercentAdd, 0.35f) }),
                Up("coin_magnet", "Coin Magnet", Rarity.Common, 2, "Coins fly to you from further away, and enemies drop 25% more.", "Mg", new Color(1f, 0.85f, 0.3f),
                    new[] { M(ShmupStats.Magnet, StatOp.PercentAdd, 1f), M(ShmupStats.CoinGain, StatOp.PercentAdd, 0.25f) }),
                Up("hot_barrels", "Hot Barrels", Rarity.Common, 2, "Shots fly 25% faster and hit a little harder.", "Hb", new Color(1f, 0.6f, 0.4f),
                    new[] { M(ShmupStats.ShotSpeed, StatOp.PercentAdd, 0.25f), M(ShmupStats.Damage, StatOp.PercentAdd, 0.05f) }),
                Up("spare_bomb", "Spare Bomb", Rarity.Common, 2, "+1 bomb now, and room for one more.", "Bm", new Color(1f, 0.85f, 0.4f),
                    null, ShmupEffects.BombPlus),
                Up("twin_spread", "Twin Spread", Rarity.Rare, 2, "Two more streams fan out to the sides.", "Sd", new Color(0.4f, 0.75f, 1f),
                    null, ShmupEffects.Spread),
                Up("piercing_shot", "Piercing Shot", Rarity.Rare, 2, "Shots pass through one more enemy.", "Pc", new Color(0.6f, 0.8f, 1f),
                    null, ShmupEffects.Pierce),
                Up("big_bang", "Big Bang", Rarity.Rare, 2, "Bombs are 50% bigger and stronger.", "BB", new Color(1f, 0.7f, 0.3f),
                    null, ShmupEffects.BiggerBombs),
                Up("lucky_charm", "Lucky Charm", Rarity.Rare, 2, "Rarer upgrades show up more often.", "Lk", new Color(0.5f, 1f, 0.5f),
                    new[] { M(ShmupStats.Luck, StatOp.Flat, 0.6f) }),
                Up("close_shave", "Close Shave", Rarity.Rare, 1, "Every 120 grazes restore half a life (or give a bomb when you're full).", "CS", new Color(0.5f, 1f, 0.7f),
                    null, ShmupEffects.GrazeHeal),
                Up("seeker_rounds", "Seeker Rounds", Rarity.Epic, 1, "Your side streams curve toward enemies.", "Sk", new Color(0.8f, 0.6f, 1f),
                    null, ShmupEffects.Homing),
                Up("rear_guard", "Rear Guard", Rarity.Epic, 1, "Two streams fire backwards too.", "RG", new Color(0.7f, 0.7f, 1f),
                    null, ShmupEffects.RearShot),
                Up("extra_life", "Extra Life", Rarity.Epic, 2, "+1 life.", "1U", new Color(1f, 0.45f, 0.55f),
                    null, ShmupEffects.LifeUp),
                Up("guardian_angel", "Guardian Angel", Rarity.Epic, 1, "When you're hit, a bomb goes off instead of losing a life (if you have a bomb).", "GA", new Color(1f, 0.95f, 0.7f),
                    null, ShmupEffects.AutoBomb),
                Up("overdrive", "Overdrive", Rarity.Legendary, 1, "40% more damage and 25% faster fire.", "OD", new Color(1f, 0.4f, 0.3f),
                    new[] { M(ShmupStats.Damage, StatOp.Multiply, 1.4f), M(ShmupStats.FireRate, StatOp.PercentAdd, 0.25f) }),
                Up("time_dilation", "Time Dilation", Rarity.Legendary, 1, "While focusing, enemy bullets slow to 60% speed.", "TD", new Color(0.6f, 0.9f, 1f),
                    null, ShmupEffects.FocusSlow),
            };
            var fallback = Up("pocket_change", "Pocket Change", Rarity.Common, 0, "+15 coins.", "$", new Color(1f, 0.83f, 0.25f),
                null, ShmupEffects.Coins + ":15");

            // ---------- Config ----------
            var config = Asset<BulletHellConfig>(ConfigPath, c =>
            {
                c.run = new RunConfig
                {
                    floors = 3,
                    roomsPerFloorMin = 5,
                    roomsPerFloorMax = 6,
                    combatWeight = 46f,
                    eliteWeight = 12f,
                    treasureWeight = 12f,
                    shopWeight = 10f,
                    restWeight = 4f,
                    eventWeight = 12f,
                    noBackToBackElites = true,
                    restBeforeBoss = true,
                    branching = true,
                    branchChance = 0.55f,
                };
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
            return config;
        }

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

        static T Asset<T>(string path, Action<T> fill) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            bool created = a == null;
            if (created)
            {
                a = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(a, path);
            }
            if (created || _reset)
            {
                fill(a);
                EditorUtility.SetDirty(a);
            }
            return a;
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
            StatModifier[] mods, params string[] effects)
        {
            return Asset<UpgradeAsset>(DataDir + "/Upgrades/" + id + ".asset", u =>
            {
                u.def = new UpgradeDef(id, rarity, stacks)
                {
                    displayName = name,
                    description = description,
                };
                if (mods != null) u.def.modifiers.AddRange(mods);
                if (effects != null) u.def.effects.AddRange(effects);
                u.iconText = iconText;
                u.iconColor = iconColor;
            });
        }
    }
}

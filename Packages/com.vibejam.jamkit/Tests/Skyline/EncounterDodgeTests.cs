using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using JamKit.Movement;
using JamKit.Skyline;
using JamKit.Tests.Movement;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>The dodging measurement on Normal with the arena's real encounters (docs/SKYLINE-DESIGN.md 2.6, 11.2). Since Task 11b this
    /// immortal-shooter harness covers the mechanics only: the balance checks (the dodge band included) run in FightSim with
    /// the player shooting back (FightTests; the creator, 2026-10-13).</summary>
    public class EncounterDodgeTests
    {
        /// <summary>Flat ground around the player for the two lane bots (the fight is relative to the encounter's anchor, so
        /// the lanes, which run along Z about the anchor, need ground on both sides of it).</summary>
        static BoxWorld Hall()
        {
            var w = new BoxWorld();
            w.Floor(GalleriaArena.HallMinX, GalleriaArena.HallMaxX, GalleriaArena.HallMinZ, GalleriaArena.HallMaxZ, 0f);
            return w;
        }

        /// <summary>The real main hall, in the fight's frame (translated by minus the anchor), for the scripted routes.</summary>
        static Func<IMoveWorld> RealHall(EncounterDef e) => () =>
        {
            var w = new BoxWorld();
            w.Floor(GalleriaArena.HallMinX - e.anchor.X, GalleriaArena.HallMaxX - e.anchor.X,
                GalleriaArena.HallMinZ - e.anchor.Z, GalleriaArena.HallMaxZ - e.anchor.Z, 0f);
            return w;
        };

        static EncounterDef ById(string id) => Array.Find(GalleriaArena.Encounters(), e => e.id == id);

        [Test]
        public void OnlyAimedShotsAreMeasured()
        {
            var food = EncounterDodge.Shooters(ById("food-court"), SkylineEnemies.Find, Difficulties.Normal());
            Assert.That(food.Count, Is.EqualTo(12), "the twelve drones; the floor-care units' sweeps are dodged by jumping");
            Assert.That(food[0].leadFactor, Is.EqualTo(Difficulties.Normal().leadFactor));
            Assert.That(food[0].aimSpreadDeg, Is.EqualTo(SkylineEnemies.MallCopDrone().aimSpreadDeg));
            Assert.That(food[1].firstShotAt, Is.GreaterThan(food[0].firstShotAt), "staggered, not in lockstep");
            var sky = EncounterDodge.Shooters(ById("skylight"), SkylineEnemies.Find, Difficulties.Normal());
            Assert.That(sky.Count, Is.EqualTo(3), "the three spotlights");
            Assert.That(sky[0].projectileSpeed, Is.EqualTo(0f));
            Assert.That(sky[0].leadFactor, Is.EqualTo(0f));
            Assert.That(sky[0].beamTelegraph, Is.EqualTo(SkylineEnemies.Spotlight().telegraphSeconds));
            Assert.That(sky[0].beamFollowRate, Is.EqualTo(SkylineEnemies.Spotlight().beamFollowRate));
        }

        [TestCase("food-court")]
        [TestCase("skylight")]
        public void ShootersStandAtTheirSpawnMinusTheAnchor(string id)
        {
            var e = ById(id);
            Assert.That(e.anchor, Is.Not.EqualTo(Vector3.Zero), "the arena's encounters are anchored at the hall's entry");
            var shooters = EncounterDodge.Shooters(e, SkylineEnemies.Find, Difficulties.Normal());
            var aimed = new List<Vector3>();
            foreach (var s in e.spawns)
            {
                var d = SkylineEnemies.Find(s.enemy);
                if (d.aim == AimModel.LeadsYou || d.aim == AimModel.FiresAtWhereYouAre) aimed.Add(s.position);
            }
            Assert.That(shooters.Count, Is.EqualTo(aimed.Count));
            for (int i = 0; i < shooters.Count; i++)
            {
                Vector3 want = aimed[i] - e.anchor;
                Assert.That(Vector3.Distance(shooters[i].position, want), Is.LessThan(1e-4f),
                    $"{id} shooter {i}: placed at {shooters[i].position}, expected spawn - anchor = {want}");
            }
        }

        [Test]
        public void AimErrorIsSeededSoTheReportRepeats()
        {
            var a = EncounterDodge.Run(ById("food-court"), Hall, SkylineEnemies.Find, Difficulties.Normal(), 20f, 3, 2);
            var b = EncounterDodge.Run(ById("food-court"), Hall, SkylineEnemies.Find, Difficulties.Normal(), 20f, 3, 2);
            Assert.That(a.straightHitsMean, Is.EqualTo(b.straightHitsMean));
            Assert.That(a.jinkHitsMean, Is.EqualTo(b.jinkHitsMean));
        }

        [Test]
        public void InTheHarnessTheSpotlightsBeamHitsTheStraightRunnerMoreThanTheJinker()
        {
            // mechanics only since Task 11b's fix round: the band is measured in FightSim with the player shooting back (FightTests);
            // this immortal-shooter harness only shows that a beam punishes straight lines and hits both runners
            var r = EncounterDodge.Run(ById("skylight"), Hall, Difficulties.Normal());
            TestContext.WriteLine($"Old harness (mechanics only), skylight: straight-line {r.straightHitsMean:0.0} hits, jinking {r.jinkHitsMean:0.0} " +
                $"(means of {r.seeds} seeds), {r.shots} shots in {r.seconds:0} s, ratio {r.Ratio:0.00}");
            Assert.That(r.straightHitsMean, Is.GreaterThan(0f), "the straight-line bot is hit");
            Assert.That(r.jinkHitsMean, Is.GreaterThan(0f), "and so is the jinking bot");
            Assert.That(r.straightHitsMean, Is.GreaterThan(r.jinkHitsMean), "changing direction beats a straight line");
        }

        [Test]
        public void InTheHarnessTheLaggedDronesMissTheRunnerAcrossTheirFireAndHitTheJinkerWhoSlowsToTurn()
        {
            // the creator's call of 2026-10-14 reverses what this test used to say (leading shots hit the straight runner more):
            // the drones aim at where you were 0.2 s ago, so the lane runner crossing their line of fire is missed, and the jinker,
            // who stops and turns, is the one they hit
            var r = EncounterDodge.Run(ById("food-court"), Hall, Difficulties.Normal());
            TestContext.WriteLine($"Old harness (mechanics only), food-court: straight-line {r.straightHitsMean:0.0} hits, jinking {r.jinkHitsMean:0.0} " +
                $"(means of {r.seeds} seeds), {r.shots} shots in {r.seconds:0} s");
            Assert.That(r.jinkHitsMean, Is.GreaterThan(0f), "the jinking bot is hit");
            Assert.That(r.straightHitsMean, Is.LessThan(r.jinkHitsMean * 0.25f), "running across their fire beats stopping to turn");
        }

        // ---- The clean line (plan amendment 6): a scripted route a skilled player could take, with no hits on Normal.

        [Test]
        public void TheScriptedRouteBotWalksItsWaypointsInOrderAndLoops()
        {
            var world = new BoxWorld();
            world.Floor(-30f, 30f, -30f, 30f, 0f);
            var sim = new MoveSim(world, new MoveDef());
            sim.Teleport(Vector3.Zero, 0f, Vector3.Zero);
            var bot = new ScriptedRouteBot(new[] { new Vector2(0f, 12f), new Vector2(12f, 12f), new Vector2(12f, 0f), new Vector2(0f, 0f) });
            float h = 1f / sim.def.tickRate, t = 0f;
            var visited = new List<int>();
            for (int i = 0; i < 20 * 120; i++)
            {
                var input = bot.Next(sim, t, h);
                sim.Step(in input, h);
                t += h;
                if (visited.Count == 0 || visited[visited.Count - 1] != bot.TargetIndex) visited.Add(bot.TargetIndex);
            }
            Assert.That(visited.Count, Is.GreaterThanOrEqualTo(5), "it reached waypoints and went on, looping");
            for (int i = 1; i < visited.Count; i++)
                Assert.That(visited[i], Is.EqualTo((visited[i - 1] + 1) % 4), "in order");
        }

        [TestCase("food-court")]
        [TestCase("skylight")]
        public void TheCleanLineStaysInsideTheHall(string id)
        {
            var e = ById(id);
            var route = EncounterDodge.CleanLine(e);
            Assert.That(route, Is.Not.Null.And.Not.Empty);
            foreach (var p in route)
            {
                float x = p.X + e.anchor.X, z = p.Y + e.anchor.Z;
                Assert.That(x, Is.InRange(GalleriaArena.HallMinX + 1f, GalleriaArena.HallMaxX - 1f), $"{id}: waypoint {p}");
                Assert.That(z, Is.InRange(GalleriaArena.HallMinZ + 1f, GalleriaArena.HallMaxZ - 1f), $"{id}: waypoint {p}");
            }
        }

        // Task 11's zero-hit sprint lane was a balance claim in this immortal-shooter harness; since Task 11b's fix round the
        // clean line is the human-timed skilled line in FightTests, so this only checks the route harness's mechanics.
        [TestCase("food-court")]
        [TestCase("skylight")]
        public void TheCleanLineHarnessRunsItsFiveSeedsAndRepeats(string id)
        {
            var e = ById(id);
            int[] a = EncounterDodge.CleanLineHits(e, RealHall(e), SkylineEnemies.Find, Difficulties.Normal(), 20f);
            int[] b = EncounterDodge.CleanLineHits(e, RealHall(e), SkylineEnemies.Find, Difficulties.Normal(), 20f);
            TestContext.WriteLine($"Clean-line route harness (mechanics only), {id}: hits per seed {string.Join(", ", a)}");
            Assert.That(a.Length, Is.EqualTo(5), "the harness's five seeds");
            Assert.That(b, Is.EqualTo(a), "seeded: the same hits again");
            int total = 0;
            foreach (int h in a) total += h;
            Assert.That(total, Is.GreaterThan(0), "the shooters fire at the route (the harness is live)");
        }

        [Test, Explicit("the Normal tuning sweep: prints a table of ratios; asserts nothing")]
        public void SweepTheNormalTuning()
        {
            var sb = new StringBuilder();
            sb.AppendLine("food-court (two drones): aimSpreadDeg leadFactor fireInterval -> straight jink ratio");
            foreach (float spread in new[] { 2f, 3f, 4f })
                foreach (float lead in new[] { 0.65f, 0.7f, 0.75f, 0.8f })
                    foreach (float fi in new[] { 1.0f, 1.2f, 1.4f })
                    {
                        Func<string, EnemyDef> lookup = id =>
                        {
                            var d = SkylineEnemies.Find(id);
                            if (d != null && id == SkylineEnemies.DroneId) { d.aimSpreadDeg = spread; d.fireInterval = fi; }
                            return d;
                        };
                        var r = EncounterDodge.Run(ById("food-court"), Hall, lookup, new DifficultyDef { leadFactor = lead });
                        sb.AppendLine($"{spread:0} {lead:0.00} {fi:0.0} -> {r.straightHitsMean:0.0} {r.jinkHitsMean:0.0} {r.Ratio:0.00}");
                    }
            sb.AppendLine("skylight (spotlight): beamFollowRate beamHitRadius fireInterval -> straight jink ratio");
            foreach (float w in new[] { 8f, 10f, 12f })
                foreach (float hr in new[] { 0.7f, 0.8f, 0.9f, 1.0f })
                    foreach (float fi in new[] { 2.4f, 2.8f, 3.2f })
                    {
                        Func<string, EnemyDef> lookup = id =>
                        {
                            var d = SkylineEnemies.Find(id);
                            if (d != null && id == SkylineEnemies.SpotlightId) { d.beamFollowRate = w; d.beamHitRadius = hr; d.fireInterval = fi; }
                            return d;
                        };
                        var r = EncounterDodge.Run(ById("skylight"), Hall, lookup, Difficulties.Normal());
                        sb.AppendLine($"{w:0} {hr:0.0} {fi:0.0} -> {r.straightHitsMean:0.0} {r.jinkHitsMean:0.0} {r.Ratio:0.00}");
                    }
            TestContext.WriteLine(sb.ToString());
        }
    }
}

using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Movement;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>The idle work before waking (fix round "idle life", 2026-10-14): the floor-care unit cleans round its post on a
    /// 1.5 m leash (short runs, gentle turns, a bump-and-turn at cover), the gondola washes a short stretch of its rail; both
    /// are seeded and deterministic, and FightSim runs them until the bot wakes, then the bot goes on from there.</summary>
    public class IdleWorkTests
    {
        const float H = FightSim.Step;
        static readonly Vector3 Post = new Vector3(-9f, 0f, -7f);

        static float Flat(Vector3 v) => MathF.Sqrt(v.X * v.X + v.Z * v.Z);

        static List<Vector3> Clean(ulong seed, float seconds, Func<Vector3, Vector3, float, bool> blocked = null, FloorCareIdle c = null)
        {
            c = c ?? new FloorCareIdle(SkylineEnemies.FloorCareUnit(), seed);
            var path = new List<Vector3>();
            for (int i = 0; i < (int)(seconds / H); i++)
            {
                c.Tick(H, Post, blocked);
                path.Add(c.Offset);
            }
            return path;
        }

        [Test]
        public void TheFloorCareUnitAndTheGondolaHaveIdleWorkInTheirDataAndTheOthersDont()
        {
            var fc = SkylineEnemies.FloorCareUnit();
            var g = SkylineEnemies.Gondola();
            Assert.That(FloorCareIdle.Works(fc), Is.True);
            Assert.That(fc.idleLeash, Is.InRange(1f, 2f), "a small leash, about 1.5 m");
            Assert.That(fc.idleSpeed, Is.LessThan(fc.moveSpeed * 0.25f), "cleaning is slow next to its charge");
            Assert.That(GondolaIdle.Works(g), Is.True);
            Assert.That(g.idleSpeed, Is.LessThan(g.railSpeed * 0.5f), "washing is a slow ride next to its awake one");
            Assert.That(g.idleLeash, Is.InRange(1f, 3f), "a short stretch of the rail");
            foreach (var d in new[] { SkylineEnemies.MallCopDrone(), SkylineEnemies.Spotlight() })
                Assert.That(FloorCareIdle.Works(d) || GondolaIdle.Works(d), Is.False, d.id);
            Assert.That(GondolaIdle.Works(fc) || FloorCareIdle.Works(g), Is.False, "each bot its own work");
        }

        [TestCase(1UL)]
        [TestCase(2UL)]
        [TestCase(77UL)]
        public void TheFloorCareUnitCleansOnItsLeashInShortRunsAndGentleTurns(ulong seed)
        {
            var def = SkylineEnemies.FloorCareUnit();
            var c = new FloorCareIdle(def, seed);
            float travelled = 0f, maxOut = 0f, run = 0f, longestRun = 0f, turned = 0f, maxSpeed = 0f;
            Vector3 prev = c.Offset;
            float prevYaw = c.Yaw;
            int stillSeconds = 0;
            float movedThisSecond = 0f;
            for (int i = 1; i <= (int)(120f / H); i++)
            {
                c.Tick(H, Post, null);
                float step = Flat(c.Offset - prev);
                travelled += step;
                movedThisSecond += step;
                maxSpeed = Math.Max(maxSpeed, step / H);
                maxOut = Math.Max(maxOut, Flat(c.Offset));
                Assert.That(Flat(c.Offset), Is.LessThanOrEqualTo(def.idleLeash + 1e-4f), "never off its leash");
                Assert.That(c.Offset.Y, Is.EqualTo(0f), "on the floor");
                float dy = Math.Abs(((c.Yaw - prevYaw) % 360f + 540f) % 360f - 180f);
                turned += dy;
                Assert.That(dy / H, Is.LessThanOrEqualTo(FloorCareIdle.BumpTurnRate + 1f), "turns are gentle, never a snap");
                if (c.State == FloorCareIdle.Phase.Run && dy < 1e-3f) { run += step; longestRun = Math.Max(longestRun, run); }
                else run = 0f;
                if (i % 60 == 0)
                {
                    if (movedThisSecond < 0.01f) stillSeconds++;
                    movedThisSecond = 0f;
                }
                prev = c.Offset;
                prevYaw = c.Yaw;
            }
            TestContext.WriteLine($"seed {seed}: travelled {travelled:0.0} m in 120 s, furthest {maxOut:0.00} m, longest straight run {longestRun:0.00} m, turned {turned:0} deg, still seconds {stillSeconds}");
            Assert.That(travelled, Is.GreaterThan(0.6f * def.idleSpeed * 120f), "it keeps cleaning");
            Assert.That(maxSpeed, Is.LessThanOrEqualTo(def.idleSpeed + 1e-3f), "slowly");
            Assert.That(maxOut, Is.GreaterThan(0.6f * def.idleLeash), "it covers its patch, not one spot");
            Assert.That(longestRun, Is.LessThanOrEqualTo(FloorCareIdle.RunMax + 0.02f), "short straight runs");
            Assert.That(turned, Is.GreaterThan(720f), "and it turns");
            Assert.That(stillSeconds, Is.LessThanOrEqualTo(12), "never still for long (only its turns at the leash)");
        }

        [Test]
        public void ItBumpsAndTurnsAtCoverAndNeverEntersIt()
        {
            var def = SkylineEnemies.FloorCareUnit();
            // a box 0.9 m east of the post: inside its patch
            var box = new CoverBox { min = new Vector3(Post.X + 0.9f, 0f, Post.Z - 1f), max = new Vector3(Post.X + 1.5f, 1.2f, Post.Z + 1f) };
            var cover = new[] { box };
            Func<Vector3, Vector3, float, bool> blocked = (a, b, r) => FloorCareIdle.FloorBlocked(a, b, r, cover);
            int bumps = 0;
            foreach (ulong seed in new ulong[] { 1, 2, 3, 4, 5 })
            {
                var c = new FloorCareIdle(def, seed);
                bool sawBack = false;
                for (int i = 0; i < (int)(90f / H); i++)
                {
                    c.Tick(H, Post, blocked);
                    Vector3 p = Post + c.Offset;
                    Assert.That(p.X > box.min.X - def.bodyRadius && p.X < box.max.X + def.bodyRadius && p.Z > box.min.Z - def.bodyRadius
                        && p.Z < box.max.Z + def.bodyRadius, Is.False, $"seed {seed}: its body never enters the cover");
                    Assert.That(Flat(c.Offset), Is.LessThanOrEqualTo(def.idleLeash + 1e-4f));
                    sawBack |= c.State == FloorCareIdle.Phase.Back && c.Speed < 0f;
                }
                bumps += c.Bumps;
                if (c.Bumps > 0) Assert.That(sawBack, Is.True, $"seed {seed}: a bump backs it off a little");
            }
            TestContext.WriteLine($"bumps in 5 x 90 s next to a box: {bumps}");
            Assert.That(bumps, Is.GreaterThanOrEqualTo(5), "it meets the cover in its patch and turns away");
        }

        [Test]
        public void TheSameSeedCleansTheSameWayAndAnotherSeedDoesnt()
        {
            var a = Clean(9, 30f);
            var b = Clean(9, 30f);
            var c = Clean(10, 30f);
            float diff = 0f;
            for (int i = 0; i < a.Count; i++)
            {
                Assert.That(b[i], Is.EqualTo(a[i]), "deterministic");
                diff = Math.Max(diff, Flat(a[i] - c[i]));
            }
            Assert.That(diff, Is.GreaterThan(0.3f), "another seed takes another path");
        }

        [TestCase(1UL)]
        [TestCase(5UL)]
        public void TheGondolaWashesAShortStretchBackAndForthSlowly(ulong seed)
        {
            var def = SkylineEnemies.Gondola();
            var w = new GondolaIdle(def, 24f, seed);
            Assert.That(w.Stretch, Is.EqualTo(def.idleLeash));
            float maxAlong = 0f, minAfterFar = float.PositiveInfinity, maxSpeed = 0f, prev = 0f;
            int reversals = 0, lastSign = 0, pausedSteps = 0;
            bool reachedFar = false;
            for (int i = 0; i < (int)(60f / H); i++)
            {
                w.Tick(H);
                Assert.That(w.Along, Is.InRange(0f, w.Stretch), "on its stretch");
                maxAlong = Math.Max(maxAlong, w.Along);
                if (w.Along >= 0.7f * w.Stretch - 1e-4f) reachedFar = true;
                if (reachedFar) minAfterFar = Math.Min(minAfterFar, w.Along);
                maxSpeed = Math.Max(maxSpeed, Math.Abs(w.Along - prev) / H);
                int sign = Math.Sign(w.Along - prev);
                if (sign != 0 && lastSign != 0 && sign != lastSign) reversals++;
                if (sign != 0) lastSign = sign;
                if (w.Pausing) pausedSteps++;
                prev = w.Along;
            }
            TestContext.WriteLine($"seed {seed}: furthest {maxAlong:0.00} m of {w.Stretch:0.0}, back to {minAfterFar:0.00} m, {reversals} reversals in 60 s, paused {pausedSteps * H:0.0} s");
            Assert.That(reachedFar, Is.True, "it rides out along its stretch");
            Assert.That(minAfterFar, Is.LessThanOrEqualTo(0.2f * w.Stretch + 1e-4f), "and back toward its spawn");
            Assert.That(reversals, Is.GreaterThanOrEqualTo(6), "back and forth, again and again");
            Assert.That(maxSpeed, Is.LessThanOrEqualTo(def.idleSpeed + 1e-3f), "slowly");
            Assert.That(pausedSteps * H, Is.InRange(5f, 30f), "a stop at each end, but mostly riding");
        }

        [Test]
        public void TheGondolasStretchNeverRunsPastAShortRailAndItIsDeterministic()
        {
            var def = SkylineEnemies.Gondola();
            var w = new GondolaIdle(def, 1.2f, 3);
            for (int i = 0; i < 1200; i++)
            {
                w.Tick(H);
                Assert.That(w.Along, Is.InRange(0f, 1.2f));
            }
            var a = new GondolaIdle(def, 24f, 11);
            var b = new GondolaIdle(def, 24f, 11);
            var c = new GondolaIdle(def, 24f, 12);
            float diff = 0f;
            for (int i = 0; i < 1800; i++)
            {
                a.Tick(H); b.Tick(H); c.Tick(H);
                Assert.That(b.Along, Is.EqualTo(a.Along));
                diff = Math.Max(diff, Math.Abs(a.Along - c.Along));
            }
            Assert.That(diff, Is.GreaterThan(0.1f), "another seed washes on another beat");
            var none = new GondolaIdle(def, 0f, 3);
            none.Tick(1f);
            Assert.That(none.Along, Is.EqualTo(0f), "a rail that stays put: nothing to wash");
        }

        [Test]
        public void TheRailStartsFromWhereTheWashingLeftItAndItsMinesKeepTheirRailPoints()
        {
            var def = SkylineEnemies.Gondola();
            var rail = new GondolaRail(Vector3.Zero, new Vector3(20f, 0f, 0f), 4f);
            rail.StartAt(1.6f);
            Assert.That(rail.Position.X, Is.EqualTo(1.6f).Within(1e-4f));
            Assert.That(rail.Travelled, Is.EqualTo(1.6f).Within(1e-4f));
            rail.Tick(0.25f);
            Assert.That(rail.Position.X, Is.EqualTo(2.6f).Within(1e-4f), "on toward the rail's end");
            rail.StartAt(5f);
            Assert.That(rail.Position.X, Is.EqualTo(2.6f).Within(1e-4f), "the hand-over is once, before it rides");

            // the mines: woken 1.6 m along, it lays at the same rail points as one woken at its spawn
            var layers = new List<MineLayer>();
            foreach (float along in new[] { 0f, 1.6f })
            {
                var r = new GondolaRail(Vector3.Zero, new Vector3(100f, 0f, 0f), 4f);
                r.StartAt(along);
                var layer = new MineLayer(def);
                var blasts = new List<float>();
                for (int i = 0; i < 25; i++)
                {
                    r.Tick(0.1f);
                    layer.Tick(0.1f, r, true, new Vector3(1000f, 0f, 0f), blasts);
                }
                layers.Add(layer);
            }
            Assert.That(layers[0].Mines.Count, Is.EqualTo(2));
            Assert.That(layers[1].Mines.Count, Is.EqualTo(2), "it rode 1.6 m less to them");
            for (int i = 0; i < 2; i++)
                Assert.That(layers[1].Mines[i].Position.X, Is.EqualTo(layers[0].Mines[i].Position.X).Within(1e-3f), "the same rail point");
        }

        // ---- in FightSim

        sealed class Still : IFightBot
        {
            public FightInput Next(FightSim f, float h) => new FightInput { move = new MoveInput(), holdFire = true };
        }

        static EncounterDef Mechanics() => new EncounterDef
        {
            id = "idle", anchor = GalleriaArena.HallEntry,
            spawns = new[]
            {
                new Spawn(SkylineEnemies.FloorCareId, Post, 0),
                new Spawn(SkylineEnemies.GondolaId, new Vector3(-24.2f, GalleriaArena.RailY, -14f), 1, new Vector3(-24.2f, GalleriaArena.RailY, 10f)),
            },
        };

        // far from both: beyond the gondola's 30 m and the unit's 10 m
        static readonly Vector3 Away = new Vector3(20f, 0f, 15f);

        static FightSim Fight(Func<string, EnemyDef> lookup = null, ulong seed = 4) =>
            new FightSim(Mechanics(), FightTests.Hall(), new Still(), GalleriaArena.FightCover(), Away, seed, Difficulties.Normal(), lookup);

        [Test]
        public void InAFightTheyWorkUntilWokenThenGoOnFromThere()
        {
            var f = Fight();
            var unit = f.Enemies[0];
            var gondola = f.Enemies[1];
            Assert.That(unit.Cleaning, Is.Not.Null);
            Assert.That(gondola.Washing, Is.Not.Null);
            float unitOut = 0f, gondolaOut = 0f;
            var cover = GalleriaArena.FightCover();
            for (int i = 0; i < (int)(30f / FightSim.Step); i++)
            {
                f.Tick();
                Assert.That(unit.Awake || gondola.Awake, Is.False, "nobody sees the player over there");
                float u = Flat(unit.Position - unit.Home);
                unitOut = Math.Max(unitOut, u);
                Assert.That(u, Is.LessThanOrEqualTo(SkylineEnemies.FloorCareUnit().idleLeash + 1e-3f), "the unit keeps to its post");
                Assert.That(unit.Position.Y, Is.EqualTo(Post.Y));
                Assert.That(FloorCareIdle.FloorBlocked(unit.Home, unit.Position, unit.Def.bodyRadius, cover), Is.False, "never in cover");
                float along = gondola.Position.Z - (-14f);
                gondolaOut = Math.Max(gondolaOut, along);
                Assert.That(along, Is.InRange(-1e-3f, SkylineEnemies.Gondola().idleLeash + 1e-3f), "the gondola washes the start of its rail");
                Assert.That(gondola.Position.X, Is.EqualTo(-24.2f).Within(1e-4f));
            }
            Assert.That(unitOut, Is.GreaterThan(0.5f), "the unit cleaned");
            Assert.That(gondolaOut, Is.GreaterThan(1f), "the gondola rode its stretch");

            // woken: each goes on from where its work left it (no jump back to the spawn)
            Vector3 unitAt = unit.Position, gondolaAt = gondola.Position;
            f.Wake(0);
            f.Wake(1);
            f.Tick();
            Assert.That(Flat(unit.Position - unitAt), Is.LessThanOrEqualTo(unit.Def.moveSpeed * FightSim.Step + 1e-3f), "the unit drives on from where it cleaned");
            Assert.That(Vector3.Distance(gondola.Position, gondolaAt), Is.LessThanOrEqualTo(gondola.Def.railSpeed * FightSim.Step + 1e-3f), "the gondola rides on from where it washed");
            Assert.That(gondola.Position.Z, Is.GreaterThan(gondolaAt.Z), "on toward its rail's end");
            Vector3 cleanedAt = unit.Position;
            for (int i = 0; i < 60; i++) f.Tick();
            Assert.That(Flat(unit.Position - cleanedAt), Is.GreaterThan(1f), "awake, it charges at its own speed");
        }

        [Test, Explicit("design aid: prints, per encounter, when the idle bots wake on the human-timed skilled line and how far from their post")]
        public void PrintWhenTheIdleBotsWake()
        {
            foreach (var id in new[] { "food-court", "skylight", "facade" })
            {
                var times = new List<float>();
                var outs = new List<float>();
                int never = 0;
                for (ulong s = 101; s < 131; s++)
                {
                    var f = FightTests.Make(id, new TacticalBot(s, FightTiming.Human()), s, FightTests.ById(id).anchor);
                    var woke = new Dictionary<int, bool>();
                    while (!f.Over && f.Time < 180f)
                    {
                        f.Tick();
                        foreach (var e in f.Enemies)
                        {
                            if ((e.Cleaning == null && e.Washing == null) || woke.ContainsKey(e.Index) || !e.Awake) continue;
                            woke[e.Index] = true;
                            times.Add(f.Time);
                            outs.Add(Vector3.Distance(e.Position, e.Home));
                        }
                    }
                    foreach (var e in f.Enemies) if ((e.Cleaning != null || e.Washing != null) && !woke.ContainsKey(e.Index)) never++;
                }
                times.Sort();
                outs.Sort();
                TestContext.WriteLine(times.Count == 0 ? $"{id}: no idle bots" :
                    $"{id}: {times.Count} idle bots woke over 30 seeds (never: {never}); wake time median {times[times.Count / 2]:0.00} s, max {times[times.Count - 1]:0.00} s; " +
                    $"distance from the post at waking median {outs[outs.Count / 2]:0.00} m, max {outs[outs.Count - 1]:0.00} m");
            }
        }

        [Test]
        public void WokenAtTheStartTheyFightExactlyAsBeforeTheIdleWork()
        {
            // the same fight with the idle work zeroed out in the data, both groups woken before the first step
            Func<string, EnemyDef> still = id =>
            {
                var d = SkylineEnemies.Find(id);
                d.idleSpeed = 0f;
                d.idleLeash = 0f;
                return d;
            };
            var a = Fight(null, 7);
            var b = Fight(still, 7);
            Assert.That(b.Enemies[0].Cleaning == null && b.Enemies[1].Washing == null, Is.True);
            foreach (var f in new[] { a, b }) { f.Wake(0); f.Wake(1); }
            for (int i = 0; i < (int)(12f / FightSim.Step); i++)
            {
                a.Tick();
                b.Tick();
                Assert.That(a.Enemies[0].Position, Is.EqualTo(b.Enemies[0].Position), "the unit's charge is unchanged");
                Assert.That(a.Enemies[1].Position, Is.EqualTo(b.Enemies[1].Position), "the gondola's ride is unchanged");
                Assert.That(a.Enemies[1].Mines.Mines.Count, Is.EqualTo(b.Enemies[1].Mines.Mines.Count), "and its mines");
            }
        }
    }
}

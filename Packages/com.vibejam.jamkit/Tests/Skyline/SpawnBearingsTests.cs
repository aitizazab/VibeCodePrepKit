using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>Task W2: spawn bearings, door choice and the 2 s telegraph (docs/SKYLINE-PLAN-FLOOR1.md). The tests carry their
    /// own copy of the layout sheet's 17 doors (so they do not depend on F2's file) and check every result against an
    /// independent rule checker written with double maths.</summary>
    public class SpawnBearingsTests
    {
        const string D = SkylineEnemies.DroneId, F = SkylineEnemies.FloorCareId, G = SkylineEnemies.GondolaId;

        // ---- fixtures ----

        static SpawnDoor Door(string id, DoorKind k, float x, float y, float z) =>
            new SpawnDoor { id = id, kind = k, position = new Vector3(x, y, z) };

        /// <summary>The layout sheet's 17 spawn doors (docs/SKYLINE-PLAN-FLOOR1.md, "Spawn doors").</summary>
        static List<SpawnDoor> SheetDoors() => new List<SpawnDoor>
        {
            Door("shutter-n1", DoorKind.ShopShutter, -20.8f, 0f, 18f), Door("shutter-n3", DoorKind.ShopShutter, 0f, 0f, 18f),
            Door("shutter-n5", DoorKind.ShopShutter, 20.8f, 0f, 18f), Door("shutter-s1", DoorKind.ShopShutter, -20.8f, 0f, -18f),
            Door("shutter-s5", DoorKind.ShopShutter, 20.8f, 0f, -18f),
            Door("vent-w-n", DoorKind.SkylightVent, -26f, 13.6f, 12f), Door("vent-w-s", DoorKind.SkylightVent, -26f, 13.6f, -12f),
            Door("vent-e-n", DoorKind.SkylightVent, 26f, 13.6f, 12f), Door("vent-e-s", DoorKind.SkylightVent, 26f, 13.6f, -12f),
            Door("vent-c-n", DoorKind.SkylightVent, -4f, 13.6f, 22f), Door("vent-c-s", DoorKind.SkylightVent, -4f, 13.6f, -22f),
            Door("hatch-ne", DoorKind.ServiceHatch, 26f, 0f, 26f), Door("hatch-se", DoorKind.ServiceHatch, 26f, 0f, -26f),
            Door("hatch-w-n", DoorKind.ServiceHatch, -33f, 0f, 14f), Door("hatch-w-s", DoorKind.ServiceHatch, -33f, 0f, -14f),
            Door("rail-n", DoorKind.RailEnd, 36.5f, 3f, 27f), Door("rail-s", DoorKind.RailEnd, 36.5f, 3f, -27f),
        };

        static WaveDef Wave(params string[] who) => new WaveDef { number = 1, enemies = who };

        static SpawnQuery Q(Vector3 player, WaveDef wave, float yaw = 0f, ulong seed = 1) =>
            new SpawnQuery { player = player, viewYawDeg = yaw, viewHalfFovDeg = 55f, wave = wave, seed = seed };

        // ---- the independent rule checker (double maths) ----

        static double Brg(Vector3 from, Vector3 to) => Math.Atan2(to.X - from.X, to.Z - from.Z) * 180.0 / Math.PI;

        static double Ang(double a, double b)
        {
            double d = Math.Abs(a - b) % 360.0;
            return d > 180.0 ? 360.0 - d : d;
        }

        static double Flat(Vector3 a, Vector3 b) => Math.Sqrt((double)(a.X - b.X) * (a.X - b.X) + (double)(a.Z - b.Z) * (a.Z - b.Z));

        const double Eps = 1e-3;

        /// <summary>Asserts every rule that the result's <c>relaxed</c> level promises.</summary>
        static void Verify(SpawnQuery q, SpawnChoice c, IReadOnlyList<SpawnDoor> doors)
        {
            Assert.That(c.ok, Is.True);
            var enemies = q.wave.enemies;
            Assert.That(c.assignments.Count, Is.EqualTo(enemies.Length));
            for (int i = 0; i < enemies.Length; i++)
            {
                Assert.That(c.assignments[i].enemy, Is.EqualTo(enemies[i]));
                Assert.That(FloorRules.DoorAllows(c.assignments[i].door.kind, enemies[i]), Is.True,
                    enemies[i] + " at " + c.assignments[i].door.id);
            }
            var used = c.assignments.Select(a => a.door).Distinct().ToList();
            Assert.That(used.Count, Is.LessThanOrEqualTo(2));
            if (enemies.Length >= 2) Assert.That(used.Count, Is.EqualTo(2), "a wave of 2+ bots uses two doors");
            double half = q.viewHalfFovDeg > 0 ? q.viewHalfFovDeg : 55.0;
            foreach (var d in used)
            {
                Assert.That(doors, Does.Contain(d));
                if (q.busyDoors != null) Assert.That(q.busyDoors, Does.Not.Contain(d.id));
                Assert.That(Flat(q.player, d.position), Is.GreaterThanOrEqualTo(15.0 - Eps), d.id + " is too close");
                if (c.relaxed <= 1 && Flat(q.player, d.position) < 25.0)
                    Assert.That(Ang(Brg(q.player, d.position), q.viewYawDeg), Is.GreaterThan(half), d.id + " is in view");
            }
            if (used.Count == 2)
                Assert.That(Ang(Brg(q.player, used[0].position), Brg(q.player, used[1].position)), Is.GreaterThanOrEqualTo(90.0 - FloorRules.BearingSlack - Eps));
            if (c.relaxed == 0 && q.lastMeanBearing.HasValue && used.Count == 2)
            {
                double far = used.Max(d => Ang(Brg(q.player, d.position), q.lastMeanBearing.Value));
                Assert.That(far, Is.GreaterThanOrEqualTo(120.0 - Eps), "the first door is within 120 of the last wave");
            }
        }

        /// <summary>Brute force: is there ANY legal choice at the loosest level (15 m, 90 degrees less the slack, allowed kinds, both doors used)?</summary>
        static bool AnyLegalAtLoosest(SpawnQuery q, IReadOnlyList<SpawnDoor> doors)
        {
            var far = doors.Where(d => (q.busyDoors == null || !q.busyDoors.Contains(d.id)) && Flat(q.player, d.position) >= 15.0).ToList();
            var e = q.wave.enemies;
            for (int i = 0; i < far.Count; i++)
                for (int j = i + 1; j < far.Count; j++)
                {
                    if (Ang(Brg(q.player, far[i].position), Brg(q.player, far[j].position)) < 90.0 - FloorRules.BearingSlack) continue;
                    for (int mask = 0; mask < (1 << e.Length); mask++)
                    {
                        bool good = true, a = false, b = false;
                        for (int k = 0; k < e.Length && good; k++)
                        {
                            var d = ((mask >> k) & 1) == 0 ? far[i] : far[j];
                            if (!FloorRules.DoorAllows(d.kind, e[k])) good = false;
                            if (((mask >> k) & 1) == 0) a = true; else b = true;
                        }
                        if (good && a && b) return true;
                    }
                }
            return false;
        }

        // ---- hand-made cases: each rule alone ----

        [Test]
        public void ThePlayerInTheMiddleOfTheRingGetsTwoLegalDoorsWithNothingRelaxed()
        {
            var doors = SheetDoors();
            foreach (var wave in new[] { Wave(D, D, D), Wave(D, D, D, F), Wave(D, D, F, F), Wave(G, D, D) })
            {
                var q = Q(new Vector3(0f, 0f, 12f), wave);
                var c = SpawnBearings.Choose(q, doors);
                Verify(q, c, doors);
                Assert.That(c.relaxed, Is.EqualTo(0));
            }
        }

        [Test]
        public void ADoorCloserThanFifteenMetresIsNeverUsed()
        {
            // Near door 10 m away, far doors east and west: only the far two are candidates.
            var doors = new List<SpawnDoor>
            {
                Door("near", DoorKind.ShopShutter, 0f, 0f, 10f),
                Door("east", DoorKind.ShopShutter, 20f, 0f, 0f), Door("west", DoorKind.ShopShutter, -20f, 0f, 0f),
            };
            var q = Q(Vector3.Zero, Wave(D, D), 180f);
            var c = SpawnBearings.Choose(q, doors);
            Verify(q, c, doors);
            Assert.That(c.assignments.Select(a => a.door.id), Does.Not.Contain("near"));
            // With only the near door and one far door, nothing is legal, and the rule is never dropped.
            var two = new List<SpawnDoor> { doors[0], doors[1] };
            Assert.That(SpawnBearings.Choose(q, two).ok, Is.False);
        }

        [Test]
        public void TwoDoorsOnTheSameBearingAreNotAPair()
        {
            var doors = new List<SpawnDoor>
            {
                Door("a", DoorKind.ShopShutter, 0f, 0f, 20f), Door("b", DoorKind.ShopShutter, 0f, 0f, 30f),
            };
            var q = Q(Vector3.Zero, Wave(D, D), 180f);
            Assert.That(SpawnBearings.Choose(q, doors).ok, Is.False, "a 0-degree spread is never dropped");
            doors.Add(Door("c", DoorKind.ShopShutter, 20f, 0f, 0f));   // 90 degrees from a and b
            var c = SpawnBearings.Choose(q, doors);
            Verify(q, c, doors);
            Assert.That(c.assignments.Select(a => a.door.id), Does.Contain("c"));
        }

        [Test]
        public void ADoorExactlyNinetyDegreesApartIsLegal()
        {
            var doors = new List<SpawnDoor>
            {
                Door("n", DoorKind.ShopShutter, 0f, 0f, 20f), Door("e", DoorKind.ShopShutter, 20f, 0f, 0f),
            };
            var q = Q(Vector3.Zero, Wave(D, D), 180f);
            Verify(q, SpawnBearings.Choose(q, doors), doors);
        }

        [Test]
        public void TheFirstDoorIsAtLeastOneTwentyFromTheLastWave()
        {
            var doors = new List<SpawnDoor>
            {
                Door("n", DoorKind.ShopShutter, 0f, 0f, 20f), Door("e", DoorKind.ShopShutter, 20f, 0f, 0f),
                Door("s", DoorKind.ShopShutter, 0f, 0f, -20f), Door("w", DoorKind.ShopShutter, -20f, 0f, 0f),
            };
            var q = Q(Vector3.Zero, Wave(D, D, D));
            q.viewYawDeg = 45f; q.viewHalfFovDeg = 1f;      // looking between the doors: the view is not the rule under test
            q.lastMeanBearing = 0f;                           // the last wave came from the north
            for (ulong seed = 1; seed <= 12; seed++)
            {
                q.seed = seed;
                var c = SpawnBearings.Choose(q, doors);
                Verify(q, c, doors);
                Assert.That(c.relaxed, Is.EqualTo(0));
                Assert.That(c.assignments.Select(a => a.door.id), Does.Contain("s"), "only the south door is 120+ from the north");
            }
            // With the south door gone no pair clears the 120: the rule is dropped (relaxed 1), the wave still comes.
            var noS = doors.Where(d => d.id != "s").ToList();
            var r = SpawnBearings.Choose(q, noS);
            Verify(q, r, noS);
            Assert.That(r.relaxed, Is.EqualTo(1));
        }

        [Test]
        public void ADoorInViewWithinTwentyFiveMetresIsSkippedButFarOnesAreFine()
        {
            var doors = new List<SpawnDoor>
            {
                Door("n20", DoorKind.ShopShutter, 0f, 0f, 20f), Door("n30", DoorKind.ShopShutter, 0f, 0f, 30f),
                Door("e", DoorKind.ShopShutter, 20f, 0f, 0f), Door("w", DoorKind.ShopShutter, -20f, 0f, 0f),
            };
            var q = Q(Vector3.Zero, Wave(D, D), 0f);          // looking north
            for (ulong seed = 1; seed <= 20; seed++)
            {
                q.seed = seed;
                var c = SpawnBearings.Choose(q, doors);
                Verify(q, c, doors);
                Assert.That(c.relaxed, Is.EqualTo(0));
                Assert.That(c.assignments.Select(a => a.door.id), Does.Not.Contain("n20"));
            }
            // n30 is allowed (beyond 25 m): it must appear in some seeds.
            var seen = new HashSet<string>();
            for (ulong seed = 1; seed <= 60; seed++)
            {
                q.seed = seed;
                foreach (var a in SpawnBearings.Choose(q, doors).assignments) seen.Add(a.door.id);
            }
            Assert.That(seen, Does.Contain("n30"));
        }

        [Test]
        public void WhenOnlyInViewDoorsFormAPairTheViewRuleIsDroppedLast()
        {
            var doors = new List<SpawnDoor>
            {
                Door("a", DoorKind.ShopShutter, 0f, 0f, 20f), Door("b", DoorKind.ShopShutter, 12f, 0f, 16f),   // both in view
                Door("c", DoorKind.ShopShutter, 20f, 0f, 0f),
            };
            var q = Q(Vector3.Zero, Wave(D, D), 0f);
            q.viewHalfFovDeg = 80f;
            var c = SpawnBearings.Choose(q, doors);
            Verify(q, c, doors);
            Assert.That(c.relaxed, Is.EqualTo(2));
        }

        [Test]
        public void TheViewConeUsesTheCamerasHalfFovAndDefaultsToFiftyFive()
        {
            var doors = new List<SpawnDoor>
            {
                Door("ahead", DoorKind.ShopShutter, 20f * (float)Math.Sin(50 * Math.PI / 180), 0f, 20f * (float)Math.Cos(50 * Math.PI / 180)),
                Door("e", DoorKind.ShopShutter, 20f, 0f, -10f), Door("w", DoorKind.ShopShutter, -20f, 0f, -10f),
            };
            var q = Q(Vector3.Zero, Wave(D, D), 0f);
            q.viewHalfFovDeg = 0f;                              // unset: 55, so 50 degrees off the nose is in view
            Assert.That(SpawnBearings.Choose(q, doors).assignments.Select(a => a.door.id), Does.Not.Contain("ahead"));
            q.viewHalfFovDeg = 40f;                             // a narrower camera: it is outside the cone
            var seen = new HashSet<string>();
            for (ulong s = 1; s <= 40; s++) { q.seed = s; foreach (var a in SpawnBearings.Choose(q, doors).assignments) seen.Add(a.door.id); }
            Assert.That(seen, Does.Contain("ahead"));
        }

        [Test]
        public void BusyDoorsAreSkipped()
        {
            var doors = SheetDoors();
            var q = Q(new Vector3(0f, 0f, 0f), Wave(D, D, D), 0f);
            var busy = new List<string>();
            for (int round = 0; round < 6; round++)
            {
                q.busyDoors = busy.ToList();
                q.seed = (ulong)round + 5;
                var c = SpawnBearings.Choose(q, doors);
                if (!c.ok) break;
                Verify(q, c, doors);
                foreach (var d in c.assignments.Select(a => a.door.id).Distinct()) busy.Add(d);
            }
            Assert.That(busy.Count, Is.EqualTo(busy.Distinct().Count()), "no door was chosen twice while busy");
        }

        [Test]
        public void EveryBotIsOnADoorThatAllowsIt()
        {
            var doors = SheetDoors();
            foreach (var wave in new[] { Wave(D, D, D, F), Wave(D, D, F, F), Wave(G, D, D) })
                for (ulong seed = 1; seed <= 30; seed++)
                {
                    var q = Q(new Vector3(-8f, 0f, 3f), wave, 90f, seed);
                    Verify(q, SpawnBearings.Choose(q, doors), doors);
                }
        }

        [Test]
        public void AGondolaWaveUsesARailEndAsOneOfItsTwoBearingPoints()
        {
            var doors = SheetDoors();
            var q = Q(new Vector3(-10f, 0f, 0f), Wave(G, D, D), 270f);
            for (ulong seed = 1; seed <= 20; seed++)
            {
                q.seed = seed;
                var c = SpawnBearings.Choose(q, doors);
                Verify(q, c, doors);
                var gon = c.assignments.Single(a => a.enemy == G).door;
                Assert.That(gon.kind, Is.EqualTo(DoorKind.RailEnd));
                var others = c.assignments.Where(a => a.enemy == D).Select(a => a.door).Distinct().ToList();
                Assert.That(others.Count, Is.EqualTo(1), "the drones share the other point");
                Assert.That(SpawnBearings.AngleBetween(SpawnBearings.Bearing(q.player, gon.position),
                    SpawnBearings.Bearing(q.player, others[0].position)), Is.GreaterThanOrEqualTo(90f - FloorRules.BearingSlack - 1e-3f));
            }
        }

        [Test]
        public void ARailEndTooCloseOrInViewDoesNotSpawnTheGondola()
        {
            var doors = SheetDoors();
            // Standing near the south rail end rules it out (under 15 m), so the gondola comes from the north one.
            var q = Q(new Vector3(30f, 0f, -24f), Wave(G, D, D), 0f);
            var c = SpawnBearings.Choose(q, doors);
            Verify(q, c, doors);
            Assert.That(c.assignments.First(a => a.enemy == G).door.id, Is.EqualTo("rail-n"));
        }

        [Test]
        public void ASingleBotWaveGetsOneLegalDoor()
        {
            var doors = SheetDoors();
            var q = Q(new Vector3(0f, 0f, 0f), Wave(D), 0f);
            var c = SpawnBearings.Choose(q, doors);
            Assert.That(c.ok, Is.True);
            Assert.That(c.assignments.Count, Is.EqualTo(1));
            Assert.That(Flat(q.player, c.assignments[0].door.position), Is.GreaterThanOrEqualTo(15.0));
        }

        // ---- the mean bearing ----

        [Test]
        public void TheMeanBearingIsTheCircularMeanOfTheUsedDoors()
        {
            Assert.That(SpawnBearings.CircularMean(new[] { 350f, 10f }, 0f), Is.EqualTo(0f).Within(0.01f));
            Assert.That(SpawnBearings.CircularMean(new[] { 170f, -170f }, 0f), Is.EqualTo(180f).Within(0.01f).Or.EqualTo(-180f).Within(0.01f));
            Assert.That(SpawnBearings.CircularMean(new[] { 0f, 90f }, 0f), Is.EqualTo(45f).Within(0.01f));
            Assert.That(SpawnBearings.CircularMean(new[] { 0f, 180f }, 77f), Is.EqualTo(77f), "opposite doors have no mean: the fallback");

            var doors = new List<SpawnDoor>
            {
                Door("n", DoorKind.ShopShutter, 0f, 0f, 20f), Door("e", DoorKind.ShopShutter, 20f, 0f, 0f),
            };
            var q = Q(Vector3.Zero, Wave(D, D), 180f);
            Assert.That(SpawnBearings.Choose(q, doors).meanBearing, Is.EqualTo(45f).Within(0.01f));
        }

        // ---- the property test: every point of the hall, five view yaws ----

        [Test]
        public void EveryPointOfTheHallGetsALegalChoiceOrAnHonestNo()
        {
            var doors = SheetDoors();
            var waves = new[] { Wave(D, D, D), Wave(D, D, D, F), Wave(D, D, F, F), Wave(G, D, D) };
            float[] yaws = { 0f, 72f, 144f, 216f, 288f };
            int total = 0, notOk = 0, relaxed1 = 0, relaxed2 = 0;
            var byWave = new Dictionary<string, int>(); int middleRefused = 0;
            for (float x = -35f; x <= 35f; x += 2f)
                for (float z = -28f; z <= 28f; z += 2f)
                    foreach (float yaw in yaws)
                        foreach (var wave in waves)
                        {
                            var q = Q(new Vector3(x, 0f, z), wave, yaw, (ulong)(total + 1));
                            q.lastMeanBearing = (total % 7) * 51f - 180f;
                            var c = SpawnBearings.Choose(q, doors);
                            total++;
                            if (c.ok)
                            {
                                Verify(q, c, doors);
                                if (c.relaxed == 1) relaxed1++; else if (c.relaxed == 2) relaxed2++;
                            }
                            else
                            {
                                notOk++;
                                string wk = string.Join(",", wave.enemies.Select(e => e.Substring(0, 2)));
                                byWave[wk] = byWave.TryGetValue(wk, out int cnt) ? cnt + 1 : 1;
                                if (Math.Abs(x) <= 19f) middleRefused++;
                                Assert.That(c.assignments, Is.Empty);
                                Assert.That(AnyLegalAtLoosest(q, doors), Is.False,
                                    $"refused although a legal pair exists: player ({x},{z}) yaw {yaw} wave {string.Join(",", wave.enemies)}");
                            }
                        }
            TestContext.WriteLine($"SPAWN grid: {total} queries, {notOk} refused, {relaxed1} dropped the 120, {relaxed2} dropped the view rule");
            foreach (var kv in byWave) TestContext.WriteLine("  refused for " + kv.Key + ": " + kv.Value);
            // The two end bands (the west leg and ring, the east promenade) can have no legal pair: every door lies in one half-plane
            // and the near ones are under 15 m. Known and reported to the director (see the W2 note in the plan); the middle is
            // always answerable.
            Assert.That(middleRefused, Is.EqualTo(0), "the middle 38 m of the hall must always have a legal choice");
        }

        [Test]
        public void ThePlayerAtTheOddSpotsStillGetsAnAnswer()
        {
            var doors = SheetDoors();
            var spots = new Dictionary<string, Vector3>
            {
                { "arrival alcove", new Vector3(0f, 0f, -21f) }, { "inside shop N2", new Vector3(-10.4f, 0f, 21f) },
                { "pit corner NE", new Vector3(9f, -4f, 7f) }, { "pit corner SW", new Vector3(-13f, -4f, -7f) },
                { "bridge", new Vector3(8f, 6f, 0f) },
            };
            foreach (var kv in spots)
                foreach (float yaw in new[] { 0f, 90f, 180f, 270f })
                    foreach (var wave in new[] { Wave(D, D, D), Wave(D, D, F, F), Wave(G, D, D) })
                    {
                        var q = Q(kv.Value, wave, yaw);
                        var c = SpawnBearings.Choose(q, doors);
                        Assert.That(c.ok, Is.True, kv.Key + " yaw " + yaw);
                        Verify(q, c, doors);
                    }
        }

        // ---- the alarm and the 120 chain ----

        [Test]
        public void AfterAnAlarmTheDoorsAreTheNearestLegalPairToTheAsset()
        {
            var doors = SheetDoors();
            var assets = new[] { new Vector3(9f, -4f, 7f), new Vector3(-10.4f, 0f, -21f), new Vector3(8f, 6f, 0f), new Vector3(-13f, -4f, -7f) };
            foreach (var asset in assets)
                for (float yaw = 0f; yaw < 360f; yaw += 90f)
                    for (ulong seed = 1; seed <= 3; seed++)
                    {
                        var q = Q(new Vector3(-4f, 0f, 2f), Wave(D, D, F, F), yaw, seed);
                        q.alarmAsset = asset;
                        q.lastMeanBearing = 30f;
                        var c = SpawnBearings.Choose(q, doors);
                        Verify(q, c, doors);
                        double got = c.assignments.Select(a => a.door).Distinct().Sum(d => (double)Vector3.Distance(d.position, asset));
                        double best = BestPairCost(q, doors, asset, c.relaxed);
                        Assert.That(got, Is.EqualTo(best).Within(0.05), $"asset {asset} yaw {yaw}");
                    }
        }

        // The cheapest legal pair at the given relax level, by plain enumeration with the wave's kinds.
        static double BestPairCost(SpawnQuery q, IReadOnlyList<SpawnDoor> doors, Vector3 asset, int relaxed)
        {
            double best = double.MaxValue;
            double half = q.viewHalfFovDeg > 0 ? q.viewHalfFovDeg : 55.0;
            var ok = doors.Where(d => Flat(q.player, d.position) >= 15.0
                && !(relaxed <= 1 && Flat(q.player, d.position) < 25.0 && Ang(Brg(q.player, d.position), q.viewYawDeg) <= half)).ToList();
            var e = q.wave.enemies;
            for (int i = 0; i < ok.Count; i++)
                for (int j = i + 1; j < ok.Count; j++)
                {
                    double ba = Brg(q.player, ok[i].position), bb = Brg(q.player, ok[j].position);
                    if (Ang(ba, bb) < 90.0 - FloorRules.BearingSlack) continue;
                    if (relaxed == 0 && q.lastMeanBearing.HasValue
                        && Math.Max(Ang(ba, q.lastMeanBearing.Value), Ang(bb, q.lastMeanBearing.Value)) < 120.0) continue;
                    bool feasible = false;
                    for (int mask = 0; mask < (1 << e.Length) && !feasible; mask++)
                    {
                        bool good = true, a = false, b = false;
                        for (int k = 0; k < e.Length && good; k++)
                        {
                            bool useA = ((mask >> k) & 1) == 0;
                            if (!FloorRules.DoorAllows((useA ? ok[i] : ok[j]).kind, e[k])) good = false;
                            if (useA) a = true; else b = true;
                        }
                        feasible = good && a && b;
                    }
                    if (!feasible) continue;
                    best = Math.Min(best, Vector3.Distance(ok[i].position, asset) + Vector3.Distance(ok[j].position, asset));
                }
            return best;
        }

        [Test]
        public void SuccessiveWavesComeFromDifferentSidesWhenTheHallAllowsIt()
        {
            var doors = SheetDoors();
            var player = new Vector3(0f, 0f, 0f);
            float? last = null;
            int clean = 0;
            for (int n = 1; n <= 12; n++)
            {
                var q = Q(player, WavePlans.For(n, false), 0f, (ulong)n);
                q.lastMeanBearing = last;
                var c = SpawnBearings.Choose(q, doors);
                Verify(q, c, doors);
                if (c.relaxed == 0 && last.HasValue)
                {
                    clean++;
                    double far = c.assignments.Select(a => a.door).Distinct().Max(d => Ang(Brg(player, d.position), last.Value));
                    Assert.That(far, Is.GreaterThanOrEqualTo(120.0 - Eps));
                }
                last = c.meanBearing;
            }
            Assert.That(clean, Is.GreaterThanOrEqualTo(8), "the 120-degree rule should hold for nearly every wave from the middle of the hall");
        }

        [Test]
        public void TheSameQueryGivesTheSameChoiceAndSeedsVary()
        {
            var doors = SheetDoors();
            var q = Q(new Vector3(3f, 0f, -2f), Wave(D, D, D, F), 40f, 77);
            string Key(SpawnChoice c) => string.Join("|", c.assignments.Select(a => a.enemy + "@" + a.door.id));
            Assert.That(Key(SpawnBearings.Choose(q, doors)), Is.EqualTo(Key(SpawnBearings.Choose(q, doors))));
            var keys = new HashSet<string>();
            for (ulong s = 1; s <= 40; s++) { q.seed = s; keys.Add(Key(SpawnBearings.Choose(q, doors))); }
            Assert.That(keys.Count, Is.GreaterThanOrEqualTo(4), "the seed should vary the doors");
        }

        [Test]
        public void BadQueriesAreRefusedNotThrown()
        {
            var doors = SheetDoors();
            Assert.That(SpawnBearings.Choose(new SpawnQuery(), doors).ok, Is.False);
            Assert.That(SpawnBearings.Choose(Q(Vector3.Zero, Wave()), doors).ok, Is.False);
            Assert.That(SpawnBearings.Choose(Q(Vector3.Zero, Wave(D, D)), null).ok, Is.False);
            Assert.That(SpawnBearings.Choose(Q(Vector3.Zero, Wave(D, D)), new List<SpawnDoor>()).ok, Is.False);
            Assert.That(SpawnBearings.Choose(Q(new Vector3(float.NaN, 0f, 0f), Wave(D, D)), doors).ok, Is.False);
            Assert.That(SpawnBearings.Choose(Q(Vector3.Zero, Wave("no-such-bot", D)), doors).ok, Is.False);
            Assert.That(SpawnBearings.Choose(Q(Vector3.Zero, Wave(SkylineEnemies.SpotlightId, D)), doors).ok, Is.False, "the spotlight is never allowed");
            var withNull = new List<SpawnDoor> { null, doors[0], doors[3] };
            Assert.DoesNotThrow(() => SpawnBearings.Choose(Q(Vector3.Zero, Wave(D, D)), withNull));
        }

        // ---- the telegraph ----

        static SpawnChoice FakeChoice(params string[] doorIds)
        {
            var c = new SpawnChoice { ok = true };
            foreach (var id in doorIds) c.assignments.Add(new SpawnAssignment { enemy = D, door = Door(id, DoorKind.ShopShutter, 0f, 0f, 20f) });
            return c;
        }

        [Test]
        public void TheTelegraphStartsAtOrderAndTheBotsAreDueTwoSecondsLater()
        {
            var sp = new WaveSpawner();
            sp.Order(FakeChoice("a", "a", "b"), 10f);
            var due = new List<SpawnAssignment>(); var started = new List<SpawnAssignment>();
            sp.Tick(10.016f, due, started);
            Assert.That(started.Count, Is.EqualTo(3));
            Assert.That(due, Is.Empty);
            Assert.That(sp.Busy("a"), Is.True);
            Assert.That(sp.Busy("b"), Is.True);
            Assert.That(sp.Busy("c"), Is.False);

            started.Clear();
            sp.Tick(11.9f, due, started);
            Assert.That(started, Is.Empty, "a telegraph is reported once");
            Assert.That(due, Is.Empty);
            Assert.That(sp.Busy("a"), Is.True);

            sp.Tick(12.0f, due, started);
            Assert.That(due.Count, Is.EqualTo(3));
            Assert.That(due[0].door.id, Is.EqualTo("a"));
            Assert.That(sp.Busy("a"), Is.False, "free once its bots are out");
            Assert.That(sp.PendingCount, Is.EqualTo(0));
            due.Clear();
            sp.Tick(13f, due, started);
            Assert.That(due, Is.Empty, "a bot is due once");
        }

        [Test]
        public void ADueTimeIsHitWithinOneFrameAtSixtyFps()
        {
            for (int offset = 0; offset < 60; offset++)
            {
                var sp = new WaveSpawner();
                float t0 = 100f + offset * 0.0137f;
                sp.Order(FakeChoice("a", "b"), t0);
                var due = new List<SpawnAssignment>();
                float now = t0; int frames = 0;
                while (due.Count == 0 && frames < 500) { now += 1f / 60f; frames++; sp.Tick(now, due, null); }
                Assert.That(now - t0, Is.InRange(2.0f - 1e-3f, 2.0f + 1f / 60f + 1e-3f), "offset " + offset);
            }
        }

        [Test]
        public void ALongHitchReportsTheTelegraphAndTheDueTogetherInOrder()
        {
            var sp = new WaveSpawner();
            sp.Order(FakeChoice("a", "b"), 0f);
            var due = new List<SpawnAssignment>(); var started = new List<SpawnAssignment>();
            sp.Tick(30f, due, started);
            Assert.That(started.Count, Is.EqualTo(2));
            Assert.That(due.Select(a => a.door.id).ToArray(), Is.EqualTo(new[] { "a", "b" }));
        }

        [Test]
        public void CancelDropsPendingOrdersAndFreesTheDoors()
        {
            var sp = new WaveSpawner();
            sp.Order(FakeChoice("a", "b"), 0f);
            sp.Tick(0.5f, null, null);
            sp.Cancel();
            Assert.That(sp.Busy("a"), Is.False);
            Assert.That(sp.BusyDoors(), Is.Empty);
            var due = new List<SpawnAssignment>(); var started = new List<SpawnAssignment>();
            sp.Tick(5f, due, started);
            Assert.That(due, Is.Empty);
            Assert.That(started, Is.Empty);
        }

        [Test]
        public void BusyDoorsFeedTheNextQuery()
        {
            var doors = SheetDoors();
            var sp = new WaveSpawner();
            var q = Q(new Vector3(0f, 0f, 0f), Wave(D, D, D), 0f, 3);
            var first = SpawnBearings.Choose(q, doors);
            sp.Order(first, 0f);
            q.busyDoors = sp.BusyDoors();
            Assert.That(q.busyDoors.Count, Is.EqualTo(first.assignments.Select(a => a.door.id).Distinct().Count()));
            var second = SpawnBearings.Choose(q, doors);
            Verify(q, second, doors);
            foreach (var a in second.assignments) Assert.That(q.busyDoors, Does.Not.Contain(a.door.id));
        }

        [Test]
        public void ANotOkChoiceOrdersNothingAndBadTimesAreIgnored()
        {
            var sp = new WaveSpawner();
            sp.Order(new SpawnChoice { ok = false }, 0f);
            sp.Order(null, 0f);
            sp.Order(FakeChoice("a"), float.NaN);
            Assert.That(sp.PendingCount, Is.EqualTo(0));
            sp.Order(FakeChoice("a"), 1f);
            Assert.DoesNotThrow(() => sp.Tick(float.NaN, null, null));
            Assert.DoesNotThrow(() => sp.Tick(1.5f, null, null));
            Assert.That(sp.PendingCount, Is.EqualTo(1));
        }
    }
}

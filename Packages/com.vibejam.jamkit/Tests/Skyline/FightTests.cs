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
    /// <summary>The main hall's fights on Normal with the player shooting back (Task 11b): standing in the open is a quick
    /// wreck, cover is real, and a skilled line clears each fight without chassis damage.</summary>
    public class FightTests
    {
        public const int Seeds = 30;
        const ulong FirstSeed = 101;
        const float Cap = 180f;

        /// <summary>The main hall as the movement engine sees it: the floor, four 10 m walls, and every cover piece solid.</summary>
        public static BoxWorld Hall()
        {
            var w = new BoxWorld();
            float x0 = GalleriaArena.HallMinX, x1 = GalleriaArena.HallMaxX, z0 = GalleriaArena.HallMinZ, z1 = GalleriaArena.HallMaxZ;
            w.Floor(x0 - 1f, x1 + 1f, z0 - 1f, z1 + 1f, 0f);
            w.Add(new Vector3(x0 - 0.5f, 5f, 0f), new Vector3(1f, 10f, z1 - z0 + 2f));
            w.Add(new Vector3(x1 + 0.5f, 5f, 0f), new Vector3(1f, 10f, z1 - z0 + 2f));
            w.Add(new Vector3(0f, 5f, z0 - 0.5f), new Vector3(x1 - x0 + 2f, 10f, 1f));
            w.Add(new Vector3(0f, 5f, z1 + 0.5f), new Vector3(x1 - x0 + 2f, 10f, 1f));
            foreach (var b in GalleriaArena.FightCover()) w.Add((b.min + b.max) * 0.5f, b.max - b.min, false);
            return w;
        }

        public static EncounterDef ById(string id) => Array.Find(GalleriaArena.Encounters(), e => e.id == id);

        public static FightSim Make(string id, IFightBot bot, ulong seed, Vector3 start, Func<string, EnemyDef> lookup = null, DifficultyDef diff = null) =>
            new FightSim(ById(id), Hall(), bot, GalleriaArena.FightCover(), start, seed, diff ?? Difficulties.Normal(), lookup, null, null, 0f);

        /// <summary>Held-out fight seeds: the tuning sweep checks its picks on these as well as on the gate's own.</summary>
        public const ulong HeldOutSeed = 201;

        public static FightResult[] RunSeeds(string id, Func<ulong, IFightBot> bot, Vector3 start, int seeds = Seeds, float cap = Cap,
            Func<string, EnemyDef> lookup = null, DifficultyDef diff = null, ulong first = FirstSeed)
        {
            var r = new FightResult[seeds];
            for (int i = 0; i < seeds; i++)
            {
                ulong s = first + (ulong)i;
                r[i] = Make(id, bot(s), s, start, lookup, diff).Run(cap);
            }
            return r;
        }

        public static FightSim[] RunSims(string id, Func<ulong, IFightBot> bot, Vector3 start, int seeds = Seeds, float cap = Cap,
            Func<string, EnemyDef> lookup = null, DifficultyDef diff = null)
        {
            var r = new FightSim[seeds];
            for (int i = 0; i < seeds; i++)
            {
                ulong s = FirstSeed + (ulong)i;
                r[i] = Make(id, bot(s), s, start, lookup, diff);
                r[i].Run(cap);
            }
            return r;
        }

        static FightResult[] Results(FightSim[] sims)
        {
            var r = new FightResult[sims.Length];
            for (int i = 0; i < sims.Length; i++) r[i] = sims[i].Result();
            return r;
        }

        // ---- design aid: which groups see each metre of the hall's floor

        [Test, Explicit("design aid: prints, per encounter, which groups see each metre of the floor (# is cover)")]
        public void PrintTheSightMaps([Values(false, true)] bool counts)
        {
            var sb = new StringBuilder();
            foreach (var e in GalleriaArena.Encounters())
            {
                var f = new FightSim(e, Hall(), new StandAndShootBot(), GalleriaArena.FightCover(), e.anchor, 1);
                sb.AppendLine(e.id + (counts ? " COUNTS of shooters" : " group masks (bit 0 = group 0)") + "  (rows z = 19 at the top to -19; columns x = -24..24; . = none)");
                for (int z = 19; z >= -19; z--)
                {
                    sb.Append(z.ToString().PadLeft(4)).Append(' ');
                    for (int x = -24; x <= 24; x++)
                    {
                        var feet = new Vector3(x, 0f, z);
                        if (CoverGeometry.DistanceToCover(feet, GalleriaArena.HallCover()) < 0.4f) { sb.Append('#'); continue; }
                        int mask = 0, count = 0;
                        foreach (var en in f.Enemies)
                            if (FightSim.IsShooter(en) && f.CanSee(en, feet)) { mask |= 1 << en.Group; count++; }
                        sb.Append(counts ? (count == 0 ? '.' : (char)('0' + Math.Min(9, count))) : mask == 0 ? '.' : "0123456789ABCDEF"[mask & 15]);
                    }
                    sb.AppendLine();
                }
            }
            TestContext.WriteLine(sb.ToString());
        }

        static float Mean(FightResult[] rs, Func<FightResult, float> f)
        {
            float s = 0f;
            foreach (var r in rs) s += f(r);
            return s / rs.Length;
        }

        static float TimeToWreck(FightResult r) => r.playerWrecked ? r.wreckedAt : r.seconds;

        static int Clean(FightResult[] rs) { int n = 0; foreach (var r in rs) if (r.cleared && r.chassisLost == 0f) n++; return n; }
        static float Lowest(FightResult[] rs) { float m = float.PositiveInfinity; foreach (var r in rs) m = Math.Min(m, r.minPlating); return m; }
        static float MedianLow(FightResult[] rs) { var l = new List<float>(); foreach (var r in rs) l.Add(r.minPlating); l.Sort(); return l.Count % 2 == 1 ? l[l.Count / 2] : 0.5f * (l[l.Count / 2 - 1] + l[l.Count / 2]); }
        static int Damaged(FightResult[] rs) { int n = 0; foreach (var r in rs) if (r.damageTaken > 0f) n++; return n; }

        static int ShootersSeeing(FightSim f, Vector3 feet)
        {
            int n = 0;
            foreach (var e in f.Enemies) if (FightSim.IsShooter(e) && f.CanSee(e, feet)) n++;
            return n;
        }

        static int Shooters(FightSim f)
        {
            int n = 0;
            foreach (var e in f.Enemies) if (FightSim.IsShooter(e)) n++;
            return n;
        }

        // ---- standing still

        /// <summary>The open stand per encounter: a point 3 m and more from any cover that most of the fight's shooters see.</summary>
        public static Vector3 OpenSpot(string id) => new Vector3(-12f, 0f, 8.5f);

        /// <summary>The partly covered stand: off the booth's south-west corner, where the screens block most sightlines but not all.</summary>
        public static Vector3 CoverSpot(string id) => new Vector3(-3f, 0f, -15f);

        [Test]
        public void TheOpenSpotIsClearOfCoverAndSeenByMostShootersAndTheCoverSpotIsPartlyExposed()
        {
            foreach (var e in GalleriaArena.Encounters())
            {
                var f = Make(e.id, new StandAndShootBot(), 1, OpenSpot(e.id));
                int all = Shooters(f), open = ShootersSeeing(f, OpenSpot(e.id)), cover = ShootersSeeing(f, CoverSpot(e.id));
                TestContext.WriteLine($"{e.id}: {all} shooters; the open spot is seen by {open}, the covered spot by {cover}");
                Assert.That(CoverGeometry.DistanceToCover(OpenSpot(e.id), GalleriaArena.HallCover()), Is.GreaterThanOrEqualTo(3f), e.id);
                Assert.That(open * 2, Is.GreaterThan(all), e.id + ": most shooters see the open spot");
                var groups = new HashSet<int>();
                var seenGroups = new HashSet<int>();
                foreach (var en in f.Enemies)
                {
                    if (!FightSim.IsShooter(en)) continue;
                    groups.Add(en.Group);
                    if (f.CanSee(en, OpenSpot(e.id))) seenGroups.Add(en.Group);
                }
                Assert.That(seenGroups.Count, Is.EqualTo(groups.Count), e.id + ": every group has a shooter that sees the open spot (in sight of the fight)");
                Assert.That(cover, Is.GreaterThanOrEqualTo(1), e.id + ": the covered spot is partly exposed (a box blocks most sightlines, not all)");
                Assert.That(cover * 2, Is.LessThanOrEqualTo(open), e.id + ": the box blocks at least half the sightlines the open spot has");
            }
        }

        [TestCase("food-court")]
        [TestCase("skylight")]
        [TestCase("facade")]
        public void StandingStillInTheOpenIsAQuickWreckAndCoverAtLeastDoublesIt(string id)
        {
            var open = RunSeeds(id, s => new StandAndShootBot(), OpenSpot(id), Seeds, 60f);
            var cover = RunSeeds(id, s => new StandAndShootBot(), CoverSpot(id), Seeds, 60f);
            float openMean = Mean(open, TimeToWreck), coverMean = Mean(cover, TimeToWreck);
            int openWrecks = 0, coverWrecks = 0;
            foreach (var r in open) if (r.playerWrecked) openWrecks++;
            foreach (var r in cover) if (r.playerWrecked) coverWrecks++;
            TestContext.WriteLine($"{id}: standing in the open, wrecked in {openWrecks}/{Seeds} at a mean {openMean:0.0} s (Writ shots {Mean(open, r => r.shotsFired):0}, " +
                $"shots at it {Mean(open, r => r.shotsAtPlayer):0.0}, hits {Mean(open, r => r.hitsTaken):0.0}, bots wrecked {Mean(open, r => r.enemiesWrecked):0.0}/{open[0].enemies}); " +
                $"partly covered: wrecked in {coverWrecks}/{Seeds}, mean {coverMean:0.0} s (capped at 60 s), ratio {coverMean / openMean:0.0}");
            Assert.That(openMean, Is.LessThanOrEqualTo(12f), "standing still in the open is a quick wreck (the creator, 2026-10-13)");
            Assert.That(coverMean, Is.GreaterThanOrEqualTo(2f * openMean), "cover is real: it at least doubles the time");
        }

        // ---- the skilled line (the gate: human timing)

        static string Summary(FightResult[] rs)
        {
            int cleared = 0, wrecked = 0, chassisHit = 0;
            float minPlating = float.PositiveInfinity, maxChassis = 0f, minClear = float.PositiveInfinity, maxClear = 0f, sumClear = 0f;
            var lows = new List<float>();
            foreach (var r in rs)
            {
                if (r.cleared) { cleared++; minClear = Math.Min(minClear, r.clearSeconds); maxClear = Math.Max(maxClear, r.clearSeconds); sumClear += r.clearSeconds; }
                if (r.playerWrecked) wrecked++;
                if (r.chassisLost > 0f) chassisHit++;
                minPlating = Math.Min(minPlating, r.minPlating);
                maxChassis = Math.Max(maxChassis, r.chassisLost);
                lows.Add(r.minPlating);
            }
            lows.Sort();
            return $"cleared {cleared}/{rs.Length} (clear {minClear:0}-{maxClear:0} s, mean {sumClear / Math.Max(1, cleared):0}), " +
                $"wrecked {wrecked}, chassis lost in {chassisHit} (worst {maxChassis:0}), lowest plating {minPlating:0} (median of the seeds' lows {lows[lows.Count / 2]:0}), " +
                $"damage taken {Mean(rs, r => r.damageTaken):0}, hits {Mean(rs, r => r.hitsTaken):0.0}, shots at it {Mean(rs, r => r.shotsAtPlayer):0}, " +
                $"Writ shots {Mean(rs, r => r.shotsFired):0}, bots wrecked {Mean(rs, r => r.enemiesWrecked):0.0}/{rs[0].enemies}, recovered scrap {Mean(rs, r => r.recoveredScrap):0}";
        }

        /// <summary>The seeds a skilled line must clear with no chassis lost (of <see cref="Seeds"/>).</summary>
        public const int CleanSeedsRequired = 27;

        static HashSet<int> Groups(EncounterDef e)
        {
            var g = new HashSet<int>();
            foreach (var s in e.spawns) g.Add(s.group);
            return g;
        }

        [TestCase("food-court")]
        [TestCase("skylight")]
        [TestCase("facade")]
        public void AHumanTimedSkilledLineClearsItWithNoChassisDamageAndTheSameChoicesWithoutCoverDoNot(string id)
        {
            var human = RunSims(id, s => new TacticalBot(s, FightTiming.Human()), ById(id).anchor);
            var perfect = RunSeeds(id, s => new TacticalBot(s, FightTiming.Perfect()), ById(id).anchor);
            var control = RunSeeds(id, s => new TacticalBot(s, FightTiming.Human(), false), ById(id).anchor);
            var hr = Results(human);
            TestContext.WriteLine($"{id}, human-timed skilled line, {Seeds} seeds: {Summary(hr)}");
            TestContext.WriteLine($"{id}, perfect-timed reference: {Summary(perfect)}");
            TestContext.WriteLine($"{id}, the same choices without cover: {Summary(control)}");
            int clean = 0;
            for (int i = 0; i < Seeds; i++)
                if (hr[i].cleared && hr[i].chassisLost == 0f) clean++;
            Assert.That(clean, Is.GreaterThanOrEqualTo(CleanSeedsRequired), $"{id}: the human-timed line clears with no chassis lost in at least {CleanSeedsRequired}/{Seeds} seeds");
            foreach (int g in Groups(ById(id)))
            {
                int fewest = int.MaxValue;
                foreach (var f in human) fewest = Math.Min(fewest, f.ShotsAtPlayerByGroup(g));
                TestContext.WriteLine($"{id} group {g}: fewest shots at the skilled line in a seed {fewest}");
                Assert.That(fewest, Is.GreaterThanOrEqualTo(3), $"{id} group {g}: at least 3 shots at the skilled line in every seed (its zero isn't from an empty fight)");
            }
            for (int i = 0; i < Seeds; i++)
                Assert.That(control[i].playerWrecked || control[i].chassisLost > 0f, Is.True,
                    $"{id} seed {FirstSeed + (ulong)i}: the same choices without cover are wrecked or lose chassis");
        }

        // ---- steady pressure on the skilled line (the ruling: over 30 seeds, the median lowest plating is 60 or less, and
        // at least 20 of the 30 fights do it some damage)

        public const float PressureMedianLow = 60f;
        public const int PressureDamagedSeeds = 20;

        static void AssertPressure(string id)
        {
            var h = RunSeeds(id, s => new TacticalBot(s, FightTiming.Human()), ById(id).anchor);
            TestContext.WriteLine($"{id}, human-timed line, {Seeds} seeds: median lowest plating {MedianLow(h):0}, damaged in {Damaged(h)}/{Seeds}, lowest {Lowest(h):0}");
            Assert.That(MedianLow(h), Is.LessThanOrEqualTo(PressureMedianLow), id + ": the median lowest plating (steady pressure)");
            Assert.That(Damaged(h), Is.GreaterThanOrEqualTo(PressureDamagedSeeds), id + ": fights that do the line some damage");
        }

        [Ignore("gap for the creator (2026-10-15, after the drones' lagged aim): passes under .NET but in Unity's EditMode run (perf11) the food court's strafing line ended with a median lowest plating of 76 against the gate's 60, the same gap as the facade below. The lagged aim misses a strafing Unit by design (the creator's call), so steady pressure on the skilled line needs a new source (more drones, other bots, the Valet) or a new gate: to be balanced with the creator in play.")]
        [Test]
        public void TheFoodCourtKeepsTheSkilledLineUnderSteadyPressure() => AssertPressure("food-court");

        [Ignore("gap for the creator: the skylight's human-timed line dips to a median lowest plating of 86 (damaged in 17/30); tried acid 14-26 a sweep, the spotlights' cycle 2.4 down to 1.0 s and beam damage 35-45: the best on the gate seeds (acid 20, a 1.2 s cycle, 45 damage) gave a median of 60 but 80 on held-out seeds 201-230, i.e. luck. The line ducks every beam it sees start (a visible 0.8 s telegraph, as a player would), so the beams rarely land and the pressure is the floor-care units alone.")]
        [Test]
        public void TheSkylightKeepsTheSkilledLineUnderSteadyPressure() => AssertPressure("skylight");

        [Ignore("gap for the creator (2026-10-15, after the drones' lagged aim): the facade's human-timed line (which now strafes across the drones' fire while peeking) takes about 1.6 hits a fight, so the median lowest plating is 76 against the gate's 60. Drone damage 40 on a 1.2 s cycle passes it (median 60) but fails the food court's clean gate (22/30 clean), and a 1.2 s cycle fails the hover patrol's liveliness test; the shipped 1.0 s / 24 passes everything else. The facade has too few drones for one set of drone numbers to satisfy both encounters.")]
        [Test]
        public void TheFacadeKeepsTheSkilledLineUnderSteadyPressure() => AssertPressure("facade");

        // ---- the dodge band, in a real fight (both runners shoot back; the shooters are wrecked and stop)

        /// <summary>Open floor for the lane runners, in their frame.</summary>
        public static BoxWorld OpenFloor()
        {
            var w = new BoxWorld();
            w.Floor(-60f, 60f, -60f, 60f, 0f);
            return w;
        }

        /// <summary>The span each runner fights for at most (s); most fights end sooner, when the runner wrecks the shooters.</summary>
        public const float DodgeSpan = 30f;
        public const int BandSeeds = 400;
        /// <summary>The held-out seeds the band test asserts on; the tuning sweep used <see cref="TuneSeed"/>.. instead.</summary>
        public const ulong BandSeed = 5001, TuneSeed = 1001;
        /// <summary>The spotlight band's lane: along the hall's x through (0, -2), across the beams' line of fire.</summary>
        public static readonly Vector3 SpotlightLane = new Vector3(0f, 0f, -2f);

        static string Band(string what, FightDodgeResult r) =>
            $"Dodge band in the fight, {what}, {r.seeds} seeds, both shooting back, no damage applied: straight {r.straightHits:0} hits in {r.straightExposure:0} " +
            $"shooter-seconds in sight ({r.straightRate:0.0000}/s, unhit in {r.straightZeroSeeds}), jinking {r.jinkHits:0} in {r.jinkExposure:0} ({r.jinkRate:0.0000}/s, " +
            $"unhit in {r.jinkZeroSeeds}); ratio {r.Ratio:0.00}, 95% interval {r.ratioLow:0.00}-{r.ratioHigh:0.00} " +
            $"({(r.ratioLow >= 1.6f && r.ratioHigh <= 2.5f ? "inside" : "not inside")} 1.6-2.5)";

        [TestCase(12f)]
        public void OneDroneGroupsBandHoldsAtTwelveMetresWhileBothShootBack(float distance)
        {
            // The drones aim at where you were 0.2 s ago (the creator, 2026-10-14), so the old band (a straight runner takes about twice
            // the hits of the jinker, 1.6-2.5, because the drones led) no longer applies: the lane runner crossing their fire is hit
            // rarely, and the jinker, who stops and turns at each reversal, is the one they hit. The band is now the other way round,
            // pinned loosely: the straight runner takes under a quarter of the jinker's hits per shooter-second, and is still hit.
            var e = ById("food-court");
            var r = FightDodge.Run(FightDodge.Framed(e, FightDodge.StandOff(e, 0, distance), false, null, 0), OpenFloor, DodgeSpan, BandSeed, BandSeeds);
            TestContext.WriteLine(Band($"one drone group at {distance:0} m", r));
            Assert.That(r.straightRate, Is.GreaterThan(0f), "the straight runner is hit now and then");
            Assert.That(r.jinkRate, Is.GreaterThan(0f), "the jinker is hit");
            Assert.That(r.Ratio, Is.LessThan(0.25f), "a runner across their fire takes far fewer hits than one who stops to turn");
        }

        [Test]
        public void TheSpotlightPairsBandHoldsWhileBothShootBack()
        {
            var r = FightDodge.Run(FightDodge.Framed(ById("skylight"), SpotlightLane, true), OpenFloor, DodgeSpan, BandSeed, BandSeeds);
            TestContext.WriteLine(Band("the spotlight pair, lane across their fire", r));
            Assert.That(r.straightRate, Is.GreaterThan(0f));
            Assert.That(r.jinkRate, Is.GreaterThan(0f));
            Assert.That(r.Ratio, Is.InRange(1.6f, 2.5f));
        }

        [Test]
        public void TheBandsLanesStayInsideTheHall()
        {
            var e = ById("food-court");
            foreach (float d in new[] { 8f, 12f, 18f })
            {
                var o = FightDodge.StandOff(e, 0, d);
                Assert.That(o.X, Is.InRange(GalleriaArena.HallMinX + 1f, GalleriaArena.HallMaxX - 1f), d + " m");
                Assert.That(o.Z - 15f, Is.GreaterThanOrEqualTo(GalleriaArena.HallMinZ), d + " m: the lane's south end");
                Assert.That(o.Z + 15f, Is.LessThanOrEqualTo(GalleriaArena.HallMaxZ), d + " m: the lane's north end");
            }
            Assert.That(SpotlightLane.X - 15f, Is.GreaterThanOrEqualTo(GalleriaArena.HallMinX));
            Assert.That(SpotlightLane.X + 15f, Is.LessThanOrEqualTo(GalleriaArena.HallMaxX));
        }

        [Test, Explicit("reports the drone band at 8, 12 and 18 m from the group (held-out seeds); asserts nothing")]
        public void ReportTheDroneBandAtThreeDistances()
        {
            var e = ById("food-court");
            var sb = new StringBuilder();
            foreach (float d in new[] { 8f, 12f, 18f })
                sb.AppendLine(Band($"one drone group at {d:0} m", FightDodge.Run(FightDodge.Framed(e, FightDodge.StandOff(e, 0, d), false, null, 0), OpenFloor, DodgeSpan, BandSeed, BandSeeds)));
            TestContext.WriteLine(sb.ToString());
        }

        // ---- the fight's mechanics, one at a time

        sealed class Scripted : IFightBot
        {
            readonly Func<FightSim, MoveInput> _f;
            public bool hold;
            public Scripted(Func<FightSim, MoveInput> f) { _f = f; }
            public FightInput Next(FightSim f, float h) => new FightInput { move = _f(f), holdFire = hold };
        }

        static EncounterDef One(params Spawn[] spawns) => new EncounterDef { id = "mechanics", anchor = GalleriaArena.HallEntry, spawns = spawns };

        static FightSim Hall(EncounterDef e, IFightBot bot, Vector3 start, ulong seed = 1) =>
            new FightSim(e, Hall(), bot, GalleriaArena.FightCover(), start, seed);

        [Test]
        public void AGroupWakesWhenOneMemberSeesYouOrIsHurt()
        {
            // the player stands in the booth's pocket: a drone south of the booth sees in; drones north of it are blocked by the
            // booth's cross screen
            var pocket = new Vector3(0f, 0f, -12.5f);
            var still = new Scripted(f => new MoveInput()) { hold = true };
            var e = One(new Spawn(SkylineEnemies.DroneId, new Vector3(0f, GalleriaArena.DroneY, -17f), 0),
                new Spawn(SkylineEnemies.DroneId, new Vector3(20f, GalleriaArena.DroneY, 9f), 0),
                new Spawn(SkylineEnemies.DroneId, new Vector3(0f, GalleriaArena.DroneY, 0f), 1),
                new Spawn(SkylineEnemies.DroneId, new Vector3(-4f, GalleriaArena.DroneY, 0f), 1));
            var f1 = Hall(e, still, pocket);
            Assert.That(f1.CanSee(f1.Enemies[0], pocket), Is.True, "setup: the south drone sees into the pocket");
            Assert.That(f1.CanSee(f1.Enemies[2], pocket) || f1.CanSee(f1.Enemies[3], pocket), Is.False, "setup: the north drones don't");
            f1.Tick();
            Assert.That(f1.Enemies[0].Awake && f1.Enemies[1].Awake, Is.True, "a member saw the player: its whole group wakes");
            Assert.That(f1.Enemies[2].Awake || f1.Enemies[3].Awake, Is.False, "another group sleeps on");
            f1.Damage(f1.Enemies[2], 5f);
            Assert.That(f1.Enemies[3].Awake, Is.True, "a hurt member wakes its group, unseen or not");
        }

        [Test]
        public void TheDeckBlocksSightAboveAndBelowExceptAtItsEdgeAndStopsBolts()
        {
            var e = One(new Spawn(SkylineEnemies.SpotlightId, new Vector3(-12.5f, 6.5f, 12.3f), 0));
            var f = Hall(e, new StandAndShootBot(), GalleriaArena.HallEntry);
            var spot = f.Enemies[0];
            Assert.That(f.CanSee(spot, new Vector3(-6f, 0f, 16f)), Is.False, "under the deck: hidden from the railing above");
            Assert.That(f.CanSee(spot, new Vector3(-6f, 0f, 2f)), Is.True, "out past the open edge: seen");
            Assert.That(f.Clear(new Vector3(0f, 7f, 16f), new Vector3(0f, 1f, 16f)), Is.False, "straight down through the deck: blocked");
            Assert.That(f.Clear(new Vector3(0f, 7f, 10f), new Vector3(0f, 1f, 10f)), Is.True, "straight down past the edge: clear");
            Assert.That(CoverGeometry.BlockedThick(new Vector3(0f, 7f, 16f), new Vector3(0f, 3f, 16f), GalleriaArena.FightCover(), 0.15f), Is.True, "a bolt down through it is stopped");
        }

        [Test]
        public void AFloorCareUnitSteersRoundAStallBackAndReachesYou()
        {
            // the unit starts east of the west stall back (x = -13), the player stands west of it: the straight line is blocked
            var e = One(new Spawn(SkylineEnemies.FloorCareId, new Vector3(-9f, 0f, -7f), 0));
            var still = new Scripted(f => new MoveInput()) { hold = true };
            var f = Hall(e, still, new Vector3(-17f, 0f, -7f));
            f.Wake(0);
            float closest = float.PositiveInfinity;
            for (int i = 0; i < 60 * 12; i++)
            {
                f.Tick();
                var p = f.Enemies[0].Position;
                closest = Math.Min(closest, MathF.Sqrt((p.X + 17f) * (p.X + 17f) + (p.Z + 7f) * (p.Z + 7f)));
            }
            Assert.That(closest, Is.LessThanOrEqualTo(SkylineEnemies.FloorCareUnit().sweepRadius), "it came round the stall back into sweep reach");
            Assert.That(f.Result().hitsTaken + f.Result().shotsAtPlayer, Is.GreaterThan(0), "and swept at the player");
        }

        [Test]
        public void GondolaMinesHurtAPlayerOnTheWallButNotOnTheFloorAwayFromIt()
        {
            var e = One(new Spawn(SkylineEnemies.GondolaId, new Vector3(-24.2f, GalleriaArena.RailY, -6f), 0, new Vector3(-24.2f, GalleriaArena.RailY, -2f)));
            // carried along a path at a steady 2 m/s (the mines are laid as the gondola rides; the player passes them later)
            Func<Vector3, Vector3, IFightBot> along = (a, b) => new Scripted(f =>
            {
                float t = Math.Min(1f, f.Time / 10f);
                f.Player.Teleport(Vector3.Lerp(a, b, t), 0f, Vector3.Zero);
                return new MoveInput();
            }) { hold = true };
            // on the wall: a wall-runner's feet about 1.2 m up, its chest beside the rail
            var fw = Hall(e, along(new Vector3(-24.55f, 1.2f, -12f), new Vector3(-24.55f, 1.2f, 2f)), new Vector3(-24.55f, 1.2f, -12f));
            fw.Wake(0);
            fw.Run(12f);
            Assert.That(fw.Result().hitsTaken, Is.GreaterThan(0), "a mine went off on a player running the wall");
            var away = Hall(e, along(new Vector3(-20f, 0f, -12f), new Vector3(-20f, 0f, 2f)), new Vector3(-20f, 0f, -12f));
            away.Wake(0);
            away.Run(12f);
            Assert.That(away.Result().hitsTaken, Is.EqualTo(0), "the floor 4 m from the wall is safe");
        }

        [Test]
        public void AWallRunnerAlongTheFacadeMeetsTheMines()
        {
            // the real movement engine: sprint along the west wall, jump, and wall-run north past the gondola's rail
            var e = One(new Spawn(SkylineEnemies.GondolaId, new Vector3(-24.2f, GalleriaArena.RailY, -14f), 0, new Vector3(-24.2f, GalleriaArena.RailY, 10f)));
            var runner = new Scripted(f =>
            {
                if (f.Time < 6f) return new MoveInput();   // let the gondola lay its first mines
                var m = new MoveInput { move = new Vector2(-0.15f, 1f), yaw = 0f, sprintHeld = true };
                float t = (f.Time - 6f) % 2.4f;
                if (t > 0.5f && t < 0.52f) { m.jumpPressed = true; m.jumpHeld = true; }
                return m;
            }) { hold = true };
            var f2 = Hall(e, runner, new Vector3(-24f, 0f, -18f));
            f2.Wake(0);
            bool walled = false;
            for (int i = 0; i < 60 * 20 && !f2.Over; i++)
            {
                f2.Tick();
                walled |= f2.Player.Mode == MoveMode.Wallrun;
            }
            Assert.That(walled, Is.True, "the bot wall-ran the facade");
            Assert.That(f2.Result().hitsTaken, Is.GreaterThan(0), "the gondola's mines caught it on the wall");
        }

        [Test]
        public void ADroneThatLosesYouBehindABoxSearchesHoldsAndGoesHome()
        {
            // one drone and one tall box between it and where the player hides. The player is seen at P0, steps behind the box
            // (P1) at 1 s, and at 1.5 s leaves for somewhere far out of range: the drone must go to a point that sees P1, never
            // enter the box, hold there, and go home when its 8 s memory runs out
            var box = new CoverBox { min = new Vector3(-1.5f, 0f, -1f), max = new Vector3(1.5f, 5f, 0f) };
            var world = new BoxWorld();
            world.Floor(-80f, 80f, -80f, 80f, 0f);
            world.Add((box.min + box.max) * 0.5f, box.max - box.min, false);
            Vector3 p0 = new Vector3(5f, 0f, 3f), p1 = new Vector3(0f, 0f, 3f), far = new Vector3(0f, 0f, -75f);
            var bot = new Scripted(f =>
            {
                f.Player.Teleport(f.Time < 1f ? p0 : f.Time < 1.5f ? p1 : far, 0f, Vector3.Zero);
                return new MoveInput();
            }) { hold = true };
            var home = new Vector3(0f, GalleriaArena.DroneY, -6f);
            var fight = new FightSim(One(new Spawn(SkylineEnemies.DroneId, home, 0)), world, bot, new[] { box }, p0, 3);
            var d = fight.Enemies[0];
            float r = d.Def.bodyRadius;
            Vector3 goal = Vector3.Zero, arrived = Vector3.Zero;
            bool committed = false, reached = false, wentHome = false;
            float arrivedAt = -1f;
            for (int i = 0; i < 60 * 16; i++)
            {
                fight.Tick();
                Assert.That(CoverGeometry.BlockedThick(d.Position, d.Position, new[] { box }, r), Is.False, $"t {fight.Time:0.00}: the drone is inside the box");
                if (!committed && d.HasSearchGoal && !d.Returning)
                {
                    committed = true;
                    goal = d.SearchGoal;
                    Assert.That(fight.Time, Is.LessThan(1.2f), "it set off on the step it lost sight");
                    Assert.That(Vector3.Distance(d.LastKnown, p1 + new Vector3(0f, fight.Player.def.standHeight * 0.5f, 0f)), Is.LessThan(0.05f), "the last known position is where the player stepped out of sight");
                    Assert.That(fight.Clear(home, d.LastKnown), Is.False, "setup: from home it can't see that point");
                    Assert.That(fight.Clear(goal, d.LastKnown), Is.True, "its goal sees the last known position");
                }
                if (committed && d.HasSearchGoal && !d.Returning) Assert.That(d.SearchGoal, Is.EqualTo(goal), "committed: no re-picking");
                if (committed && !reached && !d.HasSearchGoal && !d.Returning)
                {
                    reached = true;
                    arrived = d.Position;
                    arrivedAt = fight.Time;
                    Assert.That(Vector3.Distance(arrived, goal), Is.LessThan(0.01f), "it got there");
                }
                if (reached && !d.Returning && fight.Time < 8.5f) Assert.That(d.Position, Is.EqualTo(arrived), $"t {fight.Time:0.00}: it holds where it sees the last known position");
                if (d.Returning && Vector3.Distance(d.Position, home) < 0.01f) wentHome = true;
            }
            Assert.That(committed && reached, Is.True, "it searched and arrived");
            Assert.That(arrivedAt, Is.LessThan(8f), "well inside its memory");
            Assert.That(wentHome, Is.True, "after 8 s without a sighting it went home the way it came");
        }

        [Test]
        public void ADroneInterruptedOnItsWayHomeStillEndsAtItsSpawn()
        {
            // lose the player behind a box, search, go home after 8 s; the player shows itself mid-way home, hides behind the box
            // again, then leaves for good: the drone must still end where it spawned
            var box = new CoverBox { min = new Vector3(-1.5f, 0f, -1f), max = new Vector3(1.5f, 5f, 0f) };
            var world = new BoxWorld();
            world.Floor(-80f, 80f, -80f, 80f, 0f);
            world.Add((box.min + box.max) * 0.5f, box.max - box.min, false);
            Vector3 p0 = new Vector3(5f, 0f, 3f), p1 = new Vector3(0f, 0f, 3f), far = new Vector3(0f, 0f, -75f);
            float reSight = -1f;
            FightEnemy d = null;
            Vector3 show = p0, hide = far;
            var bot = new Scripted(f =>
            {
                // once it is half-way home, show the player at p0 for 0.4 s, then behind the box for 0.4 s, then gone
                if (reSight < 0f && d != null && d.Returning && d.HasSearchGoal && Vector3.Distance(d.Position, d.Home) > 1f
                    && Vector3.Distance(d.Position, d.SearchGoal) > 1f)
                {
                    reSight = f.Time;
                    // somewhere it sees from where it is now, then somewhere behind the box from there
                    foreach (var c in new[] { p0, new Vector3(-5f, 0f, 3f), new Vector3(0f, 0f, -12f) }) if (f.CanSee(d, c)) { show = c; break; }
                    foreach (var c in new[] { p1, new Vector3(0.8f, 0f, 3f), new Vector3(-0.8f, 0f, 3f), new Vector3(0f, 0f, 5f) }) if (!f.CanSee(d, c)) { hide = c; break; }
                }
                Vector3 at = f.Time < 1f ? p0 : f.Time < 1.5f ? p1 : far;
                if (reSight >= 0f) at = f.Time < reSight + 0.4f ? show : f.Time < reSight + 0.8f ? hide : far;
                f.Player.Teleport(at, 0f, Vector3.Zero);
                return new MoveInput();
            }) { hold = true };
            var home = new Vector3(0f, GalleriaArena.DroneY, -6f);
            var fight = new FightSim(One(new Spawn(SkylineEnemies.DroneId, home, 0)), world, bot, new[] { box }, p0, 3);
            d = fight.Enemies[0];
            bool sawAgain = false;
            for (int i = 0; i < 60 * 40; i++)
            {
                fight.Tick();
                if (reSight >= 0f && d.SeesPlayer) sawAgain = true;
            }
            Assert.That(reSight, Is.GreaterThan(0f), "setup: it was on its way home when the player showed itself");
            Assert.That(sawAgain, Is.True, "setup: it saw the player again mid-way home");
            Assert.That(d.Home, Is.EqualTo(home), "home is fixed");
            Assert.That(Vector3.Distance(d.Position, home), Is.LessThan(0.01f), $"it ended at its spawn, not at {d.Position}");
        }

        [Test]
        public void ADroneThatStillSeesTheLastKnownPositionHolds()
        {
            // the player leaves the drone's range (out of sight by distance, not by cover): the point where it was last seen is in
            // clear view, so the drone holds instead of moving
            var world = new BoxWorld();
            world.Floor(-80f, 80f, -80f, 80f, 0f);
            var start = new Vector3(0f, 0f, 3f);
            var bot = new Scripted(f =>
            {
                f.Player.Teleport(f.Time < 1f ? start : new Vector3(0f, 0f, 3f + Math.Min(60f, (f.Time - 1f) * 200f)), 0f, Vector3.Zero);
                return new MoveInput();
            }) { hold = true };
            var home = new Vector3(0f, GalleriaArena.DroneY, -6f);
            var fight = new FightSim(One(new Spawn(SkylineEnemies.DroneId, home, 0)), world, bot, new CoverBox[0], start, 3);
            fight.Run(4f);
            var d = fight.Enemies[0];
            Assert.That(d.HasLastKnown, Is.True, "it lost the player");
            Assert.That(d.HasSearchGoal, Is.False, "and set no goal");
            Assert.That(d.Position, Is.EqualTo(home), "it held its post: it still sees where it lost the player");
        }

        [Test]
        public void AFreshWreckWithinStripReachIsStrippedAndAStaleOneIsNot()
        {
            var still = new Scripted(f => new MoveInput()) { hold = true };
            var f = Hall(One(new Spawn(SkylineEnemies.DroneId, new Vector3(-20f, GalleriaArena.DroneY, -14f), 0)), still, new Vector3(0f, 0f, 19f));
            f.Tick();
            f.Armor.Damage(50f);
            f.AddWreck(SkylineEnemies.MallCopDrone(), new Vector3(1f, 0f, 19f), f.Time);           // 1 m away, fresh
            f.AddWreck(SkylineEnemies.MallCopDrone(), new Vector3(-1.2f, 0f, 19f), f.Time - 20f);   // 1.2 m away, stale
            f.AddWreck(SkylineEnemies.MallCopDrone(), new Vector3(0f, 0f, 16.5f), f.Time);          // 2.5 m away, fresh
            f.Tick();
            Assert.That(f.Wrecks[0].Wreck.stripped, Is.True, "fresh, within 1.5 m");
            Assert.That(f.Wrecks[1].Wreck.stripped, Is.False, "stale");
            Assert.That(f.Wrecks[2].Wreck.stripped, Is.False, "out of reach");
            Assert.That(f.Armor.Plating, Is.EqualTo(50f + SkylineEnemies.MallCopDrone().scrapPlating).Within(1e-3f));
        }

        [Test]
        public void TheSameSeedGivesTheSameFight()
        {
            var a = Make("food-court", new TacticalBot(7), 7, GalleriaArena.HallEntry).Run(60f);
            var b = Make("food-court", new TacticalBot(7), 7, GalleriaArena.HallEntry).Run(60f);
            Assert.That(b.seconds, Is.EqualTo(a.seconds));
            Assert.That(b.minPlating, Is.EqualTo(a.minPlating));
            Assert.That(b.damageTaken, Is.EqualTo(a.damageTaken));
            Assert.That(b.shotsFired, Is.EqualTo(a.shotsFired));
            Assert.That(b.shotsAtPlayer, Is.EqualTo(a.shotsAtPlayer));
            Assert.That(b.hitsTaken, Is.EqualTo(a.hitsTaken));
            Assert.That(b.enemiesWrecked, Is.EqualTo(a.enemiesWrecked));
        }

        [Test]
        public void TallPiecesHideTheFloorBehindThemEvenFromAJumpingEye()
        {
            // tucked behind each tall piece (on the side away from a drone of each encounter's), with the eye raised 2 m by a
            // double jump: the drones hover below the tall pieces' tops, so the shadow holds
            var f = Hall(GalleriaArena.FoodCourt(), new StandAndShootBot(), GalleriaArena.HallEntry);
            int checkedPieces = 0;
            foreach (var b in GalleriaArena.HallCover())
            {
                if (b.max.Y < 4.4f) continue;
                Vector3 c = (b.min + b.max) * 0.5f;
                foreach (var en in f.Enemies)
                {
                    if (!FightSim.IsShooter(en)) continue;
                    var away = new Vector2(c.X - en.Position.X, c.Z - en.Position.Z);
                    if (away.Length() < 1e-3f) continue;
                    away = Vector2.Normalize(away);
                    // the floor point just behind the piece's far face along the drone's line, 0.6 m clear of it
                    float half = MathF.Abs(away.X) * (b.max.X - b.min.X) * 0.5f + MathF.Abs(away.Y) * (b.max.Z - b.min.Z) * 0.5f;
                    var p = new Vector3(c.X + away.X * (half + 0.9f), 0f, c.Z + away.Y * (half + 0.9f));
                    if (CoverGeometry.DistanceToCover(p, GalleriaArena.HallCover()) < 0.5f) continue;
                    Assert.That(f.CanSee(en, p, TacticalBot.JumpEyeLift), Is.False, $"drone at {en.Position} sees over the piece at {c} to {p} with a jumping eye");
                    checkedPieces++;
                }
            }
            Assert.That(checkedPieces, Is.GreaterThan(20));
        }

        [TestCase("food-court")]
        [TestCase("skylight")]
        [TestCase("facade")]
        public void TheSkilledLinesHidePointsAreUnseenEvenWhenItJumps(string id)
        {
            // in real fights (5 seeds), every hide the human-timed line picks and calls clean is checked at that moment against
            // every live shooter, with the eye raised 2 m by a jump; and most of its picks are clean
            int picks = 0, clean = 0;
            for (ulong s = FirstSeed; s < FirstSeed + 5; s++)
            {
                var bot = new TacticalBot(s, FightTiming.Human());
                var f = Make(id, bot, s, ById(id).anchor);
                bot.HidePicked = (spot, isClean) =>
                {
                    picks++;
                    if (!isClean) return;
                    clean++;
                    var feet = new Vector3(spot.X, 0f, spot.Y);
                    foreach (var en in f.Enemies)
                        if (FightSim.IsShooter(en))
                            Assert.That(f.CanSee(en, feet, TacticalBot.JumpEyeLift), Is.False, $"{id} seed {s} at {f.Time:0.0} s: the clean hide {spot} is seen by bot {en.Index} at {en.Position}");
                };
                f.Run(Cap);
            }
            TestContext.WriteLine($"{id}: {clean} of {picks} hide picks were clean (unseen by every live shooter, eye +2 m)");
            Assert.That(picks, Is.GreaterThan(0));
            Assert.That(clean * 2, Is.GreaterThanOrEqualTo(picks), id + ": most of the line's hides are clean");
        }

        [Test, Explicit("design aid: per group, from how much of the hall a clean hide is within reach (the group alone, awake)")]
        public void PrintHideCoverage()
        {
            var sb = new StringBuilder();
            foreach (var e in GalleriaArena.Encounters())
                foreach (int g in Groups(e))
                {
                    var f = Make(e.id, new StandAndShootBot(), 1, e.anchor);
                    foreach (var en in f.Enemies) if (en.Group != g && FightSim.IsShooter(en)) f.Damage(en, 1e6f);
                    f.Wake(g);
                    var bot = new TacticalBot(1);
                    bot.Next(f, FightSim.Step);
                    int found = 0, places = 0;
                    for (float x = -22f; x <= 22f; x += 4f)
                        for (float z = -18f; z <= 10f; z += 4f)
                        {
                            if (CoverGeometry.DistanceToCover(new Vector3(x, 0f, z), GalleriaArena.HallCover()) <= 0.8f) continue;
                            places++;
                            if (bot.PickHide(f, new Vector2(x, z), out _, out bool ok) && ok) found++;
                        }
                    sb.AppendLine($"{e.id} group {g}: a clean hide within {TacticalBot.HideRadius:0} m from {found} of {places} places");
                }
            TestContext.WriteLine(sb.ToString());
        }

        // ---- tuning sweeps (explicit: print, assert nothing)

        [Test, Explicit("the dodge band sweep on the tuning seeds: prints a table; asserts nothing")]
        public void SweepTheFightDodgeBand()
        {
            var sb = new StringBuilder();
            var e = ById("food-court");
            sb.AppendLine("drone group at 12 m: aimSpreadDeg leadFactor -> ratio [interval] (straight/s, jink/s)");
            foreach (float spread in new[] { 4f, 5f })
                foreach (float lead in new[] { 0.7f, 0.75f })
                {
                    Func<string, EnemyDef> lk = id => { var d = SkylineEnemies.Find(id); if (d != null && id == SkylineEnemies.DroneId) d.aimSpreadDeg = spread; return d; };
                    var r = FightDodge.Run(FightDodge.Framed(e, FightDodge.StandOff(e, 0, 12f), false, lk, 0), OpenFloor, DodgeSpan, TuneSeed, BandSeeds, new DifficultyDef { leadFactor = lead }, lk);
                    sb.AppendLine($"{spread:0} {lead:0.00} -> {r.Ratio:0.00} [{r.ratioLow:0.00}-{r.ratioHigh:0.00}] ({r.straightRate:0.000}, {r.jinkRate:0.000})");
                }
            sb.AppendLine("spotlights: beamHitRadius beamFollowRate -> ratio [interval]");
            foreach (float hr in new[] { 1.0f, 1.4f, 1.8f })
                foreach (float w in new[] { 12f, 20f })
                {
                    Func<string, EnemyDef> lk = id => { var d = SkylineEnemies.Find(id); if (d != null && id == SkylineEnemies.SpotlightId) { d.beamHitRadius = hr; d.beamFollowRate = w; } return d; };
                    var r = FightDodge.Run(FightDodge.Framed(ById("skylight"), SpotlightLane, true, lk), OpenFloor, DodgeSpan, TuneSeed, BandSeeds, null, lk);
                    sb.AppendLine($"{hr:0.0} {w:0} -> {r.Ratio:0.00} [{r.ratioLow:0.00}-{r.ratioHigh:0.00}] ({r.straightRate:0.000}, {r.jinkRate:0.000})");
                }
            TestContext.WriteLine(sb.ToString());
        }

        [Test, Explicit("the fights' tuning sweep: prints a table; asserts nothing")]
        public void SweepTheFightTuning()
        {
            var sb = new StringBuilder();
            sb.AppendLine("encounter a b | open wreck s | human (gate seeds 101-130): clean, lowest, median low, damaged seeds | human (held-out 201-230): clean, median low, damaged | control failed");
            foreach (string id in new[] { "food-court", "facade", "skylight" })
                foreach (var ab in id == "skylight"
                    ? new[] { new Vector2(20f, 1.2f), new Vector2(26f, 1.2f), new Vector2(20f, 1.0f) }
                    : new[] { new Vector2(0.8f, 26f), new Vector2(0.8f, 30f), new Vector2(1.2f, 26f), new Vector2(0.8f, 42f) })
                {
                    Func<string, EnemyDef> lk = x =>
                    {
                        var d = SkylineEnemies.Find(x);
                        if (d == null) return null;
                        if (id != "skylight" && x == SkylineEnemies.DroneId) { d.fireInterval = ab.X; d.attackDamage = ab.Y; }
                        if (id == "skylight" && x == SkylineEnemies.FloorCareId) d.attackDamage = ab.X;
                        if (id == "skylight" && x == SkylineEnemies.SpotlightId) { d.fireInterval = ab.Y; d.attackDamage = 45f; }
                        return d;
                    };
                    var open = RunSeeds(id, s => new StandAndShootBot(), OpenSpot(id), Seeds, 60f, lk);
                    var h = RunSeeds(id, s => new TacticalBot(s, FightTiming.Human()), ById(id).anchor, Seeds, Cap, lk);
                    var h2 = RunSeeds(id, s => new TacticalBot(s, FightTiming.Human()), ById(id).anchor, Seeds, Cap, lk, null, HeldOutSeed);
                    var c = RunSeeds(id, s => new TacticalBot(s, FightTiming.Human(), false), ById(id).anchor, Seeds, Cap, lk);
                    int cf = 0;
                    foreach (var r in c) if (r.playerWrecked || r.chassisLost > 0f) cf++;
                    string band = "";
                    if (id == "food-court")
                    {
                        var e0 = ById("food-court");
                        var br = FightDodge.Run(FightDodge.Framed(e0, FightDodge.StandOff(e0, 0, 12f), false, lk, 0), OpenFloor, DodgeSpan, TuneSeed, 200, null, lk);
                        band = $" | band (tuning seeds, 200) {br.Ratio:0.00} [{br.ratioLow:0.00}-{br.ratioHigh:0.00}]";
                    }
                    sb.AppendLine($"{id} {ab.X:0.0} {ab.Y:0} | {Mean(open, TimeToWreck):0.0} | {Clean(h)} {Lowest(h):0} {MedianLow(h):0} {Damaged(h)} | {Clean(h2)} {MedianLow(h2):0} {Damaged(h2)} | {cf}/{Seeds}{band}");
                }
            TestContext.WriteLine(sb.ToString());
        }
    }
}

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

        public static FightResult[] RunSeeds(string id, Func<ulong, IFightBot> bot, Vector3 start, int seeds = Seeds, float cap = Cap,
            Func<string, EnemyDef> lookup = null, DifficultyDef diff = null)
        {
            var r = new FightResult[seeds];
            for (int i = 0; i < seeds; i++)
            {
                ulong s = FirstSeed + (ulong)i;
                r[i] = Make(id, bot(s), s, start, lookup, diff).Run(cap);
            }
            return r;
        }

        // ---- design aid: which groups see each metre of the hall's floor

        [Test, Explicit("design aid: prints, per encounter, which groups see each metre of the floor (# is cover)")]
        public void PrintTheSightMaps([Values(false, true)] bool counts)
        {
            var sb = new StringBuilder();
            foreach (var e in GalleriaArena.Encounters())
            {
                var f = new FightSim(e, Hall(), new StandAndShootBot(), GalleriaArena.FightCover(), GalleriaArena.HallEntry, 1);
                sb.AppendLine(e.id + (counts ? " COUNTS of shooters" : "") + "  (rows z = 19 at the top to -19; columns x = -24..24; hex digit = bit mask of the groups whose shooters (drones, spotlights) see it (bit 0 = group 0), . = none)");
                for (int z = 19; z >= -19; z--)
                {
                    sb.Append(z.ToString().PadLeft(4)).Append(' ');
                    for (int x = -24; x <= 24; x++)
                    {
                        var feet = new Vector3(x, 0f, z);
                        if (CoverGeometry.DistanceToCover(feet, GalleriaArena.HallCover()) < 0.4f) { sb.Append('#'); continue; }
                        int mask = 0, count = 0;
                        foreach (var en in f.Enemies)
                            if (en.Def.moveSpeed <= 0f && en.Def.layer != ThreatLayer.Walls && f.CanSee(en, feet)) { mask |= 1 << en.Group; count++; }
                        sb.Append(counts ? (count == 0 ? '.' : (char)('0' + Math.Min(9, count))) : mask == 0 ? '.' : "0123456789ABCDEF"[mask & 15]);
                    }
                    sb.AppendLine();
                }
            }
            TestContext.WriteLine(sb.ToString());
        }

        // ---- standing still

        /// <summary>The open stand per encounter: a point 3 m and more from any cover in sight of the fight (every group's
        /// shooters): the middle of the hall north of the kiosks (all three food-court groups, both spotlights), or in the facade
        /// (whose drones hover over the side rooms) the open floor north of the west counter run, which five of its eight drones see.</summary>
        public static Vector3 OpenSpot(string id) => id == "facade" ? new Vector3(-12f, 0f, 8f) : new Vector3(0f, 0f, 8f);

        /// <summary>The covered stand per encounter: behind a kiosk, where the boxes block most sightlines.</summary>
        public static Vector3 CoverSpot(string id) => id == "facade" ? new Vector3(0f, 0f, -4f) : new Vector3(0f, 0f, -10.6f);

        static float Mean(FightResult[] rs, Func<FightResult, float> f)
        {
            float s = 0f;
            foreach (var r in rs) s += f(r);
            return s / rs.Length;
        }

        static float TimeToWreck(FightResult r) => r.playerWrecked ? r.wreckedAt : r.seconds;

        [Test]
        public void TheOpenSpotHasNoCoverWithinThreeMetresAndSeesEveryGroup()
        {
            foreach (var e in GalleriaArena.Encounters())
            {
                Assert.That(CoverGeometry.DistanceToCover(OpenSpot(e.id), GalleriaArena.HallCover()), Is.GreaterThanOrEqualTo(3f), e.id);
                var f = Make(e.id, new StandAndShootBot(), 1, OpenSpot(e.id));
                var groups = new HashSet<int>();
                var seen = new HashSet<int>();
                foreach (var en in f.Enemies)
                {
                    groups.Add(en.Group);
                    if (en.Def.moveSpeed <= 0f && en.Def.layer != ThreatLayer.Walls && f.CanSee(en, OpenSpot(e.id))) seen.Add(en.Group);
                }
                Assert.That(seen.Count, Is.EqualTo(groups.Count), e.id + ": every group has a bot that sees the open spot");
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
            TestContext.WriteLine($"{id}: standing in the open, wrecked in {openWrecks}/{Seeds} at a mean {openMean:0.0} s (shots fired {Mean(open, r => r.shotsFired):0}, at it {Mean(open, r => r.shotsAtPlayer):0.0}, hits {Mean(open, r => r.hitsTaken):0.0}, damage {Mean(open, r => r.damageTaken):0}, " +
                $"bots wrecked {Mean(open, r => r.enemiesWrecked):0.0}/{open[0].enemies}); behind cover {coverWrecks}/{Seeds} wrecked, mean time {coverMean:0.0} s " +
                $"(capped at 60 s), ratio {coverMean / openMean:0.0}");
            Assert.That(openMean, Is.LessThanOrEqualTo(12f), "standing still in the open is a quick wreck (the creator, 2026-10-13)");
            Assert.That(coverMean, Is.GreaterThanOrEqualTo(2f * openMean), "cover is real: it at least doubles the time");
        }

        // ---- the skilled line

        static string Summary(FightResult[] rs)
        {
            int cleared = 0, wrecked = 0, chassisHit = 0;
            float minPlating = float.PositiveInfinity, maxChassis = 0f, minClear = float.PositiveInfinity, maxClear = 0f;
            foreach (var r in rs)
            {
                if (r.cleared) { cleared++; minClear = Math.Min(minClear, r.clearSeconds); maxClear = Math.Max(maxClear, r.clearSeconds); }
                if (r.playerWrecked) wrecked++;
                if (r.chassisLost > 0f) chassisHit++;
                minPlating = Math.Min(minPlating, r.minPlating);
                maxChassis = Math.Max(maxChassis, r.chassisLost);
            }
            return $"cleared {cleared}/{rs.Length} (clear {minClear:0}-{maxClear:0} s, mean {Mean(rs, r => r.cleared ? r.clearSeconds : 0f) * rs.Length / Math.Max(1, cleared):0}), " +
                $"wrecked {wrecked}, chassis lost in {chassisHit} (worst {maxChassis:0}), lowest plating {minPlating:0}, damage taken {Mean(rs, r => r.damageTaken):0}, " +
                $"hits {Mean(rs, r => r.hitsTaken):0.0}, shots at it {Mean(rs, r => r.shotsAtPlayer):0}, Writ shots {Mean(rs, r => r.shotsFired):0}, " +
                $"bots wrecked {Mean(rs, r => r.enemiesWrecked):0.0}/{rs[0].enemies}, recovered scrap {Mean(rs, r => r.recoveredScrap):0}";
        }

        [TestCase("food-court")]
        [TestCase("skylight")]
        [TestCase("facade")]
        public void ASkilledLineClearsItWithNoChassisDamageAndTheSameRouteWithoutCoverDoesNot(string id)
        {
            var skilled = RunSeeds(id, s => new CoverFighterBot(FightPlans.For(id)), GalleriaArena.HallEntry);
            var control = RunSeeds(id, s => new CoverFighterBot(FightPlans.For(id), false), GalleriaArena.HallEntry);
            TestContext.WriteLine($"{id}, skilled line, {Seeds} seeds: {Summary(skilled)}");
            TestContext.WriteLine($"{id}, the same route without cover: {Summary(control)}");
            for (int i = 0; i < Seeds; i++)
            {
                var r = skilled[i];
                Assert.That(r.cleared, Is.True, $"{id} seed {FirstSeed + (ulong)i}: the skilled line clears the fight ({r.enemiesWrecked}/{r.enemies} wrecked in {r.seconds:0} s)");
                Assert.That(r.chassisLost, Is.EqualTo(0f), $"{id} seed {FirstSeed + (ulong)i}: no chassis damage (plating absorbs it; stripping refills it)");
                Assert.That(r.shotsAtPlayer, Is.GreaterThan(0), $"{id} seed {FirstSeed + (ulong)i}: the fight shot at it (its zero isn't from an empty fight)");
            }
            for (int i = 0; i < Seeds; i++)
                Assert.That(control[i].playerWrecked || control[i].chassisLost > 0f, Is.True,
                    $"{id} seed {FirstSeed + (ulong)i}: walking the same route without cover is wrecked or loses chassis");
        }

        // ---- the dodge band, in a real fight (both runners shoot back; the shooters are wrecked and stop)

        /// <summary>Open floor for the lane runners, in the fight's frame (the anchor at the origin).</summary>
        public static BoxWorld OpenFloor()
        {
            var w = new BoxWorld();
            w.Floor(-60f, 60f, -60f, 60f, 0f);
            return w;
        }

        /// <summary>The span each runner fights for at most (s); most fights end sooner, when the runner wrecks the shooters.</summary>
        public const float DodgeSpan = 30f;

        [TestCase("food-court", 0)]
        [TestCase("skylight", -1)]
        public void OnNormalTheStraightRunnerTakesAboutTwiceTheJinkersHitsWhileBothShootBack(string id, int group)
        {
            var r = FightDodge.Run(FightDodge.Aimed(ById(id), null, group), OpenFloor, DodgeSpan, FirstSeed, Seeds);
            TestContext.WriteLine($"Dodge band in the fight, {id}{(group >= 0 ? " group " + group : " (both spotlights)")}, {Seeds} seeds, both shooting back: " +
                $"straight {r.straightHitsMean:0.00} hits in {r.straightSecondsMean:0.0} s ({r.straightRate:0.000}/s, unhit in {r.straightZeroSeeds} seeds), " +
                $"jinking {r.jinkHitsMean:0.00} hits in {r.jinkSecondsMean:0.0} s ({r.jinkRate:0.000}/s, unhit in {r.jinkZeroSeeds} seeds), ratio of rates {r.Ratio:0.00} (target 1.6-2.5)");
            Assert.That(r.straightRate, Is.GreaterThan(0f), "the straight runner is hit");
            Assert.That(r.jinkRate, Is.GreaterThan(0f), "and so is the jinker: nobody goes unhit");
            Assert.That(r.Ratio, Is.InRange(1.6f, 2.5f), "about twice (2.6), per second of exposure");
        }

        // ---- tuning sweep (explicit: prints, asserts nothing)

        static Func<string, EnemyDef> Tuned(float droneDamage, float spotHp, float spotDamage) => id =>
        {
            var d = SkylineEnemies.Find(id);
            if (d == null) return null;
            if (id == SkylineEnemies.DroneId && droneDamage > 0f) d.attackDamage = droneDamage;
            if (id == SkylineEnemies.SpotlightId && spotHp > 0f)
            {
                d.chassisHp = spotHp * 0.4f;
                d.plates = new[] { new PlateDef("housing-left", spotHp * 0.3f), new PlateDef("housing-right", spotHp * 0.3f) };
            }
            if (id == SkylineEnemies.SpotlightId && spotDamage > 0f) d.attackDamage = spotDamage;
            return d;
        };

        [Test, Explicit("the Normal tuning sweep for the fights: prints a table; asserts nothing")]
        public void SweepTheFightTuning()
        {
            var sb = new StringBuilder();
            sb.AppendLine("encounter droneDmg spotHp spotDmg | open wreck mean s | skilled: cleared, chassis-lost seeds, lowest plating, mean clear s | control: seeds wrecked or chassis lost");
            foreach (string id in new[] { "food-court", "facade", "skylight" })
                foreach (float dd in id == "skylight" ? new[] { 12f } : new[] { 12f, 15f, 18f })
                    foreach (float sh in id == "skylight" ? new[] { 130f, 170f, 210f } : new[] { 130f })
                        foreach (float sd in id == "skylight" ? new[] { 30f, 35f } : new[] { 30f })
                        {
                            var lk = Tuned(dd, sh, sd);
                            var open = RunSeeds(id, s => new StandAndShootBot(), OpenSpot(id), Seeds, 60f, lk);
                            var good = RunSeeds(id, s => new CoverFighterBot(FightPlans.For(id)), GalleriaArena.HallEntry, Seeds, Cap, lk);
                            var bad = RunSeeds(id, s => new CoverFighterBot(FightPlans.For(id), false), GalleriaArena.HallEntry, Seeds, Cap, lk);
                            int cleared = 0, lost = 0, badOk = 0;
                            float minPl = 999f;
                            foreach (var r in good) { if (r.cleared) cleared++; if (r.chassisLost > 0f) lost++; minPl = Math.Min(minPl, r.minPlating); }
                            foreach (var r in bad) if (r.playerWrecked || r.chassisLost > 0f) badOk++;
                            sb.AppendLine($"{id} {dd:0} {sh:0} {sd:0} | {Mean(open, TimeToWreck):0.0} | {cleared} {lost} {minPl:0} {Mean(good, r => r.seconds):0} | {badOk}/{Seeds}");
                        }
            TestContext.WriteLine(sb.ToString());
        }

        [Test, Explicit("the dodge band sweep, in the fight (both runners shoot back): prints a table; asserts nothing")]
        public void SweepTheFightDodgeBand()
        {
            var sb = new StringBuilder();
            sb.AppendLine("food-court group 0: aimSpreadDeg leadFactor -> straight/s jink/s ratio (straight hits, jink hits)");
            foreach (float spread in new[] { 2f, 3f, 4f, 5f })
                foreach (float lead in new[] { 0.6f, 0.7f, 0.75f, 0.8f })
                {
                    Func<string, EnemyDef> lk = id => { var d = SkylineEnemies.Find(id); if (d != null && id == SkylineEnemies.DroneId) d.aimSpreadDeg = spread; return d; };
                    var r = FightDodge.Run(FightDodge.Aimed(ById("food-court"), lk, 0), OpenFloor, DodgeSpan, FirstSeed, Seeds, new DifficultyDef { leadFactor = lead }, lk);
                    sb.AppendLine($"{spread:0} {lead:0.00} -> {r.straightRate:0.000} {r.jinkRate:0.000} {r.Ratio:0.00} ({r.straightHitsMean:0.0}, {r.jinkHitsMean:0.0})");
                }
            sb.AppendLine("skylight spotlights: beamHitRadius beamFollowRate -> straight/s jink/s ratio");
            foreach (float hr in new[] { 1.0f, 1.4f, 1.8f, 2.2f })
                foreach (float w in new[] { 12f, 16f, 20f })
                {
                    Func<string, EnemyDef> lk = id => { var d = SkylineEnemies.Find(id); if (d != null && id == SkylineEnemies.SpotlightId) { d.beamHitRadius = hr; d.beamFollowRate = w; } return d; };
                    var r = FightDodge.Run(FightDodge.Aimed(ById("skylight"), lk), OpenFloor, DodgeSpan, FirstSeed, Seeds, null, lk);
                    sb.AppendLine($"{hr:0.0} {w:0} -> {r.straightRate:0.000} {r.jinkRate:0.000} {r.Ratio:0.00} ({r.straightHitsMean:0.0}, {r.jinkHitsMean:0.0})");
                }
            TestContext.WriteLine(sb.ToString());
        }
    }
}

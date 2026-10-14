using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    public class EncounterTests
    {
        static EncounterDef Make(params Spawn[] spawns) => new EncounterDef { id = "test", anchor = Vector3.Zero, spawns = spawns };

        static string Problems(EncounterDef e, Func<string, EnemyDef> lookup = null) =>
            string.Join(" | ", EncounterRules.Check(e, lookup ?? SkylineEnemies.Find));

        [Test]
        public void TheArenasEncountersPassEveryRule()
        {
            foreach (var e in GalleriaArena.Encounters()) Assert.That(EncounterRules.Check(e, SkylineEnemies.Find), Is.Empty, e.id);
        }

        [Test]
        public void MixingLeadingWithWhereYouAreIsCaught()
        {
            var e = Make(new Spawn(SkylineEnemies.DroneId, new Vector3(-15f, 6f, 0f)), new Spawn(SkylineEnemies.SpotlightId, new Vector3(15f, 9f, 0f)));
            Assert.That(Problems(e), Does.Contain("mixes"));
            e.keepOut = true;
            Assert.That(Problems(e), Does.Not.Contain("mixes"), "a deliberate keep-out zone may mix (2.6)");
        }

        [Test]
        public void ThreeLayersAreTooMany()
        {
            Func<string, EnemyDef> lookup = id => id == "washer"
                ? new EnemyDef { id = "washer", aim = AimModel.Area, layer = ThreatLayer.Walls }
                : SkylineEnemies.Find(id);
            var e = Make(new Spawn(SkylineEnemies.DroneId, new Vector3(-15f, 6f, 0f)), new Spawn(SkylineEnemies.FloorCareId, new Vector3(15f, 0f, 0f)),
                new Spawn("washer", new Vector3(0f, 4f, 15f)));
            Assert.That(Problems(e, lookup), Does.Contain("3 layers"));
        }

        [Test]
        public void SpawnsOnOneBearingAreCaught()
        {
            var e = Make(new Spawn(SkylineEnemies.DroneId, new Vector3(0f, 6f, 15f)), new Spawn(SkylineEnemies.DroneId, new Vector3(1f, 6f, 15f)));
            Assert.That(Problems(e), Does.Contain("bearing"));
            Assert.That(EncounterRules.BearingSpread(e), Is.LessThan(10f));
        }

        [Test]
        public void ASpawnOnTheAnchorHasNoBearing()
        {
            var e = Make(new Spawn(SkylineEnemies.DroneId, new Vector3(0.5f, 6f, 0f)), new Spawn(SkylineEnemies.DroneId, new Vector3(0f, 6f, 15f)));
            Assert.That(EncounterRules.BearingSpread(e), Is.EqualTo(0f));
        }

        [Test]
        public void BearingsWrapTheShortWay()
        {
            var e = Make(new Spawn(SkylineEnemies.DroneId, new Vector3(-10f, 6f, -10f)), new Spawn(SkylineEnemies.DroneId, new Vector3(10f, 6f, -10f)));
            Assert.That(EncounterRules.BearingSpread(e), Is.EqualTo(90f).Within(1e-3f), "-135 and +135 are 90 apart, not 270");
        }

        [Test]
        public void UnknownEnemiesAndEmptyEncountersAreNamed()
        {
            Assert.That(Problems(Make(new Spawn("ghost", new Vector3(5f, 0f, 5f)))), Does.Contain("ghost"));
            Assert.That(Problems(Make()), Does.Contain("no spawns"));
        }

        [Test]
        public void EachArenaEncounterLeavesALayerOpen()
        {
            foreach (var e in GalleriaArena.Encounters())
                Assert.That(EncounterRules.OpenLayers(e, SkylineEnemies.Find), Is.Not.Empty, e.id);
        }

        [Test]
        public void EveryM1BotAppearsInTheArenaAndTheDroneNeverMeetsTheSpotlight()
        {
            var seen = new HashSet<string>();
            foreach (var e in GalleriaArena.Encounters())
            {
                var ids = new HashSet<string>();
                foreach (var s in e.spawns) ids.Add(s.enemy);
                Assert.That(ids.Contains(SkylineEnemies.DroneId) && ids.Contains(SkylineEnemies.SpotlightId), Is.False, e.id);
                seen.UnionWith(ids);
            }
            foreach (var d in SkylineEnemies.All())
            {
                Assert.That(seen.Contains(d.id), Is.True, d.id);
            }
        }

        [Test]
        public void EverySpawnStandsInsideTheHall()
        {
            foreach (var e in GalleriaArena.Encounters())
                foreach (var s in e.spawns)
                {
                    Assert.That(s.position.X, Is.InRange(GalleriaArena.HallMinX, GalleriaArena.HallMaxX), e.id + " " + s.enemy);
                    Assert.That(s.position.Z, Is.InRange(GalleriaArena.HallMinZ, GalleriaArena.HallMaxZ), e.id + " " + s.enemy);
                }
        }

        [Test]
        public void ShippedEncountersAreAnchoredWhereTheyStartAndSpreadWideFromThere()
        {
            // Amendment 5: bearings are measured from the zone's entry point, not the hall's centre. Since judge round 1 each
            // fight has its own anchor along the route (RouteTests); the first is the hall's entry.
            Assert.That(GalleriaArena.Encounters()[0].anchor, Is.EqualTo(GalleriaArena.HallEntry), "the first fight starts at the entry");
            foreach (var e in GalleriaArena.Encounters())
            {
                Assert.That(e.anchor.X, Is.InRange(GalleriaArena.HallMinX, GalleriaArena.HallMaxX), e.id + " anchor");
                Assert.That(e.anchor.Z, Is.InRange(GalleriaArena.HallMinZ, GalleriaArena.HallMaxZ), e.id + " anchor");
                Assert.That(CoverGeometry.DistanceToCover(e.anchor, GalleriaArena.HallCover()), Is.GreaterThanOrEqualTo(1.5f), e.id + ": the anchor stands clear of cover");
                foreach (var s in e.spawns)
                {
                    float dx = s.position.X - e.anchor.X, dz = s.position.Z - e.anchor.Z;
                    Assert.That(MathF.Sqrt(dx * dx + dz * dz), Is.GreaterThanOrEqualTo(12f), e.id + " " + s.enemy + " spawns on top of the entry");
                    Assert.That(s.position.X, Is.InRange(GalleriaArena.HallMinX, GalleriaArena.HallMaxX), e.id + " " + s.enemy);
                    Assert.That(s.position.Z, Is.InRange(GalleriaArena.HallMinZ, GalleriaArena.HallMaxZ), e.id + " " + s.enemy);
                }
                Assert.That(EncounterRules.BearingSpread(e), Is.GreaterThanOrEqualTo(EncounterRules.MinBearingSpread), e.id + " spread");
                Assert.That(EncounterRules.BearingSpread(e), Is.GreaterThanOrEqualTo(110f), e.id + " keeps comfortable margin");
                Assert.That(EncounterRules.OpenLayers(e, SkylineEnemies.Find).Count, Is.GreaterThanOrEqualTo(1), e.id + " leaves a layer open");
            }
        }

        [Test]
        public void TheBoostLedgeStandsClearOfTheWarmupWalls()
        {
            var c = GalleriaArena.BoostLedgeTop;
            float half = GalleriaArena.BoostLedgeSize * 0.5f;
            float clearance = Math.Min(Math.Min(c.X - half - GalleriaArena.WarmupMinX, GalleriaArena.WarmupMaxX - (c.X + half)),
                                       Math.Min(c.Z - half - GalleriaArena.WarmupMinZ, GalleriaArena.WarmupMaxZ - (c.Z + half)));
            Assert.That(clearance, Is.GreaterThanOrEqualTo(GalleriaArena.LedgeWallClearance), "no wall-run reaches it, so only a boost does (6.8)");
        }

        [Test]
        public void NormalLeadsButNotPerfectly()
        {
            var n = Difficulties.Normal();
            Assert.That(n.id, Is.EqualTo("normal"));
            Assert.That(n.leadFactor, Is.InRange(0.3f, 0.95f));
            Assert.That(n.scrapScale, Is.EqualTo(1f));
        }

        [Test]
        public void TheTeachingBeatsCoverM1sRowsOfTheTeachingMap()
        {
            var beats = TeachingBeats.M1();
            bool Has(Speaker s, BeatTrigger t, string enemy = null) =>
                Array.Exists(beats, b => b.speaker == s && b.trigger == t && (enemy == null || b.enemy == enemy));
            Assert.That(Has(Speaker.Dot, BeatTrigger.PracticeBotSeen), Is.True, "Writ's weak spots: Dot, the practice bot");
            Assert.That(Has(Speaker.Dot, BeatTrigger.NearBoostLedge), Is.True, "Breacher's boost: Dot, the ledge");
            Assert.That(Has(Speaker.Dot, BeatTrigger.FirstPlatingBreak), Is.True, "Plating from wrecks: Dot");
            foreach (var d in SkylineEnemies.All())
            {
                Assert.That(Has(Speaker.Concierge, BeatTrigger.FirstSeen, d.id), Is.True, "CONCIERGE's safety notice: " + d.id);
            }
        }

        [Test]
        public void TheSpecsOwnLinesAreVerbatimAndEveryLineFits()
        {
            var beats = TeachingBeats.M1();
            var ids = new HashSet<string>();
            foreach (var b in beats)
            {
                Assert.That(ids.Add(b.id), Is.True, "unique id " + b.id);
                Assert.That(b.text.Length, Is.InRange(1, TeachingBeats.MaxChars), b.id);
            }
            Assert.That(Array.Find(beats, b => b.id == "dot-plating").text, Is.EqualTo("Strip it! Scrap's armour!"));
            Assert.That(Array.Find(beats, b => b.id == "dot-plating").draft, Is.False);
            Assert.That(Array.Find(beats, b => b.id == "concierge-floor-care").text,
                Is.EqualTo("Please note: the floor-care unit operates at ankle height. Guests are advised to jump."));
        }
    }
}

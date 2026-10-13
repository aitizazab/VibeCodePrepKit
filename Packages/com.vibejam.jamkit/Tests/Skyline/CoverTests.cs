using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Movement;
using JamKit.Skyline;
using JamKit.Tests.Movement;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>The hall's cover as data, the segment-against-box geometry, and how the fight uses them (sight, range, stopped bolts).</summary>
    public class CoverTests
    {
        static CoverBox Box(float x0, float y0, float z0, float x1, float y1, float z1) =>
            new CoverBox { min = new Vector3(x0, y0, z0), max = new Vector3(x1, y1, z1) };

        // ---- the geometry

        [Test]
        public void ASegmentThroughTheBoxHitsIt()
        {
            var b = Box(-1f, 0f, 4f, 1f, 2f, 6f);
            Assert.That(CoverGeometry.SegmentHitsBox(new Vector3(0f, 1f, 0f), new Vector3(0f, 1f, 10f), b), Is.True);
            Assert.That(CoverGeometry.SegmentHitsBox(new Vector3(-5f, 0.5f, 0f), new Vector3(3f, 1.5f, 10f), b), Is.True, "slanted");
        }

        [Test]
        public void ASegmentThatMissesOrStopsShortOrPassesOverDoesNot()
        {
            var b = Box(-1f, 0f, 4f, 1f, 2f, 6f);
            Assert.That(CoverGeometry.SegmentHitsBox(new Vector3(3f, 1f, 0f), new Vector3(3f, 1f, 10f), b), Is.False, "beside it");
            Assert.That(CoverGeometry.SegmentHitsBox(new Vector3(0f, 1f, 0f), new Vector3(0f, 1f, 3.9f), b), Is.False, "stops short");
            Assert.That(CoverGeometry.SegmentHitsBox(new Vector3(0f, 1f, 7f), new Vector3(0f, 1f, 12f), b), Is.False, "starts past it");
            Assert.That(CoverGeometry.SegmentHitsBox(new Vector3(0f, 2.5f, 0f), new Vector3(0f, 2.5f, 10f), b), Is.False, "over the top");
            Assert.That(CoverGeometry.SegmentHitsBox(new Vector3(0f, 2.5f, 0f), new Vector3(0f, 6f, 10f), b), Is.False, "rising away");
        }

        [Test]
        public void ASegmentStartingInsideOrDownwardThroughTheTopHits()
        {
            var b = Box(-1f, 0f, 4f, 1f, 2f, 6f);
            Assert.That(CoverGeometry.SegmentHitsBox(new Vector3(0f, 1f, 5f), new Vector3(0f, 1f, 20f), b), Is.True, "starts inside");
            Assert.That(CoverGeometry.SegmentHitsBox(new Vector3(0f, 6f, 5f), new Vector3(0f, 0.5f, 5f), b), Is.True, "down through the top");
        }

        [Test]
        public void ADegenerateSegmentIsAPointTest()
        {
            var b = Box(-1f, 0f, 4f, 1f, 2f, 6f);
            Assert.That(CoverGeometry.SegmentHitsBox(new Vector3(0f, 1f, 5f), new Vector3(0f, 1f, 5f), b), Is.True);
            Assert.That(CoverGeometry.SegmentHitsBox(new Vector3(5f, 1f, 5f), new Vector3(5f, 1f, 5f), b), Is.False);
        }

        [Test]
        public void BlockedNeedsOneBoxAndNullOrEmptyBlocksNothing()
        {
            var list = new List<CoverBox> { Box(10f, 0f, 10f, 11f, 1f, 11f), Box(-1f, 0f, 4f, 1f, 2f, 6f) };
            Assert.That(CoverGeometry.Blocked(new Vector3(0f, 1f, 0f), new Vector3(0f, 1f, 10f), list), Is.True);
            Assert.That(CoverGeometry.Blocked(new Vector3(0f, 1f, 0f), new Vector3(0f, 1f, 10f), new List<CoverBox>()), Is.False);
            Assert.That(CoverGeometry.Blocked(new Vector3(0f, 1f, 0f), new Vector3(0f, 1f, 10f), null), Is.False);
        }

        [Test]
        public void DistanceToCoverIsOnTheFloorPlan()
        {
            var list = new List<CoverBox> { Box(-1f, 0f, 4f, 1f, 2f, 6f) };
            Assert.That(CoverGeometry.DistanceToCover(new Vector3(0f, 0f, 0f), list), Is.EqualTo(4f).Within(1e-4f));
            Assert.That(CoverGeometry.DistanceToCover(new Vector3(4f, 0f, 8f), list), Is.EqualTo(MathF.Sqrt(9f + 4f)).Within(1e-4f), "diagonal from the corner");
            Assert.That(CoverGeometry.DistanceToCover(new Vector3(0f, 0f, 5f), list), Is.EqualTo(0f), "inside");
            Assert.That(CoverGeometry.DistanceToCover(Vector3.Zero, null), Is.EqualTo(float.PositiveInfinity));
        }

        // ---- the hall's cover as data

        [Test]
        public void TheHallHasTenToFourteenPiecesAllInsideTheHallAndNoTallerThanTheMezzanine()
        {
            var cover = GalleriaArena.HallCover();
            // 10-14 since Task 11b's fix round (the controller's ruling: no sealed rooms; splitting comes from more, shorter
            // tall pieces with real gaps, which 6-10 pieces can't give next to the pillars, kiosks and planters)
            Assert.That(cover.Length, Is.InRange(10, 14));
            foreach (var b in cover)
            {
                Assert.That(b.min.X, Is.GreaterThanOrEqualTo(GalleriaArena.HallMinX));
                Assert.That(b.max.X, Is.LessThanOrEqualTo(GalleriaArena.HallMaxX));
                Assert.That(b.min.Z, Is.GreaterThanOrEqualTo(GalleriaArena.HallMinZ));
                Assert.That(b.max.Z, Is.LessThanOrEqualTo(GalleriaArena.HallMaxZ));
                Assert.That(b.min.Y, Is.EqualTo(0f), "stands on the floor");
                Assert.That(b.max.Y, Is.InRange(0.9f, GalleriaArena.MezzanineY));
                Assert.That(b.max.X, Is.GreaterThan(b.min.X));
                Assert.That(b.max.Z, Is.GreaterThan(b.min.Z));
            }
        }

        [Test]
        public void TheHallHasPillarsKiosksAndPlanters()
        {
            int pillars = 0, kiosks = 0, planters = 0;
            foreach (var b in GalleriaArena.HallCover())
            {
                float sx = b.max.X - b.min.X, sz = b.max.Z - b.min.Z, h = b.max.Y;
                if (Math.Abs(h - GalleriaArena.MezzanineY) < 1e-3f && sx <= 1.2f && sz <= 1.2f) pillars++;
                else if (h >= 2f && h <= 2.5f && Math.Max(sx, sz) >= 2.5f) kiosks++;
                else if (h >= 0.9f && h <= 1.1f) planters++;
            }
            Assert.That(pillars, Is.GreaterThanOrEqualTo(3), "mezzanine support pillars, floor to mezzanine");
            Assert.That(kiosks, Is.InRange(2, 3));
            Assert.That(planters, Is.GreaterThanOrEqualTo(2), "low planters for half cover");
        }

        [Test]
        public void TheEntryLaneIsClearForFourMetres()
        {
            Assert.That(CoverGeometry.DistanceToCover(GalleriaArena.HallEntry, GalleriaArena.HallCover()), Is.GreaterThanOrEqualTo(4f));
        }

        [TestCase("food-court")]
        [TestCase("skylight")]
        public void TheFightFrameMovesTheCoverByMinusTheAnchor(string id)
        {
            var e = Array.Find(GalleriaArena.Encounters(), x => x.id == id);
            var world = GalleriaArena.HallCover();
            var fight = EncounterDodge.Cover(e);
            Assert.That(fight.Count, Is.EqualTo(world.Length));
            for (int i = 0; i < world.Length; i++)
            {
                Assert.That(Vector3.Distance(fight[i].min, world[i].min - e.anchor), Is.LessThan(1e-4f));
                Assert.That(Vector3.Distance(fight[i].max, world[i].max - e.anchor), Is.LessThan(1e-4f));
            }
        }

        [Test]
        public void TheEncountersShootersCarryTheEnemiesRangeAndDamage()
        {
            var e = Array.Find(GalleriaArena.Encounters(), x => x.id == "food-court");
            foreach (var s in EncounterDodge.Shooters(e, SkylineEnemies.Find, Difficulties.Normal()))
            {
                Assert.That(s.range, Is.EqualTo(SkylineEnemies.MallCopDrone().range));
                Assert.That(s.damage, Is.EqualTo(SkylineEnemies.MallCopDrone().attackDamage));
            }
        }

        [Test]
        public void EveryPieceStandsThreeMetresFromEverySpawn()
        {
            foreach (var e in GalleriaArena.Encounters())
                foreach (var s in e.spawns)
                    foreach (var b in GalleriaArena.HallCover())
                    {
                        float dx = Math.Max(Math.Max(b.min.X - s.position.X, s.position.X - b.max.X), 0f);
                        float dy = Math.Max(Math.Max(b.min.Y - s.position.Y, s.position.Y - b.max.Y), 0f);
                        float dz = Math.Max(Math.Max(b.min.Z - s.position.Z, s.position.Z - b.max.Z), 0f);
                        float d = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
                        Assert.That(d, Is.GreaterThanOrEqualTo(3f), $"{e.id}: {s.enemy} at {s.position} is {d:0.0} m from a piece");
                    }
        }

        [Test]
        public void NoTwoPiecesOverlap()
        {
            var c = GalleriaArena.HallCover();
            for (int i = 0; i < c.Length; i++)
                for (int j = i + 1; j < c.Length; j++)
                {
                    bool overlap = c[i].min.X < c[j].max.X && c[i].max.X > c[j].min.X && c[i].min.Z < c[j].max.Z && c[i].max.Z > c[j].min.Z;
                    Assert.That(overlap, Is.False, $"pieces {i} and {j} overlap");
                }
        }

        // ---- the fight uses them

        sealed class ScriptBot : IPlayerBot
        {
            readonly Func<float, MoveInput> _f;
            public ScriptBot(Func<float, MoveInput> f) { _f = f; }
            public MoveInput Next(MoveSim sim, float time, float h) => _f(time);
        }

        static CombatSim Fight(IPlayerBot bot, List<Shooter> shooters, IEnumerable<CoverBox> cover, ulong seed = 1)
        {
            var world = new BoxWorld();
            world.Floor(-30f, 30f, -30f, 30f, 0f);
            var sim = new MoveSim(world, new MoveDef());
            sim.Teleport(Vector3.Zero, 0f, Vector3.Zero);
            return new CombatSim(sim, bot, shooters, seed, cover);
        }

        static List<Shooter> Gun(float spread = 0f, float range = float.PositiveInfinity) => new List<Shooter>
        {
            new Shooter { position = new Vector3(0f, 1.5f, 20f), aim = AimModel.FiresAtWhereYouAre, projectileSpeed = 30f, fireInterval = 0.5f,
                aimSpreadDeg = spread, range = range },
        };

        static readonly CoverBox Wall = Box(-3f, 0f, 8f, 3f, 3f, 10f);

        [Test]
        public void AShooterWithNoSightDoesNotFireAndHitsNothing()
        {
            var open = Fight(new StandStillBot(), Gun(), null);
            var hidden = Fight(new StandStillBot(), Gun(), new[] { Wall });
            open.Run(10f);
            hidden.Run(10f);
            Assert.That(open.ShotsFired, Is.GreaterThan(10));
            Assert.That(open.Hits, Is.GreaterThan(10));
            Assert.That(hidden.ShotsFired, Is.EqualTo(0), "it holds its fire without sight");
            Assert.That(hidden.Hits, Is.EqualTo(0));
        }

        [Test]
        public void ARangeShorterThanTheDistanceMeansNoShots()
        {
            var c = Fight(new StandStillBot(), Gun(0f, 15f), null);
            c.Run(10f);
            Assert.That(c.ShotsFired, Is.EqualTo(0), "the chest is 20 m away and the shooter reaches 15");
            var near = Fight(new StandStillBot(), Gun(0f, 25f), null);
            near.Run(10f);
            Assert.That(near.ShotsFired, Is.GreaterThan(10));
        }

        [Test]
        public void AFiringShooterWaitsForSightAndFiresTheMomentItHasIt()
        {
            // behind the wall for the first 3 s, then out from behind it
            var bot = new ScriptBot(t => t < 3f ? new MoveInput() : new MoveInput { move = new Vector2(1f, 0f), yaw = 0f });
            var shooters = Gun();
            var world = new BoxWorld();
            world.Floor(-30f, 30f, -30f, 30f, 0f);
            var sim = new MoveSim(world, new MoveDef());
            sim.Teleport(Vector3.Zero, 0f, Vector3.Zero);
            var c = new CombatSim(sim, bot, shooters, 1, new[] { Wall });
            c.Run(2.9f);
            Assert.That(c.ShotsFired, Is.EqualTo(0));
            c.Run(2f);
            Assert.That(c.ShotsFired, Is.GreaterThan(0), "it fired once the player walked into sight");
        }

        [Test]
        public void AProjectileThatMeetsCoverIsDestroyed()
        {
            // the line of sight is clear (the wall starts to the side of it), but a spread throws bolts into the wall
            var side = Box(0.1f, 0f, 8f, 8f, 3f, 10f);
            var without = Fight(new StandStillBot(), Gun(3f), null, 4);
            var with = Fight(new StandStillBot(), Gun(3f), new[] { side }, 4);
            without.Run(60f);
            with.Run(60f);
            Assert.That(with.ShotsFired, Is.EqualTo(without.ShotsFired), "same shots: sight to the chest is clear in both");
            Assert.That(without.Hits, Is.GreaterThan(0));
            Assert.That(with.Hits, Is.LessThan(without.Hits), "the wall took some of the bolts that would have hit");
        }

        [Test]
        public void ABeamHitsOnlyIfSightHoldsWhenItFires()
        {
            // the beam starts with the player in the open, then the player steps behind the wall and stands still: the
            // beam settles on a standing target, so only the wall can stop the hit
            var wall = Box(0.3f, 0f, 8f, 6f, 3f, 10f);
            Func<IPlayerBot> bot = () => new ScriptBot(t => t < 0.3f ? new MoveInput { move = new Vector2(1f, 0f), yaw = 0f } : new MoveInput());
            Func<List<Shooter>> beam = () => new List<Shooter>
            {
                new Shooter { position = new Vector3(0f, 1.5f, 20f), aim = AimModel.FiresAtWhereYouAre, projectileSpeed = 0f, fireInterval = 100f,
                    firstShotAt = 0.1f, beamTelegraph = 0.8f, beamLockBefore = 0.25f, beamFollowRate = 12f, beamHitRadius = 1f },
            };
            var open = Fight(bot(), beam(), null);
            var behind = Fight(bot(), beam(), new[] { wall });
            open.Run(3f);
            behind.Run(3f);
            Assert.That(open.ShotsFired, Is.EqualTo(1));
            Assert.That(open.Hits, Is.EqualTo(1), "control: a standing target is hit by the beam");
            Assert.That(behind.ShotsFired, Is.EqualTo(1), "the beam started while the player was in sight");
            Assert.That(behind.Hits, Is.EqualTo(0), "sight was gone when it fired");
        }

        [Test]
        public void HitsTakeThePlatingThenTheChassisAndTimeTheWreck()
        {
            var guns = Gun();
            guns[0].damage = 50f;
            guns[0].fireInterval = 1f;
            var c = Fight(new StandStillBot(), guns, null);
            c.Run(10f);
            Assert.That(c.Armor.Wrecked, Is.True);
            Assert.That(c.WreckedAt, Is.InRange(3f, 6f), "four 50 hp hits kill plating 100 over chassis 100");
        }

        // ---- Task 11b: edge cases, thick bolts, solid cover, a fight with cover through the harness

        [Test]
        public void ANonFiniteSegmentCountsAsBlocked()
        {
            var b = Box(-1f, 0f, 4f, 1f, 2f, 6f);
            Assert.That(CoverGeometry.SegmentHitsBox(new Vector3(float.NaN, 1f, 0f), new Vector3(0f, 1f, 10f), b), Is.True, "NaN start");
            Assert.That(CoverGeometry.SegmentHitsBox(new Vector3(30f, 1f, 0f), new Vector3(30f, float.NaN, 10f), b), Is.True, "NaN end, far from the box");
            Assert.That(CoverGeometry.SegmentHitsBox(new Vector3(0f, 1f, float.NegativeInfinity), new Vector3(0f, 1f, 10f), b), Is.True, "infinite end");
            Assert.That(CoverGeometry.Blocked(new Vector3(float.NaN), Vector3.Zero, new[] { b }), Is.True, "no clear sight along an undefined line");
        }

        [Test]
        public void ExactFaceAndEdgeTouchesCount()
        {
            var b = Box(-1f, 0f, 4f, 1f, 2f, 6f);
            Assert.That(CoverGeometry.SegmentHitsBox(new Vector3(0f, 1f, 0f), new Vector3(0f, 1f, 4f), b), Is.True, "ends exactly on the near face");
            Assert.That(CoverGeometry.SegmentHitsBox(new Vector3(1f, 1f, 0f), new Vector3(1f, 1f, 10f), b), Is.True, "slides along a side face (grazing counts)");
            Assert.That(CoverGeometry.SegmentHitsBox(new Vector3(0f, 2f, 0f), new Vector3(0f, 2f, 10f), b), Is.True, "skims the top face");
            Assert.That(CoverGeometry.SegmentHitsBox(new Vector3(-3f, 1f, 2f), new Vector3(1f, 1f, 6f), b), Is.True, "passes exactly through a vertical edge");
            Assert.That(CoverGeometry.SegmentHitsBox(new Vector3(1.0001f, 1f, 0f), new Vector3(1.0001f, 1f, 10f), b), Is.False, "a hair outside the side face");
            Assert.That(CoverGeometry.SegmentHitsBox(new Vector3(0f, 1f, 0f), new Vector3(0f, 1f, 3.9999f), b), Is.False, "a hair short of the face");
        }

        [Test]
        public void ABoltsEdgeIsStoppedWhereItsCentreLineWouldPass()
        {
            var cover = new[] { Box(-1f, 0f, 4f, 1f, 2f, 6f) };
            Vector3 a = new Vector3(1.1f, 1f, 0f), b = new Vector3(1.1f, 1f, 10f);   // the centre line passes 0.1 m beside the box
            Assert.That(CoverGeometry.Blocked(a, b, cover), Is.False, "a sight line there is clear");
            Assert.That(CoverGeometry.BlockedThick(a, b, cover, 0.15f), Is.True, "a 0.15 m bolt clips the box");
            Assert.That(CoverGeometry.BlockedThick(a, b, cover, 0.05f), Is.False, "a 0.05 m bolt slips past");
            Assert.That(CoverGeometry.BlockedThick(a, b, cover, 0f), Is.False, "radius 0 is the centre line");
        }

        [Test]
        public void ARegainedSightIsReStaggeredNotFiredInLockstep()
        {
            // four shooters behind one wall; the player steps out at 3 s: their first shots after that are spread, not one volley
            var shooters = new List<Shooter>();
            for (int i = 0; i < 4; i++)
                shooters.Add(new Shooter { position = new Vector3(-1.5f + i, 1.5f, 20f), aim = AimModel.FiresAtWhereYouAre, projectileSpeed = 30f,
                    fireInterval = 1f, firstShotAt = 0.5f });
            var bot = new ScriptBot(t => t < 3f ? new MoveInput() : new MoveInput { move = new Vector2(1f, 0f), yaw = 0f, sprintHeld = true });
            var c = Fight(bot, shooters, new[] { Box(-6f, 0f, 8f, 6f, 3f, 10f) }, 9);
            var firstShot = new List<int>();
            int last = 0;
            for (int step = 0; step < 6 * 120; step++)
            {
                c.Run(1f / 120f);
                if (c.ShotsFired > last) { for (int k = last; k < c.ShotsFired; k++) firstShot.Add(step); last = c.ShotsFired; }
                if (firstShot.Count >= 4) break;
            }
            Assert.That(firstShot.Count, Is.EqualTo(4), "all four fire once the player is in sight");
            Assert.That(firstShot[3] - firstShot[0], Is.GreaterThan(2), "their first shots come on different steps (re-staggered)");
        }

        [Test]
        public void TheFightsCoverIsSolidForThePlayer()
        {
            // the main hall world of the fight tests: a bot walking into the west stall back (x -13.3..-12.7) is stopped by it
            var world = FightTests.Hall();
            var sim = new MoveSim(world, new MoveDef());
            sim.Teleport(new Vector3(-9f, 0f, -7f), -90f, Vector3.Zero);
            float h = 1f / 60f;
            for (int i = 0; i < 180; i++) sim.Step(new MoveInput { move = new Vector2(0f, 1f), yaw = -90f, sprintHeld = true }, h);
            Assert.That(sim.Position.X, Is.GreaterThan(-12.7f), "it stops at the stall back's east face (x = -12.7), not through it");
            // and the fight refuses a world where the cover isn't solid
            var bare = new BoxWorld();
            bare.Floor(-30f, 30f, -30f, 30f, 0f);
            Assert.Throws<ArgumentException>(() => new FightSim(GalleriaArena.FoodCourt(), bare, new StandAndShootBot(), GalleriaArena.FightCover(),
                GalleriaArena.HallEntry, 1));
        }

        [Test]
        public void AFightWithTheHallsCoverThroughTheHarness()
        {
            // the dodge harness with the hall's cover: a stander behind the approach screen is hit far less than one in the open
            var e = GalleriaArena.FoodCourt();
            Func<List<Shooter>> shooters = () => EncounterDodge.Shooters(e, SkylineEnemies.Find, Difficulties.Normal());
            Func<IMoveWorld> world = () =>
            {
                var w = new BoxWorld();
                w.Floor(-40f, 40f, -40f, 40f, 0f);
                foreach (var b in EncounterDodge.Cover(e)) w.Add((b.min + b.max) * 0.5f, b.max - b.min, false);
                return w;
            };
            var behind = DodgeReport.Fights(world, shooters, s => new StandStillBot(), 20f, 3, 5, EncounterDodge.Cover(e), new Vector3(0f, 0f, 6f));
            var open = DodgeReport.Fights(world, shooters, s => new StandStillBot(), 20f, 3, 5, null, new Vector3(0f, 0f, 6f));
            int hb = 0, ho = 0;
            foreach (var r in behind) hb += r.hits;
            foreach (var r in open) ho += r.hits;
            TestContext.WriteLine($"Harness with cover: behind the screen {hb} hits, the same spot with no cover {ho} (5 seeds, 20 s)");
            Assert.That(ho, Is.GreaterThan(0));
            Assert.That(hb, Is.LessThan(ho / 2), "the hall's cover blocks most of the fire at that spot");
        }
    }
}

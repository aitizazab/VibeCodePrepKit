using System.Collections.Generic;
using System.Numerics;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>Task G1: the gondola outside the glass lays mines on the inside of the wall-run through the façade's twelve service
    /// hatches, and the façade's panes break when shot through (docs/SKYLINE-FLOOR-REPOSSESS.md section 10, decisions D21, D23).</summary>
    public class FacadeGondolaTests
    {
        static readonly Vector3 Far = new Vector3(0f, -100f, 0f);

        static GondolaRail OutsideRail() =>
            new GondolaRail(new Vector3(36.5f, 3f, -27f), new Vector3(36.5f, 3f, 27f), 3f);

        static GondolaRail SimRail() =>
            new GondolaRail(new Vector3(34.9f, 2.5f, -27f), new Vector3(34.9f, 2.5f, 27f), 3f);

        static void Ride(MineLayer layer, GondolaRail rail, float seconds, Vector3 chest, System.Action<MineLayer> each)
        {
            var blasts = new List<float>();
            for (float t = 0f; t < seconds; t += 0.1f)
            {
                rail.Tick(0.1f);
                layer.Tick(0.1f, rail, true, chest, blasts);
                each?.Invoke(layer);
            }
        }

        // ---- the hatches

        [Test]
        public void TheTwelveHatchesSitEveryFourMetresFromMinus22To22AndSnapToTheNearest()
        {
            Assert.That(FacadeHatches.Count, Is.EqualTo(12));
            Assert.That(FacadeHatches.ZOf(0), Is.EqualTo(-22f).Within(1e-4f));
            Assert.That(FacadeHatches.ZOf(11), Is.EqualTo(22f).Within(1e-4f));
            for (int i = 1; i < FacadeHatches.Count; i++)
                Assert.That(FacadeHatches.ZOf(i) - FacadeHatches.ZOf(i - 1), Is.EqualTo(4f).Within(1e-4f));
            Assert.That(FacadeHatches.HatchSnap(-27f), Is.EqualTo(-22f).Within(1e-4f), "the rail's end clamps to the first hatch");
            Assert.That(FacadeHatches.HatchSnap(27f), Is.EqualTo(22f).Within(1e-4f));
            Assert.That(FacadeHatches.HatchSnap(-19f), Is.EqualTo(-18f).Within(1e-4f));
            Assert.That(FacadeHatches.HatchSnap(-20.5f), Is.EqualTo(-22f).Within(1e-4f));
            Assert.That(FacadeHatches.HatchSnap(0.3f), Is.EqualTo(2f).Within(1e-4f).Or.EqualTo(-2f).Within(1e-4f), "a hatch either side of the middle");
            Assert.That(FacadeHatches.HatchSnap(float.NaN), Is.EqualTo(-22f).Within(1e-4f), "never NaN");
        }

        [Test]
        public void TheOutsideRailAndTheSimsInsideCopyAreBothFacadeRailsAndAnOldRailIsNot()
        {
            Assert.That(FacadeHatches.IsFacadeRail(OutsideRail()), Is.True);
            Assert.That(FacadeHatches.IsFacadeRail(SimRail()), Is.True);
            Assert.That(FacadeHatches.IsFacadeRail(null), Is.False);
            Assert.That(FacadeHatches.IsFacadeRail(new GondolaRail(new Vector3(0f, 10f, 0f), new Vector3(0f, 10f, 50f), 3f)), Is.False);
            Assert.That(FacadeHatches.IsFacadeRail(new GondolaRail(new Vector3(36.5f, 3f, 0f), new Vector3(36.5f, 3f, 10f), 3f)), Is.False, "too short");
        }

        // ---- the mines

        [Test]
        public void MinesLandOnlyOnTheHatchesInsideTheGlass()
        {
            foreach (var mk in new System.Func<GondolaRail>[] { OutsideRail, SimRail })
            {
                var layer = new MineLayer(SkylineEnemies.Gondola());
                var rail = mk();
                int seen = 0;
                Ride(layer, rail, 120f, Far, l =>
                {
                    foreach (var m in l.Mines)
                    {
                        seen++;
                        Assert.That(m.Position.X, Is.EqualTo(FacadeHatches.MineX).Within(1e-3f), "inside the glass (x 35), not on the rail");
                        Assert.That(m.Position.Y, Is.EqualTo(FacadeHatches.MineY).Within(1e-3f));
                        Assert.That(FacadeHatches.HatchSnap(m.Position.Z), Is.EqualTo(m.Position.Z).Within(1e-3f), "on a hatch");
                        Assert.That(FacadeHatches.IsHatchPoint(m.Position), Is.True);
                    }
                });
                Assert.That(seen, Is.GreaterThan(0));
            }
        }

        [Test]
        public void SpacingLifetimeAndCapAreUnchangedAndNoTwoLiveMinesShareAHatch()
        {
            var def = SkylineEnemies.Gondola();
            var layer = new MineLayer(def);
            var rail = OutsideRail();
            var everSeen = new HashSet<WallMine>();
            float maxAge = 0f;
            int maxLive = 0;
            Ride(layer, rail, 60f, Far, l =>
            {
                maxLive = System.Math.Max(maxLive, l.Mines.Count);
                var used = new HashSet<int>();
                foreach (var m in l.Mines)
                {
                    everSeen.Add(m);
                    maxAge = System.Math.Max(maxAge, m.Age);
                    Assert.That(used.Add(FacadeHatches.NearestIndex(m.Position.Z)), Is.True, "one mine to a hatch");
                }
            });
            Assert.That(maxLive, Is.LessThanOrEqualTo(def.mineMax));
            Assert.That(maxLive, Is.EqualTo(def.mineMax), "the cap is reached on a long ride");
            Assert.That(maxAge, Is.LessThanOrEqualTo(def.mineLifetime + 0.3f), "none outlives 12 s");
            // 60 s at 3 m/s is 180 m: a mine every 4 m would be 45; the ping-pong's ends skip the repeats at a hatch that is still live
            Assert.That(everSeen.Count, Is.InRange(30, 45));
        }

        [Test]
        public void ARailThatIsNotTheFacadeKeepsTheOldBehaviourAndTheOffsetMovesItsMines()
        {
            var def = SkylineEnemies.Gondola();
            var rail = new GondolaRail(new Vector3(0f, 10f, 0f), new Vector3(0f, 10f, 60f), 4f);
            var layer = new MineLayer(def);
            Ride(layer, rail, 3f, Far, null);   // 12 m: mines at 4, 8, 12
            Assert.That(layer.Mines.Count, Is.InRange(2, 3));
            Assert.That(layer.Mines[0].Position.Y, Is.EqualTo(10f).Within(1e-3f), "on the rail itself");
            Assert.That(layer.Mines[0].Position.X, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(layer.Mines[0].Position.Z, Is.EqualTo(4.4f).Within(0.2f), "a spacing from where it started, not snapped to a hatch");

            var shifted = new MineLayer(def) { mineOffset = new Vector3(-1.6f, -0.5f, 0f) };
            Ride(shifted, new GondolaRail(new Vector3(0f, 10f, 0f), new Vector3(0f, 10f, 60f), 4f), 3f, Far, null);
            Assert.That(shifted.Mines[0].Position.X, Is.EqualTo(-1.6f).Within(1e-3f));
            Assert.That(shifted.Mines[0].Position.Y, Is.EqualTo(9.5f).Within(1e-3f));
        }

        [Test]
        public void ThePlacementCanBeForcedEitherWay()
        {
            var def = SkylineEnemies.Gondola();
            var onRail = new MineLayer(def) { placement = MinePlacement.OnRail };
            Ride(onRail, OutsideRail(), 3f, Far, null);
            Assert.That(onRail.Mines[0].Position.X, Is.EqualTo(36.5f).Within(1e-3f), "forced onto the rail");
            var facade = new MineLayer(def) { placement = MinePlacement.Facade };
            Ride(facade, new GondolaRail(new Vector3(0f, 10f, -9f), new Vector3(0f, 10f, 9f), 4f), 3f, Far, null);
            Assert.That(FacadeHatches.IsHatchPoint(facade.Mines[0].Position), Is.True);
        }

        [Test]
        public void AHatchMineBlastsInsideTheHallOnlyWhenTheChestIsClose()
        {
            var def = SkylineEnemies.Gondola();
            var at = FacadeHatches.MinePoint(5);
            var near = new Vector3(at.X - 1.2f, at.Y, at.Z);   // inside the 1.5 m trigger and the 2.5 m blast
            var gone = new Vector3(at.X - 3.5f, at.Y, at.Z);   // outside the blast
            var hit = new WallMine(at, def);
            var dodged = new WallMine(at, def);
            hit.Tick(def.mineArmSeconds + 0.05f, Far, out _);
            dodged.Tick(def.mineArmSeconds + 0.05f, Far, out _);
            hit.Tick(0.05f, near, out _);
            dodged.Tick(0.05f, near, out _);
            Assert.That(hit.State, Is.EqualTo(MineState.Fusing));
            float damage = 0f, dodgedDamage = 0f;
            for (int i = 0; i < 10; i++)
            {
                if (hit.Tick(0.1f, near, out float d)) damage = d;
                if (dodged.Tick(0.1f, gone, out float e)) dodgedDamage = e;   // it left the wall in the fuse's time
            }
            Assert.That(hit.State, Is.EqualTo(MineState.Exploded));
            Assert.That(damage, Is.EqualTo(25f).Within(1e-4f));
            Assert.That(dodged.State, Is.EqualTo(MineState.Exploded));
            Assert.That(dodgedDamage, Is.EqualTo(0f).Within(1e-4f));
        }

        // ---- sight through the glass

        [Test]
        public void TheGondolaOutsideSeesAUnitOnTheWallRunThroughTheGlass()
        {
            var blockers = RepossessArena.SightBlockers();
            for (float z = -26f; z <= 26f; z += 2f)
            {
                var gondola = new Vector3(FacadeHatches.RailX, FacadeHatches.RailY + 0.2f, z);
                bool landing = z > RepossessArena.FacadeLandingMinZ - 2f && z < RepossessArena.FacadeLandingMaxZ + 1f;
                float y = landing ? RepossessArena.FacadeLandingTop + 1.25f : 2.5f;
                var unit = new Vector3(34.3f, y, z + 1f);
                foreach (var b in blockers)
                    Assert.That(CoverGeometry.SegmentHitsBox(gondola, unit, b), Is.False, "z " + z + " blocked by " + b.min + " " + b.max);
                Assert.That(CoverGeometry.Blocked(gondola, unit, blockers), Is.False, "the gondola sees a Unit on the wall-run at z " + z);
                Assert.That(CoverGeometry.Blocked(unit, gondola, blockers), Is.False, "and the Unit sees it");
            }
        }

        // ---- the glass

        [Test]
        public void ThePaneAtAWallPointIsTheOneTheDataCallsThat()
        {
            Assert.That(FacadeGlass.PaneAt(1f, -27.9f), Is.EqualTo(RepossessArena.PaneId(0, 0)));
            Assert.That(FacadeGlass.PaneAt(5f, 0.1f), Is.EqualTo(RepossessArena.PaneId(1, 10)));
            Assert.That(FacadeGlass.PaneAt(14f, 27.9f), Is.EqualTo(RepossessArena.PaneId(3, 19)), "the top row is 3.6 m");
            Assert.That(FacadeGlass.PaneAt(2f, 28.2f), Is.Null, "the posts are not glass");
            Assert.That(FacadeGlass.PaneAt(-1f, 0f), Is.Null);
            Assert.That(FacadeGlass.PaneAt(16f, 0f), Is.Null);
            Assert.That(FacadeGlass.PaneAt(float.NaN, 0f), Is.Null);
            // every id it returns is a real solid of the arena, and the pane's box contains the point it was asked about
            for (float y = 0.2f; y < 15.5f; y += 1.7f)
                for (float z = -27.8f; z < 27.9f; z += 3.1f)
                {
                    var s = RepossessArena.Find(FacadeGlass.PaneAt(y, z));
                    Assert.That(s, Is.Not.Null);
                    Assert.That(z, Is.InRange(s.box.min.Z - 1e-3f, s.box.max.Z + 1e-3f));
                    Assert.That(y, Is.InRange(s.box.min.Y - 1e-3f, s.box.max.Y + 1e-3f));
                }
        }

        [Test]
        public void AShotAcrossTheGlassPlaneBreaksThePaneItCrossesAndOnlyThat()
        {
            Assert.That(FacadeGlass.PaneCrossed(new Vector3(30f, 2f, 0.1f), new Vector3(40f, 2f, 0.1f)), Is.EqualTo(RepossessArena.PaneId(0, 10)));
            Assert.That(FacadeGlass.PaneCrossed(new Vector3(40f, 6f, -5f), new Vector3(30f, 6f, -5f)), Is.EqualTo(RepossessArena.PaneId(1, 8)), "from outside too");
            Assert.That(FacadeGlass.PaneCrossed(new Vector3(10f, 2f, 0f), new Vector3(30f, 2f, 0f)), Is.Null, "stops short of the glass");
            Assert.That(FacadeGlass.PaneCrossed(new Vector3(30f, 2f, 0f), new Vector3(30f, 2f, 9f)), Is.Null, "parallel to the glass");
        }

        [Test]
        public void ABrokenPaneIsBrokenOnceMendsAndRejectsIdsThatAreNotPanes()
        {
            var g = new FacadeGlass();
            string id = RepossessArena.PaneId(1, 5);
            Assert.That(g.IsBroken(id), Is.False);
            Assert.That(g.Break(id), Is.True);
            Assert.That(g.Break(id), Is.False, "already broken");
            Assert.That(g.IsBroken(id), Is.True);
            Assert.That(g.Break("facade-post-n"), Is.False);
            Assert.That(g.Break("facade-pane-9-9"), Is.False);
            Assert.That(g.Break(null), Is.False);
            Assert.That(g.BrokenCount, Is.EqualTo(1));
            var ids = new List<string>();
            g.BrokenIds(ids);
            Assert.That(ids, Is.EqualTo(new[] { id }));
            g.MendAll();
            Assert.That(g.BrokenCount, Is.EqualTo(0));
            Assert.That(g.IsBroken(id), Is.False);
        }

        [Test]
        public void TheOpenEdgeCutsAUnitInTheGapOncePerIntervalAndNotOneFarAway()
        {
            var g = new FacadeGlass();
            string id = RepossessArena.PaneId(0, 10);   // z -0..2.8 (-28 + 28), y 0..4
            var inGap = new Vector3(34.6f, 2f, 1.4f);
            var away = new Vector3(34.6f, 2f, 10f);
            Assert.That(g.Tick(0.1f, inGap), Is.EqualTo(0f), "glass whole: no edge");
            g.Break(id);
            Assert.That(g.InGap(inGap), Is.True);
            Assert.That(g.InGap(away), Is.False);
            Assert.That(g.InGap(new Vector3(20f, 2f, 1.4f)), Is.False, "deep in the hall");
            Assert.That(g.Tick(0.1f, away), Is.EqualTo(0f));
            Assert.That(g.Tick(0.1f, inGap), Is.EqualTo(FacadeGlass.EdgeDamage).Within(1e-4f));
            Assert.That(g.Tick(0.2f, inGap), Is.EqualTo(0f), "inside the interval");
            float total = 0f;
            for (int i = 0; i < 10; i++) total += g.Tick(0.1f, inGap);
            Assert.That(total, Is.EqualTo(FacadeGlass.EdgeDamage).Within(1e-4f), "one more cut in the next second");
            Assert.That(g.Tick(0.1f, new Vector3(float.NaN, 0f, 0f)), Is.EqualTo(0f));
        }

        [Test]
        public void FallingOutOfTheBuildingIsAWreckBelowTheSimsFallLine()
        {
            Assert.That(FacadeGlass.HasFallen(new Vector3(40f, -11f, 0f)), Is.False);
            Assert.That(FacadeGlass.HasFallen(new Vector3(40f, -13f, 0f)), Is.True);
            Assert.That(FacadeGlass.HasFallen(new Vector3(0f, -4f, 0f)), Is.False, "the pit is not a fall");
            Assert.That(FacadeGlass.FallDeathY, Is.EqualTo(new FloorOptions().fallDeathY).Within(1e-4f));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using JamKit.Movement;
using JamKit.Skyline;
using JamKit.Tests.Movement;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>The Food Court as data (task F1): the layout sheet's numbers, the hard rules for the data, and real
    /// <see cref="MoveSim"/> walks over the same solids.</summary>
    public class RepossessArenaTests
    {
        static FloorSolid S(string id) => RepossessArena.Find(id) ?? throw new AssertionException("no solid " + id);

        static readonly string[] WallRoles = { "wall", "rim", "shopfront", "lift" };

        // ---------------------------------------------------------------- the sheet's numbers

        [Test]
        public void TheHallIsSeventyByFiftySixWithAPitTwentyEightByTwentyAtMinusFour()
        {
            Assert.That(RepossessArena.MaxX - RepossessArena.MinX, Is.EqualTo(70f));
            Assert.That(RepossessArena.MaxZ - RepossessArena.MinZ, Is.EqualTo(56f));
            Assert.That(RepossessArena.PitMaxX - RepossessArena.PitMinX, Is.EqualTo(28f));
            Assert.That(RepossessArena.PitMaxZ - RepossessArena.PitMinZ, Is.EqualTo(20f));
            Assert.That(RepossessArena.PitFloorY, Is.EqualTo(-4f));
            Assert.That(S("pit-floor").box.max.Y, Is.EqualTo(-4f));
            Assert.That(RepossessArena.ArrivalPoint, Is.EqualTo(new Vector3(0f, 0f, -21f)));
            Assert.That(RepossessArena.SkylightY, Is.EqualTo(14f));
            Assert.That(S("skylight").box.min.Y, Is.EqualTo(14f));
            foreach (var id in new[] { "pit-wall-n1", "pit-wall-w1", "pit-wall-e2", "pit-wall-s2" })
            {
                var w = S(id);
                Assert.That(w.box.min.Y, Is.EqualTo(-4f), id);
                Assert.That(w.box.max.Y, Is.EqualTo(0f), id);
                Assert.That(w.runnable, Is.True, id + ": the pit's retaining walls are runnable");
            }
        }

        [Test]
        public void GalleriesBridgeAndCorridorsHaveTheSheetsDimensions()
        {
            foreach (var id in new[] { "deck-n", "deck-s" })
                Assert.That(S(id).box.max.Y, Is.EqualTo(6f), id + " top");
            Assert.That(S("deck-n").box.min.Y, Is.EqualTo(5.6f).Within(1e-4f));
            // 3 m overhang over the ring: the north deck starts at z 15, the shops (and the ring's edge) at 18
            Assert.That(S("shopfront-n1").box.min.Z - S("deck-n").box.min.Z, Is.EqualTo(3f).Within(1e-4f));
            Assert.That(-S("shopfront-s1").box.max.Z - -S("deck-s").box.max.Z, Is.EqualTo(3f).Within(1e-4f));
            var bridge = S("bridge-floor");
            Assert.That(bridge.box.max.X - bridge.box.min.X, Is.EqualTo(4f), "the bridge is 4 m wide");
            Assert.That(bridge.glass, Is.True);
            Assert.That(bridge.box.max.Y, Is.EqualTo(6f));
            var zones = RepossessArena.Zones();
            foreach (var id in new[] { "corridor-n", "corridor-s", "corridor-s-east" })
            {
                var z = zones.First(q => q.id == id);
                Assert.That(z.maxZ - z.minZ, Is.EqualTo(3.5f).Within(1e-4f), id);
            }
            var leg = zones.First(q => q.id == "corridor-w");
            Assert.That(leg.maxX - leg.minX, Is.EqualTo(3.5f).Within(1e-4f));
        }

        [Test]
        public void NorthRowHasFiveShopsSouthRowFourPlusTheAlcoveAndTwoWalkInsPerRow()
        {
            var zones = RepossessArena.Zones();
            var north = zones.Where(z => z.id.StartsWith("shop-n")).ToArray();
            var south = zones.Where(z => z.id.StartsWith("shop-s")).ToArray();
            Assert.That(north.Length, Is.EqualTo(5));
            Assert.That(south.Length, Is.EqualTo(4));
            Assert.That(south.Any(z => z.id == "shop-s3"), Is.False);
            var alcove = zones.First(z => z.id == "alcove");
            Assert.That(alcove.minX, Is.EqualTo(-5.2f));
            Assert.That(alcove.maxX, Is.EqualTo(5.2f));
            foreach (var z in north.Concat(south)) Assert.That(z.level, Is.EqualTo(SurfaceLevel.ShopInterior), z.id);

            foreach (string row in new[] { "n", "s" })
            {
                int walkIn = 0;
                for (int i = 1; i <= 5; i++)
                {
                    string id = row + i;
                    if (row == "s" && i == 3) continue;
                    var l = RepossessArena.Find("shopfront-" + id + "-l");
                    if (l == null) { Assert.That(RepossessArena.Find("shopfront-" + id), Is.Not.Null, "closed shop " + id); continue; }
                    walkIn++;
                    var r = S("shopfront-" + id + "-r");
                    Assert.That(r.box.min.X - l.box.max.X, Is.GreaterThanOrEqualTo(2.4f - 1e-3f), id + " front doorway");
                    Assert.That(r.box.min.X - l.box.max.X, Is.EqualTo(RepossessArena.FrontDoorWidth).Within(1e-3f));
                    var bl = S("shop-" + id + "-back-l");
                    var br = S("shop-" + id + "-back-r");
                    Assert.That(br.box.min.X - bl.box.max.X, Is.GreaterThanOrEqualTo(2.4f - 1e-3f), id + " back doorway");
                    // the doorways clear a standing Unit (the lintel is 3 m up)
                    Assert.That(S("shopfront-" + id + "-lintel").box.min.Y, Is.GreaterThanOrEqualTo(2.5f));
                }
                Assert.That(walkIn, Is.EqualTo(2), "walk-in shops in row " + row);
            }
        }

        // ---------------------------------------------------------------- hard rules of the data

        [Test]
        public void IdsAreUniqueAndEveryBoxIsWellFormed()
        {
            var solids = RepossessArena.Solids();
            Assert.That(solids.Select(s => s.id).Distinct().Count(), Is.EqualTo(solids.Length), "ids are unique");
            foreach (var s in solids)
            {
                Assert.That(s.id, Is.Not.Empty);
                Assert.That(s.role, Is.Not.Empty, s.id);
                Assert.That(s.box.max.X, Is.GreaterThan(s.box.min.X), s.id);
                Assert.That(s.box.max.Y, Is.GreaterThan(s.box.min.Y), s.id);
                Assert.That(s.box.max.Z, Is.GreaterThan(s.box.min.Z), s.id);
            }
        }

        [Test]
        public void NoTwoSolidsPenetrateByMoreThanTwoCentimetres()
        {
            var solids = RepossessArena.Solids();
            var bad = new List<string>();
            for (int i = 0; i < solids.Length; i++)
                for (int j = i + 1; j < solids.Length; j++)
                {
                    var a = solids[i].box; var b = solids[j].box;
                    float ox = Math.Min(a.max.X, b.max.X) - Math.Max(a.min.X, b.min.X);
                    float oy = Math.Min(a.max.Y, b.max.Y) - Math.Max(a.min.Y, b.min.Y);
                    float oz = Math.Min(a.max.Z, b.max.Z) - Math.Max(a.min.Z, b.min.Z);
                    if (ox > 0.02f && oy > 0.02f && oz > 0.02f) bad.Add($"{solids[i].id} / {solids[j].id} ({ox:0.00} x {oy:0.00} x {oz:0.00})");
                }
            Assert.That(bad, Is.Empty, string.Join("\n", bad));
        }

        [Test]
        public void EveryTallLongWallHasADeliberateRunnableValue()
        {
            // runnable: the pit's retaining walls, the façade, the shopfronts on the ring and the two promenade end walls.
            // Not runnable: every wall of the service corridor, the lift, the shops' insides and the hall's outer walls
            // behind the corridors.
            foreach (var s in RepossessArena.Solids())
            {
                if (Array.IndexOf(WallRoles, s.role) < 0) continue;
                var d = s.box.max - s.box.min;
                if (d.Y < 2f || Math.Max(d.X, d.Z) < 3f) continue;
                bool expected = s.id.StartsWith("pit-wall") || s.id.StartsWith("shopfront-") || s.id == "wall-north-b" || s.id == "wall-south-c";
                Assert.That(s.runnable, Is.EqualTo(expected), s.id + " (" + s.role + ")");
            }
        }

        [Test]
        public void TheFacadeIsTwoMetreSomethingPanesEachItsOwnRunnableGlassSolid()
        {
            var panes = RepossessArena.Solids().Where(s => RepossessArena.IsPane(s.id)).ToArray();
            Assert.That(panes.Length, Is.EqualTo(RepossessArena.PaneRows * RepossessArena.PaneCols));
            double area = 0;
            foreach (var p in panes)
            {
                Assert.That(p.role, Is.EqualTo("facade-pane"));
                Assert.That(p.runnable, Is.True, p.id);
                Assert.That(p.glass, Is.True, p.id);
                float w = p.box.max.Z - p.box.min.Z, h = p.box.max.Y - p.box.min.Y;
                Assert.That(w, Is.InRange(2f, 3f), p.id + ": losing one is a local cost");
                Assert.That(p.box.min.X, Is.EqualTo(RepossessArena.MaxX), p.id);
                area += w * h;
            }
            Assert.That(area, Is.EqualTo(56.0 * RepossessArena.HallClearY).Within(0.01), "the panes tile the wall without gaps or overlaps");
            for (int r = 0; r < RepossessArena.PaneRows; r++)
                for (int c = 0; c < RepossessArena.PaneCols; c++)
                    Assert.That(RepossessArena.Find(RepossessArena.PaneId(r, c)), Is.Not.Null);
            Assert.That(RepossessArena.PaneId(2, 7), Is.EqualTo("facade-pane-2-7"));
        }

        [Test]
        public void DisablingOnePaneLeavesItsNeighboursAndOnlyThatTileGone()
        {
            var map = new Dictionary<string, BoxWorld.Box>();
            var w = FloorWorld.Build(map);
            var gone = RepossessArena.PaneId(1, 10);
            map[gone].enabled = false;
            var g = RepossessArena.Find(gone).box;
            float mid = (g.min.Z + g.max.Z) * 0.5f;
            Assert.That(w.CastWall(new Vector3(30f, 6f, mid), new Vector3(1f, 0f, 0f), 0.3f, 10f, out var hole), Is.False, "the broken tile is a hole");
            Assert.That(w.CastWall(new Vector3(30f, 6f, mid + 2.8f), new Vector3(1f, 0f, 0f), 0.3f, 10f, out var next), Is.True, "its neighbour is still a wall");
            Assert.That(map[RepossessArena.PaneId(1, 11)].enabled, Is.True);
            Assert.That(map[RepossessArena.PaneId(0, 10)].enabled, Is.True);
            Assert.That(next.runnable, Is.True);
        }

        [Test]
        public void CoverIsArchitectureNotFreeStandingBoxes()
        {
            var allowed = new[] { "rim", "shopfront", "column", "pilaster", "escalator", "wall", "lift", "facade-pane", "deck", "ceiling", "ramp" };
            var zones = RepossessArena.Zones().Where(z => z.level == SurfaceLevel.Ring || z.level == SurfaceLevel.Gallery).ToArray();
            foreach (var s in RepossessArena.Solids())
            {
                foreach (var z in zones)
                {
                    float ox = Math.Min(s.box.max.X, z.maxX) - Math.Max(s.box.min.X, z.minX);
                    float oz = Math.Min(s.box.max.Z, z.maxZ) - Math.Max(s.box.min.Z, z.minZ);
                    if (ox <= 0.01f || oz <= 0.01f) continue;
                    float lo = Math.Max(s.box.min.Y, z.y), hi = Math.Min(s.box.max.Y, z.y + 3f);
                    if (hi - lo <= 0.01f) continue;     // not standing in this zone's space
                    Assert.That(s.role, Is.Not.EqualTo("table").And.Not.EqualTo("planter"), s.id + " in " + z.id);
                    if (s.box.max.Y - Math.Max(s.box.min.Y, z.y) > 1.2f)
                        Assert.That(allowed, Does.Contain(s.role), $"{s.id} ({s.role}) is taller than 1.2 m in {z.id} and is not architecture");
                }
            }
        }

        [Test]
        public void TablesAndPlantersAreCrouchCoverInThePitOnly()
        {
            var pit = RepossessArena.Zones().First(z => z.id == "pit");
            int tables = 0, planters = 0;
            foreach (var s in RepossessArena.Solids())
            {
                if (s.role != "table" && s.role != "planter") continue;
                if (s.role == "table") tables++; else planters++;
                Assert.That(s.box.max.Y - s.box.min.Y, Is.LessThanOrEqualTo(1.0f + 1e-4f), s.id);
                Assert.That(s.box.min.X, Is.GreaterThanOrEqualTo(pit.minX), s.id);
                Assert.That(s.box.max.X, Is.LessThanOrEqualTo(pit.maxX), s.id);
                Assert.That(s.box.min.Z, Is.GreaterThanOrEqualTo(pit.minZ), s.id);
                Assert.That(s.box.max.Z, Is.LessThanOrEqualTo(pit.maxZ), s.id);
                Assert.That(s.box.min.Y, Is.EqualTo(-4f), s.id);
            }
            Assert.That(tables, Is.EqualTo(8));
            Assert.That(planters, Is.EqualTo(4));
        }

        [Test]
        public void NoPitPropStandsWithinThreeMetresOfASocketsLaneToARampFoot()
        {
            var sockets = new[] { new Vector2(-2f, 0f), new Vector2(9f, 7f), new Vector2(-13f, -7f) };    // kiosk, NE corner, SW corner
            var feet = new[] { new Vector2(-10f, 0f), new Vector2(6f, 0f), new Vector2(-2f, 4f), new Vector2(-2f, -4f) };
            foreach (var s in RepossessArena.Solids())
            {
                if (s.role != "table" && s.role != "planter") continue;
                foreach (var a in sockets)
                    foreach (var b in feet)
                        Assert.That(DistanceRectToSegment(s.box, a, b), Is.GreaterThanOrEqualTo(3f), $"{s.id} vs lane {a}->{b}");
            }
        }

        static float DistanceRectToSegment(CoverBox r, Vector2 a, Vector2 b)
        {
            float best = float.MaxValue;
            for (int i = 0; i <= 40; i++)
            {
                var p = Vector2.Lerp(a, b, i / 40f);
                float dx = Math.Max(Math.Max(r.min.X - p.X, p.X - r.max.X), 0f), dz = Math.Max(Math.Max(r.min.Z - p.Y, p.Y - r.max.Z), 0f);
                best = Math.Min(best, (float)Math.Sqrt(dx * dx + dz * dz));
            }
            return best;
        }

        [Test]
        public void EveryRampIsAtMostFortyDegrees()
        {
            int n = 0;
            foreach (var s in RepossessArena.Solids().Where(q => q.ramp))
            {
                n++;
                float len = s.rampAlongX ? s.box.max.X - s.box.min.X : s.box.max.Z - s.box.min.Z;
                float deg = (float)(Math.Atan2(Math.Abs(s.rampTopEnd - s.rampTopStart), len) * 180.0 / Math.PI);
                TestContext.WriteLine($"{s.id}: {deg:0.0} degrees");
                Assert.That(deg, Is.LessThanOrEqualTo(40f), s.id);
                Assert.That(deg, Is.GreaterThan(10f), s.id);
                Assert.That(s.box.max.Y, Is.EqualTo(Math.Max(s.rampTopStart, s.rampTopEnd)), s.id + ": the box spans to the higher end");
            }
            Assert.That(n, Is.EqualTo(7), "four pit ramps, the chute and two escalators");
        }

        [Test]
        public void ZonesAreNamedAndInsideTheHall()
        {
            var zones = RepossessArena.Zones();
            Assert.That(zones.Select(z => z.id).Distinct().Count(), Is.EqualTo(zones.Length));
            foreach (var id in new[] { "pit", "ring-n", "ring-s", "ring-w", "ring-e", "gallery-n", "gallery-s", "bridge", "corridor-n", "corridor-s", "corridor-w", "exit-lift", "alcove", "cab" })
                Assert.That(zones.Any(z => z.id == id), id);
            foreach (var z in zones)
            {
                Assert.That(z.minX, Is.GreaterThanOrEqualTo(RepossessArena.MinX), z.id);
                Assert.That(z.maxX, Is.LessThanOrEqualTo(RepossessArena.MaxX), z.id);
                Assert.That(z.minZ, Is.GreaterThanOrEqualTo(RepossessArena.MinZ), z.id);
                Assert.That(z.maxZ, Is.LessThanOrEqualTo(RepossessArena.MaxZ), z.id);
                Assert.That(z.maxX, Is.GreaterThan(z.minX), z.id);
                Assert.That(z.maxZ, Is.GreaterThan(z.minZ), z.id);
            }
            var lift = RepossessArena.ExitLiftZone;
            var exit = zones.First(z => z.id == "exit-lift");
            Assert.That(lift.min.X, Is.GreaterThanOrEqualTo(exit.minX));
            Assert.That(lift.max.X, Is.LessThanOrEqualTo(exit.maxX));
            Assert.That(lift.max.X - lift.min.X, Is.EqualTo(4f));
        }

        // ---------------------------------------------------------------- TryGroundY

        [Test]
        public void TryGroundYReadsFloorsRampsAndDecks()
        {
            Assert.That(RepossessArena.TryGroundY(24f, -5f, 3f, out float y), Is.True);
            Assert.That(y, Is.EqualTo(0f));
            Assert.That(RepossessArena.TryGroundY(-2f, 7f, 1f, out y), Is.True);
            Assert.That(y, Is.EqualTo(-4f + 4f * (7f - 4f) / 6f).Within(1e-4f), "the north ramp, interpolated");
            Assert.That(RepossessArena.TryGroundY(8f, 0f, 10f, out y), Is.True);
            Assert.That(y, Is.EqualTo(6f), "the bridge");
            Assert.That(RepossessArena.TryGroundY(8f, 6f, 5f, out y), Is.True);
            Assert.That(y, Is.EqualTo(-4f), "the pit floor under the bridge");
            Assert.That(RepossessArena.TryGroundY(-20.75f, 9.5f, 5f, out y), Is.True);
            Assert.That(y, Is.EqualTo(6f * 5.5f / 11f).Within(1e-4f), "the north-west escalator");
            Assert.That(RepossessArena.TryGroundY(0f, 0f, -10f, out y), Is.False, "nothing below");
            Assert.That(RepossessArena.TryGroundY(-2f, 0f, 0f, out y), Is.True);
            Assert.That(y, Is.EqualTo(-1.5f));
        }

        [Test]
        public void TryGroundYAgreesWithMoveSimsStandingHeight()
        {
            // 40 points on a 2 m grid (feet dropped from just above, settled with no input)
            var pts = new List<Vector3>();   // x, fromY, z
            void Add(float x, float z, float fromY) => pts.Add(new Vector3(x, fromY, z));
            foreach (float x in new[] { 14f, 16f, 22f, 26f, 30f, 34f }) { Add(x, -14f, 2f); Add(x, 14f, x > 30.5f ? 4f : 2f); }   // 12: south and north ring (x 34 is the facade landing, top 2.8)
            for (float z = -8f; z <= 8f; z += 4f) Add(24f, z, 2f);                                         // 5: east ring
            for (float x = -14f; x <= 10f; x += 4f) Add(x, 12f, 2f);                                       // 7: pit rim north
            Add(-20f, -8f, 2f); Add(-20f, 0f, 2f); Add(-28f, 6f, 2f);                                      // 3: west ring
            Add(-12f, -9f, 0f); Add(8f, 4f, 0f); Add(-8f, 0f, 0f); Add(9f, -3f, -2f);                       // 4: pit floor
            Add(0f, -21f, 2f); Add(-10.4f, 21f, 2f); Add(10.4f, -21f, 2f);                                  // alcove, two shops
            Add(0f, 26f, 2f); Add(-33f, 0f, 2f); Add(-29f, -8f, 2f);                                                         // corridor
            Add(8f, 0f, 8f); Add(30f, 20f, 8f); Add(-22f, -20f, 8f);                                       // bridge, two gallery ends
            Assert.That(pts.Count, Is.EqualTo(40));
            var w = FloorWorld.Build();
            foreach (var p in pts)
            {
                Assert.That(RepossessArena.TryGroundY(p.X, p.Z, p.Y, out float expect), Is.True, $"({p.X}, {p.Z}) from {p.Y}");
                var sim = new MoveSim(w, new MoveDef());
                sim.Teleport(new Vector3(p.X, p.Y, p.Z), 0f, Vector3.Zero);
                for (int i = 0; i < 240; i++) sim.Step(default, MoveTestKit.H);
                Assert.That(sim.Grounded, Is.True, $"({p.X}, {p.Z})");
                Assert.That(sim.Position.Y, Is.EqualTo(expect).Within(0.06f), $"({p.X}, {p.Z}) from {p.Y}");
            }
        }

        [Test]
        public void TryGroundYAgreesOnRampSurfaces()
        {
            var w = FloorWorld.Build();
            foreach (var s in RepossessArena.Solids().Where(q => q.ramp))
            {
                float cx = (s.box.min.X + s.box.max.X) * 0.5f, cz = (s.box.min.Z + s.box.max.Z) * 0.5f;
                Assert.That(RepossessArena.TryGroundY(cx, cz, s.box.max.Y + 0.5f, out float expect), Is.True, s.id);
                Assert.That(expect, Is.EqualTo(s.TopAt(cx, cz)).Within(1e-4f), s.id);
                var sim = new MoveSim(w, new MoveDef());
                sim.Teleport(new Vector3(cx, expect, cz), 0f, Vector3.Zero);
                Assert.That(sim.Grounded, Is.True, s.id + ": standing on its surface");
                sim.Step(default, MoveTestKit.H);
                Assert.That(sim.Position.Y, Is.EqualTo(expect).Within(0.06f), s.id);
            }
        }

        // ---------------------------------------------------------------- sight

        [Test]
        public void SightBlockersHaveNoGlassNoGroundSlabsAndNoRampWedges()
        {
            var solids = RepossessArena.Solids();
            var blockers = RepossessArena.SightBlockers();
            Assert.That(blockers.Length, Is.GreaterThan(100));
            foreach (var g in solids.Where(s => s.glass))
                Assert.That(blockers.Any(b => Same(b, g.box)), Is.False, g.id + " is glass");
            foreach (var r in solids.Where(s => s.ramp))
                Assert.That(blockers.Any(b => Same(b, r.box)), Is.False, r.id + " as a wedge");
            foreach (var f in solids.Where(s => s.role == "floor"))
                Assert.That(blockers.Any(b => Same(b, f.box)), Is.False, f.id + " is ground");
            // but the stepped bulk of a ramp does block: a low line through the north-west escalator's body
            Assert.That(CoverGeometry.Blocked(new Vector3(-24f, 2.5f, 13f), new Vector3(-18f, 2.5f, 13f), blockers), Is.True, "through the escalator's body");
            Assert.That(CoverGeometry.Blocked(new Vector3(-24f, 5.5f, 13f), new Vector3(-18f, 5.5f, 13f), blockers), Is.False, "over its slope");
        }

        static bool Same(CoverBox a, CoverBox b) => a.min == b.min && a.max == b.max;

        [Test]
        public void ASegmentFromTheBridgeToThePitFloorIsClear()
        {
            var b = RepossessArena.SightBlockers();
            Assert.That(CoverGeometry.Blocked(new Vector3(8f, 7.6f, 3f), new Vector3(11f, -3.9f, 3f), b), Is.False);
            Assert.That(CoverGeometry.Blocked(new Vector3(8f, 7.6f, 0f), new Vector3(8f, -3.9f, -9f), b), Is.False, "down through the bridge's glass floor");
            // a closed shopfront stops sight, a walk-in's doorway does not
            Assert.That(CoverGeometry.Blocked(new Vector3(-20.8f, 1.6f, 14f), new Vector3(-20.8f, 1.6f, 22f), b), Is.True);
            Assert.That(CoverGeometry.Blocked(new Vector3(-10.4f, 1.6f, 14f), new Vector3(-10.4f, 1.6f, 22f), b), Is.False);
        }

        [Test]
        public void ShotBlockersAddTheBalustradesButNotThePanesTheBridgeFloorOrTheSkylight()
        {
            var shots = RepossessArena.ShotBlockers();
            foreach (var s in RepossessArena.Solids())
            {
                bool has = shots.Any(b => Same(b, s.box));
                if (s.role == "balustrade") Assert.That(has, Is.True, s.id + " blocks shots");
                else if (s.glass) Assert.That(has, Is.False, s.id + " (glass) is not a shot blocker");
            }
            Assert.That(CoverGeometry.Blocked(new Vector3(8f, 6.5f, -3f), new Vector3(14f, 6.5f, -3f), shots), Is.True, "the bridge's east rail stops a shot");
        }

        // ---------------------------------------------------------------- real MoveSim walks over FloorWorld.Build()

        /// <summary>Steers the sim to each waypoint in turn (flat distance under 0.6 m counts). False on timeout.</summary>
        static bool WalkPath(MoveSim sim, float maxSeconds, bool sprint, params Vector2[] path)
        {
            float t = 0f;
            foreach (var wp in path)
            {
                while (true)
                {
                    float dx = wp.X - sim.Position.X, dz = wp.Y - sim.Position.Z;
                    if (dx * dx + dz * dz < 0.36f) break;
                    if (t > maxSeconds) return false;
                    float yaw = (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI);
                    sim.Step(new MoveInput { move = new Vector2(0f, 1f), yaw = yaw, sprintHeld = sprint }, MoveTestKit.H);
                    t += MoveTestKit.H;
                }
            }
            return true;
        }

        static MoveSim StandAt(BoxWorld w, float x, float z, float y = 0f)
        {
            RepossessArena.TryGroundY(x, z, y + 0.05f, out float g);
            return MoveTestKit.Standing(w, new Vector3(x, g, z));
        }

        static Vector2 V(float x, float z) => new Vector2(x, z);

        static readonly Vector2[] ToRingSouth = { new Vector2(0f, -14f) };

        [Test]
        public void FromTheArrivalPointToEachPitRampsFootAndDown()
        {
            var w = FloorWorld.Build();
            var routes = new Dictionary<string, Vector2[]>
            {
                ["south"] = new[] { V(0f, -14f), V(-2f, -12f), V(-2f, -8f), V(-2f, -3.5f) },
                ["west"] = new[] { V(0f, -14f), V(-20f, -14f), V(-20f, 0f), V(-18f, 0f), V(-13f, 0f), V(-9f, 0f) },
                ["east"] = new[] { V(0f, -14f), V(24f, -14f), V(24f, 0f), V(14f, 0f), V(9f, 0f), V(4.5f, 0f) },
                ["north"] = new[] { V(0f, -14f), V(24f, -14f), V(24f, 14f), V(-2f, 14f), V(-2f, 11f), V(-2f, 6f), V(-2f, 2.6f) },
            };
            foreach (var kv in routes)
            {
                var sim = StandAt(w, 0f, -21f);
                Assert.That(sim.Grounded, Is.True);
                Assert.That(WalkPath(sim, 40f, false, kv.Value), Is.True, kv.Key + " ramp: reached the foot");
                Assert.That(sim.Position.Y, Is.EqualTo(-4f).Within(0.2f), kv.Key + " ramp: on the pit floor");
            }
        }

        [Test]
        public void UpEachEscalatorToItsGalleryAndAcrossTheBridge()
        {
            var w = FloorWorld.Build();
            var nw = StandAt(w, -20.75f, 1f);
            Assert.That(WalkPath(nw, 20f, false, V(-20.75f, 9f), V(-20.75f, 18f)), Is.True, "north-west escalator");
            Assert.That(nw.Position.Y, Is.EqualTo(6f).Within(0.2f));
            var se = StandAt(w, 19.25f, -1f);
            Assert.That(WalkPath(se, 20f, false, V(19.25f, -9f), V(19.25f, -18f)), Is.True, "south-east escalator");
            Assert.That(se.Position.Y, Is.EqualTo(6f).Within(0.2f));
            var deckStart = StandAt(w, 8f, -19f, 6f);
            Assert.That(deckStart.Position.Y, Is.EqualTo(6f).Within(0.1f));
            Assert.That(WalkPath(deckStart, 20f, false, V(8f, -10f), V(8f, 0f), V(8f, 10f), V(8f, 19f)), Is.True, "across the bridge");
            Assert.That(deckStart.Position.Y, Is.EqualTo(6f).Within(0.2f));
        }

        [Test]
        public void ThroughEachWalkInShopFromTheFrontDoorToTheBackDoor()
        {
            var w = FloorWorld.Build();
            foreach (var (id, cx, r) in new[] { ("n2", -10.4f, 1), ("n4", 10.4f, 1), ("s2", -10.4f, -1), ("s4", 10.4f, -1) })
            {
                var sim = StandAt(w, cx, 14f * r);
                Assert.That(WalkPath(sim, 20f, false, V(cx, 19.5f * r), V(cx, 22f * r), V(cx, 26f * r)), Is.True, id);
                Assert.That(sim.Position.Z * r, Is.GreaterThan(24.2f), id + ": out in the corridor");
                Assert.That(sim.Position.Y, Is.EqualTo(0f).Within(0.1f), id);
            }
        }

        [Test]
        public void AlongTheWholeServiceCorridorUFromTheNorthEastHatchToTheSouthWestPart()
        {
            var w = FloorWorld.Build();
            var sim = StandAt(w, 27.5f, 25.75f);
            Assert.That(WalkPath(sim, 40f, true, V(0f, 25.75f), V(-30f, 25.75f), V(-33.25f, 22f), V(-33.25f, 0f), V(-33.25f, -22f), V(-30f, -25.75f), V(-10f, -25.75f), V(-4f, -25.75f)), Is.True);
            Assert.That(sim.Position.Y, Is.EqualTo(0f).Within(0.1f));
            // and the shops' back doors, the west ring doors and the dead end behave
            var door = StandAt(w, -26f, 12f);
            Assert.That(WalkPath(door, 10f, false, V(-30f, 12f), V(-33.25f, 12f)), Is.True, "the west ring door at z 12 leads into the leg");
        }

        [Test]
        public void EveryShopShutterHasTwoAndAHalfMetresClearInFrontOfItAcrossItsWidth()
        {
            int shutters = 0;
            foreach (var d in RepossessSites.Doors())
            {
                if (d.kind != DoorKind.ShopShutter) continue;
                shutters++;
                float face = d.position.Z, sign = Math.Sign(face);
                float z0 = sign > 0 ? face - RepossessArena.ShutterClearance : face + 0.01f;
                float z1 = sign > 0 ? face - 0.01f : face + RepossessArena.ShutterClearance;
                float x0 = d.position.X - RepossessArena.ShutterWidth * 0.5f, x1 = d.position.X + RepossessArena.ShutterWidth * 0.5f;
                foreach (var s in RepossessArena.Solids())
                {
                    if (s.box.min.Y > 2.5f || s.box.max.Y < 0.3f) continue;          // decks above, floors below
                    bool hits = s.box.max.X > x0 && s.box.min.X < x1 && s.box.max.Z > z0 && s.box.min.Z < z1;
                    Assert.That(hits, Is.False, $"{s.id} ({s.role}) stands within {RepossessArena.ShutterClearance} m in front of {d.id}");
                }
            }
            Assert.That(shutters, Is.EqualTo(5));
        }

        [Test]
        public void TheGalleryColumnsKeepTheDecksSupportedAlongTheirFrontEdges()
        {
            foreach (var row in new[] { 1, -1 })
            {
                var xs = new List<float> { -26f, RepossessArena.MaxX };                      // the deck's ends
                foreach (var s in RepossessArena.Solids())
                {
                    if (s.role != "column" || s.box.min.Z * row < 0f || Math.Abs(s.box.min.Z) < 10f || Math.Abs(s.box.min.Z) > 20f) continue;
                    Assert.That(s.box.min.Y, Is.EqualTo(0f).Within(1e-4f), s.id);
                    Assert.That(s.box.max.Y, Is.EqualTo(RepossessArena.DeckBottomY).Within(1e-4f), s.id + " ends under the deck");
                    Assert.That(s.box.min.X, Is.GreaterThanOrEqualTo(-26f), s.id + " is under the deck");
                    xs.Add((s.box.min.X + s.box.max.X) * 0.5f);
                }
                xs.Sort();
                Assert.That(xs.Count, Is.GreaterThanOrEqualTo(6), "row " + row);
                for (int i = 1; i < xs.Count; i++)
                    Assert.That(xs[i] - xs[i - 1], Is.LessThanOrEqualTo(12.5f), $"row {row}: the front edge spans {xs[i - 1]}..{xs[i]} unsupported");
            }
        }

        [Test]
        public void TheClosedShopsAndTheShutterAreWalls()
        {
            var map = new Dictionary<string, BoxWorld.Box>();
            var w = FloorWorld.Build(map);
            var sim = StandAt(w, -18f, 14f);
            WalkPath(sim, 8f, false, V(-18f, 22f));
            Assert.That(sim.Position.Z, Is.LessThan(18f), "a closed shop's front stops the Unit");
            var lift = StandAt(w, -20f, 0f);
            WalkPath(lift, 10f, false, V(-27f, 0f));
            Assert.That(lift.Position.X, Is.GreaterThan(-24.2f), "the shutter holds until it is opened");
            map[RepossessArena.LiftShutterId].enabled = false;
            var open = StandAt(w, -20f, 0f);
            Assert.That(WalkPath(open, 10f, false, V(-29f, 0f)), Is.True);
            var e = RepossessArena.ExitLiftZone;
            Assert.That(open.Position.X, Is.InRange(e.min.X, e.max.X));
            Assert.That(open.Position.Z, Is.InRange(e.min.Z, e.max.Z));
        }

        [Test]
        public void TheCabOpensOntoTheAlcoveOnlyWhenItsDoorsAreDisabled()
        {
            var map = new Dictionary<string, BoxWorld.Box>();
            var w = FloorWorld.Build(map);
            var sim = StandAt(w, 0f, -26f);
            WalkPath(sim, 6f, false, V(0f, -18f));
            Assert.That(sim.Position.Z, Is.LessThan(-24f), "the closed doors hold");
            map[RepossessArena.CabDoorsId].enabled = false;
            Assert.That(WalkPath(sim, 8f, false, V(0f, -18f)), Is.True);
            Assert.That(sim.Position.Z, Is.GreaterThan(-19f));
        }

        [Test]
        public void ThePitsChuteCanBeSlidDownFromTheRing()
        {
            var w = FloorWorld.Build();
            var sim = StandAt(w, -10.5f, 17f);
            bool slid = false;
            float t = 0f;
            while (t < 8f && sim.Position.Z > 4.6f)
            {
                var input = new MoveInput { move = new Vector2(0f, 1f), yaw = 180f, sprintHeld = true };
                if (sim.Position.Z < 14f) { input.crouchHeld = true; input.crouchPressed = sim.Mode != MoveMode.Slide && !slid; }
                sim.Step(input, MoveTestKit.H);
                if (sim.Mode == MoveMode.Slide) slid = true;
                t += MoveTestKit.H;
            }
            Assert.That(sim.Position.Z, Is.LessThan(5f), "down the chute");
            Assert.That(sim.Position.Y, Is.LessThan(-3f), "into the pit");
            Assert.That(slid, Is.True, "a slide input from the ring slides");
        }
    }
}

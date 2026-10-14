using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>The walkable graph over the arena's data (task F3): reachability of the places that matter, the edge rules, the
    /// one-way chute, and the graph on small synthetic floors.</summary>
    public class ArenaNavTests
    {
        static FloorSolid Slab(string id, float x0, float x1, float z0, float z1, float top = 0f) =>
            new FloorSolid { id = id, role = "floor", box = new CoverBox { min = new Vector3(x0, top - 1f, z0), max = new Vector3(x1, top, z1) } };

        static FloorSolid Wall(string id, float x0, float x1, float z0, float z1, float h = 4f) =>
            new FloorSolid { id = id, role = "wall", box = new CoverBox { min = new Vector3(x0, 0f, z0), max = new Vector3(x1, h, z1) } };

        static List<FloorSolid> Real() => ArenaLint.OpenState(RepossessArena.Solids());

        static ArenaNav RealWithout(params string[] ids) =>
            ArenaNav.Build(Real().Where(s => !ids.Contains(s.id)).ToList(), 2f);

        static readonly ArenaNav Full = ArenaNav.Build(Real(), 2f);

        static int Node(ArenaNav nav, float x, float y, float z)
        {
            int n = nav.NodeAt(new Vector3(x, y, z));
            Assert.That(n, Is.GreaterThanOrEqualTo(0), $"a node near ({x}, {y}, {z})");
            Assert.That(Math.Abs(nav.PositionOf(n).Y - y), Is.LessThan(0.6f), $"the node near ({x}, {y}, {z}) is on the right storey");
            return n;
        }

        static readonly string[] PitRamps = { "ramp-w", "ramp-e", "ramp-n", "ramp-s" };

        [Test]
        public void TheArrivalEverySocketAndTheExitLiftAreMutuallyReachableOnFoot()
        {
            var points = new List<Vector3> { RepossessArena.ArrivalPoint, new Vector3(-29f, 0f, 0f) };
            foreach (var s in RepossessSites.Sockets())
            {
                // a node inside the circle (the kiosk's top itself is not walkable: its circle is the pit floor round it)
                int best = -1; float bd = float.MaxValue;
                for (int i = 0; i < Full.Count; i++)
                {
                    Vector3 p = Full.PositionOf(i);
                    if (Math.Abs(p.Y - s.position.Y) > Repossession.CircleHalfHeight) continue;
                    float d = (p.X - s.position.X) * (p.X - s.position.X) + (p.Z - s.position.Z) * (p.Z - s.position.Z);
                    if (d < bd) { bd = d; best = i; }
                }
                Assert.That(best, Is.GreaterThanOrEqualTo(0), s.id);
                Assert.That(bd, Is.LessThanOrEqualTo(FloorRules.CircleRadius * FloorRules.CircleRadius), s.id + ": a walkable node inside the tow circle");
                points.Add(Full.PositionOf(best));
            }
            var nodes = points.Select(p => Full.NodeAt(p)).ToList();
            foreach (int a in nodes)
                foreach (int b in nodes)
                    Assert.That(Full.Path(a, b), Is.Not.Null, $"{Full.PositionOf(a)} -> {Full.PositionOf(b)}");
        }

        [Test]
        public void ThePitIsReachableByEachOfItsFourRampsAlone()
        {
            foreach (string keep in PitRamps)
            {
                var nav = RealWithout(PitRamps.Where(r => r != keep).Concat(new[] { "chute" }).ToArray());
                int ring = Node(nav, -18f, 0f, 0f), pit = Node(nav, -2f, -4f, 6f);
                Assert.That(nav.Path(ring, pit), Is.Not.Null, keep + ": ring to pit");
                Assert.That(nav.Path(pit, ring), Is.Not.Null, keep + ": pit to ring");
            }
            var none = RealWithout(PitRamps.Concat(new[] { "chute" }).ToArray());
            Assert.That(none.Path(Node(none, -18f, 0f, 0f), Node(none, -2f, -4f, 6f)), Is.Null, "without a ramp the pit is walled off");
        }

        [Test]
        public void TheChuteIsADownwardOnlyWay()
        {
            var nav = RealWithout(PitRamps);
            int ring = Node(nav, -10f, 0f, 14f), pit = Node(nav, -2f, -4f, 6f);
            Assert.That(nav.Path(ring, pit), Is.Not.Null, "down the chute");
            Assert.That(nav.Path(pit, ring), Is.Null, "no walking back up it");
            int chuteNodes = 0;
            for (int i = 0; i < nav.Count; i++) if (nav.IsChute(i)) chuteNodes++;
            Assert.That(chuteNodes, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void EachGalleryIsReachableByAnEscalatorAndTheBridgeJoinsThem()
        {
            Node(Full, 24f, 6f, 20f); Node(Full, -20f, 6f, -20f);
            foreach (var (keep, drop) in new[] { ("escalator-nw", "escalator-se"), ("escalator-se", "escalator-nw") })
            {
                var nav = RealWithout(drop);
                int ring = Node(nav, 0f, 0f, -14f), north = Node(nav, 24f, 6f, 20f), south = Node(nav, -20f, 6f, -20f), bridge = Node(nav, 8f, 6f, 0f);
                foreach (int g in new[] { north, south, bridge })
                {
                    Assert.That(nav.Path(ring, g), Is.Not.Null, $"with only {keep}: ring to {nav.PositionOf(g)}");
                    Assert.That(nav.Path(g, ring), Is.Not.Null, $"with only {keep}: {nav.PositionOf(g)} back down");
                }
            }
            var neither = RealWithout("escalator-nw", "escalator-se");
            Assert.That(neither.Path(Node(neither, 0f, 0f, -14f), Node(neither, 8f, 6f, 0f)), Is.Null, "the galleries and the bridge are reached by the escalators on foot");
        }

        [Test]
        public void TheSouthEastEscalatorsTopExitIsClearOntoTheDeck()
        {
            // F2 flagged a gallery column at x 20, z -16.5 near the escalator's top; columns stand under the deck, the exit is on it
            int foot = Node(Full, 20f, 0f, -2f), deck = Node(Full, 20f, 6f, -18f), top = Node(Full, 20f, 6f, -16f);
            Assert.That(Full.Path(foot, top), Is.Not.Null);
            Assert.That(Full.Path(foot, deck), Is.Not.Null);
            Assert.That(Full.PositionOf(top).Y, Is.EqualTo(6f).Within(0.05f));
            foreach (var c in RepossessArena.Solids().Where(x => x.role == "column"))
                Assert.That(c.box.max.Y, Is.EqualTo(RepossessArena.DeckBottomY).Within(1e-3f), c.id + " ends under the deck");
        }

        [Test]
        public void NoIslandsAndEveryEdgeObeysTheStepAndSlopeRules()
        {
            int cut = 0, onLanding = 0;
            for (int i = 0; i < Full.Count; i++)
            {
                if (Full.ComponentOf(i) == 0) continue;
                if (!Full.IsClimbable(i)) cut++;
                else if (Full.PositionOf(i).X > 30f && Full.PositionOf(i).Y > 2.5f) onLanding++;
            }
            Assert.That(cut, Is.LessThanOrEqualTo(4), "island nodes (a ledge a jump and a mantle climb is not one)");
            Assert.That(onLanding, Is.GreaterThanOrEqualTo(4), "the facade landing is a climbable ledge, not an island");
            int edges = 0, oneWay = 0;
            for (int a = 0; a < Full.Count; a++)
                foreach (int b in Full.Neighbours(a))
                {
                    edges++;
                    Vector3 pa = Full.PositionOf(a), pb = Full.PositionOf(b);
                    float dy = Math.Abs(pb.Y - pa.Y), run = (float)Math.Sqrt((pb.X - pa.X) * (pb.X - pa.X) + (pb.Z - pa.Z) * (pb.Z - pa.Z));
                    Assert.That(run, Is.LessThanOrEqualTo(2f * 1.4143f + 1e-3f));
                    if (dy > ArenaNav.StepHeight + 1e-4f)
                    {
                        Assert.That(Full.IsRamp(a) || Full.IsRamp(b), Is.True, $"{pa} -> {pb}: a step of {dy:0.00} m off a ramp");
                        Assert.That(dy / run, Is.LessThanOrEqualTo(ArenaNav.MaxSlope + 1e-3f), $"{pa} -> {pb}: slope");
                    }
                    if (!Full.Neighbours(b).Contains(a)) { oneWay++; Assert.That(Full.IsChute(a) || Full.IsChute(b), Is.True, $"{pa} -> {pb} is one-way but not the chute"); }
                }
            Assert.That(edges, Is.GreaterThan(5000));
            Assert.That(oneWay, Is.GreaterThan(0), "the chute's one-way edges");
        }

        [Test]
        public void NothingStandsInsideASolid()
        {
            var solids = Real();
            for (int i = 0; i < Full.Count; i++)
            {
                Vector3 p = Full.PositionOf(i);
                foreach (var s in solids)
                {
                    if (s.ramp || s.role == "floor" || s.role == "deck") continue;
                    var b = s.box;
                    bool inside = p.X > b.min.X + 0.01f && p.X < b.max.X - 0.01f && p.Z > b.min.Z + 0.01f && p.Z < b.max.Z - 0.01f
                                  && p.Y + 0.1f > b.min.Y && p.Y + 0.1f < b.max.Y;
                    Assert.That(inside, Is.False, $"node {p} is inside {s.id}");
                }
            }
        }

        [Test]
        public void TheGraphOfTheWholeArenaBuildsQuickly()
        {
            var sw = Stopwatch.StartNew();
            var nav = ArenaNav.Build(Real(), 2f);
            sw.Stop();
            Assert.That(nav.Count, Is.InRange(800, 1500));
            Assert.That(sw.Elapsed.TotalSeconds, Is.LessThan(5.0));
        }

        // ---------------------------------------------------------------- synthetic floors

        [Test]
        public void AFlatFloorIsOneConnectedGridAndAWallForcesTheWayRound()
        {
            var solids = new List<FloorSolid> { Slab("f", -10f, 10f, -10f, 10f), Wall("w", -0.2f, 0.2f, -6f, 10f) };
            var nav = ArenaNav.Build(solids, 2f);
            Assert.That(nav.IslandNodes, Is.EqualTo(0));
            int a = nav.NodeAt(new Vector3(-6f, 0f, 4f)), b = nav.NodeAt(new Vector3(6f, 0f, 4f));
            var path = nav.Path(a, b);
            Assert.That(path, Is.Not.Null);
            Assert.That(path.Min(n => nav.PositionOf(n).Z), Is.LessThan(-5.5f), "the only gap is at the south end of the wall");
            float len = 0f;
            for (int i = 1; i < path.Count; i++) len += Vector3.Distance(nav.PositionOf(path[i - 1]), nav.PositionOf(path[i]));
            Assert.That(len, Is.GreaterThan(24f), "a straight line would be 12 m; round the wall is longer");
            Assert.That(nav.Path(a, a), Is.EqualTo(new List<int> { a }));
        }

        [Test]
        public void AGapInTheFloorIsNotCrossedAndATallStepIsNotWalked()
        {
            var gap = ArenaNav.Build(new List<FloorSolid> { Slab("l", -10f, -2f, -4f, 4f), Slab("r", 2f, 10f, -4f, 4f) }, 2f);
            Assert.That(gap.Path(gap.NodeAt(new Vector3(-6f, 0f, 0f)), gap.NodeAt(new Vector3(6f, 0f, 0f))), Is.Null);
            Assert.That(gap.IslandNodes, Is.GreaterThan(0));
            var step = ArenaNav.Build(new List<FloorSolid> { Slab("low", -10f, 0f, -4f, 4f), Slab("high", 0f, 10f, -4f, 4f, 1f) }, 2f);
            Assert.That(step.Path(step.NodeAt(new Vector3(-6f, 0f, 0f)), step.NodeAt(new Vector3(6f, 1f, 0f))), Is.Null, "a 1 m step needs a jump, not a walk");
            var small = ArenaNav.Build(new List<FloorSolid> { Slab("low", -10f, 0f, -4f, 4f), Slab("high", 0f, 10f, -4f, 4f, 0.3f) }, 2f);
            Assert.That(small.Path(small.NodeAt(new Vector3(-6f, 0f, 0f)), small.NodeAt(new Vector3(6f, 0.3f, 0f))), Is.Not.Null, "a 0.3 m kerb is stepped");
        }

        [Test]
        public void NodeAtPicksTheRightStoreyAndNothingFarAway()
        {
            var nav = Full;
            var ring = nav.PositionOf(nav.NodeAt(new Vector3(0f, 0.2f, -14f)));
            var gallery = nav.PositionOf(nav.NodeAt(new Vector3(0f, 5.8f, -20f)));
            Assert.That(ring.Y, Is.EqualTo(0f).Within(0.05f));
            Assert.That(gallery.Y, Is.EqualTo(6f).Within(0.05f));
            Assert.That(nav.NodeAt(new Vector3(500f, 0f, 500f)), Is.EqualTo(-1));
            Assert.That(nav.Path(-1, 0), Is.Null);
        }

        [Test]
        public void ALedgeAJumpAndAMantleClimbIsNotAnIslandButAHighOneIs()
        {
            // a 2.8 m block on a flat floor: its top is cut off for walking but the base kit climbs it; a 6 m one is not climbable
            foreach (var (h, climbable) in new[] { (2.8f, true), (6f, false) })
            {
                var block = new FloorSolid { id = "block", role = "deck", box = new CoverBox { min = new Vector3(4f, 0f, -4f), max = new Vector3(10f, h, 4f) } };
                var nav = ArenaNav.Build(new List<FloorSolid> { Slab("f", -10f, 10f, -10f, 10f), block }, 2f);
                int top = nav.NodeAt(new Vector3(6f, h, 0f));
                Assert.That(nav.PositionOf(top).Y, Is.EqualTo(h).Within(0.05f));
                Assert.That(nav.ComponentOf(top), Is.Not.EqualTo(0));
                Assert.That(nav.IsClimbable(top), Is.EqualTo(climbable), "height " + h);
            }
        }

        [Test]
        public void SimplifyKeepsTheCornersOnly()
        {
            var nav = ArenaNav.Build(new List<FloorSolid> { Slab("f", -10f, 10f, -10f, 10f) }, 2f);
            var path = nav.Path(nav.NodeAt(new Vector3(-8f, 0f, 0f)), nav.NodeAt(new Vector3(8f, 0f, 0f)));
            var pts = nav.Simplify(path);
            Assert.That(pts.Count, Is.EqualTo(2), "a straight line is its two ends");
            Assert.That(nav.Simplify(null), Is.Empty);
        }
    }
}

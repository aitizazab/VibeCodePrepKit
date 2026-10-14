using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>
    /// The layout lint (task F3): the three measured rules and the door checks on small synthetic levels that fail each rule and
    /// pass it, then on the real Food Court, which passes with no problem at all (decision D23: the door nudges, the 4 m shop
    /// doorways, no alcove back walls, the facade landing, the chute 1 m east; low props hopped in rule 1; roof vents out of rule 2
    /// and its limit 70 percent).
    /// </summary>
    public class ArenaLintTests
    {
        // ---------------------------------------------------------------- synthetic levels

        static FloorSolid Slab(string id, float x0, float x1, float z0, float z1, float top = 0f) =>
            new FloorSolid { id = id, role = "floor", box = new CoverBox { min = new Vector3(x0, top - 1f, z0), max = new Vector3(x1, top, z1) } };

        static FloorSolid Wall(string id, float x0, float x1, float z0, float z1, float h = 4f, string role = "wall") =>
            new FloorSolid { id = id, role = role, box = new CoverBox { min = new Vector3(x0, 0f, z0), max = new Vector3(x1, h, z1) } };

        static SpawnDoor Shutter(string id, float x, float z, float yaw = 0f) =>
            new SpawnDoor { id = id, kind = DoorKind.ShopShutter, position = new Vector3(x, 0f, z), yawDeg = yaw };

        static SocketDef Socket(string id, float x, float y, float z, SocketGroup g = SocketGroup.Pit) =>
            new SocketDef { id = id, group = g, position = new Vector3(x, y, z) };

        static List<FloorSolid> Open() => new List<FloorSolid> { Slab("floor", -30f, 30f, -30f, 30f) };

        /// <summary>A 2.4 m wide, 14 m long corridor of walls from z 0 northward; closed at its far end unless asked.</summary>
        static List<FloorSolid> WithCorridor(bool closed)
        {
            var l = Open();
            l.Add(Wall("corr-w", -1.6f, -1.2f, 0f, 14f));
            l.Add(Wall("corr-e", 1.2f, 1.6f, 0f, 14f));
            if (closed) l.Add(Wall("corr-end", -1.6f, 1.6f, 14f, 14.4f));
            return l;
        }

        static bool Found(LintResult r, string text) => r.problems.Any(p => p.Contains(text));

        // ---- rule 1: exits

        [Test]
        public void ExitsAreVertexDisjointWalksOutOfTheRadiusCappedAtTwo()
        {
            var open = ArenaNav.Build(Open(), 2f);
            Assert.That(ArenaLint.Exits(open, open.NodeAt(Vector3.Zero), 10f), Is.EqualTo(2), "the middle of an open floor");
            Assert.That(ArenaLint.Exits(open, open.NodeAt(new Vector3(-30f, 0f, -30f)), 10f), Is.EqualTo(2), "a corner of an open floor still has two ways out");
            var closet = ArenaNav.Build(WithCorridor(true), 2f);
            Assert.That(ArenaLint.Exits(closet, closet.NodeAt(new Vector3(0f, 0f, 12f)), 10f), Is.EqualTo(1), "the end of a closet");
            Assert.That(ArenaLint.Exits(closet, closet.NodeAt(new Vector3(0f, 0f, 4f)), 10f), Is.EqualTo(1), "near its mouth: one way out, the other leads in");
            var through = ArenaNav.Build(WithCorridor(false), 2f);
            Assert.That(ArenaLint.Exits(through, through.NodeAt(new Vector3(0f, 0f, 8f)), 10f), Is.EqualTo(2), "a corridor open at both ends");
            Assert.That(ArenaLint.Exits(open, -1, 10f), Is.EqualTo(0));
            Assert.Throws<ArgumentNullException>(() => ArenaLint.Exits(null, 0, 10f));
        }

        [Test]
        public void ADeadEndClosetFailsRule1AndAThroughCorridorAndAnOpenFloorPass()
        {
            var open = ArenaLint.Run(Open(), new SpawnDoor[0], new SocketDef[0]);
            Assert.That(open.problems, Is.Empty, string.Join("\n", open.problems));
            Assert.That(open.failedExits, Is.EqualTo(0));
            Assert.That(open.minExits, Is.EqualTo(2));

            var closed = ArenaLint.Run(WithCorridor(true), new SpawnDoor[0], new SocketDef[0]);
            Assert.That(closed.failedExits, Is.GreaterThanOrEqualTo(5), "the closet's nodes");
            Assert.That(Found(closed, "rule 1"), Is.True);
            Assert.That(closed.minExits, Is.EqualTo(1));

            var through = ArenaLint.Run(WithCorridor(false), new SpawnDoor[0], new SocketDef[0]);
            Assert.That(through.failedExits, Is.EqualTo(0), string.Join("\n", through.problems));
        }

        // ---- rule 2: seen share

        [Test]
        public void SeenShareCountsTheDoorsInSightWithinRange()
        {
            var doors = new List<SpawnDoor> { Shutter("a", 10f, 0f), Shutter("b", 10f, 10f), Shutter("c", -10f, 0f), Shutter("d", 100f, 0f) };
            var eye = new Vector3(0f, 1.6f, 0f);
            Assert.That(ArenaLint.SeenShare(new CoverBox[0], eye, doors), Is.EqualTo(0.75f).Within(1e-4f), "d is beyond 60 m");
            Assert.That(ArenaLint.SeenShare(new CoverBox[0], eye, doors, 200f), Is.EqualTo(1f).Within(1e-4f));
            var wall = new[] { new CoverBox { min = new Vector3(4f, 0f, -20f), max = new Vector3(5f, 6f, 20f) } };
            Assert.That(ArenaLint.SeenShare(wall, eye, doors), Is.EqualTo(0.25f).Within(1e-4f), "a wall hides a and b");
            Assert.That(ArenaLint.SeenShare(wall, eye, new SpawnDoor[0]), Is.EqualTo(0f));
            Assert.That(ArenaLint.SeenShare(wall, eye, null), Is.EqualTo(0f));
        }

        [Test]
        public void ADoorsTargetIsItsMouthNotItsFace()
        {
            var south = Shutter("s", 5f, 18f, 180f);
            var t = ArenaLint.DoorTarget(south);
            Assert.That(t.Y, Is.EqualTo(1.5f).Within(1e-4f));
            Assert.That(t.Z, Is.EqualTo(17.65f).Within(1e-3f), "0.35 m out along the way it faces (south)");
            Assert.That(t.X, Is.EqualTo(5f).Within(1e-3f));
            var vent = new SpawnDoor { id = "v", kind = DoorKind.SkylightVent, position = new Vector3(1f, 13.6f, 2f) };
            Assert.That(ArenaLint.DoorTarget(vent), Is.EqualTo(vent.position));
        }

        static List<SpawnDoor> SixDoors() => new List<SpawnDoor>
        {
            Shutter("w1", -20f, 0f), Shutter("w2", -10f, 5f), Shutter("w3", -10f, -5f),
            Shutter("e1", 20f, 0f), Shutter("e2", 10f, 5f), Shutter("e3", 10f, -5f),
        };

        [Test]
        public void ARoomThatSeesEveryDoorFailsRule2AndTwoRoomsThatEachSeeHalfPass()
        {
            var hall = new List<FloorSolid> { Slab("floor", -40f, 40f, -10f, 10f) };
            var open = ArenaLint.Run(hall, SixDoors(), new SocketDef[0]);
            Assert.That(open.failedSight, Is.GreaterThan(100), "every point sees all six doors");
            Assert.That(open.worstSeenShare, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(Found(open, "rule 2"), Is.True);

            var split = new List<FloorSolid>(hall) { Wall("divider", -0.2f, 0.2f, -10f, 10f) };
            var two = ArenaLint.Run(split, SixDoors(), new SocketDef[0]);
            Assert.That(two.failedSight, Is.EqualTo(0), string.Join("\n", two.problems));
            Assert.That(two.worstSeenShare, Is.EqualTo(0.5f).Within(1e-4f), "each side sees its three");
        }

        // ---- rule 3: circle exposure

        [Test]
        public void ACircleInTheOpenIsExposedFromEveryBearingAndOneAtTheEndOfAClosetIsNot()
        {
            var open = ArenaLint.Run(Open(), new SpawnDoor[0], new[] { Socket("middle", 0f, 0f, 0f) });
            Assert.That(open.circleExposure[0], Is.EqualTo(1f).Within(1e-4f));
            Assert.That(open.circleSectors[0], Is.EqualTo(8));
            Assert.That(open.failedCircles, Is.EqualTo(0));

            var closet = ArenaLint.Run(WithCorridor(true), new SpawnDoor[0], new[] { Socket("end", 0f, 0f, 10f) });
            Assert.That(closet.circleExposure[0], Is.LessThan(FloorRules.MinCircleExposure));
            Assert.That(closet.circleSectors[0], Is.LessThanOrEqualTo(1), "only along the closet's axis");
            Assert.That(closet.failedCircles, Is.EqualTo(1));
            Assert.That(Found(closet, "rule 3"), Is.True);
        }

        [Test]
        public void ACircleNeedsTwoExposingSectorsAtLeastNinetyDegreesApart()
        {
            // a long wall to the south of the circle and a long wall to the west leave a 90 degree quadrant (north to east): the
            // bearings at its two edges are exactly 90 degrees apart, so it passes; close a wall to the north and it cannot
            var corner = Open();
            corner.Add(Wall("south", -30f, 30f, -4f, -3.6f));
            corner.Add(Wall("west", -4f, -3.6f, -30f, 30f));
            var quadrant = ArenaLint.Run(corner, new SpawnDoor[0], new[] { Socket("corner", 0f, 0f, 0f) });
            Assert.That(quadrant.circleSectors[0], Is.GreaterThanOrEqualTo(2));
            var shut = new List<FloorSolid>(corner) { Wall("north", -30f, 30f, 3.6f, 4f) };
            var pocket = ArenaLint.Run(shut, new SpawnDoor[0], new[] { Socket("pocket", 0f, 0f, 0f) });
            Assert.That(pocket.circleExposure[0], Is.LessThan(quadrant.circleExposure[0]));
            Assert.That(pocket.circleSectors[0], Is.LessThanOrEqualTo(1), "open to the east only");
            Assert.That(pocket.failedCircles, Is.EqualTo(1));
        }

        // ---- the extra checks

        [Test]
        public void ADoorTooCloseToAWallFailsItsBodyCheckAndMovedItPasses()
        {
            var level = Open();
            level.Add(Wall("tower", 20f, 20.4f, -30f, 30f, 16f));
            var near = new SpawnDoor { id = "vent-near", kind = DoorKind.SkylightVent, position = new Vector3(19.9f, 13.6f, 0f), yawDeg = 270f };
            var bad = ArenaLint.Run(level, new[] { near }, new SocketDef[0]);
            Assert.That(bad.failedDoors, Is.EqualTo(1));
            Assert.That(bad.problems.Any(p => p.StartsWith("door vent-near") && p.Contains("tower")), Is.True, string.Join("\n", bad.problems));
            var moved = new SpawnDoor { id = "vent-near", kind = DoorKind.SkylightVent, position = new Vector3(19.3f, 13.6f, 0f), yawDeg = 270f };
            Assert.That(ArenaLint.Run(level, new[] { moved }, new SocketDef[0]).failedDoors, Is.EqualTo(0));
            // a floor-care hatch 0.1 m from a wall: its 0.5 m body is inside it
            var hatch = new SpawnDoor { id = "hatch-near", kind = DoorKind.ServiceHatch, position = new Vector3(19.9f, 0f, 5f), yawDeg = 270f };
            Assert.That(ArenaLint.Run(level, new[] { hatch }, new SocketDef[0]).failedDoors, Is.EqualTo(1));
            Assert.That(ArenaLint.DoorBodyRadius(hatch), Is.EqualTo(SkylineEnemies.Find(SkylineEnemies.FloorCareId).bodyRadius));
            Assert.That(ArenaLint.DoorBodyRadius(near), Is.EqualTo(SkylineEnemies.Find(SkylineEnemies.DroneId).bodyRadius));
            Assert.That(ArenaLint.DoorBodyRadius(new SpawnDoor { kind = DoorKind.RailEnd }), Is.EqualTo(SkylineEnemies.Find(SkylineEnemies.GondolaId).bodyRadius));
        }

        [Test]
        public void AShutterNeedsAWalkableMouthAndIsFineInFrontOfItsShop()
        {
            var level = Open();
            level.Add(Wall("shop", -5f, 5f, 18f, 24f));
            var ok = Shutter("shutter-ok", 0f, 18f, 180f);
            Assert.That(ArenaLint.Run(level, new[] { ok }, new SocketDef[0]).failedDoors, Is.EqualTo(0));
            // a column 1.1 m from the face and a pilaster each side: the body fits (barely) but no node is within reach of the mouth
            var blocked = new List<FloorSolid>(level) { Wall("column", -0.4f, 0.4f, 16.1f, 16.9f), Wall("pilaster-w", -4f, -3.4f, 17.4f, 18f), Wall("pilaster-e", 3.4f, 4f, 17.4f, 18f) };
            var r = ArenaLint.Run(blocked, new[] { ok }, new SocketDef[0]);
            Assert.That(r.problems.Any(p => p.StartsWith("door shutter-ok")), Is.True, string.Join("\n", r.problems));
        }

        [Test]
        public void ADoorUnderOrOverACircleOnAnotherLevelIsANoteNotAProblem()
        {
            var level = Open();
            level.Add(Slab("deck", -10f, 10f, -10f, 10f, 6f));
            var r = ArenaLint.Run(level, new[] { Shutter("under", 1f, 1f) }, new[] { Socket("high", 0f, 6f, 0f, SocketGroup.High) });
            Assert.That(r.notes.Any(n => n.Contains("door under") && n.Contains("high")), Is.True, string.Join("\n", r.notes));
            Assert.That(r.problems.Any(p => p.StartsWith("door under")), Is.False);
            var far = ArenaLint.Run(level, new[] { Shutter("far", 20f, 20f) }, new[] { Socket("high", 0f, 6f, 0f, SocketGroup.High) });
            Assert.That(far.notes, Is.Empty);
        }

        [Test]
        public void SurfacesTheSpecCallsRunnableMustBe()
        {
            var level = Open();
            level.Add(new FloorSolid { id = "facade-pane-0-0", role = "facade-pane", runnable = false, glass = true, box = new CoverBox { min = new Vector3(30f, 0f, 0f), max = new Vector3(30.4f, 4f, 2.8f) } });
            level.Add(new FloorSolid { id = "pit-wall-x", role = "rim", runnable = true, box = new CoverBox { min = new Vector3(-30f, -4f, 0f), max = new Vector3(-29.6f, 0f, 4f) } });
            var r = ArenaLint.Run(level, new SpawnDoor[0], new SocketDef[0]);
            Assert.That(r.problems.Any(p => p.StartsWith("runnable: facade-pane-0-0")), Is.True);
            Assert.That(r.problems.Any(p => p.Contains("pit-wall-x")), Is.False);
        }

        [Test]
        public void IslandsOfMoreThanFourNodesAreProblemsAndLedgesAreNot()
        {
            var cut = new List<FloorSolid> { Slab("a", -10f, 10f, -10f, 10f), Slab("b", 20f, 30f, -10f, 10f) };
            var r = ArenaLint.Run(cut, new SpawnDoor[0], new SocketDef[0]);
            Assert.That(r.islandNodes, Is.GreaterThan(4));
            Assert.That(r.problems.Any(p => p.StartsWith("nav:")), Is.True);
            var ledge = new List<FloorSolid>(Open()) { new FloorSolid { id = "landing", role = "deck", box = new CoverBox { min = new Vector3(4f, 0f, -4f), max = new Vector3(10f, 2.8f, 4f) } } };
            var ok = ArenaLint.Run(ledge, new SpawnDoor[0], new SocketDef[0]);
            Assert.That(ok.islandNodes, Is.EqualTo(0), "a jump and a mantle climb a 2.8 m landing");
            Assert.That(ok.exempt, Is.GreaterThan(0), "its top is left out of rules 1 and 2");
            Assert.That(ok.problems.Any(p => p.StartsWith("nav:") || p.StartsWith("rule 1")), Is.False, string.Join("\n", ok.problems));
        }

        [Test]
        public void BlockersAreEverythingButGlassAndTheGround()
        {
            var solids = new List<FloorSolid>
            {
                Slab("floor", -10f, 10f, -10f, 10f), Wall("wall", 0f, 1f, 0f, 5f),
                new FloorSolid { id = "pane", role = "facade-pane", glass = true, box = new CoverBox { min = new Vector3(5f, 0f, 0f), max = new Vector3(5.4f, 4f, 3f) } },
                new FloorSolid { id = "ramp", role = "ramp", ramp = true, rampTopStart = 0f, rampTopEnd = -4f, rampAlongX = true, box = new CoverBox { min = new Vector3(-8f, -4f, -2f), max = new Vector3(-2f, 0f, 2f) } },
            };
            var b = ArenaLint.BlockersOf(solids);
            Assert.That(b.Any(x => x.min.X < -9f), Is.False, "no ground slab");
            Assert.That(b.Count(x => x.min.Z == 0f && x.max.Z == 5f), Is.EqualTo(1), "the wall");
            Assert.That(b.Any(x => x.min.X == 5f), Is.False, "no glass");
            Assert.That(b.Count(x => x.min.X >= -8.001f && x.max.X <= -1.999f), Is.GreaterThanOrEqualTo(2), "the ramp as stepped slices");
            Assert.That(b.Where(x => x.min.X >= -8.001f && x.max.X <= -1.999f).All(x => x.max.Y <= 0.001f), Is.True, "never a wedge above the surface");
        }

        // ---------------------------------------------------------------- the real arena (decision D23)

        static readonly Lazy<LintResult> Real = new Lazy<LintResult>(() => ArenaLint.Arena());

        [Test]
        public void TheRealArenaLintIsFastAndMeasuresWhatItShould()
        {
            var r = Real.Value;
            Assert.That(r.seconds, Is.LessThan(10.0), "the plan's budget");
            Assert.That(r.standable, Is.InRange(900, 1100));
            Assert.That(r.exempt, Is.InRange(10, 30), "the arrival cab, the exit lift, the facade landing and a couple of stray nodes");
            Assert.That(r.islandNodes, Is.LessThanOrEqualTo(4));
            var sockets = RepossessSites.Sockets();
            for (int i = 0; i < sockets.Length; i++)
                Assert.That(r.circleExposure[i], Is.GreaterThanOrEqualTo(FloorRules.MinCircleExposure), sockets[i].id);
            Assert.That(r.circleSectors[Array.FindIndex(sockets, s => s.id == "high-bridge")], Is.EqualTo(8), "the bridge circle is seen from every side");
        }

        [Test]
        public void TheRealArenaPassesTheLintWithNoProblems()
        {
            var r = Real.Value;
            Assert.That(r.problems, Is.Empty, string.Join("\n", r.problems));
            Assert.That(r.failedDoors, Is.EqualTo(0));
            Assert.That(r.failedCircles, Is.EqualTo(0));
            Assert.That(r.failedExits, Is.EqualTo(0));
            Assert.That(r.failedSight, Is.EqualTo(0));
            Assert.That(r.minExits, Is.EqualTo(FloorRules.MinExits));
            Assert.That(r.worstSeenShare, Is.LessThanOrEqualTo(FloorRules.MaxSeenShare), "of the ground-level doors the worst point sees 8 of 13 (about 62%)");
        }

        [Test]
        public void TheRealArenaNotesTheFloorCareShutterUnderTheSouthWestCircle()
        {
            var r = Real.Value;
            Assert.That(r.notes, Has.Count.EqualTo(1), string.Join("\n", r.notes));
            Assert.That(r.notes[0], Does.Contain("shutter-s1").And.Contain("high-south-west"));
        }

        [Test]
        public void TheLayoutDecisionsOfD23AreInTheData()
        {
            var ids = RepossessArena.Solids().Select(s => s.id).ToList();
            Assert.That(ids, Does.Not.Contain("alcove-back-w").And.Not.Contain("alcove-back-e"), "the corridor stubs open onto the alcove");
            Assert.That(RepossessArena.FrontDoorWidth, Is.EqualTo(4f));
            var landing = RepossessArena.Find(RepossessArena.FacadeLandingId);
            Assert.That(landing, Is.Not.Null);
            Assert.That(landing.role, Is.EqualTo("deck"));
            Assert.That(landing.box.min.X, Is.EqualTo(30.5f)); Assert.That(landing.box.max.X, Is.EqualTo(35f));
            Assert.That(landing.box.min.Z, Is.EqualTo(9f)); Assert.That(landing.box.max.Z, Is.EqualTo(14.8f));
            Assert.That(landing.box.max.Y, Is.EqualTo(2.8f));
            var chute = RepossessArena.Find("chute");
            Assert.That(chute.box.min.X, Is.EqualTo(-11f)); Assert.That(chute.box.max.X, Is.EqualTo(-8f));
            var doors = RepossessSites.Doors().ToDictionary(d => d.id);
            Assert.That(doors["vent-corridor-n"].position.Z, Is.EqualTo(26.85f).Within(1e-3f)); Assert.That(doors["vent-corridor-s"].position.Z, Is.EqualTo(-26.85f).Within(1e-3f));
            Assert.That(doors["vent-leg-n"].position.X, Is.EqualTo(-34.35f).Within(1e-3f)); Assert.That(doors["vent-leg-s"].position.X, Is.EqualTo(-34.35f).Within(1e-3f));
            Assert.That(doors["vent-promenade-n"].position.X, Is.EqualTo(34.35f).Within(1e-3f)); Assert.That(doors["vent-promenade-s"].position.X, Is.EqualTo(34.35f).Within(1e-3f));
            Assert.That(doors["hatch-corridor-n"].position.Z, Is.EqualTo(26.85f).Within(1e-3f)); Assert.That(doors["hatch-corridor-s"].position.Z, Is.EqualTo(-26.85f).Within(1e-3f));
        }

        // ---- the readings D23 chose, on synthetic levels

        [Test]
        public void ALowPropIsHoppedByRule1AndATallOneIsNot()
        {
            // a corridor open at both ends with a prop across it, wall to wall, north of the middle
            FloorSolid Prop(float h) => new FloorSolid { id = "prop", role = "table", box = new CoverBox { min = new Vector3(-1.2f, 0f, 11f), max = new Vector3(1.2f, h, 12f) } };
            var low = WithCorridor(false); low.Add(Prop(1.0f));
            var tall = WithCorridor(false); tall.Add(Prop(1.3f));
            var lowHop = ArenaNav.Build(low, 2f, ArenaNav.HopHeight);
            Assert.That(ArenaLint.Exits(lowHop, lowHop.NodeAt(new Vector3(0f, 0f, 8f)), 10f), Is.EqualTo(2), "a 1.0 m table is hopped");
            var lowNoHop = ArenaNav.Build(low, 2f, 0f);
            Assert.That(ArenaLint.Exits(lowNoHop, lowNoHop.NodeAt(new Vector3(0f, 0f, 8f)), 10f), Is.EqualTo(1), "a wall of props without hopping");
            var tallHop = ArenaNav.Build(tall, 2f, ArenaNav.HopHeight);
            Assert.That(ArenaLint.Exits(tallHop, tallHop.NodeAt(new Vector3(0f, 0f, 8f)), 10f), Is.EqualTo(1), "1.3 m is too tall to hop");
            Assert.That(ArenaLint.Run(low, new SpawnDoor[0], new SocketDef[0]).failedExits, Is.EqualTo(0));
            Assert.That(ArenaLint.Run(tall, new SpawnDoor[0], new SocketDef[0]).failedExits, Is.GreaterThan(0), "a tall prop still closes the corridor off");
            Assert.That(ArenaLint.Arena(RepossessArena.Solids(), RepossessSites.Doors(), new LintOptions { hopHeight = 0f }).failedExits, Is.GreaterThan(0),
                "without hopping the pit's tables and planters make pockets");
        }

        [Test]
        public void RuleTwoCountsTheGroundDoorsAndLeavesTheRoofVentsOut()
        {
            // two rooms with three shutters each (each room sees half of the ground doors) and ten roof vents every point sees
            var hall = new List<FloorSolid> { Slab("floor", -40f, 40f, -10f, 10f), Wall("divider", -0.2f, 0.2f, -10f, 10f) };
            var doors = SixDoors();
            for (int i = 0; i < 10; i++)
                doors.Add(new SpawnDoor { id = "vent" + i, kind = DoorKind.SkylightVent, position = new Vector3(-27f + i * 6f, 13.6f, 0f) });
            var byDesign = ArenaLint.Run(hall, doors, new SocketDef[0]);
            Assert.That(byDesign.failedSight, Is.EqualTo(0), string.Join("\n", byDesign.problems));
            Assert.That(byDesign.worstSeenShare, Is.EqualTo(0.5f).Within(1e-4f), "three of the six ground doors");
        }

        [Test]
        public void RuleTwoWithTheVentsCountedFailsTheRealArenaOnManyPoints()
        {
            // why the vents are out: overhead, seen from everywhere (the sheet's 27 doors would fail 70% on 125 points, worst 81%)
            var withVents = ArenaLint.Arena(RepossessArena.Solids(), RepossessSites.Doors(), new LintOptions { ventsCount = true });
            Assert.That(withVents.failedSight, Is.GreaterThan(100));
            Assert.That(withVents.worstSeenShare, Is.InRange(0.75f, 0.85f));
        }

        [Test]
        public void ALintOfTheSameDataTwiceGivesTheSameAnswer()
        {
            var a = ArenaLint.Arena();
            Assert.That(a.problems, Is.EqualTo(Real.Value.problems));
            Assert.That(a.circleExposure, Is.EqualTo(Real.Value.circleExposure));
        }
    }
}

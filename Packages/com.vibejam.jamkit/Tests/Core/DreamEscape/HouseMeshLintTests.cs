using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using JamKit.DreamEscape;
using JamKit.Movement;
using JamKit.Tests.Movement;
using NUnit.Framework;

namespace JamKit.Tests.DreamEscape
{
    /// <summary>House round 2: the mesh lint's rules on synthetic boxes, the plan's parts all resting or hung, every flight
    /// of stairs (and the treehouse ladder) climbed up and down on foot without stalling, and every memory in reach of
    /// where she stands to take it (the recorded run stalled at the stair's foot under the lake photo: it was out of the
    /// autopilot's reach).</summary>
    public class HouseMeshLintTests
    {
        static HousePart Floor(float top) => new HousePart
        {
            look = HouseLook.Slab,
            piece = new LabPiece { name = "Floor", kind = LabPieceKind.Floor, center = new Vector3(0f, top - 0.15f, 0f), size = new Vector3(10f, 0.3f, 10f), solid = true },
        };

        static HousePart Wall(float x) => new HousePart
        {
            look = HouseLook.Wall,
            piece = new LabPiece { name = "Wall", kind = LabPieceKind.Wall, center = new Vector3(x, 1.5f, 0f), size = new Vector3(0.2f, 3f, 10f), solid = true },
        };

        static HousePart Chair(Vector3 min, Vector3 max) => new HousePart
        {
            look = HouseLook.Furniture,
            model = "dining.chair",
            piece = new LabPiece { name = "Chair", kind = LabPieceKind.Ledge, center = (min + max) * 0.5f, size = max - min, solid = true },
        };

        [Test]
        public void AnUprightModelOnItsBoxPasses()
        {
            var parts = new List<HousePart> { Floor(0f), Chair(new Vector3(0f, 0f, 0f), new Vector3(0.45f, 0.95f, 0.55f)) };
            var drawn = new List<HouseDrawn>
            {
                new HouseDrawn { name = "Chair model", kind = DrawnKind.Model, owner = 1, min = new Vector3(0.01f, 0f, 0.02f), max = new Vector3(0.44f, 0.94f, 0.53f), realSize = new Vector3(0.43f, 0.96f, 0.54f), fittedSize = new Vector3(0.43f, 0.94f, 0.51f) },
            };
            var issues = HouseMeshLint.Check(parts, drawn);
            Assert.That(issues, Is.Empty, string.Join("\n", issues));
        }

        [Test]
        public void AModelOnItsSideIsCaught()
        {
            var parts = new List<HousePart> { Floor(0f), Chair(new Vector3(0f, 0f, 0f), new Vector3(0.96f, 0.6f, 0.55f)) };
            // A chair (0.43 x 0.96 x 0.54 upright) lying on its back: 0.96 long, 0.54 tall.
            var drawn = new List<HouseDrawn>
            {
                new HouseDrawn { name = "Chair model", kind = DrawnKind.Model, owner = 1, min = new Vector3(0f, 0f, 0f), max = new Vector3(0.95f, 0.54f, 0.43f), realSize = new Vector3(0.43f, 0.96f, 0.54f), fittedSize = new Vector3(0.43f, 0.54f, 0.95f) },
            };
            var issues = HouseMeshLint.Check(parts, drawn);
            Assert.That(issues.Any(i => i.kind == "sideways"), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void FloatingOverlappingAndOutsideAreCaught()
        {
            var parts = new List<HousePart> { Floor(0f), Wall(2f), Chair(new Vector3(0f, 0f, 0f), new Vector3(0.45f, 0.95f, 0.55f)) };
            var drawn = new List<HouseDrawn>
            {
                new HouseDrawn { name = "A box in the air", kind = DrawnKind.Free, owner = -1, min = new Vector3(-1f, 0.6f, -1f), max = new Vector3(-0.8f, 0.8f, -0.8f) },
                new HouseDrawn { name = "A lamp through the wall", kind = DrawnKind.Free, owner = -1, min = new Vector3(1.7f, 0f, 1f), max = new Vector3(2.05f, 0.5f, 1.3f) },
                new HouseDrawn { name = "A tread past its part", kind = DrawnKind.Owned, owner = 2, min = new Vector3(0f, 0.4f, 0f), max = new Vector3(0.6f, 0.45f, 0.55f) },
            };
            var issues = HouseMeshLint.Check(parts, drawn);
            TestContext.Out.WriteLine(string.Join("\n", issues));
            Assert.That(issues.Any(i => i.kind == "floating" && i.what.Contains("in the air")), Is.True);
            Assert.That(issues.Any(i => i.kind == "overlap" && i.what.Contains("through the wall")), Is.True);
            Assert.That(issues.Any(i => i.kind == "outside" && i.what.Contains("past its part")), Is.True);
        }

        [Test]
        public void TwoModelsIntoEachOtherAreCaught()
        {
            var parts = new List<HousePart> { Floor(0f) };
            var drawn = new List<HouseDrawn>
            {
                new HouseDrawn { name = "Theo (cut-out)", kind = DrawnKind.Free, owner = -1, min = new Vector3(0f, 0f, 0f), max = new Vector3(0.6f, 1.6f, 0.02f) },
                new HouseDrawn { name = "Chair", kind = DrawnKind.Free, owner = -1, min = new Vector3(0.3f, 0f, -0.2f), max = new Vector3(0.75f, 0.95f, 0.3f) },
            };
            Assert.That(HouseMeshLint.Check(parts, drawn).Any(i => i.kind == "overlap"), Is.True);
        }

        [Test]
        public void EveryFurniturePartRestsOrHangs()
        {
            var h = DreamHouse.Build();
            var issues = HouseMeshLint.PlanParts(h.parts);
            foreach (var i in issues) TestContext.Out.WriteLine(i);
            Assert.That(issues, Is.Empty);
        }

        // =====================================================================================================
        // On foot: every flight up and down
        // =====================================================================================================

        /// <summary>Walks straight at <paramref name="to"/> (through <paramref name="via"/> first) at a walk, then a sprint:
        /// true when she gets within 0.5 m of it (and within a metre of its height) before <paramref name="seconds"/>.</summary>
        static bool Climb(BoxWorld world, Vector3 from, Vector3[] via, out string log, bool sprint, float seconds = 20f)
        {
            var sim = new MoveSim(world, new MoveDef());
            var driver = new MoveDriver(sim);
            driver.Teleport(from + new Vector3(0f, 0.05f, 0f), 0f, Vector3.Zero);
            int k = 0;
            const float dt = 1f / 60f;
            float best = float.MaxValue, still = 0f;
            for (float t = 0f; t < seconds; t += dt)
            {
                var p = sim.Position;
                var target = via[k];
                var d = new Vector2(target.X - p.X, target.Z - p.Z);
                if (d.Length() < 0.5f && Math.Abs(target.Y - p.Y) < 1.0f)
                {
                    k++;
                    best = float.MaxValue;
                    if (k >= via.Length) { log = $"there in {t:0.0} s"; return true; }
                    continue;
                }
                float dist = d.Length() + Math.Abs(target.Y - p.Y);
                if (dist < best - 0.01f) { best = dist; still = 0f; } else still += dt;
                if (still > 2.5f) { log = $"stalled at {p} heading for {target}"; return false; }
                float yaw = (float)(Math.Atan2(d.X, d.Y) * 180.0 / Math.PI);
                driver.Frame(dt, new MoveInput { move = new Vector2(0f, 1f), yaw = yaw, sprintHeld = sprint });
                if (p.Y < -20f) { log = $"fell at {p}"; return false; }
            }
            log = $"out of time at {sim.Position}";
            return false;
        }

        [Test]
        public void EveryFlightClimbsUpAndDownWithoutStalling([Values(false, true)] bool sprint)
        {
            var h = DreamHouse.Build();
            var world = DreamHouseTests.WorldOf(h);
            var fails = new List<string>();
            Assert.That(h.stairs.Any(s => s.name.Contains("treehouse")), Is.True, "the treehouse ladder is a flight");
            foreach (var s in h.stairs)
            {
                // Off the bottom end and off the top end, on the flight's centre line.
                const float off = 0.7f;
                Vector3 low, high, lowIn, highIn;
                if (s.alongX)
                {
                    float z = s.rect.CZ;
                    low = s.up > 0 ? new Vector3(s.rect.x0 - off, s.bottomY, z) : new Vector3(s.rect.x1 + off, s.bottomY, z);
                    high = s.up > 0 ? new Vector3(s.rect.x1 + off, s.topY, z) : new Vector3(s.rect.x0 - off, s.topY, z);
                }
                else
                {
                    float x = s.rect.CX;
                    low = s.up > 0 ? new Vector3(x, s.bottomY, s.rect.z0 - off) : new Vector3(x, s.bottomY, s.rect.z1 + off);
                    high = s.up > 0 ? new Vector3(x, s.topY, s.rect.z1 + off) : new Vector3(x, s.topY, s.rect.z0 - off);
                }
                lowIn = low; highIn = high;
                if (!Climb(world, low, new[] { highIn }, out var up, sprint)) fails.Add($"{s.name}: up: {up}");
                else TestContext.Out.WriteLine($"{s.name}: up {up}");
                if (!Climb(world, high, new[] { lowIn }, out var down, sprint)) fails.Add($"{s.name}: down: {down}");
                else TestContext.Out.WriteLine($"{s.name}: down {down}");
            }
            foreach (var f in fails) TestContext.Out.WriteLine(f);
            Assert.That(fails, Is.Empty);
        }

        /// <summary>The player takes a memory looking at it from 2.3 m (HouseMemoryView.InReach, from her eyes); the
        /// autopilot from 2.4 m of her eyes. Every place a memory can be must be within 2.1 m of her eyes where she stands
        /// to take it, or nobody can take it from there.</summary>
        [Test]
        public void EveryMemoryIsInReachOfWhereSheStandsForIt()
        {
            var h = DreamHouse.Build();
            const float eye = 1.5f;
            var fails = new List<string>();
            foreach (var m in h.memories)
            {
                var spots = new List<(Vector3 at, Vector3 stand)> { (m.position, m.standAt) };
                for (int i = 0; i < m.altPositions.Count; i++) spots.Add((m.altPositions[i], m.altStandAt[i]));
                foreach (var (at, stand) in spots)
                {
                    float d = Vector3.Distance(at, stand + new Vector3(0f, m.atTable ? 1.05f : eye, 0f));
                    if (d > 2.1f) fails.Add($"{m.name} at {at} is {d:0.00} m from the eyes at {stand}");
                }
            }
            foreach (var f in fails) TestContext.Out.WriteLine(f);
            Assert.That(fails, Is.Empty);
        }
    }
}

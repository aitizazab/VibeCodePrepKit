using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>The arena demo's walker (<see cref="CoverPath"/>, fix "stuck" 2026-10-14). Passes 20261014-0700 and -0735
    /// stalled the food court for good: the demo, ducking inside the information booth's U (the food court's hide), was sent
    /// north toward a drone and walked into the booth's north screen for the last 100 s of the recording (the old walker
    /// ignored a box whose margin it stood in, and its one-corner detour can't leave a U). A walker stepping along
    /// <see cref="CoverPath.Next"/> must get out of the booth and reach every open spot in the hall without touching a piece.</summary>
    public class CoverPathTests
    {
        // the demo's own numbers (SkylineArenaDemo: CoverMargin, CornerMargin)
        const float Margin = 0.75f, Corner = 0.9f, Inner = 0.3f;
        // the walker's body in this test: just inside the inner margin
        const float Body = 0.28f;

        static List<(Vector2 min, Vector2 max)> HallBoxes()
        {
            var list = new List<(Vector2, Vector2)>();
            foreach (var b in GalleriaArena.HallCover()) list.Add((new Vector2(b.min.X, b.min.Z), new Vector2(b.max.X, b.max.Z)));
            var l = GalleriaArena.LiftLobby;
            list.Add((new Vector2(l.min.X, l.min.Z), new Vector2(l.max.X, l.max.Z)));
            return list;
        }

        static bool Touches(List<(Vector2 min, Vector2 max)> boxes, Vector2 p, float r)
        {
            foreach (var (min, max) in boxes)
                if (p.X > min.X - r && p.X < max.X + r && p.Y > min.Y - r && p.Y < max.Y + r) return true;
            return false;
        }

        /// <summary>Walks from <paramref name="start"/> toward <paramref name="goal"/> in 0.1 m steps (re-planning every step, as
        /// the demo does every frame); fails on touching a piece; true once within 0.3 m of the goal.</summary>
        static bool Walk(CoverPath path, List<(Vector2 min, Vector2 max)> boxes, Vector2 start, Vector2 goal, int maxSteps, out string why)
        {
            var p = start;
            for (int s = 0; s < maxSteps; s++)
            {
                if (Vector2.Distance(p, goal) <= 0.3f) { why = ""; return true; }
                var next = path.Next(p, goal);
                var d = next - p;
                float len = d.Length();
                if (len < 1e-5f) { why = "no way on at " + p; return false; }
                var q = p + d / len * MathF.Min(0.1f, len);
                if (Touches(boxes, q, Body)) { why = "walked into a piece at " + q + " heading for " + next; return false; }
                p = q;
            }
            why = "not there after " + maxSteps + " steps (at " + p + ")";
            return false;
        }

        [TestCase(0f, -10.5f)]     // within the north screen's margin, under the CONCIERGE sign (where 0735 froze)
        [TestCase(1.9f, -10.4f)]   // the U's north-east inner corner
        [TestCase(-1.9f, -10.4f)]  // the north-west inner corner
        [TestCase(0f, -11.2f)]     // the duck spot, 1 m off the north screen
        public void FromInsideTheBoothItGetsOutAndNorthToTheDrones(float x, float z)
        {
            var boxes = HallBoxes();
            var path = new CoverPath(boxes, Margin, Corner, Inner);
            var start = new Vector2(x, z);
            Assert.That(Touches(boxes, start, Body), Is.False);
            // the north group's drones hover over (-9..9, 9..9.5); a firing spot north of the booth
            foreach (var goal in new[] { new Vector2(0f, -7.5f), new Vector2(0f, 6f), new Vector2(-9f, 8f), new Vector2(9f, 8f) })
                Assert.That(Walk(path, boxes, start, goal, 600, out var why), Is.True, "from " + start + " to " + goal + ": " + why);
        }

        [Test]
        public void TheWalkIntoTheNorthScreenIsNotTakenFromInsideItsMargin()
        {
            var path = new CoverPath(HallBoxes(), Margin, Corner, Inner);
            // the booth's north screen: x -2.7..2.7, z -10..-9.6; standing 0.5 m south of it, the goal 10 m north
            Assert.That(path.Clear(new Vector2(0f, -10.5f), new Vector2(0f, 0f)), Is.False);
            var next = path.Next(new Vector2(0f, -10.5f), new Vector2(0f, 0f));
            Assert.That(next.Y, Is.LessThan(-10.5f), "the way out of the U is south first, got " + next);
        }

        [Test]
        public void EveryOpenSpotInTheHallReachesEveryOther()
        {
            var boxes = HallBoxes();
            var path = new CoverPath(boxes, Margin, Corner, Inner);
            var rng = new Random(20261014);
            Vector2 Spot()
            {
                while (true)
                {
                    var p = new Vector2((float)(rng.NextDouble() * 46 - 23), (float)(rng.NextDouble() * 34 - 19));
                    if (!Touches(boxes, p, 0.4f)) return p;
                }
            }
            for (int k = 0; k < 300; k++)
            {
                var a = Spot();
                var b = Spot();
                Assert.That(Walk(path, boxes, a, b, 1500, out var why), Is.True, "from " + a + " to " + b + ": " + why);
            }
        }

        [Test]
        public void AGoalInsideAPieceLeadsToItsNearestReachableSideWithoutWalkingIntoIt()
        {
            var boxes = HallBoxes();
            var path = new CoverPath(boxes, Margin, Corner, Inner);
            var goal = new Vector2(6f, 3f);   // the middle of the east kiosk (a drone's spot over it, FiringSpot's fallback)
            var p = new Vector2(-10f, -15f);
            for (int s = 0; s < 800; s++)
            {
                var next = path.Next(p, goal);
                var d = next - p;
                float len = d.Length();
                if (len < 1e-5f) break;
                var q = p + d / len * MathF.Min(0.1f, len);
                if (Touches(boxes, q, Body)) break;   // pressed against it: as near as it gets
                p = q;
            }
            Assert.That(Vector2.Distance(p, goal), Is.LessThan(3.5f), "ended at " + p);
        }
    }
}

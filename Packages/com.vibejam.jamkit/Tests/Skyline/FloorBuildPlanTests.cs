using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>Task B1: the pure half of the floor builder. The wedge a ramp is built from (so the Unity collider equals the
    /// data's box and its slope is the data's) and the FloorDataMatch rule (every solid has one collider and vice versa).</summary>
    public class FloorBuildPlanTests
    {
        static FloorSolid[] AllRamps()
        {
            var l = new List<FloorSolid>();
            l.AddRange(RepossessArena.Solids().Where(s => s.ramp));
            l.AddRange(HotelLobby.Solids().Where(s => s.ramp));
            return l.ToArray();
        }

        static FloorSolid[] EverySolid()
        {
            var l = new List<FloorSolid>(RepossessArena.Solids());
            l.AddRange(HotelLobby.Solids());
            return l.ToArray();
        }

        static PlacedCollider Placed(FloorSolid s, string path = null) => new PlacedCollider
        {
            path = path ?? ("Arena/" + s.id), min = s.box.min, max = s.box.max, runnable = s.runnable, glass = s.glass,
        };

        // ------------------------------------------------------------------ the wedge

        [Test]
        public void ThereAreRampsToBuild()
        {
            Assert.That(AllRamps().Length, Is.GreaterThanOrEqualTo(7), "four pit ramps, the chute, two escalators, the lobby's stairs");
        }

        [Test]
        public void EveryWedgeHasTheDataBoxAsItsBounds()
        {
            foreach (var s in AllRamps())
            {
                var b = FloorMeshes.Wedge(s).Bounds();
                Assert.That(b.min.X, Is.EqualTo(s.box.min.X).Within(1e-4f), s.id + " min x");
                Assert.That(b.min.Y, Is.EqualTo(s.box.min.Y).Within(1e-4f), s.id + " min y");
                Assert.That(b.min.Z, Is.EqualTo(s.box.min.Z).Within(1e-4f), s.id + " min z");
                Assert.That(b.max.X, Is.EqualTo(s.box.max.X).Within(1e-4f), s.id + " max x");
                Assert.That(b.max.Y, Is.EqualTo(s.box.max.Y).Within(1e-4f), s.id + " max y");
                Assert.That(b.max.Z, Is.EqualTo(s.box.max.Z).Within(1e-4f), s.id + " max z");
            }
        }

        [Test]
        public void EveryWedgeFaceLooksOutwardAndTheSolidHasTheRightVolume()
        {
            foreach (var s in AllRamps())
            {
                var m = FloorMeshes.Wedge(s);
                Assert.That(m.triangles.Length % 3, Is.EqualTo(0), s.id);
                Assert.That(m.positions.Length, Is.EqualTo(m.normals.Length), s.id);
                var mid = Vector3.Zero;
                foreach (var p in m.positions) mid += p;
                mid /= m.positions.Length;
                float volume = 0f;
                for (int i = 0; i < m.triangles.Length; i += 3)
                {
                    var a = m.positions[m.triangles[i]]; var b = m.positions[m.triangles[i + 1]]; var c = m.positions[m.triangles[i + 2]];
                    var n = Vector3.Cross(b - a, c - a);
                    Assert.That(n.Length(), Is.GreaterThan(1e-4f), s.id + " has a degenerate triangle");
                    Assert.That(Vector3.Dot(n, (a + b + c) / 3f - mid), Is.GreaterThan(0f), s.id + " has a face turned inwards");
                    Assert.That(Vector3.Dot(Vector3.Normalize(n), m.normals[m.triangles[i]]), Is.GreaterThan(0.99f), s.id + " normal disagrees with winding");
                    volume += Vector3.Dot(a, Vector3.Cross(b, c)) / 6f;      // signed volume of the tetrahedron to the origin
                }
                float w = (s.box.max.X - s.box.min.X) * (s.box.max.Z - s.box.min.Z);
                float expected = w * ((s.rampTopStart + s.rampTopEnd) * 0.5f - s.box.min.Y);
                Assert.That(volume, Is.EqualTo(expected).Within(0.01f * Math.Max(1f, expected)), s.id + " volume (closed, outward)");
            }
        }

        [Test]
        public void TheWedgeTopFollowsTheSolidsSlope()
        {
            foreach (var s in AllRamps())
            {
                var m = FloorMeshes.Wedge(s);
                int checkedVerts = 0;
                for (int i = 0; i < m.positions.Length; i++)
                {
                    if (m.normals[i].Y < 0.2f) continue;                       // not the sloped top
                    var p = m.positions[i];
                    Assert.That(p.Y, Is.EqualTo(s.TopAt(p.X, p.Z)).Within(1e-3f), s.id + " top vertex off the slope");
                    checkedVerts++;
                }
                Assert.That(checkedVerts, Is.GreaterThanOrEqualTo(4), s.id + " has a top face");
            }
        }

        [Test]
        public void AFlatSolidIsNotAWedge()
        {
            Assert.Throws<ArgumentException>(() => FloorMeshes.Wedge(RepossessArena.Find("kiosk")));
            Assert.Throws<ArgumentNullException>(() => FloorMeshes.Wedge(null));
        }

        [Test]
        public void ARampThatMeetsItsFloorLeavesOutTheEmptyEnd()
        {
            var s = RepossessArena.Find("ramp-n");     // bottom -4, runs from -4 up to 0: its low end has no height
            var m = FloorMeshes.Wedge(s);
            Assert.That(m.triangles.Length / 3, Is.EqualTo(8), "bottom, top and the high end two triangles each, the two sides one each");
        }

        // ------------------------------------------------------------------ the match

        [Test]
        public void TheDataAgainstItselfMatches()
        {
            var solids = EverySolid();
            var colliders = solids.Select(s => Placed(s)).ToList();
            Assert.That(FloorDataMatch.Match(solids, colliders), Is.Empty);
        }

        [Test]
        public void TheOrderOfTheCollidersDoesNotMatter()
        {
            var solids = EverySolid();
            var colliders = solids.Select(s => Placed(s)).Reverse().ToList();
            Assert.That(FloorDataMatch.Match(solids, colliders), Is.Empty);
        }

        [Test]
        public void ASolidWithoutACollider()
        {
            var solids = EverySolid();
            var colliders = solids.Where(s => s.id != "kiosk").Select(s => Placed(s)).ToList();
            var problems = FloorDataMatch.Match(solids, colliders);
            Assert.That(problems.Count, Is.EqualTo(1));
            Assert.That(problems[0], Does.Contain("no collider for solid kiosk"));
        }

        [Test]
        public void AColliderWithoutASolid()
        {
            var solids = EverySolid();
            var colliders = solids.Select(s => Placed(s)).ToList();
            colliders.Add(new PlacedCollider { path = "Arena/Stray", min = new Vector3(0, 0, 0), max = new Vector3(1, 1, 1) });
            var problems = FloorDataMatch.Match(solids, colliders);
            Assert.That(problems.Count, Is.EqualTo(1));
            Assert.That(problems[0], Does.Contain("no solid for collider Arena/Stray"));
        }

        [Test]
        public void ADoubledColliderIsReported()
        {
            var solids = EverySolid();
            var colliders = solids.Select(s => Placed(s)).ToList();
            colliders.Add(Placed(RepossessArena.Find("kiosk"), "Arena/Kiosk Copy"));
            var problems = FloorDataMatch.Match(solids, colliders);
            Assert.That(problems.Count, Is.EqualTo(1));
            Assert.That(problems[0], Does.Contain("Arena/Kiosk Copy"));
        }

        [Test]
        public void AMovedColliderIsAMissingAndAnExtra()
        {
            var solids = EverySolid();
            var colliders = solids.Select(s => Placed(s)).ToList();
            var moved = colliders.First(c => c.path == "Arena/kiosk");
            moved.min += new Vector3(0.5f, 0, 0);
            moved.max += new Vector3(0.5f, 0, 0);
            var problems = FloorDataMatch.Match(solids, colliders);
            Assert.That(problems.Count, Is.EqualTo(2));
            Assert.That(problems.Any(p => p.Contains("no collider for solid kiosk")));
            Assert.That(problems.Any(p => p.Contains("no solid for collider Arena/kiosk")));
        }

        [Test]
        public void AMillimetreOfSlackIsFine()
        {
            var solids = EverySolid();
            var colliders = solids.Select(s => Placed(s)).ToList();
            foreach (var c in colliders) { c.min += new Vector3(0.002f); c.max -= new Vector3(0.002f); }
            Assert.That(FloorDataMatch.Match(solids, colliders), Is.Empty);
        }

        [Test]
        public void TheRunnableFlagAndTheGlassLayerMustAgree()
        {
            var solids = EverySolid();
            var colliders = solids.Select(s => Placed(s)).ToList();
            colliders.First(c => c.path == "Arena/pit-wall-n1").runnable = false;
            colliders.First(c => c.path == "Arena/" + RepossessArena.PaneId(1, 3)).glass = false;
            var problems = FloorDataMatch.Match(solids, colliders);
            Assert.That(problems.Count, Is.EqualTo(2));
            Assert.That(problems.Any(p => p.Contains("pit-wall-n1 runnable is False in the scene, True in the data")));
            Assert.That(problems.Any(p => p.Contains(RepossessArena.PaneId(1, 3)) && p.Contains("glass in the data but not on the glass layer")));
        }
    }
}

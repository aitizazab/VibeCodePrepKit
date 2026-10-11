using System;
using System.Numerics;
using JamKit.Movement;
using JamKit.Stealth;
using NUnit.Framework;

namespace JamKit.Tests.Stealth
{
    /// <summary>The Stealth Lab's yard is sound: the seeker's graph is one piece and reaches every hiding spot; nothing
    /// to walk to sits inside a solid box; the movement routes fit the movement kit's real limits (mantle heights, the
    /// slide-under bar, a wall long enough to run); each collectible has its own route.</summary>
    public class StealthYardTests
    {
        [Test]
        public void TheGraphIsOnePieceAndReachesEveryHidingSpot()
        {
            var y = StealthYard.Build();
            var w = y.World();
            var g = y.Graph(w);
            Assert.That(g.Connected(), Is.True, "every walking point reaches every other");
            var path = new System.Collections.Generic.List<int>();
            foreach (var s in y.spots)
            {
                int n = g.NearestReachable(w, s.Approach);
                Assert.That(WaypointGraph.WalkClear(w, new Vector3(s.Approach.X, 0f, s.Approach.Z), g[n]), Is.True, s.name + ": a clear walk to its check point");
                Assert.That(g.FindPath(0, n, path), Is.True, s.name);
            }
            foreach (int p in y.patrol) Assert.That(p, Is.InRange(0, g.Count - 1));
        }

        [Test]
        public void NothingToWalkToOrPickUpIsInsideASolidBox()
        {
            var y = StealthYard.Build();
            bool Inside(Vector3 p)
            {
                foreach (var piece in y.pieces)
                {
                    if (!piece.Solid || piece.kind == YardPieceKind.Floor) continue;
                    var mn = piece.Min - new Vector3(0.3f, 0f, 0.3f);
                    var mx = piece.Max + new Vector3(0.3f, 0f, 0.3f);
                    if (p.X > mn.X && p.X < mx.X && p.Z > mn.Z && p.Z < mx.Z && p.Y + 0.1f > mn.Y && p.Y < mx.Y) return true;
                }
                return false;
            }
            foreach (var n in y.nodes) Assert.That(Inside(n), Is.False, "node " + n);
            foreach (var s in y.spots) Assert.That(Inside(s.Approach), Is.False, s.name + " check point");
            foreach (var c in y.collectibles) Assert.That(Inside(c.position), Is.False, c.id);
            Assert.That(Inside(y.playerStart), Is.False);
            Assert.That(Inside(y.seekerStart), Is.False);
        }

        [Test]
        public void TheRoutesFitTheMovementKit()
        {
            var y = StealthYard.Build();
            var d = new MoveDef();
            float Top(string name) => y.Find(name).Max.Y;
            // Mantle: the crate from the ground, the shed roof from the crate.
            Assert.That(Top("Crate"), Is.InRange(0.4f, 1.9f), "the crate mantles from the ground");
            Assert.That(Top("Shed") - Top("Crate"), Is.InRange(0.4f, 1.9f), "the shed roof mantles from the crate");
            Assert.That(Math.Abs(y.Find("Crate").Max.X - y.Find("Shed").Min.X), Is.LessThan(0.05f), "the crate stands against the shed");
            Assert.That(Top("Deck"), Is.InRange(0.4f, 1.9f), "the deck mantles from the ground");
            Assert.That(Top("Lookout"), Is.GreaterThan(1.9f + 0.2f), "the lookout needs more than a mantle (the wallrun)");
            // Slide under: the deck and the fence bar, the Movement Lab's 1.15 m underside.
            float under = y.Find("Deck").Min.Y;
            Assert.That(under, Is.GreaterThan(d.slideHeight + 0.1f).And.LessThan(d.standHeight), "the deck: slide or crawl under, no walking");
            Assert.That(y.Find("Fence bar").Min.Y, Is.EqualTo(under).Within(0.01f));
            // Wallrun: the long wall is long and tall enough.
            var wall = y.Find("Run wall");
            Assert.That(wall.size.Z, Is.GreaterThanOrEqualTo(12f));
            Assert.That(wall.Max.Y, Is.GreaterThanOrEqualTo(3.5f));
            Assert.That(wall.Max.Z, Is.LessThan(y.Find("Lookout").Min.Z), "the lookout is past the wall's end");
            // Five collectibles, five different routes.
            Assert.That(y.collectibles.Count, Is.EqualTo(5));
            var routes = new System.Collections.Generic.HashSet<string>();
            foreach (var c in y.collectibles) routes.Add(c.route);
            Assert.That(routes.Count, Is.EqualTo(5));
            // The closet's door fits the player (1 m wide, 1.8 m high).
            Assert.That(y.Find("Closet lintel").Min.Y, Is.GreaterThan(d.standHeight + 0.05f));
        }

        [Test]
        public void LightAndShadeAreWhereTheYardSays()
        {
            var y = StealthYard.Build();
            var w = y.World();
            Assert.That(w.LightAt(new Vector3(0f, 1f, -2f)), Is.EqualTo(1f), "under the lamp");
            Assert.That(w.LightAt(new Vector3(-16f, 1f, 18f)), Is.LessThan(0.2f), "the alley");
            Assert.That(w.LightAt(new Vector3(19f, 1f, -12f)), Is.LessThan(0.2f), "the canopy's shade, where a bush is");
            Assert.That(w.LightAt(new Vector3(10f, 1f, 10f)), Is.EqualTo(y.ambientLight));
        }
    }
}

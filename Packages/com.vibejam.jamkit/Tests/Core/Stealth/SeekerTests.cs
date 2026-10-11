using System.Collections.Generic;
using System.Numerics;
using JamKit.Stealth;
using NUnit.Framework;

namespace JamKit.Tests.Stealth
{
    /// <summary>The seeker (docs/STEALTH.md): the alert states and their order, noise, the search with its hiding-spot
    /// checks, hide-and-seek in the Stealth Lab's yard (a stationary player in the open, one in the closet), the patrol
    /// loop; and the collectibles counter.</summary>
    public class SeekerTests
    {
        const float Dt = 1f / 60f;

        static SeekerBrain Open(StealthDef def = null)
        {
            var w = new StealthBoxWorld();
            var g = new WaypointGraph();
            g.Add(new Vector3(0f, 0f, 0f));
            g.Add(new Vector3(0f, 0f, 10f));
            g.Add(new Vector3(10f, 0f, 10f));
            g.AutoConnect(w, 20f);
            var b = new SeekerBrain(def ?? new StealthDef(), w, g, new List<HidingSpot>(), Vector3.Zero, 0f);
            return b;
        }

        static List<AlertState> Run(SeekerBrain b, StealthTarget t, float seconds, System.Func<SeekerBrain, bool> until = null)
        {
            var seen = new List<AlertState> { b.State };
            for (float s = 0f; s < seconds; s += Dt)
            {
                b.Tick(Dt, t, default);
                if (b.Events.stateChanged) seen.Add(b.Events.to);
                if (until != null && until(b)) break;
            }
            return seen;
        }

        [Test]
        public void AGlimpseMakesItSuspiciousThenItSearchesThenGivesUp()
        {
            var b = Open();
            var far = StealthTarget.At(new Vector3(0f, 0f, 15f));
            far.speed = 2f;
            Run(b, far, 3f, s => s.State == AlertState.Suspicious);
            Assert.That(b.State, Is.EqualTo(AlertState.Suspicious), "a far glimpse");
            Assert.That(b.Meter, Is.InRange(b.def.suspiciousAt, 0.6f), "the meter is only part full");
            var gone = default(StealthTarget);
            var states = Run(b, gone, 40f, s => s.State == AlertState.Unaware);
            Assert.That(states, Is.EqualTo(new[] { AlertState.Suspicious, AlertState.Searching, AlertState.Unaware }), "suspicious, searching, back to unaware");
            Assert.That(Vector3.Distance(b.LastKnown, new Vector3(0f, 0f, 15f)), Is.LessThan(0.01f));
        }

        [Test]
        public void SteadySightFillsTheMeterToFoundAndLosingYouStartsASearch()
        {
            var b = Open();
            var t = StealthTarget.At(new Vector3(0f, 0f, 6f));
            t.speed = 2f;
            float time = 0f;
            bool found = false;
            for (; time < 5f && !found; time += Dt)
            {
                b.Tick(Dt, t, default);
                found = b.Events.found;
            }
            Assert.That(found, Is.True);
            Assert.That(time, Is.InRange(0.6f, 2f), "over time: about a second at 6 m in the open");
            Assert.That(b.State, Is.EqualTo(AlertState.Found));
            Run(b, default, b.def.loseSightTime + 0.5f);
            Assert.That(b.State, Is.EqualTo(AlertState.Searching), "lost: back to searching at the last known position");
        }

        [Test]
        public void ANoiseBehindItTurnsItAroundAndSendsItToLook()
        {
            var b = Open();
            var noise = StealthNoise.Make(new Vector3(0f, 0f, -8f), 12f, NoiseKind.Landing);
            b.Tick(Dt, default, noise);
            Assert.That(b.Events.heard, Is.True);
            Assert.That(b.State, Is.EqualTo(AlertState.Suspicious));
            Run(b, default, 1f);
            Assert.That(System.Math.Abs(b.Yaw), Is.GreaterThan(150f), "turned to face it");
            Run(b, default, 10f, s => s.State == AlertState.Searching);
            Assert.That(b.State, Is.EqualTo(AlertState.Searching));
            Assert.That(Vector3.Distance(b.Position, new Vector3(0f, 0f, -8f)), Is.LessThan(1f), "went to where the noise was");
            var faint = StealthNoise.Make(new Vector3(0f, 0f, 30f), 5f, NoiseKind.Footsteps);
            var b2 = Open();
            b2.Tick(Dt, default, faint);
            Assert.That(b2.Events.heard, Is.False, "too far to hear");
        }

        [Test]
        public void ASearchChecksTheHidingSpotNearTheLastKnownPositionAndFindsYouInIt()
        {
            var w = new StealthBoxWorld();
            var g = new WaypointGraph();
            g.Add(Vector3.Zero);
            var spots = new List<HidingSpot>
            {
                HidingSpot.Box("Closet", HideKind.Closet, new Vector3(6f, 0.9f, 8f), new Vector3(1.5f, 1.8f, 1.5f), new Vector3(4.5f, 0f, 8f)),
                HidingSpot.Box("Far bush", HideKind.Bush, new Vector3(-30f, 0.8f, 0f), new Vector3(2f, 1.6f, 2f), new Vector3(-28f, 0f, 0f)),
            };
            var b = new SeekerBrain(new StealthDef(), w, g, spots, Vector3.Zero, 0f);
            var t = StealthTarget.At(new Vector3(5f, 0f, 8f));
            t.speed = 3f;
            Run(b, t, 3f, s => s.State == AlertState.Suspicious);
            // The target steps into the closet.
            t.feet = new Vector3(6f, 0f, 8f);
            t.hidingSpot = 0;
            t.speed = 0f;
            bool found = false;
            int checkedSpot = -1;
            for (float s = 0f; s < 30f && !found; s += Dt)
            {
                b.Tick(Dt, t, default);
                if (b.Events.checkedSpot >= 0) checkedSpot = b.Events.checkedSpot;
                found = b.Events.found;
            }
            Assert.That(found, Is.True, "it opened the closet");
            Assert.That(b.Events.foundInSpot, Is.EqualTo(0));
            Assert.That(checkedSpot, Is.EqualTo(0));
            Assert.That(b.State, Is.EqualTo(AlertState.Found));
        }

        // ---------- The Stealth Lab's yard ----------

        static SeekerBrain YardSeeker(StealthYard y, out StealthBoxWorld world)
        {
            world = y.World();
            var g = y.Graph(world);
            var b = new SeekerBrain(new StealthDef(), world, g, y.spots, y.seekerStart, y.seekerYaw);
            b.SetPatrol(y.patrol);
            return b;
        }

        static float FindTime(SeekerBrain b, StealthTarget t, float limit, out int inSpot)
        {
            inSpot = -1;
            for (float s = 0f; s < limit; s += Dt)
            {
                b.Tick(Dt, t, default);
                if (b.Events.found) { inSpot = b.Events.foundInSpot; return s; }
            }
            return -1f;
        }

        [Test]
        public void InTheYardTheSeekerCountsThenFindsAStationaryPlayerInTheOpen()
        {
            var y = StealthYard.Build();
            var b = YardSeeker(y, out _);
            b.StartHideAndSeek(y.seekerStart, y.countYaw);
            var t = StealthTarget.At(new Vector3(-14f, 0f, -12f));
            float t0 = 0f;
            for (; t0 < b.def.countTime - 0.1f; t0 += Dt)
            {
                b.Tick(Dt, t, default);
                Assert.That(b.Meter, Is.EqualTo(0f), "eyes closed while counting");
            }
            float found = FindTime(b, t, 90f, out int spot);
            TestContext.Out.WriteLine($"found a player standing still in the open after {found:0.0} s of seeking");
            Assert.That(found, Is.GreaterThan(0f), "found within 90 s");
            Assert.That(spot, Is.EqualTo(-1), "by sight");
        }

        [Test]
        public void InTheYardTheSeekerFindsAPlayerHiddenInTheClosetByCheckingIt()
        {
            var y = StealthYard.Build();
            var b = YardSeeker(y, out _);
            int closet = y.spots.FindIndex(s => s.kind == HideKind.Closet);
            b.StartHideAndSeek(y.seekerStart, y.countYaw, 0f);
            var t = StealthTarget.At(new Vector3(-9.2f, 0f, 10.3f));
            t.hidingSpot = y.SpotAt(t.feet);
            Assert.That(t.hidingSpot, Is.EqualTo(closet));
            float found = FindTime(b, t, 240f, out int spot);
            TestContext.Out.WriteLine($"found in the closet after {found:0.0} s (sweeps {b.Sweeps})");
            Assert.That(found, Is.GreaterThan(0f), "a full sweep opens every spot");
            Assert.That(spot, Is.EqualTo(closet));
        }

        [Test]
        public void ASweepReachesTheDarkAlleyAndUnderTheDeckWithoutWalkingThroughWalls()
        {
            var y = StealthYard.Build();
            foreach (var at in new[] { new Vector3(-21f, 0f, 18f), new Vector3(-4f, 0f, -13f), new Vector3(20f, 0f, 15f) })
            {
                var b = YardSeeker(y, out _);
                b.StartHideAndSeek(y.seekerStart, y.countYaw, 0f);
                var t = StealthTarget.At(at);
                t.hidingSpot = y.SpotAt(at);
                if (t.hidingSpot >= 0) { t.crouched = true; t.height = 1f; }
                float found = -1f;
                for (float s = 0f; s < 240f && found < 0f; s += Dt)
                {
                    b.Tick(Dt, t, default);
                    if (b.Events.found) found = s;
                    var q = b.Position;
                    foreach (var p in y.pieces)
                    {
                        if (!p.Solid || p.kind == YardPieceKind.Floor || p.Min.Y > 1.5f || p.Max.Y < 0.2f) continue;
                        bool inside = q.X > p.Min.X + 0.05f && q.X < p.Max.X - 0.05f && q.Z > p.Min.Z + 0.05f && q.Z < p.Max.Z - 0.05f;
                        Assert.That(inside, Is.False, $"the seeker walked into {p.name} at {q}");
                    }
                }
                TestContext.Out.WriteLine($"a player at {at} (spot {t.hidingSpot}) found after {found:0.0} s");
                Assert.That(found, Is.GreaterThan(0f), "found at " + at);
            }
        }

        [Test]
        public void APatrolWalksItsWholeLoopWithoutGettingStuck()
        {
            var y = StealthYard.Build();
            var b = YardSeeker(y, out _);
            var g = b.Graph;
            var visited = new HashSet<int>();
            Vector3 last = b.Position;
            float stillFor = 0f;
            for (float s = 0f; s < 240f && visited.Count < y.patrol.Count; s += Dt)
            {
                b.Tick(Dt, default, default);
                foreach (int n in y.patrol)
                    if (Vector3.Distance(b.Position, g[n]) < 0.6f) visited.Add(n);
                stillFor = Vector3.Distance(b.Position, last) < 1e-4f ? stillFor + Dt : 0f;
                Assert.That(stillFor, Is.LessThan(b.def.patrolPause + 0.5f), "never stuck for long");
                last = b.Position;
            }
            Assert.That(visited.Count, Is.EqualTo(new HashSet<int>(y.patrol).Count), "every patrol point reached");
            Assert.That(b.State, Is.EqualTo(AlertState.Unaware));
        }

        // ---------- The counter ----------

        [Test]
        public void TheCounterTellsHowManyAreLeftAndAnnouncesTheLastOnce()
        {
            var set = new CollectibleSet();
            int all = 0, lastRemaining = -1;
            set.AllCollected += () => all++;
            set.Collected += (i, left) => lastRemaining = left;
            Assert.That(set.AnyLeft, Is.False, "nothing registered");
            Assert.That(set.AllFound, Is.False);
            int a = set.Register("a"), b = set.Register("b"), c = set.Register("c");
            Assert.That(set.Register("b"), Is.EqualTo(b), "a repeated id is the same one");
            Assert.That(set.Total, Is.EqualTo(3));
            Assert.That(set.Collect(a), Is.True);
            Assert.That(set.Collect(a), Is.False, "once only");
            Assert.That(set.Remaining, Is.EqualTo(2));
            Assert.That(lastRemaining, Is.EqualTo(2));
            Assert.That(set.AnyLeft, Is.True);
            Assert.That(set.Collect("c"), Is.True);
            Assert.That(all, Is.EqualTo(0));
            Assert.That(set.Collect(b), Is.True);
            Assert.That(set.AnyLeft, Is.False);
            Assert.That(set.AllFound, Is.True);
            Assert.That(all, Is.EqualTo(1), "the last find fires once");
            Assert.That(set.Summary(), Is.EqualTo("All 3 found"));
            Assert.That(set.Collect(99), Is.False);
            set.ResetFound();
            Assert.That(set.Remaining, Is.EqualTo(3));
            Assert.That(set.Summary(), Is.EqualTo("0 / 3 found"));
            set.Collect(a); set.Collect(b); set.Collect(c);
            Assert.That(all, Is.EqualTo(2), "after a reset it can fire again");
        }
    }
}

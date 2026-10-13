using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>The drone's hover patrol (fix round "the drones move", 2026-10-14): it stays on its leash, it visibly moves
    /// every second while awake, it never flies into cover, and the same seed flies the same way.</summary>
    public class HoverPatrolTests
    {
        const float H = FightSim.Step;
        static readonly Vector3 Post = new Vector3(-19f, GalleriaArena.DroneY, -4f);
        static readonly Vector3 Player = new Vector3(0f, 1.25f, -17f);

        /// <summary>Flies a patrol for <paramref name="seconds"/> with a brain-like cycle (telegraph 0.35 s of every 0.8 s,
        /// a volley at each telegraph's end, a hit every <paramref name="hitEvery"/> s), returning the offset each step.</summary>
        static List<Vector3> Fly(ulong seed, float seconds, bool awake = true, float hitEvery = 5f, Func<Vector3, Vector3, float, bool> blocked = null,
            HoverPatrol p = null)
        {
            var d = SkylineEnemies.MallCopDrone();
            p = p ?? new HoverPatrol(d, seed);
            var path = new List<Vector3>();
            int steps = (int)(seconds / H);
            float cycle = d.fireInterval, tele = d.telegraphSeconds;
            for (int i = 0; i < steps; i++)
            {
                float t = i * H;
                float inCycle = t % cycle;
                bool telegraphing = awake && inCycle < tele;
                if (awake && inCycle >= tele && inCycle - H < tele) p.OnVolley();
                if (awake && hitEvery > 0f && t > 0f && (t % hitEvery) < H) p.OnHit();
                p.Tick(H, Post, awake, telegraphing, Player - (Post + p.Offset), blocked);
                path.Add(p.Offset);
            }
            return path;
        }

        static float Flat(Vector3 v) => MathF.Sqrt(v.X * v.X + v.Z * v.Z);

        [Test]
        public void TheDroneHasAPatrolInItsDataAndTheOtherBotsDoNot()
        {
            var d = SkylineEnemies.MallCopDrone();
            Assert.That(HoverPatrol.Moves(d), Is.True);
            Assert.That(d.patrolSpeed, Is.InRange(2f, 3f), "the ruling: strafes of about 2-3 m/s");
            Assert.That(d.patrolLeash, Is.InRange(2.5f, 3f), "within about 3 m of its post");
            Assert.That(HoverPatrol.Moves(SkylineEnemies.Spotlight()), Is.False);
            Assert.That(HoverPatrol.Moves(SkylineEnemies.FloorCareUnit()), Is.False);
            Assert.That(HoverPatrol.Moves(SkylineEnemies.Gondola()), Is.False);
        }

        [TestCase(1UL)]
        [TestCase(7UL)]
        [TestCase(12345UL)]
        public void ItNeverStraysPastItsLeashAwakeOrAsleep(ulong seed)
        {
            var d = SkylineEnemies.MallCopDrone();
            foreach (var o in Fly(seed, 60f))
            {
                Assert.That(Flat(o), Is.LessThanOrEqualTo(d.patrolLeash + 1e-3f), "awake: on the leash");
                Assert.That(Math.Abs(o.Y), Is.LessThanOrEqualTo(d.patrolBob + 1e-4f), "the bob is gentle");
            }
            foreach (var o in Fly(seed, 30f, false))
                Assert.That(Flat(o), Is.LessThanOrEqualTo(d.patrolLeash * HoverPatrol.AsleepScale + 1e-3f), "asleep: a short drift");
        }

        [TestCase(1UL)]
        [TestCase(7UL)]
        [TestCase(12345UL)]
        public void AwakeItMovesVisiblyInEverySecond(ulong seed)
        {
            var path = Fly(seed, 60f);
            int window = (int)MathF.Round(1f / H);
            float least = float.PositiveInfinity, top = 0f;
            for (int i = 0; i + window < path.Count; i++)
            {
                float travelled = 0f;
                for (int k = i; k < i + window; k++) travelled += Flat(path[k + 1] - path[k]);
                least = Math.Min(least, travelled);
            }
            for (int k = 1; k < path.Count; k++) top = Math.Max(top, Flat(path[k] - path[k - 1]) / H);
            TestContext.WriteLine($"seed {seed}: the least strafe in any 1 s window {least:0.00} m; top speed {top:0.00} m/s");
            Assert.That(least, Is.GreaterThanOrEqualTo(0.6f), "it strafes at least 0.6 m in every second while awake (not counting the bob)");
            Assert.That(top, Is.LessThanOrEqualTo(SkylineEnemies.MallCopDrone().patrolDashSpeed + 0.01f), "never faster than its dash");
        }

        [Test]
        public void ItStrafesAcrossTheLineToItsTarget()
        {
            // most of its travel is sideways to the line to the player, not toward or away from it
            var path = Fly(3UL, 60f, true, 0f);
            Vector3 to = Player - Post;
            var fwd = Vector3.Normalize(new Vector3(to.X, 0f, to.Z));
            var lat = new Vector3(fwd.Z, 0f, -fwd.X);
            float across = 0f, along = 0f;
            for (int k = 1; k < path.Count; k++)
            {
                Vector3 s = path[k] - path[k - 1];
                across += Math.Abs(Vector3.Dot(s, lat));
                along += Math.Abs(Vector3.Dot(s, fwd));
            }
            TestContext.WriteLine($"travel across the line {across:0.0} m, along it {along:0.0} m");
            Assert.That(across, Is.GreaterThan(1.5f * along));
        }

        [Test]
        public void TheSameSeedFliesTheSameWayAndAnotherSeedDoesNot()
        {
            var a = Fly(42UL, 20f);
            var b = Fly(42UL, 20f);
            var c = Fly(43UL, 20f);
            Assert.That(a.Count, Is.EqualTo(b.Count));
            bool differs = false;
            for (int i = 0; i < a.Count; i++)
            {
                Assert.That(a[i], Is.EqualTo(b[i]), "step " + i);
                if (Vector3.Distance(a[i], c[i]) > 0.1f) differs = true;
            }
            Assert.That(differs, Is.True, "a different seed flies a different patrol");
        }

        [Test]
        public void ItNeverFliesIntoCoverBesideItsPostAndStillMoves()
        {
            // a stall back 1.2 m east of the post's sphere, from the floor to above the drone
            var box = new CoverBox { min = new Vector3(Post.X + 1.65f, 0f, Post.Z - 3f), max = new Vector3(Post.X + 2.25f, 4.8f, Post.Z + 3f) };
            var cover = new[] { box };
            float r = SkylineEnemies.MallCopDrone().bodyRadius;
            Func<Vector3, Vector3, float, bool> blocked = (a, b, rr) => CoverGeometry.BlockedThick(a, b, cover, rr);
            var path = Fly(5UL, 60f, true, 3f, blocked);
            float travelled = 0f;
            for (int k = 0; k < path.Count; k++)
            {
                Assert.That(CoverGeometry.BlockedThick(Post + path[k], Post + path[k], cover, r * 0.99f), Is.False, $"step {k}: inside the stall back at {Post + path[k]}");
                if (k > 0) travelled += Flat(path[k] - path[k - 1]);
            }
            TestContext.WriteLine($"beside cover: {travelled:0.0} m flown in 60 s");
            Assert.That(travelled, Is.GreaterThan(60f), "it still patrols (over 1 m a second on average) on its open side");
        }

        [Test]
        public void AHitSendsItOnADashToANewSpot()
        {
            var d = SkylineEnemies.MallCopDrone();
            var p = new HoverPatrol(d, 9UL);
            Fly(9UL, 2f, true, 0f, null, p);
            Vector3 before = p.Offset;
            p.OnHit();
            p.Tick(H, Post, true, false, Player - Post, null);
            Assert.That(p.Dashing, Is.True);
            Assert.That(p.HasLeg, Is.True);
            Assert.That(Flat(p.Leg - new Vector3(before.X, 0f, before.Z)), Is.GreaterThanOrEqualTo(0.7f), "a real reposition, not a twitch");
            float top = 0f;
            for (int i = 0; i < 30; i++)
            {
                Vector3 a = p.Flat;
                p.Tick(H, Post, true, false, Player - Post, null);
                top = Math.Max(top, Flat(p.Flat - a) / H);
            }
            Assert.That(top, Is.GreaterThan(d.patrolSpeed), "the dash is faster than its strafe");
        }

        [Test]
        public void InTheFoodCourtEveryAwakeDroneMovesEverySecondAndKeepsItsLeash()
        {
            // a player standing in the open: the drones wake, fire and strafe round their posts (the posts don't move while they see)
            var f = FightTests.Make("food-court", new StandAndShootBot(), 3, new Vector3(0f, 0f, -4f));
            var leash = SkylineEnemies.MallCopDrone().patrolLeash;
            var last = new Dictionary<int, Vector3>();
            var since = new Dictionary<int, float>();
            int checkedDrones = 0;
            for (int step = 0; step < (int)(6f / H) && !f.Over; step++)
            {
                f.Tick();
                foreach (var e in f.Enemies)
                {
                    if (e.Patrol == null) continue;
                    Assert.That(Flat(e.Center - e.Position), Is.LessThanOrEqualTo(leash + 1e-3f), $"drone {e.Index} at {f.Time:0.00} s: on its leash");
                    if (e.Wrecked || !e.Awake) { last.Remove(e.Index); continue; }
                    if (!last.ContainsKey(e.Index)) { last[e.Index] = e.Center; since[e.Index] = f.Time; continue; }
                    if (f.Time - since[e.Index] >= 1f)
                    {
                        Assert.That(Flat(e.Center - last[e.Index]) + Math.Abs(e.Center.Y - last[e.Index].Y), Is.GreaterThan(0.05f), $"drone {e.Index} at {f.Time:0.00} s: it moved in the last second");
                        last[e.Index] = e.Center;
                        since[e.Index] = f.Time;
                        checkedDrones++;
                    }
                }
            }
            Assert.That(checkedDrones, Is.GreaterThan(12), "awake drones were checked");
        }
    }
}

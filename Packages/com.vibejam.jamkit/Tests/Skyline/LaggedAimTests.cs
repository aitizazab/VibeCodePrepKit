using System;
using System.Text;
using System.Numerics;
using JamKit.Movement;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>The drone's lagged aim (the creator, 2026-10-14): "make it whiff less when you're standing still, and make it miss
    /// more when you're moving fast. Like maybe it shoots at where you were 10 or 20 frames ago." The drone fires at where the
    /// player's chest was <c>aimLagSeconds</c> ago, with a small, tight spread and no leading; the bolt's flight time adds to the
    /// miss. These tests measure the hit rate of one bolt at the drone's real ranges and bolt speed against a target that stands
    /// or runs a straight line, and pin the shape; the table is in docs/SKYLINE-BOTS.md.</summary>
    public class LaggedAimTests
    {
        // ------------------------------------------------------------------ the trail

        [Test]
        public void TheTrailGivesWhereTheTargetWasInterpolated()
        {
            var t = new PositionTrail();
            for (int i = 0; i <= 60; i++) t.Record(i / 60f, new Vector3(i * 0.1f, 0f, 0f));   // 6 m/s along x for a second
            var p = t.At(0.5f, Vector3.Zero);
            Assert.That(p.X, Is.EqualTo(3f).Within(0.01f));
            var q = t.At(0.5f + 1f / 120f, Vector3.Zero);                                      // between two samples
            Assert.That(q.X, Is.EqualTo(3.05f).Within(0.01f));
        }

        [Test]
        public void TheTrailClampsToItsEndsAndFallsBackWhenEmpty()
        {
            var t = new PositionTrail();
            var none = new Vector3(9f, 9f, 9f);
            Assert.That(t.At(1f, none), Is.EqualTo(none));
            t.Record(1f, new Vector3(1f, 0f, 0f));
            t.Record(1.1f, new Vector3(2f, 0f, 0f));
            Assert.That(t.At(0.2f, none).X, Is.EqualTo(1f).Within(1e-4f), "older than the history: the oldest sample");
            Assert.That(t.At(5f, none).X, Is.EqualTo(2f).Within(1e-4f), "newer than the history: the newest");
        }

        [Test]
        public void ATeleportOrARestartedClockForgetsTheHistory()
        {
            var t = new PositionTrail();
            for (int i = 0; i < 30; i++) t.Record(i / 60f, new Vector3(i * 0.1f, 0f, 0f));
            t.Record(0.6f, new Vector3(100f, 0f, 0f));               // 97 m in one tick: a respawn
            Assert.That(t.Count, Is.EqualTo(1));
            Assert.That(t.At(0.1f, Vector3.Zero).X, Is.EqualTo(100f).Within(1e-3f), "it never aims at the place left behind");
            t.Record(0.01f, new Vector3(100f, 0f, 0f));              // the clock ran backwards
            Assert.That(t.Count, Is.EqualTo(1));
        }

        [Test]
        public void TheTrailSurvivesMoreSamplesThanItHolds()
        {
            var t = new PositionTrail();
            for (int i = 0; i < 1000; i++) t.Record(i / 240f, new Vector3(i * 0.01f, 0f, 0f));
            float now = 999 / 240f;
            Assert.That(t.At(now - 0.15f, Vector3.Zero).X, Is.EqualTo((999 - 36) * 0.01f).Within(0.02f));
        }

        [Test]
        public void AnInfiniteOrNaNPositionIsIgnored()
        {
            var t = new PositionTrail();
            t.Record(0f, new Vector3(float.NaN, 0f, 0f));
            t.Record(0.1f, new Vector3(float.PositiveInfinity, 0f, 0f));
            Assert.That(t.Count, Is.EqualTo(0));
        }

        [Test]
        public void ALagOfZeroOrNoHistoryAimsAtTheTargetNow()
        {
            var now = new Vector3(1f, 2f, 3f);
            Assert.That(Aim.LaggedPoint(null, 1f, 0.15f, now), Is.EqualTo(now));
            var t = new PositionTrail();
            t.Record(0f, Vector3.Zero);
            t.Record(0.1f, new Vector3(1f, 0f, 0f));
            Assert.That(Aim.LaggedPoint(t, 0.1f, 0f, now), Is.EqualTo(now));
        }

        // ------------------------------------------------------------------ the measurement

        /// <summary>The drone hovers this far above the player's chest (m); its real hover height is about 3 m over the floor.</summary>
        const float DroneAbove = 3f;
        const float PlayerRadius = 0.38f, ChestToCapsuleEnd = 0.47f;   // MoveDef: radius 0.38, stand height 1.7 (chest at 0.85)

        /// <summary>The share of bolts that hit a target running a straight line at <paramref name="speed"/> m/s, at slant range
        /// <paramref name="range"/>. <paramref name="angleDeg"/> is the run's angle to the line from the drone: 0 = straight away,
        /// 90 = across, 180 = straight at the drone. <paramref name="lag"/> aims at the chest that long ago; with
        /// <paramref name="leadFactor"/> above 0 and no lag it is the old leading aim. Each trial fires one bolt (its own seeded
        /// spread cone), flies it at <paramref name="boltSpeed"/> and counts a touch of the player's capsule.</summary>
        public static float HitRate(float lag, float leadFactor, float spreadDeg, float range, float speed, float angleDeg,
            int trials = 400, float boltSpeed = 30f, float boltRadius = 0.15f, ulong seed = 11)
        {
            var rng = new Rng(seed);
            float horizontal = MathF.Sqrt(MathF.Max(0.01f, range * range - DroneAbove * DroneAbove));
            // the player's chest at the moment of the shot is the origin; the drone is horizontal metres away along -x and above
            var drone = new Vector3(-horizontal, DroneAbove, 0f);
            float a = angleDeg * MathF.PI / 180f;
            var vel = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a)) * speed;   // angle 0 runs along +x: away from the drone
            int hits = 0;
            for (int n = 0; n < trials; n++)
            {
                Vector3 aimAt;
                if (lag > 0f)
                {
                    var trail = new PositionTrail();
                    for (int i = -60; i <= 0; i++) trail.Record(i / 60f, vel * (i / 60f));   // the last second of the straight run
                    aimAt = Aim.LaggedPoint(trail, 0f, lag, Vector3.Zero);
                }
                else aimAt = Aim.LeadPoint(drone, Vector3.Zero, vel, boltSpeed, leadFactor);
                Vector3 dir = Vector3.Normalize(aimAt - drone);
                dir = Spread(dir, spreadDeg, rng);
                if (Flies(drone, dir * boltSpeed, vel, boltRadius, range / boltSpeed + 1.5f)) hits++;
            }
            return hits / (float)trials;
        }

        static bool Flies(Vector3 from, Vector3 vel, Vector3 targetVel, float boltRadius, float seconds)
        {
            const float step = 1f / 480f;
            var p = from;
            for (float t = 0f; t < seconds; t += step)
            {
                p += vel * step;
                var chest = targetVel * t;
                // distance to the capsule's axis, a vertical segment around the chest
                float y = MathF.Max(chest.Y - ChestToCapsuleEnd, MathF.Min(chest.Y + ChestToCapsuleEnd, p.Y));
                if (Vector3.Distance(p, new Vector3(chest.X, y, chest.Z)) <= PlayerRadius + boltRadius) return true;
            }
            return false;
        }

        static Vector3 Spread(Vector3 dir, float deg, Rng rng)
        {
            if (!(deg > 0f)) return dir;
            Vector3 side = Vector3.Normalize(Vector3.Cross(dir, MathF.Abs(dir.Y) < 0.99f ? Vector3.UnitY : Vector3.UnitX));
            Vector3 up = Vector3.Cross(side, dir);
            float angle = rng.Range(0f, 2f * MathF.PI);
            float rr = MathF.Tan(deg * MathF.PI / 180f) * MathF.Sqrt(rng.NextFloat());
            return Vector3.Normalize(dir + (side * MathF.Cos(angle) + up * MathF.Sin(angle)) * rr);
        }

        static readonly float[] Ranges = { 8f, 12f, 16f, 20f, 25f, 30f };

        /// <summary>The mean hit rate over the drone's usual fighting ranges (8 to 30 m).</summary>
        static float Typical(float lag, float lead, float spread, float speed, float angle)
        {
            float sum = 0f;
            foreach (float r in Ranges) sum += HitRate(lag, lead, spread, r, speed, angle);
            return sum / Ranges.Length;
        }

        static EnemyDef Drone => SkylineEnemies.MallCopDrone();

        [Test]
        public void TheDronesLagIsInTheCreatorsRangeWithATightSpreadAndNoLead()
        {
            var d = Drone;
            Assert.That(d.aimLagSeconds, Is.InRange(0.1f, 0.2f), "the creator: 0.1 to 0.2 s ago, 10 or 20 frames");
            Assert.That(d.aimSpreadDeg, Is.InRange(0.01f, 1f), "small and tight, but a drone isn't a perfect shot");
            Assert.That(d.projectileSpeed, Is.EqualTo(30f), "the bolt stays slow enough to dodge when you look at it");
        }

        [Test]
        public void AStandingTargetIsHitAlmostEveryTimeAtEveryRange()
        {
            var d = Drone;
            foreach (float r in Ranges)
                Assert.That(HitRate(d.aimLagSeconds, 0f, d.aimSpreadDeg, r, 0f, 90f), Is.GreaterThanOrEqualTo(0.9f), $"standing at {r} m");
        }

        [Test]
        public void TheOldAimWhiffedAtAStandingTargetMostOfTheTime()
        {
            // what the creator saw: a lead of 0.75 and 4 degrees of spread, over the same ranges (the fix is the test above)
            Assert.That(Typical(0f, 0.75f, 4f, 0f, 90f), Is.LessThan(0.6f));
        }

        [Test]
        public void AFastTargetIsMissedMostOfTheTimeWhateverWayItRuns()
        {
            var d = Drone;
            foreach (float speed in new[] { 9.5f, 13f })
                foreach (float angle in new[] { 90f, 45f, 0f, 135f })
                    Assert.That(Typical(d.aimLagSeconds, 0f, d.aimSpreadDeg, speed, angle), Is.LessThanOrEqualTo(0.25f), $"{speed} m/s at {angle} degrees");
            // straight at the drone, a sprint is still the likeliest hit of the fast moves, but under a lag of 0.2 s well under 3 in 5
            Assert.That(Typical(d.aimLagSeconds, 0f, d.aimSpreadDeg, 9.5f, 180f), Is.LessThan(0.65f));
            Assert.That(Typical(d.aimLagSeconds, 0f, d.aimSpreadDeg, 13f, 180f), Is.LessThan(0.25f));
        }

        [Test]
        public void TheFasterYouRunTheLessYouAreHit()
        {
            var d = Drone;
            float[] speeds = { 0f, 4f, 8f, 9.5f, 13f };
            foreach (float angle in new[] { 90f, 0f, 180f })
            {
                float last = 2f;
                foreach (float speed in speeds)
                {
                    float h = Typical(d.aimLagSeconds, 0f, d.aimSpreadDeg, speed, speed < 0.01f ? 90f : angle);
                    Assert.That(h, Is.LessThanOrEqualTo(last + 0.03f), $"{speed} m/s at {angle} degrees ({h:P0} after {last:P0})");
                    last = h;
                }
            }
            // and over every way of running, walking sits between standing and sprinting
            float stand = AnyDirection(d.aimLagSeconds, 0f, d.aimSpreadDeg, 0f), walk = AnyDirection(d.aimLagSeconds, 0f, d.aimSpreadDeg, 4f);
            float sprint = AnyDirection(d.aimLagSeconds, 0f, d.aimSpreadDeg, 9.5f), fast = AnyDirection(d.aimLagSeconds, 0f, d.aimSpreadDeg, 13f);
            Assert.That(stand, Is.GreaterThan(walk));
            Assert.That(walk, Is.GreaterThan(sprint));
            Assert.That(sprint, Is.GreaterThan(fast));
        }

        static readonly float[] Speeds = { 0f, 4f, 5f, 8f, 9.5f, 13f };

        /// <summary>The mean over every way of running (0 to 180 degrees to the line from the drone in 15-degree steps; the other
        /// half is the mirror image) and over the usual ranges.</summary>
        static float AnyDirection(float lag, float lead, float spread, float speed)
        {
            if (speed < 0.01f) return Typical(lag, lead, spread, 0f, 90f);
            float sum = 0f;
            int n = 0;
            for (float angle = 0f; angle <= 180f; angle += 15f, n++) sum += Typical(lag, lead, spread, speed, angle);
            return sum / n;
        }

        [Test, Explicit("prints the hit-rate table of docs/SKYLINE-BOTS.md for the old aim and a sweep of lags and spreads; asserts nothing")]
        public void PrintTheTable()
        {
            var sb = new StringBuilder();
            sb.AppendLine("hit share over 8..30 m: speed -> across | straight at | straight away | any direction");
            Table(sb, "OLD lead 0.75, spread 4 deg", 0f, 0.75f, 4f);
            foreach (float spread in new[] { 0.5f })
                foreach (float lag in new[] { 0.1f, 0.15f, 0.2f })
                    Table(sb, $"NEW lag {lag:0.00} s, spread {spread:0.0} deg", lag, 0f, spread);
            TestContext.WriteLine(sb.ToString());
        }

        static void Table(StringBuilder sb, string title, float lag, float lead, float spread)
        {
            sb.AppendLine(title);
            foreach (float speed in Speeds)
                sb.AppendLine($"  {speed,4:0.0} m/s -> {Typical(lag, lead, spread, speed, 90f),4:P0} | {Typical(lag, lead, spread, speed, 180f),4:P0} | " +
                    $"{Typical(lag, lead, spread, speed, 0f),4:P0} | {AnyDirection(lag, lead, spread, speed),4:P0}");
        }
    }
}

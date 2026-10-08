using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Patterns;
using NUnit.Framework;

namespace JamKit.Tests
{
    public class EmitterTests
    {
        const double Deg = Math.PI / 180.0;

        /// <summary>Fire the volleys due at t = 0 (age 0, so nothing is pre-advanced).</summary>
        static List<BulletSpawn> FireNow(PatternDef def, ulong seed = 1, Vector2 origin = default, Vector2? target = null)
        {
            var output = new List<BulletSpawn>();
            new Emitter(def, new Rng(seed)).Tick(0f, origin, target ?? origin + Vector2.UnitX, output);
            return output;
        }

        static double Wrap(double radians) => Math.IEEERemainder(radians, 2 * Math.PI);

        static void AssertAngle(double actual, double expected, double tolerance = 1e-5)
        {
            Assert.That(Math.Abs(Wrap(actual - expected)), Is.LessThan(tolerance), $"angle {actual} vs {expected}");
        }

        [Test]
        public void RingHasCountBulletsEvenlySpaced()
        {
            var shots = FireNow(new PatternDef { shape = PatternShape.Ring, count = 8, baseAngleDegrees = 10, spawnRadius = 2 },
                origin: new Vector2(5, 5));
            Assert.That(shots.Count, Is.EqualTo(8));
            for (int i = 0; i < 8; i++)
            {
                AssertAngle(shots[i].angle, (10 + 45 * i) * Deg);
                Assert.That(Vector2.Distance(shots[i].position, new Vector2(5, 5)), Is.EqualTo(2f).Within(1e-5));
                Assert.That(shots[i].age, Is.EqualTo(0f));
            }
        }

        [Test]
        public void SpreadIsCentredOnTheBaseAngle()
        {
            var shots = FireNow(new PatternDef { shape = PatternShape.Spread, count = 5, spreadDegrees = 60, baseAngleDegrees = 90 });
            double[] expected = { 60, 75, 90, 105, 120 };
            for (int i = 0; i < 5; i++) AssertAngle(shots[i].angle, expected[i] * Deg);
        }

        [Test]
        public void SpreadIsCentredOnTheAimWhenAiming()
        {
            var origin = new Vector2(1, 1);
            var target = new Vector2(1, -9); // straight down
            var shots = FireNow(new PatternDef { shape = PatternShape.Spread, count = 3, spreadDegrees = 40, aimAtTarget = true },
                origin: origin, target: target);
            AssertAngle(shots[0].angle, -110 * Deg);
            AssertAngle(shots[1].angle, -90 * Deg);
            AssertAngle(shots[2].angle, -70 * Deg);
        }

        [Test]
        public void AimedPointsAtTheTarget()
        {
            var origin = new Vector2(1, 1);
            var target = new Vector2(4, 5);
            var shots = FireNow(new PatternDef { shape = PatternShape.Aimed, count = 1 }, origin: origin, target: target);
            Assert.That(shots.Count, Is.EqualTo(1));
            Assert.That(shots[0].Direction.X, Is.EqualTo(0.6f).Within(1e-5));
            Assert.That(shots[0].Direction.Y, Is.EqualTo(0.8f).Within(1e-5));
        }

        [Test]
        public void AimingAtYourselfDoesNotProduceNaN()
        {
            var shots = FireNow(new PatternDef { shape = PatternShape.Aimed, count = 1 }, target: Vector2.Zero);
            Assert.That(float.IsNaN(shots[0].angle), Is.False);
        }

        [Test]
        public void SpiralTurnsEachVolley()
        {
            var def = new PatternDef { shape = PatternShape.Spiral, count = 3, rotatePerShotDegrees = 10, shots = 4, shotInterval = 0.1f };
            var output = new List<BulletSpawn>();
            new Emitter(def, new Rng(1)).Tick(0.35f, Vector2.Zero, Vector2.UnitX, output);
            Assert.That(output.Count, Is.EqualTo(12));
            for (int v = 0; v < 4; v++)
                for (int arm = 0; arm < 3; arm++)
                    AssertAngle(output[v * 3 + arm].angle, (10 * v + 120 * arm) * Deg);
        }

        [Test]
        public void SpeedStepStacksBulletsOnOneLine()
        {
            var shots = FireNow(PatternLibrary.AimedStack(count: 4, speed: 2f));
            Assert.That(shots.Count, Is.EqualTo(4));
            for (int i = 0; i < 4; i++)
            {
                Assert.That(shots[i].speed, Is.EqualTo(2f + 0.8f * i).Within(1e-5));
                AssertAngle(shots[i].angle, shots[0].angle);
            }
        }

        [Test]
        public void MirrorDoublesTheVolleySymmetrically()
        {
            var def = new PatternDef
            {
                shape = PatternShape.Spread, count = 3, spreadDegrees = 40, baseAngleDegrees = 30,
                rotatePerShotDegrees = 20, shots = 2, shotInterval = 0, spawnRadius = 1.5f,
                angularVelocityDegrees = 45, mirror = true,
            };
            var shots = FireNow(def);
            Assert.That(shots.Count, Is.EqualTo(12));
            double axis = 30 * Deg;
            var axisDir = new Vector2((float)Math.Cos(axis), (float)Math.Sin(axis));
            for (int i = 0; i < shots.Count; i += 2)
            {
                BulletSpawn a = shots[i], m = shots[i + 1];
                AssertAngle(m.angle, 2 * axis - a.angle);
                Assert.That(m.angularVelocity, Is.EqualTo(-a.angularVelocity));
                Assert.That(m.speed, Is.EqualTo(a.speed));
                Vector2 reflected = 2f * Vector2.Dot(a.position, axisDir) * axisDir - a.position;
                Assert.That(Vector2.Distance(m.position, reflected), Is.LessThan(1e-5f));
            }
            // Second volley's centre turned +20 degrees; its mirror turned -20: a counter-rotating twin.
            AssertAngle(shots[8].angle, (30 + 20) * Deg);
            AssertAngle(shots[9].angle, (30 - 20) * Deg);
        }

        [Test]
        public void LineIsAWallFlyingOneWay()
        {
            var shots = FireNow(new PatternDef { shape = PatternShape.Line, count = 5, lineWidth = 8, baseAngleDegrees = -90 });
            for (int i = 0; i < 5; i++)
            {
                AssertAngle(shots[i].angle, -90 * Deg);
                Assert.That(shots[i].position.X, Is.EqualTo(-4f + 2f * i).Within(1e-5));
                Assert.That(shots[i].position.Y, Is.EqualTo(0f).Within(1e-5));
            }
        }

        [Test]
        public void JitterIsDeterministicPerSeedAndBounded()
        {
            var def = new PatternDef { shape = PatternShape.Ring, count = 16, jitterDegrees = 10 };
            var a = FireNow(def, seed: 7);
            var b = FireNow(def, seed: 7);
            var c = FireNow(def, seed: 8);
            int differ = 0;
            for (int i = 0; i < 16; i++)
            {
                Assert.That(b[i].angle, Is.EqualTo(a[i].angle));
                if (c[i].angle != a[i].angle) differ++;
                Assert.That(Math.Abs(Wrap(a[i].angle - 22.5 * i * Deg)), Is.LessThanOrEqualTo(10 * Deg + 1e-6));
            }
            Assert.That(differ, Is.GreaterThan(12));
        }

        [Test]
        public void ShotgunStaysInsideItsCone()
        {
            var shots = FireNow(new PatternDef { shape = PatternShape.Shotgun, count = 50, spreadDegrees = 30, baseAngleDegrees = 45 });
            double min = double.MaxValue, max = double.MinValue;
            foreach (var s in shots)
            {
                double off = Wrap(s.angle - 45 * Deg) / Deg;
                min = Math.Min(min, off);
                max = Math.Max(max, off);
            }
            Assert.That(min, Is.GreaterThanOrEqualTo(-15).And.LessThan(-10));
            Assert.That(max, Is.LessThan(15).And.GreaterThan(10));
        }

        [Test]
        public void VolleysFollowStartDelayAndInterval()
        {
            var def = new PatternDef { shots = 4, shotInterval = 0.25f, startDelay = 0.5f, count = 1 };
            var e = new Emitter(def, new Rng(1));
            var output = new List<BulletSpawn>();
            var times = new List<double>();
            double now = 0;
            for (int i = 0; i < 200; i++)
            {
                int before = output.Count;
                e.Tick(0.01f, Vector2.Zero, Vector2.UnitX, output);
                now += 0.01f;
                for (int j = before; j < output.Count; j++) times.Add(now - output[j].age);
            }
            Assert.That(times, Is.EqualTo(new[] { 0.5, 0.75, 1.0, 1.25 }).Within(1e-4));
            Assert.That(e.Finished, Is.True);
        }

        [Test]
        public void FinishesAndResetReplaysTheSameBullets()
        {
            var def = new PatternDef { shape = PatternShape.Shotgun, shots = 3, shotInterval = 0.1f, jitterDegrees = 5 };
            var e = new Emitter(def, new Rng(3));
            var first = new List<BulletSpawn>();
            e.Tick(0.15f, Vector2.Zero, Vector2.UnitY, first);
            Assert.That(e.Finished, Is.False);
            e.Tick(0.1f, Vector2.Zero, Vector2.UnitY, first);
            Assert.That(e.Finished, Is.True);

            e.Reset();
            Assert.That(e.Finished, Is.False);
            Assert.That(e.Time, Is.EqualTo(0f));
            var second = new List<BulletSpawn>();
            e.Tick(0.15f, Vector2.Zero, Vector2.UnitY, second);
            e.Tick(0.1f, Vector2.Zero, Vector2.UnitY, second);
            Assert.That(second.Count, Is.EqualTo(first.Count));
            for (int i = 0; i < first.Count; i++) Assert.That(second[i].angle, Is.EqualTo(first[i].angle));
        }

        [Test]
        public void EndlessPatternNeverFinishes()
        {
            var e = new Emitter(new PatternDef { shots = 0, shotInterval = 0.1f, count = 1 }, new Rng(1));
            var output = new List<BulletSpawn>();
            for (int i = 0; i < 105; i++) e.Tick(0.01f, Vector2.Zero, Vector2.UnitX, output);
            Assert.That(output.Count, Is.EqualTo(11)); // t = 0, 0.1 ... 1.0
            Assert.That(e.Finished, Is.False);
        }

        [Test]
        public void SequenceStepsFireOnTimeAndLoop()
        {
            var seq = new PatternSequence { loop = true, loopDelay = 0.25f }
                .Add(0f, new PatternDef { count = 1, styleIndex = 0 })
                .Add(0.5f, new PatternDef { count = 1, styleIndex = 1 });
            Assert.That(seq.Duration, Is.EqualTo(0.5f));

            var e = new Emitter(seq, new Rng(1));
            var output = new List<BulletSpawn>();
            var times = new List<double>();
            double now = 0;
            for (int i = 0; i < 190; i++)
            {
                int before = output.Count;
                e.Tick(0.01f, Vector2.Zero, Vector2.UnitX, output);
                now += 0.01f;
                for (int j = before; j < output.Count; j++) times.Add(now - output[j].age);
            }
            // Passes start every 0.5 + 0.25 s.
            Assert.That(times, Is.EqualTo(new[] { 0.0, 0.5, 0.75, 1.25, 1.5 }).Within(1e-4));
            Assert.That(output.ConvertAll(s => s.styleIndex), Is.EqualTo(new[] { 0, 1, 0, 1, 0 }));
            Assert.That(e.Finished, Is.False);
        }

        [Test]
        public void SequenceWithoutLoopFinishes()
        {
            var seq = new PatternSequence()
                .Add(0f, new PatternDef { count = 2 })
                .Add(1f, new PatternDef { count = 3 });
            var e = new Emitter(seq, new Rng(1));
            var output = new List<BulletSpawn>();
            e.Tick(0.9f, Vector2.Zero, Vector2.UnitX, output);
            Assert.That(output.Count, Is.EqualTo(2));
            Assert.That(e.Finished, Is.False);
            e.Tick(0.2f, Vector2.Zero, Vector2.UnitX, output);
            Assert.That(output.Count, Is.EqualTo(5));
            Assert.That(e.Finished, Is.True);
        }

        [Test]
        public void DensityScalesCountRoundingSensiblyNeverBelowOne()
        {
            Assert.That(Emitter.ScaledCount(10, 0.25f), Is.EqualTo(3)); // 2.5 rounds up
            Assert.That(Emitter.ScaledCount(10, 1.5f), Is.EqualTo(15));
            Assert.That(Emitter.ScaledCount(10, 0.04f), Is.EqualTo(1));
            Assert.That(Emitter.ScaledCount(10, 0f), Is.EqualTo(1));
            Assert.That(Emitter.ScaledCount(0, 1f), Is.EqualTo(1));

            var e = new Emitter(new PatternDef { shape = PatternShape.Ring, count = 12 }, new Rng(1)) { DensityScale = 0.5f };
            var output = new List<BulletSpawn>();
            e.Tick(0f, Vector2.Zero, Vector2.UnitX, output);
            Assert.That(output.Count, Is.EqualTo(6));
            for (int i = 0; i < 6; i++) AssertAngle(output[i].angle, 60 * i * Deg); // still an even ring
        }

        [Test]
        public void SpeedScaleKeepsThePathButChangesThePace()
        {
            var def = new PatternDef
            {
                shape = PatternShape.Aimed, count = 2, spreadDegrees = 0, speed = 4, speedStep = 1,
                acceleration = 2, angularVelocityDegrees = 30, lifetime = 8,
            };
            var normal = FireNow(def);
            var slow = new List<BulletSpawn>();
            new Emitter(def, new Rng(1)) { SpeedScale = 0.5f }.Tick(0f, Vector2.Zero, Vector2.UnitX, slow);

            Assert.That(slow[1].speed, Is.EqualTo(2.5f).Within(1e-5));
            Assert.That(slow[0].acceleration, Is.EqualTo(0.5f).Within(1e-5));
            Assert.That(slow[0].lifetime, Is.EqualTo(16f).Within(1e-5));

            // Half speed for twice as long lands in the same place.
            for (int i = 0; i < 2; i++)
            {
                BulletSpawn a = normal[i], b = slow[i];
                BulletMotion.Step(ref a.position, ref a.angle, ref a.speed, a.acceleration, a.angularVelocity, 1.5f);
                BulletMotion.Step(ref b.position, ref b.angle, ref b.speed, b.acceleration, b.angularVelocity, 3f);
                Assert.That(Vector2.Distance(a.position, b.position), Is.LessThan(1e-4f));
            }
        }

        // ---- Frame-rate independence -------------------------------------------------------------

        static PatternSequence BusyBossPhase()
        {
            return new PatternSequence { loop = true, loopDelay = 0.3f }
                .Add(0f, new PatternDef
                {
                    shape = PatternShape.Spiral, count = 5, rotatePerShotDegrees = 13, shots = 20, shotInterval = 0.07f,
                    startDelay = 0.013f, speed = 3, acceleration = 1.5f, angularVelocityDegrees = 35, mirror = true,
                    spawnRadius = 0.5f, jitterDegrees = 4, styleIndex = 0,
                })
                .Add(0.4f, new PatternDef
                {
                    shape = PatternShape.Shotgun, count = 7, spreadDegrees = 40, aimAtTarget = true, shots = 6,
                    shotInterval = 0.21f, speed = 6, speedStep = 0.3f, jitterDegrees = 3, styleIndex = 1,
                })
                .Add(1.1f, new PatternDef
                {
                    shape = PatternShape.Line, count = 9, lineWidth = 6, baseAngleDegrees = -90, speed = 3,
                    acceleration = -4, styleIndex = 2,
                });
        }

        struct Emitted
        {
            public BulletSpawn spawn;
            public double tickEnd;
        }

        /// <summary>Play the phase for `duration` in steps of dt, then move every bullet on to a common
        /// time with the pool's own Step, as the game would.</summary>
        static List<Emitted> Play(float dt, int ticks, double observeAt)
        {
            var e = new Emitter(BusyBossPhase(), new Rng(99));
            var output = new List<BulletSpawn>();
            var result = new List<Emitted>();
            var origin = new Vector2(0, 4);
            var target = new Vector2(2, -3);
            double now = 0;
            for (int i = 0; i < ticks; i++)
            {
                int before = output.Count;
                e.Tick(dt, origin, target, output);
                now += dt;
                for (int j = before; j < output.Count; j++) result.Add(new Emitted { spawn = output[j], tickEnd = now });
            }
            for (int i = 0; i < result.Count; i++)
            {
                var r = result[i];
                BulletMotion.Step(ref r.spawn.position, ref r.spawn.angle, ref r.spawn.speed,
                    r.spawn.acceleration, r.spawn.angularVelocity, (float)(observeAt - r.tickEnd));
                result[i] = r;
            }
            return result;
        }

        [Test]
        public void SameBulletsAt30And240FpsAndOneHugeTick()
        {
            const double observeAt = 3.5;
            var at30 = Play(1f / 30, 90, observeAt);
            var at240 = Play(1f / 240, 720, observeAt);
            var hitch = Play(3f, 1, observeAt);

            Assert.That(at30.Count, Is.GreaterThan(300)); // two passes of a busy phase, not a trivial case
            foreach (var other in new[] { at240, hitch })
            {
                Assert.That(other.Count, Is.EqualTo(at30.Count));
                for (int i = 0; i < at30.Count; i++)
                {
                    BulletSpawn a = at30[i].spawn, b = other[i].spawn;
                    Assert.That(other[i].tickEnd - b.age, Is.EqualTo(at30[i].tickEnd - a.age).Within(1e-4), "scheduled time");
                    Assert.That(b.styleIndex, Is.EqualTo(a.styleIndex));
                    Assert.That(Vector2.Distance(a.position, b.position), Is.LessThan(2e-3f), $"bullet {i} position");
                    Assert.That(b.angle, Is.EqualTo(a.angle).Within(1e-4), $"bullet {i} angle");
                    Assert.That(b.speed, Is.EqualTo(a.speed).Within(1e-4), $"bullet {i} speed");
                }
            }
        }

#if !UNITY_5_3_OR_NEWER
        [Test]
        public void TickDoesNotAllocate()
        {
            var e = new Emitter(BusyBossPhase(), new Rng(5));
            var output = new List<BulletSpawn>(4096);
            for (int i = 0; i < 30; i++) e.Tick(1f / 60, Vector2.Zero, Vector2.UnitX, output); // warm up
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 120; i++)
            {
                output.Clear();
                e.Tick(1f / 60, Vector2.Zero, Vector2.UnitX, output);
            }
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }
#endif

        [Test]
        public void EveryLibraryPresetFires()
        {
            var presets = new[]
            {
                PatternLibrary.RingBurst(), PatternLibrary.SpiralFlower(), PatternLibrary.AimedTriple(),
                PatternLibrary.AimedStack(), PatternLibrary.Wall(), PatternLibrary.Shotgun(), PatternLibrary.CurlRing(),
            };
            foreach (var def in presets)
            {
                var e = new Emitter(def, new Rng(1));
                var output = new List<BulletSpawn>();
                for (int i = 0; i < 600 && !e.Finished; i++) e.Tick(1f / 60, Vector2.Zero, -Vector2.UnitY, output);
                Assert.That(e.Finished, Is.True, def.shape.ToString());
                int perVolley = (def.mirror ? 2 : 1) * def.count;
                Assert.That(output.Count, Is.EqualTo(perVolley * def.shots), def.shape.ToString());
            }
        }
    }
}

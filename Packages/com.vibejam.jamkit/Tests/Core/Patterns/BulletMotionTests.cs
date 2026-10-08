using System;
using System.Numerics;
using JamKit.Patterns;
using NUnit.Framework;

namespace JamKit.Tests
{
    public class BulletMotionTests
    {
        static void Advance(ref Vector2 p, ref float angle, ref float speed, float accel, float turn, float dt, int steps)
        {
            for (int i = 0; i < steps; i++) BulletMotion.Step(ref p, ref angle, ref speed, accel, turn, dt);
        }

        [Test]
        public void StraightShotMovesAlongItsHeading()
        {
            var p = new Vector2(1, 2);
            float angle = (float)(Math.PI / 2), speed = 3f;
            BulletMotion.Step(ref p, ref angle, ref speed, 0f, 0f, 2f);
            Assert.That(p.X, Is.EqualTo(1f).Within(1e-5));
            Assert.That(p.Y, Is.EqualTo(8f).Within(1e-5));
        }

        [Test]
        public void AccelerationGivesHalfATSquared()
        {
            var p = Vector2.Zero;
            float angle = 0f, speed = 1f;
            BulletMotion.Step(ref p, ref angle, ref speed, 2f, 0f, 3f);
            Assert.That(p.X, Is.EqualTo(1f * 3f + 0.5f * 2f * 9f).Within(1e-4));
            Assert.That(speed, Is.EqualTo(7f).Within(1e-5));
        }

        [Test]
        public void ConstantTurnTracesACircle()
        {
            // speed v, turn rate w: radius v / w, back at the start after one period.
            var p = Vector2.Zero;
            float angle = 0f, speed = 2f, w = 1.5f;
            float period = (float)(2 * Math.PI / w);
            BulletMotion.Step(ref p, ref angle, ref speed, 0f, w, period / 2);
            Assert.That(p.X, Is.EqualTo(0f).Within(1e-4));
            Assert.That(p.Y, Is.EqualTo(2 * speed / w).Within(1e-4)); // diameter, turning left
            BulletMotion.Step(ref p, ref angle, ref speed, 0f, w, period / 2);
            Assert.That(p.Length(), Is.LessThan(1e-4f));
        }

        [Test]
        public void OneBigStepEqualsManySmallOnes()
        {
            var a = new Vector2(3, -1); float angA = 0.4f, spA = 2f;
            var b = a; float angB = angA, spB = spA;
            BulletMotion.Step(ref a, ref angA, ref spA, 1.3f, 0.9f, 2.4f);
            Advance(ref b, ref angB, ref spB, 1.3f, 0.9f, 2.4f / 240, 240);
            Assert.That(Vector2.Distance(a, b), Is.LessThan(1e-3f));
            Assert.That(angB, Is.EqualTo(angA).Within(1e-4));
            Assert.That(spB, Is.EqualTo(spA).Within(1e-4));
        }

        [Test]
        public void TinyTurnRateMatchesStraightLine()
        {
            // The series branch must agree with the exact branch near the threshold and with w = 0.
            var straight = Vector2.Zero; float a0 = 0.3f, s0 = 4f;
            BulletMotion.Step(ref straight, ref a0, ref s0, 1f, 0f, 1f);
            var tiny = Vector2.Zero; float a1 = 0.3f, s1 = 4f;
            BulletMotion.Step(ref tiny, ref a1, ref s1, 1f, 1e-7f, 1f);
            Assert.That(Vector2.Distance(straight, tiny), Is.LessThan(1e-5f));

            var series = Vector2.Zero; float a2 = 0f, s2 = 4f;
            BulletMotion.Step(ref series, ref a2, ref s2, 1f, 0.00999f, 1f);
            var exact = Vector2.Zero; float a3 = 0f, s3 = 4f;
            BulletMotion.Step(ref exact, ref a3, ref s3, 1f, 0.01001f, 1f);
            Assert.That(Vector2.Distance(series, exact), Is.LessThan(1e-3f));
        }

        [Test]
        public void DecelerationStopsInsteadOfReversing()
        {
            var p = Vector2.Zero;
            float angle = 0f, speed = 3f;
            BulletMotion.Step(ref p, ref angle, ref speed, -4f, 0f, 5f);
            Assert.That(speed, Is.EqualTo(0f));
            Assert.That(p.X, Is.EqualTo(3f * 0.75f - 0.5f * 4f * 0.75f * 0.75f).Within(1e-5));

            // Frame by frame it parks at the same spot and stays there.
            var q = Vector2.Zero; float angQ = 0f, spQ = 3f;
            Advance(ref q, ref angQ, ref spQ, -4f, 0f, 1f / 60, 300);
            Assert.That(Vector2.Distance(p, q), Is.LessThan(1e-4f));
            Assert.That(spQ, Is.EqualTo(0f));
        }
    }
}

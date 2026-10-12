using System;
using JamKit.Movement;
using NUnit.Framework;

namespace JamKit.Tests.Movement
{
    /// <summary>The camera and body springs step the same whatever the frame length (2026-10-12): the landing dip was a
    /// per-frame Euler step, and one long frame on a hard landing flung the camera up to 0.3 m off the body, out of the
    /// head's cut-away (DreamEscapePlayTests.NadiasBodyCarriesTheMovesInTheDream).</summary>
    public class SpringStepTests
    {
        // MovementCamera's landing dip: frequency 20, kicked by landDip (0.12 m) x frequency x e at a full landing.
        const float W = 20f, Kick = -0.12f * 20f * 2.718f;

        static float[] Dip(float dt, float seconds)
        {
            float x = 0f, v = Kick;
            int n = (int)Math.Round(seconds / dt);
            var path = new float[n];
            for (int i = 0; i < n; i++)
            {
                MoveMath.CriticalSpring(ref x, ref v, 0f, W, dt);
                path[i] = x;
            }
            return path;
        }

        [Test]
        public void TheLandingDipIsTheSameCurveAtAnyFrameLength()
        {
            // The exact curve from rest kicked down: x(t) = kick t e^(-w t).
            float Exact(float t) => Kick * t * (float)Math.Exp(-W * t);
            var fine = Dip(0.001f, 0.6f);
            foreach (float dt in new[] { 1f / 240f, 1f / 60f, 1f / 30f, 0.05f, 0.1f })
            {
                var coarse = Dip(dt, 0.6f);
                for (int i = 0; i < coarse.Length; i++)
                {
                    Assert.That(coarse[i], Is.EqualTo(Exact((i + 1) * dt)).Within(1e-3f), $"dt {dt:0.000}, step {i}");
                    Assert.That(coarse[i], Is.LessThanOrEqualTo(1e-4f), $"dt {dt:0.000}: a landing dip never throws the camera up");
                }
            }
            float lowest = 0f;
            foreach (var x in fine) lowest = Math.Min(lowest, x);
            Assert.That(lowest, Is.EqualTo(-0.12f).Within(0.003f), "it dips the landDip, as designed");
        }

        [Test]
        public void TheOldEulerStepThrewTheCameraUpOnALongFrame()
        {
            // What the camera did before (kept here as the regression's measure): one 50 ms frame after the kick.
            float x = 0f, v = Kick, dt = 0.05f;
            v += (-W * W * x - 2f * W * v) * dt;
            x += v * dt;
            Assert.That(x, Is.GreaterThan(0.25f), "Euler flung it 0.33 m the wrong way");
            float y = 0f, u = Kick;
            MoveMath.CriticalSpring(ref y, ref u, 0f, W, dt);
            Assert.That(y, Is.LessThan(0f).And.GreaterThan(-0.13f), "exact: still on its way down");
        }

        [Test]
        public void TheUnderdampedSpringsMatchShortStepsOnALongFrame()
        {
            // The camera's roll (14, 0.65) after a wall catch's kick, and the body's lanyard-style springs.
            foreach (var (w, zeta) in new[] { (14f, 0.65f), (8f, 0.3f), (30f, 1f) })
            {
                float a = 0f, av = 2f * w, b = 0f, bv = 2f * w;
                MoveMath.Spring(ref a, ref av, 0.2f, w, zeta, 0.1f);
                for (int i = 0; i < 6; i++) MoveMath.Spring(ref b, ref bv, 0.2f, w, zeta, 1f / 60f);
                Assert.That(a, Is.EqualTo(b).Within(1e-3f), $"w {w}, damping {zeta}: one 0.1 s frame steps as six 60 fps frames do");
                Assert.That(Math.Abs(a), Is.LessThan(2f), "bounded (a single Euler step this long at w 30 is not)");
            }
        }
    }
}

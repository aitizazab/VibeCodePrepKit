using System.Numerics;
using JamKit.Movement;
using JamKit.Stealth;
using NUnit.Framework;

namespace JamKit.Tests.Stealth
{
    /// <summary>The senses (docs/STEALTH.md): the vision cone, periphery and close sense; line of sight to three body
    /// points; distance, posture, light and hiding spots; the detection meter filling over time; the box world's line of
    /// sight; noise from the movement kit.</summary>
    public class StealthSenseTests
    {
        static readonly Vector3 Eye = new Vector3(0f, 1.6f, 0f);

        static StealthTarget Walker(float x, float z, float height = 1.7f, float speed = 2f)
        {
            var t = StealthTarget.At(new Vector3(x, 0f, z), height);
            t.speed = speed;
            return t;
        }

        static float See(StealthDef def, IStealthWorld w, in StealthTarget t, out SightZone zone, HideKind? hide = null)
            => StealthSight.Visibility(def, w, Eye, 0f, t, hide, out zone);

        [Test]
        public void TheConeSeesFullyThePeripheryLessAndBehindNothingButUpClose()
        {
            var def = new StealthDef();
            var w = new StealthBoxWorld();
            Assert.That(See(def, w, Walker(0f, 4f), out var z1), Is.EqualTo(1f).Within(1e-3f));
            Assert.That(z1, Is.EqualTo(SightZone.Cone));
            // 60 degrees off: the peripheral band.
            Assert.That(See(def, w, Walker(4f * 0.866f, 4f * 0.5f), out var z2), Is.EqualTo(def.peripheralScale).Within(1e-3f));
            Assert.That(z2, Is.EqualTo(SightZone.Peripheral));
            Assert.That(See(def, w, Walker(0f, -4f), out var z3), Is.EqualTo(0f), "behind, out of the close sense");
            Assert.That(z3, Is.EqualTo(SightZone.None));
            Assert.That(See(def, w, Walker(0f, -1f), out var z4), Is.GreaterThan(0.5f), "a breath on the neck");
            Assert.That(z4, Is.EqualTo(SightZone.Close));
        }

        [Test]
        public void AVerticalLimitHidesWhatsHighAboveAndTheDefaultHasNone()
        {
            var def = new StealthDef { verticalHalfAngle = 15f };
            var w = new StealthBoxWorld();
            var onRoof = StealthTarget.At(new Vector3(0f, 2.7f, 5f));
            onRoof.speed = 2f;
            Assert.That(StealthSight.Visibility(def, w, Eye, 0f, onRoof, null, out var zone), Is.EqualTo(0f), "2.7 m up, 5 m off: above a 15 degree gaze");
            Assert.That(zone, Is.EqualTo(SightZone.None));
            Assert.That(StealthSight.Visibility(def, w, Eye, 0f, Walker(0f, 5f), null, out _), Is.GreaterThan(0.9f), "level: seen as before");
            var far = StealthTarget.At(new Vector3(0f, 2.7f, 16f));
            far.speed = 2f;
            Assert.That(StealthSight.Visibility(def, w, Eye, 0f, far, null, out _), Is.GreaterThan(0f), "far enough away it's within the gaze again");
            Assert.That(StealthSight.Visibility(new StealthDef(), w, Eye, 0f, onRoof, null, out _), Is.GreaterThan(0f), "the default (90) has no limit");
        }

        [Test]
        public void DistanceFadesSightAndRangeEndsIt()
        {
            var def = new StealthDef();
            var w = new StealthBoxWorld();
            float near = See(def, w, Walker(0f, def.nearRange), out _);
            float far = See(def, w, Walker(0f, def.sightRange - 0.5f), out _);
            Assert.That(near, Is.EqualTo(1f).Within(1e-3f));
            Assert.That(far, Is.InRange(def.farScale, 0.45f));
            Assert.That(See(def, w, Walker(0f, def.sightRange + 1f), out _), Is.EqualTo(0f));
        }

        [Test]
        public void LowCoverHidesACrouchedBodyAndShowsAStandingHead()
        {
            var def = new StealthDef();
            var w = new StealthBoxWorld().AddBlocker(new Vector3(0f, 0.55f, 5f), new Vector3(4f, 1.1f, 0.5f));
            var crouched = Walker(0f, 6f, 1f);
            crouched.crouched = true;
            Assert.That(See(def, w, crouched, out _), Is.EqualTo(0f), "crouched behind a 1.1 m wall: unseen");
            float standing = See(def, w, Walker(0f, 6f), out _);
            Assert.That(standing, Is.EqualTo(1f / 3f).Within(0.02f), "standing: only the head shows (1 of 3 points)");
            var wall = new StealthBoxWorld().AddBlocker(new Vector3(0f, 2f, 5f), new Vector3(4f, 4f, 0.5f));
            Assert.That(See(def, wall, Walker(0f, 6f), out var zone), Is.EqualTo(0f), "a tall wall hides everything");
            Assert.That(zone, Is.EqualTo(SightZone.None));
        }

        [Test]
        public void PostureLightAndHidingSpotsChangeHowWellYouAreSeen()
        {
            var def = new StealthDef();
            var w = new StealthBoxWorld();
            float walk = See(def, w, Walker(0f, 4f), out _);
            var crouch = Walker(0f, 4f, 1f);
            crouch.crouched = true;
            var slide = Walker(0f, 4f, 1f, 9f);
            slide.sliding = true;
            Assert.That(See(def, w, crouch, out _), Is.EqualTo(walk * def.crouchScale).Within(1e-3f));
            Assert.That(See(def, w, slide, out _), Is.EqualTo(def.slideScale * def.fastScale).Within(1e-3f), "sliding is low but fast");
            Assert.That(See(def, w, Walker(0f, 4f, 1.7f, 9f), out _), Is.EqualTo(def.fastScale).Within(1e-3f), "sprinting draws the eye");
            Assert.That(See(def, w, Walker(0f, 4f, 1.7f, 0f), out _), Is.EqualTo(def.stillScale).Within(1e-3f), "standing still");

            var dark = new StealthBoxWorld { ambient = 0f };
            Assert.That(See(def, dark, Walker(0f, 4f), out _), Is.EqualTo(def.shadowScale).Within(1e-3f), "full shadow");
            var lamp = new StealthBoxWorld { ambient = 0f }.AddLight(new Vector3(0f, 1f, 4f), new Vector3(3f, 3f, 3f), 1f);
            Assert.That(See(def, lamp, Walker(0f, 4f), out _), Is.EqualTo(1f).Within(1e-3f), "under the lamp");

            Assert.That(See(def, w, Walker(0f, 4f), out _, HideKind.Bush), Is.EqualTo(def.bushScale).Within(1e-3f), "in a bush");
            Assert.That(See(def, w, Walker(0f, 1.2f), out _, HideKind.Bush), Is.EqualTo(1f).Within(1e-3f), "a bush right next to the seeker hides nothing");
            Assert.That(See(def, w, Walker(0f, 2f), out _, HideKind.Closet), Is.EqualTo(0f));
            Assert.That(See(def, w, Walker(0f, 2f), out _, HideKind.Under), Is.EqualTo(0f));
        }

        [Test]
        public void TheMeterFillsOverTimeNotAtOnceAndDrainsAfterADelay()
        {
            var def = new StealthDef();
            var m = new DetectionMeter();
            float dt = 1f / 60f;
            float t = 0f;
            while (!m.Full && t < 5f) { m.Tick(def, def.fillRate * 1f, dt); t += dt; }
            Assert.That(t, Is.EqualTo(1f / def.fillRate).Within(0.05f), "visibility 1 takes 1 / fillRate seconds (0.8 s)");
            Assert.That(t, Is.GreaterThan(0.5f), "never instant");
            m.Set(0.5f);
            for (int i = 0; i < (int)(def.drainDelay * 60f) - 2; i++) m.Tick(def, 0f, dt);
            Assert.That(m.Value, Is.EqualTo(0.5f).Within(1e-4f), "holds for the drain delay");
            for (int i = 0; i < 60; i++) m.Tick(def, 0f, dt);
            Assert.That(m.Value, Is.LessThan(0.5f - def.drainRate * 0.8f));
            for (int i = 0; i < 600; i++) m.Tick(def, 0f, dt);
            Assert.That(m.Value, Is.EqualTo(0f));
        }

        [Test]
        public void TheBoxWorldBlocksOnlyLinesThroughABox()
        {
            var w = new StealthBoxWorld().AddBlocker(new Vector3(0f, 1f, 5f), new Vector3(2f, 2f, 2f));
            Assert.That(w.LineClear(new Vector3(0f, 1f, 0f), new Vector3(0f, 1f, 10f)), Is.False, "through it");
            Assert.That(w.LineClear(new Vector3(0f, 1f, 0f), new Vector3(0f, 1f, 3.9f)), Is.True, "stops short");
            Assert.That(w.LineClear(new Vector3(3f, 1f, 0f), new Vector3(3f, 1f, 10f)), Is.True, "beside it");
            Assert.That(w.LineClear(new Vector3(0f, 2.5f, 0f), new Vector3(0f, 2.5f, 10f)), Is.True, "over it");
            Assert.That(w.LineClear(new Vector3(-5f, 1f, 5f), new Vector3(5f, 1f, 5f)), Is.False, "across it");
        }

        [Test]
        public void HardLandingsSprintingWallrunsAndSlidesMakeNoiseWalkingDoesNot()
        {
            var def = new StealthDef();
            var rules = new StealthNoiseRules();
            var feet = new Vector3(1f, 0f, 2f);
            var soft = new MoveEvents { landed = true, landImpact = def.hardLandingSpeed * 0.6f };
            Assert.That(rules.Tick(def, MoveMode.Grounded, soft, feet, 0.016f).Any, Is.False, "a soft landing is quiet");
            var hard = new MoveEvents { landed = true, landImpact = def.hardLandingSpeed * 1.2f };
            var n = rules.Tick(def, MoveMode.Grounded, hard, feet, 0.016f);
            Assert.That(n.kind, Is.EqualTo(NoiseKind.Landing));
            Assert.That(n.radius, Is.GreaterThan(def.landingNoiseRadius));
            Assert.That(n.position, Is.EqualTo(feet));

            int Count(MoveMode mode, float seconds)
            {
                var r = new StealthNoiseRules();
                int c = 0;
                for (float t = 0f; t < seconds; t += 0.01f) if (r.Tick(def, mode, default, feet, 0.01f).Any) c++;
                return c;
            }
            Assert.That(Count(MoveMode.Grounded, 2f), Is.EqualTo(0), "walking is silent");
            Assert.That(Count(MoveMode.Sprint, 2f), Is.EqualTo((int)(2f / def.footstepEvery) + 1).Within(1), "a sprint step every 0.35 s");
            Assert.That(Count(MoveMode.Wallrun, 1f), Is.GreaterThan(1));
            var slide = new MoveEvents { slideStarted = true };
            Assert.That(new StealthNoiseRules().Tick(def, MoveMode.Slide, slide, feet, 0.01f).kind, Is.EqualTo(NoiseKind.Slide));
        }
    }
}

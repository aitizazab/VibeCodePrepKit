using System;
using System.Numerics;
using JamKit.Stealth;
using NUnit.Framework;

namespace JamKit.Tests.Stealth
{
    /// <summary>Peeking and sight (docs/STEALTH.md "Peeking"): behind a door frame she's unseen; leaning her head round it
    /// shows the seeker a small head (sight looks for it where her eyes really are), so his meter crawls; stepping out into
    /// the doorway shows all of her, and the meter fills several times faster. In the open a lean hides nothing.</summary>
    public class StealthPeekTests
    {
        // A wall along x at z = 0 (0.12 thick, 2.4 high) with a doorway from x = 0 to 0.9. She's on the -z side, 0.6 m back
        // from the wall, her body behind the jamb at x = 0; the seeker is on the +z side, 3 m out, a little past the far jamb.
        static StealthBoxWorld Doorway() => new StealthBoxWorld()
            .AddBlocker(new Vector3(-2.5f, 1.2f, 0f), new Vector3(5f, 2.4f, 0.12f))
            .AddBlocker(new Vector3(3.4f, 1.2f, 0f), new Vector3(5f, 2.4f, 0.12f))
            .AddBlocker(new Vector3(0.45f, 2.25f, 0f), new Vector3(0.9f, 0.3f, 0.12f));

        static readonly Vector3 Eye = new Vector3(0.7f, 1.2f, 3f);

        static StealthTarget Her(float x, float leanX)
        {
            var t = StealthTarget.At(new Vector3(x, 0f, -0.6f));
            t.speed = 0f;
            t.lean = new Vector3(leanX, 0f, 0f);
            return t;
        }

        static float YawTo(Vector3 to) => (float)(Math.Atan2(to.X - Eye.X, to.Z - Eye.Z) * 180.0 / Math.PI);

        static float See(StealthDef def, IStealthWorld w, in StealthTarget t)
            => StealthSight.Visibility(def, w, Eye, YawTo(t.feet), t, null, out _);

        static float SecondsToFound(StealthDef def, float visibility)
        {
            var meter = new DetectionMeter();
            float t = 0f;
            while (!meter.Full && t < 120f) { meter.Tick(def, visibility * def.fillRate, 0.05f); t += 0.05f; }
            return t;
        }

        [Test]
        public void PeekingRoundADoorFrameShowsASmallHeadAndSteppingOutShowsAllOfHer()
        {
            var def = new StealthDef();
            var w = Doorway();
            float hidden = See(def, w, Her(-0.45f, 0f));
            float peek = See(def, w, Her(-0.45f, 0.4f));
            float stepped = See(def, w, Her(0.45f, 0f));
            TestContext.Out.WriteLine($"behind the frame {hidden:0.000}, peeking {peek:0.000}, stepped out {stepped:0.000}");
            Assert.That(hidden, Is.EqualTo(0f), "behind the frame, upright: unseen");
            Assert.That(peek, Is.GreaterThan(0f), "her head shows round the frame: he can see it (it isn't free)");
            Assert.That(stepped, Is.GreaterThan(0.5f), "in the doorway: all of her");
            Assert.That(peek, Is.LessThanOrEqualTo(stepped * 0.2f), "a head is a small silhouette");

            float peekTime = SecondsToFound(def, peek), stepTime = SecondsToFound(def, stepped);
            TestContext.Out.WriteLine($"meter full: peeking {peekTime:0.0} s, stepped out {stepTime:0.0} s");
            Assert.That(peekTime, Is.GreaterThanOrEqualTo(stepTime * 5f), "peeking is clearly the safer way to look");
            Assert.That(stepTime, Is.LessThan(2.5f));
        }

        [Test]
        public void TheHeadIsLookedForWhereTheEyesAreNotOverTheFeet()
        {
            var def = new StealthDef();
            // A low wall (1.1 m) only on her right: standing straight her head shows over it; leaning left behind a tall post
            // takes the head out of sight. Here: a tall post between him and her head when she leans into it.
            var w = new StealthBoxWorld().AddBlocker(new Vector3(-0.85f, 1.5f, -0.35f), new Vector3(0.3f, 3f, 0.1f));
            var straight = Her(-0.45f, 0f);
            var leaning = Her(-0.45f, -0.4f);
            // He's square in front of her, close: the post hides the leaning head only if sight follows the lean.
            var eye = new Vector3(-0.85f, 1.2f, 1.5f);
            float a = StealthSight.Visibility(def, w, eye, 180f, straight, null, out _);
            float b = StealthSight.Visibility(def, w, eye, 180f, leaning, null, out _);
            Assert.That(a, Is.GreaterThan(0f));
            Assert.That(b, Is.LessThan(a), "leaning behind the post hides her head from him");
        }

        [Test]
        public void InTheOpenALeanHidesNothingAndATinyLeanIsntAPeek()
        {
            var def = new StealthDef();
            var open = new StealthBoxWorld();
            float straight = See(def, open, Her(-0.45f, 0f));
            float leaning = See(def, open, Her(-0.45f, 0.4f));
            Assert.That(leaning, Is.EqualTo(straight).Within(1e-4f), "no cover: a lean is no hiding");
            var w = Doorway();
            Assert.That(See(def, w, Her(-0.45f, 0.05f)), Is.EqualTo(0f), "5 cm isn't round the frame");
        }
    }
}

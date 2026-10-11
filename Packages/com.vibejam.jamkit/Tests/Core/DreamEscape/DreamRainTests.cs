using System;
using System.Numerics;
using JamKit.DreamEscape;
using JamKit.Movement;
using JamKit.Tests.Movement;
using NUnit.Framework;

namespace JamKit.Tests.DreamEscape
{
    /// <summary>The umbrella and the rain at the escape's front door: the umbrella leans by the door, beside the lane, on
    /// the porch; the dream tool's aim locks it when you look at it; taking it keeps her dry in the rain; going on
    /// without it soaks her (slower, lower jumps, darker edges) and, past the point of no return, is the Despair choice,
    /// once per run; a reset puts the umbrella back; the rain path runs to the end with no falls.</summary>
    public class DreamRainTests
    {
        static DreamEscapeCourse Build() => DreamEscapeCourse.Build();

        [Test]
        public void TheUmbrellaLeansByTheFrontDoorBesideTheLane()
        {
            var d = Build();
            Assert.That(d.rainStartS, Is.GreaterThan(d.course.finishS), "the rain is past the end of the chase");
            Assert.That(d.rainStartS, Is.GreaterThan(d.exitLight.Z), "the door is past the light");
            Assert.That(Math.Abs(d.umbrella.Z - d.rainStartS), Is.LessThan(1.5f), "next to the door");
            Assert.That(Math.Abs(d.umbrella.X - d.course.LaneAt(d.umbrella.Z)), Is.InRange(1.5f, 4f), "beside the lane, not in it");
            Assert.That(d.FloorTopAt(d.umbrella.X, d.umbrella.Z, d.umbrella.Y, out float top), Is.True, "standing on the porch");
            Assert.That(d.umbrella.Y - top, Is.InRange(0.3f, 1.2f));
            Assert.That(d.despairS, Is.GreaterThan(d.rainStartS + 3f), "room to step out, see the rain and turn back");
            Assert.That(d.rainEndS, Is.GreaterThan(d.despairS + 10f));
            Assert.That(d.umbrellaResetS, Is.LessThan(d.umbrella.Z));
            Assert.That(d.course.SectionAt(d.rainStartS + 1f), Is.EqualTo("The rain"));
            for (float s = d.rainStartS; s <= d.rainEndS; s += 1f)
                Assert.That(d.FloorTopAt(d.course.LaneAt(s), s, 100f, out _), Is.True, "the rain path is walkable at s " + s);
        }

        [Test]
        public void TheToolLocksTheUmbrellaWhenYouLookAtIt()
        {
            var d = Build();
            var rain = new DreamRain(d);
            var tool = new DreamToolDef();
            var eye = new Vector3(0f, d.ExitTop + 1.5f, d.umbrella.Z - 8f);
            var at = Vector3.Normalize(d.umbrella - eye);
            Assert.That(rain.Locks(eye, at, tool.coneMouse, tool.range), Is.True, "looking at it");
            // Roughly at it: 8 degrees off its middle still locks (it is about 4 degrees wide each side here).
            float a = 8f * (float)Math.PI / 180f;
            var off = Vector3.Normalize(new Vector3(at.X * (float)Math.Cos(a) - at.Z * (float)Math.Sin(a), at.Y, at.X * (float)Math.Sin(a) + at.Z * (float)Math.Cos(a)));
            Assert.That(rain.Locks(eye, off, tool.conePad, tool.range), Is.True, "roughly at it, on a pad");
            Assert.That(rain.Locks(eye, new Vector3(-1f, 0f, 0f), tool.conePad, tool.range), Is.False, "looking away");
            Assert.That(rain.Locks(eye - new Vector3(0f, 0f, 60f), at, tool.conePad, tool.range), Is.False, "out of range");
            Assert.That(rain.PickUp(), Is.True);
            Assert.That(rain.Locks(eye, at, tool.conePad, tool.range), Is.False, "taken");
            Assert.That(rain.PickUp(), Is.False, "once");
        }

        [Test]
        public void WithTheUmbrellaSheStaysDryAndNoDespair()
        {
            var d = Build();
            var rain = new DreamRain(d);
            rain.PickUp();
            bool entered = false, despair = false;
            for (float s = d.rainStartS - 5f; s <= d.rainEndS; s += 0.1f)
            {
                var ev = rain.Tick(0.01f, s);
                entered |= ev.enteredRain;
                despair |= ev.despairChosen;
            }
            Assert.That(entered, Is.True);
            Assert.That(rain.InRain && !rain.Exposed, Is.True);
            Assert.That(rain.Soak, Is.EqualTo(0f));
            Assert.That(rain.SpeedScale, Is.EqualTo(1f));
            Assert.That(despair || rain.DespairThisRun, Is.False, "covered: she continues normally");
        }

        [Test]
        public void WithoutItTheRainSoaksHerAndGoingOnIsDespair()
        {
            var d = Build();
            var rain = new DreamRain(d);
            // A step out into the rain and back: wet, then drying; no choice made yet.
            rain.Tick(0.5f, d.rainStartS + 1f);
            Assert.That(rain.Exposed, Is.True);
            float wet = rain.Soak;
            Assert.That(wet, Is.GreaterThan(0f));
            Assert.That(rain.SpeedScale, Is.LessThan(1f), "heavier");
            Assert.That(rain.Vignette, Is.GreaterThan(0f), "darker at the edges");
            rain.Tick(0.5f, d.rainStartS - 2f);
            Assert.That(rain.Soak, Is.LessThan(wet), "drying indoors");
            Assert.That(rain.DespairThisRun, Is.False, "stepping out and back isn't the choice");

            // Going on: soaked through, slower, and past the point of no return the Despair choice, once.
            int despairs = 0;
            for (float s = d.rainStartS; s <= d.rainEndS; s += 0.4f)
                if (rain.Tick(0.1f, s).despairChosen) despairs++; // a slow 4 m/s walk: 8 s in the rain
            Assert.That(despairs, Is.EqualTo(1));
            Assert.That(rain.DespairThisRun, Is.True);
            Assert.That(rain.Soak, Is.EqualTo(1f).Within(1e-4f), "soaked through by the end");
            Assert.That(rain.SpeedScale, Is.EqualTo(rain.def.soakedSpeed).Within(1e-4f));
            Assert.That(rain.JumpScale, Is.EqualTo(rain.def.soakedJump).Within(1e-4f));
            Assert.That(rain.def.soakedSpeed, Is.InRange(0.6f, 0.9f), "a modest, readable cost");

            // Taking the umbrella late doesn't undo the choice; a reset (a new run) does, and the umbrella is back.
            rain.PickUp();
            Assert.That(rain.DespairThisRun, Is.True);
            rain.Reset();
            Assert.That(rain.HasUmbrella || rain.DespairThisRun || rain.Soak > 0f, Is.False);
            Assert.That(DreamRain.DespairFlag, Is.Not.Empty);
        }

        [Test]
        public void TheAutopilotRunsFromTheLastCheckpointThroughTheDoorToTheEndOfTheRain()
        {
            var d = Build();
            int last = d.course.checkpoints.Count - 1;
            var r = LabCourseTests.Drive(d.course, last, d.rainEndS, 30f, 60, false);
            if (!r.finished || r.falls > 0) TestContext.Out.WriteLine(r.log.ToString());
            Assert.That(r.finished, Is.True);
            Assert.That(r.falls, Is.EqualTo(0));
        }
    }
}

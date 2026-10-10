using System;
using System.Numerics;
using JamKit.Movement;
using NUnit.Framework;

namespace JamKit.Tests.Movement
{
    /// <summary>The first-person body's feel (BodyFeel, docs/MOVEMENT.md "The body"): the wall hand lands on
    /// its spot within two frames of the catch, the body takes the landing after the camera, the pump is zero
    /// in the air and in step with the footfalls, each speed tier changes the posture, and Motion effects 0
    /// turns every effect off but keeps the contact cues.</summary>
    public class BodyFeelTests
    {
        const float Dt = 1f / 60f;

        static BodyFeelInput Input(MoveMode mode, float speed, float phase = 0f, float motion = 1f, float sprintSpeed = 9f) => new BodyFeelInput
        {
            dt = Dt,
            mode = mode,
            speed = speed,
            sprintSpeed = sprintSpeed,
            velocityBody = new Vector3(0f, 0f, speed),
            stridePhase = phase,
            motion = motion,
        };

        static void Run(BodyFeel f, BodyFeelInput i, int frames)
        {
            for (int n = 0; n < frames; n++)
            {
                f.Tick(i);
                i.events = default;
            }
        }

        [Test]
        public void TheWallHandLandsOnItsSpotWithinTwoFramesOfTheCatch()
        {
            var f = new BodyFeel(new BodyFeelDef());
            Run(f, Input(MoveMode.Air, 8f), 30);
            var target = new Vector3(0.55f, -0.06f, 0.34f);
            var i = Input(MoveMode.Wallrun, 8.6f);
            i.wallSide = 1;
            i.hasWallTarget = true;
            i.wallHandTarget = target;
            i.events.wallrunStarted = true;
            i.events.wallSide = 1;
            f.Tick(i);
            Assert.That(f.pose.contact[1], Is.True, "the hand is on the wall from the catch");
            i.events = default;
            f.Tick(i);
            Assert.That(Vector3.Distance(f.pose.hand[1], target), Is.LessThan(1e-4f), "on its spot by the second frame");
            Assert.That(f.pose.contact[0], Is.False, "the other hand swings");
            // It drags along the wall: the spot moves with her and the hand stays on it.
            i.wallHandTarget = target + new Vector3(0f, 0f, -0.02f);
            f.Tick(i);
            Assert.That(Vector3.Distance(f.pose.hand[1], i.wallHandTarget), Is.LessThan(1e-4f));
        }

        [Test]
        public void TheBodyTakesTheLandingAMomentAfterTheCamera()
        {
            var def = new BodyFeelDef();
            var f = new BodyFeel(def);
            var i = Input(MoveMode.Grounded, 0f);
            Run(f, i, 60);
            // The camera dips at once: a spring kicked down at the landing frame.
            float camDip = 0f, camV = 0f;
            float t = 0f, cameraStart = -1f, bodyStart = -1f;
            for (int n = 0; n < 60; n++)
            {
                if (n == 0) camV -= 0.06f * 20f * 2.718f;
                camV += (-400f * camDip - 40f * camV) * Dt;
                camDip += camV * Dt;
                i.cameraDip = camDip;
                i.events = default;
                if (n == 0) { i.events.landed = true; i.events.landImpact = 10f; }
                f.Tick(i);
                if (cameraStart < 0f && camDip < -1e-4f) cameraStart = t;
                if (bodyStart < 0f && f.pose.bodyDip < -1e-4f) bodyStart = t;
                t += Dt;
            }
            Assert.That(cameraStart, Is.EqualTo(0f), "the camera first");
            Assert.That(bodyStart - cameraStart, Is.InRange(def.landLag - Dt * 0.5f, def.landLag + Dt * 1.5f), "the body about 0.03 s later");
        }

        [Test]
        public void ThePumpIsZeroInTheAirAndInStepWithTheFootfalls()
        {
            var f = new BodyFeel(new BodyFeelDef());
            Run(f, Input(MoveMode.Air, 9f, 0.3f), 90);
            Assert.That(f.pose.pump, Is.EqualTo(0f));
            var a = f.pose.hand[1];
            Run(f, Input(MoveMode.Air, 9f, 1.3f), 1);
            Assert.That(Vector3.Distance(f.pose.hand[1], a), Is.LessThan(1e-3f), "the stride doesn't move the hands in the air");

            Run(f, Input(MoveMode.Sprint, 6.5f, 0f, 1f, 6.5f), 2);
            Assert.That(f.pose.pump, Is.EqualTo(new BodyFeelDef().pumpForward).Within(1e-4f), "about 4 cm at a full run (the running tier)");
            // The arms are at their extremes at each footfall: the right hand forward when the left foot lands.
            Assert.That(BodyFeel.Swing(1f, 0f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(BodyFeel.Swing(1f, 1f), Is.EqualTo(-1f).Within(1e-5f));
            Assert.That(BodyFeel.Swing(-1f, 0f), Is.EqualTo(-1f).Within(1e-5f));
        }

        [Test]
        public void EachFootPlantsOnItsFootstep()
        {
            var f = new BodyFeel(new BodyFeelDef());
            for (int k = 0; k < 4; k++)
            {
                Run(f, Input(MoveMode.Sprint, 9f, k + 0.001f), 1);
                int foot = k % 2;
                Assert.That(f.pose.footPlanted[foot], Is.True, "step " + k + ": foot " + foot + " lands on the footstep");
                Assert.That(f.pose.foot[foot].Y, Is.EqualTo(0f));
                Assert.That(f.pose.foot[foot].Z, Is.GreaterThan(0.3f), "it lands in front of her");
                Assert.That(f.pose.footPlanted[1 - foot], Is.False, "the other foot is in the air (running)");
            }
        }

        [Test]
        public void EachSpeedTierChangesThePosture()
        {
            float[] speeds = { 6f, 10.5f, 16f };
            var pitch = new float[3];
            var pump = new float[3];
            var z = new float[3];
            for (int t = 0; t < 3; t++)
            {
                var f = new BodyFeel(new BodyFeelDef());
                Run(f, Input(MoveMode.Sprint, speeds[t], 0.5f, 1f, 6f), 120);
                pitch[t] = f.pose.torsoPitch;
                pump[t] = f.pose.pump;
                z[t] = f.pose.hand[0].Z + f.pose.hand[1].Z;
                Assert.That(f.pose.tier, Is.EqualTo(t), "tier at " + speeds[t] + " m/s");
            }
            Assert.That(pitch[1], Is.GreaterThan(pitch[0] + 1f), "she leans in at 'fast'");
            Assert.That(pitch[2], Is.GreaterThan(pitch[1] + 2f), "and more at 'flying'");
            Assert.That(pump[1], Is.LessThan(pump[0] * 0.9f), "the pump tightens");
            Assert.That(pump[2], Is.LessThan(pump[1] * 0.8f));
            Assert.That(z[2], Is.LessThan(z[1] - 0.1f), "the arms sweep back at 'flying'");
        }

        [Test]
        public void SheLeansIntoTurnsAndTheArmsTrailTheView()
        {
            var f = new BodyFeel(new BodyFeelDef());
            var i = Input(MoveMode.Sprint, 10f, 0.5f);
            i.lookYaw = 3f; // 180 degrees a second, to the right
            Run(f, i, 30);
            Assert.That(f.pose.torsoRoll, Is.GreaterThan(1f), "into the turn");
            Assert.That(f.pose.swayYaw, Is.LessThan(-1f), "the arms lag behind a turn to the right");
        }

        [Test]
        public void MotionEffectsZeroTurnsTheEffectsOffButKeepsContact()
        {
            var def = new BodyFeelDef();
            var f = new BodyFeel(def);
            var i = Input(MoveMode.Sprint, 16f, 0.3f, 0f);
            i.lookYaw = 4f;
            i.events.sprintKick = true;
            i.events.jumped = true;
            Run(f, i, 120);
            Assert.That(f.pose.torsoPitch, Is.EqualTo(0f));
            Assert.That(f.pose.torsoRoll, Is.EqualTo(0f));
            Assert.That(f.pose.swayYaw, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(f.pose.pump, Is.EqualTo(0f));
            Assert.That(f.pose.armDipOffset, Is.EqualTo(0f));
            Assert.That(f.pose.lanyardRoll, Is.EqualTo(0f).Within(1e-5f));
            for (int k = 0; k < 2; k++)
                Assert.That(Vector3.Distance(f.pose.hand[k], new Vector3((k == 0 ? -1f : 1f) * def.restX, def.restY, def.restZ)), Is.LessThan(1e-3f), "hand " + k + " at rest");
            // Contact stays: the wall hand.
            var w = Input(MoveMode.Wallrun, 9f, 0f, 0f);
            w.wallSide = -1;
            w.hasWallTarget = true;
            w.wallHandTarget = new Vector3(-0.5f, -0.06f, 0.34f);
            w.events.wallrunStarted = true;
            Run(f, w, 3);
            Assert.That(Vector3.Distance(f.pose.hand[0], w.wallHandTarget), Is.LessThan(1e-4f), "the wall hand is a contact cue");
            // And the slide legs.
            Run(f, Input(MoveMode.Slide, 12f, 0f, 0f), 30);
            Assert.That(f.pose.legs, Is.EqualTo(1f));
        }

        [Test]
        public void TheSlideBringsTheFeetIntoViewAndTheDoubleJumpTucksThem()
        {
            var def = new BodyFeelDef();
            var f = new BodyFeel(def);
            Run(f, Input(MoveMode.Slide, 12f), 40);
            Assert.That(f.pose.legs, Is.EqualTo(1f));
            Assert.That(f.pose.foot[0].Z, Is.EqualTo(def.slideFootForward + def.slideLead).Within(1e-3f), "the leading foot out in front");
            Assert.That(f.pose.foot[0].Y, Is.EqualTo(def.slideFootHeight).Within(1e-3f));
            Assert.That(f.pose.hand[0].Y, Is.LessThan(def.restY), "the hands drop for balance");

            var g = new BodyFeel(def);
            var air = Input(MoveMode.Air, 8f);
            Run(g, air, 10);
            air.events.doubleJumped = true;
            Run(g, air, 9);
            Assert.That(g.pose.foot[1].Y, Is.GreaterThan(0.3f), "the legs tuck on the double jump");
            Assert.That(g.pose.hand[1].Z, Is.LessThan(def.airZ - 0.05f), "the arms sweep down and back");
        }

        [Test]
        public void MantleHandsPlantOnTheLedge()
        {
            var f = new BodyFeel(new BodyFeelDef());
            var i = Input(MoveMode.Mantle, 0f);
            i.hasLedge = true;
            i.ledgeLeft = new Vector3(-0.22f, -0.1f, 0.5f);
            i.ledgeRight = new Vector3(0.22f, -0.1f, 0.5f);
            i.events.mantleStarted = true;
            Run(f, i, 2);
            Assert.That(f.pose.contact[0] && f.pose.contact[1], Is.True);
            Assert.That(Vector3.Distance(f.pose.hand[0], i.ledgeLeft), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(f.pose.hand[1], i.ledgeRight), Is.LessThan(1e-4f));
        }
    }
}

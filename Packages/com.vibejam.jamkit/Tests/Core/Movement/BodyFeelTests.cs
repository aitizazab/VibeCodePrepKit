using System;
using System.Numerics;
using JamKit.Movement;
using NUnit.Framework;

namespace JamKit.Tests.Movement
{
    /// <summary>The first-person body's feel (BodyFeel, docs/MOVEMENT.md "The body", evidence in RESEARCH-MOVEMENT 8.7):
    /// the wall hand reaches out before contact and plants on the contact frame (or comes back if no catch
    /// happens), the body takes the landing after the camera, there's no jump lift but vertical inertia, the
    /// double jump's pose holds long enough to read, the slide tilts the arms (already in the air with crouch
    /// held), the mantle hands plant by IK within range and in a time set by the approach speed, the pump is zero
    /// in the air and in step with the footfalls, each tier changes the posture, and Motion effects 0 turns every
    /// effect off but keeps the contact cues.</summary>
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

        static Vector3 Rest(BodyFeelDef d, float side) => new Vector3(side * d.restX, d.restY, d.restZ);

        [Test]
        public void TheWallHandReachesOutBeforeContactAndPlantsOnTheContactFrame()
        {
            var def = new BodyFeelDef();
            var f = new BodyFeel(def);
            Run(f, Input(MoveMode.Sprint, 9f), 30);
            var target = new Vector3(0.55f, -0.06f, 0.34f);
            Vector3 start = f.pose.hand[1];
            // 0.25 s of approach (15 frames), closeness 0 -> 1.
            var a = Input(MoveMode.Air, 9f);
            a.approaching = true;
            a.approachSide = 1;
            a.approachTarget = target;
            float halfway = -1f;
            for (int n = 0; n < 15; n++)
            {
                a.approachCloseness = n / 15f;
                f.Tick(a);
                if (halfway < 0f && Vector3.Distance(f.pose.hand[1], target) < Vector3.Distance(start, target) * 0.5f) halfway = n * Dt;
            }
            Assert.That(halfway, Is.GreaterThan(0f), "the hand is more than halfway to the wall before contact (the lean-in)");
            Assert.That(f.pose.reach[1], Is.GreaterThan(0.9f));
            Assert.That(f.pose.contact[1], Is.False, "not on the wall yet");
            // Contact: planted on that very frame.
            var w = Input(MoveMode.Wallrun, 8.6f);
            w.wallSide = 1;
            w.hasWallTarget = true;
            w.wallHandTarget = target;
            w.events.wallrunStarted = true;
            w.events.wallSide = 1;
            f.Tick(w);
            Assert.That(f.pose.contact[1], Is.True);
            Assert.That(Vector3.Distance(f.pose.hand[1], target), Is.LessThan(1e-5f), "planted on the contact frame");
            // It drags along the wall; the other arm goes one-handed over 0.2 s.
            w.events = default;
            w.wallHandTarget = target + new Vector3(0f, 0f, -0.02f);
            Run(f, w, 12);
            Assert.That(Vector3.Distance(f.pose.hand[1], w.wallHandTarget), Is.LessThan(1e-5f));
            Assert.That(f.pose.wallBlend, Is.EqualTo(1f).Within(1e-4f), "one-handed pose in 0.2 s");
            // Off the wall: the pose blends back over 0.35 s.
            Run(f, Input(MoveMode.Air, 9f), 12);
            Assert.That(f.pose.wallBlend, Is.InRange(0.3f, 0.6f));
        }

        [Test]
        public void TheReachComesBackIfNoCatchHappens()
        {
            var def = new BodyFeelDef();
            var f = new BodyFeel(def);
            Run(f, Input(MoveMode.Air, 9f), 30);
            var a = Input(MoveMode.Air, 9f);
            a.approaching = true;
            a.approachSide = -1;
            a.approachTarget = new Vector3(-0.6f, -0.06f, 0.34f);
            a.approachCloseness = 0.8f;
            Run(f, a, 10);
            Assert.That(f.pose.hand[0].X, Is.LessThan(-0.45f), "reaching");
            Run(f, Input(MoveMode.Air, 9f), 40);
            Assert.That(Vector3.Distance(f.pose.hand[0], new Vector3(-def.airX, def.airY, def.airZ)), Is.LessThan(0.01f), "back smoothly, no catch");
            Assert.That(f.pose.contact[0], Is.False);
        }

        [Test]
        public void TheBodyTakesTheLandingAMomentAfterTheCamera()
        {
            var def = new BodyFeelDef();
            var f = new BodyFeel(def);
            var i = Input(MoveMode.Grounded, 0f);
            Run(f, i, 60);
            float camDip = 0f, camV = 0f;
            float t = 0f, cameraStart = -1f, bodyStart = -1f;
            for (int n = 0; n < 60; n++)
            {
                if (n == 0) camV -= 0.06f * 20f * 2.718f;
                camV += (-400f * camDip - 40f * camV) * Dt;
                camDip += camV * Dt;
                i.cameraDip = camDip;
                i.events = default;
                if (n == 0) { i.events.landed = true; i.events.landImpact = 14f; }
                f.Tick(i);
                if (cameraStart < 0f && camDip < -1e-4f) cameraStart = t;
                if (bodyStart < 0f && f.pose.bodyDip < -1e-4f) bodyStart = t;
                t += Dt;
            }
            Assert.That(cameraStart, Is.EqualTo(0f), "the camera first");
            Assert.That(bodyStart - cameraStart, Is.InRange(def.landLag - Dt * 0.5f, def.landLag + Dt * 1.5f), "the body about 0.03 s later");
        }

        [Test]
        public void NoJumpLiftTheArmsSinkWhileRisingAndFloatWhileFalling()
        {
            var def = new BodyFeelDef();
            var f = new BodyFeel(def);
            var air = Input(MoveMode.Air, 8f);
            Run(f, air, 60);
            float rest = f.pose.hand[1].Y;
            air.events.jumped = true;
            air.velocityBody = new Vector3(0f, 6f, 8f);
            Run(f, air, 30);
            Assert.That(f.pose.vertical, Is.LessThan(-0.01f), "down while rising");
            Assert.That(f.pose.hand[1].Y, Is.LessThan(rest), "no lift on the jump");
            Assert.That(f.pose.vertical, Is.GreaterThanOrEqualTo(-def.verticalSwayMax - 1e-5f));
            air.velocityBody = new Vector3(0f, -12f, 8f);
            Run(f, air, 40);
            Assert.That(f.pose.vertical, Is.GreaterThan(0.01f).And.LessThanOrEqualTo(def.verticalSwayMax + 1e-5f), "up while falling, at most about 1.5 cm");
        }

        [Test]
        public void TheDoubleJumpPoseHoldsLongEnoughToRead()
        {
            var def = new BodyFeelDef();
            var f = new BodyFeel(def);
            var air = Input(MoveMode.Air, 8f);
            Run(f, air, 20);
            air.events.doubleJumped = true;
            Run(f, air, 1);
            air.events = default;
            Run(f, air, 14); // 0.25 s
            Assert.That(f.pose.doubleJump, Is.EqualTo(1f), "still held at 0.25 s");
            Assert.That(f.pose.hand[1].Z, Is.LessThan(def.airZ - 0.1f), "down and back");
            Assert.That(f.pose.foot[1].Y, Is.GreaterThan(0.3f), "the legs tuck");
            Run(f, air, 12); // 0.45 s
            Assert.That(f.pose.doubleJump, Is.EqualTo(0f), "released by 0.4 s");
        }

        [Test]
        public void TheSlideTiltsTheArmsAlreadyInTheAirWithCrouchHeld()
        {
            var def = new BodyFeelDef();
            var f = new BodyFeel(def);
            var air = Input(MoveMode.Air, 10f);
            air.crouchHeld = true;
            Run(f, air, 30);
            Assert.That(f.pose.cant, Is.GreaterThan(0.95f), "the tilt shows before the slide");
            Assert.That(f.pose.armRoll, Is.EqualTo(def.slideCant).Within(0.5f));
            Assert.That(f.pose.hand[1].Y, Is.LessThan(f.pose.hand[0].Y - 0.05f), "tilted, not spread");
            Run(f, Input(MoveMode.Slide, 12f), 40);
            Assert.That(f.pose.cant, Is.GreaterThan(0.95f));
            Assert.That(f.pose.legs, Is.EqualTo(1f));
            Assert.That(f.pose.foot[0].Z, Is.EqualTo(def.slideFootForward + def.slideLead).Within(1e-3f), "the leading foot out in front");
            Assert.That(f.pose.torsoPitch, Is.EqualTo(-def.slideRecline).Within(0.5f), "she reclines");
            Run(f, Input(MoveMode.Air, 10f), 60);
            Assert.That(f.pose.cant, Is.LessThan(0.01f), "gone without crouch");
            var noLegs = new BodyFeelDef { slideLegs = false };
            var g = new BodyFeel(noLegs);
            Run(g, Input(MoveMode.Slide, 12f), 40);
            Assert.That(g.pose.legs, Is.EqualTo(0f), "legs in view are optional");
        }

        [Test]
        public void MantleHandsPlantOnTheLipInATimeSetByTheApproach()
        {
            var def = new BodyFeelDef();
            var fast = new BodyFeel(def);
            Run(fast, Input(MoveMode.Sprint, 8f), 30);
            var m = Input(MoveMode.Mantle, 0f);
            m.hasLedge = true;
            m.ledgeHeight = 1f;
            m.ledgeLeft = new Vector3(-0.22f, -0.25f, 0.5f);
            m.ledgeRight = new Vector3(0.22f, -0.25f, 0.5f);
            m.events.mantleStarted = true;
            Run(fast, m, 4);
            Assert.That(fast.pose.contact[0] && fast.pose.contact[1], Is.True);
            Assert.That(Vector3.Distance(fast.pose.hand[1], m.ledgeRight), Is.LessThan(1e-4f), "a fast approach reaches the lip in the shortest reach (0.06 s)");

            var slow = new BodyFeel(def);
            Run(slow, Input(MoveMode.Grounded, 2f), 30);
            Run(slow, m, 3);
            Assert.That(Vector3.Distance(slow.pose.hand[1], m.ledgeRight), Is.GreaterThan(0.01f), "a slow approach takes longer");
            Run(slow, m, 12);
            Assert.That(Vector3.Distance(slow.pose.hand[1], m.ledgeRight), Is.LessThan(1e-4f));

            // A lip far from the pose: IK moves the hand at most mantleIkRange.
            var far = new BodyFeel(def);
            var mf = m;
            mf.ledgeRight = new Vector3(0.22f, -0.25f, 1.6f);
            Run(far, mf, 30);
            Vector3 nominal = Vector3.Lerp(new Vector3(def.mantleLowX, def.mantleLowY, def.mantleLowZ), new Vector3(def.mantleHighX, def.mantleHighY, def.mantleHighZ), 0.5f);
            Assert.That(Vector3.Distance(far.pose.hand[1], nominal), Is.EqualTo(def.mantleIkRange).Within(1e-3f));
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
            Assert.That(pitch[2], Is.GreaterThan(pitch[1] + 1f), "and more at 'flying'");
            Assert.That(pump[1], Is.LessThan(pump[0] * 0.95f), "the pump tightens");
            Assert.That(pump[2], Is.LessThan(pump[1] * 0.9f));
            Assert.That(z[2], Is.LessThan(z[1] - 0.05f), "the arms draw back at 'flying'");
        }

        [Test]
        public void TheArmsLagTurnsAndRollWhenStrafing()
        {
            var def = new BodyFeelDef();
            var f = new BodyFeel(def);
            var i = Input(MoveMode.Sprint, 10f, 0.5f);
            i.lookYaw = 6f; // 360 degrees a second, to the right
            Run(f, i, 40);
            Assert.That(f.pose.torsoRoll, Is.GreaterThan(1f), "into the turn");
            Assert.That(f.pose.swayYaw, Is.EqualTo(-def.swayYawMax).Within(0.2f), "the arms lag a turn to the right, clamped");
            Assert.That(Math.Abs(f.pose.swayMove), Is.LessThanOrEqualTo(def.swayMoveMax + 1e-5f));
            var g = new BodyFeel(def);
            var s = Input(MoveMode.Grounded, 6f);
            s.velocityBody = new Vector3(6f, 0f, 0f);
            Run(g, s, 60);
            Assert.That(Math.Abs(g.pose.armRoll), Is.EqualTo(def.strafeRoll).Within(0.3f), "strafing rolls the arms about 4 degrees");
        }

        [Test]
        public void MotionEffectsZeroTurnsTheEffectsOffButKeepsContact()
        {
            var def = new BodyFeelDef();
            var f = new BodyFeel(def);
            var i = Input(MoveMode.Sprint, 16f, 0.3f, 0f);
            i.lookYaw = 4f;
            i.velocityBody = new Vector3(3f, 0f, 15f);
            i.events.sprintKick = true;
            Run(f, i, 120);
            Assert.That(f.pose.torsoPitch, Is.EqualTo(0f));
            Assert.That(f.pose.torsoRoll, Is.EqualTo(0f));
            Assert.That(f.pose.swayYaw, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(f.pose.armRoll, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(f.pose.pump, Is.EqualTo(0f));
            Assert.That(f.pose.armDipOffset, Is.EqualTo(0f));
            Assert.That(f.pose.lanyardRoll, Is.EqualTo(0f).Within(1e-5f));
            for (int k = 0; k < 2; k++)
                Assert.That(Vector3.Distance(f.pose.hand[k], Rest(def, k == 0 ? -1f : 1f)), Is.LessThan(1e-3f), "hand " + k + " at rest");
            // Contact stays: the reach and the plant.
            var a = Input(MoveMode.Air, 9f, 0f, 0f);
            a.approaching = true;
            a.approachSide = -1;
            a.approachCloseness = 1f;
            a.approachTarget = new Vector3(-0.5f, -0.06f, 0.34f);
            Run(f, a, 20);
            Assert.That(f.pose.hand[0].X, Is.LessThan(-0.45f), "the lean-in is a contact cue");
            var w = Input(MoveMode.Wallrun, 9f, 0f, 0f);
            w.wallSide = -1;
            w.hasWallTarget = true;
            w.wallHandTarget = new Vector3(-0.5f, -0.06f, 0.34f);
            w.events.wallrunStarted = true;
            Run(f, w, 1);
            Assert.That(Vector3.Distance(f.pose.hand[0], w.wallHandTarget), Is.LessThan(1e-5f), "the plant is a contact cue");
            // And the slide cant and legs.
            Run(f, Input(MoveMode.Slide, 12f, 0f, 0f), 40);
            Assert.That(f.pose.legs, Is.EqualTo(1f));
            Assert.That(f.pose.armRoll, Is.EqualTo(def.slideCant).Within(0.5f));
        }
    
        [Test]
        public void AGestureLiftsTheHandTowardsItsPointAndYieldsToTheWallHand()
        {
            var def = new BodyFeelDef();
            var f = new BodyFeel(def);
            var i = Input(MoveMode.Sprint, 9f, 0.5f);
            Run(f, i, 30);
            var point = new Vector3(0.6f, 0.4f, 6f); // up ahead, to the right
            Vector3 start = f.pose.hand[1];
            i.gestureTarget = point;
            f.Gesture(1, 0.2f);
            Run(f, i, 4); // 0.067 s: the quick raise
            Assert.That(f.pose.gesture[1], Is.GreaterThan(0.9f));
            Assert.That(f.pose.hand[1].Y, Is.GreaterThan(start.Y + 0.05f), "the hand lifts");
            Vector3 dir = Vector3.Normalize(point - start);
            Assert.That(Vector3.Dot(f.pose.hand[1] - start, dir), Is.GreaterThan(0.06f), "towards the point");
            Assert.That(f.pose.gesture[0], Is.EqualTo(0f), "only that hand");
            Run(f, i, 20); // past 0.2 s: back to the run
            Assert.That(f.pose.gesture[1], Is.EqualTo(0f));
            Run(f, i, 30);
            Assert.That(Vector3.Distance(f.pose.hand[1], start), Is.LessThan(0.06f), "blended back");

            // On the wall the planted hand wins.
            var w = Input(MoveMode.Wallrun, 8.6f);
            w.wallSide = 1;
            w.hasWallTarget = true;
            w.wallHandTarget = new Vector3(0.5f, -0.06f, 0.34f);
            w.events.wallrunStarted = true;
            w.gestureTarget = point;
            Run(f, w, 2);
            f.Gesture(1, 0.2f);
            Run(f, w, 4);
            Assert.That(f.pose.gesture[1], Is.EqualTo(0f));
            Assert.That(Vector3.Distance(f.pose.hand[1], w.wallHandTarget), Is.LessThan(1e-5f), "the wall hand stays planted");
        }
    }
}

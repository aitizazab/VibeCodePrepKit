using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    public class GunFeelTests
    {
        static readonly AimAssistDef Def = new AimAssistDef
        {
            slowdownConeDeg = 6f, slowdownScale = 0.55f, magnetConeDeg = 3f, magnetDegPerSec = 8f, range = 40f,
        };

        static List<AimTarget> One(Vector3 c, float r = 0.5f) => new List<AimTarget> { new AimTarget(c, r) };

        static Vector3 AtYaw(float deg, float dist) =>
            new Vector3(MathF.Sin(deg * MathF.PI / 180f) * dist, 0f, MathF.Cos(deg * MathF.PI / 180f) * dist);

        [Test]
        public void NoTargetsMeansNoAssist()
        {
            var r = AimAssist.Compute(Vector3.Zero, 0f, 0f, new List<AimTarget>(), true, 0.016f, Def);
            Assert.That(r.turnScale, Is.EqualTo(1f));
            Assert.That(r.yawNudge, Is.EqualTo(0f));
            Assert.That(r.target, Is.EqualTo(-1));
        }

        [Test]
        public void ATargetOnTheCrosshairSlowsTheStick()
        {
            var r = AimAssist.Compute(Vector3.Zero, 0f, 0f, One(new Vector3(0f, 0f, 10f)), false, 0.016f, Def);
            Assert.That(r.turnScale, Is.EqualTo(0.55f));
            Assert.That(r.target, Is.EqualTo(0));
        }

        [Test]
        public void ATargetOutsideTheConeOrOutOfRangeIsIgnored()
        {
            Assert.That(AimAssist.Compute(Vector3.Zero, 0f, 0f, One(AtYaw(30f, 10f)), true, 0.016f, Def).turnScale, Is.EqualTo(1f));
            Assert.That(AimAssist.Compute(Vector3.Zero, 0f, 0f, One(new Vector3(0f, 0f, 100f)), true, 0.016f, Def).target, Is.EqualTo(-1));
        }

        [Test]
        public void TheMagnetPullsTowardTheCentreButNeverPastIt()
        {
            var t = One(AtYaw(2f, 10f), 0f);
            var whole = AimAssist.Compute(Vector3.Zero, 0f, 0f, t, true, 1f, Def);
            Assert.That(whole.yawNudge, Is.EqualTo(2f).Within(1e-3f), "8 deg/s for 1 s, but the centre is only 2 deg away");
            var part = AimAssist.Compute(Vector3.Zero, 0f, 0f, t, true, 0.1f, Def);
            Assert.That(part.yawNudge, Is.EqualTo(0.8f).Within(1e-3f));
            Assert.That(part.pitchNudge, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void ATargetBelowPullsThePitchDown()
        {
            var r = AimAssist.Compute(Vector3.Zero, 0f, 0f, One(new Vector3(0f, -0.3f, 10f), 0f), true, 0.1f, Def);
            Assert.That(r.pitchNudge, Is.GreaterThan(0f), "positive pitch looks down");
        }

        [Test]
        public void NoPullWhenThePlayerIsStill()
        {
            var r = AimAssist.Compute(Vector3.Zero, 0f, 0f, One(AtYaw(2f, 10f), 0f), false, 1f, Def);
            Assert.That(r.yawNudge, Is.EqualTo(0f));
            Assert.That(r.turnScale, Is.EqualTo(0.55f), "the slowdown still applies");
        }

        [Test]
        public void TheClosestTargetInAngleWins()
        {
            var targets = new List<AimTarget> { new AimTarget(AtYaw(5f, 10f), 0f), new AimTarget(AtYaw(-1f, 30f), 0f) };
            Assert.That(AimAssist.Compute(Vector3.Zero, 0f, 0f, targets, true, 0.016f, Def).target, Is.EqualTo(1));
        }

        [Test]
        public void TargetsBehindOrOnTheEyeAreNeverAssisted()
        {
            Assert.That(AimAssist.Compute(Vector3.Zero, 0f, 0f, One(new Vector3(0f, 0f, -10f), 20f), true, 0.016f, Def).target, Is.EqualTo(-1));
            var onEye = AimAssist.Compute(Vector3.Zero, 0f, 0f, One(Vector3.Zero), true, 0.016f, Def);
            Assert.That(onEye.target, Is.EqualTo(-1));
            Assert.That(float.IsNaN(onEye.yawNudge), Is.False);
        }

        [Test]
        public void DeltaAngleWrapsTheShortWay()
        {
            Assert.That(AimAssist.DeltaAngle(350f, 10f), Is.EqualTo(20f).Within(1e-4f));
            Assert.That(AimAssist.DeltaAngle(10f, 350f), Is.EqualTo(-20f).Within(1e-4f));
            Assert.That(AimAssist.DeltaAngle(0f, 180f), Is.EqualTo(180f).Within(1e-4f));
        }

        [Test]
        public void TheKickRaisesTheViewAndRecoversByItself()
        {
            var d = new CameraKickDef { pitchDeg = 2f, yawDeg = 0.5f, recoverRate = 10f };
            var k = new CameraKick();
            k.Kick(d, 1f);
            Assert.That(k.Pitch, Is.EqualTo(-2f), "negative pitch is up");
            Assert.That(k.Yaw, Is.EqualTo(0.5f));
            for (int i = 0; i < 18; i++) k.Tick(1f / 60f);    // 0.3 s = 3 / rate
            Assert.That(MathF.Abs(k.Pitch), Is.LessThan(2f * 0.06f));
        }

        [Test]
        public void TheRecoveryIsTheSameAtAnyFrameRate()
        {
            var d = new CameraKickDef();
            var a = new CameraKick();
            var b = new CameraKick();
            a.Kick(d, -1f);
            b.Kick(d, -1f);
            for (int i = 0; i < 60; i++) a.Tick(1f / 60f);
            b.Tick(1f);
            Assert.That(a.Pitch, Is.EqualTo(b.Pitch).Within(1e-4f));
            Assert.That(a.Yaw, Is.EqualTo(b.Yaw).Within(1e-4f));
        }

        [Test]
        public void ABrokenSideGivesNoSidewaysKick()
        {
            var k = new CameraKick();
            k.Kick(new CameraKickDef(), float.NaN);
            Assert.That(k.Yaw, Is.EqualTo(0f));
            Assert.That(k.Pitch, Is.LessThan(0f));
        }
    }
}

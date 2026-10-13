using System;
using System.Numerics;
using JamKit.Movement;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    public class DecisionTests
    {
        static DecisionEvent At(float t) => new DecisionEvent { time = t, kind = DecisionKind.Jump, subject = -1, ammo = -1 };

        [Test]
        public void KeepsTheNewestWhenFull()
        {
            var log = new DecisionLog(4);
            for (int i = 0; i < 6; i++) log.Add(At(i));
            Assert.That(log.Count, Is.EqualTo(4));
            Assert.That(log.Total, Is.EqualTo(6));
            Assert.That(log[0].time, Is.EqualTo(2f));
            Assert.That(log[3].time, Is.EqualTo(5f));
        }

        [Test]
        public void RejectsANonPositiveCapacity()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DecisionLog(0));
        }

        [Test]
        public void IndexOutsideTheLogThrows()
        {
            var log = new DecisionLog(4);
            log.Add(At(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => { var _ = log[1]; });
        }

        [Test]
        public void JumpAndWallrunBecomeDecisions()
        {
            var log = new DecisionLog(16);
            var e = new MoveEvents { jumped = true, wallrunStarted = true, wallSide = -1 };
            int n = MoveDecisions.Append(log, e, 3.5f, new Vector3(1f, 0f, 2f), DecisionContext.InCombat, 90f);
            Assert.That(n, Is.EqualTo(2));
            Assert.That(log[0].kind, Is.EqualTo(DecisionKind.Jump));
            Assert.That(log[1].kind, Is.EqualTo(DecisionKind.WallrunStart));
            Assert.That(log[1].side, Is.EqualTo(-1));
            Assert.That(log[1].time, Is.EqualTo(3.5f));
            Assert.That(log[1].threatBearing, Is.EqualTo(90f));
            Assert.That(log[1].subject, Is.EqualTo(-1));
            Assert.That(log[1].ammo, Is.EqualTo(-1));
        }

        [Test]
        public void LandingIsNotADecision()
        {
            var log = new DecisionLog(16);
            var e = new MoveEvents { landed = true, landImpact = 8f, leftGround = true };
            Assert.That(MoveDecisions.Append(log, e, 0f, Vector3.Zero, DecisionContext.None, float.NaN), Is.EqualTo(0));
            Assert.That(log.Count, Is.EqualTo(0));
        }
    }
}

using System.Numerics;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    public class AimTests
    {
        [Test]
        public void AStillTargetIsShotWhereItStands()
        {
            var p = Aim.LeadPoint(Vector3.Zero, new Vector3(10f, 0f, 0f), Vector3.Zero, 30f, 1f);
            Assert.That(Vector3.Distance(p, new Vector3(10f, 0f, 0f)), Is.LessThan(1e-4f));
        }

        [Test]
        public void AFullLeadMeetsTheTarget()
        {
            var shooter = Vector3.Zero;
            var target = new Vector3(0f, 0f, 20f);
            var vel = new Vector3(8f, 0f, 0f);
            const float speed = 30f;
            var p = Aim.LeadPoint(shooter, target, vel, speed, 1f);
            float t = Vector3.Distance(shooter, p) / speed;          // when the shot arrives at p
            var targetThen = target + vel * t;                        // where the target is then
            Assert.That(Vector3.Distance(p, targetThen), Is.LessThan(0.01f));
        }

        [Test]
        public void NoLeadMeansAtTheTargetNow()
        {
            var target = new Vector3(0f, 0f, 20f);
            var p = Aim.LeadPoint(Vector3.Zero, target, new Vector3(8f, 0f, 0f), 30f, 0f);
            Assert.That(Vector3.Distance(p, target), Is.LessThan(1e-4f));
        }

        [Test]
        public void NoInterceptFallsBackToTheTarget()
        {
            // the target flees faster than the shot: no solution
            var target = new Vector3(0f, 0f, 20f);
            var p = Aim.LeadPoint(Vector3.Zero, target, new Vector3(0f, 0f, 50f), 30f, 1f);
            Assert.That(Vector3.Distance(p, target), Is.LessThan(1e-4f));
        }

        [Test]
        public void ATargetOnTheShooterGivesNoNaN()
        {
            var p = Aim.LeadPoint(Vector3.Zero, Vector3.Zero, new Vector3(5f, 0f, 0f), 30f, 1f);
            Assert.That(float.IsNaN(p.X) || float.IsNaN(p.Y) || float.IsNaN(p.Z), Is.False);
        }

        [Test]
        public void LeadingAndWhereYouAreNeverShareAnEncounter()
        {
            Assert.That(AimRules.CanShareEncounter(AimModel.LeadsYou, AimModel.FiresAtWhereYouAre), Is.False);
            Assert.That(AimRules.CanShareEncounter(AimModel.LeadsYou, AimModel.Area, AimModel.Melee), Is.True);
            Assert.That(AimRules.CanShareEncounter(AimModel.FiresAtWhereYouAre, AimModel.Area), Is.True);
        }

        static bool RunShot(TelegraphedShot s, System.Func<float, Vector3> targetAt)
        {
            const float h = 1f / 120f;
            float t = 0f;
            s.Start(targetAt(0f));
            while (true)
            {
                t += h;
                if (s.Step(h, targetAt(t), out bool hit)) return hit;
                if (t > 5f) Assert.Fail("the shot never fired");
            }
        }

        [Test]
        public void TheSpotlightHitsAStillTarget()
        {
            Assert.That(RunShot(new TelegraphedShot(), t => new Vector3(0f, 0f, 20f)), Is.True);
        }

        [Test]
        public void ChangingDirectionAfterTheLockDodgesTheSpotlight()
        {
            // still until 0.6 s (after the 0.55 s lock), then sidestep at 7 m/s
            Assert.That(RunShot(new TelegraphedShot(), t => new Vector3(t < 0.6f ? 0f : (t - 0.6f) * 7f, 0f, 20f)), Is.False);
        }

        [Test]
        public void TheSpotlightLocksBeforeItFires()
        {
            var s = new TelegraphedShot();
            s.Start(Vector3.Zero);
            s.Step(0.5f, Vector3.Zero, out _);
            Assert.That(s.Locked, Is.False);
            s.Step(0.1f, Vector3.Zero, out _);
            Assert.That(s.Locked, Is.True);
            Assert.That(s.Active, Is.True);
        }
    }
}

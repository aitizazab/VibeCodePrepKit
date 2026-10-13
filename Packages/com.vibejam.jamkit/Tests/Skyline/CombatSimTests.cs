using System.Collections.Generic;
using System.Numerics;
using JamKit.Movement;
using JamKit.Skyline;
using JamKit.Tests.Movement;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    public class CombatSimTests
    {
        static BoxWorld Arena()
        {
            var w = new BoxWorld();
            w.Floor(-25f, 25f, -25f, 25f, 0f);
            return w;
        }

        static List<Shooter> OneShooter(AimModel aim, float lead) => new List<Shooter>
        {
            new Shooter { position = new Vector3(20f, 1.5f, 0f), aim = aim, projectileSpeed = 30f, fireInterval = 1.2f, leadFactor = lead },
        };

        static CombatSim Sim(IPlayerBot bot, List<Shooter> shooters)
        {
            var sim = new MoveSim(Arena(), new MoveDef());
            sim.Teleport(Vector3.Zero, 0f, Vector3.Zero);
            return new CombatSim(sim, bot, shooters);
        }

        [Test]
        public void AStandingTargetIsHitByNearlyEveryShot()
        {
            var c = Sim(new StandStillBot(), OneShooter(AimModel.FiresAtWhereYouAre, 0f));
            c.Run(30f);
            Assert.That(c.ShotsFired, Is.GreaterThan(20));
            Assert.That(c.Hits, Is.GreaterThanOrEqualTo(c.ShotsFired - 2));   // the last shots may still be in the air
        }

        [Test]
        public void LeadingCatchesAStraightLineRunnerMoreThanNotLeading()
        {
            var lead = Sim(new StraightLineBot(), OneShooter(AimModel.LeadsYou, 1f));
            var none = Sim(new StraightLineBot(), OneShooter(AimModel.LeadsYou, 0f));
            lead.Run(60f);
            none.Run(60f);
            Assert.That(lead.Hits, Is.GreaterThan(none.Hits));
        }

        [Test]
        public void TheSpotlightFiresTelegraphedHitscan()
        {
            var shooters = new List<Shooter>
            {
                new Shooter { position = new Vector3(20f, 6f, 0f), aim = AimModel.FiresAtWhereYouAre, projectileSpeed = 0f, fireInterval = 2f },
            };
            var c = Sim(new StandStillBot(), shooters);
            c.Run(10f);
            Assert.That(c.ShotsFired, Is.GreaterThanOrEqualTo(4));
            Assert.That(c.Hits, Is.EqualTo(c.ShotsFired));
        }

        [Test]
        public void TheDodgeReportIsDeterministicAndPrintsItsNumbers()
        {
            var a = DodgeReport.Run(Arena, () => OneShooter(AimModel.LeadsYou, 0.9f), 60f, 7UL);
            var b = DodgeReport.Run(Arena, () => OneShooter(AimModel.LeadsYou, 0.9f), 60f, 7UL);
            Assert.That(a.straightHits, Is.EqualTo(b.straightHits));
            Assert.That(a.jinkHits, Is.EqualTo(b.jinkHits));
            TestContext.WriteLine($"Dodge report (one leading drone, lead 0.9, 60 s): straight-line bot {a.straightHits} hits, jinking bot {a.jinkHitsMean:0.0} hits (averaged over {a.seeds} seeds), ratio {a.Ratio:0.00} (M1 target: about 2, neither 0)");
            Assert.That(a.shots, Is.GreaterThan(0));
        }
    }
}

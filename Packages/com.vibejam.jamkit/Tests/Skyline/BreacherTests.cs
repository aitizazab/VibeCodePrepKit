using System;
using System.Numerics;
using JamKit.Movement;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    public class BreacherTests
    {
        const float G = 24f;
        static readonly Vector3 Down = new Vector3(0f, -1f, 0f);

        static Vector3 Push(Vector3 aim, Vector3 vel, bool braced, BoostCharge c, out bool boosted) =>
            BreacherKnockback.SelfPush(aim, vel, braced, G, c, new BreacherBoostDef(), out boosted);

        static Vector3 Request(Vector3 aim, Vector3 vel, bool braced, BoostCharge c, out bool spendsCharge) =>
            BreacherKnockback.RequestSelfPush(aim, vel, braced, G, c, new BreacherBoostDef(), out spendsCharge);

        [Test]
        public void AStraightDownShotFromRestLiftsTheBoostHeight()
        {
            var p = Push(Down, Vector3.Zero, false, new BoostCharge(), out bool boosted);
            Assert.That(boosted, Is.True);
            Assert.That(p.X, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(p.Z, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(p.Y * p.Y / (2f * G), Is.EqualTo(new BreacherBoostDef().boostHeight).Within(1e-4f), "rise = v^2 / 2g");
        }

        [Test]
        public void ItCancelsAFallButNeverAddsToAFasterRise()
        {
            float up = MathF.Sqrt(2f * G * 1f);
            Assert.That(Push(Down, new Vector3(0f, -5f, 0f), false, new BoostCharge(), out _).Y, Is.EqualTo(up + 5f).Within(1e-4f));
            var rising = Push(Down, new Vector3(0f, 9f, 0f), false, new BoostCharge(), out bool spent);
            Assert.That(rising.Y, Is.EqualTo(0f));
            Assert.That(spent, Is.True, "an air shot spends the boost even when it adds no lift");
        }

        static readonly BreacherBoostDef OnePerAirtimeDef = new BreacherBoostDef { oneBoostPerAirtime = true };

        static Vector3 PushOne(Vector3 aim, Vector3 vel, bool braced, BoostCharge c, out bool boosted) =>
            BreacherKnockback.SelfPush(aim, vel, braced, G, c, OnePerAirtimeDef, out boosted);

        static Vector3 RequestOne(Vector3 aim, Vector3 vel, bool braced, BoostCharge c, out bool spendsCharge) =>
            BreacherKnockback.RequestSelfPush(aim, vel, braced, G, c, OnePerAirtimeDef, out spendsCharge);

        [Test]
        public void OneBoostPerAirtimeRefreshedByLandingOrAWall()
        {
            var c = new BoostCharge();
            Assert.That(PushOne(Down, Vector3.Zero, false, c, out _).Y, Is.GreaterThan(0f));
            Assert.That(PushOne(Down, Vector3.Zero, false, c, out bool again), Is.EqualTo(Vector3.Zero));
            Assert.That(again, Is.False);
            c.Observe(false, false);
            Assert.That(c.Ready, Is.False, "still airborne");
            c.Observe(false, true);
            Assert.That(c.Ready, Is.True, "a wall refreshes it");
            PushOne(Down, Vector3.Zero, false, c, out _);
            c.Observe(true, false);
            Assert.That(c.Ready, Is.True, "landing refreshes it");
        }

        [Test]
        public void AFlatShotOnTheGroundShovesBackWithoutSpendingTheBoost()
        {
            var c = new BoostCharge();
            var d = new BreacherBoostDef();
            var p = Push(new Vector3(0f, 0f, 1f), Vector3.Zero, true, c, out bool boosted);
            Assert.That(p.Z, Is.EqualTo(-d.selfPushSpeed * d.bracedScale).Within(1e-5f), "braced: mostly eaten by the floor");
            Assert.That(p.Y, Is.EqualTo(0f));
            Assert.That(boosted, Is.False);
            Assert.That(c.Ready, Is.True);
        }

        [Test]
        public void AShotPointingUpOrBarelyDownNeverLifts()
        {
            Assert.That(Push(new Vector3(0f, 0.8f, 0.6f), Vector3.Zero, false, new BoostCharge(), out _).Y, Is.EqualTo(0f));
            Assert.That(Push(Vector3.Normalize(new Vector3(0f, -0.2f, 1f)), Vector3.Zero, false, new BoostCharge(), out _).Y, Is.EqualTo(0f));
        }

        [Test]
        public void AnAngledShotLiftsLessAndPushesBack()
        {
            var aim = Vector3.Normalize(new Vector3(0f, -1f, 1f));
            var p = Push(aim, Vector3.Zero, false, new BoostCharge(), out _);
            Assert.That(p.Y, Is.EqualTo(MathF.Sqrt(2f * G) * 0.7071f).Within(1e-3f));
            Assert.That(p.Z, Is.LessThan(0f));
        }

        [Test]
        public void ABrokenAimGivesNoPush()
        {
            var c = new BoostCharge();
            Assert.That(Push(Vector3.Zero, Vector3.Zero, false, c, out _), Is.EqualTo(Vector3.Zero));
            Assert.That(Push(new Vector3(float.NaN, 0f, 0f), Vector3.Zero, false, c, out _), Is.EqualTo(Vector3.Zero));
            Assert.That(c.Ready, Is.True, "nothing was spent");
        }

        [Test]
        public void TheLimitsAreTheSpecsDefaults()
        {
            var d = new BreacherBoostDef();
            Assert.That(d.oneBoostPerAirtime, Is.False, "the creator's call (2026-10-13): limited only by the fire rate");
            Assert.That(d.selfDamage, Is.EqualTo(0f));
            Assert.That(d.bracedScale, Is.EqualTo(0.2f), "the creator's call (2026-10-13 evening): braced shots mostly eaten");
            Assert.That(MathF.Abs(d.boostHeight - new MoveDef().doubleJumpHeight), Is.LessThanOrEqualTo(0.25f),
                "about the jump upgrade's height (a third jump: the double jump's)");
        }

        // The creator's call (2026-10-13): "The Breacher's boost should just be limited by its fire rate."

        [Test]
        public void ByDefaultTwoAirShotsInARowBothLift()
        {
            var c = new BoostCharge();
            var first = Push(Down, new Vector3(0f, -3f, 0f), false, c, out bool boosted1);
            Assert.That(first.Y, Is.GreaterThan(0f));
            Assert.That(boosted1, Is.True);
            c.Observe(false, false);
            var second = Push(Down, new Vector3(0f, -3f, 0f), false, c, out bool boosted2);
            Assert.That(second.Y, Is.GreaterThan(0f), "still airborne, no wall: the second shot lifts too");
            Assert.That(boosted2, Is.True);
        }

        [Test]
        public void EachAirShotSetsTheRiseSpeedAndNeverAddsToIt()
        {
            float up = MathF.Sqrt(2f * G * new BreacherBoostDef().boostHeight);
            var c = new BoostCharge();
            var first = Push(Down, Vector3.Zero, false, c, out _);
            Assert.That(first.Y, Is.EqualTo(up).Within(1e-4f));
            // Now rising at exactly the boost speed: a second shot adds nothing; slower than it adds only the difference.
            Assert.That(Push(Down, new Vector3(0f, first.Y, 0f), false, c, out bool spent).Y, Is.EqualTo(0f), "already rising as fast as the boost");
            Assert.That(spent, Is.True, "it still counts as an air shot");
            Assert.That(Push(Down, new Vector3(0f, up + 4f, 0f), false, c, out _).Y, Is.EqualTo(0f), "rising faster than the boost: 0 lift");
            Assert.That(Push(Down, new Vector3(0f, up - 2f, 0f), false, c, out _).Y, Is.EqualTo(2f).Within(1e-4f), "a slower rise is topped up to the boost speed");
        }

        [Test]
        public void ByDefaultTheChargeNeverBlocksAShot()
        {
            var c = new BoostCharge();
            c.Spend();
            Assert.That(c.Ready, Is.False);
            var p = Push(Down, Vector3.Zero, false, c, out bool boosted);
            Assert.That(p.Y, Is.GreaterThan(0f), "a spent charge doesn't stop the lift when the rule is off");
            Assert.That(boosted, Is.True);
            var flat = Push(new Vector3(0f, 0f, 1f), Vector3.Zero, false, c, out _);
            Assert.That(flat.Z, Is.EqualTo(-new BreacherBoostDef().selfPushSpeed).Within(1e-5f), "and the flat shove still happens");
        }

        [Test]
        public void WithTheRuleOnASpentChargeStillBlocksTheAirShot()
        {
            var c = new BoostCharge();
            c.Spend();
            Assert.That(PushOne(Down, Vector3.Zero, false, c, out bool boosted), Is.EqualTo(Vector3.Zero));
            Assert.That(boosted, Is.False);
        }

        // The engine's AddPush can refuse a push (a mantle, ignored states), so the charge is spent only on acceptance.

        [Test]
        public void ARequestAloneSpendsNothing()
        {
            var c = new BoostCharge();
            var p = Request(Down, Vector3.Zero, false, c, out bool spends);
            Assert.That(p.Y, Is.GreaterThan(0f));
            Assert.That(spends, Is.True, "the push would spend the boost");
            Assert.That(c.Ready, Is.True, "but nothing is spent until the engine accepts it");
        }

        [Test]
        public void ARefusedPushLeavesTheChargeReadyToTryAgain()
        {
            var c = new BoostCharge();
            Request(Down, Vector3.Zero, false, c, out bool spends);
            c.Commit(spends, accepted: false);
            Assert.That(c.Ready, Is.True, "the engine dropped it (e.g. mid-mantle): not spent");
            var retry = Request(Down, Vector3.Zero, false, c, out bool spendsAgain);
            Assert.That(retry.Y, Is.GreaterThan(0f), "the next shot still has its boost");
            Assert.That(spendsAgain, Is.True);
        }

        [Test]
        public void AnAcceptedPushSpendsTheChargeAndTheNextAirShotGivesNothing()
        {
            var c = new BoostCharge();
            Request(Down, Vector3.Zero, false, c, out bool spends);
            c.Commit(spends, accepted: true);
            Assert.That(c.Ready, Is.False);
            var next = RequestOne(Down, Vector3.Zero, false, c, out bool spendsNext);
            Assert.That(next, Is.EqualTo(Vector3.Zero));
            Assert.That(spendsNext, Is.False);
        }

        [Test]
        public void AnAcceptedPushThatSpendsNothingLeavesTheChargeAlone()
        {
            var c = new BoostCharge();
            var p = Request(new Vector3(0f, 0f, 1f), Vector3.Zero, true, c, out bool spends);
            Assert.That(p.Z, Is.LessThan(0f));
            Assert.That(spends, Is.False, "a flat shot on the ground never spends it");
            c.Commit(spends, accepted: true);
            Assert.That(c.Ready, Is.True);
        }

        // The creator's call (2026-10-13 evening): "a Breacher shot during a wall-run should just be mostly eaten up by the
        // wall, the same when standing or crouched on the ground. The impulse should only be felt when shot mid-air."

        static readonly Vector3[] Aims =
        {
            new Vector3(0f, -1f, 0f), Vector3.Normalize(new Vector3(0f, -1f, 1f)), Vector3.Normalize(new Vector3(0.3f, -0.8f, 0.5f)),
            new Vector3(0f, 0f, 1f), Vector3.Normalize(new Vector3(-1f, -0.2f, 0f)), Vector3.Normalize(new Vector3(0f, 0.8f, 0.6f)),
        };

        [Test]
        public void ABracedShotKeepsAtMostTheBracedShareOfTheFlatPushAndNeverLifts()
        {
            var d = new BreacherBoostDef();
            foreach (var aim in Aims)
            foreach (var vel in new[] { Vector3.Zero, new Vector3(0f, -5f, 0f), new Vector3(2f, 0f, 7f) })
            {
                var c = new BoostCharge();
                var p = Request(aim, vel, true, c, out bool spends);
                Assert.That(p.Y, Is.EqualTo(0f), "no lift when braced");
                Assert.That(p.Length(), Is.LessThanOrEqualTo(d.bracedScale * d.selfPushSpeed + 1e-5f));
                var back = -aim;
                var flat = new Vector3(back.X, 0f, back.Z) * d.selfPushSpeed * d.bracedScale;
                Assert.That(Vector3.Distance(p, flat), Is.LessThan(1e-5f), "the flat push, scaled");
                Assert.That(spends, Is.False, "a braced shot never spends the charge");
            }
        }

        [Test]
        public void ABracedShotWorksTheSameWithTheOneBoostRuleAndASpentCharge()
        {
            var d = OnePerAirtimeDef;
            var c = new BoostCharge();
            c.Spend();
            var p = PushOne(new Vector3(0f, 0f, 1f), Vector3.Zero, true, c, out bool boosted);
            Assert.That(p.Z, Is.EqualTo(-d.selfPushSpeed * d.bracedScale).Within(1e-5f), "the small shove isn't blocked by the charge");
            Assert.That(boosted, Is.False);
            Assert.That(PushOne(Down, Vector3.Zero, true, new BoostCharge(), out bool spent).Y, Is.EqualTo(0f));
            Assert.That(spent, Is.False, "a braced straight-down shot doesn't spend a ready charge");
        }

        [Test]
        public void AMidAirShotIsUnchanged()
        {
            var d = new BreacherBoostDef();
            float up = MathF.Sqrt(2f * G * d.boostHeight);
            foreach (var aim in Aims)
            foreach (var vel in new[] { Vector3.Zero, new Vector3(0f, -5f, 0f), new Vector3(2f, 3f, 7f) })
            {
                var p = Request(aim, vel, false, new BoostCharge(), out bool spends);
                var back = -aim;
                var expect = new Vector3(back.X, 0f, back.Z) * d.selfPushSpeed;
                if (back.Y > d.minLift) expect.Y = MathF.Max(0f, up * back.Y - vel.Y);
                Assert.That(Vector3.Distance(p, expect), Is.LessThan(1e-4f), "the full flat push, and the lift");
                Assert.That(spends, Is.True, "an air shot uses the boost");
            }
        }

        [Test]
        public void RequestAndSelfPushAgreeOnTheVelocityChange()
        {
            var aim = Vector3.Normalize(new Vector3(0.3f, -0.8f, 0.5f));
            var vel = new Vector3(1f, -2f, 3f);
            var a = Request(aim, vel, false, new BoostCharge(), out bool spends);
            var b = Push(aim, vel, false, new BoostCharge(), out bool boosted);
            Assert.That(a, Is.EqualTo(b));
            Assert.That(spends, Is.EqualTo(boosted));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    public class BotBrainTests
    {
        const float H = 1f / 60f;

        /// <summary>A fixed clock for the timing tests, so Task 11's retuning of the shipped bots can't move them.</summary>
        static EnemyDef Clock() => new EnemyDef
        {
            id = "clock", alertSeconds = 0.6f, telegraphSeconds = 0.35f, fireInterval = 1.2f, loseSightSeconds = 3f,
        };

        static int Attacks(BotBrain b, float seconds, float dt, bool sees = true)
        {
            var cues = new List<BotCue>();
            int steps = (int)Math.Round(seconds / dt);
            for (int i = 0; i < steps; i++) b.Tick(dt, sees, cues);
            return cues.FindAll(c => c.kind == BotCueKind.Attack).Count;
        }

        [Test]
        public void AnIdleBotWakesWhenItSeesYou()
        {
            var b = new BotBrain(SkylineEnemies.MallCopDrone(), 1);
            var cues = new List<BotCue>();
            b.Tick(H, false, cues);
            Assert.That(b.State, Is.EqualTo(BotState.Idle));
            Assert.That(b.Face, Is.EqualTo(BotFace.Happy));
            Assert.That(cues.Count, Is.EqualTo(0));
            b.Tick(H, true, cues);
            Assert.That(b.State, Is.EqualTo(BotState.Alert));
            Assert.That(b.Face, Is.EqualTo(BotFace.Alert));
            Assert.That(cues.Exists(c => c.kind == BotCueKind.Bark && c.bark == "spotted"), Is.True);
        }

        [Test]
        public void ItAttacksOncePerCycleAlwaysAfterItsTelegraph()
        {
            var d = Clock();
            var b = new BotBrain(d, 1);
            var cues = new List<BotCue>();
            var attacks = new List<float>();
            float t = 0f, telegraphAt = -1f;
            for (int i = 0; i < 600; i++)
            {
                t += H;
                int before = cues.Count;
                b.Tick(H, true, cues);
                for (int k = before; k < cues.Count; k++)
                {
                    if (cues[k].kind == BotCueKind.State && cues[k].state == BotState.Telegraph) telegraphAt = t;
                    if (cues[k].kind == BotCueKind.Attack)
                    {
                        Assert.That(t - telegraphAt, Is.EqualTo(d.telegraphSeconds).Within(H + 1e-4f), "the telegraph always comes first");
                        Assert.That(b.Face, Is.EqualTo(BotFace.Angry));
                        attacks.Add(t);
                    }
                }
            }
            Assert.That(attacks.Count, Is.InRange(7, 8));
            for (int i = 1; i < attacks.Count; i++)
                Assert.That(attacks[i] - attacks[i - 1], Is.EqualTo(d.fireInterval).Within(H + 1e-4f));
        }

        [Test]
        public void TheCycleHoldsAt20And144Fps()
        {
            int slow = Attacks(new BotBrain(Clock(), 3), 30f, 1f / 20f);
            int fast = Attacks(new BotBrain(Clock(), 3), 30f, 1f / 144f);
            Assert.That(Math.Abs(slow - fast), Is.LessThanOrEqualTo(1));
        }

        [Test]
        public void ItGivesUpAfterLosingSightForAWhile()
        {
            var b = new BotBrain(Clock(), 1);
            Attacks(b, 2f, H);
            var cues = new List<BotCue>();
            for (int i = 0; i < 300; i++) b.Tick(H, false, cues);
            Assert.That(b.State, Is.EqualTo(BotState.Idle));
            Assert.That(cues.Exists(c => c.kind == BotCueKind.Bark && c.bark == "lost"), Is.True);
        }

        [Test]
        public void BeingShotWakesAnIdleBotAndFlinchesItsFace()
        {
            var b = new BotBrain(SkylineEnemies.FloorCareUnit(), 1);
            var cues = new List<BotCue>();
            b.OnDamaged(cues);
            Assert.That(b.State, Is.EqualTo(BotState.Alert));
            Assert.That(b.Face, Is.EqualTo(BotFace.Hurt));
            for (int i = 0; i < 20; i++) b.Tick(H, true, cues);
            Assert.That(b.Face, Is.Not.EqualTo(BotFace.Hurt), "the flinch lasts 0.25 s");
        }

        [Test]
        public void AWreckedBotDoesNothing()
        {
            var b = new BotBrain(SkylineEnemies.MallCopDrone(), 1);
            var cues = new List<BotCue>();
            b.OnWrecked(cues);
            Assert.That(b.State, Is.EqualTo(BotState.Wrecked));
            Assert.That(b.Face, Is.EqualTo(BotFace.Wrecked));
            Assert.That(cues.Exists(c => c.kind == BotCueKind.Bark && c.bark == "wrecked"), Is.True);
            int n = cues.Count;
            for (int i = 0; i < 300; i++) b.Tick(H, true, cues);
            b.OnDamaged(cues);
            Assert.That(cues.Count, Is.EqualTo(n));
        }

        [Test]
        public void TwoBotsDontAttackInLockstep()
        {
            float First(ulong seed)
            {
                var b = new BotBrain(SkylineEnemies.MallCopDrone(), seed);
                var cues = new List<BotCue>();
                for (int i = 0; i < 300; i++)
                {
                    b.Tick(H, true, cues);
                    if (cues.Exists(c => c.kind == BotCueKind.Attack)) return i * H;
                }
                return -1f;
            }
            var firsts = new HashSet<float>();
            for (ulong seed = 1; seed <= 5; seed++) firsts.Add(First(seed));
            Assert.That(firsts.Count, Is.GreaterThan(1), "the seeded jitter spreads the first shots");
            Assert.That(firsts.Contains(-1f), Is.False);
        }

        static readonly EnemyDef Floor = SkylineEnemies.FloorCareUnit();

        static Vector3 AtBearing(float deg, float dist) =>
            new Vector3(MathF.Sin(deg * MathF.PI / 180f) * dist, 0f, MathF.Cos(deg * MathF.PI / 180f) * dist);

        [Test]
        public void TheAcidSweepCrossesItsArcAtAnkleHeight()
        {
            var left = AtBearing(-60f, 3f);
            Assert.That(AcidSweep.Touches(Vector3.Zero, 0f, 0f, left, 0f, Floor), Is.True, "the jet starts on the left");
            Assert.That(AcidSweep.Touches(Vector3.Zero, 0f, Floor.sweepSeconds, left, 0f, Floor), Is.False, "and ends on the right");
            Assert.That(AcidSweep.Touches(Vector3.Zero, 0f, Floor.sweepSeconds, AtBearing(60f, 3f), 0f, Floor), Is.True);
        }

        [Test]
        public void JumpingOverItOrStandingOutOfReachIsSafe()
        {
            var left = AtBearing(-60f, 3f);
            Assert.That(AcidSweep.Touches(Vector3.Zero, 0f, 0f, left + new Vector3(0f, 0.5f, 0f), 0f, Floor), Is.False, "guests are advised to jump");
            Assert.That(AcidSweep.Touches(Vector3.Zero, 0f, 0f, AtBearing(-60f, 5f), 0f, Floor), Is.False);
            Assert.That(AcidSweep.Touches(Vector3.Zero, 0f, -0.1f, left, 0f, Floor), Is.False, "not before the sweep");
            for (float t = 0f; t <= Floor.sweepSeconds; t += 0.02f)
                Assert.That(AcidSweep.Touches(Vector3.Zero, 0f, t, AtBearing(180f, 2f), 0f, Floor), Is.False, "never behind it");
        }

        [Test]
        public void TheSweepFollowsTheUnitsFacing()
        {
            var right = AtBearing(90f - 60f, 3f);
            Assert.That(AcidSweep.Touches(Vector3.Zero, 90f, 0f, right, 0f, Floor), Is.True);
        }

        [Test]
        public void ALongHitchDoesNotFireABurstOrShrinkTheWarning()
        {
            var d = Clock();
            var b = new BotBrain(d, 1);
            var cues = new List<BotCue>();
            b.Tick(H, true, cues);   // wakes
            b.Tick(10f, true, cues); // a ten-second load hitch
            Assert.That(cues.FindAll(c => c.kind == BotCueKind.Attack).Count, Is.LessThanOrEqualTo(1));
            Assert.That(b.State, Is.EqualTo(BotState.Telegraph));
            float telegraphFor = 0f;
            int attacks = 0;
            for (int i = 0; i < 30; i++)
            {
                int before = cues.Count;
                if (b.State == BotState.Telegraph) telegraphFor += H;
                b.Tick(H, true, cues);
                for (int k = before; k < cues.Count; k++) if (cues[k].kind == BotCueKind.Attack) attacks++;
            }
            Assert.That(attacks, Is.EqualTo(1), "one attack for the one telegraph");
            Assert.That(telegraphFor + BotBrain.MaxCarrySeconds, Is.GreaterThanOrEqualTo(d.telegraphSeconds - H), "the warning still shows nearly its full length");
            Assert.That(telegraphFor, Is.GreaterThan(d.telegraphSeconds - BotBrain.MaxCarrySeconds - 2 * H));
        }

        [Test]
        public void HitchesAtEveryPointOfTheCycleNeverBurst()
        {
            var d = Clock();
            for (int hitchAt = 0; hitchAt < 150; hitchAt += 7)
            {
                var b = new BotBrain(d, 2);
                var cues = new List<BotCue>();
                for (int i = 0; i < hitchAt; i++) b.Tick(H, true, cues);
                cues.Clear();
                b.Tick(10f, true, cues);
                for (int i = 0; i < 60; i++) b.Tick(H, true, cues);   // one second
                Assert.That(cues.FindAll(c => c.kind == BotCueKind.Attack).Count, Is.LessThanOrEqualTo(1), "hitch at frame " + hitchAt);
            }
        }

        /// <summary>Seconds from the step it sees the player again to its telegraph, after seeing it for 1 s and then not for
        /// <paramref name="unseen"/> seconds (long enough to have gone idle).</summary>
        static float ReSightToTelegraph(EnemyDef d, float unseen, ulong seed)
        {
            var b = new BotBrain(d, seed);
            for (int i = 0; i < 60; i++) b.Tick(H, true, null);
            for (int i = 0; i < (int)Math.Round(unseen / H); i++) b.Tick(H, false, null);
            Assert.That(b.State, Is.EqualTo(BotState.Idle), "it went back to idle out of sight");
            float t = 0f;
            for (int i = 0; i < 600 && b.State != BotState.Telegraph; i++) { b.Tick(H, true, null); t += H; }
            return t;
        }

        [Test]
        public void AReSightWithinTheMemorySkipsTheAlertAndOneAfterItDoesNot()
        {
            var d = Clock();
            d.rememberSeconds = 8f;
            for (ulong s = 1; s <= 20; s++)
            {
                float within = ReSightToTelegraph(d, 5f, s), after = ReSightToTelegraph(d, 9f, s);
                Assert.That(within, Is.LessThanOrEqualTo(BotBrain.RememberJitterSeconds + 2f * H), $"seed {s}: within 8 s it skips the {d.alertSeconds} s alert");
                Assert.That(after, Is.GreaterThanOrEqualTo(d.alertSeconds), $"seed {s}: after 8 s it alerts again");
            }
            // the jitter is real: a group re-sighting together doesn't telegraph in lockstep
            var times = new HashSet<float>();
            for (ulong s = 1; s <= 20; s++) times.Add((float)Math.Round(ReSightToTelegraph(d, 5f, s) / H));
            Assert.That(times.Count, Is.GreaterThan(3));
            // and without a memory nothing changes
            Assert.That(ReSightToTelegraph(Clock(), 5f, 1), Is.GreaterThanOrEqualTo(Clock().alertSeconds));
        }
    }
}

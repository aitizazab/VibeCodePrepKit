using System.Collections.Generic;
using System.Linq;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>Task W1: the wave composition table, the wave clock and the cap (docs/SKYLINE-PLAN-FLOOR1.md).</summary>
    public class WaveClockTests
    {
        const string D = SkylineEnemies.DroneId, F = SkylineEnemies.FloorCareId, G = SkylineEnemies.GondolaId;

        static int Count(WaveDef w, string id) => w.enemies.Count(e => e == id);

        // ---- the table ----

        [TestCase(1, 3, 0, 0)]
        [TestCase(2, 3, 1, 0)]
        [TestCase(3, 2, 2, 0)]
        [TestCase(4, 2, 0, 1)]
        public void TheFirstFourWavesAreTheSpecTable(int n, int drones, int floorCare, int gondolas)
        {
            var w = WavePlans.For(n, false);
            Assert.That(w.number, Is.EqualTo(n));
            Assert.That(w.time, Is.EqualTo((n - 1) * FloorRules.WaveSeconds));
            Assert.That(Count(w, D), Is.EqualTo(drones));
            Assert.That(Count(w, F), Is.EqualTo(floorCare));
            Assert.That(Count(w, G), Is.EqualTo(gondolas));
            Assert.That(w.enemies.Length, Is.EqualTo(drones + floorCare + gondolas));
        }

        [Test]
        public void WaveFourCarriesItsGondolaEvenIfOneIsFlaggedAlive()
        {
            Assert.That(Count(WavePlans.For(4, true), G), Is.EqualTo(1));
        }

        [Test]
        public void WavesFiveToSevenAlternateAandB()
        {
            foreach (int n in new[] { 5, 7, 9, 11, 13 })
            {
                var a = WavePlans.For(n, false);
                Assert.That(Count(a, D), Is.EqualTo(3), "wave " + n);
                Assert.That(Count(a, F), Is.EqualTo(1), "wave " + n);
            }
            foreach (int n in new[] { 6, 10, 14 })
            {
                var b = WavePlans.For(n, false);
                Assert.That(Count(b, D), Is.EqualTo(2), "wave " + n);
                Assert.That(Count(b, F), Is.EqualTo(2), "wave " + n);
            }
        }

        [Test]
        public void AGondolaJoinsEveryFourthWaveFromEightOnlyWhenNoneIsAlive()
        {
            foreach (int n in new[] { 8, 12, 16, 20, 40 })
            {
                Assert.That(Count(WavePlans.For(n, false), G), Is.EqualTo(1), "wave " + n + " no gondola alive");
                Assert.That(Count(WavePlans.For(n, true), G), Is.EqualTo(0), "wave " + n + " gondola alive");
            }
            foreach (int n in new[] { 5, 6, 7, 9, 10, 11, 13, 14, 15 })
                Assert.That(Count(WavePlans.For(n, false), G), Is.EqualTo(0), "wave " + n);
        }

        [Test]
        public void AGondolaWaveDropsTheFloorCareSoOneLayerStaysOpen()
        {
            var w = WavePlans.For(8, false);
            Assert.That(Count(w, F), Is.EqualTo(0));
            Assert.That(WavePlans.OpenLayersCheck(w), Is.Null);
        }

        [Test]
        public void WaveNumbersBelowOneAreRefused()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => WavePlans.For(0, false));
        }

        [Test]
        public void EveryPlanWaveObeysTheAimRuleAndCoversAtMostTwoLayers()
        {
            for (int n = 1; n <= 80; n++)
                foreach (bool alive in new[] { false, true })
                {
                    var w = WavePlans.For(n, alive);
                    var problems = WavePlans.OpenLayersCheck(w);
                    Assert.That(problems, Is.Null, $"wave {n} (gondola alive {alive}): {(problems == null ? "" : string.Join("; ", problems))}");
                    Assert.That(w.enemies.Length, Is.LessThanOrEqualTo(FloorRules.WaveCap), "a wave must fit under the cap");
                    foreach (var id in w.enemies)
                        Assert.That(id, Is.Not.EqualTo(SkylineEnemies.SpotlightId), "no spotlight on this floor");
                }
        }

        [Test]
        public void OpenLayersCheckCatchesBadWaves()
        {
            Assert.That(WavePlans.OpenLayersCheck(null), Is.Not.Null);
            Assert.That(WavePlans.OpenLayersCheck(new WaveDef { number = 1, enemies = new string[0] }), Is.Not.Null);
            Assert.That(WavePlans.OpenLayersCheck(new WaveDef { number = 1, enemies = new[] { "nope" } }), Is.Not.Null);
            // drone (leads you) + spotlight (fires at where you are now): the aim models never mix
            var mixed = WavePlans.OpenLayersCheck(new WaveDef { number = 1, enemies = new[] { D, SkylineEnemies.SpotlightId } });
            Assert.That(mixed, Is.Not.Null);
            Assert.That(mixed.Any(p => p.Contains("lead")), Is.True);
            // drone + floor-care + gondola: air, floor and walls at once
            var three = WavePlans.OpenLayersCheck(new WaveDef { number = 1, enemies = new[] { D, F, G } });
            Assert.That(three, Is.Not.Null);
            Assert.That(three.Any(p => p.Contains("layers")), Is.True);
            Assert.That(WavePlans.OpenLayersCheck(new WaveDef { number = 1, enemies = new[] { D, F } }), Is.Null);
        }

        // ---- the clock ----

        static List<(int number, double at)> Run(float dt, float total, int living = 0)
        {
            var clock = new WaveClock(interval: 30f);
            clock.Start();
            var got = new List<(int, double)>();
            var buf = new List<WaveDef>();
            double t = 0;
            while (t < total)
            {
                buf.Clear();
                clock.Tick(dt, living, false, buf);
                t += dt;
                foreach (var w in buf) got.Add((w.number, t));
            }
            return got;
        }

        [Test]
        public void NothingIsReleasedBeforeStart()
        {
            var clock = new WaveClock(interval: 30f);
            var buf = new List<WaveDef>();
            clock.Tick(10f, 0, false, buf);
            Assert.That(buf, Is.Empty);
            Assert.That(clock.Running, Is.False);
            Assert.That(clock.SecondsToNext, Is.EqualTo(0f));
        }

        [Test]
        public void WaveOneComesOnTheFirstTickAndTheRestEveryThirtySeconds()
        {
            var clock = new WaveClock(interval: 30f);
            var buf = new List<WaveDef>();
            clock.Start();
            Assert.That(clock.Running, Is.True);
            clock.Tick(0.016f, 0, false, buf);
            Assert.That(buf.Select(w => w.number), Is.EqualTo(new[] { 1 }));
            Assert.That(clock.SecondsToNext, Is.EqualTo(30f - 0.016f).Within(1e-3f));
            buf.Clear();
            clock.Tick(29.9f, 3, false, buf);
            Assert.That(buf, Is.Empty);
            clock.Tick(0.1f, 3, false, buf);
            Assert.That(buf.Select(w => w.number), Is.EqualTo(new[] { 2 }));
            Assert.That(clock.Released, Is.EqualTo(2));
        }

        [Test]
        public void TheReleaseSequenceIsTheSameWhateverTheFrameLength()
        {
            // 92 s of running: waves 1..4 (at 0, 30, 60, 90); the frame lengths differ, the sequence does not.
            foreach (float dt in new[] { 0.016f, 0.5f, 31f })
            {
                var seq = Run(dt, 92f).Select(r => r.number).ToArray();
                Assert.That(seq, Is.EqualTo(new[] { 1, 2, 3, 4 }), "dt " + dt);
            }
            // and no wave is released ahead of its time (the hitch frame of 31 s is the only late one)
            foreach (var (number, at) in Run(0.016f, 92f))
                Assert.That(at, Is.GreaterThanOrEqualTo((number - 1) * 30.0 - 0.02).And.LessThan((number - 1) * 30.0 + 0.1), "wave " + number);
        }

        [Test]
        public void AHugeHitchReleasesEveryDueWaveOnceInOrder()
        {
            var clock = new WaveClock(cap: 100, interval: 30f);
            var buf = new List<WaveDef>();
            clock.Start();
            clock.Tick(95f, 0, false, buf);
            Assert.That(buf.Select(w => w.number), Is.EqualTo(new[] { 1, 2, 3, 4 }));
            buf.Clear();
            clock.Tick(0.016f, 0, false, buf);
            Assert.That(buf, Is.Empty, "nothing is released twice");
        }

        [Test]
        public void BotsReleasedInTheSameCallCountAgainstTheCap()
        {
            var clock = new WaveClock(interval: 30f);
            var buf = new List<WaveDef>();
            clock.Start();
            // waves 1 (3) + 2 (4) = 7; wave 3 (4) would make 11 > 10 and waits; wave 4 (due at 90 s) is skipped.
            clock.Tick(100f, 0, false, buf);
            Assert.That(buf.Select(w => w.number), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(clock.Waiting, Is.Not.Null);
            Assert.That(clock.Waiting.number, Is.EqualTo(3));
            Assert.That(clock.Skipped, Is.EqualTo(1));
        }

        [Test]
        public void AWaveThatWouldPassTheCapWaitsAndIsReleasedWhenThereIsRoom()
        {
            var clock = new WaveClock(interval: 30f);
            var buf = new List<WaveDef>();
            clock.Start();
            clock.Tick(0.1f, 0, false, buf);              // wave 1
            buf.Clear();
            clock.Tick(30f, 9, false, buf);               // wave 2 (4 bots) with 9 living: 13 > 10
            Assert.That(buf, Is.Empty);
            Assert.That(clock.Waiting.number, Is.EqualTo(2));
            Assert.That(clock.Released, Is.EqualTo(1));
            clock.Tick(1f, 7, false, buf);                // 7 + 4 = 11: still waits
            Assert.That(buf, Is.Empty);
            clock.Tick(1f, 6, false, buf);                // 6 + 4 = 10: room
            Assert.That(buf.Select(w => w.number), Is.EqualTo(new[] { 2 }));
            Assert.That(clock.Waiting, Is.Null);
            Assert.That(clock.Released, Is.EqualTo(2));
        }

        [Test]
        public void TheNextIntervalWhileOneWaitsIsSkippedAndCounted()
        {
            var clock = new WaveClock(interval: 30f);
            var buf = new List<WaveDef>();
            clock.Start();
            clock.Tick(0.1f, 0, false, buf);              // wave 1
            buf.Clear();
            clock.Tick(30f, 10, false, buf);              // wave 2 waits
            clock.Tick(30f, 10, false, buf);              // wave 3's interval: skipped
            Assert.That(buf, Is.Empty);
            Assert.That(clock.Skipped, Is.EqualTo(1));
            Assert.That(clock.Waiting.number, Is.EqualTo(2), "still only one waiting wave");
            clock.Tick(30f, 10, false, buf);              // wave 4's interval: skipped
            Assert.That(clock.Skipped, Is.EqualTo(2));
            clock.Tick(0.1f, 0, false, buf);
            Assert.That(buf.Select(w => w.number), Is.EqualTo(new[] { 2 }));
            Assert.That(clock.Released, Is.EqualTo(2));
            // the numbering follows the intervals: the next interval (wave 5 at 120 s) is wave 5
            buf.Clear();
            clock.Tick(30f, 0, false, buf);
            Assert.That(buf.Select(w => w.number), Is.EqualTo(new[] { 5 }));
        }

        [Test]
        public void TheClockKeepsItsOwnTimeWhileAWaveWaits()
        {
            var clock = new WaveClock(interval: 30f);
            var buf = new List<WaveDef>();
            clock.Start();
            clock.Tick(0.1f, 0, false, buf);
            clock.Tick(30f, 10, false, buf);              // t = 30.1; wave 2 waits
            Assert.That(clock.SecondsToNext, Is.EqualTo(60f - 30.1f).Within(1e-3f));
            clock.Tick(10f, 10, false, buf);
            Assert.That(clock.SecondsToNext, Is.EqualTo(60f - 40.1f).Within(1e-3f));
        }

        [Test]
        public void AGondolaReleasedInTheSameCallStopsAnotherJoining()
        {
            var clock = new WaveClock(cap: 100, interval: 30f);
            var buf = new List<WaveDef>();
            clock.Start();
            clock.Tick(215f, 0, false, buf);              // waves 1..8 all due; wave 4 brings a gondola, wave 8 would too
            Assert.That(buf.Select(w => w.number), Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
            Assert.That(buf.Sum(w => Count(w, G)), Is.EqualTo(1), "wave 8 sees the gondola of wave 4 alive");
        }

        [Test]
        public void TheGondolaFlagDecidesWaveEightAtItsReleaseTime()
        {
            var clock = new WaveClock(cap: 100, interval: 30f);
            var buf = new List<WaveDef>();
            clock.Start();
            clock.Tick(215f, 0, false, buf);
            buf.Clear();
            clock.Tick(30f, 0, false, buf);               // wave 9
            clock.Tick(30f, 0, false, buf);               // wave 10
            clock.Tick(30f, 0, false, buf);               // wave 11
            buf.Clear();
            clock.Tick(30f, 0, false, buf);               // wave 12: no gondola alive now
            Assert.That(buf.Single().number, Is.EqualTo(12));
            Assert.That(Count(buf.Single(), G), Is.EqualTo(1));
        }

        [Test]
        public void StopCancelsTheWaitingWaveAndNothingMoreComes()
        {
            var clock = new WaveClock(interval: 30f);
            var buf = new List<WaveDef>();
            clock.Start();
            clock.Tick(0.1f, 0, false, buf);
            clock.Tick(30f, 10, false, buf);
            Assert.That(clock.Waiting, Is.Not.Null);
            clock.Stop();
            Assert.That(clock.Running, Is.False);
            Assert.That(clock.Waiting, Is.Null);
            buf.Clear();
            clock.Tick(500f, 0, false, buf);
            Assert.That(buf, Is.Empty);
        }

        [Test]
        public void ResetClearsEverythingForTheNextRun()
        {
            var clock = new WaveClock(interval: 30f);
            var buf = new List<WaveDef>();
            clock.Start();
            clock.Tick(0.1f, 0, false, buf);
            clock.Tick(30f, 10, false, buf);
            clock.Tick(30f, 10, false, buf);
            Assert.That(clock.Skipped, Is.GreaterThan(0));
            clock.Reset();
            Assert.That(clock.Running, Is.False);
            Assert.That(clock.Released, Is.EqualTo(0));
            Assert.That(clock.Skipped, Is.EqualTo(0));
            Assert.That(clock.Waiting, Is.Null);
            Assert.That(clock.SecondsToNext, Is.EqualTo(0f));
            buf.Clear();
            clock.Start();
            clock.Tick(0.016f, 0, false, buf);
            Assert.That(buf.Select(w => w.number), Is.EqualTo(new[] { 1 }), "a fresh run starts with wave 1 again");
        }

        [Test]
        public void StartingARunningClockDoesNotRestartIt()
        {
            var clock = new WaveClock(interval: 30f);
            var buf = new List<WaveDef>();
            clock.Start();
            clock.Tick(0.1f, 0, false, buf);
            buf.Clear();
            clock.Start();
            clock.Tick(0.1f, 3, false, buf);
            Assert.That(buf, Is.Empty);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(-1000f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void ZeroNegativeAndNonFiniteStepsDoNothing(float dt)
        {
            var clock = new WaveClock(interval: 30f);
            var buf = new List<WaveDef>();
            clock.Start();
            clock.Tick(dt, 0, false, buf);
            Assert.That(buf, Is.Empty);
            Assert.That(clock.Released, Is.EqualTo(0));
            Assert.That(clock.SecondsToNext, Is.EqualTo(0f));
            clock.Tick(0.016f, 0, false, buf);
            Assert.That(buf.Select(w => w.number), Is.EqualTo(new[] { 1 }), "the bad step left the clock untouched");
        }

        [Test]
        public void ConstructorRefusesNonsense()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new WaveClock(0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new WaveClock(10, 0f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new WaveClock(10, float.NaN));
        }

        [Test]
        public void ACustomIntervalIsHonoured()
        {
            var clock = new WaveClock(10, 10f);
            var buf = new List<WaveDef>();
            clock.Start();
            clock.Tick(0.1f, 0, false, buf);
            buf.Clear();
            clock.Tick(10f, 0, false, buf);
            Assert.That(buf.Select(w => w.number), Is.EqualTo(new[] { 2 }));
            Assert.That(buf[0].time, Is.EqualTo(10f));
        }
    }
}

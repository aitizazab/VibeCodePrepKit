using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>Task R1: the tow meter and the repossession objective (docs/SKYLINE-PLAN-FLOOR1.md).</summary>
    public class RepossessionTests
    {
        static SocketDef S(string id, SocketGroup g, float x, float y, float z) =>
            new SocketDef { id = id, group = g, position = new Vector3(x, y, z) };

        // The layout sheet's ten sockets (3 pit, 4 inside, 3 high), hand-copied so these tests don't wait for RepossessSites.
        static readonly SocketDef[] Ten =
        {
            S("pit-kiosk", SocketGroup.Pit, -2, -1.5f, 0), S("pit-ne", SocketGroup.Pit, 9, -4, 7), S("pit-sw", SocketGroup.Pit, -13, -4, -7),
            S("inside-n2", SocketGroup.Inside, -10.4f, 0, 21), S("inside-n4", SocketGroup.Inside, 10.4f, 0, 21),
            S("inside-s2", SocketGroup.Inside, -10.4f, 0, -21), S("inside-s4", SocketGroup.Inside, 10.4f, 0, -21),
            S("high-bridge", SocketGroup.High, 8, 6, 0), S("high-north-east", SocketGroup.High, 30, 6, 20),
            S("high-south-west", SocketGroup.High, -22, 6, -20),
        };

        static AssetDef[] Assets() => new[]
        {
            new AssetDef { kind = AssetKind.Mascot, id = "mascot" },
            new AssetDef { kind = AssetKind.MassageChair, id = "chair" },
            new AssetDef { kind = AssetKind.ArcadeCabinet, id = "cabinet" },
        };

        // ---------------------------------------------------------------- the meter

        [TestCase(0.016f)]
        [TestCase(1f / 60f)]
        [TestCase(0.5f)]
        [TestCase(1f)]
        [TestCase(31f)]
        public void TenSecondsInsideFillsTheMeterHoweverDtIsCut(float dt)
        {
            var m = new TowMeter();
            float t = 0f;
            while (!m.Done && t < 20f) { m.Tick(dt, true); t += dt; }
            Assert.That(m.Done, Is.True);
            Assert.That(m.Progress, Is.EqualTo(1f));
            Assert.That(t, Is.EqualTo(10f).Within(dt + 1e-3f), "done after 10 s of inside time, not a frame before");
        }

        [Test]
        public void NotDoneBeforeTenSeconds()
        {
            var m = new TowMeter();
            for (int i = 0; i < 599; i++) m.Tick(1f / 60f, true);   // 9.983 s
            Assert.That(m.Done, Is.False);
            Assert.That(m.Progress, Is.EqualTo(0.9983f).Within(0.001f));
        }

        [Test]
        public void LeavingPausesAtFourSecondsAndReturningResumes()
        {
            var m = new TowMeter();
            m.Tick(4f, true);
            Assert.That(m.Progress, Is.EqualTo(0.4f).Within(1e-5f));
            m.Tick(30f, false);                                       // away for ages
            Assert.That(m.Progress, Is.EqualTo(0.4f).Within(1e-5f), "never drains");
            m.Tick(5.9f, true);
            Assert.That(m.Done, Is.False);
            m.Tick(0.1f, true);
            Assert.That(m.Done, Is.True);
        }

        [Test]
        public void StandingOnTheEdgeCountsOnlyInsideTime()
        {
            var m = new TowMeter();
            for (int i = 0; i < 100; i++) { m.Tick(0.1f, i % 2 == 0); }   // 5 s inside, 5 s outside
            Assert.That(m.Progress, Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void StartedMeansTimeWasAdded()
        {
            var m = new TowMeter();
            Assert.That(m.Started, Is.False);
            m.Tick(0f, true);
            m.Tick(1f, false);
            Assert.That(m.Started, Is.False, "zero dt or outside the circle does not start it");
            m.Tick(0.001f, true);
            Assert.That(m.Started, Is.True);
            m.Tick(5f, false);
            Assert.That(m.Started, Is.True, "stays started when the player leaves");
        }

        [Test]
        public void DoneIsStickyAndResetClearsIt()
        {
            var m = new TowMeter();
            m.Tick(10f, true);
            Assert.That(m.Done, Is.True);
            m.Tick(5f, false);
            Assert.That(m.Done && m.Progress == 1f, Is.True);
            m.Reset();
            Assert.That(m.Done, Is.False);
            Assert.That(m.Started, Is.False);
            Assert.That(m.Progress, Is.EqualTo(0f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void BadDtDoesNothing(float dt)
        {
            var m = new TowMeter();
            m.Tick(3f, true);
            m.Tick(dt, true);
            Assert.That(m.Progress, Is.EqualTo(0.3f).Within(1e-5f));
        }

        // ---------------------------------------------------------------- the sheet

        [Test]
        public void PickGivesOneSocketPerGroupAndIsDeterministic()
        {
            for (ulong seed = 0; seed < 50; seed++)
            {
                var a = Repossession.Pick(seed, Ten);
                var b = Repossession.Pick(seed, Ten);
                Assert.That(a.Select(s => s.group), Is.EqualTo(new[] { SocketGroup.Pit, SocketGroup.Inside, SocketGroup.High }));
                Assert.That(a.Select(s => s.id), Is.EqualTo(b.Select(s => s.id)));
            }
        }

        [Test]
        public void PickIsDifferentAcrossSeeds()
        {
            var distinct = new HashSet<string>();
            for (ulong seed = 0; seed < 50; seed++) distinct.Add(string.Join(",", Repossession.Pick(seed, Ten).Select(s => s.id)));
            Assert.That(distinct.Count, Is.GreaterThan(10));
        }

        [Test]
        public void OverFiveHundredSeedsEverySocketAndEveryAssetToGroupPermutationAppears()
        {
            var sockets = new HashSet<string>();
            var perms = new HashSet<string>();
            for (ulong seed = 1; seed <= 500; seed++)
            {
                var r = new Repossession(Assets(), seed, Ten);
                foreach (var e in r.Sheet) sockets.Add(e.socket.id);
                perms.Add(string.Join(",", r.Sheet.Select(e => e.socket.group)));
            }
            Assert.That(sockets.Count, Is.EqualTo(10), "every socket of every group is picked at least once");
            Assert.That(perms.Count, Is.EqualTo(6), "all 3! asset-to-group permutations");
        }

        [Test]
        public void SheetHasOneAssetPerGroupAndKeepsTheAssetOrder()
        {
            var r = new Repossession(Assets(), 7, Ten);
            Assert.That(r.Sheet.Select(e => e.asset.id), Is.EqualTo(new[] { "mascot", "chair", "cabinet" }));
            Assert.That(r.Sheet.Select(e => e.socket.group).Distinct().Count(), Is.EqualTo(3));
            Assert.That(r.Sheet.All(e => !e.Towed), Is.True);
        }

        [Test]
        public void SameSeedSameSheet()
        {
            var a = new Repossession(Assets(), 99, Ten);
            var b = new Repossession(Assets(), 99, Ten);
            Assert.That(a.Sheet.Select(e => e.socket.id), Is.EqualTo(b.Sheet.Select(e => e.socket.id)));
        }

        // ---------------------------------------------------------------- the play

        class Log
        {
            public readonly List<string> started = new List<string>(), towed = new List<string>();
            public int all;
            public Log(Repossession r)
            {
                r.MeterStarted += e => started.Add(e.asset.id);
                r.Towed += e => towed.Add(e.asset.id);
                r.AllTowedEvent += () => all++;
            }
        }

        static Vector3 Feet(JobEntry e, float dx = 0f, float dy = 0f) => e.socket.position + new Vector3(dx, dy, 0f);

        [Test]
        public void MeterStartedFiresOncePerAssetOnTheFirstMovingTick()
        {
            var r = new Repossession(Assets(), 3, Ten);
            var log = new Log(r);
            var e = r.Sheet[0];
            r.Tick(0f, Feet(e));
            Assert.That(log.started, Is.Empty, "standing in the circle with no time passing is not clamping");
            r.Tick(0.016f, Feet(e));
            r.Tick(0.016f, Feet(e));
            r.Tick(1f, Feet(e, 50f));                       // leave
            r.Tick(0.016f, Feet(e));                        // come back
            Assert.That(log.started, Is.EqualTo(new[] { e.asset.id }));
            Assert.That(log.towed, Is.Empty);
        }

        [Test]
        public void TowedFiresOnceAndTheCircleGoesDead()
        {
            var r = new Repossession(Assets(), 3, Ten);
            var log = new Log(r);
            var e = r.Sheet[1];
            for (int i = 0; i < 700; i++) r.Tick(1f / 60f, Feet(e));
            Assert.That(e.Towed, Is.True);
            Assert.That(log.towed, Is.EqualTo(new[] { e.asset.id }));
            Assert.That(r.TowedCount, Is.EqualTo(1));
            Assert.That(r.ActiveCircle, Is.EqualTo(-1), "a towed asset's circle holds nobody");
            Assert.That(log.started.Count, Is.EqualTo(1));
        }

        [Test]
        public void AllTowedEventFiresOnceWhenTheThirdCompletes()
        {
            var r = new Repossession(Assets(), 11, Ten);
            var log = new Log(r);
            for (int k = 0; k < 3; k++)
            {
                Assert.That(r.AllTowed, Is.False);
                Assert.That(log.all, Is.EqualTo(0));
                var e = r.Sheet[k];
                for (int i = 0; i < 660; i++) r.Tick(1f / 60f, Feet(e));
            }
            Assert.That(r.AllTowed, Is.True);
            Assert.That(log.all, Is.EqualTo(1));
            Assert.That(log.towed.Count, Is.EqualTo(3));
            Assert.That(log.started.Count, Is.EqualTo(3));
            for (int i = 0; i < 100; i++) r.Tick(1f / 60f, Feet(r.Sheet[0]));
            Assert.That(log.all, Is.EqualTo(1));
        }

        [Test]
        public void OrderIsFree()
        {
            var r = new Repossession(Assets(), 5, Ten);
            foreach (int k in new[] { 2, 0, 1 })
                for (int i = 0; i < 660; i++) r.Tick(1f / 60f, Feet(r.Sheet[k]));
            Assert.That(r.AllTowed, Is.True);
        }

        [Test]
        public void EventOrderInOneCallIsStartedThenTowedThenAll()
        {
            var r = new Repossession(Assets(), 2, Ten);
            var order = new List<string>();
            r.MeterStarted += e => order.Add("started");
            r.Towed += e => order.Add("towed");
            r.AllTowedEvent += () => order.Add("all");
            // One enormous frame fills each meter at once.
            foreach (var e in r.Sheet) r.Tick(100f, Feet(e));
            Assert.That(order, Is.EqualTo(new[] { "started", "towed", "started", "towed", "started", "towed", "all" }));
        }

        [Test]
        public void AFarAbovePlayerDoesNotFillACircle()
        {
            // the pit kiosk's circle, seen from the bridge (dy 6) and from the ring (dy 1.5 is fine, a jump is fine)
            var r = new Repossession(Assets(), 1, Ten);
            var e = r.Sheet[0];
            r.Tick(5f, Feet(e, 0f, 6f));
            Assert.That(e.meter.Progress, Is.EqualTo(0f));
            Assert.That(r.ActiveCircle, Is.EqualTo(-1));
            r.Tick(1f, Feet(e, 0f, 2.5f));
            Assert.That(e.meter.Progress, Is.EqualTo(0.1f).Within(1e-5f), "2.5 m above is still inside the circle's reach");
            r.Tick(1f, Feet(e, 0f, 2.6f));
            Assert.That(e.meter.Progress, Is.EqualTo(0.1f).Within(1e-5f));
            r.Tick(1f, Feet(e, 0f, -2.6f));
            Assert.That(e.meter.Progress, Is.EqualTo(0.1f).Within(1e-5f), "and 2.6 m below is outside");
        }

        [Test]
        public void TheCircleIsThreeMetresAcross()
        {
            var r = new Repossession(Assets(), 1, Ten);
            var e = r.Sheet[0];
            Assert.That(r.CircleAt(Feet(e, 2.99f)), Is.EqualTo(0));
            Assert.That(r.CircleAt(Feet(e, 3.01f)), Is.EqualTo(-1));
            Assert.That(r.CircleAt(e.socket.position + new Vector3(0, 0, -2.99f)), Is.EqualTo(0));
        }

        [Test]
        public void WhereTwoCirclesOverlapTheNearerFills()
        {
            // synthetic: two pit sockets 4 m apart, so a point between them is in both circles
            var two = new[]
            {
                S("pit-a", SocketGroup.Pit, 0, 0, 0), S("in-b", SocketGroup.Inside, 4, 0, 0), S("hi-c", SocketGroup.High, 0, 6, 40),
            };
            var r = new Repossession(Assets(), 4, two);
            var a = r.Sheet.First(e => e.socket.id == "pit-a");
            var b = r.Sheet.First(e => e.socket.id == "in-b");
            r.Tick(1f, new Vector3(1.5f, 0, 0));       // 1.5 from A, 2.5 from B: both contain the feet
            Assert.That(a.meter.Progress, Is.EqualTo(0.1f).Within(1e-5f));
            Assert.That(b.meter.Progress, Is.EqualTo(0f));
            Assert.That(r.Sheet[r.ActiveCircle], Is.SameAs(a));
            r.Tick(1f, new Vector3(2.5f, 0, 0));
            Assert.That(b.meter.Progress, Is.EqualTo(0.1f).Within(1e-5f));
            Assert.That(a.meter.Progress, Is.EqualTo(0.1f).Within(1e-5f));
        }

        [Test]
        public void AHitchedFrameAndSmoothFramesGiveTheSameResult()
        {
            string Run(float dt)
            {
                var r = new Repossession(Assets(), 21, Ten);
                var log = new Log(r);
                var order = new List<string>();
                r.Towed += e => order.Add(e.asset.id);
                float t = 0f;
                // 12 s at asset 0, then 12 s at asset 1, then 12 s at asset 2
                for (int k = 0; k < 3; k++)
                    for (t = 0f; t < 12f; t += dt) r.Tick(dt, Feet(r.Sheet[k]));
                return string.Join(",", order) + "|" + r.TowedCount + "|" + log.all;
            }
            var expected = Run(1f / 60f);
            Assert.That(Run(0.016f), Is.EqualTo(expected));
            Assert.That(Run(0.5f), Is.EqualTo(expected));
            Assert.That(Run(11f), Is.EqualTo(expected));
        }

        [Test]
        public void ResetKeepsTheSocketsAndRearmsTheAlarm()
        {
            var r = new Repossession(Assets(), 8, Ten);
            var log = new Log(r);
            var ids = r.Sheet.Select(e => e.socket.id).ToArray();
            var first = r.Sheet[0];
            r.Tick(4f, Feet(first));                         // mid-tow
            for (int i = 0; i < 660; i++) r.Tick(1f / 60f, Feet(r.Sheet[1]));   // another one towed
            Assert.That(r.TowedCount, Is.EqualTo(1));
            r.Reset();
            Assert.That(r.Sheet.Select(e => e.socket.id), Is.EqualTo(ids), "the same sockets this run");
            Assert.That(r.TowedCount, Is.EqualTo(0));
            Assert.That(r.AllTowed, Is.False);
            Assert.That(r.ActiveCircle, Is.EqualTo(-1));
            Assert.That(r.Sheet.All(e => e.meter.Progress == 0f && !e.Towed && !e.meter.Started), Is.True);
            int startedBefore = log.started.Count;
            r.Tick(0.1f, Feet(first));
            Assert.That(log.started.Count, Is.EqualTo(startedBefore + 1), "clamping is loud again after a restart");
            for (int k = 0; k < 3; k++)
                for (int i = 0; i < 660; i++) r.Tick(1f / 60f, Feet(r.Sheet[k]));
            Assert.That(log.all, Is.EqualTo(1), "the all-towed event can fire again in the new run");
        }

        [Test]
        public void BadInputsDoNothing()
        {
            var r = new Repossession(Assets(), 1, Ten);
            var e = r.Sheet[0];
            r.Tick(float.NaN, Feet(e));
            r.Tick(-1f, Feet(e));
            r.Tick(float.PositiveInfinity, Feet(e));
            r.Tick(1f, new Vector3(float.NaN, 0, 0));
            r.Tick(1f, new Vector3(float.PositiveInfinity, 0, 0));
            Assert.That(r.Sheet.All(x => x.meter.Progress == 0f), Is.True);
            Assert.That(r.TowedCount, Is.EqualTo(0));
        }

        [Test]
        public void PickNeedsAGroupInEverySocketGroup()
        {
            Assert.Throws<System.InvalidOperationException>(() => Repossession.Pick(1, Ten.Where(s => s.group != SocketGroup.High).ToArray()));
            Assert.Throws<System.ArgumentNullException>(() => Repossession.Pick(1, null));
        }
    }
}

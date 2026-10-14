using System.Collections.Generic;
using System.Numerics;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>Task R2 (pure part): the events the floor director turns into beats (docs/SKYLINE-PLAN-FLOOR1.md). The tow
    /// circle is 3 m in radius and 2.5 m high around a socket's floor point; the positions below stay well inside or outside it.</summary>
    public class TowWatchTests
    {
        static TowWatch Make(ulong seed = 7) => new TowWatch(new Repossession(RepossessSites.Assets(), seed));

        static Vector3 InsideFirst(TowWatch w) => w.Objective.Sheet[0].socket.position + new Vector3(0.5f, 0f, 0f);

        // The arrival alcove: no socket is within 3 m of it (the nearest, inside-s2 and inside-s4, are 10.4 m away).
        static Vector3 FarAway() => new Vector3(0f, 0f, -21f);

        [Test]
        public void TheArrivalPointIsInNoCircleWhicheverSocketsAreChosen()
        {
            for (ulong s = 1; s < 40; s++) Assert.That(Make(s).Objective.CircleAt(FarAway()), Is.EqualTo(-1), "seed " + s);
        }

        [Test]
        public void MeterStartedFiresOnceWithTheAssetAndSetsTheAlarmPosition()
        {
            var w = Make();
            var started = new List<JobEntry>();
            w.MeterStarted += started.Add;
            Vector3 feet = InsideFirst(w);
            for (int i = 0; i < 100; i++) w.Tick(0.016f, feet);
            Assert.That(started.Count, Is.EqualTo(1));
            Assert.That(started[0], Is.SameAs(w.Objective.Sheet[0]));
            Assert.That(w.AlarmAsset.HasValue, Is.True);
            Assert.That(Vector3.Distance(w.AlarmAsset.Value, started[0].socket.position), Is.LessThan(0.001f));
        }

        [Test]
        public void TheAlarmPositionIsTakenOnceByTheWaveThatUsesIt()
        {
            var w = Make();
            w.Tick(0.1f, InsideFirst(w));
            Assert.That(w.ConsumeAlarm().HasValue, Is.True);
            Assert.That(w.AlarmAsset.HasValue, Is.False);
            Assert.That(w.ConsumeAlarm().HasValue, Is.False);
        }

        [Test]
        public void SteppingInAndOutWithoutTimeInTheMeterIsNotAMidTowLeave()
        {
            var w = Make();
            int left = 0;
            w.LeftCircleMidTow += e => left++;
            w.Tick(0f, InsideFirst(w));            // in, but no time passes: the meter has not started
            w.Tick(0.016f, FarAway());
            Assert.That(left, Is.EqualTo(0));
        }

        [Test]
        public void LeavingAfterTheMeterStartedRaisesLeftCircleMidTowOnceForThatTick()
        {
            var w = Make();
            var left = new List<JobEntry>();
            w.LeftCircleMidTow += left.Add;
            Vector3 feet = InsideFirst(w);
            for (int i = 0; i < 30; i++) w.Tick(0.016f, feet);
            Assert.That(left, Is.Empty);
            w.Tick(0.016f, FarAway());
            Assert.That(left.Count, Is.EqualTo(1));
            Assert.That(left[0], Is.SameAs(w.Objective.Sheet[0]));
            w.Tick(0.016f, FarAway());
            w.Tick(0.016f, FarAway());
            Assert.That(left.Count, Is.EqualTo(1), "staying outside does not repeat it");
            Assert.That(w.LeaveCount, Is.EqualTo(1));
        }

        [Test]
        public void ReturningAndLeavingAgainFiresAgain()
        {
            var w = Make();
            int left = 0;
            w.LeftCircleMidTow += e => left++;
            Vector3 feet = InsideFirst(w);
            for (int k = 0; k < 3; k++)
            {
                w.Tick(0.5f, feet);
                w.Tick(0.016f, FarAway());
            }
            Assert.That(left, Is.EqualTo(3));
        }

        [Test]
        public void PausingNeverDrainsTheMeter()
        {
            var w = Make();
            w.Tick(2f, InsideFirst(w));
            float before = w.Objective.Sheet[0].meter.Progress;
            w.Tick(5f, FarAway());
            Assert.That(w.Objective.Sheet[0].meter.Progress, Is.EqualTo(before).Within(1e-4f));
            Assert.That(before, Is.EqualTo(0.2f).Within(1e-3f));
        }

        [Test]
        public void TheTowCompletingIsNotALeave()
        {
            var w = Make();
            var order = new List<string>();
            w.MeterStarted += e => order.Add("started");
            w.Towed += e => order.Add("towed");
            w.LeftCircleMidTow += e => order.Add("left");
            Vector3 feet = InsideFirst(w);
            for (int i = 0; i < 700; i++) w.Tick(0.016f, feet);     // 11.2 s: done at 10 s
            w.Tick(0.016f, FarAway());
            Assert.That(order, Is.EqualTo(new[] { "started", "towed" }));
            Assert.That(w.TowedCount, Is.EqualTo(1));
        }

        [Test]
        public void ALeaveThenThreeTowsFireInOrderAndAllTowedOnce()
        {
            var w = Make();
            var order = new List<string>();
            w.Towed += e => order.Add("towed " + e.asset.id);
            w.LeftCircleMidTow += e => order.Add("left " + e.asset.id);
            w.AllTowed += () => order.Add("all");
            // asset 0: in, partway, out
            w.Tick(3f, InsideFirst(w));
            w.Tick(0.016f, FarAway());
            // then each asset in turn to its end
            for (int i = 0; i < 3; i++)
            {
                Vector3 f = w.Objective.Sheet[i].socket.position + new Vector3(0.3f, 0f, 0f);
                for (int k = 0; k < 800 && !w.Objective.Sheet[i].Towed; k++) w.Tick(0.016f, f);
                w.Tick(0.016f, FarAway());
            }
            Assert.That(order[0], Is.EqualTo("left " + w.Objective.Sheet[0].asset.id));
            Assert.That(order.FindAll(s => s == "all").Count, Is.EqualTo(1));
            Assert.That(order[order.Count - 1], Is.EqualTo("all"));
            Assert.That(order.FindAll(s => s.StartsWith("towed")).Count, Is.EqualTo(3));
            Assert.That(order.FindAll(s => s.StartsWith("left")).Count, Is.EqualTo(1));
        }

        [Test]
        public void AWreckResetsTheObjectiveAndTheAlarmKeepsTheLeaveCountAndReportsNoLeave()
        {
            var w = Make();
            int left = 0;
            w.LeftCircleMidTow += e => left++;
            Vector3 feet = InsideFirst(w);
            w.Tick(2f, feet);
            w.Tick(0.016f, FarAway());
            Assert.That(left, Is.EqualTo(1));
            w.Tick(2f, feet);                 // back in, mid-tow again
            w.Reset();                        // wrecked here
            w.Tick(0.016f, FarAway());        // respawned elsewhere: no leave is reported for the wreck
            Assert.That(left, Is.EqualTo(1));
            Assert.That(w.LeaveCount, Is.EqualTo(1));
            Assert.That(w.Objective.Sheet[0].meter.Progress, Is.EqualTo(0f));
            Assert.That(w.AlarmAsset.HasValue, Is.False);
            int started = 0;
            w.MeterStarted += e => started++;   // the alarm can fire again, at the same socket
            w.Tick(0.1f, feet);
            Assert.That(started, Is.EqualTo(1));
        }

        [Test]
        public void TheCircleIsThreeDimensional()
        {
            var w = Make(3);
            // directly above the first socket and 6 m up: within the horizontal radius, outside the vertical reach
            Vector3 above = w.Objective.Sheet[0].socket.position + new Vector3(0f, 6f, 0f);
            int started = 0;
            w.MeterStarted += e => started++;
            w.Tick(1f, above);
            Assert.That(started, Is.EqualTo(0));
        }

        [Test]
        public void ANullObjectiveIsRefused()
        {
            Assert.Throws<System.ArgumentNullException>(() => new TowWatch(null));
        }
    }
}

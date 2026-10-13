using System.Collections.Generic;
using System.Numerics;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    public class HandlingTimelineTests
    {
        static HandlingClip Reload() => new HandlingClip(HandlingClipKind.ReloadEmpty, 2f,
            new[]
            {
                new HandlingEvent(0f, "start", "test/start"),
                new HandlingEvent(0.25f, "mag_out", "test/mag_out"),
                new HandlingEvent(0.5f, "cell_in", "test/cell_in"),
                new HandlingEvent(1f, "bolt", "test/bolt"),
            },
            new[]
            {
                new PoseFrame(0f, Vector3.Zero, Vector3.Zero, Vector3.Zero),
                new PoseFrame(1f, new Vector3(1f, 0f, 0f), new Vector3(0f, 10f, 0f), new Vector3(0f, -1f, 0f)),
            });

        /// <summary>Plays the clip in steps of <paramref name="dt"/>; each event's id with the time it fired.</summary>
        static List<KeyValuePair<string, float>> Times(HandlingClip clip, float dt, float speed = 1f)
        {
            var p = new HandlingPlayer();
            p.Play(clip, speed);
            var fired = new List<HandlingEvent>();
            var times = new List<KeyValuePair<string, float>>();
            float t = 0f;
            p.Advance(0f, fired);
            foreach (var e in fired) times.Add(new KeyValuePair<string, float>(e.id, t));
            for (int i = 0; i < 100000 && p.Playing; i++)
            {
                fired.Clear();
                p.Advance(dt, fired);
                t += dt;
                foreach (var e in fired) times.Add(new KeyValuePair<string, float>(e.id, t));
            }
            return times;
        }

        [Test]
        public void EventsFireInOrderAtTheirFractionsOfTheClip()
        {
            var times = Times(Reload(), 1f / 120f);
            Assert.That(times.Count, Is.EqualTo(4));
            string[] ids = { "start", "mag_out", "cell_in", "bolt" };
            float[] due = { 0f, 0.5f, 1f, 2f };
            for (int i = 0; i < 4; i++)
            {
                Assert.That(times[i].Key, Is.EqualTo(ids[i]));
                Assert.That(times[i].Value, Is.EqualTo(due[i]).Within(1f / 120f + 1e-4f), ids[i]);
            }
        }

        [Test]
        public void ALongStepFiresEveryEventItCrossesOnceInOrder()
        {
            var p = new HandlingPlayer();
            p.Play(Reload());
            var fired = new List<HandlingEvent>();
            Assert.That(p.Advance(5f, fired), Is.EqualTo(4));
            Assert.That(fired[0].id, Is.EqualTo("start"));
            Assert.That(fired[3].id, Is.EqualTo("bolt"));
            Assert.That(p.Advance(1f, fired), Is.EqualTo(0), "nothing fires twice");
            Assert.That(p.Playing, Is.False);
            Assert.That(p.Progress01, Is.EqualTo(1f));
        }

        [Test]
        public void AFasterReloadRescalesTheTimelineAndKeepsTheFractions()
        {
            var times = Times(Reload(), 1f / 240f, 1.25f);
            Assert.That(times[2].Key, Is.EqualTo("cell_in"));
            Assert.That(times[2].Value, Is.EqualTo(0.8f).Within(1f / 240f + 1e-4f), "0.5 of 2 s / 1.25");
            Assert.That(times[3].Value, Is.EqualTo(1.6f).Within(1f / 240f + 1e-4f));
            Assert.That(HandlingPlayer.EventTime(Reload(), 2, 1.25f), Is.EqualTo(0.8f).Within(1e-5f));
        }

        [Test]
        public void TheSameEventsFireAt20And144Fps()
        {
            var slow = Times(Reload(), 1f / 20f);
            var fast = Times(Reload(), 1f / 144f);
            Assert.That(slow.Count, Is.EqualTo(fast.Count));
            for (int i = 0; i < slow.Count; i++)
            {
                Assert.That(slow[i].Key, Is.EqualTo(fast[i].Key));
                Assert.That(slow[i].Value, Is.EqualTo(fast[i].Value).Within(1f / 20f + 1e-4f));
            }
        }

        [Test]
        public void StoppingDropsTheRestOfTheClip()
        {
            var p = new HandlingPlayer();
            p.Play(Reload());
            var fired = new List<HandlingEvent>();
            p.Advance(0.6f, fired);
            Assert.That(fired.Count, Is.EqualTo(2));
            p.Stop();
            Assert.That(p.Advance(5f, fired), Is.EqualTo(0));
            Assert.That(p.Clip, Is.Null);
        }

        [Test]
        public void ABrokenStepIsIgnored()
        {
            var p = new HandlingPlayer();
            p.Play(Reload());
            var fired = new List<HandlingEvent>();
            p.Advance(float.NaN, fired);
            p.Advance(-1f, fired);
            Assert.That(fired.Count, Is.EqualTo(1), "only the event at 0");
            Assert.That(p.Elapsed, Is.EqualTo(0f));
        }

        [Test]
        public void ThePoseIsEasedBetweenFramesAndHeldOutsideThem()
        {
            var c = Reload();
            Assert.That(c.SamplePose(0.5f).gunOffset.X, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(c.SamplePose(0.25f).gunOffset.X, Is.EqualTo(0.15625f).Within(1e-5f), "smoothstep, not linear");
            Assert.That(c.SamplePose(1.5f).magOffset.Y, Is.EqualTo(-1f).Within(1e-5f));
            Assert.That(new HandlingClip(HandlingClipKind.Fire, 0.2f, null, null).SamplePose(0.5f).gunEuler, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void ValidateNamesWhatIsWrong()
        {
            Assert.That(Reload().Validate(), Is.Null);
            Assert.That(new HandlingClip(HandlingClipKind.Fire, 0f, null, null).Validate(), Does.Contain("seconds"));
            var outOfRange = new HandlingClip(HandlingClipKind.Inspect, 1f, new[] { new HandlingEvent(1.2f, "spin", "x/spin") }, null);
            Assert.That(outOfRange.Validate(), Does.Contain("spin").And.Contain("[0, 1]"));
            var unordered = new HandlingClip(HandlingClipKind.Inspect, 1f, new[] { new HandlingEvent(0.5f, "a", null), new HandlingEvent(0.2f, "b", null) }, null);
            Assert.That(unordered.Validate(), Does.Contain("order"));
            var twice = new HandlingClip(HandlingClipKind.Inspect, 1f, new[] { new HandlingEvent(0.1f, "a", null), new HandlingEvent(0.2f, "a", null) }, null);
            Assert.That(twice.Validate(), Does.Contain("twice"));
            var badPose = new HandlingClip(HandlingClipKind.Inspect, 1f, null, new[] { new PoseFrame(0.6f, Vector3.Zero, Vector3.Zero, Vector3.Zero), new PoseFrame(0.3f, Vector3.Zero, Vector3.Zero, Vector3.Zero) });
            Assert.That(badPose.Validate(), Does.Contain("pose frame 1"));
        }
    }
}

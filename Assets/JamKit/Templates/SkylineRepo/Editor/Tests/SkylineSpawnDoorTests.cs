using System;
using System.Collections.Generic;
using JamKit.Skyline;
using NUnit.Framework;
using UnityEngine;

namespace JamKit.SkylineRepo.Tests
{
    /// <summary>
    /// EditMode checks for the spawn doors (task SD1; docs/SKYLINE-FLOOR-REPOSSESS.md section 3): the telegraph lasts 2.0 s (within a
    /// frame at 60 fps) and fires once, Cancel stops it, every kind has a light, a sound and a motion, a busy door says so, and a
    /// second door that starts in the same frame has its first sound staggered so the mix's retrigger gap does not drop it.
    /// </summary>
    public class SkylineSpawnDoorTests
    {
        GameObject _arena;
        SkylineSpawnDoors _doors;
        readonly List<string> _sounds = new List<string>();
        readonly List<float> _soundTimes = new List<float>();
        float _clock;

        [SetUp]
        public void SetUp()
        {
            _sounds.Clear();
            _soundTimes.Clear();
            _clock = 0f;
            SkylineRepossessFx.SoundSink = (s, p, v) => { _sounds.Add(s); _soundTimes.Add(_clock); };
            _arena = new GameObject("Arena");
            _doors = SkylineSpawnDoors.Attach(_arena.transform);
        }

        [TearDown]
        public void TearDown()
        {
            SkylineRepossessFx.UseDefaultSound();
            UnityEngine.Object.DestroyImmediate(_arena);
        }

        SkylineSpawnDoor First(DoorKind k)
        {
            foreach (var d in _doors.All) if (d.Kind == k) return d;
            return null;
        }

        void Tick(SkylineSpawnDoor d, float seconds, float dt = 1f / 60f)
        {
            int n = Mathf.RoundToInt(seconds / dt);
            for (int i = 0; i < n; i++) { _clock += dt; d.Tick(dt); }
        }

        [Test]
        public void EveryDoorOfTheFloorGetsAComponentOfItsKind()
        {
            var data = RepossessSites.Doors();
            Assert.That(_doors.Count, Is.EqualTo(data.Length));
            foreach (var d in data)
            {
                var door = _doors.Get(d.id);
                Assert.That(door, Is.Not.Null, d.id);
                Assert.That(door.Kind, Is.EqualTo(d.kind));
                Assert.That(Vector3.Distance(door.transform.position, SkylineRepossessFx.U(d.position)), Is.LessThan(0.01f), d.id);
                Assert.That(door.Busy, Is.False);
            }
        }

        [Test]
        public void TheTelegraphLastsTwoSecondsWithinAFrameAndFiresOnce()
        {
            foreach (DoorKind k in Enum.GetValues(typeof(DoorKind)))
            {
                var d = First(k);
                int fired = 0;
                d.TelegraphEnded += x => fired++;
                Assert.That(d.BeginTelegraph(), Is.True, k.ToString());
                Assert.That(d.Busy, Is.True);
                Tick(d, 1.9f);                         // 114 frames: not yet
                Assert.That(fired, Is.EqualTo(0), k + " at 1.9 s");
                Assert.That(d.Busy, Is.True);
                Tick(d, 0.1f);                         // 120 frames: 2.0 s, it is up (within a frame)
                Assert.That(fired, Is.EqualTo(1), k + " after 2.0 s");
                Assert.That(d.Busy, Is.False);
                Tick(d, 3f);                           // settling and closing never fires again
                Assert.That(fired, Is.EqualTo(1), k + " once");
                Assert.That(d.FireCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void TheTelegraphEndsAtTwoSecondsWhateverTheFrameRate()
        {
            foreach (float dt in new[] { 1f / 30f, 1f / 60f, 1f / 144f, 0.25f })
            {
                var d = First(DoorKind.SkylightVent);
                d.Cancel();
                int frames = 0;
                bool fired = false;
                Action<SkylineSpawnDoor> on = x => fired = true;
                d.TelegraphEnded += on;
                d.BeginTelegraph();
                while (!fired && frames < 2000) { d.Tick(dt); frames++; }
                d.TelegraphEnded -= on;
                Assert.That(frames * dt, Is.InRange(2f - 1e-3f, 2f + dt + 1e-3f), "dt " + dt);
            }
        }

        [Test]
        public void CancelStopsTheTelegraphShutsTheDoorAndFiresNothing()
        {
            var d = First(DoorKind.ShopShutter);
            int fired = 0;
            d.TelegraphEnded += x => fired++;
            d.BeginTelegraph();
            Tick(d, 1.2f);
            Assert.That(d.OpenAmount, Is.GreaterThan(0f));
            Vector3 risen = d.Panel.localPosition;
            d.Cancel();
            Assert.That(d.Busy, Is.False);
            Assert.That(d.OpenAmount, Is.EqualTo(0f));
            Assert.That(d.DoorLight.enabled, Is.False);
            Assert.That(d.Panel.localPosition.y, Is.LessThan(risen.y));
            Tick(d, 3f);
            Assert.That(fired, Is.EqualTo(0));
            Assert.That(d.FireCount, Is.EqualTo(0));
            Assert.That(d.BeginTelegraph(), Is.True, "and it can be used again");
        }

        [Test]
        public void BeginningAgainWhileBusyChangesNothing()
        {
            var d = First(DoorKind.ServiceHatch);
            Assert.That(d.BeginTelegraph(), Is.True);
            Tick(d, 1f);
            Assert.That(d.BeginTelegraph(), Is.False);
            Assert.That(d.Elapsed, Is.EqualTo(1f).Within(0.03f));
        }

        [Test]
        public void EveryKindHasALightASoundAndAMotion()
        {
            foreach (DoorKind k in Enum.GetValues(typeof(DoorKind)))
            {
                var d = First(k);
                _sounds.Clear();
                Assert.That(d.DoorLight, Is.Not.Null, k + " light");
                Assert.That(d.DoorLight.enabled, Is.False, k + " light is off at rest");
                d.BeginTelegraph();
                Assert.That(_sounds, Does.Contain(DoorTelegraphPlan.For(k).startSound), k + " sound at the start");
                Assert.That(d.DoorLight.enabled, Is.True, k + " light on");
                Tick(d, 1.0f);
                Assert.That(d.DoorLight.intensity, Is.GreaterThan(0f), k + " light level");
                Assert.That(d.OpenAmount, Is.GreaterThan(0f), k + " motion");
                Tick(d, 1.1f);
                Assert.That(d.DoorLight.intensity, Is.GreaterThan(0f));
                Tick(d, 3f);
                Assert.That(d.DoorLight.enabled, Is.False, k + " light off again once it has closed");
                Assert.That(d.OpenAmount, Is.EqualTo(0f).Within(1e-4f), k + " closed again");
            }
        }

        [Test]
        public void TheShutterRisesTwoAndAHalfMetresAndTheVentFlapsSlideApart()
        {
            var s = First(DoorKind.ShopShutter);
            float y0 = s.Panel.localPosition.y;
            s.BeginTelegraph();
            Tick(s, 1.99f);
            Assert.That(s.Panel.localPosition.y - y0, Is.GreaterThan(2.3f));
            Tick(s, 0.2f);
            Assert.That(s.Panel.localPosition.y - y0, Is.EqualTo(2.5f).Within(0.05f), "held at the top just after the telegraph");

            var v = First(DoorKind.SkylightVent);
            var flapA = v.transform.Find("Flap A");
            var flapB = v.transform.Find("Flap B");
            float gap0 = flapB.localPosition.x - flapA.localPosition.x;
            v.BeginTelegraph();
            Tick(v, 2.05f);
            float gap1 = flapB.localPosition.x - flapA.localPosition.x;
            Assert.That(gap1 - gap0, Is.EqualTo(2f * DoorTelegraphPlan.For(DoorKind.SkylightVent).travel).Within(0.05f));
        }

        [Test]
        public void ABlinkingDoorBeepsAtEachBlinkAndANonBlinkingOneOnlyAtTheStart()
        {
            var vent = First(DoorKind.SkylightVent);
            vent.BeginTelegraph();
            Tick(vent, 2.05f);
            Assert.That(_sounds.Count, Is.InRange(6, 10), "the start beep plus the climbing blinks");
            _sounds.Clear();
            var shutter = First(DoorKind.ShopShutter);
            shutter.BeginTelegraph();
            Tick(shutter, 2.05f);
            Assert.That(_sounds.Count, Is.EqualTo(1));
        }

        [Test]
        public void TheRegistryStartsADoorOnceForAWaveThatSendsTwoBotsThroughIt()
        {
            var door = RepossessSites.Doors()[0];
            var a = new SpawnAssignment { enemy = SkylineEnemies.DroneId, door = door };
            var b = new SpawnAssignment { enemy = SkylineEnemies.FloorCareId, door = door };
            Assert.That(_doors.Telegraph(a), Is.True);
            Assert.That(_doors.Telegraph(b), Is.True);
            Assert.That(_doors.BusyDoors(), Is.EqualTo(new[] { door.id }));
            Assert.That(_sounds.Count, Is.EqualTo(1));
            _doors.CancelAll();
            Assert.That(_doors.BusyDoors(), Is.Empty);
        }

        [Test]
        public void AnUnknownDoorIsRefused()
        {
            var ghost = new SpawnAssignment { enemy = SkylineEnemies.DroneId, door = new SpawnDoor { id = "nowhere" } };
            Assert.That(_doors.Telegraph(ghost), Is.False);
            Assert.That(_doors.Telegraph(null), Is.False);
        }

        [Test]
        public void TwoDoorsStartedInOneFrameStaggerTheirFirstSound()
        {
            var all = RepossessSites.Doors();
            SpawnDoor d1 = null, d2 = null;
            foreach (var d in all)
                if (d.kind == DoorKind.ShopShutter) { if (d1 == null) d1 = d; else if (d2 == null) d2 = d; }
            _doors.Telegraph(new SpawnAssignment { enemy = SkylineEnemies.DroneId, door = d1 });
            _doors.Telegraph(new SpawnAssignment { enemy = SkylineEnemies.DroneId, door = d2 });
            Assert.That(_sounds.Count, Is.EqualTo(1), "the second door's sound waits");
            var door2 = _doors.Get(d2.id);
            Tick(door2, SkylineSpawnDoors.SoundStagger + 0.05f);
            Assert.That(_sounds.Count, Is.EqualTo(2));
            Assert.That(_soundTimes[1] - _soundTimes[0], Is.GreaterThanOrEqualTo(SkylineSpawnDoors.SoundStagger - 0.02f));
        }

        [Test]
        public void TheBuildersGreyBoxBlockOnAMarkerIsHidden()
        {
            UnityEngine.Object.DestroyImmediate(_arena);       // a fresh arena: the marker must be the first "Door <id>" found
            _arena = new GameObject("Arena");
            var d = RepossessSites.Doors()[0];
            var marker = new GameObject("Door " + d.id);
            marker.transform.SetParent(_arena.transform, false);
            var block = new GameObject("Marker"); block.transform.SetParent(marker.transform, false);
            var nose = new GameObject("Marker Nose"); nose.transform.SetParent(marker.transform, false);
            var set = SkylineSpawnDoors.Attach(_arena.transform);
            Assert.That(block.activeSelf, Is.False);
            Assert.That(nose.activeSelf, Is.False);
            Assert.That(set.Get(d.id).gameObject, Is.SameAs(marker), "the component sits on the builder's marker");
        }

        [Test]
        public void ABotNeverComesBeforeTheTelegraphIsUpUnderTheDirectorsLoop()
        {
            // The director's loop: WaveSpawner decides when (2 s), the door shows it. Both clocks agree to within a frame.
            var choice = new SpawnChoice { ok = true };
            var door = RepossessSites.Doors()[0];
            choice.assignments.Add(new SpawnAssignment { enemy = SkylineEnemies.DroneId, door = door });
            choice.assignments.Add(new SpawnAssignment { enemy = SkylineEnemies.FloorCareId, door = RepossessSites.Doors()[3] });
            var spawner = new WaveSpawner();
            spawner.Order(choice, 0f);
            var started = new List<SpawnAssignment>();
            var due = new List<SpawnAssignment>();
            spawner.Tick(0.016f, due, started);
            foreach (var a in started) _doors.Telegraph(a);
            Assert.That(due, Is.Empty);
            var fireTimes = new Dictionary<string, float>();
            foreach (var a in started)
            {
                string id = a.door.id;
                _doors.Get(id).TelegraphEnded += x => fireTimes[x.DoorId] = _clock;
            }
            float now = 0.016f;
            for (int i = 0; i < 130; i++)
            {
                now += 1f / 60f;
                _clock = now;
                foreach (var a in started) _doors.Get(a.door.id).Tick(1f / 60f);
                due.Clear();
                spawner.Tick(now, due, null);
                if (due.Count > 0) break;
            }
            Assert.That(due.Count, Is.EqualTo(2));
            foreach (var a in due)
                Assert.That(fireTimes.ContainsKey(a.door.id) && Math.Abs(fireTimes[a.door.id] - now) < 2f / 60f + 1e-3f, Is.True, a.door.id);
        }
    }
}

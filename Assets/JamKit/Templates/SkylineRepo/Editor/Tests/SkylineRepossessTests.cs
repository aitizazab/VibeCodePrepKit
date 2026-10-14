using System.Collections.Generic;
using JamKit.Skyline;
using NUnit.Framework;
using UnityEngine;

namespace JamKit.SkylineRepo.Tests
{
    /// <summary>
    /// EditMode checks for the repossess runtime (task R2; docs/SKYLINE-PLAN-FLOOR1.md): the meter fills from the feet check (3D),
    /// the events the director needs fire once and in order, the alarm sounds once per asset, a tow hoists the asset away in 1.2 s,
    /// the third tow opens the west lift and the exit run ends the floor, and a restart keeps the sockets and re-places everything.
    /// A fake arena stands in for the built scene: socket markers with grey-box discs, and a <c>lift-shutter</c> box.
    /// </summary>
    public class SkylineRepossessTests
    {
        GameObject _arena, _feet, _host;
        SkylineRepossess _r;
        Transform _shutter;
        readonly List<string> _sounds = new List<string>();
        readonly List<string> _logs = new List<string>();

        void OnLog(string condition, string stack, LogType type) { if (type == LogType.Log) _logs.Add(condition); }

        [SetUp]
        public void SetUp()
        {
            _sounds.Clear();
            _logs.Clear();
            Application.logMessageReceived += OnLog;
            SkylineRepossessFx.SoundSink = (s, p, v) => _sounds.Add(s);

            _arena = new GameObject("Arena");
            foreach (var s in RepossessSites.Sockets())
            {
                var marker = new GameObject("Socket " + s.id);
                marker.transform.SetParent(_arena.transform, false);
                marker.transform.position = SkylineRepossessFx.U(s.position);
                var decal = new GameObject("Circle Decal (grey-box)");
                decal.transform.SetParent(marker.transform, false);
            }
            var shutter = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shutter.name = RepossessArena.LiftShutterId;
            shutter.transform.SetParent(_arena.transform, false);
            shutter.transform.position = new Vector3(-24.2f, 1.6f, 0f);
            shutter.transform.localScale = new Vector3(0.4f, 3.2f, 4f);
            _shutter = shutter.transform;
            _feet = new GameObject("Feet");
            _feet.transform.position = new Vector3(0f, 0f, -21f);
            _host = new GameObject("Repossess");
            _r = _host.AddComponent<SkylineRepossess>();
            _r.setupOnStart = false;
            _r.Setup(_arena.transform, _feet.transform, 11UL);
            _r.Begin();
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLog;
            SkylineRepossessFx.UseDefaultSound();
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_feet);
            Object.DestroyImmediate(_arena);
        }

        Vector3 InCircle(int i) => _r.AssetPosition(i) + new Vector3(0.5f, 0f, 0f);
        static readonly Vector3 Away = new Vector3(0f, 0f, -21f);

        // Steps `seconds` at 60 fps with the feet at `where`.
        void Run(float seconds, Vector3 where)
        {
            int n = Mathf.RoundToInt(seconds * 60f);
            for (int i = 0; i < n; i++)
            {
                _r.Step(1f / 60f, where);
                _r.Animate(1f / 60f);
            }
        }

        void TowAll()
        {
            for (int i = 0; i < 3; i++)
            {
                Run(10.1f, InCircle(i));
                Run(0.1f, Away);
            }
        }

        [Test]
        public void OneSocketPerGroupIsChosenAndLogged()
        {
            var groups = new HashSet<SocketGroup>();
            foreach (var e in _r.Objective.Sheet) groups.Add(e.socket.group);
            Assert.That(groups.Count, Is.EqualTo(3));
            foreach (var e in _r.Objective.Sheet)
                Assert.That(_logs, Does.Contain("SKYLINE_FLOOR socket " + e.asset.id + "=" + e.socket.id));
        }

        [Test]
        public void TheGreyBoxDiscsAreHiddenAndTheLiveCirclesStandOnTheirSockets()
        {
            foreach (var s in RepossessSites.Sockets())
                Assert.That(_arena.transform.Find("Socket " + s.id + "/Circle Decal (grey-box)").gameObject.activeSelf, Is.False, s.id);
            Assert.That(_r.Circles.Count, Is.EqualTo(3));
            for (int i = 0; i < 3; i++)
            {
                Vector3 want = _r.AssetPosition(i);
                Assert.That(Vector3.Distance(_r.Circles[i].transform.position, want), Is.LessThan(0.01f));
                Assert.That(Vector3.Distance(_r.Assets[i].Root.transform.position, want), Is.LessThan(0.01f));
            }
        }

        [Test]
        public void TheMeterFillsFromTheFeetCheckAndPausesOutside()
        {
            Run(5f, InCircle(0));
            Assert.That(_r.Objective.Sheet[0].meter.Progress, Is.EqualTo(0.5f).Within(0.02f));
            Run(3f, Away);
            Assert.That(_r.Objective.Sheet[0].meter.Progress, Is.EqualTo(0.5f).Within(0.02f), "leaving pauses, it never drains");
            Assert.That(_r.Circles[0].Progress, Is.EqualTo(0.5f).Within(0.02f));
            Assert.That(_r.Circles[0].VisibleSegments, Is.EqualTo(SkylineTowCircle.Segments / 2).Within(2));
        }

        [Test]
        public void TheCircleIsThreeDimensional()
        {
            Vector3 above = _r.AssetPosition(0) + new Vector3(0f, 6f, 0f);     // directly over the socket, out of its vertical reach
            Run(2f, above);
            Assert.That(_r.Objective.Sheet[0].meter.Progress, Is.EqualTo(0f));
            Vector3 edge = _r.AssetPosition(0) + new Vector3(FloorRules.CircleRadius + 0.6f, 0f, 0f);     // a little outside the radius
            Run(2f, edge);
            Assert.That(_r.Objective.Sheet[0].meter.Progress, Is.EqualTo(0f));
        }

        [Test]
        public void NothingTicksUntilBeginAndAfterStop()
        {
            _r.Stop();
            Run(2f, InCircle(0));
            Assert.That(_r.Objective.Sheet[0].meter.Progress, Is.EqualTo(0f));
            _r.Begin();
            Run(1f, InCircle(0));
            Assert.That(_r.Objective.Sheet[0].meter.Progress, Is.GreaterThan(0.05f));
        }

        [Test]
        public void TheAlarmSoundsOncePerAssetWithABeaconAndTheEventsCarryThePosition()
        {
            int started = 0, alarms = 0;
            Vector3 alarmAt = Vector3.zero;
            _r.MeterStarted += e => started++;
            _r.AlarmSounded += (e, p) => { alarms++; alarmAt = p; };
            Run(0.5f, InCircle(1));
            Run(1f, Away);
            Run(2f, InCircle(1));          // back in: the meter is already going, no second alarm
            Assert.That(started, Is.EqualTo(1));
            Assert.That(alarms, Is.EqualTo(1));
            Assert.That(_sounds.FindAll(s => s == SkylineRepossessAlarm.AlarmSound).Count, Is.EqualTo(1));
            Assert.That(Vector3.Distance(alarmAt, _r.AssetPosition(1)), Is.LessThan(0.01f));
            Assert.That(_r.Alarms[1].On, Is.True);
            Assert.That(_r.Alarms[0].On, Is.False);
            Assert.That(_logs, Does.Contain("SKYLINE_FLOOR meter-start " + _r.Objective.Sheet[1].asset.id));
            var pending = _r.ConsumeAlarmAsset();
            Assert.That(pending.HasValue, Is.True);
            Assert.That(_r.ConsumeAlarmAsset().HasValue, Is.False);
        }

        [Test]
        public void TheBeaconSitsOnTheNearestWall()
        {
            // a wall 5 m east of the first asset (a box collider), nothing nearer
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(_arena.transform, false);
            Vector3 a = _r.AssetPosition(0);
            wall.transform.position = a + new Vector3(5.5f, 2f, 0f);
            wall.transform.localScale = new Vector3(1f, 6f, 8f);
            Physics.SyncTransforms();
            Run(0.3f, InCircle(0));
            var beacon = _r.Alarms[0];
            Assert.That(beacon.On, Is.True);
            Assert.That(beacon.OnWall, Is.True);
            Assert.That(beacon.Position.x, Is.EqualTo(a.x + 5f - SkylineRepossessAlarm.StandOff).Within(0.05f));
            Assert.That(beacon.Position.y, Is.EqualTo(a.y + SkylineRepossessAlarm.BeaconHeight).Within(0.05f));
        }

        [Test]
        public void ATowTakesTenSecondsThenTheAssetIsWinchedAwayInOnePointTwo()
        {
            var towed = new List<JobEntry>();
            _r.Towed += towed.Add;
            Run(9.8f, InCircle(0));
            Assert.That(towed, Is.Empty);
            Assert.That(_r.Assets[0].Winching, Is.False);
            Run(0.4f, InCircle(0));
            Assert.That(towed.Count, Is.EqualTo(1));
            Assert.That(_r.TowedCount, Is.EqualTo(1));
            Assert.That(_r.Assets[0].Winching, Is.True);
            Assert.That(_r.Circles[0].Hidden, Is.True);
            Run(SkylineRepossessAsset.WinchSeconds * 0.5f, Away);
            Assert.That(_r.Assets[0].Gone, Is.False);
            Run(SkylineRepossessAsset.WinchSeconds * 0.5f + 0.2f, Away);
            Assert.That(_r.Assets[0].Gone, Is.True);
            Assert.That(_r.Assets[0].Root.activeSelf, Is.False);
            Assert.That(_r.Alarms[0].On, Is.False, "the alarm ends with its asset");
            bool logged = _logs.Exists(l => l.StartsWith("SKYLINE_FLOOR towed " + _r.Objective.Sheet[0].asset.id + " at "));
            Assert.That(logged, Is.True);
        }

        [Test]
        public void LeavingMidTowRaisesTheEventTheDirectorNeeds()
        {
            int left = 0;
            _r.LeftCircleMidTow += e => left++;
            Run(0.016f * 2, InCircle(2));
            Run(0.1f, Away);
            Assert.That(left, Is.EqualTo(1));
            Assert.That(_r.LeaveCount, Is.EqualTo(1));
            Assert.That(_logs.Exists(l => l.StartsWith("SKYLINE_FLOOR left-circle ")), Is.True);
        }

        [Test]
        public void TheEventsComeInTheDirectorsOrderAndAllTowedOnce()
        {
            var order = new List<string>();
            _r.MeterStarted += e => order.Add("meter");
            _r.Towed += e => order.Add("towed");
            _r.AllTowed += () => order.Add("all");
            _r.ExitReached += () => order.Add("exit");
            TowAll();
            Assert.That(order, Is.EqualTo(new[] { "meter", "towed", "meter", "towed", "meter", "towed", "all" }));
            Assert.That(_r.AllDone, Is.True);
            Assert.That(_logs, Does.Contain("SKYLINE_FLOOR all-towed"));
        }

        [Test]
        public void TheThirdTowRollsTheLiftShutterUpAndTheRunForTheLiftEndsTheFloor()
        {
            Vector3 start = _shutter.position;
            int exits = 0;
            _r.ExitReached += () => exits++;
            Run(10.1f, InCircle(0));
            Run(10.1f, InCircle(1));
            Assert.That(_r.Exit.Open, Is.False);
            Run(10.1f, InCircle(2));
            Assert.That(_r.Exit.Open, Is.True);
            Run(SkylineRepossessExit.RollSeconds + 0.2f, Away);
            Assert.That(_shutter.position.y, Is.GreaterThan(start.y + 3.2f), "fully up, clear of the 3.2 m doorway");
            Assert.That(_shutter.GetComponent<Collider>().enabled, Is.False);
            // the feet short of the lift do not end it; inside the zone they do, once
            Run(0.5f, new Vector3(-20f, 0f, 0f));
            Assert.That(exits, Is.EqualTo(0));
            Run(0.5f, new Vector3(-29f, 0f, 0f));
            Run(0.5f, new Vector3(-29f, 0f, 0f));
            Assert.That(exits, Is.EqualTo(1));
            Assert.That(_r.ExitHasBeenReached, Is.True);
            Assert.That(_logs, Does.Contain("SKYLINE_FLOOR exit reached"));
        }

        [Test]
        public void TheLiftZoneIsTheArenasExitBoxWithAHandOfSlack()
        {
            Assert.That(SkylineRepossessExit.InLift(new Vector3(-29f, 0f, 0f)), Is.True);
            Assert.That(SkylineRepossessExit.InLift(new Vector3(-26f, 0f, 0f)), Is.False);
            Assert.That(SkylineRepossessExit.InLift(new Vector3(-29f, 0f, 3f)), Is.False);
            Assert.That(SkylineRepossessExit.InLift(new Vector3(-29f, 4f, 0f)), Is.False);
        }

        [Test]
        public void AWreckKeepsTheSocketsReplacesTheAssetsAndShutsTheLift()
        {
            var sockets = new List<string>();
            foreach (var e in _r.Objective.Sheet) sockets.Add(e.socket.id);
            Vector3 shutterStart = _shutter.position;
            TowAll();
            Run(SkylineRepossessExit.RollSeconds + 0.3f, Away);
            Assert.That(_r.TowedCount, Is.EqualTo(3));
            _r.ResetRun();
            Assert.That(_r.TowedCount, Is.EqualTo(0));
            Assert.That(_r.AllDone, Is.False);
            Assert.That(_r.Running, Is.False);
            for (int i = 0; i < 3; i++)
            {
                Assert.That(_r.Objective.Sheet[i].socket.id, Is.EqualTo(sockets[i]), "same sockets");
                Assert.That(_r.Assets[i].Gone, Is.False);
                Assert.That(_r.Assets[i].Root.activeSelf, Is.True);
                Assert.That(_r.Circles[i].Hidden, Is.False);
                Assert.That(_r.Alarms[i].On, Is.False);
                Assert.That(_r.Drones[i].Present, Is.False);
                Assert.That(_r.Objective.Sheet[i].meter.Progress, Is.EqualTo(0f));
            }
            Assert.That(_r.Exit.Open, Is.False);
            Assert.That(Vector3.Distance(_shutter.position, shutterStart), Is.LessThan(0.01f));
            Assert.That(_shutter.GetComponent<Collider>().enabled, Is.True);
            Assert.That(_shutter.GetComponent<Renderer>().enabled, Is.True);
            // and the whole thing can be done again, the alarm included
            _r.Begin();
            int alarms = 0;
            _r.AlarmSounded += (e, p) => alarms++;
            Run(0.5f, InCircle(0));
            Assert.That(alarms, Is.EqualTo(1));
            Assert.That(_sounds.FindAll(s => s == SkylineRepossessAlarm.AlarmSound).Count, Is.EqualTo(4), "three the first time, one now");
        }

        [Test]
        public void TheDroneComesDownOnTheFirstMeterStartAndItsBeamFollowsTheFeet()
        {
            Assert.That(_r.Drones[0].Present, Is.False);
            Run(1.2f, InCircle(0));
            Assert.That(_r.Drones[0].Present, Is.True);
            Assert.That(_r.Drones[0].BeamBright, Is.True);
            Assert.That(_r.Drones[0].Spot.intensity, Is.GreaterThan(1.5f));
            Run(0.2f, Away);
            Assert.That(_r.Drones[0].BeamBright, Is.False);
            Assert.That(_r.Drones[0].Spot.intensity, Is.LessThan(2f));
            Assert.That(_r.Drones[1].Present, Is.False);
        }

        [Test]
        public void TheCircleRingBrightensWhileTheUnitStandsInIt()
        {
            Run(0.3f, Away);
            var dim = _r.Circles[0].RingRenderer.sharedMaterial;
            Run(0.3f, InCircle(0));
            Assert.That(_r.Circles[0].Active, Is.True);
            Assert.That(_r.Circles[0].RingRenderer.sharedMaterial, Is.Not.SameAs(dim));
            Assert.That(_r.Circles[0].PoolLight.intensity, Is.GreaterThan(3f));
        }

        [Test]
        public void TheAssetsAreInTheObjectiveInkRole()
        {
            // every material of an asset body is an ink material in the objective role (violet outline, through walls)
            foreach (var asset in _r.Assets)
                foreach (var rend in asset.Root.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in rend.sharedMaterials)
                        if (SkylineInkRoles.IsInk(m))
                            Assert.That(SkylineInkRoles.GetRole(m), Is.EqualTo(SkylineInkRoles.Objective), asset.Root.name + "/" + rend.name);
        }
    }
}

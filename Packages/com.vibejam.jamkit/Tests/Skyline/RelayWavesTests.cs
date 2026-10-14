using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>The Relay's wave cadence, cap and spawn side (docs/SKYLINE-RELAY.md).</summary>
    public class RelayWavesTests
    {
        [Test]
        public void AWaveComesAtTheStartAndThenEveryTwentySeconds()
        {
            var w = new RelayWaves();
            w.Start();
            var due = new List<string[]>();
            var times = new List<float>();
            float t = 0f;
            for (int i = 0; i < 121 * 10; i++)
            {
                t += 0.1f;
                int before = due.Count;
                w.Tick(0.1f, 0, due);
                if (due.Count > before) times.Add(t);
            }
            Assert.That(times.Count, Is.EqualTo(7), "waves at 0, 20, 40, 60, 80, 100, 120");
            Assert.That(times[0], Is.EqualTo(0.1f).Within(0.001f));
            for (int i = 1; i < times.Count; i++) Assert.That(times[i], Is.EqualTo(20f * i).Within(0.15f));
        }

        [Test]
        public void NoWaveBeforeTheClockStarts()
        {
            var w = new RelayWaves();
            var due = new List<string[]>();
            w.Tick(5f, 0, due);
            Assert.That(due, Is.Empty);
        }

        [Test]
        public void TheCapHoldsAWaveBackUntilThereIsRoom()
        {
            var w = new RelayWaves();
            w.Start();
            var due = new List<string[]>();
            w.Tick(0.1f, 0, due);
            Assert.That(due.Count, Is.EqualTo(1));
            // 20 s later eight bots are alive: wave 2 (four bots) cannot come
            w.Tick(20f, 8, due);
            Assert.That(due.Count, Is.EqualTo(1));
            Assert.That(w.Waiting, Is.True);
            // two wrecked: still no room for four
            w.Tick(1f, 6, due);
            Assert.That(due.Count, Is.EqualTo(1));
            // room for four: it goes
            w.Tick(0.1f, 4, due);
            Assert.That(due.Count, Is.EqualTo(2));
            Assert.That(w.Waiting, Is.False);
        }

        [Test]
        public void TheLivingCountNeverPassesTheCap()
        {
            var w = new RelayWaves();
            w.Start();
            var due = new List<string[]>();
            int living = 0;
            for (int i = 0; i < 2000; i++)
            {
                int before = due.Count;
                w.Tick(0.1f, living, due);
                for (int k = before; k < due.Count; k++) living += due[k].Length;
                Assert.That(living, Is.LessThanOrEqualTo(RelayWaves.Cap));
            }
        }

        [Test]
        public void WavesAreOnlyDronesAndFloorCareUnits()
        {
            var w = new RelayWaves(100, 5f);
            w.Start();
            var due = new List<string[]>();
            for (int i = 0; i < 400; i++) w.Tick(0.1f, 0, due);
            Assert.That(due.Count, Is.GreaterThan(6));
            foreach (var wave in due)
                foreach (var id in wave)
                    Assert.That(id == SkylineEnemies.DroneId || id == SkylineEnemies.FloorCareId, Is.True, id);
        }

        [Test]
        public void SpawnPointsComeFromTheSideAwayFromTheTargetAndNeverNearTheUnit()
        {
            var rng = new Rng(5);
            var target = RelayArena.Stations[0];    // A, the south-west corner
            var player = new Vector3(0f, 0f, 0f);
            var picks = RelayWaves.PickSpawns(RelayArena.AirSpawns, target, player, 3, rng);
            Assert.That(picks.Count, Is.EqualTo(3));
            Assert.That(picks.Distinct().Count(), Is.EqualTo(3));
            float mean = picks.Average(p => Flat(p, target)), all = RelayArena.AirSpawns.Average(p => Flat(p, target));
            Assert.That(mean, Is.GreaterThan(all), "farther from the target than the average point");
            foreach (var p in picks) Assert.That(Flat(p, player), Is.GreaterThanOrEqualTo(RelayWaves.MinPlayerDistance));
        }

        [Test]
        public void SpawnPickingKeepsOutOfTheUnitsFaceAndGivesFewerWhenFewQualify()
        {
            var rng = new Rng(9);
            var near = RelayArena.Stations[4];
            var picks = RelayWaves.PickSpawns(RelayArena.FloorSpawns, RelayArena.Stations[0], near, 99, rng);
            Assert.That(picks.Count, Is.LessThanOrEqualTo(RelayArena.FloorSpawns.Length));
            foreach (var p in picks) Assert.That(Flat(p, near), Is.GreaterThanOrEqualTo(RelayWaves.MinPlayerDistance));
        }

        static float Flat(Vector3 a, Vector3 b) => (float)System.Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));
    }
}

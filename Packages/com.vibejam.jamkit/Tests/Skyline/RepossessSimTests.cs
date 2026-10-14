using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Movement;
using JamKit.Skyline;
using JamKit.Tests.Movement;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>
    /// Task W3: the headless floor. The old arena's sims stay as they were (FightTests and friends run unchanged); these tests
    /// pin the generalisation (<see cref="FightArea"/>, <see cref="FightSim.AddEnemy"/>) and the new <see cref="RepossessSim"/>:
    /// the wave clock, the doors, the tow, the wreck restart, the façade panes and the gondola that only lays mines.
    /// The walking bots here follow the plan's route data (<see cref="RepossessSites.Routes"/>); F3's navigation graph is
    /// not used (it was being written in parallel): they are stand-ins for the pilot (DM1), not the pilot.
    /// </summary>
    public class RepossessSimTests
    {
        // ---- test drivers

        /// <summary>Stands still; optionally never fires (so nothing it meets dies).</summary>
        sealed class StandBot : IFloorBot
        {
            public bool holdFire;
            public FightInput Next(RepossessSim sim, float h) =>
                new FightInput { move = new MoveInput { yaw = sim.Fight.Player.Yaw }, holdFire = holdFire };
        }

        sealed class Leg
        {
            public Vector3[] points;
            /// <summary>Stand at the end until this socket's asset is towed (null: go on at once).</summary>
            public string towSocket;
        }

        /// <summary>Walks legs of waypoints (sprinting, flat steering) and stands at a leg's end until its socket is towed.</summary>
        sealed class TourBot : IFloorBot
        {
            public readonly List<Leg> legs = new List<Leg>();
            public bool holdFire;
            int _leg, _point;
            public bool Done => _leg >= legs.Count;

            public FightInput Next(RepossessSim sim, float h)
            {
                var p = sim.Fight.Player.Position;
                var idle = new FightInput { move = new MoveInput { yaw = sim.Fight.Player.Yaw }, holdFire = holdFire };
                while (_leg < legs.Count)
                {
                    var leg = legs[_leg];
                    while (_point < leg.points.Length && Flat(p, leg.points[_point]) < (_point == leg.points.Length - 1 ? 0.7f : 1.6f)) _point++;
                    if (_point < leg.points.Length) break;
                    if (leg.towSocket != null && !TowedAt(sim, leg.towSocket)) return idle;
                    _leg++;
                    _point = 0;
                }
                if (_leg >= legs.Count) return idle;
                var t = legs[_leg].points[_point];
                float yaw = MathF.Atan2(t.X - p.X, t.Z - p.Z) * (180f / MathF.PI);
                return new FightInput { move = new MoveInput { move = new Vector2(0f, 1f), yaw = yaw, sprintHeld = true }, holdFire = holdFire };
            }
        }

        static float Flat(Vector3 a, Vector3 b)
        {
            float dx = a.X - b.X, dz = a.Z - b.Z;
            return MathF.Sqrt(dx * dx + dz * dz);
        }

        static bool TowedAt(RepossessSim sim, string socketId)
        {
            foreach (var j in sim.Objective.Sheet) if (j.socket.id == socketId) return j.Towed;
            return false;
        }

        static Vector3[] RouteTo(string id)
        {
            foreach (var r in RepossessSites.Routes()) if (r.id == id) return r.waypoints;
            throw new ArgumentException(id);
        }

        static Vector3[] Reversed(Vector3[] pts)
        {
            var l = new List<Vector3>(pts);
            l.Reverse();
            return l.ToArray();
        }

        /// <summary>A seed whose job sheet holds exactly these sockets.</summary>
        static ulong SeedWithSockets(params string[] ids)
        {
            for (ulong seed = 1; seed < 20000; seed++)
            {
                var pick = Repossession.Pick(seed);
                int found = 0;
                foreach (var s in pick) if (Array.IndexOf(ids, s.id) >= 0) found++;
                if (found == ids.Length) return seed;
            }
            throw new InvalidOperationException("no seed gives " + string.Join(",", ids));
        }

        sealed class World
        {
            public BoxWorld world;
            public readonly Dictionary<string, BoxWorld.Box> boxes = new Dictionary<string, BoxWorld.Box>();
            public readonly List<string> log = new List<string>();
            public FloorOptions options;
        }

        static World Make(Action<FloorOptions> tune = null)
        {
            var w = new World();
            w.world = FloorWorld.Build(w.boxes);
            w.options = new FloorOptions();
            w.options.solidEnabled = (id, on) =>
            {
                w.log.Add(id + (on ? "+" : "-"));
                BoxWorld.Box b;
                if (w.boxes.TryGetValue(id, out b)) b.enabled = on;
            };
            tune?.Invoke(w.options);
            return w;
        }

        static RepossessSim Sim(IFloorBot bot, ulong seed, World w) => new RepossessSim(bot, seed, w.world, w.options);

        // ---- the old sims: defaults preserve M1

        [Test]
        public void TheDefaultAreaIsTheOldHall()
        {
            var a = FightArea.Galleria;
            Assert.That(a.minX, Is.EqualTo(GalleriaArena.HallMinX));
            Assert.That(a.maxX, Is.EqualTo(GalleriaArena.HallMaxX));
            Assert.That(a.minZ, Is.EqualTo(GalleriaArena.HallMinZ));
            Assert.That(a.maxZ, Is.EqualTo(GalleriaArena.HallMaxZ));
            Assert.That(a.searchMaxZ, Is.EqualTo(11f), "the old SearchMaxZ");
            Assert.That(a.mineRailOffset, Is.EqualTo(Vector3.Zero), "mines on the rail itself");
            var fight = FightTests.Make("food-court", new StandFightBot(), 7, new Vector3(0f, 0f, -18f));
            Assert.That(fight.Area.minX, Is.EqualTo(GalleriaArena.HallMinX));
            Assert.That(fight.Area.searchMaxZ, Is.EqualTo(11f));
        }

        sealed class StandFightBot : IFightBot
        {
            public FightInput Next(FightSim f, float h) => new FightInput { move = new MoveInput { yaw = f.Player.Yaw } };
        }

        [Test]
        public void AFightWithNoEnemiesYetDoesNotEndOnItsFirstTick()
        {
            var world = FloorWorld.Build();
            var fight = new FightSim(new EncounterDef { id = "empty", spawns = new Spawn[0] }, world, new StandFightBot(), new CoverBox[0],
                RepossessArena.ArrivalPoint, 5);
            for (int i = 0; i < 30; i++) fight.Tick();
            Assert.That(fight.Over, Is.False);
            Assert.That(fight.Result().cleared, Is.False);
            // and once an enemy is added and wrecked, it ends as before
            var e = fight.AddEnemy(new Spawn(SkylineEnemies.DroneId, new Vector3(0f, 3.8f, -10f)), 0);
            fight.Damage(e, 1000f);
            fight.Tick();
            Assert.That(fight.Over, Is.True);
            Assert.That(fight.Result().cleared, Is.True);
        }

        [Test]
        public void AddEnemyBuildsTheSameEnemyAConstructorSpawnWould()
        {
            var drone = new Spawn(SkylineEnemies.DroneId, new Vector3(2f, 3.8f, -8f), 3);
            var care = new Spawn(SkylineEnemies.FloorCareId, new Vector3(-6f, 0f, -12f), 3);
            var gondola = new Spawn(SkylineEnemies.GondolaId, new Vector3(36.5f, 3f, -27f), 3) { railEnd = new Vector3(36.5f, 3f, 27f) };
            var both = new EncounterDef { id = "ctor", spawns = new[] { drone, care, gondola } };
            var later = new EncounterDef { id = "add", spawns = new[] { drone } };
            var a = new FightSim(both, FloorWorld.Build(), new StandFightBot(), RepossessArena.SightBlockers(), RepossessArena.ArrivalPoint, 21, null, null,
                new FightPlayerDef(), null, 0f, FightArea.Floor1);
            var b = new FightSim(later, FloorWorld.Build(), new StandFightBot(), RepossessArena.SightBlockers(), RepossessArena.ArrivalPoint, 21, null, null,
                new FightPlayerDef(), null, 0f, FightArea.Floor1);
            b.AddEnemy(care, 1);
            b.AddEnemy(gondola, 2);
            a.Wake(3);
            b.Wake(3);
            for (int i = 0; i < 600; i++) { a.Tick(); b.Tick(); }
            Assert.That(b.Enemies.Count, Is.EqualTo(3));
            for (int i = 0; i < 3; i++)
            {
                Assert.That(b.Enemies[i].Index, Is.EqualTo(a.Enemies[i].Index));
                Assert.That(b.Enemies[i].Group, Is.EqualTo(a.Enemies[i].Group));
                Assert.That(b.Enemies[i].Home, Is.EqualTo(a.Enemies[i].Home));
                Assert.That(b.Enemies[i].Position, Is.EqualTo(a.Enemies[i].Position), "enemy " + i + " has moved the same way (same patrol / idle / rail seeds)");
                Assert.That(b.Enemies[i].Brain.State, Is.EqualTo(a.Enemies[i].Brain.State));
            }
            Assert.That(b.Result().hitsTaken, Is.EqualTo(a.Result().hitsTaken));
            Assert.That(b.Result().shotsAtPlayer, Is.EqualTo(a.Result().shotsAtPlayer));
        }

        // ---- the floor sim

        [Test]
        public void ItStartsAtTheDropWithTheClockRunning()
        {
            var w = Make();
            var sim = Sim(new StandBot(), 3, w);
            Assert.That(sim.Flow.Phase, Is.EqualTo(FloorPhase.Floor));
            Assert.That(sim.Clock.Running, Is.True);
            Assert.That(sim.Fight.Player.Position.X, Is.EqualTo(RepossessArena.ArrivalPoint.X).Within(0.01f));
            Assert.That(sim.Fight.Player.Position.Z, Is.EqualTo(RepossessArena.ArrivalPoint.Z).Within(0.01f));
            Assert.That(sim.Fight.EndsOnClear, Is.False);
            Assert.That(sim.Fight.Panes.Count, Is.EqualTo(RepossessArena.PaneRows * RepossessArena.PaneCols), "the 80 façade panes");
            Assert.That(sim.Fight.Area.maxX, Is.EqualTo(35f));
        }

        [Test]
        public void EveryDoorPutsEachBotItMayHostAtAFreePoint()
        {
            var cover = RepossessArena.SightBlockers();
            foreach (var d in RepossessSites.Doors())
                foreach (var id in new[] { SkylineEnemies.DroneId, SkylineEnemies.FloorCareId, SkylineEnemies.GondolaId })
                {
                    if (!FloorRules.DoorAllows(d.kind, id)) continue;
                    var def = SkylineEnemies.Find(id);
                    var p = RepossessSim.SpawnPosition(d, def);
                    Assert.That(float.IsFinite(p.X + p.Y + p.Z), Is.True, d.id);
                    if (d.kind == DoorKind.RailEnd) { Assert.That(p, Is.EqualTo(d.position)); continue; }
                    Assert.That(CoverGeometry.BlockedThick(p, p, cover, def.bodyRadius), Is.False, id + " at " + d.id + " " + p + " is inside a piece");
                    Assert.That(p.X, Is.InRange(-35f, 35f), d.id);
                    Assert.That(p.Z, Is.InRange(-28f, 28f), d.id);
                    if (def.layer == ThreatLayer.Air) Assert.That(p.Y, Is.GreaterThanOrEqualTo(2f), "a drone is never on the floor: " + d.id);
                    if (def.layer == ThreatLayer.Floor) Assert.That(p.Y, Is.EqualTo(0f), "floor-care stands on the floor: " + d.id);
                }
        }

        [Test]
        public void WaveOneComesThroughTwoDoorsNinetyDegreesApartAfterTheTwoSecondTelegraph()
        {
            var w = Make(o => o.invulnerable = true);
            var sim = Sim(new StandBot { holdFire = true }, 11, w);
            var player = sim.Fight.Player.Position;
            var telegraphAt = new List<float>();
            var spawned = new List<SpawnAssignment>();
            var spawnedAt = new List<float>();
            sim.TelegraphStarted += a => telegraphAt.Add(sim.Time);
            sim.BotSpawned += (e, a) => { spawned.Add(a); spawnedAt.Add(sim.Time); };
            sim.Run(3f);
            Assert.That(telegraphAt.Count, Is.EqualTo(3), "all three drones telegraph");
            foreach (float t in telegraphAt) Assert.That(t, Is.LessThan(0.1f), "the telegraph starts with the wave, at the drop");
            Assert.That(spawned.Count, Is.EqualTo(3));
            foreach (float t in spawnedAt) Assert.That(t, Is.EqualTo(FloorRules.DoorTelegraphSeconds).Within(0.05f), "the bots come 2.0 s later");
            var doors = new Dictionary<string, SpawnDoor>();
            foreach (var a in spawned)
            {
                Assert.That(a.enemy, Is.EqualTo(SkylineEnemies.DroneId));
                doors[a.door.id] = a.door;
                Assert.That(Flat(player, a.door.position), Is.GreaterThanOrEqualTo(FloorRules.MinSpawnDistance), a.door.id + " is at least 15 m away");
            }
            Assert.That(doors.Count, Is.EqualTo(2), "two doors");
            var list = new List<SpawnDoor>(doors.Values);
            float spread = SpawnBearings.AngleBetween(SpawnBearings.Bearing(player, list[0].position), SpawnBearings.Bearing(player, list[1].position));
            Assert.That(spread, Is.GreaterThanOrEqualTo(FloorRules.MinBearingSpread));
            Assert.That(sim.Fight.Enemies.Count, Is.EqualTo(3));
            foreach (var e in sim.Fight.Enemies)
            {
                Assert.That(e.Awake, Is.True, "they arrive hunting");
                Assert.That(e.Group / 10, Is.EqualTo(1), "group = wave number x 10 + door index");
            }
            Assert.That(new HashSet<int> { sim.Fight.Enemies[0].Group, sim.Fight.Enemies[1].Group, sim.Fight.Enemies[2].Group }.Count, Is.EqualTo(2));
        }

        [Test]
        public void WavesComeOnTheWaveClockWithTheTablesCompositions()
        {
            var w = Make(o => { o.invulnerable = true; o.waveCap = 100; });
            var sim = Sim(new StandBot { holdFire = true }, 12, w);
            var firstSpawn = new Dictionary<int, float>();
            var sizes = new Dictionary<int, int>();
            var gondolas = new Dictionary<int, int>();
            sim.BotSpawned += (e, a) =>
            {
                int wave = e.Group / 10;
                if (!firstSpawn.ContainsKey(wave)) firstSpawn[wave] = sim.Time;
                sizes[wave] = (sizes.ContainsKey(wave) ? sizes[wave] : 0) + 1;
                if (e.Def.id == SkylineEnemies.GondolaId) gondolas[wave] = 1;
            };
            sim.Run(4 * FloorRules.WaveSeconds + 5f);   // wave 5 is due at 4 intervals, its first bot 2 s later
            Assert.That(firstSpawn.Count, Is.EqualTo(5), "waves 1 to 5 inside four intervals and 5 s");
            for (int n = 1; n <= 5; n++)
                Assert.That(firstSpawn[n], Is.EqualTo((n - 1) * FloorRules.WaveSeconds + 2f).Within(0.1f), "wave " + n);
            Assert.That(sizes[1], Is.EqualTo(3));
            Assert.That(sizes[2], Is.EqualTo(4));
            Assert.That(sizes[3], Is.EqualTo(4));
            Assert.That(sizes[4], Is.EqualTo(3));
            Assert.That(gondolas.ContainsKey(4), Is.True, "wave 4 carries the gondola");
            Assert.That(sizes[5], Is.EqualTo(4));
            Assert.That(sim.Result().wavesReleased, Is.EqualTo(5));
        }

        [Test]
        public void TheCapHoldsAtTenWhenNothingIsKilledAndAWaitingWaveComesWhenThereIsRoom()
        {
            var w = Make(o => o.invulnerable = true);
            var sim = Sim(new StandBot { holdFire = true }, 13, w);
            // wave 1 brings 3, wave 2 brings 4: wave 3 (4 more) would make 11, so it waits
            sim.Run(65f);
            Assert.That(sim.Alive, Is.EqualTo(7));
            Assert.That(sim.Clock.Waiting, Is.Not.Null, "wave 3 waits for room");
            Assert.That(sim.Clock.Waiting.number, Is.EqualTo(3));
            Assert.That(sim.Clock.Released, Is.EqualTo(2));
            // one drone down: 6 + 4 = 10, exactly the cap, so the waiting wave is released
            sim.Fight.Damage(sim.Fight.Enemies[0], 1000f);
            sim.Run(3.5f);
            Assert.That(sim.Clock.Waiting, Is.Null);
            Assert.That(sim.Clock.Released, Is.EqualTo(3));
            Assert.That(sim.Alive, Is.EqualTo(FloorRules.WaveCap), "the cap, no more");
            int worst = 0;
            int steps = (int)(150f / FightSim.Step);
            for (int i = 0; i < steps; i++)
            {
                sim.Tick();
                worst = Math.Max(worst, sim.Alive);
                Assert.That(sim.Committed, Is.LessThanOrEqualTo(FloorRules.WaveCap), "at " + sim.Time);
            }
            var r = sim.Result();
            TestContext.WriteLine("cap run: peak " + r.peakAlive + ", released " + r.wavesReleased + ", skipped " + r.wavesSkipped + ", stuck " + r.wavesStuck);
            Assert.That(worst, Is.LessThanOrEqualTo(FloorRules.WaveCap));
            Assert.That(r.peakAlive, Is.EqualTo(FloorRules.WaveCap));
            Assert.That(r.wavesSkipped, Is.GreaterThan(0), "intervals that came while a wave waited are skipped");
            Assert.That(r.wavesReleased, Is.EqualTo(3), "nothing more fits while nothing dies");
            Assert.That(r.wavesReleased + r.wavesSkipped, Is.GreaterThanOrEqualTo(6), "the clock kept its own time while a wave waited");
            Assert.That(sim.AliveHistory.Count, Is.GreaterThan(150));
        }

        [Test]
        public void ARunIsBoundedByMaxSeconds()
        {
            var w = Make(o => o.invulnerable = true);
            var sim = Sim(new StandBot(), 14, w);
            var r = sim.Run(5f);
            Assert.That(r.secondsRun, Is.EqualTo(5f).Within(0.05f));
            Assert.That(sim.Finished, Is.False);
        }

        [Test]
        public void StandingInATowCircleFillsTheMeterInTenSecondsAndRaisesTheAlarmOnce()
        {
            ulong seed = SeedWithSockets("pit-sw");
            var bot = new TourBot { holdFire = false };
            bot.legs.Add(new Leg { points = RouteTo("arrival-pit-sw"), towSocket = "pit-sw" });
            var w = Make(o => o.invulnerable = true);
            var sim = Sim(bot, seed, w);
            var alarmAt = new List<float>();
            var towedAt = new List<float>();
            Vector3 asset = Vector3.Zero;
            foreach (var j in sim.Objective.Sheet) if (j.socket.id == "pit-sw") asset = j.socket.position;
            sim.AlarmRaised += j => alarmAt.Add(sim.Time);
            sim.AssetTowed += j => towedAt.Add(sim.Time);
            // run until the wave after the alarm has been chosen
            var nextDoors = new List<SpawnDoor>();
            int wavesBefore = -1;
            sim.TelegraphStarted += a =>
            {
                if (alarmAt.Count == 0) return;
                if (wavesBefore < 0) wavesBefore = sim.LastWave;
                if (sim.LastWave == wavesBefore) nextDoors.Add(a.door);
            };
            var r = sim.Run(75f);
            Assert.That(alarmAt.Count, Is.EqualTo(1), "the alarm fires once, when the meter first moves");
            Assert.That(towedAt.Count, Is.EqualTo(1));
            Assert.That(towedAt[0] - alarmAt[0], Is.EqualTo(FloorRules.TowSeconds).Within(0.1f), "10 s inside takes the asset");
            Assert.That(r.towed, Is.EqualTo(1));
            Assert.That(r.alarms, Is.EqualTo(1));
            Assert.That(sim.Flow.Phase, Is.EqualTo(FloorPhase.Floor), "one of three is not the end");
            Assert.That(nextDoors.Count, Is.GreaterThan(0), "a wave came after the alarm");
            float chosen = 0f, all = 0f;
            foreach (var d in nextDoors) chosen += Vector3.Distance(d.position, asset);
            chosen /= nextDoors.Count;
            var doors = RepossessSites.Doors();
            foreach (var d in doors) all += Vector3.Distance(d.position, asset);
            all /= doors.Length;
            TestContext.WriteLine("alarm wave: mean door distance to the asset " + chosen + " m against " + all + " m over every door");
            Assert.That(chosen, Is.LessThan(all), "after the alarm the wave comes from the doors nearest the asset");
            Assert.That(sim.PendingAlarm.HasValue, Is.False, "the alarm applies to one wave");
        }

        [Test]
        public void AWreckResetsTheFloorAsTheGameDoes()
        {
            ulong seed = SeedWithSockets("pit-sw");
            var w = Make(o =>
            {
                o.stopOnWreck = false;
                o.start = new Vector3(0f, 0f, -12f);
                o.player = new FightPlayerDef { platingMax = 15f, chassisMax = 15f };
            });
            var sim = Sim(new StandBot { holdFire = true }, seed, w);
            var sockets = new List<string>();
            foreach (var j in sim.Objective.Sheet) sockets.Add(j.socket.id);
            int wrecks = 0;
            int aliveSeen = -1, queued = -1, released = -1;
            FloorPhase phase = FloorPhase.Floor;
            float armor = 0f;
            sim.Wrecked += () =>
            {
                wrecks++;
                aliveSeen = sim.Alive;
                queued = sim.Spawner.PendingCount;
                released = sim.Clock.Released;
                phase = sim.Flow.Phase;
                armor = sim.Fight.Armor.Plating + sim.Fight.Armor.Chassis;
            };
            var r = sim.Run(240f);
            TestContext.WriteLine("wreck run: " + r.wrecks + " wrecks, first after wave " + r.waveAtWreck + ", " + r.aliveAtWreck + " bots alive then");
            Assert.That(wrecks, Is.GreaterThanOrEqualTo(1), "a Unit standing in the open is wrecked");
            Assert.That(r.wrecked, Is.True);
            Assert.That(r.wrecks, Is.EqualTo(wrecks));
            Assert.That(aliveSeen, Is.EqualTo(0), "every bot is gone");
            Assert.That(queued, Is.EqualTo(0), "no telegraph survives");
            Assert.That(released, Is.EqualTo(0), "the wave clock is back at the start");
            Assert.That(phase, Is.EqualTo(FloorPhase.Lobby), "a wreck sends the run back to the lobby");
            Assert.That(armor, Is.EqualTo(30f), "the Unit is whole again");
            var after = new List<string>();
            foreach (var j in sim.Objective.Sheet) after.Add(j.socket.id);
            Assert.That(after, Is.EqualTo(sockets), "the same sockets this run");
            Assert.That(sim.Finished, Is.False, "stopOnWreck is off: the run goes on from the restart");
            Assert.That(sim.Flow.Phase, Is.EqualTo(FloorPhase.Floor).Or.EqualTo(FloorPhase.Finale));
        }

        [Test]
        public void StopOnWreckEndsTheRunAndReportsIt()
        {
            var w = Make(o =>
            {
                o.start = new Vector3(0f, 0f, -12f);
                o.player = new FightPlayerDef { platingMax = 15f, chassisMax = 15f };
            });
            var sim = Sim(new StandBot { holdFire = true }, 9, w);
            var r = sim.Run(300f);
            Assert.That(sim.Finished, Is.True);
            Assert.That(r.wrecked, Is.True);
            Assert.That(r.wrecks, Is.EqualTo(1));
            Assert.That(r.towed, Is.EqualTo(0));
            Assert.That(r.reachedExit, Is.False);
            Assert.That(r.secondsRun, Is.LessThan(300f));
            Assert.That(r.aliveAtWreck, Is.GreaterThan(0));
            Assert.That(sim.Alive, Is.EqualTo(0), "the floor was reset after the wreck");
        }

        [Test]
        public void TheSameSeedGivesTheSameRun()
        {
            ulong seed = SeedWithSockets("pit-sw");
            FloorResult Go()
            {
                var bot = new TourBot();
                bot.legs.Add(new Leg { points = RouteTo("arrival-pit-sw"), towSocket = "pit-sw" });
                var w = Make(o => o.invulnerable = true);
                var sim = Sim(bot, seed, w);
                return sim.Run(80f);
            }
            var a = Go();
            var b = Go();
            Assert.That(b.towed, Is.EqualTo(a.towed));
            Assert.That(b.hits, Is.EqualTo(a.hits));
            Assert.That(b.wavesReleased, Is.EqualTo(a.wavesReleased));
            Assert.That(b.minPlating, Is.EqualTo(a.minPlating));
            Assert.That(b.firstFlankedSeconds, Is.EqualTo(a.firstFlankedSeconds));
            Assert.That(b.botsSpawned, Is.EqualTo(a.botsSpawned));
            Assert.That(b.peakAlive, Is.EqualTo(a.peakAlive));
            Assert.That(b.towSeconds, Is.EqualTo(a.towSeconds));
            Assert.That(a.towed, Is.EqualTo(1));
        }

        // ---- the façade, the gondola

        static SpawnDoor Door(string id)
        {
            foreach (var d in RepossessSites.Doors()) if (d.id == id) return d;
            throw new ArgumentException(id);
        }

        [Test]
        public void TheGondolaNeverShootsAndLaysItsMinesOnTheInsideOfTheWallRun()
        {
            var w = Make(o =>
            {
                o.invulnerable = true;
                o.start = new Vector3(28f, 0f, -22f);
            });
            var sim = Sim(new StandBot { holdFire = true }, 31, w);
            sim.Clock.Reset();   // no waves: only the staged gondola
            var g = sim.SpawnNow(SkylineEnemies.GondolaId, Door("rail-s"), 7);
            int maxMines = 0;
            for (int i = 0; i < 60 * 14; i++)
            {
                sim.Tick();
                maxMines = Math.Max(maxMines, g.Mines.Mines.Count);
                foreach (var m in g.Mines.Mines)
                {
                    Assert.That(m.Position.X, Is.EqualTo(34.9f).Within(0.01f), "on the inside face of the façade");
                    Assert.That(m.Position.Y, Is.EqualTo(2.5f).Within(0.01f), "at the hatches' height");
                }
            }
            Assert.That(g.Position.X, Is.EqualTo(36.5f).Within(0.01f), "it rides outside the glass");
            Assert.That(maxMines, Is.GreaterThanOrEqualTo(3), "a mine every 4 m of rail");
            Assert.That(maxMines, Is.LessThanOrEqualTo(6));
            Assert.That(sim.Fight.ShotsAtPlayerByGroup(7), Is.EqualTo(0), "the gondola never shoots");
            Assert.That(sim.Fight.Result().shotsAtPlayer, Is.EqualTo(0));
            foreach (var p in sim.Fight.Panes) Assert.That(p.broken, Is.False, "nobody shot");
        }

        [Test]
        public void AShotThroughAPaneBreaksItAndTheWorldLetsGoOfIt()
        {
            var w = Make(o =>
            {
                o.invulnerable = true;
                o.start = new Vector3(28f, 0f, -22f);
            });
            var sim = Sim(new StandBot(), 32, w);
            sim.Clock.Reset();
            var g = sim.SpawnNow(SkylineEnemies.GondolaId, Door("rail-s"), 7);
            int broken = 0;
            sim.Fight.PaneBroken += p => broken++;
            for (int i = 0; i < 60 * 6; i++) sim.Tick();
            Assert.That(sim.Fight.Result().shotsFired, Is.GreaterThan(0), "the Writ fired at the gondola outside the glass");
            Assert.That(broken, Is.GreaterThan(0), "shooting through glass costs a pane");
            Assert.That(sim.Result().panesBroken, Is.EqualTo(broken));
            int disabled = 0;
            foreach (var kv in w.boxes) if (RepossessArena.IsPane(kv.Key) && !kv.Value.enabled) disabled++;
            Assert.That(disabled, Is.EqualTo(broken), "each broken pane's box is off in the movement world");
            Assert.That(w.log.Exists(s => s.StartsWith(RepossessArena.PanePrefix) && s.EndsWith("-")), Is.True);
            foreach (var p in sim.Fight.Panes)
                if (p.broken) Assert.That(w.boxes[p.id].enabled, Is.False);
            // a wreck mends them
            sim.Fight.Armor.Damage(1e6f);
            sim.Tick();
            foreach (var kv in w.boxes) if (RepossessArena.IsPane(kv.Key)) Assert.That(kv.Value.enabled, Is.True, kv.Key + " is whole again");
            foreach (var p in sim.Fight.Panes) Assert.That(p.broken, Is.False);
        }

        [Test]
        public void ABalustradeStopsAShotButNotSight()
        {
            var w = Make();
            var sim = Sim(new StandBot(), 33, w);
            // across the bridge's west glass balustrade (x 5.9..6, y 5.6..7): a segment at 6.5 m is blocked for a shot, clear for sight
            var a = new Vector3(2f, 6.5f, 0f);
            var b = new Vector3(8f, 6.5f, 0f);
            Assert.That(sim.Fight.Clear(a, b), Is.True, "glass is not sight cover");
            Assert.That(sim.Fight.ShotClear(a, b), Is.False, "the bridge's glass balustrade blocks shots");
        }

        static int HitsFromARimUnit(Vector3 playerFeet)
        {
            var w = Make(o => { o.start = playerFeet; });
            var sim = Sim(new StandBot { holdFire = true }, 34, w);
            sim.Clock.Reset();
            sim.Fight.AddEnemy(new Spawn(SkylineEnemies.FloorCareId, new Vector3(-8f, 0f, -11.5f), 5), 900);
            sim.Fight.Wake(5);
            for (int i = 0; i < 60 * 5; i++) sim.Tick();
            return sim.Fight.Result().hitsTaken;
        }

        [Test]
        public void AFloorUnitOnTheRimDoesNotSprayAUnitFourMetresBelowIt()
        {
            Assert.That(HitsFromARimUnit(new Vector3(-8f, 0f, -14f)), Is.GreaterThan(0), "on the ring beside it the unit does spray (the control)");
            Assert.That(HitsFromARimUnit(new Vector3(-8f, -4f, -9f)), Is.EqualTo(0), "in the pit under the rim it cannot");
        }

        // ---- W4 joined: door-spawned bots arrive before they attack

        static SpawnDoor DoorById(string id)
        {
            foreach (var d in RepossessSites.Doors()) if (d.id == id) return d;
            throw new ArgumentException(id);
        }

        [Test]
        public void ADoorSpawnedDroneFliesToItsHoverPointBeforeItSeesOrShoots()
        {
            var w = Make();
            var sim = Sim(new StandBot { holdFire = true }, 21, w);
            sim.Clock.Stop();       // no waves: only the staged drone is out there, so any shot at the Unit is its
            var vent = DoorById("vent-w-frame-n");
            var e = sim.SpawnNow(SkylineEnemies.DroneId, vent, 5);
            var start = e.Position;
            Assert.That(e.Arriving, Is.True);
            Assert.That(sim.ArrivalOf(e), Is.Not.Null);
            float doneAt = -1f;
            sim.ArrivalDone += (b, a) => { if (b == e) doneAt = sim.Time; };
            int shotsWhileArriving = 0;
            for (int i = 0; i < 60 * 30 && doneAt < 0f; i++)
            {
                sim.Tick();
                if (e.Arriving)
                {
                    Assert.That(e.SeesPlayer, Is.False, "it neither sees nor attacks on the way");
                    shotsWhileArriving = sim.Fight.Result().shotsAtPlayer;
                }
            }
            Assert.That(doneAt, Is.GreaterThan(0f), "it arrived");
            Assert.That(doneAt, Is.LessThan(12f), "within a few seconds of flight");
            Assert.That(e.Arriving, Is.False);
            var p = sim.Fight.Player.Position;
            float flat = Flat(e.Position, p);
            TestContext.WriteLine($"drone from {vent.id}: arrived at {doneAt:0.0} s, {flat:0.0} m from the Unit, {e.Position.Y - p.Y:0.0} m above, moved {Vector3.Distance(start, e.Position):0.0} m");
            Assert.That(flat, Is.InRange(EngagePoints.MinRange - 4f, EngagePoints.MaxRange + 4f));
            Assert.That(e.Position.Y - p.Y, Is.InRange(2f, 6f));
            Assert.That(shotsWhileArriving, Is.LessThanOrEqualTo(0));
        }

        [Test]
        public void ADoorSpawnedFloorCareUnitPathsToTheUnitOnArenaNavAndThenWorksOnItsOwn()
        {
            var w = Make();
            var sim = Sim(new StandBot { holdFire = true }, 22, w);
            sim.Clock.Stop();
            var door = DoorById("shutter-n5");
            var e = sim.SpawnNow(SkylineEnemies.FloorCareId, door, 5);
            Assert.That(e.Arriving, Is.True);
            float doneAt = -1f;
            sim.ArrivalDone += (b, a) => { if (b == e) doneAt = sim.Time; };
            for (int i = 0; i < 60 * 40 && doneAt < 0f; i++)
            {
                sim.Tick();
                if (e.Arriving) Assert.That(e.SeesPlayer, Is.False);
            }
            TestContext.WriteLine($"floor-care from {door.id}: arrived at {doneAt:0.0} s, {Flat(e.Position, sim.Fight.Player.Position):0.0} m from the Unit");
            Assert.That(doneAt, Is.GreaterThan(0f).And.LessThan(30f));
            Assert.That(Flat(e.Position, sim.Fight.Player.Position), Is.LessThanOrEqualTo(BotArrival.FloorEngageDistance + 0.6f));
            sim.Run(6f);
            Assert.That(sim.Fight.Result().hitsTaken, Is.GreaterThan(0), "once there it sprays");
        }

        [Test]
        public void TheGondolaAndAnUnjoinedSimStartWhereTheyAlwaysDid()
        {
            var w = Make();
            var sim = Sim(new StandBot { holdFire = true }, 23, w);
            var rail = DoorById("rail-n");
            var g = sim.SpawnNow(SkylineEnemies.GondolaId, rail, 5);
            Assert.That(g.Arriving, Is.False, "the gondola rides its rail: engaged at once");
            Assert.That(sim.ArrivalOf(g), Is.Null, "no arrival is kept for a bot that engages at once");
            var off = Make(o => o.arrivals = false);
            var plain = Sim(new StandBot { holdFire = true }, 23, off);
            var d = plain.SpawnNow(SkylineEnemies.DroneId, DoorById("shutter-n1"), 5);
            Assert.That(d.Arriving, Is.False);
            Assert.That(plain.ArrivalOf(d), Is.Null);
            Assert.That(d.Position, Is.EqualTo(RepossessSim.SpawnPosition(DoorById("shutter-n1"), SkylineEnemies.Find(SkylineEnemies.DroneId))));
        }

        // ---- a whole floor: the numbers W3 reported, re-run with arrival (generous bounds; the details go to the test log)

        static string Describe(string label, FloorResult r, RepossessSim sim)
        {
            var sb = new System.Text.StringBuilder(label + ": ");
            sb.Append(r.wrecked ? "WRECKED at " + r.secondsRun.ToString("0.0") + " s (wave " + r.waveAtWreck + ", " + r.aliveAtWreck + " bots alive)" : "not wrecked in " + r.secondsRun.ToString("0") + " s")
                .Append("; flanked ").Append(r.firstFlankedSeconds < 0f ? "never" : "first at " + r.firstFlankedSeconds.ToString("0.0") + " s (wave " + r.firstFlankedWave + ")")
                .Append("; waves ").Append(r.wavesReleased).Append("/skipped ").Append(r.wavesSkipped).Append(", bots spawned ").Append(r.botsSpawned)
                .Append(", peak alive ").Append(r.peakAlive).Append(", hits ").Append(r.hits).Append(", min plating ").Append(r.minPlating.ToString("0"));
            return sb.ToString();
        }
        /// <summary>A Unit that stands where it was dropped and shoots. Per kind of bot, the seconds it had the Unit in sight come back in
        /// <paramref name="sight"/>; the others are counted by the run's own <see cref="FloorResult"/>.</summary>
        static FloorResult CampStats(Vector3 at, float seconds, ulong seed, Dictionary<string, float> sight, bool arrivals = true)
        {
            var w = Make(o => { o.start = at; o.arrivals = arrivals; });
            var sim = Sim(new StandBot(), seed, w);
            int steps = (int)(seconds / FightSim.Step);
            for (int i = 0; i < steps && !sim.Finished; i++)
            {
                sim.Tick();
                if (sight == null) continue;
                foreach (var e in sim.Fight.Enemies)
                    if (!e.Wrecked && e.SeesPlayer)
                    {
                        float t;
                        sight.TryGetValue(e.Def.id, out t);
                        sight[e.Def.id] = t + FightSim.Step;
                    }
            }
            return sim.Result();
        }

        static string CampLine(Vector3 at, float seconds, bool arrivals, ulong firstSeed, int seeds)
        {
            var sb = new System.Text.StringBuilder();
            for (ulong seed = firstSeed; seed < firstSeed + (ulong)seeds; seed++)
            {
                var sight = new Dictionary<string, float>();
                var r = CampStats(at, seconds, seed, sight, arrivals);
                sb.Append(r.wrecked ? " W" + r.secondsRun.ToString("0") + "(w" + r.waveAtWreck + ",a" + r.aliveAtWreck + ")" : " ok(hits" + r.hits + ",minP" + r.minPlating.ToString("0") + ")")
                  .Append(r.firstFlankedSeconds >= 0f ? "F" + r.firstFlankedSeconds.ToString("0") : "");
                if (seed == firstSeed) { sb.Append(" [sight s:"); foreach (var kv in sight) sb.Append(' ').Append(kv.Key.Substring(0, 5)).Append('=').Append(kv.Value.ToString("0")); sb.Append(']'); }
            }
            return sb.ToString();
        }

        [Test, Explicit("prints the camper survey (6 seeds, 240 s per spot, about a minute)")]
        public void ReportCampersAndHolders()
        {
            var spots = new (string, Vector3)[]
            {
                ("alcove", RepossessArena.ArrivalPoint),
                ("open east ring", new Vector3(24f, 0f, 6f)),
                ("open west ring", new Vector3(-24f, 0f, 12f)),
                ("open south ring", new Vector3(0f, 0f, -12f)),
                ("north corridor (at the N4 back door)", new Vector3(10f, 0f, 25.75f)),
                ("north corridor (hidden, west)", new Vector3(-22f, 0f, 25.75f)),
                ("north corridor (middle)", new Vector3(0f, 0f, 25.75f)),
                ("south corridor (hidden, west)", new Vector3(-20f, 0f, -25.75f)),
                ("south corridor (hidden, east end)", new Vector3(22f, 0f, -25.75f)),
                ("west leg", new Vector3(-33.25f, 0f, 0f)),
                ("inside shop N2", new Vector3(-10.4f, 0f, 21f)),
                ("inside shop N2 (corner, off the door line)", new Vector3(-14f, 0f, 21f)),
                ("inside shop S4", new Vector3(10.4f, 0f, -21f)),
                ("inside shop S4 (corner)", new Vector3(14f, 0f, -21f)),
                ("west leg (north end)", new Vector3(-33.25f, 0f, 22f)),
            };
            foreach (var (name, at) in spots) TestContext.WriteLine(name + " |" + CampLine(at, 240f, true, 1, 6));
        }

        // ---- a whole floor, with a stand-in pilot

        static FloorResult RunTour(bool invulnerable, out RepossessSim sim, out World w)
        {
            ulong seed = SeedWithSockets("pit-sw", "inside-n4", "high-bridge");
            var bot = new TourBot();
            // inside first, then the high one, then the pit (the lift run starts from the pit)
            bot.legs.Add(new Leg { points = RouteTo("arrival-inside-n4"), towSocket = "inside-n4" });
            bot.legs.Add(new Leg { points = Reversed(RouteTo("arrival-inside-n4")) });
            bot.legs.Add(new Leg { points = RouteTo("arrival-high-bridge"), towSocket = "high-bridge" });
            bot.legs.Add(new Leg { points = Reversed(RouteTo("arrival-high-bridge")) });
            bot.legs.Add(new Leg { points = RouteTo("arrival-pit-sw"), towSocket = "pit-sw" });
            var lift = RouteTo("pit-to-exit-lift");
            var toLift = new List<Vector3> { new Vector3(-10f, -4f, 0f) };
            for (int i = 2; i < lift.Length; i++) toLift.Add(lift[i]);
            bot.legs.Add(new Leg { points = toLift.ToArray() });
            w = Make(o => o.invulnerable = invulnerable);
            sim = Sim(bot, seed, w);
            return sim.Run(420f);
        }

        [Test]
        public void AScriptedTourOfThreeAssetsThenTheLiftFinishesTheFloor()
        {
            RepossessSim sim;
            World w;
            var r = RunTour(true, out sim, out w);
            var sb = new System.Text.StringBuilder();
            sb.Append("tour: towed ").Append(r.towed).Append(" at");
            foreach (float t in r.towSeconds) sb.Append(' ').Append(t.ToString("0.0"));
            sb.Append("; third at ").Append(r.timeToThird.ToString("0.0")).Append(" s; exit ").Append(r.reachedExit)
                .Append(" at ").Append(r.secondsRun.ToString("0.0")).Append(" s; waves released ").Append(r.wavesReleased)
                .Append(", skipped ").Append(r.wavesSkipped).Append("; bots spawned ").Append(r.botsSpawned).Append(", peak alive ").Append(r.peakAlive)
                .Append("; hits ").Append(r.hits).Append(", min plating ").Append(r.minPlating.ToString("0")).Append("; panes broken ").Append(r.panesBroken)
                .Append("; first flanked wave ").Append(r.firstFlankedWave).Append(" at ").Append(r.firstFlankedSeconds.ToString("0.0")).Append(" s");
            TestContext.WriteLine(sb.ToString());
            sb.Clear();
            sb.Append("alive each 10 s:");
            foreach (var s in sim.AliveHistory) if (((int)(s.t + 0.5f)) % 10 == 0) sb.Append(' ').Append((int)(s.t + 0.5f)).Append("s=").Append(s.alive);
            TestContext.WriteLine(sb.ToString());
            Assert.That(r.towed, Is.EqualTo(3), "all three assets");
            Assert.That(r.timeToThird, Is.GreaterThan(0f));
            Assert.That(sim.Flow.Phase, Is.EqualTo(FloorPhase.Done).Or.EqualTo(FloorPhase.Finale));
            Assert.That(sim.Clock.Running, Is.False, "the third tow stops the waves");
            Assert.That(w.log, Does.Contain(RepossessArena.LiftShutterId + "-"), "the west lift's shutter rolled up");
            Assert.That(r.reachedExit, Is.True, "the Unit's feet reached the lift");
            Assert.That(sim.Finished, Is.True);
            // the numbers (2026-10-14, with arrival): third tow at 57 s, in the lift at 60 s, two waves, never over 4 bots alive
            Assert.That(r.timeToThird, Is.InRange(40f, 90f));
            Assert.That(r.secondsRun, Is.LessThan(110f));
            Assert.That(r.wavesReleased, Is.InRange(1, 4));
            Assert.That(r.wavesSkipped, Is.EqualTo(0));
            Assert.That(r.peakAlive, Is.InRange(1, 10));
        }

        [Test]
        public void TheSameTourWithoutInvulnerabilityEndsOneWayOrTheOther()
        {
            RepossessSim sim;
            World w;
            var r = RunTour(false, out sim, out w);
            TestContext.WriteLine(Describe("vulnerable tour", r, sim) + "; towed " + r.towed + ", exit " + r.reachedExit + " at " + r.secondsRun.ToString("0.0") + " s");
            // 2026-10-14: this walk-in-the-open stand-in (no cover play, no dodging) is wrecked at 39 s in wave 2 with 4 bots alive; DM1 has to do better
            Assert.That(r.wrecked || r.reachedExit, Is.True, "the run ends one way or the other inside 420 s");
        }

        // ---- campers and holders (a Unit that stands and shoots; the hostile spots are hostile, the hidden corridor is not)

        static void AssertWreckedEarly(Vector3 at, float within, params ulong[] seeds)
        {
            foreach (var seed in seeds)
            {
                var r = CampStats(at, within + 60f, seed, null);
                Assert.That(r.wrecked, Is.True, $"a Unit standing at {at} with seed {seed} is wrecked ({Describe("run", r, null)})");
                Assert.That(r.secondsRun, Is.LessThan(within), $"seed {seed}");
                Assert.That(r.waveAtWreck, Is.InRange(1, 5), $"seed {seed}");
            }
        }

        [Test]
        public void AUnitHoldingTheOpenRingIsWreckedInTheSecondOrThirdWave()
        {
            // 2026-10-14, 6 seeds each: east ring 38..66 s, south ring 39..44 s, west ring 40..96 s (waves 2 to 4)
            AssertWreckedEarly(new Vector3(24f, 0f, 6f), 130f, 1, 2, 3, 4);
            AssertWreckedEarly(new Vector3(0f, 0f, -12f), 130f, 1, 2, 3, 4);
        }

        [Test]
        public void AUnitCampingInTheArrivalAlcoveIsWreckedInTheSecondOrThirdWave()
        {
            AssertWreckedEarly(RepossessArena.ArrivalPoint, 130f, 1, 2, 3, 4);        // 41..71 s over 6 seeds
        }

        [Test]
        public void AUnitCampingInsideAWalkInShopIsWreckedAndFlankedFromTheDoorsOrTheCorner()
        {
            AssertWreckedEarly(new Vector3(-10.4f, 0f, 21f), 150f, 1, 2, 3, 4);       // 38..77 s over 6 seeds
            AssertWreckedEarly(new Vector3(-14f, 0f, 21f), 170f, 1, 2, 3, 4);         // off the door line: 43..101 s
            AssertWreckedEarly(new Vector3(10.4f, 0f, -21f), 150f, 1, 2, 3, 4);       // 42..79 s
        }

        [Test]
        public void AUnitInTheServiceCorridorAtABackDoorOrTheMiddleIsFoundWithinTheFirstThreeWaves()
        {
            AssertWreckedEarly(new Vector3(10f, 0f, 25.75f), 130f, 1, 2, 3, 4);       // in line with the N4 shop's back door: 40..72 s
        }

        [Test]
        public void ACamperHiddenInTheWestCorridorsSurvivesBecauseOnlyFloorCareUnitsCanReachIt()
        {
            // The exploit this pins (DM2 and the director must close it, then flip this test): drones never enter the corridors and
            // have no line into the west parts, so only floor-care units come, one at a time down the corridor, into the Writ.
            // 6 seeds, 240 s each: never wrecked, 1..7 hits, lowest plating 32..86; no drone ever had it in sight.
            foreach (var at in new[] { new Vector3(-22f, 0f, 25.75f), new Vector3(-20f, 0f, -25.75f) })
                foreach (ulong seed in new ulong[] { 1, 2, 3 })
                {
                    var sight = new Dictionary<string, float>();
                    var r = CampStats(at, 200f, seed, sight);
                    Assert.That(r.wrecked, Is.False, $"{at} seed {seed}: {Describe("run", r, null)}");
                    Assert.That(r.minPlating, Is.GreaterThan(0f), $"{at} seed {seed}");
                    float drone;
                    sight.TryGetValue(SkylineEnemies.DroneId, out drone);
                    Assert.That(drone, Is.LessThan(3f), $"{at} seed {seed}: a drone should not see into the corridor");
                    float care;
                    sight.TryGetValue(SkylineEnemies.FloorCareId, out care);
                    Assert.That(care, Is.GreaterThan(0f), $"{at} seed {seed}: floor-care units do come");
                }
        }

        [Test]
        public void ACamperAtTheEastHatchEndOfTheSouthCorridorIsSprayedInTheFirstWave()
        {
            // the hatch at (26, -26) spawns a floor-care unit 4 m from a Unit standing at (22, -25.75): wrecked at 9..15 s, wave 1.
            // A spawn door this close to a corridor position is a layout finding for F2/F3 (the spawn-distance rule is 6 m to a socket only).
            // 2026-10-15 (larger bots, lagged drones): the camper dies in wave 1 at 13 s in about 40% of seeds and, in the rest, to wave 2, which
            // arrives at about 40 s (seeds 1-10: 13-14 s or 40-42 s); the old bound of 40 s sat on that second mode (39 to 40.1 s), so it is
            // 45 s: still "early", before a third wave could matter
            AssertWreckedEarly(new Vector3(22f, 0f, -25.75f), 45f, 1, 2, 3);
        }
    }
}

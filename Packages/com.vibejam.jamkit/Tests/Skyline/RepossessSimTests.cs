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
        public void WavesComeEveryThirtySecondsWithTheTablesCompositions()
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
            sim.Run(125f);
            Assert.That(firstSpawn.Count, Is.EqualTo(5), "waves 1 to 5 inside 125 s");
            for (int n = 1; n <= 5; n++)
                Assert.That(firstSpawn[n], Is.EqualTo((n - 1) * 30f + 2f).Within(0.1f), "wave " + n);
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

        // ---- a whole floor, with a stand-in pilot

        [Test]
        public void AScriptedTourOfThreeAssetsThenTheLiftFinishesTheFloor()
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
            var w = Make(o => o.invulnerable = true);
            var sim = Sim(bot, seed, w);
            var r = sim.Run(420f);
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
        }
    }
}

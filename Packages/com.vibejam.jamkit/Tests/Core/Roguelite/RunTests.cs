using System.Collections.Generic;
using JamKit.Roguelite;
using NUnit.Framework;

namespace JamKit.Tests
{
    public class RunTests
    {
        static string Describe(RunPlan plan)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var f in plan.floors)
            {
                foreach (var s in f.steps) sb.Append(string.Join("/", s.options)).Append(' ');
                sb.Append("| ");
            }
            return sb.ToString();
        }

        [Test]
        public void RulesHoldAcross500Seeds()
        {
            var configs = new[]
            {
                new RunConfig(),
                new RunConfig { branching = false },
                new RunConfig { restBeforeBoss = false, roomsPerFloorMin = 2, roomsPerFloorMax = 4 },
                new RunConfig { eliteWeight = 1000f, branchChance = 1f }, // stress the elite rule
                new RunConfig { roomsPerFloorMin = 0, roomsPerFloorMax = 1, floors = 5 }, // bad numbers get repaired
            };
            foreach (var config in configs)
                for (ulong seed = 0; seed < 500; seed++)
                {
                    var plan = RunGenerator.Generate(config, new Rng(seed));
                    var errors = plan.Validate(config);
                    Assert.That(errors, Is.Empty, $"seed {seed}: {Describe(plan)}");
                }
        }

        [Test]
        public void BranchingActuallyBranchesAndElitesStillAppear()
        {
            var config = new RunConfig();
            bool sawChoice = false, sawElite = false;
            for (ulong seed = 0; seed < 50; seed++)
                foreach (var f in RunGenerator.Generate(config, new Rng(seed)).floors)
                    foreach (var s in f.steps)
                    {
                        sawChoice |= s.options.Count == 2;
                        sawElite |= s.Contains(RoomType.Elite);
                    }
            Assert.That(sawChoice, Is.True);
            Assert.That(sawElite, Is.True);
        }

        [Test]
        public void ValidateCatchesBrokenPlans()
        {
            var config = new RunConfig { floors = 1, roomsPerFloorMin = 4, roomsPerFloorMax = 4 };
            var plan = new RunPlan();
            var floor = new FloorPlan();
            foreach (var t in new[] { RoomType.Elite, RoomType.Elite, RoomType.Shop, RoomType.Boss })
                floor.steps.Add(new RunStep { options = new List<RoomType> { t } });
            plan.floors.Add(floor);
            var errors = plan.Validate(config);
            Assert.That(errors, Has.Some.Contains("first room"));
            Assert.That(errors, Has.Some.Contains("Elite twice"));
            Assert.That(errors, Has.Some.Contains("Rest"));
        }

        [Test]
        public void GenerationIsDeterministicPerSeed()
        {
            var config = new RunConfig();
            string a = Describe(RunGenerator.Generate(config, new Rng(42)));
            string b = Describe(RunGenerator.Generate(config, new Rng(42)));
            string c = Describe(RunGenerator.Generate(config, new Rng(43)));
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a, Is.Not.EqualTo(c));
        }

        [Test]
        public void RunStateWalksFloorsToCompletion()
        {
            var config = new RunConfig { floors = 2 };
            var run = RunState.Begin(config, 7);
            Assert.That(run.CurrentRoom, Is.EqualTo(RoomType.Combat));
            Assert.That(run.IsBossRoom, Is.False);
            int bosses = 0, guard = 0;
            while (!run.IsComplete && guard++ < 100)
            {
                if (run.IsBossRoom) bosses++;
                int doors = run.NextOptions.Count;
                Assert.That(run.Advance(System.Math.Max(1, doors)), Is.False, "out-of-range door is refused");
                Assert.That(run.Advance(-1), Is.False);
                Assert.That(run.Advance(doors > 1 ? 1 : 0), Is.True);
            }
            Assert.That(run.IsComplete, Is.True);
            Assert.That(bosses, Is.EqualTo(2));
            Assert.That(run.Advance(0), Is.False);
            Assert.That(run.IsBossRoom, Is.False);
        }

        [Test]
        public void AdvancePicksTheChosenDoor()
        {
            var config = new RunConfig { branchChance = 1f };
            var run = RunState.Begin(config, 3);
            var next = run.NextOptions;
            Assert.That(next.Count, Is.EqualTo(2));
            var second = next[1];
            run.Advance(1);
            Assert.That(run.CurrentRoom, Is.EqualTo(second));
            Assert.That(run.step, Is.EqualTo(1));
            Assert.That(run.choices, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void RestoreRebuildsTheSamePlanAndRoomSeedsAreStable()
        {
            var config = new RunConfig();
            var run = RunState.Begin(config, 555);
            run.Advance(0);
            ulong roomSeed = run.RoomSeed();
            var loaded = new RunState { seed = run.seed, floor = run.floor, step = run.step, choice = run.choice };
            loaded.Restore(config);
            Assert.That(Describe(loaded.Plan), Is.EqualTo(Describe(run.Plan)));
            Assert.That(loaded.CurrentRoom, Is.EqualTo(run.CurrentRoom));
            Assert.That(loaded.RoomSeed(), Is.EqualTo(roomSeed));
            run.Advance(0);
            Assert.That(run.RoomSeed(), Is.Not.EqualTo(roomSeed));
        }

        [Test]
        public void CurrencyAndTime()
        {
            var run = RunState.Begin(new RunConfig(), 1);
            run.currency = 10;
            Assert.That(run.TrySpend(15), Is.False);
            Assert.That(run.TrySpend(4), Is.True);
            Assert.That(run.currency, Is.EqualTo(6));
            run.Tick(1.5f);
            Assert.That(run.elapsedTime, Is.EqualTo(1.5f));
        }
    }
}

using System;
using System.Collections.Generic;

namespace JamKit.Roguelite
{
    /// <summary>
    /// Where the player is in a run and what they've earned. Public fields so it can be saved as is;
    /// the plan isn't saved, it's rebuilt from the seed by <see cref="Restore"/> (generation is deterministic).
    /// Call <see cref="Advance"/> after a room is cleared, with the door the player picked.
    /// </summary>
    [Serializable]
    public class RunState
    {
        public ulong seed;
        public int floor;
        public int step;
        /// <summary>Which option of the current step the player is in.</summary>
        public int choice;
        public int currency;
        public int score;
        public float elapsedTime;
        /// <summary>Every door picked so far (one per step after the first of each floor), for replays and stats.</summary>
        public List<int> choices = new List<int>();
        public bool complete;

        [NonSerialized] RunPlan _plan;

        public RunPlan Plan => _plan;

        public RunState() { }

        public RunState(RunPlan plan, ulong seed)
        {
            _plan = plan ?? throw new ArgumentNullException(nameof(plan));
            this.seed = seed;
            complete = plan.floors.Count == 0;
        }

        /// <summary>Generate the plan from <paramref name="seed"/> and stand in the first room.</summary>
        public static RunState Begin(RunConfig config, ulong seed) =>
            new RunState(RunGenerator.Generate(config, new Rng(seed)), seed);

        /// <summary>After loading a saved state: rebuild the plan with the config the run was started with.</summary>
        public void Restore(RunConfig config) => _plan = RunGenerator.Generate(config, new Rng(seed));

        public bool IsComplete => complete;

        public RoomType CurrentRoom => _plan.Step(floor, step).options[choice];

        public bool IsBossRoom => !complete && CurrentRoom == RoomType.Boss;

        public bool IsLastFloor => floor >= _plan.floors.Count - 1;

        /// <summary>The doors offered after this room; empty when the run is on its last room.</summary>
        public IReadOnlyList<RoomType> NextOptions
        {
            get
            {
                if (complete) return Array.Empty<RoomType>();
                if (step + 1 < _plan.RoomCount(floor)) return _plan.Step(floor, step + 1).options;
                if (floor + 1 < _plan.floors.Count) return _plan.Step(floor + 1, 0).options;
                return Array.Empty<RoomType>();
            }
        }

        /// <summary>
        /// Leave the current room through door <paramref name="choiceIndex"/> of the next step (0 when
        /// there's one door). Past a Boss it moves to the next floor; past the last Boss the run is complete.
        /// Returns false if the run was already complete or the index isn't a door.
        /// </summary>
        public bool Advance(int choiceIndex = 0)
        {
            if (complete) return false;
            var next = NextOptions;
            if (choiceIndex < 0 || choiceIndex >= Math.Max(1, next.Count)) return false;
            if (next.Count == 0)
            {
                complete = true;
                return true;
            }
            if (step + 1 < _plan.RoomCount(floor)) step++;
            else { floor++; step = 0; }
            choice = choiceIndex;
            choices.Add(choiceIndex);
            return true;
        }

        public void Tick(float dt)
        {
            if (!complete) elapsedTime += dt;
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0 || currency < amount) return false;
            currency -= amount;
            return true;
        }

        /// <summary>A seed for the current room's contents (enemy waves, loot), stable for this run and room
        /// no matter how many other rolls happened before.</summary>
        public ulong RoomSeed()
        {
            unchecked
            {
                ulong h = Mix(seed ^ 0x9E3779B97F4A7C15UL);
                h = Mix(h + (ulong)floor);
                h = Mix(h + (ulong)step);
                return Mix(h + (ulong)choice);
            }
        }

        static ulong Mix(ulong z) // SplitMix64 finalizer
        {
            z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
            z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
            return z ^ (z >> 31);
        }
    }
}

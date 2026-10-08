using System;
using System.Collections.Generic;

namespace JamKit.Roguelite
{
    /// <summary>Room kinds. New kinds go at the end (saved data and colour lists index by the number).
    /// Forge, Altar, Wager, Trial and Trader are the encounter rooms between battles: their weights
    /// default to 0, so a game that doesn't use them never sees them.</summary>
    public enum RoomType { Combat, Elite, Treasure, Shop, Rest, Event, Boss, Forge, Altar, Wager, Trial, Trader }

    /// <summary>
    /// Shape of a run. Room counts include the opening Combat and the Boss. Weights pick the
    /// middle rooms; 0 turns a type off. Tune it in the inspector, then run <see cref="RunPlan.Validate"/>.
    /// </summary>
    [Serializable]
    public class RunConfig
    {
        public int floors = 3;
        public int roomsPerFloorMin = 6;
        public int roomsPerFloorMax = 9;

        public float combatWeight = 50f;
        public float eliteWeight = 10f;
        public float treasureWeight = 8f;
        public float shopWeight = 8f;
        public float restWeight = 6f;
        public float eventWeight = 12f;
        public float forgeWeight;
        public float altarWeight;
        public float wagerWeight;
        public float trialWeight;
        public float traderWeight;

        /// <summary>Never Elite at two consecutive steps (whichever door the player takes).</summary>
        public bool noBackToBackElites = true;
        /// <summary>The step right before each Boss is a single Rest (the campfire before the fight).</summary>
        public bool restBeforeBoss = true;
        /// <summary>Middle steps may offer two doors instead of one.</summary>
        public bool branching = true;
        /// <summary>Chance (0..1) that a middle step offers two doors, when <see cref="branching"/> is on.</summary>
        public float branchChance = 0.4f;

        public float WeightOf(RoomType type)
        {
            switch (type)
            {
                case RoomType.Combat: return combatWeight;
                case RoomType.Elite: return eliteWeight;
                case RoomType.Treasure: return treasureWeight;
                case RoomType.Shop: return shopWeight;
                case RoomType.Rest: return restWeight;
                case RoomType.Event: return eventWeight;
                case RoomType.Forge: return forgeWeight;
                case RoomType.Altar: return altarWeight;
                case RoomType.Wager: return wagerWeight;
                case RoomType.Trial: return trialWeight;
                case RoomType.Trader: return traderWeight;
                default: return 0f; // Boss is placed, never rolled
            }
        }

        /// <summary>Fewest rooms a floor can have under these rules (Combat [+ Rest] + Boss).</summary>
        public int EffectiveMinRooms => Math.Max(roomsPerFloorMin, restBeforeBoss ? 3 : 2);

        public int EffectiveMaxRooms => Math.Max(roomsPerFloorMax, EffectiveMinRooms);
    }

    /// <summary>One point on the path: the player picks one of its 1-2 rooms.</summary>
    [Serializable]
    public class RunStep
    {
        public List<RoomType> options = new List<RoomType>();

        public bool Contains(RoomType t) => options.Contains(t);
    }

    [Serializable]
    public class FloorPlan
    {
        public List<RunStep> steps = new List<RunStep>();
    }

    /// <summary>
    /// A whole run, linear with optional forks: each step is one room or a choice of two, and every
    /// choice leads to the same next step. Jam-sized: no graph, no backtracking, easy to draw as a row of doors.
    /// Serializable so a run can be inspected, but a save only needs the seed and the choices made.
    /// </summary>
    [Serializable]
    public class RunPlan
    {
        public List<FloorPlan> floors = new List<FloorPlan>();

        public int RoomCount(int floor) => floors[floor].steps.Count;

        public RunStep Step(int floor, int step) => floors[floor].steps[step];

        /// <summary>Every rule this plan breaks, in plain words. Empty means valid.</summary>
        public List<string> Validate(RunConfig config)
        {
            var errors = new List<string>();
            if (floors.Count != config.floors)
                errors.Add($"Expected {config.floors} floors, got {floors.Count}");
            int min = config.EffectiveMinRooms, max = config.EffectiveMaxRooms;
            for (int f = 0; f < floors.Count; f++)
            {
                var steps = floors[f].steps;
                int n = steps.Count;
                if (n < min || n > max)
                {
                    errors.Add($"Floor {f}: {n} rooms, outside {min}..{max}");
                    if (n < 2) continue;
                }
                for (int s = 0; s < n; s++)
                {
                    var o = steps[s].options;
                    if (o.Count < 1 || o.Count > 2) errors.Add($"Floor {f} step {s}: {o.Count} options (1-2 allowed)");
                    else if (o.Count == 2 && !config.branching) errors.Add($"Floor {f} step {s}: a choice with branching off");
                    if (o.Count == 2 && o[0] == o[1]) errors.Add($"Floor {f} step {s}: both doors are {o[0]}");
                    bool isLast = s == n - 1;
                    if (!isLast && o.Contains(RoomType.Boss)) errors.Add($"Floor {f} step {s}: Boss before the end");
                    if (s > 0 && config.noBackToBackElites && o.Contains(RoomType.Elite) && steps[s - 1].Contains(RoomType.Elite))
                        errors.Add($"Floor {f} steps {s - 1}-{s}: Elite twice in a row");
                }
                if (!IsOnly(steps[0], RoomType.Combat)) errors.Add($"Floor {f}: first room isn't a single Combat");
                if (!IsOnly(steps[n - 1], RoomType.Boss)) errors.Add($"Floor {f}: last room isn't a single Boss");
                if (config.restBeforeBoss && !IsOnly(steps[n - 2], RoomType.Rest))
                    errors.Add($"Floor {f}: no Rest right before the Boss");
            }
            return errors;
        }

        static bool IsOnly(RunStep step, RoomType t) => step.options.Count == 1 && step.options[0] == t;
    }

    /// <summary>Builds a <see cref="RunPlan"/> from a config. Same config + same seed = same plan.</summary>
    public static class RunGenerator
    {
        static readonly RoomType[] Rollable =
        {
            RoomType.Combat, RoomType.Elite, RoomType.Treasure, RoomType.Shop, RoomType.Rest, RoomType.Event,
            RoomType.Forge, RoomType.Altar, RoomType.Wager, RoomType.Trial, RoomType.Trader,
        };

        public static RunPlan Generate(RunConfig config, Rng rng)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            var plan = new RunPlan();
            int min = config.EffectiveMinRooms, max = config.EffectiveMaxRooms;
            var weights = new List<float>(Rollable.Length);
            for (int f = 0; f < Math.Max(0, config.floors); f++)
            {
                var floor = new FloorPlan();
                int n = rng.Range(min, max + 1);
                int lastMiddle = config.restBeforeBoss ? n - 3 : n - 2;
                floor.steps.Add(Single(RoomType.Combat));
                for (int s = 1; s <= lastMiddle; s++)
                {
                    bool prevElite = floor.steps[s - 1].Contains(RoomType.Elite);
                    var step = new RunStep();
                    int doors = config.branching && rng.Chance(config.branchChance) ? 2 : 1;
                    for (int d = 0; d < doors; d++)
                    {
                        RoomType? t = Roll(config, rng, weights, step, prevElite && config.noBackToBackElites);
                        if (t.HasValue) step.options.Add(t.Value);
                    }
                    if (step.options.Count == 0) step.options.Add(RoomType.Combat); // every type weighted 0
                    floor.steps.Add(step);
                }
                if (config.restBeforeBoss) floor.steps.Add(Single(RoomType.Rest));
                floor.steps.Add(Single(RoomType.Boss));
                plan.floors.Add(floor);
            }
            return plan;
        }

        static RoomType? Roll(RunConfig config, Rng rng, List<float> weights, RunStep step, bool banElite)
        {
            weights.Clear();
            for (int i = 0; i < Rollable.Length; i++)
            {
                var t = Rollable[i];
                bool banned = step.Contains(t) || (banElite && t == RoomType.Elite);
                weights.Add(banned ? 0f : config.WeightOf(t));
            }
            int idx = rng.WeightedIndex(weights);
            return idx < 0 ? (RoomType?)null : Rollable[idx];
        }

        static RunStep Single(RoomType t)
        {
            var s = new RunStep();
            s.options.Add(t);
            return s;
        }
    }
}

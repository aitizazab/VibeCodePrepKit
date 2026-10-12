using System;
using System.Collections.Generic;

namespace JamKit.DeepBlock
{
    /// <summary>Where Night 1 is (docs/NIGHTS-1-2.md "Night 1: The Street").</summary>
    public enum StreetPhase { Arrival, Shop, Yard, Walt, Arguing, Climb, Top, Ending, Ended }

    /// <summary>
    /// How Walt's argument ended. A STUB until the argument agent's API arrives: the plan's outcomes, and in all of them
    /// Walt steps aside; how she treated him is kept for the ending (<see cref="LarchStreetStory.WaltOutcomeKey"/>).
    /// </summary>
    public enum WaltOutcome { None, Persuaded, HeardOut, Pressed }

    public sealed class StreetLine
    {
        public string speaker, text;
        public float seconds;
        public StreetLine(string speaker, string text, float seconds) { this.speaker = speaker; this.text = text; this.seconds = seconds; }
    }

    /// <summary>
    /// Night 1's words. EVERY LINE IS A PLACEHOLDER (marked so on screen): the dialogue is the creator's. The lines quoted
    /// from docs/NIGHTS-1-2.md (Mags's "You're lighter in here.", Walt's "Let him have it", the deductions, Mags's last line)
    /// are main's placeholders from the plan.
    /// </summary>
    public static class LarchStreetLines
    {
        public const string Placeholder = "[placeholder] ";
        public const string Mags = "Mags", Nadia = "Nadia", Walt = "Walt", Neighbour = "Mrs. Lindqvist";

        static StreetLine L(string who, string text, float s = 3.2f) => new StreetLine(who, Placeholder + text, s);

        public static readonly StreetLine[] Arrival =
        {
            L(Mags, "You're lighter in here."),
            L(Mags, "The creek. Jump, then jump again in the air.", 3.6f),
        };
        public static readonly StreetLine[] GarageDoor = { L(Mags, "The door's half up. Run at it and crouch: slide under.", 3.6f) };
        public static readonly StreetLine[] WaltAlive = { L(Nadia, "Mr. Abernathy. Waving at me like it's any Saturday.", 3.4f) };
        public static readonly StreetLine[] WaltDead = { L(Nadia, "Walt died in 2015. This is memory.", 3.4f) };
        public static readonly StreetLine[] WaltBlocks =
        {
            L(Walt, "Your brother's having a good day. Let him have it. Come back tomorrow.", 4f),
        };
        public const string ArgumentStub = "PLACEHOLDER: Walt's argument (the argument agent's battle goes here). How does it end?   [1] persuaded   [2] heard out   [3] pressed";
        public static readonly StreetLine[] WaltAsidePersuaded = { L(Walt, "Go on, then. He's at the top. He always liked the view.", 3.6f) };
        public static readonly StreetLine[] WaltAsideHeardOut = { L(Walt, "I won't stop you. I won't help you either.", 3.4f) };
        public static readonly StreetLine[] WaltAsidePressed = { L(Walt, "...Fine. Go.", 2.4f) };
        public static readonly StreetLine[] FourWaysUp = { L(Mags, "The house at the top. Road, stairs, roofs or the creek: pick one.", 3.8f) };
        public static readonly StreetLine[] Paper = { L(Nadia, "Saturday's paper. Still in its plastic.", 3f) };
        public const string LoopLine = "Lovely Saturday for it!";
        public static readonly StreetLine[] LoopHeard = { L(Nadia, "She said that already. Word for word.", 3f) };
        public static readonly StreetLine[] SameSaturday = { L(Nadia, "He's living one Saturday, over and over.", 3.4f) };
        public static readonly StreetLine[] Ledger = { L(Nadia, "Walt's ledger. Every line says Saturday.", 3f) };
        public static readonly StreetLine[] EliasGlimpsed = { L(Nadia, "Eli!", 1.6f) };
        public static readonly StreetLine[] Ending = { L(Mags, "His heart's up. That's enough for tonight. You found the house.", 4.2f) };

        public static StreetLine[] WaltAside(WaltOutcome o) =>
            o == WaltOutcome.Persuaded ? WaltAsidePersuaded : o == WaltOutcome.Pressed ? WaltAsidePressed : WaltAsideHeardOut;
    }

    /// <summary>
    /// Night 1's story state: the phase, what Nadia knows (memories, and the deductions two of them make), and how Walt's
    /// argument ended. The Unity side keeps every entry learnt in the save's shared dream journal (the house's
    /// <c>DreamJournal</c>, save entry "vh_dream_journal") so Night 2 and the chain read it (Night 2 looks for
    /// <see cref="MemSaturdayPaper"/>). The deductions make themselves once both halves are known (the dream scene has no
    /// journal screen; the waking journal's pair table is the same two pairs).
    /// </summary>
    public sealed class LarchStreetStory
    {
        // Entry ids (the waking journal's where they exist: fact_effects, mem_walt_alive, ded_walt_dead, ded_same_saturday).
        public const string FactEffects = "fact_effects";
        public const string MemWaltAlive = "mem_walt_alive";
        public const string MemSaturdayPaper = "mem_saturday_paper";
        public const string MemNeighboursLoop = "mem_neighbours_loop";
        public const string MemWaltLedger = "mem_walt_ledger";
        public const string MemTheHouse = "mem_the_house";
        public const string DedWaltDead = "ded_walt_dead";
        public const string DedSameSaturday = "ded_same_saturday";

        /// <summary>The save entry for how Walt's argument ended ("persuaded", "heardout", "pressed").</summary>
        public const string WaltOutcomeKey = "vh_walt_night1";
        /// <summary>The save flag set when Night 1 has ended (the door shut).</summary>
        public const string NightDoneFlag = "vh_night1_done";

        public static string WaltOutcomeValue(WaltOutcome o) => o.ToString().ToLowerInvariant();

        /// <summary>The pairs: (a, b) make the result.</summary>
        public static readonly string[,] Pairs =
        {
            { FactEffects, MemWaltAlive, DedWaltDead },
            { MemSaturdayPaper, MemNeighboursLoop, DedSameSaturday },
        };

        readonly HashSet<string> _known = new HashSet<string>(StringComparer.Ordinal);
        StreetPhase _phase = StreetPhase.Arrival;
        WaltOutcome _walt;

        public StreetPhase Phase => _phase;
        public WaltOutcome Walt => _walt;
        public bool WaltAside => _walt != WaltOutcome.None;
        public bool Knows(string id) => !string.IsNullOrEmpty(id) && _known.Contains(id);
        public int KnownCount => _known.Count;

        public void Reset()
        {
            _known.Clear();
            _phase = StreetPhase.Arrival;
            _walt = WaltOutcome.None;
        }

        /// <summary>Learns an entry; any deduction it completes is learnt too and added to <paramref name="made"/> (if given).
        /// False when it was already known.</summary>
        public bool Learn(string id, List<string> made = null)
        {
            if (string.IsNullOrEmpty(id) || !_known.Add(id)) return false;
            for (int i = 0; i < Pairs.GetLength(0); i++)
            {
                string a = Pairs[i, 0], b = Pairs[i, 1], r = Pairs[i, 2];
                if ((a == id || b == id) && Knows(a) && Knows(b) && _known.Add(r)) made?.Add(r);
            }
            return true;
        }

        /// <summary>Moves the night on. Only forward: an earlier phase is ignored.</summary>
        public bool Advance(StreetPhase to)
        {
            if (to <= _phase) return false;
            if (to > StreetPhase.Arguing && to < StreetPhase.Ended && _walt == WaltOutcome.None && _phase <= StreetPhase.Arguing) return false;
            _phase = to;
            return true;
        }

        /// <summary>Walt's argument ended: he steps aside whatever the outcome; the climb begins.</summary>
        public bool EndArgument(WaltOutcome outcome)
        {
            if (outcome == WaltOutcome.None || _walt != WaltOutcome.None) return false;
            _walt = outcome;
            if (_phase < StreetPhase.Climb) _phase = StreetPhase.Climb;
            return true;
        }
    }

    /// <summary>
    /// The neighbours' loop (docs/NIGHTS-1-2.md: "the sprinkler, the dog's ball, the same line, on a timer"): one fixed
    /// 24 s Saturday afternoon, the same every time it plays. Where in it a night starts is fixed per night
    /// (<see cref="StartOffset"/>), so Night 2 replays Night 1's loop exactly by asking for night 1. Pure numbers: the Unity
    /// side moves the sprinkler, the ball and the dog from them and says the line when <see cref="LineBetween"/> says so.
    /// </summary>
    public sealed class NeighbourLoop
    {
        public const float Period = 24f;
        /// <summary>The ball leaves her hand, lands; the dog runs out to it and brings it back.</summary>
        public const float ThrowAt = 5f, LandAt = 6.4f, DogOutUntil = 8.2f, DogBackUntil = 11.5f;
        /// <summary>The line, every loop.</summary>
        public const float LineAt = 14f;
        /// <summary>The sprinkler sweeps 120° and back over the loop's period, four times.</summary>
        public const float SprinklerSweep = 120f, SprinklerSweeps = 4f;

        readonly int _night;
        readonly float _offset;

        public NeighbourLoop(int night)
        {
            _night = night;
            _offset = StartOffset(night);
        }

        public int Night => _night;

        /// <summary>Where in the loop night <paramref name="night"/> starts: fixed per night (a small hash, no randomness).</summary>
        public static float StartOffset(int night)
        {
            uint h = (uint)night * 2654435761u;
            h ^= h >> 13;
            return (h % 2400u) / 100f;
        }

        /// <summary>The time within the loop (0..Period) at <paramref name="time"/> seconds into the night.</summary>
        public float Phase(float time)
        {
            float t = (time + _offset) % Period;
            return t < 0f ? t + Period : t;
        }

        /// <summary>The loop count at <paramref name="time"/> (how many times it has started over).</summary>
        public int Cycle(float time) => (int)Math.Floor((time + _offset) / Period);

        /// <summary>The sprinkler's angle (degrees, -60..60) at <paramref name="time"/>.</summary>
        public float SprinklerAngle(float time)
        {
            float u = Phase(time) / Period * SprinklerSweeps;
            float tri = 1f - Math.Abs(2f * (u - (float)Math.Floor(u)) - 1f);
            return (tri - 0.5f) * SprinklerSweep;
        }

        /// <summary>The ball in the air: 0 thrown .. 1 landed; -1 when it isn't flying.</summary>
        public float BallFlight(float time)
        {
            float p = Phase(time);
            return p >= ThrowAt && p < LandAt ? (p - ThrowAt) / (LandAt - ThrowAt) : -1f;
        }

        /// <summary>The dog between his spot (0) and the ball (1).</summary>
        public float DogOut(float time)
        {
            float p = Phase(time);
            if (p < LandAt) return 0f;
            if (p < DogOutUntil) return (p - LandAt) / (DogOutUntil - LandAt);
            if (p < DogBackUntil) return 1f - (p - DogOutUntil) / (DogBackUntil - DogOutUntil);
            return 0f;
        }

        /// <summary>The ball rests where it landed until the dog reaches it, then rides back with him.</summary>
        public bool BallWithDog(float time)
        {
            float p = Phase(time);
            return p >= DogOutUntil && p < DogBackUntil;
        }

        /// <summary>True when the line is said between <paramref name="from"/> (exclusive) and <paramref name="to"/>.</summary>
        public bool LineBetween(float from, float to)
        {
            if (to <= from) return false;
            double a = (from + _offset - LineAt) / Period, b = (to + _offset - LineAt) / Period;
            return Math.Floor(b) > Math.Floor(a);
        }
    }
}

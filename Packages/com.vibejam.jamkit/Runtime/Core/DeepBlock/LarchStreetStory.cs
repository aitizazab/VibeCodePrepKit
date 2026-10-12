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
    /// Night 1's words on Larch Hill (docs/redesign/world.md "Night 1, beat by beat"): Mags on the earpiece, Walt, Nadia, the
    /// neighbours. Walt's argument itself is the battle's (ArgumentText/walt_night1.txt); these are the street's lines
    /// around it, written in the voice the creator's lines set. The creator edits by playing.
    /// </summary>
    public static class LarchStreetLines
    {
        /// <summary>Kept for old callers: the street's lines are no longer placeholders.</summary>
        public const string Placeholder = "";
        public const string Mags = "Mags", Nadia = "Nadia", Walt = "Walt", Neighbour = "Mrs. Lindqvist", CarWash = "Mr. Okonkwo";

        static StreetLine L(string who, string text, float s = 3.2f) => new StreetLine(who, text, s);

        public static readonly StreetLine[] Arrival =
        {
            L(Mags, "You're in. Golden hour, the street you two grew up on. He's up that hill somewhere.", 4.2f),
            L(Mags, "You're lighter in here. The creek: jump, then jump again in the air.", 3.8f),
        };
        public static readonly StreetLine[] CreekFall = { L(Mags, "The ford's downstream. Wade out and try it again.", 3.2f) };
        public static readonly StreetLine[] GarageDoor = { L(Mags, "Walt's door's stuck halfway. Run at it and get low: slide.", 3.6f) };
        public static readonly StreetLine[] Paper = { L(Nadia, "Saturday's paper. Still in its plastic.", 3f) };
        public static readonly StreetLine[] Ledger = { L(Nadia, "\"Sat. E., rear tyre, flat. Patched.\" Forty-one times. The same date.", 4.2f) };
        public static readonly StreetLine[] PatchedBike = { L(Nadia, "Eli's bike. New patch on the back tyre. Again.", 3f) };
        public static readonly StreetLine[] WaltAlive = { L(Nadia, "Mr. Abernathy. Sitting on his wall like it's any Saturday.", 3.4f) };
        public static readonly StreetLine[] WaltDead = { L(Nadia, "Walt died in 2015. This is memory.", 3.4f) };
        public static readonly StreetLine[] WaltBlocks = { L(Walt, "Afternoon, Nads. Your brother's having a good day. Let him have it.", 4f) };
        /// <summary>When she just sat with him at the bedside (DreamChanges n1_walt_warm): Walt greets her warmly, by name.</summary>
        public static readonly StreetLine[] WaltWarm =
        {
            L(Walt, "Nadia Lasname. Look at you, all grown up and still climbing my walls.", 4f),
            L(Walt, "Your brother's having a good day, love. Let him have it.", 3.4f),
        };
        public static readonly StreetLine[] LedgerOpen = { L(Nadia, "It's open on the bench. Like he wanted me to see it.", 3.2f) };
        public static readonly StreetLine[] Plank = { L(Mags, "There's a plank across the creek. You told him about it, didn't you.", 3.6f) };
        public static readonly StreetLine[] HospitalLit = { L(Mags, "The lights are on up at St. Brigid's. He heard you.", 3.2f) };
        public const string ArgumentStub = "Debug: how does Walt's argument end?   [1] persuaded   [2] heard out   [3] pressed";
        public static readonly StreetLine[] WaltAsidePersuaded = { L(Walt, "Go on, then. Over the wall, the way you kids always went.", 3.6f) };
        public static readonly StreetLine[] WaltAsideHeardOut = { L(Walt, "I won't stop you. The wall's there, if you want it.", 3.4f) };
        public static readonly StreetLine[] WaltAsidePressed = { L(Walt, "...Fine. The wall. Go.", 2.4f) };
        public static readonly StreetLine[] LessonMantle = { L(Walt, "Hands on the top, and up. You used to do it with a bike on your shoulder.", 4f) };
        public static readonly StreetLine[] LessonWallrun = { L(Mags, "Too wide to jump. Run along the wall beside the pool. Don't slow down.", 3.8f) };
        public static readonly StreetLine[] PoolFall = { L(Mags, "Steps at the shallow end. Faster this time.", 2.8f) };
        public static readonly StreetLine[] LessonSlide = { L(Mags, "Under the beam. Keep your speed and drop low.", 3f) };
        public static readonly StreetLine[] LessonClimb = { L(Mags, "Too high to step up. Jump first, then grab the top.", 3.2f) };
        public static readonly StreetLine[] LessonChimney = { L(Mags, "Two walls, close together. Jump into one, kick off into the other. Keep climbing.", 4.2f) };
        public static readonly StreetLine[] MillStreet =
        {
            L(Mags, "There's the house, over the green. And that's St. Brigid's on top. Lose your way, look for it.", 4.6f),
        };
        public static readonly StreetLine[] Lane = { L(Mags, "Roofs are quicker than the lane. Usually.", 2.8f) };
        public static readonly StreetLine[] DitchFall = { L(Mags, "Steps at the far end of the ditch. Up and round.", 3f) };
        public static readonly StreetLine[] Shutter = { L(Mags, "That shutter goes up and down like clockwork. Count it.", 3.2f) };
        public static readonly StreetLine[] Nail = { L(Nadia, "A roofing nail, point up, in the same crack. His tyre never stood a chance.", 4f) };
        public static readonly StreetLine[] Oak = { L(Mags, "Up the oak. That limb goes over the wall.", 3f) };
        public static readonly StreetLine[] Treehouse = { L(Nadia, "\"NADS + ELI. NO GIRLS.\" He crossed the last bit out for me.", 4f) };
        public static readonly StreetLine[] EliasGlimpsed = { L(Nadia, "Eli!", 1.4f) };
        public static readonly StreetLine[] RewindBegins =
        {
            L(Mags, "He heard you. So did the dream. Run.", 2.6f),
            L(Mags, "Whatever that light touches goes back to the start of the day. Don't let it touch you.", 4.2f),
        };
        public static readonly StreetLine[][] RewindCaught =
        {
            new[] { L(Mags, "Back to the start of the street. Again.", 2.6f) },
            new[] { L(Mags, "Don't stop for anything. Use the truck.", 2.6f) },
            new[] { L(Mags, "It's slowing. So are you. Go.", 2.4f) },
        };
        public static readonly StreetLine[] RewindOutrun = { L(Nadia, "Eli, it's me.", 2f) };
        public const string GrillNeighbour = CarWash;
        public static readonly StreetLine[] Chatter = { L(CarWash, "Afternoon! Mind the suds.", 2.4f) };
        public const string LoopLine = "Lovely Saturday for it!";
        public static readonly StreetLine[] LoopHeard = { L(Nadia, "Same words. Same wave of the hand.", 3f) };
        public static readonly StreetLine[] SameSaturday = { L(Nadia, "He's living one Saturday, over and over.", 3.4f) };
        public static readonly StreetLine[] Ending = { L(Mags, "His heart rate's up. That's enough for tonight. You found the house.", 4.2f) };
        public static readonly StreetLine[] FourWaysUp = Lane;

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
        /// <summary>Optional, in Back Lane B: the nail that flattens Eli's tyre every Saturday (with the ledger: the loop
        /// counted).</summary>
        public const string MemTheNail = "mem_the_nail";
        /// <summary>Optional, the old oak's treehouse: "NADS + ELI" (the dream still keeps her name).</summary>
        public const string MemTreehouse = "mem_treehouse";
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

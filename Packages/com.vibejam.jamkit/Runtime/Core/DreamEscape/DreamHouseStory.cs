using System;

namespace JamKit.DreamEscape
{
    /// <summary>Where Nadia is in Night 2's house.</summary>
    public enum HousePhase
    {
        /// <summary>The front garden and the way in (the hallway that stretches, the door onto the slide).</summary>
        WayIn,
        /// <summary>At the table: pancakes with Lena and Theo (lines, then the game begins).</summary>
        Pancakes,
        /// <summary>Hide-and-seek with Theo: find the memories while he looks for you.</summary>
        HideAndSeek,
        /// <summary>Elias hasn't come: she questions Lena (the argument battle's place; a debug choice for now).</summary>
        Argument,
        /// <summary>The house kicks her out: it tips over as she runs; the front door; the rain.</summary>
        Escape,
        /// <summary>Out of the house, at the end of the rain path.</summary>
        Out,
    }

    /// <summary>How the argument with Lena ends (the battle isn't built: a debug choice decides it for now).</summary>
    public enum ArgumentOutcome { None, Unsure, Condemns }

    /// <summary>One line of dialogue. Every line in <see cref="DreamHouseLines"/> is a PLACEHOLDER: the dialogue is the creator's.</summary>
    public struct HouseLine
    {
        public string speaker, text;
        public float seconds;

        public HouseLine(string speaker, string text, float seconds = 3.2f)
        {
            this.speaker = speaker;
            this.text = text;
            this.seconds = seconds;
        }
    }

    /// <summary>
    /// The house's lines, every one a PLACEHOLDER marked as such (the dialogue is the creator's; replace the text here).
    /// The last-memory line is the creator's own, from their notes; its two endings are both kept, because which one is
    /// the Despair route's is still the creator's open question (<see cref="DespairEnding"/>).
    /// </summary>
    public static class DreamHouseLines
    {
        public const string Placeholder = "[placeholder] ";
        public const string Nadia = "Nadia", Lena = "Lena", Theo = "Theo";

        public static readonly HouseLine[] Arrival =
        {
            new HouseLine(Nadia, Placeholder + "The door's open. It wasn't, a second ago."),
        };

        public static readonly HouseLine[] Pancakes =
        {
            new HouseLine(Lena, Placeholder + "Sit, they're still warm. Your chair's where it always was."),
            new HouseLine(Theo, Placeholder + "Hide-and-seek after! You hide, I count!"),
            new HouseLine(Nadia, Placeholder + "Okay. Just until your dad gets here."),
        };

        public static readonly HouseLine[] FoundYou =
        {
            new HouseLine(Theo, Placeholder + "Found you! Again! I'll count again!", 2.6f),
        };

        /// <summary>The creator's line for when the last memory is found (their notes): it tells the player there's nothing
        /// left to find. Its ending differs by route: see <see cref="AllFoundEndings"/>.</summary>
        public const string AllFoundStart = "I'm not sure if there's anything left to find. I could let Theo find me here...";

        /// <summary>The two endings from the creator's notes, both kept: one is the Despair route's, the other the normal
        /// route's. Which is which is open.</summary>
        public static readonly string[] AllFoundEndings =
        {
            "but what does it matter, when he's not even real?",
            "it would be nice to see his smile again.",
        };

        /// <summary>OPEN (the creator's question): which of <see cref="AllFoundEndings"/> the Despair route gets. 0 for now
        /// (the more hopeless one); the normal route gets the other.</summary>
        public static int DespairEnding = 0;

        public static string AllFound(bool despair) =>
            Placeholder + AllFoundStart + " " + AllFoundEndings[despair ? DespairEnding : 1 - DespairEnding];

        public static readonly HouseLine[] EliasNotHere =
        {
            new HouseLine(Nadia, Placeholder + "He isn't coming, is he? Lena, where is he?"),
            new HouseLine(Lena, Placeholder + "He's happy here. Leave him be."),
        };

        public const string ArgumentPrompt = "PLACEHOLDER: the argument battle with Lena goes here (not built yet). Debug: how does it end?   [1] Lena is unsure   [2] Lena condemns Nadia";

        public static readonly HouseLine[] Unsure =
        {
            new HouseLine(Lena, Placeholder + "I... don't know any more. Go. Please, just go."),
        };

        public static readonly HouseLine[] Condemns =
        {
            new HouseLine(Lena, Placeholder + "You don't belong here. Get out of his house."),
        };

        public static readonly HouseLine[] KickedOut =
        {
            new HouseLine(Nadia, Placeholder + "The floor-- the house is turning over!", 2.4f),
        };
    }

    /// <summary>
    /// Night 2's house as a story, pure (docs/DREAM-HOUSE.md): the way in, pancakes, hide-and-seek until every memory is
    /// found and Theo finds her, the argument with Lena (a debug choice: unsure or condemns), the escape, out. Lena
    /// condemning Nadia makes the Despair route easier to reach: the front door's point of no return comes sooner and
    /// the rain soaks her faster (<see cref="RainFor"/>, <see cref="RainDefFor"/>).
    /// </summary>
    public sealed class DreamHouseStory
    {
        /// <summary>The save flag set when Lena condemns Nadia (it stays, like the Despair flag).</summary>
        public const string CondemnsFlag = "vh_lena_condemns";
        /// <summary>Metres into the rain without the umbrella before it's the Despair choice; after Lena condemns her.</summary>
        public const float DespairDepth = 8f, CondemnedDespairDepth = 3f;
        public const float SoakTime = 5f, CondemnedSoakTime = 3f;

        HousePhase _phase;
        ArgumentOutcome _outcome;
        bool _allFound;
        int _rounds, _foundTimes;

        public HousePhase Phase => _phase;
        public ArgumentOutcome Outcome => _outcome;
        public bool LenaCondemned => _outcome == ArgumentOutcome.Condemns;
        /// <summary>Every memory found (the creator's line has played): the next time Theo finds her, the game ends.</summary>
        public bool AllMemoriesFound => _allFound;
        /// <summary>Hide-and-seek rounds started (Theo counting).</summary>
        public int Rounds => _rounds;
        public int TimesFound => _foundTimes;

        /// <summary>A fresh visit: the front garden, nothing found, no argument.</summary>
        public void Restart()
        {
            _phase = HousePhase.WayIn;
            _outcome = ArgumentOutcome.None;
            _allFound = false;
            _rounds = 0;
            _foundTimes = 0;
        }

        /// <summary>A chapter jump: straight to <paramref name="phase"/> (earlier steps count as done; the argument keeps
        /// <paramref name="outcome"/>).</summary>
        public void JumpTo(HousePhase phase, ArgumentOutcome outcome = ArgumentOutcome.None)
        {
            Restart();
            _phase = phase;
            if (phase >= HousePhase.HideAndSeek) _rounds = 1;
            if (phase >= HousePhase.Argument) _allFound = true;
            if (phase >= HousePhase.Escape) _outcome = outcome == ArgumentOutcome.None ? ArgumentOutcome.Unsure : outcome;
        }

        /// <summary>She came down the slide into the kitchen: the pancakes. False if that's already happened.</summary>
        public bool ReachKitchen()
        {
            if (_phase != HousePhase.WayIn) return false;
            _phase = HousePhase.Pancakes;
            return true;
        }

        /// <summary>The pancake lines are done: hide-and-seek begins (Theo counts).</summary>
        public bool StartHideAndSeek()
        {
            if (_phase != HousePhase.Pancakes) return false;
            _phase = HousePhase.HideAndSeek;
            _rounds = 1;
            return true;
        }

        /// <summary>The last memory was found: the line to say (its ending by route).</summary>
        public string FoundEveryMemory(bool despair)
        {
            _allFound = true;
            return DreamHouseLines.AllFound(despair);
        }

        /// <summary>Theo found her. True: the game's over (every memory found), Elias hasn't come, the argument begins.
        /// False: another round (he counts again).</summary>
        public bool TheoFoundHer()
        {
            if (_phase != HousePhase.HideAndSeek) return false;
            _foundTimes++;
            if (!_allFound)
            {
                _rounds++;
                return false;
            }
            _phase = HousePhase.Argument;
            return true;
        }

        /// <summary>How the argument ends: the house kicks her out either way.</summary>
        public bool Choose(ArgumentOutcome outcome)
        {
            if (_phase != HousePhase.Argument || outcome == ArgumentOutcome.None) return false;
            _outcome = outcome;
            _phase = HousePhase.Escape;
            return true;
        }

        /// <summary>The end of the rain path.</summary>
        public void Escaped()
        {
            if (_phase == HousePhase.Escape) _phase = HousePhase.Out;
        }

        /// <summary>The front door's rain after this argument (or after an earlier visit's, <paramref name="condemned"/>):
        /// condemned, the point of no return is nearer the door.</summary>
        public static DreamRainSpot RainFor(DreamRainSpot spot, bool condemned)
        {
            var s = spot.Copy();
            s.despairS = spot.rainStartS + (condemned ? CondemnedDespairDepth : DespairDepth);
            return s;
        }

        /// <summary>Condemned, the rain soaks her faster.</summary>
        public static DreamRainDef RainDefFor(bool condemned) => new DreamRainDef { soakTime = condemned ? CondemnedSoakTime : SoakTime };
    }
}

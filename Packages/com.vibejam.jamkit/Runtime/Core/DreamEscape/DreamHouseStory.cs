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

    /// <summary>How Nadia answered Theo's first question, "Is Dad going to be sad?" (docs/NIGHTS-1-2.md, Night 2 step 5).
    /// He remembers: it's kept in the save (<see cref="DreamHouseStory.TheoAnswerKey"/>).</summary>
    public enum TheoAnswer { None, Honest, Comfort, Silence }

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

        public const string Neighbour = "A neighbour", Walt = "Walt";

        /// <summary>The street (Night 2 step 1): the neighbours know her name now; the street replays Night 1.</summary>
        public static readonly HouseLine[] Street =
        {
            new HouseLine(Neighbour, Placeholder + "Morning, Nadia!", 2.4f),
            new HouseLine(Nadia, Placeholder + "Same sprinkler. Same ball. Same morning. It's playing yesterday again.", 4f),
            new HouseLine(Walt, Placeholder + "(Walt waves her up to the house.)", 2.6f),
        };

        public static readonly HouseLine[] Arrival =
        {
            new HouseLine(Nadia, Placeholder + "The door's open. It wasn't, a second ago."),
        };

        /// <summary>The house misbehaving (the movement intro, the wallrun): when the hallway stretches.</summary>
        public static readonly HouseLine[] HouseMisbehaves =
        {
            new HouseLine(Nadia, Placeholder + "The hall's getting longer. The house doesn't want me in yet.", 3.4f),
        };

        /// <summary>The pancakes (step 3): the table set for four, her place card on the fourth chair in Theo's handwriting;
        /// she sits, thinking Elias will be down soon.</summary>
        public static readonly HouseLine[] Pancakes =
        {
            new HouseLine(Lena, Placeholder + "Sit, sit, they're still warm."),
            new HouseLine(Theo, Placeholder + "I made your card! That's your chair!"),
            new HouseLine(Nadia, Placeholder + "(Her name, in a kid's careful letters. She sits.)", 3.4f),
            new HouseLine(Lena, Placeholder + "Elias will be down soon."),
            new HouseLine(Theo, Placeholder + "Hide-and-seek after! You hide, I count!"),
            new HouseLine(Nadia, Placeholder + "Okay. Just until your dad gets here."),
        };

        /// <summary>Theo's first question when he finds her (step 5). The question is the creator's (CHARACTERS.md).</summary>
        public const string TheoQuestion = "Is Dad going to be sad?";

        public const string TheoQuestionPrompt = "Is Dad going to be sad?   [1] Honest   [2] Comfort   [3] Say nothing";

        /// <summary>Nadia's answer and Theo's reply, by answer (index = <see cref="TheoAnswer"/> - 1). PLACEHOLDERS.</summary>
        public static readonly HouseLine[][] TheoAnswers =
        {
            new[] { new HouseLine(Nadia, Placeholder + "Yes. I think he will be, for a while."), new HouseLine(Theo, Placeholder + "...Okay. I'll count again.", 2.6f) },
            new[] { new HouseLine(Nadia, Placeholder + "No. He'll be all right. I promise."), new HouseLine(Theo, Placeholder + "Good! I'll count again!", 2.6f) },
            new[] { new HouseLine(Nadia, Placeholder + "(She doesn't answer.)", 2.4f), new HouseLine(Theo, Placeholder + "...I'll count again.", 2.6f) },
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

        /// <summary>Which of <see cref="AllFoundEndings"/> the Despair route gets (docs/NIGHTS-1-2.md, main's call: the Long
        /// Night is the pull of the dream family, so the longing line leads into it): "...it would be nice to see his smile
        /// again." The normal route gets "...but what does it matter, when he's not even real?".</summary>
        public static int DespairEnding = 1;

        /// <summary>The creator's line (not a placeholder), its ending by route.</summary>
        public static string AllFound(bool despair) =>
            AllFoundStart + " " + AllFoundEndings[despair ? DespairEnding : 1 - DespairEnding];

        /// <summary>Elias never comes down (step 6): Nadia asks Lena where he is; Lena admits she won't let him go.</summary>
        public static readonly HouseLine[] EliasNotHere =
        {
            new HouseLine(Nadia, Placeholder + "He isn't coming down, is he? Lena, where is he?"),
            new HouseLine(Lena, Placeholder + "Upstairs. Where he's safe. I'm not letting him go."),
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

        /// <summary>Out of the front door (step 7): Lena watches from the window.</summary>
        public static readonly HouseLine[] LenaAtTheWindow =
        {
            new HouseLine(Nadia, Placeholder + "(Behind her, in the front window, Lena watches her go.)", 3.6f),
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

        /// <summary>The save entry holding Theo's question's answer ("honest", "comfort", "silence").</summary>
        public const string TheoAnswerKey = "vh_theo_dad_sad";
        /// <summary>The battle with Lena, by id (the argument's battle API starts it by id).</summary>
        public const string LenaBattleId = "lena_night2";

        HousePhase _phase;
        ArgumentOutcome _outcome;
        TheoAnswer _theoAnswer;
        bool _allFound;
        int _rounds, _foundTimes;

        public HousePhase Phase => _phase;
        public ArgumentOutcome Outcome => _outcome;
        public bool LenaCondemned => _outcome == ArgumentOutcome.Condemns;
        /// <summary>How she answered Theo's question this visit (None: he hasn't asked yet).</summary>
        public TheoAnswer TheoAnswered => _theoAnswer;
        /// <summary>Theo asks his question the first time he finds her.</summary>
        public bool TheoAsks => _phase == HousePhase.HideAndSeek && _theoAnswer == TheoAnswer.None;
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
            _theoAnswer = TheoAnswer.None;
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

        /// <summary>Her answer to Theo's question (once a visit): false if he hasn't asked or she's answered.</summary>
        public bool AnswerTheo(TheoAnswer answer)
        {
            if (answer == TheoAnswer.None || _theoAnswer != TheoAnswer.None) return false;
            _theoAnswer = answer;
            return true;
        }

        /// <summary>The save's text for an answer.</summary>
        public static string AnswerText(TheoAnswer a) => a == TheoAnswer.None ? null : a.ToString().ToLowerInvariant();

        /// <summary>The memory ids (Night 2's five, docs/NIGHTS-1-2.md "The memories").</summary>
        public const string MemPlaceCard = "mem_fourth_chair", MemCalendar = "mem_calendar", MemBadDream = "mem_bad_dream",
            MemHeightMarks = "mem_height_marks", FactPhoto = "fact_photo";
        /// <summary>The deduction the calendar makes with Night 1's Saturday paper ("Pancakes!" on every Sunday; it's
        /// Saturday): breaks Lena's L3b.</summary>
        public const string DedSaturdayOff = "ded_saturday_off";

        /// <summary>The evidence cards she carries into the battle: the memories found, and the calendar turned into
        /// <see cref="DedSaturdayOff"/> when she has Night 1's Saturday paper (<see cref="DreamJournal.SaturdayPaper"/>).</summary>
        public static System.Collections.Generic.List<string> EvidenceFor(System.Collections.Generic.IEnumerable<string> found, bool hasSaturdayPaper)
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (var id in found)
            {
                if (id == MemCalendar && hasSaturdayPaper) list.Add(DedSaturdayOff);
                else if (!string.IsNullOrEmpty(id) && !list.Contains(id)) list.Add(id);
            }
            return list;
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

namespace JamKit.DreamEscape
{
    /// <summary>
    /// What Nadia carries from night to night (the shared journal of the dives, kept in the save under <see cref="Key"/>
    /// as a comma list of ids): Night 1 adds <see cref="SaturdayPaper"/> when she finds the Saturday paper, Night 2 reads
    /// it to make <see cref="DreamHouseStory.DedSaturdayOff"/>. Pure, so any night can use it on the save's text.
    /// </summary>
    public static class DreamJournal
    {
        public const string Key = "vh_dream_journal";
        /// <summary>Night 1's Saturday paper (its date: it's Saturday).</summary>
        public const string SaturdayPaper = "mem_saturday_paper";

        public static bool Has(string entry, string id)
        {
            if (string.IsNullOrEmpty(entry) || string.IsNullOrEmpty(id)) return false;
            foreach (var part in entry.Split(','))
                if (part.Trim() == id) return true;
            return false;
        }

        /// <summary>The entry with <paramref name="id"/> added (once).</summary>
        public static string Add(string entry, string id)
        {
            if (string.IsNullOrEmpty(id) || Has(entry, id)) return entry ?? "";
            return string.IsNullOrEmpty(entry) ? id : entry + "," + id;
        }
    }
}

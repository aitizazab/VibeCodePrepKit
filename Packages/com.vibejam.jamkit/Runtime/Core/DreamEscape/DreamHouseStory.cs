using System;
using System.Collections.Generic;

namespace JamKit.DreamEscape
{
    /// <summary>Where Nadia is in Night 2's house.</summary>
    public enum HousePhase
    {
        /// <summary>The street and the front garden, up to the front door; the hall breathing as she comes in.</summary>
        WayIn,
        /// <summary>At the table: she sits at the fourth chair; pancakes with Lena and Theo.</summary>
        Pancakes,
        /// <summary>Hide-and-seek with Theo: find the memories while he looks for her.</summary>
        HideAndSeek,
        /// <summary>The round is over (every memory found and he found her, or he caught her three times): his question.</summary>
        TheoAsks,
        /// <summary>Elias hasn't come: she questions Lena (the argument battle).</summary>
        Argument,
        /// <summary>The house throws her out: the fever-dream run to the front door.</summary>
        Escape,
        /// <summary>Out of the front door, onto the hill, in the rain.</summary>
        Out,
        /// <summary>She fell, or the house closed on her: Mags pulled her out of the dream. Night 2's ending is lost.</summary>
        ThrownOut,
    }

    /// <summary>How the argument with Lena ends.</summary>
    public enum ArgumentOutcome { None, Unsure, Condemns }

    /// <summary>How Nadia answered Theo's question, "Is Dad going to be sad?". He remembers (the save,
    /// <see cref="DreamHouseStory.TheoAnswerKey"/>).</summary>
    public enum TheoAnswer { None, Honest, Comfort, Silence }

    /// <summary>One line of dialogue: who says it ("" for Nadia's thought, shown in italics), the text, and how long it
    /// stays (the presenter reveals each line in turn, never all at once).</summary>
    public struct HouseLine
    {
        public string speaker, text;
        public float seconds;

        public HouseLine(string speaker, string text, float seconds = 0f)
        {
            this.speaker = speaker;
            this.text = text;
            this.seconds = seconds > 0f ? seconds : ReadTime(text);
        }

        /// <summary>Time to read it: 1.6 s plus 0.3 s a word.</summary>
        public static float ReadTime(string text) => 1.6f + 0.3f * (text ?? "").Split(' ').Length;
    }

    /// <summary>
    /// Every line in Night 2's house (docs/redesign/house.md "The lines"). The creator's own lines are kept word for word
    /// and marked: "Morning, Nadia!", Theo's question, the last-memory line and its two endings. The rest is written in the
    /// voice their lines set (Lena's battle: sharp, warm, proud; Mags on the earpiece: dry, short, a doctor's calm; Theo:
    /// ten, in a hoodie too big for him, rules-lawyering his own game).
    /// </summary>
    public static class DreamHouseLines
    {
        public const string Nadia = "Nadia", Lena = "Lena", Theo = "Theo", Mags = "Mags", Neighbour = "A neighbour", Thought = "";

        // ---------- The way in ----------
        public static readonly HouseLine[] Street =
        {
            new HouseLine(Neighbour, "Morning, Nadia!"),                                   // the creator's
            new HouseLine(Thought, "He knows my name now. Same sprinkler, same dog, same ball. Same second."),
            new HouseLine(Mags, "That's his house. Whoever opens the door, it isn't them. Remember that."),
        };

        public static readonly HouseLine[] AtTheDoor =
        {
            new HouseLine(Thought, "The door's open. It was never left open. Lena used to shout about the heating."),
        };

        /// <summary>The hall breathes as she comes in (a dolly zoom: the escape's corridor, foreshadowed).</summary>
        public static readonly HouseLine[] HallBreathes =
        {
            new HouseLine(Thought, "...The hall just got longer."),
            new HouseLine(Lena, "Nadia? We're in the dining room! They're getting cold!"),
            new HouseLine(Thought, "It's back. It's just a hall."),
        };

        // ---------- What she told him at his bedside (Day 2) changes the house (docs/redesign/day.md) ----------
        /// <summary>"The lake": the photo's up on the hall wall already, lit (it counts as found).</summary>
        public static readonly HouseLine[] PhotoAlreadyUp = { new HouseLine(Thought, "The lake. He's put it up in the hall, where I'd see it coming in.") };
        /// <summary>"Lena": she's at the front window as Nadia comes up the path, and colder for it.</summary>
        public static readonly HouseLine[] LenaWaryAtTheDoor = { new HouseLine(Lena, "You came back, then. Wipe your feet.") };
        /// <summary>"Just sit": the house is gentler.</summary>
        public static readonly HouseLine[] LenaSofterAtTheDoor = { new HouseLine(Lena, "There you are. Come in out of the cold, love.") };
        /// <summary>Theo's room is the base until she's found three memories (or, if she told Elias about Theo's team, open
        /// from the start).</summary>
        public static readonly HouseLine[] TheoRoomShut = { new HouseLine(Theo, "My room's the base! No hiding in the base!") };
        public static readonly HouseLine[] TheoRoomOpens = { new HouseLine(Theo, "Okay, okay. You can hide in my room now. But not in the wardrobe!") };
        public static readonly HouseLine[] TheoTeam = { new HouseLine(Theo, "You can hide in my room if you want. You're on my team.") };

        // ---------- Pancakes ----------
        /// <summary>She sits at the fourth chair (her place card in Theo's hand: the first memory).</summary>
        public static readonly HouseLine[] Pancakes =
        {
            new HouseLine(Lena, "There she is. Sit, sit, before Theo eats yours."),
            new HouseLine(Theo, "I made your card! I did the N backwards first but I fixed it."),
            new HouseLine(Thought, "My name, in his careful letters. The fourth chair."),
            new HouseLine(Lena, "Elias will be down soon. You know what he's like on a Saturday."),
            new HouseLine(Theo, "Can we play hide-and-seek? Before Dad comes down? Please?"),
            new HouseLine(Lena, "After breakfast."),
            new HouseLine(Theo, "It IS after breakfast. I ate mine."),
            new HouseLine(Nadia, "Okay. Just until your dad gets here."),
            new HouseLine(Theo, "I'm counting to twenty! The whole house counts. Even the garage. Even the treehouse!"),
            new HouseLine(Mags, "A kid's game. Fine. While he looks for you, you look too. Whatever this house is hiding, it's hiding it from him."),
        };

        // ---------- Hide-and-seek: Theo ----------
        public static string Count(int n) => n + "...";
        public const string CountDone = "Ready or not, here I come!";
        /// <summary>He peeks while he counts (a kid does).</summary>
        public static readonly string[] Peeked = { "I wasn't peeking! ...Much.", "I'm not looking. I'm NOT." };
        /// <summary>He caught a glimpse while peeking: where she went (by storey: basement, ground, upper, attic; 4 outside).</summary>
        public static readonly string[] SawYouGo =
        {
            "I saw you go down to the basement! ...Maybe.", "I saw you! You're downstairs somewhere!", "I saw you go upstairs!", "I saw you go up to the attic! Mum says that's not for hiding!", "I saw you go outside!",
        };
        public static readonly string[] Heard = { "I heard that!", "Was that you?", "Nadia's being LOUD.", "I can hear you!" };
        public static readonly string[] Suspicious = { "Hmm...?", "Is someone there?", "Nadia...?" };
        public static readonly string[] Chase = { "I SEE you!", "There you are!", "Gotcha-- wait!" };
        public static readonly string[] CheckedEmpty = { "Not here...", "Nope.", "Where are youuu?", "Hmm. Empty.", "Not in here!" };
        public static readonly string[] Giggle = { "(giggles)", "(a muffled giggle)", "(humming)" };
        public static readonly string[] LostYou = { "Aw. Where'd you go?", "No fair!", "I'll find you!" };

        /// <summary>What he calls when he goes into a room (sometimes), by room id.</summary>
        public static string RoomCall(string room)
        {
            switch (room)
            {
                case "kitchen": return "Are you in the kitchen?";
                case "pantry": return "Mum! Is she in the pantry?";
                case "living": return "Behind the sofa? ...No.";
                case "den": return "Dad's den. Shh.";
                case "dining": return "Not under the table...";
                case "bath": return "Nadia's not in the bathroom. Probably.";
                case "theo": return "Not in MY room. That's my room.";
                case "guest": return "Are you in your room?";
                case "parents": return "Are you in Mum and Dad's room? You're not allowed. ...Are you?";
                case "closet": return "Mum's dresses. Hello, Mum's dresses.";
                case "rec": return "The basement's not scary. It's NOT.";
                case "furnace": return "It's dark in here. I don't like the furnace.";
                case "workshop": return "Dad's radio's on again.";
                case "laundry": return "Behind the washing? ...Behind the washing!";
                case "garage": return "Not in the car. Dad said never in the car.";
                case "attic": return "The attic! Mum's going to be so mad.";
                case "observatory": return "Mum's stars room. Shh. Shh!";
                case "yard": return "Are you outside? It's going to rain!";
                case "treehouse": return "Nobody's allowed in the treehouse except members!";
                case "shed": return "Spiders. Spiders. Nadia, are you in with the spiders?";
                default: return null;
            }
        }

        /// <summary>He's stuck: he asks his mum (she won't tell).</summary>
        public static readonly HouseLine[] AsksMum =
        {
            new HouseLine(Theo, "Mum! Where is she?"),
            new HouseLine(Lena, "I'm not telling. Look properly."),
        };

        /// <summary>He found her (tagged her), by how many times (1, 2, 3).</summary>
        public static readonly string[] FoundYou = { "Found you! That's one!", "Found you AGAIN! That's two!", "Three! I win! I win!" };

        // ---------- The catch's cost: the house takes back the last memory she found ----------
        public static readonly HouseLine[][] Caught =
        {
            new[]
            {
                new HouseLine(Thought, "No. It's gone. The house took it back."),
                new HouseLine(Mags, "It's moved it somewhere else. It'll do that every time he catches you. Don't let him."),
            },
            new[]
            {
                new HouseLine(Thought, "Again. Somewhere else in the house."),
                new HouseLine(Mags, "That's two. One more and the game's over, and you go into that kitchen with whatever you're holding."),
            },
            new[]
            {
                new HouseLine(Nadia, "...You win."),
                new HouseLine(Mags, "That's it, then. You've got what you've got."),
            },
        };
        /// <summary>Caught with nothing found since the table: nothing to take back but the count.</summary>
        public static readonly HouseLine[] CaughtEmptyHanded = { new HouseLine(Thought, "Nothing for it to take. Yet.") };

        // ---------- The memories (what she thinks when she takes each one) ----------
        public static HouseLine[] MemoryLine(string id, bool hasSaturdayPaper)
        {
            switch (id)
            {
                case DreamHouseStory.MemPlaceCard: return new[] { new HouseLine(Thought, "He saved me a seat. The Sundays I didn't come, he still set it.") };
                case DreamHouseStory.MemCalendar:
                    return hasSaturdayPaper
                        ? new[] { new HouseLine(Thought, "\"Pancakes!\" on every Sunday. A little key on every Thursday, in Elias's hand."), new HouseLine(Thought, "And the paper says Saturday. Pancakes on a Saturday.") }
                        : new[] { new HouseLine(Thought, "\"Pancakes!\" on every Sunday. A little key on every Thursday, in Elias's hand. He drew those, and never asked.") };
                case DreamHouseStory.FactPhoto: return new[] { new HouseLine(Thought, "The lake. The three of them, and me. He kept me in it.") };
                case DreamHouseStory.MemHeightMarks: return new[] { new HouseLine(Thought, "Pencil lines up the frame, a date by each one. They stop at ten.") };
                case DreamHouseStory.MemBadDream: return new[] { new HouseLine(Thought, "THE BAD DREAM. It isn't a monster. It's Elias, in his scrubs, walking out of the door."), new HouseLine(Thought, "And them at the table, watching him go.") };
                case DreamHouseStory.MemSaturdayPaper: return new[] { new HouseLine(Thought, "Today's paper, folded into a hat. Saturday. The same Saturday it always is.") };
                case DreamHouseStory.MemRadio: return new[] { new HouseLine(Thought, "\"...rain by noon, clearing by evening.\" I've heard this. Word for word, the same minute, every time.") };
                case DreamHouseStory.MemLenaThursdays: return new[] { new HouseLine(Thought, "Her star wheel, turned to a Thursday night. Every Thursday.") };
                default: return new HouseLine[0];
            }
        }

        // ---------- The last memory (the creator's line) ----------
        public const string AllFoundStart = "I'm not sure if there's anything left to find. I could let Theo find me here...";
        /// <summary>The two endings from the creator's notes, both kept.</summary>
        public static readonly string[] AllFoundEndings =
        {
            "but what does it matter, when he's not even real?",
            "it would be nice to see his smile again.",
        };
        /// <summary>Which ending the Despair route gets (main's call, docs/NIGHTS-1-2.md: the longing line leads into the Long
        /// Night): "...it would be nice to see his smile again."</summary>
        public static int DespairEnding = 1;
        public static string AllFound(bool despair) => AllFoundStart + " " + AllFoundEndings[despair ? DespairEnding : 1 - DespairEnding];
        /// <summary>He comes to find her (she waits; she can still run).</summary>
        public const string ComingToFindYou = "I can hear you breathing! ...I think!";

        // ---------- Theo's question (the creator's) ----------
        public const string TheoQuestion = "Is Dad going to be sad?";
        public const string TheoQuestionPrompt = "[1] \"Yes. He will be.\"   [2] \"No. I promise.\"   [3] Say nothing";
        public static readonly HouseLine[] TheoAsksLead =
        {
            new HouseLine(Theo, "Nadia? Can I ask you something? It's not part of the game."),
        };
        /// <summary>Her answer and his reply (index = <see cref="TheoAnswer"/> - 1).</summary>
        public static readonly HouseLine[][] TheoAnswers =
        {
            new[] { new HouseLine(Nadia, "Yes. I think he'll be very sad. For a long time."), new HouseLine(Theo, "...Okay. That's okay. Mum says sad is just what loving something feels like after."), new HouseLine(Theo, "Let's see if he's down yet.") },
            new[] { new HouseLine(Nadia, "No. He's going to be fine. I promise."), new HouseLine(Theo, "You promise promise?"), new HouseLine(Theo, "Okay! Let's see if he's down yet!") },
            new[] { new HouseLine(Thought, "I can't. I can't say it to him."), new HouseLine(Theo, "...You're doing the face. The grown-up face."), new HouseLine(Theo, "Let's go see if Dad's down.") },
        };

        // ---------- Elias never comes down ----------
        public static readonly HouseLine[] EliasNotHere =
        {
            new HouseLine(Thought, "His plate's still there. Cold."),
            new HouseLine(Nadia, "He isn't coming down, is he."),
            new HouseLine(Lena, "He's resting."),
            new HouseLine(Nadia, "Lena. Where is he?"),
            new HouseLine(Lena, "Where you can't take him. Sit down, Nadia."),
        };

        // ---------- After the argument ----------
        public static readonly HouseLine[] Unsure =
        {
            new HouseLine(Lena, "...I don't know. I don't know any more. Go, Nadia. Before he hears you."),
        };
        public static readonly HouseLine[] Condemns =
        {
            new HouseLine(Lena, "Get out of his house."),
        };
        /// <summary>Theo's parting line as the house turns her out, by his question's answer (index = answer - 1).</summary>
        public static readonly HouseLine[] TheoParting =
        {
            new HouseLine(Theo, "Nadia? Tell Dad it's okay to be sad. I'll be brave."),
            new HouseLine(Theo, "Bye, Nadia! You promised!"),
            new HouseLine(Thought, "Theo doesn't look up from his plate. He doesn't wave."),
        };
        public static HouseLine[] PartingFor(TheoAnswer a) => a == TheoAnswer.None ? new HouseLine[0] : new[] { TheoParting[(int)a - 1] };
        public static readonly HouseLine[] HouseTips =
        {
            new HouseLine(Nadia, "The floor--"),
            new HouseLine(Mags, "The whole house is turning. Move. MOVE."),
        };

        // ---------- The escape ----------
        public static readonly HouseLine[] EscapeStart =
        {
            new HouseLine(Mags, "Don't stop. If it closes on you, I pull you out, and you don't get this night back."),
        };
        /// <summary>A line as she turns into each of the escape's rooms (by <see cref="HouseEscape"/>'s room index).</summary>
        public static HouseLine[] EscapeRoom(int room)
        {
            switch (room)
            {
                case 1: return new[] { new HouseLine(Thought, "The walls are the floor--") };
                case 2: return new[] { new HouseLine(Mags, "The stairs. Slide them. Let them take you.") };
                case 4: return new[] { new HouseLine(Thought, "His glow stars. Under my feet.") };
                case 5: return new[] { new HouseLine(Thought, "The kitchen again. It's all coming round again.") };
                case 6: return new[] { new HouseLine(Mags, "Her stars. Don't look down. Don't look at them either.") };
                case 7: return new[] { new HouseLine(Thought, "The front door. The right way up.") };
                default: return new HouseLine[0];
            }
        }
        /// <summary>The corridor that gets longer (by how many times it has stretched, 1-based; the last repeats).</summary>
        public static readonly HouseLine[][] Stretched =
        {
            new[] { new HouseLine(Thought, "It's... longer. It just got longer."), new HouseLine(Mags, "It's stretching to keep you in. You won't walk out of that. Run at it.") },
            new[] { new HouseLine(Mags, "Faster. It only grows when you give it the time.") },
            new[] { new HouseLine(Mags, "The floor slopes. Slide down it and jump out of the slide. Keep the speed.") },
            new[] { new HouseLine(Mags, "Off the walls, Nadia. Run the wall, kick off, slide. Don't let it breathe.") },
            new[] { new HouseLine(Mags, "It's closing behind you. Go!") },
        };
        public static readonly HouseLine[] BrokeThrough = { new HouseLine(Nadia, "Got you.") };
        /// <summary>Lena ended unsure: Theo opens a door off the corridor (a way past the stretching).</summary>
        public static readonly HouseLine[] TheoDoor =
        {
            new HouseLine(Theo, "Nadia! In here! It's a shortcut, I promise!"),
        };
        /// <summary>Lena condemned her: the corridor wants more speed and the house closes in faster.</summary>
        public static readonly HouseLine[] CondemnedCorridor =
        {
            new HouseLine(Mags, "It's angrier than before. Whatever she said to it, it's listening."),
        };
        public static readonly HouseLine[] TheUmbrella = { new HouseLine(Thought, "Her umbrella. By the door, where it always was.") };
        public static readonly HouseLine[] LenaAtTheWindow =
        {
            new HouseLine(Thought, "Up in the tower's round window, Lena. Watching me go."),
        };

        // ---------- Thrown out (the fall, or the house closing on her) ----------
        public static readonly HouseLine[] ThrownOutFalling =
        {
            new HouseLine(Mags, "Nadia-- I've got you. I'm pulling you out. Hold on."),
        };
        /// <summary>In the booth, after (the Day agent's booth can play these; the house plays them over black until then).</summary>
        public static readonly HouseLine[] ThrownOutBooth =
        {
            new HouseLine(Mags, "Breathe. You're out. You're in the booth."),
            new HouseLine(Nadia, "Lena-- Theo--"),
            new HouseLine(Mags, "His heart went through the roof when you fell. I had to. That's a night we don't get back."),
            new HouseLine(Mags, "And they'll remember how you left."),
        };
        /// <summary>The costs, said plainly (under the booth's lines).</summary>
        public const string ThrownOutCosts = "Thrown out of the dream.  Despair +1.  Lena's mind is made up against you.  Night 2's ending is lost.";
    }

    /// <summary>
    /// Night 2's house as a story, pure (docs/redesign/house.md): the way in, the pancakes, the hide-and-seek (the round's
    /// own rules are <see cref="HideAndSeekRound"/>), Theo's question, the argument with Lena, the escape, and its two ends:
    /// out of the front door, or thrown out of the dream. Thrown out costs Despair +1, hardens Lena's outcome to condemning
    /// and loses Night 2's ending (the umbrella and Lena at the window).
    /// </summary>
    public sealed class DreamHouseStory
    {
        /// <summary>The save flag set when Lena condemns Nadia (it stays, like the Despair flag).</summary>
        public const string CondemnsFlag = "vh_lena_condemns";
        /// <summary>The save flag set when she was thrown out of Night 2 (the chain keeps the rest: Chain.ForceOut's cost, a
        /// step of Despair, the crash call the next day, one fewer of Mags's nights).</summary>
        public const string ThrownOutFlag = "vh_n2_thrown_out";
        /// <summary>How many times Theo caught her (0-3), and the evidence cards she went into the battle with.</summary>
        public const string CatchesKey = "vh_n2_catches", CardsKey = "vh_n2_cards";
        /// <summary>Metres into the rain without the umbrella before it's the Despair choice; after Lena condemns her.</summary>
        public const float DespairDepth = 8f, CondemnedDespairDepth = 3f;
        public const float SoakTime = 5f, CondemnedSoakTime = 3f;
        /// <summary>The save entry holding Theo's answer ("honest", "comfort", "silence").</summary>
        public const string TheoAnswerKey = "vh_theo_dad_sad";
        /// <summary>The battle with Lena, by id.</summary>
        public const string LenaBattleId = "lena_night2";

        /// <summary>The memory ids (docs/redesign/house.md "The memories"; the evidence cards they become).</summary>
        public const string MemPlaceCard = "mem_fourth_chair", MemCalendar = "mem_calendar", MemBadDream = "mem_bad_dream",
            MemHeightMarks = "mem_height_marks", FactPhoto = "fact_photo", MemSaturdayPaper = DreamJournal.SaturdayPaper,
            MemRadio = "mem_radio_loop", MemLenaThursdays = "mem_lena_thursdays";
        /// <summary>The deductions: the calendar with the Saturday paper breaks Lena's L3b; the radio's forecast repeating
        /// is the loop (the card Night 1's street also gives).</summary>
        public const string DedSaturdayOff = "ded_saturday_off", DedSameSaturday = "ded_same_saturday";

        HousePhase _phase;
        ArgumentOutcome _outcome;
        TheoAnswer _theoAnswer;
        bool _thrownOut;

        public HousePhase Phase => _phase;
        public ArgumentOutcome Outcome => _outcome;
        public bool LenaCondemned => _outcome == ArgumentOutcome.Condemns;
        public TheoAnswer TheoAnswered => _theoAnswer;
        public bool ThrownOut => _thrownOut;

        public void Restart()
        {
            _phase = HousePhase.WayIn;
            _outcome = ArgumentOutcome.None;
            _theoAnswer = TheoAnswer.None;
            _thrownOut = false;
        }

        /// <summary>A chapter jump: straight to <paramref name="phase"/> (the argument keeps <paramref name="outcome"/>).</summary>
        public void JumpTo(HousePhase phase, ArgumentOutcome outcome = ArgumentOutcome.None)
        {
            Restart();
            _phase = phase;
            if (phase >= HousePhase.Escape) _outcome = outcome == ArgumentOutcome.None ? ArgumentOutcome.Unsure : outcome;
        }

        /// <summary>She sat down at the fourth chair: the pancakes.</summary>
        public bool SitDown()
        {
            if (_phase != HousePhase.WayIn) return false;
            _phase = HousePhase.Pancakes;
            return true;
        }

        public bool StartHideAndSeek()
        {
            if (_phase != HousePhase.Pancakes) return false;
            _phase = HousePhase.HideAndSeek;
            return true;
        }

        /// <summary>The round is over: Theo asks his question.</summary>
        public bool RoundOver()
        {
            if (_phase != HousePhase.HideAndSeek) return false;
            _phase = HousePhase.TheoAsks;
            return true;
        }

        /// <summary>Her answer (once a visit); then Elias doesn't come down and the argument begins.</summary>
        public bool AnswerTheo(TheoAnswer answer)
        {
            if (answer == TheoAnswer.None || _phase != HousePhase.TheoAsks) return false;
            _theoAnswer = answer;
            _phase = HousePhase.Argument;
            return true;
        }

        public static string AnswerText(TheoAnswer a) => a == TheoAnswer.None ? null : a.ToString().ToLowerInvariant();

        /// <summary>How the argument ends: the house throws her out either way.</summary>
        public bool Choose(ArgumentOutcome outcome)
        {
            if (_phase != HousePhase.Argument || outcome == ArgumentOutcome.None) return false;
            _outcome = outcome;
            _phase = HousePhase.Escape;
            return true;
        }

        /// <summary>Out of the front door, onto the hill.</summary>
        public void Escaped()
        {
            if (_phase == HousePhase.Escape) _phase = HousePhase.Out;
        }

        /// <summary>She fell, or the house closed on her: Mags pulls her out, and Lena's outcome hardens (unsure becomes
        /// condemning). False if she isn't in the escape.</summary>
        public bool Thrown()
        {
            if (_phase != HousePhase.Escape) return false;
            _thrownOut = true;
            _outcome = ArgumentOutcome.Condemns;
            _phase = HousePhase.ThrownOut;
            return true;
        }

        /// <summary>The evidence cards she carries into the battle: the memories found; the calendar becomes
        /// <see cref="DedSaturdayOff"/> with the Saturday paper (found tonight, or carried from Night 1); the radio becomes
        /// <see cref="DedSameSaturday"/>; the paper itself is half of the calendar's deduction, not a card.</summary>
        public static List<string> EvidenceFor(IEnumerable<string> found, bool hasSaturdayPaper)
        {
            var ids = new List<string>(found);
            bool paper = hasSaturdayPaper || ids.Contains(MemSaturdayPaper);
            var list = new List<string>();
            foreach (var id in ids)
            {
                string card = id;
                if (id == MemCalendar && paper) card = DedSaturdayOff;
                else if (id == MemRadio) card = DedSameSaturday;
                else if (id == MemSaturdayPaper) continue;
                if (!string.IsNullOrEmpty(card) && !list.Contains(card)) list.Add(card);
            }
            return list;
        }

        /// <summary>The front door's rain after this argument: condemned, the point of no return is nearer the door.</summary>
        public static DreamRainSpot RainFor(DreamRainSpot spot, bool condemned)
        {
            var s = spot.Copy();
            s.despairS = spot.rainStartS + (condemned ? CondemnedDespairDepth : DespairDepth);
            return s;
        }

        public static DreamRainDef RainDefFor(bool condemned) => new DreamRainDef { soakTime = condemned ? CondemnedSoakTime : SoakTime };
    }

    /// <summary>
    /// What Nadia carries from night to night (the shared journal of the dives, in the save under <see cref="Key"/> as a
    /// comma list of ids): Night 1 adds <see cref="SaturdayPaper"/> when she finds the Saturday paper.
    /// </summary>
    public static class DreamJournal
    {
        public const string Key = "vh_dream_journal";
        public const string SaturdayPaper = "mem_saturday_paper";

        public static bool Has(string entry, string id)
        {
            if (string.IsNullOrEmpty(entry) || string.IsNullOrEmpty(id)) return false;
            foreach (var part in entry.Split(','))
                if (part.Trim() == id) return true;
            return false;
        }

        public static string Add(string entry, string id)
        {
            if (string.IsNullOrEmpty(id) || Has(entry, id)) return entry ?? "";
            return string.IsNullOrEmpty(entry) ? id : entry + "," + id;
        }
    }
}

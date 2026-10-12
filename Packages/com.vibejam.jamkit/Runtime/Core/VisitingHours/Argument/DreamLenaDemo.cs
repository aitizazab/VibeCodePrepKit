using System;
using System.Collections.Generic;

namespace JamKit.Argument
{
    /// <summary>
    /// The prototype's demo battle: Dream-Lena at the kitchen table, Night 2 re-plot ("Lena isn't willing to let
    /// Elias go"). The premise map follows docs/ARGUMENT-BATTLES.md section 5 ("Lena, at the kitchen table").
    ///
    /// This is the battle's STRUCTURE only: ids, kinds, truth, keys, what reveals what, which evidence breaks what.
    /// Every word is in the prose file (Assets/JamKit/Templates/VisitingHours/Argument/Resources/ArgumentText/
    /// lena_night2.txt), which the creator edits directly; <see cref="ProseSource"/> supplies it (the Unity side reads
    /// the Resources file, tests read it from disk). The lines there are placeholders until the creator writes them;
    /// the brief is docs/ARGUMENT-PROSE-BRIEF.md (its ids are these).
    ///
    /// The map (the creator's rewrite, 2026-10-12; docs/NIGHTS-1-2.md, "The memories, mapped to the creator's battle"):
    ///   L1   fact TRUE   out there, Theo is dead                concede: reveals L1' (Theo's height marks back it)
    ///   L1b  link FALSE  so nothing's left for him out there   KEY: the fourth chair (falls back to L1'')
    ///   L1c  fact TRUE   where were you last Sunday?            concede
    ///   L2   fact TRUE   you don't know where I am              concede: reveals L2'. She asks a question (retort)
    ///   L2b  link HALF   so waking means searching forever      can't be broken tonight (the lesson)
    ///   L3   link HALF   he chose this                          ask: reveals L3'; ask again: L5
    ///   L3b  link FALSE  this is the life he wants              KEY: Saturday off (falls back to L3'')
    ///   L3c  link FALSE  then let him have his wish             KEY: the same Saturday (or Walt's truth, if won)
    ///   L4   fact TRUE   he called you first                    surfaces by script (painful) after L1'' or L3'; the police report
    ///   L5   link HALF   to him you're the bad dream            hidden deepest: a slip, or asking L3 twice; Theo's drawing
    /// Her anger rises with antagonising, retorts and presses (<see cref="ArgumentDef.pressAngers"/>), and her stance is
    /// the creator's two endings (<see cref="StanceRule.TwoEndings"/>): unsure, or condemning Nadia.
    /// Each card is in the tray only once its memory or deduction is found (the setup's journal ids).
    /// </summary>
    public static class DreamLenaDemo
    {
        public const string Id = "lena_night2";
        // The cards (docs/NIGHTS-1-2.md, the memory-to-card table): each is in the tray once the journal has its id.
        /// <summary>Nadia's place card at the fourth chair, in Theo's handwriting (found in the house). Breaks L1' (KEY).</summary>
        public const string FourthChair = "mem_fourth_chair";
        /// <summary>The fridge calendar's Sunday pancakes + Night 1's Saturday paper. Breaks L3' (KEY).</summary>
        public const string SaturdayOff = "ded_saturday_off";
        /// <summary>The street replaying Night 1 to the second (and Walt's ledger). Breaks L3'' (KEY).</summary>
        public const string SameSaturday = "ded_same_saturday";
        /// <summary>Theo's drawing, "THE BAD DREAM", found in his room. Against L5 (half true).</summary>
        public const string BadDream = "mem_bad_dream";
        /// <summary>Theo's height marks, stopping at ten. Backs L1 (true: concede it).</summary>
        public const string HeightMarks = "mem_height_marks";
        /// <summary>The photo wall: four of them at the lake (the "four" includes Nadia). Worldbuilding.</summary>
        public const string Photo = "fact_photo";
        /// <summary>Day 1's police report and its call log. L4 is true and painful: it stays.</summary>
        public const string PoliceReport = "fact_police_report";
        /// <summary>Walt's truth, if Night 1's battle convinced him: it also breaks L3''.</summary>
        public const string TruthWalt = WaltNight1.TruthId;
        public const string TruthLena = "truth_lena";
        /// <summary>Her keepsake (Heard out), a placeholder: her planisphere, the star wheel, set to her last Thursday night.</summary>
        public const string Planisphere = "pers_lena_planisphere";

        /// <summary>The prose file's name (Resources/ArgumentText/lena_night2).</summary>
        public const string ProseFile = Id;

        /// <summary>Every card but Walt's truth and "the same Saturday": without them L3'' can't break (Heard out at best).</summary>
        public static readonly string[] EvidenceWithoutWalt = { FourthChair, SaturdayOff, BadDream, HeightMarks, Photo, PoliceReport };

        /// <summary>Every card of the battle, in tray order.</summary>
        public static readonly string[] Cards = { FourthChair, SaturdayOff, SameSaturday, BadDream, HeightMarks, Photo, PoliceReport, TruthWalt };

        /// <summary>Where the prose file's text comes from (set by the Unity side, or by tests). Null: every line
        /// shows as its id in brackets.</summary>
        public static Func<string> ProseSource;

        /// <summary>The battle with the prose file's lines (<see cref="ProseSource"/>).</summary>
        public static ArgumentDef Build() => Build(ProseSource != null ? ProseSource() : null);

        /// <summary>The battle with these lines (the prose file's text). Ids with no line show as "[id]".</summary>
        public static ArgumentDef Build(string prose)
        {
            var d = Structure();
            ArgumentText.Apply(d, ArgumentText.Parse(prose));
            ArgumentText.ApplyVariants(d, prose);
            return d;
        }

        /// <summary>The battle with no words (the prose file fills them).</summary>
        public static ArgumentDef Structure()
        {
            var d = new ArgumentDef
            {
                id = Id,
                arena = "kitchen_table",
                truthId = TruthLena,
                personalId = Planisphere,
                placeholderLines = false,
                stanceRule = StanceRule.TwoEndings,
                pressAngers = true,
                calmShapes = new List<PressureShape> { PressureShape.Bloom, PressureShape.Wall },
                hotShapes = new List<PressureShape> { PressureShape.Wall, PressureShape.Bloom, PressureShape.Arc },
            };

            d.evidence.Add(new EvidenceDef { id = FourthChair, style = EvidenceStyle.Doodled });
            d.evidence.Add(new EvidenceDef { id = SaturdayOff, style = EvidenceStyle.Pinned });
            d.evidence.Add(new EvidenceDef { id = SameSaturday, style = EvidenceStyle.Pinned });
            d.evidence.Add(new EvidenceDef { id = BadDream, style = EvidenceStyle.Doodled });
            d.evidence.Add(new EvidenceDef { id = HeightMarks, style = EvidenceStyle.Doodled });
            d.evidence.Add(new EvidenceDef { id = Photo, style = EvidenceStyle.Stamped });
            d.evidence.Add(new EvidenceDef { id = PoliceReport, style = EvidenceStyle.Stamped });
            d.evidence.Add(new EvidenceDef { id = TruthWalt, style = EvidenceStyle.Gold });

            d.premises.Add(new PremiseDef { id = "L1", label = "L1", kind = PremiseKind.Fact, truth = PremiseTruth.True, concedeReveals = { "L1b" } });
            d.premises.Add(new PremiseDef
            {
                id = "L1b", label = "L1'", kind = PremiseKind.Link, truth = PremiseTruth.False, key = true, hidden = true, parent = "L1",
                challenges = { Challenge(FourthChair, SoundEffect.FallBack, "L1c", "rattled", "angry", "neutral", "angry") },
            });
            d.premises.Add(new PremiseDef { id = "L1c", label = "L1''", kind = PremiseKind.Fact, truth = PremiseTruth.True, hidden = true, parent = "L1b" });
            d.premises.Add(new PremiseDef
            {
                id = "L2", label = "L2", kind = PremiseKind.Fact, truth = PremiseTruth.True, concedeReveals = { "L2b" },
                retort = new RetortDef { expression = "angry" },
            });
            d.premises.Add(new PremiseDef
            {
                id = "L2b", label = "L2'", kind = PremiseKind.Link, truth = PremiseTruth.Half, hidden = true, parent = "L2", unbreakable = true,
            });
            d.premises.Add(new PremiseDef
            {
                id = "L3", label = "L3", kind = PremiseKind.Link, truth = PremiseTruth.Half, askReveals = { "L3b" }, askAgainReveals = { "L5" },
            });
            d.premises.Add(new PremiseDef
            {
                id = "L3b", label = "L3'", kind = PremiseKind.Link, truth = PremiseTruth.False, key = true, hidden = true, parent = "L3",
                challenges = { Challenge(SaturdayOff, SoundEffect.FallBack, "L3c", "rattled", "neutral", "angry", null) },
            });
            d.premises.Add(new PremiseDef
            {
                id = "L3c", label = "L3''", kind = PremiseKind.Link, truth = PremiseTruth.False, key = true, hidden = true, parent = "L3b",
                challenges =
                {
                    Challenge(TruthWalt, SoundEffect.Break, "", "softened", "neutral", "neutral", null),
                    Challenge(SameSaturday, SoundEffect.Break, "", "softened", "neutral", "neutral", null),
                },
            });
            d.premises.Add(new PremiseDef
            {
                id = "L4", label = "L4", kind = PremiseKind.Fact, truth = PremiseTruth.True, hidden = true, painful = true,
                surfacesAfter = { "L1c", "L3b" },
                retort = new RetortDef { expression = "angry" },
            });
            d.premises.Add(new PremiseDef
            {
                id = "L5", label = "L5", kind = PremiseKind.Link, truth = PremiseTruth.Half, hidden = true, revealedBySlip = true, parent = "L3",
                // Half true: he does fear the woman in scrubs, but she isn't what took them (the crash did). The drawing
                // takes it apart; conceding it is also fair. (The creator asked to talk this block through.)
                challenges = { Challenge(BadDream, SoundEffect.Break, "", "hurt", "neutral", "angry", null) },
            });
            return d;
        }

        /// <summary>A challenge with its three (or four, with an antagonising card) reasons; the words come from the file.</summary>
        static ChallengeDef Challenge(string evidence, SoundEffect effect, string target, string sound, string beside, string leap, string antagonise)
        {
            var c = new ChallengeDef { evidence = evidence, effect = effect, effectTarget = target };
            c.reasons.Add(new ReasonDef { kind = ReasonKind.Sound, expression = sound });
            c.reasons.Add(new ReasonDef { kind = ReasonKind.Beside, expression = beside });
            c.reasons.Add(new ReasonDef { kind = ReasonKind.NonSequitur, expression = leap });
            if (antagonise != null) c.reasons.Add(new ReasonDef { kind = ReasonKind.Antagonise, expression = antagonise });
            return c;
        }
    }
}

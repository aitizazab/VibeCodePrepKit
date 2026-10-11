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
    /// The map: claim "He stays. This is his home."
    ///   L1   fact TRUE   their son is dead out there           concede: reveals L1'
    ///   L1b  link FALSE  so nothing's left for him out there   KEY: the fourth chair (falls back to L1'')
    ///   L1c  fact TRUE   you stopped coming on Sundays          concede
    ///   L2   fact TRUE   nobody knows where I am                concede: reveals L2'. She asks a question (retort)
    ///   L2b  link HALF   so waking means searching forever      can't be broken tonight (the lesson)
    ///   L3   link HALF   he chose this                          ask: reveals L3'; ask again: L5
    ///   L3b  link FALSE  this is the life he wanted              KEY: Saturday off (falls back to L3'')
    ///   L3c  link FALSE  then let him have his wish              KEY: only Walt's truth or "the same Saturday"
    ///   L4   fact TRUE   he called you first                    surfaces by script (painful) after L1'' or L3'
    ///   L5   link HALF   to him you're the bad dream            hidden deepest: a slip, or asking L3 twice
    /// </summary>
    public static class DreamLenaDemo
    {
        public const string Id = "lena_night2";
        public const string FourthChair = "mem_fourth_chair";
        public const string SaturdayOff = "ded_saturday_off";
        public const string TruthWalt = "truth_walt";
        public const string SameSaturday = "ded_same_saturday";
        public const string WhyLenaCame = "ded_why_lena_came";
        public const string Chart = "fact_chart";
        public const string PoliceReport = "fact_police_report";
        public const string Photo = "fact_photo";
        public const string TruthLena = "truth_lena";
        public const string HairTie = "pers_lena_hair_tie";

        /// <summary>The prose file's name (Resources/ArgumentText/lena_night2).</summary>
        public const string ProseFile = Id;

        /// <summary>Every card but Walt's truth and "the same Saturday": without them L3'' can't break (Heard out at best).</summary>
        public static readonly string[] EvidenceWithoutWalt = { FourthChair, SaturdayOff, WhyLenaCame, Chart, PoliceReport, Photo };

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
                personalId = HairTie,
                placeholderLines = true,
                calmShapes = new List<PressureShape> { PressureShape.Bloom, PressureShape.Wall },
                hotShapes = new List<PressureShape> { PressureShape.Wall, PressureShape.Bloom, PressureShape.Arc },
            };

            d.evidence.Add(new EvidenceDef { id = FourthChair, style = EvidenceStyle.Doodled });
            d.evidence.Add(new EvidenceDef { id = SaturdayOff, style = EvidenceStyle.Pinned });
            d.evidence.Add(new EvidenceDef { id = TruthWalt, style = EvidenceStyle.Gold });
            d.evidence.Add(new EvidenceDef { id = SameSaturday, style = EvidenceStyle.Pinned });
            d.evidence.Add(new EvidenceDef { id = WhyLenaCame, style = EvidenceStyle.Pinned });
            d.evidence.Add(new EvidenceDef { id = Chart, style = EvidenceStyle.Stamped });
            d.evidence.Add(new EvidenceDef { id = PoliceReport, style = EvidenceStyle.Stamped });
            d.evidence.Add(new EvidenceDef { id = Photo, style = EvidenceStyle.Stamped });

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

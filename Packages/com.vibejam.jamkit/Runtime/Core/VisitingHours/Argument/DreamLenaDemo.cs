using System.Collections.Generic;

namespace JamKit.Argument
{
    /// <summary>
    /// The prototype's demo battle: Dream-Lena at the kitchen table, Night 2 re-plot ("Lena isn't willing to let
    /// Elias go"). The premise map follows docs/ARGUMENT-BATTLES.md section 5 ("Lena, at the kitchen table").
    ///
    /// EVERY LINE HERE IS A PLACEHOLDER. The creator writes the dialogue; these lines only carry the logic each line
    /// must carry (the claim, what each premise says, why a reason does or doesn't follow). <see cref="ArgumentDef.placeholderLines"/>
    /// is on, and the view says so on screen.
    ///
    /// The map: claim "He stays. This is his home."
    ///   L1   fact TRUE   "Out there, Theo is dead."                 concede: reveals L1'
    ///   L1'  link FALSE  "So there's nothing left for him out there." KEY: the fourth chair (falls back to L1'')
    ///   L1'' fact TRUE   "He set it every Sunday. You stopped coming." concede
    ///   L2   fact TRUE   "You don't know where I am. Neither does he." concede: reveals L2'. She asks "Then tell me
    ///                    where I am" (the antagonising reply dodges that)
    ///   L2'  link        "So waking means searching forever."       can't be broken tonight (the lesson)
    ///   L3   link HALF   "He chose this. I know him."               ask: reveals L3'
    ///   L3'  link FALSE  "This is the life he wanted."               KEY: Saturday off (falls back to L3'')
    ///   L3'' link FALSE  "Then let him have his wish."               KEY: only Walt's truth or "the same Saturday" breaks it
    ///   L4   fact TRUE   "He called you first..."                   surfaces by script (painful) after L1'' or L3'
    ///   L5   link HALF   "To him you're the bad dream."              hidden deepest: a slip (the paper plane) or asking L3 twice
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

        /// <summary>Every card but Walt's truth and "the same Saturday": without them L3'' can't break (Heard out at best).</summary>
        public static readonly string[] EvidenceWithoutWalt = { FourthChair, SaturdayOff, WhyLenaCame, Chart, PoliceReport, Photo };

        public static ArgumentDef Build()
        {
            var d = new ArgumentDef
            {
                id = Id,
                opponent = "Lena",
                arena = "kitchen_table",
                claim = "He stays. This is his home.",
                opening = "Sit down, Nadia. Pancakes are nearly done. He stays. This is his home.",
                noTouchReply = "That's not what I said. Listen to me.",
                pressClaimLine = "He is NOT staying.",
                pressClaimReply = "...Then take him. You always take everything.",
                concedeClaimLine = "...Maybe he should stay.",
                concedeClaimReply = "Thank you. Now eat something.",
                letItRestLine = "I hear you, Lena.",
                letItRestReply = "...You do, don't you. Here. Take this. Keep it for him.",
                truthId = TruthLena,
                truthText = "The Lena he remembers wouldn't let him hide. She'd want him to live, even without her.",
                personalId = HairTie,
                personalText = "Lena's apron hair tie.",
                convincedReply = "...She'd hate this. Me, keeping him here. She'd hate it.",
                heardOutReply = "I still think he's safer here. But I heard you.",
                pressedReply = "...You sound like the bad dream.",
                composureLostLine = "I can't... I can't breathe in here.",
                placeholderLines = true,
                calmShapes = new List<PressureShape> { PressureShape.Bloom, PressureShape.Wall },
                hotShapes = new List<PressureShape> { PressureShape.Wall, PressureShape.Bloom, PressureShape.Arc },
            };

            d.evidence.Add(new EvidenceDef { id = FourthChair, name = "the fourth chair", style = EvidenceStyle.Doodled, note = "A place set for someone who isn't here." });
            d.evidence.Add(new EvidenceDef { id = SaturdayOff, name = "Saturday off", style = EvidenceStyle.Pinned, note = "He was on shift the day this breakfast never happened." });
            d.evidence.Add(new EvidenceDef { id = TruthWalt, name = "Walt's truth", style = EvidenceStyle.Gold, note = "\"Patching it isn't fixing it.\"" });
            d.evidence.Add(new EvidenceDef { id = SameSaturday, name = "the same Saturday", style = EvidenceStyle.Pinned, note = "The same day, every night: the same flat tyre." });
            d.evidence.Add(new EvidenceDef { id = WhyLenaCame, name = "why Lena came out", style = EvidenceStyle.Pinned, note = "The call log and the voicemail." });
            d.evidence.Add(new EvidenceDef { id = Chart, name = "his chart", style = EvidenceStyle.Stamped, note = "Bed 14." });
            d.evidence.Add(new EvidenceDef { id = PoliceReport, name = "the police report", style = EvidenceStyle.Stamped, note = "The crash on Larch Street." });
            d.evidence.Add(new EvidenceDef { id = Photo, name = "the family photo", style = EvidenceStyle.Stamped, note = "Four of them, at the lake." });

            d.premises.Add(new PremiseDef
            {
                id = "L1", label = "L1", kind = PremiseKind.Fact, truth = PremiseTruth.True,
                text = "Out there, Theo is dead.",
                askReply = "You read the report. You know what happened to my boy.",
                concedeLine = "...Out there, Theo is gone.",
                concedeReply = "Yes. So out there, there's nothing left for him.",
                concedeReveals = { "L1b" },
                trueReply = "Don't. Don't you dare tell me that isn't true.",
                pressLine = "Stop saying that!",
                pressReply = "Saying it louder doesn't bring him back.",
            });
            d.premises.Add(new PremiseDef
            {
                id = "L1b", label = "L1'", kind = PremiseKind.Link, truth = PremiseTruth.False, key = true, hidden = true, parent = "L1",
                text = "So there's nothing left for him out there.",
                askReply = "What's out there? An empty house and a hospital bed.",
                pressLine = "There's plenty left!",
                pressReply = "Name one thing. You can't.",
                challenges =
                {
                    new ChallengeDef
                    {
                        evidence = FourthChair, effect = SoundEffect.FallBack, effectTarget = "L1c",
                        reasons =
                        {
                            new ReasonDef { kind = ReasonKind.Sound, text = "In here he still sets a place for someone. He sets it for me. There's someone out there.", reply = "...He set it every Sunday for a year. You stopped coming.", expression = "rattled" },
                            new ReasonDef { kind = ReasonKind.Beside, text = "Theo would want his dad to wake up.", reply = "Don't tell me what my son would want.", expression = "angry" },
                            new ReasonDef { kind = ReasonKind.NonSequitur, text = "He set a chair, so he's ready to leave.", reply = "Setting a table isn't packing a bag.", expression = "neutral" },
                            new ReasonDef { kind = ReasonKind.Antagonise, text = "You're a dream. A dream doesn't get a say in his life.", reply = "A dream. Is that all I am to you? Get out of my kitchen.", expression = "angry" },
                        },
                    },
                },
            });
            d.premises.Add(new PremiseDef
            {
                id = "L1c", label = "L1''", kind = PremiseKind.Fact, truth = PremiseTruth.True, hidden = true, parent = "L1b",
                text = "He set it every Sunday for a year. You stopped coming.",
                askReply = "Every Sunday. Your chair. Empty.",
                concedeLine = "I did. I stopped coming.",
                concedeReply = "...At least you admit it.",
                trueReply = "You know it's true. Don't argue with me about it.",
            });
            d.premises.Add(new PremiseDef
            {
                id = "L2", label = "L2", kind = PremiseKind.Fact, truth = PremiseTruth.True,
                text = "You don't know where I am. Neither does he.",
                question = "Then tell me where I am, Nadia.",
                askReply = "Missing. That's the word they used. Missing.",
                concedeLine = "No. I don't know where you are.",
                concedeReply = "So waking up means searching. Forever.",
                concedeReveals = { "L2b" },
                trueReply = "Then where am I? Go on.",
                retort = new RetortDef
                {
                    line = "You don't get to ask me that. You're not real. Stop acting like you are.",
                    reply = "Not real. Then whose kitchen is this? Whose pancakes are these? Say it to my face.",
                    expression = "angry",
                },
            });
            d.premises.Add(new PremiseDef
            {
                id = "L2b", label = "L2'", kind = PremiseKind.Link, truth = PremiseTruth.Half, hidden = true, parent = "L2",
                text = "So waking means searching forever.",
                askReply = "Every night he'd go looking. Every night he wouldn't find me.",
                concedeLine = "...It might.",
                concedeReply = "Then let him rest.",
                unbreakable = true,
                unbreakableReply = "You can't promise him an ending. Nobody can. Not tonight.",
            });
            d.premises.Add(new PremiseDef
            {
                id = "L3", label = "L3", kind = PremiseKind.Link, truth = PremiseTruth.Half,
                text = "He chose this. I know him.",
                askReply = "Twelve years. I know what he wants. This is the life he wanted.",
                askReveals = { "L3b" },
                askAgainReply = "...You want to know what he dreams about? The bad dream. A woman in scrubs who comes to take us away.",
                askAgainReveals = { "L5" },
                concedeLine = "You know him better than anyone.",
                concedeReply = "I do. So trust me.",
                trueReply = "You weren't there. I was.",
            });
            d.premises.Add(new PremiseDef
            {
                id = "L3b", label = "L3'", kind = PremiseKind.Link, truth = PremiseTruth.False, key = true, hidden = true, parent = "L3",
                text = "This is the life he wanted.",
                askReply = "Pancakes. Sunday. All of us at the table. What else would he want?",
                pressLine = "This isn't a life!",
                pressReply = "It's the only one he's got left.",
                challenges =
                {
                    new ChallengeDef
                    {
                        evidence = SaturdayOff, effect = SoundEffect.FallBack, effectTarget = "L3c",
                        reasons =
                        {
                            new ReasonDef { kind = ReasonKind.Sound, text = "He worked that Saturday. This is the breakfast he never got to have. That's a regret, not a choice.", reply = "...A regret. Fine. Then let him have his wish.", expression = "rattled" },
                            new ReasonDef { kind = ReasonKind.Beside, text = "He loved Saturdays with you.", reply = "Yes. That's my point.", expression = "neutral" },
                            new ReasonDef { kind = ReasonKind.NonSequitur, text = "He worked that day, so he never really wanted a family.", reply = "That's cruel, and it isn't even true.", expression = "angry" },
                        },
                    },
                },
            });
            var wish = new PremiseDef
            {
                id = "L3c", label = "L3''", kind = PremiseKind.Link, truth = PremiseTruth.False, key = true, hidden = true, parent = "L3b",
                text = "Then let him have his wish.",
                askReply = "One morning. Let him keep one morning.",
                pressLine = "He can't have it!",
                pressReply = "You'd take that from him too?",
            };
            foreach (var ev in new[] { TruthWalt, SameSaturday })
            {
                wish.challenges.Add(new ChallengeDef
                {
                    evidence = ev, effect = SoundEffect.Break,
                    reasons =
                    {
                        new ReasonDef { kind = ReasonKind.Sound, text = "He's had this morning forty-two times, and Walt patches the same flat every one. A wish you have to repair every night isn't peace.", reply = "...Forty-two. I thought it was the first time. Every time.", expression = "softened" },
                        new ReasonDef { kind = ReasonKind.Beside, text = "Walt misses him too.", reply = "Everyone misses him. That's why he should stay.", expression = "neutral" },
                        new ReasonDef { kind = ReasonKind.NonSequitur, text = "Walt said so, so it's true.", reply = "Walt says a lot of things.", expression = "neutral" },
                    },
                });
            }
            d.premises.Add(wish);
            d.premises.Add(new PremiseDef
            {
                id = "L4", label = "L4", kind = PremiseKind.Fact, truth = PremiseTruth.True, hidden = true, painful = true,
                text = "You're not here for him. He called you first. You didn't pick up. So he called me.",
                surfacesAfter = { "L1c", "L3b" },
                question = "Why didn't you pick up, Nadia?",
                askReply = "Three rings. Then he called me. And I went out in the rain.",
                concedeLine = "...I know. I didn't pick up.",
                concedeReply = "...Thank you for saying it.",
                trueReply = "Check your phone. It's all there.",
                retort = new RetortDef
                {
                    line = "Stop pretending you're her. You're a dream wearing her face.",
                    reply = "Then why are you shaking? Get out. Get out of my house.",
                    expression = "angry",
                },
            });
            d.premises.Add(new PremiseDef
            {
                id = "L5", label = "L5", kind = PremiseKind.Link, truth = PremiseTruth.Half, hidden = true, revealedBySlip = true,
                slipName = "a paper plane: THE BAD DREAM", parent = "L3",
                text = "To him, you're the bad dream: the woman in scrubs who takes people away.",
                askReply = "He drew you. Theo drew you, from what his dad said in his sleep.",
                concedeLine = "...Maybe I am.",
                concedeReply = "Then prove you're not.",
                trueReply = "Look at the drawing. Look at it.",
            });
            return d;
        }
    }
}

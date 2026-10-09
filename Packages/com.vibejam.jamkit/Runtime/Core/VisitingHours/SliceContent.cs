using System.Collections.Generic;
using JamKit.Patterns;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// The slice's default content, as fresh objects: journal entries, the pair table, Walt's
    /// argument and the opening's cues. The editor copies it into the template's config the first
    /// time (edit it there afterwards); the core tests check the real slice against it.
    /// Text is placeholder in the creator's direction (docs/SLICE-VISITING-HOURS.md).
    /// </summary>
    public static class SliceContent
    {
        // Entry ids the game and the ink use.
        public const string FactChart = "fact_chart";
        public const string FactEffects = "fact_effects";
        public const string FactPoliceReport = "fact_police_report";
        public const string FactPhoto = "fact_photo";
        public const string MemWaltAlive = "mem_walt_alive";
        public const string MemNewspaper = "mem_newspaper";
        public const string MemShopSign = "mem_shop_sign";
        public const string MemSaturday = "mem_saturday";
        public const string DedWaltDead = "ded_walt_dead";
        public const string DedDayBefore = "ded_day_before";
        public const string TruthWalt = "truth_walt";
        public const string PersWrench = "pers_wrench";

        public static List<JournalEntryDef> Entries() => new List<JournalEntryDef>
        {
            new JournalEntryDef
            {
                id = FactChart, kind = EntryKind.Fact, title = "His chart",
                text = "Charting: GCS 6. No sedation. Scans normal. He just won't wake up.",
                sayLine = "Your scans are normal, Eli. There's no reason you can't wake up.",
                icon = "prop_chart",
            },
            new JournalEntryDef
            {
                id = FactEffects, kind = EntryKind.Fact, title = "His effects bag",
                text = "His things, in a bag with his name on it. A keyring with a tag: \"W.A. workshop\". A folded memorial card: Walter Abernathy, 1941–2015.",
                sayLine = "These are his keys. Your workshop, Walt.",
                icon = "prop_effects_bag",
            },
            new JournalEntryDef
            {
                id = FactPoliceReport, kind = EntryKind.Fact, title = "The police report",
                text = "An envelope at the station, for the family. Route 9, Sunday 23 August, 6:41 AM: the car crossed the median. Driver: Elias Voss, critical. Lena Voss and Theo Voss, dead at the scene.\nPhone log: 5:52, call to Nadia Voss, not answered. 5:58, call to Lena Voss, 2 minutes.",
                scribble = "I should have",
                sayLine = "There's a police report, Walt.",
                icon = "prop_police_envelope",
            },
            new JournalEntryDef
            {
                id = FactPhoto, kind = EntryKind.Fact, title = "The photo",
                text = "On his bedside table: Eli, Lena and Theo on the beach, laughing so hard Theo's a blur.",
                sayLine = "He has a family. Had.",
                icon = "prop_family_photo",
            },
            new JournalEntryDef
            {
                id = MemWaltAlive, kind = EntryKind.Memory, title = "Walt at the grill",
                text = "Mr. Abernathy at the barbecue, waving at me with the tongs. Flat cap, apron, the moustache. Exactly like when we were kids.",
                sayLine = "You're at the grill, waving. Like always.",
                icon = "portrait_walt_neutral",
            },
            new JournalEntryDef
            {
                id = MemNewspaper, kind = EntryKind.Memory, title = "The newspaper",
                text = "The Courier, still in its plastic on a porch. Sunday, 23 August. The morning of the crash.",
                sayLine = "The paper on the porch says Sunday.",
                icon = "prop_newspaper",
            },
            new JournalEntryDef
            {
                id = MemShopSign, kind = EntryKind.Memory, title = "The shop sign",
                text = "\"Abernathy Cycles, since 1971.\" Walt's garage. Eli spent every summer in there with grease up to his elbows.",
                sayLine = "Abernathy Cycles. Since 1971.",
                icon = "flat_bike_garage",
            },
            new JournalEntryDef
            {
                id = MemSaturday, kind = EntryKind.Memory, title = "Saturday",
                text = "The radio says it's Saturday. Mrs. Lindqvist says it's Saturday. Everyone here is very sure it's Saturday.",
                sayLine = "Everyone keeps telling me it's Saturday.",
                icon = "billboard_neighbor_roses",
            },
            new JournalEntryDef
            {
                id = DedWaltDead, kind = EntryKind.Deduction, title = "Walt can't be here",
                text = "Walt died in 2015. He can't be here.",
                sayLine = "Walt. You died in 2015. Eli gave the eulogy.",
                icon = "prop_memorial_card",
            },
            new JournalEntryDef
            {
                id = DedDayBefore, kind = EntryKind.Deduction, title = "The day before",
                text = "He's living the day before the crash, over and over.",
                sayLine = "Not this. You can't fix this by doing it again.",
                icon = "prop_newspaper",
            },
            new JournalEntryDef
            {
                id = TruthWalt, kind = EntryKind.Truth, title = "Why Walt",
                text = "He brings Walt back because Walt is the one who told him you can fix anything.",
                icon = "prop_wrench",
            },
            new JournalEntryDef
            {
                id = PersWrench, kind = EntryKind.Personal, title = "Walt's wrench",
                text = "He pressed it into my hand, warm from the sun. \"For the next thing that needs fixing.\"",
                icon = "prop_wrench",
            },
        };

        public static List<DeductionPairDef> Pairs() => new List<DeductionPairDef>
        {
            new DeductionPairDef
            {
                a = FactEffects, b = MemWaltAlive, result = DedWaltDead,
                question = "The card in Eli's bag says Walter Abernathy, 1941–2015. And Walt is at the grill, waving.",
            },
            new DeductionPairDef
            {
                a = MemNewspaper, b = MemSaturday, result = DedDayBefore,
                question = "The paper on the porch is from the morning of the crash. Everyone says it's Saturday.",
            },
        };

        /// <summary>What Nadia thinks when two entries don't go together (one at a time, in turn).</summary>
        public static List<string> GentleLines() => new List<string>
        {
            "Those don't go together. Not yet.",
            "I'm reaching. Leave it.",
            "No. That's not it.",
        };

        static VolleyDef V(PatternDef p, float x, float y, float sweep = 0f, float hz = 0.3f) =>
            new VolleyDef { pattern = p, originX = x, originY = y, sweepX = sweep, sweepHz = hz };

        /// <summary>Walt Abernathy: a practice volley, then four statements; the third is the key.</summary>
        public static BattleDef WaltBattle() => new BattleDef
        {
            id = "walt",
            speaker = "Walt",
            composure = 5,
            practiceText = "Watch the pattern first.",
            practiceDuration = 5.5f,
            practice = new List<VolleyDef>
            {
                V(new PatternDef { shape = PatternShape.Spread, count = 5, spreadDegrees = 50f, baseAngleDegrees = -90f, shots = 3, shotInterval = 1.4f, speed = 1.5f, lifetime = 6f, styleIndex = 0 }, 0f, 2.7f),
            },
            statements = new List<StatementDef>
            {
                new StatementDef
                {
                    text = "\"Go home, Nadia.\"",
                    expression = "neutral",
                    duration = 9f,
                    volleys = new List<VolleyDef>
                    {
                        // Gentle rain: two staggered lines falling, drifting side to side.
                        V(new PatternDef { shape = PatternShape.Line, count = 4, lineWidth = 8f, baseAngleDegrees = -90f, shots = 22, shotInterval = 0.42f, speed = 2f, jitterDegrees = 5f, lifetime = 6f, styleIndex = 0 }, 0f, 2.7f, 1f, 0.23f),
                        V(new PatternDef { shape = PatternShape.Line, count = 4, lineWidth = 8f, baseAngleDegrees = -90f, shots = 21, shotInterval = 0.42f, startDelay = 0.21f, speed = 2f, jitterDegrees = 5f, lifetime = 6f, styleIndex = 0 }, 1f, 2.7f, 1f, 0.31f),
                    },
                    offers = new List<string> { FactPhoto, FactChart },
                },
                new StatementDef
                {
                    text = "\"He's fine here. Look at him.\"",
                    expression = "neutral",
                    duration = 9.5f,
                    volleys = new List<VolleyDef>
                    {
                        // Slow rings of reassurance, from near the top: closer (pressing) is denser.
                        V(new PatternDef { shape = PatternShape.Ring, count = 14, shots = 6, shotInterval = 1.35f, rotatePerShotDegrees = 12.86f, speed = 1.25f, lifetime = 8f, spawnRadius = 0.3f, styleIndex = 1 }, 0f, 1.6f),
                        V(new PatternDef { shape = PatternShape.Ring, count = 10, shots = 4, shotInterval = 2f, startDelay = 0.7f, speed = 0.9f, lifetime = 8f, spawnRadius = 0.3f, styleIndex = 1 }, 0f, 2.1f, 2.5f, 0.12f),
                    },
                    offers = new List<string> { FactChart, FactPhoto },
                },
                new StatementDef
                {
                    text = "\"I've always been on this street.\"",
                    expression = "firm",
                    duration = 11f,
                    volleys = new List<VolleyDef>
                    {
                        // Walls of denial, each with a gap on one side, alternating.
                        V(new PatternDef { shape = PatternShape.Line, count = 17, lineWidth = 5f, baseAngleDegrees = -90f, shots = 5, shotInterval = 2.6f, speed = 1.5f, lifetime = 7f, styleIndex = 2 }, -1.5f, 2.7f),
                        V(new PatternDef { shape = PatternShape.Line, count = 17, lineWidth = 5f, baseAngleDegrees = -90f, shots = 4, shotInterval = 2.6f, startDelay = 1.3f, speed = 1.5f, lifetime = 7f, styleIndex = 2 }, 1.5f, 2.7f),
                    },
                    key = DedWaltDead,
                    offers = new List<string> { DedWaltDead, MemShopSign, FactPhoto },
                    crackLine = "...I know, love. He knows too. He just doesn't want to.",
                    crackExpression = "sad",
                },
                new StatementDef
                {
                    text = "\"You can fix anything, if you're patient enough.\"",
                    expression = "neutral",
                    duration = 10f,
                    volleys = new List<VolleyDef>
                    {
                        // His old lesson: a patient spiral.
                        V(new PatternDef { shape = PatternShape.Spiral, count = 3, shots = 68, shotInterval = 0.14f, rotatePerShotDegrees = 11f, speed = 1.5f, lifetime = 7f, spawnRadius = 0.25f, styleIndex = 3 }, 0f, 1f),
                    },
                    key = DedDayBefore,
                    optional = true,
                    offers = new List<string> { DedDayBefore, DedWaltDead, MemNewspaper },
                    crackLine = "...No. Some things you can't. He never learned that one from me.",
                    crackExpression = "sad",
                },
            },
            convincedEntry = TruthWalt,
            heardOutEntry = PersWrench,
        };

        /// <summary>The opening, before the title (placeholder timing for the placeholder voicemail;
        /// the voicemail's subtitles stretch to the real recording's length).</summary>
        public static List<OpeningCue> Opening() => new List<OpeningCue>
        {
            new OpeningCue { at = 0f, kind = OpeningCueKind.Ring },
            new OpeningCue { at = 1.8f, kind = OpeningCueKind.EyesOpen, seconds = 4f },
            new OpeningCue { at = 3.2f, kind = OpeningCueKind.PhoneText, text = "Elias calling…" },
            new OpeningCue { at = 8.6f, kind = OpeningCueKind.RingStop },
            new OpeningCue { at = 8.8f, kind = OpeningCueKind.PhoneText, text = "Missed call: Elias, 5:52 AM" },
            new OpeningCue { at = 10.8f, kind = OpeningCueKind.PhoneText, text = "Voicemail: Elias" },
            new OpeningCue { at = 11.2f, kind = OpeningCueKind.Voice, text = "vo_elias_voicemail" },
            new OpeningCue { at = 11.2f, kind = OpeningCueKind.Subtitle, text = "Hey, Nads. It's me. Could I bother you for a..." },
            new OpeningCue { at = 12.6f, kind = OpeningCueKind.SitUp, seconds = 1.6f },
            new OpeningCue { at = 15.0f, kind = OpeningCueKind.Subtitle, text = "...it's five in the morning." },
            new OpeningCue { at = 15.3f, kind = OpeningCueKind.SinkBack, seconds = 2.2f },
            new OpeningCue { at = 17.2f, kind = OpeningCueKind.Subtitle, text = "You're asleep. And your phone's probably dead again, isn't it." },
            new OpeningCue { at = 21.0f, kind = OpeningCueKind.Subtitle, text = "(a small laugh) No cabs out here at this hour. Figures." },
            new OpeningCue { at = 25.0f, kind = OpeningCueKind.Subtitle, text = "Look. About yesterday. I'm sorry. I shouldn't have said that." },
            new OpeningCue { at = 30.0f, kind = OpeningCueKind.Subtitle, text = "...G'night." },
            new OpeningCue { at = 32.0f, kind = OpeningCueKind.PhoneHide },
            new OpeningCue { at = 32.0f, kind = OpeningCueKind.ClearSubtitle },
            new OpeningCue { at = 32.6f, kind = OpeningCueKind.Voice, text = "vo_nadia_sleep_in" },
            new OpeningCue { at = 32.6f, kind = OpeningCueKind.Subtitle, speaker = "Nadia", text = "...sleep in." },
            new OpeningCue { at = 34.6f, kind = OpeningCueKind.ClearSubtitle },
            new OpeningCue { at = 35.0f, kind = OpeningCueKind.FadeOut, seconds = 3f },
            new OpeningCue { at = 38.8f, kind = OpeningCueKind.End },
        };

        /// <summary>The things Nadia can look at with E: where, what the prompt calls it, the ink knot
        /// it plays and the entry that knot writes in the journal (empty for none).</summary>
        public static List<ExamineDef> Examinables() => new List<ExamineDef>
        {
            new ExamineDef { id = "chart", area = SliceArea.Hospital, label = "the chart", knot = "examine_chart", records = FactChart },
            new ExamineDef { id = "effects", area = SliceArea.Hospital, label = "his effects bag", knot = "examine_effects", records = FactEffects },
            new ExamineDef { id = "police_report", area = SliceArea.Hospital, label = "the envelope", knot = "examine_police_report", records = FactPoliceReport },
            new ExamineDef { id = "photo", area = SliceArea.Hospital, label = "the photo", knot = "examine_photo", records = FactPhoto },
            new ExamineDef { id = "elias", area = SliceArea.Hospital, label = "Elias", knot = "examine_elias" },
            new ExamineDef { id = "station", area = SliceArea.Hospital, label = "the nurses' station", knot = "examine_station" },
            new ExamineDef { id = "stairwell", area = SliceArea.Hospital, label = "the stairwell door", knot = "stairwell" },
            new ExamineDef { id = "newspaper", area = SliceArea.Street, label = "the newspaper", knot = "examine_newspaper", records = MemNewspaper },
            new ExamineDef { id = "shop_sign", area = SliceArea.Street, label = "the garage", knot = "examine_shop_sign", records = MemShopSign },
            new ExamineDef { id = "roses", area = SliceArea.Street, label = "the woman watering roses", knot = "examine_roses", records = MemSaturday },
            new ExamineDef { id = "grill", area = SliceArea.Street, label = "the man at the grill", knot = "examine_grill", records = MemSaturday },
            new ExamineDef { id = "dog", area = SliceArea.Street, label = "the dog", knot = "examine_dog" },
            new ExamineDef { id = "walt", area = SliceArea.Street, label = "Walt", knot = "walt_intro" },
        };

        /// <summary>Knots the game plays by itself (not from an examinable).</summary>
        public const string KnotDay1Intro = "day1_intro";
        public const string KnotBooth = "booth_mags";
        public const string KnotBoothAfterJournal = "booth_after_journal";
        public const string KnotNightArrive = "night_arrive";
        public const string KnotWaltWave = "walt_wave";
        public const string KnotWaltRushing = "walt_rushing";
        public const string KnotWaltIntro = "walt_intro";
        public const string KnotConvinced = "walt_convinced";
        public const string KnotHeardOut = "walt_heard";
        public const string KnotPressed = "walt_pressed";
        public const string KnotLost = "walt_lost";
        public const string KnotSliceEnd = "slice_end";

        public static string OutcomeKnot(BattleOutcome outcome)
        {
            switch (outcome)
            {
                case BattleOutcome.Convinced: return KnotConvinced;
                case BattleOutcome.HeardOut: return KnotHeardOut;
                case BattleOutcome.Pressed: return KnotPressed;
                case BattleOutcome.ComposureLost: return KnotLost;
                default: return "";
            }
        }

        /// <summary>Every knot the game can play.</summary>
        public static List<string> AllKnots()
        {
            var list = new List<string>
            {
                KnotDay1Intro, KnotBooth, KnotBoothAfterJournal, KnotNightArrive, KnotWaltWave, KnotWaltRushing, KnotWaltIntro,
                KnotConvinced, KnotHeardOut, KnotPressed, KnotLost, KnotSliceEnd,
            };
            foreach (var e in Examinables()) if (!list.Contains(e.knot)) list.Add(e.knot);
            return list;
        }

        /// <summary>Everything the slice can find before Walt's argument, deductions included (tests and the smoke run).</summary>
        public static string[] KnownBeforeWalt() => new[]
        {
            FactChart, FactEffects, FactPoliceReport, FactPhoto,
            MemWaltAlive, MemNewspaper, MemShopSign, MemSaturday,
            DedWaltDead, DedDayBefore,
        };

        /// <summary>The ticker under the title's TV picture.</summary>
        public const string NewsTicker = "ROUTE 9: TWO DEAD, DRIVER CRITICAL AFTER EARLY-MORNING CRASH";
    }
}

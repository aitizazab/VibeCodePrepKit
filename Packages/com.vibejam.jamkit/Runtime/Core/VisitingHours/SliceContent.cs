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
        public const string FactBouquet = "fact_bouquet";
        public const string FactDidLenaGetOut = "fact_did_lena";
        public const string FactVisitor = "fact_visitor";
        public const string MemWaltAlive = "mem_walt_alive";
        public const string MemNewspaper = "mem_newspaper";
        public const string MemShopSign = "mem_shop_sign";
        public const string MemSaturday = "mem_saturday";
        public const string DedWaltDead = "ded_walt_dead";
        public const string DedDayBefore = "ded_day_before";
        public const string TruthWalt = "truth_walt";
        public const string PersWrench = "pers_wrench";

        /// <summary>Every journal entry: the slice's, then the bigger floor's (<see cref="DayContent.Entries"/>).</summary>
        public static List<JournalEntryDef> Entries()
        {
            var list = SliceEntries();
            list.AddRange(DayContent.Entries());
            return list;
        }

        static List<JournalEntryDef> SliceEntries() => new List<JournalEntryDef>
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
                // In line with script/opening-script.md and docs/VISITING-HOURS.md: his voicemail to Nadia around 3 AM, the
                // car caught in a gang crossfire (the Larpaca gang) on the drive home, the surname Lasname.
                // Lena is missing, not dead (the creator, settled 2026-10-12); Theo died at the scene (his height marks stop at ten).
                text = "An envelope at the station, for the family. Route 9, about 3:40 AM: the car was caught in a crossfire (gang violence: the Larpaca gang). Driver: Elias Lasname, critical. Passenger: Theo Lasname, 10, dead at the scene. Lena Lasname: not at the scene. The passenger door was open. Listed as missing.\nPhone log: 3:03, call to Nadia Lasname, not answered. 3:06, call to Lena Lasname, 2 minutes.",
                scribble = "I should have",
                sayLine = "There's a police report, Walt.",
                icon = "prop_police_envelope",
            },
            new JournalEntryDef
            {
                id = FactPhoto, kind = EntryKind.Fact, title = "The photo",
                text = "On his locker: Eli, Lena and Theo at the lake last summer, laughing so hard Theo's a blur. I took it.",
                sayLine = "He has a family. Had.",
                icon = "prop_family_photo",
            },
            // PLACEHOLDER (agent-written, for the creator to rewrite): the chain's new Day 1 and Day 2 facts (docs/NIGHTS-1-2.md).
            new JournalEntryDef
            {
                id = FactBouquet, kind = EntryKind.Fact, title = "The bouquet",
                text = "Lilies on the windowsill of room 14, too big for it. The card: \"With our deepest sympathies. LeBaget, Community Partnerships.\" Nobody here knows who brought them.",
                sayLine = "Somebody from LeBaget sent flowers.",
                icon = "",
            },
            new JournalEntryDef
            {
                id = FactDidLenaGetOut, kind = EntryKind.Fact, title = "\"Did Lena get out?\"",
                text = "Rafael was on when they brought him in. Elias said one thing before he went under: \"Did Lena get out?\"",
                sayLine = "You asked if Lena got out.",
                icon = "",
            },
            new JournalEntryDef
            {
                id = FactVisitor, kind = EntryKind.Fact, title = "The woman from LeBaget",
                text = "Corinne Vasseur, LeBaget's Community Partnerships, with fruit Elias can't eat. \"Has he said anything?\"",
                sayLine = "LeBaget came asking what you've said.",
                icon = "",
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

        /// <summary>
        /// The opening's cues (docs/OPENING-AND-TITLE.md's shot list). Cues before the voicemail are
        /// absolute; everything after it hangs off the recordings' markers ("voicemail:drop",
        /// "voicemail:goodnight", "voicemail:end", "news:start", "news:end"), so re-timing a new take
        /// means moving the markers in <see cref="VoicemailTrack"/> only.
        /// </summary>
        public static List<OpeningCue> OpeningCues()
        {
            var c = new List<OpeningCue>();
            void At(float at, OpeningCueKind kind, string text = "", float seconds = 0f, float value = 1f, string note = "") =>
                c.Add(new OpeningCue { at = at, kind = kind, text = text, seconds = seconds, value = value, note = note });
            void On(string anchor, float offset, OpeningCueKind kind, string text = "", float seconds = 0f, float value = 1f, string note = "") =>
                c.Add(new OpeningCue { anchor = anchor, offset = offset, kind = kind, text = text, seconds = seconds, value = value, note = note });

            // Black. Silence, then the phone (two rings).
            At(0f, OpeningCueKind.Camera, "pillow");
            At(0f, OpeningCueKind.Phone, "incoming");
            At(0f, OpeningCueKind.Lights, "", 0f, 1f);
            At(0.5f, OpeningCueKind.Ring, note: "two rings before her eyes open");
            // Fade up from her pillow: the dark bedroom, the streetlight through the blinds on the ceiling.
            At(3.0f, OpeningCueKind.Fade, "in", 2.2f);
            At(3.5f, OpeningCueKind.Sfx, "vh_rustle", 0f, 0.6f);
            // She turns towards the nightstand; the phone glows: "Elias".
            At(4.3f, OpeningCueKind.Camera, "nightstand", 1.5f);
            At(5.4f, OpeningCueKind.Arm, "reach", 1.35f, note: "her hand comes out from under the blanket");
            At(5.6f, OpeningCueKind.Sfx, "vh_rustle", 0f, 0.8f);
            // She grabs it just as the ringing stops: missed call.
            At(6.8f, OpeningCueKind.RingStop);
            At(6.8f, OpeningCueKind.Arm, "grab", 0.25f);
            At(6.85f, OpeningCueKind.Phone, "missed");
            At(7.1f, OpeningCueKind.Arm, "lift", 0.9f, note: "she holds the phone near");
            // Her thumb plays the voicemail.
            At(8.25f, OpeningCueKind.Arm, "tap", 0.3f);
            At(8.4f, OpeningCueKind.Sfx, "vh_tap", 0f, 0.7f);
            At(8.45f, OpeningCueKind.Phone, "playing");
            At(8.6f, OpeningCueKind.Voice, "voicemail", note: "close and clear while she holds it");

            // "Well, it's three in the morning. You're probably asleep...": she sets the phone down and rolls away;
            // her eyes close (a slow blink, then they stay shut) and his voice fades as she drifts off.
            On("voicemail:drop", 0f, OpeningCueKind.Arm, "drop", 0.55f);
            On("voicemail:drop", 0.5f, OpeningCueKind.Sfx, "vh_clunk", 0f, 0.9f);
            On("voicemail:drop", 0.6f, OpeningCueKind.Arm, "hide", 0.3f);
            On("voicemail:drop", 0.7f, OpeningCueKind.Camera, "away", 1.0f);
            On("voicemail:drop", 0.75f, OpeningCueKind.Sfx, "vh_rustle_big", 0f, 1f);
            On("voicemail:drop", 1.0f, OpeningCueKind.VoiceLevel, "", 3.0f, 0f, note: "his voice fades as she drifts off");
            On("voicemail:drop", 1.6f, OpeningCueKind.Fade, "out", 0.7f, note: "her eyes close...");
            On("voicemail:drop", 2.4f, OpeningCueKind.Fade, "in", 0.6f, note: "...open, heavy...");
            On("voicemail:drop", 3.2f, OpeningCueKind.Fade, "out", 1.2f, note: "...and stay shut");
            // Cut: the phone from above, still playing; Nadia a shape under the blanket, asleep. His voice back, clear
            // (not through the phone's speaker: his lines must be understood).
            On("voicemail:drop", 4.6f, OpeningCueKind.Camera, "overhead", 0f);
            On("voicemail:drop", 4.6f, OpeningCueKind.Speaker, "", 0f, 0f, note: "his own voice, clear");
            On("voicemail:drop", 4.6f, OpeningCueKind.Breathing, "on");
            On("voicemail:drop", 4.6f, OpeningCueKind.VoiceLevel, "", 0.8f, 1f);
            On("voicemail:drop", 4.6f, OpeningCueKind.Fade, "in", 1.5f);
            On("voicemail:drop", 4.7f, OpeningCueKind.Phone, "dim", 20f, 0.45f, note: "the screen slowly dims");
            // "G'night": the room goes, the phone's screen goes out last.
            On("voicemail:goodnight", 0.2f, OpeningCueKind.Lights, "", 2.0f, 0f);
            On("voicemail:goodnight", 2.4f, OpeningCueKind.Phone, "off", 0.6f);
            On("voicemail:goodnight", 3.0f, OpeningCueKind.Fade, "out", 0.4f);
            On("voicemail:end", 0.6f, OpeningCueKind.Breathing, "off");
            // Black, held. Then the music blooms.
            On("voicemail:end", 3.0f, OpeningCueKind.Music, "", 5f, note: "the placeholder sad pad (the title music)");
            // The news report, from the tablet's speaker (once). Out of the black the title's pieces arrive one after
            // another: the coffee, the badge, the notepad (and its pen), the tablet, the table under them, the room.
            On("voicemail:end", 6.0f, OpeningCueKind.Camera, "table", 0f);
            On("voicemail:end", 6.0f, OpeningCueKind.Voice, "news");
            On("news:start", 0f, OpeningCueKind.Fade, "in", 2.0f);
            // Paced by the report's own length (a shorter or longer take re-paces them): one after another, no long gaps.
            On("news:2%", 0f, OpeningCueKind.Show, "mug", 1.8f);
            On("news:14%", 0f, OpeningCueKind.Show, "badge", 1.8f);
            On("news:27%", 0f, OpeningCueKind.Show, "notepad", 1.8f);
            On("news:27%", 0.6f, OpeningCueKind.Show, "pen", 1.8f);
            On("news:41%", 0f, OpeningCueKind.Show, "tablet", 2.0f);
            On("news:54%", 0f, OpeningCueKind.Show, "table", 2.2f);
            On("news:67%", 0f, OpeningCueKind.Show, "room", 2.5f);
            // The end of the report: the tablet's play button is Play.
            On("news:end", 0f, OpeningCueKind.Interactive);
            On("news:end", 0.2f, OpeningCueKind.End);
            return c;
        }

        /// <summary>
        /// Elias's voicemail and its markers, timed on the creator's first take (32.3 s, from its pauses and a
        /// Whisper transcript; the take mixes the old and new scripts, so the subtitles are what he says in it).
        /// Playback skips the take's 3.9 s quiet lead-in. "drop" is where Nadia drops the phone ("Well, it's five
        /// [three] in the morning..."), "goodnight" the "G'night". A re-record re-times them (docs/OPENING-AND-TITLE.md).
        /// </summary>
        public static VoiceTrack VoicemailTrack() => new VoiceTrack
        {
            name = "voicemail",
            clip = "vo_elias_voicemail",
            speaker = "Elias",
            recordedLength = 32.32f,
            trimStart = 3.9f,
            markers = new List<VoiceMarker>
            {
                new VoiceMarker { name = "hey", at = 4.5f, subtitle = "Hey, Nads. It's me. Could I bother you for a..." },
                new VoiceMarker { name = "drop", at = 10.25f, subtitle = "...Well, it's five in the morning. You're probably asleep," },
                new VoiceMarker { name = "dead", at = 12.5f, subtitle = "and you'd pick up if your phone wasn't dead." },
                new VoiceMarker { name = "look", at = 18.6f, subtitle = "Look. About yesterday. I'm sorry. I shouldn't have said that." },
                new VoiceMarker { name = "cab", at = 24.25f, subtitle = "I'll see if I can get a cab back home." },
                new VoiceMarker { name = "lena", at = 26.5f, subtitle = "Or maybe just call Lena..." },
                new VoiceMarker { name = "goodnight", at = 30.2f, subtitle = "...G'night." },
            },
        };

        /// <summary>The news report (17.60 s: a clear newsreader, cut by clauses; the Larpaca-gang wording). No markers needed: the table's pieces
        /// hang off shares of its length ("news:40%"), its start and its end, so another take re-paces them.</summary>
        public static VoiceTrack NewsTrack() => new VoiceTrack
        {
            name = "news",
            clip = "vo_news_anchor",
            speaker = "News",
            recordedLength = 17.60f,
            markers = new List<VoiceMarker>(),
        };

        /// <summary>The pieces of the title table, in the order they fade in (the room last: the floor and the wall).</summary>
        public static readonly string[] TablePieces = { "mug", "badge", "notepad", "pen", "tablet", "table", "room" };

        /// <summary>The camera poses the opening uses.</summary>
        public static readonly string[] CameraPoses = { "pillow", "nightstand", "away", "overhead", "table" };

        /// <summary>The tablet's scrolling headline (script/opening-script.md). LeBaget is a corporation and Larpaca is the
        /// gang (the creator's settled names); an old "WeWork" placeholder there becomes "Larpaca".</summary>
        public static string Headline(string scriptLine)
        {
            string s = string.IsNullOrEmpty(scriptLine) ? NewsHeadline : scriptLine.Trim();
            return s.Replace("WeWork", "Larpaca").Replace("WEWORK", "LARPACA");
        }

        /// <summary>The things Nadia can look at with E: where, what the prompt calls it, the ink knot
        /// it plays and the entry that knot writes in the journal (empty for none).</summary>
        /// <summary>Every examinable: the slice's, then the bigger floor's (<see cref="DayContent.Examinables"/>).</summary>
        public static List<ExamineDef> Examinables()
        {
            var list = SliceExaminables();
            list.AddRange(DayContent.Examinables());
            return list;
        }

        static List<ExamineDef> SliceExaminables() => new List<ExamineDef>
        {
            new ExamineDef { id = "chart", area = SliceArea.Hospital, label = "the chart", knot = "examine_chart", records = FactChart },
            new ExamineDef { id = "effects", area = SliceArea.Hospital, label = "his effects bag", knot = "examine_effects", records = FactEffects },
            new ExamineDef { id = "police_report", area = SliceArea.Hospital, label = "the envelope", knot = "examine_police_report", records = FactPoliceReport },
            new ExamineDef { id = "photo", area = SliceArea.Hospital, label = "the photo", knot = "examine_photo", records = FactPhoto },
            new ExamineDef { id = "elias", area = SliceArea.Hospital, label = "Elias", knot = "examine_elias" },
            new ExamineDef { id = "station", area = SliceArea.Hospital, label = "the nurses' station", knot = "examine_station" },
            new ExamineDef { id = "stairwell", area = SliceArea.Hospital, label = "the stairwell door", knot = "stairwell" },
            // Day 1 in the chain (docs/NIGHTS-1-2.md): the handover, the tutorial duty round (two checks: the creator wants it
            // a bit shorter than the later rounds), the bouquet, her phone.
            new ExamineDef { id = "rafael", area = SliceArea.Hospital, label = "Rafael", knot = "day1_rafael" },
            new ExamineDef { id = "wristband", area = SliceArea.Hospital, label = "his wristband", knot = "duty_wristband" },
            new ExamineDef { id = "vitals", area = SliceArea.Hospital, label = "the monitor", knot = "duty_vitals" },
            new ExamineDef { id = "bouquet", area = SliceArea.Hospital, label = "the flowers", knot = "examine_bouquet", records = FactBouquet },
            new ExamineDef { id = "phone", area = SliceArea.Hospital, label = "your phone", knot = "day1_phone" },
            // Day 2: Rafael's question, the LeBaget visitor, one duty round, sitting with him, the stairs down.
            new ExamineDef { id = "rafael2", area = SliceArea.HospitalDay2, label = "Rafael", knot = "day2_rafael", records = FactDidLenaGetOut },
            new ExamineDef { id = "visitor", area = SliceArea.HospitalDay2, label = "the visitor", knot = "day2_visitor", records = FactVisitor },
            // Day 2's round: three checks (wristband, the drip, vitals), a step longer than the tutorial's.
            new ExamineDef { id = "wristband2", area = SliceArea.HospitalDay2, label = "his wristband", knot = "day2_wristband" },
            new ExamineDef { id = "round2", area = SliceArea.HospitalDay2, label = "the drip", knot = "day2_round" },
            new ExamineDef { id = "vitals2", area = SliceArea.HospitalDay2, label = "the monitor", knot = "day2_vitals" },
            new ExamineDef { id = "sit", area = SliceArea.HospitalDay2, label = "the chair by his bed", knot = "day2_sit" },
            new ExamineDef { id = "stairwell2", area = SliceArea.HospitalDay2, label = "the stairwell door", knot = "day2_stairwell" },
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
        public const string KnotDay2Intro = "day2_intro";
        public const string KnotBooth2 = "booth_mags2";

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
                KnotConvinced, KnotHeardOut, KnotPressed, KnotLost, KnotSliceEnd, KnotDay2Intro, KnotBooth2,
            };
            foreach (var e in Examinables()) if (!list.Contains(e.knot)) list.Add(e.knot);
            foreach (var k in DayContent.Knots()) if (!list.Contains(k)) list.Add(k);
            return list;
        }

        /// <summary>Everything the slice can find before Walt's argument, deductions included (tests and the smoke run).</summary>
        public static string[] KnownBeforeWalt() => new[]
        {
            FactChart, FactEffects, FactPoliceReport, FactPhoto,
            MemWaltAlive, MemNewspaper, MemShopSign, MemSaturday,
            DedWaltDead, DedDayBefore,
        };

        /// <summary>The tablet's scrolling headline (script/opening-script.md, the headline section).</summary>
        public const string NewsHeadline = "ROUTE 9: THREE CASUALTIES OF EARLY-MORNING GANG VIOLENCE - DOCTOR IN CRITICAL CONDITION - WIFE MISSING";
    }
}

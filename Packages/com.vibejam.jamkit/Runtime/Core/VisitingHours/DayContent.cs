using System;
using System.Collections.Generic;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// What the dream reads from the waking days (docs/redesign/day.md, "The bedside and the dream"). Each id is one change
    /// in a night, made by what Nadia told Elias at his bedside the day before. The night's own code asks
    /// <see cref="Chain.HasDreamChange"/> (or reads <see cref="Chain.DreamChanges"/>) when it builds and acts on the ones it
    /// knows; an id it doesn't know is ignored. The ids are stable: rename them only together with the nights' code.
    /// </summary>
    public static class DreamChanges
    {
        // ---------- Night 1, the street (the World agent's hill) ----------

        /// <summary>She told him about Walt's shop: Walt's repair ledger lies open on his workbench, at the page with the same
        /// line written 41 times ("Sat. E., rear tyre, flat. Patched."), where the player can't miss it.</summary>
        public const string WaltLedger = "n1_walt_ledger";
        /// <summary>She told him about the creek and the bikes: the old plank they laid across the creek is back, a short
        /// cut over the water at the foot of the hill (the double jump's lesson stays; the plank is the easy way).</summary>
        public const string CreekPlank = "n1_creek_plank";
        /// <summary>She told him his patients ask after him: the hospital at the top of the hill has its windows lit, a warm
        /// light against the sunset, from everywhere on the hill.</summary>
        public const string HospitalLit = "n1_hospital_lit";
        /// <summary>She sat with him and said nothing: the dream is easy with her. Walt greets her warmly by name at the foot
        /// of the hill (his gentlest opening).</summary>
        public const string WaltWarm = "n1_walt_warm";

        // ---------- Night 2, the house (the House agent's) ----------

        /// <summary>She told him about the lake: the photo wall in the hall has the lake photo (the four of them) up and lit
        /// from the first moment; it counts as found (the hide-and-seek counter starts at 1).</summary>
        public const string PhotoWall = "n2_photo_wall";
        /// <summary>She told him about Theo's football: Theo's room is open from the start (no key, no detour), his kit on
        /// the bed, and Theo trusts her a little more (his question comes sooner).</summary>
        public const string TheoRoomOpen = "n2_theo_room_open";
        /// <summary>She told him they're still looking for Lena: Lena knows Nadia is coming. She's at the window when Nadia
        /// arrives, and her anger starts one step higher in the argument.</summary>
        public const string LenaWary = "n2_lena_wary";
        /// <summary>She told him she's sorry about the phone: in the argument Lena's "He called you first" lands on a Nadia
        /// who has already said it out loud (the honest admission is there from the start).</summary>
        public const string LenaSorry = "n2_lena_sorry";
        /// <summary>She sat with him and said nothing: the house is gentle with her. Lena's anger starts one step lower.</summary>
        public const string LenaSofter = "n2_lena_softer";

        /// <summary>Every change, with the night it belongs to and what it does (the doc's table, and the tests).</summary>
        public static readonly DreamChangeDef[] All =
        {
            new DreamChangeDef(WaltLedger, ChainSegment.Night1, "Walt's ledger lies open on his workbench at the page of 41 identical lines."),
            new DreamChangeDef(CreekPlank, ChainSegment.Night1, "The old plank lies across the creek again: a short cut at the foot of the hill."),
            new DreamChangeDef(HospitalLit, ChainSegment.Night1, "The hospital at the top of the hill has its windows lit."),
            new DreamChangeDef(WaltWarm, ChainSegment.Night1, "Walt greets her warmly, by name."),
            new DreamChangeDef(PhotoWall, ChainSegment.Night2, "The lake photo is up on the hall's photo wall, lit, and counts as found."),
            new DreamChangeDef(TheoRoomOpen, ChainSegment.Night2, "Theo's room is open from the start; Theo trusts her sooner."),
            new DreamChangeDef(LenaWary, ChainSegment.Night2, "Lena waits at the window; her anger starts a step higher."),
            new DreamChangeDef(LenaSorry, ChainSegment.Night2, "Nadia has already said sorry out loud: the honest admission is open from the start."),
            new DreamChangeDef(LenaSofter, ChainSegment.Night2, "The house is gentle; Lena's anger starts a step lower."),
        };

        public static DreamChangeDef Find(string id)
        {
            foreach (var c in All)
                if (c.id == id) return c;
            return null;
        }
    }

    /// <summary>One change the bedside makes in a night: its id, the night, and what it does in words.</summary>
    public sealed class DreamChangeDef
    {
        public readonly string id;
        public readonly ChainSegment night;
        public readonly string what;

        public DreamChangeDef(string id, ChainSegment night, string what)
        {
            this.id = id;
            this.night = night;
            this.what = what;
        }
    }

    /// <summary>What Elias's body does while Nadia talks (the monitor and his hand, on screen).</summary>
    public enum BedsideResponse
    {
        /// <summary>His heart rate climbs a few beats; the monitor's tone quickens.</summary>
        Heart,
        /// <summary>His fingers move against hers.</summary>
        Fingers,
        /// <summary>His heart slows; his breathing evens.</summary>
        Calm,
    }

    /// <summary>One thing Nadia can tell Elias at his bedside, unlocked by something she found that day (or before).</summary>
    [Serializable]
    public class BedsideTopicDef
    {
        public string id = "";
        /// <summary>The day she can tell it (1 or 2); the night after that day hears it.</summary>
        public int day = 1;
        /// <summary>The choice's words.</summary>
        public string label = "";
        /// <summary>The journal entry that unlocks it (empty: always there).</summary>
        public string requires = "";
        /// <summary>The ink knot with her lines (and the #event tags for his responses).</summary>
        public string knot = "";
        public BedsideResponse response;
        /// <summary>The <see cref="DreamChanges"/> id it makes.</summary>
        public string change = "";
    }

    /// <summary>What one task on the duty round is.</summary>
    public enum RoundTaskKind
    {
        /// <summary>Right patient: the wristband against the chart.</summary>
        Identity,
        /// <summary>Observations: read the monitor, chart them (or escalate).</summary>
        Obs,
        /// <summary>Medicines: the right drug, dose, route and time, or holding it.</summary>
        Meds,
        /// <summary>The drip: the right bag.</summary>
        Drip,
    }

    /// <summary>One task on a day's duty round. Its knot asks the question (ink choices) and ends with
    /// #event:vh:round:&lt;id&gt;:ok or #event:vh:round:&lt;id&gt;:slip.</summary>
    [Serializable]
    public class RoundTaskDef
    {
        public string id = "";
        public int day = 1;
        public RoundTaskKind kind;
        /// <summary>The round sheet's line: "Room 14, Elias: wristband".</summary>
        public string label = "";
        /// <summary>The examinable (spot) where it's done.</summary>
        public string spot = "";
        public string knot = "";
    }

    /// <summary>
    /// The waking days' content beyond the slice's (docs/redesign/day.md): the bigger floor's finds, the duty rounds, the
    /// bedside topics. Ids here are what the ink, the ward and the chain use. All the writing is the Day agent's in the
    /// creator's voice except where marked as the creator's.
    /// </summary>
    public static class DayContent
    {
        /// <summary>How many things she can tell him in one sitting (then his heart is up, and that's enough for one day).</summary>
        public const int MaxTopicsPerDay = 2;

        // ---------- Entry ids (facts found on the floor) ----------
        public const string FactLockerPhoto = "fact_locker_photo";
        public const string FactJune = "fact_june_brandt";
        public const string FactTheoTeam = "fact_theo_team";
        public const string FactBrandt = "fact_brandt";
        public const string FactOkafor = "fact_okafor";
        /// <summary>"Told him": one personal entry per topic (she writes down what she said, and waits to see).</summary>
        public const string ToldPrefix = "told_";

        // ---------- Spot ids (examinables on the floor) ----------
        public const string SpotLocker = "locker";
        public const string SpotRota = "rota";
        public const string SpotKettle = "kettle";
        public const string SpotJune = "june";
        public const string SpotTv = "tv";
        public const string SpotVending = "vending";
        public const string SpotBook = "remembrance_book";
        public const string SpotCandles = "candles";
        public const string SpotOkafor = "okafor";
        public const string SpotBrandt = "brandt";
        public const string SpotBrandtBand = "brandt_band";
        public const string SpotOkaforChart = "okafor_chart";
        public const string SpotRoundBoard = "round_board";
        public const string SpotWindow = "west_window";
        public const string SpotChair = "sit";
        public const string SpotDrip = "round2";

        public static List<JournalEntryDef> Entries() => new List<JournalEntryDef>
        {
            new JournalEntryDef
            {
                id = FactLockerPhoto, kind = EntryKind.Fact, title = "The photo in my locker",
                text = "Taped inside my locker door: Eli and me at the creek below Larch Street, with our bikes. I'm seven, soaked to the knees. He's laughing because I made the jump and he didn't.",
                sayLine = "You didn't make the jump, Eli. I did.",
                icon = "prop_family_photo",
            },
            new JournalEntryDef
            {
                id = FactJune, kind = EntryKind.Fact, title = "June Brandt",
                text = "Mrs. Brandt, knitting in the family room while Harold sleeps. Four years ago Eli kept Harold's heart going for forty minutes in the ER. \"Tell him Harold's still annoying me. He'll like that.\"",
                sayLine = "There are people out there you kept alive.",
                icon = "",
            },
            new JournalEntryDef
            {
                id = FactTheoTeam, kind = EntryKind.Fact, title = "The remembrance book",
                text = "A page in the quiet room's book, in felt-tip, from the Larch Hill Under-11s: \"For Theo, our left back. We kept your shirt. Number 4.\" Eleven names. Some spelled wrong.",
                sayLine = "They kept his shirt, Eli. Number 4.",
                icon = "",
            },
            new JournalEntryDef
            {
                id = FactBrandt, kind = EntryKind.Fact, title = "Harold Brandt",
                text = "Room 16. Mr. Brandt asked for \"the doctor with the bad jokes\" and then remembered. \"He told me I'd be too stubborn to die. Tell him he was right.\"",
                sayLine = "Harold Brandt says you were right. He was too stubborn to die.",
                icon = "",
            },
            new JournalEntryDef
            {
                id = FactOkafor, kind = EntryKind.Fact, title = "Mrs. Okafor",
                text = "Room 12. She's lived on Larch Street forty years. She remembers Eli as \"the boy who fixed my gate with a bicycle chain\".",
                sayLine = "Mrs. Okafor still has the gate you fixed.",
                icon = "",
            },
            // What she told him (one per topic): a personal entry, so the journal shows what the dream heard.
            Told("walt", "I told him about Walt's shop", "I told Eli about Saturdays at Walt's. The grease, the radio, the ledger Walt wrote every repair in. If he heard me, it'll be in there tonight."),
            Told("creek", "I told him about the creek", "I told Eli about the creek and the old plank we laid across it, and the day I jumped it instead. If he heard me, it'll be in there tonight."),
            Told("patients", "I told him his patients ask after him", "I told Eli that June Brandt says Harold's still annoying her. That people ask after him. If he heard me, it'll be in there tonight."),
            Told("sorry", "I told him I'm sorry about the phone", "I told Eli I'm sorry I didn't pick up. I didn't say the rest. If he heard me, he heard that much."),
            Told("lake", "I told him about the lake", "I told Eli about the lake last summer, the four of us, Theo laughing so hard he's a blur in the photo. If he heard me, it'll be in there tonight."),
            Told("theo", "I told him about Theo's team", "I told Eli the Under-11s kept Theo's shirt. Number 4. If he heard me, it'll be in there tonight."),
            Told("lena", "I told him we're still looking for Lena", "I told Eli the truth: nobody's found Lena. Not yet. If he heard me, the house will know I'm coming."),
            Told("silence1", "I sat with him", "I didn't say anything. I held his hand until the monitor slowed down. Maybe that's enough to bring in with me."),
            Told("silence2", "I sat with him", "I didn't say anything today either. His hand was warm. Maybe the house will be gentler for it."),
        };

        static JournalEntryDef Told(string topic, string title, string text) => new JournalEntryDef
        {
            id = ToldPrefix + topic, kind = EntryKind.Personal, title = title, text = text, icon = "",
        };

        public static string ToldEntry(string topicId) => ToldPrefix + topicId;

        /// <summary>The things she can tell him. The two "just sit" topics are always there; the rest need a find.</summary>
        public static List<BedsideTopicDef> Topics() => new List<BedsideTopicDef>
        {
            new BedsideTopicDef { id = "walt", day = 1, label = "Walt's shop", requires = SliceContent.FactEffects, knot = "bedside_walt", response = BedsideResponse.Fingers, change = DreamChanges.WaltLedger },
            new BedsideTopicDef { id = "creek", day = 1, label = "The creek, and the bikes", requires = FactLockerPhoto, knot = "bedside_creek", response = BedsideResponse.Heart, change = DreamChanges.CreekPlank },
            new BedsideTopicDef { id = "patients", day = 1, label = "His patients ask after him", requires = FactJune, knot = "bedside_patients", response = BedsideResponse.Heart, change = DreamChanges.HospitalLit },
            new BedsideTopicDef { id = "sorry", day = 1, label = "The phone", requires = SliceContent.FactPoliceReport, knot = "bedside_sorry", response = BedsideResponse.Fingers, change = DreamChanges.LenaSorry },
            new BedsideTopicDef { id = "silence1", day = 1, label = "Just sit with him", requires = "", knot = "bedside_silence", response = BedsideResponse.Calm, change = DreamChanges.WaltWarm },
            new BedsideTopicDef { id = "lake", day = 2, label = "The lake, last summer", requires = SliceContent.FactPhoto, knot = "bedside_lake", response = BedsideResponse.Heart, change = DreamChanges.PhotoWall },
            new BedsideTopicDef { id = "theo", day = 2, label = "Theo's team", requires = FactTheoTeam, knot = "bedside_theo", response = BedsideResponse.Fingers, change = DreamChanges.TheoRoomOpen },
            new BedsideTopicDef { id = "lena", day = 2, label = "Lena", requires = SliceContent.FactDidLenaGetOut, knot = "bedside_lena", response = BedsideResponse.Heart, change = DreamChanges.LenaWary },
            new BedsideTopicDef { id = "silence2", day = 2, label = "Just sit with him", requires = "", knot = "bedside_silence", response = BedsideResponse.Calm, change = DreamChanges.LenaSofter },
        };

        public static BedsideTopicDef FindTopic(string id)
        {
            foreach (var t in Topics())
                if (t.id == id) return t;
            return null;
        }

        /// <summary>Is this one of the "just sit" topics (it takes the whole sitting).</summary>
        public static bool IsSilence(BedsideTopicDef t) => t != null && t.id.StartsWith("silence", StringComparison.Ordinal);

        /// <summary>
        /// The topics she can choose now, in order: the day's topics whose find she has (and hasn't told yet), then "just
        /// sit" (only before she's told him anything).
        /// </summary>
        public static List<BedsideTopicDef> AvailableTopics(int day, Func<string, bool> knows, ICollection<string> toldToday)
        {
            var list = new List<BedsideTopicDef>();
            bool spoke = toldToday != null && toldToday.Count > 0;
            foreach (var t in Topics())
            {
                if (t.day != day) continue;
                if (toldToday != null && toldToday.Contains(t.id)) continue;
                if (IsSilence(t))
                {
                    if (!spoke) list.Add(t);
                    continue;
                }
                if (!string.IsNullOrEmpty(t.requires) && (knows == null || !knows(t.requires))) continue;
                list.Add(t);
            }
            // "Just sit" goes last.
            list.Sort((a, b) => (IsSilence(a) ? 1 : 0).CompareTo(IsSilence(b) ? 1 : 0));
            return list;
        }

        /// <summary>The duty round's tasks for a day (Day 1's tutorial round is the shorter: three tasks; Day 2's five).</summary>
        public static List<RoundTaskDef> RoundTasks(int day)
        {
            var all = new List<RoundTaskDef>
            {
                new RoundTaskDef { id = "r1_band", day = 1, kind = RoundTaskKind.Identity, label = "Room 14, Elias: wristband against the chart", spot = "wristband", knot = "round_r1_band" },
                new RoundTaskDef { id = "r1_obs", day = 1, kind = RoundTaskKind.Obs, label = "Room 14, Elias: observations", spot = "vitals", knot = "round_r1_obs" },
                new RoundTaskDef { id = "r1_meds", day = 1, kind = RoundTaskKind.Meds, label = "Room 12, Mrs. Okafor: six o'clock pain relief", spot = SpotOkafor, knot = "round_r1_meds" },
                new RoundTaskDef { id = "r2_band", day = 2, kind = RoundTaskKind.Identity, label = "Room 16, Mr. Brandt: wristband against the drug chart", spot = SpotBrandtBand, knot = "round_r2_band" },
                new RoundTaskDef { id = "r2_obs_okafor", day = 2, kind = RoundTaskKind.Obs, label = "Room 12, Mrs. Okafor: observations", spot = SpotOkaforChart, knot = "round_r2_obs_okafor" },
                new RoundTaskDef { id = "r2_meds_okafor", day = 2, kind = RoundTaskKind.Meds, label = "Room 12, Mrs. Okafor: her antibiotic", spot = SpotOkafor, knot = "round_r2_meds_okafor" },
                new RoundTaskDef { id = "r2_drip", day = 2, kind = RoundTaskKind.Drip, label = "Room 14, Elias: a new bag on the drip", spot = SpotDrip, knot = "round_r2_drip" },
                new RoundTaskDef { id = "r2_obs", day = 2, kind = RoundTaskKind.Obs, label = "Room 14, Elias: observations", spot = "vitals2", knot = "round_r2_obs" },
            };
            return all.FindAll(t => t.day == day);
        }

        /// <summary>The round task done at this spot on this day (null: none, or it's done).</summary>
        public static RoundTaskDef TaskAt(int day, string spot, DutyRound round)
        {
            foreach (var t in RoundTasks(day))
                if (t.spot == spot && (round == null || !round.IsDone(t.id))) return t;
            return null;
        }

        /// <summary>The new examinables on the bigger floor (the slice's own stay in <see cref="SliceContent.Examinables"/>).</summary>
        public static List<ExamineDef> Examinables() => new List<ExamineDef>
        {
            // Both days (the ward is the same floor; Day 2's are the HospitalDay2 copies).
            new ExamineDef { id = SpotLocker, area = SliceArea.Hospital, label = "your locker", knot = "examine_locker", records = FactLockerPhoto },
            new ExamineDef { id = SpotRota, area = SliceArea.Hospital, label = "the rota", knot = "examine_rota" },
            new ExamineDef { id = SpotKettle, area = SliceArea.Hospital, label = "the kettle", knot = "examine_kettle" },
            new ExamineDef { id = SpotJune, area = SliceArea.Hospital, label = "Mrs. Brandt", knot = "day1_june", records = FactJune },
            new ExamineDef { id = SpotTv, area = SliceArea.Hospital, label = "the television", knot = "examine_tv" },
            new ExamineDef { id = SpotVending, area = SliceArea.Hospital, label = "the vending machine", knot = "examine_vending" },
            new ExamineDef { id = SpotBook, area = SliceArea.Hospital, label = "the remembrance book", knot = "examine_book", records = FactTheoTeam },
            new ExamineDef { id = SpotCandles, area = SliceArea.Hospital, label = "the candles", knot = "examine_candles" },
            new ExamineDef { id = SpotOkafor, area = SliceArea.Hospital, label = "Mrs. Okafor", knot = "talk_okafor", records = FactOkafor },
            new ExamineDef { id = SpotRoundBoard, area = SliceArea.Hospital, label = "the round board", knot = "examine_round_board" },
            new ExamineDef { id = SpotWindow, area = SliceArea.Hospital, label = "the window", knot = "examine_window" },
            new ExamineDef { id = "sit1", area = SliceArea.Hospital, label = "the chair by his bed", knot = "bedside_sit" },
            // Day 2 only.
            new ExamineDef { id = SpotBrandt, area = SliceArea.HospitalDay2, label = "Mr. Brandt", knot = "talk_brandt", records = FactBrandt },
            new ExamineDef { id = SpotBrandtBand, area = SliceArea.HospitalDay2, label = "Mr. Brandt's drug chart", knot = "round_r2_band" },
            new ExamineDef { id = SpotOkaforChart, area = SliceArea.HospitalDay2, label = "Mrs. Okafor's monitor", knot = "round_r2_obs_okafor" },
        };

        /// <summary>Every knot the days can play beyond the examinables' (the round's, the bedside's, the intros').</summary>
        public static List<string> Knots()
        {
            var list = new List<string> { "bedside_intro", "bedside_more", "bedside_enough", "bedside_rise", "round_not_handed_over", "round_done", "bedside_after_round", "stairwell_no_bedside", "booth_heard" };
            foreach (var d in new[] { 1, 2 })
                foreach (var t in RoundTasks(d))
                    if (!list.Contains(t.knot)) list.Add(t.knot);
            foreach (var t in Topics())
                if (!list.Contains(t.knot)) list.Add(t.knot);
            return list;
        }
    }

    /// <summary>
    /// A day's duty round as it's played (docs/redesign/day.md): the tasks, which are done, and the slips (a wrong
    /// choice Nadia catches herself making). Any order. The round is done when every task is.
    /// </summary>
    public sealed class DutyRound
    {
        readonly List<RoundTaskDef> _tasks;
        readonly Dictionary<string, bool> _done = new Dictionary<string, bool>(StringComparer.Ordinal);
        int _slips;

        public DutyRound(int day)
        {
            Day = day;
            _tasks = DayContent.RoundTasks(day);
        }

        public readonly int Day;
        public IReadOnlyList<RoundTaskDef> Tasks => _tasks;
        public int Slips => _slips;
        public int DoneCount => _done.Count;
        public bool Complete => _tasks.Count > 0 && _done.Count >= _tasks.Count;

        public bool IsDone(string id) => _done.ContainsKey(id);

        /// <summary>Whether this task was done with a slip.</summary>
        public bool Slipped(string id) => _done.TryGetValue(id, out bool s) && s;

        public RoundTaskDef Find(string id)
        {
            foreach (var t in _tasks)
                if (t.id == id) return t;
            return null;
        }

        /// <summary>Marks a task done (with a slip or not). False if it isn't this day's or was already done.</summary>
        public bool Mark(string id, bool slip)
        {
            if (Find(id) == null || _done.ContainsKey(id)) return false;
            _done[id] = slip;
            if (slip) _slips++;
            return true;
        }

        /// <summary>The marker on a done task's line (plain words: every UI font has them).</summary>
        public const string DoneMark = "  (done)";

        /// <summary>The round sheet's lines, one per task: "•  label", and "  (done)" after the done ones.</summary>
        public void Lines(List<string> into)
        {
            into.Clear();
            foreach (var t in _tasks)
                into.Add("•  " + t.label + (IsDone(t.id) ? DoneMark : ""));
        }
    }
}

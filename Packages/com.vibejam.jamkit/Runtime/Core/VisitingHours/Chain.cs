using System;
using System.Collections.Generic;
using JamKit.Argument;
using JamKit.Flow;

namespace JamKit.VisitingHours
{
    /// <summary>The game's chain of segments (docs/NIGHTS-1-2.md): the waking days and the nights they lead into.</summary>
    public enum ChainSegment { Day1, Night1, Day2, Night2, End }

    /// <summary>
    /// The chain from the title to the end card: Day 1 (with its 11 PM booth) → Night 1 (Larch Street) → Day 2 (and its
    /// booth) → Night 2 (the house) → the end card. What one segment hands the next is kept in the save: the journal's
    /// entries (the house's DreamJournal entry), Force, the outcomes; Despair and Lena's condemning are the save's own
    /// flags. The scenes themselves are the Unity layer's (VhChainRunner); any scene can say its segment is
    /// done with <see cref="Done"/> (a night's own game, e.g. the movement or house agents', calls it when Nadia leaves).
    /// </summary>
    public static class Chain
    {
        public const string SegmentKey = "vh_chain_segment";
        /// <summary>The journal carried night to night: the house's DreamJournal.Key entry (one comma list, the
        /// waking days' facts and the nights' memories and deductions together).</summary>
        public const string JournalKey = "vh_dream_journal";
        public const string ForceKey = "vh_chain_force";
        /// <summary>Walt's outcome on Night 1 (the street's LarchStreetStory.WaltOutcomeKey, the same text).</summary>
        public const string WaltKey = "vh_walt_night1";
        /// <summary>Night 1 is over (the street's LarchStreetStory.NightDoneFlag).</summary>
        public const string Night1DoneFlag = "vh_night1_done";
        public const string LenaKey = "vh_chain_lena";
        /// <summary>What the bedside told the dream: <see cref="DreamChanges"/> ids, one comma list (docs/redesign/day.md).</summary>
        public const string DreamChangesKey = "vh_dream_changes";
        /// <summary>The bedside topics she has told him (DayContent topic ids), one comma list.</summary>
        public const string ToldKey = "vh_bedside_told";
        /// <summary>The duty rounds' slips across the run (a wrong choice she caught herself making).</summary>
        public const string SlipsKey = "vh_round_slips";
        /// <summary>The nights she was forced out of (night names, one comma list; a night forced out twice is listed twice).</summary>
        public const string ForcedOutKey = "vh_forced_out";
        /// <summary>Why the last forced exit happened (the night's own words, for the next day's lines).</summary>
        public const string ForcedOutWhyKey = "vh_forced_out_why";

        /// <summary>The scenes each night can be (the first one in the build wins). Night 1's own scene is being built (the
        /// movement agent's Larch Street on a hill); until it's in the build, the dream escape stands in for it.</summary>
        public static readonly string[] Night1Scenes = { "LarchStreet", "DreamStreet", "DreamEscape" };
        /// <summary>Night 2 starts on the hill (World's request, docs/redesign/world.md): DreamStreet, told it's Night 2
        /// (<see cref="NightOnTheHill"/>), on Rowan Street outside the house; reaching the gate loads DreamHouse. Without the
        /// hill in the build, the house itself.</summary>
        public static readonly string[] Night2Scenes = { "DreamStreet", "DreamHouse" };

        /// <summary>The scene that is the hill (it plays more than one night: whoever loads it says which first).</summary>
        public const string HillScene = "DreamStreet";

        /// <summary>
        /// Which night the hill should play when <paramref name="scene"/> is loaded for <paramref name="night"/>: 1 or 2 for the
        /// hill (DreamStreetGame.NextNight), 0 for any other scene (it plays its own night).
        /// </summary>
        public static int NightOnTheHill(ChainSegment night, string scene)
        {
            if (scene != HillScene) return 0;
            return night == ChainSegment.Night2 ? 2 : night == ChainSegment.Night1 ? 1 : 0;
        }

        static Action _done;

        /// <summary>A scene says its segment is over (a night: Nadia's out). The chain's runner listens.</summary>
        public static event Action SegmentDone
        {
            add => _done += value;
            remove => _done -= value;
        }

        /// <summary>The current segment is over (a night's game calls this when Nadia leaves the dream).</summary>
        public static void Done() => _done?.Invoke();

        /// <summary>Tests: nobody listens any more.</summary>
        public static void ClearListeners()
        {
            _done = null;
            _forcedOut = null;
        }

        public static ChainSegment Next(ChainSegment s) => s >= ChainSegment.End ? ChainSegment.End : s + 1;

        /// <summary>The segments played in the Visiting Hours scene (the days, the booths, the end card).</summary>
        public static bool IsWaking(ChainSegment s) => s == ChainSegment.Day1 || s == ChainSegment.Day2 || s == ChainSegment.End;

        public static bool IsNight(ChainSegment s) => s == ChainSegment.Night1 || s == ChainSegment.Night2;

        /// <summary>The scenes a night can be (empty for the waking segments).</summary>
        public static string[] NightScenes(ChainSegment s) =>
            s == ChainSegment.Night1 ? Night1Scenes : s == ChainSegment.Night2 ? Night2Scenes : new string[0];

        public static string Title(ChainSegment s)
        {
            switch (s)
            {
                case ChainSegment.Day1: return "Day 1";
                case ChainSegment.Night1: return "Night 1: the Street";
                case ChainSegment.Day2: return "Day 2";
                case ChainSegment.Night2: return "Night 2: the House";
                default: return "The end";
            }
        }

        public static bool TryParse(string text, out ChainSegment s)
        {
            s = ChainSegment.Day1;
            if (string.IsNullOrEmpty(text)) return false;
            foreach (ChainSegment v in Enum.GetValues(typeof(ChainSegment)))
                if (string.Equals(v.ToString(), text.Trim(), StringComparison.OrdinalIgnoreCase)) { s = v; return true; }
            return false;
        }

        // ---------- The journal and the tallies, in the save ----------

        /// <summary>Records an entry in the carried journal (a night's game can call this for a memory it found).</summary>
        public static bool Record(SaveData save, string id)
        {
            if (save == null || string.IsNullOrEmpty(id)) return false;
            var known = Split(save.GetEntry(JournalKey));
            if (known.Contains(id)) return false;
            known.Add(id);
            save.SetEntry(JournalKey, Join(known));
            return true;
        }

        /// <summary>Whether the carried journal has the entry (a night's battle can read the deductions with this).</summary>
        public static bool Knows(SaveData save, string id) => save != null && Split(save.GetEntry(JournalKey)).Contains(id);

        public static List<string> Known(SaveData save) => Split(save != null ? save.GetEntry(JournalKey) : null);

        public static int Force(SaveData save) => save != null && int.TryParse(save.GetEntry(ForceKey), out int f) ? f : 0;

        public static void AddForce(SaveData save, int amount)
        {
            if (save == null || amount == 0) return;
            save.SetEntry(ForceKey, (Force(save) + amount).ToString());
        }

        /// <summary>Lena's outcome in Night 2 ("Convinced", "Heard out", "Pressed", "Composure lost"; the house writes it).</summary>
        public static void SetLenaOutcome(SaveData save, string outcome) => save?.SetEntry(LenaKey, outcome ?? "");

        public static string LenaOutcome(SaveData save) => save != null ? save.GetEntry(LenaKey) ?? "" : "";

        public static void SetWaltOutcome(SaveData save, string outcome) => save?.SetEntry(WaltKey, outcome ?? "");

        public static string WaltOutcome(SaveData save) => save != null ? save.GetEntry(WaltKey) ?? "" : "";

        public static ChainSegment Segment(SaveData save) =>
            save != null && TryParse(save.GetEntry(SegmentKey), out var s) ? s : ChainSegment.Day1;

        public static void SetSegment(SaveData save, ChainSegment s) => save?.SetEntry(SegmentKey, s.ToString());

        /// <summary>The argument battles' ledger (each battle's latest result): a new game clears it with the rest.</summary>
        public const string ArgumentsKey = ArgumentLedger.SaveKey;

        // ---------- Force and Despair across the nights (the argument agent: "the Force tally is yours") ----------

        /// <summary>
        /// Force across the run: what the slice's own argument left (<see cref="ForceKey"/>) plus what every battle in the
        /// ledger added (<see cref="ArgumentResult.forceAdded"/>). The ledger keeps each battle's latest result, so a battle
        /// played again replaces its share instead of adding twice.
        /// </summary>
        public static int ForceTally(SaveData save)
        {
            int f = Force(save);
            foreach (var r in BattleResults(save)) f += r.forceAdded;
            return f < 0 ? 0 : f;
        }

        /// <summary>
        /// Despair across the run, 0..<see cref="ArgumentMeters.DespairMax"/>: every battle's
        /// <see cref="ArgumentResult.despairAdded"/> (Lena condemning), and the dream's Despair flag (she went on into the
        /// rain without the umbrella) counts as the maximum, as the dream's battles always read it.
        /// </summary>
        public static int DespairTally(SaveData save)
        {
            int d = 0;
            foreach (var r in BattleResults(save)) d += r.despairAdded;
            // Her shifts fray her: a run's worth of slips on the duty rounds is one step down the Long Night's slope, and so
            // is each time the dream threw her out (the strain on him, and on her).
            if (Slips(save) >= SlipsForDespair) d += 1;
            d += Strain(save);
            if (Despair(save)) d = ArgumentMeters.DespairMax;
            return d < 0 ? 0 : d > ArgumentMeters.DespairMax ? ArgumentMeters.DespairMax : d;
        }

        /// <summary>
        /// What a night's battle starts with: the run's Force and Despair (<see cref="ForceTally"/>,
        /// <see cref="DespairTally"/>) and the cards the carried journal has found, plus any <paramref name="evidence"/>
        /// the night passes. Hand it to ArgumentHost.BeginBattle (DreamBattles.Context does).
        /// </summary>
        public static ArgumentContext BattleContext(SaveData save, IEnumerable<string> evidence = null)
        {
            var c = new ArgumentContext { force = ForceTally(save), despair = DespairTally(save) };
            foreach (var id in Known(save)) if (!c.found.Contains(id)) c.found.Add(id);
            if (evidence != null)
                foreach (var id in evidence)
                    if (!string.IsNullOrEmpty(id) && !c.found.Contains(id)) c.found.Add(id);
            return c;
        }

        /// <summary>Every battle's latest result in the save's ledger (what each one added, won, and how it ended).</summary>
        public static List<ArgumentResult> BattleResults(SaveData save)
        {
            var list = new List<ArgumentResult>();
            if (save == null) return list;
            var ledger = ArgumentLedger.Parse(save.GetEntry(ArgumentsKey));
            foreach (var rec in ledger.Records)
                list.Add(ArgumentBattles.Result(ArgumentBattles.Build(rec.id), rec));
            return list;
        }

        /// <summary>A battle's latest result (null: not played this run).</summary>
        public static ArgumentResult BattleResult(SaveData save, string battleId)
        {
            foreach (var r in BattleResults(save))
                if (r.battleId == battleId || (r.record != null && r.record.id == battleId)) return r;
            return null;
        }

        /// <summary>The dream's Despair flag (DreamRain.DespairFlag, the same text: the dream core needs the movement core, so the keys are copied; ChainKeysTests checks they match).</summary>
        public const string DespairFlag = "vh_despair";

        /// <summary>The house's keys (DreamHouseStory.CondemnsFlag, .TheoAnswerKey, .ThrownOutFlag; the same text).</summary>
        public const string CondemnsFlag = "vh_lena_condemns", TheoAnswerKey = "vh_theo_dad_sad", ThrownOutFlag = "vh_n2_thrown_out";

        /// <summary>She was thrown out of Night 2's house (the house's flag): the booth plays the house's lines for it.</summary>
        public static bool ThrownOutOfNight2(SaveData save) => save != null && save.HasFlag(ThrownOutFlag);

        // ---------- Lena's keepsake: her star-shaped watch (docs/REDESIGN.md, the creator 2026-10-12) ----------

        /// <summary>The keepsake carried into the waking world: set when it's hers (any code that gives it can set it).</summary>
        public const string KeepsakeWatchFlag = "vh_lena_watch";

        /// <summary>
        /// Lena's keepsake is Nadia's (Heard out): her star-shaped watch, worn on Nadia's right wrist, in the dream and in the
        /// ward. True when the flag is set, when the carried journal has the keepsake's entry, or when Lena's battle in the
        /// ledger ended with a keepsake.
        /// </summary>
        public static bool HasLenaWatch(SaveData save)
        {
            if (save == null) return false;
            if (save.HasFlag(KeepsakeWatchFlag)) return true;
            if (Knows(save, DreamLenaDemo.Planisphere)) return true;
            var lena = BattleResult(save, DreamLenaDemo.Id);
            return lena != null && !string.IsNullOrEmpty(lena.keepsake);
        }

        /// <summary>Despair: she went on into the rain without the umbrella (the dream's flag; carried to Night 2).</summary>
        public static bool Despair(SaveData save) => save != null && save.HasFlag(DespairFlag);

        /// <summary>
        /// A new game: the chain from Day 1 with an empty journal, no tallies, and the last run's story cleared (Despair,
        /// Lena's condemning, Theo's question, the battles' ledger), so a new run doesn't inherit them. Within a run they're
        /// carried by the save as each night's code keeps them. (The title's quit piece shows the last run's Despair until
        /// a new game starts.)
        /// </summary>
        public static void Reset(SaveData save)
        {
            if (save == null) return;
            save.SetEntry(SegmentKey, ChainSegment.Day1.ToString());
            save.SetEntry(JournalKey, "");
            save.SetEntry(ForceKey, "0");
            save.SetEntry(WaltKey, "");
            save.SetEntry(LenaKey, "");
            save.SetEntry(ArgumentsKey, "");
            save.SetEntry(TheoAnswerKey, "");
            save.ClearFlag(DespairFlag);
            save.ClearFlag(CondemnsFlag);
            save.ClearFlag(Night1DoneFlag);
            save.ClearFlag(ThrownOutFlag);
            save.ClearFlag(KeepsakeWatchFlag);
            save.SetEntry(DreamChangesKey, "");
            save.SetEntry(ToldKey, "");
            save.SetEntry(SlipsKey, "0");
            save.SetEntry(ForcedOutKey, "");
            save.SetEntry(ForcedOutWhyKey, "");
        }

        // ---------- The bedside and the dream (docs/redesign/day.md: the API the nights read) ----------

        /// <summary>The changes the bedside has made in the dream so far (<see cref="JamKit.VisitingHours.DreamChanges"/> ids).</summary>
        public static List<string> DreamChanges(SaveData save) => Split(save != null ? save.GetEntry(DreamChangesKey) : null);

        /// <summary>
        /// The night's question: did what she told him change this? (e.g. the street: <c>Chain.HasDreamChange(save,
        /// DreamChanges.WaltLedger)</c> puts Walt's ledger out on his bench). Ask it when the night builds.
        /// </summary>
        public static bool HasDreamChange(SaveData save, string changeId) =>
            save != null && !string.IsNullOrEmpty(changeId) && DreamChanges(save).Contains(changeId);

        /// <summary>The changes for one night (its own code can loop over them).</summary>
        public static List<string> DreamChangesFor(SaveData save, ChainSegment night)
        {
            var list = new List<string>();
            foreach (var id in DreamChanges(save))
            {
                var def = JamKit.VisitingHours.DreamChanges.Find(id);
                if (def != null && def.night == night) list.Add(id);
            }
            return list;
        }

        public static bool AddDreamChange(SaveData save, string changeId)
        {
            if (save == null || string.IsNullOrEmpty(changeId)) return false;
            var list = DreamChanges(save);
            if (list.Contains(changeId)) return false;
            list.Add(changeId);
            save.SetEntry(DreamChangesKey, Join(list));
            return true;
        }

        /// <summary>The bedside topics she's told him this run.</summary>
        public static List<string> Told(SaveData save) => Split(save != null ? save.GetEntry(ToldKey) : null);

        public static bool HasTold(SaveData save, string topicId) => save != null && Told(save).Contains(topicId);

        /// <summary>
        /// She told him <paramref name="topic"/> at his bedside: it's kept (the topic, its "told him" journal entry, and the
        /// change it makes in the dream). False for an unknown topic or one already told.
        /// </summary>
        public static bool Tell(SaveData save, BedsideTopicDef topic)
        {
            if (save == null || topic == null || string.IsNullOrEmpty(topic.id)) return false;
            var told = Told(save);
            if (told.Contains(topic.id)) return false;
            told.Add(topic.id);
            save.SetEntry(ToldKey, Join(told));
            Record(save, DayContent.ToldEntry(topic.id));
            AddDreamChange(save, topic.change);
            return true;
        }

        // ---------- The duty rounds' slips ----------

        /// <summary>A run's slips from which the Long Night's slope takes one step (<see cref="DespairTally"/>).</summary>
        public const int SlipsForDespair = 3;

        public static int Slips(SaveData save) => save != null && int.TryParse(save.GetEntry(SlipsKey), out int n) && n > 0 ? n : 0;

        public static void AddSlips(SaveData save, int n)
        {
            if (save == null || n <= 0) return;
            save.SetEntry(SlipsKey, (Slips(save) + n).ToString());
        }

        // ---------- Forced out of the dream (a failure with a cost: the House and World agents' hook) ----------

        static Action<string> _forcedOut;

        /// <summary>A night's game forced Nadia out (the runner listens: it ends the night the way <see cref="Done"/> does,
        /// with the cost kept). The argument is the night's reason, in a few words ("fell from the stairwell").</summary>
        public static event Action<string> ForcedOutRaised
        {
            add => _forcedOut += value;
            remove => _forcedOut -= value;
        }

        /// <summary>
        /// The dream threw her out (she fell, she was caught, the house tipped her out): call this instead of <see cref="Done"/>.
        /// The night ends, and it costs: the forced exit is kept (<see cref="ForcedOutCount"/>); the next day opens on the crash
        /// call in the night and a worse monitor; the run's strain rises (<see cref="Strain"/>: each forced exit is one step of
        /// Despair, the Long Night's slope, and burns the "maybe six" of Mags's nights, <see cref="NightsLeft"/>).
        /// </summary>
        public static void ForceOut(string reason) => _forcedOut?.Invoke(reason ?? "");

        /// <summary>Keeps a forced exit from <paramref name="night"/> in the save (the runner calls this; tests too).</summary>
        public static void RecordForcedOut(SaveData save, ChainSegment night, string reason)
        {
            if (save == null) return;
            string list = save.GetEntry(ForcedOutKey) ?? "";
            save.SetEntry(ForcedOutKey, list.Length > 0 ? list + "," + night : night.ToString());
            save.SetEntry(ForcedOutWhyKey, reason ?? "");
        }

        /// <summary>How many times she's been forced out this run (of <paramref name="night"/>, or of any night).</summary>
        public static int ForcedOutCount(SaveData save, ChainSegment? night = null)
        {
            if (save == null) return 0;
            int n = 0;
            foreach (var part in (save.GetEntry(ForcedOutKey) ?? "").Split(','))
            {
                string p = part.Trim();
                if (p.Length == 0) continue;
                if (night == null || p == night.Value.ToString()) n++;
            }
            return n;
        }

        public static bool WasForcedOut(SaveData save, ChainSegment night) => ForcedOutCount(save, night) > 0;

        public static string ForcedOutReason(SaveData save) => save != null ? save.GetEntry(ForcedOutWhyKey) ?? "" : "";

        /// <summary>The strain on Elias across the run: one per forced exit.</summary>
        public static int Strain(SaveData save) => ForcedOutCount(save);

        /// <summary>Mags's budget ("Five Nights, maybe Six"): six, or five once he's been strained by a forced exit.</summary>
        public static int NightsLeft(SaveData save) => Strain(save) > 0 ? 5 : 6;

        public static List<string> Split(string joined)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(joined)) return list;
            foreach (var part in joined.Split(','))
            {
                string id = part.Trim();
                if (id.Length > 0 && !list.Contains(id)) list.Add(id);
            }
            return list;
        }

        public static string Join(IEnumerable<string> ids)
        {
            var list = new List<string>();
            if (ids != null)
                foreach (var id in ids)
                    if (!string.IsNullOrEmpty(id) && !list.Contains(id.Trim())) list.Add(id.Trim());
            return string.Join(",", list);
        }
    }
}

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

        /// <summary>The scenes each night can be (the first one in the build wins). Night 1's own scene is being built (the
        /// movement agent's Larch Street on a hill); until it's in the build, the dream escape stands in for it.</summary>
        public static readonly string[] Night1Scenes = { "LarchStreet", "DreamStreet", "DreamEscape" };
        public static readonly string[] Night2Scenes = { "DreamHouse" };

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
        public static void ClearListeners() => _done = null;

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

        /// <summary>The house's keys (DreamHouseStory.CondemnsFlag, .TheoAnswerKey; the same text).</summary>
        public const string CondemnsFlag = "vh_lena_condemns", TheoAnswerKey = "vh_theo_dad_sad";

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
        }

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

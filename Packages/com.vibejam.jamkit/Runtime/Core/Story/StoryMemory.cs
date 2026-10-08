using System;
using System.Collections.Generic;
using System.Globalization;

namespace JamKit.Narrative
{
    /// <summary>An ending reached at least once: how often, and on which playthrough first.</summary>
    [Serializable]
    public class EndingRecord
    {
        public string id = "";
        public int times;
        public int firstPlaythrough;
    }

    /// <summary>One remembered value (ink's remember(key, value)).</summary>
    [Serializable]
    public class MemoryValue
    {
        public string key = "";
        /// <summary>0 text, 1 whole number, 2 decimal number, 3 true/false.</summary>
        public int kind;
        public string text = "";
    }

    /// <summary>
    /// What the game remembers across playthroughs, even after "New game", a restart or closing the
    /// game: how many times it was played, finished and abandoned, which endings were seen, values ink
    /// remembered, knots ever visited, lines ever read (for skip), how often the player loaded or went
    /// back in time, and the fourth-wall overrides of the title screen. Saved in its own file, never
    /// inside a save slot (loading a slot can't undo it). Only "Reset memory" (debug menu) clears it.
    /// </summary>
    [Serializable]
    public class StoryMemory
    {
        public const int CurrentVersion = 1;
        public const int MaxReadLines = 20000;

        public int version = CurrentVersion;
        /// <summary>Playthroughs started ("New game"). The current one is number <see cref="playthroughs"/>.</summary>
        public int playthroughs;
        /// <summary>Playthroughs that reached an ending.</summary>
        public int finished;
        /// <summary>A playthrough is under way (set on New game, cleared at an ending).</summary>
        public bool inProgress;
        /// <summary>Times the player left a playthrough before its end (quit to title or closed the game).</summary>
        public int timesQuit;
        /// <summary>New games started while an earlier playthrough was still unfinished.</summary>
        public int abandoned;
        public int loadsThisRun;
        public int rewindsThisRun;
        public int totalLoads;
        /// <summary>The furthest the current playthrough got (ink's turn index: choices made).</summary>
        public int furthestTurn;
        public string lastEnding = "";
        public List<EndingRecord> endings = new List<EndingRecord>();
        public List<MemoryValue> values = new List<MemoryValue>();
        public List<string> visitedKnots = new List<string>();
        public List<int> readLines = new List<int>();
        public string titleOverride = "";
        public string taglineOverride = "";
        public string playLabelOverride = "";

        [NonSerialized] HashSet<int> _read;

        // ---------- Playthroughs ----------

        /// <summary>New game. Returns true if the last playthrough was left unfinished.</summary>
        public bool BeginPlaythrough()
        {
            bool wasAbandoned = inProgress;
            if (wasAbandoned) abandoned++;
            playthroughs++;
            inProgress = true;
            loadsThisRun = 0;
            rewindsThisRun = 0;
            furthestTurn = 0;
            return wasAbandoned;
        }

        /// <summary>The player left mid-story (quit to title, closed the game). Counted once per leave.</summary>
        public void NoteQuit()
        {
            if (inProgress) timesQuit++;
        }

        /// <summary>An ending was reached. Returns true the first time this ending is seen.</summary>
        public bool RecordEnding(string id)
        {
            if (string.IsNullOrEmpty(id)) id = "the_end";
            finished++;
            inProgress = false;
            lastEnding = id;
            var r = FindEnding(id);
            bool first = r == null;
            if (first)
            {
                r = new EndingRecord { id = id, firstPlaythrough = playthroughs };
                endings.Add(r);
            }
            r.times++;
            return first;
        }

        public EndingRecord FindEnding(string id)
        {
            if (endings == null || string.IsNullOrEmpty(id)) return null;
            foreach (var e in endings)
                if (e != null && string.Equals(e.id, id, StringComparison.OrdinalIgnoreCase)) return e;
            return null;
        }

        public bool SeenEnding(string id) => FindEnding(id) != null;

        public int DistinctEndings => endings != null ? endings.Count : 0;

        /// <summary>A save was loaded. <paramref name="savedTurn"/> earlier than the furthest point reached = going back in time.</summary>
        public void RecordLoad(int savedTurn)
        {
            loadsThisRun++;
            totalLoads++;
            if (savedTurn < furthestTurn) rewindsThisRun++;
            inProgress = true;
        }

        public void NoteTurn(int turn)
        {
            if (turn > furthestTurn) furthestTurn = turn;
        }

        // ---------- Values ----------

        public void Remember(string key, object value)
        {
            if (string.IsNullOrEmpty(key)) return;
            var v = FindValue(key);
            if (v == null) { v = new MemoryValue { key = key }; values.Add(v); }
            switch (value)
            {
                case null: v.kind = 0; v.text = ""; break;
                case bool b: v.kind = 3; v.text = b ? "true" : "false"; break;
                case int i: v.kind = 1; v.text = i.ToString(CultureInfo.InvariantCulture); break;
                case float f: v.kind = 2; v.text = f.ToString("R", CultureInfo.InvariantCulture); break;
                case double d: v.kind = 2; v.text = ((float)d).ToString("R", CultureInfo.InvariantCulture); break;
                default: v.kind = 0; v.text = value.ToString(); break;
            }
        }

        /// <summary>The remembered value (int, float, bool or string), or null if never remembered.</summary>
        public object Recall(string key)
        {
            var v = FindValue(key);
            if (v == null) return null;
            switch (v.kind)
            {
                case 1: return int.TryParse(v.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) ? i : 0;
                case 2: return float.TryParse(v.text, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : 0f;
                case 3: return v.text == "true";
                default: return v.text ?? "";
            }
        }

        public bool Knows(string key) => FindValue(key) != null;

        public bool Forget(string key)
        {
            var v = FindValue(key);
            return v != null && values.Remove(v);
        }

        MemoryValue FindValue(string key)
        {
            if (values == null || string.IsNullOrEmpty(key)) return null;
            foreach (var v in values)
                if (v != null && v.key == key) return v;
            return null;
        }

        // ---------- Knots and read lines ----------

        /// <summary>Returns true the first time a knot is ever visited.</summary>
        public bool Visit(string knot)
        {
            if (string.IsNullOrEmpty(knot) || visitedKnots.Contains(knot)) return false;
            visitedKnots.Add(knot);
            return true;
        }

        public bool VisitedEver(string knot) => !string.IsNullOrEmpty(knot) && visitedKnots != null && visitedKnots.Contains(knot);

        public bool IsRead(int lineHash)
        {
            EnsureReadSet();
            return _read.Contains(lineHash);
        }

        /// <summary>Returns true if the line was new.</summary>
        public bool MarkRead(int lineHash)
        {
            EnsureReadSet();
            if (!_read.Add(lineHash)) return false;
            readLines.Add(lineHash);
            if (readLines.Count > MaxReadLines)
            {
                int drop = readLines.Count - MaxReadLines;
                for (int i = 0; i < drop; i++) _read.Remove(readLines[i]);
                readLines.RemoveRange(0, drop);
            }
            return true;
        }

        void EnsureReadSet()
        {
            if (readLines == null) readLines = new List<int>();
            if (_read != null) return;
            _read = new HashSet<int>(readLines);
        }

        /// <summary>A stable id for a line (FNV-1a of where it is and what it says), the same in every build.</summary>
        public static int LineHash(string knot, string text)
        {
            unchecked
            {
                uint h = 2166136261;
                string s = (knot ?? "") + "\u0001" + (text ?? "");
                for (int i = 0; i < s.Length; i++)
                {
                    h ^= s[i];
                    h *= 16777619;
                }
                return (int)h;
            }
        }

        // ---------- Upkeep ----------

        /// <summary>Repairs nulls after loading (an old or hand-edited file). Returns true if anything changed.</summary>
        public bool Migrate()
        {
            bool changed = false;
            if (version < CurrentVersion) { version = CurrentVersion; changed = true; }
            if (endings == null) { endings = new List<EndingRecord>(); changed = true; }
            if (values == null) { values = new List<MemoryValue>(); changed = true; }
            if (visitedKnots == null) { visitedKnots = new List<string>(); changed = true; }
            if (readLines == null) { readLines = new List<int>(); changed = true; }
            if (lastEnding == null) { lastEnding = ""; changed = true; }
            if (titleOverride == null) { titleOverride = ""; changed = true; }
            if (taglineOverride == null) { taglineOverride = ""; changed = true; }
            if (playLabelOverride == null) { playLabelOverride = ""; changed = true; }
            if (playthroughs < 0) { playthroughs = 0; changed = true; }
            _read = null;
            return changed;
        }

        /// <summary>Forget everything (debug "Reset memory").</summary>
        public void Reset()
        {
            var d = new StoryMemory();
            version = d.version;
            playthroughs = 0; finished = 0; inProgress = false; timesQuit = 0; abandoned = 0;
            loadsThisRun = 0; rewindsThisRun = 0; totalLoads = 0; furthestTurn = 0;
            lastEnding = "";
            endings = new List<EndingRecord>();
            values = new List<MemoryValue>();
            visitedKnots = new List<string>();
            readLines = new List<int>();
            titleOverride = ""; taglineOverride = ""; playLabelOverride = "";
            _read = null;
        }
    }
}

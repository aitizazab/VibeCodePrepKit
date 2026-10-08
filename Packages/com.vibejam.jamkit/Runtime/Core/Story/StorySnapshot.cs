using System;
using System.Collections.Generic;

namespace JamKit.Narrative
{
    /// <summary>
    /// Everything one save slot holds: ink's own state (variables, visit counts, where the story is,
    /// the choices on screen, the inventory list), the stage picture, the history, the line on
    /// screen and the hotspot scene if one is open. Run state only: the cross-playthrough memory is
    /// never in a slot.
    /// </summary>
    [Serializable]
    public class StorySnapshot
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public string slot = "";
        /// <summary>What the slot card shows: the chapter title, else the knot.</summary>
        public string label = "";
        /// <summary>When it was saved, local time, "yyyy-MM-dd HH:mm".</summary>
        public string savedAt = "";
        public float playSeconds;
        public int playthrough;
        /// <summary>ink's turn index (choices made so far): loading an earlier one is going back in time.</summary>
        public int turn;
        public string knot = "";
        public string chapter = "";
        public string inkState = "";
        public StageState stage = new StageState();
        public List<BacklogEntry> backlog = new List<BacklogEntry>();
        /// <summary>The full-screen text page (NVL mode) as it was.</summary>
        public List<BacklogEntry> nvlPage = new List<BacklogEntry>();
        public string lineSpeaker = "";
        public string lineText = "";
        public LineKind lineKind;
        /// <summary>Open hotspot scene id ("" = none).</summary>
        public string investigation = "";
        /// <summary>The story was waiting in the hotspot scene (not in a line).</summary>
        public bool parked;
        /// <summary>An #ending tag was passed and the story is about to end.</summary>
        public string pendingEnding = "";
        /// <summary>Hotspots already used in this playthrough (for "hide after use").</summary>
        public List<string> used = new List<string>();

        public bool IsValid => !string.IsNullOrEmpty(inkState);

        /// <summary>Repairs nulls after loading. Returns true if anything changed.</summary>
        public bool Migrate()
        {
            bool changed = false;
            if (stage == null) { stage = new StageState(); changed = true; }
            if (stage.characters == null) { stage.characters = new List<StageCharacter>(); changed = true; }
            if (backlog == null) { backlog = new List<BacklogEntry>(); changed = true; }
            if (nvlPage == null) { nvlPage = new List<BacklogEntry>(); changed = true; }
            if (slot == null) slot = "";
            if (label == null) label = "";
            if (savedAt == null) savedAt = "";
            if (knot == null) knot = "";
            if (chapter == null) chapter = "";
            if (lineSpeaker == null) lineSpeaker = "";
            if (lineText == null) lineText = "";
            if (investigation == null) investigation = "";
            if (pendingEnding == null) pendingEnding = "";
            if (used == null) { used = new List<string>(); changed = true; }
            if (version < CurrentVersion) { version = CurrentVersion; changed = true; }
            return changed;
        }

        /// <summary>"2:05" style play time.</summary>
        public static string FormatPlayTime(float seconds)
        {
            if (!(seconds > 0f)) return "0:00";
            int s = (int)seconds;
            int h = s / 3600, m = (s / 60) % 60, sec = s % 60;
            return h > 0 ? h + ":" + m.ToString("00") + ":" + sec.ToString("00") : m + ":" + sec.ToString("00");
        }
    }

    /// <summary>The save slots: three manual ones, the quick save and the autosave.</summary>
    public static class StorySlots
    {
        public const string Auto = "auto";
        public const string Quick = "quick";
        public const int ManualCount = 3;

        public static string Manual(int index) => "slot" + (index + 1);

        public static readonly string[] All = { "slot1", "slot2", "slot3", Quick, Auto };

        public static string DisplayName(string slot)
        {
            switch (slot)
            {
                case Auto: return "Autosave";
                case Quick: return "Quick save";
                case "slot1": return "Slot 1";
                case "slot2": return "Slot 2";
                case "slot3": return "Slot 3";
                default: return slot ?? "";
            }
        }

        public static bool IsManual(string slot) => slot == "slot1" || slot == "slot2" || slot == "slot3";
    }
}

using System;
using System.Collections.Generic;

namespace JamKit.Flow
{
    /// <summary>
    /// Everything that outlives a run: settings, records, unlocks. Serialized with JsonUtility by
    /// the Unity side. Always call <see cref="Migrate"/> right after loading: a missing field loads
    /// as 0/null, and a file written before versioning loads as version 0.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>Bump when the layout changes, and add a step to <see cref="Migrate"/>.</summary>
        public const int CurrentVersion = 2;

        public int version = CurrentVersion;
        public Settings settings = new Settings();
        /// <summary>Fastest winning run in seconds; 0 = no win yet.</summary>
        public float bestTime;
        /// <summary>Highest floor reached (1-based), 0 = never played.</summary>
        public int bestFloor;
        public int runsStarted;
        public int runsWon;
        public List<string> unlocked = new List<string>();
        /// <summary>Story and world flags (ink's set_flag/has_flag): "met_ava", "door_open".</summary>
        public List<string> flags = new List<string>();
        /// <summary>The How to Play pages were shown once (GameConfig.showHowToPlayOnFirstRun).</summary>
        public bool seenHowToPlay;
        /// <summary>A template's own data that outlives runs, as key + text (usually JSON): the bullet hell
        /// keeps its remnants here ("bh_remnants"). Keeps template fields out of the shell's save.</summary>
        public List<SaveEntry> entries = new List<SaveEntry>();

        /// <summary>The text saved under <paramref name="key"/>, or null.</summary>
        public string GetEntry(string key)
        {
            if (entries == null || string.IsNullOrEmpty(key)) return null;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null && entries[i].key == key) return entries[i].value;
            return null;
        }

        /// <summary>Saves <paramref name="value"/> under <paramref name="key"/> (null removes it).</summary>
        public void SetEntry(string key, string value)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (entries == null) entries = new List<SaveEntry>();
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] == null || entries[i].key != key) continue;
                if (value == null) entries.RemoveAt(i);
                else entries[i].value = value;
                return;
            }
            if (value != null) entries.Add(new SaveEntry { key = key, value = value });
        }

        /// <summary>
        /// Upgrade an older save to <see cref="CurrentVersion"/> one step at a time, then repair
        /// nulls and out-of-range values. A save from a newer build is left at its version (we can't
        /// know its layout) but still repaired so this build can run on it. Returns true if anything changed.
        /// </summary>
        public bool Migrate()
        {
            bool changed = false;
            while (version < CurrentVersion)
            {
                switch (version)
                {
                    case 0: MigrateV0ToV1(); break;
                    case 1: MigrateV1ToV2(); break;
                    // case 2: MigrateV2ToV3(); break;
                }
                version++;
                changed = true;
            }
            if (settings == null) { settings = new Settings(); changed = true; }
            if (unlocked == null) { unlocked = new List<string>(); changed = true; }
            if (flags == null) { flags = new List<string>(); changed = true; }
            if (entries == null) { entries = new List<SaveEntry>(); changed = true; }
            if (settings.Clamp()) changed = true;
            if (bestTime < 0f || float.IsNaN(bestTime)) { bestTime = 0f; changed = true; }
            if (bestFloor < 0) { bestFloor = 0; changed = true; }
            if (runsStarted < 0) { runsStarted = 0; changed = true; }
            if (runsWon < 0) { runsWon = 0; changed = true; }
            return changed;
        }

        /// <summary>v0 = before versioning: the assists didn't exist, so they load as 0. Derive them from the difficulty.</summary>
        void MigrateV0ToV1()
        {
            if (settings != null) settings.ApplyDifficulty();
        }

        /// <summary>v1 had no dialogue settings: they'd load as 0 (and clamp to the slowest), so set the defaults.</summary>
        void MigrateV1ToV2()
        {
            if (settings == null) return;
            var d = new Settings();
            settings.textSpeed = d.textSpeed;
            settings.autoDelay = d.autoDelay;
            settings.skipUnread = d.skipUnread;
        }

        public bool IsUnlocked(string id) => unlocked != null && unlocked.Contains(id);

        /// <summary>Returns true the first time (show the "unlocked!" toast only then).</summary>
        public bool Unlock(string id)
        {
            if (unlocked == null) unlocked = new List<string>();
            if (string.IsNullOrEmpty(id) || unlocked.Contains(id)) return false;
            unlocked.Add(id);
            return true;
        }

        public bool HasFlag(string name) => flags != null && !string.IsNullOrEmpty(name) && flags.Contains(name);

        /// <summary>Returns true if the flag was newly set.</summary>
        public bool SetFlag(string name)
        {
            if (flags == null) flags = new List<string>();
            if (string.IsNullOrEmpty(name) || flags.Contains(name)) return false;
            flags.Add(name);
            return true;
        }

        public bool ClearFlag(string name) => flags != null && flags.Remove(name);

        public void RecordRunStarted() => runsStarted++;

        /// <summary>Update the records. Returns true if this run set a new best time.</summary>
        public bool RecordRunEnded(bool won, int floorReached, float time)
        {
            if (floorReached > bestFloor) bestFloor = floorReached;
            if (!won) return false;
            runsWon++;
            if (time > 0f && (bestTime <= 0f || time < bestTime)) { bestTime = time; return true; }
            return false;
        }
    }

    /// <summary>One <see cref="SaveData.entries"/> item.</summary>
    [Serializable]
    public class SaveEntry
    {
        public string key;
        public string value;
    }
}

using System;
using System.IO;
using JamKit.Shell;
using UnityEngine;

namespace JamKit.Narrative
{
    /// <summary>
    /// The story's files: the cross-playthrough memory and the save slots (3 manual, quick, auto), as
    /// JSON beside the shell's save (a file in persistentDataPath; PlayerPrefs on WebGL). Names follow
    /// the shell's save slot ("save_story_memory.json"), so tests and the smoke run keep their own.
    /// </summary>
    public static class StoryStorage
    {
        static StoryMemory _memory;
        static string _memoryPrefix;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _memory = null;
            _memoryPrefix = null;
        }

        static string Prefix => (GameRoot.Exists ? GameRoot.Save.Slot : "save") + "_story_";

        static bool UsePlayerPrefs => Application.platform == RuntimePlatform.WebGLPlayer;

        static string PathOf(string name) => System.IO.Path.Combine(Application.persistentDataPath, Prefix + name + ".json");

        static string Read(string name)
        {
            try
            {
                if (UsePlayerPrefs) return PlayerPrefs.GetString("jamkit." + Prefix + name, null);
                string p = PathOf(name);
                return File.Exists(p) ? File.ReadAllText(p) : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("JamKit story: couldn't read " + name + ". " + e.Message);
                return null;
            }
        }

        static void Write(string name, string json)
        {
            try
            {
                if (UsePlayerPrefs)
                {
                    PlayerPrefs.SetString("jamkit." + Prefix + name, json);
                    PlayerPrefs.Save();
                    return;
                }
                string p = PathOf(name);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(p));
                string tmp = p + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(p)) File.Delete(p);
                File.Move(tmp, p);
            }
            catch (Exception e)
            {
                Debug.LogWarning("JamKit story: couldn't write " + name + ". " + e.Message);
            }
        }

        static void Delete(string name)
        {
            try
            {
                if (UsePlayerPrefs) { PlayerPrefs.DeleteKey("jamkit." + Prefix + name); PlayerPrefs.Save(); return; }
                string p = PathOf(name);
                if (File.Exists(p)) File.Delete(p);
            }
            catch (Exception e)
            {
                Debug.LogWarning("JamKit story: couldn't delete " + name + ". " + e.Message);
            }
        }

        // ---------- Memory ----------

        /// <summary>The cross-playthrough memory (loaded once per save slot).</summary>
        public static StoryMemory Memory
        {
            get
            {
                if (_memory != null && _memoryPrefix == Prefix) return _memory;
                _memoryPrefix = Prefix;
                StoryMemory m = null;
                string json = Read("memory");
                if (!string.IsNullOrEmpty(json))
                {
                    try { m = JsonUtility.FromJson<StoryMemory>(json); }
                    catch (Exception e) { Debug.LogWarning("JamKit story: the memory file was damaged, starting fresh. " + e.Message); }
                }
                _memory = m ?? new StoryMemory();
                _memory.Migrate();
                return _memory;
            }
        }

        public static void SaveMemory()
        {
            if (_memory == null) return;
            Write("memory", JsonUtility.ToJson(_memory));
        }

        /// <summary>Forgets every playthrough (debug "Reset memory"), and the shell's flags with it.</summary>
        public static void ResetMemory()
        {
            Memory.Reset();
            SaveMemory();
            if (GameRoot.Exists)
            {
                GameRoot.Save.Data.flags.Clear();
                GameRoot.Save.Save();
            }
        }

        // ---------- Slots ----------

        public static bool Exists(string slot) => !string.IsNullOrEmpty(Read("slot_" + slot));

        public static StorySnapshot ReadSlot(string slot)
        {
            string json = Read("slot_" + slot);
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var s = JsonUtility.FromJson<StorySnapshot>(json);
                if (s == null) return null;
                s.Migrate();
                return s.IsValid ? s : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("JamKit story: save " + slot + " is damaged. " + e.Message);
                return null;
            }
        }

        public static void WriteSlot(string slot, StorySnapshot snapshot)
        {
            if (snapshot == null) return;
            snapshot.slot = slot;
            Write("slot_" + slot, JsonUtility.ToJson(snapshot));
        }

        public static void DeleteSlot(string slot) => Delete("slot_" + slot);

        public static void DeleteAllSlots()
        {
            foreach (var s in StorySlots.All) DeleteSlot(s);
        }

        public static bool AnySlot()
        {
            foreach (var s in StorySlots.All) if (Exists(s)) return true;
            return false;
        }

        /// <summary>Memory and every slot (the smoke run and tests start clean).</summary>
        public static void DeleteAll()
        {
            DeleteAllSlots();
            Delete("memory");
            _memory = null;
            _memoryPrefix = null;
        }
    }
}

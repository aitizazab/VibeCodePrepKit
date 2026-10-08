using System;
using System.IO;
using JamKit.Flow;
using UnityEngine;

namespace JamKit.Shell
{
    /// <summary>
    /// Loads and saves the core <see cref="SaveData"/> (settings, records, flags) as JSON:
    /// PlayerPrefs on WebGL (the browser's IndexedDB), a file in persistentDataPath elsewhere.
    /// Runs <see cref="SaveData.Migrate"/> on every load, so old or hand-edited saves still work.
    /// </summary>
    public sealed class SaveService
    {
        string _slot;
        SaveData _data = new SaveData();

        public SaveData Data => _data;
        public Settings Settings => _data.settings;
        public string Slot => _slot;

        /// <summary>Raised after <see cref="Load"/> or a settings change, so services re-apply volumes etc.</summary>
        public event Action<Settings> SettingsChanged;

        public SaveService(string slot = "save")
        {
            _slot = string.IsNullOrEmpty(slot) ? "save" : slot;
        }

        static bool UsePlayerPrefs => Application.platform == RuntimePlatform.WebGLPlayer;

        string PrefsKey => "jamkit." + _slot;

        public string FilePath => Path.Combine(Application.persistentDataPath, _slot + ".json");

        /// <summary>Switch to another slot (tests, the smoke run). <paramref name="fresh"/> deletes it first.</summary>
        public void UseSlot(string slot, bool fresh)
        {
            _slot = string.IsNullOrEmpty(slot) ? "save" : slot;
            if (fresh) Delete();
            Load();
        }

        public void Load()
        {
            string json = null;
            try
            {
                if (UsePlayerPrefs) json = PlayerPrefs.GetString(PrefsKey, null);
                else if (File.Exists(FilePath)) json = File.ReadAllText(FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("JamKit: couldn't read the save, starting fresh. " + e.Message);
            }

            SaveData loaded = null;
            if (!string.IsNullOrEmpty(json))
            {
                try { loaded = JsonUtility.FromJson<SaveData>(json); }
                catch (Exception e) { Debug.LogWarning("JamKit: the save was damaged, starting fresh. " + e.Message); }
            }
            _data = loaded ?? new SaveData();
            _data.Migrate();
            SettingsChanged?.Invoke(_data.settings);
        }

        public void Save()
        {
            string json = JsonUtility.ToJson(_data, true);
            try
            {
                if (UsePlayerPrefs)
                {
                    PlayerPrefs.SetString(PrefsKey, json);
                    PlayerPrefs.Save();
                    return;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                // Write then swap, so a crash mid-write never leaves half a save.
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("JamKit: couldn't write the save. " + e.Message);
            }
        }

        public void Delete()
        {
            try
            {
                if (UsePlayerPrefs) { PlayerPrefs.DeleteKey(PrefsKey); PlayerPrefs.Save(); }
                else if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("JamKit: couldn't delete the save. " + e.Message);
            }
            _data = new SaveData();
        }

        /// <summary>Call after changing <see cref="Settings"/> so volumes, shake and fullscreen follow.</summary>
        public void NotifySettingsChanged()
        {
            _data.settings.Clamp();
            SettingsChanged?.Invoke(_data.settings);
        }
    }
}

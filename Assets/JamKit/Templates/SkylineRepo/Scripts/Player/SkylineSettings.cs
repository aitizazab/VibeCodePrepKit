using System;
using JamKit.Skyline;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The player's options on the Unity side: <see cref="SkylineOptions"/> (pure: four volumes, look sensitivity for the mouse
    /// and the pad, invert Y, aim assist) kept in PlayerPrefs as one string, loaded on first use and applied (the master
    /// volume to <c>AudioListener.volume</c>; the rest are read where they act: SkylineSounds' effects gain, the barks' and the
    /// teaching lines' voice gain, SkylineDeviceInput's sensitivities, SkylinePlayer's invert and aim assist). In the scripted
    /// runs (the capture, the demo, the perf probe) the saved options are ignored and the defaults used, so the audio check
    /// measures a full-level mix whatever the creator last set.
    /// </summary>
    public static class SkylineSettings
    {
        public const string PrefsKey = "skyline.options";

        static SkylineOptions _o;

        public static event Action Changed;

        public static SkylineOptions Options
        {
            get
            {
                if (_o == null) Load();
                return _o;
            }
        }

        public static void Load()
        {
            _o = new SkylineOptions();
            if (SkylineRunArgs.Demo) return;
            try
            {
                string text = PlayerPrefs.GetString(PrefsKey, "");
                if (!string.IsNullOrEmpty(text)) _o = SkylineOptions.Deserialize(text);
            }
            catch (Exception e)
            {
                Debug.LogWarning("SkylineSettings: could not read the saved options (" + e.Message + "); defaults");
            }
        }

        /// <summary>Clamps, applies and saves the options after the menu changed them.</summary>
        public static void Commit()
        {
            Options.Clamp();
            ApplyMaster();
            if (!SkylineRunArgs.Demo)
            {
                try
                {
                    PlayerPrefs.SetString(PrefsKey, Options.Serialize());
                    PlayerPrefs.Save();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("SkylineSettings: could not save the options (" + e.Message + ")");
                }
            }
            Changed?.Invoke();
        }

        public static void ResetToDefaults()
        {
            _o = new SkylineOptions();
            Commit();
        }

        /// <summary>Which subtitles show (Off / Dialogue / Dialogue + bot barks); saved with the other options, "Dialogue" by default.
        /// Setting it saves and raises <see cref="Changed"/>.</summary>
        public static SubtitleMode SubtitleMode
        {
            get => Options.subtitleMode;
            set { Options.subtitleMode = value; Commit(); }
        }

        /// <summary>The subtitle text size (28 / 34 / 42 px at the 1080p reference); saved, "Medium" by default.</summary>
        public static SubtitleSize SubtitleSize
        {
            get => Options.subtitleSize;
            set { Options.subtitleSize = value; Commit(); }
        }

        /// <summary>The factor the main camera's field of view is scaled by for the player's field-of-view option (1 at the
        /// default): the movement camera sets the view afresh every frame from its own base, and the viewmodel multiplies it.</summary>
        public static float FovScale => Options.fieldOfView / SkylineOptions.DefaultFieldOfView;

        /// <summary>The master volume onto the listener (every frame is cheap: it only writes on a change).</summary>
        public static void ApplyMaster()
        {
            float m = Options.master;
            if (!Mathf.Approximately(AudioListener.volume, m)) AudioListener.volume = m;
        }

    }
}

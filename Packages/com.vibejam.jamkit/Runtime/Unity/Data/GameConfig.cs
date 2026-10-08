using System;
using System.Collections.Generic;
using UnityEngine;

namespace JamKit.Shell
{
    /// <summary>One card of the How to Play screen.</summary>
    [Serializable]
    public class HowToPlayPage
    {
        public string title = "Move";
        [TextArea(2, 6)] public string body = "WASD or the left stick.";
        [Tooltip("Optional picture shown above the text.")]
        public Sprite image;
    }

    /// <summary>
    /// The one asset a jam team edits first: the game's name, its scenes, music, credits and How to
    /// Play cards. Lives at Assets/JamKit/Resources/JamKit/GameConfig.asset (GameRoot loads it from
    /// Resources, so keep the path). "JamKit/Rebuild Shell Scenes" never overwrites it.
    /// </summary>
    [CreateAssetMenu(menuName = "JamKit/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        public const string ResourcePath = "JamKit/GameConfig";

        [Header("Game")]
        public string gameTitle = "My Jam Game";
        [Tooltip("Small line under the title. Leave empty for none.")]
        public string tagline = "Made in 3 days";

        [Header("Scenes (names as listed in Build Settings)")]
        public string bootScene = "Boot";
        public string titleScene = "Title";
        [Tooltip("The scene Play loads.")]
        public string firstGameplayScene = "Sandbox";

        [Header("Music (ids in the Audio Registry)")]
        public string titleMusic = "music_title";
        public string gameMusic = "music_game";

        [Header("Content")]
        public AudioRegistry audio;
        [Tooltip("Characters the dialogue can show (#portrait:Id expression left|right).")]
        public List<Portrait> portraits = new List<Portrait>();
        [Tooltip("Plain text, one credit per line. Shown on the scrolling credits screen.")]
        public TextAsset credits;

        [Header("How to Play")]
        public List<HowToPlayPage> howToPlay = new List<HowToPlayPage>();
        [Tooltip("Show the How to Play cards the first time someone presses Play.")]
        public bool showHowToPlayOnFirstRun = true;

        [Header("Debug")]
        [Tooltip("The debug menu (` or F1) always works in the editor and development builds. Tick this to allow it in release builds too.")]
        public bool allowDebugMenuInRelease;

        [Header("Settings screen")]
        [Tooltip("Show the Pixel look toggle (games with a low-resolution render, like the DOOM-like template).")]
        public bool showPixelLookSetting;
        [Tooltip("Show text speed, auto-advance delay and skip-unread (dialogue-heavy games, like the story template).")]
        public bool showTextSettings;
        [Tooltip("Show mouse sensitivity, invert Y and difficulty (games with action). Off for a pure story game.")]
        public bool showActionSettings = true;
        [Tooltip("Show the best time on the title screen.")]
        public bool showBestTime = true;

        [Header("Starting a run")]
        [Tooltip("Play opens a small Story / Normal / Hard picker before the run (it remembers the last choice; Settings keeps the same value). " +
                 "On for action games; off for a pure story game. Only asked when Show Action Settings is on.")]
        public bool chooseDifficultyOnPlay = true;

        [Header("HUD")]
        [Tooltip("A small control legend at the edge of the HUD (templates that have one, like the bullet hell). Turn it off for a cleaner screen.")]
        public bool showControlLegend = true;

        /// <summary>Play asks for the difficulty first (<see cref="chooseDifficultyOnPlay"/>, and only in games
        /// whose Settings show the difficulty).</summary>
        public bool AsksDifficultyOnPlay => chooseDifficultyOnPlay && showActionSettings;

        public Portrait FindPortrait(string id)
        {
            if (portraits == null || string.IsNullOrEmpty(id)) return null;
            foreach (var p in portraits)
                if (p != null && string.Equals(p.characterId, id, StringComparison.OrdinalIgnoreCase)) return p;
            return null;
        }
    }
}

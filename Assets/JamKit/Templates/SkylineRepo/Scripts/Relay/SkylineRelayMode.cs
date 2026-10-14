using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// Whether the arena runs as the Relay (docs/SKYLINE-RELAY.md) or as the normal mode. The normal mode is the default and is
    /// untouched. The Relay is on for <c>-skyline-mode relay</c> on the command line, or after the pause menu's
    /// "Relay (test)" (which restarts the arena scene); the menu's choice wins over the command line until the next launch.
    /// <see cref="SkylineArenaDirector"/> stays off in the Relay, and <see cref="SkylineRelayDirector"/> is added to the arena
    /// scene when it loads. Only the playable arena scene (a scene named <c>SkylineArena*</c> with a player) is ever changed.
    /// </summary>
    public static class SkylineRelayMode
    {
        public const string Arg = "-skyline-mode", SeedArg = "-skyline-relay-seed";

        static int _menu;   // 0 no choice, 1 the menu chose the Relay, -1 the menu chose the normal mode

        public static bool Active => !SkylineRunArgs.Demo && (_menu == 1 || (_menu == 0 && string.Equals(SkylineRunArgs.Value(Arg), "relay", StringComparison.OrdinalIgnoreCase)));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _menu = 0;   // Enter Play Mode without a domain reload keeps statics

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!Active || !scene.IsValid() || !scene.name.StartsWith("SkylineArena", StringComparison.Ordinal)) return;
            if (SkylineRunArgs.Demo) return;   // the capture and the perf probe run the normal arena
            if (UnityEngine.Object.FindAnyObjectByType<SkylineRelayDirector>() != null) return;
            if (UnityEngine.Object.FindAnyObjectByType<SkylinePlayer>() == null) return;
            new GameObject("Skyline Relay").AddComponent<SkylineRelayDirector>();
        }

        /// <summary>Restarts the arena scene in the Relay (true) or the normal mode (false).</summary>
        public static void Restart(bool relay)
        {
            _menu = relay ? 1 : -1;
            Time.timeScale = 1f;
            var s = SceneManager.GetActiveScene();
            Debug.Log("SKYLINE_RELAY mode " + (relay ? "relay" : "normal") + " (scene restart)");
#if UNITY_EDITOR
            if (s.buildIndex < 0)
            {
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(s.path, new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
#endif
            if (s.buildIndex >= 0) SceneManager.LoadScene(s.buildIndex);
            else SceneManager.LoadScene(s.name);
        }
    }
}

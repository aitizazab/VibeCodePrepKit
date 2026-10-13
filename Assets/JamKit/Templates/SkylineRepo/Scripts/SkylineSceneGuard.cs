using System;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>Keeps Visiting Hours' shell out of Skyline's pipeline scenes (added to both by SkylineScenes.BuildAll).
    /// The shared GameRoot (Packages/com.vibejam.jamkit/Runtime/Unity/GameRoot.cs, owned by Visiting Hours, not edited here)
    /// creates itself in every scene (RuntimeInitializeOnLoadMethod BeforeSceneLoad), treats any scene it doesn't know as
    /// Gameplay, starts a run, plays the game music, and saves the creator's shared save.json on quit.
    ///
    /// What GameRoot has already done when this guard's Awake runs (its own Awake ran before the first scene loaded, and
    /// nothing here can come earlier): it loaded save.json (read only); created an AudioListener on its object; and applied
    /// the creator's saved settings: <c>AudioListener.volume</c> = the master volume, and in a player (not the editor, not
    /// batch mode) <c>Screen.fullScreen</c> = the saved fullscreen flag. It also subscribed to SceneManager.sceneLoaded and
    /// called DontDestroyOnLoad on itself. It has NOT yet run Start, Update or sceneLoaded for this scene, and has written
    /// nothing.
    ///
    /// What the guard does: destroys the "[JamKit GameRoot]" object with DestroyImmediate (a plain Destroy is deferred, and
    /// the editor fires sceneLoaded before the end of the frame, which would let GameRoot start its run first). Its OnDestroy
    /// unsubscribes from sceneLoaded; a destroyed component gets no Start, Update or OnApplicationQuit, so nothing is ever
    /// saved to save.json. Then it undoes what matters for the pass: <c>AudioListener.volume</c> back to 1 (the audio check
    /// measures the recording at full level), and, in a player started with <c>-screen-fullscreen 0</c>, windowed mode again at
    /// the size on the command line (<c>-screen-width</c> / <c>-screen-height</c>), so the command line wins over the saved flag.
    /// The execution order puts the guard before every other script of the scene.</summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class SkylineSceneGuard : MonoBehaviour
    {
        public const string GameRootName = "[JamKit GameRoot]";

        void Awake()
        {
            RemoveGameRoot();
        }

        /// <summary>Removes the shared shell if it exists and undoes its settings. Returns true when it was there.</summary>
        public static bool RemoveGameRoot()
        {
            var root = GameObject.Find(GameRootName);
            if (root == null) return false;
            DestroyImmediate(root);
            AudioListener.volume = 1f;
            RestoreWindowFromCommandLine();
            Debug.Log("SkylineSceneGuard: removed " + GameRootName + " (no run, no music, no save.json); listener volume 1");
            return true;
        }

        /// <summary>Windowed mode again when the command line asks for it (-screen-fullscreen 0), which the saved fullscreen
        /// setting applied by GameRoot would otherwise override. Not in the editor or batch mode (GameRoot never sets fullscreen there).</summary>
        static void RestoreWindowFromCommandLine()
        {
            if (Application.isEditor || Application.isBatchMode) return;
            bool windowedRequested = false;
            int width = 0, height = 0;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-screen-fullscreen") windowedRequested = args[i + 1] == "0";
                else if (args[i] == "-screen-width") int.TryParse(args[i + 1], out width);
                else if (args[i] == "-screen-height") int.TryParse(args[i + 1], out height);
            }
            if (!windowedRequested) return;
            Screen.SetResolution(width > 0 ? width : Screen.width, height > 0 ? height : Screen.height, FullScreenMode.Windowed);
        }
    }
}

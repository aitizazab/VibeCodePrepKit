using System;
using System.Collections;
using System.IO;
using JamKit.Flow;
using JamKit.Presenting;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// The recorded runs of Night 2 in the built player, so no editor window opens and takes focus (the creator, 2026-10-12):
    /// <c>VisitingHours.exe -house-record "C:/…/out/house-runs/&lt;time&gt;" [-house-record-run all|hideandseek|escape]
    /// -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "…/player.log"</c>. Like Day's <c>-day-run</c>
    /// and Battle's <c>-battle-record</c>: it skips to the house through the presenter, the house plays itself on the
    /// autopilot (the hide-and-seek, on through the argument, then the escape), a JPG every 0.5 s of game time goes to
    /// <c>hide-and-seek/</c> and <c>escape/</c> under the folder with <c>run.txt</c> (memories, catches, stretches, by time),
    /// and it quits (exit 0; <c>JAMKIT_HOUSE_RUN_FAIL</c> and exit 1 if the house never came up).
    /// </summary>
    public sealed class HouseRecordFlag : MonoBehaviour
    {
        public const string Flag = "-house-record", RunFlag = "-house-record-run";

        string _folder, _run;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Application.isEditor || !Parse(Environment.GetCommandLineArgs(), out var folder, out var run)) return;
            var go = new GameObject("[House record]");
            DontDestroyOnLoad(go);
            var f = go.AddComponent<HouseRecordFlag>();
            f._folder = folder;
            f._run = run;
        }

        /// <summary>The flags (false without <see cref="Flag"/>). No folder: "house-run" under persistentDataPath.</summary>
        public static bool Parse(string[] args, out string folder, out string run)
        {
            folder = null;
            run = "all";
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
            {
                string next = i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1].Trim('"') : null;
                if (args[i] == Flag) folder = next ?? "house-run";
                if (args[i] == RunFlag && next != null) run = next.ToLowerInvariant();
            }
            if (run != "all" && run != "hideandseek" && run != "escape") run = "all";
            return folder != null;
        }

        IEnumerator Start()
        {
            if (!Path.IsPathRooted(_folder)) _folder = Path.Combine(Application.persistentDataPath, _folder);
            Directory.CreateDirectory(_folder);
            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            Debug.Log("JAMKIT_HOUSE_RUN player recording (" + _run + ") into " + _folder);
            float until = Time.realtimeSinceStartup + 90f;
            while (Time.realtimeSinceStartup < until && !(GameRoot.Exists && !GameRoot.IsLoadingScene && GameRoot.Flow.Current != GameState.Boot)) yield return null;
            if (!GameRoot.Exists) { Fail("the game never started"); yield break; }
            float wait = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < wait) yield return null;
            GameRoot.Save.Data.seenHowToPlay = true;
            bool jumped = false;
            for (int i = 0; i < 20 && !jumped; i++)
            {
                var r = PresenterMode.Jump(DreamHousePresenter.Scene, DreamHouse.ChapterArrival);
                jumped = r == JumpResult.Pending || r == JumpResult.Jumped;
                if (!jumped) { float t = Time.realtimeSinceStartup + 1f; while (Time.realtimeSinceStartup < t) yield return null; }
            }
            if (!jumped) { Fail("couldn't open " + DreamHousePresenter.Scene + " (is it in the build?)"); yield break; }
            until = Time.realtimeSinceStartup + 120f;
            while (Time.realtimeSinceStartup < until && (GameRoot.IsLoadingScene || DreamHouseGame.Current == null || SceneManager.GetActiveScene().name != DreamHousePresenter.Scene)) yield return null;
            if (DreamHouseGame.Current == null) { Fail(DreamHousePresenter.Scene + " didn't come up"); yield break; }
            wait = Time.realtimeSinceStartup + 1.5f;
            while (Time.realtimeSinceStartup < wait) yield return null;
            DreamHouseGame.Current.StartRecording(_folder, _run);
        }

        void Fail(string why)
        {
            Debug.LogError("JAMKIT_HOUSE_RUN_FAIL " + why);
            try { File.WriteAllText(Path.Combine(_folder, "run.txt"), "FAILED: " + why + "\n"); } catch (Exception) { }
            Application.Quit(1);
        }
    }
}

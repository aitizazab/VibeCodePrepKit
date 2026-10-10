using System;
using System.Collections;
using System.Globalization;
using JamKit.Flow;
using UnityEngine;

namespace JamKit.Shell
{
    /// <summary>
    /// Build smoke test: run the player with <c>-jamSmoke 20</c> and it plays itself through
    /// boot, title, Play, the first gameplay scene (mashing through any dialogue), a pause and resume,
    /// a debug win, victory, credits and back to the title, then quits with exit code 0. Any logged
    /// error or exception, or running out of time, quits with exit code 1. Uses its own save slot.
    /// Log lines start with JAMKIT_SMOKE so tools/smoke.ps1 can report them.
    /// </summary>
    public sealed class SmokeRunner : MonoBehaviour
    {
        static Func<IEnumerator> _driver;

        /// <summary>A gameplay scene can drive a few seconds of its own game here (skip a room, take a
        /// draft...) before the smoke run's debug win. Set it in Start, clear it in OnDestroy. Keep it
        /// short: the whole smoke run has one time budget.</summary>
        public static Func<IEnumerator> GameplayDriver
        {
            get => _driver;
            set => _driver = value;
        }

        static Func<bool> _titleGate;

        /// <summary>A template whose title has its own opening (Visiting Hours) sets this: the smoke run
        /// waits until it returns true (the title is ready for Play) before pressing Play. Null: Play at once.</summary>
        public static Func<bool> TitleGate
        {
            get => _titleGate;
            set => _titleGate = value;
        }

        /// <summary>True when this run is a smoke run (-jamSmoke).</summary>
        public static bool Active => Requested(out _);

        float _seconds = 20f;
        int _errors;
        string _firstError = "";
        bool _done;

        public static bool Requested(out float seconds)
        {
            seconds = 0f;
            if (Application.platform == RuntimePlatform.WebGLPlayer) return false;
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] != "-jamSmoke") continue;
                seconds = 20f;
                if (i + 1 < args.Length && float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float s) && s > 0f)
                    seconds = s;
                return true;
            }
            return false;
        }

        void Awake()
        {
            Requested(out _seconds);
            Application.logMessageReceived += OnLog;
            if (Application.isBatchMode) Application.targetFrameRate = 60;
        }

        IEnumerator Start()
        {
            Log("start, budget " + _seconds.ToString("0", CultureInfo.InvariantCulture) + " s");
            StartCoroutine(Watchdog());
            var flow = GameRoot.Flow;
            var ui = GameRoot.Ui;

            yield return Until(() => flow.Current == GameState.Title && !GameRoot.IsLoadingScene, "title screen");
            yield return Real(0.5f);
            if (TitleGate != null)
            {
                yield return Until(() => TitleGate == null || TitleGate(), "the title to be ready (its opening)");
                Log("title gate open");
            }
            Log("title");
            ui.Title.Activate("play");
            yield return null;
            // Action games ask for the difficulty first: take the preselected one (Normal on a fresh save).
            if (ui.IsOverlayOpen(ui.DifficultyPicker))
            {
                var d = ui.DifficultyPicker.Preselected;
                Log("difficulty picker: " + DifficultyText.Name(d));
                if (!ui.DifficultyPicker.Activate(DifficultyScreen.ButtonName(d))) Fail("the difficulty picker has no " + d + " card");
                yield return null;
                if (ui.IsOverlayOpen(ui.DifficultyPicker)) Fail("choosing a difficulty didn't close the picker");
                Log("difficulty is " + DifficultyText.Name(GameRoot.Settings.difficulty) + ", extra lives " + GameRoot.Settings.extraLives);
            }
            else Log("no difficulty picker (GameConfig.chooseDifficultyOnPlay off)");
            // First run: the How to Play cards come up before the game starts.
            for (int i = 0; i < 10 && ui.IsOverlayOpen(ui.HowToPlay); i++)
            {
                ui.HowToPlay.Activate("next");
                yield return null;
            }

            yield return Until(() => flow.InRun && !GameRoot.IsLoadingScene, "gameplay scene");
            Log("playing " + SceneService.ActiveName);

            float playUntil = Time.realtimeSinceStartup + 2.5f;
            bool paused = false;
            while (Time.realtimeSinceStartup < playUntil)
            {
                if (GameRoot.Dialogue.IsActive)
                {
                    GameRoot.Dialogue.AutoStep();
                    playUntil = Mathf.Max(playUntil, Time.realtimeSinceStartup + 1f);
                    // A line a frame: story games have hundreds of lines.
                    yield return null;
                    continue;
                }
                else if (!paused && flow.Current == GameState.Playing)
                {
                    paused = true;
                    GameRoot.Instance.Pause();
                    yield return Real(0.3f);
                    if (flow.Current != GameState.Paused) Fail("pause didn't pause");
                    ui.Pause.Activate("resume");
                }
                yield return Real(0.1f);
            }

            var driver = GameplayDriver;
            if (driver != null && flow.InRun)
            {
                Log("template driver");
                yield return driver();
                Log("template driver done");
            }

            Log("debug win");
            DebugMenu.Win();
            yield return Until(() => flow.Current == GameState.Victory, "victory");
            yield return Real(0.5f);
            ui.Victory.Activate("continue");
            yield return Until(() => flow.Current == GameState.Credits && !GameRoot.IsLoadingScene, "credits");
            yield return Real(1f);
            ui.Credits.Activate("back");
            yield return Until(() => flow.Current == GameState.Title && !GameRoot.IsLoadingScene, "back to title");
            yield return Real(0.5f);
            Finish();
        }

        IEnumerator Until(Func<bool> condition, string what)
        {
            while (!condition())
            {
                if (_done) yield break;
                yield return null;
            }
            Log("reached " + what);
        }

        static IEnumerator Real(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        IEnumerator Watchdog()
        {
            float end = Time.realtimeSinceStartup + _seconds;
            while (!_done && Time.realtimeSinceStartup < end) yield return null;
            if (!_done) Fail("timed out after " + _seconds + " s in state " + GameRoot.Flow.Current);
        }

        void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (message.StartsWith("JAMKIT_SMOKE", StringComparison.Ordinal)) return;
            _errors++;
            if (_firstError.Length == 0) _firstError = message;
        }

        void Fail(string why)
        {
            if (_done) return;
            _done = true;
            Debug.Log("JAMKIT_SMOKE_FAIL " + why);
            Quit(1);
        }

        void Finish()
        {
            if (_done) return;
            _done = true;
            if (_errors > 0)
            {
                Debug.Log($"JAMKIT_SMOKE_FAIL {_errors} error(s) logged; first: {_firstError}");
                Quit(1);
                return;
            }
            Debug.Log("JAMKIT_SMOKE_PASS in " + Time.realtimeSinceStartup.ToString("0.0", CultureInfo.InvariantCulture) + " s");
            Quit(0);
        }

        static void Log(string what) => Debug.Log("JAMKIT_SMOKE " + what);

        static void Quit(int code)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(code);
#endif
        }

        void OnDestroy() => Application.logMessageReceived -= OnLog;
    }
}

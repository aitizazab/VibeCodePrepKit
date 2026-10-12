using JamKit.Flow;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// Hooks Visiting Hours into the shell's title while it's the active template (GameConfig's first
    /// gameplay scene is the slice's): the title scene gets the opening's 3D sets (<see cref="VhStage"/>),
    /// the shell's menu hides (the table is the menu: <see cref="VhTitleTable"/>), and the first time the
    /// title comes up the opening plays (<see cref="VhOpening"/>). Skipped in batch mode (tests) except
    /// the smoke run, which plays it fast and waits for the table before pressing Play. Holding Escape
    /// skips it. Presenter mode's "Opening" chapter plays it again from anywhere.
    /// </summary>
    public static class VhBoot
    {
        /// <summary>The smoke run plays the opening this much faster.</summary>
        public const float SmokeSpeed = 30f;

        static VisitingHoursBoot _boot;
        static VhTitleRunner _runner;
        static bool _openingPlayed;
        static bool _skipOpening;

        /// <summary>Tests: don't play the opening on the next title.</summary>
        public static bool SkipOpening
        {
            get => _skipOpening;
            set => _skipOpening = value;
        }

        public static bool OpeningPlayed => _openingPlayed;
        public static VhOpening Opening => _runner != null ? _runner.opening : null;
        public static VhStage Stage => _runner != null ? _runner.stage : null;
        public static VisitingHoursBoot Config => _boot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Hook()
        {
            _boot = null;
            _runner = null;
            _openingPlayed = false;
            _skipOpening = false;
            GameRoot.Ready -= OnReady;
            GameRoot.Ready += OnReady;
        }

        static void OnReady(GameRoot root)
        {
            _boot = VisitingHoursBoot.Load();
            if (_boot == null) return;
            _runner = root.gameObject.GetComponent<VhTitleRunner>();
            if (_runner == null) _runner = root.gameObject.AddComponent<VhTitleRunner>();
            _runner.boot = _boot;
            // Peeking on the bumpers (hold Q / F, LB / RB, in the dream and the waking world): LB comes off Sprint (L3 and
            // Shift sprint, as the dream always allowed) and RB off Jump. Only while Visiting Hours is the game.
            GameRoot.Input.Bindings.SetPeekOnBumpers(IsActive);
            // The opening editor (F9 on the title, or the presenter panel's "Edit the opening").
            if (root.gameObject.GetComponent<VhOpeningEditor>() == null) root.gameObject.AddComponent<VhOpeningEditor>();
            // The title screenshot for checking the table against the sketch (-title-shot; does nothing without it).
            if (root.gameObject.GetComponent<VhTitleShot>() == null) root.gameObject.AddComponent<VhTitleShot>();
            // Stills of the waking days for review (-ward-shot <folder>; does nothing without it).
            if (root.gameObject.GetComponent<VhWardShot>() == null) root.gameObject.AddComponent<VhWardShot>();
            // A recorded autopilot run of a day (-day-run <folder> [-day-run-day 2]; does nothing without it).
            if (root.gameObject.GetComponent<VhDayRun>() == null) root.gameObject.AddComponent<VhDayRun>();
            // Its capture tour (tools/vh-editor-capture.ps1): only with -opening-editor-tour.
            if (VhOpeningEditorTour.RequestedFolder() != null && root.gameObject.GetComponent<VhOpeningEditorTour>() == null)
                root.gameObject.AddComponent<VhOpeningEditorTour>();
            // The chain from Day 1 to the end card (docs/NIGHTS-1-2.md).
            if (root.gameObject.GetComponent<VhChainRunner>() == null) root.gameObject.AddComponent<VhChainRunner>();
            GameRoot.Ui.Title.Shown -= OnTitleShown;
            GameRoot.Ui.Title.Shown += OnTitleShown;
            VhPresenter.RegisterOpening(PresenterMode.Chapters);
            if (SmokeRunner.Active) SmokeRunner.TitleGate = SmokeGate;
        }

        /// <summary>
        /// The smoke run presses Play once the table is up and the opening is over. It plays the bedroom part (fast) but
        /// doesn't sit through the news report: once the opening reaches the table, it ends the opening there (the title's
        /// final state, as a skip leaves it), so the smoke's time budget never depends on the report's length. The real
        /// game's pacing is untouched (this gate is only set under the smoke run).
        /// </summary>
        static bool SmokeGate()
        {
            if (!IsActive) return true;
            var stage = Stage;
            if (stage == null) return false;
            var opening = Opening;
            if (opening != null && opening.Running && stage.Pose == "table")
            {
                UnityEngine.Debug.Log("JAMKIT_SMOKE_VH the opening reached the table at " + Time.realtimeSinceStartup.ToString("0.0") + " s: ending it there (the report isn't sat through)");
                opening.Finish(false);
            }
            return stage.Interactive && Opening == null;
        }

        /// <summary>True when Play starts the slice.</summary>
        public static bool IsActive => GameRoot.Exists && _boot != null && GameRoot.Config.firstGameplayScene == _boot.sceneName;

        /// <summary>Presenter mode's "Opening" chapter: plays the opening now on the title, or goes back
        /// to the title first (from a run, an end screen, the credits) and plays it there.</summary>
        public static void PresenterOpening()
        {
            if (_runner == null || !GameRoot.Exists) return;
            if (GameRoot.Flow.Current == GameState.Title && !GameRoot.IsLoadingScene && _runner.stage != null)
            {
                PlayOpeningNow();
                return;
            }
            _runner.openingOnTitle = true;
            if (GameRoot.Flow.Current == GameState.Credits) GameRoot.Instance.CloseCredits();
            else if (GameRoot.Flow.Current != GameState.Title) GameRoot.Instance.QuitToTitle();
        }

        static void OnTitleShown(TitleScreen title)
        {
            if (_runner == null) return;
            bool active = IsActive;
            _runner.ApplyShellLook(title, active || _runner.forced || _runner.openingOnTitle);
            if (!active) return;
            if (!_openingPlayed && !_skipOpening && (!Application.isBatchMode || SmokeRunner.Active))
            {
                _openingPlayed = true;
                _runner.openingWanted = true;
                _runner.openingSpeed = SmokeRunner.Active ? SmokeSpeed : 1f;
            }
        }

        /// <summary>Plays the opening now (tests run it fast). Null when the title's sets can't be built here.</summary>
        public static VhOpening PlayOpeningNow(float speed = 1f)
        {
            if (_runner == null || !GameRoot.Exists) return null;
            _openingPlayed = true;
            _runner.forced = true;
            if (_runner.stage == null && !_runner.TryBuildStage()) return null;
            _runner.PlayOpening(speed);
            return _runner.opening;
        }

        /// <summary>Re-reads whether Visiting Hours is active (tests switch the config) and builds or clears the title's sets now.</summary>
        public static void Refresh()
        {
            if (_runner == null || !GameRoot.Exists) return;
            _runner.forced = false;
            _runner.ApplyShellLook(GameRoot.Ui.Title, IsActive);
            if (IsActive) _runner.TryBuildStage();
            else _runner.DestroyStage();
        }
    }

    /// <summary>Builds the title's sets when the title scene is up, and ticks the opening (lives on the GameRoot).</summary>
    public sealed class VhTitleRunner : MonoBehaviour
    {
        public VisitingHoursBoot boot;
        public VhStage stage;
        public VhOpening opening;
        /// <summary>The opening plays as soon as the sets are built (the first title of a launch).</summary>
        public bool openingWanted;
        public float openingSpeed = 1f;
        /// <summary>Presenter mode asked for the opening: play it as soon as the title is up.</summary>
        public bool openingOnTitle;
        /// <summary>The sets stay up on the title even when Visiting Hours isn't the active template (the
        /// presenter's "Opening", tests), until the title scene goes.</summary>
        public bool forced;
        bool _look;

        static bool OnTitleScene =>
            GameRoot.Exists && !GameRoot.IsLoadingScene && SceneManager.GetActiveScene().name == GameRoot.Config.titleScene;

        public bool TryBuildStage()
        {
            if (stage != null) return true;
            if (!OnTitleScene || boot == null) return false;
            float t0 = Time.realtimeSinceStartup;
            stage = VhStageBuilder.Build(boot, true);
            if (SmokeRunner.Active) Debug.Log("JAMKIT_SMOKE_VH the title's sets built in " + (Time.realtimeSinceStartup - t0).ToString("0.00") + " s (at " + Time.realtimeSinceStartup.ToString("0.0") + " s)");
            if (!openingWanted && !openingOnTitle) stage.ShowTitleFinal();
            ApplyShellLook(GameRoot.Ui.Title, true);
            return true;
        }

        public void DestroyStage()
        {
            if (opening != null) opening.Finish(false);
            opening = null;
            if (stage != null) VhUtil.Kill(stage.gameObject);
            stage = null;
            if (GameRoot.Exists) ApplyShellLook(GameRoot.Ui.Title, false);
        }

        /// <summary>The shell's title menu hides while the table is the menu (its buttons stay wired: the table presses them).</summary>
        public void ApplyShellLook(TitleScreen title, bool on)
        {
            if (title == null || title.Root == null) return;
            var screen = title.Root.Q<VisualElement>("title-screen");
            if (screen == null) return;
            _look = on;
            if (on) screen.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            else screen.style.backgroundColor = StyleKeyword.Null;
            foreach (var child in screen.Children())
            {
                if (on) child.style.display = DisplayStyle.None;
                else child.style.display = StyleKeyword.Null;
            }
            title.Root.Query<Button>().ForEach(b => b.focusable = !on);
            if (on) title.Root.focusController?.focusedElement?.Blur();
        }

        public void PlayOpening(float speed)
        {
            if (opening != null) opening.Finish(false);
            opening = null;
            if (stage == null) return;
            opening = new VhOpening(boot, stage, GameRoot.Ui.Title) { speed = speed };
            if (SmokeRunner.Active) Debug.Log("JAMKIT_SMOKE_VH the opening starts at " + Time.realtimeSinceStartup.ToString("0.0") + " s (x" + speed + ", " + opening.Length.ToString("0.0") + " s long)");
            var started = opening;
            opening.Finished += () => { if (opening == started) opening = null; };
            opening.Start();
        }

        void Update()
        {
            if (!GameRoot.Exists || boot == null) return;
            if (stage == null && !openingOnTitle) forced = false;
            bool want = VhBoot.IsActive || forced || openingOnTitle;
            if (want && stage == null && OnTitleScene) TryBuildStage();
            if (!want && stage != null) DestroyStage();
            if (!want && _look) ApplyShellLook(GameRoot.Ui.Title, false);

            bool onTitle = GameRoot.Flow.Current == GameState.Title && !GameRoot.IsLoadingScene;
            if (stage != null && onTitle && (openingWanted || openingOnTitle))
            {
                float speed = openingWanted ? openingSpeed : 1f;
                if (openingOnTitle) forced = true;
                openingWanted = false;
                openingOnTitle = false;
                PlayOpening(speed);
            }
            if (opening != null)
            {
                if (GameRoot.Flow.Current != GameState.Title || stage == null) opening.Finish(false);
                else opening.Tick(Time.unscaledDeltaTime);
            }
        }
    }
}

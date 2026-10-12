using System;
using JamKit.Flow;
using JamKit.Presenting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace JamKit.Shell
{
    /// <summary>
    /// The shell's persistent root. It creates itself before the first scene loads (so any scene can
    /// be played on its own) and survives scene changes. Everything is reachable statically:
    /// <code>
    /// GameRoot.Flow      core GameFlow (Title, Playing, Paused, Dialogue, GameOver, Victory, Credits)
    /// GameRoot.Save      save data + GameRoot.Settings
    /// GameRoot.Audio     Play("coin"), PlayMusic("music_game")
    /// GameRoot.Juice     HitStop, Shake, Flash, FloatingText
    /// GameRoot.Input     the Gameplay and UI actions
    /// GameRoot.Dialogue  Play(inkFile, "knot")
    /// GameRoot.Hud       Show(), SetHealth(), SetScoreText(), Message()
    /// GameRoot.Run       this run's time, score and extra stat lines
    /// GameRoot.Win() / GameRoot.Lose()
    /// </code>
    /// Time.timeScale belongs to GameRoot: 0 while paused, in dialogue and on end screens, the hit
    /// stop's scale while playing. Gameplay code should only read it.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class GameRoot : MonoBehaviour
    {
        static GameRoot _instance;

        GameConfig _config;
        ShellAssets _assets;
        GameFlow _flow;
        SaveService _save;
        RunStats _run;
        InputService _input;
        AudioService _audio;
        JuiceService _juice;
        SceneService _scenes;
        ShellUI _ui;
        DialogueService _dialogue;
        AudioListener _listener;
        string _runScene = "";
        bool _lastRunWasBest;
        bool _quitting;

        public static GameRoot Instance => _instance;
        public static bool Exists => _instance != null;

        public static GameConfig Config => _instance._config;
        public static GameFlow Flow => _instance._flow;
        public static SaveService Save => _instance._save;
        public static Settings Settings => _instance._save.Settings;
        public static RunStats Run => _instance._run;
        public static InputService Input => _instance._input;
        public static AudioService Audio => _instance._audio;
        public static JuiceService Juice => _instance._juice;
        public static DialogueService Dialogue => _instance._dialogue;
        public static HudView Hud => _instance._ui.Hud;
        public static ShellUI Ui => _instance._ui;
        public static bool IsLoadingScene => _instance != null && _instance._scenes.IsLoading;

        public SceneService SceneService => _scenes;
        public bool LastRunWasBest => _lastRunWasBest;
        /// <summary>The scene Retry and Restart go back to.</summary>
        public string RunScene => _runScene;

        /// <summary>Raised once the shell is ready (after the save loaded). Static so templates can hook in early.</summary>
        public static event Action<GameRoot> Ready;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance = null;
            Ready = null;
            DebugMenu.GodMode = false;
            DebugMenu.TimeScale = 1f;
            DebugMenu.SkipHandler = null;
            SmokeRunner.GameplayDriver = null;
        }

        /// <summary>Runs on a different config (tests use an in-memory copy, so the asset on disk is
        /// never touched). Scenes, How to Play pages and music ids are read from it from then on.</summary>
        public static void UseConfig(GameConfig config)
        {
            if (_instance != null && config != null) _instance._config = config;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoCreate()
        {
            if (_instance != null) return;
            var go = new GameObject("[JamKit GameRoot]");
            go.AddComponent<GameRoot>();
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            _config = Resources.Load<GameConfig>(GameConfig.ResourcePath);
            if (_config == null)
            {
                Debug.LogWarning("JamKit: no GameConfig at Resources/" + GameConfig.ResourcePath + "; using defaults. Run JamKit > Rebuild Shell Scenes.");
                _config = ScriptableObject.CreateInstance<GameConfig>();
            }
            _assets = Resources.Load<ShellAssets>(ShellAssets.ResourcePath);
            if (_assets == null) Debug.LogWarning("JamKit: no ShellAssets in Resources; menus will be bare. Run JamKit > Rebuild Shell Scenes.");

            bool smoke = SmokeRunner.Requested(out _);
            _flow = new GameFlow();
            _run = new RunStats();
            _save = new SaveService(smoke ? "smoke" : "save");
            if (smoke) _save.Delete();
            _save.Load();
            _input = new InputService(_assets != null ? _assets.input : null, _flow);
            _input.Bindings.UseSave(_save); // the player's rebinds, before any scene reads input

            _listener = gameObject.AddComponent<AudioListener>();
            _audio = gameObject.AddComponent<AudioService>();
            _audio.Init(_config.audio);
            _juice = gameObject.AddComponent<JuiceService>();
            _ui = gameObject.AddComponent<ShellUI>();
            _ui.Init(_assets, transform);
            _juice.Init(_ui);
            _scenes = gameObject.AddComponent<SceneService>();
            _scenes.Init(_ui, _juice);
            _dialogue = gameObject.AddComponent<DialogueService>();
            _dialogue.Init(_ui.Dialogue);
            CreateEventSystem();

            _save.SettingsChanged += ApplySettings;
            ApplySettings(_save.Settings);
            _flow.Changed += OnFlowChanged;
            _flow.RunStarted += OnRunStarted;
            SceneManager.sceneLoaded += OnSceneLoaded;

            gameObject.AddComponent<PresenterRunner>();
            gameObject.AddComponent<PointerCursor>(); // the cursor hides while the pad is in use
            if (smoke) gameObject.AddComponent<SmokeRunner>();
            Ready?.Invoke(this);
        }

        void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            go.AddComponent<EventSystem>();
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.actionsAsset = _input.Asset;
            module.point = UnityEngine.InputSystem.InputActionReference.Create(_input.Point);
            module.leftClick = UnityEngine.InputSystem.InputActionReference.Create(_input.Click);
            module.scrollWheel = UnityEngine.InputSystem.InputActionReference.Create(_input.ScrollWheel);
            module.move = UnityEngine.InputSystem.InputActionReference.Create(_input.Navigate);
            module.submit = UnityEngine.InputSystem.InputActionReference.Create(_input.Submit);
            module.cancel = UnityEngine.InputSystem.InputActionReference.Create(_input.Cancel);
            module.deselectOnBackgroundClick = false;
            go.SetActive(true);
        }

        void OnDestroy()
        {
            if (_instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_save != null) _save.SettingsChanged -= ApplySettings;
            _input?.Dispose();
            _instance = null;
        }

        void ApplySettings(Settings s)
        {
            _audio.ApplySettings(s);
            _juice.ApplySettings(s);
            if (CanSetFullscreen && Screen.fullScreen != s.fullscreen) Screen.fullScreen = s.fullscreen;
        }

        static bool CanSetFullscreen =>
            !Application.isEditor && !Application.isBatchMode && Application.platform != RuntimePlatform.WebGLPlayer;

        // ---------- Time ----------

        void Update()
        {
            float unscaled = Time.unscaledDeltaTime;
            _input.BeginFrame();
            _juice.Tick(unscaled, _flow.Current == GameState.Paused);
            _flow.Tick(unscaled);
            // Playing: hit stop x debug speed x presenter slow motion / freeze; 0 when paused, in
            // dialogue, on the end screens and behind a scene load (core GameTime.Scale).
            Time.timeScale = GameTime.Scale(_flow.Current, _scenes.IsLoading, _juice.HitStopState.CurrentTimeScale,
                DebugMenu.TimeScale, PresenterMode.TimeFactor);
            if (_flow.Current == GameState.Playing && !_scenes.IsLoading) _run.Tick(Time.deltaTime);
        }

        // ---------- Scenes ----------

        enum SceneRole { Boot, Title, Gameplay, Ignored }

        SceneRole RoleOf(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName) || sceneName.StartsWith("InitTestScene", StringComparison.Ordinal)) return SceneRole.Ignored;
            if (sceneName == _config.bootScene) return SceneRole.Boot;
            if (sceneName == _config.titleScene) return SceneRole.Title;
            return SceneRole.Gameplay;
        }

        // In a player the first scene is already loaded when GameRoot is created (BeforeSceneLoad),
        // and sceneLoaded doesn't fire for it, so Start handles it. The editor does fire it; the
        // handle check makes sure each scene is handled once either way.
        int _handledScene = -1;

        void Start()
        {
            var active = SceneManager.GetActiveScene();
            if (active.IsValid() && active.isLoaded) OnSceneLoaded(active, LoadSceneMode.Single);
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            if (scene.handle == _handledScene) return;
            _handledScene = scene.handle;
            var role = RoleOf(scene.name);
            if (role == SceneRole.Ignored) return;
            // Our listener hears the shell's sounds; if the scene brings its own (a camera's), use that one.
            _listener.enabled = FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length <= 1;
            _ui.Hud.ResetForScene();
            _juice.ResetAll();
            switch (role)
            {
                case SceneRole.Boot:
                    _ui.SetFade(1f); // start black and fade into the title
                    GoToTitleState();
                    _ui.OnFlowChanged(GameState.Title, GameState.Title); // a fresh boot: no overlays left open
                    _scenes.LoadQueued(_config.titleScene);
                    break;
                case SceneRole.Title:
                    if (_flow.Current != GameState.Credits) GoToTitleState();
                    // The title's own sound, and nothing from the scene before it: the old scene's objects are gone by now,
                    // but some hand the world's sound back as they go (a battle restoring the music and ambience it took
                    // over), and the ambience bed lives on the GameRoot. Quit to title used to carry them in.
                    _audio.StopAmbience(0.4f);
                    _audio.PlayMusic(_config.titleMusic);
                    break;
                case SceneRole.Gameplay:
                    if (!_flow.InRun)
                    {
                        // Played directly, or loaded by hand: start a run in this scene.
                        if (_flow.Current != GameState.Title && _flow.Current != GameState.GameOver && _flow.Current != GameState.Victory)
                            GoToTitleState();
                        _runScene = scene.name;
                        BeginRun();
                    }
                    if (string.IsNullOrEmpty(_runScene)) _runScene = scene.name;
                    _audio.PlayMusic(_config.gameMusic);
                    break;
            }
        }

        /// <summary>Gets GameFlow to Title from any state.</summary>
        void GoToTitleState()
        {
            if (_dialogue.IsActive) _dialogue.Stop();
            switch (_flow.Current)
            {
                case GameState.Title: return;
                case GameState.Boot: _flow.ShowTitle(); return;
                case GameState.Playing:
                case GameState.Dialogue:
                    _flow.Pause();
                    _flow.QuitToTitle();
                    return;
                default:
                    _flow.QuitToTitle();
                    return;
            }
        }

        bool BeginRun()
        {
            if (!_flow.StartRun()) return false;
            _save.Data.RecordRunStarted();
            return true;
        }

        void OnRunStarted()
        {
            _run.Reset();
            _lastRunWasBest = false;
            _juice.ResetAll();
        }

        void OnFlowChanged(GameState from, GameState to)
        {
            switch (to)
            {
                case GameState.Victory:
                    _lastRunWasBest = _save.Data.RecordRunEnded(true, 0, _run.time);
                    _save.Save();
                    _audio.StopMusic(0.3f);
                    _audio.Play("win");
                    break;
                case GameState.GameOver:
                    _save.Data.RecordRunEnded(false, 0, _run.time);
                    _save.Save();
                    _audio.StopMusic(0.3f);
                    _audio.Play("lose");
                    break;
                case GameState.Title:
                    _audio.PlayMusic(_config.titleMusic);
                    _audio.StopAmbience(0.8f);
                    break;
            }
            if (to == GameState.Paused || to == GameState.Title) _juice.HitStopState.Clear();
            _ui.OnFlowChanged(from, to);
        }

        // ---------- Shell actions (menus call these; so can game code) ----------

        /// <summary>Title's Play: the difficulty picker first (GameConfig.chooseDifficultyOnPlay), then How to
        /// Play on the very first run (if configured), then the first gameplay scene.</summary>
        public void StartGame() => StartGame(true);

        /// <summary>Like Play, but <paramref name="askDifficulty"/> false skips the picker (loading a story
        /// save, a "Continue" button: the player already chose).</summary>
        public void StartGame(bool askDifficulty)
        {
            if (_scenes.IsLoading || _flow.Current != GameState.Title) return;
            if (askDifficulty && _config.AsksDifficultyOnPlay && _ui.DifficultyPicker != null)
            {
                _ui.OpenDifficultyPicker();
                return;
            }
            LaunchRun();
        }

        /// <summary>The picker's choice: saves <paramref name="difficulty"/> as the Settings difficulty (its
        /// assists apply; the same choice as before keeps any hand-tuned assists) and starts the run.</summary>
        public void StartRun(Difficulty difficulty)
        {
            if (_scenes.IsLoading || _flow.Current != GameState.Title) return;
            var s = _save.Settings;
            if (s.difficulty != difficulty)
            {
                s.SetDifficulty(difficulty);
                _save.NotifySettingsChanged();
            }
            _save.Save();
            if (_ui.IsOverlayOpen(_ui.DifficultyPicker)) _ui.CloseOverlay(false);
            LaunchRun();
        }

        void LaunchRun()
        {
            if (_scenes.IsLoading || _flow.Current != GameState.Title) return;
            if (_config.showHowToPlayOnFirstRun && !_save.Data.seenHowToPlay && _config.howToPlay != null && _config.howToPlay.Count > 0)
            {
                _save.Data.seenHowToPlay = true;
                _save.Save();
                _ui.OpenHowToPlay(LaunchRun);
                return;
            }
            _runScene = _config.firstGameplayScene;
            if (!SceneService.Exists(_runScene))
            {
                Debug.LogError($"JamKit: GameConfig.firstGameplayScene \"{_runScene}\" isn't in Build Settings.");
                return;
            }
            if (BeginRun()) _scenes.Load(_runScene);
        }

        /// <summary>Retry (game over) and Restart (pause): a fresh run from the run's first scene.</summary>
        public void Retry()
        {
            if (_scenes.IsLoading) return;
            if (_dialogue.IsActive) _dialogue.Stop();
            bool ok = _flow.Current == GameState.Paused || _flow.Current == GameState.Playing ? _flow.Restart() : _flow.StartRun();
            if (!ok) return;
            _save.Data.RecordRunStarted();
            string scene = string.IsNullOrEmpty(_runScene) ? _config.firstGameplayScene : _runScene;
            _scenes.Load(scene);
        }

        /// <summary>
        /// Presenter mode's way in: a fresh run in <paramref name="scene"/> from any state (title, pause,
        /// game over, victory, credits), skipping the difficulty picker and the How to Play cards. Retry
        /// and Restart go back to that scene. False while a scene loads or if the scene isn't in Build Settings.
        /// </summary>
        public bool StartRunAt(string scene)
        {
            if (_scenes.IsLoading || !SceneService.Exists(scene)) return false;
            if (_dialogue.IsActive) _dialogue.Stop();
            if (_flow.Current == GameState.Credits || _flow.Current == GameState.Boot) GoToTitleState();
            bool ok = _flow.Current == GameState.Paused || _flow.Current == GameState.Playing ? _flow.Restart() : _flow.StartRun();
            if (!ok) return false;
            _save.Data.RecordRunStarted();
            _runScene = scene;
            _scenes.Load(scene);
            return true;
        }

        public void Pause() => _flow.Pause();

        public void Resume() => _flow.Resume();

        public void QuitToTitle()
        {
            if (_scenes.IsLoading) return;
            GoToTitleState();
            _save.Save();
            if (SceneService.ActiveName != _config.titleScene) _scenes.Load(_config.titleScene);
        }

        /// <summary>From the title or the victory screen. Loads the title scene behind the credits if needed.</summary>
        public void ShowCredits()
        {
            if (_scenes.IsLoading || !_flow.ShowCredits()) return;
            if (SceneService.ActiveName != _config.titleScene) _scenes.Load(_config.titleScene);
        }

        public void CloseCredits()
        {
            if (_scenes.IsLoading) return;
            if (_flow.Current == GameState.Credits) _flow.QuitToTitle();
            if (SceneService.ActiveName != _config.titleScene) _scenes.Load(_config.titleScene);
        }

        public void QuitApp()
        {
            _save.Save();
            _quitting = true;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>End the run as a win (shows Victory). Works from Playing and Dialogue.</summary>
        public static void Win()
        {
            if (!Exists) return;
            if (_instance._dialogue.IsActive) _instance._dialogue.Stop();
            _instance._flow.Win();
        }

        /// <summary>End the run as a loss (shows Game Over).</summary>
        public static void Lose()
        {
            if (!Exists) return;
            if (_instance._dialogue.IsActive) _instance._dialogue.Stop();
            _instance._flow.Lose();
        }

        void OnApplicationFocus(bool focused)
        {
            // Alt-tab or clicking outside the browser tab pauses, so nobody dies while away.
            if (!focused && !Application.isEditor && !Application.isBatchMode && !_quitting && _flow.Current == GameState.Playing)
                _flow.Pause();
        }

        void OnApplicationQuit()
        {
            _quitting = true;
            _save?.Save();
        }
    }
}

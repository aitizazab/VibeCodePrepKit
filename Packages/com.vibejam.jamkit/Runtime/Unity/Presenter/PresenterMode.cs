using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using JamKit.Flow;
using JamKit.Presenting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace JamKit.Shell
{
    /// <summary>A template's attract-mode driver: plays <paramref name="segment"/> for about
    /// <paramref name="seconds"/> of real time through <paramref name="input"/> (the synthetic pad).</summary>
    public delegate IEnumerator AttractDriver(string segment, float seconds, SyntheticInput input);

    /// <summary>
    /// Presenter mode, for the team presenting the game live (works in release builds). A hidden
    /// chord opens its panel: <b>Ctrl+Shift+P</b>, or hold <b>L3+R3</b> (both stick clicks) for 1 s on a
    /// pad. The panel has the chapter select (jump points each template registers), Invincible, Slow
    /// motion (1 / 0.5 / 0.25), Freeze frame, Hide HUD/UI, Photo (UI hidden, 2x PNG to
    /// Pictures/JamKit), Attract mode and Exit. Nothing shows until it's opened; closing the panel
    /// keeps the toggles (so the demo can be played invincible), Exit undoes everything. Nothing is
    /// saved: the toggles last for this session only. Game time stops behind the open panel.
    /// <para>Templates: <see cref="Declare"/> their chapter names (at load), <see cref="Register"/> the
    /// live jumps in Start and <see cref="Unregister"/> in OnDestroy, set an <see cref="AttractScript"/>
    /// and register an <see cref="AttractDriver"/>. See docs/PRESENTING.md.</para>
    /// </summary>
    public static class PresenterMode
    {
        static readonly ChapterRegistry _chapters = new ChapterRegistry();
        static readonly PresenterTime _time = new PresenterTime();
        static readonly Dictionary<string, AttractScript> _scripts = new Dictionary<string, AttractScript>();
        static readonly List<DriverEntry> _drivers = new List<DriverEntry>();
        static readonly List<int> _scratch = new List<int>();
        static bool _invincible, _indicator;
        static string _photoFolder;
        static string _lastPhoto = "";
        static PresenterRunner _runner;

        /// <summary>Owner for chapters that work from any state (no run needed), e.g. an opening on the title.</summary>
        public static readonly object AnyTime = new object();

        struct DriverEntry
        {
            public object owner;
            public string group;
            public AttractDriver driver;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // Declarations come after this (AfterAssembliesLoaded), so they survive a play-mode restart.
            _chapters.Clear();
            _time.Reset();
            _scripts.Clear();
            _drivers.Clear();
            _invincible = _indicator = false;
            _photoFolder = null;
            _lastPhoto = "";
            _runner = null;
            _verbose = VerboseRequested();
        }

        internal static void Attach(PresenterRunner runner) => _runner = runner;

        static bool _verbose = VerboseRequested();

        /// <summary>Log what presenter mode does ("jumped to...", "photo saved to...", attract on/off).
        /// Off in normal play so a game's console and its tests stay clean; on in a -jamSmoke run, with
        /// -jamPresenterLog, and while JamKit > Record drives attract mode. Warnings and errors always log.</summary>
        public static bool Verbose
        {
            get => _verbose;
            set => _verbose = value;
        }

        static bool VerboseRequested()
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer) return false;
            foreach (var a in Environment.GetCommandLineArgs())
                if (a == "-jamPresenterLog" || a == "-jamSmoke") return true;
            return false;
        }

        internal static void Note(string what)
        {
            if (_verbose) Debug.Log("JamKit presenter: " + what);
        }

        public static ChapterRegistry Chapters => _chapters;
        public static PresenterTime Time => _time;

        // ---------- Toggles ----------

        /// <summary>The player takes no damage (no deaths). Nothing on screen says so unless the indicator is on.</summary>
        public static bool Invincible
        {
            get => _invincible;
            set => _invincible = value;
        }

        /// <summary>Invincible, or attract mode running (it plays invincible). Damage code reads
        /// <see cref="DebugMenu.PlayerInvincible"/>, which includes this.</summary>
        public static bool InvincibleNow => _invincible || AttractRunning;

        /// <summary>A small status line in the corner ("PRESENTER  invincible  x0.5").</summary>
        public static bool ShowIndicator
        {
            get => _indicator;
            set => _indicator = value;
        }

        public static bool Frozen
        {
            get => _time.Frozen;
            set => _time.Frozen = value;
        }

        public static float SlowMotion => _time.SlowMotion;

        public static float CycleSlowMotion() => _time.CycleSlowMotion();

        public static void SetSlowMotion(float speed)
        {
            int best = 0;
            for (int i = 1; i < PresenterTime.SlowSteps.Length; i++)
                if (Mathf.Abs(PresenterTime.SlowSteps[i] - speed) < Mathf.Abs(PresenterTime.SlowSteps[best] - speed)) best = i;
            _time.SetSlowStep(best);
        }

        /// <summary>The HUD, dialogue box and overlays hidden (the picture stays; menus still open).</summary>
        public static bool UiHidden
        {
            get => GameRoot.Exists && GameRoot.Ui.UiHidden;
            set { if (GameRoot.Exists) GameRoot.Ui.SetUiHidden(value); }
        }

        /// <summary>The factor GameRoot folds into Time.timeScale while playing.</summary>
        public static float TimeFactor => _time.Factor;

        /// <summary>Gameplay input is off: the panel is open (backstage) or the frame is frozen.</summary>
        public static bool BlocksGameplay => _time.Frozen || _time.Backstage;

        /// <summary>Anything presenter mode changed is still in effect.</summary>
        public static bool Active => _invincible || _indicator || !_time.IsDefault || UiHidden || AttractRunning || IsOpen;

        // ---------- The panel ----------

        public static bool IsOpen => GameRoot.Exists && GameRoot.Ui.Presenter != null && GameRoot.Ui.IsOverlayOpen(GameRoot.Ui.Presenter);

        /// <summary>Ctrl and Shift are both down (the start of the keyboard chord: the shell ignores P as pause then).</summary>
        public static bool ChordKeysDown
        {
            get
            {
                var kb = Keyboard.current;
                return kb != null && kb.ctrlKey.isPressed && kb.shiftKey.isPressed;
            }
        }

        /// <summary>Opens the panel over whatever is on screen (stops attract mode).</summary>
        public static void Open()
        {
            if (!GameRoot.Exists || IsOpen) return;
            StopAttract();
            var ui = GameRoot.Ui;
            ui.OpenOverlay(ui.Presenter);
        }

        /// <summary>Hides the panel; the toggles stay on.</summary>
        public static void Hide()
        {
            if (!IsOpen) return;
            var ui = GameRoot.Ui;
            if (ui.Top == ui.Presenter) ui.CloseOverlay(false);
            else ui.Presenter.Hide();
        }

        public static void Toggle()
        {
            if (IsOpen) Hide();
            else Open();
        }

        /// <summary>Exit presenter mode: stops attract mode, turns every toggle off, shows the UI and hides the panel.</summary>
        public static void Close()
        {
            StopAttract();
            _invincible = false;
            _indicator = false;
            _time.Reset();
            UiHidden = false;
            _chapters.CancelPending();
            Hide();
            _time.Backstage = false;
        }

        internal static void SetBackstage(bool on) => _time.Backstage = on;

        // ---------- Chapters ----------

        /// <summary>Lists a game scene's chapters before it runs (call at load: RuntimeInitializeOnLoadMethod AfterAssembliesLoaded).</summary>
        public static void Declare(string scene, IList<string> names) => _chapters.Declare(scene, names);

        /// <summary>Makes a chapter live while its scene runs; <paramref name="owner"/> (usually the
        /// template's game component) unregisters them all at once.</summary>
        public static void Register(string scene, string name, Action jump, object owner) => _chapters.Register(scene, name, jump, owner);

        /// <summary>Takes away <paramref name="owner"/>'s live chapters and attract driver.</summary>
        public static void Unregister(object owner)
        {
            _chapters.Unregister(owner);
            UnregisterDriver(owner);
        }

        /// <summary>The scene whose chapters the panel lists: the running game scene if it has any,
        /// else the active template's first gameplay scene.</summary>
        public static string CurrentGroup
        {
            get
            {
                if (!GameRoot.Exists) return "";
                string active = SceneService.ActiveName;
                if (_chapters.InGroup(active, _scratch) > 0) return active;
                return GameRoot.Config.firstGameplayScene ?? "";
            }
        }

        /// <summary>The current group's chapter names, in order (fills <paramref name="output"/>).</summary>
        public static int ChapterNames(List<string> output)
        {
            output.Clear();
            _chapters.InGroup(CurrentGroup, _scratch);
            for (int i = 0; i < _scratch.Count; i++) output.Add(_chapters.NameAt(_scratch[i]));
            return output.Count;
        }

        public static JumpResult Jump(string name) => Jump(CurrentGroup, name);

        /// <summary>
        /// Jumps to a chapter in a clean state: the panel and any overlay closed, no fade, flash, shake
        /// or hit stop left, unpaused. In the chapter's scene during a run it jumps at once; from the
        /// title, an end screen or another scene it starts a fresh run there and jumps when the scene
        /// has registered the chapter (Pending).
        /// </summary>
        public static JumpResult Jump(string group, string name)
        {
            if (!GameRoot.Exists || GameRoot.IsLoadingScene) return JumpResult.Failed;
            int i = _chapters.IndexOf(group, name);
            if (i < 0)
            {
                Debug.LogWarning("JamKit presenter: no chapter \"" + name + "\" in " + group);
                return JumpResult.Unknown;
            }
            PrepareForJump();
            var flow = GameRoot.Flow;
            bool live = _chapters.IsLive(i);
            bool anyTime = live && ReferenceEquals(_chapters.OwnerAt(i), AnyTime);
            bool inScene = SceneService.ActiveName == group && flow.InRun;
            if (live && (anyTime || inScene))
            {
                if (flow.Current == GameState.Paused) GameRoot.Instance.Resume();
                return Run(group, name);
            }
            _chapters.SetPending(group, name);
            if (inScene) return JumpResult.Pending; // the scene is still starting: it registers the chapter soon
            if (!GameRoot.Instance.StartRunAt(group))
            {
                _chapters.CancelPending();
                Debug.LogWarning("JamKit presenter: can't start " + group + " for chapter \"" + name + "\" (is the scene in Build Settings?)");
                return JumpResult.Failed;
            }
            return JumpResult.Pending;
        }

        static JumpResult Run(string group, string name)
        {
            var r = _chapters.Jump(group, name);
            if (r == JumpResult.Failed) Debug.LogError("JamKit presenter: chapter \"" + name + "\" failed: " + _chapters.LastError);
            else if (r == JumpResult.Jumped) PresenterMode.Note("jumped to \"" + name + "\"");
            return r;
        }

        /// <summary>The chapter after the last one jumped to (or the first): what to do when the demo goes wrong.</summary>
        public static JumpResult NextChapter()
        {
            string group = CurrentGroup;
            string last = _chapters.LastJumpedGroup == group ? _chapters.LastJumped : "";
            string next = last.Length > 0 ? _chapters.NextAfter(group, last) : "";
            if (next.Length == 0) next = _chapters.FirstIn(group);
            return next.Length > 0 ? Jump(group, next) : JumpResult.Unknown;
        }

        /// <summary>No half-finished overlay, fade, flash, shake, hit stop or HUD message carries into a chapter.</summary>
        static void PrepareForJump()
        {
            var ui = GameRoot.Ui;
            ui.CloseOverlays();
            _time.Backstage = false;
            if (!GameRoot.IsLoadingScene) ui.SetFade(0f);
            ui.ClearFlash();
            GameRoot.Juice.ResetAll();
            GameRoot.Hud.ClearMessage();
        }

        /// <summary>Called by the runner every frame: finishes a pending jump once its scene registered it.</summary>
        internal static void TickPending(int framesSinceLoad)
        {
            if (!_chapters.HasPending || !GameRoot.Exists || GameRoot.IsLoadingScene) return;
            if (!_chapters.PendingReady || framesSinceLoad < 2) return;
            var flow = GameRoot.Flow;
            int i = _chapters.IndexOf(_chapters.PendingGroup, _chapters.PendingName);
            bool anyTime = i >= 0 && ReferenceEquals(_chapters.OwnerAt(i), AnyTime);
            if (!anyTime && (!flow.InRun || SceneService.ActiveName != _chapters.PendingGroup)) return;
            if (flow.Current == GameState.Paused) GameRoot.Instance.Resume();
            PrepareForJump();
            string g = _chapters.PendingGroup, n = _chapters.PendingName;
            Run(g, n);
        }

        // ---------- Photo ----------

        /// <summary>Where photos go: Pictures/JamKit (tests point it elsewhere; null = the default).</summary>
        public static string PhotoFolder
        {
            get
            {
                if (!string.IsNullOrEmpty(_photoFolder)) return _photoFolder;
                string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                return string.IsNullOrEmpty(pictures)
                    ? Path.Combine(Application.persistentDataPath, "Photos")
                    : Path.Combine(pictures, "JamKit");
            }
            set => _photoFolder = value;
        }

        /// <summary>The last photo's full path ("" before the first).</summary>
        public static string LastPhotoPath => _lastPhoto;
        public static bool TakingPhoto => _runner != null && _runner.TakingPhoto;
        public static bool CanTakePhoto => Application.platform != RuntimePlatform.WebGLPlayer;

        public static event Action<string> PhotoSaved;

        /// <summary>Hides the panel and all UI, saves a 2x supersampled PNG and flashes like a shutter.</summary>
        public static bool TakePhoto()
        {
            if (_runner == null || !CanTakePhoto || _runner.TakingPhoto) return false;
            Hide();
            _runner.StartCoroutine(_runner.Photo());
            return true;
        }

        internal static void NotePhoto(string path)
        {
            _lastPhoto = path;
            PhotoSaved?.Invoke(path);
        }

        // ---------- Attract mode ----------

        /// <summary>The attract script for a game scene (a template sets it at load).</summary>
        public static void SetAttractScript(AttractScript script)
        {
            if (script == null || string.IsNullOrEmpty(script.scene)) return;
            _scripts[script.scene] = script;
        }

        public static AttractScript AttractScriptFor(string scene) =>
            scene != null && _scripts.TryGetValue(scene, out var s) ? s : null;

        public static void RegisterDriver(object owner, string scene, AttractDriver driver)
        {
            if (owner == null || driver == null) return;
            UnregisterDriver(owner);
            _drivers.Add(new DriverEntry { owner = owner, group = scene ?? "", driver = driver });
        }

        public static void UnregisterDriver(object owner)
        {
            for (int i = _drivers.Count - 1; i >= 0; i--)
                if (ReferenceEquals(_drivers[i].owner, owner)) _drivers.RemoveAt(i);
        }

        internal static AttractDriver DriverFor(string scene)
        {
            for (int i = _drivers.Count - 1; i >= 0; i--)
                if (_drivers[i].group == scene) return _drivers[i].driver;
            return null;
        }

        public static bool AttractRunning => _runner != null && _runner.AttractRunning;

        /// <summary>The step attract mode is on ("" when not running).</summary>
        public static string AttractStep => _runner != null ? _runner.AttractStepText : "";

        /// <summary>Starts attract mode with the active template's script (it loops until stopped:
        /// Escape, the presenter chord, or Exit). False when the template has none.</summary>
        public static bool StartAttract()
        {
            if (_runner == null || !GameRoot.Exists) return false;
            string scene = GameRoot.Config.firstGameplayScene;
            var script = AttractScriptFor(CurrentGroup) ?? AttractScriptFor(scene);
            if (script == null)
            {
                Debug.LogWarning("JamKit presenter: no attract script for " + CurrentGroup);
                return false;
            }
            Hide();
            return _runner.StartAttract(script);
        }

        public static void StopAttract()
        {
            if (_runner != null) _runner.StopAttract();
        }

        /// <summary>True when the player started the build with -jamAttract.</summary>
        public static bool AttractRequested()
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer) return false;
            foreach (var a in Environment.GetCommandLineArgs())
                if (a == "-jamAttract") return true;
            return false;
        }
    }

    /// <summary>
    /// Presenter mode's per-frame side, on the GameRoot: the chord, pending chapter jumps, the
    /// indicator, the photo and the attract loop.
    /// </summary>
    public sealed class PresenterRunner : MonoBehaviour
    {
        readonly HoldChord _padChord = new HoldChord { holdSeconds = 1f };
        int _loadedFrame;
        bool _attractOnTitle;
        float _titleWait;
        bool _photo;
        string _indicatorText = "";

        // Attract
        Coroutine _attract;
        SyntheticInput _synthetic;
        bool _savedAuto;
        string _stepText = "";
        float _choiceWait;

        public bool TakingPhoto => _photo;
        public bool AttractRunning => _attract != null;
        public string AttractStepText => _stepText;

        void Awake()
        {
            PresenterMode.Attach(this);
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            _attractOnTitle = PresenterMode.AttractRequested();
        }

        void OnDestroy()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            StopAttract();
        }

        void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => _loadedFrame = UnityEngine.Time.frameCount;

        void Update()
        {
            if (!GameRoot.Exists) return;
            float udt = UnityEngine.Time.unscaledDeltaTime;
            var ui = GameRoot.Ui;
            PresenterMode.SetBackstage(PresenterMode.IsOpen);

            // The chord: Ctrl+Shift+P, or both stick clicks held for a second.
            var kb = Keyboard.current;
            bool keyChord = kb != null && kb.pKey.wasPressedThisFrame && kb.ctrlKey.isPressed && kb.shiftKey.isPressed;
            var pad = Gamepad.current;
            bool padDown = pad != null && pad.leftStickButton.isPressed && pad.rightStickButton.isPressed;
            bool padChord = _padChord.Tick(padDown, udt);
            if ((keyChord || padChord) && !GameRoot.IsLoadingScene && !_photo) PresenterMode.Toggle();

            PresenterMode.TickPending(UnityEngine.Time.frameCount - _loadedFrame);

            // -jamAttract: once the title is up (after any opening), play the attract loop.
            if (_attractOnTitle && GameRoot.Flow.Current == GameState.Title && !GameRoot.IsLoadingScene)
            {
                _titleWait += udt;
                if (_titleWait > 1.5f)
                {
                    _attractOnTitle = false;
                    PresenterMode.StartAttract();
                }
            }

            if (_attract != null) TickAttractDialogue(udt);
            UpdateIndicator();
        }

        void UpdateIndicator()
        {
            var label = GameRoot.Ui.PresenterIndicator;
            if (label == null) return;
            bool show = PresenterMode.ShowIndicator && !_photo;
            label.style.display = show ? UnityEngine.UIElements.DisplayStyle.Flex : UnityEngine.UIElements.DisplayStyle.None;
            if (!show) return;
            string text = "PRESENTER";
            if (PresenterMode.InvincibleNow) text += "  invincible";
            if (PresenterMode.SlowMotion < 1f) text += "  x" + PresenterMode.SlowMotion.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            if (PresenterMode.Frozen) text += "  frozen";
            if (PresenterMode.UiHidden) text += "  UI hidden";
            if (PresenterMode.AttractRunning) text += "  attract: " + _stepText;
            if (text != _indicatorText)
            {
                _indicatorText = text;
                label.text = text;
            }
        }

        // ---------- Photo ----------

        internal IEnumerator Photo()
        {
            _photo = true;
            bool wasHidden = PresenterMode.UiHidden;
            PresenterMode.UiHidden = true;
            yield return null; // a frame for the hidden layout
            Texture2D tex = null;
            try
            {
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                {
                    // No graphics device (batch tests): a blank frame of the right size, so the file still appears.
                    tex = new Texture2D(Mathf.Max(2, Screen.width * 2), Mathf.Max(2, Screen.height * 2), TextureFormat.RGB24, false);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("JamKit presenter: photo failed (" + e.Message + ")");
            }
            if (tex == null && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
            {
                yield return new WaitForEndOfFrame();
                tex = ScreenCapture.CaptureScreenshotAsTexture(2);
            }
            string path = "";
            if (tex != null)
            {
                try
                {
                    string folder = PresenterMode.PhotoFolder;
                    Directory.CreateDirectory(folder);
                    string name = PhotoNames.Unique(PhotoNames.FileName(GameRoot.Config.gameTitle, DateTime.Now), n => File.Exists(Path.Combine(folder, n)));
                    path = Path.Combine(folder, name);
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                }
                catch (Exception e)
                {
                    Debug.LogWarning("JamKit presenter: couldn't save the photo (" + e.Message + ")");
                    path = "";
                }
                Destroy(tex);
            }
            PresenterMode.UiHidden = wasHidden;
            _photo = false;
            if (path.Length > 0)
            {
                GameRoot.Juice.Flash(new Color(1f, 1f, 1f, 0.85f), 0.3f);
                GameRoot.Audio.Play("ui_select");
                PresenterMode.Note("photo saved to " + path);
                PresenterMode.NotePhoto(path);
            }
        }

        // ---------- Attract ----------

        internal bool StartAttract(AttractScript script)
        {
            StopAttract();
            _synthetic = new SyntheticInput();
            GameRoot.Input.Synthetic = _synthetic;
            _savedAuto = GameRoot.Dialogue.AutoMode;
            _choiceWait = 0f;
            _attract = StartCoroutine(AttractLoop(script));
            PresenterMode.Note("attract mode on (" + script.scene + ", " + script.steps.Count + " steps, "
                + script.TotalSeconds.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " s a loop)");
            return true;
        }

        internal void StopAttract()
        {
            if (_attract == null) return;
            StopCoroutine(_attract);
            if (_segment != null) StopCoroutine(_segment);
            _segment = null;
            _attract = null;
            _stepText = "";
            if (GameRoot.Exists)
            {
                if (ReferenceEquals(GameRoot.Input.Synthetic, _synthetic)) GameRoot.Input.Synthetic = null;
                GameRoot.Dialogue.AutoMode = _savedAuto;
            }
            _synthetic = null;
            PresenterMode.Note("attract mode off");
        }

        Coroutine _segment;
        bool _segmentDone;

        IEnumerator AttractLoop(AttractScript script)
        {
            var seq = new AttractSequencer(script);
            int failures = 0;
            while (true)
            {
                var step = seq.Next();
                if (step == null) break;
                _synthetic.Clear();
                _stepText = step.kind == AttractStepKind.Wait ? "wait" : step.name;
                switch (step.kind)
                {
                    case AttractStepKind.Jump:
                    {
                        while (GameRoot.IsLoadingScene) yield return null;
                        var r = PresenterMode.Jump(script.scene, step.name);
                        if (r == JumpResult.Pending)
                        {
                            float end = UnityEngine.Time.unscaledTime + 20f;
                            while (PresenterMode.Chapters.HasPending && UnityEngine.Time.unscaledTime < end) yield return null;
                            if (PresenterMode.Chapters.HasPending)
                            {
                                PresenterMode.Chapters.CancelPending();
                                r = JumpResult.Failed;
                            }
                        }
                        if (r == JumpResult.Failed || r == JumpResult.Unknown)
                        {
                            Debug.LogWarning("JamKit presenter: attract couldn't jump to \"" + step.name + "\"");
                            if (++failures > 3 * Mathf.Max(1, script.steps.Count)) { StopAttract(); yield break; }
                        }
                        yield return Real(step.seconds);
                        break;
                    }
                    case AttractStepKind.Drive:
                    {
                        var driver = PresenterMode.DriverFor(script.scene);
                        if (driver == null || !GameRoot.Flow.InRun)
                        {
                            yield return Real(step.seconds);
                            break;
                        }
                        _segmentDone = false;
                        _segment = StartCoroutine(Segment(driver(step.name, step.seconds, _synthetic)));
                        float end = UnityEngine.Time.unscaledTime + step.seconds + 4f;
                        while (!_segmentDone && UnityEngine.Time.unscaledTime < end) yield return null;
                        if (!_segmentDone && _segment != null) StopCoroutine(_segment);
                        _segment = null;
                        break;
                    }
                    default:
                        yield return Real(step.seconds);
                        break;
                }
            }
            _attract = null;
        }

        IEnumerator Segment(IEnumerator body)
        {
            if (body != null) yield return body;
            _segmentDone = true;
        }

        static IEnumerator Real(float seconds)
        {
            float end = UnityEngine.Time.unscaledTime + Mathf.Max(0f, seconds);
            while (UnityEngine.Time.unscaledTime < end) yield return null;
        }

        /// <summary>While attract mode plays: dialogue reads itself at the auto-mode pace, and a choice
        /// that stays up for a moment is taken (the first one).</summary>
        void TickAttractDialogue(float udt)
        {
            var d = GameRoot.Dialogue;
            if (!d.IsActive) { _choiceWait = 0f; return; }
            if (!d.AutoMode) d.AutoMode = true;
            if (d.ChoiceCount > 0 && !d.IsTyping)
            {
                _choiceWait += udt;
                if (_choiceWait > 1.8f)
                {
                    _choiceWait = 0f;
                    d.Choose(0);
                }
            }
            else _choiceWait = 0f;
        }
    }
}

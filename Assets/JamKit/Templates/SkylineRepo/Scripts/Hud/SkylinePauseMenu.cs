using System;
using System.Collections.Generic;
using JamKit.Skyline;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The pause menu (UI Toolkit, in the HUD's style: a dark backing, white text, a gold accent). It opens on the Pause
    /// binding (Escape, Start or Options by default) and closes on it again or on "Resume". While it is open the time scale is 0,
    /// the listener is paused (but for the menu's own click sound), the cursor is free, and the game gets an empty input frame
    /// (<see cref="SkylineDeviceInput.Paused"/>; <see cref="SkylinePlayer"/> does nothing). Pages:
    /// <list type="bullet">
    /// <item><b>Resume.</b></item>
    /// <item><b>Audio:</b> Master, Music, Effects and Voices, 0 to 100% (SkylineOptions; Master is the listener's volume, the
    /// rest are SkylineMix's user gains).</item>
    /// <item><b>Controls:</b> the actions with their keyboard-and-mouse and pad bindings; pick one and press a button to rebind
    /// it (Escape or Start cancels); a control another action already uses swaps the two; "Reset to defaults".</item>
    /// <item><b>Look:</b> mouse sensitivity, pad sensitivity, invert Y, aim assist.</item>
    /// <item><b>Subtitles:</b> Off / Dialogue / Dialogue + bot barks, and the size Small / Medium / Large
    /// (<see cref="SkylineSettings.SubtitleMode"/>, <see cref="SkylineSettings.SubtitleSize"/>; they act at once).</item>
    /// <item><b>Quit to desktop</b> (asks twice).</item>
    /// </list>
    /// Navigable by pad (D-pad or left stick up and down, left and right to adjust, A or Cross to select, B or Circle for
    /// back), by keyboard (arrows, Enter or Space, Backspace) and by mouse (hover, click, drag a slider). Options and bindings
    /// are saved as they change (SkylineSettings, SkylineBindings). The menu never exists in the scripted runs
    /// (<see cref="SkylineRunArgs.Demo"/>: the capture, the demo, the perf probe), and never opens while a script drives the
    /// player. It creates itself in every Skyline scene that has a player (like <see cref="SkylineMixer"/>).
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public sealed class SkylinePauseMenu : MonoBehaviour
    {
        // Look (reference 1920 x 1080 panel units).
        const float PanelWidth = 940f, RowHeight = 58f, RowGap = 6f, LabelWidth = 300f, TrackWidth = 380f, CellWidth = 280f;
        const int TitleSize = 52, RowSize = 28, HintSize = 20;
        static readonly Color Back = new Color(0.02f, 0.03f, 0.05f, 0.82f);
        static readonly Color PanelBack = new Color(0.04f, 0.05f, 0.08f, 0.94f);
        static readonly Color PanelEdge = new Color(1f, 1f, 1f, 0.1f);
        static readonly Color Gold = new Color(0.95f, 0.8f, 0.4f, 1f);
        static readonly Color RowIdle = new Color(1f, 1f, 1f, 0.05f), RowSelected = new Color(1f, 1f, 1f, 0.15f);
        static readonly Color Dim = new Color(1f, 1f, 1f, 0.62f);
        static readonly Color Shadow = new Color(0f, 0f, 0f, 0.9f);
        // Navigation repeat (unscaled seconds).
        const float RepeatFirst = 0.4f, RepeatNext = 0.11f, StickPress = 0.6f;
        const float CommitInterval = 0.3f, QuitConfirmSeconds = 3f;

        /// <summary>The menu is open (everything else holds still).</summary>
        public static bool IsOpen { get; private set; }

        enum Page { Main, Audio, Controls, Look, Subtitles }
        enum Kind { Button, Slider, Toggle, Binding, Choice }

        sealed class Item
        {
            public Kind kind;
            public VisualElement el, track, fill;
            public Label name, value;
            public Label[] cells;
            public Func<string> text;
            public Action activate;
            public float min, max, step;
            public Func<float, string> format;   // the slider's value text; null shows a percentage
            public Func<float> get;
            public Action<float> set;
            public Func<bool> getBool;
            public Action<bool> setBool;
            public string[] choices;           // a Choice row: the words, and which one is set
            public Func<int> getIndex;
            public Action<int> setIndex;
            public BindAction action;
        }

        SkylinePlayer _player;
        UIDocument _doc;
        VisualElement _overlay, _list;
        Label _title, _hint, _note;
        readonly List<Item> _items = new List<Item>();
        Page _page = Page.Main;
        int _sel, _col;
        bool _dirtyUi = true, _dirtyOptions, _resumePending, _evLive, _evPressed, _evReleased, _evHeld;
        float _prevScale = 1f, _lastCommit, _quitArmedUntil = -1f, _navNext;
        Vector2 _evPos, _lastPtr;
        Vector2Int _navPrev;
        Item _drag;
        // Rebinding: waiting for the next control of this device for this action.
        bool _capturing;
        BindDevice _captureDevice;
        BindAction _captureAction;
        int _captureFrame;
        AudioSource _click;
        AudioClip _tick, _confirm;

        // ------------------------------------------------------------------ creating itself

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            IsOpen = false;                       // statics survive when Enter Play Mode skips the domain reload
            SkylineDeviceInput.Paused = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!scene.IsValid() || !scene.name.StartsWith("Skyline", StringComparison.Ordinal)) return;
            // The scripted runs (the capture, the demo, the perf probe) never get a menu: the game must not pause under them.
            if (SkylineRunArgs.Demo) return;
            if (FindAnyObjectByType<SkylinePauseMenu>() != null) return;
            var player = FindAnyObjectByType<SkylinePlayer>();
            if (player == null) return;
            PanelSettings ps = null;
            var hud = SkylineHud.Instance;
            if (hud != null)
            {
                var hudDoc = hud.GetComponent<UIDocument>();
                if (hudDoc != null) ps = hudDoc.panelSettings;
            }
            if (ps == null)
            {
                ps = ScriptableObject.CreateInstance<PanelSettings>();
                ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                ps.referenceResolution = new Vector2Int(1920, 1080);
                ps.match = 0.5f;
            }
            // The document's panel must be set before it is enabled, so the object starts inactive.
            var go = new GameObject("SkylinePauseMenu");
            go.SetActive(false);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = ps;
            doc.sortingOrder = 1000f;
            var menu = go.AddComponent<SkylinePauseMenu>();
            menu._doc = doc;
            menu._player = player;
            go.SetActive(true);
        }

        void Start()
        {
            if (_player == null) _player = FindAnyObjectByType<SkylinePlayer>();
            var root = _doc != null ? _doc.rootVisualElement : null;
            if (root == null)
            {
                Debug.LogWarning("SkylinePauseMenu: the UIDocument has no panel; the menu is off");
                enabled = false;
                return;
            }
            Build(root);
            BuildSounds();
            _overlay.style.display = DisplayStyle.None;
        }

        void OnDestroy()
        {
            if (IsOpen) ForceClose();
            if (_dirtyOptions) SkylineSettings.Commit();
        }

        // ------------------------------------------------------------------ building

        void Build(VisualElement docRoot)
        {
            _overlay = new VisualElement { name = "skyline-pause" };
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = _overlay.style.top = _overlay.style.right = _overlay.style.bottom = 0;
            _overlay.style.backgroundColor = Back;
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            _overlay.pickingMode = PickingMode.Position;
            docRoot.Add(_overlay);
            // Pointer events, when the panel gets them: a click, a move or a drag. (Until one arrives the menu reads the mouse
            // itself, so the mouse works either way.)
            _overlay.RegisterCallback<PointerMoveEvent>(e => { _evLive = true; _evPos = e.position; });
            _overlay.RegisterCallback<PointerDownEvent>(e => { _evLive = true; _evPos = e.position; _evPressed = true; _evHeld = true; });
            _overlay.RegisterCallback<PointerUpEvent>(e => { _evLive = true; _evPos = e.position; _evReleased = true; _evHeld = false; });

            var panel = new VisualElement();
            panel.style.width = PanelWidth;
            panel.style.backgroundColor = PanelBack;
            Round(panel, 12f);
            panel.style.borderTopWidth = panel.style.borderBottomWidth = panel.style.borderLeftWidth = panel.style.borderRightWidth = 1f;
            panel.style.borderTopColor = panel.style.borderBottomColor = panel.style.borderLeftColor = panel.style.borderRightColor = PanelEdge;
            panel.style.borderTopWidth = 4f;
            panel.style.borderTopColor = Gold;
            panel.style.paddingLeft = panel.style.paddingRight = 34f;
            panel.style.paddingTop = 24f;
            panel.style.paddingBottom = 22f;
            _overlay.Add(panel);

            _title = Text(panel, TitleSize, Color.white);
            _title.style.letterSpacing = 6f;
            _title.style.marginBottom = 14f;
            _list = new VisualElement();
            panel.Add(_list);
            _note = Text(panel, HintSize, Gold, false);
            _note.style.marginTop = 10f;
            _note.style.minHeight = HintSize + 6f;
            _hint = Text(panel, HintSize - 2, Dim, false);
            _hint.style.marginTop = 6f;
            _hint.style.whiteSpace = WhiteSpace.Normal;
            _hint.text = "D-pad or stick: move and adjust     A / Cross: select     B / Circle: back     Esc / Start: resume";
        }

        static Label Text(VisualElement parent, int size, Color c, bool bold = true)
        {
            var l = new Label { pickingMode = PickingMode.Ignore };
            l.style.fontSize = size;
            l.style.color = c;
            l.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
            l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
            l.style.paddingLeft = l.style.paddingRight = l.style.paddingTop = l.style.paddingBottom = 0;
            l.style.textShadow = new TextShadow { offset = new Vector2(0f, 2f), blurRadius = 3f, color = Shadow };
            parent.Add(l);
            return l;
        }

        static void Round(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;
        }

        VisualElement NewRow()
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.height = RowHeight;
            row.style.marginBottom = RowGap;
            row.style.paddingLeft = 18f;
            row.style.paddingRight = 18f;
            row.style.backgroundColor = RowIdle;
            Round(row, 6f);
            row.style.borderLeftWidth = 5f;
            row.style.borderLeftColor = Color.clear;
            _list.Add(row);
            return row;
        }

        Item AddButton(Func<string> text, Action activate)
        {
            var it = new Item { kind = Kind.Button, text = text, activate = activate };
            it.el = NewRow();
            it.name = Text(it.el, RowSize, Color.white);
            _items.Add(it);
            return it;
        }

        Item AddSlider(string label, float min, float max, float step, Func<float> get, Action<float> set, Func<float, string> format = null)
        {
            var it = new Item { kind = Kind.Slider, min = min, max = max, step = step, get = get, set = set, format = format };
            it.el = NewRow();
            it.name = Text(it.el, RowSize, Color.white);
            it.name.text = label;
            it.name.style.width = LabelWidth;
            it.track = new VisualElement { pickingMode = PickingMode.Ignore };
            it.track.style.width = TrackWidth;
            it.track.style.height = 16f;
            it.track.style.backgroundColor = new Color(1f, 1f, 1f, 0.16f);
            Round(it.track, 8f);
            it.track.style.overflow = Overflow.Hidden;
            it.el.Add(it.track);
            it.fill = new VisualElement { pickingMode = PickingMode.Ignore };
            it.fill.style.height = 16f;
            it.fill.style.backgroundColor = Gold;
            it.track.Add(it.fill);
            it.value = Text(it.el, RowSize, Color.white);
            it.value.style.width = 110f;
            it.value.style.unityTextAlign = TextAnchor.MiddleRight;
            it.value.style.marginLeft = 14f;
            _items.Add(it);
            return it;
        }

        Item AddToggle(string label, Func<bool> get, Action<bool> set)
        {
            var it = new Item { kind = Kind.Toggle, getBool = get, setBool = set };
            it.el = NewRow();
            it.name = Text(it.el, RowSize, Color.white);
            it.name.text = label;
            it.name.style.width = LabelWidth;
            it.value = Text(it.el, RowSize, Color.white);
            _items.Add(it);
            return it;
        }

        /// <summary>A row that cycles through <paramref name="choices"/> (left and right, Enter, a click).</summary>
        Item AddChoice(string label, string[] choices, Func<int> get, Action<int> set)
        {
            var it = new Item { kind = Kind.Choice, choices = choices, getIndex = get, setIndex = set };
            it.el = NewRow();
            it.name = Text(it.el, RowSize, Color.white);
            it.name.text = label;
            it.name.style.width = LabelWidth;
            it.value = Text(it.el, RowSize, Color.white);
            _items.Add(it);
            return it;
        }

        /// <summary>A sample line, so a change of mode or size can be seen behind the menu (nothing in Off).</summary>
        static void PreviewSubtitle()
        {
            var hud = SkylineHud.Instance;
            if (hud != null) hud.Say(Speaker.Dot, "Clamped! Hold still-ish.", 3f);
        }

        void Cycle(Item it, int dir)
        {
            int n = it.choices.Length;
            it.setIndex(((it.getIndex() + dir) % n + n) % n);
            MarkOptions();
        }

        Item AddBinding(BindAction action)
        {
            var it = new Item { kind = Kind.Binding, action = action, cells = new Label[2] };
            it.el = NewRow();
            it.name = Text(it.el, RowSize, Color.white);
            it.name.text = ActionName(action);
            it.name.style.width = 190f;
            for (int c = 0; c < 2; c++)
            {
                var cell = Text(it.el, RowSize - 4, Color.white, false);
                cell.style.width = CellWidth;
                cell.style.height = RowHeight - 14f;
                cell.style.unityTextAlign = TextAnchor.MiddleCenter;
                cell.style.marginLeft = c == 0 ? 0f : 12f;
                cell.style.backgroundColor = new Color(0f, 0f, 0f, 0.35f);
                Round(cell, 5f);
                cell.style.borderTopWidth = cell.style.borderBottomWidth = cell.style.borderLeftWidth = cell.style.borderRightWidth = 2f;
                cell.style.borderTopColor = cell.style.borderBottomColor = cell.style.borderLeftColor = cell.style.borderRightColor = Color.clear;
                it.cells[c] = cell;
            }
            _items.Add(it);
            return it;
        }

        static string ActionName(BindAction a)
        {
            switch (a)
            {
                case BindAction.Crouch: return "Crouch / slide";
                case BindAction.Swap: return "Swap gun";
                case BindAction.Pause: return "Pause menu";
                default: return a.ToString();
            }
        }

        // ------------------------------------------------------------------ pages

        static readonly string[] SubtitleModeNames = { "Off", "Dialogue", "Dialogue + bot barks" };
        static readonly string[] SubtitleSizeNames = { "Small", "Medium", "Large" };

        void ShowPage(Page page)
        {
            CommitOptions();
            _page = page;
            _capturing = false;
            _drag = null;
            _list.Clear();
            _items.Clear();
            _sel = 0;
            _note.text = "";
            var o = SkylineSettings.Options;
            switch (page)
            {
                case Page.Main:
                    _title.text = "PAUSED";
                    AddButton(() => "Resume", Close);
                    AddButton(() => "Audio", () => ShowPage(Page.Audio));
                    AddButton(() => "Controls", () => ShowPage(Page.Controls));
                    AddButton(() => "Look", () => ShowPage(Page.Look));
                    AddButton(() => "Subtitles", () => ShowPage(Page.Subtitles));
                    // The Relay test mode (docs/SKYLINE-RELAY.md): restarts the arena in (or back out of) it.
                    AddButton(() => SkylineRelayMode.Active ? "Relay (test): new run" : "Relay (test)", () => { Close(); SkylineRelayMode.Restart(true); });
                    if (SkylineRelayMode.Active) AddButton(() => "Back to the normal mode", () => { Close(); SkylineRelayMode.Restart(false); });
                    AddButton(() => Time.unscaledTime < _quitArmedUntil ? "Quit to desktop: press again to confirm" : "Quit to desktop", QuitPressed);
                    break;
                case Page.Audio:
                    _title.text = "AUDIO";
                    AddSlider("Master", 0f, 1f, 0.05f, () => o.master, v => { o.master = v; SkylineSettings.ApplyMaster(); });
                    AddSlider("Music", 0f, 1f, 0.05f, () => o.music, v => o.music = v);
                    AddSlider("Effects", 0f, 1f, 0.05f, () => o.effects, v => o.effects = v);
                    AddSlider("Voices", 0f, 1f, 0.05f, () => o.voices, v => o.voices = v);
                    AddButton(() => "Back", () => ShowPage(Page.Main));
                    break;
                case Page.Controls:
                    _title.text = "CONTROLS";
                    foreach (var a in BindingTable.AllActions) AddBinding(a);
                    AddButton(() => "Reset to defaults", () =>
                    {
                        SkylineBindings.ResetToDefaults();
                        _note.text = "Controls are back to their defaults.";
                        _dirtyUi = true;
                    });
                    AddButton(() => "Back", () => ShowPage(Page.Main));
                    _hint.text = "Left and right pick keyboard / mouse or controller; A / Cross or Enter, then press the new button.     Esc / Start cancels.";
                    break;
                case Page.Look:
                    _title.text = "LOOK";
                    AddSlider("Mouse sensitivity", SkylineOptions.MinSensitivity, SkylineOptions.MaxSensitivity, 0.05f, () => o.mouseSensitivity, v => o.mouseSensitivity = v);
                    AddSlider("Pad sensitivity", SkylineOptions.MinSensitivity, SkylineOptions.MaxSensitivity, 0.05f, () => o.padSensitivity, v => o.padSensitivity = v);
                    AddSlider("Field of view", SkylineOptions.MinFieldOfView, SkylineOptions.MaxFieldOfView, 1f, () => o.fieldOfView, v => o.fieldOfView = v,
                        v => Mathf.RoundToInt(v) + " deg");
                    AddToggle("Invert Y", () => o.invertY, v => o.invertY = v);
                    AddToggle("Aim assist (pad)", () => o.aimAssist, v => o.aimAssist = v);
                    AddButton(() => "Back", () => ShowPage(Page.Main));
                    break;
                case Page.Subtitles:
                    _title.text = "SUBTITLES";
                    AddChoice("Subtitles", SubtitleModeNames, () => (int)o.subtitleMode, v => { o.subtitleMode = (SubtitleMode)v; PreviewSubtitle(); });
                    AddChoice("Size", SubtitleSizeNames, () => (int)o.subtitleSize, v => { o.subtitleSize = (SubtitleSize)v; PreviewSubtitle(); });
                    AddButton(() => "Back", () => ShowPage(Page.Main));
                    break;
            }
            if (page != Page.Controls) _hint.text = "D-pad or stick: move and adjust     A / Cross: select     B / Circle: back     Esc / Start: resume";
            _dirtyUi = true;
        }

        void QuitPressed()
        {
            if (Time.unscaledTime < _quitArmedUntil)
            {
                CommitOptions();
                Debug.Log("SKYLINE_PAUSE quit");
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                return;
            }
            _quitArmedUntil = Time.unscaledTime + QuitConfirmSeconds;
            _dirtyUi = true;
        }

        // ------------------------------------------------------------------ refreshing

        void Refresh()
        {
            var table = SkylineBindings.Table;
            for (int i = 0; i < _items.Count; i++)
            {
                var it = _items[i];
                bool sel = i == _sel;
                it.el.style.backgroundColor = sel ? RowSelected : RowIdle;
                it.el.style.borderLeftColor = sel ? Gold : Color.clear;
                switch (it.kind)
                {
                    case Kind.Button:
                        it.name.text = it.text();
                        it.name.style.color = sel ? Gold : Color.white;
                        break;
                    case Kind.Slider:
                    {
                        float v = it.get();
                        float t = Mathf.Clamp01((v - it.min) / (it.max - it.min));
                        it.fill.style.width = TrackWidth * t;
                        it.value.text = it.format != null ? it.format(v) : Mathf.RoundToInt(v * 100f) + "%";
                        it.name.style.color = sel ? Gold : Color.white;
                        break;
                    }
                    case Kind.Toggle:
                        it.value.text = it.getBool() ? "ON" : "OFF";
                        it.value.style.color = it.getBool() ? Gold : Dim;
                        it.name.style.color = sel ? Gold : Color.white;
                        break;
                    case Kind.Choice:
                        it.value.text = it.choices[Mathf.Clamp(it.getIndex(), 0, it.choices.Length - 1)];
                        it.value.style.color = Gold;
                        it.name.style.color = sel ? Gold : Color.white;
                        break;
                    case Kind.Binding:
                        it.name.style.color = sel ? Gold : Color.white;
                        for (int c = 0; c < 2; c++)
                        {
                            var dev = c == 0 ? BindDevice.KeyboardMouse : BindDevice.Pad;
                            bool waiting = _capturing && sel && _captureDevice == dev;
                            bool cur = sel && _col == c;
                            it.cells[c].text = waiting ? "press a button..." : BindingNames.Display(table.Get(dev, it.action));
                            it.cells[c].style.color = waiting ? Gold : Color.white;
                            var edge = cur ? Gold : Color.clear;
                            it.cells[c].style.borderTopColor = it.cells[c].style.borderBottomColor =
                                it.cells[c].style.borderLeftColor = it.cells[c].style.borderRightColor = edge;
                        }
                        break;
                }
            }
            _dirtyUi = false;
        }

        // ------------------------------------------------------------------ the frame

        void Update()
        {
            if (_overlay == null) return;
            if (!IsOpen)
            {
                if (PausePressed() && CanOpen()) Open();
                return;
            }

            float now = Time.unscaledTime;
            if (_dirtyOptions && now - _lastCommit > CommitInterval) CommitOptions();
            if (_quitArmedUntil > 0f && now >= _quitArmedUntil) { _quitArmedUntil = -1f; _dirtyUi = true; }

            if (_capturing) UpdateCapture();
            else
            {
                if (PausePressed()) { Close(); return; }
                UpdateNavigation(now);
                UpdatePointer();
            }
            if (IsOpen && _dirtyUi) Refresh();
        }

        void LateUpdate()
        {
            // The frame the menu closed on still holds the press that closed it (A, Escape...): the player stays asleep until the
            // frame is over, so that press is not also a jump.
            if (_resumePending)
            {
                _resumePending = false;
                if (!IsOpen) SkylineDeviceInput.Paused = false;
            }
        }

        static bool PausePressed() =>
            SkylineBindings.Pressed(BindDevice.KeyboardMouse, BindAction.Pause, true) || SkylineBindings.Pressed(BindDevice.Pad, BindAction.Pause, true);

        bool CanOpen()
        {
            if (SkylineRunArgs.Demo || _player == null) return false;
            return _player.ScriptedInput == null;
        }

        void Open()
        {
            IsOpen = true;
            _resumePending = false;
            SkylineDeviceInput.Paused = true;
            _prevScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _overlay.style.display = DisplayStyle.Flex;
            _evPressed = _evReleased = false;
            _navPrev = Vector2Int.zero;
            _drag = null;
            _lastPtr = new Vector2(-9999f, -9999f);
            _col = 0;
            ShowPage(Page.Main);
            Debug.Log("SKYLINE_PAUSE open");
        }

        /// <summary>Closes the menu: the game runs again. The input stays asleep until the end of this frame.</summary>
        void Close()
        {
            if (!IsOpen) return;
            CommitOptions();
            IsOpen = false;
            Time.timeScale = _prevScale;
            AudioListener.pause = false;
            _capturing = false;
            _drag = null;
            _overlay.style.display = DisplayStyle.None;
            if (!Application.isBatchMode && (_player == null || _player.ScriptedInput == null))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            _resumePending = true;
            Debug.Log("SKYLINE_PAUSE close");
        }

        /// <summary>The menu is going away while open (the scene unloads): undo everything it holds.</summary>
        void ForceClose()
        {
            IsOpen = false;
            SkylineDeviceInput.Paused = false;
            if (Time.timeScale == 0f) Time.timeScale = _prevScale;
            AudioListener.pause = false;
        }

        void CommitOptions()
        {
            if (!_dirtyOptions) return;
            _dirtyOptions = false;
            _lastCommit = Time.unscaledTime;
            SkylineSettings.Commit();
        }

        // ------------------------------------------------------------------ navigation

        void UpdateNavigation(float now)
        {
            var pad = Gamepad.current;
            var kb = Keyboard.current;
            bool confirm = false, back = false;
            Vector2Int dir = Vector2Int.zero;
            if (pad != null)
            {
                confirm |= pad.buttonSouth.wasPressedThisFrame;
                back |= pad.buttonEast.wasPressedThisFrame;
                var ls = pad.leftStick.ReadValue();
                if (pad.dpad.up.isPressed || ls.y > StickPress) dir.y = -1;           // up the list is a lower index
                else if (pad.dpad.down.isPressed || ls.y < -StickPress) dir.y = 1;
                else if (pad.dpad.left.isPressed || ls.x < -StickPress) dir.x = -1;
                else if (pad.dpad.right.isPressed || ls.x > StickPress) dir.x = 1;
            }
            if (kb != null)
            {
                confirm |= kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
                back |= kb.backspaceKey.wasPressedThisFrame;
                if (dir == Vector2Int.zero)
                {
                    if (kb.upArrowKey.isPressed) dir.y = -1;
                    else if (kb.downArrowKey.isPressed) dir.y = 1;
                    else if (kb.leftArrowKey.isPressed) dir.x = -1;
                    else if (kb.rightArrowKey.isPressed) dir.x = 1;
                }
            }

            // A direction acts on the frame it begins, then repeats while held.
            bool fire = false;
            if (dir != Vector2Int.zero)
            {
                if (dir != _navPrev) { fire = true; _navNext = now + RepeatFirst; }
                else if (now >= _navNext) { fire = true; _navNext = now + RepeatNext; }
            }
            _navPrev = dir;
            if (fire) Move(dir);
            if (confirm) Activate();
            if (back)
            {
                Tick(false);
                if (_page == Page.Main) Close(); else ShowPage(Page.Main);
            }
        }

        void Move(Vector2Int dir)
        {
            if (_items.Count == 0) return;
            if (dir.y != 0)
            {
                _sel = (_sel + dir.y + _items.Count) % _items.Count;
                Tick(false);
            }
            else if (dir.x != 0)
            {
                var it = _items[_sel];
                if (it.kind == Kind.Slider) { Nudge(it, dir.x); Tick(false); }
                else if (it.kind == Kind.Toggle) { it.setBool(!it.getBool()); MarkOptions(); Tick(true); }
                else if (it.kind == Kind.Choice) { Cycle(it, dir.x); Tick(true); }
                else if (it.kind == Kind.Binding) { _col = dir.x < 0 ? 0 : 1; Tick(false); }
            }
            _dirtyUi = true;
        }

        void Nudge(Item it, int dir)
        {
            float v = Mathf.Clamp(it.get() + dir * it.step, it.min, it.max);
            v = Mathf.Round(v / it.step) * it.step;
            it.set(Mathf.Clamp(v, it.min, it.max));
            MarkOptions();
        }

        void MarkOptions()
        {
            _dirtyOptions = true;
            _dirtyUi = true;
        }

        void Activate()
        {
            if (_sel < 0 || _sel >= _items.Count) return;
            var it = _items[_sel];
            switch (it.kind)
            {
                case Kind.Button:
                    Tick(true);
                    it.activate();
                    break;
                case Kind.Toggle:
                    it.setBool(!it.getBool());
                    MarkOptions();
                    Tick(true);
                    break;
                case Kind.Choice:
                    Cycle(it, 1);
                    Tick(true);
                    break;
                case Kind.Binding:
                    BeginCapture(it, _col == 0 ? BindDevice.KeyboardMouse : BindDevice.Pad);
                    break;
            }
            _dirtyUi = true;
        }

        // ------------------------------------------------------------------ the mouse

        void UpdatePointer()
        {
            Vector2 pos;
            bool pressed, held, released;
            if (_evLive)
            {
                pos = _evPos;
                pressed = _evPressed;
                held = _evHeld;
                released = _evReleased;
            }
            else
            {
                var m = Mouse.current;
                if (m == null || _overlay.panel == null) return;
                Vector2 mp = m.position.ReadValue();
                pos = RuntimePanelUtils.ScreenToPanel(_overlay.panel, new Vector2(mp.x, Screen.height - mp.y));
                pressed = m.leftButton.wasPressedThisFrame;
                held = m.leftButton.isPressed;
                released = m.leftButton.wasReleasedThisFrame;
            }
            _evPressed = _evReleased = false;

            bool moved = (pos - _lastPtr).sqrMagnitude > 4f;
            if (moved)
            {
                _lastPtr = pos;
                int hover = HitRow(pos);
                if (hover >= 0 && hover != _sel) { _sel = hover; _dirtyUi = true; }
            }
            if (pressed)
            {
                int row = HitRow(pos);
                if (row >= 0)
                {
                    _sel = row;
                    var it = _items[row];
                    if (it.kind == Kind.Slider)
                    {
                        _drag = it;
                        SetFromPointer(it, pos.x);
                    }
                    else if (it.kind == Kind.Binding)
                    {
                        for (int c = 0; c < 2; c++) if (it.cells[c].worldBound.Contains(pos)) _col = c;
                        if (it.cells[_col].worldBound.Contains(pos)) Activate();
                    }
                    else Activate();
                    _dirtyUi = true;
                }
            }
            if (_drag != null)
            {
                if (held) SetFromPointer(_drag, pos.x);
                if (released || !held) _drag = null;
            }
        }

        int HitRow(Vector2 pos)
        {
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].el.worldBound.Contains(pos)) return i;
            return -1;
        }

        void SetFromPointer(Item it, float x)
        {
            var b = it.track.worldBound;
            if (b.width < 1f) return;
            float t = Mathf.Clamp01((x - b.xMin) / b.width);
            float v = it.min + t * (it.max - it.min);
            v = Mathf.Round(v / it.step) * it.step;
            float old = it.get();
            it.set(Mathf.Clamp(v, it.min, it.max));
            if (!Mathf.Approximately(old, it.get())) { MarkOptions(); Tick(false); }
        }

        // ------------------------------------------------------------------ rebinding

        void BeginCapture(Item it, BindDevice device)
        {
            if (device == BindDevice.Pad && Gamepad.current == null)
            {
                _note.text = "No controller found: plug one in or press a button on it first.";
                return;
            }
            _capturing = true;
            _captureDevice = device;
            _captureAction = it.action;
            _captureFrame = Time.frameCount;
            _note.text = device == BindDevice.Pad
                ? "Press the controller button for " + ActionName(it.action) + ". Start cancels."
                : "Press the key or mouse button for " + ActionName(it.action) + ". Esc cancels.";
            Tick(true);
        }

        void UpdateCapture()
        {
            if (Time.frameCount == _captureFrame) return;   // the press that started the capture is not the new binding
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool cancel = (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame);
            if (cancel)
            {
                _capturing = false;
                _note.text = "Cancelled.";
                _dirtyUi = true;
                Tick(false);
                return;
            }
            if (!SkylineBindings.TryCapture(_captureDevice, out var path)) return;

            var table = SkylineBindings.Table;
            var before = new Dictionary<BindAction, string>();
            foreach (var a in BindingTable.AllActions) before[a] = table.Get(_captureDevice, a);
            var result = SkylineBindings.Rebind(_captureDevice, _captureAction, path);
            _capturing = false;
            if (result == BindResult.Rejected) _note.text = "That button can't be used here.";
            else
            {
                string msg = ActionName(_captureAction) + " is now " + BindingNames.Display(path) + ".";
                if (result == BindResult.Swapped)
                    foreach (var a in BindingTable.AllActions)
                        if (a != _captureAction && before[a] != table.Get(_captureDevice, a))
                            msg += " " + ActionName(a) + " took " + BindingNames.Display(table.Get(_captureDevice, a)) + " in exchange.";
                _note.text = msg;
                Tick(true);
            }
            _dirtyUi = true;
        }

        // ------------------------------------------------------------------ the click

        void BuildSounds()
        {
            _tick = MakeClip("SkylineMenuTick", 1500f, 0.035f, 0.5f);
            _confirm = MakeClip("SkylineMenuConfirm", 950f, 0.07f, 0.6f);
            _click = gameObject.AddComponent<AudioSource>();
            _click.playOnAwake = false;
            _click.spatialBlend = 0f;
            _click.ignoreListenerPause = true;   // the one sound that plays while the listener is paused
            _click.volume = 0.35f;
        }

        static AudioClip MakeClip(string name, float hz, float seconds, float amp)
        {
            const int rate = 44100;
            int n = Mathf.Max(1, (int)(rate * seconds));
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float attack = Mathf.Clamp01(i / 48f);
                float env = Mathf.Exp(-t * (3.2f / seconds));
                data[i] = Mathf.Sin(2f * Mathf.PI * hz * t) * env * attack * amp;
            }
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void Tick(bool confirm)
        {
            if (_click == null) return;
            _click.PlayOneShot(confirm ? _confirm : _tick);
        }
    }
}

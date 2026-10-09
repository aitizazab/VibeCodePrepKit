using System;
using System.Collections.Generic;
using Ink.Runtime;
using JamKit.Dialogue;
using JamKit.Flow;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace JamKit.Narrative
{
    /// <summary>
    /// The story template's scene behaviour. It plays the config's ink story through the shell's
    /// DialogueService, as its presenter: tags drive the stage (backgrounds, characters, CGs,
    /// effects, sound), lines go to the text box or full-screen text and into the backlog, the
    /// cross-playthrough memory and endings are recorded, hotspot scenes open when ink asks, and
    /// saves capture all of it. Split into StoryGame.cs (set-up, lines, tags, ending), .Saves.cs,
    /// .Hotspots.cs, .Memory.cs (ink functions, meta tricks) and .Debug.cs (debug menu, smoke run).
    /// </summary>
    public sealed partial class StoryGame : MonoBehaviour, IDialoguePresenter, IStoryFacts
    {
        public StoryConfig config;
        public StyleSheet style;

        static StoryGame _current;
        static string _pendingLoad;

        /// <summary>The running story scene, or null.</summary>
        public static StoryGame Current => _current;

        /// <summary>A slot to load when the story scene starts (set by Load on the title screen).</summary>
        public static string PendingLoad
        {
            get => _pendingLoad;
            set => _pendingLoad = value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _current = null;
            _pendingLoad = null;
        }

        StoryStage _stage;
        StoryTextView _text;
        HotspotLayer _hotspots;
        InventoryBar _inventory;
        VisualElement _endingCard;
        Label _endingNumber, _endingTitle, _endingText, _endingNew;
        readonly StageState _state = new StageState();
        readonly Backlog _backlog = new Backlog(300);
        string _chapter = "", _knot = "", _pendingEnding = "";
        float _playSeconds, _endingLeft;
        int _linesSinceAutosave, _unhideFrame = -1;
        bool _started, _ended, _endingShown, _currentRead;
        string _endedWith = "";

        public StoryStage Stage => _stage;
        public StoryTextView Text => _text;
        public HotspotLayer Hotspots => _hotspots;
        public InventoryBar Inventory => _inventory;
        public StageState State => _state;
        public Backlog History => _backlog;
        public StoryMemory Memory => StoryStorage.Memory;
        public string Chapter => _chapter;
        public string Knot => _knot;
        public bool Ended => _ended;
        public bool EndingCardShown => _endingShown;
        /// <summary>The ending this playthrough reached ("" until then).</summary>
        public string EndedWith => _endedWith;
        public float PlaySeconds => _playSeconds;

        // ---------- Set-up ----------

        void Start()
        {
            if (config == null) config = StoryConfig.Load();
            if (!GameRoot.Exists || config == null || config.story == null || !config.story.isCompiled)
            {
                Debug.LogError("StoryGame: no compiled story in the StoryConfig. Check the ink file for errors, then JamKit > Rebuild Shell Scenes.");
                enabled = false;
                return;
            }
            if (style == null) style = config.style;
            _current = this;
            BuildUi();

            var d = GameRoot.Dialogue;
            d.UseView(_text);
            d.Presenter = this;
            d.InlineSpeakers = config.inlineSpeakers ? (Func<string, bool>)IsSpeaker : null;
            d.Palette = config.PaletteHex;
            _savedCharsPerSecond = d.charsPerSecond;
            _savedTypeSound = d.typeSound;
            d.charsPerSecond = config.charsPerSecond;
            d.StoryCreated += OnStoryCreated;
            d.LineShown += OnLineShown;
            d.ChoiceMade += OnChoiceMade;
            d.Ended += OnDialogueEnded;
            d.Parked += OnParked;

            GameRoot.Hud.Show();
            GameRoot.Hud.ShowTimer(false);
            StoryScreens.Ensure(config);
            AddPauseButtons(true);
            SetupDebug();
            SetupPresenter();
            _started = true;

            string load = _pendingLoad;
            _pendingLoad = null;
            if (!string.IsNullOrEmpty(load) && StoryStorage.Exists(load)) LoadNow(load);
            else NewGame();
        }

        bool _ctrlSkip;
        float _savedCharsPerSecond = 45f;
        string _savedTypeSound = "blip";

        void BuildUi()
        {
            var parent = GameRoot.Hud.Root;
            _stage = new StoryStage(parent, config, style);
            _hotspots = new HotspotLayer(_stage.UiLayer);
            if (config.TryPalette("accent", out var accent)) _hotspots.SetAccent(accent);
            _hotspots.Activated = OnHotspotActivated;
            _hotspots.RightClicked = () => _inventory.SetHeld("");
            _inventory = new InventoryBar(_stage.UiLayer, config);
            _inventory.HeldChanged = held =>
            {
                var def = config.FindItem(held);
                _hotspots.SetHeldItem(def != null ? def.displayName : held);
            };
            _text = new StoryTextView(_stage.UiLayer, config);
            if (config.quickMenu)
            {
                _text.AddQuickButton("log", "Log", OpenBacklog);
                _text.AddQuickButton("auto", "Auto", ToggleAuto);
                _text.AddQuickButton("skip", "Skip", ToggleSkip);
                _text.AddQuickButton("save", "Save", StoryScreens.OpenSave);
                _text.AddQuickButton("load", "Load", StoryScreens.OpenLoad);
                _text.AddQuickButton("qsave", "Q.Save", QuickSave);
                _text.AddQuickButton("qload", "Q.Load", QuickLoad);
                _text.AddQuickButton("menu", "Menu", () => GameRoot.Instance.Pause());
            }
            BuildEndingCard();
        }

        void BuildEndingCard()
        {
            _endingCard = new VisualElement { name = "ending-card" };
            _endingCard.AddToClassList("st-ending-card");
            _endingNumber = StoryScreens.L("", "st-ending-card-number", _endingCard);
            _endingTitle = StoryScreens.L("", "st-ending-card-title", _endingCard);
            _endingText = StoryScreens.L("", "st-ending-card-text", _endingCard);
            _endingNew = StoryScreens.L("", "st-ending-card-new", _endingCard);
            StoryScreens.L("Click or press Enter to go on", "st-ending-card-hint", _endingCard);
            _endingCard.style.display = DisplayStyle.None;
            _stage.Frame.Add(_endingCard);
        }

        void OnDestroy()
        {
            if (_current == this) _current = null;
            if (!_started || !GameRoot.Exists) return;
            // Leaving mid-story (quit to title, restart, closing the game): keep an autosave and remember it.
            if (!_ended)
            {
                if (_last != null) StoryStorage.WriteSlot(StorySlots.Auto, _last);
                Memory.NoteQuit();
            }
            StoryStorage.SaveMemory();

            var d = GameRoot.Dialogue;
            d.StoryCreated -= OnStoryCreated;
            d.LineShown -= OnLineShown;
            d.ChoiceMade -= OnChoiceMade;
            d.Ended -= OnDialogueEnded;
            d.Parked -= OnParked;
            if (d.IsActive || d.IsParked) d.Stop();
            if (ReferenceEquals(d.Presenter, this)) d.Presenter = null;
            d.UseView(null);
            d.InlineSpeakers = null;
            d.Palette = null;
            d.AutoMode = false;
            d.SkipMode = false;
            d.charsPerSecond = _savedCharsPerSecond;
            d.typeSound = _savedTypeSound;
            GameRoot.Audio.StopAmbience(0.5f);
            AddPauseButtons(false);
            TeardownDebug();
            TeardownPresenter();
            _stage?.Remove();
        }

        void OnApplicationQuit()
        {
            if (!_started || _ended || !GameRoot.Exists) return;
            if (_last != null) StoryStorage.WriteSlot(StorySlots.Auto, _last);
            StoryStorage.SaveMemory();
        }

        void AddPauseButtons(bool on)
        {
            var pause = GameRoot.Ui.Pause;
            if (on)
            {
                pause.AddButton("story-save", "Save", StoryScreens.OpenSave, "settings");
                pause.AddButton("story-load", "Load", StoryScreens.OpenLoad, "settings");
                pause.AddButton("story-log", "Story so far", OpenBacklog, "settings");
            }
            pause.SetButtonShown("story-save", on);
            pause.SetButtonShown("story-load", on);
            pause.SetButtonShown("story-log", on);
        }

        // ---------- Starting ----------

        /// <summary>A new playthrough from the start.</summary>
        public void NewGame()
        {
            var d = GameRoot.Dialogue;
            if (d.IsActive || d.IsParked) d.Stop();
            Memory.BeginPlaythrough();
            StoryStorage.SaveMemory();
            _state.Reset();
            _backlog.Clear();
            _text.SetMode(false);
            _text.ClearNvl();
            _stage.ShowState(_state);
            _chapter = "";
            _knot = "";
            _pendingEnding = "";
            _endedWith = "";
            _investigation = null;
            _usedHotspots.Clear();
            _lastItems.Clear();
            _hotspots.Hide();
            _inventory.SetVisible(false);
            _inventory.SetHeld("");
            _playSeconds = 0f;
            _ended = false;
            _endingShown = false;
            _endingCard.style.display = DisplayStyle.None;
            _last = null;
            GameRoot.Audio.StopMusic(0.3f);
            GameRoot.Audio.StopAmbience(0.3f);
            d.Play(config.story.storyJson, string.IsNullOrEmpty(config.startKnot) ? null : config.startKnot);
        }

        // ---------- Per frame ----------

        void Update()
        {
            if (!GameRoot.Exists || _stage == null) return;
            var flow = GameRoot.Flow;
            float dt = Time.unscaledDeltaTime;
            bool paused = flow.Current == GameState.Paused || GameRoot.IsLoadingScene;
            if (!paused)
            {
                _stage.Tick(Mathf.Min(dt, 0.1f));
                _hotspots.Tick(dt);
                if (flow.InRun && !_ended) _playSeconds += dt;
            }
            _text.SetScramble(_stage.GlitchStrength);
            _text.SetQuickActive("auto", GameRoot.Dialogue.AutoMode);
            _text.SetQuickActive("skip", GameRoot.Dialogue.SkipMode);
            if (_endingShown) TickEndingCard(dt);
            // Hold-to-skip ends when Ctrl is let go, even if a menu opened meanwhile; Ctrl+Shift is the
            // start of the presenter chord, not a skip.
            if (_ctrlSkip)
            {
                var keys = Keyboard.current;
                if (keys == null || !keys.ctrlKey.isPressed || PresenterMode.ChordKeysDown)
                {
                    _ctrlSkip = false;
                    GameRoot.Dialogue.SkipMode = false;
                }
            }
            if (!paused && GameRoot.Ui.Top == null && !PresenterMode.ChordKeysDown) HandleInput();
        }

        void HandleInput()
        {
            var input = GameRoot.Input;
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            var mouse = Mouse.current;
            var d = GameRoot.Dialogue;

            if (_endingShown)
            {
                if (input.Submit.WasPressedThisFrame() || input.Interact.WasPressedThisFrame() || input.Fire.WasPressedThisFrame()) FinishEndingCard();
                return;
            }
            if (_stage.Frozen) return;

            bool any = input.Submit.WasPressedThisFrame() || input.Interact.WasPressedThisFrame() || input.Fire.WasPressedThisFrame();
            if (_text.HiddenByPlayer)
            {
                if (any || (mouse != null && mouse.rightButton.wasPressedThisFrame)) { _text.SetHiddenByPlayer(false); _unhideFrame = Time.frameCount; }
                return;
            }

            if (d.IsParked && _hotspots.Active)
            {
                HandleHotspotInput(kb, pad);
                return;
            }
            if (!d.IsActive) return;

            // Choices by number.
            if (kb != null && d.ChoiceCount > 0)
                for (int i = 0; i < 9 && i < d.ChoiceCount; i++)
                    if (kb[Key.Digit1 + i].wasPressedThisFrame || kb[Key.Numpad1 + i].wasPressedThisFrame) { d.Choose(i); return; }

            bool wheelUp = mouse != null && mouse.scroll.ReadValue().y > 0.1f;
            if ((kb != null && kb.lKey.wasPressedThisFrame) || wheelUp || (pad != null && pad.buttonNorth.wasPressedThisFrame)) { OpenBacklog(); return; }
            if ((kb != null && kb.aKey.wasPressedThisFrame) || (pad != null && pad.leftShoulder.wasPressedThisFrame)) ToggleAuto();
            if ((kb != null && kb.sKey.wasPressedThisFrame) || (pad != null && pad.rightShoulder.wasPressedThisFrame)) ToggleSkip();
            if ((kb != null && kb.hKey.wasPressedThisFrame) || (mouse != null && mouse.rightButton.wasPressedThisFrame) || (pad != null && pad.selectButton.wasPressedThisFrame))
                _text.SetHiddenByPlayer(true);
            if (kb != null && kb.f5Key.wasPressedThisFrame) QuickSave();
            if (kb != null && kb.f9Key.wasPressedThisFrame) QuickLoad();
            // Hold Ctrl to skip (read text only, unless Settings says otherwise).
            if (kb != null && kb.leftCtrlKey.wasPressedThisFrame) { d.SkipMode = true; _ctrlSkip = true; }
        }

        public void ToggleAuto() => GameRoot.Dialogue.AutoMode = !GameRoot.Dialogue.AutoMode;

        public void ToggleSkip() => GameRoot.Dialogue.SkipMode = !GameRoot.Dialogue.SkipMode;

        public void OpenBacklog() => StoryScreens.OpenBacklog(_backlog);

        bool IsSpeaker(string name) => config.FindCharacter(name) != null;

        // ---------- IDialoguePresenter ----------

        public bool IsBusy => _stage != null && _stage.Busy;

        public void Hurry() => _stage?.Hurry();

        public bool AllowAdvanceInput() =>
            !_text.PointerOverMenu && !_text.HiddenByPlayer && !_stage.PopupOpen && !_stage.Frozen && Time.frameCount != _unhideFrame && !_endingShown;

        public bool IsRead(DialogueLineInfo line) => _currentRead;

        public void OnLine(ref DialogueLineInfo line)
        {
            var def = config.FindCharacter(line.speakerId);
            if (def != null)
            {
                line.speaker = def.displayName;
                line.speakerId = def.id;
                GameRoot.Dialogue.typeSound = string.IsNullOrEmpty(def.voice) ? _savedTypeSound : def.voice;
            }
            else GameRoot.Dialogue.typeSound = _savedTypeSound;
            _stage.SetSpeaker(line.kind == DialogueLineKind.Dialogue ? line.speakerId : "");
        }

        public bool OnOutOfContent()
        {
            if (_investigation == null) return false;
            return true;
        }

        public bool HandleTag(DialogueTag tag)
        {
            var cmd = StoryCommand.Parse(tag);
            switch (cmd.kind)
            {
                case StoryCommandKind.Background:
                case StoryCommandKind.Scene:
                case StoryCommandKind.Show:
                case StoryCommandKind.Hide:
                case StoryCommandKind.HideAll:
                case StoryCommandKind.Move:
                case StoryCommandKind.Cg:
                case StoryCommandKind.CgOff:
                case StoryCommandKind.Fade:
                case StoryCommandKind.Zoom:
                case StoryCommandKind.Blur:
                case StoryCommandKind.Filter:
                    _state.Apply(cmd);
                    _stage.Play(cmd, _state, GameRoot.Dialogue.SkipMode);
                    return true;
                case StoryCommandKind.Shake:
                case StoryCommandKind.Flash:
                    if (!GameRoot.Dialogue.SkipMode) _stage.Play(cmd, _state, false);
                    return true;
                case StoryCommandKind.Glitch:
                    if (!GameRoot.Dialogue.SkipMode)
                    {
                        _stage.Play(cmd, _state, false);
                        GameRoot.Audio.Play("glitch");
                    }
                    return true;
                case StoryCommandKind.Music:
                    _state.Apply(cmd);
                    GameRoot.Audio.PlayMusic(cmd.id, cmd.SecondsOr(1.5f));
                    return true;
                case StoryCommandKind.StopMusic:
                    _state.Apply(cmd);
                    GameRoot.Audio.StopMusic(cmd.SecondsOr(1.5f));
                    return true;
                case StoryCommandKind.Ambience:
                    _state.Apply(cmd);
                    GameRoot.Audio.PlayAmbience(cmd.id, cmd.SecondsOr(1.5f));
                    return true;
                case StoryCommandKind.StopAmbience:
                    _state.Apply(cmd);
                    GameRoot.Audio.StopAmbience(cmd.SecondsOr(1.5f));
                    return true;
                case StoryCommandKind.Sfx:
                    if (!GameRoot.Dialogue.SkipMode) GameRoot.Audio.Play(cmd.id);
                    return true;
                case StoryCommandKind.Nvl:
                case StoryCommandKind.Adv:
                    _state.Apply(cmd);
                    _text.SetMode(_state.nvl);
                    return true;
                case StoryCommandKind.Clear:
                    _text.ClearNvl();
                    return true;
                case StoryCommandKind.Chapter:
                    _chapter = cmd.text;
                    if (!GameRoot.Dialogue.SkipMode) _stage.Chapter(cmd.text);
                    return true;
                case StoryCommandKind.Ending:
                    _pendingEnding = cmd.id;
                    return true;
                case StoryCommandKind.Investigate:
                    BeginInvestigation(cmd.id);
                    return true;
                case StoryCommandKind.Leave:
                    EndInvestigation();
                    return true;
                case StoryCommandKind.Meta:
                    Meta(cmd);
                    return true;
                default:
                    return false;
            }
        }

        // ---------- Lines ----------

        void OnLineShown(DialogueLineInfo line)
        {
            string plain = RichLine.StripTags(line.text);
            int hash = StoryMemory.LineHash(line.knot, plain);
            _currentRead = Memory.IsRead(hash);
            Memory.MarkRead(hash);
            _backlog.Add(line.kind == DialogueLineKind.Narration ? "" : line.speaker, RichLine.Parse(line.text, config.PaletteHex).Markup,
                line.kind == DialogueLineKind.Thought ? LineKind.Thought : line.kind == DialogueLineKind.Dialogue ? LineKind.Dialogue : LineKind.Narration);
            bool newKnot = false;
            if (!string.IsNullOrEmpty(line.knot) && line.knot != _knot)
            {
                _knot = line.knot;
                Memory.Visit(_knot);
                newKnot = true;
            }
            Memory.NoteTurn(GameRoot.Dialogue.TurnIndex);
            Capture();
            if (newKnot || ++_linesSinceAutosave >= 6) Autosave();
        }

        void OnChoiceMade(int index, string text)
        {
            _backlog.AddChoice(RichLine.Parse(text, config.PaletteHex).Markup);
            GameRoot.Dialogue.SkipMode = false;
        }

        // ---------- The end ----------

        void OnDialogueEnded(string startKnot)
        {
            if (_ended) return;
            _ended = true;
            _endedWith = _pendingEnding;
            _hotspots.Hide();
            _inventory.SetVisible(false);
            bool first = false;
            if (!string.IsNullOrEmpty(_pendingEnding)) first = Memory.RecordEnding(_pendingEnding);
            else Memory.inProgress = false;
            StoryStorage.SaveMemory();
            StoryStorage.DeleteSlot(StorySlots.Auto);
            _last = null;
            ShowEndingCard(_pendingEnding, first);
        }

        void ShowEndingCard(string id, bool first)
        {
            SmokeNoteEnding(id);
            var infos = config.EndingInfos();
            var def = config.FindEnding(id);
            int number = EndingsRegistry.Number(infos, id);
            _endingNumber.text = number > 0 ? "Ending " + number + " of " + infos.Count : "The End";
            _endingTitle.text = def != null ? def.title : "The End";
            _endingText.text = def != null ? def.description : "";
            int found = EndingsRegistry.Found(infos, Memory);
            _endingNew.text = def == null ? "" : first ? "New ending!   " + found + " / " + infos.Count + " found" : found + " / " + infos.Count + " found";
            _endingCard.style.display = DisplayStyle.Flex;
            _endingCard.BringToFront();
            _endingShown = true;
            _endingLeft = Mathf.Max(1.5f, config.endingCardSeconds);
            GameRoot.Audio.Play("chime");
        }

        void TickEndingCard(float dt)
        {
            _endingLeft -= dt;
            if (_endingLeft <= 0f) FinishEndingCard();
        }

        /// <summary>Closes the ending card and shows the shell's victory screen (then credits, then the title).</summary>
        public void FinishEndingCard()
        {
            if (!_endingShown) return;
            _endingShown = false;
            _endingCard.style.display = DisplayStyle.None;
            var infos = config.EndingInfos();
            var def = config.FindEnding(_endedWith);
            GameRoot.Run.Set("Ending", def != null ? def.title : "The End");
            if (infos.Count > 0) GameRoot.Run.Set("Endings found", EndingsRegistry.Found(infos, Memory) + " / " + infos.Count);
            GameRoot.Win();
        }
    }
}

using System;
using System.Collections.Generic;
using Ink.Runtime;
using Ink.UnityIntegration;
using JamKit.Dialogue;
using JamKit.Flow;
using JamKit.Narrative;
using UnityEngine;

namespace JamKit.Shell
{
    /// <summary>
    /// Runs ink stories in the shell's dialogue box. <c>GameRoot.Dialogue.Play(inkFile, "knot")</c>
    /// enters GameFlow's Dialogue state (game time stops), types each line, shows choices and goes
    /// back to Playing at the end.
    /// <para>Tags: #speaker:Name, #portrait:Name expression left|right|hide, #sfx:id, #music:id
    /// (or stop), #shake:0.4, #flash (or #flash:seconds), #wait:seconds, #auto[:seconds] (the line goes
    /// on by itself), #thought (the line is a thought), #event:name.
    /// External functions (declare them with EXTERNAL in ink): has_flag(name), set_flag(name),
    /// clear_flag(name), backed by the save data.</para>
    /// <para>Lines can carry inline markup: &lt;b&gt;, &lt;i&gt;, &lt;color=red&gt;, and for the typing
    /// &lt;pause=0.5&gt; and &lt;speed=0.5&gt;...&lt;/speed&gt; (see <see cref="RichLine"/>).</para>
    /// <para>Events: <see cref="Ended"/> (with the knot it started at), <see cref="EventRaised"/> for
    /// #event tags, <see cref="TagReceived"/> for every tag, <see cref="StoryCreated"/> (bind your own
    /// ink functions there), <see cref="LineShown"/> and <see cref="ChoiceMade"/>.</para>
    /// <para>Templates can draw the dialogue their own way (<see cref="UseView"/>) and take part in
    /// running it (<see cref="Presenter"/>): the story template uses both, plus <see cref="Resume"/>
    /// (save/load), <see cref="Park"/> (waiting in a hotspot scene), auto and skip modes.</para>
    /// </summary>
    public sealed class DialogueService : MonoBehaviour
    {
        public float charsPerSecond = 45f;
        [Tooltip("Sound id played while text types (every few letters). Empty for silence.")]
        public string typeSound = "blip";

        readonly Typewriter _typewriter = new Typewriter();
        readonly List<string> _choiceTexts = new List<string>();
        readonly HashSet<string> _warned = new HashSet<string>();
        Story _story;
        DialogueView _shellView;
        IDialogueView _view;
        IDialoguePresenter _presenter;
        Func<string, bool> _inlineSpeakers;
        string _startPath = "";
        string _speaker = "";
        string _lastKnot = "";
        bool _active, _parked, _choicesShown, _ownsFlowState, _autoMode, _skipMode;
        bool _lineThought, _waitingForPresenter;
        float _lineAuto;
        float _pendingWait, _inputBlock, _autoClock, _skipClock;
        int _shownChars;
        DialogueLineInfo _line;

        /// <summary>Raised when a story starts, with the knot it starts at ("" = the top).</summary>
        public event Action<string> Started;
        /// <summary>Raised when a story reaches its end, with the knot it started at.</summary>
        public event Action<string> Ended;
        /// <summary>#event:name tags.</summary>
        public event Action<string> EventRaised;
        /// <summary>Every tag, after the shell has handled its own.</summary>
        public event Action<DialogueTag> TagReceived;
        /// <summary>A new ink Story object was made (Play, Resume, hot reload): bind external functions here.</summary>
        public event Action<Story> StoryCreated;
        /// <summary>A line starts on screen.</summary>
        public event Action<DialogueLineInfo> LineShown;
        /// <summary>The player picked a choice (index, text), just before ink goes on.</summary>
        public event Action<int, string> ChoiceMade;
        /// <summary>The story went to wait (<see cref="Park"/>).</summary>
        public event Action Parked;

        public bool IsActive => _active;
        /// <summary>The story is kept but waiting (a hotspot scene is open); <see cref="Unpark"/> goes on.</summary>
        public bool IsParked => _parked;
        public Story Story => _story;
        public string Speaker => _speaker;
        /// <summary>The whole current line as drawn (also while it's still typing).</summary>
        public string CurrentLine => _typewriter.Text;
        /// <summary>The current line: speaker, kind, text as written.</summary>
        public DialogueLineInfo Line => _line;
        public bool IsTyping => _active && !_typewriter.IsComplete;
        public int ChoiceCount => _choicesShown ? _choiceTexts.Count : 0;
        public IReadOnlyList<string> Choices => _choiceTexts;
        /// <summary>The knot the story is in (from ink's position; the last known one while waiting).</summary>
        public string CurrentKnot
        {
            get
            {
                string path = _story != null ? _story.state.currentPathString : null;
                if (!string.IsNullOrEmpty(path))
                {
                    int dot = path.IndexOf('.');
                    string k = dot < 0 ? path : path.Substring(0, dot);
                    if (k.Length > 0 && !char.IsDigit(k[0])) _lastKnot = k;
                }
                return _lastKnot;
            }
        }
        /// <summary>ink's turn index: how many choices were made so far.</summary>
        public int TurnIndex => _story != null ? _story.state.currentTurnIndex : 0;
        public IDialogueView View => _view;

        public IDialoguePresenter Presenter
        {
            get => _presenter;
            set => _presenter = value;
        }

        /// <summary>Names that may start a line ("Iris: Hello."): when set, a known name before ": " becomes the speaker for that line.</summary>
        public Func<string, bool> InlineSpeakers
        {
            get => _inlineSpeakers;
            set => _inlineSpeakers = value;
        }

        /// <summary>Resolves &lt;c=name&gt; colours in lines.</summary>
        public Func<string, string> Palette
        {
            get => _typewriter.palette;
            set => _typewriter.palette = value;
        }

        /// <summary>Lines go on by themselves (Settings.autoDelay after each finishes typing).</summary>
        public bool AutoMode
        {
            get => _autoMode;
            set { _autoMode = value; _autoClock = 0f; if (value) _skipMode = false; }
        }

        /// <summary>Lines fly by until a choice, a line never read (unless Settings.skipUnread) or the end.</summary>
        public bool SkipMode
        {
            get => _skipMode;
            set { _skipMode = value; _skipClock = 0f; if (value) _autoMode = false; }
        }

        public void Init(DialogueView view)
        {
            _shellView = view;
            UseView(null);
        }

        /// <summary>Draw the dialogue somewhere else (null = the shell's own box).</summary>
        public void UseView(IDialogueView view)
        {
            _view = view ?? _shellView;
            _view?.SetChoiceHandler(Choose);
        }

        public bool Play(InkFile file, string knot = null)
        {
            if (file == null || !file.isCompiled)
            {
                Debug.LogError("JamKit: that ink file isn't compiled (check it for ink errors in the Console).");
                return false;
            }
            return Play(file.storyJson, knot);
        }

        public bool Play(TextAsset compiledJson, string knot = null)
        {
            if (compiledJson == null) { Debug.LogError("JamKit: no ink story given."); return false; }
            return Play(compiledJson.text, knot);
        }

        /// <summary>Starts a compiled ink story (JSON), optionally at a knot ("sign" or "sign.stitch").</summary>
        public bool Play(string storyJson, string knot = null)
        {
            if (_active)
            {
                Debug.LogWarning("JamKit: a dialogue is already running; ignoring the new one.");
                return false;
            }
            var story = CreateStory(storyJson);
            if (story == null) return false;
            if (!string.IsNullOrEmpty(knot) && !GoTo(story, knot))
            {
                Debug.LogError($"JamKit: ink has no knot \"{knot}\" (check the spelling; knots are \"=== name ===\").");
                story.onError -= OnInkError;
                return false;
            }
            DropParkedStory();
            _story = story;
            BeginSession(knot ?? "");
            Next();
            return true;
        }

        /// <summary>Continues a story from a saved ink state (story.state.ToJson()). The saved line is
        /// shown again (already typed); with <paramref name="parked"/> the story goes straight back to waiting.</summary>
        public bool Resume(string storyJson, string stateJson, DialogueLineInfo shownLine, bool parked, string startKnot = "")
        {
            if (_active) Finish(false);
            var story = CreateStory(storyJson);
            if (story == null) return false;
            try { story.state.LoadJson(stateJson); }
            catch (Exception e)
            {
                Debug.LogError("JamKit: that save doesn't fit this story any more (" + e.Message + ").");
                story.onError -= OnInkError;
                return false;
            }
            DropParkedStory();
            _story = story;
            BeginSession(startKnot ?? "");
            _speaker = "";
            if (parked)
            {
                Park();
                return true;
            }
            if (!string.IsNullOrEmpty(shownLine.text))
            {
                ShowLine(shownLine, true);
                _typewriter.Complete();
                ShowTyped();
                if (!_story.canContinue && _story.currentChoices.Count > 0) ShowChoices();
                return true;
            }
            Next();
            return true;
        }

        Story CreateStory(string storyJson)
        {
            Story story;
            try
            {
                story = new Story(storyJson);
            }
            catch (Exception e)
            {
                Debug.LogError("JamKit: couldn't load the ink story. " + e.Message);
                return null;
            }
            story.onError += OnInkError;
            story.allowExternalFunctionFallbacks = true;
            BindExternals(story);
            StoryCreated?.Invoke(story);
            return story;
        }

        static bool GoTo(Story story, string path)
        {
            try
            {
                if (story.ContentAtPath(new Ink.Runtime.Path(path)).correctObj == null) return false;
                story.ChoosePathString(path);
                return true;
            }
            catch (Exception) { return false; }
        }

        /// <summary>True if the running (or waiting) story has this knot or path.</summary>
        public bool HasPath(string path)
        {
            if (_story == null || string.IsNullOrEmpty(path)) return false;
            try { return _story.ContentAtPath(new Ink.Runtime.Path(path)).correctObj != null; }
            catch (Exception) { return false; }
        }

        void BeginSession(string startPath)
        {
            _startPath = startPath;
            _active = true;
            _parked = false;
            _speaker = "";
            _pendingWait = 0f;
            _inputBlock = 0.12f;
            _skipMode = false;
            _ownsFlowState = GameRoot.Flow.OpenDialogue();
            _view.Open();
            Started?.Invoke(_startPath);
        }

        void DropParkedStory()
        {
            if (_story != null) _story.onError -= OnInkError;
            _story = null;
            _parked = false;
        }

        /// <summary>True if <paramref name="story"/> has this knot or path (false for null, empty or broken paths).</summary>
        public static bool PathExists(Story story, string path)
        {
            if (story == null || string.IsNullOrEmpty(path)) return false;
            try { return story.ContentAtPath(new Ink.Runtime.Path(path)).correctObj != null; }
            catch (Exception) { return false; }
        }

        /// <summary>
        /// Runs <paramref name="knot"/> of a compiled story without showing anything and returns its first
        /// line of text (as label markup) and the #speaker in effect: for a line on an end screen or a
        /// tooltip, chosen by ink. A missing knot, an ink error or a knot without text returns false,
        /// quietly. The shell's ink functions are bound; <paramref name="bind"/> can add the game's own.
        /// Flags the knot sets (set_flag) are kept.
        /// </summary>
        public bool TryReadLine(string storyJson, string knot, out string speaker, out string text, Action<Story> bind = null)
        {
            speaker = "";
            text = "";
            if (string.IsNullOrEmpty(storyJson) || string.IsNullOrEmpty(knot)) return false;
            try
            {
                var story = new Story(storyJson);
                story.allowExternalFunctionFallbacks = true;
                BindExternals(story);
                bind?.Invoke(story);
                if (!GoTo(story, knot)) return false;
                for (int guard = 0; guard < 200 && story.canContinue; guard++)
                {
                    string line = story.Continue();
                    var tags = story.currentTags;
                    if (tags != null)
                        foreach (var raw in tags)
                            if (DialogueTag.TryParse(raw, out var tag) && tag.key == "speaker") speaker = tag.value;
                    line = line != null ? line.Trim() : "";
                    if (line.Length == 0) continue;
                    text = RichLine.Parse(line, Palette).Markup;
                    return text.Length > 0;
                }
            }
            catch (Exception) { }
            speaker = "";
            text = "";
            return false;
        }

        void BindExternals(Story story)
        {
            var save = GameRoot.Save;
            story.BindExternalFunction("has_flag", new Func<string, object>(name => save.Data.HasFlag(name)), true);
            story.BindExternalFunction("set_flag", new Action<string>(name => save.Data.SetFlag(name)));
            story.BindExternalFunction("clear_flag", new Action<string>(name => save.Data.ClearFlag(name)));
            // Cross-run memory any template can use (the story template adds playthrough_count() and more).
            story.BindExternalFunction("run_count", new Func<object>(() => Math.Max(1, save.Data.runsStarted)), true);
            story.BindExternalFunction("runs_won", new Func<object>(() => save.Data.runsWon), true);
            story.BindExternalFunction("best_floor", new Func<object>(() => save.Data.bestFloor), true);
        }

        void OnInkError(string message, Ink.ErrorType type)
        {
            if (type == Ink.ErrorType.Error) Debug.LogError("ink: " + message);
            else Debug.LogWarning("ink: " + message);
        }

        /// <summary>Press: finish a transition, finish typing, or go to the next line. Ignored while choices are up.</summary>
        public void Advance()
        {
            if (!_active || _choicesShown) return;
            if (_presenter != null && _presenter.IsBusy)
            {
                _presenter.Hurry();
                return;
            }
            if (_waitingForPresenter) { Next(); return; }
            if (!_typewriter.IsComplete)
            {
                _typewriter.Complete();
                ShowTyped();
                return;
            }
            Next();
        }

        public void Choose(int index)
        {
            if (!_active || !_choicesShown || _story == null || index < 0 || index >= _story.currentChoices.Count) return;
            GameRoot.Audio.Play("ui_select");
            ChoiceMade?.Invoke(index, _story.currentChoices[index].text);
            _story.ChooseChoiceIndex(index);
            _inputBlock = 0.15f;
            Next();
        }

        /// <summary>Ends the dialogue now (also a waiting one). <paramref name="raiseEnded"/>: also tell listeners it ended.</summary>
        public void Stop(bool raiseEnded = false)
        {
            if (!_active && !_parked) return;
            Finish(raiseEnded);
        }

        /// <summary>One step of "a player mashing through": finishes transitions and typing, picks the
        /// first choice, advances. For tests, the smoke run and a debug skip.</summary>
        public void AutoStep()
        {
            if (!_active) return;
            if (_presenter != null && _presenter.IsBusy) _presenter.Hurry();
            if (_choicesShown) { Choose(0); return; }
            if (!_typewriter.IsComplete) { _typewriter.Complete(); ShowTyped(); }
            Next();
        }

        /// <summary>Mashes through to the next choice (or until the story waits or ends), without choosing.
        /// Returns true if a choice is on screen.</summary>
        public bool SkipToChoice(int maxLines = 2000)
        {
            for (int i = 0; i < maxLines && _active && !_choicesShown; i++)
            {
                if (_presenter != null && _presenter.IsBusy) _presenter.Hurry();
                if (!_typewriter.IsComplete) { _typewriter.Complete(); ShowTyped(); }
                if (!_story.canContinue && _story.currentChoices.Count > 0) { ShowChoices(); break; }
                Next();
            }
            return _active && _choicesShown;
        }

        /// <summary>The story waits (a hotspot scene is open): the box closes, the game is Playing again,
        /// and <see cref="Unpark"/> runs a knot of the same story.</summary>
        public void Park()
        {
            if (_story == null) return;
            _active = false;
            _parked = true;
            _choicesShown = false;
            _skipMode = false;
            _choiceTexts.Clear();
            _view.HideChoices();
            _view.Close();
            if (_ownsFlowState && GameRoot.Flow.Current == GameState.Dialogue) GameRoot.Flow.CloseDialogue();
            _ownsFlowState = false;
            GameRoot.Save.Save();
            Parked?.Invoke();
        }

        /// <summary>Goes on from waiting at a knot ("office_desk" or "knot.stitch").</summary>
        public bool Unpark(string path)
        {
            if (!_parked || _story == null) return false;
            if (!HasPath(path))
            {
                Debug.LogError($"JamKit: ink has no knot \"{path}\".");
                return false;
            }
            _parked = false;
            _active = true;
            _pendingWait = 0f;
            _inputBlock = 0.12f;
            _ownsFlowState = GameRoot.Flow.OpenDialogue();
            _view.Open();
            _story.ChoosePathString(path);
            Next();
            return true;
        }

        /// <summary>Debug: go to a knot now (running or waiting).</summary>
        public bool JumpTo(string path)
        {
            if (_parked) return Unpark(path);
            if (!_active || _story == null || !HasPath(path)) return false;
            _story.ChoosePathString(path);
            _skipMode = false;
            Next();
            return true;
        }

        /// <summary>ink's state as JSON (variables, visits, position, choices on screen).</summary>
        public string SaveState() => _story != null ? _story.state.ToJson() : "";

        /// <summary>Swaps in a recompiled story (edited ink in play mode), keeping the variables, and
        /// restarts <paramref name="restartPath"/> (normally the current knot) so the edit shows at once.</summary>
        public bool HotSwap(string storyJson, string restartPath)
        {
            if (_story == null) return false;
            string state = _story.state.ToJson();
            var fresh = CreateStory(storyJson);
            if (fresh == null) return false;
            try { fresh.state.LoadJson(state); }
            catch (Exception) { /* the layout changed too much: start clean at the knot */ }
            _story.onError -= OnInkError;
            _story = fresh;
            if (_parked) return true;
            if (!string.IsNullOrEmpty(restartPath) && HasPath(restartPath)) _story.ChoosePathString(restartPath);
            Next();
            return true;
        }

        void Next()
        {
            _waitingForPresenter = false;
            _choicesShown = false;
            _choiceTexts.Clear();
            _view.HideChoices();
            while (_story.canContinue)
            {
                string line;
                try { line = _story.Continue(); }
                catch (Exception e)
                {
                    Debug.LogError("JamKit: ink error: " + e.Message);
                    Finish(true);
                    return;
                }
                HandleTags(_story.currentTags);
                if (!_active || _story == null) return; // a tag handler stopped the dialogue
                line = line.Trim();
                if (line.Length == 0)
                {
                    // A line of tags only: if they started something that takes time (a fade, a
                    // chapter card), what follows waits for it, so effects on their own lines play in order.
                    if (_presenter != null && _presenter.IsBusy)
                    {
                        _waitingForPresenter = true;
                        return;
                    }
                    continue;
                }
                ShowLine(MakeLine(line), false);
                return;
            }
            if (_story.currentChoices.Count > 0)
            {
                // Choices with no line before them: keep the last line on screen.
                _typewriter.Complete();
                ShowChoices();
                return;
            }
            if (_presenter != null && _presenter.OnOutOfContent())
            {
                if (_active) Park();
                return;
            }
            Finish(true);
        }

        DialogueLineInfo MakeLine(string text)
        {
            var info = new DialogueLineInfo { speaker = _speaker, speakerId = _speaker, text = text, knot = CurrentKnot, autoSeconds = _lineAuto };
            if (_inlineSpeakers != null && StoryLine.TrySplitSpeaker(text, _inlineSpeakers, out string who, out string rest))
            {
                info.speaker = who;
                info.speakerId = who;
                info.text = rest;
            }
            info.kind = _lineThought ? DialogueLineKind.Thought : string.IsNullOrEmpty(info.speaker) ? DialogueLineKind.Narration : DialogueLineKind.Dialogue;
            _lineThought = false;
            _lineAuto = 0f;
            return info;
        }

        void ShowLine(DialogueLineInfo info, bool restoring)
        {
            _presenter?.OnLine(ref info);
            _line = info;
            _view.SetSpeaker(info.speaker);
            ApplyTextSpeed();
            var rich = RichLine.Parse(info.text, _typewriter.palette);
            _typewriter.BeginParsed(rich);
            if (_pendingWait > 0f) _typewriter.Delay(_pendingWait);
            _pendingWait = 0f;
            _autoClock = 0f;
            _shownChars = -1;
            _view.BeginLine(info, rich);
            ShowTyped();
            LineShown?.Invoke(info);
        }

        void ApplyTextSpeed()
        {
            float speed = GameRoot.Exists ? GameRoot.Settings.textSpeed : 1f;
            _typewriter.charsPerSecond = speed >= Settings.InstantTextSpeed ? 0f : charsPerSecond * Mathf.Max(0.05f, speed);
        }

        void ShowChoices()
        {
            _choicesShown = true;
            _skipMode = false;
            _choiceTexts.Clear();
            foreach (var c in _story.currentChoices) _choiceTexts.Add(c.text);
            _view.ShowChoices(_choiceTexts);
        }

        void ShowTyped()
        {
            int n = _typewriter.Visible;
            if (n == _shownChars) return;
            _shownChars = n;
            _view.SetTyped(_typewriter.Line, n);
        }

        void Finish(bool raiseEnded)
        {
            _active = false;
            _parked = false;
            _choicesShown = false;
            _skipMode = false;
            _view.Close();
            if (_ownsFlowState && GameRoot.Flow.Current == GameState.Dialogue) GameRoot.Flow.CloseDialogue();
            _ownsFlowState = false;
            if (_story != null) _story.onError -= OnInkError;
            _story = null;
            GameRoot.Save.Save();
            if (raiseEnded) Ended?.Invoke(_startPath);
        }

        void HandleTags(List<string> tags)
        {
            if (tags == null) return;
            for (int i = 0; i < tags.Count; i++)
            {
                if (!DialogueTag.TryParse(tags[i], out var tag)) continue;
                if (_presenter != null && _presenter.HandleTag(tag))
                {
                    TagReceived?.Invoke(tag);
                    if (!_active) return;
                    continue;
                }
                switch (tag.key)
                {
                    case "speaker":
                        _speaker = tag.value;
                        break;
                    case "portrait":
                        if (PortraitCommand.TryParse(tag.value, out var cmd))
                        {
                            var portrait = GameRoot.Config.FindPortrait(cmd.character);
                            if (!_view.ApplyPortrait(cmd, portrait, _speaker) && _warned.Add(cmd.character))
                                Debug.LogWarning($"JamKit: no Portrait asset with id \"{cmd.character}\" in GameConfig.portraits.");
                        }
                        break;
                    case "sfx":
                        GameRoot.Audio.Play(tag.value);
                        break;
                    case "music":
                        GameRoot.Audio.PlayMusic(tag.value);
                        break;
                    case "shake":
                        GameRoot.Juice.Shake(tag.FloatOr(0.4f));
                        break;
                    case "flash":
                        GameRoot.Juice.Flash(new Color(1f, 1f, 1f, 0.7f), tag.FloatOr(0.2f));
                        break;
                    case "wait":
                        _pendingWait += Mathf.Max(0f, tag.FloatOr(0.5f));
                        break;
                    case "auto":
                        _lineAuto = Mathf.Max(0.01f, tag.FloatOr(1.5f));
                        break;
                    case "thought":
                    case "think":
                        _lineThought = true;
                        break;
                    case "event":
                        EventRaised?.Invoke(tag.value);
                        break;
                }
                TagReceived?.Invoke(tag);
                if (!_active) return;
            }
        }

        void Update()
        {
            if (!_active) return;
            var flow = GameRoot.Flow;
            if (flow.Current == GameState.Paused || GameRoot.IsLoadingScene) return;
            // Capped so a loading hitch doesn't type half a line in one frame.
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            bool busy = _presenter != null && _presenter.IsBusy;
            if (_waitingForPresenter && !busy)
            {
                Next();
                if (!_active) return;
                busy = _presenter != null && _presenter.IsBusy;
            }
            if (!busy)
            {
                ApplyTextSpeed();
                if (_typewriter.Tick(dt))
                {
                    ShowTyped();
                    if (!string.IsNullOrEmpty(typeSound) && _shownChars % 3 == 1) GameRoot.Audio.Play(typeSound, 0.6f);
                }
            }
            bool complete = _typewriter.IsComplete && !_waitingForPresenter;
            if (complete && !_choicesShown && _story != null && !_story.canContinue && _story.currentChoices.Count > 0)
                ShowChoices();
            _view.SetContinueHint(complete && !_choicesShown && !busy);

            if (_skipMode)
            {
                TickSkip(dt);
                if (!_active) return;
            }
            else if (complete && !_choicesShown && !busy)
            {
                float after = _line.autoSeconds > 0f ? _line.autoSeconds
                    : _autoMode ? GameRoot.Settings.autoDelay + 0.025f * _typewriter.Length : -1f;
                if (after >= 0f)
                {
                    _autoClock += dt;
                    if (_autoClock >= after)
                    {
                        Next();
                        if (!_active) return;
                    }
                }
            }

            if (_inputBlock > 0f) { _inputBlock -= dt; return; }
            if (GameRoot.Ui.Top != null) return;
            if (_presenter != null && !_presenter.AllowAdvanceInput()) return;
            var input = GameRoot.Input;
            if (input.Submit.WasPressedThisFrame() || input.Interact.WasPressedThisFrame() || input.Fire.WasPressedThisFrame())
            {
                if (_skipMode) { _skipMode = false; return; }
                Advance();
            }
        }

        void TickSkip(float dt)
        {
            if (_choicesShown) { _skipMode = false; return; }
            if (_presenter != null && _presenter.IsBusy) _presenter.Hurry();
            bool skipUnread = GameRoot.Settings.skipUnread;
            if (!skipUnread && _presenter != null && !_presenter.IsRead(_line))
            {
                // A line never seen before: stop here so it can be read.
                _skipMode = false;
                return;
            }
            if (!_typewriter.IsComplete) { _typewriter.Complete(); ShowTyped(); }
            _skipClock -= dt;
            if (_skipClock > 0f) return;
            _skipClock = 0.06f;
            Next();
        }
    }
}

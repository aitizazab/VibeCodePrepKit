using System;
using System.Collections.Generic;
using Ink.Runtime;
using Ink.UnityIntegration;
using JamKit.Dialogue;
using JamKit.Flow;
using UnityEngine;

namespace JamKit.Shell
{
    /// <summary>
    /// Runs ink stories in the shell's dialogue box. <c>GameRoot.Dialogue.Play(inkFile, "knot")</c>
    /// enters GameFlow's Dialogue state (game time stops), types each line, shows choices and goes
    /// back to Playing at the end.
    /// <para>Tags: #speaker:Name, #portrait:Name expression left|right|hide, #sfx:id, #music:id
    /// (or stop), #shake:0.4, #flash (or #flash:seconds), #wait:seconds, #event:name.
    /// External functions (declare them with EXTERNAL in ink): has_flag(name), set_flag(name),
    /// clear_flag(name), backed by the save data.</para>
    /// <para>Events: <see cref="Ended"/> (with the knot it started at), <see cref="EventRaised"/> for
    /// #event tags and <see cref="TagReceived"/> for every tag, including your own.</para>
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
        DialogueView _view;
        string _startPath = "";
        string _speaker = "";
        bool _active, _choicesShown, _ownsFlowState;
        float _pendingWait, _inputBlock;
        int _shownChars;

        /// <summary>Raised when a story starts, with the knot it starts at ("" = the top).</summary>
        public event Action<string> Started;
        /// <summary>Raised when a story reaches its end, with the knot it started at.</summary>
        public event Action<string> Ended;
        /// <summary>#event:name tags.</summary>
        public event Action<string> EventRaised;
        /// <summary>Every tag, after the shell has handled its own.</summary>
        public event Action<DialogueTag> TagReceived;

        public bool IsActive => _active;
        public Story Story => _story;
        public string Speaker => _speaker;
        /// <summary>The whole current line (also while it's still typing).</summary>
        public string CurrentLine => _typewriter.Text;
        public bool IsTyping => _active && !_typewriter.IsComplete;
        public int ChoiceCount => _choicesShown ? _choiceTexts.Count : 0;
        public IReadOnlyList<string> Choices => _choiceTexts;

        public void Init(DialogueView view)
        {
            _view = view;
            _view.ChoiceClicked = Choose;
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
            try
            {
                _story = new Story(storyJson);
            }
            catch (Exception e)
            {
                Debug.LogError("JamKit: couldn't load the ink story. " + e.Message);
                return false;
            }
            _story.onError += OnInkError;
            BindExternals(_story);
            if (!string.IsNullOrEmpty(knot))
            {
                bool found;
                try
                {
                    found = _story.ContentAtPath(new Ink.Runtime.Path(knot)).correctObj != null;
                    if (found) _story.ChoosePathString(knot);
                }
                catch (Exception) { found = false; }
                if (!found)
                {
                    Debug.LogError($"JamKit: ink has no knot \"{knot}\" (check the spelling; knots are \"=== name ===\").");
                    _story = null;
                    return false;
                }
            }

            _startPath = knot ?? "";
            _active = true;
            _speaker = "";
            _pendingWait = 0f;
            _inputBlock = 0.12f;
            _ownsFlowState = GameRoot.Flow.OpenDialogue();
            _view.Open();
            Started?.Invoke(_startPath);
            Next();
            return true;
        }

        void BindExternals(Story story)
        {
            var save = GameRoot.Save;
            story.BindExternalFunction("has_flag", new Func<string, object>(name => save.Data.HasFlag(name)), true);
            story.BindExternalFunction("set_flag", new Action<string>(name => save.Data.SetFlag(name)));
            story.BindExternalFunction("clear_flag", new Action<string>(name => save.Data.ClearFlag(name)));
        }

        void OnInkError(string message, Ink.ErrorType type)
        {
            if (type == Ink.ErrorType.Error) Debug.LogError("ink: " + message);
            else Debug.LogWarning("ink: " + message);
        }

        /// <summary>Press: finish typing, or go to the next line. Ignored while choices are up.</summary>
        public void Advance()
        {
            if (!_active || _choicesShown) return;
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
            if (!_active || !_choicesShown || index < 0 || index >= _story.currentChoices.Count) return;
            GameRoot.Audio.Play("ui_select");
            _story.ChooseChoiceIndex(index);
            _inputBlock = 0.15f;
            Next();
        }

        /// <summary>Ends the dialogue now. <paramref name="raiseEnded"/>: also tell listeners it ended.</summary>
        public void Stop(bool raiseEnded = false)
        {
            if (!_active) return;
            Finish(raiseEnded);
        }

        /// <summary>One step of "a player mashing through": finishes typing, picks the first choice, advances.
        /// For tests, the smoke run and a debug skip.</summary>
        public void AutoStep()
        {
            if (!_active) return;
            if (_choicesShown) { Choose(0); return; }
            if (!_typewriter.IsComplete) { _typewriter.Complete(); ShowTyped(); }
            Next();
        }

        void Next()
        {
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
                if (!_active) return; // a tag handler stopped the dialogue
                line = line.Trim();
                if (line.Length == 0) continue;
                ShowLine(line);
                return;
            }
            if (_story.currentChoices.Count > 0)
            {
                // Choices with no line before them: keep the last line on screen.
                _typewriter.Complete();
                ShowChoices();
                return;
            }
            Finish(true);
        }

        void ShowLine(string line)
        {
            _view.SetSpeaker(_speaker);
            _typewriter.charsPerSecond = charsPerSecond;
            _typewriter.Begin(line);
            if (_pendingWait > 0f) _typewriter.Delay(_pendingWait);
            _pendingWait = 0f;
            _shownChars = -1;
            ShowTyped();
        }

        void ShowChoices()
        {
            _choicesShown = true;
            _choiceTexts.Clear();
            foreach (var c in _story.currentChoices) _choiceTexts.Add(c.text);
            _view.ShowChoices(_choiceTexts);
        }

        void ShowTyped()
        {
            int n = _typewriter.Visible;
            if (n == _shownChars) return;
            _shownChars = n;
            string text = _typewriter.Text;
            _view.SetText(n >= text.Length ? text : text.Substring(0, n));
        }

        void Finish(bool raiseEnded)
        {
            _active = false;
            _choicesShown = false;
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
                    case "event":
                        EventRaised?.Invoke(tag.value);
                        break;
                }
                TagReceived?.Invoke(tag);
            }
        }

        void Update()
        {
            if (!_active) return;
            var flow = GameRoot.Flow;
            if (flow.Current == GameState.Paused || GameRoot.IsLoadingScene) return;
            // Capped so a loading hitch doesn't type half a line in one frame.
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (_typewriter.Tick(dt))
            {
                ShowTyped();
                if (!string.IsNullOrEmpty(typeSound) && _shownChars % 3 == 1) GameRoot.Audio.Play(typeSound, 0.6f);
            }
            bool complete = _typewriter.IsComplete;
            if (complete && !_choicesShown && _story != null && !_story.canContinue && _story.currentChoices.Count > 0)
                ShowChoices();
            _view.SetContinueHint(complete && !_choicesShown);

            if (_inputBlock > 0f) { _inputBlock -= dt; return; }
            if (GameRoot.Ui.Top != null) return;
            var input = GameRoot.Input;
            if (input.Submit.WasPressedThisFrame() || input.Interact.WasPressedThisFrame() || input.Fire.WasPressedThisFrame())
                Advance();
        }
    }
}

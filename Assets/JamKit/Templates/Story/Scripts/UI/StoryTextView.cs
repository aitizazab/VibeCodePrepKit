using System;
using System.Collections.Generic;
using System.Text;
using JamKit.Dialogue;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.Narrative
{
    /// <summary>
    /// The story's text: a text box at the bottom (ADV) with a name plate, typed rich text and a small
    /// quick menu, or full-screen text (NVL, #nvl) where lines stack up like a page of a book. Choices
    /// appear as a column of buttons. Thoughts are italic and dimmer, narration has no name plate.
    /// Implements the shell's <see cref="IDialogueView"/>, so DialogueService drives it.
    /// </summary>
    public sealed class StoryTextView : IDialogueView
    {
        public readonly VisualElement Root;
        readonly VisualElement _box, _namePlate, _choices, _quick, _nvl, _nvlLines, _boxNext, _nvlNext;
        readonly Label _name, _text;
        readonly List<Button> _choiceButtons = new List<Button>();
        readonly List<Label> _nvlLabels = new List<Label>();
        readonly List<BacklogEntry> _nvlPage = new List<BacklogEntry>();
        readonly StoryConfig _config;
        readonly System.Random _rng = new System.Random(11);
        Action<int> _onChoice;
        bool _open, _nvlMode, _hasLine, _hiddenByPlayer, _pointerOverMenu;
        int _visibleChoices;
        RichLine _rich = RichLine.Empty;
        int _visible;
        DialogueLineInfo _line;
        string _nvlPrefix = "";
        float _scramble;

        public readonly Dictionary<string, Button> QuickButtons = new Dictionary<string, Button>();

        public bool IsNvl => _nvlMode;
        public bool IsOpen => _open;
        public bool PointerOverMenu => _pointerOverMenu;
        public bool HiddenByPlayer => _hiddenByPlayer;
        public int VisibleChoiceCount => _visibleChoices;
        /// <summary>The text on screen now (markup included), for tests.</summary>
        public string ShownText => _nvlMode ? (_nvlLabels.Count > 0 ? _nvlLabels[_nvlLabels.Count - 1].text : "") : _text.text;
        public string ShownSpeaker => _name.text;
        public bool NamePlateShown => _namePlate.style.display == DisplayStyle.Flex;
        public IReadOnlyList<BacklogEntry> NvlPage => _nvlPage;
        public int NvlLineCount => _nvlLabels.Count;

        public StoryTextView(VisualElement parent, StoryConfig config)
        {
            _config = config;
            Root = El("st-text-root", parent);

            _nvl = El("st-nvl", Root);
            _nvlLines = El("st-nvl-lines", _nvl);
            _nvlNext = El("st-next", _nvl);
            _nvlNext.AddToClassList("st-next--nvl");
            Hide(_nvl);

            _choices = El("st-choices", Root);
            _choices.pickingMode = PickingMode.Ignore;

            _box = El("st-box", Root);
            _box.pickingMode = PickingMode.Position;
            _namePlate = El("st-name", _box);
            _name = new Label { name = "story-speaker" };
            _name.AddToClassList("st-name-label");
            _namePlate.Add(_name);
            _text = new Label { name = "story-text" };
            _text.AddToClassList("st-text");
            _text.enableRichText = true;
            _box.Add(_text);
            _boxNext = El("st-next", _box);
            _quick = El("st-quick", _box);
            _quick.pickingMode = PickingMode.Position;
            _quick.RegisterCallback<PointerEnterEvent>(_ => _pointerOverMenu = true);
            _quick.RegisterCallback<PointerLeaveEvent>(_ => _pointerOverMenu = false);
            _choices.RegisterCallback<PointerEnterEvent>(_ => _pointerOverMenu = true);
            _choices.RegisterCallback<PointerLeaveEvent>(_ => _pointerOverMenu = false);
            Hide(_box);
            if (config != null && !config.quickMenu) Hide(_quick);
        }

        static VisualElement El(string cls, VisualElement parent)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.AddToClassList(cls);
            parent?.Add(e);
            return e;
        }

        static void Hide(VisualElement e) => e.style.display = DisplayStyle.None;
        static void Show(VisualElement e) => e.style.display = DisplayStyle.Flex;

        /// <summary>A small button on the text box (Log, Auto, Skip, Save...).</summary>
        public Button AddQuickButton(string name, string text, Action action)
        {
            var b = new Button(() => { if (GameRoot.Exists) GameRoot.Audio.Play("ui_select"); action(); }) { name = "quick-" + name, text = text };
            b.AddToClassList("st-quick-button");
            b.focusable = false;
            _quick.Add(b);
            QuickButtons[name] = b;
            return b;
        }

        public void SetQuickActive(string name, bool on)
        {
            if (QuickButtons.TryGetValue(name, out var b)) b.EnableInClassList("st-quick-button--on", on);
        }

        // ---------- IDialogueView ----------

        public void SetChoiceHandler(Action<int> onChoice) => _onChoice = onChoice;

        public void Open()
        {
            _open = true;
            _hasLine = false;
            HideChoices();
            Refresh();
        }

        public void Close()
        {
            _open = false;
            HideChoices();
            Refresh();
        }

        public void SetSpeaker(string name)
        {
            _name.text = name ?? "";
        }

        public void BeginLine(DialogueLineInfo line, RichLine rich)
        {
            _line = line;
            _rich = rich ?? RichLine.Empty;
            _visible = 0;
            _hasLine = true;
            var def = _config != null ? _config.FindCharacter(line.speakerId) : null;
            bool named = !string.IsNullOrEmpty(line.speaker) && line.kind != DialogueLineKind.Narration;
            _name.text = named ? line.speaker : "";
            _namePlate.style.display = named && !_nvlMode ? DisplayStyle.Flex : DisplayStyle.None;
            if (def != null) _namePlate.style.backgroundColor = def.nameColor;
            else _namePlate.style.backgroundColor = StyleKeyword.Null;
            _text.EnableInClassList("st-text--thought", line.kind == DialogueLineKind.Thought);
            _text.EnableInClassList("st-text--narration", line.kind == DialogueLineKind.Narration);
            if (_nvlMode) AddNvlLine(line, def);
            Refresh();
        }

        void AddNvlLine(DialogueLineInfo line, CharacterDef def)
        {
            int max = _config != null && _config.nvlMaxLines > 0 ? _config.nvlMaxLines : 8;
            if (_nvlLabels.Count >= max) ClearNvl();
            var l = new Label();
            l.AddToClassList("st-nvl-line");
            l.enableRichText = true;
            if (line.kind == DialogueLineKind.Thought) l.AddToClassList("st-nvl-line--thought");
            _nvlPrefix = "";
            if (!string.IsNullOrEmpty(line.speaker) && line.kind == DialogueLineKind.Dialogue)
            {
                string hex = def != null ? ColorUtility.ToHtmlStringRGB(def.nameColor) : "FFC94A";
                _nvlPrefix = "<color=#" + hex + "><b>" + line.speaker + "</b></color>   ";
            }
            _nvlLines.Add(l);
            _nvlLabels.Add(l);
            _nvlPage.Add(new BacklogEntry { speaker = line.speaker ?? "", text = _rich.Markup, kind = Kind(line.kind) });
        }

        static LineKind Kind(DialogueLineKind k) => k == DialogueLineKind.Thought ? LineKind.Thought : k == DialogueLineKind.Dialogue ? LineKind.Dialogue : LineKind.Narration;

        public void SetTyped(RichLine rich, int visible)
        {
            _rich = rich ?? RichLine.Empty;
            _visible = visible;
            Render();
        }

        void Render()
        {
            string body = _rich.Build(_visible, true);
            if (_scramble > 0f) body = Scrambled(body, _scramble);
            if (_nvlMode)
            {
                if (_nvlLabels.Count > 0) _nvlLabels[_nvlLabels.Count - 1].text = _nvlPrefix + body;
            }
            else _text.text = body;
        }

        /// <summary>Glitch: swaps some letters for symbols while it lasts (0 = clean).</summary>
        public void SetScramble(float strength)
        {
            if (Mathf.Approximately(strength, _scramble) && strength <= 0f) return;
            _scramble = strength;
            if (_hasLine) Render();
        }

        static readonly string Symbols = "#%&@$*!?/\\|<>~^=+01";

        string Scrambled(string markup, float strength)
        {
            var sb = new StringBuilder(markup.Length);
            bool inTag = false;
            foreach (char c in markup)
            {
                if (c == '<') inTag = true;
                if (!inTag && char.IsLetterOrDigit(c) && _rng.NextDouble() < 0.35 * strength)
                {
                    char s = Symbols[_rng.Next(Symbols.Length)];
                    if (s == '<' || s == '>') s = '#';
                    sb.Append(s);
                }
                else sb.Append(c);
                if (c == '>') inTag = false;
            }
            return sb.ToString();
        }

        public void ShowChoices(IList<string> texts)
        {
            while (_choiceButtons.Count < texts.Count)
            {
                int index = _choiceButtons.Count;
                var b = new Button { name = "story-choice-" + index };
                b.AddToClassList("st-choice");
                b.enableRichText = true;
                b.clicked += () => _onChoice?.Invoke(index);
                _choices.Add(b);
                _choiceButtons.Add(b);
            }
            for (int i = 0; i < _choiceButtons.Count; i++)
            {
                bool used = i < texts.Count;
                _choiceButtons[i].style.display = used ? DisplayStyle.Flex : DisplayStyle.None;
                if (used) _choiceButtons[i].text = RichLine.Parse(texts[i], _config != null ? (Func<string, string>)_config.PaletteHex : null).Markup;
            }
            _visibleChoices = texts.Count;
            _choices.EnableInClassList("st-choices--nvl", _nvlMode);
            Show(_choices);
            if (texts.Count > 0)
            {
                var first = _choiceButtons[0];
                first.Focus();
                first.schedule.Execute(() => { if (_visibleChoices > 0) first.Focus(); });
            }
        }

        public void HideChoices()
        {
            foreach (var b in _choiceButtons) b.style.display = DisplayStyle.None;
            if (_visibleChoices > 0) Root.focusController?.focusedElement?.Blur();
            _visibleChoices = 0;
            Hide(_choices);
            _pointerOverMenu = false;
        }

        public void SetContinueHint(bool shown)
        {
            _boxNext.style.visibility = shown ? Visibility.Visible : Visibility.Hidden;
            _nvlNext.style.visibility = shown ? Visibility.Visible : Visibility.Hidden;
        }

        public bool ApplyPortrait(PortraitCommand cmd, Portrait portrait, string speaker) => true;

        /// <summary>Presses choice <paramref name="index"/> as if clicked (tests).</summary>
        public void Click(int index)
        {
            if (index >= 0 && index < _visibleChoices) _onChoice?.Invoke(index);
        }

        public string ChoiceText(int index) => index >= 0 && index < _choiceButtons.Count ? _choiceButtons[index].text : "";

        // ---------- Modes ----------

        public void SetMode(bool nvl)
        {
            if (_nvlMode == nvl) return;
            _nvlMode = nvl;
            if (!nvl) ClearNvl();
            // The other box stays hidden until its first line.
            _hasLine = false;
            Refresh();
        }

        public void ClearNvl()
        {
            foreach (var l in _nvlLabels) l.RemoveFromHierarchy();
            _nvlLabels.Clear();
            _nvlPage.Clear();
        }

        /// <summary>Puts a saved page back (loading in NVL mode).</summary>
        public void LoadNvl(IList<BacklogEntry> page)
        {
            ClearNvl();
            if (page == null) return;
            foreach (var e in page)
            {
                var def = _config != null ? _config.FindCharacter(e.speaker) : null;
                var l = new Label();
                l.AddToClassList("st-nvl-line");
                l.enableRichText = true;
                if (e.kind == LineKind.Thought) l.AddToClassList("st-nvl-line--thought");
                string prefix = !string.IsNullOrEmpty(e.speaker) && e.kind == LineKind.Dialogue
                    ? "<color=#" + (def != null ? ColorUtility.ToHtmlStringRGB(def.nameColor) : "FFC94A") + "><b>" + e.speaker + "</b></color>   " : "";
                l.text = prefix + e.text;
                _nvlLines.Add(l);
                _nvlLabels.Add(l);
                _nvlPage.Add(e.Clone());
            }
        }

        /// <summary>Right click or H: hide the text to look at the picture (any press brings it back).</summary>
        public void SetHiddenByPlayer(bool hidden)
        {
            _hiddenByPlayer = hidden;
            Refresh();
        }

        void Refresh()
        {
            bool show = _open && !_hiddenByPlayer;
            _box.style.display = show && !_nvlMode && _hasLine ? DisplayStyle.Flex : DisplayStyle.None;
            _nvl.style.display = show && _nvlMode ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) Hide(_choices);
            else if (_visibleChoices > 0) Show(_choices);
        }
    }
}

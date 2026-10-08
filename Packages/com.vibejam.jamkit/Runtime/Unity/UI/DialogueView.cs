using System;
using System.Collections.Generic;
using JamKit.Dialogue;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.Shell
{
    /// <summary>
    /// The dialogue box: speaker name, typed text, choice buttons and two portrait slots (left,
    /// right), each a base sprite with an expression layer on top. Driven by <see cref="DialogueService"/>.
    /// </summary>
    public sealed class DialogueView : IDialogueView
    {
        public readonly VisualElement Root;
        readonly Label _speaker, _text, _hint;
        readonly VisualElement _choices;
        readonly Slot _left, _right;
        readonly List<Button> _choiceButtons = new List<Button>();

        public Action<int> ChoiceClicked;

        sealed class Slot
        {
            public VisualElement root, baseLayer, exprLayer;
            public string character = "";
            public bool shown;
        }

        public string SpeakerText => _speaker != null ? _speaker.text : "";
        public string BodyText => _text != null ? _text.text : "";
        int _visibleChoices;
        public int VisibleChoiceCount => _visibleChoices;
        public string LeftCharacter => _left.shown ? _left.character : "";
        public string RightCharacter => _right.shown ? _right.character : "";

        public DialogueView(VisualTreeAsset tree, VisualElement parent)
        {
            Root = tree != null ? (VisualElement)tree.Instantiate() : new VisualElement();
            Root.AddToClassList("jk-layer");
            Root.pickingMode = PickingMode.Ignore;
            parent.Add(Root);
            _speaker = Root.Q<Label>("speaker-name");
            _text = Root.Q<Label>("dialogue-text");
            _hint = Root.Q<Label>("continue-hint");
            _choices = Root.Q<VisualElement>("choices");
            _left = MakeSlot("portrait-left");
            _right = MakeSlot("portrait-right");
            Root.style.display = DisplayStyle.None;
        }

        Slot MakeSlot(string name)
        {
            var s = new Slot
            {
                root = Root.Q<VisualElement>(name),
                baseLayer = Root.Q<VisualElement>(name + "-base"),
                exprLayer = Root.Q<VisualElement>(name + "-expr"),
            };
            if (s.root != null) s.root.style.visibility = Visibility.Hidden;
            return s;
        }

        public void SetChoiceHandler(Action<int> onChoice) => ChoiceClicked = onChoice;

        public void BeginLine(DialogueLineInfo line, RichLine rich) { }

        public void SetTyped(RichLine rich, int visible) => SetText(rich != null ? rich.Build(visible) : "");

        public void Open()
        {
            Root.style.display = DisplayStyle.Flex;
            SetSpeaker("");
            SetText("");
            HideChoices();
            HideSlot(_left);
            HideSlot(_right);
        }

        public void Close()
        {
            HideChoices();
            Root.style.display = DisplayStyle.None;
        }

        public void SetSpeaker(string name)
        {
            if (_speaker != null)
            {
                _speaker.text = name ?? "";
                _speaker.style.display = string.IsNullOrEmpty(name) ? DisplayStyle.None : DisplayStyle.Flex;
            }
            Highlight(_left, name);
            Highlight(_right, name);
        }

        static void Highlight(Slot s, string speaker)
        {
            if (s.root == null) return;
            bool active = string.IsNullOrEmpty(speaker) || string.Equals(s.character, speaker, StringComparison.OrdinalIgnoreCase);
            s.root.EnableInClassList("jk-portrait--inactive", !active);
        }

        public void SetText(string text)
        {
            if (_text != null) _text.text = text ?? "";
        }

        public void SetContinueHint(bool shown)
        {
            if (_hint != null) _hint.style.visibility = shown ? Visibility.Visible : Visibility.Hidden;
        }

        /// <summary>Applies a #portrait command. Returns false if the character has no Portrait asset.</summary>
        public bool ApplyPortrait(PortraitCommand cmd, Portrait portrait, string speaker)
        {
            Slot existing = Matches(_left, cmd.character) ? _left : Matches(_right, cmd.character) ? _right : null;
            if (cmd.hide)
            {
                if (existing != null) HideSlot(existing);
                return true;
            }
            if (portrait == null) return false;

            Slot slot = cmd.side == PortraitSide.Left ? _left
                : cmd.side == PortraitSide.Right ? _right
                : existing ?? (!_left.shown ? _left : !_right.shown ? _right : _left);
            if (existing != null && existing != slot) HideSlot(existing);

            slot.character = cmd.character;
            slot.shown = true;
            if (slot.root != null) slot.root.style.visibility = Visibility.Visible;
            SetSprite(slot.baseLayer, portrait.baseSprite);
            var expr = portrait.FindExpression(cmd.expression);
            if (!string.IsNullOrEmpty(cmd.expression) && expr == null)
                Debug.LogWarning($"JamKit: portrait \"{cmd.character}\" has no expression \"{cmd.expression}\".");
            SetSprite(slot.exprLayer, expr);
            Highlight(slot, speaker);
            return true;
        }

        static bool Matches(Slot s, string character) =>
            s.shown && string.Equals(s.character, character, StringComparison.OrdinalIgnoreCase);

        static void HideSlot(Slot s)
        {
            s.shown = false;
            s.character = "";
            if (s.root != null) s.root.style.visibility = Visibility.Hidden;
        }

        static void SetSprite(VisualElement layer, Sprite sprite)
        {
            if (layer == null) return;
            if (sprite == null)
            {
                layer.style.backgroundImage = StyleKeyword.None;
                layer.style.display = DisplayStyle.None;
                return;
            }
            layer.style.backgroundImage = new StyleBackground(sprite);
            layer.style.display = DisplayStyle.Flex;
        }

        public void ShowChoices(IList<string> texts)
        {
            if (_choices == null) return;
            while (_choiceButtons.Count < texts.Count)
            {
                int index = _choiceButtons.Count;
                var b = new Button { name = "choice-" + index };
                b.AddToClassList("jk-choice");
                b.clicked += () => ChoiceClicked?.Invoke(index);
                _choices.Add(b);
                _choiceButtons.Add(b);
            }
            for (int i = 0; i < _choiceButtons.Count; i++)
            {
                bool used = i < texts.Count;
                _choiceButtons[i].style.display = used ? DisplayStyle.Flex : DisplayStyle.None;
                if (used) _choiceButtons[i].text = (i + 1) + ".  " + texts[i];
            }
            _visibleChoices = texts.Count;
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
        }
    }
}

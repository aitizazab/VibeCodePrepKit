using System;
using System.Collections.Generic;
using JamKit.Flow;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.Doomlike
{
    /// <summary>
    /// The "pick one" screen between levels (the upgrade draft): a heading, a row of cards and footer
    /// buttons. Keyboard (arrows + Enter/Space), gamepad (stick + A) and mouse. Built in code inside
    /// the template HUD; styled by Doomlike.uss with the shell's theme. Tests and the smoke run
    /// press cards with <see cref="Choose"/>.
    /// </summary>
    public sealed class DoomChoicePanel
    {
        readonly VisualElement _root, _cards, _footer;
        readonly Label _heading, _sub;
        readonly List<Button> _cardButtons = new List<Button>();
        readonly List<Action> _cardActions = new List<Action>();
        readonly List<Button> _footerButtons = new List<Button>();
        readonly List<Action> _footerActions = new List<Action>();
        bool _open;
        float _inputBlock;

        public bool IsOpen => _open;
        public int CardCount => _cardButtons.Count;
        public string Heading => _heading.text;

        public DoomChoicePanel(VisualElement parent)
        {
            _root = new VisualElement { name = "dl-choice" };
            _root.AddToClassList("dl-choice");
            _root.pickingMode = PickingMode.Position;
            var panel = new VisualElement();
            panel.AddToClassList("dl-choice-panel");
            _heading = new Label();
            _heading.AddToClassList("jk-heading");
            _sub = new Label();
            _sub.AddToClassList("jk-text");
            _sub.AddToClassList("jk-dim");
            _cards = new VisualElement();
            _cards.AddToClassList("dl-cards");
            _footer = new VisualElement();
            _footer.AddToClassList("jk-button-row");
            panel.Add(_heading);
            panel.Add(_sub);
            panel.Add(_cards);
            panel.Add(_footer);
            _root.Add(panel);
            parent.Add(_root);
            _root.style.display = DisplayStyle.None;
        }

        public void Begin(string heading, string subheading)
        {
            _heading.text = heading ?? "";
            _sub.text = subheading ?? "";
            _sub.style.display = string.IsNullOrEmpty(subheading) ? DisplayStyle.None : DisplayStyle.Flex;
            _cards.Clear();
            _footer.Clear();
            _cardButtons.Clear();
            _cardActions.Clear();
            _footerButtons.Clear();
            _footerActions.Clear();
        }

        public void AddCard(string kicker, Color kickerColor, string title, string body, string iconText, Color iconColor, Action onPick)
        {
            var b = new Button();
            b.AddToClassList("dl-card");
            b.text = "";
            var icon = new Label(iconText ?? "") { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("dl-card-icon");
            icon.style.backgroundColor = new Color(iconColor.r * 0.35f, iconColor.g * 0.35f, iconColor.b * 0.35f, 1f);
            icon.style.color = iconColor;
            icon.style.borderTopColor = iconColor;
            icon.style.borderBottomColor = iconColor;
            icon.style.borderLeftColor = iconColor;
            icon.style.borderRightColor = iconColor;
            var k = new Label(kicker) { pickingMode = PickingMode.Ignore };
            k.AddToClassList("dl-card-kicker");
            k.style.color = kickerColor;
            var t = new Label(title) { pickingMode = PickingMode.Ignore };
            t.AddToClassList("dl-card-title");
            var d = new Label(body) { pickingMode = PickingMode.Ignore };
            d.AddToClassList("dl-card-body");
            b.Add(icon);
            b.Add(k);
            b.Add(t);
            b.Add(d);
            b.style.borderTopColor = kickerColor;
            int index = _cardButtons.Count;
            b.clicked += () => Press(index);
            _cards.Add(b);
            _cardButtons.Add(b);
            _cardActions.Add(onPick);
        }

        public void AddFooter(string text, Action onPick)
        {
            var b = new Button { text = text };
            b.AddToClassList("jk-button");
            b.AddToClassList("jk-button--small");
            int index = _footerButtons.Count;
            b.clicked += () => PressFooter(index);
            _footer.Add(b);
            _footerButtons.Add(b);
            _footerActions.Add(onPick);
        }

        public void Show()
        {
            _open = true;
            _inputBlock = 0.3f; // the press that closed the last screen mustn't pick on this one
            _root.style.display = DisplayStyle.Flex;
            _root.BringToFront();
            FocusFirst();
            GameRoot.Audio.Play("ui_move");
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;
            _root.style.display = DisplayStyle.None;
            _root.focusController?.focusedElement?.Blur();
        }

        /// <summary>Picks card <paramref name="index"/> as if clicked. False if there's no such card.</summary>
        public bool Choose(int index)
        {
            if (!_open || index < 0 || index >= _cardActions.Count) return false;
            GameRoot.Audio.Play("ui_select");
            _cardActions[index]?.Invoke();
            return true;
        }

        public bool ChooseFooter(int index)
        {
            if (!_open || index < 0 || index >= _footerActions.Count) return false;
            GameRoot.Audio.Play("ui_select");
            _footerActions[index]?.Invoke();
            return true;
        }

        void Press(int index)
        {
            if (_inputBlock > 0f) return;
            Choose(index);
        }

        void PressFooter(int index)
        {
            if (_inputBlock > 0f) return;
            ChooseFooter(index);
        }

        void FocusFirst()
        {
            VisualElement target = _cardButtons.Count > 0 ? _cardButtons[0] : _footerButtons.Count > 0 ? _footerButtons[0] : null;
            target?.Focus();
        }

        /// <summary>Keeps keyboard/gamepad focus on the panel (after a pause menu, or a click on the backdrop).</summary>
        public void Tick(float unscaledDt)
        {
            if (!_open) return;
            if (_inputBlock > 0f) _inputBlock -= unscaledDt;
            if (GameRoot.Flow.Current != GameState.Playing || GameRoot.Ui.Top != null) return;
            var focused = _root.focusController?.focusedElement as VisualElement;
            bool inside = false;
            for (var p = focused; p != null; p = p.parent)
                if (p == _root) { inside = true; break; }
            if (!inside) FocusFirst();
        }
    }
}

using System;
using System.Collections.Generic;
using JamKit.Flow;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.BulletHell
{
    /// <summary>
    /// The "pick one" screen used for drafts (1 of 3 upgrades), doors, the shop, rest sites and the
    /// encounter rooms (Forge, Altar, Wager, Trial board, Trader, and "pick one of your upgrades"):
    /// a heading, an optional note (odds, a warning), a row of cards (rarity or type in colour, name,
    /// description, price or odds) and footer buttons (reroll, leave). Compact cards wrap for long lists.
    /// Keyboard (arrows + Enter/Space), gamepad (stick + A) and mouse.
    /// Built in code inside the template HUD; styled by BulletHell.uss with the shell's theme.
    /// Tests and the smoke run press cards with <see cref="Choose"/>.
    /// </summary>
    public sealed class ChoicePanel
    {
        public enum Kind { None, Draft, Doors, Shop, Rest, Forge, Altar, Wager, Trial, Trader, Pick }

        readonly VisualElement _root, _cards, _footer;
        readonly Label _heading, _sub, _note;
        readonly List<string> _cardTitles = new List<string>();
        readonly List<Button> _cardButtons = new List<Button>();
        readonly List<Action> _cardActions = new List<Action>();
        readonly List<bool> _cardEnabled = new List<bool>();
        readonly List<Button> _footerButtons = new List<Button>();
        readonly List<Action> _footerActions = new List<Action>();
        Kind _kind;
        bool _open;
        float _inputBlock;

        public bool IsOpen => _open;
        public Kind Current => _open ? _kind : Kind.None;
        public int CardCount => _cardButtons.Count;
        public int FooterCount => _footerButtons.Count;
        public string Heading => _heading.text;
        public string Note => _note.text;
        /// <summary>Card <paramref name="i"/>'s title ("" if there's no such card), for tests and the smoke log.</summary>
        public string CardTitle(int i) => i >= 0 && i < _cardTitles.Count ? _cardTitles[i] : "";
        /// <summary>The first card whose title contains <paramref name="text"/>, or -1.</summary>
        public int FindCard(string text)
        {
            for (int i = 0; i < _cardTitles.Count; i++)
                if (_cardTitles[i] != null && _cardTitles[i].IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0) return i;
            return -1;
        }

        public ChoicePanel(VisualElement parent)
        {
            _root = new VisualElement { name = "bh-choice" };
            _root.AddToClassList("bh-choice");
            _root.pickingMode = PickingMode.Position; // swallow clicks on the backdrop
            var panel = new VisualElement();
            panel.AddToClassList("bh-choice-panel");
            _heading = new Label();
            _heading.AddToClassList("jk-heading");
            _sub = new Label();
            _sub.AddToClassList("jk-text");
            _sub.AddToClassList("jk-dim");
            _note = new Label();
            _note.AddToClassList("jk-text");
            _note.AddToClassList("bh-choice-note");
            _cards = new VisualElement();
            _cards.AddToClassList("bh-cards");
            _footer = new VisualElement();
            _footer.AddToClassList("jk-button-row");
            panel.Add(_heading);
            panel.Add(_sub);
            panel.Add(_note);
            panel.Add(_cards);
            panel.Add(_footer);
            _root.Add(panel);
            parent.Add(_root);
            _root.style.display = DisplayStyle.None;
        }

        /// <summary>Starts a fresh screen (clears the old cards). Add cards and buttons, then <see cref="Show"/>.
        /// <paramref name="compact"/>: smaller cards that wrap (a list of the player's upgrades).</summary>
        public void Begin(Kind kind, string heading, string subheading, bool compact = false)
        {
            _kind = kind;
            _heading.text = heading ?? "";
            _sub.text = subheading ?? "";
            _sub.style.display = string.IsNullOrEmpty(subheading) ? DisplayStyle.None : DisplayStyle.Flex;
            SetNote(null);
            _cards.EnableInClassList("bh-cards--compact", compact);
            _cardTitles.Clear();
            _cards.Clear();
            _footer.Clear();
            _cardButtons.Clear();
            _cardActions.Clear();
            _cardEnabled.Clear();
            _footerButtons.Clear();
            _footerActions.Clear();
        }

        /// <summary>A line under the heading: the odds of a forge push, what an offering costs. Null hides it.</summary>
        public void SetNote(string text)
        {
            _note.text = text ?? "";
            _note.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public void AddCard(string kicker, Color kickerColor, string title, string body, string price, bool enabled, Action onPick, Sprite icon = null)
        {
            _cardTitles.Add(title ?? "");
            var b = new Button();
            b.AddToClassList("bh-card");
            b.text = "";
            if (icon != null)
            {
                var img = new VisualElement { pickingMode = PickingMode.Ignore };
                img.AddToClassList("bh-card-icon");
                img.style.backgroundImage = new StyleBackground(icon);
                b.Add(img);
            }
            var k = new Label(kicker) { pickingMode = PickingMode.Ignore };
            k.AddToClassList("bh-card-kicker");
            k.style.color = kickerColor;
            var t = new Label(title) { pickingMode = PickingMode.Ignore };
            t.AddToClassList("bh-card-title");
            var d = new Label(body) { pickingMode = PickingMode.Ignore };
            d.AddToClassList("bh-card-body");
            b.Add(k);
            b.Add(t);
            b.Add(d);
            if (!string.IsNullOrEmpty(price))
            {
                var p = new Label(price) { pickingMode = PickingMode.Ignore };
                p.AddToClassList("bh-card-price");
                b.Add(p);
            }
            b.style.borderTopColor = kickerColor;
            int index = _cardButtons.Count;
            b.clicked += () => Press(index);
            b.SetEnabled(enabled);
            _cards.Add(b);
            _cardButtons.Add(b);
            _cardActions.Add(onPick);
            _cardEnabled.Add(enabled);
        }

        public void AddFooter(string text, bool enabled, Action onPick)
        {
            var b = new Button { text = text };
            b.AddToClassList("jk-button");
            b.AddToClassList("jk-button--small");
            int index = _footerButtons.Count;
            b.clicked += () => PressFooter(index);
            b.SetEnabled(enabled);
            _footer.Add(b);
            _footerButtons.Add(b);
            _footerActions.Add(onPick);
        }

        public void Show()
        {
            _open = true;
            _inputBlock = 0.25f; // the press that closed the last screen mustn't pick on this one
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

        /// <summary>Picks card <paramref name="index"/> as if clicked. False if there's no such enabled card.</summary>
        public bool Choose(int index)
        {
            if (!_open || index < 0 || index >= _cardActions.Count || !_cardEnabled[index]) return false;
            GameRoot.Audio.Play("ui_select");
            _cardActions[index]?.Invoke();
            return true;
        }

        public bool ChooseFooter(int index)
        {
            if (!_open || index < 0 || index >= _footerActions.Count || !_footerButtons[index].enabledSelf) return false;
            GameRoot.Audio.Play("ui_select");
            _footerActions[index]?.Invoke();
            return true;
        }

        /// <summary>The first enabled card, or -1.</summary>
        public int FirstEnabledCard()
        {
            for (int i = 0; i < _cardEnabled.Count; i++) if (_cardEnabled[i]) return i;
            return -1;
        }

        public bool IsCardEnabled(int i) => i >= 0 && i < _cardEnabled.Count && _cardEnabled[i];

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
            VisualElement target = null;
            for (int i = 0; i < _cardButtons.Count && target == null; i++)
                if (_cardEnabled[i]) target = _cardButtons[i];
            for (int i = 0; i < _footerButtons.Count && target == null; i++)
                if (_footerButtons[i].enabledSelf) target = _footerButtons[i];
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

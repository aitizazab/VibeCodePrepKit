using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.Shell
{
    /// <summary>
    /// A full-screen menu cloned from a UXML file. Buttons are wired by name with <see cref="Bind"/>,
    /// which also lets tests and the smoke run press them with <see cref="Activate"/>.
    /// Showing a screen focuses its first button, so keyboard and gamepad work at once.
    /// </summary>
    public abstract class ShellScreen
    {
        public VisualElement Root;
        protected ShellUI Ui;
        readonly Dictionary<string, Action> _actions = new Dictionary<string, Action>();
        readonly List<Button> _buttons = new List<Button>();
        VisualElement _lastFocused;

        public bool IsVisible => Root != null && Root.style.display != DisplayStyle.None;

        public void Init(ShellUI ui, VisualTreeAsset tree, VisualElement parent)
        {
            Ui = ui;
            if (tree != null)
            {
                var container = tree.Instantiate();
                // The screen's own root element (first child) carries the classes; the TemplateContainer just fills the layer.
                container.AddToClassList("jk-layer");
                container.pickingMode = PickingMode.Ignore;
                Root = container;
            }
            else
            {
                Root = new VisualElement();
                Root.AddToClassList("jk-layer");
                Root.Add(new Label(GetType().Name + ": UXML missing. Run JamKit > Rebuild Shell Scenes."));
            }
            Root.style.display = DisplayStyle.None;
            parent.Add(Root);
            OnInit();
        }

        protected abstract void OnInit();

        /// <summary>Wires a button by name. Missing buttons log a warning instead of breaking the screen.</summary>
        protected Button Bind(string name, Action action)
        {
            _actions[name] = action;
            var b = Root.Q<Button>(name);
            if (b == null)
            {
                Debug.LogWarning($"JamKit: {GetType().Name} has no button named \"{name}\" in its UXML.");
                return null;
            }
            b.clicked += () =>
            {
                Ui.PlayUiSound("ui_select");
                action();
            };
            b.RegisterCallback<FocusInEvent>(_ => { _lastFocused = b; });
            _buttons.Add(b);
            return b;
        }

        /// <summary>Presses a bound button by name, as if clicked (tests, smoke run). Returns false if unknown or hidden.</summary>
        public bool Activate(string name)
        {
            if (!_actions.TryGetValue(name, out var a)) return false;
            var b = Root.Q<Button>(name);
            if (b != null && b.style.display == DisplayStyle.None) return false;
            a();
            return true;
        }

        public void Show()
        {
            Root.style.display = DisplayStyle.Flex;
            Root.BringToFront();
            OnShow();
            FocusFirst();
        }

        public void Hide()
        {
            if (Root.style.display == DisplayStyle.None) return;
            Root.style.display = DisplayStyle.None;
            OnHide();
        }

        /// <summary>Focuses the last focused (if still visible) or the first visible, enabled button.</summary>
        public virtual void FocusFirst()
        {
            Root.schedule.Execute(() =>
            {
                if (Root.style.display == DisplayStyle.None) return;
                var target = FirstFocusable();
                target?.Focus();
            });
            var now = FirstFocusable();
            now?.Focus();
        }

        protected virtual VisualElement FirstFocusable()
        {
            foreach (var b in _buttons)
                if (b.style.display != DisplayStyle.None && b.enabledInHierarchy && b.canGrabFocus) return b;
            return null;
        }

        /// <summary>Back/Cancel pressed while this screen is on top. Return true if handled.</summary>
        public virtual bool OnBack() => false;

        public virtual void Tick(float unscaledDt) { }

        protected virtual void OnShow() { }

        protected virtual void OnHide() { }

        protected static void SetShown(VisualElement e, bool shown)
        {
            if (e != null) e.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Remembers the focused button so it gets focus back after an overlay closes.</summary>
        public void RestoreFocus()
        {
            if (_lastFocused != null && _lastFocused.style.display != DisplayStyle.None) _lastFocused.Focus();
            else FocusFirst();
        }
    }
}

using System.Collections.Generic;
using UnityEngine.UIElements;

namespace JamKit.Shell
{
    /// <summary>
    /// Presenter mode's "Hide HUD/UI": hides the shell's HUD, dialogue, floating-text and debug layers
    /// (menus stay usable), except anything marked with <see cref="KeepClass"/> (a template's picture drawn in UI Toolkit: the story stage,
    /// Visiting Hours' argument box). Containers holding a kept element are opened up and their other
    /// children hidden instead. What it hid is restored exactly on <see cref="Restore"/>; if game code
    /// shows a hidden element meanwhile, that wish is remembered and applied on restore. A periodic
    /// re-walk catches elements added after hiding. No allocation per frame.
    /// </summary>
    public sealed class UiHider
    {
        /// <summary>Elements (and their children) with this class stay visible when the UI is hidden.</summary>
        public const string KeepClass = "jk-presenter-keep";

        readonly List<VisualElement> _roots = new List<VisualElement>(8);
        readonly List<VisualElement> _hidden = new List<VisualElement>(64);
        readonly List<StyleEnum<DisplayStyle>> _was = new List<StyleEnum<DisplayStyle>>(64);
        bool _active;
        float _rewalk;

        public bool Active => _active;
        public int HiddenCount => _hidden.Count;

        public void Hide(IList<VisualElement> roots)
        {
            Restore();
            _roots.Clear();
            for (int i = 0; i < roots.Count; i++) if (roots[i] != null) _roots.Add(roots[i]);
            _active = true;
            Walk();
        }

        public void Restore()
        {
            for (int i = 0; i < _hidden.Count; i++) _hidden[i].style.display = _was[i];
            _hidden.Clear();
            _was.Clear();
            _active = false;
        }

        /// <summary>Keeps things hidden that game code showed again, and every 0.25 s picks up new elements.</summary>
        public void Tick(float unscaledDt)
        {
            if (!_active) return;
            for (int i = 0; i < _hidden.Count; i++)
            {
                var e = _hidden[i];
                var cur = e.style.display;
                if (cur.keyword == StyleKeyword.Undefined && cur.value == DisplayStyle.None) continue;
                _was[i] = cur;
                e.style.display = DisplayStyle.None;
            }
            _rewalk -= unscaledDt;
            if (_rewalk <= 0f)
            {
                _rewalk = 0.25f;
                Walk();
            }
        }

        void Walk()
        {
            for (int i = 0; i < _roots.Count; i++) Visit(_roots[i]);
        }

        // A layer with nothing to keep is hidden whole (nothing else ever sets a shell layer's display);
        // only branches holding a kept element are opened up.
        void Visit(VisualElement e)
        {
            if (e.ClassListContains(KeepClass)) return;
            if (ContainsKept(e))
            {
                int n = e.childCount;
                for (int i = 0; i < n; i++) Visit(e[i]);
                return;
            }
            if (_hidden.Contains(e)) return;
            _was.Add(e.style.display);
            _hidden.Add(e);
            e.style.display = DisplayStyle.None;
        }

        static bool ContainsKept(VisualElement e)
        {
            int n = e.childCount;
            for (int i = 0; i < n; i++)
            {
                var c = e[i];
                if (c.ClassListContains(KeepClass) || ContainsKept(c)) return true;
            }
            return false;
        }
    }
}

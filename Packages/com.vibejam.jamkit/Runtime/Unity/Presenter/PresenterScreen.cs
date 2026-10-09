using System.Collections.Generic;
using System.Globalization;
using JamKit.Presenting;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.Shell
{
    /// <summary>
    /// Presenter mode's panel (an overlay: Escape or the chord hides it, game time stops behind it):
    /// the template's chapters on the left, the toggles on the right. Built in code; its look is the
    /// .jk-presenter-* rules in Shell.uss. Buttons have names so tests can press them
    /// (presenter-chapter-0.., presenter-invincible, -slowmo, -freeze, -hideui, -indicator, -photo,
    /// -attract, -next, -exit, -close).
    /// </summary>
    public sealed class PresenterScreen : ShellScreen
    {
        public const int MaxChapters = 16;

        readonly Button[] _chapters = new Button[MaxChapters];
        readonly List<string> _names = new List<string>(MaxChapters);
        Button _inv, _slow, _freeze, _hide, _indicator, _photo, _attract;
        Label _group, _empty, _status;

        protected override bool BuildsOwnContent => true;

        public static string ChapterButton(int index) => "presenter-chapter-" + index;

        /// <summary>The chapters listed right now (refreshed each time the panel opens).</summary>
        public IReadOnlyList<string> Chapters => _names;

        protected override void OnInit()
        {
            Root.AddToClassList("jk-presenter");
            var panel = new VisualElement { name = "presenter-panel" };
            panel.AddToClassList("jk-presenter-panel");
            Root.Add(panel);

            var kicker = new Label("PRESENTER MODE");
            kicker.AddToClassList("jk-presenter-kicker");
            panel.Add(kicker);
            _group = new Label("");
            _group.AddToClassList("jk-presenter-title");
            panel.Add(_group);

            var columns = new VisualElement();
            columns.AddToClassList("jk-presenter-columns");
            panel.Add(columns);

            var left = new VisualElement();
            left.AddToClassList("jk-presenter-column");
            columns.Add(left);
            left.Add(Heading("Chapters"));
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("jk-presenter-scroll");
            left.Add(scroll);
            for (int i = 0; i < MaxChapters; i++)
            {
                var b = MakeButton(ChapterButton(i), "", scroll.contentContainer);
                b.AddToClassList("jk-presenter-chapter");
                b.style.display = DisplayStyle.None;
                _chapters[i] = b;
            }
            _empty = new Label("This game has no chapters yet (a template registers them; see docs/PRESENTING.md).");
            _empty.AddToClassList("jk-presenter-note");
            scroll.Add(_empty);
            MakeButton("presenter-next", "Next chapter", left);

            var right = new VisualElement();
            right.AddToClassList("jk-presenter-column");
            columns.Add(right);
            right.Add(Heading("Show"));
            _inv = MakeButton("presenter-invincible", "", right);
            _slow = MakeButton("presenter-slowmo", "", right);
            _freeze = MakeButton("presenter-freeze", "", right);
            _hide = MakeButton("presenter-hideui", "", right);
            _indicator = MakeButton("presenter-indicator", "", right);
            right.Add(Heading("Capture"));
            _photo = MakeButton("presenter-photo", "Photo (2x PNG)", right);
            _attract = MakeButton("presenter-attract", "", right);
            right.Add(Heading("Leave"));
            MakeButton("presenter-close", "Hide panel (keep settings)", right);
            MakeButton("presenter-exit", "Exit presenter mode", right);

            _status = new Label("");
            _status.AddToClassList("jk-presenter-note");
            panel.Add(_status);
            var hint = new Label("Ctrl+Shift+P or hold L3+R3: show / hide     Esc: hide     Exit undoes everything");
            hint.AddToClassList("jk-presenter-note");
            panel.Add(hint);

            for (int i = 0; i < MaxChapters; i++)
            {
                int index = i;
                Bind(ChapterButton(i), () => JumpTo(index));
            }
            Bind("presenter-next", () => PresenterMode.NextChapter());
            Bind("presenter-invincible", () => { PresenterMode.Invincible = !PresenterMode.Invincible; Refresh(); });
            Bind("presenter-slowmo", () => { PresenterMode.CycleSlowMotion(); Refresh(); });
            Bind("presenter-freeze", () => { PresenterMode.Frozen = !PresenterMode.Frozen; Refresh(); });
            Bind("presenter-hideui", () => { PresenterMode.UiHidden = !PresenterMode.UiHidden; Refresh(); });
            Bind("presenter-indicator", () => { PresenterMode.ShowIndicator = !PresenterMode.ShowIndicator; Refresh(); });
            Bind("presenter-photo", () => PresenterMode.TakePhoto());
            Bind("presenter-attract", () =>
            {
                if (PresenterMode.AttractRunning) PresenterMode.StopAttract();
                else PresenterMode.StartAttract();
                Refresh();
            });
            Bind("presenter-close", PresenterMode.Hide);
            Bind("presenter-exit", PresenterMode.Close);
        }

        static Label Heading(string text)
        {
            var l = new Label(text);
            l.AddToClassList("jk-presenter-heading");
            return l;
        }

        static Button MakeButton(string name, string text, VisualElement parent)
        {
            var b = new Button { name = name, text = text };
            b.AddToClassList("jk-presenter-button");
            parent.Add(b);
            return b;
        }

        protected override void OnShow()
        {
            PresenterMode.SetBackstage(true);
            Refresh();
        }

        protected override void OnHide() => PresenterMode.SetBackstage(false);

        /// <summary>Jumps to the listed chapter <paramref name="index"/> (closes the panel).</summary>
        public JumpResult JumpTo(int index)
        {
            if (index < 0 || index >= _names.Count) return JumpResult.Unknown;
            return PresenterMode.Jump(_names[index]);
        }

        public void Refresh()
        {
            PresenterMode.ChapterNames(_names);
            string group = PresenterMode.CurrentGroup;
            _group.text = GameRoot.Exists ? GameRoot.Config.gameTitle + "  (" + group + ")" : group;
            for (int i = 0; i < MaxChapters; i++)
            {
                bool shown = i < _names.Count;
                _chapters[i].style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                if (shown) _chapters[i].text = (i + 1) + ".  " + _names[i];
            }
            _empty.style.display = _names.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _inv.text = "Invincible: " + (PresenterMode.Invincible ? "ON" : "off");
            _slow.text = "Slow motion: x" + PresenterMode.SlowMotion.ToString("0.##", CultureInfo.InvariantCulture);
            _freeze.text = "Freeze frame: " + (PresenterMode.Frozen ? "ON" : "off");
            _hide.text = "Hide HUD / UI: " + (PresenterMode.UiHidden ? "ON" : "off");
            _indicator.text = "Corner indicator: " + (PresenterMode.ShowIndicator ? "on" : "off");
            _attract.text = PresenterMode.AttractRunning ? "Stop attract mode" : "Attract mode (plays itself)";
            _photo.SetEnabled(PresenterMode.CanTakePhoto);
            var chapters = PresenterMode.Chapters;
            string last = chapters.LastJumped;
            _status.text = (last.Length > 0 ? "Last chapter: " + last : "No chapter jumped to yet")
                + (chapters.HasPending ? "    Loading: " + chapters.PendingName : "")
                + (PresenterMode.LastPhotoPath.Length > 0 ? "    Last photo: " + PresenterMode.LastPhotoPath : "");
        }
    }
}

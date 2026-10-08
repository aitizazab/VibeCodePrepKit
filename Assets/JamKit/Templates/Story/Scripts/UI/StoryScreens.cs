using System;
using System.Collections.Generic;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.Narrative
{
    /// <summary>
    /// The story's own menus, added to the shell's UI (so Back, focus, sounds and the theme work
    /// like Settings): save/load slots, the endings gallery, the backlog and the debug knot list.
    /// Made once per run of the game (the title screen can open them before the story scene exists).
    /// </summary>
    public static class StoryScreens
    {
        static ShellUI _ui;
        static SaveLoadScreen _saveLoad;
        static EndingsScreen _endings;
        static BacklogScreen _backlog;
        static KnotScreen _knots;

        public static SaveLoadScreen SaveLoad => _saveLoad;
        public static EndingsScreen Endings => _endings;
        public static BacklogScreen Backlog => _backlog;
        public static KnotScreen Knots => _knots;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _ui = null;
            _saveLoad = null;
            _endings = null;
            _backlog = null;
            _knots = null;
        }

        public static void Ensure(StoryConfig config)
        {
            if (!GameRoot.Exists) return;
            var ui = GameRoot.Ui;
            if (ReferenceEquals(_ui, ui) && _saveLoad != null) return;
            _ui = ui;
            _saveLoad = ui.AddScreen(new SaveLoadScreen(config), null);
            _endings = ui.AddScreen(new EndingsScreen(config), null);
            _backlog = ui.AddScreen(new BacklogScreen(config), null);
            _knots = ui.AddScreen(new KnotScreen(config), null);
        }

        public static void OpenSave() { _saveLoad.SetMode(true); _ui.OpenOverlay(_saveLoad); }
        public static void OpenLoad() { _saveLoad.SetMode(false); _ui.OpenOverlay(_saveLoad); }
        public static void OpenEndings() => _ui.OpenOverlay(_endings);
        public static void OpenBacklog(Backlog log) { _backlog.Set(log); _ui.OpenOverlay(_backlog); }
        public static void OpenKnots(IList<string> knots, Action<string> jump) { _knots.Set(knots, jump); _ui.OpenOverlay(_knots); }

        public static bool AnyOpen => _ui != null && (_ui.IsOverlayOpen(_saveLoad) || _ui.IsOverlayOpen(_endings) || _ui.IsOverlayOpen(_backlog) || _ui.IsOverlayOpen(_knots));

        // ---------- Shared building ----------

        internal static VisualElement Panel(ShellScreen screen, VisualElement root, StoryConfig config, string title, out Label heading, out VisualElement body)
        {
            if (config != null && config.style != null) root.styleSheets.Add(config.style);
            var scrim = new VisualElement();
            scrim.AddToClassList("jk-screen");
            scrim.AddToClassList("jk-scrim");
            root.Add(scrim);
            var panel = new VisualElement();
            panel.AddToClassList("jk-panel");
            panel.AddToClassList("jk-panel--wide");
            panel.AddToClassList("st-panel");
            scrim.Add(panel);
            heading = new Label(title);
            heading.AddToClassList("jk-heading");
            panel.Add(heading);
            body = new VisualElement();
            body.AddToClassList("st-panel-body");
            panel.Add(body);
            return panel;
        }

        internal static Button BackButton(VisualElement panel, string name = "back")
        {
            var back = new Button { name = name, text = "Back" };
            back.AddToClassList("jk-button");
            back.AddToClassList("jk-button--back");
            panel.Add(back);
            return back;
        }

        internal static Label L(string text, string cls, VisualElement parent)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.AddToClassList(cls);
            l.enableRichText = true;
            parent.Add(l);
            return l;
        }
    }

    /// <summary>Save to, or load from, the three slots, the quick save and the autosave.</summary>
    public sealed class SaveLoadScreen : ShellScreen
    {
        readonly StoryConfig _config;
        readonly Dictionary<string, Button> _cards = new Dictionary<string, Button>();
        readonly Dictionary<string, Label> _texts = new Dictionary<string, Label>();
        Label _heading;
        bool _saving;

        public bool Saving => _saving;

        public SaveLoadScreen(StoryConfig config) { _config = config; }

        protected override bool BuildsOwnContent => true;

        public void SetMode(bool saving) => _saving = saving;

        protected override void OnInit()
        {
            var panel = StoryScreens.Panel(this, Root, _config, "Load", out _heading, out var body);
            foreach (var slot in StorySlots.All)
            {
                var card = new Button { name = "slot-" + slot };
                card.AddToClassList("st-slot");
                var text = StoryScreens.L("", "st-slot-text", card);
                body.Add(card);
                _cards[slot] = card;
                _texts[slot] = text;
                string s = slot;
                Bind("slot-" + slot, () => Pick(s));
            }
            StoryScreens.BackButton(panel);
            Bind("back", () => Ui.CloseOverlay());
        }

        protected override void OnShow() => Refresh();

        public void Refresh()
        {
            _heading.text = _saving ? "Save" : "Load";
            foreach (var slot in StorySlots.All)
            {
                var snap = StoryStorage.ReadSlot(slot);
                bool shown = !_saving || slot != StorySlots.Auto;
                SetShown(_cards[slot], shown);
                string name = "<b>" + StorySlots.DisplayName(slot) + "</b>";
                if (snap == null)
                {
                    _texts[slot].text = name + "   <color=#8a879a>empty</color>";
                    _cards[slot].EnableInClassList("st-slot--empty", true);
                    continue;
                }
                _cards[slot].EnableInClassList("st-slot--empty", false);
                string line = snap.lineText ?? "";
                line = JamKit.Dialogue.RichLine.StripTags(line);
                if (line.Length > 70) line = line.Substring(0, 67) + "...";
                string who = string.IsNullOrEmpty(snap.lineSpeaker) ? "" : DisplayName(snap.lineSpeaker) + ": ";
                _texts[slot].text = name + "   " + Escape(snap.label) + "\n<size=80%><color=#b4b0c4>" + snap.savedAt + "   played " + StorySnapshot.FormatPlayTime(snap.playSeconds)
                    + (snap.parked ? "   (looking around)" : "") + "</color></size>\n<size=85%><i>" + Escape(who + line) + "</i></size>";
            }
        }

        string DisplayName(string id)
        {
            var c = _config != null ? _config.FindCharacter(id) : null;
            return c != null ? c.displayName : id;
        }

        static string Escape(string s) => (s ?? "").Replace("<", "‹").Replace(">", "›");

        void Pick(string slot)
        {
            if (_saving)
            {
                if (slot == StorySlots.Auto) return;
                if (StoryGame.Current != null && StoryGame.Current.SaveTo(slot)) Refresh();
                return;
            }
            if (!StoryStorage.Exists(slot)) return;
            Ui.CloseOverlay();
            StoryGame.LoadSlot(slot);
        }
    }

    /// <summary>Every ending: found ones with their title and text, locked ones with a hint, secret ones hidden.</summary>
    public sealed class EndingsScreen : ShellScreen
    {
        readonly StoryConfig _config;
        Label _heading;
        VisualElement _grid;

        public EndingsScreen(StoryConfig config) { _config = config; }

        protected override bool BuildsOwnContent => true;

        public int CardCount => _grid != null ? _grid.childCount : 0;
        public string HeadingText => _heading != null ? _heading.text : "";

        protected override void OnInit()
        {
            var panel = StoryScreens.Panel(this, Root, _config, "Endings", out _heading, out var body);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("st-scroll");
            body.Add(scroll);
            _grid = new VisualElement();
            _grid.AddToClassList("st-endings");
            scroll.Add(_grid);
            StoryScreens.BackButton(panel);
            Bind("back", () => Ui.CloseOverlay());
        }

        protected override void OnShow()
        {
            _grid.Clear();
            var infos = _config != null ? _config.EndingInfos() : new List<EndingInfo>();
            var memory = StoryStorage.Memory;
            _heading.text = "Endings   " + EndingsRegistry.Found(infos, memory) + " / " + EndingsRegistry.Total(infos);
            foreach (var card in EndingsRegistry.Cards(infos, memory))
            {
                var def = _config.FindEnding(card.id);
                var el = new VisualElement();
                el.AddToClassList("st-ending");
                el.EnableInClassList("st-ending--locked", !card.found);
                var pic = new VisualElement();
                pic.AddToClassList("st-ending-pic");
                if (card.found && def != null && def.image != null) pic.style.backgroundImage = new StyleBackground(def.image);
                pic.style.backgroundColor = card.found && def != null ? def.color : new Color(0.12f, 0.12f, 0.2f);
                el.Add(pic);
                var text = new VisualElement();
                text.AddToClassList("st-ending-text");
                el.Add(text);
                StoryScreens.L("Ending " + card.number + (card.secret && card.found ? "  (secret)" : ""), "st-ending-number", text);
                StoryScreens.L(card.title, "st-ending-title", text);
                if (!string.IsNullOrEmpty(card.description)) StoryScreens.L(card.description, "st-ending-desc", text);
                if (card.found && card.times > 1) StoryScreens.L("Reached " + card.times + " times", "st-ending-desc", text);
                _grid.Add(el);
            }
        }
    }

    /// <summary>The history: every line and choice so far (the last few hundred), newest at the bottom.</summary>
    public sealed class BacklogScreen : ShellScreen
    {
        readonly StoryConfig _config;
        ScrollView _scroll;
        Backlog _log;

        public BacklogScreen(StoryConfig config) { _config = config; }

        protected override bool BuildsOwnContent => true;

        public int EntryCount => _scroll != null ? _scroll.contentContainer.childCount : 0;

        public string EntryText(int index) => index >= 0 && index < EntryCount ? ((Label)_scroll.contentContainer[index]).text : "";

        public void Set(Backlog log) => _log = log;

        protected override void OnInit()
        {
            var panel = StoryScreens.Panel(this, Root, _config, "Story so far", out _, out var body);
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.AddToClassList("st-scroll");
            _scroll.AddToClassList("st-backlog");
            body.Add(_scroll);
            StoryScreens.BackButton(panel);
            Bind("back", () => Ui.CloseOverlay());
        }

        protected override void OnShow()
        {
            _scroll.Clear();
            if (_log == null) return;
            for (int i = 0; i < _log.Count; i++)
            {
                var e = _log[i];
                string text;
                switch (e.kind)
                {
                    case LineKind.Choice: text = "<color=#ffc94a>▸ " + e.text + "</color>"; break;
                    case LineKind.Thought: text = "<i><color=#b4b0c4>" + e.text + "</color></i>"; break;
                    case LineKind.Dialogue:
                    {
                        var def = _config != null ? _config.FindCharacter(e.speaker) : null;
                        string hex = def != null ? ColorUtility.ToHtmlStringRGB(def.nameColor) : "FFC94A";
                        text = "<color=#" + hex + "><b>" + e.speaker + "</b></color>   " + e.text;
                        break;
                    }
                    default: text = e.text; break;
                }
                var l = new Label(text);
                l.AddToClassList("st-backlog-line");
                l.enableRichText = true;
                _scroll.Add(l);
            }
            _scroll.schedule.Execute(() => _scroll.scrollOffset = new Vector2(0f, float.MaxValue)).StartingIn(30);
        }
    }

    /// <summary>Debug: every knot as a button; pick one to jump there.</summary>
    public sealed class KnotScreen : ShellScreen
    {
        readonly StoryConfig _config;
        ScrollView _scroll;
        Action<string> _jump;
        readonly List<string> _knots = new List<string>();

        public KnotScreen(StoryConfig config) { _config = config; }

        protected override bool BuildsOwnContent => true;

        public void Set(IList<string> knots, Action<string> jump)
        {
            _knots.Clear();
            if (knots != null) _knots.AddRange(knots);
            _jump = jump;
        }

        protected override void OnInit()
        {
            var panel = StoryScreens.Panel(this, Root, _config, "Jump to knot", out _, out var body);
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.AddToClassList("st-scroll");
            body.Add(_scroll);
            StoryScreens.BackButton(panel);
            Bind("back", () => Ui.CloseOverlay());
        }

        protected override void OnShow()
        {
            _scroll.Clear();
            foreach (var k in _knots)
            {
                string knot = k;
                var b = new Button(() =>
                {
                    Ui.CloseOverlay();
                    _jump?.Invoke(knot);
                }) { text = knot };
                b.AddToClassList("jk-debug-button");
                b.AddToClassList("st-knot-button");
                _scroll.Add(b);
            }
        }
    }
}

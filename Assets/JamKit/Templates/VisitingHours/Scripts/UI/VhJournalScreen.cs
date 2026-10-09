using System;
using System.Collections.Generic;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// Nadia's journal: a shell overlay (Escape, J or Tab closes it) drawn on a paper page. Tabs for
    /// Facts, Memories, Deductions and Truths (with the personal things people gave her); clicking
    /// an entry reads it and picks it; two picks and Combine try the pair table (a new deduction,
    /// or a gentle line). The open questions (pairs whose halves she has, not yet combined) are
    /// listed at the side. Scribbled-out notes show struck through and faint, clearer on hover.
    /// Buttons have names for tests and the smoke run: journal-tab-facts, journal-entry-fact_chart,
    /// journal-combine, journal-close.
    /// </summary>
    public sealed class VhJournalScreen : ShellScreen
    {
        public enum Tab { Facts, Memories, Deductions, Truths }

        static VhJournalScreen _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _instance = null;

        /// <summary>The journal screen, added to the shell once per game.</summary>
        public static VhJournalScreen Ensure(StyleSheet style)
        {
            if (_instance == null || _instance.Root == null || _instance.Root.panel == null)
                _instance = GameRoot.Ui.AddScreen(new VhJournalScreen(), null);
            if (style != null && !_instance.Root.styleSheets.Contains(style)) _instance.Root.styleSheets.Add(style);
            return _instance;
        }

        protected override bool BuildsOwnContent => true;

        VisualElement _book, _list, _questions, _detailIcon;
        Label _detailTitle, _detailText, _detailScribble, _pickA, _pickB, _result, _tutorial, _questionsHeader;
        Button _combine;
        readonly Dictionary<string, Button> _entryButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
        readonly Button[] _tabs = new Button[4];
        readonly List<Label> _questionLabels = new List<Label>();
        readonly List<OpenQuestion> _open = new List<OpenQuestion>();
        readonly List<string> _picks = new List<string>(2);
        readonly HashSet<string> _seen = new HashSet<string>(StringComparer.Ordinal);

        VisitingHoursGame _game;
        VisitingHoursConfig _config;
        Journal _journal;
        Tab _tab;
        string _shown = "";
        int _shownRevision = -1, _gentle;
        string _lastResult = "";

        /// <summary>The journal closed (Escape, J, Tab, Close, or anything that shut the overlay): the
        /// game hears it at once, even when it opened and closed within one frame.</summary>
        public event Action Closed;

        protected override void OnHide() => Closed?.Invoke();

        public Tab CurrentTab => _tab;
        public bool IsOpen => GameRoot.Exists && GameRoot.Ui.IsOverlayOpen(this);
        public string Shown => _shown;
        public IReadOnlyList<string> Picks => _picks;
        public string LastResult => _lastResult;
        public int OpenQuestionCount => _open.Count;
        /// <summary>The scribbled-out line on the entry being read ("" when none).</summary>
        public string ShownScribble => _detailScribble != null && _detailScribble.style.display != DisplayStyle.None ? _detailScribble.userData as string ?? "" : "";

        protected override void OnInit()
        {
            Root.AddToClassList("vh-journal");
            Root.pickingMode = PickingMode.Position;
            _book = new VisualElement { name = "vh-journal-book" };
            _book.AddToClassList("vh-journal__book");
            Root.Add(_book);

            var header = Row("vh-journal__header");
            _book.Add(header);
            var title = new Label("Nadia's notebook");
            title.AddToClassList("vh-journal__title");
            header.Add(title);
            var tabs = Row("vh-journal__tabs");
            header.Add(tabs);
            string[] names = { "Facts", "Memories", "Deductions", "Truths" };
            for (int i = 0; i < 4; i++)
            {
                var t = (Tab)i;
                var b = new Button { name = "journal-tab-" + names[i].ToLowerInvariant(), text = names[i] };
                b.AddToClassList("vh-journal__tab");
                tabs.Add(b);
                Bind(b.name, () => ShowTab(t));
                _tabs[i] = b;
            }
            var close = new Button { name = "journal-close", text = "Close" };
            close.AddToClassList("vh-journal__close");
            header.Add(close);
            Bind("journal-close", () => Ui.CloseOverlay());

            var body = Row("vh-journal__body");
            _book.Add(body);
            _list = new ScrollView(ScrollViewMode.Vertical) { name = "vh-journal-list" };
            _list.AddToClassList("vh-journal__list");
            body.Add(_list);

            var detail = new VisualElement();
            detail.AddToClassList("vh-journal__detail");
            body.Add(detail);
            _detailIcon = new VisualElement();
            _detailIcon.AddToClassList("vh-journal__detail-icon");
            detail.Add(_detailIcon);
            _detailTitle = new Label("") { name = "journal-detail-title" };
            _detailTitle.AddToClassList("vh-journal__detail-title");
            detail.Add(_detailTitle);
            _detailText = new Label("") { name = "journal-detail-text" };
            _detailText.AddToClassList("vh-journal__detail-text");
            detail.Add(_detailText);
            _detailScribble = new Label("") { name = "journal-detail-scribble", pickingMode = PickingMode.Position };
            _detailScribble.AddToClassList("vh-journal__scribble");
            detail.Add(_detailScribble);

            var side = new VisualElement();
            side.AddToClassList("vh-journal__side");
            body.Add(side);
            _questionsHeader = new Label("Open questions");
            _questionsHeader.AddToClassList("vh-journal__side-title");
            side.Add(_questionsHeader);
            _questions = new VisualElement();
            _questions.AddToClassList("vh-journal__questions");
            side.Add(_questions);
            var combineTitle = new Label("Put two together");
            combineTitle.AddToClassList("vh-journal__side-title");
            side.Add(combineTitle);
            _pickA = new Label("—");
            _pickA.AddToClassList("vh-journal__pick");
            _pickB = new Label("—");
            _pickB.AddToClassList("vh-journal__pick");
            side.Add(_pickA);
            side.Add(_pickB);
            _combine = new Button { name = "journal-combine", text = "Combine" };
            _combine.AddToClassList("vh-journal__combine");
            side.Add(_combine);
            Bind("journal-combine", () => CombinePicked());
            _result = new Label("") { name = "journal-result" };
            _result.AddToClassList("vh-journal__result");
            side.Add(_result);

            _tutorial = new Label("Click an entry to read it, and to pick it. Pick two, then Combine: if they fit, they make something new.\nJ, Tab or Escape closes the notebook.");
            _tutorial.AddToClassList("vh-journal__tutorial");
            _book.Add(_tutorial);
        }

        static VisualElement Row(string cls)
        {
            var e = new VisualElement();
            e.AddToClassList(cls);
            return e;
        }

        /// <summary>Points the journal at this game's state (once per scene).</summary>
        public void Attach(VisitingHoursGame game, VisitingHoursConfig config, Journal journal)
        {
            _game = game;
            _journal = journal;
            _picks.Clear();
            _seen.Clear();
            _shown = "";
            _lastResult = "";
            _shownRevision = -1;
            var page = config.Art("journal_page");
            _book.style.backgroundImage = page != null ? new StyleBackground(page) : new StyleBackground(StyleKeyword.None);
            if (_config != config || _entryButtons.Count == 0)
            {
                _config = config;
                foreach (var b in _entryButtons.Values) b.RemoveFromHierarchy();
                _entryButtons.Clear();
                foreach (var e in config.entries)
                {
                    if (e == null || string.IsNullOrEmpty(e.id) || _entryButtons.ContainsKey(e.id)) continue;
                    var b = new Button { name = "journal-entry-" + e.id };
                    b.AddToClassList("vh-journal__entry");
                    var icon = new VisualElement();
                    icon.AddToClassList("vh-journal__entry-icon");
                    var tex = config.Art(e.icon);
                    if (tex != null) icon.style.backgroundImage = new StyleBackground(tex);
                    b.Add(icon);
                    var label = new Label(e.title);
                    label.AddToClassList("vh-journal__entry-title");
                    b.Add(label);
                    _list.Add(b);
                    string id = e.id;
                    Bind(b.name, () => Pick(id));
                    _entryButtons[id] = b;
                }
            }
            Refresh();
        }

        /// <summary>Opens on a tab (the first time Mags explains it, with the tutorial line).</summary>
        public void Open(Tab tab, bool tutorial)
        {
            if (_journal == null) return;
            _tab = tab;
            _tutorial.style.display = tutorial ? DisplayStyle.Flex : DisplayStyle.None;
            _result.text = "";
            if (!IsOpen) Ui.OpenOverlay(this);
            Refresh();
            FocusFirstEntry();
        }

        public void Close()
        {
            if (IsOpen && Ui.Top == this) Ui.CloseOverlay(false);
        }

        public void ShowTab(Tab tab)
        {
            _tab = tab;
            Refresh();
            FocusFirstEntry();
        }

        void FocusFirstEntry()
        {
            foreach (var e in _config.entries)
            {
                if (e == null || !_entryButtons.TryGetValue(e.id, out var b)) continue;
                if (b.style.display != DisplayStyle.None) { b.Focus(); return; }
            }
            _tabs[(int)_tab].Focus();
        }

        /// <summary>Reads an entry and picks it for combining (picking it again puts it back).</summary>
        public void Pick(string id)
        {
            if (_journal == null || !_journal.Knows(id)) return;
            Read(id);
            if (_picks.Contains(id)) _picks.Remove(id);
            else
            {
                if (_picks.Count >= 2) _picks.RemoveAt(0);
                _picks.Add(id);
            }
            _result.text = "";
            RefreshPicks();
        }

        public void Read(string id)
        {
            var e = _journal != null ? _journal.Book.Find(id) : null;
            if (e == null) return;
            _shown = id;
            _seen.Add(id);
            _detailTitle.text = e.title;
            _detailText.text = e.text;
            var tex = _config.Art(e.icon);
            _detailIcon.style.backgroundImage = tex != null ? new StyleBackground(tex) : new StyleBackground(StyleKeyword.None);
            bool scribble = !string.IsNullOrEmpty(e.scribble);
            _detailScribble.style.display = scribble ? DisplayStyle.Flex : DisplayStyle.None;
            _detailScribble.text = scribble ? "<s>" + e.scribble + "</s>" : "";
            _detailScribble.userData = scribble ? e.scribble : null;
            if (_entryButtons.TryGetValue(id, out var b)) b.RemoveFromClassList("vh-journal__entry--new");
        }

        /// <summary>Tries the two picked entries. Made switches to the new deduction.</summary>
        public CombineResult CombinePicked()
        {
            if (_journal == null) return CombineResult.NotKnown;
            if (_picks.Count < 2)
            {
                _lastResult = "Pick two entries first.";
                _result.text = _lastResult;
                return CombineResult.NotKnown;
            }
            string a = _picks[0], b = _picks[1];
            var r = _journal.Combine(a, b, out string made);
            switch (r)
            {
                case CombineResult.Made:
                    var def = _journal.Book.Find(made);
                    _lastResult = "Worked out: " + (def != null ? def.title : made);
                    _picks.Clear();
                    _tab = def != null && (def.kind == EntryKind.Truth || def.kind == EntryKind.Personal) ? Tab.Truths : Tab.Deductions;
                    Refresh();
                    Read(made);
                    break;
                case CombineResult.AlreadyMade:
                    var known = _journal.Book.Find(made);
                    _lastResult = "I've already worked that out: " + (known != null ? known.title : made) + ".";
                    break;
                default:
                    var lines = _config.gentleLines;
                    _lastResult = lines != null && lines.Count > 0 ? lines[_gentle++ % lines.Count] : "Those don't go together.";
                    break;
            }
            _result.text = _lastResult;
            RefreshPicks();
            if (_game != null) _game.OnCombined(a, b, r, made);
            return r;
        }

        public override void Tick(float unscaledDt)
        {
            if (_journal != null && _journal.Revision != _shownRevision) Refresh();
        }

        public override bool OnBack() => false;

        void Refresh()
        {
            if (_journal == null || _config == null) return;
            _shownRevision = _journal.Revision;
            int[] counts = new int[4];
            foreach (var e in _config.entries)
            {
                if (e == null || !_entryButtons.TryGetValue(e.id, out var b)) continue;
                bool known = _journal.Knows(e.id);
                var tab = TabOf(e.kind);
                if (known) counts[(int)tab]++;
                b.style.display = known && tab == _tab ? DisplayStyle.Flex : DisplayStyle.None;
                b.EnableInClassList("vh-journal__entry--new", known && !_seen.Contains(e.id));
                b.EnableInClassList("vh-journal__entry--open", known && _journal.IsInOpenQuestion(e.id));
            }
            string[] names = { "Facts", "Memories", "Deductions", "Truths" };
            for (int i = 0; i < 4; i++)
            {
                _tabs[i].text = counts[i] > 0 ? names[i] + "  " + counts[i] : names[i];
                _tabs[i].EnableInClassList("vh-journal__tab--on", i == (int)_tab);
            }
            _journal.OpenQuestions(_open);
            while (_questionLabels.Count < _open.Count)
            {
                var l = new Label("");
                l.AddToClassList("vh-journal__question");
                _questions.Add(l);
                _questionLabels.Add(l);
            }
            for (int i = 0; i < _questionLabels.Count; i++)
            {
                bool on = i < _open.Count;
                _questionLabels[i].style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                if (on) _questionLabels[i].text = _open[i].question;
            }
            _questionsHeader.text = _open.Count > 0 ? "Open questions" : "Open questions: none";
            if (_shown.Length == 0 || !_journal.Knows(_shown))
            {
                _detailTitle.text = _journal.Count > 0 ? "" : "Nothing written yet.";
                _detailText.text = "";
                _detailScribble.style.display = DisplayStyle.None;
                _detailIcon.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            }
            RefreshPicks();
        }

        void RefreshPicks()
        {
            _pickA.text = _picks.Count > 0 ? TitleOf(_picks[0]) : "—";
            _pickB.text = _picks.Count > 1 ? TitleOf(_picks[1]) : "—";
            _combine.SetEnabled(_picks.Count == 2);
            foreach (var kv in _entryButtons) kv.Value.EnableInClassList("vh-journal__entry--picked", _picks.Contains(kv.Key));
        }

        string TitleOf(string id)
        {
            var e = _journal.Book.Find(id);
            return e != null ? e.title : id;
        }

        public static Tab TabOf(EntryKind kind)
        {
            switch (kind)
            {
                case EntryKind.Fact: return Tab.Facts;
                case EntryKind.Memory: return Tab.Memories;
                case EntryKind.Deduction: return Tab.Deductions;
                default: return Tab.Truths;
            }
        }
    }
}
